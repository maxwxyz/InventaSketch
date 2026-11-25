using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// How the machine elements of a document hang together for values (section 1.6), read from the core coupling relations
    /// (<see cref="Coupling.Graph"/>, the same relations "select connected – moves along" follows):
    /// <list type="bullet">
    /// <item><b>Shaft lines:</b> shafts, couplings and the parts fixed on them (carried or concentric, hub not loose) turn
    /// together; bearings take the speed of their shaft but join nothing; planet carriers, cages and gear sets stay out.</item>
    /// <item><b>Meshes:</b> touching gears (spur, ring, bevel, worm and wheel), except planets on a carrier pin (Willis needs
    /// signed speeds: use the planetary set element).</item>
    /// <item><b>Strands:</b> a belt or chain and the pulleys or sprockets it runs over; a screw and the nut on it.</item>
    /// </list>
    /// Built once per document and registry (documents are immutable).
    /// </summary>
    internal sealed class MechTopology
    {
        private static readonly ConditionalWeakTable<SketchDocument, ConditionalWeakTable<Registry, MechTopology>> Cache = new();

        /// <summary>The topology of <paramref name="doc"/> (cached).</summary>
        public static MechTopology Of(Registry registry, SketchDocument doc)
            => Cache.GetValue(doc, _ => new ConditionalWeakTable<Registry, MechTopology>()).GetValue(registry, r => new MechTopology(r, doc));

        /// <summary>A set of parts that turn together: members with a speed, torque members and pass-through members.</summary>
        public sealed class Line
        {
            public readonly List<string> Members = new();
            /// <summary>Members that take or give torque (gears, pulleys, machines …), not shafts or couplings.</summary>
            public readonly List<string> Torque = new();
            /// <summary>Shafts and couplings with a torque variable: they carry the line's torque unchanged.</summary>
            public readonly List<string> Pass = new();
            public string Rep = "";
        }

        private readonly Dictionary<string, List<string>> _mesh = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Line> _lineOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _leaves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _leafOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _strandParts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _strandsOf = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _nuts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _screwOf = new(StringComparer.Ordinal);
        private readonly Func<string, string, bool> _has;

        private MechTopology(Registry registry, SketchDocument doc)
        {
            var vars = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            HashSet<string> VarsOf(string id)
            {
                if (vars.TryGetValue(id, out var s)) return s;
                var t = doc.Get(id) is { } o ? registry.Type(o.Type) : null;
                vars[id] = s = new HashSet<string>(ValueModel.Variables(t).Select(v => v.Key), StringComparer.Ordinal);
                return s;
            }
            _has = (id, key) => VarsOf(id).Contains(key);
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

            foreach (var g in parent.Keys.ToList().GroupBy(Find))
            {
                var members = g.OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (members.Count < 2) continue;
                var line = new Line { Rep = members[0] };
                line.Members.AddRange(members);
                foreach (var m in members)
                {
                    if (!_has(m, "T")) continue;
                    if (MechValues.PassThrough.Contains(TypeOf(m))) line.Pass.Add(m);
                    else line.Torque.Add(m);
                }
                foreach (var m in members) _lineOf[m] = line;
            }
        }

        public bool Has(string id, string key) => _has(id, key);

        /// <summary>Gears meshing with <paramref name="id"/> (values-eligible meshes only).</summary>
        public IReadOnlyList<string> MeshPartners(string id) => _mesh.TryGetValue(id, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public Line? LineOf(string id) => _lineOf.TryGetValue(id, out var l) ? l : null;

        public IReadOnlyList<string> LeavesOf(string id) => _leaves.TryGetValue(id, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public string? LeafCarrier(string id) => _leafOf.TryGetValue(id, out var c) ? c : null;

        public IReadOnlyList<string> StrandParts(string strand) => _strandParts.TryGetValue(strand, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public IReadOnlyList<string> StrandsOf(string part) => _strandsOf.TryGetValue(part, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public IReadOnlyList<string> NutsOf(string screw) => _nuts.TryGetValue(screw, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        public string? ScrewOf(string nut) => _screwOf.TryGetValue(nut, out var s) ? s : null;
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
                if (line.Torque.Count <= 2 && line.Torque.Count + line.Pass.Count >= 2 && (line.Torque.Contains(id) || line.Pass.Contains(id)))
                {
                    var rep = line.Torque.Count > 0 ? line.Torque[0] : line.Pass[0];
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
            return list;
        }

        private static IEnumerable<BoundRelation> Strand(MechTopology topo, SketchDocument doc, string strand, string part)
        {
            var kind = doc.Get(strand)?.Type;
            if (kind == "mech.belt" && topo.Has(part, "d"))
            {
                yield return Bind(BeltSpeed, ("v", strand, "v"), ("d", part, "d"), ("n", part, "n"));
                yield return Bind(BeltTorque, ("T", part, "T"), ("F", strand, "F"), ("d", part, "d"));
            }
            else if (kind == "mech.chain" && topo.Has(part, "z"))
            {
                yield return Bind(ChainSpeed, ("v", strand, "v"), ("z", part, "z"), ("p", strand, "p"), ("n", part, "n"));
                yield return Bind(ChainForce, ("T", part, "T"), ("F", strand, "F"), ("z", part, "z"), ("p", strand, "p"));
                // Without a pitch the two sprockets still keep z·n and the torque ratio.
                var ends = topo.StrandParts(strand).Where(x => topo.Has(x, "z")).ToList();
                if (ends.Count == 2)
                {
                    var other = ends[0] == part ? ends[1] : ends[0];
                    yield return Bind(MeshSpeed, ("na", part, "n"), ("za", part, "z"), ("nb", other, "n"), ("zb", other, "z"));
                    yield return Bind(ChainTorque, ("Ta", part, "T"), ("za", part, "z"), ("Tb", other, "T"), ("zb", other, "z"));
                }
            }
        }
    }
}
