using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Where a line crosses another line (of any module that hops at crossings) without being connected to it.</summary>
    public sealed record Crossing(string Other, Vec2 At, int Segment, bool Above);

    /// <summary>A line end that takes part in a <see cref="Junction"/>: the line and the index of its end point.</summary>
    public sealed record JunctionEnd(string Line, int Index);

    /// <summary>
    /// A junction of lines that hop at crossings (<see cref="ObjectType.HopsAtCrossings"/>): line ends docked at one place
    /// onto the rail of the line <see cref="Host"/> (a T or X junction, drawn with a dot), or onto a junction element
    /// (<see cref="Host"/> a box object such as a junction dot). The reverse of <see cref="Crossings.Connect"/> is
    /// <see cref="Crossings.Disconnect"/>.
    /// </summary>
    public sealed record Junction(string Host, Vec2 At, IReadOnlyList<JunctionEnd> Ends);

    /// <summary>
    /// Crossings of wires and pipes (<see cref="ObjectType.HopsAtCrossings"/>): the line drawn later (higher in the page
    /// order) shows a small hop (bridge) where it crosses an earlier one, so crossings read as "not connected". One rule
    /// for every line family: a hydraulic line hops over an electric wire, a process pipe over an energy cable, as over a
    /// line of its own kind. Hosts offer a
    /// connect icon at each crossing (<see cref="Connect"/>: both parts of the line are docked onto the other line, a
    /// junction dot appears) and, for two selected lines that cross, "Connect (junction)" (<see cref="ConnectBetween"/>).
    /// The reverse turns a junction back into a crossing with a hop (<see cref="Disconnect"/>, "Make bridge (not
    /// connected)"): line ends meeting from opposite sides are joined into one line passing over, a single branch is
    /// carried a little beyond the other line, and a junction element in between is removed.
    /// </summary>
    public static class Crossings
    {
        /// <summary>Radius of a hop (mm).</summary>
        public const double HopRadius = 1.2;

        /// <summary>
        /// Crossings of line <paramref name="o"/> (drawn path <paramref name="path"/>) with the other lines on its page: lines of
        /// the same type with their drawn paths from <paramref name="pathOf"/>, and – given the <paramref name="registry"/> –
        /// lines of other types that hop at crossings too, with their drawn paths from their rails. Proper intersections away
        /// from the segment ends of both lines (ends docked onto a line are branches, not crossings).
        /// <see cref="Crossing.Segment"/> indexes <paramref name="path"/>.
        /// </summary>
        public static IReadOnlyList<Crossing> Find(SketchDocument doc, SketchObject o, IReadOnlyList<Vec2> path, Func<SketchObject, IReadOnlyList<Vec2>> pathOf, Registry? registry = null)
        {
            var list = new List<Crossing>();
            if (path.Count < 2) return list;
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(o.Id));
            if (page is null) return list;
            var order = page.Order.ToList();
            var own = order.IndexOf(o.Id);
            RenderContext? ctx = null;
            foreach (var other in doc.ObjectsOn(page))
            {
                if (other.Id == o.Id || other.Points is null) continue;
                IReadOnlyList<Vec2> q;
                if (other.Type == o.Type) q = pathOf(other);
                else if (registry?.Type(other.Type)?.HopsAtCrossings == true) q = RailPath(registry, doc, other, ctx ??= new RenderContext(doc));
                else continue;
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
            IReadOnlyList<Vec2> PathOf(SketchObject x) => RailPath(registry, doc, x, ctx);
            return Find(doc, o, PathOf(o), PathOf, registry);
        }

        /// <summary>The drawn path of a line from its rails (<see cref="AnchorRole.Rail"/>), else its points.</summary>
        private static IReadOnlyList<Vec2> RailPath(Registry registry, SketchDocument doc, SketchObject x, RenderContext ctx)
        {
            var rails = registry.PageAnchors(x, ctx).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail).ToList();
            if (rails.Count == 0) return doc.ResolvePoints(x);
            var pts = new List<Vec2> { rails[0].P };
            foreach (var r in rails) { if (pts[pts.Count - 1].DistanceTo(r.P) > 1e-9) pts.Add(r.P); pts.Add(r.Q); }
            return pts;
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
        /// Connects line <paramref name="id"/> to the other line of <paramref name="crossing"/> at that crossing: the line is split there and both
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

        // ------------------------------------------------------------------ connect two lines, junctions and bridges

        /// <summary>How far a single branch is carried beyond the other line when its junction becomes a bridge (mm).</summary>
        public const double BridgeOvershoot = 3.0;

        /// <summary>Line ends this close to a junction belong to it (mm).</summary>
        private const double JunctionTolerance = 0.3;

        /// <summary>The crossings of line <paramref name="a"/> with line <paramref name="b"/> (either may be the one that hops).</summary>
        public static IReadOnlyList<Crossing> Between(Registry registry, SketchDocument doc, string a, string b, Lang lang = Lang.En)
        {
            var la = doc.Get(a);
            var lb = doc.Get(b);
            if (la is null || lb is null || a == b) return Array.Empty<Crossing>();
            var list = Find(registry, doc, la, lang).Where(c => c.Other == b).ToList();
            return list.Count > 0 ? list : Find(registry, doc, lb, lang).Where(c => c.Other == a).ToList();
        }

        /// <summary>
        /// Connects two lines at every place where they cross (<see cref="Connect"/> at each crossing): the parts of
        /// <paramref name="a"/> (or of <paramref name="b"/>, if only that one hops at crossings) are docked onto the other line.
        /// The document is returned unchanged if the two do not cross.
        /// </summary>
        public static SketchDocument ConnectBetween(Registry registry, SketchDocument doc, string a, string b, Lang lang = Lang.En)
        {
            if (doc.Get(a) is not { } la || doc.Get(b) is null) return doc;
            // The line that is split: a, unless only b finds the crossings (a does not hop at crossings).
            var (split, other) = Find(registry, doc, la, lang).Any(c => c.Other == b) ? (a, b) : (b, a);
            var parts = new List<string> { split };
            for (var k = 0; k < 16; k++)
            {
                string? on = null;
                Crossing? crossing = null;
                foreach (var id in parts)
                {
                    if (doc.Get(id) is not { } line) continue;
                    crossing = Find(registry, doc, line, lang).FirstOrDefault(c => c.Other == other);
                    if (crossing is not null) { on = id; break; }
                }
                if (on is null || crossing is null) break;
                var before = new HashSet<string>(doc.Objects.Keys, StringComparer.Ordinal);
                var next = Connect(registry, doc, on, crossing, lang);
                if (ReferenceEquals(next, doc)) break;
                parts.AddRange(next.Objects.Keys.Where(key => !before.Contains(key)));
                doc = next;
            }
            return doc;
        }

        /// <summary>
        /// The junctions line <paramref name="o"/> takes part in: its own ends docked onto the rail of another line, and the
        /// ends of other lines docked onto its rails (each place once, with every line end docked there). For a box object
        /// (a junction element), the junction of the lines docked on it. Only lines that hop at crossings count.
        /// </summary>
        public static IReadOnlyList<Junction> Junctions(Registry registry, SketchDocument doc, SketchObject o)
        {
            var list = new List<Junction>();
            if (!o.IsPointGeometry)
            {
                if (JunctionAt(registry, doc, o.Id, null) is { } j) list.Add(j);
                return list;
            }
            if (registry.Type(o.Type)?.HopsAtCrossings != true || o.Points is null) return list;
            var places = new List<(string Host, Vec2 At)>();
            var pts = doc.ResolvePoints(o);
            if (o.Attach is not null)
                foreach (var i in Ends(pts.Count))
                    if (i < o.Attach.Count && o.Attach[i] is { } a && IsRail(a) && doc.Get(a.ObjectId) is { IsPointGeometry: true } host && Hops(registry, host))
                        places.Add((host.Id, pts[i]));
            foreach (var x in DockedLines(registry, doc, o.Id, rails: true))
                places.Add((o.Id, x.At));
            foreach (var (host, at) in places)
            {
                if (list.Any(j => j.Host == host && j.At.DistanceTo(at) <= JunctionTolerance)) continue;
                if (JunctionAt(registry, doc, host, at) is { } j) list.Add(j);
            }
            return list;
        }

        /// <summary>
        /// The junctions that at least two of the lines <paramref name="ids"/> take part in (as the line passing or with a
        /// docked end), each once, and that can become a bridge: "Make bridge (not connected)" for a multi-selection.
        /// </summary>
        public static IReadOnlyList<Junction> SharedJunctions(Registry registry, SketchDocument doc, IReadOnlyCollection<string> ids, Lang lang = Lang.En)
        {
            var set = new HashSet<string>(ids, StringComparer.Ordinal);
            var list = new List<Junction>();
            foreach (var id in ids)
            {
                if (doc.Get(id) is not { IsPointGeometry: true } o) continue;
                foreach (var j in Junctions(registry, doc, o))
                {
                    if (list.Any(x => x.Host == j.Host && x.At.DistanceTo(j.At) <= JunctionTolerance)) continue;
                    var involved = j.Ends.Select(e => e.Line).Append(j.Host).Where(set.Contains).Distinct().Count();
                    if (involved >= 2 && CanDisconnect(registry, doc, j, lang)) list.Add(j);
                }
            }
            return list;
        }

        /// <summary>
        /// <see cref="Disconnect"/> at each of <paramref name="junctions"/> in turn (each looked up again in the changed
        /// document); <paramref name="keep"/> keeps its id where it is joined with another line.
        /// </summary>
        public static SketchDocument DisconnectAll(Registry registry, SketchDocument doc, IEnumerable<Junction> junctions, string? keep = null, Lang lang = Lang.En)
        {
            foreach (var j in junctions.ToList()) doc = Disconnect(registry, doc, j, keep, lang);
            return doc;
        }

        /// <summary>
        /// The junction at <paramref name="host"/>: line ends docked on its rails near <paramref name="at"/> (host a line), or
        /// docked anywhere on it (host a junction element, <paramref name="at"/> may be null). Null if no such end exists.
        /// </summary>
        public static Junction? JunctionAt(Registry registry, SketchDocument doc, string host, Vec2? at)
        {
            var h = doc.Get(host);
            if (h is null) return null;
            var rails = h.IsPointGeometry;
            var ends = DockedLines(registry, doc, host, rails)
                .Where(x => at is not { } p || x.At.DistanceTo(p) <= JunctionTolerance)
                .ToList();
            if (ends.Count == 0) return null;
            var place = at ?? (rails ? ends[0].At : new Vec2(ends.Average(e => e.At.X), ends.Average(e => e.At.Y)));
            return new Junction(host, place, ends.Select(e => e.End).ToList());
        }

        /// <summary>
        /// True if <see cref="Disconnect"/> turns the junction into a crossing with a hop: on a line any docked end does; on a
        /// junction element two ends must meet from opposite sides (one line passing over) and at least one more line remain.
        /// </summary>
        public static bool CanDisconnect(Registry registry, SketchDocument doc, Junction junction, Lang lang = Lang.En)
        {
            var host = doc.Get(junction.Host);
            if (host is null) return false;
            var current = JunctionAt(registry, doc, junction.Host, host.IsPointGeometry ? junction.At : null);
            if (current is null) return false;
            if (host.IsPointGeometry) return true;
            var (pairs, singles) = Plan(registry, doc, current, lang);
            return pairs.Count >= 1 && pairs.Count + singles.Count >= 2;
        }

        /// <summary>
        /// Turns a junction back into a crossing with a hop ("Make bridge (not connected)"), the reverse of
        /// <see cref="Connect"/>: two line ends meeting from opposite sides are joined into one line that passes the place
        /// undocked (the corner left there is removed when the line runs straight through, else moved a little off the other
        /// line so it crosses it); a single end is undocked and carried <see cref="BridgeOvershoot"/> beyond the place, so it
        /// crosses the other line. A junction element in between is removed. Lines keep their ids where possible: when two
        /// are joined, <paramref name="keep"/> (if one of them) keeps its id.
        /// </summary>
        public static SketchDocument Disconnect(Registry registry, SketchDocument doc, Junction junction, string? keep = null, Lang lang = Lang.En)
        {
            var host = doc.Get(junction.Host);
            if (host is null) return doc;
            var onLine = host.IsPointGeometry;
            var current = JunctionAt(registry, doc, junction.Host, onLine ? junction.At : null);
            if (current is null) return doc;
            var at = current.At;
            var (pairs, singles) = Plan(registry, doc, current, lang);
            // Singles first: they keep their point indices.
            foreach (var (end, outward) in singles)
                doc = Edits.SetPoint(doc, end.Line, end.Index, at - outward * BridgeOvershoot, null);
            // The host's direction at the place (lines): the joint moves off it when the joined line bends there.
            Vec2? across = null;
            if (onLine)
            {
                var rail = HostRails(registry, doc, host, lang).OrderBy(r => Geo.ClosestOnSegment(at, r.P, r.Q).Point.DistanceTo(at)).FirstOrDefault();
                if (rail is not null && (rail.Q - rail.P).Length > Geo.Eps) across = (rail.Q - rail.P).Normalized().Perp;
            }
            foreach (var ((ea, oa), (eb, ob)) in pairs)
            {
                var (first, second, outSecond) = eb.Line == keep ? (eb, ea, oa) : (ea, eb, ob);
                var joined = Polylines.Join(registry, doc, first.Line, first.Index == 0, second.Line, second.Index == 0, lang);
                if (ReferenceEquals(joined, doc)) continue;
                doc = joined;
                var line = doc.Get(first.Line);
                if (line?.Points is null) continue;
                var pts = doc.ResolvePoints(line);
                var joint = -1;
                var best = double.MaxValue;
                for (var i = 1; i + 1 < pts.Count; i++)
                {
                    var d = pts[i].DistanceTo(at);
                    if (d < best) { best = d; joint = i; }
                }
                if (joint < 0 || best > JunctionTolerance * 2) continue;
                if (oa.Dot(ob) < -0.98) doc = Polylines.RemoveCorner(registry, doc, first.Line, joint, lang);
                else if (across is { } n)
                {
                    // Off the other line, to the side the second part leaves to: the line crosses it once, beside the corner.
                    var side = n.Dot(outSecond) >= 0 ? 1.0 : -1.0;
                    doc = Edits.SetPoint(doc, first.Line, joint, at + n * (side * BridgeOvershoot), null);
                }
            }
            // A junction element with no line docked on it any more goes.
            if (!onLine && !doc.Objects.Values.Any(x => x.Attach is not null && x.Attach.Any(a => a?.ObjectId == host.Id)))
                doc = Edits.Remove(doc, new[] { host.Id });
            return doc;
        }

        /// <summary>
        /// The ends of a junction paired into lines passing straight on (same type, meeting from opposite sides, the most
        /// opposite first) and the remaining single ends, each with its outward direction along its drawn path.
        /// </summary>
        private static (List<((JunctionEnd End, Vec2 Out) A, (JunctionEnd End, Vec2 Out) B)> Pairs, List<(JunctionEnd End, Vec2 Out)> Singles) Plan(
            Registry registry, SketchDocument doc, Junction junction, Lang lang)
        {
            // A line with both ends at the junction (a loop) is left alone.
            var ends = junction.Ends.GroupBy(e => e.Line).Where(g => g.Count() == 1).Select(g => g.First())
                .Select(e => (End: e, Out: Outward(registry, doc, e, lang))).Where(e => e.Out.Length > 0.5).ToList();
            var candidates = new List<(int I, int J, double Dot)>();
            for (var i = 0; i < ends.Count; i++)
                for (var j = i + 1; j < ends.Count; j++)
                {
                    var d = ends[i].Out.Dot(ends[j].Out);
                    if (d < -0.7 && doc.Get(ends[i].End.Line)?.Type == doc.Get(ends[j].End.Line)?.Type) candidates.Add((i, j, d));
                }
            var used = new HashSet<int>();
            var pairs = new List<((JunctionEnd, Vec2), (JunctionEnd, Vec2))>();
            foreach (var (i, j, _) in candidates.OrderBy(c => c.Dot))
            {
                if (used.Contains(i) || used.Contains(j)) continue;
                used.Add(i);
                used.Add(j);
                pairs.Add((ends[i], ends[j]));
            }
            var singles = ends.Where((_, k) => !used.Contains(k)).ToList();
            return (pairs, singles);
        }

        /// <summary>Unit direction from a line end along its drawn path (its rails), else along its stored points.</summary>
        private static Vec2 Outward(Registry registry, SketchDocument doc, JunctionEnd end, Lang lang)
        {
            var line = doc.Get(end.Line);
            if (line is null) return Vec2.Zero;
            var pts = doc.ResolvePoints(line);
            if (end.Index < 0 || end.Index >= pts.Count || pts.Count < 2) return Vec2.Zero;
            var p = pts[end.Index];
            foreach (var r in HostRails(registry, doc, line, lang))
            {
                if (r.P.DistanceTo(p) <= 0.05 && r.Q.DistanceTo(p) > Geo.Eps) return (r.Q - r.P).Normalized();
                if (r.Q.DistanceTo(p) <= 0.05 && r.P.DistanceTo(p) > Geo.Eps) return (r.P - r.Q).Normalized();
            }
            var near = end.Index == 0 ? pts.Skip(1) : pts.Take(pts.Count - 1).Reverse();
            foreach (var q in near)
                if (q.DistanceTo(p) > Geo.Eps) return (q - p).Normalized();
            return Vec2.Zero;
        }

        private static IEnumerable<Anchor> HostRails(Registry registry, SketchDocument doc, SketchObject line, Lang lang)
            => registry.PageAnchors(line, new RenderContext(doc, lang)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail);

        private static IEnumerable<int> Ends(int count) => count < 2 ? Array.Empty<int>() : new[] { 0, count - 1 };

        private static bool IsRail(Attachment a) => a.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal);

        private static bool Hops(Registry registry, SketchObject o) => registry.Type(o.Type)?.HopsAtCrossings == true;

        /// <summary>Ends of lines that hop at crossings docked on <paramref name="host"/> (on its rails only, if <paramref name="rails"/>).</summary>
        private static IEnumerable<(JunctionEnd End, Vec2 At)> DockedLines(Registry registry, SketchDocument doc, string host, bool rails)
        {
            foreach (var x in doc.Objects.Values)
            {
                if (x.Id == host || !x.IsPointGeometry || x.Attach is null || !Hops(registry, x)) continue;
                var pts = doc.ResolvePoints(x);
                foreach (var i in Ends(pts.Count))
                    if (i < x.Attach.Count && x.Attach[i] is { } a && a.ObjectId == host && (!rails || IsRail(a)))
                        yield return (new JunctionEnd(x.Id, i), pts[i]);
            }
        }
    }
}
