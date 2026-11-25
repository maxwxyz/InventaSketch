using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>Construction helpers: compass marks.</summary>
    internal static class Compass
    {
        /// <summary>Unit-space points of a compass arc around centre c through p, ±<paramref name="halfDeg"/> (for fitting).</summary>
        public static List<Vec2> MarkPoints(Vec2 c, Vec2 p, double halfDeg)
        {
            var r = c.DistanceTo(p);
            var a = Math.Atan2(p.Y - c.Y, p.X - c.X);
            var h = Geo.DegToRad(halfDeg);
            return Geo.ArcPoints(c, r, r, a - h, 2 * h, 8);
        }

        /// <summary>Draws a compass mark (short arc) around c through p after mapping.</summary>
        public static void Mark(Fig f, Func<Vec2, Vec2> map, Vec2 c, Vec2 p, double halfDeg)
            => f.Poly(MarkPoints(c, p, halfDeg).Select(map), Mx.Construction, tag: MathTags.Construction);
    }

    /// <summary>Perpendicular bisector of a segment AB with compass marks, midpoint and right angle.</summary>
    internal sealed class PerpendicularBisectorType : MathBoxType
    {
        public PerpendicularBisectorType() : base("math.construct.perpendicular", new I18nText("Perpendicular bisector", "Mittelsenkrechte"), MathCategories.Constructions,
            "perpendicular bisector", "midpoint", "construction", "compass", "mittelsenkrechte", "mittelpunkt", "konstruktion", "zirkel") { }

        public override Size2 DefaultSize => new(50, 40);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Bool("arcs", "Compass marks", "Zirkelbögen", true),
            Pm.Bool("labels", "Labels", "Beschriftung", true),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var labels = o.Flag("labels", true);
            var arcs = o.Flag("arcs", true);
            Vec2 A = new(-0.5, 0), B = new(0.5, 0);
            const double rho = 0.7;
            var h = Math.Sqrt(rho * rho - 0.25);
            Vec2 S1 = new(0, -h), S2 = new(0, h);
            Vec2 top = new(0, -0.75), bottom = new(0, 0.75);
            var marks = new List<(Vec2 C, Vec2 P)> { (A, S1), (A, S2), (B, S1), (B, S2) };
            var pts = new List<Vec2> { A, B, top, bottom };
            if (arcs) foreach (var (c, p) in marks) pts.AddRange(Compass.MarkPoints(c, p, 16));
            var m = labels ? 1.0 + tl : 0.4;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var (map, s) = Mx.FitUniform(pts, inner);
            Vec2 a = map(A), b = map(B), mid = map(Vec2.Zero), t = map(top), bo = map(bottom);
            f.Line(a, b, Mx.Outline, MathTags.Main);
            if (arcs) foreach (var (c, p) in marks) Compass.Mark(f, map, c, p, 16);
            f.Line(t, bo, Mx.Colored(Style.Visible, Mx.Red), MathTags.Construction);
            Mx.RightAngle(f, mid, new Vec2(1, 0), new Vec2(0, -1), Math.Min(3 * k, s * 0.1));
            var dotR = Math.Max(0.35, 0.5 * Math.Min(1.5, k));
            f.Dot(a, dotR); f.Dot(b, dotR); f.Dot(mid, dotR);
            if (labels)
            {
                f.Text(Mx.Beside(a, new Vec2(-1, 0), "A", tl, 0.8), "A", tl);
                f.Text(Mx.Beside(b, new Vec2(1, 0), "B", tl, 0.8), "B", tl);
                f.Text(Mx.Beside(mid, new Vec2(-0.7, 0.7), "M", tl, 0.6), "M", tl);
                f.Text(Mx.Beside(t, new Vec2(1, 0.4), "m", tl, 0.8), "m", tl, italic: true);
            }
            f.Port("A", a); f.Port("B", b); f.Port("M", mid);
            f.Port("S1", map(S1)); f.Port("S2", map(S2));
            f.Pt(AnchorRole.Mid, t, "top"); f.Pt(AnchorRole.Mid, bo, "bottom");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Toggle(o, "math.perpendicular.arcs", new I18nText("Compass marks", "Zirkelbögen"), "arcs", true, "construction");
            yield return Mx.Toggle(o, "math.perpendicular.labels", new I18nText("Labels", "Beschriftung"), "labels", true, "construction");
        }
    }

    /// <summary>Angle bisector constructed with compass marks; both halves marked as equal.</summary>
    internal sealed class AngleBisectorType : MathBoxType
    {
        public AngleBisectorType() : base("math.construct.anglebisector", new I18nText("Angle bisector", "Winkelhalbierende"), MathCategories.Constructions,
            "angle bisector", "bisect", "construction", "compass", "winkelhalbierende", "halbieren", "konstruktion", "zirkel") { }

        public override Size2 DefaultSize => new(56, 40);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("angle", "Angle", "Winkel", 70, 10, 170, "°", 5),
            Pm.Bool("arcs", "Compass marks", "Zirkelbögen", true),
            Pm.Bool("labels", "Labels", "Beschriftung", true),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var labels = o.Flag("labels", true);
            var arcs = o.Flag("arcs", true);
            var phi = Geo.Clamp(o.Num("angle", 70), 10, 170);
            const double len = 1.1, rho1 = 0.45;
            var S = Vec2.Zero;
            Vec2 r1 = new(len, 0), r2 = Mx.Dir(phi) * len, bisEnd = Mx.Dir(phi / 2) * len;
            Vec2 P1 = new(rho1, 0), P2 = Mx.Dir(phi) * rho1;
            var half = rho1 * Math.Sin(Geo.DegToRad(phi / 2));
            var rho2 = Math.Max(0.32, half * 1.25);
            var Q = Mx.Dir(phi / 2) * (rho1 * Math.Cos(Geo.DegToRad(phi / 2)) + Math.Sqrt(rho2 * rho2 - half * half));
            // Arc around S through both rays (counter-clockwise from the first ray, a little beyond both).
            var arcPts = Geo.ArcPoints(S, rho1, rho1, Geo.DegToRad(8), -Geo.DegToRad(phi + 16), 24);
            var pts = new List<Vec2> { S, r1, r2, bisEnd };
            if (arcs)
            {
                pts.AddRange(arcPts);
                pts.AddRange(Compass.MarkPoints(P1, Q, 14));
                pts.AddRange(Compass.MarkPoints(P2, Q, 14));
            }
            var m = labels ? 1.0 + tl : 0.4;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var (map, s) = Mx.FitUniform(pts, inner);
            var s0 = map(S);
            f.Line(s0, map(r1), Mx.Outline, MathTags.Main);
            f.Line(s0, map(r2), Mx.Outline, MathTags.Main);
            if (arcs)
            {
                f.Poly(arcPts.Select(map), Mx.Construction, tag: MathTags.Construction);
                Compass.Mark(f, map, P1, Q, 14);
                Compass.Mark(f, map, P2, Q, 14);
            }
            f.Line(s0, map(bisEnd), Mx.Colored(Style.Visible, Mx.Red), MathTags.Construction);
            // Equal halves: an arc with a small tick in each half.
            var mr = s * 0.22;
            foreach (var (from, to) in new[] { (0.0, phi / 2), (phi / 2, phi) })
            {
                f.Arc(s0, mr, -Geo.DegToRad(from), -Geo.DegToRad(to - from), Mx.Mark, MathTags.Mark);
                var md = Mx.Dir((from + to) / 2);
                f.Line(s0 + md * (mr - 0.8), s0 + md * (mr + 0.8), Mx.Mark, MathTags.Mark);
            }
            var dotR = Math.Max(0.35, 0.5 * Math.Min(1.5, k));
            f.Dot(s0, dotR);
            if (labels)
            {
                f.Text(Mx.Beside(s0, -Mx.Dir(phi / 2), "S", tl, 0.8), "S", tl);
                f.Text(Mx.Beside(map(bisEnd), Mx.Dir(phi / 2), "w", tl, 0.6), "w", tl, italic: true);
            }
            f.Port("vertex", s0);
            f.Port("P1", map(P1)); f.Port("P2", map(P2)); f.Port("Q", map(Q));
            f.Pt(AnchorRole.Mid, map(r1), "ray1"); f.Pt(AnchorRole.Mid, map(r2), "ray2"); f.Pt(AnchorRole.Mid, map(bisEnd), "bisector");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.NumberInput(o, "math.anglebisector.angle", new I18nText("Angle…", "Winkel…"), Parameters[0], "construction");
            yield return Mx.Toggle(o, "math.anglebisector.arcs", new I18nText("Compass marks", "Zirkelbögen"), "arcs", true, "construction");
            yield return Mx.Toggle(o, "math.anglebisector.labels", new I18nText("Labels", "Beschriftung"), "labels", true, "construction");
        }
    }

    /// <summary>Two parallel lines cut by a transversal with corresponding, alternate or co-interior angles marked.</summary>
    internal sealed class ParallelLinesType : MathBoxType
    {
        public ParallelLinesType() : base("math.lines.parallel", new I18nText("Parallel lines and transversal", "Parallelen mit Querlinie"), MathCategories.Constructions,
            "parallel", "transversal", "corresponding angles", "alternate angles", "parallelen", "stufenwinkel", "wechselwinkel", "nebenwinkel", "nachbarwinkel", "querlinie") { }

        public override Size2 DefaultSize => new(64, 44);

        public static readonly ParamDef Pair = Pm.Choice("pair", "Marked angles", "Markierte Winkel", "corresponding",
            ("corresponding", "Corresponding angles", "Stufenwinkel"), ("alternate", "Alternate angles", "Wechselwinkel"),
            ("cointerior", "Co-interior angles", "Nachbarwinkel (Ergänzungswinkel)"), ("all", "All angles", "Alle Winkel"), ("none", "None", "Keine"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("angle", "Angle of the transversal", "Winkel der Querlinie", 60, 20, 160, "°", 5),
            Pair,
            Pm.Bool("values", "Show values", "Werte anzeigen", false),
            Pm.Bool("labels", "Line names (g, h)", "Geradennamen (g, h)", true),
        };

        /// <summary>The four angles at an intersection: bounding directions (screen) and whether it equals the transversal angle.</summary>
        internal static (Vec2 U1, Vec2 U2, bool Alpha)[] Quadrants(Vec2 up) => new[]
        {
            (new Vec2(1, 0), up, true),     // upper right
            (up, new Vec2(-1, 0), false),   // upper left
            (new Vec2(-1, 0), -up, true),   // lower left
            (-up, new Vec2(1, 0), false),   // lower right
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var ts = tl * 0.85;
            var labels = o.Flag("labels", true);
            var phi = Geo.Clamp(o.Num("angle", 60), 20, 160);
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var left = labels ? 1.0 + Mx.TextW("g", tl) + 0.3 : 0.3;
            double x0 = -hw + left, x1 = hw - 0.3;
            var r = Math.Min(5 * k, Math.Min(o.Size.W, o.Size.H) * 0.12);
            var pad = r + 1.0 + Mx.TextW("180°", ts);
            var up = Mx.Dir(phi);
            var steep = Math.Abs(Math.Tan(Geo.DegToRad(phi)));
            var halfRoom = Math.Max(0.5, Math.Min(x1, -x0) - pad);
            var sep = Math.Max(0.5, Math.Min(o.Size.H * 0.45, 2 * halfRoom * steep));
            var yTop = -sep / 2; var yBot = sep / 2;
            var I1 = up * (sep / 2 / Math.Sin(Geo.DegToRad(phi)));
            var I2 = -I1;
            // Transversal through the centre, clipped to the drawing area.
            double tMin = double.NegativeInfinity, tMax = double.PositiveInfinity;
            void Slab(double p, double d, double lo, double hi)
            {
                if (Math.Abs(d) < 1e-12) return;
                double ta = (lo - p) / d, tb = (hi - p) / d;
                tMin = Math.Max(tMin, Math.Min(ta, tb));
                tMax = Math.Min(tMax, Math.Max(ta, tb));
            }
            Slab(0, up.X, x0, x1);
            Slab(0, up.Y, -hh + 0.3, hh - 0.3);
            var line = Style.Visible;
            f.Line(new Vec2(x0, yTop), new Vec2(x1, yTop), line, MathTags.Main);
            f.Line(new Vec2(x0, yBot), new Vec2(x1, yBot), line, MathTags.Main);
            f.Line(up * tMin, up * tMax, line, MathTags.Main);
            // Parallel marks (chevrons) near the right end.
            foreach (var y in new[] { yTop, yBot })
            {
                var cx = x1 - 3;
                f.Poly(new[] { new Vec2(cx - 1, y - 1), new Vec2(cx, y), new Vec2(cx - 1, y + 1) }, Mx.Mark, tag: MathTags.Mark);
            }
            if (labels)
            {
                f.Text(new Vec2(x0 - 0.8, yTop), "g", tl, italic: true, anchor: TextAnchor.End);
                f.Text(new Vec2(x0 - 0.8, yBot), "h", tl, italic: true, anchor: TextAnchor.End);
            }
            var pair = o.Str("pair", "corresponding");
            var q = Quadrants(up);
            var marks = pair switch
            {
                "corresponding" => new[] { (I1, 0), (I2, 0) },
                "alternate" => new[] { (I1, 2), (I2, 0) },
                "cointerior" => new[] { (I1, 3), (I2, 0) },
                "all" => Enumerable.Range(0, 4).SelectMany(i => new[] { (I1, i), (I2, i) }).ToArray(),
                _ => Array.Empty<(Vec2, int)>(),
            };
            var values = o.Flag("values", false);
            foreach (var (at, i) in marks)
            {
                var (u1, u2, alpha) = q[i];
                Mx.AngleArc(f, at, u1, u2, r);
                if (!alpha) Mx.AngleArc(f, at, u1, u2, Math.Max(0.5, r - 1.0));
                var deg = alpha ? phi : 180 - phi;
                var text = values ? Mx.Num(deg, lang, 1) + "°" : alpha ? "α" : "β";
                var bis = (u1 + u2).Normalized();
                f.Text(Mx.Beside(at + bis * r, bis, text, ts, 0.6), text, ts, italic: !values);
            }
            f.Port("s1", I1);
            f.Port("s2", I2);
            f.Rail(new Vec2(x0, yTop), new Vec2(x1, yTop), "g");
            f.Rail(new Vec2(x0, yBot), new Vec2(x1, yBot), "h");
            f.Rail(up * tMin, up * tMax, "transversal");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var opt in Mx.Choice(o, "math.parallel.pair", new I18nText("Angle pair", "Winkelpaar"), Pair, "angles")) yield return opt;
            yield return Mx.NumberInput(o, "math.parallel.angle", new I18nText("Angle…", "Winkel…"), Parameters[0], "angles");
            yield return Mx.Toggle(o, "math.parallel.values", new I18nText("Show values", "Werte anzeigen"), "values", false, "angles");
        }
    }
}
