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

namespace InventaSketch.Modules.Geometry
{
    /// <summary>Library category ids of the math &amp; geometry module.</summary>
    public static class MathCategories
    {
        public const string Axes = "math.axes";
        public const string Graphs = "math.graphs";
        public const string Vectors = "math.vectors";
        public const string Angles = "math.angles";
        public const string Figures = "math.figures";
        public const string Constructions = "math.constructions";
    }

    /// <summary>Semantic primitive tags of the module (tests, host naming, style overrides).</summary>
    public static class MathTags
    {
        /// <summary>The main stroke of a point object (vector shaft, angle rays): arrow, dash and width overrides apply.</summary>
        public const string Main = "main";
        /// <summary>Closed outline of a figure (triangle, polygon, circle): a fill override applies to it.</summary>
        public const string Outline = "outline";
        public const string Axis = "axis";
        public const string Grid = "grid";
        public const string Tick = "tick";
        public const string Number = "number";
        public const string Curve = "curve";
        public const string Formula = "formula";
        public const string Label = "label";
        /// <summary>Construction lines (bisectors, medians, altitudes, compass marks).</summary>
        public const string Construction = "construction";
        /// <summary>Angle arcs and right-angle squares.</summary>
        public const string Mark = "mark";
        /// <summary>Dashed helper lines (vector components, projections, parallelogram copies).</summary>
        public const string Helper = "helper";
        public const string Dot = "dot";
        public const string Incircle = "incircle";
        public const string Circumcircle = "circumcircle";
        public const string Resultant = "resultant";
        public const string Sine = "sin";
        public const string Cosine = "cos";
        public const string Tangent = "tan";
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class Pm
    {
        public static ParamDef Num(string key, string en, string de, double def, double? min = null, double? max = null, string? unit = null, double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def, Advanced = advanced };

        public static ParamDef Text(string key, string en, string de, string def)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        /// <summary>Notation of vector labels.</summary>
        public static ParamDef Notation() => Choice("notation", "Vector notation", "Vektorschreibweise", "arrow",
            ("arrow", "Arrow above", "Pfeil darüber"), ("bold", "Bold", "Fett"), ("plain", "Plain", "Ohne"));
    }

    /// <summary>A figure under construction: primitives and anchors in local (box) or page (points) coordinates.</summary>
    internal sealed class Fig
    {
        public List<Primitive> Items { get; private set; } = new();
        public List<Anchor> Anchors { get; private set; } = new();

        public T Add<T>(T p) where T : Primitive { Items.Add(p); return p; }

        public LinePrim Line(Vec2 a, Vec2 b, Style s, string? tag = null) => Add(new LinePrim(a, b, s) { Tag = tag });

        public PathPrim Poly(IEnumerable<Vec2> pts, Style s, bool closed = false, string? tag = null) => Add(Prim.Polyline(pts, s, closed) with { Tag = tag });

        public EllipsePrim Circle(Vec2 c, double r, Style s, string? tag = null) => Add(new EllipsePrim(c, r, r, s) { Tag = tag });

        /// <summary>Circular arc (screen angles in radians, positive sweep = clockwise on screen) as a polyline.</summary>
        public PathPrim Arc(Vec2 c, double r, double start, double sweep, Style s, string? tag = null)
            => Add(Prim.Polyline(Geo.ArcPoints(c, r, r, start, sweep), s) with { Tag = tag });

        /// <summary>Filled point marker.</summary>
        public EllipsePrim Dot(Vec2 p, double r = 0.6, string color = Mx.Black)
            => Add(new EllipsePrim(p, r, r, new Style { Role = LineRole.Thin, StrokeWidth = 0.1, Stroke = color, Fill = Fill.Solid(color) }) { Tag = MathTags.Dot });

        public TextPrim Text(Vec2 at, string text, double size, bool italic = false, TextAnchor anchor = TextAnchor.Middle, string? tag = MathTags.Label, string? color = null, bool bold = false)
            => Add(new TextPrim(at, text, new TextStyle { Size = size, Italic = italic, Bold = bold, Anchor = anchor, Color = color }) { Tag = tag });

        public void Port(string id, Vec2 p) => Anchors.Add(Anchor.Point(AnchorRole.Port, p, id));
        public void Pt(AnchorRole role, Vec2 p, string? id = null) => Anchors.Add(Anchor.Point(role, p, id));
        public void Rail(Vec2 a, Vec2 b, string id) => Anchors.Add(Anchor.Rail(a, b, id));

        /// <summary>
        /// Safety net for extreme box sizes: when the drawing (text estimate included) exceeds the box, the whole figure –
        /// primitives, text sizes and anchors alike – is shrunk uniformly about the centre of its bounds and centred in the box.
        /// Regular layouts fit by construction and are left untouched.
        /// </summary>
        public Fig Fit(Size2 size)
        {
            var bounds = Prim.Bounds(Items);
            if (bounds is not Rect b) return this;
            double hw = size.W / 2, hh = size.H / 2;
            const double tol = 1e-6;
            if (b.X >= -hw - tol && b.Right <= hw + tol && b.Y >= -hh - tol && b.Bottom <= hh + tol) return this;
            var s = Math.Min(1, Math.Min(size.W / Math.Max(b.W, 1e-9), size.H / Math.Max(b.H, 1e-9))) * 0.995;
            var m = Matrix2D.Scaling(s, s) * Matrix2D.Translation(-b.Center.X, -b.Center.Y);
            Items = Items.Select(p => Mx.ScaleText(Prim.Transform(p, m), s)).ToList();
            Anchors = Anchors.Select(a => a.Transform(m)).ToList();
            return this;
        }
    }

    /// <summary>Shared drawing helpers, styles, number formatting and parsing.</summary>
    internal static class Mx
    {
        public const string Black = "#000000";
        public const string Blue = "#1565c0";
        public const string Red = "#c62828";
        public const string Green = "#2e7d32";
        public const string Grey = "#9e9e9e";

        /// <summary>Text height (em, mm) of labels (points, sides, vectors) at the design size: ≈ 11 pt.</summary>
        public const double LabelSize = 4.0;
        /// <summary>Text height of tick numbers at the design size: ≈ 8.5 pt.</summary>
        public const double NumberSize = 3.0;
        /// <summary>Advance width per character relative to the em size used for layout (a little wider than the bounds estimate).</summary>
        public const double CharW = 0.6;

        /// <summary>Figure outlines (ISO 128 visible line).</summary>
        public static readonly Style Outline = Style.Visible;
        /// <summary>Axes (thin, 0.35 mm, arrowhead at the end).</summary>
        public static readonly Style Axis = Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
        public static readonly Style AxisPlain = Style.ThinLine with { StrokeWidth = 0.35 };
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style GridLine = new() { Role = LineRole.Thin, StrokeWidth = 0.15, Stroke = "#bdbdbd" };
        public static readonly Style Construction = new() { Role = LineRole.Thin, StrokeWidth = 0.25, Stroke = Blue };
        public static readonly Style Mark = Style.ThinLine with { StrokeWidth = 0.35 };
        public static readonly Style Helper = Style.Hidden;

        public static Style Colored(Style s, string color) => s with { Stroke = color };

        // ------------------------------------------------------------------ numbers

        /// <summary>Number for display: up to <paramref name="decimals"/> decimals, decimal comma in German, minus sign "−".</summary>
        public static string Num(double v, Lang lang, int decimals = 3)
        {
            var r = Math.Round(v, decimals);
            if (Math.Abs(r) < Math.Pow(10, -decimals) / 2) r = 0;
            var s = Math.Abs(r).ToString(decimals <= 0 ? "0" : "0." + new string('#', decimals), CultureInfo.InvariantCulture);
            if (lang == Lang.De) s = s.Replace('.', ',');
            return r < 0 ? "−" + s : s;
        }

        /// <summary>Parses a typed number: "45", "45°", "−2,5", "0.5 mm". Throws <see cref="FormatException"/>.</summary>
        public static double ParseNumber(string? text)
        {
            var s = (text ?? "").Trim().Replace('−', '-').Replace("°", "").Replace("mm", "").Trim().Replace(',', '.');
            if (s.Length == 0 || !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
                throw new FormatException("Not a number: " + text);
            return v;
        }

        /// <summary>
        /// Parses a range "min;max", "min..max", "min … max", "min to max", "min bis max" or "min max" (a comma is a decimal
        /// comma). Returns (min, max) with min &lt; max. Throws <see cref="FormatException"/>.
        /// </summary>
        public static (double Min, double Max) ParseRange(string? text)
        {
            var s = (text ?? "").Trim().Replace('−', '-');
            string[] parts;
            if (s.IndexOf(';') >= 0) parts = s.Split(';');
            else if (s.IndexOf("..", StringComparison.Ordinal) >= 0) parts = s.Split(new[] { ".." }, StringSplitOptions.None);
            else if (s.IndexOf('…') >= 0) parts = s.Split('…');
            else parts = s.Split(new[] { " to ", " bis ", " " }, StringSplitOptions.RemoveEmptyEntries);
            parts = parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (parts.Length != 2) throw new FormatException("Expected two numbers: " + text);
            double a = ParseNumber(parts[0]), b = ParseNumber(parts[1]);
            if (Math.Abs(a - b) < 1e-12) throw new FormatException("Empty range: " + text);
            return a < b ? (a, b) : (b, a);
        }

        public static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        // ------------------------------------------------------------------ geometry

        /// <summary>Screen unit vector of a mathematical angle (degrees, counter-clockwise from +x, y up on the page).</summary>
        public static Vec2 Dir(double mathDeg) => new(Math.Cos(Geo.DegToRad(mathDeg)), -Math.Sin(Geo.DegToRad(mathDeg)));

        /// <summary>Text scale of a box object relative to its design size (smaller factor, clamped to 0.5 … 2.5).</summary>
        public static double Scale(Size2 size, Size2 design)
            => Geo.Clamp(Math.Min(size.W / design.W, size.H / design.H), 0.5, 2.5);

        public static double TextW(string text, double size) => Math.Max(1, (text ?? "").Length) * size * CharW;

        /// <summary>Half extent of a text box along a direction (support function), with an optional extra height on top.</summary>
        public static double HalfExtentAlong(string text, double size, Vec2 dir, double extraHeight = 0)
        {
            var d = dir.Normalized();
            return Math.Abs(d.X) * TextW(text, size) / 2 + Math.Abs(d.Y) * (size + extraHeight) / 2;
        }

        /// <summary>Unit normal of a direction that points to the upper side (or to the left for vertical directions).</summary>
        public static Vec2 UpperNormal(Vec2 d)
        {
            var n = d.Normalized().Perp;
            if (n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0)) n = -n;
            return n;
        }

        /// <summary>Start angle and signed sweep (screen radians) of the smaller angle from direction u1 to u2.</summary>
        public static (double Start, double Sweep) SmallSweep(Vec2 u1, Vec2 u2)
        {
            var a1 = Math.Atan2(u1.Y, u1.X);
            var a2 = Math.Atan2(u2.Y, u2.X);
            var s = a2 - a1;
            while (s > Math.PI) s -= 2 * Math.PI;
            while (s <= -Math.PI) s += 2 * Math.PI;
            return (a1, s);
        }

        /// <summary>Angle arc for the smaller angle between unit directions u1 and u2 at v.</summary>
        public static void AngleArc(Fig f, Vec2 v, Vec2 u1, Vec2 u2, double r, Style? s = null, string? tag = MathTags.Mark)
        {
            var (start, sweep) = SmallSweep(u1, u2);
            if (Math.Abs(sweep) < 1e-6) return;
            f.Arc(v, r, start, sweep, s ?? Mark, tag);
        }

        /// <summary>Right-angle mark: the square spanned by the unit directions u1, u2 at v, with a dot in its centre.</summary>
        public static void RightAngle(Fig f, Vec2 v, Vec2 u1, Vec2 u2, double q, bool dot = true, Style? s = null)
        {
            f.Poly(new[] { v + u1 * q, v + (u1 + u2) * q, v + u2 * q }, s ?? Mark, tag: MathTags.Mark);
            if (dot) f.Dot(v + (u1 + u2) * (q / 2), Math.Max(0.25, q * 0.09));
        }

        /// <summary>Intersection of the infinite lines a1–a2 and b1–b2, or null when parallel.</summary>
        public static Vec2? LineIntersection(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2)
        {
            var r = a2 - a1; var s = b2 - b1;
            var den = r.Cross(s);
            if (Math.Abs(den) < 1e-12) return null;
            return a1 + r * ((b1 - a1).Cross(s) / den);
        }

        /// <summary>
        /// Uniform fit: maps <paramref name="pts"/> (design coordinates) into <paramref name="area"/> keeping their shape
        /// (largest uniform scale, centred). Returns the mapping and the scale factor.
        /// </summary>
        public static (Func<Vec2, Vec2> Map, double Scale) FitUniform(IEnumerable<Vec2> pts, Rect area)
        {
            var b = Rect.FromPoints(pts);
            var s = Math.Min(area.W / Math.Max(b.W, 1e-9), area.H / Math.Max(b.H, 1e-9));
            if (!Finite(s) || s <= 0) s = 1;
            var bc = b.Center; var ac = area.Center;
            return (p => ac + (p - bc) * s, s);
        }

        /// <summary>Points of a circle's bounding box (for fitting).</summary>
        public static IEnumerable<Vec2> CircleBox(Vec2 c, double r)
        {
            yield return new Vec2(c.X - r, c.Y - r);
            yield return new Vec2(c.X + r, c.Y + r);
        }

        /// <summary>Ramer–Douglas–Peucker simplification of a polyline (keeps the end points).</summary>
        public static List<Vec2> Simplify(IReadOnlyList<Vec2> pts, double tol)
        {
            if (pts.Count < 3) return pts.ToList();
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                var (i0, i1) = stack.Pop();
                double best = -1; var bi = -1;
                for (var i = i0 + 1; i < i1; i++)
                {
                    var d = Geo.ClosestOnSegment(pts[i], pts[i0], pts[i1]).Point.DistanceTo(pts[i]);
                    if (d > best) { best = d; bi = i; }
                }
                if (bi >= 0 && best > tol)
                {
                    keep[bi] = true;
                    stack.Push((i0, bi));
                    stack.Push((bi, i1));
                }
            }
            var result = new List<Vec2>();
            for (var i = 0; i < pts.Count; i++) if (keep[i]) result.Add(pts[i]);
            return result;
        }

        // ------------------------------------------------------------------ labels

        /// <summary>
        /// A vector label: the text, with a small arrow above it (<c>arrow</c> notation) or in bold (<c>bold</c>).
        /// <paramref name="at"/> is the centre of the letters.
        /// </summary>
        public static void VectorLabel(Fig f, Vec2 at, string text, double size, string notation, string? color = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            f.Text(at, text, size, italic: true, color: color, bold: notation == "bold");
            if (notation != "arrow") return;
            var w = Math.Max(TextW(text, size) * 0.8, size * 0.55);
            var y = at.Y - size * 0.72;
            var s = new Style { Role = LineRole.Thin, StrokeWidth = Math.Max(0.15, size * 0.05), Stroke = color, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            f.Line(new Vec2(at.X - w / 2, y), new Vec2(at.X + w / 2, y), s, MathTags.Label);
        }

        /// <summary>Extra height a vector label needs above the letters for the notation arrow.</summary>
        public static double NotationHeight(string notation, double size) => notation == "arrow" ? size * 0.5 : 0;

        /// <summary>
        /// A label with a subscript (e.g. v with index x): main letters centred at <paramref name="at"/>, the index smaller
        /// and lowered to the right of it. Returns the total width.
        /// </summary>
        public static void IndexedLabel(Fig f, Vec2 at, string main, string index, double size, string notation, string? color = null)
        {
            var mw = TextW(main, size);
            var iw = TextW(index, size * 0.7);
            var mainAt = new Vec2(at.X - iw / 2, at.Y);
            VectorLabel(f, mainAt, main, size, notation, color);
            f.Text(new Vec2(mainAt.X + mw / 2 + 0.1, at.Y + size * 0.3), index, size * 0.7, italic: true, anchor: TextAnchor.Start, color: color);
        }

        /// <summary>Width of an indexed label (for layout).</summary>
        public static double IndexedWidth(string main, string index, double size) => TextW(main, size) + TextW(index, size * 0.7) + 0.1;

        /// <summary>
        /// A row of tokens centred at <paramref name="at"/> (e.g. "a", "+", "b"), each token centred in its own slot so that
        /// notation arrows sit exactly over their letters whatever the host font. Vector tokens get the notation.
        /// </summary>
        public static void TokenRow(Fig f, Vec2 at, IReadOnlyList<(string Text, bool Vector)> tokens, double size, string notation, string? color = null)
        {
            var widths = tokens.Select(t => TextW(t.Text, size)).ToList();
            const double gap = 0.6;
            var total = widths.Sum() + gap * (tokens.Count - 1);
            var x = at.X - total / 2;
            for (var i = 0; i < tokens.Count; i++)
            {
                var c = new Vec2(x + widths[i] / 2, at.Y);
                if (tokens[i].Vector) VectorLabel(f, c, tokens[i].Text, size, notation, color);
                else f.Text(c, tokens[i].Text, size, color: color);
                x += widths[i] + gap;
            }
        }

        public static double TokenRowWidth(IReadOnlyList<(string Text, bool Vector)> tokens, double size)
            => tokens.Sum(t => TextW(t.Text, size)) + 0.6 * (tokens.Count - 1);

        /// <summary>Places a label of the given extent outside a point along <paramref name="dir"/> with a gap.</summary>
        public static Vec2 Beside(Vec2 p, Vec2 dir, string text, double size, double gap, double extraHeight = 0)
        {
            var d = dir.Normalized();
            if (d == Vec2.Zero) d = new Vec2(0, -1);
            // The label box (text plus the notation arrow above it) sits outside; the text centre lies extra/2 below the box centre.
            return p + d * (gap + HalfExtentAlong(text, size, d, extraHeight)) + new Vec2(0, extraHeight / 2);
        }

        // ------------------------------------------------------------------ overrides

        /// <summary>The primitive with every text size multiplied by <paramref name="s"/>.</summary>
        public static Primitive ScaleText(Primitive p, double s) => p switch
        {
            TextPrim t => t with { TextStyle = t.TextStyle with { Size = t.TextStyle.Size * s } },
            GroupPrim g => g with { Children = g.Children.Select(c => ScaleText(c, s)).ToList() },
            _ => p,
        };

        /// <summary>
        /// Style override: colour on every stroke, text and point marker; width on the main stroke, outlines and visible
        /// lines; dash and arrowheads on the main stroke only; fill on closed outlines.
        /// </summary>
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
            var main = p.Tag == MathTags.Main;
            var outline = p.Tag == MathTags.Outline;
            var dot = p.Tag == MathTags.Dot;
            return Prim.Restyle(p, s => s with
            {
                Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                Opacity = over.Opacity ?? s.Opacity,
                StrokeWidth = over.StrokeWidth is double w && (main || outline || (s.Role ?? LineRole.Visible) == LineRole.Visible) ? w : s.StrokeWidth,
                Fill = dot && over.Stroke is string c && !string.Equals(c, "none", StringComparison.OrdinalIgnoreCase) ? Fill.Solid(c)
                    : outline && over.Fill is not null ? over.Fill : s.Fill,
                ArrowStart = main ? over.ArrowStart ?? s.ArrowStart : s.ArrowStart,
                ArrowEnd = main ? over.ArrowEnd ?? s.ArrowEnd : s.ArrowEnd,
                ArrowSize = main ? over.ArrowSize ?? s.ArrowSize : s.ArrowSize,
                Dash = main || outline ? over.Dash ?? s.Dash : s.Dash,
            });
        }

        // ------------------------------------------------------------------ actions

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string key, bool fallback, string? group = null, string icon = IconIds.Toggle)
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group, ParamKeys = new[] { key } };

        /// <summary>
        /// A choice parameter as a choice (one option per value, the current one marked; hosts show a dropdown) – never a
        /// cycle button (maintainer 2026-11-25).
        /// </summary>
        public static IEnumerable<ContextAction> Choice(SketchObject o, string id, I18nText label, ParamDef def, string? group = null)
            => ActionChoices.FromParam(o, id, def, label, group, null);

        public static ContextAction TextInput(SketchObject o, string id, I18nText label, string key, string fallback, string? group = null)
            => ContextAction.WithInput(id, label, IconIds.Name, label, o.Str(key, fallback), (x, t) => x.WithParam(key, (t ?? "").Trim()))
                with { Group = group, ParamKeys = new[] { key } };

        /// <summary>Number input clamped to the parameter's limits.</summary>
        public static ContextAction NumberInput(SketchObject o, string id, I18nText label, ParamDef def, string? group = null)
            => ContextAction.WithInput(id, label, IconIds.ForQuantity(def.Unit, def.Key), label, Num(o.Num(def.Key, def.Default?.Number ?? 0), Lang.En, 4),
                (x, t) => x.WithParam(def.Key, Geo.Clamp(ParseNumber(t), def.Min ?? double.MinValue, def.Max ?? double.MaxValue)), def.Unit)
                with { Group = group, ParamKeys = new[] { def.Key } };
    }

    /// <summary>Base class of all object types of the module: figures for slides, not parts of a product.</summary>
    internal abstract class MathType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected MathType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override bool IsPart => false;
        public override Primitive ApplyOverride(Primitive p, Style over) => Mx.Override(p, over);
    }

    /// <summary>Box type: draws a <see cref="Fig"/> in local coordinates centred at (0,0) inside <see cref="SketchObject.Size"/>.</summary>
    internal abstract class MathBoxType : MathType
    {
        protected MathBoxType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        internal abstract void Draw(Fig f, SketchObject o, Lang lang);

        internal Fig Build(SketchObject o, Lang lang)
        {
            var f = new Fig();
            if (o.Size.W > 1e-6 && o.Size.H > 1e-6) Draw(f, o, lang);
            return f.Fit(o.Size);
        }

        /// <summary>Text scale relative to the default (design) size.</summary>
        protected double K(SketchObject o) => Mx.Scale(o.Size, DefaultSize);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Build(o, ctx.Lang).Items;
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Build(o, ctx.Lang).Anchors;
    }

    /// <summary>Point type: draws from the resolved page points (attachments honoured).</summary>
    internal abstract class MathPointType : MathType
    {
        protected MathPointType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        public override GeometryKind Geometry => GeometryKind.Points;
        /// <summary>Ends dropped on a figure (axes, triangle) dock there and follow it.</summary>
        public override bool DocksOnSurface => true;
        protected virtual int MinPoints => 2;

        internal abstract void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p);

        private Fig Build(SketchObject o, RenderContext ctx)
        {
            var f = new Fig();
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count >= MinPoints) Draw(f, o, ctx.Lang, pts);
            return f;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Build(o, ctx).Items;
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Build(o, ctx).Anchors;

        /// <summary>The object with point <paramref name="index"/> moved to <paramref name="p"/> and its attachment dropped.</summary>
        protected static SketchObject WithPoint(SketchObject o, int index, Vec2 p)
        {
            var pts = o.Points!.ToList();
            pts[index] = p;
            var att = o.Attach?.ToList();
            if (att is not null && index < att.Count) att[index] = null;
            return o with { Points = pts, Attach = att };
        }
    }
}
