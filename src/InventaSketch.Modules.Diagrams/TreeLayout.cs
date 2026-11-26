using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Tree layouts of connected diagram elements (fault trees, mind maps): a tidy top-down tree, a left-right tree with the
    /// branches on one or both sides of the root, and a radial tree. The layouts only compute new centres; the callers move
    /// the elements, and docked connectors follow them. Elements reached twice (shared causes) are placed at their first
    /// visit; cycles stop there.
    /// </summary>
    public static class TreeLayout
    {
        /// <summary>A tree node with its children in drawing order.</summary>
        public sealed class Node
        {
            public Node(SketchObject obj) { Obj = obj; }
            public SketchObject Obj { get; }
            public List<Node> Children { get; } = new();
            public double W => Obj.Bounds().W;
            public double H => Obj.Bounds().H;
            public Vec2 Centre => new(Obj.Transform.X, Obj.Transform.Y);
        }

        /// <summary>
        /// The tree below <paramref name="rootId"/>: <paramref name="children"/> gives the child ids of an element (box elements
        /// only); the children are ordered by <paramref name="order"/> of their current centres (relative to the parent).
        /// </summary>
        public static Node? Build(SketchDocument doc, string rootId, Func<string, IEnumerable<string>> children, Func<Vec2, double> order)
        {
            if (doc.Get(rootId) is not { IsPointGeometry: false } root) return null;
            var seen = new HashSet<string>(StringComparer.Ordinal) { rootId };
            var top = new Node(root);
            var queue = new Queue<Node>();
            queue.Enqueue(top);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                var kids = children(n.Obj.Id).Where(id => !seen.Contains(id)).Select(doc.Get)
                    .Where(o => o is { IsPointGeometry: false }).Select(o => o!)
                    .OrderBy(o => order(new Vec2(o.Transform.X, o.Transform.Y) - n.Centre)).ToList();
                foreach (var k in kids)
                {
                    if (!seen.Add(k.Id)) continue;
                    var child = new Node(k);
                    n.Children.Add(child);
                    queue.Enqueue(child);
                }
            }
            return top;
        }

        /// <summary>
        /// Tidy top-down tree: the levels are stacked with <paramref name="vgap"/> (each level as tall as its tallest element),
        /// siblings side by side with <paramref name="hgap"/>, every parent centred above its children. The root keeps its
        /// top edge and horizontal centre.
        /// </summary>
        public static Dictionary<string, Vec2> Down(Node root, double hgap, double vgap)
        {
            var levels = new List<double>();
            void Levels(Node n, int d)
            {
                while (levels.Count <= d) levels.Add(0);
                levels[d] = Math.Max(levels[d], n.H);
                foreach (var c in n.Children) Levels(c, d + 1);
            }
            Levels(root, 0);
            var widths = new Dictionary<Node, double>();
            double Width(Node n)
            {
                var span = n.Children.Sum(Width) + hgap * Math.Max(0, n.Children.Count - 1);
                return widths[n] = Math.Max(n.W, span);
            }
            Width(root);
            var tops = new List<double> { root.Centre.Y - root.H / 2 };
            for (var d = 1; d < levels.Count; d++) tops.Add(tops[d - 1] + levels[d - 1] + vgap);
            var result = new Dictionary<string, Vec2>(StringComparer.Ordinal);
            double Place(Node n, double left, int d)
            {
                double x;
                if (n.Children.Count == 0) x = left + widths[n] / 2;
                else
                {
                    var span = n.Children.Sum(c => widths[c]) + hgap * (n.Children.Count - 1);
                    var at = left + (widths[n] - span) / 2;
                    var xs = new List<double>();
                    foreach (var c in n.Children)
                    {
                        xs.Add(Place(c, at, d + 1));
                        at += widths[c] + hgap;
                    }
                    x = (xs[0] + xs[xs.Count - 1]) / 2;
                }
                // Top-aligned within the level, so the links from one parent share their horizontal run.
                result[n.Obj.Id] = new Vec2(x, tops[d] + n.H / 2);
                return x;
            }
            Place(root, 0, 0);
            // The root keeps its top edge (its level is as tall as it) and its horizontal centre.
            var shift = new Vec2(root.Centre.X - result[root.Obj.Id].X, root.Centre.Y - result[root.Obj.Id].Y);
            return Round(result.ToDictionary(kv => kv.Key, kv => kv.Value + shift, StringComparer.Ordinal));
        }

        /// <summary>
        /// Left-right tree (mind map): the root stays; its branches go to the right (<paramref name="sides"/> = 1), or to both
        /// sides (2: branches already on the left stay left, the others right; if all are on one side, the lower half goes
        /// left). Every child stands <paramref name="hgap"/> beside its parent, siblings are stacked with
        /// <paramref name="vgap"/> and centred on their parent.
        /// </summary>
        public static Dictionary<string, Vec2> Sideways(Node root, int sides, double hgap, double vgap)
        {
            var result = new Dictionary<string, Vec2>(StringComparer.Ordinal) { [root.Obj.Id] = root.Centre };
            var heights = new Dictionary<Node, double>();
            double Height(Node n)
            {
                var span = n.Children.Sum(Height) + vgap * Math.Max(0, n.Children.Count - 1);
                return heights[n] = Math.Max(n.H, span);
            }
            foreach (var c in root.Children) Height(c);
            void Stack(Node parent, Vec2 at, IReadOnlyList<Node> kids, int dir)
            {
                var span = kids.Sum(k => heights[k]) + vgap * Math.Max(0, kids.Count - 1);
                var y = at.Y - span / 2;
                foreach (var k in kids)
                {
                    var c = new Vec2(at.X + dir * (parent.W / 2 + hgap + k.W / 2), y + heights[k] / 2);
                    result[k.Obj.Id] = c;
                    Stack(k, c, k.Children, dir);
                    y += heights[k] + vgap;
                }
            }
            var right = new List<Node>();
            var left = new List<Node>();
            if (sides <= 1) right.AddRange(root.Children);
            else
            {
                var cx = root.Centre.X;
                var onLeft = root.Children.Where(k => k.Centre.X < cx - 1e-6).ToList();
                if (onLeft.Count == 0 || onLeft.Count == root.Children.Count)
                {
                    var byY = root.Children.OrderBy(k => k.Centre.Y).ToList();
                    var half = (byY.Count + 1) / 2;
                    right.AddRange(byY.Take(half));
                    left.AddRange(byY.Skip(half));
                }
                else
                {
                    left.AddRange(onLeft.OrderBy(k => k.Centre.Y));
                    right.AddRange(root.Children.Except(onLeft).OrderBy(k => k.Centre.Y));
                }
            }
            Stack(root, root.Centre, right, 1);
            Stack(root, root.Centre, left, -1);
            return Round(result);
        }

        /// <summary>
        /// Radial tree: the root stays in the middle, depth d on a circle of radius d·R; every subtree gets a wedge in
        /// proportion to its number of leaves, starting on the right and going clockwise. R grows until neighbouring
        /// elements on the first circle keep <paramref name="gap"/> between them.
        /// </summary>
        public static Dictionary<string, Vec2> Radial(Node root, double gap)
        {
            var result = new Dictionary<string, Vec2>(StringComparer.Ordinal) { [root.Obj.Id] = root.Centre };
            if (root.Children.Count == 0) return result;
            var leaves = new Dictionary<Node, int>();
            int Leaves(Node n) => leaves[n] = n.Children.Count == 0 ? 1 : n.Children.Sum(Leaves);
            var total = Leaves(root);
            var all = new List<Node>();
            void Collect(Node n) { foreach (var c in n.Children) { all.Add(c); Collect(c); } }
            Collect(root);
            var size = all.Max(n => Math.Sqrt(n.W * n.W + n.H * n.H));
            var rootSize = Math.Sqrt(root.W * root.W + root.H * root.H);
            // Ring spacing: clear of the root and of the next ring; the leaves on the outer circles need room side by side.
            var r = Math.Max(rootSize / 2 + size / 2 + gap, size + gap);
            r = Math.Max(r, total * (size * 0.8 + gap) / (2 * Math.PI));
            void Place(Node n, double from, double to, int depth)
            {
                var at = from;
                foreach (var c in n.Children)
                {
                    var span = (to - from) * leaves[c] / leaves[n];
                    var a = at + span / 2;
                    result[c.Obj.Id] = root.Centre + new Vec2(Math.Cos(a), Math.Sin(a)) * (r * depth);
                    Place(c, at, at + span, depth + 1);
                    at += span;
                }
            }
            Place(root, 0, 2 * Math.PI, 1);
            return Round(result);
        }

        /// <summary>Exit and entry directions of a link from element <c>a</c> to element <c>b</c> after a layout.</summary>
        public delegate (Vec2 FromDir, Vec2 ToDir) LinkSides(Vec2 a, Vec2 b);

        /// <summary>Tree drawn downwards: the upper end leaves at the bottom, the lower end enters at the top.</summary>
        public static (Vec2, Vec2) Vertical(Vec2 a, Vec2 b) => b.Y >= a.Y ? (new Vec2(0, 1), new Vec2(0, -1)) : (new Vec2(0, -1), new Vec2(0, 1));

        /// <summary>Branches to the sides: the ends face each other horizontally.</summary>
        public static (Vec2, Vec2) Horizontal(Vec2 a, Vec2 b) => b.X >= a.X ? (new Vec2(1, 0), new Vec2(-1, 0)) : (new Vec2(-1, 0), new Vec2(1, 0));

        /// <summary>Radial branches: the ends face each other along the line between the centres.</summary>
        public static (Vec2, Vec2) Facing(Vec2 a, Vec2 b)
        {
            var d = b - a;
            if (d.Length < 1e-9) return (new Vec2(1, 0), new Vec2(-1, 0));
            var n = d.Normalized();
            return (n, n * -1);
        }

        /// <summary>
        /// The document with every element of <paramref name="centres"/> moved to its new centre, the whole layout shifted
        /// onto its page if it would run past an edge (4 mm margin; a layout wider than the page starts at the left margin).
        /// Lines of <paramref name="lineType"/> joining two moved elements are docked again on the ports facing each other
        /// (<paramref name="sides"/>); docked connectors follow their elements.
        /// </summary>
        public static SketchDocument Apply(SketchDocument doc, IReadOnlyDictionary<string, Vec2> centres, Registry? registry, string lineType, LinkSides sides)
        {
            var moved = centres.Where(kv => doc.Get(kv.Key) is { IsPointGeometry: false }).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            if (moved.Count == 0) return doc;
            var shift = OntoPage(doc, moved);
            var ids = moved.Keys.Where(id => doc.Get(id) is { } o && (Math.Abs(o.Transform.X - moved[id].X - shift.X) > 1e-9 || Math.Abs(o.Transform.Y - moved[id].Y - shift.Y) > 1e-9)).ToList();
            if (ids.Count == 0) return doc;
            doc = Edits.Update(doc, ids, o => o with { Transform = o.Transform with { X = Math.Round(moved[o.Id].X + shift.X, 6), Y = Math.Round(moved[o.Id].Y + shift.Y, 6) } });
            if (registry is null) return doc;
            var ctx = new RenderContext(doc).For(registry);
            var lines = doc.Objects.Values.Where(l => l.Type == lineType && l.Attach is not null && l.Points is not null).ToList();
            var updates = new List<SketchObject>();
            foreach (var line in lines)
            {
                var att = line.Attach!.ToList();
                var first = att.FindIndex(a => a is not null && moved.ContainsKey(a.ObjectId));
                var last = att.FindLastIndex(a => a is not null && moved.ContainsKey(a.ObjectId));
                if (first < 0 || last <= first || last >= line.Points!.Count || att[first]!.ObjectId == att[last]!.ObjectId) continue;
                var a0 = doc.Get(att[first]!.ObjectId)!;
                var b0 = doc.Get(att[last]!.ObjectId)!;
                var (da, db) = sides(new Vec2(a0.Transform.X, a0.Transform.Y), new Vec2(b0.Transform.X, b0.Transform.Y));
                var (pa, ppa) = Flow.Facing(registry, a0, ctx, da);
                var (pb, ppb) = Flow.Facing(registry, b0, ctx, db);
                var pts = line.Points!.ToList();
                att[first] = Attachments.For(a0, ppa, pa);
                att[last] = Attachments.For(b0, ppb, pb);
                pts[first] = ppa;
                pts[last] = ppb;
                updates.Add(line with { Attach = att, Points = pts });
            }
            foreach (var u in updates) doc = Edits.Replace(doc, u);
            return Attachments.Refresh(registry, doc);
        }

        /// <summary>Shift that keeps the laid-out elements on their page (zero if they fit or no page is known).</summary>
        private static Vec2 OntoPage(SketchDocument doc, IReadOnlyDictionary<string, Vec2> centres)
        {
            var page = doc.PageOf(centres.Keys.First());
            if (page is null) return Vec2.Zero;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var kv in centres)
            {
                var b = doc.Get(kv.Key)!.Bounds();
                x0 = Math.Min(x0, kv.Value.X - b.W / 2); x1 = Math.Max(x1, kv.Value.X + b.W / 2);
                y0 = Math.Min(y0, kv.Value.Y - b.H / 2); y1 = Math.Max(y1, kv.Value.Y + b.H / 2);
            }
            const double margin = 4;
            double Shift(double lo, double hi, double size)
                => hi - lo > size - 2 * margin ? margin - lo : lo < margin ? margin - lo : hi > size - margin ? size - margin - hi : 0;
            return new Vec2(Shift(x0, x1, page.Size.W), Shift(y0, y1, page.Size.H));
        }

        private static Dictionary<string, Vec2> Round(Dictionary<string, Vec2> d)
            => d.ToDictionary(kv => kv.Key, kv => new Vec2(Math.Round(kv.Value.X, 6), Math.Round(kv.Value.Y, 6)), StringComparer.Ordinal);

        /// <summary>Angle of a direction in [0, 2π), clockwise on screen from the right (orders siblings around a parent).</summary>
        public static double Angle(Vec2 d)
        {
            var a = Math.Atan2(d.Y, d.X);
            return a < 0 ? a + 2 * Math.PI : a;
        }
    }
}
