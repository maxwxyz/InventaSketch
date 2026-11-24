using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// One part of an assembly that can be exploded (<see cref="ObjectType.Parts"/>): a library item (type and view) at a
    /// point of the assembly's box (local mm, centred), sized by a pitch radius, ring pitch radius or pin radius (local mm),
    /// turned by <see cref="Rotation"/> relative to the assembly, with extra parameters (teeth, number of pins …).
    /// </summary>
    public sealed record PartSpec(string TypeId, string? View, Vec2 Local)
    {
        /// <summary>Points of a line part (link, wire …) in the assembly's local mm; <see cref="Local"/> is then ignored.</summary>
        public IReadOnlyList<Vec2>? Points { get; init; }
        /// <summary>Per point of a line part: the part (index in the list) and its anchor id the point docks onto, or null.</summary>
        public IReadOnlyList<PartDock?>? Docks { get; init; }
        /// <summary>Anchor id of a box part that goes to <see cref="Local"/> (e.g. the pivot point "c"), instead of its hub/centre.</summary>
        public string? AlignAnchor { get; init; }
        /// <summary>Box size in mm (else the library size).</summary>
        public Size2? Size { get; init; }
        public double? Pitch { get; init; }
        public double? InnerPitch { get; init; }
        /// <summary>Distance of the first pin from the hub (carriers).</summary>
        public double? PinRadius { get; init; }
        public double Rotation { get; init; }
        public IReadOnlyDictionary<string, ParamValue>? Params { get; init; }
    }

    /// <summary>A point of a line part docked onto anchor <paramref name="AnchorId"/> of part <paramref name="Part"/> (index in the part list).</summary>
    public sealed record PartDock(int Part, string AnchorId);

    /// <summary>
    /// Exploding a predefined assembly (planetary set, differential …) into its parts: each part becomes an element of its
    /// own, placed and sized so the parts are coupled as before (meshing gears, planets on the carrier pins, concentric
    /// hubs) and can be edited one by one.
    /// </summary>
    public static class Assemblies
    {
        public static bool CanExplode(Registry registry, SketchObject o)
            => !o.IsPointGeometry && (registry.Type(o.Type)?.Parts(o) is { Count: > 0 } || RecipeView(registry, o) is not null);

        /// <summary>
        /// Another view of <paramref name="o"/> that has a parts recipe when its own view has none (a planetary set drawn as
        /// a stick diagram is exploded in its axial view and the parts are switched back, <see cref="AssemblyViews"/>).
        /// </summary>
        private static string? RecipeView(Registry registry, SketchObject o)
        {
            if (registry.Type(o.Type) is not { } type || type.Parts(o) is { Count: > 0 }) return null;
            var ctx = new RenderContext(SketchDocument.Create());
            foreach (var v in type.Views)
                if (v.Id != type.ViewOf(o) && type.Parts(registry.WithView(o, v.Id, ctx)) is { Count: > 0 }) return v.Id;
            return null;
        }

        /// <summary>The parts of assembly <paramref name="id"/> as new objects (not added to the document).</summary>
        public static IReadOnlyList<SketchObject> Explode(Registry registry, SketchDocument doc, string id, Lang lang = Lang.En)
        {
            var original = doc.Get(id);
            if (original is not null && RecipeView(registry, original) is { } recipeView && registry.Type(original.Type) is { } t0)
            {
                // Exploded in the view that has a recipe, then the parts take the assembly's own view with the mechanism kept.
                var other = registry.WithView(original, recipeView, new RenderContext(doc, lang));
                var parts = Explode(registry, Edits.Update(doc, new[] { id }, _ => other), id, lang);
                if (parts.Count == 0) return parts;
                var tmp = SketchDocument.Create();
                tmp = Edits.Add(tmp, tmp.Pages[0].Id, parts.ToArray());
                var back = AssemblyViews.Switch(registry, tmp, parts.Select(x => x.Id).ToList(), t0.ViewOf(original), lang);
                // Centred where the assembly was.
                var box = Rect.FromPoints(parts.Select(x => back.Get(x.Id)!).Select(x => new Vec2(x.Transform.X, x.Transform.Y)));
                var shift = new Vec2(original.Transform.X - (box.X + box.W / 2), original.Transform.Y - (box.Y + box.H / 2));
                return parts.Select(x => back.Get(x.Id)!).Select(x => x with { Transform = x.Transform with { X = Math.Round(x.Transform.X + shift.X, 6), Y = Math.Round(x.Transform.Y + shift.Y, 6) } }).ToList();
            }
            var o = original;
            if (o is null || registry.Type(o.Type)?.Parts(o) is not { Count: > 0 } specs) return Array.Empty<SketchObject>();
            var m = o.Matrix();
            var list = new List<SketchObject>();
            foreach (var spec in specs)
            {
                var item = registry.Library.FirstOrDefault(i => i.TypeId == spec.TypeId && i.View == spec.View) ?? registry.Library.FirstOrDefault(i => i.TypeId == spec.TypeId);
                if (item is null || registry.Type(spec.TypeId) is not { } type) continue;
                var at = m.Apply(spec.Local);
                if (spec.Points is { Count: >= 2 } local)
                {
                    // Line part (link): page points; docks are set once all parts exist.
                    var line = registry.Create(item, at);
                    if (spec.Params is not null) foreach (var kv in spec.Params) line = line.WithParam(kv.Key, kv.Value);
                    list.Add(line with { Points = local.Select(m.Apply).Select(q => new Vec2(Math.Round(q.X, 6), Math.Round(q.Y, 6))).ToList(), Attach = null });
                    continue;
                }
                var part = registry.Create(item, at);
                if (spec.Size is { } sz) part = part with { Size = sz };
                if (spec.View is not null && part.View != spec.View) part = part with { View = spec.View };
                if (spec.Params is not null) foreach (var kv in spec.Params) part = part.WithParam(kv.Key, kv.Value);
                part = part with { Transform = part.Transform with { Rotation = Math.Round(o.Transform.Rotation + spec.Rotation, 6), FlipX = o.Transform.FlipX, FlipY = o.Transform.FlipY } };
                var anchors = AnchorsOf(registry, doc, part, lang);
                double? have = null, want = null;
                if (spec.Pitch is double p) { have = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch)?.Radius; want = p; }
                else if (spec.InnerPitch is double q) { have = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.PitchInner)?.Radius; want = q; }
                else if (spec.PinRadius is double r)
                {
                    var hub = anchors.FirstOrDefault(a => a.Role == AnchorRole.Hub && a.Id == "hub")?.P ?? at;
                    var pin = anchors.FirstOrDefault(a => a.Id is { } pid && pid.StartsWith("pin", StringComparison.Ordinal));
                    if (pin is not null) { have = (pin.Kind == AnchorKind.Point ? pin.P : Vec2.Lerp(pin.P, pin.Q, 0.5)).DistanceTo(hub); want = r; }
                }
                // Sizes in the assembly's local mm are page mm (the assembly box is not scaled further).
                if (have is double h && want is double w && h > 1e-9) part = type.ScalePitch(part, w / h);
                // The part's hub (or centre, or the named anchor) goes to the given point.
                var now = AnchorsOf(registry, doc, part, lang);
                var hubNow = (spec.AlignAnchor is { } align ? now.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Id == align && a.Role != AnchorRole.Center)?.P : null)
                    ?? now.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && (a.Id == "hub" || a.Id is null))?.P
                    ?? new Vec2(part.Transform.X, part.Transform.Y);
                var d = at - hubNow;
                part = part with { Transform = part.Transform with { X = Math.Round(part.Transform.X + d.X, 6), Y = Math.Round(part.Transform.Y + d.Y, 6) } };
                list.Add(part);
            }
            // Docks of line parts onto the other parts (pivot points, joints of links).
            if (specs.Any(sp => sp.Docks is not null) && list.Count == specs.Count)
            {
                var all = SketchDocument.Create();
                all = Edits.Add(all, all.Pages[0].Id, list.ToArray());
                var ctx = new RenderContext(all, lang);
                for (var i = 0; i < specs.Count; i++)
                {
                    if (specs[i].Docks is not { } docks || list[i].Points is not { } pts) continue;
                    var attach = new Attachment?[pts.Count];
                    var points = pts.ToList();
                    for (var k = 0; k < pts.Count && k < docks.Count; k++)
                    {
                        if (docks[k] is not { } d || d.Part < 0 || d.Part >= list.Count) continue;
                        var target = list[d.Part];
                        var anchors = registry.PageAnchors(target, ctx).Where(a => a.Id == d.AnchorId).ToList();
                        var anchor = anchors.Where(a => a.Kind == AnchorKind.Point).OrderBy(a => a.Role == AnchorRole.Center ? 1 : 0).FirstOrDefault();
                        if (anchor is null)
                        {
                            // A rail (slot of a link, line): the point stays and docks at its place along the rail.
                            var rail = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail);
                            if (rail is null) continue;
                            var on = Geo.ClosestOnSegment(points[k], rail.P, rail.Q).Point;
                            points[k] = on;
                            attach[k] = Attachments.For(target, on, rail);
                            continue;
                        }
                        points[k] = anchor.P;
                        attach[k] = Attachments.For(target, anchor.P, anchor);
                    }
                    list[i] = list[i] with { Points = points, Attach = attach.Any(a => a is not null) ? attach : null };
                    all = Edits.Update(all, new[] { list[i].Id }, _ => list[i]);
                    ctx = new RenderContext(all, lang);
                }
            }
            return list;
        }

        private static List<Anchor> AnchorsOf(Registry registry, SketchDocument doc, SketchObject o, Lang lang)
        {
            var tmp = Edits.Add(doc, doc.Pages[0].Id, o);
            return registry.PageAnchors(tmp.Get(o.Id)!, new RenderContext(tmp, lang)).ToList();
        }
    }
}
