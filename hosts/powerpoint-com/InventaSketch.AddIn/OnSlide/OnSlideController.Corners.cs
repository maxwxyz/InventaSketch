using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Corners and branches of a selected wire, pipe, road or polyline (<see cref="Polylines"/>), like the bend handles of
    /// draw.io and Visio: every segment shows a small "+" in its middle. Click it for a new corner there, or drag it to bend
    /// the line in one go. Lines that take branches also show a branch handle beside the "+": drag it to a pin or anywhere
    /// (a click adds a short stub) and a new line of the same kind starts on the line (wires and pipes draw a junction
    /// dot, roads join). A double-click on a corner removes it. Branches already on the line stay where they are.
    /// Free ends of open lines show a "+" beyond the end: drag it to lengthen the line, click it to keep drawing (each click
    /// adds a corner; double-click, Enter, Esc or right-click ends; the last point docks like a line end). A click on a corner
    /// selects it: the bar then offers split here and remove. Dropping an end on a free end of a line of the same kind joins
    /// both lines.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private const double SegHandlePx = 5, BranchOffsetPx = 16, MinSegmentPx = 28;

        /// <summary>Segment handles of the armed line: segment index, "+" position and the side for the branch handle (mm).</summary>
        private List<(int Seg, Vec2 At, Vec2 Normal)> _segHandles = new();
        private bool _canBranch;
        /// <summary>Hovered handle: 2 * segment (+1 for the branch handle), -1 for none.</summary>
        private int _hoverSeg = -1;

        // Corner insert during a point drag: the unit with the new corner (preview) and what to restore on cancel.
        private int _insertSeg = -1;
        private Vec2 _insertAt;
        private SketchUnit? _unitBeforeInsert;

        // Branch drag.
        private int _branchSeg;
        private Vec2 _branchFrom;
        private Vec2 _branchEnd;
        private SnapResult? _branchSnap;
        private POINT _branchDown;
        private bool _branchMoved;

        private void PrepareSegments(SketchUnit unit)
        {
            _segHandles = new List<(int, Vec2, Vec2)>();
            _endHandles = new List<(bool, Vec2, Vec2)>();
            _crossings = new List<Crossing>();
            _canBranch = false;
            _hoverSeg = -1;
            var o = PointObject();
            var registry = _registry();
            if (o is null || !(Polylines.CanInsert(registry, o) || Polylines.CanExtend(registry, o))) return;
            try
            {
                var doc = _unitContext ?? unit.Doc;
                var pts = doc.ResolvePoints(o);
                // Straight lines and arrows only get end handles (drawing on makes them polylines).
                var segments = Polylines.CanInsert(registry, o) ? Polylines.SegmentCount(registry, doc, doc.Get(o.Id) ?? o) : 0;
                for (var i = 0; i < segments; i++)
                {
                    var d = pts[(i + 1) % pts.Count] - pts[i];
                    if (d.Length < 1e-6) continue;
                    var n = new Vec2(-d.Y, d.X) * (1 / d.Length);
                    // The branch handle sits above (or left of) the segment.
                    if (n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0)) n = n * -1;
                    _segHandles.Add((i, Polylines.SegmentHandle(registry, doc, doc.Get(o.Id) ?? o, i, _lang()), n));
                }
                _canBranch = Polylines.CanBranch(registry, doc, doc.Get(o.Id) ?? o, _lang());
                // Crossings with other wires or pipes: a connect icon at each.
                var full = _unitContext ?? SketchUnits.WithContext(unit.Doc, _otherUnits);
                _crossings = Crossings.Find(registry, full, full.Get(o.Id) ?? o, _lang()).ToList();
                // Free ends (not docked) of open lines: "+" to extend or keep drawing.
                if (Polylines.CanExtend(registry, o))
                    foreach (var atStart in new[] { true, false })
                    {
                        var i = atStart ? 0 : pts.Count - 1;
                        var docked = o.Attach is not null && i < o.Attach.Count && o.Attach[i] is not null;
                        var dir = pts[i] - pts[atStart ? 1 : pts.Count - 2];
                        if (docked || dir.Length < 1e-6) continue;
                        _endHandles.Add((atStart, pts[i], dir * (1 / dir.Length)));
                    }
            }
            catch (Exception ex)
            {
                Log.Error("Segment handles", ex);
                _segHandles.Clear();
            }
        }

        /// <summary>
        /// Screen positions of the visible segment handles from the shared handle layout (<see cref="Handles"/>): junction
        /// icons, "+" at free ends without a port "+", corner dots and branch handles (short segments get none).
        /// </summary>
        private IEnumerable<(int Code, PointF At)> SegmentHandlesOnScreen(SlideView view)
        {
            if (PointObject() is null || _unit is null || (_segHandles.Count == 0 && _endHandles.Count == 0 && _crossings.Count == 0)) yield break;
            foreach (var h in Handles(view))
            {
                if (!h.Visible) continue;
                var code = SegmentCode(h.Id);
                if (code >= 0) yield return (code, h.At);
            }
        }

        /// <summary>Hover/hit code of a segment handle id ("cross:i", "end:k", "seg:i", "branch:i"), -1 for other handles.</summary>
        internal static int SegmentCode(string id)
        {
            var cut = id.IndexOf(':');
            if (cut < 0 || !int.TryParse(id.Substring(cut + 1), out var n)) return -1;
            return id.Substring(0, cut) switch
            {
                "cross" => CrossCode + n,
                "end" => EndCode + n,
                "seg" => 2 * n,
                "branch" => 2 * n + 1,
                _ => -1,
            };
        }

        /// <summary>The tooltip of a segment handle (from the layout).</summary>
        private string SegmentTip(SlideView view, int code)
            => Handles(view).FirstOrDefault(h => h.Visible && SegmentCode(h.Id) == code)?.Spec.Tip ?? "";

        private int SegmentHandleAt(POINT pt, SlideView view)
        {
            if (PointObject() is null || (_segHandles.Count == 0 && _endHandles.Count == 0 && _crossings.Count == 0)) return -1;
            var h = NearestHandle(view, pt);
            return h is null ? -1 : SegmentCode(h.Id);
        }

        private void DrawSegmentHandles(Graphics g, SlideView view)
        {
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (PointObject() is null || _unit is null) return;
            foreach (var h in Handles(view))
            {
                if (!h.Visible) continue;
                var code = SegmentCode(h.Id);
                if (code < 0) continue;
                var hover = code == _hoverSeg;
                // Progressive disclosure: corner dots, branches, junctions and further "+" appear as the pointer comes near.
                var alpha = hover ? 1 : Reveal(view, h);
                var half = (float)(Math.Max(PortPx, SegHandlePx) * 1.5 * s) + 3;
                Faded(g, h.At, alpha, half, (gg, c) => DrawSegmentHandle(gg, view, c, code, hover));
                if (hover) QueueTip(view, h.At, h.Spec.Tip, (float)(code >= EndCode && code < CrossCode ? PortPx : SegHandlePx));
            }
        }

        private static void DrawSegmentHandle(Graphics g, SlideView view, PointF c, int code, bool hover)
        {
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (code >= EndCode && code < CrossCode)
            {
                // Free end: the same "add here" handle as every other "+".
                DrawAddHandle(g, view, c, hover);
                return;
            }
            using var ring = new Pen(FlowBlue, 1.3f * s);
            using var glyph = new Pen(FlowBlue, 1.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new SolidBrush(Color.White);
            using var hot = new SolidBrush(Color.FromArgb(225, 236, 252));
            using var blue = new SolidBrush(FlowBlue);
            if (code < EndCode && code % 2 == 0)
            {
                // New corner: a small hollow dot on the segment (no "+", so it is not mistaken for "add an element").
                var rd = (float)(CornerDotPx * (hover ? 1.5 : 1.0) * s);
                g.FillEllipse(hover ? hot : fill, c.X - rd, c.Y - rd, 2 * rd, 2 * rd);
                g.DrawEllipse(ring, c.X - rd, c.Y - rd, 2 * rd, 2 * rd);
                return;
            }
            var r = (float)(SegHandlePx * (hover ? 1.25 : 1.0) * s);
            g.FillEllipse(hover ? hot : fill, c.X - r, c.Y - r, 2 * r, 2 * r);
            g.DrawEllipse(ring, c.X - r, c.Y - r, 2 * r, 2 * r);
            var k = r * 0.55f;
            if (code >= CrossCode)
            {
                // Junction: two crossing strokes and a dot.
                g.DrawLine(glyph, c.X - k, c.Y, c.X + k, c.Y);
                g.DrawLine(glyph, c.X, c.Y - k, c.X, c.Y + k);
                var d = 1.5f * s;
                g.FillEllipse(blue, c.X - d, c.Y - d, 2 * d, 2 * d);
            }
            else
            {
                // Branch: a stub leaving a dot.
                var d = 1.3f * s;
                g.FillEllipse(blue, c.X - d, c.Y + k - d, 2 * d, 2 * d);
                g.DrawLine(glyph, c.X, c.Y + k, c.X, c.Y - k);
            }
        }

        /// <summary>Segment handles while armed: start of a corner drag (with the new corner) or of a branch drag.</summary>
        private bool SegmentMouse(int message, POINT pt, SlideView view)
        {
            if (message != WM_LBUTTONDOWN || _unit is null) return false;
            var code = SegmentHandleAt(pt, view);
            if (code < 0) return false;
            var o = PointObject();
            if (o is null) return false;
            if (code >= CrossCode)
            {
                var unit = _unit;
                var crossing = _crossings[code - CrossCode];
                HideBar();
                Post(() => ApplyConnectCrossing(unit, crossing));
                return true;
            }
            if (code >= EndCode)
            {
                HideBar();
                BeginDraw(code == EndCode, pt);
                return true;
            }
            var seg = code / 2;
            var handle = _segHandles.FirstOrDefault(h => h.Seg == seg);
            HideBar();
            _down = pt;
            _moved = false;
            if (code % 2 == 1)
            {
                _branchSeg = seg;
                _branchFrom = handle.At;
                _branchEnd = handle.At + handle.Normal * 15;
                _branchSnap = null;
                _branchDown = pt;
                _branchMoved = false;
                _mode = Mode.Branch;
                return true;
            }
            // Corner: drag the new point right away (preview with the corner inserted; committed on release). The corner
            // starts where its dot is drawn: the layout may have slid the dot along its segment, away from the middle
            // (concept section 9 item 6).
            var start = DrawnCornerAt(view, o, seg, handle.At);
            _unitBeforeInsert = _unit;
            _insertSeg = seg;
            _insertAt = start;
            _unit = new SketchUnit(_unit.Shape, _unit.Id, Edits.InsertPoint(_unit.Doc, o.Id, seg + 1, start), _unit.BoundsPt);
            _selectedPoints.Clear();
            _pointIndex = seg + 1;
            _pointOrigin = _unit.Doc.ResolvePoints(PointObject()!);
            _pointAt = start;
            _mode = Mode.EditPoint;
            return true;
        }

        /// <summary>
        /// The page point (mm) where the corner dot of segment <paramref name="seg"/> is drawn, projected onto the segment
        /// (kept clear of its ends); <paramref name="fallback"/> (the segment's handle point) when the dot is not placed.
        /// </summary>
        private Vec2 DrawnCornerAt(SlideView view, SketchObject line, int seg, Vec2 fallback)
        {
            var placed = HandleById(view, "seg:" + seg.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var pts = (_unitContext ?? _unit?.Doc)?.ResolvePoints(line);
            if (placed is not { Visible: true } || pts is null || seg >= pts.Count || pts.Count < 2) return fallback;
            var mm = view.ToMm(new POINT { X = (int)Math.Round(placed.At.X), Y = (int)Math.Round(placed.At.Y) });
            Vec2 a = pts[seg], b = pts[(seg + 1) % pts.Count];
            var d = b - a;
            var len2 = d.X * d.X + d.Y * d.Y;
            if (len2 < 1e-9) return fallback;
            var t = Math.Max(0.08, Math.Min(0.92, ((mm - a).X * d.X + (mm - a).Y * d.Y) / len2));
            var at = a + d * t;
            // Within a pixel of the handle point: keep the engine's point exactly (the usual case).
            return (at - fallback).Length * Geo.PtPerMm * view.PixelsPerPoint < 1.5 ? fallback : at;
        }

        /// <summary>Ends a corner drag: restores the armed unit and returns the segment (or -1 if no corner was being added).</summary>
        private int EndCornerInsert()
        {
            var seg = _insertSeg;
            if (seg < 0) return -1;
            _insertSeg = -1;
            if (_unitBeforeInsert is not null) _unit = _unitBeforeInsert;
            _unitBeforeInsert = null;
            return seg;
        }

        /// <summary>Input while dragging a branch out of a line.</summary>
        private bool BranchMouse(int message, POINT pt, SlideView view)
        {
            if (message == WM_MOUSEMOVE)
            {
                if (!_branchMoved && Math.Abs(pt.X - _branchDown.X) + Math.Abs(pt.Y - _branchDown.Y) < 5 * view.DeviceScale) return true;
                _branchMoved = true;
                var raw = view.ToMm(pt);
                // Shift: 15 degree steps from where the branch leaves the line.
                var angled = KeyDown(VK_SHIFT);
                var snap = NoSnap() || angled ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
                _branchEnd = angled ? AngleStep(_branchFrom, raw) : raw + snap.Delta;
                _branchSnap = snap;
                DrawBranch(view, snap.Guides);
                return true;
            }
            if (message == WM_LBUTTONUP)
            {
                var unit = _unit;
                var seg = _branchSeg;
                var from = _branchFrom;
                var end = _branchEnd;
                var attach = _branchMoved ? AnchorFor(end, _branchSnap, PointObject()?.Type) : null;
                _mode = Mode.Armed;
                _overlay.Clear();
                if (unit is not null) Post(() => ApplyBranch(unit, from, end, attach));
                Log.Info($"Branch from segment {seg}" + (attach is null ? "" : " to " + attach.ObjectId));
                return true;
            }
            if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
            return true;
        }

        private void DrawBranch(SlideView view, IReadOnlyList<SnapGuide> guides)
        {
            var o = PointObject();
            if (o is null || _unit is null) return;
            var registry = _registry();
            var (doc, id) = Polylines.Branch(registry, _unitContext ?? _unit.Doc, o.Id, _branchFrom, _branchEnd, null, _lang());
            if (id is null) return;
            var prims = registry.Render(doc.Get(id)!, new RenderContext(doc, _lang()));
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, guides);
            });
        }

        // ------------------------------------------------------------------ extend and keep drawing

        private const int EndCode = 100000, CrossCode = 200000;

        /// <summary>Crossings of the armed wire or pipe with others (connect icons).</summary>
        private List<Crossing> _crossings = new();

        /// <summary>Connects the armed line to the line it crosses (split and docked on both sides); one undo step.</summary>
        private void ApplyConnectCrossing(SketchUnit stale, Crossing crossing)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var doc = Crossings.Connect(registry, full, line.Id, crossing, _lang());
            if (ReferenceEquals(doc, full)) return;
            _app.StartNewUndoEntry();
            foreach (var part in doc.Objects.Values.Where(o => full.Get(o.Id) is null))
                SketchUnits.Draw(slide, registry, SingleUnitDoc(part), _lang());
            CommitLineEdit(slide, unit, others, full, doc, line.Id, startUndo: false);
            Log.Info($"Connected {line.Type} at a crossing with {crossing.Other}");
        }

        /// <summary>Free ends of the armed line: start or end, position and outward direction (mm).</summary>
        private List<(bool AtStart, Vec2 End, Vec2 Dir)> _endHandles = new();

        private bool _drawAtStart;
        private bool _drawSticky;
        private POINT _drawDown;
        private readonly List<Vec2> _drawPts = new();
        private Vec2 _drawAt;
        private SnapResult? _drawSnap;

        /// <summary>Starts extending the armed line from one of its free ends (drag) or drawing on (click).</summary>
        private void BeginDraw(bool atStart, POINT pt)
        {
            _drawAtStart = atStart;
            _drawSticky = false;
            _drawDown = pt;
            _drawPts.Clear();
            _drawSnap = null;
            _drawAt = _endHandles.First(e => e.AtStart == atStart).End;
            _mode = Mode.Draw;
        }

        private bool DrawMouse(int message, POINT pt, SlideView view)
        {
            if (message == WM_MOUSEMOVE)
            {
                if (!_drawSticky && _drawPts.Count == 0 && Math.Abs(pt.X - _drawDown.X) + Math.Abs(pt.Y - _drawDown.Y) < 4 * view.DeviceScale) return true;
                UpdateDraw(pt, view);
                return true;
            }
            if (message == WM_LBUTTONUP && !_drawSticky)
            {
                var moved = Math.Abs(pt.X - _drawDown.X) + Math.Abs(pt.Y - _drawDown.Y) >= 4 * view.DeviceScale;
                if (moved)
                {
                    // Drag: one new end where it was released.
                    UpdateDraw(pt, view);
                    _drawPts.Add(_drawAt);
                    FinishDraw();
                }
                else
                {
                    // Click: keep drawing; every click adds a corner.
                    _drawSticky = true;
                    UpdateDraw(pt, view);
                }
                return true;
            }
            if (message == WM_LBUTTONDOWN && _drawSticky)
            {
                UpdateDraw(pt, view);
                var last = _drawPts.Count > 0 ? view.ToScreen(_drawPts[_drawPts.Count - 1]) : (PointF?)null;
                // A second click on the last corner ends the line there.
                if (last is { } l && Math.Abs(l.X - pt.X) + Math.Abs(l.Y - pt.Y) < 6 * view.DeviceScale) { FinishDraw(); return true; }
                _drawPts.Add(_drawAt);
                UpdateDraw(pt, view);
                return true;
            }
            if (message == WM_LBUTTONDBLCLK || message == WM_RBUTTONDOWN) { FinishDraw(); return true; }
            return message is WM_LBUTTONUP or WM_RBUTTONUP;
        }

        /// <summary>Preview of the line with the corners drawn so far and the pointer (snapping like a line end).</summary>
        private void UpdateDraw(POINT pt, SlideView view)
        {
            var o = PointObject();
            if (o is null || _unit is null) return;
            var raw = view.ToMm(pt);
            // Shift: 15 degree steps from the last corner (or the end being extended).
            var from = _drawPts.Count > 0 ? _drawPts[_drawPts.Count - 1] : _endHandles.First(e => e.AtStart == _drawAtStart).End;
            var angled = KeyDown(VK_SHIFT);
            var snap = NoSnap() || angled ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
            _drawAt = angled ? AngleStep(from, raw) : raw + snap.Delta;
            _drawSnap = snap;
            var registry = _registry();
            var doc = _unitContext ?? _unit.Doc;
            foreach (var q in _drawPts.Append(_drawAt)) doc = Polylines.Extend(registry, doc, o.Id, _drawAtStart, q, null, _lang()).Doc;
            var prims = registry.Render(doc.Get(o.Id)!, new RenderContext(doc, _lang()));
            var hint = _drawSticky ? T("Click: corner · double-click, Enter or Esc: done", "Klick: Ecke · Doppelklick, Enter oder Esc: fertig") : null;
            Present(view, g =>
            {
                ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                DrawGuides(g, view, snap.Guides);
            }, hint is null ? null : g => DrawTip(g, view, new PointF(pt.X + 18 * (float)view.DeviceScale, pt.Y + 22 * (float)view.DeviceScale), hint));
        }

        /// <summary>Commits the drawn corners (one undo step); the last one docks like a line end.</summary>
        private void FinishDraw()
        {
            var unit = _unit;
            var pts = _drawPts.ToList();
            var atStart = _drawAtStart;
            var attach = pts.Count > 0 && _drawSnap is not null && pts[pts.Count - 1].DistanceTo(_drawAt) < 1e-6 ? AnchorFor(_drawAt, _drawSnap, PointObject()?.Type) : null;
            _drawPts.Clear();
            _mode = Mode.Armed;
            _overlay.Clear();
            if (unit is null || pts.Count == 0)
            {
                if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
                return;
            }
            Log.Info($"Draw on: {pts.Count} point(s) at the {(atStart ? "start" : "end")}");
            Post(() => ApplyExtend(unit, atStart, pts, attach));
        }

        private void ApplyExtend(SketchUnit stale, bool atStart, IReadOnlyList<Vec2> pts, Attachment? lastAttach)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var doc = full;
            for (var i = 0; i < pts.Count; i++)
                doc = Polylines.Extend(registry, doc, line.Id, atStart, pts[i], i == pts.Count - 1 ? lastAttach : null, _lang()).Doc;
            CommitLineEdit(slide, unit, others, full, doc, line.Id);
            Log.Info($"Extended {line.Type} by {pts.Count} point(s)");
        }

        // ------------------------------------------------------------------ corner selection, split, join

        /// <summary>A click on a corner of a line with free corners selects it (for split / remove in the bar).</summary>
        private bool SelectCorner(int index)
        {
            var o = PointObject();
            if (o is null || !Polylines.CanInsert(_registry(), o)) return false;
            var again = _selectedPoints.Count == 1 && _selectedPoints.Contains(index);
            _selectedPoints.Clear();
            if (!again) _selectedPoints.Add(index);
            return true;
        }

        private IEnumerable<BarButton> CornerButtons()
        {
            var o = PointObject();
            if (o?.Points is null || _selectedPoints.Count != 1) yield break;
            var registry = _registry();
            if (!Polylines.CanInsert(registry, o)) yield break;
            var i = _selectedPoints.First();
            var n = o.Points.Count;
            var closed = Polylines.IsClosed(registry, o);
            if (closed || (i > 0 && i < n - 1))
                yield return new BarButton("pt:split:" + i, "", closed ? T("Open the outline here", "Umriss hier öffnen") : T("Split the line here", "Linie hier teilen")) { IconId = IconIds.Split };
            if (n > (closed ? 3 : 2) && (closed || (i > 0 && i < n - 1)))
                yield return new BarButton("pt:remove:" + i, "", T("Remove this corner (double-click)", "Diese Ecke entfernen (Doppelklick)")) { IconId = IconIds.RemovePoint };
            yield return new BarButton("", "", "", Separator: true);
        }

        private void CornerCommand(SketchUnit unit, string id)
        {
            var parts = id.Split(':');
            if (parts.Length < 3 || !int.TryParse(parts[2], out var index)) return;
            _selectedPoints.Clear();
            if (parts[1] == "remove") { ApplyRemoveCorner(unit, index); return; }
            if (parts[1] != "split") return;
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var (doc, newId) = Polylines.Split(registry, full, line.Id, index, _lang());
            if (ReferenceEquals(doc, full)) return;
            _app.StartNewUndoEntry();
            if (newId is not null && doc.Get(newId) is { } part) SketchUnits.Draw(slide, registry, SingleUnitDoc(part), _lang());
            CommitLineEdit(slide, unit, others, full, doc, line.Id, startUndo: false);
            Log.Info($"Split {line.Type} at corner {index}");
        }

        /// <summary>A free end of a line of the same kind in another unit exactly at <paramref name="at"/> (join on drop).</summary>
        private (SketchUnit Unit, string LineId, bool AtStart)? JoinTarget(int index, Vec2 at)
        {
            var o = PointObject();
            if (o?.Points is null || (index != 0 && index != o.Points.Count - 1) || !Polylines.CanExtend(_registry(), o)) return null;
            foreach (var u in _otherUnits)
                foreach (var other in u.Objects.Where(x => x.Type == o.Type && x.Points is { Count: >= 2 }))
                {
                    if (!Polylines.CanExtend(_registry(), other)) continue;
                    var pts = u.Doc.ResolvePoints(other);
                    foreach (var atStart in new[] { true, false })
                    {
                        var k = atStart ? 0 : pts.Count - 1;
                        var docked = other.Attach is not null && k < other.Attach.Count && other.Attach[k] is not null;
                        if (!docked && pts[k].DistanceTo(at) < 0.05) return (u, other.Id, atStart);
                    }
                }
            return null;
        }

        private void ApplyJoin(SketchUnit stale, int index, Vec2 at, SketchUnit otherUnit, string otherId, bool otherAtStart)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            var otherLive = SketchUnits.Read(otherUnit.Shape);
            if (slide is null || unit is null || otherLive is null) return;
            var registry = _registry();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var doc = Edits.SetPoint(full, line.Id, index, at);
            doc = Polylines.Join(registry, doc, line.Id, index == 0, otherId, otherAtStart, _lang());
            if (doc.Get(otherId) is not null) return;
            _app.StartNewUndoEntry();
            // The joined line's unit goes; its other objects (if any) stay in their unit.
            var rest = otherLive.Objects.Where(x => x.Id != otherId).Select(x => x.Id).ToList();
            if (rest.Count == 0) otherLive.Shape.Delete();
            else SketchUnits.Replace(slide, otherLive, Edits.Remove(otherLive.Doc, new[] { otherId }), registry, _lang());
            CommitLineEdit(slide, unit, others.Where(u => u.Id != otherLive.Id).ToList(), full, doc, line.Id, startUndo: false);
            Log.Info($"Joined {line.Type} with {otherId}");
        }

        /// <summary>
        /// Stores a line edit made on the merged document: other units whose objects changed (docked branches) are redrawn,
        /// then the line's own unit with its dependents (selected). One undo step unless the caller started it.
        /// </summary>
        private void CommitLineEdit(PowerPoint.Slide slide, SketchUnit unit, IReadOnlyList<SketchUnit> others, SketchDocument full, SketchDocument doc, string lineId, bool startUndo = true)
        {
            var registry = _registry();
            var lang = _lang();
            doc = Attachments.Refresh(registry, doc, new RenderContext(doc, lang));
            if (startUndo) _app.StartNewUndoEntry();
            foreach (var u in others)
            {
                var ids = u.Objects.Where(o => doc.Get(o.Id) is { } t && full.Get(o.Id) is { } f && !ReferenceEquals(t, f)).Select(o => o.Id).ToList();
                if (ids.Count > 0) SketchUnits.Replace(slide, u, Edits.Update(u.Doc, ids, o => o with { Points = doc.Get(o.Id)!.Points, Attach = doc.Get(o.Id)!.Attach }), registry, lang);
            }
            ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { lineId }, _ => doc.Get(lineId)!));
        }

        /// <summary>Adds the corner of segment <paramref name="seg"/> (at <paramref name="at"/>); branches stay docked; one undo step.</summary>
        private void ApplyCorner(SketchUnit stale, int seg, Vec2 mid, Vec2 at, Attachment? attach)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var lang = _lang();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var doc = Polylines.InsertCorner(registry, full, line.Id, seg + 1, mid, lang);
            if ((at - mid).Length > 1e-6 || attach is not null) doc = Edits.SetPoint(doc, line.Id, seg + 1, at, attach);
            // Branches in other units were docked again onto the new rails.
            CommitLineEdit(slide, unit, others, full, doc, line.Id);
            Log.Info($"Corner added to {line.Type} on segment {seg}");
        }

        /// <summary>Removes corner <paramref name="index"/> of the armed line (double-click); one undo step.</summary>
        private void ApplyRemoveCorner(SketchUnit stale, int index)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var lang = _lang();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var doc = Polylines.RemoveCorner(registry, full, line.Id, index, lang);
            if (ReferenceEquals(doc, full)) return;
            CommitLineEdit(slide, unit, others, full, doc, line.Id);
            Log.Info($"Corner {index} removed from {line.Type}");
        }

        /// <summary>A branch of the armed line from <paramref name="from"/> to <paramref name="end"/>; one undo step, selected.</summary>
        private void ApplyBranch(SketchUnit stale, Vec2 from, Vec2 end, Attachment? endAttach)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var lang = _lang();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
            var (doc, id) = Polylines.Branch(registry, full, line.Id, from, end, endAttach, lang);
            if (id is null) return;
            _app.StartNewUndoEntry();
            SketchUnits.Draw(slide, registry, SingleUnitDoc(doc.Get(id)!), lang)?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Branch {line.Type} added");
        }
    }
}
