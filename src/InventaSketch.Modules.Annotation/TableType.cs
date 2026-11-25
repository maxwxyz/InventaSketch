using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Table of free cells (parameter <c>cells</c>: rows by line break or "\n", columns by "|" or tab), drawn with native
    /// lines and texts: bold header row, borders all / outer / horizontal / none, column widths from the content. Rows share
    /// the box height; the text size follows the row height and the widest cell.
    /// </summary>
    public sealed class TableType : AnnBoxType
    {
        public TableType() : base("ann.table", new I18nText("Table (cells)", "Tabelle (Zellen)"), AnnCategories.Labels,
            "table", "cells", "grid", "matrix", "tabelle", "zellen", "raster", "wertetabelle", "spalten", "zeilen")
        { }

        public override Size2 DefaultSize => new(60, 24);
        protected override bool EdgePorts => false;

        public static readonly (string, string, string)[] BorderOptions =
        {
            ("all", "All lines", "Alle Linien"), ("outer", "Outer frame only", "Nur Außenrahmen"), ("horizontal", "Horizontal lines only", "Nur waagerechte Linien"), ("none", "None", "Keine"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("cells", "Cells (rows by line break, columns by |)", "Zellen (Zeilen per Zeilenumbruch, Spalten per |)", null),
            P.Bool("header", "Header row (bold)", "Kopfzeile (fett)", true),
            P.Choice("borders", "Borders", "Linien", "all", BorderOptions),
            P.Num("textSize", "Text height (maximum)", "Schrifthöhe (höchstens)", 3.5, 1, 20, "mm", 0.5),
        };

        /// <summary>Cells used when the parameter is empty (a small two-column table in the drawing language).</summary>
        public static string DefaultCells(Lang lang) => lang == Lang.De ? "Merkmal|Wert\nA|1\nB|2" : "Feature|Value\nA|1\nB|2";

        /// <summary>The cell grid of a cell text: rows by line break (or a literal "\n"), columns by "|" or tab; rows padded to equal length.</summary>
        public static List<List<string>> Parse(string cells)
        {
            var lines = cells.Replace("\r", "").Replace("\\n", "\n").Split('\n').ToList();
            // A trailing line break adds no row (a row of empty cells "||" does).
            while (lines.Count > 1 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            var rows = lines.Select(r => r.Split('|', '\t').Select(c => c.Trim()).ToList()).ToList();
            var cols = Math.Max(1, rows.Max(r => r.Count));
            foreach (var r in rows) while (r.Count < cols) r.Add("");
            return rows;
        }

        public static string Format(IEnumerable<IEnumerable<string>> rows) => string.Join("\n", rows.Select(r => string.Join("|", r)));

        /// <summary>
        /// Clean slides for sharing (<see cref="Sanitizer"/>): numbers in the cells below the header row made unrecognisable
        /// per column – normalised: divided by the largest number of the column (two decimals); randomised: a random factor
        /// per column and ±spread per cell, decimals kept. "Replace texts" turns cells without digits into placeholders.
        /// </summary>
        public override SketchObject Sanitize(SketchObject o, SanitizeOptions opt, Random rng)
        {
            var r = Sanitizer.Basic(this, o, opt, rng);
            var raw = o.Str("cells", "");
            if (string.IsNullOrWhiteSpace(raw)) return r;
            var rows = Parse(raw);
            var start = o.Flag("header", true) && rows.Count > 1 ? 1 : 0;
            var cols = rows[0].Count;
            for (var c = 0; c < cols; c++)
            {
                if (opt.Mode == SanitizeMode.Normalised)
                {
                    var max = 0.0;
                    for (var i = start; i < rows.Count; i++) SanitizeText.MapNumbers(rows[i][c], v => { max = Math.Max(max, Math.Abs(v)); return v; });
                    if (max > 0)
                        for (var i = start; i < rows.Count; i++) rows[i][c] = SanitizeText.MapNumbers(rows[i][c], v => v / max, 2);
                }
                else if (opt.Mode == SanitizeMode.Randomised)
                {
                    var k = SanitizeText.Away(rng, opt.Variation);
                    for (var i = start; i < rows.Count; i++) rows[i][c] = SanitizeText.MapNumbers(rows[i][c], v => v * k * SanitizeText.Vary(rng, opt.Variation));
                }
            }
            if (opt.ReplaceTexts)
                foreach (var row in rows)
                    for (var c = 0; c < row.Count; c++)
                        if (row[c].Trim().Length > 0 && !row[c].Any(char.IsDigit)) row[c] = SanitizeText.Placeholder(row[c], rng).Replace("\n", " ");
            var text = Format(rows);
            return text == raw ? r : r.WithParam("cells", text);
        }

        /// <summary>An empty row inserted before row <paramref name="index"/> (clamped); the box grows by one row height.</summary>
        public static SketchObject InsertRow(SketchObject x, Lang lang, int index)
        {
            var rows = CellsOf(x, lang);
            rows.Insert(Math.Max(0, Math.Min(rows.Count, index)), Enumerable.Repeat("", rows[0].Count).ToList());
            return x.WithParam("cells", Format(rows)) with { Size = new Size2(x.Size.W, x.Size.H * rows.Count / Math.Max(1, rows.Count - 1)) };
        }

        /// <summary>An empty column inserted before column <paramref name="index"/> (clamped); the box grows by one mean column width.</summary>
        public static SketchObject InsertColumn(SketchObject x, Lang lang, int index)
        {
            var rows = CellsOf(x, lang);
            var cols = rows[0].Count;
            var at = Math.Max(0, Math.Min(cols, index));
            foreach (var r in rows) r.Insert(at, "");
            return x.WithParam("cells", Format(rows)) with { Size = new Size2(x.Size.W * (cols + 1) / Math.Max(1, cols), x.Size.H) };
        }

        private static List<List<string>> CellsOf(SketchObject o, Lang lang)
        {
            var raw = o.Str("cells", "");
            return Parse(string.IsNullOrWhiteSpace(raw) ? DefaultCells(lang) : raw);
        }

        /// <summary>Column widths (sum = <paramref name="width"/>) from the longest cell of each column, at least a quarter of the mean.</summary>
        public static List<double> ColumnWidths(IReadOnlyList<IReadOnlyList<string>> rows, double width)
        {
            var cols = rows.Count == 0 ? 1 : rows[0].Count;
            var need = Enumerable.Range(0, cols).Select(c => Math.Max(1, rows.Max(r => c < r.Count ? r[c].Length : 0)) + 1.5).ToList();
            var mean = need.Average();
            need = need.Select(n => Math.Max(n, mean * 0.25)).ToList();
            var sum = need.Sum();
            return need.Select(n => width * n / sum).ToList();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = Math.Max(4, o.Size.W), h = Math.Max(3, o.Size.H);
            var rows = CellsOf(o, ctx.Lang);
            var header = o.Flag("header", true) && rows.Count > 1;
            var borders = o.Str("borders", "all");
            var nRows = rows.Count; var nCols = rows[0].Count;
            var rh = h / nRows;
            var widths = ColumnWidths(rows, w);
            var xs = new List<double> { -w / 2 };
            foreach (var cw in widths) xs.Add(xs[xs.Count - 1] + cw);
            // One text size for the whole table: the row height and the widest cell relative to its column decide.
            var size = Math.Min(o.Num("textSize", 3.5), rh * 0.6);
            for (var c = 0; c < nCols; c++)
            {
                var longest = rows.Max(r => r[c].Length);
                if (longest > 0) size = Math.Min(size, Math.Max(0.5, widths[c] - 1.5) / (longest * Ann.CharWidth));
            }
            size = Math.Max(1.0, Math.Round(size, 3));
            var outer = Style.ThinLine with { StrokeWidth = 0.35 };
            var inner = Style.ThinLine with { StrokeWidth = 0.25 };
            var list = new List<Primitive>();
            if (borders is "all" or "outer")
                list.Add(Prim.Rect(-w / 2, -h / 2, w, h, outer with { Fill = Fill.Solid("#ffffff") }) with { Tag = AnnTags.Main });
            else
                // Invisible background keeps the box (selection, white page underneath).
                list.Add(Prim.Rect(-w / 2, -h / 2, w, h, Style.ThinLine with { Stroke = "none", Fill = Fill.Solid("#ffffff") }) with { Tag = "background" });
            if (borders == "horizontal")
            {
                list.Add(Prim.Line(-w / 2, -h / 2, w / 2, -h / 2, outer) with { Tag = "rule" });
                list.Add(Prim.Line(-w / 2, h / 2, w / 2, h / 2, outer) with { Tag = "rule" });
            }
            if (borders is "all" or "horizontal")
                for (var r = 1; r < nRows; r++)
                {
                    var y = -h / 2 + r * rh;
                    list.Add(Prim.Line(-w / 2, y, w / 2, y, r == 1 && header ? outer : inner) with { Tag = "rule" });
                }
            if (borders == "all")
                for (var c = 1; c < nCols; c++) list.Add(Prim.Line(xs[c], -h / 2, xs[c], h / 2, inner) with { Tag = "rule" });
            for (var r = 0; r < nRows; r++)
            {
                var y = -h / 2 + (r + 0.5) * rh;
                var bold = header && r == 0;
                for (var c = 0; c < nCols; c++)
                {
                    var text = rows[r][c];
                    if (text.Length == 0) continue;
                    var numeric = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                    var centred = bold || numeric;
                    var x = centred ? (xs[c] + xs[c + 1]) / 2 : xs[c] + 0.8;
                    list.Add(Ann.Text(new Vec2(x, y), text, size, "cell", centred ? TextAnchor.Middle : TextAnchor.Start, bold));
                }
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.table.header", new I18nText("Header row", "Kopfzeile"), IconIds.Toggle, "header", true, "table");
            foreach (var (value, en, de) in BorderOptions)
                yield return Ann.SetChoice(o, "ann.table.borders." + value, new I18nText("Borders: " + en, "Linien: " + de), IconIds.Option, "borders", value, "all", "borders");
            // Adding at a side (group "side.<side>"): hosts draw these as a "+" on that side of the table, one click each.
            // A row at the top goes under the header row while there is one (the header stays the header).
            var header = o.Flag("header", true) && CellsOf(o, lang).Count > 1;
            yield return new ContextAction("ann.table.row.add.top",
                header ? new I18nText("Add a row under the header", "Zeile unter der Kopfzeile einfügen") : new I18nText("Add a row at the top", "Zeile oben einfügen"),
                IconIds.RowAddTop, x => InsertRow(x, lang, x.Flag("header", true) && CellsOf(x, lang).Count > 1 ? 1 : 0)) { Group = "side.top" };
            yield return new ContextAction("ann.table.row.add", new I18nText("Add a row at the bottom", "Zeile unten anfügen"), IconIds.RowAdd,
                x => InsertRow(x, lang, int.MaxValue)) { Group = "side.bottom" };
            yield return new ContextAction("ann.table.column.add.left", new I18nText("Add a column on the left", "Spalte links einfügen"), IconIds.ColumnAdd,
                x => InsertColumn(x, lang, 0)) { Group = "side.left" };
            yield return new ContextAction("ann.table.column.add", new I18nText("Add a column on the right", "Spalte rechts anfügen"), IconIds.ColumnAdd,
                x => InsertColumn(x, lang, int.MaxValue)) { Group = "side.right" };
            yield return new ContextAction("ann.table.row.remove", new I18nText("Remove the last row", "Letzte Zeile entfernen"), IconIds.RowRemove, x =>
            {
                var rows = CellsOf(x, lang);
                if (rows.Count > 1) rows.RemoveAt(rows.Count - 1);
                return x.WithParam("cells", Format(rows));
            }) { Group = "cells", IsEnabled = CellsOf(o, lang).Count > 1 };
            yield return new ContextAction("ann.table.column.remove", new I18nText("Remove the last column", "Letzte Spalte entfernen"), IconIds.ColumnRemove, x =>
            {
                var rows = CellsOf(x, lang);
                if (rows[0].Count > 1) foreach (var r in rows) r.RemoveAt(r.Count - 1);
                return x.WithParam("cells", Format(rows));
            }) { Group = "cells", IsEnabled = CellsOf(o, lang)[0].Count > 1 };
        }
    }
}
