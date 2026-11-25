using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>What "select connected" follows from an element (<see cref="Connectivity"/>).</summary>
    public enum ConnectKind
    {
        /// <summary>The most meaningful kind for the element (<see cref="Connectivity.AutoKind"/>).</summary>
        Auto,
        /// <summary>
        /// Everything joined by connection lines (wires, cables, buses, pipes, ducts, process and energy lines): line ends
        /// docked on ports and elements, branches docked on lines, junctions and connected crossings – and the elements on
        /// them, through which the net continues (the whole connected circuit or piping).
        /// </summary>
        Net,
        /// <summary>
        /// What moves along: mechanical coupling (<see cref="Coupling.Graph"/>: meshing gears, a shaft and its hubs, concentric
        /// parts, belts and chains over their pulleys and sprockets) and linkages (links docked on joints and pivots).
        /// </summary>
        Coupled,
        /// <summary>
        /// The following elements: successors along directed connectors (first docked end → last docked end; flowcharts,
        /// block and process diagrams), with the connectors; for a linkage the links from the selected one outward (away from
        /// the fixed pivots).
        /// </summary>
        Chain,
        /// <summary>The closed loop the element is part of (a cycle in the graph of its <see cref="Connectivity.AutoKind"/>).</summary>
        Loop,
        /// <summary>Plan lines of the same type joined at their ends or butting on each other (walls of rooms).</summary>
        Walls,
    }

    /// <summary>
    /// "Select connected": the elements connected with one element, by kind (<see cref="ConnectKind"/>). Works on one document
    /// holding every element of the page (hosts that keep each element in its own unit pass a context document with all of
    /// them). Reuses what the engine already knows: attachments (<see cref="Attachment"/>), line media
    /// (<see cref="Building.LineMedium"/>), coupling (<see cref="Coupling.Graph"/>), linkage roles (<see cref="Linkage"/>)
    /// and the neighbours a type renders from (<see cref="ObjectType.Dependents"/>). Results are ordered: the element first,
    /// then in the order they were reached.
    /// </summary>
    public static class Connectivity
    {
        /// <summary>Kinds offered to the user, in menu order.</summary>
        public static readonly IReadOnlyList<ConnectKind> Offered = new[] { ConnectKind.Net, ConnectKind.Coupled, ConnectKind.Chain, ConnectKind.Loop, ConnectKind.Walls };

        /// <summary>Short menu label of a kind.</summary>
        public static I18nText Label(ConnectKind kind) => kind switch
        {
            ConnectKind.Net => new("Net", "Netz"),
            ConnectKind.Coupled => new("Moves along", "Bewegt sich mit"),
            ConnectKind.Chain => new("Following elements", "Folgende Elemente"),
            ConnectKind.Loop => new("Closed loop", "Geschlossene Schleife"),
            ConnectKind.Walls => new("Room walls", "Raumwände"),
            _ => new("Connected", "Verbunden"),
        };

        /// <summary>One line on what a kind selects (menu hint).</summary>
        public static I18nText Hint(ConnectKind kind) => kind switch
        {
            ConnectKind.Net => new("Every line and element joined to it (wires, cables, pipes, junctions)", "Alle damit verbundenen Leitungen und Elemente (Leitungen, Kabel, Rohre, Knoten)"),
            ConnectKind.Coupled => new("Every part that moves with it (gears, shaft, belts, links)", "Alle Teile, die sich mitbewegen (Zahnräder, Welle, Riemen, Glieder)"),
            ConnectKind.Chain => new("Everything after it along the connectors", "Alles, was entlang der Verbinder danach kommt"),
            ConnectKind.Loop => new("The closed loop it belongs to", "Die geschlossene Schleife, zu der es gehört"),
            ConnectKind.Walls => new("Walls joined with it at corners and ends", "An Ecken und Enden verbundene Wände"),
            _ => new("Connected elements", "Verbundene Elemente"),
        };

        /// <summary>
        /// The elements connected with <paramref name="id"/> by <paramref name="kind"/>, the element first; just the element if
        /// nothing is connected that way; empty if it is not in the document.
        /// </summary>
        public static IReadOnlyList<string> Connected(Registry registry, SketchDocument doc, string id, ConnectKind kind, RenderContext? ctx = null)
            => new Scope(registry, doc, ctx).Connected(id, kind);

        /// <summary>The most meaningful kind for <paramref name="id"/> (wire → net, gear → moves along, flow box → following, wall → room walls); <see cref="ConnectKind.Auto"/> if none applies.</summary>
        public static ConnectKind AutoKind(Registry registry, SketchDocument doc, string id, RenderContext? ctx = null)
            => new Scope(registry, doc, ctx).Auto(id);

        /// <summary>
        /// The kinds that select more than the element itself, in <see cref="Offered"/> order, each with its result (the loop
        /// only if the element is part of one). Computes every graph once.
        /// </summary>
        public static IReadOnlyList<(ConnectKind Kind, IReadOnlyList<string> Ids)> Options(Registry registry, SketchDocument doc, string id, RenderContext? ctx = null)
        {
            var scope = new Scope(registry, doc, ctx);
            var list = new List<(ConnectKind, IReadOnlyList<string>)>();
            if (doc.Get(id) is null) return list;
            foreach (var k in Offered)
            {
                var ids = scope.Connected(id, k);
                if (ids.Count > 1) list.Add((k, ids));
            }
            return list;
        }

        // ------------------------------------------------------------------ graphs

        private sealed record Edge(int Id, string A, string B, string? Via);

        /// <summary>A multigraph over object ids; undirected graphs list each edge under both ends.</summary>
        private sealed class Graph
        {
            public Graph(bool directed) { Directed = directed; }
            public bool Directed { get; }
            public readonly Dictionary<string, List<Edge>> Out = new(StringComparer.Ordinal);
            public readonly Dictionary<string, List<Edge>> In = new(StringComparer.Ordinal);
            public readonly Dictionary<string, Edge> ByVia = new(StringComparer.Ordinal);
            private int _next;

            public void Add(string a, string b, string? via = null)
            {
                if (a == b) return;
                var e = new Edge(_next++, a, b, via);
                List(Out, a).Add(e);
                if (Directed) List(In, b).Add(e); else List(Out, b).Add(e);
                if (via is not null && !ByVia.ContainsKey(via)) ByVia[via] = e;
            }

            /// <summary>Adds an undirected edge unless the pair is already joined (relations found from both sides).</summary>
            public void AddOnce(string a, string b)
            {
                if (a == b || !_pairs.Add(string.CompareOrdinal(a, b) < 0 ? a + "" + b : b + "" + a)) return;
                Add(a, b);
            }

            private readonly HashSet<string> _pairs = new(StringComparer.Ordinal);

            public bool Has(string id) => Out.ContainsKey(id) || In.ContainsKey(id);

            public IReadOnlyList<Edge> From(string id) => Out.TryGetValue(id, out var l) ? l : (IReadOnlyList<Edge>)Array.Empty<Edge>();

            public IReadOnlyList<Edge> To(string id) => In.TryGetValue(id, out var l) ? l : (IReadOnlyList<Edge>)Array.Empty<Edge>();

            private static List<Edge> List(Dictionary<string, List<Edge>> map, string key)
            {
                if (!map.TryGetValue(key, out var l)) map[key] = l = new List<Edge>();
                return l;
            }
        }

        /// <summary>One query context: the document, its anchors and the graphs, each built once when first needed.</summary>
        private sealed class Scope
        {
            private readonly Registry _registry;
            private readonly SketchDocument _doc;
            private readonly RenderContext _ctx;
            private readonly Dictionary<string, List<Anchor>> _anchors = new(StringComparer.Ordinal);
            private readonly Dictionary<string, bool> _conductor = new(StringComparer.Ordinal);
            private Graph? _net, _coupled, _chain, _walls;
            private HashSet<string>? _linkage;

            public Scope(Registry registry, SketchDocument doc, RenderContext? ctx)
            {
                _registry = registry;
                _doc = doc;
                _ctx = ctx is not null && ReferenceEquals(ctx.Doc, doc) ? ctx : new RenderContext(doc, ctx?.Lang ?? Lang.En, ctx?.Monochrome ?? false);
            }

            private IEnumerable<SketchObject> Lines => _doc.Objects.Values.Where(o => o.IsPointGeometry && o.Attach is not null && _registry.Type(o.Type) is not null);

            private List<Anchor> AnchorsOf(SketchObject o)
            {
                if (!_anchors.TryGetValue(o.Id, out var list))
                    _anchors[o.Id] = list = _registry.Type(o.Type) is null ? new List<Anchor>() : _registry.PageAnchors(o, _ctx).ToList();
                return list;
            }

            /// <summary>A connection line: its type carries a medium or one of its ends is docked on a connection port.</summary>
            private bool IsConductor(SketchObject o)
            {
                if (!o.IsPointGeometry) return false;
                if (!_conductor.TryGetValue(o.Id, out var yes))
                    _conductor[o.Id] = yes = Building.LineMedium(_registry, _doc, o, _ctx.Lang) is not null;
                return yes;
            }

            private bool IsLinkage(string id)
            {
                _linkage ??= new HashSet<string>(_doc.Objects.Values.Where(o => Linkage.IsPart(_registry, o)).Select(o => o.Id), StringComparer.Ordinal);
                return _linkage.Contains(id);
            }

            private bool IsChainable(SketchObject o) => !o.IsPointGeometry && _registry.Type(o.Type)?.ChainConnector(o) is not null;

            /// <summary>A plan line that joins others of its type at its ends (walls): renders from neighbours, plan scale, no medium.</summary>
            private bool IsWallLike(SketchObject o)
                => o.IsPointGeometry && _registry.Type(o.Type) is { } t && t.DependsOnNeighbours && t.UsesPlanScale && !t.HopsAtCrossings && t.InsertablePoints
                   && !IsConductor(o) && !t.DocksOnSurface;

            public Graph Net
            {
                get
                {
                    if (_net is not null) return _net;
                    var g = new Graph(directed: false);
                    foreach (var line in Lines)
                    {
                        if (!IsConductor(line)) continue;
                        foreach (var a in line.Attach!)
                        {
                            if (a is null || a.ObjectId == line.Id || _doc.Get(a.ObjectId) is not { } target) continue;
                            // A branch joins another connection line; annotation lines docked on a line are no part of the net.
                            if (target.IsPointGeometry && !IsConductor(target)) continue;
                            g.Add(line.Id, target.Id);
                        }
                    }
                    return _net = g;
                }
            }

            public Graph Coupled
            {
                get
                {
                    if (_coupled is not null) return _coupled;
                    var g = new Graph(directed: false);
                    foreach (var (_, a, b) in Coupling.Graph(_registry, _doc, _ctx.Lang)) g.AddOnce(a, b);
                    foreach (var line in Lines)
                    {
                        if (!IsLinkage(line.Id)) continue;
                        foreach (var a in line.Attach!)
                            if (a is not null && a.ObjectId != line.Id && _doc.Objects.ContainsKey(a.ObjectId) && IsLinkage(a.ObjectId)) g.AddOnce(line.Id, a.ObjectId);
                    }
                    return _coupled = g;
                }
            }

            /// <summary>Directed connectors: lines whose first and last docked ends sit on two elements (on ports, or on chainable elements).</summary>
            public Graph Chain
            {
                get
                {
                    if (_chain is not null) return _chain;
                    var g = new Graph(directed: true);
                    foreach (var line in Lines)
                    {
                        if (IsLinkage(line.Id)) continue;
                        var ends = new List<string>();
                        foreach (var a in line.Attach!)
                        {
                            if (a is null || _doc.Get(a.ObjectId) is not { IsPointGeometry: false } target) continue;
                            var onPort = a.AnchorId is not null && AnchorsOf(target).Any(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port && x.Id == a.AnchorId);
                            if (onPort || IsChainable(target)) ends.Add(target.Id);
                        }
                        if (ends.Count >= 2 && ends[0] != ends[ends.Count - 1]) g.Add(ends[0], ends[ends.Count - 1], line.Id);
                    }
                    return _chain = g;
                }
            }

            public Graph Walls
            {
                get
                {
                    if (_walls is not null) return _walls;
                    // Walls meet at joints: each joint is a node of its own, so three walls meeting in one corner form a
                    // star (no false loop), while four walls around a room form a ring.
                    var g = new Graph(directed: false);
                    var joints = new List<Vec2>();
                    string JointAt(Vec2 p)
                    {
                        var k = joints.FindIndex(q => q.DistanceTo(p) <= JointTolerance);
                        if (k < 0) { joints.Add(p); k = joints.Count - 1; }
                        return JointPrefix + k.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    foreach (var o in _doc.Objects.Values.Where(IsWallLike))
                        foreach (var other in _registry.Type(o.Type)!.Dependents(o, _ctx))
                        {
                            if (_doc.Get(other) is not { } w || w.Type != o.Type || w.Id == o.Id) continue;
                            var joint = JointAt(Meeting(_doc.ResolvePoints(o), _doc.ResolvePoints(w)));
                            g.AddOnce(o.Id, joint);
                            g.AddOnce(w.Id, joint);
                        }
                    return _walls = g;
                }
            }

            /// <summary>Joint nodes of the wall graph (never part of a result).</summary>
            private const string JointPrefix = "joint:";
            private const double JointTolerance = 0.5;

            /// <summary>Where two joined lines meet: the end of one nearest to the other line.</summary>
            private static Vec2 Meeting(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
            {
                static double Dist(Vec2 p, IReadOnlyList<Vec2> line)
                {
                    var d = double.MaxValue;
                    for (var i = 0; i + 1 < line.Count; i++) d = Math.Min(d, Geo.ClosestOnSegment(p, line[i], line[i + 1]).Point.DistanceTo(p));
                    return line.Count == 1 ? p.DistanceTo(line[0]) : d;
                }
                var ends = new List<Vec2>();
                if (a.Count > 0) { ends.Add(a[0]); ends.Add(a[a.Count - 1]); }
                if (b.Count > 0) { ends.Add(b[0]); ends.Add(b[b.Count - 1]); }
                return ends.OrderBy(e => Math.Min(Dist(e, a), Dist(e, b)) + (Dist(e, a) + Dist(e, b))).First();
            }

            private static IReadOnlyList<string> WithoutJoints(IReadOnlyList<string> ids)
                => ids.Any(x => x.StartsWith(JointPrefix, StringComparison.Ordinal)) ? ids.Where(x => !x.StartsWith(JointPrefix, StringComparison.Ordinal)).ToList() : ids;

            // ------------------------------------------------------------------ queries

            public IReadOnlyList<string> Connected(string id, ConnectKind kind)
            {
                var o = _doc.Get(id);
                if (o is null) return Array.Empty<string>();
                if (kind == ConnectKind.Auto) kind = Auto(id);
                return kind switch
                {
                    ConnectKind.Net => Reach(Net, id),
                    ConnectKind.Coupled => Reach(Coupled, id),
                    ConnectKind.Chain => IsLinkage(id) ? Outward(id) : Following(id),
                    ConnectKind.Loop => Loop(id),
                    ConnectKind.Walls => IsWallLike(o) ? WithoutJoints(Reach(Walls, id)) : new[] { id },
                    _ => new[] { id },
                };
            }

            public ConnectKind Auto(string id)
            {
                var o = _doc.Get(id);
                if (o is null) return ConnectKind.Auto;
                if (IsLinkage(id)) return ConnectKind.Coupled;
                if (o.IsPointGeometry)
                {
                    // A connector between flow elements follows the flow; a connection line the net.
                    if (Chain.ByVia.TryGetValue(id, out var e) && IsChainable(_doc.Get(e.A)!) && IsChainable(_doc.Get(e.B)!)) return ConnectKind.Chain;
                    if (IsConductor(o)) return ConnectKind.Net;
                    if (IsWallLike(o)) return ConnectKind.Walls;
                    if (Chain.ByVia.ContainsKey(id)) return ConnectKind.Chain;
                }
                else
                {
                    if (Coupled.Has(id)) return ConnectKind.Coupled;
                    if (IsChainable(o) && Chain.Has(id)) return ConnectKind.Chain;
                    if (Net.Has(id)) return ConnectKind.Net;
                    if (Chain.Has(id)) return ConnectKind.Chain;
                }
                foreach (var k in new[] { ConnectKind.Net, ConnectKind.Coupled, ConnectKind.Chain, ConnectKind.Walls })
                    if (Connected(id, k).Count > 1) return k;
                return ConnectKind.Auto;
            }

            /// <summary>Everything reachable over the edges of <paramref name="g"/>, breadth first.</summary>
            private static IReadOnlyList<string> Reach(Graph g, string id)
            {
                var result = new List<string> { id };
                var seen = new HashSet<string>(result, StringComparer.Ordinal);
                for (var i = 0; i < result.Count; i++)
                    foreach (var e in g.From(result[i]))
                    {
                        var other = e.A == result[i] ? e.B : e.A;
                        if (seen.Add(other)) result.Add(other);
                    }
                return result;
            }

            /// <summary>Successors along the directed connectors, with the connectors (a connector starts at its target).</summary>
            private IReadOnlyList<string> Following(string id)
            {
                var g = Chain;
                var result = new List<string> { id };
                var seen = new HashSet<string>(result, StringComparer.Ordinal);
                var queue = new Queue<string>();
                if (g.ByVia.TryGetValue(id, out var start)) { if (seen.Add(start.B)) result.Add(start.B); queue.Enqueue(start.B); }
                else queue.Enqueue(id);
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    foreach (var e in g.From(n))
                    {
                        if (e.Via is not null && seen.Add(e.Via)) result.Add(e.Via);
                        if (seen.Add(e.B)) { result.Add(e.B); queue.Enqueue(e.B); }
                    }
                }
                return result;
            }

            /// <summary>
            /// A linkage from <paramref name="id"/> outward: the parts reached without stepping to a part nearer to the fixed
            /// pivots (frame) than the selected one; the whole mechanism if it has no fixed pivot.
            /// </summary>
            private IReadOnlyList<string> Outward(string id)
            {
                var g = Coupled;
                var depth = new Dictionary<string, int>(StringComparer.Ordinal);
                var queue = new Queue<string>();
                foreach (var o in _doc.Objects.Values)
                    if (IsLinkage(o.Id) && _registry.Type(o.Type)?.LinkageRole(o) == LinkageRole.FixedPivot) { depth[o.Id] = 0; queue.Enqueue(o.Id); }
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    foreach (var e in g.From(n))
                    {
                        var other = e.A == n ? e.B : e.A;
                        if (IsLinkage(other) && !depth.ContainsKey(other)) { depth[other] = depth[n] + 1; queue.Enqueue(other); }
                    }
                }
                int D(string x) => depth.TryGetValue(x, out var d) ? d : int.MaxValue;
                var own = D(id);
                var result = new List<string> { id };
                var seen = new HashSet<string>(result, StringComparer.Ordinal);
                for (var i = 0; i < result.Count; i++)
                    foreach (var e in g.From(result[i]))
                    {
                        var other = e.A == result[i] ? e.B : e.A;
                        if (IsLinkage(other) && D(other) >= own && seen.Add(other)) result.Add(other);
                    }
                return result;
            }

            /// <summary>The closed loop of the element in the graph of its own kind first, then in the others.</summary>
            private IReadOnlyList<string> Loop(string id)
            {
                var auto = Auto(id);
                var order = new List<ConnectKind> { auto, ConnectKind.Chain, ConnectKind.Net, ConnectKind.Walls, ConnectKind.Coupled };
                foreach (var k in order.Distinct())
                {
                    IReadOnlyList<string> loop = k switch
                    {
                        ConnectKind.Chain => DirectedLoop(Chain, id),
                        ConnectKind.Net => UndirectedLoop(Net, id),
                        ConnectKind.Walls => WithoutJoints(UndirectedLoop(Walls, id)),
                        ConnectKind.Coupled => UndirectedLoop(Coupled, id),
                        _ => Array.Empty<string>(),
                    };
                    if (loop.Count > 1) return loop;
                }
                return new[] { id };
            }

            /// <summary>
            /// The strongly connected part of a directed graph around the element (what leads back to it), with the connectors
            /// inside it; a connector counts when both its ends are in such a loop.
            /// </summary>
            private static IReadOnlyList<string> DirectedLoop(Graph g, string id)
            {
                var node = g.ByVia.TryGetValue(id, out var via) ? via.A : id;
                var forward = Walk(node, n => g.From(n).Select(e => e.B));
                var back = new HashSet<string>(Walk(node, n => g.To(n).Select(e => e.A)), StringComparer.Ordinal);
                var scc = forward.Where(back.Contains).ToList();
                if (scc.Count < 2) return Array.Empty<string>();
                var inside = new HashSet<string>(scc, StringComparer.Ordinal);
                if (via is not null && !(inside.Contains(via.A) && inside.Contains(via.B))) return Array.Empty<string>();
                var result = new List<string> { id };
                var seen = new HashSet<string>(result, StringComparer.Ordinal);
                foreach (var n in scc)
                {
                    if (seen.Add(n)) result.Add(n);
                    foreach (var e in g.From(n))
                        if (e.Via is not null && inside.Contains(e.B) && seen.Add(e.Via)) result.Add(e.Via);
                }
                return result;
            }

            private static List<string> Walk(string start, Func<string, IEnumerable<string>> next)
            {
                var list = new List<string> { start };
                var seen = new HashSet<string>(list, StringComparer.Ordinal);
                for (var i = 0; i < list.Count; i++)
                    foreach (var n in next(list[i]))
                        if (seen.Add(n)) list.Add(n);
                return list;
            }

            /// <summary>
            /// The two-edge-connected part of an undirected multigraph around the element: what stays connected when any one
            /// edge is removed, i.e. the loops it lies on (bridges, found with Tarjan's low-link, are left out).
            /// </summary>
            private static IReadOnlyList<string> UndirectedLoop(Graph g, string id)
            {
                if (!g.Has(id)) return Array.Empty<string>();
                var component = Reach(g, id);
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                var low = new Dictionary<string, int>(StringComparer.Ordinal);
                var bridges = new HashSet<int>();
                var counter = 0;
                // Iterative depth-first search (no recursion limit on long chains).
                var stack = new Stack<(string Node, int InEdge, int Next)>();
                index[id] = low[id] = counter++;
                stack.Push((id, -1, 0));
                while (stack.Count > 0)
                {
                    var (n, inEdge, next) = stack.Pop();
                    var edges = g.From(n);
                    if (next < edges.Count)
                    {
                        stack.Push((n, inEdge, next + 1));
                        var e = edges[next];
                        if (e.Id == inEdge) continue;
                        var other = e.A == n ? e.B : e.A;
                        if (index.TryGetValue(other, out var oi)) { low[n] = Math.Min(low[n], oi); continue; }
                        index[other] = low[other] = counter++;
                        stack.Push((other, e.Id, 0));
                        continue;
                    }
                    // Finished n: report to its parent.
                    if (inEdge < 0 || stack.Count == 0) continue;
                    var parent = stack.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[n]);
                    if (low[n] > index[parent]) bridges.Add(inEdge);
                }
                var result = new List<string> { id };
                var seen = new HashSet<string>(result, StringComparer.Ordinal);
                for (var i = 0; i < result.Count; i++)
                    foreach (var e in g.From(result[i]))
                    {
                        if (bridges.Contains(e.Id)) continue;
                        var other = e.A == result[i] ? e.B : e.A;
                        if (seen.Add(other)) result.Add(other);
                    }
                return result.Count > 1 && component.Count > 1 ? result : Array.Empty<string>();
            }
        }
    }
}
