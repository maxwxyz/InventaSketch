using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// An annotation line that acts as a connector (elbow connector, polyline with an arrowhead or a docked end): it hops
    /// over earlier connectors it crosses, and other connectors hop over it.
    /// </summary>
    public interface IAnnConnector
    {
        /// <summary>The drawn path of <paramref name="o"/> in page coordinates, or null when it is no connector (e.g. a plain polyline).</summary>
        IReadOnlyList<Vec2>? ConnectorPath(SketchObject o, RenderContext ctx);
    }

    /// <summary>
    /// Shared behaviour of annotation connectors: rails for T junctions with junction dots, hops at crossings (the
    /// <see cref="Crossings"/> rule of wires and pipes, applied among annotation connectors only), bus marks (number of
    /// lines, IEC 60617 S00002/S00003 drawn independently) and offset polylines for double-line and block arrows.
    /// </summary>
    public static class AnnConnectors
    {
        /// <summary>Tag of junction dots.</summary>
        public const string JunctionTag = "junction";
        /// <summary>Tag of bus mark strokes and their count text.</summary>
        public const string BusTag = "bus";
        /// <summary>Radius of a junction dot (mm).</summary>
        public const double DotRadius = 0.8;

        // ---------- Rails and junction dots ----------

        /// <summary><see cref="AnchorRole.Rail"/> anchors along a drawn path (ids <c>rail0</c>, <c>rail1</c> …).</summary>
        public static IEnumerable<Anchor> Rails(IReadOnlyList<Vec2> path)
        {
            for (var i = 0; i + 1 < path.Count; i++)
                if (path[i].DistanceTo(path[i + 1]) > Geo.Eps) yield return Anchor.Rail(path[i], path[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>A junction dot (filled circle) at <paramref name="p"/>.</summary>
        public static Primitive Dot(Vec2 p, double r = DotRadius)
            => Prim.Circle(p.X, p.Y, r, Style.ThinLine with { StrokeWidth = 0.1, Fill = Fill.Solid("#000000") }) with { Tag = JunctionTag };

        /// <summary>Junction dots at the ends of <paramref name="o"/> that are docked onto another line's rail (a T junction).</summary>
        public static IEnumerable<Primitive> BranchDots(SketchObject o, IReadOnlyList<Vec2> p)
        {
            if (o.Attach is null || p.Count == 0) yield break;
            foreach (var i in new[] { 0, p.Count - 1 }.Distinct())
                if (i < o.Attach.Count && o.Attach[i]?.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal))
                    yield return Dot(p[i]);
        }

        // ---------- Hops ----------

        public static ParamDef HopsParam(bool def) => P.Bool("hops", "Hops at crossings", "Brücken an Kreuzungen", def);

        public static ContextAction HopsAction(SketchObject o, bool def)
            => Ann.Toggle(o, "ann.hops", new I18nText("Hops at crossings on/off", "Brücken an Kreuzungen an/aus"), IconIds.Hop, "hops", def, "hops");

        /// <summary>
        /// Crossings of connector <paramref name="o"/> (drawn path <paramref name="path"/>) with the other annotation connectors
        /// on its page: proper intersections away from the segment ends of both (ends docked onto a line are branches).
        /// </summary>
        public static List<Crossing> Find(SketchObject o, IReadOnlyList<Vec2> path, RenderContext ctx, ObjectType self)
        {
            var list = new List<Crossing>();
            if (path.Count < 2) return list;
            var doc = ctx.Doc;
            var page = doc.Pages.FirstOrDefault(pg => pg.Order.Contains(o.Id));
            if (page is null) return list;
            var registry = ctx.Registry ?? self.Owner;
            var order = page.Order.ToList();
            var own = order.IndexOf(o.Id);
            foreach (var other in doc.ObjectsOn(page))
            {
                if (other.Id == o.Id || other.Points is null) continue;
                var t = other.Type == o.Type ? self : registry?.Type(other.Type);
                if (t is not IAnnConnector c || c.ConnectorPath(other, ctx) is not { Count: >= 2 } q) continue;
                var above = own > order.IndexOf(other.Id);
                for (var i = 0; i + 1 < path.Count; i++)
                    for (var j = 0; j + 1 < q.Count; j++)
                        if (Intersect(path[i], path[i + 1], q[j], q[j + 1]) is { } x && Clear(x, path[i], path[i + 1]) && Clear(x, q[j], q[j + 1]))
                            list.Add(new Crossing(other.Id, x, i, above));
            }
            return list;
        }

        /// <summary>The hops <paramref name="o"/> draws (crossings over connectors drawn before it), none when switched off.</summary>
        public static List<Crossing> Hops(SketchObject o, IReadOnlyList<Vec2> path, RenderContext ctx, ObjectType self, bool def)
            => o.Flag("hops", def) ? Find(o, path, ctx, self).Where(c => c.Above).ToList() : new List<Crossing>();

        /// <summary>The main stroke along <paramref name="path"/> with a hop at every crossing in <paramref name="hops"/>.</summary>
        public static PathPrim MainPath(IReadOnlyList<Vec2> path, IReadOnlyList<Crossing> hops, Style style)
            => (hops.Count == 0 ? Prim.Polyline(path, style) : Prim.Path(Crossings.HopPath(path, hops), style)) with { Tag = AnnTags.Main };

        private static bool Clear(Vec2 x, Vec2 a, Vec2 b) => x.DistanceTo(a) > Crossings.HopRadius * 1.5 && x.DistanceTo(b) > Crossings.HopRadius * 1.5;

        private static Vec2? Intersect(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            var r = b - a;
            var s = d - c;
            var den = r.Cross(s);
            if (Math.Abs(den) < 1e-9) return null;
            var t = (c - a).Cross(s) / den;
            var u = (c - a).Cross(r) / den;
            if (t <= 0 || t >= 1 || u <= 0 || u >= 1) return null;
            return a + r * t;
        }

        // ---------- Bus marks ----------

        public static readonly (string, string, string)[] BusOptions =
        {
            ("none", "None", "Keine"), ("slash", "Slash with number (n lines)", "Schrägstrich mit Anzahl (n Leitungen)"), ("strokes", "One stroke per line", "Ein Strich je Leitung"),
        };

        public static IEnumerable<ParamDef> BusParams()
        {
            yield return P.Choice("busMark", "Bus mark (number of lines)", "Busmarkierung (Anzahl Leitungen)", "none", BusOptions);
            yield return P.Text("busCount", "Number of lines", "Anzahl Leitungen", "3");
        }

        public static ContextAction BusAction(SketchObject o)
            => new("ann.bus.next", new I18nText("Next bus mark (none / slash / strokes)", "Nächste Busmarkierung (keine / Schrägstrich / Striche)"), IconIds.Cycle, x =>
            {
                var modes = BusOptions.Select(b => b.Item1).ToList();
                var i = modes.IndexOf(x.Str("busMark", "none"));
                return x.WithParam("busMark", modes[(i + 1 + modes.Count) % modes.Count]);
            }) { Group = "bus", ParamKeys = new[] { "busMark" } };

        /// <summary>Midpoint and unit direction of the longest segment of a path.</summary>
        public static (Vec2 Mid, Vec2 Dir) Longest(IReadOnlyList<Vec2> p)
        {
            var best = 0; var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var d = p.Count < 2 ? new Vec2(1, 0) : (p[best + 1] - p[best]).Normalized();
            if (d == Vec2.Zero) d = new Vec2(1, 0);
            return (p.Count < 2 ? Vec2.Zero : Vec2.Lerp(p[best], p[best + 1], 0.5), d);
        }

        /// <summary>The normal of <paramref name="dir"/> that points up (or left on vertical runs): label side.</summary>
        public static Vec2 UpNormal(Vec2 dir)
        {
            var n = dir.Perp;
            return n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0) ? n * -1 : n;
        }

        /// <summary>
        /// Bus mark on the middle of the longest segment: one oblique stroke with the number of lines written beside it
        /// ("slash", IEC 60617 S00002 idea) or one oblique stroke per line ("strokes", S00003 idea, at most 12).
        /// </summary>
        public static IEnumerable<Primitive> BusMark(SketchObject o, IReadOnlyList<Vec2> path, double textSize = 3.5)
        {
            var mode = o.Str("busMark", "none");
            if (mode is not ("slash" or "strokes") || path.Count < 2) yield break;
            var (mid, dir) = Longest(path);
            var n = UpNormal(dir);
            const double half = 1.8;
            var v = (dir * 0.55 + n * 0.85).Normalized() * half;
            var style = Style.ThinLine with { StrokeWidth = 0.35 };
            var count = o.Str("busCount", "3").Trim();
            if (mode == "slash")
            {
                yield return Prim.Line(mid - v, mid + v, style) with { Tag = BusTag };
                if (count.Length > 0)
                {
                    var at = mid + v + n * (textSize * 0.55) + dir * (textSize * 0.35 + Ann.HalfExtentAlong(count, textSize, dir));
                    yield return Ann.Text(at, count, textSize, BusTag);
                }
                yield break;
            }
            var k = int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? Math.Max(1, Math.Min(12, c)) : 3;
            const double gap = 1.0;
            for (var i = 0; i < k; i++)
            {
                var m = mid + dir * ((i - (k - 1) / 2.0) * gap);
                yield return Prim.Line(m - v, m + v, style) with { Tag = BusTag };
            }
        }

        // ---------- Offsets and routes ----------

        /// <summary>
        /// The polyline <paramref name="c"/> offset by <paramref name="d"/> to its left (normal (-dy, dx) of each segment) with
        /// mitred corners (mitres limited to four times the offset).
        /// </summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> c, double d)
        {
            var res = new List<Vec2>();
            if (c.Count < 2) return res;
            Vec2 N(int seg)
            {
                var dir = (c[seg + 1] - c[seg]).Normalized();
                return new Vec2(-dir.Y, dir.X);
            }
            for (var i = 0; i < c.Count; i++)
            {
                if (i == 0) { res.Add(c[0] + N(0) * d); continue; }
                if (i == c.Count - 1) { res.Add(c[i] + N(i - 1) * d); continue; }
                var n1 = N(i - 1); var n2 = N(i);
                var m = (n1 + n2);
                if (m.Length < 1e-6) { res.Add(c[i] + n2 * d); continue; }
                m = m.Normalized();
                var k = 1 / Math.Max(0.25, m.Dot(n2));
                res.Add(c[i] + m * (d * k));
            }
            return res;
        }

        /// <summary>
        /// Orthogonal route through all points: consecutive points that are not on one horizontal or vertical get a corner
        /// (horizontal first). Repeated points and straight middle points are dropped.
        /// </summary>
        public static List<Vec2> Orthogonal(IReadOnlyList<Vec2> p)
        {
            var list = new List<Vec2> { p[0] };
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var a = p[i]; var b = p[i + 1];
                if (Math.Abs(a.X - b.X) > 1e-9 && Math.Abs(a.Y - b.Y) > 1e-9) list.Add(new Vec2(b.X, a.Y));
                list.Add(b);
            }
            return ElbowArrowType.Simplify(list);
        }

        /// <summary>Cuts <paramref name="len"/> off the start (<paramref name="atStart"/>) or the end of a polyline, stopping at its last segment.</summary>
        public static List<Vec2> Trim(IReadOnlyList<Vec2> p, double len, bool atStart)
        {
            var list = p.ToList();
            if (len <= 0 || list.Count < 2) return list;
            if (atStart) list.Reverse();
            var n = list.Count;
            var a = list[n - 2]; var b = list[n - 1];
            var seg = a.DistanceTo(b);
            var cut = Math.Min(len, seg * 0.95);
            list[n - 1] = seg < Geo.Eps ? b : b + (a - b) * (cut / seg);
            if (atStart) list.Reverse();
            return list;
        }
    }
}
