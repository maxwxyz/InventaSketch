using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using InventaSketch.Core;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// The built-in calculator with units (maintainer 2026-11-25: "a unit converter / calculator should be built in too"):
    /// one input line for expressions and conversions ("230 V * 16 A", "1500 rpm in rad/s", "(4k7 Ω || 10 kΩ)"), the result
    /// live while typing, the steps below it, a "Show in" list with the other units of the result's quantity, a history
    /// and copying. The engine does the work (<see cref="UnitCalculator"/>, <see cref="Units"/>); nothing is executed.
    /// Non-modal, so it stays open next to the slide.
    /// </summary>
    internal sealed class CalculatorDialog : Form
    {
        private static CalculatorDialog? _open;
        private readonly Lang _lang;
        private readonly TextBox _input;
        private readonly Label _result;
        private readonly Label _steps;
        private readonly ComboBox _showIn;
        private readonly ListBox _history;
        private CalcResult? _last;

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        /// <summary>Shows the calculator (one window; a second call brings it to the front).</summary>
        public static void ShowCalculator(Lang lang) => ShowCalculator(lang, null);

        /// <summary>Shows the calculator with <paramref name="text"/> in the input line (the value card's "Calculator …": the chosen value).</summary>
        public static void ShowCalculator(Lang lang, string? text)
        {
            var open = _open is { IsDisposed: false } existing ? existing : null;
            if (open is null) { open = _open = new CalculatorDialog(lang); open.Show(); }
            else open.Activate();
            if (!string.IsNullOrEmpty(text))
            {
                open._input.Text = text;
                open._input.SelectionStart = open._input.Text.Length;
                open._input.Focus();
            }
        }

        private CalculatorDialog(Lang lang)
        {
            _lang = lang;
            var font = new Font("Segoe UI", 10f);
            var em = TextRenderer.MeasureText("M", font).Height;
            Text = "InventaSketch – " + T("Calculator with units", "Rechner mit Einheiten");
            Icon = Branding.WindowIcon;
            Font = font;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(em * 30, em * 24);
            MinimumSize = new Size(em * 22, em * 18);

            var hint = new Label
            {
                Text = T("Type a calculation or a conversion, e.g. 230 V * 16 A · 1500 rpm in rad/s · 12 mm + 0.5 in · (4k7 Ω || 10 kΩ)",
                         "Rechnung oder Umrechnung eingeben, z. B. 230 V * 16 A · 1500 min⁻¹ in rad/s · 12 mm + 0,5 in · (4k7 Ω || 10 kΩ)"),
                Dock = DockStyle.Top, AutoSize = false, Height = em * 3, Padding = new Padding(em / 2, em / 3, em / 2, 0), ForeColor = SystemColors.GrayText,
            };
            _input = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 13f) };
            _result = new Label { Dock = DockStyle.Top, AutoSize = false, Height = em * 3, Font = new Font("Segoe UI Semibold", 18f), Padding = new Padding(em / 2, em / 4, 0, 0) };
            var showRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = em * 2 + 6, Padding = new Padding(em / 2, 0, 0, 0), WrapContents = false };
            var showLabel = new Label { Text = T("Show in", "Anzeigen in"), AutoSize = true, Margin = new Padding(0, em / 3, em / 3, 0) };
            _showIn = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = em * 8, Enabled = false };
            var copy = new Button { Text = T("Copy", "Kopieren"), AutoSize = true, Margin = new Padding(em, 0, 0, 0) };
            showRow.Controls.AddRange(new Control[] { showLabel, _showIn, copy });
            _steps = new Label { Dock = DockStyle.Top, AutoSize = false, Height = em * 4, ForeColor = SystemColors.GrayText, Padding = new Padding(em / 2, em / 4, em / 2, 0) };
            var historyTitle = new Label { Text = T("History (double-click to use again)", "Verlauf (Doppelklick: erneut verwenden)"), Dock = DockStyle.Top, Height = em + 6, Padding = new Padding(em / 2, 2, 0, 0) };
            _history = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };

            Controls.Add(_history);
            Controls.Add(historyTitle);
            Controls.Add(_steps);
            Controls.Add(showRow);
            Controls.Add(_result);
            Controls.Add(_input);
            Controls.Add(hint);

            _input.TextChanged += (_, _) => Recalculate();
            _input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Remember(); }
                if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
            };
            _showIn.SelectedIndexChanged += (_, _) => ShowInUnit();
            copy.Click += (_, _) => { if (!string.IsNullOrEmpty(_result.Text) && _last is not null) Clipboard.SetText(_result.Text); };
            _history.DoubleClick += (_, _) => { if (_history.SelectedItem is string s) { _input.Text = s.Split('=')[0].Trim(); _input.SelectionStart = _input.Text.Length; } };
            FormClosed += (_, _) => _open = null;
            Shown += (_, _) => _input.Focus();
        }

        private void Recalculate()
        {
            var text = _input.Text.Trim();
            _showIn.Items.Clear();
            _showIn.Enabled = false;
            _last = null;
            if (text.Length == 0) { _result.Text = ""; _steps.Text = ""; return; }
            if (UnitCalculator.TryEvaluate(text, out var r, out var error, _lang) && r is not null)
            {
                _last = r;
                _result.ForeColor = SystemColors.ControlText;
                _result.Text = r.Text;
                _steps.Text = string.Join(Environment.NewLine, r.Steps);
                if (r.Kind is { } kind)
                {
                    foreach (var u in Units.DisplayUnits(kind)) _showIn.Items.Add(u);
                    _showIn.Enabled = _showIn.Items.Count > 1;
                }
            }
            else
            {
                // Typing is not an error yet: show what is wrong quietly.
                _result.ForeColor = SystemColors.GrayText;
                _result.Text = "…";
                _steps.Text = error?.Get(_lang) ?? "";
            }
        }

        private void ShowInUnit()
        {
            if (_last is null || _showIn.SelectedItem is not string unit) return;
            var text = _input.Text.Trim();
            var cut = System.Text.RegularExpressions.Regex.Replace(text, @"\s+(in|to|as|->|→)\s+\S+\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            _input.Text = cut + " in " + unit;
            _input.SelectionStart = _input.Text.Length;
        }

        private void Remember()
        {
            if (_last is null) return;
            var line = _input.Text.Trim() + " = " + _last.Text;
            _history.Items.Remove(line);
            _history.Items.Insert(0, line);
            while (_history.Items.Count > 30) _history.Items.RemoveAt(_history.Items.Count - 1);
        }
    }
}
