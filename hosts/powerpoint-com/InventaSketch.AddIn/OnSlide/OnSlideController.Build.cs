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
    /// Build handles for assembling gearboxes and drivetrains (<see cref="Building"/>): a selected gear, ring gear, shaft,
    /// e-machine or engine shows a blue "+" at every free build port: the four sides of a gear (meshing gear), inside a
    /// ring gear (planet), the free ends of an axis (next shaft section, coupling, bearing, e-machine, wheel …), a free
    /// hub (shaft), a free connection port or line end. A click on a "+" opens the insert box at the "+" with the parts that
    /// fit there first; choosing one docks it there, coupled and selected. Dragging the "+" at a shaft end stretches the
    /// shaft (the parts on it keep their place); dragging the "+" at a free line end draws the line on. Positions come from
    /// the shared handle layout (<see cref="HandleLayout"/>).
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private const double PortOffsetPx = 15, PortPx = 6.5;

        private List<BuildPort> _ports = new();
        private string? _portOwner;
        private BuildPort? _portPressed;
        private POINT _portDown;
        private bool _portDragging;
        private Vec2 _portTo;
        private int _hoverPort2 = -1;
        /// <summary>Set while an edit must not couple (stretching a shaft: its parts keep their place).</summary>
        private bool _noCouple;

        private void PreparePorts(SketchUnit unit)
        {
            _ports = new List<BuildPort>();
            _hoverPort2 = -1;
            var registry = _registry();
            // Boxes, links of linkages and connection lines (cables, pipes, ducts: their free ends continue, 2026-11-25).
            var first = unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (first is null || unit.Objects.Count() != 1) { _portOwner = null; return; }
            _portOwner = first.Id;
            try
            {
                var full = SketchUnits.WithContext(unit.Doc, _otherUnits);
                _ports = Building.Ports(registry, full, first.Id, _lang()).ToList();
            }
            catch (Exception ex)
            {
                Log.Error("Build ports", ex);
            }
        }

        /// <summary>
        /// Screen position of a port's "+" from the shared handle layout (<see cref="Handles"/>): a little beyond the port
        /// along its direction, clear of PowerPoint's handles, the point diamonds and every other handle. A free hub's "+"
        /// sits outside the unit, beyond its top right corner, so the element can still be dragged by its centre.
        /// </summary>
        private PointF PortScreen(SlideView view, BuildPort p)
        {
            var i = _ports.IndexOf(p);
            if (i >= 0 && HandleById(view, "port:" + i) is { } h) return h.At;
            var s = view.ToScreen(p.At);
            var off = (float)(PortOffsetPx * view.DeviceScale);
            return new PointF(s.X + (float)p.Dir.X * off, s.Y + (float)p.Dir.Y * off);
        }

        private int PortAt2(POINT pt, SlideView view)
        {
            if (_ports.Count == 0) return -1;
            var i = NearestIndex(view, pt, "port:");
            return i < _ports.Count ? i : -1;
        }

        private void DrawPorts(Graphics g, SlideView view)
        {
            if (_ports.Count == 0) return;
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var lead = new Pen(Color.FromArgb(120, FlowBlue), 1f * s) { DashPattern = new[] { 2f, 2f } };
            for (var i = 0; i < _ports.Count; i++)
            {
                var p = _ports[i];
                var c = PortScreen(view, p);
                var hover = i == _hoverPort2;
                // While hovered, a thin dashed lead shows where the "+" belongs (the layout may have pushed it out of the way).
                if (hover) g.DrawLine(lead, view.ToScreen(p.At), c);
                // Progressive disclosure: the primary "+" always, the others as the pointer comes near (or with Shift).
                var h = HandleById(view, "port:" + i);
                var alpha = hover || h is null ? 1 : Reveal(view, h);
                Faded(g, c, alpha, AddHalf(view), (gg, cc) => DrawAddHandle(gg, view, cc, hover));
            }
            if (_hoverPort2 >= 0 && _hoverPort2 < _ports.Count)
                QueueTip(view, PortScreen(view, _ports[_hoverPort2]), PortTip(_ports[_hoverPort2]), (float)PortPx);
        }

        private string PortTip(BuildPort p) => p.Kind switch
        {
            PortKind.Mesh => T("Add a meshing gear here", "Gegenrad hier anfügen"),
            PortKind.InnerMesh => T("Add a planet here", "Planet hier einsetzen"),
            PortKind.Hub => T("Add a shaft through the hub", "Welle durch die Nabe"),
            PortKind.Joint => T("Add the next link, a fixed pivot or a slider here", "Nächstes Glied, Gestelldrehpunkt oder Schieber anfügen"),
            PortKind.Pin => T("Add a planet on this pin", "Planet auf diesen Bolzen setzen"),
            PortKind.Bevel => T("Add a mating bevel gear here", "Gegen-Kegelrad hier anfügen"),
            PortKind.Mount => T("Add a tool here, facing along the flange", "Werkzeug hier anbauen, ausgerichtet am Flansch"),
            PortKind.Connect => T("Connect a line or the next element here", "Leitung oder nächstes Element hier anschließen"),
            PortKind.LineEnd => EndOfPort(p) is not null
                ? T("Continue here: click for a fitting element or line · drag to draw the line on", "Hier weiterbauen: Klick für passendes Element oder Leitung · ziehen zeichnet die Leitung weiter")
                : T("Continue the line or connect an element", "Leitung fortsetzen oder Element anschließen"),
            _ => StretchablePort(p)
                ? T("Add at the shaft end (click) · lengthen the shaft (drag)", "Am Wellenende anfügen (Klick) · Welle verlängern (ziehen)")
                : T("Add at the shaft end", "Am Wellenende anfügen"),
        };

        private bool StretchablePort(BuildPort p)
        {
            if (_unit is null || _portOwner is null || _unit.Doc.Get(_portOwner) is not { } o) return false;
            return Building.CanStretch(_registry(), _unit.Doc, o, p, _lang());
        }

        /// <summary>Build ports while armed: hover feedback and the start of a click or a stretch drag.</summary>
        private bool PortMouse(int message, POINT pt, SlideView view, out bool swallow)
        {
            swallow = false;
            if (_ports.Count == 0 || _unit is null) return false;
            if (message == WM_MOUSEMOVE)
            {
                var i = PortAt2(pt, view);
                if (i != _hoverPort2) { _hoverPort2 = i; DrawPointHandles(view); }
                return false;
            }
            if (message != WM_LBUTTONDOWN) return false;
            var hit = PortAt2(pt, view);
            if (hit < 0) return false;
            HideBar();
            _portPressed = _ports[hit];
            _portDown = pt;
            _portDragging = false;
            _portTo = _portPressed.At;
            _mode = Mode.Port;
            swallow = true;
            return true;
        }

        private bool PortDragMouse(int message, POINT pt, SlideView view)
        {
            var port = _portPressed;
            if (port is null) { _mode = Mode.Armed; return true; }
            if (message == WM_MOUSEMOVE)
            {
                if (!_portDragging && Math.Abs(pt.X - _portDown.X) + Math.Abs(pt.Y - _portDown.Y) < 5 * view.DeviceScale) return true;
                // One "+" per free line end: dragging it draws the line on (the click opens what fits there).
                if (!_portDragging && EndOfPort(port) is bool atStart)
                {
                    _portPressed = null;
                    BeginDraw(atStart, _portDown);
                    return DrawMouse(message, pt, view);
                }
                if (!StretchablePort(port)) return true;
                _portDragging = true;
                var raw = view.ToMm(pt);
                // Snap the new end to points along the axis direction (other shaft ends, hubs …).
                var snap = NoSnap() ? SnapResult.None(Vec2.Zero) : Snapper.SnapPoint(raw, _targets, Options(view));
                _portTo = raw + snap.Delta;
                var registry = _registry();
                var doc = Building.StretchEnd(registry, _unit!.Doc, _portOwner!, port, _portTo, _lang());
                var prims = registry.Render(doc.Get(_portOwner!)!, new RenderContext(doc, _lang()));
                Present(view, g =>
                {
                    ThumbnailRenderer.DrawAll(g, prims, Ghost, (float)view.Mm(1.6));
                    DrawGuides(g, view, snap.Guides);
                });
                return true;
            }
            if (message == WM_LBUTTONUP)
            {
                var unit = _unit;
                var dragging = _portDragging;
                var to = _portTo;
                _portPressed = null;
                _mode = Mode.Armed;
                _overlay.Clear();
                if (dragging && unit is not null)
                {
                    Post(() => ApplyStretch(unit, port.Id, to));
                    return true;
                }
                // Click: the insert box opens right at the "+" with the parts that dock there first (the bar does not grow).
                if (unit is not null)
                {
                    var at = PortScreen(view, port);
                    Post(() => OpenPortInsert(unit, port, new Point((int)at.X, (int)at.Y)));
                }
                return true;
            }
            if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
            return true;
        }

        /// <summary>
        /// The insert box at a build "+": the parts that dock there first (docked and coupled when chosen), typing searches
        /// the whole library (anything else is placed at the pointer).
        /// </summary>
        private void OpenPortInsert(SketchUnit unit, BuildPort port, Point at)
        {
            var slide = CurrentSlide();
            if (slide is null || _portOwner is null) return;
            var registry = _registry();
            var lang = _lang();
            List<LibraryItem> items;
            try
            {
                items = Building.Items(registry, SketchUnits.WithContext(unit.Doc, _otherUnits), _portOwner, port, 12, lang).ToList();
            }
            catch (Exception ex)
            {
                Log.Error("Port items", ex);
                items = new List<LibraryItem>();
            }
            var first = unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            var view = first is null ? null : registry.Type(first.Type)?.ViewOf(first);
            var chosen = InsertSearch.Ask(registry, lang, AddInSettings.RecentItems, items, view, at, _view?.MainWindow ?? IntPtr.Zero, (i, size) => Thumb(i, size));
            if (chosen is null) return;
            AddInSettings.RecentItems = LibrarySearch.Remember(AddInSettings.RecentItems, chosen.Id);
            Log.Info("Port insert: " + port.Id + " " + chosen.Id);
            if (items.Any(i => i.Id == chosen.Id)) ApplyAction(unit, "port:" + port.Id + ":" + chosen.Id);
            else BeginPlacement(chosen);
        }

        /// <summary>Build ports of a unit with the items they offer, for UI tests ("id|kind|item,item…" per line).</summary>
        internal string BuildPortsOf(string shapeName) => OnUi(() =>
        {
            var slide = CurrentSlide();
            if (slide is null) return "";
            var unit = SketchUnits.Read(slide.Shapes[shapeName]);
            var owner = unit?.Objects.FirstOrDefault(o => _registry().Type(o.Type) is not null);
            if (unit is null || owner is null) return "";
            var registry = _registry();
            var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
            return string.Join("\n", Building.Ports(registry, full, owner.Id, _lang()).Select(p =>
                p.Id + "|" + p.Kind + "|" + string.Join(",", Building.Items(registry, full, owner.Id, p, 8, _lang()).Select(i => i.Id))));
        });

        /// <summary>Docks the chosen part at a build port (one undo step), coupled and selected.</summary>
        private void PortCommand(SketchUnit unit, string id)
        {
            var parts = id.Split(new[] { ':' }, 3);
            if (parts.Length < 3) return;
            var slide = CurrentSlide();
            var registry = _registry();
            var lang = _lang();
            var owner = unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (slide is null || owner is null || registry.Item(parts[2]) is not { } item) return;
            var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
            var port = Building.Ports(registry, full, owner.Id, lang).FirstOrDefault(p => p.Id == parts[1]);
            if (port is null) return;
            if (port.Kind is PortKind.Connect or PortKind.LineEnd)
            {
                ConnectCommand(slide, unit, full, owner, port, item);
                return;
            }
            var placed = Building.Place(registry, full, owner.Id, port, item, lang);
            if (placed is null) return;
            _app.StartNewUndoEntry();
            var doc = SingleUnitDoc(placed);
            SketchUnits.Draw(slide, registry, doc, lang);
            // A pivot, joint or slider placed at a link's joint: the link docks onto it.
            if (port.Kind == PortKind.Joint && !placed.IsPointGeometry && Building.DockBack(registry, Edits.Add(full, full.Pages[0].Id, placed), owner.Id, port, placed.Id, lang) is { } docked)
            {
                SketchUnits.Replace(slide, unit, Edits.Update(unit.Doc, new[] { owner.Id }, o => o with { Points = docked.Get(owner.Id)!.Points, Attach = docked.Get(owner.Id)!.Attach }), registry, lang);
            }
            SketchUnits.RedrawDependents(slide, registry, lang, Array.Empty<SketchDocument>(), new[] { doc });
            SketchUnits.All(slide).FirstOrDefault(u => u.Objects.Any(o => o.Id == placed.Id))?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Docked {item.Id} at {port.Id} of {owner.Type}");
        }

        /// <summary>
        /// "+" at a connection port or a line end (<see cref="Building.Apply"/>): the connecting line docked on the port, or
        /// the element together with its connecting line, or the line extended by a segment, or the element docked on the
        /// line end. New objects become units of their own (docked across units), the selected unit is updated in place;
        /// one undo step, the built part is selected.
        /// </summary>
        private void ConnectCommand(PowerPoint.Slide slide, SketchUnit unit, SketchDocument full, SketchObject owner, BuildPort port, LibraryItem item)
        {
            var registry = _registry();
            var lang = _lang();
            var built = Building.Apply(registry, full, owner.Id, port, item, lang);
            if (built is null) return;
            _app.StartNewUndoEntry();
            var changed = unit.Objects.Where(o => built.Doc.Get(o.Id) is { } now && !ReferenceEquals(now, o)).Select(o => o.Id).ToList();
            if (changed.Count > 0)
                SketchUnits.Replace(slide, unit, Edits.Update(unit.Doc, changed, o => built.Doc.Get(o.Id) ?? o), registry, lang);
            var added = built.Doc.Objects.Values.Where(o => full.Get(o.Id) is null).Select(SingleUnitDoc).ToList();
            foreach (var doc in added) SketchUnits.Draw(slide, registry, doc, lang);
            SketchUnits.RedrawDependents(slide, registry, lang, Array.Empty<SketchDocument>(), added.ToArray());
            SketchUnits.All(slide).FirstOrDefault(u => u.Objects.Any(o => o.Id == built.SelectId))?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Connected {item.Id} at {port.Id} ({port.Kind}) of {owner.Type}: {added.Count} new, {changed.Count} changed");
        }

        /// <summary>
        /// Replaces a predefined assembly by its parts (<see cref="Assemblies.Explode"/>), each a unit of its own, coupled as
        /// before; runs inside the caller's undo entry. The first part is selected.
        /// </summary>
        private void ApplyExplode(PowerPoint.Slide slide, SketchUnit unit)
        {
            var registry = _registry();
            var lang = _lang();
            var first = unit.Objects.FirstOrDefault(o => !o.IsPointGeometry && registry.Type(o.Type) is not null);
            if (first is null) return;
            var parts = Assemblies.Explode(registry, unit.Doc, first.Id, lang);
            if (parts.Count == 0) return;
            unit.Shape.Delete();
            foreach (var part in parts) SketchUnits.Draw(slide, registry, SingleUnitDoc(part), lang);
            // Teeth of the parts interlock (coupling of the inserted parts).
            var all = SketchDocument.Create();
            all = Edits.Add(all, all.Pages[0].Id, parts.ToArray());
            SketchUnits.RedrawDependents(slide, registry, lang, Array.Empty<SketchDocument>(), new[] { all });
            SketchUnits.All(slide).FirstOrDefault(u => u.Objects.Any(o => o.Id == parts[0].Id))?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Exploded {first.Type} into {parts.Count} parts");
        }

        /// <summary>Stretches a shaft at a build port to <paramref name="to"/>; the parts on it keep their place (no coupling).</summary>
        private void ApplyStretch(SketchUnit stale, string portId, Vec2 to)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var lang = _lang();
            var owner = unit.Objects.FirstOrDefault(o => !o.IsPointGeometry && registry.Type(o.Type) is not null);
            if (owner is null) return;
            var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
            var port = Building.Ports(registry, full, owner.Id, lang).FirstOrDefault(p => p.Id == portId);
            if (port is null) return;
            var doc = Building.StretchEnd(registry, unit.Doc, owner.Id, port, to, lang);
            if (ReferenceEquals(doc, unit.Doc)) return;
            _app.StartNewUndoEntry();
            _noCouple = true;
            try
            {
                ReplaceWithDependents(slide, unit, doc);
            }
            finally
            {
                _noCouple = false;
            }
            Log.Info($"Stretched {owner.Type} at {portId}");
        }
    }
}
