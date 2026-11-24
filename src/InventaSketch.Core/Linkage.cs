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
    /// form a chain. A joint docked onto a slot of another link (<see cref="SlotPrefix"/>, a rail along one of its edges)
    /// slides along that link (pin in slot, a sliding block in a slotted link). Dragging one joint moves the chain while every link keeps its length, fixed pivots stay and sliders
    /// stay on their guide (position-based constraint solving, started from the current position so the chain keeps its
    /// assembly mode). The degree of freedom follows Grübler's formula.
    /// </summary>
    public static class Linkage
    {
        /// <summary>Anchor id prefix of the slots of links ("slot0" = the edge from point 0 to point 1).</summary>
        public const string SlotPrefix = "slot";

        /// <summary>Edge index of a slot anchor id ("slot2" → 2), else null.</summary>
        public static int? SlotIndex(string? anchorId)
        {
            if (anchorId is null || !anchorId.StartsWith(SlotPrefix, StringComparison.Ordinal) || anchorId.Length == SlotPrefix.Length) return null;
            return int.TryParse(anchorId.Substring(SlotPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : null;
        }

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
            /// <summary>Pin-in-slot pairs: joint <c>P</c> stays on the line through the joints <c>A</c> and <c>B</c>.</summary>
            public readonly List<(string P, string A, string B)> Slots = new();
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
            // A pin in a slot is a higher pair: it removes one degree of freedom.
            var h = chain.Slots.Count;
            var j = 0;
            foreach (var node in chain.Nodes.Values)
            {
                var m = node.Members.Select(x => x.Link).Distinct().Count();
                // Revolute joints: on the frame or a slider every link pairs with it; free joints pair m links with m-1 joints.
                j += node.Fixed || node.Guide is not null ? m : Math.Max(0, m - 1);
                if (node.Guide is not null) j += 1; // the prismatic pair of the slider with the frame
            }
            return (n, j + h, 3 * (n - 1) - 2 * j - h);
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
            double Weight(string key, int phase) => chain.Nodes[key].Fixed || phase == 0 && key == drag ? 0 : 1;
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
                    // Pins in slots: distance of the pin to the slot line → 0, shared by the pin and the slot's two joints.
                    foreach (var (sp, sa, sb) in chain.Slots)
                    {
                        double wp = Weight(sp, phase), wa = Weight(sa, phase), wb = Weight(sb, phase);
                        var a = pos[sa];
                        var d = pos[sb] - a;
                        var len = d.Length;
                        if (len < 1e-9) continue;
                        var u = d * (1 / len);
                        var nrm = u.Perp;
                        var t = (pos[sp] - a).Dot(u) / len;
                        var dist = (pos[sp] - a).Dot(nrm);
                        var denom = wp + wa * (1 - t) * (1 - t) + wb * t * t;
                        if (denom < 1e-12) continue;
                        var lam = dist / denom;
                        pos[sp] -= nrm * (wp * lam);
                        pos[sa] += nrm * (wa * lam * (1 - t));
                        pos[sb] += nrm * (wb * lam * t);
                        Project(sp);
                        Project(sa);
                        Project(sb);
                    }
                }
            }
            return Apply(doc, chain, pos);
        }

        /// <summary>
        /// The path of point <paramref name="index"/> of link <paramref name="linkId"/> while the chain's crank (a link on a
        /// fixed pivot) turns – a coupler curve. The crank turns in <paramref name="steps"/> steps; a crank that cannot go
        /// all the way round (rocker input, dead position) is turned both ways from the current position up to where the
        /// chain stops. Null if the chain has no crank or not exactly one degree of freedom.
        /// </summary>
        public static (IReadOnlyList<Vec2> Points, bool Closed)? Trace(Registry registry, SketchDocument doc, string linkId, int index, int steps = 72, Lang lang = Lang.En)
        {
            if (Dof(registry, doc, linkId, lang) is not { Dof: 1 }) return null;
            var chain = Build(registry, doc, linkId, lang);
            if (chain is null) return null;
            // The crank: the selected link if it sits on a fixed pivot, else the first link that does.
            (string Link, int Tip, Vec2 Pivot)? crank = null;
            foreach (var link in new[] { linkId }.Concat(chain.Links.Where(l => l != linkId)))
            {
                var members = chain.NodeOf.Where(kv => kv.Key.Link == link).ToList();
                var fixedOnes = members.Where(kv => chain.Nodes[kv.Value].Fixed).ToList();
                var free = members.Where(kv => !chain.Nodes[kv.Value].Fixed).ToList();
                if (fixedOnes.Count == 1 && free.Count >= 1)
                {
                    crank = (link, free[0].Key.Index, chain.Nodes[fixedOnes[0].Value].P);
                    break;
                }
            }
            if (crank is not { } c) return null;
            Vec2 Tip(SketchDocument d) => d.ResolvePoints(d.Get(c.Link)!)[c.Tip];
            Vec2 Traced(SketchDocument d) => d.ResolvePoints(d.Get(linkId)!)[index];
            var r = Tip(doc).DistanceTo(c.Pivot);
            if (r < 1e-6) return null;
            var a0 = Math.Atan2(Tip(doc).Y - c.Pivot.Y, Tip(doc).X - c.Pivot.X);
            var step = 2 * Math.PI / Math.Max(8, steps);
            var tol = Math.Max(0.05, r * 0.02);
            List<Vec2> Run(int dir)
            {
                var pts = new List<Vec2>();
                var d = doc;
                for (var k = 1; k <= Math.Max(8, steps); k++)
                {
                    var a = a0 + dir * k * step;
                    var target = c.Pivot + new Vec2(Math.Cos(a), Math.Sin(a)) * r;
                    var next = Drag(registry, d, c.Link, c.Tip, target, lang);
                    if (Tip(next).DistanceTo(target) > tol) break; // the chain stops here (limit, dead position)
                    d = next;
                    pts.Add(Traced(d));
                }
                return pts;
            }
            var forward = Run(1);
            var start = Traced(doc);
            if (forward.Count >= Math.Max(8, steps)) return (new[] { start }.Concat(forward.Take(forward.Count - 1)).ToList(), true);
            var backward = Run(-1);
            backward.Reverse();
            return (backward.Concat(new[] { start }).Concat(forward).ToList(), false);
        }

        /// <summary>
        /// Ghost positions of the chain of <paramref name="linkId"/>: the chain's links (points resolved) at
        /// <paramref name="count"/> crank angles spread over the crank's range (all the way round, or between its limits),
        /// without the current position. Each pose maps link id → page points. Empty without a crank or with F ≠ 1.
        /// </summary>
        public static IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyList<Vec2>>> Poses(Registry registry, SketchDocument doc, string linkId, int count = 4, Lang lang = Lang.En)
        {
            var result = new List<IReadOnlyDictionary<string, IReadOnlyList<Vec2>>>();
            if (count < 1 || Dof(registry, doc, linkId, lang) is not { Dof: 1 }) return result;
            var chain = Build(registry, doc, linkId, lang);
            if (chain is null) return result;
            (string Link, int Tip, Vec2 Pivot)? crank = null;
            foreach (var link in new[] { linkId }.Concat(chain.Links.Where(l => l != linkId)))
            {
                var members = chain.NodeOf.Where(kv => kv.Key.Link == link).ToList();
                var fixedOnes = members.Where(kv => chain.Nodes[kv.Value].Fixed).ToList();
                var free = members.Where(kv => !chain.Nodes[kv.Value].Fixed).ToList();
                if (fixedOnes.Count == 1 && free.Count >= 1) { crank = (link, free[0].Key.Index, chain.Nodes[fixedOnes[0].Value].P); break; }
            }
            if (crank is not { } c) return result;
            var tip0 = doc.ResolvePoints(doc.Get(c.Link)!)[c.Tip];
            var r = tip0.DistanceTo(c.Pivot);
            if (r < 1e-6) return result;
            var a0 = Math.Atan2(tip0.Y - c.Pivot.Y, tip0.X - c.Pivot.X);
            const int Fine = 72;
            var step = 2 * Math.PI / Fine;
            var tol = Math.Max(0.05, r * 0.02);
            // Walk the crank in small steps (the chain keeps its assembly mode) and remember every reachable pose.
            List<SketchDocument> Walk(int dir)
            {
                var list = new List<SketchDocument>();
                var d = doc;
                for (var k = 1; k < Fine; k++)
                {
                    var a = a0 + dir * k * step;
                    var target = c.Pivot + new Vec2(Math.Cos(a), Math.Sin(a)) * r;
                    var next = Drag(registry, d, c.Link, c.Tip, target, lang);
                    if (next.ResolvePoints(next.Get(c.Link)!)[c.Tip].DistanceTo(target) > tol) break;
                    d = next;
                    list.Add(d);
                }
                return list;
            }
            var forward = Walk(1);
            List<SketchDocument> range;
            if (forward.Count >= Fine - 1) range = forward; // all the way round: spread over the turn
            else
            {
                var backward = Walk(-1);
                backward.Reverse();
                range = backward.Concat(forward).ToList();
            }
            if (range.Count == 0) return result;
            for (var i = 1; i <= count; i++)
            {
                var d = range[Math.Min(range.Count - 1, (int)Math.Round((double)i * range.Count / (count + 1)))];
                result.Add(chain.Links.ToDictionary(id => id, id => (IReadOnlyList<Vec2>)d.ResolvePoints(d.Get(id)!).ToList(), StringComparer.Ordinal));
            }
            return result;
        }

        /// <summary>Writes joint positions back: the storing point of every link, joint symbols and sliders moved.</summary>
        private static SketchDocument Apply(SketchDocument doc, Chain chain, Dictionary<string, Vec2> pos)
        {
            var result = doc;
            var slotOf = chain.Slots.GroupBy(s => s.P).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var kv in chain.Nodes)
            {
                var key = kv.Key;
                var node = kv.Value;
                var p = pos[key];
                var inSlot = slotOf.TryGetValue(key, out var slot);
                if (!inSlot && p.DistanceTo(node.P) < 1e-9) continue;
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
                    var at = o.Attach is not null && i < o.Attach.Count ? o.Attach[i] : null;
                    if (inSlot && at is not null && SlotIndex(at.AnchorId) is not null)
                    {
                        // The pin slides: its point and its position along the slot (the attachment's fraction) move.
                        var (_, t) = Geo.ClosestOnSegment(p, pos[slot.A], pos[slot.B]);
                        result = Edits.Update(result, new[] { link }, x =>
                        {
                            var pts = x.Points!.ToList();
                            pts[i] = new Vec2(Math.Round(p.X, 6), Math.Round(p.Y, 6));
                            var att = x.Attach!.ToList();
                            att[i] = at with { U = Math.Round(t, 9) };
                            return x with { Points = pts, Attach = att };
                        });
                        continue;
                    }
                    if (at is not null) continue;
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
            var slotDocks = new List<(string Link, int Index, string Target, int Edge)>();
            foreach (var link in links)
            {
                var pts = doc.ResolvePoints(link);
                var keys = new List<string>();
                for (var i = 0; i < pts.Count; i++)
                {
                    var key = KeyOf(link, i);
                    var dock = link.Attach is not null && i < link.Attach.Count ? link.Attach[i] : null;
                    if (dock is not null && SlotIndex(dock.AnchorId) is int edge && doc.Get(dock.ObjectId) is { } slotted && slotted.IsPointGeometry && RoleOf(slotted) == LinkageRole.Link)
                        slotDocks.Add((link.Id, i, slotted.Id, edge));
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
            // Pins in slots: the slot runs between the joints of its edge (the last edge of a plate closes it).
            foreach (var (link, i, target, edge) in slotDocks)
            {
                var count = doc.Get(target)!.Points!.Count;
                if (edge >= count || !all.NodeOf.TryGetValue((link, i), out var pin)
                    || !all.NodeOf.TryGetValue((target, edge), out var sa) || !all.NodeOf.TryGetValue((target, (edge + 1) % count), out var sb)) continue;
                if (pin == sa || pin == sb || sa == sb) continue;
                all.Slots.Add((pin, sa, sb));
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
                foreach (var (sp, sa, sb) in all.Slots)
                {
                    if (sp != k && sa != k && sb != k) continue;
                    foreach (var x in new[] { sp, sa, sb })
                        if (keep.Add(x)) queue.Enqueue(x);
                }
            }
            var chain = new Chain();
            foreach (var k in keep) chain.Nodes[k] = all.Nodes[k];
            foreach (var bar in all.Bars.Where(b => keep.Contains(b.A))) chain.Bars.Add(bar);
            foreach (var kv in all.NodeOf.Where(kv => keep.Contains(kv.Value))) chain.NodeOf[kv.Key] = kv.Value;
            chain.Slots.AddRange(all.Slots.Where(s => keep.Contains(s.P)));
            chain.Links.AddRange(chain.NodeOf.Keys.Select(k => k.Link).Distinct());
            return chain;
        }
    }
}
