using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Mechanical coupling: elements that belong together follow an edit, as in a real gearbox. The relations are read
    /// from the geometry before the edit (no stored links), from the anchors the types expose:
    /// <list type="bullet">
    /// <item><b>Mesh:</b> pitch circles (<see cref="AnchorRole.Pitch"/>, ring gears <see cref="AnchorRole.PitchInner"/>) that
    /// touch. Moving one gear towards or away from its partner changes the centre distance: both gears scale by the same
    /// factor and stay in mesh (the ratio is kept). A gear that grows or shrinks (resize, teeth) pushes its partners
    /// along the centre line, together with their shafts and the rest of their train.</item>
    /// <item><b>Carried:</b> a hub point (<see cref="AnchorRole.Hub"/>) on a shaft axis (<see cref="AnchorRole.Axis"/>):
    /// gears, bearings and hubs follow their shaft when it moves, rotates or stretches; moving the gear alone slides it.</item>
    /// <item><b>Bonded:</b> hub points that coincide (gear and shaft in axial view; not two gears lying on top of each
    /// other): they move together.</item>
    /// </list>
    /// Everything that moves along is translated as a whole, so meshes further down the train stay intact.
    /// </summary>
    public static class Coupling
    {
        private const double Eps = 1e-6;

        private sealed class Features
        {
            public Vec2 Centre;
            public double? Pitch;
            public bool Inner;
            public readonly List<Vec2> Hubs = new();
            public readonly List<(Vec2 A, Vec2 B)> Axes = new();
            public bool Any => Pitch is not null || Hubs.Count > 0 || Axes.Count > 0;
        }

        private static Features Read(Registry registry, SketchObject o, RenderContext ctx)
        {
            var f = new Features { Centre = new Vec2(o.Transform.X, o.Transform.Y) };
            if (o.IsPointGeometry || registry.Type(o.Type) is null) return f;
            foreach (var a in registry.PageAnchors(o, ctx))
            {
                if (a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner && f.Pitch is null)
                {
                    f.Pitch = a.Radius;
                    f.Inner = a.Role == AnchorRole.PitchInner;
                    f.Centre = a.P;
                }
                else if (a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub) f.Hubs.Add(a.P);
                else if (a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis) f.Axes.Add((a.P, a.Q));
            }
            return f;
        }

        /// <summary>Centre distance at which two pitch circles mesh (external pair, or a gear inside a ring gear).</summary>
        private static double MeshDistance(double ra, bool innerA, double rb, bool innerB)
            => innerA == innerB ? ra + rb : Math.Abs(ra - rb);

        private static bool SameState(SketchObject a, SketchObject b)
        {
            if (a.Transform != b.Transform || a.Size != b.Size || a.View != b.View || a.Params.Count != b.Params.Count) return false;
            foreach (var kv in a.Params)
                if (!b.Params.TryGetValue(kv.Key, out var v) || !v.Equals(kv.Value)) return false;
            return true;
        }

        /// <summary>
        /// Applies the coupling of an edit from <paramref name="before"/> to <paramref name="after"/> and returns the coupled
        /// document (the same instance if nothing follows). Objects changed by the edit are the starting points; objects they
        /// drag along are moved or scaled.
        /// </summary>
        public static SketchDocument Propagate(Registry registry, SketchDocument before, SketchDocument after, Lang lang = Lang.En)
        {
            var changed = after.Objects.Values
                .Where(o => !o.IsPointGeometry && before.Get(o.Id) is { } b && !SameState(b, o))
                .Select(o => o.Id).ToList();
            if (changed.Count == 0) return after;

            var ctx0 = new RenderContext(before, lang);
            var f0 = new Dictionary<string, Features>(StringComparer.Ordinal);
            foreach (var o in before.Objects.Values.Where(o => !o.IsPointGeometry && after.Get(o.Id) is not null))
            {
                var f = Read(registry, o, ctx0);
                if (f.Any) f0[o.Id] = f;
            }
            if (!changed.Any(f0.ContainsKey)) return after;

            // Relations before the edit.
            var mesh = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var carried = new Dictionary<string, List<string>>(StringComparer.Ordinal); // shaft → hubs on it
            var carrier = new Dictionary<string, List<string>>(StringComparer.Ordinal); // hub → shafts it sits on
            var bonded = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Link(Dictionary<string, List<string>> map, string a, string b)
            {
                if (!map.TryGetValue(a, out var l)) map[a] = l = new List<string>();
                if (!l.Contains(b)) l.Add(b);
            }
            var ids = f0.Keys.ToList();
            for (var i = 0; i < ids.Count; i++)
                for (var j = 0; j < ids.Count; j++)
                {
                    if (i == j) continue;
                    var a = f0[ids[i]];
                    var b = f0[ids[j]];
                    if (j > i && a.Pitch is double ra && b.Pitch is double rb)
                    {
                        var d = a.Centre.DistanceTo(b.Centre);
                        var need = MeshDistance(ra, a.Inner, rb, b.Inner);
                        if (Math.Abs(d - need) <= Math.Max(0.3, 0.02 * (ra + rb)) && d > Eps) { Link(mesh, ids[i], ids[j]); Link(mesh, ids[j], ids[i]); }
                    }
                    // a's hub on b's axis: b carries a.
                    var onAxis = a.Hubs.Any(h => b.Axes.Any(x => Geo.ClosestOnSegment(h, x.A, x.B).Point.DistanceTo(h) <= 0.3));
                    if (onAxis) { Link(carried, ids[j], ids[i]); Link(carrier, ids[i], ids[j]); continue; }
                    // Concentric hubs bond a gear to its shaft or hub (not two gears that merely lie on top of each other).
                    if (j > i && (a.Pitch is null || b.Pitch is null)
                        && !b.Hubs.Any(h => a.Axes.Any(x => Geo.ClosestOnSegment(h, x.A, x.B).Point.DistanceTo(h) <= 0.3))
                        && a.Hubs.Any(h => b.Hubs.Any(k => h.DistanceTo(k) <= 0.3)))
                    {
                        Link(bonded, ids[i], ids[j]);
                        Link(bonded, ids[j], ids[i]);
                    }
                }
            if (mesh.Count == 0 && carried.Count == 0 && bonded.Count == 0) return after;

            var doc = after;
            var visited = new HashSet<string>(changed, StringComparer.Ordinal);
            List<string> Of(Dictionary<string, List<string>> map, string id) => map.TryGetValue(id, out var l) ? l : new List<string>();
            Features Now(string id) => Read(registry, doc.Get(id)!, new RenderContext(doc, lang));

            void Translate(string id, Vec2 delta)
                => doc = Edits.Update(doc, new[] { id }, o => o with { Transform = o.Transform with { X = o.Transform.X + delta.X, Y = o.Transform.Y + delta.Y } });

            // Everything connected to the start (except what is already handled) moves by the same offset.
            void RigidMove(string start, Vec2 delta)
            {
                if (delta.Length < Eps) return;
                var queue = new Queue<string>();
                queue.Enqueue(start);
                visited.Add(start);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    Translate(id, delta);
                    foreach (var next in Of(mesh, id).Concat(Of(carried, id)).Concat(Of(carrier, id)).Concat(Of(bonded, id)))
                        if (visited.Add(next)) queue.Enqueue(next);
                }
            }

            void Scale(string id, double k)
                => doc = Edits.Update(doc, new[] { id }, o => o with { Size = new Size2(o.Size.W * k, o.Size.H * k) });

            var work = new Queue<string>(changed.Where(f0.ContainsKey));
            while (work.Count > 0)
            {
                var id = work.Dequeue();
                var a0 = f0[id];
                var a1 = Now(id);
                var moved = a1.Centre - a0.Centre;

                foreach (var partner in Of(mesh, id))
                {
                    if (visited.Contains(partner) || a0.Pitch is not double r0 || a1.Pitch is not double r1) continue;
                    var b = Now(partner);
                    if (b.Pitch is not double rb) continue;
                    if (Math.Abs(r1 - r0) > Eps)
                    {
                        // Grown or shrunk: the partner (with its train) moves along the centre line to stay in mesh.
                        var dir = b.Centre - a1.Centre;
                        if (dir.Length < Eps) dir = b.Centre - a0.Centre;
                        if (dir.Length < Eps) continue;
                        var target = a1.Centre + dir * (MeshDistance(r1, a1.Inner, rb, b.Inner) / dir.Length);
                        RigidMove(partner, target - b.Centre);
                    }
                    else if (moved.Length > Eps)
                    {
                        // Centre distance changed: both gears scale by the same factor (ratio kept, partner stays put).
                        var need = MeshDistance(r0, a0.Inner, rb, b.Inner);
                        var k = need > Eps ? a1.Centre.DistanceTo(b.Centre) / need : 1;
                        if (Math.Abs(k - 1) < 1e-4 || k < 0.2 || k > 5) continue;
                        Scale(id, k);
                        Scale(partner, k);
                        visited.Add(partner);
                        // The partner grew: its other partners move to stay in mesh, its shaft keeps its place.
                        f0[partner] = b;
                        work.Enqueue(partner);
                        // So did this gear: its remaining partners follow as for a resize (one scaling per edit).
                        f0[id] = a1;
                        work.Enqueue(id);
                        break;
                    }
                }

                // A shaft takes the gears, bearings and hubs on its axis along (move, rotate, stretch).
                if (carried.ContainsKey(id))
                {
                    var before0 = before.Get(id)!;
                    var after1 = doc.Get(id)!;
                    foreach (var child in Of(carried, id))
                    {
                        if (visited.Contains(child)) continue;
                        var hub = f0[child].Hubs.FirstOrDefault(h => f0[id].Axes.Any(x => Geo.ClosestOnSegment(h, x.A, x.B).Point.DistanceTo(h) <= 0.3));
                        var (u, v) = before0.ToBox(hub);
                        RigidMove(child, after1.BoxPoint(u, v) - hub);
                    }
                }

                // Concentric parts move together.
                if (moved.Length > Eps)
                    foreach (var other in Of(bonded, id))
                        if (!visited.Contains(other)) RigidMove(other, moved);
            }
            return doc;
        }
    }
}
