using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Values of the triangle (<c>math.shape.triangle</c>): sides a, b, c, angles α, β, γ, area A and perimeter u with the
    /// angle sum, the law of cosines (three times: a side from two sides and the included angle, an angle from three sides),
    /// the law of sines (an angle from two sides and an opposite angle; the acute solution of the ambiguous case), the area
    /// A = ½·a·b·sin γ and the perimeter. Any three independent values with at least one side (SSS, SAS, SSA, ASA, AAS)
    /// give the rest. The kind "true to the values" draws the triangle in the shape the entered values give.
    /// </summary>
    internal static class TriValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        public static readonly string[] SideKeys = { "a", "b", "c" };
        public static readonly string[] AngleKeys = { "alpha", "beta", "gamma" };
        public static readonly string[] Greek = { "α", "β", "γ" };

        internal static readonly VarDef[] Vars =
        {
            new("a", T("Side a (BC)", "Seite a (BC)"), T("a", "a"), Quantities.Length) { Unit = "cm", Min = 1e-6, Max = 1e6 },
            new("b", T("Side b (CA)", "Seite b (CA)"), T("b", "b"), Quantities.Length) { Unit = "cm", Min = 1e-6, Max = 1e6 },
            new("c", T("Side c (AB)", "Seite c (AB)"), T("c", "c"), Quantities.Length) { Unit = "cm", Min = 1e-6, Max = 1e6 },
            new("alpha", T("Angle α at A", "Winkel α bei A"), T("α", "α"), Quantities.Angle) { Unit = "°", Min = 1e-9, Max = Math.PI, Relevance = VarRelevance.Panel },
            new("beta", T("Angle β at B", "Winkel β bei B"), T("β", "β"), Quantities.Angle) { Unit = "°", Min = 1e-9, Max = Math.PI, Relevance = VarRelevance.Panel },
            new("gamma", T("Angle γ at C", "Winkel γ bei C"), T("γ", "γ"), Quantities.Angle) { Unit = "°", Min = 1e-9, Max = Math.PI, Relevance = VarRelevance.Panel },
            new("A", T("Area", "Flächeninhalt"), T("A", "A"), Quantities.Area) { Unit = "cm²", Param = "area", Min = 1e-12, Max = 1e12 },
            new("u", T("Perimeter", "Umfang"), T("u", "u"), Quantities.Length) { Unit = "cm", Param = "perimeter", Min = 1e-6, Max = 1e6, Relevance = VarRelevance.Panel },
        };

        /// <summary>The value parameters (text, as typed; all optional and advanced).</summary>
        internal static IEnumerable<ParamDef> Params => new[]
        {
            new ParamDef("a", T("Side a", "Seite a"), ParamKind.Text) { Unit = "cm", Advanced = true },
            new ParamDef("b", T("Side b", "Seite b"), ParamKind.Text) { Unit = "cm", Advanced = true },
            new ParamDef("c", T("Side c", "Seite c"), ParamKind.Text) { Unit = "cm", Advanced = true },
            new ParamDef("alpha", T("Angle α", "Winkel α"), ParamKind.Text) { Unit = "°", Advanced = true },
            new ParamDef("beta", T("Angle β", "Winkel β"), ParamKind.Text) { Unit = "°", Advanced = true },
            new ParamDef("gamma", T("Angle γ", "Winkel γ"), ParamKind.Text) { Unit = "°", Advanced = true },
            new ParamDef("area", T("Area", "Flächeninhalt"), ParamKind.Text) { Unit = "cm²", Advanced = true },
            ValueLabels.ShowValues,
        };

        private static Relation CosineLaw(string side, string angle, string s1, string s2, string sym, string symAngle)
            => new Relation("math.triangle.cosine." + side, T("Law of cosines for " + sym, "Kosinussatz für " + sym),
                    "{" + side + "}² = {" + s1 + "}² + {" + s2 + "}² − 2·{" + s1 + "}·{" + s2 + "}·cos {" + angle + "}", side, angle, s1, s2) { Source = "S43" }
                .Solve(side, "√({" + s1 + "}² + {" + s2 + "}² − 2·{" + s1 + "}·{" + s2 + "}·cos {" + angle + "})",
                    v => Math.Sqrt(Math.Max(0, v(s1) * v(s1) + v(s2) * v(s2) - 2 * v(s1) * v(s2) * Math.Cos(v(angle)))))
                .Solve(angle, "arccos(({" + s1 + "}² + {" + s2 + "}² − {" + side + "}²)/(2·{" + s1 + "}·{" + s2 + "}))",
                    v => Acos((v(s1) * v(s1) + v(s2) * v(s2) - v(side) * v(side)) / (2 * v(s1) * v(s2))));

        /// <summary>a·sin β = b·sin α (sides <paramref name="s1"/>, <paramref name="s2"/> opposite the angles <paramref name="w1"/>, <paramref name="w2"/>).</summary>
        private static Relation SineLaw(string s1, string w1, string s2, string w2)
            => new Relation("math.triangle.sine." + s1 + s2, T("Law of sines", "Sinussatz"), "{" + s1 + "}/sin {" + w1 + "} = {" + s2 + "}/sin {" + w2 + "}", s1, w1, s2, w2) { Source = "S43" }
                .Solve(s1, "{" + s2 + "}·sin {" + w1 + "}/sin {" + w2 + "}", v => v(s2) * Math.Sin(v(w1)) / Math.Sin(v(w2)))
                .Solve(s2, "{" + s1 + "}·sin {" + w2 + "}/sin {" + w1 + "}", v => v(s1) * Math.Sin(v(w2)) / Math.Sin(v(w1)))
                .Solve(w1, "arcsin({" + s1 + "}·sin {" + w2 + "}/{" + s2 + "})", v => Asin(v(s1) * Math.Sin(v(w2)) / v(s2)))
                .Solve(w2, "arcsin({" + s2 + "}·sin {" + w1 + "}/{" + s1 + "})", v => Asin(v(s2) * Math.Sin(v(w1)) / v(s1)));

        private static double Acos(double x) => x < -1 - 1e-9 || x > 1 + 1e-9 ? double.NaN : Math.Acos(Math.Max(-1, Math.Min(1, x)));
        private static double Asin(double x) => x < -1 - 1e-9 || x > 1 + 1e-9 ? double.NaN : Math.Asin(Math.Max(-1, Math.Min(1, x)));

        private static readonly Relation AngleSum =
            new Relation("math.triangle.anglesum", T("Angle sum in the triangle", "Winkelsumme im Dreieck"), "{alpha} + {beta} + {gamma} = 180°", "alpha", "beta", "gamma") { Source = "S43" }
                .Solve("alpha", "180° − {beta} − {gamma}", v => Math.PI - v("beta") - v("gamma"))
                .Solve("beta", "180° − {alpha} − {gamma}", v => Math.PI - v("alpha") - v("gamma"))
                .Solve("gamma", "180° − {alpha} − {beta}", v => Math.PI - v("alpha") - v("beta"));

        private static readonly Relation Area =
            new Relation("math.triangle.area", T("Area of the triangle", "Flächeninhalt des Dreiecks"), "{A} = ½·{a}·{b}·sin {gamma}", "A", "a", "b", "gamma") { Source = "S43" }
                .Solve("A", "½·{a}·{b}·sin {gamma}", v => 0.5 * v("a") * v("b") * Math.Sin(v("gamma")))
                .Solve("a", "2·{A}/({b}·sin {gamma})", v => 2 * v("A") / (v("b") * Math.Sin(v("gamma"))))
                .Solve("b", "2·{A}/({a}·sin {gamma})", v => 2 * v("A") / (v("a") * Math.Sin(v("gamma"))))
                .Solve("gamma", "arcsin(2·{A}/({a}·{b}))", v => Asin(2 * v("A") / (v("a") * v("b"))));

        private static readonly Relation Perimeter =
            new Relation("math.triangle.perimeter", T("Perimeter of the triangle", "Umfang des Dreiecks"), "{u} = {a} + {b} + {c}", "u", "a", "b", "c") { Source = "S43" }
                .Solve("u", "{a} + {b} + {c}", v => v("a") + v("b") + v("c"))
                .Solve("a", "{u} − {b} − {c}", v => v("u") - v("b") - v("c"))
                .Solve("b", "{u} − {a} − {c}", v => v("u") - v("a") - v("c"))
                .Solve("c", "{u} − {a} − {b}", v => v("u") - v("a") - v("b"));

        internal static readonly Relation[] Rels =
        {
            AngleSum,
            CosineLaw("a", "alpha", "b", "c", "a", "α"), CosineLaw("b", "beta", "c", "a", "b", "β"), CosineLaw("c", "gamma", "a", "b", "c", "γ"),
            SineLaw("a", "alpha", "b", "beta"), SineLaw("b", "beta", "c", "gamma"), SineLaw("a", "alpha", "c", "gamma"),
            Area, Perimeter,
        };

        /// <summary>The entered text of a value parameter, trimmed ("" when not entered).</summary>
        internal static string Entered(SketchObject o, string key) => (o.Params.TryGetValue(key, out var p) ? p.Text ?? p.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) : null)?.Trim() ?? "";

        /// <summary>The entered value (SI) of a variable, or null when not entered or not readable.</summary>
        private static double? Value(SketchObject o, string key)
        {
            var v = Vars.First(x => x.Key == key);
            var (value, _, _, _) = ValueModel.ReadEntered(o, v);
            return value is double d && d > 0 && !double.IsInfinity(d) ? d : null;
        }

        /// <summary>
        /// Side lengths a, b, c from the entered values (SSS, SAS, SSA with the acute solution, ASA, AAS; two or three angles
        /// alone give the shape with c = 1), or null when the values do not determine a triangle.
        /// </summary>
        internal static (double A, double B, double C)? Shape(SketchObject o)
        {
            var s = SideKeys.Select(k => Value(o, k)).ToArray();
            var w = AngleKeys.Select(k => Value(o, k)).ToArray();
            // Two angles give the third.
            var knownAngles = w.Count(x => x is not null);
            if (knownAngles == 2)
            {
                var i = Array.FindIndex(w, x => x is null);
                w[i] = Math.PI - w.Where(x => x is not null).Sum(x => x!.Value);
                if (w[i] <= 1e-9) return null;
            }
            if (w.All(x => x is not null))
            {
                if (Math.Abs(w.Sum(x => x!.Value) - Math.PI) > 1e-6) return null;
                var j = Array.FindIndex(s, x => x is not null);
                var k = j < 0 ? 2 : j;
                var known = j < 0 ? 1.0 : s[j]!.Value;
                var d = known / Math.Sin(w[k]!.Value); // law of sines: side/sin(opposite angle) is constant
                return Valid(d * Math.Sin(w[0]!.Value), d * Math.Sin(w[1]!.Value), d * Math.Sin(w[2]!.Value));
            }
            var sides = s.Count(x => x is not null);
            if (sides == 3) return Valid(s[0]!.Value, s[1]!.Value, s[2]!.Value);
            if (sides == 2)
            {
                var m = Array.FindIndex(s, x => x is null); // the missing side
                int p = (m + 1) % 3, q = (m + 2) % 3;
                if (w[m] is double inc) // SAS: the included angle lies opposite the missing side
                {
                    s[m] = Math.Sqrt(Math.Max(0, s[p]!.Value * s[p]!.Value + s[q]!.Value * s[q]!.Value - 2 * s[p]!.Value * s[q]!.Value * Math.Cos(inc)));
                    return Valid(s[0]!.Value, s[1]!.Value, s[2]!.Value);
                }
                // SSA: an angle opposite one of the known sides; the angle opposite the other known side by the law of sines (acute).
                foreach (var (known, other) in new[] { (p, q), (q, p) })
                {
                    if (w[known] is not double wk) continue;
                    var sinOther = s[other]!.Value * Math.Sin(wk) / s[known]!.Value;
                    if (sinOther > 1 + 1e-12) return null;
                    var wo = Math.Asin(Math.Min(1, sinOther));
                    var wm = Math.PI - wk - wo;
                    if (wm <= 1e-9) return null;
                    s[m] = s[known]!.Value * Math.Sin(wm) / Math.Sin(wk);
                    return Valid(s[0]!.Value, s[1]!.Value, s[2]!.Value);
                }
            }
            return null;
        }

        private static (double, double, double)? Valid(double a, double b, double c)
        {
            if (!(a > 0 && b > 0 && c > 0) || double.IsInfinity(a + b + c)) return null;
            if (a + b <= c * (1 + 1e-9) || b + c <= a * (1 + 1e-9) || c + a <= b * (1 + 1e-9)) return null;
            return (a, b, c);
        }

        /// <summary>
        /// Vertices of the triangle with the sides a, b, c in shape (A bottom left, B bottom right, C above; y down), fitted
        /// uniformly into <paramref name="r"/>.
        /// </summary>
        internal static (Vec2 A, Vec2 B, Vec2 C) Place(double a, double b, double c, Rect r)
        {
            var cosA = Math.Max(-1, Math.Min(1, (b * b + c * c - a * a) / (2 * b * c)));
            var pts = new[] { new Vec2(0, 0), new Vec2(c, 0), new Vec2(b * cosA, -b * Math.Sqrt(1 - cosA * cosA)) };
            var (map, _) = Mx.FitUniform(pts, r);
            return (map(pts[0]), map(pts[1]), map(pts[2]));
        }
    }
}
