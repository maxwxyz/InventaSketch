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
            => PruneReleased(registry, AlignTeeth(registry, before, PropagateCore(registry, before, after, lang), lang), lang);

        private static SketchDocument PropagateCore(Registry registry, SketchDocument before, SketchDocument after, Lang lang)
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

            // Relations before the edit (pairs the user released do not couple).
            var (mesh, carried, carrier, bonded) = Relations(f0, (a, b) => IsReleased(before, a, b));
            if (mesh.Count == 0 && carried.Count == 0 && bonded.Count == 0) return after;
            return Apply(registry, before, after, lang, changed, f0, mesh, carried, carrier, bonded);
        }

        /// <summary>True if the user released the pair (<see cref="SketchObject.Released"/> on either object).</summary>
        public static bool IsReleased(SketchDocument doc, string a, string b)
            => doc.Get(a)?.Released?.Contains(b) == true || doc.Get(b)?.Released?.Contains(a) == true;

        /// <summary>A geometric relation: mesh (A, B gears), carried (shaft A carries B) or bonded (concentric); At = contact point.</summary>
        private readonly struct Pair
        {
            public Pair(LinkKind kind, string a, string b, Vec2 at) { Kind = kind; A = a; B = b; At = at; }
            public LinkKind Kind { get; }
            public string A { get; }
            public string B { get; }
            public Vec2 At { get; }
        }

        private static List<Pair> Pairs(Dictionary<string, Features> f0)
        {
            var list = new List<Pair>();
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
                        if (Math.Abs(d - need) <= Math.Max(0.3, 0.02 * (ra + rb)) && d > Eps)
                        {
                            // Contact on the centre line: on a's pitch circle (b's when b is the ring gear around a).
                            var at = b.Inner && !a.Inner ? b.Centre + (a.Centre - b.Centre) * (rb / d) : a.Centre + (b.Centre - a.Centre) * (ra / d);
                            list.Add(new Pair(LinkKind.Mesh, ids[i], ids[j], at));
                        }
                    }
                    // a's hub on b's axis: b carries a.
                    var hub = a.Hubs.Where(h => b.Axes.Any(x => Geo.ClosestOnSegment(h, x.A, x.B).Point.DistanceTo(h) <= 0.3)).Select(h => (Vec2?)h).FirstOrDefault();
                    if (hub is not null) { list.Add(new Pair(LinkKind.Carried, ids[j], ids[i], hub.Value)); continue; }
                    // Concentric hubs bond a gear to its shaft or hub (not two gears that merely lie on top of each other).
                    if (j > i && (a.Pitch is null || b.Pitch is null)
                        && !b.Hubs.Any(h => a.Axes.Any(x => Geo.ClosestOnSegment(h, x.A, x.B).Point.DistanceTo(h) <= 0.3)))
                    {
                        var c = a.Hubs.Where(h => b.Hubs.Any(k => h.DistanceTo(k) <= 0.3)).Select(h => (Vec2?)h).FirstOrDefault();
                        if (c is not null) list.Add(new Pair(LinkKind.Bonded, ids[i], ids[j], c.Value));
                    }
                }
            return list;
        }

        private static (Dictionary<string, List<string>> Mesh, Dictionary<string, List<string>> Carried, Dictionary<string, List<string>> Carrier, Dictionary<string, List<string>> Bonded)
            Relations(Dictionary<string, Features> f0, Func<string, string, bool>? released = null)
        {
            var mesh = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var carried = new Dictionary<string, List<string>>(StringComparer.Ordinal); // shaft → hubs on it
            var carrier = new Dictionary<string, List<string>>(StringComparer.Ordinal); // hub → shafts it sits on
            var bonded = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Link(Dictionary<string, List<string>> map, string a, string b)
            {
                if (!map.TryGetValue(a, out var l)) map[a] = l = new List<string>();
                if (!l.Contains(b)) l.Add(b);
            }
            foreach (var p in Pairs(f0))
            {
                if (released?.Invoke(p.A, p.B) == true) continue;
                switch (p.Kind)
                {
                    case LinkKind.Mesh: Link(mesh, p.A, p.B); Link(mesh, p.B, p.A); break;
                    case LinkKind.Carried: Link(carried, p.A, p.B); Link(carrier, p.B, p.A); break;
                    default: Link(bonded, p.A, p.B); Link(bonded, p.B, p.A); break;
                }
            }
            return (mesh, carried, carrier, bonded);
        }

        /// <summary>Features of all box objects of a document that take part in coupling.</summary>
        private static Dictionary<string, Features> FeaturesOf(Registry registry, SketchDocument doc, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            var f0 = new Dictionary<string, Features>(StringComparer.Ordinal);
            foreach (var o in doc.Objects.Values.Where(o => !o.IsPointGeometry))
            {
                var f = Read(registry, o, ctx);
                if (f.Any) f0[o.Id] = f;
            }
            return f0;
        }

        /// <summary>
        /// Mechanical links between box objects (mesh, carried, bonded) with their contact point, released or not, where
        /// one side is in <paramref name="ids"/> (all links when null).
        /// </summary>
        internal static IEnumerable<Link> MechanicalLinks(Registry registry, SketchDocument doc, ISet<string>? ids, Lang lang)
        {
            var f0 = FeaturesOf(registry, doc, lang);
            if (ids is not null && !ids.Any(f0.ContainsKey)) yield break;
            foreach (var p in Pairs(f0))
                if (ids is null || ids.Contains(p.A) || ids.Contains(p.B))
                    yield return new Link(p.Kind, p.A, p.B, p.At, IsReleased(doc, p.A, p.B));
        }

        /// <summary>Drops released entries of pairs that no longer touch, so parts snapped together again are coupled again.</summary>
        public static SketchDocument PruneReleased(Registry registry, SketchDocument doc, Lang lang = Lang.En)
        {
            if (!doc.Objects.Values.Any(o => o.Released is { Count: > 0 })) return doc;
            var touching = new HashSet<(string, string)>(Pairs(FeaturesOf(registry, doc, lang)).SelectMany(p => new[] { (p.A, p.B), (p.B, p.A) }));
            var stale = doc.Objects.Values.Where(o => o.Released is { Count: > 0 } r && r.Any(x => !touching.Contains((o.Id, x)))).Select(o => o.Id).ToList();
            if (stale.Count == 0) return doc;
            return Edits.Update(doc, stale, o =>
            {
                var keep = o.Released!.Where(x => touching.Contains((o.Id, x))).ToList();
                return o with { Released = keep.Count > 0 ? keep : null };
            });
        }

        /// <summary>
        /// Objects that move rigidly along when <paramref name="ids"/> are moved (dragged): gears, bearings and hubs on a moved
        /// shaft (and what sits on those), concentric parts. Meshing partners are not included: a changed centre distance
        /// scales them after the move (<see cref="Propagate"/>). Hosts show these followers during a drag and let their
        /// anchors snap as well, so a gear on a dragged shaft meshes with a gear on another shaft.
        /// </summary>
        public static IReadOnlyList<string> RigidFollowers(Registry registry, SketchDocument doc, IEnumerable<string> ids, Lang lang = Lang.En)
        {
            var start = new HashSet<string>(ids, StringComparer.Ordinal);
            var f0 = FeaturesOf(registry, doc, lang);
            if (!start.Any(f0.ContainsKey)) return Array.Empty<string>();
            var (_, carried, _, bonded) = Relations(f0, (a, b) => IsReleased(doc, a, b));
            var result = new List<string>();
            var seen = new HashSet<string>(start, StringComparer.Ordinal);
            var queue = new Queue<string>(start);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var next = (carried.TryGetValue(id, out var c) ? c : new List<string>()).Concat(bonded.TryGetValue(id, out var b) ? b : new List<string>());
                foreach (var n in next)
                    if (seen.Add(n)) { result.Add(n); queue.Enqueue(n); }
            }
            return result;
        }

        private static SketchDocument Apply(Registry registry, SketchDocument before, SketchDocument after, Lang lang, List<string> changed,
            Dictionary<string, Features> f0, Dictionary<string, List<string>> mesh, Dictionary<string, List<string>> carried,
            Dictionary<string, List<string>> carrier, Dictionary<string, List<string>> bonded)
        {
            var doc = after;
            var visited = new HashSet<string>(changed, StringComparer.Ordinal);
            List<string> Of(Dictionary<string, List<string>> map, string id) => map.TryGetValue(id, out var l) ? l : new List<string>();
            Features Now(string id) => Read(registry, doc.Get(id)!, new RenderContext(doc, lang));

            void Translate(string id, Vec2 delta)
                => doc = Edits.Update(doc, new[] { id }, o => o with { Transform = o.Transform with { X = o.Transform.X + delta.X, Y = o.Transform.Y + delta.Y } });

            var work = new Queue<string>(changed.Where(f0.ContainsKey));

            // Everything connected to the start (except what is already handled) moves by the same offset and, when a
            // rotating shaft carries it, turns with it about the pivot. withMesh: a pushed gear takes its whole train
            // along; without it (parts carried by a moved shaft) the moved gears' meshes follow the centre-distance rule.
            void RigidMove(string start, Vec2 delta, Vec2? pivot = null, double degrees = 0, bool withMesh = true)
            {
                var turn = pivot is not null && Math.Abs(degrees) > Eps;
                if (delta.Length < Eps && !turn) return;
                var queue = new Queue<string>();
                queue.Enqueue(start);
                visited.Add(start);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    Translate(id, delta);
                    if (turn) doc = Edits.Rotate(doc, new[] { id }, pivot!.Value, degrees);
                    if (!withMesh && mesh.ContainsKey(id) && f0.ContainsKey(id)) work.Enqueue(id);
                    var next = withMesh ? Of(mesh, id).Concat(Of(carried, id)).Concat(Of(carrier, id)).Concat(Of(bonded, id))
                                        : Of(carried, id).Concat(Of(carrier, id)).Concat(Of(bonded, id));
                    foreach (var n in next)
                        if (visited.Add(n)) queue.Enqueue(n);
                }
            }

            void Scale(string id, double k)
                => doc = Edits.Update(doc, new[] { id }, o => registry.Type(o.Type)?.ScalePitch(o, k) ?? o with { Size = new Size2(o.Size.W * k, o.Size.H * k) });

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
                    var turned = after1.Transform.Rotation - before0.Transform.Rotation;
                    foreach (var child in Of(carried, id))
                    {
                        if (visited.Contains(child)) continue;
                        var hub = f0[child].Hubs.FirstOrDefault(h => f0[id].Axes.Any(x => Geo.ClosestOnSegment(h, x.A, x.B).Point.DistanceTo(h) <= 0.3));
                        var (u, v) = before0.ToBox(hub);
                        var target = after1.BoxPoint(u, v);
                        RigidMove(child, target - hub, target, turned, withMesh: false);
                    }
                }

                // Concentric parts move together.
                if (moved.Length > Eps)
                    foreach (var other in Of(bonded, id))
                        if (!visited.Contains(other)) RigidMove(other, moved, withMesh: false);
            }
            return doc;
        }
    
        /// <summary>
        /// Turns gears that show teeth (<see cref="ObjectType.ToothPhase"/>) so teeth meet gaps where they mesh: every gear the
        /// edit added, moved, scaled or changed is turned to fit a meshing partner (preferably one the edit left alone);
        /// the partner keeps its angle. External meshes only.
        /// </summary>
        public static SketchDocument AlignTeeth(Registry registry, SketchDocument before, SketchDocument after, Lang lang = Lang.En)
        {
            var moved = after.Objects.Values.Where(o => !o.IsPointGeometry && (before.Get(o.Id) is not { } b || !SameState(b, o))).Select(o => o.Id).ToList();
            if (moved.Count == 0) return after;
            var gears = new Dictionary<string, (Features F, int Z, double Tooth)>(StringComparer.Ordinal);
            var ctx = new RenderContext(after, lang);
            foreach (var o in after.Objects.Values.Where(o => !o.IsPointGeometry))
            {
                if (registry.Type(o.Type)?.ToothPhase(o) is not { } tp || tp.Teeth < 3) continue;
                var f = Read(registry, o, ctx);
                if (f.Pitch is null || f.Inner) continue;
                gears[o.Id] = (f, tp.Teeth, tp.ToothDeg);
            }
            if (gears.Count < 2) return after;
            var doc = after;
            var done = new HashSet<string>(StringComparer.Ordinal);
            var movedSet = new HashSet<string>(moved, StringComparer.Ordinal);
            foreach (var id in moved)
            {
                if (!gears.TryGetValue(id, out var b)) continue;
                var partners = gears.Where(g => g.Key != id && !IsReleased(after, g.Key, id) && Math.Abs(g.Value.F.Centre.DistanceTo(b.F.Centre) - (g.Value.F.Pitch!.Value + b.F.Pitch!.Value)) <= Math.Max(0.3, 0.02 * (g.Value.F.Pitch!.Value + b.F.Pitch!.Value)))
                    .OrderBy(g => movedSet.Contains(g.Key) && !done.Contains(g.Key) ? 1 : 0).ToList();
                if (partners.Count == 0) continue;
                var aId = partners[0].Key;
                var a = partners[0].Value;
                var oa = doc.Get(aId)!;
                var ob = doc.Get(id)!;
                var d = b.F.Centre - a.F.Centre;
                var theta = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                double pa = 360.0 / a.Z, pb = 360.0 / b.Z;
                var u = Mod((theta - oa.Transform.Rotation - a.Tooth) / pa, 1);
                var target = theta + 180 + (u + 0.5) * pb - b.Tooth;
                var current = ob.Transform.Rotation;
                var step = Mod(target - current + pb / 2, pb) - pb / 2;
                if (Math.Abs(step) > 1e-6)
                    doc = Edits.Update(doc, new[] { id }, o => o with { Transform = o.Transform with { Rotation = Math.Round(o.Transform.Rotation + step, 6) } });
                done.Add(id);
            }
            return doc;
        }

        private static double Mod(double x, double m) => ((x % m) + m) % m;
}
}
