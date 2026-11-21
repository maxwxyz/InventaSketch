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
            _canBranch = false;
            _hoverSeg = -1;
            var o = PointObject();
            var registry = _registry();
            if (o is null || !Polylines.CanInsert(registry, o)) return;
            try
            {
                var doc = _unitContext ?? unit.Doc;
                var pts = doc.ResolvePoints(o);
                for (var i = 0; i + 1 < pts.Count; i++)
                {
                    var d = pts[i + 1] - pts[i];
                    if (d.Length < 1e-6) continue;
                    var n = new Vec2(-d.Y, d.X) * (1 / d.Length);
                    // The branch handle sits above (or left of) the segment.
                    if (n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0)) n = n * -1;
                    _segHandles.Add((i, Polylines.SegmentHandle(registry, doc, doc.Get(o.Id) ?? o, i, _lang()), n));
                }
                _canBranch = Polylines.CanBranch(registry, doc, doc.Get(o.Id) ?? o, _lang());
            }
            catch (Exception ex)
            {
                Log.Error("Segment handles", ex);
                _segHandles.Clear();
            }
        }

        /// <summary>Screen positions of the visible segment handles (segments too short on screen get none).</summary>
        private IEnumerable<(int Code, PointF At)> SegmentHandlesOnScreen(SlideView view)
        {
            var o = PointObject();
            if (o is null || _segHandles.Count == 0 || _unit is null) yield break;
            var pts = (_unitContext ?? _unit.Doc).ResolvePoints(o);
            var off = (float)(BranchOffsetPx * view.DeviceScale);
            foreach (var (seg, at, n) in _segHandles)
            {
                if (seg + 1 >= pts.Count) continue;
                var a = view.ToScreen(pts[seg]);
                var b = view.ToScreen(pts[seg + 1]);
                if (Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) < MinSegmentPx * view.DeviceScale) continue;
                var s = view.ToScreen(at);
                yield return (2 * seg, s);
                if (_canBranch) yield return (2 * seg + 1, new PointF(s.X + (float)n.X * off, s.Y + (float)n.Y * off));
            }
        }

        private int SegmentHandleAt(POINT pt, SlideView view)
        {
            var r = SegHandlePx * 1.4 * view.DeviceScale;
            foreach (var (code, at) in SegmentHandlesOnScreen(view))
                if (Math.Abs(at.X - pt.X) <= r && Math.Abs(at.Y - pt.Y) <= r) return code;
            return -1;
        }

        private void DrawSegmentHandles(Graphics g, SlideView view)
        {
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var ring = new Pen(FlowBlue, 1.3f * s);
            using var glyph = new Pen(FlowBlue, 1.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new SolidBrush(Color.White);
            using var hot = new SolidBrush(Color.FromArgb(225, 236, 252));
            using var blue = new SolidBrush(FlowBlue);
            PointF? tipAt = null;
            string? tip = null;
            foreach (var (code, c) in SegmentHandlesOnScreen(view))
            {
                var hover = code == _hoverSeg;
                var r = (float)(SegHandlePx * (hover ? 1.25 : 1.0) * s);
                g.FillEllipse(hover ? hot : fill, c.X - r, c.Y - r, 2 * r, 2 * r);
                g.DrawEllipse(ring, c.X - r, c.Y - r, 2 * r, 2 * r);
                var k = r * 0.55f;
                if (code % 2 == 0)
                {
                    g.DrawLine(glyph, c.X - k, c.Y, c.X + k, c.Y);
                    g.DrawLine(glyph, c.X, c.Y - k, c.X, c.Y + k);
                }
                else
                {
                    // Branch: a stub leaving a dot.
                    var d = 1.3f * s;
                    g.FillEllipse(blue, c.X - d, c.Y + k - d, 2 * d, 2 * d);
                    g.DrawLine(glyph, c.X, c.Y + k, c.X, c.Y - k);
                }
                if (hover)
                {
                    tipAt = new PointF(c.X + r + 4 * s, c.Y);
                    tip = code % 2 == 0
                        ? T("Corner: click, or drag to bend", "Ecke: klicken oder ziehen zum Knicken")
                        : T("Branch: drag to a pin or anywhere, click for a stub", "Abzweig: zu einem Anschluss oder frei ziehen, Klick für Stich");
                }
            }
            if (tip is not null && tipAt is { } at) DrawTip(g, view, at, tip);
        }

        /// <summary>Segment handles while armed: start of a corner drag (with the new corner) or of a branch drag.</summary>
        private bool SegmentMouse(int message, POINT pt, SlideView view)
        {
            if (message != WM_LBUTTONDOWN || _unit is null || _segHandles.Count == 0) return false;
            var code = SegmentHandleAt(pt, view);
            if (code < 0) return false;
            var o = PointObject();
            if (o is null) return false;
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
            // Corner: drag the new point right away (preview with the corner inserted; committed on release).
            _unitBeforeInsert = _unit;
            _insertSeg = seg;
            _insertAt = handle.At;
            _unit = new SketchUnit(_unit.Shape, _unit.Id, Edits.InsertPoint(_unit.Doc, o.Id, seg + 1, handle.At), _unit.BoundsPt);
            _selectedPoints.Clear();
            _pointIndex = seg + 1;
            _pointOrigin = _unit.Doc.ResolvePoints(PointObject()!);
            _pointAt = handle.At;
            _mode = Mode.EditPoint;
            return true;
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
                var snap = NoSnap() ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
                _branchEnd = raw + snap.Delta;
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
                var attach = _branchMoved ? AnchorFor(end, _branchSnap) : null;
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
            doc = Attachments.Refresh(registry, doc, new RenderContext(doc, lang));
            _app.StartNewUndoEntry();
            // Branches in other units were docked again onto the new rails.
            foreach (var u in others)
            {
                var ids = u.Objects.Where(o => doc.Get(o.Id) is { } t && full.Get(o.Id) is { } f && !ReferenceEquals(t, f)).Select(o => o.Id).ToList();
                if (ids.Count > 0) SketchUnits.Replace(slide, u, Edits.Update(u.Doc, ids, o => o with { Points = doc.Get(o.Id)!.Points, Attach = doc.Get(o.Id)!.Attach }), registry, lang);
            }
            ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { line.Id }, _ => doc.Get(line.Id)!));
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
            doc = Attachments.Refresh(registry, doc, new RenderContext(doc, lang));
            _app.StartNewUndoEntry();
            foreach (var u in others)
            {
                var ids = u.Objects.Where(o => doc.Get(o.Id) is { } t && full.Get(o.Id) is { } f && !ReferenceEquals(t, f)).Select(o => o.Id).ToList();
                if (ids.Count > 0) SketchUnits.Replace(slide, u, Edits.Update(u.Doc, ids, o => o with { Points = doc.Get(o.Id)!.Points, Attach = doc.Get(o.Id)!.Attach }), registry, lang);
            }
            ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { line.Id }, _ => doc.Get(line.Id)!));
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
