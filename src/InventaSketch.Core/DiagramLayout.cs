using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Automatic layout of a connected diagram (flowchart, UML, BPMN, ER, process steps): the selected elements are the
    /// nodes, every line whose first and last docked ends sit on two of them is an edge (from its first to its last end).
    /// A layered layout in the manner of Sugiyama: cycles are broken along a depth-first search, layers follow the longest
    /// path from the sources, the order within a layer is improved by barycentre sweeps (fewer crossings), and the layers
    /// are spaced by the elements' own sizes. The diagram keeps its place (top edge and horizontal centre for "down", left
    /// edge and vertical centre for "right"); docked connectors follow their elements.
    /// </summary>
    public static class DiagramLayout
    {
        /// <summary>
        /// New centres of the box elements <paramref name="boxes"/> laid out in layers (top to bottom when
        /// <paramref name="down"/>, else left to right), or null if no line connects two of them.
        /// </summary>
        public static IReadOnlyDictionary<string, Vec2>? Layered(SketchDocument doc, IReadOnlyList<SketchObject> boxes, bool down)
        {
            var nodes = boxes.Where(b => !b.IsPointGeometry).GroupBy(b => b.Id).Select(g => g.First()).ToList();
            if (nodes.Count < 2) return null;
            var ids = new HashSet<string>(nodes.Select(n => n.Id), StringComparer.Ordinal);
            var edges = Edges(doc, ids);
            if (edges.Count == 0) return null;

            // Index and adjacency in the order of the current positions (stable, the user's arrangement is a good start).
            var ordered = nodes.OrderBy(n => down ? n.Transform.Y : n.Transform.X).ThenBy(n => down ? n.Transform.X : n.Transform.Y).ToList();
            var index = ordered.Select((n, i) => (n.Id, i)).ToDictionary(t => t.Id, t => t.i, StringComparer.Ordinal);
            var n0 = ordered.Count;
            var outs = Enumerable.Range(0, n0).Select(_ => new List<int>()).ToList();
            foreach (var (a, b) in edges)
            {
                int ia = index[a], ib = index[b];
                if (ia != ib && !outs[ia].Contains(ib)) outs[ia].Add(ib);
            }
            // Break cycles: drop the edges that point back to a node on the current depth-first path.
            var state = new int[n0];
            var dag = Enumerable.Range(0, n0).Select(_ => new List<int>()).ToList();
            void Visit(int v)
            {
                state[v] = 1;
                foreach (var w in outs[v])
                {
                    if (state[w] == 1) continue;
                    dag[v].Add(w);
                    if (state[w] == 0) Visit(w);
                }
                state[v] = 2;
            }
            for (var v = 0; v < n0; v++) if (state[v] == 0) Visit(v);

            // Layers: longest path from the sources.
            var layer = new int[n0];
            var indeg = new int[n0];
            foreach (var l in dag) foreach (var w in l) indeg[w]++;
            var queue = new Queue<int>(Enumerable.Range(0, n0).Where(v => indeg[v] == 0));
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                foreach (var w in dag[v])
                {
                    layer[w] = Math.Max(layer[w], layer[v] + 1);
                    if (--indeg[w] == 0) queue.Enqueue(w);
                }
            }
            var layers = Enumerable.Range(0, layer.Max() + 1).Select(k => Enumerable.Range(0, n0).Where(v => layer[v] == k).ToList()).ToList();

            // Order within the layers: barycentre sweeps down and up.
            var preds = Enumerable.Range(0, n0).Select(_ => new List<int>()).ToList();
            for (var v = 0; v < n0; v++) foreach (var w in dag[v]) preds[w].Add(v);
            var pos = new double[n0];
            void Number() { foreach (var l in layers) for (var i = 0; i < l.Count; i++) pos[l[i]] = i; }
            Number();
            for (var sweep = 0; sweep < 6; sweep++)
            {
                var forward = sweep % 2 == 0;
                var range = forward ? Enumerable.Range(1, layers.Count - 1) : Enumerable.Range(0, layers.Count - 1).Reverse();
                foreach (var k in range)
                {
                    var l = layers[k];
                    double Bary(int v)
                    {
                        var nb = forward ? preds[v] : dag[v];
                        return nb.Count == 0 ? pos[v] : nb.Average(u => pos[u]);
                    }
                    layers[k] = l.OrderBy(Bary).ThenBy(v => pos[v]).ToList();
                    for (var i = 0; i < layers[k].Count; i++) pos[layers[k][i]] = i;
                }
            }

            // Coordinates: main axis = layers, cross axis = order; gaps from the elements' sizes.
            double Main(SketchObject o) => down ? o.Size.H : o.Size.W;
            double Cross(SketchObject o) => down ? o.Size.W : o.Size.H;
            var mainGap = Math.Max(8, 0.7 * ordered.Average(Main));
            var crossGap = Math.Max(6, 0.4 * ordered.Average(Cross));
            var bounds = ordered.Select(o => o.Bounds()).ToList();
            var startMain = down ? bounds.Min(b => b.Y) : bounds.Min(b => b.X);
            var centreCross = down ? (bounds.Min(b => b.X) + bounds.Max(b => b.X + b.W)) / 2 : (bounds.Min(b => b.Y) + bounds.Max(b => b.Y + b.H)) / 2;
            var result = new Dictionary<string, Vec2>(StringComparer.Ordinal);
            var at = startMain;
            foreach (var l in layers)
            {
                var depth = l.Max(v => Main(ordered[v]));
                var width = l.Sum(v => Cross(ordered[v])) + crossGap * (l.Count - 1);
                var c = centreCross - width / 2;
                foreach (var v in l)
                {
                    var o = ordered[v];
                    var mid = c + Cross(o) / 2;
                    var main = at + depth / 2;
                    result[o.Id] = down ? new Vec2(Math.Round(mid, 6), Math.Round(main, 6)) : new Vec2(Math.Round(main, 6), Math.Round(mid, 6));
                    c += Cross(o) + crossGap;
                }
                at += depth + mainGap;
            }
            return result;
        }

        /// <summary>
        /// A connector between two laid-out elements docked on their facing sides: its first docked end on the middle of the
        /// source's bottom (right when not <paramref name="down"/>), its last on the middle of the target's top (left), so
        /// routed connectors run between the elements instead of through them; null if it joins no two of
        /// <paramref name="ids"/>. Ends on a named anchor (a port) stay.
        /// </summary>
        public static SketchObject? Redock(SketchObject line, ISet<string> ids, bool down)
        {
            if (!line.IsPointGeometry || line.Attach is null) return null;
            var att = line.Attach.ToList();
            var first = att.FindIndex(a => a is not null && ids.Contains(a.ObjectId));
            var last = att.FindLastIndex(a => a is not null && ids.Contains(a.ObjectId));
            if (first < 0 || last <= first || att[first]!.ObjectId == att[last]!.ObjectId) return null;
            var changed = false;
            void Set(int i, double u, double v)
            {
                var a = att[i]!;
                if (a.AnchorId is not null || (Math.Abs(a.U - u) < 1e-9 && Math.Abs(a.V - v) < 1e-9)) return;
                att[i] = a with { U = u, V = v };
                changed = true;
            }
            if (down) { Set(first, 0, 0.5); Set(last, 0, -0.5); }
            else { Set(first, 0.5, 0); Set(last, -0.5, 0); }
            return changed ? line with { Attach = att } : line;
        }

        /// <summary>Lines whose first and last docked ends sit on two different elements of <paramref name="ids"/>.</summary>
        public static List<(string From, string To)> Edges(SketchDocument doc, ISet<string> ids)
        {
            var list = new List<(string, string)>();
            foreach (var line in doc.Objects.Values.Where(o => o.IsPointGeometry && o.Attach is not null))
            {
                var docked = line.Attach!.Where(a => a is not null && ids.Contains(a.ObjectId)).ToList();
                if (docked.Count < 2) continue;
                // First and last docked end (a branch in between does not change the direction).
                var first = line.Attach!.First(a => a is not null && ids.Contains(a.ObjectId))!.ObjectId;
                var last = line.Attach!.Last(a => a is not null && ids.Contains(a.ObjectId))!.ObjectId;
                if (first != last) list.Add((first, last));
            }
            return list;
        }
    }
}
