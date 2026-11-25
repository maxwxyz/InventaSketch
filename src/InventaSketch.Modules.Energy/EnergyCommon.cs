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

namespace InventaSketch.Modules.Energy
{
    /// <summary>Library category ids of the energy systems module.</summary>
    public static class EnCategories
    {
        public const string Generation = "energy.generation";
        public const string Storage = "energy.storage";
        public const string Sector = "energy.sector";
        public const string Consumers = "energy.consumers";
        public const string Grid = "energy.grid";
        public const string Lines = "energy.lines";
    }

    /// <summary>The two views every element of the module offers.</summary>
    public static class EnViews
    {
        /// <summary>Iconic front view for slides.</summary>
        public const string Pictorial = "pictorial";
        /// <summary>Single-line diagram symbol (one line stands for all conductors of a connection).</summary>
        public const string SingleLine = "single-line";

        public static readonly IReadOnlyList<string> All = new[] { Pictorial, SingleLine };

        public static readonly I18nText PictorialLabel = new("Pictorial (icon)", "Bildhaft (Piktogramm)");
        public static readonly I18nText SingleLineLabel = new("Single-line diagram", "Einlinienschaltbild");
    }

    /// <summary>Semantic primitive tags (tests, host naming).</summary>
    public static class EnTags
    {
        public const string Label = "label";
        public const string Lead = "lead";
        public const string Cable = "cable";
        public const string Pipe = "pipe";
        public const string Flow = "flow";
        public const string Junction = "junction";
        public const string Busbar = "busbar";
        public const string Tick = "tick";
    }

    /// <summary>Voltage level bands used for colours (a drawing convention of this module, not a standard).</summary>
    public enum VoltageLevel { Low, Medium, High, ExtraHigh, Dc }

    /// <summary>Voltage levels: classification of a nominal voltage and the module's colour per level.</summary>
    public static class Voltage
    {
        /// <summary>
        /// Band of a nominal voltage in kV. The limits (up to 1 kV low, up to 52 kV medium, up to 150 kV high voltage, above
        /// that extra-high voltage) follow common usage in power distribution; they are only used to pick a colour.
        /// </summary>
        public static VoltageLevel Of(double kv, bool dc = false)
        {
            if (dc) return VoltageLevel.Dc;
            if (kv <= 1.0 + 1e-9) return VoltageLevel.Low;
            if (kv <= 52 + 1e-9) return VoltageLevel.Medium;
            if (kv <= 150 + 1e-9) return VoltageLevel.High;
            return VoltageLevel.ExtraHigh;
        }

        /// <summary>Colour of a voltage level (module convention, documented in docs/en/modules/energy.md).</summary>
        public static string Color(VoltageLevel level) => level switch
        {
            VoltageLevel.Low => "#1565c0",
            VoltageLevel.Medium => "#2e7d32",
            VoltageLevel.High => "#ef6c00",
            VoltageLevel.ExtraHigh => "#c62828",
            _ => "#6a1b9a",
        };

        public static I18nText Name(VoltageLevel level) => level switch
        {
            VoltageLevel.Low => new I18nText("Low voltage", "Niederspannung"),
            VoltageLevel.Medium => new I18nText("Medium voltage", "Mittelspannung"),
            VoltageLevel.High => new I18nText("High voltage", "Hochspannung"),
            VoltageLevel.ExtraHigh => new I18nText("Extra-high voltage", "Höchstspannung"),
            _ => new I18nText("Direct current", "Gleichstrom"),
        };
    }

    /// <summary>Number and unit formatting for labels (decimal comma in German).</summary>
    public static class Fmt
    {
        /// <summary>A number with up to <paramref name="decimals"/> decimals, decimal comma for German.</summary>
        public static string Num(double v, Lang lang, int decimals = 2)
        {
            var s = Math.Round(v, decimals).ToString("0." + new string('#', Math.Max(1, decimals)), CultureInfo.InvariantCulture);
            if (s == "-0") s = "0";
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>
        /// A quantity given in its base unit (W, Wp, VA, Wh) with an SI prefix chosen so the number stays readable:
        /// 430 Wp, 2.58 kWp, 630 kVA, 40 MVA, 2 MWh.
        /// </summary>
        public static string Si(double baseValue, string unit, Lang lang)
        {
            var a = Math.Abs(baseValue);
            if (a >= 1e9 - 1e-6) return Num(baseValue / 1e9, lang) + " G" + unit;
            if (a >= 1e6 - 1e-6) return Num(baseValue / 1e6, lang) + " M" + unit;
            if (a >= 1e3 - 1e-6) return Num(baseValue / 1e3, lang) + " k" + unit;
            return Num(baseValue, lang) + " " + unit;
        }

        /// <summary>A nominal voltage given in kV: AC always in kV ("0.4 kV", "20 kV"), DC below 1 kV in volts ("800 V DC").</summary>
        public static string Kv(double kv, Lang lang, bool dc = false)
        {
            if (dc) return (kv < 1 - 1e-9 ? Num(kv * 1000, lang) + " V" : Num(kv, lang) + " kV") + " DC";
            return Num(kv, lang) + " kV";
        }

        /// <summary>Voltages of a transformer as one compact label: "20/0.4 kV", "110/20/10 kV".</summary>
        public static string Ratio(Lang lang, params double[] kv) => string.Join("/", kv.Select(v => Num(v, lang))) + " kV";

        /// <summary>
        /// Reads voltages typed as "20/0.4", "20 / 0,4 kV", "110/20/10" (kV). Throws <see cref="FormatException"/> with a
        /// bilingual message for anything else.
        /// </summary>
        public static double[] ParseRatio(string text, int count, Lang lang)
        {
            var t = (text ?? "").Trim().ToLowerInvariant().Replace("kv", "").Replace(" ", "");
            var parts = t.Split(new[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries);
            var res = new List<double>();
            foreach (var p in parts)
            {
                if (!double.TryParse(p.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0 || double.IsInfinity(v) || double.IsNaN(v))
                {
                    res.Clear();
                    break;
                }
                res.Add(v);
            }
            if (res.Count != count)
                throw new FormatException(lang == Lang.De
                    ? "Bitte " + count.ToString(CultureInfo.InvariantCulture) + " Spannungen in kV eingeben, z. B. „" + (count == 2 ? "20/0,4" : "110/20/10") + "“"
                    : "Please enter " + count.ToString(CultureInfo.InvariantCulture) + " voltages in kV, e.g. “" + (count == 2 ? "20/0.4" : "110/20/10") + "”");
            return res.ToArray();
        }
    }

    /// <summary>Colour palette of the pictorial view (muted colours that read well on slides).</summary>
    internal static class Pal
    {
        public const string Ink = "#37474f";
        public const string White = "#ffffff";
        public const string Light = "#f5f5f5";
        public const string Metal = "#eceff1";
        public const string Steel = "#b0bec5";
        public const string Dark = "#263238";
        public const string Glass = "#1d3557";
        public const string Cell = "#5b84c4";
        public const string Frame = "#8a96a3";
        public const string Copper = "#c98b4f";
        public const string Porcelain = "#8d6e63";
        public const string Green = "#43a047";
        public const string Amber = "#f9a825";
        public const string Red = "#e53935";
        public const string Sky = "#bbdefb";
        public const string Teal = "#00897b";
        public const string Heat = "#c62828";
        public const string Water = "#0288d1";
        public const string Gas = "#8d6e63";
        public const string Concrete = "#d7ccc8";
        public const string Roof = "#a1483f";
        public const string Wood = "#8d6e63";
    }

    /// <summary>Shared line styles.</summary>
    internal static class St
    {
        /// <summary>Single-line symbol strokes (one uniform weight, as usual in circuit diagrams).</summary>
        public static readonly Style Sym = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Emphasised strokes (battery minus plate, breaker squares).</summary>
        public static readonly Style Bold = Style.Visible with { StrokeWidth = 0.7 };
        /// <summary>Thin details.</summary>
        public static readonly Style Thin = Style.ThinLine with { StrokeWidth = 0.18 };
        /// <summary>White-filled symbol outline (parts lying on top of lines).</summary>
        public static readonly Style SymWhite = Sym with { Fill = Core.Fill.Solid(Pal.White) };
        /// <summary>Solid black (arrow heads, dots, filled breaker).</summary>
        public static readonly Style Black = Style.Visible with { StrokeWidth = 0.1, Fill = Core.Fill.Solid("#000000") };
        /// <summary>Communication lines (dashed thin).</summary>
        public static readonly Style Data = Style.ThinLine with { StrokeWidth = 0.25, Dash = DashStyle.Dash };

        /// <summary>Pictorial outline.</summary>
        public static readonly Style Out = Style.ThinLine with { Stroke = Pal.Ink, StrokeWidth = 0.25 };
        /// <summary>Pictorial fine detail line in a colour.</summary>
        public static Style Detail(string color, double width = 0.15) => Style.ThinLine with { Stroke = color, StrokeWidth = width };
        /// <summary>Pictorial filled shape with the ink outline.</summary>
        public static Style Fill(string color) => Out with { Fill = Core.Fill.Solid(color) };
        /// <summary>Filled shape whose outline has the fill colour (no visible edge).</summary>
        public static Style Solid(string color) => Style.ThinLine with { Stroke = color, StrokeWidth = 0.1, Fill = Core.Fill.Solid(color) };
        /// <summary>Coloured stroke (cable stubs, pipes).</summary>
        public static Style Stroke(string color, double width) => Style.Visible with { Stroke = color, StrokeWidth = width };
    }

    /// <summary>Parameter definition helpers (all parameters are optional and have defaults).</summary>
    internal static class Pm
    {
        public static ParamDef Num(string key, string en, string de, double def, double min, double max, string unit, double? step = null)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def };

        public static ParamDef Text(string key, string en, string de, string def)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        /// <summary>Designation shown as the first label line (e.g. "T1", "PV south").</summary>
        public static readonly ParamDef Name = Text("name", "Designation", "Bezeichnung", "");
        /// <summary>Shows the designation and rating label.</summary>
        public static readonly ParamDef ShowLabel = Bool("showLabel", "Show rating label", "Bemessungsdaten anzeigen", true);
        /// <summary>Colours lines and windings by their voltage level.</summary>
        public static readonly ParamDef LevelColors = Bool("levelColors", "Colour by voltage level", "Farbe nach Spannungsebene", true);

        /// <summary>Label of the option currently set for a choice parameter.</summary>
        public static I18nText OptionLabel(ParamDef p, string value)
            => p.Options?.FirstOrDefault(x => x.Value == value)?.Label ?? p.Options?.FirstOrDefault()?.Label ?? new I18nText(value);
    }

    /// <summary>Typed reads of a parameter with the definition's default as fallback.</summary>
    internal static class ParamRead
    {
        public static double N(this SketchObject o, ParamDef p) => o.Num(p.Key, p.Default?.Number ?? 0);
        public static int I(this SketchObject o, ParamDef p)
        {
            var v = o.Int(p.Key, (int)Math.Round(p.Default?.Number ?? 0));
            if (p.Min is double min) v = Math.Max(v, (int)Math.Ceiling(min));
            if (p.Max is double max) v = Math.Min(v, (int)Math.Floor(max));
            return v;
        }
        public static double Clamped(this SketchObject o, ParamDef p)
        {
            var v = o.N(p);
            if (double.IsNaN(v) || double.IsInfinity(v)) v = p.Default?.Number ?? 0;
            if (p.Min is double min) v = Math.Max(v, min);
            if (p.Max is double max) v = Math.Min(v, max);
            return v;
        }
        public static string S(this SketchObject o, ParamDef p) => o.Str(p.Key, p.Default?.Text ?? "");
        public static bool B(this SketchObject o, ParamDef p) => o.Flag(p.Key, p.Default?.Bool ?? false);
    }

    /// <summary>Context action helpers.</summary>
    internal static class Act
    {
        public static ContextAction Toggle(SketchObject o, string id, I18nText label, ParamDef p, string icon = IconIds.Toggle, string group = "param")
            => new(id, label, icon, x => x.WithParam(p.Key, !x.B(p))) { IsActive = o.B(p), Group = group, ParamKeys = new[] { p.Key } };

        /// <summary>Cycles a choice parameter; the label names the next value.</summary>
        public static ContextAction Cycle(SketchObject o, string id, ParamDef choice, string group = "param")
        {
            var values = choice.Options!;
            var cur = o.S(choice);
            var idx = Math.Max(0, values.ToList().FindIndex(v => v.Value == cur));
            var next = values[(idx + 1) % values.Count];
            return new ContextAction(id, next.Label, IconIds.Cycle, x => x.WithParam(choice.Key, next.Value)) { Group = group, ParamKeys = new[] { choice.Key } };
        }

        /// <summary>Steps an integer parameter by <paramref name="delta"/> within its limits.</summary>
        public static ContextAction Step(SketchObject o, string id, I18nText label, ParamDef p, int delta)
        {
            var cur = o.I(p);
            var next = cur + delta;
            var ok = (p.Min is not double min || next >= min) && (p.Max is not double max || next <= max);
            return new ContextAction(id, label, delta > 0 ? IconIds.Increase : IconIds.Decrease, x => x.WithParam(p.Key, Math.Max(p.Min ?? next, Math.Min(p.Max ?? next, x.I(p) + delta))))
            { Group = "count", IsEnabled = ok, ParamKeys = new[] { p.Key } };
        }

        /// <summary>Typed number with an optional unit suffix ("10", "10 kW", "10,5").</summary>
        public static ContextAction Number(SketchObject o, string id, I18nText label, ParamDef p, Lang lang)
            => ContextAction.WithInput(id, label, IconIds.Text, p.Label, Fmt.Num(o.N(p), lang, 3), (x, text) =>
            {
                var t = (text ?? "").Trim();
                if (p.Unit is string u && t.EndsWith(u, StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - u.Length);
                t = t.Trim().Replace(',', '.');
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
                    throw new FormatException(lang == Lang.De ? "Keine gültige Zahl: „" + text + "“" : "Not a valid number: “" + text + "”");
                if (p.Min is double min) v = Math.Max(min, v);
                if (p.Max is double max) v = Math.Min(max, v);
                return x.WithParam(p.Key, v);
            }, p.Unit) with { Group = "rating", ParamKeys = new[] { p.Key } };
    }

    /// <summary>Shared rendering helpers.</summary>
    internal static class En
    {
        /// <summary>Approximate advance width per character relative to the em size (as Core's text bounds).</summary>
        public const double CharWidth = 0.55;

        /// <summary>Style override: text gets the stroke colour, white fills keep their look.</summary>
        public static Primitive Override(Primitive p, Style over)
        {
            switch (p)
            {
                case GroupPrim g:
                    return g with { Children = g.Children.Select(c => Override(c, over)).ToList() };
                case TextPrim t:
                    return over.Stroke is string col && !string.Equals(col, "none", StringComparison.OrdinalIgnoreCase)
                        ? t with { TextStyle = t.TextStyle with { Color = col } }
                        : t;
            }
            return Prim.Restyle(p, s => s with
            {
                Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                Opacity = over.Opacity ?? s.Opacity,
                StrokeWidth = over.StrokeWidth is double w && s.HasStroke && (s.Role ?? LineRole.Visible) is LineRole.Visible ? w : s.StrokeWidth,
                Fill = over.Fill is not null && s.Fill is not null && s.Fill.Kind != FillKind.None && s.Fill.Color != Pal.White ? over.Fill : s.Fill,
                Dash = over.Dash ?? s.Dash,
            });
        }

        /// <summary>Rotation that keeps text upright on the page after the object's transform.</summary>
        public static double UprightRotation(SketchObject o)
        {
            var t = o.Transform;
            return t.FlipX ^ t.FlipY ? 0 : -t.Rotation;
        }

        /// <summary>Polyline offset by <paramref name="d"/> to the left of the running direction, mitred at corners.</summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> p, double d)
        {
            var res = new List<Vec2>(p.Count);
            for (var i = 0; i < p.Count; i++)
            {
                var din = i > 0 ? (p[i] - p[i - 1]).Normalized() : (p[1] - p[0]).Normalized();
                var dout = i + 1 < p.Count ? (p[i + 1] - p[i]).Normalized() : din;
                var nIn = new Vec2(din.Y, -din.X);
                var nOut = new Vec2(dout.Y, -dout.X);
                var n = (nIn + nOut).Normalized();
                var cos = n.Dot(nOut);
                if (n.Length < Geo.Eps || Math.Abs(cos) < 0.2) { res.Add(p[i] + nOut * d); continue; }
                res.Add(p[i] + n * (d / cos));
            }
            return res;
        }

        /// <summary>Points without consecutive duplicates.</summary>
        public static IReadOnlyList<Vec2> Clean(IReadOnlyList<Vec2> p)
        {
            var list = new List<Vec2>(p.Count);
            foreach (var q in p) if (list.Count == 0 || list[list.Count - 1].DistanceTo(q) > 1e-6) list.Add(q);
            return list;
        }

        /// <summary>Middle and unit direction of the longest segment (label position).</summary>
        public static (Vec2 Mid, Vec2 Dir) Longest(IReadOnlyList<Vec2> p)
        {
            var best = 0; var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var d = p[best + 1] - p[best];
            return (Vec2.Lerp(p[best], p[best + 1], 0.5), len > Geo.Eps ? d / len : new Vec2(1, 0));
        }

        /// <summary>Label beside the longest segment, upright (reads left to right or bottom to top).</summary>
        public static TextPrim LineLabel(IReadOnlyList<Vec2> p, string text, double size, string? color, double offset)
        {
            var (mid, dir) = Longest(p);
            if (dir.X < -1e-9 || (Math.Abs(dir.X) < 1e-9 && dir.Y > 0)) dir = -dir;
            var up = new Vec2(dir.Y, -dir.X);
            var at = mid + up * (offset + size * 0.7);
            var rot = Geo.RadToDeg(Math.Atan2(dir.Y, dir.X));
            return new TextPrim(at, text, new TextStyle { Size = size, Color = color }, Math.Abs(rot) < 1e-9 ? 0 : rot) { Tag = EnTags.Label };
        }

        /// <summary>
        /// Exit direction (on the page) of an attached end: perpendicular to the box edge of the target the port sits on,
        /// honouring the target's rotation. Null for free ends and anchors inside the box.
        /// </summary>
        public static bool? ExitHorizontal(SketchDocument doc, SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a is null) return null;
            Vec2 local;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6) local = new Vec2(Math.Sign(a.U), 0);
            else if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6) local = new Vec2(0, Math.Sign(a.V));
            else return null;
            var target = doc.Get(a.ObjectId);
            if (target is null || target.IsPointGeometry) return null;
            var dir = target.Matrix().ApplyLinear(local);
            return Math.Abs(dir.X) >= Math.Abs(dir.Y);
        }

        /// <summary>Orthogonal route through all points (an L or Z between consecutive points).</summary>
        public static List<Vec2> Orthogonal(IReadOnlyList<Vec2> pts, bool? startHorizontal, bool? endHorizontal)
        {
            var result = new List<Vec2> { pts[0] };
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var d = b - a;
                if (Math.Abs(d.X) > 1e-9 && Math.Abs(d.Y) > 1e-9)
                {
                    var sh = i == 0 ? startHorizontal : null;
                    var eh = i + 2 == pts.Count ? endHorizontal : null;
                    var s = sh ?? (eh ?? Math.Abs(d.X) >= Math.Abs(d.Y));
                    var e = eh ?? s;
                    if (s && e) { var mx = (a.X + b.X) / 2; result.Add(new Vec2(mx, a.Y)); result.Add(new Vec2(mx, b.Y)); }
                    else if (s) result.Add(new Vec2(b.X, a.Y));
                    else if (e) result.Add(new Vec2(a.X, b.Y));
                    else { var my = (a.Y + b.Y) / 2; result.Add(new Vec2(a.X, my)); result.Add(new Vec2(b.X, my)); }
                }
                result.Add(b);
            }
            return result;
        }

        /// <summary>Junction dots where an end of the line is docked onto another line or a busbar rail (a tee).</summary>
        public static IEnumerable<Primitive> Junctions(SketchDocument doc, SketchObject o, IReadOnlyList<Vec2> raw, double r, string color)
        {
            if (o.Attach is null) yield break;
            foreach (var i in new[] { 0, raw.Count - 1 })
            {
                if (i < 0 || i >= o.Attach.Count || o.Attach[i] is not { } a) continue;
                var onRail = a.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal);
                var onBar = !onRail && a.AnchorId is null && doc.Get(a.ObjectId) is { Type: BusbarTypeId };
                if (onRail || onBar)
                    yield return Prim.Circle(raw[i].X, raw[i].Y, r, new Style { Stroke = color, StrokeWidth = 0.1, Fill = Core.Fill.Solid(color) }) with { Tag = EnTags.Junction };
            }
        }

        public const string BusbarTypeId = "energy.grid.busbar";
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for the view's design box (centred at 0,0) and
    /// stretched to the actual object size, so free (also non-uniform) scaling stretches the drawing and every drawing
    /// stays inside its box. Round details and text use the uniform detail scale <see cref="K"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items;
        private readonly Vec2 _origin;

        public Pen(Size2 actual, Size2 design, double textRotation = 0, Lang lang = Lang.En)
        {
            _items = new List<Primitive>();
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = design.W;
            DH = design.H;
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
            TextRotation = textRotation;
            Lang = lang;
        }

        public double W { get; }
        public double H { get; }
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }
        public double TextRotation { get; }
        public Lang Lang { get; }
        public IReadOnlyList<Primitive> Items => _items;

        private Pen(Pen parent, Vec2 origin)
        {
            _items = parent._items;
            _origin = origin;
            W = parent.W; H = parent.H; DW = parent.DW; DH = parent.DH;
            Sx = Sy = K = parent.K;
            TextRotation = parent.TextRotation;
            Lang = parent.Lang;
        }

        /// <summary>
        /// A pen centred at design point (cx, cy) that draws with the uniform detail scale in both directions: symbol
        /// cores (circles of generators and transformer windings) keep their shape under non-uniform scaling while the
        /// leads drawn with this pen stretch. Everything stays inside the box because the detail scale is the smaller one.
        /// </summary>
        public Pen Uniform(double cx, double cy) => new(this, V(cx, cy));

        /// <summary>Design point to local (actual) point.</summary>
        public Vec2 V(double x, double y) => new(_origin.X + x * Sx, _origin.Y + y * Sy);
        public Vec2 V(Vec2 p) => V(p.X, p.Y);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? St.Sym), tag);

        public void Poly(Style s, bool closed, string? tag, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed), tag);
        }

        public void PolyD(IEnumerable<Vec2> design, Style s, bool closed = false, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);

        /// <summary>Path from design segments.</summary>
        public void Path(IEnumerable<PathSeg> design, Style s, string? tag = null)
            => Add(Prim.Path(design.Select(seg => seg.Op switch
            {
                SegOp.Z => seg,
                SegOp.C => new PathSeg(SegOp.C, V(seg.P), V(seg.C1), V(seg.C2)),
                SegOp.Q => new PathSeg(SegOp.Q, V(seg.P), V(seg.C1)),
                _ => new PathSeg(seg.Op, V(seg.P)),
            }), s), tag);

        /// <summary>Axis-aligned rectangle given by two design corners (optional corner radius in design mm).</summary>
        public void Rect(double x0, double y0, double x1, double y1, Style s, string? tag = null, double radius = 0)
        {
            var a = V(x0, y0);
            var b = V(x1, y1);
            double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
            Add(Prim.Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), w, h, s, Math.Min(radius * K, Math.Min(w, h) / 2)), tag);
        }

        /// <summary>Stretched ellipse in design coordinates.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style s, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Ellipse(c.X, c.Y, Math.Max(0.01, rx * Sx), Math.Max(0.01, ry * Sy), s), tag);
        }

        /// <summary>Circle that follows the stretch (an ellipse when the box is scaled non-uniformly).</summary>
        public void Circle(double cx, double cy, double r, Style s, string? tag = null) => Ellipse(cx, cy, r, r, s, tag);

        /// <summary>True circle at a design centre with a radius in design mm scaled by the detail scale.</summary>
        public void Round(double cx, double cy, double r, Style s, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, Math.Max(0.01, r * K), s), tag);
        }

        /// <summary>Filled dot.</summary>
        public void Dot(double cx, double cy, double r = 0.5, string? color = null, string? tag = "dot")
            => Round(cx, cy, r, color is null ? St.Black : St.Solid(color), tag);

        /// <summary>Elliptical arc in design coordinates (degrees, positive sweep = clockwise on screen).</summary>
        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style s, string? tag = null)
            => PolyD(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg)), s, false, tag);

        /// <summary>
        /// Text at a design point, fitted into a width/height budget (design mm): the size shrinks so the text's bounds stay
        /// inside the budget, then scales with the detail scale.
        /// </summary>
        public void Text(double x, double y, string text, double size, double maxW, double maxH, TextAnchor anchor = TextAnchor.Middle,
            bool bold = false, string? color = null, string? tag = EnTags.Label)
        {
            if (string.IsNullOrEmpty(text)) return;
            var len = Math.Max(1, text.Length);
            var s = Math.Max(0.05, Math.Min(size, Math.Min(maxW * (bold ? 0.85 : 1) / (len * En.CharWidth), maxH)));
            var at = V(x, y);
            var ts = new TextStyle { Size = s * K, Anchor = anchor, Baseline = TextBaseline.Middle, Bold = bold, Color = color };
            Add(Prim.Text(at.X, at.Y, text, ts, TextRotation), tag);
        }

        /// <summary>
        /// Stacked text lines in a design box, one common size so every line fits; left aligned (Start) or centred.
        /// </summary>
        public void Lines(double x0, double y0, double x1, double y1, IReadOnlyList<string> lines, double size, TextAnchor anchor, string? tag = EnTags.Label)
        {
            if (lines.Count == 0) return;
            double w = x1 - x0, h = y1 - y0;
            var longest = Math.Max(1, lines.Max(l => l.Length));
            var s = Math.Max(0.05, Math.Min(size, Math.Min(w / (longest * En.CharWidth), h / (lines.Count * 1.2))));
            var total = s * 1.2 * lines.Count;
            var top = (y0 + y1) / 2 - total / 2 + s * 0.6;
            var x = anchor == TextAnchor.Start ? x0 : anchor == TextAnchor.End ? x1 : (x0 + x1) / 2;
            for (var i = 0; i < lines.Count; i++)
            {
                var at = V(x, top + i * s * 1.2);
                var ts = new TextStyle { Size = s * K, Anchor = anchor, Baseline = TextBaseline.Middle };
                Add(Prim.Text(at.X, at.Y, lines[i], ts, TextRotation), tag);
            }
        }

        /// <summary>Filled arrow head with its tip at design (tx, ty), pointing away from design (fx, fy).</summary>
        public void Head(double tx, double ty, double fx, double fy, double len = 1.6, double half = 0.65, string? color = null, string? tag = "head")
        {
            var tip = V(tx, ty);
            var dir = (tip - V(fx, fy)).Normalized();
            if (dir.Length < Geo.Eps) return;
            var n = dir.Perp;
            var b = tip - dir * (len * K);
            Add(Prim.Polygon(new[] { tip, b + n * (half * K), b - n * (half * K) }, color is null ? St.Black : St.Solid(color)), tag);
        }

        /// <summary>Line from (fx, fy) to (tx, ty) with a filled arrow head at the end.</summary>
        public void Arrow(double fx, double fy, double tx, double ty, double len = 1.4, double half = 0.55, Style? s = null, string? tag = "arrow")
        {
            var a = V(fx, fy);
            var tip = V(tx, ty);
            var dir = (tip - a).Normalized();
            Add(Prim.Line(a, tip - dir * (len * K * 0.8), s ?? St.Sym), tag);
            Head(tx, ty, fx, fy, len, half, s?.Stroke, tag);
        }

        /// <summary>Small cross (x) centred at a design point, half size in design mm (detail scale).</summary>
        public void Cross(double cx, double cy, double a, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            var d = a * K;
            Add(Prim.Line(c + new Vec2(-d, -d), c + new Vec2(d, d), s ?? St.Sym), tag);
            Add(Prim.Line(c + new Vec2(-d, d), c + new Vec2(d, -d), s ?? St.Sym), tag);
        }

        /// <summary>One period of a sine ("~") centred at (cx, cy) with half width w and amplitude a (design mm).</summary>
        public void Sine(double cx, double cy, double w, double a, Style s, string? tag = "ac")
        {
            var pts = new List<Vec2>();
            for (var i = 0; i <= 16; i++)
            {
                var t = i / 16.0;
                pts.Add(new Vec2(cx - w + 2 * w * t, cy - a * Math.Sin(2 * Math.PI * t)));
            }
            PolyD(pts, s, false, tag);
        }

        /// <summary>Direct current sign: a solid line over a dashed line, centred at (cx, cy), half width w.</summary>
        public void DcSign(double cx, double cy, double w, Style s, string? tag = "dc")
        {
            Line(cx - w, cy - 0.5, cx + w, cy - 0.5, s, tag);
            var seg = 2 * w / 5;
            for (var i = 0; i < 3; i++)
            {
                var x = cx - w + i * 2 * seg;
                Line(x, cy + 0.5, x + seg, cy + 0.5, s, tag);
            }
        }

        /// <summary>Earth symbol: three shrinking bars below (cx, cy).</summary>
        public void Earth(double cx, double cy, double w, Style s, string? tag = "earth")
        {
            Line(cx - w, cy, cx + w, cy, s, tag);
            Line(cx - w * 0.62, cy + 1.2, cx + w * 0.62, cy + 1.2, s, tag);
            Line(cx - w * 0.25, cy + 2.4, cx + w * 0.25, cy + 2.4, s, tag);
        }

        /// <summary>Blade of a switch as a polygon of width <paramref name="width"/> between two design points.</summary>
        public void Bar(Vec2 a, Vec2 b, double width, Style s, string? tag = null)
        {
            var d = (b - a).Normalized();
            if (d.Length < Geo.Eps) return;
            var n = d.Perp * (width / 2);
            PolyD(new[] { a + n, b + n, b - n, a - n }, s, true, tag);
        }
    }
}
