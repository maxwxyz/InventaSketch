using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>One radial menu entry. Delete is always the first entry and sits at the top.</summary>
    internal sealed record PieItem(string Id, string Label, bool Enabled = true, bool Active = false, bool Danger = false);

    /// <summary>
    /// Radial (pie) menu layout, hit test and drawing. Unavailable commands are greyed instead of hidden so every command
    /// keeps its place; the first item (delete) is at 12 o'clock (Slidemax conventions).
    /// </summary>
    internal sealed class PieMenu
    {
        public PieMenu(PointF centre, IReadOnlyList<PieItem> items, float scale)
        {
            Centre = centre; Items = items; Scale = scale;
        }

        public PointF Centre { get; }
        public IReadOnlyList<PieItem> Items { get; }
        public float Scale { get; }
        public float Inner => 22 * Scale;
        public float Outer => 104 * Scale;
        public int Hover { get; set; } = -1;

        private double Sweep => 2 * Math.PI / Math.Max(1, Items.Count);

        /// <summary>Index of the entry under the screen point, -1 for the centre/outside.</summary>
        public int HitTest(PointF p)
        {
            double dx = p.X - Centre.X, dy = p.Y - Centre.Y;
            var r = Math.Sqrt(dx * dx + dy * dy);
            if (r < Inner || r > Outer + 30 * Scale) return -1;
            // 0 rad = 12 o'clock, clockwise.
            var a = Math.Atan2(dx, -dy);
            if (a < 0) a += 2 * Math.PI;
            var idx = (int)Math.Floor((a + Sweep / 2) / Sweep) % Items.Count;
            return idx;
        }

        public bool Contains(PointF p)
        {
            double dx = p.X - Centre.X, dy = p.Y - Centre.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= Outer + 30 * Scale;
        }

        public void Draw(Graphics g, bool dark)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var bg = dark ? Color.FromArgb(240, 38, 40, 44) : Color.FromArgb(245, 252, 252, 253);
            var fg = dark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(32, 33, 36);
            var muted = dark ? Color.FromArgb(110, 110, 115) : Color.FromArgb(170, 170, 175);
            var accent = Color.FromArgb(31, 95, 191);
            var rect = new RectangleF(Centre.X - Outer, Centre.Y - Outer, 2 * Outer, 2 * Outer);
            using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(shadow, RectangleF.Inflate(rect, 3 * Scale, 3 * Scale));
            using (var b = new SolidBrush(bg)) g.FillEllipse(b, rect);
            var sweepDeg = 360f / Math.Max(1, Items.Count);
            using var font = new Font("Segoe UI", 8.5f * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                // GDI+ angles: 0° = 3 o'clock, clockwise. Entry i is centred at -90 + i*sweep.
                var start = -90 - sweepDeg / 2 + i * sweepDeg;
                if (i == Hover && item.Enabled)
                    using (var hb = new SolidBrush(Color.FromArgb(item.Danger ? 70 : 50, item.Danger ? Color.Red : accent)))
                        g.FillPie(hb, rect.X, rect.Y, rect.Width, rect.Height, start, sweepDeg);
                using (var sep = new Pen(Color.FromArgb(40, fg), 1)) g.DrawPie(sep, rect.X, rect.Y, rect.Width, rect.Height, start, sweepDeg);
                var mid = (start + sweepDeg / 2) * Math.PI / 180;
                var rr = (Inner + Outer) / 2 + 4 * Scale;
                var pos = new PointF(Centre.X + (float)(Math.Cos(mid) * rr), Centre.Y + (float)(Math.Sin(mid) * rr));
                var color = !item.Enabled ? muted : item.Danger ? Color.FromArgb(200, 40, 40) : item.Active ? accent : fg;
                using var tb = new SolidBrush(color);
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var box = new RectangleF(pos.X - 34 * Scale, pos.Y - 16 * Scale, 68 * Scale, 32 * Scale);
                var label = item.Active ? "● " + item.Label : item.Label;
                g.DrawString(label, font, tb, box, fmt);
            }
            using (var c = new SolidBrush(bg)) g.FillEllipse(c, Centre.X - Inner, Centre.Y - Inner, 2 * Inner, 2 * Inner);
            using (var p = new Pen(Color.FromArgb(60, fg), 1)) g.DrawEllipse(p, Centre.X - Inner, Centre.Y - Inner, 2 * Inner, 2 * Inner);
        }
    }
}
