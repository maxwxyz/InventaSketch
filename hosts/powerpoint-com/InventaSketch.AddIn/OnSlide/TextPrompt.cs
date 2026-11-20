using System;
using System.Drawing;
using System.Windows.Forms;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>Small modal text prompt (label, numeral, dimension text) owned by PowerPoint's window.</summary>
    internal static class TextPrompt
    {
        /// <summary>Returns the entered text, or null if cancelled.</summary>
        public static string? Ask(string label, string current, IntPtr owner)
        {
            // Layout from font metrics (auto-sizing panels), never fixed pixels: PowerPoint runs per-monitor DPI aware, so
            // fixed positions do not scale with the font on high-DPI screens.
            using var font = new Font("Segoe UI", 9f);
            var em = TextRenderer.MeasureText("M", font).Height;
            using var form = new Form
            {
                Text = "InventaSketch",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = font,
            };
            var width = Math.Max(em * 18, TextRenderer.MeasureText(label, font).Width + em);
            var layout = new TableLayoutPanel
            {
                ColumnCount = 1, RowCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(em * 2 / 3), Dock = DockStyle.Fill,
            };
            var caption = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 0, 0, em / 4) };
            var box = new TextBox { Text = current, Width = width, Margin = new Padding(0, 0, 0, em * 2 / 3) };
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false,
            };
            var minButton = new Size(em * 5, em * 3 / 2 + 6);
            var cancel = new Button { Text = Language() == "de" ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = minButton, Margin = new Padding(em / 3, 0, 0, 0) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = minButton, Margin = new Padding(em / 3, 0, 0, 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(caption, 0, 0);
            layout.Controls.Add(box, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            form.Controls.Add(layout);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            form.Shown += (_, _) => { box.Focus(); box.SelectAll(); };
            var result = owner != IntPtr.Zero ? form.ShowDialog(new Owner(owner)) : form.ShowDialog();
            return result == DialogResult.OK ? box.Text : null;
        }

        /// <summary>Asks for the value of an input context action (label plus unit, prefilled with the current value).</summary>
        public static string? AskInput(string label, string? unit, string current, IntPtr owner)
            => Ask(string.IsNullOrEmpty(unit) || label.Contains("(" + unit + ")") ? label : label + " (" + unit + ")", current, owner);

        /// <summary>Shows a warning (e.g. a value the action could not read), owned by PowerPoint's window.</summary>
        public static void Warn(string message, IntPtr owner)
        {
            if (owner != IntPtr.Zero) MessageBox.Show(new Owner(owner), message, "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else MessageBox.Show(message, "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string Language() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        private sealed class Owner : IWin32Window
        {
            public Owner(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
