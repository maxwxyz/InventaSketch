using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>How an optical surface acts on a ray.</summary>
    internal enum SurfaceKind
    {
        /// <summary>Ideal thin lens in the plane of the segment (paraxial: slope change −y/f).</summary>
        ThinLens,
        /// <summary>Plane mirror, reflecting on its front (local −x) side, absorbing on the back.</summary>
        Mirror,
        /// <summary>Curved mirror (one segment of its drawn arc): reflection plus the paraxial slope change −y/f.</summary>
        CurvedMirror,
        /// <summary>Glass face (prism): Snell's law, total internal reflection when it applies.</summary>
        Refract,
        /// <summary>Beam splitter surface: the ray continues and a reflected ray branches off.</summary>
        Split,
        /// <summary>Aperture/slit plane: rays farther than <see cref="Surface.Open"/> from the centre are stopped.</summary>
        Stop,
        /// <summary>Transmission grating: orders −m … +m (sin θm = sin θ + m·sin θ1·λ/550 nm).</summary>
        Grating,
        /// <summary>Filter plate: neutral filters pass everything, colour filters pass their band and colour white light.</summary>
        Filter,
        /// <summary>Detector, screen, housing: the ray ends here.</summary>
        Absorb,
    }

    /// <summary>One optical surface segment of an element, in the element's local coordinates (mm, box centre at 0).</summary>
    internal sealed record Surface(SurfaceKind Kind, Vec2 A, Vec2 B)
    {
        /// <summary>Signed focal length (thin lens, curved mirror): positive converging, negative diverging.</summary>
        public double F { get; init; }
        /// <summary>Optical centre (lens) or vertex (mirror); heights are measured from it.</summary>
        public Vec2 Centre { get; init; }
        /// <summary>Outward normal of a refracting face.</summary>
        public Vec2 Normal { get; init; }
        /// <summary>Refractive index (d line) of the glass behind a refracting face.</summary>
        public double Index { get; init; } = 1.5;
        /// <summary>n(F) − n(C) of the glass; 0 = no dispersion.</summary>
        public double Dispersion { get; init; }
        /// <summary>Half opening of a stop (mm from the centre).</summary>
        public double Open { get; init; }
        /// <summary>Grating: sine of the first-order angle at 550 nm and normal incidence.</summary>
        public double Sine { get; init; }
        /// <summary>Grating: highest order drawn on each side.</summary>
        public int Orders { get; init; } = 1;
        /// <summary>Filter colour ("neutral", "red", "green", "blue", "yellow").</summary>
        public string? Tint { get; init; }

        /// <summary>A polyline as consecutive surface segments sharing the settings of <paramref name="proto"/>.</summary>
        public static IEnumerable<Surface> Chain(Surface proto, IReadOnlyList<Vec2> pts)
        {
            for (var i = 0; i + 1 < pts.Count; i++) yield return proto with { A = pts[i], B = pts[i + 1] };
        }
    }

    /// <summary>An element of the traced scene: its object, local-to-page matrix and surfaces.</summary>
    internal sealed class SceneElement
    {
        public SceneElement(SketchObject o, IReadOnlyList<Surface> surfaces)
        {
            Object = o;
            M = o.Matrix();
            Inv = M.Invert();
            Surfaces = surfaces;
        }

        public SketchObject Object { get; }
        public Matrix2D M { get; }
        public Matrix2D Inv { get; }
        public IReadOnlyList<Surface> Surfaces { get; }
        public Vec2 Centre => M.Apply(Vec2.Zero);
        /// <summary>Local +x (the element's optical axis) in page coordinates.</summary>
        public Vec2 Axis => M.ApplyLinear(new Vec2(1, 0)).Normalized();
        /// <summary>Local +y in page coordinates.</summary>
        public Vec2 Across => M.ApplyLinear(new Vec2(0, 1)).Normalized();

        /// <summary>The thin lens or curved mirror of the element (for principal rays), else null.</summary>
        public Surface? Focusing => Surfaces.FirstOrDefault(s => s.Kind is SurfaceKind.ThinLens or SurfaceKind.CurvedMirror && Math.Abs(s.F) > 1e-9);
    }

    /// <summary>A ray leaving an emitter (page coordinates, unit direction); wavelength in nm, 0 = white light.</summary>
    internal sealed record StartRay(Vec2 P, Vec2 D, double Lambda = 0);

    /// <summary>The rays of an emitter and the element they leave (excluded from the trace), if any.</summary>
    internal sealed record Emission(IReadOnlyList<StartRay> Rays, string? Exclude);

    /// <summary>One traced ray: its polyline, the wavelength of each segment and its virtual (dashed) extensions.</summary>
    internal sealed class TracedPath
    {
        public TracedPath(Vec2 start) => Points.Add(start);
        public List<Vec2> Points { get; } = new();
        /// <summary>Wavelength per segment (nm, 0 = white).</summary>
        public List<double> Lambdas { get; } = new();
        public List<(Vec2 A, Vec2 B, double Lambda)> Virtual { get; } = new();
        /// <summary>True if the ray ended on a detector, screen, stop or housing.</summary>
        public bool Absorbed { get; set; }

        public void LineTo(Vec2 p, double lambda)
        {
            if (p.DistanceTo(Points[Points.Count - 1]) < 1e-9) return;
            Points.Add(p);
            Lambdas.Add(lambda);
        }

        public Vec2 Last => Points[Points.Count - 1];
    }

    /// <summary>Result of a trace: the paths (branches of splitters and gratings are paths of their own).</summary>
    internal sealed class TraceResult
    {
        public List<TracedPath> Paths { get; } = new();
        /// <summary>True if white light was split into colours (prism dispersion, grating orders).</summary>
        public bool Dispersed { get; set; }
    }

    /// <summary>Wavelengths and colours used for dispersion (C, D, e, F, g lines of the visible spectrum).</summary>
    internal static class Spectrum
    {
        public static readonly double[] Lines = { 656.3, 589.3, 546.1, 486.1, 435.8 };

        /// <summary>Drawing colour of a wavelength; null for white light (the bundle colour).</summary>
        public static string? Color(double lambda)
        {
            if (lambda <= 0) return null;
            if (lambda >= 620) return "#e53935";
            if (lambda >= 570) return "#f9a825";
            if (lambda >= 495) return "#43a047";
            if (lambda >= 450) return "#1e88e5";
            return "#8e24aa";
        }

        /// <summary>
        /// Refractive index at <paramref name="lambda"/> from the d-line index and n(F) − n(C) (two-term Cauchy formula);
        /// white light (0) uses the d-line index.
        /// </summary>
        public static double Index(double nd, double dispersion, double lambda)
        {
            if (dispersion <= 0 || lambda <= 0) return nd;
            var b = dispersion / (1 / (486.1 * 486.1) - 1 / (656.3 * 656.3));
            return nd + b * (1 / (lambda * lambda) - 1 / (587.6 * 587.6));
        }
    }

    /// <summary>Paraxial imaging by a thin lens (Gaussian lens equation 1/f = 1/s + 1/s′, distances positive in front/behind).</summary>
    public static class ThinLens
    {
        /// <summary>Image distance s′ behind the lens for an object at distance <paramref name="s"/> in front; infinite at s = f.</summary>
        public static double ImageDistance(double f, double s)
        {
            var d = 1 / f - 1 / s;
            return Math.Abs(d) < 1e-15 ? double.PositiveInfinity : 1 / d;
        }

        /// <summary>Lateral magnification −s′/s.</summary>
        public static double Magnification(double f, double s) => -ImageDistance(f, s) / s;
    }

    /// <summary>
    /// Two-dimensional sequential ray tracer of the optics module. A ray runs from its start until it meets the nearest
    /// surface of an element on the page, is changed there (thin lens, mirror, refraction, splitting …) and runs on.
    /// Lenses and curved mirrors are ideal and paraxial (slope change −y/f, so all parallel rays meet in the focal point);
    /// plane mirrors, splitters and prism faces follow the law of reflection and Snell's law exactly.
    /// </summary>
    internal static class RayTracer
    {
        public const int MaxEvents = 48;
        public const int MaxPaths = 120;
        public const double MaxLength = 20000;

        private sealed class Hit
        {
            public SceneElement E = null!;
            public int K;
            public Surface S = null!;
            public double T;
            public Vec2 Local;
            public Vec2 LocalDir;
        }

        /// <summary>The optical elements on the page of <paramref name="ray"/> (hidden layers left out), in z-order.</summary>
        public static List<SceneElement> Scene(SketchDocument doc, SketchObject ray)
        {
            var page = doc.PageOf(ray.Id) ?? doc.Pages.FirstOrDefault();
            var objects = page is null ? doc.Objects.Values : doc.ObjectsOn(page);
            var hidden = new HashSet<string>(doc.Layers.Where(l => !l.Visible).Select(l => l.Id), StringComparer.Ordinal);
            var list = new List<SceneElement>();
            foreach (var o in objects)
            {
                if (o.IsPointGeometry || o.Id == ray.Id || o.Layer is not null && hidden.Contains(o.Layer)) continue;
                if (OpticsModule.TypeOf(o.Type) is not OpticsType t) continue;
                if (o.Size.W < 1e-6 || o.Size.H < 1e-6) continue;
                var surfaces = t.Surfaces(o);
                if (surfaces.Count > 0) list.Add(new SceneElement(o, surfaces));
            }
            return list;
        }

        /// <summary>
        /// Traces <paramref name="starts"/> through <paramref name="scene"/>. After the last element a ray runs on for
        /// <paramref name="tail"/> mm; a ray that meets nothing runs at least <paramref name="reach"/> mm.
        /// </summary>
        public static TraceResult Trace(IReadOnlyList<SceneElement> scene, IEnumerable<StartRay> starts, double tail, double reach = 0, bool virtualRays = true)
        {
            var result = new TraceResult();
            var work = new Stack<(Vec2 P, Vec2 D, double Lambda, TracedPath Path, double Travelled, SceneElement? SkipE, int SkipK, double Reach)>();
            foreach (var s in starts.Reverse())
            {
                if (s.D.Length < 1e-12) continue;
                var path = new TracedPath(s.P);
                work.Push((s.P, s.D.Normalized(), s.Lambda, path, 0, null, -1, reach));
            }
            // Paths in the order of their start rays (the stack pops the first one first).
            var order = work.Select(w => w.Path).ToList();
            result.Paths.AddRange(order);
            var axis = starts.Aggregate(Vec2.Zero, (acc, x) => acc + x.D.Normalized()).Normalized();
            var open = new List<TracedPath>();
            while (work.Count > 0)
            {
                var (p, d, lambda, path, travelled, skipE, skipK, minReach) = work.Pop();
                for (var step = 0; ; step++)
                {
                    if (step >= MaxEvents || travelled > MaxLength) break;
                    var hit = Nearest(scene, p, d, skipE, skipK);
                    if (hit is null)
                    {
                        // Automatic tail (0): as long again as the way so far (between 30 and 400 mm), so images and foci
                        // behind the last element are reached.
                        var after = tail > 0 ? tail : Geo.Clamp(1.2 * travelled, 30, 400);
                        // A free ray that meets nothing ends where it was drawn to.
                        var free = step == 0 && minReach > 0;
                        var len = free ? minReach : Math.Max(after, minReach - travelled);
                        if (len > 1e-9) path.LineTo(p + d * len, lambda);
                        if (!free && tail <= 0 && path.Points.Count >= 2) open.Add(path);
                        break;
                    }
                    var at = p + d * hit.T;
                    path.LineTo(at, lambda);
                    travelled += hit.T;
                    var e = hit.E;
                    var s = hit.S;
                    var ld = hit.LocalDir;
                    Vec2 Page(Vec2 local) => e.M.ApplyLinear(local).Normalized();
                    void Branch(Vec2 localDir, double lam)
                    {
                        if (result.Paths.Count >= MaxPaths) return;
                        var child = new TracedPath(at);
                        result.Paths.Add(child);
                        work.Push((at, Page(localDir), lam, child, travelled, e, hit.K, 0));
                    }
                    Vec2? next = null;
                    switch (s.Kind)
                    {
                        case SurfaceKind.Absorb:
                            path.Absorbed = true;
                            break;
                        case SurfaceKind.Stop:
                            if (Math.Abs(hit.Local.Y - s.Centre.Y) > s.Open) path.Absorbed = true;
                            else next = ld;
                            break;
                        case SurfaceKind.ThinLens:
                            {
                                var nd = Deflect(ld, hit.Local.Y - s.Centre.Y, s.F);
                                next = nd;
                                if (virtualRays && s.F < 0) AddVirtual(path, e, hit.Local, nd, s, lambda);
                                break;
                            }
                        case SurfaceKind.Mirror:
                            if (ld.X > 1e-12) next = new Vec2(-ld.X, ld.Y);
                            else path.Absorbed = true;
                            break;
                        case SurfaceKind.CurvedMirror:
                            if (ld.X > 1e-12)
                            {
                                var nd = MirrorDirection(hit.Local, ld, s);
                                next = nd;
                                if (virtualRays && s.F < 0) AddVirtual(path, e, hit.Local, nd, s, lambda);
                            }
                            else path.Absorbed = true;
                            break;
                        case SurfaceKind.Split:
                            {
                                var n = (s.B - s.A).Perp.Normalized();
                                Branch(ld - n * (2 * ld.Dot(n)), lambda);
                                next = ld;
                                break;
                            }
                        case SurfaceKind.Refract:
                            if (lambda <= 0 && s.Dispersion > 0 && ld.Dot(s.Normal) < 0)
                            {
                                // White light entering a dispersing glass: one ray per colour from here on.
                                result.Dispersed = true;
                                foreach (var line in Spectrum.Lines) Branch(Refract(ld, s.Normal, Spectrum.Index(s.Index, s.Dispersion, line)), line);
                            }
                            else next = Refract(ld, s.Normal, Spectrum.Index(s.Index, s.Dispersion, lambda));
                            break;
                        case SurfaceKind.Grating:
                            {
                                if (Math.Abs(ld.X) < 1e-9) { next = ld; break; }
                                var sign = Math.Sign(ld.X);
                                Vec2? Order(int m, double lam)
                                {
                                    var sm = ld.Y + m * s.Sine * (lam <= 0 ? 550 : lam) / 550;
                                    return Math.Abs(sm) >= 0.999 ? null : new Vec2(sign * Math.Sqrt(1 - sm * sm), sm);
                                }
                                for (var m = -s.Orders; m <= s.Orders; m++)
                                {
                                    if (m == 0) continue;
                                    if (lambda <= 0)
                                    {
                                        result.Dispersed = true;
                                        foreach (var line in Spectrum.Lines) if (Order(m, line) is { } dl) Branch(dl, line);
                                    }
                                    else if (Order(m, lambda) is { } dm) Branch(dm, lambda);
                                }
                                next = ld;
                                break;
                            }
                        case SurfaceKind.Filter:
                            {
                                var (pass, band) = FilterPass(s.Tint, lambda);
                                if (!pass) path.Absorbed = true;
                                else { next = ld; lambda = band; }
                                break;
                            }
                    }
                    if (next is not Vec2 nl) break;
                    p = at;
                    d = Page(nl);
                    skipE = e;
                    skipK = hit.K;
                    minReach = 0;
                }
            }
            AlignEnds(result.Paths, open, axis);
            return result;
        }

        /// <summary>
        /// Automatic length: the open ends of a bundle running the same way (forwards or back along the mean start direction)
        /// are lengthened to end on one line across the bundle, so the rays do not stop at ragged points.
        /// </summary>
        private static void AlignEnds(List<TracedPath> all, List<TracedPath> open, Vec2 axis)
        {
            if (open.Count == 0 || all.Count < 2 || axis.Length < 1e-9) return;
            (TracedPath Path, Vec2 D) Dir(TracedPath p) => (p, (p.Last - p.Points[p.Points.Count - 2]).Normalized());
            foreach (var sign in new[] { 1.0, -1.0 })
            {
                var a = axis * sign;
                // The line runs through the farthest end of the bundle, rays ending on a detector included.
                var reach = all.Where(p => p.Points.Count >= 2).Select(Dir).Where(x => x.D.Dot(a) > 0.3).ToList();
                var group = open.Select(Dir).Where(x => x.D.Dot(a) > 0.3).ToList();
                if (reach.Count < 2 || group.Count == 0) continue;
                var target = reach.Max(x => x.Path.Last.Dot(a));
                foreach (var (path, d) in group)
                {
                    var extra = Math.Min(400, (target - path.Last.Dot(a)) / d.Dot(a));
                    if (extra > 1e-9) path.Points[path.Points.Count - 1] = path.Last + d * extra;
                }
            }
        }

        /// <summary>Thin-element deflection in local coordinates: the slope dy/dx changes by −y/f, the direction of travel along x stays.</summary>
        internal static Vec2 Deflect(Vec2 d, double y, double f)
        {
            if (Math.Abs(d.X) < 1e-9 || Math.Abs(f) < 1e-12) return d;
            var s = Math.Sign(d.X);
            var u = d.Y / Math.Abs(d.X) - y / f;
            return new Vec2(s, u).Normalized();
        }

        /// <summary>
        /// Ideal curved mirror: the ray is reflected as by a thin mirror in the vertex plane (slope change −y/f at the height
        /// where the incoming ray crosses that plane) and leaves the drawn surface point towards the point where that ideal
        /// ray reaches the focal plane, so parallel rays meet exactly in F however the surface is drawn.
        /// </summary>
        internal static Vec2 MirrorDirection(Vec2 local, Vec2 ld, Surface s)
        {
            var tPlane = (s.Centre.X - local.X) / ld.X;
            var yp = local.Y + ld.Y * tPlane;
            var ideal = Deflect(new Vec2(-ld.X, ld.Y), yp - s.Centre.Y, s.F);
            var k = Math.Abs(s.F) / Math.Max(1e-9, Math.Abs(ideal.X));
            var target = new Vec2(s.Centre.X, yp) + ideal * k;
            var dir = target - local;
            return dir.Length < 1e-9 ? ideal : dir.Normalized();
        }

        /// <summary>
        /// Snell's law at a face with outward normal <paramref name="outward"/> between air and glass of index
        /// <paramref name="n"/>; total internal reflection when no refracted ray exists.
        /// </summary>
        internal static Vec2 Refract(Vec2 d, Vec2 outward, double n)
        {
            var entering = d.Dot(outward) < 0;
            var normal = entering ? outward : outward * -1;   // points back to the side the ray comes from
            var eta = entering ? 1 / n : n;
            var cosI = -normal.Dot(d);
            var k = 1 - eta * eta * (1 - cosI * cosI);
            if (k < 0) return (d + normal * (2 * cosI)).Normalized();
            return (d * eta + normal * (eta * cosI - Math.Sqrt(k))).Normalized();
        }

        /// <summary>Filter: passes and the wavelength behind it.</summary>
        private static (bool Pass, double Lambda) FilterPass(string? tint, double lambda)
        {
            (double Lo, double Hi, double Rep) band = tint switch
            {
                "red" => (600, 2000, 650),
                "green" => (495, 570, 530),
                "blue" => (0, 495, 460),
                "yellow" => (550, 2000, 580),
                _ => (0, 0, 0),
            };
            if (band.Rep <= 0) return (true, lambda);
            if (lambda <= 0) return (true, band.Rep);
            return (lambda >= band.Lo && lambda <= band.Hi, lambda);
        }

        /// <summary>
        /// Dashed backward extension of a ray leaving a diverging lens or convex mirror, back to the element's virtual focal
        /// plane (where parallel rays seem to come from).
        /// </summary>
        private static void AddVirtual(TracedPath path, SceneElement e, Vec2 local, Vec2 outLocal, Surface s, double lambda)
        {
            if (Math.Abs(outLocal.X) < 1e-9) return;
            // An undeflected ray (on the axis) has no virtual part of its own.
            if (Math.Abs(local.Y - s.Centre.Y) < 1e-9) return;
            var plane = s.Centre.X - Math.Sign(outLocal.X) * Math.Abs(s.F);
            var tau = (local.X - plane) / outLocal.X;
            if (tau <= 1e-6 || tau > 10 * Math.Abs(s.F) + 1) return;
            var back = local - outLocal * tau;
            path.Virtual.Add((e.M.Apply(local), e.M.Apply(back), lambda));
        }

        private static Hit? Nearest(IReadOnlyList<SceneElement> scene, Vec2 p, Vec2 d, SceneElement? skipE, int skipK)
        {
            Hit? best = null;
            foreach (var e in scene)
            {
                var lp = e.Inv.Apply(p);
                var ld = e.Inv.ApplyLinear(d);
                for (var k = 0; k < e.Surfaces.Count; k++)
                {
                    var s = e.Surfaces[k];
                    var edge = s.B - s.A;
                    var den = ld.Cross(edge);
                    if (Math.Abs(den) < 1e-12) continue;
                    var w = s.A - lp;
                    var t = w.Cross(edge) / den;
                    var u = w.Cross(ld) / den;
                    if (u < -1e-9 || u > 1 + 1e-9) continue;
                    var min = ReferenceEquals(e, skipE) && k == skipK ? 1e-4 : 1e-7;
                    if (t <= min) continue;
                    if (best is not null && t >= best.T) continue;
                    best = new Hit { E = e, K = k, S = s, T = t, Local = lp + ld * t, LocalDir = ld };
                }
            }
            return best;
        }
    }
}
