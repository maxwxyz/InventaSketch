using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>One button of the floating context bar.</summary>
    internal sealed record BarButton(string Id, string Glyph, string Tip, bool Active = false, bool Danger = false, bool Separator = false)
    {
        /// <summary>Optional picture (e.g. the object rendered in that view); replaces the glyph.</summary>
        public Bitmap? Icon { get; init; }
        /// <summary>Abstract pictogram id (<see cref="InventaSketch.Core.IconIds"/>); preferred over Icon and Glyph.</summary>
        public string? IconId { get; init; }
        /// <summary>The button opens a choice (icon row) in the bar: a small caret marks it.</summary>
        public bool Menu { get; init; }
        /// <summary>Draw the pictogram mirrored (start arrowheads point left).</summary>
        public bool Mirror { get; init; }
        /// <summary>Show <see cref="Glyph"/> as a short label beside the pictogram (main actions whose pictogram alone says too little).</summary>
        public bool ShowLabel { get; init; }
    }

    /// <summary>One row of a choice dropdown under a bar button: pictogram (or picture) and label.</summary>
    internal sealed record DropItem(string Id, string Label, string? IconId, Bitmap? Icon, bool Active, bool Mirror);

    /// <summary>
    /// Fade rule of the floating bar (behaviour as in Slidemax): fully opaque within 12 logical px of the bar, otherwise a
    /// resting opacity of 0.92 that fades linearly over 180 px, measured from the smallest pointer distance seen since the
    /// bar appeared. Below 0.1 the bar is dismissed until the next selection.
    /// </summary>
    internal sealed class BarFade
    {
        public const double Near = 12, Span = 180, Resting = 0.92, DismissBelow = 0.1;
        private double _minDistance = double.MaxValue;

        public bool Dismissed { get; private set; }

        public void Reset()
        {
            _minDistance = double.MaxValue;
            Dismissed = false;
        }

        /// <summary>Opacity for the current pointer distance (logical pixels, 0 inside the bar).</summary>
        public double Update(double distance)
        {
            if (Dismissed) return 0;
            if (distance < _minDistance) _minDistance = distance;
            if (distance <= Near) return 1;
            var t = (distance - Math.Max(_minDistance, Near)) / Span;
            var opacity = Resting * (1 - Math.Max(0, Math.Min(1, t)));
            if (opacity < DismissBelow) { Dismissed = true; return 0; }
            return opacity;
        }

        public void Dismiss() => Dismissed = true;
    }

    /// <summary>Layout, hit test and drawing of the floating context bar (screen pixels).</summary>
    internal sealed class FloatingBar
    {
        private readonly List<Rectangle> _cells = new();

        public FloatingBar(IReadOnlyList<BarButton> buttons, float scale)
        {
            Buttons = buttons;
            Scale = scale;
            int size = (int)Math.Round(30 * scale), gap = (int)Math.Round(2 * scale), sep = (int)Math.Round(9 * scale), pad = (int)Math.Round(4 * scale);
            var x = pad;
            foreach (var b in buttons)
            {
                if (b.Separator) { _cells.Add(new Rectangle(x, pad, sep, size)); x += sep; continue; }
                // Text buttons (view names) get a width that fits the label; symbol buttons are square; a pictogram with a
                // label (the main action where the pictogram alone says too little) gets both.
                var width = b.ShowLabel && b.IconId is not null ? size + (int)Math.Round(b.Glyph.Length * 6.4 * scale + 6 * scale)
                    : b.Icon is null && b.IconId is null && b.Glyph.Length > 2 ? (int)Math.Round(Math.Max(size, b.Glyph.Length * 6.6 * scale + 14 * scale))
                    : size;
                if (b.Menu) width += (int)Math.Round(7 * scale);
                _cells.Add(new Rectangle(x, pad, width, size));
                x += width + gap;
            }
            Size = new Size(x - gap + pad, size + 2 * pad);
            TipHeight = (int)Math.Round(26 * scale);
        }

        /// <summary>Height of the tooltip strip below the bar (part of the window, never hit-tested).</summary>
        public int TipHeight { get; }

        /// <summary>Hit-test index offset of dropdown rows (row i answers <c>DropBase + i</c>).</summary>
        public const int DropBase = 1000;

        /// <summary>Bar button the open dropdown belongs to (-1: none) and its rows.</summary>
        public int DropAnchor { get; private set; } = -1;
        public IReadOnlyList<DropItem> DropItems { get; private set; } = Array.Empty<DropItem>();
        private Rectangle _drop;
        private int _rowH;

        /// <summary>Opens a dropdown list under bar button <paramref name="anchor"/>: one row per option with icon and label.</summary>
        public void SetDropdown(int anchor, IReadOnlyList<DropItem> items)
        {
            if (anchor < 0 || anchor >= _cells.Count || items.Count == 0) return;
            DropAnchor = anchor;
            DropItems = items;
            _rowH = (int)Math.Round(28 * Scale);
            var longest = items.Max(i => i.Label.Length);
            var width = (int)Math.Round(Math.Max(150 * Scale, 46 * Scale + longest * 6.6 * Scale));
            var pad = (int)Math.Round(4 * Scale);
            _drop = new Rectangle(_cells[anchor].X, Size.Height + pad, width, items.Count * _rowH + 2 * pad);
        }

        private Rectangle DropScreen => new(Bounds.X + _drop.X, Bounds.Y + _drop.Y, _drop.Width, _drop.Height);

        /// <summary>Window rectangle: bar plus tooltip strip, or plus the open dropdown.</summary>
        public Rectangle WindowBounds => DropAnchor < 0
            ? new(Bounds.X, Bounds.Y, Math.Max(Bounds.Width, (int)(260 * Scale)), Bounds.Height + TipHeight)
            : new(Bounds.X, Bounds.Y, Math.Max(Bounds.Width, _drop.Right + 2), Math.Max(Bounds.Height + TipHeight, _drop.Bottom + 2));

        /// <summary>Command id behind a hit-test index (bar button or dropdown row).</summary>
        public string IdAt(int index) => index >= DropBase ? DropItems[index - DropBase].Id : Buttons[index].Id;

        public IReadOnlyList<BarButton> Buttons { get; }
        public float Scale { get; }
        public Size Size { get; }
        /// <summary>Bar position on screen.</summary>
        public Rectangle Bounds { get; private set; }
        public int Hover { get; set; } = -1;

        /// <summary>Centres the bar above the selection, or below it if there is no room; clamped to the pane.</summary>
        public void Place(RectangleF selection, Rectangle pane, IReadOnlyList<RectangleF>? obstacles = null)
            => Bounds = Placement(Size, Scale, selection, pane, obstacles);

        /// <summary>
        /// Where the bar goes: above the selection (clear of PowerPoint's rotation handle, about 35 logical px above the
        /// frame), else below, else beside it, inside the pane. With <paramref name="obstacles"/> (other elements on the
        /// slide) the place that covers the least of them wins, so the bar does not hide parts coupled to the selection;
        /// on a tie the order above, below, right, left decides.
        /// </summary>
        internal static Rectangle Placement(Size size, float scale, RectangleF selection, Rectangle pane, IReadOnlyList<RectangleF>? obstacles = null)
        {
            var gapAbove = (int)Math.Round(46 * scale);
            var gapBelow = (int)Math.Round(14 * scale);
            var gapSide = (int)Math.Round(22 * scale);
            int cx = (int)Math.Round(selection.X + selection.Width / 2 - size.Width / 2.0);
            int cy = (int)Math.Round(selection.Y + selection.Height / 2 - size.Height / 2.0);
            var candidates = new[]
            {
                new Point(cx, (int)Math.Round(selection.Y) - gapAbove - size.Height),
                new Point(cx, (int)Math.Round(selection.Bottom) + gapBelow),
                new Point((int)Math.Round(selection.Right) + gapSide, cy),
                new Point((int)Math.Round(selection.X) - gapSide - size.Width, cy),
            };
            Rectangle Clamp(Point p)
            {
                var x = Math.Max(pane.Left + 2, Math.Min(pane.Right - size.Width - 2, p.X));
                var y = Math.Max(pane.Top + 2, Math.Min(pane.Bottom - size.Height - 2, p.Y));
                return new Rectangle(x, y, size.Width, size.Height);
            }
            bool Fits(Point p) => p.X >= pane.Left + 2 && p.X + size.Width <= pane.Right - 2 && p.Y >= pane.Top + 2 && p.Y + size.Height <= pane.Bottom - 2;
            double Covered(Rectangle r)
            {
                double sum = 0;
                // Covering the selection itself is worst; then other elements.
                var own = RectangleF.Intersect(r, selection);
                if (!own.IsEmpty) sum += 4 * own.Width * own.Height;
                if (obstacles is null) return sum;
                foreach (var o in obstacles)
                {
                    var i = RectangleF.Intersect(r, o);
                    if (!i.IsEmpty) sum += i.Width * i.Height;
                }
                return sum;
            }
            Rectangle? best = null;
            var bestScore = double.MaxValue;
            for (var k = 0; k < candidates.Length; k++)
            {
                // The horizontal position may be clamped into the pane; a place outside it vertically is skipped.
                var c = candidates[k];
                var shifted = new Point(Math.Max(pane.Left + 2, Math.Min(pane.Right - size.Width - 2, c.X)), c.Y);
                if (!Fits(shifted)) continue;
                var r = new Rectangle(shifted, size);
                var score = Covered(r) + k * 0.5;
                if (score < bestScore) { bestScore = score; best = r; }
            }
            return best ?? Clamp(candidates[0].Y < pane.Top + 2 ? candidates[1] : candidates[0]);
        }

        public bool Contains(Point p) => Bounds.Contains(p) || (DropAnchor >= 0 && DropScreen.Contains(p));

        /// <summary>Distance from a screen point to the bar in logical pixels (0 inside).</summary>
        public double Distance(Point p)
        {
            double D(Rectangle r)
            {
                double dx = Math.Max(0, Math.Max(r.Left - p.X, p.X - r.Right));
                double dy = Math.Max(0, Math.Max(r.Top - p.Y, p.Y - r.Bottom));
                return Math.Sqrt(dx * dx + dy * dy) / Scale;
            }
            return DropAnchor >= 0 ? Math.Min(D(Bounds), D(DropScreen)) : D(Bounds);
        }

        public int HitTest(Point p)
        {
            if (DropAnchor >= 0 && DropScreen.Contains(p))
            {
                var row = (p.Y - DropScreen.Y - (int)Math.Round(4 * Scale)) / Math.Max(1, _rowH);
                return row >= 0 && row < DropItems.Count ? DropBase + row : -1;
            }
            if (!Bounds.Contains(p)) return -1;
            var local = new Point(p.X - Bounds.X, p.Y - Bounds.Y);
            for (var i = 0; i < _cells.Count; i++)
                if (!Buttons[i].Separator && _cells[i].Contains(local)) return i;
            return -1;
        }

        public Bitmap Render(bool dark)
        {
            var bmp = OverlayWindow.Canvas(WindowBounds.Size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            var bg = dark ? Color.FromArgb(250, 38, 40, 44) : Color.FromArgb(252, 252, 252, 253);
            var fg = dark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(32, 33, 36);
            var border = dark ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0);
            var accent = dark ? Color.FromArgb(110, 165, 245) : Color.FromArgb(31, 95, 191);
            var radius = 7 * Scale;
            using (var path = Rounded(new RectangleF(0.5f, 0.5f, Size.Width - 1, Size.Height - 1), radius))
            {
                using var b = new SolidBrush(bg);
                using var p = new Pen(border, 1);
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using var font = new Font("Segoe UI Symbol", 13f * Scale, GraphicsUnit.Pixel);
            using var small = new Font("Segoe UI", 9.5f * Scale, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            for (var i = 0; i < Buttons.Count; i++)
            {
                var btn = Buttons[i];
                var cell = _cells[i];
                if (btn.Separator)
                {
                    using var sp = new Pen(border, 1);
                    var cx = cell.X + cell.Width / 2f;
                    g.DrawLine(sp, cx, cell.Top + 5 * Scale, cx, cell.Bottom - 5 * Scale);
                    continue;
                }
                if (i == Hover || btn.Active)
                {
                    using var hb = new SolidBrush(Color.FromArgb(i == Hover ? 45 : 28, btn.Danger && i == Hover ? Color.Red : accent));
                    using var hp = Rounded(cell, 5 * Scale);
                    g.FillPath(hb, hp);
                }
                var content = cell;
                if (btn.Menu)
                {
                    // Caret of a choice button (opens the options as an icon row).
                    var caretW = (int)Math.Round(7 * Scale);
                    content = new Rectangle(cell.X, cell.Y, cell.Width - caretW, cell.Height);
                    using var cb = new SolidBrush(Color.FromArgb(150, fg));
                    float cx = cell.Right - caretW / 2f - 1 * Scale, cy = cell.Y + cell.Height / 2f + 1 * Scale, h = 2.2f * Scale;
                    g.FillPolygon(cb, new[] { new PointF(cx - h, cy - h * 0.6f), new PointF(cx + h, cy - h * 0.6f), new PointF(cx, cy + h * 0.8f) });
                }
                if (btn.IconId is not null)
                {
                    var ink = btn.Danger ? Color.FromArgb(200, 40, 40) : btn.Active ? accent : fg;
                    var pad = (int)Math.Round(5 * Scale);
                    if (btn.ShowLabel && btn.Glyph.Length > 0)
                    {
                        // Pictogram plus a short label.
                        var square = new Rectangle(content.X, content.Y, content.Height, content.Height);
                        IconPainter.Draw(g, btn.IconId, Rectangle.Inflate(square, -pad, -pad), ink, btn.Mirror);
                        using var lb = new SolidBrush(ink);
                        using var labelFont = new Font("Segoe UI", 10f * Scale, GraphicsUnit.Pixel);
                        var lf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                        g.DrawString(btn.Glyph, labelFont, lb, new RectangleF(square.Right - 2 * Scale, content.Y, content.Right - square.Right + 2 * Scale, content.Height), lf);
                        continue;
                    }
                    IconPainter.Draw(g, btn.IconId, Rectangle.Inflate(content, -pad, -pad), ink, btn.Mirror);
                    continue;
                }
                if (btn.Icon is not null)
                {
                    var inset = (int)Math.Round(4 * Scale);
                    var box = Rectangle.Inflate(content, -inset, -inset);
                    var side = Math.Min(box.Width, box.Height);
                    g.DrawImage(btn.Icon, new Rectangle(box.X + (box.Width - side) / 2, box.Y + (box.Height - side) / 2, side, side));
                    continue;
                }
                var color = btn.Danger ? Color.FromArgb(200, 40, 40) : btn.Active ? accent : fg;
                using var tb = new SolidBrush(color);
                g.DrawString(btn.Glyph, btn.Glyph.Length > 1 ? small : font, tb, cell, fmt);
            }
            if (DropAnchor >= 0) { DrawDropdown(g, dark, bg, fg, border, accent); return bmp; }
            // Tooltip for the hovered button, below the bar.
            if (Hover >= 0 && Hover < Buttons.Count && !string.IsNullOrEmpty(Buttons[Hover].Tip))
            {
                using var tipFont = new Font("Segoe UI", 9f * Scale, GraphicsUnit.Pixel);
                var text = Buttons[Hover].Tip;
                var sz = g.MeasureString(text, tipFont);
                var cell = _cells[Hover];
                var tw = sz.Width + 12 * Scale;
                var tx = Math.Max(0, Math.Min(WindowBounds.Width - tw - 1, cell.X + cell.Width / 2f - tw / 2));
                var tr = new RectangleF(tx, Size.Height + 3 * Scale, tw, sz.Height + 6 * Scale);
                using var tp = Rounded(tr, 4 * Scale);
                using var tbg = new SolidBrush(dark ? Color.FromArgb(245, 60, 62, 66) : Color.FromArgb(245, 50, 50, 54));
                g.FillPath(tbg, tp);
                using var tfg = new SolidBrush(Color.White);
                g.DrawString(text, tipFont, tfg, tr.X + 6 * Scale, tr.Y + 3 * Scale);
            }
            return bmp;
        }

        /// <summary>The open dropdown: a panel under its button, one row per option (icon, label), current option marked.</summary>
        private void DrawDropdown(Graphics g, bool dark, Color bg, Color fg, Color border, Color accent)
        {
            using (var path = Rounded(new RectangleF(_drop.X + 0.5f, _drop.Y + 0.5f, _drop.Width - 1, _drop.Height - 1), 6 * Scale))
            {
                using var b = new SolidBrush(bg);
                using var p = new Pen(border, 1);
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using var font = new Font("Segoe UI", 10f * Scale, GraphicsUnit.Pixel);
            using var bold = new Font("Segoe UI", 10f * Scale, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var pad = (int)Math.Round(4 * Scale);
            for (var i = 0; i < DropItems.Count; i++)
            {
                var item = DropItems[i];
                var row = new Rectangle(_drop.X + pad, _drop.Y + pad + i * _rowH, _drop.Width - 2 * pad, _rowH);
                if (Hover == DropBase + i || item.Active)
                {
                    using var hb = new SolidBrush(Color.FromArgb(Hover == DropBase + i ? 45 : 26, accent));
                    using var hp = Rounded(row, 4 * Scale);
                    g.FillPath(hb, hp);
                }
                var ink = item.Active ? accent : fg;
                var iconBox = new Rectangle(row.X + (int)(4 * Scale), row.Y + (int)(3 * Scale), _rowH - (int)(6 * Scale), _rowH - (int)(6 * Scale));
                if (item.IconId is not null) IconPainter.Draw(g, item.IconId, iconBox, ink, item.Mirror);
                else if (item.Icon is not null) g.DrawImage(item.Icon, iconBox);
                var hasIcon = item.IconId is not null || item.Icon is not null;
                var textX = hasIcon ? iconBox.Right + (int)(8 * Scale) : row.X + (int)(10 * Scale);
                using var tb = new SolidBrush(ink);
                g.DrawString(item.Label, item.Active ? bold : font, tb, new RectangleF(textX, row.Y, row.Right - textX - 4 * Scale, row.Height), fmt);
            }
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
