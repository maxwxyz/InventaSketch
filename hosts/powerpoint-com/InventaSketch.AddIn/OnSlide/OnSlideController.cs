using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Interaction directly on the slide:
    /// - insert at pointer: after picking a library item an orange outline follows the pointer and snaps (gear mesh,
    ///   hub on shaft axis, points, alignment); click inserts, Esc cancels, Alt suspends snapping;
    /// - drag with snapping: pressing inside a selected InventaSketch unit moves it with the same snapping (PowerPoint keeps
    ///   its own resize/rotate handles on the border);
    /// - pie menu: right-click on a selected unit opens a radial menu (delete on top, views, type actions, duplicate,
    ///   mirror, rotate).
    /// Hooks only compute and draw; every PowerPoint object-model call runs later through BeginInvoke.
    /// </summary>
    internal sealed class OnSlideController : IDisposable
    {
        private enum Mode { Idle, Armed, Place, Drag, Pie, Resize, EditPoint }

        private static readonly Color Ghost = Color.FromArgb(240, 140, 0);
        private static readonly Color GuideColor = Color.FromArgb(220, 40, 40);

        private readonly PowerPoint.Application _app;
        private readonly Func<Registry> _registry;
        private readonly Func<Lang> _lang;
        private readonly InputHooks _hooks = new();
        private readonly OverlayWindow _overlay;
        private readonly Timer _timer;
        private Mode _mode;
        private SlideView? _view;
        private List<Anchor> _targets = new();

        // Insert at pointer
        private LibraryItem? _item;
        private Vec2 _placeAt;
        private bool _placePressed;
        private bool _loggedPlaceMessage;

        // Armed / drag
        private SketchUnit? _unit;
        private IReadOnlyList<Primitive> _unitPrims = Array.Empty<Primitive>();
        private List<Anchor> _unitAnchors = new();
        private POINT _down;
        private Vec2 _delta;
        private bool _moved;
        private IReadOnlyList<SnapGuide> _guides = Array.Empty<SnapGuide>();

        // Pie menu
        private PieMenu? _pie;

        // Two-point tool (lines, arrows, dimensions, lead lines) and point editing
        private Vec2? _twoStart;
        private int _pointIndex = -1;
        private Vec2 _pointAt;

        // Resize with snapping
        private int _handle = -1;
        private Rect _startRect;
        private Vec2 _pivot;
        private double _sx = 1, _sy = 1;
        private bool _resizeMeshed;

        // Floating context bar
        private readonly OverlayWindow _barWindow;
        private FloatingBar? _bar;
        private readonly BarFade _fade = new();
        private bool _barPressed;
        private byte _barAlpha;

        public OnSlideController(PowerPoint.Application app, Func<Registry> registry, Func<Lang> lang)
        {
            _app = app; _registry = registry; _lang = lang;
            using (DpiScope.PerMonitor())
            {
                _overlay = new OverlayWindow();
                _overlay.CreateControl();
                var _ = _overlay.Handle;
                _barWindow = new OverlayWindow();
                _barWindow.CreateControl();
                _ = _barWindow.Handle;
            }
            _hooks.Mouse = OnMouse;
            _hooks.KeyDown = OnKey;
            _hooks.Reset = () => Cancel("reset");
            _timer = new Timer { Interval = 250 };
            _timer.Tick += (_, _) => RefreshView();
            _app.WindowSelectionChange += OnSelectionChange;
        }

        public void Dispose()
        {
            try { _app.WindowSelectionChange -= OnSelectionChange; } catch { /* PowerPoint closing */ }
            _timer.Dispose();
            _hooks.Dispose();
            _overlay.Dispose();
            _barWindow.Dispose();
        }

        private bool German => _lang() == Lang.De;
        private string T(string en, string de) => German ? de : en;

        // ------------------------------------------------------------------ modes

        /// <summary>Starts insert-at-pointer for a library item. Returns false if no slide is visible.</summary>
        public bool BeginPlacement(LibraryItem item)
        {
            // Hooks are thread-local: they must be installed on PowerPoint's UI thread. Out-of-process automation calls on
            // .NET objects arrive on RPC worker threads (the CLR wrapper is agile), so marshal them.
            if (_overlay.InvokeRequired) return (bool)_overlay.Invoke(new Func<bool>(() => BeginPlacement(item)));
            using var dpi = DpiScope.PerMonitor();
            var view = SlideView.Current(_app);
            var slide = CurrentSlide();
            if (view is null || slide is null) return false;
            Cancel("new placement");
            _view = view;
            _item = item;
            _targets = CollectTargets(slide, exceptId: null);
            _mode = Mode.Place;
            _placePressed = false;
            _twoStart = null;
            _loggedPlaceMessage = false;
            _hooks.Install();
            Log.Info("Place hooks installed=" + _hooks.Installed + " thread=" + GetCurrentThreadId());
            _timer.Start();
            GetCursorPos(out var p);
            UpdatePlacement(p);
            Log.Info("Insert at pointer: " + item.Id);
            return true;
        }

        private void OnSelectionChange(PowerPoint.Selection sel)
        {
            try
            {
                if (_mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint) return;
                using var dpi = DpiScope.PerMonitor();
                SketchUnit? unit = null;
                if (sel.Type == PowerPoint.PpSelectionType.ppSelectionShapes && sel.ShapeRange.Count == 1)
                {
                    var shape = sel.ShapeRange[1];
                    if (SketchUnits.IsUnit(shape)) unit = SketchUnits.Read(shape);
                }
                if (unit is null) { Disarm(); return; }
                Log.Info("Selection: InventaSketch unit " + unit.Shape.Name);
                Arm(unit);
            }
            catch (Exception ex)
            {
                Log.Error("Selection change", ex);
                Disarm();
            }
        }

        private void Arm(SketchUnit unit)
        {
            var slide = CurrentSlide();
            var view = SlideView.Current(_app);
            if (slide is null || view is null) { Disarm(); return; }
            _view = view;
            _unit = unit;
            var registry = _registry();
            var ctx = new RenderContext(unit.Doc, _lang());
            _unitPrims = unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
            _unitAnchors = unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.PageAnchors(o, ctx)).ToList();
            _targets = CollectTargets(slide, unit.Id);
            _mode = Mode.Armed;
            _hooks.Install();
            _timer.Start();
            _overlay.Clear();
            ShowBar(view);
            DrawPointHandles(view);
            Log.Info($"Armed {unit.Shape.Name}: {_unitAnchors.Count} anchors, {_targets.Count} targets, pane {view.PaneRect}, {view.PixelsPerPoint:0.###} px/pt, hooks {_hooks.Installed}");
        }

        private void Disarm()
        {
            if (_mode is Mode.Place) return;
            _mode = Mode.Idle;
            _unit = null;
            _pie = null;
            HideBar();
            _hooks.Uninstall();
            _timer.Stop();
            _overlay.Clear();
        }

        private void Cancel(string reason)
        {
            if (_mode == Mode.Idle) return;
            if (_mode != Mode.Armed) Log.Info("Cancel " + _mode + ": " + reason);
            var wasArmedUnit = _unit;
            _item = null;
            _pie = null;
            _guides = Array.Empty<SnapGuide>();
            _overlay.Clear();
            if (wasArmedUnit is not null && _mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint)
            {
                _mode = Mode.Armed;
                if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
                return;
            }
            HideBar();
            _mode = Mode.Idle;
            _hooks.Uninstall();
            _timer.Stop();
        }

        private void RefreshView()
        {
            try
            {
                if (_mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint) return;
                using var dpi = DpiScope.PerMonitor();
                var view = SlideView.Current(_app);
                if (view is null) { Cancel("no slide view"); return; }
                if (!view.SameAs(_view)) _view = view;
                // Native moves/resizes of the armed unit (mouse, arrow keys, format pane): re-read and re-arm.
                if (_mode == Mode.Armed && _unit is not null)
                {
                    var s = _unit.Shape;
                    var b = _unit.BoundsPt;
                    if (Math.Abs(s.Left - b.X) > 0.01 || Math.Abs(s.Top - b.Y) > 0.01 || Math.Abs(s.Width - b.Width) > 0.01 || Math.Abs(s.Height - b.Height) > 0.01)
                    {
                        var fresh = SketchUnits.Read(s);
                        if (fresh is not null) Arm(fresh);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Refresh view", ex);
            }
        }

        // ------------------------------------------------------------------ input (runs inside the hook: compute and draw only)

        private bool OnMouse(int message, POINT pt, IntPtr hwnd)
        {
            var view = _view;
            if (view is null) return false;
            if (_mode == Mode.Place && !_loggedPlaceMessage) { _loggedPlaceMessage = true; Log.Info("Place: first mouse message 0x" + message.ToString("X4") + " at " + pt.X + "," + pt.Y); }
            switch (_mode)
            {
                case Mode.Place when IsTwoPoint(_item):
                    if (message == WM_MOUSEMOVE) { UpdatePlacement(pt); return false; }
                    if (message == WM_LBUTTONDOWN)
                    {
                        if (!view.Contains(pt)) { Cancel("click outside"); return false; }
                        UpdatePlacement(pt);
                        if (_twoStart is null) { _twoStart = _placeAt; _down = pt; }
                        _placePressed = true;
                        return true;
                    }
                    if (message == WM_LBUTTONUP && _placePressed)
                    {
                        _placePressed = false;
                        UpdatePlacement(pt);
                        var start = _twoStart;
                        var end = _placeAt;
                        // Click-click: the first release only fixes the start; press-drag-release sets both ends.
                        if (start is null || (start.Value - end).Length < view.Mm(4)) return true;
                        var item = _item;
                        Cancel("placed");
                        if (item is not null) Post(() => InsertPoints(item, start.Value, end));
                        return true;
                    }
                    if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
                    return false;

                case Mode.Place:
                    if (message == WM_MOUSEMOVE) { UpdatePlacement(pt); return false; }
                    if (message == WM_LBUTTONDOWN)
                    {
                        Log.Info("Place press at " + pt.X + "," + pt.Y + " inside=" + view.Contains(pt));
                        if (!view.Contains(pt)) { Cancel("click outside"); return false; }
                        UpdatePlacement(pt);
                        _placePressed = true;
                        return true;
                    }
                    if (message == WM_LBUTTONUP && _placePressed)
                    {
                        var item = _item;
                        var at = _placeAt;
                        Cancel("placed");
                        if (item is not null) Post(() => InsertAt(item, at));
                        return true;
                    }
                    if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
                    return false;

                case Mode.Armed:
                    if (HandleBar(message, pt, view, out var barSwallow)) return barSwallow;
                    if (_unit is null || !view.Contains(pt)) return false;
                    if (message == WM_LBUTTONDOWN && PointHandleAt(pt, view) is int pointIndex and >= 0)
                    {
                        HideBar();
                        _mode = Mode.EditPoint;
                        _pointIndex = pointIndex;
                        _pointAt = PointObject()!.Points![pointIndex];
                        _moved = false;
                        _down = pt;
                        return true;
                    }
                    if (message == WM_LBUTTONDOWN && HandleAt(pt, view) is int handle and >= 0)
                    {
                        HideBar();
                        _mode = Mode.Resize;
                        _handle = handle;
                        _startRect = UnitRectMm();
                        _sx = _sy = 1;
                        _moved = false;
                        _down = pt;
                        Log.Info("Resize start, handle " + handle);
                        return true;
                    }
                    if (message == WM_LBUTTONDOWN && Inside(pt, view, inset: 6))
                    {
                        Log.Info("Drag start at " + pt.X + "," + pt.Y);
                        HideBar();
                        _mode = Mode.Drag;
                        _down = pt;
                        _delta = Vec2.Zero;
                        _moved = false;
                        return true;
                    }
                    if (message == WM_RBUTTONDOWN && Inside(pt, view, inset: 0)) return true;
                    if (message == WM_RBUTTONUP && Inside(pt, view, inset: 0))
                    {
                        OpenPie(pt, view);
                        return true;
                    }
                    return false;

                case Mode.EditPoint:
                    if (message == WM_MOUSEMOVE)
                    {
                        if (!_moved && Math.Abs(pt.X - _down.X) + Math.Abs(pt.Y - _down.Y) < 3 * view.DeviceScale) return true;
                        _moved = true;
                        UpdatePointEdit(pt, view);
                        return true;
                    }
                    if (message == WM_LBUTTONUP)
                    {
                        var unit = _unit;
                        var index = _pointIndex;
                        var at = _pointAt;
                        var moved = _moved;
                        _mode = Mode.Armed;
                        _overlay.Clear();
                        if (moved && unit is not null) Post(() => ApplyPoint(unit, index, at));
                        else if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
                        return true;
                    }
                    return message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_RBUTTONUP;

                case Mode.Resize:
                    if (message == WM_MOUSEMOVE)
                    {
                        if (!_moved && Math.Abs(pt.X - _down.X) + Math.Abs(pt.Y - _down.Y) < 3 * view.DeviceScale) return true;
                        _moved = true;
                        UpdateResize(pt, view);
                        return true;
                    }
                    if (message == WM_LBUTTONUP)
                    {
                        var unit = _unit;
                        double sx = _sx, sy = _sy;
                        var pivot = _pivot;
                        var moved = _moved;
                        _mode = Mode.Armed;
                        _overlay.Clear();
                        Log.Info($"Resize end: moved={moved}, scale=({sx:0.###}, {sy:0.###}), gear mesh snap={_resizeMeshed}");
                        if (moved && unit is not null && (Math.Abs(sx - 1) > 1e-4 || Math.Abs(sy - 1) > 1e-4)) Post(() => ApplyResize(unit, pivot, sx, sy));
                        else if (_view is not null) ShowBar(_view);
                        return true;
                    }
                    return message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_RBUTTONUP;

                case Mode.Drag:
                    if (message == WM_MOUSEMOVE)
                    {
                        if (!_moved && Math.Abs(pt.X - _down.X) + Math.Abs(pt.Y - _down.Y) < 3 * view.DeviceScale) return true;
                        _moved = true;
                        var raw = view.ToMm(pt) - view.ToMm(_down);
                        var snap = KeyDown(VK_MENU)
                            ? SnapResult.None(raw)
                            : Snapper.SnapMove(_unitAnchors, raw, _targets, Options(view));
                        _delta = snap.Delta;
                        _guides = snap.Guides;
                        DrawDrag(view);
                        return true;
                    }
                    if (message == WM_LBUTTONUP)
                    {
                        var unit = _unit;
                        var delta = _delta;
                        var moved = _moved;
                        _mode = Mode.Armed;
                        _overlay.Clear();
                        Log.Info($"Drag end: moved={moved}, delta=({delta.X:0.##}, {delta.Y:0.##}) mm, snap guides {_guides.Count}");
                        if (moved && unit is not null && delta.Length > 1e-6) Post(() => ApplyMove(unit, delta));
                        return true;
                    }
                    return message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_RBUTTONUP;

                case Mode.Pie:
                    {
                        var pie = _pie;
                        if (pie is null) { _mode = Mode.Armed; return false; }
                        var p = new PointF(pt.X, pt.Y);
                        if (message == WM_MOUSEMOVE)
                        {
                            var h = pie.HitTest(p);
                            if (h != pie.Hover) { pie.Hover = h; DrawPie(view); }
                            return false;
                        }
                        if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN)
                        {
                            if (pie.Contains(p)) return true;
                            Cancel("click outside pie");
                            return false;
                        }
                        if (message is WM_LBUTTONUP or WM_RBUTTONUP)
                        {
                            var idx = pie.HitTest(p);
                            if (idx >= 0 && pie.Items[idx].Enabled && pie.Items[idx].Id.StartsWith("menu:", StringComparison.Ordinal))
                            {
                                var sub = pie.Items[idx].Id.Substring(5);
                                OpenPie(new POINT { X = (int)pie.Centre.X, Y = (int)pie.Centre.Y }, view, sub == "back" ? null : sub);
                                return true;
                            }
                            var unit = _unit;
                            Cancel("pie closed");
                            if (idx >= 0 && pie.Items[idx].Enabled && unit is not null)
                            {
                                var id = pie.Items[idx].Id;
                                Post(() => ApplyAction(unit, id));
                            }
                            return true;
                        }
                        return false;
                    }
            }
            return false;
        }

        private bool OnKey(int vk, bool shift, bool ctrl, bool alt)
        {
            // Typing (text, shortcuts) hides the bar until the next selection, as in Slidemax.
            if (_mode == Mode.Armed && vk is not (VK_SHIFT or VK_CONTROL or VK_MENU) && _bar is not null)
            {
                _fade.Dismiss();
                HideBar();
            }
            if (vk == VK_ESCAPE && _mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint)
            {
                Cancel("escape");
                return true;
            }
            return false;
        }

        private bool Inside(POINT pt, SlideView view, int inset)
        {
            if (_unit is null) return false;
            var b = _unit.BoundsPt;
            var a = view.ToScreen(new Vec2(b.X / Geo.PtPerMm, b.Y / Geo.PtPerMm));
            var c = view.ToScreen(new Vec2(b.Right / Geo.PtPerMm, b.Bottom / Geo.PtPerMm));
            var d = (float)(inset * view.DeviceScale);
            var r = RectangleF.FromLTRB(a.X + d, a.Y + d, c.X - d, c.Y - d);
            // Thin units (lines, shafts): keep a minimum grab height around the centre line.
            if (r.Height < 8 * view.DeviceScale) r = RectangleF.FromLTRB(r.Left, (a.Y + c.Y) / 2 - 5, r.Right, (a.Y + c.Y) / 2 + 5);
            if (r.Width < 8 * view.DeviceScale) r = RectangleF.FromLTRB((a.X + c.X) / 2 - 5, r.Top, (a.X + c.X) / 2 + 5, r.Bottom);
            return r.Contains(pt.X, pt.Y);
        }

        private static SnapOptions Options(SlideView view) => new()
        {
            Tolerance = view.Mm(8),
            Grid = null,
            Page = true,
        };

        // ------------------------------------------------------------------ drawing

        private void UpdatePlacement(POINT pt)
        {
            var view = _view;
            var item = _item;
            if (view is null || item is null) return;
            var registry = _registry();
            var raw = view.ToMm(pt);
            if (IsTwoPoint(item))
            {
                var snapped = KeyDown(VK_MENU) ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
                _placeAt = raw + snapped.Delta;
                var marker = (float)view.Mm(5);
                if (_twoStart is null)
                {
                    var at0 = _placeAt;
                    Present(view, g =>
                    {
                        using var pen = new Pen(Ghost, (float)view.Mm(1.6));
                        g.DrawLine(pen, (float)at0.X - marker, (float)at0.Y, (float)at0.X + marker, (float)at0.Y);
                        g.DrawLine(pen, (float)at0.X, (float)at0.Y - marker, (float)at0.X, (float)at0.Y + marker);
                        DrawGuides(g, view, snapped.Guides);
                    });
                    return;
                }
                var line = CreateBetween(registry, item, _twoStart.Value, _placeAt);
                var ldoc = SketchDocument.Create(new Size2(view.SlideWidth / Geo.PtPerMm, view.SlideHeight / Geo.PtPerMm));
                ldoc = Edits.Add(ldoc, ldoc.Pages[0].Id, line);
                var lprims = registry.Render(line, new RenderContext(ldoc, _lang()));
                Present(view, g =>
                {
                    ThumbnailRenderer.DrawAll(g, lprims, Ghost, (float)view.Mm(1.6));
                    DrawGuides(g, view, snapped.Guides);
                });
                return;
            }
            var probe = registry.Create(item, raw);
            var doc = SketchDocument.Create(new Size2(view.SlideWidth / Geo.PtPerMm, view.SlideHeight / Geo.PtPerMm));
            doc = Edits.Add(doc, doc.Pages[0].Id, probe);
            var ctx = new RenderContext(doc, _lang());
            var snap = KeyDown(VK_MENU)
                ? SnapResult.None(Vec2.Zero)
                : Snapper.SnapMove(registry.PageAnchors(probe, ctx).ToList(), Vec2.Zero, _targets, Options(view));
            _placeAt = raw + snap.Delta;
            doc = Edits.Move(doc, new[] { probe.Id }, snap.Delta);
            var prims = registry.Render(doc.Get(probe.Id)!, new RenderContext(doc, _lang()));
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, snap.Guides);
            });
        }

        private void DrawDrag(SlideView view)
        {
            var delta = _delta;
            var m = Matrix2D.Translation(delta.X, delta.Y);
            var moved = _unitPrims.Select(p => Prim.Transform(p, m)).ToList();
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, moved, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, _guides);
            });
        }

        private void DrawGuides(Graphics g, SlideView view, IReadOnlyList<SnapGuide> guides)
        {
            using var pen = new Pen(GuideColor, (float)view.Mm(1.2)) { DashPattern = new[] { 4f, 3f } };
            using var dot = new SolidBrush(GuideColor);
            foreach (var gd in guides)
            {
                switch (gd.Kind)
                {
                    case GuideKind.Vertical:
                        g.DrawLine(pen, (float)gd.A.X, 0, (float)gd.A.X, (float)(view.SlideHeight / Geo.PtPerMm));
                        break;
                    case GuideKind.Horizontal:
                        g.DrawLine(pen, 0, (float)gd.A.Y, (float)(view.SlideWidth / Geo.PtPerMm), (float)gd.A.Y);
                        break;
                    case GuideKind.Segment:
                        g.DrawLine(pen, (float)gd.A.X, (float)gd.A.Y, (float)gd.B.X, (float)gd.B.Y);
                        break;
                    case GuideKind.Circle:
                        g.DrawEllipse(pen, (float)(gd.A.X - gd.Radius), (float)(gd.A.Y - gd.Radius), (float)(2 * gd.Radius), (float)(2 * gd.Radius));
                        break;
                    case GuideKind.Point:
                        var r = (float)view.Mm(4);
                        g.FillEllipse(dot, (float)gd.A.X - r, (float)gd.A.Y - r, 2 * r, 2 * r);
                        break;
                }
            }
        }

        /// <summary>Draws into a pane-sized bitmap with a page-millimetre transform and shows it.</summary>
        private void Present(SlideView view, Action<Graphics> draw, Action<Graphics>? screenSpace = null)
        {
            using var bmp = OverlayWindow.Canvas(view.PaneRect.Size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var state = g.Save();
                g.TranslateTransform((float)(view.OffsetX - view.PaneRect.X), (float)(view.OffsetY - view.PaneRect.Y));
                g.ScaleTransform((float)view.PixelsPerMm, (float)view.PixelsPerMm);
                draw(g);
                g.Restore(state);
                if (screenSpace is not null)
                {
                    g.TranslateTransform(-view.PaneRect.X, -view.PaneRect.Y);
                    screenSpace(g);
                }
            }
            _overlay.Present(bmp, view.PaneRect, view.MainWindow);
        }

        // ------------------------------------------------------------------ floating bar

        private List<BarButton> BarButtons()
        {
            var registry = _registry();
            var list = new List<BarButton>();
            var first = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            var type = first is null ? null : registry.Type(first.Type);
            if (type is not null && first is not null && type.Views.Count > 1)
            {
                foreach (var v in type.Views.Take(5))
                    list.Add(new BarButton("view:" + v.Id, v.Label.Get(_lang()), v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id));
                list.Add(new BarButton("", "", "", Separator: true));
            }
            list.Add(new BarButton("flipH", "\u21C6", T("Mirror horizontally", "Horizontal spiegeln")));
            list.Add(new BarButton("flipV", "\u21C5", T("Mirror vertically", "Vertikal spiegeln")));
            list.Add(new BarButton("rot90", "\u21BB", T("Rotate 90 degrees", "Um 90 Grad drehen")));
            list.Add(new BarButton("dup", "\u29C9", T("Duplicate", "Duplizieren")));
            list.Add(new BarButton("", "", "", Separator: true));
            list.Add(new BarButton("delete", "\u2715", T("Delete", "L\u00f6schen"), Danger: true));
            return list;
        }

        private RectangleF UnitScreenRect(SlideView view)
        {
            var b = _unit!.BoundsPt;
            var a = view.ToScreen(new Vec2(b.X / Geo.PtPerMm, b.Y / Geo.PtPerMm));
            var c = view.ToScreen(new Vec2(b.Right / Geo.PtPerMm, b.Bottom / Geo.PtPerMm));
            return RectangleF.FromLTRB(a.X, a.Y, c.X, c.Y);
        }

        /// <summary>Shows the bar next to the selection (a dismissed bar comes back only with the next selection).</summary>
        private void ShowBar(SlideView view)
        {
            if (_unit is null) return;
            if (_bar is null || _fade.Dismissed) _fade.Reset();
            _bar = new FloatingBar(BarButtons(), (float)view.DeviceScale);
            _bar.Place(UnitScreenRect(view), view.PaneRect);
            GetCursorPos(out var p);
            var opacity = _fade.Update(_bar.Distance(new Point(p.X, p.Y)));
            Log.Info($"Bar shown at {_bar.Bounds.X},{_bar.Bounds.Y},{_bar.Bounds.Width},{_bar.Bounds.Height} opacity {opacity:0.##}");
            DrawBar(view, opacity);
        }

        private void HideBar()
        {
            _barPressed = false;
            _barAlpha = 0;
            _barWindow.Clear();
        }

        private void DrawBar(SlideView view, double opacity)
        {
            var bar = _bar;
            if (bar is null || opacity <= 0) { HideBar(); return; }
            _barAlpha = (byte)Math.Round(255 * Math.Max(0, Math.Min(1, opacity)));
            using var bmp = bar.Render(dark: false);
            _barWindow.Present(bmp, bar.Bounds, view.MainWindow, _barAlpha);
        }

        /// <summary>Bar input while armed. Returns true if the message concerns the bar; swallow tells whether to eat it.</summary>
        private bool HandleBar(int message, POINT pt, SlideView view, out bool swallow)
        {
            swallow = false;
            var bar = _bar;
            if (bar is null || _fade.Dismissed) return false;
            var p = new Point(pt.X, pt.Y);
            var inside = bar.Contains(p);
            if (message == WM_MOUSEMOVE)
            {
                var hover = inside ? bar.HitTest(p) : -1;
                var opacity = _fade.Update(bar.Distance(p));
                var alpha = (byte)Math.Round(255 * opacity);
                if (hover != bar.Hover || Math.Abs(alpha - _barAlpha) >= 6 || opacity <= 0)
                {
                    bar.Hover = hover;
                    DrawBar(view, opacity);
                }
                return inside;
            }
            if (!inside)
            {
                if (_barPressed && message == WM_LBUTTONUP) { _barPressed = false; swallow = true; return true; }
                return false;
            }
            swallow = true;
            if (message == WM_LBUTTONDOWN || message == WM_LBUTTONDBLCLK) { _barPressed = true; return true; }
            if (message == WM_LBUTTONUP)
            {
                var idx = bar.HitTest(p);
                var unit = _unit;
                var pressed = _barPressed;
                _barPressed = false;
                if (pressed && idx >= 0 && unit is not null)
                {
                    var id = bar.Buttons[idx].Id;
                    Log.Info("Bar: " + id);
                    Post(() => ApplyAction(unit, id));
                }
                return true;
            }
            return true; // right button and other messages over the bar never reach the slide
        }

        /// <summary>Point-geometry types a selected line/arrow/dimension/lead line can be converted to.</summary>
        private static readonly string[] ConvertTargets =
        {
            "ann.line", "ann.arrow", "ann.arrow.double", "ann.arrow.curved", "ann.arrow.elbow", "ann.leader",
            "patent.numeral", "ann.dimension.linear", "ann.dimension.aligned",
        };

        private List<PieItem> SubMenu(string menu, SketchObject o)
        {
            var registry = _registry();
            var lang = _lang();
            var items = new List<PieItem> { new("menu:back", T("\u2190 Back", "\u2190 Zur\u00fcck")) };
            if (menu == "type")
            {
                foreach (var id in ConvertTargets)
                {
                    var t = registry.Type(id);
                    if (t is null || t.Geometry != GeometryKind.Points) continue;
                    items.Add(new PieItem("conv:" + id, t.Name.Get(lang), Active: o.Type == id));
                }
                return items;
            }
            // Label menu, generated from the type's optional parameters (module-agnostic).
            var type = registry.Type(o.Type);
            if (type is null) return items;
            foreach (var pd in type.Parameters)
            {
                if (items.Count >= 11) break;
                var label = pd.Label.Get(lang);
                switch (pd.Kind)
                {
                    case ParamKind.Text:
                        items.Add(new PieItem("edit:" + pd.Key, label + " \u2026"));
                        break;
                    case ParamKind.Boolean:
                        {
                            var cur = o.Flag(pd.Key, pd.Default?.Bool ?? false);
                            items.Add(new PieItem("param:" + pd.Key + ":" + (cur ? "false" : "true"), label, Active: cur));
                            break;
                        }
                    case ParamKind.Choice when pd.Options is { Count: > 1 }:
                        {
                            var cur = o.Str(pd.Key, pd.Default?.Text ?? pd.Options[0].Value);
                            var i = pd.Options.ToList().FindIndex(x => x.Value == cur);
                            var curLabel = i >= 0 ? pd.Options[i].Label.Get(lang) : cur;
                            var next = pd.Options[(i + 1 + pd.Options.Count) % pd.Options.Count].Value;
                            items.Add(new PieItem("param:" + pd.Key + ":" + next, label + ": " + curLabel));
                            break;
                        }
                    case ParamKind.Number when pd.Key == "textSize":
                        items.Add(new PieItem("size:" + pd.Key + ":1.2", T("Text larger", "Text gr\u00f6\u00dfer")));
                        items.Add(new PieItem("size:" + pd.Key + ":0.8333", T("Text smaller", "Text kleiner")));
                        break;
                }
            }
            return items;
        }

        private void OpenPie(POINT pt, SlideView view, string? submenu = null)
        {
            if (_unit is null) return;
            var registry = _registry();
            var pointObject = PointObject();
            if (submenu is not null && pointObject is not null)
            {
                _pie = new PieMenu(new PointF(pt.X, pt.Y), SubMenu(submenu, pointObject), (float)view.DeviceScale);
                _mode = Mode.Pie;
                DrawPie(view);
                return;
            }
            var items = new List<PieItem> { new("delete", T("Delete", "L\u00f6schen"), Danger: true) };
            if (pointObject is not null)
            {
                items.Add(new PieItem("menu:type", T("Type \u25B8", "Typ \u25B8")));
                items.Add(new PieItem("menu:label", T("Label \u25B8", "Beschriftung \u25B8")));
            }
            var first = _unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            var type = first is null ? null : registry.Type(first.Type);
            if (type is not null && first is not null)
            {
                foreach (var v in type.Views.Take(5))
                    items.Add(new PieItem("view:" + v.Id, v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id));
                foreach (var a in type.Actions(first, _lang()).Where(a => a.Group != "view").Take(3))
                    items.Add(new PieItem("act:" + a.Id, a.Label.Get(_lang()), Active: a.IsActive));
            }
            items.Add(new PieItem("dup", T("Duplicate", "Duplizieren")));
            items.Add(new PieItem("flipH", T("Mirror ↔", "Spiegeln ↔")));
            items.Add(new PieItem("flipV", T("Mirror ↕", "Spiegeln ↕")));
            items.Add(new PieItem("rot90", T("Rotate 90°", "Drehen 90°")));
            HideBar();
            _pie = new PieMenu(new PointF(pt.X, pt.Y), items, (float)view.DeviceScale);
            _mode = Mode.Pie;
            DrawPie(view);
        }

        private void DrawPie(SlideView view)
        {
            var pie = _pie;
            if (pie is null) return;
            Present(view, _ => { }, g => pie.Draw(g, dark: false));
        }

        // ------------------------------------------------------------------ commands (UI thread, outside the hook)

        private void Post(Action action)
        {
            _overlay.BeginInvoke(new Action(() =>
            {
                try
                {
                    using (DpiScope.PerMonitor()) action();
                }
                catch (Exception ex)
                {
                    Log.Error("On-slide command", ex);
                }
            }));
        }

        private PowerPoint.Slide? CurrentSlide()
        {
            try { return _app.Windows.Count == 0 ? null : _app.ActiveWindow.View.Slide as PowerPoint.Slide; }
            catch { return null; }
        }

        private List<Anchor> CollectTargets(PowerPoint.Slide slide, string? exceptId)
        {
            var registry = _registry();
            var list = new List<Anchor>();
            foreach (var unit in SketchUnits.All(slide, exceptId))
            {
                var ctx = new RenderContext(unit.Doc, _lang());
                foreach (var o in unit.Objects)
                    if (registry.Type(o.Type) is not null) list.AddRange(registry.PageAnchors(o, ctx));
            }
            var w = (_view?.SlideWidth ?? 960) / Geo.PtPerMm;
            var h = (_view?.SlideHeight ?? 540) / Geo.PtPerMm;
            list.Add(Anchor.Point(AnchorRole.Center, new Vec2(w / 2, h / 2), "page"));
            return list;
        }

        private void InsertAt(LibraryItem item, Vec2 at)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var setup = _app.ActivePresentation.PageSetup;
            var doc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
            doc = Edits.Add(doc, doc.Pages[0].Id, registry.Create(item, at));
            _app.StartNewUndoEntry();
            var shape = SketchUnits.Draw(slide, registry, doc, _lang());
            shape?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Inserted {item.Id} at ({at.X:0.#}, {at.Y:0.#}) mm");
        }

        // ------------------------------------------------------------------ two-point tool and point editing

        private bool IsTwoPoint(LibraryItem? item)
            => item is not null && _registry().Type(item.TypeId)?.Geometry == GeometryKind.Points;

        /// <summary>
        /// Creates a point-geometry object whose first and last points are <paramref name="start"/> and
        /// <paramref name="end"/>; intermediate default points (curve control points) keep their relative position.
        /// </summary>
        private static SketchObject CreateBetween(Registry registry, LibraryItem item, Vec2 start, Vec2 end)
        {
            var obj = registry.Create(item, start);
            var pts = obj.Points ?? new[] { start, end };
            if (pts.Count < 2) return obj with { Points = new[] { start, end } };
            Vec2 a = pts[0], b = pts[pts.Count - 1];
            var axis = b - a;
            var l2 = axis.Dot(axis);
            var target = end - start;
            var mapped = pts.Select((q, i) =>
            {
                if (i == 0) return start;
                if (i == pts.Count - 1) return end;
                if (l2 < Geo.Eps) return Vec2.Lerp(start, end, (double)i / (pts.Count - 1));
                var u = (q - a).Dot(axis) / l2;
                var v = (q - a).Cross(axis) / l2;
                return start + target * u - target.Perp * v;
            }).ToList();
            return obj with { Points = mapped };
        }

        private void InsertPoints(LibraryItem item, Vec2 start, Vec2 end)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var setup = _app.ActivePresentation.PageSetup;
            var doc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
            doc = Edits.Add(doc, doc.Pages[0].Id, CreateBetween(registry, item, start, end));
            _app.StartNewUndoEntry();
            SketchUnits.Draw(slide, registry, doc, _lang())?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Inserted {item.Id} from ({start.X:0.#}, {start.Y:0.#}) to ({end.X:0.#}, {end.Y:0.#}) mm");
        }

        /// <summary>The single point-geometry object of the armed unit (lines, arrows, dimensions, lead lines), or null.</summary>
        private SketchObject? PointObject()
        {
            var objs = _unit?.Objects.ToList();
            return objs is { Count: 1 } && objs[0].IsPointGeometry && objs[0].Points is { Count: >= 2 } ? objs[0] : null;
        }

        private int PointHandleAt(POINT pt, SlideView view)
        {
            var o = PointObject();
            if (o is null) return -1;
            var tol = 9 * view.DeviceScale;
            var pts = _unit!.Doc.ResolvePoints(o);
            var best = -1;
            var bestD = double.MaxValue;
            for (var i = 0; i < pts.Count; i++)
            {
                var sp = view.ToScreen(pts[i]);
                var d = Math.Sqrt((sp.X - pt.X) * (sp.X - pt.X) + (sp.Y - pt.Y) * (sp.Y - pt.Y));
                if (d <= tol && d < bestD) { best = i; bestD = d; }
            }
            return best;
        }

        /// <summary>Small diamond handles on the points of a selected line/arrow/dimension/lead line.</summary>
        private void DrawPointHandles(SlideView view)
        {
            var o = PointObject();
            if (o is null) return;
            var pts = _unit!.Doc.ResolvePoints(o);
            Present(view, _ => { }, g =>
            {
                var r = (float)(5 * view.DeviceScale);
                using var fill = new SolidBrush(Color.White);
                using var pen = new Pen(Ghost, (float)(1.6 * view.DeviceScale));
                foreach (var p in pts)
                {
                    var c = view.ToScreen(p);
                    var diamond = new[] { new PointF(c.X, c.Y - r), new PointF(c.X + r, c.Y), new PointF(c.X, c.Y + r), new PointF(c.X - r, c.Y) };
                    g.FillPolygon(fill, diamond);
                    g.DrawPolygon(pen, diamond);
                }
            });
        }

        private void UpdatePointEdit(POINT pt, SlideView view)
        {
            var o = PointObject();
            if (o is null) return;
            var raw = view.ToMm(pt);
            var snap = KeyDown(VK_MENU) ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
            _pointAt = raw + snap.Delta;
            var doc = Edits.SetPoint(_unit!.Doc, o.Id, _pointIndex, _pointAt);
            var prims = _registry().Render(doc.Get(o.Id)!, new RenderContext(doc, _lang()));
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, snap.Guides);
            });
        }

        private void ApplyPoint(SketchUnit stale, int index, Vec2 at)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var o = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (o is null) return;
            _app.StartNewUndoEntry();
            var doc = Edits.SetPoint(unit.Doc, o.Id, index, at);
            SketchUnits.Replace(slide, unit, doc, _registry(), _lang())?.Select(Office.MsoTriState.msoTrue);
        }

        /// <summary>
        /// Converts a line/arrow/dimension/lead line into another point-geometry type: first and last points stay,
        /// curve control points come from the target's defaults, text/numeral, layer, group and colour are kept.
        /// </summary>
        private static SketchObject Convert(Registry registry, SketchObject o, string targetType)
        {
            var target = registry.Type(targetType);
            if (target is null || o.Points is not { Count: >= 2 } || o.Type == targetType) return o;
            var item = registry.Library.FirstOrDefault(i => i.Id == targetType && i.TypeId == targetType)
                ?? new LibraryItem(targetType, targetType, target.Name, "convert");
            var created = CreateBetween(registry, item, o.Points[0], o.Points[o.Points.Count - 1]);
            var numeral = o.Type is "ann.leader" or "patent.numeral" ? (o.Text ?? o.Str("numeral", o.Ref ?? "")) : o.Ref;
            var result = created with
            {
                Id = o.Id,
                GroupId = o.GroupId,
                Layer = o.Layer,
                Ref = numeral,
                Style = o.Style is null ? null : new Style { Stroke = o.Style.Stroke, StrokeWidth = o.Style.StrokeWidth, Opacity = o.Style.Opacity },
            };
            if (!string.IsNullOrEmpty(numeral) && target.Parameters.Any(pd => pd.Key == "numeral")) result = result.WithParam("numeral", numeral!) with { Text = numeral };
            return result;
        }

        // ------------------------------------------------------------------ resize with snapping

        /// <summary>Unit bounds in page millimetres.</summary>
        private Rect UnitRectMm()
        {
            var b = _unit!.BoundsPt;
            return new Rect(b.X / Geo.PtPerMm, b.Y / Geo.PtPerMm, b.Width / Geo.PtPerMm, b.Height / Geo.PtPerMm);
        }

        /// <summary>Handle positions 0..7: top-left, top, top-right, right, bottom-right, bottom, bottom-left, left.</summary>
        private static Vec2 HandlePoint(Rect r, int h) => h switch
        {
            0 => new Vec2(r.X, r.Y),
            1 => new Vec2(r.X + r.W / 2, r.Y),
            2 => new Vec2(r.Right, r.Y),
            3 => new Vec2(r.Right, r.Y + r.H / 2),
            4 => new Vec2(r.Right, r.Bottom),
            5 => new Vec2(r.X + r.W / 2, r.Bottom),
            6 => new Vec2(r.X, r.Bottom),
            _ => new Vec2(r.X, r.Y + r.H / 2),
        };

        private static bool IsCorner(int h) => h % 2 == 0;

        /// <summary>PowerPoint's resize handle under the pointer, or -1.</summary>
        private int HandleAt(POINT pt, SlideView view)
        {
            if (_unit is null) return -1;
            var r = UnitRectMm();
            var tol = 9 * view.DeviceScale;
            var best = -1;
            var bestD = double.MaxValue;
            for (var h = 0; h < 8; h++)
            {
                // Edge handles of very thin units coincide with corners; PowerPoint hides them, so do we.
                if (!IsCorner(h) && ((h is 1 or 5 && r.W * view.PixelsPerMm < 30) || (h is 3 or 7 && r.H * view.PixelsPerMm < 30))) continue;
                var s = view.ToScreen(HandlePoint(r, h));
                var d = Math.Sqrt((s.X - pt.X) * (s.X - pt.X) + (s.Y - pt.Y) * (s.Y - pt.Y));
                if (d <= tol && d < bestD) { best = h; bestD = d; }
            }
            return best;
        }

        /// <summary>
        /// Computes the scale for the dragged handle. Corner handles scale uniformly (Shift: free), edge handles stretch one
        /// axis; Ctrl scales about the centre. Snapping (unless Alt): the handle point snaps to points and alignment
        /// guides of other objects; with uniform scaling, gear pitch circles snap into mesh with neighbouring gears.
        /// </summary>
        private void UpdateResize(POINT pt, SlideView view)
        {
            var r = _startRect;
            var h = _handle;
            var h0 = HandlePoint(r, h);
            var centre = new Vec2(r.X + r.W / 2, r.Y + r.H / 2);
            _pivot = KeyDown(VK_CONTROL) ? centre : HandlePoint(r, (h + 4) % 8);
            var p = view.ToMm(pt);
            var snap = !KeyDown(VK_MENU);
            var guides = new List<SnapGuide>();
            if (snap)
            {
                var res = Snapper.SnapPoint(p, _targets, Options(view) with { Semantic = false });
                p += res.Delta;
                guides.AddRange(res.Guides);
            }
            var uniform = IsCorner(h) && !KeyDown(VK_SHIFT);
            _resizeMeshed = false;
            var arm = h0 - _pivot;
            double sx = 1, sy = 1;
            if (uniform)
            {
                var l2 = arm.Dot(arm);
                var s = l2 < Geo.Eps ? 1 : (p - _pivot).Dot(arm) / l2;
                s = Math.Max(0.05, s);
                if (snap && TryMeshScale(s, view, out var meshed, out var guide))
                {
                    s = meshed;
                    _resizeMeshed = true;
                    guides.Clear();
                    guides.Add(guide);
                }
                sx = sy = s;
            }
            else
            {
                if (h is 0 or 2 or 3 or 4 or 6 or 7 && Math.Abs(arm.X) > Geo.Eps) sx = Math.Max(0.05, (p.X - _pivot.X) / arm.X);
                if (h is 0 or 1 or 2 or 4 or 5 or 6 && Math.Abs(arm.Y) > Geo.Eps) sy = Math.Max(0.05, (p.Y - _pivot.Y) / arm.Y);
            }
            _sx = sx;
            _sy = sy;
            var registry = _registry();
            var unit = _unit!;
            var doc = Edits.Scale(unit.Doc, unit.Doc.Objects.Keys.ToList(), _pivot, sx, sy);
            var ctx = new RenderContext(doc, _lang());
            var prims = doc.ObjectsOn(doc.Pages[0]).Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
            var label = uniform ? $"×{sx:0.00}" : $"×{sx:0.00} / {sy:0.00}";
            var at = view.ToScreen(p);
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, guides);
            }, g =>
            {
                using var font = new Font("Segoe UI", (float)(11 * view.DeviceScale), FontStyle.Bold, GraphicsUnit.Pixel);
                using var bg = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                using var fg = new SolidBrush(Ghost);
                var size = g.MeasureString(label, font);
                var box = new RectangleF(at.X + 14 * (float)view.DeviceScale, at.Y + 14 * (float)view.DeviceScale, size.Width + 8, size.Height + 4);
                g.FillRectangle(bg, box);
                g.DrawString(label, font, fg, box.X + 4, box.Y + 2);
            });
        }

        /// <summary>
        /// Uniform scale near <paramref name="s"/> at which a pitch circle of the unit touches a pitch circle of another
        /// gear (external mesh) or a ring gear (internal mesh). Newton iteration on the centre-distance condition.
        /// </summary>
        private bool TryMeshScale(double s, SlideView view, out double result, out SnapGuide guide)
        {
            result = s;
            guide = new SnapGuide(GuideKind.Point, Vec2.Zero, Vec2.Zero);
            var tolMm = view.Mm(8);
            var armLength = (HandlePoint(_startRect, _handle) - _pivot).Length;
            var best = double.MaxValue;
            var found = false;
            foreach (var m in _unitAnchors.Where(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch))
            {
                foreach (var t in _targets.Where(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner))
                {
                    var internalMesh = t.Role == AnchorRole.PitchInner;
                    double F(double k)
                    {
                        var c = _pivot + (m.P - _pivot) * k;
                        var d = (c - t.P).Length;
                        return internalMesh ? d - (t.Radius - m.Radius * k) : d - (m.Radius * k + t.Radius);
                    }
                    var x = s;
                    for (var i = 0; i < 12; i++)
                    {
                        var f = F(x);
                        var df = (F(x + 1e-5) - f) / 1e-5;
                        if (Math.Abs(df) < 1e-12) break;
                        var nx = x - f / df;
                        if (Math.Abs(nx - x) < 1e-9) { x = nx; break; }
                        x = nx;
                    }
                    if (x <= 0.05 || Math.Abs(F(x)) > 1e-3) continue;
                    var handleShift = Math.Abs(x - s) * armLength;
                    if (handleShift > tolMm || handleShift >= best) continue;
                    best = handleShift;
                    found = true;
                    result = x;
                    guide = new SnapGuide(GuideKind.Circle, t.P, t.P, t.Radius);
                }
            }
            return found;
        }

        private void ApplyResize(SketchUnit stale, Vec2 pivot, double sx, double sy)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            _app.StartNewUndoEntry();
            var doc = Edits.Scale(unit.Doc, unit.Doc.Objects.Keys.ToList(), pivot, sx, sy);
            var shape = SketchUnits.Replace(slide, unit, doc, _registry(), _lang());
            shape?.Select(Office.MsoTriState.msoTrue);
        }

        private void ApplyMove(SketchUnit unit, Vec2 deltaMm)
        {
            _app.StartNewUndoEntry();
            unit.Shape.Left += (float)(deltaMm.X * Geo.PtPerMm);
            unit.Shape.Top += (float)(deltaMm.Y * Geo.PtPerMm);
            unit.Shape.Select(Office.MsoTriState.msoTrue);
            var fresh = SketchUnits.Read(unit.Shape);
            if (fresh is not null) Arm(fresh);
        }

        private void ApplyAction(SketchUnit stale, string id)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var doc = unit.Doc;
            var ids = doc.Objects.Keys.ToList();
            var b = unit.BoundsPt;
            var centre = new Vec2((b.X + b.Width / 2) / Geo.PtPerMm, (b.Y + b.Height / 2) / Geo.PtPerMm);
            _app.StartNewUndoEntry();
            if (id == "delete")
            {
                unit.Shape.Delete();
                Disarm();
                return;
            }
            if (id == "dup")
            {
                var copy = Edits.Move(doc, ids, new Vec2(5, 5));
                SketchUnits.Draw(slide, registry, copy, _lang())?.Select(Office.MsoTriState.msoTrue);
                return;
            }
            if (id.StartsWith("view:", StringComparison.Ordinal))
            {
                var view = id.Substring(5);
                doc = Edits.Update(doc, ids, o => registry.Type(o.Type)?.Views.Any(v => v.Id == view) == true ? o with { View = view } : o);
            }
            else if (id.StartsWith("act:", StringComparison.Ordinal))
            {
                var actionId = id.Substring(4);
                doc = Edits.Update(doc, ids, o =>
                {
                    var action = registry.Type(o.Type)?.Actions(o, _lang()).FirstOrDefault(a => a.Id == actionId);
                    return action is null ? o : action.Apply(o);
                });
            }
            else if (id.StartsWith("conv:", StringComparison.Ordinal))
            {
                var target = id.Substring(5);
                doc = Edits.Update(doc, ids, o => o.IsPointGeometry ? Convert(registry, o, target) : o);
            }
            else if (id.StartsWith("param:", StringComparison.Ordinal))
            {
                var parts = id.Split(new[] { ':' }, 3);
                ParamValue value = parts[2] == "true" ? true : parts[2] == "false" ? false : (ParamValue)parts[2];
                doc = Edits.Update(doc, ids, o => registry.Type(o.Type)?.Parameters.Any(pd => pd.Key == parts[1]) == true ? o.WithParam(parts[1], value) : o);
            }
            else if (id.StartsWith("size:", StringComparison.Ordinal))
            {
                var parts = id.Split(':');
                var factor = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                doc = Edits.Update(doc, ids, o =>
                {
                    var pd = registry.Type(o.Type)?.Parameters.FirstOrDefault(x => x.Key == parts[1]);
                    if (pd is null) return o;
                    var cur = o.Num(parts[1], pd.Default?.Number ?? 3.5);
                    return o.WithParam(parts[1], Math.Round(Math.Max(pd.Min ?? 0.5, Math.Min(pd.Max ?? 100, cur * factor)), 2));
                });
            }
            else if (id.StartsWith("edit:", StringComparison.Ordinal))
            {
                var key = id.Substring(5);
                var first = doc.Objects.Values.FirstOrDefault(o => registry.Type(o.Type)?.Parameters.Any(pd => pd.Key == key) == true);
                if (first is null) return;
                var pdef = registry.Type(first.Type)!.Parameters.First(pd => pd.Key == key);
                var current = key == "numeral" && !string.IsNullOrEmpty(first.Text) ? first.Text! : first.Str(key, pdef.Default?.Text ?? "");
                var entered = TextPrompt.Ask(pdef.Label.Get(_lang()), current, _view?.MainWindow ?? IntPtr.Zero);
                if (entered is null) return;
                doc = Edits.Update(doc, ids, o => o.Id == first.Id
                    ? (key == "numeral" ? o.WithParam(key, entered) with { Text = null, Ref = entered } : o.WithParam(key, entered))
                    : o);
            }
            else if (id == "flipH") doc = Edits.Flip(doc, ids, centre, horizontal: true);
            else if (id == "flipV") doc = Edits.Flip(doc, ids, centre, horizontal: false);
            else if (id == "rot90") doc = Edits.Rotate(doc, ids, centre, 90);
            var shape = SketchUnits.Replace(slide, unit, doc, registry, _lang());
            shape?.Select(Office.MsoTriState.msoTrue);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);
    }
}
