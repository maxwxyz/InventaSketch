using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Attachments of points to other objects. To a box object an <see cref="Attachment"/> stores normalised box
    /// coordinates, so it follows moving, rotating and free scaling of the target. When it also names a target anchor
    /// (<see cref="Attachment.AnchorId"/>, e.g. an electrical pin), <see cref="Refresh"/> re-resolves it to that anchor
    /// after edits that move the anchor inside the box (polarity flip, parameter or view change). To a line (point
    /// object) it names one of the line's rails (<c>rail0</c>, <c>rail1</c> …, see <see cref="Registry.PageAnchors"/>) and
    /// stores the position along it in <see cref="Attachment.U"/> (0 = start, 1 = end): a branch wire, pipe or road
    /// follows the line it joins, and connection exports count it as connected.
    /// </summary>
    public static class Attachments
    {
        /// <summary>Tolerance in normalised box coordinates below which an attachment is considered unchanged.</summary>
        private const double Same = 1e-9;

        /// <summary>
        /// Attachment for a point snapped with <see cref="Snapper.SnapPoint"/> to <paramref name="snapped"/>: the box
        /// object owning the winning target anchor, the normalised position and, for named point anchors (pins, ports,
        /// mounts), the anchor id. Null if the snap has no box owner (grid, alignment, page, point objects such as wires).
        /// </summary>
        public static Attachment? FromSnap(SketchDocument doc, Vec2 snapped, SnapResult result)
        {
            var t = result.Target;
            if (t?.Owner is null) return null;
            var owner = doc.Get(t.Owner);
            return owner is null ? null : For(owner, snapped, t);
        }

        /// <summary>
        /// Attachment of a point at <paramref name="at"/> snapped to <paramref name="target"/> of <paramref name="owner"/>:
        /// box owners give normalised box coordinates (plus the anchor id of named point anchors), lines give their rail
        /// id and the position along it. Null for a line target that is not a rail.
        /// </summary>
        public static Attachment? For(SketchObject owner, Vec2 at, Anchor target)
        {
            if (owner.IsPointGeometry)
            {
                if (target.Kind != AnchorKind.Segment || target.Role != AnchorRole.Rail || target.Id is null) return null;
                var (_, t) = Geo.ClosestOnSegment(at, target.P, target.Q);
                return new Attachment(owner.Id, t, 0) { AnchorId = target.Id };
            }
            var (u, v) = owner.ToBox(at);
            return new Attachment(owner.Id, u, v) { AnchorId = target.Kind == AnchorKind.Point && target.P.DistanceTo(at) < 1e-6 ? target.Id : null };
        }

        /// <summary>
        /// Free docking on a surface: the attachment of a point lying inside the box of one of <paramref name="candidates"/>
        /// (the smallest such box, so a part wins over the housing around it), in normalised box coordinates, so the point
        /// follows that element when it moves, turns or is resized. Null if the point lies on no box object.
        /// </summary>
        public static Attachment? OnSurface(Vec2 at, IEnumerable<SketchObject> candidates)
        {
            SketchObject? best = null;
            var bestArea = double.MaxValue;
            double bu = 0, bv = 0;
            foreach (var o in candidates)
            {
                if (o.IsPointGeometry || o.Size.W < 1 || o.Size.H < 1) continue;
                var (u, v) = o.ToBox(at);
                if (Math.Abs(u) > 0.5 || Math.Abs(v) > 0.5) continue;
                var area = o.Size.W * o.Size.H;
                if (area >= bestArea) continue;
                best = o; bestArea = area; bu = u; bv = v;
            }
            return best is null ? null : new Attachment(best.Id, bu, bv);
        }

        /// <summary>
        /// Re-resolves every attachment with an <see cref="Attachment.AnchorId"/> to the current page position of that
        /// anchor of its target (via <see cref="Registry.PageAnchors"/>). Attachments whose anchor no longer exists keep
        /// their last position and their id (so the link returns when the anchor does). Returns the same instance if
        /// nothing changed, so it can run after every edit without creating empty undo steps.
        /// </summary>
        public static SketchDocument Refresh(Registry registry, SketchDocument doc, RenderContext? ctx = null)
        {
            if (!doc.Objects.Values.Any(o => o.Attach is not null && o.Attach.Any(a => a?.AnchorId is not null))) return doc;
            // Branches of branches follow in turn: one pass per level (bounded, cycles stop there).
            for (var pass = 0; pass < 4; pass++)
            {
                var next = RefreshOnce(registry, doc, pass == 0 ? ctx : null);
                if (ReferenceEquals(next, doc)) return doc;
                doc = next;
            }
            return doc;
        }

        private static SketchDocument RefreshOnce(Registry registry, SketchDocument doc, RenderContext? ctx)
        {
            var context = ctx is not null && ReferenceEquals(ctx.Doc, doc) ? ctx : new RenderContext(doc, ctx?.Lang ?? Lang.En, ctx?.Monochrome ?? false);
            var cache = new Dictionary<string, List<Anchor>>(StringComparer.Ordinal);
            Dictionary<string, SketchObject>? changed = null;
            foreach (var o in doc.Objects.Values)
            {
                if (o.Attach is null || o.Points is null) continue;
                List<Attachment?>? attach = null;
                List<Vec2>? points = null;
                for (var i = 0; i < o.Attach.Count && i < o.Points.Count; i++)
                {
                    var a = o.Attach[i];
                    if (a?.AnchorId is null) continue;
                    var target = doc.Get(a.ObjectId);
                    if (target is null || target.Id == o.Id || registry.Type(target.Type) is null) continue;
                    if (!cache.TryGetValue(target.Id, out var anchors))
                        cache[target.Id] = anchors = registry.PageAnchors(target, context).Where(x => x.Id is not null).ToList();
                    if (target.IsPointGeometry)
                    {
                        // Branch: the point sits at its fraction along the named rail of the line.
                        var rail = anchors.FirstOrDefault(x => x.Kind == AnchorKind.Segment && x.Id == a.AnchorId);
                        if (rail is null) continue;
                        var on = Vec2.Lerp(rail.P, rail.Q, Math.Max(0, Math.Min(1, a.U)));
                        if (on.DistanceTo(o.Points[i]) < Same) continue;
                        attach ??= o.Attach.ToList();
                        points ??= o.Points.ToList();
                        points[i] = on;
                        continue;
                    }
                    var anchor = anchors.FirstOrDefault(x => x.Kind == AnchorKind.Point && x.Id == a.AnchorId);
                    if (anchor is null) continue;
                    var (u, v) = target.ToBox(anchor.P);
                    if (Math.Abs(u - a.U) < Same && Math.Abs(v - a.V) < Same) continue;
                    attach ??= o.Attach.ToList();
                    points ??= o.Points.ToList();
                    attach[i] = a with { U = u, V = v };
                    points[i] = anchor.P;
                }
                if (attach is null) continue;
                changed ??= new Dictionary<string, SketchObject>(StringComparer.Ordinal);
                changed[o.Id] = o with { Attach = attach, Points = points };
            }
            return changed is null ? doc : Edits.Update(doc, changed.Keys, o => changed[o.Id]);
        }
    }
}
