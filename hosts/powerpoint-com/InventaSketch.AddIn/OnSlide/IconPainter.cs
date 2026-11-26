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

        /// <summary>Host pictograms of the pie menu's places (edit, style, view, arrange); not used by element actions.</summary>
        public const string UiEdit = "ui-edit", UiStyle = "ui-style", UiView = "ui-view", UiArrange = "ui-arrange";

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
            if (SharedPaths.TryGetValue(iconId, out var shared))
            {
                DrawSvgPath(g, shared, pen, brush);
                g.Restore(state);
                return;
            }
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
                case IconIds.Dimension:
                    // Dimension line with arrowheads between two extension lines.
                    g.DrawLine(thin, 4, 6, 4, 19); g.DrawLine(thin, 20, 6, 20, 19);
                    g.DrawLine(pen, 5, 14, 19, 14);
                    g.FillPolygon(brush, new[] { new PointF(4.5f, 14), new PointF(8.5f, 12), new PointF(8.5f, 16) });
                    g.FillPolygon(brush, new[] { new PointF(19.5f, 14), new PointF(15.5f, 12), new PointF(15.5f, 16) });
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
                // ---- places of the pie menu (host pictograms, not element actions)
                case UiEdit:
                    // A pencil.
                    g.DrawPolygon(pen, new[] { new PointF(5, 19), new PointF(6, 15), new PointF(16, 5), new PointF(19, 8), new PointF(9, 18) });
                    g.DrawLine(thin, 14, 7, 17, 10);
                    break;
                case UiStyle:
                    // Three strokes: thick, dashed, thin.
                    using (var heavy = new Pen(ink, 3f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                        g.DrawLine(heavy, 4, 7, 20, 7);
                    g.DrawLine(dash, 4, 12.5f, 20, 12.5f);
                    g.DrawLine(thin, 4, 17.5f, 20, 17.5f);
                    break;
                case UiView:
                    // An eye.
                    g.DrawBezier(pen, 3, 12, 7, 5.5f, 17, 5.5f, 21, 12);
                    g.DrawBezier(pen, 3, 12, 7, 18.5f, 17, 18.5f, 21, 12);
                    g.FillEllipse(brush, 9.5f, 9.5f, 5, 5);
                    break;
                case UiArrange:
                    // Two boxes, one turned.
                    g.DrawRectangle(pen, 3, 9, 9, 9);
                    g.DrawPolygon(pen, new[] { new PointF(16.5f, 3), new PointF(21.5f, 8), new PointF(16.5f, 13), new PointF(11.5f, 8) });
                    break;
                default:
                    g.DrawEllipse(pen, 5, 5, 14, 14); g.FillEllipse(brush, 10.5f, 10.5f, 3, 3);
                    break;
            }
            g.Restore(state);
        }

        /// <summary>
        /// Pictograms defined once as SVG path data on the 24-unit grid, identical to the web add-in's icons.ts (a web test
        /// compares both tables). Sub-paths closed with an uppercase "Z" are filled, everything is stroked; a zero-length
        /// sub-path ("h.01") is a dot.
        /// </summary>
        internal static readonly Dictionary<string, string> SharedPaths = new()
        {
            // <shared-paths>
            ["cmd-option"] = "M12 4.5a7.5 7.5 0 1 0 0 15a7.5 7.5 0 1 0 0 -15zM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0 -6Z",
            ["cmd-value"] = "M3 8h18v8H3zM16.5 5.5v13M14.5 5.5h4M14.5 18.5h4M6.5 12h.01M9.5 12h.01M12.5 12h.01",
            ["cmd-name"] = "M2.5 12L8.5 5H21v14H8.5zM13 10h5M13 14h3.5M8.6 10.6a1.4 1.4 0 1 0 0 2.8a1.4 1.4 0 1 0 0 -2.8z",
            ["cmd-label-show"] = "M3 14h7v7H3zM10 14l3.5-3.5M13 3h8.5v7.5H13zM15.5 6.75h3.5",
            ["cmd-port-labels"] = "M8 4h8v16H8zM2.5 8.5H8M2.5 15.5H8M16 8.5h5.5M16 15.5h5.5M10.5 8.5h2.5M10.5 15.5h2.5",
            ["cmd-values"] = "M3.5 7.5l7 9M10.5 7.5l-7 9M13.5 10h7M13.5 14h7",
            ["cmd-formula"] = "M5.5 20.5c1.6 0 2.4-.8 2.8-2.8l2.2-11c.4-2 1.2-2.8 2.8-2.8M6.5 10h6.5M13.5 12l6 8M19.5 12l-6 8",
            ["cmd-parameters"] = "M4 6h16M4 12h16M4 18h16M8 4a2 2 0 1 0 0 4a2 2 0 1 0 0 -4ZM15.5 10a2 2 0 1 0 0 4a2 2 0 1 0 0 -4ZM10.5 16a2 2 0 1 0 0 4a2 2 0 1 0 0 -4Z",
            ["cmd-code"] = "M9 4C7 4 6.5 5 6.5 7v2.5C6.5 11 5.5 12 4 12c1.5 0 2.5 1 2.5 2.5V17c0 2 .5 3 2.5 3M15 4c2 0 2.5 1 2.5 3v2.5c0 1.5 1 2.5 2.5 2.5c-1.5 0-2.5 1-2.5 2.5V17c0 2-.5 3-2.5 3",
            ["cmd-data"] = "M3 4h18v16H3zM3 13.5h18M9 4v16M15 4v16M3 4h18v4.5H3Z",
            ["cmd-legend"] = "M4 5h3.5v3.5H4ZM4 10.25h3.5v3.5H4ZM4 15.5h3.5V19H4ZM11 6.75h9.5M11 12h7M11 17.25h8.5",
            ["cmd-visibility"] = "M2 12C5 7 8.5 5 12 5C15.5 5 19 7 22 12C19 17 15.5 19 12 19C8.5 19 5 17 2 12zM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0 -6Z",
            ["cmd-grid"] = "M4 3v17h17M4 14.5h16M4 9h16M4 3.5h16M9.5 3.5V20M15 3.5V20M20 3.5V20",
            ["cmd-axis-ticks"] = "M7 3v14h14M11 17v3.5M15 17v3.5M19 17v3.5M3.5 13H7M3.5 9H7M3.5 5H7",
            ["cmd-smooth"] = "M3 19C5 9 9 6 12 12C15 18 19 15 21 5M7.5 4a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0 -3ZM16.5 17.5a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0 -3Z",
            ["cmd-points"] = "M3 17l6-8 6 6 6-9M3 15.2a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0 -3.6ZM9 7.2a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0 -3.6ZM15 13.2a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0 -3.6ZM21 4.2a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0 -3.6Z",
            ["cmd-contour"] = "M3.5 13C3 7 8 3.5 13 4C18.5 4.5 21.5 8 20.5 13C19.5 18 15 20.5 10.5 19.5C6.5 18.5 4 16.5 3.5 13zM8 12.5C8 9.5 10.5 7.5 13.5 8.2C16 8.8 17 11 16.5 13C16 15 14 16 12 15.6C10 15.2 8 14.5 8 12.5z",
            ["cmd-function"] = "M3 5h18v14H3zM5.5 12C7 6.5 9 6.5 10 12C11 17.5 13 17.5 14 12C15 6.5 17 6.5 18.5 12",
            ["cmd-marker"] = "M4 21h16M12 21v-3M12 15.5v-3M12 10V3M12 3h6.5l-2 2.5 2 2.5H12Z",
            ["cmd-value-line"] = "M4 3v18h17M7 8.5h3M12.5 8.5h3M18 8.5h3M6 18C10 17 13 13 20 12",
            ["cmd-hatch-between"] = "M3 6C8 4 14 4 21 6.5M3 18.5C8 15 14 15 21 18.5M6 16l3-10.5M11 15.2l2.8-10M16 15.6l2.6-10",
            ["cmd-hatch-below"] = "M3 20h18M3 14C8 5 14 5 21 10M5.5 20l3.5-11M10.5 20l3-12.5M15.5 20l3-11.5",
            ["cmd-break-x"] = "M2 15h7M15 15h7M7.5 19l3-8M13.5 19l3-8",
            ["cmd-break-y"] = "M12 2v7M12 15v7M7.5 11.5l9-3M7.5 17.5l9-3",
            ["cmd-break-x-remove"] = "M2 15h20M7.5 19l3-8M13.5 19l3-8M15.5 3l5 5M20.5 3l-5 5",
            ["cmd-break-y-remove"] = "M8 2v20M3.5 9.5l9-3M3.5 15.5l9-3M15.5 16l5 5M20.5 16l-5 5",
            ["cmd-ghosts"] = "M5 19L12.5 5.5M5 19l14-7M5 19h15M5 16.8a2.2 2.2 0 1 0 0 4.4a2.2 2.2 0 1 0 0 -4.4ZM12.5 4a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0 -3zM19 10.5a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0 -3zM20 17.5a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0 -3z",
            ["cmd-rays"] = "M3 6h16M3 12h16M3 18h16M16 3.5l3.5 2.5-3.5 2.5M16 9.5l3.5 2.5-3.5 2.5M16 15.5l3.5 2.5-3.5 2.5",
            ["curve-smoothed"] = "M4 18.5C5.6 12 7.3 6.5 11.5 6.5C15.5 6.5 18.2 10.5 20 16.5M2.39 15.52h.01M7.5 14.41h.01M4.03 10.5h.01M9.22 10.43h.01M7.55 5.5h.01M11.34 8.81h.01M14.44 4.76h.01M14.08 9.93h.01M19.14 8.88h.01M16.36 13.31h.01",
            ["curve-spline"] = "M4.57 16.27C5.7 12.02 7.02 8.48 9.28 7.11M13.75 6.97C16.1 7.99 17.9 10.64 19.27 14.32M4 16.2a2.3 2.3 0 1 0 0 4.6a2.3 2.3 0 1 0 0 -4.6zM11.5 4.2a2.3 2.3 0 1 0 0 4.6a2.3 2.3 0 1 0 0 -4.6zM20 14.2a2.3 2.3 0 1 0 0 4.6a2.3 2.3 0 1 0 0 -4.6z",
            ["curve-fitted"] = "M3 20C6 9.5 11 6.2 21 5.6M7.21 15.61a1.05 1.05 0 1 0 0 2.1a1.05 1.05 0 1 0 0 -2.1ZM5.3 7.98a1.05 1.05 0 1 0 0 2.1a1.05 1.05 0 1 0 0 -2.1ZM12.87 9.2a1.05 1.05 0 1 0 0 2.1a1.05 1.05 0 1 0 0 -2.1ZM16.62 2.16a1.05 1.05 0 1 0 0 2.1a1.05 1.05 0 1 0 0 -2.1Z",
            ["curve-segments"] = "M5.22 16.55L10.28 8.45M12.99 8.25L18.51 14.75M4 16.2a2.3 2.3 0 1 0 0 4.6a2.3 2.3 0 1 0 0 -4.6zM11.5 4.2a2.3 2.3 0 1 0 0 4.6a2.3 2.3 0 1 0 0 -4.6zM20 14.2a2.3 2.3 0 1 0 0 4.6a2.3 2.3 0 1 0 0 -4.6z",
            ["curve-raw"] = "M4 18.5L4.07 17.49L4.13 16.49L5.04 15.74L6.02 15.06L5.02 13.81L5.42 12.91L4.96 11.73L6.31 11.2L7.37 10.7L7.31 9.62L7.86 8.86L7.37 7.21L8.76 7.23L9.78 7.14L10.66 7.17L11.45 7.18L12.52 5.66L13.37 6.48L14.31 6.76L14.54 8.14L15.03 8.84L16.57 8.64L17.05 9.49L18.21 9.86L17.5 11.43L17.66 12.36L18.49 13.02L19.03 13.79L20.31 14.35L19.69 15.63L20 16.5",
            ["qty-angle"] = "M3.5 19.5h17M3.5 19.5L14.5 4.5M11.5 19.5A8 8 0 0 0 8.2 13.05",
            ["qty-length"] = "M2 8h20v7H2zM6 8v3.5M10 8v2M14 8v3.5M18 8v2",
            ["qty-width"] = "M3.5 4v16M20.5 4v16M8 12h8M4.5 12L7.7 10L7.7 14ZM19.5 12L16.3 14L16.3 10Z",
            ["qty-height"] = "M3 21h18M8 3h8M12 7v10M12 3.5L14 6.7L10 6.7ZM12 20.5L10 17.3L14 17.3Z",
            ["qty-thickness"] = "M3 10h18M3 14h18M12 2v4M12 22v-4M12 9.5L10 6.3L14 6.3ZM12 14.5L14 17.7L10 17.7Z",
            ["qty-area"] = "M8 8h12v12H8zM4 8v12M8 4h12M3 8h2M3 20h2M8 3v2M20 3v2M11 17l6-6M11 13l2-2M15 17l2-2",
            ["qty-volume"] = "M6 3v15a3 3 0 0 0 3 3h6a3 3 0 0 0 3-3V3M6 11h12M6 7h2.5M6 15h2.5",
            ["qty-mass"] = "M7 10h10l3 11H4zM9.5 10a2.5 2.5 0 1 1 5 0",
            ["qty-pressure"] = "M12 3a8 8 0 1 0 0 16a8 8 0 1 0 0-16zM12 11l4-4.5M10 19v3h4v-3M12 9.6a1.4 1.4 0 1 0 0 2.8a1.4 1.4 0 1 0 0 -2.8Z",
            ["qty-power"] = "M13.5 2L5 13.5h6.5L10 22l9-12h-6.5z",
            ["qty-voltage"] = "M2.5 12H9M9 5v14M14 8.5v7M14 12h7.5M16.5 5h4M18.5 3v4",
            ["qty-capacity"] = "M2.5 7h16v10h-16zM18.5 10h2.5v4h-2.5M8 12h5M5 12L7.8 10.2L7.8 13.8ZM16 12L13.2 13.8L13.2 10.2Z",
            ["qty-charge"] = "M2.5 7h16v10h-16zM18.5 10h2.5v4h-2.5M4.5 9h7v6h-7Z",
            ["qty-time"] = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18zM12 7v5l3.5 2.5",
            ["qty-frequency"] = "M3 11C4.8 3.5 7.7 3.5 9.75 11C11.8 18.5 14.7 18.5 16.5 11M3 21h13.5M3 19v4M16.5 19v4",
            ["qty-tolerance"] = "M3 12h18M3 6.5h3M9 6.5h3M15 6.5h3M3 17.5h3M9 17.5h3M15 17.5h3",
            ["qty-flow"] = "M2 6.5h20M2 17.5h20M4 12h9M18 12L13 15L13 9Z",
            ["qty-range-x"] = "M2 17h20M6 10v11M18 10v11M6 10h2.5M6 21h2.5M18 10h-2.5M18 21h-2.5",
            ["qty-range-y"] = "M7 2v20M3 6h11M3 18h11M3 6v2.5M14 6v2.5M3 18v-2.5M14 18v-2.5",
            ["cmd-point-add"] = "M2 15h20M12 10.5l4.5 4.5-4.5 4.5L7.5 15zM9 5h6M12 2v6",
            ["cmd-row-add"] = "M3 3h18v9H3zM3 7.5h18M12 14v7M8.5 17.5h7",
            ["cmd-row-add-top"] = "M3 12h18v9H3zM3 16.5h18M12 3v7M8.5 6.5h7",
            ["cmd-row-remove"] = "M3 3h18v9H3zM3 7.5h18M8.5 17.5h7",
            ["cmd-column-add"] = "M3 3h9v18H3zM7.5 3v18M18 8.5v7M14.5 12h7",
            ["cmd-column-remove"] = "M3 3h9v18H3zM7.5 3v18M14.5 12h7",
            ["cmd-operation-add"] = "M3 3h18v9H3zM6 7.5h7M16 5.5c-1 1.2-1 2.8 0 4M18.5 5.5c1 1.2 1 2.8 0 4M12 14v7M8.5 17.5h7",
            ["cmd-operation-remove"] = "M3 3h18v9H3zM6 7.5h7M16 5.5c-1 1.2-1 2.8 0 4M18.5 5.5c1 1.2 1 2.8 0 4M8.5 17.5h7",
            ["cmd-port-add"] = "M3 8h9v12H3zM12 14h5.5M19 2.5v6M16 5.5h6M19 12.4a1.6 1.6 0 1 0 0 3.2a1.6 1.6 0 1 0 0 -3.2z",
            ["cmd-port-remove"] = "M3 8h9v12H3zM12 14h5.5M16 5.5h6M19 12.4a1.6 1.6 0 1 0 0 3.2a1.6 1.6 0 1 0 0 -3.2z",
            ["cmd-count-more"] = "M3 15h4.5v5H3zM9.75 15h4.5v5h-4.5zM16.5 15H21v5h-4.5zM12 2.5v8M8 6.5h8",
            ["cmd-count-fewer"] = "M3 15h4.5v5H3zM9.75 15h4.5v5h-4.5zM16.5 15H21v5h-4.5zM8 6.5h8",
            ["cmd-reverse"] = "M18 21V10a6 6 0 0 0-12 0v5M6 20L3.2 16L8.8 16Z",
            ["cmd-side-swap"] = "M2 12h20M11 6.5c5 0 7.5 3 6 9.5M6.5 4.3a2.2 2.2 0 1 0 0 4.4a2.2 2.2 0 1 0 0 -4.4ZM16.5 18.5L14.97 14.93L19.28 15.79Z",
            ["cmd-polarity"] = "M3 7h6M6 4v6M15 17h6M12 4.5c4 0 6.5 2 7 6M11.5 19.5c-4 0-6.5-2-7-6M19.2 12.5L17.15 10.09L20.73 9.73ZM4.6 11.5L6.65 13.91L3.07 14.27Z",
            ["cmd-flow-arrow"] = "M2 12h20M8.5 6.5L16 12l-7.5 5.5Z",
            ["cmd-joint-start"] = "M8.5 12H21M5.5 9a3 3 0 1 0 0 6a3 3 0 1 0 0 -6z",
            ["cmd-joint-end"] = "M3 12h12.5M18.5 9a3 3 0 1 0 0 6a3 3 0 1 0 0 -6z",
            ["cmd-angle-next"] = "M5 19L12.66 12.57M11.81 6.2A14.5 14.5 0 0 1 19.07 15.49M19.36 16.66L16.64 14.04L20.53 13.07ZM5 17a2 2 0 1 0 0 4a2 2 0 1 0 0 -4Z",
            ["cmd-angle-prev"] = "M5 19L12.66 12.57M19.07 15.49A14.5 14.5 0 0 0 11.81 6.2M10.75 5.63L14.51 5.37L12.63 8.9ZM5 17a2 2 0 1 0 0 4a2 2 0 1 0 0 -4Z",
            ["cmd-home"] = "M3 11.5L12 3.5l9 8M5.5 9.5V21h13V9.5M10 21v-6h4v6",
            ["cmd-align-wall"] = "M2 16h20M2 21h20M3 21l5-5M9 21l5-5M15 21l5-5M8 3h8v6H8zM12 9v2.5M12 15L9.8 12L14.2 12Z",
            ["text-align-left"] = "M4 5h16M4 9.5h10M4 14h16M4 18.5h10",
            ["text-align-center"] = "M4 5h16M7 9.5h10M4 14h16M7 18.5h10",
            ["text-align-right"] = "M4 5h16M10 9.5h10M4 14h16M10 18.5h10",
            ["head-open-dashed"] = "M3 12h3M8.5 12h3M14 12h6.5M14 7l6.5 5-6.5 5",
            ["head-triangle-hollow-dashed"] = "M3 12h3M8 12h3M13.5 7.5l7.5 4.5-7.5 4.5z",
            ["cmd-create-object"] = "M2 12h2.5M6.5 12h2.5M11 12h1.5M10 9l3 3-3 3M15 6.5h7v11h-7z",
            ["cmd-pencil"] = "M15 3.5l4 4L8.5 18 3.5 19.5 5 14.5zM13 5.5l4 4M12.5 21c2.5-2 4.5 1.5 8.5-1",
            // Travel modes (route, distance callout) and phases (switchgear devices and loads), round 4.
            ["travel-walk"] = "M13 2.5a2 2 0 1 0 0 4a2 2 0 1 0 0 -4zM12.2 8.5L10.6 14.5L8 21M10.6 14.5l3.6 2.6l.6 3.9M12 9.5L8.2 12.3M12 9.5l3.6 3",
            ["travel-bike"] = "M6 12.5a3.5 3.5 0 1 0 0 7a3.5 3.5 0 1 0 0 -7zM18 12.5a3.5 3.5 0 1 0 0 7a3.5 3.5 0 1 0 0 -7zM6 16l4 -6.5h6l2 6.5M10 9.5l1.5 6.5l4.5 -6.5M11.5 16H6M16 9.5l-1 -3h-2M10 9.5V7.5M8.5 7.5h3",
            ["travel-car"] = "M4.8 16.5H2.5v-4l2 -1.5l2.5 -4.5h9.5l3 4.5l2 1.5v4h-2.3M9.2 16.5h5.6M4.5 11h15M12 6.5V11M7 14.3a2.2 2.2 0 1 0 0 4.4a2.2 2.2 0 1 0 0 -4.4zM17 14.3a2.2 2.2 0 1 0 0 4.4a2.2 2.2 0 1 0 0 -4.4z",
            ["travel-transit"] = "M6.5 3h11a2.5 2.5 0 0 1 2.5 2.5V17H4V5.5A2.5 2.5 0 0 1 6.5 3zM4 11h16M9 5.5h6M7.5 14h.01M16.5 14h.01M7 17v3.5M17 17v3.5",
            ["travel-none"] = "M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0 -16zM6.35 17.65L17.65 6.35",
            ["phase-l1"] = "M3 5h17v12.5M3 10h10M3 15h10M20 21.5l-1.8 -3.6h3.6Z",
            ["phase-l2"] = "M3 5h10M3 10h17v7.5M3 15h10M20 21.5l-1.8 -3.6h3.6Z",
            ["phase-l3"] = "M3 5h10M3 10h10M3 15h17v2.5M20 21.5l-1.8 -3.6h3.6Z",
            ["phase-3"] = "M3 5h18v12.5M3 10h13.5v7.5M3 15h9v2.5M21 21.5l-1.6 -3.6h3.2ZM16.5 21.5l-1.6 -3.6h3.2ZM12 21.5l-1.6 -3.6h3.2Z",
            ["phase-circuit"] = "M12 2.5V8M10.4 6.4l3.2 3.2M13.6 6.4l-3.2 3.2M12 15.5l4.2 -6.3M12 15.5V21.5",
            // </shared-paths>
        };

        /// <summary>Strokes (and fills, see <see cref="SharedPaths"/>) SVG path data with M, L, H, V, C, S, A, Z (absolute and relative).</summary>
        internal static void DrawSvgPath(Graphics g, string d, Pen pen, Brush brush)
        {
            foreach (var (figure, filled, length) in ParseSvgPath(d))
            {
                using (figure)
                {
                    if (length < 0.05)
                    {
                        var p = figure.PointCount > 0 ? figure.PathPoints[0] : PointF.Empty;
                        var r = pen.Width * 0.55f;
                        g.FillEllipse(brush, p.X - r, p.Y - r, 2 * r, 2 * r);
                        continue;
                    }
                    if (filled) g.FillPath(brush, figure);
                    g.DrawPath(pen, figure);
                }
            }
        }

        /// <summary>The sub-paths of SVG path data: figure, filled (closed with "Z"), approximate length.</summary>
        internal static List<(GraphicsPath Figure, bool Filled, float Length)> ParseSvgPath(string d)
        {
            var result = new List<(GraphicsPath, bool, float)>();
            var tokens = System.Text.RegularExpressions.Regex.Matches(d, @"[MmLlHhVvCcSsAaZz]|-?(?:\d+\.?\d*|\.\d+)(?:e-?\d+)?");
            var i = 0;
            char cmd = 'M';
            float x = 0, y = 0, sx = 0, sy = 0, lastCx = 0, lastCy = 0;
            GraphicsPath? fig = null;
            var length = 0f;
            bool IsNumber() => i < tokens.Count && !char.IsLetter(tokens[i].Value[0]);
            float Num() => float.Parse(tokens[i++].Value, System.Globalization.CultureInfo.InvariantCulture);
            void Flush(bool filled)
            {
                if (fig is not null) result.Add((fig, filled, length));
                fig = null;
                length = 0;
            }
            void Line(float nx, float ny)
            {
                fig ??= new GraphicsPath();
                fig.AddLine(x, y, nx, ny);
                length += (float)Math.Sqrt((nx - x) * (nx - x) + (ny - y) * (ny - y));
                x = nx; y = ny;
            }
            void Bezier(float c1x, float c1y, float c2x, float c2y, float nx, float ny)
            {
                fig ??= new GraphicsPath();
                fig.AddBezier(x, y, c1x, c1y, c2x, c2y, nx, ny);
                length += (float)Math.Sqrt((nx - x) * (nx - x) + (ny - y) * (ny - y)) + 0.1f;
                lastCx = c2x; lastCy = c2y;
                x = nx; y = ny;
            }
            while (i < tokens.Count)
            {
                if (!IsNumber()) cmd = tokens[i++].Value[0];
                var rel = char.IsLower(cmd);
                float ox = rel ? x : 0, oy = rel ? y : 0;
                var prevCmd = char.ToUpperInvariant(cmd);
                switch (prevCmd)
                {
                    case 'M':
                        Flush(false);
                        x = Num() + ox; y = Num() + oy; sx = x; sy = y;
                        fig = new GraphicsPath();
                        fig.StartFigure();
                        cmd = rel ? 'l' : 'L';
                        lastCx = x; lastCy = y;
                        continue;
                    case 'L': Line(Num() + ox, Num() + oy); lastCx = x; lastCy = y; break;
                    case 'H': Line(Num() + ox, y); lastCx = x; lastCy = y; break;
                    case 'V': Line(x, Num() + oy); lastCx = x; lastCy = y; break;
                    case 'C':
                        {
                            float a = Num() + ox, b = Num() + oy, c = Num() + ox, e = Num() + oy, nx = Num() + ox, ny = Num() + oy;
                            Bezier(a, b, c, e, nx, ny);
                            break;
                        }
                    case 'S':
                        {
                            float c1x = 2 * x - lastCx, c1y = 2 * y - lastCy;
                            float c = Num() + ox, e = Num() + oy, nx = Num() + ox, ny = Num() + oy;
                            Bezier(c1x, c1y, c, e, nx, ny);
                            break;
                        }
                    case 'A':
                        {
                            float rx = Num(), ry = Num(), rot = Num();
                            var large = Num() != 0;
                            var sweep = Num() != 0;
                            float nx = Num() + ox, ny = Num() + oy;
                            foreach (var (c1, c2, p) in ArcToBeziers(x, y, rx, ry, rot, large, sweep, nx, ny)) Bezier(c1.X, c1.Y, c2.X, c2.Y, p.X, p.Y);
                            x = nx; y = ny; lastCx = x; lastCy = y;
                            break;
                        }
                    case 'Z':
                        if (fig is not null)
                        {
                            if (Math.Abs(x - sx) > 1e-3 || Math.Abs(y - sy) > 1e-3) Line(sx, sy);
                            fig.CloseFigure();
                        }
                        x = sx; y = sy;
                        Flush(cmd == 'Z');
                        continue;
                    default:
                        i++;
                        break;
                }
            }
            Flush(false);
            return result;
        }

        /// <summary>An SVG elliptical arc as cubic Bézier segments of at most 90° (endpoint to centre parameterisation).</summary>
        private static IEnumerable<(PointF C1, PointF C2, PointF P)> ArcToBeziers(float x1, float y1, float rx, float ry, float rotDeg, bool large, bool sweep, float x2, float y2)
        {
            if (rx == 0 || ry == 0 || (x1 == x2 && y1 == y2)) { yield return (new PointF(x1, y1), new PointF(x2, y2), new PointF(x2, y2)); yield break; }
            double phi = rotDeg * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (x1 - x2) / 2.0, dy = (y1 - y2) / 2.0;
            double x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
            double arx = Math.Abs(rx), ary = Math.Abs(ry);
            var lambda = x1p * x1p / (arx * arx) + y1p * y1p / (ary * ary);
            if (lambda > 1) { arx *= Math.Sqrt(lambda); ary *= Math.Sqrt(lambda); }
            var num = arx * arx * ary * ary - arx * arx * y1p * y1p - ary * ary * x1p * x1p;
            var den = arx * arx * y1p * y1p + ary * ary * x1p * x1p;
            var coef = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
            double cxp = coef * arx * y1p / ary, cyp = -coef * ary * x1p / arx;
            double cx = cos * cxp - sin * cyp + (x1 + x2) / 2.0, cy = sin * cxp + cos * cyp + (y1 + y2) / 2.0;
            double Angle(double ux, double uy, double vx, double vy)
            {
                var a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
                return a;
            }
            var t1 = Angle(1, 0, (x1p - cxp) / arx, (y1p - cyp) / ary);
            var dt = Angle((x1p - cxp) / arx, (y1p - cyp) / ary, (-x1p - cxp) / arx, (-y1p - cyp) / ary);
            if (!sweep && dt > 0) dt -= 2 * Math.PI;
            if (sweep && dt < 0) dt += 2 * Math.PI;
            var n = (int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 2) - 1e-9);
            var step = dt / n;
            var k = 4.0 / 3 * Math.Tan(step / 4);
            PointF Pt(double ex, double ey) => new((float)(cos * ex - sin * ey + cx), (float)(sin * ex + cos * ey + cy));
            for (var s = 0; s < n; s++)
            {
                double a0 = t1 + s * step, a1 = a0 + step;
                double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                yield return (Pt(arx * (c0 - k * s0), ary * (s0 + k * c0)), Pt(arx * (c1 + k * s1), ary * (s1 - k * c1)), Pt(arx * c1, ary * s1));
            }
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
