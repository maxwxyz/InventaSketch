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

namespace InventaSketch.Modules.Process
{
    /// <summary>Library category ids of the process &amp; thermal module.</summary>
    public static class ProcessCategories
    {
        public const string Vessels = "process.vessels";
        public const string Heat = "process.heat";
        public const string Machines = "process.machines";
        public const string Valves = "process.valves";
        public const string Instruments = "process.instruments";
        public const string Lines = "process.lines";
        public const string Hvac = "process.hvac";
        public const string Thermo = "process.thermo";
    }

    /// <summary>View ids of the module.</summary>
    public static class ProcessViews
    {
        /// <summary>P&amp;ID / flow sheet symbol (default view of every equipment and fitting).</summary>
        public const string Symbol = "symbol";
        /// <summary>Block of a block flow diagram: a rectangle with the equipment name and tag.</summary>
        public const string Block = "block";
        /// <summary>Thermodynamic diagram (axes with an idealised cycle).</summary>
        public const string Chart = "chart";

        public static readonly ViewDef SymbolView = new(Symbol, new I18nText("P&ID symbol", "R&I-Symbol")) { Icon = IconIds.Symbol };
        public static readonly ViewDef BlockView = new(Block, new I18nText("Block (block flow diagram)", "Block (Grundfließschema)")) { Icon = IconIds.Block };
        public static readonly ViewDef ChartView = new(Chart, new I18nText("Diagram", "Diagramm")) { Icon = IconIds.Chart };
    }

    /// <summary>Semantic primitive tags (tests, host naming).</summary>
    public static class ProcessTags
    {
        public const string Tag = "tag";
        public const string Body = "body";
        public const string Line = "line";
        public const string Junction = "junction";
        public const string Axis = "axis";
        public const string Cycle = "cycle";
        public const string Dome = "dome";
        public const string State = "state";
        public const string Label = "label";
    }

    /// <summary>Shared line styles (black only, so monochrome output is unchanged).</summary>
    internal static class S
    {
        /// <summary>Equipment outlines (one uniform weight as usual in flow sheets).</summary>
        public static readonly Style Sym = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Emphasised strokes (main process lines, tube sheets).</summary>
        public static readonly Style Bold = Style.Visible with { StrokeWidth = 0.5 };
        /// <summary>Thin details (trays, internals, springs, hatching).</summary>
        public static readonly Style Thin = Style.ThinLine with { StrokeWidth = 0.25 };
        /// <summary>Very fine lines (plates, packing).</summary>
        public static readonly Style Fine = Style.ThinLine with { StrokeWidth = 0.18 };
        /// <summary>Dashed thin line (concealed internals, tube bundles, jackets).</summary>
        public static readonly Style Dashed = Style.ThinLine with { StrokeWidth = 0.25, Dash = DashStyle.Dash };
        /// <summary>Filled black (closed valves, arrow heads, dots).</summary>
        public static readonly Style Black = Sym with { Fill = Fill.Solid("#000000") };
        /// <summary>White-filled outline (instrument bubbles lying on top of lines).</summary>
        public static readonly Style White = Sym with { Fill = Fill.Solid("#ffffff") };
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class P
    {
        public static I18nText T(string en, string de) => new(en, de);

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, T(en, de), ParamKind.Choice) { Default = def, Options = options.Select(x => new ParamOption(x.Value, T(x.En, x.De))).ToList() };

        public static ParamDef Flag(string key, string en, string de, bool def) => new(key, T(en, de), ParamKind.Boolean) { Default = def };

        public static ParamDef Num(string key, string en, string de, double def, double min, double max, double step, string? unit = null, bool advanced = false)
            => new(key, T(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Step = step, Unit = unit, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, T(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Text(string key, string en, string de, string def) => new(key, T(en, de), ParamKind.Text) { Default = def };

        /// <summary>Cycles a choice parameter through its options; the label names the parameter.</summary>
        /// <summary>
        /// A choice parameter as a choice (one option per value, the current one marked; hosts show a dropdown) – never a
        /// cycle button (maintainer 2026-11-25). The label defaults to the parameter's.
        /// </summary>
        public static IEnumerable<ContextAction> Choice(SketchObject o, string id, I18nText? label, ParamDef def, string group = "param")
            => ActionChoices.FromParam(o, id, def, label, group, null);

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string key, bool def, string icon = IconIds.Toggle, string group = "param")
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = group, ParamKeys = new[] { key } };

        /// <summary>Steps an integer parameter up or down within its limits.</summary>
        public static ContextAction Step(string id, I18nText label, ParamDef def, int delta)
        {
            var fallback = (int)(def.Default?.Number ?? 0);
            int min = (int)(def.Min ?? int.MinValue), max = (int)(def.Max ?? int.MaxValue);
            return new ContextAction(id, label, delta > 0 ? IconIds.CountMore : IconIds.CountFewer,
                x => x.WithParam(def.Key, Math.Max(min, Math.Min(max, x.Int(def.Key, fallback) + delta)))) { Group = "param", ParamKeys = new[] { def.Key } };
        }

        public static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Nozzles that do not depend on parameters.</summary>
        public static Func<SketchObject, IReadOnlyList<Port>> Fixed(params Port[] ports) => _ => ports;
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for a reference box (the design size, centred at 0,0)
    /// and stretched to the actual object size, so free scaling simply stretches the drawing. Small details (arrow heads,
    /// dots, text) use the uniform detail scale <see cref="K"/>; text is kept upright on the page via
    /// <see cref="TextRotation"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();

        public Pen(Size2 actual, Size2 design, double textRotation = 0, Lang lang = Lang.En)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = Math.Max(design.W, 0.01);
            DH = Math.Max(design.H, 0.01);
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
        /// <summary>Half design width and height.</summary>
        public double Hw => DW / 2;
        public double Hh => DH / 2;

        public IReadOnlyList<Primitive> Items => _items;

        /// <summary>Design point to local (actual) point.</summary>
        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => V(p.X, p.Y);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? S.Sym), tag);

        /// <summary>Open or closed polyline from design coordinate pairs.</summary>
        public void Poly(Style s, bool closed, string? tag, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed), tag);
        }

        public void PolyD(IEnumerable<Vec2> design, Style? s = null, bool closed = false, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s ?? S.Sym, closed), tag);

        /// <summary>Axis-aligned rectangle given by two design corners (optional corner radius in design mm).</summary>
        public void Rect(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null, double radius = 0)
        {
            var a = V(x0, y0);
            var b = V(x1, y1);
            var w = Math.Abs(b.X - a.X);
            var h = Math.Abs(b.Y - a.Y);
            Add(Prim.Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), w, h, s ?? S.Sym, Math.Min(radius * K, Math.Min(w, h) / 2)), tag);
        }

        /// <summary>Circle that follows the stretch (becomes an ellipse when the box is scaled non-uniformly).</summary>
        public void Circle(double cx, double cy, double r, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Ellipse(c.X, c.Y, r * Sx, r * Sy, s ?? S.Sym), tag);
        }

        /// <summary>Ellipse that follows the stretch.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Ellipse(c.X, c.Y, rx * Sx, ry * Sy, s ?? S.Sym), tag);
        }

        /// <summary>True circle at a design centre with a radius in design mm scaled by the detail scale.</summary>
        public void Round(double cx, double cy, double r, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, r * K, s ?? S.Sym), tag);
        }

        /// <summary>Filled dot.</summary>
        public void Dot(double cx, double cy, double r = 0.6, string? tag = "dot") => Round(cx, cy, r, S.Black, tag);

        /// <summary>Elliptical arc in design coordinates (degrees, positive sweep = clockwise on screen).</summary>
        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style? s = null, string? tag = null)
            => PolyD(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg)), s, false, tag);

        /// <summary>Arc points (design coordinates) for composing closed outlines.</summary>
        public static List<Vec2> ArcPts(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg)
            => Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg));

        /// <summary>Text at a design point; size in design mm (detail scale), kept upright on the page.</summary>
        public void Text(double x, double y, string text, double size, TextAnchor anchor = TextAnchor.Middle, string? tag = ProcessTags.Label, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var ts = new TextStyle { Size = size * K, Anchor = anchor, Baseline = TextBaseline.Middle, Bold = bold };
            var p = V(x, y);
            Add(Prim.Text(p.X, p.Y, text, ts, TextRotation), tag);
        }

        /// <summary>Text fitted into a design width (shrinks long texts); size in design mm.</summary>
        public void FitText(double x, double y, string text, double size, double maxW, TextAnchor anchor = TextAnchor.Middle, string? tag = ProcessTags.Label, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var fit = Math.Min(size, maxW * (Sx / K) / (Math.Max(1, text.Length) * (bold ? 0.62 : 0.56)));
            Text(x, y, text, fit, anchor, tag, bold);
        }

        /// <summary>Filled arrow head with its tip at design (tx, ty), pointing away from design (fx, fy). Sizes in design mm (detail scale).</summary>
        public void Head(double tx, double ty, double fx, double fy, double len = 1.4, double half = 0.5, string? tag = "head")
        {
            var tip = V(tx, ty);
            var dir = (tip - V(fx, fy)).Normalized();
            if (dir.Length < Geo.Eps) return;
            var n = dir.Perp;
            var b = tip - dir * (len * K);
            Add(Prim.Polygon(new[] { tip, b + n * (half * K), b - n * (half * K) }, S.Black), tag);
        }

        /// <summary>Line from (fx, fy) to (tx, ty) with a filled arrow head at the end.</summary>
        public void Arrow(double fx, double fy, double tx, double ty, Style? s = null, string? tag = "arrow", double len = 1.4, double half = 0.5)
        {
            var a = V(fx, fy);
            var tip = V(tx, ty);
            var dir = (tip - a).Normalized();
            Add(Prim.Line(a, tip - dir * (len * K * 0.8), s ?? S.Sym), tag);
            Head(tx, ty, fx, fy, len, half, tag);
        }

        /// <summary>Valve body: two triangles meeting at (cx, cy), half length hl and half height hh (design mm).</summary>
        public void BowTie(double cx, double cy, double hl, double hh, Style? left = null, Style? right = null, string? tag = "valve")
        {
            Poly(left ?? S.Sym, true, tag, cx, cy, cx - hl, cy - hh, cx - hl, cy + hh);
            Poly(right ?? S.Sym, true, tag, cx, cy, cx + hl, cy - hh, cx + hl, cy + hh);
        }

        /// <summary>Zigzag between (x0, y) and (x1, y) with n peaks of amplitude a (design mm).</summary>
        public void ZigzagH(double x0, double x1, double y, double a, int n, Style? s = null, string? tag = null)
        {
            var pts = new List<Vec2> { new(x0, y) };
            var step = (x1 - x0) / (2 * n);
            for (var i = 0; i < 2 * n; i++) pts.Add(new Vec2(x0 + step * (i + 0.5), y + (i % 2 == 0 ? -a : a)));
            pts.Add(new Vec2(x1, y));
            PolyD(pts, s, false, tag);
        }

        /// <summary>Vertical zigzag between (x, y0) and (x, y1).</summary>
        public void ZigzagV(double x, double y0, double y1, double a, int n, Style? s = null, string? tag = null)
        {
            var pts = new List<Vec2> { new(x, y0) };
            var step = (y1 - y0) / (2 * n);
            for (var i = 0; i < 2 * n; i++) pts.Add(new Vec2(x + (i % 2 == 0 ? -a : a), y0 + step * (i + 0.5)));
            pts.Add(new Vec2(x, y1));
            PolyD(pts, s, false, tag);
        }
    }
}
