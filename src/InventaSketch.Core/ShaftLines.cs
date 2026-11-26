using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace InventaSketch.Core
{
    /// <summary>How an element takes part in a shaft line (<see cref="ShaftLines"/>).</summary>
    public enum ShaftPart
    {
        /// <summary>Turns with the parts it is fixed to and takes or gives torque (gears, pulleys, machines, pumps).</summary>
        Member,
        /// <summary>Turns with the line and passes its torque on unchanged (shafts, couplings, joints).</summary>
        PassThrough,
        /// <summary>Takes the speed of the part it sits on but joins nothing (bearings).</summary>
        Leaf,
        /// <summary>Never joins a shaft line (planet carriers, cages, gear sets, nuts).</summary>
        Outside,
    }

    /// <summary>Optional for types: their <see cref="ShaftPart"/>. Types without it are <see cref="ShaftPart.Member"/>s.</summary>
    public interface IShaftPart
    {
        ShaftPart ShaftPart(SketchObject o);
    }

    /// <summary>
    /// Shaft lines of a document for element values, read from the core coupling relations (<see cref="Coupling.Graph"/>):
    /// elements with a speed variable <c>n</c> that are carried by a shaft or concentric with each other (a loose hub turns
    /// freely) turn together. Members and pass-through parts (<see cref="IShaftPart"/>) join a line; leaves (bearings) take the
    /// speed of the part they sit on; outside parts stay out. On a line the torque <c>T</c> passes when at most two members
    /// take or give it (with a branch it splits in an unknown way); a member of a gear unit docked on the line
    /// (<see cref="IGearUnit"/>, <see cref="GearUnitDocking"/>) counts as one of them. Every module emits the same star of
    /// equalities (<see cref="Links"/>) with its own relations, so the solver joins elements of any module on one shaft.
    /// Built once per document and registry (documents are immutable).
    /// </summary>
    public sealed class ShaftLines
    {
        /// <summary>A set of parts that turn together.</summary>
        public sealed class Line
        {
            internal Line(IReadOnlyList<string> members, IReadOnlyList<string> torque, IReadOnlyList<string> pass, string rep, string? torqueRep, int ports = 0)
            {
                Members = members; Torque = torque; Pass = pass; Rep = rep; TorqueRep = torqueRep; Ports = ports;
            }

            /// <summary>All parts of the line (ordinal order).</summary>
            public IReadOnlyList<string> Members { get; }
            /// <summary>Members that take or give torque (drives, driven machines, gears, pulleys), not pass-through parts.</summary>
            public IReadOnlyList<string> Torque { get; }
            /// <summary>Pass-through parts (shafts, couplings) with a torque variable: they carry the line torque unchanged.</summary>
            public IReadOnlyList<string> Pass { get; }
            /// <summary>The speed representative: the first pass-through part, else the first member.</summary>
            public string Rep { get; }
            /// <summary>The torque representative (first of <see cref="Pass"/>, else of <see cref="Torque"/>), null without torque.</summary>
            public string? TorqueRep { get; }
            /// <summary>
            /// Members of gear units (gear sets, CVT) docked on the line (<see cref="GearUnitDocking"/>): each takes or gives
            /// torque, so they count towards the "at most two" rule, as in the mechanical module's own topology.
            /// </summary>
            public int Ports { get; }
            /// <summary>
            /// The torque passes along the line: at most two parts take or give it (torque members and docked unit members) and
            /// at least two parts of the line have a torque.
            /// </summary>
            public bool CarriesTorque => Torque.Count + Ports <= 2 && Torque.Count + Pass.Count >= 2;
        }

        private static readonly ConditionalWeakTable<SketchDocument, ConditionalWeakTable<Registry, ShaftLines>> Cache = new();

        private readonly Dictionary<string, Line> _lineOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _leaves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _leafOf = new(StringComparer.Ordinal);
        private readonly List<Line> _lines = new();

        /// <summary>
        /// The shaft lines of <paramref name="doc"/> (cached). While the coupling graph of the document is being built on this
        /// thread (<see cref="Coupling.GraphPending"/>) the result is empty and not cached.
        /// </summary>
        public static ShaftLines Of(Registry registry, SketchDocument doc)
        {
            var perDoc = Cache.GetValue(doc, _ => new ConditionalWeakTable<Registry, ShaftLines>());
            if (perDoc.TryGetValue(registry, out var cached)) return cached;
            if (Coupling.GraphPending(doc)) return new ShaftLines(registry, doc, Array.Empty<(LinkKind, string, string)>());
            return perDoc.GetValue(registry, r => new ShaftLines(r, doc, Coupling.Graph(r, doc, Lang.En)));
        }

        private ShaftLines(Registry registry, SketchDocument doc, IReadOnlyList<(LinkKind Kind, string A, string B)> graph)
        {
            var vars = new Dictionary<string, (bool N, bool T, ShaftPart Part)>(StringComparer.Ordinal);
            (bool N, bool T, ShaftPart Part) Info(string id)
            {
                if (vars.TryGetValue(id, out var v)) return v;
                var o = doc.Get(id);
                var type = o is null ? null : registry.Type(o.Type);
                var part = o is not null && type is IShaftPart sp ? sp.ShaftPart(o) : ShaftPart.Member;
                return vars[id] = (ValueModel.Variable(type, "n") is not null, ValueModel.Variable(type, "T") is not null, part);
            }
            bool Joins(string id) => Info(id) is (true, _, ShaftPart.Member or ShaftPart.PassThrough);
            bool IsLeaf(string id) => Info(id) is (true, _, ShaftPart.Leaf);

            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            string Find(string x)
            {
                if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; }
                if (p == x) return x;
                var root = Find(p);
                parent[x] = root;
                return root;
            }
            void Union(string x, string y)
            {
                var rx = Find(x);
                var ry = Find(y);
                if (rx == ry) return;
                if (string.CompareOrdinal(rx, ry) < 0) parent[ry] = rx; else parent[rx] = ry;
            }
            void Leaf(string carrier, string leaf)
            {
                if (!_leaves.TryGetValue(carrier, out var l)) _leaves[carrier] = l = new List<string>();
                if (!l.Contains(leaf)) l.Add(leaf);
                _leafOf[leaf] = carrier;
            }

            foreach (var (kind, a, b) in graph)
            {
                if (kind is not (LinkKind.Carried or LinkKind.Bonded)) continue;
                // Carried: a carries b; a loose hub turns freely on its shaft.
                if (kind == LinkKind.Carried && doc.Get(b)?.Str("hub", "fixed") == "loose") continue;
                if (Info(a).Part == ShaftPart.Outside || Info(b).Part == ShaftPart.Outside) continue;
                if (IsLeaf(b) && Joins(a)) { Leaf(a, b); continue; }
                if (IsLeaf(a) && Joins(b)) { Leaf(b, a); continue; }
                if (Joins(a) && Joins(b)) Union(a, b);
            }

            var ports = DockedPorts(registry, doc, graph, Joins, Find);

            foreach (var g in parent.Keys.ToList().GroupBy(Find))
            {
                var members = g.OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (members.Count < 2) continue;
                var torque = members.Where(m => Info(m) is (_, true, ShaftPart.Member)).ToList();
                var pass = members.Where(m => Info(m) is (_, true, ShaftPart.PassThrough)).ToList();
                var rep = members.FirstOrDefault(m => Info(m).Part == ShaftPart.PassThrough) ?? members[0];
                var line = new Line(members, torque, pass, rep, pass.Count > 0 ? pass[0] : torque.FirstOrDefault(), ports.TryGetValue(g.Key, out var pc) ? pc : 0);
                _lines.Add(line);
                foreach (var m in members) _lineOf[m] = line;
            }
        }

        /// <summary>
        /// Gear unit members docked on parts that join a line: the number per line root (<paramref name="find"/>). Leaves and
        /// outside parts do not count; a unit docked on another unit neither (the two share their members directly).
        /// </summary>
        private static Dictionary<string, int> DockedPorts(Registry registry, SketchDocument doc, IReadOnlyList<(LinkKind Kind, string A, string B)> graph,
            Func<string, bool> joins, Func<string, string> find)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var seen = new HashSet<(string, string)>();
            RenderContext? ctx = null;
            var anchors = new Dictionary<string, IReadOnlyList<Anchor>>(StringComparer.Ordinal);
            IReadOnlyList<Anchor> AnchorsOf(SketchObject o)
            {
                if (anchors.TryGetValue(o.Id, out var l)) return l;
                ctx ??= new RenderContext(doc, Lang.En).For(registry);
                return anchors[o.Id] = registry.Type(o.Type) is null ? Array.Empty<Anchor>() : registry.PageAnchors(o, ctx).ToList();
            }
            foreach (var (kind, a, b) in graph)
            {
                if (kind is not (LinkKind.Carried or LinkKind.Bonded)) continue;
                SketchObject? oa = doc.Get(a), ob = doc.Get(b);
                bool ua = GearUnitDocking.IsUnit(registry, oa), ub = GearUnitDocking.IsUnit(registry, ob);
                if (ua == ub) continue;
                var (unit, part) = ua ? (oa!, ob) : (ob!, oa);
                if (part is null || !joins(part.Id) || !seen.Add((unit.Id, part.Id))) continue;
                if (GearUnitDocking.DockedPort(registry, unit, part, AnchorsOf) is null) continue;
                var root = find(part.Id);
                counts[root] = (counts.TryGetValue(root, out var c) ? c : 0) + 1;
            }
            return counts;
        }

        /// <summary>All lines (two or more parts).</summary>
        public IReadOnlyList<Line> Lines => _lines;

        /// <summary>The line <paramref name="id"/> belongs to, or null.</summary>
        public Line? LineOf(string id) => _lineOf.TryGetValue(id, out var l) ? l : null;

        /// <summary>Leaves (bearings) sitting on <paramref name="id"/>.</summary>
        public IReadOnlyList<string> LeavesOf(string id) => _leaves.TryGetValue(id, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The part leaf <paramref name="id"/> sits on, or null.</summary>
        public string? LeafCarrier(string id) => _leafOf.TryGetValue(id, out var c) ? c : null;

        /// <summary>
        /// The shaft equalities <paramref name="id"/> takes part in, as a star around the representatives (the representative
        /// links every other part, the others link the representative): one speed <c>n</c> (with <paramref name="sameSpeed"/>),
        /// the speed of the leaves, and the torque <c>T</c> when the line carries it (with <paramref name="sameTorque"/>, none
        /// when null). The representatives are shafts or couplings where there are any, so a line whose other members belong to
        /// modules that emit no links is still joined.
        /// </summary>
        public IEnumerable<BoundRelation> Links(string id, Relation sameSpeed, Relation? sameTorque)
        {
            var list = new List<BoundRelation>();
            if (LineOf(id) is { } line)
            {
                if (id == line.Rep) { foreach (var m in line.Members) if (m != id) list.Add(Eq(sameSpeed, id, m, "n")); }
                else list.Add(Eq(sameSpeed, line.Rep, id, "n"));
                if (sameTorque is not null && line.CarriesTorque && line.TorqueRep is { } rep && (line.Torque.Contains(id) || line.Pass.Contains(id)))
                {
                    if (id == rep) { foreach (var m in line.Torque.Concat(line.Pass)) if (m != id) list.Add(Eq(sameTorque, id, m, "T")); }
                    else list.Add(Eq(sameTorque, rep, id, "T"));
                }
            }
            foreach (var leaf in LeavesOf(id)) list.Add(Eq(sameSpeed, id, leaf, "n"));
            if (LeafCarrier(id) is { } carrier) list.Add(Eq(sameSpeed, carrier, id, "n"));
            return list;
        }

        private static BoundRelation Eq(Relation r, string a, string b, string key)
            => new(r, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(a, key), ["b"] = new VarRef(b, key) });
    }
}
