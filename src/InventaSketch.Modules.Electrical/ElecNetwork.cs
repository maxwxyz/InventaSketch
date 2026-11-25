using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// A two-terminal branch of an element in a circuit: the pins it connects and the variable keys of the element that
    /// describe it. <see cref="U"/> null with <see cref="ZeroU"/> = an ideal conductor (no voltage across it, e.g. a fuse);
    /// U or I null otherwise = not known from the element (a capacitor's current, an inductor's voltage).
    /// </summary>
    internal sealed record BranchDef(string PinA, string PinB)
    {
        public string? U { get; init; }
        public bool ZeroU { get; init; }
        public string? I { get; init; }
        /// <summary>Resistance key: the branch is resistive (U = R·I), so it takes part in the equivalent resistance.</summary>
        public string? R { get; init; }
        /// <summary>Capacitance and charge keys: the branch is capacitive (Q = C·U), so it takes part in the equivalent capacitance.</summary>
        public string? C { get; init; }
        public string? Q { get; init; }
        /// <summary>A source (voltage/current source, battery, generator, transformer secondary).</summary>
        public bool Source { get; init; }
    }

    /// <summary>How an element takes part in a wired circuit (<see cref="ElecNetwork"/>).</summary>
    internal sealed class NetSpec
    {
        public List<BranchDef> Branches { get; } = new();
        /// <summary>Pin groups joined inside the element (a closed contact, a junction, a busbar).</summary>
        public List<string[]> Shorts { get; } = new();
        /// <summary>Pins that carry no current (an open contact, a test point).</summary>
        public HashSet<string> Ignore { get; } = new(StringComparer.Ordinal);
        /// <summary>A name joining the first pin with every element of the same name on the page (earth symbols, net labels).</summary>
        public string? Global { get; init; }
        /// <summary>A global name joins only when it occurs at least twice on the page (a lone net label leads elsewhere).</summary>
        public bool GlobalNeedsPartner { get; init; }
        /// <summary>Single-line load (socket outlet, luminaire): voltage and current keys, at the net of its first wired pin.</summary>
        public (string U, string I)? Sink { get; init; }

        public static NetSpec Branch(BranchDef b) { var n = new NetSpec(); n.Branches.Add(b); return n; }
        public static NetSpec Short(params string[] pins) { var n = new NetSpec(); n.Shorts.Add(pins); return n; }
        public static NetSpec Open(params string[] pins) { var n = new NetSpec(); foreach (var p in pins) n.Ignore.Add(p); return n; }
    }

    /// <summary>
    /// Circuit analysis of one document for element values (Calc 3, engineering-calculations.md sections 1.6 and 10.10):
    /// pins are joined into nets by wires, cable routes and buses docked to them (as in the netlist export), by junction
    /// dots, closed contacts, earth symbols and net labels of the same name. Two-terminal elements are branches between
    /// nets. The network is reduced by series and parallel steps only (no circuit simulation):
    /// <list type="bullet">
    /// <item>series (a net joining exactly two branches and nothing else): the same current, voltages add;</item>
    /// <item>parallel (two branches between the same nets): the same voltage, currents add;</item>
    /// <item>resistive or capacitive groups: the equivalent resistance (R = ΣRᵢ, 1/R = Σ1/Rᵢ) or capacitance;</item>
    /// <item>one source (or sources in series/parallel) across the reduced load: the closed circuit (U and I of the source equal
    /// those of the load);</item>
    /// <item>single-line loads on a net fed by one branch: Kirchhoff's current law.</item>
    /// </list>
    /// What remains with a loop after these steps (a bridge, several sources in different places) is "not reducible": the
    /// elements get the hint <c>elec.net.reducible</c>. Voltages and currents are magnitudes in consumer orientation, which
    /// holds for series-parallel networks driven by one source. The relations of a connected circuit are returned by one
    /// element (the owner); every other element returns a membership relation without solvers that pulls the owner into the
    /// solver scope, so a single element's values see the whole circuit.
    /// </summary>
    internal sealed class ElecNetwork
    {
        private static readonly ConditionalWeakTable<SketchDocument, ElecNetwork> Cache = new();

        /// <summary>The analysis of <paramref name="doc"/> (computed once per document instance).</summary>
        public static ElecNetwork Of(SketchDocument doc) => Cache.GetValue(doc, d => new ElecNetwork(d));

        private readonly Dictionary<string, List<BoundRelation>> _owned = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _owner = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _componentOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _components = new(StringComparer.Ordinal);
        private readonly SketchDocument _doc;

        /// <summary>Elements in a loop that series/parallel steps cannot reduce (bridge, sources in several places).</summary>
        public HashSet<string> Irreducible { get; } = new(StringComparer.Ordinal);

        /// <summary>Relations of the circuit returned by <paramref name="id"/> (owner: all of them; member: the membership link).</summary>
        public IReadOnlyList<BoundRelation> LinksOf(string id)
            => _owned.TryGetValue(id, out var l) ? l : (IReadOnlyList<BoundRelation>)Array.Empty<BoundRelation>();

        /// <summary>The elements wired into the same circuit as <paramref name="id"/> (through any element), itself included.</summary>
        public IReadOnlyList<string> CircuitOf(string id)
            => _componentOf.TryGetValue(id, out var c) ? _components[c] : new[] { id };

        // ------------------------------------------------------------------ construction

        private sealed class Tree
        {
            public char Kind; // 'L' leaf, 'S' series, 'P' parallel, 'C' closed circuit (sources ∥ load)
            public readonly List<Tree> Kids = new();
            public Tree? Parent;
            public string? Obj;
            public BranchDef? Def;
            public bool Source;
            public List<VarRef>? UMemo, IMemo, QMemo;
            public bool UDone, IDone, QDone;
        }

        private sealed class Edge
        {
            public string A = "", B = "";
            public Tree T = null!;
            public bool Source;
            public bool Alive = true;
            /// <summary>Left out as a dangling branch (still a top-level part: it can feed single-line loads).</summary>
            public bool Pruned;
        }

        private ElecNetwork(SketchDocument doc)
        {
            _doc = doc;
            var objects = doc.Objects.Values.OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            var page = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pg in doc.Pages) foreach (var id in pg.Order) page[id] = pg.Id;

            var nets = new UnionFind();
            var lines = objects.Where(IsLine).ToList();
            if (lines.Count == 0) return; // nothing wired

            // 1. Lines: every line is a node; docked pins join it, and so does a line it branches off.
            foreach (var w in lines)
            {
                var node = "w\u0001" + w.Id;
                nets.Add(node);
                foreach (var a in w.Attach ?? Array.Empty<Attachment?>())
                {
                    if (a is null || doc.Get(a.ObjectId) is not { } target) continue;
                    if (IsLine(target)) nets.Union(node, "w\u0001" + target.Id);
                    else if (a.AnchorId is not null) nets.Union(PinKey(a.ObjectId, a.AnchorId), node);
                }
            }

            // 2. Elements: specs, shorts inside elements, global names.
            var specs = new Dictionary<string, (ElecType Type, NetSpec? Spec, List<string> Pins)>(StringComparer.Ordinal);
            var globalCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var o in objects)
            {
                if (o.IsPointGeometry || ElectricalModule.FindType(o.Type) is not ElecType et || et.Pins is null) continue;
                var spec = et.Net?.Invoke(o);
                specs[o.Id] = (et, spec, et.Pins(o).Select(p => p.Id).ToList());
                if (spec?.Global is string g)
                {
                    var key = GlobalKey(page, o.Id, g);
                    globalCount[key] = globalCount.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            }
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in specs)
            {
                var (_, spec, pins) = kv.Value;
                if (spec is null || pins.Count == 0) continue;
                foreach (var group in spec.Shorts)
                {
                    for (var i = 1; i < group.Length; i++) nets.Union(PinKey(kv.Key, group[0]), PinKey(kv.Key, group[i]));
                    foreach (var p in group) covered.Add(PinKey(kv.Key, p));
                }
                foreach (var p in spec.Ignore) covered.Add(PinKey(kv.Key, p));
                foreach (var b in spec.Branches) { covered.Add(PinKey(kv.Key, b.PinA)); covered.Add(PinKey(kv.Key, b.PinB)); }
                if (spec.Sink is not null) foreach (var p in pins) covered.Add(PinKey(kv.Key, p));
                if (spec.Global is string g)
                {
                    var key = GlobalKey(page, kv.Key, g);
                    if (!spec.GlobalNeedsPartner || globalCount[key] > 1)
                    {
                        nets.Union(PinKey(kv.Key, pins[0]), "g\u0001" + key);
                        covered.Add(PinKey(kv.Key, pins[0]));
                    }
                }
            }

            // Nets that are wired (contain a line or a global name); a pin is connected when its net is wired.
            var wired = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in nets.Keys.ToList()) if (k[0] == 'w' || k[0] == 'g') wired.Add(nets.Find(k));
            string? NetOf(string obj, string pin)
            {
                var key = PinKey(obj, pin);
                if (!nets.Contains(key)) return null;
                var root = nets.Find(key);
                return wired.Contains(root) ? root : null;
            }

            // Circuits (element level): an element joins the nets of its wired pins.
            var circuits = new UnionFind();
            foreach (var kv in specs)
                foreach (var p in kv.Value.Pins)
                    if (NetOf(kv.Key, p) is string n) circuits.Union("e\u0001" + kv.Key, "n\u0001" + n);
            foreach (var k in nets.Keys.ToList())
                if (k[0] == 'p' && NetOf(ObjOf(k), PinOf(k)) is string n && !specs.ContainsKey(ObjOf(k))) circuits.Union("e\u0001" + ObjOf(k), "n\u0001" + n);
            foreach (var k in circuits.Keys.ToList())
            {
                if (k[0] != 'e') continue;
                var id = k.Substring(2);
                var c = circuits.Find(k);
                _componentOf[id] = c;
                if (!_components.TryGetValue(c, out var l)) _components[c] = l = new List<string>();
                l.Add(id);
            }

            // 3. Branches, single-line loads and terminals of unknown current.
            var edges = new List<Edge>();
            var sinks = new Dictionary<string, List<(string Obj, string U, string I)>>(StringComparer.Ordinal);
            var foreign = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in specs)
            {
                var spec = kv.Value.Spec;
                if (spec is null) continue;
                foreach (var b in spec.Branches)
                {
                    var na = NetOf(kv.Key, b.PinA);
                    var nb = NetOf(kv.Key, b.PinB);
                    if (na is null || nb is null || na == nb) continue;
                    edges.Add(new Edge { A = na, B = nb, Source = b.Source, T = new Tree { Kind = 'L', Obj = kv.Key, Def = b, Source = b.Source } });
                }
                if (spec.Sink is { } s && kv.Value.Pins.Select(p => NetOf(kv.Key, p)).FirstOrDefault(n => n is not null) is string sn)
                {
                    if (!sinks.TryGetValue(sn, out var l)) sinks[sn] = l = new List<(string, string, string)>();
                    l.Add((kv.Key, s.U, s.I));
                }
            }
            foreach (var k in nets.Keys.ToList())
                if (k[0] == 'p' && !covered.Contains(k) && NetOf(ObjOf(k), PinOf(k)) is string n) foreign.Add(n);
            if (edges.Count == 0 && sinks.Count == 0) return;

            // 4. Reduction.
            var closed = new List<Tree>();
            Reduce(edges, sinks, foreign, closed);
            var all = new List<Tree>();
            var seen = new HashSet<Tree>();
            foreach (var e in edges) if (e.T.Parent is null && seen.Add(e.T)) all.Add(e.T);
            all.AddRange(closed);

            // 5. Relations.
            var relations = new List<BoundRelation>();
            foreach (var t in all) Emit(t, relations);
            foreach (var kv in sinks) EmitSinks(kv.Key, kv.Value, edges, foreign, relations);

            // 6. Not reducible: a loop left after reduction in a circuit of two-terminal elements only.
            MarkIrreducible(edges, sinks, foreign);

            // 7. Owners: one element per circuit returns the relations; the others link to it.
            var byOwner = new Dictionary<string, List<BoundRelation>>(StringComparer.Ordinal);
            foreach (var r in relations)
            {
                var first = r.Map.Values.Select(v => v.ObjectId).First();
                var circuit = _componentOf.TryGetValue(first, out var c) ? c : first;
                var owner = OwnerOf(circuit);
                if (!byOwner.TryGetValue(owner, out var l)) byOwner[owner] = l = new List<BoundRelation>();
                l.Add(r);
            }
            foreach (var kv in byOwner)
            {
                _owned[kv.Key] = kv.Value;
                var members = new HashSet<string>(kv.Value.SelectMany(r => r.Map.Values.Select(v => v.ObjectId)), StringComparer.Ordinal);
                var ownerKey = FirstVar(kv.Key);
                foreach (var m in members)
                {
                    if (m == kv.Key || FirstVar(m) is not string mk || ownerKey is null) continue;
                    _owner[m] = kv.Key;
                    _owned[m] = new List<BoundRelation>
                    {
                        new(Member, new Dictionary<string, VarRef> { ["a"] = new VarRef(m, mk), ["b"] = new VarRef(kv.Key, ownerKey) }),
                    };
                }
            }
        }

        private string OwnerOf(string circuit)
        {
            if (!_components.TryGetValue(circuit, out var members)) return circuit;
            // The first element (by id) of the circuit that has variables.
            foreach (var id in members.OrderBy(x => x, StringComparer.Ordinal)) if (FirstVar(id) is not null) return id;
            return members[0];
        }

        private string? FirstVar(string id)
            => _doc.Get(id) is { } o && ElectricalModule.FindType(o.Type) is ElecType { Variables.Count: > 0 } t ? t.Variables[0].Key : null;

        /// <summary>Membership in one circuit: no solver, never a check; it only pulls the circuit's owner into the scope.</summary>
        private static readonly Relation Member = new("elec.net.member", new I18nText("Same circuit", "Gleicher Stromkreis"), "{a} ~ {b}", "a", "b");

        private static bool IsLine(SketchObject o)
            => o.IsPointGeometry && (o.Type == WireType.TypeId || o.Type == "elec.house.cable" || o.Type.StartsWith("elec.bus", StringComparison.Ordinal));

        private static string PinKey(string obj, string pin) => "p\u0001" + obj + "\u0001" + pin;
        private static string ObjOf(string pinKey) => pinKey.Split('\u0001')[1];
        private static string PinOf(string pinKey) => pinKey.Split('\u0001')[2];
        private static string GlobalKey(Dictionary<string, string> page, string id, string name) => (page.TryGetValue(id, out var p) ? p : "") + "\u0001" + name;

        // ------------------------------------------------------------------ reduction

        private static void Reduce(List<Edge> edges, Dictionary<string, List<(string Obj, string U, string I)>> sinks, HashSet<string> foreign, List<Tree> closed)
        {
            bool Blocked(string n) => sinks.ContainsKey(n) || foreign.Contains(n);
            var changed = true;
            while (changed)
            {
                changed = false;
                var alive = edges.Where(e => e.Alive).ToList();
                var ends = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
                foreach (var e in alive)
                {
                    Add(ends, e.A, e);
                    Add(ends, e.B, e);
                }

                // Dangling branches (one end on a net with nothing else) carry no current: leave them out.
                foreach (var e in alive)
                    if (!Blocked(e.A) && ends[e.A].Count == 1 || !Blocked(e.B) && ends[e.B].Count == 1) { e.Alive = false; e.Pruned = true; changed = true; }
                if (changed) continue;

                // Parallel: branches of the same kind (passive / source) between the same two nets.
                foreach (var g in alive.GroupBy(e => (Min(e.A, e.B), Max(e.A, e.B), e.Source)).Where(g => g.Count() > 1))
                {
                    var list = g.ToList();
                    foreach (var e in list) e.Alive = false;
                    edges.Add(new Edge { A = list[0].A, B = list[0].B, Source = list[0].Source, T = Combine('P', list.Select(e => e.T)) });
                    changed = true;
                }
                if (changed) continue;

                // Series: a net joining exactly two branches of the same kind and nothing else.
                foreach (var kv in ends)
                {
                    var n = kv.Key;
                    var at = kv.Value;
                    if (Blocked(n) || at.Count != 2 || at[0] == at[1] || at[0].Source != at[1].Source) continue;
                    var x = at[0].A == n ? at[0].B : at[0].A;
                    var y = at[1].A == n ? at[1].B : at[1].A;
                    at[0].Alive = at[1].Alive = false;
                    var t = Combine('S', new[] { at[0].T, at[1].T });
                    if (x != y) edges.Add(new Edge { A = x, B = y, Source = at[0].Source, T = t });
                    else closed.Add(t); // a closed ring of one kind: its series relations still hold
                    changed = true;
                    break;
                }
                if (changed) continue;

                // Closed circuit: sources and one load between the same two nets, nothing else there.
                foreach (var s in alive.Where(e => e.Source))
                {
                    var loads = alive.Where(e => !e.Source && (e.A == s.A && e.B == s.B || e.A == s.B && e.B == s.A)).ToList();
                    if (loads.Count != 1 || Blocked(s.A) || Blocked(s.B) || ends[s.A].Count != 2 || ends[s.B].Count != 2) continue;
                    s.Alive = loads[0].Alive = false;
                    var c = new Tree { Kind = 'C' };
                    foreach (var k in new[] { s.T, loads[0].T }) { c.Kids.Add(k); k.Parent = c; }
                    closed.Add(c);
                    changed = true;
                    break;
                }
            }
        }

        private static Tree Combine(char kind, IEnumerable<Tree> parts)
        {
            var t = new Tree { Kind = kind };
            foreach (var p in parts)
            {
                if (p.Kind == kind) foreach (var k in p.Kids) { t.Kids.Add(k); k.Parent = t; }
                else { t.Kids.Add(p); p.Parent = t; }
            }
            t.Source = t.Kids.All(k => k.Source);
            return t;
        }

        private static void Add(Dictionary<string, List<Edge>> map, string key, Edge e)
        {
            if (!map.TryGetValue(key, out var l)) map[key] = l = new List<Edge>();
            l.Add(e);
        }

        private static string Min(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a : b;
        private static string Max(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? b : a;

        private void MarkIrreducible(List<Edge> edges, Dictionary<string, List<(string Obj, string U, string I)>> sinks, HashSet<string> foreign)
        {
            var alive = edges.Where(e => e.Alive).ToList();
            if (alive.Count == 0) return;
            var uf = new UnionFind();
            foreach (var e in alive) uf.Union(e.A, e.B);
            foreach (var g in alive.GroupBy(e => uf.Find(e.A)))
            {
                var list = g.ToList();
                var nodes = new HashSet<string>(list.SelectMany(e => new[] { e.A, e.B }), StringComparer.Ordinal);
                if (nodes.Any(n => sinks.ContainsKey(n) || foreign.Contains(n))) continue;
                if (list.Count - nodes.Count + 1 <= 0) continue;
                foreach (var e in list) foreach (var leaf in Leaves(e.T)) Irreducible.Add(leaf.Obj!);
            }
        }

        private static IEnumerable<Tree> Leaves(Tree t)
        {
            if (t.Kind == 'L') { yield return t; yield break; }
            foreach (var k in t.Kids) foreach (var l in Leaves(k)) yield return l;
        }

        // ------------------------------------------------------------------ expressions (sums of element variables)

        /// <summary>Variables whose sum is the voltage across <paramref name="t"/>; empty = zero; null = unknown.</summary>
        private static List<VarRef>? U(Tree t)
        {
            if (t.UDone) return t.UMemo;
            t.UDone = true;
            return t.UMemo = t.Kind switch
            {
                'L' => t.Def!.U is string u ? new List<VarRef> { new(t.Obj!, u) } : t.Def.ZeroU ? new List<VarRef>() : null,
                'S' => Concat(t.Kids.Select(U)),
                _ => Shortest(t.Kids.Select(U)),
            };
        }

        /// <summary>Variables whose sum is the current through <paramref name="t"/>; null = unknown.</summary>
        private static List<VarRef>? I(Tree t)
        {
            if (t.IDone) return t.IMemo;
            t.IDone = true;
            return t.IMemo = t.Kind switch
            {
                'L' => t.Def!.I is string i ? new List<VarRef> { new(t.Obj!, i) } : null,
                'P' => Concat(t.Kids.Select(I)),
                _ => Shortest(t.Kids.Select(I)),
            };
        }

        /// <summary>Variables whose sum is the charge of <paramref name="t"/> (capacitors); null = unknown.</summary>
        private static List<VarRef>? Q(Tree t)
        {
            if (t.QDone) return t.QMemo;
            t.QDone = true;
            return t.QMemo = t.Kind switch
            {
                'L' => t.Def!.Q is string q ? new List<VarRef> { new(t.Obj!, q) } : null,
                'P' => Concat(t.Kids.Select(Q)),
                _ => Shortest(t.Kids.Select(Q)),
            };
        }

        /// <summary>The shortest known voltage expression of <paramref name="t"/>, also from parallel siblings above it.</summary>
        private static List<VarRef>? UBest(Tree t)
            => t.Parent is { Kind: 'P' or 'C' } p ? Shortest(new[] { U(t), UBest(p) }) : U(t);

        private static List<VarRef>? IBest(Tree t)
            => t.Parent is { Kind: 'S' or 'C' } p ? Shortest(new[] { I(t), IBest(p) }) : I(t);

        private static List<VarRef>? QBest(Tree t)
            => t.Parent is { Kind: 'S' } p ? Shortest(new[] { Q(t), QBest(p) }) : Q(t);

        private static List<VarRef>? Concat(IEnumerable<List<VarRef>?> parts)
        {
            var list = new List<VarRef>();
            foreach (var p in parts) { if (p is null) return null; list.AddRange(p); }
            return list;
        }

        private static List<VarRef>? Shortest(IEnumerable<List<VarRef>?> parts)
        {
            List<VarRef>? best = null;
            foreach (var p in parts) if (p is not null && (best is null || p.Count < best.Count)) best = p;
            return best;
        }

        // ------------------------------------------------------------------ relations

        private static readonly I18nText KvlName = new("Kirchhoff's voltage law (parallel branches)", "Maschensatz (parallele Zweige)");
        private static readonly I18nText SeriesName = new("Series circuit: same current", "Reihenschaltung: gleicher Strom");
        private static readonly I18nText ChargeName = new("Capacitors in series: same charge", "Kondensatoren in Reihe: gleiche Ladung");
        private static readonly I18nText ClosedName = new("Closed circuit: source and load", "Geschlossener Stromkreis: Quelle und Last");
        private static readonly I18nText KclName = new("Kirchhoff's current law", "Knotensatz");
        private static readonly I18nText SinkVoltageName = new("Loads on one circuit: same voltage", "Verbraucher an einem Stromkreis: gleiche Spannung");
        private static readonly I18nText ReqName = new("Equivalent resistance (series/parallel)", "Ersatzwiderstand (Reihe/parallel)");
        private static readonly I18nText CeqName = new("Equivalent capacitance (series/parallel)", "Ersatzkapazität (Reihe/parallel)");

        private static void Emit(Tree t, List<BoundRelation> into)
        {
            if (t.Kind == 'L') return;
            foreach (var k in t.Kids) Emit(k, into);
            switch (t.Kind)
            {
                case 'P':
                    Equalities("elec.net.parallel", KvlName, t.Kids.Select(U), into);
                    break;
                case 'S':
                    Equalities("elec.net.series", SeriesName, t.Kids.Select(I), into);
                    Equalities("elec.net.charge", ChargeName, t.Kids.Select(Q), into);
                    break;
                case 'C':
                    Equalities("elec.net.closed", ClosedName, t.Kids.Select(U), into);
                    Equalities("elec.net.closed", ClosedName, t.Kids.Select(I), into);
                    break;
            }
            if (t.Kind is 'S' or 'P')
            {
                var leaves = Leaves(t).ToList();
                if (leaves.All(l => l.Def!.R is not null)) Add(EquivalentResistance(t, UBest(t), IBest(t)), into);
                if (leaves.All(l => l.Def!.C is not null)) Add(EquivalentCapacitance(t, UBest(t), QBest(t)), into);
            }
        }

        private static void EmitSinks(string net, List<(string Obj, string U, string I)> list, List<Edge> edges, HashSet<string> foreign, List<BoundRelation> into)
        {
            for (var i = 1; i < list.Count; i++)
                Add(SumEquality("elec.net.loads", SinkVoltageName, new[] { new VarRef(list[0].Obj, list[0].U) }, new[] { new VarRef(list[i].Obj, list[i].U) }), into);
            if (foreign.Contains(net)) return;
            var feeding = edges.Where(e => (e.Alive || e.Pruned) && (e.A == net || e.B == net)).ToList();
            if (feeding.Count != 1 || I(feeding[0].T) is not { Count: > 0 } feed) return;
            Add(SumEquality("elec.net.kcl", KclName, feed, list.Select(s => new VarRef(s.Obj, s.I)).ToList()), into);
        }

        private static void Equalities(string id, I18nText name, IEnumerable<List<VarRef>?> exprs, List<BoundRelation> into)
        {
            var known = exprs.Where(e => e is not null).Select(e => e!).ToList();
            if (known.Count < 2) return;
            var base0 = Shortest(known)!;
            foreach (var e in known)
                if (!ReferenceEquals(e, base0) && !(e.Count == 0 && base0.Count == 0)) Add(SumEquality(id, name, base0, e), into);
        }

        private static void Add(BoundRelation? r, List<BoundRelation> into)
        {
            if (r is null) return;
            // A variable may appear only once in a relation (otherwise the single-unknown step is undefined).
            if (r.Map.Values.Distinct().Count() != r.Map.Count) return;
            into.Add(r);
        }

        private static readonly Dictionary<(string, int, int), Relation> SumCache = new();

        /// <summary>Σ left = Σ right, solved for any one variable.</summary>
        private static BoundRelation? SumEquality(string id, I18nText name, IReadOnlyList<VarRef> left, IReadOnlyList<VarRef> right)
        {
            if (left.Count == 0 && right.Count == 0) return null;
            Relation rel;
            lock (SumCache)
                if (!SumCache.TryGetValue((id, left.Count, right.Count), out rel!))
                    SumCache[(id, left.Count, right.Count)] = rel = BuildSum(id, name, left.Count, right.Count);
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal);
            for (var i = 0; i < left.Count; i++) map["a" + i] = left[i];
            for (var i = 0; i < right.Count; i++) map["b" + i] = right[i];
            return new BoundRelation(rel, map);
        }

        private static Relation BuildSum(string id, I18nText name, int na, int nb)
        {
            var a = Enumerable.Range(0, na).Select(i => "a" + i).ToArray();
            var b = Enumerable.Range(0, nb).Select(i => "b" + i).ToArray();
            static string Sum(IEnumerable<string> keys) { var k = keys.Select(x => "{" + x + "}").ToList(); return k.Count == 0 ? "0" : string.Join(" + ", k); }
            var rel = new Relation(id, name, Sum(a) + " = " + Sum(b), a.Concat(b).ToArray()) { Source = "S7", IsLink = na == 1 && nb == 1 };
            void Solver(string key, string[] same, string[] other)
            {
                var rest = same.Where(x => x != key).ToArray();
                var expr = Sum(other) + string.Concat(rest.Select(x => " − {" + x + "}"));
                rel.Solve(key, expr, v => other.Sum(x => v(x)) - rest.Sum(x => v(x)));
            }
            foreach (var k in a) Solver(k, a, b);
            foreach (var k in b) Solver(k, b, a);
            return rel;
        }

        /// <summary>Σu = Σi · R_eq(tree): explicit for the voltage and current terms, numeric (Brent) for a resistance.</summary>
        private static BoundRelation? EquivalentResistance(Tree t, List<VarRef>? u, List<VarRef>? i)
            => Equivalent(t, u, i, "elec.net.req", ReqName, l => l.Def!.R!, inverse: false);

        /// <summary>Σq = C_eq(tree) · Σu.</summary>
        private static BoundRelation? EquivalentCapacitance(Tree t, List<VarRef>? u, List<VarRef>? q)
            => Equivalent(t, q, u, "elec.net.ceq", CeqName, l => l.Def!.C!, inverse: true);

        /// <summary>
        /// Σy = Σx · Z(tree) where Z adds in series and combines as 1/Σ(1/zᵢ) in parallel (resistance); with
        /// <paramref name="inverse"/> the other way round (capacitance: parallel adds).
        /// </summary>
        private static BoundRelation? Equivalent(Tree t, List<VarRef>? y, List<VarRef>? x, string id, I18nText name, Func<Tree, string> key, bool inverse)
        {
            if (y is null || x is null || y.Count == 0 || x.Count == 0) return null;
            var leaves = Leaves(t).ToList();
            var ys = Enumerable.Range(0, y.Count).Select(k => "y" + k).ToArray();
            var xs = Enumerable.Range(0, x.Count).Select(k => "x" + k).ToArray();
            var zs = Enumerable.Range(0, leaves.Count).Select(k => "z" + k).ToArray();
            var index = new Dictionary<Tree, string>();
            for (var k = 0; k < leaves.Count; k++) index[leaves[k]] = zs[k];

            string Text(Tree n)
            {
                if (n.Kind == 'L') return "{" + index[n] + "}";
                var adds = (n.Kind == 'S') != inverse;
                var s = string.Join(adds ? " + " : " ∥ ", n.Kids.Select(Text));
                return n.Kids.Count == 1 ? s : "(" + s + ")";
            }
            double Eval(Tree n, Func<string, double> v)
            {
                if (n.Kind == 'L') return v(index[n]);
                var adds = (n.Kind == 'S') != inverse;
                return adds ? n.Kids.Sum(k => Eval(k, v)) : 1 / n.Kids.Sum(k => 1 / Eval(k, v));
            }
            static string Sum(string[] keys) => keys.Length == 1 ? "{" + keys[0] + "}" : "(" + string.Join(" + ", keys.Select(k => "{" + k + "}")) + ")";
            var z = Text(t);
            var rel = new Relation(id, name, Sum(ys) + " = " + Sum(xs) + "·" + z, ys.Concat(xs).Concat(zs).ToArray()) { Source = "S7" };
            foreach (var k in ys)
            {
                var rest = ys.Where(q => q != k).ToArray();
                rel.Solve(k, Sum(xs) + "·" + z + string.Concat(rest.Select(q => " − {" + q + "}")),
                    v => xs.Sum(q => v(q)) * Eval(t, v) - rest.Sum(q => v(q)));
            }
            foreach (var k in xs)
            {
                var rest = xs.Where(q => q != k).ToArray();
                rel.Solve(k, Sum(ys) + "/" + z + string.Concat(rest.Select(q => " − {" + q + "}")),
                    v => ys.Sum(q => v(q)) / Eval(t, v) - rest.Sum(q => v(q)));
            }
            rel.WithResidual(v => ys.Sum(q => v(q)) - xs.Sum(q => v(q)) * Eval(t, v));
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal);
            for (var k = 0; k < y.Count; k++) map[ys[k]] = y[k];
            for (var k = 0; k < x.Count; k++) map[xs[k]] = x[k];
            for (var k = 0; k < leaves.Count; k++) map[zs[k]] = new VarRef(leaves[k].Obj!, key(leaves[k]));
            return new BoundRelation(rel, map);
        }

        /// <summary>Union-find over string keys.</summary>
        private sealed class UnionFind
        {
            private readonly Dictionary<string, string> _parent = new(StringComparer.Ordinal);
            public IEnumerable<string> Keys => _parent.Keys;
            public bool Contains(string k) => _parent.ContainsKey(k);
            public void Add(string k) { if (!_parent.ContainsKey(k)) _parent[k] = k; }

            public string Find(string k)
            {
                if (!_parent.TryGetValue(k, out var p)) { _parent[k] = k; return k; }
                if (p == k) return k;
                var root = Find(p);
                _parent[k] = root;
                return root;
            }

            public void Union(string a, string b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb) return;
                // Deterministic: the ordinal smaller key becomes the root.
                if (string.CompareOrdinal(ra, rb) < 0) _parent[rb] = ra; else _parent[ra] = rb;
            }
        }
    }
}
