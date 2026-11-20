using System;
using System.Collections.Generic;
using System.Drawing;
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
    }

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
                // Text buttons (view names) get a width that fits the label; symbol buttons are square.
                var width = b.Icon is null && b.IconId is null && b.Glyph.Length > 2 ? (int)Math.Round(Math.Max(size, b.Glyph.Length * 6.6 * scale + 14 * scale)) : size;
                _cells.Add(new Rectangle(x, pad, width, size));
                x += width + gap;
            }
            Size = new Size(x - gap + pad, size + 2 * pad);
            TipHeight = (int)Math.Round(26 * scale);
        }

        /// <summary>Height of the tooltip strip below the bar (part of the window, never hit-tested).</summary>
        public int TipHeight { get; }

        /// <summary>Window rectangle: bar plus tooltip strip.</summary>
        public Rectangle WindowBounds => new(Bounds.X, Bounds.Y, Math.Max(Bounds.Width, (int)(260 * Scale)), Bounds.Height + TipHeight);

        public IReadOnlyList<BarButton> Buttons { get; }
        public float Scale { get; }
        public Size Size { get; }
        /// <summary>Bar position on screen.</summary>
        public Rectangle Bounds { get; private set; }
        public int Hover { get; set; } = -1;

        /// <summary>Centres the bar above the selection, or below it if there is no room; clamped to the pane.</summary>
        public void Place(RectangleF selection, Rectangle pane)
        {
            // Above the selection, clear of PowerPoint's rotation handle (about 35 logical px above the frame).
            var gapAbove = (int)Math.Round(46 * Scale);
            var gapBelow = (int)Math.Round(14 * Scale);
            int x = (int)Math.Round(selection.X + selection.Width / 2 - Size.Width / 2.0);
            int y = (int)Math.Round(selection.Y) - gapAbove - Size.Height;
            if (y < pane.Top + 2) y = (int)Math.Round(selection.Bottom) + gapBelow;
            var gap = gapBelow;
            if (y + Size.Height > pane.Bottom - 2) y = Math.Max(pane.Top + 2, (int)Math.Round(selection.Y) + gap);
            x = Math.Max(pane.Left + 2, Math.Min(pane.Right - Size.Width - 2, x));
            Bounds = new Rectangle(x, y, Size.Width, Size.Height);
        }

        public bool Contains(Point p) => Bounds.Contains(p);

        /// <summary>Distance from a screen point to the bar in logical pixels (0 inside).</summary>
        public double Distance(Point p)
        {
            double dx = Math.Max(0, Math.Max(Bounds.Left - p.X, p.X - Bounds.Right));
            double dy = Math.Max(0, Math.Max(Bounds.Top - p.Y, p.Y - Bounds.Bottom));
            return Math.Sqrt(dx * dx + dy * dy) / Scale;
        }

        public int HitTest(Point p)
        {
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
            var accent = Color.FromArgb(31, 95, 191);
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
                if (btn.IconId is not null)
                {
                    var ink = btn.Danger ? Color.FromArgb(200, 40, 40) : btn.Active ? accent : fg;
                    var pad = (int)Math.Round(5 * Scale);
                    IconPainter.Draw(g, btn.IconId, Rectangle.Inflate(cell, -pad, -pad), ink);
                    continue;
                }
                if (btn.Icon is not null)
                {
                    var inset = (int)Math.Round(4 * Scale);
                    g.DrawImage(btn.Icon, Rectangle.Inflate(cell, -inset, -inset));
                    continue;
                }
                var color = btn.Danger ? Color.FromArgb(200, 40, 40) : btn.Active ? accent : fg;
                using var tb = new SolidBrush(color);
                g.DrawString(btn.Glyph, btn.Glyph.Length > 1 ? small : font, tb, cell, fmt);
            }
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
