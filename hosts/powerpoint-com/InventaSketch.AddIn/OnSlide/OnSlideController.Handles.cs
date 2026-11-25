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
            var key = (_unit, _unit?.Doc, view.OffsetX, view.OffsetY, view.PixelsPerPoint, view.DeviceScale, _ports, _links, _segHandles, _sides, _chainSource, _crossings, _endHandles);
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
                DrawAddHandle(g, view, h.At, i == _hoverSide2);
                if (i == _hoverSide2) QueueTip(view, h.At, h.Spec.Tip, (float)PortPx);
            }
        }

        // ------------------------------------------------------------------ shared drawing

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
