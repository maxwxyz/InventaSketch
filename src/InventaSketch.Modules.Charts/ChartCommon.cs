using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif

namespace InventaSketch.Modules.Charts
{
    /// <summary>Library category ids of the charts module.</summary>
    public static class ChartCategories
    {
        public const string Data = "chart.data";
        public const string Engineering = "chart.eng";
    }

    /// <summary>
    /// Semantic primitive tags used by the chart types. A tag is also the part key (charts v2, <see cref="PartLooks"/>):
    /// kind plus what it belongs to – "series.0", "marker.1", "bar.2", "axis.x", "axis.y.2" (second panel), "label.x",
    /// "tick.y", "grid.major", "axistitle.y". A look on the kind ("series", "grid") applies to all of them.
    /// </summary>
    public static class ChartTags
    {
        /// <summary>True if <paramref name="tag"/> is of <paramref name="kind"/> ("series.0" is a "series").</summary>
        public static bool Is(string? tag, string kind) =>
            tag is not null && (tag == kind || tag.Length > kind.Length && tag[kind.Length] == '.' && tag.StartsWith(kind, StringComparison.Ordinal));

        /// <summary>The key of the k-th item of a kind ("series.0").</summary>
        public static string Nth(string kind, int k) => kind + "." + k.ToString(CultureInfo.InvariantCulture);

        public const string Axis = "axis";
        public const string Tick = "tick";
        public const string Grid = "grid.major";
        public const string GridMinor = "grid.minor";
        public const string Zero = "zero";
        public const string TickLabel = "label";
        public const string AxisTitle = "axistitle";
        public const string Title = "title";
        public const string Series = "series";
        public const string Marker = "marker";
        public const string Bar = "bar";
        public const string Band = "band";
        public const string Limit = "limit";
        public const string Contour = "contour";
        public const string ContourLabel = "contourlabel";
        public const string Envelope = "envelope";
        public const string Helper = "helper";
        public const string Point = "point";
        public const string Note = "note";
        public const string Legend = "legend";
        public const string LegendSample = "legend.sample";
        public const string LegendText = "legend.text";
        public const string ValueLabel = "value";
    }

    /// <summary>Marker shapes of data series (distinguishable without colour).</summary>
    internal enum MarkerKind { Circle, Square, Triangle, Diamond, Cross, Plus }

    /// <summary>
    /// Look of the k-th data series: a dark colour (luminance below 0.35, so it stays black in monochrome output), a dash
    /// pattern, a marker and a fill pattern for areas (bars) in monochrome output. The sequence is chosen so series stay
    /// distinguishable without colour (ISO 128 style dash patterns and different markers).
    /// </summary>
    internal readonly record struct SeriesLook(string Color, DashStyle Dash, MarkerKind Marker, bool FilledMarker, Fill MonoFill)
    {
        public static readonly string[] Colors = { "#1f4e9a", "#b03a2e", "#1b5e20", "#6a1b9a", "#a84300", "#00695c" };
        private static readonly DashStyle[] Dashes = { DashStyle.Solid, DashStyle.Dash, DashStyle.Dot, DashStyle.DashDot, DashStyle.LongDash, DashStyle.DashDotDot };
        private static readonly MarkerKind[] Markers = { MarkerKind.Circle, MarkerKind.Square, MarkerKind.Triangle, MarkerKind.Diamond, MarkerKind.Cross, MarkerKind.Plus };

        public static SeriesLook Of(int i)
        {
            var k = ((i % 6) + 6) % 6;
            Fill mono = k switch
            {
                0 => Fill.Solid(Palette.Black),
                1 => Fill.Solid(Palette.White),
                2 => Fill.Hatched(new HatchPattern(45, 1.2)),
                3 => Fill.Hatched(new HatchPattern(135, 1.2)),
                4 => Fill.Hatched(new HatchPattern(45, 1.4, Cross: true)),
                _ => Fill.Dotted(1.0),
            };
            return new SeriesLook(Colors[k], Dashes[k], Markers[k], k % 2 == 0, mono);
        }

        /// <summary>Line style of the series (visible line, own colour and dash pattern).</summary>
        public Style Line(double width) => new() { Stroke = Color, StrokeWidth = width, Dash = Dash, Role = LineRole.Visible };

        /// <summary>Area style of the series: solid colour, or the monochrome pattern (also when patterns are forced).</summary>
        public Style Area(bool mono, double width) => new()
        {
            Stroke = mono ? Palette.Black : Color,
            StrokeWidth = width,
            Role = LineRole.Visible,
            Fill = mono ? MonoFill : Fill.Solid(Color),
        };
    }

    /// <summary>Colours of axes, grid, text and helper elements.</summary>
    internal static class Palette
    {
        public const string Black = "#000000";
        public const string White = "#ffffff";
        public const string Ink = "#222222";
        public const string Axis = "#222222";
        public const string Grid = "#bdbdbd";
        public const string GridMinor = "#e0e0e0";
        public const string Helper = "#555555";
        public const string Band = "#d6e4f5";
        public const string LegendBorder = "#808080";
    }

    /// <summary>Shared line styles (ISO 128 line roles).</summary>
    internal static class S
    {
        public static Style Axis => new() { Stroke = Palette.Axis, StrokeWidth = 0.35, Role = LineRole.Visible };
        public static Style Tick => new() { Stroke = Palette.Axis, StrokeWidth = 0.25, Role = LineRole.Thin };
        public static Style Grid(bool mono) => new() { Stroke = Palette.Grid, StrokeWidth = 0.13, Role = LineRole.Thin, Dash = mono ? DashStyle.Dot : DashStyle.Solid };
        public static Style GridMinor(bool mono) => new() { Stroke = Palette.GridMinor, StrokeWidth = 0.1, Role = LineRole.Thin, Dash = DashStyle.Dot };
        public static Style Zero => new() { Stroke = Palette.Helper, StrokeWidth = 0.25, Role = LineRole.Thin };
        /// <summary>Helper lines (drop lines to the axes, corner speed): dotted helper role.</summary>
        public static Style Helper => new() { Stroke = Palette.Helper, StrokeWidth = 0.25, Role = LineRole.Helper, Dash = DashStyle.Dash };
        public static Style NoStroke => new() { Stroke = "none", Role = LineRole.Visible };
        public static Style LegendBox => new() { Stroke = Palette.LegendBorder, StrokeWidth = 0.18, Role = LineRole.Thin, Fill = Fill.Solid(Palette.White) };
        public static Style FillOnly(string color) => NoStroke with { Fill = Fill.Solid(color) };
    }

    /// <summary>Parameter definition and action helpers (all parameters are optional).</summary>
    internal static class Prm
    {
        public static ParamDef Num(string key, string en, string de, double def, double min, double max, string? unit = null, double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Bool(string key, string en, string de, bool def, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def, Advanced = advanced };

        /// <summary>Text parameter; <paramref name="def"/> null means "automatic" (the type supplies a language-dependent default).</summary>
        public static ParamDef Text(string key, string en, string de, string? def = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def is null ? null : (ParamValue?)def, Advanced = advanced };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        public static ContextAction Toggle(SketchObject o, string id, string key, bool def, I18nText label)
            => new(id, label, IconIds.Toggle, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = "param", ParamKeys = new[] { key } };

        /// <summary>Cycles a choice parameter through its options; the label names the next option ("Style: step ▸").</summary>
        public static ContextAction Cycle(SketchObject o, string id, ParamDef def, I18nText prefix)
        {
            var options = def.Options ?? Array.Empty<ParamOption>();
            var fallback = def.Default?.Text ?? (options.Count > 0 ? options[0].Value : "");
            var cur = o.Str(def.Key, fallback);
            var idx = options.ToList().FindIndex(p => p.Value == cur);
            var next = options.Count == 0 ? null : options[(idx + 1) % options.Count];
            var label = next is null ? prefix : new I18nText(prefix.En + ": " + next.Label.En, (prefix.De ?? prefix.En) + ": " + (next.Label.De ?? next.Label.En));
            return new ContextAction(id, label, IconIds.Cycle, x =>
            {
                if (options.Count == 0) return x;
                var c = x.Str(def.Key, fallback);
                var i = options.ToList().FindIndex(p => p.Value == c);
                return x.WithParam(def.Key, options[(i + 1) % options.Count].Value);
            }) { Group = "param", ParamKeys = new[] { def.Key } };
        }
    }

    /// <summary>Number formatting (decimal comma in German) and text measuring.</summary>
    internal static class Fmt
    {
        public const double CharWidth = 0.55;

        /// <summary>Shortest representation with at most <paramref name="decimals"/> decimals ("0.25", "1", "1,5").</summary>
        public static string Num(double v, Lang lang, int decimals = 3)
        {
            var s = Math.Round(v, Math.Max(0, Math.Min(12, decimals))).ToString(decimals <= 0 ? "0" : "0." + new string('#', decimals), CultureInfo.InvariantCulture);
            if (s == "-0") s = "0";
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>Number with a fixed count of decimals ("2.50"), decimal comma in German.</summary>
        public static string Fixed(double v, Lang lang, int decimals)
        {
            var s = Math.Round(v, Math.Max(0, Math.Min(12, decimals))).ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture);
            if (s.StartsWith("-", StringComparison.Ordinal) && s.Trim('-', '0', '.') == "") s = s.Substring(1);
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>Estimated width of a single-line text (mm).</summary>
        public static double Width(string text, double size) => Math.Max(1, text.Length) * size * CharWidth;

        public static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

        public static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>
        /// Parses a number for an input action ("2.5", "2,5", "1 kHz", "2.5k"); SI prefixes k, M, m, µ/u are accepted, a unit
        /// suffix in <paramref name="units"/> is ignored. Throws a bilingual <see cref="FormatException"/> otherwise.
        /// </summary>
        public static double ParseInput(string text, Lang lang, double min, double max, params string[] units)
        {
            var t = (text ?? "").Trim();
            foreach (var u in units.OrderByDescending(u => u.Length))
                if (u.Length > 0 && t.EndsWith(u, StringComparison.OrdinalIgnoreCase)) { t = t.Substring(0, t.Length - u.Length).Trim(); break; }
            var factor = 1.0;
            if (t.Length > 1)
            {
                var last = t[t.Length - 1];
                var f = last switch { 'k' or 'K' => 1e3, 'M' => 1e6, 'm' => 1e-3, 'µ' or 'u' => 1e-6, _ => 0.0 };
                if (f > 0) { factor = f; t = t.Substring(0, t.Length - 1).Trim(); }
            }
            if (!ChartData.TryNumber(t, out var v) || !Finite(v * factor) || v * factor < min || v * factor > max)
                throw new FormatException(lang == Lang.De ? "Keine gültige Zahl: „" + text + "“" : "Not a valid number: “" + text + "”");
            return v * factor;
        }
    }
}
