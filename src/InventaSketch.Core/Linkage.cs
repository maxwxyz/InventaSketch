using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Role of an element in a self-built linkage (<see cref="Linkage"/>).</summary>
    public enum LinkageRole
    {
        None,
        /// <summary>Rigid link: its joints (its points) keep their distances.</summary>
        Link,
        /// <summary>Fixed pivot on the frame: its joint never moves.</summary>
        FixedPivot,
        /// <summary>Free joint symbol (revolute joint): moves with the links docked on it.</summary>
        Joint,
        /// <summary>Slider on a fixed guide (prismatic joint): its joint moves along the guide axis.</summary>
        Slider,
    }

    /// <summary>
    /// Self-built linkages: rigid links whose joints are docked onto each other, onto fixed pivots, free joints and sliders
    /// form a chain. Dragging one joint moves the chain while every link keeps its length, fixed pivots stay and sliders
    /// stay on their guide (position-based constraint solving, started from the current position so the chain keeps its
    /// assembly mode). The degree of freedom follows Grübler's formula.
    /// </summary>
    public static class Linkage
    {
        private sealed class Node
        {
            public string Key = "";
            public Vec2 P;
            public bool Fixed;
            /// <summary>Box element carrying the joint (pivot, joint symbol, slider), else null.</summary>
            public string? Box;
            public (Vec2 A, Vec2 B)? Guide;
            /// <summary>Link points (owner, index) sitting at this joint; the first free one stores it.</summary>
            public readonly List<(string Link, int Index)> Members = new();
        }

        private sealed class Chain
        {
            public readonly Dictionary<string, Node> Nodes = new(StringComparer.Ordinal);
            public readonly List<string> Links = new();
            public readonly List<(string A, string B, double L)> Bars = new();
            public readonly Dictionary<(string Link, int Index), string> NodeOf = new();
        }

        /// <summary>True if <paramref name="o"/> takes part in self-built linkages.</summary>
        public static bool IsPart(Registry registry, SketchObject o) => registry.Type(o.Type)?.LinkageRole(o) is { } r && r != LinkageRole.None;

        /// <summary>Links, joints and degree of freedom (Grübler, frame counted as one link) of the chain containing <paramref name="id"/>.</summary>
        public static (int Links, int Joints, int Dof)? Dof(Registry registry, SketchDocument doc, string id, Lang lang = Lang.En)
        {
            var chain = Build(registry, doc, id, lang);
            if (chain is null || chain.Links.Count == 0) return null;
            var frame = chain.Nodes.Values.Any(n => n.Fixed || n.Guide is not null) ? 1 : 0;
            var sliders = chain.Nodes.Values.Count(n => n.Guide is not null);
            var n = chain.Links.Count + frame + sliders;
            var j = 0;
            foreach (var node in chain.Nodes.Values)
            {
                var m = node.Members.Select(x => x.Link).Distinct().Count();
                // Revolute joints: on the frame or a slider every link pairs with it; free joints pair m links with m-1 joints.
                j += node.Fixed || node.Guide is not null ? m : Math.Max(0, m - 1);
                if (node.Guide is not null) j += 1; // the prismatic pair of the slider with the frame
            }
            return (n, j, 3 * (n - 1) - 2 * j);
        }

        /// <summary>
        /// The chain of <paramref name="linkId"/> with its joint <paramref name="index"/> dragged towards <paramref name="target"/>:
        /// links keep their lengths, fixed pivots stay, sliders stay on their guides; the dragged joint ends as close to the
        /// target as the chain allows. Returns the document with the links' points and the joint symbols moved.
        /// </summary>
        public static SketchDocument Drag(Registry registry, SketchDocument doc, string linkId, int index, Vec2 target, Lang lang = Lang.En)
        {
            var chain = Build(registry, doc, linkId, lang);
            if (chain is null || !chain.NodeOf.TryGetValue((linkId, index), out var drag)) return doc;
            var pos = chain.Nodes.ToDictionary(kv => kv.Key, kv => kv.Value.P, StringComparer.Ordinal);
            var dn = chain.Nodes[drag];
            if (dn.Fixed) return doc;
            void Project(string key)
            {
                var n = chain.Nodes[key];
                if (n.Fixed) pos[key] = n.P;
                else if (n.Guide is { } g) pos[key] = Geo.ProjectOnLine(pos[key], g.A, g.B);
            }
            // 1) Pull the dragged joint to the target while the bars hold the rest; 2) let it go to restore exact lengths.
            for (var phase = 0; phase < 2; phase++)
            {
                for (var it = 0; it < (phase == 0 ? 120 : 200); it++)
                {
                    if (phase == 0) { pos[drag] = target; Project(drag); }
                    foreach (var (a, b, l) in chain.Bars)
                    {
                        double wa = chain.Nodes[a].Fixed || phase == 0 && a == drag ? 0 : 1;
                        double wb = chain.Nodes[b].Fixed || phase == 0 && b == drag ? 0 : 1;
                        if (wa + wb == 0) continue;
                        var d = pos[b] - pos[a];
                        var len = d.Length;
                        if (len < 1e-9) continue;
                        var corr = d * ((len - l) / len);
                        pos[a] += corr * (wa / (wa + wb));
                        pos[b] -= corr * (wb / (wa + wb));
                        Project(a);
                        Project(b);
                    }
                }
            }
            return Apply(doc, chain, pos);
        }

        /// <summary>Writes joint positions back: the storing point of every link, joint symbols and sliders moved.</summary>
        private static SketchDocument Apply(SketchDocument doc, Chain chain, Dictionary<string, Vec2> pos)
        {
            var result = doc;
            foreach (var kv in chain.Nodes)
            {
                var key = kv.Key;
                var node = kv.Value;
                var p = pos[key];
                if (p.DistanceTo(node.P) < 1e-9) continue;
                if (node.Box is { } box)
                {
                    if (node.Fixed) continue;
                    var d = p - node.P;
                    result = Edits.Update(result, new[] { box }, o => o with { Transform = o.Transform with { X = Math.Round(o.Transform.X + d.X, 6), Y = Math.Round(o.Transform.Y + d.Y, 6) } });
                    continue;
                }
                // The member whose point is not docked holds the joint; docked members follow it.
                foreach (var (link, i) in node.Members)
                {
                    var o = result.Get(link)!;
                    var docked = o.Attach is not null && i < o.Attach.Count && o.Attach[i] is not null;
                    if (docked) continue;
                    result = Edits.Update(result, new[] { link }, x =>
                    {
                        var pts = x.Points!.ToList();
                        pts[i] = new Vec2(Math.Round(p.X, 6), Math.Round(p.Y, 6));
                        return x with { Points = pts };
                    });
                }
            }
            return result;
        }

        /// <summary>The chain containing <paramref name="id"/> (links connected through docked joints), or null.</summary>
        private static Chain? Build(Registry registry, SketchDocument doc, string id, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            LinkageRole RoleOf(SketchObject o) => registry.Type(o.Type)?.LinkageRole(o) ?? LinkageRole.None;
            var links = doc.Objects.Values.Where(o => o.IsPointGeometry && RoleOf(o) == LinkageRole.Link && o.Points is { Count: >= 2 }).ToList();
            if (links.Count == 0) return null;
            // Node key of a link point: follow docks onto other links' joints and onto joint elements.
            string KeyOf(SketchObject link, int i, int depth = 0)
            {
                var a = link.Attach is not null && i < link.Attach.Count ? link.Attach[i] : null;
                if (a is not null && doc.Get(a.ObjectId) is { } t)
                {
                    if (t.IsPointGeometry && RoleOf(t) == LinkageRole.Link && Attachments.PointIndex(a.AnchorId) is int k && depth < 8) return KeyOf(t, k, depth + 1);
                    if (!t.IsPointGeometry && RoleOf(t) is LinkageRole.FixedPivot or LinkageRole.Joint or LinkageRole.Slider) return "box:" + t.Id;
                }
                return link.Id + "#" + i;
            }
            var all = new Chain();
            foreach (var link in links)
            {
                var pts = doc.ResolvePoints(link);
                var keys = new List<string>();
                for (var i = 0; i < pts.Count; i++)
                {
                    var key = KeyOf(link, i);
                    if (!all.Nodes.TryGetValue(key, out var node))
                    {
                        node = new Node { Key = key, P = pts[i] };
                        if (key.StartsWith("box:", StringComparison.Ordinal))
                        {
                            var box = doc.Get(key.Substring(4))!;
                            node.Box = box.Id;
                            var role = RoleOf(box);
                            node.Fixed = role == LinkageRole.FixedPivot;
                            if (role == LinkageRole.Slider && registry.PageAnchors(box, ctx).FirstOrDefault(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis) is { } ax)
                                node.Guide = (ax.P, ax.Q);
                        }
                        all.Nodes[key] = node;
                    }
                    node.Members.Add((link.Id, i));
                    all.NodeOf[(link.Id, i)] = key;
                    keys.Add(key);
                }
                for (var i = 0; i < keys.Count; i++)
                    for (var j = i + 1; j < keys.Count; j++)
                        if (keys[i] != keys[j]) all.Bars.Add((keys[i], keys[j], pts[i].DistanceTo(pts[j])));
            }
            // The connected chain of id (id may be a link or a joint element).
            var start = all.NodeOf.Where(kv => kv.Key.Link == id).Select(kv => kv.Value).FirstOrDefault() ?? (all.Nodes.ContainsKey("box:" + id) ? "box:" + id : null);
            if (start is null) return null;
            var keep = new HashSet<string>(StringComparer.Ordinal) { start };
            var queue = new Queue<string>(keep);
            while (queue.Count > 0)
            {
                var k = queue.Dequeue();
                foreach (var (a, b, _) in all.Bars)
                {
                    if (a == k && keep.Add(b)) queue.Enqueue(b);
                    if (b == k && keep.Add(a)) queue.Enqueue(a);
                }
            }
            var chain = new Chain();
            foreach (var k in keep) chain.Nodes[k] = all.Nodes[k];
            foreach (var bar in all.Bars.Where(b => keep.Contains(b.A))) chain.Bars.Add(bar);
            foreach (var kv in all.NodeOf.Where(kv => keep.Contains(kv.Value))) chain.NodeOf[kv.Key] = kv.Value;
            chain.Links.AddRange(chain.NodeOf.Keys.Select(k => k.Link).Distinct());
            return chain;
        }
    }
}
