using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>Triangle helpers: classic centres.</summary>
    internal static class Tri
    {
        public static double Area(Vec2 a, Vec2 b, Vec2 c) => Math.Abs((b - a).Cross(c - a)) / 2;

        public static (Vec2 Centre, double Radius) Incircle(Vec2 A, Vec2 B, Vec2 C)
        {
            double a = B.DistanceTo(C), b = C.DistanceTo(A), c = A.DistanceTo(B);
            var p = a + b + c;
            if (p < 1e-12) return (A, 0);
            return ((A * a + B * b + C * c) / p, 2 * Area(A, B, C) / p);
        }

        public static (Vec2 Centre, double Radius)? Circumcircle(Vec2 A, Vec2 B, Vec2 C)
        {
            var d = 2 * (A.X * (B.Y - C.Y) + B.X * (C.Y - A.Y) + C.X * (A.Y - B.Y));
            if (Math.Abs(d) < 1e-12) return null;
            double a2 = A.Dot(A), b2 = B.Dot(B), c2 = C.Dot(C);
            var u = new Vec2((a2 * (B.Y - C.Y) + b2 * (C.Y - A.Y) + c2 * (A.Y - B.Y)) / d, (a2 * (C.X - B.X) + b2 * (A.X - C.X) + c2 * (B.X - A.X)) / d);
            return (u, u.DistanceTo(A));
        }

        /// <summary>Foot of the perpendicular from <paramref name="p"/> onto the line a–b.</summary>
        public static Vec2 Foot(Vec2 p, Vec2 a, Vec2 b) => Geo.ProjectOnLine(p, a, b);

        /// <summary>Names of the triangle centres: incentre, circumcentre, centroid, orthocentre (school usage per language).</summary>
        public static string CentreName(string construction, Lang lang) => (construction, lang) switch
        {
            ("incircle", Lang.De) => "W",
            ("incircle", _) => "I",
            ("circumcircle", Lang.De) => "U",
            ("circumcircle", _) => "O",
            ("medians", Lang.De) => "S",
            ("medians", _) => "G",
            _ => "H",
        };

        /// <summary>Name of a circle centre: M in German, O in English.</summary>
        public static string CircleCentre(Lang lang) => lang == Lang.De ? "M" : "O";
    }

    /// <summary>Triangle with labelled vertices, sides and angles and the classic constructions.</summary>
    internal sealed class TriangleType : MathBoxType
    {
        public TriangleType() : base("math.shape.triangle", new I18nText("Triangle", "Dreieck"), MathCategories.Figures,
            "triangle", "dreieck", "right triangle", "rechtwinklig", "isosceles", "gleichschenklig", "equilateral", "gleichseitig",
            "incircle", "inkreis", "circumcircle", "umkreis", "median", "seitenhalbierende", "altitude", "höhe", "schwerpunkt", "centroid") { }

        public override Size2 DefaultSize => new(50, 40);
        public override IReadOnlyList<string> Companions => new[] { "math.angle.mark", "math.point.labelled", "math.circle.thales" };

        public static readonly ParamDef Kind = Pm.Choice("kind", "Kind", "Art", "general",
            ("general", "General", "Allgemein"), ("right", "Right-angled (γ = 90°)", "Rechtwinklig (γ = 90°)"),
            ("isosceles", "Isosceles", "Gleichschenklig"), ("equilateral", "Equilateral", "Gleichseitig"));
        public static readonly ParamDef ConstructionDef = Pm.Choice("construction", "Construction", "Konstruktion", "none",
            ("none", "None", "Keine"), ("incircle", "Incircle (angle bisectors)", "Inkreis (Winkelhalbierende)"),
            ("circumcircle", "Circumcircle (perpendicular bisectors)", "Umkreis (Mittelsenkrechte)"),
            ("medians", "Medians and centroid", "Seitenhalbierende und Schwerpunkt"), ("altitudes", "Altitudes and orthocentre", "Höhen und Höhenschnittpunkt"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Kind, ConstructionDef,
            Pm.Bool("vertexLabels", "Vertex labels (A, B, C)", "Eckpunkte beschriften (A, B, C)", true),
            Pm.Bool("sideLabels", "Side labels (a, b, c)", "Seiten beschriften (a, b, c)", true),
            Pm.Bool("angleMarks", "Angle marks (α, β, γ)", "Winkel markieren (α, β, γ)", false),
        };

        /// <summary>Vertices A, B, C (counter-clockwise on screen) in local coordinates, before constructions shrink them.</summary>
        internal static (Vec2 A, Vec2 B, Vec2 C) Vertices(string kind, Rect r)
        {
            Vec2 V(double fx, double fy) => new(r.X + fx * r.W, r.Y + fy * r.H);
            switch (kind)
            {
                case "right": return (V(1, 1), V(0, 0), V(0, 1)); // right angle at C (bottom left), legs along the box edges
                case "isosceles": return (V(0, 1), V(1, 1), V(0.5, 0));
                case "equilateral":
                    {
                        var h = Math.Sqrt(3) / 2;
                        var pts = new[] { new Vec2(0, h), new Vec2(1, h), new Vec2(0.5, 0) };
                        var (map, _) = Mx.FitUniform(pts, r);
                        return (map(pts[0]), map(pts[1]), map(pts[2]));
                    }
                default: return (V(0, 1), V(1, 1), V(0.3, 0));
            }
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            bool vl = o.Flag("vertexLabels", true), sl = o.Flag("sideLabels", true), am = o.Flag("angleMarks", false);
            var kind = o.Str("kind", "general");
            var construction = o.Str("construction", "none");
            var m = vl || sl ? 1.0 + tl : 0.6;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var (A, B, C) = Vertices(kind, inner);

            // Constructions that reach outside the triangle (circumcircle, altitudes of obtuse triangles) shrink the figure.
            var extent = new List<Vec2> { A, B, C };
            var cc = Tri.Circumcircle(A, B, C);
            if (construction == "circumcircle" && cc is { } c0) extent.AddRange(Mx.CircleBox(c0.Centre, c0.Radius));
            if (construction == "altitudes" && cc is { } c1) extent.Add(A + B + C - c1.Centre * 2);
            var eb = Rect.FromPoints(extent);
            if (eb.W > inner.W + 1e-9 || eb.H > inner.H + 1e-9 || !inner.Contains(eb))
            {
                var s = Math.Min(1, Math.Min(inner.W / Math.Max(eb.W, 1e-9), inner.H / Math.Max(eb.H, 1e-9)));
                Vec2 Map(Vec2 p) => inner.Center + (p - eb.Center) * s;
                (A, B, C) = (Map(A), Map(B), Map(C));
                cc = Tri.Circumcircle(A, B, C);
            }

            f.Poly(new[] { A, B, C }, Mx.Outline, closed: true, tag: MathTags.Outline);
            var shortest = Math.Min(A.DistanceTo(B), Math.Min(B.DistanceTo(C), C.DistanceTo(A)));
            var dotR = Math.Max(0.35, 0.5 * Math.Min(1.5, k));
            var cons = Mx.Construction;
            var ts = tl * 0.85;

            void CentreLabel(Vec2 p, Vec2 dir)
            {
                f.Dot(p, dotR);
                var name = Tri.CentreName(construction, lang);
                f.Text(Mx.Beside(p, dir, name, ts, 0.6), name, ts);
                f.Port("centre", p);
            }

            switch (construction)
            {
                case "incircle":
                    {
                        double a = B.DistanceTo(C), b = C.DistanceTo(A), c = A.DistanceTo(B);
                        if (b + c > 1e-9) f.Line(A, (B * b + C * c) / (b + c), cons, MathTags.Construction);
                        if (c + a > 1e-9) f.Line(B, (C * c + A * a) / (c + a), cons, MathTags.Construction);
                        if (a + b > 1e-9) f.Line(C, (A * a + B * b) / (a + b), cons, MathTags.Construction);
                        var (ic, ir) = Tri.Incircle(A, B, C);
                        f.Circle(ic, ir, cons with { StrokeWidth = 0.35 }, MathTags.Incircle);
                        CentreLabel(ic, new Vec2(0.7, 0.7));
                        break;
                    }
                case "circumcircle" when cc is { } u:
                    {
                        foreach (var (p, q) in new[] { (B, C), (C, A), (A, B) })
                        {
                            var mid = Vec2.Lerp(p, q, 0.5);
                            var n = (q - p).Normalized().Perp;
                            var tU = (u.Centre - mid).Dot(n);
                            var e = 0.12 * u.Radius;
                            f.Line(mid + n * (Math.Min(tU, 0) - e), mid + n * (Math.Max(tU, 0) + e), cons, MathTags.Construction);
                        }
                        f.Circle(u.Centre, u.Radius, cons with { StrokeWidth = 0.35 }, MathTags.Circumcircle);
                        CentreLabel(u.Centre, new Vec2(0.7, 0.7));
                        break;
                    }
                case "medians":
                    {
                        f.Line(A, Vec2.Lerp(B, C, 0.5), cons, MathTags.Construction);
                        f.Line(B, Vec2.Lerp(C, A, 0.5), cons, MathTags.Construction);
                        f.Line(C, Vec2.Lerp(A, B, 0.5), cons, MathTags.Construction);
                        CentreLabel((A + B + C) / 3, new Vec2(0.7, 0.7));
                        break;
                    }
                case "altitudes" when cc is { } u:
                    {
                        var H = A + B + C - u.Centre * 2;
                        var dashed = Mx.Helper with { Stroke = Mx.Blue, StrokeWidth = 0.25 };
                        foreach (var (v, p, q) in new[] { (A, B, C), (B, C, A), (C, A, B) })
                        {
                            var foot = Tri.Foot(v, p, q);
                            if (v.DistanceTo(foot) > 1e-6) f.Line(v, foot, cons, MathTags.Construction);
                            // Foot outside the side: extend the side (dashed) to it.
                            var (_, t) = Geo.ClosestOnSegment(foot, p, q);
                            var onSide = Geo.ClosestOnSegment(foot, p, q).Point;
                            if (onSide.DistanceTo(foot) > 1e-6) f.Line(onSide, foot, dashed, MathTags.Helper);
                            // Orthocentre beyond the altitude segment: extend the altitude (dashed).
                            var hOn = Geo.ClosestOnSegment(H, v, foot).Point;
                            if (hOn.DistanceTo(H) > 1e-6) f.Line(hOn, H, dashed, MathTags.Helper);
                            if (foot.DistanceTo(p) > shortest * 0.08 && foot.DistanceTo(q) > shortest * 0.08 && v.DistanceTo(foot) > 1e-6)
                            {
                                var along = (t < 0.5 ? q - foot : p - foot).Normalized();
                                Mx.RightAngle(f, foot, (v - foot).Normalized(), along, Math.Min(2.5 * k, shortest * 0.08), dot: false, s: cons);
                            }
                        }
                        CentreLabel(H, new Vec2(0.7, -0.7));
                        break;
                    }
            }

            var verts = new[] { A, B, C };
            if (am)
            {
                var greek = new[] { "α", "β", "γ" };
                for (var i = 0; i < 3; i++)
                {
                    Vec2 v = verts[i], p = verts[(i + 1) % 3], q = verts[(i + 2) % 3];
                    Vec2 u1 = (p - v).Normalized(), u2 = (q - v).Normalized();
                    var rr = Math.Min(6 * k, Math.Min(v.DistanceTo(p), v.DistanceTo(q)) * 0.3);
                    var bis = (u1 + u2).Normalized();
                    var (_, sweep) = Mx.SmallSweep(u1, u2);
                    var right = kind == "right" && i == 2 || Math.Abs(Math.Abs(Geo.RadToDeg(sweep)) - 90) < 0.05;
                    if (right) Mx.RightAngle(f, v, u1, u2, rr * 0.7);
                    else
                    {
                        Mx.AngleArc(f, v, u1, u2, rr);
                        if (bis != Vec2.Zero) f.Text(v + bis * (rr * 0.55 + Mx.HalfExtentAlong(greek[i], ts * 0.8, bis) + 0.4), greek[i], ts * 0.8, italic: true);
                    }
                }
            }
            if (sl)
            {
                var sides = new[] { ("a", B, C, A), ("b", C, A, B), ("c", A, B, C) };
                foreach (var (name, p, q, opp) in sides)
                {
                    // The perpendicular bisectors of the circumcircle construction leave the sides at their midpoints: label beside them.
                    var mid = Vec2.Lerp(p, q, construction == "circumcircle" ? 0.3 : 0.5);
                    var n = (q - p).Normalized().Perp;
                    if (n.Dot(opp - mid) > 0) n = -n;
                    f.Text(Mx.Beside(mid, n, name, tl, 0.8), name, tl, italic: true);
                }
            }
            var names = new[] { "A", "B", "C" };
            for (var i = 0; i < 3; i++)
            {
                Vec2 v = verts[i], p = verts[(i + 1) % 3], q = verts[(i + 2) % 3];
                if (vl)
                {
                    var dir = -((p - v).Normalized() + (q - v).Normalized());
                    if (dir.Length < 1e-6) dir = v - (A + B + C) / 3;
                    f.Text(Mx.Beside(v, dir, names[i], tl, 0.8), names[i], tl);
                }
                f.Port(names[i], v);
            }
            f.Pt(AnchorRole.Mid, Vec2.Lerp(B, C, 0.5), "ma");
            f.Pt(AnchorRole.Mid, Vec2.Lerp(C, A, 0.5), "mb");
            f.Pt(AnchorRole.Mid, Vec2.Lerp(A, B, 0.5), "mc");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Cycle(o, "math.triangle.kind", new I18nText("Next kind", "Nächste Art"), Kind, "triangle");
            yield return Mx.Cycle(o, "math.triangle.construction", new I18nText("Next construction", "Nächste Konstruktion"), ConstructionDef, "triangle");
            yield return Mx.Toggle(o, "math.triangle.vertices", new I18nText("Vertex labels", "Eckpunkte beschriften"), "vertexLabels", true, "labels");
            yield return Mx.Toggle(o, "math.triangle.sides", new I18nText("Side labels", "Seiten beschriften"), "sideLabels", true, "labels");
            yield return Mx.Toggle(o, "math.triangle.angles", new I18nText("Angle marks", "Winkel markieren"), "angleMarks", false, "labels");
        }
    }

    /// <summary>Regular polygon with n sides (stays regular under free scaling), optional circumcircle and incircle.</summary>
    internal sealed class RegularPolygonType : MathBoxType
    {
        public RegularPolygonType() : base("math.shape.polygon", new I18nText("Regular polygon", "Regelmäßiges Vieleck"), MathCategories.Figures,
            "polygon", "regular", "hexagon", "pentagon", "square", "vieleck", "polygon", "sechseck", "fünfeck", "n-eck", "regelmäßig") { }

        public override Size2 DefaultSize => new(40, 40);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Int("sides", "Sides", "Ecken", 6, 3, 12),
            Pm.Bool("circumcircle", "Circumcircle", "Umkreis", false),
            Pm.Bool("incircle", "Incircle", "Inkreis", false),
            Pm.Bool("centre", "Centre", "Mittelpunkt", false),
            Pm.Bool("labels", "Vertex labels", "Eckpunkte beschriften", false),
        };

        /// <summary>Unit vertices (circumradius 1) counter-clockwise on screen from the bottom-left one; the bottom edge is horizontal.</summary>
        internal static List<Vec2> Unit(int n)
            => Enumerable.Range(0, n).Select(i => Geo.DegToRad(90 + 180.0 / n - i * 360.0 / n)).Select(a => new Vec2(Math.Cos(a), Math.Sin(a))).ToList();

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var n = Math.Max(3, Math.Min(12, o.Int("sides", 6)));
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var labels = o.Flag("labels", false);
            var circum = o.Flag("circumcircle", false);
            var m = labels ? 1.0 + tl : 0.4;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var unit = Unit(n);
            var fitPts = circum ? unit.Concat(Mx.CircleBox(Vec2.Zero, 1)).ToList() : unit;
            var (map, s) = Mx.FitUniform(fitPts, inner);
            var c = map(Vec2.Zero);
            var v = unit.Select(map).ToList();
            f.Poly(v, Mx.Outline, closed: true, tag: MathTags.Outline);
            if (circum) f.Circle(c, s, Mx.Construction with { StrokeWidth = 0.35 }, MathTags.Circumcircle);
            if (o.Flag("incircle", false)) f.Circle(c, s * Math.Cos(Math.PI / n), Mx.Construction with { StrokeWidth = 0.35 }, MathTags.Incircle);
            if (o.Flag("centre", false))
            {
                f.Dot(c, Math.Max(0.35, 0.5 * Math.Min(1.5, k)));
                var name = Tri.CircleCentre(lang);
                f.Text(Mx.Beside(c, new Vec2(0, 1), name, tl * 0.85, 0.6), name, tl * 0.85);
            }
            for (var i = 0; i < n; i++)
            {
                if (labels)
                {
                    var name = ((char)('A' + i)).ToString();
                    f.Text(Mx.Beside(v[i], v[i] - c, name, tl, 0.8), name, tl);
                }
                f.Port("v" + i.ToString(CultureInfo.InvariantCulture), v[i]);
                f.Pt(AnchorRole.Mid, Vec2.Lerp(v[i], v[(i + 1) % n], 0.5), "e" + i.ToString(CultureInfo.InvariantCulture));
            }
            f.Port("centre", c);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var n = o.Int("sides", 6);
            yield return new ContextAction("math.polygon.more", new I18nText("More sides", "Mehr Ecken"), IconIds.CountMore,
                x => x.WithParam("sides", Math.Min(12, x.Int("sides", 6) + 1))) { Group = "sides", ParamKeys = new[] { "sides" }, IsEnabled = n < 12 };
            yield return new ContextAction("math.polygon.fewer", new I18nText("Fewer sides", "Weniger Ecken"), IconIds.CountFewer,
                x => x.WithParam("sides", Math.Max(3, x.Int("sides", 6) - 1))) { Group = "sides", ParamKeys = new[] { "sides" }, IsEnabled = n > 3 };
            yield return Mx.Toggle(o, "math.polygon.circumcircle", new I18nText("Circumcircle", "Umkreis"), "circumcircle", false, "circles");
            yield return Mx.Toggle(o, "math.polygon.incircle", new I18nText("Incircle", "Inkreis"), "incircle", false, "circles");
            yield return Mx.Toggle(o, "math.polygon.labels", new I18nText("Vertex labels", "Eckpunkte beschriften"), "labels", false, "labels");
        }
    }

    /// <summary>Circle with centre, radius and/or diameter (stays round under free scaling).</summary>
    internal sealed class CircleType : MathBoxType
    {
        public CircleType() : base("math.circle.basic", new I18nText("Circle with radius", "Kreis mit Radius"), MathCategories.Figures,
            "circle", "radius", "diameter", "centre", "center", "kreis", "radius", "durchmesser", "mittelpunkt") { }

        public override Size2 DefaultSize => new(40, 40);
        public override bool PreferUniformScale => true;

        public static readonly ParamDef Show = Pm.Choice("show", "Show", "Anzeigen", "radius",
            ("radius", "Radius", "Radius"), ("diameter", "Diameter", "Durchmesser"), ("both", "Radius and diameter", "Radius und Durchmesser"), ("none", "Circle only", "Nur Kreis"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Show,
            Pm.Num("angle", "Direction of the radius", "Richtung des Radius", 45, -360, 360, "°", 15),
            Pm.Bool("centre", "Centre", "Mittelpunkt", true),
            Pm.Bool("labels", "Labels (r, d, centre)", "Beschriftung (r, d, Mittelpunkt)", true),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var labels = o.Flag("labels", true);
            var R = Math.Max(0.5, Math.Min(o.Size.W, o.Size.H) / 2 - 0.4);
            var c = Vec2.Zero;
            var show = o.Str("show", "radius");
            var angle = o.Num("angle", 45);
            f.Circle(c, R, Mx.Outline, MathTags.Outline);
            var line = Style.ThinLine with { StrokeWidth = 0.35 };
            if (show is "radius" or "both")
            {
                var d = Mx.Dir(angle);
                f.Line(c, c + d * R, line, MathTags.Construction);
                if (labels) f.Text(Mx.Beside(c + d * (R / 2), Mx.UpperNormal(d), "r", tl, 0.6), "r", tl, italic: true);
                f.Pt(AnchorRole.Mid, c + d * R, "rend");
            }
            if (show is "diameter" or "both")
            {
                var d = Mx.Dir(show == "both" ? 0 : angle);
                f.Line(c - d * R, c + d * R, line, MathTags.Construction);
                if (labels) f.Text(Mx.Beside(c - d * (R / 2), -Mx.UpperNormal(d), "d", tl, 0.6), "d", tl, italic: true);
            }
            if (o.Flag("centre", true))
            {
                f.Dot(c, Math.Max(0.35, 0.5 * Math.Min(1.5, k)));
                if (labels)
                {
                    var name = Tri.CircleCentre(lang);
                    f.Text(Mx.Beside(c, Mx.Dir(angle + 225), name, tl, 0.6), name, tl);
                }
            }
            f.Port("centre", c);
            for (var i = 0; i < 4; i++) f.Pt(AnchorRole.Quadrant, c + Mx.Dir(90 * i) * R, "q" + i.ToString(CultureInfo.InvariantCulture));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Cycle(o, "math.circle.show", new I18nText("Radius / diameter", "Radius / Durchmesser"), Show, "circle");
            yield return Mx.Toggle(o, "math.circle.centre", new I18nText("Centre", "Mittelpunkt"), "centre", true, "circle");
            yield return Mx.Toggle(o, "math.circle.labels", new I18nText("Labels", "Beschriftung"), "labels", true, "circle");
        }
    }

    /// <summary>Tangent to a circle at a point, with the radius to the point of contact and the right angle.</summary>
    internal sealed class TangentType : MathBoxType
    {
        public TangentType() : base("math.circle.tangent", new I18nText("Tangent to a circle", "Tangente an einen Kreis"), MathCategories.Figures,
            "tangent", "circle", "tangente", "kreis", "berührpunkt", "point of contact") { }

        public override Size2 DefaultSize => new(56, 44);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("angle", "Position of the point of contact", "Lage des Berührpunkts", 60, -360, 360, "°", 15),
            Pm.Bool("radius", "Radius and right angle", "Radius und rechter Winkel", true),
            Pm.Bool("labels", "Labels", "Beschriftung", true),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var labels = o.Flag("labels", true);
            var phi = o.Num("angle", 60);
            var T = Mx.Dir(phi);
            var t = Mx.Dir(phi + 90);
            const double L = 1.35;
            var pts = Mx.CircleBox(Vec2.Zero, 1).Concat(new[] { T - t * L, T + t * L }).ToList();
            var m = labels ? 1.0 + tl : 0.4;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var (map, R) = Mx.FitUniform(pts, inner);
            var M = map(Vec2.Zero);
            var P = map(T);
            Vec2 e0 = map(T - t * L), e1 = map(T + t * L);
            f.Circle(M, R, Mx.Outline, MathTags.Outline);
            f.Line(e0, e1, Mx.Colored(Style.Visible, Mx.Red), MathTags.Main);
            var dotR = Math.Max(0.35, 0.5 * Math.Min(1.5, k));
            if (o.Flag("radius", true))
            {
                f.Line(M, P, Style.ThinLine with { StrokeWidth = 0.35 }, MathTags.Construction);
                Mx.RightAngle(f, P, (M - P).Normalized(), t, Math.Min(3 * k, R * 0.2));
                if (labels) f.Text(Mx.Beside(Vec2.Lerp(M, P, 0.5), -t, "r", tl, 0.6), "r", tl, italic: true);
            }
            f.Dot(M, dotR);
            f.Dot(P, dotR);
            if (labels)
            {
                var centre = Tri.CircleCentre(lang);
                f.Text(Mx.Beside(M, -T, centre, tl, 0.6), centre, tl);
                var contact = lang == Lang.De ? "B" : "T";
                f.Text(Mx.Beside(P, T - t * 0.6, contact, tl, 0.8), contact, tl);
                f.Text(Mx.Beside(e1, T, "t", tl, 0.6), "t", tl, italic: true);
            }
            f.Port("centre", M);
            f.Port("contact", P);
            f.Pt(AnchorRole.Mid, e0, "t0");
            f.Pt(AnchorRole.Mid, e1, "t1");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var def = Parameters[0];
            yield return Mx.NumberInput(o, "math.tangent.angle", new I18nText("Contact point angle…", "Winkel des Berührpunkts…"), def, "tangent");
            yield return new ContextAction("math.tangent.turn", new I18nText("Move the contact point", "Berührpunkt verschieben"), IconIds.AngleNext,
                x => x.WithParam("angle", Geo.NormDeg(x.Num("angle", 60) + 30))) { Group = "tangent", ParamKeys = new[] { "angle" } };
            yield return Mx.Toggle(o, "math.tangent.radius", new I18nText("Radius and right angle", "Radius und rechter Winkel"), "radius", true, "tangent");
        }
    }

    /// <summary>Thales' theorem: a right triangle over the diameter of a (semi)circle, the right angle at C.</summary>
    internal sealed class ThalesType : MathBoxType
    {
        public ThalesType() : base("math.circle.thales", new I18nText("Thales circle", "Thaleskreis"), MathCategories.Figures,
            "thales", "semicircle", "right angle", "thaleskreis", "halbkreis", "rechter winkel", "satz des thales") { }

        public override Size2 DefaultSize => new(60, 40);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("angle", "Position of C on the circle", "Lage von C auf dem Kreis", 115, 1, 179, "°", 5),
            Pm.Bool("full", "Full circle", "Vollkreis", false),
            Pm.Bool("centre", "Centre", "Mittelpunkt", true),
            Pm.Bool("labels", "Labels", "Beschriftung", true),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var labels = o.Flag("labels", true);
            var full = o.Flag("full", false);
            var phi = Geo.Clamp(o.Num("angle", 115), 1, 179);
            var unitC = Mx.Dir(phi);
            var pts = new List<Vec2> { new(-1, 0), new(1, 0), new(0, -1) };
            if (full) pts.Add(new Vec2(0, 1));
            var m = labels ? 1.0 + tl : 0.4;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var (map, R) = Mx.FitUniform(pts, inner);
            Vec2 M = map(Vec2.Zero), A = map(new Vec2(-1, 0)), B = map(new Vec2(1, 0)), C = map(unitC);
            var circle = Mx.Construction with { StrokeWidth = 0.35 };
            if (full) f.Circle(M, R, circle, MathTags.Construction);
            else f.Arc(M, R, Math.PI, Math.PI, circle, MathTags.Construction);
            f.Poly(new[] { A, B, C }, Mx.Outline, closed: true, tag: MathTags.Outline);
            Mx.RightAngle(f, C, (A - C).Normalized(), (B - C).Normalized(), Math.Min(3.5 * k, R * 0.15));
            if (o.Flag("centre", true))
            {
                f.Dot(M, Math.Max(0.35, 0.5 * Math.Min(1.5, k)));
                if (labels)
                {
                    var name = Tri.CircleCentre(lang);
                    f.Text(Mx.Beside(M, new Vec2(0, 1), name, tl, 0.6), name, tl);
                }
            }
            if (labels)
            {
                f.Text(Mx.Beside(A, new Vec2(-1, 0.3), "A", tl, 0.8), "A", tl);
                f.Text(Mx.Beside(B, new Vec2(1, 0.3), "B", tl, 0.8), "B", tl);
                f.Text(Mx.Beside(C, C - M, "C", tl, 0.8), "C", tl);
            }
            f.Port("A", A); f.Port("B", B); f.Port("C", C); f.Port("centre", M);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.NumberInput(o, "math.thales.angle", new I18nText("Position of C…", "Lage von C…"), Parameters[0], "thales");
            yield return new ContextAction("math.thales.move", new I18nText("Move C on the circle", "C auf Kreis verschieben"), IconIds.AngleNext,
                x => x.WithParam("angle", x.Num("angle", 115) + 20 > 175 ? 20 : x.Num("angle", 115) + 20)) { Group = "thales", ParamKeys = new[] { "angle" } };
            yield return Mx.Toggle(o, "math.thales.full", new I18nText("Full circle", "Vollkreis"), "full", false, "thales");
        }
    }

    /// <summary>Unit circle with an angle, sine (red), cosine (blue) and optionally tangent (green).</summary>
    internal sealed class UnitCircleType : MathBoxType
    {
        public UnitCircleType() : base("math.circle.unit", new I18nText("Unit circle", "Einheitskreis"), MathCategories.Figures,
            "unit circle", "sine", "cosine", "tangent", "trigonometry", "einheitskreis", "sinus", "kosinus", "cosinus", "tangens", "trigonometrie") { }

        public override Size2 DefaultSize => new(64, 64);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("angle", "Angle", "Winkel", 40, -360, 360, "°", 5),
            Pm.Text("label", "Angle name", "Name des Winkels", "α"),
            Pm.Bool("tan", "Tangent segment", "Tangensstrecke", false),
            Pm.Bool("values", "Show values", "Werte anzeigen", false),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            double tl = Mx.LabelSize * k, ts = Mx.NumberSize * k;
            var theta = o.Num("angle", 40);
            var name = o.Str("label", "α");
            if (name.Length == 0) name = Mx.Num(theta, lang, 1) + "°";
            var values = o.Flag("values", false);
            const double ext = 1.3;
            var m = 1.2 + tl;
            var rad = Geo.DegToRad(theta);
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            string Caption(string fn, double value) => values ? fn + " " + name + " = " + Mx.Num(value, lang, 2) : fn + " " + name;
            var showTan = o.Flag("tan", false) && Math.Abs(cos) > 0.05;
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            // Reserve room beside the circle for the sine and tangent captions (they sit next to their vertical segments).
            double mL = m, mR = m;
            Func<Vec2, Vec2> map = p => p;
            double R = 1;
            for (var iter = 0; iter < 4; iter++)
            {
                var inner = new Rect(-hw + mL, -hh + m, Math.Max(1, o.Size.W - mL - mR), Math.Max(1, o.Size.H - 2 * m));
                (map, R) = Mx.FitUniform(new[] { new Vec2(-ext, -ext), new Vec2(ext, ext) }, inner);
                var ox = map(Vec2.Zero).X;
                double defR = 0, defL = 0;
                if (Math.Abs(sin) > 0.02)
                {
                    var w = Mx.TextW(Caption("sin", sin), tl * 0.85);
                    var x0 = ox + cos * R;
                    if (cos >= 0 && !showTan) defR = w + 0.2 - (hw - 0.3 - x0 - 1.0);
                    else defL = w + 0.2 - (x0 - 1.0 - (-hw + 0.3));
                }
                if (showTan)
                {
                    var w = Mx.TextW(values ? "tan " + name + " = " + Mx.Num(sin / cos, lang, 2) : "tan " + name, tl * 0.85);
                    defR = Math.Max(defR, w + 0.2 - (hw - 0.3 - (ox + R) - 1.0));
                }
                if (defR <= 1e-6 && defL <= 1e-6) break;
                mR += Math.Max(0, defR);
                mL += Math.Max(0, defL);
            }
            var O = map(Vec2.Zero);
            Vec2 U(double x, double y) => map(new Vec2(x, -y));

            f.Circle(O, R, Mx.Outline, MathTags.Outline);
            var xEnd = U(ext, 0); var yEnd = U(0, ext);
            f.Line(U(-ext, 0), xEnd, Mx.Axis, MathTags.Axis);
            f.Line(U(0, -ext), yEnd, Mx.Axis, MathTags.Axis);
            f.Text(new Vec2(xEnd.X + 0.6, xEnd.Y), "x", tl, italic: true, anchor: TextAnchor.Start);
            f.Text(new Vec2(yEnd.X, yEnd.Y - 0.4 - tl / 2), "y", tl, italic: true);
            foreach (var v in new[] { 1.0, -1.0 })
            {
                var px = U(v, 0); var py = U(0, v);
                f.Line(px + new Vec2(0, -0.7), px + new Vec2(0, 0.7), Mx.AxisPlain, MathTags.Tick);
                f.Line(py + new Vec2(-0.7, 0), py + new Vec2(0.7, 0), Mx.AxisPlain, MathTags.Tick);
                var label = Mx.Num(v, lang, 0);
                f.Text(new Vec2(px.X + (v > 0 ? 1.0 : -1.0) * (0.4 + Mx.TextW(label, ts) / 2), px.Y + 0.8 + ts / 2), label, ts, tag: MathTags.Number);
                f.Text(new Vec2(py.X - 1.0, py.Y + (v > 0 ? -1 : 1) * (0.4 + ts / 2)), label, ts, anchor: TextAnchor.End, tag: MathTags.Number);
            }

            var P = U(cos, sin);
            var F = U(cos, 0);
            f.Line(O, P, Style.Visible, MathTags.Main);
            var dotR = Math.Max(0.35, 0.5 * Math.Min(1.5, k));
            // Angle arc counter-clockwise from the positive x axis (negative sweep on screen).
            var arcR = R * 0.22;
            var sweep = -Geo.DegToRad(Geo.NormDeg(theta));
            if (Math.Abs(sweep) > 1e-6) f.Arc(O, arcR, 0, sweep, Mx.Mark, MathTags.Mark);
            var bis = Mx.Dir(Geo.NormDeg(theta) / 2);
            f.Text(O + bis * (arcR + 0.6 + Mx.HalfExtentAlong(name, tl * 0.85, bis)), name, tl * 0.85, italic: name.Length <= 2);

            var thick = Style.Visible with { StrokeWidth = 0.8 };
            double minX = -o.Size.W / 2 + 0.3, maxX = o.Size.W / 2 - 0.3;
            // A label beside a vertical segment at x: on the preferred side if it fits, else on the other side, else smaller.
            void SideText(double x, double y, string text, double size, bool right, string color)
            {
                var w = Mx.TextW(text, size);
                double roomR = maxX - (x + 1.0), roomL = x - 1.0 - minX;
                if ((right ? roomR : roomL) < w - 0.05 && (right ? roomL : roomR) >= w) right = !right;
                var room = right ? roomR : roomL;
                if (room < w) size *= Math.Max(0.05, room) / w;
                f.Text(new Vec2(right ? x + 1.0 : x - 1.0, y), text, size, anchor: right ? TextAnchor.Start : TextAnchor.End, color: color);
            }
            if (Math.Abs(cos) > 0.02)
            {
                f.Line(O, F, thick with { Stroke = Mx.Blue }, MathTags.Cosine);
                var text = Caption("cos", cos);
                var below = sin >= 0;
                f.Text(new Vec2((O.X + F.X) / 2, O.Y + (below ? 1 : -1) * (1.0 + tl * 0.85 / 2)), text, tl * 0.85, color: Mx.Blue);
            }
            if (Math.Abs(sin) > 0.02)
            {
                f.Line(F, P, thick with { Stroke = Mx.Red }, MathTags.Sine);
                SideText(F.X, (F.Y + P.Y) / 2, Caption("sin", sin), tl * 0.85, cos >= 0 && !showTan, Mx.Red);
                f.Line(P, new Vec2(O.X, P.Y), Mx.Helper with { StrokeWidth = 0.25 }, MathTags.Helper);
            }
            if (showTan)
            {
                var tan = Geo.Clamp(sin / cos, -ext, ext);
                var T0 = U(1, 0); var T = U(1, tan);
                f.Line(T0, T, thick with { Stroke = Mx.Green }, MathTags.Tangent);
                var far = O.DistanceTo(T) > O.DistanceTo(P) ? T : P;
                var near = far == T ? P : T;
                if (cos < 0) f.Line(O, T, Mx.Helper with { StrokeWidth = 0.25 }, MathTags.Helper);
                else if (far.DistanceTo(near) > 1e-6) f.Line(near, far, Mx.Helper with { StrokeWidth = 0.25 }, MathTags.Helper);
                var text = values ? "tan " + name + " = " + Mx.Num(sin / cos, lang, 2) : "tan " + name;
                SideText(T0.X, (T0.Y + T.Y) / 2, text, tl * 0.85, true, Mx.Green);
            }
            f.Dot(P, dotR);
            f.Text(Mx.Beside(P, P - O, "P", tl, 0.8), "P", tl);
            f.Port("origin", O);
            f.Port("P", P);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.NumberInput(o, "math.unit.angle", new I18nText("Angle…", "Winkel…"), Parameters[0], "unit");
            yield return new ContextAction("math.unit.turn", new I18nText("Turn by 15°", "Um 15° drehen"), IconIds.AngleNext,
                x => x.WithParam("angle", Geo.NormDeg(x.Num("angle", 40) + 15))) { Group = "unit", ParamKeys = new[] { "angle" } };
            yield return Mx.Toggle(o, "math.unit.tan", new I18nText("Tangent segment", "Tangensstrecke"), "tan", false, "unit");
            yield return Mx.Toggle(o, "math.unit.values", new I18nText("Show values", "Werte anzeigen"), "values", false, "unit");
        }
    }
}
