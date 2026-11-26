using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Oblique parallel projection of school solids (German "Schrägbild"): the front plane is drawn in true shape, depth
    /// recedes at 45° to the upper right, shortened by the factor q (cabinet projection q = ½, cavalier projection q = 1).
    /// A point (x right, y up, z away from the viewer) is drawn at (x + q·cos45°·z, −(y + q·sin45°·z)) in page millimetres.
    /// </summary>
    internal readonly struct Oblique
    {
        public Oblique(double q)
        {
            Q = q;
            Qc = q * Math.Cos(Math.PI / 4);
            Qs = q * Math.Sin(Math.PI / 4);
        }

        public double Q { get; }
        /// <summary>Horizontal and vertical page offset per unit of depth.</summary>
        public double Qc { get; }
        public double Qs { get; }

        public Vec2 P(double x, double y, double z) => new(x + Qc * z, -(y + Qs * z));

        /// <summary>Direction towards the viewer: a face is visible when its outward normal points along it.</summary>
        public (double X, double Y, double Z) ToViewer => (Qc, Qs, -1);

        public static Oblique Of(SketchObject o) => new(o.Str("projection", "cabinet") == "cavalier" ? 1.0 : 0.5);
    }

    /// <summary>
    /// A school solid in oblique view (cube/cuboid, cylinder, cone, sphere, square pyramid) with hidden edges dashed
    /// (<see cref="LineRole.Hidden"/>). The solid fills its box: free (also non-uniform) scaling changes its proportions (a
    /// stretched cube becomes a cuboid with a square base), never the parameters. The sphere is drawn conventionally (outline
    /// circle with the equator as an ellipse), because its exact oblique outline is a skewed ellipse.
    /// </summary>
    internal sealed class SolidType : MathBoxType
    {
        public enum Shape { Cube, Cylinder, Cone, Sphere, Pyramid }

        private readonly Shape _shape;
        private readonly Size2 _size;

        public SolidType(string id, I18nText name, Shape shape, Size2 size, params string[] keywords)
            : base(id, name, MathCategories.Solids, keywords.Concat(new[] { "solid", "körper", "oblique", "schrägbild", "cavalier", "kavalierprojektion", "cabinet", "kabinettprojektion", "3d", "geometry", "geometrie" }).ToArray())
        {
            _shape = shape;
            _size = size;
        }

        public override Size2 DefaultSize => _size;
        public override bool PreferUniformScale => _shape == Shape.Sphere;

        public static readonly ParamDef Projection = Pm.Choice("projection", "Projection", "Projektion", "cabinet",
            ("cabinet", "Cabinet (depth ½, 45°)", "Kabinettprojektion (Tiefe ½, 45°)"),
            ("cavalier", "Cavalier (depth 1, 45°)", "Kavalierprojektion (Tiefe 1, 45°)"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Projection,
            Pm.Bool("hidden", "Hidden edges (dashed)", "Verdeckte Kanten (gestrichelt)", true),
            Pm.Bool("labels", "Labels (a, r, h)", "Beschriftung (a, r, h)", true),
        };

        /// <summary>Visible edges (ISO 128 visible line).</summary>
        private static readonly Style Edge = Mx.Outline;
        /// <summary>Hidden edges (ISO 128 hidden line, dashed).</summary>
        private static readonly Style HiddenEdge = Style.Hidden with { StrokeWidth = 0.35 };
        /// <summary>Interior dimension lines (height, radius): thin dashed.</summary>
        private static readonly Style Dim = Style.Hidden with { StrokeWidth = 0.25 };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var k = K(o);
            var tl = Mx.LabelSize * k * 0.9;
            var labels = o.Flag("labels", true);
            var m = labels ? tl * 1.3 + 0.6 : 0.4;
            var inner = new Rect(-o.Size.W / 2 + m, -o.Size.H / 2 + m, Math.Max(1, o.Size.W - 2 * m), Math.Max(1, o.Size.H - 2 * m));
            var pr = Oblique.Of(o);
            var hidden = o.Flag("hidden", true);
            var sk = new Sketch(f, hidden, labels, tl);
            switch (_shape)
            {
                case Shape.Cube: Cube(sk, pr, inner); break;
                case Shape.Cylinder: Cylinder(sk, pr, inner); break;
                case Shape.Cone: Cone(sk, pr, inner); break;
                case Shape.Sphere: Sphere(sk, pr, inner); break;
                default: Pyramid(sk, pr, inner); break;
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Mx.Choice(o, "math.solid.projection", new I18nText("Projection", "Projektion"), Projection, "solid")) yield return a;
            yield return Mx.Toggle(o, "math.solid.hidden", new I18nText("Hidden edges", "Verdeckte Kanten"), "hidden", true, "solid");
            yield return Mx.Toggle(o, "math.solid.labels", new I18nText("Labels", "Beschriftung"), "labels", true, "labels");
        }

        // ------------------------------------------------------------------ layout helpers

        /// <summary>Drawing helper: collects lines in solid coordinates and shifts them into the box at the end.</summary>
        private sealed class Sketch
        {
            private readonly Fig _f;
            private readonly List<(Vec2 A, Vec2 B, bool Visible, string Tag, Style? Style)> _lines = new();
            private readonly List<(List<Vec2> Pts, bool Visible, bool Closed, string Tag, Style? Style)> _curves = new();
            private readonly List<(Vec2 At, Vec2 Dir, string Text)> _labels = new();
            private readonly List<(AnchorRole Role, Vec2 P, string Id)> _anchors = new();

            public Sketch(Fig f, bool hidden, bool labels, double textSize) { _f = f; ShowHidden = hidden; Labels = labels; TextSize = textSize; }

            public bool ShowHidden { get; }
            public bool Labels { get; }
            public double TextSize { get; }

            public void Line(Vec2 a, Vec2 b, bool visible, string? tag = null, Style? style = null) => _lines.Add((a, b, visible, tag ?? (visible ? "edge" : "hidden"), style));
            public void Curve(List<Vec2> pts, bool visible, bool closed = false, string? tag = null, Style? style = null) => _curves.Add((pts, visible, closed, tag ?? (visible ? "edge" : "hidden"), style));
            public void Label(Vec2 at, Vec2 dir, string text) { if (Labels) _labels.Add((at, dir, text)); }
            public void Anchor(AnchorRole role, Vec2 p, string id) => _anchors.Add((role, p, id));

            /// <summary>Draws everything centred in <paramref name="area"/> (the drawing's bounds centre on the area centre).</summary>
            public void Flush(Rect area, Vec2? centreOverride = null)
            {
                var pts = _lines.SelectMany(l => new[] { l.A, l.B }).Concat(_curves.SelectMany(c => c.Pts)).ToList();
                if (pts.Count == 0) return;
                var b = Rect.FromPoints(pts);
                var shift = area.Center - (centreOverride ?? b.Center);
                foreach (var c in _curves)
                {
                    if (!c.Visible && !ShowHidden) continue;
                    _f.Poly(c.Pts.Select(p => p + shift), c.Style ?? (c.Visible ? Edge : HiddenEdge), c.Closed, c.Tag);
                }
                foreach (var l in _lines)
                {
                    if (!l.Visible && !ShowHidden) continue;
                    _f.Line(l.A + shift, l.B + shift, l.Style ?? (l.Visible ? Edge : HiddenEdge), l.Tag);
                }
                foreach (var (at, dir, text) in _labels)
                    _f.Text(Mx.Beside(at + shift, dir, text, TextSize, 0.6), text, TextSize, italic: true);
                foreach (var (role, p, id) in _anchors)
                {
                    if (role == AnchorRole.Port) _f.Port(id, p + shift);
                    else _f.Pt(role, p + shift, id);
                }
            }
        }

        /// <summary>
        /// The base size a and height h of a solid whose drawing is a·<paramref name="wa"/> wide and a·<paramref name="ha"/> + h high,
        /// filling <paramref name="area"/>; h is kept at least <paramref name="hmin"/>·a (a then shrinks).
        /// </summary>
        private static (double A, double H) Dims(Rect area, double wa, double ha, double hmin)
        {
            var a = area.W / wa;
            var h = area.H - a * ha;
            if (h < hmin * a)
            {
                a = area.H / (ha + hmin);
                h = hmin * a;
            }
            return (Math.Max(0.05, a), Math.Max(0.05, h));
        }

        /// <summary>
        /// Edges of a convex polyhedron with their visibility: a face is visible when its outward normal points towards the
        /// viewer of the oblique projection; an edge is visible when one of its faces is.
        /// </summary>
        private static void Polyhedron(Sketch s, Oblique pr, IReadOnlyList<(double X, double Y, double Z)> v, IReadOnlyList<int[]> faces)
        {
            double cx = v.Average(p => p.X), cy = v.Average(p => p.Y), cz = v.Average(p => p.Z);
            var (tx, ty, tz) = pr.ToViewer;
            var edges = new Dictionary<(int, int), bool>();
            foreach (var face in faces)
            {
                var p0 = v[face[0]]; var p1 = v[face[1]]; var p2 = v[face[2]];
                double ux = p1.X - p0.X, uy = p1.Y - p0.Y, uz = p1.Z - p0.Z;
                double wx = p2.X - p0.X, wy = p2.Y - p0.Y, wz = p2.Z - p0.Z;
                double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                double fx = face.Average(i => v[i].X) - cx, fy = face.Average(i => v[i].Y) - cy, fz = face.Average(i => v[i].Z) - cz;
                if (nx * fx + ny * fy + nz * fz < 0) { nx = -nx; ny = -ny; nz = -nz; }
                var visible = nx * tx + ny * ty + nz * tz > 1e-9;
                for (var i = 0; i < face.Length; i++)
                {
                    int a = face[i], b = face[(i + 1) % face.Length];
                    var key = a < b ? (a, b) : (b, a);
                    edges[key] = edges.TryGetValue(key, out var was) ? was || visible : visible;
                }
            }
            // Hidden edges first so that visible ones are drawn on top.
            foreach (var e in edges.OrderBy(e => e.Value))
                s.Line(pr.P(v[e.Key.Item1].X, v[e.Key.Item1].Y, v[e.Key.Item1].Z), pr.P(v[e.Key.Item2].X, v[e.Key.Item2].Y, v[e.Key.Item2].Z), e.Value);
            for (var i = 0; i < v.Count; i++) s.Anchor(AnchorRole.Port, pr.P(v[i].X, v[i].Y, v[i].Z), "v" + i.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Points of a horizontal circle (radius r at height y, centred on the y axis) from θ0 over the sweep.</summary>
        private static List<Vec2> Circle3D(Oblique pr, double r, double y, double t0, double sweep, int n = 96)
        {
            var steps = Math.Max(2, (int)Math.Ceiling(n * Math.Abs(sweep) / (2 * Math.PI)));
            var list = new List<Vec2>(steps + 1);
            for (var i = 0; i <= steps; i++)
            {
                var t = t0 + sweep * i / steps;
                list.Add(pr.P(r * Math.Cos(t), y, r * Math.Sin(t)));
            }
            return list;
        }

        // ------------------------------------------------------------------ solids

        private static void Cube(Sketch s, Oblique pr, Rect inner)
        {
            // Square base a x a (depth = width), height h: a square box gives a cube in both projections.
            var (a, h) = Dims(inner, 1 + pr.Qc, pr.Qs, 0.15);
            var v = new List<(double, double, double)>();
            for (var i = 0; i < 8; i++) v.Add(((i & 1) * a, ((i >> 1) & 1) * h, ((i >> 2) & 1) * a));
            int I(int x, int y, int z) => x | (y << 1) | (z << 2);
            var faces = new[]
            {
                new[] { I(0, 0, 0), I(1, 0, 0), I(1, 1, 0), I(0, 1, 0) }, new[] { I(0, 0, 1), I(1, 0, 1), I(1, 1, 1), I(0, 1, 1) },
                new[] { I(0, 0, 0), I(0, 1, 0), I(0, 1, 1), I(0, 0, 1) }, new[] { I(1, 0, 0), I(1, 1, 0), I(1, 1, 1), I(1, 0, 1) },
                new[] { I(0, 0, 0), I(1, 0, 0), I(1, 0, 1), I(0, 0, 1) }, new[] { I(0, 1, 0), I(1, 1, 0), I(1, 1, 1), I(0, 1, 1) },
            };
            Polyhedron(s, pr, v, faces);
            s.Label(pr.P(a / 2, 0, 0), new Vec2(0, 1), "a");
            s.Label(pr.P(0, h / 2, 0), new Vec2(-1, 0), Math.Abs(h - a) <= 1e-6 * a ? "a" : "h");
            s.Anchor(AnchorRole.Center, pr.P(a / 2, h / 2, a / 2), "centre");
            s.Flush(inner);
        }

        private static void Pyramid(Sketch s, Oblique pr, Rect inner)
        {
            // Square base a x a, apex above the base centre; h at least q·sin45°·a, so the apex is the top of the drawing.
            var (a, h) = Dims(inner, 1 + pr.Qc, pr.Qs / 2, Math.Max(0.5, pr.Qs));
            var v = new List<(double, double, double)> { (0, 0, 0), (a, 0, 0), (a, 0, a), (0, 0, a), (a / 2, h, a / 2) };
            var faces = new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 1, 4 }, new[] { 1, 2, 4 }, new[] { 2, 3, 4 }, new[] { 3, 0, 4 } };
            Polyhedron(s, pr, v, faces);
            var foot = pr.P(a / 2, 0, a / 2);
            var apex = pr.P(a / 2, h, a / 2);
            if (s.Labels) s.Line(foot, apex, false, MathTags.Helper, Dim);
            s.Label(pr.P(a / 2, 0, 0), new Vec2(0, 1), "a");
            s.Label(Vec2.Lerp(foot, apex, 0.45), new Vec2(1, 0), "h");
            s.Anchor(AnchorRole.Apex, apex, "apex");
            s.Flush(inner);
        }

        private static void Cylinder(Sketch s, Oblique pr, Rect inner)
        {
            var e = Math.Sqrt(1 + pr.Qc * pr.Qc);
            var (r, h) = Dims(inner, 2 * e, 2 * pr.Qs, 0.4);
            // Silhouette generators where the projected circle is widest (tan θ = q·cos45°); the front half of the base is visible.
            var t = Math.Atan(pr.Qc);
            s.Curve(Circle3D(pr, r, 0, t + Math.PI, Math.PI), true, tag: "edge");
            s.Curve(Circle3D(pr, r, 0, t, Math.PI), false);
            s.Curve(Circle3D(pr, r, h, 0, 2 * Math.PI), true, closed: true);
            s.Line(pr.P(r * Math.Cos(t), 0, r * Math.Sin(t)), pr.P(r * Math.Cos(t), h, r * Math.Sin(t)), true);
            s.Line(pr.P(-r * Math.Cos(t), 0, -r * Math.Sin(t)), pr.P(-r * Math.Cos(t), h, -r * Math.Sin(t)), true);
            var top = pr.P(0, h, 0);
            if (s.Labels) s.Line(top, pr.P(r, h, 0), true, MathTags.Construction, Mx.Thin);
            s.Label(Vec2.Lerp(top, pr.P(r, h, 0), 0.5), new Vec2(0, -1), "r");
            var right = Vec2.Lerp(pr.P(r * Math.Cos(t), 0, r * Math.Sin(t)), pr.P(r * Math.Cos(t), h, r * Math.Sin(t)), 0.5);
            s.Label(right, new Vec2(1, 0), "h");
            s.Anchor(AnchorRole.Center, top, "top");
            s.Anchor(AnchorRole.Center, pr.P(0, 0, 0), "bottom");
            s.Flush(inner);
        }

        private static void Cone(Sketch s, Oblique pr, Rect inner)
        {
            var e = Math.Sqrt(1 + pr.Qc * pr.Qc);
            var (r, h) = Dims(inner, 2 * e, pr.Qs, Math.Max(0.6, 2.2 * pr.Qs));
            var apex = pr.P(0, h, 0);
            Vec2 E(double th) => pr.P(r * Math.Cos(th), 0, r * Math.Sin(th));
            // Tangent points from the apex to the projected base: (E(θ) − apex) × E'(θ) = 0.
            double F(double th)
            {
                var d = new Vec2(-r * Math.Sin(th) + pr.Qc * r * Math.Cos(th), -pr.Qs * r * Math.Cos(th));
                return (E(th) - apex).Cross(d);
            }
            var roots = new List<double>();
            const int n = 360;
            for (var i = 0; i < n && roots.Count < 2; i++)
            {
                double t0 = 2 * Math.PI * i / n, t1 = 2 * Math.PI * (i + 1) / n;
                double f0 = F(t0), f1 = F(t1);
                if (f0 == 0) { roots.Add(t0); continue; }
                if (f0 * f1 >= 0) continue;
                for (var it = 0; it < 60; it++)
                {
                    var tm = (t0 + t1) / 2;
                    var fm = F(tm);
                    if (f0 * fm <= 0) t1 = tm; else { t0 = tm; f0 = fm; }
                }
                roots.Add((t0 + t1) / 2);
            }
            if (roots.Count == 2)
            {
                double a1 = roots[0], a2 = roots[1];
                // The arc from a1 to a2 (increasing θ) is the front one if it passes θ = 3π/2 (nearest to the viewer).
                var front = a1 < 1.5 * Math.PI && 1.5 * Math.PI < a2;
                s.Curve(Circle3D(pr, r, 0, a1, a2 - a1), front, tag: front ? "edge" : "hidden");
                s.Curve(Circle3D(pr, r, 0, a2, 2 * Math.PI - (a2 - a1)), !front, tag: !front ? "edge" : "hidden");
                s.Line(E(a1), apex, true);
                s.Line(E(a2), apex, true);
            }
            else s.Curve(Circle3D(pr, r, 0, 0, 2 * Math.PI), true, closed: true);
            var foot = pr.P(0, 0, 0);
            if (s.Labels)
            {
                s.Line(foot, apex, false, MathTags.Helper, Dim);
                s.Line(foot, E(0), false, MathTags.Helper, Dim);
            }
            s.Label(Vec2.Lerp(foot, E(0), 0.5), new Vec2(0, 1), "r");
            s.Label(Vec2.Lerp(foot, apex, 0.55), new Vec2(1, 0), "h");
            s.Anchor(AnchorRole.Apex, apex, "apex");
            s.Anchor(AnchorRole.Center, foot, "bottom");
            s.Flush(inner);
        }

        private static void Sphere(Sketch s, Oblique pr, Rect inner)
        {
            var R = Math.Max(0.5, Math.Min(inner.W, inner.H) / 2);
            // Outline circle (centred on the origin) and the equator as an axis-parallel ellipse with the projection's depth factor.
            var ry = R * pr.Qs;
            var outline = Enumerable.Range(0, 97).Select(i => new Vec2(R * Math.Cos(2 * Math.PI * i / 96), R * Math.Sin(2 * Math.PI * i / 96))).ToList();
            s.Curve(outline, true, closed: true, tag: MathTags.Outline);
            var frontEq = Enumerable.Range(0, 49).Select(i => new Vec2(R * Math.Cos(Math.PI * i / 48), ry * Math.Sin(Math.PI * i / 48))).ToList();
            var backEq = frontEq.Select(p => new Vec2(p.X, -p.Y)).ToList();
            s.Curve(frontEq, true, tag: "equator", style: Mx.Thin with { StrokeWidth = 0.35 });
            s.Curve(backEq, false, tag: "hidden", style: Dim);
            if (s.Labels) s.Line(Vec2.Zero, new Vec2(R, 0), false, MathTags.Helper, Dim);
            s.Label(new Vec2(R / 2, 0), new Vec2(0, -1), "r");
            s.Anchor(AnchorRole.Port, Vec2.Zero, "centre");
            for (var i = 0; i < 4; i++)
                s.Anchor(AnchorRole.Quadrant, new Vec2(R * Math.Cos(i * Math.PI / 2), -R * Math.Sin(i * Math.PI / 2)), "q" + i.ToString(CultureInfo.InvariantCulture));
            s.Flush(inner, Vec2.Zero);
        }
    }
}
