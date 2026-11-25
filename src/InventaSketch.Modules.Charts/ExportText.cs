using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Numbers and texts of the code exports: numbers always in invariant culture (a German decimal comma never reaches the
    /// code), Python string literals that are safe for matplotlib's mathtext, and LaTeX text. A subscript written as
    /// "t_krit" or "f_c" becomes a math subscript in both.
    /// </summary>
    internal static class ExportText
    {
        /// <summary>A number in invariant culture with up to 8 significant digits ("nan" for NaN).</summary>
        public static string N(double v)
        {
            if (double.IsNaN(v)) return "nan";
            if (double.IsPositiveInfinity(v)) return "inf";
            if (double.IsNegativeInfinity(v)) return "-inf";
            if (Math.Abs(v) < 1e-300) return "0";
            var a = Math.Abs(v);
            string s = a >= 1e-4 && a < 1e12
                ? Math.Round(v, Math.Max(0, Math.Min(12, 7 - (int)Math.Floor(Math.Log10(a))))).ToString("0.############", CultureInfo.InvariantCulture)
                : v.ToString("0.#######e+0", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        /// <summary>Millimetres to points.</summary>
        public static string Pt(double mm) => N(Math.Round(mm * ExportBuilder.PT, 2));

        private static readonly Regex Subscript = new(@"(?<![\p{L}\p{N}_])([\p{L}])_([\p{L}\p{N}]+)(?![\p{L}\p{N}_])", RegexOptions.CultureInvariant);

        /// <summary>Splits a text into plain pieces and subscript tokens ("t_krit" → base "t", index "krit").</summary>
        private static IEnumerable<(string Text, string? Base, string? Index)> Pieces(string text)
        {
            var pos = 0;
            foreach (Match m in Subscript.Matches(text))
            {
                if (m.Index > pos) yield return (text.Substring(pos, m.Index - pos), null, null);
                yield return (m.Value, m.Groups[1].Value, m.Groups[2].Value);
                pos = m.Index + m.Length;
            }
            if (pos < text.Length) yield return (text.Substring(pos), null, null);
        }

        private static bool AllDigits(string s) => s.All(char.IsDigit);

        // ------------------------------------------------------------------ Python / matplotlib

        /// <summary>A Python string literal of a raw value (backslashes, quotes and line breaks escaped).</summary>
        public static string PyLit(string s)
        {
            var sb = new StringBuilder("'");
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\x").Append(((int)ch).ToString("x2", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('\'').ToString();
        }

        /// <summary>
        /// A Python string literal of a chart text, safe for matplotlib's mathtext: "$" is escaped (no accidental math
        /// mode); subscripts "t_krit" become "$t_{\mathrm{krit}}$".
        /// </summary>
        public static string Py(string text)
        {
            var sb = new StringBuilder();
            foreach (var (t, b, i) in Pieces(text ?? ""))
            {
                if (b is null) sb.Append(t.Replace("$", "\\$"));
                else sb.Append('$').Append(MathBase(b, false)).Append("_{").Append(AllDigits(i!) ? i : "\\mathrm{" + i + "}").Append("}$");
            }
            return PyLit(sb.ToString());
        }

        private static string MathBase(string b, bool latex) => b switch
        {
            "η" => "\\eta",
            "φ" => "\\varphi",
            "Δ" => "\\Delta",
            "ω" => "\\omega",
            "τ" => "\\tau",
            "μ" or "µ" => "\\mu",
            _ => b,
        };

        /// <summary>A Python list of numbers, wrapped after about 100 characters.</summary>
        public static string PyList(IEnumerable<double> values, string indent)
        {
            var sb = new StringBuilder("[");
            var line = 1;
            var first = true;
            foreach (var v in values)
            {
                var s = N(v);
                if (!first) { sb.Append(','); line++; }
                if (line + s.Length > 100) { sb.Append('\n').Append(indent).Append("    "); line = indent.Length + 4; }
                else if (!first) { sb.Append(' '); line++; }
                sb.Append(s); line += s.Length;
                first = false;
            }
            return sb.Append(']').ToString();
        }

        // ------------------------------------------------------------------ LaTeX

        private static readonly Dictionary<char, string> TexChars = new()
        {
            ['\\'] = "\\textbackslash{}", ['{'] = "\\{", ['}'] = "\\}", ['$'] = "\\$", ['&'] = "\\&", ['#'] = "\\#", ['%'] = "\\%",
            ['_'] = "\\_", ['^'] = "\\textasciicircum{}", ['~'] = "\\textasciitilde{}", ['<'] = "\\textless{}", ['>'] = "\\textgreater{}",
            ['|'] = "\\textbar{}",
            ['°'] = "\\textdegree{}", ['²'] = "\\textsuperscript{2}", ['³'] = "\\textsuperscript{3}", ['·'] = "\\textperiodcentered{}",
            ['µ'] = "\\textmu{}", ['μ'] = "\\textmu{}", ['±'] = "\\textpm{}", ['×'] = "\\texttimes{}", ['–'] = "--", ['—'] = "---",
            ['−'] = "\\textminus{}", ['…'] = "\\dots{}", ['€'] = "\\texteuro{}", ['“'] = "``", ['”'] = "''", ['„'] = ",,", ['‘'] = "`", ['’'] = "'",
            ['η'] = "$\\eta$", ['φ'] = "$\\varphi$", ['Δ'] = "$\\Delta$", ['ω'] = "$\\omega$", ['τ'] = "$\\tau$", ['α'] = "$\\alpha$",
            ['β'] = "$\\beta$", ['γ'] = "$\\gamma$", ['δ'] = "$\\delta$", ['ε'] = "$\\varepsilon$", ['λ'] = "$\\lambda$", ['π'] = "$\\pi$",
            ['ρ'] = "$\\rho$", ['σ'] = "$\\sigma$", ['ϑ'] = "$\\vartheta$", ['θ'] = "$\\theta$", ['Ω'] = "$\\Omega$", ['≤'] = "$\\leq$",
            ['≥'] = "$\\geq$", ['≈'] = "$\\approx$", ['→'] = "$\\rightarrow$", ['∞'] = "$\\infty$", ['ζ'] = "$\\zeta$",
            [' '] = "~", [' '] = "\\,", [' '] = "\\,",
        };

        /// <summary>
        /// LaTeX text of a chart text: special characters escaped, Greek letters and symbols as LaTeX commands (pdfLaTeX
        /// safe), a dotted letter (V̇) as \dot, subscripts "t_krit" as $t_{\mathrm{krit}}$.
        /// </summary>
        public static string Tex(string text)
        {
            var sb = new StringBuilder();
            foreach (var (t, b, i) in Pieces((text ?? "").Replace("\r", "")))
            {
                if (b is null) { AppendTex(sb, t); continue; }
                sb.Append('$').Append(MathBase(b, true)).Append("_{").Append(AllDigits(i!) ? i : "\\mathrm{" + i + "}").Append("}$");
            }
            return sb.ToString();
        }

        private static void AppendTex(StringBuilder sb, string t)
        {
            for (var k = 0; k < t.Length; k++)
            {
                var ch = t[k];
                // A letter followed by a combining dot above (V̇ volume flow): \dot{V}.
                if (k + 1 < t.Length && t[k + 1] == '̇' && char.IsLetter(ch)) { sb.Append("$\\dot{").Append(ch).Append("}$"); k++; continue; }
                if (ch == '\n') { sb.Append("\\\\ "); continue; }
                if (TexChars.TryGetValue(ch, out var rep)) sb.Append(rep);
                else if (ch < 0x20) continue;
                else sb.Append(ch);
            }
        }
    }
}
