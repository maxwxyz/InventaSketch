using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>One data row: an optional text label (first field) and the numeric fields (NaN for empty or invalid cells).</summary>
    public sealed record DataRow(string? Label, IReadOnlyList<double> Values);

    /// <summary>
    /// A block of rows: lines between blank lines (or headers). <see cref="Name"/> comes from a single-word header line,
    /// <see cref="Columns"/> from a header line with several fields (Excel header row).
    /// </summary>
    public sealed class DataBlock
    {
        public string? Name { get; set; }
        public IReadOnlyList<string>? Columns { get; set; }
        public List<DataRow> Rows { get; } = new();

        /// <summary>True if any row starts with a text label (category data).</summary>
        public bool HasLabels => Rows.Any(r => r.Label is not null);

        /// <summary>Largest number of numeric fields in a row.</summary>
        public int ValueColumns => Rows.Count == 0 ? 0 : Rows.Max(r => r.Values.Count);

        /// <summary>Header name of value column <paramref name="k"/> (the label column, if any, is skipped).</summary>
        public string? ColumnName(int k)
        {
            if (Columns is null) return null;
            var i = HasLabels ? k + 1 : k;
            return i >= 0 && i < Columns.Count && Columns[i].Length > 0 ? Columns[i] : null;
        }
    }

    /// <summary>A named XY series (points in data coordinates, sorted as entered).</summary>
    public sealed record XySeries(string? Name, IReadOnlyList<Vec2> Points);

    /// <summary>Category data for bar charts: category labels and one value list per series (NaN = no bar).</summary>
    public sealed record CategorySet(IReadOnlyList<string> Categories, IReadOnlyList<(string? Name, IReadOnlyList<double> Values)> Series);

    /// <summary>
    /// Compact text format for chart data, typed in the inspector or pasted from a spreadsheet:
    /// <list type="bullet">
    /// <item>one data row per line (or rows separated by <c>|</c> in a single-line input);</item>
    /// <item>fields separated by tab (Excel paste), semicolon, whitespace, or comma (only when no other separator occurs;
    /// "1, 2" with a space after the comma is read as two fields);</item>
    /// <item>numbers in invariant culture or with a German decimal comma ("2,5"); thousands separators ("1.234,5",
    /// "1,234.5"), a unicode minus, a trailing "%" and exponents ("1e3") are accepted;</item>
    /// <item>a blank line starts a new series; a text-only line names the next series (one field) or the columns (several
    /// fields, e.g. an Excel header row); a field in double quotes is always text ("2025");</item>
    /// <item>lines starting with <c>#</c> or <c>//</c> are comments; empty or invalid cells are skipped.</item>
    /// </list>
    /// </summary>
    public static class ChartData
    {
        /// <summary>Parses the text into blocks of rows; never throws (unreadable cells become NaN, empty text gives no blocks).</summary>
        public static IReadOnlyList<DataBlock> Parse(string? text)
        {
            var blocks = new List<DataBlock>();
            if (string.IsNullOrWhiteSpace(text)) return blocks;
            var lines = text!.Replace("\r\n", "\n").Replace('\r', '\n').Replace('|', '\n').Split('\n');
            DataBlock? cur = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    if (cur is not null && cur.Rows.Count > 0) cur = null;
                    continue;
                }
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;
                var fields = SplitFields(line);
                if (fields.Count == 0) continue;
                var numbers = fields.Select(f => TryNumber(f, out var v) ? v : double.NaN).ToList();
                if (numbers.All(double.IsNaN))
                {
                    // Header: names the next series (one field) or the columns (several fields).
                    if (cur is null || cur.Rows.Count > 0) { cur = new DataBlock(); blocks.Add(cur); }
                    var texts = fields.Select(Unquote).ToList();
                    if (texts.Count(t => t.Length > 0) <= 1) cur.Name = texts.FirstOrDefault(t => t.Length > 0);
                    else cur.Columns = texts;
                    continue;
                }
                if (cur is null) { cur = new DataBlock(); blocks.Add(cur); }
                var label = double.IsNaN(numbers[0]) && Unquote(fields[0]).Length > 0 && !IsMissing(fields[0]) ? Unquote(fields[0]) : null;
                var values = label is null ? numbers : numbers.Skip(1).ToList();
                cur.Rows.Add(new DataRow(label, values));
            }
            return blocks.Where(b => b.Rows.Count > 0).ToList();
        }

        private static bool IsMissing(string field)
        {
            var t = field.Trim();
            return t.Length == 0 || t == "-" || t == "–" || t.Equals("NaN", StringComparison.OrdinalIgnoreCase) || t.Equals("n/a", StringComparison.OrdinalIgnoreCase);
        }

        private static string Unquote(string field)
        {
            var t = field.Trim();
            if (t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"') t = t.Substring(1, t.Length - 2).Replace("\"\"", "\"");
            return t.Trim();
        }

        /// <summary>Splits one line into fields (tab, semicolon, whitespace or comma; see the class remarks).</summary>
        public static IReadOnlyList<string> SplitFields(string line)
        {
            line = line.Trim();
            if (line.Length == 0) return Array.Empty<string>();
            if (line.IndexOf('\t') >= 0) return line.Split('\t').Select(f => f.Trim()).ToList();
            if (line.IndexOf(';') >= 0) return line.Split(';').Select(f => f.Trim()).ToList();
            if (line.IndexOf(", ", StringComparison.Ordinal) >= 0) return line.Split(',').Select(f => f.Trim()).ToList();
            if (line.Any(char.IsWhiteSpace)) return line.Split(new[] { ' ', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            // Only commas: comma separated values ("1,5" is x = 1, y = 5). Decimal commas need another separator.
            if (line.IndexOf(',') >= 0) return line.Split(',').Select(f => f.Trim()).ToList();
            return new[] { line };
        }

        /// <summary>
        /// Reads a number in invariant culture or with a German decimal comma; thousands separators, unicode minus, a
        /// trailing "%" and exponents are accepted. Quoted fields are text (false).
        /// </summary>
        public static bool TryNumber(string? token, out double value)
        {
            value = double.NaN;
            if (token is null) return false;
            var t = token.Trim();
            if (t.Length == 0 || t[0] == '"') return false;
            t = t.Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace("'", "").Replace(" ", "").Replace('−', '-');
            if (t.EndsWith("%", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);
            if (t.Length == 0) return false;
            var comma = t.LastIndexOf(',');
            var dot = t.LastIndexOf('.');
            if (comma >= 0 && dot >= 0)
            {
                // Both: the later one is the decimal separator.
                t = comma > dot ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
            }
            else if (comma >= 0)
            {
                t = t.Count(c => c == ',') == 1 ? t.Replace(',', '.') : t.Replace(",", "");
            }
            else if (dot >= 0 && t.Count(c => c == '.') > 1)
            {
                t = t.Replace(".", ""); // "1.234.567": German thousands separators
            }
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !Fmt.Finite(v)) return false;
            value = v;
            return true;
        }

        /// <summary>
        /// XY series of the data: per block, rows "x;y" (one series), "x;y1;y2;…" (one series per y column, names from the
        /// header row) or a single value per row (x = 1, 2, 3 …); rows with a text label use their row number as x.
        /// </summary>
        public static IReadOnlyList<XySeries> Xy(IReadOnlyList<DataBlock> blocks)
        {
            var result = new List<XySeries>();
            foreach (var b in blocks)
            {
                var cols = b.ValueColumns;
                if (cols == 0) continue;
                var labels = b.HasLabels;
                if (cols == 1 || labels)
                {
                    // Values only (or category rows): x is the row number.
                    for (var k = 0; k < cols; k++)
                    {
                        var pts = new List<Vec2>();
                        for (var i = 0; i < b.Rows.Count; i++)
                            if (k < b.Rows[i].Values.Count && Fmt.Finite(b.Rows[i].Values[k])) pts.Add(new Vec2(i + 1, b.Rows[i].Values[k]));
                        if (pts.Count > 0) result.Add(new XySeries(cols == 1 ? b.Name ?? b.ColumnName(0) : b.ColumnName(k) ?? b.Name, pts));
                    }
                    continue;
                }
                for (var k = 1; k < cols; k++)
                {
                    var pts = new List<Vec2>();
                    foreach (var r in b.Rows)
                        if (r.Values.Count > k && Fmt.Finite(r.Values[0]) && Fmt.Finite(r.Values[k])) pts.Add(new Vec2(r.Values[0], r.Values[k]));
                    if (pts.Count == 0) continue;
                    var name = cols == 2 ? b.Name ?? b.ColumnName(1) : b.ColumnName(k) ?? (b.Name is null ? null : b.Name + " " + Fmt.I(k));
                    result.Add(new XySeries(name, pts));
                }
            }
            return result;
        }

        /// <summary>
        /// Category data of the blocks: rows "label;v1;v2;…" (a series per value column) or "v" (categories 1, 2, 3 …);
        /// numeric first columns with further values are used as labels (years). Several blocks are several series,
        /// matched by category label.
        /// </summary>
        public static CategorySet Categories(IReadOnlyList<DataBlock> blocks, Lang lang = Lang.En)
        {
            var cats = new List<string>();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            var raw = new List<(string? Name, Dictionary<int, double> Values)>();
            int Cat(string label)
            {
                if (!index.TryGetValue(label, out var i)) { i = cats.Count; cats.Add(label); index[label] = i; }
                return i;
            }
            foreach (var b in blocks)
            {
                var cols = b.ValueColumns;
                if (cols == 0) continue;
                var labels = b.HasLabels;
                // Without labels and with several columns the first column labels the rows (e.g. years).
                var first = labels || cols == 1 ? 0 : 1;
                var series = Enumerable.Range(first, cols - first).Select(k => (Name: b.ColumnName(k), Values: new Dictionary<int, double>())).ToList();
                for (var i = 0; i < b.Rows.Count; i++)
                {
                    var r = b.Rows[i];
                    string label = r.Label
                        ?? (first == 1 && r.Values.Count > 0 && Fmt.Finite(r.Values[0]) ? Fmt.Num(r.Values[0], lang, 6) : Fmt.I(i + 1));
                    var c = Cat(label);
                    for (var k = first; k < cols; k++)
                        if (k < r.Values.Count && Fmt.Finite(r.Values[k])) series[k - first].Values[c] = r.Values[k];
                }
                if (series.Count == 1 && series[0].Name is null) series[0] = (b.Name, series[0].Values);
                raw.AddRange(series.Select(s => ((string?)s.Name, s.Values)));
            }
            var list = raw.Select(s => (s.Name, (IReadOnlyList<double>)Enumerable.Range(0, cats.Count).Select(i => s.Values.TryGetValue(i, out var v) ? v : double.NaN).ToList())).ToList();
            return new CategorySet(cats, list);
        }

        /// <summary>
        /// An axis range "min;max" ("0;100", "0..100", "0,5;2,5", "10 1000"); null for empty text, "auto" or anything that
        /// does not give two different finite numbers. The result is ordered (min &lt; max).
        /// </summary>
        public static (double Min, double Max)? Range(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text!.Trim();
            if (t.Equals("auto", StringComparison.OrdinalIgnoreCase)) return null;
            t = t.Replace("..", ";").Replace("…", ";");
            var fields = SplitFields(t);
            var nums = fields.Select(f => TryNumber(f, out var v) ? v : double.NaN).Where(Fmt.Finite).ToList();
            if (nums.Count != 2 || Math.Abs(nums[0] - nums[1]) < 1e-300) return null;
            return (Math.Min(nums[0], nums[1]), Math.Max(nums[0], nums[1]));
        }

        /// <summary>A list of numbers ("80;85;90", "80 85 90", "0,5; 1,5"); invalid fields are skipped.</summary>
        public static IReadOnlyList<double> Numbers(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<double>();
            // A single number with one decimal comma ("92,5") is one value, not the list "92;5".
            var t = text!.Trim();
            if (t.Count(c => c == ',') == 1 && t.IndexOfAny(new[] { ';', '\t', ' ', '|', '\n' }) < 0 && TryNumber(t, out var single)) return new[] { single };
            return Parse(text).SelectMany(b => b.Rows).SelectMany(r => r.Values).Where(Fmt.Finite).ToList();
        }
    }
}
