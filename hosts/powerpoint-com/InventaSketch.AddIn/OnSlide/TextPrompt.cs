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
            using var form = new Form
            {
                Text = "InventaSketch",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.Dpi,
                ClientSize = new Size(340, 110),
                Font = new Font("Segoe UI", 9f),
            };
            var caption = new Label { Text = label, Left = 12, Top = 12, Width = 316, AutoSize = false };
            var box = new TextBox { Text = current, Left = 12, Top = 36, Width = 316 };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 172, Top = 72, Width = 75 };
            var cancel = new Button { Text = Language() == "de" ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel, Left = 253, Top = 72, Width = 75 };
            form.Controls.AddRange(new Control[] { caption, box, ok, cancel });
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            form.Shown += (_, _) => { box.Focus(); box.SelectAll(); };
            var result = owner != IntPtr.Zero ? form.ShowDialog(new Owner(owner)) : form.ShowDialog();
            return result == DialogResult.OK ? box.Text : null;
        }

        private static string Language() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        private sealed class Owner : IWin32Window
        {
            public Owner(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
