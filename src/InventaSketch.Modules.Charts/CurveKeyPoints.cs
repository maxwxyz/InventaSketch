using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Adaptive key points of sketched curves (maintainer 2026-11-25: "when drawing a curve it sometimes samples too many
    /// points – maybe it can be set, or depend on how fast or which curve you draw?"). A sketched curve stores only the points
    /// that carry its shape; smooth shapes are drawn through them with the chart's spline (<see cref="Draw.Spline"/>, uniform
    /// Catmull–Rom, affine invariant, so the check in frame-normalised coordinates holds on the drawing too).
    /// <list type="bullet">
    /// <item><see cref="Polyline"/>: Ramer–Douglas–Peucker with a tolerance per point – straight segments stay within it.</item>
    /// <item><see cref="Spline"/>: seeds at the ends, at turning points (x and y extrema with hysteresis), at corners and at
    /// the steepest point between a peak and a valley (the inflection); between them as few points as possible, spread
    /// evenly over the curve's bending (√|curvature|), their number raised until the spline through all key points stays
    /// within the tolerance band around the target curve (both directions, so overshoots count). A straight stretch keeps
    /// two points, a gentle arc a few, tight bends more.</item>
    /// <item>Pace (<see cref="Pace"/>): drawing speed as a hint – each point's tolerance is multiplied by a factor
    /// √(speed / <see cref="ReferenceSpeed"/>) clamped to <see cref="PaceMin"/> … <see cref="PaceMax"/>, so slow, careful
    /// drawing keeps more detail and a fast stroke is simplified more.</item>
    /// </list>
    /// All coordinates are frame-normalised (1 = plot size). Pure and deterministic.
    /// </summary>
    public static class CurveKeyPoints
    {
        /// <summary>Speed (plot sizes per second) at which the pace factor is 1.</summary>
        public const double ReferenceSpeed = 1.0;
        /// <summary>Smallest and largest pace factor (slow, careful drawing … a fast flick).</summary>
        public const double PaceMin = 0.6, PaceMax = 1.5;
        /// <summary>Pace factors are stored in steps of this size (one digit per point: 0 = <see cref="PaceMin"/> … 9 = <see cref="PaceMax"/>).</summary>
        public const double PaceStep = 0.1;
        /// <summary>Shortest time span (ms) over which the speed at a point is measured (pointer events come every 8 … 16 ms).</summary>
        public const double PaceWindowMs = 120;
        /// <summary>Hysteresis of a turning point, in tolerances: the curve must turn back by this much to count.</summary>
        public const double TurnHysteresis = 4;
        /// <summary>A corner turns by at least this angle (degrees) within ±<see cref="CornerWindow"/> of arc length.</summary>
        public const double CornerAngle = 50;
        public const double CornerWindow = 0.02;
        /// <summary>Half-width (arc length) over which the curvature of the target is measured.</summary>
        public const double BendWindow = 0.01;
        /// <summary>Steps per spline segment when the spline is checked against the target.</summary>
        private const int CheckSteps = 10;

        // ------------------------------------------------------------------ polyline (straight segments, as drawn)

        /// <summary>
        /// Key points of a polyline drawn straight: Ramer–Douglas–Peucker with tolerance <paramref name="tol"/> × the point's
        /// factor; at most <paramref name="max"/> points (the tolerance grows until they fit).
        /// </summary>
        public static IReadOnlyList<Vec2> Polyline(IReadOnlyList<Vec2> target, double tol, IReadOnlyList<double>? factor, int max)
        {
            if (target.Count <= 2) return target.ToList();
            var f = Factors(target.Count, factor);
            for (var round = 0; round < 12; round++, tol *= 1.35)
            {
                var keys = Rdp(target, new SortedSet<int> { 0, target.Count - 1 }, tol, f);
                if (keys.Count <= max) return keys.Select(i => target[i]).ToList();
            }
            return CurveShapes.Thin(target, max);
        }

        // ------------------------------------------------------------------ spline (smoothed, smooth fitted functions)

        /// <summary>
        /// Key points of a curve drawn as a spline (<see cref="Draw.Spline"/>) that stays within <paramref name="tol"/> × the
        /// point's factor of <paramref name="target"/> (a dense, evenly spaced polyline); at most <paramref name="max"/> points.
        /// </summary>
        public static IReadOnlyList<Vec2> Spline(IReadOnlyList<Vec2> target, double tol, IReadOnlyList<double>? factor, int max)
        {
            if (target.Count <= 2) return target.ToList();
            var f = Factors(target.Count, factor);
            for (var round = 0; round < 12; round++, tol *= 1.35)
            {
                var keys = SplineKeys(target, tol, f, max);
                if (keys.Count <= max) return keys.Select(i => target[i]).ToList();
            }
            return CurveShapes.Thin(target, max);
        }

        /// <summary>
        /// The fewest key points (scanning their number upwards) that are spread evenly over the curve's bending – equal shares
        /// of ∫ (√|curvature| + base) / pace ds between neighbours, so tight bends and slowly drawn parts get more – plus the
        /// seeds (<see cref="Seeds"/>; a spread point next to a seed gives way to it), such that the spline through them stays
        /// within the tolerance band of the target in both directions. More points than <paramref name="max"/> when even
        /// those do not fit (the caller then widens the tolerance).
        /// </summary>
        private static List<int> SplineKeys(IReadOnlyList<Vec2> t, double tol, double[] f, int max)
        {
            var n = t.Count;
            var seeds = Seeds(t, tol).ToList();
            var cum = Bending(t, f);
            var total = cum[n - 1];
            List<int> With(int m)
            {
                var spread = new List<int>();
                for (var j = 0; j < m; j++)
                {
                    var u = total * j / (m - 1);
                    var i = Array.BinarySearch(cum, u);
                    if (i < 0) i = Math.Min(n - 1, ~i);
                    spread.Add(i);
                }
                // A spread point gives way to a seed closer than a third of its spacing.
                var gap = n / (3.0 * (m - 1));
                return seeds.Concat(spread.Where(i => !seeds.Any(s => Math.Abs(s - i) < gap))).Distinct().OrderBy(i => i).ToList();
            }
            // Gallop to a number of spread points that fits (2, 3, 5, 8, 12 …), then bisect down to the fewest that fit.
            int lo = 1, hi = 2;
            List<int>? fit = null;
            while (true)
            {
                var keys = With(hi);
                if (keys.Count > max) return keys;
                if (Fits(t, keys, tol, f)) { fit = keys; break; }
                lo = hi;
                hi = Math.Max(hi + 1, hi * 3 / 2);
                if (hi > max + 1) return With(max + 1);
            }
            while (hi - lo > 1)
            {
                var mid = (lo + hi) / 2;
                var keys = With(mid);
                if (Fits(t, keys, tol, f)) { hi = mid; fit = keys; }
                else lo = mid;
            }
            return Prune(t, fit, new HashSet<int>(seeds), tol, f);
        }

        /// <summary>Drops spread points (not seeds) whose removal keeps the spline within the band (checked on the affected segments).</summary>
        private static List<int> Prune(IReadOnlyList<Vec2> t, List<int> keys, HashSet<int> seeds, double tol, double[] f)
        {
            for (var j = 1; j < keys.Count - 1;)
            {
                if (seeds.Contains(keys[j])) { j++; continue; }
                var trial = keys.Take(j).Concat(keys.Skip(j + 1)).ToList();
                // Removing key j changes the segments from j − 2 to j + 1 of the trial list.
                if (FitsRange(t, trial, Math.Max(0, j - 2), Math.Min(trial.Count - 2, j + 1), tol, f)) keys = trial;
                else j++;
            }
            return keys;
        }

        /// <summary>
        /// Cumulative bending weight along the target: (√|curvature| + a base of 30 % of its mean, so straight runs still count
        /// a little) / pace factor, integrated over arc length.
        /// </summary>
        private static double[] Bending(IReadOnlyList<Vec2> t, double[] f)
        {
            var n = t.Count;
            var s = new double[n];
            for (var i = 1; i < n; i++) s[i] = s[i - 1] + t[i].DistanceTo(t[i - 1]);
            var ds = n > 1 ? s[n - 1] / (n - 1) : 1;
            var k = Math.Max(1, (int)Math.Round(BendWindow / Math.Max(ds, 1e-9)));
            var root = new double[n];
            for (var i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - k), b = Math.Min(n - 1, i + k);
                if (a == i || b == i) continue;
                Vec2 u = t[i] - t[a], w = t[b] - t[i];
                var len = u.Length + w.Length;
                if (u.Length < 1e-12 || w.Length < 1e-12) continue;
                var angle = Math.Abs(Math.Atan2(u.X * w.Y - u.Y * w.X, u.X * w.X + u.Y * w.Y));
                root[i] = Math.Sqrt(2 * angle / len);
            }
            // The ends have no full window: take their neighbours' value.
            for (var i = 0; i < Math.Min(k, n); i++) root[i] = root[Math.Min(n - 1, k)];
            for (var i = Math.Max(0, n - k); i < n; i++) root[i] = root[Math.Max(0, n - 1 - k)];
            var mean = root.Average();
            var cum = new double[n];
            for (var i = 1; i < n; i++)
                cum[i] = cum[i - 1] + (s[i] - s[i - 1]) * ((root[i] + root[i - 1]) / 2 + 0.3 * mean + 1e-9) / ((f[i] + f[i - 1]) / 2);
            return cum;
        }

        /// <summary>True if the spline through the key points stays within tol × factor of the target, both ways, on every segment.</summary>
        private static bool Fits(IReadOnlyList<Vec2> t, IReadOnlyList<int> keys, double tol, double[] f)
            => FitsRange(t, keys, 0, keys.Count - 2, tol, f);

        /// <summary><see cref="Fits"/> on the segments <paramref name="from"/> … <paramref name="to"/> only.</summary>
        private static bool FitsRange(IReadOnlyList<Vec2> t, IReadOnlyList<int> keys, int from, int to, double tol, double[] f)
        {
            for (var j = Math.Max(0, from); j <= to && j + 1 < keys.Count; j++)
            {
                int a = keys[j], b = keys[j + 1];
                var seg = SplineSegment(t, keys, j);
                for (var i = a + 1; i < b; i++)
                    if (DistanceToPolyline(t[i], seg) > tol * f[i]) return false;
                // Spline → target: catches an overshoot (bulge or loop) between the target points.
                var limit = tol * f[(a + b) / 2];
                foreach (var q in seg)
                    if (DistanceToRange(q, t, a, b) > limit) return false;
            }
            return true;
        }

        /// <summary>
        /// Seed indices: the ends, turning points of x and y (zigzag with hysteresis <see cref="TurnHysteresis"/> × tol), corners
        /// and the steepest point between a peak and the next valley (or valley and peak).
        /// </summary>
        internal static SortedSet<int> Seeds(IReadOnlyList<Vec2> t, double tol)
        {
            var n = t.Count;
            var seeds = new SortedSet<int> { 0, n - 1 };
            var h = TurnHysteresis * tol;
            var ys = Zigzag(t.Select(p => p.Y).ToList(), h);
            foreach (var i in ys) seeds.Add(i);
            foreach (var i in Zigzag(t.Select(p => p.X).ToList(), h)) seeds.Add(i);
            // Inflections: the steepest point between two opposite turning points of y.
            for (var k = 0; k + 1 < ys.Count; k++)
            {
                int a = ys[k], b = ys[k + 1];
                if (b - a < 4) continue;
                double best = -1;
                var at = -1;
                for (var i = a + 1; i < b; i++)
                {
                    var d = t[Math.Min(n - 1, i + 1)] - t[Math.Max(0, i - 1)];
                    var slope = Math.Abs(d.Y) / Math.Max(1e-12, d.Length);
                    if (slope > best) { best = slope; at = i; }
                }
                if (at > 0) seeds.Add(at);
            }
            foreach (var i in Corners(t)) seeds.Add(i);
            return seeds;
        }

        /// <summary>Turning points of a sequence: extrema that the values leave again by at least <paramref name="h"/> (ends excluded).</summary>
        internal static List<int> Zigzag(IReadOnlyList<double> v, double h)
        {
            var result = new List<int>();
            int iMax = 0, iMin = 0, dir = 0;
            for (var i = 1; i < v.Count; i++)
            {
                if (dir >= 0 && v[i] > v[iMax]) iMax = i;
                if (dir <= 0 && v[i] < v[iMin]) iMin = i;
                if (dir >= 0 && v[iMax] - v[i] >= h)
                {
                    if (iMax > 0) result.Add(iMax);
                    dir = -1;
                    iMin = i;
                }
                else if (dir <= 0 && v[i] - v[iMin] >= h)
                {
                    if (iMin > 0) result.Add(iMin);
                    dir = 1;
                    iMax = i;
                }
            }
            return result;
        }

        /// <summary>Corners: points where the direction turns by at least <see cref="CornerAngle"/> within ±<see cref="CornerWindow"/> (local maxima).</summary>
        internal static List<int> Corners(IReadOnlyList<Vec2> t)
        {
            var n = t.Count;
            var s = new double[n];
            for (var i = 1; i < n; i++) s[i] = s[i - 1] + t[i].DistanceTo(t[i - 1]);
            var angle = new double[n];
            int a = 0, b = 0;
            for (var i = 1; i < n - 1; i++)
            {
                while (a < i && s[i] - s[a + 1] >= CornerWindow) a++;
                if (b < i) b = i;
                while (b < n - 1 && s[b] - s[i] < CornerWindow) b++;
                if (a >= i || b <= i || s[i] - s[a] < CornerWindow / 2 || s[b] - s[i] < CornerWindow / 2) continue;
                Vec2 u = t[i] - t[a], w = t[b] - t[i];
                if (u.Length < 1e-12 || w.Length < 1e-12) continue;
                angle[i] = Math.Abs(Math.Atan2(u.X * w.Y - u.Y * w.X, u.X * w.X + u.Y * w.Y)) * 180 / Math.PI;
            }
            var result = new List<int>();
            for (var i = 1; i < n - 1; i++)
            {
                if (angle[i] < CornerAngle) continue;
                var peak = true;
                for (var j = i - 1; j >= 0 && s[i] - s[j] < CornerWindow && peak; j--) if (angle[j] > angle[i]) peak = false;
                for (var j = i + 1; j < n && s[j] - s[i] < CornerWindow && peak; j++) if (angle[j] >= angle[i]) peak = false;
                if (peak) result.Add(i);
            }
            return result;
        }

        /// <summary>Ramer–Douglas–Peucker between consecutive <paramref name="seeds"/>, tolerance tol × factor of the farthest point.</summary>
        private static List<int> Rdp(IReadOnlyList<Vec2> t, SortedSet<int> seeds, double tol, double[] f)
        {
            var keep = new SortedSet<int>(seeds);
            var stack = new Stack<(int, int)>();
            var list = seeds.ToList();
            for (var k = 0; k + 1 < list.Count; k++) stack.Push((list[k], list[k + 1]));
            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                double worst = 0;
                var at = -1;
                for (var i = a + 1; i < b; i++)
                {
                    var d = t[i].DistanceTo(Geo.ClosestOnSegment(t[i], t[a], t[b]).Point) / (tol * f[i]);
                    if (d > worst) { worst = d; at = i; }
                }
                if (at < 0 || worst <= 1) continue;
                keep.Add(at);
                stack.Push((a, at));
                stack.Push((at, b));
            }
            return keep.ToList();
        }

        /// <summary>Segment <paramref name="j"/> of the chart spline through the key points, sampled (exactly <see cref="Draw.Spline"/>).</summary>
        private static List<Vec2> SplineSegment(IReadOnlyList<Vec2> t, IReadOnlyList<int> keys, int j)
        {
            var n = keys.Count;
            Vec2 P(int i) => t[keys[Math.Max(0, Math.Min(n - 1, i))]];
            Vec2 p0 = P(j - 1), p1 = P(j), p2 = P(j + 1), p3 = P(j + 2);
            var seg = new List<Vec2>(CheckSteps + 1) { p1 };
            if (n == 2) { seg.Add(p2); return seg; }
            Vec2 c1 = p1 + (p2 - p0) / 6, c2 = p2 - (p3 - p1) / 6;
            for (var k = 1; k <= CheckSteps; k++) seg.Add(Geo.CubicAt(p1, c1, c2, p2, (double)k / CheckSteps));
            return seg;
        }

        /// <summary>The chart spline through the points, sampled with <paramref name="steps"/> steps per segment (as drawn).</summary>
        public static IReadOnlyList<Vec2> SplineCurve(IReadOnlyList<Vec2> keys, int steps = 10)
        {
            if (keys.Count < 3) return keys.ToList();
            return Draw.Sample(Draw.Spline(keys), steps);
        }

        private static double DistanceToPolyline(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            var best = double.PositiveInfinity;
            for (var i = 0; i + 1 < poly.Count; i++) best = Math.Min(best, p.DistanceTo(Geo.ClosestOnSegment(p, poly[i], poly[i + 1]).Point));
            return poly.Count == 1 ? p.DistanceTo(poly[0]) : best;
        }

        private static double DistanceToRange(Vec2 p, IReadOnlyList<Vec2> t, int a, int b)
        {
            var best = double.PositiveInfinity;
            for (var i = a; i < b; i++) best = Math.Min(best, p.DistanceTo(Geo.ClosestOnSegment(p, t[i], t[i + 1]).Point));
            return best;
        }

        private static double[] Factors(int n, IReadOnlyList<double>? factor)
        {
            var f = new double[n];
            for (var i = 0; i < n; i++) f[i] = factor is not null && i < factor.Count && factor[i] > 0 && Fmt.Finite(factor[i]) ? factor[i] : 1;
            return f;
        }

        // ------------------------------------------------------------------ pace (drawing speed)

        /// <summary>
        /// Pace factor per trail point from the time stamps (ms, same count, never falling): the speed (plot sizes per second)
        /// over at least <see cref="PaceWindowMs"/> around the point, factor √(speed / <see cref="ReferenceSpeed"/>) clamped to
        /// <see cref="PaceMin"/> … <see cref="PaceMax"/>. Null without usable time stamps.
        /// </summary>
        public static IReadOnlyList<double>? Pace(IReadOnlyList<Vec2> norm, IReadOnlyList<double>? times)
        {
            if (times is null || times.Count != norm.Count || norm.Count < 2) return null;
            for (var i = 0; i < times.Count; i++)
                if (!Fmt.Finite(times[i]) || (i > 0 && times[i] < times[i - 1])) return null;
            var total = times[times.Count - 1] - times[0];
            if (!(total > 0)) return null;
            var n = norm.Count;
            var s = new double[n];
            for (var i = 1; i < n; i++) s[i] = s[i - 1] + norm[i].DistanceTo(norm[i - 1]);
            var window = Math.Min(PaceWindowMs, total);
            var result = new double[n];
            for (var i = 0; i < n; i++)
            {
                int a = i, b = i;
                while (times[b] - times[a] < window && (a > 0 || b < n - 1))
                {
                    if (a > 0 && (b == n - 1 || times[i] - times[a - 1] <= times[b + 1] - times[i])) a--;
                    else b++;
                }
                var dt = (times[b] - times[a]) / 1000;
                var speed = dt > 0 ? (s[b] - s[a]) / dt : s[n - 1] / (total / 1000);
                result[i] = Geo.Clamp(Math.Sqrt(speed / ReferenceSpeed), PaceMin, PaceMax);
            }
            return result;
        }

        /// <summary>
        /// The pace factors <paramref name="pace"/> (one per point of a trail, spread evenly over its arc length) at the points
        /// of <paramref name="target"/> (by their share of its arc length); null for no pace.
        /// </summary>
        public static IReadOnlyList<double>? Along(IReadOnlyList<Vec2> target, IReadOnlyList<double>? pace)
        {
            if (pace is null || pace.Count < 2 || target.Count == 0) return null;
            var n = target.Count;
            var s = new double[n];
            for (var i = 1; i < n; i++) s[i] = s[i - 1] + target[i].DistanceTo(target[i - 1]);
            var total = s[n - 1];
            var result = new double[n];
            for (var i = 0; i < n; i++) result[i] = At(pace, total > 0 ? s[i] / total : 0);
            return result;
        }

        /// <summary>The factor at share <paramref name="u"/> (0 … 1) of the evenly spread <paramref name="pace"/> (linear).</summary>
        internal static double At(IReadOnlyList<double> pace, double u)
        {
            var x = Geo.Clamp(u, 0, 1) * (pace.Count - 1);
            var i = Math.Min(pace.Count - 2, (int)Math.Floor(x));
            return pace[i] + (pace[i + 1] - pace[i]) * (x - i);
        }

        /// <summary>A factor rounded to the stored steps (<see cref="PaceStep"/>, within <see cref="PaceMin"/> … <see cref="PaceMax"/>).</summary>
        public static double Quantise(double f) => PaceMin + Digit(f) * PaceStep;

        /// <summary>The stored digit (0 … 9) of a factor.</summary>
        internal static int Digit(double f) => (int)Geo.Clamp(Math.Round((f - PaceMin) / PaceStep), 0, 9);
    }
}
