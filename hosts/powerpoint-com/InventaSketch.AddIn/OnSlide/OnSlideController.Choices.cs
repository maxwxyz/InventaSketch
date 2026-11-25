using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Choices right in the floating bar as dropdowns: every choice of the selected element (line type, arrowheads, head
    /// size, line style, line course, machine arrangement and type, hub …) is a button showing its current value as a
    /// pictogram (<see cref="ParamDef.HasOptionIcons"/>) or a short text, with a caret. A click opens a list under it with
    /// pictogram and label of every option, the current one marked, so it is clear what comes; a click on a row picks.
    /// Cycle actions over the same parameters (<see cref="ContextAction.ParamKeys"/>) leave the bar.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Choice whose icon row the bar shows ("@type", "@dash" or a parameter key); null: the normal bar.</summary>
        private string? _barChoice;

        /// <summary>At most this many choice buttons in the bar.</summary>
        internal const int MaxBarChoices = 6;

        /// <summary>Up to this many views are single buttons in the bar; more become one view chip with a list.</summary>
        internal const int MaxViewButtons = 3;

        internal sealed record ChoiceOption(string Value, string Label, string? IconId, Bitmap? Icon);

        internal sealed record BarChoice(string Key, string Tip, string Current, bool Mirror, IReadOnlyList<ChoiceOption> Options)
        {
            public ChoiceOption? CurrentOption => IsMenu ? null : Options.FirstOrDefault(o => o.Value == Current) ?? Options.FirstOrDefault();
            /// <summary>All options have a pictogram or picture: the button shows it instead of text.</summary>
            public bool Pictorial => !IsMenu && !LabelInBar && (BarIcon is not null || Options.All(o => o.IconId is not null || o.Icon is not null));
            /// <summary>The options are commands (nothing current): "Fill to ▸", "Add area ▸".</summary>
            public bool IsMenu { get; init; }
            /// <summary>The button shows the current option's name, not its picture (<see cref="ActionChoice.LabelInBar"/>).</summary>
            public bool LabelInBar { get; init; }
            /// <summary>Pictogram of the choice itself for the closed button (<see cref="ActionChoice.BarIcon"/>); null: the current option's.</summary>
            public string? BarIcon { get; init; }
        }

        /// <summary>Option pictures rendered from engine previews (<see cref="ContextAction.Preview"/>), by action id and size.</summary>
        private readonly Dictionary<string, Bitmap> _previewCache = new(StringComparer.Ordinal);

        private Bitmap? PreviewBitmap(ContextAction a, int size)
        {
            if (a.Preview is not { Count: > 0 } prims) return null;
            var key = a.Id + "#" + size + "#" + prims.Count + "#" + (Prim.Bounds(prims)?.ToString() ?? "");
            if (_previewCache.TryGetValue(key, out var bmp)) return bmp;
            if (_previewCache.Count > 400) { foreach (var b in _previewCache.Values) b.Dispose(); _previewCache.Clear(); }
            try
            {
                // As thick as the bar pictograms (IconPainter: 1.7 on its 24 grid), so pictures and pictograms look alike.
                bmp = ThumbnailRenderer.Render(prims, size, Color.FromArgb(40, 40, 40), Color.Transparent, 1.7f * size / 24f);
            }
            catch (Exception ex)
            {
                Log.Error("Option preview", ex);
                return null;
            }
            _previewCache[key] = bmp;
            return bmp;
        }

        /// <summary>
        /// The choices among <paramref name="actions"/> (<see cref="ActionChoice"/>: curve shape, line style, pattern …) as bar
        /// choices with keys <paramref name="prefix"/> + choice key; options keep their pictogram, else their preview picture.
        /// </summary>
        private List<BarChoice> ActionBarChoices(IEnumerable<ContextAction> actions, string prefix)
        {
            var lang = _lang();
            var size = (int)Math.Round(26 * (_view?.DeviceScale ?? 1));
            return ActionChoiceBar.Entries(actions).Where(e => e.IsChoice).Select(e =>
            {
                var options = e.Options.Select(a => new ChoiceOption(a.Id, a.Label.Get(lang) + (a.Input is not null && !a.Label.Get(lang).EndsWith("…", StringComparison.Ordinal) ? " …" : ""),
                    IsTelling(a.Icon) ? a.Icon : null, IsTelling(a.Icon) ? null : PreviewBitmap(a, size))).ToList();
                var current = e.Options.FirstOrDefault(a => a.IsActive)?.Id ?? "";
                return new BarChoice(ActionChoiceBar.Key(prefix, e.Choice!), e.Choice!.Label.Get(lang), current, false, options) { IsMenu = e.Choice.IsMenu, LabelInBar = e.Choice.LabelInBar, BarIcon = IsTelling(e.Choice.BarIcon ?? "") ? e.Choice.BarIcon : null };
            }).ToList();
        }

        /// <summary>The choices of the selected part (empty without a part selection).</summary>
        private List<BarChoice> PartChoices(SketchObject o) => _part is null ? new List<BarChoice>() : ActionBarChoices(PartActions(o), ActionChoiceBar.PartPrefix);

        /// <summary>The element's action choices that are no parameter choice ("Add area ▸" …).</summary>
        private List<BarChoice> ElementActionChoices(SketchObject o)
        {
            var paramKeys = new HashSet<string>(BarChoices(o, int.MaxValue).Select(c => c.Key));
            var actions = UnitActions(out _).Where(a => a.Choice is not null && (a.ParamKeys is null || !a.ParamKeys.Any(paramKeys.Contains)));
            return ActionBarChoices(actions, ActionChoiceBar.ElementPrefix);
        }

        /// <summary>Every choice of the armed element: its bar choices, its action choices and those of the selected part.</summary>
        private List<BarChoice> AllChoices(SketchObject o) => BarChoices(o, int.MaxValue).Concat(ElementActionChoices(o)).Concat(PartChoices(o)).ToList();

        /// <summary>The bar choices of an object, in order: line type, then its icon parameters, then the line style.</summary>
        private List<BarChoice> BarChoices(SketchObject o, int max = MaxBarChoices)
        {
            var registry = _registry();
            var lang = _lang();
            var list = new List<BarChoice>();
            var scale = _view?.DeviceScale ?? 1;
            // Several views: one view chip with a list (the bar has room for one view button only).
            if (registry.Type(o.Type) is { } vt && vt.Views.Count > 1)
                list.Add(new BarChoice("@view", T("View", "Ansicht"), vt.ViewOf(o), false,
                    vt.Views.Select(v => new ChoiceOption(v.Id, v.Label.Get(lang), IconPainter.ForView(v), null)).ToList()));
            if (o.IsPointGeometry && Registry.IsConvertible(o))
            {
                var options = Registry.ConversionTargets
                    .Select(id => (Id: id, Type: registry.Type(id), Item: registry.Library.FirstOrDefault(i => i.TypeId == id)))
                    .Where(x => x.Type is { Geometry: GeometryKind.Points } && x.Item is not null)
                    .Select(x => new ChoiceOption(x.Id, x.Type!.Name.Get(lang), null, Thumb(x.Item!, (int)Math.Round(26 * scale))))
                    .ToList();
                if (options.Count > 1) list.Add(new BarChoice("@type", T("Line type", "Linientyp"), o.Type, false, options));
            }
            if (registry.Type(o.Type) is { } type)
            {
                // Choices with pictograms first, then the other choices (as short text), in parameter order.
                var choices = type.Parameters.Where(p => p.Kind == ParamKind.Choice && p.Options is { Count: > 1 } && !p.Advanced).ToList();
                foreach (var pd in choices.Where(p => p.HasOptionIcons).Concat(choices.Where(p => !p.HasOptionIcons)))
                {
                    var current = o.Str(pd.Key, pd.Default?.Text ?? pd.Options![0].Value);
                    var pictorial = pd.HasOptionIcons;
                    list.Add(new BarChoice(pd.Key, pd.Label.Get(lang), current, pd.MirrorIcons,
                        pd.Options!.Select(x => new ChoiceOption(x.Value, x.Label.Get(lang), pictorial ? x.Icon : null, null)).ToList()));
                }
            }
            if (o.IsPointGeometry)
            {
                var dashes = new[] { DashStyle.Solid, DashStyle.Dash, DashStyle.Dot, DashStyle.DashDot, DashStyle.LongDash, DashStyle.DashDotDot };
                string Name(DashStyle d) => d switch
                {
                    DashStyle.Dash => T("Dashed", "Gestrichelt"),
                    DashStyle.Dot => T("Dotted", "Gepunktet"),
                    DashStyle.DashDot => T("Dash-dot", "Strichpunkt"),
                    DashStyle.LongDash => T("Long dashes", "Lange Striche"),
                    DashStyle.DashDotDot => T("Dash-dot-dot", "Strich-Punkt-Punkt"),
                    _ => T("Solid", "Durchgezogen"),
                };
                list.Add(new BarChoice("@dash", T("Line style", "Linienart"), (o.Style?.Dash ?? DashStyle.Solid).ToString(), false,
                    dashes.Select(d => new ChoiceOption(d.ToString(), Name(d), IconIds.ForDash(d), null)).ToList()));
            }
            return list.Take(max).ToList();
        }

        /// <summary>Dropdown buttons for the bar: the current option's pictogram (or a short text) plus a caret.</summary>
        private IEnumerable<BarButton> ChoiceButtons(SketchObject o)
            => BarChoices(o).Select(c =>
            {
                var cur = c.CurrentOption;
                var tip = c.Tip + (cur is null ? "" : ": " + cur.Label) + T(" – click to choose", " – klicken zum Wählen");
                return c.Pictorial
                    ? new BarButton("choice:" + c.Key, "", tip, Active: _barChoice == c.Key) { IconId = c.BarIcon ?? cur?.IconId, Icon = c.BarIcon is null ? cur?.Icon : null, Menu = true, Mirror = c.Mirror }
                    : new BarButton("choice:" + c.Key, ShortLabel(cur?.Label ?? c.Tip, 16), tip, Active: _barChoice == c.Key) { Menu = true };
            });

        /// <summary>Opens the dropdown of the open choice under its button (rows: pictogram and label, current marked).</summary>
        private void AttachDropdown(FloatingBar bar, SketchObject o)
        {
            if (_barChoice is null) return;
            var choice = AllChoices(o).FirstOrDefault(c => c.Key == _barChoice);
            var anchor = bar.Buttons.ToList().FindIndex(b => b.Id == "choice:" + _barChoice);
            if (choice is null || anchor < 0) { _barChoice = null; return; }
            var items = choice.Options.Select(x => new DropItem("pick:" + choice.Key + ":" + x.Value, x.Label, x.IconId, x.Icon, !choice.IsMenu && x.Value == choice.Current, choice.Mirror)).ToList();
            bar.SetDropdown(anchor, items);
            LastDropItems = string.Join("\n", items.Select(i => i.Id + "\t" + i.Label + (i.Active ? "\t(current)" : "") + (i.IconId is null && i.Icon is null ? "" : "\t(picture)")));
            Log.Info("Dropdown [" + choice.Tip + "]: " + string.Join("; ", items.Select(i => i.Id + "=" + i.Label + (i.Active ? " (current)" : ""))));
        }

        /// <summary>Rows of the open bar dropdown as last shown: "id\tlabel[\t(current)][\t(picture)]" per line (UI tests).</summary>
        internal string LastDropItems { get; private set; } = "";

        /// <summary>
        /// Bar commands of the choices: "choice:&lt;key&gt;" opens (or with an empty key closes) the icon row without an edit;
        /// "pick:&lt;key&gt;:&lt;value&gt;" closes it and returns the edit command (conversion, parameter or line style).
        /// Returns null when the command was handled here.
        /// </summary>
        private string? ChoiceCommand(string id)
        {
            if (id.StartsWith("choice:", StringComparison.Ordinal))
            {
                var key = id.Substring(7);
                _barChoice = key.Length == 0 || _barChoice == key ? null : key;
                if (_view is not null) ShowBar(_view);
                return null;
            }
            if (!id.StartsWith("pick:", StringComparison.Ordinal)) return id;
            _barChoice = null;
            var rest = id.Substring(5);
            var cut = rest.IndexOf(':');
            if (cut < 0) return null;
            var k = rest.Substring(0, cut);
            var value = rest.Substring(cut + 1);
            // Choices among actions (a part's curve shape, pattern …; the element's "Add area ▸"): run that option.
            if (ActionChoiceBar.Command(k, value) is { } run) return run;
            return k switch
            {
                "@type" => "conv:" + value,
                "@view" => "view:" + value,
                "@dash" => "dash:" + value,
                _ => "param:" + k + ":" + value,
            };
        }
    }
}
