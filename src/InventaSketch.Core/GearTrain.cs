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
    /// pointing to the apex (so the two side gears of a differential with the cage held turn opposite ways);</item>
    /// <item>fixed carriers (grounded): 0.</item>
    /// </list>
    /// Speeds the system leaves open (a planetary set without a fixed member) are taken as 0, ring gears and carriers
    /// first, so the usual case (ring fixed to the housing) comes out. Planets on a carrier orbit with it. Only parts seen
    /// along their axis (axial views) visibly turn; side views keep their drawing.
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
        }

        /// <summary>True if <paramref name="o"/> visibly turns about its own axis (seen along it: axial views).</summary>
        public static bool Spins(Registry registry, SketchObject o)
            => !o.IsPointGeometry && registry.Type(o.Type) is { } t && t.MeshDirections(o).Count == 4
               && registry.PageAnchors(o, new RenderContext(SketchDocument.Create())).Any(a => a.Role is AnchorRole.Pitch or AnchorRole.PitchInner || a.Role == AnchorRole.Hub && a.Id is { } id && id.StartsWith("pin", StringComparison.Ordinal));

        /// <summary>
        /// Angular speed of every part of the train of <paramref name="inputId"/> relative to the input (input = 1; same
        /// sense = positive). Parts outside the train are not listed.
        /// </summary>
        public static IReadOnlyDictionary<string, double> Speeds(Registry registry, SketchDocument doc, string inputId, Lang lang = Lang.En)
        {
            var bodies = Bodies(registry, doc, lang);
            if (!bodies.ContainsKey(inputId)) return new Dictionary<string, double>();
            var eqs = Equations(registry, doc, bodies, lang);
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
            // Column order: input, plain parts, carriers, ring gears (free columns last become 0: ring and carrier fixed).
            var cols = train.OrderBy(id => id == inputId ? 0 : bodies[id].Pins.Count > 0 ? 2 : bodies[id].Inner ? 3 : 1).ToList();
            var index = cols.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i, StringComparer.Ordinal);
            var rows = new List<double[]>();
            foreach (var e in eqs.Where(e => e.Keys.All(index.ContainsKey)))
            {
                var r = new double[cols.Count + 1];
                foreach (var kv in e) r[index[kv.Key]] += kv.Value;
                rows.Add(r);
            }
            foreach (var id in cols.Where(id => bodies[id].Fixed && id != inputId))
            {
                var r = new double[cols.Count + 1];
                r[index[id]] = 1;
                rows.Add(r);
            }
            var input = new double[cols.Count + 1];
            input[0] = 1;
            input[cols.Count] = 1;
            rows.Insert(0, input);
            var w = Solve(rows, cols.Count);
            return cols.Select((id, i) => (id, w[i])).ToDictionary(t => t.id, t => t.Item2, StringComparer.Ordinal);
        }

        /// <summary>
        /// The document with the train of <paramref name="inputId"/> turned by <paramref name="degrees"/> at the input: every
        /// part turns by its speed times the angle (axial views), planets orbit with their carrier.
        /// </summary>
        public static SketchDocument Turn(Registry registry, SketchDocument doc, string inputId, double degrees, Lang lang = Lang.En)
        {
            var speeds = Speeds(registry, doc, inputId, lang);
            if (speeds.Count == 0) return doc;
            var bodies = Bodies(registry, doc, lang);
            var result = doc;
            foreach (var kv in speeds)
            {
                var b = bodies[kv.Key];
                var turn = kv.Value * degrees;
                var o = result.Get(kv.Key)!;
                var t = o.Transform;
                if (b.Spins) t = t with { Rotation = Math.Round(t.Rotation + turn, 6) };
                // Planets orbit about their carrier's centre.
                if (b.Carrier is { } c && speeds.TryGetValue(c, out var wc) && bodies[c].Spins)
                {
                    var pivot = bodies[c].Centre;
                    var a = wc * degrees * Math.PI / 180;
                    var p = new Vec2(t.X, t.Y) - pivot;
                    var q = pivot + new Vec2(p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a));
                    t = t with { X = Math.Round(q.X, 6), Y = Math.Round(q.Y, 6) };
                }
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
                var b = new Body { O = o, Centre = new Vec2(o.Transform.X, o.Transform.Y), Fixed = o.Flag("fixed", false) };
                var pitch = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner);
                if (pitch is not null) { b.Pitch = pitch.Radius; b.Inner = pitch.Role == AnchorRole.PitchInner; b.Centre = pitch.P; b.Side = pitch.AxisOf() is not null; }
                var hub = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && (a.Id == "hub" || a.Id is null));
                if (pitch is null && anchors.Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex))
                {
                    b.Bevel = true;
                    b.Teeth = Math.Max(1, o.Num("teeth", 0));
                }
                if (pitch is null && hub is not null) b.Centre = hub.P;
                b.Pins.AddRange(anchors.Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id is { } id && id.StartsWith("pin", StringComparison.Ordinal)).Select(a => a.P));
                b.Spins = registry.Type(o.Type)!.MeshDirections(o).Count == 4 && (pitch is not null || b.Pins.Count > 0);
                if (pitch is not null || hub is not null || b.Bevel || b.Pins.Count > 0 || anchors.Any(a => a.Role == AnchorRole.Axis)) map[o.Id] = b;
            }
            // Planets: parts whose centre sits on a carrier pin.
            foreach (var b in map.Values)
                foreach (var c in map.Values.Where(c => c != b && c.Pins.Count > 0))
                    if (c.Pins.Any(p => p.DistanceTo(b.Centre) <= 0.5)) { b.Carrier = c.O.Id; break; }
            return map;
        }

        private static List<Dictionary<string, double>> Equations(Registry registry, SketchDocument doc, Dictionary<string, Body> bodies, Lang lang)
        {
            var eqs = new List<Dictionary<string, double>>();
            void Add(params (string Id, double K)[] terms)
            {
                var e = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (id, k) in terms) e[id] = (e.TryGetValue(id, out var v) ? v : 0) + k;
                eqs.Add(e);
            }
            foreach (var link in Coupling.MechanicalLinks(registry, doc, null, lang).Where(l => !l.Released))
            {
                if (!bodies.TryGetValue(link.A, out var a) || !bodies.TryGetValue(link.B, out var b)) continue;
                switch (link.Kind)
                {
                    case LinkKind.Mesh when a.Pitch is double ra && b.Pitch is double rb:
                        {
                            var c = a.Carrier ?? b.Carrier;
                            if (a.Inner == b.Inner)
                            {
                                if (c is null) Add((a.O.Id, ra), (b.O.Id, rb));
                                else Add((a.O.Id, ra), (b.O.Id, rb), (c, -(ra + rb)));
                            }
                            else
                            {
                                // Ring (inner) and gear inside it turn the same way.
                                var (ring, rr, gear, rg) = a.Inner ? (a, ra, b, rb) : (b, rb, a, ra);
                                if (c is null) Add((ring.O.Id, rr), (gear.O.Id, -rg));
                                else Add((ring.O.Id, rr), (gear.O.Id, -rg), (c, -(rr - rg)));
                            }
                            break;
                        }
                    case LinkKind.Mesh when a.Bevel && b.Bevel:
                        // Bevel pair: speeds about the axes pointing to the shared apex (see the class).
                        Add((a.O.Id, a.Teeth), (b.O.Id, b.Teeth));
                        break;
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

        /// <summary>Gauss–Jordan elimination with partial pivoting; free columns (no pivot) are 0. Rows: coefficients + right side.</summary>
        private static double[] Solve(List<double[]> rows, int n)
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
            return w;
        }
    }
}
