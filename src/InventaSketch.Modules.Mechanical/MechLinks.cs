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
    internal sealed partial class MechType : IShaftPart, IPitchMesh
    {
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
    /// the parts docked on their ports; a docked member counts as a torque part of the line.</item>
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
            BuildUnits(graph, TypeOf, AnchorsOf);
            BuildRacks(registry, doc, AnchorsOf, Add);
        }

        /// <summary>Docking tolerance of a port hub on a shaft axis or another hub (as the core coupling).</summary>
        private const double PortTol = 0.3;

        /// <summary>
        /// Gear units (gear sets, CVT; <see cref="GearUnits"/>): a member port docked on a part (the part's shaft axis through
        /// the port hub, or a concentric hub) shares the speed with the part's shaft line and the torque when the line carries it
        /// (the docked member counts as one part taking or giving torque). Two units docked on each other share both directly.
        /// </summary>
        private void BuildUnits(IReadOnlyList<(LinkKind Kind, string A, string B)> graph, Func<string, string> typeOf, Func<string, List<Anchor>> anchorsOf)
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

            GearUnits.Member? MemberAt(string unit, string part)
            {
                var table = GearUnits.PortsOf(typeOf(unit));
                if (table is null) return null;
                var pa = anchorsOf(part);
                bool On(Vec2 h) => pa.Any(x => x.Kind == AnchorKind.Segment && x.Role == AnchorRole.Axis && Geo.ClosestOnSegment(h, x.P, x.Q).Point.DistanceTo(h) <= PortTol)
                                   || pa.Any(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Hub && x.P.DistanceTo(h) <= PortTol);
                var hits = anchorsOf(unit)
                    .Where(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Hub && x.Id is { } pid && table.ContainsKey(pid) && On(x.P))
                    .Select(x => table[x.Id!]).ToList();
                // Ports at the shaft stubs take precedence over the centre bore; a shaft through two members locks the unit: none.
                var chosen = hits.Any(h => !h.Centre) ? hits.Where(h => !h.Centre).ToList() : hits;
                return chosen.Select(h => h.Name).Distinct(StringComparer.Ordinal).Count() == 1 ? chosen[0] : null;
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
        /// (within the rack's length) is its pinion; with more than one such gear the rack takes none (its pinion values are
        /// then entered on the rack).
        /// </summary>
        private void BuildRacks(Registry registry, SketchDocument doc, Func<string, List<Anchor>> anchorsOf, Action<Dictionary<string, List<string>>, string, string> add)
        {
            var racks = doc.Objects.Values.Where(o => !o.IsPointGeometry && o.Type == GearUnits.RackId && registry.Type(o.Type) is MechType).ToList();
            if (racks.Count == 0) return;
            var gears = doc.Objects.Values
                .Where(o => !o.IsPointGeometry && o.Type == "mech.gear.spur" && _has(o.Id, "n"))
                .Select(o => (o.Id, Pitch: anchorsOf(o.Id).FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch && !a.NoMesh && a.AxisOf() is null)))
                .Where(x => x.Pitch is not null).ToList();
            if (gears.Count == 0) return;
            foreach (var rack in racks)
            {
                var view = ((MechType)registry.Type(rack.Type)!).Spec(rack).Id;
                var y = Gears.RackPitchY(rack.Size.H, view);
                var mx = rack.Matrix();
                Vec2 a = mx.Apply(new Vec2(-rack.Size.W / 2, y)), b = mx.Apply(new Vec2(rack.Size.W / 2, y));
                var tip = mx.Apply(new Vec2(0, y - 1)) - mx.Apply(new Vec2(0, y));
                var len = a.DistanceTo(b);
                if (len < Geo.Eps || tip.Length < Geo.Eps) continue;
                Vec2 u = (b - a) * (1 / len), n = tip * (1 / tip.Length);
                var pinions = gears.Where(g =>
                {
                    var c = g.Pitch!.P - a;
                    var r = g.Pitch.Radius;
                    var t = c.Dot(u);
                    return t >= 0 && t <= len && Math.Abs(c.Dot(n) - r) <= Math.Max(PortTol, 0.03 * r) && !Coupling.IsReleased(doc, rack.Id, g.Id);
                }).Select(g => g.Id).ToList();
                if (pinions.Count != 1) continue;
                _pinionOf[rack.Id] = pinions[0];
                add(_racksOf, pinions[0], rack.Id);
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

        /// <summary>A member of a unit: its name, speed and torque variable; <see cref="Centre"/> for the centre bore.</summary>
        internal sealed record Member(string Name, string Speed, string? Torque, bool Centre = false);

        private static Dictionary<string, Member> Ports(params (string Anchor, Member M)[] ports)
            => ports.ToDictionary(p => p.Anchor, p => p.M, StringComparer.Ordinal);

        private static readonly Member Sun = new("sun", "ns", "Ts"), Carrier = new("carrier", "nc", "Tc");
        private static readonly Member In = new("in", "n1", "T1"), Out = new("out", "n2", "T2");

        private static readonly Dictionary<string, Dictionary<string, Member>> Table = new(StringComparer.Ordinal)
        {
            // Sun on the right shaft stub (and the bore of the axial views), carrier on the left one.
            ["mech.gearset.planetary"] = Ports(("hub", Sun with { Centre = true }), ("sun", Sun), ("carrier", Carrier)),
            // Small sun on the left stub, large sun on the right one and in the bore of the axial view; no torques.
            ["mech.gearset.ravigneaux"] = Ports(("hub", new Member("sun2", "ns2", null, true)), ("sun1", new Member("sun1", "ns1", null)), ("sun2", new Member("sun2", "ns2", null))),
            // Half shafts at the outer ends.
            ["mech.gearset.differential"] = Ports(("left", new Member("left", "n1", "T1")), ("right", new Member("right", "n2", "T2"))),
            // Input (wave generator, eccentric) left and in the bore, output right.
            ["mech.gearset.harmonic"] = Ports(("hub", In with { Centre = true }), ("in", In), ("out", Out)),
            ["mech.gearset.cycloidal"] = Ports(("hub", In with { Centre = true }), ("in", In), ("out", Out)),
            // Primary pulley (input) and secondary pulley (output).
            ["mech.cvt"] = Ports(("hub", In), ("hub2", Out)),
        };

        public static bool IsUnit(string typeId) => Table.ContainsKey(typeId);

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

            // Rack and pinion: the rack's pinion values are those of the meshing gear.
            if (topo.PinionOf(id) is { } pinion) list.AddRange(RackMesh(id, pinion));
            foreach (var rack in topo.RacksOf(id)) list.AddRange(RackMesh(rack, id));
            return list;
        }

        private static readonly Relation UnitSpeed = Relation.Equality("mech.unit.speed", T("Same speed at a member of the gear unit", "Gleiche Drehzahl an einem Glied der Getriebeeinheit"));
        private static readonly Relation UnitTorque = Relation.Equality("mech.unit.torque", T("Torque passes into a member of the gear unit", "Drehmoment geht in ein Glied der Getriebeeinheit"));

        /// <summary>
        /// A shaft at a member of a gear set with signed member speeds: n = |n_m| (shaft speeds are magnitudes); a member driven
        /// from its shaft turns in the positive sense.
        /// </summary>
        private static readonly Relation UnitSpeedMagnitude =
            new Relation("mech.unit.speed.magnitude", T("Shaft speed at a member of the set (magnitude)", "Wellendrehzahl an einem Glied des Satzes (Betrag)"), "{x} = |{m}|", "x", "m")
            {
                Validity = T("shaft speeds are magnitudes; a member driven from its shaft turns in the positive sense",
                             "Wellendrehzahlen sind Beträge; ein von seiner Welle angetriebenes Glied dreht im positiven Sinn"),
            }
                .Solve("x", "|{m}|", v => Math.Abs(v("m")))
                .Solve("m", "{x}", v => v("x"));

        /// <summary>A shaft at a member of a gear set with signed member torques: T = |T_m| (shaft torques are magnitudes).</summary>
        private static readonly Relation UnitTorqueMagnitude =
            new Relation("mech.unit.torque.magnitude", T("Shaft torque at a member of the set (magnitude)", "Wellenmoment an einem Glied des Satzes (Betrag)"), "{x} = |{m}|", "x", "m")
            {
                Validity = T("shaft torques are magnitudes; a member driven from its shaft takes the positive sense",
                             "Wellenmomente sind Beträge; ein von seiner Welle angetriebenes Glied nimmt den positiven Sinn an"),
            }
                .Solve("x", "|{m}|", v => Math.Abs(v("m")))
                .Solve("m", "{x}", v => v("x"));

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
