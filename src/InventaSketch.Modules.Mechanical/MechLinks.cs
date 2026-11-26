using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// How mechanical elements take part in the core shaft lines (<see cref="ShaftLines"/>) and the coupling: shafts and
    /// couplings pass the torque on, bearings take the speed only, carriers, cages, gear sets and nuts stay out (gear sets
    /// dock their members on shafts instead, <see cref="GearUnits"/>); pulleys,
    /// sprockets and idlers have a pitch circle that sizes the belt or chain but does not mesh (<see cref="IPitchMesh"/>).
    /// </summary>
    internal sealed partial class MechType : IShaftPart, IPitchMesh, IGearUnit
    {
        /// <summary>Member ports of gear sets and the CVT (<see cref="GearUnits"/>); empty for every other element.</summary>
        IReadOnlyList<GearUnitPort> IGearUnit.MemberPorts(SketchObject o) => GearUnits.CorePorts(Id);

        /// <summary>The speed relations of the members (<see cref="GearUnits.SpeedRelations"/>).</summary>
        IReadOnlyList<IReadOnlyDictionary<string, double>> IGearUnit.SpeedRelations(SketchObject o) => GearUnits.SpeedRelations(this, o);

        ShaftPart IShaftPart.ShaftPart(SketchObject o)
            => MechValues.PassThrough.Contains(Id) ? ShaftPart.PassThrough
             : MechValues.IsBearing(Id) ? ShaftPart.Leaf
             : MechValues.IsCarrierLike(Id) || Id == "mech.screwdrive.nut" ? ShaftPart.Outside
             : ShaftPart.Member;

        /// <summary>Belt pulleys, chain sprockets, idlers and tensioners: their pitch circle is no gear mesh.</summary>
        private static readonly HashSet<string> StrandWheels = new(StringComparer.Ordinal) { "mech.pulley", "mech.sprocket", Drives.IdlerId, Drives.TensionerId };

        bool IPitchMesh.MeshesAtPitch(SketchObject o) => !StrandWheels.Contains(Id);
    }

    /// <summary>
    /// How the machine elements of a document hang together for values (section 1.6), read from the core coupling relations
    /// (<see cref="Coupling.Graph"/>, the same relations "select connected – moves along" follows):
    /// <list type="bullet">
    /// <item><b>Shaft lines:</b> shafts, couplings and the parts fixed on them (carried or concentric, hub not loose) turn
    /// together; bearings take the speed of their shaft but join nothing; planet carriers, cages and gear sets stay out.</item>
    /// <item><b>Meshes:</b> touching gears (spur, ring, bevel, worm and wheel), except planets on a carrier pin (Willis needs
    /// signed speeds: use the planetary set element).</item>
    /// <item><b>Strands:</b> a belt or chain and the pulleys or sprockets it runs over; a screw and the nut on it.</item>
    /// <item><b>Gear units:</b> the members of gear sets and the CVT (<see cref="GearUnits"/>) share speed and torque with
    /// the parts docked on their ports; a docked member counts as a torque part of the line. A gear meshing with the outer
    /// teeth of a set's ring gear meshes with the ring member.</item>
    /// <item><b>Rack and pinion:</b> the spur gear meshing with a rack's pitch line gives the rack its pinion values.</item>
    /// </list>
    /// Built once per document and registry (documents are immutable).
    /// </summary>
    internal sealed class MechTopology
    {
        private static readonly ConditionalWeakTable<SketchDocument, ConditionalWeakTable<Registry, MechTopology>> Cache = new();

        /// <summary>
        /// The topology of <paramref name="doc"/> (cached). A request while the document's coupling graph is being built (a
        /// linked chart solving its element from the graph's anchor pass, <see cref="Coupling.GraphPending"/>) sees an empty
        /// graph: that topology is not cached.
        /// </summary>
        public static MechTopology Of(Registry registry, SketchDocument doc)
        {
            var perDoc = Cache.GetValue(doc, _ => new ConditionalWeakTable<Registry, MechTopology>());
            if (perDoc.TryGetValue(registry, out var cached)) return cached;
            if (Coupling.GraphPending(doc)) return new MechTopology(registry, doc);
            return perDoc.GetValue(registry, r => new MechTopology(r, doc));
        }

        /// <summary>A set of parts that turn together: members with a speed, torque members and pass-through members.</summary>
        public sealed class Line
        {
            public readonly List<string> Members = new();
            /// <summary>Members that take or give torque (gears, pulleys, machines …), not shafts or couplings.</summary>
            public readonly List<string> Torque = new();
            /// <summary>Shafts and couplings with a torque variable: they carry the line's torque unchanged.</summary>
            public readonly List<string> Pass = new();
            /// <summary>Speed representative: a shaft or coupling, else a mechanical member (it links every member).</summary>
            public string Rep = "";
            /// <summary>Torque representative: a shaft or coupling with a torque, else a mechanical torque member.</summary>
            public string TorqueRep = "";
            /// <summary>
            /// Members of gear units (gear sets, CVT) docked on the line: each takes or gives torque like a torque member, so
            /// they count towards the "at most two" rule of the line torque.
            /// </summary>
            public int Ports;
            /// <summary>The torque passes along the line: at most two parts take or give it (members and docked unit members).</summary>
            public bool CarriesTorque => Torque.Count + Ports <= 2 && Torque.Count + Pass.Count >= 2;
        }

        /// <summary>
        /// One value a member of a gear unit shares with the part docked on it (or with the member of another unit): the
        /// speed or the torque. <see cref="Part"/> is a magnitude (shafts, gears); <see cref="Member"/> may be signed (set
        /// members, same sense positive), then <see cref="Signed"/> is set and the part takes its magnitude.
        /// </summary>
        public sealed record Share(VarRef Part, VarRef Member, bool Signed, bool Torque);

        private readonly Dictionary<string, List<string>> _mesh = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Line> _lineOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _leaves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _leafOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _strandParts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _strandsOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _nuts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _screwOf = new(StringComparer.Ordinal);
        private readonly Func<string, string, bool> _has;
        private readonly Func<string, string, VarDef?> _var;
        private readonly HashSet<(string Strand, string Part)> _backSide = new();
        private readonly Func<string, string, bool> _idler;
        private readonly Dictionary<string, List<Share>> _shares = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pinionOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _racksOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(string Unit, string Member)>> _docked = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(string Gear, string Unit, GearUnits.Member Member)>> _unitMeshes = new(StringComparer.Ordinal);

        private MechTopology(Registry registry, SketchDocument doc)
        {
            var vars = new Dictionary<string, Dictionary<string, VarDef>>(StringComparer.Ordinal);
            Dictionary<string, VarDef> VarsOf(string id)
            {
                if (vars.TryGetValue(id, out var s)) return s;
                var t = doc.Get(id) is { } o ? registry.Type(o.Type) : null;
                vars[id] = s = new Dictionary<string, VarDef>(StringComparer.Ordinal);
                foreach (var v in ValueModel.Variables(t)) s[v.Key] = v;
                return s;
            }
            _has = (id, key) => VarsOf(id).ContainsKey(key);
            _var = (id, key) => VarsOf(id).TryGetValue(key, out var v) ? v : null;
            string TypeOf(string id) => doc.Get(id)?.Type ?? "";

            var graph = Coupling.Graph(registry, doc, Lang.En);
            var planets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (kind, a, b) in graph)
                if (kind is LinkKind.Bonded or LinkKind.Carried && (TypeOf(a) == "mech.carrier" || TypeOf(b) == "mech.carrier"))
                    planets.Add(TypeOf(a) == "mech.carrier" ? b : a);

            // Union-find over the rigid joints of shaft lines.
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
            void Add(Dictionary<string, List<string>> map, string k, string v)
            {
                if (!map.TryGetValue(k, out var l)) map[k] = l = new List<string>();
                if (!l.Contains(v)) l.Add(v);
            }
            bool Joins(string id) => _has(id, "n") && !MechValues.IsBearing(TypeOf(id)) && TypeOf(id) != "mech.screwdrive.nut" && !MechValues.IsCarrierLike(TypeOf(id));
            var unitMeshes = new List<(string A, string B)>();

            foreach (var (kind, a, b) in graph)
            {
                string ta = TypeOf(a), tb = TypeOf(b);
                switch (kind)
                {
                    case LinkKind.Mesh:
                        if (MechValues.GearTypes.Contains(ta) && MechValues.GearTypes.Contains(tb) && !planets.Contains(a) && !planets.Contains(b))
                        {
                            Add(_mesh, a, b);
                            Add(_mesh, b, a);
                        }
                        else if (GearUnits.IsUnit(ta) != GearUnits.IsUnit(tb)) unitMeshes.Add((a, b));
                        break;
                    case LinkKind.Carried:
                    case LinkKind.Bonded:
                        {
                            if (MechValues.IsCarrierLike(ta) || MechValues.IsCarrierLike(tb) || planets.Contains(a) || planets.Contains(b)) break;
                            // Ring gears are not fixed to a concentric shaft hub (as in the gear train).
                            if (kind == LinkKind.Bonded && (ta == "mech.gear.internal" || tb == "mech.gear.internal")) break;
                            if (ta == "mech.screwdrive.screw" && tb == "mech.screwdrive.nut") { Add(_nuts, a, b); _screwOf[b] = a; break; }
                            if (tb == "mech.screwdrive.screw" && ta == "mech.screwdrive.nut") { Add(_nuts, b, a); _screwOf[a] = b; break; }
                            // Carried: a carries b; a loose hub turns freely on its shaft.
                            if (kind == LinkKind.Carried && doc.Get(b)?.Str("hub", "fixed") == "loose") break;
                            if (MechValues.IsBearing(tb) && _has(b, "n") && Joins(a)) { Add(_leaves, a, b); _leafOf[b] = a; break; }
                            if (MechValues.IsBearing(ta) && _has(a, "n") && Joins(b)) { Add(_leaves, b, a); _leafOf[a] = b; break; }
                            if (Joins(a) && Joins(b)) Union(a, b);
                            break;
                        }
                    case LinkKind.Spanned:
                        Add(_strandParts, a, b);
                        Add(_strandsOf, b, a);
                        break;
                }
            }

            // Serpentine belts and chains: the core coupling links a strand to the parts on its first two hubs; the parts on
            // its further hubs are found here (same tolerance), and parts on a hub the route marks as back side are idlers.
            var serpentines = doc.Objects.Values
                .Where(o => !o.IsPointGeometry && registry.Type(o.Type) is MechType { Spans: true } && o.View == Drives.Serpentine).ToList();
            if (serpentines.Count > 0)
            {
                var ctx = new RenderContext(doc, Lang.En).For(registry);
                List<Vec2> HubsOf(SketchObject o)
                    => registry.PageAnchors(o, ctx).Where(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Hub).Select(x => x.P).ToList();
                var parts = doc.Objects.Values
                    .Where(o => !o.IsPointGeometry && registry.Type(o.Type) is { SpansHubs: false })
                    .Select(o => (o.Id, Hubs: HubsOf(o))).Where(x => x.Hubs.Count > 0).ToList();
                foreach (var s in serpentines)
                {
                    var route = BeltRoute.Of(s);
                    var hubs = HubsOf(s);
                    for (var i = 0; i < hubs.Count; i++)
                        foreach (var (pid, ph) in parts)
                        {
                            if (!ph.Any(h => h.DistanceTo(hubs[i]) <= 0.3)) continue;
                            Add(_strandParts, s.Id, pid);
                            Add(_strandsOf, pid, s.Id);
                            if (i < route.Count && route[i].Back) _backSide.Add((s.Id, pid));
                        }
                }
            }
            _idler = (strand, part) => MechValues.IsIdler(TypeOf(part)) || _backSide.Contains((strand, part));

            // The representatives are mechanical (a shaft or coupling where there is one): only mechanical types emit these
            // links, so a representative of another module (an e-machine, a pump) would leave the other foreign members of the
            // line unlinked. Mechanical members that are no representative link the representative.
            bool Mechanical(string id) => doc.Get(id) is { } x && registry.Type(x.Type) is MechType;
            foreach (var g in parent.Keys.ToList().GroupBy(Find))
            {
                var members = g.OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (members.Count < 2) continue;
                var line = new Line();
                line.Members.AddRange(members);
                foreach (var m in members)
                {
                    if (!_has(m, "T")) continue;
                    if (MechValues.PassThrough.Contains(TypeOf(m))) line.Pass.Add(m);
                    else line.Torque.Add(m);
                }
                line.Rep = members.FirstOrDefault(m => MechValues.PassThrough.Contains(TypeOf(m))) ?? members.FirstOrDefault(Mechanical) ?? members[0];
                line.TorqueRep = line.Pass.Count > 0 ? line.Pass[0] : line.Torque.FirstOrDefault(Mechanical) ?? line.Torque.FirstOrDefault() ?? "";
                foreach (var m in members) _lineOf[m] = line;
            }

            var ctxA = new RenderContext(doc, Lang.En).For(registry);
            var anchors = new Dictionary<string, List<Anchor>>(StringComparer.Ordinal);
            List<Anchor> AnchorsOf(string id)
            {
                if (anchors.TryGetValue(id, out var l)) return l;
                var o = doc.Get(id);
                return anchors[id] = o is null || o.IsPointGeometry || registry.Type(o.Type) is null ? new List<Anchor>() : registry.PageAnchors(o, ctxA).ToList();
            }
            BuildUnits(registry, doc, graph, TypeOf, AnchorsOf);
            BuildUnitMeshes(registry, doc, unitMeshes, TypeOf, AnchorsOf);
            BuildRacks(graph, TypeOf, Add);
        }

        /// <summary>
        /// Gears meshing with a pitch circle of a gear unit that belongs to one member (the outer teeth of a ring gear,
        /// <see cref="GearUnitDocking.MeshPort"/>): the gear and that member keep z·n (and the torque ratio).
        /// </summary>
        private void BuildUnitMeshes(Registry registry, SketchDocument doc, List<(string A, string B)> meshes, Func<string, string> typeOf, Func<string, List<Anchor>> anchorsOf)
        {
            foreach (var (a, b) in meshes)
            {
                var (unit, gear) = GearUnits.IsUnit(typeOf(a)) ? (a, b) : (b, a);
                if (!MechValues.GearTypes.Contains(typeOf(gear)) || !_has(gear, "n") || !_has(gear, "z")) continue;
                if (doc.Get(unit) is not { } uo || GearUnits.PortsOf(typeOf(unit)) is not { } table) continue;
                if (GearUnitDocking.MeshPort(registry, uo, anchorsOf(unit)) is not { } port || !table.TryGetValue(port.AnchorId, out var m) || m.Teeth is null) continue;
                foreach (var id in new[] { unit, gear })
                {
                    if (!_unitMeshes.TryGetValue(id, out var l)) _unitMeshes[id] = l = new List<(string, string, GearUnits.Member)>();
                    if (!l.Contains((gear, unit, m))) l.Add((gear, unit, m));
                }
            }
        }

        /// <summary>Gears meshing with the outer teeth of a gear unit member, as (gear, unit, member), for the gear or the unit <paramref name="id"/>.</summary>
        public IReadOnlyList<(string Gear, string Unit, GearUnits.Member Member)> UnitMeshesOf(string id)
            => _unitMeshes.TryGetValue(id, out var l) ? l : (IReadOnlyList<(string, string, GearUnits.Member)>)Array.Empty<(string, string, GearUnits.Member)>();

        /// <summary>
        /// Gear units (gear sets, CVT; <see cref="GearUnits"/>): a member port docked on a part (the part's shaft axis through
        /// the port hub, or a concentric hub) shares the speed with the part's shaft line and the torque when the line carries it
        /// (the docked member counts as one part taking or giving torque). Two units docked on each other share both directly.
        /// </summary>
        private void BuildUnits(Registry registry, SketchDocument doc, IReadOnlyList<(LinkKind Kind, string A, string B)> graph, Func<string, string> typeOf, Func<string, List<Anchor>> anchorsOf)
        {
            var docked = new List<(string Unit, GearUnits.Member M, string Part, GearUnits.Member? PM)>();
            foreach (var (kind, a, b) in graph)
            {
                if (kind is not (LinkKind.Carried or LinkKind.Bonded)) continue;
                bool ua = GearUnits.IsUnit(typeOf(a)), ub = GearUnits.IsUnit(typeOf(b));
                if (!ua && !ub) continue;
                if (ua && ub)
                {
                    if (MemberAt(a, b) is { } ma && MemberAt(b, a) is { } mb) docked.Add((a, ma, b, mb));
                    continue;
                }
                var (unit, part) = ua ? (a, b) : (b, a);
                var tp = typeOf(part);
                if (!_has(part, "n") || MechValues.IsCarrierLike(tp) || tp == "mech.screwdrive.nut") continue;
                if (MemberAt(unit, part) is { } m && !docked.Any(d => d.Unit == unit && d.Part == part)) docked.Add((unit, m, part, null));
            }
            if (docked.Count == 0) return;

            // The core rule (GearUnitDocking, shared with the core shaft lines and the moving gear train): ports at the shaft
            // stubs take precedence over the centre bore; a shaft through two members locks the unit: none.
            GearUnits.Member? MemberAt(string unit, string part)
            {
                var table = GearUnits.PortsOf(typeOf(unit));
                if (table is null || doc.Get(unit) is not { } uo || doc.Get(part) is not { } po) return null;
                return GearUnitDocking.DockedPort(registry, uo, po, o => anchorsOf(o.Id)) is { } port && table.TryGetValue(port.AnchorId, out var m) ? m : null;
            }

            // Docked members count as torque parts of the part's line (or of the part alone).
            string GroupOf(string part) => LineOf(part) is { } l ? "\u0001" + l.Rep : part;
            var ports = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var d in docked)
            {
                if (d.PM is not null || MechValues.IsBearing(typeOf(d.Part))) continue;
                var g = GroupOf(d.Part);
                ports[g] = (ports.TryGetValue(g, out var c) ? c : 0) + 1;
            }
            foreach (var line in _lineOf.Values.Distinct()) line.Ports = ports.TryGetValue(GroupOf(line.Rep), out var c) ? c : 0;

            void Emit(Share s, params string[] ids)
            {
                foreach (var id in ids.Distinct(StringComparer.Ordinal))
                {
                    if (!_shares.TryGetValue(id, out var l)) _shares[id] = l = new List<Share>();
                    if (!l.Contains(s)) l.Add(s);
                }
            }
            foreach (var (unit, m, part, pm) in docked)
            {
                if (!_docked.TryGetValue(unit, out var dl)) _docked[unit] = dl = new List<(string, string)>();
                dl.Add((part, m.Name));
                if (pm is not null)
                {
                    // Unit on unit: the magnitude side (if only one is signed) is the part.
                    bool sa = GearUnits.Signed(this, unit, m.Speed), sb = GearUnits.Signed(this, part, pm.Speed);
                    var (p, q, signed) = sa && !sb ? ((part, pm.Speed), (unit, m.Speed), true) : ((unit, m.Speed), (part, pm.Speed), sb && !sa);
                    Emit(new Share(new VarRef(p.Item1, p.Item2), new VarRef(q.Item1, q.Item2), signed, false), unit, part);
                    if (m.Torque is { } ta && pm.Torque is { } tb)
                    {
                        bool ta1 = GearUnits.Signed(this, unit, ta), tb1 = GearUnits.Signed(this, part, tb);
                        var (pt, qt, st) = ta1 && !tb1 ? ((part, tb), (unit, ta), true) : ((unit, ta), (part, tb), tb1 && !ta1);
                        Emit(new Share(new VarRef(pt.Item1, pt.Item2), new VarRef(qt.Item1, qt.Item2), st, true), unit, part);
                    }
                    continue;
                }
                var line = LineOf(part);
                var speedRep = line?.Rep ?? part;
                Emit(new Share(new VarRef(speedRep, "n"), new VarRef(unit, m.Speed), GearUnits.Signed(this, unit, m.Speed), false), unit, part, speedRep);
                if (m.Torque is null || MechValues.IsBearing(typeOf(part))) continue;
                var tp = typeOf(part);
                var count = ports.TryGetValue(GroupOf(part), out var pc) ? pc : 0;
                int torque, pass;
                string target;
                if (line is null)
                {
                    var hasT = _has(part, "T");
                    pass = hasT && MechValues.PassThrough.Contains(tp) ? 1 : 0;
                    torque = hasT && pass == 0 ? 1 : 0;
                    target = hasT ? part : "";
                }
                else { torque = line.Torque.Count; pass = line.Pass.Count; target = line.TorqueRep; }
                if (target.Length == 0 || torque + count > 2 || torque + pass + count < 2) continue;
                Emit(new Share(new VarRef(target, "T"), new VarRef(unit, m.Torque), GearUnits.Signed(this, unit, m.Torque), true), unit, part, target);
            }
        }

        /// <summary>
        /// Rack and pinion: a spur gear seen along its axis whose pitch circle touches the rack's pitch line on the teeth side
        /// (within the rack's length) is its pinion. The core coupling finds that mesh from the rack's pitch line anchor
        /// (<see cref="AnchorRole.PitchLine"/>), the same pair the snapping places and the moving gear train drives; with more
        /// than one such gear the rack takes none (its pinion values are then entered on the rack).
        /// </summary>
        private void BuildRacks(IReadOnlyList<(LinkKind Kind, string A, string B)> graph, Func<string, string> typeOf, Action<Dictionary<string, List<string>>, string, string> add)
        {
            foreach (var g in graph.Where(l => l.Kind == LinkKind.Mesh && typeOf(l.B) == GearUnits.RackId).GroupBy(l => l.B))
            {
                var pinions = g.Select(l => l.A).Where(id => typeOf(id) == "mech.gear.spur" && _has(id, "n")).Distinct(StringComparer.Ordinal).ToList();
                if (pinions.Count != 1) continue;
                _pinionOf[g.Key] = pinions[0];
                add(_racksOf, pinions[0], g.Key);
            }
        }

        /// <summary>The values <paramref name="id"/> shares with gear unit members (as a unit, a docked part or a representative).</summary>
        public IReadOnlyList<Share> SharesOf(string id) => _shares.TryGetValue(id, out var l) ? l : (IReadOnlyList<Share>)Array.Empty<Share>();

        /// <summary>The parts docked on gear unit <paramref name="unit"/> with the member each is docked on.</summary>
        public IReadOnlyList<(string Part, string Member)> DockedOn(string unit) => _docked.TryGetValue(unit, out var l) ? l : (IReadOnlyList<(string, string)>)Array.Empty<(string, string)>();

        /// <summary>The pinion meshing with rack <paramref name="rack"/>, or null.</summary>
        public string? PinionOf(string rack) => _pinionOf.TryGetValue(rack, out var p) ? p : null;

        /// <summary>Racks <paramref name="gear"/> is the pinion of.</summary>
        public IReadOnlyList<string> RacksOf(string gear) => _racksOf.TryGetValue(gear, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The variable <paramref name="key"/> of <paramref name="id"/> (null if none).</summary>
        internal VarDef? Var(string id, string key) => _var(id, key);

        public bool Has(string id, string key) => _has(id, key);

        /// <summary>Gears meshing with <paramref name="id"/> (values-eligible meshes only).</summary>
        public IReadOnlyList<string> MeshPartners(string id) => _mesh.TryGetValue(id, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public Line? LineOf(string id) => _lineOf.TryGetValue(id, out var l) ? l : null;

        public IReadOnlyList<string> LeavesOf(string id) => _leaves.TryGetValue(id, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public string? LeafCarrier(string id) => _leafOf.TryGetValue(id, out var c) ? c : null;

        public IReadOnlyList<string> StrandParts(string strand) => _strandParts.TryGetValue(strand, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public IReadOnlyList<string> StrandsOf(string part) => _strandsOf.TryGetValue(part, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// <paramref name="part"/> runs in <paramref name="strand"/> as an idler (idler pulley, tensioner, or any part on a hub the
        /// serpentine route marks as back side): it takes the strand speed but no torque.
        /// </summary>
        public bool IsIdler(string strand, string part) => _idler(strand, part);

        /// <summary>Parts of <paramref name="strand"/> that take or give torque through it (not idlers) and have variable <paramref name="key"/>.</summary>
        public IReadOnlyList<string> Drivers(string strand, string key) => StrandParts(strand).Where(p => _has(p, key) && !_idler(strand, p)).ToList();

        public IReadOnlyList<string> NutsOf(string screw) => _nuts.TryGetValue(screw, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public string? ScrewOf(string nut) => _screwOf.TryGetValue(nut, out var s) ? s : null;
    }

    /// <summary>
    /// Gear units (gear sets and the CVT): elements whose members turn at different speeds, so they join no shaft line but
    /// dock each member on its own shaft. A member port is a hub point of the unit's view (its id names the member); a part
    /// whose shaft axis passes through it, or whose hub is concentric with it, is docked on that member (<see cref="MechTopology"/>).
    /// The centre hub of a unit is its bore (axial views: the sun, the large sun, the input) and yields to a port at a shaft
    /// stub that the same part also touches.
    /// </summary>
    internal static class GearUnits
    {
        public const string RackId = "mech.gear.rack";

        /// <summary>
        /// A member of a unit: its name, speed and torque variable; <see cref="Centre"/> for the centre bore; <see cref="Teeth"/>
        /// the teeth variable of a pitch circle port (outer teeth of a ring gear: a gear meshing there, <see cref="MechTopology"/>).
        /// </summary>
        internal sealed record Member(string Name, string Speed, string? Torque, bool Centre = false, string? Teeth = null);

        private static Dictionary<string, Member> Ports(params (string Anchor, Member M)[] ports)
            => ports.ToDictionary(p => p.Anchor, p => p.M, StringComparer.Ordinal);

        private static readonly Member Sun = new("sun", "ns", "Ts"), Carrier = new("carrier", "nc", "Tc"), Ring = new("ring", "nr", "Tr");
        private static readonly Member In = new("in", "n1", "T1"), Out = new("out", "n2", "T2");
        private static readonly Member RvRing = new("ring", "nr", null);

        private static readonly Dictionary<string, Dictionary<string, Member>> Table = new(StringComparer.Ordinal)
        {
            // Sun on the right shaft stub (and the bore of the axial views), carrier on the left one, ring at the outer end of
            // its bracket (its drum); the outer teeth of the ring (pitch circle "ring-teeth", parameter "outer") mesh with gears.
            ["mech.gearset.planetary"] = Ports(("hub", Sun with { Centre = true }), ("sun", Sun), ("carrier", Carrier), ("ring", Ring),
                (Gears.OuterTeethId, Ring with { Teeth = "zo" })),
            // Small sun on the left stub, large sun on the right one and in the bore of the axial view, carrier on top of the
            // carrier bar, ring at its bracket; no torques.
            ["mech.gearset.ravigneaux"] = Ports(("hub", new Member("sun2", "ns2", null, true)), ("sun1", new Member("sun1", "ns1", null)), ("sun2", new Member("sun2", "ns2", null)),
                ("carrier", new Member("carrier", "nc", null)), ("ring", RvRing), (Gears.OuterTeethId, RvRing with { Teeth = "zo" })),
            // Half shafts at the outer ends, the case at the crown wheel.
            ["mech.gearset.differential"] = Ports(("left", new Member("left", "n1", "T1")), ("right", new Member("right", "n2", "T2")), ("case", new Member("case", "nc", "Tc"))),
            // Input (wave generator, eccentric) left and in the bore, output right.
            ["mech.gearset.harmonic"] = Ports(("hub", In with { Centre = true }), ("in", In), ("out", Out)),
            ["mech.gearset.cycloidal"] = Ports(("hub", In with { Centre = true }), ("in", In), ("out", Out)),
            // Primary pulley (input) and secondary pulley (output).
            ["mech.cvt"] = Ports(("hub", In), ("hub2", Out)),
        };

        public static bool IsUnit(string typeId) => Table.ContainsKey(typeId);

        private static readonly Dictionary<string, IReadOnlyList<GearUnitPort>> Core = Table.ToDictionary(
            kv => kv.Key, kv => (IReadOnlyList<GearUnitPort>)kv.Value.Select(p => new GearUnitPort(p.Key, p.Value.Name, p.Value.Centre)).ToList(), StringComparer.Ordinal);

        /// <summary>The member ports of the unit type as the core sees them (<see cref="IGearUnit"/>), empty for other types.</summary>
        public static IReadOnlyList<GearUnitPort> CorePorts(string typeId) => Core.TryGetValue(typeId, out var p) ? p : Array.Empty<GearUnitPort>();

        /// <summary>
        /// The speed relations of the members for the moving gear train (<see cref="IGearUnit.SpeedRelations"/>), signed, same
        /// sense positive, from the entered values or the defaults of the element's variables:
        /// <list type="bullet">
        /// <item>planetary set: Willis z_s·ω_s + k·z_r·ω_r − (z_s + k·z_r)·ω_c = 0 (members sun, carrier, ring);</item>
        /// <item>Ravigneaux set: large sun z_s2·(ω_s2 − ω_c) + z_r·(ω_r − ω_c) = 0, small sun (planet pairs)
        /// z_s1·(ω_s1 − ω_c) − z_r·(ω_r − ω_c) = 0 (members sun1, sun2, ring, carrier);</item>
        /// <item>differential: ω₁ + ω₂ − 2·ω_c = 0 (members left, right, case);</item>
        /// <item>strain wave gear, cycloidal drive, CVT: ω_in − i·ω_out = 0 with the entered ratio, else from the teeth
        /// (i = −z_f/(z_c − z_f), i = −z_d/(z_p − z_d)) or the CVT's ratio parameter.</item>
        /// </list>
        /// A member whose speed is entered as 0 is held (a row with itself alone); multi-DOF sets stay unresolved without one.
        /// </summary>
        public static IReadOnlyList<IReadOnlyDictionary<string, double>> SpeedRelations(MechType type, SketchObject o)
        {
            if (!IsUnit(type.Id)) return Array.Empty<IReadOnlyDictionary<string, double>>();
            double? Entered(string key) => ValueModel.Variable(type, key) is { } v ? ValueModel.ReadEntered(o, v).Value : null;
            double Num(string key, double fallback)
                => Entered(key) is double x && !double.IsNaN(x) ? x : ValueModel.Variable(type, key)?.Default ?? fallback;
            var rows = new List<IReadOnlyDictionary<string, double>>();
            void Row(params (string Member, double K)[] terms)
            {
                var r = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (m, k) in terms) r[m] = (r.TryGetValue(m, out var x) ? x : 0) + k;
                rows.Add(r);
            }
            void Held(string member, string key)
            {
                if (Entered(key) is double x && Math.Abs(x) < 1e-12) Row((member, 1));
            }
            switch (type.Id)
            {
                case "mech.gearset.planetary":
                    {
                        double zs = Num("zs", 20), zr = Num("zr", 52), k = Num("k", 1);
                        Row(("sun", zs), ("ring", k * zr), ("carrier", -(zs + k * zr)));
                        Held("sun", "ns"); Held("carrier", "nc"); Held("ring", "nr");
                        break;
                    }
                case "mech.gearset.ravigneaux":
                    {
                        double zs1 = Num("zs1", 14), zs2 = Num("zs2", 20), zr = Num("zr", 52);
                        Row(("sun2", zs2), ("ring", zr), ("carrier", -(zs2 + zr)));
                        Row(("sun1", zs1), ("ring", -zr), ("carrier", zr - zs1));
                        Held("sun1", "ns1"); Held("sun2", "ns2"); Held("ring", "nr"); Held("carrier", "nc");
                        break;
                    }
                case "mech.gearset.differential":
                    Row(("left", 1), ("right", 1), ("case", -2));
                    Held("left", "n1"); Held("right", "n2"); Held("case", "nc");
                    break;
                default:
                    {
                        // Fixed-ratio units: ω_in = i·ω_out.
                        double Teeth()
                        {
                            if (type.Id == "mech.gearset.harmonic") { double zf = Num("zf", 200), zc = Num("zc", 202); return -zf / (zc - zf); }
                            if (type.Id == "mech.gearset.cycloidal") { var zd = Num("zd", 11); var zp = Entered("zp") ?? zd + 1; return -zd / (zp - zd); }
                            return Num("i", 1);
                        }
                        var i = Entered("i") is double e && !double.IsNaN(e) && Math.Abs(e) > 1e-12 ? e : Teeth();
                        if (double.IsNaN(i) || double.IsInfinity(i) || Math.Abs(i) < 1e-12) break;
                        Row(("in", 1), ("out", -i));
                        Held("in", "n1"); Held("out", "n2");
                        break;
                    }
            }
            return rows;
        }

        /// <summary>Port anchor id → member of the unit type, or null for no unit.</summary>
        public static IReadOnlyDictionary<string, Member>? PortsOf(string typeId) => Table.TryGetValue(typeId, out var p) ? p : null;

        /// <summary>True if variable <paramref name="key"/> of <paramref name="id"/> is signed (its range reaches below 0).</summary>
        internal static bool Signed(MechTopology topo, string id, string key) => topo.Var(id, key) is { Min: < 0 };
    }

    /// <summary>
    /// Port relations of machine elements (Calc 3, section 1.6): shaft lines share n (and the torque when it cannot split),
    /// meshes keep z·n and pass the torque with the stage efficiencies, belts and chains run at one speed and pull, the nut
    /// follows the screw. Every element returns the links it takes part in, so a selection pulls its whole train into the
    /// solver scope (a link found from both ends is harmless: the second copy is no independent check).
    /// </summary>
    internal static class MechLinks
    {
        private static I18nText T(string en, string de) => new(en, de);

        private const double TwoPi = 2 * Math.PI;

        private static readonly Relation SameSpeed = Relation.Equality("mech.shaft.speed", T("Same speed on one shaft", "Gleiche Drehzahl auf einer Welle"));
        private static readonly Relation SameTorque = Relation.Equality("mech.shaft.torque", T("Torque passes along the shaft", "Drehmoment wird über die Welle weitergeleitet"));
        private static readonly Relation SameLinSpeed = Relation.Equality("mech.screw.nut.speed", T("The nut moves with the screw", "Die Mutter bewegt sich mit der Spindel"));
        private static readonly Relation SameForce = Relation.Equality("mech.screw.nut.force", T("Axial force on screw and nut", "Axialkraft an Spindel und Mutter"));

        /// <summary>Mesh: z_a·n_a = z_b·n_b (speeds as magnitudes) [S18].</summary>
        internal static readonly Relation MeshSpeed =
            new Relation("mech.mesh.speed", T("Gear ratio", "Übersetzung"), "{na}·{za} = {nb}·{zb}", "na", "za", "nb", "zb") { Source = "S18" }
                .Solve("nb", "{na}·{za}/{zb}", v => v("na") * v("za") / v("zb"))
                .Solve("na", "{nb}·{zb}/{za}", v => v("nb") * v("zb") / v("za"))
                .Solve("zb", "{na}·{za}/{nb}", v => v("na") * v("za") / v("nb"))
                .Solve("za", "{nb}·{zb}/{na}", v => v("nb") * v("zb") / v("na"));

        /// <summary>Mesh torque: T_b = T_a·(z_b/z_a)·(η_b/η_a), i.e. P_b/η_b = P_a/η_a (η of the driven gear) [S18].</summary>
        internal static readonly Relation MeshTorque =
            new Relation("mech.mesh.torque", T("Torque across a mesh", "Drehmoment über einen Eingriff"), "{Tb} = {Ta}·({zb}/{za})·({eb}/{ea})", "Tb", "Ta", "zb", "za", "eb", "ea") { Source = "S18" }
                .Solve("Tb", "{Ta}·{zb}/{za}·{eb}/{ea}", v => v("Ta") * v("zb") / v("za") * v("eb") / v("ea"))
                .Solve("Ta", "{Tb}·{za}/{zb}·{ea}/{eb}", v => v("Tb") * v("za") / v("zb") * v("ea") / v("eb"))
                .Solve("eb", "{ea}·{Tb}·{za}/({Ta}·{zb})", v => v("ea") * v("Tb") * v("za") / (v("Ta") * v("zb")))
                .Solve("ea", "{eb}·{Ta}·{zb}/({Tb}·{za})", v => v("eb") * v("Ta") * v("zb") / (v("Tb") * v("za")));

        /// <summary>Chain over two sprockets without a pitch: T_b·z_a = T_a·z_b [S18].</summary>
        private static readonly Relation ChainTorque =
            new Relation("mech.chain.torque.ratio", T("Torque ratio of a chain drive", "Momentenverhältnis des Kettentriebs"), "{Tb}·{za} = {Ta}·{zb}", "Tb", "Ta", "zb", "za") { Source = "S18" }
                .Solve("Tb", "{Ta}·{zb}/{za}", v => v("Ta") * v("zb") / v("za"))
                .Solve("Ta", "{Tb}·{za}/{zb}", v => v("Tb") * v("za") / v("zb"));

        /// <summary>Belt speed v = π·d·n (n in turns per time; d/2·n with n in rad/s) [S18].</summary>
        private static readonly Relation BeltSpeed =
            new Relation("mech.belt.speed", T("Belt speed", "Riemengeschwindigkeit"), "{v} = π·{d}·{n}", "v", "d", "n") { Source = "S18" }
                .Solve("v", "π·{d}·{n}", v => v("d") / 2 * v("n"))
                .Solve("n", "{v}/(π·{d})", v => 2 * v("v") / v("d"))
                .Solve("d", "{v}/(π·{n})", v => 2 * v("v") / v("n"));

        /// <summary>Pulley torque T = F_t·d/2 [S18].</summary>
        private static readonly Relation BeltTorque =
            new Relation("mech.belt.torque", T("Torque from the effective pull", "Drehmoment aus der Umfangskraft"), "{T} = {F}·{d}/2", "T", "F", "d") { Source = "S18" }
                .Solve("T", "{F}·{d}/2", v => v("F") * v("d") / 2)
                .Solve("F", "2·{T}/{d}", v => 2 * v("T") / v("d"))
                .Solve("d", "2·{T}/{F}", v => 2 * v("T") / v("F"));

        /// <summary>Mean chain speed v = z·p·n (n in turns per time) [S18, S20].</summary>
        private static readonly Relation ChainSpeed =
            new Relation("mech.chain.speed", T("Mean chain speed", "Mittlere Kettengeschwindigkeit"), "{v} = {z}·{p}·{n}", "v", "z", "p", "n") { Source = "S18" }
                .Solve("v", "{z}·{p}·{n}", v => v("z") * v("p") * v("n") / TwoPi)
                .Solve("n", "{v}/({z}·{p})", v => v("v") * TwoPi / (v("z") * v("p")))
                .Solve("p", "{v}/({z}·{n})", v => v("v") * TwoPi / (v("z") * v("n")))
                .Solve("z", "{v}/({p}·{n})", v => v("v") * TwoPi / (v("p") * v("n")));

        /// <summary>Sprocket torque from the mean chain pull T = F·z·p/(2π) (P = F·v = T·n) [S18].</summary>
        private static readonly Relation ChainForce =
            new Relation("mech.chain.force", T("Torque from the chain pull", "Drehmoment aus der Kettenzugkraft"), "{T} = {F}·{z}·{p}/(2π)", "T", "F", "z", "p") { Source = "S18" }
                .Solve("T", "{F}·{z}·{p}/(2π)", v => v("F") * v("z") * v("p") / TwoPi)
                .Solve("F", "2π·{T}/({z}·{p})", v => TwoPi * v("T") / (v("z") * v("p")));

        private static BoundRelation Bind(Relation r, params (string Key, string Obj, string Var)[] map)
            => new(r, map.ToDictionary(m => m.Key, m => new VarRef(m.Obj, m.Var), StringComparer.Ordinal));

        private static BoundRelation Eq(Relation r, string a, string b, string key) => Bind(r, ("a", a, key), ("b", b, key));

        /// <summary>The port relations element <paramref name="o"/> takes part in.</summary>
        public static IEnumerable<BoundRelation> For(MechType type, SketchObject o, RenderContext ctx)
        {
            var registry = ctx.Registry ?? type.Owner;
            if (registry is null) return Array.Empty<BoundRelation>();
            var topo = MechTopology.Of(registry, ctx.Doc);
            var list = new List<BoundRelation>();
            var id = o.Id;

            // Shaft line: one speed (star around the representative); the torque when at most two members take or give it.
            if (topo.LineOf(id) is { } line)
            {
                if (id == line.Rep) { foreach (var m in line.Members) if (m != id) list.Add(Eq(SameSpeed, id, m, "n")); }
                else list.Add(Eq(SameSpeed, line.Rep, id, "n"));
                if (line.CarriesTorque && (line.Torque.Contains(id) || line.Pass.Contains(id)))
                {
                    var rep = line.TorqueRep;
                    if (id == rep) { foreach (var m in line.Torque.Concat(line.Pass)) if (m != id) list.Add(Eq(SameTorque, id, m, "T")); }
                    else list.Add(Eq(SameTorque, rep, id, "T"));
                }
            }
            foreach (var leaf in topo.LeavesOf(id)) list.Add(Eq(SameSpeed, id, leaf, "n"));
            if (topo.LeafCarrier(id) is { } carrier) list.Add(Eq(SameSpeed, carrier, id, "n"));

            // Meshes.
            foreach (var x in topo.MeshPartners(id))
            {
                list.Add(Bind(MeshSpeed, ("na", id, "n"), ("za", id, "z"), ("nb", x, "n"), ("zb", x, "z")));
                list.Add(Bind(MeshTorque, ("Ta", id, "T"), ("za", id, "z"), ("ea", id, "eta"), ("Tb", x, "T"), ("zb", x, "z"), ("eb", x, "eta")));
            }

            // Belts and chains: the strand and the parts it runs over.
            if (type.Spans) foreach (var p in topo.StrandParts(id)) list.AddRange(Strand(topo, ctx.Doc, id, p));
            foreach (var s in topo.StrandsOf(id)) list.AddRange(Strand(topo, ctx.Doc, s, id));

            // Screw and nut.
            foreach (var nut in topo.NutsOf(id)) { list.Add(Eq(SameLinSpeed, id, nut, "v")); list.Add(Eq(SameForce, id, nut, "F")); }
            if (topo.ScrewOf(id) is { } screw) { list.Add(Eq(SameLinSpeed, screw, id, "v")); list.Add(Eq(SameForce, screw, id, "F")); }

            // Gear units: the members share speed and torque with the parts docked on them.
            foreach (var s in topo.SharesOf(id))
            {
                var rel = s.Torque ? (s.Signed ? UnitTorqueMagnitude : UnitTorque) : (s.Signed ? UnitSpeedMagnitude : UnitSpeed);
                list.Add(s.Signed
                    ? Bind(rel, ("x", s.Part.ObjectId, s.Part.Key), ("m", s.Member.ObjectId, s.Member.Key))
                    : Bind(rel, ("a", s.Part.ObjectId, s.Part.Key), ("b", s.Member.ObjectId, s.Member.Key)));
            }

            // A gear on the outer teeth of a set's member (ring gear): z·n and the torque ratio with the signed member.
            foreach (var (gear, unit, m) in topo.UnitMeshesOf(id))
            {
                list.Add(Bind(UnitMeshSpeed, ("ng", gear, "n"), ("zg", gear, "z"), ("zo", unit, m.Teeth!), ("nm", unit, m.Speed)));
                if (m.Torque is { } tm && topo.Has(gear, "T"))
                    list.Add(Bind(UnitMeshTorque, ("Tg", gear, "T"), ("zg", gear, "z"), ("zo", unit, m.Teeth!), ("Tm", unit, tm)));
            }

            // Rack and pinion: the rack's pinion values are those of the meshing gear.
            if (topo.PinionOf(id) is { } pinion) list.AddRange(RackMesh(id, pinion));
            foreach (var rack in topo.RacksOf(id)) list.AddRange(RackMesh(rack, id));
            return list;
        }

        private static readonly Relation UnitSpeed = Relation.Equality("mech.unit.speed", T("Same speed at a member of the gear unit", "Gleiche Drehzahl an einem Glied der Getriebeeinheit"));
        private static readonly Relation UnitTorque = Relation.Equality("mech.unit.torque", T("Torque passes into a member of the gear unit", "Drehmoment geht in ein Glied der Getriebeeinheit"));

        /// <summary>
        /// A shaft at a member of a gear set with signed member speeds: n = |n_m| (shaft speeds are magnitudes); a member driven
        /// from its shaft turns in the positive sense. It passes the value on (only the sign is dropped), so a solved value
        /// shows as linked, like the plain equalities of the other port relations.
        /// </summary>
        private static readonly Relation UnitSpeedMagnitude =
            new Relation("mech.unit.speed.magnitude", T("Shaft speed at a member of the set (magnitude)", "Wellendrehzahl an einem Glied des Satzes (Betrag)"), "{x} = |{m}|", "x", "m")
            {
                IsLink = true,
                Validity = T("shaft speeds are magnitudes; a member driven from its shaft turns in the positive sense",
                             "Wellendrehzahlen sind Beträge; ein von seiner Welle angetriebenes Glied dreht im positiven Sinn"),
            }
                .Solve("x", "|{m}|", v => Math.Abs(v("m")))
                .Solve("m", "{x}", v => v("x"));

        /// <summary>A shaft at a member of a gear set with signed member torques: T = |T_m| (shaft torques are magnitudes); linked as the speed.</summary>
        private static readonly Relation UnitTorqueMagnitude =
            new Relation("mech.unit.torque.magnitude", T("Shaft torque at a member of the set (magnitude)", "Wellenmoment an einem Glied des Satzes (Betrag)"), "{x} = |{m}|", "x", "m")
            {
                IsLink = true,
                Validity = T("shaft torques are magnitudes; a member driven from its shaft takes the positive sense",
                             "Wellenmomente sind Beträge; ein von seiner Welle angetriebenes Glied nimmt den positiven Sinn an"),
            }
                .Solve("x", "|{m}|", v => Math.Abs(v("m")))
                .Solve("m", "{x}", v => v("x"));

        /// <summary>
        /// A gear meshing with the outer teeth of a member of a gear set (ring gear): z_g·n_g = z_o·|n_m| [S18]. The gear's speed
        /// is a magnitude, the member's signed: a member driven by the gear takes the positive sense.
        /// </summary>
        private static readonly Relation UnitMeshSpeed =
            new Relation("mech.unit.mesh.speed", T("Gear on the outer teeth of the set", "Rad an der Außenverzahnung des Satzes"), "{ng}·{zg} = {zo}·|{nm}|", "ng", "zg", "zo", "nm")
            {
                Source = "S18",
                Validity = T("the gear's speed is a magnitude; a member driven by the gear turns in the positive sense",
                             "die Drehzahl des Rads ist ein Betrag; ein vom Rad angetriebenes Glied dreht im positiven Sinn"),
            }
                .Solve("ng", "{zo}·|{nm}|/{zg}", v => v("zo") * Math.Abs(v("nm")) / v("zg"))
                .Solve("nm", "{ng}·{zg}/{zo}", v => v("ng") * v("zg") / v("zo"))
                .Solve("zg", "{zo}·|{nm}|/{ng}", v => v("zo") * Math.Abs(v("nm")) / v("ng"))
                .Solve("zo", "{ng}·{zg}/|{nm}|", v => v("ng") * v("zg") / Math.Abs(v("nm")));

        /// <summary>Torque across the outer teeth of a set's member, lossless: T_g·z_o = |T_m|·z_g [S18].</summary>
        private static readonly Relation UnitMeshTorque =
            new Relation("mech.unit.mesh.torque", T("Torque across the outer teeth of the set", "Drehmoment über die Außenverzahnung des Satzes"), "{Tg}·{zo} = |{Tm}|·{zg}", "Tg", "zg", "zo", "Tm")
            {
                Source = "S18",
                Validity = T("lossless mesh; a member driven by the gear takes the positive sense",
                             "verlustfreier Eingriff; ein vom Rad angetriebenes Glied nimmt den positiven Sinn an"),
            }
                .Solve("Tg", "|{Tm}|·{zg}/{zo}", v => Math.Abs(v("Tm")) * v("zg") / v("zo"))
                .Solve("Tm", "{Tg}·{zo}/{zg}", v => v("Tg") * v("zo") / v("zg"));

        private static readonly Relation RackPinionSpeed = Relation.Equality("mech.rack.mesh.speed", T("The rack's pinion is the meshing gear: speed", "Das Ritzel der Zahnstange ist das kämmende Rad: Drehzahl"));
        private static readonly Relation RackPinionTorque = Relation.Equality("mech.rack.mesh.torque", T("The rack's pinion is the meshing gear: torque", "Das Ritzel der Zahnstange ist das kämmende Rad: Drehmoment"));
        private static readonly Relation RackPinionTeeth = Relation.Equality("mech.rack.mesh.teeth", T("The rack's pinion is the meshing gear: teeth", "Das Ritzel der Zahnstange ist das kämmende Rad: Zähnezahl"));
        private static readonly Relation RackPinionDiameter = Relation.Equality("mech.rack.mesh.diameter", T("The rack's pinion is the meshing gear: pitch diameter", "Das Ritzel der Zahnstange ist das kämmende Rad: Teilkreisdurchmesser"));

        /// <summary>Rack <paramref name="rack"/> and its pinion gear: n_p, T_p, z_p and d_p of the rack are the gear's n, T, z and d (v = ω·r on the rack).</summary>
        private static IEnumerable<BoundRelation> RackMesh(string rack, string pinion)
        {
            yield return Bind(RackPinionSpeed, ("a", pinion, "n"), ("b", rack, "np"));
            yield return Bind(RackPinionTorque, ("a", pinion, "T"), ("b", rack, "Tp"));
            yield return Bind(RackPinionTeeth, ("a", pinion, "z"), ("b", rack, "zp"));
            yield return Bind(RackPinionDiameter, ("a", pinion, "d"), ("b", rack, "dp"));
        }

        private static IEnumerable<BoundRelation> Strand(MechTopology topo, SketchDocument doc, string strand, string part)
        {
            var kind = doc.Get(strand)?.Type;
            // Idlers take the strand speed only. One effective pull F stands for the whole strand only with at most two parts
            // taking or giving torque (a driving and a driven one); with more, the pulls differ per strand section and no torque
            // relation is set (as at a shaft branch).
            var idler = topo.IsIdler(strand, part);
            if (kind == "mech.belt" && topo.Has(part, "d"))
            {
                yield return Bind(BeltSpeed, ("v", strand, "v"), ("d", part, "d"), ("n", part, "n"));
                if (!idler && topo.Drivers(strand, "d").Count <= 2)
                    yield return Bind(BeltTorque, ("T", part, "T"), ("F", strand, "F"), ("d", part, "d"));
            }
            else if (kind == "mech.chain" && topo.Has(part, "z"))
            {
                yield return Bind(ChainSpeed, ("v", strand, "v"), ("z", part, "z"), ("p", strand, "p"), ("n", part, "n"));
                var ends = topo.Drivers(strand, "z");
                if (!idler && ends.Count <= 2)
                    yield return Bind(ChainForce, ("T", part, "T"), ("F", strand, "F"), ("z", part, "z"), ("p", strand, "p"));
                // Without a pitch the two sprockets still keep z·n and the torque ratio.
                if (!idler && ends.Count == 2)
                {
                    var other = ends[0] == part ? ends[1] : ends[0];
                    yield return Bind(MeshSpeed, ("na", part, "n"), ("za", part, "z"), ("nb", other, "n"), ("zb", other, "z"));
                    yield return Bind(ChainTorque, ("Ta", part, "T"), ("za", part, "z"), ("Tb", other, "T"), ("zb", other, "z"));
                }
            }
        }
    }
}
