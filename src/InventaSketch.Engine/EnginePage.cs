using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Patent;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Commands of a whole page that need no selected element (hosts show them with their page-level commands: the
    /// COM add-in's "More" menu, the web add-in's top bar, the LibreOffice Tools panel). Today: "Number all parts ▸ 10, 20,
    /// 30 … / 1, 2, 3 …" (<see cref="PartNumbering.PageCommands"/>), offered only on a page with parts and greyed out when
    /// every part already has a numeral. Every command is one document edit (one undo step).
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
        /// command is greyed out.
        /// </summary>
        /// <exception cref="ArgumentException">The page offers no command with that id.</exception>
        public static SketchDocument Run(Registry registry, SketchDocument doc, string pageId, string actionId, Lang lang)
        {
            var command = For(registry, doc, pageId).FirstOrDefault(c => c.Id == actionId)
                ?? throw new ArgumentException($"The page offers no command '{actionId}'.");
            return command.IsEnabled ? command.Run(registry, doc, pageId, lang) : doc;
        }

        /// <summary>
        /// Pictogram of the menu a page command belongs to, for a host's menu button (<see cref="ActionInfo.ChoiceBarIcon"/>):
        /// "Number all parts" shows the renumber pictogram (numerals in reading order).
        /// </summary>
        public static string MenuIcon(PageCommand command) => command.Choice.Key == PartNumbering.ActionKey ? IconIds.Renumber : IconIds.Option;

        /// <summary>The label of a page command for the undo history ("Number all parts: 10, 20, 30 …").</summary>
        public static I18nText UndoLabel(PageCommand command)
            => new(command.Choice.Label.En + ": " + command.Label.En, command.Choice.Label.Get(Lang.De) + ": " + command.Label.Get(Lang.De));
    }

    public sealed partial class EngineApi
    {
        private void RegisterPageMethods()
        {
            _methods["pageActions"] = PageActionsCore;
            _methods["applyPageAction"] = ApplyPageActionCore;
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
        /// editor state (unchanged when the command is greyed out).
        /// </summary>
        private object ApplyPageActionCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Action ?? throw new ArgumentException("Missing 'action'.");
            var command = PageActions.For(Registry, s.Doc, s.PageId).FirstOrDefault(c => c.Id == id)
                ?? throw new ArgumentException($"The page offers no command '{id}'.");
            if (command.IsEnabled)
                s.Apply(PageActions.UndoLabel(command).Get(s.Lang), d => command.Run(Registry, d, s.PageId, s.Lang));
            return StateOf(s, r);
        }
    }
}
