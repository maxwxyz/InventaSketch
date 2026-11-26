using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Hydraulic circuits of a circulator pump (round 6, engineering-calculations.md section 3.7): the paths of water from the
    /// pump's outlet back to its inlet through pipes (also pipes docked on pipes) and through the elements along the way, in
    /// their direction of flow (generators return → flow, consumers flow → return, the pump in → out, the mixer A/B → AB, the
    /// manifold flow → outlets and returns → return, separator and buffer tank primary f1 → r1 and secondary r2 → f2; valves,
    /// strainers and meters either way; a riser joins everything docked on it). Each path sums the pressure drops it passes:
    /// pipes whose length, Δp or Σζ is entered (Δp and Δp_ζ) and elements whose Δp, k_v or ζ is entered. The pump's
    /// "pressure drop of the index circuit" is the largest path sum (relation <c>bs.circuit.sum</c>), so it is known once every
    /// counted drop on every path is known. Elements of other modules end a path. The search stops after
    /// <see cref="MaxPaths"/> paths or <see cref="MaxSteps"/> steps (no sum then).
    /// </summary>
    internal static class BsCircuits
    {
        internal const int MaxPaths = 256;
        internal const int MaxSteps = 50000;

        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>The circuits of one pump: distinct counted drops, the drops of each path (indices) and its consumer.</summary>
        internal sealed class Circuit
        {
            public Circuit(IReadOnlyList<VarRef> members, IReadOnlyList<int[]> paths, IReadOnlyList<string?> consumers)
            {
                Members = members; Paths = paths; Consumers = consumers;
            }

            public IReadOnlyList<VarRef> Members { get; }
            public IReadOnlyList<int[]> Paths { get; }
            /// <summary>The first consumer (radiator, floor loop, cylinder coil) on each path, or null.</summary>
            public IReadOnlyList<string?> Consumers { get; }
        }

        private static readonly HashSet<string> CircuitConsumers = new(BsValues.Consumers.Append("bs.san.heater"), StringComparer.Ordinal);

        private static readonly ConditionalWeakTable<SketchDocument, Dictionary<string, Circuit?>> Cache = new();
        private static readonly ConditionalWeakTable<SketchDocument, Graph> Graphs = new();

        /// <summary>The circuits of <paramref name="pump"/> (cached per document; null without a closed path or when truncated).</summary>
        internal static Circuit? Of(SketchDocument doc, SketchObject pump)
        {
            var map = Cache.GetValue(doc, _ => new Dictionary<string, Circuit?>(StringComparer.Ordinal));
            lock (map)
            {
                if (!map.TryGetValue(pump.Id, out var c)) map[pump.Id] = c = Build(doc, pump);
                return c;
            }
        }

        // ------------------------------------------------------------------ graph

        private const char Sep = '\u0001';
        private static string PortNode(string obj, string port) => "P" + Sep + obj + Sep + port;
        private static string LineNode(string id) => "L" + Sep + id;
        private static string ObjOf(string node) => node.Split(Sep)[1];

        /// <summary>Directed passages of an element between its docked ports: (from, to, both ways).</summary>
        private static IEnumerable<(string From, string To, bool Both)> Passages(SketchObject o, IReadOnlyCollection<string> docked)
        {
            switch (o.Type)
            {
                case "bs.heat.boiler":
                case "bs.heat.heatpump":
                case "bs.heat.solarthermal":
                    return new[] { ("return", "flow", false) };
                case "bs.heat.radiator":
                case "bs.heat.floorloop":
                case "bs.san.heater":
                    return new[] { ("flow", "return", false) };
                case BsValues.PumpId:
                    return new[] { ("in", "out", false) };
                case "bs.heat.trv":
                case "bs.valve.ball":
                case "bs.valve.check":
                case "bs.strainer":
                case "bs.meter.heat":
                case "bs.meter.water":
                    return new[] { ("in", "out", true) };
                case "bs.heat.mixer":
                    return new[] { ("a", "ab", false), ("b", "ab", false) };
                case BsValues.SeparatorId:
                case "bs.heat.buffer":
                    return new[] { ("f1", "r1", false), ("r2", "f2", false) };
                case "bs.heat.manifold":
                    return docked.Where(p => p.Length > 1 && p[0] == 'f' && char.IsDigit(p[1])).Select(p => ("flow", p, false))
                        .Concat(docked.Where(p => p.Length > 1 && p[0] == 'r' && char.IsDigit(p[1])).Select(p => (p, "return", false))).ToList();
                case "bs.san.riser":
                    var list = docked.ToList();
                    return list.SelectMany((a, i) => list.Skip(i + 1).Select(b => (a, b, true))).ToList();
            }
            return Array.Empty<(string, string, bool)>();
        }

        private sealed class Graph
        {
            /// <summary>Undirected: pipe ↔ pipe, pipe ↔ port.</summary>
            public readonly Dictionary<string, List<string>> Adj = new(StringComparer.Ordinal);
            /// <summary>Directed: port → port of the same element.</summary>
            public readonly Dictionary<string, List<string>> Pass = new(StringComparer.Ordinal);

            public static void Add(Dictionary<string, List<string>> d, string a, string b)
            {
                if (!d.TryGetValue(a, out var l)) d[a] = l = new List<string>();
                if (!l.Contains(b)) l.Add(b);
            }

            public IReadOnlyList<string> Of(Dictionary<string, List<string>> d, string n) => d.TryGetValue(n, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();
        }

        private static Graph BuildGraph(SketchDocument doc)
        {
            var g = new Graph();
            var docked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var line in doc.Objects.Values.Where(o => o.Type == PipeType.TypeId && o.IsPointGeometry).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var ln = LineNode(line.Id);
                foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                {
                    if (a is null || a.ObjectId == line.Id || doc.Get(a.ObjectId) is not { } target) continue;
                    if (target.IsPointGeometry)
                    {
                        if (target.Type != PipeType.TypeId) continue;
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
                if (doc.Get(kv.Key) is not { } o) continue;
                foreach (var (from, to, both) in Passages(o, kv.Value))
                {
                    if (!kv.Value.Contains(from) || !kv.Value.Contains(to) || from == to) continue;
                    Graph.Add(g.Pass, PortNode(o.Id, from), PortNode(o.Id, to));
                    if (both) Graph.Add(g.Pass, PortNode(o.Id, to), PortNode(o.Id, from));
                }
            }
            return g;
        }

        /// <summary>
        /// Simple paths from <paramref name="start"/> (left through its pipes) to <paramref name="target"/> (reached through a
        /// pipe): each a list of steps, "L…" a pipe, "E…" a passage through an element. Null when the search was cut off.
        /// </summary>
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

        /// <summary>The counted pressure drops of one step (a pipe: Δp and Δp_ζ; an element: Δp).</summary>
        private static IEnumerable<VarRef> Drops(SketchDocument doc, string step)
        {
            if (doc.Get(ObjOf(step)) is not { } o) yield break;
            var spec = BsValues.For(o.Type);
            if (step[0] == 'L')
            {
                if (Entered(o, spec.LossParams)) yield return new VarRef(o.Id, "dp");
                if (Entered(o, new[] { "lossCoefficient", "pressureDropSingle" })) yield return new VarRef(o.Id, "dpz");
                yield break;
            }
            if (spec.Variables.Any(v => v.Key == "dp") && Entered(o, spec.LossParams)) yield return new VarRef(o.Id, "dp");
        }

        private static Circuit? Build(SketchDocument doc, SketchObject pump)
        {
            var paths = Paths(Graphs.GetValue(doc, BuildGraph), PortNode(pump.Id, "out"), PortNode(pump.Id, "in"));
            if (paths is null || paths.Count == 0) return null;
            var members = new List<VarRef>();
            var index = new Dictionary<VarRef, int>();
            var sets = new List<int[]>();
            var consumers = new List<string?>();
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
                var consumer = p.Where(s => s[0] == 'E').Select(ObjOf).FirstOrDefault(id => doc.Get(id) is { } x && CircuitConsumers.Contains(x.Type));
                // Paths with the same drops and consumer are one circuit for the sum.
                if (!seen.Add(string.Join(",", ids) + "|" + consumer)) continue;
                sets.Add(ids.ToArray());
                consumers.Add(consumer);
            }
            return new Circuit(members, sets, consumers);
        }

        // ------------------------------------------------------------------ the pump's sum

        /// <summary>
        /// The relation "pressure drop of the index circuit" of a pump: ΣΔp = max over the paths of the sum of their counted
        /// drops (null without a path or without any counted drop).
        /// </summary>
        internal static BoundRelation? Sum(SketchDocument doc, SketchObject pump)
        {
            if (Of(doc, pump) is not { Members.Count: > 0 } c) return null;
            var rel = MaxOfSums("bs.circuit.sum", T("Pressure drop of the index circuit (largest sum along the circuits)", "Druckverlust des ungünstigsten Kreises (größte Summe entlang der Kreise)"),
                c.Paths, c.Members.Count, "S30");
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["s"] = new VarRef(pump.Id, "dpC") };
            for (var i = 0; i < c.Members.Count; i++) map["a" + (i + 1)] = c.Members[i];
            return new BoundRelation(rel, map);
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
                Validity = T("counts the parts with an entered length, Δp, kv or ζ; other parts count zero",
                             "zählt die Teile mit eingegebener Länge, Δp, kv oder ζ; andere Teile zählen null"),
            }
                .Solve("s", expr, v => copy.Max(p => p.Sum(i => v(keys[i]))));
        }

        // ------------------------------------------------------------------ balance check

        /// <summary>
        /// True if the circuits through the pump's consumers differ too much: per consumer the largest fully known path sum;
        /// the smallest of them below (1 − <paramref name="share"/>) of the largest (at least two consumers).
        /// </summary>
        internal static bool Unbalanced(SketchDocument doc, SketchObject pump, ValueTable table, double share)
        {
            if (Of(doc, pump) is not { Members.Count: > 0 } c) return false;
            var values = c.Members.Select(m => table.Get(m.ObjectId, m.Key)?.Value).ToArray();
            var best = new Dictionary<string, double>(StringComparer.Ordinal);
            for (var k = 0; k < c.Paths.Count; k++)
            {
                if (c.Consumers[k] is not { } consumer) continue;
                var p = c.Paths[k];
                if (p.Any(i => values[i] is null)) continue;
                var sum = p.Sum(i => values[i]!.Value);
                if (!best.TryGetValue(consumer, out var b) || sum > b) best[consumer] = sum;
            }
            if (best.Count < 2) return false;
            var max = best.Values.Max();
            return max > 0 && best.Values.Min() < (1 - share) * max * (1 - 1e-9);
        }
    }
}
