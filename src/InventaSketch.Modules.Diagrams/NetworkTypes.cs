using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Network diagrams (activity on node) and the critical path method: dependency links (<see cref="LinkType"/>) join an
    /// activity (start) to its successor (end, finish-to-start without lag). Every activity carries an optional duration;
    /// when all activities of a connected network have one, the forward pass gives the earliest start and finish
    /// (ES = max EF of the predecessors, EF = ES + D), the backward pass the latest ones (LF = min LS of the successors, the
    /// project end for the last activities; LS = LF − D), the total float TF = LS − ES and the free float
    /// FF = min ES of the successors − EF. Activities with TF = 0 and the links between them with no free float form the
    /// critical path. Values are computed while drawing and never stored; durations are in the user's unit (days, weeks …).
    /// </summary>
    public static class Cpm
    {
        public const string NodeType = "diag.net.node";
        public const string LinkType = "diag.net.link";

        private const double Eps = 1e-9;

        /// <summary>Times of one activity.</summary>
        public sealed record Times(double Es, double D, double Ef, double Ls, double Lf, double Tf, double Ff)
        {
            public bool Critical => Math.Abs(Tf) < 1e-9;
        }

        /// <summary>(Predecessor, successor) pairs of all links whose first and last docked ends sit on two activities.</summary>
        public static List<(string From, string To)> Edges(SketchDocument doc)
        {
            var list = new List<(string, string)>();
            foreach (var line in doc.Objects.Values)
            {
                if (line.Type != LinkType || line.Attach is null) continue;
                var ends = line.Attach.Where(a => a is not null && doc.Get(a.ObjectId) is { } t && t.Type == NodeType).Select(a => a!.ObjectId).ToList();
                if (ends.Count < 2 || ends[0] == ends[ends.Count - 1]) continue;
                var e = (ends[0], ends[ends.Count - 1]);
                if (!list.Contains(e)) list.Add(e);
            }
            return list;
        }

        /// <summary>Reads a duration: "5", "2.5", "2,5", "5 d", "3 Wochen" (the leading number); null if empty, unreadable or negative.</summary>
        public static double? ParseDuration(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var s = text!.Trim();
            var end = 0;
            while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == ',' || (end == 0 && s[end] == '+'))) end++;
            if (end == 0) return null;
            var num = s.Substring(0, end).Replace(',', '.');
            if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v) || v < 0) return null;
            return v;
        }

        public static double? Duration(SketchObject o) => ParseDuration(o.Str("duration", ""));

        /// <summary>"5", "2.5" (German: "2,5").</summary>
        public static string Format(double v, Lang lang)
        {
            var s = Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        private static readonly ConditionalWeakTable<SketchDocument, Dictionary<string, Times>> Cache = new();

        /// <summary>The times of every activity of a complete network (all durations known, no cycle); others are missing.</summary>
        public static IReadOnlyDictionary<string, Times> Schedule(SketchDocument doc) => Cache.GetValue(doc, Compute);

        /// <summary>The times of one activity, or null.</summary>
        public static Times? Of(SketchDocument doc, string id) => Schedule(doc).TryGetValue(id, out var t) ? t : null;

        /// <summary>True if the link joins two critical activities without free float.</summary>
        public static bool IsCriticalLink(SketchDocument doc, (string From, string To) e)
        {
            var s = Schedule(doc);
            return s.TryGetValue(e.From, out var a) && s.TryGetValue(e.To, out var b) && a.Critical && b.Critical && Math.Abs(b.Es - a.Ef) < 1e-9;
        }

        /// <summary>Every activity of the network holding <paramref name="id"/> (undirected, itself included).</summary>
        public static List<string> Component(SketchDocument doc, string id) => Component(Edges(doc), id);

        private static List<string> Component(List<(string From, string To)> edges, string id)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { id };
            var order = new List<string> { id };
            var queue = new Queue<string>();
            queue.Enqueue(id);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var (a, b) in edges)
                {
                    var other = a == c ? b : b == c ? a : null;
                    if (other is not null && seen.Add(other)) { order.Add(other); queue.Enqueue(other); }
                }
            }
            return order;
        }

        /// <summary>The activities of a component in topological order, or null for a cycle.</summary>
        private static List<string>? Topological(List<string> nodes, List<(string From, string To)> edges)
        {
            var set = new HashSet<string>(nodes, StringComparer.Ordinal);
            var inner = edges.Where(e => set.Contains(e.From) && set.Contains(e.To)).ToList();
            var indeg = nodes.ToDictionary(n => n, n => inner.Count(e => e.To == n), StringComparer.Ordinal);
            var ready = new Queue<string>(nodes.Where(n => indeg[n] == 0));
            var result = new List<string>();
            while (ready.Count > 0)
            {
                var n = ready.Dequeue();
                result.Add(n);
                foreach (var e in inner.Where(e => e.From == n))
                    if (--indeg[e.To] == 0) ready.Enqueue(e.To);
            }
            return result.Count == nodes.Count ? result : null;
        }

        private static Dictionary<string, Times> Compute(SketchDocument doc)
        {
            var result = new Dictionary<string, Times>(StringComparer.Ordinal);
            var edges = Edges(doc);
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in doc.Objects.Values.Where(o => o.Type == NodeType).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                if (done.Contains(o.Id)) continue;
                var nodes = Component(edges, o.Id);
                foreach (var n in nodes) done.Add(n);
                var d = nodes.ToDictionary(n => n, n => doc.Get(n) is { } x ? Duration(x) : null, StringComparer.Ordinal);
                if (d.Values.Any(v => v is null)) continue;
                var topo = Topological(nodes, edges);
                if (topo is null) continue;
                var preds = nodes.ToDictionary(n => n, n => edges.Where(e => e.To == n).Select(e => e.From).ToList(), StringComparer.Ordinal);
                var succs = nodes.ToDictionary(n => n, n => edges.Where(e => e.From == n).Select(e => e.To).ToList(), StringComparer.Ordinal);
                var es = new Dictionary<string, double>(StringComparer.Ordinal);
                var ef = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var n in topo)
                {
                    es[n] = preds[n].Count == 0 ? 0 : preds[n].Max(p => ef[p]);
                    ef[n] = es[n] + d[n]!.Value;
                }
                var end = ef.Values.Max();
                var ls = new Dictionary<string, double>(StringComparer.Ordinal);
                var lf = new Dictionary<string, double>(StringComparer.Ordinal);
                for (var i = topo.Count - 1; i >= 0; i--)
                {
                    var n = topo[i];
                    lf[n] = succs[n].Count == 0 ? end : succs[n].Min(s => ls[s]);
                    ls[n] = lf[n] - d[n]!.Value;
                }
                foreach (var n in nodes)
                {
                    var tf = ls[n] - es[n];
                    var ff = (succs[n].Count == 0 ? end : succs[n].Min(s => es[s])) - ef[n];
                    result[n] = new Times(es[n], d[n]!.Value, ef[n], ls[n], lf[n], Math.Abs(tf) < Eps ? 0 : tf, Math.Abs(ff) < Eps ? 0 : ff);
                }
            }
            return result;
        }

        /// <summary>
        /// "Arrange network": the activities of the network left to right by their longest chain of predecessors, each
        /// column stacked from the top; the links are docked again on facing sides.
        /// </summary>
        internal static ContextAction ArrangeAction(ObjectType owner)
            => ContextAction.InDocument("diag.net.arrange", new I18nText("Arrange network", "Netzplan anordnen"), IconIds.DistributeH, (doc, x, ctx) =>
            {
                var edges = Edges(doc);
                var nodes = Component(edges, x.Id).Where(id => doc.Get(id) is not null).ToList();
                var topo = Topological(nodes, edges);
                if (topo is null || nodes.Count < 2) return doc;
                var rank = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var n in topo)
                {
                    var ps = edges.Where(e => e.To == n && rank.ContainsKey(e.From)).Select(e => rank[e.From]).ToList();
                    rank[n] = ps.Count == 0 ? 0 : ps.Max() + 1;
                }
                var objs = nodes.Select(id => doc.Get(id)!).ToList();
                double w = objs.Max(o => o.Size.W), h = objs.Max(o => o.Size.H);
                double x0 = objs.Min(o => o.Transform.X), y0 = objs.Min(o => o.Transform.Y);
                var centres = new Dictionary<string, Vec2>(StringComparer.Ordinal);
                foreach (var col in topo.GroupBy(n => rank[n]))
                {
                    var i = 0;
                    foreach (var n in col) centres[n] = new Vec2(x0 + col.Key * (w + 14), y0 + i++ * (h + 10));
                }
                return TreeLayout.Apply(doc, centres, ctx.Registry ?? owner.Owner, LinkType, TreeLayout.Horizontal);
            }) with { Group = "layout" };
    }

    /// <summary>
    /// Activity node of a network diagram (activity on node, drawn independently after the common node box of project
    /// planning): earliest start | duration | earliest finish on top, the number and the name in the middle, latest start |
    /// total float | latest finish at the bottom. A critical activity has a heavy outline.
    /// </summary>
    public sealed class NetNodeType : DiagBoxType
    {
        public NetNodeType() : base(Cpm.NodeType, new I18nText("Activity (network diagram)", "Vorgang (Netzplan)"), DiagCategories.Network,
            "network diagram", "netzplan", "critical path", "kritischer pfad", "cpm", "pert", "activity on node", "vorgangsknoten", "activity", "vorgang",
            "float", "puffer", "schedule", "terminplan", "project", "projekt", "earliest start", "frühester anfang", "latest finish", "spätestes ende") { }

        public override Size2 DefaultSize => new(44, 24);
        protected override I18nText Placeholder => new("Activity", "Vorgang");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("duration", "Duration (e.g. 5)", "Dauer (z. B. 5)", null),
            P.Text("no", "Number", "Nummer", null),
            P.TextSize(),
        };

        public override bool DependsOnNeighbours => true;
        public override bool HasDependents => true;

        /// <summary>A changed duration moves the times of the whole network (activities and links).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var nodes = Cpm.Component(ctx.Doc, o.Id);
            var set = new HashSet<string>(nodes, StringComparer.Ordinal);
            var links = ctx.Doc.Objects.Values.Where(l => l.Type == Cpm.LinkType && l.Attach is not null && l.Attach.Any(a => a is not null && set.Contains(a.ObjectId))).Select(l => l.Id);
            return nodes.Where(n => n != o.Id).Concat(links).ToList();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var times = Cpm.Of(ctx.Doc, o.Id);
            var critical = times?.Critical == true;
            var outline = D.Outline with { StrokeWidth = critical ? 0.8 : D.Stroke };
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, outline) with { Tag = DiagTags.Outline } };
            var row = Math.Min(h * 0.28, 7);
            double y1 = -hh + row, y2 = hh - row;
            list.Add(new LinePrim(new Vec2(-hw, y1), new Vec2(hw, y1), D.Thin) { Tag = DiagTags.Divider });
            list.Add(new LinePrim(new Vec2(-hw, y2), new Vec2(hw, y2), D.Thin) { Tag = DiagTags.Divider });
            foreach (var x in new[] { -w / 6, w / 6 })
            {
                list.Add(new LinePrim(new Vec2(x, -hh), new Vec2(x, y1), D.Thin) { Tag = DiagTags.Divider });
                list.Add(new LinePrim(new Vec2(x, y2), new Vec2(x, hh), D.Thin) { Tag = DiagTags.Divider });
            }
            var size = Math.Max(D.MinTextSize, Math.Min(TextSizeOf(o) * 0.85, row * 0.62));
            void Cell(double cx, double cy, double? v)
            {
                if (v is double value) list.Add(D.Text(new Vec2(cx, cy), Cpm.Format(value, ctx.Lang), size, DiagTags.Value));
            }
            double top = -hh + row / 2, bottom = hh - row / 2, left = -w / 3, right = w / 3;
            Cell(left, top, times?.Es);
            Cell(0, top, times?.D ?? Cpm.Duration(o));
            Cell(right, top, times?.Ef);
            Cell(left, bottom, times?.Ls);
            Cell(0, bottom, times?.Tf);
            Cell(right, bottom, times?.Lf);
            var no = o.Str("no", "").Trim();
            var nameLeft = -hw;
            if (no.Length > 0)
            {
                var nw = Math.Min(w * 0.22, Math.Max(6, D.Width(no, size) + 2));
                nameLeft = -hw + nw;
                list.Add(new LinePrim(new Vec2(nameLeft, y1), new Vec2(nameLeft, y2), D.Thin) { Tag = DiagTags.Divider });
                list.Add(D.Text(new Vec2(-hw + nw / 2, 0), no, size, DiagTags.Numeral));
            }
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2((nameLeft + hw) / 2, 0), hw - nameLeft - 2, y2 - y1 - 1);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return ContextAction.WithInput("diag.net.duration", new I18nText("Duration", "Dauer"), IconIds.Value,
                new I18nText("Duration (e.g. 5 or 2.5; one unit for the whole network)", "Dauer (z. B. 5 oder 2,5; eine Einheit für den ganzen Netzplan)"), o.Str("duration", ""), (x, s) =>
                {
                    if (string.IsNullOrWhiteSpace(s)) return x with { Params = x.Params.Where(kv => kv.Key != "duration").ToDictionary(kv => kv.Key, kv => kv.Value) };
                    if (Cpm.ParseDuration(s) is null) throw new FormatException("Not a duration: " + s);
                    return x.WithParam("duration", s.Trim());
                }) with { Group = "value" };
            yield return ContextAction.WithInput("diag.net.number", new I18nText("Number", "Nummer"), IconIds.Name,
                new I18nText("Activity number", "Vorgangsnummer"), o.Str("no", ""), (x, s) => x.WithParam("no", (s ?? "").Trim())) with { Group = "label" };
            yield return Cpm.ArrangeAction(this);
        }

        /// <summary>"Arrange network" is offered greyed out while the activity has no link.</summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var linked = Cpm.Edges(ctx.Doc).Any(e => e.From == o.Id || e.To == o.Id);
            return base.Actions(o, ctx).Select(a => a.Id == "diag.net.arrange" ? a with { IsEnabled = linked } : a);
        }
    }

    /// <summary>
    /// Dependency of a network diagram (finish-to-start): from an activity to its successor, with an arrowhead. A link on
    /// the critical path is drawn heavy.
    /// </summary>
    public sealed class NetLinkType : DiagConnectorType
    {
        public NetLinkType() : base(Cpm.LinkType, new I18nText("Dependency (network diagram)", "Anordnungsbeziehung (Netzplan)"), DiagCategories.Network,
            "network diagram", "netzplan", "dependency", "abhängigkeit", "anordnungsbeziehung", "predecessor", "vorgänger", "successor", "nachfolger",
            "critical path", "kritischer pfad", "link", "pfeil") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Route(), P.TextSize() };

        protected override DiagEnd StartEnd(SketchObject o) => DiagEnd.None;
        protected override DiagEnd EndEnd(SketchObject o) => DiagEnd.Filled;

        public override bool DependsOnNeighbours => true;
        public override bool HasDependents => true;

        /// <summary>A new or removed dependency changes the times of the network on both sides.</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var ids = new List<string>();
            foreach (var a in o.Attach ?? Array.Empty<Attachment?>())
            {
                if (a is null || ctx.Doc.Get(a.ObjectId) is not { Type: Cpm.NodeType } || ids.Contains(a.ObjectId)) continue;
                ids.AddRange(Cpm.Component(ctx.Doc, a.ObjectId).Where(x => !ids.Contains(x)));
                if (!ids.Contains(a.ObjectId)) ids.Add(a.ObjectId);
            }
            return ids;
        }

        /// <summary>The (predecessor, successor) the link joins, or null.</summary>
        private static (string From, string To)? Ends(SketchObject o, SketchDocument doc)
        {
            var ends = (o.Attach ?? Array.Empty<Attachment?>()).Where(a => a is not null && doc.Get(a.ObjectId) is { Type: Cpm.NodeType }).Select(a => a!.ObjectId).ToList();
            return ends.Count < 2 || ends[0] == ends[ends.Count - 1] ? null : (ends[0], ends[ends.Count - 1]);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var list = base.RenderPoints(o, ctx, p);
            if (Ends(o, ctx.Doc) is not { } e || !Cpm.IsCriticalLink(ctx.Doc, e)) return list;
            return list.Select(x => x.Tag == DiagTags.Main ? Prim.Restyle(x, s => s with { StrokeWidth = 0.6, ArrowSize = s.ArrowStart is null && s.ArrowEnd is null ? s.ArrowSize : ArrowSize.Small }) : x).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
            foreach (var a in RouteActions(o)) yield return a;
        }
    }
}
