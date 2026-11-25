using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// A function of the family y = a · f(b · (x − c)) + d with a basic function f (no expression parser: the basic
    /// functions are fixed, the four coefficients stretch and shift them, which covers the usual school examples).
    /// </summary>
    internal sealed record FnSpec(string Kind, double A, double B, double C, double D)
    {
        public double At(double x) => A * Fn.Base(Kind, B * (x - C)) + D;
    }

    internal static class Fn
    {
        public static readonly (string Value, string En, string De)[] Kinds =
        {
            ("linear", "Linear", "Linear"), ("quadratic", "Quadratic (parabola)", "Quadratisch (Parabel)"), ("cubic", "Cubic", "Kubisch"),
            ("sine", "Sine", "Sinus"), ("cosine", "Cosine", "Kosinus"), ("exponential", "Exponential (eˣ)", "Exponentiell (eˣ)"),
            ("logarithm", "Natural logarithm (ln x)", "Natürlicher Logarithmus (ln x)"), ("sqrt", "Square root", "Quadratwurzel"),
            ("abs", "Absolute value", "Betrag"), ("reciprocal", "Reciprocal (hyperbola)", "Kehrwert (Hyperbel)"),
        };

        public static double Base(string kind, double u) => kind switch
        {
            "linear" => u,
            "quadratic" => u * u,
            "cubic" => u * u * u,
            "sine" => Math.Sin(u),
            "cosine" => Math.Cos(u),
            "exponential" => u > 700 ? double.PositiveInfinity : Math.Exp(u),
            "logarithm" => u > 0 ? Math.Log(u) : double.NaN,
            "sqrt" => u >= 0 ? Math.Sqrt(u) : double.NaN,
            "abs" => Math.Abs(u),
            "reciprocal" => Math.Abs(u) < 1e-300 ? double.NaN : 1 / u,
            _ => u * u,
        };

        /// <summary>Points where the curve is cut into separate pieces: the pole of 1/x and the domain edge of ln and √.</summary>
        public static IEnumerable<double> Cuts(FnSpec f)
        {
            if (f.Kind is "logarithm" or "sqrt" or "reciprocal" && Math.Abs(f.B) > 1e-12) yield return f.C;
        }

        // ------------------------------------------------------------------ formula text

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

        /// <summary>The formula "f(x) = …" in school notation (decimal comma in German), simplified for unit coefficients.</summary>
        public static string Formula(FnSpec f, Lang lang, string name = "f")
        {
            string N(double v) => Mx.Num(v, lang, 3);
            bool Is(double v, double w) => Math.Abs(v - w) < 1e-12;
            string Coef(double v) => Is(v, 1) ? "" : Is(v, -1) ? "−" : N(v);
            string CoefDot(double v) => Is(v, 1) ? "" : Is(v, -1) ? "−" : N(v) + "·";
            string Plus(double v) => Is(v, 0) ? "" : v > 0 ? " + " + N(v) : " − " + N(-v);
            double a = f.A, b = f.B, c = f.C, d = f.D;
            var xc = Is(c, 0) ? "x" : c > 0 ? "x − " + N(c) : "x + " + N(-c);
            var xcP = Is(c, 0) ? "x" : "(" + xc + ")";
            var u = Is(b, 1) ? xc : Is(b, -1) ? "−" + xcP : N(b) + xcP;
            var uP = u == "x" ? "x" : "(" + u + ")";
            string body;
            if (Is(a, 0) || Is(b, 0))
            {
                // Constant function (or undefined everywhere, e.g. ln(0·x)).
                var v = f.At(c + 1);
                body = Mx.Finite(v) ? N(v) : "–";
            }
            else
                switch (f.Kind)
                {
                    case "linear":
                        {
                            double m = a * b, n = d - a * b * c;
                            body = Coef(m) + "x" + Plus(n);
                            break;
                        }
                    case "quadratic":
                    case "cubic":
                        {
                            var p = f.Kind == "quadratic" ? 2 : 3;
                            var k = a * Math.Pow(b, p);
                            body = Coef(k) + xcP + (p == 2 ? "²" : "³") + Plus(d);
                            break;
                        }
                    case "sine": body = CoefDot(a) + "sin" + "(" + u + ")" + Plus(d); break;
                    case "cosine": body = CoefDot(a) + "cos" + "(" + u + ")" + Plus(d); break;
                    case "exponential":
                        {
                            var sup = Superscript(u);
                            body = CoefDot(a) + (sup.Length > 0 ? "e" + sup : "exp(" + u + ")") + Plus(d);
                            break;
                        }
                    case "logarithm": body = CoefDot(a) + "ln(" + u + ")" + Plus(d); break;
                    case "sqrt": body = Coef(a) + "√" + uP + Plus(d); break;
                    case "abs": body = Coef(a) + "|" + u + "|" + Plus(d); break;
                    case "reciprocal": body = (a < 0 ? "−" : "") + N(Math.Abs(a)) + "/" + uP + Plus(d); break;
                    default: body = N(0); break;
                }
            return name + "(x) = " + body;
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>x in [xa, xb] where the continuous function crosses <paramref name="level"/> (bisection).</summary>
        public static double Cross(Func<double, double> f, double xa, double xb, double level)
        {
            var fa = f(xa) - level;
            for (var i = 0; i < 60; i++)
            {
                var xm = (xa + xb) / 2;
                var fm = f(xm) - level;
                if (!Mx.Finite(fm)) { xb = xm; continue; }
                if (Math.Sign(fm) == Math.Sign(fa)) { xa = xm; fa = fm; }
                else xb = xm;
            }
            return (xa + xb) / 2;
        }

        /// <summary>Continuous x intervals of the function inside [x0, x1] (split at <see cref="Cuts"/>), with open ends nudged.</summary>
        public static IEnumerable<(double A, double B)> Intervals(FnSpec f, double x0, double x1)
        {
            var cuts = Cuts(f).Where(c => c > x0 && c < x1).ToList();
            var bounds = new List<double> { x0 };
            bounds.AddRange(cuts);
            bounds.Add(x1);
            var eps = (x1 - x0) * 1e-7;
            var all = Cuts(f).ToList();
            for (var i = 0; i + 1 < bounds.Count; i++)
            {
                double a = bounds[i], b = bounds[i + 1];
                if (all.Any(c => Math.Abs(c - a) < 1e-12)) a += eps;
                if (all.Any(c => Math.Abs(c - b) < 1e-12)) b -= eps;
                if (b > a) yield return (a, b);
            }
        }

        /// <summary>The curve clipped to the y range of the plot, as polylines in local coordinates.</summary>
        public static List<List<Vec2>> Curve(FnSpec fn, Plot2D p)
        {
            var s = p.S;
            var runs = new List<List<Vec2>>();
            var span = s.X1 - s.X0;
            Func<double, double> f = fn.At;
            bool In(double y) => y >= s.Y0 - 1e-9 && y <= s.Y1 + 1e-9;
            foreach (var (a, b) in Intervals(fn, s.X0, s.X1))
            {
                List<Vec2>? cur = null;
                void Close()
                {
                    if (cur is { Count: >= 2 }) runs.Add(cur);
                    cur = null;
                }
                var n = Math.Max(8, (int)Math.Ceiling(800 * (b - a) / span));
                var havePrev = false;
                double px = 0, py = 0;
                for (var i = 0; i <= n; i++)
                {
                    var x = a + (b - a) * i / n;
                    var y = f(x);
                    if (!Mx.Finite(y)) { Close(); havePrev = false; continue; }
                    if (!havePrev)
                    {
                        if (In(y)) cur = new List<Vec2> { p.Map(x, y) };
                    }
                    else
                    {
                        bool pin = In(py), cin = In(y);
                        if (pin && cin)
                        {
                            cur ??= new List<Vec2> { p.Map(px, py) };
                            cur.Add(p.Map(x, y));
                        }
                        else if (pin)
                        {
                            var lvl = y > s.Y1 ? s.Y1 : s.Y0;
                            var xb = Cross(f, px, x, lvl);
                            (cur ??= new List<Vec2> { p.Map(px, py) }).Add(p.Map(xb, lvl));
                            Close();
                        }
                        else if (cin)
                        {
                            var lvl = py > s.Y1 ? s.Y1 : s.Y0;
                            var xb = Cross(f, px, x, lvl);
                            cur = new List<Vec2> { p.Map(xb, lvl), p.Map(x, y) };
                        }
                        else if (py > s.Y1 != y > s.Y1)
                        {
                            // Steep passage through the whole range between two samples.
                            var l1 = py > s.Y1 ? s.Y1 : s.Y0;
                            var l2 = py > s.Y1 ? s.Y0 : s.Y1;
                            var xa = Cross(f, px, x, l1);
                            var xb = Cross(f, xa, x, l2);
                            runs.Add(new List<Vec2> { p.Map(xa, l1), p.Map(xb, l2) });
                        }
                    }
                    havePrev = true; px = x; py = y;
                }
                Close();
            }
            return runs.Select(r => Mx.Simplify(r, 0.01)).Where(r => r.Count >= 2).ToList();
        }

        /// <summary>Zeros, extrema and the y-intercept inside the plot range (data coordinates).</summary>
        public static (List<double> Zeros, List<Vec2> Extrema, double? YIntercept) KeyPoints(FnSpec fn, AxesSpec s)
        {
            var zeros = new List<double>();
            var extrema = new List<Vec2>();
            Func<double, double> f = fn.At;
            bool InY(double y) => y >= s.Y0 - 1e-9 && y <= s.Y1 + 1e-9;
            void AddZero(double x)
            {
                if (zeros.Count < 24 && x >= s.X0 - 1e-9 && x <= s.X1 + 1e-9 && zeros.All(z => Math.Abs(z - x) > (s.X1 - s.X0) * 1e-6)) zeros.Add(x);
            }
            foreach (var (a, b) in Intervals(fn, s.X0, s.X1))
            {
                const int n = 800;
                var xs = Enumerable.Range(0, n + 1).Select(i => a + (b - a) * i / n).ToArray();
                var ys = xs.Select(f).ToArray();
                for (var i = 1; i <= n; i++)
                {
                    if (!Mx.Finite(ys[i]) || !Mx.Finite(ys[i - 1])) continue;
                    if (ys[i - 1] == 0) AddZero(xs[i - 1]);
                    else if (ys[i - 1] * ys[i] < 0) AddZero(Cross(f, xs[i - 1], xs[i], 0));
                    if (i == n && ys[i] == 0) AddZero(xs[i]);
                }
                for (var i = 1; i < n; i++)
                {
                    if (!Mx.Finite(ys[i - 1]) || !Mx.Finite(ys[i]) || !Mx.Finite(ys[i + 1])) continue;
                    double d0 = ys[i] - ys[i - 1], d1 = ys[i + 1] - ys[i];
                    if (d0 * d1 >= 0 || extrema.Count >= 24) continue;
                    // Golden-section refinement for a maximum (d0 > 0) or a minimum.
                    var sign = d0 > 0 ? -1.0 : 1.0;
                    double lo = xs[i - 1], hi = xs[i + 1];
                    for (var k = 0; k < 80; k++)
                    {
                        double m1 = lo + (hi - lo) * 0.382, m2 = lo + (hi - lo) * 0.618;
                        if (sign * f(m1) < sign * f(m2)) hi = m2; else lo = m1;
                    }
                    var xe = (lo + hi) / 2;
                    var ye = f(xe);
                    if (Mx.Finite(ye) && InY(ye) && extrema.All(e => Math.Abs(e.X - xe) > (s.X1 - s.X0) * 1e-6)) extrema.Add(new Vec2(xe, ye));
                    if (Mx.Finite(ye) && Math.Abs(ye) < 1e-9) AddZero(xe);
                }
            }
            zeros.Sort();
            double? yint = null;
            if (s.X0 <= 0 && s.X1 >= 0)
            {
                var y0 = f(0);
                if (Mx.Finite(y0) && InY(y0)) yint = y0;
            }
            return (zeros.Where(z => InY(0)).ToList(), extrema, yint);
        }
    }

    /// <summary>Function graph: axes plus the curve of y = a·f(b(x − c)) + d with the formula.</summary>
    internal sealed class FunctionGraphType : MathBoxType
    {
        public FunctionGraphType() : base("math.graph.function", new I18nText("Function graph", "Funktionsgraph"), MathCategories.Graphs,
            "function", "graph", "plot", "curve", "funktion", "graph", "schaubild", "kurve", "parabel", "parabola", "sinus", "sine", "linear", "exponential") { }

        public override Size2 DefaultSize => new(70, 60);
        public override IReadOnlyList<string> Companions => new[] { "math.point.labelled", "math.vector.arrow" };

        public static readonly ParamDef Function = Pm.Choice("function", "Function", "Funktion", "quadratic", Fn.Kinds);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Function,
            Pm.Num("a", "a (vertical stretch)", "a (Streckung in y-Richtung)", 1, -100, 100, step: 0.5),
            Pm.Num("b", "b (horizontal factor)", "b (Faktor in x-Richtung)", 1, -100, 100, step: 0.5),
            Pm.Num("c", "c (shift right)", "c (Verschiebung nach rechts)", 0, -100, 100, step: 0.5),
            Pm.Num("d", "d (shift up)", "d (Verschiebung nach oben)", 0, -100, 100, step: 0.5),
            Pm.Bool("formula", "Show formula", "Funktionsgleichung anzeigen", true),
        }.Concat(AxesSpec.Params(-4, 4, -2, 8)).ToList();

        internal static FnSpec Spec(SketchObject o)
            => new(o.Str("function", "quadratic"), o.Num("a", 1), o.Num("b", 1), o.Num("c", 0), o.Num("d", 0));

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var axes = AxesSpec.From(o, this);
            var p = Plot2D.Create(axes, o.Size, K(o), lang);
            var fn = Spec(o);
            var curve = Mx.Colored(Style.Visible, Mx.Blue);
            var runs = Fn.Curve(fn, p);
            var samples = Samples(runs);
            p.Draw(f, lang, samples);
            foreach (var run in runs) f.Poly(run, curve, tag: MathTags.Curve);
            if (o.Flag("formula", true))
            {
                var text = Fn.Formula(fn, lang);
                var area = p.Area;
                var size = Math.Min(p.Tl, Math.Max(1, area.W - 1) / (Math.Max(1, text.Length) * Mx.CharW));
                var (at, anchor) = FormulaPlace(p, samples, text, size);
                f.Text(at, text, size, italic: false, anchor: anchor, tag: MathTags.Formula, color: Mx.Blue);
            }
            var (zeros, extrema, yint) = Fn.KeyPoints(fn, axes);
            for (var i = 0; i < zeros.Count; i++) f.Port("zero" + i.ToString(CultureInfo.InvariantCulture), p.Map(zeros[i], 0));
            for (var i = 0; i < extrema.Count; i++) f.Port("ext" + i.ToString(CultureInfo.InvariantCulture), p.Map(extrema[i].X, extrema[i].Y));
            if (yint is double y0) f.Port("yint", p.Map(0, y0));
        }

        /// <summary>Points along the curve every ~0.5 mm (to keep text off it).</summary>
        internal static List<Vec2> Samples(List<List<Vec2>> runs)
        {
            var samples = new List<Vec2>();
            foreach (var run in runs)
                for (var i = 0; i + 1 < run.Count; i++)
                {
                    var n = Math.Max(1, (int)Math.Ceiling(run[i].DistanceTo(run[i + 1]) / 0.5));
                    for (var j = 0; j <= n; j++) samples.Add(Vec2.Lerp(run[i], run[i + 1], (double)j / n));
                }
            return samples;
        }

        /// <summary>
        /// The corner of the plot area where the formula covers the least: no curve, no axis line, no tick numbers
        /// (top left, top right, bottom left, bottom right in this order of preference).
        /// </summary>
        internal static (Vec2 At, TextAnchor Anchor) FormulaPlace(Plot2D p, IReadOnlyList<Vec2> samples, string text, double size)
        {
            var area = p.Area;
            var w = Mx.TextW(text, size);
            double ax = p.MapX(p.Ox), ay = p.MapY(p.Oy);
            var best = (Score: double.MaxValue, At: Vec2.Zero, Anchor: TextAnchor.Start);
            var candidates = new[]
            {
                (Right: false, Top: true), (Right: true, Top: true), (Right: false, Top: false), (Right: true, Top: false),
            };
            for (var c = 0; c < candidates.Length; c++)
            {
                var (right, top) = candidates[c];
                var x0 = right ? area.Right - 0.5 - w : area.X + 0.5;
                var y0 = top ? area.Y + 0.3 : area.Bottom - 0.3 - size;
                var rect = new Rect(x0, y0, w, size).Inflate(0.6);
                double score = c * 0.1;
                score += samples.Count(rect.Contains) * 10;
                if (ax > rect.X && ax < rect.Right) score += 6;
                if (ay > rect.Y && ay < rect.Bottom) score += 6;
                // Tick numbers below the x axis and left of the y axis.
                if (p.S.Numbers && rect.Y < ay + 1.0 + p.Ts + 0.3 && rect.Bottom > ay) score += 4;
                if (p.S.Numbers && rect.X < ax && rect.Right > ax - 1.3 - p.Ts * 3) score += 4;
                if (score < best.Score) best = (score, new Vec2(right ? area.Right - 0.5 : area.X + 0.5, y0 + size / 2), right ? TextAnchor.End : TextAnchor.Start);
            }
            return (best.At, best.Anchor);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var opt in Mx.Choice(o, "math.graph.function", new I18nText("Function", "Funktion"), Function, "function")) yield return opt;
            var fn = Spec(o);
            var label = new I18nText("Coefficients a; b; c; d…", "Koeffizienten a; b; c; d…");
            yield return ContextAction.WithInput("math.graph.coefficients", label, IconIds.Parameters, label,
                string.Join("; ", new[] { fn.A, fn.B, fn.C, fn.D }.Select(v => Mx.Num(v, Lang.En, 4).Replace('−', '-'))),
                (x, t) =>
                {
                    var parts = (t ?? "").Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    if (parts.Count is < 1 or > 4) throw new FormatException("Expected up to four numbers separated by ';'.");
                    var keys = new[] { "a", "b", "c", "d" };
                    for (var i = 0; i < parts.Count; i++) x = x.WithParam(keys[i], Geo.Clamp(Mx.ParseNumber(parts[i]), -100, 100));
                    return x;
                }) with { Group = "function", ParamKeys = new[] { "a", "b", "c", "d" } };
            yield return Mx.Toggle(o, "math.graph.formula", new I18nText("Show formula", "Funktionsgleichung anzeigen"), "formula", true, "function");
            foreach (var a in CartesianAxesType.AxesActions(o, this)) yield return a;
        }
    }
}
