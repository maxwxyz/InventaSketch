using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace InventaSketch.Core
{
    /// <summary>One line of a calculation sheet: a known value of an element with how it came about.</summary>
    public sealed record CalcSheetRow(string Element, string Quantity, string Symbol, string Value, string Unit, string Range, string Formula, string Origin, string Source);

    /// <summary>
    /// The calculation sheet (Calc 6, docs/en/design/engineering-calculations.md section 9 "Calc 6"): every known value of
    /// the chosen elements with quantity, symbol, value, unit, tolerance range, formula, origin (entered, derived, linked,
    /// default) and source – as CSV, Markdown, LaTeX or the cells of an annotation table on the slide. Built from a solved
    /// <see cref="ValueTable"/>; nothing is changed.
    /// </summary>
    public static class CalcSheet
    {
        /// <summary>The rows for <paramref name="ids"/> in that order (elements without known values are left out).</summary>
        public static IReadOnlyList<CalcSheetRow> Rows(ValueTable table, Registry registry, IEnumerable<string> ids, Lang lang, int digits = 3)
        {
            var rows = new List<CalcSheetRow>();
            foreach (var id in ids)
            {
                if (!table.Objects.TryGetValue(id, out var o)) continue;
                var type = registry.Type(o.Type);
                var name = type?.Name.Get(lang) ?? o.Type;
                var designator = o.Str("ref", "").Trim();
                var element = designator.Length > 0 ? designator + " (" + name + ")" : name;
                foreach (var v in table.Of(id))
                {
                    if (v.Value is not double si) continue;
                    // One unit per row (with the prefix the value is shown in), the range in the same unit.
                    var (_, unit) = ValueFormat.Scale(si, table.UnitOf(v), digits);
                    string Num(double x) => StripUnit(ValueFormat.Format(x, unit, lang, digits), unit.Symbol);
                    var number = Num(si);
                    var range = v.Interval is { } iv ? Num(iv.Lo) + " … " + Num(iv.Hi) : "";
                    var formula = v.Provenance is Provenance.Derived or Provenance.Linked ? table.FormulaOf(v, lang) ?? "" : v.Text ?? "";
                    var source = v.Via?.Relation.Source is { } s ? "[" + s + "]" : "";
                    rows.Add(new CalcSheetRow(element, v.Def.Name.Get(lang), v.Def.Symbol.Get(lang), number, unit.Symbol, range, formula, Origin(v.Provenance, lang), source));
                }
            }
            return rows;
        }

        /// <summary>Column titles in the sheet's language.</summary>
        public static IReadOnlyList<string> Header(Lang lang) => lang == Lang.De
            ? new[] { "Element", "Größe", "Zeichen", "Wert", "Einheit", "Bereich", "Formel", "Herkunft", "Quelle" }
            : new[] { "Element", "Quantity", "Symbol", "Value", "Unit", "Range", "Formula", "Origin", "Source" };

        private static IEnumerable<string> Cells(CalcSheetRow r) => new[] { r.Element, r.Quantity, r.Symbol, r.Value, r.Unit, r.Range, r.Formula, r.Origin, r.Source };

        /// <summary>CSV (RFC 4180 quoting; ";" as separator for German, "," otherwise, like spreadsheet defaults).</summary>
        public static string Csv(IReadOnlyList<CalcSheetRow> rows, Lang lang)
        {
            var sep = lang == Lang.De ? ";" : ",";
            static string Q(string s) => s.IndexOfAny(new[] { ',', ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            var sb = new StringBuilder();
            sb.Append(string.Join(sep, Header(lang).Select(Q))).Append("\r\n");
            foreach (var r in rows) sb.Append(string.Join(sep, Cells(r).Select(Q))).Append("\r\n");
            return sb.ToString();
        }

        /// <summary>A Markdown table, followed by the cited sources.</summary>
        public static string Markdown(IReadOnlyList<CalcSheetRow> rows, Lang lang)
        {
            static string E(string s) => s.Replace("|", "\\|");
            var sb = new StringBuilder();
            var head = Header(lang);
            sb.Append("| ").Append(string.Join(" | ", head)).Append(" |\n");
            sb.Append('|').Append(string.Concat(head.Select(_ => "---|"))).Append('\n');
            foreach (var r in rows) sb.Append("| ").Append(string.Join(" | ", Cells(r).Select(E))).Append(" |\n");
            AppendSources(sb, rows, lang, id => "- [" + id + "] ");
            return sb.ToString();
        }

        /// <summary>A LaTeX tabular (booktabs), followed by the cited sources as an itemize list.</summary>
        public static string Latex(IReadOnlyList<CalcSheetRow> rows, Lang lang)
        {
            static string E(string s)
            {
                var sb = new StringBuilder();
                foreach (var ch in s)
                    sb.Append(ch switch
                    {
                        '\\' => "\\textbackslash{}", '&' => "\\&", '%' => "\\%", '$' => "\\$", '#' => "\\#", '_' => "\\_",
                        '{' => "\\{", '}' => "\\}", '~' => "\\textasciitilde{}", '^' => "\\textasciicircum{}", _ => ch.ToString(),
                    });
                return sb.ToString();
            }
            var sb2 = new StringBuilder();
            sb2.Append("\\begin{tabular}{lllrllll l}\n\\toprule\n");
            sb2.Append(string.Join(" & ", Header(lang).Select(E))).Append(" \\\\\n\\midrule\n");
            foreach (var r in rows) sb2.Append(string.Join(" & ", Cells(r).Select(E))).Append(" \\\\\n");
            sb2.Append("\\bottomrule\n\\end{tabular}\n");
            var cited = Cited(rows).ToList();
            if (cited.Count > 0)
            {
                sb2.Append("\\begin{itemize}\n");
                foreach (var id in cited) sb2.Append("\\item[").Append(E("[" + id + "]")).Append("] ").Append(E(ValueSources.Get(id)!.Get(lang))).Append('\n');
                sb2.Append("\\end{itemize}\n");
            }
            return sb2.ToString();
        }

        /// <summary>
        /// The sheet as the <c>cells</c> parameter of an annotation table ("a|b\nc|d"): element, symbol, value with unit,
        /// formula and source – the columns that fit on a slide.
        /// </summary>
        public static string TableCells(IReadOnlyList<CalcSheetRow> rows, Lang lang)
        {
            static string C(string s) => s.Replace("|", "/").Replace("\n", " ");
            var head = lang == Lang.De ? new[] { "Element", "Zeichen", "Wert", "Formel", "Quelle" } : new[] { "Element", "Symbol", "Value", "Formula", "Source" };
            var lines = new List<string> { string.Join("|", head) };
            foreach (var r in rows)
            {
                var value = (r.Value + (r.Unit.Length > 0 ? " " + r.Unit : "")).Trim() + (r.Range.Length > 0 ? " (" + r.Range + ")" : "");
                lines.Add(string.Join("|", new[] { r.Element, r.Symbol, value, r.Formula, r.Source }.Select(C)));
            }
            return string.Join("\n", lines);
        }

        /// <summary>The source ids the rows cite, in order of first use, known to <see cref="ValueSources"/>.</summary>
        public static IEnumerable<string> Cited(IEnumerable<CalcSheetRow> rows)
            => rows.Select(r => r.Source.Trim('[', ']')).Where(s => s.Length > 0 && ValueSources.Get(s) is not null).Distinct();

        private static void AppendSources(StringBuilder sb, IReadOnlyList<CalcSheetRow> rows, Lang lang, Func<string, string> prefix)
        {
            var cited = Cited(rows).ToList();
            if (cited.Count == 0) return;
            sb.Append('\n').Append(lang == Lang.De ? "Quellen:" : "Sources:").Append('\n');
            foreach (var id in cited) sb.Append(prefix(id)).Append(ValueSources.Get(id)!.Get(lang)).Append('\n');
        }

        private static string Origin(Provenance p, Lang lang) => p switch
        {
            Provenance.Entered => lang == Lang.De ? "eingegeben" : "entered",
            Provenance.Derived => lang == Lang.De ? "berechnet" : "derived",
            Provenance.Linked => lang == Lang.De ? "übernommen" : "linked",
            Provenance.Default => lang == Lang.De ? "Vorgabe" : "default",
            _ => "",
        };

        private static string StripUnit(string formatted, string unit)
        {
            if (unit.Length == 0) return formatted.Trim();
            var s = formatted.TrimEnd();
            if (s.EndsWith(unit, StringComparison.Ordinal)) s = s.Substring(0, s.Length - unit.Length);
            return s.Trim().TrimEnd('\u00a0', '\u202f').Trim();
        }
    }
}
