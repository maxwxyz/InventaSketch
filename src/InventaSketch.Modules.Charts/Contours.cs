using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Contour lines (isolines) of a sampled scalar field by marching squares, stitched into polylines.</summary>
    internal static class Contours
    {
        /// <summary>
        /// Isolines of <paramref name="v"/> (v[i, j] at x = xs[i], y = ys[j]) at <paramref name="level"/>, as polylines in
        /// (x, y). Saddle cells are resolved with the cell average. Cells with a NaN corner are skipped.
        /// </summary>
        public static List<List<Vec2>> Trace(double[,] v, IReadOnlyList<double> xs, IReadOnlyList<double> ys, double level)
        {
            int nx = xs.Count, ny = ys.Count;
            var points = new Dictionary<long, Vec2>();
            var segments = new List<(long A, long B)>();
            long H(int i, int j) => ((long)j * nx + i) * 2;      // edge (i,j)-(i+1,j)
            long V(int i, int j) => ((long)j * nx + i) * 2 + 1;  // edge (i,j)-(i,j+1)
            Vec2 Cross(double x0, double y0, double v0, double x1, double y1, double v1)
            {
                var t = Math.Abs(v1 - v0) < 1e-12 ? 0.5 : Geo.Clamp((level - v0) / (v1 - v0), 0, 1);
                return new Vec2(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t);
            }
            for (var j = 0; j + 1 < ny; j++)
                for (var i = 0; i + 1 < nx; i++)
                {
                    double a = v[i, j], b = v[i + 1, j], c = v[i + 1, j + 1], d = v[i, j + 1];
                    if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c) || double.IsNaN(d)) continue;
                    bool ia = a >= level, ib = b >= level, ic = c >= level, id = d >= level;
                    // Edges: 0 bottom (a-b), 1 right (b-c), 2 top (d-c), 3 left (a-d).
                    var edges = new long[] { H(i, j), V(i + 1, j), H(i, j + 1), V(i, j) };
                    var cross = new bool[] { ia != ib, ib != ic, id != ic, ia != id };
                    void Point(int e)
                    {
                        if (points.ContainsKey(edges[e])) return;
                        points[edges[e]] = e switch
                        {
                            0 => Cross(xs[i], ys[j], a, xs[i + 1], ys[j], b),
                            1 => Cross(xs[i + 1], ys[j], b, xs[i + 1], ys[j + 1], c),
                            2 => Cross(xs[i], ys[j + 1], d, xs[i + 1], ys[j + 1], c),
                            _ => Cross(xs[i], ys[j], a, xs[i], ys[j + 1], d),
                        };
                    }
                    var list = Enumerable.Range(0, 4).Where(e => cross[e]).ToList();
                    if (list.Count == 2)
                    {
                        Point(list[0]); Point(list[1]);
                        segments.Add((edges[list[0]], edges[list[1]]));
                    }
                    else if (list.Count == 4)
                    {
                        for (var e = 0; e < 4; e++) Point(e);
                        var centre = (a + b + c + d) / 4 >= level;
                        // Isolate the corners whose state differs from the centre: a (left, bottom), b (bottom, right), c (right, top), d (top, left).
                        if (ia != centre) segments.Add((edges[3], edges[0]));
                        if (ib != centre) segments.Add((edges[0], edges[1]));
                        if (ic != centre) segments.Add((edges[1], edges[2]));
                        if (id != centre) segments.Add((edges[2], edges[3]));
                    }
                }
            // Stitch segments sharing edge points into polylines.
            var byPoint = new Dictionary<long, List<int>>();
            for (var s = 0; s < segments.Count; s++)
            {
                foreach (var key in new[] { segments[s].A, segments[s].B })
                {
                    if (!byPoint.TryGetValue(key, out var l)) byPoint[key] = l = new List<int>();
                    l.Add(s);
                }
            }
            var used = new bool[segments.Count];
            var result = new List<List<Vec2>>();
            for (var s = 0; s < segments.Count; s++)
            {
                if (used[s]) continue;
                used[s] = true;
                var chain = new LinkedList<long>();
                chain.AddLast(segments[s].A);
                chain.AddLast(segments[s].B);
                Extend(chain, true, byPoint, segments, used);
                Extend(chain, false, byPoint, segments, used);
                result.Add(chain.Select(k => points[k]).ToList());
            }
            return result;
        }

        private static void Extend(LinkedList<long> chain, bool atEnd, Dictionary<long, List<int>> byPoint, List<(long A, long B)> segments, bool[] used)
        {
            while (true)
            {
                var key = atEnd ? chain.Last!.Value : chain.First!.Value;
                var next = FirstWhere(byPoint[key], i => !used[i], -1);
                if (next < 0) return;
                used[next] = true;
                var other = segments[next].A == key ? segments[next].B : segments[next].A;
                if (atEnd) chain.AddLast(other); else chain.AddFirst(other);
                if (other == (atEnd ? chain.First!.Value : chain.Last!.Value)) return; // closed loop
            }
        }

        private static int FirstWhere(IEnumerable<int> source, Func<int, bool> predicate, int fallback)
        {
            foreach (var i in source) if (predicate(i)) return i;
            return fallback;
        }

        /// <summary>
        /// Cuts a polyline into the pieces where <paramref name="keep"/> holds; piece ends are refined by bisection.
        /// </summary>
        public static List<List<Vec2>> Keep(IReadOnlyList<Vec2> line, Func<Vec2, bool> keep)
        {
            var pieces = new List<List<Vec2>>();
            if (line.Count < 2) return pieces;
            List<Vec2>? cur = null;
            Vec2 Boundary(Vec2 a, Vec2 b, bool aIn)
            {
                for (var k = 0; k < 20; k++)
                {
                    var m = Vec2.Lerp(a, b, 0.5);
                    if (keep(m) == aIn) a = m; else b = m;
                }
                return Vec2.Lerp(a, b, 0.5);
            }
            var prevIn = keep(line[0]);
            if (prevIn) { cur = new List<Vec2> { line[0] }; pieces.Add(cur); }
            for (var i = 1; i < line.Count; i++)
            {
                var qIn = keep(line[i]);
                if (qIn != prevIn)
                {
                    var x = Boundary(line[i - 1], line[i], prevIn);
                    if (prevIn) { cur!.Add(x); cur = null; }
                    else { cur = new List<Vec2> { x }; pieces.Add(cur); }
                }
                if (qIn) cur!.Add(line[i]);
                prevIn = qIn;
            }
            return pieces.Where(p => p.Count >= 2).ToList();
        }
    }
}
