using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>One radial menu entry. Delete is always the first entry and sits at the top.</summary>
    internal sealed record PieItem(string Id, string Label, bool Enabled = true, bool Active = false, bool Danger = false)
    {
        /// <summary>Optional picture shown above the label (views: the object rendered in that view).</summary>
        public Bitmap? Icon { get; init; }
        /// <summary>Optional symbol shown above the label.</summary>
        public string? Glyph { get; init; }
        /// <summary>Abstract pictogram id (<see cref="InventaSketch.Core.IconIds"/>); preferred over Icon and Glyph.</summary>
        public string? IconId { get; init; }
        /// <summary>One line that says what the entry does, shown below the ring while the pointer is on it.</summary>
        public string? Hint { get; init; }
    }

    /// <summary>
    /// Radial (pie) menu layout, hit test and drawing. Unavailable commands are greyed instead of hidden so every command
    /// keeps its place; the first item (delete) is at 12 o'clock (Slidemax conventions), the others follow clockwise in
    /// the order edit, insert, view, style, arrange (<see cref="OnSlideController.PieRing"/>). Each sector shows an abstract
    /// pictogram above a label that is wrapped (two lines at most) to the sector's chord, shrunk or shortened with "…";
    /// the hovered entry shows its full label and a one-line explanation (<see cref="PieItem.Hint"/>) in a pill below the
    /// ring. Esc, a click outside the ring or on its centre close it.
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
        /// <summary>Name shown in the centre (the element, or the open submenu); a click there closes the menu.</summary>
        public string? Title { get; set; }
        public float Inner => (Title is null ? 28 : 42) * Scale;
        /// <summary>The ring grows with the number of entries so labels keep their room.</summary>
        public float Outer => (Items.Count <= 6 ? 112 : Items.Count <= 8 ? 124 : 152) * Scale;
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

        private static bool HasIcon(PieItem item) => item.IconId is not null || item.Icon is not null || item.Glyph is not null;

        /// <summary>Annular sector of entry <paramref name="i"/> (GDI+ angles: 0° = 3 o'clock, clockwise).</summary>
        private GraphicsPath Sector(int i, float inner, float outer)
        {
            var sweep = 360f / Math.Max(1, Items.Count);
            var start = -90 - sweep / 2 + i * sweep;
            var path = new GraphicsPath();
            path.AddArc(Centre.X - outer, Centre.Y - outer, 2 * outer, 2 * outer, start, sweep);
            path.AddArc(Centre.X - inner, Centre.Y - inner, 2 * inner, 2 * inner, start + sweep, -sweep);
            path.CloseFigure();
            return path;
        }

        /// <summary>Wraps <paramref name="text"/> to <paramref name="width"/>; true if it fits completely in two lines.</summary>
        private static bool Fits(Graphics g, string text, Font font, float width, out SizeF size)
        {
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.None };
            size = g.MeasureString(text, font, new SizeF(width, 1000), fmt, out var chars, out var lines);
            // Never break inside a word: every word must fit on a line of its own.
            return chars >= text.Length && lines <= 2 && WordsFit(g, text, font, width);
        }

        private static bool WordsFit(Graphics g, string text, Font font, float width)
            => text.Split(new[] { (char)32 }, StringSplitOptions.RemoveEmptyEntries).All(w => g.MeasureString(w, font).Width <= width);


        public void Draw(Graphics g, bool dark)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var bg = dark ? Color.FromArgb(246, 36, 38, 42) : Color.FromArgb(248, 252, 252, 253);
            var fg = dark ? Color.FromArgb(232, 232, 236) : Color.FromArgb(36, 37, 40);
            // Greyed places (nothing of that kind for this element) recede: low contrast, no pictogram weight.
            var muted = dark ? Color.FromArgb(78, 78, 84) : Color.FromArgb(198, 198, 204);
            var accent = dark ? Color.FromArgb(110, 160, 255) : Color.FromArgb(31, 95, 191);
            var danger = dark ? Color.FromArgb(255, 110, 100) : Color.FromArgb(200, 40, 40);
            float inner = Inner, outer = Outer;
            var rect = new RectangleF(Centre.X - outer, Centre.Y - outer, 2 * outer, 2 * outer);

            // Soft shadow, body and a hairline border.
            for (var k = 3; k >= 1; k--)
                using (var shadow = new SolidBrush(Color.FromArgb(dark ? 28 : 16, 0, 0, 0)))
                    g.FillEllipse(shadow, RectangleF.Inflate(new RectangleF(rect.X, rect.Y + 2 * Scale, rect.Width, rect.Height), k * 2 * Scale, k * 2 * Scale));
            using (var b = new SolidBrush(bg)) g.FillEllipse(b, rect);
            using (var border = new Pen(Color.FromArgb(dark ? 70 : 40, fg), 1f)) g.DrawEllipse(border, rect);

            var sweepDeg = 360f / Math.Max(1, Items.Count);
            var sweepRad = sweepDeg * Math.PI / 180;
            using var font = new Font("Segoe UI", 9f * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
            using var small = new Font("Segoe UI", 8f * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
            string? hoverFull = null;
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var start = -90 - sweepDeg / 2 + i * sweepDeg;
                var color = !item.Enabled ? muted : item.Danger ? danger : item.Active ? accent : fg;
                if (i == Hover && item.Enabled)
                    using (var path = Sector(i, inner + 2 * Scale, outer - 2 * Scale))
                    using (var hb = new SolidBrush(Color.FromArgb(dark ? 60 : 34, item.Danger ? danger : accent)))
                        g.FillPath(hb, path);
                if (item.Active)
                    using (var mark = new Pen(accent, 3 * Scale))
                        g.DrawArc(mark, rect.X + 3 * Scale, rect.Y + 3 * Scale, rect.Width - 6 * Scale, rect.Height - 6 * Scale, start + 4, sweepDeg - 8);
                // Short separators between sectors.
                var sepA = start * Math.PI / 180;
                using (var sep = new Pen(Color.FromArgb(dark ? 45 : 28, fg), 1f))
                    g.DrawLine(sep,
                        Centre.X + (float)Math.Cos(sepA) * (inner + 8 * Scale), Centre.Y + (float)Math.Sin(sepA) * (inner + 8 * Scale),
                        Centre.X + (float)Math.Cos(sepA) * (outer - 8 * Scale), Centre.Y + (float)Math.Sin(sepA) * (outer - 8 * Scale));

                // Content block (pictogram over label) centred in the sector.
                var mid = (start + sweepDeg / 2) * Math.PI / 180;
                // Text-only entries (sub-menus) sit further out, where the sector is wider.
                var rc = inner + (outer - inner) * (HasIcon(item) ? 0.55f : 0.64f);
                var chord = (float)(2 * rc * Math.Sin(Math.Min(sweepRad, Math.PI) / 2));
                var width = Math.Max(44 * Scale, Math.Min(chord - 6 * Scale, 96 * Scale));
                var useFont = font;
                var oneLine = false;
                var full = Fits(g, item.Label, font, width, out var textSize);
                if (!full && Fits(g, item.Label, small, width, out var smallSize)) { useFont = small; textSize = smallSize; full = true; }
                else if (!full)
                {
                    useFont = small;
                    // A word too long even in the small font: one line shortened with "…" instead of a break inside the word.
                    oneLine = !WordsFit(g, item.Label, small, width);
                    textSize = new SizeF(width, (oneLine ? 1 : 2) * small.GetHeight(g));
                }
                if (i == Hover)
                {
                    // Below the ring: the full label of a shortened entry and what the entry does.
                    var hint = string.IsNullOrEmpty(item.Hint) || item.Hint == item.Label ? null : item.Hint;
                    hoverFull = !full && hint is not null ? item.Label + " – " + hint : !full ? item.Label : hint;
                }
                var icon = HasIcon(item) ? 18 * Scale : 0;
                var gap = icon > 0 ? 3 * Scale : 0;
                var blockH = icon + gap + textSize.Height;
                var cx = Centre.X + (float)(Math.Cos(mid) * rc);
                var cy = Centre.Y + (float)(Math.Sin(mid) * rc);
                var top = cy - blockH / 2;
                if (icon > 0)
                {
                    var iconRect = new RectangleF(cx - icon / 2, top, icon, icon);
                    if (item.IconId is not null) IconPainter.Draw(g, item.IconId, iconRect, color);
                    else if (item.Icon is not null) g.DrawImage(item.Icon, iconRect);
                    else
                        using (var glyphFont = new Font("Segoe UI Symbol", 13f * Scale, GraphicsUnit.Pixel))
                        using (var gb = new SolidBrush(color))
                        using (var gf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString(item.Glyph, glyphFont, gb, iconRect, gf);
                }
                using var tb = new SolidBrush(color);
                using var lineFmt = new StringFormat(fmt) { FormatFlags = oneLine ? StringFormatFlags.NoWrap : 0 };
                g.DrawString(item.Label, useFont, tb, new RectangleF(cx - width / 2, top + icon + gap, width, textSize.Height + 1), lineFmt);
            }

            // Centre: a quiet cap with a close mark (clicking there or outside closes the menu).
            using (var c = new SolidBrush(bg)) g.FillEllipse(c, Centre.X - inner, Centre.Y - inner, 2 * inner, 2 * inner);
            using (var p = new Pen(Color.FromArgb(dark ? 70 : 45, fg), 1f)) g.DrawEllipse(p, Centre.X - inner, Centre.Y - inner, 2 * inner, 2 * inner);
            if (Title is not null)
            {
                // The element's name in the centre, so it is clear what the menu acts on.
                using var titleFont = new Font("Segoe UI", 8.5f * Scale, FontStyle.Bold, GraphicsUnit.Pixel);
                using var tb = new SolidBrush(Color.FromArgb(dark ? 200 : 190, fg));
                using var tf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };
                var box = new RectangleF(Centre.X - inner * 0.86f, Centre.Y - inner * 0.62f, inner * 1.72f, inner * 1.24f);
                g.DrawString(Title, titleFont, tb, box, tf);
            }
            else
                using (var x = new Pen(Color.FromArgb(dark ? 120 : 110, fg), 1.4f * Scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    var d = 5 * Scale;
                    g.DrawLine(x, Centre.X - d, Centre.Y - d, Centre.X + d, Centre.Y + d);
                    g.DrawLine(x, Centre.X + d, Centre.Y - d, Centre.X - d, Centre.Y + d);
                }

            // Full label of a shortened entry and the one-line explanation of the entry under the pointer.
            if (hoverFull is not null)
            {
                var size = g.MeasureString(hoverFull, font);
                var pill = new RectangleF(Centre.X - size.Width / 2 - 8 * Scale, Centre.Y + outer + 8 * Scale, size.Width + 16 * Scale, size.Height + 6 * Scale);
                using var path = new GraphicsPath();
                var rr = pill.Height;
                path.AddArc(pill.X, pill.Y, rr, rr, 90, 180);
                path.AddArc(pill.Right - rr, pill.Y, rr, rr, 270, 180);
                path.CloseFigure();
                using (var pb = new SolidBrush(dark ? Color.FromArgb(240, 60, 62, 68) : Color.FromArgb(240, 40, 42, 46))) g.FillPath(pb, path);
                using var pt = new SolidBrush(Color.White);
                using var pf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(hoverFull, font, pt, pill, pf);
            }
        }
    }
}
