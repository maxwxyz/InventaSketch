using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Conic sections with their foci and defining property: ellipse (|PF₁| + |PF₂| = 2a), hyperbola (||PF₁| − |PF₂|| = 2a, with
    /// asymptotes) and parabola (|PF| = |PL|, focus and directrix). The curve fills the box: free (also non-uniform) scaling
    /// changes the semi-axes, the foci follow (ellipse: c² = a² − b², hyperbola: c² = a² + b²).
    /// </summary>
    internal sealed class ConicType : MathBoxType
    {
        public ConicType() : base("math.conic.section", new I18nText("Conic section (foci)", "Kegelschnitt (Brennpunkte)"), MathCategories.Figures,
            "conic", "conic section", "ellipse", "hyperbola", "parabola", "focus", "foci", "directrix", "asymptote",
            "kegelschnitt", "ellipse", "hyperbel", "parabel", "brennpunkt", "brennpunkte", "leitlinie", "asymptoten", "gärtnerkonstruktion") { }

        public override Size2 DefaultSize => new(60, 40);

        public static readonly ParamDef KindDef = Pm.Choice("kind", "Curve", "Kurve", "ellipse",
            ("ellipse", "Ellipse", "Ellipse"), ("hyperbola", "Hyperbola", "Hyperbel"), ("parabola", "Parabola", "Parabel"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            KindDef,
            Pm.Bool("point", "Point P with its distances", "Punkt P mit seinen Abständen", true),
            Pm.Num("position", "Position of P", "Lage von P", 50, -180, 180, "°", 5),
            Pm.Bool("formula", "Defining property", "Definierende Eigenschaft", true),
            Pm.Bool("axes", "Axes (and asymptotes)", "Achsen (und Asymptoten)", true),
            Pm.Bool("labels", "Labels", "Beschriftung", true),
        };

        /// <summary>The defining property shown above the curve.</summary>
        internal static string Formula(string kind, Lang lang) => kind switch
        {
            "hyperbola" => "||PF₁| − |PF₂|| = 2a",
            "parabola" => "|PF| = |PL|",
            _ => "|PF₁| + |PF₂| = 2a",
        };

        /// <summary>Semi-axes a, b and focal distance c of the ellipse or hyperbola drawn into a half-extent (X, Y).</summary>
        internal static (double A, double B, double C) Axes(string kind, double X, double Y)
        {
            if (kind == "hyperbola")
            {
                var a = 0.4 * X;
                var b = Math.Min(Y / Math.Sqrt(Math.Pow(0.95 / 0.4, 2) - 1), Math.Sqrt(0.9025 - 0.16) * X);
                return (a, b, Math.Sqrt(a * a + b * b));
            }
            return (X, Y, Math.Sqrt(Math.Abs(X * X - Y * Y)));
        }

        /// <summary>Parameter p (focus to vertex) and half width of a parabola from its directrix to the top of a box of height h and half width X.</summary>
        internal static (double P, double HalfWidth) Parabola(double h, double X)
        {
            if (h <= X) return (h / 2, h);
            var p = (h - Math.Sqrt(h * h - X * X)) / 2;
            return (p, X);
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var ts = Mx.LabelSize * k;
            var kind = o.Str("kind", "ellipse");
            bool labels = o.Flag("labels", true), axes = o.Flag("axes", true), point = o.Flag("point", true), formula = o.Flag("formula", true);
            var m = labels ? 0.8 + ts : 0.6;
            var fh = formula ? ts * 1.3 : 0;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m + fh, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m - fh));
            var c0 = inner.Center;
            double X = inner.W / 2, Y = inner.H / 2;
            var t = Geo.DegToRad(Geo.Clamp(o.Num("position", 50), -180, 180));
            var dot = Math.Max(0.35, 0.5 * Math.Min(1.5, k));
            var helper = Mx.Helper with { StrokeWidth = 0.25 };
            var dist = Mx.Construction;
            Vec2 P;

            if (kind == "parabola")
            {
                var (p, half) = Parabola(inner.H, X);
                var yd = inner.Bottom;               // directrix
                var V = new Vec2(c0.X, yd - p);       // vertex
                var F = new Vec2(c0.X, yd - 2 * p);   // focus
                var pts = new List<Vec2>();
                for (var i = 0; i <= 96; i++)
                {
                    var x = -half + 2 * half * i / 96;
                    pts.Add(new Vec2(c0.X + x, V.Y - x * x / (4 * p)));
                }
                f.Poly(pts, Mx.Outline, tag: MathTags.Curve);
                f.Line(new Vec2(inner.X, yd), new Vec2(inner.Right, yd), Mx.Colored(Style.ThinLine with { StrokeWidth = 0.35 }, Mx.Blue), "directrix");
                if (axes) f.Line(new Vec2(c0.X, inner.Y), new Vec2(c0.X, yd), helper, MathTags.Helper);
                var px = half * Geo.Clamp(Math.Sin(t), -0.95, 0.95);
                P = new Vec2(c0.X + px, V.Y - px * px / (4 * p));
                f.Dot(F, dot); f.Dot(V, dot);
                if (point)
                {
                    var L = new Vec2(P.X, yd);
                    f.Line(P, F, dist, MathTags.Construction);
                    f.Line(P, L, dist, MathTags.Construction);
                    f.Dot(L, dot);
                    if (labels) f.Text(Mx.Beside(L, new Vec2(0.4, 1), "L", ts * 0.8, 0.4), "L", ts * 0.8);
                }
                if (labels)
                {
                    f.Text(Mx.Beside(F, new Vec2(-1, 0), "F", ts, 0.7), "F", ts);
                    f.Text(Mx.Beside(V, new Vec2(0.8, 1), lang == Lang.De ? "S" : "V", ts * 0.85, 0.5), lang == Lang.De ? "S" : "V", ts * 0.85);
                    var dName = lang == Lang.De ? "l" : "d";
                    f.Text(Mx.Beside(new Vec2(inner.X, yd), new Vec2(-1, 0), dName, ts, 0.5), dName, ts, italic: true);
                }
                f.Port("F", F); f.Port("vertex", V);
                f.Rail(new Vec2(inner.X, yd), new Vec2(inner.Right, yd), "directrix");
                f.Rail(new Vec2(c0.X, inner.Y), new Vec2(c0.X, yd), "axis");
            }
            else
            {
                var (a, b, c) = Axes(kind, X, Y);
                var alongX = kind == "hyperbola" || a >= b;
                var ux = alongX ? new Vec2(1, 0) : new Vec2(0, 1);
                Vec2 F1 = c0 - ux * c, F2 = c0 + ux * c;
                if (kind == "hyperbola")
                {
                    var yMax = Math.Min(Y, b * Math.Sqrt(Math.Pow(0.95 * X / a, 2) - 1));
                    var uMax = Asinh(yMax / b);
                    foreach (var side in new[] { 1, -1 })
                    {
                        var pts = new List<Vec2>();
                        for (var i = 0; i <= 64; i++)
                        {
                            var u = -uMax + 2 * uMax * i / 64;
                            pts.Add(new Vec2(c0.X + side * a * Math.Cosh(u), c0.Y - b * Math.Sinh(u)));
                        }
                        f.Poly(pts, Mx.Outline, tag: MathTags.Curve);
                    }
                    if (axes)
                    {
                        var tx = Math.Min(X, Y * a / b);
                        f.Line(c0 + new Vec2(-tx, tx * b / a), c0 + new Vec2(tx, -tx * b / a), helper, MathTags.Helper);
                        f.Line(c0 + new Vec2(-tx, -tx * b / a), c0 + new Vec2(tx, tx * b / a), helper, MathTags.Helper);
                        f.Line(new Vec2(inner.X, c0.Y), new Vec2(inner.Right, c0.Y), helper, MathTags.Helper);
                    }
                    var us = Asinh(yMax / b) * Geo.Clamp(Math.Sin(t), -0.9, 0.9);
                    P = new Vec2(c0.X + a * Math.Cosh(us), c0.Y - b * Math.Sinh(us));
                }
                else
                {
                    f.Add(new EllipsePrim(c0, X, Y, Mx.Outline) { Tag = MathTags.Outline });
                    if (axes)
                    {
                        f.Line(new Vec2(inner.X, c0.Y), new Vec2(inner.Right, c0.Y), helper, MathTags.Helper);
                        f.Line(new Vec2(c0.X, inner.Y), new Vec2(c0.X, inner.Bottom), helper, MathTags.Helper);
                    }
                    P = new Vec2(c0.X + X * Math.Cos(t), c0.Y - Y * Math.Sin(t));
                }
                if (point)
                {
                    f.Line(P, F1, dist, MathTags.Construction);
                    f.Line(P, F2, dist, MathTags.Construction);
                }
                f.Dot(F1, dot); f.Dot(F2, dot); f.Dot(c0, dot * 0.8);
                if (labels)
                {
                    var down = alongX ? new Vec2(0, 1) : new Vec2(1, 0);
                    f.Text(Mx.Beside(F1, down, "F₁", ts, 0.6), "F₁", ts);
                    f.Text(Mx.Beside(F2, down, "F₂", ts, 0.6), "F₂", ts);
                    f.Text(Mx.Beside(c0, -down, Tri.CircleCentre(lang), ts * 0.85, 0.5), Tri.CircleCentre(lang), ts * 0.85);
                }
                f.Port("centre", c0); f.Port("F1", F1); f.Port("F2", F2);
                var v = alongX ? new Vec2(a, 0) : new Vec2(0, b);
                f.Pt(AnchorRole.Quadrant, c0 - v, "V1"); f.Pt(AnchorRole.Quadrant, c0 + v, "V2");
                f.Rail(new Vec2(inner.X, c0.Y), new Vec2(inner.Right, c0.Y), "axis");
            }

            if (point)
            {
                f.Dot(P, dot);
                if (labels) f.Text(Mx.Beside(P, (P - c0).Length < 1e-9 ? new Vec2(1, -1) : P - c0, "P", ts, 0.6), "P", ts);
                f.Port("P", P);
            }
            if (formula)
            {
                var text = Formula(kind, lang);
                var size = Math.Min(ts * 0.9, inner.W / Math.Max(1, Mx.TextW(text, 1)));
                f.Text(new Vec2(0, -o.Size.H / 2 + m * 0.5 + fh / 2), text, size, tag: MathTags.Formula);
            }
        }

        private static double Asinh(double x) => Math.Log(x + Math.Sqrt(x * x + 1));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var opt in Mx.Choice(o, "math.conic.kind", new I18nText("Curve", "Kurve"), KindDef, "conic")) yield return opt;
            yield return Mx.NumberInput(o, "math.conic.angle", new I18nText("Position of P…", "Lage von P…"), Parameters[2], "conic");
            yield return Mx.Toggle(o, "math.conic.point", new I18nText("Point P with its distances", "Punkt P mit Abständen"), "point", true, "conic");
            yield return Mx.Toggle(o, "math.conic.formula", new I18nText("Defining property", "Definierende Eigenschaft"), "formula", true, "labels");
            yield return Mx.Toggle(o, "math.conic.axes", new I18nText("Axes and asymptotes", "Achsen und Asymptoten"), "axes", true, "labels");
            yield return Mx.Toggle(o, "math.conic.labels", new I18nText("Labels", "Beschriftung"), "labels", true, "labels");
        }
    }
}
