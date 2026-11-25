using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Junction dot: a filled dot that marks where lines are connected. Its centre is a port, so it docks anywhere on a
    /// connector (rails of the elbow connector and connector polylines) or on a box port.
    /// </summary>
    public sealed class JunctionType : AnnBoxType
    {
        public JunctionType() : base("ann.junction", new I18nText("Junction dot (lines)", "Knotenpunkt (Linien)"), AnnCategories.Lines,
            "junction", "dot", "node", "connection", "branch", "knotenpunkt", "verbindungspunkt", "abzweig", "punkt")
        { }

        public override Size2 DefaultSize => new(1.6, 1.6);
        public override bool PreferUniformScale => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new[] { AnnConnectors.Dot(Vec2.Zero, Math.Max(0.2, Math.Min(o.Size.W, o.Size.H) / 2)) };

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "dot");
        }
    }

    /// <summary>
    /// Hollow block arrow along a straight or orthogonal route, drawn as one closed outline (native freeform): shaft of
    /// <c>width</c> (default 6 mm), heads proportional to the width at the start and/or end, fill none, hatch or dots, and an
    /// optional text along the shaft. Presets: straight, double-headed, elbow and U-turn.
    /// </summary>
    public sealed class WideArrowType : AnnPointType
    {
        public WideArrowType() : base("ann.arrow.wide", new I18nText("Block arrow (hollow)", "Blockpfeil (hohl)"), AnnCategories.Lines,
            "block arrow", "wide arrow", "hollow arrow", "fat arrow", "blockpfeil", "breiter pfeil", "hohlpfeil", "flow", "strömung", "fluss")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };
        public override bool InsertablePoints => true;
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Num("width", "Shaft width", "Schaftbreite", 6, 1, 50, "mm", 0.5),
            P.Bool("headStart", "Head at start", "Spitze am Anfang", false),
            P.Bool("headEnd", "Head at end", "Spitze am Ende", true),
            P.Choice("route", "Routing", "Führung", "straight", ("straight", "Straight through the points", "Gerade durch die Punkte"), ("orthogonal", "Orthogonal", "Orthogonal"))
                .WithOptionIcons(v => v == "orthogonal" ? IconIds.RouteOrthogonal : IconIds.RouteFree),
            P.Choice("fill", "Fill", "Füllung", "none", ("none", "None (hollow)", "Keine (hohl)"), ("hatch", "Hatched", "Schraffiert"), ("dots", "Dotted", "Punktiert"))
                .WithOptionIcons(v => v switch { "hatch" => IconIds.FillHatch, "dots" => IconIds.FillDots, _ => IconIds.FillNone }),
            P.TextSize(3.5),
        };

        /// <summary>The centre line of the arrow through its points (orthogonal when <c>route</c> says so).</summary>
        public static List<Vec2> CenterLine(SketchObject o, IReadOnlyList<Vec2> p)
            => ElbowArrowType.Simplify(o.Str("route", "straight") == "orthogonal" ? AnnConnectors.Orthogonal(p) : p.ToList());

        /// <summary>Head length and half width for a shaft width.</summary>
        public static (double Length, double Half) HeadOf(double width) => (width * 1.2, width);

        /// <summary>The closed outline of the arrow (shaft with mitred corners, heads) in page coordinates.</summary>
        public static List<PathSeg> Outline(IReadOnlyList<Vec2> c, double width, bool headStart, bool headEnd)
        {
            var hw = width / 2;
            var (lh, hh) = HeadOf(width);
            // Heads take at most most of their segment (both heads on one segment: half each).
            double First() => c[0].DistanceTo(c[1]);
            double Last() => c[c.Count - 2].DistanceTo(c[c.Count - 1]);
            var share = c.Count == 2 && headStart && headEnd ? 0.45 : 0.9;
            var ls = headStart ? Math.Min(lh, First() * share) : 0;
            var le = headEnd ? Math.Min(lh, Last() * share) : 0;
            var shaft = AnnConnectors.Trim(AnnConnectors.Trim(c, le, false), ls, true);
            var left = AnnConnectors.Offset(shaft, hw);
            var right = AnnConnectors.Offset(shaft, -hw);
            Vec2 Nrm(Vec2 a, Vec2 b) { var d = (b - a).Normalized(); return new Vec2(-d.Y, d.X); }
            var n0 = Nrm(c[0], c[1]);
            var n1 = Nrm(c[c.Count - 2], c[c.Count - 1]);
            var segs = new List<PathSeg> { PathSeg.Move(left[0]) };
            for (var i = 1; i < left.Count; i++) segs.Add(PathSeg.Line(left[i]));
            var se = shaft[shaft.Count - 1];
            if (headEnd)
            {
                segs.Add(PathSeg.Line(se + n1 * hh));
                segs.Add(PathSeg.Line(c[c.Count - 1]));
                segs.Add(PathSeg.Line(se - n1 * hh));
            }
            for (var i = right.Count - 1; i >= 0; i--) segs.Add(PathSeg.Line(right[i]));
            if (headStart)
            {
                var ss = shaft[0];
                segs.Add(PathSeg.Line(ss - n0 * hh));
                segs.Add(PathSeg.Line(c[0]));
                segs.Add(PathSeg.Line(ss + n0 * hh));
            }
            segs.Add(PathSeg.Close);
            return segs;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var c = CenterLine(o, p);
            if (c.Count < 2 || c[0].DistanceTo(c[c.Count - 1]) < Geo.Eps && c.Count == 2) return Array.Empty<Primitive>();
            var width = Math.Max(1, o.Num("width", 6));
            var fill = o.Str("fill", "none") switch
            {
                "hatch" => Fill.Hatched(new HatchPattern(45, Math.Max(1, width / 4)) { Width = 0.18 }),
                "dots" => Fill.Dotted(Math.Max(0.8, width / 5)),
                _ => Fill.None,
            };
            var style = Style.ThinLine with { StrokeWidth = 0.35, Fill = fill };
            var list = new List<Primitive> { new PathPrim(Outline(c, width, o.Flag("headStart", false), o.Flag("headEnd", true)), style) { Tag = AnnTags.Main } };
            if (!string.IsNullOrWhiteSpace(o.Text))
            {
                // Along the longest run of the shaft, inside it, readable from below or the right.
                var (mid, dir) = AnnConnectors.Longest(c);
                var run = 0.0;
                for (var i = 0; i + 1 < c.Count; i++) run = Math.Max(run, c[i].DistanceTo(c[i + 1]));
                var size = Math.Min(TextSizeOf(o, 3.5), width * 0.6);
                var avail = Math.Max(2, run - HeadOf(width).Length * 1.2);
                var (fitted, fs) = Ann.Fit(o.Text!.Replace("\n", " "), size, avail, width * 0.8);
                list.Add(Ann.Text(mid, fitted.Replace("\n", " "), Math.Min(fs, size), "label", rotation: Ann.ReadableAngle(dir)));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.wide.headstart", new I18nText("Head at start", "Spitze am Anfang"), IconIds.ArrowStart, "headStart", false, "heads");
            yield return Ann.Toggle(o, "ann.wide.headend", new I18nText("Head at end", "Spitze am Ende"), IconIds.ArrowEnd, "headEnd", true, "heads");
            yield return Ann.SwapAction();
            foreach (var a in Ann.Choice(o, "ann.wide.fill", new I18nText("Fill", "Füllung"), "fill", "none", "fill", new (string, I18nText, string?)[]
            {
                ("none", new I18nText("Hollow", "Hohl"), IconIds.FillNone),
                ("hatch", new I18nText("Hatched", "Schraffiert"), IconIds.FillHatch),
                ("dots", new I18nText("Dotted", "Punktiert"), IconIds.FillDots),
            })) yield return a;
            yield return new ContextAction("ann.wide.wider", new I18nText("Wider", "Breiter"), IconIds.Increase, x => x.WithParam("width", Math.Min(50, x.Num("width", 6) + 1))) { Group = "width" };
            yield return new ContextAction("ann.wide.narrower", new I18nText("Narrower", "Schmaler"), IconIds.Decrease, x => x.WithParam("width", Math.Max(1, x.Num("width", 6) - 1))) { Group = "width" };
        }
    }

    /// <summary>
    /// Double-line arrow (⇒): two parallel strokes (gap 2 mm) along an orthogonal route with an open chevron head. With
    /// two points it routes and docks like the elbow connector; with more points it runs orthogonally through them.
    /// </summary>
    public sealed class DoubleLineArrowType : AnnPointType
    {
        public DoubleLineArrowType() : base("ann.arrow.doubleline", new I18nText("Double-line arrow (⇒)", "Doppellinienpfeil (⇒)"), AnnCategories.Lines,
            "double line", "double-line arrow", "implies", "doppellinie", "doppellinienpfeil", "folgepfeil", "⇒", "transfer", "übergang")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override bool InsertablePoints => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Num("gap", "Gap between the lines", "Abstand der Linien", 2, 0.5, 10, "mm", 0.25),
            P.Bool("headStart", "Head at start", "Spitze am Anfang", false),
            P.Bool("headEnd", "Head at end", "Spitze am Ende", true),
            P.Num("clearance", "Clearance beside the boxes", "Abstand neben den Kästen", 8, 1, 100, "mm", 1, advanced: true),
        };

        /// <summary>The centre route: elbow routing between two (docked) points, else orthogonal through all points.</summary>
        public static List<Vec2> RouteOf(SketchObject o, IReadOnlyList<Vec2> p)
            => p.Count == 2 ? ElbowArrowType.PathOf(o, p) : AnnConnectors.Orthogonal(p);

        /// <summary>Chevron length and half width for a gap.</summary>
        public static (double Length, double Half) HeadOf(double gap) => (gap * 0.8 + 2.4, gap / 2 + 1.8);

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var c = RouteOf(o, p);
            if (c.Count < 2) return Array.Empty<Primitive>();
            var gap = Math.Max(0.5, o.Num("gap", 2));
            bool hs = o.Flag("headStart", false), he = o.Flag("headEnd", true);
            var (lh, half) = HeadOf(gap);
            // The strokes end where they meet the legs of the chevron.
            var cut = lh * (gap / 2) / half;
            var shaft = AnnConnectors.Trim(AnnConnectors.Trim(c, he ? cut : 0, false), hs ? cut : 0, true);
            var style = Style.ThinLine with { StrokeWidth = 0.35 };
            var list = new List<Primitive>
            {
                Prim.Polyline(AnnConnectors.Offset(shaft, gap / 2), style) with { Tag = AnnTags.Main },
                Prim.Polyline(AnnConnectors.Offset(shaft, -gap / 2), style) with { Tag = AnnTags.Main },
            };
            void Chevron(Vec2 tip, Vec2 from)
            {
                var d = (tip - from).Normalized();
                var n = new Vec2(-d.Y, d.X);
                list.Add(Prim.Polyline(new[] { tip - d * lh + n * half, tip, tip - d * lh - n * half }, style) with { Tag = "head" });
            }
            if (he) Chevron(c[c.Count - 1], c[c.Count - 2]);
            if (hs) Chevron(c[0], c[1]);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.doubleline.headstart", new I18nText("Head at start", "Spitze am Anfang"), IconIds.ArrowStart, "headStart", false, "heads");
            yield return Ann.Toggle(o, "ann.doubleline.headend", new I18nText("Head at end", "Spitze am Ende"), IconIds.ArrowEnd, "headEnd", true, "heads");
            yield return Ann.SwapAction();
        }
    }

    /// <summary>
    /// Arrow pair (request / response): two parallel arrows in opposite directions (gap 3 mm) between two points, each with
    /// an optional label on its outer side. "Swap directions" exchanges which side runs forward.
    /// </summary>
    public sealed class ArrowPairType : AnnPointType
    {
        public ArrowPairType() : base("ann.arrow.pair", new I18nText("Arrow pair (request / response)", "Pfeilpaar (Anfrage / Antwort)"), AnnCategories.Lines,
            "arrow pair", "request", "response", "exchange", "bidirectional", "pfeilpaar", "anfrage", "antwort", "austausch", "hin und zurück", "gegenpfeile")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Num("gap", "Gap between the arrows", "Abstand der Pfeile", 3, 0.5, 30, "mm", 0.5),
            P.Text("labelForward", "Label of the forward arrow", "Beschriftung Hinpfeil", null),
            P.Text("labelBack", "Label of the backward arrow", "Beschriftung Rückpfeil", null),
            P.Bool("swap", "Swap directions", "Richtungen tauschen", false),
            P.TextSize(Ann.TextSize * 0.8),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return Array.Empty<Primitive>();
            var d = (b - a) / len;
            var up = AnnConnectors.UpNormal(d);
            var g = Math.Max(0.5, o.Num("gap", 3)) / 2;
            var fwd = o.Flag("swap", false) ? up * -1 : up;
            var style = Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
            var list = new List<Primitive>
            {
                new LinePrim(a + fwd * g, b + fwd * g, style) { Tag = AnnTags.Main },
                new LinePrim(b - fwd * g, a - fwd * g, style) { Tag = AnnTags.Main },
            };
            var size = TextSizeOf(o, Ann.TextSize * 0.8);
            var mid = Vec2.Lerp(a, b, 0.5);
            var angle = Ann.ReadableAngle(d);
            foreach (var (key, side) in new[] { ("labelForward", fwd), ("labelBack", fwd * -1) })
            {
                var text = o.Str(key, "");
                if (string.IsNullOrWhiteSpace(text)) continue;
                list.Add(Ann.Text(mid + side * (g + 1 + size * 0.5), text, size, "label", rotation: angle));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.pair.swap", new I18nText("Reverse directions", "Richtungen umkehren"), IconIds.Reverse, "swap", false, "direction");
        }
    }

    /// <summary>Heat flow arrow: wavy shaft with a straight end and a filled head, labelled "Q" (italic, beside the shaft).</summary>
    public sealed class HeatArrowType : AnnPointType
    {
        public HeatArrowType() : base("ann.arrow.heat", new I18nText("Heat flow arrow (Q)", "Wärmestrompfeil (Q)"), AnnCategories.Lines,
            "heat", "heat flow", "thermal", "radiation", "wärme", "wärmestrom", "wärmefluss", "strahlung", "q", "wavy arrow", "wellenpfeil")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("label", "Label", "Beschriftung", "Q"),
            P.Num("amplitude", "Wave amplitude", "Wellenhöhe", 1.2, 0.2, 10),
            P.TextSize(Ann.TextSize),
        };

        /// <summary>The wavy shaft: half waves of about 3 mm, the last part straight so the head points along the line.</summary>
        public static List<PathSeg> Shaft(Vec2 a, Vec2 b, double amp)
        {
            var len = a.DistanceTo(b);
            var d = (b - a) / Math.Max(len, Geo.Eps);
            var n = d.Perp;
            var straight = Math.Min(4, len * 0.3);
            var wave = len - straight;
            var k = Math.Max(2, (int)Math.Round(wave / 3));
            var hl = wave / k;
            var segs = new List<PathSeg> { PathSeg.Move(a) };
            for (var i = 0; i < k; i++)
            {
                var s0 = a + d * (i * hl);
                var sign = i % 2 == 0 ? 1 : -1;
                segs.Add(PathSeg.Cubic(s0 + d * (hl / 3) + n * (amp * sign * 1.33), s0 + d * (2 * hl / 3) + n * (amp * sign * 1.33), s0 + d * hl));
            }
            segs.Add(PathSeg.Line(b));
            return segs;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            if (a.DistanceTo(b) < Geo.Eps) return Array.Empty<Primitive>();
            var amp = Math.Max(0.2, o.Num("amplitude", 1.2));
            var list = new List<Primitive>
            {
                new PathPrim(Shaft(a, b, amp), Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium }) { Tag = AnnTags.Main },
            };
            var text = !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("label", "Q");
            if (!string.IsNullOrEmpty(text))
            {
                var size = TextSizeOf(o);
                var d = (b - a).Normalized();
                var n = AnnConnectors.UpNormal(d);
                var pos = Vec2.Lerp(a, b, 0.4) + n * (amp + 1.2 + Ann.HalfExtentAlong(text, size, n));
                list.Add(Ann.Text(pos, text, size, italic: true));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) { yield return Ann.SwapAction(); }
    }
}
