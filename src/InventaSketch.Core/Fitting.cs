using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// "Insert a fitting element" without aiming: places a library item where it belongs relative to a selected object, so
    /// it is coupled right away (<see cref="Coupling"/>, <see cref="Links"/>):
    /// <list type="bullet">
    /// <item>a part with a hub (gear, bearing, wheel, coupling) on a selected shaft: hub on the axis, at a free place
    /// along it, turned like the shaft, its bore fitted to the shaft (<see cref="ShaftFit"/>);</item>
    /// <item>a gear next to a selected gear: in mesh (pitch circles touch), below, above, right or left of it, wherever
    /// it does not overlap other elements;</item>
    /// <item>a shaft for a selected part with a hub: the axis through the hub, as thick as the bore.</item>
    /// </list>
    /// The item keeps its view (pick the item in the selection's view, see <see cref="Registry.Suggestions"/>). Returns null
    /// when no rule fits; hosts then let the user place it with the pointer. An optional preference point (where the user
    /// clicked) puts the part on that side: a meshing gear in the direction of the point (any angle), a part on a shaft at
    /// the point's projection onto the axis.
    /// </summary>
    public static class Fitting
    {
        /// <summary>
        /// The new object placed relative to <paramref name="anchorId"/>, or null if no rule applies. <paramref name="toward"/>
        /// (page mm, optional): the side where the user clicked. A gear in mesh goes in the direction of that point from the
        /// selected gear's centre (any angle in axial views; across the axis on the point's side for gears seen from the side),
        /// falling back to the free sides when it would overlap another element; a part on a shaft goes to the axis position
        /// nearest the point's projection (clamped to the shaft, moved off the hubs already there). A shaft through a part
        /// ignores it.
        /// </summary>
        public static SketchObject? Place(Registry registry, SketchDocument doc, string anchorId, LibraryItem item, Lang lang = Lang.En, Vec2? toward = null)
        {
            var sel = doc.Get(anchorId);
            if (sel is null || sel.IsPointGeometry || registry.Type(sel.Type) is null || registry.Type(item.TypeId) is null) return null;
            var ctx = new RenderContext(doc, lang);
            var selAnchors = registry.PageAnchors(sel, ctx).ToList();
            var centre = new Vec2(sel.Transform.X, sel.Transform.Y);
            var probe = registry.Create(item, centre) with { Transform = new Transform(centre.X, centre.Y, sel.Transform.Rotation) };
            if (probe.IsPointGeometry) return null;
            var newAnchors = Anchors(registry, doc, probe, lang);

            var axis = selAnchors.FirstOrDefault(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);
            var newHub = newAnchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
            var selPitch = selAnchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var newPitch = newAnchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var selHub = selAnchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub);
            var newAxis = newAnchors.FirstOrDefault(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);

            // Gear next to a gear: in mesh.
            if (selPitch is not null && newPitch is not null)
                return Mesh(registry, doc, sel, probe, selPitch, newPitch, lang, toward);
            // Part on a shaft: its bore fits the shaft (ShaftFit).
            if (axis is not null && newHub is not null)
                return OnShaft(registry, doc, sel, axis, probe, lang, toward);
            // Shaft through a part: as thick as the part's bore.
            if (selHub is not null && newAxis is not null)
            {
                var through = Translate(probe, selHub.P - Vec2.Lerp(newAxis.P, newAxis.Q, 0.5));
                var bore = selAnchors.Where(ShaftFit.HasBore).OrderBy(h => h.P.DistanceTo(selHub.P)).FirstOrDefault();
                return bore is null ? through : ShaftFit.ScaleShaft(registry, doc, through, bore, lang);
            }
            // Part next to a part on a shaft (bearing beside a gear): on the same shaft, turned like it, fitted to it.
            if (selHub is not null && newHub is not null && CarrierOf(registry, doc, sel, selHub.P, lang) is { } carrier)
            {
                var onShaft = probe with { Transform = probe.Transform with { Rotation = carrier.Shaft.Transform.Rotation } };
                return OnShaft(registry, doc, carrier.Shaft, carrier.Axis, onShaft, lang, toward);
            }
            return null;
        }

        /// <summary>
        /// <paramref name="probe"/> (turned like the shaft) with its hub on the shaft axis, fitted to the shaft: at the axis
        /// position nearest <paramref name="toward"/> if given, else at a free place (<see cref="OnAxis"/>).
        /// </summary>
        private static SketchObject? OnShaft(Registry registry, SketchDocument doc, SketchObject shaft, Anchor axis, SketchObject probe, Lang lang, Vec2? toward)
        {
            var hub = Anchors(registry, doc, probe, lang).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub)
                .OrderBy(a => a.Radius > 0 ? 0 : 1).FirstOrDefault();
            if (hub is null) return null;
            if (toward is not Vec2 t) return ShaftFit.Scale(registry, doc, Translate(probe, OnAxis(registry, doc, shaft, axis, lang) - hub.P), axis, lang);
            // Fit first (about the hub at the projection), then keep the fitted part off the hubs already there.
            var at0 = Geo.ClosestOnSegment(t, axis.P, axis.Q).Point;
            var fitted = ShaftFit.Scale(registry, doc, Translate(probe, at0 - hub.P), axis, lang);
            var at = Nearest(registry, doc, shaft, axis, t, fitted.Size.W, lang);
            return at.DistanceTo(at0) < 1e-9 ? fitted : ShaftFit.Scale(registry, doc, Translate(fitted, at - at0), axis, lang);
        }

        /// <summary>
        /// The axis position nearest the projection of <paramref name="toward"/> (clamped to the shaft) where a part of the
        /// given width (along the axis) does not sit on a hub already on the shaft; the projection itself when no free place
        /// is left.
        /// </summary>
        private static Vec2 Nearest(Registry registry, SketchDocument doc, SketchObject shaft, Anchor axis, Vec2 toward, double width, Lang lang)
        {
            var (_, t0) = Geo.ClosestOnSegment(toward, axis.P, axis.Q);
            var len = axis.P.DistanceTo(axis.Q);
            if (len < Geo.Eps) return axis.P;
            var ctx = new RenderContext(doc, lang);
            var taken = new List<(double T, double Half)>();
            foreach (var o in doc.Objects.Values.Where(o => o.Id != shaft.Id && !o.IsPointGeometry && registry.Type(o.Type) is not null))
                foreach (var h in registry.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub))
                {
                    var (p, t) = Geo.ClosestOnSegment(h.P, axis.P, axis.Q);
                    if (p.DistanceTo(h.P) <= ShaftFit.OnAxis) taken.Add((t, o.Size.W / 2 / len));
                }
            var half = width / 2 / len;
            bool Free(double t) => taken.All(x => Math.Abs(x.T - t) >= x.Half + half - 1e-9);
            if (Free(t0)) return Vec2.Lerp(axis.P, axis.Q, t0);
            const double step = 0.0025;
            for (var k = 1; k * step <= 1; k++)
                foreach (var t in new[] { t0 + k * step, t0 - k * step })
                    if (t >= 0 && t <= 1 && Free(t)) return Vec2.Lerp(axis.P, axis.Q, t);
            return Vec2.Lerp(axis.P, axis.Q, t0);
        }

        /// <summary>
        /// Live placement near a shaft (insertion preview): the object for <paramref name="item"/> on the shaft axis closest to
        /// <paramref name="pointer"/> (page mm) within <paramref name="reach"/> of the shaft surface, its hub at the pointer's
        /// projection onto the axis, turned like the shaft, in the shaft's kind of view (schematic, side or section; see
        /// <see cref="ViewFor"/>) and scaled so its bore fits the shaft (<see cref="ShaftFit"/>). Null when no shaft is within
        /// reach or the item has no hub; hosts then place the item freely.
        /// </summary>
        public static SketchObject? PlaceNear(Registry registry, SketchDocument doc, LibraryItem item, Vec2 pointer, double reach, Lang lang = Lang.En)
        {
            var type = registry.Type(item.TypeId);
            if (type is null || doc.Pages.Count == 0) return null;
            if (ShaftFit.Nearest(registry, doc, pointer, reach, null, lang) is not { } near) return null;
            var (shaft, axis, at) = near;
            var probe = registry.Create(item, at);
            if (probe.IsPointGeometry) return null;
            var view = ViewFor(registry, shaft, probe);
            if (view is not null) probe = registry.WithView(probe, view, new RenderContext(doc, lang));
            probe = probe with { Transform = new Transform(at.X, at.Y, shaft.Transform.Rotation) };
            // Same rule as Place with a preference point: nearest the pointer's projection, off the hubs already there.
            return OnShaft(registry, doc, shaft, axis, probe, lang, pointer);
        }

        /// <summary>
        /// The view of <paramref name="part"/>'s type that matches the kind of view of <paramref name="shaft"/> (by the views'
        /// pictograms, <see cref="IconIds.ForView"/>): schematic for a schematic line; side, else simplified, section or detailed
        /// for a shaft body; section, else detailed, side or simplified for a sectioned shaft. Null when the part offers none of
        /// them (it keeps its view, for instance an axial view).
        /// </summary>
        public static string? ViewFor(Registry registry, SketchObject shaft, SketchObject part)
        {
            var st = registry.Type(shaft.Type);
            var pt = registry.Type(part.Type);
            if (st is null || pt is null) return null;
            var sv = st.Views.FirstOrDefault(v => v.Id == st.ViewOf(shaft));
            if (sv is null) return null;
            var wanted = IconIds.ForView(sv) switch
            {
                IconIds.Schematic => new[] { IconIds.Schematic },
                IconIds.Section => new[] { IconIds.Section, IconIds.Detailed, IconIds.Side, IconIds.Simplified },
                IconIds.Side => new[] { IconIds.Side, IconIds.Simplified, IconIds.Section, IconIds.Detailed },
                _ => Array.Empty<string>(),
            };
            foreach (var w in wanted)
                if (pt.Views.FirstOrDefault(v => IconIds.ForView(v) == w) is { } match) return match.Id;
            return null;
        }

        /// <summary>The shaft whose axis passes through <paramref name="hub"/> (the part's carrier), if any.</summary>
        private static (SketchObject Shaft, Anchor Axis)? CarrierOf(Registry registry, SketchDocument doc, SketchObject part, Vec2 hub, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            foreach (var o in doc.Objects.Values.Where(o => o.Id != part.Id && !o.IsPointGeometry && registry.Type(o.Type) is not null))
                foreach (var a in registry.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis))
                    if (Geo.ClosestOnSegment(hub, a.P, a.Q).Point.DistanceTo(hub) <= 0.3) return (o, a);
            return null;
        }

        private static List<Anchor> Anchors(Registry registry, SketchDocument doc, SketchObject o, Lang lang)
        {
            var tmp = Edits.Add(doc, doc.Pages[0].Id, o);
            return registry.PageAnchors(tmp.Get(o.Id)!, new RenderContext(tmp, lang)).ToList();
        }

        private static SketchObject Translate(SketchObject o, Vec2 d)
            => o with { Transform = o.Transform with { X = Math.Round(o.Transform.X + d.X, 6), Y = Math.Round(o.Transform.Y + d.Y, 6) } };

        /// <summary>
        /// A free place on the shaft axis: the middle of an empty shaft, else the position farthest from the hubs already on
        /// it and from the shaft ends.
        /// </summary>
        private static Vec2 OnAxis(Registry registry, SketchDocument doc, SketchObject shaft, Anchor axis, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            var taken = new List<double>();
            foreach (var o in doc.Objects.Values.Where(o => o.Id != shaft.Id && !o.IsPointGeometry && registry.Type(o.Type) is not null))
                foreach (var h in registry.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub))
                {
                    var (p, t) = Geo.ClosestOnSegment(h.P, axis.P, axis.Q);
                    if (p.DistanceTo(h.P) <= 0.3) taken.Add(t);
                }
            var best = 0.5;
            if (taken.Count > 0)
            {
                var bestScore = double.MinValue;
                for (var t = 0.1; t <= 0.9001; t += 0.05)
                {
                    var score = Math.Min(taken.Min(x => Math.Abs(x - t)), Math.Min(t, 1 - t));
                    if (score > bestScore + 1e-9 || (Math.Abs(score - bestScore) <= 1e-9 && Math.Abs(t - 0.5) < Math.Abs(best - 0.5))) { bestScore = score; best = t; }
                }
            }
            return Vec2.Lerp(axis.P, axis.Q, best);
        }

        /// <summary>
        /// The new gear in mesh with the selected one: towards <paramref name="toward"/> if given (any angle; across the axis on
        /// its side for gears seen from the side), else (or when that overlaps) on the first free side (local down, up, right,
        /// left).
        /// </summary>
        private static SketchObject Mesh(Registry registry, SketchDocument doc, SketchObject sel, SketchObject probe, Anchor selPitch, Anchor newPitch, Lang lang, Vec2? toward = null)
        {
            var d = selPitch.Radius + newPitch.Radius;
            var rad = Geo.DegToRad(sel.Transform.Rotation);
            Vec2 Turn(Vec2 v) => new(v.X * Math.Cos(rad) - v.Y * Math.Sin(rad), v.X * Math.Sin(rad) + v.Y * Math.Cos(rad));
            var dirs = new[] { new Vec2(0, 1), new Vec2(0, -1), new Vec2(1, 0), new Vec2(-1, 0) }.Select(Turn).ToList();
            if (toward is Vec2 tw && (tw - selPitch.P).Length > Geo.Eps)
            {
                var want = (tw - selPitch.P).Normalized();
                if (selPitch.AxisOf() is { } ax)
                {
                    // Seen from the side: the partner sits across the axis, on the side of the point.
                    var n = new Vec2(-ax.Y, ax.X);
                    want = want.Dot(n) >= 0 ? n : n * -1;
                }
                dirs.Insert(0, want);
            }
            var others = doc.Objects.Values.Where(o => o.Id != sel.Id && !o.IsPointGeometry && registry.Type(o.Type) is not null).Select(o => o.Bounds()).ToList();
            SketchObject? first = null;
            foreach (var dir in dirs)
            {
                var target = selPitch.P + dir * d;
                var placed = Translate(probe, target - newPitch.P);
                first ??= placed;
                var b = placed.Bounds();
                var inner = new Rect(b.X + 1, b.Y + 1, Math.Max(0, b.W - 2), Math.Max(0, b.H - 2));
                if (!others.Any(r => r.Intersects(inner))) return placed;
            }
            return first!;
        }
    }
}
