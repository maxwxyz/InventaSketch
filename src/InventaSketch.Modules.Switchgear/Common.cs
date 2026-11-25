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

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>Library category ids of the switchgear module.</summary>
    public static class SwCategories
    {
        public const string Protection = "sw.protection";
        public const string Switching = "sw.switching";
        public const string Supply = "sw.supply";
        public const string Terminals = "sw.terminals";
        public const string Mounting = "sw.mounting";
        public const string Wiring = "sw.wiring";
        public const string House = "sw.house";
    }

    /// <summary>Primitive tags of the module (part keys).</summary>
    public static class SwTags
    {
        public const string Body = "body";
        public const string Front = "front";
        public const string Lever = "lever";
        public const string Terminal = "terminal";
        public const string Text = "text";
        public const string Label = "label";
        public const string Symbol = "symbol";
    }

    /// <summary>
    /// Width of one modular unit ("Teilungseinheit", TE / module) of DIN-rail devices on a TS35 rail: 17.5 mm (the
    /// common pitch of modular installation devices). Devices are multiples of it, so they line up side by side.
    /// </summary>
    public static class Modular
    {
        public const double Unit = 17.5;
        /// <summary>Design height of the front view of a modular installation device.</summary>
        public const double DeviceHeight = 90;
        /// <summary>Height of a TS35 rail.</summary>
        public const double RailHeight = 35;
    }

    /// <summary>Colours used by the front views.</summary>
    internal static class Sx
    {
        public const string Black = "#000000";
        public const string Body = "#eceff1";
        public const string FrontPlate = "#f9fafb";
        public const string LabelField = "#ffffff";
        public const string Lever = "#37474f";
        public const string LeverRed = "#c62828";
        public const string Slot = "#90a4ae";
        public const string Screw = "#cfd8dc";
        public const string On = "#e53935";
        public const string Off = "#43a047";
        public const string TestButton = "#fbc02d";
        public const string Display = "#c5e1a5";
        public const string DisplayFrame = "#33691e";
        public const string Led = "#e53935";
        public const string LedGreen = "#43a047";
        public const string BusRed = "#d32f2f";
        public const string TerminalGrey = "#bdbdbd";
        public const string TerminalBlue = "#1e88e5";
        public const string PeGreen = "#2e7d32";
        public const string PeYellow = "#fdd835";
        public const string Rail = "#cfd8dc";
        public const string Duct = "#cfd8dc";
        public const string Bridge = "#e53935";

        public static Style Fill(string fill) => Style.Visible with { Fill = Core.Fill.Solid(fill) };
        public static Style ThinFill(string fill) => Style.ThinLine with { Fill = Core.Fill.Solid(fill) };
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style Outline = Style.Visible;
        public static Style Solid(string colour) => new() { Role = LineRole.Thin, StrokeWidth = 0.1, Stroke = colour, Fill = Core.Fill.Solid(colour) };
        public static Style Coloured(string colour, double width) => new() { Role = LineRole.Visible, Stroke = colour, StrokeWidth = width };

        public static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>Parameter definition helpers (every parameter is optional).</summary>
    internal static class Pm
    {
        public static I18nText T(string en, string de) => new(en, de);

        public static ParamDef Text(string key, string en, string de, string def = "")
            => new(key, T(en, de), ParamKind.Text) { Default = def };

        public static ParamDef Num(string key, string en, string de, double def, double min, double max, string? unit = null, double? step = null)
            => new(key, T(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, T(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def)
            => new(key, T(en, de), ParamKind.Boolean) { Default = def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, T(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, T(x.En, x.De))).ToList(),
            };

        /// <summary>Choice whose option labels are the values themselves (ratings, voltages).</summary>
        public static ParamDef Values(string key, string en, string de, string def, params string[] values)
            => Choice(key, en, de, def, values.Select(v => (v, v, v)).ToArray());

        // Shared parameters.
        public static readonly ParamDef Ref = Text("ref", "Designation (e.g. -F1)", "Kennzeichen (z. B. -F1)");
        public static readonly ParamDef Circuit = Text("circuit", "Circuit name", "Stromkreis (Bezeichnung)");
        public static readonly ParamDef Room = Text("room", "Room", "Raum");
        public static readonly ParamDef Feed = Text("feed", "Fed from (designation)", "Gespeist von (Kennzeichen)");
        public static readonly ParamDef On = Bool("on", "Switched on", "Eingeschaltet", true);
    }

    /// <summary>
    /// Drawing in a design box: the drawing is authored for <c>design</c> (mm, centred at 0,0) and stretched to the object
    /// size, so free non-uniform scaling never changes parameters. Circles and text sizes follow the smaller scale.
    /// </summary>
    internal sealed class Pen
    {
        public Pen(Size2 size, Size2 design)
        {
            SX = design.W > Geo.Eps ? size.W / design.W : 1;
            SY = design.H > Geo.Eps ? size.H / design.H : 1;
            W = design.W; H = design.H;
        }

        public double SX { get; }
        public double SY { get; }
        /// <summary>Design width and height.</summary>
        public double W { get; }
        public double H { get; }
        public double K => Math.Min(SX, SY);
        public List<Primitive> Items { get; } = new();

        public Vec2 P(double x, double y) => new(x * SX, y * SY);

        public void Add(Primitive p, string? tag) => Items.Add(tag is null ? p : p with { Tag = tag });

        public void Rect(double x, double y, double w, double h, Style s, double r = 0, string? tag = SwTags.Body)
            => Add(Prim.Rect(x * SX, y * SY, w * SX, h * SY, s, r * K), tag);

        public void CRect(double cx, double cy, double w, double h, Style s, double r = 0, string? tag = SwTags.Body)
            => Rect(cx - w / 2, cy - h / 2, w, h, s, r, tag);

        public void Line(double x1, double y1, double x2, double y2, Style s, string? tag = SwTags.Body)
            => Add(Prim.Line(P(x1, y1), P(x2, y2), s), tag);

        public void Circle(double cx, double cy, double r, Style s, string? tag = SwTags.Body)
            => Add(Prim.Circle(cx * SX, cy * SY, r * K, s), tag);

        public void Ellipse(double cx, double cy, double rx, double ry, Style s, string? tag = SwTags.Body)
            => Add(Prim.Ellipse(cx * SX, cy * SY, rx * SX, ry * SY, s), tag);

        public void Poly(Style s, bool closed, string? tag, params (double X, double Y)[] pts)
            => Add(Prim.Polyline(pts.Select(p => P(p.X, p.Y)), s, closed), tag);

        public void Arc(double cx, double cy, double r, double startDeg, double sweepDeg, Style s, string? tag = SwTags.Body)
            => Add(Prim.Arc(cx * SX, cy * SY, r * K, r * K, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg), s), tag);

        public void Text(double x, double y, string text, double size, bool bold = false, TextAnchor anchor = TextAnchor.Middle, string? colour = null, string? tag = SwTags.Text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Add(Prim.Text(x * SX, y * SY, text, new TextStyle { Size = size * K, Bold = bold, Anchor = anchor, Baseline = TextBaseline.Middle, Color = colour }), tag);
        }

        /// <summary>Text that fits into <paramref name="maxWidth"/> design mm: smaller first, then cut with an ellipsis.</summary>
        public void FitText(double x, double y, string text, double size, double maxWidth, bool bold = false, string? colour = null, string? tag = SwTags.Label)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return;
            var (t, s) = Fit(text, size, maxWidth);
            Text(x, y, t, s, bold, TextAnchor.Middle, colour, tag);
        }

        /// <summary>Approximate text width (design mm) of <paramref name="text"/> at font size <paramref name="size"/>.</summary>
        public static double TextWidth(string text, double size) => text.Length * size * 0.58;

        public static (string Text, double Size) Fit(string text, double size, double maxWidth)
        {
            var s = size;
            var min = size * 0.6;
            while (s > min && TextWidth(text, s) > maxWidth) s -= size * 0.05;
            if (TextWidth(text, s) <= maxWidth) return (text, s);
            var n = Math.Max(1, (int)Math.Floor(maxWidth / (s * 0.58)) - 1);
            return (n >= text.Length ? text : text.Substring(0, n) + "…", s);
        }
    }

    internal static class Num
    {
        /// <summary>Parses a typed number: "16", "2,5", "16 A". Throws <see cref="FormatException"/>.</summary>
        public static double Parse(string? text)
        {
            var s = new string((text ?? "").Trim().Replace(',', '.').TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (s.Length == 0 || !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
                throw new FormatException("Not a number: " + text);
            return v;
        }

        public static double ParseOr(string? text, double fallback)
        {
            try { return Parse(text); } catch (FormatException) { return fallback; }
        }
    }
}
