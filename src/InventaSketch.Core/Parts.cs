using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// The look of one part of an element (a curve, an axis, the grid …): a style that wins over the drawn one, or hidden.
    /// Stored in <see cref="SketchObject.Parts"/> under the part key (see <see cref="PartLooks"/>).
    /// </summary>
    public sealed record PartLook
    {
        public Style? Style { get; init; }
        public bool Hidden { get; init; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty => !Hidden && (Style is null || Style == new Style());
    }

    /// <summary>A selectable part of an element: key (the primitive tag, e.g. "series.0"), label and kind (e.g. "series").</summary>
    public sealed record PartInfo(string Key, I18nText Label, string Kind)
    {
        /// <summary>True while the part is switched off (it can still be selected from a list and shown again).</summary>
        public bool Hidden { get; init; }
    }

    /// <summary>
    /// Parts of elements (charts v2, docs/en/design/charts-v2.md): every primitive carries a <see cref="Primitive.Tag"/>;
    /// that tag is the part key. A look stored under a key applies to the primitives with that tag, a key without its last
    /// ".segment" to all of them ("series" for "series.0", "series.1" …); the longer key wins. Groups pass their tag on to
    /// children without one. Applied in <see cref="Registry.RenderLocal"/>, the one render path of every host.
    /// </summary>
    public static class PartLooks
    {
        /// <summary>The look for <paramref name="key"/>: the exact key, else its shorter prefixes ("a.b.c" → "a.b" → "a").</summary>
        public static PartLook? LookOf(IReadOnlyDictionary<string, PartLook>? parts, string? key)
        {
            if (parts is null || parts.Count == 0 || string.IsNullOrEmpty(key)) return null;
            for (var k = key!; ; )
            {
                if (parts.TryGetValue(k, out var look)) return look;
                var dot = k.LastIndexOf('.');
                if (dot <= 0) return null;
                k = k.Substring(0, dot);
            }
        }

        /// <summary>The primitives with the part looks applied (hidden parts left out).</summary>
        public static IReadOnlyList<Primitive> Apply(IReadOnlyList<Primitive> list, IReadOnlyDictionary<string, PartLook>? parts)
        {
            if (parts is null || parts.Count == 0) return list;
            var result = new List<Primitive>(list.Count);
            foreach (var p in list)
                if (ApplyOne(p, parts, null) is { } q) result.Add(q);
            return result;
        }

        private static Primitive? ApplyOne(Primitive p, IReadOnlyDictionary<string, PartLook> parts, string? inherited)
        {
            var key = p.Tag ?? inherited;
            var look = LookOf(parts, key);
            if (look?.Hidden == true) return null;
            if (p is GroupPrim g)
            {
                var children = g.Children.Select(c => ApplyOne(c, parts, key)).Where(c => c is not null).Select(c => c!).ToList();
                return children.Count == 0 ? null : g with { Children = children };
            }
            if (look?.Style is not { } s) return p;
            var styled = Prim.Restyle(p, d => d with
            {
                Stroke = s.Stroke ?? d.Stroke,
                StrokeWidth = s.StrokeWidth ?? d.StrokeWidth,
                Dash = s.Dash ?? d.Dash,
                Fill = s.Fill ?? d.Fill,
                Opacity = s.Opacity ?? d.Opacity,
                ArrowStart = s.ArrowStart ?? d.ArrowStart,
                ArrowEnd = s.ArrowEnd ?? d.ArrowEnd,
                ArrowSize = s.ArrowSize ?? d.ArrowSize,
            });
            return Prim.TextOverride(styled, s);
        }

        /// <summary>The object with the look of <paramref name="key"/> changed (an empty look is removed).</summary>
        public static SketchObject WithLook(SketchObject o, string key, Func<PartLook, PartLook> change)
        {
            var d = o.Parts is null ? new Dictionary<string, PartLook>(StringComparer.Ordinal) : new Dictionary<string, PartLook>(o.Parts.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal);
            var next = change(d.TryGetValue(key, out var cur) ? cur : new PartLook());
            if (next.IsEmpty) d.Remove(key); else d[key] = next;
            return o with { Parts = d.Count == 0 ? null : d };
        }

        /// <summary>The part key of the primitive nearest to <paramref name="p"/> within <paramref name="tolerance"/> (texts first, then lines, then areas; the topmost on a tie); null if none.</summary>
        public static string? At(IEnumerable<Primitive> primitives, Vec2 p, double tolerance, ICollection<string>? keys = null)
        {
            string? best = null;
            var bestRank = int.MaxValue;
            var bestDist = double.MaxValue;
            void Visit(Primitive prim, string? inherited)
            {
                var key = prim.Tag ?? inherited;
                if (prim is GroupPrim g) { foreach (var c in g.Children) Visit(c, key); return; }
                if (key is null || (keys is not null && !Known(keys, key))) return;
                var (dist, rank) = Distance(prim, p);
                if (dist > tolerance) return;
                // Equal rank and distance: the later (topmost) primitive wins, as the user sees it.
                if (rank < bestRank || rank == bestRank && dist <= bestDist + 1e-6) { best = Resolve(keys, key); bestRank = rank; bestDist = dist; }
            }
            foreach (var prim in primitives) Visit(prim, null);
            return best;
        }

        private static bool Known(ICollection<string> keys, string key) => Resolve(keys, key) is not null;

        private static string? Resolve(ICollection<string>? keys, string key)
        {
            if (keys is null) return key;
            for (var k = key; ; )
            {
                if (keys.Contains(k)) return k;
                var dot = k.LastIndexOf('.');
                if (dot <= 0) return null;
                k = k.Substring(0, dot);
            }
        }

        /// <summary>Distance from <paramref name="q"/> to a primitive and its hit rank (0 text, 1 line, 2 area inside).</summary>
        public static (double Distance, int Rank) Distance(Primitive prim, Vec2 q)
        {
            switch (prim)
            {
                case TextPrim:
                    {
                        var b = Rect.FromPoints(Prim.Points(prim));
                        return (OutsideDistance(b, q), 0);
                    }
                case LinePrim l:
                    return (Geo.ClosestOnSegment(q, l.A, l.B).Point.DistanceTo(q), 1);
                case RectPrim r:
                    {
                        var rect = new Rect(r.X, r.Y, r.W, r.H);
                        var edge = Outline(new[] { new Vec2(r.X, r.Y), new Vec2(r.X + r.W, r.Y), new Vec2(r.X + r.W, r.Y + r.H), new Vec2(r.X, r.Y + r.H), new Vec2(r.X, r.Y) }, q);
                        return Filled(r.Style) && OutsideDistance(rect, q) == 0 ? (Math.Min(edge, 0), edge < 1e-9 ? 1 : 2) : (edge, 1);
                    }
                case EllipsePrim e:
                    {
                        var pts = Geo.ArcPoints(e.Center, e.Rx, e.Ry, 0, 2 * Math.PI, 72);
                        var edge = Outline(pts, q);
                        var inside = e.Rx > 0 && e.Ry > 0 && Math.Pow((q.X - e.Center.X) / e.Rx, 2) + Math.Pow((q.Y - e.Center.Y) / e.Ry, 2) <= 1;
                        return Filled(e.Style) && inside ? (0, 2) : (edge, 1);
                    }
                case PathPrim path:
                    {
                        var pts = Sample(path.Segments);
                        var edge = pts.Count < 2 ? double.MaxValue : Outline(pts, q);
                        if (path.IsClosed && Filled(path.Style) && edge > 1e-9 && Inside(pts, q)) return (0, 2);
                        return (edge, 1);
                    }
                default:
                    return (double.MaxValue, 9);
            }
        }

        private static bool Filled(Style s) => s.Fill is { } f && f.Kind != FillKind.None;

        private static double OutsideDistance(Rect r, Vec2 q)
        {
            var dx = Math.Max(Math.Max(r.X - q.X, 0), q.X - (r.X + r.W));
            var dy = Math.Max(Math.Max(r.Y - q.Y, 0), q.Y - (r.Y + r.H));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Outline(IReadOnlyList<Vec2> pts, Vec2 q)
        {
            var best = double.MaxValue;
            for (var i = 1; i < pts.Count; i++) best = Math.Min(best, Geo.ClosestOnSegment(q, pts[i - 1], pts[i]).Point.DistanceTo(q));
            return best;
        }

        private static bool Inside(IReadOnlyList<Vec2> poly, Vec2 q)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y) && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            return inside;
        }

        /// <summary>Path segments as points (curves sampled; a close adds the start point again).</summary>
        public static List<Vec2> Sample(IReadOnlyList<PathSeg> segs, int steps = 8)
        {
            var pts = new List<Vec2>();
            Vec2 cur = default, start = default;
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case SegOp.M: cur = start = s.P; pts.Add(cur); break;
                    case SegOp.L: cur = s.P; pts.Add(cur); break;
                    case SegOp.C:
                        for (var i = 1; i <= steps; i++)
                        {
                            var t = (double)i / steps; var u = 1 - t;
                            pts.Add(cur * (u * u * u) + s.C1 * (3 * u * u * t) + s.C2 * (3 * u * t * t) + s.P * (t * t * t));
                        }
                        cur = s.P; break;
                    case SegOp.Q:
                        for (var i = 1; i <= steps; i++)
                        {
                            var t = (double)i / steps; var u = 1 - t;
                            pts.Add(cur * (u * u) + s.C1 * (2 * u * t) + s.P * (t * t));
                        }
                        cur = s.P; break;
                    case SegOp.Z: pts.Add(start); cur = start; break;
                }
            }
            return pts;
        }

        /// <summary>The stroke width the part is drawn with now (first primitive with that key), or null.</summary>
        public static double? DrawnWidth(IEnumerable<Primitive> primitives, string key)
        {
            foreach (var p in Prim.Flatten(primitives))
                if (p.Tag is { } t && (t == key || t.StartsWith(key + ".", StringComparison.Ordinal)))
                    switch (p)
                    {
                        case LinePrim l: return l.Style.EffectiveWidth;
                        case PathPrim a: return a.Style.EffectiveWidth;
                        case EllipsePrim e: return e.Style.EffectiveWidth;
                        case RectPrim r: return r.Style.EffectiveWidth;
                    }
            return null;
        }

        private static readonly DashStyle[] DashCycle = { DashStyle.Solid, DashStyle.Dash, DashStyle.Dot, DashStyle.DashDot };

        /// <summary>
        /// Actions every part offers: show/hide, thicker, thinner, next dash style, reset (its own look removed). Type actions
        /// come first (<see cref="ObjectType.PartActions"/>), these after them.
        /// </summary>
        public static IEnumerable<ContextAction> Generic(SketchObject o, string key, double? drawnWidth)
        {
            var look = o.Parts is not null && o.Parts.TryGetValue(key, out var l) ? l : new PartLook();
            yield return new ContextAction("part.hide", look.Hidden ? new I18nText("Show", "Einblenden") : new I18nText("Hide", "Ausblenden"), IconIds.Toggle,
                x => WithLook(x, key, p => p with { Hidden = !p.Hidden })) { IsActive = look.Hidden, Group = "part" };
            var w = look.Style?.StrokeWidth ?? drawnWidth ?? 0.35;
            yield return new ContextAction("part.thicker", new I18nText("Thicker", "Dicker"), IconIds.Increase,
                x => WithLook(x, key, p => p with { Style = (p.Style ?? new Style()) with { StrokeWidth = Math.Round(Math.Min(5, w * 1.5), 3) } })) { Group = "part" };
            yield return new ContextAction("part.thinner", new I18nText("Thinner", "Dünner"), IconIds.Decrease,
                x => WithLook(x, key, p => p with { Style = (p.Style ?? new Style()) with { StrokeWidth = Math.Round(Math.Max(0.05, w / 1.5), 3) } })) { Group = "part" };
            var dash = look.Style?.Dash ?? DashStyle.Solid;
            var nextDash = DashCycle[(Array.IndexOf(DashCycle, dash) + 1 + DashCycle.Length) % DashCycle.Length];
            yield return new ContextAction("part.dash", new I18nText("Line style", "Strichart"), IconIds.LineDash,
                x => WithLook(x, key, p => p with { Style = (p.Style ?? new Style()) with { Dash = nextDash } })) { Group = "part" };
            if (!look.IsEmpty)
                yield return new ContextAction("part.reset", new I18nText("Reset look", "Aussehen zurücksetzen"), IconIds.Cycle,
                    x => WithLook(x, key, _ => new PartLook())) { Group = "part" };
        }
    }
}
