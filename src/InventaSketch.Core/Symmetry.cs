using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Which of the quick transforms visibly change a drawing (the others are left out of the bar).</summary>
    public readonly record struct TransformEffect(bool MirrorH, bool MirrorV, bool Rotate90)
    {
        public static readonly TransformEffect All = new(true, true, true);
    }

    /// <summary>
    /// Symmetry of rendered drawings: a quick transform (mirror horizontally/vertically about the centre, rotate by 90°)
    /// "changes nothing" when the drawing maps onto itself – a circle, a gear in axial view, a symmetric bearing. Hosts
    /// show only the transforms that make a difference. Works on the drawn geometry, so every element type is covered
    /// without declaring its symmetry; texts count as oriented (mirroring a label changes it).
    /// </summary>
    public static class Symmetry
    {
        public static TransformEffect Of(IEnumerable<Primitive> primitives)
        {
            var pts = Sample(Prim.Flatten(primitives)).ToList();
            if (pts.Count == 0) return TransformEffect.All;
            var box = Rect.FromPoints(pts);
            var c = new Vec2(box.X + box.W / 2, box.Y + box.H / 2);
            var tol = Math.Max(0.05, 0.01 * Math.Max(box.W, box.H));
            var index = new PointIndex(pts, tol);
            bool Changes(Func<Vec2, Vec2> f) => pts.Any(p => !index.Has(f(p)));
            return new TransformEffect(
                Changes(p => new Vec2(2 * c.X - p.X, p.Y)),
                Changes(p => new Vec2(p.X, 2 * c.Y - p.Y)),
                Changes(p => new Vec2(c.X - (p.Y - c.Y), c.Y + (p.X - c.X))));
        }

        /// <summary>Points along the primitives: ends, quarter points, corners, points around ellipses, text direction.</summary>
        private static IEnumerable<Vec2> Sample(IEnumerable<Primitive> list)
        {
            foreach (var p in list)
            {
                switch (p)
                {
                    case LinePrim l:
                        for (var i = 0; i <= 4; i++) yield return Vec2.Lerp(l.A, l.B, i / 4.0);
                        foreach (var q in Heads(l.Style, l.A, l.B, l.B, l.A)) yield return q;
                        break;
                    case RectPrim r:
                        foreach (var q in Edges(new[] { new Vec2(r.X, r.Y), new Vec2(r.X + r.W, r.Y), new Vec2(r.X + r.W, r.Y + r.H), new Vec2(r.X, r.Y + r.H) }, true)) yield return q;
                        break;
                    case EllipsePrim e:
                        for (var i = 0; i < 24; i++)
                        {
                            var a = 2 * Math.PI * i / 24;
                            yield return new Vec2(e.Center.X + e.Rx * Math.Cos(a), e.Center.Y + e.Ry * Math.Sin(a));
                        }
                        break;
                    case PathPrim path:
                        var ends = path.Segments.Where(x => x.Op != SegOp.Z).Select(x => x.P).ToList();
                        if (ends.Count >= 2)
                            foreach (var q in Heads(path.Style, ends[0], ends[1], ends[ends.Count - 1], ends[ends.Count - 2])) yield return q;
                        var prev = (Vec2?)null;
                        var start = (Vec2?)null;
                        foreach (var s in path.Segments)
                        {
                            if (s.Op == SegOp.Z)
                            {
                                if (prev is { } a && start is { } b) for (var i = 1; i < 4; i++) yield return Vec2.Lerp(a, b, i / 4.0);
                                prev = start;
                                continue;
                            }
                            if (s.Op == SegOp.M) { start = s.P; yield return s.P; prev = s.P; continue; }
                            if (prev is { } from)
                            {
                                if (s.Op == SegOp.L) for (var i = 1; i < 4; i++) yield return Vec2.Lerp(from, s.P, i / 4.0);
                                else if (s.Op == SegOp.C) for (var i = 1; i < 4; i++) yield return Cubic(from, s.C1, s.C2, s.P, i / 4.0);
                                else if (s.Op == SegOp.Q) for (var i = 1; i < 4; i++) yield return Cubic(from, from + (s.C1 - from) * (2.0 / 3), s.P + (s.C1 - s.P) * (2.0 / 3), s.P, i / 4.0);
                            }
                            yield return s.P;
                            prev = s.P;
                        }
                        break;
                    case TextPrim t:
                        // Oriented: the anchor and a point along the reading direction.
                        var len = Math.Max(1, t.Text.Length) * t.TextStyle.Size * 0.5;
                        var r0 = Geo.DegToRad(t.Rotation);
                        yield return t.Position;
                        yield return t.Position + new Vec2(Math.Cos(r0), Math.Sin(r0)) * len;
                        yield return t.Position + new Vec2(-Math.Sin(r0), Math.Cos(r0)) * (t.TextStyle.Size * 0.3);
                        break;
                }
            }
        }

        /// <summary>Arrowheads are line-end markers in the style, not geometry: two wing points per head.</summary>
        private static IEnumerable<Vec2> Heads(Style style, Vec2 start, Vec2 afterStart, Vec2 end, Vec2 beforeEnd)
        {
            IEnumerable<Vec2> Wings(Vec2 tip, Vec2 from)
            {
                var d = tip - from;
                if (d.Length < 1e-9) yield break;
                var u = d.Normalized();
                yield return tip - u * 2 + u.Perp * 0.8;
                yield return tip - u * 2 - u.Perp * 0.8;
            }
            if (style.ArrowStart is { } a && a != ArrowHead.None) foreach (var q in Wings(start, afterStart)) yield return q;
            if (style.ArrowEnd is { } b && b != ArrowHead.None) foreach (var q in Wings(end, beforeEnd)) yield return q;
        }

        private static IEnumerable<Vec2> Edges(IReadOnlyList<Vec2> corners, bool closed)
        {
            var n = closed ? corners.Count : corners.Count - 1;
            for (var i = 0; i < n; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % corners.Count];
                for (var k = 0; k < 4; k++) yield return Vec2.Lerp(a, b, k / 4.0);
            }
        }

        private static Vec2 Cubic(Vec2 a, Vec2 b, Vec2 c, Vec2 d, double t)
        {
            var u = 1 - t;
            return a * (u * u * u) + b * (3 * u * u * t) + c * (3 * u * t * t) + d * (t * t * t);
        }

        /// <summary>Grid hash of points for "is there a point within the tolerance" queries.</summary>
        private sealed class PointIndex
        {
            private readonly Dictionary<(long, long), List<Vec2>> _cells = new();
            private readonly double _tol;

            public PointIndex(IEnumerable<Vec2> pts, double tol)
            {
                _tol = tol;
                foreach (var p in pts)
                {
                    var k = Key(p);
                    if (!_cells.TryGetValue(k, out var list)) _cells[k] = list = new List<Vec2>();
                    list.Add(p);
                }
            }

            private (long, long) Key(Vec2 p) => ((long)Math.Floor(p.X / _tol), (long)Math.Floor(p.Y / _tol));

            public bool Has(Vec2 p)
            {
                var (kx, ky) = Key(p);
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                        if (_cells.TryGetValue((kx + dx, ky + dy), out var list) && list.Any(q => q.DistanceTo(p) <= _tol)) return true;
                return false;
            }
        }
    }
}
