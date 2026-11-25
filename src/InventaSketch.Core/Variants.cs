using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Variants and states (before/after, baseline vs. invention, operating modes): two states of the same drawing – a slide
    /// and its duplicate, which keep the elements' ids – compared element by element. An element is added (only in the new
    /// state), removed (only in the old one) or changed (type, view, parameters, text, position, size or points differ).
    /// </summary>
    public static class Variants
    {
        /// <summary>The difference from <paramref name="before"/> to <paramref name="after"/>.</summary>
        public sealed record Difference(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed)
        {
            public bool None => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
        }

        /// <summary>Compares two states of a drawing by element id.</summary>
        public static Difference Diff(SketchDocument before, SketchDocument after)
        {
            var added = after.Objects.Keys.Where(id => before.Get(id) is null).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var removed = before.Objects.Keys.Where(id => after.Get(id) is null).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var changed = after.Objects.Values.Where(o => before.Get(o.Id) is { } b && !Same(b, o, before, after)).Select(o => o.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            return new Difference(added, removed, changed);
        }

        /// <summary>Page bounds of an element (points resolved for lines).</summary>
        public static Rect Bounds(SketchDocument doc, SketchObject o) => o.IsPointGeometry ? Rect.FromPoints(doc.ResolvePoints(o)) : o.Bounds();

        private static bool Same(SketchObject a, SketchObject b, SketchDocument da, SketchDocument db)
        {
            if (a.Type != b.Type || a.View != b.View || a.Text != b.Text || a.Size != b.Size) return false;
            if (Math.Abs(a.Transform.X - b.Transform.X) > 1e-3 || Math.Abs(a.Transform.Y - b.Transform.Y) > 1e-3 || Math.Abs(a.Transform.Rotation - b.Transform.Rotation) > 1e-3
                || a.Transform.FlipX != b.Transform.FlipX || a.Transform.FlipY != b.Transform.FlipY) return false;
            if (a.Params.Count != b.Params.Count || a.Params.Any(kv => !b.Params.TryGetValue(kv.Key, out var v) || !v.Equals(kv.Value))) return false;
            if (a.IsPointGeometry)
            {
                var pa = da.ResolvePoints(a); var pb = db.ResolvePoints(b);
                if (pa.Count != pb.Count || pa.Zip(pb, (x, y) => x.DistanceTo(y)).Any(d => d > 1e-3)) return false;
            }
            return true;
        }
    }
}
