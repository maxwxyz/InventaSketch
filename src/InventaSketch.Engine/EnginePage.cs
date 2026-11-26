using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using InventaSketch.Modules.Patent;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Commands of a whole page that need no selected element (hosts show them with their page-level commands: the
    /// COM add-in's "More" menu, the web add-in's top bar, the LibreOffice Tools panel). Today: "Number all parts ▸ 10, 20,
    /// 30 … / 1, 2, 3 …" (<see cref="PartNumbering.PageCommands"/>), offered only on a page with parts and greyed out when
    /// every part already has a numeral. Every command is one document edit (one undo step). Hosts that keep every slide
    /// or page in a model of its own pass the numerals of the other ones (<see cref="OtherPage"/>), so numbering continues
    /// across the document and identical parts keep their numeral.
    /// </summary>
    public static class PageActions
    {
        /// <summary>The page commands of page <paramref name="pageId"/> (empty when none applies: no placeholder commands).</summary>
        public static IReadOnlyList<PageCommand> For(Registry registry, SketchDocument doc, string pageId)
        {
            var page = doc.Pages.FirstOrDefault(p => p.Id == pageId);
            // Numbering needs the numeral type of the figures module.
            if (page is null || registry.Type(PatentIds.Numeral) is null) return Array.Empty<PageCommand>();
            return PartNumbering.PageCommands(registry, doc, page);
        }

        /// <summary>
        /// Runs page command <paramref name="actionId"/> on page <paramref name="pageId"/>; the unchanged document when the
        /// command is greyed out. <paramref name="elsewhere"/>: the numerals of the document's other pages or slides kept
        /// in models of their own.
        /// </summary>
        /// <exception cref="ArgumentException">The page offers no command with that id.</exception>
        public static SketchDocument Run(Registry registry, SketchDocument doc, string pageId, string actionId, Lang lang, IReadOnlyList<OtherPage>? elsewhere = null)
        {
            var command = For(registry, doc, pageId).FirstOrDefault(c => c.Id == actionId)
                ?? throw new ArgumentException($"The page offers no command '{actionId}'.");
            return command.IsEnabled ? command.Run(registry, doc, pageId, Context(lang, elsewhere)) : doc;
        }

        /// <summary>The context of a page command.</summary>
        public static PageCommandContext Context(Lang lang, IReadOnlyList<OtherPage>? elsewhere)
            => new(lang) { Elsewhere = elsewhere ?? Array.Empty<OtherPage>() };

        /// <summary>True if <paramref name="actionId"/> is a page command id ("patent.numerals.auto=tens").</summary>
        public static bool IsPageCommandId(string actionId) => actionId.StartsWith(PartNumbering.ActionKey + ActionChoices.Separator, StringComparison.Ordinal);

        /// <summary>
        /// Pictogram of the menu a page command belongs to, for a host's menu button (<see cref="ActionInfo.ChoiceBarIcon"/>):
        /// "Number all parts" shows the renumber pictogram (numerals in reading order).
        /// </summary>
        public static string MenuIcon(PageCommand command) => command.Choice.Key == PartNumbering.ActionKey ? IconIds.Renumber : IconIds.Option;

        /// <summary>The label of a page command for the undo history ("Number all parts: 10, 20, 30 …").</summary>
        public static I18nText UndoLabel(PageCommand command)
            => new(command.Choice.Label.En + ": " + command.Label.En, command.Choice.Label.Get(Lang.De) + ": " + command.Label.Get(Lang.De));

        /// <summary>The numerals of request field <c>elsewhere</c> as other pages (empty when none is given).</summary>
        public static IReadOnlyList<OtherPage> Elsewhere(IEnumerable<OtherPageInfo>? pages)
            => pages is null ? Array.Empty<OtherPage>()
                : pages.Where(p => p is not null).Select(p => new OtherPage(p.Page ?? "",
                    (p.Numerals ?? new List<NumeralInfo>()).Where(n => n is not null && !string.IsNullOrWhiteSpace(n.Numeral))
                        .Select(n => new OtherNumeral(n.Numeral.Trim(), string.IsNullOrWhiteSpace(n.Label) ? null : n.Label, string.IsNullOrEmpty(n.Kind) ? null : n.Kind)).ToList()))
                  .Where(p => p.Numerals.Count > 0).ToList();

        /// <summary>The numerals of a document for <c>numeralsOf</c> (<see cref="NumeralsElsewhere.Of"/>).</summary>
        public static List<NumeralInfo> Numerals(SketchDocument doc)
            => NumeralsElsewhere.Of(doc).Select(n => new NumeralInfo { Numeral = n.Numeral, Label = n.Label, Kind = n.Kind }).ToList();
    }

    public sealed partial class EngineApi
    {
        private void RegisterPageMethods()
        {
            _methods["pageActions"] = PageActionsCore;
            _methods["applyPageAction"] = ApplyPageActionCore;
            _methods["numeralsOf"] = NumeralsOfCore;
        }

        /// <summary>
        /// The commands of the session's page without a selection (<see cref="PageActions"/>), as actions of kind "page" with
        /// their choice (a menu: <c>choiceMenu</c>; <c>choiceBarIcon</c>: the pictogram of the menu button); an empty list when
        /// the page offers none.
        /// </summary>
        private object PageActionsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            return PageActions.For(Registry, s.Doc, s.PageId).Select(c => new ActionInfo
            {
                Id = c.Id, Label = c.Label.Get(lang), Icon = IconIds.Option, Group = "page", Kind = "page", Enabled = c.IsEnabled,
                Choice = c.Choice.Key, ChoiceLabel = c.Choice.Label.Get(lang), ChoiceMenu = c.Choice.IsMenu, ChoiceBarIcon = PageActions.MenuIcon(c),
            }).ToList();
        }

        /// <summary>
        /// Runs page command <c>action</c> (an id of <c>pageActions</c>) on the session's page, one undo step; returns the
        /// editor state (unchanged when the command is greyed out). <c>elsewhere</c>: the numerals of the other pages or
        /// slides (<c>numeralsOf</c>), so numbering continues after them and identical parts take over their numeral.
        /// </summary>
        private object ApplyPageActionCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Action ?? throw new ArgumentException("Missing 'action'.");
            RunPageCommand(s, id, r);
            return StateOf(s, r);
        }

        /// <summary>Runs page command <paramref name="id"/> on the session's page (one undo step); false if the page offers no such command.</summary>
        private bool TryRunPageCommand(EditorSession s, string id, EngineRequest r)
        {
            var command = PageActions.For(Registry, s.Doc, s.PageId).FirstOrDefault(c => c.Id == id);
            if (command is null) return false;
            if (command.IsEnabled)
            {
                var context = PageActions.Context(s.Lang, PageActions.Elsewhere(r.Elsewhere));
                s.Apply(PageActions.UndoLabel(command).Get(s.Lang), d => command.Run(Registry, d, s.PageId, context));
            }
            return true;
        }

        private void RunPageCommand(EditorSession s, string id, EngineRequest r)
        {
            if (!TryRunPageCommand(s, id, r)) throw new ArgumentException($"The page offers no command '{id}'.");
        }

        /// <summary>
        /// <c>numeralsOf</c> {json = JSON array of compact payloads}: the numerals of each payload without a session
        /// (<see cref="NumeralsElsewhere.Of"/>: numeral, part name, part kind), <c>[[{numeral, label, kind}…], …]</c> in payload
        /// order. A host that keeps one model per unit or slide collects them per slide or page and passes them as
        /// <c>elsewhere</c> to applyPageAction, applyAction and preflight. A payload that cannot be read gives an empty list.
        /// </summary>
        private object NumeralsOfCore(EngineRequest r)
        {
            var payloads = string.IsNullOrWhiteSpace(r.Json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(r.Json!) ?? new List<string>();
            var result = new List<List<NumeralInfo>>(payloads.Count);
            foreach (var payload in payloads)
            {
                try
                {
                    result.Add(PageActions.Numerals(DocumentJson.FromCompact(payload ?? "")));
                }
                catch (Exception ex) when (ex is FormatException or JsonException or System.IO.InvalidDataException or ArgumentException)
                {
                    result.Add(new List<NumeralInfo>());
                }
            }
            return result;
        }
    }
}
