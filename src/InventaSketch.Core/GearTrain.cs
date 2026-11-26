using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Moving gear trains: turning one part (gear, carrier, pulley) turns every part coupled to it with the right ratio.
    /// The angular speeds are solved as a linear system over the parts of the train:
    /// <list type="bullet">
    /// <item>external mesh: r_a (ω_a − ω_c) + r_b (ω_b − ω_c) = 0, internal (ring a): R_a (ω_a − ω_c) − r_b (ω_b − ω_c) = 0, with
    /// ω_c the carrier of the planet involved (Willis), else 0;</item>
    /// <item>parts fixed on a shaft (hub fixed, sliding or none) and concentric gears (compound gears): same speed; loose
    /// hubs are free;</item>
    /// <item>belts and chains: r_a ω_a = r_b ω_b (crossed belts: opposite sense);</item>
    /// <item>bevel pairs (shared cone apex, axes crossing): z_a ω_a + z_b ω_b = 0 with each speed taken about the gear's axis
    /// pointing to the apex (so the two side gears of a differential with the cage held turn opposite ways); inside a cage (a
    /// part with crossing axes carrying bevel gears loose, the differential case) Willis applies with the cage speed
    /// projected on each gear's axis: z_a (ω_a − (u_a·c) ω_c) + z_b (ω_b − (u_b·c) ω_c) = 0 (u: axis to the apex, c: the cage
    /// axis) – the side gears turn with the cage when driving straight, and hold one and the other turns twice as fast;</item>
    /// <item>fixed carriers (grounded): 0;</item>
    /// <item>gear units (<see cref="IGearUnit"/>: gear set elements, a CVT): every part docked on a member port turns with
    /// that member (<see cref="GearUnitDocking"/>), and the members follow the unit's own speed relations (fixed ratio:
    /// ω_out = ω_in/i; planetary or differential: Willis) instead of turning all docked parts rigidly together;</item>
    /// <item>rack and pinion (a pitch circle touching a rack's <see cref="AnchorRole.PitchLine"/>): the rack travels
    /// v = ω·r along its pitch line; a rack marked <c>fixed</c> stays and the pinion rolls along it instead: the pinion's
    /// centre, and with it every part of the train that is not fixed (the carriage: its shaft, the gears driving it), travels
    /// v = ω·r the other way (<see cref="Travel"/>);</item>
    /// <item>a gear meshing with a pitch circle of a gear unit that belongs to one member (<see cref="GearUnitDocking.MeshPort"/>:
    /// the outer teeth of a ring gear) meshes with that member.</item>
    /// </list>
    /// Speeds the system leaves open (a planetary set without a fixed member) are taken as 0, ring gears and carriers
    /// first, so the usual case (ring fixed to the housing) comes out. Gear units are the exception: what depends on a free
    /// member of a unit (a planetary set element with no member held, a differential with its case free) stays unresolved
    /// and does not move. Planets on a carrier orbit with it. Only parts seen along their axis (axial views) visibly turn;
    /// side views keep their drawing; racks translate.
    /// </summary>
    public static class GearTrain
    {
        private sealed class Body
        {
            public SketchObject O = null!;
            public Vec2 Centre;
            public double? Pitch;
            public bool Inner;
            public bool Side;
            public readonly List<Vec2> Pins = new();
            public string? Carrier;
            public bool Fixed;
            public bool Spins;
            public bool Bevel;
            public double Teeth;
            /// <summary>Bevel gear: unit axis from its centre to the cone apex.</summary>
            public Vec2 ApexDir;
            /// <summary>Cage (two crossing axes: differential case): its rotation axis (unit), else null.</summary>
            public Vec2? CageAxis;
            /// <summary>Rack: its pitch line and the unit normal to its teeth, else null.</summary>
            public (Vec2 A, Vec2 B, Vec2 N)? Rack;
            /// <summary>Member ports of a gear unit (<see cref="IGearUnit"/>), empty for other parts.</summary>
            public IReadOnlyList<GearUnitPort> Ports = Array.Empty<GearUnitPort>();
            /// <summary>Gear unit whose pitch circle belongs to one member (outer teeth of a ring gear): that member, else null.</summary>
            public string? PitchMember;
            public IReadOnlyList<Anchor> Anchors = Array.Empty<Anchor>();
            public bool IsUnit => Ports.Count > 0;
        }

        /// <summary>Column id of member <paramref name="member"/> of gear unit <paramref name="unit"/> (no object id contains U+0001).</summary>
        private static string MemberColumn(string unit, string member) => unit + "\u0001" + member;

        private static bool IsMemberColumn(string id) => id.IndexOf('\u0001') >= 0;

        /// <summary>Travel direction of a rack for a positive speed of its pinion (the teeth normal turned by +90° on screen).</summary>
        private static Vec2 RackTravel(Vec2 n) => new(n.Y, -n.X);

        /// <summary>True if <paramref name="o"/> visibly turns about its own axis (seen along it: axial views).</summary>
        public static bool Spins(Registry registry, SketchObject o)
            => !o.IsPointGeometry && registry.Type(o.Type) is { } t && t.MeshDirections(o).Count == 4
               && registry.PageAnchors(o, new RenderContext(SketchDocument.Create())).Any(a => a.Role is AnchorRole.Pitch or AnchorRole.PitchInner || a.Role == AnchorRole.Hub && a.Id is { } id && id.StartsWith("pin", StringComparison.Ordinal));

        /// <summary>
        /// Angular speed of every part of the train of <paramref name="inputId"/> relative to the input (input = 1; same
        /// sense = positive). Racks give their travel along the pitch line in mm per radian of the input. Parts outside the
        /// train, and parts whose speed depends on a free member of a gear unit, are not listed.
        /// </summary>
        public static IReadOnlyDictionary<string, double> Speeds(Registry registry, SketchDocument doc, string inputId, Lang lang = Lang.En)
            => Solve(registry, doc, inputId, lang).Speeds;

        /// <summary>
        /// Travel of the pinion's centre (mm per radian of the input, page coordinates) when the train of
        /// <paramref name="inputId"/> rolls along a fixed rack (a rack whose <c>fixed</c> flag is set): v = ω·r opposite to
        /// the way a free rack would travel. Every part of the train that is not fixed travels with it. Null if the train
        /// meshes with no fixed rack, or with several whose travels disagree (it cannot roll).
        /// </summary>
        public static Vec2? Travel(Registry registry, SketchDocument doc, string inputId, Lang lang = Lang.En)
            => Solve(registry, doc, inputId, lang).Travel;

        private sealed class Result
        {
            public Dictionary<string, double> Speeds = new(StringComparer.Ordinal);
            public Dictionary<string, Body> Bodies = new(StringComparer.Ordinal);
            /// <summary>Travel of the carriage along fixed racks (mm per radian of the input), else null.</summary>
            public Vec2? Travel;
        }

        private static Result Solve(Registry registry, SketchDocument doc, string inputId, Lang lang)
        {
            var result = new Result { Bodies = Bodies(registry, doc, lang) };
            var bodies = result.Bodies;
            if (!bodies.ContainsKey(inputId)) return result;
            var spiders = new HashSet<string>(StringComparer.Ordinal);
            var rolling = new List<(string Column, double R, Vec2 N)>();
            var eqs = Equations(registry, doc, bodies, lang, spiders, rolling);
            // The train: everything reachable from the input through the equations (and carrier membership).
            var adj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            void Edge(string a, string b)
            {
                if (!adj.TryGetValue(a, out var s)) adj[a] = s = new HashSet<string>(StringComparer.Ordinal);
                s.Add(b);
            }
            foreach (var e in eqs) foreach (var a in e.Keys) foreach (var b in e.Keys) if (a != b) Edge(a, b);
            var train = new List<string> { inputId };
            var seen = new HashSet<string>(train, StringComparer.Ordinal);
            for (var i = 0; i < train.Count; i++)
                if (adj.TryGetValue(train[i], out var next))
                    foreach (var n in next) if (seen.Add(n)) train.Add(n);
            // Column order: input, plain parts, carriers, ring gears, spider gears of a cage (free columns last become 0: ring
            // and carrier fixed, a differential drives straight on).
            // Members of gear units come after everything else: when free they stay unresolved (not 0).
            var cols = train.OrderBy(id => id == inputId ? 0 : IsMemberColumn(id) ? 5 : spiders.Contains(id) ? 4 : bodies[id].Pins.Count > 0 || bodies[id].CageAxis is not null ? 2 : bodies[id].Inner ? 3 : 1).ToList();
            var index = cols.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i, StringComparer.Ordinal);
            var rows = new List<double[]>();
            foreach (var e in eqs.Where(e => e.Keys.All(index.ContainsKey)))
            {
                var r = new double[cols.Count + 1];
                foreach (var kv in e) r[index[kv.Key]] += kv.Value;
                rows.Add(r);
            }
            foreach (var id in cols.Where(id => !IsMemberColumn(id) && bodies[id].Fixed && id != inputId))
            {
                var r = new double[cols.Count + 1];
                r[index[id]] = 1;
                rows.Add(r);
            }
            var input = new double[cols.Count + 1];
            input[0] = 1;
            input[cols.Count] = 1;
            rows.Insert(0, input);
            var soft = cols.Select(IsMemberColumn).ToArray();
            var (w, determined) = Solve(rows, cols.Count, soft);
            result.Speeds = cols.Select((id, i) => (id, i)).Where(t => !soft[t.i] && determined[t.i])
                .ToDictionary(t => t.id, t => w[t.i], StringComparer.Ordinal);
            // Rolling along fixed racks: the pinion's centre travels against the way a free rack would (v = ω·r); all contacts
            // must agree, else the train cannot roll.
            Vec2? travel = null;
            foreach (var (col, r, n) in rolling)
            {
                if (!index.TryGetValue(col, out var k) || !determined[k]) continue;
                var d = RackTravel(n) * (-r * w[k]);
                if (travel is null) travel = d;
                else if ((travel.Value - d).Length > 1e-6) { travel = null; break; }
            }
            result.Travel = travel is { } tv && tv.Length > 1e-12 ? tv : null;
            return result;
        }

        /// <summary>
        /// The document with the train of <paramref name="inputId"/> turned by <paramref name="degrees"/> at the input: every
        /// part turns by its speed times the angle (axial views), planets orbit with their carrier.
        /// </summary>
        public static SketchDocument Turn(Registry registry, SketchDocument doc, string inputId, double degrees, Lang lang = Lang.En)
        {
            var solved = Solve(registry, doc, inputId, lang);
            var speeds = solved.Speeds;
            if (speeds.Count == 0) return doc;
            var bodies = solved.Bodies;
            // Rolling along a fixed rack: every part of the train that is not fixed travels with the pinion (the carriage).
            var carriage = solved.Travel is { } v ? v * (degrees * Math.PI / 180) : Vec2.Zero;
            var result = doc;
            foreach (var kv in speeds)
            {
                var b = bodies[kv.Key];
                var turn = kv.Value * degrees;
                var o = result.Get(kv.Key)!;
                var t = o.Transform;
                if (b.Spins) t = t with { Rotation = Math.Round(t.Rotation + turn, 6) };
                // A rack travels along its pitch line (speed in mm per radian of the input).
                if (b.Rack is { } rack)
                {
                    var d = RackTravel(rack.N) * (kv.Value * degrees * Math.PI / 180);
                    t = t with { X = Math.Round(t.X + d.X, 6), Y = Math.Round(t.Y + d.Y, 6) };
                }
                // Planets orbit about their carrier's centre.
                if (b.Carrier is { } c && speeds.TryGetValue(c, out var wc) && bodies[c].Spins)
                {
                    var pivot = bodies[c].Centre;
                    var a = wc * degrees * Math.PI / 180;
                    var p = new Vec2(t.X, t.Y) - pivot;
                    var q = pivot + new Vec2(p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a));
                    t = t with { X = Math.Round(q.X, 6), Y = Math.Round(q.Y, 6) };
                }
                if (carriage.Length > 0 && !b.Fixed) t = t with { X = Math.Round(t.X + carriage.X, 6), Y = Math.Round(t.Y + carriage.Y, 6) };
                if (t != o.Transform) result = Edits.Update(result, new[] { kv.Key }, x => x with { Transform = t });
            }
            return result;
        }

        /// <summary>
        /// Frames for an animation: <paramref name="count"/> states of the whole train, the input turned by
        /// <paramref name="totalDegrees"/> over the frames (first frame = current state).
        /// </summary>
        public static IReadOnlyList<SketchDocument> Frames(Registry registry, SketchDocument doc, string inputId, int count, double totalDegrees, Lang lang = Lang.En)
        {
            var list = new List<SketchDocument> { doc };
            for (var k = 1; k < count; k++) list.Add(Turn(registry, doc, inputId, totalDegrees * k / (count - 1), lang));
            return list;
        }

        private static Dictionary<string, Body> Bodies(Registry registry, SketchDocument doc, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            var map = new Dictionary<string, Body>(StringComparer.Ordinal);
            foreach (var o in doc.Objects.Values.Where(o => !o.IsPointGeometry && registry.Type(o.Type) is not null))
            {
                var anchors = registry.PageAnchors(o, ctx).ToList();
                var b = new Body { O = o, Centre = new Vec2(o.Transform.X, o.Transform.Y), Fixed = o.Flag("fixed", false), Anchors = anchors };
                b.Ports = GearUnitDocking.PortsOf(registry, o);
                if (b.Ports.Count > 0) b.PitchMember = GearUnitDocking.MeshPort(registry, o, anchors)?.Member;
                if (anchors.FirstOrDefault(a => a.TeethSide() is not null) is { } line) b.Rack = (line.P, line.Q, line.TeethSide()!.Value);
                var pitch = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner);
                if (pitch is not null) { b.Pitch = pitch.Radius; b.Inner = pitch.Role == AnchorRole.PitchInner; b.Centre = pitch.P; b.Side = pitch.AxisOf() is not null; }
                var hub = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && (a.Id == "hub" || a.Id is null));
                if (pitch is null && anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex) is { } apex)
                {
                    b.Bevel = true;
                    b.Teeth = Math.Max(1, o.Num("teeth", 0));
                    var u = apex.P - new Vec2(o.Transform.X, o.Transform.Y);
                    b.ApexDir = u.Length > 1e-9 ? u * (1 / u.Length) : new Vec2(1, 0);
                }
                var axes = anchors.Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && (a.Q - a.P).Length > 1e-9).Select(a => (a.Q - a.P).Normalized()).ToList();
                if (axes.Count >= 2 && axes.Skip(1).Any(x => Math.Abs(axes[0].Cross(x)) > 0.5)) b.CageAxis = axes[0];
                if (pitch is null && hub is not null) b.Centre = hub.P;
                b.Pins.AddRange(anchors.Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id is { } id && id.StartsWith("pin", StringComparison.Ordinal)).Select(a => a.P));
                b.Spins = registry.Type(o.Type)!.MeshDirections(o).Count == 4 && (pitch is not null || b.Pins.Count > 0);
                if (pitch is not null || hub is not null || b.Bevel || b.Pins.Count > 0 || b.Rack is not null || b.IsUnit || anchors.Any(a => a.Role == AnchorRole.Axis)) map[o.Id] = b;
            }
            // Planets: parts whose centre sits on a carrier pin.
            foreach (var b in map.Values)
                foreach (var c in map.Values.Where(c => c != b && c.Pins.Count > 0))
                    if (c.Pins.Any(p => p.DistanceTo(b.Centre) <= 0.5)) { b.Carrier = c.O.Id; break; }
            return map;
        }

        /// <summary>
        /// The speed equations of the document; <paramref name="spiders"/> receives the spider gears of cages,
        /// <paramref name="rolling"/> the pinions on fixed racks (column, pitch radius, teeth normal of the rack).
        /// </summary>
        private static List<Dictionary<string, double>> Equations(Registry registry, SketchDocument doc, Dictionary<string, Body> bodies, Lang lang, HashSet<string> spiders,
            List<(string Column, double R, Vec2 N)> rolling)
        {
            var eqs = new List<Dictionary<string, double>>();
            void Add(params (string Id, double K)[] terms)
            {
                var e = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (id, k) in terms) e[id] = (e.TryGetValue(id, out var v) ? v : 0) + k;
                eqs.Add(e);
            }
            var links = Coupling.MechanicalLinks(registry, doc, null, lang).Where(l => !l.Released).ToList();
            // Bevel gears carried by a cage run loose in it (Willis with the cage, see the class).
            var cageOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var l in links.Where(l => l.Kind == LinkKind.Carried))
                if (bodies.TryGetValue(l.A, out var ca) && ca.CageAxis is { } axis && bodies.TryGetValue(l.B, out var cb) && cb.Bevel)
                {
                    cageOf[l.B] = l.A;
                    if (Math.Abs(cb.ApexDir.Dot(axis)) < 0.5) spiders.Add(l.B);
                }
            // Gear units with a docked part: their member relations join the docked parts.
            var units = new HashSet<string>(StringComparer.Ordinal);
            GearUnitPort? Docked(Body unit, Body part)
                => GearUnitDocking.DockedPort(registry, unit.O, part.O, o => bodies.TryGetValue(o.Id, out var x) ? x.Anchors : Array.Empty<Anchor>());
            // The column a mesh acts on: the member whose teeth the unit's pitch circle is (outer teeth of a ring), else the part.
            string Col(Body x)
            {
                if (x.PitchMember is not { } m) return x.O.Id;
                units.Add(x.O.Id);
                return MemberColumn(x.O.Id, m);
            }
            // A unit drawn with a centre bore (axial views) turns as a whole with the member at the bore, as it did before.
            foreach (var u in bodies.Values.Where(x => x.IsUnit))
                if (u.Ports.FirstOrDefault(p => p.Centre && u.Anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == p.AnchorId)) is { } bore)
                {
                    Add((u.O.Id, 1), (MemberColumn(u.O.Id, bore.Member), -1));
                    units.Add(u.O.Id);
                }
            foreach (var link in links)
            {
                if (!bodies.TryGetValue(link.A, out var a) || !bodies.TryGetValue(link.B, out var b)) continue;
                if ((a.IsUnit || b.IsUnit) && link.Kind is LinkKind.Carried or LinkKind.Bonded)
                {
                    // A loose hub turns freely on its shaft; otherwise the part turns with the member it is docked on (none:
                    // not docked on a port, or a shaft through two members, which would lock the unit).
                    if (link.Kind == LinkKind.Carried && b.O.Str("hub", "fixed") == "loose") continue;
                    if (a.IsUnit && b.IsUnit)
                    {
                        if (Docked(a, b) is { } pa && Docked(b, a) is { } pb)
                        {
                            Add((MemberColumn(a.O.Id, pa.Member), 1), (MemberColumn(b.O.Id, pb.Member), -1));
                            units.Add(a.O.Id);
                            units.Add(b.O.Id);
                        }
                        continue;
                    }
                    var (unit, part) = a.IsUnit ? (a, b) : (b, a);
                    if (Docked(unit, part) is { } port)
                    {
                        Add((part.O.Id, 1), (MemberColumn(unit.O.Id, port.Member), -1));
                        units.Add(unit.O.Id);
                    }
                    continue;
                }
                switch (link.Kind)
                {
                    case LinkKind.Mesh when a.Pitch is double rg && !a.Inner && b.Rack is { } rk:
                        // Rack and pinion: the rack travels r·ω along its pitch line; a fixed rack stays and the pinion rolls.
                        if (b.Fixed) rolling.Add((Col(a), rg, rk.N));
                        else Add((b.O.Id, 1), (Col(a), -rg));
                        break;
                    case LinkKind.Mesh when a.Pitch is double ra && b.Pitch is double rb:
                        {
                            var c = a.Carrier ?? b.Carrier;
                            string ca = Col(a), cb = Col(b);
                            if (a.Inner == b.Inner)
                            {
                                if (c is null) Add((ca, ra), (cb, rb));
                                else Add((ca, ra), (cb, rb), (c, -(ra + rb)));
                            }
                            else
                            {
                                // Ring (inner) and gear inside it turn the same way.
                                var (ring, rr, gear, rg) = a.Inner ? (ca, ra, cb, rb) : (cb, rb, ca, ra);
                                if (c is null) Add((ring, rr), (gear, -rg));
                                else Add((ring, rr), (gear, -rg), (c, -(rr - rg)));
                            }
                            break;
                        }
                    case LinkKind.Mesh when a.Bevel && b.Bevel:
                        {
                            // Bevel pair: speeds about the axes pointing to the shared apex, relative to their cage (see the class).
                            var cage = cageOf.TryGetValue(a.O.Id, out var x) ? x : cageOf.TryGetValue(b.O.Id, out var y) ? y : null;
                            if (cage is null) Add((a.O.Id, a.Teeth), (b.O.Id, b.Teeth));
                            else
                            {
                                var c = bodies[cage].CageAxis!.Value;
                                Add((a.O.Id, a.Teeth), (b.O.Id, b.Teeth), (cage, -(a.Teeth * a.ApexDir.Dot(c) + b.Teeth * b.ApexDir.Dot(c))));
                            }
                            break;
                        }
                    case LinkKind.Carried when a.CageAxis is not null && b.Bevel:
                        break; // loose in the cage
                    case LinkKind.Carried:
                        {
                            // Shaft A carries B: fixed on it unless the hub is loose.
                            var kind = b.O.Str("hub", "fixed");
                            if (kind != "loose") Add((a.O.Id, 1), (b.O.Id, -1));
                            break;
                        }
                    case LinkKind.Bonded:
                        {
                            // Planet on a carrier pin: no speed relation (it rolls); concentric gears: one compound gear.
                            if (a.Carrier == b.O.Id || b.Carrier == a.O.Id) break;
                            if (a.Pins.Count > 0 || b.Pins.Count > 0 || a.Inner || b.Inner) break;
                            Add((a.O.Id, 1), (b.O.Id, -1));
                            break;
                        }
                }
            }
            foreach (var id in units)
                if (registry.Type(bodies[id].O.Type) is IGearUnit gu)
                    foreach (var row in gu.SpeedRelations(bodies[id].O))
                        if (row.Count > 0) Add(row.Select(kv => (MemberColumn(id, kv.Key), kv.Value)).ToArray());
            // Belts and chains: both pulleys of one strand.
            foreach (var strand in Coupling.MechanicalLinks(registry, doc, null, lang).Where(l => l.Kind == LinkKind.Spanned && !l.Released).GroupBy(l => l.A))
            {
                var ends = strand.Select(l => l.B).Distinct().Where(bodies.ContainsKey).ToList();
                if (ends.Count != 2 || bodies[ends[0]].Pitch is not double r0 || bodies[ends[1]].Pitch is not double r1) continue;
                var crossed = doc.Get(strand.Key)?.View == "crossed";
                Add((ends[0], r0), (ends[1], crossed ? r1 : -r1));
            }
            return eqs;
        }

        /// <summary>
        /// Gauss–Jordan elimination with partial pivoting; free columns (no pivot) are 0. Rows: coefficients + right side.
        /// <paramref name="soft"/> marks columns that must not be fixed at 0 when free (members of gear units): they and every
        /// pivot column that depends on a free one of them come back as not determined.
        /// </summary>
        private static (double[] W, bool[] Determined) Solve(List<double[]> rows, int n, bool[] soft)
        {
            var m = rows.Select(r => (double[])r.Clone()).ToList();
            var pivotCol = new List<int>();
            var row = 0;
            for (var col = 0; col < n && row < m.Count; col++)
            {
                var best = row;
                for (var r = row + 1; r < m.Count; r++) if (Math.Abs(m[r][col]) > Math.Abs(m[best][col])) best = r;
                if (Math.Abs(m[best][col]) < 1e-9) continue;
                (m[row], m[best]) = (m[best], m[row]);
                var pv = m[row][col];
                for (var k = 0; k <= n; k++) m[row][k] /= pv;
                for (var r = 0; r < m.Count; r++)
                {
                    if (r == row || Math.Abs(m[r][col]) < 1e-12) continue;
                    var f = m[r][col];
                    for (var k = 0; k <= n; k++) m[r][k] -= f * m[row][k];
                }
                pivotCol.Add(col);
                row++;
            }
            var w = new double[n];
            for (var i = 0; i < pivotCol.Count; i++) w[pivotCol[i]] = m[i][n];
            var determined = Enumerable.Repeat(true, n).ToArray();
            var pivots = new HashSet<int>(pivotCol);
            var freeSoft = Enumerable.Range(0, n).Where(c => soft[c] && !pivots.Contains(c)).ToList();
            foreach (var c in freeSoft) determined[c] = false;
            for (var i = 0; i < pivotCol.Count; i++)
                if (freeSoft.Any(c => Math.Abs(m[i][c]) > 1e-9)) determined[pivotCol[i]] = false;
            return (w, determined);
        }
    }
}
