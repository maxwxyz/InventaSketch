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

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.None).ToList();

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => new Primitive[] { new LinePrim(p[0], p[p.Count - 1], Style.Visible with { ArrowStart = Ann.Head(o, "headStart", ArrowHead.None), ArrowEnd = Ann.Head(o, "headEnd", ArrowHead.None), ArrowSize = Ann.HeadSize(o) }) { Tag = AnnTags.Main } };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Ann.ArrowActions(o, ArrowHead.None, ArrowHead.None);
    }

    /// <summary>Open or closed polyline through all points.</summary>
    public sealed class PolylineType : AnnPointType
    {
        public PolylineType() : base("ann.polyline", new I18nText("Polyline", "Polylinie"), AnnCategories.Lines, "polyline", "linienzug") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 8), new Vec2(-5, -8), new Vec2(5, 8), new Vec2(15, -8) };

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.None).Append(P.Bool("closed", "Closed", "Geschlossen", false)).ToList();

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var closed = o.Flag("closed", false) && p.Count > 2;
            var style = Style.Visible with
            {
                ArrowStart = closed ? null : Ann.Head(o, "headStart", ArrowHead.None),
                ArrowEnd = closed ? null : Ann.Head(o, "headEnd", ArrowHead.None),
                ArrowSize = Ann.HeadSize(o),
            };
            return new Primitive[] { Prim.Polyline(p, style, closed) with { Tag = AnnTags.Main } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => Ann.ArrowActions(o, ArrowHead.None, ArrowHead.None).Append(Ann.Toggle(o, "ann.closed", new I18nText("Closed", "Geschlossen"), "polygon", "closed", false));
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

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(_start, ArrowHead.Triangle).ToList();

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
            };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Ann.ArrowActions(o, _start, ArrowHead.Triangle);
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

    /// <summary>Orthogonal elbow connector routed automatically with one or two bends.</summary>
    public sealed class ElbowArrowType : AnnPointType
    {
        public ElbowArrowType() : base("ann.arrow.elbow", new I18nText("Elbow connector", "Winkelverbinder"), AnnCategories.Lines, "elbow", "connector", "orthogonal", "verbinder", "winkel") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, -8), new Vec2(15, 8) };

        public override IReadOnlyList<ParamDef> Parameters => P.Heads(ArrowHead.None, ArrowHead.Triangle).Append(
            P.Choice("route", "Routing", "Führung", "auto", ("auto", "Automatic", "Automatisch"), ("hv", "Horizontal, then vertical", "Erst waagerecht"),
                ("vh", "Vertical, then horizontal", "Erst senkrecht"), ("hvh", "Horizontal-vertical-horizontal", "Waagerecht-senkrecht-waagerecht"),
                ("vhv", "Vertical-horizontal-vertical", "Senkrecht-waagerecht-senkrecht"))).ToList();

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

        private static bool? ExitHorizontal(SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a is null) return null;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6 && Math.Abs(a.V) < 0.5 - 1e-6) return true;
            if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6) return false;
            return null;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var pts = Route(a, b, o.Str("route", "auto"), ExitHorizontal(o, 0), ExitHorizontal(o, p.Count - 1));
            var style = Style.ThinLine with { StrokeWidth = 0.35, ArrowStart = Ann.Head(o, "headStart", ArrowHead.None), ArrowEnd = Ann.Head(o, "headEnd", ArrowHead.Triangle), ArrowSize = Ann.HeadSize(o) };
            return new Primitive[] { Prim.Polyline(pts, style) with { Tag = AnnTags.Main } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.ArrowActions(o, ArrowHead.None, ArrowHead.Triangle)) yield return a;
            yield return Ann.SetChoice(o, "ann.route.auto", new I18nText("Automatic routing", "Automatische Führung"), "route-auto", "route", "auto", "auto", "route");
            yield return Ann.SetChoice(o, "ann.route.hvh", new I18nText("Route horizontal first", "Erst waagerecht führen"), "route-hvh", "route", "hvh", "auto", "route");
            yield return Ann.SetChoice(o, "ann.route.vhv", new I18nText("Route vertical first", "Erst senkrecht führen"), "route-vhv", "route", "vhv", "auto", "route");
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
            yield return Ann.SetChoice(o, "ann.break.zigzag", new I18nText("Zig-zag break", "Zickzack-Bruch"), "break-zigzag", "form", "zigzag", "zigzag", "form");
            yield return Ann.SetChoice(o, "ann.break.wavy", new I18nText("Freehand break", "Freihand-Bruch"), "break-wavy", "form", "wavy", "zigzag", "form");
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
            yield return Ann.Toggle(o, "ann.flip", new I18nText("Flip side", "Seite wechseln"), "flip", "flip", false);
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
            P.TextSize(Ann.TextSize),
        };

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
