using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Supply paths of a cylinder (round 6, engineering-calculations.md section 3.5): the paths from a supply's pressure port P
    /// (pump, hydraulic power unit, pressure source) through lines (also lines docked on lines) and through the passages of
    /// the elements on the way (directional valve P–A–B, non-return valves, filters, coolers, throttles … as for the flow
    /// <c>Q</c> in <see cref="FluidValues.Nets"/>) to the cylinder's working port A (else B). Each path sums the pressure drops
    /// it passes: lines whose length, Δp or Σζ is entered (Δp and Δp_ζ) and elements whose Δp, ζ or datasheet point (throttle:
    /// orifice or Δp) is entered. The cylinder's "pressure drop of the supply path" is the largest path sum (relation
    /// <c>fluid.circuit.sum</c>) and the pressure available at the cylinder p_av = p_supply − ΣΔp (<c>fluid.circuit.available</c>,
    /// p_supply the supply's <c>p</c>). The first supply (by id) with a path counts; elements of other modules end a path. The
    /// search stops after <see cref="MaxPaths"/> paths or <see cref="MaxSteps"/> steps (no sum then).
    /// </summary>
    internal static class FluidCircuits
    {
        internal const int MaxPaths = 256;
        internal const int MaxSteps = 50000;

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly string[] SupplyTypes = { "fluid.pump", FluidValues.PowerUnitId, "fluid.source" };

        /// <summary>The supply path of a cylinder: the supply, the cylinder port reached, the counted drops and the paths.</summary>
        internal sealed class Circuit
        {
            public Circuit(string supply, string port, IReadOnlyList<VarRef> members, IReadOnlyList<int[]> paths)
            {
                Supply = supply; Port = port; Members = members; Paths = paths;
            }

            public string Supply { get; }
            public string Port { get; }
            public IReadOnlyList<VarRef> Members { get; }
            public IReadOnlyList<int[]> Paths { get; }
        }

        private static readonly ConditionalWeakTable<SketchDocument, Dictionary<string, Circuit?>> Cache = new();
        private static readonly ConditionalWeakTable<SketchDocument, Graph> Graphs = new();

        /// <summary>The supply path of <paramref name="cylinder"/> (cached per document; null without a path or when truncated).</summary>
        internal static Circuit? Of(SketchDocument doc, SketchObject cylinder)
        {
            var map = Cache.GetValue(doc, _ => new Dictionary<string, Circuit?>(StringComparer.Ordinal));
            lock (map)
            {
                if (!map.TryGetValue(cylinder.Id, out var c)) map[cylinder.Id] = c = Build(doc, cylinder);
                return c;
            }
        }

        // ------------------------------------------------------------------ graph

        private const char Sep = '\u0001';
        private static string PortNode(string obj, string port) => "P" + Sep + obj + Sep + port;
        private static string LineNode(string id) => "L" + Sep + id;
        private static string ObjOf(string node) => node.Split(Sep)[1];

        private sealed class Graph
        {
            /// <summary>Undirected: line ↔ line, line ↔ port.</summary>
            public readonly Dictionary<string, List<string>> Adj = new(StringComparer.Ordinal);
            /// <summary>Passages between ports of the same element (both ways).</summary>
            public readonly Dictionary<string, List<string>> Pass = new(StringComparer.Ordinal);

            public static void Add(Dictionary<string, List<string>> d, string a, string b)
            {
                if (!d.TryGetValue(a, out var l)) d[a] = l = new List<string>();
                if (!l.Contains(b)) l.Add(b);
            }

            public IReadOnlyList<string> Of(Dictionary<string, List<string>> d, string n) => d.TryGetValue(n, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();
        }

        private static Graph GraphOf(SketchDocument doc) => Graphs.GetValue(doc, d =>
        {
            var g = new Graph();
            var docked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var line in d.Objects.Values.Where(o => o.Type == FluidLineType.TypeId && o.IsPointGeometry).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var ln = LineNode(line.Id);
                foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                {
                    if (a is null || a.ObjectId == line.Id || d.Get(a.ObjectId) is not { } target) continue;
                    if (target.IsPointGeometry)
                    {
                        if (target.Type != FluidLineType.TypeId) continue;
                        Graph.Add(g.Adj, ln, LineNode(target.Id));
                        Graph.Add(g.Adj, LineNode(target.Id), ln);
                        continue;
                    }
                    var port = a.AnchorId is { } id && !id.StartsWith("rail", StringComparison.Ordinal) && !id.StartsWith("bar", StringComparison.Ordinal) ? id : "*";
                    var pn = PortNode(target.Id, port);
                    Graph.Add(g.Adj, ln, pn);
                    Graph.Add(g.Adj, pn, ln);
                    if (!docked.TryGetValue(target.Id, out var ports)) docked[target.Id] = ports = new List<string>();
                    if (!ports.Contains(port)) ports.Add(port);
                }
            }
            foreach (var kv in docked)
            {
                if (d.Get(kv.Key) is not { } o) continue;
                foreach (var group in FluidValues.Passages(o))
                {
                    var present = group.Where(kv.Value.Contains).ToList();
                    foreach (var x in present)
                        foreach (var y in present)
                            if (x != y) Graph.Add(g.Pass, PortNode(o.Id, x), PortNode(o.Id, y));
                }
            }
            return g;
        });

        /// <summary>Simple paths from <paramref name="start"/> (left through its lines) to <paramref name="target"/> (reached through a line); null when cut off.</summary>
        private static List<List<string>>? Paths(Graph g, string start, string target)
        {
            var paths = new List<List<string>>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { start };
            var path = new List<string>();
            var steps = 0;
            var cut = false;

            void Leave(string port)
            {
                foreach (var n in g.Of(g.Adj, port))
                    if (!cut && !visited.Contains(n)) Line(n);
            }

            void Line(string line)
            {
                if (++steps > MaxSteps) { cut = true; return; }
                visited.Add(line);
                path.Add(line);
                foreach (var n in g.Of(g.Adj, line))
                {
                    if (cut) break;
                    if (n == target)
                    {
                        if (paths.Count >= MaxPaths) { cut = true; break; }
                        paths.Add(new List<string>(path));
                        continue;
                    }
                    if (visited.Contains(n)) continue;
                    if (n[0] == 'L') { Line(n); continue; }
                    visited.Add(n);
                    // Several lines docked on one port meet there (a branch at the port).
                    foreach (var m in g.Of(g.Adj, n))
                        if (!cut && !visited.Contains(m)) Line(m);
                    foreach (var to in g.Of(g.Pass, n))
                    {
                        if (cut || visited.Contains(to)) continue;
                        visited.Add(to);
                        path.Add("E" + Sep + ObjOf(n));
                        Leave(to);
                        path.RemoveAt(path.Count - 1);
                        visited.Remove(to);
                    }
                    visited.Remove(n);
                }
                path.RemoveAt(path.Count - 1);
                visited.Remove(line);
            }

            Leave(start);
            return cut ? null : paths;
        }

        /// <summary>True if one of the parameters is entered (a number or non-blank text).</summary>
        internal static bool Entered(SketchObject o, IEnumerable<string> keys)
            => keys.Any(k => o.Params.TryGetValue(k, out var p) && (p.Number.HasValue || !string.IsNullOrWhiteSpace(p.Text)));

        /// <summary>The counted pressure drops of one step (a line: Δp and Δp_ζ; an element: Δp).</summary>
        private static IEnumerable<VarRef> Drops(SketchDocument doc, string step)
        {
            if (doc.Get(ObjOf(step)) is not { } o) yield break;
            var spec = FluidValues.For(o.Type);
            if (step[0] == 'L')
            {
                if (Entered(o, spec.LossParams)) yield return new VarRef(o.Id, "dp");
                if (Entered(o, new[] { "lossCoefficient", "pressureDropSingle" })) yield return new VarRef(o.Id, "dpz");
                yield break;
            }
            if (spec.Variables.Any(v => v.Key == "dp") && Entered(o, spec.LossParams)) yield return new VarRef(o.Id, "dp");
        }

        private static Circuit? Build(SketchDocument doc, SketchObject cylinder)
        {
            var g = GraphOf(doc);
            var supplies = doc.Objects.Values.Where(o => SupplyTypes.Contains(o.Type) && !o.IsPointGeometry).OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            foreach (var supply in supplies)
                foreach (var port in new[] { "A", "B" })
                {
                    var paths = Paths(g, PortNode(supply.Id, "P"), PortNode(cylinder.Id, port));
                    if (paths is null) return null;
                    if (paths.Count == 0) continue;
                    var members = new List<VarRef>();
                    var index = new Dictionary<VarRef, int>();
                    var sets = new List<int[]>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var p in paths)
                    {
                        var ids = new List<int>();
                        foreach (var step in p)
                            foreach (var m in Drops(doc, step))
                            {
                                if (!index.TryGetValue(m, out var i)) { index[m] = i = members.Count; members.Add(m); }
                                if (!ids.Contains(i)) ids.Add(i);
                            }
                        ids.Sort();
                        if (seen.Add(string.Join(",", ids))) sets.Add(ids.ToArray());
                    }
                    return new Circuit(supply.Id, port, members, sets);
                }
            return null;
        }

        // ------------------------------------------------------------------ relations of the cylinder

        /// <summary>
        /// The cylinder's supply-path relations: ΣΔp = the largest path sum, and p_av = p_supply − ΣΔp (nothing without a
        /// path or without any counted drop on it).
        /// </summary>
        internal static IEnumerable<BoundRelation> Links(SketchDocument doc, SketchObject cylinder)
        {
            if (Of(doc, cylinder) is not { Members.Count: > 0 } c) yield break;
            var rel = MaxOfSums("fluid.circuit.sum", T("Pressure drop of the supply path (largest sum along the paths)", "Druckverlust des Zulaufwegs (größte Summe entlang der Wege)"),
                c.Paths, c.Members.Count, "S26");
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["s"] = new VarRef(cylinder.Id, "dpS") };
            for (var i = 0; i < c.Members.Count; i++) map["a" + (i + 1)] = c.Members[i];
            yield return new BoundRelation(rel, map);
            yield return new BoundRelation(FluidValues.Available, new Dictionary<string, VarRef>(StringComparer.Ordinal)
            {
                ["pav"] = new VarRef(cylinder.Id, "pav"), ["ps"] = new VarRef(c.Supply, "p"), ["dpS"] = new VarRef(cylinder.Id, "dpS"),
            });
        }

        /// <summary>s = max over the paths of Σ a_i (one direction: the sum).</summary>
        internal static Relation MaxOfSums(string id, I18nText name, IReadOnlyList<int[]> paths, int n, string source)
        {
            var keys = Enumerable.Range(1, n).Select(i => "a" + i).ToArray();
            string Term(int[] p) => p.Length == 0 ? "0" : string.Join(" + ", p.Select(i => "{" + keys[i] + "}"));
            var terms = paths.Select(Term).Distinct().ToList();
            var expr = terms.Count == 1 ? terms[0] : "max(" + string.Join(", ", terms) + ")";
            var copy = paths.Select(p => p.ToArray()).ToArray();
            return new Relation(id, name, "{s} = " + expr, new[] { "s" }.Concat(keys).ToArray())
            {
                Source = source,
                Validity = T("counts the parts with an entered length, Δp, ζ or datasheet point; other parts count zero",
                             "zählt die Teile mit eingegebener Länge, Δp, ζ oder Datenblattpunkt; andere Teile zählen null"),
            }
                .Solve("s", expr, v => copy.Max(p => p.Sum(i => v(keys[i]))));
        }
    }
}
