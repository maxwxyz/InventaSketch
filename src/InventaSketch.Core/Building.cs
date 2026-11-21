using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Kind of a build port (<see cref="Building"/>).</summary>
    public enum PortKind
    {
        /// <summary>On a gear's pitch circle: a meshing gear goes here.</summary>
        Mesh,
        /// <summary>Inside a ring gear's pitch circle: a planet goes here.</summary>
        InnerMesh,
        /// <summary>Free end of an axis (shaft end, e-machine or engine output): the next shaft section or a part goes here; dragging stretches a shaft.</summary>
        AxisEnd,
        /// <summary>Hub of a part that sits on no shaft: a shaft goes through it.</summary>
        Hub,
    }

    /// <summary>A place on an element where a fitting part can be docked: page point, outward direction, kind.</summary>
    public sealed record BuildPort(string Id, PortKind Kind, Vec2 At, Vec2 Dir)
    {
        /// <summary>The axis segment of an <see cref="PortKind.AxisEnd"/> port (start = the other end).</summary>
        public Vec2 AxisFrom { get; init; }
    }

    /// <summary>
    /// Building assemblies with "+" handles (gearboxes, spur stages, planetary stages, drivetrains, e-drives): every element
    /// offers build ports where a fitting part docks and is coupled right away: a meshing gear on each side of a gear (below,
    /// above, right, left in the element's own orientation), a planet inside a ring gear, the next shaft section, a coupling,
    /// bearing, e-machine or wheel at a free axis end, a shaft through a free hub. Ports already taken are not offered.
    /// Parts keep the view of the element (schematic stays schematic).
    /// </summary>
    public static class Building
    {
        private static readonly (string Name, Vec2 Dir)[] Sides = { ("down", new Vec2(0, 1)), ("up", new Vec2(0, -1)), ("right", new Vec2(1, 0)), ("left", new Vec2(-1, 0)) };

        /// <summary>Free build ports of object <paramref name="id"/>.</summary>
        public static IReadOnlyList<BuildPort> Ports(Registry registry, SketchDocument doc, string id, Lang lang = Lang.En)
        {
            var list = new List<BuildPort>();
            var o = doc.Get(id);
            if (o is null || o.IsPointGeometry || registry.Type(o.Type) is null) return list;
            var ctx = new RenderContext(doc, lang);
            var own = registry.PageAnchors(o, ctx).ToList();
            var others = doc.Objects.Values.Where(x => x.Id != id && !x.IsPointGeometry && registry.Type(x.Type) is not null)
                .Select(x => (Obj: x, Anchors: registry.PageAnchors(x, ctx).ToList())).ToList();
            var rad = Geo.DegToRad(o.Transform.Rotation);
            Vec2 Turn(Vec2 v) => new(v.X * Math.Cos(rad) - v.Y * Math.Sin(rad), v.X * Math.Sin(rad) + v.Y * Math.Cos(rad));

            // Gears: a meshing gear on each free side; ring gears: a planet inside.
            var pitch = own.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var meshDirs = registry.Type(o.Type)!.MeshDirections(o);
            if (pitch is not null)
                foreach (var (name, d) in Sides.Where(s => meshDirs.Any(m => m.DistanceTo(s.Dir) < 1e-6)))
                {
                    var dir = Turn(d);
                    var at = pitch.P + dir * pitch.Radius;
                    // Taken: another body lies right beyond this point.
                    var probe = at + dir * Math.Max(1, pitch.Radius * 0.4);
                    if (others.Any(x => Covers(x.Obj, x.Anchors, probe))) continue;
                    list.Add(new BuildPort("mesh." + name, PortKind.Mesh, at, dir));
                }
            var ring = own.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.PitchInner);
            if (ring is not null && pitch is null)
                foreach (var (name, d) in Sides)
                {
                    var dir = Turn(d);
                    var at = ring.P + dir * ring.Radius;
                    var probe = at - dir * Math.Max(1, ring.Radius * 0.3);
                    if (others.Any(x => Covers(x.Obj, x.Anchors, probe))) continue;
                    list.Add(new BuildPort("inner." + name, PortKind.InnerMesh, at, dir * -1));
                }

            // Free axis ends: the next shaft section or a part.
            var axes = own.Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis).ToList();
            for (var k = 0; k < axes.Count; k++)
            {
                var ax = axes[k];
                var len = ax.P.DistanceTo(ax.Q);
                if (len < 1e-6) continue;
                foreach (var (end, from, name) in new[] { (ax.P, ax.Q, "start"), (ax.Q, ax.P, "end") })
                {
                    var dir = (end - from) * (1 / len);
                    // Taken: another axis continues here, or a part sits on this end.
                    var beyond = end + dir * 1.0;
                    if (others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis
                            && Geo.ClosestOnSegment(beyond, a.P, a.Q).Point.DistanceTo(beyond) <= 0.3))) continue;
                    if (others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.P.DistanceTo(end) <= 0.5))) continue;
                    list.Add(new BuildPort("axis" + k + "." + name, PortKind.AxisEnd, end, dir) { AxisFrom = from });
                }
            }

            // A hub on no shaft (and no axis of its own): a shaft through it.
            var hub = own.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
            if (hub is not null && axes.Count == 0
                && !others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && Geo.ClosestOnSegment(hub.P, a.P, a.Q).Point.DistanceTo(hub.P) <= 0.3)))
                list.Add(new BuildPort("hub", PortKind.Hub, hub.P, Turn(new Vec2(1, 0))));
            return list;
        }

        /// <summary>True if the point lies on another element: inside its pitch circle, or inside its box (not for lines).</summary>
        private static bool Covers(SketchObject x, List<Anchor> anchors, Vec2 p)
        {
            var c = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch);
            if (c is not null) return c.P.DistanceTo(p) < c.Radius;
            var (u, v) = x.ToBox(p);
            return Math.Abs(u) <= 0.5 && Math.Abs(v) <= 0.5 && x.Size.W > 2 && x.Size.H > 2;
        }

        /// <summary>
        /// Library items that fit port <paramref name="port"/> of object <paramref name="id"/>, in its kind of view: gears for
        /// mesh ports, shafts and parts with a hub for axis ends, shafts for free hubs. The element's own suggestions
        /// (<see cref="Registry.Suggestions"/>) first.
        /// </summary>
        public static IReadOnlyList<LibraryItem> Items(Registry registry, SketchDocument doc, string id, BuildPort port, int max = 8, Lang lang = Lang.En)
        {
            var o = doc.Get(id);
            if (o is null) return Array.Empty<LibraryItem>();
            var candidates = registry.Suggestions(o, 14).ToList();
            // The element's own type in its view (a gear next to a gear, the next shaft section).
            var same = registry.Library.FirstOrDefault(i => i.TypeId == o.Type && i.View == o.View);
            if (same is not null && !candidates.Contains(same)) candidates.Insert(0, same);
            if (port.Kind is PortKind.Hub or PortKind.AxisEnd)
            {
                var shaft = registry.Suggestions(o, 14).FirstOrDefault(i => i.TypeId == "mech.shaft");
                if (shaft is not null && !candidates.Contains(shaft)) candidates.Insert(0, shaft);
            }
            var list = new List<LibraryItem>();
            foreach (var item in candidates)
            {
                if (list.Count >= max) break;
                if (list.Any(i => i.TypeId == item.TypeId)) continue;
                if (Place(registry, doc, id, port, item, lang) is not null) list.Add(item);
            }
            return list;
        }

        /// <summary>The new object for <paramref name="item"/> docked at <paramref name="port"/> of <paramref name="id"/>, or null if it does not fit there.</summary>
        public static SketchObject? Place(Registry registry, SketchDocument doc, string id, BuildPort port, LibraryItem item, Lang lang = Lang.En)
        {
            var sel = doc.Get(id);
            if (sel is null || registry.Type(item.TypeId) is null) return null;
            var probe = registry.Create(item, port.At);
            if (probe.IsPointGeometry) return null;
            probe = probe with { Transform = probe.Transform with { Rotation = sel.Transform.Rotation } };
            var anchors = Anchors(registry, doc, probe, lang);
            var pitch = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var axis = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);
            var hub = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
            switch (port.Kind)
            {
                case PortKind.Mesh:
                case PortKind.InnerMesh:
                    if (pitch is null) return null;
                    return Translate(probe, port.At + port.Dir * pitch.Radius - pitch.P);
                case PortKind.Hub:
                    if (axis is null) return null;
                    return Translate(probe, port.At - Vec2.Lerp(axis.P, axis.Q, 0.5));
                case PortKind.AxisEnd:
                    if (axis is not null && hub is null)
                    {
                        // The next shaft section: collinear, starting where this axis ends.
                        var own = axis.Q - axis.P;
                        if (own.Length < 1e-6) return null;
                        var turn = (Math.Atan2(port.Dir.Y, port.Dir.X) - Math.Atan2(own.Y, own.X)) * 180 / Math.PI;
                        var turned = probe with { Transform = probe.Transform with { Rotation = Math.Round(probe.Transform.Rotation + turn, 6) } };
                        var ta = Anchors(registry, doc, turned, lang).First(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);
                        var start = (ta.P - port.At).Dot(port.Dir) < (ta.Q - port.At).Dot(port.Dir) ? ta.P : ta.Q;
                        return Translate(turned, port.At - start);
                    }
                    if (hub is null) return null;
                    {
                        // A part on the end of the axis: its hub just inside the end, turned like the element (both have
                        // their axis along the local x direction).
                        var turned = probe;
                        var len = port.At.DistanceTo(port.AxisFrom);
                        var inset = Math.Min(turned.Size.W / 2, len * 0.15);
                        var h = Anchors(registry, doc, turned, lang).First(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
                        return Translate(turned, port.At - port.Dir * inset - h.P);
                    }
            }
            return null;
        }

        /// <summary>
        /// Stretches the shaft (or other part with one axis spanning its width) at port <paramref name="port"/> so the end
        /// reaches <paramref name="to"/> projected onto the axis; the other end stays. Parts on the shaft keep their place
        /// (the host applies this without coupling). Minimum length 2 mm.
        /// </summary>
        public static SketchDocument StretchEnd(Registry registry, SketchDocument doc, string id, BuildPort port, Vec2 to, Lang lang = Lang.En)
        {
            var o = doc.Get(id);
            if (o is null || port.Kind != PortKind.AxisEnd) return doc;
            var axisLen = port.At.DistanceTo(port.AxisFrom);
            if (axisLen < 1e-6) return doc;
            var u = port.Dir;
            var t = Math.Max(2, (to - port.AxisFrom).Dot(u));
            var grow = t - axisLen;
            if (Math.Abs(grow) < 1e-6) return doc;
            // The axis spans the box width (shafts): the width grows by the same amount, the centre moves half of it.
            var centre = new Vec2(o.Transform.X, o.Transform.Y) + u * (grow / 2);
            return Edits.Update(doc, new[] { id }, x => x with
            {
                Size = new Size2(Math.Round(Math.Max(2, x.Size.W + grow), 6), x.Size.H),
                Transform = x.Transform with { X = Math.Round(centre.X, 6), Y = Math.Round(centre.Y, 6) },
            });
        }

        /// <summary>True if dragging port <paramref name="port"/> of <paramref name="o"/> stretches it (shafts: the axis spans the width).</summary>
        public static bool CanStretch(Registry registry, SketchDocument doc, SketchObject o, BuildPort port, Lang lang = Lang.En)
        {
            if (port.Kind != PortKind.AxisEnd || registry.Type(o.Type) is null) return false;
            var len = port.At.DistanceTo(port.AxisFrom);
            return Math.Abs(len - o.Size.W) < 0.05 * Math.Max(1, o.Size.W) && !registry.PageAnchors(o, new RenderContext(doc, lang)).Any(a => a.Role == AnchorRole.Hub);
        }

        private static List<Anchor> Anchors(Registry registry, SketchDocument doc, SketchObject o, Lang lang)
        {
            var tmp = Edits.Add(doc, doc.Pages[0].Id, o);
            return registry.PageAnchors(tmp.Get(o.Id)!, new RenderContext(tmp, lang)).ToList();
        }

        private static SketchObject Translate(SketchObject o, Vec2 d)
            => o with { Transform = o.Transform with { X = Math.Round(o.Transform.X + d.X, 6), Y = Math.Round(o.Transform.Y + d.Y, 6) } };
    }
}
