using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Choices right in the floating bar, with pictograms instead of text: a selected line, arrow, lead line or dimension
    /// shows its line type, arrowheads (start, end, size), line style and line course as icon buttons with a small caret;
    /// a click turns the bar into the icon row of that choice, a second click picks (no pie menu, no submenu). Every
    /// choice parameter whose options carry pictograms (<see cref="ParamDef.HasOptionIcons"/>) takes part, so modules opt
    /// in by adding option icons.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Choice whose icon row the bar shows ("@type", "@dash" or a parameter key); null: the normal bar.</summary>
        private string? _barChoice;

        /// <summary>At most this many choice buttons in the bar.</summary>
        internal const int MaxBarChoices = 6;

        internal sealed record ChoiceOption(string Value, string Label, string? IconId, Bitmap? Icon);

        internal sealed record BarChoice(string Key, string Tip, string Current, bool Mirror, IReadOnlyList<ChoiceOption> Options)
        {
            public ChoiceOption? CurrentOption => Options.FirstOrDefault(o => o.Value == Current) ?? Options.FirstOrDefault();
        }

        /// <summary>The bar choices of an object, in order: line type, then its icon parameters, then the line style.</summary>
        private List<BarChoice> BarChoices(SketchObject o)
        {
            var registry = _registry();
            var lang = _lang();
            var list = new List<BarChoice>();
            var scale = _view?.DeviceScale ?? 1;
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
                foreach (var pd in type.Parameters.Where(p => p.HasOptionIcons))
                {
                    var current = o.Str(pd.Key, pd.Default?.Text ?? pd.Options![0].Value);
                    list.Add(new BarChoice(pd.Key, pd.Label.Get(lang), current, pd.MirrorIcons,
                        pd.Options!.Select(x => new ChoiceOption(x.Value, x.Label.Get(lang), x.Icon, null)).ToList()));
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
            return list.Take(MaxBarChoices).ToList();
        }

        /// <summary>Choice buttons for the normal bar: the current option's pictogram plus a caret.</summary>
        private IEnumerable<BarButton> ChoiceButtons(SketchObject o)
            => BarChoices(o).Select(c =>
            {
                var cur = c.CurrentOption;
                return new BarButton("choice:" + c.Key, "", c.Tip + (cur is null ? "" : ": " + cur.Label)) { IconId = cur?.IconId, Icon = cur?.Icon, Menu = true, Mirror = c.Mirror };
            });

        /// <summary>The open choice as the whole bar: back, then one icon per option (the current one highlighted).</summary>
        private List<BarButton>? OpenChoiceButtons(SketchObject o)
        {
            if (_barChoice is null) return null;
            var choice = BarChoices(o).FirstOrDefault(c => c.Key == _barChoice);
            if (choice is null) { _barChoice = null; return null; }
            var list = new List<BarButton> { new("choice:", "‹", T("Back", "Zurück")), new("", "", "", Separator: true) };
            list.AddRange(choice.Options.Select(x => new BarButton("pick:" + choice.Key + ":" + x.Value, "", x.Label, Active: x.Value == choice.Current)
            {
                IconId = x.IconId, Icon = x.Icon, Mirror = choice.Mirror,
            }));
            return list;
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
                "@dash" => "dash:" + value,
                _ => "param:" + k + ":" + value,
            };
        }
    }
}
