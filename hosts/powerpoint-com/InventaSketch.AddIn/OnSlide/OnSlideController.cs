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
        /// <summary>Button ids of the floating bar as last shown (comma-separated; for UI tests through the automation object).</summary>
        internal string LastBarIds { get; private set; } = "";
        /// <summary>Screen rectangle "x,y,w,h" (device pixels) of the floating bar as last shown (UI tests).</summary>
        internal string LastBarRect { get; private set; } = "";

        private enum Mode { Idle, Armed, Place, Drag, Pie, Resize, EditPoint, Rotate, Band, Connect, Branch, Draw, Port, Turn, Multi, Sketch, Grip }

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

        /// <summary>Keyboard-only hook for shortcuts that work without a selection (Ctrl+Space).</summary>
        private readonly InputHooks _keys = new();

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
            // While a hook runs nothing calls the object model or renders the halo (_inHook).
            _hooks.Mouse = (m, p, h) => { _inHook = true; try { return OnMouse(m, p, h); } finally { _inHook = false; } };
            _hooks.KeyDown = (vk, shift, ctrl, alt) => { _inHook = true; try { return OnKey(vk, shift, ctrl, alt); } finally { _inHook = false; } };
            _hooks.Reset = () => Cancel("reset");
            // Ctrl+Space also without a selection: a keyboard-only hook that stays installed (the full hooks take over
            // while an element is selected and handle the key themselves).
            _keys.KeyDown = (vk, shift, ctrl, alt) =>
            {
                if (vk != 0x20 || !ctrl || shift || alt || _mode != Mode.Idle) return false;
                Post(() => { if (CurrentSlide() is { } s) { _view ??= SlideView.Current(_app); OpenInsertSearch(s, null); } });
                return true;
            };
            _keys.InstallKeyboard();
            _timer = new Timer { Interval = 250 };
            _timer.Tick += (_, _) => RefreshView();
            _app.WindowSelectionChange += OnSelectionChange;
            // Gears stay in mesh and shafts carry their gears while "Anchor" is on; Alt edits freely.
            SketchUnits.CouplingEnabled = () => AddInSettings.Anchor && !KeyDown(VK_MENU) && !_noCouple;
        }

        public void Dispose()
        {
            try { _app.WindowSelectionChange -= OnSelectionChange; } catch { /* PowerPoint closing */ }
            _timer.Dispose();
            _hooks.Dispose();
            _keys.Dispose();
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
            PreparePlacementContext();
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
                if (_mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Band or Mode.Branch or Mode.Draw or Mode.Port or Mode.Turn or Mode.Sketch or Mode.Grip) return;
                using var dpi = DpiScope.PerMonitor();
                SketchUnit? unit = null;
                if (sel.Type == PowerPoint.PpSelectionType.ppSelectionShapes && sel.ShapeRange.Count == 1)
                {
                    var shape = sel.ShapeRange[1];
                    if (SketchUnits.IsUnit(shape)) unit = SketchUnits.Read(shape);
                }
                // Several elements: the multi-selection bar (selection-wide operations).
                if (sel.Type == PowerPoint.PpSelectionType.ppSelectionShapes && sel.ShapeRange.Count > 1)
                {
                    var units = new List<SketchUnit>();
                    for (var i = 1; i <= sel.ShapeRange.Count; i++)
                    {
                        var shape = sel.ShapeRange[i];
                        if (SketchUnits.IsUnit(shape) && SketchUnits.Read(shape) is { } u) units.Add(u);
                    }
                    if (units.Count >= 2) { ArmMulti(units); return; }
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

        /// <summary>Arms the current selection again (after a setting changed how the overlay looks, e.g. the control size).</summary>
        internal void Rearm()
        {
            try
            {
                Disarm();
                OnSelectionChange(_app.ActiveWindow.Selection);
            }
            catch (Exception ex)
            {
                Log.Error("Rearm", ex);
            }
        }

        private void Arm(SketchUnit unit)
        {
            var newlyArmed = _unit?.Id != unit.Id;
            if (newlyArmed) { _selectedPoints.Clear(); _barChoice = null; _part = null; }
            var slide = CurrentSlide();
            var view = SlideView.Current(_app);
            if (slide is null || view is null) { Disarm(); return; }
            _view = view;
            if (_unit?.Id != unit.Id) _haloPreview = null;
            _unit = unit;
            _dark = OfficeTheme.IsDark();
            ReadSlideDark();
            var registry = _registry();
            // Designations found on other slides: continuation markers glow with the line, "Same name" reports them.
            _elsewhere = Continuations.For(slide, unit.Doc, registry, _lang());
            var ctx = new RenderContext(unit.Doc, _lang()) { Continuations = _elsewhere, PageNames = PageNames.Slides };
            _unitPrims = unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
            _unitAnchors = unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.PageAnchors(o, ctx)).ToList();
            UpdateChainSource(unit);
            _targets = CollectTargets(slide, unit.Id);
            _unitContext = SketchUnits.NeedsContext(registry, unit.Doc) || SketchUnits.HasDependents(registry, unit.Doc) ? SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id)) : unit.Doc;
            PrepareFollowers(unit);
            ResetCouplingPreview();
            PrepareLinks(unit);
            PrepareSegments(unit);
            PreparePorts(unit);
            PrepareSides(unit);
            PrepareGrips(unit);
            GetCursorPos(out var cursor);
            _pointer = cursor;
            _shiftHeld = KeyDown(VK_SHIFT);
            _revealKey = "";
            _mode = Mode.Armed;
            // A click right on a chart's curve, area, axis … selects that part at once (maintainer 2026-11-25: "the chart
            // turns blue first and only then the curve, even when I click the curve").
            if (newlyArmed && KeyDownNow(0x01) && PartOwner() is not null && view.Contains(cursor))
            {
                SelectPartAt(cursor, view);
            }
            _hooks.Install();
            _timer.Start();
            _overlay.Clear();
            ShowBar(view);
            StartFirstHint(view);
            DrawPointHandles(view);
            if (LastHandles.Length > 0) Log.Info("Shown: " + LastShownHandles);
            if (LastHandles.Length > 0) Log.Info("Handles: " + LastHandles + (HandleLayout.AnyOverlap(Handles(view), (float)view.DeviceScale) ? " (overlap!)" : ""));
            Log.Info($"Armed {unit.Shape.Name}: {_unitAnchors.Count} anchors, {_targets.Count} targets, pane {view.PaneRect}, {view.PixelsPerPoint:0.###} px/pt, hooks {_hooks.Installed}");
            // Ctrl+double-click: PowerPoint's first Ctrl+click takes the element out of the selection, its second click puts it
            // back – the same element re-selected with Ctrl within the double-click time is the gesture.
            if (_ctrlClick is { } cc && cc.UnitId == unit.Id && KeyDown(VK_CONTROL)
                && unchecked(Environment.TickCount - cc.Tick) <= GetDoubleClickTime() + 200)
            {
                _ctrlClick = null;
                Log.Info("Ctrl+double-click: select connected");
                var again = unit;
                Post(() => { if (CurrentSlide() is { } cs && SketchUnits.Read(again.Shape) is { } fresh) SelectConnected(cs, fresh, ConnectKind.Auto); });
            }
        }

        /// <summary>The last Ctrl+click on the armed element (for Ctrl+double-click, see <see cref="Arm"/>).</summary>
        private (string UnitId, int Tick)? _ctrlClick;

        private void Disarm()
        {
            if (_mode is Mode.Place) return;
            _mode = Mode.Idle;
            _selectedPoints.Clear();
            _part = null;
            _unit = null;
            _multi = new List<SketchUnit>();
            _unitContext = null;
            _pie = null;
            ResetHalo();
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
            _haloPreview = null;
            _guides = Array.Empty<SnapGuide>();
            _overlay.Clear();
            if (wasArmedUnit is not null && _mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Connect or Mode.Branch or Mode.Draw or Mode.Port or Mode.Turn or Mode.Sketch or Mode.Grip)
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
                if (_mode is Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Connect or Mode.Branch or Mode.Draw or Mode.Port or Mode.Turn or Mode.Grip) return;
                using var dpi = DpiScope.PerMonitor();
                var view = SlideView.Current(_app);
                if (view is null) { Cancel("no slide view"); return; }
                if (!view.SameAs(_view)) _view = view;
                // A multi-selection PowerPoint moved: its halo follows once the button is released.
                if (_mode == Mode.Multi) RefreshMultiHalo(view);
                // A selection grown to several elements without a selection event (selected through the object model):
                // the multi-selection bar takes over.
                if (_mode == Mode.Armed)
                {
                    var sel = _app.ActiveWindow?.Selection;
                    if (sel is not null && sel.Type == PowerPoint.PpSelectionType.ppSelectionShapes && sel.ShapeRange.Count > 1)
                    {
                        OnSelectionChange(sel);
                        return;
                    }
                }
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

        /// <summary>
        /// True while a window other than PowerPoint's main window is in front – a modal dialog of the add-in (text prompt,
        /// insert box, about box, message box) or another program. The hooks then leave every message alone, so OK and
        /// Cancel of a dialog lying over the selected element work (maintainer report 2026-11-25: the text prompt of a
        /// flowchart step could not be closed).
        /// </summary>
        private static bool ForeignForeground(SlideView view)
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || view.MainWindow == IntPtr.Zero) return false;
            return GetAncestor(fg, GA_ROOT) != GetAncestor(view.MainWindow, GA_ROOT);
        }

        private bool OnMouse(int message, POINT pt, IntPtr hwnd)
        {
            var view = _view;
            if (view is null) return false;
            if (ForeignForeground(view)) return false;
            if (_mode == Mode.Place && !_loggedPlaceMessage) { _loggedPlaceMessage = true; Log.Info("Place: first mouse message 0x" + message.ToString("X4") + " at " + pt.X + "," + pt.Y); }
            // A press-and-drag whose release never reached the hooks (it went to another window): the first move with the
            // left button up ends it like a release, so nothing keeps hanging on the pointer (maintainer 2026-11-25).
            if (message == WM_MOUSEMOVE && _mode is Mode.Drag or Mode.Grip or Mode.Port or Mode.Resize or Mode.Rotate or Mode.EditPoint or Mode.Band or Mode.Turn
                && !KeyDownNow(0x01))
            {
                Log.Info("Release missed in " + _mode + ": ending the drag");
                return OnMouse(WM_LBUTTONUP, pt, hwnd);
            }
            switch (_mode)
            {
                case Mode.Place when IsTwoPoint(_item):
                    if (message == WM_MOUSEMOVE) { UpdatePlacement(pt); return false; }
                    if (message == WM_LBUTTONDOWN)
                    {
                        if (!view.Contains(pt)) { Cancel("click outside"); return false; }
                        UpdatePlacement(pt);
                        if (_twoStart is null) { _twoStart = _placeAt; _down = pt; _twoStartAttach = AnchorFor(_placeAt, _lastPointSnap, _item?.TypeId); }
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
                        var endAttach = AnchorFor(end, _lastPointSnap, item?.TypeId);
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
                        var fitted = _placeFitted;
                        Cancel("placed");
                        if (item is not null && fitted is not null) Post(() => InsertFitted(fitted, item));
                        else if (item is not null) Post(() => InsertAt(item, at));
                        return true;
                    }
                    if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
                    return false;

                case Mode.Connect:
                    return ConnectMouse(message, pt, view);

                case Mode.Branch:
                    return BranchMouse(message, pt, view);

                case Mode.Draw:
                    return DrawMouse(message, pt, view);

                case Mode.Port:
                    return PortDragMouse(message, pt, view);

                case Mode.Grip:
                    return GripMouse(message, pt, view);

                case Mode.Turn:
                    return TurnMouse(message, pt, view);

                case Mode.Multi:
                    return MultiMouse(message, pt, view);

                case Mode.Sketch:
                    return SketchMouse(message, pt, view);

                case Mode.Armed:
                    // Progressive disclosure: secondary handles fade in near the pointer.
                    if (message == WM_MOUSEMOVE) UpdateReveal(pt, view, fromMouse: true);
                    if (HandleBar(message, pt, view, out var barSwallow)) return barSwallow;
                    // Lock badges where the unit touches and follows other elements: a click releases or couples.
                    if (LinkMouse(message, pt, view, out var linkSwallow)) return linkSwallow;
                    // Drag handles of the element (chart limit lines, value lines, markers): before the body drag inside the plot.
                    if (GripStart(message, pt, view, out var gripSwallow)) return gripSwallow;
                    // Build "+" on gears, shafts and machines: what fits there, or stretch a shaft.
                    if (PortMouse(message, pt, view, out var portSwallow)) return portSwallow;
                    // "+" on the sides of a table: a row or column there.
                    if (SideMouse(message, pt, view, out var sideSwallow)) return sideSwallow;
                    // Quick add: blue triangles and connection ports of flowchart, UML, ER, BPMN and basic elements.
                    if (ChainMouse(message, pt, view, out var chainSwallow)) return chainSwallow;
                    if (_unit is null || !view.Contains(pt)) return false;
                    // Ctrl+double-click: two Ctrl+presses on the element within the double-click time (the hooks see two presses,
                    // not a double-click message); the first is remembered and passed on.
                    if (message == WM_LBUTTONDOWN && KeyDownNow(VK_CONTROL) && Inside(pt, view, inset: -4) && _unit is { } ctrlUnit)
                    {
                        if (_ctrlClick is { } prev && prev.UnitId == ctrlUnit.Id && unchecked(Environment.TickCount - prev.Tick) <= GetDoubleClickTime())
                        {
                            _ctrlClick = null;
                            HideBar();
                            Log.Info("Ctrl+double-click: select connected");
                            Post(() => { if (CurrentSlide() is { } cs && SketchUnits.Read(ctrlUnit.Shape) is { } fresh) SelectConnected(cs, fresh, ConnectKind.Auto); });
                            return true;
                        }
                        _ctrlClick = (ctrlUnit.Id, Environment.TickCount);
                    }
                    // Ctrl+double-click on the element: select what is connected with it (the most meaningful kind).
                    if (message == WM_LBUTTONDBLCLK && KeyDown(VK_CONTROL) && Inside(pt, view, inset: -4) && _unit is { } connectUnit)
                    {
                        HideBar();
                        Log.Info("Ctrl+double-click: select connected");
                        Post(() => { if (CurrentSlide() is { } cs && SketchUnits.Read(connectUnit.Shape) is { } fresh) SelectConnected(cs, fresh, ConnectKind.Auto); });
                        return true;
                    }
                    // Motion mode: gears, carriers and linkages move the way they can instead of being edited (also on a joint).
                    if (message == WM_LBUTTONDOWN && AddInSettings.Motion && (Inside(pt, view, inset: -6) || PointHandleAt(pt, view) >= 0) && BeginTurn(pt, view)) return true;
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
                        && PointHandleAt(pt, view) is int corner and >= 0 && (Polylines.IsClosed(_registry(), cornerLine) || (corner > 0 && corner < cornerLine.Points!.Count - 1)) && _unit is { } cornerUnit)
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
                        var pointAttach = InnerCorner(index) ? null : AnchorFor(at, _lastPointSnap, PointObject()?.Type);
                        var group = MovedGroup(index, at);
                        var insertAt = _insertAt;
                        var cornerSeg = EndCornerInsert();
                        var join = moved && cornerSeg < 0 ? JoinTarget(index, at) : null;
                        if (cornerSeg >= 0 && unit is not null) Post(() => ApplyCorner(unit, cornerSeg, insertAt, moved ? at : insertAt, moved ? pointAttach : null));
                        else if (join is { } j && unit is not null) Post(() => ApplyJoin(unit, index, at, j.Unit, j.LineId, j.AtStart));
                        else if (moved && unit is not null) Post(() => ApplyPoint(unit, index, at, pointAttach, group));
                        else if (unit is not null && SelectCorner(index) && _view is not null) { ShowBar(_view); DrawPointHandles(_view); }
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
                        _snapTarget = snap.Target;
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
                        // A click without dragging on a chart selects the part under the pointer (curve, axis, grid …).
                        else if (!moved && unit is not null && SelectPartAt(pt, view)) { ShowBar(view); DrawPointHandles(view); }
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
                            if (h != pie.Hover)
                            {
                                pie.Hover = h;
                                // Hovering "select connected": the elements it would add glow faintly.
                                SetPreview(PreviewFor(h >= 0 ? pie.Items[h].Id : null));
                                DrawPie(view);
                            }
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
                            // A greyed place (nothing of that kind for this element) keeps the menu open.
                            if (idx >= 0 && !pie.Items[idx].Enabled) return true;
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
            if (_view is { } kv && ForeignForeground(kv)) return false;
            if (_mode == Mode.Sketch && vk == VK_ESCAPE) { Cancel("sketch: Esc"); return true; }
            // Shift shows every handle of the selection (progressive disclosure); released, the next pointer move hides them.
            if (vk == VK_SHIFT && _mode == Mode.Armed && _view is { } sv) { _shiftHeld = true; UpdateReveal(_pointer, sv); return false; }
            // Typing (text, shortcuts) hides the bar until the next selection, as in Slidemax.
            if (_mode == Mode.Armed && vk is not (VK_SHIFT or VK_CONTROL or VK_MENU) && _bar is not null)
            {
                _fade.Dismiss();
                HideBar();
            }
            // Esc or Enter while drawing a line on: the corners drawn so far are kept.
            if (vk is VK_ESCAPE or 0x0D && _mode == Mode.Draw)
            {
                FinishDraw();
                return true;
            }
            if (vk == VK_ESCAPE && _mode is Mode.Place or Mode.Drag or Mode.Pie or Mode.Resize or Mode.EditPoint or Mode.Rotate or Mode.Band or Mode.Grip)
            {
                Cancel("escape");
                return true;
            }
            // Ctrl+Space: insert by typing at the pointer (with a selection: fitting parts first).
            if (vk == 0x20 && ctrl && !shift && !alt && _mode is Mode.Idle or Mode.Armed)
            {
                var armed = _mode == Mode.Armed ? _unit : null;
                Post(() => { if (CurrentSlide() is { } s) OpenInsertSearch(s, armed); });
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
                // With a chart part selected, Del deletes that part (a point, a curve, an area …; else hides it), never the element.
                if (_part is not null && IsDeleteKey(vk) && !ctrl && !shift)
                {
                    Post(DeletePart);
                    return true;
                }
                // With a chart part selected: Tab / Shift+Tab steps through the parts, Esc returns to the whole element.
                if (_part is not null && (vk == VK_TAB && !ctrl || vk == VK_ESCAPE && !ctrl && !shift))
                {
                    var back = shift;
                    var escape = vk == VK_ESCAPE;
                    Post(() => { if (escape) SelectPart(null); else StepPart(back); });
                    return true;
                }
                // Tab / Shift+Tab: the next part of the assembly (coupled parts only; else PowerPoint's own Tab).
                if (vk == VK_TAB && !ctrl)
                {
                    var parts = AssemblyOf();
                    if (parts.Count > 1)
                    {
                        var next = parts[shift ? parts.Count - 1 : 1];
                        Post(() => next.Shape.Select(Office.MsoTriState.msoTrue));
                        return true;
                    }
                }
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
                // Shift: the second point in 15 degree steps from the first (no other snapping).
                var angled = _twoStart is not null && KeyDown(VK_SHIFT);
                var snapped = NoSnap() || angled ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
                _placeAt = angled ? AngleStep(_twoStart!.Value, raw) : raw + snapped.Delta;
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
            // A part that fits a shaft near the pointer: the ghost shows the fitted result (on the shaft, in its view and
            // size), not the item's default look; a click inserts exactly that. Alt places freely.
            _placeFitted = NoSnap() ? null : FitNear(item, raw, view.Mm(FitReachPx));
            if (_placeFitted is { } fitted && _placeContext is { } placeDoc)
            {
                var fdoc = Edits.Add(placeDoc, placeDoc.Pages[0].Id, fitted);
                var fprims = registry.Render(fdoc.Get(fitted.Id)!, new RenderContext(fdoc, _lang()));
                var tagAt = new PointF(pt.X, pt.Y);
                var fitLabel = T("Fits here – click to insert", "Passt hier – Klick fügt ein");
                Present(view, g => ThumbnailRenderer.DrawAll(g, fprims, Ghost, (float)view.Mm(1.6)), g => DrawTag(g, view, tagAt, fitLabel));
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
            // What the neighbours will do on release (meshing partners, belts, shafts that follow).
            var partners = _unit is null || Alt() ? Array.Empty<Primitive>() : CouplingPreview(Edits.Move(_unit.Doc, _unit.Doc.Objects.Keys.ToList(), delta));
            // What the part docks onto, named at the target ("pitch circle", "axis", "pin 2").
            var target = _snapTarget;
            var label = SnapLabel.For(target, _lang());
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, partners, Partner, (float)view.Mm(1.6));
                ThumbnailRenderer.DrawAll(g, moved, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, _guides);
            }, label is null || target is null ? null : g => DrawTag(g, view, view.ToScreen(target.P), label));
        }

        /// <summary>Snap target of the current drag (for its label), null without an object snap.</summary>
        private Anchor? _snapTarget;

        /// <summary>A small tag next to a screen point (snap labels).</summary>
        private static void DrawTag(Graphics g, SlideView view, PointF at, string text)
        {
            var k = (float)view.DeviceScale;
            using var font = new Font("Segoe UI", 10 * k, FontStyle.Regular, GraphicsUnit.Pixel);
            using var bg = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
            using var fg = new SolidBrush(GuideColor);
            var size = g.MeasureString(text, font);
            var box = new RectangleF(at.X + 8 * k, at.Y - size.Height - 8 * k, size.Width + 6 * k, size.Height + 2 * k);
            g.FillRectangle(bg, box);
            g.DrawString(text, font, fg, box.X + 3 * k, box.Y + 1 * k);
        }

        private static bool Alt() => KeyDown(VK_MENU);

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
                // The selection halo lies under everything else (handles, ghosts, the pie).
                DrawHalo(g, view);
                var state = g.Save();
                g.TranslateTransform((float)(view.OffsetX - view.PaneRect.X), (float)(view.OffsetY - view.PaneRect.Y));
                g.ScaleTransform((float)view.PixelsPerMm, (float)view.PixelsPerMm);
                draw(g);
                g.Restore(state);
                if (screenSpace is not null)
                {
                    g.TranslateTransform(-view.PaneRect.X, -view.PaneRect.Y);
                    screenSpace(g);
                    g.ResetTransform();
                }
                // Conflicting element values: a small amber badge at the selected element (overlay only, Calc 2).
                if (_mode == Mode.Armed)
                {
                    g.TranslateTransform(-view.PaneRect.X, -view.PaneRect.Y);
                    DrawValueBadge(g, view);
                }
            }
            _overlay.Present(bmp, view.PaneRect, view.MainWindow);
        }

        // ------------------------------------------------------------------ floating bar

        /// <summary>The insert box for the ribbon button: with the armed element's fitting parts, else the whole library.</summary>
        public void OpenInsertBox()
        {
            var armed = _mode == Mode.Armed ? _unit : null;
            Post(() =>
            {
                if (CurrentSlide() is not { } s) return;
                _view ??= SlideView.Current(_app);
                OpenInsertSearch(s, armed);
            });
        }

        /// <summary>
        /// The insert box (<see cref="InsertSearch"/>) at the pointer: without typing it lists the parts that fit the selection
        /// (placed in their spot, e.g. a gear on the shaft) and then the recent items; typing searches the whole library,
        /// fitting and recent items first, the selection's view preferred. The chosen item is placed where it fits, else it
        /// follows the pointer until a click places it.
        /// </summary>
        private void OpenInsertSearch(PowerPoint.Slide slide, SketchUnit? unit)
        {
            var registry = _registry();
            var lang = _lang();
            var first = unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            var fitting = new List<LibraryItem>();
            string? view = null;
            if (first is not null)
            {
                var suggestions = registry.Suggestions(first, 12);
                var direct = DirectInserts(first, suggestions);
                fitting.AddRange(direct);
                fitting.AddRange(suggestions.Where(i => !direct.Contains(i)));
                view = registry.Type(first.Type)?.ViewOf(first);
            }
            GetCursorPos(out var p);
            // The side to attach to: where the pie was opened (right-click), else the pointer (Ctrl+Space).
            var from = _pieOpenedAt is { } po && (DateTime.UtcNow - po.When).TotalSeconds < 30 ? po.At : p;
            _insertToward = _view?.ToMm(from);
            var chosen = InsertSearch.Ask(registry, lang, AddInSettings.RecentItems, fitting, view, new Point(p.X, p.Y), _view?.MainWindow ?? IntPtr.Zero, (i, size) => Thumb(i, size));
            if (chosen is null) return;
            AddInSettings.RecentItems = LibrarySearch.Remember(AddInSettings.RecentItems, chosen.Id);
            Log.Info("Insert by typing: " + chosen.Id);
            if (unit is not null && InsertFitting(slide, unit, chosen)) return;
            // Inserted at once beside the selection (on the side the pie was opened), nothing follows the pointer.
            InsertNow(chosen, _insertToward, unit);
        }

        /// <summary>
        /// The units of the slide coupled with the armed unit, directly or through others (meshes, shafts, carriers, belts,
        /// linkages: "moves along", <see cref="ConnectKind.Coupled"/>), the armed unit first; just the armed unit if it is
        /// coupled with nothing.
        /// </summary>
        private List<SketchUnit> AssemblyOf() => ConnectedOf(ConnectKind.Coupled);

        /// <summary>Which quick transforms visibly change the selected unit (<see cref="Symmetry"/>); all on errors.</summary>
        private TransformEffect UnitEffect()
        {
            if (_unit is null) return TransformEffect.All;
            try
            {
                var registry = _registry();
                var ctx = new RenderContext(SketchUnits.WithContext(_unit.Doc, _otherUnits), _lang());
                var prims = _unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
                return Symmetry.Of(prims);
            }
            catch (Exception ex)
            {
                Log.Error("Transform effect", ex);
                return TransformEffect.All;
            }
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
                    if (FitToUnit(_unit, full, item) is not null) list.Add(item);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Direct inserts", ex);
            }
            return list;
        }

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
            LastBarRect = $"{_bar.Bounds.X},{_bar.Bounds.Y},{_bar.Bounds.Width},{_bar.Bounds.Height}";
            Log.Info($"Bar shown at {LastBarRect} opacity {opacity:0.##}");
            LastBarIds = string.Join(",", _bar.Buttons.Where(b => !b.Separator).Select(b => b.Id));
            Log.Info("Bar ids: " + LastBarIds);
            LastCommandIds = CommandIds(_bar.Buttons);
            Log.Info("Commands: " + LastCommandIds);
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
                    var hoverChanged = hover != bar.Hover;
                    bar.Hover = hover;
                    DrawBar(view, opacity);
                    // Hovering "select the assembly": what moves along glows faintly.
                    if (hoverChanged && _mode == Mode.Armed && SetPreview(PreviewFor(hover >= 0 ? bar.IdAt(hover) : null))) DrawPointHandles(view);
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
                if (pressed && idx >= 0 && _mode == Mode.Multi)
                {
                    var multiId = bar.IdAt(idx);
                    Log.Info("Bar: " + multiId);
                    HideBar();
                    Post(() => ApplyMulti(multiId));
                    return true;
                }
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
            // Tables: the cells, on one line ("|" between columns, "\n" between rows), so double-click and "Text" edit them.
            if (type.Parameters.FirstOrDefault(p => p.Key == "cells" && p.Kind == ParamKind.Text) is { } cellsDef)
            {
                var raw = o.Str("cells", "");
                if (string.IsNullOrWhiteSpace(raw) && o.Type == "ann.table") raw = InventaSketch.Modules.Annotation.TableType.DefaultCells(_lang());
                return (cellsDef.Label.Get(_lang()), raw.Replace("\r", "").Replace("\n", "\\n"), (x, t) => x.WithParam("cells", t));
            }
            return null;
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

        /// <summary>
        /// Inserts a library item at once and selects it (maintainer 2026-11-26: nothing hangs on the pointer after a pick
        /// in the gallery, the sidebar or the insert box). Placed beside <paramref name="beside"/> (the selected unit, on the
        /// side of <paramref name="toward"/>), else at <paramref name="toward"/>, else in the middle of the visible slide; a
        /// spot already taken by an earlier insert moves on diagonally. Returns false if no slide is visible.
        /// </summary>
        public bool InsertNow(LibraryItem item, Vec2? toward = null, SketchUnit? beside = null)
        {
            if (_overlay.InvokeRequired) return (bool)_overlay.Invoke(new Func<bool>(() => InsertNow(item, toward, beside)));
            var slide = CurrentSlide();
            if (slide is null) return false;
            Cancel("insert now");
            var setup = _app.ActivePresentation.PageSetup;
            double w = setup.SlideWidth / Geo.PtPerMm, h = setup.SlideHeight / Geo.PtPerMm;
            var at = InsertSpot(slide, toward, beside, w, h);
            InsertAt(item, at);
            return true;
        }

        /// <summary>Where <see cref="InsertNow"/> puts a new element (mm on the slide).</summary>
        private Vec2 InsertSpot(PowerPoint.Slide slide, Vec2? toward, SketchUnit? beside, double w, double h)
        {
            Vec2 at;
            if (beside is not null)
            {
                var b = beside.BoundsPt;
                double l = b.Left / Geo.PtPerMm, t = b.Top / Geo.PtPerMm, r = b.Right / Geo.PtPerMm, bo = b.Bottom / Geo.PtPerMm;
                var c = new Vec2((l + r) / 2, (t + bo) / 2);
                var d = (toward ?? new Vec2(r + 1, c.Y)) - c;
                const double gap = 15;
                // The side the pie was opened on (right, left, below, above), clamped to the slide.
                at = Math.Abs(d.X) * (bo - t) >= Math.Abs(d.Y) * (r - l)
                    ? new Vec2(d.X >= 0 ? r + gap : l - gap, c.Y)
                    : new Vec2(c.X, d.Y >= 0 ? bo + gap : t - gap);
            }
            else if (toward is { } p) at = p;
            else
            {
                // The middle of the visible part of the slide (zoomed in), else of the slide.
                var view = _view ?? SlideView.Current(_app);
                at = new Vec2(w / 2, h / 2);
                if (view is not null && view.PaneRect.Width > 0 && view.PaneRect.Height > 0)
                {
                    var pr = view.PaneRect;
                    var a = view.ToMm(new POINT { X = pr.Left, Y = pr.Top });
                    var z = view.ToMm(new POINT { X = pr.Right, Y = pr.Bottom });
                    var mid = new Vec2((Math.Max(0, a.X) + Math.Min(w, z.X)) / 2, (Math.Max(0, a.Y) + Math.Min(h, z.Y)) / 2);
                    if (mid.X > 0 && mid.X < w && mid.Y > 0 && mid.Y < h) at = mid;
                }
            }
            at = new Vec2(Math.Max(5, Math.Min(w - 5, at.X)), Math.Max(5, Math.Min(h - 5, at.Y)));
            // Repeated inserts do not land exactly on each other.
            var taken = new List<Vec2>();
            foreach (PowerPoint.Shape s in slide.Shapes)
            {
                try
                {
                    if (!SketchUnits.IsUnit(s)) continue;
                    taken.Add(new Vec2((s.Left + s.Width / 2) / Geo.PtPerMm, (s.Top + s.Height / 2) / Geo.PtPerMm));
                }
                catch { }
            }
            for (var i = 0; i < 12 && taken.Any(q => (q - at).Length < 3); i++)
                at = new Vec2(Math.Min(w - 5, at.X + 8), Math.Min(h - 5, at.Y + 8));
            return at;
        }

        private void InsertAt(LibraryItem item, Vec2 at)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var setup = _app.ActivePresentation.PageSetup;
            var doc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
            doc = Edits.Add(doc, doc.Pages[0].Id, SymbolSize.ForSlide(registry, registry.Create(item, at), SlideStyle.Current(slide).Symbols));
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
            // While armed: the nearest handle decides (a "+" or corner dot beside the point keeps its own clicks).
            if (_mode == Mode.Armed) return NearestIndex(view, pt, "pt:");
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
            if (_unit is not null) { LastHandles = HandleLayout.Describe(Handles(view)); LastShownHandles = ShownHandles(view); }
            if (_part is not null && PartOwner() is not null)
            {
                // A selected chart part: its outline, and the grips stay draggable.
                Present(view, _ => { }, g => { DrawPart(g, view); DrawGrips(g, view); FlushTip(g, view); });
                return;
            }
            var o = PointObject();
            if (o is null && _chainSource is not null) { DrawChainTools(view); return; }
            if (o is null && _links.Count == 0 && _ports.Count == 0 && _sides.Count == 0 && _grips.Count == 0) { PresentHaloOnly(view); return; }
            var pts = o is null ? null : _unit!.Doc.ResolvePoints(o);
            Present(view, _ => { }, g =>
            {
                DrawGrips(g, view);
                if (pts is not null) DrawHandleSet(g, view, pts, _selectedPoints);
                if (pts is not null) DrawSegmentHandles(g, view);
                DrawPorts(g, view);
                DrawSides(g, view);
                DrawLinks(g, view);
                QueueFirstHint(view);
                // The hovered handle's tooltip last, so no handle covers it.
                FlushTip(g, view);
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

        /// <summary>
        /// Shift while drawing or editing a line: the direction from <paramref name="from"/> snaps to 15 degree steps, the
        /// length follows the pointer.
        /// </summary>
        internal static Vec2 AngleStep(Vec2 from, Vec2 to, double stepDegrees = 15)
        {
            var d = to - from;
            var len = d.Length;
            if (len < 1e-9) return to;
            var step = stepDegrees * Math.PI / 180;
            var a = Math.Round(Math.Atan2(d.Y, d.X) / step) * step;
            return new Vec2(from.X + Math.Cos(a) * len, from.Y + Math.Sin(a) * len);
        }

        private void UpdatePointEdit(POINT pt, SlideView view)
        {
            var o = PointObject();
            if (o is null) return;
            var raw = view.ToMm(pt);
            // Shift: 15 degree steps from the neighbouring point.
            var neighbour = _pointOrigin is { Count: > 1 } po ? po[_pointIndex == 0 ? 1 : _pointIndex - 1] : (Vec2?)null;
            var angled = neighbour is not null && KeyDown(VK_SHIFT);
            var snap = NoSnap() || angled ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
            _pointAt = angled ? AngleStep(neighbour!.Value, raw) : raw + snap.Delta;
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
        /// point then follows that element (moved, rotated, resized). Pins keep their anchor id. Annotation lines
        /// (<paramref name="typeId"/> with <see cref="ObjectType.DocksOnSurface"/>) also dock where no snap point is, on the
        /// surface of the element below the point. Null if not anchoring.
        /// </summary>
        private Attachment? AnchorFor(Vec2 at, SnapResult? snap, string? typeId = null)
        {
            if (!AddInSettings.Anchor) return null;
            // Box targets: normalised position (and pin id); lines: their rail and the position along it (a branch).
            if (snap?.Target is { Owner: { } ownerId } target && _targetOwners.TryGetValue(ownerId, out var owner))
                return Attachments.For(owner, at, target);
            return typeId is not null && _registry().Type(typeId)?.DocksOnSurface == true ? Attachments.OnSurface(at, _targetOwners.Values) : null;
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
            var partners = CouplingPreview(doc);
            var label = (uniform ? $"×{sx:0.00}" : $"×{sx:0.00} / {sy:0.00}")
                + (aboutCentre ? T("  · centre (Ctrl: edge)", "  · Mitte (Strg: Kante)") : T("  · edge (Ctrl: centre)", "  · Kante (Strg: Mitte)"));
            var at = view.ToScreen(p);
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, partners, Partner, (float)view.Mm(1.6));
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

        /// <summary>A value for the next input action instead of its prompt (automation), consumed by <see cref="RunTypeAction"/>.</summary>
        private string? _scriptedInput;

        /// <summary>Runs an input command with <paramref name="input"/> instead of asking (automation); false if no unit is armed.</summary>
        public bool RunCommand(string id, string input)
        {
            if (_unit is null) return false;
            Post(() => _scriptedInput = input);
            return RunCommand(id);
        }

        /// <summary>Runs a bar/pie command on the armed unit (automation); false if no unit is armed.</summary>
        public bool RunCommand(string id)
        {
            if (_mode == Mode.Multi && _multi.Count >= 2)
            {
                Log.Info("Command: " + id);
                Post(() => ApplyMulti(id));
                return true;
            }
            var unit = _unit;
            if (unit is null) return false;
            Log.Info("Command: " + id);
            Post(() => ApplyAction(unit, id));
            return true;
        }

        private void ApplyAction(SketchUnit stale, string id)
        {
            if (id == "more") { OpenPieFromBar(); return; }
            // A pie submenu by id ("menu:c:@p.chart.curve.shape", "menu:g:style"; automation and UI tests): opened at the pointer.
            if (id.StartsWith("menu:", StringComparison.Ordinal))
            {
                if (_view is { } pv && _unit is not null && _mode is Mode.Armed or Mode.Pie)
                {
                    if (id.StartsWith("menu:g:", StringComparison.Ordinal)) _pieMenus = PieRing(PieGroups(PointObject(), _registry()), T("More ▸", "Mehr ▸")).Menus;
                    GetCursorPos(out var pp);
                    OpenPie(pp, pv, id.Substring(5));
                }
                return;
            }
            // Opening or closing a bar choice is no edit; picking an option becomes the edit command.
            if (ChoiceCommand(id) is not { } command) return;
            id = command;
            // Element values: the card, a value of the pie, the values label (Calc 2).
            if (ValueCommand(stale, id)) return;
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            if (id.StartsWith("port:", StringComparison.Ordinal))
            {
                PortCommand(unit, id);
                return;
            }
            if (id == "partlist" || id.StartsWith("part:", StringComparison.Ordinal) || id.StartsWith("partsel:", StringComparison.Ordinal))
            {
                PartCommand(slide, unit, id);
                return;
            }
            if (id.StartsWith("pt:", StringComparison.Ordinal))
            {
                CornerCommand(unit, id);
                return;
            }
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
            if (id.StartsWith("info:", StringComparison.Ordinal)) return;
            if (id == "motion")
            {
                ToggleMotion();
                return;
            }
            if (id == "sketch")
            {
                BeginSketch();
                return;
            }
            // "Select the assembly" is "moves along"; "connect:<kind>" selects what is connected that way.
            if (id == "assembly" || id.StartsWith("connect:", StringComparison.Ordinal))
            {
                SelectConnected(slide, unit, id == "assembly" ? ConnectKind.Coupled : KindOf(id.Substring(8)) ?? ConnectKind.Auto);
                return;
            }
            if (id == "explode")
            {
                ApplyExplode(slide, unit);
                return;
            }
            if (id.StartsWith("ins:", StringComparison.Ordinal))
            {
                // Placed where it fits (gear on the shaft, gear in mesh, bearing beside the gear …), else at once beside
                // the selection (nothing follows the pointer).
                if (registry.Item(id.Substring(4)) is { } item && !InsertFitting(slide, unit, item)) InsertNow(item, _insertToward, unit);
                return;
            }
            if (id == "planscale")
            {
                PlanScaleRequested?.Invoke();
                return;
            }
            if (id == "ghosts")
            {
                // Four ghost positions of the chain as thin dashed copies of its links, one unit (one undo step).
                var link = unit.Objects.FirstOrDefault(o => o.IsPointGeometry && Linkage.IsPart(registry, o));
                if (link is null) return;
                var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
                var poses = Linkage.Poses(registry, full, link.Id, 4, _lang());
                if (poses.Count == 0) return;
                var ghostStyle = new Style { Stroke = "#8C8C8C", Dash = InventaSketch.Core.DashStyle.Dash, StrokeWidth = 0.18 };
                var ghosts = new List<SketchObject>();
                foreach (var pose in poses)
                    foreach (var kv in pose)
                    {
                        var src = full.Get(kv.Key);
                        if (src is null) continue;
                        ghosts.Add(src with
                        {
                            Id = Ids.New(),
                            Points = kv.Value.Select(q => new Vec2(Math.Round(q.X, 4), Math.Round(q.Y, 4))).ToList(),
                            Attach = null,
                            Released = null,
                            GroupId = null,
                            Style = ghostStyle,
                        });
                    }
                _app.StartNewUndoEntry();
                var setup = _app.ActivePresentation.PageSetup;
                var ghostDoc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
                ghostDoc = Edits.Add(ghostDoc, ghostDoc.Pages[0].Id, ghosts.ToArray());
                ghostDoc = Edits.Group(ghostDoc, ghosts.Select(g => g.Id).ToList());
                SketchUnits.Draw(slide, registry, ghostDoc, _lang(), zOrder: 0);
                Log.Info("Ghost positions: " + poses.Count);
                return;
            }
            if (id == "trace")
            {
                // The coupler curve of the link's last point as a motion path element (one undo step).
                var link = unit.Objects.FirstOrDefault(o => o.IsPointGeometry && Linkage.IsPart(registry, o));
                if (link is null) return;
                var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
                if (Linkage.Trace(registry, full, link.Id, link.Points!.Count - 1, 72, _lang()) is not { } path || path.Points.Count < 3) return;
                var item = registry.Item(path.Closed ? "kin.path.closed" : "kin.path");
                if (item is null) return;
                _app.StartNewUndoEntry();
                var curve = registry.Create(item, path.Points[0]) with { Points = path.Points.Select(q => new Vec2(Math.Round(q.X, 4), Math.Round(q.Y, 4))).ToList() };
                if (!path.Closed) curve = curve.WithParam("arrow", false);
                SketchUnits.Draw(slide, registry, SingleUnitDoc(curve), _lang());
                Log.Info("Traced coupler curve: " + path.Points.Count + " points" + (path.Closed ? ", closed" : ""));
                return;
            }
            if (id == "nextbox")
            {
                Post(() => OpenNextBox(unit));
                return;
            }
            if (id == "insert")
            {
                // The insert box at the pointer (no undo step of its own; the inserted element makes one).
                Post(() => OpenInsertSearch(slide, unit));
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
            else if (id.StartsWith("gap:", StringComparison.Ordinal))
            {
                // An insert place on the drawing (between table rows or columns): applied to the element that offers it.
                var key = id.Substring(4);
                var ctx = new RenderContext(_unitContext ?? doc, _lang());
                var owner = doc.Objects.Values.FirstOrDefault(o => InsertHandles.Of(registry, o, ctx).Any(h => h.Key == key));
                if (owner is null) return;
                doc = Edits.Update(doc, new[] { owner.Id }, o => InsertHandles.Apply(registry, o, key, ctx));
            }
            else if (id.StartsWith("act:", StringComparison.Ordinal) && InventaSketch.Engine.PageActions.IsPageCommandId(id.Substring(4)))
            {
                // "Number all parts" from a numeral's menu: the slide's page command, as in the "More" menu (every unit of the
                // slide, continuing after the numerals of the other slides; identical parts there keep their numeral).
                SlideNumbering.Run(_app, registry, _lang(), slide, id.Substring(4));
                return;
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
        /// result (other units through <see cref="ApplyOutsideOwn"/>; a unit whose own elements the action removed is deleted).
        /// Returns null if nothing is to be applied.
        /// </summary>
        private SketchDocument? RunTypeAction(PowerPoint.Slide slide, SketchUnit unit, string actionId)
        {
            var registry = _registry();
            var lang = _lang();
            var own = unit.Doc;
            var full = SketchUnits.NeedsContext(registry, own) || SketchUnits.HasDependents(registry, own) ? SketchUnits.WithContext(own, SketchUnits.All(slide, unit.Id)) : own;
            var first = own.ObjectsOn(own.Pages[0]).FirstOrDefault(o => registry.Type(o.Type) is not null);
            var action = first is null ? null : registry.Actions(first, new RenderContext(full, lang)).FirstOrDefault(a => a.Id == actionId);
            var scripted = _scriptedInput;
            _scriptedInput = null;
            if (action is null || !action.IsEnabled) return null;
            var owner = _view?.MainWindow ?? IntPtr.Zero;
            string? input = null;
            if (action.Input is not null && scripted is not null) input = scripted;
            else if (action.Input is not null)
            {
                // Chart data: the table editor instead of the one-line prompt (charts v2).
                input = action.Id == "chart.data" ? DataEditor.Ask(action.Label.Get(lang), action.Input.Value, owner)
                    : TextPrompt.AskInput(action.Input.Label.Get(lang), action.Input.Unit, action.Input.Value, owner);
                if (input is null) return null;
            }
            try
            {
                var ran = ContextActions.Run(registry, full, own.Objects.Keys.ToList(), actionId, input, lang);
                ApplyOutsideOwn(slide, unit, full, ran);
                // The action removed the unit's own element (a junction dot made a bridge): its shape goes too.
                if (own.Objects.Count > 0 && own.Objects.Keys.All(k => ran.Get(k) is null))
                {
                    unit.Shape.Delete();
                    Log.Info("Action " + actionId + " removed unit " + unit.Id);
                    return null;
                }
                return Edits.Update(own, own.Objects.Keys.ToList(), o => ran.Get(o.Id) ?? o);
            }
            catch (FormatException ex)
            {
                TextPrompt.Warn(ex.Message, owner);
                return null;
            }
        }

        /// <summary>
        /// What a document action did outside the unit's own objects (ROADMAP plan review item 2, "Add ray bundle"): objects it
        /// created become one new unit on the slide (inside the action's undo step), objects it removed from other units are
        /// taken out of those units (a unit left empty is deleted), and objects it changed in other units are taken over
        /// there (the operating point of a linked chart sets the pump's flow, Calc 5).
        /// </summary>
        private void ApplyOutsideOwn(PowerPoint.Slide slide, SketchUnit unit, SketchDocument before, SketchDocument after)
        {
            var registry = _registry();
            var lang = _lang();
            var own = new HashSet<string>(unit.Doc.Objects.Keys);
            var added = after.Objects.Values.Where(o => !before.Objects.ContainsKey(o.Id)).ToList();
            var removed = new HashSet<string>(before.Objects.Keys.Where(k => !own.Contains(k) && !after.Objects.ContainsKey(k)));
            if (removed.Count > 0)
                foreach (var other in SketchUnits.All(slide, unit.Id).Where(u => u.Doc.Objects.Keys.Any(removed.Contains)).ToList())
                {
                    var keep = other.Doc.Objects.Keys.Where(k => !removed.Contains(k)).ToList();
                    if (keep.Count == 0 || other.Doc.Objects.Values.Where(o => keep.Contains(o.Id)).All(o => registry.Type(o.Type) is null))
                        other.Shape.Delete();
                    else
                        SketchUnits.Replace(slide, other, Edits.Remove(other.Doc, other.Doc.Objects.Keys.Where(removed.Contains).ToList()), registry, lang);
                    Log.Info("Action removed objects of unit " + other.Id);
                }
            var changed = after.Objects.Values.Where(o => !own.Contains(o.Id) && before.Get(o.Id) is { } b && !ReferenceEquals(b, o) && !Equals(b, o) && registry.Type(o.Type) is not null)
                .Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            if (changed.Count > 0)
                foreach (var other in SketchUnits.All(slide, unit.Id).Where(u => u.Doc.Objects.Keys.Any(changed.Contains)).ToList())
                {
                    var ids = other.Doc.Objects.Keys.Where(changed.Contains).ToList();
                    var replaced = Edits.Update(other.Doc, ids, x => after.Get(x.Id) ?? x);
                    SketchUnits.Replace(slide, other, replaced, registry, lang);
                    SketchUnits.RedrawDependents(slide, registry, lang, new[] { other.Doc }, new[] { replaced });
                    Log.Info("Action changed " + ids.Count + " object(s) of unit " + other.Id);
                }
            if (added.Count == 0) return;
            var setup = _app.ActivePresentation.PageSetup;
            var doc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
            doc = Edits.Add(doc, doc.Pages[0].Id, added.ToArray());
            SketchUnits.Draw(slide, registry, doc, lang);
            Log.Info("Action added " + added.Count + " object(s) as a new unit: " + string.Join(", ", added.Select(o => o.Type)));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);
    }
}
