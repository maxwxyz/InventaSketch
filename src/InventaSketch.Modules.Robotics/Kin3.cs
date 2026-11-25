using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>3D vector in model millimetres (world: x forward/right, y away from the side-view observer, z up).</summary>
    internal readonly record struct V3(double X, double Y, double Z)
    {
        public static readonly V3 Zero = new(0, 0, 0);
        public static readonly V3 UnitX = new(1, 0, 0);
        public static readonly V3 UnitY = new(0, 1, 0);
        public static readonly V3 UnitZ = new(0, 0, 1);
        public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator -(V3 a) => new(-a.X, -a.Y, -a.Z);
        public static V3 operator *(V3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static V3 operator *(double s, V3 a) => new(a.X * s, a.Y * s, a.Z * s);
        public double Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public V3 Cross(V3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized() { var l = Length; return l < 1e-12 ? Zero : new V3(X / l, Y / l, Z / l); }
        public double DistanceTo(V3 b) => (this - b).Length;
    }

    /// <summary>3x3 rotation matrix (columns are the rotated unit axes).</summary>
    internal readonly struct M3
    {
        public readonly double A, B, C, D, E, F, G, H, I; // row-major: [A B C; D E F; G H I]

        public M3(double a, double b, double c, double d, double e, double f, double g, double h, double i)
        {
            A = a; B = b; C = c; D = d; E = e; F = f; G = g; H = h; I = i;
        }

        public static readonly M3 Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

        /// <summary>Right-handed rotation by <paramref name="deg"/> about the unit axis <paramref name="k"/> (Rodrigues).</summary>
        public static M3 Rot(V3 k, double deg)
        {
            k = k.Normalized();
            var t = Geo.DegToRad(deg);
            double c = Math.Cos(t), s = Math.Sin(t), v = 1 - c;
            return new M3(
                k.X * k.X * v + c, k.X * k.Y * v - k.Z * s, k.X * k.Z * v + k.Y * s,
                k.X * k.Y * v + k.Z * s, k.Y * k.Y * v + c, k.Y * k.Z * v - k.X * s,
                k.X * k.Z * v - k.Y * s, k.Y * k.Z * v + k.X * s, k.Z * k.Z * v + c);
        }

        public static M3 operator *(M3 m, M3 n) => new(
            m.A * n.A + m.B * n.D + m.C * n.G, m.A * n.B + m.B * n.E + m.C * n.H, m.A * n.C + m.B * n.F + m.C * n.I,
            m.D * n.A + m.E * n.D + m.F * n.G, m.D * n.B + m.E * n.E + m.F * n.H, m.D * n.C + m.E * n.F + m.F * n.I,
            m.G * n.A + m.H * n.D + m.I * n.G, m.G * n.B + m.H * n.E + m.I * n.H, m.G * n.C + m.H * n.F + m.I * n.I);

        public static V3 operator *(M3 m, V3 v) => new(m.A * v.X + m.B * v.Y + m.C * v.Z, m.D * v.X + m.E * v.Y + m.F * v.Z, m.G * v.X + m.H * v.Y + m.I * v.Z);
    }

    /// <summary>
    /// A coordinate frame (orientation and origin) in the world. Chains are built by moving along local axes and turning
    /// about local axes: T_i = T_(i−1) · Trans(offset) · Rot(axis, θ) (product of elementary transforms).
    /// </summary>
    internal readonly record struct Frame3(M3 R, V3 P)
    {
        public static readonly Frame3 Identity = new(M3.Identity, V3.Zero);

        /// <summary>The frame moved by a local offset.</summary>
        public Frame3 Move(double x, double y, double z) => new(R, P + R * new V3(x, y, z));

        /// <summary>The frame turned right-handed about a local axis by <paramref name="deg"/>.</summary>
        public Frame3 Turn(V3 localAxis, double deg) => new(R * M3.Rot(localAxis, deg), P);

        /// <summary>World point of a local point.</summary>
        public V3 At(double x, double y, double z) => P + R * new V3(x, y, z);

        /// <summary>World direction of a local direction.</summary>
        public V3 Dir(V3 local) => R * local;
    }

    /// <summary>Orthographic projections of the module's views.</summary>
    internal static class Proj
    {
        /// <summary>Page-oriented 2D point (x right, y down) of a world point: side (x, −z), top (x, −y).</summary>
        public static Vec2 To2(V3 p, string view) => view == RoboViews.Top ? new Vec2(p.X, -p.Y) : new Vec2(p.X, -p.Z);

        /// <summary>Distance from the observer (larger = farther): side +y, top −z.</summary>
        public static double Depth(V3 p, string view) => view == RoboViews.Top ? -p.Z : p.Y;

        /// <summary>Viewing direction in world coordinates.</summary>
        public static V3 ViewDir(string view) => view == RoboViews.Top ? new V3(0, 0, -1) : new V3(0, 1, 0);

        /// <summary>Points on a circle of radius r around c, perpendicular to the axis direction.</summary>
        public static IEnumerable<V3> Ring(V3 c, V3 axis, double r, int n = 16)
        {
            var a = axis.Normalized();
            var helper = Math.Abs(a.Z) < 0.9 ? V3.UnitZ : V3.UnitX;
            var u = a.Cross(helper).Normalized();
            var v = a.Cross(u).Normalized();
            for (var i = 0; i < n; i++)
            {
                var t = 2 * Math.PI * i / n;
                yield return c + u * (r * Math.Cos(t)) + v * (r * Math.Sin(t));
            }
        }
    }

    /// <summary>Kind of a 3D body; selects its fill.</summary>
    internal enum BodyKind { Link, Dark, White }

    /// <summary>
    /// A convex 3D body drawn by its exact silhouette: a capsule (sphere-swept segment, silhouette = 2D capsule) or a
    /// convex point set (cylinder, frustum, box; silhouette = convex hull of the projected points).
    /// </summary>
    internal sealed class Body3
    {
        private Body3(BodyKind kind, string tag) { Kind = kind; Tag = tag; }

        public BodyKind Kind { get; }
        public string Tag { get; }
        public V3 A { get; private init; }
        public V3 B { get; private init; }
        public double Radius { get; private init; }
        public IReadOnlyList<V3>? Points { get; private init; }
        /// <summary>Optional axis marker: a small circle at this point when its axis faces the observer.</summary>
        public (V3 C, V3 Axis, double R)? Marker { get; init; }

        public static Body3 Capsule(V3 a, V3 b, double r, BodyKind kind = BodyKind.Link, string tag = RoboTags.Link) => new(kind, tag) { A = a, B = b, Radius = r };

        public static Body3 Hull(IEnumerable<V3> pts, BodyKind kind, string tag) => new(kind, tag) { Points = pts.ToList() };

        /// <summary>Frustum (cylinder when r0 = r1) from a to b.</summary>
        public static Body3 Cylinder(V3 a, V3 b, double r0, double r1, BodyKind kind, string tag, bool marker = false)
        {
            var axis = b - a;
            var body = Hull(Proj.Ring(a, axis, r0, 20).Concat(Proj.Ring(b, axis, r1, 20)), kind, tag);
            return marker ? new Body3(kind, tag) { Points = body.Points, Marker = ((a + b) * 0.5, axis.Normalized(), Math.Min(r0, r1) * 0.32) } : body;
        }

        /// <summary>Joint housing: cylinder of radius r and length len centred at c along the axis, with an axis marker.</summary>
        public static Body3 Housing(V3 c, V3 axis, double r, double len, BodyKind kind = BodyKind.Link)
        {
            var a = axis.Normalized();
            return Cylinder(c - a * (len / 2), c + a * (len / 2), r, r, kind, RoboTags.Joint, marker: true);
        }

        /// <summary>Box in a frame: centre (local) and half extents along the local axes.</summary>
        public static Body3 Box(Frame3 f, V3 centre, V3 half, BodyKind kind, string tag)
        {
            var pts = new List<V3>();
            for (var i = 0; i < 8; i++)
                pts.Add(f.At(centre.X + ((i & 1) == 0 ? -half.X : half.X), centre.Y + ((i & 2) == 0 ? -half.Y : half.Y), centre.Z + ((i & 4) == 0 ? -half.Z : half.Z)));
            return Hull(pts, kind, tag);
        }

        /// <summary>Slab: two vertical (or any axis) cylinders of radius r at p and q, thickness t, hull (stadium seen along the axis).</summary>
        public static Body3 Slab(V3 p, V3 q, V3 axis, double r, double t, BodyKind kind, string tag)
        {
            var a = axis.Normalized() * (t / 2);
            var pts = Proj.Ring(p - a, axis, r, 20).Concat(Proj.Ring(p + a, axis, r, 20)).Concat(Proj.Ring(q - a, axis, r, 20)).Concat(Proj.Ring(q + a, axis, r, 20));
            return Hull(pts, kind, tag);
        }

        public double Depth(string view)
            => Points is null ? (Proj.Depth(A, view) + Proj.Depth(B, view)) / 2 : Points.Average(p => Proj.Depth(p, view));

        /// <summary>Silhouette polygon in projected model coordinates.</summary>
        public List<Vec2> Outline(string view)
        {
            if (Points is null) return Rb.Capsule(Proj.To2(A, view), Proj.To2(B, view), Radius);
            return Rb.Hull(Points.Select(p => Proj.To2(p, view)));
        }

        /// <summary>Projected points for bounds.</summary>
        public IEnumerable<Vec2> BoundPoints(string view) => Outline(view);
    }

    /// <summary>Kind of a schematic joint symbol.</summary>
    internal enum JointKind { Revolute, Prismatic, Spherical }

    /// <summary>Joint for the schematic drawing and the joint labels.</summary>
    internal sealed record SymJoint(V3 P, V3 Axis, JointKind Kind, string Key);

    /// <summary>Posed robot: bodies, schematic joints and stick chains, base and flange.</summary>
    internal sealed class ArmPose
    {
        public bool Ok { get; set; } = true;
        public List<Body3> Bodies { get; } = new();
        public List<SymJoint> Joints { get; } = new();
        /// <summary>Stick chains of the schematic drawing (link centre lines).</summary>
        public List<V3[]> Sticks { get; } = new();
        /// <summary>Guides of prismatic axes (schematic: drawn as thin lines).</summary>
        public List<(V3 A, V3 B)> Guides { get; } = new();
        /// <summary>Foot of the base (floor point on the first axis).</summary>
        public V3 Base { get; set; }
        /// <summary>Flange centre (tool mounting point, "tool0").</summary>
        public V3 Flange { get; set; }
        /// <summary>Unit direction of the tool axis (out of the flange).</summary>
        public V3 ToolDir { get; set; } = new(0, 0, -1);
        /// <summary>A point on the flange rim that turns with the last joint (index mark).</summary>
        public V3? FlangeMark { get; set; }
        /// <summary>Radius of the flange plate.</summary>
        public double FlangeRadius { get; set; }
        /// <summary>The robot hangs from above (delta): the schematic ground symbol is hatched upwards.</summary>
        public bool BaseUp { get; set; }
        /// <summary>False when the schematic draws its own frame (gantry posts) instead of a ground symbol.</summary>
        public bool Ground { get; set; } = true;
    }

    /// <summary>A 2D area: one fill contour (holes bridged by a seam, so every host fills it as one shape) and its outline.</summary>
    internal sealed class Area2
    {
        public List<Vec2> Fill { get; } = new();
        public List<(List<Vec2> Pts, bool Closed)> Strokes { get; } = new();

        public IEnumerable<Vec2> Points => Fill.Concat(Strokes.SelectMany(s => s.Pts));

        public Area2 Map(Func<Vec2, Vec2> f)
        {
            var r = new Area2();
            r.Fill.AddRange(Fill.Select(f));
            foreach (var (pts, closed) in Strokes) r.Strokes.Add((pts.Select(f).ToList(), closed));
            return r;
        }

        public static Area2 Polygon(IReadOnlyList<Vec2> pts)
        {
            var r = new Area2();
            r.Fill.AddRange(pts);
            r.Strokes.Add((pts.ToList(), true));
            return r;
        }
    }

    /// <summary>
    /// Reach envelopes. A planar two-link chain (shoulder at the origin, inner link L1, outer link L2, shoulder angle θ and
    /// elbow angle φ measured from the stretched position, both clockwise on screen) reaches the point at distance
    /// ρ(φ) = √(L1² + L2² + 2·L1·L2·cos φ) in the direction α = θ + β(φ), β(φ) = atan2(L2·sin φ, L1 + L2·cos φ).
    /// For every |φ| the reachable directions are [θmin + β, θmax + β] for each sign of φ that lies in the elbow range;
    /// the union of both (its hull) is drawn ring by ring. Rings covered all round are closed with a seam so the area stays
    /// one fill contour.
    /// </summary>
    internal static class Reach
    {
        public static double Rho(double l1, double l2, double phiDeg)
            => Math.Sqrt(Math.Max(0, l1 * l1 + l2 * l2 + 2 * l1 * l2 * Math.Cos(Geo.DegToRad(phiDeg))));

        public static double Beta(double l1, double l2, double phiDeg)
        {
            var p = Geo.DegToRad(phiDeg);
            return Geo.RadToDeg(Math.Atan2(l2 * Math.Sin(p), l1 + l2 * Math.Cos(p)));
        }

        private static bool InRange(double v, double lo, double hi)
        {
            for (var k = -2; k <= 2; k++)
            {
                var x = v + 360 * k;
                if (x >= lo - 1e-7 && x <= hi + 1e-7) return true;
            }
            return false;
        }

        /// <summary>Minimum and maximum distance of the outer end from the shoulder over the elbow range.</summary>
        public static (double Min, double Max) RhoRange(double l1, double l2, double p0, double p1)
        {
            double min = double.MaxValue, max = 0;
            var n = 720;
            for (var i = 0; i <= n; i++)
            {
                var r = Rho(l1, l2, p0 + (p1 - p0) * i / n);
                min = Math.Min(min, r); max = Math.Max(max, r);
            }
            if (InRange(0, p0, p1)) max = l1 + l2;
            if (InRange(180, p0, p1)) min = Math.Abs(l1 - l2);
            return (min, max);
        }

        /// <summary>Reach area of a two-link chain around <paramref name="centre"/> (page-oriented model coordinates).</summary>
        public static Area2 TwoLink(Vec2 centre, double l1, double l2, double t0, double t1, double p0, double p1, int samples = 90)
        {
            if (t1 < t0) (t0, t1) = (t1, t0);
            if (p1 < p0) (p0, p1) = (p1, p0);
            var rho = new List<double>(); var lo = new List<double>(); var hi = new List<double>();
            // |φ| from the most stretched to the most folded feasible value.
            double mMin = 180, mMax = 0;
            for (var i = 0; i <= 720; i++)
            {
                var m = Math.Abs(Rb.NormAngle(p0 + (p1 - p0) * i / 720.0));
                mMin = Math.Min(mMin, m); mMax = Math.Max(mMax, m);
            }
            if (InRange(0, p0, p1)) mMin = 0;
            if (InRange(180, p0, p1)) mMax = 180;
            for (var i = 0; i <= samples; i++)
            {
                var m = mMin + (mMax - mMin) * i / samples;
                double? l = null, h = null;
                foreach (var phi in new[] { m, -m })
                {
                    if (!InRange(phi, p0, p1)) continue;
                    var b = Beta(l1, l2, phi);
                    l = l is null ? t0 + b : Math.Min(l.Value, t0 + b);
                    h = h is null ? t1 + b : Math.Max(h.Value, t1 + b);
                }
                if (l is null || h is null) continue;
                rho.Add(Rho(l1, l2, m)); lo.Add(l.Value); hi.Add(h.Value);
            }
            return Build(centre, rho, lo, hi);
        }

        /// <summary>Annular sector between radii <paramref name="r0"/>..<paramref name="r1"/> over the angles t0..t1.</summary>
        public static Area2 Sector(Vec2 centre, double r0, double r1, double t0, double t1)
        {
            if (t1 < t0) (t0, t1) = (t1, t0);
            var rho = new List<double> { r1, r0 };
            return Build(centre, rho, new List<double> { t0, t0 }, new List<double> { t1, t1 });
        }

        /// <summary>
        /// Builds the area from rings (ρ_k, [lo_k, hi_k]). Fill: lo edge, arc at the last ring, hi edge back, arc at the first
        /// ring. Rings reached all round get a hairline seam opposite their middle direction (keyhole), which the outline omits.
        /// </summary>
        private static Area2 Build(Vec2 c, List<double> rho, List<double> lo, List<double> hi)
        {
            var area = new Area2();
            var n = rho.Count;
            if (n == 0) return area;
            var full = new bool[n];
            var l = new double[n]; var h = new double[n];
            const double seam = 0.02;
            for (var k = 0; k < n; k++)
            {
                full[k] = hi[k] - lo[k] >= 360 - 1e-6;
                if (full[k])
                {
                    // The seam sits opposite the middle of the reachable directions (behind the arm).
                    l[k] = (lo[k] + hi[k]) / 2 - 180 + seam; h[k] = (lo[k] + hi[k]) / 2 + 180 - seam;
                }
                else { l[k] = lo[k]; h[k] = hi[k]; }
            }
            Vec2 P(double r, double aDeg) => c + new Vec2(r * Math.Cos(Geo.DegToRad(aDeg)), r * Math.Sin(Geo.DegToRad(aDeg)));
            List<Vec2> ArcPts(double r, double a0, double a1)
            {
                var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(a1 - a0) / 3));
                var pts = new List<Vec2>(steps + 1);
                for (var i = 0; i <= steps; i++) pts.Add(P(r, a0 + (a1 - a0) * i / steps));
                return pts;
            }
            // Fill contour.
            for (var k = 0; k < n; k++) area.Fill.Add(P(rho[k], l[k]));
            area.Fill.AddRange(ArcPts(rho[n - 1], l[n - 1], h[n - 1]).Skip(1));
            for (var k = n - 2; k >= 0; k--) area.Fill.Add(P(rho[k], h[k]));
            var inner = ArcPts(rho[0], h[0], l[0]).Skip(1).ToList();
            if (inner.Count > 0) inner.RemoveAt(inner.Count - 1);
            area.Fill.AddRange(inner);

            // Outline: end arcs (full circles where reached all round) and the edges outside full rings.
            void Ring(int k)
            {
                if (rho[k] < 1e-6) return;
                if (full[k]) area.Strokes.Add((ArcPts(rho[k], 0, 360).Take(120).ToList(), true));
                else area.Strokes.Add((ArcPts(rho[k], l[k], h[k]), false));
            }
            Ring(n - 1);
            if (n > 1) Ring(0);
            void Edge(double[] a)
            {
                var run = new List<Vec2>();
                for (var k = 0; k < n; k++)
                {
                    if (full[k]) { if (run.Count >= 2) area.Strokes.Add((run, false)); run = new List<Vec2>(); continue; }
                    run.Add(P(rho[k], a[k]));
                }
                if (run.Count >= 2) area.Strokes.Add((run, false));
            }
            if (n > 1) { Edge(l); Edge(h); }
            return area;
        }
    }
}
