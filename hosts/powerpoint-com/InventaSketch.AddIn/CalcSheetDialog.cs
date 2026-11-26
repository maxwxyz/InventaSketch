using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using InventaSketch.Core;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// The calculation sheet of the selected elements (or the slide) as text (Calc 6): CSV, Markdown or LaTeX with the cited
    /// sources, a preview, Copy, Save … and "Insert as table" (an annotation table on the slide). The engine builds the
    /// sheet (<see cref="CalcSheet"/>); the dialog only shows it.
    /// </summary>
    internal sealed class CalcSheetDialog : Form
    {
        private readonly Lang _lang;
        private readonly IReadOnlyList<CalcSheetRow> _rows;
        private readonly ComboBox _format;
        private readonly TextBox _text;

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        /// <summary>Shows the sheet of <paramref name="rows"/>; <paramref name="insertTable"/> puts it on the slide (null: no button).</summary>
        public static void Show(Lang lang, IReadOnlyList<CalcSheetRow> rows, string scope, Action<string>? insertTable, IWin32Window? owner)
        {
            var dialog = new CalcSheetDialog(lang, rows, scope, insertTable);
            if (owner is null) dialog.Show(); else dialog.Show(owner);
        }

        private CalcSheetDialog(Lang lang, IReadOnlyList<CalcSheetRow> rows, string scope, Action<string>? insertTable)
        {
            _lang = lang;
            _rows = rows;
            var font = new Font("Segoe UI", 10f);
            var em = TextRenderer.MeasureText("M", font).Height;
            Text = "InventaSketch – " + T("Calculation sheet", "Rechenblatt") + " (" + scope + ")";
            Icon = Branding.WindowIcon;
            Font = font;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(em * 48, em * 26);
            MinimumSize = new Size(em * 30, em * 16);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = em * 2 + 10, Padding = new Padding(em / 2, 4, 0, 0), WrapContents = false };
            var label = new Label { Text = T("Format", "Format"), AutoSize = true, Margin = new Padding(0, em / 3, em / 3, 0) };
            _format = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = em * 9 };
            _format.Items.AddRange(new object[] { "CSV", "Markdown", "LaTeX" });
            var copy = new Button { Text = T("Copy", "Kopieren"), AutoSize = true, Margin = new Padding(em, 0, 0, 0) };
            var save = new Button { Text = T("Save …", "Speichern …"), AutoSize = true };
            top.Controls.AddRange(new Control[] { label, _format, copy, save });
            if (insertTable is not null)
            {
                var insert = new Button { Text = T("Insert as table", "Als Tabelle einfügen"), AutoSize = true };
                insert.Click += (_, _) => { insertTable(CalcSheet.TableCells(_rows, _lang)); };
                top.Controls.Add(insert);
            }
            _text = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9.5f), BackColor = SystemColors.Window,
            };
            var hint = new Label
            {
                Dock = DockStyle.Bottom, Height = em + 8, Padding = new Padding(em / 2, 2, 0, 0), ForeColor = SystemColors.GrayText,
                Text = rows.Count == 0
                    ? T("No values yet: enter values on the elements (Values in the bar).", "Noch keine Werte: Werte an den Elementen eingeben (Werte in der Leiste).")
                    : T($"{rows.Count} values with formula, origin and source", $"{rows.Count} Werte mit Formel, Herkunft und Quelle"),
            };
            Controls.Add(_text);
            Controls.Add(hint);
            Controls.Add(top);

            _format.SelectedIndexChanged += (_, _) => _text.Text = Current().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
            _format.SelectedIndex = 0;
            copy.Click += (_, _) => { try { Clipboard.SetText(Current()); } catch (Exception ex) { Log.Error("Calc sheet copy", ex); } };
            save.Click += (_, _) => Save();
            KeyPreview = true;
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private string Current() => _format.SelectedIndex switch
        {
            1 => CalcSheet.Markdown(_rows, _lang),
            2 => CalcSheet.Latex(_rows, _lang),
            _ => CalcSheet.Csv(_rows, _lang),
        };

        private void Save()
        {
            var (ext, filter) = _format.SelectedIndex switch
            {
                1 => ("md", "Markdown (*.md)|*.md"),
                2 => ("tex", "LaTeX (*.tex)|*.tex"),
                _ => ("csv", "CSV (*.csv)|*.csv"),
            };
            using var dlg = new SaveFileDialog { Filter = filter, DefaultExt = ext, FileName = T("calculation-sheet", "rechenblatt") + "." + ext };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                // CSV with a byte order mark so spreadsheet programs read the units (Ω, ², ⁻¹) right.
                File.WriteAllText(dlg.FileName, Current(), new UTF8Encoding(ext == "csv"));
            }
            catch (Exception ex)
            {
                Log.Error("Calc sheet save", ex);
                MessageBox.Show(this, ex.Message, "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
