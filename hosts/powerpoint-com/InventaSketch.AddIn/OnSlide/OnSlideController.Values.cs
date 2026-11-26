using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using InventaSketch.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Element values in PowerPoint (Calc 2, docs/en/design/engineering-calculations.md sections 1.1 and 1.5):
    /// <list type="bullet">
    /// <item>the value level of the presentation (<see cref="ValueLevelSetting"/>): Quick sketch shows nothing new;</item>
    /// <item>Explore and Engineer: a labelled "Values" button as the bar's main action for elements whose type has values;</item>
    /// <item>pie Edit ▸ Values ▸ (every level): one entry per variable with its value; choosing one opens the card on it,
    /// and "Show values on the drawing" switches the values label (part "values");</item>
    /// <item>the value card (<see cref="ValueCard"/>) at the element: entering runs setValue, × runs clearValue (one undo
    /// step each, the element redrawn), the grip drags a value live (<see cref="ValueDrag"/>);</item>
    /// <item>a small amber ⚠ badge on an element whose entered values conflict (overlay only, Explore and Engineer).</item>
    /// </list>
    /// The engine is used in-process (<see cref="ValueSolver"/>, <see cref="ValueEdits"/>), as the rest of the add-in does.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private (string SlideKey, int Tick, ValueLevel Level)? _levelCache;

        /// <summary>The value level of the presentation (cached briefly: the bar asks on every show).</summary>
        internal ValueLevel Level()
        {
            var slide = CurrentSlide();
            string key;
            try { key = slide is null ? "" : slide.SlideID.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            catch { key = ""; }
            if (_levelCache is { } c && c.SlideKey == key && unchecked(Environment.TickCount - c.Tick) < 1500) return c.Level;
            PowerPoint.Presentation? presentation = null;
            try { presentation = _app.ActivePresentation; } catch { /* no presentation */ }
            var level = ValueLevelSetting.Read(presentation, slide);
            _levelCache = (key, Environment.TickCount, level);
            return level;
        }

        /// <summary>The level changed (More menu): forget the cache and show the bar and overlay of the selection again.</summary>
        internal void LevelChanged()
        {
            _levelCache = null;
            _badgeDoc = null;
            if (_mode == Mode.Armed && _view is { } view && _unit is not null)
            {
                ShowBar(view);
                DrawPointHandles(view);
            }
        }

        /// <summary>The armed unit's object with values, or null.</summary>
        private SketchObject? ValueObject()
        {
            var registry = _registry();
            return _unit?.Objects.FirstOrDefault(o => ValueModel.HasValues(registry.Type(o.Type)));
        }

        /// <summary>The bar's main action in Explore and Engineer for an element with values ("Values", labelled).</summary>
        private BarButton? ValuesButtonFor(SketchObject first, ObjectType type)
        {
            if (!ValueModel.HasValues(type) || Level() == ValueLevel.QuickSketch) return null;
            return new BarButton("values", T("Values", "Werte"), T("Values: enter what you know, the rest follows", "Werte: Bekanntes eingeben, der Rest folgt")) { IconId = IconIds.Values, ShowLabel = true };
        }

        /// <summary>Pie Edit ▸ "Values ▸" for an element with values (at every level, so values can always be entered).</summary>
        private PieItem? ValuesPieItem(SketchObject first)
        {
            if (!ValueModel.HasValues(_registry().Type(first.Type))) return null;
            var model = CardModelFor(first.Id);
            var hint = model is null ? "" : string.Join(", ", model.Rows.Select(r => r.Symbol + (r.Si is null ? "" : " = " + r.Display)));
            return new PieItem("menu:values", T("Values ▸", "Werte ▸")) { IconId = IconIds.Values, Hint = hint };
        }

        /// <summary>The Values submenu: one entry per variable ("Voltage U … 230 V", "Power P = 460 W"), then the label switch.</summary>
        private List<PieItem>? ValuesMenu(PieItem back)
        {
            var o = ValueObject();
            if (o is null || CardModelFor(o.Id) is not { } model) return null;
            var items = new List<PieItem> { back };
            foreach (var r in model.Rows.Take(MaxSubItems - 1))
                items.Add(new PieItem("val:" + r.Key, ValueCardModel.PieLabel(r), Active: r.Provenance == Provenance.Entered)
                {
                    IconId = IconIds.Value,
                    Hint = r.Formula ?? (r.Provenance == Provenance.Entered ? T("Entered – opens the card to change it", "Eingegeben – öffnet die Karte zum Ändern")
                        : r.Si is null ? T("Type a value – opens the card", "Wert eingeben – öffnet die Karte") : ValueCardModel.MarkerTip(r.Provenance, r.Conflict, _lang())),
                });
            items.Add(new PieItem("vlabel", T("Show values on the drawing", "Werte auf der Zeichnung zeigen"), Active: model.ShowsLabel)
            {
                IconId = IconIds.LabelShow,
                Hint = T("A label with the values below the element; printed and exported", "Eine Beschriftung mit den Werten unter dem Element; wird gedruckt und exportiert"),
            });
            return items;
        }

        /// <summary>Value commands of the bar and the pie: "values" (the card), "val:&lt;key&gt;" (the card on that value), "vlabel". False if not one.</summary>
        private bool ValueCommand(SketchUnit stale, string id)
        {
            if (id != "values" && id != "vlabel" && !id.StartsWith("val:", StringComparison.Ordinal)) return false;
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            var registry = _registry();
            var o = unit?.Objects.FirstOrDefault(x => ValueModel.HasValues(registry.Type(x.Type)));
            if (slide is null || unit is null || o is null) return true;
            if (id == "vlabel")
            {
                EditValues(o.Id, x => ValueLabels.WithShown(x, !ValueLabels.Shows(x)), "values label");
                return true;
            }
            OpenValueCard(o.Id, id.StartsWith("val:", StringComparison.Ordinal) ? id.Substring(4) : null);
            return true;
        }

        // ------------------------------------------------------------------ card

        /// <summary>The unit of the current slide that holds <paramref name="objectId"/>.</summary>
        private static SketchUnit? UnitHolding(PowerPoint.Slide slide, string objectId)
            => SketchUnits.All(slide).FirstOrDefault(u => u.Doc.Get(objectId) is not null);

        /// <summary>The card content of an object as stored now (or with a drag preview).</summary>
        private ValueCardModel? CardModelFor(string objectId, ValueTable? table = null)
        {
            var slide = CurrentSlide();
            var unit = _unit?.Doc.Get(objectId) is not null ? _unit : slide is null ? null : UnitHolding(slide, objectId);
            var o = unit?.Doc.Get(objectId);
            if (unit is null || o is null) return null;
            var registry = _registry();
            var lang = _lang();
            table ??= ValueSolver.Solve(ValueDoc(unit), registry, new[] { objectId }, null, lang);
            var name = registry.Type(o.Type)?.Name.Get(lang) ?? o.Type;
            var designator = o.Str("ref", "").Trim();
            var title = name + (designator.Length > 0 ? " " + designator : "") + " · " + T("Values", "Werte");
            return ValueCardModel.Build(table, objectId, title, Level(), lang, ValueLabels.Shows(o));
        }

        /// <summary>Applies <paramref name="change"/> to the object as one undo step (redrawn with its dependents); returns the new card content.</summary>
        private ValueCardModel? EditValues(string objectId, Func<SketchObject, SketchObject> change, string what)
        {
            var slide = CurrentSlide();
            if (slide is null || UnitHolding(slide, objectId) is not { } unit || unit.Doc.Get(objectId) is not { } o) return null;
            var next = change(o);
            if (!ReferenceEquals(next, o) && !SameParams(o, next))
            {
                _app.StartNewUndoEntry();
                var doc = Edits.Update(unit.Doc, new[] { objectId }, _ => next);
                ReplaceWithDependents(slide, unit, doc);
                _badgeDoc = null;
                if (SketchUnits.Read(unit.Shape) is { } fresh) _unit = fresh;
                else if (UnitHolding(slide, objectId) is { } again) _unit = again;
                Log.Info("Values: " + what + " on " + o.Type);
            }
            return CardModelFor(objectId);
        }

        private static bool SameParams(SketchObject a, SketchObject b)
            => a.Params.Count == b.Params.Count && a.Params.All(kv => b.Params.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));

        /// <summary>Opens the value card at the element, focused on <paramref name="focusKey"/>.</summary>
        private void OpenValueCard(string objectId, string? focusKey)
        {
            var model = CardModelFor(objectId);
            if (model is null) return;
            var lang = _lang();
            var registry = _registry();
            var at = CardPosition();
            HideBar();
            var actions = new ValueCardActions
            {
                Enter = (key, text) =>
                {
                    try
                    {
                        return (EditValues(objectId, o => ValueEdits.Set(registry, o, key, text, lang), "set " + key), null);
                    }
                    catch (FormatException ex)
                    {
                        return (null, ex.Message);
                    }
                    catch (ArgumentException ex)
                    {
                        return (null, ex.Message);
                    }
                },
                Clear = key => EditValues(objectId, o => ValueEdits.Clear(registry, o, key), "clear " + key),
                ShowLabel = on => EditValues(objectId, o => ValueLabels.WithShown(o, on), on ? "label on" : "label off"),
                Preview = (key, si) =>
                {
                    if (double.IsNaN(si)) return CardModelFor(objectId);
                    var slide = CurrentSlide();
                    if (slide is null || UnitHolding(slide, objectId) is not { } unit) return null;
                    return CardModelFor(objectId, ValueDrag.Plan(ValueDoc(unit), registry, objectId, key, si, lang).Table);
                },
                Commit = (key, si) =>
                {
                    var slide = CurrentSlide();
                    if (slide is null || UnitHolding(slide, objectId) is not { } unit) return null;
                    var plan = ValueDrag.Plan(ValueDoc(unit), registry, objectId, key, si, lang);
                    return EditValues(objectId, o => ValueDrag.Apply(registry, o, key, si, plan.Released, lang, ValueLevelSetting.Digits(Level())), "drag " + key);
                },
            };
            Log.Info("Value card: " + model.Title + (focusKey is null ? "" : " [" + focusKey + "]"));
            var calculator = ValueCard.ShowCard(model, focusKey, at, _view?.MainWindow ?? IntPtr.Zero, lang, actions);
            if (calculator == ValueCard.SheetCommand) ShowCalcSheet(new[] { objectId });
            else if (calculator is not null) CalculatorDialog.ShowCalculator(lang, calculator);
            if (_mode == Mode.Armed && _view is { } view) { ShowBar(view); DrawPointHandles(view); }
        }

        /// <summary>
        /// The calculation sheet (Calc 6) of <paramref name="objectIds"/>, else of the selected elements, else of the whole
        /// slide: solved with every unit of the slide as context; "Insert as table" puts it beside the selection as an
        /// annotation table (one undo step).
        /// </summary>
        internal void ShowCalcSheet(IReadOnlyList<string>? objectIds = null)
        {
            try
            {
                if (ComputeCalcSheet(objectIds) is not { } sheet) return;
                CalcSheetDialog.Show(_lang(), sheet.Rows, sheet.Scope, sheet.Rows.Count == 0 ? null : cells => InsertCalcTable(cells, sheet.Anchor), null);
            }
            catch (Exception ex)
            {
                Log.Error("Calculation sheet", ex);
            }
        }

        /// <summary>
        /// Automation (UI tests): the calculation sheet of the selection or slide as text in <paramref name="mode"/> (csv,
        /// markdown, latex, table); with <paramref name="insert"/> it is also inserted as a table.
        /// </summary>
        internal string CalcSheetText(string mode, bool insert) => OnUi(() =>
        {
            if (ComputeCalcSheet(null) is not { } sheet) return "";
            var lang = _lang();
            if (insert && sheet.Rows.Count > 0) InsertCalcTable(CalcSheet.TableCells(sheet.Rows, lang), sheet.Anchor);
            return mode switch
            {
                "markdown" => CalcSheet.Markdown(sheet.Rows, lang),
                "latex" => CalcSheet.Latex(sheet.Rows, lang),
                "table" => CalcSheet.TableCells(sheet.Rows, lang),
                _ => CalcSheet.Csv(sheet.Rows, lang),
            };
        });

        private (IReadOnlyList<CalcSheetRow> Rows, string Scope, System.Drawing.RectangleF Anchor)? ComputeCalcSheet(IReadOnlyList<string>? objectIds)
        {
            {
                var slide = CurrentSlide();
                if (slide is null) return null;
                var registry = _registry();
                var lang = _lang();
                var units = SketchUnits.All(slide);
                var doc = SketchUnits.Combined(units);
                var selected = new List<SketchUnit>();
                try
                {
                    var sel = _app.ActiveWindow.Selection;
                    if (sel.Type == PowerPoint.PpSelectionType.ppSelectionShapes)
                        for (var i = 1; i <= sel.ShapeRange.Count; i++)
                            if (SketchUnits.IsUnit(sel.ShapeRange[i]) && SketchUnits.Read(sel.ShapeRange[i]) is { } u) selected.Add(u);
                }
                catch (Exception) { /* no window selection */ }
                var ids = objectIds?.ToList()
                    ?? (selected.Count > 0 ? selected.SelectMany(u => u.Objects.Select(o => o.Id)).ToList() : doc.Pages[0].Order.ToList());
                var scope = objectIds is not null || selected.Count > 0 ? T("selection", "Auswahl") : T("slide", "Folie");
                var table = ValueSolver.Solve(doc, registry, ids, new ValueSolveOptions { BudgetMs = 2000 }, lang);
                var rows = CalcSheet.Rows(table, registry, ids, lang, ValueLevelSetting.Digits(Level()));
                Log.Info("Calculation sheet: " + rows.Count + " rows (" + scope + ")");
                var anchor = (selected.Count > 0 ? selected : units).Select(u => u.BoundsPt).DefaultIfEmpty(new System.Drawing.RectangleF(50, 50, 0, 0))
                    .Aggregate(System.Drawing.RectangleF.Union);
                return (rows, scope, anchor);
            }
        }

        /// <summary>The sheet as an annotation table right of <paramref name="anchorPt"/> (or below it near the slide edge).</summary>
        private void InsertCalcTable(string cells, System.Drawing.RectangleF anchorPt)
        {
            var slide = CurrentSlide();
            var registry = _registry();
            if (slide is null || registry.Item("ann.table") is not { } item) return;
            var lines = cells.Split('\n').Length;
            var size = new Size2(150, Math.Max(12, 6.5 * lines));
            var setup = _app.ActivePresentation.PageSetup;
            var slideW = setup.SlideWidth / Geo.PtPerMm;
            var x = anchorPt.Right / Geo.PtPerMm + 8 + size.W / 2;
            var y = anchorPt.Top / Geo.PtPerMm + size.H / 2;
            if (x + size.W / 2 > slideW) { x = Math.Max(size.W / 2 + 5, anchorPt.Left / Geo.PtPerMm + size.W / 2); y = anchorPt.Bottom / Geo.PtPerMm + 8 + size.H / 2; }
            var o = registry.Create(item, new Vec2(x, y)).WithParam("cells", cells) with { Size = size };
            _app.StartNewUndoEntry();
            SketchUnits.Draw(slide, registry, SingleUnitDoc(o), _lang());
            Log.Info("Calculation sheet inserted as a table: " + (lines - 1) + " rows");
        }

        /// <summary>Where the card opens: right of the selected element, at its top (screen pixels).</summary>
        private Point CardPosition()
        {
            if (_view is { } view && _unit is not null)
            {
                var r = UnitScreenRect(view);
                var gap = (float)(14 * view.DeviceScale);
                return new Point((int)(r.Right + gap), (int)r.Top);
            }
            GetCursorPos(out var p);
            return new Point(p.X, p.Y);
        }

        // ------------------------------------------------------------------ conflict badge (overlay only)

        private SketchDocument? _badgeDoc;
        private bool _badgeConflict;

        /// <summary>
        /// The model the values are solved in: the unit with the other units of the slide as context. Gear trains, circuits
        /// and drive chains span several units, and the Calc 3 links read the neighbours; entered values stay in the unit.
        /// </summary>
        private SketchDocument ValueDoc(SketchUnit unit)
        {
            try
            {
                var slide = CurrentSlide();
                return slide is null ? unit.Doc : SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
            }
            catch (Exception ex)
            {
                Log.Error("Value context", ex);
                return unit.Doc;
            }
        }

        /// <summary>The armed element's entered values conflict and the level shows values.</summary>
        private bool ValueBadgeWanted()
        {
            if (_unit is null || _mode != Mode.Armed) return false;
            if (!ReferenceEquals(_badgeDoc, _unit.Doc))
            {
                _badgeDoc = _unit.Doc;
                _badgeConflict = false;
                try
                {
                    var o = ValueObject();
                    if (o is not null && Level() != ValueLevel.QuickSketch)
                        _badgeConflict = ValueSolver.Solve(ValueDoc(_unit), _registry(), new[] { o.Id }, null, _lang()).Status(o.Id) == ValueStatus.Conflict;
                }
                catch (Exception ex)
                {
                    Log.Error("Value badge", ex);
                }
            }
            return _badgeConflict;
        }

        /// <summary>The badge's centre in screen pixels: just outside the top right corner of the element.</summary>
        private PointF BadgeCentre(SlideView view)
        {
            var r = UnitScreenRect(view);
            var off = (float)(6 * view.DeviceScale);
            return new PointF(r.Right + off, r.Top - off);
        }

        /// <summary>A small amber triangle with "!" (screen coordinates; the graphics is translated to the pane).</summary>
        private void DrawValueBadge(Graphics g, SlideView view)
        {
            if (!ValueBadgeWanted()) return;
            var c = BadgeCentre(view);
            var s = (float)view.DeviceScale;
            var h = 11 * s;
            var tri = new[] { new PointF(c.X, c.Y - h / 2), new PointF(c.X + h * 0.58f, c.Y + h / 2), new PointF(c.X - h * 0.58f, c.Y + h / 2) };
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(Color.FromArgb(240, 168, 0)))
            using (var rim = new Pen(Color.White, 1.2f * s))
            {
                g.FillPolygon(fill, tri);
                g.DrawPolygon(rim, tri);
            }
            using (var ink = new Pen(Color.FromArgb(60, 40, 0), 1.4f * s))
            {
                g.DrawLine(ink, c.X, c.Y - h * 0.18f, c.X, c.Y + h * 0.18f);
                g.DrawLine(ink, c.X, c.Y + h * 0.3f, c.X, c.Y + h * 0.36f);
            }
            g.SmoothingMode = smoothing;
            LastValueBadge = "conflict@" + Math.Round(c.X) + "," + Math.Round(c.Y);
        }

        /// <summary>The badge as last drawn ("conflict@x,y"), "" when none (UI tests).</summary>
        internal string LastValueBadge { get; private set; } = "";

        // ------------------------------------------------------------------ automation (UI tests)

        private SketchObject? ValueObjectOf(PowerPoint.Slide slide, string shapeName, out SketchUnit? unit)
        {
            unit = SketchUnits.Read(slide.Shapes[shapeName]);
            var registry = _registry();
            return unit?.Objects.FirstOrDefault(o => ValueModel.HasValues(registry.Type(o.Type)));
        }

        /// <summary>The value card of a shape's element as text (<see cref="ValueCardModel.Describe"/>); "" without values.</summary>
        internal string ValuesOf(string shapeName) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null || ValueObjectOf(slide, shapeName, out _) is not { } o) return "";
            return CardModelFor(o.Id)?.Describe() ?? "";
        });

        /// <summary>Enters a value as the card does (one undo step); "" on success, else the message.</summary>
        internal string SetValueOf(string shapeName, string key, string text) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null || ValueObjectOf(slide, shapeName, out _) is not { } o) return "no element with values";
            try
            {
                EditValues(o.Id, x => ValueEdits.Set(_registry(), x, key, text, _lang()), "set " + key);
                return "";
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                return ex.Message;
            }
        });

        /// <summary>Clears an entered value as the card's × does (one undo step).</summary>
        internal bool ClearValueOf(string shapeName, string key) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null || ValueObjectOf(slide, shapeName, out _) is not { } o) return false;
            EditValues(o.Id, x => ValueEdits.Clear(_registry(), x, key), "clear " + key);
            return true;
        });

        /// <summary>Switches the values label of a shape's element (one undo step).</summary>
        internal bool ShowValuesOf(string shapeName, bool on) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null || ValueObjectOf(slide, shapeName, out _) is not { } o) return false;
            EditValues(o.Id, x => ValueLabels.WithShown(x, on), on ? "label on" : "label off");
            return true;
        });

        /// <summary>Drags a value as the card's grip does: preview=true returns the card text while held, false stores it.</summary>
        internal string DragValueOf(string shapeName, string key, double si, bool preview) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null || ValueObjectOf(slide, shapeName, out var unit) is not { } o || unit is null) return "";
            var registry = _registry();
            var lang = _lang();
            var plan = ValueDrag.Plan(ValueDoc(unit), registry, o.Id, key, si, lang);
            if (preview) return CardModelFor(o.Id, plan.Table)?.Describe() ?? "";
            return EditValues(o.Id, x => ValueDrag.Apply(registry, x, key, si, plan.Released, lang, ValueLevelSetting.Digits(Level())), "drag " + key)?.Describe() ?? "";
        });

        /// <summary>The conflict badge of the selected element: "conflict" while it would be drawn, else "".</summary>
        internal string ValueBadgeState() => OnUi(() => ValueBadgeWanted() ? "conflict" : "");

        /// <summary>True while the value card is open; closing it with <paramref name="close"/>.</summary>
        internal bool ValueCardOpen(bool close) => OnUi(() =>
        {
            var open = ValueCard.IsOpen;
            if (close) ValueCard.CloseOpen();
            return open;
        });
    }
}
