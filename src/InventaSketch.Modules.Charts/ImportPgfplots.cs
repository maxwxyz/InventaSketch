using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Syn = InventaSketch.Core.TikzImport.Syntax;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Reads pgfplots axes of a LaTeX text (roadmap 4b) into the neutral figure model: axis, semilogxaxis, semilogyaxis,
    /// loglogaxis, polaraxis and groupplot (stacked panels); \addplot coordinates, inline tables and simple expressions
    /// (evaluated with <see cref="FunctionExpression"/>, degrees as in pgfplots), fill between, bars (ybar/xbar, stacked),
    /// histograms (hist), box plots (boxplot, boxplot prepared), Gantt bars (filled rectangles over part of the height),
    /// \draw value lines and arrows in axis coordinates, \node texts and numerals, labels, ranges, log modes, grids and
    /// legends; pies of pgf-pie (\pie) and the exported TikZ pies and Sankey diagrams (their data keys). Everything else is
    /// reported as not imported.
    /// </summary>
    internal sealed class PgfplotsReader
    {
        private readonly string _s;
        private readonly CodeImportReport _report;
        private readonly Dictionary<string, string> _colors = new(StringComparer.Ordinal);
        private readonly List<ImpFigure> _figures = new();
        private readonly List<int> _lineStarts = new() { 0 };

        private PgfplotsReader(string s, CodeImportReport report)
        {
            _s = s; _report = report;
            for (var i = 0; i < s.Length; i++) if (s[i] == '\n') _lineStarts.Add(i + 1);
        }

        private static readonly Regex AxisBegin = new(@"\\begin\s*\{\s*(axis|semilogxaxis|semilogyaxis|loglogaxis|groupplot|polaraxis|ternaryaxis|smithchart)\s*\}", RegexOptions.CultureInvariant);

        public static List<ImpFigure> Read(string text, CodeImportReport report)
        {
            var src = TikzImport.StripComments((text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'));
            var r = new PgfplotsReader(src, report);
            r.Run();
            return r._figures;
        }

        private int LineOf(int pos)
        {
            var k = _lineStarts.BinarySearch(pos);
            return k >= 0 ? k + 1 : ~k;
        }

        private static string Snip(string code)
        {
            var t = Regex.Replace(code, @"\s+", " ").Trim();
            return t.Length > 90 ? t.Substring(0, 87) + "…" : t;
        }

        private void Skip(int pos, string code, string en, string de) => _report.Skip(LineOf(pos), Snip(code), new I18nText(en, de));

        private sealed class Unsupported : Exception
        {
            public Unsupported(string en, string de) : base(en) { Text = new I18nText(en, de); }
            public I18nText Text { get; }
        }

        private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private void Colors(string part)
        {
            foreach (Match m in Regex.Matches(part, @"\\definecolor\s*\{([^}]*)\}\s*\{([^}]*)\}\s*\{([^}]*)\}"))
                if (TikzImport.Define(m.Groups[2].Value, m.Groups[3].Value) is { } hex) _colors[m.Groups[1].Value.Trim()] = hex;
            foreach (Match m in Regex.Matches(part, @"\\colorlet\s*\{([^}]*)\}\s*\{([^}]*)\}"))
                if (TikzImport.Color(m.Groups[2].Value, _colors) is { } hex) _colors[m.Groups[1].Value.Trim()] = hex;
        }

        // ------------------------------------------------------------------ pictures

        private void Run()
        {
            var pics = Regex.Matches(_s, @"\\begin\s*\{\s*tikzpicture\s*\}").Cast<Match>().ToList();
            var ranges = new List<(int Start, int End)>();
            if (pics.Count == 0) ranges.Add((0, _s.Length));
            foreach (var m in pics)
            {
                var end = _s.IndexOf("\\end{tikzpicture}", m.Index, StringComparison.Ordinal);
                ranges.Add((m.Index + m.Length, end < 0 ? _s.Length : end));
            }
            Colors(_s.Substring(0, pics.Count > 0 ? pics[0].Index : _s.Length));
            foreach (var (start, end) in ranges)
            {
                Colors(_s.Substring(start, end - start));
                Picture(start, end);
            }
            // pgf-pie outside a tikzpicture (\pie makes its own picture).
            if (pics.Count > 0)
                foreach (Match m in PieCommand.Matches(_s))
                    if (!ranges.Any(r => m.Index >= r.Start && m.Index < r.End)) PgfPie(m.Index);
            foreach (Match m in Regex.Matches(_s, @"\\begin\s*\{\s*sankeydiagram\s*\}"))
                Skip(m.Index, m.Value, "Diagrams of the sankey package are not imported (the exported Sankey diagrams are).",
                    "Diagramme des Pakets sankey werden nicht importiert (die exportierten Sankey-Diagramme schon).");
        }

        private static readonly Regex PieCommand = new(@"\\pie(?![A-Za-z@])", RegexOptions.CultureInvariant);

        /// <summary>Named coordinates in axis coordinates: figure, panel index, x, y.</summary>
        private readonly Dictionary<string, (ImpFigure Fig, int Panel, double X, double Y)> _named = new(StringComparer.Ordinal);
        /// <summary>Named nodes (numerals): their note, to become a leader when a \draw connects them to a target.</summary>
        private readonly Dictionary<string, (ImpAxes Ax, ImpNote Note, double Dx, double Dy)> _nodes = new(StringComparer.Ordinal);

        private void Picture(int start, int end)
        {
            var axes = AxisBegin.Matches(_s.Substring(start, end - start)).Cast<Match>().ToList();
            if (axes.Count == 0) { Drawing(start, end); return; }
            ImpFigure? primary = null;
            var pos = start;
            foreach (var m in axes)
            {
                var at = start + m.Index;
                if (at < pos) continue; // nested inside an earlier environment
                // Commands between axes (on the picture level).
                Outside(pos, at, primary);
                var env = m.Groups[1].Value;
                var close = "\\end{" + env + "}";
                var bodyStart = at + m.Length;
                var stop = _s.IndexOf(close, bodyStart, StringComparison.Ordinal);
                if (stop < 0 || stop > end) stop = end;
                pos = Math.Min(end, stop + close.Length);
                if (env is "ternaryaxis" or "smithchart")
                {
                    Skip(at, m.Value, "The axis type “" + env + "” is not imported.", "Der Achsentyp „" + env + "“ wird nicht importiert.");
                    continue;
                }
                var k = bodyStart;
                var opts = Options(ref k);
                if (env != "groupplot" && primary is not null && (Has(opts, "axis y line", "right") || Has(opts, "axis x line", "none")))
                {
                    Skip(at, m.Value, "A secondary y axis is not imported.", "Eine zweite y-Achse wird nicht importiert.");
                    continue;
                }
                var fig = new ImpFigure { Line = LineOf(at) };
                if (env == "groupplot") Group(fig, opts, k, stop);
                else
                {
                    var ax = new ImpAxes { Line = LineOf(at) };
                    fig.Axes.Add(ax);
                    if (env is "semilogxaxis" or "loglogaxis") ax.XScale = "log";
                    if (env is "semilogyaxis" or "loglogaxis") ax.YScale = "log";
                    AxisOptions(fig, ax, opts, at);
                    if (env == "polaraxis") PolarOptions(ax, opts, at);
                    Body(fig, ax, 0, k, stop);
                    Finish(fig, ax);
                    Size(fig, opts, 1);
                }
                if (primary is not null) Skip(at, m.Value, "A further axis in the same picture becomes its own chart.", "Eine weitere Achse im selben Bild wird ein eigenes Diagramm.");
                primary ??= fig;
                _figures.Add(fig);
            }
            Outside(pos, end, primary);
        }

        private static bool Has(List<(string Key, string? Value)> opts, string key, string value) => opts.Any(o => o.Key == key && o.Value?.Trim() == value);

        private List<(string Key, string? Value)> Options(ref int k)
        {
            var j = k;
            Ws(_s, ref j);
            if (j < _s.Length && _s[j] == '[')
            {
                var g = Syn.Group(_s, ref j, '[', ']');
                if (g is not null) { k = j; return Syn.Options(g); }
            }
            return new List<(string, string?)>();
        }

        /// <summary>Picture-level commands (after a groupplot: lines between panels, the caption node).</summary>
        private void Outside(int from, int to, ImpFigure? fig)
        {
            var i = from;
            while (i < to)
            {
                var c = _s.IndexOf('\\', i);
                if (c < 0 || c >= to) break;
                var j = c + 1;
                while (j < to && char.IsLetter(_s[j])) j++;
                var cmd = _s.Substring(c + 1, j - c - 1);
                if (cmd is "draw" or "node" or "path" or "fill" or "coordinate")
                {
                    var e = StatementEnd(j, to);
                    var stmt = _s.Substring(c, Math.Min(e + 1, to) - c);
                    try
                    {
                        if (cmd == "node" && fig is not null && stmt.Contains("current bounding box"))
                        {
                            var g = stmt.LastIndexOf('{');
                            var k = g;
                            var text = g >= 0 ? Syn.Group(stmt, ref k, '{', '}') : null;
                            if (text is not null) fig.Caption = TikzImport.PlainText(text, out _, out _);
                        }
                        else if (cmd == "draw" && fig is not null && Regex.Match(stmt, @"\\draw\s*\[([^;]*?)\]\s*\(([^()]+)\)\s*--\s*\(([^()]+)\)\s*;") is { Success: true } dm
                                 && _named.TryGetValue(dm.Groups[2].Value.Trim(), out var a) && _named.TryGetValue(dm.Groups[3].Value.Trim(), out var b) && a.Fig == fig && b.Fig == fig)
                        {
                            var opts = Syn.Options(dm.Groups[1].Value);
                            var (color, line, width, start, endTip) = Look(opts);
                            if (Math.Abs(a.X - b.X) < 1e-12 && start == ArrowHead.None && endTip == ArrowHead.None)
                                fig.PanelLines.Add((a.X, Math.Min(a.Panel, b.Panel), Math.Max(a.Panel, b.Panel), new FigRef { Kind = "v", Color = color, Line = line ?? "solid", Width = width }));
                            else
                                fig.CrossNotes.Add(new ImpNote
                                {
                                    Note = new FigNote
                                    {
                                        Kind = start != ArrowHead.None && endTip != ArrowHead.None ? "dim" : endTip != ArrowHead.None || start != ArrowHead.None ? "arrow" : "line",
                                        P1 = new FigPoint { X = start != ArrowHead.None && endTip == ArrowHead.None ? b.X : a.X, Y = start != ArrowHead.None && endTip == ArrowHead.None ? b.Y : a.Y, Panel = a.Panel },
                                        P2 = new FigPoint { X = start != ArrowHead.None && endTip == ArrowHead.None ? a.X : b.X, Y = start != ArrowHead.None && endTip == ArrowHead.None ? a.Y : b.Y, Panel = b.Panel },
                                        Line = line is "solid" ? null : line,
                                    },
                                    Line = LineOf(c), Code = Snip(stmt),
                                });
                        }
                        else if (stmt.Contains("axis cs:") || fig is null)
                            Skip(c, stmt, "A drawing outside the axis is not imported.", "Eine Zeichnung außerhalb der Achse wird nicht importiert.");
                        else
                            Skip(c, stmt, "A drawing outside the axis is not imported (it can be imported as a TikZ drawing).", "Eine Zeichnung außerhalb der Achse wird nicht importiert (sie kann als TikZ-Zeichnung importiert werden).");
                    }
                    catch (Unsupported ex) { _report.Skip(LineOf(c), Snip(stmt), ex.Text); }
                    i = e + 1;
                    continue;
                }
                i = j;
            }
        }

        private int StatementEnd(int i, int end)
        {
            var depth = 0;
            for (var k = i; k < end; k++)
            {
                var c = _s[k];
                if (c == '\\') { k++; continue; }
                if (c is '{' or '[' or '(') depth++;
                else if (c is '}' or ']' or ')') depth = Math.Max(0, depth - 1);
                else if (c == ';' && depth == 0) return k;
            }
            return end;
        }

        // ------------------------------------------------------------------ pictures without an axis: pies and Sankey diagrams

        /// <summary>
        /// A picture without an axis: an exported pie or Sankey diagram (their data keys "pie chart"/"pie slice" and "sankey
        /// chart"/"sankey flow" hold the chart data; the paths only draw it), or pies of pgf-pie. Other drawings are left to
        /// the TikZ import.
        /// </summary>
        private void Drawing(int start, int end)
        {
            var k = start;
            var opts = Options(ref k);
            var body = _s.Substring(start, end - start);
            var pie = opts.FirstOrDefault(o => o.Key == "pie chart");
            var sankey = opts.FirstOrDefault(o => o.Key == "sankey chart");
            if (pie.Key is not null || body.Contains("pie slice")) { OwnPie(start, end, pie.Value); return; }
            if (sankey.Key is not null || body.Contains("sankey flow")) { OwnSankey(start, end, sankey.Value); return; }
            foreach (Match m in PieCommand.Matches(body)) PgfPie(start + m.Index);
        }

        /// <summary>The options of every path command (\path, \draw, \fill, \filldraw) in the range.</summary>
        private IEnumerable<(int Pos, List<(string Key, string? Value)> Opts)> Paths(int start, int end)
        {
            var i = start;
            while (i < end)
            {
                var c = _s.IndexOf('\\', i);
                if (c < 0 || c >= end) yield break;
                var j = c + 1;
                while (j < end && char.IsLetter(_s[j])) j++;
                var cmd = _s.Substring(c + 1, j - c - 1);
                if (cmd is "fill" or "path" or "draw" or "filldraw")
                {
                    var k = j;
                    var opts = Options(ref k);
                    yield return (c, opts);
                    i = StatementEnd(k, end) + 1;
                    continue;
                }
                i = Math.Max(j, c + 2);
            }
        }

        /// <summary>The brace groups of a value with several arguments ("{A}{M}{30}").</summary>
        private static List<string> Args(string? v)
        {
            var list = new List<string>();
            if (v is null) return list;
            var i = 0;
            while (i < v.Length)
            {
                Ws(v, ref i);
                if (i >= v.Length) break;
                if (v[i] != '{') { list.Add(v.Substring(i).Trim()); break; }
                var g = Syn.Group(v, ref i, '{', '}');
                if (g is null) break;
                list.Add(g);
            }
            return list;
        }

        private static ImpFigure Single(int line, out ImpAxes ax)
        {
            var fig = new ImpFigure { Line = line };
            ax = new ImpAxes { Line = line };
            fig.Axes.Add(ax);
            return fig;
        }

        /// <summary>An exported pie: the settings of "pie chart" and the slices of "pie slice={name}{value}".</summary>
        private void OwnPie(int start, int end, string? settings)
        {
            var names = new List<string>();
            var values = new List<double>();
            foreach (var (pos, opts) in Paths(start, end))
            {
                if (opts.FirstOrDefault(o => o.Key == "pie slice").Value is not { } v) continue;
                var a = Args(v);
                if (a.Count < 2 || Num(a[1]) is not { } value)
                {
                    Skip(pos, "pie slice=" + v, "A slice without a value is not imported.", "Ein Segment ohne Wert wird nicht importiert.");
                    continue;
                }
                names.Add(Text(a[0]));
                values.Add(value);
            }
            if (values.Count == 0) return;
            var fig = Single(LineOf(start), out var ax);
            var set = Syn.Options(settings ?? "");
            string? Get(string key) => set.LastOrDefault(o => o.Key == key).Value?.Trim();
            var pie = new ImpPie { Values = values.ToArray(), Labels = names.ToArray(), Line = LineOf(start) };
            pie.Hole = Num(Get("hole")) ?? 0;
            // The chart's start (degrees clockwise from 12 o'clock) as matplotlib's start angle (counter-clockwise from 3 o'clock).
            pie.StartAngle = 90 - (Num(Get("start")) ?? 0);
            pie.CounterClock = Get("direction") == "ccw";
            if (Get("labels") is "percent" or "name" or "namepercent" or "value" or "none") pie.LabelMode = Get("labels");
            if (Get("legend") is "right" or "none" or "inside" or "auto") pie.Legend = Get("legend");
            if (Get("center") is { Length: > 0 } c) pie.Center = Text(c);
            if (Get("title") is { Length: > 0 } t) fig.Title = Text(t);
            ax.Pie = pie;
            _figures.Add(fig);
        }

        /// <summary>An exported Sankey diagram: the flows of "sankey flow={source}{target}{value}" and the settings of "sankey chart".</summary>
        private void OwnSankey(int start, int end, string? settings)
        {
            var sk = new ImpSankey { Own = true, Line = LineOf(start) };
            foreach (var (pos, opts) in Paths(start, end))
            {
                if (opts.FirstOrDefault(o => o.Key == "sankey flow").Value is not { } v) continue;
                var a = Args(v);
                if (a.Count < 3 || Num(a[2]) is not { } value)
                {
                    Skip(pos, "sankey flow=" + v, "A flow without source, target and value is not imported.", "Ein Fluss ohne Quelle, Ziel und Wert wird nicht importiert.");
                    continue;
                }
                sk.Flows.Add((Text(a[0]), Text(a[1]), value));
            }
            if (sk.Flows.Count == 0) return;
            var fig = Single(LineOf(start), out var ax);
            var set = Syn.Options(settings ?? "");
            string? Get(string key) => set.LastOrDefault(o => o.Key == key).Value?.Trim();
            if (Get("values") is "true" or "false") sk.Values = Get("values") == "true";
            if (Get("title") is { Length: > 0 } t) fig.Title = Text(t);
            ax.Sankey = sk;
            _figures.Add(fig);
        }

        /// <summary>
        /// A pie of pgf-pie: \pie[options]{value/name, …}. Slices run counter-clockwise from <c>rotate</c> (degrees from
        /// 3 o'clock); the numbers are percentages unless <c>after number</c> says otherwise; <c>text=legend</c> moves the names
        /// into a legend; the values fill a circle of <c>sum</c> (100 by default, <c>auto</c>: their total).
        /// </summary>
        private void PgfPie(int pos)
        {
            var k = pos + 4;
            var opts = Options(ref k);
            Ws(_s, ref k);
            var data = k < _s.Length && _s[k] == '{' ? Syn.Group(_s, ref k, '{', '}') : null;
            var stmt = _s.Substring(pos, Math.Max(4, Math.Min(k, _s.Length) - pos));
            if (data is null)
            {
                Skip(pos, stmt, "The data of \\pie cannot be read.", "Die Daten von \\pie sind nicht lesbar.");
                return;
            }
            var values = new List<double>();
            var names = new List<string>();
            foreach (var item in Syn.SplitTop(data, ','))
            {
                var t = item.Trim();
                if (t.Length == 0) continue;
                var slash = t.IndexOf('/');
                var num = Syn.Unbrace(slash < 0 ? t : t.Substring(0, slash));
                if (Num(num) is not { } v)
                {
                    Skip(pos, stmt, "The slice value “" + num + "” is not a number.", "Der Segmentwert „" + num + "“ ist keine Zahl.");
                    continue;
                }
                values.Add(v);
                names.Add(slash < 0 ? "" : Text(t.Substring(slash + 1)));
            }
            if (values.Count == 0) return;
            string? Get(string key) => opts.LastOrDefault(o => o.Key == key).Value?.Trim();
            bool Flag(string key) => opts.Any(o => o.Key == key && (o.Value is null || o.Value.Trim() == "true"));
            var pie = new ImpPie { Values = values.ToArray(), Line = LineOf(pos), CounterClock = true, StartAngle = Num(Get("rotate")) ?? 0 };
            var named = names.Any(n => n.Length > 0);
            if (named) pie.Labels = names.ToArray();
            var text = Get("text") ?? "label";
            var numbers = !Flag("hide number");
            var after = Get("after number");
            var percent = after is null || after.Contains("%");
            var showNames = named && text != "legend";
            pie.LabelMode = showNames ? (numbers ? "namepercent" : "name") : numbers ? (percent ? "percent" : "value") : "none";
            if (showNames && numbers && !percent)
                Skip(pos, "after number=" + after, "Values next to the names are shown as percentages.", "Werte neben den Namen werden als Prozent gezeigt.");
            pie.Legend = text == "legend" && named ? "right" : "none";
            var sum = Get("sum");
            var total = values.Where(v => v > 0).Sum();
            if (sum != "auto" && (Num(sum) ?? 100) > total * (1 + 1e-9) + 1e-9)
                Skip(pos, stmt, "A partial pie (a sum larger than the values) is drawn as a full pie.", "Ein Teilkreis (eine Summe größer als die Werte) wird als voller Kreis gezeichnet.");
            if (Get("explode") is not null) Skip(pos, "explode", "Exploded slices are drawn closed.", "Herausgezogene Segmente werden geschlossen gezeichnet.");
            if (Flag("polar") || Flag("square") || Flag("cloud")) Skip(pos, stmt, "This pgf-pie form is drawn as a pie.", "Diese pgf-pie-Form wird als Kreis gezeichnet.");
            var fig = Single(LineOf(pos), out var ax);
            ax.Pie = pie;
            _figures.Add(fig);
        }

        // ------------------------------------------------------------------ axes

        /// <summary>
        /// A polaraxis: angles in degrees (radians with data cs=polarrad); <c>rotate=90</c> puts 0° up, <c>x dir=reverse</c>
        /// runs the angles clockwise.
        /// </summary>
        private void PolarOptions(ImpAxes ax, List<(string Key, string? Value)> opts, int pos)
        {
            ax.Polar = true;
            _polar = true;
            _polarRad = Has(opts, "data cs", "polarrad");
            if (opts.LastOrDefault(o => o.Key == "rotate").Value is { } rv && Num(rv) is { } rot && Math.Abs(rot) > 1e-9)
            {
                if (Math.Abs(rot - 90) < 1e-9) ax.PolarTop = true;
                else Skip(pos, "rotate=" + rv, "Only a polar axis turned by 90° (0° up) is imported; the axis is not turned.", "Nur eine um 90° gedrehte Polarachse (0° oben) wird importiert; die Achse wird nicht gedreht.");
            }
            if (_reverse == 'x') { ax.PolarClockwise = true; _reverse = '\0'; }
        }

        private void Group(ImpFigure fig, List<(string Key, string? Value)> common, int from, int to)
        {
            var rows = 1; var cols = 1;
            if (common.FirstOrDefault(o => o.Key == "group style").Value is { } gs)
            {
                var size = Syn.Options(gs).FirstOrDefault(o => o.Key == "group size").Value;
                var m = size is null ? null : Regex.Match(size, @"(\d+)\s*by\s*(\d+)");
                if (m is { Success: true }) { cols = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); rows = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); }
            }
            fig.Rows = rows; fig.Cols = cols;
            var parts = Regex.Matches(_s.Substring(from, to - from), @"\\nextgroupplot").Cast<Match>().Select(m => from + m.Index).ToList();
            var heights = new List<double>();
            for (var p = 0; p < parts.Count; p++)
            {
                var k = parts[p] + "\\nextgroupplot".Length;
                var local = Options(ref k);
                var stop = p + 1 < parts.Count ? parts[p + 1] : to;
                var ax = new ImpAxes { Row = cols > 0 ? p / cols : p, Col = cols > 0 ? p % cols : 0, Line = LineOf(parts[p]) };
                fig.Axes.Add(ax);
                var opts = common.Where(o => o.Key != "group style").Concat(local).ToList();
                if (opts.Any(o => o.Key == "xmode" && o.Value == "log")) ax.XScale = "log";
                AxisOptions(fig, ax, opts, parts[p]);
                if (opts.LastOrDefault(o => o.Key == "height").Value is { } hv && TikzImport.LengthMm(hv, 25.4 / 72.27) is { } h) heights.Add(h);
                Body(fig, ax, p, k, stop);
                Finish(fig, ax);
            }
            // A title on the first panel only is the title of the group.
            if (fig.Axes.Count > 1 && fig.Axes[0].Title is { } t0 && fig.Axes.Skip(1).All(a => a.Title is null)) { fig.Title = t0; fig.Axes[0].Title = null; }
            if (heights.Count == fig.Axes.Count && heights.Count > 1)
            {
                var min = heights.Min();
                for (var i = 0; i < heights.Count; i++) fig.Axes[i].Share = Math.Round(heights[i] / min, 4);
            }
            Size(fig, common, Math.Max(1, rows));
            if (heights.Count > 0 && cols == 1) fig.HeightMm = heights.Sum() + 8 * (rows - 1) + 22;
        }

        private static void Size(ImpFigure fig, List<(string Key, string? Value)> opts, int rows)
        {
            var w = opts.LastOrDefault(o => o.Key == "width").Value is { } wv ? TikzImport.LengthMm(wv, 25.4 / 72.27) : null;
            var h = opts.LastOrDefault(o => o.Key == "height").Value is { } hv ? TikzImport.LengthMm(hv, 25.4 / 72.27) : null;
            var scaleOnly = opts.Any(o => o.Key == "scale only axis");
            if (w is { } ww) fig.WidthMm = scaleOnly ? ww + 24 : ww;
            if (h is { } hh) fig.HeightMm = scaleOnly ? hh * rows + 20 : hh * rows;
        }

        private double? Num(string? v)
        {
            if (v is null) return null;
            var t = v.Trim();
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private string Text(string tex) => TikzImport.PlainText(tex, out _, out _);

        private static List<string> Items(string v) => Syn.SplitTop(Syn.Unbrace(v), ',').Select(x => Syn.Unbrace(x.Trim())).ToList();

        private void AxisOptions(ImpFigure fig, ImpAxes ax, List<(string Key, string? Value)> opts, int pos)
        {
            foreach (var (key, v) in opts)
            {
                switch (key)
                {
                    case "xlabel" when v is not null: ax.XLabel = Text(v); break;
                    case "ylabel" when v is not null: ax.YLabel = Text(v); break;
                    case "title" when v is not null: ax.Title = Text(v); break;
                    case "xmin": ax.XMin = Num(v); break;
                    case "xmax": ax.XMax = Num(v); break;
                    case "ymin": ax.YMin = Num(v); break;
                    case "ymax": ax.YMax = Num(v); break;
                    case "xmode": ax.XScale = v == "log" ? "log" : "linear"; break;
                    case "ymode": ax.YScale = v == "log" ? "log" : "linear"; break;
                    case "xmajorgrids": ax.XGrid = v is null || v == "true"; break;
                    case "ymajorgrids": ax.YGrid = v is null || v == "true"; break;
                    case "grid": if (v is "major" or "both") { ax.XGrid = true; ax.YGrid = true; } else if (v == "none") { ax.XGrid = false; ax.YGrid = false; } break;
                    case "legend pos": ax.Legend = true; ax.LegendWhere = v?.Trim() == "outer north east" ? "right" : "inside"; break;
                    case "legend entries" when v is not null: _legendEntries = Items(v).Select(Text).ToList(); ax.Legend = true; break;
                    case "xtick" when v is not null && v.Trim() != "data" && v.Trim() != "\\empty":
                        ax.XTicks = Items(v).Select(x => Num(x) ?? double.NaN).ToList(); break;
                    case "ytick" when v is not null && v.Trim() != "data" && v.Trim() != "\\empty":
                        ax.YTicks = Items(v).Select(x => Num(x) ?? double.NaN).ToList(); break;
                    case "xticklabels" when v is not null: ax.XTickLabels = Items(v).Select(Text).ToList(); break;
                    case "yticklabels" when v is not null: ax.YTickLabels = Items(v).Select(Text).ToList(); break;
                    case "symbolic x coords" when v is not null: _symbolicX = Items(v); break;
                    case "symbolic y coords" when v is not null: _symbolicY = Items(v); break;
                    case "ybar": case "ybar stacked": case "xbar": case "xbar stacked":
                        _bars = key; break;
                    case "x dir" when v == "reverse": case "y dir" when v == "reverse":
                        _reverse = key[0]; break;
                    case "nodes near coords": ax.BarValues = true; break;
                    case "x axis line style" when v is not null && (v.Contains("Stealth") || v.Contains("->") || v.Contains("latex")): ax.XArrow = true; break;
                    case "y axis line style" when v is not null && (v.Contains("Stealth") || v.Contains("->") || v.Contains("latex")): ax.YArrow = true; break;
                    case "xtick" when v is not null && v.Trim() == "\\empty": ax.XTicks = new List<double>(); break;
                    case "ytick" when v is not null && v.Trim() == "\\empty": ax.YTicks = new List<double>(); break;
                    case "colorbar": case "view":
                        Skip(pos, key, "The option “" + key + "” is not imported.", "Die Option „" + key + "“ wird nicht importiert."); break;
                }
            }
        }

        // State of the axis being read.
        private List<string>? _legendEntries;
        private List<string>? _symbolicX, _symbolicY;
        private string? _bars;
        private char _reverse;
        private readonly List<object> _legendTargets = new();
        private readonly Dictionary<string, int> _paths = new(StringComparer.Ordinal);
        private bool _polar, _polarRad;
        /// <summary>Polar: filled plots without a line (the area under a curve that is plotted again with its line).</summary>
        private readonly List<FigSeries> _polarFills = new();
        /// <summary>Filled rectangles over part of the height: tasks of a Gantt chart if the axis holds nothing else.</summary>
        private readonly List<(int Pos, string Code, double X0, double X1, double Y0, double Y1, string? Color, string Look)> _rects = new();
        /// <summary>Box plots read in this axis (for the default draw positions 1, 2 …) and their series by look.</summary>
        private int _boxPlots;
        private readonly Dictionary<string, ImpBoxes> _boxSeries = new(StringComparer.Ordinal);
        private bool _boxRepresentative;

        /// <summary>A legend image without a plot (\addlegendimage): its legend text names a group of a Gantt chart or a box plot series.</summary>
        private sealed class LegendImage
        {
            public LegendImage(int pos) { Pos = pos; }
            public int Pos { get; }
        }

        /// <summary>The legend target of a histogram series.</summary>
        private sealed class HistTarget
        {
            public HistTarget(ImpHist h, int k) { Hist = h; K = k; }
            public ImpHist Hist { get; }
            public int K { get; }
        }

        private void Finish(ImpFigure fig, ImpAxes ax)
        {
            // Polar: a filled plot without a line that repeats a plotted curve is the area of that curve.
            foreach (var p in _polarFills)
            {
                var same = ax.Series.FirstOrDefault(s => s.X.SequenceEqual(p.X) && s.Y.SequenceEqual(p.Y));
                if (same is not null) { same.Label ??= p.Label; continue; }
                ax.Series.Add(p);
                ax.HiddenLines.Add(ax.Series.Count - 1);
            }
            // Filled rectangles over part of the height: a Gantt chart when the axis shows nothing but bars and milestones.
            if (_rects.Count > 0)
            {
                var gantt = ax.Bars.Count == 0 && ax.Pie is null && ax.Hist is null && ax.Boxes.Count == 0 && !ax.Polar
                    && ax.Series.Select((s, i) => (s, i)).All(x => x.s.Marker == "diamond" && ax.HiddenLines.Contains(x.i));
                if (gantt)
                {
                    ax.GanttRects = true;
                    ImpBars? cur = null;
                    var look = "";
                    var rows = new List<double>(); var starts = new List<double>(); var lengths = new List<double>();
                    void Flush()
                    {
                        if (cur is null) return;
                        cur.Pos = rows.ToArray(); cur.Bottom = starts.ToArray(); cur.Values = lengths.ToArray();
                        ax.Bars.Add(cur);
                        rows.Clear(); starts.Clear(); lengths.Clear();
                    }
                    foreach (var r in _rects)
                    {
                        if (cur is null || r.Look != look) { Flush(); cur = new ImpBars { Horizontal = true, Color = r.Color, Line = LineOf(r.Pos) }; look = r.Look; }
                        rows.Add((r.Y0 + r.Y1) / 2); starts.Add(Math.Min(r.X0, r.X1)); lengths.Add(Math.Abs(r.X1 - r.X0));
                    }
                    Flush();
                    if (_reverse == 'y') { ax.YInverted = true; _reverse = '\0'; }
                }
                else
                    foreach (var r in _rects)
                        _report.Skip(LineOf(r.Pos), r.Code, new I18nText("A filled rectangle over part of the height is not imported.", "Ein gefülltes Rechteck über einen Teil der Höhe wird nicht importiert."));
            }
            // Legend entries (\addlegendentry, \legend, legend entries) go to the plots in order.
            var entries = _legendEntries ?? new List<string>();
            var images = new List<LegendImage>();
            for (var i = 0; i < _legendTargets.Count; i++)
            {
                var text = i < entries.Count ? entries[i] : null;
                if (_legendTargets[i] is LegendImage img)
                {
                    images.Add(img);
                    ax.LegendImages.Add(string.IsNullOrEmpty(text) ? null : text);
                    continue;
                }
                if (string.IsNullOrEmpty(text)) continue;
                switch (_legendTargets[i])
                {
                    case FigSeries s: s.Label = new I18nText(text!); break;
                    case ImpBars b: b.Label = text; break;
                    case ImpFill f: f.Label = text; break;
                    case ImpBoxes bx: bx.Label = text; break;
                    case HistTarget h: while (h.Hist.Labels.Count <= h.K) h.Hist.Labels.Add(null); h.Hist.Labels[h.K] = text; break;
                }
            }
            // Legend images name the groups of a Gantt chart and the series of box plots; elsewhere they are reported.
            if (ax.Boxes.Count > 0)
                for (var k = 0; k < Math.Min(ax.Boxes.Count, ax.LegendImages.Count); k++) ax.Boxes[k].Label ??= ax.LegendImages[k];
            else if (!ax.GanttRects)
                foreach (var img in images)
                    Skip(img.Pos, "\\addlegendimage", "A legend entry without a plot (\\addlegendimage) is not imported.", "Ein Legendeneintrag ohne Plot (\\addlegendimage) wird nicht importiert.");
            if (entries.Count > 0) ax.Legend = true;
            if (_polar && _reverse == 'x') _reverse = '\0';
            if (_bars is not null && ax.Bars.Count > 0)
            {
                ax.BarStacked = _bars.EndsWith("stacked", StringComparison.Ordinal);
                if (_reverse != '\0' && !(_bars.StartsWith("x", StringComparison.Ordinal) && _reverse == 'y'))
                    Skip(0, "dir=reverse", "A reversed axis is not imported.", "Eine umgekehrte Achse wird nicht importiert.");
            }
            else if (_reverse != '\0') _report.Skip(ax.Line, "dir=reverse", new I18nText("A reversed axis is not imported.", "Eine umgekehrte Achse wird nicht importiert."));
            if (ax.XTicks is { } xt && ax.XTickLabels is null && ax.Bars.Count == 0 && xt.Count > 0) ax.XTicks = null;
            if (_boxRepresentative)
                _report.Skip(ax.Line, "boxplot prepared", new I18nText("Box plots from prepared values get representative samples with the same quartiles and whiskers (the samples are not in the code).",
                    "Boxplots aus vorbereiteten Werten erhalten stellvertretende Messwerte mit denselben Quartilen und Whiskern (die Messwerte stehen nicht im Code)."));
            _legendEntries = null; _symbolicX = null; _symbolicY = null; _bars = null; _reverse = '\0';
            _legendTargets.Clear(); _paths.Clear();
            _polar = _polarRad = false; _polarFills.Clear(); _rects.Clear(); _boxPlots = 0; _boxSeries.Clear(); _boxRepresentative = false;
        }

        // ------------------------------------------------------------------ axis body

        private void Body(ImpFigure fig, ImpAxes ax, int panel, int from, int to)
        {
            var i = from;
            while (i < to)
            {
                Ws(_s, ref i);
                if (i >= to) break;
                if (_s[i] != '\\')
                {
                    var next = _s.IndexOf('\\', i);
                    i = next < 0 || next > to ? to : next;
                    continue;
                }
                var j = i + 1;
                while (j < to && char.IsLetter(_s[j])) j++;
                var cmd = _s.Substring(i + 1, j - i - 1);
                if (cmd is "addlegendentry" or "legend" or "addlegendimage")
                {
                    var k = j;
                    Ws(_s, ref k);
                    if (k < to && _s[k] == '[') Syn.Group(_s, ref k, '[', ']');
                    Ws(_s, ref k);
                    var g = Syn.Group(_s, ref k, '{', '}') ?? "";
                    if (cmd == "addlegendentry") (_legendEntries ??= new List<string>()).Add(Text(g));
                    else if (cmd == "legend") _legendEntries = Items(g).Select(Text).ToList();
                    else _legendTargets.Add(new LegendImage(i)); // names a Gantt group or a box series, else reported in Finish
                    i = k;
                    if (i < to && _s[i] == ';') i++;
                    continue;
                }
                if (cmd is "pgfplotsset" or "pgfplotsextra" or "tikzset")
                {
                    var k = j;
                    Ws(_s, ref k);
                    Syn.Group(_s, ref k, '{', '}');
                    i = k;
                    continue;
                }
                var end = StatementEnd(j, to);
                var stmt = _s.Substring(i, Math.Min(end + 1, to) - i);
                try
                {
                    switch (cmd)
                    {
                        case "addplot": AddPlot(fig, ax, panel, j, end, i); break;
                        case "draw": case "path": Draw(fig, ax, panel, j, end, i); break;
                        case "node": Node(ax, j, end, i); break;
                        case "fill": Fill(ax, j, end, i); break;
                        case "coordinate": Coordinate(fig, panel, j, end); break;
                        case "addplot3": throw new Unsupported("3D plots are not imported.", "3D-Plots werden nicht importiert.");
                        case "closedcycle": break;
                        default: throw new Unsupported("The command \\" + cmd + " is not read.", "Der Befehl \\" + cmd + " wird nicht gelesen.");
                    }
                }
                catch (Unsupported ex) { _report.Skip(LineOf(i), Snip(stmt), ex.Text); }
                catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException or InvalidOperationException or InvalidCastException or NullReferenceException or KeyNotFoundException)
                {
                    _report.Skip(LineOf(i), Snip(stmt), new I18nText("Cannot be read: " + ex.Message, "Nicht lesbar: " + ex.Message));
                }
                i = end + 1;
            }
        }

        /// <summary>Colour, line, width and arrow tips of draw options.</summary>
        private (string? Color, string? Line, double? Width, ArrowHead Start, ArrowHead End) Look(List<(string Key, string? Value)> opts)
        {
            string? color = null;
            ArrowHead s = ArrowHead.None, e = ArrowHead.None;
            foreach (var (k, v) in opts)
            {
                if (v is null)
                {
                    if (Syn.Arrow(k) is { } a) { s = a.Start; e = a.End; continue; }
                    if (TikzImport.Color(k, _colors) is { } c) color = c;
                }
                else if (k is "color" or "draw" && TikzImport.Color(v, _colors) is { } c2) color = c2;
            }
            var dash = Syn.Dash(opts);
            var line = dash switch
            {
                DashStyle.Dash => "dash", DashStyle.Dot => "dot", DashStyle.DashDot => "dashdot", DashStyle.LongDash => "longdash", DashStyle.DashDotDot => "dashdotdot",
                DashStyle.Solid => "solid", _ => null,
            };
            return (color, line, Syn.Width(opts), s, e);
        }

        private static string? MarkerOf(string mark, out bool open)
        {
            open = !mark.EndsWith("*", StringComparison.Ordinal) && mark is not ("x" or "+" or "-" or "|");
            return mark.TrimEnd('*') switch
            {
                "" => "circle",
                "o" => "circle",
                "square" => "square",
                "triangle" => "triangle",
                "diamond" => "diamond",
                "x" => "cross",
                "+" => "plus",
                _ => null,
            };
        }

        private void AddPlot(ImpFigure fig, ImpAxes ax, int panel, int j, int end, int pos)
        {
            var k = j;
            var plus = false;
            if (k < end && _s[k] == '+') { plus = true; k++; }
            var opts = Options(ref k);
            Ws(_s, ref k);
            var data = _s.Substring(k, end - k).Trim();
            var forget = opts.Any(o => o.Key == "forget plot");
            var (color, line, width, _, _) = Look(opts);
            string? Get(string key) => opts.LastOrDefault(o => o.Key == key).Value;
            bool Flag(string key) => opts.Any(o => o.Key == key && (o.Value is null || o.Value == "true"));
            var drawNone = Get("draw") == "none";

            // fill between
            if (data.StartsWith("fill between", StringComparison.Ordinal))
            {
                var fk = "fill between".Length;
                var fo = Syn.Options(Syn.Group(data, ref fk, '[', ']') ?? "");
                var of = fo.FirstOrDefault(o => o.Key == "of").Value ?? throw new Unsupported("fill between needs “of=A and B”.", "fill between braucht „of=A and B“.");
                if (fo.Any(o => o.Key is "soft clip" or "split" or "every segment" or "every odd segment" or "every even segment"))
                    throw new Unsupported("fill between with soft clip or split is not imported.", "fill between mit soft clip oder split wird nicht importiert.");
                var names = Regex.Split(of, @"\s+and\s+").Select(x => x.Trim()).ToList();
                if (names.Count != 2 || !_paths.TryGetValue(names[0], out var a) || !_paths.TryGetValue(names[1], out var b))
                    throw new Unsupported("The paths of fill between are not known.", "Die Pfade von fill between sind unbekannt.");
                var fill = new ImpFill { SeriesA = a, SeriesB = b, Region = RegionLook(opts), Line = LineOf(pos), Code = Snip(_s.Substring(pos, Math.Min(end + 1, _s.Length) - pos)) };
                ax.Fills.Add(fill);
                if (!forget) _legendTargets.Add(fill);
                return;
            }

            // The points.
            List<(object X, object Y)> pts;
            if (data.StartsWith("coordinates", StringComparison.Ordinal)) pts = Coordinates(data.Substring("coordinates".Length));
            else if (data.StartsWith("table", StringComparison.Ordinal)) pts = Table(data.Substring("table".Length));
            else if (data.StartsWith("expression", StringComparison.Ordinal) || data.StartsWith("{", StringComparison.Ordinal)) pts = Expression(data, opts);
            else if (data.StartsWith("gnuplot", StringComparison.Ordinal) || data.StartsWith("shell", StringComparison.Ordinal))
                throw new Unsupported("External programs (gnuplot, shell) are never run.", "Externe Programme (gnuplot, shell) werden nie ausgeführt.");
            else if (data.StartsWith("(", StringComparison.Ordinal)) throw new Unsupported("Parametric plots are not imported.", "Parametrische Plots werden nicht importiert.");
            else throw new Unsupported("This \\addplot is not read.", "Dieses \\addplot wird nicht gelesen.");

            if (opts.Any(o => o.Key == "hist" || o.Key.StartsWith("hist/", StringComparison.Ordinal))) { Hist(ax, opts, pts, forget, pos); return; }
            if (opts.Any(o => o.Key is "boxplot" or "boxplot prepared")) { BoxPlot(ax, opts, pts, forget, pos); return; }

            var barMode = _bars ?? (opts.FirstOrDefault(o => o.Key is "ybar" or "xbar" or "ybar stacked" or "xbar stacked").Key);
            if (barMode is not null && !(Get("draw") == "none" && Get("name path") is not null))
            {
                var horizontal = barMode.StartsWith("x", StringComparison.Ordinal);
                var bars = new ImpBars { Horizontal = horizontal, Line = LineOf(pos) };
                // ybar: (position, value); xbar: (value, position).
                var positions = pts.Select(p => horizontal ? p.Y : p.X).ToList();
                var values = pts.Select(p => (horizontal ? p.X : p.Y) is double d ? d : throw new Unsupported("A bar value is not a number.", "Ein Balkenwert ist keine Zahl.")).ToList();
                if (positions.Any(p => p is string))
                {
                    var names = horizontal ? _symbolicY : _symbolicX;
                    bars.Cats = positions.Select(p => p as string ?? ((double)p).ToString("R", CultureInfo.InvariantCulture)).ToArray();
                    if (names is not null && names.Count > 0)
                    {
                        // The order of the symbolic coordinates: bars of every name, missing ones empty.
                        var map = new Dictionary<string, double>(StringComparer.Ordinal);
                        for (var q = 0; q < bars.Cats.Length; q++) map[bars.Cats[q]] = values[q];
                        bars.Cats = names.Select(Text).ToArray();
                        values = names.Select(nm => map.TryGetValue(nm, out var v) ? v : double.NaN).ToList();
                    }
                }
                else bars.Pos = positions.Select(p => (double)p).ToArray();
                bars.Values = values.ToArray();
                if (Flag("nodes near coords")) ax.BarValues = true;
                ax.Bars.Add(bars);
                if (!forget) _legendTargets.Add(bars);
                return;
            }
            if (pts.Any(p => p.X is string || p.Y is string)) throw new Unsupported("Text coordinates are only read for bar plots.", "Text-Koordinaten werden nur bei Balkenplots gelesen.");
            var se = new FigSeries { X = pts.Select(p => (double)p.X).ToList(), Y = pts.Select(p => (double)p.Y).ToList(), Color = color, Line = line, Width = width };
            var hidden = drawNone || Flag("only marks");
            var mark = Get("mark");
            var noMarks = Flag("no marks") || Flag("no markers");
            if (mark is not null && mark != "none" && !noMarks)
            {
                se.Marker = MarkerOf(mark.Trim(), out var open);
                if (se.Marker is null) { se.Marker = "circle"; Skip(pos, "mark=" + mark, "The mark “" + mark + "” is drawn as a circle.", "Die Markierung „" + mark + "“ wird als Kreis gezeichnet."); }
                se.MarkerOpen = open;
                if (Get("mark options") is { } mo && Regex.IsMatch(mo, @"fill\s*=\s*white")) se.MarkerOpen = true;
                else if (Get("mark options") is { } mo2 && Regex.IsMatch(mo2, @"fill\s*=\s*(?!white)\w")) se.MarkerOpen = false;
            }
            else if ((opts.Count == 0 || plus) && !noMarks && !drawNone)
            {
                // pgfplots' default cycle list draws marks.
                se.Marker = "circle";
            }
            if (Flag("only marks") && se.Marker is null) se.Marker = "circle";
            if (Flag("const plot") || Flag("const plot mark left") || Flag("jump mark left")) se.Step = true;
            if (Flag("const plot mark right") || Flag("jump mark right")) Skip(pos, "const plot mark right", "Steps held from the right are imported as steps from the left.", "Stufen von rechts werden als Stufen von links importiert.");
            if (Flag("smooth")) se.Smooth = true;
            if (se.Color is null && Get("fill") is { } fc && TikzImport.Color(fc, _colors) is { } fcol && !drawNone) se.Color = fcol;
            // Marks only: their colour (milestones of a Gantt chart join the bars of that colour).
            if (se.Color is null && Get("mark options") is { } mopt)
            {
                var mo = Syn.Options(mopt);
                if ((mo.LastOrDefault(o => o.Key is "draw" or "color").Value ?? mo.LastOrDefault(o => o.Key == "fill").Value) is { } mc && TikzImport.Color(mc, _colors) is { } mcol) se.Color = mcol;
            }
            if (_polar)
            {
                // Angles in degrees (pgfplots' default) as radians; a filled plot without a line is the area of a curve.
                if (!_polarRad && !Has(opts, "data cs", "polarrad")) se.X = se.X.Select(v => v * Math.PI / 180).ToList();
                var filled = Get("fill") is { } pf && pf != "none" || Get("pattern") is not null;
                if (filled) ax.PolarFill = true;
                if (filled && drawNone) { _polarFills.Add(se); if (!forget) _legendTargets.Add(se); return; }
                ax.Series.Add(se);
                if (hidden) ax.HiddenLines.Add(ax.Series.Count - 1);
                if (!forget) _legendTargets.Add(se);
                return;
            }
            if (Get("fill") is not null && !drawNone && Get("name path") is null)
                Skip(pos, "fill", "The filling of a plot is not imported (use fill between).", "Die Füllung eines Plots wird nicht importiert (bitte fill between verwenden).");
            if (opts.Any(o => o.Key == "error bars/.cd" || o.Key.StartsWith("error bars", StringComparison.Ordinal)))
                Skip(pos, "error bars", "Error bars are not imported; the curve is.", "Fehlerbalken werden nicht importiert; die Kurve schon.");
            ax.Series.Add(se);
            // The group of filled rectangles drawn before (milestones of a Gantt chart follow the bars of their group).
            ax.AfterBars[se] = _rects.Select(r => r.Look).Where((l, i) => i == 0 || l != _rects[i - 1].Look).Count() - 1;
            if (hidden) ax.HiddenLines.Add(ax.Series.Count - 1);
            if (Get("name path") is { } np) _paths[np.Trim()] = ax.Series.Count - 1;
            if (drawNone && Get("name path") is not null) { if (!forget) _legendTargets.Add(se); return; }
            if (!forget) _legendTargets.Add(se);
        }

        /// <summary>Settings of a statistics plot: the group value ("hist={bins=6}") and the single keys ("hist/bins=6").</summary>
        private static List<(string Key, string? Value)> Sub(List<(string Key, string? Value)> opts, string name)
        {
            var list = new List<(string Key, string? Value)>();
            foreach (var (k, v) in opts)
            {
                if (k == name && v is not null) list.AddRange(Syn.Options(v));
                else if (k.StartsWith(name + "/", StringComparison.Ordinal)) list.Add((k.Substring(name.Length + 1), v));
            }
            return list;
        }

        private static double? Y(object v) => v is double d && Fmt.Finite(d) ? d : null;

        /// <summary>\addplot[hist={bins=…, data min=…, data max=…}] table/coordinates: a histogram of the y values (data=x: of the x values).</summary>
        private void Hist(ImpAxes ax, List<(string Key, string? Value)> opts, List<(object X, object Y)> pts, bool forget, int pos)
        {
            var h = ax.Hist ??= new ImpHist { Line = LineOf(pos) };
            var ho = Sub(opts, "hist");
            string? H(string key) => ho.LastOrDefault(o => o.Key == key).Value?.Trim();
            var useX = H("data") == "x";
            var samples = pts.Select(p => Y(useX ? p.X : p.Y)).Where(v => v is not null).Select(v => v!.Value).ToArray();
            var bins = Num(H("bins")) is { } b && b >= 1 ? (int)Math.Round(b) : 10;
            if (h.Bins is { } first && first != bins)
                Skip(pos, "bins=" + Fmt.I(bins), "Histograms with other bins in the same axis use the bins of the first one.", "Histogramme mit anderen Klassen in derselben Achse verwenden die Klassen des ersten.");
            h.Bins ??= bins;
            double? lo = Num(H("data min")), hi = Num(H("data max"));
            if (h.Range is null && (lo is not null || hi is not null) && samples.Length > 0)
                h.Range = (lo ?? samples.Min(), hi ?? samples.Max());
            if (ho.Any(o => o.Key is "density" or "cumulative" or "intervals" || o.Key == "handler"))
                Skip(pos, "hist", "Densities, cumulative counts and own handlers of a histogram are not imported; the counts are.", "Dichten, kumulierte Häufigkeiten und eigene Darstellungen eines Histogramms werden nicht importiert; die Häufigkeiten schon.");
            h.Data.Add(samples);
            h.Labels.Add(null);
            if (!forget) _legendTargets.Add(new HistTarget(h, h.Data.Count - 1));
        }

        /// <summary>
        /// \addplot[boxplot] with raw samples, or \addplot[boxplot prepared={…}] with the five values and the outliers as
        /// points. Box plots of one look form a series. The exported form keeps its raw samples in the data key "box samples";
        /// prepared values alone get representative samples with the same quartiles and whiskers.
        /// </summary>
        private void BoxPlot(ImpAxes ax, List<(string Key, string? Value)> opts, List<(object X, object Y)> pts, bool forget, int pos)
        {
            var prepared = opts.Any(o => o.Key == "boxplot prepared");
            var bo = Sub(opts, prepared ? "boxplot prepared" : "boxplot");
            bo.AddRange(Sub(opts, "boxplot").Where(o => !bo.Contains(o)));
            double? B(string key) => Num(bo.LastOrDefault(o => o.Key == key).Value);
            _boxPlots++;
            var at = B("draw position") ?? _boxPlots;
            var whisker = B("whisker range");
            var points = pts.Select(p => Y(p.Y)).Where(v => v is not null).Select(v => v!.Value).ToList();
            List<double> samples;
            var minMax = false;
            if (opts.LastOrDefault(o => o.Key == "box samples").Value is { } raw)
            {
                samples = Syn.SplitTop(raw, ',').Select(x => Num(x)).Where(v => v is not null && Fmt.Finite(v.Value)).Select(v => v!.Value).ToList();
                // Whiskers at the minimum and maximum although the rule would leave outliers: no outliers (min/max whiskers).
                if (prepared && whisker is null && Stats.Box(samples) is { } st && st.Outliers.Count > 0
                    && B("lower whisker") is { } lw && B("upper whisker") is { } uw && Near(lw, st.Min) && Near(uw, st.Max))
                    minMax = true;
            }
            else if (prepared)
            {
                if (B("median") is not { } med) throw new Unsupported("boxplot prepared needs a median.", "boxplot prepared braucht einen Median.");
                var q1 = B("lower quartile") ?? med; var q3 = B("upper quartile") ?? med;
                var low = B("lower whisker") ?? q1; var high = B("upper whisker") ?? q3;
                samples = Representative(low, q1, med, q3, high, points);
                _boxRepresentative = true;
                // Whiskers beyond the rule of 1.5 × IQR without outliers: whiskers at the minimum and maximum.
                var w = whisker ?? 1.5;
                if (points.Count == 0 && (q1 - low > w * (q3 - q1) + 1e-12 || high - q3 > w * (q3 - q1) + 1e-12)) minMax = true;
            }
            else samples = points;
            if (samples.Count == 0) throw new Unsupported("A box plot without samples is not imported.", "Ein Boxplot ohne Messwerte wird nicht importiert.");
            // The series: box plots of the same look.
            string? Get(string key) => opts.LastOrDefault(o => o.Key == key).Value?.Trim();
            var look = string.Join("|", new[] { Get("fill"), Get("draw"), Get("pattern"), Get("pattern color"), Get("color") }.Select(x => x ?? ""))
                + "|" + string.Join(",", opts.Where(o => o.Value is null && TikzImport.Color(o.Key, _colors) is not null).Select(o => o.Key));
            if (!_boxSeries.TryGetValue(look, out var series))
            {
                series = new ImpBoxes { Line = LineOf(pos), Whis = whisker ?? 1.5 };
                _boxSeries[look] = series;
                ax.Boxes.Add(series);
                if (!forget) _legendTargets.Add(series);
            }
            series.MinMax |= minMax;
            series.Data.Add(samples.ToArray());
            series.Positions = (series.Positions ?? Array.Empty<double>()).Concat(new[] { at }).ToArray();
        }

        /// <summary>
        /// Samples whose linear-interpolation quartiles are q1, median and q3 and whose whiskers are low and high, with the
        /// outliers: n = 4j + 1 sorted values put q1, the median and q3 at the indices j, 2j and 3j exactly.
        /// </summary>
        internal static List<double> Representative(double low, double q1, double med, double q3, double high, IReadOnlyList<double> outliers)
        {
            var below = outliers.Where(v => v < low).OrderBy(v => v).ToList();
            var above = outliers.Where(v => v > high).OrderBy(v => v).ToList();
            var j = Math.Max(below.Count, above.Count) + 1;
            var n = 4 * j + 1;
            var s = new double[n];
            for (var i = 0; i < n; i++)
            {
                if (i < below.Count) s[i] = below[i];
                else if (i == below.Count) s[i] = Math.Min(low, q1);
                else if (i <= j) s[i] = q1;
                else if (i <= 2 * j) s[i] = i == 2 * j ? med : q1;
                else if (i < 3 * j) s[i] = med;
                else if (i < n - 1 - above.Count) s[i] = q3;
                else if (i == n - 1 - above.Count) s[i] = Math.Max(high, q3);
                else s[i] = above[i - (n - above.Count)];
            }
            return s.ToList();
        }

        private FigRegion RegionLook(List<(string Key, string? Value)> opts)
        {
            var r = new FigRegion { Fill = "solid" };
            foreach (var (k, v) in opts)
            {
                switch (k)
                {
                    case "fill" when v is not null && v != "none": r.Color = TikzImport.Color(v, _colors) ?? r.Color; break;
                    case "fill opacity": case "opacity": r.Opacity = Num(v); break;
                    case "pattern color": r.Color = TikzImport.Color(v, _colors) ?? r.Color; break;
                    case "pattern" when v is not null:
                        {
                            var t = v.Trim();
                            var ang = Regex.Match(t, @"angle\s*=\s*([-\d.]+)");
                            var dist = Regex.Match(t, @"distance\s*=\s*([\d.]+\s*(mm|pt|cm)?)");
                            r.Fill = t.StartsWith("Hatch", StringComparison.Ordinal) || t is "crosshatch" or "grid" ? "cross" : t.StartsWith("Dots", StringComparison.Ordinal) || t.Contains("dots") ? "dots" : "hatch";
                            if (ang.Success) r.Angle = double.Parse(ang.Groups[1].Value, CultureInfo.InvariantCulture);
                            else r.Angle = t switch { "north east lines" => 45, "north west lines" => 135, "horizontal lines" => 0, "vertical lines" => 90, _ => (double?)null };
                            if (dist.Success) r.Spacing = TikzImport.LengthMm(dist.Groups[1].Value.Trim(), 1);
                            break;
                        }
                    default:
                        if (v is null && TikzImport.Color(k, _colors) is { } c) r.Color = c;
                        break;
                }
            }
            return r;
        }

        /// <summary>coordinates {(x,y) (x,y) …}: numbers, nan/inf, or symbolic names; "[meta]" after a point is ignored.</summary>
        private List<(object X, object Y)> Coordinates(string rest)
        {
            var k = 0;
            Ws(rest, ref k);
            var body = Syn.Group(rest, ref k, '{', '}') ?? throw new Unsupported("The coordinates cannot be read.", "Die Koordinaten sind nicht lesbar.");
            var pts = new List<(object, object)>();
            var i = 0;
            while (i < body.Length)
            {
                Ws(body, ref i);
                if (i >= body.Length) break;
                if (body[i] == '[') { Syn.Group(body, ref i, '[', ']'); continue; }
                if (body[i] == '+' && i + 1 < body.Length && body[i + 1] == '-') throw new Unsupported("Error bars (+-) are not imported.", "Fehlerbalken (+-) werden nicht importiert.");
                if (body[i] != '(') throw new Unsupported("The coordinates cannot be read near “" + body.Substring(i, Math.Min(10, body.Length - i)) + "”.", "Die Koordinaten sind bei „" + body.Substring(i, Math.Min(10, body.Length - i)) + "“ nicht lesbar.");
                var g = Syn.Group(body, ref i, '(', ')') ?? throw new Unsupported("The coordinates cannot be read.", "Die Koordinaten sind nicht lesbar.");
                var parts = Syn.SplitTop(g, ',');
                if (parts.Count < 2) throw new Unsupported("A coordinate needs x and y.", "Eine Koordinate braucht x und y.");
                if (parts.Count > 2) throw new Unsupported("3D coordinates are not imported.", "3D-Koordinaten werden nicht importiert.");
                object x = Value(parts[0]) is { } xv ? xv : (object)Syn.Unbrace(parts[0].Trim());
                object y = Value(parts[1]) is { } yv ? yv : (object)Syn.Unbrace(parts[1].Trim());
                pts.Add((x, y));
                if (pts.Count > 200_000) throw new Unsupported("Too many coordinates.", "Zu viele Koordinaten.");
            }
            return pts;
        }

        private static double? Value(string s)
        {
            var t = Syn.Unbrace(s.Trim());
            switch (t.ToLowerInvariant())
            {
                case "nan": case "-nan": return double.NaN;
                case "inf": case "+inf": return double.PositiveInfinity;
                case "-inf": return double.NegativeInfinity;
            }
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        /// <summary>table [x=…, y=…] {inline data}: columns by name or index; a file name is reported.</summary>
        private List<(object X, object Y)> Table(string rest)
        {
            var k = 0;
            Ws(rest, ref k);
            var opts = new List<(string Key, string? Value)>();
            if (k < rest.Length && rest[k] == '[') opts = Syn.Options(Syn.Group(rest, ref k, '[', ']') ?? "");
            Ws(rest, ref k);
            var body = Syn.Group(rest, ref k, '{', '}') ?? throw new Unsupported("The table cannot be read.", "Die Tabelle ist nicht lesbar.");
            string? Get(string key) => opts.LastOrDefault(o => o.Key == key).Value;
            var rowSep = Get("row sep")?.Trim();
            var lines = (rowSep is "\\\\" or "crcr" ? body.Replace("\\\\", "\n") : body).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count <= 1 && Regex.IsMatch(body.Trim(), @"^[\w\-./\\:]+\.(dat|csv|txt|tsv|tab)$|^[\w\-./]+$") && !body.Trim().Any(char.IsWhiteSpace) && Value(body) is null)
                throw new Unsupported("Data from the file “" + body.Trim() + "” are not read; paste the data inline.", "Daten aus der Datei „" + body.Trim() + "“ werden nicht gelesen; bitte die Daten direkt einfügen.");
            var colSep = Get("col sep")?.Trim();
            char[]? sep = colSep switch { "comma" => new[] { ',' }, "semicolon" => new[] { ';' }, "tab" => new[] { '\t' }, "&" or "ampersand" => new[] { '&' }, _ => null };
            List<string> Fields(string l) => (sep is null ? l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries) : l.Split(sep)).Select(f => f.Trim()).ToList();
            var header = Fields(lines[0]);
            var hasHeader = header.Any(f => Value(f) is null);
            var rows = lines.Skip(hasHeader ? 1 : 0).Select(Fields).ToList();
            int Col(string? name, string? index, int def)
            {
                if (index is not null && int.TryParse(index.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ix)) return ix;
                if (name is null) return def;
                var n = name.Trim();
                if (n.StartsWith("\\thisrow", StringComparison.Ordinal) || n.Contains("\\")) throw new Unsupported("Computed table columns are not read.", "Berechnete Tabellenspalten werden nicht gelesen.");
                var i = hasHeader ? header.IndexOf(n) : -1;
                if (i < 0) throw new Unsupported("The column “" + n + "” is not in the table.", "Die Spalte „" + n + "“ steht nicht in der Tabelle.");
                return i;
            }
            var xc = Col(Get("x"), Get("x index"), 0);
            var yc = Col(Get("y"), Get("y index"), 1);
            var pts = new List<(object, object)>();
            foreach (var r in rows)
            {
                if (r.Count <= Math.Max(xc, yc)) continue;
                object x = Value(r[xc]) is { } xv ? xv : r[xc];
                object y = Value(r[yc]) is { } yv ? yv : r[yc];
                pts.Add((x, y));
            }
            return pts;
        }

        /// <summary>An expression plot sampled over its domain (pgfplots default -5:5, 25 samples; trigonometry in degrees).</summary>
        private List<(object X, object Y)> Expression(string data, List<(string Key, string? Value)> opts)
        {
            var k = data.StartsWith("expression", StringComparison.Ordinal) ? "expression".Length : 0;
            Ws(data, ref k);
            var expr = Syn.Group(data, ref k, '{', '}') ?? throw new Unsupported("The expression cannot be read.", "Der Ausdruck ist nicht lesbar.");
            string? Get(string key) => opts.LastOrDefault(o => o.Key == key).Value;
            double lo = -5, hi = 5;
            if (Get("domain") is { } dom)
            {
                var m = Regex.Match(dom, @"^\s*([-+\d.eE]+)\s*:\s*([-+\d.eE]+)\s*$");
                if (!m.Success) throw new Unsupported("The domain “" + dom + "” cannot be read.", "Der Bereich „" + dom + "“ ist nicht lesbar.");
                lo = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); hi = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            }
            var samples = 25;
            if (Get("samples") is { } sv)
            {
                if (!int.TryParse(sv.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out samples) || samples < 2 || samples > 10_000)
                    throw new Unsupported("The number of samples cannot be read.", "Die Anzahl der Stützstellen ist nicht lesbar.");
            }
            if (opts.Any(o => o.Key == "samples at")) throw new Unsupported("samples at is not read.", "samples at wird nicht gelesen.");
            var rad = opts.Any(o => o.Key is "trig format plots" or "trig format" && o.Value?.Trim() == "rad");
            var converted = Convert(expr, rad);
            if (!FunctionExpression.TryParse(converted, out var fe, out var error) || fe is null)
                throw new Unsupported("The expression “" + expr.Trim() + "” cannot be evaluated: " + error?.En, "Der Ausdruck „" + expr.Trim() + "“ ist nicht auswertbar: " + error?.Get(Lang.De));
            var pts = new List<(object, object)>();
            for (var i = 0; i < samples; i++)
            {
                var x = i == samples - 1 ? hi : lo + (hi - lo) * i / (samples - 1);
                pts.Add((x, fe.Evaluate(x)));
            }
            return pts;
        }

        private const string DegToRad = "0.017453292519943295";
        private const string RadToDeg = "57.29577951308232";

        /// <summary>A pgfplots expression as a <see cref="FunctionExpression"/> formula: \x → x, deg()/rad(), trigonometry in degrees.</summary>
        internal static string Convert(string expr, bool rad)
        {
            var e = expr.Trim().Replace("\\x", "x").Replace("\\pi", "pi");
            if (e.Contains('\\')) throw new Unsupported("Macros in expressions are not evaluated.", "Makros in Ausdrücken werden nicht ausgewertet.");
            if (e.Contains(',')) throw new Unsupported("Functions with several arguments are not evaluated.", "Funktionen mit mehreren Argumenten werden nicht ausgewertet.");
            if (Regex.IsMatch(e, @"\by\b")) throw new Unsupported("Only expressions in x are evaluated.", "Nur Ausdrücke in x werden ausgewertet.");
            var sb = new StringBuilder();
            var i = 0;
            while (i < e.Length)
            {
                if (char.IsLetter(e[i]))
                {
                    var s = i;
                    while (i < e.Length && (char.IsLetterOrDigit(e[i]) || e[i] == '_')) i++;
                    var name = e.Substring(s, i - s);
                    var j = i;
                    while (j < e.Length && e[j] == ' ') j++;
                    if (j < e.Length && e[j] == '(')
                    {
                        var k = j;
                        var inner = Syn.Group(e, ref k, '(', ')') ?? throw new Unsupported("Unbalanced parentheses.", "Unausgeglichene Klammern.");
                        var arg = Convert(inner, rad);
                        i = k;
                        switch (name)
                        {
                            case "deg": sb.Append("((").Append(arg).Append(")*").Append(RadToDeg).Append(')'); continue;
                            case "rad": sb.Append("((").Append(arg).Append(")*").Append(DegToRad).Append(')'); continue;
                            case "sin": case "cos": case "tan":
                                sb.Append(name).Append(rad ? "(" + arg + ")" : "((" + arg + ")*" + DegToRad + ")"); continue;
                            case "asin": case "acos": case "atan":
                                sb.Append(rad ? name + "(" + arg + ")" : "(" + name + "(" + arg + ")*" + RadToDeg + ")"); continue;
                            case "log10": sb.Append("lg(").Append(arg).Append(')'); continue;
                            case "ln": case "exp": case "sqrt": case "abs": case "log2": case "sinh": case "cosh": case "tanh": case "floor": case "ceil": case "sign": case "round":
                                sb.Append(name).Append('(').Append(arg).Append(')'); continue;
                            default:
                                throw new Unsupported("The function “" + name + "” is not evaluated.", "Die Funktion „" + name + "“ wird nicht ausgewertet.");
                        }
                    }
                    if (name is not ("x" or "pi" or "e")) throw new Unsupported("The name “" + name + "” is not evaluated.", "Der Name „" + name + "“ wird nicht ausgewertet.");
                    sb.Append(name);
                    continue;
                }
                sb.Append(e[i]);
                i++;
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ drawings in axis coordinates

        private (double X, double Y)? AxisPoint(string c)
        {
            var t = c.Trim();
            var m = Regex.Match(t, @"^axis (?:direction )?cs:\s*(.+)$");
            if (!m.Success) return null;
            var parts = Syn.SplitTop(m.Groups[1].Value, ',');
            if (parts.Count != 2) return null;
            var x = Value(parts[0]); var y = Value(parts[1]);
            if (x is null || y is null) throw new Unsupported("Only numeric axis coordinates are read.", "Nur numerische Achskoordinaten werden gelesen.");
            return (x.Value, y.Value);
        }

        private List<string> PathPoints(string body, out List<string> ops)
        {
            var pts = new List<string>();
            ops = new List<string>();
            var i = 0;
            while (i < body.Length)
            {
                Ws(body, ref i);
                if (i >= body.Length) break;
                if (body[i] == '(') { pts.Add(Syn.Group(body, ref i, '(', ')') ?? ""); continue; }
                if (body.Substring(i).StartsWith("--", StringComparison.Ordinal)) { ops.Add("--"); i += 2; continue; }
                var w = Regex.Match(body.Substring(i), @"^[A-Za-z]+");
                if (w.Success) { ops.Add(w.Value); i += w.Length; continue; }
                ops.Add(body[i].ToString());
                i++;
            }
            return pts;
        }

        private void Coordinate(ImpFigure fig, int panel, int j, int end)
        {
            var m = Regex.Match(_s.Substring(j, end - j), @"^\s*\(([^()]+)\)\s*at\s*\((.+)\)\s*$", RegexOptions.Singleline);
            if (!m.Success || AxisPoint(m.Groups[2].Value) is not { } p) throw new Unsupported("The coordinate cannot be read.", "Die Koordinate ist nicht lesbar.");
            _named[m.Groups[1].Value.Trim()] = (fig, panel, p.X, p.Y);
        }

        private void Draw(ImpFigure fig, ImpAxes ax, int panel, int j, int end, int pos)
        {
            var k = j;
            var opts = Options(ref k);
            var body = _s.Substring(k, end - k);
            var pts = PathPoints(body, out var ops);
            if (pts.Count != 2 || ops.Any(o => o != "--")) throw new Unsupported("Only straight lines between two points are read in an axis.", "In einer Achse werden nur gerade Linien zwischen zwei Punkten gelesen.");
            var (color, line, width, s, e) = Look(opts);
            // A leader from a named numeral node to its target.
            if (_nodes.TryGetValue(pts[0].Trim(), out var node) && AxisPoint(pts[1]) is { } target)
            {
                node.Note.Note.Kind = "numeral";
                node.Note.Note.P1 = new FigPoint { X = target.X, Y = target.Y };
                node.Note.Note.Dx = node.Dx;
                node.Note.Note.Dy = -node.Dy;
                node.Note.XFrac = node.Note.YFrac = false;
                return;
            }
            if (AxisPoint(pts[0]) is not { } a || AxisPoint(pts[1]) is not { } b) throw new Unsupported("Only lines in axis coordinates (axis cs:) are read.", "Nur Linien in Achskoordinaten (axis cs:) werden gelesen.");
            var heads = s != ArrowHead.None || e != ArrowHead.None;
            if (!heads && a.Y == b.Y && (ax.XMin is null || Near(Math.Min(a.X, b.X), ax.XMin.Value)) && (ax.XMax is null || Near(Math.Max(a.X, b.X), ax.XMax.Value)))
            {
                ax.Refs.Add(new FigRef { Kind = "h", Y = a.Y, Color = color, Line = line ?? "solid", Width = width });
                return;
            }
            if (!heads && a.X == b.X && (ax.YMin is null || Near(Math.Min(a.Y, b.Y), ax.YMin.Value)) && (ax.YMax is null || Near(Math.Max(a.Y, b.Y), ax.YMax.Value)))
            {
                ax.Refs.Add(new FigRef { Kind = "v", X = a.X, Color = color, Line = line ?? "solid", Width = width });
                return;
            }
            var reverse = s != ArrowHead.None && e == ArrowHead.None;
            var note = new FigNote
            {
                Kind = s != ArrowHead.None && e != ArrowHead.None ? "dim" : heads ? "arrow" : "line",
                P1 = new FigPoint { X = reverse ? b.X : a.X, Y = reverse ? b.Y : a.Y },
                P2 = new FigPoint { X = reverse ? a.X : b.X, Y = reverse ? a.Y : b.Y },
                Line = line is "solid" ? null : line,
            };
            ax.Notes.Add(new ImpNote { Note = note, Line = LineOf(pos), Code = Snip(_s.Substring(pos, Math.Min(end + 1, _s.Length) - pos)) });
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

        private void Node(ImpAxes ax, int j, int end, int pos)
        {
            var s = _s.Substring(j, end - j);
            var i = 0;
            var opts = new List<(string Key, string? Value)>();
            string? name = null;
            string? at = null;
            while (true)
            {
                Ws(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '[') { opts.AddRange(Syn.Options(Syn.Group(s, ref i, '[', ']') ?? "")); continue; }
                if (s[i] == '(') { name = Syn.Group(s, ref i, '(', ')')?.Trim(); continue; }
                if (s.Substring(i).StartsWith("at", StringComparison.Ordinal)) { i += 2; Ws(s, ref i); at = Syn.Group(s, ref i, '(', ')'); continue; }
                break;
            }
            var text = Syn.Group(s, ref i, '{', '}') ?? throw new Unsupported("The node text is missing.", "Der Knotentext fehlt.");
            if (at is null || AxisPoint(at) is not { } p) throw new Unsupported("Only nodes at axis coordinates (axis cs:) are read.", "Nur Knoten an Achskoordinaten (axis cs:) werden gelesen.");
            var plain = TikzImport.PlainText(text, out var bold, out var italic);
            if (plain.Length == 0) return;
            string? Get(string key) => opts.LastOrDefault(o => o.Key == key).Value;
            var anchor = Get("anchor") ?? "";
            var note = new FigNote
            {
                Kind = "text", Text = new I18nText(plain), P1 = new FigPoint { X = p.X, Y = p.Y }, Italic = italic,
                Anchor = anchor.Contains("west") ? "start" : anchor.Contains("east") ? "end" : null,
                Box = Get("fill") == "white",
            };
            var imp = new ImpNote { Note = note, Line = LineOf(pos), Code = Snip(_s.Substring(pos, Math.Min(end + 1, _s.Length) - pos)) };
            ax.Notes.Add(imp);
            if (name is not null)
            {
                double Em(string? v) => v is null ? 0 : v.Trim().EndsWith("em", StringComparison.Ordinal) && double.TryParse(v.Trim().TrimEnd('m', 'e'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (TikzImport.LengthMm(v, 1) ?? 0) / 3.5;
                _nodes[name] = (ax, imp, Em(Get("xshift")), Em(Get("yshift")));
            }
        }

        private void Fill(ImpAxes ax, int j, int end, int pos)
        {
            var k = j;
            var opts = Options(ref k);
            var body = _s.Substring(k, end - k);
            var pts = PathPoints(body, out var ops);
            if (pts.Count == 2 && ops.Count == 1 && ops[0] == "rectangle" && AxisPoint(pts[0]) is { } a && AxisPoint(pts[1]) is { } b)
            {
                var r = RegionLook(opts);
                r.Kind = "span"; r.From = Math.Min(a.X, b.X); r.To = Math.Max(a.X, b.X);
                var lo = Math.Min(a.Y, b.Y); var hi = Math.Max(a.Y, b.Y);
                if (ax.YMin is { } ymin && ax.YMax is { } ymax && (!Near(lo, ymin) || !Near(hi, ymax)))
                {
                    // A task bar of a Gantt chart, or reported when the axis turns out to hold more (Finish).
                    _rects.Add((pos, Snip(_s.Substring(pos, Math.Min(end + 1, _s.Length) - pos)), a.X, b.X, lo, hi, r.Color,
                        string.Join("|", new[] { "fill", "draw", "pattern", "pattern color" }.Select(key => opts.LastOrDefault(o => o.Key == key).Value ?? ""))));
                    return;
                }
                ax.Spans.Add(r);
                return;
            }
            throw new Unsupported("Filled polygons in an axis are not imported (use fill between).", "Gefüllte Polygone in einer Achse werden nicht importiert (bitte fill between verwenden).");
        }
    }
}
