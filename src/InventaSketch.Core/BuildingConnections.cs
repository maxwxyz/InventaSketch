using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Build "+" for connections (cables, wires, pipes, ducts, signal lines): every free connection port of an element
    /// (<see cref="ObjectType.Connection"/>) offers the connector of its medium and the elements with a port of that
    /// medium; a connector starts docked on the port and its free end offers "+" again (the line continues, or the next
    /// element docks there with its port). Media come from the modules (<see cref="PortMedia"/>); a line without a medium
    /// of its own carries the medium of the port it is docked on, so any line drawn from a connection port continues.
    /// </summary>
    public static partial class Building
    {
        /// <summary>Medium of the line <paramref name="line"/>: its type's, else that of a connection port one of its ends is docked on.</summary>
        public static string? LineMedium(Registry registry, SketchDocument doc, SketchObject line, Lang lang = Lang.En)
        {
            if (!line.IsPointGeometry || registry.Type(line.Type) is not { } type) return null;
            if (type.LineMedium(line) is { } own) return own;
            if (line.Attach is null) return null;
            var ctx = new RenderContext(doc, lang);
            foreach (var a in line.Attach)
            {
                if (a?.AnchorId is null || doc.Get(a.ObjectId) is not { IsPointGeometry: false } target || registry.Type(target.Type) is not { } tt) continue;
                var anchor = registry.PageAnchors(target, ctx).FirstOrDefault(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port && x.Id == a.AnchorId);
                if (anchor is not null && tt.Connection(target, anchor) is { } c) return c.Medium;
            }
            return null;
        }

        /// <summary>The connection of port <paramref name="anchorId"/> of box object <paramref name="o"/>, with the anchor (page coordinates).</summary>
        public static (PortConnection Connection, Anchor Anchor)? ConnectionOf(Registry registry, SketchDocument doc, SketchObject o, string? anchorId, Lang lang = Lang.En)
        {
            if (anchorId is null || o.IsPointGeometry || registry.Type(o.Type) is not { } type) return null;
            var a = registry.PageAnchors(o, new RenderContext(doc, lang)).FirstOrDefault(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port && x.Id == anchorId);
            return a is not null && type.Connection(o, a) is { } c ? (c, a) : null;
        }

        /// <summary>
        /// Builds <paramref name="item"/> at <paramref name="port"/> of <paramref name="id"/> in one go: the placed part
        /// docked (and a link docked back onto a joint element); at a connection port the connector, or the next element
        /// joined to the port by the connector; at a line end the line one segment longer, or the element with the line
        /// end docked on its port. Null if the item does not fit there.
        /// </summary>
        public static BuildResult? Apply(Registry registry, SketchDocument doc, string id, BuildPort port, LibraryItem item, Lang lang = Lang.En)
        {
            var sel = doc.Get(id);
            if (sel is null || registry.Type(item.TypeId) is null) return null;
            var page = (doc.PageOf(id) ?? doc.Pages[0]).Id;
            if (port.Kind is not (PortKind.Connect or PortKind.LineEnd))
            {
                var placed = Place(registry, doc, id, port, item, lang);
                if (placed is null) return null;
                var next = Edits.Add(doc, page, placed);
                if (port.Kind == PortKind.Joint && !placed.IsPointGeometry && DockBack(registry, next, id, port, placed.Id, lang) is { } docked) next = docked;
                return new BuildResult(next, placed.Id);
            }
            var built = PlaceConnection(registry, doc, sel, port, item, lang);
            if (built is null) return null;
            var (obj, elementPort, connector) = built.Value;
            SketchDocument result;
            if (port.Kind == PortKind.LineEnd)
            {
                if (obj.Id == sel.Id)
                {
                    // The line continues: one segment more, the new end free.
                    var atStart = port.AnchorId == "p0";
                    var ext = obj.Points![atStart ? 0 : obj.Points.Count - 1];
                    result = Polylines.Extend(registry, doc, sel.Id, atStart, ext, null, lang).Doc;
                }
                else
                {
                    // An element docks on the free end: the line end sits on its port and follows it.
                    result = Edits.Add(doc, page, obj);
                    var index = Attachments.PointIndex(port.AnchorId) ?? 0;
                    result = Edits.SetPoint(result, sel.Id, index, elementPort!.P, Attachments.For(obj, elementPort.P, elementPort));
                }
            }
            else
            {
                result = Edits.Add(doc, page, obj);
                if (connector is not null) result = Edits.Add(result, page, connector);
            }
            result = Attachments.Refresh(registry, result, new RenderContext(result, lang));
            return new BuildResult(result, obj.Id);
        }

        /// <summary>Length of a connector drawn by "+" from an element: half its larger side, 8 … 30 mm.</summary>
        private static double ConnectorLength(SketchObject sel) => Math.Max(8, Math.Min(30, Math.Max(sel.Size.W, sel.Size.H) * 0.5));

        /// <summary>
        /// The outward direction of a port at <paramref name="p"/>: the normal of the box edge nearest to it (pins leave a
        /// symbol across its edge), in page coordinates.
        /// </summary>
        private static Vec2 OutDir(SketchObject o, Vec2 p)
        {
            var (u, v) = o.ToBox(p);
            var dx = (0.5 - Math.Abs(u)) * o.Size.W;
            var dy = (0.5 - Math.Abs(v)) * o.Size.H;
            Vec2 local;
            if (dx < dy - 1e-9 || (Math.Abs(dx - dy) <= 1e-9 && Math.Abs(u) >= Math.Abs(v))) local = new Vec2(u < 0 ? -1 : 1, 0);
            else local = new Vec2(0, v < 0 ? -1 : 1);
            var m = o.Matrix();
            var d = m.Apply(local) - m.Apply(Vec2.Zero);
            return d.Length < 1e-9 ? local : d.Normalized();
        }

        /// <summary>Free connection ports of a box element: ports with a medium that no line end, port or other element occupies.</summary>
        private static IEnumerable<BuildPort> ConnectPorts(Registry registry, SketchDocument doc, SketchObject o, List<Anchor> own, IEnumerable<Anchor> othersAnchors, Lang lang)
        {
            var type = registry.Type(o.Type)!;
            var ports = own.Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id is not null).ToList();
            if (ports.Count == 0) yield break;
            List<Vec2>? occupied = null;
            HashSet<string>? attached = null;
            foreach (var a in ports)
            {
                var c = type.Connection(o, a);
                if (c is null) continue;
                if (attached is null)
                {
                    attached = new HashSet<string>(StringComparer.Ordinal);
                    occupied = new List<Vec2>();
                    foreach (var x in doc.Objects.Values.Where(x => x.Id != o.Id && x.IsPointGeometry))
                    {
                        if (x.Attach is not null)
                            foreach (var t in x.Attach)
                                if (t?.ObjectId == o.Id && t.AnchorId is { } aid) attached.Add(aid);
                        var pts = doc.ResolvePoints(x);
                        if (pts.Count > 0) { occupied.Add(pts[0]); occupied.Add(pts[pts.Count - 1]); }
                    }
                    occupied.AddRange(othersAnchors.Where(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port).Select(x => x.P));
                }
                if (attached.Contains(a.Id!) || occupied!.Any(p => p.DistanceTo(a.P) <= 0.3)) continue;
                yield return new BuildPort("port." + a.Id, PortKind.Connect, a.P, OutDir(o, a.P)) { Medium = c.Medium, AnchorId = a.Id };
            }
        }

        /// <summary>Free ends of a connection line (one with a medium): not docked, and nothing docked or sitting on them.</summary>
        private static List<BuildPort> LineEndPorts(Registry registry, SketchDocument doc, SketchObject line, Lang lang)
        {
            var list = new List<BuildPort>();
            var type = registry.Type(line.Type)!;
            if (type.IsClosed(line) || !Polylines.CanExtend(registry, line)) return list;
            var medium = LineMedium(registry, doc, line, lang);
            if (medium is null) return list;
            var pts = doc.ResolvePoints(line);
            if (pts.Count < 2) return list;
            var ctx = new RenderContext(doc, lang);
            var rails = registry.PageAnchors(line, ctx).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail).ToList();
            foreach (var (i, name) in new[] { (0, "line.start"), (pts.Count - 1, "line.end") })
            {
                if (line.Attach is not null && i < line.Attach.Count && line.Attach[i] is not null) continue;
                var pid = "p" + i.ToString(CultureInfo.InvariantCulture);
                var end = pts[i];
                var taken = doc.Objects.Values.Any(x => x.Id != line.Id && x.Attach is not null && x.Attach.Any(a => a?.ObjectId == line.Id && a.AnchorId == pid))
                    || doc.Objects.Values.Any(x => x.Id != line.Id && !x.IsPointGeometry && registry.Type(x.Type) is not null
                        && registry.PageAnchors(x, ctx).Any(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.P.DistanceTo(end) <= 0.3));
                if (taken) continue;
                // Direction: the drawn segment that ends here (routed lines leave straight), else the raw one.
                Vec2? seg = rails.FirstOrDefault(r => r.P.DistanceTo(end) <= 0.05 && r.Q.DistanceTo(end) > 1e-6) is { } r1 ? r1.Q
                    : rails.FirstOrDefault(r => r.Q.DistanceTo(end) <= 0.05 && r.P.DistanceTo(end) > 1e-6) is { } r2 ? r2.P
                    : null;
                if (seg is null)
                    foreach (var q in i == 0 ? pts.Skip(1) : pts.Take(pts.Count - 1).Reverse())
                        if (q.DistanceTo(end) > 1e-6) { seg = q; break; }
                if (seg is not Vec2 from) continue;
                list.Add(new BuildPort(name, PortKind.LineEnd, end, (end - from).Normalized()) { AxisFrom = from, Medium = medium, AnchorId = pid });
            }
            return list;
        }

        /// <summary>Connector items for a connection: the declared one first, then lines whose medium covers it (exact first), one per type.</summary>
        private static List<LibraryItem> Connectors(Registry registry, PortConnection c, int max = 3)
        {
            var list = new List<LibraryItem>();
            if (Flow.ConnectorItem(registry, c.Connector) is { } declared && registry.Type(declared.TypeId)?.Geometry == GeometryKind.Points) list.Add(declared);
            var others = new List<(LibraryItem Item, int Rank)>();
            foreach (var g in registry.Library.Where(i => registry.Type(i.TypeId)?.Geometry == GeometryKind.Points).GroupBy(i => i.TypeId))
            {
                if (list.Any(i => i.TypeId == g.Key)) continue;
                (LibraryItem Item, int Rank)? best = null;
                foreach (var item in g)
                {
                    var m = ItemMedia(registry, item).FirstOrDefault();
                    if (!PortMedia.Covers(m, c.Medium)) continue;
                    var rank = m == c.Medium ? 0 : 1;
                    if (best is null || rank < best.Value.Rank) best = (item, rank);
                }
                if (best is { } b) others.Add(b);
            }
            list.AddRange(others.OrderBy(x => x.Rank).Select(x => x.Item));
            return list.Take(max).ToList();
        }

        /// <summary>True if <paramref name="item"/> is the connector the connection declares.</summary>
        private static bool IsDeclared(Registry registry, PortConnection c, LibraryItem item)
            => Flow.ConnectorItem(registry, c.Connector) is { } d && (d.Id == item.Id || c.Connector == item.TypeId && d.TypeId == item.TypeId);

        /// <summary>The module that registers type <paramref name="typeId"/>, or null.</summary>
        private static IModule? ModuleOf(Registry registry, string typeId) => registry.Modules.FirstOrDefault(m => m.Types.Any(t => t.Id == typeId));

        /// <summary>
        /// Element items that may dock at a connection of <paramref name="medium"/> next to <paramref name="o"/>: its suggestions
        /// (companions), then the other types of its module, then types of other modules with exactly that medium; one per
        /// type, the item in the view of <paramref name="o"/> where there is one. Each with the port it docks with.
        /// </summary>
        private static IEnumerable<LibraryItem> ElementCandidates(Registry registry, SketchObject o, string medium)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var module = ModuleOf(registry, o.Type);
            var moduleTypes = new HashSet<string>(module?.Types.Select(t => t.Id) ?? Array.Empty<string>(), StringComparer.Ordinal);
            LibraryItem Pick(IGrouping<string, LibraryItem> g) => g.FirstOrDefault(i => i.View is not null && i.View == o.View) ?? g.First();
            foreach (var i in registry.Suggestions(o, 14))
                if (registry.Type(i.TypeId)?.Geometry == GeometryKind.Box && seen.Add(i.TypeId)) yield return i;
            foreach (var g in registry.Library.Where(i => moduleTypes.Contains(i.TypeId)).GroupBy(i => i.TypeId))
                if (registry.Type(g.Key)?.Geometry == GeometryKind.Box && seen.Add(g.Key)) yield return Pick(g);
            foreach (var g in registry.Library.Where(i => !moduleTypes.Contains(i.TypeId)).GroupBy(i => i.TypeId))
                if (registry.Type(g.Key)?.Geometry == GeometryKind.Box && seen.Add(g.Key)) yield return Pick(g);
        }

        /// <summary>Library items at a connection port or a line end: connectors (or "continue the line"), then fitting elements.</summary>
        private static IReadOnlyList<LibraryItem> ConnectionItems(Registry registry, SketchDocument doc, SketchObject o, BuildPort port, int max, Lang lang)
        {
            var list = new List<LibraryItem>();
            string medium;
            SketchObject from = o;
            if (port.Kind == PortKind.Connect)
            {
                if (ConnectionOf(registry, doc, o, port.AnchorId, lang) is not { } c) return list;
                medium = c.Connection.Medium;
                foreach (var item in Connectors(registry, c.Connection))
                    if (list.Count < max && PlaceConnection(registry, doc, o, port, item, lang) is not null) list.Add(item);
            }
            else
            {
                if (port.Medium is null) return list;
                medium = port.Medium;
                // Continue the line: its own library item.
                var own = registry.Library.Where(i => i.TypeId == o.Type)
                    .OrderByDescending(i => i.Params is not null && i.Params.All(kv => o.Params.TryGetValue(kv.Key, out var v) && v.Equals(kv.Value)) ? 1 : 0)
                    .FirstOrDefault();
                if (own is not null) list.Add(own);
            }
            var module = ModuleOf(registry, o.Type);
            var moduleTypes = new HashSet<string>(module?.Types.Select(t => t.Id) ?? Array.Empty<string>(), StringComparer.Ordinal);
            var suggested = new HashSet<string>(registry.Suggestions(o, 14).Select(s => s.TypeId), StringComparer.Ordinal);
            foreach (var item in ElementCandidates(registry, from, medium))
            {
                if (list.Count >= max) break;
                var media = ItemMedia(registry, item);
                if (!media.Any(m => PortMedia.Fits(m, medium))) continue;
                // Other modules: only elements with exactly this medium (a resistor does not follow a PV string).
                if (!moduleTypes.Contains(item.TypeId) && !suggested.Contains(item.TypeId) && !media.Contains(medium)) continue;
                if (PlaceConnection(registry, doc, o, port, item, lang) is not null) list.Add(item);
            }
            return list;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Registry, Dictionary<string, string[]>> MediaCache = new();

        /// <summary>
        /// Media of a library item as inserted: the port media of a box element, the line medium of a line (cached per
        /// registry; a cheap pre-filter before an element is placed for real).
        /// </summary>
        private static string[] ItemMedia(Registry registry, LibraryItem item)
        {
            var cache = MediaCache.GetValue(registry, _ => new Dictionary<string, string[]>(StringComparer.Ordinal));
            lock (cache)
                if (cache.TryGetValue(item.Id, out var known)) return known;
            string[] media;
            try
            {
                var probe = registry.Create(item, Vec2.Zero);
                var type = registry.Type(item.TypeId)!;
                if (probe.IsPointGeometry) media = type.LineMedium(probe) is { } m ? new[] { m } : Array.Empty<string>();
                else
                {
                    var tmp = SketchDocument.Create();
                    tmp = Edits.Add(tmp, tmp.Pages[0].Id, probe);
                    media = registry.PageAnchors(probe, new RenderContext(tmp)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id is not null)
                        .Select(a => type.Connection(probe, a)?.Medium).Where(m => m is not null).Select(m => m!).Distinct().ToArray();
                }
            }
            catch (Exception)
            {
                media = Array.Empty<string>();
            }
            lock (cache) cache[item.Id] = media;
            return media;
        }

        /// <summary>Port anchors (page coordinates) of a free-standing element whose medium fits <paramref name="medium"/>.</summary>
        private static List<(Anchor Anchor, PortConnection Connection)> ElementPorts(Registry registry, SketchObject probe, string medium, bool exact = false)
        {
            var list = new List<(Anchor, PortConnection)>();
            if (probe.IsPointGeometry || registry.Type(probe.Type) is not { } type) return list;
            var tmp = SketchDocument.Create();
            tmp = Edits.Add(tmp, tmp.Pages[0].Id, probe);
            foreach (var a in registry.PageAnchors(probe, new RenderContext(tmp)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id is not null))
                if (type.Connection(probe, a) is { } c && (exact ? c.Medium == medium : PortMedia.Fits(c.Medium, medium))) list.Add((a, c));
            return list;
        }

        /// <summary>
        /// The element <paramref name="item"/> placed with one of its ports of <paramref name="medium"/> on <paramref name="at"/>:
        /// the port of exactly that medium first, then the one facing back along <paramref name="dir"/>; moved further along
        /// <paramref name="dir"/> while it would overlap <paramref name="avoid"/>. Its scale follows <paramref name="scaleFrom"/>.
        /// </summary>
        private static (SketchObject Obj, Anchor Port)? PlaceElement(Registry registry, LibraryItem item, Vec2 at, Vec2 dir, string medium, SketchObject? avoid, SketchObject scaleFrom)
        {
            var probe = registry.Create(item, at);
            if (probe.IsPointGeometry) return null;
            if (!scaleFrom.IsPointGeometry && SourceScale(registry, scaleFrom) is { } sc && Math.Sqrt(sc.X * sc.Y) is var k && k > 1e-6 && Math.Abs(k - 1) > 1e-3
                && ModuleOf(registry, scaleFrom.Type) == ModuleOf(registry, item.TypeId))
                probe = probe with { Size = new Size2(Math.Round(probe.Size.W * k, 6), Math.Round(probe.Size.H * k, 6)) };
            var ports = ElementPorts(registry, probe, medium);
            if (ports.Count == 0) return null;
            var back = dir * -1;
            var best = ports.OrderByDescending(p => (p.Connection.Medium == medium ? 2 : 0) + OutDir(probe, p.Anchor.P).Dot(back)).First();
            var placed = Translate(probe, at - best.Anchor.P);
            var shift = Vec2.Zero;
            if (avoid is not null && !avoid.IsPointGeometry)
            {
                var keep = avoid.Bounds().Inflate(1);
                var step = Math.Max(2, ConnectorLength(avoid) * 0.5);
                for (var i = 0; i < 12 && placed.Bounds().Intersects(keep); i++)
                {
                    placed = Translate(placed, dir * step);
                    shift += dir * step;
                }
            }
            return (placed, best.Anchor with { P = best.Anchor.P + shift });
        }

        /// <summary>
        /// What "+" builds with <paramref name="item"/> at a connection port or a line end: the object (connector, element,
        /// or the extended line), the element's port it docks with, and the connector joining an element to the port.
        /// </summary>
        private static (SketchObject Obj, Anchor? ElementPort, SketchObject? Connector)? PlaceConnection(Registry registry, SketchDocument doc, SketchObject sel, BuildPort port, LibraryItem item, Lang lang)
        {
            var type = registry.Type(item.TypeId);
            if (type is null) return null;
            if (port.Kind == PortKind.LineEnd)
            {
                if (port.Medium is null) return null;
                if (item.TypeId == sel.Type)
                {
                    // Continue the line by one segment along its end direction.
                    var last = port.At.DistanceTo(port.AxisFrom);
                    var len = Math.Max(8, Math.Min(30, last));
                    var atStart = port.AnchorId == "p0";
                    var (ext, _) = Polylines.Extend(registry, doc, sel.Id, atStart, port.At + port.Dir * len, null, lang);
                    return ext.Get(sel.Id) is { } longer ? (longer, null, null) : null;
                }
                if (type.Geometry != GeometryKind.Box) return null;
                var el = PlaceElement(registry, item, port.At, port.Dir, port.Medium, null, sel);
                return el is { } e ? (e.Obj, e.Port, null) : null;
            }
            if (ConnectionOf(registry, doc, sel, port.AnchorId, lang) is not { } conn) return null;
            var (c, anchor) = conn;
            var length = ConnectorLength(sel);
            if (type.Geometry == GeometryKind.Points)
            {
                var line = Connector(registry, c, item, port.At, port.At + port.Dir * length);
                if (line is null) return null;
                line = line with { Attach = AttachEnds(line, Attachments.For(sel, port.At, anchor), null) };
                return (line, null, null);
            }
            // The next element: one connector length away, joined by the port's connector.
            var placed = PlaceElement(registry, item, port.At + port.Dir * length, port.Dir, c.Medium, sel, sel);
            if (placed is not { } p) return null;
            var connectorItem = Connectors(registry, c).FirstOrDefault();
            SketchObject? connector = null;
            if (connectorItem is not null && Connector(registry, c, connectorItem, port.At, p.Port.P) is { } wire)
                connector = wire with { Attach = AttachEnds(wire, Attachments.For(sel, port.At, anchor), Attachments.For(p.Obj, p.Port.P, p.Port)) };
            return (p.Obj, p.Port, connector);
        }

        /// <summary>A connector line of <paramref name="item"/> from <paramref name="a"/> to <paramref name="b"/> (declared parameters applied), or null if it does not carry the medium.</summary>
        private static SketchObject? Connector(Registry registry, PortConnection c, LibraryItem item, Vec2 a, Vec2 b)
        {
            var type = registry.Type(item.TypeId)!;
            if (type.Geometry != GeometryKind.Points) return null;
            var declared = IsDeclared(registry, c, item);
            var line = registry.CreateBetween(item, a, b);
            if (declared && c.ConnectorParams is not null)
                foreach (var kv in c.ConnectorParams)
                    if (type.Parameters.Any(p => p.Key == kv.Key)) line = line.WithParam(kv.Key, kv.Value);
            if (!declared && !PortMedia.Covers(type.LineMedium(line), c.Medium)) return null;
            return line;
        }

        private static IReadOnlyList<Attachment?> AttachEnds(SketchObject line, Attachment? start, Attachment? end)
        {
            var n = line.Points?.Count ?? 2;
            var att = new Attachment?[n];
            att[0] = start;
            att[n - 1] = end;
            return att;
        }
    }
}
