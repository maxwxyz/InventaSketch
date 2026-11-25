using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Settings of a math-style coordinate system (charts v2 stage 4, the merge of the math module's coordinate systems and
    /// function graphs into the charts module): ranges, tick steps, grid, numbers, π ticks and axis names.
    /// </summary>
    internal static class MathAxes
    {
        public static ParamDef XMin(double v) => Prm.Num("xmin", "x from", "x von", v, -1000, 1000, step: 1);
        public static ParamDef XMax(double v) => Prm.Num("xmax", "x to", "x bis", v, -1000, 1000, step: 1);
        public static ParamDef YMin(double v) => Prm.Num("ymin", "y from", "y von", v, -1000, 1000, step: 1);
        public static ParamDef YMax(double v) => Prm.Num("ymax", "y to", "y bis", v, -1000, 1000, step: 1);
        public static readonly ParamDef XStep = Prm.Num("xstep", "x tick step", "x-Teilung", 1, 0.01, 1000, step: 0.5);
        public static readonly ParamDef YStep = Prm.Num("ystep", "y tick step", "y-Teilung", 1, 0.01, 1000, step: 0.5);
        public static readonly ParamDef Grid = Prm.Bool("grid", "Grid", "Gitternetz", true);
        public static readonly ParamDef Numbers = Prm.Bool("numbers", "Tick numbers", "Achsenzahlen", true);
        public static readonly ParamDef XPi = Prm.Bool("xpi", "x ticks in multiples of π", "x-Teilung in Vielfachen von π", false);
        public static readonly ParamDef XName = Prm.Text("xname", "x-axis name", "Name der x-Achse", "x");
        public static readonly ParamDef YName = Prm.Text("yname", "y-axis name", "Name der y-Achse", "y");

        public static IReadOnlyList<ParamDef> Params(double x0, double x1, double y0, double y1) => new[]
        {
            XMin(x0), XMax(x1), YMin(y0), YMax(y1), XStep, YStep, Grid, Numbers, XPi, XName, YName, Common.Title,
            ChartBreaks.YBreaks, ChartBreaks.XBreaks, ChartBreaks.Style,
        };

        /// <summary>The ranges (ordered, at least 1e-9 wide) of an object; defaults from the type's parameters.</summary>
        public static (double X0, double X1, double Y0, double Y1) Ranges(SketchObject o, ObjectType t)
        {
            double D(string key) => o.Num(key, t.Parameters.First(p => p.Key == key).Default!.Value.Number!.Value);
            double x0 = D("xmin"), x1 = D("xmax"), y0 = D("ymin"), y1 = D("ymax");
            if (x1 < x0) (x0, x1) = (x1, x0);
            if (y1 < y0) (y0, y1) = (y1, y0);
            if (x1 - x0 < 1e-9) x1 = x0 + 1;
            if (y1 - y0 < 1e-9) y1 = y0 + 1;
            return (x0, x1, y0, y1);
        }

        /// <summary>A linear axis over a fixed range with ticks every <paramref name="step"/> (coarser when there would be more than 100).</summary>
        public static Axis Linear(double min, double max, double step)
        {
            if (!(step > 0) || !Fmt.Finite(step)) step = 1;
            return Axis.Linear(Array.Empty<double>(), (min, max), target: 100, steps: new[] { step });
        }

        /// <summary>Ticks at multiples of π/2 (π or 2π for wide ranges), labelled "π/2", "π", "3π/2", "−π" …</summary>
        public static IReadOnlyList<Tick> PiTicks(double a, double b)
        {
            var halves = 1L;
            while ((b - a) / (halves * Math.PI / 2) > 24) halves *= 2;
            var step = halves * Math.PI / 2;
            var list = new List<Tick>();
            var k0 = (long)Math.Ceiling(a / step - 1e-9);
            var k1 = (long)Math.Floor(b / step + 1e-9);
            for (var k = k0; k <= k1 && list.Count < 200; k++) list.Add(new Tick(k * step, TickMark.Major, PiLabel(k * halves)));
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

        /// <summary>
        /// The math-style frame of an object: axes through the origin over the ranges, breaks, grid, numbers, π ticks and names.
        /// Not laid out yet (the caller adds what the layout needs, then calls <see cref="Frame.Layout"/>).
        /// </summary>
        public static (Frame Frame, Panel Panel) Build(SketchObject o, ObjectType t, Size2 design, RenderContext ctx)
        {
            var (x0, x1, y0, y1) = Ranges(o, t);
            var pi = o.Flag("xpi", false);
            var x = ChartBreaks.Apply(Linear(x0, x1, pi ? Math.PI / 2 : o.Num("xstep", 1)), o, ChartBreaks.XKey, null);
            var y = ChartBreaks.Apply(Linear(y0, y1, o.Num("ystep", 1)), o, ChartBreaks.YKey, null);
            var panel = new Panel(y, null);
            var f = new Frame(o.Size, design, x, null, panel)
            {
                MathAxes = true,
                Title = Common.TitleOf(o),
                Grid = o.Flag("grid", true),
                Numbers = o.Flag("numbers", true),
                XName = o.Str("xname", "x") is { Length: > 0 } xn ? xn : null,
                YName = o.Str("yname", "y") is { Length: > 0 } yn ? yn : null,
                LegendMode = "none",
                Mono = ChartType.Mono(ctx),
            };
            if (pi) f.XTicks = PiTicks(x0, x1);
            return (f, panel);
        }

        /// <summary>Actions of a coordinate system: grid, numbers, ranges, π ticks.</summary>
        public static IEnumerable<ContextAction> Actions(SketchObject o, ObjectType t)
        {
            yield return Prm.Toggle(o, "chart.grid", "grid", true, new I18nText("Grid", "Gitternetz"));
            yield return Prm.Toggle(o, "chart.numbers", "numbers", true, new I18nText("Tick numbers", "Achsenzahlen"));
            yield return Prm.Toggle(o, "chart.xpi", "xpi", false, new I18nText("x ticks in multiples of π", "x-Teilung in Vielfachen von π"));
            var (x0, x1, y0, y1) = Ranges(o, t);
            yield return RangeInput("chart.xrange", new I18nText("x range…", "x-Bereich…"), "xmin", "xmax", x0, x1);
            yield return RangeInput("chart.yrange", new I18nText("y range…", "y-Bereich…"), "ymin", "ymax", y0, y1);
        }

        private static ContextAction RangeInput(string id, I18nText label, string minKey, string maxKey, double min, double max)
            => ContextAction.WithInput(id, label, IconIds.Text, new I18nText(label.En + " (from;to)", (label.De ?? label.En) + " (von;bis)"),
                Fmt.Num(min, Lang.En, 4) + ";" + Fmt.Num(max, Lang.En, 4),
                (x, text) =>
                {
                    if (ChartData.Range(text) is not { } r)
                        throw new FormatException("Expected two numbers “from;to” / Erwartet: zwei Zahlen „von;bis“: “" + text + "”");
                    return x.WithParam(minKey, Geo.Clamp(r.Min, -1000, 1000)).WithParam(maxKey, Geo.Clamp(r.Max, -1000, 1000));
                }) with { Group = "range", ParamKeys = new[] { minKey, maxKey } };

        /// <summary>The break actions of a coordinate system (suggestions from the given values).</summary>
        public static IEnumerable<ContextAction> BreakActions(SketchObject o, ObjectType t, IEnumerable<double> xValues, IEnumerable<double> yValues)
        {
            var (x0, x1, y0, y1) = Ranges(o, t);
            return ChartBreaks.Actions(o, Linear(x0, x1, o.Num("xstep", 1)), xValues, Linear(y0, y1, o.Num("ystep", 1)), yValues);
        }
    }

    /// <summary>
    /// 2D coordinate system in math style (charts v2 stage 4; replaces the library item of the math module's Cartesian
    /// axes): axes through the origin with arrowheads, grid, tick numbers, π ticks, axis names, axis breaks. The origin is a
    /// port, the axes are rails and the grid points are snap points, so points and vectors dock on it.
    /// </summary>
    internal sealed class MathAxesChart : ChartType
    {
        public const string TypeId = "chart.math.axes";

        public MathAxesChart() : base(TypeId, new I18nText("Coordinate system (2D)", "Koordinatensystem (2D)"), ChartCategories.Math,
            "coordinate system", "axes", "cartesian", "grid", "origin", "xy", "math",
            "koordinatensystem", "achsenkreuz", "kartesisch", "gitter", "ursprung", "mathematik")
        { }

        public override Size2 Design => new(70, 60);
        public override IReadOnlyList<string> Companions => new[] { "math.vector.arrow", "math.point.labelled", FunctionChart.TypeId };

        protected override IReadOnlyList<ParamDef> BuildParams() => MathAxes.Params(-5, 5, -4, 4);

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var (f, _) = MathAxes.Build(o, this, Design, ctx);
            f.Layout(ctx.Lang);
            return new Chart(f, ctx.Lang);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => MathAxes.Actions(o, this).Concat(MathAxes.BreakActions(o, this, Array.Empty<double>(), Array.Empty<double>()));
    }

    /// <summary>
    /// The basic functions of a function graph: y = a · f(b · (x − c)) + d with a fixed basic function f (the math module's
    /// family, moved here), a typed formula (<see cref="FunctionExpression"/>) or a function generator (<see cref="CurveFunctions"/>).
    /// </summary>
    internal static class MathFunctions
    {
        public static readonly (string Value, string En, string De)[] Kinds =
        {
            ("linear", "Linear", "Linear"), ("quadratic", "Quadratic (parabola)", "Quadratisch (Parabel)"), ("cubic", "Cubic", "Kubisch"),
            ("sine", "Sine", "Sinus"), ("cosine", "Cosine", "Kosinus"), ("exponential", "Exponential (eˣ)", "Exponentiell (eˣ)"),
            ("logarithm", "Natural logarithm (ln x)", "Natürlicher Logarithmus (ln x)"), ("sqrt", "Square root", "Quadratwurzel"),
            ("abs", "Absolute value", "Betrag"), ("reciprocal", "Reciprocal (hyperbola)", "Kehrwert (Hyperbel)"),
            ("expression", "Formula (typed)", "Formel (eingegeben)"), ("generator", "Function generator (ramp, PT1, pulse …)", "Funktionsgenerator (Rampe, PT1, Impuls …)"),
        };

        public static double Base(string kind, double u) => kind switch
        {
            "linear" => u,
            "quadratic" => u * u,
            "cubic" => u * u * u,
            "sine" => Math.Sin(u),
            "cosine" => Math.Cos(u),
            "exponential" => u > 700 ? double.NaN : Math.Exp(u),
            "logarithm" => u > 0 ? Math.Log(u) : double.NaN,
            "sqrt" => u >= 0 ? Math.Sqrt(u) : double.NaN,
            "abs" => Math.Abs(u),
            "reciprocal" => Math.Abs(u) < 1e-300 ? double.NaN : 1 / u,
            _ => u * u,
        };

        private static string Superscript(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                var m = ch switch
                {
                    '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴', '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', '9' => '⁹',
                    '+' => '⁺', '-' or '−' => '⁻', 'x' => 'ˣ', '(' => '⁽', ')' => '⁾', ' ' => '\0', _ => '?',
                };
                if (m == '?') return "";
                if (m != '\0') sb.Append(m);
            }
            return sb.ToString();
        }

        /// <summary>"−2,5" style numbers (unicode minus, decimal comma in German).</summary>
        private static string N(double v, Lang lang)
        {
            var s = Fmt.Num(v, lang, 3);
            return s.StartsWith("-", StringComparison.Ordinal) ? "−" + s.Substring(1) : s;
        }

        /// <summary>The formula "f(x) = …" of the family in school notation, simplified for unit coefficients.</summary>
        public static string Formula(string kind, double a, double b, double c, double d, Lang lang)
        {
            bool Is(double v, double w) => Math.Abs(v - w) < 1e-12;
            string Coef(double v) => Is(v, 1) ? "" : Is(v, -1) ? "−" : N(v, lang);
            string CoefDot(double v) => Is(v, 1) ? "" : Is(v, -1) ? "−" : N(v, lang) + "·";
            string Plus(double v) => Is(v, 0) ? "" : v > 0 ? " + " + N(v, lang) : " − " + N(-v, lang);
            var xc = Is(c, 0) ? "x" : c > 0 ? "x − " + N(c, lang) : "x + " + N(-c, lang);
            var xcP = Is(c, 0) ? "x" : "(" + xc + ")";
            var u = Is(b, 1) ? xc : Is(b, -1) ? "−" + xcP : N(b, lang) + xcP;
            var uP = u == "x" ? "x" : "(" + u + ")";
            string body;
            if (Is(a, 0) || Is(b, 0))
            {
                var v = a * Base(kind, b * 1) + d;
                body = Fmt.Finite(v) ? N(v, lang) : "–";
            }
            else
                switch (kind)
                {
                    case "linear": body = Coef(a * b) + "x" + Plus(d - a * b * c); break;
                    case "quadratic":
                    case "cubic":
                        {
                            var p = kind == "quadratic" ? 2 : 3;
                            body = Coef(a * Math.Pow(b, p)) + xcP + (p == 2 ? "²" : "³") + Plus(d);
                            break;
                        }
                    case "sine": body = CoefDot(a) + "sin(" + u + ")" + Plus(d); break;
                    case "cosine": body = CoefDot(a) + "cos(" + u + ")" + Plus(d); break;
                    case "exponential":
                        {
                            var sup = Superscript(u);
                            body = CoefDot(a) + (sup.Length > 0 ? "e" + sup : "exp(" + u + ")") + Plus(d);
                            break;
                        }
                    case "logarithm": body = CoefDot(a) + "ln(" + u + ")" + Plus(d); break;
                    case "sqrt": body = Coef(a) + "√" + uP + Plus(d); break;
                    case "abs": body = Coef(a) + "|" + u + "|" + Plus(d); break;
                    case "reciprocal": body = (a < 0 ? "−" : "") + N(Math.Abs(a), lang) + "/" + uP + Plus(d); break;
                    default: body = "0"; break;
                }
            return "f(x) = " + body;
        }

        /// <summary>x in [xa, xb] where the function crosses <paramref name="level"/> (bisection).</summary>
        public static double Cross(Func<double, double> f, double xa, double xb, double level)
        {
            var fa = f(xa) - level;
            for (var i = 0; i < 60; i++)
            {
                var xm = (xa + xb) / 2;
                var fm = f(xm) - level;
                if (!Fmt.Finite(fm)) { xb = xm; continue; }
                if (!Fmt.Finite(fa) || fm > 0 == fa > 0) { xa = xm; fa = fm; }
                else xb = xm;
            }
            return (xa + xb) / 2;
        }

        /// <summary>Continuous x intervals inside [x0, x1], split at <paramref name="cuts"/> (poles, domain edges), open ends nudged.</summary>
        public static IEnumerable<(double A, double B)> Intervals(IReadOnlyList<double> cuts, double x0, double x1)
        {
            var bounds = new List<double> { x0 };
            bounds.AddRange(cuts.Where(c => c > x0 && c < x1).OrderBy(c => c));
            bounds.Add(x1);
            var eps = (x1 - x0) * 1e-7;
            for (var i = 0; i + 1 < bounds.Count; i++)
            {
                double a = bounds[i], b = bounds[i + 1];
                if (cuts.Any(c => Math.Abs(c - a) < 1e-12)) a += eps;
                if (cuts.Any(c => Math.Abs(c - b) < 1e-12)) b -= eps;
                if (b > a) yield return (a, b);
            }
        }

        /// <summary>
        /// The curve y = f(x) over [x0, x1] clipped to [y0, y1], as polylines in data coordinates: cut at the given points,
        /// at non-finite values and where the curve leaves the y range (crossing points found by bisection); a jump across the
        /// whole range between two samples is drawn only where the function is continuous there (no line through a pole).
        /// </summary>
        public static List<List<Vec2>> Curve(Func<double, double> f, IReadOnlyList<double> cuts, double x0, double x1, double y0, double y1, int samples = 800)
        {
            var runs = new List<List<Vec2>>();
            var span = x1 - x0;
            bool In(double y) => y >= y0 - 1e-9 && y <= y1 + 1e-9;
            foreach (var (a, b) in Intervals(cuts, x0, x1))
            {
                List<Vec2>? cur = null;
                void Close()
                {
                    if (cur is { Count: >= 2 }) runs.Add(cur);
                    cur = null;
                }
                var n = Math.Max(8, (int)Math.Ceiling(samples * (b - a) / span));
                var havePrev = false;
                double px = 0, py = 0;
                for (var i = 0; i <= n; i++)
                {
                    var x = a + (b - a) * i / n;
                    var y = f(x);
                    if (!Fmt.Finite(y)) { Close(); havePrev = false; continue; }
                    if (!havePrev)
                    {
                        if (In(y)) cur = new List<Vec2> { new(x, y) };
                    }
                    else
                    {
                        bool pin = In(py), cin = In(y);
                        if (pin && cin)
                        {
                            cur ??= new List<Vec2> { new(px, py) };
                            cur.Add(new Vec2(x, y));
                        }
                        else if (pin)
                        {
                            var lvl = y > y1 ? y1 : y0;
                            var xb = Cross(f, px, x, lvl);
                            (cur ??= new List<Vec2> { new(px, py) }).Add(new Vec2(xb, lvl));
                            Close();
                        }
                        else if (cin)
                        {
                            var lvl = py > y1 ? y1 : y0;
                            cur = new List<Vec2> { new(Cross(f, px, x, lvl), lvl), new(x, y) };
                        }
                        else if (py > y1 != y > y1)
                        {
                            // Steep passage through the whole range between two samples – or a pole.
                            double l1 = py > y1 ? y1 : y0, l2 = py > y1 ? y0 : y1;
                            var xa = Cross(f, px, x, l1);
                            var xb = Cross(f, xa, x, l2);
                            var mid = f((xa + xb) / 2);
                            if (Fmt.Finite(mid) && mid >= y0 - 1e-9 && mid <= y1 + 1e-9) runs.Add(new List<Vec2> { new(xa, l1), new(xb, l2) });
                        }
                    }
                    havePrev = true; px = x; py = y;
                }
                Close();
            }
            return runs;
        }

        /// <summary>Zeros, extrema and the y-intercept inside the ranges (data coordinates).</summary>
        public static (List<double> Zeros, List<Vec2> Extrema, double? YIntercept) KeyPoints(Func<double, double> f, IReadOnlyList<double> cuts, double x0, double x1, double y0, double y1)
        {
            var zeros = new List<double>();
            var extrema = new List<Vec2>();
            bool InY(double y) => y >= y0 - 1e-9 && y <= y1 + 1e-9;
            void AddZero(double x)
            {
                if (zeros.Count < 24 && x >= x0 - 1e-9 && x <= x1 + 1e-9 && zeros.All(z => Math.Abs(z - x) > (x1 - x0) * 1e-6)) zeros.Add(x);
            }
            foreach (var (a, b) in Intervals(cuts, x0, x1))
            {
                const int n = 800;
                var xs = Enumerable.Range(0, n + 1).Select(i => a + (b - a) * i / n).ToArray();
                var ys = xs.Select(f).ToArray();
                for (var i = 1; i <= n; i++)
                {
                    if (!Fmt.Finite(ys[i]) || !Fmt.Finite(ys[i - 1])) continue;
                    if (ys[i - 1] == 0) AddZero(xs[i - 1]);
                    else if (ys[i - 1] * ys[i] < 0)
                    {
                        // A sign change through a pole (1/x) is no zero.
                        var z = Cross(f, xs[i - 1], xs[i], 0);
                        if (Math.Abs(f(z)) < 1e-6 * Math.Max(1, Math.Max(Math.Abs(ys[i - 1]), Math.Abs(ys[i])))) AddZero(z);
                    }
                    if (i == n && ys[i] == 0) AddZero(xs[i]);
                }
                for (var i = 1; i < n; i++)
                {
                    if (!Fmt.Finite(ys[i - 1]) || !Fmt.Finite(ys[i]) || !Fmt.Finite(ys[i + 1])) continue;
                    double d0 = ys[i] - ys[i - 1], d1 = ys[i + 1] - ys[i];
                    if (d0 * d1 >= 0 || extrema.Count >= 24) continue;
                    var sign = d0 > 0 ? -1.0 : 1.0;
                    double lo = xs[i - 1], hi = xs[i + 1];
                    for (var k = 0; k < 80; k++)
                    {
                        double m1 = lo + (hi - lo) * 0.382, m2 = lo + (hi - lo) * 0.618;
                        if (sign * f(m1) < sign * f(m2)) hi = m2; else lo = m1;
                    }
                    var xe = (lo + hi) / 2;
                    var ye = f(xe);
                    if (Fmt.Finite(ye) && InY(ye) && extrema.All(e => Math.Abs(e.X - xe) > (x1 - x0) * 1e-6)) extrema.Add(new Vec2(xe, ye));
                    if (Fmt.Finite(ye) && Math.Abs(ye) < 1e-9) AddZero(xe);
                }
            }
            zeros.Sort();
            double? yint = null;
            if (x0 <= 0 && x1 >= 0)
            {
                var v = f(0);
                if (Fmt.Finite(v) && InY(v)) yint = v;
            }
            return (InY(0) ? zeros : new List<double>(), extrema, yint);
        }

        /// <summary>Douglas–Peucker simplification of a polyline (page coordinates, tolerance in mm).</summary>
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
    }

    /// <summary>
    /// Function graph in math style (charts v2 stage 4; replaces the library items of the math module's function graph):
    /// the coordinate system of <see cref="MathAxesChart"/> with the curve of y = a·f(b(x − c)) + d (ten basic functions),
    /// of a typed formula or of a function generator, its formula in the free corner of the plot, and ports at the zeros,
    /// extrema and the y-intercept.
    /// </summary>
    internal sealed class FunctionChart : ChartType
    {
        public const string TypeId = "chart.math.function";
        public const string DefaultExpression = "x^3/4 - x";
        public const string DefaultGenerator = "pt1(y0=0, yinf=3, tau=1.5)";

        public static readonly ParamDef FunctionDef = Prm.Choice("function", "Function", "Funktion", "quadratic", MathFunctions.Kinds);
        public static readonly ParamDef ExprDef = Prm.Text("expr", "Formula f(x) (for “Formula”)", "Formel f(x) (für „Formel“)", DefaultExpression);
        public static readonly ParamDef GeneratorDef = Prm.Text("generator", "Function generator (for “Function generator”)", "Funktionsgenerator (für „Funktionsgenerator“)", DefaultGenerator, advanced: true);

        private static readonly I18nText Unreadable = new("Formula cannot be read", "Formel nicht lesbar");

        public FunctionChart() : base(TypeId, new I18nText("Function graph", "Funktionsgraph"), ChartCategories.Math,
            "function", "graph", "plot", "curve", "formula", "parabola", "sine", "linear", "exponential", "math",
            "funktion", "funktionsgraph", "schaubild", "kurve", "formel", "parabel", "sinus", "mathematik")
        { }

        public override Size2 Design => new(70, 60);
        public override IReadOnlyList<string> Companions => new[] { "math.point.labelled", "math.vector.arrow" };

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            FunctionDef,
            Prm.Num("a", "a (vertical stretch)", "a (Streckung in y-Richtung)", 1, -100, 100, step: 0.5),
            Prm.Num("b", "b (horizontal factor)", "b (Faktor in x-Richtung)", 1, -100, 100, step: 0.5),
            Prm.Num("c", "c (shift right)", "c (Verschiebung nach rechts)", 0, -100, 100, step: 0.5),
            Prm.Num("d", "d (shift up)", "d (Verschiebung nach oben)", 0, -100, 100, step: 0.5),
            ExprDef, GeneratorDef,
            Prm.Bool("formula", "Show formula", "Funktionsgleichung anzeigen", true),
        }.Concat(MathAxes.Params(-4, 4, -2, 8)).ToList();

        /// <summary>The function of an object: f, its cut points, the formula text and whether it is a closed generator curve.</summary>
        internal sealed record Source(Func<double, double> F, IReadOnlyList<double> Cuts, string Formula, CurveSpec? Generator, bool Valid);

        internal static Source SourceOf(SketchObject o, Lang lang)
        {
            var kind = o.Str("function", "quadratic");
            switch (kind)
            {
                case "expression":
                    return FunctionExpression.TryParse(o.Str("expr", DefaultExpression), out var e, out _) && e is not null
                        ? new Source(e.Evaluate, Array.Empty<double>(), "f(x) = " + e.Pretty(), null, true)
                        : new Source(_ => double.NaN, Array.Empty<double>(), "f(x) = ?", null, false);
                case "generator":
                    return CurveFunctions.TryParse(o.Str("generator", DefaultGenerator), out var g, out _) && g is not null
                        ? new Source(g.Evaluate, Array.Empty<double>(), "f(x) = " + g.Format(), g, true)
                        : new Source(_ => double.NaN, Array.Empty<double>(), "?", null, false);
                default:
                    {
                        double a = o.Num("a", 1), b = o.Num("b", 1), c = o.Num("c", 0), d = o.Num("d", 0);
                        var cuts = kind is "logarithm" or "sqrt" or "reciprocal" && Math.Abs(b) > 1e-12 ? new[] { c } : Array.Empty<double>();
                        return new Source(x => a * MathFunctions.Base(kind, b * (x - c)) + d, cuts, MathFunctions.Formula(kind, a, b, c, d, lang), null, true);
                    }
            }
        }

        /// <summary>The curve in data coordinates (clipped to the ranges; generators as their exact polyline).</summary>
        internal static List<List<Vec2>> DataRuns(SketchObject o, ObjectType t, Lang lang)
        {
            var (x0, x1, y0, y1) = MathAxes.Ranges(o, t);
            var src = SourceOf(o, lang);
            if (!src.Valid) return new List<List<Vec2>>();
            if (src.Generator is { } g)
            {
                // Exact vertices (jumps, corners); clipped to the plot when drawn.
                var pts = g.Sample(x0, x1).ToList();
                return pts.Count >= 2 ? new List<List<Vec2>> { pts } : new List<List<Vec2>>();
            }
            return MathFunctions.Curve(src.F, src.Cuts, x0, x1, y0, y1);
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var (f, panel) = MathAxes.Build(o, this, Design, ctx);
            var src = SourceOf(o, lang);
            var style = new Style { Stroke = "#1565c0", StrokeWidth = 0.5, Role = LineRole.Visible };
            // The formula names the curve part ("series.0"); no legend.
            f.Legend.Add(new LegendEntry(src.Formula, style));
            f.Layout(lang);
            var c = new Chart(f, lang);
            var tag = ChartTags.Nth(ChartTags.Series, 0);
            var segs = new List<PathSeg>();
            var samples = new List<Vec2>();
            foreach (var run in DataRuns(o, this, lang))
                foreach (var vis in f.VisibleRuns(panel, run))
                {
                    var page = MathFunctions.Simplify(vis.Select(p => f.P(panel, p.X, p.Y)).Where(Frame.Ok).ToList(), 0.01);
                    if (page.Count < 2) continue;
                    if (Draw.Series(page, panel.Area, style, false, tag) is { } path) segs.AddRange(path.Segments);
                    samples.AddRange(Draw.Densify(new[] { page }, 0.5));
                }
            if (segs.Count > 0) c.Lines.Add(new PathPrim(segs, style) { Tag = tag });
            f.NumberAvoid.AddRange(samples.Where(panel.Area.Inflate(0.01).Contains));
            c.Avoid.Add(samples);

            if (!src.Valid)
            {
                var text = Unreadable.Get(lang);
                var a = panel.Area;
                var fs = Math.Max(0.3, Math.Min(f.Fs * 0.9, a.W * 0.9 / (text.Length * Fmt.CharWidth)));
                c.Over.Add(new TextPrim(new Vec2(a.X + a.W / 2, a.Y + a.H / 4), text, new TextStyle { Size = fs, Color = Palette.Helper, Italic = true }) { Tag = ChartTags.Error });
            }
            else if (o.Flag("formula", true))
            {
                var text = src.Formula;
                var area = panel.Area;
                var size = Math.Max(0.3, Math.Min(f.Fs * 1.1, Math.Max(1, area.W - 1) / (Math.Max(1, text.Length) * Fmt.CharWidth)));
                var (at, anchor) = FormulaPlace(f, panel, samples, text, size);
                c.Over.Add(new TextPrim(at, text, new TextStyle { Size = size, Anchor = anchor, Color = style.Stroke! }) { Tag = ChartTags.Formula });
            }

            if (src.Valid && !(src.Generator?.Info.Closed ?? false))
            {
                var (x0, x1, y0, y1) = MathAxes.Ranges(o, this);
                var (zeros, extrema, yint) = MathFunctions.KeyPoints(src.F, src.Cuts, x0, x1, y0, y1);
                for (var i = 0; i < zeros.Count; i++)
                    if (f.IsVisible(panel, zeros[i], 0)) c.Port(f.P(panel, zeros[i], 0), "zero" + Fmt.I(i));
                for (var i = 0; i < extrema.Count; i++)
                    if (f.IsVisible(panel, extrema[i].X, extrema[i].Y)) c.Port(f.P(panel, extrema[i].X, extrema[i].Y), "ext" + Fmt.I(i));
                if (yint is double yi && f.IsVisible(panel, 0, yi)) c.Port(f.P(panel, 0, yi), "yint");
            }
            return c;
        }

        /// <summary>
        /// The corner of the plot where the formula covers the least: no curve, no axis line, no tick numbers (top left,
        /// top right, bottom left, bottom right in this order of preference).
        /// </summary>
        private static (Vec2 At, TextAnchor Anchor) FormulaPlace(Frame f, Panel p, IReadOnlyList<Vec2> samples, string text, double size)
        {
            var area = p.Area;
            var w = Fmt.Width(text, size);
            double ax = f.AxisX(p), ay = f.AxisY(p);
            var best = (Score: double.MaxValue, At: Vec2.Zero, Anchor: TextAnchor.Start);
            var candidates = new[] { (Right: false, Top: true), (Right: true, Top: true), (Right: false, Top: false), (Right: true, Top: false) };
            for (var k = 0; k < candidates.Length; k++)
            {
                var (right, top) = candidates[k];
                var x0 = right ? area.Right - 0.5 - w : area.X + 0.5;
                var y0 = top ? area.Y + 0.3 : area.Bottom - 0.3 - size;
                var rect = new Rect(x0, y0, w, size).Inflate(0.6);
                var score = k * 0.1 + samples.Count(rect.Contains) * 10;
                if (ax > rect.X && ax < rect.Right) score += 6;
                if (ay > rect.Y && ay < rect.Bottom) score += 6;
                if (f.Numbers && rect.Y < ay + f.Fs * 1.6 && rect.Bottom > ay) score += 4;
                if (f.Numbers && rect.X < ax && rect.Right > ax - f.Fs * 3) score += 4;
                if (score < best.Score) best = (score, new Vec2(right ? area.Right - 0.5 : area.X + 0.5, y0 + size / 2), right ? TextAnchor.End : TextAnchor.Start);
            }
            return (best.At, best.Anchor);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var option in Prm.Options(o, "chart.function", FunctionDef, new I18nText("Function", "Funktion"))) yield return option;
            var label = new I18nText("Formula f(x)…", "Formel f(x)…");
            yield return ContextAction.WithInput("chart.function.formula", label, IconIds.Text,
                new I18nText("f(x) = … (e.g. x^2 - 2, 2 sin(x), e^(-x/2), |x - 1|)", "f(x) = … (z. B. x^2 - 2, 2 sin(x), e^(-x/2), |x - 1|)"),
                o.Str("function", "quadratic") is "expression" or "generator" ? o.Str("expr", DefaultExpression)
                    : ExpressionOf(o.Str("function", "quadratic"), o.Num("a", 1), o.Num("b", 1), o.Num("c", 0), o.Num("d", 0)),
                (x, text) =>
                {
                    var e = FunctionExpression.Parse(text);
                    return x.WithParam("expr", e.Text).WithParam("function", "expression");
                }) with { Group = "function", ParamKeys = new[] { "expr", "function" } };
            var fnKind = o.Str("function", "quadratic");
            if (fnKind is not ("expression" or "generator"))
            {
                var coef = new I18nText("Coefficients a; b; c; d…", "Koeffizienten a; b; c; d…");
                yield return ContextAction.WithInput("chart.function.coefficients", coef, IconIds.Text, coef,
                    string.Join("; ", new[] { o.Num("a", 1), o.Num("b", 1), o.Num("c", 0), o.Num("d", 0) }.Select(v => Fmt.Num(v, Lang.En, 4))),
                    (x, t) =>
                    {
                        var parts = (t ?? "").Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                        if (parts.Count is < 1 or > 4) throw new FormatException("Expected up to four numbers separated by ';' / Erwartet: bis zu vier Zahlen, getrennt durch ';'");
                        var keys = new[] { "a", "b", "c", "d" };
                        for (var i = 0; i < parts.Count; i++)
                        {
                            if (!ChartData.TryNumber(parts[i], out var v) || !Fmt.Finite(v)) throw new FormatException("Not a number / Keine Zahl: “" + parts[i] + "”");
                            x = x.WithParam(keys[i], Geo.Clamp(v, -100, 100));
                        }
                        return x;
                    }) with { Group = "function", ParamKeys = new[] { "a", "b", "c", "d" } };
            }
            if (fnKind == "generator")
            {
                var gen = new I18nText("Function generator…", "Funktionsgenerator…");
                yield return ContextAction.WithInput("chart.function.generator", gen, IconIds.Text,
                    new I18nText("e.g. pt1(y0=0, yinf=3, tau=1.5), step(x0=1), sine(amplitude=2, period=4)", "z. B. pt1(y0=0, yinf=3, tau=1.5), step(x0=1), sine(amplitude=2, period=4)"),
                    o.Str("generator", DefaultGenerator),
                    (x, t) => x.WithParam("generator", CurveFunctions.Parse(t).Format())) with { Group = "function", ParamKeys = new[] { "generator" } };
            }
            yield return Prm.Toggle(o, "chart.function.showformula", "formula", true, new I18nText("Show formula", "Funktionsgleichung anzeigen"));
            foreach (var a in MathAxes.Actions(o, this)) yield return a;
            var runs = DataRuns(o, this, Lang.En);
            foreach (var a in MathAxes.BreakActions(o, this, Array.Empty<double>(), runs.SelectMany(r => r.Select(p => p.Y)))) yield return a;
        }

        /// <summary>The function a·f(b(x − c)) + d of the family as a formula the evaluator reads ("2*(x - 1)^2 + 3").</summary>
        internal static string ExpressionOf(string kind, double a, double b, double c, double d)
        {
            string N(double v) => Fmt.Num(v, Lang.En, 6);
            var xc = c == 0 ? "x" : c > 0 ? "x - " + N(c) : "x + " + N(-c);
            var u = b == 1 ? xc : b == -1 ? "-(" + xc + ")" : N(b) + "*(" + xc + ")";
            var single = u == "x";
            var body = kind switch
            {
                "linear" => single ? "x" : "(" + u + ")",
                "cubic" => (single ? "x" : "(" + u + ")") + "^3",
                "sine" => "sin(" + u + ")",
                "cosine" => "cos(" + u + ")",
                "exponential" => "exp(" + u + ")",
                "logarithm" => "ln(" + u + ")",
                "sqrt" => "sqrt(" + u + ")",
                "abs" => "abs(" + u + ")",
                "reciprocal" => "1/" + (single ? "x" : "(" + u + ")"),
                _ => (single ? "x" : "(" + u + ")") + "^2",
            };
            var head = a == 1 ? body : a == -1 ? "-" + body : N(a) + "*" + body;
            return d == 0 ? head : d > 0 ? head + " + " + N(d) : head + " - " + N(-d);
        }
    }
}
