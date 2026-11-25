using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
namespace InventaSketch.Core
{
    // =============================================================================================
    // Port flows across connections (Calc 4, engineering-calculations.md sections 1.6 and 13): one net builder shared by the
    // modules (Fluid, Process, Building services, Energy). Each module describes its elements and lines in a NetSpec; the
    // nets are built per document and spec and cached.
    // =============================================================================================

    /// <summary>How the values of one class meet in a connected net.</summary>
    public enum NetRule
    {
        /// <summary>Every member shares the value (pressure, voltage): the whole net, branches included.</summary>
        Equal,
        /// <summary>
        /// A through quantity (volume flow, temperature of a stream): shared when at most two elements carry it in the net;
        /// lines join only if the net does not branch.
        /// </summary>
        Flow,
        /// <summary>
        /// A total member is the sum of the part members (parallel strings, feeders on a board, the trunk of a branched pipe);
        /// an unbranched run with at most two elements, or a net without a total, behaves as <see cref="Flow"/>.
        /// </summary>
        Sum,
        /// <summary>
        /// The single total member passes its value to every part member (the supply temperature at a split); an unbranched run
        /// with at most two elements, or a net without exactly one total, behaves as <see cref="Flow"/>.
        /// </summary>
        Spread,
    }

    /// <summary>Role of a port variable in a <see cref="NetRule.Sum"/> class.</summary>
    public enum PortRole { Share, Part, Total }

    /// <summary>A variable an element exposes at one of its ports for a value class ("Q" → the pump's flow).</summary>
    public readonly record struct PortVar(string Class, string Key, PortRole Role = PortRole.Share);

    /// <summary>What a module tells the net builder about its elements and lines.</summary>
    public sealed class NetSpec
    {
        public string Prefix { get; init; } = "";
        /// <summary>Connection lines of the module (their ends dock on ports and on other lines).</summary>
        public Func<SketchObject, bool> IsLine { get; init; } = _ => false;
        /// <summary>Variables of an element at a port (port id "*" for an end docked without a port id, e.g. on a rail).</summary>
        public Func<SketchObject, string, IEnumerable<PortVar>> PortVars { get; init; } = (_, _) => Array.Empty<PortVar>();
        /// <summary>Variables of a line (one value along the whole line).</summary>
        public Func<SketchObject, IEnumerable<PortVar>> LineVars { get; init; } = _ => Array.Empty<PortVar>();
        /// <summary>Groups of ports an element passes a class through unchanged (valves, inline parts, couplings).</summary>
        public Func<SketchObject, string, IEnumerable<string[]>> Through { get; init; } = (_, _) => Array.Empty<string[]>();
        public Func<string, NetRule> Rule { get; init; } = _ => NetRule.Equal;
        /// <summary>Bilingual name of the link relation of a class ("Flow passed on").</summary>
        public Func<string, I18nText> Name { get; init; } = c => new I18nText(c);
        /// <summary>The classes in a fixed order (deterministic relation order).</summary>
        public IReadOnlyList<string> Classes { get; init; } = Array.Empty<string>();

        /// <summary>The nets built for this spec, per document (documents are immutable).</summary>
        internal ConditionalWeakTable<SketchDocument, ValueNets> Cache { get; } = new();
    }

    /// <summary>
    /// The nets of a document for one module: line ends docked on ports (attachments with a port id) and on other lines
    /// join ports into nets; elements pass classes through (valves). For each class the members of a net are linked by its
    /// <see cref="NetRule"/>. Built once per document (documents are immutable) and cached.
    /// </summary>
    public sealed class ValueNets
    {
        private readonly Dictionary<string, List<BoundRelation>> _byObject = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> _component = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<int, List<(string Obj, string Port)>>> _ports = new(StringComparer.Ordinal);

        private static readonly Dictionary<string, Relation> Equalities = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Relation> Sums = new(StringComparer.Ordinal);

        /// <summary>The nets of <paramref name="doc"/> (cached per document and spec).</summary>
        public static ValueNets Of(SketchDocument doc, NetSpec spec) => spec.Cache.GetValue(doc, d => new ValueNets(d, spec));

        /// <summary>Port relations that involve <paramref name="objectId"/>.</summary>
        public IReadOnlyList<BoundRelation> For(string objectId)
            => _byObject.TryGetValue(objectId, out var l) ? l : (IReadOnlyList<BoundRelation>)Array.Empty<BoundRelation>();

        /// <summary>The element ports in the same net as (<paramref name="objectId"/>, <paramref name="port"/>) for a class (itself included).</summary>
        public IReadOnlyList<(string Obj, string Port)> PortsWith(string objectId, string port, string cls)
        {
            if (!_component.TryGetValue(cls, out var comp) || !comp.TryGetValue(PortNode(objectId, port), out var c)) return Array.Empty<(string, string)>();
            return _ports[cls].TryGetValue(c, out var l) ? l : (IReadOnlyList<(string, string)>)Array.Empty<(string, string)>();
        }

        private static string PortNode(string obj, string port) => "P\u0001" + obj + "\u0001" + port;
        private static string LineNode(string obj) => "L\u0001" + obj;

        private ValueNets(SketchDocument doc, NetSpec spec)
        {
            var objects = doc.Objects.Values.OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            var lines = objects.Where(o => o.IsPointGeometry && spec.IsLine(o)).ToList();
            var edges = new List<(string A, string B)>();
            var nodes = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            var degree = new Dictionary<string, int>(StringComparer.Ordinal);
            var tees = new HashSet<string>(StringComparer.Ordinal);
            void Node(string n) { if (known.Add(n)) nodes.Add(n); }

            foreach (var line in lines)
            {
                var ln = LineNode(line.Id);
                Node(ln);
                foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                {
                    if (a is null || a.ObjectId == line.Id || doc.Get(a.ObjectId) is not { } target) continue;
                    if (target.IsPointGeometry)
                    {
                        if (!spec.IsLine(target)) continue;
                        Node(LineNode(target.Id));
                        edges.Add((ln, LineNode(target.Id)));
                        tees.Add(ln);
                        continue;
                    }
                    var port = a.AnchorId is { } id && !id.StartsWith("rail", StringComparison.Ordinal) && !id.StartsWith("bar", StringComparison.Ordinal) ? id : "*";
                    var pn = PortNode(target.Id, port);
                    Node(pn);
                    edges.Add((ln, pn));
                    degree[pn] = degree.TryGetValue(pn, out var d) ? d + 1 : 1;
                }
            }

            // Ports of the same element that are docked (only those matter for passages).
            var docked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var n in nodes)
            {
                if (n[0] != 'P') continue;
                var parts = n.Split('\u0001');
                if (!docked.TryGetValue(parts[1], out var l)) docked[parts[1]] = l = new List<string>();
                l.Add(parts[2]);
            }

            foreach (var cls in spec.Classes)
            {
                var parent = new Dictionary<string, string>(StringComparer.Ordinal);
                string Find(string x)
                {
                    if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; }
                    if (p == x) return x;
                    var root = Find(p);
                    parent[x] = root;
                    return root;
                }
                void Union(string a, string b)
                {
                    var ra = Find(a);
                    var rb = Find(b);
                    if (ra != rb) parent[ra] = rb;
                }
                foreach (var n in nodes) Find(n);
                foreach (var (a, b) in edges) Union(a, b);
                foreach (var kv in docked)
                {
                    if (doc.Get(kv.Key) is not { } o) continue;
                    foreach (var group in spec.Through(o, cls))
                    {
                        var present = group.Where(g => kv.Value.Contains(g)).Select(g => PortNode(o.Id, g)).ToList();
                        for (var i = 1; i < present.Count; i++) Union(present[0], present[i]);
                    }
                }

                // Components in node order.
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                var comps = new List<List<string>>();
                foreach (var n in nodes)
                {
                    var r = Find(n);
                    if (!index.TryGetValue(r, out var c)) { index[r] = c = comps.Count; comps.Add(new List<string>()); }
                    comps[c].Add(n);
                }
                var compOf = new Dictionary<string, int>(StringComparer.Ordinal);
                var portsOf = new Dictionary<int, List<(string, string)>>();
                foreach (var n in nodes) compOf[n] = index[Find(n)];
                _component[cls] = compOf;
                _ports[cls] = portsOf;

                var rule = spec.Rule(cls);
                for (var c = 0; c < comps.Count; c++)
                {
                    var elementMembers = new List<(string Obj, string Key, PortRole Role)>();
                    var lineMembers = new List<(string Obj, string Key)>();
                    var branched = false;
                    var portList = new List<(string, string)>();
                    foreach (var n in comps[c])
                    {
                        var parts = n.Split('\u0001');
                        if (parts[0] == "L")
                        {
                            if (tees.Contains(n)) branched = true;
                            if (doc.Get(parts[1]) is { } line)
                                foreach (var pv in spec.LineVars(line))
                                    if (pv.Class == cls && !lineMembers.Contains((line.Id, pv.Key))) lineMembers.Add((line.Id, pv.Key));
                            continue;
                        }
                        portList.Add((parts[1], parts[2]));
                        if (degree.TryGetValue(n, out var deg) && deg > 1) branched = true;
                        if (doc.Get(parts[1]) is not { } o) continue;
                        foreach (var pv in spec.PortVars(o, parts[2]))
                            if (pv.Class == cls && !elementMembers.Any(m => m.Obj == o.Id && m.Key == pv.Key)) elementMembers.Add((o.Id, pv.Key, pv.Role));
                    }
                    portsOf[c] = portList;
                    elementMembers.Sort((x, y) => string.CompareOrdinal(x.Obj + "\u0001" + x.Key, y.Obj + "\u0001" + y.Key));
                    lineMembers.Sort((x, y) => string.CompareOrdinal(x.Obj + "\u0001" + x.Key, y.Obj + "\u0001" + y.Key));

                    var totals = elementMembers.Where(m => m.Role == PortRole.Total).ToList();
                    var simple = elementMembers.Count <= 2 && !branched;
                    if (rule == NetRule.Sum && totals.Count > 0 && !simple)
                    {
                        var parts = elementMembers.Where(m => m.Role != PortRole.Total).Select(m => new VarRef(m.Obj, m.Key)).ToList();
                        if (parts.Count == 0) continue;
                        foreach (var t in totals) AddSum(spec, cls, new VarRef(t.Obj, t.Key), parts);
                        continue;
                    }
                    if (rule == NetRule.Spread && totals.Count == 1 && !simple)
                    {
                        var total = new VarRef(totals[0].Obj, totals[0].Key);
                        foreach (var m in elementMembers.Where(m => m.Role != PortRole.Total)) AddEquality(spec, cls, total, new VarRef(m.Obj, m.Key));
                        continue;
                    }

                    var chain = new List<VarRef>();
                    if (rule == NetRule.Equal)
                    {
                        chain.AddRange(elementMembers.Select(m => new VarRef(m.Obj, m.Key)));
                        chain.AddRange(lineMembers.Select(m => new VarRef(m.Obj, m.Key)));
                    }
                    else if (elementMembers.Count <= 2)
                    {
                        chain.AddRange(elementMembers.Select(m => new VarRef(m.Obj, m.Key)));
                        if (!branched) chain.AddRange(lineMembers.Select(m => new VarRef(m.Obj, m.Key)));
                    }
                    for (var i = 1; i < chain.Count; i++) AddEquality(spec, cls, chain[0], chain[i]);
                }
            }
        }

        private void Register(BoundRelation b)
        {
            foreach (var id in b.Map.Values.Select(v => v.ObjectId).Distinct())
            {
                if (!_byObject.TryGetValue(id, out var l)) _byObject[id] = l = new List<BoundRelation>();
                l.Add(b);
            }
        }

        private void AddEquality(NetSpec spec, string cls, VarRef a, VarRef b)
        {
            if (a == b) return;
            Relation rel;
            var id = spec.Prefix + ".link." + cls;
            lock (Equalities)
                if (!Equalities.TryGetValue(id, out rel!)) Equalities[id] = rel = Relation.Equality(id, spec.Name(cls));
            Register(new BoundRelation(rel, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = a, ["b"] = b }));
        }

        private void AddSum(NetSpec spec, string cls, VarRef total, IReadOnlyList<VarRef> parts)
        {
            if (parts.Count == 1) { AddEquality(spec, cls, parts[0], total); return; }
            var id = spec.Prefix + ".sum." + cls;
            var key = id + "#" + parts.Count;
            Relation rel;
            lock (Sums)
                if (!Sums.TryGetValue(key, out rel!)) Sums[key] = rel = SumRelation(id, spec.Name(cls), parts.Count);
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["s"] = total };
            for (var i = 0; i < parts.Count; i++) map["a" + (i + 1)] = parts[i];
            Register(new BoundRelation(rel, map));
        }

        /// <summary>s = a1 + … + an, solved for every variable.</summary>
        private static Relation SumRelation(string id, I18nText name, int n)
        {
            var keys = Enumerable.Range(1, n).Select(i => "a" + i).ToArray();
            var sum = string.Join(" + ", keys.Select(k => "{" + k + "}"));
            var rel = new Relation(id, name, "{s} = " + sum, new[] { "s" }.Concat(keys).ToArray()) { Source = "S1" }
                .Solve("s", sum, v => keys.Sum(k => v(k)));
            foreach (var k in keys)
            {
                var others = keys.Where(x => x != k).ToArray();
                var kk = k;
                rel.Solve(kk, "{s} − " + string.Join(" − ", others.Select(x => "{" + x + "}")), v => v("s") - others.Sum(x => v(x)));
            }
            return rel;
        }
    }
}
