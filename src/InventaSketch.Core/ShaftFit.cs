using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Parts fit onto their shaft: a bearing, gear, pulley, sprocket, coupling or wheel put on a shaft gets the shaft's
    /// diameter as its bore, so nobody has to scale it by hand. Types tell the radii through their anchors: a
    /// <see cref="AnchorRole.Hub"/> point carries the bore radius of the part at its current size (in every view the type
    /// offers: side, section and schematic views across the axis, axial views as the round bore), an
    /// <see cref="AnchorRole.Axis"/> segment the shaft radius (<see cref="Anchor.RadiusAt"/>: the section at the hub on a
    /// stepped shaft). The part is scaled uniformly (Size × k, Quick Sketch: parameters stay) about its hub, so it stays on
    /// the axis. Anchors without a radius are left alone.
    /// <para>
    /// Used by <see cref="Fitting.Place"/>, <see cref="Fitting.PlaceNear"/>, <see cref="Building"/> (a part at a free axis
    /// end) and <see cref="Coupling.Propagate"/> (a part dragged and snapped onto a shaft, or a shaft dragged through it).
    /// </para>
    /// </summary>
    public static class ShaftFit
    {
        /// <summary>A hub this close to an axis segment (mm) sits on it (as for <see cref="Coupling"/>).</summary>
        public const double OnAxis = 0.3;

        /// <summary>Fits outside this range of factors are ignored (degenerate radii).</summary>
        public const double MinFactor = 0.02, MaxFactor = 50;

        /// <summary>True for an axis segment that knows its shaft radius.</summary>
        public static bool HasRadius(Anchor axis)
            => axis.Kind == AnchorKind.Segment && axis.Role == AnchorRole.Axis && (axis.Radius > Geo.Eps || axis.Steps is { Count: > 0 });

        /// <summary>True for a hub point that knows its bore radius.</summary>
        public static bool HasBore(Anchor hub) => hub.Kind == AnchorKind.Point && hub.Role == AnchorRole.Hub && hub.Radius > Geo.Eps;

        /// <summary>
        /// The uniform factor that makes the bore of <paramref name="hub"/> equal to the shaft at the hub (shaft radius at the
        /// hub / bore radius), or null when either radius is unknown or the factor is degenerate.
        /// </summary>
        public static double? Factor(Anchor hub, Anchor axis)
        {
            if (!HasBore(hub) || !HasRadius(axis)) return null;
            var r = axis.RadiusAt(hub.P);
            if (r <= Geo.Eps) return null;
            var k = r / hub.Radius;
            return k < MinFactor || k > MaxFactor ? null : k;
        }

        /// <summary><paramref name="o"/> scaled uniformly by <paramref name="k"/> (Size × k) so the page point <paramref name="fixedPoint"/> stays put.</summary>
        public static SketchObject ScaleAbout(SketchObject o, double k, Vec2 fixedPoint)
        {
            var c = new Vec2(o.Transform.X, o.Transform.Y);
            var nc = fixedPoint - (fixedPoint - c) * k;
            return o with
            {
                Size = new Size2(Math.Round(o.Size.W * k, 6), Math.Round(o.Size.H * k, 6)),
                Transform = o.Transform with { X = Math.Round(nc.X, 6), Y = Math.Round(nc.Y, 6) },
            };
        }

        /// <summary>Page anchors of <paramref name="o"/>, which need not be part of <paramref name="doc"/> yet.</summary>
        internal static List<Anchor> AnchorsOf(Registry registry, SketchDocument doc, SketchObject o, Lang lang)
        {
            var tmp = doc.Get(o.Id) is null ? Edits.Add(doc, doc.Pages[0].Id, o) : Edits.Replace(doc, o);
            return registry.PageAnchors(tmp.Get(o.Id)!, new RenderContext(tmp, lang)).ToList();
        }

        /// <summary>
        /// <paramref name="part"/> fitted onto the shaft <paramref name="axis"/> (page anchor): its bore hub nearest the axis
        /// gets the shaft radius there, the part scaled uniformly about that hub. Unchanged when the part has no bore or the
        /// axis no radius (or it already fits).
        /// </summary>
        public static SketchObject Scale(Registry registry, SketchDocument doc, SketchObject part, Anchor axis, Lang lang = Lang.En)
        {
            if (part.IsPointGeometry || !HasRadius(axis) || registry.Type(part.Type) is null) return part;
            var hub = AnchorsOf(registry, doc, part, lang).Where(HasBore)
                .OrderBy(h => Geo.ClosestOnSegment(h.P, axis.P, axis.Q).Point.DistanceTo(h.P)).FirstOrDefault();
            if (hub is null || Factor(hub, axis) is not double k || Math.Abs(k - 1) < 1e-6) return part;
            return ScaleAbout(part, k, hub.P);
        }

        /// <summary>
        /// The shaft <paramref name="shaft"/> made as thick as the bore of <paramref name="hub"/> (a page anchor of the part it
        /// runs through): scaled across its axis only (the length stays), its axis kept through the hub. Unchanged when the
        /// radii are unknown.
        /// </summary>
        public static SketchObject ScaleShaft(Registry registry, SketchDocument doc, SketchObject shaft, Anchor hub, Lang lang = Lang.En)
        {
            if (shaft.IsPointGeometry || !HasBore(hub) || registry.Type(shaft.Type) is null) return shaft;
            var axis = AnchorsOf(registry, doc, shaft, lang).Where(HasRadius)
                .OrderBy(a => Geo.ClosestOnSegment(hub.P, a.P, a.Q).Point.DistanceTo(hub.P)).FirstOrDefault();
            if (axis is null) return shaft;
            var r = axis.RadiusAt(hub.P);
            if (r <= Geo.Eps) return shaft;
            var k = hub.Radius / r;
            if (k < MinFactor || k > MaxFactor || Math.Abs(k - 1) < 1e-6) return shaft;
            var local = shaft.Matrix().Invert().ApplyLinear(axis.Q - axis.P);
            var size = Math.Abs(local.X) >= Math.Abs(local.Y)
                ? new Size2(shaft.Size.W, Math.Round(shaft.Size.H * k, 6))
                : new Size2(Math.Round(shaft.Size.W * k, 6), shaft.Size.H);
            var sized = shaft with { Size = size };
            // Keep the axis line through the hub (an axis off the box centre moves when the box grows).
            var now = AnchorsOf(registry, doc, sized, lang).Where(HasRadius)
                .OrderBy(a => Geo.ClosestOnSegment(hub.P, a.P, a.Q).Point.DistanceTo(hub.P)).FirstOrDefault();
            if (now is null) return sized;
            var d = hub.P - Geo.ProjectOnLine(hub.P, now.P, now.Q);
            return d.Length < 1e-9 ? sized : sized with { Transform = sized.Transform with { X = Math.Round(sized.Transform.X + d.X, 6), Y = Math.Round(sized.Transform.Y + d.Y, 6) } };
        }

        /// <summary>
        /// The closest shaft axis (an <see cref="AnchorRole.Axis"/> segment with a radius) of another object within
        /// <paramref name="reach"/> of <paramref name="p"/> (measured from the shaft surface: the shaft radius there plus the
        /// reach), with the shaft object and the projection of the point onto the axis; null if none.
        /// </summary>
        public static (SketchObject Shaft, Anchor Axis, Vec2 At)? Nearest(Registry registry, SketchDocument doc, Vec2 p, double reach, string? excludeId = null, Lang lang = Lang.En)
        {
            var ctx = new RenderContext(doc, lang);
            (SketchObject, Anchor, Vec2)? best = null;
            var bestDist = double.MaxValue;
            foreach (var o in doc.Objects.Values.Where(o => o.Id != excludeId && !o.IsPointGeometry && registry.Type(o.Type) is not null))
                foreach (var a in registry.PageAnchors(o, ctx).Where(HasRadius))
                {
                    if (a.Id is { } id && id.StartsWith("pin", StringComparison.Ordinal)) continue;
                    var (at, _) = Geo.ClosestOnSegment(p, a.P, a.Q);
                    var dist = at.DistanceTo(p);
                    if (dist > reach + a.RadiusAt(at) + 1e-9 || dist >= bestDist) continue;
                    bestDist = dist;
                    best = (o, a, at);
                }
            return best;
        }

        /// <summary>
        /// Seats parts that came onto a shaft with an edit (dragged and snapped onto its axis, inserted there, or a shaft
        /// dragged through their hub): every part whose bore hub lies on a shaft axis with a radius now, but did not lie on
        /// that shaft before (or is new), is fitted to the shaft (<see cref="Scale"/>). Parts that already sat on the shaft
        /// keep the size the user gave them; released pairs are left alone.
        /// </summary>
        internal static SketchDocument Seat(Registry registry, SketchDocument before, SketchDocument after, Lang lang)
        {
            var ctx = new RenderContext(after, lang);
            var boxes = after.Objects.Values.Where(o => !o.IsPointGeometry && registry.Type(o.Type) is not null).ToList();
            var anchors = boxes.ToDictionary(o => o.Id, o => registry.PageAnchors(o, ctx).ToList(), StringComparer.Ordinal);
            var shafts = boxes.Select(o => (Obj: o, Axes: anchors[o.Id].Where(HasRadius).ToList())).Where(x => x.Axes.Count > 0).ToList();
            if (shafts.Count == 0) return after;
            var ctx0 = new RenderContext(before, lang);
            var doc = after;
            foreach (var part in boxes)
            {
                var hubs = anchors[part.Id].Where(HasBore).ToList();
                if (hubs.Count == 0) continue;
                var p0 = before.Get(part.Id);
                var partChanged = p0 is null || !Coupling.SameState(p0, part);
                foreach (var (shaft, axes) in shafts)
                {
                    if (shaft.Id == part.Id || Coupling.IsReleased(after, part.Id, shaft.Id)) continue;
                    var on = hubs.SelectMany(h => axes.Select(a => (Hub: h, Axis: a)))
                        .FirstOrDefault(x => Geo.ClosestOnSegment(x.Hub.P, x.Axis.P, x.Axis.Q).Point.DistanceTo(x.Hub.P) <= OnAxis);
                    if (on.Hub is null) continue;
                    var s0 = before.Get(shaft.Id);
                    if (!partChanged && s0 is not null && Coupling.SameState(s0, shaft)) break;
                    if (p0 is not null && s0 is not null && Carried(registry, ctx0, p0, s0)) break;
                    var current = doc.Get(part.Id)!;
                    var k = Factor(on.Hub, on.Axis);
                    if (k is double f && Math.Abs(f - 1) > 1e-3) doc = Edits.Replace(doc, ScaleAbout(current, f, on.Hub.P));
                    break;
                }
            }
            return doc;
        }

        /// <summary>True if a hub of <paramref name="part"/> lay on an axis of <paramref name="shaft"/> (before the edit).</summary>
        private static bool Carried(Registry registry, RenderContext ctx, SketchObject part, SketchObject shaft)
        {
            var hubs = registry.PageAnchors(part, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).ToList();
            if (hubs.Count == 0) return false;
            var axes = registry.PageAnchors(shaft, ctx).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis).ToList();
            return hubs.Any(h => axes.Any(a => Geo.ClosestOnSegment(h.P, a.P, a.Q).Point.DistanceTo(h.P) <= OnAxis));
        }
    }
}
