using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Engine methods for the handles the Office web add-in draws on the stage, so it can offer what the PowerPoint COM add-in
    /// does in process (docs/en/hosts/office-web.md, "Handles on the drawing"):
    /// <list type="bullet">
    /// <item><c>links</c> {id | ids}: places where the elements touch others and follow them (<see cref="Links.For"/>), each
    /// with a key, the contact point, released or not and a bilingual description.</item>
    /// <item><c>toggleLink</c> {key, id | ids}: releases a coupled link or couples a released one (<see cref="Links.Toggle"/>);
    /// one undo step; the selection stays.</item>
    /// <item><c>polylineHandles</c> {id}: corner dots and branch handles of a line with free corners, the removable and
    /// splittable corners (<see cref="Polylines"/>).</item>
    /// <item><c>insertCorner</c> {id, index (segment), x, y, points [[x0, y0]], snap, tolerance, anchor}: a new corner on
    /// segment <c>index</c>, started at the dot (x0, y0) and moved to x/y (snapped, docked); branches stay; one undo step.</item>
    /// <item><c>removeCorner</c> {id, index}, <c>splitLine</c> {id, index}: one undo step each.</item>
    /// <item><c>branch</c> {id, points [[fx, fy], [ex, ey]], snap, tolerance, anchor}: a new line of the same kind from the
    /// line's rail at (fx, fy) to (ex, ey), the end snapped and docked; one undo step, the branch selected.</item>
    /// <item><c>joinLines</c> {id, index, x, y, snap, tolerance}: the end <c>index</c> dropped at x/y joins a free end of a
    /// line of the same kind lying there: <c>{ joined, state }</c>; nothing changes when there is none.</item>
    /// <item><c>insertCalcTable</c> {ids, x, y, digits}: the calculation sheet of <c>ids</c> as an annotation table (cells of
    /// <c>calcSheet</c> mode "table") right of the elements (or at x/y); one undo step, the table selected.</item>
    /// <item><c>lineEnds</c> {id}: the free ends of a line that can be drawn on (<see cref="Polylines.CanExtend"/>), with the
    /// outward direction.</item>
    /// <item><c>extendLine</c> {id, index (an end), points [[x, y] …], snap, tolerance, anchor, preview}: draws the line on from
    /// that end through the points (<see cref="Polylines.Extend"/>; the last one snapped and docked like a line end); one undo
    /// step, the line selected; with <c>preview</c> only <c>{ svg }</c> of the line as it would be.</item>
    /// <item><c>crossings</c> {id}: where a wire or pipe crosses another one (<c>Crossings.Find</c>);
    /// <c>connectCrossing</c> {id, index}: connects it there (<see cref="Crossings.Connect"/>), one undo step.</item>
    /// <item><c>insertMediumLegend</c> {json (colour ids, default: those used), x, y}: the legend of the medium colours
    /// (<see cref="LineMedia.Legend"/>) as one group, top right; <c>{ inserted, state }</c>, one undo step.</item>
    /// <item><c>valueSettings</c>: the drawing's muted checks and preferred units (Calc 6).</item>
    /// </list>
    /// </summary>
    public sealed partial class EngineApi
    {
        private void RegisterWebMethods()
        {
            _methods["lineEnds"] = LineEndsCore;
            _methods["extendLine"] = ExtendLineCore;
            _methods["crossings"] = CrossingsCore;
            _methods["connectCrossing"] = ConnectCrossingCore;
            _methods["insertMediumLegend"] = InsertMediumLegendCore;
            _methods["valueSettings"] = ValueSettingsCore;
            _methods["links"] = LinksCore;
            _methods["toggleLink"] = ToggleLinkCore;
            _methods["polylineHandles"] = PolylineHandlesCore;
            _methods["insertCorner"] = InsertCornerCore;
            _methods["removeCorner"] = RemoveCornerCore;
            _methods["splitLine"] = SplitLineCore;
            _methods["branch"] = BranchCore;
            _methods["joinLines"] = JoinLinesCore;
            _methods["insertCalcTable"] = InsertCalcTableCore;
        }

        // ------------------------------------------------------------------ links (lock badges)

        private static string LinkKey(Link l) => $"{Camel(l.Kind.ToString())}:{l.A}:{l.B}:{l.Index.ToString(CultureInfo.InvariantCulture)}";

        private IReadOnlyList<Link> LinksOf(EditorSession s, EngineRequest r, Lang lang)
        {
            var ids = r.Id is not null ? new List<string> { r.Id } : s.Resolve(r.Ids).ToList();
            return ids.Count == 0 ? Array.Empty<Link>() : Links.For(Registry, s.Doc, ids, lang);
        }

        private object LinksCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var ids = r.Id is not null ? new List<string> { r.Id } : s.Resolve(r.Ids).ToList();
            return LinksOf(s, r, lang).Select(l => new LinkInfo
            {
                Key = LinkKey(l), Kind = Camel(l.Kind.ToString()), A = l.A, B = l.B, X = l.At.X, Y = l.At.Y, Released = l.Released, Index = l.Index,
                Label = Links.Describe(Registry, s.Doc, l, ids.Contains(l.A) ? l.A : l.B, lang),
            }).ToList();
        }

        private object ToggleLinkCore(EngineRequest r)
        {
            var s = S(r);
            var key = r.Key ?? throw new ArgumentException("Missing 'key'.");
            var link = LinksOf(s, r, s.Lang).FirstOrDefault(l => LinkKey(l) == key)
                       ?? Links.For(Registry, s.Doc, s.Doc.ObjectsOn(s.Page).Select(o => o.Id), s.Lang).FirstOrDefault(l => LinkKey(l) == key)
                       ?? throw new ArgumentException("Unknown link: " + key);
            s.Apply(link.Released ? "Couple" : "Release", d => Links.Toggle(d, link));
            return StateOf(s, r);
        }

        // ------------------------------------------------------------------ corners and branches

        private SketchObject Line(EditorSession s, EngineRequest r)
        {
            var o = s.Doc.Get(r.Id ?? s.Selection.FirstOrDefault() ?? "") ?? throw new KeyNotFoundException($"Unknown object '{r.Id}'.");
            if (!o.IsPointGeometry) throw new ArgumentException($"'{o.Id}' is not a line.");
            return o;
        }

        private object PolylineHandlesCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            var info = new PolylineHandlesInfo();
            if (!Polylines.CanInsert(Registry, o)) return info;
            var doc = s.Doc;
            var pts = doc.ResolvePoints(o);
            var n = pts.Count;
            info.Corners = true;
            info.Closed = Polylines.IsClosed(Registry, o);
            info.Branch = Polylines.CanBranch(Registry, doc, o, lang);
            var segments = Polylines.SegmentCount(Registry, doc, o);
            for (var i = 0; i < segments; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                var d = b - a;
                if (d.Length < 1e-6) continue;
                var nrm = new Vec2(-d.Y, d.X) * (1 / d.Length);
                // The branch handle sits above (or left of) the segment, as in the COM add-in.
                if (nrm.Y > 1e-9 || (Math.Abs(nrm.Y) <= 1e-9 && nrm.X > 0)) nrm = nrm * -1;
                var at = Polylines.SegmentHandle(Registry, doc, o, i, lang);
                info.Segments.Add(new SegmentHandleInfo { Index = i, X = at.X, Y = at.Y, Nx = nrm.X, Ny = nrm.Y, Ax = a.X, Ay = a.Y, Bx = b.X, By = b.Y });
            }
            for (var i = 0; i < n; i++)
            {
                var inner = info.Closed || (i > 0 && i < n - 1);
                if (!inner) continue;
                info.Splittable.Add(i);
                if (n > (info.Closed ? 3 : 2)) info.Removable.Add(i);
            }
            return info;
        }

        /// <summary>Snaps a line point like <c>setPoint</c> (the line itself excluded) and returns where it lands and what it docks to.</summary>
        private (Vec2 P, Attachment? Attach) SnapLinePoint(EditorSession s, SketchObject line, Vec2 p, EngineRequest r)
        {
            if (r.Snap != true) return (p, null);
            var sp = SnapPointAt(s, p, new[] { line.Id }, r.Tolerance);
            var q = new Vec2(sp.X, sp.Y);
            var attach = r.Anchor == false ? null : sp.Attach;
            if (attach is null && r.Anchor != false && Registry.Type(line.Type)?.DocksOnSurface == true)
                attach = Attachments.OnSurface(q, s.Doc.ObjectsOn(s.Page).Where(x => x.Id != line.Id));
            return (q, attach);
        }

        private object InsertCornerCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            if (!Polylines.CanInsert(Registry, o)) throw new ArgumentException($"'{o.Type}' takes no corners.");
            var seg = r.Index ?? throw new ArgumentException("Missing 'index' (segment).");
            var target = new Vec2(r.X ?? 0, r.Y ?? 0);
            var start = r.Points is { Count: > 0 } ps && ps[0].Length >= 2 ? new Vec2(ps[0][0], ps[0][1]) : target;
            var (at, attach) = SnapLinePoint(s, o, target, r);
            s.Apply("Corner", d =>
            {
                var next = Polylines.InsertCorner(Registry, d, o.Id, seg + 1, start, lang);
                if (ReferenceEquals(next, d)) return d;
                return (at - start).Length > 1e-9 || attach is not null ? Edits.SetPoint(next, o.Id, seg + 1, at, attach) : next;
            });
            return StateOf(s, r);
        }

        private object RemoveCornerCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            var index = r.Index ?? throw new ArgumentException("Missing 'index'.");
            s.Apply("Remove corner", d => Polylines.RemoveCorner(Registry, d, o.Id, index, lang));
            return StateOf(s, r);
        }

        private object SplitLineCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            var index = r.Index ?? throw new ArgumentException("Missing 'index'.");
            s.Apply("Split", d => Polylines.Split(Registry, d, o.Id, index, lang).Doc);
            return StateOf(s, r);
        }

        private object BranchCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            if (r.Points is not { Count: >= 2 } ps || ps[0].Length < 2 || ps[1].Length < 2) throw new ArgumentException("Missing 'points' ([[fromX, fromY], [endX, endY]]).");
            var from = new Vec2(ps[0][0], ps[0][1]);
            var (end, attach) = SnapLinePoint(s, o, new Vec2(ps[1][0], ps[1][1]), r);
            string? branchId = null;
            s.Apply("Branch", d =>
            {
                var (next, id) = Polylines.Branch(Registry, d, o.Id, from, end, attach, lang);
                branchId = id;
                return id is null ? d : next;
            });
            if (branchId is not null) s.Select(new[] { branchId });
            return StateOf(s, r);
        }

        private object JoinLinesCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            var index = r.Index ?? throw new ArgumentException("Missing 'index'.");
            var n = o.Points!.Count;
            if ((index != 0 && index != n - 1) || !Polylines.CanExtend(Registry, o)) return new { joined = false, state = (EditorState?)null };
            var at = new Vec2(r.X ?? 0, r.Y ?? 0);
            if (r.Snap == true) { var sp = SnapPointAt(s, at, new[] { o.Id }, r.Tolerance); at = new Vec2(sp.X, sp.Y); }
            // A free end of a line of the same kind exactly there (as the COM add-in's join on drop).
            (string Id, bool AtStart)? target = null;
            foreach (var other in s.Doc.ObjectsOn(s.Page).Where(x => x.Id != o.Id && x.Type == o.Type && x.Points is { Count: >= 2 }))
            {
                if (!Polylines.CanExtend(Registry, other)) continue;
                var pts = s.Doc.ResolvePoints(other);
                foreach (var atStart in new[] { true, false })
                {
                    var k = atStart ? 0 : pts.Count - 1;
                    var docked = other.Attach is not null && k < other.Attach.Count && other.Attach[k] is not null;
                    if (!docked && pts[k].DistanceTo(at) < 0.05) { target = (other.Id, atStart); break; }
                }
                if (target is not null) break;
            }
            if (target is not { } t) return new { joined = false, state = (EditorState?)null };
            var joined = s.Apply("Join", d => Polylines.Join(Registry, Edits.SetPoint(d, o.Id, index, at), o.Id, index == 0, t.Id, t.AtStart, lang));
            if (joined) s.Select(new[] { o.Id });
            return new { joined, state = (EditorState?)StateOf(s, r) };
        }

        // ------------------------------------------------------------------ draw on from a free end

        private object LineEndsCore(EngineRequest r)
        {
            var s = S(r);
            var list = new List<LineEndInfo>();
            var o = s.Doc.Get(r.Id ?? s.Selection.FirstOrDefault() ?? "");
            if (o is null || !Polylines.CanExtend(Registry, o)) return list;
            var pts = s.Doc.ResolvePoints(o);
            if (pts.Count < 2) return list;
            foreach (var atStart in new[] { true, false })
            {
                var i = atStart ? 0 : pts.Count - 1;
                var docked = o.Attach is not null && i < o.Attach.Count && o.Attach[i] is not null;
                var dir = pts[i] - pts[atStart ? 1 : pts.Count - 2];
                if (docked || dir.Length < 1e-6) continue;
                var u = dir * (1 / dir.Length);
                list.Add(new LineEndInfo { Index = i, AtStart = atStart, X = pts[i].X, Y = pts[i].Y, Dx = u.X, Dy = u.Y });
            }
            return list;
        }

        private object ExtendLineCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            if (!Polylines.CanExtend(Registry, o)) throw new ArgumentException($"'{o.Type}' cannot be drawn on.");
            var n = o.Points!.Count;
            var index = r.Index ?? n - 1;
            if (index != 0 && index != n - 1) throw new ArgumentException("'index' must be an end of the line (0 or the last point).");
            if (r.Points is not { Count: > 0 } raw || raw.Any(p => p.Length < 2)) throw new ArgumentException("Missing 'points' ([[x, y], …]).");
            var pts = raw.Select(p => new Vec2(p[0], p[1])).ToList();
            var (last, attach) = SnapLinePoint(s, o, pts[pts.Count - 1], r);
            pts[pts.Count - 1] = last;
            SketchDocument Extend(SketchDocument d)
            {
                for (var i = 0; i < pts.Count; i++)
                    d = Polylines.Extend(Registry, d, o.Id, index == 0, pts[i], i == pts.Count - 1 ? attach : null, lang).Doc;
                return d;
            }
            if (r.Preview == true)
            {
                var doc = Extend(s.Doc);
                var line = doc.Get(o.Id);
                return new { svg = line is null ? "" : SvgRenderer.RenderPrimitives(Registry.Render(line, new RenderContext(doc, lang))), x = last.X, y = last.Y };
            }
            s.Apply("Draw on", Extend);
            s.Select(new[] { o.Id });
            return StateOf(s, r);
        }

        // ------------------------------------------------------------------ crossings of wires and pipes

        private object CrossingsCore(EngineRequest r)
        {
            var s = S(r);
            var o = s.Doc.Get(r.Id ?? s.Selection.FirstOrDefault() ?? "");
            if (o is null || !o.IsPointGeometry) return new List<CrossingInfo>();
            return Crossings.Find(Registry, s.Doc, o, LangOf(r, s))
                .Select((c, i) => new CrossingInfo { Index = i, Other = c.Other, X = c.At.X, Y = c.At.Y, Above = c.Above }).ToList();
        }

        private object ConnectCrossingCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var o = Line(s, r);
            var index = r.Index ?? throw new ArgumentException("Missing 'index' (crossing).");
            var list = Crossings.Find(Registry, s.Doc, o, lang);
            if (index < 0 || index >= list.Count) throw new ArgumentException("Unknown crossing: " + index.ToString(CultureInfo.InvariantCulture));
            var crossing = list[index];
            s.Apply("Connect", d => Crossings.Connect(Registry, d, o.Id, crossing, lang));
            s.Select(new[] { o.Id });
            return StateOf(s, r);
        }

        // ------------------------------------------------------------------ colour by medium: legend

        /// <summary>Distance of the legend from the right page edge and from the top (mm), as in the COM add-in.</summary>
        private const double LegendFromRightMm = 75, LegendTopMm = 12;

        private object InsertMediumLegendCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            IReadOnlyList<MediumColour> entries;
            if (!string.IsNullOrWhiteSpace(r.Json))
            {
                var ids = new HashSet<string>(System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.Json!) ?? new List<string>(), StringComparer.Ordinal);
                entries = LineMedia.Scheme.Where(c => ids.Contains(c.Id)).ToList();
            }
            else entries = LineMedia.Used(Registry, s.Doc, lang);
            var page = s.Page.Size;
            var at = r.X is double x && r.Y is double y ? new Vec2(x, y) : new Vec2(Math.Max(2, page.W - LegendFromRightMm), LegendTopMm);
            var objects = entries.Count == 0 ? Array.Empty<SketchObject>() : LineMedia.Legend(Registry, entries, at, lang);
            if (objects.Count == 0) return new { inserted = false, state = StateOf(s, r) };
            // One group: the legend moves as a whole.
            s.Apply("Legend", d => Edits.Group(Edits.Add(d, s.PageId, objects.ToArray()), objects.Select(o => o.Id).ToList()));
            s.Select(objects.Select(o => o.Id).ToList());
            return new { inserted = true, state = StateOf(s, r) };
        }

        // ------------------------------------------------------------------ Calc 6 settings

        private object ValueSettingsCore(EngineRequest r)
        {
            var s = S(r);
            return new
            {
                mutedChecks = s.Doc.Settings.MutedChecks ?? Array.Empty<string>(),
                unitPreferences = s.Doc.Settings.UnitPreferences ?? new Dictionary<string, string>(),
            };
        }

        // ------------------------------------------------------------------ calculation sheet as a table

        /// <summary>Row height (mm) of the inserted calculation table and the width per character of its widest cells.</summary>
        private const double CalcRowMm = 6, CalcCharMm = 1.8, CalcGapMm = 8;

        private object InsertCalcTableCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var ids = ValueIds(r) ?? s.Page.Order.ToList();
            var table = SolveValues(s, r);
            var rows = InventaSketch.Core.CalcSheet.Rows(table, Registry, ids, lang, r.Digits ?? 3);
            if (rows.Count == 0) throw new ArgumentException(lang == Lang.De ? "Keine Werte für ein Rechenblatt." : "No values for a calculation sheet.");
            var cells = InventaSketch.Core.CalcSheet.TableCells(rows, lang);
            var item = Registry.AllItems.FirstOrDefault(i => i.TypeId == "ann.table") ?? throw new InvalidOperationException("The annotation table is not available.");
            var grid = cells.Split('\n').Select(l => l.Split('|')).ToList();
            var cols = grid.Max(g => g.Length);
            var width = 0.0;
            for (var c = 0; c < cols; c++) width += 3 + CalcCharMm * grid.Max(g => c < g.Length ? g[c].Length : 0);
            var page = s.Page.Size;
            var w = Math.Max(40, Math.Min(width, Math.Max(40, page.W - 10)));
            var h = CalcRowMm * grid.Count;
            Vec2 centre;
            if (r.X is double x && r.Y is double y) centre = new Vec2(x, y);
            else
            {
                var b = s.Bounds(ids.Where(id => s.Doc.Get(id) is not null)) ?? new Rect(page.W / 2, page.H / 2, 0, 0);
                // Right of the elements; below them when it would leave the page.
                centre = b.Right + CalcGapMm + w <= page.W ? new Vec2(b.Right + CalcGapMm + w / 2, b.Y + h / 2) : new Vec2(b.X + w / 2, b.Bottom + CalcGapMm + h / 2);
                centre = new Vec2(Math.Max(w / 2 + 2, Math.Min(centre.X, page.W - w / 2 - 2)), Math.Max(h / 2 + 2, centre.Y));
            }
            var o = Registry.Create(item, centre) with { Size = new Size2(w, h) };
            o = Registry.SetParam(o, "cells", new ParamValue { Text = cells });
            s.Apply("Calculation sheet", d => Edits.Add(d, s.PageId, o));
            s.Select(new[] { o.Id });
            return StateOf(s, r);
        }
    }
}
