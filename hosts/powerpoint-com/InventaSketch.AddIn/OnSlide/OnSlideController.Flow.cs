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
    /// Building diagrams on the slide (like the blue arrows of draw.io and Slidemax): a selected flowchart, UML, ER, BPMN or
    /// basic element shows blue triangles on its four sides and, when the pointer comes near, its connection ports.
    /// Click a triangle: a copy is added on that side with a connector between the facing ports, and the floating bar
    /// offers the next element right away (one click replaces the copy, connections stay). Drag a triangle or a port: drop
    /// on a port of another element to connect both, or on free space to add an element there.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private static readonly Color FlowBlue = Color.FromArgb(31, 95, 191);
        private static readonly Color FlowGreen = Color.FromArgb(30, 150, 70);

        /// <summary>
        /// Distance of the triangles from the selection frame and their size (logical pixels, as in Slidemax); the top one sits
        /// beyond PowerPoint's rotation handle (Slidemax: 41 px).
        /// </summary>
        private const double TriangleOffsetPx = 13, TopTriangleOffsetPx = 44, TrianglePx = 12;
        private const double PortHitPx = 7, PortShowPx = 18, ConnectSnapPx = 14;

        private SketchObject? _chainSource;
        private int _hoverSide = -1;
        private string? _hoverPort;
        private bool _showPorts;
        private FlowSide _connectSide;
        private Anchor? _connectFrom;
        private Vec2 _connectStart;
        private POINT _connectDown;
        private bool _connectMoved;
        private Vec2 _connectAt;
        private Anchor? _connectTarget;
        /// <summary>Object just added by quick add: the bar offers the "next element" palette for it.</summary>
        private string? _nextFor;
        private readonly Dictionary<string, Bitmap> _thumbs = new(StringComparer.Ordinal);

        /// <summary>The armed unit's element if it chains (one box object of a chainable type), else null.</summary>
        private void UpdateChainSource(SketchUnit unit)
        {
            var objs = unit.Objects.ToList();
            _chainSource = objs.Count == 1 && Flow.CanChain(_registry(), objs[0]) ? objs[0] : null;
            if (_nextFor is not null && (objs.Count != 1 || objs[0].Id != _nextFor)) _nextFor = null;
            _hoverSide = -1;
            _hoverPort = null;
            _showPorts = false;
        }

        private static readonly FlowSide[] Sides = { FlowSide.Left, FlowSide.Top, FlowSide.Right, FlowSide.Bottom };

        private PointF TriangleCentre(SlideView view, FlowSide side)
        {
            var r = UnitScreenRect(view);
            var d = (float)(TriangleOffsetPx * view.DeviceScale);
            return side switch
            {
                FlowSide.Left => new PointF(r.Left - d, r.Top + r.Height / 2),
                FlowSide.Top => new PointF(r.Left + r.Width / 2, r.Top - (float)(TopTriangleOffsetPx * view.DeviceScale)),
                FlowSide.Right => new PointF(r.Right + d, r.Top + r.Height / 2),
                _ => new PointF(r.Left + r.Width / 2, r.Bottom + d),
            };
        }

        private int TriangleAt(POINT pt, SlideView view)
        {
            if (_chainSource is null || _unit is null) return -1;
            var tol = TrianglePx * 0.8 * view.DeviceScale;
            foreach (var side in Sides)
            {
                var c = TriangleCentre(view, side);
                if (Math.Abs(pt.X - c.X) <= tol && Math.Abs(pt.Y - c.Y) <= tol) return (int)side;
            }
            return -1;
        }

        /// <summary>Connection ports of the chain element that are not under a resize handle (those keep resizing).</summary>
        private List<Anchor> ConnectablePorts(SlideView view)
        {
            if (_chainSource is null) return new List<Anchor>();
            var rect = UnitRectMm();
            var handles = Enumerable.Range(0, 8).Select(h => view.ToScreen(HandlePoint(rect, h))).ToList();
            var tol = 7 * view.DeviceScale;
            return _unitAnchors.Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id is not null)
                .Where(a =>
                {
                    var s = view.ToScreen(a.P);
                    return handles.All(h => Math.Abs(h.X - s.X) > tol || Math.Abs(h.Y - s.Y) > tol);
                }).ToList();
        }

        private Anchor? PortAt(POINT pt, SlideView view)
        {
            var tol = PortHitPx * view.DeviceScale;
            return ConnectablePorts(view).Select(a => (a, s: view.ToScreen(a.P)))
                .Where(x => Math.Abs(x.s.X - pt.X) <= tol && Math.Abs(x.s.Y - pt.Y) <= tol)
                .OrderBy(x => Math.Abs(x.s.X - pt.X) + Math.Abs(x.s.Y - pt.Y)).Select(x => x.a).FirstOrDefault();
        }

        private bool NearUnit(POINT pt, SlideView view)
        {
            var r = UnitScreenRect(view);
            r.Inflate((float)(PortShowPx * view.DeviceScale), (float)(PortShowPx * view.DeviceScale));
            return r.Contains(pt.X, pt.Y);
        }

        /// <summary>Draws the quick-add triangles (and the ports when the pointer is near) for the armed chain element.</summary>
        private void DrawChainTools(SlideView view)
        {
            if (_chainSource is null || _unit is null || _mode != Mode.Armed) return;
            Present(view, _ => { }, g => DrawChainOverlay(g, view));
        }

        private void DrawChainOverlay(Graphics g, SlideView view)
        {
            var s = (float)view.DeviceScale;
            DrawLinks(g, view);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var blue = new SolidBrush(FlowBlue);
            using var hover = new SolidBrush(Color.FromArgb(60, 130, 230));
            using var white = new Pen(Color.White, 1.6f * s);
            foreach (var side in Sides)
            {
                var c = TriangleCentre(view, side);
                var big = _hoverSide == (int)side;
                var h = (float)(TrianglePx * (big ? 1.3 : 1.0) * s) / 2;
                var dir = Flow.Direction(side);
                // Triangle pointing outwards: tip in the direction of the side.
                var tip = new PointF(c.X + (float)dir.X * h, c.Y + (float)dir.Y * h);
                var n = new PointF((float)-dir.Y * h, (float)dir.X * h);
                var b = new PointF(c.X - (float)dir.X * h, c.Y - (float)dir.Y * h);
                g.FillPolygon(big ? hover : blue, new[] { tip, new PointF(b.X + n.X, b.Y + n.Y), new PointF(b.X - n.X, b.Y - n.Y) });
                if (big)
                {
                    // A small "+" inside the hovered triangle: adds the next element.
                    var m = new PointF(c.X - (float)dir.X * h * 0.25f, c.Y - (float)dir.Y * h * 0.25f);
                    var p = h * 0.38f;
                    g.DrawLine(white, m.X - p, m.Y, m.X + p, m.Y);
                    g.DrawLine(white, m.X, m.Y - p, m.X, m.Y + p);
                    QueueTip(view, c, T("Add the next element here (a copy, connected) · drag: connect or place it",
                        "Nächstes Element hier anfügen (Kopie, verbunden) · ziehen: verbinden oder ablegen"), (float)(TrianglePx * 0.6));
                }
            }
            if (_showPorts)
            {
                using var ring = new Pen(FlowBlue, 1.4f * s);
                using var fill = new SolidBrush(Color.White);
                foreach (var a in ConnectablePorts(view))
                {
                    var p = view.ToScreen(a.P);
                    var r = (a.Id == _hoverPort ? 4.5f : 3.2f) * s;
                    g.FillEllipse(a.Id == _hoverPort ? blue : fill, p.X - r, p.Y - r, 2 * r, 2 * r);
                    g.DrawEllipse(ring, p.X - r, p.Y - r, 2 * r, 2 * r);
                    if (a.Id == _hoverPort)
                        QueueTip(view, p, T("Drag from this port to connect", "Von diesem Anschluss ziehen zum Verbinden"), 4.5f);
                }
            }
            FlushTip(g, view);
        }

        /// <summary>Quick-add input while armed: hover feedback and the start of a triangle or port drag.</summary>
        private bool ChainMouse(int message, POINT pt, SlideView view, out bool swallow)
        {
            swallow = false;
            if (_chainSource is null || _unit is null) return false;
            if (message == WM_MOUSEMOVE)
            {
                var side = TriangleAt(pt, view);
                var port = side < 0 ? PortAt(pt, view)?.Id : null;
                var show = NearUnit(pt, view);
                if (side != _hoverSide || port != _hoverPort || show != _showPorts)
                {
                    _hoverSide = side;
                    _hoverPort = port;
                    _showPorts = show;
                    DrawChainTools(view);
                }
                return false;
            }
            if (message != WM_LBUTTONDOWN) return false;
            var hitSide = TriangleAt(pt, view);
            var hitPort = hitSide < 0 ? PortAt(pt, view) : null;
            if (hitSide < 0 && hitPort is null) return false;
            HideBar();
            var centre = new Vec2(_chainSource.Transform.X, _chainSource.Transform.Y);
            _connectFrom = hitPort;
            _connectSide = hitSide >= 0 ? (FlowSide)hitSide : Flow.SideOf(hitPort!.P - centre);
            _connectStart = hitPort?.P ?? Flow.Facing(_registry(), _chainSource, new RenderContext(_unit.Doc, _lang()), Flow.Direction(_connectSide)).Point;
            _connectDown = pt;
            _connectMoved = false;
            _connectTarget = null;
            _connectAt = view.ToMm(pt);
            _mode = Mode.Connect;
            swallow = true;
            return true;
        }

        /// <summary>Input while dragging from a triangle or port.</summary>
        private bool ConnectMouse(int message, POINT pt, SlideView view)
        {
            if (message == WM_MOUSEMOVE)
            {
                if (!_connectMoved && Math.Abs(pt.X - _connectDown.X) + Math.Abs(pt.Y - _connectDown.Y) < 5 * view.DeviceScale) return true;
                _connectMoved = true;
                _connectAt = view.ToMm(pt);
                _connectTarget = TargetPortAt(pt, view);
                DrawConnect(view);
                return true;
            }
            if (message == WM_LBUTTONUP)
            {
                var unit = _unit;
                var src = _chainSource;
                var side = _connectSide;
                var from = _connectFrom;
                var moved = _connectMoved;
                var at = _connectAt;
                var target = _connectTarget;
                _mode = Mode.Armed;
                _overlay.Clear();
                if (unit is null || src is null) return true;
                var centre = new Vec2(src.Transform.X, src.Transform.Y);
                Log.Info("Quick add: " + (moved ? target is not null ? "connect to " + target.Owner + "." + target.Id : "drop" : "click") + " on " + side);
                if (!moved) Post(() => AddNext(unit, side, null, from?.Id));
                else if (target is not null) Post(() => ConnectTo(unit, side, from, target));
                else Post(() => AddNext(unit, Flow.SideOf(at - centre), at, from?.Id));
                return true;
            }
            if (message == WM_RBUTTONDOWN) { Cancel("right click"); return true; }
            return true;
        }

        /// <summary>The nearest connection point of another element under the pointer (ports, else edge middles).</summary>
        private Anchor? TargetPortAt(POINT pt, SlideView view)
        {
            var tol = ConnectSnapPx * view.DeviceScale;
            Anchor? best = null;
            var bestD = double.MaxValue;
            foreach (var a in _targets)
            {
                if (a.Owner is null || a.Owner == _chainSource?.Id || a.Kind != AnchorKind.Point) continue;
                if (a.Role is not (AnchorRole.Port or AnchorRole.EdgeMid)) continue;
                if (_targetOwners.TryGetValue(a.Owner, out var owner) && owner.IsPointGeometry) continue;
                var s = view.ToScreen(a.P);
                var d = Math.Sqrt((s.X - pt.X) * (s.X - pt.X) + (s.Y - pt.Y) * (s.Y - pt.Y));
                // Ports win over edge middles at the same distance.
                if (a.Role == AnchorRole.EdgeMid) d += 2 * view.DeviceScale;
                if (d <= tol && d < bestD) { best = a; bestD = d; }
            }
            return best;
        }

        private void DrawConnect(SlideView view)
        {
            var src = _chainSource;
            if (src is null) return;
            Present(view, _ => { }, g =>
            {
                var s = (float)view.DeviceScale;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var a = view.ToScreen(_connectStart);
                var end = _connectTarget?.P ?? _connectAt;
                var b = view.ToScreen(end);
                using var pen = new Pen(FlowBlue, 1.8f * s) { DashPattern = new[] { 4f, 3f } };
                g.DrawLine(pen, a, b);
                if (_connectTarget is not null)
                {
                    using var ok = new SolidBrush(FlowGreen);
                    var r = 5f * s;
                    g.FillEllipse(ok, b.X - r, b.Y - r, 2 * r, 2 * r);
                    return;
                }
                // Free drop: outline of the element that will be added there.
                var bounds = src.Bounds();
                var tl = view.ToScreen(new Vec2(end.X - bounds.W / 2, end.Y - bounds.H / 2));
                var br = view.ToScreen(new Vec2(end.X + bounds.W / 2, end.Y + bounds.H / 2));
                using var ghost = new Pen(FlowBlue, 1.2f * s) { DashPattern = new[] { 2f, 2f } };
                g.DrawRectangle(ghost, tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
            });
        }

        // ------------------------------------------------------------------ coupled drag

        /// <summary>The other units of the slide (read when arming).</summary>
        private List<SketchUnit> _otherUnits = new();
        /// <summary>Objects of other units that move rigidly with the armed unit (gears on its shaft …).</summary>
        private HashSet<string> _followerIds = new(StringComparer.Ordinal);
        private IReadOnlyList<Primitive> _dragPrims = Array.Empty<Primitive>();
        private List<Anchor> _dragAnchors = new();
        private List<Anchor> _dragTargets = new();
        private bool _dragCoupled;

        /// <summary>
        /// Followers of the armed unit for a drag (<see cref="Coupling.RigidFollowers"/>): their shapes are previewed with
        /// the unit, their anchors snap as well (a gear on a dragged shaft meshes with a gear on another shaft) and they are
        /// no snap targets themselves.
        /// </summary>
        private void PrepareFollowers(SketchUnit unit)
        {
            _followerIds = new HashSet<string>(StringComparer.Ordinal);
            _dragPrims = _unitPrims;
            _dragAnchors = _unitAnchors;
            _dragTargets = _targets;
            try
            {
                var registry = _registry();
                var full = SketchUnits.WithContext(unit.Doc, _otherUnits);
                foreach (var id in Coupling.RigidFollowers(registry, full, unit.Doc.Objects.Keys, _lang())) _followerIds.Add(id);
                if (_followerIds.Count == 0) return;
                var ctx = new RenderContext(full, _lang());
                var followers = _followerIds.Select(full.Get).Where(o => o is not null && registry.Type(o.Type) is not null).Cast<SketchObject>().ToList();
                _dragPrims = _unitPrims.Concat(followers.SelectMany(o => registry.Render(o, ctx))).ToList();
                _dragAnchors = _unitAnchors.Concat(followers.SelectMany(o => registry.PageAnchors(o, ctx))).ToList();
                _dragTargets = _targets.Where(a => a.Owner is null || !_followerIds.Contains(a.Owner)).ToList();
                Log.Info($"Drag followers: {_followerIds.Count} coupled object(s)");
            }
            catch (Exception ex)
            {
                Log.Error("Drag followers", ex);
            }
        }

        // ------------------------------------------------------------------ commands (UI thread)

        private SketchDocument SingleUnitDoc(SketchObject o)
        {
            var setup = _app.ActivePresentation.PageSetup;
            var doc = SketchDocument.Create(new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm));
            return Edits.Add(doc, doc.Pages[0].Id, o with { GroupId = null });
        }

        /// <summary>Adds the next element (copy) on <paramref name="side"/> or at <paramref name="at"/>, connected; one undo step.</summary>
        private void AddNext(SketchUnit unit, FlowSide side, Vec2? at, string? fromAnchorId)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var lang = _lang();
            var fresh = SketchUnits.Read(unit.Shape) ?? unit;
            var src = fresh.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (src is null) return;
            var full = SketchUnits.WithContext(fresh.Doc, SketchUnits.All(slide, fresh.Id));
            var res = Flow.QuickAdd(registry, full, src.Id, side, at, null, fromAnchorId);
            var added = res.Doc.Get(res.ObjectId)!;
            _app.StartNewUndoEntry();
            _nextFor = added.Id;
            var shape = SketchUnits.Draw(slide, registry, SingleUnitDoc(added), lang);
            if (res.ConnectorId is not null && res.Doc.Get(res.ConnectorId) is { } connector)
                SketchUnits.Draw(slide, registry, SingleUnitDoc(connector), lang);
            shape?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Added {added.Type} {side} of {src.Id}" + (res.ConnectorId is null ? "" : " with connector"));
        }

        /// <summary>Connects the chain element to a port of another element with the type's connector; one undo step.</summary>
        private void ConnectTo(SketchUnit unit, FlowSide side, Anchor? from, Anchor target)
        {
            var slide = CurrentSlide();
            if (slide is null || target.Owner is null || !_targetOwners.TryGetValue(target.Owner, out var owner)) return;
            var registry = _registry();
            var fresh = SketchUnits.Read(unit.Shape) ?? unit;
            var src = fresh.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (src is null) return;
            var item = Flow.ConnectorItem(registry, registry.Type(src.Type)?.ChainConnector(src));
            if (item is null) return;
            var start = from ?? Flow.Facing(registry, src, new RenderContext(fresh.Doc, _lang()), Flow.Direction(side)).Anchor;
            var connector = registry.Create(item, (start.P + target.P) * 0.5) with
            {
                Points = new[] { start.P, target.P },
                Attach = new[] { Attachments.For(src, start.P, start), Attachments.For(owner, target.P, target) },
            };
            _app.StartNewUndoEntry();
            SketchUnits.Draw(slide, registry, SingleUnitDoc(connector), _lang())?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Connected {src.Id} to {owner.Id}.{target.Id}");
        }

        /// <summary>"Next element": turns the just added element into <paramref name="itemId"/>; connectors stay attached.</summary>
        private void ReplaceNext(SketchUnit unit, string itemId)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var lang = _lang();
            var fresh = SketchUnits.Read(unit.Shape) ?? unit;
            var obj = fresh.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (obj is null) return;
            var others = SketchUnits.All(slide, fresh.Id);
            var full = SketchUnits.WithContext(fresh.Doc, others);
            var doc = Flow.Replace(registry, full, obj.Id, itemId);
            _app.StartNewUndoEntry();
            _nextFor = obj.Id;
            var shape = SketchUnits.Replace(slide, fresh, SingleUnitDoc(doc.Get(obj.Id)!), registry, lang);
            // Connectors in other units move to the ports of the new element.
            foreach (var u in others)
            {
                var ids = u.Objects.Where(o => o.Attach is not null && o.Attach.Any(a => a?.ObjectId == obj.Id)).Select(o => o.Id).ToList();
                if (ids.Count == 0) continue;
                SketchUnits.Replace(slide, u, Edits.Update(u.Doc, ids, o => doc.Get(o.Id) ?? o), registry, lang);
            }
            shape?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Replaced {obj.Type} by {itemId}");
        }

        /// <summary>
        /// Inserts <paramref name="item"/> where it fits the selected element (<see cref="Fitting"/>): coupled right away and
        /// selected, so its choices (shaft connection: fixed, sliding, loose …) are in the bar. False if no rule applies.
        /// Runs inside the caller's undo entry.
        /// </summary>
        private bool InsertFitting(PowerPoint.Slide slide, SketchUnit unit, LibraryItem item)
        {
            var registry = _registry();
            var lang = _lang();
            var fresh = SketchUnits.Read(unit.Shape) ?? unit;
            var first = fresh.Objects.FirstOrDefault(o => !o.IsPointGeometry && registry.Type(o.Type) is not null);
            if (first is null) return false;
            var full = SketchUnits.WithContext(fresh.Doc, SketchUnits.All(slide, fresh.Id));
            var placed = Fitting.Place(registry, full, first.Id, item, lang);
            if (placed is null) return false;
            var doc = SingleUnitDoc(placed);
            SketchUnits.Draw(slide, registry, doc, lang);
            // Coupling of the insert (teeth interlock with the partner) may redraw the new unit: select the live one.
            SketchUnits.RedrawDependents(slide, registry, lang, Array.Empty<SketchDocument>(), new[] { doc });
            SketchUnits.All(slide).FirstOrDefault(u => u.Objects.Any(o => o.Id == placed.Id))?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Inserted {item.Id} fitting {first.Type}");
            return true;
        }

        /// <summary>Palette buttons of the "next element" (thumbnails) for the bar, while the element was just added.</summary>
        private List<BarButton> NextButtons(SketchObject first)
        {
            // Just added with "+": one button opens the insert box with what the new element can become (no thumbnail row).
            var list = new List<BarButton>();
            if (_nextFor != first.Id || Flow.NextItems(_registry(), first, 2).Count == 0) return list;
            // The main action right after a quick add, with a short text: the cycle pictogram alone says too little.
            list.Add(new BarButton("nextbox", T("Make it …", "Ändern in …"), T("Turn the new element into another one (connections stay)", "Das neue Element in ein anderes ändern (Verbindungen bleiben)")) { IconId = IconIds.Cycle, ShowLabel = true });
            return list;
        }

        /// <summary>The insert box for "make it …": what a just added element can become; choosing replaces it.</summary>
        private void OpenNextBox(SketchUnit unit)
        {
            var registry = _registry();
            var first = unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (first is null) return;
            var items = Flow.NextItems(registry, first, 12).ToList();
            GetCursorPos(out var p);
            var chosen = InsertSearch.Ask(registry, _lang(), AddInSettings.RecentItems, items, registry.Type(first.Type)?.ViewOf(first),
                new Point(p.X, p.Y), _view?.MainWindow ?? IntPtr.Zero, (i, size) => Thumb(i, size));
            if (chosen is null) return;
            AddInSettings.RecentItems = LibrarySearch.Remember(AddInSettings.RecentItems, chosen.Id);
            ApplyAction(unit, "next:" + chosen.Id);
        }

        private Bitmap Thumb(LibraryItem item, int size)
        {
            var key = item.Id + "|" + size + "|" + (_dark ? "d" : "l");
            if (_thumbs.TryGetValue(key, out var bmp)) return bmp;
            var registry = _registry();
            var doc = SketchDocument.Create();
            var o = registry.Create(item, new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var prims = registry.Render(o, new RenderContext(doc, _lang()));
            var ink = _dark ? Color.FromArgb(230, 230, 230) : Color.FromArgb(40, 40, 40);
            bmp = ThumbnailRenderer.Render(prims, size, ink, Color.Transparent);
            _thumbs[key] = bmp;
            return bmp;
        }
    }
}
