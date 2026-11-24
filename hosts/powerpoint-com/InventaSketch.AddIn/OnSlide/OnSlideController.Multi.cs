using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using static InventaSketch.AddIn.OnSlide.NativeMethods;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Several InventaSketch elements selected: a small bar with the selection-wide operations that change something
    /// (<see cref="Arrange"/>): one view for all, centres on a row or column, even spacing, numerals renumbered in reading
    /// order. PowerPoint's own formatting stays available for the native side of the shapes.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private List<SketchUnit> _multi = new();

        private void ArmMulti(List<SketchUnit> units)
        {
            var view = SlideView.Current(_app);
            if (view is null || units.Count < 2) { Disarm(); return; }
            _view = view;
            _unit = null;
            _multi = units;
            _barChoice = null;
            _dark = OfficeTheme.IsDark();
            _mode = Mode.Multi;
            _hooks.Install();
            _timer.Start();
            _overlay.Clear();
            ShowMultiBar(view);
            Log.Info("Armed multi-selection: " + units.Count + " units");
        }

        private static Rect MmRect(SketchUnit u) => new(u.BoundsPt.X / Geo.PtPerMm, u.BoundsPt.Y / Geo.PtPerMm, u.BoundsPt.Width / Geo.PtPerMm, u.BoundsPt.Height / Geo.PtPerMm);

        private List<BarButton> MultiButtons()
        {
            var registry = _registry();
            var list = new List<BarButton>();
            var firsts = _multi.Select(u => u.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null)).ToList();
            if (firsts.All(o => o is not null))
            {
                var views = Arrange.CommonViews(registry, firsts!);
                foreach (var v in views.Take(6))
                    list.Add(new BarButton("mview:" + v.Id, v.Label.Get(_lang()), T("All in view: ", "Alle in Ansicht: ") + v.Label.Get(_lang())) { IconId = IconPainter.ForView(v) });
                if (views.Count > 0) list.Add(new BarButton("", "", "", Separator: true));
            }
            var boxes = _multi.Select(MmRect).ToList();
            if (Arrange.Changes(Arrange.AlignCentres(boxes, row: true)))
                list.Add(new BarButton("align:row", "", T("Centres on one horizontal line", "Mitten auf eine waagrechte Linie")) { IconId = IconIds.AlignRow });
            if (Arrange.Changes(Arrange.AlignCentres(boxes, row: false)))
                list.Add(new BarButton("align:column", "", T("Centres on one vertical line", "Mitten auf eine senkrechte Linie")) { IconId = IconIds.AlignColumn });
            if (Arrange.Changes(Arrange.Distribute(boxes, horizontal: true)))
                list.Add(new BarButton("dist:h", "", T("Space evenly left to right", "Gleichmäßig von links nach rechts verteilen")) { IconId = IconIds.DistributeH });
            if (Arrange.Changes(Arrange.Distribute(boxes, horizontal: false)))
                list.Add(new BarButton("dist:v", "", T("Space evenly top to bottom", "Gleichmäßig von oben nach unten verteilen")) { IconId = IconIds.DistributeV });
            // Two parts: a dimension between their axes (or centres), docked on both so it follows them.
            if (_multi.Count == 2 && firsts.All(o => o is not null && !o.IsPointGeometry))
                list.Add(new BarButton("dim", "", T("Dimension between the two (centre distance)", "Maß zwischen beiden (Achsabstand)")) { IconId = IconIds.Dimension });
            // Format painter: the first selected element's view, parameters and style for the others of its type.
            if (firsts[0] is { } source && firsts.Count > 1)
            {
                var ctx = new RenderContext(_multi[0].Doc, _lang());
                if (Arrange.MatchFormat(registry, source, _multi.Skip(1).SelectMany(u => u.Objects), ctx).Count > 0)
                    list.Add(new BarButton("match", "", T("Like the first: view, parameters and style", "Wie das erste: Ansicht, Parameter und Stil")) { IconId = IconIds.MatchFormat });
            }
            if (Numerals().Count >= 2 && Arrange.Renumber(Numerals()).Count > 0)
                list.Add(new BarButton("renumber", "", T("Renumber reference numerals in reading order", "Bezugszeichen in Lesereihenfolge neu nummerieren")) { IconId = IconIds.Renumber });
            while (list.Count > 0 && list[list.Count - 1].Separator) list.RemoveAt(list.Count - 1);
            return list;
        }

        /// <summary>Reference numerals of the selection with their numeral position (point 1) and current numeral.</summary>
        private List<(string Id, Vec2 At, string Numeral)> Numerals()
        {
            var list = new List<(string, Vec2, string)>();
            foreach (var u in _multi)
                foreach (var o in u.Objects.Where(o => o.Type == "patent.numeral" && o.Points is { Count: >= 2 }))
                {
                    var numeral = !string.IsNullOrEmpty(o.Ref) ? o.Ref! : !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("numeral", "10");
                    list.Add((u.Id + "/" + o.Id, u.Doc.ResolvePoints(o)[1], numeral));
                }
            return list;
        }

        private void ShowMultiBar(SlideView view)
        {
            if (_multi.Count < 2 || !AddInSettings.Bar) { HideBar(); return; }
            var buttons = MultiButtons();
            if (buttons.Count == 0) { HideBar(); return; }
            if (_bar is null || _fade.Dismissed) _fade.Reset();
            _bar = new FloatingBar(buttons, (float)view.DeviceScale);
            var rects = _multi.Select(u =>
            {
                var a = view.ToScreen(new Vec2(u.BoundsPt.X / Geo.PtPerMm, u.BoundsPt.Y / Geo.PtPerMm));
                var c = view.ToScreen(new Vec2(u.BoundsPt.Right / Geo.PtPerMm, u.BoundsPt.Bottom / Geo.PtPerMm));
                return RectangleF.FromLTRB(a.X, a.Y, c.X, c.Y);
            }).ToList();
            var frame = rects.Aggregate(RectangleF.Union);
            var slide = CurrentSlide();
            var selected = new HashSet<string>(_multi.Select(u => u.Id));
            var others = slide is null ? new List<RectangleF>() : SketchUnits.All(slide).Where(u => !selected.Contains(u.Id)).Select(u =>
            {
                var a = view.ToScreen(new Vec2(u.BoundsPt.X / Geo.PtPerMm, u.BoundsPt.Y / Geo.PtPerMm));
                var c = view.ToScreen(new Vec2(u.BoundsPt.Right / Geo.PtPerMm, u.BoundsPt.Bottom / Geo.PtPerMm));
                return RectangleF.FromLTRB(Math.Min(a.X, c.X), Math.Min(a.Y, c.Y), Math.Max(a.X, c.X), Math.Max(a.Y, c.Y));
            }).ToList();
            _bar.Place(frame, view.PaneRect, others);
            GetCursorPos(out var p);
            var opacity = _fade.Update(_bar.Distance(new Point(p.X, p.Y)));
            Log.Info($"Bar shown at {_bar.Bounds.X},{_bar.Bounds.Y},{_bar.Bounds.Width},{_bar.Bounds.Height} opacity {opacity:0.##}");
            Log.Info("Bar ids: " + string.Join(",", _bar.Buttons.Where(b => !b.Separator).Select(b => b.Id)));
            DrawBar(view, opacity);
        }

        /// <summary>Mouse in multi-selection mode: the bar handles its clicks; any other press hides it (native drag, new selection).</summary>
        private bool MultiMouse(int message, POINT pt, SlideView view)
        {
            if (HandleBar(message, pt, view, out var swallow)) return swallow;
            if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN) HideBar();
            return false;
        }

        private void ApplyMulti(string id)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var units = _multi.Select(u => SketchUnits.Read(u.Shape)).Where(u => u is not null).Select(u => u!).ToList();
            if (units.Count < 2) return;
            var registry = _registry();
            var lang = _lang();
            _app.StartNewUndoEntry();
            var shapes = new List<PowerPoint.Shape>();
            if (id.StartsWith("align:", StringComparison.Ordinal) || id.StartsWith("dist:", StringComparison.Ordinal))
            {
                var boxes = units.Select(MmRect).ToList();
                var moves = id switch
                {
                    "align:row" => Arrange.AlignCentres(boxes, row: true),
                    "align:column" => Arrange.AlignCentres(boxes, row: false),
                    "dist:h" => Arrange.Distribute(boxes, horizontal: true),
                    _ => Arrange.Distribute(boxes, horizontal: false),
                };
                // Moved like a user drag: followers and couplings update as after a native move.
                for (var i = 0; i < units.Count; i++)
                {
                    var s = units[i].Shape;
                    s.Left += (float)(moves[i].X * Geo.PtPerMm);
                    s.Top += (float)(moves[i].Y * Geo.PtPerMm);
                    shapes.Add(s);
                }
            }
            else if (id.StartsWith("mview:", StringComparison.Ordinal))
            {
                // The whole selection as one assembly: parts on one shaft, meshes and centre distances stay the same
                // (AssemblyViews), e.g. a gearbox from its stick diagram to the view along the shafts.
                var view = id.Substring(6);
                var all = SketchDocument.Create();
                all = Edits.Add(all, all.Pages[0].Id, units.SelectMany(u => u.Objects).ToArray());
                var switched = AssemblyViews.Switch(registry, all, units.SelectMany(u => u.Objects).Select(o => o.Id).ToList(), view, lang);
                _noCouple = true;
                try
                {
                    foreach (var u in units)
                    {
                        var doc = Edits.Update(u.Doc, u.Doc.Objects.Keys.ToList(), o => switched.Get(o.Id) ?? o);
                        if (SketchUnits.Replace(slide, u, doc, registry, lang) is { } s) shapes.Add(s);
                    }
                }
                finally
                {
                    _noCouple = false;
                }
            }
            else if (id == "dim")
            {
                var objs = units.Select(u => u.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null)).ToList();
                var item = registry.Item("ann.dimension.aligned");
                if (objs.Count == 2 && objs[0] is { } o1 && objs[1] is { } o2 && item is not null)
                {
                    var ctx = new RenderContext(SketchUnits.WithContext(units[0].Doc, units.Skip(1)), lang);
                    var a1 = Arrange.MeasurePoint(registry, o1, ctx);
                    var a2 = Arrange.MeasurePoint(registry, o2, ctx);
                    var dim = registry.CreateBetween(item, a1.P, a2.P);
                    dim = dim with { Attach = new[] { Attachments.For(o1, a1.P, a1), Attachments.For(o2, a2.P, a2) } };
                    var setup = _app.ActivePresentation.PageSetup;
                    var dimDoc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
                    dimDoc = Edits.Add(dimDoc, dimDoc.Pages[0].Id, dim);
                    SketchUnits.Draw(slide, registry, dimDoc, lang);
                }
                shapes.AddRange(units.Select(u => u.Shape));
            }
            else if (id == "match")
            {
                var source = units[0].Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
                shapes.Add(units[0].Shape);
                foreach (var u in units.Skip(1))
                {
                    var ctx = new RenderContext(u.Doc, lang);
                    var changed = source is null ? new List<SketchObject>() : Arrange.MatchFormat(registry, source, u.Objects, ctx).ToList();
                    if (changed.Count == 0) { shapes.Add(u.Shape); continue; }
                    var doc = Edits.Update(u.Doc, changed.Select(o => o.Id).ToList(), o => changed.First(x => x.Id == o.Id));
                    if (SketchUnits.Replace(slide, u, doc, registry, lang) is { } s) shapes.Add(s);
                }
            }
            else if (id == "renumber")
            {
                var changes = Arrange.Renumber(Numerals());
                foreach (var u in units)
                {
                    var mine = u.Objects.Where(o => changes.ContainsKey(u.Id + "/" + o.Id)).Select(o => o.Id).ToList();
                    if (mine.Count == 0) { shapes.Add(u.Shape); continue; }
                    var doc = Edits.Update(u.Doc, mine, o =>
                    {
                        var n = changes[u.Id + "/" + o.Id];
                        return !string.IsNullOrEmpty(o.Ref) ? o with { Ref = n } : !string.IsNullOrEmpty(o.Text) ? o with { Text = n } : o.WithParam("numeral", n);
                    });
                    if (SketchUnits.Replace(slide, u, doc, registry, lang) is { } s) shapes.Add(s);
                }
            }
            Log.Info("Multi: " + id + " on " + units.Count + " units");
            // Keep the selection: the bar shows what is still useful.
            for (var i = 0; i < shapes.Count; i++) shapes[i].Select(i == 0 ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse);
        }
    }
}
