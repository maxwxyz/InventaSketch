using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Computes explicit hatch line segments clipped to one or more closed contours (even-odd rule,
    /// so holes/islands stay empty). Used for hosts without native pattern fills and for exact SVG/PDF output.
    /// </summary>
    public static class Hatching
    {
        /// <summary>Hatch segments for the given contours. Angle in degrees, counter-clockwise on screen.</summary>
        public static List<(Vec2 A, Vec2 B)> Segments(IReadOnlyList<IReadOnlyList<Vec2>> contours, HatchPattern pattern, double phase = 0)
        {
            var result = Family(contours, pattern.Angle, pattern.Spacing, phase);
            if (pattern.Cross) result.AddRange(Family(contours, pattern.Angle + 90, pattern.Spacing, phase));
            return result;
        }

        private static List<(Vec2, Vec2)> Family(IReadOnlyList<IReadOnlyList<Vec2>> contours, double angleDeg, double spacing, double phase)
        {
            var result = new List<(Vec2, Vec2)>();
            if (spacing <= Geo.Eps || contours.Count == 0) return result;
            // Screen y points down: a visually counter-clockwise angle is a negative rotation in page space.
            var rad = -Geo.DegToRad(angleDeg);
            // Rotate geometry so hatch lines become horizontal scanlines.
            var toLocal = Matrix2D.Rotation(-rad);
            var toPage = Matrix2D.Rotation(rad);
            var local = contours.Where(c => c.Count >= 3).Select(c => c.Select(toLocal.Apply).ToList()).ToList();
            if (local.Count == 0) return result;
            var minY = local.SelectMany(c => c).Min(p => p.Y);
            var maxY = local.SelectMany(c => c).Max(p => p.Y);
            var start = Math.Floor((minY - phase) / spacing) * spacing + phase;
            var xs = new List<double>();
            for (var y = start; y <= maxY + Geo.Eps; y += spacing)
            {
                if (y <= minY + 1e-7) continue;
                xs.Clear();
                foreach (var c in local)
                {
                    for (var i = 0; i < c.Count; i++)
                    {
                        var a = c[i]; var b = c[(i + 1) % c.Count];
                        if ((a.Y > y) == (b.Y > y)) continue; // half-open rule avoids double counting vertices
                        xs.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                    }
                }
                xs.Sort();
                for (var i = 0; i + 1 < xs.Count; i += 2)
                {
                    if (xs[i + 1] - xs[i] < 1e-6) continue;
                    result.Add((toPage.Apply(new Vec2(xs[i], y)), toPage.Apply(new Vec2(xs[i + 1], y))));
                }
            }
            return result;
        }

        /// <summary>Flattens primitive outlines into polygons (curves sampled) for hatching/hit-testing.</summary>
        public static List<IReadOnlyList<Vec2>> Contours(Primitive p, int curveSteps = 12)
        {
            var list = new List<IReadOnlyList<Vec2>>();
            switch (p)
            {
                case RectPrim r:
                    list.Add(new[] { new Vec2(r.X, r.Y), new Vec2(r.X + r.W, r.Y), new Vec2(r.X + r.W, r.Y + r.H), new Vec2(r.X, r.Y + r.H) });
                    break;
                case EllipsePrim e:
                    list.Add(Geo.ArcPoints(e.Center, e.Rx, e.Ry, 0, 2 * Math.PI, 64).Take(64).ToList());
                    break;
                case PathPrim path:
                    {
                        List<Vec2>? cur = null;
                        var last = Vec2.Zero;
                        foreach (var s in path.Segments)
                        {
                            switch (s.Op)
                            {
                                case SegOp.M:
                                    if (cur is { Count: >= 3 }) list.Add(cur);
                                    cur = new List<Vec2> { s.P }; last = s.P; break;
                                case SegOp.L:
                                    (cur ??= new List<Vec2>()).Add(s.P); last = s.P; break;
                                case SegOp.C:
                                    cur ??= new List<Vec2>();
                                    for (var i = 1; i <= curveSteps; i++) cur.Add(Geo.CubicAt(last, s.C1, s.C2, s.P, (double)i / curveSteps));
                                    last = s.P; break;
                                case SegOp.Q:
                                    cur ??= new List<Vec2>();
                                    for (var i = 1; i <= curveSteps; i++) cur.Add(Geo.QuadAt(last, s.C1, s.P, (double)i / curveSteps));
                                    last = s.P; break;
                                case SegOp.Z:
                                    if (cur is { Count: >= 3 }) list.Add(cur);
                                    cur = null; break;
                            }
                        }
                        if (cur is { Count: >= 3 }) list.Add(cur);
                        break;
                    }
            }
            return list;
        }
    }
}
