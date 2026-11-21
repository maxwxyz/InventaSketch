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
    /// Motion mode ("Move" in the ribbon and the bar): dragging moves things only the way they can move. A gear, carrier or
    /// pulley seen along its axis turns about its centre and the whole gear train turns with it (ratios, planets orbiting,
    /// fixed parts standing still, <see cref="GearTrain"/>); a predefined linkage turns its input crank about its fixed
    /// pivot. Centre distances and sizes never change. The preview follows the pointer live; Shift turns in 15° steps;
    /// releasing applies it (one undo step). Outside the mode dragging edits as usual.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Raised when the motion mode is switched from the bar (the ribbon toggle follows).</summary>
        public event Action? MotionToggled;

        private string? _turnId;
        private bool _turnMechanism;
        private Vec2 _turnPivot;
        private double _turnLast;
        private double _turnAngle;
        private double _turnParam0;
        private SketchDocument? _turnDoc;

        /// <summary>The armed unit's object that moves in motion mode (gear train part or linkage), else null.</summary>
        private SketchObject? MovablePart()
        {
            var registry = _registry();
            var o = _unit?.Objects.FirstOrDefault(x => !x.IsPointGeometry && registry.Type(x.Type) is not null);
            if (o is null || _unit!.Objects.Count() != 1) return null;
            if (registry.Type(o.Type)!.Parameters.Any(p => p.Key == "angle") && registry.Type(o.Type)!.AnimationFrames(o, 2).Count > 1) return o;
            return GearTrain.Spins(registry, o) ? o : null;
        }

        /// <summary>Starts turning in motion mode; false if the armed unit cannot move that way.</summary>
        private bool BeginTurn(POINT pt, SlideView view)
        {
            var o = MovablePart();
            if (o is null || _unit is null) return false;
            var registry = _registry();
            var full = SketchUnits.WithContext(_unit.Doc, _otherUnits);
            var anchors = registry.PageAnchors(o, new RenderContext(full, _lang())).ToList();
            _turnMechanism = !GearTrain.Spins(registry, o);
            _turnPivot = _turnMechanism
                ? anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Mount)?.P ?? new Vec2(o.Transform.X, o.Transform.Y)
                : (anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner)?.P
                   ?? anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub)?.P ?? new Vec2(o.Transform.X, o.Transform.Y));
            var p = view.ToMm(pt) - _turnPivot;
            _turnLast = Math.Atan2(p.Y, p.X) * 180 / Math.PI;
            _turnAngle = 0;
            _turnId = o.Id;
            _turnParam0 = o.Num("angle", 0);
            _turnDoc = full;
            HideBar();
            _down = pt;
            _moved = false;
            _mode = Mode.Turn;
            return true;
        }

        private bool TurnMouse(int message, POINT pt, SlideView view)
        {
            if (message == WM_MOUSEMOVE)
            {
                if (!_moved && Math.Abs(pt.X - _down.X) + Math.Abs(pt.Y - _down.Y) < 3 * view.DeviceScale) return true;
                _moved = true;
                var p = view.ToMm(pt) - _turnPivot;
                if (p.Length < 0.5) return true;
                var a = Math.Atan2(p.Y, p.X) * 180 / Math.PI;
                var d = a - _turnLast;
                if (d > 180) d -= 360;
                if (d < -180) d += 360;
                _turnLast = a;
                _turnAngle += d;
                DrawTurn(view);
                return true;
            }
            if (message == WM_LBUTTONUP)
            {
                var unit = _unit;
                var angle = TurnStep(_turnAngle);
                var moved = _moved;
                _mode = Mode.Armed;
                _overlay.Clear();
                if (moved && unit is not null && Math.Abs(angle) > 1e-6) Post(() => ApplyTurn(unit, angle));
                else if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
                return true;
            }
            if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
            return true;
        }

        /// <summary>Shift: 15° steps.</summary>
        private static double TurnStep(double angle) => KeyDown(VK_SHIFT) ? Math.Round(angle / 15) * 15 : angle;

        /// <summary>Live preview: every moving part of the train (or the linkage) in its new position, and the angle.</summary>
        private void DrawTurn(SlideView view)
        {
            if (_turnId is null || _turnDoc is null) return;
            var registry = _registry();
            var angle = TurnStep(_turnAngle);
            var prims = new List<Primitive>();
            try
            {
                if (_turnMechanism)
                {
                    var o = _turnDoc.Get(_turnId)!;
                    var moved = o.WithParam("angle", Math.Round(_turnParam0 + angle, 3));
                    prims.AddRange(registry.Render(moved, new RenderContext(_turnDoc, _lang())));
                }
                else
                {
                    var turned = GearTrain.Turn(registry, _turnDoc, _turnId, angle, _lang());
                    var ctx = new RenderContext(turned, _lang());
                    foreach (var o in turned.Objects.Values.Where(o => !ReferenceEquals(o, _turnDoc.Get(o.Id))))
                        prims.AddRange(registry.Render(o, ctx));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Turn preview", ex);
            }
            var label = angle.ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "°";
            var pivot = view.ToScreen(_turnPivot);
            Present(view, g => ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6)),
                g => DrawTip(g, view, new PointF(pivot.X + 14 * (float)view.DeviceScale, pivot.Y - 24 * (float)view.DeviceScale), label));
        }

        /// <summary>Applies a turn: the train's parts in their new positions (no coupling for this edit), or the crank angle.</summary>
        private void ApplyTurn(SketchUnit stale, double angle)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null || _turnId is null) return;
            var registry = _registry();
            var lang = _lang();
            var id = _turnId;
            if (_turnMechanism)
            {
                _app.StartNewUndoEntry();
                var doc = Edits.Update(unit.Doc, new[] { id }, o => o.WithParam("angle", Math.Round(o.Num("angle", _turnParam0) + angle, 3)));
                ReplaceWithDependents(slide, unit, doc);
                Log.Info($"Turned linkage input by {angle:0.#}°");
                return;
            }
            var others = SketchUnits.All(slide, unit.Id);
            var full = SketchUnits.WithContext(unit.Doc, others);
            var turned = GearTrain.Turn(registry, full, id, angle, lang);
            _app.StartNewUndoEntry();
            _noCouple = true;
            try
            {
                foreach (var u in new[] { unit }.Concat(others))
                {
                    var ids = u.Objects.Where(o => turned.Get(o.Id) is { } t && full.Get(o.Id) is { } f && !ReferenceEquals(t, f)).Select(o => o.Id).ToList();
                    if (ids.Count == 0) continue;
                    var before = u.Doc;
                    var doc = Edits.Update(u.Doc, ids, o => o with { Transform = turned.Get(o.Id)!.Transform });
                    SketchUnits.Replace(slide, u, doc, registry, lang);
                    // Lead lines and arrows docked on a turned part follow it.
                    SketchUnits.RedrawDependents(slide, registry, lang, new[] { before }, new[] { doc }, new[] { unit.Id });
                }
            }
            finally
            {
                _noCouple = false;
            }
            SketchUnits.All(slide).FirstOrDefault(u => u.Id == unit.Id)?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Turned gear train by {angle:0.#}° at {id}");
        }

        /// <summary>Switches motion mode (bar button) and tells the ribbon.</summary>
        private void ToggleMotion()
        {
            AddInSettings.Motion = !AddInSettings.Motion;
            MotionToggled?.Invoke();
            if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
            Log.Info("Motion mode " + (AddInSettings.Motion ? "on" : "off"));
        }
    }
}
