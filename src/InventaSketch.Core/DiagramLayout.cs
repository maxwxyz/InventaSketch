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
            var bounds = ordered.Select(o => o.Bounds()).ToList();
            var startMain = down ? bounds.Min(b => b.Y) : bounds.Min(b => b.X);
            var centreCross = down ? (bounds.Min(b => b.X) + bounds.Max(b => b.X + b.W)) / 2 : (bounds.Min(b => b.Y) + bounds.Max(b => b.Y + b.H)) / 2;
            Dictionary<string, Vec2> Place(double mainGap, double crossGap)
            {
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
                        result[o.Id] = down ? new Vec2(mid, main) : new Vec2(main, mid);
                        c += Cross(o) + crossGap;
                    }
                    at += depth + mainGap;
                }
                return result;
            }
            Rect Extent(Dictionary<string, Vec2> r)
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                foreach (var o in ordered)
                {
                    var c = r[o.Id];
                    x0 = Math.Min(x0, c.X - o.Size.W / 2); x1 = Math.Max(x1, c.X + o.Size.W / 2);
                    y0 = Math.Min(y0, c.Y - o.Size.H / 2); y1 = Math.Max(y1, c.Y + o.Size.H / 2);
                }
                return new Rect(x0, y0, x1 - x0, y1 - y0);
            }
            // Gaps from the elements' sizes; on a page the diagram should fit, the gaps shrink (down to 3 mm) until it does,
            // and it is shifted onto the page (a long chain left to right would otherwise run past the edge).
            var page = doc.PageOf(ordered[0].Id);
            double mainGap0 = Math.Max(8, 0.7 * ordered.Average(Main)), crossGap0 = Math.Max(6, 0.4 * ordered.Average(Cross));
            var placed = Place(mainGap0, crossGap0);
            if (page is not null)
            {
                const double margin = 4;
                double pw = page.Size.W - 2 * margin, ph = page.Size.H - 2 * margin;
                for (var f = 0.8; f >= 0.05; f -= 0.15)
                {
                    var e = Extent(placed);
                    if (e.W <= pw && e.H <= ph) break;
                    placed = Place(Math.Max(3, mainGap0 * f), Math.Max(3, crossGap0 * f));
                }
                var ext = Extent(placed);
                double dx = 0, dy = 0;
                // Too large even with small gaps: it starts at the left/top margin rather than running past both edges.
                dx = ext.W > pw ? margin - ext.X : ext.X < margin ? margin - ext.X : ext.X + ext.W > page.Size.W - margin ? page.Size.W - margin - (ext.X + ext.W) : 0;
                dy = ext.H > ph ? margin - ext.Y : ext.Y < margin ? margin - ext.Y : ext.Y + ext.H > page.Size.H - margin ? page.Size.H - margin - (ext.Y + ext.H) : 0;
                if (dx != 0 || dy != 0) placed = placed.ToDictionary(kv => kv.Key, kv => kv.Value + new Vec2(dx, dy), StringComparer.Ordinal);
            }
            return placed.ToDictionary(kv => kv.Key, kv => new Vec2(Math.Round(kv.Value.X, 6), Math.Round(kv.Value.Y, 6)), StringComparer.Ordinal);
        }

        /// <summary>
        /// A connector between two laid-out elements docked on the sides a drawn flowchart uses (<see cref="Sides"/>):
        /// mostly the source's bottom and the target's top (right and left when not <paramref name="down"/>), side exits for
        /// branches, and both ends on one side for long jumps and loops back, so routed connectors run around the elements
        /// instead of through them. An end docked on a named anchor (a port) moves to the element's
        /// named point anchor nearest to that side; other ends to the side's middle. Null if it joins no two of
        /// <paramref name="ids"/>; the same object if nothing changes.
        /// </summary>
        public static SketchObject? Redock(Registry registry, SketchDocument doc, SketchObject line, ISet<string> ids, bool down)
        {
            if (!line.IsPointGeometry || line.Attach is null) return null;
            var att = line.Attach.ToList();
            var first = att.FindIndex(a => a is not null && ids.Contains(a.ObjectId));
            var last = att.FindLastIndex(a => a is not null && ids.Contains(a.ObjectId));
            if (first < 0 || last <= first || att[first]!.ObjectId == att[last]!.ObjectId) return null;
            var ctx = new RenderContext(doc).For(registry);
            var changed = false;
            void Set(int i, double u, double v)
            {
                var a = att[i]!;
                if (doc.Get(a.ObjectId) is not { IsPointGeometry: false } target) return;
                var next = a with { U = u, V = v };
                if (a.AnchorId is not null)
                {
                    // The named anchor nearest to the side's middle (a flow step's "bottom" port, a class's side port …).
                    var want = target.BoxPoint(u, v);
                    var best = registry.PageAnchors(target, ctx).Where(x => x.Kind == AnchorKind.Point && x.Id is not null && x.Role is AnchorRole.Port or AnchorRole.EdgeMid)
                        .OrderBy(x => x.P.DistanceTo(want)).FirstOrDefault();
                    if (best is null) return;
                    var (bu, bv) = target.ToBox(best.P);
                    next = a with { AnchorId = best.Id, U = bu, V = bv };
                }
                if (next == a) return;
                att[i] = next;
                changed = true;
            }
            var (exit, entry) = Sides(doc, att[first]!.ObjectId, att[last]!.ObjectId, ids, down);
            Set(first, exit.X * 0.5, exit.Y * 0.5);
            Set(last, entry.X * 0.5, entry.Y * 0.5);
            var result = changed ? line with { Attach = att } : line;
            // Both ends on one side: the line runs around the column; connectors with a clearance parameter keep clear of
            // every element in between (a wider step beside the loop).
            if (exit == entry && registry.Type(line.Type)?.Parameters.Any(p => p.Key == "clearance") == true
                && Clearance(doc, att[first]!.ObjectId, att[last]!.ObjectId, ids, exit) is double need)
            {
                var have = line.Num("clearance", 8);
                if (Math.Abs(need - have) > 0.25) result = result.WithParam("clearance", need);
            }
            return ReferenceEquals(result, line) ? line : result;
        }

        /// <summary>
        /// Clearance (mm) a connector leaving <paramref name="from"/> and entering <paramref name="to"/> on the same side
        /// <paramref name="side"/> needs so its run beside them passes every element of <paramref name="ids"/> in between
        /// with 4 mm to spare (at least 8 mm).
        /// </summary>
        public static double? Clearance(SketchDocument doc, string from, string to, ISet<string> ids, Vec2 side)
        {
            if (doc.Get(from) is not { IsPointGeometry: false } src || doc.Get(to) is not { IsPointGeometry: false } dst) return null;
            var horizontalSide = Math.Abs(side.X) > 0.5;
            var sb = src.Bounds(); var tb = dst.Bounds();
            // Span along the run (perpendicular to the side) and the ends' outer coordinate on the side.
            double lo = horizontalSide ? Math.Min(sb.Y + sb.H / 2, tb.Y + tb.H / 2) : Math.Min(sb.X + sb.W / 2, tb.X + tb.W / 2);
            double hi = horizontalSide ? Math.Max(sb.Y + sb.H / 2, tb.Y + tb.H / 2) : Math.Max(sb.X + sb.W / 2, tb.X + tb.W / 2);
            double Outer(Rect r) => horizontalSide ? (side.X > 0 ? r.Right : r.X) : (side.Y > 0 ? r.Bottom : r.Y);
            var ends = side.X + side.Y > 0 ? Math.Max(Outer(sb), Outer(tb)) : Math.Min(Outer(sb), Outer(tb));
            var extreme = ends;
            foreach (var o in ids.Select(doc.Get))
            {
                if (o is not { IsPointGeometry: false }) continue;
                var r = o.Bounds();
                var (a, b) = horizontalSide ? (r.Y, r.Bottom) : (r.X, r.Right);
                if (b < lo || a > hi) continue;
                extreme = side.X + side.Y > 0 ? Math.Max(extreme, Outer(r)) : Math.Min(extreme, Outer(r));
            }
            return Math.Max(8, Math.Round((Math.Abs(extreme - ends) + 4) * 2) / 2);
        }

        /// <summary>
        /// The sides a connector leaves its source and enters its target on (outward unit vectors of the two boxes), as in a
        /// drawn flowchart (layout "down"; "right" is the same turned): a target straight below enters from the top through
        /// the source's bottom; a target below and to the side is reached from the source's side and entered from the top
        /// (one bend, the branch of a decision); a target straight below with other elements in between is reached around
        /// them on the right ("yes" exit to a later step); a target above or beside it (a loop back) leaves and enters on
        /// the left, so the routed line runs around the column.
        /// </summary>
        public static (Vec2 Exit, Vec2 Entry) Sides(SketchDocument doc, string from, string to, ISet<string> ids, bool down)
        {
            var m = down ? new Vec2(0, 1) : new Vec2(1, 0);
            var c = down ? new Vec2(1, 0) : new Vec2(0, 1);
            if (doc.Get(from) is not { IsPointGeometry: false } src || doc.Get(to) is not { IsPointGeometry: false } dst) return (m, -1 * m);
            double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
            var sb = src.Bounds(); var tb = dst.Bounds();
            var sc = new Vec2(sb.X + sb.W / 2, sb.Y + sb.H / 2); var tc = new Vec2(tb.X + tb.W / 2, tb.Y + tb.H / 2);
            var along = Dot(tc - sc, m); var across = Dot(tc - sc, c);
            double HalfAlong(Rect r) => down ? r.H / 2 : r.W / 2;
            double HalfAcross(Rect r) => down ? r.W / 2 : r.H / 2;
            if (along > HalfAlong(sb) + HalfAlong(tb) * 0.5)
            {
                if (Math.Abs(across) <= Math.Min(HalfAcross(sb), HalfAcross(tb)) * 0.5)
                {
                    // Straight on, unless other elements stand in the corridor: then around them on the right.
                    var corridor = down
                        ? new Rect(Math.Min(sc.X, tc.X) - 0.5, sb.Bottom, Math.Abs(tc.X - sc.X) + 1, Math.Max(0, tb.Y - sb.Bottom))
                        : new Rect(sb.Right, Math.Min(sc.Y, tc.Y) - 0.5, Math.Max(0, tb.X - sb.Right), Math.Abs(tc.Y - sc.Y) + 1);
                    var blocked = ids.Where(id => id != from && id != to).Select(doc.Get)
                        .Any(o => o is { IsPointGeometry: false } && o.Bounds().Intersects(corridor));
                    return blocked ? (c, c) : (m, -1 * m);
                }
                return (across > 0 ? c : -1 * c, -1 * m);
            }
            if (Math.Abs(along) <= (HalfAlong(sb) + HalfAlong(tb)) * 0.5 && Math.Abs(across) > 1e-6)
                return (across > 0 ? c : -1 * c, across > 0 ? -1 * c : c);
            // A loop back to an earlier element: out on the left, in on the left.
            return (-1 * c, -1 * c);
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
