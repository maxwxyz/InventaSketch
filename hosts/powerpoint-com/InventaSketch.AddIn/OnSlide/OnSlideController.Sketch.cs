using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Charts;
using static InventaSketch.AddIn.OnSlide.NativeMethods;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Sketching a curve into a chart (charts v2 stage 3): "Sketch a curve" on an XY chart or a figure, then draw with the
    /// left button held; on release the trail is recognised (step, pulse, ramp, sine, PT1 …) or smoothed and added to the
    /// chart as a new curve (one undo step). Esc or a right click cancels.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private readonly List<Vec2> _sketchPts = new();
        private bool _sketchPressed;
        private POINT _sketchLast;

        /// <summary>The unit's chart if a curve can be sketched into it.</summary>
        private SketchObject? SketchChart() => _unit?.Objects.FirstOrDefault(o => _registry().Type(o.Type) is not null && ChartSketchInput.Supports(o));

        private void BeginSketch()
        {
            if (_view is null || SketchChart() is null) return;
            _sketchPts.Clear();
            _sketchPressed = false;
            HideBar();
            _mode = Mode.Sketch;
            DrawSketch(_view);
            Log.Info("Sketch: started");
        }

        private bool SketchMouse(int message, POINT pt, SlideView view)
        {
            switch (message)
            {
                case WM_LBUTTONDOWN:
                    if (!view.Contains(pt)) { Cancel("sketch: click outside"); return false; }
                    _sketchPressed = true;
                    _sketchPts.Clear();
                    _sketchPts.Add(view.ToMm(pt));
                    _sketchLast = pt;
                    return true;
                case WM_MOUSEMOVE:
                    if (_sketchPressed && Math.Abs(pt.X - _sketchLast.X) + Math.Abs(pt.Y - _sketchLast.Y) >= 2 * view.DeviceScale)
                    {
                        _sketchPts.Add(view.ToMm(pt));
                        _sketchLast = pt;
                        DrawSketch(view);
                    }
                    return _sketchPressed;
                case WM_LBUTTONUP when _sketchPressed:
                    {
                        _sketchPressed = false;
                        _sketchPts.Add(view.ToMm(pt));
                        var unit = _unit;
                        var trail = _sketchPts.ToList();
                        _mode = Mode.Armed;
                        _overlay.Clear();
                        if (unit is not null && trail.Count >= 3) Post(() => ApplySketch(unit, trail));
                        else ShowBar(view);
                        return true;
                    }
                case WM_RBUTTONDOWN:
                    Cancel("sketch: right click");
                    return true;
            }
            return message is WM_LBUTTONUP or WM_RBUTTONUP;
        }

        /// <summary>The trail drawn so far, or a hint before the first stroke.</summary>
        private void DrawSketch(SlideView view)
        {
            var pts = _sketchPts.Select(view.ToScreen).ToArray();
            Present(view, _ => { }, g =>
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if (pts.Length >= 2)
                {
                    using var pen = new Pen(Color.FromArgb(220, 31, 95, 191), (float)(2.4 * view.DeviceScale)) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
                    g.DrawLines(pen, pts);
                }
                else
                {
                    GetCursorPos(out var p);
                    DrawTip(g, view, new PointF(p.X + (float)(16 * view.DeviceScale), p.Y), T("Draw the curve with the left button held · Esc cancels", "Kurve mit gedrückter linker Taste zeichnen · Esc bricht ab"));
                }
            });
        }

        /// <summary>Adds the sketched curve to the chart (recognised or smoothed), one undo step.</summary>
        private void ApplySketch(SketchUnit stale, List<Vec2> trail)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var chart = unit.Objects.FirstOrDefault(o => _registry().Type(o.Type) is not null && ChartSketchInput.Supports(o));
            if (chart is null) return;
            SketchedCurve? result;
            try { result = ChartSketchInput.Add(_registry(), chart, trail, new RenderContext(unit.Doc, _lang())); }
            catch (Exception ex) { Log.Error("Sketch", ex); return; }
            if (result is null) { Log.Info("Sketch: nothing added"); return; }
            Log.Info("Sketch: " + result.Message.En);
            _app.StartNewUndoEntry();
            ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { chart.Id }, _ => result.Chart));
        }
    }
}
