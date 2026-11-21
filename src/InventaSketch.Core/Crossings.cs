using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Where a line crosses another line of the same kind without being connected to it.</summary>
    public sealed record Crossing(string Other, Vec2 At, int Segment, bool Above);

    /// <summary>
    /// Crossings of wires and pipes (<see cref="ObjectType.HopsAtCrossings"/>): the line drawn later (higher in the page
    /// order) shows a small hop (bridge) where it crosses an earlier one, so crossings read as "not connected". Hosts offer a
    /// connect icon at each crossing (<see cref="Connect"/>: both parts of the line are docked onto the other line, a
    /// junction dot appears).
    /// </summary>
    public static class Crossings
    {
        /// <summary>Radius of a hop (mm).</summary>
        public const double HopRadius = 1.2;

        /// <summary>
        /// Crossings of line <paramref name="o"/> (drawn path <paramref name="path"/>) with the other lines of the same type on
        /// its page (their drawn paths from <paramref name="pathOf"/>): proper intersections away from the segment ends of both
        /// lines (ends docked onto a line are branches, not crossings). <see cref="Crossing.Segment"/> indexes
        /// <paramref name="path"/>.
        /// </summary>
        public static IReadOnlyList<Crossing> Find(SketchDocument doc, SketchObject o, IReadOnlyList<Vec2> path, Func<SketchObject, IReadOnlyList<Vec2>> pathOf)
        {
            var list = new List<Crossing>();
            if (path.Count < 2) return list;
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(o.Id));
            if (page is null) return list;
            var order = page.Order.ToList();
            var own = order.IndexOf(o.Id);
            foreach (var other in doc.ObjectsOn(page))
            {
                if (other.Id == o.Id || other.Type != o.Type || other.Points is null) continue;
                var q = pathOf(other);
                var above = own > order.IndexOf(other.Id);
                for (var i = 0; i + 1 < path.Count; i++)
                    for (var j = 0; j + 1 < q.Count; j++)
                        if (Intersect(path[i], path[i + 1], q[j], q[j + 1]) is { } x
                            && Clear(x, path[i], path[i + 1]) && Clear(x, q[j], q[j + 1]))
                            list.Add(new Crossing(other.Id, x, i, above));
            }
            return list;
        }

        /// <summary>
        /// Crossings of line <paramref name="o"/> for hosts: drawn paths from the lines' rails (<see cref="AnchorRole.Rail"/>);
        /// empty for types without <see cref="ObjectType.HopsAtCrossings"/>.
        /// </summary>
        public static IReadOnlyList<Crossing> Find(Registry registry, SketchDocument doc, SketchObject o, Lang lang = Lang.En)
        {
            if (registry.Type(o.Type)?.HopsAtCrossings != true) return Array.Empty<Crossing>();
            var ctx = new RenderContext(doc, lang);
            IReadOnlyList<Vec2> PathOf(SketchObject x)
            {
                var rails = registry.PageAnchors(x, ctx).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail).ToList();
                if (rails.Count == 0) return doc.ResolvePoints(x);
                var pts = new List<Vec2> { rails[0].P };
                foreach (var r in rails) { if (pts[pts.Count - 1].DistanceTo(r.P) > 1e-9) pts.Add(r.P); pts.Add(r.Q); }
                return pts;
            }
            return Find(doc, o, PathOf(o), PathOf);
        }

        /// <summary>The crossing lies away from both segment ends (an end on the other line is a branch, not a crossing).</summary>
        private static bool Clear(Vec2 x, Vec2 a, Vec2 b) => x.DistanceTo(a) > HopRadius * 1.5 && x.DistanceTo(b) > HopRadius * 1.5;

        private static Vec2? Intersect(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            var r = b - a;
            var s = d - c;
            var den = r.Cross(s);
            if (Math.Abs(den) < 1e-9) return null;
            var t = (c - a).Cross(s) / den;
            var u = (c - a).Cross(r) / den;
            if (t <= 0 || t >= 1 || u <= 0 || u >= 1) return null;
            return a + r * t;
        }

        /// <summary>
        /// The path with a hop (half circle of <see cref="HopRadius"/>, bulging up, or left on vertical segments) at every
        /// given crossing; a plain polyline when there is none.
        /// </summary>
        public static List<PathSeg> HopPath(IReadOnlyList<Vec2> path, IEnumerable<Crossing> hops)
        {
            var segs = new List<PathSeg> { PathSeg.Move(path[0]) };
            var bySeg = hops.GroupBy(h => h.Segment).ToDictionary(g => g.Key, g => g.Select(h => h.At).ToList());
            const double k = 4.0 / 3.0;
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                var len = a.DistanceTo(b);
                if (bySeg.TryGetValue(i, out var at) && len > 4 * HopRadius)
                {
                    var dir = (b - a) * (1 / len);
                    var n = new Vec2(dir.Y, -dir.X);
                    if (n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0)) n = n * -1;
                    var last = a;
                    foreach (var x in at.OrderBy(p => p.DistanceTo(a)))
                    {
                        var s = x - dir * HopRadius;
                        var e = x + dir * HopRadius;
                        // Hops too close to the previous one are skipped (they would overlap).
                        if ((s - last).Dot(dir) < 0) continue;
                        segs.Add(PathSeg.Line(s));
                        segs.Add(PathSeg.Cubic(s + n * (HopRadius * k), e + n * (HopRadius * k), e));
                        last = e;
                    }
                }
                segs.Add(PathSeg.Line(b));
            }
            return segs;
        }

        /// <summary>
        /// Connects line <paramref name="id"/> to line <paramref name="otherId"/> at a crossing: the line is split there and both
        /// parts are docked onto the other line (a junction; wires and pipes draw a dot, connection exports count it).
        /// </summary>
        public static SketchDocument Connect(Registry registry, SketchDocument doc, string id, Crossing crossing, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            var other = doc.Get(crossing.Other);
            if (line?.Points is null || other is null) return doc;
            var pts = doc.ResolvePoints(line);
            // Stored segment that carries the crossing (routed wires may draw more segments than they store).
            var seg = 0;
            var best = double.MaxValue;
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var d = Geo.ClosestOnSegment(crossing.At, pts[i], pts[i + 1]).Point.DistanceTo(crossing.At);
                if (d < best) { best = d; seg = i; }
            }
            var next = Polylines.InsertCorner(registry, doc, id, seg + 1, crossing.At, lang);
            var (split, newId) = Polylines.Split(registry, next, id, seg + 1, lang);
            if (newId is null) return doc;
            var rail = registry.PageAnchors(split.Get(crossing.Other)!, new RenderContext(split, lang))
                .Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail && a.Id is not null)
                .OrderBy(a => Geo.ClosestOnSegment(crossing.At, a.P, a.Q).Point.DistanceTo(crossing.At)).FirstOrDefault();
            if (rail is null) return doc;
            var on = Geo.ClosestOnSegment(crossing.At, rail.P, rail.Q).Point;
            var target = split.Get(crossing.Other)!;
            var first = split.Get(id)!;
            split = Edits.SetPoint(split, id, first.Points!.Count - 1, on, Attachments.For(target, on, rail));
            split = Edits.SetPoint(split, newId, 0, on, Attachments.For(target, on, rail));
            return split;
        }
    }
}
