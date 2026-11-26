using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Base of the live constructions on points: the figure is drawn from its points, which dock on the ports, grid points and
    /// surfaces of other figures (<see cref="MathPointType.DocksOnSurface"/>) and follow them (<see cref="Attachments"/>):
    /// a perpendicular bisector docked on the vertices A and B of a triangle moves with the triangle.
    /// </summary>
    internal abstract class FreeFigureType : MathPointType
    {
        protected FreeFigureType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        protected static ParamDef Labels => Pm.Bool("labels", "Labels", "Beschriftung", true);
        protected static ParamDef TextSize => Pm.Num("textSize", "Text height", "Schrifthöhe", Mx.LabelSize, 1, 20, "mm", 0.5);

        protected static double Ts(SketchObject o) => Math.Max(1, o.Num("textSize", Mx.LabelSize));
        protected static double DotR(double ts) => Math.Max(0.35, 0.5 * Math.Min(1.5, ts / Mx.LabelSize));

        /// <summary>A point name placed outside the point along <paramref name="dir"/>.</summary>
        protected static void PointName(Fig f, Vec2 p, Vec2 dir, string name, double ts, bool italic = false)
        {
            if (name.Length == 0) return;
            f.Text(Mx.Beside(p, dir, name, ts, 0.7), name, ts, italic: italic);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Toggle(o, Id + ".labels", new I18nText("Labels", "Beschriftung"), "labels", true, "labels");
        }
    }

    // ===================================================================================== triangle through points

    /// <summary>
    /// Triangle through three points with the classic constructions (incircle, circumcircle, medians, altitudes) drawn live
    /// from the vertices, angle marks with the measured angles and side labels. The vertices dock on other figures.
    /// </summary>
    internal sealed class FreeTriangleType : FreeFigureType
    {
        public FreeTriangleType() : base("math.free.triangle", new I18nText("Triangle through three points", "Dreieck durch drei Punkte"), MathCategories.Figures,
            "triangle", "dreieck", "points", "punkte", "vertices", "eckpunkte", "free", "frei", "live", "dynamic", "dynamisch",
            "incircle", "inkreis", "circumcircle", "umkreis", "inscribed circle", "circumscribed circle", "median", "altitude", "höhe", "schwerpunkt") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-22, 14), new Vec2(22, 14), new Vec2(-6, -16) };
        protected override int MinPoints => 3;
        public override IReadOnlyList<string> Companions => new[] { "math.free.bisector", "math.angle.mark", "math.point.labelled" };

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            TriangleType.ConstructionDef,
            Pm.Bool("vertexLabels", "Vertex labels (A, B, C)", "Eckpunkte beschriften (A, B, C)", true),
            Pm.Bool("sideLabels", "Side labels (a, b, c)", "Seiten beschriften (a, b, c)", true),
            Pm.Bool("angleMarks", "Angle marks (α, β, γ)", "Winkel markieren (α, β, γ)", false),
            Pm.Bool("values", "Measured angles (α = 63.4°)", "Gemessene Winkel (α = 63,4°)", false),
            TextSize,
        };

        /// <summary>Interior angles (degrees) at A, B and C of the drawn triangle.</summary>
        internal static double[] Angles(Vec2 A, Vec2 B, Vec2 C)
        {
            double Ang(Vec2 v, Vec2 p, Vec2 q)
            {
                var u1 = (p - v).Normalized(); var u2 = (q - v).Normalized();
                return Geo.RadToDeg(Math.Acos(Math.Max(-1, Math.Min(1, u1.Dot(u2)))));
            }
            return new[] { Ang(A, B, C), Ang(B, C, A), Ang(C, A, B) };
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            Vec2 A = p[0], B = p[1], C = p[2];
            var ts = Ts(o);
            var k = ts / Mx.LabelSize;
            var names = new[] { "A", "B", "C" };
            for (var i = 0; i < 3; i++) f.Port(names[i], p[i]);
            if (Tri.Area(A, B, C) < 1e-6)
            {
                f.Poly(new[] { A, B, C }, Mx.Outline, tag: MathTags.Main);
                return;
            }
            f.Poly(new[] { A, B, C }, Mx.Outline, closed: true, tag: MathTags.Outline);
            Tri.Construct(f, A, B, C, o.Str("construction", "none"), lang, k, ts * 0.85);
            var verts = new[] { A, B, C };
            var deg = Angles(A, B, C);
            bool marks = o.Flag("angleMarks", false), values = o.Flag("values", false);
            if (marks || values)
                for (var i = 0; i < 3; i++)
                {
                    Vec2 v = verts[i], q1 = verts[(i + 1) % 3], q2 = verts[(i + 2) % 3];
                    Vec2 u1 = (q1 - v).Normalized(), u2 = (q2 - v).Normalized();
                    var rr = Math.Min(6 * k, Math.Min(v.DistanceTo(q1), v.DistanceTo(q2)) * 0.3);
                    if (Math.Abs(deg[i] - 90) < 0.05) Mx.RightAngle(f, v, u1, u2, rr * 0.7);
                    else Mx.AngleArc(f, v, u1, u2, rr);
                    var text = values ? TriValues.Greek[i] + " = " + Mx.Num(deg[i], lang, 1) + "°" : TriValues.Greek[i];
                    var bis = (u1 + u2).Normalized();
                    var at = Mx.WedgeDistance(text, ts * 0.7, Geo.DegToRad(deg[i]) / 2, rr * 0.55 + Mx.HalfExtentAlong(text, ts * 0.7, bis) + 0.4);
                    if (bis != Vec2.Zero) f.Text(v + bis * at, text, ts * 0.7, italic: !values);
                }
            if (o.Flag("sideLabels", true))
                foreach (var (name, s1, s2, opp) in new[] { ("a", B, C, A), ("b", C, A, B), ("c", A, B, C) })
                {
                    var mid = Vec2.Lerp(s1, s2, o.Str("construction", "none") == "circumcircle" ? 0.3 : 0.5);
                    var n = (s2 - s1).Normalized().Perp;
                    if (n.Dot(opp - mid) > 0) n = -n;
                    PointName(f, mid, n, name, ts, italic: true);
                }
            for (var i = 0; i < 3; i++)
            {
                var v = verts[i];
                f.Dot(v, DotR(ts));
                if (o.Flag("vertexLabels", true))
                {
                    var dir = -((verts[(i + 1) % 3] - v).Normalized() + (verts[(i + 2) % 3] - v).Normalized());
                    if (dir.Length < 1e-6) dir = v - (A + B + C) / 3;
                    PointName(f, v, dir, names[i], ts);
                }
            }
            f.Pt(AnchorRole.Mid, Vec2.Lerp(B, C, 0.5), "ma");
            f.Pt(AnchorRole.Mid, Vec2.Lerp(C, A, 0.5), "mb");
            f.Pt(AnchorRole.Mid, Vec2.Lerp(A, B, 0.5), "mc");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var opt in Mx.Choice(o, "math.free.triangle.construction", new I18nText("Construction", "Konstruktion"), TriangleType.ConstructionDef, "triangle")) yield return opt;
            yield return Mx.Toggle(o, "math.free.triangle.vertices", new I18nText("Vertex labels", "Eckpunkte beschriften"), "vertexLabels", true, "labels");
            yield return Mx.Toggle(o, "math.free.triangle.sides", new I18nText("Side labels", "Seiten beschriften"), "sideLabels", true, "labels");
            yield return Mx.Toggle(o, "math.free.triangle.angles", new I18nText("Angle marks", "Winkel markieren"), "angleMarks", false, "labels");
            yield return Mx.Toggle(o, "math.free.triangle.values", new I18nText("Measured angles", "Gemessene Winkel"), "values", false, "labels");
        }
    }

    // ===================================================================================== perpendicular bisector

    /// <summary>Perpendicular bisector of the segment between two points, with compass marks, midpoint M and right angle.</summary>
    internal sealed class FreeBisectorType : FreeFigureType
    {
        public FreeBisectorType() : base("math.free.bisector", new I18nText("Perpendicular bisector of two points", "Mittelsenkrechte zu zwei Punkten"), MathCategories.Constructions,
            "perpendicular bisector", "midpoint", "mittelsenkrechte", "mittelpunkt", "strecke", "segment", "points", "punkte", "live", "dynamic", "dynamisch", "zirkel", "compass") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 6), new Vec2(20, -6) };

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Bool("segment", "Draw the segment AB", "Strecke AB zeichnen", true),
            Pm.Bool("arcs", "Compass marks", "Zirkelbögen", true),
            Labels, TextSize,
        };

        /// <summary>Compass radius as a share of |AB| (the arcs meet at S1 and S2 on the bisector).</summary>
        internal const double Rho = 0.75;

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            Vec2 A = p[0], B = p[p.Count - 1];
            var ts = Ts(o);
            var len = A.DistanceTo(B);
            if (len < 1e-6) { f.Dot(A, DotR(ts)); return; }
            var M = Vec2.Lerp(A, B, 0.5);
            var d = (B - A) / len;
            var n = d.Perp;
            var r = Rho * len;
            var hh = Math.Sqrt(r * r - len * len / 4);
            Vec2 S1 = M - n * hh, S2 = M + n * hh;
            var ext = hh * 1.25;
            Vec2 e1 = M - n * ext, e2 = M + n * ext;
            if (o.Flag("segment", true)) f.Line(A, B, Mx.Outline, MathTags.Main);
            if (o.Flag("arcs", true))
                foreach (var c in new[] { A, B })
                    foreach (var s in new[] { S1, S2 })
                        f.Poly(Compass.MarkPoints(c, s, 14), Mx.Construction, tag: MathTags.Construction);
            f.Line(e1, e2, Mx.Colored(Style.Visible, Mx.Red), MathTags.Construction);
            Mx.RightAngle(f, M, d, -n, Math.Min(3 * ts / Mx.LabelSize, len * 0.08));
            var dot = DotR(ts);
            f.Dot(A, dot); f.Dot(B, dot); f.Dot(M, dot);
            if (o.Flag("labels", true))
            {
                PointName(f, A, -d, "A", ts);
                PointName(f, B, d, "B", ts);
                PointName(f, M, (n - d).Normalized(), "M", ts);
                PointName(f, e1, -n, "m", ts, italic: true);
            }
            f.Port("M", M); f.Port("S1", S1); f.Port("S2", S2);
            f.Rail(e1, e2, "m");
        }
    }

    // ===================================================================================== angle bisector

    /// <summary>Angle bisector of the angle at a vertex between two points (point on the first ray, vertex, point on the second ray).</summary>
    internal sealed class FreeAngleBisectorType : FreeFigureType
    {
        public FreeAngleBisectorType() : base("math.free.anglebisector", new I18nText("Angle bisector of three points", "Winkelhalbierende zu drei Punkten"), MathCategories.Constructions,
            "angle bisector", "winkelhalbierende", "halbieren", "bisect", "points", "punkte", "live", "dynamic", "dynamisch", "zirkel", "compass") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(22, 12), new Vec2(-18, 12), new Vec2(4, -18) };
        protected override int MinPoints => 3;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Bool("arcs", "Compass marks", "Zirkelbögen", true),
            Pm.Bool("values", "Measured half angles", "Gemessene halbe Winkel", false),
            Labels, TextSize,
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            Vec2 P1 = p[0], S = p[1], P2 = p[2];
            var ts = Ts(o);
            var l1 = P1.DistanceTo(S); var l2 = P2.DistanceTo(S);
            if (l1 < 1e-6 || l2 < 1e-6) { f.Dot(S, DotR(ts)); return; }
            Vec2 u1 = (P1 - S) / l1, u2 = (P2 - S) / l2;
            f.Line(S, P1, Mx.Outline, MathTags.Main);
            f.Line(S, P2, Mx.Outline, MathTags.Main);
            var bis = (u1 + u2).Normalized();
            var (start, sweep) = Mx.SmallSweep(u1, u2);
            var half = Math.Abs(sweep) / 2;
            if (bis == Vec2.Zero) bis = u1.Perp; // straight angle
            var reach = Math.Max(l1, l2);
            var bisEnd = S + bis * reach;
            var rho = Math.Min(l1, l2) * 0.45;
            Vec2 Q1 = S + u1 * rho, Q2 = S + u2 * rho;
            var halfChord = rho * Math.Sin(half);
            var rho2 = Math.Max(rho * 0.6, halfChord * 1.25);
            var Q = S + bis * (rho * Math.Cos(half) + Math.Sqrt(Math.Max(0, rho2 * rho2 - halfChord * halfChord)));
            if (o.Flag("arcs", true))
            {
                f.Arc(S, rho, start - 0.12 * Math.Sign(sweep), sweep + 0.24 * Math.Sign(sweep), Mx.Construction, MathTags.Construction);
                f.Poly(Compass.MarkPoints(Q1, Q, 14), Mx.Construction, tag: MathTags.Construction);
                f.Poly(Compass.MarkPoints(Q2, Q, 14), Mx.Construction, tag: MathTags.Construction);
            }
            f.Line(S, bisEnd, Mx.Colored(Style.Visible, Mx.Red), MathTags.Construction);
            // Equal halves: an arc with a small tick in each half.
            var mr = Math.Min(rho * 0.55, 8 * ts / Mx.LabelSize);
            foreach (var (a0, sw) in new[] { (start, sweep / 2), (start + sweep / 2, sweep / 2) })
            {
                f.Arc(S, mr, a0, sw, Mx.Mark, MathTags.Mark);
                var md = new Vec2(Math.Cos(a0 + sw / 2), Math.Sin(a0 + sw / 2));
                f.Line(S + md * (mr - 0.8), S + md * (mr + 0.8), Mx.Mark, MathTags.Mark);
            }
            f.Dot(S, DotR(ts));
            if (o.Flag("labels", true))
            {
                PointName(f, S, -bis, "S", ts);
                PointName(f, bisEnd, bis, "w", ts, italic: true);
            }
            if (o.Flag("values", false))
            {
                var text = Mx.Num(Geo.RadToDeg(half), lang, 1) + "°";
                foreach (var a in new[] { start + sweep / 4, start + 3 * sweep / 4 })
                {
                    var dir = new Vec2(Math.Cos(a), Math.Sin(a));
                    f.Text(S + dir * (mr + 1.2 + Mx.HalfExtentAlong(text, ts * 0.7, dir)), text, ts * 0.7);
                }
            }
            f.Port("vertex", S); f.Port("Q", Q);
            f.Rail(S, bisEnd, "w");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Toggle(o, "math.free.anglebisector.arcs", new I18nText("Compass marks", "Zirkelbögen"), "arcs", true, "construction");
            yield return Mx.Toggle(o, "math.free.anglebisector.values", new I18nText("Measured half angles", "Gemessene halbe Winkel"), "values", false, "construction");
            foreach (var a in base.Actions(o, lang)) yield return a;
        }
    }

    // ===================================================================================== tangents from a point

    /// <summary>
    /// Tangents from a point P to a circle (points: centre M, a point on the circle, P), constructed with the Thales circle
    /// over MP: the points of contact T₁, T₂, the radii with their right angles and both tangents.
    /// </summary>
    internal sealed class FreeTangentsType : FreeFigureType
    {
        public FreeTangentsType() : base("math.free.tangents", new I18nText("Tangents from a point to a circle", "Tangenten von einem Punkt an einen Kreis"), MathCategories.Constructions,
            "tangent", "tangents", "tangente", "tangenten", "point outside", "außenpunkt", "thales", "thaleskreis", "circle", "kreis", "berührpunkt", "point of contact", "live", "dynamisch") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-14, 0), new Vec2(-14, -12), new Vec2(24, 0) };
        protected override int MinPoints => 3;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Bool("thales", "Thales circle over MP", "Thaleskreis über MP", true),
            Pm.Bool("radii", "Radii and right angles", "Radien und rechte Winkel", true),
            Labels, TextSize,
        };

        /// <summary>Points of contact of the tangents from <paramref name="P"/> to the circle (centre M, radius r), or null for P inside.</summary>
        internal static (Vec2 T1, Vec2 T2)? Contacts(Vec2 M, double r, Vec2 P)
        {
            var d = M.DistanceTo(P);
            if (d <= r * (1 + 1e-9) || r < 1e-9) return null;
            var u = (P - M) / d;
            var along = r * r / d;
            var h = r * Math.Sqrt(1 - r * r / (d * d));
            var foot = M + u * along;
            return (foot + u.Perp * h, foot - u.Perp * h);
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            Vec2 M = p[0], P = p[2];
            var r = M.DistanceTo(p[1]);
            var ts = Ts(o);
            var dot = DotR(ts);
            var labels = o.Flag("labels", true);
            f.Port("centre", M);
            if (r < 1e-6) { f.Dot(M, dot); return; }
            f.Circle(M, r, Mx.Outline, MathTags.Outline);
            f.Dot(M, dot); f.Dot(P, dot);
            if (labels)
            {
                PointName(f, M, M - P, Tri.CircleCentre(lang), ts);
                PointName(f, P, P - M, "P", ts);
            }
            if (Contacts(M, r, P) is not var (T1, T2)) return;
            var mid = Vec2.Lerp(M, P, 0.5);
            if (o.Flag("thales", true))
            {
                f.Circle(mid, M.DistanceTo(P) / 2, Mx.Construction, MathTags.Construction);
                f.Line(M, P, Mx.Construction, MathTags.Construction);
            }
            var red = Mx.Colored(Style.Visible, Mx.Red);
            foreach (var T in new[] { T1, T2 })
            {
                var dir = (T - P).Normalized();
                var end = T + dir * Math.Min(r, P.DistanceTo(T)) * 0.6;
                f.Line(P, end, red, MathTags.Tangent);
                if (o.Flag("radii", true))
                {
                    f.Line(M, T, Mx.Construction, MathTags.Construction);
                    Mx.RightAngle(f, T, (M - T).Normalized(), -dir, Math.Min(2.5 * ts / Mx.LabelSize, r * 0.25));
                }
                f.Dot(T, dot);
                f.Rail(P, end, T == T1 ? "t1" : "t2");
            }
            if (labels)
            {
                PointName(f, T1, T1 - M, lang == Lang.De ? "B₁" : "T₁", ts);
                PointName(f, T2, T2 - M, lang == Lang.De ? "B₂" : "T₂", ts);
            }
            f.Port("T1", T1); f.Port("T2", T2);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Toggle(o, "math.free.tangents.thales", new I18nText("Thales circle", "Thaleskreis"), "thales", true, "construction");
            yield return Mx.Toggle(o, "math.free.tangents.radii", new I18nText("Radii and right angles", "Radien und rechte Winkel"), "radii", true, "construction");
            foreach (var a in base.Actions(o, lang)) yield return a;
        }
    }

    // ===================================================================================== polygon with area and centroid

    /// <summary>
    /// Polygon through any number of points with its area, perimeter and centroid (area centre), measured from the drawing at a
    /// scale 1 : n (default 1:1, so the area is the area on the page). Values A and u (read-only) and a label "A = 12.5 cm²".
    /// </summary>
    internal sealed class FreePolygonType : FreeFigureType, IValueType, IValueLinks
    {
        public FreePolygonType() : base("math.free.polygon", new I18nText("Polygon with area and centroid", "Vieleck mit Fläche und Schwerpunkt"), MathCategories.Figures,
            "polygon", "vieleck", "area", "fläche", "flächeninhalt", "centroid", "schwerpunkt", "perimeter", "umfang", "quadrilateral", "viereck", "trapezoid", "trapez",
            "points", "punkte", "shoelace", "gaußsche trapezformel") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-22, 14), new Vec2(20, 14), new Vec2(14, -12), new Vec2(-12, -16) };
        protected override int MinPoints => 3;
        public override bool InsertablePoints => true;
        public override bool IsClosed(SketchObject o) => true;

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Bool("area", "Show the area", "Flächeninhalt anzeigen", true),
            Pm.Bool("centroid", "Centroid", "Schwerpunkt", true),
            Pm.Bool("vertexLabels", "Vertex labels (A, B, C …)", "Eckpunkte beschriften (A, B, C …)", true),
            Pm.Num("scale", "Scale 1 :", "Maßstab 1 :", 1, 0.001, 1e6, null, 1, advanced: true),
            TextSize,
            ValueLabels.ShowValues,
        };

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly VarDef[] Vars =
        {
            new("A", T("Area from the drawing", "Flächeninhalt aus der Zeichnung"), T("A", "A"), Quantities.Area) { Unit = "cm²", Param = "areaValue", Min = 0, Max = 1e12, ReadOnly = true },
            new("u", T("Perimeter from the drawing", "Umfang aus der Zeichnung"), T("u", "u"), Quantities.Length) { Unit = "cm", Param = "perimeter", Min = 0, Max = 1e9, ReadOnly = true },
        };

        public IReadOnlyList<VarDef> Variables => Vars;
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();

        internal static double Scale(SketchObject o) => Math.Max(1e-9, o.Num("scale", 1));

        /// <summary>Area (m²) of the polygon at the scale (shoelace formula; a self-intersecting outline counts its loops with sign).</summary>
        internal static double AreaM2(IReadOnlyList<Vec2> p, double scale) => Math.Abs(Geo.PolygonArea(p)) * scale * scale / 1e6;

        internal static double PerimeterM(IReadOnlyList<Vec2> p, double scale)
        {
            double len = 0;
            for (var i = 0; i < p.Count; i++) len += p[i].DistanceTo(p[(i + 1) % p.Count]);
            return len * scale / 1000;
        }

        /// <summary>Area centroid (vertex mean for a degenerate outline).</summary>
        internal static Vec2 Centroid(IReadOnlyList<Vec2> p)
        {
            double a = 0, cx = 0, cy = 0;
            for (var i = 0; i < p.Count; i++)
            {
                var q = p[i]; var r = p[(i + 1) % p.Count];
                var c = q.X * r.Y - r.X * q.Y;
                a += c; cx += (q.X + r.X) * c; cy += (q.Y + r.Y) * c;
            }
            if (Math.Abs(a) < 1e-9) return new Vec2(p.Average(v => v.X), p.Average(v => v.Y));
            return new Vec2(cx / (3 * a), cy / (3 * a));
        }

        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 3) yield break;
            var s = Scale(o);
            var area = AreaM2(p, s);
            var per = PerimeterM(p, s);
            yield return BoundRelation.Local(new Relation("math.polygon.area", T("Area from the drawing (shoelace formula)", "Flächeninhalt aus der Zeichnung (Gaußsche Trapezformel)"),
                "{A} = ½·|Σ (xᵢ·yᵢ₊₁ − xᵢ₊₁·yᵢ)|", "A").Solve("A", "½·|Σ (xᵢ·yᵢ₊₁ − xᵢ₊₁·yᵢ)|", _ => area), o.Id);
            yield return BoundRelation.Local(new Relation("math.polygon.perimeter", T("Perimeter from the drawing", "Umfang aus der Zeichnung"),
                "{u} = Σ |PᵢPᵢ₊₁|", "u").Solve("u", "Σ |PᵢPᵢ₊₁|", _ => per), o.Id);
        }

        /// <summary>The area text in cm² (decimal comma in German).</summary>
        internal static string AreaText(IReadOnlyList<Vec2> p, double scale, Lang lang)
            => "A = " + Mx.Num(AreaM2(p, scale) * 1e4, lang, 1) + " cm²";

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            var ts = Ts(o);
            f.Poly(p, Mx.Outline, closed: true, tag: MathTags.Outline);
            var c = Centroid(p);
            var dot = DotR(ts);
            if (o.Flag("centroid", true))
            {
                f.Dot(c, dot * 1.2);
                f.Text(Mx.Beside(c, new Vec2(0.7, -0.7), Tri.CentreName("medians", lang), ts, 0.5), Tri.CentreName("medians", lang), ts);
            }
            if (o.Flag("area", true))
            {
                var text = AreaText(p, Scale(o), lang);
                f.Text(c + new Vec2(0, ts * 1.1), text, ts * 0.8);
            }
            var labels = o.Flag("vertexLabels", true);
            for (var i = 0; i < p.Count; i++)
            {
                f.Dot(p[i], dot);
                var id = "v" + i.ToString(CultureInfo.InvariantCulture);
                if (labels)
                {
                    var name = i < 26 ? ((char)('A' + i)).ToString() : "P" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    var dir = p[i] - c;
                    PointName(f, p[i], dir.Length < 1e-9 ? new Vec2(0, -1) : dir, name, ts);
                }
                f.Pt(AnchorRole.Mid, Vec2.Lerp(p[i], p[(i + 1) % p.Count], 0.5), "m" + id);
            }
            f.Port("centre", c);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.Toggle(o, "math.free.polygon.area", new I18nText("Show the area", "Flächeninhalt anzeigen"), "area", true, "labels");
            yield return Mx.Toggle(o, "math.free.polygon.centroid", new I18nText("Centroid", "Schwerpunkt"), "centroid", true, "labels");
            yield return Mx.Toggle(o, "math.free.polygon.vertices", new I18nText("Vertex labels", "Eckpunkte beschriften"), "vertexLabels", true, "labels");
        }
    }
}
