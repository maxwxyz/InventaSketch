using System;
using System.Drawing;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Mapping between slide points and physical screen pixels of the visible slide pane at the current zoom and scroll.
    /// Approach (proven in Slidemax): two sample points of DocumentWindow.PointsToScreenPixelsX/Y with a wide span (the API
    /// rounds to whole pixels); the slide pane is the window under the slide centre ("paneClassDC" in older versions,
    /// "mdiClass" in Microsoft 365, where thumbnails, notes and scroll bar must be cut away). Call inside
    /// <see cref="DpiScope.PerMonitor"/> so everything is in physical pixels.
    /// </summary>
    internal sealed class SlideView
    {
        private const string PaneClass = "paneClassDC";
        private const string DocumentClass = "mdiClass";

        private SlideView(double k, double x0, double y0, IntPtr pane, Rectangle paneRect, IntPtr main, double slideW, double slideH, double deviceScale)
        {
            PixelsPerPoint = k; OffsetX = x0; OffsetY = y0; Pane = pane; PaneRect = paneRect; MainWindow = main;
            SlideWidth = slideW; SlideHeight = slideH; DeviceScale = deviceScale;
        }

        public double PixelsPerPoint { get; }
        public double OffsetX { get; }
        public double OffsetY { get; }
        public IntPtr Pane { get; }
        /// <summary>Visible slide area in screen pixels.</summary>
        public Rectangle PaneRect { get; }
        public IntPtr MainWindow { get; }
        public double SlideWidth { get; }
        public double SlideHeight { get; }
        /// <summary>Screen pixels per logical pixel of the on-slide controls: the display scale (1 = 96 dpi) times the
        /// user's control size (<see cref="AddInSettings.UiScale"/>).</summary>
        public double DeviceScale { get; }

        public bool Contains(POINT p) => PaneRect.Contains(p.X, p.Y);

        /// <summary>Screen pixel to page millimetres.</summary>
        public Vec2 ToMm(POINT p) => new((p.X - OffsetX) / PixelsPerPoint / Geo.PtPerMm, (p.Y - OffsetY) / PixelsPerPoint / Geo.PtPerMm);

        /// <summary>Page millimetres to screen pixels.</summary>
        public PointF ToScreen(Vec2 mm) => new((float)(mm.X * Geo.PtPerMm * PixelsPerPoint + OffsetX), (float)(mm.Y * Geo.PtPerMm * PixelsPerPoint + OffsetY));

        /// <summary>Screen pixels per millimetre.</summary>
        public double PixelsPerMm => PixelsPerPoint * Geo.PtPerMm;

        /// <summary>Millimetres for a length in logical pixels (snap tolerance, handle sizes).</summary>
        public double Mm(double logicalPixels) => logicalPixels * DeviceScale / PixelsPerMm;

        public bool SameAs(SlideView? o) => o is not null && o.PixelsPerPoint == PixelsPerPoint && o.OffsetX == OffsetX && o.OffsetY == OffsetY && o.PaneRect == PaneRect;

        /// <summary>Current view, or null if no slide is visible in an editable view.</summary>
        public static SlideView? Current(PowerPoint.Application app)
        {
            try
            {
                if (app.Windows.Count == 0) return null;
                var window = app.ActiveWindow;
                if (window.ViewType != PowerPoint.PpViewType.ppViewNormal && window.ViewType != PowerPoint.PpViewType.ppViewSlide) return null;
                if (window.ActivePane.ViewType != PowerPoint.PpViewType.ppViewSlide && window.ViewType == PowerPoint.PpViewType.ppViewNormal) return null;
                var setup = window.Presentation.PageSetup;
                const float span = 1000f;
                int x0 = window.PointsToScreenPixelsX(0), x1 = window.PointsToScreenPixelsX(span);
                int y0 = window.PointsToScreenPixelsY(0);
                double k = (x1 - x0) / (double)span;
                if (k <= 0) return null;
                var centre = new POINT { X = (int)(setup.SlideWidth / 2 * k + x0), Y = (int)(setup.SlideHeight / 2 * k + y0) };
                var hwnd = WindowFromPoint(centre);
                var main = new IntPtr(window.HWND);
                if (main == IntPtr.Zero) main = RootOwner(hwnd);
                var root = GetAncestor(main, 2 /* GA_ROOT */);
                var cls = ClassName(hwnd);
                if (hwnd == IntPtr.Zero || (cls != PaneClass && cls != DocumentClass) || GetAncestor(hwnd, 2) != root)
                {
                    hwnd = Largest(root, PaneClass);
                    if (hwnd == IntPtr.Zero) hwnd = Largest(root, DocumentClass);
                }
                if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return null;
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                var parent = GetParent(hwnd);
                if (parent != IntPtr.Zero && GetWindowRect(parent, out var pr))
                    rect = Rectangle.Intersect(rect, Rectangle.FromLTRB(pr.Left, pr.Top, pr.Right, pr.Bottom));
                double scale = DeviceScaleOf(hwnd);
                if (ClassName(hwnd) == DocumentClass && window.ViewType == PowerPoint.PpViewType.ppViewNormal)
                {
                    // Microsoft 365: one mdiClass window holds thumbnails (left), slide, notes (bottom) and the scroll bar.
                    int left = rect.Left + (int)Math.Round(rect.Width * Math.Max(0, Math.Min(90, window.SplitHorizontal)) / 100.0);
                    int bottom = rect.Top + (int)Math.Round(rect.Height * Math.Max(10, Math.Min(100, window.SplitVertical)) / 100.0);
                    int right = rect.Right - (int)Math.Round(System.Windows.Forms.SystemInformation.VerticalScrollBarWidth * scale);
                    rect = Rectangle.FromLTRB(left, rect.Top, Math.Max(left, right), bottom);
                }
                if (rect.Width <= 0 || rect.Height <= 0) return null;
                // The user's control size (125 % for low vision) scales bar, pie, handles and hit targets together.
                return new SlideView(k, x0, y0, hwnd, rect, root, setup.SlideWidth, setup.SlideHeight, scale * AddInSettings.UiScale);
            }
            catch (Exception ex)
            {
                Log.Error("SlideView", ex);
                return null;
            }
        }

        private static IntPtr Largest(IntPtr window, string className)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            EnumChildWindows(window, (h, _) =>
            {
                if (IsWindowVisible(h) && ClassName(h) == className && GetWindowRect(h, out var r))
                {
                    long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
                    if (area > bestArea) { bestArea = area; best = h; }
                }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        private static double DeviceScaleOf(IntPtr hwnd)
        {
            try
            {
                var dpi = GetDpiForWindow(hwnd);
                return dpi > 0 ? dpi / 96.0 : 1;
            }
            catch (EntryPointNotFoundException)
            {
                return 1;
            }
        }
    }
}
