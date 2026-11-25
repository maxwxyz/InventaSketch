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
        /// <summary>Hub of a part that sits on no shaft: a shaft goes through it (or a concentric sun, ring gear or carrier).</summary>
        Hub,
        /// <summary>Free planet pin of a carrier: a planet goes here, sized to mesh with the sun or ring around.</summary>
        Pin,
        /// <summary>Cone apex of a bevel gear: a mating bevel gear at 90° (or facing it on the same axis) shares it.</summary>
        Bevel,
        /// <summary>Free joint of a link (linkage): the next link, a fixed pivot, a joint or a slider goes here.</summary>
        Joint,
        /// <summary>
        /// A directed mount (robot flange): a part with a directed mount of the same id goes here, turned so its mount
        /// direction faces the port's (a gripper on the flange, pointing along the tool axis).
        /// </summary>
        Mount,
        /// <summary>
        /// A free connection port (cable, wire, pipe, duct, signal line): the connector for the port's medium goes here (its
        /// start docked on the port, its free end offering "+" in turn), or the next element, joined by that connector.
        /// </summary>
        Connect,
        /// <summary>
        /// The free end of a connection line: the line continues (one segment more), or an element with a port of the
        /// line's medium docks there with that port.
        /// </summary>
        LineEnd,
    }

    /// <summary>A place on an element where a fitting part can be docked: page point, outward direction, kind.</summary>
    public sealed record BuildPort(string Id, PortKind Kind, Vec2 At, Vec2 Dir)
    {
        /// <summary>The axis segment of an <see cref="PortKind.AxisEnd"/> port (start = the other end).</summary>
        public Vec2 AxisFrom { get; init; }
        /// <summary>Radius of the connection face of a <see cref="PortKind.Mount"/> port (0 = unknown).</summary>
        public double FaceRadius { get; init; }
        /// <summary>Medium of a <see cref="PortKind.Connect"/> or <see cref="PortKind.LineEnd"/> port (<see cref="PortMedia"/>).</summary>
        public string? Medium { get; init; }
        /// <summary>Anchor id of a <see cref="PortKind.Connect"/> port on its element; point index of a <see cref="PortKind.LineEnd"/> ("p0", "p5").</summary>
        public string? AnchorId { get; init; }
    }

    /// <summary>Result of <see cref="Building.Apply"/>: the document with the built part(s) and the object to select next.</summary>
    public sealed record BuildResult(SketchDocument Doc, string SelectId);

    /// <summary>
    /// Building assemblies with "+" handles (gearboxes, spur stages, planetary stages, drivetrains, e-drives): every element
    /// offers build ports where a fitting part docks and is coupled right away: a meshing gear on each side of a gear (below,
    /// above, right, left in the element's own orientation), a planet inside a ring gear, the next shaft section, a coupling,
    /// bearing, e-machine or wheel at a free axis end, a shaft through a free hub. Ports already taken are not offered.
    /// Parts keep the view of the element (schematic stays schematic).
    /// </summary>
    public static partial class Building
    {
        private static readonly (string Name, Vec2 Dir)[] Sides = { ("down", new Vec2(0, 1)), ("up", new Vec2(0, -1)), ("right", new Vec2(1, 0)), ("left", new Vec2(-1, 0)) };

        /// <summary>Free build ports of object <paramref name="id"/>.</summary>
        public static IReadOnlyList<BuildPort> Ports(Registry registry, SketchDocument doc, string id, Lang lang = Lang.En)
        {
            var list = new List<BuildPort>();
            var o = doc.Get(id);
            if (o is not null && o.IsPointGeometry && registry.Type(o.Type)?.LinkageRole(o) == LinkageRole.Link) return JointPorts(registry, doc, o);
            if (o is not null && o.IsPointGeometry && registry.Type(o.Type) is not null) return LineEndPorts(registry, doc, o, lang);
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

            // Bevel gears: mating gears share the cone apex: above and below (axes at 90°) and facing on the same axis.
            var apex = own.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex);
            if (apex is not null)
            {
                var toApex = apex.P - new Vec2(o.Transform.X, o.Transform.Y);
                var ax = toApex.Length > 1e-6 ? toApex * (1 / toApex.Length) : Turn(new Vec2(1, 0));
                var up = new Vec2(ax.Y, -ax.X);
                if (up.Y > 1e-9) up = up * -1;
                foreach (var (name, dir) in new[] { ("bevel.up", up), ("bevel.down", up * -1), ("bevel.across", ax) })
                {
                    var taken = others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex && a.P.DistanceTo(apex.P) <= 0.5)
                        && (new Vec2(x.Obj.Transform.X, x.Obj.Transform.Y) - apex.P).Dot(dir) > 0.5);
                    if (!taken) list.Add(new BuildPort(name, PortKind.Bevel, apex.P, dir));
                }
            }

            // Carrier pins (hub points or axes with ids "pin…"): a planet on each free pin.
            var centre = (own.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == "hub")?.P) ?? new Vec2(o.Transform.X, o.Transform.Y);
            foreach (var pin in own.Where(IsPin))
            {
                var at = pin.Kind == AnchorKind.Point ? pin.P : Vec2.Lerp(pin.P, pin.Q, 0.5);
                var taken = others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub
                    && (pin.Kind == AnchorKind.Point ? a.P.DistanceTo(pin.P) <= 0.5 : Geo.ClosestOnSegment(a.P, pin.P, pin.Q).Point.DistanceTo(a.P) <= 0.3)));
                if (taken) continue;
                var d = at - centre;
                list.Add(new BuildPort(pin.Id!, PortKind.Pin, at, d.Length > 1e-6 ? d * (1 / d.Length) : Turn(new Vec2(0, -1))) { AxisFrom = centre });
            }

            // Free axis ends: the next shaft section or a part. An element that marks the ends of its axes (roads: lane start
            // and end points) gets ports there only – not at the joints of the chords that make up a curved lane or a ring,
            // nor at the inner ends of a junction's arms.
            var axes = own.Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && !IsPin(a)).ToList();
            var marked = own.Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.End).Select(a => a.P).ToList();
            for (var k = 0; k < axes.Count; k++)
            {
                var ax = axes[k];
                var len = ax.P.DistanceTo(ax.Q);
                if (len < 1e-6) continue;
                foreach (var (end, from, name) in new[] { (ax.P, ax.Q, "start"), (ax.Q, ax.P, "end") })
                {
                    if (marked.Count > 0 && !marked.Any(m => m.DistanceTo(end) <= 0.05)) continue;
                    var dir = (end - from) * (1 / len);
                    // Taken: another axis continues here, or a part sits on this end.
                    var beyond = end + dir * 1.0;
                    if (others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis
                            && Geo.ClosestOnSegment(beyond, a.P, a.Q).Point.DistanceTo(beyond) <= 0.3))) continue;
                    if (others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.P.DistanceTo(end) <= 0.5))) continue;
                    list.Add(new BuildPort("axis" + k + "." + name, PortKind.AxisEnd, end, dir) { AxisFrom = from });
                }
            }

            // Directed mounts (robot flange): a part with a matching mount goes there, facing it; taken if another element's
            // mount already sits on it.
            foreach (var m in own.Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Mount && a.Direction is not null && a.Id is not null))
                if (!others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Mount && a.P.DistanceTo(m.P) <= 0.5)))
                    list.Add(new BuildPort("mount." + m.Id, PortKind.Mount, m.P, m.Direction!.Value) { FaceRadius = m.Radius });

            // A hub on no shaft (and no axis of its own): a shaft through it, or a concentric gear or carrier.
            var hub = own.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == "hub")
                ?? own.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && !IsPin(a));
            if (hub is not null && axes.Count == 0
                && !others.Any(x => x.Anchors.Any(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && Geo.ClosestOnSegment(hub.P, a.P, a.Q).Point.DistanceTo(hub.P) <= 0.3)))
                list.Add(new BuildPort("hub", PortKind.Hub, hub.P, Turn(new Vec2(1, 0))));

            // Connection ports (cables, wires, pipes, ducts, signal lines): every free port with a medium.
            list.AddRange(ConnectPorts(registry, doc, o, own, others.SelectMany(x => x.Anchors), lang));
            return list;
        }

        /// <summary>Free joints of a link: points docked nowhere that no other link or joint element is docked on.</summary>
        private static List<BuildPort> JointPorts(Registry registry, SketchDocument doc, SketchObject link)
        {
            var list = new List<BuildPort>();
            var pts = doc.ResolvePoints(link);
            if (pts.Count < 2) return list;
            var centre = new Vec2(pts.Average(q => q.X), pts.Average(q => q.Y));
            for (var i = 0; i < pts.Count; i++)
            {
                var docked = link.Attach is not null && i < link.Attach.Count && link.Attach[i] is not null;
                if (docked) continue;
                var jid = "j" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var taken = doc.Objects.Values.Any(x => x.Id != link.Id && x.Attach is not null && x.Attach.Any(a => a?.ObjectId == link.Id && a.AnchorId == jid));
                if (taken) continue;
                var d = pts[i] - centre;
                list.Add(new BuildPort("joint" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), PortKind.Joint, pts[i], d.Length > 1e-6 ? d * (1 / d.Length) : new Vec2(1, 0))
                {
                    AxisFrom = pts[i == 0 ? 1 : i - 1],
                });
            }
            return list;
        }

        /// <summary>
        /// After a joint element (fixed pivot, joint, slider) was placed at a free joint of a link: the link's point docked onto
        /// it, so the link turns about it. Null if nothing changes.
        /// </summary>
        public static SketchDocument? DockBack(Registry registry, SketchDocument doc, string linkId, BuildPort port, string placedId, Lang lang = Lang.En)
        {
            if (port.Kind != PortKind.Joint || doc.Get(placedId) is not { IsPointGeometry: false } box || doc.Get(linkId) is not { } link) return null;
            var c = registry.PageAnchors(box, new RenderContext(doc, lang)).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port);
            if (c is null || !int.TryParse(port.Id.Substring("joint".Length), out var i)) return null;
            return Edits.SetPoint(doc, link.Id, i, c.P, Attachments.For(box, c.P, c));
        }

        private static bool IsPin(Anchor a) => a.Id is { } id && id.StartsWith("pin", StringComparison.Ordinal)
            && (a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub || a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);

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
            if (port.Kind is PortKind.Connect or PortKind.LineEnd) return ConnectionItems(registry, doc, o, port, max, lang);
            if (port.Kind == PortKind.Joint)
            {
                // The next link (same kind and view), a fixed pivot, a joint symbol, a slider.
                var parts = registry.Library.Where(i => registry.Type(i.TypeId) is { } t && t.LinkageRole(registry.Create(i, Vec2.Zero)) != LinkageRole.None)
                    .GroupBy(i => i.TypeId)
                    .Select(g => g.FirstOrDefault(i => i.View == o.View) ?? g.First())
                    .OrderBy(i => i.TypeId == o.Type ? 0 : 1).ToList();
                return parts.Where(i => Place(registry, doc, id, port, i, lang) is not null).Take(max).ToList();
            }
            if (port.Kind == PortKind.Mount)
            {
                // Parts with a directed mount of the same id (tools for a flange): the element's suggestions first, then the
                // other types of its module (directed mounts are a module's own connection faces, so the search stays small).
                var mountId = port.Id.Substring("mount.".Length);
                var prefix = o.Type.Split('.')[0] + ".";
                var firsts = registry.Suggestions(o, 14)
                    .Concat(registry.Library.Where(i => i.TypeId.StartsWith(prefix, StringComparison.Ordinal)).GroupBy(i => i.TypeId).Select(g => g.First())).ToList();
                var fits = new List<LibraryItem>();
                foreach (var item in firsts)
                {
                    if (fits.Count >= max) break;
                    if (fits.Any(i => i.TypeId == item.TypeId) || registry.Type(item.TypeId) is null) continue;
                    var probe = registry.Create(item, Vec2.Zero);
                    if (probe.IsPointGeometry) continue;
                    if (!Anchors(registry, doc, probe, lang).Any(a => a.Role == AnchorRole.Mount && a.Id == mountId && a.Direction is not null)) continue;
                    if (Place(registry, doc, id, port, item, lang) is not null) fits.Add(item);
                }
                return fits;
            }
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
            var selKind = registry.Type(o.Type)!.MeshDirections(o).Count;
            foreach (var item in candidates)
            {
                if (list.Count >= max) break;
                if (list.Any(i => i.TypeId == item.TypeId)) continue;
                // Gears, carriers and hubs only take parts in the same kind of view (axial with axial, side with side).
                if (port.Kind is not PortKind.AxisEnd && registry.Type(item.TypeId) is { } it && it.MeshDirections(registry.Create(item, Vec2.Zero)).Count != selKind) continue;
                if (Place(registry, doc, id, port, item, lang) is not null) list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// The new object for <paramref name="item"/> docked at <paramref name="port"/> of <paramref name="id"/>, or null if it
        /// does not fit there. At a <see cref="PortKind.Connect"/> port: the connector line (start docked on the port) or the
        /// next element (without the connector that <see cref="Apply"/> adds); at a <see cref="PortKind.LineEnd"/>: the line
        /// itself, one segment longer (same id), or the element whose port sits on the line end. Prefer <see cref="Apply"/>.
        /// </summary>
        public static SketchObject? Place(Registry registry, SketchDocument doc, string id, BuildPort port, LibraryItem item, Lang lang = Lang.En)
        {
            var sel = doc.Get(id);
            if (sel is null || registry.Type(item.TypeId) is null) return null;
            if (port.Kind == PortKind.Joint) return PlaceAtJoint(registry, doc, sel, port, item, lang);
            if (port.Kind is PortKind.Connect or PortKind.LineEnd) return PlaceConnection(registry, doc, sel, port, item, lang)?.Obj;
            var probe = registry.Create(item, port.At);
            if (probe.IsPointGeometry) return null;
            // A scaled element passes its scale on: the new part fits it (lane widths, shaft diameter, tooth size). At an
            // axis end the scale across the axis counts, elsewhere the mean of both directions.
            if (port.Kind != PortKind.Mount && SourceScale(registry, sel) is { } sc)
            {
                double k;
                if (port.Kind == PortKind.AxisEnd)
                {
                    var r = -Geo.DegToRad(sel.Transform.Rotation);
                    var lx = port.Dir.X * Math.Cos(r) - port.Dir.Y * Math.Sin(r);
                    var ly = port.Dir.X * Math.Sin(r) + port.Dir.Y * Math.Cos(r);
                    k = Math.Sqrt(sc.X * ly * sc.X * ly + sc.Y * lx * sc.Y * lx);
                }
                else k = Math.Sqrt(sc.X * sc.Y);
                if (k > 1e-6 && Math.Abs(k - 1) > 1e-3) probe = probe with { Size = new Size2(Math.Round(probe.Size.W * k, 6), Math.Round(probe.Size.H * k, 6)) };
            }
            if (port.Kind == PortKind.Mount)
            {
                // Turn the part so its mount direction faces the port, then put its mount on the port.
                var mountId = port.Id.Substring("mount.".Length);
                var mount = Anchors(registry, doc, probe, lang).FirstOrDefault(a => a.Role == AnchorRole.Mount && a.Id == mountId && a.Direction is not null);
                if (mount is null) return null;
                var want = port.Dir * -1;
                var have = mount.Direction!.Value;
                var turn = (Math.Atan2(want.Y, want.X) - Math.Atan2(have.Y, have.X)) * 180 / Math.PI;
                var turned = probe with { Transform = probe.Transform with { Rotation = Math.Round(((probe.Transform.Rotation + turn) % 360 + 540) % 360 - 180, 6) } };
                // Faces of known size: the part is scaled so its face matches the port's (a gripper as wide as the flange).
                if (port.FaceRadius > 1e-6 && mount.Radius > 1e-6 && Math.Abs(port.FaceRadius / mount.Radius - 1) > 1e-3)
                {
                    var k = port.FaceRadius / mount.Radius;
                    turned = turned with { Size = new Size2(turned.Size.W * k, turned.Size.H * k) };
                }
                var tm = Anchors(registry, doc, turned, lang).First(a => a.Role == AnchorRole.Mount && a.Id == mountId);
                return Translate(turned, port.At - tm.P);
            }
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
                    if (axis is not null && hub is null) return Translate(probe, port.At - Vec2.Lerp(axis.P, axis.Q, 0.5));
                    return Concentric(registry, doc, sel, probe, port, lang);
                case PortKind.Bevel:
                    {
                        var apexP = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex);
                        if (apexP is null) return null;
                        // The mating gear sits on the port's side and points back at the shared apex.
                        var theta = Math.Atan2(-port.Dir.Y, -port.Dir.X) * 180 / Math.PI;
                        var turned = probe with { Transform = probe.Transform with { Rotation = Math.Round(theta, 6) } };
                        var ta = Anchors(registry, doc, turned, lang).First(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex);
                        return Translate(turned, port.At - ta.P);
                    }
                case PortKind.Pin:
                    if (pitch is null || hub is null) return null;
                    {
                        // Planet size: it meshes with the sun inside or the ring outside (whichever is there).
                        var (sun, ring) = Around(registry, doc, sel, port.AxisFrom, lang);
                        var r = port.At.DistanceTo(port.AxisFrom);
                        double? want = sun is double rs && r > rs ? r - rs : ring is double rr && rr > r ? rr - r : null;
                        var sized = want is double w ? registry.Type(probe.Type)!.ScalePitch(probe, w / pitch.Radius) : probe;
                        var h = Anchors(registry, doc, sized, lang).First(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
                        return Translate(sized, port.At - h.P);
                    }
                case PortKind.AxisEnd:
                    if (axis is not null && hub is null)
                    {
                        // The next shaft section (or road segment): collinear, starting where this axis ends. With several
                        // axes (lanes) every axis of the part is tried at the port; the placement where most of its axis
                        // ends meet the element's axis ends wins (both lanes of a road continue; an intersection arm takes
                        // a road on the right lanes); ties prefer the axis of the same id, then the first.
                        var axisId = PortAxisId(registry, doc, sel, port, lang);
                        var selEnds = registry.PageAnchors(sel, new RenderContext(doc, lang)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && !IsPin(a))
                            .SelectMany(a => new[] { a.P, a.Q }).ToList();
                        SketchObject? best = null;
                        var bestScore = -1;
                        foreach (var cand in anchors.Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && !IsPin(a)).GroupBy(a => a.Id ?? "").Select(g => g.First()))
                        {
                            var own = cand.Q - cand.P;
                            if (own.Length < 1e-6) continue;
                            var turn = (Math.Atan2(port.Dir.Y, port.Dir.X) - Math.Atan2(own.Y, own.X)) * 180 / Math.PI;
                            var turned = probe with { Transform = probe.Transform with { Rotation = Math.Round(probe.Transform.Rotation + turn, 6) } };
                            var turnedAxes = Anchors(registry, doc, turned, lang).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && !IsPin(a)).ToList();
                            var ta = turnedAxes.FirstOrDefault(a => (a.Id ?? "") == (cand.Id ?? ""));
                            if (ta is null) continue;
                            var start = (ta.P - port.At).Dot(port.Dir) < (ta.Q - port.At).Dot(port.Dir) ? ta.P : ta.Q;
                            var d = port.At - start;
                            var placed = Translate(turned, d);
                            var score = turnedAxes.SelectMany(a => new[] { a.P + d, a.Q + d }).Distinct().Count(p => selEnds.Any(e => e.DistanceTo(p) <= 0.5)) * 2
                                + (axisId is not null && cand.Id == axisId ? 1 : 0);
                            if (score > bestScore) { best = placed; bestScore = score; }
                        }
                        return best;
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
        /// At a free joint of a link: the next link of the same length continuing outwards, docked on the joint; or a joint
        /// element (fixed pivot, joint, slider) with its joint point on the link's joint.
        /// </summary>
        private static SketchObject? PlaceAtJoint(Registry registry, SketchDocument doc, SketchObject link, BuildPort port, LibraryItem item, Lang lang)
        {
            var type = registry.Type(item.TypeId)!;
            var probe = registry.Create(item, port.At);
            var role = type.LinkageRole(probe);
            if (role == LinkageRole.None) return null;
            var index = int.Parse(port.Id.Substring("joint".Length), System.Globalization.CultureInfo.InvariantCulture);
            if (probe.IsPointGeometry)
            {
                if (role != LinkageRole.Link || probe.Points is not { Count: 2 }) return null;
                var len = Math.Max(5, port.At.DistanceTo(port.AxisFrom));
                // Bent by 60° (clockwise on screen): three links between two pivots form a chain that can move.
                var c60 = Math.Cos(Math.PI / 3);
                var s60 = Math.Sin(Math.PI / 3);
                var dir = new Vec2(port.Dir.X * c60 - port.Dir.Y * s60, port.Dir.X * s60 + port.Dir.Y * c60);
                var end = port.At + dir * len;
                var next = registry.CreateBetween(item, port.At, end);
                return next with { Attach = new Attachment?[] { new Attachment(link.Id, 0, 0) { AnchorId = "j" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) }, null } };
            }
            var c = Anchors(registry, doc, probe, lang).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port);
            return c is null ? null : Translate(probe, port.At - c.P);
        }

        /// <summary>
        /// A part placed concentric with the selection's hub: a sun or ring gear sized to the planets on a carrier, a carrier
        /// sized to a sun gear (planets of 0.8 × the sun), a ring gear around a sun. Null if the item is none of these.
        /// </summary>
        private static SketchObject? Concentric(Registry registry, SketchDocument doc, SketchObject sel, SketchObject probe, BuildPort port, Lang lang)
        {
            var pa = Anchors(registry, doc, probe, lang);
            var pPitch = pa.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var pRing = pa.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.PitchInner);
            var pPins = pa.Where(IsPin).ToList();
            var sa = registry.PageAnchors(sel, new RenderContext(doc, lang)).ToList();
            var selPitch = sa.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var selPin = sa.FirstOrDefault(IsPin);
            var type = registry.Type(probe.Type)!;
            SketchObject sized;
            if (selPin is not null)
            {
                // On a carrier: pin radius R, planet radius from a planet already on a pin (else 0.45 R).
                var R = (selPin.Kind == AnchorKind.Point ? selPin.P : Vec2.Lerp(selPin.P, selPin.Q, 0.5)).DistanceTo(port.At);
                var planet = PlanetOn(registry, doc, sel, lang) ?? R * 0.45;
                if (pPitch is not null) sized = type.ScalePitch(probe, (R - planet) / pPitch.Radius);
                else if (pRing is not null) sized = type.ScalePitch(probe, (R + planet) / pRing.Radius);
                else return null;
            }
            else if (selPitch is not null && pPins.Count > 0)
            {
                // A carrier on a sun gear: planets of 0.8 × the sun fit on its pins.
                var hubP = pa.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == "hub")?.P ?? new Vec2(probe.Transform.X, probe.Transform.Y);
                var pinR = (pPins[0].Kind == AnchorKind.Point ? pPins[0].P : Vec2.Lerp(pPins[0].P, pPins[0].Q, 0.5)).DistanceTo(hubP);
                if (pinR < 1e-6) return null;
                sized = type.ScalePitch(probe, selPitch.Radius * 1.8 / pinR);
            }
            else if (selPitch is not null && pRing is not null)
                sized = type.ScalePitch(probe, selPitch.Radius * 2.6 / pRing.Radius);
            else return null;
            var h = Anchors(registry, doc, sized, lang).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == "hub")
                ?? Anchors(registry, doc, sized, lang).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
            return h is null ? null : Translate(sized, port.At - h.P);
        }

        /// <summary>Pitch radius of a gear sitting on one of the carrier's pins, else null.</summary>
        private static double? PlanetOn(Registry registry, SketchDocument doc, SketchObject carrier, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            var pins = registry.PageAnchors(carrier, ctx).Where(IsPin).ToList();
            foreach (var x in doc.Objects.Values.Where(x => x.Id != carrier.Id && !x.IsPointGeometry && registry.Type(x.Type) is not null))
            {
                var xa = registry.PageAnchors(x, ctx).ToList();
                var h = xa.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
                var pc = xa.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
                if (h is null || pc is null) continue;
                if (pins.Any(pin => pin.Kind == AnchorKind.Point ? pin.P.DistanceTo(h.P) <= 0.5 : Geo.ClosestOnSegment(h.P, pin.P, pin.Q).Point.DistanceTo(h.P) <= 0.3))
                    return pc.Radius;
            }
            return null;
        }

        /// <summary>Pitch radii of a sun (external) and a ring gear (internal) concentric with <paramref name="centre"/>.</summary>
        private static (double? Sun, double? Ring) Around(Registry registry, SketchDocument doc, SketchObject except, Vec2 centre, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            double? sun = null, ring = null;
            foreach (var x in doc.Objects.Values.Where(x => x.Id != except.Id && !x.IsPointGeometry && registry.Type(x.Type) is not null))
                foreach (var a in registry.PageAnchors(x, ctx).Where(a => a.Kind == AnchorKind.Circle && a.P.DistanceTo(centre) <= 0.5))
                {
                    if (a.Role == AnchorRole.Pitch) sun ??= a.Radius;
                    else if (a.Role == AnchorRole.PitchInner) ring ??= a.Radius;
                }
            return (sun, ring);
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

        /// <summary>The id of the axis an <see cref="PortKind.AxisEnd"/> port ("axis{k}.start|end") sits on, or null.</summary>
        private static string? PortAxisId(Registry registry, SketchDocument doc, SketchObject sel, BuildPort port, Lang lang)
        {
            if (!port.Id.StartsWith("axis", StringComparison.Ordinal)) return null;
            var dot = port.Id.IndexOf('.');
            if (dot < 0 || !int.TryParse(port.Id.Substring(4, dot - 4), out var k)) return null;
            var axes = registry.PageAnchors(sel, new RenderContext(doc, lang)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && !IsPin(a)).ToList();
            return k < axes.Count ? axes[k].Id : null;
        }

        /// <summary>
        /// How much an element was scaled since it was inserted, per local direction: its size against the size of the
        /// library item it matches (same type, view and item parameters). Null if no item matches (no reliable reference).
        /// </summary>
        public static Vec2? SourceScale(Registry registry, SketchObject o)
        {
            if (o.IsPointGeometry) return null;
            var item = registry.Library
                .Where(i => i.TypeId == o.Type && (i.View is null || i.View == o.View)
                    && (i.Params is null || i.Params.All(kv => o.Params.TryGetValue(kv.Key, out var v) && v.Equals(kv.Value))))
                .OrderByDescending(i => i.View == o.View ? 1 : 0).ThenByDescending(i => i.Params?.Count ?? 0)
                .FirstOrDefault();
            if (item is null) return null;
            var type = registry.Type(o.Type);
            var reference = item.Size ?? type?.DefaultSize;
            if (reference is not { } r || r.W < 1e-6 || r.H < 1e-6) return null;
            // Parameters that change the natural size (more lanes) are not a scale: the item's size is carried over to the
            // element's parameters first.
            if (type?.NaturalSize(o) is { } no && type.NaturalSize(registry.Create(item, Vec2.Zero)) is { } ni && ni.W > 1e-6 && ni.H > 1e-6)
                r = new Size2(r.W * no.W / ni.W, r.H * no.H / ni.H);
            return new Vec2(o.Size.W / r.W, o.Size.H / r.H);
        }

        private static SketchObject Translate(SketchObject o, Vec2 d)
            => o with { Transform = o.Transform with { X = Math.Round(o.Transform.X + d.X, 6), Y = Math.Round(o.Transform.Y + d.Y, 6) } };
    }
}
