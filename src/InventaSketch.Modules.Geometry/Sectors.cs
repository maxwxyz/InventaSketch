using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Circle sector (bounded by two radii and the arc) or circle segment (bounded by the chord and the arc) with the
    /// central angle marked and labelled (value in degrees or α). Stays round under free scaling; both rays have grips.
    /// </summary>
    internal sealed class SectorType : MathBoxType
    {
        public SectorType() : base("math.arc.sector", new I18nText("Circle sector", "Kreissektor (Kreisausschnitt)"), MathCategories.Figures,
            "sector", "segment", "arc", "central angle", "pie", "kreissektor", "kreisausschnitt", "kreissegment", "kreisabschnitt", "kreisbogen", "mittelpunktswinkel", "sehne", "chord") { }

        public override Size2 DefaultSize => new(40, 40);
        public override bool PreferUniformScale => true;

        public static readonly ParamDef Kind = Pm.Choice("kind", "Kind", "Art", "sector",
            ("sector", "Sector (two radii and the arc)", "Sektor (zwei Radien und Bogen)"), ("segment", "Segment (chord and arc)", "Segment (Sehne und Bogen)"));
        public static readonly ParamDef AngleLabel = Pm.Choice("angleLabel", "Angle label", "Winkelbeschriftung", "value",
            ("value", "Value (60°)", "Wert (60°)"), ("alpha", "Letter (α)", "Buchstabe (α)"), ("none", "None", "Keine"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Kind,
            Pm.Num("angle", "Central angle", "Mittelpunktswinkel", 70, 1, 359, "°", 5),
            Pm.Num("start", "Direction of the first radius", "Richtung des ersten Radius", 20, -360, 360, "°", 15),
            AngleLabel,
            Pm.Bool("circle", "Rest of the circle", "Restlicher Kreis", true),
            Pm.Bool("fill", "Tinted area", "Fläche getönt", true),
            Pm.Bool("labels", "Radius label (r)", "Radius beschriften (r)", true),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var R = Math.Max(0.5, Math.Min(o.Size.W, o.Size.H) / 2 - 0.4);
            var c = Vec2.Zero;
            var start = o.Num("start", 20);
            var angle = Geo.Clamp(o.Num("angle", 70), 1, 359);
            var segment = o.Str("kind", "sector") == "segment";
            var d1 = Mx.Dir(start);
            var d2 = Mx.Dir(start + angle);
            // Mathematical angles turn counter-clockwise: on screen the sweep is negative.
            var arc = Geo.ArcPoints(c, R, R, Geo.DegToRad(-start), Geo.DegToRad(-angle), Math.Max(8, (int)Math.Ceiling(angle / 3)));
            var area = segment ? arc : new[] { c }.Concat(arc).ToList();
            var style = o.Flag("fill", true) ? Mx.Outline with { Fill = Fill.Solid("#bbdefb") } : Mx.Outline;
            f.Poly(area, style, closed: true, tag: MathTags.Outline);
            if (o.Flag("circle", true))
                f.Arc(c, R, Geo.DegToRad(-(start + angle)), Geo.DegToRad(-(360 - angle)), Mx.Thin with { StrokeWidth = 0.35 }, "circle");
            if (segment)
            {
                f.Line(c, c + d1 * R, Mx.Construction, MathTags.Construction);
                f.Line(c, c + d2 * R, Mx.Construction, MathTags.Construction);
            }
            f.Dot(c, Math.Max(0.35, 0.45 * Math.Min(1.5, k)));

            // Angle mark at the centre and its label on the bisector.
            var rm = Math.Min(R * 0.3, Math.Max(R * 0.18, tl * 1.4));
            f.Arc(c, rm, Geo.DegToRad(-start), Geo.DegToRad(-angle), Mx.Mark, MathTags.Mark);
            var mode = o.Str("angleLabel", "value");
            if (mode != "none")
            {
                var text = mode == "alpha" ? "α" : Mx.Num(angle, lang, 1) + "°";
                var size = mode == "alpha" ? tl : tl * 0.8;
                var bis = Mx.Dir(start + angle / 2);
                f.Text(Mx.Beside(c + bis * rm, bis, text, size, 0.5), text, size, italic: mode == "alpha");
            }
            if (o.Flag("labels", true))
            {
                // "r" beside the first radius, on the side away from the area.
                var away = Mx.Dir(start - 90);
                f.Text(Mx.Beside(c + d1 * (R * 0.62), away, "r", tl, 0.5), "r", tl, italic: true);
            }
            f.Port("centre", c);
            f.Pt(AnchorRole.End, c + d1 * R, "ray1");
            f.Pt(AnchorRole.End, c + d2 * R, "ray2");
            f.Pt(AnchorRole.Mid, c + Mx.Dir(start + angle / 2) * R, "arc");
            for (var i = 0; i < 4; i++) f.Pt(AnchorRole.Quadrant, c + Mx.Dir(90 * i) * R, "q" + i.ToString(CultureInfo.InvariantCulture));

            // Grips: the first ray turns the whole sector, the second one sets the central angle.
            f.AngleGrip("start", c, c + d1 * R, start, -360, 360, "Direction of the first radius", "Richtung des ersten Radius");
            var label = new I18nText("Central angle: " + Mx.Num(angle, Lang.En, 1) + "°", "Mittelpunktswinkel: " + Mx.Num(angle, Lang.De, 1) + "°");
            f.Grips.Add(new MathGrip(new DragHandle("angle", c + d2 * R, DragConstraint.Free, label), (x, p) =>
            {
                var d = p - c;
                if (d.Length < Geo.Eps) return x;
                var deg = Geo.RadToDeg(Math.Atan2(-d.Y, d.X));
                var a = Geo.NormDeg(deg - x.Num("start", 20));
                return x.WithParam("angle", Geo.Clamp(Dragging.Snap(a, Mx.AngleSnap), 1, 359));
            }));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Mx.Choice(o, "math.sector.kind", new I18nText("Sector or segment", "Sektor oder Segment"), Kind, "sector")) yield return a;
            yield return Mx.NumberInput(o, "math.sector.angle", new I18nText("Central angle…", "Mittelpunktswinkel…"), Parameters[1], "sector");
            foreach (var a in Mx.Choice(o, "math.sector.anglelabel", new I18nText("Angle label", "Winkelbeschriftung"), AngleLabel, "labels")) yield return a;
            yield return Mx.Toggle(o, "math.sector.circle", new I18nText("Rest of the circle", "Restlicher Kreis"), "circle", true, "sector");
            yield return Mx.Toggle(o, "math.sector.fill", new I18nText("Tinted area", "Fläche getönt"), "fill", true, "sector", IconIds.FillTint);
            yield return Mx.Toggle(o, "math.sector.labels", new I18nText("Radius label", "Radius beschriften"), "labels", true, "labels");
        }
    }

    /// <summary>
    /// Theorem of Pythagoras: a right triangle (legs a and b, hypotenuse c, right angle at C) with the squares on its three
    /// sides, labelled a², b², c² (or their values) and optionally divided into unit squares for whole-number sides.
    /// </summary>
    internal sealed class PythagorasType : MathBoxType
    {
        public PythagorasType() : base("math.shape.pythagoras", new I18nText("Pythagoras (squares on the sides)", "Satz des Pythagoras (Quadrate)"), MathCategories.Figures,
            "pythagoras", "pythagorean theorem", "hypotenuse", "legs", "squares", "satz des pythagoras", "kathete", "katheten", "hypotenuse", "quadrate",
            "rechtwinkliges dreieck", "right triangle", "a²+b²=c²") { }

        public override Size2 DefaultSize => new(50, 54);
        public override bool PreferUniformScale => true;

        public static readonly ParamDef Areas = Pm.Choice("areas", "Square labels", "Beschriftung der Quadrate", "symbols",
            ("symbols", "a², b², c²", "a², b², c²"), ("values", "Areas (9, 16, 25)", "Flächen (9, 16, 25)"), ("none", "None", "Keine"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("a", "Leg a", "Kathete a", 3, 1, 20, null, 1),
            Pm.Num("b", "Leg b", "Kathete b", 4, 1, 20, null, 1),
            Areas,
            Pm.Bool("labels", "Side labels (a, b, c)", "Seiten beschriften (a, b, c)", true),
            Pm.Bool("grid", "Unit squares (whole-number sides)", "Einheitsquadrate (ganzzahlige Seiten)", false),
            Pm.Bool("fill", "Tinted squares", "Quadrate getönt", true),
        };

        private static readonly string[] Tints = { "#bbdefb", "#c8e6c9", "#ffe0b2" };

        /// <summary>Vertices A, B (hypotenuse, horizontal) and C (right angle, above) for legs a = BC and b = AC.</summary>
        internal static (Vec2 A, Vec2 B, Vec2 C) Triangle(double a, double b)
        {
            var c = Math.Sqrt(a * a + b * b);
            return (Vec2.Zero, new Vec2(c, 0), new Vec2(b * b / c, -a * b / c));
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            double a = Geo.Clamp(o.Num("a", 3), 1, 20), b = Geo.Clamp(o.Num("b", 4), 1, 20);
            var (A, B, C) = Triangle(a, b);
            var cLen = A.DistanceTo(B);
            // Squares outward: on AB below, on AC away from B, on BC away from A.
            Vec2 Out(Vec2 p, Vec2 q, Vec2 other)
            {
                var n = (q - p).Normalized().Perp;
                return (other - p).Dot(n) > 0 ? -n : n;
            }
            var sq = new[]
            {
                (P: B, Q: C, N: Out(B, C, A), Len: a, Name: "a"),
                (P: C, Q: A, N: Out(C, A, B), Len: b, Name: "b"),
                (P: A, Q: B, N: Out(A, B, C), Len: cLen, Name: "c"),
            };
            var corners = sq.Select(s => new[] { s.P, s.Q, s.Q + s.N * s.Len, s.P + s.N * s.Len }).ToArray();
            var inner = new Rect(-o.Size.W / 2 + 0.4, -o.Size.H / 2 + 0.4, Math.Max(1, o.Size.W - 0.8), Math.Max(1, o.Size.H - 0.8));
            var (map, s0) = Mx.FitUniform(corners.SelectMany(x => x), inner);
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var fill = o.Flag("fill", true);
            var areas = o.Str("areas", "symbols");
            for (var i = 0; i < 3; i++)
            {
                var pts = corners[i].Select(map).ToList();
                f.Poly(pts, fill ? Mx.Outline with { Fill = Fill.Solid(Tints[i]) } : Mx.Outline, closed: true, tag: "square");
                var len = sq[i].Len;
                if (o.Flag("grid", false) && Math.Abs(len - Math.Round(len)) < 1e-9 && Math.Round(len) >= 2)
                {
                    var n = (int)Math.Round(len);
                    for (var j = 1; j < n; j++)
                    {
                        var t = (double)j / n;
                        f.Line(Vec2.Lerp(pts[0], pts[1], t), Vec2.Lerp(pts[3], pts[2], t), Mx.GridLine, MathTags.Grid);
                        f.Line(Vec2.Lerp(pts[0], pts[3], t), Vec2.Lerp(pts[1], pts[2], t), Mx.GridLine, MathTags.Grid);
                    }
                }
                if (areas != "none")
                {
                    var text = areas == "values" ? Mx.Num(len * len, lang, 2) : sq[i].Name + "²";
                    var size = Math.Min(tl * 1.1, len * s0 * 0.4);
                    f.Text(pts.Aggregate(Vec2.Zero, (acc, p) => acc + p) / 4, text, size, italic: areas == "symbols", tag: MathTags.Formula);
                }
            }
            var (a2, b2, c2) = (map(A), map(B), map(C));
            f.Poly(new[] { a2, b2, c2 }, Mx.Outline with { Fill = fill ? Fill.Solid("#ffffff") : null }, closed: true, tag: MathTags.Outline);
            var q = Math.Min(Math.Min(a, b) * s0 * 0.25, tl * 0.9);
            Mx.RightAngle(f, c2, (a2 - c2).Normalized(), (b2 - c2).Normalized(), q);
            if (o.Flag("labels", true))
            {
                // The side letters sit just inside the squares, along the triangle's sides (the triangle itself is often too small).
                for (var i = 0; i < 3; i++)
                {
                    var p0 = map(sq[i].P); var p1 = map(sq[i].Q);
                    var side = sq[i].Len * s0;
                    var size = Math.Min(tl * 0.9, Math.Max(1, side * 0.3));
                    f.Text(Vec2.Lerp(p0, p1, 0.5) + sq[i].N * (size * 0.85), sq[i].Name, size, italic: true);
                }
            }
            f.Port("A", a2); f.Port("B", b2); f.Port("C", c2);
            f.Pt(AnchorRole.Mid, Vec2.Lerp(b2, c2, 0.5), "ma");
            f.Pt(AnchorRole.Mid, Vec2.Lerp(c2, a2, 0.5), "mb");
            f.Pt(AnchorRole.Mid, Vec2.Lerp(a2, b2, 0.5), "mc");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Mx.Choice(o, "math.pythagoras.areas", new I18nText("Square labels", "Beschriftung der Quadrate"), Areas, "labels")) yield return a;
            yield return Mx.Toggle(o, "math.pythagoras.labels", new I18nText("Side labels", "Seiten beschriften"), "labels", true, "labels");
            yield return Mx.Toggle(o, "math.pythagoras.grid", new I18nText("Unit squares", "Einheitsquadrate"), "grid", false, "squares", IconIds.Grid);
            yield return Mx.Toggle(o, "math.pythagoras.fill", new I18nText("Tinted squares", "Quadrate getönt"), "fill", true, "squares", IconIds.FillTint);
        }
    }
}
