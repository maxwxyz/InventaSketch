using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Venn diagram of two or three sets: overlapping circles (they stay circles under free scaling), the set names inside
    /// their exclusive parts, optional translucent tints and the universal set as a frame filling the box.
    /// </summary>
    internal sealed class VennType : MathBoxType
    {
        public VennType() : base("math.venn.diagram", new I18nText("Venn diagram", "Venn-Diagramm (Mengendiagramm)"), MathCategories.Stochastics,
            "venn", "set", "sets", "intersection", "union", "menge", "mengen", "mengendiagramm", "schnittmenge", "vereinigungsmenge", "grundmenge", "euler") { }

        public override Size2 DefaultSize => new(60, 44);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Int("sets", "Sets", "Mengen", 2, 2, 3),
            Pm.Text("labelA", "Name of set 1", "Name der Menge 1", "A"),
            Pm.Text("labelB", "Name of set 2", "Name der Menge 2", "B"),
            Pm.Text("labelC", "Name of set 3", "Name der Menge 3", "C"),
            Pm.Bool("universe", "Universal set (frame)", "Grundmenge (Rahmen)", true),
            Pm.Text("universeLabel", "Name of the universal set (empty: U / G)", "Name der Grundmenge (leer: U / G)", ""),
            Pm.Bool("fill", "Tinted sets", "Mengen getönt", true),
        };

        private static readonly string[] Colors = { Mx.Blue, Mx.Red, Mx.Green };

        /// <summary>Circle centres for a radius of 1 (centre distance 1) and the directions of the exclusive parts.</summary>
        internal static (Vec2 Centre, Vec2 Out)[] Layout(int n)
        {
            if (n <= 2) return new[] { (new Vec2(-0.5, 0), new Vec2(-1, 0)), (new Vec2(0.5, 0), new Vec2(1, 0)) };
            var rho = 1 / Math.Sqrt(3);
            // Set 1 top left, set 2 top right, set 3 at the bottom.
            return new[] { 150.0, 30.0, 270.0 }.Select(Mx.Dir).Select(d => (d * rho, d)).ToArray();
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var n = Math.Max(2, Math.Min(3, o.Int("sets", 2)));
            var k = K(o);
            var tl = Mx.LabelSize * k;
            var universe = o.Flag("universe", true);
            var frame = new Rect(-o.Size.W / 2 + 0.4, -o.Size.H / 2 + 0.4, Math.Max(1, o.Size.W - 0.8), Math.Max(1, o.Size.H - 0.8));
            var uLabel = o.Str("universeLabel", "");
            if (string.IsNullOrWhiteSpace(uLabel)) uLabel = lang == Lang.De ? "G" : "U";
            // The circles keep clear of the frame and of the name of the universal set in the top left corner.
            var pad = universe ? Math.Max(1.2, tl * 0.4) : 0.2;
            var top = universe ? tl * 1.2 : 0;
            var area = new Rect(frame.X + pad, frame.Y + pad + top, Math.Max(1, frame.W - 2 * pad), Math.Max(1, frame.H - 2 * pad - top));
            var layout = Layout(n);
            var (map, s) = Mx.FitUniform(layout.SelectMany(c => Mx.CircleBox(c.Centre, 1)), area);
            if (universe)
            {
                f.Add(Prim.Rect(frame.X, frame.Y, frame.W, frame.H, Mx.Thin with { StrokeWidth = 0.35 }) with { Tag = "universe" });
                f.Text(new Vec2(frame.X + 0.8, frame.Y + 0.6 + tl * 0.5), uLabel, tl, italic: true, anchor: TextAnchor.Start);
            }
            if (o.Flag("fill", true))
                for (var i = 0; i < n; i++)
                {
                    var c = map(layout[i].Centre);
                    f.Circle(c, s, new Style { Stroke = "none", Fill = Fill.Solid(Colors[i]), Opacity = 0.18 }, "fill");
                }
            var keys = new[] { ("labelA", "A"), ("labelB", "B"), ("labelC", "C") };
            for (var i = 0; i < n; i++)
            {
                var c = map(layout[i].Centre);
                f.Circle(c, s, Mx.Outline with { StrokeWidth = 0.5 }, "set");
                var name = o.Str(keys[i].Item1, keys[i].Item2);
                if (!string.IsNullOrEmpty(name)) f.Text(c + layout[i].Out * (s * 0.5), name, tl, italic: true);
                f.Port("c" + (i + 1).ToString(CultureInfo.InvariantCulture), c);
            }
            if (universe) f.Rail(new Vec2(frame.X, frame.Y), new Vec2(frame.Right, frame.Y), "frame");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var n = o.Int("sets", 2);
            yield return new ContextAction("math.venn.more", new I18nText("Three sets", "Drei Mengen"), IconIds.CountMore,
                x => x.WithParam("sets", 3)) { Group = "sets", ParamKeys = new[] { "sets" }, IsEnabled = n < 3 };
            yield return new ContextAction("math.venn.fewer", new I18nText("Two sets", "Zwei Mengen"), IconIds.CountFewer,
                x => x.WithParam("sets", 2)) { Group = "sets", ParamKeys = new[] { "sets" }, IsEnabled = n > 2 };
            var names = string.Join("; ", new[] { o.Str("labelA", "A"), o.Str("labelB", "B"), o.Str("labelC", "C") }.Take(Math.Max(2, Math.Min(3, n))));
            yield return ContextAction.WithInput("math.venn.names", new I18nText("Set names…", "Mengennamen…"), IconIds.Name,
                new I18nText("Set names (separated by ;)", "Mengennamen (durch ; getrennt)"), names, (x, t) =>
                {
                    var parts = (t ?? "").Split(';').Select(p => p.Trim()).ToArray();
                    var keys = new[] { "labelA", "labelB", "labelC" };
                    for (var i = 0; i < Math.Min(3, parts.Length); i++) x = x.WithParam(keys[i], parts[i]);
                    return x;
                }) with { Group = "labels", ParamKeys = new[] { "labelA", "labelB", "labelC" } };
            yield return Mx.Toggle(o, "math.venn.universe", new I18nText("Universal set", "Grundmenge"), "universe", true, "labels");
            yield return Mx.Toggle(o, "math.venn.fill", new I18nText("Tinted sets", "Mengen getönt"), "fill", true, "labels", IconIds.FillTint);
        }
    }

    /// <summary>
    /// A probability (exact or decimal) as typed or computed: a fraction n/d, a percentage or a decimal number.
    /// </summary>
    internal readonly struct Prob
    {
        public Prob(double value, long num = 0, long den = 0, bool percent = false)
        {
            Value = value; Num = num; Den = den; Percent = percent;
        }

        public double Value { get; }
        public long Num { get; }
        public long Den { get; }
        public bool IsFraction => Den > 0;
        public bool Percent { get; }

        private static long Gcd(long a, long b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) (a, b) = (b, a % b); return a == 0 ? 1 : a; }

        public static Prob Fraction(long n, long d)
        {
            if (d < 0) { n = -n; d = -d; }
            var g = Gcd(n, d);
            n /= g; d /= g;
            return new Prob((double)n / d, n, d);
        }

        /// <summary>Reads "0.3", "0,3", "30 %", "1/6"; null for empty or invalid text and for values outside 0 … 1.</summary>
        public static Prob? Parse(string? text)
        {
            var s = (text ?? "").Trim().Replace('−', '-');
            if (s.Length == 0) return null;
            var slash = s.IndexOf('/');
            if (slash > 0)
            {
                if (long.TryParse(s.Substring(0, slash).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    && long.TryParse(s.Substring(slash + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)
                    && d > 0 && d < 1_000_000 && n >= 0 && n <= d)
                    return Fraction(n, d);
                return null;
            }
            var percent = s.EndsWith("%", StringComparison.Ordinal);
            if (percent) s = s.Substring(0, s.Length - 1).Trim();
            if (!double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !Mx.Finite(v)) return null;
            if (percent) v /= 100;
            if (v < 0 || v > 1 + 1e-12) return null;
            return new Prob(Math.Min(1, v), percent: percent);
        }

        public string Format(Lang lang)
        {
            if (IsFraction) return Num.ToString(CultureInfo.InvariantCulture) + "/" + Den.ToString(CultureInfo.InvariantCulture);
            if (Percent) return Mx.Num(Value * 100, lang, 2) + " %";
            return Mx.Num(Value, lang, 4);
        }
    }

    /// <summary>
    /// Probability tree: 1 … 3 stages with 2 or 3 branches per node, the outcomes at the nodes, the branch probabilities on
    /// the branches and the path probabilities (product rule) at the leaves. The layout is automatic (leaves evenly spaced,
    /// each node centred over its children). Probabilities per stage are typed as "0.3; 0.7" (several nodes of a stage
    /// separated by "|"); a missing last value is the complement, missing values share what is left.
    /// </summary>
    internal sealed class ProbabilityTreeType : MathBoxType
    {
        public ProbabilityTreeType() : base("math.tree.probability", new I18nText("Probability tree", "Baumdiagramm (Wahrscheinlichkeit)"), MathCategories.Stochastics,
            "probability", "tree", "tree diagram", "stochastics", "wahrscheinlichkeit", "baumdiagramm", "stochastik", "pfadregel", "product rule", "zufallsexperiment", "mehrstufig") { }

        public override Size2 DefaultSize => new(84, 54);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Int("levels", "Stages", "Stufen", 2, 1, 3),
            Pm.Int("branches", "Branches per node", "Äste je Knoten", 2, 2, 3),
            Pm.Text("e1", "Outcomes of stage 1 (empty: A; Ā)", "Ergebnisse Stufe 1 (leer: A; Ā)", ""),
            Pm.Text("e2", "Outcomes of stage 2 (empty: B; B̄)", "Ergebnisse Stufe 2 (leer: B; B̄)", ""),
            Pm.Text("e3", "Outcomes of stage 3 (empty: C; C̄)", "Ergebnisse Stufe 3 (leer: C; C̄)", ""),
            Pm.Text("p1", "Probabilities of stage 1", "Wahrscheinlichkeiten Stufe 1", "0.3"),
            Pm.Text("p2", "Probabilities of stage 2 (nodes separated by |)", "Wahrscheinlichkeiten Stufe 2 (Knoten durch | getrennt)", "0.6 | 0.2"),
            Pm.Text("p3", "Probabilities of stage 3 (nodes separated by |)", "Wahrscheinlichkeiten Stufe 3 (Knoten durch | getrennt)", ""),
            Pm.Bool("products", "Path probabilities", "Pfadwahrscheinlichkeiten", true),
        };

        /// <summary>The outcome names of a stage (1-based) for <paramref name="b"/> branches.</summary>
        internal static string[] Outcomes(SketchObject o, int stage, int b)
        {
            var letter = ((char)('A' + stage - 1)).ToString();
            var defaults = b == 2 ? new[] { letter, letter + "̄" } : Enumerable.Range(1, b).Select(i => letter + i.ToString(CultureInfo.InvariantCulture)).ToArray();
            var typed = o.Str("e" + stage.ToString(CultureInfo.InvariantCulture), "").Split(';').Select(s => s.Trim()).ToArray();
            return Enumerable.Range(0, b).Select(i => i < typed.Length && typed[i].Length > 0 ? typed[i] : defaults[i]).ToArray();
        }

        /// <summary>The branch probabilities of node <paramref name="node"/> (0-based within its stage) of a stage.</summary>
        internal static Prob[] Branches(SketchObject o, int stage, int node, int b)
        {
            var text = o.Str("p" + stage.ToString(CultureInfo.InvariantCulture), "");
            var groups = text.Split('|');
            var group = groups[Math.Min(node, groups.Length - 1)];
            var typed = group.Split(';').Select(s => Prob.Parse(s)).Take(b).ToList();
            while (typed.Count < b) typed.Add(null);
            var given = typed.Where(p => p.HasValue).Select(p => p!.Value).ToList();
            var missing = b - given.Count;
            if (missing == 0) return typed.Select(p => p!.Value).ToArray();
            Prob share;
            if (given.Count == 0) share = Prob.Fraction(1, b);
            else if (given.All(p => p.IsFraction))
            {
                // Exact remainder: (1 − Σ n/d) / missing.
                long num = 1, den = 1;
                foreach (var p in given) { num = num * p.Den - p.Num * den; den *= p.Den; var g = Gcd(num, den); num /= g; den /= g; }
                share = num < 0 ? new Prob(double.NaN) : Prob.Fraction(num, den * missing);
            }
            else
            {
                var rest = (1 - given.Sum(p => p.Value)) / missing;
                share = rest < -1e-9 ? new Prob(double.NaN) : new Prob(Math.Max(0, rest), percent: given.All(p => p.Percent));
            }
            return typed.Select(p => p ?? share).ToArray();
        }

        private static long Gcd(long a, long b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) (a, b) = (b, a % b); return a == 0 ? 1 : a; }

        /// <summary>The product of the probabilities along a path: exact if every factor is a fraction.</summary>
        internal static Prob Product(IReadOnlyList<Prob> path)
        {
            if (path.All(p => p.IsFraction && !double.IsNaN(p.Value)))
            {
                long n = 1, d = 1;
                foreach (var p in path)
                {
                    n *= p.Num; d *= p.Den;
                    var g = Gcd(n, d); n /= g; d /= g;
                    if (d > 1_000_000_000) return new Prob(path.Aggregate(1.0, (a, q) => a * q.Value));
                }
                return Prob.Fraction(n, d);
            }
            return new Prob(path.Aggregate(1.0, (a, q) => a * q.Value));
        }

        private sealed class Node
        {
            public int Stage;
            public int Index;
            public Vec2 P;
            public string Name = "";
            public Prob Prob;
            public List<Prob> Path = new();
            public List<Node> Children = new();
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            var levels = Math.Max(1, Math.Min(3, o.Int("levels", 2)));
            var b = Math.Max(2, Math.Min(3, o.Int("branches", 2)));
            var products = o.Flag("products", true);
            var k = K(o);
            var leaves = (int)Math.Pow(b, levels);

            // Tree in breadth-first order.
            var root = new Node { Stage = 0 };
            var stages = new List<List<Node>> { new() { root } };
            for (var s = 1; s <= levels; s++)
            {
                var names = Outcomes(o, s, b);
                var list = new List<Node>();
                for (var j = 0; j < stages[s - 1].Count; j++)
                {
                    var parent = stages[s - 1][j];
                    var ps = Branches(o, s, j, b);
                    for (var i = 0; i < b; i++)
                    {
                        var child = new Node { Stage = s, Index = list.Count, Name = names[i], Prob = ps[i], Path = parent.Path.Append(ps[i]).ToList() };
                        parent.Children.Add(child);
                        list.Add(child);
                    }
                }
                stages.Add(list);
            }

            // Text sizes: the leaves share the height.
            var frame = new Rect(-o.Size.W / 2 + 0.4, -o.Size.H / 2 + 0.4, Math.Max(1, o.Size.W - 0.8), Math.Max(1, o.Size.H - 0.8));
            var dy = frame.H / leaves;
            var ns = Math.Max(0.8, Math.Min(Mx.LabelSize * k * 0.85, dy * 0.8));
            var ps0 = Math.Max(0.7, Math.Min(Mx.NumberSize * k, dy * 0.75));
            var nameW = stages.Skip(1).SelectMany(l => l).Select(n => Mx.TextW(n.Name, ns)).DefaultIfEmpty(1).Max();
            var leafTexts = stages[levels].Select(n => !products ? "" : Product(n.Path) is var pp && double.IsNaN(pp.Value) ? "?" : Product(n.Path).Format(lang)).ToList();
            var prodW = products ? leafTexts.Select(t => Mx.TextW("= " + t, ps0)).DefaultIfEmpty(0).Max() + 1 : 0;
            var left = frame.X + 0.8;
            var right = frame.Right - prodW - nameW / 2;
            var dx = Math.Max(1, (right - left) / levels);

            // Leaves evenly spaced, inner nodes centred over their children.
            for (var i = 0; i < stages[levels].Count; i++) stages[levels][i].P = new Vec2(left + levels * dx, frame.Y + dy * (i + 0.5));
            for (var s = levels - 1; s >= 0; s--)
                foreach (var n in stages[s]) n.P = new Vec2(left + s * dx, n.Children.Average(c => c.P.Y));

            var line = Style.ThinLine with { StrokeWidth = 0.3 };
            f.Dot(root.P, Math.Max(0.35, 0.45 * Math.Min(1.5, k)));
            f.Port("root", root.P);
            for (var s = 0; s < levels; s++)
                foreach (var parent in stages[s])
                    foreach (var child in parent.Children)
                    {
                        var from = parent.P + new Vec2(parent.Stage == 0 ? 0.6 : Mx.TextW(parent.Name, ns) / 2 + 0.6, 0);
                        var to = child.P - new Vec2(Mx.TextW(child.Name, ns) / 2 + 0.6, 0);
                        f.Line(from, to, line, "branch");
                        var d = to - from;
                        var text = double.IsNaN(child.Prob.Value) ? "?" : child.Prob.Format(lang);
                        // Above rising and level branches, below falling ones. The middle one of three branches is labelled further
                        // out, in the wedge between it and the upper branch, small enough to fit there.
                        var nrm = Mx.UpperNormal(d);
                        if (d.Y > 1e-9) nrm = -nrm;
                        var middle = parent.Children.Count == 3 && parent.Children[1] == child;
                        var t = middle ? 0.72 : 0.5;
                        var size = ps0;
                        if (middle)
                        {
                            var gap = Math.Abs(parent.Children[0].P.Y - child.P.Y) * t;
                            size = Math.Max(0.6, Math.Min(ps0, (gap - 0.5) / 1.15));
                        }
                        f.Text(Mx.Beside(Vec2.Lerp(from, to, t), nrm, text, size, 0.3), text, size, tag: MathTags.Number);
                    }
            foreach (var st in stages.Skip(1))
                foreach (var n in st)
                {
                    f.Text(n.P, n.Name, ns, italic: true);
                    f.Pt(AnchorRole.Mid, n.P, "n" + n.Stage.ToString(CultureInfo.InvariantCulture) + "." + n.Index.ToString(CultureInfo.InvariantCulture));
                }
            if (products)
                for (var i = 0; i < stages[levels].Count; i++)
                {
                    var leaf = stages[levels][i];
                    var at = leaf.P + new Vec2(Mx.TextW(leaf.Name, ns) / 2 + 1, 0);
                    f.Text(at, "= " + leafTexts[i], ps0, anchor: TextAnchor.Start, tag: MathTags.Formula);
                }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var levels = o.Int("levels", 2);
            var b = o.Int("branches", 2);
            yield return new ContextAction("math.tree.more", new I18nText("More stages", "Mehr Stufen"), IconIds.CountMore,
                x => x.WithParam("levels", Math.Min(3, x.Int("levels", 2) + 1))) { Group = "stages", ParamKeys = new[] { "levels" }, IsEnabled = levels < 3 };
            yield return new ContextAction("math.tree.fewer", new I18nText("Fewer stages", "Weniger Stufen"), IconIds.CountFewer,
                x => x.WithParam("levels", Math.Max(1, x.Int("levels", 2) - 1))) { Group = "stages", ParamKeys = new[] { "levels" }, IsEnabled = levels > 1 };
            yield return new ContextAction("math.tree.branches", b == 2 ? new I18nText("Three branches", "Drei Äste") : new I18nText("Two branches", "Zwei Äste"),
                IconIds.Option, x => x.WithParam("branches", x.Int("branches", 2) == 2 ? 3 : 2)) { Group = "stages", ParamKeys = new[] { "branches" } };
            for (var s = 1; s <= Math.Max(1, Math.Min(3, levels)); s++)
            {
                var n = s.ToString(CultureInfo.InvariantCulture);
                // One pictogram per meaning: the stages differ by their number only (the hosts show the labels).
                yield return Mx.TextInput(o, "math.tree.probabilities." + n, new I18nText("Probabilities of stage " + n + "…", "Wahrscheinlichkeiten Stufe " + n + "…"), "p" + n, "", "values")
                    with { Icon = IconIds.Value };
                yield return Mx.TextInput(o, "math.tree.outcomes." + n, new I18nText("Outcomes of stage " + n + "…", "Ergebnisse Stufe " + n + "…"), "e" + n, "", "values");
            }
            yield return Mx.Toggle(o, "math.tree.products", new I18nText("Path probabilities", "Pfadwahrscheinlichkeiten"), "products", true, "values");
        }
    }
}
