using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace InventaSketch.AddIn
{
    /// <summary>"About InventaSketch": logo, version, licence, project link and log file. Layout from font metrics.</summary>
    internal static class AboutDialog
    {
        public const string ProjectUrl = "https://github.com/maxwxyz/InventaSketch";

        public static void Show(IWin32Window? owner, bool german)
        {
            string T(string en, string de) => german ? de : en;
            using var font = new Font("Segoe UI", 9f);
            using var title = new Font("Segoe UI Semibold", 13f);
            var em = TextRenderer.MeasureText("M", font).Height;
            using var form = new Form
            {
                Text = T("About InventaSketch", "Über InventaSketch"),
                Icon = Branding.WindowIcon,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = font,
            };
            var logoSize = em * 4;
            var logo = new PictureBox { Image = Branding.Image(logoSize), Size = new Size(logoSize, logoSize), Margin = new Padding(0, 0, em, 0) };
            var text = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            text.Controls.Add(new Label { Text = "InventaSketch", Font = title, AutoSize = true, Margin = new Padding(0, 0, 0, em / 4) });
            text.Controls.Add(new Label { Text = T("Version ", "Version ") + typeof(AboutDialog).Assembly.GetName().Version, AutoSize = true });
            text.Controls.Add(new Label { Text = T("Licence: Apache-2.0", "Lizenz: Apache-2.0"), AutoSize = true, Margin = new Padding(0, 0, 0, em / 2) });
            var link = new LinkLabel { Text = ProjectUrl, AutoSize = true };
            link.LinkClicked += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true }); }
                catch (Exception ex) { Log.Error("Open project page", ex); }
            };
            text.Controls.Add(link);
            text.Controls.Add(new Label { Text = T("Log: ", "Protokoll: ") + Log.File, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, em / 2, 0, 0) });

            var top = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            top.Controls.Add(logo);
            top.Controls.Add(text);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(em * 5, em * 3 / 2 + 6), Anchor = AnchorStyles.Right, Margin = new Padding(0, em, 0, 0) };
            var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(em), Dock = DockStyle.Fill };
            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(ok, 0, 1);
            form.Controls.Add(layout);
            form.AcceptButton = ok;
            form.CancelButton = ok;
            if (owner is not null) form.ShowDialog(owner); else form.ShowDialog();
        }
    }
}
