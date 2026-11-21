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
        public double? Pitch { get; init; }
        public double? InnerPitch { get; init; }
        /// <summary>Distance of the first pin from the hub (carriers).</summary>
        public double? PinRadius { get; init; }
        public double Rotation { get; init; }
        public IReadOnlyDictionary<string, ParamValue>? Params { get; init; }
    }

    /// <summary>
    /// Exploding a predefined assembly (planetary set, differential …) into its parts: each part becomes an element of its
    /// own, placed and sized so the parts are coupled as before (meshing gears, planets on the carrier pins, concentric
    /// hubs) and can be edited one by one.
    /// </summary>
    public static class Assemblies
    {
        public static bool CanExplode(Registry registry, SketchObject o) => !o.IsPointGeometry && registry.Type(o.Type)?.Parts(o) is { Count: > 0 };

        /// <summary>The parts of assembly <paramref name="id"/> as new objects (not added to the document).</summary>
        public static IReadOnlyList<SketchObject> Explode(Registry registry, SketchDocument doc, string id, Lang lang = Lang.En)
        {
            var o = doc.Get(id);
            if (o is null || registry.Type(o.Type)?.Parts(o) is not { Count: > 0 } specs) return Array.Empty<SketchObject>();
            var m = o.Matrix();
            var list = new List<SketchObject>();
            foreach (var spec in specs)
            {
                var item = registry.Library.FirstOrDefault(i => i.TypeId == spec.TypeId && i.View == spec.View) ?? registry.Library.FirstOrDefault(i => i.TypeId == spec.TypeId);
                if (item is null || registry.Type(spec.TypeId) is not { } type) continue;
                var at = m.Apply(spec.Local);
                var part = registry.Create(item, at);
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
                // The part's hub (or centre) goes to the given point.
                var hubNow = AnchorsOf(registry, doc, part, lang).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && (a.Id == "hub" || a.Id is null))?.P
                    ?? new Vec2(part.Transform.X, part.Transform.Y);
                var d = at - hubNow;
                part = part with { Transform = part.Transform with { X = Math.Round(part.Transform.X + d.X, 6), Y = Math.Round(part.Transform.Y + d.Y, 6) } };
                list.Add(part);
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
