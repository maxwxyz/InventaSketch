using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Table editor for chart data (charts v2 stage 2; replaces the one-line "Data…" prompt): rows and columns like a sheet,
    /// "x;y" or "label;value;value…" per row, an empty row starts a new series (blank line of the chart data format).
    /// Ctrl+V pastes a range copied from Excel (tab separated) at the current cell; Delete clears the selected cells.
    /// </summary>
    internal static class DataEditor
    {
        /// <summary>The edited data as rows joined by " | " (the chart's data input format), or null if cancelled.</summary>
        public static string? Ask(string title, string current, IntPtr owner)
        {
            using var font = new Font("Segoe UI", 9f);
            var em = TextRenderer.MeasureText("M", font).Height;
            var rows = current.Replace("\r", "").Split(new[] { '|', '\n' }).Select(r => r.Trim()).ToList();
            while (rows.Count > 0 && rows[rows.Count - 1].Length == 0) rows.RemoveAt(rows.Count - 1);
            var cells = rows.Select(r => r.Length == 0 ? Array.Empty<string>() : r.Split(';').Select(c => c.Trim()).ToArray()).ToList();
            var columns = Math.Max(3, cells.Count == 0 ? 2 : cells.Max(c => c.Length) + 1);

            using var form = new Form
            {
                Text = "InventaSketch – " + title,
                FormBorderStyle = FormBorderStyle.Sizable,
                Icon = Branding.WindowIcon,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                Font = font,
                ClientSize = new Size(em * 30, em * 24),
                MinimumSize = new Size(em * 20, em * 14),
            };
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToResizeRows = false,
                RowHeadersWidth = em * 3,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                // Columns share the width (headers readable), white like a sheet.
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
            };
            for (var c = 0; c < columns; c++)
                grid.Columns.Add("c" + c, c == 0 ? German() ? "x / Bezeichnung" : "x / label" : (German() ? "Wert " : "Value ") + c);
            foreach (var r in cells)
            {
                var i = grid.Rows.Add();
                for (var c = 0; c < r.Length && c < columns; c++) grid.Rows[i].Cells[c].Value = r[c];
            }
            grid.KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.V) { Paste(grid); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { foreach (DataGridViewCell cell in grid.SelectedCells) cell.Value = null; e.Handled = true; }
            };
            var hint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = em * 3,
                Padding = new Padding(em / 2, em / 4, em / 2, em / 4),
                Text = German()
                    ? "Eine Zeile je Punkt bzw. Kategorie; leere Zeile = neue Reihe. Strg+V fügt einen Bereich aus Excel ein."
                    : "One row per point or category; an empty row starts a new series. Ctrl+V pastes a range from Excel.",
            };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(em / 2) };
            var minButton = new Size(em * 5, em * 3 / 2 + 6);
            var cancel = new Button { Text = German() ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = minButton };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = minButton };
            var addColumn = new Button { Text = German() ? "Spalte +" : "Column +", AutoSize = true, MinimumSize = minButton };
            addColumn.Click += (_, _) => grid.Columns.Add("c" + grid.Columns.Count, (German() ? "Wert " : "Value ") + grid.Columns.Count);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            buttons.Controls.Add(addColumn);
            form.Controls.Add(grid);
            form.Controls.Add(hint);
            form.Controls.Add(buttons);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            var result = owner != IntPtr.Zero ? form.ShowDialog(new Owner(owner)) : form.ShowDialog();
            if (result != DialogResult.OK) return null;
            grid.EndEdit();
            return ToText(grid);
        }

        /// <summary>Grid rows as "a;b;c" joined by " | " (trailing empty cells and rows dropped, inner empty rows kept as separators).</summary>
        internal static string ToText(DataGridView grid)
        {
            var lines = new List<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var values = row.Cells.Cast<DataGridViewCell>().Select(c => (c.Value?.ToString() ?? "").Trim()).ToList();
                while (values.Count > 0 && values[values.Count - 1].Length == 0) values.RemoveAt(values.Count - 1);
                lines.Add(string.Join(";", values));
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
            return string.Join(" | ", lines);
        }

        /// <summary>Pastes a tab-separated range (Excel) at the current cell, adding rows and columns as needed.</summary>
        private static void Paste(DataGridView grid)
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Replace("\r", "").TrimEnd('\n');
            var r0 = grid.CurrentCell?.RowIndex ?? 0;
            var c0 = grid.CurrentCell?.ColumnIndex ?? 0;
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split(lines[i].Contains('\t') ? '\t' : ';');
                while (grid.Rows.Count - 1 <= r0 + i) grid.Rows.Add();
                for (var j = 0; j < parts.Length; j++)
                {
                    while (grid.Columns.Count <= c0 + j) grid.Columns.Add("c" + grid.Columns.Count, (German() ? "Wert " : "Value ") + grid.Columns.Count);
                    grid.Rows[r0 + i].Cells[c0 + j].Value = parts[j].Trim();
                }
            }
        }

        private static bool German() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

        private sealed class Owner : IWin32Window
        {
            public Owner(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
