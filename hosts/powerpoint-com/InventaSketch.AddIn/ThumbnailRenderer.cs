using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.AddIn
{
    /// <summary>Renders engine primitives with GDI+ (ribbon images, later the on-slide overlay preview).</summary>
    internal static class ThumbnailRenderer
    {
        public static Bitmap Render(IReadOnlyList<Primitive> prims, int size, Color ink, Color background)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(background);
            var bounds = Prim.Bounds(prims);
            if (bounds is not Core.Rect b || b.W <= 0 && b.H <= 0) return bmp;
            var pad = size * 0.1f;
            // Tiny elements (a junction dot) keep a small size instead of filling the thumbnail as a black blob
            // (maintainer 2026-11-25): the scale treats anything smaller than MinExtent mm as that big.
            const double MinExtent = 7;
            var extent = Math.Max(MinExtent, Math.Max(b.W, b.H));
            var scale = (float)Math.Min((size - 2 * pad) / Math.Max(Math.Max(b.W, 1e-3), b.W < MinExtent && b.H < MinExtent ? extent : 0),
                                        (size - 2 * pad) / Math.Max(Math.Max(b.H, 1e-3), b.W < MinExtent && b.H < MinExtent ? extent : 0));
            g.TranslateTransform((float)(size / 2.0 - (b.X + b.W / 2) * scale), (float)(size / 2.0 - (b.Y + b.H / 2) * scale));
            g.ScaleTransform(scale, scale);
            // Thumbnails use a fixed pixel line width so tiny and large symbols stay readable.
            var px = (size <= 36 ? 1.7f : 1.2f) / scale;
            foreach (var p in Prim.Flatten(prims)) Draw(g, p, ink, px);
            return bmp;
        }

        /// <summary>Draws primitives with the current Graphics transform; <paramref name="px"/> is the line width in world units.</summary>
        public static void DrawAll(Graphics g, IEnumerable<Primitive> prims, Color ink, float px)
        {
            foreach (var p in Prim.Flatten(prims)) Draw(g, p, ink, px);
        }

        private static void Draw(Graphics g, Primitive p, Color ink, float px)
        {
            if (p is LinePrim or PathPrim)
            {
                // Arrowheads (including hollow and crow's-foot ends) as explicit geometry from the engine.
                var (shaft, heads) = ArrowHeads.Explode(p, Hex(ink));
                if (heads.Count > 0)
                {
                    Draw(g, shaft, ink, px);
                    foreach (var h in Prim.Flatten(heads)) Draw(g, h, ink, px);
                    return;
                }
            }
            switch (p)
            {
                case LinePrim l:
                    using (var pen = PenFor(l.Style, ink, px)) g.DrawLine(pen, (float)l.A.X, (float)l.A.Y, (float)l.B.X, (float)l.B.Y);
                    break;
                case RectPrim r when r.Radius > 0:
                    {
                        // Rounded corners: fill and outline the rounded shape, not its bounding rectangle.
                        using var shape = new GraphicsPath();
                        var d = (float)Math.Min(2 * r.Radius, Math.Min(r.W, r.H));
                        float x = (float)r.X, y = (float)r.Y, w = (float)r.W, h = (float)r.H;
                        shape.AddArc(x, y, d, d, 180, 90);
                        shape.AddArc(x + w - d, y, d, d, 270, 90);
                        shape.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                        shape.AddArc(x, y + h - d, d, d, 90, 90);
                        shape.CloseFigure();
                        Fill(g, r.Style, ink, shape.GetBounds(), shape);
                        using var pen = PenFor(r.Style, ink, px);
                        if (r.Style.HasStroke) g.DrawPath(pen, shape);
                        break;
                    }
                case RectPrim r:
                    Fill(g, r.Style, ink, new RectangleF((float)r.X, (float)r.Y, (float)r.W, (float)r.H), null);
                    using (var pen = PenFor(r.Style, ink, px)) if (r.Style.HasStroke) g.DrawRectangle(pen, (float)r.X, (float)r.Y, (float)r.W, (float)r.H);
                    break;
                case EllipsePrim e:
                    {
                        var rect = new RectangleF((float)(e.Center.X - e.Rx), (float)(e.Center.Y - e.Ry), (float)(2 * e.Rx), (float)(2 * e.Ry));
                        // Filled as an ellipse (it was filled as its bounding rectangle: white squares behind round symbols).
                        using (var shape = new GraphicsPath()) { shape.AddEllipse(rect); Fill(g, e.Style, ink, rect, shape); }
                        using var pen = PenFor(e.Style, ink, px);
                        if (e.Style.HasStroke) g.DrawEllipse(pen, rect);
                        break;
                    }
                case PathPrim path:
                    {
                        using var gp = ToPath(path);
                        Fill(g, path.Style, ink, gp.GetBounds(), gp);
                        using var pen = PenFor(path.Style, ink, px);
                        if (path.Style.HasStroke) g.DrawPath(pen, gp);
                        break;
                    }
                case TextPrim t:
                    {
                        var fs = (t.TextStyle.Bold ? FontStyle.Bold : FontStyle.Regular) | (t.TextStyle.Italic ? FontStyle.Italic : 0) | (t.TextStyle.Underline ? FontStyle.Underline : 0);
                        using var font = new Font("Arial", (float)Math.Max(0.5, t.TextStyle.Size * 0.75), fs, GraphicsUnit.World);
                        using var brush = new SolidBrush(ink);
                        var fmt = new StringFormat { Alignment = t.TextStyle.Anchor switch { TextAnchor.Start => StringAlignment.Near, TextAnchor.End => StringAlignment.Far, _ => StringAlignment.Center }, LineAlignment = StringAlignment.Center };
                        g.DrawString(t.Text, font, brush, (float)t.Position.X, (float)t.Position.Y, fmt);
                        break;
                    }
            }
        }

        private static void Fill(Graphics g, Core.Style s, Color ink, RectangleF rect, GraphicsPath? path)
        {
            var fill = s.EffectiveFill;
            if (fill.Kind == FillKind.Solid && fill.Color is not null)
            {
                using var b = new SolidBrush(ParseColor(fill.Color, ink));
                if (path is null) g.FillRectangle(b, rect); else g.FillPath(b, path);
            }
            else if (fill.Kind == FillKind.Hatch)
            {
                using var b = new HatchBrush(HatchStyle.WideUpwardDiagonal, ink, Color.Transparent);
                if (path is null) g.FillRectangle(b, rect); else g.FillPath(b, path);
            }
        }

        private static Pen PenFor(Core.Style s, Color ink, float px)
        {
            var pen = new Pen(ink, px);
            if (s.EffectiveDash != Core.DashStyle.Solid) pen.DashPattern = new[] { 4f, 2f };
            return pen;
        }

        private static GraphicsPath ToPath(PathPrim path)
        {
            var gp = new GraphicsPath();
            PointF last = default;
            PointF P(Vec2 v) => new((float)v.X, (float)v.Y);
            foreach (var s in path.Segments)
            {
                switch (s.Op)
                {
                    case SegOp.M: gp.StartFigure(); last = P(s.P); break;
                    case SegOp.L: gp.AddLine(last, P(s.P)); last = P(s.P); break;
                    case SegOp.C: gp.AddBezier(last, P(s.C1), P(s.C2), P(s.P)); last = P(s.P); break;
                    case SegOp.Q:
                        {
                            var c1 = new PointF(last.X + 2f / 3 * ((float)s.C1.X - last.X), last.Y + 2f / 3 * ((float)s.C1.Y - last.Y));
                            var c2 = new PointF((float)s.P.X + 2f / 3 * ((float)s.C1.X - (float)s.P.X), (float)s.P.Y + 2f / 3 * ((float)s.C1.Y - (float)s.P.Y));
                            gp.AddBezier(last, c1, c2, P(s.P)); last = P(s.P); break;
                        }
                    case SegOp.Z: gp.CloseFigure(); break;
                }
            }
            return gp;
        }

        private static string Hex(Color c) => "#" + c.R.ToString("x2", CultureInfo.InvariantCulture) + c.G.ToString("x2", CultureInfo.InvariantCulture) + c.B.ToString("x2", CultureInfo.InvariantCulture);

        public static Color ParseColor(string? hex, Color fallback)
        {
            if (hex is null || hex.Length < 7 || hex[0] != '#') return fallback;
            return int.TryParse(hex.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
                ? Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255)
                : fallback;
        }
    }
}
