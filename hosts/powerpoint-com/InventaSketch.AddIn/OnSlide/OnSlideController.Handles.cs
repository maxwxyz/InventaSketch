using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// One layout for every handle InventaSketch draws around the selected element (<see cref="HandleLayout"/>): point
    /// diamonds, "+" at build ports, free line ends and table sides, lock badges, junction icons, corner dots and branch
    /// handles. Their positions are computed together, clear of PowerPoint's own handles and of each other, and the same
    /// positions serve drawing and hit testing. One look per meaning (docs/en/design/overlay-ux.md):
    /// filled blue "+" = add something here (the tooltip says what), hollow dot on a segment = new corner, stub = branch,
    /// lock = coupled, diamond = move this point.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Visible handles as last drawn: "id@x,y,r;…" in device pixels (UI tests through the automation object).</summary>
        internal string LastHandles { get; private set; } = "";

        private const double CornerDotPx = 3.6, SidePlusOffsetPx = 14;

        private List<PlacedHandle> _layout = new();
        private object? _layoutKey;

        /// <summary>Table-like elements: actions that add at a side ("side.top" …), drawn as "+" on that side.</summary>
        private List<(string Side, ContextAction Action)> _sides = new();
        private int _hoverSide2 = -1;

        /// <summary>Pending tooltip of the hovered handle, drawn last so no handle covers it.</summary>
        private (PointF At, string Text)? _tip;

        /// <summary>The handle layout for the armed unit in the current view (cached until the unit or the view changes).</summary>
        private IReadOnlyList<PlacedHandle> Handles(SlideView view)
        {
            var key = (_unit, _unit?.Doc, view.OffsetX, view.OffsetY, view.PixelsPerPoint, view.DeviceScale, _ports, _links, _segHandles, _sides, _chainSource, _crossings, _endHandles, _grips);
            if (_layoutKey is not null && key.Equals(_layoutKey)) return _layout;
            try
            {
                _layout = HandleLayout.Resolve(HandleSpecs(view), (float)view.DeviceScale);
            }
            catch (Exception ex)
            {
                Log.Error("Handle layout", ex);
                _layout = new List<PlacedHandle>();
            }
            _layoutKey = key;
            return _layout;
        }

        private PlacedHandle? HandleById(SlideView view, string id) => Handles(view).FirstOrDefault(h => h.Id == id);

        // ------------------------------------------------------------------ progressive disclosure

        /// <summary>
        /// Pointer distance (logical pixels) within which a secondary handle is fully shown, and beyond which it is gone;
        /// in between it fades. Shift shows every handle.
        /// </summary>
        internal const float RevealNear = 24, RevealFar = 56;

        /// <summary>Last pointer position while armed (screen pixels), for the disclosure of handles.</summary>
        private POINT _pointer = new() { X = -100000, Y = -100000 };
        private string _revealKey = "";
        /// <summary>Shift is held: every handle shown.</summary>
        private bool _shiftHeld;

        /// <summary>
        /// Opacity of a secondary handle for a pointer distance (logical pixels): 1 within <see cref="RevealNear"/>, 0 beyond
        /// <see cref="RevealFar"/>, linear in between.
        /// </summary>
        internal static float RevealFor(double distance)
            => distance <= RevealNear ? 1f : distance >= RevealFar ? 0f : (float)((RevealFar - distance) / (RevealFar - RevealNear));

        /// <summary>
        /// How much of a handle is shown (0..1). Always: point diamonds, the one primary "+" (<see cref="PrimaryHandleId"/>).
        /// Everything else (more "+", corner dots, branches, junctions, grips, lock badges, quick-add triangles) appears as
        /// the pointer comes near it, or while Shift is held.
        /// </summary>
        private float Reveal(SlideView view, PlacedHandle h, PointF? anchor = null)
        {
            if (h.Kind is HandleKind.Point or HandleKind.Native) return 1;
            if (h.Id == PrimaryHandleId(view) || _shiftHeld) return 1;
            var s = view.DeviceScale;
            double D(PointF p) => Math.Sqrt((p.X - _pointer.X) * (p.X - _pointer.X) + (p.Y - _pointer.Y) * (p.Y - _pointer.Y)) / s;
            var d = D(h.At);
            if (anchor is { } a) d = Math.Min(d, D(a));
            else if (h.Spec.Anchor != h.At) d = Math.Min(d, D(h.Spec.Anchor));
            return RevealFor(d);
        }

        /// <summary>
        /// The one "+" shown right away: the most useful place to build on. Lines: the free end of the line (else its start);
        /// tables: the bottom (a row); diagram nodes: the triangle below; gears: the free side to the right (else any free
        /// side); others: a connection port, a free axis end, then any build port. Null if the element has none.
        /// </summary>
        private string? PrimaryHandleId(SlideView view)
        {
            var handles = Handles(view);
            if (_primaryFor == _layout) return _primary;
            _primaryFor = _layout;
            _primary = null;
            bool Has(string id) => handles.Any(h => h.Visible && h.Id == id);
            if (_chainSource is not null) { _primary = "tri:" + FlowSide.Bottom; return _primary; }
            if (_sides.Any(x => x.Side == "bottom")) { _primary = "side:bottom"; return _primary; }
            if (PointObject() is { Points: { } pts })
            {
                var endPort = EndPortIndex(atStart: false);
                var startPort = EndPortIndex(atStart: true);
                _primary = endPort >= 0 ? "port:" + endPort : Has("end:1") ? "end:1" : startPort >= 0 ? "port:" + startPort : Has("end:0") ? "end:0" : null;
                if (_primary is null && _ports.Count > 0) _primary = "port:0";
                return _primary;
            }
            int Find(Func<BuildPort, bool> f) => _ports.FindIndex(p => f(p));
            var order = new Func<BuildPort, bool>[]
            {
                p => p.Kind == PortKind.Mesh && p.Id == "mesh.right",
                p => p.Kind == PortKind.Mesh,
                p => p.Kind == PortKind.Connect,
                p => p.Kind == PortKind.AxisEnd && p.Id.EndsWith(".end", StringComparison.Ordinal),
                p => p.Kind == PortKind.AxisEnd,
                p => true,
            };
            foreach (var f in order)
                if (Find(f) is var i and >= 0) { _primary = "port:" + i; break; }
            return _primary;
        }

        private string? _primary;
        private object? _primaryFor;

        /// <summary>Ids of the handles shown at least half as last drawn ("id;id;…"; UI tests: calm on selection).</summary>
        internal string LastShownHandles { get; private set; } = "";

        private string ShownHandles(SlideView view)
            => string.Join(";", Handles(view).Where(h => h.Visible && h.Kind != HandleKind.Native && Reveal(view, h) >= 0.5f).Select(h => h.Id));

        /// <summary>
        /// While armed: remembers the pointer and redraws the handles when what is shown changes (handles fading in or out as
        /// the pointer comes near, Shift pressed or released). Opacities are compared in fifths, so a redraw is rare.
        /// </summary>
        private void UpdateReveal(POINT pt, SlideView view, bool fromMouse = false)
        {
            _pointer = pt;
            // The key state is current when the pointer moves (inside the keyboard hook it is not yet).
            if (fromMouse) _shiftHeld = KeyDown(VK_SHIFT);
            if (_unit is null) return;
            var handles = Handles(view);
            var sb = new System.Text.StringBuilder(_shiftHeld ? "S" : "-");
            foreach (var h in handles)
                if (h.Visible && h.Kind is not (HandleKind.Native or HandleKind.Point))
                    sb.Append((int)Math.Round(Reveal(view, h) * 5));
            if (_chainSource is not null)
                foreach (var side in Sides)
                    sb.Append((int)Math.Round(TriangleReveal(view, side) * 5));
            var key = sb.ToString();
            if (key == _revealKey) return;
            _revealKey = key;
            DrawPointHandles(view);
        }

        /// <summary>Quick-add triangle opacity: the one below always, the others near the pointer.</summary>
        private float TriangleReveal(SlideView view, FlowSide side)
        {
            var h = HandleById(view, "tri:" + side);
            return h is null ? 1 : Reveal(view, h);
        }

        /// <summary>
        /// Draws one handle with an opacity: fully opaque straight onto the overlay, otherwise through a small bitmap blended
        /// with that alpha (so every handle look fades the same way without touching its colours).
        /// </summary>
        private static void Faded(Graphics g, PointF c, float alpha, float half, Action<Graphics, PointF> draw)
        {
            if (alpha <= 0.02f) return;
            if (alpha >= 0.98f) { draw(g, c); return; }
            var size = Math.Max(4, (int)Math.Ceiling(2 * half));
            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.SmoothingMode = SmoothingMode.AntiAlias;
                bg.Clear(Color.Transparent);
                draw(bg, new PointF(size / 2f, size / 2f));
            }
            using var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha });
            g.DrawImage(bmp, new Rectangle((int)Math.Round(c.X - size / 2f), (int)Math.Round(c.Y - size / 2f), size, size), 0, 0, size, size, GraphicsUnit.Pixel, ia);
        }

        /// <summary>Hit radius of every handle of ours (logical pixels): targets of 24 px at least, whatever they look like.</summary>
        internal const float HitPx = 12;

        /// <summary>
        /// The handle of ours under the pointer: the nearest one within <see cref="HitPx"/>, so neighbouring targets never
        /// steal each other's clicks. Null when nothing is near or PowerPoint's own handle is nearer (it keeps resizing and
        /// rotating); a point diamond wins over PowerPoint's resize handle at the same place.
        /// </summary>
        private PlacedHandle? NearestHandle(SlideView view, POINT pt)
        {
            var s = (float)view.DeviceScale;
            PlacedHandle? best = null;
            var bestD = double.MaxValue;
            foreach (var h in Handles(view))
            {
                var native = h.Kind == HandleKind.Native;
                if (!native && !h.Visible) continue;
                var d = Math.Sqrt((h.At.X - pt.X) * (h.At.X - pt.X) + (h.At.Y - pt.Y) * (h.At.Y - pt.Y));
                var tol = native ? 9 * s : Math.Max(HitPx, h.Spec.Radius * 1.3f) * s;
                if (native) d += 2 * s;
                if (d <= tol && d < bestD) { best = h; bestD = d; }
            }
            return best is { Kind: HandleKind.Native } ? null : best;
        }

        /// <summary>The number after the prefix of the nearest handle's id ("port:3" → 3) if it has that prefix, else -1.</summary>
        private int NearestIndex(SlideView view, POINT pt, string prefix)
        {
            var h = NearestHandle(view, pt);
            return h is not null && h.Id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(h.Id.Substring(prefix.Length), out var n) ? n : -1;
        }

        /// <summary>Every handle of the armed unit with its preferred place, in drawing order.</summary>
        private List<HandleSpec> HandleSpecs(SlideView view)
        {
            var s = (float)view.DeviceScale;
            var specs = new List<HandleSpec>();
            if (_unit is null) return specs;
            var frame = UnitScreenRect(view);
            specs.AddRange(HandleLayout.NativeHandles(frame, s));
            var small = Math.Max(frame.Width, frame.Height) < HandleLayout.SmallElement * s;
            var o = PointObject();
            if (o is not null)
            {
                var pts = _unit.Doc.ResolvePoints(o);
                for (var i = 0; i < pts.Count; i++)
                    specs.Add(new HandleSpec("pt:" + i, HandleKind.Point, view.ToScreen(pts[i]), default, 0, 5, 100) { Fixed = true });
            }
            // Grips on values of the element (chart limit lines, value lines, markers): on the drawing, fixed.
            for (var i = 0; i < _grips.Count; i++)
                specs.Add(new HandleSpec("drag:" + i, HandleKind.Drag, view.ToScreen(_grips[i].Position), default, 0, (float)GripPx + 3, 95) { Fixed = true, Tip = _grips[i].Label.Get(_lang()) });
            if (_chainSource is not null)
                foreach (var side in Sides)
                    specs.Add(new HandleSpec("tri:" + side, HandleKind.Triangle, TriangleCentre(view, side), default, 0, (float)(TrianglePx * 0.6), 100) { Fixed = true });
            // "+" at build ports (gears, shafts, machines, connection ports, free line ends).
            for (var i = 0; i < _ports.Count; i++)
            {
                var p = _ports[i];
                if (p.Kind == PortKind.Hub)
                {
                    // A free hub's "+" sits beyond the top right corner, so the element is still dragged by its centre.
                    specs.Add(new HandleSpec("port:" + i, HandleKind.Add, new PointF(frame.Right, frame.Top), new PointF(1, -1), 22, (float)PortPx, 90) { Tip = PortTip(p), MaxPush = 40 });
                    continue;
                }
                specs.Add(new HandleSpec("port:" + i, HandleKind.Add, view.ToScreen(p.At), new PointF((float)p.Dir.X, (float)p.Dir.Y), (float)PortOffsetPx, (float)PortPx, 90) { Tip = PortTip(p), MaxPush = 72 });
            }
            // Free ends of lines without a port "+" there (polylines, roads …): the same "+", it draws the line on.
            foreach (var (atStart, end, dir) in _endHandles)
            {
                if (EndPortIndex(atStart) >= 0) continue;
                specs.Add(new HandleSpec("end:" + (atStart ? 0 : 1), HandleKind.Add, view.ToScreen(end), new PointF((float)dir.X, (float)dir.Y), 18, (float)PortPx, 88)
                {
                    Tip = T("Draw the line on: drag, or click and click the corners", "Linie weiterzeichnen: ziehen, oder klicken und Ecken anklicken"),
                    MaxPush = 60,
                });
            }
            // Table sides: one "+" per side that adds a row or column there.
            foreach (var (side, action) in _sides)
            {
                var (anchor, dir) = side switch
                {
                    "top" => (new PointF(frame.Left + frame.Width / 2, frame.Top), new PointF(0, -1)),
                    "bottom" => (new PointF(frame.Left + frame.Width / 2, frame.Bottom), new PointF(0, 1)),
                    "left" => (new PointF(frame.Left, frame.Top + frame.Height / 2), new PointF(-1, 0)),
                    _ => (new PointF(frame.Right, frame.Top + frame.Height / 2), new PointF(1, 0)),
                };
                specs.Add(new HandleSpec("side:" + side, HandleKind.Add, anchor, dir, (float)SidePlusOffsetPx, (float)PortPx, 85) { Tip = action.Label.Get(_lang()), MaxPush = 30 });
            }
            // Lock badges where the element touches another one and follows it.
            for (var i = 0; i < _links.Count; i++)
                specs.Add(new HandleSpec("lock:" + i, HandleKind.Lock, view.ToScreen(_links[i].At), new PointF(0.7071f, -0.7071f), (float)(BadgeOffsetPx * 1.414), (float)BadgePx, 70) { Orbit = true, MaxPush = 48 });
            // Junctions where a wire or pipe crosses another one.
            for (var i = 0; i < _crossings.Count; i++)
                specs.Add(new HandleSpec("cross:" + i, HandleKind.Junction, view.ToScreen(_crossings[i].At), new PointF(0.7071f, -0.7071f), (float)(BranchOffsetPx * 1.1), (float)SegHandlePx, 60)
                {
                    Orbit = true,
                    MaxPush = 30,
                    Tip = T("Connect here (junction)", "Hier verbinden (Knotenpunkt)"),
                });
            // Corner dots and branch handles: optional, none on very small elements or on segments too short on screen.
            if (o is not null && !small && _segHandles.Count > 0)
            {
                var pts = (_unitContext ?? _unit.Doc).ResolvePoints(o);
                foreach (var (seg, at, n) in _segHandles)
                {
                    if (seg >= pts.Count) continue;
                    var a = view.ToScreen(pts[seg]);
                    var b = view.ToScreen(pts[(seg + 1) % pts.Count]);
                    if (Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) < MinSegmentPx * s) continue;
                    var c = view.ToScreen(at);
                    var len = (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                    // The corner dot stays on its segment: it may slide along it (a quarter of the length each way).
                    specs.Add(new HandleSpec("seg:" + seg, HandleKind.Corner, c, new PointF((b.X - a.X) / len, (b.Y - a.Y) / len), 0, (float)CornerDotPx, 40)
                    {
                        Optional = true,
                        Along = true,
                        MaxPush = len / 4 / s,
                        Tip = T("Add a corner: click, or drag to bend", "Ecke einfügen: klicken, oder ziehen zum Knicken"),
                    });
                    if (_canBranch)
                        specs.Add(new HandleSpec("branch:" + seg, HandleKind.Branch, c, new PointF((float)n.X, (float)n.Y), (float)BranchOffsetPx, (float)SegHandlePx, 30)
                        {
                            Optional = true,
                            Orbit = true,
                            MaxPush = 10,
                            Tip = T("Branch: drag to a pin or anywhere, click for a stub", "Abzweig: zu einem Anschluss oder frei ziehen, Klick für Stich"),
                        });
                }
            }
            return specs;
        }

        /// <summary>Index in <see cref="_ports"/> of the line-end port at the start or end of the armed line, -1 if none.</summary>
        private int EndPortIndex(bool atStart)
        {
            var o = PointObject();
            if (o?.Points is null) return -1;
            var pid = "p" + (atStart ? 0 : o.Points.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return _ports.FindIndex(p => p.Kind == PortKind.LineEnd && p.AnchorId == pid);
        }

        /// <summary>The free end a line-end port belongs to (true: start), or null if the line has no end handle there.</summary>
        private bool? EndOfPort(BuildPort port)
        {
            if (port.Kind != PortKind.LineEnd || PointObject()?.Points is not { } pts) return null;
            var atStart = port.AnchorId == "p0";
            if (!atStart && port.AnchorId != "p" + (pts.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)) return null;
            return _endHandles.Any(e => e.AtStart == atStart) ? atStart : null;
        }

        // ------------------------------------------------------------------ table sides

        /// <summary>Side actions of the armed unit's element (<see cref="ContextAction.Group"/> "side.top" …).</summary>
        private void PrepareSides(SketchUnit unit)
        {
            _sides = new List<(string, ContextAction)>();
            _hoverSide2 = -1;
            try
            {
                var registry = _registry();
                var objs = unit.Objects.Where(o => registry.Type(o.Type) is not null).ToList();
                if (objs.Count != 1 || objs[0].IsPointGeometry) return;
                var ctx = new RenderContext(_unitContext ?? unit.Doc, _lang());
                foreach (var a in registry.Type(objs[0].Type)!.Actions(objs[0], ctx))
                    if (SideOf(a) is { } side && a.IsEnabled && _sides.All(x => x.Side != side)) _sides.Add((side, a));
            }
            catch (Exception ex)
            {
                Log.Error("Side handles", ex);
            }
        }

        /// <summary>"top", "bottom", "left" or "right" for an action that adds at a side of its element, else null.</summary>
        internal static string? SideOf(ContextAction a)
            => a.Group is { } g && g.StartsWith("side.", StringComparison.Ordinal) && g.Substring(5) is "top" or "bottom" or "left" or "right" ? g.Substring(5) : null;

        private int SideAt(POINT pt, SlideView view)
        {
            if (_sides.Count == 0) return -1;
            var hit = NearestHandle(view, pt);
            return hit is null ? -1 : _sides.FindIndex(x => "side:" + x.Side == hit.Id);
        }

        /// <summary>Side "+" input while armed: hover feedback and the click that adds a row or column there.</summary>
        private bool SideMouse(int message, POINT pt, SlideView view, out bool swallow)
        {
            swallow = false;
            if (_sides.Count == 0 || _unit is null) return false;
            if (message == WM_MOUSEMOVE)
            {
                var i = SideAt(pt, view);
                if (i != _hoverSide2) { _hoverSide2 = i; DrawPointHandles(view); }
                return false;
            }
            if (message is not (WM_LBUTTONDOWN or WM_LBUTTONDBLCLK)) return false;
            var hit = SideAt(pt, view);
            if (hit < 0) return false;
            var unit = _unit;
            var id = "act:" + _sides[hit].Action.Id;
            Log.Info("Side +: " + _sides[hit].Side + " " + id);
            HideBar();
            Post(() => ApplyAction(unit, id));
            swallow = true;
            return true;
        }

        private void DrawSides(Graphics g, SlideView view)
        {
            for (var i = 0; i < _sides.Count; i++)
            {
                var h = HandleById(view, "side:" + _sides[i].Side);
                if (h is null || !h.Visible) continue;
                var hover = i == _hoverSide2;
                Faded(g, h.At, hover ? 1 : Reveal(view, h), AddHalf(view), (gg, c) => DrawAddHandle(gg, view, c, hover));
                if (hover) QueueTip(view, h.At, h.Spec.Tip, (float)PortPx);
            }
        }

        // ------------------------------------------------------------------ grips: drag a value on the drawing

        /// <summary>Visible half size of a grip (logical pixels).</summary>
        private const double GripPx = 5.5;

        /// <summary>Drag handles of the armed unit's element (<see cref="Registry.DragHandles"/>), page mm.</summary>
        private List<DragHandle> _grips = new();
        private string? _gripOwner;
        private int _hoverGrip = -1;
        private DragHandle? _grip;
        private Vec2 _gripAt;
        private POINT _gripDown;
        private bool _gripMoved;

        private void PrepareGrips(SketchUnit unit)
        {
            _grips = new List<DragHandle>();
            _gripOwner = null;
            _hoverGrip = -1;
            try
            {
                var registry = _registry();
                var ctx = new RenderContext(_unitContext ?? unit.Doc, _lang());
                foreach (var o in unit.Objects.Where(x => registry.Type(x.Type) is not null))
                {
                    var handles = registry.DragHandles(o, ctx);
                    if (handles.Count == 0) continue;
                    _grips = handles.ToList();
                    _gripOwner = o.Id;
                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Drag handles", ex);
            }
        }

        private SketchObject? GripObject() => _gripOwner is null ? null : _unit?.Doc.Get(_gripOwner);

        /// <summary>Grip input while armed: hover (tooltip with the value) and the start of a drag.</summary>
        private bool GripStart(int message, POINT pt, SlideView view, out bool swallow)
        {
            swallow = false;
            if (_grips.Count == 0 || _unit is null) return false;
            if (message == WM_MOUSEMOVE)
            {
                var i = NearestIndex(view, pt, "drag:");
                if (i != _hoverGrip) { _hoverGrip = i; DrawPointHandles(view); }
                return false;
            }
            if (message != WM_LBUTTONDOWN) return false;
            var hit = NearestIndex(view, pt, "drag:");
            if (hit < 0 || hit >= _grips.Count) return false;
            HideBar();
            _grip = _grips[hit];
            _gripAt = _grip.Position;
            _gripDown = pt;
            _gripMoved = false;
            _mode = Mode.Grip;
            swallow = true;
            return true;
        }

        /// <summary>
        /// Dragging a grip: the pointer constrained to the grip's direction, a live preview of the changed part with its new
        /// value (the slide is not touched); release applies it in one undo step, a click without moving selects the part.
        /// </summary>
        private bool GripMouse(int message, POINT pt, SlideView view)
        {
            var grip = _grip;
            if (grip is null || _unit is null) { _mode = Mode.Armed; return true; }
            if (message == WM_MOUSEMOVE)
            {
                if (!_gripMoved && Math.Abs(pt.X - _gripDown.X) + Math.Abs(pt.Y - _gripDown.Y) < 3 * view.DeviceScale) return true;
                _gripMoved = true;
                _gripAt = Dragging.Constrain(grip, view.ToMm(pt));
                DrawGripDrag(view, grip);
                return true;
            }
            if (message == WM_LBUTTONUP)
            {
                var unit = _unit;
                var at = _gripAt;
                var moved = _gripMoved;
                _grip = null;
                _mode = Mode.Armed;
                _overlay.Clear();
                if (moved) Post(() => ApplyGrip(unit, grip.Key, at));
                else if (grip.Part is not null && PartOwner() is not null) Post(() => SelectPart(grip.Part));
                else { ShowBar(view); DrawPointHandles(view); }
                return true;
            }
            if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
            return true;
        }

        private void DrawGripDrag(SlideView view, DragHandle grip)
        {
            var o = GripObject();
            if (o is null || _unit is null) return;
            var registry = _registry();
            var lang = _lang();
            var ctxDoc = _unitContext ?? _unit.Doc;
            SketchObject moved;
            try { moved = registry.MoveHandle(o, grip.Key, _gripAt, new RenderContext(ctxDoc, lang)); }
            catch (Exception ex) { Log.Error("Move handle", ex); return; }
            var doc = Edits.Update(ctxDoc, new[] { o.Id }, _ => moved);
            var ctx = new RenderContext(doc, lang);
            var all = Prim.Flatten(registry.Render(moved, ctx)).ToList();
            var part = grip.Part is null ? all : all.Where(p => p.Tag is { } t && (t == grip.Part || t.StartsWith(grip.Part + ".", StringComparison.Ordinal))).ToList();
            var prims = part.Count > 0 ? part : all;
            var after = Dragging.Find(registry.DragHandles(moved, ctx), grip.Key);
            var label = (after ?? grip).Label.Get(lang);
            var at = view.ToScreen(after?.Position ?? _gripAt);
            Present(view, g => ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6)), g =>
            {
                DrawGrip(g, view, at, grip, hover: true);
                DrawTag(g, view, at, label);
            });
        }

        /// <summary>Applies a grip drag to the element on the slide (value snapped by the element): one undo step.</summary>
        private void ApplyGrip(SketchUnit stale, string key, Vec2 at)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var lang = _lang();
            var o = (_gripOwner is null ? null : unit.Doc.Get(_gripOwner)) ?? unit.Objects.FirstOrDefault(x => registry.Type(x.Type) is not null);
            if (o is null) return;
            var full = SketchUnits.NeedsContext(registry, unit.Doc) ? SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id)) : unit.Doc;
            var moved = registry.MoveHandle(o, key, at, new RenderContext(full, lang));
            if (Equals(moved, o)) { Log.Info("Drag handle " + key + ": no change"); if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); } return; }
            _app.StartNewUndoEntry();
            ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { o.Id }, _ => moved));
            Log.Info($"Drag handle {key} of {o.Type} to ({at.X:0.##}, {at.Y:0.##}) mm");
        }

        private void DrawGrips(Graphics g, SlideView view)
        {
            for (var i = 0; i < _grips.Count; i++)
            {
                var h = HandleById(view, "drag:" + i);
                if (h is null) continue;
                var hover = i == _hoverGrip;
                var grip = _grips[i];
                // A grip of the selected part is always shown; the others appear as the pointer comes near.
                var alpha = hover || (_part is not null && grip.Part == _part) ? 1 : Reveal(view, h);
                Faded(g, h.At, alpha, (float)((GripPx * 1.15 + 8) * view.DeviceScale), (gg, c) => DrawGrip(gg, view, c, grip, hover));
                if (hover) QueueTip(view, h.At, h.Spec.Tip + T(" · drag to change", " · ziehen zum Ändern"), (float)GripPx + 4);
            }
        }

        /// <summary>A grip: a small blue-rimmed square with small arrowheads in the directions it may move.</summary>
        private static void DrawGrip(Graphics g, SlideView view, PointF c, DragHandle grip, bool hover)
        {
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = (float)(GripPx * (hover ? 1.15 : 1.0) * s);
            using var fill = new SolidBrush(hover ? Color.FromArgb(225, 236, 252) : Color.White);
            using var rim = new Pen(FlowBlue, 1.6f * s);
            using var blue = new SolidBrush(FlowBlue);
            g.FillRectangle(fill, c.X - r, c.Y - r, 2 * r, 2 * r);
            g.DrawRectangle(rim, c.X - r, c.Y - r, 2 * r, 2 * r);
            g.FillRectangle(blue, c.X - r * 0.35f, c.Y - r * 0.35f, r * 0.7f, r * 0.7f);
            var dirs = grip.Constraint == DragConstraint.Free || grip.Direction.Length < 1e-9
                ? new[] { new Vec2(1, 0), new Vec2(-1, 0), new Vec2(0, 1), new Vec2(0, -1) }
                : new[] { grip.Direction, grip.Direction * -1 };
            foreach (var d in dirs)
            {
                // Arrowhead just outside the square, pointing away from it.
                var tip = new PointF(c.X + (float)d.X * (r + 6 * s), c.Y + (float)d.Y * (r + 6 * s));
                var bx = c.X + (float)d.X * (r + 2 * s);
                var by = c.Y + (float)d.Y * (r + 2 * s);
                var n = new PointF((float)-d.Y * 3 * s, (float)d.X * 3 * s);
                g.FillPolygon(blue, new[] { tip, new PointF(bx + n.X, by + n.Y), new PointF(bx - n.X, by - n.Y) });
            }
        }

        /// <summary>Drag handles of a shape's element for UI tests: "key:x,y;…" in slide points.</summary>
        internal string DragHandlesOf(string shapeName) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null) return "";
            var unit = SketchUnits.Read(slide.Shapes[shapeName]);
            if (unit is null) return "";
            var registry = _registry();
            var ctx = new RenderContext(SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id)), _lang());
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join(";", unit.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.DragHandles(o, ctx))
                .Select(h => h.Key + ":" + (h.Position.X * Geo.PtPerMm).ToString("0.##", ci) + "," + (h.Position.Y * Geo.PtPerMm).ToString("0.##", ci)));
        });

        // ------------------------------------------------------------------ shared drawing

        /// <summary>Half the size of the bitmap a faded "+" is drawn into (screen pixels).</summary>
        private static float AddHalf(SlideView view) => (float)(PortPx * 1.25 * view.DeviceScale) + 3;

        /// <summary>The "add here" handle: a filled blue circle with a white "+" (build ports, free ends, table sides).</summary>
        private static void DrawAddHandle(Graphics g, SlideView view, PointF c, bool hover)
        {
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = (float)(PortPx * (hover ? 1.2 : 1.0) * s);
            using var fill = new SolidBrush(hover ? Color.FromArgb(60, 130, 230) : FlowBlue);
            using var ring = new Pen(Color.White, 1.2f * s);
            g.FillEllipse(fill, c.X - r, c.Y - r, 2 * r, 2 * r);
            g.DrawEllipse(ring, c.X - r, c.Y - r, 2 * r, 2 * r);
            using var white = new Pen(Color.White, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var k = r * 0.55f;
            g.DrawLine(white, c.X - k, c.Y, c.X + k, c.Y);
            g.DrawLine(white, c.X, c.Y - k, c.X, c.Y + k);
        }

        /// <summary>Remembers the tooltip of the hovered handle (drawn last by <see cref="FlushTip"/>).</summary>
        private void QueueTip(SlideView view, PointF at, string text, float radiusPx)
        {
            if (string.IsNullOrEmpty(text)) return;
            _tip = (new PointF(at.X + (radiusPx * 1.6f + 4) * (float)view.DeviceScale, at.Y), text);
        }

        private void FlushTip(Graphics g, SlideView view)
        {
            if (_tip is { } t) DrawTip(g, view, t.At, t.Text);
            _tip = null;
        }
    }
}
