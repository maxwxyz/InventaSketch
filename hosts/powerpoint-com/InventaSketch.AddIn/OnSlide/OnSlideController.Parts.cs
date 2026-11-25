using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Parts of the selected element (charts v2, docs/en/design/charts-v2.md): a click without dragging on a chart selects
    /// the curve, axis, grid … under the pointer; the bar and the pie menu then show that part's actions (its own ones,
    /// then show/hide, thicker, thinner, line style, reset). "Parts ▸" lists every part, also hidden ones.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Selected part key of the armed unit's element, or null (the whole element).</summary>
        private string? _part;

        /// <summary>The element whose parts can be selected (the unit's first element with parts), or null.</summary>
        private SketchObject? PartOwner()
        {
            var registry = _registry();
            if (_unit is null) return null;
            var ctx = new RenderContext(_unit.Doc, _lang());
            return _unit.Objects.Count() == 1 && _unit.Objects.First() is { } o && registry.Type(o.Type) is not null && registry.Parts(o, ctx).Count > 0 ? o : null;
        }

        /// <summary>Selects the part under the pointer (or the whole element when there is none). False if the element has no parts.</summary>
        private bool SelectPartAt(POINT pt, SlideView view)
        {
            if (PartOwner() is not { } o) return false;
            var at = view.ToMm(pt);
            // About six pixels of slack, in mm.
            var tolerance = Math.Max(0.4, (view.ToMm(new POINT { X = pt.X + (int)Math.Round(6 * view.DeviceScale), Y = pt.Y }) - at).Length);
            var part = _registry().PartAt(o, at, new RenderContext(_unit!.Doc, _lang()), tolerance);
            Log.Info("Part: " + (part ?? "(whole element)"));
            _part = part;
            return true;
        }

        private IReadOnlyList<ContextAction> PartActions(SketchObject o)
            => _part is null ? Array.Empty<ContextAction>() : _registry().PartActions(o, _part, new RenderContext(_unit!.Doc, _lang()));

        private PartInfo? SelectedPartInfo(SketchObject o)
            => _part is null ? null : _registry().Parts(o, new RenderContext(_unit!.Doc, _lang())).FirstOrDefault(p => p.Key == _part);

        /// <summary>Bar of a selected part: its name, its actions, "Parts ▸" and "whole element".</summary>
        private List<BarButton>? PartButtons()
        {
            if (_part is null || PartOwner() is not { } o) return null;
            var info = SelectedPartInfo(o);
            if (info is null) { _part = null; return null; }
            var list = new List<BarButton> { new("info:part", info.Label.Get(_lang()), T("Selected part (click on the element: another part)", "Ausgewähltes Teil (Klick ins Element: anderes Teil)")) };
            list.Add(new BarButton("", "", "", Separator: true));
            foreach (var a in PartActions(o))
            {
                var label = a.Label.Get(_lang());
                var tip = label + (a.Input is not null && !label.EndsWith("…", StringComparison.Ordinal) ? " …" : "");
                list.Add(IsTelling(a.Icon)
                    ? new BarButton("part:" + a.Id, "", tip, Active: a.IsActive) { IconId = a.Icon }
                    : new BarButton("part:" + a.Id, ShortLabel(label), tip, Active: a.IsActive));
            }
            list.Add(new BarButton("", "", "", Separator: true));
            list.Add(new BarButton("partlist", "", T("Parts … (also hidden ones)", "Teile … (auch ausgeblendete)")) { IconId = IconIds.Table });
            list.Add(new BarButton("part:none", "", T("Whole element", "Ganzes Element")) { IconId = IconIds.SelectAssembly });
            return list;
        }

        /// <summary>Pie entries of a selected part (first, essential).</summary>
        private IEnumerable<(PieItem Item, bool Essential)> PartPieItems()
        {
            if (_part is null || PartOwner() is not { } o) yield break;
            foreach (var a in PartActions(o))
                yield return (new PieItem("part:" + a.Id, a.Label.Get(_lang()), a.IsEnabled, a.IsActive) { IconId = a.Icon }, true);
            yield return (new PieItem("part:none", T("Whole element", "Ganzes Element")) { IconId = IconIds.SelectAssembly }, true);
        }

        /// <summary>The "Parts ▸" pie: every part of the element (hidden ones marked), choosing one selects it.</summary>
        private List<PieItem>? PartListMenu()
        {
            if (PartOwner() is not { } o) return null;
            var items = new List<PieItem> { new("menu:back", T("← Back", "← Zurück")) };
            foreach (var p in _registry().Parts(o, new RenderContext(_unit!.Doc, _lang())).Take(MaxPieItems + 8))
                items.Add(new PieItem("partsel:" + p.Key, p.Label.Get(_lang()) + (p.Hidden ? T(" (hidden)", " (ausgeblendet)") : ""), Active: p.Key == _part));
            return items;
        }

        /// <summary>Runs a part command from the bar or pie: select another part, leave part mode, or apply a part action.</summary>
        private void PartCommand(PowerPoint.Slide slide, SketchUnit unit, string id)
        {
            if (id == "partlist")
            {
                if (_view is not null) { GetCursorPos(out var p); OpenPie(p, _view, "parts"); }
                return;
            }
            if (id.StartsWith("partsel:", StringComparison.Ordinal))
            {
                _part = id.Substring(8);
                if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
                return;
            }
            var actionId = id.Substring(5);
            if (actionId == "none")
            {
                _part = null;
                if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
                return;
            }
            var o = unit.Doc.ObjectsOn(unit.Doc.Pages[0]).FirstOrDefault(x => _registry().Type(x.Type) is not null);
            if (o is null || _part is null) return;
            var action = _registry().PartActions(o, _part, new RenderContext(unit.Doc, _lang())).FirstOrDefault(a => a.Id == actionId);
            if (action is null || !action.IsEnabled) return;
            var owner = _view?.MainWindow ?? IntPtr.Zero;
            SketchObject next;
            if (action.Input is not null)
            {
                var input = TextPrompt.AskInput(action.Input.Label.Get(_lang()), action.Input.Unit, action.Input.Value, owner);
                if (input is null) return;
                try { next = action.Input.Apply(o, input); }
                catch (FormatException ex) { TextPrompt.Warn(ex.Message, owner); return; }
            }
            else next = action.Apply(o);
            Log.Info("Part action " + actionId + " on " + _part);
            _app.StartNewUndoEntry();
            ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { o.Id }, _ => next));
        }

        /// <summary>Part key at a slide point (points) of a unit, for UI tests; "" if none.</summary>
        internal string PartAtOf(string shapeName, double xPt, double yPt) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null) return "";
            var unit = SketchUnits.Read(slide.Shapes[shapeName]);
            var o = unit?.Objects.FirstOrDefault(x => _registry().Type(x.Type) is not null);
            if (unit is null || o is null) return "";
            return _registry().PartAt(o, new Vec2(xPt / Geo.PtPerMm, yPt / Geo.PtPerMm), new RenderContext(unit.Doc, _lang()), 0.6) ?? "";
        });

        /// <summary>Outlines the selected part on the overlay (accent colour), so it is clear what the bar acts on.</summary>
        private void DrawPart(Graphics g, SlideView view)
        {
            if (_part is null || PartOwner() is not { } o) return;
            var drawn = Prim.Flatten(_registry().Render(o, new RenderContext(_unit!.Doc, _lang())))
                .Where(p => p.Tag is { } t && (t == _part || t.StartsWith(_part + ".", StringComparison.Ordinal))).ToList();
            using var pen = new Pen(Color.FromArgb(200, 31, 95, 191), (float)(2.2 * view.DeviceScale));
            foreach (var p in drawn)
            {
                switch (p)
                {
                    case LinePrim l: g.DrawLine(pen, view.ToScreen(l.A), view.ToScreen(l.B)); break;
                    case PathPrim a:
                        {
                            // Each sub-path on its own (grid lines, ticks), so no line joins them.
                            var sub = new List<PathSeg>();
                            void Flush()
                            {
                                var pts = PartLooks.Sample(sub).Select(view.ToScreen).ToArray();
                                if (pts.Length >= 2) g.DrawLines(pen, pts);
                                sub.Clear();
                            }
                            foreach (var seg in a.Segments) { if (seg.Op == SegOp.M) Flush(); sub.Add(seg); }
                            Flush();
                            break;
                        }
                    default:
                        {
                            var pts = Prim.Points(p).Select(view.ToScreen).ToList();
                            if (pts.Count == 0) break;
                            var r = RectangleF.FromLTRB(pts.Min(x => x.X), pts.Min(x => x.Y), pts.Max(x => x.X), pts.Max(x => x.Y));
                            r.Inflate((float)(2 * view.DeviceScale), (float)(2 * view.DeviceScale));
                            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                            break;
                        }
                }
            }
        }

    }
}
