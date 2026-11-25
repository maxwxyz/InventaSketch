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
            public ChoiceOption? CurrentOption => Options.FirstOrDefault(o => o.Value == Current) ?? Options.FirstOrDefault();
            /// <summary>All options have a pictogram or picture: the button shows it instead of text.</summary>
            public bool Pictorial => Options.All(o => o.IconId is not null || o.Icon is not null);
        }

        /// <summary>The bar choices of an object, in order: line type, then its icon parameters, then the line style.</summary>
        private List<BarChoice> BarChoices(SketchObject o, int max = MaxBarChoices)
        {
            var registry = _registry();
            var lang = _lang();
            var list = new List<BarChoice>();
            var scale = _view?.DeviceScale ?? 1;
            // Many views (a gear has five): one view chip with a list instead of a row of view buttons.
            if (registry.Type(o.Type) is { } vt && vt.Views.Count > MaxViewButtons)
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
                    ? new BarButton("choice:" + c.Key, "", tip, Active: _barChoice == c.Key) { IconId = cur?.IconId, Icon = cur?.Icon, Menu = true, Mirror = c.Mirror }
                    : new BarButton("choice:" + c.Key, ShortLabel(cur?.Label ?? c.Tip, 16), tip, Active: _barChoice == c.Key) { Menu = true };
            });

        /// <summary>Opens the dropdown of the open choice under its button (rows: pictogram and label, current marked).</summary>
        private void AttachDropdown(FloatingBar bar, SketchObject o)
        {
            if (_barChoice is null) return;
            var choice = BarChoices(o).FirstOrDefault(c => c.Key == _barChoice);
            var anchor = bar.Buttons.ToList().FindIndex(b => b.Id == "choice:" + _barChoice);
            if (choice is null || anchor < 0) { _barChoice = null; return; }
            bar.SetDropdown(anchor, choice.Options.Select(x => new DropItem("pick:" + choice.Key + ":" + x.Value, x.Label, x.IconId, x.Icon, x.Value == choice.Current, choice.Mirror)).ToList());
        }

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
