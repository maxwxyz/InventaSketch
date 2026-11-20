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
        private enum Mode { Idle, Armed, Place, Drag, Pie, Resize, EditPoint, Rotate }

        private static readonly Color Ghost = Color.FromArgb(240, 140, 0);
        private static readonly Color GuideColor = Color.FromArgb(220, 40, 40);

        private readonly PowerPoint.Application _app;
        private readonly Func<Registry> _registry;
        private readonly Func<Lang> _lang;
        private readonly InputHooks _hooks = new();
        private readonly OverlayWindow _overlay;
        private readonly Timer _timer;
        private Mode _mode;
        private bool _rightDownOnUnit;
        private SlideView? _view;
        private List<Anchor> _targets = new();

        // Insert at pointer
        private LibraryItem? _item;
        private Vec2 _placeAt;
        private bool _placePressed;
        private bool _loggedPlaceMessage;

        // Armed / drag
        private SketchUnit? _unit;
        /// <summary>The armed unit's model plus the other units of the slide when its objects render from neighbours.</summary>
        private SketchDocument? _unitContext;
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

        // Rotate with snapping
        private Vec2 _rotCentre;
        private double _rotStart;
        private double _rotAngle;
        private bool _dark;

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

        /// <summary>Raised when an InventaSketch unit becomes the selection (sidebar editor follows it).</summary>
        public event Action<SketchUnit>? UnitArmed;

        /// <summary>Runs a function on PowerPoint's UI thread (automation calls arrive on RPC threads).</summary>
        public T OnUi<T>(Func<T> f) => _overlay.InvokeRequired ? (T)_overlay.Invoke(f) : f();

        /// <summary>Snapping is off (ribbon toggle) or suspended (Alt held).</summary>
        private static bool NoSnap() => !AddInSettings.Snap || KeyDown(VK_MENU);

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
                if (_mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate) return;
                using var dpi = DpiScope.PerMonitor();
                SketchUnit? unit = null;
                if (sel.Type == PowerPoint.PpSelectionType.ppSelectionShapes && sel.ShapeRange.Count == 1)
                {
                    var shape = sel.ShapeRange[1];
                    if (SketchUnits.IsUnit(shape)) unit = SketchUnits.Read(shape);
                }
                if (unit is null) { Disarm(); return; }
                if (CurrentSlide() is PowerPoint.Slide slide && SketchUnits.HasTwin(slide, unit))
                {
                    _app.StartNewUndoEntry();
                    var renamed = SketchUnits.Replace(slide, unit, SketchUnits.WithNewIds(unit.Doc, Vec2.Zero), _registry(), _lang());
                    Log.Info("Copied unit got new ids: " + unit.Id + " -> " + renamed?.Tags["ISK_ID"]);
                    renamed?.Select(Office.MsoTriState.msoTrue);
                    return; // the new selection arms it
                }
                Log.Info("Selection: InventaSketch unit " + unit.Shape.Name);
                Arm(unit);
                UnitArmed?.Invoke(unit);
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
            _dark = OfficeTheme.IsDark();
            var registry = _registry();
            var ctx = new RenderContext(unit.Doc, _lang());
            _unitPrims = unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
            _unitAnchors = unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.PageAnchors(o, ctx)).ToList();
            _targets = CollectTargets(slide, unit.Id);
            _unitContext = SketchUnits.NeedsContext(registry, unit.Doc) ? SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id)) : unit.Doc;
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
            _unitContext = null;
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
            if (wasArmedUnit is not null && _mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate)
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
                if (_mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate) return;
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
                    if (message == WM_LBUTTONDOWN && OnRotationHandle(pt, view))
                    {
                        HideBar();
                        var r = UnitRectMm();
                        _rotCentre = new Vec2(r.X + r.W / 2, r.Y + r.H / 2);
                        var start = view.ToMm(pt) - _rotCentre;
                        _rotStart = Math.Atan2(start.Y, start.X);
                        _rotAngle = 0;
                        _moved = false;
                        _down = pt;
                        _mode = Mode.Rotate;
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
                    // Right-click on the selected unit (with a small margin, so PowerPoint's own menu does not appear
                    // when the click lands just beside a thin unit) opens the pie menu; the release is paired by the hook.
                    if (message == WM_RBUTTONDOWN && Inside(pt, view, inset: -4))
                    {
                        _rightDownOnUnit = true;
                        return true;
                    }
                    if (message == WM_RBUTTONUP && _rightDownOnUnit)
                    {
                        _rightDownOnUnit = false;
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

                case Mode.Rotate:
                    if (message == WM_MOUSEMOVE)
                    {
                        if (!_moved && Math.Abs(pt.X - _down.X) + Math.Abs(pt.Y - _down.Y) < 3 * view.DeviceScale) return true;
                        _moved = true;
                        UpdateRotate(pt, view);
                        return true;
                    }
                    if (message == WM_LBUTTONUP)
                    {
                        var unit = _unit;
                        var angle = _rotAngle;
                        var centre = _rotCentre;
                        var moved = _moved;
                        _mode = Mode.Armed;
                        _overlay.Clear();
                        Log.Info($"Rotate end: {angle:0.##} degrees");
                        if (moved && unit is not null && Math.Abs(angle) > 1e-3) Post(() => ApplyRotate(unit, centre, angle));
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
                        var snap = NoSnap()
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
            if (vk == VK_ESCAPE && _mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate)
            {
                Cancel("escape");
                return true;
            }
            if (_mode == Mode.Armed && _unit is not null && !alt)
            {
                var unit = _unit;
                string? command = vk switch
                {
                    0x52 when !ctrl => shift ? "rotm90" : "rot90",   // R / Shift+R
                    0x58 when !ctrl && !shift => "flipH",             // X (Ctrl+X stays "cut")
                    0x59 when !ctrl && !shift => "flipV",             // Y
                    0x44 when ctrl && !shift => "dup",                // Ctrl+D: duplicate with new ids
                    _ => null,
                };
                if (command is not null)
                {
                    Log.Info("Key: " + command);
                    Post(() => ApplyAction(unit, command));
                    return true;
                }
                // Shift+F10 or the context-menu key: pie menu at the unit centre.
                if ((vk == 0x79 && shift && !ctrl) || vk == 0x5D)
                {
                    var view = _view;
                    if (view is null) return false;
                    var b = UnitScreenRect(view);
                    OpenPie(new POINT { X = (int)(b.X + b.Width / 2), Y = (int)(b.Y + b.Height / 2) }, view);
                    return true;
                }
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
            // Thin units (lines, walls, shafts): keep a minimum grab band around the centre line (logical pixels).
            var band = (float)(6 * view.DeviceScale);
            if (r.Height < 2 * band) r = RectangleF.FromLTRB(r.Left, (a.Y + c.Y) / 2 - band, r.Right, (a.Y + c.Y) / 2 + band);
            if (r.Width < 2 * band) r = RectangleF.FromLTRB((a.X + c.X) / 2 - band, r.Top, (a.X + c.X) / 2 + band, r.Bottom);
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
                var snapped = NoSnap() ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
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
            var snap = NoSnap()
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
                foreach (var v in type.Views.Take(6))
                    list.Add(new BarButton("view:" + v.Id, v.Label.Get(_lang()), v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id) { IconId = IconPainter.ForView(v) });
                list.Add(new BarButton("", "", "", Separator: true));
            }
            list.Add(new BarButton("flipH", "", T("Mirror horizontally", "Horizontal spiegeln")) { IconId = IconIds.MirrorH });
            list.Add(new BarButton("flipV", "", T("Mirror vertically", "Vertikal spiegeln")) { IconId = IconIds.MirrorV });
            list.Add(new BarButton("rot90", "", T("Rotate 90 degrees", "Um 90 Grad drehen")) { IconId = IconIds.Rotate });
            list.Add(new BarButton("dup", "", T("Duplicate", "Duplizieren")) { IconId = IconIds.Duplicate });
            list.Add(new BarButton("", "", "", Separator: true));
            list.Add(new BarButton("delete", "", T("Delete", "L\u00f6schen"), Danger: true) { IconId = IconIds.Delete });
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
            if (!AddInSettings.Bar) { HideBar(); return; }
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
            using var bmp = bar.Render(_dark);
            _barWindow.Present(bmp, bar.WindowBounds, view.MainWindow, _barAlpha);
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

        /// <summary>Type actions of the armed unit's first object, evaluated with the neighbouring units as document context.</summary>
        private List<ContextAction> UnitActions(out SketchObject? first)
        {
            var registry = _registry();
            first = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (first is null || _unit is null) return new List<ContextAction>();
            var ctx = new RenderContext(_unitContext ?? _unit.Doc, _lang());
            return registry.Type(first.Type)!.Actions(first, ctx).Where(a => a.Group != "view").ToList();
        }

        /// <summary>Pie entry of a type action: input actions open a prompt ("…"), unavailable ones are greyed.</summary>
        private PieItem ActionItem(ContextAction a)
        {
            var label = a.Label.Get(_lang());
            if (a.Input is not null && !label.EndsWith("…", StringComparison.Ordinal)) label += " …";
            return new PieItem("act:" + a.Id, label, Enabled: a.IsEnabled, Active: a.IsActive);
        }

        private void OpenPie(POINT pt, SlideView view, string? submenu = null)
        {
            if (_unit is null) return;
            var registry = _registry();
            var pointObject = PointObject();
            if (submenu == "more")
            {
                var sub = new List<PieItem> { new("menu:back", T("← Back", "← Zurück")) };
                sub.AddRange(_pieOverflow.Take(MaxPieItems + 3));
                ShowPie(pt, view, sub);
                return;
            }
            if (submenu is not null && pointObject is not null)
            {
                ShowPie(pt, view, SubMenu(submenu, pointObject));
                return;
            }
            var (main, overflow) = PieLayout(BuildPieItems(pointObject, registry), T("More ▸", "Mehr ▸"));
            _pieOverflow = overflow;
            HideBar();
            ShowPie(pt, view, main);
        }

        /// <summary>More entries than this make the ring unreadable; the rest goes into "More".</summary>
        internal const int MaxPieItems = 8;

        private List<PieItem> _pieOverflow = new();

        /// <summary>All entries of the main pie in priority order (essential entries first, see <see cref="PieLayout"/>).</summary>
        private List<(PieItem Item, bool Essential)> BuildPieItems(SketchObject? pointObject, Registry registry)
        {
            var items = new List<(PieItem, bool)> { (new PieItem("delete", T("Delete", "Löschen"), Danger: true) { IconId = IconIds.Delete }, true) };
            // Type conversion and the label menu belong to lines, arrows, dimensions and lead lines only.
            if (pointObject is not null && Array.IndexOf(ConvertTargets, pointObject.Type) >= 0)
            {
                items.Add((new PieItem("menu:type", T("Type ▸", "Typ ▸")) { IconId = IconIds.Cycle }, true));
                items.Add((new PieItem("menu:label", T("Label ▸", "Beschriftung ▸")) { IconId = IconIds.Text }, true));
            }
            var actions = UnitActions(out var first);
            var type = first is null ? null : registry.Type(first.Type);
            if (type is not null && first is not null)
                foreach (var v in type.Views.Take(4))
                    items.Add((new PieItem("view:" + v.Id, v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id) { IconId = IconPainter.ForView(v) }, true));
            items.Add((new PieItem("dup", T("Duplicate", "Duplizieren")) { IconId = IconIds.Duplicate }, true));
            items.Add((new PieItem("rot90", T("Rotate 90°", "Drehen 90°")) { IconId = IconIds.Rotate }, true));
            items.AddRange(actions.Select(a => (ActionItem(a), false)));
            items.Add((new PieItem("flipH", T("Mirror ↔", "Spiegeln ↔")) { IconId = IconIds.MirrorH }, false));
            items.Add((new PieItem("flipV", T("Mirror ↕", "Spiegeln ↕")) { IconId = IconIds.MirrorV }, false));
            return items;
        }

        /// <summary>
        /// Splits the entries into the main ring (at most <see cref="MaxPieItems"/>: essential entries first, then the
        /// others in order) and the "More" overflow, so no entry appears twice.
        /// </summary>
        internal static (List<PieItem> Main, List<PieItem> Overflow) PieLayout(IReadOnlyList<(PieItem Item, bool Essential)> all, string moreLabel)
        {
            if (all.Count <= MaxPieItems) return (all.Select(e => e.Item).ToList(), new List<PieItem>());
            var essential = all.Where(e => e.Essential).Select(e => e.Item).Take(MaxPieItems - 1).ToList();
            var rest = all.Where(e => !e.Essential).Select(e => e.Item)
                .Concat(all.Where(e => e.Essential).Select(e => e.Item).Skip(MaxPieItems - 1)).ToList();
            var room = MaxPieItems - 1 - essential.Count;
            var main = essential.Concat(rest.Take(room)).ToList();
            main.Add(new PieItem("menu:more", moreLabel) { IconId = IconIds.Cycle });
            return (main, rest.Skip(room).ToList());
        }

        private void ShowPie(POINT pt, SlideView view, List<PieItem> items)
        {
            _pie = new PieMenu(new PointF(pt.X, pt.Y), items, (float)view.DeviceScale);
            _mode = Mode.Pie;
            DrawPie(view);
        }

        private void DrawPie(SlideView view)
        {
            var pie = _pie;
            if (pie is null) return;
            Present(view, _ => { }, g => pie.Draw(g, _dark));
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
            SketchUnits.RedrawDependents(slide, registry, _lang(), Array.Empty<SketchDocument>(), new[] { doc });
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
            var shape = SketchUnits.Draw(slide, registry, doc, _lang());
            SketchUnits.RedrawDependents(slide, registry, _lang(), Array.Empty<SketchDocument>(), new[] { doc });
            shape?.Select(Office.MsoTriState.msoTrue);
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
            var snap = NoSnap() ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
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
            ReplaceWithDependents(slide, unit, doc);
        }

        /// <summary>
        /// Re-renders the edited unit and then the units depending on it (wall joints, openings), selects the edited unit.
        /// Runs inside the caller's undo entry.
        /// </summary>
        private PowerPoint.Shape? ReplaceWithDependents(PowerPoint.Slide slide, SketchUnit unit, SketchDocument doc, SketchDocument? before = null)
        {
            var registry = _registry();
            var shape = SketchUnits.Replace(slide, unit, doc, registry, _lang());
            SketchUnits.RedrawDependents(slide, registry, _lang(), new[] { before ?? unit.Doc }, new[] { doc });
            shape?.Select(Office.MsoTriState.msoTrue);
            return shape;
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

        // ------------------------------------------------------------------ rotate with snapping

        /// <summary>PowerPoint's rotation handle: about 35 logical px above the top centre of the selection frame.</summary>
        private bool OnRotationHandle(POINT pt, SlideView view)
        {
            if (_unit is null) return false;
            var r = UnitRectMm();
            var top = view.ToScreen(new Vec2(r.X + r.W / 2, r.Y));
            var cy = top.Y - 35 * view.DeviceScale;
            var dx = pt.X - top.X;
            var dy = pt.Y - cy;
            return Math.Sqrt(dx * dx + dy * dy) <= 13 * view.DeviceScale;
        }

        /// <summary>
        /// Rotation about the unit centre. Snapping (unless Alt) pulls the angle to multiples of 15° within 4°;
        /// Shift rotates in strict 15° steps.
        /// </summary>
        private void UpdateRotate(POINT pt, SlideView view)
        {
            var v = view.ToMm(pt) - _rotCentre;
            var a = (Math.Atan2(v.Y, v.X) - _rotStart) * 180 / Math.PI;
            while (a <= -180) a += 360;
            while (a > 180) a -= 360;
            var step = Math.Round(a / 15) * 15;
            var snapped = KeyDown(VK_SHIFT) || (!NoSnap() && Math.Abs(a - step) <= 4);
            if (snapped) a = step;
            _rotAngle = a;
            var registry = _registry();
            var unit = _unit!;
            var doc = Edits.Rotate(unit.Doc, unit.Doc.Objects.Keys.ToList(), _rotCentre, a);
            var ctx = new RenderContext(doc, _lang());
            var prims = doc.ObjectsOn(doc.Pages[0]).Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
            var label = $"{a:0.#}\u00b0";
            var at = view.ToScreen(view.ToMm(pt));
            var centre = _rotCentre;
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                using var pen = new Pen(snapped ? GuideColor : Ghost, (float)view.Mm(1.2)) { DashPattern = new[] { 4f, 3f } };
                var tip = view.ToMm(pt);
                g.DrawLine(pen, (float)centre.X, (float)centre.Y, (float)tip.X, (float)tip.Y);
            }, g =>
            {
                using var font = new Font("Segoe UI", (float)(11 * view.DeviceScale), FontStyle.Bold, GraphicsUnit.Pixel);
                using var bg = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                using var fg = new SolidBrush(snapped ? GuideColor : Ghost);
                var size = g.MeasureString(label, font);
                var box = new RectangleF(at.X + 14 * (float)view.DeviceScale, at.Y + 14 * (float)view.DeviceScale, size.Width + 8, size.Height + 4);
                g.FillRectangle(bg, box);
                g.DrawString(label, font, fg, box.X + 4, box.Y + 2);
            });
        }

        private void ApplyRotate(SketchUnit stale, Vec2 centre, double degrees)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            _app.StartNewUndoEntry();
            var doc = Edits.Rotate(unit.Doc, unit.Doc.Objects.Keys.ToList(), centre, degrees);
            ReplaceWithDependents(slide, unit, doc);
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
            var snap = !NoSnap();
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
            ReplaceWithDependents(slide, unit, doc);
        }

        private void ApplyMove(SketchUnit unit, Vec2 deltaMm)
        {
            _app.StartNewUndoEntry();
            unit.Shape.Left += (float)(deltaMm.X * Geo.PtPerMm);
            unit.Shape.Top += (float)(deltaMm.Y * Geo.PtPerMm);
            unit.Shape.Select(Office.MsoTriState.msoTrue);
            var fresh = SketchUnits.Read(unit.Shape);
            // Neighbour-dependent objects (walls) re-render at the new place (joints open or close) together with the
            // units that depend on them; everything else just keeps the natively moved shapes.
            if (fresh is not null && CurrentSlide() is PowerPoint.Slide slide && SketchUnits.NeedsContext(_registry(), fresh.Doc))
            {
                var shape = ReplaceWithDependents(slide, fresh, fresh.Doc, before: unit.Doc);
                fresh = shape is null ? null : SketchUnits.Read(shape);
            }
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
                SketchUnits.RedrawDependents(slide, registry, _lang(), new[] { doc }, Array.Empty<SketchDocument>());
                Disarm();
                return;
            }
            if (id == "dup")
            {
                var copy = SketchUnits.WithNewIds(doc, new Vec2(5, 5));
                var drawn = SketchUnits.Draw(slide, registry, copy, _lang());
                SketchUnits.RedrawDependents(slide, registry, _lang(), Array.Empty<SketchDocument>(), new[] { copy });
                drawn?.Select(Office.MsoTriState.msoTrue);
                return;
            }
            if (id.StartsWith("view:", StringComparison.Ordinal))
            {
                // View switch with size adaptation to the view's aspect (ObjectType.WithView).
                var view = id.Substring(5);
                var ctx = new RenderContext(doc, _lang());
                doc = Edits.Update(doc, ids, o => registry.Type(o.Type)?.WithView(o, view, ctx) ?? o);
            }
            else if (id.StartsWith("act:", StringComparison.Ordinal))
            {
                var ran = RunTypeAction(slide, unit, id.Substring(4));
                if (ran is null) return;
                doc = ran;
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
            else if (id == "rotm90") doc = Edits.Rotate(doc, ids, centre, -90);
            ReplaceWithDependents(slide, unit, doc);
        }

        /// <summary>
        /// Runs a type context action on the unit's objects with the shared engine runner: input actions ask for their value
        /// first (cancel = no change), document-aware actions see the neighbouring units. Only the unit's own objects take the
        /// result. Returns null if nothing is to be applied.
        /// </summary>
        private SketchDocument? RunTypeAction(PowerPoint.Slide slide, SketchUnit unit, string actionId)
        {
            var registry = _registry();
            var lang = _lang();
            var own = unit.Doc;
            var full = SketchUnits.NeedsContext(registry, own) ? SketchUnits.WithContext(own, SketchUnits.All(slide, unit.Id)) : own;
            var first = own.ObjectsOn(own.Pages[0]).FirstOrDefault(o => registry.Type(o.Type) is not null);
            var action = first is null ? null : registry.Type(first.Type)!.Actions(first, new RenderContext(full, lang)).FirstOrDefault(a => a.Id == actionId);
            if (action is null || !action.IsEnabled) return null;
            var owner = _view?.MainWindow ?? IntPtr.Zero;
            string? input = null;
            if (action.Input is not null)
            {
                input = TextPrompt.AskInput(action.Input.Label.Get(lang), action.Input.Unit, action.Input.Value, owner);
                if (input is null) return null;
            }
            try
            {
                var ran = ContextActions.Run(registry, full, own.Objects.Keys.ToList(), actionId, input, lang);
                return Edits.Update(own, own.Objects.Keys.ToList(), o => ran.Get(o.Id) ?? o);
            }
            catch (FormatException ex)
            {
                TextPrompt.Warn(ex.Message, owner);
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);
    }
}
