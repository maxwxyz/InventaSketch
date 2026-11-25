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

        /// <summary>Selects a part (null: the whole element) and refreshes the bar and the outline.</summary>
        private void SelectPart(string? key)
        {
            _part = key;
            Log.Info("Part: " + (key ?? "(whole element)"));
            if (_view is not null) { ShowBar(_view); DrawPointHandles(_view); }
        }

        /// <summary>Tab / Shift+Tab with a part selected: the next (previous) visible part, wrapping around.</summary>
        private void StepPart(bool back)
        {
            if (PartOwner() is not { } o) return;
            var keys = _registry().Parts(o, new RenderContext(_unit!.Doc, _lang())).Where(p => !p.Hidden && !p.Detail).Select(p => p.Key).ToList();
            if (keys.Count == 0) return;
            var i = _part is null ? -1 : keys.IndexOf(_part);
            var next = i < 0 ? (back ? keys.Count - 1 : 0) : (i + (back ? keys.Count - 1 : 1)) % keys.Count;
            SelectPart(keys[next]);
        }

        /// <summary>Selected part key, for UI tests ("" = whole element).</summary>
        internal string SelectedPartKey() => OnUi(() => _part ?? "");

        private IReadOnlyList<ContextAction> PartActions(SketchObject o)
            => _part is null ? Array.Empty<ContextAction>() : _registry().PartActions(o, _part, new RenderContext(_unit!.Doc, _lang()));

        private PartInfo? SelectedPartInfo(SketchObject o)
            => _part is null ? null : _registry().Parts(o, new RenderContext(_unit!.Doc, _lang())).FirstOrDefault(p => p.Key == _part);

        /// <summary>
        /// Bar of a selected part: its name, its three most used commands, "⋯". A choice among several options (curve shape,
        /// line style, pattern …) is one dropdown button "choice:@p.&lt;key&gt;" showing the current option (its pictogram or
        /// picture, else a short text), never a cycle (maintainer 2026-11-25).
        /// </summary>
        private List<BarButton>? PartButtons()
        {
            if (_part is null || PartOwner() is not { } o) return null;
            var info = SelectedPartInfo(o);
            if (info is null) { _part = null; return null; }
            var list = new List<BarButton> { new("info:part", info.Label.Get(_lang()), T("Selected part (click or Tab: another part, Esc: whole element)", "Ausgewähltes Teil (Klick oder Tab: anderes Teil, Esc: ganzes Element)")) };
            list.Add(new BarButton("", "", "", Separator: true));
            var actions = PartActions(o);
            var choices = PartChoices(o);
            foreach (var e in ActionChoiceBar.Entries(actions).Where(e => e.IsChoice || e.Action!.IsEnabled).Take(MaxBarButtons - 1))
            {
                if (e.Choice is { } c)
                {
                    var bc = choices.FirstOrDefault(x => x.Key == ActionChoiceBar.Key(ActionChoiceBar.PartPrefix, c));
                    if (bc is not null) list.Add(ChoiceButton(bc));
                    continue;
                }
                var a = e.Action!;
                var label = a.Label.Get(_lang());
                var tip = label + (a.Input is not null && !label.EndsWith("…", StringComparison.Ordinal) ? " …" : "");
                list.Add(IsTelling(a.Icon)
                    ? IconIds.IsStep(a.Icon)
                        ? new BarButton("part:" + a.Id, ShortLabel(label), tip, Active: a.IsActive) { IconId = a.Icon, ShowLabel = true }
                        : new BarButton("part:" + a.Id, "", tip, Active: a.IsActive) { IconId = a.Icon }
                    : new BarButton("part:" + a.Id, ShortLabel(label), tip, Active: a.IsActive));
            }
            list.Add(new BarButton("", "", "", Separator: true));
            list.Add(MoreButton());
            return list;
        }

        /// <summary>The dropdown button of a choice: the current option's pictogram or picture (or a short text) and a caret.</summary>
        private BarButton ChoiceButton(BarChoice c)
        {
            var cur = c.CurrentOption;
            var tip = c.Tip + (cur is null ? " …" : ": " + cur.Label) + T(" – click to choose", " – klicken zum Wählen");
            return c.Pictorial
                ? new BarButton("choice:" + c.Key, "", tip, Active: _barChoice == c.Key) { IconId = c.BarIcon ?? cur?.IconId, Icon = c.BarIcon is null && cur?.IconId is null ? cur?.Icon : null, Menu = true, Mirror = c.Mirror }
                : new BarButton("choice:" + c.Key, ShortLabel(cur?.Label ?? c.Tip, 16), tip, Active: _barChoice == c.Key) { Menu = true };
        }

        /// <summary>
        /// Pie entries of a selected part: its commands and menus ("Fill to ▸") for the Edit place, its state choices ("Line
        /// style: Dashed ▸", opening the options with the current one marked) for the Style place.
        /// </summary>
        private (List<PieItem> Edit, List<PieItem> Style) PartPieItems()
        {
            var edit = new List<PieItem>();
            var style = new List<PieItem>();
            if (_part is null || PartOwner() is not { } o) return (edit, style);
            var choices = PartChoices(o);
            foreach (var e in ActionChoiceBar.Entries(PartActions(o)))
            {
                if (e.Choice is { } c)
                {
                    var bc = choices.FirstOrDefault(x => x.Key == ActionChoiceBar.Key(ActionChoiceBar.PartPrefix, c));
                    var cur = bc?.CurrentOption;
                    var item = new PieItem("menu:c:" + ActionChoiceBar.Key(ActionChoiceBar.PartPrefix, c), ActionChoiceBar.PieLabel(c, e.Options, _lang()))
                    {
                        IconId = cur?.IconId, Icon = cur?.IconId is null ? cur?.Icon : null,
                        Hint = T("Choose: ", "Wählen: ") + string.Join(", ", e.Options.Take(4).Select(x => x.Label.Get(_lang()))) + (e.Options.Count > 4 ? ", …" : ""),
                    };
                    (c.IsMenu ? edit : style).Add(item);
                    continue;
                }
                var a = e.Action!;
                edit.Add(new PieItem("part:" + a.Id, a.Label.Get(_lang()), a.IsEnabled, a.IsActive) { IconId = IsTelling(a.Icon) || a.Icon == IconIds.Delete ? a.Icon : null });
            }
            edit.Add(new PieItem("part:none", T("Whole element", "Ganzes Element")) { IconId = IconIds.SelectAssembly });
            return (edit, style);
        }

        /// <summary>Del with a part selected: the part's own delete (a point, a curve, an area …), else it is hidden – never the whole element.</summary>
        private void DeletePart()
        {
            if (_part is null || PartOwner() is not { } o || _unit is null || CurrentSlide() is not { } slide) return;
            if (ActionChoiceBar.DeleteActionId(PartActions(o)) is not { } id) { Log.Info("Del on part " + _part + ": nothing to delete"); return; }
            Log.Info("Del on part " + _part + ": " + id);
            if (SketchUnits.Read(_unit.Shape) is { } unit) PartCommand(slide, unit, "part:" + id);
        }

        /// <summary>The "Parts ▸" pie: every part of the element (hidden ones marked), choosing one selects it.</summary>
        private List<PieItem>? PartListMenu()
        {
            if (PartOwner() is not { } o) return null;
            var items = new List<PieItem> { new("menu:back", T("← Back", "← Zurück")) };
            foreach (var p in _registry().Parts(o, new RenderContext(_unit!.Doc, _lang())).Where(p => !p.Detail).Take(MaxSubItems))
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
                SelectPart(id.Substring(8));
                return;
            }
            var actionId = id.Substring(5);
            if (actionId == "none")
            {
                SelectPart(null);
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
                var input = action.Id == "chart.data" ? DataEditor.Ask(action.Label.Get(_lang()), action.Input.Value, owner)
                    : TextPrompt.AskInput(action.Input.Label.Get(_lang()), action.Input.Unit, action.Input.Value, owner);
                if (input is null) return;
                try { next = action.Input.Apply(o, input); }
                catch (FormatException ex) { TextPrompt.Warn(ex.Message, owner); return; }
            }
            else next = action.Apply(o);
            Log.Info("Part action " + actionId + " on " + _part);
            // A deleted part is gone: a deleted point leaves its curve selected, anything else the whole element.
            if (actionId == PartLooks.DeleteAction) _part = ActionChoiceBar.AfterDelete(_part);
            // A command that adds a part ("Fill below ▸ //", "Fill to ▸ curve 2"): the new part is selected, so its own
            // choices (pattern, colour …) are right there in the bar.
            else if (action.Choice?.IsMenu == true)
            {
                var ctx = new RenderContext(unit.Doc, _lang());
                var before = _registry().Parts(o, ctx).Where(p => !p.Detail).Select(p => p.Key).ToList();
                var after = _registry().Parts(next, ctx).Where(p => !p.Detail).Select(p => p.Key).ToList();
                if (ActionChoiceBar.Added(before, after) is { } added) _part = added;
            }
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
            // The drawn primitives of the part plus its part shapes (a data point: a small circle).
            var drawn = _registry().PartPrimitives(o, _part, new RenderContext(_unit!.Doc, _lang()));
            using var pen = new Pen(Color.FromArgb(200, 31, 95, 191), (float)(2.2 * view.DeviceScale));
            foreach (var p in drawn)
            {
                switch (p)
                {
                    case LinePrim l: g.DrawLine(pen, view.ToScreen(l.A), view.ToScreen(l.B)); break;
                    case EllipsePrim e:
                        {
                            var c = view.ToScreen(e.Center);
                            var rx = Math.Max(5 * view.DeviceScale, Math.Abs(view.ToScreen(e.Center + new Vec2(e.Rx, 0)).X - c.X)) + 2 * view.DeviceScale;
                            using var glow = new SolidBrush(Color.FromArgb(90, 31, 95, 191));
                            g.FillEllipse(glow, (float)(c.X - rx), (float)(c.Y - rx), (float)(2 * rx), (float)(2 * rx));
                            g.DrawEllipse(pen, (float)(c.X - rx), (float)(c.Y - rx), (float)(2 * rx), (float)(2 * rx));
                            break;
                        }
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
