using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>Primitives (page mm) that get a halo, and how strong it is (1 = selected, less = connected but not selected).</summary>
    internal sealed record HaloLayer(IReadOnlyList<Primitive> Prims, float Strength);

    /// <summary>A rendered halo: the bitmap and its top-left corner in screen pixels.</summary>
    internal sealed class HaloImage : IDisposable
    {
        public HaloImage(Bitmap bitmap, Point origin) { Bitmap = bitmap; Origin = origin; }
        public Bitmap Bitmap { get; }
        public Point Origin { get; }
        public void Dispose() => Bitmap.Dispose();
    }

    /// <summary>
    /// The selection look (maintainer 2026-11-25, like Slidemax's blue gradient): a soft blue glow along the exact outline of
    /// the selected elements – their rendered primitives widened and blurred – with the element's own strokes and the inside
    /// of its closed shapes kept clear, so the glow lies around the element as if behind it and shows which of overlapping
    /// elements is selected (also where its outline runs under another element). Connected elements that are not selected get a fainter glow. Rendered once per selection and view (the
    /// caller caches it); the blur runs on a half-resolution alpha mask.
    /// </summary>
    internal static class SelectionHalo
    {
        /// <summary>Extra width on each side of a stroke before blurring (logical pixels).</summary>
        internal const float Spread = 3.5f;
        /// <summary>Blur radius of the glow (logical pixels).</summary>
        internal const float Blur = 5f;
        /// <summary>Resolution divisor of the mask (the glow is soft, so half resolution is enough).</summary>
        internal const int Down = 2;

        /// <summary>Glow colour and its peak opacity: deeper blue on light slides, lighter blue on dark ones.</summary>
        internal static Color Tint(bool darkSlide) => darkSlide ? Color.FromArgb(150, 120, 185, 255) : Color.FromArgb(120, 0, 110, 230);

        /// <summary>Screen rectangle (device pixels) of primitives in page mm for a view at <paramref name="offsetX"/>, <paramref name="offsetY"/>, <paramref name="ppm"/> px per mm.</summary>
        internal static RectangleF? ScreenBounds(IEnumerable<Primitive> prims, double offsetX, double offsetY, double ppm)
        {
            if (Prim.Bounds(prims) is not Core.Rect r) return null;
            return new RectangleF((float)(offsetX + r.X * ppm), (float)(offsetY + r.Y * ppm), (float)(r.W * ppm), (float)(r.H * ppm));
        }

        /// <summary>
        /// Renders the halo of <paramref name="layers"/> for a view (page mm → screen: offset plus <paramref name="ppm"/> px per mm),
        /// clipped to <paramref name="clip"/> (screen pixels, the slide pane). Null if nothing is visible.
        /// </summary>
        internal static HaloImage? Render(IReadOnlyList<HaloLayer> layers, double offsetX, double offsetY, double ppm, float deviceScale, bool darkSlide, Rectangle clip)
        {
            var used = layers.Where(l => l.Prims.Count > 0 && l.Strength > 0).ToList();
            if (used.Count == 0 || ppm <= 0) return null;
            var spread = Spread * deviceScale;
            var blur = Blur * deviceScale;
            var pad = spread + 3 * blur + 2;
            RectangleF? all = null;
            foreach (var l in used)
                if (ScreenBounds(l.Prims, offsetX, offsetY, ppm) is { } b) all = all is { } a ? RectangleF.Union(a, b) : b;
            if (all is not { } box) return null;
            box.Inflate(pad, pad);
            var rect = Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(box.Left), (int)Math.Floor(box.Top), (int)Math.Ceiling(box.Right), (int)Math.Ceiling(box.Bottom)), clip);
            if (rect.Width <= 0 || rect.Height <= 0) return null;

            // Per layer (connected ones first, the selection last): a half-resolution mask of the widened strokes and outlines,
            // blurred, coloured and scaled up; then the layer's own element is cut out of its glow (strokes and the areas of
            // closed shapes), so the glow lies around the element as if behind it – but still shows over other elements, so
            // an outline running under another element stays visible.
            int w2 = (rect.Width + Down - 1) / Down, h2 = (rect.Height + Down - 1) / Down;
            var tint = Tint(darkSlide);
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (var final = Graphics.FromImage(bmp))
            {
                final.Clear(Color.Transparent);
                foreach (var layer in used)
                {
                    var mask = Mask(layer.Prims, w2, h2, (float)((offsetX - rect.X) / Down), (float)((offsetY - rect.Y) / Down), (float)(ppm / Down), spread / Down);
                    BlurAlpha(mask, w2, h2, Math.Max(1, (int)Math.Round(blur / Down)));
                    using var small = Colour(mask, w2, h2, tint, layer.Strength);
                    using var full = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(full))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(small, new Rectangle(0, 0, w2 * Down, h2 * Down));
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.TranslateTransform((float)(offsetX - rect.X), (float)(offsetY - rect.Y));
                        g.ScaleTransform((float)ppm, (float)ppm);
                        using var clear = new SolidBrush(Color.Transparent);
                        foreach (var p in Prim.Flatten(layer.Prims))
                        {
                            using var path = PathOf(p, out var filled, out var stroked, out var widthMm);
                            if (path is null) continue;
                            if (filled || IsArea(p)) g.FillPath(clear, path);
                            if (stroked)
                                using (var pen = new Pen(Color.Transparent, (float)Math.Max(widthMm, 1 / ppm)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                                    g.DrawPath(pen, path);
                        }
                    }
                    final.DrawImageUnscaled(full, 0, 0);
                }
            }
            return new HaloImage(bmp, rect.Location);
        }

        /// <summary>A closed shape (rectangle, ellipse, closed path): its inside belongs to the element, the glow stays outside.</summary>
        private static bool IsArea(Primitive p) => p is RectPrim or EllipsePrim || p is PathPrim path && path.Segments.Any(s => s.Op == SegOp.Z);

        /// <summary>The mask as a tinted bitmap: full strength right at the outline, then the soft fall-off of the blur.</summary>
        private static Bitmap Colour(byte[] mask, int w, int h, Color tint, float strength)
        {
            var pixels = new int[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                var a = (int)Math.Round(Math.Min(1f, mask[i] / 255f * 1.7f) * strength * tint.A);
                pixels[i] = a <= 0 ? 0 : (a << 24) | (tint.R << 16) | (tint.G << 8) | tint.B;
            }
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < h; y++) Marshal.Copy(pixels, y * w, data.Scan0 + y * data.Stride, w);
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }

        /// <summary>Alpha mask (0..255 per pixel) of the primitives drawn widened by <paramref name="spread"/> pixels on each side.</summary>
        private static byte[] Mask(IReadOnlyList<Primitive> prims, int w, int h, float dx, float dy, float ppm, float spread)
        {
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(dx, dy);
                g.ScaleTransform(ppm, ppm);
                using var brush = new SolidBrush(Color.White);
                foreach (var p in Prim.Flatten(prims))
                {
                    using var path = PathOf(p, out var filled, out _, out var widthMm);
                    if (path is null) continue;
                    // Outlines of filled shapes too (the glow runs around them); text is filled as well.
                    if (p is TextPrim) g.FillPath(brush, path);
                    using var pen = new Pen(Color.White, (float)(Math.Max(widthMm, 0.1) + 2 * spread / ppm)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawPath(pen, path);
                }
            }
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var raw = new int[w * h];
            try
            {
                for (var y = 0; y < h; y++) Marshal.Copy(data.Scan0 + y * data.Stride, raw, y * w, w);
            }
            finally { bmp.UnlockBits(data); }
            var mask = new byte[w * h];
            for (var i = 0; i < raw.Length; i++) mask[i] = (byte)((raw[i] >> 24) & 0xFF);
            return mask;
        }

        /// <summary>
        /// Blurs an alpha mask in place: three passes of a separable box blur of <paramref name="radius"/> pixels (close to a
        /// Gaussian), edges clamped.
        /// </summary>
        internal static void BlurAlpha(byte[] a, int w, int h, int radius)
        {
            if (radius < 1 || w <= 0 || h <= 0) return;
            var tmp = new byte[a.Length];
            for (var pass = 0; pass < 3; pass++)
            {
                BoxLine(a, tmp, w, h, radius, horizontal: true);
                BoxLine(tmp, a, w, h, radius, horizontal: false);
            }
        }

        private static void BoxLine(byte[] src, byte[] dst, int w, int h, int r, bool horizontal)
        {
            int lines = horizontal ? h : w, len = horizontal ? w : h;
            var div = 2 * r + 1;
            for (var line = 0; line < lines; line++)
            {
                int Idx(int k) { k = k < 0 ? 0 : k >= len ? len - 1 : k; return horizontal ? line * w + k : k * w + line; }
                var sum = 0;
                for (var k = -r; k <= r; k++) sum += src[Idx(k)];
                for (var k = 0; k < len; k++)
                {
                    dst[Idx(k)] = (byte)((sum + div / 2) / div);
                    sum += src[Idx(k + r + 1)] - src[Idx(k - r)];
                }
            }
        }

        /// <summary>
        /// The outline of a primitive as a path in page mm: whether it has a visible fill and stroke, and its stroke width.
        /// Null for primitives without geometry.
        /// </summary>
        internal static GraphicsPath? PathOf(Primitive p, out bool filled, out bool stroked, out double widthMm)
        {
            filled = false; stroked = false; widthMm = 0;
            static PointF P(Vec2 v) => new((float)v.X, (float)v.Y);
            Core.Style? style = p switch { LinePrim l => l.Style, PathPrim a => a.Style, EllipsePrim e => e.Style, RectPrim r => r.Style, _ => null };
            if (style is not null)
            {
                filled = style.EffectiveFill.Kind != FillKind.None;
                stroked = style.HasStroke;
                widthMm = style.EffectiveWidth;
            }
            var gp = new GraphicsPath();
            switch (p)
            {
                case LinePrim l:
                    gp.AddLine(P(l.A), P(l.B));
                    filled = false;
                    break;
                case RectPrim r when r.Radius > 0:
                    {
                        var d = (float)Math.Min(2 * r.Radius, Math.Min(r.W, r.H));
                        float x = (float)r.X, y = (float)r.Y, w = (float)r.W, h = (float)r.H;
                        if (d <= 0) { gp.AddRectangle(new RectangleF(x, y, w, h)); break; }
                        gp.AddArc(x, y, d, d, 180, 90);
                        gp.AddArc(x + w - d, y, d, d, 270, 90);
                        gp.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                        gp.AddArc(x, y + h - d, d, d, 90, 90);
                        gp.CloseFigure();
                        break;
                    }
                case RectPrim r:
                    gp.AddRectangle(new RectangleF((float)r.X, (float)r.Y, (float)Math.Max(r.W, 1e-4), (float)Math.Max(r.H, 1e-4)));
                    break;
                case EllipsePrim e:
                    gp.AddEllipse((float)(e.Center.X - e.Rx), (float)(e.Center.Y - e.Ry), (float)Math.Max(2 * e.Rx, 1e-4), (float)Math.Max(2 * e.Ry, 1e-4));
                    break;
                case PathPrim path:
                    {
                        PointF last = default;
                        var any = false;
                        foreach (var s in path.Segments)
                        {
                            switch (s.Op)
                            {
                                case SegOp.M: gp.StartFigure(); last = P(s.P); break;
                                case SegOp.L: gp.AddLine(last, P(s.P)); last = P(s.P); any = true; break;
                                case SegOp.C: gp.AddBezier(last, P(s.C1), P(s.C2), P(s.P)); last = P(s.P); any = true; break;
                                case SegOp.Q:
                                    {
                                        var c1 = new PointF(last.X + 2f / 3 * ((float)s.C1.X - last.X), last.Y + 2f / 3 * ((float)s.C1.Y - last.Y));
                                        var c2 = new PointF((float)s.P.X + 2f / 3 * ((float)s.C1.X - (float)s.P.X), (float)s.P.Y + 2f / 3 * ((float)s.C1.Y - (float)s.P.Y));
                                        gp.AddBezier(last, c1, c2, P(s.P)); last = P(s.P); any = true; break;
                                    }
                                case SegOp.Z: gp.CloseFigure(); break;
                            }
                        }
                        if (!any) { gp.Dispose(); return null; }
                        break;
                    }
                case TextPrim t when !string.IsNullOrWhiteSpace(t.Text):
                    {
                        var fs = (t.TextStyle.Bold ? FontStyle.Bold : FontStyle.Regular) | (t.TextStyle.Italic ? FontStyle.Italic : 0);
                        using var family = new FontFamily("Arial");
                        using var fmt = new StringFormat
                        {
                            Alignment = t.TextStyle.Anchor switch { TextAnchor.Start => StringAlignment.Near, TextAnchor.End => StringAlignment.Far, _ => StringAlignment.Center },
                            LineAlignment = StringAlignment.Center,
                        };
                        gp.AddString(t.Text, family, (int)fs, (float)Math.Max(0.5, t.TextStyle.Size * 0.75), P(t.Position), fmt);
                        if (Math.Abs(t.Rotation) > 1e-9)
                            using (var m = new Matrix()) { m.RotateAt((float)t.Rotation, P(t.Position)); gp.Transform(m); }
                        filled = true; stroked = false; widthMm = 0;
                        break;
                    }
                default:
                    gp.Dispose();
                    return null;
            }
            return gp;
        }
    }
}
