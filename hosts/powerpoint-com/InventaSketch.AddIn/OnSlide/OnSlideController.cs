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
    internal sealed partial class OnSlideController : IDisposable
    {
        private enum Mode { Idle, Armed, Place, Drag, Pie, Resize, EditPoint, Rotate, Band, Connect, Branch }

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
        private bool _centredResize;
        // Anchoring: owners of the snap targets (objects of other units) and the last point snap.
        private readonly Dictionary<string, SketchObject> _targetOwners = new(StringComparer.Ordinal);
        private SnapResult? _lastPointSnap;
        private Attachment? _twoStartAttach;
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
        // Box selection of points (Alt + drag beside a selected line): selected indices, band corners, original points.
        private readonly HashSet<int> _selectedPoints = new();
        private POINT _bandStart;
        private IReadOnlyList<Vec2>? _pointOrigin;
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
            // Gears stay in mesh and shafts carry their gears while "Anchor" is on; Alt edits freely.
            SketchUnits.CouplingEnabled = () => AddInSettings.Anchor && !KeyDown(VK_MENU);
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

        /// <summary>"Plan scale …" from the bar or pie menu of a plan element (the add-in asks and re-renders the slide).</summary>
        public event Action? PlanScaleRequested;

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
                if (_mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Band or Mode.Branch) return;
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
            if (_unit?.Id != unit.Id) { _selectedPoints.Clear(); _barInsertOpen = false; _barChoice = null; }
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
            UpdateChainSource(unit);
            _targets = CollectTargets(slide, unit.Id);
            _unitContext = SketchUnits.NeedsContext(registry, unit.Doc) ? SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id)) : unit.Doc;
            PrepareFollowers(unit);
            PrepareLinks(unit);
            PrepareSegments(unit);
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
            _selectedPoints.Clear();
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
            EndCornerInsert();
            var wasArmedUnit = _unit;
            _item = null;
            _pie = null;
            _guides = Array.Empty<SnapGuide>();
            _overlay.Clear();
            if (wasArmedUnit is not null && _mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Connect or Mode.Branch)
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
                if (_mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Connect or Mode.Branch) return;
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
                        var before = _unit.Doc;
                        var fresh = SketchUnits.Read(s);
                        if (fresh is not null)
                        {
                            // Anchored line ends and neighbour-dependent units (walls) follow native moves too.
                            if (CurrentSlide() is PowerPoint.Slide slide)
                            {
                                var registry = _registry();
                                if (SketchUnits.NeedsContext(registry, fresh.Doc))
                                {
                                    var shape = SketchUnits.Replace(slide, fresh, fresh.Doc, registry, _lang());
                                    if (shape is not null) fresh = SketchUnits.Read(shape) ?? fresh;
                                    SketchUnits.RedrawDependents(slide, registry, _lang(), new[] { before }, new[] { fresh.Doc });
                                }
                                else SketchUnits.RedrawDependents(slide, registry, _lang(), new[] { before }, new[] { fresh.Doc });
                                // Coupling may have redrawn this unit (a gear scaled with its partner).
                                fresh = Live(fresh) ?? fresh;
                                fresh.Shape.Select(Office.MsoTriState.msoTrue);
                            }
                            Arm(fresh);
                        }
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
                        if (_twoStart is null) { _twoStart = _placeAt; _down = pt; _twoStartAttach = AnchorFor(_placeAt, _lastPointSnap); }
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
                        var startAttach = _twoStartAttach;
                        var endAttach = AnchorFor(end, _lastPointSnap);
                        Cancel("placed");
                        if (item is not null) Post(() => InsertPoints(item, start.Value, end, startAttach, endAttach));
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

                case Mode.Connect:
                    return ConnectMouse(message, pt, view);

                case Mode.Branch:
                    return BranchMouse(message, pt, view);

                case Mode.Armed:
                    if (HandleBar(message, pt, view, out var barSwallow)) return barSwallow;
                    // Lock badges where the unit touches and follows other elements: a click releases or couples.
                    if (LinkMouse(message, pt, view, out var linkSwallow)) return linkSwallow;
                    // Quick add: blue triangles and connection ports of flowchart, UML, ER, BPMN and basic elements.
                    if (ChainMouse(message, pt, view, out var chainSwallow)) return chainSwallow;
                    if (_unit is null || !view.Contains(pt)) return false;
                    if (message == WM_LBUTTONDOWN && PointHandleAt(pt, view) is int pointIndex and >= 0)
                    {
                        HideBar();
                        _mode = Mode.EditPoint;
                        _pointIndex = pointIndex;
                        _pointOrigin = _unit.Doc.ResolvePoints(PointObject()!);
                        if (!_selectedPoints.Contains(pointIndex)) _selectedPoints.Clear();
                        _pointAt = PointObject()!.Points![pointIndex];
                        _moved = false;
                        _down = pt;
                        return true;
                    }
                    // "+" in the middle of a segment: a new corner (click or drag), or a branch drawn out of the line.
                    if (SegmentMouse(message, pt, view)) return true;
                    // Double-click on a corner of a wire, pipe, road or polyline removes it.
                    if (message == WM_LBUTTONDBLCLK && PointObject() is { } cornerLine && Polylines.CanInsert(_registry(), cornerLine)
                        && PointHandleAt(pt, view) is int corner and > 0 && corner < cornerLine.Points!.Count - 1 && _unit is { } cornerUnit)
                    {
                        HideBar();
                        Post(() => ApplyRemoveCorner(cornerUnit, corner));
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
                        _centredResize = IsCentredUnit();
                        Log.Info("Resize start, handle " + handle + (_centredResize ? " (about the centre)" : ""));
                        return true;
                    }
                    // Alt + drag beside a selected line, wire or wall draws a box that selects its points (moved together).
                    if (message == WM_LBUTTONDOWN && KeyDown(VK_MENU) && PointObject() is not null && !Inside(pt, view, inset: -4))
                    {
                        HideBar();
                        _mode = Mode.Band;
                        _bandStart = pt;
                        _down = pt;
                        return true;
                    }
                    // Double-click on the selected unit edits its text (like PowerPoint text boxes).
                    if (message == WM_LBUTTONDBLCLK && Inside(pt, view, inset: 0) && _unit is { } dblUnit
                        && dblUnit.Objects.FirstOrDefault(o => _registry().Type(o.Type) is not null) is { } dblFirst
                        && EditableText(_registry(), dblFirst) is not null)
                    {
                        HideBar();
                        Post(() => ApplyAction(dblUnit, "text"));
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

                case Mode.Band:
                    if (message == WM_MOUSEMOVE) { DrawBand(view, pt); return true; }
                    if (message == WM_LBUTTONUP)
                    {
                        _mode = Mode.Armed;
                        _selectedPoints.Clear();
                        foreach (var i in PointsInBand(view, pt)) _selectedPoints.Add(i);
                        Log.Info($"Box selection: {_selectedPoints.Count} point(s)");
                        _overlay.Clear();
                        ShowBar(view);
                        DrawPointHandles(view);
                        return true;
                    }
                    return true;

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
                        // Corners inside a wire, pipe, road or polyline snap but do not dock (only its ends do).
                        var pointAttach = InnerCorner(index) ? null : AnchorFor(at, _lastPointSnap);
                        var group = MovedGroup(index, at);
                        var insertAt = _insertAt;
                        var cornerSeg = EndCornerInsert();
                        if (cornerSeg >= 0 && unit is not null) Post(() => ApplyCorner(unit, cornerSeg, insertAt, moved ? at : insertAt, moved ? pointAttach : null));
                        else if (moved && unit is not null) Post(() => ApplyPoint(unit, index, at, pointAttach, group));
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
                        // Coupled parts (gears on a dragged shaft) move along and snap too, unless Alt moves freely.
                        var coupled = !KeyDown(VK_MENU) && AddInSettings.Anchor && _followerIds.Count > 0;
                        var snap = NoSnap()
                            ? SnapResult.None(raw)
                            : Snapper.SnapMove(coupled ? _dragAnchors : _unitAnchors, raw, coupled ? _dragTargets : _targets, Options(view));
                        _dragCoupled = coupled;
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
            if (vk == VK_ESCAPE && _mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Band)
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
                _lastPointSnap = snapped;
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
                var line = registry.CreateBetween(item, _twoStart.Value, _placeAt);
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
            var moved = (_dragCoupled ? _dragPrims : _unitPrims).Select(p => Prim.Transform(p, m)).ToList();
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
            // Just added with "+": the next element is one click away (no submenu).
            if (first is not null) list.AddRange(NextButtons(first));
            if (type is not null && first is not null && type.Views.Count > 1)
            {
                foreach (var v in type.Views.Take(6))
                    list.Add(new BarButton("view:" + v.Id, v.Label.Get(_lang()), v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id) { IconId = IconPainter.ForView(v) });
                list.Add(new BarButton("", "", "", Separator: true));
            }
            // Choices with pictograms (line type, arrowheads, line style, line course): icon button plus caret.
            if (first is not null)
            {
                var chips = ChoiceButtons(first).ToList();
                list.AddRange(chips);
                if (chips.Count > 0) list.Add(new BarButton("", "", "", Separator: true));
            }
            var hasText = first is not null && EditableText(registry, first) is not null;
            if (hasText)
                list.Add(new BarButton("text", "", T("Edit text (double-click)", "Text bearbeiten (Doppelklick)")) { IconId = IconIds.Text });
            // Key parameters straight in the bar (no submenu): actions with a telling pictogram as icons, switches and
            // cycles (hub, helix, kind …) as short text buttons; the pie menu keeps the complete list.
            // Actions that only set a parameter already shown as a choice button stay in the pie menu.
            var choiceKeys = first is null ? new HashSet<string>() : new HashSet<string>(BarChoices(first).Select(c => c.Key));
            var keyActions = BarActions(UnitActions(out _).Where(a => a.ParamKeys is null || !a.ParamKeys.Any(choiceKeys.Contains)), hasText);
            foreach (var a in keyActions)
            {
                var label = a.Label.Get(_lang());
                var tip = label + (a.Input is not null && !label.EndsWith("…", StringComparison.Ordinal) ? " …" : "");
                list.Add(IsTelling(a.Icon)
                    ? new BarButton("act:" + a.Id, "", tip, Active: a.IsActive) { IconId = a.Icon }
                    : new BarButton("act:" + a.Id, ShortLabel(label), tip, Active: a.IsActive));
            }
            // Plan elements (walls, rooms, openings): the plan scale of the slide, one click away.
            if (type?.UsesPlanScale == true && _unit is not null)
            {
                var scale = _unit.Doc.Settings.PlanScale ?? 100;
                list.Add(new BarButton("planscale", "1:" + scale.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture), T("Plan scale of the slide …", "Planmaßstab der Folie …")));
            }
            // "Insert": elements that fit the selection as thumbnails right in the bar (shaft: gears, bearings …).
            if (first is not null)
            {
                // Parts that snap into place (gear on the shaft, gear in mesh, bearing beside a gear) are in the bar right
                // away; the insert button opens the rest.
                var scale = _view?.DeviceScale ?? 1;
                var suggestions = registry.Suggestions(first, 8);
                var direct = DirectInserts(first, suggestions);
                foreach (var item in direct)
                    list.Add(new BarButton("ins:" + item.Id, "", T("Add: ", "Anfügen: ") + item.Name.Get(_lang())) { Icon = Thumb(item, (int)Math.Round(26 * scale)) });
                list.Add(new BarButton("insert", "", T("Insert a fitting element", "Passendes Element einfügen"), Active: _barInsertOpen) { IconId = IconIds.Insert });
                if (_barInsertOpen)
                    foreach (var item in suggestions.Where(i => !direct.Contains(i)))
                        list.Add(new BarButton("ins:" + item.Id, "", item.Name.Get(_lang())) { Icon = Thumb(item, (int)Math.Round(26 * scale)) });
            }
            list.Add(new BarButton("", "", "", Separator: true));
            list.Add(new BarButton("flipH", "", T("Mirror horizontally", "Horizontal spiegeln")) { IconId = IconIds.MirrorH });
            list.Add(new BarButton("flipV", "", T("Mirror vertically", "Vertikal spiegeln")) { IconId = IconIds.MirrorV });
            list.Add(new BarButton("rot90", "", T("Rotate 90 degrees", "Um 90 Grad drehen")) { IconId = IconIds.Rotate });
            list.Add(new BarButton("dup", "", T("Duplicate", "Duplizieren")) { IconId = IconIds.Duplicate });
            list.Add(new BarButton("", "", "", Separator: true));
            list.Add(new BarButton("delete", "", T("Delete", "L\u00f6schen"), Danger: true) { IconId = IconIds.Delete });
            return list;
        }

        /// <summary>At most this many suggestions with a fitting place (<see cref="Fitting"/>) right in the bar.</summary>
        internal const int MaxDirectInserts = 3;

        /// <summary>Suggestions that <see cref="Fitting.Place"/> puts in place relative to <paramref name="first"/> (first ones only).</summary>
        private List<LibraryItem> DirectInserts(SketchObject first, IReadOnlyList<LibraryItem> suggestions)
        {
            var list = new List<LibraryItem>();
            if (_unit is null || first.IsPointGeometry) return list;
            try
            {
                var registry = _registry();
                var full = SketchUnits.WithContext(_unit.Doc, _otherUnits);
                foreach (var item in suggestions)
                {
                    if (list.Count >= MaxDirectInserts) break;
                    if (Fitting.Place(registry, full, first.Id, item, _lang()) is not null) list.Add(item);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Direct inserts", ex);
            }
            return list;
        }

        /// <summary>At most this many type actions in the floating bar; the pie menu has all of them.</summary>
        internal const int MaxBarActions = 5;

        /// <summary>Whether the "Insert" palette of the bar is open (closes with the next selection).</summary>
        private bool _barInsertOpen;

        /// <summary>Pictograms the bar already has as its own buttons.</summary>
        private static readonly HashSet<string> NotInBar = new() { IconIds.Delete, IconIds.MirrorH, IconIds.MirrorV, IconIds.Rotate, IconIds.Duplicate };

        /// <summary>Generic pictograms (switch, cycle) say nothing on their own: such actions show a short text instead.</summary>
        private static bool IsTelling(string icon) => IconIds.All.Contains(icon) && icon != IconIds.Toggle && icon != IconIds.Cycle;

        /// <summary>
        /// Short bar text of an action label: without a leading "Next", "on/off"-type suffixes and explanations in brackets,
        /// at most 22 characters (so "Next start cardinality" and "Next end cardinality" stay apart).
        /// </summary>
        internal static string ShortLabel(string label, int max = 22)
        {
            var s = label;
            foreach (var prefix in new[] { "Next ", "Nächste ", "Nächster ", "Nächstes ", "Nächste ", "Nächster ", "Nächstes " })
                if (s.StartsWith(prefix, StringComparison.Ordinal) && s.Length > prefix.Length + 2)
                {
                    s = s.Substring(prefix.Length);
                    s = char.ToUpper(s[0], System.Globalization.CultureInfo.CurrentCulture) + s.Substring(1);
                    break;
                }
            var cut = s.IndexOf(" (", StringComparison.Ordinal);
            if (cut > 0) s = s.Substring(0, cut);
            foreach (var suffix in new[] { " on/off", " an/aus", " ein/aus" })
                if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - suffix.Length);
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";
        }

        /// <summary>
        /// Type actions for the floating bar (key parameters: thicker/thinner, more/fewer, swap, hub, helix …): available ones,
        /// telling pictograms first (each icon once), then switches and cycles as text; text actions only when the bar has
        /// no text button yet; at most <see cref="MaxBarActions"/>.
        /// </summary>
        internal static List<ContextAction> BarActions(IEnumerable<ContextAction> actions, bool hasTextButton)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var usable = actions.Where(a => a.IsEnabled && !NotInBar.Contains(a.Icon) && !(hasTextButton && a.Icon == IconIds.Text)).ToList();
            var telling = usable.Where(a => IsTelling(a.Icon) && seen.Add(a.Icon));
            var text = usable.Where(a => !IsTelling(a.Icon));
            return telling.Concat(text).Take(MaxBarActions).ToList();
        }

        private RectangleF UnitScreenRect(SlideView view)
        {
            var b = _unit!.BoundsPt;
            var a = view.ToScreen(new Vec2(b.X / Geo.PtPerMm, b.Y / Geo.PtPerMm));
            var c = view.ToScreen(new Vec2(b.Right / Geo.PtPerMm, b.Bottom / Geo.PtPerMm));
            return RectangleF.FromLTRB(a.X, a.Y, c.X, c.Y);
        }

        /// <summary>Screen rectangles of the other units of the slide (the bar avoids covering them).</summary>
        private List<RectangleF> OtherUnitScreenRects(SlideView view)
        {
            var list = new List<RectangleF>();
            foreach (var u in _otherUnits)
            {
                var b = u.BoundsPt;
                var a = view.ToScreen(new Vec2(b.X / Geo.PtPerMm, b.Y / Geo.PtPerMm));
                var c = view.ToScreen(new Vec2(b.Right / Geo.PtPerMm, b.Bottom / Geo.PtPerMm));
                // Lines have no height: give them a little so a bar lying on them counts.
                var pad = (float)(4 * view.DeviceScale);
                list.Add(RectangleF.FromLTRB(Math.Min(a.X, c.X) - pad, Math.Min(a.Y, c.Y) - pad, Math.Max(a.X, c.X) + pad, Math.Max(a.Y, c.Y) + pad));
            }
            return list;
        }

        /// <summary>Shows the bar next to the selection (a dismissed bar comes back only with the next selection).</summary>
        private void ShowBar(SlideView view)
        {
            if (_unit is null) return;
            if (!AddInSettings.Bar) { HideBar(); return; }
            if (_bar is null || _fade.Dismissed) _fade.Reset();
            _bar = new FloatingBar(BarButtons(), (float)view.DeviceScale);
            if (_unit.Objects.FirstOrDefault(o => _registry().Type(o.Type) is not null) is { } chosen) AttachDropdown(_bar, chosen);
            var frame = UnitScreenRect(view);
            // Room for the top quick-add triangle beyond the rotation handle.
            if (_chainSource is not null) { var up = (float)(14 * view.DeviceScale); frame = RectangleF.FromLTRB(frame.Left, frame.Top - up, frame.Right, frame.Bottom); }
            _bar.Place(frame, view.PaneRect, OtherUnitScreenRects(view));
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
                // A click elsewhere closes an open dropdown (the click itself goes on as usual).
                if (message == WM_LBUTTONDOWN && _barChoice is not null) { _barChoice = null; ShowBar(view); }
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
                    var id = bar.IdAt(idx);
                    Log.Info("Bar: " + id);
                    Post(() => ApplyAction(unit, id));
                }
                return true;
            }
            return true; // right button and other messages over the bar never reach the slide
        }

        private List<PieItem> SubMenu(string menu, SketchObject o)
        {
            var registry = _registry();
            var lang = _lang();
            var items = new List<PieItem> { new("menu:back", T("\u2190 Back", "\u2190 Zur\u00fcck")) };
            if (menu == "type")
            {
                foreach (var id in Registry.ConversionTargets)
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

        /// <summary>
        /// The main text of an object for quick editing (double-click, bar, pie): the object text of text types, else the
        /// first text parameter among numeral, text, label, name, title and ref (electrical designator). Null if the type has no such text.
        /// </summary>
        internal (string Label, string Current, Func<SketchObject, string, SketchObject> Apply)? EditableText(Registry registry, SketchObject o)
        {
            var type = registry.Type(o.Type);
            if (type is null) return null;
            // Same rule as the engine (EngineApi.MainTextOf): a reference numeral first (its object text would not show),
            // then the object text of text types, then the first text parameter.
            if (type.Parameters.FirstOrDefault(p => p.Key == "numeral" && p.Kind == ParamKind.Text) is { } numeralDef)
                return (numeralDef.Label.Get(_lang()), o.Ref ?? o.Str("numeral", numeralDef.Default?.Text ?? ""), (x, t) => x.WithParam("numeral", t) with { Text = null, Ref = t });
            if (type.HasText) return (T("Text", "Text"), o.Text ?? "", (x, t) => x with { Text = t });
            foreach (var key in new[] { "text", "label", "name", "title", "ref" })
            {
                var pd = type.Parameters.FirstOrDefault(p => p.Key == key && p.Kind == ParamKind.Text);
                if (pd is null) continue;
                if (key == "numeral")
                    return (pd.Label.Get(_lang()), o.Ref ?? o.Str(key, pd.Default?.Text ?? ""), (x, t) => x.WithParam(key, t) with { Text = null, Ref = t });
                return (pd.Label.Get(_lang()), o.Str(key, pd.Default?.Text ?? ""), (x, t) => x.WithParam(key, t));
            }
            return null;
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
            if (submenu == "insert")
            {
                var sub = new List<PieItem> { new("menu:back", T("\u2190 Back", "\u2190 Zur\u00fcck")) };
                var anchor = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
                if (anchor is not null)
                    sub.AddRange(registry.Suggestions(anchor, MaxPieItems + 1).Select(i => new PieItem("ins:" + i.Id, i.Name.Get(_lang()))));
                ShowPie(pt, view, sub);
                return;
            }
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
            if (pointObject is not null && Registry.IsConvertible(pointObject))
            {
                items.Add((new PieItem("menu:type", T("Type ▸", "Typ ▸")) { IconId = IconIds.Cycle }, true));
                items.Add((new PieItem("menu:label", T("Label ▸", "Beschriftung ▸")) { IconId = IconIds.Text }, true));
            }
            var actions = UnitActions(out var first);
            var type = first is null ? null : registry.Type(first.Type);
            if (type is not null && first is not null)
                foreach (var v in type.Views.Take(4))
                    items.Add((new PieItem("view:" + v.Id, v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id) { IconId = IconPainter.ForView(v) }, true));
            if (first is not null && EditableText(registry, first) is not null)
                items.Add((new PieItem("text", T("Text \u2026", "Text \u2026")) { IconId = IconIds.Text }, true));
            // Insert elements that fit the selection (shaft: gears, bearings; wire: symbols; always annotations).
            if (first is not null)
                items.Add((new PieItem("menu:insert", T("Insert \u25B8", "Einf\u00fcgen \u25B8")) { IconId = IconIds.Insert }, true));
            items.Add((new PieItem("dup", T("Duplicate", "Duplizieren")) { IconId = IconIds.Duplicate }, true));
            items.Add((new PieItem("rot90", T("Rotate 90°", "Drehen 90°")) { IconId = IconIds.Rotate }, true));
            if (type?.UsesPlanScale == true)
                items.Add((new PieItem("planscale", T("Plan scale …", "Planmaßstab …")) { IconId = IconIds.Plan }, false));
            // Mechanisms: the input motion as a slide sequence with Morph transitions.
            if (type is not null && first is not null && type.AnimationFrames(first, 2).Count > 0)
                items.Add((new PieItem("anim", T("Animate as slides", "Als Folien animieren")) { IconId = IconIds.Animate }, false));
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
            _targetOwners.Clear();
            _otherUnits = SketchUnits.All(slide, exceptId);
            foreach (var unit in _otherUnits)
            {
                var ctx = new RenderContext(unit.Doc, _lang());
                foreach (var o in unit.Objects)
                {
                    if (registry.Type(o.Type) is null) continue;
                    _targetOwners[o.Id] = o;
                    list.AddRange(registry.PageAnchors(o, ctx).Select(a => a with { Owner = o.Id }));
                }
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

        private void InsertPoints(LibraryItem item, Vec2 start, Vec2 end, Attachment? startAttach = null, Attachment? endAttach = null)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var setup = _app.ActivePresentation.PageSetup;
            var doc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
            var line = registry.CreateBetween(item, start, end);
            if ((startAttach ?? endAttach) is not null && line.Points is { Count: > 1 } pts)
            {
                var att = new Attachment?[pts.Count];
                att[0] = startAttach;
                att[pts.Count - 1] = endAttach;
                line = line with { Attach = att };
            }
            doc = Edits.Add(doc, doc.Pages[0].Id, line);
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

        /// <summary>True if point <paramref name="index"/> of the armed line is a corner between its ends of a line with free corners.</summary>
        private bool InnerCorner(int index)
            => PointObject() is { } o && index > 0 && index < o.Points!.Count - 1 && _registry().Type(o.Type)?.InsertablePoints == true;

        /// <summary>Small diamond handles on the points of a selected line/arrow/dimension/lead line.</summary>
        private void DrawPointHandles(SlideView view)
        {
            var o = PointObject();
            if (o is null && _chainSource is not null) { DrawChainTools(view); return; }
            if (o is null && _links.Count == 0) return;
            var pts = o is null ? null : _unit!.Doc.ResolvePoints(o);
            Present(view, _ => { }, g =>
            {
                if (pts is not null) DrawHandleSet(g, view, pts, _selectedPoints);
                if (pts is not null) DrawSegmentHandles(g, view);
                DrawLinks(g, view);
            });
        }

        /// <summary>Diamond handles; selected points (box selection) are filled with the accent colour.</summary>
        private void DrawHandleSet(Graphics g, SlideView view, IReadOnlyList<Vec2> pts, ICollection<int> selected)
        {
            var r = (float)(5 * view.DeviceScale);
            using var fill = new SolidBrush(Color.White);
            using var chosen = new SolidBrush(Color.FromArgb(31, 95, 191));
            using var pen = new Pen(Ghost, (float)(1.6 * view.DeviceScale));
            for (var i = 0; i < pts.Count; i++)
            {
                var c = view.ToScreen(pts[i]);
                var diamond = new[] { new PointF(c.X, c.Y - r), new PointF(c.X + r, c.Y), new PointF(c.X, c.Y + r), new PointF(c.X - r, c.Y) };
                g.FillPolygon(selected.Contains(i) ? chosen : fill, diamond);
                g.DrawPolygon(pen, diamond);
            }
        }

        private void UpdatePointEdit(POINT pt, SlideView view)
        {
            var o = PointObject();
            if (o is null) return;
            var raw = view.ToMm(pt);
            var snap = NoSnap() ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
            _pointAt = raw + snap.Delta;
            _lastPointSnap = snap;
            var doc = Edits.SetPoint(_unit!.Doc, o.Id, _pointIndex, _pointAt);
            foreach (var kv in MovedGroup(_pointIndex, _pointAt)) doc = Edits.SetPoint(doc, o.Id, kv.Key, kv.Value);
            var prims = _registry().Render(doc.Get(o.Id)!, new RenderContext(doc, _lang()));
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, snap.Guides);
            });
        }

        /// <summary>
        /// Attachment of a snapped point to the element of another unit that owns the snap target (with "Anchor" on): the
        /// point then follows that element (moved, rotated, resized). Pins keep their anchor id. Null if not anchoring.
        /// </summary>
        private Attachment? AnchorFor(Vec2 at, SnapResult? snap)
        {
            if (!AddInSettings.Anchor || snap?.Target is not { Owner: { } ownerId } target) return null;
            // Box targets: normalised position (and pin id); lines: their rail and the position along it (a branch).
            return _targetOwners.TryGetValue(ownerId, out var owner) ? Attachments.For(owner, at, target) : null;
        }

        /// <summary>The other selected points (box selection) moved by the same offset as the dragged one.</summary>
        private Dictionary<int, Vec2> MovedGroup(int dragged, Vec2 at)
        {
            var result = new Dictionary<int, Vec2>();
            if (_pointOrigin is null || !_selectedPoints.Contains(dragged) || _selectedPoints.Count < 2 || dragged >= _pointOrigin.Count) return result;
            var delta = at - _pointOrigin[dragged];
            foreach (var i in _selectedPoints)
                if (i != dragged && i < _pointOrigin.Count) result[i] = _pointOrigin[i] + delta;
            return result;
        }

        private IEnumerable<int> PointsInBand(SlideView view, POINT end)
        {
            var o = PointObject();
            if (o is null) yield break;
            var rect = RectangleF.FromLTRB(Math.Min(_bandStart.X, end.X), Math.Min(_bandStart.Y, end.Y), Math.Max(_bandStart.X, end.X), Math.Max(_bandStart.Y, end.Y));
            var pts = _unit!.Doc.ResolvePoints(o);
            for (var i = 0; i < pts.Count; i++)
            {
                var sp = view.ToScreen(pts[i]);
                if (rect.Contains(sp.X, sp.Y)) yield return i;
            }
        }

        private void DrawBand(SlideView view, POINT end)
        {
            var rect = RectangleF.FromLTRB(Math.Min(_bandStart.X, end.X), Math.Min(_bandStart.Y, end.Y), Math.Max(_bandStart.X, end.X), Math.Max(_bandStart.Y, end.Y));
            var inside = new HashSet<int>(PointsInBand(view, end));
            var pts = PointObject() is { } o ? _unit!.Doc.ResolvePoints(o) : Array.Empty<Vec2>();
            Present(view, _ => { }, g =>
            {
                using var fill = new SolidBrush(Color.FromArgb(30, 31, 95, 191));
                using var pen = new Pen(Color.FromArgb(31, 95, 191), (float)(1.2 * view.DeviceScale)) { DashPattern = new[] { 4f, 3f } };
                g.FillRectangle(fill, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                DrawHandleSet(g, view, pts, inside);
            });
        }

        private void ApplyPoint(SketchUnit stale, int index, Vec2 at, Attachment? attach = null, IReadOnlyDictionary<int, Vec2>? group = null)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var o = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (o is null) return;
            _app.StartNewUndoEntry();
            var doc = Edits.SetPoint(unit.Doc, o.Id, index, at, attach);
            if (group is not null) foreach (var kv in group) doc = Edits.SetPoint(doc, o.Id, kv.Key, kv.Value);
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
            if (SketchUnits.RedrawDependents(slide, registry, _lang(), new[] { before ?? unit.Doc }, new[] { doc }) > 0)
                shape = SketchUnits.All(slide).FirstOrDefault(u => u.Id == unit.Id)?.Shape ?? shape; // coupling may have redrawn it
            shape?.Select(Office.MsoTriState.msoTrue);
            return shape;
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

        /// <summary>True if the armed unit is a centred part (an object with a pitch circle or hub anchor).</summary>
        private bool IsCentredUnit()
        {
            if (_unit is null) return false;
            var registry = _registry();
            var ctx = new RenderContext(_unit.Doc, _lang());
            return _unit.Objects.Any(o => registry.Type(o.Type) is not null && !o.IsPointGeometry
                && registry.PageAnchors(o, ctx).Any(a => a.Role is AnchorRole.Pitch or AnchorRole.PitchInner or AnchorRole.Hub));
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
            // Centred parts (gears, bearings, wheels: pitch circle or hub) scale about their centre/axis by default; Ctrl
            // switches to the opposite handle. Other units scale from the opposite handle; Ctrl scales about the centre.
            var aboutCentre = KeyDown(VK_CONTROL) != _centredResize;
            _pivot = aboutCentre ? centre : HandlePoint(r, (h + 4) % 8);
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
            var label = (uniform ? $"×{sx:0.00}" : $"×{sx:0.00} / {sy:0.00}")
                + (aboutCentre ? T("  · centre (Ctrl: edge)", "  · Mitte (Strg: Kante)") : T("  · edge (Ctrl: centre)", "  · Kante (Strg: Mitte)"));
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
                    // Already in mesh: with coupling the partner follows the resize, so no snapping against it.
                    if (SketchUnits.CouplingEnabled() && Math.Abs(F(1)) <= 0.3) continue;
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
            else if (fresh is not null && CurrentSlide() is PowerPoint.Slide s2
                     && SketchUnits.RedrawDependents(s2, _registry(), _lang(), new[] { unit.Doc }, new[] { fresh.Doc }) > 0)
            {
                // Line ends anchored to the moved unit followed it ("Anchor") and coupled gears moved or scaled (which
                // may have redrawn this unit too); keep the moved unit selected.
                fresh = Live(fresh);
                fresh?.Shape.Select(Office.MsoTriState.msoTrue);
            }
            if (fresh is not null) Arm(fresh);
        }

        /// <summary>Frames of an animation as a slide sequence (one slide each, the first is the current slide).</summary>
        internal const int AnimationFrameCount = 12;

        /// <summary>PpEntryEffect.ppEffectMorphByObject (PowerPoint 2016+, read from the installed type library).</summary>
        private const int MorphByObject = 3954;

        /// <summary>
        /// The unit models of an animation: per frame the unit with every animatable object replaced by its frame
        /// (<see cref="ObjectType.AnimationFrames"/>); empty if nothing in the unit moves.
        /// </summary>
        internal static List<SketchDocument> AnimationDocs(Registry registry, SketchDocument doc, int count)
        {
            var frames = new Dictionary<string, IReadOnlyList<SketchObject>>(StringComparer.Ordinal);
            foreach (var o in doc.Objects.Values)
                if (registry.Type(o.Type)?.AnimationFrames(o, count) is { Count: > 1 } f) frames[o.Id] = f;
            if (frames.Count == 0) return new List<SketchDocument>();
            var n = frames.Values.Min(f => f.Count);
            var docs = new List<SketchDocument>();
            for (var k = 0; k < n; k++)
            {
                var frame = k;
                docs.Add(k == 0 ? doc : Edits.Update(doc, frames.Keys.ToList(), o => frames[o.Id][frame]));
            }
            return docs;
        }

        /// <summary>
        /// Duplicates the slide once per further frame and redraws the unit there in that frame (same name, same stacking
        /// position), with Morph transitions that advance on their own: the slide show plays the motion. One undo step.
        /// </summary>
        private void AnimateAsSlides(PowerPoint.Slide slide, SketchUnit unit, Registry registry)
        {
            var docs = AnimationDocs(registry, unit.Doc, AnimationFrameCount);
            if (docs.Count < 2) return;
            var previous = slide;
            for (var k = 1; k < docs.Count; k++)
            {
                var copy = previous.Duplicate()[1];
                PowerPoint.Shape? old = null;
                foreach (PowerPoint.Shape s in copy.Shapes)
                {
                    try { if (s.Tags["ISK_ID"] == unit.Id) { old = s; break; } } catch { }
                }
                if (old is null) break;
                var z = old.ZOrderPosition;
                old.Delete();
                var drawn = SketchUnits.Draw(copy, registry, docs[k], _lang());
                for (var guard = 0; drawn is not null && drawn.ZOrderPosition > z && guard < 500; guard++)
                    drawn.ZOrder(Office.MsoZOrderCmd.msoSendBackward);
                var t = copy.SlideShowTransition;
                t.EntryEffect = (PowerPoint.PpEntryEffect)MorphByObject;
                t.Duration = 0.4f;
                t.AdvanceOnClick = Office.MsoTriState.msoTrue;
                t.AdvanceOnTime = Office.MsoTriState.msoTrue;
                t.AdvanceTime = 0;
                previous = copy;
            }
            Log.Info($"Animated {unit.Shape.Name} as {docs.Count} slides");
            try { _app.ActiveWindow.View.GotoSlide(slide.SlideIndex); unit.Shape.Select(Office.MsoTriState.msoTrue); } catch { }
        }

        /// <summary>The unit as it is on the slide now (re-read by id when coupling or dependents replaced its shape).</summary>
        private SketchUnit? Live(SketchUnit unit)
        {
            try
            {
                _ = unit.Shape.Name;
                return SketchUnits.Read(unit.Shape) ?? unit;
            }
            catch
            {
                return CurrentSlide() is PowerPoint.Slide slide ? SketchUnits.All(slide).FirstOrDefault(u => u.Id == unit.Id) : null;
            }
        }

        /// <summary>"x,y,w,h" in device pixels of a shape on the current slide (automation); "" if there is no slide view.</summary>
        public string ScreenRectOf(string shapeName) => OnUi(() =>
        {
            using (DpiScope.PerMonitor())
            {
                var view = SlideView.Current(_app);
                var slide = CurrentSlide();
                if (view is null || slide is null) return "";
                var s = slide.Shapes[shapeName];
                var a = view.ToScreen(new Vec2(s.Left / Geo.PtPerMm, s.Top / Geo.PtPerMm));
                var b = view.ToScreen(new Vec2((s.Left + s.Width) / Geo.PtPerMm, (s.Top + s.Height) / Geo.PtPerMm));
                return string.Join(",", new[] { a.X, a.Y, b.X - a.X, b.Y - a.Y }.Select(v => Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        });

        /// <summary>Runs a bar/pie command on the armed unit (automation); false if no unit is armed.</summary>
        public bool RunCommand(string id)
        {
            var unit = _unit;
            if (unit is null) return false;
            Log.Info("Command: " + id);
            Post(() => ApplyAction(unit, id));
            return true;
        }

        private void ApplyAction(SketchUnit stale, string id)
        {
            // Opening or closing a bar choice is no edit; picking an option becomes the edit command.
            if (ChoiceCommand(id) is not { } command) return;
            id = command;
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
            if (id.StartsWith("ins:", StringComparison.Ordinal))
            {
                // Placed where it fits (gear on the shaft, gear in mesh, bearing beside the gear …), else at the pointer
                // with snapping (onto the axis, ports and mounts of the selection).
                if (registry.Item(id.Substring(4)) is { } item && !InsertFitting(slide, unit, item)) BeginPlacement(item);
                return;
            }
            if (id == "planscale")
            {
                PlanScaleRequested?.Invoke();
                return;
            }
            if (id == "insert")
            {
                // Opens or closes the insert palette in the bar (no undo step, no redraw of the unit).
                _barInsertOpen = !_barInsertOpen;
                if (_view is not null) ShowBar(_view);
                return;
            }
            if (id.StartsWith("next:", StringComparison.Ordinal))
            {
                ReplaceNext(unit, id.Substring(5));
                return;
            }
            if (id == "anim")
            {
                AnimateAsSlides(slide, unit, registry);
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
                doc = Edits.Update(doc, ids, o => registry.WithView(o, view, ctx));
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
                doc = Edits.Update(doc, ids, o => o.IsPointGeometry ? registry.Convert(o, target) : o);
            }
            else if (id.StartsWith("param:", StringComparison.Ordinal))
            {
                var parts = id.Split(new[] { ':' }, 3);
                ParamValue value = parts[2] == "true" ? true : parts[2] == "false" ? false : (ParamValue)parts[2];
                doc = Edits.Update(doc, ids, o => registry.Type(o.Type)?.Parameters.Any(pd => pd.Key == parts[1]) == true ? registry.SetParam(o, parts[1], value) : o);
            }
            else if (id.StartsWith("dash:", StringComparison.Ordinal))
            {
                if (!Enum.TryParse<InventaSketch.Core.DashStyle>(id.Substring(5), out var dash)) return;
                doc = Edits.Update(doc, ids, o => o with { Style = (o.Style ?? new Style()) with { Dash = dash } });
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
            else if (id == "text")
            {
                var first = doc.ObjectsOn(doc.Pages[0]).FirstOrDefault(o => registry.Type(o.Type) is not null);
                if (first is null || EditableText(registry, first) is not { } edit) return;
                var entered = TextPrompt.Ask(edit.Label, edit.Current, _view?.MainWindow ?? IntPtr.Zero);
                if (entered is null || entered == edit.Current) return;
                doc = Edits.Update(doc, new[] { first.Id }, o => edit.Apply(o, entered));
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
