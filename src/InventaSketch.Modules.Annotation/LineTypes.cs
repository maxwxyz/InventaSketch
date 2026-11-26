using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Straight line (visible line weight by default).</summary>
    public sealed class LineType : AnnPointType
    {
        public LineType() : base("ann.line", new I18nText("Line", "Linie"), AnnCategories.Lines, "line", "linie", "strich") { }

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.None).Concat(AnnConnectors.BusParams()).ToList();

        /// <summary>Drawn on with corners, a line becomes a polyline.</summary>
        public override string? CornerType => "ann.polyline";

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => new Primitive[] { new LinePrim(p[0], p[p.Count - 1], Style.Visible with { ArrowStart = Ann.Head(o, "headStart", ArrowHead.None), ArrowEnd = Ann.Head(o, "headEnd", ArrowHead.None), ArrowSize = Ann.HeadSize(o) }) { Tag = AnnTags.Main } }
                .Concat(AnnConnectors.BusMark(o, new[] { p[0], p[p.Count - 1] })).ToList();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Ann.ArrowActions(o, ArrowHead.None, ArrowHead.None).Concat(AnnConnectors.BusActions(o));
    }

    /// <summary>
    /// Open or closed polyline through all points. An open polyline with an arrowhead or a docked end is a connector: it
    /// hops over earlier connectors it crosses (parameter <c>hops</c>), its segments are rails other connector ends dock
    /// on (with a junction dot), and it may carry a bus mark. Plain polylines (drawings, imported vector graphics) stay plain.
    /// </summary>
    public sealed class PolylineType : AnnPointType, IAnnConnector
    {
        public PolylineType() : base("ann.polyline", new I18nText("Polyline", "Polylinie"), AnnCategories.Lines, "polyline", "linienzug") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 8), new Vec2(-5, -8), new Vec2(5, 8), new Vec2(15, -8) };
        public override bool InsertablePoints => true;
        public override bool IsClosed(SketchObject o) => o.Flag("closed", false) && o.Points is { Count: > 2 };
        public override bool DependsOnNeighbours => true;

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.None)
            .Append(P.Bool("closed", "Closed", "Geschlossen", false)).Append(AnnConnectors.HopsParam(true)).Concat(AnnConnectors.BusParams()).ToList();

        /// <summary>True for an open polyline with an arrowhead or a docked end (a connector line).</summary>
        public static bool IsConnector(SketchObject o)
            => !(o.Flag("closed", false) && o.Points is { Count: > 2 })
               && (Ann.Head(o, "headStart", ArrowHead.None) != ArrowHead.None || Ann.Head(o, "headEnd", ArrowHead.None) != ArrowHead.None
                   || (o.Attach is { } a && a.Count > 0 && (a[0] is not null || a[a.Count - 1] is not null)));

        public IReadOnlyList<Vec2>? ConnectorPath(SketchObject o, RenderContext ctx)
            => IsConnector(o) ? ctx.Doc.ResolvePoints(o) : null;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var closed = o.Flag("closed", false) && p.Count > 2;
            var style = Style.Visible with
            {
                ArrowStart = closed ? null : Ann.Head(o, "headStart", ArrowHead.None),
                ArrowEnd = closed ? null : Ann.Head(o, "headEnd", ArrowHead.None),
                ArrowSize = Ann.HeadSize(o),
            };
            if (closed) return new Primitive[] { Prim.Polyline(p, style, true) with { Tag = AnnTags.Main } };
            var hops = IsConnector(o) ? AnnConnectors.Hops(o, p, ctx, this, true) : new List<Crossing>();
            var list = new List<Primitive> { AnnConnectors.MainPath(p, hops, style) };
            list.AddRange(AnnConnectors.BranchDots(o, p));
            list.AddRange(AnnConnectors.BusMark(o, p));
            return list;
        }

        /// <summary>Connector polylines are rails: other connector ends and junction dots dock anywhere on them.</summary>
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var a in base.Anchors(o, ctx)) yield return a;
            if (!o.Flag("closed", false)) foreach (var r in AnnConnectors.Rails(ctx.Doc.ResolvePoints(o))) yield return r;
        }

        /// <summary>Connectors crossing this one (their hops depend on where it runs).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => ConnectorPath(o, ctx) is { Count: >= 2 } p ? AnnConnectors.Find(o, p, ctx, this).Select(c => c.Other).Distinct().ToList() : Enumerable.Empty<string>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => Ann.ArrowActions(o, ArrowHead.None, ArrowHead.None)
                .Append(Ann.Toggle(o, "ann.closed", new I18nText("Closed", "Geschlossen"), IconIds.Toggle, "closed", false))
                .Append(AnnConnectors.HopsAction(o, true))
                .Concat(AnnConnectors.BusActions(o));
    }

    /// <summary>Straight arrow; head style per end is configurable (single or double arrow).</summary>
    public sealed class ArrowType : AnnPointType
    {
        private readonly ArrowHead _start;

        public ArrowType(bool doubleHeaded)
            : base(doubleHeaded ? "ann.arrow.double" : "ann.arrow",
                   doubleHeaded ? new I18nText("Double arrow", "Doppelpfeil") : new I18nText("Arrow", "Pfeil"),
                   AnnCategories.Lines, "arrow", "pfeil", doubleHeaded ? "double" : "single", doubleHeaded ? "doppel" : "einfach")
        {
            _start = doubleHeaded ? ArrowHead.Triangle : ArrowHead.None;
        }

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(_start, ArrowHead.Triangle).Concat(AnnConnectors.BusParams()).ToList();

        /// <summary>Drawn on with corners, an arrow becomes a polyline with the same heads.</summary>
        public override string? CornerType => "ann.polyline";

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => new Primitive[]
            {
                new LinePrim(p[0], p[p.Count - 1], Style.ThinLine with
                {
                    StrokeWidth = 0.35,
                    ArrowStart = Ann.Head(o, "headStart", _start),
                    ArrowEnd = Ann.Head(o, "headEnd", ArrowHead.Triangle),
                    ArrowSize = Ann.HeadSize(o),
                }) { Tag = AnnTags.Main },
            }.Concat(AnnConnectors.BusMark(o, new[] { p[0], p[p.Count - 1] })).ToList();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Ann.ArrowActions(o, _start, ArrowHead.Triangle).Concat(AnnConnectors.BusActions(o));
    }

    /// <summary>Curved arrow through three points (start, through-point, end) as a quadratic curve.</summary>
    public sealed class CurvedArrowType : AnnPointType
    {
        public CurvedArrowType() : base("ann.arrow.curved", new I18nText("Curved arrow", "Bogenpfeil"), AnnCategories.Lines, "arrow", "curve", "pfeil", "bogen", "gebogen") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 6), new Vec2(0, -6), new Vec2(15, 6) };
        protected override int MinPoints => 3;

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.Triangle).ToList();

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            // The middle point lies on the curve (t = 0.5): control = 2*m - (a + b)/2.
            var a = p[0]; var m = p[1]; var b = p[2];
            var c = m * 2 - (a + b) * 0.5;
            var style = Style.ThinLine with { StrokeWidth = 0.35, ArrowStart = Ann.Head(o, "headStart", ArrowHead.None), ArrowEnd = Ann.Head(o, "headEnd", ArrowHead.Triangle), ArrowSize = Ann.HeadSize(o) };
            // Cubic equivalent of the quadratic (better host support than quadratic segments).
            var c1 = a + (c - a) * (2.0 / 3); var c2 = b + (c - b) * (2.0 / 3);
            return new Primitive[] { Ann.Cubic(a, c1, c2, b, style, AnnTags.Main) };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Ann.ArrowActions(o, ArrowHead.None, ArrowHead.Triangle);
    }

    /// <summary>
    /// Orthogonal elbow connector routed automatically with one or two bends. It hops over earlier connectors it crosses
    /// (parameter <c>hops</c>), its segments are rails (another connector docked onto it gets a junction dot), and a
    /// decision exit label ("yes"/"no", parameter <c>exit</c>) is written in the language of the drawing.
    /// </summary>
    public sealed class ElbowArrowType : AnnPointType, IAnnConnector
    {
        public ElbowArrowType() : base("ann.arrow.elbow", new I18nText("Elbow connector", "Winkelverbinder"), AnnCategories.Lines, "elbow", "connector", "orthogonal", "verbinder", "winkel") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, -8), new Vec2(15, 8) };

        /// <summary>A label at the start ("yes"/"no" of a decision exit), edited like any element text.</summary>
        public override bool HasText => true;
        public override bool DependsOnNeighbours => true;

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.Triangle).Concat(new[]
        {
            P.Choice("route", "Routing", "Führung", "auto", ("auto", "Automatic", "Automatisch"), ("hv", "Horizontal, then vertical", "Erst waagerecht"),
                ("vh", "Vertical, then horizontal", "Erst senkrecht"), ("hvh", "Horizontal-vertical-horizontal", "Waagerecht-senkrecht-waagerecht"),
                ("vhv", "Vertical-horizontal-vertical", "Senkrecht-waagerecht-senkrecht")),
            P.Num("clearance", "Clearance beside the boxes", "Abstand neben den Kästen", 8, 1, 100, "mm", 1, advanced: true),
            P.TextSize(Ann.TextSize * 0.8),
            P.Text("exit", "Exit label (yes/no translated)", "Ausgangsbeschriftung (ja/nein übersetzt)", null),
            AnnConnectors.HopsParam(true),
        }).Concat(AnnConnectors.BusParams()).ToList();

        /// <summary>The label of an exit: "yes"/"no" in the drawing language, any other value as written.</summary>
        public static string ExitText(string exit, Lang lang) => exit.Trim().ToLowerInvariant() switch
        {
            "yes" or "ja" => lang == Lang.De ? "ja" : "yes",
            "no" or "nein" => lang == Lang.De ? "nein" : "no",
            _ => exit.Trim(),
        };

        /// <summary>The label drawn at the start: the element text, else the translated exit label.</summary>
        public static string LabelOf(SketchObject o, Lang lang) => !string.IsNullOrWhiteSpace(o.Text) ? o.Text! : ExitText(o.Str("exit", ""), lang);

        /// <summary>The drawn route of an elbow-routed line through its resolved points (side-aware when both ends are docked).</summary>
        public static List<Vec2> PathOf(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var route = o.Str("route", "auto");
            var sa = ExitSide(o, 0); var sb = ExitSide(o, p.Count - 1);
            return route == "auto" && sa is Vec2 da && sb is Vec2 db
                ? RouteSides(a, da, b, db, o.Num("clearance", 8))
                : Route(a, b, route, ExitHorizontal(o, 0), ExitHorizontal(o, p.Count - 1));
        }

        public IReadOnlyList<Vec2>? ConnectorPath(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            return p.Count < 2 ? null : PathOf(o, p);
        }

        /// <summary>
        /// Route between two docked ends that leave their boxes on known sides (<paramref name="da"/>, <paramref name="db"/>:
        /// outward unit vectors). Exits on the same side run around with <paramref name="clearance"/> ("C" shape: a "yes"
        /// exit to the right that joins a lower box from the right, a loop back into the left side of an earlier box);
        /// facing sides meet in the middle; perpendicular sides take one corner when it lies ahead of both ends.
        /// </summary>
        public static List<Vec2> RouteSides(Vec2 a, Vec2 da, Vec2 b, Vec2 db, double clearance)
        {
            var a1 = a + da * clearance; var b1 = b + db * clearance;
            bool ha = Math.Abs(da.X) > 0.5, hb = Math.Abs(db.X) > 0.5;
            var pts = new List<Vec2> { a };
            if (ha && hb)
            {
                if (Math.Sign(da.X) == Math.Sign(db.X))
                {
                    var x = da.X > 0 ? Math.Max(a1.X, b1.X) : Math.Min(a1.X, b1.X);
                    pts.Add(new Vec2(x, a.Y)); pts.Add(new Vec2(x, b.Y));
                }
                else if ((b.X - a.X) * da.X > 0) { var x = (a.X + b.X) / 2; pts.Add(new Vec2(x, a.Y)); pts.Add(new Vec2(x, b.Y)); }
                else { var y = (a.Y + b.Y) / 2; pts.Add(a1); pts.Add(new Vec2(a1.X, y)); pts.Add(new Vec2(b1.X, y)); pts.Add(b1); }
            }
            else if (!ha && !hb)
            {
                if (Math.Sign(da.Y) == Math.Sign(db.Y))
                {
                    var y = da.Y > 0 ? Math.Max(a1.Y, b1.Y) : Math.Min(a1.Y, b1.Y);
                    pts.Add(new Vec2(a.X, y)); pts.Add(new Vec2(b.X, y));
                }
                else if ((b.Y - a.Y) * da.Y > 0) { var y = (a.Y + b.Y) / 2; pts.Add(new Vec2(a.X, y)); pts.Add(new Vec2(b.X, y)); }
                else { var x = (a.X + b.X) / 2; pts.Add(a1); pts.Add(new Vec2(x, a1.Y)); pts.Add(new Vec2(x, b1.Y)); pts.Add(b1); }
            }
            else if (ha)
            {
                if ((b.X - a.X) * da.X > 0 && (a.Y - b.Y) * db.Y > 0) pts.Add(new Vec2(b.X, a.Y));
                else { pts.Add(a1); pts.Add(new Vec2(a1.X, b1.Y)); pts.Add(b1); }
            }
            else
            {
                if ((b.Y - a.Y) * da.Y > 0 && (a.X - b.X) * db.X > 0) pts.Add(new Vec2(a.X, b.Y));
                else { pts.Add(a1); pts.Add(new Vec2(b1.X, a1.Y)); pts.Add(b1); }
            }
            pts.Add(b);
            return Simplify(pts);
        }

        /// <summary>Drops repeated points and middle points of straight runs.</summary>
        public static List<Vec2> Simplify(List<Vec2> pts)
        {
            var list = new List<Vec2>();
            foreach (var p in pts)
            {
                if (list.Count > 0 && list[list.Count - 1].DistanceTo(p) < 1e-9) continue;
                if (list.Count >= 2)
                {
                    var q = list[list.Count - 2]; var r = list[list.Count - 1];
                    var cross = (r.X - q.X) * (p.Y - r.Y) - (r.Y - q.Y) * (p.X - r.X);
                    if (Math.Abs(cross) < 1e-9) { list[list.Count - 1] = p; continue; }
                }
                list.Add(p);
            }
            return list;
        }

        /// <summary>Outward direction of a docked end from its box side (u, v = ±0.5), or null if not on a side.</summary>
        internal static Vec2? ExitSide(SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a?.AnchorId is { } rid && rid.StartsWith("rail", StringComparison.Ordinal)) return null; // docked on a line, not a box side
            if (a is null) return null;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6 && Math.Abs(a.V) < 0.5 - 1e-6) return new Vec2(Math.Sign(a.U), 0);
            if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6 && Math.Abs(a.U) < 0.5 - 1e-6) return new Vec2(0, Math.Sign(a.V));
            return null;
        }

        /// <summary>Computes the orthogonal route. Attached ends leave their box perpendicular to the edge they sit on.</summary>
        public static List<Vec2> Route(Vec2 a, Vec2 b, string route, bool? startHorizontal = null, bool? endHorizontal = null)
        {
            var d = b - a;
            if (route == "auto")
            {
                var sh = startHorizontal ?? Math.Abs(d.X) >= Math.Abs(d.Y);
                var eh = endHorizontal ?? sh;
                route = sh ? (eh ? "hvh" : "hv") : (eh ? "vh" : "vhv");
            }
            if (Math.Abs(d.X) < 1e-9 || Math.Abs(d.Y) < 1e-9) return new List<Vec2> { a, b };
            return route switch
            {
                "hv" => new List<Vec2> { a, new(b.X, a.Y), b },
                "vh" => new List<Vec2> { a, new(a.X, b.Y), b },
                "vhv" => new List<Vec2> { a, new(a.X, (a.Y + b.Y) / 2), new(b.X, (a.Y + b.Y) / 2), b },
                _ => new List<Vec2> { a, new((a.X + b.X) / 2, a.Y), new((a.X + b.X) / 2, b.Y), b },
            };
        }

        internal static bool? ExitHorizontal(SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a?.AnchorId is { } rid && rid.StartsWith("rail", StringComparison.Ordinal)) return null;
            if (a is null) return null;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6 && Math.Abs(a.V) < 0.5 - 1e-6) return true;
            if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6) return false;
            return null;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            // Both ends docked on box sides (flowcharts): side-aware routing that never runs back through a box edge.
            var pts = PathOf(o, p);
            var style = Style.ThinLine with { StrokeWidth = 0.35, ArrowStart = Ann.Head(o, "headStart", ArrowHead.None), ArrowEnd = Ann.Head(o, "headEnd", ArrowHead.Triangle), ArrowSize = Ann.HeadSize(o) };
            var list = new List<Primitive> { AnnConnectors.MainPath(pts, AnnConnectors.Hops(o, pts, ctx, this, true), style) };
            var label = LabelOf(o, ctx.Lang);
            if (!string.IsNullOrWhiteSpace(label) && pts.Count >= 2)
            {
                // Label beside the first run, just after the start ("ja" right of a line leaving downwards, "nein" above a
                // line leaving sideways).
                var size = TextSizeOf(o, Ann.TextSize * 0.8);
                var d = pts[1] - pts[0];
                var len = d.Length;
                var dir = len > 1e-9 ? d * (1 / len) : new Vec2(0, 1);
                var at = pts[0] + dir * Math.Min(len * 0.5, size * 1.4);
                var vertical = Math.Abs(dir.Y) > Math.Abs(dir.X);
                list.Add(vertical
                    ? Ann.Text(at + new Vec2(size * 0.45, 0), label, size, "label", TextAnchor.Start)
                    : Ann.Text(at + new Vec2(0, -size * 0.75), label, size, "label", dir.X >= 0 ? TextAnchor.Start : TextAnchor.End));
            }
            list.AddRange(AnnConnectors.BranchDots(o, p));
            list.AddRange(AnnConnectors.BusMark(o, pts));
            return list;
        }

        /// <summary>The routed path is a rail: other connector ends and junction dots dock anywhere on it (T junctions).</summary>
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var a in base.Anchors(o, ctx)) yield return a;
            if (ConnectorPath(o, ctx) is { } path) foreach (var r in AnnConnectors.Rails(path)) yield return r;
        }

        /// <summary>Connectors crossing this one (their hops depend on where it runs).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => ConnectorPath(o, ctx) is { Count: >= 2 } p ? AnnConnectors.Find(o, p, ctx, this).Select(c => c.Other).Distinct().ToList() : Enumerable.Empty<string>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.ArrowActions(o, ArrowHead.None, ArrowHead.Triangle)) yield return a;
            // Option pictures for the fixed routes: the elbow line horizontal-vertical-horizontal or vertical-horizontal-vertical.
            foreach (var a in Ann.Choice(o, "ann.route", new I18nText("Routing", "Führung"), "route", "auto", "route", new (string, I18nText, string?)[]
            {
                ("auto", new I18nText("Automatic", "Automatisch"), null),
                ("hvh", new I18nText("Horizontal first", "Erst waagerecht"), null),
                ("vhv", new I18nText("Vertical first", "Erst senkrecht"), null),
            }, v => v switch
            {
                "hvh" => new Primitive[] { Prim.Polyline(new[] { new Vec2(-6, 4), new Vec2(0, 4), new Vec2(0, -4), new Vec2(6, -4) }, Style.Visible with { StrokeWidth = 0.5 }) },
                "vhv" => new Primitive[] { Prim.Polyline(new[] { new Vec2(-6, 4), new Vec2(-6, 0), new Vec2(6, 0), new Vec2(6, -4) }, Style.Visible with { StrokeWidth = 0.5 }) },
                _ => null,
            })) yield return a;
            yield return AnnConnectors.HopsAction(o, true);
            foreach (var a in AnnConnectors.BusActions(o)) yield return a;
        }
    }

    /// <summary>Thin dash-dot centre line (axes, symmetry). Note: not permitted in USPTO drawings (37 CFR 1.84(h)).</summary>
    public sealed class CenterlineType : AnnPointType
    {
        public CenterlineType() : base("ann.centerline", new I18nText("Centre line", "Mittellinie"), AnnCategories.PatentMarks, "center", "centre", "axis", "symmetry", "mittellinie", "achse", "strichpunkt") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => new Primitive[] { new LinePrim(p[0], p[p.Count - 1], Style.CenterLine) { Tag = AnnTags.Main } };
    }

    /// <summary>Break line: thin line with a zig-zag (long break) or a freehand wavy course (short break).</summary>
    public sealed class BreaklineType : AnnPointType
    {
        public BreaklineType() : base("ann.breakline", new I18nText("Break line", "Bruchlinie"), AnnCategories.PatentMarks, "break", "bruch", "zigzag", "freehand", "abbruch") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, -15), new Vec2(0, 15) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("form", "Form", "Form", "zigzag", ("zigzag", "Zig-zag", "Zickzack"), ("wavy", "Freehand (wavy)", "Freihand (wellig)"))
                .WithOptionIcons(v => v == "wavy" ? IconIds.CourseWavy : IconIds.CourseZigzag),
            P.Num("amplitude", "Amplitude", "Amplitude", 2.5, 0.5, 20),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return Array.Empty<Primitive>();
            var d = (b - a) / len; var n = d.Perp;
            var amp = o.Num("amplitude", 2.5);
            if (o.Str("form", "zigzag") == "wavy")
            {
                // Freehand break: a gentle S-wave of cubic segments.
                var waves = Math.Max(1, (int)Math.Round(len / 12));
                var segs = new List<PathSeg> { PathSeg.Move(a) };
                var step = len / waves;
                for (var i = 0; i < waves; i++)
                {
                    var s0 = a + d * (i * step);
                    segs.Add(PathSeg.Cubic(s0 + d * (step / 3) + n * amp, s0 + d * (2 * step / 3) - n * amp, s0 + d * step));
                }
                return new Primitive[] { new PathPrim(segs, Style.ThinLine) { Tag = AnnTags.Main } };
            }
            // Long break: straight runs with one sharp "Z" in the middle (ISO 128 style).
            var mid = a + d * (len / 2);
            var w = Math.Min(len / 4, amp * 1.4);
            var pts = new[] { a, mid - d * w, mid - d * (w / 2) + n * amp * 1.6, mid + d * (w / 2) - n * amp * 1.6, mid + d * w, b };
            return new Primitive[] { Prim.Polyline(pts, Style.ThinLine) with { Tag = AnnTags.Main } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.break.form", new I18nText("Break line", "Bruchlinie"), "form", "zigzag", "form", new (string, I18nText, string?)[]
            {
                ("zigzag", new I18nText("Zig-zag", "Zickzack"), IconIds.CourseZigzag),
                ("wavy", new I18nText("Freehand", "Freihand"), IconIds.CourseWavy),
            })) yield return a;
        }
    }

    /// <summary>Curly brace or square bracket between two points (grouping in exploded views, US 37 CFR 1.84(h)(1)).</summary>
    public sealed class BracketType : AnnPointType
    {
        private readonly bool _curly;

        public BracketType(bool curly)
            : base(curly ? "ann.bracket.curly" : "ann.bracket.square",
                   curly ? new I18nText("Curly bracket", "Geschweifte Klammer") : new I18nText("Square bracket", "Eckige Klammer"),
                   AnnCategories.PatentMarks, "bracket", "brace", "klammer", "exploded", "explosion", curly ? "curly" : "square")
        {
            _curly = curly;
        }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, -20), new Vec2(0, 20) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Num("depth", "Depth", "Tiefe", _curly ? 5 : 3, 0.5, 50),
            P.Bool("flip", "Other side", "Andere Seite", false),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return Array.Empty<Primitive>();
            var d = (b - a) / len;
            var n = d.Perp * (o.Flag("flip", false) ? -1 : 1);
            var h = o.Num("depth", _curly ? 5 : 3);
            Vec2 L(double x, double y) => a + d * x + n * y;
            if (!_curly)
                return new Primitive[] { Prim.Polyline(new[] { L(0, 0), L(0, h), L(len, h), L(len, 0) }, Style.ThinLine) with { Tag = AnnTags.Main } };
            var r = Math.Min(h / 2, len / 4);
            var half = len / 2;
            var segs = new List<PathSeg>
            {
                PathSeg.Move(L(0, 0)),
                PathSeg.Cubic(L(0, h * 0.35), L(r * 0.45, h / 2), L(r, h / 2)),
                PathSeg.Line(L(half - r, h / 2)),
                PathSeg.Cubic(L(half - r * 0.45, h / 2), L(half, h * 0.65), L(half, h)),
                PathSeg.Cubic(L(half, h * 0.65), L(half + r * 0.45, h / 2), L(half + r, h / 2)),
                PathSeg.Line(L(len - r, h / 2)),
                PathSeg.Cubic(L(len - r * 0.45, h / 2), L(len, h * 0.35), L(len, 0)),
            };
            return new Primitive[] { new PathPrim(segs, Style.ThinLine) { Tag = AnnTags.Main } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.flip", new I18nText("Put on other side", "Auf die andere Seite"), IconIds.SideSwap, "flip", false);
        }
    }

    /// <summary>Force vector: thick arrow with a label (default "F").</summary>
    public sealed class ForceType : AnnPointType
    {
        public ForceType() : base("ann.force", new I18nText("Force", "Kraft"), AnnCategories.Mechanics, "force", "kraft", "vector", "vektor", "load", "last") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, -20), new Vec2(0, 0) };
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("label", "Label", "Beschriftung", "F"),
            P.Num("value", "Magnitude (beam diagrams)", "Betrag (Schnittgrößenverläufe)", null, -1e9, 1e9, null, advanced: true),
            P.TextSize(Ann.TextSize),
        };

        /// <summary>Beam diagrams on the page read forces touching a beam (<see cref="BeamScene"/>).</summary>
        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => BeamScene.DiagramsNear(o, ctx.Doc);

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var list = new List<Primitive>
            {
                new LinePrim(a, b, Style.Visible with { StrokeWidth = 0.7, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Large }) { Tag = AnnTags.Main },
            };
            var text = o.Text ?? o.Str("label", "F");
            if (!string.IsNullOrEmpty(text))
            {
                var size = TextSizeOf(o);
                var d = (b - a).Normalized();
                if (d == Vec2.Zero) d = new Vec2(1, 0);
                var n = d.Perp;
                if (n.X < 0 || (Math.Abs(n.X) < 1e-9 && n.Y > 0)) n = -n; // label to the right/top of the shaft
                var pos = Vec2.Lerp(a, b, 0.35) + n * (1.5 + Ann.HalfExtentAlong(text, size, n));
                list.Add(Ann.Text(pos, text, size, italic: true));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) { yield return Ann.SwapAction(); }
    }
}
