using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>A tick of an axis: data value, its label and its index (value = index · step).</summary>
    internal readonly record struct Tick(double Value, string Label, long Index);

    /// <summary>Axis ticks: linear steps or multiples of π/2.</summary>
    internal static class Ticks
    {
        public const int MaxTicks = 100;

        public static List<Tick> Linear(double a, double b, double step, Lang lang)
        {
            if (!(step > 0) || !Mx.Finite(step)) step = 1;
            while ((b - a) / step > MaxTicks) step *= 2;
            var list = new List<Tick>();
            var k0 = (long)Math.Ceiling(a / step - 1e-9);
            var k1 = (long)Math.Floor(b / step + 1e-9);
            for (var k = k0; k <= k1; k++)
            {
                var v = k * step;
                list.Add(new Tick(v, Mx.Num(v, lang, 6), k));
            }
            return list;
        }

        /// <summary>Ticks at multiples of π/2 (π or 2π for wide ranges), labelled "π/2", "π", "3π/2", "−π" …</summary>
        public static List<Tick> Pi(double a, double b)
        {
            var halves = 1L;
            while ((b - a) / (halves * Math.PI / 2) > 24) halves *= 2;
            var step = halves * Math.PI / 2;
            var list = new List<Tick>();
            var k0 = (long)Math.Ceiling(a / step - 1e-9);
            var k1 = (long)Math.Floor(b / step + 1e-9);
            for (var k = k0; k <= k1; k++) list.Add(new Tick(k * step, PiLabel(k * halves), k));
            return list;
        }

        /// <summary>Label of n·π/2: "0", "π/2", "π", "3π/2", "2π", "−π/2" …</summary>
        public static string PiLabel(long n)
        {
            if (n == 0) return "0";
            var sign = n < 0 ? "−" : "";
            var m = Math.Abs(n);
            if (m % 2 == 0) { var w = m / 2; return sign + (w == 1 ? "" : w.ToString(CultureInfo.InvariantCulture)) + "π"; }
            return sign + (m == 1 ? "" : m.ToString(CultureInfo.InvariantCulture)) + "π/2";
        }
    }

    /// <summary>Axis settings read from the parameters of an axes or graph object.</summary>
    internal sealed record AxesSpec(double X0, double X1, double Y0, double Y1, double XStep, double YStep, bool Grid, bool Numbers, bool XPi, string XLabel, string YLabel)
    {
        public static IEnumerable<ParamDef> Params(double x0, double x1, double y0, double y1, double yStep = 1) => new[]
        {
            Pm.Num("xMin", "x from", "x von", x0, -1000, 1000, step: 1),
            Pm.Num("xMax", "x to", "x bis", x1, -1000, 1000, step: 1),
            Pm.Num("yMin", "y from", "y von", y0, -1000, 1000, step: 1),
            Pm.Num("yMax", "y to", "y bis", y1, -1000, 1000, step: 1),
            Pm.Num("xStep", "x tick step", "x-Teilung", 1, 0.01, 1000, step: 0.5),
            Pm.Num("yStep", "y tick step", "y-Teilung", yStep, 0.01, 1000, step: 0.5),
            Pm.Bool("grid", "Grid", "Gitternetz", true),
            Pm.Bool("numbers", "Tick numbers", "Achsenzahlen", true),
            Pm.Bool("xPi", "x ticks in multiples of π", "x-Teilung in Vielfachen von π", false),
            Pm.Text("xLabel", "x axis label", "Beschriftung x-Achse", "x"),
            Pm.Text("yLabel", "y axis label", "Beschriftung y-Achse", "y"),
        };

        public static AxesSpec From(SketchObject o, ObjectType t)
        {
            double D(string key) => o.Num(key, t.Parameters.First(p => p.Key == key).Default!.Value.Number!.Value);
            double x0 = D("xMin"), x1 = D("xMax"), y0 = D("yMin"), y1 = D("yMax");
            if (x1 < x0) (x0, x1) = (x1, x0);
            if (y1 < y0) (y0, y1) = (y1, y0);
            if (x1 - x0 < 1e-9) x1 = x0 + 1;
            if (y1 - y0 < 1e-9) y1 = y0 + 1;
            return new AxesSpec(x0, x1, y0, y1, D("xStep"), D("yStep"), o.Flag("grid", true), o.Flag("numbers", true), o.Flag("xPi", false),
                o.Str("xLabel", "x"), o.Str("yLabel", "y"));
        }
    }

    /// <summary>
    /// Layout and drawing of a 2D Cartesian coordinate system inside a box: the plot area maps the data ranges
    /// linearly (free non-uniform scaling stretches the units), the axes cross at the origin (clamped into the ranges),
    /// margins are reserved for arrows, axis labels and tick numbers.
    /// </summary>
    internal sealed class Plot2D
    {
        public AxesSpec S { get; }
        public Rect Area { get; private set; }
        public double Ox { get; }
        public double Oy { get; }
        public double Ts { get; }
        public double Tl { get; }
        public double Over { get; }
        public List<Tick> XTicks { get; }
        public List<Tick> YTicks { get; }

        private Plot2D(AxesSpec s, double ts, double tl, Lang lang)
        {
            S = s; Ts = ts; Tl = tl;
            Over = Math.Max(2.5, tl * 0.7);
            Ox = Geo.Clamp(0, s.X0, s.X1);
            Oy = Geo.Clamp(0, s.Y0, s.Y1);
            XTicks = s.XPi ? Ticks.Pi(s.X0, s.X1) : Ticks.Linear(s.X0, s.X1, s.XStep, lang);
            YTicks = Ticks.Linear(s.Y0, s.Y1, s.YStep, lang);
        }

        public double MapX(double x) => Area.X + (x - S.X0) / (S.X1 - S.X0) * Area.W;
        public double MapY(double y) => Area.Bottom - (y - S.Y0) / (S.Y1 - S.Y0) * Area.H;
        public Vec2 Map(double x, double y) => new(MapX(x), MapY(y));

        public static Plot2D Create(AxesSpec s, Size2 size, double k, Lang lang)
        {
            var p = new Plot2D(s, Mx.NumberSize * k, Mx.LabelSize * k, lang);
            p.Layout(size);
            return p;
        }

        private double MaxW(IEnumerable<Tick> ticks) => ticks.Select(t => Mx.TextW(t.Label, Ts)).DefaultIfEmpty(0).Max();

        private void Layout(Size2 size)
        {
            double hw = size.W / 2, hh = size.H / 2;
            var xlw = S.XLabel.Length == 0 ? 0 : Mx.TextW(S.XLabel, Tl);
            var ylw = S.YLabel.Length == 0 ? 0 : Mx.TextW(S.YLabel, Tl);
            double right = Over + (xlw > 0 ? 0.6 + xlw : 0) + 0.3;
            double top = Over + (ylw > 0 ? 0.4 + Tl : 0) + 0.3;
            double left = 0.3, bottom = 0.3;
            var maxXW = S.Numbers ? MaxW(XTicks) : 0;
            var maxYW = S.Numbers ? MaxW(YTicks) : 0;
            if (S.Numbers)
            {
                left = Math.Max(left, maxXW / 2 + 0.3);
                bottom = Math.Max(bottom, Ts / 2 + 0.3);
            }
            for (var iter = 0; iter < 4; iter++)
            {
                Area = new Rect(-hw + left, -hh + top, Math.Max(1, size.W - left - right), Math.Max(1, size.H - top - bottom));
                double ax = MapX(Ox), ay = MapY(Oy);
                double needLeft = 0, needBottom = 0;
                if (S.Numbers) needLeft = Math.Max(needLeft, maxYW + 1.3 + 0.3 - (ax + hw));
                if (ylw > 0) needLeft = Math.Max(needLeft, ylw / 2 + 0.3 - (ax + hw));
                if (S.Numbers) needBottom = 1.0 + Ts + 0.3 - (hh - ay);
                if (needLeft <= 1e-6 && needBottom <= 1e-6) break;
                left += Math.Max(0, needLeft);
                bottom += Math.Max(0, needBottom);
            }
        }

        /// <summary>Grid, axes with arrowheads, ticks, numbers, axis labels and the anchors (origin, axis rails, grid points).</summary>
        public void Draw(Fig f, Lang lang, IReadOnlyList<Vec2>? avoid = null)
        {
            // Tick numbers move to the other side of their axis where a curve passes through them.
            bool Hit(Rect r) => avoid is not null && avoid.Any(r.Contains);
            double ax = MapX(Ox), ay = MapY(Oy);
            if (S.Grid)
            {
                foreach (var t in XTicks) f.Line(new Vec2(MapX(t.Value), Area.Y), new Vec2(MapX(t.Value), Area.Bottom), Mx.GridLine, MathTags.Grid);
                foreach (var t in YTicks) f.Line(new Vec2(Area.X, MapY(t.Value)), new Vec2(Area.Right, MapY(t.Value)), Mx.GridLine, MathTags.Grid);
            }
            var xEnd = new Vec2(Area.Right + Over, ay);
            var yEnd = new Vec2(ax, Area.Y - Over);
            f.Line(new Vec2(Area.X, ay), xEnd, Mx.Axis, MathTags.Axis);
            f.Line(new Vec2(ax, Area.Bottom), yEnd, Mx.Axis, MathTags.Axis);

            // Label every n-th tick so that numbers never overlap.
            var xSpacing = XTicks.Count > 1 ? Math.Abs(MapX(XTicks[1].Value) - MapX(XTicks[0].Value)) : double.MaxValue;
            var ySpacing = YTicks.Count > 1 ? Math.Abs(MapY(YTicks[1].Value) - MapY(YTicks[0].Value)) : double.MaxValue;
            var xEvery = 1; var maxXW = MaxW(XTicks);
            while (xEvery * xSpacing < maxXW + 1 && xEvery < 1000) xEvery++;
            var yEvery = 1;
            while (yEvery * ySpacing < Ts * 1.15 && yEvery < 1000) yEvery++;

            foreach (var t in XTicks)
            {
                var x = MapX(t.Value);
                if (Math.Abs(t.Value - Ox) < 1e-9) continue;
                f.Line(new Vec2(x, ay - 0.7), new Vec2(x, ay + 0.7), Mx.AxisPlain, MathTags.Tick);
                if (S.Numbers && t.Index % xEvery == 0)
                {
                    var w = Mx.TextW(t.Label, Ts);
                    var y = ay + 1.0 + Ts / 2;
                    var above = new Rect(x - w / 2, ay - 1.0 - Ts, w, Ts);
                    if (Hit(new Rect(x - w / 2, ay + 1.0, w, Ts)) && above.Y >= Area.Y && !Hit(above)) y = ay - 1.0 - Ts / 2;
                    f.Text(new Vec2(x, y), t.Label, Ts, tag: MathTags.Number);
                }
            }
            foreach (var t in YTicks)
            {
                var y = MapY(t.Value);
                if (Math.Abs(t.Value - Oy) < 1e-9) continue;
                f.Line(new Vec2(ax - 0.7, y), new Vec2(ax + 0.7, y), Mx.AxisPlain, MathTags.Tick);
                if (S.Numbers && t.Index % yEvery == 0)
                {
                    var w = Mx.TextW(t.Label, Ts);
                    var right = new Rect(ax + 1.3, y - Ts / 2, w, Ts);
                    if (Hit(new Rect(ax - 1.3 - w, y - Ts / 2, w, Ts)) && right.Right <= Area.Right && !Hit(right))
                        f.Text(new Vec2(ax + 1.3, y), t.Label, Ts, anchor: TextAnchor.Start, tag: MathTags.Number);
                    else f.Text(new Vec2(ax - 1.3, y), t.Label, Ts, anchor: TextAnchor.End, tag: MathTags.Number);
                }
            }
            if (S.Numbers && Math.Abs(Ox) < 1e-12 && Math.Abs(Oy) < 1e-12)
                f.Text(new Vec2(ax - 1.0, ay + 1.0 + Ts / 2), "0", Ts, anchor: TextAnchor.End, tag: MathTags.Number);
            if (S.XLabel.Length > 0) f.Text(new Vec2(xEnd.X + 0.6, ay), S.XLabel, Tl, italic: true, anchor: TextAnchor.Start);
            if (S.YLabel.Length > 0) f.Text(new Vec2(ax, yEnd.Y - 0.4 - Tl / 2), S.YLabel, Tl, italic: true);

            f.Port("origin", new Vec2(ax, ay));
            f.Rail(new Vec2(Area.X, ay), xEnd, "xaxis");
            f.Rail(new Vec2(ax, Area.Bottom), yEnd, "yaxis");
            if (XTicks.Count * YTicks.Count <= 900)
                foreach (var tx in XTicks)
                    foreach (var ty in YTicks)
                        f.Pt(AnchorRole.Mid, Map(tx.Value, ty.Value), "g" + tx.Index.ToString(CultureInfo.InvariantCulture) + "_" + ty.Index.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>2D Cartesian coordinate system with grid, ticks, numbers and axis labels.</summary>
    internal sealed class CartesianAxesType : MathBoxType
    {
        public CartesianAxesType() : base("math.axes.cartesian", new I18nText("Coordinate system (2D)", "Koordinatensystem (2D)"), MathCategories.Axes,
            "coordinate system", "axes", "cartesian", "grid", "koordinatensystem", "achsenkreuz", "kartesisch", "gitter", "xy") { }

        public override Size2 DefaultSize => new(70, 60);
        public override IReadOnlyList<string> Companions => new[] { "math.vector.arrow", "math.point.labelled", GeometryModule.ChartFunctionType, "math.graph.function" };
        public override IReadOnlyList<ParamDef> Parameters { get; } = AxesSpec.Params(-5, 5, -4, 4).ToList();

        internal override void Draw(Fig f, SketchObject o, Lang lang)
            => Plot2D.Create(AxesSpec.From(o, this), o.Size, K(o), lang).Draw(f, lang);

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => AxesActions(o, this);

        internal static IEnumerable<ContextAction> AxesActions(SketchObject o, ObjectType t)
        {
            yield return Mx.Toggle(o, "math.axes.grid", new I18nText("Grid", "Gitternetz"), "grid", true, "axes");
            yield return Mx.Toggle(o, "math.axes.numbers", new I18nText("Tick numbers", "Achsenzahlen"), "numbers", true, "axes");
            var s = AxesSpec.From(o, t);
            yield return RangeInput("math.axes.xrange", new I18nText("x range…", "x-Bereich…"), "xMin", "xMax", s.X0, s.X1);
            yield return RangeInput("math.axes.yrange", new I18nText("y range…", "y-Bereich…"), "yMin", "yMax", s.Y0, s.Y1);
        }

        internal static ContextAction RangeInput(string id, I18nText label, string minKey, string maxKey, double min, double max)
            => ContextAction.WithInput(id, label, IconIds.Text, label, Mx.Num(min, Lang.En, 4) + ";" + Mx.Num(max, Lang.En, 4).Replace('−', '-'),
                (x, t) =>
                {
                    var (a, b) = Mx.ParseRange(t);
                    return x.WithParam(minKey, Geo.Clamp(a, -1000, 1000)).WithParam(maxKey, Geo.Clamp(b, -1000, 1000));
                }) with { Group = "range", ParamKeys = new[] { minKey, maxKey } };
    }

    /// <summary>Number line with ticks, optional minor ticks and numbers.</summary>
    internal sealed class NumberLineType : MathBoxType
    {
        public NumberLineType() : base("math.axes.number", new I18nText("Number line", "Zahlenstrahl"), MathCategories.Axes,
            "number line", "number ray", "zahlenstrahl", "zahlengerade", "skala", "scale") { }

        public override Size2 DefaultSize => new(90, 14);
        public override IReadOnlyList<string> Companions => new[] { "math.point.labelled" };

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("min", "From", "Von", -5, -1000, 1000, step: 1),
            Pm.Num("max", "To", "Bis", 5, -1000, 1000, step: 1),
            Pm.Num("step", "Tick step", "Teilung", 1, 0.01, 1000, step: 0.5),
            Pm.Int("minor", "Minor ticks between", "Zwischenstriche", 0, 0, 9),
            Pm.Bool("numbers", "Numbers", "Zahlen", true),
            Pm.Text("label", "Label", "Beschriftung", ""),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            double a = o.Num("min", -5), b = o.Num("max", 5);
            if (b < a) (a, b) = (b, a);
            if (b - a < 1e-9) b = a + 1;
            var k = K(o);
            double ts = Mx.NumberSize * k * 1.15, tl = Mx.LabelSize * k;
            var ticks = Ticks.Linear(a, b, o.Num("step", 1), lang);
            var numbers = o.Flag("numbers", true);
            var label = o.Str("label", "");
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var over = Math.Max(2.5, tl * 0.7);
            var maxW = numbers ? ticks.Select(t => Mx.TextW(t.Label, ts)).DefaultIfEmpty(0).Max() : 0;
            var left = Math.Max(0.3, maxW / 2 + 0.3);
            var right = over + 0.3 + (label.Length > 0 ? 0.6 + Mx.TextW(label, tl) : 0);
            right = Math.Max(right, maxW / 2 + 0.3);
            var x0 = -hw + left; var x1 = hw - right;
            if (x1 - x0 < 1) x1 = x0 + 1;
            var groupH = 1.0 + (numbers ? 1.0 + ts : 0);
            var y = -hh + (o.Size.H - groupH) / 2 + 1.0;
            double X(double v) => x0 + (v - a) / (b - a) * (x1 - x0);
            var end = new Vec2(x1 + over, y);
            f.Line(new Vec2(x0, y), end, Mx.Axis with { StrokeWidth = 0.5 }, MathTags.Axis);
            var minor = o.Int("minor", 0);
            var spacing = ticks.Count > 1 ? X(ticks[1].Value) - X(ticks[0].Value) : double.MaxValue;
            var every = 1;
            while (every * spacing < maxW + 1 && every < 1000) every++;
            foreach (var t in ticks)
            {
                var x = X(t.Value);
                var major = t.Index % every == 0;
                f.Line(new Vec2(x, y - 1.0), new Vec2(x, y + 1.0), Mx.AxisPlain, MathTags.Tick);
                if (numbers && major) f.Text(new Vec2(x, y + 1.0 + 0.6 + ts / 2), t.Label, ts, tag: MathTags.Number);
                f.Pt(AnchorRole.Mid, new Vec2(x, y), "t" + t.Index.ToString(CultureInfo.InvariantCulture));
                if (Math.Abs(t.Value) < 1e-12) f.Port("zero", new Vec2(x, y));
            }
            if (minor > 0 && spacing < double.MaxValue)
            {
                var step = ticks.Count > 1 ? ticks[1].Value - ticks[0].Value : 1;
                var fine = step / (minor + 1);
                var count = (b - a) / fine;
                if (count <= 1000)
                    for (var v = Math.Ceiling(a / fine - 1e-9) * fine; v <= b + 1e-9; v += fine)
                        if (Math.Abs(Math.IEEERemainder(v, step)) > fine * 1e-3)
                            f.Line(new Vec2(X(v), y - 0.5), new Vec2(X(v), y + 0.5), Mx.Thin with { StrokeWidth = 0.2 }, MathTags.Tick);
            }
            if (label.Length > 0) f.Text(new Vec2(end.X + 0.6, y), label, tl, italic: true, anchor: TextAnchor.Start);
            f.Rail(new Vec2(x0, y), end, "line");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Toggle(o, "math.number.numbers", new I18nText("Numbers", "Zahlen"), "numbers", true, "axes");
            yield return CartesianAxesType.RangeInput("math.number.range", new I18nText("Range…", "Bereich…"), "min", "max", o.Num("min", -5), o.Num("max", 5));
            yield return new ContextAction("math.number.minor.more", new I18nText("More minor divisions", "Feinere Unterteilung"), IconIds.Increase,
                x => x.WithParam("minor", Math.Min(9, x.Int("minor", 0) + 1))) { Group = "minor", ParamKeys = new[] { "minor" }, IsEnabled = o.Int("minor", 0) < 9 };
            yield return new ContextAction("math.number.minor.less", new I18nText("Fewer minor divisions", "Gröbere Unterteilung"), IconIds.Decrease,
                x => x.WithParam("minor", Math.Max(0, x.Int("minor", 0) - 1))) { Group = "minor", ParamKeys = new[] { "minor" }, IsEnabled = o.Int("minor", 0) > 0 };
        }
    }

    /// <summary>Spatial (3D) axes in oblique (cabinet) or isometric projection.</summary>
    internal sealed class SpatialAxesType : MathBoxType
    {
        public SpatialAxesType() : base("math.axes.spatial", new I18nText("Coordinate system (3D)", "Koordinatensystem (3D)"), MathCategories.Axes,
            "3d", "spatial", "oblique", "isometric", "räumlich", "schrägbild", "isometrie", "xyz", "x1x2x3", "raum") { }

        public override Size2 DefaultSize => new(50, 50);
        public override bool PreferUniformScale => true;

        public static readonly ParamDef Projection = Pm.Choice("projection", "Projection", "Projektion", "oblique",
            ("oblique", "Oblique (cabinet, 45°, ½√2)", "Schrägbild (45°, ½√2)"), ("isometric", "Isometric", "Isometrisch"));
        public static readonly ParamDef Names = Pm.Choice("names", "Axis names", "Achsennamen", "xyz",
            ("xyz", "x, y, z", "x, y, z"), ("indexed", "x₁, x₂, x₃", "x₁, x₂, x₃"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Projection, Names,
            Pm.Int("ticks", "Unit ticks", "Einheitsstriche", 4, 0, 10),
            Pm.Bool("negative", "Negative half-axes", "Negative Halbachsen", true),
            Pm.Bool("numbers", "Unit numbers", "Einheitszahlen", true),
        };

        /// <summary>Screen directions per unit of the x, y and z axes.</summary>
        internal static (Vec2 X, Vec2 Y, Vec2 Z) Units(string projection) => projection == "isometric"
            ? (new Vec2(-Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)), new Vec2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)), new Vec2(0, -1))
            : (new Vec2(-0.5, 0.5), new Vec2(1, 0), new Vec2(0, -1));

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var (ux, uy, uz) = Units(o.Str("projection", "oblique"));
            var ticks = Math.Max(0, Math.Min(10, o.Int("ticks", 4)));
            var neg = o.Flag("negative", true) ? 1.5 : 0;
            var len = Math.Max(ticks, 1) + 1.2;
            var k = K(o);
            double tl = Mx.LabelSize * k, ts = Mx.NumberSize * k;
            var indexed = o.Str("names", "xyz") == "indexed";
            var names = indexed ? new[] { "x₁", "x₂", "x₃" } : new[] { "x", "y", "z" };
            var dirs = new[] { ux, uy, uz };
            var pts = new List<Vec2> { Vec2.Zero };
            foreach (var d in dirs) { pts.Add(d * len); pts.Add(-d * neg); }
            var margin = 0.8 + names.Max(n => Math.Max(Mx.TextW(n, tl), tl)) + 0.3;
            var area = new Rect(-o.Size.W / 2 + margin, -o.Size.H / 2 + margin, Math.Max(1, o.Size.W - 2 * margin), Math.Max(1, o.Size.H - 2 * margin));
            var (map, _) = Mx.FitUniform(pts, area);
            var origin = map(Vec2.Zero);
            f.Port("origin", origin);
            for (var i = 0; i < 3; i++)
            {
                var d = dirs[i];
                var u = d.Normalized();
                var axisId = indexed ? "x" + (i + 1).ToString(CultureInfo.InvariantCulture) : names[i];
                var end = map(d * len);
                f.Line(map(-d * neg), end, Mx.Axis, MathTags.Axis);
                f.Text(Mx.Beside(end, u, names[i], tl, 0.8), names[i], tl, italic: true);
                var n = u.Perp;
                for (var t = 1; t <= ticks; t++)
                {
                    var p = map(d * t);
                    f.Line(p - n * 0.7, p + n * 0.7, Mx.AxisPlain, MathTags.Tick);
                    f.Pt(AnchorRole.Mid, p, axisId + "_" + t.ToString(CultureInfo.InvariantCulture));
                }
                if (o.Flag("numbers", true) && ticks > 0)
                {
                    // "1" beside the first unit tick, on the side away from the other axes.
                    var side = i == 2 ? new Vec2(-1, 0) : new Vec2(0, 1);
                    f.Text(Mx.Beside(map(d), side, "1", ts, 1.0), "1", ts, tag: MathTags.Number);
                }
                f.Rail(map(-d * neg), end, "axis-" + axisId);
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Cycle(o, "math.spatial.projection", new I18nText("Switch projection", "Projektion wechseln"), Projection, "axes");
            yield return Mx.Cycle(o, "math.spatial.names", new I18nText("Switch axis names", "Achsennamen wechseln"), Names, "axes");
            yield return Mx.Toggle(o, "math.spatial.negative", new I18nText("Negative half-axes", "Negative Halbachsen"), "negative", true, "axes");
        }
    }

    /// <summary>Polar grid: concentric circles and rays with angle labels (mathematical sense, counter-clockwise).</summary>
    internal sealed class PolarGridType : MathBoxType
    {
        public PolarGridType() : base("math.axes.polar", new I18nText("Polar grid", "Polarkoordinatennetz"), MathCategories.Axes,
            "polar", "polar coordinates", "polarkoordinaten", "kreisnetz", "winkel", "angle") { }

        public override Size2 DefaultSize => new(64, 64);
        public override bool PreferUniformScale => true;

        public static readonly ParamDef Step = Pm.Choice("step", "Angle step", "Winkelschritt", "30",
            ("15", "15°", "15°"), ("30", "30°", "30°"), ("45", "45°", "45°"), ("90", "90°", "90°"));
        public static readonly ParamDef Unit = Pm.Choice("unit", "Angle unit", "Winkeleinheit", "deg", ("deg", "Degrees", "Grad"), ("rad", "Radians (π)", "Bogenmaß (π)"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Int("rings", "Rings", "Ringe", 4, 1, 10),
            Step, Unit,
            Pm.Bool("angleLabels", "Angle labels", "Winkelbeschriftung", true),
            Pm.Bool("radiusLabels", "Radius numbers", "Radiuszahlen", true),
        };

        internal static string AngleLabel(int deg, string unit, Lang lang)
        {
            if (unit != "rad") return deg.ToString(CultureInfo.InvariantCulture) + "°";
            if (deg == 0) return "0";
            int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
            var g = Gcd(deg, 180);
            int p = deg / g, q = 180 / g;
            return (p == 1 ? "" : p.ToString(CultureInfo.InvariantCulture)) + "π" + (q == 1 ? "" : "/" + q.ToString(CultureInfo.InvariantCulture));
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var rings = Math.Max(1, Math.Min(10, o.Int("rings", 4)));
            var step = int.TryParse(o.Str("step", "30"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var st) && st > 0 && 360 % st == 0 ? st : 30;
            var unit = o.Str("unit", "deg");
            var k = K(o);
            var ts = Mx.NumberSize * k;
            var labels = o.Flag("angleLabels", true);
            var angles = Enumerable.Range(0, 360 / step).Select(i => i * step).ToList();
            var margin = 0.3 + (labels ? 0.8 + angles.Max(a => Math.Max(Mx.TextW(AngleLabel(a, unit, lang), ts), ts)) : 0);
            var r = Math.Max(1, Math.Min(o.Size.W, o.Size.H) / 2 - margin);
            var c = Vec2.Zero;
            for (var i = 1; i <= rings; i++)
                f.Circle(c, r * i / rings, i == rings ? Mx.Thin : Mx.GridLine with { StrokeWidth = 0.2, Stroke = Mx.Grey }, MathTags.Grid);
            foreach (var a in angles)
            {
                var d = Mx.Dir(a);
                if (a == 0) f.Line(c, c + d * r, Mx.Axis, MathTags.Axis);
                else f.Line(c, c + d * r, Mx.GridLine with { StrokeWidth = 0.2, Stroke = Mx.Grey }, MathTags.Grid);
                if (labels)
                {
                    var text = AngleLabel(a, unit, lang);
                    f.Text(Mx.Beside(c + d * r, d, text, ts, 0.8), text, ts, tag: MathTags.Number);
                }
                for (var i = 1; i <= rings; i++) f.Pt(AnchorRole.Mid, c + d * (r * i / rings), "r" + i.ToString(CultureInfo.InvariantCulture) + "a" + a.ToString(CultureInfo.InvariantCulture));
            }
            if (o.Flag("radiusLabels", true))
                for (var i = 1; i < rings; i++)
                    f.Text(new Vec2(r * i / rings + 0.5, 0.6 + ts / 2), i.ToString(CultureInfo.InvariantCulture), ts, anchor: TextAnchor.Start, tag: MathTags.Number);
            f.Dot(c, 0.5);
            f.Port("pole", c);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("math.polar.rings.more", new I18nText("More rings", "Mehr Ringe"), IconIds.Increase,
                x => x.WithParam("rings", Math.Min(10, x.Int("rings", 4) + 1))) { Group = "rings", ParamKeys = new[] { "rings" }, IsEnabled = o.Int("rings", 4) < 10 };
            yield return new ContextAction("math.polar.rings.less", new I18nText("Fewer rings", "Weniger Ringe"), IconIds.Decrease,
                x => x.WithParam("rings", Math.Max(1, x.Int("rings", 4) - 1))) { Group = "rings", ParamKeys = new[] { "rings" }, IsEnabled = o.Int("rings", 4) > 1 };
            yield return Mx.Cycle(o, "math.polar.step", new I18nText("Next angle step", "Nächster Winkelschritt"), Step, "angles");
            yield return Mx.Cycle(o, "math.polar.unit", new I18nText("Degrees / radians", "Grad / Bogenmaß"), Unit, "angles");
        }
    }
}
