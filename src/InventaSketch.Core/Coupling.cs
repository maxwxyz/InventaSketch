using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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
    /// <item><b>Seated:</b> a part with a bore that comes onto a shaft (dragged onto its axis, or the shaft through its hub)
    /// gets the shaft's diameter (<see cref="ShaftFit"/>).</item>
    /// <item><b>Bonded:</b> hub points that coincide (gear and shaft in axial view; not two gears lying on top of each
    /// other): they move together.</item>
    /// </list>
    /// Everything that moves along is translated as a whole, so meshes further down the train stay intact.
    /// </summary>
    /// <summary>
    /// A belt or chain that runs over more than two hubs (a serpentine drive): every <see cref="AnchorRole.Hub"/> anchor
    /// carries a pulley, and when pulleys move the strand is routed through the hubs' new places.
    /// </summary>
    public interface ISpanRoute
    {
        /// <summary>True if <paramref name="o"/> is currently routed over its hub list (else the two-end-hub rule applies).</summary>
        bool IsRouted(SketchObject o);

        /// <summary>
        /// The strand routed through <paramref name="pageHubs"/> (page mm, in the order of its hub anchors; radii unchanged):
        /// box, position and route adapt so every hub stays inside.
        /// </summary>
        SketchObject RouteThrough(SketchObject o, IReadOnlyList<Vec2> pageHubs, RenderContext ctx);
    }

    /// <summary>
    /// Optional for types with a <see cref="AnchorRole.Pitch"/> circle that is no mesh: a belt pulley or chain sprocket whose
    /// pitch circle only sizes the belt or chain does not mesh with a pitch circle it touches (<see cref="Coupling"/>,
    /// <see cref="Coupling.AlignTeeth"/>). Types without this interface mesh (gears, friction wheels).
    /// </summary>
    public interface IPitchMesh
    {
        /// <summary>True if the pitch circle of <paramref name="o"/> meshes with a touching pitch circle.</summary>
        bool MeshesAtPitch(SketchObject o);
    }

    public static class Coupling
    {
        private const double Eps = 1e-6;

        private sealed class Features
        {
            public Vec2 Centre;
            public double? Pitch;
            public bool Inner;
            /// <summary>Axis direction of a gear seen from the side (axis in the drawing plane), null in axial view.</summary>
            public Vec2? Axis;
            /// <summary><see cref="ObjectType.DrivesShaft"/>.</summary>
            public bool Drives;
            /// <summary><see cref="ObjectType.SpansHubs"/>.</summary>
            public bool Span;
            /// <summary>The pitch circle meshes (not a pulley or sprocket, see <see cref="IPitchMesh"/>).</summary>
            public bool Meshes = true;
            /// <summary>A strand routed over all its hubs (<see cref="ISpanRoute"/>), not only the first two.</summary>
            public bool Routed;
            public readonly List<Vec2> Hubs = new();
            public readonly List<(Vec2 A, Vec2 B)> Axes = new();
            /// <summary>Cone apex of a bevel gear seen from the side (<see cref="AnchorRole.Apex"/>), else null.</summary>
            public Vec2? Apex;
            /// <summary>Pitch line of a rack (<see cref="AnchorRole.PitchLine"/>) with the unit normal to its teeth, else null.</summary>
            public (Vec2 A, Vec2 B, Vec2 N)? Rack;
            public bool Any => Pitch is not null || Hubs.Count > 0 || Axes.Count > 0 || Apex is not null || Rack is not null;
        }

        private static Features Read(Registry registry, SketchObject o, RenderContext ctx)
        {
            var f = new Features { Centre = new Vec2(o.Transform.X, o.Transform.Y) };
            if (o.IsPointGeometry || registry.Type(o.Type) is not { } type) return f;
            f.Drives = type.DrivesShaft;
            f.Span = type.SpansHubs;
            f.Meshes = type is not IPitchMesh pm || pm.MeshesAtPitch(o);
            f.Routed = type is ISpanRoute route && route.IsRouted(o);
            foreach (var a in registry.PageAnchors(o, ctx))
            {
                if (a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner && f.Pitch is null)
                {
                    f.Pitch = a.Radius;
                    f.Inner = a.Role == AnchorRole.PitchInner;
                    f.Centre = a.P;
                    f.Axis = a.AxisOf();
                }
                else if (a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub) f.Hubs.Add(a.P);
                else if (a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex && f.Apex is null) f.Apex = a.P;
                else if (a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis) f.Axes.Add((a.P, a.Q));
                else if (a.TeethSide() is { } n && f.Rack is null) f.Rack = (a.P, a.Q, n);
            }
            return f;
        }

        /// <summary>
        /// Gears mesh only within the same kind of view: both axial, or both seen from the side with parallel axes and the
        /// partner across the axis (one above the other), never an axial gear with a side view.
        /// </summary>
        private static bool SameMeshKind(Features a, Features b)
        {
            if (a.Axis is null && b.Axis is null) return true;
            if (a.Axis is not { } ax || b.Axis is not { } bx) return false;
            if (Math.Abs(ax.Cross(bx)) > 0.05) return false;
            var d = b.Centre - a.Centre;
            return d.Length < Eps || Math.Abs(d.Dot(ax)) <= 0.2 * d.Length;
        }

        /// <summary>Centre distance at which two pitch circles mesh (external pair, or a gear inside a ring gear).</summary>
        private static double MeshDistance(double ra, bool innerA, double rb, bool innerB)
            => innerA == innerB ? ra + rb : Math.Abs(ra - rb);

        internal static bool SameState(SketchObject a, SketchObject b)
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
            => PruneReleased(registry, AlignTeeth(registry, before, FollowMounts(registry, before, ShaftFit.Seat(registry, before, PropagateCore(registry, before, after, lang), lang), lang), lang), lang);

        /// <summary>
        /// Parts docked on a directed mount (<see cref="Anchor.Directed"/>: a tool on a robot flange) follow it: when the
        /// element carrying the mount changes (a joint turns, it moves), every part whose own directed mount sat on it facing
        /// it is turned by the change of the mount's direction and moved onto it again; parts docked on those follow in turn.
        /// </summary>
        private static SketchDocument FollowMounts(Registry registry, SketchDocument before, SketchDocument after, Lang lang)
        {
            var changed = after.Objects.Values.Where(o => !o.IsPointGeometry && before.Get(o.Id) is { } b && !SameState(b, o)).Select(o => o.Id).ToList();
            if (changed.Count == 0) return after;
            var ctx0 = new RenderContext(before, lang).For(registry);
            List<Anchor> Directed(SketchDocument d, SketchObject o, RenderContext c)
                => registry.Type(o.Type) is null ? new List<Anchor>()
                    : registry.PageAnchors(o, c).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Mount && a.Direction is not null).ToList();
            var others = before.Objects.Values.Where(o => !o.IsPointGeometry && after.Get(o.Id) is not null)
                .Select(o => (Obj: o, Mounts: Directed(before, o, ctx0))).Where(x => x.Mounts.Count > 0).ToList();
            if (others.Count < 2) return after;
            var doc = after;
            var done = new HashSet<string>(changed, StringComparer.Ordinal);
            var queue = new Queue<string>(changed);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var b0 = before.Get(id);
                var b1 = doc.Get(id);
                if (b0 is null || b1 is null) continue;
                var old = Directed(before, b0, ctx0);
                if (old.Count == 0) continue;
                var now = Directed(doc, b1, new RenderContext(doc, lang).For(registry));
                foreach (var (other, mounts) in others)
                {
                    if (done.Contains(other.Id)) continue;
                    foreach (var m in mounts)
                    {
                        // Docked: its mount on one of ours, facing it.
                        var host = old.FirstOrDefault(h => h.P.DistanceTo(m.P) <= 0.3 && h.Direction!.Value.Dot(m.Direction!.Value) < -0.99);
                        if (host is null || now.FirstOrDefault(h => h.Id == host.Id) is not { } moved) continue;
                        var d0 = host.Direction!.Value; var d1 = moved.Direction!.Value;
                        var turn = (Math.Atan2(d1.Y, d1.X) - Math.Atan2(d0.Y, d0.X)) * 180 / Math.PI;
                        var o = doc.Get(other.Id)!;
                        var turned = Math.Abs(turn) < 1e-9 ? o : o with { Transform = o.Transform with { Rotation = Math.Round(((o.Transform.Rotation + turn) % 360 + 540) % 360 - 180, 6) } };
                        var tm = registry.PageAnchors(turned, new RenderContext(doc, lang).For(registry)).First(a => a.Role == AnchorRole.Mount && a.Id == m.Id);
                        var shift = moved.P - tm.P;
                        var placed = turned with { Transform = turned.Transform with { X = Math.Round(turned.Transform.X + shift.X, 6), Y = Math.Round(turned.Transform.Y + shift.Y, 6) } };
                        if (!SameState(o, placed)) doc = Edits.Replace(doc, placed);
                        done.Add(other.Id);
                        queue.Enqueue(other.Id);
                        break;
                    }
                }
            }
            return doc;
        }

        private static SketchDocument PropagateCore(Registry registry, SketchDocument before, SketchDocument after, Lang lang)
        {
            var changed = after.Objects.Values
                .Where(o => !o.IsPointGeometry && before.Get(o.Id) is { } b && !SameState(b, o))
                .Select(o => o.Id).ToList();
            if (changed.Count == 0) return after;

            // Turning a part seen along its axis (gear, carrier, pulley in axial view) about its centre drives its train.
            if (changed.Count == 1 && before.Get(changed[0]) is { } t0 && after.Get(changed[0]) is { } t1 && t0.Size == t1.Size
                && t0.Transform with { Rotation = 0 } == t1.Transform with { Rotation = 0 } && Math.Abs(t1.Transform.Rotation - t0.Transform.Rotation) > Eps
                && GearTrain.Spins(registry, t1))
            {
                var turned = GearTrain.Turn(registry, before, t1.Id, t1.Transform.Rotation - t0.Transform.Rotation, lang);
                // Objects only in the new state (none for a pure turn) stay as they are.
                foreach (var o in after.Objects.Values.Where(o => turned.Get(o.Id) is null))
                    turned = Edits.Add(turned, turned.Pages[0].Id, o);
                return Edits.Update(turned, new[] { t1.Id }, o => o with { Transform = t1.Transform });
            }

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
            var spans = Pairs(f0).Where(p => p.Kind == LinkKind.Spanned && !IsReleased(before, p.A, p.B)).ToList();
            if (mesh.Count == 0 && carried.Count == 0 && bonded.Count == 0 && spans.Count == 0) return after;
            var doc = Apply(registry, before, after, lang, changed, f0, mesh, carried, carrier, bonded);
            return spans.Count == 0 ? doc : FollowSpans(registry, before, doc, lang, f0, spans, changed);
        }

        /// <summary>
        /// The coupling relations of a document's box objects (mesh, carried, bonded, spanned) without released pairs, as
        /// (kind, a, b): for a carried pair a carries b (shaft, gear).
        /// </summary>
        /// <remarks>
        /// Cached per document instance, registry and language (documents are immutable). Re-entrancy safe: reading the
        /// anchors of every object can solve values again (a linked characteristic chart solves its pump, whose shaft links ask
        /// for this graph); such a nested request for a document whose graph is being built gets an empty graph (not cached)
        /// instead of recursing without end. Callers that cache results derived from the graph check <see cref="GraphPending"/>.
        /// </remarks>
        public static IReadOnlyList<(LinkKind Kind, string A, string B)> Graph(Registry registry, SketchDocument doc, Lang lang = Lang.En)
        {
            var slot = Graphs.GetValue(doc, _ => new ConditionalWeakTable<Registry, Dictionary<Lang, IReadOnlyList<(LinkKind, string, string)>>>())
                .GetValue(registry, _ => new Dictionary<Lang, IReadOnlyList<(LinkKind, string, string)>>());
            lock (slot)
                if (slot.TryGetValue(lang, out var cached)) return cached;
            if (GraphPending(doc)) return Array.Empty<(LinkKind, string, string)>();
            var building = _building ??= new List<SketchDocument>();
            building.Add(doc);
            IReadOnlyList<(LinkKind, string, string)> graph;
            try
            {
                graph = Pairs(FeaturesOf(registry, doc, lang)).Where(p => !IsReleased(doc, p.A, p.B)).Select(p => (p.Kind, p.A, p.B)).ToList().AsReadOnly();
            }
            finally { building.RemoveAt(building.Count - 1); }
            lock (slot) slot[lang] = graph;
            return graph;
        }

        private static readonly ConditionalWeakTable<SketchDocument, ConditionalWeakTable<Registry, Dictionary<Lang, IReadOnlyList<(LinkKind, string, string)>>>> Graphs = new();

        /// <summary>Documents whose <see cref="Graph"/> is being built on this thread (innermost last).</summary>
        [ThreadStatic] private static List<SketchDocument>? _building;

        /// <summary>
        /// True while <see cref="Graph"/> of <paramref name="doc"/> is being built on this thread: a nested request gets an empty
        /// graph, so results derived from it must not be cached.
        /// </summary>
        public static bool GraphPending(SketchDocument doc)
        {
            var building = _building;
            if (building is null) return false;
            foreach (var d in building) if (ReferenceEquals(d, doc)) return true;
            return false;
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
                    // A belt or chain runs over the parts whose hubs lie on its end hubs (no other relation).
                    if (a.Span || b.Span)
                    {
                        if (a.Span && !b.Span)
                        {
                            var end = (a.Routed ? a.Hubs : a.Hubs.Take(2)).Where(h => b.Hubs.Any(k => h.DistanceTo(k) <= 0.3)).Select(h => (Vec2?)h).FirstOrDefault();
                            if (end is not null) list.Add(new Pair(LinkKind.Spanned, ids[i], ids[j], end.Value));
                        }
                        continue;
                    }
                    if (j > i && a.Pitch is double ra && b.Pitch is double rb && a.Meshes && b.Meshes && SameMeshKind(a, b))
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
                    // Rack and pinion: a gear seen along its axis whose pitch circle touches the rack's pitch line on the teeth side
                    // (the gear centre within the rack's length) meshes with the rack: A gear, B rack, contact on the line.
                    if (a.Pitch is double rp && !a.Inner && a.Meshes && a.Axis is null && b.Rack is { } rack && RackContact(a.Centre, rp, rack) is { } touch)
                        list.Add(new Pair(LinkKind.Mesh, ids[i], ids[j], touch));
                    // Bevel gears mesh where their cone apexes meet and their axes cross (angle drive, differential).
                    if (j > i && a.Pitch is null && b.Pitch is null && a.Apex is Vec2 pa && b.Apex is Vec2 pb && pa.DistanceTo(pb) <= 0.3)
                    {
                        var ua = pa - a.Centre; var ub = pb - b.Centre;
                        if (ua.Length > Eps && ub.Length > Eps && Math.Abs(ua.Normalized().Cross(ub.Normalized())) > 0.5)
                        {
                            list.Add(new Pair(LinkKind.Mesh, ids[i], ids[j], pa));
                            continue;
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

        /// <summary>
        /// The contact point of a pitch circle (centre <paramref name="c"/>, radius <paramref name="r"/>) with a rack pitch line
        /// when the circle touches the line on its teeth side (within 0.3 mm or 3 % of the radius) and its centre lies within
        /// the rack's length; else null.
        /// </summary>
        internal static Vec2? RackContact(Vec2 c, double r, (Vec2 A, Vec2 B, Vec2 N) rack)
        {
            var len = rack.A.DistanceTo(rack.B);
            if (len < Eps || r < Eps) return null;
            var u = (rack.B - rack.A) * (1 / len);
            var d = c - rack.A;
            var t = d.Dot(u);
            if (t < 0 || t > len || Math.Abs(d.Dot(rack.N) - r) > Math.Max(0.3, 0.03 * r)) return null;
            return rack.A + u * t;
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
                    case LinkKind.Spanned: break; // see Spans
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

        /// <summary>
        /// Belts and chains follow the pulleys and sprockets they run over: each end goes to where its part's hub is now, so the
        /// strand stretches and turns; a strand the edit moved itself is left alone.
        /// </summary>
        private static SketchDocument FollowSpans(Registry registry, SketchDocument before, SketchDocument doc, Lang lang, Dictionary<string, Features> f0, List<Pair> spans, List<string> changed)
        {
            var ctx = new RenderContext(doc, lang);
            foreach (var group in spans.GroupBy(p => p.A))
            {
                var id = group.Key;
                if (changed.Contains(id) || !f0.TryGetValue(id, out var fs) || fs.Hubs.Count < 2) continue;
                if (fs.Routed && doc.Get(id) is { } strand && registry.Type(strand.Type) is ISpanRoute route)
                {
                    // A serpentine strand: every hub goes to where its part's hub is now; the module re-routes.
                    var hubs = fs.Hubs.ToList();
                    var moved = false;
                    foreach (var p in group)
                    {
                        if (doc.Get(p.B) is not { } part || !f0.TryGetValue(p.B, out var fb)) continue;
                        var k = fb.Hubs.FindIndex(h => h.DistanceTo(p.At) <= 0.3);
                        var now = Read(registry, part, ctx);
                        if (k < 0 || k >= now.Hubs.Count) continue;
                        var i = hubs.FindIndex(h => h.DistanceTo(p.At) <= 0.3);
                        if (i < 0 || now.Hubs[k].DistanceTo(hubs[i]) < Eps) continue;
                        hubs[i] = now.Hubs[k];
                        moved = true;
                    }
                    if (moved)
                    {
                        var routed = route.RouteThrough(strand, hubs, ctx);
                        if (!ReferenceEquals(routed, strand)) doc = Edits.Update(doc, new[] { id }, _ => routed);
                    }
                    continue;
                }
                Vec2 l0 = fs.Hubs[0], r0 = fs.Hubs[1], l1 = l0, r1 = r0;
                foreach (var p in group)
                {
                    if (doc.Get(p.B) is not { } part || !f0.TryGetValue(p.B, out var fb)) continue;
                    var k = fb.Hubs.FindIndex(h => h.DistanceTo(p.At) <= 0.3);
                    var now = Read(registry, part, ctx);
                    if (k < 0 || k >= now.Hubs.Count) continue;
                    if (p.At.DistanceTo(l0) <= 0.3) l1 = now.Hubs[k];
                    else if (p.At.DistanceTo(r0) <= 0.3) r1 = now.Hubs[k];
                }
                if (l1.DistanceTo(l0) < Eps && r1.DistanceTo(r0) < Eps) continue;
                double d0 = l0.DistanceTo(r0), d1 = l1.DistanceTo(r1);
                if (d1 < 1) continue;
                var o = doc.Get(id)!;
                var (u, v) = o.ToBox(Vec2.Lerp(l0, r0, 0.5));
                var mid = new Vec2(u * o.Size.W, v * o.Size.H);
                var turn = (Math.Atan2(r1.Y - l1.Y, r1.X - l1.X) - Math.Atan2(r0.Y - l0.Y, r0.X - l0.X)) * 180 / Math.PI;
                var rot = o.Transform.Rotation + turn;
                var w = Math.Max(1, o.Size.W + d1 - d0);
                var offset = (o with { Transform = new Transform(0, 0, rot, o.Transform.FlipX, o.Transform.FlipY) }).Matrix().ApplyLinear(mid);
                var centre = Vec2.Lerp(l1, r1, 0.5) - offset;
                doc = Edits.Update(doc, new[] { id }, x => x with
                {
                    Transform = x.Transform with { X = Math.Round(centre.X, 6), Y = Math.Round(centre.Y, 6), Rotation = Math.Round(rot, 6) },
                    Size = new Size2(Math.Round(w, 6), x.Size.H),
                });
            }
            return doc;
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
            var (_, carried, carrier, bonded) = Relations(f0, (a, b) => IsReleased(doc, a, b));
            var result = new List<string>();
            var seen = new HashSet<string>(start, StringComparer.Ordinal);
            var queue = new Queue<string>(start);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var next = (carried.TryGetValue(id, out var c) ? c : new List<string>()).Concat(bonded.TryGetValue(id, out var b) ? b : new List<string>());
                // A dragged motor or engine takes its shaft (and what sits on it) along.
                if (start.Contains(id) && f0.TryGetValue(id, out var fd) && fd.Drives && carrier.TryGetValue(id, out var shafts)) next = next.Concat(shafts);
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
                    if (b.Pitch is null && b.Rack is { } rack)
                    {
                        // A grown or shrunk pinion keeps touching its rack: the rack moves along its normal (the pinion stays).
                        if (Math.Abs(r1 - r0) > Eps)
                        {
                            var gap = (a1.Centre - rack.A).Dot(rack.N) - r1;
                            RigidMove(partner, rack.N * gap);
                        }
                        continue;
                    }
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

                // Bevel pairs (shared cone apex, no pitch circle in these views): the mating gear moves along.
                if (a0.Pitch is null && a0.Apex is not null && moved.Length > Eps)
                    foreach (var partner in Of(mesh, id))
                        if (!visited.Contains(partner)) RigidMove(partner, moved);

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

                // A moved motor or engine takes its shaft (and the rest of the shaft's parts) along.
                if (a0.Drives && moved.Length > Eps)
                    foreach (var shaft in Of(carrier, id))
                        if (!visited.Contains(shaft)) RigidMove(shaft, moved, withMesh: false);

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
                if (f.Pitch is null || f.Inner || !f.Meshes) continue;
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
