using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>Fault tree gates.</summary>
    public enum FtaGateKind
    {
        And, Or,
        /// <summary>k-out-of-n (voting) gate: an OR shape with "k/n".</summary>
        Vote,
        /// <summary>Inhibit gate: hexagon; its condition is one more input (a conditioning event).</summary>
        Inhibit,
    }

    /// <summary>Fault tree events.</summary>
    public enum FtaEventKind { Basic, Top, Intermediate, Undeveloped, House, Conditioning }

    /// <summary>
    /// Fault tree structure and values: the links (<see cref="LinkType"/>) join a higher-level event or gate (start) to its
    /// input (end); basic, undeveloped, house and conditioning events carry an optional probability <c>p</c>, from which
    /// every gate and event above them gets its probability (AND and inhibit: product; OR: 1 − Π(1 − p); k-of-n: at least
    /// k of the inputs, independent events). Values are computed while drawing and never stored.
    /// </summary>
    public static class Fta
    {
        public const string LinkType = "diag.fta.link";
        public const string Prefix = "diag.fta.";

        /// <summary>True for fault tree elements other than the link.</summary>
        public static bool IsNode(string type) => type.StartsWith(Prefix, StringComparison.Ordinal) && type != LinkType;

        /// <summary>Events that never have inputs (they carry an entered probability).</summary>
        public static bool IsLeaf(string type)
            => type is "diag.fta.event.basic" or "diag.fta.event.undeveloped" or "diag.fta.event.house" or "diag.fta.event.conditioning";

        /// <summary>
        /// (Parent, Child) pairs of all fault tree links of the document whose first and last docked ends sit on two fault
        /// tree elements: from the start to the end, except a link drawn from a leaf event up to a gate or event, which is read
        /// the other way round.
        /// </summary>
        public static List<(string Parent, string Child)> Edges(SketchDocument doc)
        {
            var list = new List<(string, string)>();
            foreach (var line in doc.Objects.Values)
            {
                if (line.Type != LinkType || line.Attach is null) continue;
                var ends = line.Attach.Where(a => a is not null && doc.Get(a.ObjectId) is { } t && IsNode(t.Type)).Select(a => a!.ObjectId).ToList();
                if (ends.Count < 2 || ends[0] == ends[ends.Count - 1]) continue;
                string a = ends[0], b = ends[ends.Count - 1];
                if (IsLeaf(doc.Get(a)!.Type) && !IsLeaf(doc.Get(b)!.Type)) (a, b) = (b, a);
                if (!list.Contains((a, b))) list.Add((a, b));
            }
            return list;
        }

        /// <summary>Inputs of a gate or event (distinct, in link order).</summary>
        public static IReadOnlyList<string> Children(SketchDocument doc, string id) => Edges(doc).Where(e => e.Parent == id).Select(e => e.Child).Distinct().ToList();

        /// <summary>Every gate and event above <paramref name="id"/> (breadth first, without itself).</summary>
        public static IEnumerable<string> Ancestors(SketchDocument doc, string id)
        {
            var edges = Edges(doc);
            var seen = new HashSet<string>(StringComparer.Ordinal) { id };
            var queue = new Queue<string>();
            queue.Enqueue(id);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var (p, _) in edges.Where(e => e.Child == c))
                    if (seen.Add(p)) { queue.Enqueue(p); yield return p; }
            }
        }

        /// <summary>The top of the tree that holds <paramref name="id"/>: the first ancestor without a parent (itself if it has none).</summary>
        public static string Root(SketchDocument doc, string id)
        {
            var edges = Edges(doc);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var cur = id;
            while (seen.Add(cur))
            {
                var parent = edges.FirstOrDefault(e => e.Child == cur).Parent;
                if (parent is null) return cur;
                cur = parent;
            }
            return id;
        }

        /// <summary>
        /// Reads a probability: "1e-4", "0.001", "0,001", "2.5 %" (percent). Null if empty, unreadable or outside [0, 1].
        /// </summary>
        public static double? ParseProbability(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var s = text!.Trim().Replace(" ", "");
            var percent = s.EndsWith("%", StringComparison.Ordinal);
            if (percent) s = s.Substring(0, s.Length - 1);
            if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v)) return null;
            if (percent) v /= 100;
            return v < 0 || v > 1 ? null : v;
        }

        /// <summary>"p = 0.012", "p = 1.2e-5" (German: decimal comma).</summary>
        public static string Format(double p, Lang lang)
        {
            string s;
            if (p == 0) s = "0";
            else if (p >= 0.01) s = p.ToString("0.###", CultureInfo.InvariantCulture);
            else s = p.ToString("0.##e+0", CultureInfo.InvariantCulture);
            if (lang == Lang.De) s = s.Replace('.', ',');
            return "p = " + s;
        }

        /// <summary>Probability of at least <paramref name="k"/> of the independent events <paramref name="ps"/>.</summary>
        public static double AtLeast(int k, IReadOnlyList<double> ps)
        {
            if (k <= 0) return 1;
            if (k > ps.Count) return 0;
            // dp[j]: probability that exactly j of the events seen so far occur.
            var dp = new double[ps.Count + 1];
            dp[0] = 1;
            for (var i = 0; i < ps.Count; i++)
                for (var j = i + 1; j >= 0; j--)
                    dp[j] = dp[j] * (1 - ps[i]) + (j > 0 ? dp[j - 1] * ps[i] : 0);
            var sum = 0.0;
            for (var j = k; j <= ps.Count; j++) sum += dp[j];
            return Math.Min(1, Math.Max(0, sum));
        }

        /// <summary>The probability of an element (entered for leaves, computed above them), or null if any input is unknown.</summary>
        public static double? Probability(SketchDocument doc, string id) => new Evaluator(doc).Eval(id);

        private sealed class Evaluator
        {
            private readonly SketchDocument _doc;
            private readonly Dictionary<string, List<string>> _children = new(StringComparer.Ordinal);
            private readonly Dictionary<string, double?> _memo = new(StringComparer.Ordinal);
            private readonly HashSet<string> _active = new(StringComparer.Ordinal);

            public Evaluator(SketchDocument doc)
            {
                _doc = doc;
                foreach (var (p, c) in Edges(doc))
                {
                    if (!_children.TryGetValue(p, out var l)) _children[p] = l = new List<string>();
                    if (!l.Contains(c)) l.Add(c);
                }
            }

            public double? Eval(string id)
            {
                if (_memo.TryGetValue(id, out var m)) return m;
                if (!_active.Add(id)) return null; // a cycle
                var v = Compute(id);
                _active.Remove(id);
                return _memo[id] = v;
            }

            private double? Compute(string id)
            {
                var o = _doc.Get(id);
                if (o is null) return null;
                var own = ParseProbability(o.Params.TryGetValue("p", out var pv) ? pv.Text : null);
                if (IsLeaf(o.Type)) return own;
                var kids = _children.TryGetValue(id, out var l) ? l : new List<string>();
                if (o.Type == "diag.fta.transfer" && own is not null) return own;
                if (kids.Count == 0) return null;
                var values = new List<double>();
                foreach (var k in kids)
                {
                    if (Eval(k) is not double x) return null;
                    values.Add(x);
                }
                switch (o.Type)
                {
                    case "diag.fta.gate.and":
                    case "diag.fta.gate.inhibit":
                        return values.Aggregate(1.0, (a, b) => a * b);
                    case "diag.fta.gate.vote":
                        return AtLeast(Math.Max(1, o.Int("k", 2)), values);
                    default:
                        // OR gate; an event or transfer with several inputs reads them as alternatives.
                        return values.Count == 1 ? values[0] : 1 - values.Aggregate(1.0, (a, b) => a * (1 - b));
                }
            }
        }

        // ------------------------------------------------------------------ shared drawing and actions

        internal static double ValueSize(SketchObject o) => Math.Min(o.Num("textSize", D.TextSize) * 0.85, 3.0);

        /// <summary>Value label centred below a symbol (outside its box, like BPMN labels).</summary>
        internal static IEnumerable<Primitive> BelowValue(SketchObject o, RenderContext ctx, double hh)
        {
            if (Probability(ctx.Doc, o.Id) is not double p) yield break;
            var s = ValueSize(o);
            yield return D.Text(new Vec2(0, hh + 1 + s * D.LineFactor / 2), Format(p, ctx.Lang), s, DiagTags.Value);
        }

        /// <summary>"Arrange tree": the whole fault tree holding the element as a tidy tree from its top event down.</summary>
        internal static ContextAction ArrangeAction(ObjectType owner)
            => ContextAction.InDocument("diag.fta.arrange", new I18nText("Arrange tree", "Baum anordnen"), IconIds.DistributeH, (doc, x, ctx) =>
            {
                var root = Root(doc, x.Id);
                var edges = Edges(doc);
                var tree = TreeLayout.Build(doc, root, id => edges.Where(e => e.Parent == id).Select(e => e.Child), d => d.X);
                return tree is null || tree.Children.Count == 0 ? doc : TreeLayout.Apply(doc, TreeLayout.Down(tree, 6, 8), ctx.Registry ?? owner.Owner, LinkType, TreeLayout.Vertical);
            }) with { Group = "layout" };

        /// <summary>"Probability …": asks for p (empty clears it).</summary>
        internal static ContextAction ProbabilityAction(SketchObject o)
            => ContextAction.WithInput("diag.fta.p", new I18nText("Probability", "Wahrscheinlichkeit"), IconIds.Value,
                new I18nText("Probability p (e.g. 1e-4 or 0.5 %)", "Wahrscheinlichkeit p (z. B. 1e-4 oder 0,5 %)"), o.Str("p", ""), (x, s) =>
                {
                    if (string.IsNullOrWhiteSpace(s)) return x with { Params = x.Params.Where(kv => kv.Key != "p").ToDictionary(kv => kv.Key, kv => kv.Value) };
                    if (ParseProbability(s) is null) throw new FormatException("Not a probability between 0 and 1: " + s);
                    return x.WithParam("p", s.Trim());
                }) with { Group = "value" };

        internal static ParamDef ProbabilityParam() => P.Text("p", "Probability p (e.g. 1e-4)", "Wahrscheinlichkeit p (z. B. 1e-4)", null);
    }

    /// <summary>Common base of the fault tree elements: quick add with fault tree links, dependents for value updates.</summary>
    public abstract class FtaNodeType : DiagBoxType
    {
        protected FtaNodeType(string id, I18nText name, params string[] keywords)
            : base(id, name, DiagCategories.Fta, new[] { "fta", "fault tree", "fehlerbaum", "fehlerbaumanalyse", "reliability", "zuverlässigkeit", "safety", "sicherheit" }.Concat(keywords).ToArray()) { }

        /// <summary>Gates and events above leaves draw values computed from their inputs.</summary>
        public override bool DependsOnNeighbours => !Fta.IsLeaf(Id);
        public override bool HasDependents => true;
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Fta.Ancestors(ctx.Doc, o.Id);

        /// <summary>"Arrange tree" is offered greyed out while the element is not linked into a tree.</summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var linked = Fta.Edges(ctx.Doc).Any(e => e.Parent == o.Id || e.Child == o.Id);
            return base.Actions(o, ctx).Select(a => a.Id == "diag.fta.arrange" ? a with { IsEnabled = linked } : a);
        }
    }

    /// <summary>
    /// Fault tree gate (drawn independently after the common fault tree conventions, e.g. IEC 61025): AND (flat base, round
    /// top), OR (curved base, pointed top), k-of-n (OR shape with "k/n"), inhibit (hexagon). The output stem leaves at the
    /// top, the inputs dock at the bottom; the object text (e.g. "G1") stands to the right. A gate without an event above
    /// it shows its computed probability.
    /// </summary>
    public sealed class FtaGateType : FtaNodeType
    {
        private readonly FtaGateKind _kind;

        public FtaGateType(FtaGateKind kind) : base("diag.fta.gate." + kind.ToString().ToLowerInvariant(), kind switch
            {
                FtaGateKind.And => new I18nText("AND gate (fault tree)", "UND-Gatter (Fehlerbaum)"),
                FtaGateKind.Or => new I18nText("OR gate (fault tree)", "ODER-Gatter (Fehlerbaum)"),
                FtaGateKind.Vote => new I18nText("k-out-of-n gate (fault tree)", "k-aus-n-Gatter (Fehlerbaum)"),
                _ => new I18nText("Inhibit gate (fault tree)", "Inhibit-Gatter (Fehlerbaum)"),
            },
            new[] { "gate", "gatter", "verknüpfung" }.Concat(kind switch
            {
                FtaGateKind.And => new[] { "and", "und", "conjunction" },
                FtaGateKind.Or => new[] { "or", "oder", "disjunction" },
                FtaGateKind.Vote => new[] { "voting", "vote", "k of n", "k aus n", "majority", "mehrheit", "2oo3" },
                _ => new[] { "inhibit", "hemmung", "condition", "bedingung" },
            }).ToArray())
        {
            _kind = kind;
        }

        public FtaGateKind Kind => _kind;
        public override Size2 DefaultSize => new(12, 14);

        public override IReadOnlyList<ParamDef> Parameters => _kind == FtaGateKind.Vote
            ? new[] { P.Int("k", "Required inputs k", "Benötigte Eingänge k", 2, 1, 20), P.Int("n", "Inputs n (label)", "Eingänge n (Beschriftung)", 3, 1, 20), P.TextSize() }
            : new[] { P.TextSize() };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("bottom", new Vec2(0, hh));
            yield return ("bottom.q1", new Vec2(-hw / 2, hh));
            yield return ("bottom.q3", new Vec2(hw / 2, hh));
            yield return ("left", new Vec2(-hw, 0));
            yield return ("right", new Vec2(hw, 0));
        }

        /// <summary>Gate body outline and the y where the input stem meets it (local coordinates).</summary>
        internal static (PathPrim Body, double StemTop, double Top, double Bottom) Shape(FtaGateKind kind, double w, double h, Style style)
        {
            double hw = w / 2, hh = h / 2;
            var s = Math.Min(2.5, h * 0.18);
            double yt = -hh + s, yb = hh - s, bh = yb - yt, bw = Math.Max(0.1, hw - 0.2);
            const double k = 0.5522847498307936;
            switch (kind)
            {
                case FtaGateKind.And:
                {
                    var ym = yb - bh * 0.45;
                    var rv = ym - yt;
                    return (Prim.Path(new[]
                    {
                        PathSeg.Move(new Vec2(-bw, yb)), PathSeg.Line(new Vec2(-bw, ym)),
                        PathSeg.Cubic(new Vec2(-bw, ym - k * rv), new Vec2(-k * bw, yt), new Vec2(0, yt)),
                        PathSeg.Cubic(new Vec2(k * bw, yt), new Vec2(bw, ym - k * rv), new Vec2(bw, ym)),
                        PathSeg.Line(new Vec2(bw, yb)), PathSeg.Close,
                    }, style), yb, yt, yb);
                }
                case FtaGateKind.Inhibit:
                {
                    var q = bh * 0.25;
                    return (Prim.Polygon(new[] { new Vec2(0, yt), new Vec2(bw, yt + q), new Vec2(bw, yb - q), new Vec2(0, yb), new Vec2(-bw, yb - q), new Vec2(-bw, yt + q) }, style), yb, yt, yb);
                }
                default:
                {
                    var c = bh * 0.25;
                    return (Prim.Path(new[]
                    {
                        PathSeg.Move(new Vec2(-bw, yb)),
                        PathSeg.Cubic(new Vec2(-bw, yb - 0.5 * bh), new Vec2(-0.45 * bw, yt + 0.2 * bh), new Vec2(0, yt)),
                        PathSeg.Cubic(new Vec2(0.45 * bw, yt + 0.2 * bh), new Vec2(bw, yb - 0.5 * bh), new Vec2(bw, yb)),
                        PathSeg.Cubic(new Vec2(0.5 * bw, yb - c), new Vec2(-0.5 * bw, yb - c), new Vec2(-bw, yb)), PathSeg.Close,
                    }, style), yb - 0.75 * c, yt, yb);
                }
            }
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var (body, stemTop, top, bottom) = Shape(_kind, w, h, D.Outline);
            var list = new List<Primitive>
            {
                new LinePrim(new Vec2(0, -hh), new Vec2(0, top), D.OutlineOpen) { Tag = DiagTags.Outline },
                new LinePrim(new Vec2(0, stemTop), new Vec2(0, hh), D.OutlineOpen) { Tag = DiagTags.Outline },
                body with { Tag = DiagTags.Outline },
            };
            if (_kind == FtaGateKind.Vote)
            {
                var label = D.Inv(o.Int("k", 2)) + "/" + D.Inv(o.Int("n", 3));
                var bh = bottom - top;
                var size = D.Fit(TextSizeOf(o), new[] { label }, hw * 1.1, bh * 0.4, 1);
                list.Add(D.Text(new Vec2(0, top + bh * 0.62), label, size, DiagTags.Marker));
            }
            // Name and (for a gate not under an event) the computed value to the right of the output stem.
            var rows = D.Lines(o.Text).ToList();
            var ts = TextSizeOf(o);
            var y = top + ts * D.LineFactor / 2;
            foreach (var r in rows)
            {
                list.Add(D.Text(new Vec2(hw + 0.8, y), r, ts, DiagTags.Label, TextAnchor.Start));
                y += ts * D.LineFactor;
            }
            if (!UnderEvent(ctx.Doc, o.Id) && Fta.Probability(ctx.Doc, o.Id) is double p)
            {
                var vs = Fta.ValueSize(o);
                var vy = rows.Count == 0 ? top + vs * D.LineFactor / 2 : y - ts * D.LineFactor / 2 + vs * D.LineFactor / 2;
                list.Add(D.Text(new Vec2(hw + 0.8, vy), Fta.Format(p, ctx.Lang), vs, DiagTags.Value, TextAnchor.Start));
            }
            return list;
        }

        /// <summary>True if an event (which shows the value itself) sits directly above the gate.</summary>
        private static bool UnderEvent(SketchDocument doc, string id)
            => Fta.Edges(doc).Any(e => e.Child == id && doc.Get(e.Parent) is { } p && p.Type.StartsWith("diag.fta.event.", StringComparison.Ordinal));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            if (_kind == FtaGateKind.Vote)
                yield return ContextAction.WithInput("diag.fta.kofn", new I18nText("k out of n", "k aus n"), IconIds.Value,
                    new I18nText("Required inputs / inputs (e.g. 2/3)", "Benötigte / alle Eingänge (z. B. 2/3)"), D.Inv(o.Int("k", 2)) + "/" + D.Inv(o.Int("n", 3)), (x, s) =>
                    {
                        var parts = s.Split('/');
                        if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) || k < 1) throw new FormatException("k/n expected: " + s);
                        var n = parts.Length > 1 && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nn) ? nn : Math.Max(k, x.Int("n", 3));
                        return x.WithParam("k", Math.Min(20, k)).WithParam("n", Math.Min(20, Math.Max(k, n)));
                    }) with { Group = "value" };
            yield return Fta.ArrangeAction(this);
        }
    }

    /// <summary>
    /// Fault tree event: basic event (circle), top and intermediate event (rectangle; the top event with a heavier outline
    /// and bold text), undeveloped event (diamond), house event (house outline) and conditioning event (ellipse, the
    /// condition of an inhibit gate). The description is the object text. Leaves carry an optional probability <c>p</c>
    /// (shown below the symbol); event rectangles show the value computed from their input inside, under the text.
    /// </summary>
    public sealed class FtaEventType : FtaNodeType
    {
        private readonly FtaEventKind _kind;

        public FtaEventType(FtaEventKind kind) : base("diag.fta.event." + kind.ToString().ToLowerInvariant(), kind switch
            {
                FtaEventKind.Basic => new I18nText("Basic event (fault tree)", "Basisereignis (Fehlerbaum)"),
                FtaEventKind.Top => new I18nText("Top event (fault tree)", "Top-Ereignis (Fehlerbaum)"),
                FtaEventKind.Intermediate => new I18nText("Intermediate event (fault tree)", "Zwischenereignis (Fehlerbaum)"),
                FtaEventKind.Undeveloped => new I18nText("Undeveloped event (fault tree)", "Nicht entwickeltes Ereignis (Fehlerbaum)"),
                FtaEventKind.House => new I18nText("House event (fault tree)", "Hausereignis (Fehlerbaum)"),
                _ => new I18nText("Conditioning event (fault tree)", "Bedingungsereignis (Fehlerbaum)"),
            },
            new[] { "event", "ereignis" }.Concat(kind switch
            {
                FtaEventKind.Basic => new[] { "basic", "basis", "primary", "primär", "cause", "ursache", "circle", "kreis" },
                FtaEventKind.Top => new[] { "top", "top event", "hazard", "gefährdung", "unerwünschtes ereignis", "rectangle", "rechteck" },
                FtaEventKind.Intermediate => new[] { "intermediate", "zwischen", "rectangle", "rechteck" },
                FtaEventKind.Undeveloped => new[] { "undeveloped", "nicht entwickelt", "diamond", "raute" },
                FtaEventKind.House => new[] { "house", "haus", "external", "extern", "normal" },
                _ => new[] { "conditioning", "condition", "bedingung", "inhibit", "ellipse", "oval" },
            }).ToArray())
        {
            _kind = kind;
        }

        public FtaEventKind Kind => _kind;
        private bool Rectangle => _kind is FtaEventKind.Top or FtaEventKind.Intermediate;

        public override Size2 DefaultSize => _kind switch
        {
            FtaEventKind.Basic => new(14, 14),
            FtaEventKind.Top => new(36, 14),
            FtaEventKind.Intermediate => new(32, 12),
            FtaEventKind.Undeveloped => new(18, 12),
            FtaEventKind.House => new(14, 14),
            _ => new(22, 11),
        };

        public override bool PreferUniformScale => _kind is FtaEventKind.Basic or FtaEventKind.House;

        protected override I18nText Placeholder => _kind switch
        {
            FtaEventKind.Basic => new("Cause", "Ursache"),
            FtaEventKind.Top => new("Top event", "Top-Ereignis"),
            FtaEventKind.Intermediate => new("Event", "Ereignis"),
            FtaEventKind.Undeveloped => new("Event", "Ereignis"),
            FtaEventKind.House => new("Event", "Ereignis"),
            _ => new("Condition", "Bedingung"),
        };

        public override IReadOnlyList<ParamDef> Parameters => Rectangle ? new[] { P.TextSize() } : new[] { Fta.ProbabilityParam(), P.TextSize() };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            switch (_kind)
            {
                case FtaEventKind.Basic:
                case FtaEventKind.Conditioning:
                    return D.EllipsePorts(o.Size);
                case FtaEventKind.Undeveloped:
                    return D.DiamondPorts(o.Size);
                case FtaEventKind.House:
                    return new[] { ("top", new Vec2(0, -hh)), ("bottom", new Vec2(0, hh)), ("left", new Vec2(-hw, hh * 0.3)), ("right", new Vec2(hw, hh * 0.3)) };
                default:
                    return D.RectPorts(o.Size);
            }
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive>();
            var ts = TextSizeOf(o);
            var text = Label(o, ctx);
            switch (_kind)
            {
                case FtaEventKind.Basic:
                case FtaEventKind.Conditioning:
                    list.Add(Prim.Ellipse(0, 0, hw, hh, D.Outline) with { Tag = DiagTags.Outline });
                    D.CenterText(list, text, ts, Vec2.Zero, w * 0.72, h * 0.62);
                    break;
                case FtaEventKind.Undeveloped:
                    list.Add(D.Diamond(hw, hh, D.Outline) with { Tag = DiagTags.Outline });
                    D.CenterText(list, text, ts, Vec2.Zero, w * 0.55, h * 0.5);
                    break;
                case FtaEventKind.House:
                {
                    var roof = h * 0.35;
                    list.Add(Prim.Polygon(new[] { new Vec2(0, -hh), new Vec2(hw, -hh + roof), new Vec2(hw, hh), new Vec2(-hw, hh), new Vec2(-hw, -hh + roof) }, D.Outline) with { Tag = DiagTags.Outline });
                    D.CenterText(list, text, ts, new Vec2(0, roof / 2), w - 2, h - roof - 1.5);
                    break;
                }
                default:
                {
                    var top = _kind == FtaEventKind.Top;
                    list.Add(Prim.Rect(-hw, -hh, w, h, top ? D.Outline with { StrokeWidth = 0.6 } : D.Outline) with { Tag = DiagTags.Outline });
                    var value = Fta.Probability(ctx.Doc, o.Id);
                    var vs = Fta.ValueSize(o);
                    var band = value is null ? 0 : vs * D.LineFactor;
                    D.CenterText(list, text, ts, new Vec2(0, -band / 2), w - 3, h - 2 - band, bold: top);
                    if (value is double p)
                        list.Add(D.Text(new Vec2(0, hh - 0.8 - vs * D.LineFactor / 2), Fta.Format(p, ctx.Lang), Math.Min(vs, D.Fit(vs, new[] { Fta.Format(p, ctx.Lang) }, w - 2, h, 1)), DiagTags.Value));
                    return list;
                }
            }
            list.AddRange(Fta.BelowValue(o, ctx, hh));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            if (!Rectangle) yield return Fta.ProbabilityAction(o);
            yield return Fta.ArrangeAction(this);
        }
    }

    /// <summary>
    /// Transfer symbol (triangle): the tree continues on another page or elsewhere. Its optional <c>p</c> stands for the
    /// transferred subtree; without one, inputs drawn below it give its value. The object text is the transfer label.
    /// </summary>
    public sealed class FtaTransferType : FtaNodeType
    {
        public FtaTransferType() : base("diag.fta.transfer", new I18nText("Transfer (fault tree)", "Übertrag (Fehlerbaum)"),
            "transfer", "übertrag", "übertragung", "triangle", "dreieck", "continuation", "fortsetzung") { }

        public override Size2 DefaultSize => new(12, 10);
        protected override I18nText Placeholder => new("A", "A");
        public override IReadOnlyList<ParamDef> Parameters => new[] { Fta.ProbabilityParam(), P.TextSize() };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("bottom", new Vec2(0, hh));
            yield return ("left", new Vec2(-hw / 2, 0));
            yield return ("right", new Vec2(hw / 2, 0));
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Polygon(new[] { new Vec2(0, -hh), new Vec2(hw, hh), new Vec2(-hw, hh) }, D.Outline) with { Tag = DiagTags.Outline } };
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2(0, hh * 0.4), w * 0.45, h * 0.4);
            list.AddRange(Fta.BelowValue(o, ctx, hh));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Fta.ProbabilityAction(o);
            yield return Fta.ArrangeAction(this);
        }
    }

    /// <summary>
    /// Plain diagram link without end notations, orthogonal by default (fault trees, sequential function charts, mind
    /// maps build on it). Draw it from the higher-level element to its input.
    /// </summary>
    public abstract class PlainLinkType : DiagConnectorType
    {
        protected PlainLinkType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        protected virtual string DefaultRoute => "orthogonal";
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, -10), new Vec2(0, 10) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("route", "Routing", "Führung", DefaultRoute, ("straight", "Straight / waypoints", "Gerade / Stützpunkte"), ("orthogonal", "Orthogonal", "Rechtwinklig")),
            P.TextSize(),
        };

        protected override DiagEnd StartEnd(SketchObject o) => DiagEnd.None;
        protected override DiagEnd EndEnd(SketchObject o) => DiagEnd.None;

        /// <summary>The plain link line through <paramref name="path"/> (user width, dash and colour overrides apply).</summary>
        protected static Primitive MainLine(IReadOnlyList<Vec2> path)
        {
            var style = Style.ThinLine with { StrokeWidth = D.LineWidth };
            return path.Count == 2 ? new LinePrim(path[0], path[1], style) { Tag = DiagTags.Main } : Prim.Polyline(path, style) with { Tag = DiagTags.Main };
        }

        public override List<Vec2> Path(SketchObject o, IReadOnlyList<Vec2> p)
        {
            if (p.Count == 2 && o.Str("route", DefaultRoute) == "orthogonal")
                return D.Orthogonal(p[0], p[1], D.ExitHorizontal(o, 0), D.ExitHorizontal(o, 1));
            return base.Path(o, p);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
            foreach (var a in RouteActions(o, DefaultRoute)) yield return a;
        }
    }

    /// <summary>
    /// Fault tree link: from a gate or event (start) down to one of its inputs (end). Changing a link redraws the values of
    /// the elements above it.
    /// </summary>
    public sealed class FtaLinkType : PlainLinkType
    {
        public FtaLinkType() : base(Fta.LinkType, new I18nText("Link (fault tree)", "Verbindung (Fehlerbaum)"), DiagCategories.Fta,
            "fta", "fault tree", "fehlerbaum", "link", "verbindung", "line", "linie", "input", "eingang") { }

        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var parent = o.Attach?.FirstOrDefault(a => a is not null && ctx.Doc.Get(a.ObjectId) is { } t && Fta.IsNode(t.Type))?.ObjectId;
            var child = o.Attach?.LastOrDefault(a => a is not null && ctx.Doc.Get(a.ObjectId) is { } t && Fta.IsNode(t.Type))?.ObjectId;
            var ids = new List<string>();
            foreach (var end in new[] { parent, child })
            {
                if (end is null || ids.Contains(end)) continue;
                ids.Add(end);
                ids.AddRange(Fta.Ancestors(ctx.Doc, end).Where(x => !ids.Contains(x)));
            }
            return ids;
        }
    }
}
