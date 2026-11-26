using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A flow of a Sankey diagram: source and target node index, value and the data row it came from (0-based).</summary>
    internal sealed record SankeyFlow(int Source, int Target, double Value, int Row);

    /// <summary>
    /// The flows of a Sankey diagram, read from the data text: one flow per row "source;target;value" with the separators of
    /// <see cref="ChartData"/> (tab, semicolon, comma; decimal comma accepted, rows separated by "|" in the prompt). Nodes are
    /// named by their first appearance; rows that are not a flow (headers, comments, a value that is not positive) are skipped.
    /// </summary>
    internal sealed class SankeyModel
    {
        public List<string> Nodes { get; } = new();
        public List<SankeyFlow> Flows { get; } = new();

        public static SankeyModel Parse(string? text)
        {
            var m = new SankeyModel();
            if (string.IsNullOrWhiteSpace(text)) return m;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            int Node(string name)
            {
                if (!index.TryGetValue(name, out var i)) { i = m.Nodes.Count; m.Nodes.Add(name); index[name] = i; }
                return i;
            }
            var lines = text!.Replace("\r\n", "\n").Replace('\r', '\n').Replace('|', '\n').Split('\n');
            var row = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;
                var f = ChartData.SplitFields(line);
                var r = row++;
                if (f.Count < 3) continue;
                var source = Unquote(f[0]); var target = Unquote(f[1]);
                if (source.Length == 0 || target.Length == 0 || !ChartData.TryNumber(f[2], out var v) || !Fmt.Finite(v) || v <= 0) continue;
                m.Flows.Add(new SankeyFlow(Node(source), Node(target), v, r));
            }
            return m;
        }

        private static string Unquote(string field)
        {
            var t = field.Trim();
            if (t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"') t = t.Substring(1, t.Length - 2).Replace("\"\"", "\"");
            return t.Trim();
        }

        /// <summary>The data text of the model (one row per flow; names quoted when they contain a separator).</summary>
        public string ToText()
        {
            static string Name(string s) => s.IndexOfAny(new[] { ';', '\t', '|', '"' }) >= 0 || s.Length == 0 ? "\"" + s.Replace("\"", "\"\"").Replace("|", "/").Replace(";", ",") + "\"" : s;
            return string.Join("\n", Flows.Select(f => Name(Nodes[f.Source]) + ";" + Name(Nodes[f.Target]) + ";" + ExportText.N(f.Value)));
        }
    }

    /// <summary>
    /// Left-to-right layered layout of a Sankey diagram: every node in the column of its longest path from a source, nodes
    /// without outgoing flows in the last column, node heights proportional to the larger of their inflow and outflow (one
    /// scale for the whole diagram, chosen so the fullest column fits), columns centred vertically and ordered by the mean
    /// position of their neighbours to reduce crossings. Flows that run against the direction (cycles) are not laid out.
    /// </summary>
    internal sealed class SankeyLayout
    {
        public int[] Layer { get; private set; } = Array.Empty<int>();
        public int Layers { get; private set; }
        public double[] X { get; private set; } = Array.Empty<double>();
        public double[] Y { get; private set; } = Array.Empty<double>();
        public double[] H { get; private set; } = Array.Empty<double>();
        public double[] In { get; private set; } = Array.Empty<double>();
        public double[] Out { get; private set; } = Array.Empty<double>();
        /// <summary>Millimetres per unit of value.</summary>
        public double Scale { get; private set; }
        public double NodeWidth { get; private set; }
        /// <summary>Laid out flows: top edge at the source and at the target, thickness.</summary>
        public List<(SankeyFlow Flow, double Y0, double Y1, double T)> Ribbons { get; } = new();
        /// <summary>Flows against the direction (part of a cycle) that are not drawn.</summary>
        public List<SankeyFlow> Backward { get; } = new();

        public static SankeyLayout Compute(SankeyModel m, Rect area, double nodeWidth, double gap)
        {
            var n = m.Nodes.Count;
            var l = new SankeyLayout { NodeWidth = nodeWidth };
            l.Layer = new int[n]; l.X = new double[n]; l.Y = new double[n]; l.H = new double[n]; l.In = new double[n]; l.Out = new double[n];
            if (n == 0) return l;

            // Back edges of a depth-first search close cycles: they are left out.
            var outs = Enumerable.Range(0, n).Select(_ => new List<SankeyFlow>()).ToArray();
            foreach (var f in m.Flows) outs[f.Source].Add(f);
            var state = new int[n];
            var order = new List<int>();
            var forward = new List<SankeyFlow>();
            void Visit(int u)
            {
                // Iterative depth-first search (no recursion depth limit for long chains).
                var stack = new Stack<(int Node, int Next)>();
                stack.Push((u, 0));
                state[u] = 1;
                while (stack.Count > 0)
                {
                    var (v, i) = stack.Pop();
                    if (i < outs[v].Count)
                    {
                        stack.Push((v, i + 1));
                        var f = outs[v][i];
                        if (state[f.Target] == 1) { l.Backward.Add(f); continue; }
                        forward.Add(f);
                        if (state[f.Target] == 0) { state[f.Target] = 1; stack.Push((f.Target, 0)); }
                    }
                    else { state[v] = 2; order.Add(v); }
                }
            }
            for (var i = 0; i < n; i++) if (state[i] == 0) Visit(i);
            order.Reverse();
            var fins = Enumerable.Range(0, n).Select(_ => new List<SankeyFlow>()).ToArray();
            var fouts = Enumerable.Range(0, n).Select(_ => new List<SankeyFlow>()).ToArray();
            foreach (var f in forward) { fins[f.Target].Add(f); fouts[f.Source].Add(f); }
            foreach (var u in order)
                foreach (var f in fouts[u])
                    l.Layer[f.Target] = Math.Max(l.Layer[f.Target], l.Layer[u] + 1);
            var max = l.Layer.Max();
            for (var i = 0; i < n; i++) if (fouts[i].Count == 0 && fins[i].Count > 0) l.Layer[i] = max;
            l.Layers = max + 1;
            for (var i = 0; i < n; i++) { l.In[i] = fins[i].Sum(f => f.Value); l.Out[i] = fouts[i].Sum(f => f.Value); }
            var value = Enumerable.Range(0, n).Select(i => Math.Max(l.In[i], l.Out[i])).ToArray();

            var columns = Enumerable.Range(0, l.Layers).Select(c => Enumerable.Range(0, n).Where(i => l.Layer[i] == c).ToList()).ToList();
            var most = columns.Max(c => c.Count);
            if (most > 1) gap = Math.Min(gap, area.H * 0.4 / (most - 1));
            l.Scale = columns.Where(c => c.Count > 0).Select(c => c.Sum(i => value[i]) is var s && s > 0 ? (area.H - gap * (c.Count - 1)) / s : double.PositiveInfinity).DefaultIfEmpty(0).Min();
            if (!Fmt.Finite(l.Scale) || l.Scale < 0) l.Scale = 0;
            for (var i = 0; i < n; i++)
            {
                l.H[i] = value[i] * l.Scale;
                l.X[i] = max == 0 ? area.X + (area.W - nodeWidth) / 2 : area.X + l.Layer[i] * (area.W - nodeWidth) / max;
            }

            void Stack(List<int> col)
            {
                var used = col.Sum(i => l.H[i]) + gap * Math.Max(0, col.Count - 1);
                var y = area.Y + (area.H - used) / 2;
                foreach (var i in col) { l.Y[i] = y; y += l.H[i] + gap; }
            }
            foreach (var col in columns) Stack(col);
            double Centre(int i) => l.Y[i] + l.H[i] / 2;
            // Order every column by the weighted mean position of its neighbours (left to right, then right to left).
            for (var pass = 0; pass < 3; pass++)
            {
                for (var c = 1; c < columns.Count; c++) Sort(columns[c], i => fins[i].Select(f => (Centre(f.Source), f.Value)));
                for (var c = columns.Count - 2; c >= 0; c--) Sort(columns[c], i => fouts[i].Select(f => (Centre(f.Target), f.Value)));
            }
            void Sort(List<int> col, Func<int, IEnumerable<(double Y, double W)>> neighbours)
            {
                var keys = col.ToDictionary(i => i, i =>
                {
                    var nb = neighbours(i).ToList();
                    var w = nb.Sum(x => x.W);
                    return w > 0 ? nb.Sum(x => x.Y * x.W) / w : Centre(i);
                });
                var sorted = col.Select((i, k) => (i, k)).OrderBy(x => keys[x.i]).ThenBy(x => x.k).Select(x => x.i).ToList();
                col.Clear(); col.AddRange(sorted);
                Stack(col);
            }

            // Flows: stacked at the source in the order of their targets, at the target in the order of their sources.
            var y0 = new Dictionary<SankeyFlow, double>();
            var y1 = new Dictionary<SankeyFlow, double>();
            for (var i = 0; i < n; i++)
            {
                var y = l.Y[i];
                foreach (var f in fouts[i].OrderBy(f => Centre(f.Target)).ThenBy(f => f.Row)) { y0[f] = y; y += f.Value * l.Scale; }
                y = l.Y[i];
                foreach (var f in fins[i].OrderBy(f => Centre(f.Source)).ThenBy(f => f.Row)) { y1[f] = y; y += f.Value * l.Scale; }
            }
            foreach (var f in forward.OrderBy(f => f.Row)) l.Ribbons.Add((f, y0[f], y1[f], f.Value * l.Scale));
            return l;
        }
    }

    /// <summary>
    /// Sankey diagram: flows between nodes as ribbons whose widths are proportional to the values, laid out left to right in
    /// columns (<see cref="SankeyLayout"/>). Nodes are coloured bars with their names (and values); a flow takes the colour
    /// of its source. In monochrome output the nodes are black and the flows outlined. Every node, label and flow is a part.
    /// </summary>
    internal sealed class SankeyChart : CanvasChart
    {
        public const string TypeId = "chart.sankey";

        public const string DefaultData = "A;M;30\nB;M;45\nC;M;25\nM;X;50\nM;Y;35\nM;Z;15";

        public static readonly ParamDef DataDef = Prm.Text("data", "Flows (rows source;target;value)", "Flüsse (Zeilen Quelle;Ziel;Wert)", DefaultData);
        public static readonly ParamDef ValuesDef = Prm.Bool("values", "Values at the nodes", "Werte an den Knoten", true);

        public SankeyChart() : base(TypeId, new I18nText("Sankey diagram", "Sankey-Diagramm"), ChartCategories.Data,
            "sankey", "flow", "flows", "energy flow", "material flow", "budget", "allocation", "source", "target", "stream",
            "sankey-diagramm", "flussdiagramm", "energiefluss", "stoffstrom", "materialfluss", "mengenfluss", "aufteilung", "quelle", "ziel")
        { }

        public override Size2 Design => new(100, 65);

        protected override IReadOnlyList<ParamDef> BuildParams() => new[] { DataDef, ValuesDef, Common.Title, Common.Patterns };

        /// <summary>The drawn diagram (for part names and tests).</summary>
        internal sealed class Drawing
        {
            public SankeyModel Model { get; set; } = new();
            public SankeyLayout Layout { get; set; } = new();
        }

        private static string Label(SankeyModel m, SankeyLayout l, int i, bool values, Lang lang)
            => values ? m.Nodes[i] + " " + Fmt.Num(Math.Max(l.In[i], l.Out[i]), lang, 3) : m.Nodes[i];

        protected override void Paint(SketchObject o, RenderContext ctx, Canvas cv)
        {
            var lang = ctx.Lang;
            var m = SankeyModel.Parse(o.Str("data", DefaultData));
            var values = o.Flag("values", true);
            var mono = cv.Mono || o.Flag("patterns", false);
            cv.LegendMode = "none";
            cv.Layout();
            var fs = cv.Fs * 0.85;
            var nodeW = Math.Max(0.6, cv.Fs * 0.55);
            // A first layout gives the columns; the labels of the first column go left, all others right of their node.
            var probe = SankeyLayout.Compute(m, cv.Area, nodeW, cv.Fs * 0.6);
            double Width(Func<int, bool> which) => Enumerable.Range(0, m.Nodes.Count).Where(which)
                .Select(i => Fmt.Width(Label(m, probe, i, values, lang), fs) + cv.Gap * 2).DefaultIfEmpty(0).Max();
            var last = probe.Layers - 1;
            var left = probe.Layers > 1 ? Math.Min(Width(i => probe.Layer[i] == 0), cv.Area.W * 0.3) : 0;
            var right = Math.Min(Width(i => probe.Layer[i] == last), cv.Area.W * 0.3);
            var bottom = probe.Backward.Count > 0 ? fs * 1.4 : 0;
            var area = new Rect(cv.Area.X + left, cv.Area.Y, Math.Max(cv.Area.W - left - right, nodeW * 2), Math.Max(cv.Area.H - bottom, 1));
            var l = SankeyLayout.Compute(m, area, nodeW, cv.Fs * 0.6);
            cv.Model = new Drawing { Model = m, Layout = l };

            foreach (var (f, y0, y1, t) in l.Ribbons)
            {
                var x0 = l.X[f.Source] + nodeW; var x1 = l.X[f.Target];
                var xm = (x0 + x1) / 2;
                var segs = new List<PathSeg>
                {
                    PathSeg.Move(new Vec2(x0, y0)),
                    PathSeg.Cubic(new Vec2(xm, y0), new Vec2(xm, y1), new Vec2(x1, y1)),
                    PathSeg.Line(new Vec2(x1, y1 + t)),
                    PathSeg.Cubic(new Vec2(xm, y1 + t), new Vec2(xm, y0 + t), new Vec2(x0, y0 + t)),
                    PathSeg.Close,
                };
                var style = mono
                    ? new Style { Stroke = Palette.Black, StrokeWidth = 0.15, Role = LineRole.Thin, Fill = Fill.Solid(Palette.White) }
                    : new Style { Stroke = "none", Role = LineRole.Visible, Fill = Fill.Solid(SeriesLook.Of(f.Source).Color), Opacity = 0.35 };
                cv.Back.Add(new PathPrim(segs, style) { Tag = ChartTags.Nth(ChartTags.Flow, m.Flows.IndexOf(f)) });
            }
            for (var i = 0; i < m.Nodes.Count; i++)
            {
                var h = Math.Max(l.H[i], 0.05);
                var color = mono ? Palette.Black : SeriesLook.Of(i).Color;
                cv.Main.Add(Prim.Rect(l.X[i], l.Y[i], nodeW, h, new Style { Stroke = color, StrokeWidth = 0.1, Role = LineRole.Visible, Fill = Fill.Solid(color) }) with { Tag = ChartTags.Nth(ChartTags.Node, i) });
                var leftSide = l.Layers > 1 && l.Layer[i] == 0;
                var at = new Vec2(leftSide ? l.X[i] - cv.Gap : l.X[i] + nodeW + cv.Gap, l.Y[i] + h / 2);
                var label = cv.Text(at, Label(m, l, i, values, lang), fs, leftSide ? TextAnchor.End : TextAnchor.Start, ChartTags.Nth(ChartTags.NodeLabel, i));
                // Labels over the flows (middle columns) get a white backing in monochrome output, where the flows are outlined.
                if (mono && l.Layer[i] > 0 && l.Layer[i] < l.Layers - 1)
                {
                    var w = Fmt.Width(label.Text, label.TextStyle.Size);
                    var s = label.TextStyle.Size;
                    var x1 = Math.Min(at.X + w + s * 0.15, cv.W / 2);
                    cv.Over.Add(Prim.Rect(at.X - s * 0.15, at.Y - s * 0.6, Math.Max(0.05, x1 - at.X + s * 0.15), s * 1.2, S.FillOnly(Palette.White)) with { Tag = label.Tag });
                }
                cv.Over.Add(label);
                cv.Port(new Vec2(l.X[i], l.Y[i] + h / 2), "n" + Fmt.I(i + 1) + ".in");
                cv.Port(new Vec2(l.X[i] + nodeW, l.Y[i] + h / 2), "n" + Fmt.I(i + 1) + ".out");
            }
            if (l.Backward.Count > 0)
            {
                var text = lang == Lang.De
                    ? Fmt.I(l.Backward.Count) + (l.Backward.Count == 1 ? " Fluss gegen die Richtung (Kreislauf) nicht gezeigt" : " Flüsse gegen die Richtung (Kreislauf) nicht gezeigt")
                    : Fmt.I(l.Backward.Count) + (l.Backward.Count == 1 ? " flow against the direction (cycle) not shown" : " flows against the direction (cycle) not shown");
                cv.Over.Add(cv.Text(new Vec2(cv.Area.X, cv.Area.Bottom - fs * 0.6), text, fs * 0.9, TextAnchor.Start, ChartTags.Note, Palette.Helper));
            }
            cv.PlotAnchors(area);
        }

        protected override I18nText PartName(string key, Canvas cv)
        {
            var i = IndexOf(key);
            if (cv.Model is not Drawing d) return base.PartName(key, cv);
            var m = d.Model;
            string Node(int k) => k >= 0 && k < m.Nodes.Count ? m.Nodes[k] : Fmt.I(k + 1);
            return KindOf(key) switch
            {
                ChartTags.Node when i >= 0 => new I18nText("Node " + Node(i), "Knoten " + Node(i)),
                ChartTags.NodeLabel when i >= 0 => new I18nText("Label – " + Node(i), "Beschriftung – " + Node(i)),
                ChartTags.Flow when i >= 0 && i < m.Flows.Count => new I18nText("Flow " + Node(m.Flows[i].Source) + " → " + Node(m.Flows[i].Target),
                    "Fluss " + Node(m.Flows[i].Source) + " → " + Node(m.Flows[i].Target)),
                ChartTags.Note => new I18nText("Note", "Hinweis"),
                _ => base.PartName(key, cv),
            };
        }

        /// <summary>Appends a flow from the last node to a new one (half of the last flow's value).</summary>
        internal static SketchObject AddFlow(SketchObject x)
        {
            var m = SankeyModel.Parse(x.Str("data", DefaultData));
            var from = m.Nodes.Count > 0 ? m.Nodes[m.Nodes.Count - 1] : "A";
            var k = m.Nodes.Count + 1;
            var names = new HashSet<string>(m.Nodes, StringComparer.Ordinal);
            while (names.Contains("N" + Fmt.I(k))) k++;
            var v = m.Flows.Count > 0 ? m.Flows[m.Flows.Count - 1].Value / 2 : 10;
            var text = x.Str("data", DefaultData).TrimEnd();
            return x.WithParam("data", (text.Length > 0 ? text + "\n" : "") + from + ";N" + Fmt.I(k) + ";" + ExportText.N(v));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = o.Str("data", DefaultData).Replace("\r\n", "\n").Replace('\r', '\n');
            var rows = current.Split('\n').Select(r => r.Trim()).Where(r => r.Length > 0).ToList();
            var flows = SankeyModel.Parse(current).Flows.Count;
            return new[]
            {
                ContextAction.WithInput("chart.data", new I18nText("Data…", "Daten…"), IconIds.Data,
                    new I18nText("Flow rows “source;target;value” separated by |", "Flusszeilen „Quelle;Ziel;Wert“ getrennt durch |"),
                    string.Join(" | ", rows),
                    (x, text) => x.WithParam("data", string.Join("\n", (text ?? "").Split('|', '\n').Select(l => l.Trim()).Where(l => l.Length > 0)))) with { Group = "data" },
                new ContextAction("chart.sankey.add", new I18nText("Add flow", "Fluss hinzufügen"), IconIds.RowAdd, AddFlow) { Group = "data" },
                new ContextAction("chart.sankey.remove", new I18nText("Remove last flow", "Letzten Fluss entfernen"), IconIds.RowRemove, x =>
                {
                    var lines = x.Str("data", DefaultData).Replace("\r\n", "\n").Replace('\r', '\n').Replace('|', '\n').Split('\n').ToList();
                    var model = SankeyModel.Parse(x.Str("data", DefaultData));
                    if (model.Flows.Count < 2) return x;
                    var row = model.Flows[model.Flows.Count - 1].Row;
                    var k = -1;
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var t = lines[i].Trim();
                        if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal) || t.StartsWith("//", StringComparison.Ordinal)) continue;
                        if (++k == row) { lines.RemoveAt(i); break; }
                    }
                    return x.WithParam("data", string.Join("\n", lines).Trim('\n'));
                }) { Group = "data", IsEnabled = flows > 1 },
                Prm.Toggle(o, "chart.values", "values", true, new I18nText("Values at the nodes", "Werte an den Knoten"), IconIds.LabelShow),
            };
        }
    }
}
