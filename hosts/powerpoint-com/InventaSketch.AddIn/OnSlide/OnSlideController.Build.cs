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
    /// hub (shaft). A click on a "+" puts the parts that fit there into the floating bar; a click on one docks it there,
    /// coupled and selected. Dragging the "+" at a shaft end stretches the shaft; the parts on it keep their place.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private const double PortOffsetPx = 15, PortPx = 6.5;

        private List<BuildPort> _ports = new();
        private string? _portOwner;
        private BuildPort? _portFocus;
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
            var first = unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null && (!o.IsPointGeometry || registry.Type(o.Type)!.LinkageRole(o) == LinkageRole.Link));
            if (first is null || unit.Objects.Count() != 1) { _portOwner = null; _portFocus = null; return; }
            if (_portOwner != first.Id) _portFocus = null;
            _portOwner = first.Id;
            try
            {
                var full = SketchUnits.WithContext(unit.Doc, _otherUnits);
                _ports = Building.Ports(registry, full, first.Id, _lang()).ToList();
                if (_portFocus is not null && !_ports.Any(p => p.Id == _portFocus.Id)) _portFocus = null;
            }
            catch (Exception ex)
            {
                Log.Error("Build ports", ex);
            }
        }

        /// <summary>
        /// Screen position of a port's "+": a little beyond the port along its direction, pushed further out until it is
        /// clear of PowerPoint's resize and rotation handles (those keep working). A free hub's "+" sits outside the unit,
        /// beyond its top right corner, so the element can still be dragged by its centre.
        /// </summary>
        private PointF PortScreen(SlideView view, BuildPort p)
        {
            var scale = (float)view.DeviceScale;
            var rect = UnitRectMm();
            if (p.Kind == PortKind.Hub)
            {
                var corner = view.ToScreen(new Vec2(rect.X + rect.W, rect.Y));
                return new PointF(corner.X + 16 * scale, corner.Y - 16 * scale);
            }
            var s = view.ToScreen(p.At);
            var handles = Enumerable.Range(0, 8).Select(h => view.ToScreen(HandlePoint(rect, h))).ToList();
            var top = view.ToScreen(new Vec2(rect.X + rect.W / 2, rect.Y));
            handles.Add(new PointF(top.X, top.Y - 35 * scale));
            var clear = 15 * scale;
            var off = (float)(PortOffsetPx * scale);
            for (var i = 0; i < 12; i++)
            {
                var c = new PointF(s.X + (float)p.Dir.X * off, s.Y + (float)p.Dir.Y * off);
                if (handles.All(h => Math.Abs(h.X - c.X) > clear || Math.Abs(h.Y - c.Y) > clear)) return c;
                off += 6 * scale;
            }
            return new PointF(s.X + (float)p.Dir.X * off, s.Y + (float)p.Dir.Y * off);
        }

        private int PortAt2(POINT pt, SlideView view)
        {
            var r = PortPx * 1.4 * view.DeviceScale;
            for (var i = 0; i < _ports.Count; i++)
            {
                var c = PortScreen(view, _ports[i]);
                if (Math.Abs(c.X - pt.X) <= r && Math.Abs(c.Y - pt.Y) <= r) return i;
            }
            return -1;
        }

        private void DrawPorts(Graphics g, SlideView view)
        {
            if (_ports.Count == 0) return;
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var blue = new SolidBrush(FlowBlue);
            using var hot = new SolidBrush(Color.FromArgb(60, 130, 230));
            using var white = new Pen(Color.White, 1.5f * s);
            using var lead = new Pen(Color.FromArgb(120, FlowBlue), 1f * s) { DashPattern = new[] { 2f, 2f } };
            using var focus = new Pen(FlowGreen, 2f * s);
            for (var i = 0; i < _ports.Count; i++)
            {
                var p = _ports[i];
                var at = view.ToScreen(p.At);
                var c = PortScreen(view, p);
                g.DrawLine(lead, at, c);
                var hover = i == _hoverPort2;
                var r = (float)(PortPx * (hover ? 1.2 : 1.0) * s);
                g.FillEllipse(hover ? hot : blue, c.X - r, c.Y - r, 2 * r, 2 * r);
                if (_portFocus?.Id == p.Id) g.DrawEllipse(focus, c.X - r - 2 * s, c.Y - r - 2 * s, 2 * r + 4 * s, 2 * r + 4 * s);
                var k = r * 0.55f;
                g.DrawLine(white, c.X - k, c.Y, c.X + k, c.Y);
                g.DrawLine(white, c.X, c.Y - k, c.X, c.Y + k);
            }
            if (_hoverPort2 >= 0 && _hoverPort2 < _ports.Count)
            {
                var p = _ports[_hoverPort2];
                var c = PortScreen(view, p);
                DrawTip(g, view, new PointF(c.X + (float)(PortPx * 1.8 * s), c.Y), PortTip(p));
            }
        }

        private string PortTip(BuildPort p) => p.Kind switch
        {
            PortKind.Mesh => T("Add a meshing gear here", "Gegenrad hier anfügen"),
            PortKind.InnerMesh => T("Add a planet here", "Planet hier einsetzen"),
            PortKind.Hub => T("Add a shaft through the hub", "Welle durch die Nabe"),
            PortKind.Joint => T("Add the next link, a fixed pivot or a slider here", "Nächstes Glied, Gestelldrehpunkt oder Schieber anfügen"),
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

        /// <summary>Bar buttons for the focused build port: thumbnails of the parts that dock there.</summary>
        private IEnumerable<BarButton> PortButtons()
        {
            var port = _portFocus;
            if (port is null || _unit is null || _portOwner is null) yield break;
            List<LibraryItem> items;
            try
            {
                var full = SketchUnits.WithContext(_unit.Doc, _otherUnits);
                items = Building.Items(_registry(), full, _portOwner, port, 8, _lang()).ToList();
            }
            catch (Exception ex)
            {
                Log.Error("Port items", ex);
                yield break;
            }
            var scale = _view?.DeviceScale ?? 1;
            foreach (var item in items)
                yield return new BarButton("port:" + port.Id + ":" + item.Id, "", T("Add here: ", "Hier anfügen: ") + item.Name.Get(_lang())) { Icon = Thumb(item, (int)Math.Round(26 * scale)) };
            if (items.Count > 0) yield return new BarButton("", "", "", Separator: true);
        }

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
            _portFocus = null;
            SketchUnits.All(slide).FirstOrDefault(u => u.Objects.Any(o => o.Id == placed.Id))?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Docked {item.Id} at {port.Id} of {owner.Type}");
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
