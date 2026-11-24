using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// The shared set of abstract line pictograms (<see cref="IconIds"/>) for the floating bar and the pie menu: one stroke
    /// weight, square 24-unit grid, no element-specific detail, so icons stay recognisable at 16–24 px.
    /// </summary>
    internal static class IconPainter
    {
        public static string ForView(ViewDef v) => IconIds.ForView(v);

        /// <summary>Draws the icon into <paramref name="r"/> with the given ink.</summary>
        public static void Draw(Graphics g, string iconId, RectangleF r, Color ink, bool mirror = false)
        {
            var s = Math.Min(r.Width, r.Height) / 24f;
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X + (r.Width - 24 * s) / 2, r.Y + (r.Height - 24 * s) / 2);
            g.ScaleTransform(s, s);
            // Mirrored option pictograms (start arrowheads point left).
            if (mirror) { g.TranslateTransform(24, 0); g.ScaleTransform(-1, 1); }
            using var pen = new Pen(ink, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var thin = new Pen(ink, 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var dash = new Pen(ink, 1.1f) { DashPattern = new[] { 3.5f, 1.6f, 1f, 1.6f } };
            using var brush = new SolidBrush(ink);
            switch (iconId)
            {
                case IconIds.Schematic:
                    g.DrawLine(pen, 12, 4, 12, 20); g.DrawLine(pen, 8, 4, 16, 4); g.DrawLine(pen, 8, 20, 16, 20);
                    g.DrawLine(dash, 3, 12, 21, 12); g.DrawLine(thin, 10, 10, 14, 14); g.DrawLine(thin, 14, 10, 10, 14);
                    break;
                case IconIds.Side:
                    g.DrawRectangle(pen, 8, 3, 8, 18); g.DrawLine(dash, 3, 12, 21, 12);
                    break;
                case IconIds.Section:
                    g.DrawRectangle(pen, 8, 3, 8, 18);
                    for (var y = 5; y < 21; y += 4) g.DrawLine(thin, 9, y + 3, 15, y - 1);
                    g.DrawLine(dash, 3, 12, 21, 12);
                    break;
                case IconIds.Axial:
                    g.DrawEllipse(pen, 4, 4, 16, 16); g.DrawLine(dash, 2, 12, 22, 12); g.DrawLine(dash, 12, 2, 12, 22);
                    break;
                case IconIds.Teeth:
                    {
                        g.DrawEllipse(pen, 6, 6, 12, 12);
                        for (var i = 0; i < 8; i++)
                        {
                            var a = i * Math.PI / 4;
                            float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                            g.DrawLine(pen, 12 + 6 * c, 12 + 6 * sn, 12 + 9.5f * c, 12 + 9.5f * sn);
                        }
                        g.FillEllipse(brush, 10.5f, 10.5f, 3, 3);
                        break;
                    }
                case IconIds.Top:
                    g.DrawRectangle(pen, 5, 9, 14, 12); g.DrawLine(pen, 12, 2, 12, 7); g.DrawLine(pen, 9.5f, 4.5f, 12, 7); g.DrawLine(pen, 14.5f, 4.5f, 12, 7);
                    break;
                case IconIds.Front:
                    g.DrawRectangle(pen, 9, 5, 12, 14); g.DrawLine(pen, 2, 12, 7, 12); g.DrawLine(pen, 4.5f, 9.5f, 7, 12); g.DrawLine(pen, 4.5f, 14.5f, 7, 12);
                    break;
                case IconIds.Rear:
                    g.DrawRectangle(pen, 3, 5, 12, 14); g.DrawLine(pen, 22, 12, 17, 12); g.DrawLine(pen, 19.5f, 9.5f, 17, 12); g.DrawLine(pen, 19.5f, 14.5f, 17, 12);
                    break;
                case IconIds.Iso:
                    g.DrawPolygon(pen, new[] { new PointF(12, 3), new PointF(20, 7.5f), new PointF(20, 16.5f), new PointF(12, 21), new PointF(4, 16.5f), new PointF(4, 7.5f) });
                    g.DrawLine(thin, 12, 12, 12, 21); g.DrawLine(thin, 12, 12, 4, 7.5f); g.DrawLine(thin, 12, 12, 20, 7.5f);
                    break;
                case IconIds.Detailed:
                    g.DrawRectangle(pen, 4, 5, 16, 14); g.DrawLine(thin, 7, 9, 17, 9); g.DrawLine(thin, 7, 12, 14, 12); g.DrawLine(thin, 7, 15, 16, 15);
                    break;
                case IconIds.Simplified:
                    g.DrawRectangle(pen, 5, 5, 14, 14); g.DrawLine(thin, 5, 5, 19, 19); g.DrawLine(thin, 19, 5, 5, 19);
                    break;
                case IconIds.Symbol:
                    g.DrawRectangle(pen, 8, 8, 8, 8); g.DrawLine(pen, 2, 12, 8, 12); g.DrawLine(pen, 16, 12, 22, 12);
                    break;
                case IconIds.Block:
                    g.DrawRectangle(pen, 2, 8, 7, 8); g.DrawRectangle(pen, 15, 8, 7, 8); g.DrawLine(thin, 9, 12, 15, 12);
                    break;
                case IconIds.Plan:
                    g.DrawLines(pen, new[] { new PointF(4, 20), new PointF(4, 4), new PointF(20, 4), new PointF(20, 13) });
                    g.DrawLine(pen, 4, 20, 13, 20); g.DrawArc(thin, 13, 13, 14, 14, 180, 90);
                    break;
                case IconIds.Exploded:
                    g.DrawRectangle(pen, 3, 9, 4, 6); g.DrawRectangle(pen, 10, 7, 4, 10); g.DrawRectangle(pen, 17, 9, 4, 6);
                    break;
                case IconIds.Table:
                    g.DrawRectangle(pen, 4, 5, 16, 14); g.DrawLine(thin, 4, 9.5f, 20, 9.5f); g.DrawLine(thin, 4, 14, 20, 14); g.DrawLine(thin, 10, 9.5f, 10, 19);
                    break;
                case IconIds.RotateCcw:
                    RotateArrow(g, pen, brush, clockwise: false);
                    break;
                case IconIds.Increase:
                    g.DrawLine(pen, 12, 5, 12, 19); g.DrawLine(pen, 5, 12, 19, 12);
                    break;
                case IconIds.Decrease:
                    g.DrawLine(pen, 5, 12, 19, 12);
                    break;
                case IconIds.Delete:
                    g.DrawLine(pen, 6, 6, 18, 18); g.DrawLine(pen, 18, 6, 6, 18);
                    break;
                case IconIds.Duplicate:
                    g.DrawRectangle(pen, 4, 4, 11, 11); g.DrawRectangle(pen, 9, 9, 11, 11);
                    break;
                case IconIds.MirrorH:
                    g.DrawLine(dash, 12, 3, 12, 21);
                    g.DrawPolygon(pen, new[] { new PointF(9, 6), new PointF(9, 18), new PointF(3, 18) });
                    g.DrawPolygon(pen, new[] { new PointF(15, 6), new PointF(15, 18), new PointF(21, 18) });
                    break;
                case IconIds.MirrorV:
                    g.DrawLine(dash, 3, 12, 21, 12);
                    g.DrawPolygon(pen, new[] { new PointF(6, 9), new PointF(18, 9), new PointF(18, 3) });
                    g.DrawPolygon(pen, new[] { new PointF(6, 15), new PointF(18, 15), new PointF(18, 21) });
                    break;
                case IconIds.Rotate:
                    RotateArrow(g, pen, brush, clockwise: true);
                    break;
                case IconIds.Swap:
                    g.DrawLine(pen, 4, 9, 20, 9); g.DrawLines(pen, new[] { new PointF(16, 5), new PointF(20, 9), new PointF(16, 13) });
                    g.DrawLine(pen, 20, 15, 4, 15); g.DrawLines(pen, new[] { new PointF(8, 11), new PointF(4, 15), new PointF(8, 19) });
                    break;
                case IconIds.ArrowStart:
                    g.DrawLine(pen, 6, 12, 21, 12); g.FillPolygon(brush, new[] { new PointF(3, 12), new PointF(9, 8), new PointF(9, 16) });
                    break;
                case IconIds.ArrowEnd:
                    g.DrawLine(pen, 3, 12, 18, 12); g.FillPolygon(brush, new[] { new PointF(21, 12), new PointF(15, 8), new PointF(15, 16) });
                    break;
                case IconIds.Text:
                    g.DrawLine(pen, 5, 5, 19, 5); g.DrawLine(pen, 12, 5, 12, 20);
                    break;
                case IconIds.Toggle:
                    g.DrawEllipse(pen, 3, 7, 18, 10); g.FillEllipse(brush, 13, 9, 6, 6);
                    break;
                case IconIds.Cycle:
                    // Two arcs chasing each other clockwise, each ending in a tangent arrowhead.
                    ArcArrow(g, pen, brush, 200, 130, 2.6f, 1.6f, 2.6f);
                    ArcArrow(g, pen, brush, 20, 130, 2.6f, 1.6f, 2.6f);
                    break;
                case IconIds.Connect:
                    g.FillEllipse(brush, 2, 15, 6, 6); g.FillEllipse(brush, 16, 3, 6, 6); g.DrawBezier(pen, 5, 18, 12, 18, 12, 6, 19, 6);
                    break;
                case IconIds.Animate:
                    g.DrawPolygon(pen, new[] { new PointF(7, 4), new PointF(19, 12), new PointF(7, 20) });
                    break;
                case IconIds.Pin:
                    {
                        using var pinPath = new GraphicsPath();
                        pinPath.AddArc(6, 3, 12, 12, 150, 240);
                        pinPath.AddLine(17.2f, 12, 12, 21);
                        pinPath.AddLine(12, 21, 6.8f, 12);
                        pinPath.CloseFigure();
                        g.DrawPath(pen, pinPath);
                        g.FillEllipse(brush, 10, 7, 4, 4);
                        break;
                    }
                case IconIds.Medium:
                    {
                        using var drop = new GraphicsPath();
                        drop.AddBezier(12, 3, 9, 8, 6, 11, 6, 14.5f);
                        drop.AddArc(6, 8.5f, 12, 12, 180, -180);
                        drop.AddBezier(18, 14.5f, 18, 11, 15, 8, 12, 3);
                        g.DrawPath(pen, drop);
                        break;
                    }
                case IconIds.Insert:
                    g.DrawRectangle(pen, 4, 4, 16, 16); g.DrawLine(pen, 12, 8, 12, 16); g.DrawLine(pen, 8, 12, 16, 12);
                    break;
                case IconIds.Actuation:
                    g.DrawLine(pen, 8, 4, 16, 4); g.DrawLine(pen, 12, 4, 12, 11); g.DrawRectangle(pen, 5, 11, 14, 9);
                    break;
                // ---- option pictograms: a short line with the feature on the right
                case IconIds.HeadNone:
                    g.DrawLine(pen, 3, 12, 21, 12);
                    break;
                case IconIds.HeadTriangle:
                    g.DrawLine(pen, 3, 12, 14, 12);
                    g.FillPolygon(brush, new[] { new PointF(13, 7), new PointF(21.5f, 12), new PointF(13, 17) });
                    break;
                case IconIds.HeadOpen:
                    g.DrawLine(pen, 3, 12, 20.5f, 12);
                    g.DrawLines(pen, new[] { new PointF(14, 7), new PointF(20.5f, 12), new PointF(14, 17) });
                    break;
                case IconIds.HeadStealth:
                    g.DrawLine(pen, 3, 12, 16, 12);
                    g.FillPolygon(brush, new[] { new PointF(12.5f, 6.5f), new PointF(21.5f, 12), new PointF(12.5f, 17.5f), new PointF(15.5f, 12) });
                    break;
                case IconIds.HeadDiamond:
                    g.DrawLine(pen, 3, 12, 12, 12);
                    g.FillPolygon(brush, new[] { new PointF(11.5f, 12), new PointF(16.5f, 8), new PointF(21.5f, 12), new PointF(16.5f, 16) });
                    break;
                case IconIds.HeadDiamondHollow:
                    g.DrawLine(pen, 3, 12, 11.5f, 12);
                    g.DrawPolygon(pen, new[] { new PointF(11.5f, 12), new PointF(16.5f, 8), new PointF(21, 12), new PointF(16.5f, 16) });
                    break;
                case IconIds.HeadDot:
                    g.DrawLine(pen, 3, 12, 15, 12);
                    g.FillEllipse(brush, 14.5f, 8.5f, 7, 7);
                    break;
                case IconIds.HeadTick:
                    g.DrawLine(pen, 3, 12, 21, 12);
                    g.DrawLine(pen, 15, 17, 21, 7);
                    break;
                case IconIds.HeadHalf:
                    g.DrawLine(pen, 3, 12, 21, 12);
                    g.FillPolygon(brush, new[] { new PointF(13, 6.5f), new PointF(21.5f, 12), new PointF(13, 12) });
                    break;
                case IconIds.HeadTriangleHollow:
                    g.DrawLine(pen, 3, 12, 13.5f, 12);
                    g.DrawPolygon(pen, new[] { new PointF(13.5f, 7.5f), new PointF(21, 12), new PointF(13.5f, 16.5f) });
                    break;
                case IconIds.SizeSmall:
                case IconIds.SizeMedium:
                case IconIds.SizeLarge:
                    {
                        var h = iconId == IconIds.SizeSmall ? 2.6f : iconId == IconIds.SizeMedium ? 4.4f : 6.4f;
                        g.DrawLine(pen, 3, 12, 21.5f - 1.6f * h, 12);
                        g.FillPolygon(brush, new[] { new PointF(21.5f - 1.8f * h, 12 - h), new PointF(21.5f, 12), new PointF(21.5f - 1.8f * h, 12 + h) });
                        break;
                    }
                case IconIds.LineSolid:
                    g.DrawLine(pen, 3, 12, 21, 12);
                    break;
                case IconIds.LineDash:
                    g.DrawLine(pen, 3, 12, 7, 12); g.DrawLine(pen, 10, 12, 14, 12); g.DrawLine(pen, 17, 12, 21, 12);
                    break;
                case IconIds.LineDot:
                    for (var x = 3.5f; x <= 21; x += 4.3f) g.FillEllipse(brush, x - 1.2f, 10.8f, 2.4f, 2.4f);
                    break;
                case IconIds.LineDashDot:
                    g.DrawLine(pen, 3, 12, 8, 12); g.FillEllipse(brush, 10, 10.8f, 2.4f, 2.4f); g.DrawLine(pen, 14.5f, 12, 21, 12);
                    break;
                case IconIds.LineDashDotDot:
                    g.DrawLine(pen, 3, 12, 7, 12); g.FillEllipse(brush, 8.8f, 10.8f, 2.4f, 2.4f); g.FillEllipse(brush, 12.6f, 10.8f, 2.4f, 2.4f); g.DrawLine(pen, 17, 12, 21, 12);
                    break;
                case IconIds.LineLongDash:
                    g.DrawLine(pen, 3, 12, 10, 12); g.DrawLine(pen, 14, 12, 21, 12);
                    break;
                case IconIds.CourseStraight:
                    g.DrawLine(pen, 3, 18, 21, 6);
                    break;
                case IconIds.CourseCurved:
                    g.DrawBezier(pen, 3, 18, 6, 7, 13, 5, 21, 6);
                    break;
                case IconIds.CourseWavy:
                    g.DrawBezier(pen, 3, 17, 10, 2, 14, 22, 21, 7);
                    break;
                case IconIds.CourseZigzag:
                    g.DrawLines(pen, new[] { new PointF(3, 12), new PointF(6.5f, 7), new PointF(10, 17), new PointF(14, 7), new PointF(17.5f, 17), new PointF(21, 12) });
                    break;
                case IconIds.MachInner:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.FillRectangle(brush, 9, 8, 6, 2.2f); g.FillRectangle(brush, 9, 13.8f, 6, 2.2f);
                    g.DrawRectangle(pen, 8, 3.5f, 8, 2.6f); g.DrawRectangle(pen, 8, 17.9f, 8, 2.6f);
                    break;
                case IconIds.MachOuter:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.DrawRectangle(pen, 9, 7.5f, 6, 2.4f); g.DrawRectangle(pen, 9, 14.1f, 6, 2.4f);
                    g.FillRectangle(brush, 8, 3.5f, 8, 2.4f); g.FillRectangle(brush, 8, 18.1f, 8, 2.4f);
                    g.DrawLine(pen, 17.5f, 4.5f, 17.5f, 19.5f);
                    break;
                case IconIds.MachAxial:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.FillRectangle(brush, 8.5f, 5, 2.6f, 14); g.DrawRectangle(pen, 13, 5, 2.6f, 14);
                    break;
                case IconIds.MachAxialDoubleRotor:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.FillRectangle(brush, 6, 5, 2.4f, 14); g.DrawRectangle(pen, 10.8f, 5, 2.4f, 14); g.FillRectangle(brush, 15.6f, 5, 2.4f, 14);
                    break;
                case IconIds.MachAxialDoubleStator:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.DrawRectangle(pen, 6, 5, 2.4f, 14); g.FillRectangle(brush, 10.8f, 5, 2.4f, 14); g.DrawRectangle(pen, 15.6f, 5, 2.4f, 14);
                    break;
                case IconIds.MachMagnet:
                    g.DrawRectangle(pen, 4, 8, 16, 8); g.FillRectangle(brush, 4, 8, 8, 8);
                    break;
                case IconIds.MachWound:
                    for (var i = 0; i < 4; i++) g.DrawArc(pen, 3.5f + i * 4.25f, 8, 4.25f, 8, 180, 180);
                    g.DrawLine(thin, 3.5f, 12, 3.5f, 17); g.DrawLine(thin, 20.5f, 12, 20.5f, 17);
                    break;
                case IconIds.MachCage:
                    g.DrawLine(pen, 4, 7, 20, 7); g.DrawLine(pen, 4, 17, 20, 17);
                    for (var x = 6f; x <= 18.5f; x += 4f) g.DrawLine(thin, x, 7, x, 17);
                    break;
                case IconIds.MachReluctance:
                    g.DrawLines(pen, new[] { new PointF(3, 17), new PointF(5, 17), new PointF(5, 8), new PointF(9, 8), new PointF(9, 17), new PointF(15, 17), new PointF(15, 8), new PointF(19, 8), new PointF(19, 17), new PointF(21, 17) });
                    break;
                case IconIds.Motion:
                    // A gear with a turning arrow around it.
                    g.DrawEllipse(pen, 8, 8, 8, 8);
                    g.FillEllipse(brush, 10.8f, 10.8f, 2.4f, 2.4f);
                    g.DrawArc(pen, 3.5f, 3.5f, 17, 17, -60, 270);
                    g.FillPolygon(brush, new[] { new PointF(16.2f, 2.2f), new PointF(20.6f, 5.4f), new PointF(15.4f, 7.4f) });
                    break;
                case IconIds.Chart:
                    // Axes with a rising curve.
                    g.DrawLine(pen, 4, 3, 4, 20); g.DrawLine(pen, 4, 20, 21, 20);
                    g.DrawBezier(pen, 6, 17, 10, 16, 12, 8, 19, 6);
                    break;
                case IconIds.AlignRow:
                    // Three parts of different size centred on one horizontal line.
                    g.DrawLine(dash, 2, 12, 22, 12);
                    g.DrawRectangle(pen, 3, 8, 5, 8); g.DrawRectangle(pen, 10, 5, 4, 14); g.DrawRectangle(pen, 16, 9.5f, 5, 5);
                    break;
                case IconIds.AlignColumn:
                    g.DrawLine(dash, 12, 2, 12, 22);
                    g.DrawRectangle(pen, 8, 3, 8, 5); g.DrawRectangle(pen, 5, 10, 14, 4); g.DrawRectangle(pen, 9.5f, 16, 5, 5);
                    break;
                case IconIds.DistributeH:
                    // Three parts with equal gaps, the gaps marked below.
                    g.DrawRectangle(pen, 3, 6, 4, 9); g.DrawRectangle(pen, 10, 6, 4, 9); g.DrawRectangle(pen, 17, 6, 4, 9);
                    g.DrawLine(thin, 7, 19, 10, 19); g.DrawLine(thin, 14, 19, 17, 19);
                    break;
                case IconIds.DistributeV:
                    g.DrawRectangle(pen, 6, 3, 9, 4); g.DrawRectangle(pen, 6, 10, 9, 4); g.DrawRectangle(pen, 6, 17, 9, 4);
                    g.DrawLine(thin, 19, 7, 19, 10); g.DrawLine(thin, 19, 14, 19, 17);
                    break;
                case IconIds.Trace:
                    // A dashed loop with the tracing point on it.
                    g.DrawBezier(dash, 4, 15, 4, 4, 20, 4, 20, 12);
                    g.DrawBezier(dash, 20, 12, 20, 20, 8, 21, 4, 15);
                    g.FillEllipse(brush, 17.5f, 5.5f, 4.5f, 4.5f);
                    break;
                case IconIds.MatchFormat:
                    // A brush: handle, ferrule and bristles painting a stroke.
                    g.DrawLine(pen, 17, 3, 12, 11);
                    g.DrawRectangle(pen, 9.5f, 10.5f, 5, 3.5f);
                    g.FillPolygon(brush, new[] { new PointF(9.5f, 14), new PointF(14.5f, 14), new PointF(13.5f, 19), new PointF(8, 19) });
                    g.DrawLine(thin, 4, 21, 20, 21);
                    break;
                case IconIds.FitAround:
                    // A frame closing in on a part: corner brackets around a small box.
                    g.DrawLines(pen, new[] { new PointF(3, 8), new PointF(3, 3), new PointF(8, 3) });
                    g.DrawLines(pen, new[] { new PointF(16, 3), new PointF(21, 3), new PointF(21, 8) });
                    g.DrawLines(pen, new[] { new PointF(21, 16), new PointF(21, 21), new PointF(16, 21) });
                    g.DrawLines(pen, new[] { new PointF(8, 21), new PointF(3, 21), new PointF(3, 16) });
                    g.DrawRectangle(thin, 8, 8, 8, 8);
                    break;
                case IconIds.SelectAssembly:
                    // Two coupled wheels inside a dashed selection frame.
                    g.DrawRectangle(dash, 2.5f, 2.5f, 19, 19);
                    g.DrawEllipse(pen, 5, 6, 8, 8); g.DrawEllipse(pen, 12.5f, 11, 6.5f, 6.5f);
                    break;
                case IconIds.Renumber:
                    // Lead lines with numerals in order: "1 2 3".
                    g.DrawLine(thin, 4, 20, 7, 14); g.DrawLine(thin, 11, 20, 13, 14); g.DrawLine(thin, 18, 20, 19, 14);
                    g.DrawLine(pen, 6, 5, 6, 11);
                    g.DrawLines(pen, new[] { new PointF(10.5f, 6), new PointF(12, 4.8f), new PointF(14, 5.6f), new PointF(11, 11), new PointF(14.5f, 11) });
                    g.DrawLines(pen, new[] { new PointF(17, 5), new PointF(20.5f, 5), new PointF(18.5f, 7.6f), new PointF(20.5f, 9.2f), new PointF(18.6f, 11.2f), new PointF(16.8f, 10.4f) });
                    break;
                case IconIds.Explode:
                    // An assembly flying apart: four parts pushed out of the centre.
                    g.DrawRectangle(pen, 3.5f, 3.5f, 6, 6); g.DrawRectangle(pen, 14.5f, 3.5f, 6, 6);
                    g.DrawRectangle(pen, 3.5f, 14.5f, 6, 6); g.DrawRectangle(pen, 14.5f, 14.5f, 6, 6);
                    g.FillEllipse(brush, 10.5f, 10.5f, 3, 3);
                    break;
                case IconIds.Hop:
                    // A line hopping over a crossing line.
                    g.DrawLine(thin, 12, 4, 12, 20);
                    g.DrawLine(pen, 3, 13, 8.5f, 13); g.DrawLine(pen, 15.5f, 13, 21, 13);
                    g.DrawBezier(pen, 8.5f, 13, 8.5f, 8.3f, 15.5f, 8.3f, 15.5f, 13);
                    break;
                case IconIds.Junction:
                    // Two lines crossing with a junction dot.
                    g.DrawLine(pen, 12, 4, 12, 20); g.DrawLine(pen, 4, 12, 20, 12);
                    g.FillEllipse(brush, 9, 9, 6, 6);
                    break;
                case IconIds.Split:
                    // Scissors: two blades crossing, two finger rings.
                    g.DrawLine(pen, 8, 15, 19, 4); g.DrawLine(pen, 16, 15, 5, 4);
                    g.DrawEllipse(pen, 4, 15, 5, 5); g.DrawEllipse(pen, 15, 15, 5, 5);
                    break;
                case IconIds.RemovePoint:
                    g.DrawLine(thin, 2, 15, 22, 15);
                    g.DrawPolygon(pen, new[] { new PointF(12, 10.5f), new PointF(16.5f, 15), new PointF(12, 19.5f), new PointF(7.5f, 15) });
                    g.DrawLine(pen, 8, 5, 16, 5);
                    break;
                case IconIds.ShapeSquare:
                    g.DrawRectangle(pen, 5, 5, 14, 14);
                    break;
                case IconIds.ShapeCircle:
                    g.DrawEllipse(pen, 5, 5, 14, 14);
                    break;
                case IconIds.ShapeTriangle:
                    g.DrawPolygon(pen, new[] { new PointF(12, 4.5f), new PointF(20, 19), new PointF(4, 19) });
                    break;
                case IconIds.FillTint:
                    g.DrawRectangle(pen, 4, 5, 16, 14);
                    g.FillPolygon(brush, new[] { new PointF(12, 7.5f), new PointF(15.5f, 13), new PointF(12, 16.5f), new PointF(8.5f, 13) });
                    break;
                case IconIds.FillHatch:
                    g.DrawRectangle(pen, 4, 5, 16, 14);
                    g.DrawLine(thin, 4, 12, 11, 5); g.DrawLine(thin, 4, 19, 18, 5); g.DrawLine(thin, 11, 19, 20, 10);
                    break;
                case IconIds.FillDots:
                    g.DrawRectangle(pen, 4, 5, 16, 14);
                    foreach (var (dx, dy) in new[] { (8f, 9f), (12f, 9f), (16f, 9f), (8f, 15f), (12f, 15f), (16f, 15f) })
                        g.FillEllipse(brush, dx - 1, dy - 1, 2, 2);
                    break;
                case IconIds.FillNone:
                    g.DrawRectangle(pen, 4, 5, 16, 14);
                    break;
                case IconIds.RouteFree:
                    g.DrawLine(pen, 4.5f, 18.5f, 19.5f, 5.5f);
                    g.FillEllipse(brush, 2.5f, 16.5f, 4, 4); g.FillEllipse(brush, 17.5f, 3.5f, 4, 4);
                    break;
                case IconIds.RouteOrthogonal:
                    g.DrawLines(pen, new[] { new PointF(4.5f, 18.5f), new PointF(12, 18.5f), new PointF(12, 5.5f), new PointF(19.5f, 5.5f) });
                    g.FillEllipse(brush, 2.5f, 16.5f, 4, 4); g.FillEllipse(brush, 17.5f, 3.5f, 4, 4);
                    break;
                case IconIds.Route45:
                    g.DrawLines(pen, new[] { new PointF(4.5f, 18.5f), new PointF(8, 18.5f), new PointF(16, 10.5f), new PointF(16, 5.5f), new PointF(19.5f, 5.5f) });
                    g.FillEllipse(brush, 2.5f, 16.5f, 4, 4); g.FillEllipse(brush, 17.5f, 3.5f, 4, 4);
                    break;
                case IconIds.WallThick:
                    g.FillRectangle(brush, 3, 7, 18, 10);
                    break;
                case IconIds.WallMedium:
                    g.FillRectangle(brush, 3, 9, 18, 6);
                    break;
                case IconIds.WallThin:
                    g.DrawRectangle(pen, 3, 10.5f, 18, 3);
                    break;
                case IconIds.HubNone:
                    g.DrawLine(pen, 12, 5, 12, 19); g.DrawLine(pen, 9.5f, 5, 14.5f, 5); g.DrawLine(pen, 9.5f, 19, 14.5f, 19);
                    break;
                case IconIds.HubFixed:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.DrawLine(pen, 12, 4, 12, 20); g.DrawLine(pen, 9.5f, 4, 14.5f, 4); g.DrawLine(pen, 9.5f, 20, 14.5f, 20);
                    g.DrawLine(pen, 9.8f, 9.8f, 14.2f, 14.2f); g.DrawLine(pen, 14.2f, 9.8f, 9.8f, 14.2f);
                    break;
                case IconIds.HubLoose:
                    g.DrawLine(thin, 3, 12, 21, 12);
                    g.DrawLine(pen, 12, 4, 12, 8.4f); g.DrawLine(pen, 12, 15.6f, 12, 20); g.DrawLine(pen, 9.5f, 4, 14.5f, 4); g.DrawLine(pen, 9.5f, 20, 14.5f, 20);
                    g.DrawEllipse(thin, 10.3f, 8.4f, 3.4f, 3.2f); g.DrawEllipse(thin, 10.3f, 12.4f, 3.4f, 3.2f);
                    break;
                case IconIds.HubSliding:
                    g.DrawLine(thin, 3, 11, 21, 11);
                    g.DrawLine(pen, 12, 3, 12, 16); g.DrawLine(pen, 9.5f, 3, 14.5f, 3);
                    g.DrawLine(pen, 10, 9, 14, 13); g.DrawLine(pen, 14, 9, 10, 13);
                    g.DrawLine(thin, 6, 20, 18, 20);
                    g.FillPolygon(brush, new[] { new PointF(4.5f, 20), new PointF(7.5f, 18), new PointF(7.5f, 22) });
                    g.FillPolygon(brush, new[] { new PointF(19.5f, 20), new PointF(16.5f, 18), new PointF(16.5f, 22) });
                    break;
                case IconIds.Helix:
                    g.DrawRectangle(pen, 6, 4, 12, 16);
                    g.DrawLine(thin, 6, 12, 12, 4); g.DrawLine(thin, 6, 20, 18, 4); g.DrawLine(thin, 12, 20, 18, 12);
                    break;
                case IconIds.ShaftEnds:
                    g.DrawLine(pen, 8, 12, 16, 12); g.DrawLine(thin, 2.5f, 12, 5, 12); g.DrawLine(thin, 19, 12, 21.5f, 12);
                    g.DrawLine(thin, 8, 9, 8, 15); g.DrawLine(thin, 16, 9, 16, 15);
                    break;
                case IconIds.Ground:
                    g.DrawLine(pen, 4, 9, 20, 9);
                    for (var x = 6f; x <= 20; x += 4f) g.DrawLine(thin, x, 9, x - 3.5f, 14);
                    g.DrawLine(thin, 12, 4, 12, 9);
                    break;
                case IconIds.CourseElbow:
                    g.DrawLines(pen, new[] { new PointF(3, 18), new PointF(12, 18), new PointF(12, 6), new PointF(21, 6) });
                    break;
                default:
                    g.DrawEllipse(pen, 5, 5, 14, 14); g.FillEllipse(brush, 10.5f, 10.5f, 3, 3);
                    break;
            }
            g.Restore(state);
        }

        /// <summary>
        /// Rotation pictogram: an open arc (290°, gap at the top right) ending in a filled arrowhead aligned with the arc's
        /// tangent, pointing into the gap in the direction of rotation. Mirrored for counter-clockwise.
        /// </summary>
        private static void RotateArrow(Graphics g, Pen pen, Brush brush, bool clockwise)
            => ArcArrow(g, pen, brush, clockwise ? -45f : 225f, clockwise ? 290f : -290f, 2.8f, 1.8f, 2.9f);

        /// <summary>
        /// Arc of radius 7.5 around the icon centre (GDI+ angles: degrees clockwise from 3 o'clock; a negative sweep runs
        /// counter-clockwise) ending in a filled arrowhead that follows the arc's tangent in the direction of travel.
        /// </summary>
        private static void ArcArrow(Graphics g, Pen pen, Brush brush, float start, float sweep, float ahead, float back, float half)
        {
            const float cx = 12, cy = 12, r = 7.5f;
            g.DrawArc(pen, cx - r, cy - r, 2 * r, 2 * r, start, sweep);
            var end = (start + sweep) * Math.PI / 180;
            var e = new PointF(cx + r * (float)Math.Cos(end), cy + r * (float)Math.Sin(end));
            // Unit tangent in the direction of travel and the radial normal.
            float tx = (float)-Math.Sin(end), ty = (float)Math.Cos(end);
            if (sweep < 0) { tx = -tx; ty = -ty; }
            float nx = (float)Math.Cos(end), ny = (float)Math.Sin(end);
            var head = new[]
            {
                new PointF(e.X + tx * ahead, e.Y + ty * ahead),
                new PointF(e.X - tx * back + nx * half, e.Y - ty * back + ny * half),
                new PointF(e.X - tx * back - nx * half, e.Y - ty * back - ny * half),
            };
            g.FillPolygon(brush, head);
            using var edge = new Pen(brush, 0.8f) { LineJoin = LineJoin.Round };
            g.DrawPolygon(edge, head);
        }
    }
}
