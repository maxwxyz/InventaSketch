using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Editing lines with free corners (<see cref="ObjectType.InsertablePoints"/>: wires, pipes, roads, polylines): a new
    /// corner on a segment, and a branch, a new line of the same kind docked onto the line's rail (a T junction; wires and
    /// pipes draw a junction dot there, roads join). Branches already docked on the line stay where they are.
    /// </summary>
    public static class Polylines
    {
        /// <summary>True if corners can be inserted into <paramref name="o"/>.</summary>
        public static bool CanInsert(Registry registry, SketchObject o)
            => o.IsPointGeometry && o.Points is { Count: >= 2 } && registry.Type(o.Type)?.InsertablePoints == true;

        /// <summary>True if <paramref name="o"/> has free ends that can be extended (an open line with free corners).</summary>
        public static bool CanExtend(Registry registry, SketchObject o)
            => o.IsPointGeometry && o.Points is { Count: >= 2 } && registry.Type(o.Type) is { } t && t.IsClosed(o) != true
               && (t.InsertablePoints || (t.CornerType is { } c && registry.Type(c)?.InsertablePoints == true));

        /// <summary>
        /// The line as the type it becomes when it gets corners (<see cref="ObjectType.CornerType"/>: line or arrow →
        /// polyline); the old type's defaults are written into the parameters so arrowheads and the like stay as they were.
        /// </summary>
        public static SketchObject WithCorners(Registry registry, SketchObject o)
        {
            var t = registry.Type(o.Type);
            if (t is null || t.InsertablePoints || t.CornerType is not { } target || registry.Type(target) is not { } tt) return o;
            var p = o.Params.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var pd in t.Parameters)
                if (!p.ContainsKey(pd.Key) && pd.Default is { } d && tt.Parameters.Any(x => x.Key == pd.Key)) p[pd.Key] = d;
            return o with { Type = target, View = tt.DefaultView, Params = p };
        }

        /// <summary>True if <paramref name="o"/> is a closed outline (its last segment runs back to the first point).</summary>
        public static bool IsClosed(Registry registry, SketchObject o) => registry.Type(o.Type)?.IsClosed(o) == true;

        /// <summary>Number of segments that get a corner handle (closed outlines include the closing segment).</summary>
        public static int SegmentCount(Registry registry, SketchDocument doc, SketchObject o)
        {
            var n = doc.ResolvePoints(o).Count;
            return n < 2 ? 0 : IsClosed(registry, o) && n > 2 ? n : n - 1;
        }

        /// <summary>True if branches can dock onto <paramref name="o"/> (it exposes named rails).</summary>
        public static bool CanBranch(Registry registry, SketchDocument doc, SketchObject o, Lang lang = Lang.En)
            => CanInsert(registry, o) && Rails(registry, doc, o, lang).Count > 0;

        private static List<Anchor> Rails(Registry registry, SketchDocument doc, SketchObject o, Lang lang)
            => registry.PageAnchors(o, new RenderContext(doc, lang)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail && a.Id is not null).ToList();

        /// <summary>
        /// Where the "+" of segment <paramref name="segment"/> (between points i and i+1) sits: the middle of the segment,
        /// moved onto the drawn path when the line is routed (orthogonal wires and pipes).
        /// </summary>
        public static Vec2 SegmentHandle(Registry registry, SketchDocument doc, SketchObject o, int segment, Lang lang = Lang.En)
        {
            var p = doc.ResolvePoints(o);
            var mid = Vec2.Lerp(p[segment], p[(segment + 1) % p.Count], 0.5);
            var rails = Rails(registry, doc, o, lang);
            if (rails.Count == 0) return mid;
            return rails.Select(r => Geo.ClosestOnSegment(mid, r.P, r.Q).Point).OrderBy(q => q.DistanceTo(mid)).First();
        }

        /// <summary>
        /// Inserts a corner at <paramref name="p"/> as point <paramref name="index"/> (1 … count-1) of line <paramref name="id"/>.
        /// Branches docked onto the line (other lines' ends on its rails) are docked again at the same place.
        /// </summary>
        public static SketchDocument InsertCorner(Registry registry, SketchDocument doc, string id, int index, Vec2 p, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            // index == count: the closing segment of a closed outline (the new point goes after the last one).
            if (line?.Points is null || index < 1 || index > line.Points.Count - (IsClosed(registry, line) ? 0 : 1)) return doc;
            var docked = Docked(doc, id);
            var next = Edits.InsertPoint(doc, id, index, p);
            return Redock(registry, next, id, docked, lang);
        }

        /// <summary>
        /// Extends line <paramref name="id"/> at its start (<paramref name="atStart"/>) or end by a new point
        /// <paramref name="p"/> with an optional attachment; the old end becomes a corner (and keeps no attachment). Returns
        /// the index of the new point.
        /// </summary>
        public static (SketchDocument Doc, int Index) Extend(Registry registry, SketchDocument doc, string id, bool atStart, Vec2 p, Attachment? attach = null, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            if (line?.Points is null) return (doc, -1);
            // A straight line or arrow drawn on becomes a polyline (heads kept).
            var cornered = WithCorners(registry, line);
            if (!ReferenceEquals(cornered, line)) doc = Edits.Update(doc, new[] { id }, _ => cornered);
            var docked = Docked(doc, id);
            var count = line.Points.Count;
            var index = atStart ? 0 : count;
            var next = Edits.InsertPoint(doc, id, index, p);
            // The former end is now a corner: corners never dock.
            var former = atStart ? 1 : count - 1;
            next = Edits.Update(next, new[] { id }, o =>
            {
                if (o.Attach is null) return o;
                var att = o.Attach.ToList();
                if (former < att.Count) att[former] = null;
                att[index] = attach;
                return o with { Attach = att.Any(a => a is not null) ? att : null };
            });
            if (attach is not null && next.Get(id)!.Attach is null)
                next = Edits.SetPoint(next, id, index, p, attach);
            return (Redock(registry, next, id, docked, lang), index);
        }

        /// <summary>
        /// Splits line <paramref name="id"/> at corner <paramref name="index"/> into two lines of the same kind (the second
        /// one gets a new id). Branches docked on the line stay where they are, on whichever part they lie. A closed outline
        /// is opened at that corner instead.
        /// </summary>
        public static (SketchDocument Doc, string? NewId) Split(Registry registry, SketchDocument doc, string id, int index, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            if (line?.Points is null) return (doc, null);
            var pts = doc.ResolvePoints(line).ToList();
            var att = (line.Attach ?? new Attachment?[pts.Count]).ToList();
            while (att.Count < pts.Count) att.Add(null);
            var docked = Docked(doc, id);
            if (IsClosed(registry, line))
            {
                if (index < 0 || index >= pts.Count) return (doc, null);
                // Open the loop at the corner: it becomes both ends.
                var ring = pts.Skip(index).Concat(pts.Take(index)).ToList();
                ring.Add(pts[index]);
                var opened = Edits.Update(doc, new[] { id }, o => Open(o) with { Points = ring, Attach = null });
                return (Redock(registry, opened, id, docked, lang), null);
            }
            if (index < 1 || index > pts.Count - 2) return (doc, null);
            var first = pts.Take(index + 1).ToList();
            var second = pts.Skip(index).ToList();
            var attFirst = att.Take(index + 1).ToList();
            var attSecond = att.Skip(index).ToList();
            attFirst[attFirst.Count - 1] = null;
            attSecond[0] = null;
            var copy = line with
            {
                Id = Ids.New(),
                Points = second,
                Attach = attSecond.Any(a => a is not null) ? attSecond : null,
                Ref = null, Released = null,
            };
            var next = Edits.Update(doc, new[] { id }, o => o with { Points = first, Attach = attFirst.Any(a => a is not null) ? attFirst : null });
            var page = doc.Pages.FirstOrDefault(pg => pg.Order.Contains(id)) ?? doc.Pages[0];
            next = Edits.Add(next, page.Id, copy);
            next = Redock(registry, next, new[] { id, copy.Id }, docked, lang);
            return (next, copy.Id);
        }

        /// <summary>Same object, no longer a closed loop (wall or polyline "closed" flag cleared).</summary>
        private static SketchObject Open(SketchObject o)
            => o.Params.TryGetValue("closed", out var v) && v.Bool == true ? o.WithParam("closed", false) : o;

        /// <summary>
        /// Joins line <paramref name="b"/> onto line <paramref name="a"/>: the end of <paramref name="a"/>
        /// (<paramref name="aAtStart"/>) meets the end of <paramref name="b"/> (<paramref name="bAtStart"/>). Both must be of
        /// the same type; <paramref name="a"/> keeps its settings and id, <paramref name="b"/> is removed, branches docked on
        /// either stay where they are.
        /// </summary>
        public static SketchDocument Join(Registry registry, SketchDocument doc, string a, bool aAtStart, string b, bool bAtStart, Lang lang = Lang.En)
        {
            var la = doc.Get(a);
            var lb = doc.Get(b);
            if (la?.Points is null || lb?.Points is null || la.Type != lb.Type || a == b) return doc;
            var pa = doc.ResolvePoints(la).ToList();
            var pb = doc.ResolvePoints(lb).ToList();
            var aa = (la.Attach ?? new Attachment?[pa.Count]).ToList();
            var ab = (lb.Attach ?? new Attachment?[pb.Count]).ToList();
            while (aa.Count < pa.Count) aa.Add(null);
            while (ab.Count < pb.Count) ab.Add(null);
            // Orient: a runs towards the joint, b runs away from it.
            if (aAtStart) { pa.Reverse(); aa.Reverse(); }
            if (!bAtStart) { pb.Reverse(); ab.Reverse(); }
            var pts = pa.Concat(pb.Skip(1)).ToList();
            var att = aa.Take(aa.Count - 1).Append(null).Concat(ab.Skip(1)).ToList();
            var docked = Docked(doc, a).Concat(Docked(doc, b)).Where(d => d.Owner != a && d.Owner != b).ToList();
            var next = Edits.Update(doc, new[] { a }, o => o with { Points = pts, Attach = att.Any(x => x is not null) ? att : null });
            next = Edits.Remove(next, new[] { b });
            // Docks onto b (removed with it) and onto a go to the joined line.
            next = RetargetDocks(next, docked, a);
            return Redock(registry, next, a, docked, lang);
        }

        private static SketchDocument RetargetDocks(SketchDocument doc, List<(string Owner, int Index, Vec2 At)> docked, string target)
        {
            foreach (var (owner, index, at) in docked)
                doc = Edits.SetPoint(doc, owner, index, at, new Attachment(target, 0, 0) { AnchorId = "rail0" });
            return doc;
        }

        /// <summary>Removes corner <paramref name="index"/> (not an end) of line <paramref name="id"/>; branches stay docked.</summary>
        public static SketchDocument RemoveCorner(Registry registry, SketchDocument doc, string id, int index, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            var closed = line is not null && IsClosed(registry, line);
            if (line?.Points is null || line.Points.Count < (closed ? 4 : 3) || index < (closed ? 0 : 1) || index > line.Points.Count - (closed ? 1 : 2)) return doc;
            var docked = Docked(doc, id);
            var next = Edits.Update(doc, new[] { id }, o =>
            {
                var pts = doc.ResolvePoints(o).ToList();
                pts.RemoveAt(index);
                if (o.Attach is null) return o with { Points = pts };
                var att = o.Attach.ToList();
                if (index < att.Count) att.RemoveAt(index);
                return o with { Points = pts, Attach = att.Any(a => a is not null) ? att : null };
            });
            return Redock(registry, next, id, docked, lang);
        }

        /// <summary>
        /// A branch: a new line of the same type and settings from the rail of <paramref name="id"/> nearest to
        /// <paramref name="at"/> to <paramref name="end"/> (with an optional attachment there). Returns the document and the new id.
        /// </summary>
        public static (SketchDocument Doc, string? BranchId) Branch(Registry registry, SketchDocument doc, string id, Vec2 at, Vec2 end, Attachment? endAttach = null, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            if (line is null) return (doc, null);
            var rail = Rails(registry, doc, line, lang).OrderBy(r => Geo.ClosestOnSegment(at, r.P, r.Q).Point.DistanceTo(at)).FirstOrDefault();
            if (rail is null) return (doc, null);
            var on = Geo.ClosestOnSegment(at, rail.P, rail.Q).Point;
            var branch = line with
            {
                Id = Ids.New(),
                Points = new[] { on, end },
                Attach = new[] { Attachments.For(line, on, rail), endAttach },
                Ref = null, GroupId = null, Released = null, Text = null, Locked = false,
            };
            var page = doc.Pages.FirstOrDefault(pg => pg.Order.Contains(id)) ?? doc.Pages[0];
            return (Edits.Add(doc, page.Id, branch), branch.Id);
        }

        /// <summary>Line ends docked onto rails of <paramref name="id"/>: owner, point index, page position.</summary>
        private static List<(string Owner, int Index, Vec2 At)> Docked(SketchDocument doc, string id)
        {
            var list = new List<(string, int, Vec2)>();
            foreach (var o in doc.Objects.Values.Where(o => o.Id != id && o.Attach is not null))
            {
                var pts = doc.ResolvePoints(o);
                for (var i = 0; i < o.Attach!.Count && i < pts.Count; i++)
                    if (o.Attach[i] is { } a && a.ObjectId == id && a.AnchorId is not null) list.Add((o.Id, i, pts[i]));
            }
            return list;
        }

        private static SketchDocument Redock(Registry registry, SketchDocument doc, string id, List<(string Owner, int Index, Vec2 At)> docked, Lang lang)
            => Redock(registry, doc, new[] { id }, docked, lang);

        /// <summary>Docks the given line ends again onto the nearest rail of any of the lines <paramref name="ids"/>.</summary>
        private static SketchDocument Redock(Registry registry, SketchDocument doc, IReadOnlyList<string> ids, List<(string Owner, int Index, Vec2 At)> docked, Lang lang)
        {
            if (docked.Count == 0) return doc;
            var rails = ids.Select(doc.Get).Where(l => l is not null).SelectMany(l => Rails(registry, doc, l!, lang).Select(r => (Line: l!, Rail: r))).ToList();
            if (rails.Count == 0) return doc;
            foreach (var (owner, index, at) in docked)
            {
                if (doc.Get(owner) is null) continue;
                var (line, rail) = rails.OrderBy(r => Geo.ClosestOnSegment(at, r.Rail.P, r.Rail.Q).Point.DistanceTo(at)).First();
                var on = Geo.ClosestOnSegment(at, rail.P, rail.Q).Point;
                doc = Edits.SetPoint(doc, owner, index, on, Attachments.For(line, on, rail));
            }
            return doc;
        }
    }
}
