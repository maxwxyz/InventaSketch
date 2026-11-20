using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Attachments of points to box objects. An <see cref="Attachment"/> stores normalised box coordinates, so it follows
    /// moving, rotating and free scaling of the target. When it also names a target anchor
    /// (<see cref="Attachment.AnchorId"/>, e.g. an electrical pin), <see cref="Refresh"/> re-resolves it to that anchor
    /// after edits that move the anchor inside the box (polarity flip, parameter or view change).
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
            if (owner is null || owner.IsPointGeometry) return null;
            var (u, v) = owner.ToBox(snapped);
            var anchorId = t.Kind == AnchorKind.Point && t.P.DistanceTo(snapped) < 1e-6 ? t.Id : null;
            return new Attachment(owner.Id, u, v) { AnchorId = anchorId };
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
                    if (target is null || target.IsPointGeometry || registry.Type(target.Type) is null) continue;
                    if (!cache.TryGetValue(target.Id, out var anchors))
                        cache[target.Id] = anchors = registry.PageAnchors(target, context).Where(x => x.Kind == AnchorKind.Point && x.Id is not null).ToList();
                    var anchor = anchors.FirstOrDefault(x => x.Id == a.AnchorId);
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
