using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Side of a selection frame where a quick-add arrow sits.</summary>
    public enum FlowSide { Left, Top, Right, Bottom }

    /// <summary>Result of a quick add: the document, the new element and the connector joining it (if any).</summary>
    public sealed record FlowResult(SketchDocument Doc, string ObjectId, string? ConnectorId);

    /// <summary>
    /// Building flowcharts, UML, ER and BPMN diagrams by quick add (like the blue "+" arrows of draw.io or Slidemax): an
    /// element of a chainable type (<see cref="ObjectType.ChainConnector"/>) gets a copy next to it on the chosen side,
    /// clear of other elements, and a connector from the port facing that side to the port of the copy facing back, its
    /// ends attached so both stay connected. <see cref="Replace"/> then turns the new element into another library item
    /// ("next element") without losing its connections.
    /// </summary>
    public static class Flow
    {
        /// <summary>Gap between an element and the one added next to it (mm), unless the element is very small or large.</summary>
        public const double MinGap = 12, MaxGap = 25;

        public static Vec2 Direction(FlowSide side) => side switch
        {
            FlowSide.Left => new Vec2(-1, 0),
            FlowSide.Top => new Vec2(0, -1),
            FlowSide.Right => new Vec2(1, 0),
            _ => new Vec2(0, 1),
        };

        /// <summary>The side whose direction is closest to <paramref name="d"/> (for drops and port drags).</summary>
        public static FlowSide SideOf(Vec2 d) => Math.Abs(d.X) >= Math.Abs(d.Y)
            ? (d.X < 0 ? FlowSide.Left : FlowSide.Right)
            : (d.Y < 0 ? FlowSide.Top : FlowSide.Bottom);

        /// <summary>True if quick-add arrows make sense for <paramref name="o"/>.</summary>
        public static bool CanChain(Registry registry, SketchObject? o)
            => o is not null && !o.IsPointGeometry && registry.Type(o.Type)?.ChainConnector(o) is not null;

        /// <summary>The library item of a chain connector: an item id, else the first item of that type id; null if unknown.</summary>
        public static LibraryItem? ConnectorItem(Registry registry, string? idOrType)
            => string.IsNullOrEmpty(idOrType) ? null : registry.Item(idOrType!) ?? registry.Library.FirstOrDefault(i => i.TypeId == idOrType);

        /// <summary>
        /// Adds an element next to <paramref name="objectId"/> on <paramref name="side"/>: a copy (or <paramref name="itemId"/>
        /// if given), centred at <paramref name="at"/> if given, else one gap beyond the element and beyond anything already
        /// there, plus the type's connector between the facing ports (from <paramref name="fromAnchorId"/> if the drag started
        /// on a port). The new element keeps the source's group.
        /// </summary>
        public static FlowResult QuickAdd(Registry registry, SketchDocument doc, string objectId, FlowSide side, Vec2? at = null, string? itemId = null, string? fromAnchorId = null)
        {
            var src = doc.Get(objectId) ?? throw new KeyNotFoundException("Unknown object " + objectId);
            if (src.IsPointGeometry) throw new ArgumentException("Quick add needs a box object.");
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(src.Id)) ?? doc.Pages[0];
            var dir = Direction(side);
            var item = itemId is null ? null : registry.Item(itemId) ?? throw new KeyNotFoundException("Unknown library item " + itemId);

            SketchObject added;
            if (item is null)
                added = src with { Id = Ids.New(), Ref = null };
            else
                added = registry.Create(item, new Vec2(src.Transform.X, src.Transform.Y)) with { GroupId = src.GroupId };
            var srcBox = src.Bounds();
            var size = added.Bounds();
            var along = Math.Abs(dir.X) > 0 ? (srcBox.W + size.W) / 2 : (srcBox.H + size.H) / 2;
            var extent = Math.Abs(dir.X) > 0 ? size.W : size.H;
            var gap = Math.Max(MinGap, Math.Min(MaxGap, extent));
            Vec2 centre;
            if (at is Vec2 drop) centre = drop;
            else
            {
                centre = srcBox.Center + dir * (along + gap);
                // Behind elements already there (a branch added twice goes one step further), at most a few steps.
                var others = doc.ObjectsOn(page).Where(o => o.Id != src.Id && !o.IsPointGeometry).Select(o => o.Bounds()).ToList();
                for (var k = 0; k < 8 && others.Any(b => b.Intersects(RectAt(centre, size.W, size.H, 0.5))); k++)
                    centre += dir * (extent + gap);
            }
            added = added with { Transform = added.Transform with { X = centre.X, Y = centre.Y } };
            doc = Edits.Add(doc, page.Id, added);

            string? connectorId = null;
            var connectorItem = ConnectorItem(registry, registry.Type(src.Type)?.ChainConnector(src));
            if (connectorItem is not null)
            {
                var ctx = new RenderContext(doc);
                var (a, aPoint) = fromAnchorId is not null && registry.PageAnchors(src, ctx).FirstOrDefault(x => x.Kind == AnchorKind.Point && x.Id == fromAnchorId) is { } from
                    ? (from, from.P)
                    : Facing(registry, src, ctx, dir);
                var (b, bPoint) = Facing(registry, doc.Get(added.Id)!, ctx, dir * -1);
                var connector = registry.Create(connectorItem, (aPoint + bPoint) * 0.5);
                connector = connector with
                {
                    Points = new[] { aPoint, bPoint },
                    Attach = new[] { Attachments.For(src, aPoint, a), Attachments.For(doc.Get(added.Id)!, bPoint, b) },
                    GroupId = src.GroupId,
                };
                connectorId = connector.Id;
                doc = Edits.Add(doc, page.Id, connector);
                doc = Attachments.Refresh(registry, doc, ctx);
            }
            return new FlowResult(doc, added.Id, connectorId);
        }

        /// <summary>
        /// Replaces <paramref name="objectId"/> by a new object of <paramref name="itemId"/> at the same centre with the same id,
        /// so connectors stay attached; each attached connector end moves to the port of the new element nearest to it.
        /// </summary>
        public static SketchDocument Replace(Registry registry, SketchDocument doc, string objectId, string itemId)
        {
            var old = doc.Get(objectId) ?? throw new KeyNotFoundException("Unknown object " + objectId);
            var item = registry.Item(itemId) ?? throw new KeyNotFoundException("Unknown library item " + itemId);
            var fresh = registry.Create(item, new Vec2(old.Transform.X, old.Transform.Y)) with { Id = old.Id, GroupId = old.GroupId, Ref = old.Ref };
            // The edge facing the element it is connected to stays where it was (a taller decision grows away from it).
            var link = doc.Objects.Values.FirstOrDefault(o => o.Attach is not null && o.Attach.Any(a => a?.ObjectId == old.Id));
            if (link is not null)
            {
                var pts = doc.ResolvePoints(link);
                var i = link.Attach!.ToList().FindIndex(a => a?.ObjectId == old.Id);
                var other = pts[i == 0 ? pts.Count - 1 : 0];
                var oc = new Vec2(old.Transform.X, old.Transform.Y);
                var d = Direction(SideOf(other - oc));
                double Half(SketchObject x) => Math.Abs(d.X) > 0 ? x.Bounds().W / 2 : x.Bounds().H / 2;
                var c = oc + d * (Half(old) - Half(fresh));
                fresh = fresh with { Transform = fresh.Transform with { X = c.X, Y = c.Y } };
            }
            doc = Edits.Update(doc, new[] { old.Id }, _ => fresh);
            var ctx = new RenderContext(doc);
            var anchors = registry.PageAnchors(fresh, ctx).ToList();
            var ports = anchors.Where(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port).ToList();
            if (ports.Count == 0) ports = anchors.Where(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.EdgeMid).ToList();
            var dependents = doc.Objects.Values.Where(o => o.Attach is not null && o.Attach.Any(a => a?.ObjectId == old.Id)).Select(o => o.Id).ToList();
            doc = Edits.Update(doc, dependents, o =>
            {
                var pts = doc.ResolvePoints(o).ToList();
                var attach = o.Attach!.ToList();
                for (var i = 0; i < attach.Count && i < pts.Count; i++)
                {
                    if (attach[i]?.ObjectId != old.Id || ports.Count == 0) continue;
                    // The port facing the other end of the connector, else the nearest one.
                    var other = pts[i == 0 ? pts.Count - 1 : 0];
                    var port = ports.OrderBy(p => p.P.DistanceTo(other)).First();
                    attach[i] = Attachments.For(fresh, port.P, port);
                    pts[i] = port.P;
                }
                return o with { Points = pts, Attach = attach };
            });
            return Attachments.Refresh(registry, doc, new RenderContext(doc));
        }

        /// <summary>Library items offered as the "next element" after a quick add: same category first, then the type's companions.</summary>
        public static IReadOnlyList<LibraryItem> NextItems(Registry registry, SketchObject o, int max = 8)
        {
            var type = registry.Type(o.Type);
            if (type is null) return Array.Empty<LibraryItem>();
            var list = new List<LibraryItem>();
            void Add(LibraryItem i)
            {
                if (list.Count < max && !i.IsPointGeometry(registry) && list.All(x => x.Id != i.Id)) list.Add(i);
            }
            foreach (var i in registry.Library.Where(i => i.Category == type.Category)) Add(i);
            foreach (var i in registry.Suggestions(o, max)) Add(i);
            return list;
        }

        private static bool IsPointGeometry(this LibraryItem item, Registry registry) => registry.Type(item.TypeId)?.Geometry == GeometryKind.Points;

        private static Rect RectAt(Vec2 c, double w, double h, double shrink) => new(c.X - w / 2 + shrink, c.Y - h / 2 + shrink, Math.Max(0, w - 2 * shrink), Math.Max(0, h - 2 * shrink));

        /// <summary>The port of <paramref name="o"/> facing <paramref name="dir"/> (else the edge middle), with its page point.</summary>
        public static (Anchor Anchor, Vec2 Point) Facing(Registry registry, SketchObject o, RenderContext ctx, Vec2 dir)
        {
            var anchors = registry.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Point).ToList();
            var centre = new Vec2(o.Transform.X, o.Transform.Y);
            double Score(Anchor a)
            {
                var d = a.P - centre;
                var len = d.Length;
                return len < 1e-9 ? -2 : (d.X * dir.X + d.Y * dir.Y) / len + 1e-3 * len;
            }
            var ports = anchors.Where(a => a.Role == AnchorRole.Port).ToList();
            var best = ports.Count > 0 && ports.Max(Score) > 0.5
                ? ports.OrderByDescending(Score).First()
                : anchors.Where(a => a.Role == AnchorRole.EdgeMid).OrderByDescending(Score).FirstOrDefault()
                  ?? Anchor.Point(AnchorRole.EdgeMid, centre + dir * (Math.Abs(dir.X) > 0 ? o.Size.W / 2 : o.Size.H / 2));
            return (best, best.P);
        }
    }
}
