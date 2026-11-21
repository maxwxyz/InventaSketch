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
            var mid = Vec2.Lerp(p[segment], p[segment + 1], 0.5);
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
            if (line?.Points is null || index < 1 || index > line.Points.Count - 1) return doc;
            var docked = Docked(doc, id);
            var next = Edits.InsertPoint(doc, id, index, p);
            return Redock(registry, next, id, docked, lang);
        }

        /// <summary>Removes corner <paramref name="index"/> (not an end) of line <paramref name="id"/>; branches stay docked.</summary>
        public static SketchDocument RemoveCorner(Registry registry, SketchDocument doc, string id, int index, Lang lang = Lang.En)
        {
            var line = doc.Get(id);
            if (line?.Points is null || line.Points.Count < 3 || index < 1 || index > line.Points.Count - 2) return doc;
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
        {
            if (docked.Count == 0) return doc;
            var line = doc.Get(id)!;
            var rails = Rails(registry, doc, line, lang);
            if (rails.Count == 0) return doc;
            foreach (var (owner, index, at) in docked)
            {
                var rail = rails.OrderBy(r => Geo.ClosestOnSegment(at, r.P, r.Q).Point.DistanceTo(at)).First();
                var on = Geo.ClosestOnSegment(at, rail.P, rail.Q).Point;
                doc = Edits.SetPoint(doc, owner, index, on, Attachments.For(line, on, rail));
            }
            return doc;
        }
    }
}
