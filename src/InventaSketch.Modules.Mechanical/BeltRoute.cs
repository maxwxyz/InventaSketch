using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Serpentine routing of a belt or chain over an ordered list of pulleys, sprockets and idlers (parameter <c>route</c>).
    /// <para>
    /// The route is stored relative to the object box, so free scaling changes the size and never the parameter: each hub is
    /// "u,v,k" or "u,v,k,b" (entries separated by ";"), with the centre at (u·W, v·H) (u, v from −0.5 to 0.5) and the
    /// running radius k·min(W, H); "b" marks a hub the belt runs over with its back (an idler outside the loop that bends
    /// the belt inwards). Entries are clamped so every hub stays inside the box.
    /// </para>
    /// <para>
    /// Geometry: consecutive hubs are joined by their common tangent. With signed radii ρ (+r for a hub the belt wraps with its
    /// running face, −r for a back-side idler) the tangent normal m satisfies d·m = ρᵢ − ρⱼ, which gives the outer tangent for
    /// two front hubs and the crossing (inner) tangent next to a back-side idler. The loop runs around the front hubs in the
    /// sense of the polygon of their centres; front hubs are wrapped in that sense, back-side idlers in the opposite one.
    /// </para>
    /// </summary>
    internal static class BeltRoute
    {
        /// <summary>One hub of a stored route (box fractions).</summary>
        public readonly struct Hub
        {
            public Hub(double u, double v, double k, bool back) { U = u; V = v; K = k; Back = back; }
            public double U { get; }
            public double V { get; }
            public double K { get; }
            public bool Back { get; }
        }

        /// <summary>A placed hub in local millimetres: centre, running radius, back side.</summary>
        public readonly struct Placed
        {
            public Placed(Vec2 c, double r, bool back) { C = c; R = r; Back = back; }
            public Vec2 C { get; }
            public double R { get; }
            public bool Back { get; }
            /// <summary>Signed radius: +r for the running face, −r for the back of the belt.</summary>
            public double Rho => Back ? -R : R;
        }

        /// <summary>Contact of the loop with one hub: where the belt arrives and leaves and the wrap (radians, signed).</summary>
        public readonly struct Wrap
        {
            public Wrap(Vec2 c, double r, Vec2 arrive, Vec2 leave, double start, double sweep)
            {
                C = c; R = r; Arrive = arrive; Leave = leave; Start = start; Sweep = sweep;
            }
            public Vec2 C { get; }
            public double R { get; }
            public Vec2 Arrive { get; }
            public Vec2 Leave { get; }
            public double Start { get; }
            /// <summary>Wrap angle, positive in the sense of increasing angles (clockwise on screen).</summary>
            public double Sweep { get; }
        }

        /// <summary>
        /// Default route (quick sketch): crankshaft pulley bottom left, water pump bottom right, alternator top right and a
        /// back-side idler that bends the strand from the alternator back to the crankshaft inwards.
        /// </summary>
        public static readonly IReadOnlyList<Hub> Default = new[]
        {
            new Hub(-0.26, 0.22, 0.22, false),
            new Hub(0.29, 0.21, 0.18, false),
            new Hub(0.33, -0.29, 0.14, false),
            new Hub(-0.03, -0.09, 0.10, true),
        };

        public const string DefaultText = "-0.26,0.22,0.22;0.29,0.21,0.18;0.33,-0.29,0.14;-0.03,-0.09,0.1,b";

        private static string F(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>The stored route of <paramref name="o"/> (the default route when missing or with fewer than two hubs).</summary>
        public static IReadOnlyList<Hub> Of(SketchObject o) => Parse(o.Str("route", ""));

        public static IReadOnlyList<Hub> Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Default;
            var list = new List<Hub>();
            foreach (var entry in text!.Split(';'))
            {
                var f = entry.Split(',').Select(s => s.Trim()).ToArray();
                if (f.Length < 3) continue;
                if (!double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var u)
                    || !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    || !double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var k)) continue;
                if (double.IsNaN(u) || double.IsNaN(v) || double.IsNaN(k) || double.IsInfinity(u) || double.IsInfinity(v) || double.IsInfinity(k)) continue;
                var back = f.Length > 3 && f[3].StartsWith("b", StringComparison.OrdinalIgnoreCase);
                list.Add(Clamp(new Hub(u, v, k, back)));
            }
            return list.Count >= 2 ? list : Default;
        }

        /// <summary>Keeps a hub inside the box: radius 0.01 to 0.5 of the smaller side, centre so the circle fits.</summary>
        public static Hub Clamp(Hub h)
        {
            // Only the coarse range: Place keeps every circle inside the actual box (u and v are fractions of width and
            // height, k of the smaller side, so a tighter bound here would pull hubs inwards in wide boxes).
            var k = Geo.Clamp(h.K, 0.01, 0.5);
            return new Hub(Geo.Clamp(h.U, -0.5, 0.5), Geo.Clamp(h.V, -0.5, 0.5), k, h.Back);
        }

        public static string Format(IEnumerable<Hub> hubs)
            => string.Join(";", hubs.Select(h => F(h.U) + "," + F(h.V) + "," + F(h.K) + (h.Back ? ",b" : "")));

        /// <summary>
        /// The hubs placed in a box of <paramref name="w"/> × <paramref name="h"/> mm. <paramref name="margin"/> (mm) keeps
        /// that much room between each circle and the box edge (the chain plates outside the pitch line).
        /// </summary>
        public static List<Placed> Place(IReadOnlyList<Hub> hubs, double w, double h, double margin = 0)
        {
            var list = new List<Placed>(hubs.Count);
            var min = Math.Min(w, h);
            foreach (var hub in hubs)
            {
                var r = Math.Max(0.01, Math.Min(hub.K * min, min / 2 - margin));
                double mx = Math.Max(0, w / 2 - r - margin), my = Math.Max(0, h / 2 - r - margin);
                list.Add(new Placed(new Vec2(Geo.Clamp(hub.U * w, -mx, mx), Geo.Clamp(hub.V * h, -my, my)), r, hub.Back));
            }
            return list;
        }

        /// <summary>Sense of the loop: the sign of the area of the polygon of the front hub centres (all hubs if fewer than three).</summary>
        public static double Sense(IReadOnlyList<Placed> hubs)
        {
            var front = hubs.Where(h => !h.Back).Select(h => h.C).ToList();
            if (front.Count < 3) front = hubs.Select(h => h.C).ToList();
            double a = 0;
            for (var i = 0; i < front.Count; i++) a += front[i].Cross(front[(i + 1) % front.Count]);
            return a < 0 ? -1 : 1;
        }

        /// <summary>
        /// Tangent strand from hub <paramref name="a"/> to hub <paramref name="b"/> with signed radii offset by
        /// <paramref name="offset"/> (mm, outwards; 0 = the running line): start and end point.
        /// </summary>
        public static (Vec2 From, Vec2 To) Tangent(Placed a, Placed b, double sense, double offset = 0)
        {
            double ra = Rho(a, offset), rb = Rho(b, offset);
            var d = b.C - a.C;
            var len = d.Length;
            if (len < Geo.Eps) return (a.C, b.C);
            var u = d / len;
            var right = new Vec2(u.Y, -u.X) * sense;
            var cos = Geo.Clamp((ra - rb) / len, -1, 1);
            var sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
            var m = u * cos + right * sin;
            return (a.C + m * ra, b.C + m * rb);
        }

        private static double Rho(Placed h, double offset)
        {
            var rho = h.Rho + offset;
            // An offset never turns a hub inside out (very small idler with a thick chain).
            return h.Back ? Math.Min(rho, -0.01) : Math.Max(rho, 0.01);
        }

        /// <summary>The wrap at every hub (in route order) for the line offset by <paramref name="offset"/> mm outwards.</summary>
        public static List<Wrap> Wraps(IReadOnlyList<Placed> hubs, double offset = 0)
        {
            var n = hubs.Count;
            var sense = Sense(hubs);
            var strands = new (Vec2 From, Vec2 To)[n];
            for (var i = 0; i < n; i++) strands[i] = Tangent(hubs[i], hubs[(i + 1) % n], sense, offset);
            var wraps = new List<Wrap>(n);
            for (var j = 0; j < n; j++)
            {
                var h = hubs[j];
                var arrive = strands[(j - 1 + n) % n].To;
                var leave = strands[j].From;
                double a0 = Math.Atan2(arrive.Y - h.C.Y, arrive.X - h.C.X), a1 = Math.Atan2(leave.Y - h.C.Y, leave.X - h.C.X);
                var dir = sense * (h.Back ? -1 : 1);
                var sweep = a1 - a0;
                if (dir > 0) { while (sweep < 0) sweep += 2 * Math.PI; while (sweep >= 2 * Math.PI) sweep -= 2 * Math.PI; }
                else { while (sweep > 0) sweep -= 2 * Math.PI; while (sweep <= -2 * Math.PI) sweep += 2 * Math.PI; }
                wraps.Add(new Wrap(h.C, Math.Abs(Rho(h, offset)), arrive, leave, a0, sweep));
            }
            return wraps;
        }

        /// <summary>The closed loop as polygon points (arcs sampled, tangent strands straight).</summary>
        public static List<Vec2> Loop(IReadOnlyList<Placed> hubs, double offset = 0)
        {
            var pts = new List<Vec2>();
            foreach (var w in Wraps(hubs, offset))
            {
                if (Math.Abs(w.Sweep) < 1e-9) { pts.Add(w.Arrive); continue; }
                pts.AddRange(Geo.ArcPoints(w.C, w.R, w.R, w.Start, w.Sweep));
            }
            return pts;
        }

        /// <summary>Points every <paramref name="step"/> mm along the closed polygon <paramref name="loop"/>.</summary>
        public static List<Vec2> Sample(IReadOnlyList<Vec2> loop, double step)
        {
            var result = new List<Vec2>();
            if (loop.Count < 2 || step <= Geo.Eps) return result;
            double total = 0;
            for (var i = 0; i < loop.Count; i++) total += loop[i].DistanceTo(loop[(i + 1) % loop.Count]);
            var n = Math.Max(1, (int)Math.Floor(total / step));
            var spacing = total / n;
            double next = 0, run = 0;
            for (var i = 0; i < loop.Count && result.Count < n; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Count];
                var len = a.DistanceTo(b);
                while (next <= run + len && result.Count < n)
                {
                    result.Add(len < Geo.Eps ? a : Vec2.Lerp(a, b, (next - run) / len));
                    next += spacing;
                }
                run += len;
            }
            return result;
        }
    }
}
