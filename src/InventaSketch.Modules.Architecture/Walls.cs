using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>Centre-line geometry of one wall in page millimetres (duplicate points removed).</summary>
    internal sealed class WallLine
    {
        public WallLine(string id, IReadOnlyList<Vec2> pts, double half, bool closed)
        {
            Id = id; Pts = pts; Half = half; Closed = closed;
        }

        public string Id { get; }
        public IReadOnlyList<Vec2> Pts { get; }
        public double Half { get; }
        public bool Closed { get; }
        public int SegCount => Closed ? Pts.Count : Pts.Count - 1;
        public Vec2 A(int k) => Pts[k];
        public Vec2 B(int k) => Pts[(k + 1) % Pts.Count];

        /// <summary>Builds the wall line of a wall object (null if it has fewer than two distinct points).</summary>
        public static WallLine? Of(SketchObject o, SketchDocument doc) => Of(o, doc.ResolvePoints(o), doc);

        public static WallLine? Of(SketchObject o, IReadOnlyList<Vec2> raw, SketchDocument? doc)
        {
            var pts = new List<Vec2>(raw.Count);
            foreach (var p in raw) if (pts.Count == 0 || pts[pts.Count - 1].DistanceTo(p) > 1e-6) pts.Add(p);
            var closed = o.Flag("closed", false) && pts.Count >= 3;
            // A polyline whose last point returns to the first one is a closed loop as well.
            if (!closed && pts.Count >= 4 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-6) { pts.RemoveAt(pts.Count - 1); closed = true; }
            if (closed && pts.Count >= 2 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-6) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 2) return null;
            if (closed && pts.Count < 3) closed = false;
            return new WallLine(o.Id, pts, WallType.ThicknessOnPage(o, doc) / 2, closed);
        }

        /// <summary>The other walls on the page of <paramref name="o"/> (empty if the object is not placed on a page).</summary>
        public static List<WallLine> Neighbours(SketchObject o, SketchDocument doc)
        {
            var list = new List<WallLine>();
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(o.Id));
            if (page is null) return list;
            foreach (var other in doc.ObjectsOn(page))
            {
                if (other.Id == o.Id || other.Type != WallType.TypeId || other.Points is null) continue;
                var w = Of(other, doc);
                if (w is not null) list.Add(w);
            }
            return list;
        }

        /// <summary>All walls on the page of <paramref name="o"/> (used by openings to find their host wall).</summary>
        public static IEnumerable<WallLine> OnPageOf(SketchObject o, SketchDocument doc)
        {
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(o.Id));
            if (page is null) yield break;
            foreach (var other in doc.ObjectsOn(page))
            {
                if (other.Type != WallType.TypeId || other.Points is null) continue;
                var w = Of(other, doc);
                if (w is not null) yield return w;
            }
        }
    }

    /// <summary>
    /// Offset outline of a wall with mitred corners. Ends are capped when free, mitred when exactly one other wall
    /// ends at the same point (L joint), trimmed to the face of a wall they butt against (T joint) and left open in
    /// all other cases. Face lines are interrupted where another wall butts against them.
    /// </summary>
    internal static class WallBuilder
    {
        /// <summary>Distance (page mm) within which wall points are considered joined.</summary>
        public const double JoinTolerance = 0.25;

        private enum EndKind { Cap, Open, Mitre, Trim }

        private sealed class EndCond
        {
            public EndKind Kind;
            public Vec2 OtherDir;
            public double OtherHalf;
            public Vec2 FaceA, FaceB;
            public double FaceHalf;
        }

        public sealed class Result
        {
            public List<Vec2> Left = new();
            public List<Vec2> Right = new();
            public bool CapStart, CapEnd;
            /// <summary>Break intervals per (segment, side) as fractions along the face line of that segment.</summary>
            public Dictionary<(int Seg, int Side), List<(double A, double B)>> Breaks = new();
        }

        private static Vec2 Dir(Vec2 a, Vec2 b) => (b - a).Normalized();

        private static bool Near(Vec2 a, Vec2 b) => a.DistanceTo(b) <= JoinTolerance;

        private static EndCond Classify(WallLine self, bool start, List<WallLine> others)
        {
            var n = self.Pts.Count;
            var p = start ? self.Pts[0] : self.Pts[n - 1];
            var inward = start ? Dir(self.Pts[0], self.Pts[1]) : Dir(self.Pts[n - 1], self.Pts[n - 2]);
            var matches = new List<(Vec2 Dir, double Half)>();
            var hits = 0;
            EndCond? through = null;

            // The other end of the same wall (an open loop whose ends meet).
            var opposite = start ? self.Pts[n - 1] : self.Pts[0];
            if (n >= 3 && Near(opposite, p))
                matches.Add((start ? Dir(self.Pts[n - 1], self.Pts[n - 2]) : Dir(self.Pts[0], self.Pts[1]), self.Half));

            foreach (var w in others)
            {
                var m = w.Pts.Count;
                if (!w.Closed)
                {
                    if (Near(w.Pts[0], p)) matches.Add((Dir(w.Pts[0], w.Pts[1]), w.Half));
                    if (Near(w.Pts[m - 1], p)) matches.Add((Dir(w.Pts[m - 1], w.Pts[m - 2]), w.Half));
                }
                for (var i = w.Closed ? 0 : 1; i < (w.Closed ? m : m - 1); i++)
                    if (Near(w.Pts[i], p)) hits++;
                for (var k = 0; k < w.SegCount; k++)
                {
                    Vec2 a = w.A(k), b = w.B(k);
                    if (Near(a, p) || Near(b, p) || Arch.DistanceToSegment(p, a, b) > JoinTolerance) continue;
                    if (Math.Abs(Dir(a, b).Cross(inward)) < 0.05) continue; // running along the other wall: overlap, not a joint
                    through = new EndCond { Kind = EndKind.Trim, FaceA = a, FaceB = b, FaceHalf = w.Half };
                }
            }

            if (matches.Count == 1 && hits == 0 && through is null)
            {
                var od = matches[0].Dir;
                // Walls folding back onto each other cannot be mitred.
                if (od.Dot(inward) > 0.999) return new EndCond { Kind = EndKind.Open };
                return new EndCond { Kind = EndKind.Mitre, OtherDir = od, OtherHalf = matches[0].Half };
            }
            if (through is not null && matches.Count == 0 && hits == 0) return through;
            return matches.Count + hits + (through is null ? 0 : 1) > 0 ? new EndCond { Kind = EndKind.Open } : new EndCond { Kind = EndKind.Cap };
        }

        /// <summary>Offset points at a vertex between an incoming direction d1 (half h1) and an outgoing direction d2 (half h2).</summary>
        private static (Vec2 L, Vec2 R) Corner(Vec2 p, Vec2 d1, double h1, Vec2 d2, double h2)
        {
            var n1 = d1.Perp; var n2 = d2.Perp;
            var l = Arch.Intersect(p + n1 * h1, d1, p + n2 * h2, d2, p + n1 * h1);
            var r = Arch.Intersect(p - n1 * h1, d1, p - n2 * h2, d2, p - n1 * h1);
            var limit = 5 * Math.Max(h1, h2);
            if (l.DistanceTo(p) > limit || r.DistanceTo(p) > limit) { l = p + n1 * h1; r = p - n1 * h1; }
            return (l, r);
        }

        /// <summary>Offset points of a free/joined end. <paramref name="d"/> is the wall direction at that end (start to end of the wall).</summary>
        private static (Vec2 L, Vec2 R) End(Vec2 p, Vec2 d, double h, EndCond c, bool start)
        {
            var n = d.Perp;
            switch (c.Kind)
            {
                case EndKind.Mitre:
                    // Continue the chain through the other wall: start = other -> p -> wall, end = wall -> p -> other.
                    return start ? Corner(p, -c.OtherDir, c.OtherHalf, d, h) : Corner(p, d, h, c.OtherDir, c.OtherHalf);
                case EndKind.Trim:
                    {
                        var fd = Dir(c.FaceA, c.FaceB);
                        var m = fd.Perp;
                        var inward = start ? d : -d;
                        if (m.Dot(inward) < 0) m = -m;
                        var f = c.FaceA + m * c.FaceHalf;
                        var l = Arch.Intersect(p + n * h, d, f, fd, p + n * h);
                        var r = Arch.Intersect(p - n * h, d, f, fd, p - n * h);
                        if (l.DistanceTo(p) > 5 * Math.Max(h, c.FaceHalf) || r.DistanceTo(p) > 5 * Math.Max(h, c.FaceHalf)) return (p + n * h, p - n * h);
                        return (l, r);
                    }
                default:
                    return (p + n * h, p - n * h);
            }
        }

        public static Result Build(WallLine w, List<WallLine> others)
        {
            var res = new Result();
            var n = w.Pts.Count;
            var h = w.Half;
            for (var i = 0; i < n; i++)
            {
                var p = w.Pts[i];
                (Vec2 L, Vec2 R) lr;
                if (w.Closed || (i > 0 && i < n - 1))
                {
                    var prev = w.Pts[(i - 1 + n) % n]; var next = w.Pts[(i + 1) % n];
                    lr = Corner(p, Dir(prev, p), h, Dir(p, next), h);
                }
                else if (i == 0)
                {
                    var c = Classify(w, true, others);
                    res.CapStart = c.Kind == EndKind.Cap;
                    lr = End(p, Dir(p, w.Pts[1]), h, c, true);
                }
                else
                {
                    var c = Classify(w, false, others);
                    res.CapEnd = c.Kind == EndKind.Cap;
                    lr = End(p, Dir(w.Pts[n - 2], p), h, c, false);
                }
                res.Left.Add(lr.L);
                res.Right.Add(lr.R);
            }

            // Face breaks where the end of another wall butts against this wall (T joint).
            for (var k = 0; k < w.SegCount; k++)
            {
                Vec2 a = w.A(k), b = w.B(k);
                var d = Dir(a, b); var nn = d.Perp;
                foreach (var o in others)
                {
                    if (o.Closed) continue;
                    var m = o.Pts.Count;
                    foreach (var (e, v) in new[] { (o.Pts[0], Dir(o.Pts[0], o.Pts[1])), (o.Pts[m - 1], Dir(o.Pts[m - 1], o.Pts[m - 2])) })
                    {
                        if (Near(e, a) || Near(e, b) || Arch.DistanceToSegment(e, a, b) > JoinTolerance) continue;
                        var dot = nn.Dot(v);
                        if (Math.Abs(dot) < 0.05) continue;
                        var side = dot > 0 ? 1 : -1;
                        var f = a + nn * (h * side);
                        var x1 = Arch.Intersect(e + v.Perp * o.Half, v, f, d, e);
                        var x2 = Arch.Intersect(e - v.Perp * o.Half, v, f, d, e);
                        var la = side > 0 ? res.Left[k] : res.Right[k];
                        var lb = side > 0 ? res.Left[(k + 1) % n] : res.Right[(k + 1) % n];
                        double sa = (la - a).Dot(d), sb = (lb - a).Dot(d);
                        if (Math.Abs(sb - sa) < Geo.Eps) continue;
                        double f1 = ((x1 - a).Dot(d) - sa) / (sb - sa), f2 = ((x2 - a).Dot(d) - sa) / (sb - sa);
                        var key = (k, side);
                        if (!res.Breaks.TryGetValue(key, out var list)) res.Breaks[key] = list = new List<(double, double)>();
                        list.Add((Math.Min(f1, f2), Math.Max(f1, f2)));
                    }
                }
            }
            return res;
        }

        /// <summary>Face polylines of one side, interrupted by the break intervals.</summary>
        public static List<List<Vec2>> FaceRuns(WallLine w, Result r, int side)
        {
            var pts = side > 0 ? r.Left : r.Right;
            var n = pts.Count;
            var runs = new List<List<Vec2>>();
            List<Vec2>? cur = null;
            for (var k = 0; k < w.SegCount; k++)
            {
                Vec2 a = pts[k], b = pts[(k + 1) % n];
                var pieces = new List<(double, double)> { (0, 1) };
                if (r.Breaks.TryGetValue((k, side), out var breaks))
                    foreach (var (b0, b1) in breaks.OrderBy(x => x.A))
                    {
                        var next = new List<(double, double)>();
                        foreach (var (p0, p1) in pieces)
                        {
                            if (b1 <= p0 || b0 >= p1) { next.Add((p0, p1)); continue; }
                            if (b0 > p0) next.Add((p0, b0));
                            if (b1 < p1) next.Add((b1, p1));
                        }
                        pieces = next;
                    }
                foreach (var (p0, p1) in pieces)
                {
                    if (p1 - p0 < 1e-6) continue;
                    if (cur is null || p0 > 1e-9)
                    {
                        if (cur is { Count: >= 2 }) runs.Add(cur);
                        cur = new List<Vec2> { Vec2.Lerp(a, b, p0) };
                    }
                    cur.Add(Vec2.Lerp(a, b, p1));
                    if (p1 < 1 - 1e-9) { runs.Add(cur); cur = null; }
                }
                if (pieces.Count == 0 || pieces[pieces.Count - 1].Item2 < 1 - 1e-9)
                {
                    if (cur is { Count: >= 2 }) runs.Add(cur);
                    cur = null;
                }
            }
            if (cur is { Count: >= 2 }) runs.Add(cur);
            return runs;
        }
    }

    /// <summary>
    /// Wall as a point object: the points are the wall centre line. Rendered as two face lines with mitred corners
    /// and the kind-specific hatch or fill between them. Thickness is given in real millimetres and converted with
    /// the plan scale (default 1:100).
    /// </summary>
    internal sealed class WallType : PointType
    {
        public const string TypeId = "arch.wall";

        public static readonly double[] Thicknesses = { 75, 100, 115, 150, 175, 200, 240, 300, 365, 425, 490 };

        public static readonly (string Value, I18nText Label)[] Kinds =
        {
            ("exterior", new I18nText("Exterior wall", "Außenwand")),
            ("interior", new I18nText("Interior wall (load-bearing)", "Innenwand (tragend)")),
            ("partition", new I18nText("Partition wall", "Trennwand (leicht)")),
        };

        public WallType() : base(TypeId, new I18nText("Wall", "Wand"), ArchCategories.Structure,
            "wall", "wand", "mauer", "masonry", "mauerwerk", "exterior", "außenwand", "interior", "innenwand", "partition", "trennwand", "floor plan", "grundriss") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, 0), new Vec2(30, 0) };

        /// <summary>A wall is a centre line with corners: corners can be inserted, ends extended, walls branched (T joints).</summary>
        public override bool InsertablePoints => true;
        public override bool IsClosed(SketchObject o) => o.Flag("closed", false) && o.Points is { Count: > 2 };

        public override IReadOnlyList<ViewDef> Views => new[] { BoxType.PlanView, BoxType.ElevationView };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Num("thickness", "Thickness", "Dicke", 240, 10, 2000, "mm", 5),
            Pm.Choice("kind", "Kind", "Art", "exterior", ("exterior", "Exterior wall", "Außenwand"), ("interior", "Interior wall (load-bearing)", "Innenwand (tragend)"), ("partition", "Partition wall", "Trennwand (leicht)"))
                .WithOptionIcons(v => v switch { "exterior" => IconIds.WallThick, "interior" => IconIds.WallMedium, "partition" => IconIds.WallThin, _ => null }),
            Pm.Bool("closed", "Closed loop", "Geschlossen", false),
            Pm.Num("height", "Height (elevation)", "Höhe (Ansicht)", 2750, 100, 20000, "mm", 25, advanced: true),
            Pm.Scale,
        };

        /// <summary>Wall thickness in page millimetres (real thickness divided by the plan scale).</summary>
        public static double ThicknessOnPage(SketchObject o, SketchDocument? doc) => Math.Max(0.05, o.Num("thickness", 240)) / Arch.ScaleOf(o, doc);

        /// <summary>Hatch or fill of the wall core per kind.</summary>
        public static Style CoreStyle(string kind) => kind switch
        {
            "partition" => new Style { Stroke = "none", Fill = Fill.Solid("#d9d9d9") },
            "interior" => new Style { Stroke = "none", Fill = Fill.Hatched(new HatchPattern(45, 1.0, Color: "#000000", Width: 0.13)) },
            _ => new Style { Stroke = "none", Fill = Fill.Hatched(new HatchPattern(45, 0.7, Color: "#000000", Width: 0.13), "#e8e8e8") },
        };

        public static Style FaceStyle(string kind) => kind == "partition" ? St.WallLight : St.Wall;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var w = WallLine.Of(o, p, ctx.Doc);
            if (w is null) return Array.Empty<Primitive>();
            return ViewOf(o) == BoxType.ElevationView.Id ? Elevation(o, w, ctx.Doc) : Plan(o, w, WallLine.Neighbours(o, ctx.Doc));
        }

        private static IReadOnlyList<Primitive> Plan(SketchObject o, WallLine w, List<WallLine> others)
        {
            var kind = o.Str("kind", "exterior");
            var r = WallBuilder.Build(w, others);
            var list = new List<Primitive>();
            var core = CoreStyle(kind);
            var n = w.Pts.Count;
            // Core: one polygon for an open wall, two half rings for a closed loop (no holes needed in any host).
            if (!w.Closed)
                list.Add(Prim.Polygon(r.Left.Concat(Enumerable.Reverse(r.Right)), core) with { Tag = ArchTags.WallFill });
            else
            {
                var k = n / 2;
                IEnumerable<int> Range(int from, int to) { for (var i = from; ; i = (i + 1) % n) { yield return i; if (i == to) yield break; } }
                list.Add(Prim.Polygon(Range(0, k).Select(i => r.Left[i]).Concat(Range(0, k).Reverse().Select(i => r.Right[i])), core) with { Tag = ArchTags.WallFill });
                list.Add(Prim.Polygon(Range(k, 0).Select(i => r.Left[i]).Concat(Range(k, 0).Reverse().Select(i => r.Right[i])), core) with { Tag = ArchTags.WallFill });
            }
            var face = FaceStyle(kind);
            foreach (var side in new[] { 1, -1 })
                foreach (var run in WallBuilder.FaceRuns(w, r, side))
                    list.Add(Prim.Polyline(run, face) with { Tag = ArchTags.WallFace });
            if (!w.Closed && r.CapStart) list.Add(Prim.Line(r.Left[0], r.Right[0], face) with { Tag = ArchTags.WallCap });
            if (!w.Closed && r.CapEnd) list.Add(Prim.Line(r.Left[n - 1], r.Right[n - 1], face) with { Tag = ArchTags.WallCap });
            return list;
        }

        /// <summary>Simple front elevation (viewed from below on the page): outline over the plan extent and corner edges.</summary>
        private static IReadOnlyList<Primitive> Elevation(SketchObject o, WallLine w, SketchDocument doc)
        {
            var h = Math.Max(0.1, o.Num("height", 2750) / Arch.ScaleOf(o, doc));
            var x0 = w.Pts.Min(q => q.X); var x1 = w.Pts.Max(q => q.X);
            var y = w.Pts.Max(q => q.Y);
            if (x1 - x0 < 1e-6) x1 = x0 + 2 * w.Half;
            var list = new List<Primitive>
            {
                Prim.Rect(x0, y - h, x1 - x0, h, St.Thin) with { Tag = ArchTags.Outline },
                Prim.Line(new Vec2(x0 - 1, y), new Vec2(x1 + 1, y), St.Wall) with { Tag = "ground" },
            };
            foreach (var x in w.Pts.Select(q => q.X).Where(x => x > x0 + 1e-6 && x < x1 - 1e-6).Distinct())
                list.Add(Prim.Line(new Vec2(x, y), new Vec2(x, y - h), St.Fine) with { Tag = "edge" });
            return list;
        }

        /// <summary>The ground line of the elevation: the lowest point of the centre line (points do not move between views).</summary>
        public override double? ElevationGround(SketchObject o, RenderContext ctx)
            => WallLine.Of(o, ctx.Doc) is { } w && w.Pts.Count > 0 ? w.Pts.Max(q => q.Y) : null;

        /// <summary>Walls render their joints and face breaks from the neighbouring walls.</summary>
        public override bool DependsOnNeighbours => true;

        /// <summary>
        /// Walls whose end touches this wall or which this wall's end touches (L and T joints, face breaks), and doors and
        /// windows whose centre sits on this wall (their band thickness comes from it).
        /// </summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var doc = ctx.Doc;
            var w = WallLine.Of(o, doc);
            if (w is null) yield break;
            foreach (var other in ObjectsNear(doc, o, w.Half + WallBuilder.JoinTolerance))
            {
                if (other.Type == TypeId && other.Points is not null)
                {
                    var ow = WallLine.Of(other, doc);
                    if (ow is not null && (EndTouches(w, ow) || EndTouches(ow, w))) yield return other.Id;
                }
                else if (!other.IsPointGeometry && Openings.IsOpening(other.Type))
                {
                    var c = new Vec2(other.Transform.X, other.Transform.Y);
                    for (var k = 0; k < w.SegCount; k++)
                        if (Arch.DistanceToSegment(c, w.A(k), w.B(k)) <= Math.Max(w.Half, WallBuilder.JoinTolerance)) { yield return other.Id; break; }
                }
            }
        }

        /// <summary>True if a free end of <paramref name="a"/> lies on a segment of <paramref name="b"/> (joint or butt).</summary>
        private static bool EndTouches(WallLine a, WallLine b)
        {
            if (a.Closed) return false;
            foreach (var e in new[] { a.Pts[0], a.Pts[a.Pts.Count - 1] })
                for (var k = 0; k < b.SegCount; k++)
                    if (Arch.DistanceToSegment(e, b.A(k), b.B(k)) <= WallBuilder.JoinTolerance) return true;
            return false;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var w = WallLine.Of(o, ctx.Doc);
            if (w is null) yield break;
            for (var i = 0; i < w.Pts.Count; i++) yield return Anchor.Point(AnchorRole.Mount, w.Pts[i], "v" + i.ToString(CultureInfo.InvariantCulture));
            for (var k = 0; k < w.SegCount; k++) yield return Anchor.Segment(AnchorRole.Axis, w.A(k), w.B(k)) with { Id = "axis" + k.ToString(CultureInfo.InvariantCulture) };
            // The centre line is a rail: a branch wall docks onto it (T joint) and follows the wall.
            for (var k = 0; k < w.SegCount; k++) yield return Anchor.Rail(w.A(k), w.B(k), "rail" + k.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Next standard thickness up (+1) or down (-1) from the current one.</summary>
        public static double StepThickness(double current, int dir)
        {
            if (dir > 0) { foreach (var t in Thicknesses) if (t > current + 1e-6) return t; return current + 50; }
            for (var i = Thicknesses.Length - 1; i >= 0; i--) if (Thicknesses[i] < current - 1e-6) return Thicknesses[i];
            return Math.Max(10, current - 10);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var t = o.Num("thickness", 240);
            var up = StepThickness(t, 1);
            var down = StepThickness(t, -1);
            string Mm(double v) => v.ToString("0", CultureInfo.InvariantCulture) + " mm";
            yield return new ContextAction("arch.thickness.inc", new I18nText("Thicker wall (" + Mm(up) + ")", "Dickere Wand (" + Mm(up) + ")"), IconIds.Increase, x => x.WithParam("thickness", up)) { Group = "thickness" };
            yield return new ContextAction("arch.thickness.dec", new I18nText("Thinner wall (" + Mm(down) + ")", "Dünnere Wand (" + Mm(down) + ")"), IconIds.Decrease, x => x.WithParam("thickness", down)) { Group = "thickness" };
            yield return ContextAction.WithInput("arch.thickness.set", new I18nText("Thickness…", "Dicke…"), IconIds.QtyThickness,
                new I18nText("Wall thickness (mm)", "Wanddicke (mm)"), t.ToString("0.#", CultureInfo.InvariantCulture),
                (x, text) => x.WithParam("thickness", Math.Min(2000, Math.Max(10, Arch.ParseMm(text, lang)))), "mm") with { Group = "thickness" };
            foreach (var a in Arch.Choice(o, "arch.kind", new I18nText("Wall kind", "Wandart"), "kind", "exterior", Kinds)) yield return a;
            yield return Arch.Toggle(o, "arch.closed", new I18nText("Closed loop", "Geschlossen"), IconIds.Toggle, "closed", false);
        }
    }

    /// <summary>
    /// Room: polygon point object with name and area (computed from the points and the plan scale). Element values: floor
    /// area <c>A</c> (m², from the outline at the plan scale, a marking relation of the room itself), room height <c>h</c>
    /// (m, optional parameter, default 2.5 m), volume <c>V = A·h</c> (m³) and an optional heating load estimate
    /// <c>Φ_HL = q·A</c> (W, from a specific heating load q in W/m²). Building services elements placed inside the room take
    /// these values by containment (the air terminal A, h and V, the radiator Φ_HL; BsValues), keys and quantities match.
    /// </summary>
    internal sealed class RoomType : PointType, IValueType, IValueLinks
    {
        public static readonly (string Value, string En, string De)[] Functions =
        {
            ("room", "Room", "Raum"), ("living", "Living room", "Wohnen"), ("bedroom", "Bedroom", "Schlafen"), ("kitchen", "Kitchen", "Küche"),
            ("dining", "Dining", "Essen"), ("bath", "Bathroom", "Bad"), ("wc", "WC", "WC"), ("hall", "Hall", "Flur"), ("office", "Office", "Arbeiten"),
            ("child", "Child's room", "Kind"), ("storage", "Storage", "Abstellraum"), ("utility", "Utility room", "Hauswirtschaft"), ("garage", "Garage", "Garage"),
        };

        public RoomType() : base("arch.room", new I18nText("Room (name and area)", "Raum (Name und Fläche)"), ArchCategories.Structure,
            "room", "raum", "area", "fläche", "wohnfläche", "label", "zimmer", "stamp", "raumstempel") { }

        public override bool HasText => true;
        protected override int MinPoints => 3;
        public override bool InsertablePoints => true;
        public override bool IsClosed(SketchObject o) => true;

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, -15), new Vec2(20, -15), new Vec2(20, 15), new Vec2(-20, 15) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Choice("function", "Use", "Nutzung", "room", Functions),
            Pm.Bool("showArea", "Show area", "Fläche anzeigen", true),
            Pm.Bool("fill", "Tinted floor", "Boden getönt", false),
            Pm.Num("textSize", "Text height", "Schrifthöhe", 3.5, 1, 50, "mm", 0.5),
            Pm.Scale,
            // No default on purpose: unset, the value default h = 2.5 m applies (and gives way to an entered volume).
            new ParamDef(HeightParam, new I18nText("Room height (default 2.5 m)", "Raumhöhe (Vorgabe 2,5 m)"), ParamKind.Number) { Min = 0.5, Max = 100, Unit = "m", Step = 0.05 },
            new ParamDef(VolumeParam, new I18nText("Room volume", "Raumvolumen"), ParamKind.Text) { Unit = "m³", Advanced = true },
            new ParamDef(HeatLoadDensityParam, new I18nText("Specific heating load (W/m²)", "Spezifische Heizlast (W/m²)"), ParamKind.Text) { Unit = "W/m²", Advanced = true },
            new ParamDef(HeatLoadParam, new I18nText("Heating load", "Heizlast"), ParamKind.Text) { Unit = "W", Advanced = true },
            ValueLabels.ShowValues,
        };

        // ------------------------------------------------------------------ element values

        public const string HeightParam = "height";
        public const string VolumeParam = "volume";
        /// <summary>Default room height in metres.</summary>
        public const double DefaultHeight = 2.5;

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly VarDef[] Vars =
        {
            new("A", T("Floor area", "Grundfläche"), T("A", "A"), Quantities.Area) { Unit = "m²", Param = "area", Min = 1e-4, Max = 1e7 },
            new("h", T("Room height", "Raumhöhe"), T("h", "h"), Quantities.Length) { Unit = "m", Param = HeightParam, Default = DefaultHeight, Min = 0.1, Max = 1000 },
            new("V", T("Room volume", "Raumvolumen"), T("V", "V"), Quantities.Volume) { Unit = "m³", Param = VolumeParam, Min = 1e-4, Max = 1e10 },
            new("PhiHL", T("Heating load (estimate)", "Heizlast (Schätzwert)"), T("Φ_HL", "Φ_HL"), Quantities.HeatFlow) { Unit = "W", Param = HeatLoadParam, Min = 0, Max = 1e9 },
            new("q", T("Specific heating load per floor area", "Spezifische Heizlast je Grundfläche"), T("q", "q"), Quantities.Irradiance)
                { Unit = "W/m²", Param = HeatLoadDensityParam, Relevance = VarRelevance.Panel, Min = 0, Max = 1e4 },
        };

        public const string HeatLoadParam = "heatLoad";
        public const string HeatLoadDensityParam = "heatLoadDensity";

        /// <summary>Room volume V = A·h [S1].</summary>
        private static readonly Relation VolumeRelation =
            new Relation("arch.room.volume", T("Room volume", "Raumvolumen"), "{V} = {A}·{h}", "V", "A", "h") { Source = "S1" }
                .Solve("V", "{A}·{h}", v => v("A") * v("h"))
                .Solve("A", "{V}/{h}", v => v("V") / v("h"))
                .Solve("h", "{V}/{A}", v => v("V") / v("A"));

        /// <summary>Heating load estimate Φ_HL = q·A from a specific heating load per floor area (handbook values [S30]).</summary>
        private static readonly Relation HeatLoadRelation =
            new Relation("arch.room.heatload", T("Heating load from the floor area", "Heizlast aus der Grundfläche"), "{PhiHL} = {q}·{A}", "PhiHL", "q", "A")
            {
                Source = "S30",
                Validity = T("rough estimate for sizing; not a heating load calculation to EN 12831",
                             "grobe Schätzung zur Auslegung; keine Heizlastberechnung nach EN 12831"),
            }
                .Solve("PhiHL", "{q}·{A}", v => v("q") * v("A"))
                .Solve("q", "{PhiHL}/{A}", v => v("PhiHL") / v("A"));

        private static readonly Relation[] Rels = { VolumeRelation, HeatLoadRelation };

        private static readonly I18nText OutlineName = T("Floor area from the outline at the plan scale", "Grundfläche aus dem Umriss im Planmaßstab");

        public IReadOnlyList<VarDef> Variables => Vars;
        public IReadOnlyList<Relation> Relations => Rels;

        /// <summary>The floor area from the outline (page mm² times the plan scale squared): a relation of the room alone.</summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 3) yield break;
            var scale = Arch.ScaleOf(o, ctx.Doc);
            var area = AreaM2(p, scale);
            if (!(area > 0) || double.IsInfinity(area)) yield break;
            var expr = "A_outline·" + scale.ToString("0.###", CultureInfo.InvariantCulture) + "²";
            var rel = new Relation("arch.room.area", OutlineName, "{A} = " + expr, "A").Solve("A", expr, _ => area);
            yield return BoundRelation.Local(rel, o.Id);
        }

        /// <summary>Room area in square metres for page points at the given plan scale denominator.</summary>
        public static double AreaM2(IReadOnlyList<Vec2> pts, double scale) => Math.Abs(Geo.PolygonArea(pts)) * scale * scale / 1e6;

        /// <summary>Area centroid of a simple polygon (vertex mean for degenerate polygons).</summary>
        public static Vec2 Centroid(IReadOnlyList<Vec2> p)
        {
            double a = 0, cx = 0, cy = 0;
            for (var i = 0; i < p.Count; i++)
            {
                var q = p[i]; var r = p[(i + 1) % p.Count];
                var c = q.X * r.Y - r.X * q.Y;
                a += c; cx += (q.X + r.X) * c; cy += (q.Y + r.Y) * c;
            }
            if (Math.Abs(a) < 1e-9) return new Vec2(p.Average(v => v.X), p.Average(v => v.Y));
            return new Vec2(cx / (3 * a), cy / (3 * a));
        }

        public static string NameOf(SketchObject o, Lang lang)
        {
            if (!string.IsNullOrEmpty(o.Text)) return o.Text!;
            var f = o.Str("function", "room");
            var e = Functions.FirstOrDefault(x => x.Value == f);
            return e.Value is null ? (lang == Lang.De ? "Raum" : "Room") : (lang == Lang.De ? e.De : e.En);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var outline = Style.Helper with { Stroke = "#7f7f7f" };
            if (o.Flag("fill", false)) outline = outline with { Fill = Fill.Solid("#f3efe4") };
            var list = new List<Primitive> { Prim.Polygon(p, outline) with { Tag = ArchTags.Outline } };
            var c = Centroid(p);
            var b = Rect.FromPoints(p);
            var name = NameOf(o, ctx.Lang);
            var size = Arch.FitText(name, o.Num("textSize", 3.5), b.W * 0.9, b.H * 0.35);
            var showArea = o.Flag("showArea", true);
            var area = Arch.Format(AreaM2(p, Arch.ScaleOf(o, ctx.Doc)), 2, ctx.Lang) + " m²";
            var areaSize = Arch.FitText(area, size * 0.8, b.W * 0.9, b.H * 0.3);
            var dy = showArea ? (size + areaSize) * 0.6 / 2 : 0;
            list.Add(Arch.Text(new Vec2(c.X, c.Y - dy), name, size, ArchTags.RoomName, bold: true));
            if (showArea) list.Add(Arch.Text(new Vec2(c.X, c.Y + dy), area, areaSize, ArchTags.RoomArea));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count >= 3) yield return Anchor.Point(AnchorRole.Center, Centroid(p), "c");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return ContextAction.WithInput("arch.room.name", new I18nText("Name…", "Name…"), IconIds.Name,
                new I18nText("Room name", "Raumname"), NameOf(o, lang), (x, text) => x with { Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim() });
            yield return Arch.Toggle(o, "arch.room.area", new I18nText("Show area", "Fläche anzeigen"), IconIds.QtyArea, "showArea", true);
            yield return Arch.Toggle(o, "arch.room.fill", new I18nText("Tinted floor", "Boden getönt"), IconIds.Toggle, "fill", false);
        }
    }

    /// <summary>Beam (above the cut plane): dashed outline between two points, with an axis for snapping.</summary>
    internal sealed class BeamType : PointType
    {
        public BeamType() : base("arch.beam", new I18nText("Beam (above)", "Unterzug / Träger"), ArchCategories.Structure,
            "beam", "träger", "unterzug", "balken", "lintel", "sturz", "girder") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-25, 0), new Vec2(25, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[] { Pm.Num("width", "Width", "Breite", 240, 10, 2000, "mm", 5), Pm.Scale };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var d = (b - a).Normalized();
            if (d == Vec2.Zero) return Array.Empty<Primitive>();
            var n = d.Perp * (Math.Max(0.05, o.Num("width", 240)) / Arch.ScaleOf(o, ctx.Doc) / 2);
            return new Primitive[]
            {
                Prim.Polygon(new[] { a + n, b + n, b - n, a - n }, St.Hidden) with { Tag = ArchTags.Outline },
                Prim.Line(a, b, St.Center) with { Tag = "axis" },
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count >= 2) yield return Anchor.Segment(AnchorRole.Axis, p[0], p[p.Count - 1]) with { Id = "axis" };
        }
    }
}
