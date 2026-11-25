using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InventaSketch.Core
{
    /// <summary>A part of imported code that was not imported: its (first) line, the code and the reason.</summary>
    public sealed record CodeImportSkip(int Line, string Code, I18nText Reason);

    /// <summary>
    /// Report of a code import (matplotlib, pgfplots, TikZ; roadmap 4b): what became elements and what was not imported.
    /// Code is read, never executed; anything outside the readable subset is listed here instead of being guessed.
    /// </summary>
    public sealed class CodeImportReport
    {
        public const int MaxSkipped = 300;

        /// <summary>One line per created element group ("Chart 1: 3 curves, 1 value line").</summary>
        public List<I18nText> Imported { get; } = new();

        /// <summary>Statements or parts that were not imported, in source order.</summary>
        public List<CodeImportSkip> Skipped { get; } = new();

        /// <summary>Adds a skipped part (the same line and reason only once; at most <see cref="MaxSkipped"/> entries).</summary>
        public void Skip(int line, string code, I18nText reason)
        {
            if (Skipped.Count >= MaxSkipped) return;
            if (Skipped.Any(s => s.Line == line && s.Reason.En == reason.En)) return;
            Skipped.Add(new CodeImportSkip(line, code, reason));
        }

        /// <summary>Appends the entries of <paramref name="other"/>.</summary>
        public void Add(CodeImportReport other)
        {
            Imported.AddRange(other.Imported);
            foreach (var s in other.Skipped) Skip(s.Line, s.Code, s.Reason);
            Skipped.Sort((a, b) => a.Line.CompareTo(b.Line));
        }
    }

    /// <summary>Detection of the code format of an import (matplotlib script, pgfplots axes, TikZ drawing).</summary>
    public static class CodeImportFormat
    {
        public const string Matplotlib = "matplotlib";
        public const string Pgfplots = "pgfplots";
        public const string Tikz = "tikz";

        private static readonly Regex Axis = new(@"\\begin\s*\{\s*(axis|semilogxaxis|semilogyaxis|loglogaxis|groupplot)\s*\}|\\addplot", RegexOptions.CultureInvariant);
        private static readonly Regex Tikz2 = new(@"\\begin\s*\{\s*tikzpicture\s*\}|\\(draw|fill|filldraw|node|path)\b", RegexOptions.CultureInvariant);
        private static readonly Regex Python = new(@"(^|\n)\s*(import\s+matplotlib|from\s+matplotlib|import\s+numpy|from\s+numpy)|\bplt\s*\.|\bpyplot\b|\.\s*plot\s*\(|\bsubplots\s*\(|\bax\s*\.\s*\w+\s*\(", RegexOptions.CultureInvariant);

        /// <summary>"pgfplots", "tikz" or "matplotlib" by content; null if the text is none of them.</summary>
        public static string? Detect(string? text)
        {
            var t = text ?? "";
            if (Axis.IsMatch(t)) return Pgfplots;
            if (Tikz2.IsMatch(t)) return Tikz;
            if (Python.IsMatch(t)) return Matplotlib;
            return null;
        }

        /// <summary>The format of a hint ("auto", "matplotlib", "python", "pgfplots", "tikz", "latex", "tex"); null for auto/unknown.</summary>
        public static string? FromHint(string? hint) => (hint ?? "").Trim().ToLowerInvariant() switch
        {
            "matplotlib" or "python" or "py" => Matplotlib,
            "pgfplots" => Pgfplots,
            "tikz" => Tikz,
            _ => null,
        };
    }

    /// <summary>
    /// Import of TikZ drawings (roadmap 4b): <c>\draw</c>, <c>\fill</c>, <c>\filldraw</c>, <c>\path</c> with lines, polylines,
    /// rectangles, circles, ellipses, arcs and Bézier curves (flattened), <c>\node</c> texts, colours (xcolor names, mixes,
    /// <c>\definecolor</c>), line widths, dash patterns, fills, hatch patterns and arrow tips (<c>-&gt;</c>, <c>&lt;-&gt;</c>,
    /// <c>-Stealth</c> …). Picture and scope options shift, scale and rotate are applied; units cm (default), mm, pt, in.
    /// Everything else (foreach, calc, node anchors of named nodes, styles, pics, matrices …) is reported as not imported.
    /// Parsing only; <see cref="Elements"/> creates basic shapes, lines, arrows and texts.
    /// </summary>
    public static class TikzImport
    {
        public enum ShapeKind { Line, Polyline, Rect, Ellipse, Text }

        /// <summary>One imported shape in mm (y down); colours are #rrggbb.</summary>
        public sealed class Shape
        {
            public ShapeKind Kind { get; set; }
            public List<Vec2> Points { get; set; } = new();
            public bool Closed { get; set; }
            public Rect Box { get; set; }
            public double Radius { get; set; }
            public string? Text { get; set; }
            /// <summary>Text height (mm).</summary>
            public double TextSize { get; set; } = 3.5;
            public TextAnchor Anchor { get; set; } = TextAnchor.Middle;
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            /// <summary>Text rotation in degrees, clockwise on screen.</summary>
            public double Rotation { get; set; }
            /// <summary>Frame around a node text (draw option).</summary>
            public bool Frame { get; set; }
            /// <summary>Stroke colour; null = no stroke.</summary>
            public string? Stroke { get; set; }
            public double? Width { get; set; }
            public DashStyle Dash { get; set; }
            public Fill? Fill { get; set; }
            public double? Opacity { get; set; }
            public ArrowHead HeadStart { get; set; }
            public ArrowHead HeadEnd { get; set; }
            public string? TextColor { get; set; }
            public int Line { get; set; }
        }

        /// <summary>The shapes of the imported pictures.</summary>
        public sealed class Drawing
        {
            public List<Shape> Shapes { get; } = new();
            public int Pictures { get; set; }
            public bool IsEmpty => Shapes.Count == 0;

            public Rect Bounds()
            {
                var pts = new List<Vec2>();
                foreach (var s in Shapes)
                {
                    switch (s.Kind)
                    {
                        case ShapeKind.Rect:
                        case ShapeKind.Ellipse:
                            pts.Add(new Vec2(s.Box.X, s.Box.Y)); pts.Add(new Vec2(s.Box.Right, s.Box.Bottom));
                            break;
                        case ShapeKind.Text:
                            var b = TextBox(s);
                            pts.Add(new Vec2(b.X, b.Y)); pts.Add(new Vec2(b.Right, b.Bottom));
                            break;
                        default: pts.AddRange(s.Points); break;
                    }
                }
                return pts.Count == 0 ? new Rect(0, 0, 0, 0) : Rect.FromPoints(pts);
            }
        }

        /// <summary>Estimated box of a text shape (anchor point, height, an average character width).</summary>
        public static Rect TextBox(Shape s)
        {
            var lines = (s.Text ?? "").Split('\n');
            var w = Math.Max(2, 0.6 * s.TextSize * lines.Max(l => l.Length)) + (s.Frame ? 1.2 : 0);
            var h = s.TextSize * 1.25 * lines.Length + (s.Frame ? 1.2 : 0);
            var p = s.Points.Count > 0 ? s.Points[0] : Vec2.Zero;
            var x = s.Anchor switch { TextAnchor.Start => p.X, TextAnchor.End => p.X - w, _ => p.X - w / 2 };
            return new Rect(x, p.Y - h / 2, w, h);
        }

        // ------------------------------------------------------------------ parsing

        /// <summary>
        /// Every tikzpicture of <paramref name="text"/> (or the whole text as one picture body when it has none). Pictures that
        /// contain a pgfplots axis are skipped when <paramref name="skipAxisPictures"/> (the pgfplots import reads them), else
        /// reported. Never throws.
        /// </summary>
        public static Drawing Parse(string text, CodeImportReport report, bool skipAxisPictures = true)
        {
            var drawing = new Drawing();
            var src = StripComments((text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'));
            var colors = new Dictionary<string, string>(StringComparer.Ordinal);
            // Global colour definitions (preamble).
            var p = new Parser(src, report, colors, drawing);
            var pics = Regex.Matches(src, @"\\begin\s*\{\s*tikzpicture\s*\}").Cast<Match>().ToList();
            if (pics.Count == 0)
            {
                p.Body(0, src.Length, Matrix2D.Identity, Units.Default);
                if (p.Statements > 0) drawing.Pictures = 1;
                return drawing;
            }
            p.Colors(0, pics[0].Index);
            foreach (var m in pics)
            {
                var start = m.Index + m.Length;
                var end = src.IndexOf("\\end{tikzpicture}", start, StringComparison.Ordinal);
                if (end < 0) end = src.Length;
                var body = src.Substring(start, end - start);
                if (Regex.IsMatch(body, @"\\begin\s*\{\s*(axis|semilogxaxis|semilogyaxis|loglogaxis|groupplot)\s*\}"))
                {
                    if (!skipAxisPictures)
                        report.Skip(LineOf(src, m.Index), "\\begin{tikzpicture}", new I18nText("A pgfplots axis is read by the pgfplots import, not as a drawing.", "Eine pgfplots-Achse wird vom pgfplots-Import gelesen, nicht als Zeichnung."));
                    continue;
                }
                var pos = start;
                var opts = p.TryOptions(ref pos);
                var units = Units.Default;
                var matrix = Matrix2D.Identity;
                if (opts is not null) (matrix, units) = p.Transform(opts, matrix, units, LineOf(src, m.Index));
                p.Body(pos, end, matrix, units);
                drawing.Pictures++;
            }
            return drawing;
        }

        /// <summary>Line number (1-based) of a position.</summary>
        public static int LineOf(string s, int pos)
        {
            var n = 1;
            for (var i = 0; i < pos && i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }

        /// <summary>LaTeX comments replaced by spaces (positions and line numbers stay valid).</summary>
        public static string StripComments(string s)
        {
            var sb = new StringBuilder(s);
            for (var i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '\\') { i++; continue; }
                if (sb[i] == '%') while (i < sb.Length && sb[i] != '\n') sb[i++] = ' ';
            }
            return sb.ToString();
        }

        /// <summary>Unit vectors of the xyz coordinate system in mm (y up).</summary>
        internal readonly record struct Units(double X, double Y)
        {
            public static readonly Units Default = new(10, 10);
        }

        /// <summary>A length in mm ("2mm", "0.4pt", "1.5cm", "1in", "1em"); a plain number is <paramref name="plainUnit"/> mm. Null if unreadable.</summary>
        public static double? LengthMm(string? s, double plainUnit = 10)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var m = Regex.Match(s!.Trim(), @"^([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)\s*(mm|cm|pt|in|bp|em|ex|pc)?$");
            if (!m.Success) return null;
            var v = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return m.Groups[2].Value switch
            {
                "mm" => v, "cm" => v * 10, "pt" => v * 25.4 / 72.27, "bp" => v * 25.4 / 72, "in" => v * 25.4, "pc" => v * 25.4 / 6,
                "em" => v * 3.5, "ex" => v * 1.6, _ => v * plainUnit,
            };
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        // ------------------------------------------------------------------ colours

        private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = "#000000", ["white"] = "#ffffff", ["red"] = "#ff0000", ["green"] = "#00ff00", ["blue"] = "#0000ff", ["cyan"] = "#00ffff",
            ["magenta"] = "#ff00ff", ["yellow"] = "#ffff00", ["gray"] = "#808080", ["grey"] = "#808080", ["darkgray"] = "#404040", ["lightgray"] = "#bfbfbf",
            ["brown"] = "#bf8040", ["lime"] = "#bfff00", ["olive"] = "#808000", ["orange"] = "#ff8000", ["pink"] = "#ffbfbf", ["purple"] = "#bf0040",
            ["teal"] = "#008080", ["violet"] = "#800080",
            // dvipsnames (common)
            ["Gray"] = "#949698", ["NavyBlue"] = "#006eb8", ["RoyalBlue"] = "#0071bc", ["ForestGreen"] = "#009b55", ["OliveGreen"] = "#3c8031",
            ["BrickRed"] = "#b6321c", ["Maroon"] = "#af3235", ["Orange"] = "#f58137", ["Goldenrod"] = "#ffdf42", ["Plum"] = "#92268f",
            ["Cerulean"] = "#00a2e3", ["Emerald"] = "#00a99d", ["Bittersweet"] = "#c04f17", ["Mahogany"] = "#a9341f", ["Sepia"] = "#671800",
        };

        /// <summary>A colour expression ("red", "blue!30", "red!50!black", "isk1F4E9A", "c1f77b4") as #rrggbb; null if unknown.</summary>
        public static string? Color(string? expr, IReadOnlyDictionary<string, string> defined)
        {
            if (string.IsNullOrWhiteSpace(expr)) return null;
            var e = expr!.Trim().Trim('{', '}').Trim();
            if (e.Length == 0) return null;
            var parts = e.Split('!');
            double[]? Rgb(string name)
            {
                name = name.Trim();
                if (name.StartsWith("-", StringComparison.Ordinal)) return null; // complement colours are not read
                string? hex = defined.TryGetValue(name, out var d) ? d : NamedColors.TryGetValue(name, out var n) ? n : null;
                if (hex is null) return null;
                return new[] { Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0, Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0, Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0 };
            }
            var cur = Rgb(parts[0]);
            if (cur is null) return null;
            // c!p!d: p % of c mixed with d (white when d is missing); chained left to right.
            var k = 1;
            while (k < parts.Length)
            {
                if (!double.TryParse(parts[k], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)) return null;
                var other = k + 1 < parts.Length ? Rgb(parts[k + 1]) : new[] { 1.0, 1.0, 1.0 };
                if (other is null) return null;
                var f = Math.Max(0, Math.Min(100, pct)) / 100;
                cur = new[] { cur[0] * f + other[0] * (1 - f), cur[1] * f + other[1] * (1 - f), cur[2] * f + other[2] * (1 - f) };
                k += 2;
            }
            return "#" + string.Concat(cur.Select(c => ((int)Math.Round(Math.Max(0, Math.Min(1, c)) * 255)).ToString("x2", CultureInfo.InvariantCulture)));
        }

        /// <summary>A colour definition: model HTML, rgb, RGB, gray, cmyk; null if unreadable.</summary>
        public static string? Define(string model, string spec)
        {
            var nums = spec.Split(',').Select(x => double.TryParse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).ToList();
            string Hex(double r, double g, double b) => "#" + string.Concat(new[] { r, g, b }.Select(c => ((int)Math.Round(Math.Max(0, Math.Min(1, c)) * 255)).ToString("x2", CultureInfo.InvariantCulture)));
            switch (model.Trim())
            {
                case "HTML":
                    var h = spec.Trim();
                    return h.Length == 6 && h.All(Uri.IsHexDigit) ? "#" + h.ToLowerInvariant() : null;
                case "rgb" when nums.Count == 3 && nums.All(Finite): return Hex(nums[0], nums[1], nums[2]);
                case "RGB" when nums.Count == 3 && nums.All(Finite): return Hex(nums[0] / 255, nums[1] / 255, nums[2] / 255);
                case "gray" when nums.Count == 1 && Finite(nums[0]): return Hex(nums[0], nums[0], nums[0]);
                case "cmyk" when nums.Count == 4 && nums.All(Finite):
                    return Hex((1 - nums[0]) * (1 - nums[3]), (1 - nums[1]) * (1 - nums[3]), (1 - nums[2]) * (1 - nums[3]));
            }
            return null;
        }

        // ------------------------------------------------------------------ text

        private static readonly Dictionary<string, string> TexSymbols = new(StringComparer.Ordinal)
        {
            ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["varepsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η",
            ["theta"] = "θ", ["vartheta"] = "ϑ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π", ["rho"] = "ρ", ["sigma"] = "σ",
            ["tau"] = "τ", ["phi"] = "φ", ["varphi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ",
            ["Lambda"] = "Λ", ["Pi"] = "Π", ["Sigma"] = "Σ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
            ["leq"] = "≤", ["le"] = "≤", ["geq"] = "≥", ["ge"] = "≥", ["neq"] = "≠", ["approx"] = "≈", ["pm"] = "±", ["times"] = "×", ["cdot"] = "·",
            ["infty"] = "∞", ["rightarrow"] = "→", ["to"] = "→", ["leftarrow"] = "←", ["Rightarrow"] = "⇒", ["circ"] = "°", ["degree"] = "°",
            ["textdegree"] = "°", ["textmu"] = "µ", ["textpm"] = "±", ["texttimes"] = "×", ["textminus"] = "−", ["dots"] = "…", ["ldots"] = "…",
            ["texteuro"] = "€", ["euro"] = "€", ["textperiodcentered"] = "·", ["textbackslash"] = "\\", ["textasciicircum"] = "^", ["textasciitilde"] = "~",
            ["textless"] = "<", ["textgreater"] = ">", ["textbar"] = "|", ["S"] = "§", ["P"] = "¶", ["copyright"] = "©", ["minus"] = "−",
        };

        private static readonly HashSet<string> DropCommands = new(StringComparer.Ordinal)
        {
            "small", "footnotesize", "scriptsize", "tiny", "normalsize", "large", "Large", "LARGE", "huge", "Huge", "bfseries", "itshape", "mdseries",
            "upshape", "rmfamily", "sffamily", "ttfamily", "selectfont", "centering", "raggedright", "raggedleft", "strut", "displaystyle", "mathrm",
            "textrm", "textsf", "texttt", "textnormal", "mathit", "mathbf", "mathsf", "boldmath", "unboldmath", "text", "mbox", "hbox", "ensuremath", "operatorname",
            "left", "right", "big", "Big", "underline", "emph", "textit", "textbf", "textup", "textsc", "textsuperscript", "textsubscript",
        };

        /// <summary>
        /// Plain text of a LaTeX node text: \textbf/\emph → bold/italic flags, math $…$ with Greek letters and symbols as
        /// Unicode, ^2/^3 as superscripts, line breaks \\ as new lines, escaped characters unescaped, \dot{V} as V̇.
        /// </summary>
        public static string PlainText(string tex, out bool bold, out bool italic)
        {
            bold = tex.Contains("\\textbf") || tex.Contains("\\bfseries");
            italic = tex.Contains("\\textit") || tex.Contains("\\emph") || tex.Contains("\\itshape");
            var sb = new StringBuilder();
            var s = tex;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '\\')
                {
                    if (i + 1 >= s.Length) break;
                    var n = s[i + 1];
                    if (n == '\\') { sb.Append('\n'); i++; while (i + 1 < s.Length && s[i + 1] == '[') { var e = s.IndexOf(']', i + 1); if (e < 0) break; i = e; } continue; }
                    if (n is ',' or ';' or ':' or ' ' or '!') { if (n != '!') sb.Append(' '); i++; continue; }
                    if (!char.IsLetter(n)) { sb.Append(n); i++; continue; }
                    var j = i + 1;
                    while (j < s.Length && char.IsLetter(s[j])) j++;
                    var name = s.Substring(i + 1, j - i - 1);
                    i = j - 1;
                    if (name == "dot" && j < s.Length)
                    {
                        // \dot{V} → V with a combining dot above
                        var arg = j < s.Length && s[j] == '{' ? s.IndexOf('}', j) : -1;
                        if (arg > j) { sb.Append(PlainText(s.Substring(j + 1, arg - j - 1), out _, out _)).Append('\u0307'); i = arg; }
                        continue;
                    }
                    if (name == "fontsize")
                    {
                        // \fontsize{a}{b}: skip both arguments
                        for (var k = 0; k < 2; k++)
                        {
                            var open = s.IndexOf('{', i + 1);
                            var close = open >= 0 ? s.IndexOf('}', open) : -1;
                            if (open < 0 || close < 0 || s.Substring(i + 1, open - i - 1).Trim().Length > 0) break;
                            i = close;
                        }
                        continue;
                    }
                    if (name == "textsuperscript")
                    {
                        // \textsuperscript{2} → ²
                        var open = j < s.Length && s[j] == '{' ? j : -1;
                        var close = open >= 0 ? s.IndexOf('}', open) : -1;
                        if (open >= 0 && close > open)
                        {
                            var sup = s.Substring(open + 1, close - open - 1).Trim();
                            sb.Append(sup switch { "2" => "²", "3" => "³", "1" => "¹", _ => sup });
                            i = close;
                        }
                        continue;
                    }
                    if (name is "color" or "textcolor")
                    {
                        // \color{red} / \textcolor{red}{text}: drop the colour argument
                        var open = s.IndexOf('{', i + 1);
                        var close = open >= 0 ? s.IndexOf('}', open) : -1;
                        if (open >= 0 && close > open && s.Substring(i + 1, open - i - 1).Trim().Length == 0) i = close;
                        continue;
                    }
                    if (TexSymbols.TryGetValue(name, out var sym)) { sb.Append(sym); if (j < s.Length && s[j] == '{' && j + 1 < s.Length && s[j + 1] == '}') i = j + 1; continue; }
                    if (DropCommands.Contains(name)) continue;
                    // Unknown command: keep its name without the backslash (better than dropping text).
                    sb.Append(name);
                    continue;
                }
                if (c is '{' or '}' or '$') continue;
                if (c == '~') { sb.Append('\u00a0'); continue; }
                if (c == '^' && i + 1 < s.Length)
                {
                    var t = s[i + 1] == '{' && i + 3 < s.Length && s[i + 3] == '}' ? s[i + 2] : s[i + 1];
                    var sup = t switch { '2' => "²", '3' => "³", '1' => "¹", '0' => "⁰", '-' => "⁻", _ => null };
                    if (sup is not null) { sb.Append(sup); i += s[i + 1] == '{' ? 3 : 1; continue; }
                    continue;
                }
                if (c == '_')
                {
                    // Subscripts stay as "x_i" (the chart texts' subscript notation).
                    sb.Append('_');
                    continue;
                }
                if (c == '\n' || c == '\t') { sb.Append(' '); continue; }
                sb.Append(c);
            }
            var text = Regex.Replace(sb.ToString(), "[ ]{2,}", " ");
            text = text.Replace("---", "—").Replace("--", "–").Replace("``", "“").Replace("''", "”").Replace(",,", "„");
            return string.Join("\n", text.Split('\n').Select(l => l.Trim())).Trim();
        }

        /// <summary>Text height in mm of a font option (\fontsize{8.5}{…}, \small, \footnotesize …; 10 pt base).</summary>
        internal static double FontMm(string? font, double def)
        {
            if (string.IsNullOrEmpty(font)) return def;
            var m = Regex.Match(font, @"\\fontsize\s*\{\s*([\d.]+)\s*(pt)?\s*\}");
            double pt;
            if (m.Success) pt = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            else if (font!.Contains("\\tiny")) pt = 5;
            else if (font.Contains("\\scriptsize")) pt = 7;
            else if (font.Contains("\\footnotesize")) pt = 8;
            else if (font.Contains("\\small")) pt = 9;
            else if (font.Contains("\\normalsize")) pt = 10;
            else if (font.Contains("\\LARGE")) pt = 17.28;
            else if (font.Contains("\\Large")) pt = 14.4;
            else if (font.Contains("\\large")) pt = 12;
            else if (font.Contains("\\Huge")) pt = 24.88;
            else if (font.Contains("\\huge")) pt = 20.74;
            else return def;
            return pt * 25.4 / 72.27;
        }

        /// <summary>LaTeX/TikZ syntax helpers shared with the pgfplots import.</summary>
        public static class Syntax
        {
            /// <summary>Content of a balanced group starting at s[i] (the open char); i is moved past the close char. Null if unbalanced.</summary>
            public static string? Group(string s, ref int i, char open, char close) => Parser.Group(s, ref i, open, close);

            /// <summary>An option list "a=1, b={x,y}, flag" as key/value pairs (values without outer braces).</summary>
            public static List<(string Key, string? Value)> Options(string list) => Parser.SplitOptions(list);

            /// <summary>The text without enclosing braces.</summary>
            public static string Unbrace(string v) => Parser.Unbrace(v);

            /// <summary>Splits at <paramref name="sep"/> outside braces, brackets and parentheses.</summary>
            public static List<string> SplitTop(string s, char sep) => Parser.SplitTop(s, sep);

            /// <summary>Arrow tips of an option ("-&gt;", "-{Stealth[length=2mm]}", "&lt;-&gt;"); null if the option is no arrow specification.</summary>
            public static (ArrowHead Start, ArrowHead End)? Arrow(string option)
                => new Parser("", new CodeImportReport(), new Dictionary<string, string>(), new Drawing()).Arrow(option);

            /// <summary>Dash pattern of TikZ options (dashed, dotted, dash dot, dash pattern=…); null when none is given.</summary>
            public static DashStyle? Dash(IEnumerable<(string Key, string? Value)> options)
            {
                DashStyle? d = null;
                foreach (var (k, v) in options)
                {
                    if (v is null)
                        switch (k)
                        {
                            case "solid": d = DashStyle.Solid; break;
                            case "dashed": case "densely dashed": case "loosely dashed": d = DashStyle.Dash; break;
                            case "dotted": case "densely dotted": case "loosely dotted": d = DashStyle.Dot; break;
                            case "dash dot": case "dashdotted": case "densely dashdotted": case "loosely dashdotted": d = DashStyle.DashDot; break;
                            case "dash dot dot": case "dashdotdotted": case "densely dashdotdotted": case "loosely dashdotdotted": d = DashStyle.DashDotDot; break;
                        }
                    else if (k == "dash pattern") d = Parser.DashOfPattern(v);
                }
                return d;
            }

            /// <summary>Line width of TikZ options (line width=…, thin, thick …) in mm; null when none is given.</summary>
            public static double? Width(IEnumerable<(string Key, string? Value)> options)
            {
                double? w = null;
                foreach (var (k, v) in options)
                {
                    if (v is null && Parser.WidthOf(k) is { } pt) w = pt * 25.4 / 72.27;
                    else if (k == "line width" && LengthMm(v, 25.4 / 72.27) is { } mm) w = mm;
                }
                return w;
            }
        }

        // ------------------------------------------------------------------ parser

        private sealed class Parser
        {
            private readonly string _s;
            private readonly CodeImportReport _report;
            private readonly Dictionary<string, string> _colors;
            private readonly Drawing _d;
            private readonly Dictionary<string, Vec2> _named = new(StringComparer.Ordinal);
            private string? _defaultArrow;

            public int Statements { get; private set; }

            private readonly List<int> _lineStarts = new() { 0 };

            public Parser(string s, CodeImportReport report, Dictionary<string, string> colors, Drawing d)
            {
                _s = s; _report = report; _colors = colors; _d = d;
                for (var i = 0; i < s.Length; i++) if (s[i] == '\n') _lineStarts.Add(i + 1);
            }

            private int LineOf(string s, int pos)
            {
                if (!ReferenceEquals(s, _s)) return TikzImport.LineOf(s, pos);
                var k = _lineStarts.BinarySearch(pos);
                return k >= 0 ? k + 1 : ~k;
            }

            private void Skip(int pos, string code, string en, string de) => _report.Skip(LineOf(_s, pos), Snip(code), new I18nText(en, de));

            private static string Snip(string code)
            {
                var t = Regex.Replace(code, @"\s+", " ").Trim();
                return t.Length > 90 ? t.Substring(0, 87) + "…" : t;
            }

            private sealed class Unsupported : Exception
            {
                public Unsupported(string en, string de) : base(en) { Text = new I18nText(en, de); }
                public I18nText Text { get; }
            }

            // ---------------------------------------------------------- low-level reading

            private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            /// <summary>
            /// Content of a balanced group starting at s[i] (open char), i moved past the close char. Null if s[i] is not the
            /// open char (i unchanged) or the group is unbalanced (i moved to the end, so reading loops always end).
            /// </summary>
            internal static string? Group(string s, ref int i, char open, char close)
            {
                if (i >= s.Length || s[i] != open) return null;
                var depth = 0;
                var start = i + 1;
                for (var k = i; k < s.Length; k++)
                {
                    var c = s[k];
                    if (c == '\\') { k++; continue; }
                    if (c == open && (open != close || k == i)) depth++;
                    else if (c == close) { depth--; if (depth == 0) { i = k + 1; return s.Substring(start, k - start); } }
                    else if (open != '{' && c == '{')
                    {
                        // Braces protect the close char (options like {(1,2)} or labels with "]").
                        var j = k;
                        if (Group(s, ref j, '{', '}') is null) { i = s.Length; return null; }
                        k = j - 1;
                    }
                }
                i = s.Length;
                return null;
            }

            public List<(string Key, string? Value)>? TryOptions(ref int i)
            {
                var j = i;
                Ws(_s, ref j);
                if (j >= _s.Length || _s[j] != '[') return null;
                var g = Group(_s, ref j, '[', ']');
                if (g is null) return null;
                i = j;
                return SplitOptions(g);
            }

            internal static List<(string Key, string? Value)> SplitOptions(string g)
            {
                var list = new List<(string, string?)>();
                foreach (var part in SplitTop(g, ','))
                {
                    var p = part.Trim();
                    if (p.Length == 0) continue;
                    var eq = TopIndex(p, '=');
                    if (eq > 0) list.Add((p.Substring(0, eq).Trim(), Unbrace(p.Substring(eq + 1).Trim())));
                    else list.Add((p, null));
                }
                return list;
            }

            internal static string Unbrace(string v)
            {
                v = v.Trim();
                while (v.Length >= 2 && v[0] == '{' && v[v.Length - 1] == '}')
                {
                    var i = 0;
                    if (Group(v, ref i, '{', '}') is null || i != v.Length) break;
                    v = v.Substring(1, v.Length - 2).Trim();
                }
                return v;
            }

            internal static List<string> SplitTop(string s, char sep)
            {
                var list = new List<string>();
                var depth = 0;
                var start = 0;
                for (var i = 0; i < s.Length; i++)
                {
                    var c = s[i];
                    if (c == '\\') { i++; continue; }
                    if (c is '{' or '[' or '(') depth++;
                    else if (c is '}' or ']' or ')') depth--;
                    else if (c == sep && depth == 0) { list.Add(s.Substring(start, i - start)); start = i + 1; }
                }
                list.Add(s.Substring(start));
                return list;
            }

            private static int TopIndex(string s, char c)
            {
                var depth = 0;
                for (var i = 0; i < s.Length; i++)
                {
                    if (s[i] == '\\') { i++; continue; }
                    if (s[i] is '{' or '[' or '(') depth++;
                    else if (s[i] is '}' or ']' or ')') depth--;
                    else if (s[i] == c && depth == 0) return i;
                }
                return -1;
            }

            /// <summary>End of a statement: the ";" at depth 0 from <paramref name="i"/> (or the end).</summary>
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

            // ---------------------------------------------------------- preamble colours

            public void Colors(int from, int to)
            {
                foreach (Match m in Regex.Matches(_s.Substring(from, to - from), @"\\definecolor\s*\{([^}]*)\}\s*\{([^}]*)\}\s*\{([^}]*)\}"))
                    if (Define(m.Groups[2].Value, m.Groups[3].Value) is { } hex) _colors[m.Groups[1].Value.Trim()] = hex;
                foreach (Match m in Regex.Matches(_s.Substring(from, to - from), @"\\colorlet\s*\{([^}]*)\}\s*\{([^}]*)\}"))
                    if (Color(m.Groups[2].Value, _colors) is { } hex) _colors[m.Groups[1].Value.Trim()] = hex;
            }

            // ---------------------------------------------------------- body

            /// <summary>Reads the statements between <paramref name="from"/> and <paramref name="to"/>.</summary>
            public void Body(int from, int to, Matrix2D m, Units u)
            {
                var stack = new Stack<(Matrix2D M, Units U)>();
                var i = from;
                while (i < to)
                {
                    Ws(_s, ref i);
                    if (i >= to) break;
                    if (_s[i] != '\\')
                    {
                        // Stray text (\begin{document} leftovers, braces): skip to the next command.
                        var next = _s.IndexOf('\\', i);
                        i = next < 0 || next > to ? to : next;
                        continue;
                    }
                    var j = i + 1;
                    while (j < to && char.IsLetter(_s[j])) j++;
                    var cmd = _s.Substring(i + 1, j - i - 1);
                    switch (cmd)
                    {
                        case "begin":
                            {
                                var k = j;
                                Ws(_s, ref k);
                                var env = Group(_s, ref k, '{', '}')?.Trim() ?? "";
                                if (env == "scope")
                                {
                                    stack.Push((m, u));
                                    var opts = TryOptions(ref k);
                                    if (opts is not null) (m, u) = Transform(opts, m, u, LineOf(_s, i));
                                    i = k;
                                    continue;
                                }
                                if (env is "document" or "tikzpicture" or "center" or "figure") { i = k; TryOptions(ref i); continue; }
                                Skip(i, "\\begin{" + env + "}", "The environment “" + env + "” is not read.", "Die Umgebung „" + env + "“ wird nicht gelesen.");
                                var endTag = "\\end{" + env + "}";
                                var e = _s.IndexOf(endTag, k, StringComparison.Ordinal);
                                i = e < 0 || e > to ? to : e + endTag.Length;
                                continue;
                            }
                        case "end":
                            {
                                var k = j;
                                Ws(_s, ref k);
                                var env = Group(_s, ref k, '{', '}')?.Trim() ?? "";
                                if (env == "scope" && stack.Count > 0) (m, u) = stack.Pop();
                                i = k;
                                continue;
                            }
                        case "definecolor":
                            {
                                var k = j;
                                var name = Arg(ref k); var model = Arg(ref k); var spec = Arg(ref k);
                                if (name is not null && model is not null && spec is not null && Define(model, spec) is { } hex) _colors[name.Trim()] = hex;
                                else Skip(i, _s.Substring(i, Math.Min(60, to - i)), "The colour definition cannot be read.", "Die Farbdefinition ist nicht lesbar.");
                                i = k;
                                continue;
                            }
                        case "colorlet":
                            {
                                var k = j;
                                var name = Arg(ref k); var spec = Arg(ref k);
                                if (name is not null && Color(spec, _colors) is { } hex) _colors[name.Trim()] = hex;
                                i = k;
                                continue;
                            }
                        case "documentclass": case "usepackage": case "usetikzlibrary": case "usepgfplotslibrary": case "pgfplotsset": case "tikzset":
                        case "centering": case "noindent": case "par": case "pagestyle": case "thispagestyle": case "caption": case "label":
                            {
                                var k = j;
                                TryOptions(ref k);
                                var a = Arg(ref k);
                                if (cmd == "tikzset" && a is not null)
                                {
                                    var arrow = Regex.Match(a, @">\s*=\s*([A-Za-z]+)");
                                    if (arrow.Success) _defaultArrow = arrow.Groups[1].Value;
                                    else Skip(i, "\\tikzset{" + a + "}", "Style definitions (\\tikzset) are not read.", "Stildefinitionen (\\tikzset) werden nicht gelesen.");
                                }
                                i = k;
                                continue;
                            }
                    }
                    var end = StatementEnd(j, to);
                    var stmt = _s.Substring(i, Math.Min(end + 1, to) - i);
                    try
                    {
                        switch (cmd)
                        {
                            case "draw": case "fill": case "filldraw": case "path": case "shade": case "shadedraw":
                                Statements++;
                                PathCommand(cmd, j, end, m, u, i);
                                break;
                            case "node":
                                Statements++;
                                NodeCommand(j, end, m, u, i);
                                break;
                            case "coordinate":
                                Statements++;
                                CoordinateCommand(j, end, m, u);
                                break;
                            case "clip":
                                Skip(i, stmt, "Clipping (\\clip) is not imported; the shapes are imported whole.", "Zuschneiden (\\clip) wird nicht importiert; die Formen werden vollständig importiert.");
                                break;
                            default:
                                Skip(i, stmt, "The command \\" + cmd + " is not read.", "Der Befehl \\" + cmd + " wird nicht gelesen.");
                                if (cmd == "foreach")
                                {
                                    // \foreach … { … } without ";" at the end of the body.
                                    var brace = _s.IndexOf('{', j);
                                    if (brace >= 0 && brace < to)
                                    {
                                        var k = brace;
                                        // The last group of the foreach is the body.
                                        var inG = _s.IndexOf(" in ", j, StringComparison.Ordinal);
                                        if (inG >= 0 && inG < brace) { Group(_s, ref k, '{', '}'); Ws(_s, ref k); if (k < to && _s[k] == '{') Group(_s, ref k, '{', '}'); }
                                        else Group(_s, ref k, '{', '}');
                                        if (k <= to && k < end) end = k - 1;
                                    }
                                }
                                break;
                        }
                    }
                    catch (Unsupported ex) { _report.Skip(LineOf(_s, i), Snip(stmt), ex.Text); }
                    catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException or InvalidOperationException or InvalidCastException or NullReferenceException or KeyNotFoundException)
                    {
                        _report.Skip(LineOf(_s, i), Snip(stmt), new I18nText("Cannot be read: " + ex.Message, "Nicht lesbar: " + ex.Message));
                    }
                    i = end + 1;
                }
            }

            private string? Arg(ref int k)
            {
                Ws(_s, ref k);
                return Group(_s, ref k, '{', '}');
            }

            /// <summary>Applies transform options (shift, xshift, yshift, scale, xscale, yscale, rotate, x, y) to the matrix and units.</summary>
            public (Matrix2D, Units) Transform(List<(string Key, string? Value)> opts, Matrix2D m, Units u, int line)
            {
                foreach (var (k, v) in opts)
                {
                    switch (k)
                    {
                        case "x" when v is not null:
                            if (LengthMm(v) is { } x) u = u with { X = x };
                            else _report.Skip(line, "x=" + v, new I18nText("Unit vectors other than a length are not read.", "Einheitsvektoren außer einer Länge werden nicht gelesen."));
                            break;
                        case "y" when v is not null:
                            if (LengthMm(v) is { } y) u = u with { Y = y };
                            else _report.Skip(line, "y=" + v, new I18nText("Unit vectors other than a length are not read.", "Einheitsvektoren außer einer Länge werden nicht gelesen."));
                            break;
                        case "scale" when Num(v) is { } s: m = m * Matrix2D.Scaling(s, s); break;
                        case "xscale" when Num(v) is { } s: m = m * Matrix2D.Scaling(s, 1); break;
                        case "yscale" when Num(v) is { } s: m = m * Matrix2D.Scaling(1, s); break;
                        case "rotate" when Num(v) is { } r: m = m * Matrix2D.Rotation(r * Math.PI / 180); break;
                        case "xshift" when LengthMm(v, u.X) is { } dx: m = m * Matrix2D.Translation(dx, 0); break;
                        case "yshift" when LengthMm(v, u.Y) is { } dy: m = m * Matrix2D.Translation(0, dy); break;
                        case "shift" when v is not null:
                            {
                                var t = v.Trim();
                                if (t.StartsWith("(", StringComparison.Ordinal) && t.EndsWith(")", StringComparison.Ordinal) && Raw(t.Substring(1, t.Length - 2), u) is { } p)
                                    m = m * Matrix2D.Translation(p.X, p.Y);
                                else _report.Skip(line, "shift=" + v, new I18nText("This shift cannot be read.", "Diese Verschiebung ist nicht lesbar."));
                                break;
                            }
                    }
                }
                return (m, u);
            }

            private static double? Num(string? v)
                => v is not null && double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && Finite(d) ? d : null;

            /// <summary>A coordinate "x,y" or "a:r" before the transformation (mm, y up); null if it is not a plain coordinate.</summary>
            private static Vec2? Raw(string c, Units u)
            {
                var t = c.Trim();
                if (t.StartsWith("canvas cs:", StringComparison.Ordinal) || t.StartsWith("xyz cs:", StringComparison.Ordinal))
                {
                    var o = SplitOptions(t.Substring(t.IndexOf(':') + 1));
                    double? Get(string k) => o.FirstOrDefault(x => x.Key == k).Value is { } v ? LengthMm(v, k == "x" ? u.X : u.Y) : 0;
                    return Get("x") is { } cx && Get("y") is { } cy ? new Vec2(cx, cy) : null;
                }
                var comma = TopIndex(t, ',');
                if (comma > 0)
                {
                    var x = LengthMm(Unbrace(t.Substring(0, comma)), u.X);
                    var y = LengthMm(Unbrace(t.Substring(comma + 1)), u.Y);
                    return x is { } xv && y is { } yv ? new Vec2(xv, yv) : null;
                }
                var colon = TopIndex(t, ':');
                if (colon > 0)
                {
                    var a = Num(Unbrace(t.Substring(0, colon)));
                    var r = LengthMm(Unbrace(t.Substring(colon + 1)), u.X);
                    if (a is { } av && r is { } rv) return new Vec2(rv * Math.Cos(av * Math.PI / 180), rv * Math.Sin(av * Math.PI / 180));
                }
                return null;
            }

            /// <summary>A coordinate in paper mm (y up, transformed): plain, polar or a named coordinate.</summary>
            private Vec2 Point(string c, Matrix2D m, Units u)
            {
                var t = c.Trim();
                if (t.StartsWith("$", StringComparison.Ordinal)) throw new Unsupported("Calculated coordinates ($…$) are not read.", "Berechnete Koordinaten ($…$) werden nicht gelesen.");
                if (t.Contains("axis cs:") || t.Contains("axis description cs:")) throw new Unsupported("Axis coordinates belong to a pgfplots axis.", "Achskoordinaten gehören zu einer pgfplots-Achse.");
                if (Raw(t, u) is { } p) return m.Apply(p);
                var name = t.EndsWith(".center", StringComparison.Ordinal) ? t.Substring(0, t.Length - 7) : t;
                if (_named.TryGetValue(name, out var n)) return n;
                if (t.Contains('.')) throw new Unsupported("Anchors of named nodes (" + t + ") are not read.", "Anker benannter Knoten (" + t + ") werden nicht gelesen.");
                throw new Unsupported("The coordinate (" + t + ") cannot be read.", "Die Koordinate (" + t + ") ist nicht lesbar.");
            }

            private void CoordinateCommand(int j, int end, Matrix2D m, Units u)
            {
                var k = j;
                TryOptions(ref k);
                Ws(_s, ref k);
                var name = Group(_s, ref k, '(', ')');
                Ws(_s, ref k);
                if (name is null || !_s.Substring(k, Math.Max(0, end - k)).TrimStart().StartsWith("at", StringComparison.Ordinal)) throw new Unsupported("The coordinate cannot be read.", "Die Koordinate ist nicht lesbar.");
                k = _s.IndexOf("at", k, StringComparison.Ordinal) + 2;
                Ws(_s, ref k);
                var c = Group(_s, ref k, '(', ')') ?? throw new Unsupported("The coordinate cannot be read.", "Die Koordinate ist nicht lesbar.");
                _named[name.Trim()] = Point(c, m, u);
            }

            // ---------------------------------------------------------- style

            private sealed class Look
            {
                public bool Draw;
                public bool FillOn;
                public string? Stroke;
                public string? FillColor;
                public double? Width;
                public DashStyle Dash;
                public double? Opacity;
                public double? FillOpacity;
                public ArrowHead Start;
                public ArrowHead End;
                public double Rounded;
                public Fill? Pattern;
                public string? PatternColor;
                public string? TextColor;
            }

            internal static double? WidthOf(string key) => Widths.TryGetValue(key, out var w) ? w : null;

            private static readonly Dictionary<string, double> Widths = new(StringComparer.Ordinal)
            {
                ["ultra thin"] = 0.1, ["very thin"] = 0.2, ["thin"] = 0.4, ["semithick"] = 0.6, ["thick"] = 0.8, ["very thick"] = 1.2, ["ultra thick"] = 1.6,
            };

            private Look Style(string cmd, List<(string Key, string? Value)> opts, int pos)
            {
                var l = new Look
                {
                    Draw = cmd is "draw" or "filldraw" or "shadedraw",
                    FillOn = cmd is "fill" or "filldraw" or "shade" or "shadedraw",
                };
                string? color = null;
                foreach (var (k, v) in opts)
                {
                    if (v is null)
                    {
                        if (Widths.TryGetValue(k, out var wpt)) { l.Width = wpt * 25.4 / 72.27; continue; }
                        switch (k)
                        {
                            case "draw": l.Draw = true; continue;
                            case "fill": l.FillOn = true; continue;
                            case "solid": l.Dash = DashStyle.Solid; continue;
                            case "dashed": case "densely dashed": case "loosely dashed": l.Dash = DashStyle.Dash; continue;
                            case "dotted": case "densely dotted": case "loosely dotted": l.Dash = DashStyle.Dot; continue;
                            case "dash dot": case "dashdotted": case "densely dashdotted": case "loosely dashdotted": l.Dash = DashStyle.DashDot; continue;
                            case "dash dot dot": case "dashdotdotted": case "densely dashdotdotted": case "loosely dashdotdotted": l.Dash = DashStyle.DashDotDot; continue;
                            case "rounded corners": l.Rounded = 4 * 25.4 / 72.27; continue;
                            case "sharp corners": l.Rounded = 0; continue;
                            case "line cap=round": case "line join=round": continue;
                        }
                        if (Arrow(k) is { } heads) { l.Start = heads.Start; l.End = heads.End; continue; }
                        if (Color(k, _colors) is { } c) { color = c; continue; }
                        continue;
                    }
                    switch (k)
                    {
                        case "draw": if (v == "none") l.Draw = false; else { l.Draw = true; l.Stroke = Color(v, _colors) ?? Unknown(v, pos); } break;
                        case "fill": if (v == "none") l.FillOn = false; else { l.FillOn = true; l.FillColor = Color(v, _colors) ?? Unknown(v, pos); } break;
                        case "color": color = Color(v, _colors) ?? Unknown(v, pos); break;
                        case "text": l.TextColor = Color(v, _colors) ?? Unknown(v, pos); break;
                        case "line width": l.Width = LengthMm(v, 25.4 / 72.27); break;
                        case "opacity": l.Opacity = Num(v); break;
                        case "draw opacity": l.Opacity = Num(v); break;
                        case "fill opacity": l.FillOpacity = Num(v); break;
                        case "rounded corners": l.Rounded = LengthMm(v, 1) ?? 0; break;
                        case "dash pattern": l.Dash = DashOfPattern(v); break;
                        case "pattern": l.Pattern = PatternOf(v); l.FillOn = true; break;
                        case "pattern color": l.PatternColor = Color(v, _colors); break;
                        case ">": _defaultArrow = v; break;
                        case "arrows": if (Arrow(v) is { } a) { l.Start = a.Start; l.End = a.End; } break;
                    }
                }
                if (color is not null) { l.Stroke ??= color; l.FillColor ??= color; l.TextColor ??= color; }
                return l;
            }

            private string? Unknown(string v, int pos)
            {
                Skip(pos, v, "The colour “" + v + "” is not known; the default colour is used.", "Die Farbe „" + v + "“ ist unbekannt; die Standardfarbe wird verwendet.");
                return null;
            }

            /// <summary>Dash pattern "on a off b …" classified like the TikZ export writes it.</summary>
            internal static DashStyle DashOfPattern(string v)
            {
                var ons = Regex.Matches(v, @"on\s*([\d.]+)\s*(mm|pt|cm|bp)?").Cast<Match>().Select(m => LengthMm(m.Groups[1].Value + m.Groups[2].Value, 25.4 / 72.27) ?? 0).ToList();
                var offs = Regex.Matches(v, @"off\s*([\d.]+)\s*(mm|pt|cm|bp)?").Cast<Match>().Select(m => LengthMm(m.Groups[1].Value + m.Groups[2].Value, 25.4 / 72.27) ?? 0).ToList();
                if (ons.Count == 0) return DashStyle.Solid;
                if (ons.Count >= 3) return DashStyle.DashDotDot;
                if (ons.Count == 2) return DashStyle.DashDot;
                var off = offs.Count > 0 ? offs[0] : ons[0];
                if (ons[0] <= off * 0.2) return DashStyle.Dot;
                return off > 0 && ons[0] / off >= 2.6 ? DashStyle.LongDash : DashStyle.Dash;
            }

            private static Fill? PatternOf(string v)
            {
                var t = v.Trim();
                double Opt(string key, double def)
                {
                    var m = Regex.Match(t, key + @"\s*=\s*([-\d.]+\s*(mm|pt|cm)?)");
                    return m.Success ? LengthMm(m.Groups[1].Value.Trim(), 1) ?? def : def;
                }
                double Ang(double def)
                {
                    var m = Regex.Match(t, @"angle\s*=\s*([-\d.]+)");
                    return m.Success ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : def;
                }
                if (t.StartsWith("Lines", StringComparison.Ordinal)) return Fill.Hatched(new HatchPattern(Ang(0), Opt("distance", 3 * 25.4 / 72.27)));
                if (t.StartsWith("Hatch", StringComparison.Ordinal)) return Fill.Hatched(new HatchPattern(Ang(0), Opt("distance", 3 * 25.4 / 72.27), Cross: true));
                if (t.StartsWith("Dots", StringComparison.Ordinal)) return Fill.Dotted(Opt("distance", 3 * 25.4 / 72.27));
                return t switch
                {
                    "north east lines" => Fill.Hatched(new HatchPattern(45, 1.06)),
                    "north west lines" => Fill.Hatched(new HatchPattern(135, 1.06)),
                    "horizontal lines" => Fill.Hatched(new HatchPattern(0, 1.06)),
                    "vertical lines" => Fill.Hatched(new HatchPattern(90, 1.06)),
                    "crosshatch" => Fill.Hatched(new HatchPattern(0, 1.06, Cross: true)),
                    "crosshatch dots" or "dots" or "fivepointed stars" or "sixpointed stars" => Fill.Dotted(1.06),
                    "grid" => Fill.Hatched(new HatchPattern(0, 1.06, Cross: true)),
                    _ => null,
                };
            }

            /// <summary>Arrow specification "-&gt;", "&lt;-&gt;", "-Stealth", "{Latex[open]}-{Latex}", "|-|"; null if the option is not one.</summary>
            internal (ArrowHead Start, ArrowHead End)? Arrow(string opt)
            {
                var o = opt.Trim();
                // The separating "-" at depth 0.
                var depth = 0;
                var dash = -1;
                for (var i = 0; i < o.Length; i++)
                {
                    var c = o[i];
                    if (c is '{' or '[') depth++;
                    else if (c is '}' or ']') depth--;
                    else if (c == '-' && depth == 0) { if (dash >= 0) return null; dash = i; }
                }
                if (dash < 0) return null;
                var a = Tip(o.Substring(0, dash), true);
                var b = Tip(o.Substring(dash + 1), false);
                if (a is null || b is null) return null;
                if (a == ArrowHead.None && b == ArrowHead.None && o != "-") return null;
                return (a.Value, b.Value);
            }

            private ArrowHead? Tip(string spec, bool start)
            {
                var t = spec.Trim();
                if (t.Length == 0) return ArrowHead.None;
                t = Unbrace(t);
                var open = Regex.IsMatch(t, @"\[[^\]]*\bopen\b");
                var bracket = t.IndexOf('[');
                if (bracket >= 0) t = t.Substring(0, bracket).Trim();
                if (t.Length == 0) return ArrowHead.None;
                // "<" at the start and ">" at the end are the default tips; ">>" double tips count once.
                if (t.Trim('<', '>').Length == 0) return Default(open);
                return t switch
                {
                    "Stealth" or "stealth" or "stealth'" or "Stealth Round" => ArrowHead.Stealth,
                    "Latex" or "latex" or "latex'" or "Triangle" or "triangle 45" or "triangle 60" or "triangle 90" or "Kite" => open ? ArrowHead.TriangleOpen : ArrowHead.Triangle,
                    "To" or "to" or "Computer Modern Rightarrow" or "Straight Barb" or "Arc Barb" or "angle 45" or "angle 60" or "angle 90" => ArrowHead.Open,
                    "Circle" or "o" or "*" => ArrowHead.Oval,
                    "Diamond" or "diamond" => open ? ArrowHead.DiamondOpen : ArrowHead.Diamond,
                    "Bar" or "|" or "Tee Barb" => ArrowHead.Tick,
                    "Square" or "Rectangle" => ArrowHead.Triangle,
                    "Hooks" or "Hooks[right]" => ArrowHead.Half,
                    _ => null,
                };
            }

            private ArrowHead Default(bool open)
            {
                var d = _defaultArrow is { } da ? Tip(da, false) : null;
                return d ?? ArrowHead.Open;
            }

            // ---------------------------------------------------------- paths

            private sealed class Sub
            {
                public List<Vec2> Points = new();
                public bool Closed;
            }

            private void PathCommand(string cmd, int j, int end, Matrix2D m, Units u, int pos)
            {
                var k = j;
                var opts = TryOptions(ref k) ?? new List<(string, string?)>();
                if (cmd is "shade" or "shadedraw")
                    Skip(pos, "\\" + cmd, "Shadings are imported as a plain fill.", "Farbverläufe werden als einfache Füllung importiert.");
                var look = Style(cmd, opts, pos);
                var (pm, pu) = Transform(opts, m, u, LineOf(_s, pos));
                var subs = new List<Sub>();
                var shapes = new List<Shape>();
                Sub? cur = null;
                Vec2? at = null;
                Vec2? basePt = null;
                Vec2? prev = null;
                var body = _s.Substring(k, end - k);
                var i = 0;
                var rounded = look.Rounded;
                while (i < body.Length)
                {
                    Ws(body, ref i);
                    if (i >= body.Length) break;
                    var c = body[i];
                    if (c == '[')
                    {
                        var o = Group(body, ref i, '[', ']');
                        if (o is not null)
                            foreach (var (ok, ov) in SplitOptions(o))
                                if (ok == "rounded corners") rounded = ov is null ? 4 * 25.4 / 72.27 : LengthMm(ov, 1) ?? 0;
                        continue;
                    }
                    if (c == '(' || c == '+')
                    {
                        var rel = 0;
                        while (i < body.Length && body[i] == '+') { rel++; i++; }
                        Ws(body, ref i);
                        var g = Group(body, ref i, '(', ')') ?? throw new Unsupported("Unbalanced parentheses.", "Unausgeglichene Klammern.");
                        var p = Coord(g, pm, pu, rel, basePt);
                        cur = new Sub(); subs.Add(cur); cur.Points.Add(p);
                        prev = at; at = p;
                        if (rel != 1) basePt = p;
                        continue;
                    }
                    if (Starts(body, i, "--") || Starts(body, i, "-|") || Starts(body, i, "|-"))
                    {
                        var op = body.Substring(i, 2);
                        i += 2;
                        Ws(body, ref i);
                        if (Starts(body, i, "cycle"))
                        {
                            i += 5;
                            if (cur is not null) { cur.Closed = true; at = basePt = cur.Points[0]; }
                            continue;
                        }
                        var rel = 0;
                        while (i < body.Length && body[i] == '+') { rel++; i++; }
                        Ws(body, ref i);
                        var g = Group(body, ref i, '(', ')') ?? throw new Unsupported("A coordinate is missing after " + op + ".", "Nach " + op + " fehlt eine Koordinate.");
                        var p = Coord(g, pm, pu, rel, basePt);
                        if (rel != 1) basePt = p;
                        if (at is null || cur is null) { cur = new Sub(); subs.Add(cur); cur.Points.Add(p); at = p; continue; }
                        if (op == "-|") cur.Points.Add(new Vec2(p.X, at.Value.Y));
                        else if (op == "|-") cur.Points.Add(new Vec2(at.Value.X, p.Y));
                        cur.Points.Add(p);
                        prev = at; at = p;
                        continue;
                    }
                    if (Starts(body, i, ".."))
                    {
                        i += 2;
                        Ws(body, ref i);
                        if (!Starts(body, i, "controls")) throw new Unsupported("Curves without controls are not read.", "Kurven ohne controls werden nicht gelesen.");
                        i += 8;
                        Ws(body, ref i);
                        var rel1 = Plus(body, ref i);
                        var c1 = Coord(Group(body, ref i, '(', ')') ?? "", pm, pu, rel1, basePt);
                        Ws(body, ref i);
                        var c2 = c1;
                        if (Starts(body, i, "and"))
                        {
                            i += 3;
                            Ws(body, ref i);
                            var rel2 = Plus(body, ref i);
                            c2 = Coord(Group(body, ref i, '(', ')') ?? "", pm, pu, rel2, basePt);
                            Ws(body, ref i);
                        }
                        if (!Starts(body, i, "..")) throw new Unsupported("The curve cannot be read.", "Die Kurve ist nicht lesbar.");
                        i += 2;
                        Ws(body, ref i);
                        Vec2 p;
                        if (Starts(body, i, "cycle")) { i += 5; p = cur?.Points[0] ?? Vec2.Zero; if (cur is not null) cur.Closed = true; }
                        else { var rel = Plus(body, ref i); p = Coord(Group(body, ref i, '(', ')') ?? "", pm, pu, rel, basePt); }
                        if (at is null || cur is null) throw new Unsupported("A curve needs a start point.", "Eine Kurve braucht einen Startpunkt.");
                        var a0 = at.Value;
                        for (var t = 1; t <= 16; t++)
                        {
                            var s = t / 16.0; var w = 1 - s;
                            cur.Points.Add(new Vec2(w * w * w * a0.X + 3 * w * w * s * c1.X + 3 * w * s * s * c2.X + s * s * s * p.X,
                                                    w * w * w * a0.Y + 3 * w * w * s * c1.Y + 3 * w * s * s * c2.Y + s * s * s * p.Y));
                        }
                        prev = at; at = basePt = p;
                        continue;
                    }
                    if (StartsWord(body, i, "rectangle"))
                    {
                        i += 9;
                        Ws(body, ref i);
                        var rel = Plus(body, ref i);
                        var g = Group(body, ref i, '(', ')') ?? throw new Unsupported("A corner is missing after rectangle.", "Nach rectangle fehlt eine Ecke.");
                        if (at is null) throw new Unsupported("A rectangle needs a start corner.", "Ein Rechteck braucht eine Startecke.");
                        var p = Coord(g, pm, pu, rel, basePt);
                        if (cur is not null && cur.Points.Count == 1) subs.Remove(cur);
                        var a0 = at.Value;
                        if (pm.IsAxisAligned)
                        {
                            shapes.Add(new Shape { Kind = ShapeKind.Rect, Box = Rect.FromPoints(new[] { Flip(a0), Flip(p) }), Radius = rounded, Line = LineOf(_s, pos) });
                        }
                        else
                        {
                            var inv = pm.Invert();
                            var ra = inv.Apply(a0); var rb = inv.Apply(p);
                            var poly = new Sub { Closed = true, Points = { a0, pm.Apply(new Vec2(rb.X, ra.Y)), p, pm.Apply(new Vec2(ra.X, rb.Y)) } };
                            subs.Add(poly);
                        }
                        cur = null;
                        prev = at; at = p;
                        if (rel != 1) basePt = p;
                        continue;
                    }
                    if (StartsWord(body, i, "circle") || StartsWord(body, i, "ellipse"))
                    {
                        var word = StartsWord(body, i, "circle") ? "circle" : "ellipse";
                        i += word.Length;
                        Ws(body, ref i);
                        double rx, ry;
                        if (i < body.Length && body[i] == '[')
                        {
                            var o = SplitOptions(Group(body, ref i, '[', ']') ?? "");
                            var r = o.FirstOrDefault(x => x.Key == "radius").Value;
                            var xr = o.FirstOrDefault(x => x.Key == "x radius").Value ?? r;
                            var yr = o.FirstOrDefault(x => x.Key == "y radius").Value ?? r;
                            rx = LengthMm(xr, Math.Abs(pu.X)) ?? throw new Unsupported("The radius cannot be read.", "Der Radius ist nicht lesbar.");
                            ry = LengthMm(yr, Math.Abs(pu.Y)) ?? rx;
                        }
                        else if (i < body.Length && body[i] == '(')
                        {
                            var g = Group(body, ref i, '(', ')') ?? "";
                            var and = Regex.Split(g, @"\s+and\s+");
                            rx = LengthMm(and[0].Trim(), Math.Abs(pu.X)) ?? throw new Unsupported("The radius cannot be read.", "Der Radius ist nicht lesbar.");
                            ry = and.Length > 1 ? LengthMm(and[1].Trim(), Math.Abs(pu.Y)) ?? rx : LengthMm(and[0].Trim(), Math.Abs(pu.Y)) ?? rx;
                        }
                        else throw new Unsupported("The radius cannot be read.", "Der Radius ist nicht lesbar.");
                        if (at is null) throw new Unsupported("A circle needs a centre.", "Ein Kreis braucht einen Mittelpunkt.");
                        if (cur is not null && cur.Points.Count == 1) subs.Remove(cur);
                        cur = null;
                        var cpt = at.Value;
                        var lin = pm;
                        var ex = lin.ApplyLinear(new Vec2(rx, 0)); var ey = lin.ApplyLinear(new Vec2(0, ry));
                        if (pm.IsAxisAligned)
                        {
                            var fx = Math.Abs(ex.X); var fy = Math.Abs(ey.Y);
                            var cc = Flip(cpt);
                            shapes.Add(new Shape { Kind = ShapeKind.Ellipse, Box = new Rect(cc.X - fx, cc.Y - fy, 2 * fx, 2 * fy), Line = LineOf(_s, pos) });
                        }
                        else
                        {
                            var poly = new Sub { Closed = true };
                            for (var a = 0; a < 72; a++) { var t = a * Math.PI / 36; poly.Points.Add(cpt + ex * Math.Cos(t) + ey * Math.Sin(t)); }
                            subs.Add(poly);
                        }
                        continue;
                    }
                    if (StartsWord(body, i, "arc"))
                    {
                        i += 3;
                        Ws(body, ref i);
                        double a0, a1, rx, ry;
                        if (i < body.Length && body[i] == '[')
                        {
                            var o = SplitOptions(Group(body, ref i, '[', ']') ?? "");
                            string? Get(string key) => o.FirstOrDefault(x => x.Key == key).Value;
                            a0 = Num(Get("start angle")) ?? throw new Unsupported("The arc needs a start angle.", "Der Bogen braucht einen Startwinkel.");
                            a1 = Num(Get("end angle")) ?? (Num(Get("delta angle")) is { } da ? a0 + da : throw new Unsupported("The arc needs an end angle.", "Der Bogen braucht einen Endwinkel."));
                            rx = LengthMm(Get("x radius") ?? Get("radius"), Math.Abs(pu.X)) ?? throw new Unsupported("The arc needs a radius.", "Der Bogen braucht einen Radius.");
                            ry = LengthMm(Get("y radius") ?? Get("radius"), Math.Abs(pu.Y)) ?? rx;
                        }
                        else if (i < body.Length && body[i] == '(')
                        {
                            var g = (Group(body, ref i, '(', ')') ?? "").Split(':');
                            if (g.Length != 3) throw new Unsupported("The arc cannot be read.", "Der Bogen ist nicht lesbar.");
                            a0 = Num(g[0]) ?? double.NaN; a1 = Num(g[1]) ?? double.NaN;
                            var radii = Regex.Split(g[2], @"\s+and\s+");
                            rx = LengthMm(radii[0].Trim(), Math.Abs(pu.X)) ?? double.NaN;
                            ry = radii.Length > 1 ? LengthMm(radii[1].Trim(), Math.Abs(pu.Y)) ?? rx : rx;
                            if (!Finite(a0) || !Finite(a1) || !Finite(rx)) throw new Unsupported("The arc cannot be read.", "Der Bogen ist nicht lesbar.");
                        }
                        else throw new Unsupported("The arc cannot be read.", "Der Bogen ist nicht lesbar.");
                        if (at is null || cur is null) throw new Unsupported("An arc needs a start point.", "Ein Bogen braucht einen Startpunkt.");
                        // Arc in untransformed units around the centre, then transformed by the linear part.
                        var lin = pm;
                        Vec2 Off(double deg) => lin.ApplyLinear(new Vec2(rx * Math.Cos(deg * Math.PI / 180), ry * Math.Sin(deg * Math.PI / 180)));
                        var centre = at.Value - Off(a0);
                        var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(a1 - a0) / 5));
                        for (var s = 1; s <= n; s++) cur.Points.Add(centre + Off(a0 + (a1 - a0) * s / n));
                        prev = at; at = basePt = cur.Points[cur.Points.Count - 1];
                        continue;
                    }
                    if (StartsWord(body, i, "to"))
                    {
                        i += 2;
                        Ws(body, ref i);
                        if (i < body.Length && body[i] == '[')
                        {
                            var o = Group(body, ref i, '[', ']') ?? "";
                            if (Regex.IsMatch(o, @"\b(bend|out|in|looseness|loop)\b")) throw new Unsupported("Curved “to” paths (bend, in, out) are not read.", "Gebogene „to“-Pfade (bend, in, out) werden nicht gelesen.");
                        }
                        Ws(body, ref i);
                        var rel = Plus(body, ref i);
                        var g = Group(body, ref i, '(', ')') ?? throw new Unsupported("A coordinate is missing after to.", "Nach to fehlt eine Koordinate.");
                        var p = Coord(g, pm, pu, rel, basePt);
                        if (rel != 1) basePt = p;
                        if (at is null || cur is null) { cur = new Sub(); subs.Add(cur); cur.Points.Add(p); at = p; continue; }
                        cur.Points.Add(p);
                        prev = at; at = p;
                        continue;
                    }
                    if (StartsWord(body, i, "node"))
                    {
                        i += 4;
                        var (text, nodeOpts) = InlineNode(body, ref i);
                        var place = at ?? throw new Unsupported("A node needs a position.", "Ein Knoten braucht eine Position.");
                        var nodeKeys = nodeOpts.Select(x => x.Key).ToList();
                        if (prev is { } pv && at is { } av && (nodeKeys.Contains("midway") || nodeKeys.Contains("pos") || nodeKeys.Contains("near start") || nodeKeys.Contains("near end") || nodeKeys.Contains("at start")))
                        {
                            var t = nodeKeys.Contains("midway") ? 0.5 : nodeKeys.Contains("near start") ? 0.25 : nodeKeys.Contains("near end") ? 0.75 : nodeKeys.Contains("at start") ? 0 : Num(nodeOpts.First(x => x.Key == "pos").Value) ?? 1;
                            place = pv + (av - pv) * t;
                        }
                        AddNode(text, nodeOpts, place, pm, pu, look, LineOf(_s, pos));
                        continue;
                    }
                    if (StartsWord(body, i, "coordinate"))
                    {
                        i += 10;
                        Ws(body, ref i);
                        TryOptionsIn(body, ref i);
                        Ws(body, ref i);
                        var name = Group(body, ref i, '(', ')');
                        if (name is not null && at is { } a) _named[name.Trim()] = a;
                        continue;
                    }
                    if (StartsWord(body, i, "grid") || StartsWord(body, i, "plot") || StartsWord(body, i, "sin") || StartsWord(body, i, "cos") || StartsWord(body, i, "parabola") || StartsWord(body, i, "edge"))
                        throw new Unsupported("Path operations grid, plot, sin, cos, parabola and edge are not read.", "Die Pfadoperationen grid, plot, sin, cos, parabola und edge werden nicht gelesen.");
                    if (c == '{' ) { Group(body, ref i, '{', '}'); continue; }
                    throw new Unsupported("The path cannot be read near “" + body.Substring(i, Math.Min(12, body.Length - i)) + "”.", "Der Pfad ist bei „" + body.Substring(i, Math.Min(12, body.Length - i)) + "“ nicht lesbar.");
                }
                // Subpaths become lines and polylines (y flipped to page coordinates).
                var line = LineOf(_s, pos);
                var open = subs.Where(s => s.Points.Count >= 2).ToList();
                foreach (var s in open)
                {
                    var pts = s.Points.Select(Flip).ToList();
                    if (s.Closed && pts.Count > 2 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-6) pts.RemoveAt(pts.Count - 1);
                    shapes.Add(new Shape { Kind = pts.Count == 2 && !s.Closed ? ShapeKind.Line : ShapeKind.Polyline, Points = pts, Closed = s.Closed && pts.Count > 2, Line = line });
                }
                foreach (var sh in shapes)
                {
                    if (sh.Kind == ShapeKind.Text) continue;
                    sh.Stroke = look.Draw ? look.Stroke ?? "#000000" : null;
                    sh.Width = look.Width;
                    sh.Dash = look.Dash;
                    sh.Opacity = look.Opacity;
                    var closed = sh.Kind is ShapeKind.Rect or ShapeKind.Ellipse || sh.Closed;
                    if (look.FillOn && closed)
                    {
                        if (look.Pattern is { } pat)
                        {
                            var pc = look.PatternColor ?? "#000000";
                            sh.Fill = pat.Kind == FillKind.Hatch && pat.Hatch is { } h ? Fill.Hatched(h with { Color = pc }, look.FillColor) : pat;
                        }
                        else sh.Fill = Fill.Solid(look.FillColor ?? "#000000");
                        if (look.FillOpacity is { } fo && !look.Draw) sh.Opacity = fo;
                    }
                    if (sh.Stroke is null && sh.Fill is null) sh.Stroke = null;
                }
                // Arrow tips: at the start of the first and the end of the last open subpath.
                var lines = shapes.Where(s => s.Kind is ShapeKind.Line or ShapeKind.Polyline && !s.Closed).ToList();
                if (lines.Count > 0 && look.Draw)
                {
                    lines[0].HeadStart = look.Start;
                    lines[lines.Count - 1].HeadEnd = look.End;
                }
                foreach (var sh in shapes)
                {
                    if (sh.Kind != ShapeKind.Text && sh.Stroke is null && sh.Fill is null) continue; // \path without draw or fill
                    if (sh.Kind == ShapeKind.Rect && rounded > 0) sh.Radius = rounded;
                    _d.Shapes.Add(sh);
                }
            }

            private static Vec2 Flip(Vec2 p) => new(p.X, -p.Y);

            private static bool Starts(string s, int i, string w) => string.CompareOrdinal(s, i, w, 0, w.Length) == 0;

            private static bool StartsWord(string s, int i, string w) => Starts(s, i, w) && (i + w.Length >= s.Length || !char.IsLetter(s[i + w.Length]));

            private static int Plus(string s, ref int i)
            {
                var n = 0;
                while (i < s.Length && s[i] == '+') { n++; i++; }
                Ws(s, ref i);
                return n;
            }

            private static void TryOptionsIn(string s, ref int i)
            {
                Ws(s, ref i);
                if (i < s.Length && s[i] == '[') Group(s, ref i, '[', ']');
            }

            /// <summary>A coordinate, relative to <paramref name="at"/> for + and ++ (paper mm, y up).</summary>
            private Vec2 Coord(string g, Matrix2D m, Units u, int rel, Vec2? at)
            {
                if (rel == 0) return Point(g, m, u);
                var raw = Raw(g.Trim(), u) ?? throw new Unsupported("The relative coordinate (" + g + ") cannot be read.", "Die relative Koordinate (" + g + ") ist nicht lesbar.");
                return (at ?? Vec2.Zero) + m.ApplyLinear(raw);
            }

            // ---------------------------------------------------------- nodes

            private (string Text, List<(string Key, string? Value)> Opts) InlineNode(string s, ref int i)
            {
                var opts = new List<(string, string?)>();
                while (true)
                {
                    Ws(s, ref i);
                    if (i >= s.Length) break;
                    if (s[i] == '[') { opts.AddRange(SplitOptions(Group(s, ref i, '[', ']') ?? "")); continue; }
                    if (s[i] == '(')
                    {
                        // Node name.
                        var name = Group(s, ref i, '(', ')');
                        if (name is not null) opts.Add(("name", name.Trim()));
                        continue;
                    }
                    if (StartsWord(s, i, "at"))
                    {
                        i += 2;
                        Ws(s, ref i);
                        var c = Group(s, ref i, '(', ')');
                        if (c is not null) opts.Add(("at", c));
                        continue;
                    }
                    break;
                }
                Ws(s, ref i);
                var text = Group(s, ref i, '{', '}') ?? throw new Unsupported("The node text is missing.", "Der Knotentext fehlt.");
                return (text, opts);
            }

            private void NodeCommand(int j, int end, Matrix2D m, Units u, int pos)
            {
                var s = _s.Substring(j, end - j);
                var i = 0;
                var (text, opts) = InlineNode(s, ref i);
                Vec2 place;
                if (opts.FirstOrDefault(x => x.Key == "at").Value is { } at) place = Point(at, m, u);
                else place = m.Apply(Vec2.Zero);
                var rest = s.Substring(i).Trim();
                if (rest.Length > 0) Skip(pos, rest, "Path operations after a node are not read.", "Pfadoperationen nach einem Knoten werden nicht gelesen.");
                var look = Style("node", opts, pos);
                AddNode(text, opts, place, m, u, look, LineOf(_s, pos));
            }

            private void AddNode(string tex, List<(string Key, string? Value)> opts, Vec2 place, Matrix2D m, Units u, Look pathLook, int line)
            {
                var look = Style("node", opts, 0);
                var plain = PlainText(tex, out var bold, out var italic);
                string? Get(string key) => opts.LastOrDefault(x => x.Key == key).Value;
                bool Has(string key) => opts.Any(x => x.Key == key);
                if (Get("name") is { } name) _named[name] = place;
                var size = FontMm(Get("font"), 10 * 25.4 / 72.27);
                if (Regex.IsMatch(tex, @"\\(tiny|scriptsize|footnotesize|small|normalsize|large|Large|LARGE|huge|Huge)\b")) size = FontMm(tex, size);
                // Anchor: explicit, or from above/below/left/right (the node sits on that side of the point).
                var anchor = Get("anchor") ?? "";
                double dx = 0, dy = 0;
                foreach (var (k, v) in opts)
                {
                    var dist = v is null ? 0.5 * 25.4 / 72.27 * 2 : LengthMm(v, 1) ?? 1;
                    switch (k)
                    {
                        case "above": anchor = "south"; dy = dist; break;
                        case "below": anchor = "north"; dy = -dist; break;
                        case "left": anchor = "east"; dx = -dist; break;
                        case "right": anchor = "west"; dx = dist; break;
                        case "above left": anchor = "south east"; dx = -dist; dy = dist; break;
                        case "above right": anchor = "south west"; dx = dist; dy = dist; break;
                        case "below left": anchor = "north east"; dx = -dist; dy = -dist; break;
                        case "below right": anchor = "north west"; dx = dist; dy = -dist; break;
                    }
                }
                var lines = Math.Max(1, plain.Split('\n').Length);
                var h = size * 1.2 * lines;
                var p = Flip(place + new Vec2(dx, dy));
                // Shift the point to the vertical middle of the text (y down: north = top edge).
                if (anchor.Contains("north")) p = new Vec2(p.X, p.Y + h / 2);
                else if (anchor.Contains("south") || anchor.Contains("base")) p = new Vec2(p.X, p.Y - h / 2);
                var ta = anchor.Contains("west") ? TextAnchor.Start : anchor.Contains("east") ? TextAnchor.End : TextAnchor.Middle;
                var rot = Num(Get("rotate")) is { } r ? -r - m.RotationDegrees : -m.RotationDegrees;
                var frame = Has("draw") || Get("draw") is { } dv && dv != "none";
                var shape = new Shape
                {
                    Kind = ShapeKind.Text, Points = { p }, Text = plain, TextSize = Math.Round(size, 3), Anchor = ta, Bold = bold, Italic = italic,
                    Rotation = Math.Abs(rot) < 1e-9 ? 0 : rot, Frame = frame, TextColor = look.TextColor ?? pathLook.TextColor, Line = line,
                    Stroke = frame ? look.Stroke ?? "#000000" : null, Width = look.Width, Dash = look.Dash,
                    Fill = look.FillOn && look.FillColor is { } fc ? Fill.Solid(fc) : null,
                };
                if (Has("circle") && frame)
                {
                    // A circle node: the circle as an ellipse shape, the text without frame.
                    var b = TextBox(shape);
                    var d = Math.Max(b.W, b.H) + size * 0.3;
                    var c = new Vec2(b.X + b.W / 2, b.Y + b.H / 2);
                    _d.Shapes.Add(new Shape { Kind = ShapeKind.Ellipse, Box = new Rect(c.X - d / 2, c.Y - d / 2, d, d), Stroke = shape.Stroke, Width = shape.Width, Dash = shape.Dash, Fill = shape.Fill, Line = line });
                    shape.Frame = false;
                    shape.Stroke = null;
                    shape.Fill = null;
                }
                if (plain.Length == 0 && !shape.Frame) return;
                _d.Shapes.Add(shape);
            }
        }

        // ------------------------------------------------------------------ elements

        /// <summary>
        /// Elements for the shapes: lines and arrows (<c>ann.line</c>, <c>ann.arrow</c> with heads), polylines
        /// (<c>ann.polyline</c>, closed or with heads), rectangles and ellipses (<c>basic.rect</c>, <c>basic.ellipse</c>) and
        /// texts (<c>ann.text</c>) with colours, widths, dashes and fills as style overrides. Real size when it fits into the
        /// page (inset by a tenth), else fitted; centred on the page.
        /// </summary>
        public static IReadOnlyList<SketchObject> Elements(Registry registry, Drawing drawing, Size2 page)
        {
            var created = new List<SketchObject>();
            if (drawing.IsEmpty) return created;
            var b = drawing.Bounds();
            var w = Math.Max(b.W, 1e-6); var h = Math.Max(b.H, 1e-6);
            var fits = b.W <= page.W * 0.9 && b.H <= page.H * 0.9;
            var s = fits ? 1.0 : Math.Min(page.W * 0.8 / w, page.H * 0.8 / h);
            var offset = new Vec2(page.W / 2 - (b.X + b.W / 2) * s, page.H / 2 - (b.Y + b.H / 2) * s);
            Vec2 M(Vec2 p) => new(Math.Round(p.X * s + offset.X, 4), Math.Round(p.Y * s + offset.Y, 4));
            LibraryItem? Item(string type) => registry.Library.FirstOrDefault(i => i.TypeId == type && i.Params is not { Count: > 0 } && i.Style is null)
                ?? registry.Library.FirstOrDefault(i => i.TypeId == type);
            var line = Item("ann.line"); var arrow = Item("ann.arrow"); var poly = Item("ann.polyline"); var rect = Item("basic.rect");
            var ellipse = Item("basic.ellipse"); var text = Item("ann.text");
            string Head(ArrowHead a) { var n = a.ToString(); return char.ToLowerInvariant(n[0]) + n.Substring(1); }
            Style? StyleOf(Shape sh, bool closed)
            {
                var st = new Style
                {
                    Stroke = sh.Stroke ?? "none",
                    StrokeWidth = sh.Width is { } wd ? Math.Round(Math.Max(0.05, wd * Math.Min(1, s)), 3) : null,
                    Dash = sh.Dash == DashStyle.Solid ? null : sh.Dash,
                    Fill = closed ? sh.Fill ?? Fill.None : null,
                    Opacity = sh.Opacity is { } o && o < 1 ? o : null,
                };
                return st;
            }
            foreach (var sh in drawing.Shapes)
            {
                switch (sh.Kind)
                {
                    case ShapeKind.Line when line is not null || arrow is not null:
                        {
                            var heads = sh.HeadStart != ArrowHead.None || sh.HeadEnd != ArrowHead.None;
                            var it = heads ? (arrow ?? line)! : (line ?? arrow)!;
                            var o = registry.Create(it, M(sh.Points[0])) with { Points = sh.Points.Select(M).ToList(), Attach = null, Style = StyleOf(sh, false) };
                            if (it.TypeId != "ann.line" || sh.HeadStart != ArrowHead.None || sh.HeadEnd != ArrowHead.None)
                                o = o.WithParam("headStart", Head(sh.HeadStart)).WithParam("headEnd", Head(sh.HeadEnd));
                            created.Add(o);
                            break;
                        }
                    case ShapeKind.Line:
                    case ShapeKind.Polyline:
                        {
                            if (poly is null) break;
                            var pts = sh.Points.Select(M).ToList();
                            var thin = new List<Vec2> { pts[0] };
                            foreach (var p in pts.Skip(1)) if (p.DistanceTo(thin[thin.Count - 1]) >= 0.05) thin.Add(p);
                            if (thin.Count < 2) break;
                            var o = registry.Create(poly, thin[0]) with { Points = thin, Attach = null, Style = StyleOf(sh, sh.Closed && thin.Count > 2) };
                            if (sh.Closed && thin.Count > 2) o = o.WithParam("closed", true);
                            if (sh.HeadStart != ArrowHead.None || sh.HeadEnd != ArrowHead.None) o = o.WithParam("headStart", Head(sh.HeadStart)).WithParam("headEnd", Head(sh.HeadEnd));
                            created.Add(o);
                            break;
                        }
                    case ShapeKind.Rect when rect is not null:
                    case ShapeKind.Ellipse when ellipse is not null:
                        {
                            var item = sh.Kind == ShapeKind.Rect ? rect! : ellipse!;
                            var c = M(new Vec2(sh.Box.X + sh.Box.W / 2, sh.Box.Y + sh.Box.H / 2));
                            var box = registry.Create(item, c);
                            box = box with { Transform = box.Transform with { X = c.X, Y = c.Y }, Size = new Size2(Math.Max(0.1, Math.Round(sh.Box.W * s, 4)), Math.Max(0.1, Math.Round(sh.Box.H * s, 4))), Style = StyleOf(sh, true) };
                            if (sh.Kind == ShapeKind.Rect && sh.Radius > 0) box = box.WithParam("radius", Math.Round(sh.Radius * s, 3));
                            created.Add(box);
                            break;
                        }
                    case ShapeKind.Text when text is not null && !string.IsNullOrWhiteSpace(sh.Text):
                        {
                            var th = Math.Max(1, Math.Min(50, Math.Round(sh.TextSize * s, 2)));
                            var scaled = new Shape { Kind = ShapeKind.Text, Text = sh.Text, TextSize = th, Anchor = sh.Anchor, Frame = sh.Frame, Points = { M(sh.Points[0]) } };
                            var bx = TextBox(scaled);
                            var t = registry.Create(text, new Vec2(bx.X + bx.W / 2, bx.Y + bx.H / 2));
                            t = t with
                            {
                                Transform = t.Transform with { X = Math.Round(bx.X + bx.W / 2, 4), Y = Math.Round(bx.Y + bx.H / 2, 4), Rotation = Math.Round(sh.Rotation, 3) },
                                Size = new Size2(Math.Round(bx.W, 3), Math.Round(bx.H, 3)),
                                Text = sh.Text,
                                Style = sh.TextColor is { } tc && tc != "#000000" || sh.Frame && sh.Stroke is { } fs && fs != "#000000"
                                    ? new Style { TextColor = sh.TextColor is { } tc2 && tc2 != "#000000" ? tc2 : null, Stroke = sh.Frame && sh.Stroke != "#000000" ? sh.Stroke : null }
                                    : null,
                            };
                            t = t.WithParam("textSize", th)
                                .WithParam("align", sh.Anchor switch { TextAnchor.Start => "left", TextAnchor.End => "right", _ => "center" });
                            if (sh.Bold) t = t.WithParam("bold", true);
                            if (sh.Italic) t = t.WithParam("italic", true);
                            if (sh.Frame) t = t.WithParam("frame", true);
                            created.Add(t);
                            break;
                        }
                }
            }
            return created;
        }
    }
}
