using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Shared dimension parameters and helpers (ISO 129-1 inspired; figures for applications normally avoid dimensions).</summary>
    internal static class Dim
    {
        public const double TextSize = 3.5;
        public const double Gap = 1.0;       // gap between feature and extension line
        public const double Overshoot = 2.0; // extension beyond the dimension line

        public static IEnumerable<ParamDef> Common()
        {
            yield return P.Choice("terminator", "Terminator", "Begrenzung", "arrow", ("arrow", "Arrow", "Pfeil"), ("open", "Open arrow", "Offener Pfeil"), ("tick", "Oblique tick", "Schrägstrich"), ("dot", "Dot", "Punkt")).WithOptionIcons(IconIds.ForHead);
            yield return P.Text("text", "Override text", "Ersatztext", null);
            yield return P.Text("prefix", "Prefix", "Präfix", null);
            yield return P.Text("unit", "Unit suffix", "Einheit", null);
            yield return P.Int("decimals", "Decimals", "Nachkommastellen", 1, 0, 4);
            yield return P.Num("scale", "Drawing scale factor", "Maßstabsfaktor", 1, 0.0001, 10000, null, null, advanced: true);
            yield return P.TextSize(TextSize);
        }

        public static ArrowHead Terminator(SketchObject o) => o.Str("terminator", "arrow") switch
        {
            "tick" => ArrowHead.Tick,
            "dot" => ArrowHead.Oval,
            "open" => ArrowHead.Open,
            _ => ArrowHead.Triangle,
        };

        public static Style LineStyle(SketchObject o, bool start = true, bool end = true)
        {
            var t = Terminator(o);
            return Style.ThinLine with { ArrowStart = start ? t : ArrowHead.None, ArrowEnd = end ? t : ArrowHead.None, ArrowSize = ArrowSize.Large };
        }

        /// <summary>Formats a measured value with prefix, decimals, scale and unit unless override text is set.</summary>
        public static string Label(SketchObject o, double measured, string defaultPrefix = "", string defaultSuffix = "")
        {
            var over = !string.IsNullOrEmpty(o.Text) ? o.Text : o.Str("text", "");
            if (!string.IsNullOrEmpty(over)) return over!;
            var v = measured * o.Num("scale", 1);
            var unit = o.Str("unit", "");
            return o.Str("prefix", defaultPrefix) + Ann.Fmt(v, o.Int("decimals", 1)) + defaultSuffix + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
        }

        private const string Letters = "abcdefghkmnpqrstuvwxyz";

        /// <summary>
        /// Clean slides for sharing (<see cref="Sanitizer"/>): the shown value made unrecognisable. Normalised: a letter
        /// instead of the number and the scale factor reset (the drawn geometry keeps the proportions); randomised: the drawing scale factor varied by
        /// a random factor (override texts: their numbers varied). Override texts are values, so "replace texts" leaves them.
        /// </summary>
        public static SketchObject Sanitize(ObjectType type, SketchObject o, SanitizeOptions opt, Random rng)
        {
            var r = Sanitizer.Basic(type, o, opt with { ReplaceTexts = false }, rng);
            var textOver = !string.IsNullOrEmpty(o.Text);
            var over = textOver ? o.Text! : o.Str("text", "");
            switch (opt.Mode)
            {
                case SanitizeMode.Normalised:
                    return r.WithParam("text", Letters[rng.Next(Letters.Length)].ToString()).WithParam("scale", 1.0) with { Text = null };
                case SanitizeMode.Randomised:
                    var k = SanitizeText.Away(rng, opt.Variation);
                    if (over.Length > 0)
                    {
                        var mapped = SanitizeText.MapNumbers(over, v => v * k);
                        return textOver ? r with { Text = mapped } : r.WithParam("text", mapped);
                    }
                    return r.WithParam("scale", Geo.Clamp(o.Num("scale", 1) * k, 0.0001, 10000));
                default:
                    return r;
            }
        }

        /// <summary>Snap of dragged offsets and radii (mm) and of dragged text positions (share of the line).</summary>
        public const double LengthSnap = 0.5, ShareSnap = 0.05;

        /// <summary>
        /// A grip of a dimension (<see cref="DragHandle"/>) in page coordinates. Dimensions are point objects, so the allowed
        /// direction is given in page coordinates directly; the constraint names the motion in the dimension's own frame
        /// (<see cref="DragConstraint.Horizontal"/> along the measuring direction or arc, <see cref="DragConstraint.Vertical"/>
        /// across it).
        /// </summary>
        public static DragHandle Grip(string key, Vec2 at, DragConstraint constraint, Vec2 dir, string en, string de, string value)
            => new(key, at, constraint, new I18nText(en + ": " + value, de + ": " + value)) { Direction = dir.Length < Geo.Eps ? Vec2.Zero : dir.Normalized() };

        /// <summary>A length for a grip label ("−8 mm").</summary>
        public static string Mm(double v) => Ann.Fmt(v, 1).Replace("-", "−") + " mm";

        /// <summary>A share for a grip label ("50 %").</summary>
        public static string Pct(double v) => Ann.Fmt(v * 100, 0).Replace("-", "−") + " %";

        /// <summary>Text position along the dimension line (share; outside 0 … 1 the text sits beyond the extension lines).</summary>
        public static ParamDef TextAtParam(double? def) => P.Num("textAt", "Text position along the line", "Textlage längs der Maßlinie", def, -1, 1.5, null, ShareSnap, advanced: true);

        /// <summary>The terminator choice of a dimension (arrows, ticks or dots; one dropdown).</summary>
        public static IEnumerable<ContextAction> TerminatorActions(SketchObject o)
            => Ann.Choice(o, "ann.dim.terminator", new I18nText("Terminators", "Begrenzung"), "terminator", "arrow", "terminator", new (string, I18nText, string?)[]
            {
                ("arrow", new I18nText("Arrows", "Pfeile"), IconIds.HeadTriangle),
                ("tick", new I18nText("Ticks", "Striche"), IconIds.HeadTick),
                ("dot", new I18nText("Dots", "Punkte"), IconIds.HeadDot),
            });
    }

    /// <summary>
    /// Linear (horizontal/vertical) or aligned dimension between two points with an offset dimension line.
    /// The text is the measured length (mm × scale) unless override text is set.
    /// </summary>
    public sealed class LinearDimensionType : AnnPointType
    {
        private readonly bool _aligned;

        public LinearDimensionType(bool aligned)
            : base(aligned ? "ann.dimension.aligned" : "ann.dimension.linear",
                   aligned ? new I18nText("Aligned dimension", "Paralleles Maß") : new I18nText("Linear dimension", "Lineares Maß"),
                   AnnCategories.Dimensions, "dimension", "measure", "bemaßung", "maß", "length", "länge", aligned ? "aligned" : "linear")
        {
            _aligned = aligned;
        }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override SketchObject Sanitize(SketchObject o, SanitizeOptions opt, Random rng) => Dim.Sanitize(this, o, opt, rng);

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef> { P.Num("offset", "Offset", "Abstand", -8, -500, 500) };
                if (!_aligned) list.Add(P.Choice("axis", "Direction", "Richtung", "auto", ("auto", "Automatic", "Automatisch"), ("horizontal", "Horizontal", "Waagerecht"), ("vertical", "Vertical", "Senkrecht")));
                list.AddRange(Dim.Common());
                list.Add(Dim.TextAtParam(0.5));
                return list;
            }
        }

        /// <summary>The dimension line: measuring direction u, its normal n, the feature sides sa/sb, the line position s and its ends qa/qb.</summary>
        private (Vec2 U, Vec2 N, double Sa, double Sb, double S, Vec2 Qa, Vec2 Qb) Layout(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var u = Axis(o, a, b);
            var n = u.Perp; // (-u.y, u.x)
            var offset = o.Num("offset", -8);
            var sa = a.Dot(n); var sb = b.Dot(n);
            var s = (offset < 0 ? Math.Min(sa, sb) : Math.Max(sa, sb)) + offset;
            return (u, n, sa, sb, s, a + n * (s - sa), b + n * (s - sb));
        }

        /// <summary>Text position along the line (share, clamped).</summary>
        private static double TextAt(SketchObject o) => Geo.Clamp(o.Num("textAt", 0.5), -1, 1.5);

        /// <summary>Centre of the text: beside the dimension line at its share, on the side away from the feature.</summary>
        private static Vec2 TextPos(SketchObject o, Vec2 n, Vec2 qa, Vec2 qb, double size)
        {
            var offset = o.Num("offset", -8);
            var side = offset < 0 ? -n : n;
            if (Math.Abs(offset) < Geo.Eps) side = n.Y > 0 ? -n : n;
            return Vec2.Lerp(qa, qb, TextAt(o)) + side * (0.8 + size * 0.45);
        }

        /// <summary>
        /// Grips: "offset" on the dimension line (away from the text; drags the line across the measuring direction) and
        /// "text" on the text (drags it along the line). The measured end points stay the element's point handles.
        /// </summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) yield break;
            var (u, n, _, _, _, qa, qb) = Layout(o, p);
            var t = TextAt(o);
            yield return Dim.Grip("offset", Vec2.Lerp(qa, qb, t <= 0.5 ? 0.75 : 0.25), DragConstraint.Vertical, n,
                "Dimension line offset", "Abstand der Maßlinie", Dim.Mm(o.Num("offset", -8)));
            if (qa.DistanceTo(qb) > Geo.Eps)
                yield return Dim.Grip("text", TextPos(o, n, qa, qb, TextSizeOf(o, Dim.TextSize)), DragConstraint.Horizontal, u,
                    "Dimension text position", "Lage des Maßtexts", Dim.Pct(t));
        }

        /// <summary>
        /// "offset": the line through the pointer (snapped to 0.5 mm; negative offsets count from the lower feature side, so
        /// the line stays outside the feature); "text": the pointer's share along the line (snapped to 5 %).
        /// </summary>
        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) return o;
            var (u, n, sa, sb, _, qa, qb) = Layout(o, p);
            if (key == "offset")
            {
                var s = pagePos.Dot(n);
                double lo = Math.Min(sa, sb), hi = Math.Max(sa, sb);
                var off = s < (lo + hi) / 2
                    ? Math.Min(Dragging.Snap(s - lo, Dim.LengthSnap), -Dim.LengthSnap)
                    : Math.Max(Dragging.Snap(s - hi, Dim.LengthSnap), 0);
                return o.WithParam("offset", Geo.Clamp(off, -500, 500));
            }
            if (key == "text")
            {
                var len = (qb - qa).Dot(u);
                if (Math.Abs(len) < Geo.Eps) return o;
                return o.WithParam("textAt", Geo.Clamp(Dragging.Snap((pagePos - qa).Dot(u) / len, Dim.ShareSnap), -1, 1.5));
            }
            return o;
        }

        /// <summary>Measuring direction (unit vector) for the given points.</summary>
        public Vec2 Axis(SketchObject o, Vec2 a, Vec2 b)
        {
            var d = b - a;
            if (_aligned) return d.Length < Geo.Eps ? new Vec2(1, 0) : d.Normalized();
            return o.Str("axis", "auto") switch
            {
                "horizontal" => new Vec2(1, 0),
                "vertical" => new Vec2(0, 1),
                _ => Math.Abs(d.X) >= Math.Abs(d.Y) ? new Vec2(1, 0) : new Vec2(0, 1),
            };
        }

        /// <summary>Measured length in mm (before the scale factor).</summary>
        public double Measure(SketchObject o, IReadOnlyList<Vec2> p) => Math.Abs((p[p.Count - 1] - p[0]).Dot(Axis(o, p[0], p[p.Count - 1])));

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var (u, n, sa, sb, s, qa, qb) = Layout(o, p);
            var list = new List<Primitive>();
            foreach (var (pt, q, sp) in new[] { (a, qa, sa), (b, qb, sb) })
            {
                var dist = s - sp;
                if (Math.Abs(dist) < Dim.Gap + 0.1) continue;
                var sign = Math.Sign(dist);
                list.Add(new LinePrim(pt + n * (Dim.Gap * sign), q + n * (Dim.Overshoot * sign), Style.ThinLine));
            }
            list.Add(new LinePrim(qa, qb, Dim.LineStyle(o)) { Tag = AnnTags.Main });
            var text = Dim.Label(o, Measure(o, p));
            var size = TextSizeOf(o, Dim.TextSize);
            // Text sits beside the dimension line, on the side away from the feature; readable orientation.
            var pos = TextPos(o, n, qa, qb, size);
            list.Add(Ann.Text(pos, text, size, AnnTags.DimText, rotation: Ann.ReadableAngle(u)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Dim.TerminatorActions(o)) yield return a;
            yield return new ContextAction("ann.dim.flip", new I18nText("Put on other side", "Auf die andere Seite"), IconIds.SideSwap, x => x.WithParam("offset", -x.Num("offset", -8))) { Group = "side" };
        }
    }

    /// <summary>Angular dimension: point 0 = vertex, points 1 and 2 on the legs. Text in degrees.</summary>
    public sealed class AngleDimensionType : AnnPointType
    {
        public AngleDimensionType() : base("ann.dimension.angle", new I18nText("Angle dimension", "Winkelmaß"), AnnCategories.Dimensions, "angle", "winkel", "dimension", "bemaßung", "degree", "grad") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-12, 10), new Vec2(16, 10), new Vec2(4, -12) };
        protected override int MinPoints => 3;

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Num("radius", "Arc radius", "Bogenradius", null, 1, 500) }.Concat(Dim.Common()).ToList();

        /// <summary>Unsigned angle between the legs in degrees (0..180).</summary>
        public static double Measure(IReadOnlyList<Vec2> p)
        {
            var a = p[1] - p[0]; var b = p[2] - p[0];
            return Geo.RadToDeg(Math.Abs(Math.Atan2(a.Cross(b), a.Dot(b))));
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var v = p[0]; var a = p[1] - v; var b = p[2] - v;
            if (a.Length < Geo.Eps || b.Length < Geo.Eps) return Array.Empty<Primitive>();
            var r = Radius(o, a, b);
            var start = Math.Atan2(a.Y, a.X);
            var sweep = Math.Atan2(a.Cross(b), a.Dot(b));
            var list = new List<Primitive>();
            foreach (var leg in new[] { a, b })
                if (r > leg.Length + Dim.Gap) list.Add(new LinePrim(v + leg.Normalized() * (leg.Length + Dim.Gap), v + leg.Normalized() * (r + Dim.Overshoot), Style.ThinLine));
            list.Add(Prim.Arc(v.X, v.Y, r, r, start, sweep, Dim.LineStyle(o)) with { Tag = AnnTags.Main });
            var size = TextSizeOf(o, Dim.TextSize);
            var text = Dim.Label(o, Measure(p), "", "°");
            var mid = start + sweep / 2;
            var dir = Vec2.Polar(1, mid);
            var pos = v + dir * (r + 1 + Ann.HalfExtentAlong(text, size, dir));
            list.Add(Ann.Text(pos, text, size, AnnTags.DimText));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        /// <summary>Share of the sweep where the radius grip sits on the arc (beside the text, which is at the middle).</summary>
        internal const double GripAt = 0.3;

        /// <summary>Arc radius: the parameter, else 0.65 of the shorter leg.</summary>
        private static double Radius(SketchObject o, Vec2 a, Vec2 b) => o.Num("radius", 0.65 * Math.Min(a.Length, b.Length));

        /// <summary>Grip "radius" on the arc, dragged radially (the arc grows or shrinks about the vertex).</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 3) yield break;
            var v = p[0]; var a = p[1] - v; var b = p[2] - v;
            if (a.Length < Geo.Eps || b.Length < Geo.Eps) yield break;
            var r = Radius(o, a, b);
            var dir = Vec2.Polar(1, Math.Atan2(a.Y, a.X) + GripAt * Math.Atan2(a.Cross(b), a.Dot(b)));
            yield return Dim.Grip("radius", v + dir * r, DragConstraint.Vertical, dir, "Arc radius", "Bogenradius", Dim.Mm(r));
        }

        /// <summary>"radius": the pointer's distance from the vertex along the grip's ray (snapped to 0.5 mm, at least 1 mm).</summary>
        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            if (key != "radius") return o;
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 3) return o;
            var v = p[0]; var a = p[1] - v; var b = p[2] - v;
            if (a.Length < Geo.Eps || b.Length < Geo.Eps) return o;
            var dir = Vec2.Polar(1, Math.Atan2(a.Y, a.X) + GripAt * Math.Atan2(a.Cross(b), a.Dot(b)));
            return o.WithParam("radius", Geo.Clamp(Dragging.Snap((pagePos - v).Dot(dir), Dim.LengthSnap), 1, 500));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Dim.TerminatorActions(o);
    }

    /// <summary>Radius or diameter dimension: point 0 = centre, point 1 = point on the circle.</summary>
    public sealed class RadiusDimensionType : AnnPointType
    {
        public RadiusDimensionType() : base("ann.dimension.radius", new I18nText("Radius dimension", "Radiusmaß"), AnnCategories.Dimensions, "radius", "diameter", "durchmesser", "dimension", "bemaßung") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(12, -9) };

        public override SketchObject Sanitize(SketchObject o, SanitizeOptions opt, Random rng) => Dim.Sanitize(this, o, opt, rng);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "radius", ("radius", "Radius", "Radius"), ("diameter", "Diameter", "Durchmesser")),
        }.Concat(Dim.Common()).Concat(new[] { Dim.TextAtParam(null) }).ToList();

        /// <summary>
        /// Text position as a share of the radius from the centre (default: the middle of the radius, a quarter for a
        /// diameter); negative shares lie on the other half of a diameter.
        /// </summary>
        private static double TextAt(SketchObject o, bool dia) => Geo.Clamp(o.Num("textAt", dia ? 0.25 : 0.5), dia ? -1 : 0, 1.5);

        private static bool IsDiameter(SketchObject o) => o.Str("kind", "radius") == "diameter";

        /// <summary>Centre of the text beside the line at its share (above the line).</summary>
        private Vec2 TextPos(SketchObject o, Vec2 c, Vec2 e, double size)
        {
            var u = (e - c).Normalized();
            var n = u.Perp; if (n.Y > 0) n = -n;
            return Vec2.Lerp(c, e, TextAt(o, IsDiameter(o))) + n * (0.8 + size * 0.45);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var c = p[0]; var e = p[p.Count - 1];
            var r = c.DistanceTo(e);
            if (r < Geo.Eps) return Array.Empty<Primitive>();
            var dia = IsDiameter(o);
            var u = (e - c) / r;
            var start = dia ? c - u * r : c;
            var list = new List<Primitive> { new LinePrim(start, e, Dim.LineStyle(o, start: dia)) { Tag = AnnTags.Main } };
            var size = TextSizeOf(o, Dim.TextSize);
            var text = Dim.Label(o, dia ? 2 * r : r, dia ? "Ø" : "R");
            list.Add(Ann.Text(TextPos(o, c, e, size), text, size, AnnTags.DimText, rotation: Ann.ReadableAngle(u)));
            return list;
        }

        /// <summary>Grip "text" on the dimension text, dragged along the radius or diameter line.</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) yield break;
            var c = p[0]; var e = p[p.Count - 1];
            if (c.DistanceTo(e) < Geo.Eps) yield break;
            yield return Dim.Grip("text", TextPos(o, c, e, TextSizeOf(o, Dim.TextSize)), DragConstraint.Horizontal, e - c,
                "Dimension text position", "Lage des Maßtexts", Dim.Pct(TextAt(o, IsDiameter(o))));
        }

        /// <summary>"text": the pointer's share of the radius along the line (snapped to 5 %).</summary>
        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            if (key != "text") return o;
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) return o;
            var c = p[0]; var e = p[p.Count - 1];
            var r = c.DistanceTo(e);
            if (r < Geo.Eps) return o;
            var dia = IsDiameter(o);
            var t = (pagePos - c).Dot((e - c) / r) / r;
            return o.WithParam("textAt", Geo.Clamp(Dragging.Snap(t, Dim.ShareSnap), dia ? -1 : 0, 1.5));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.dim.kind", new I18nText("Dimension", "Maßart"), "kind", "radius", "kind", new (string, I18nText, string?)[]
            {
                ("radius", new I18nText("Radius", "Radius"), null),
                ("diameter", new I18nText("Diameter", "Durchmesser"), null),
            }, KindPreview)) yield return a;
            foreach (var a in Dim.TerminatorActions(o)) yield return a;
        }

        /// <summary>Option picture of the dimension kind: a circle with its radius or its diameter.</summary>
        private static IReadOnlyList<Primitive> KindPreview(string kind)
        {
            var s = Style.Visible with { StrokeWidth = 0.4 };
            var arrow = s with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            var circle = Prim.Circle(0, 0, 5, Style.ThinLine with { StrokeWidth = 0.3 });
            return kind == "diameter"
                ? new Primitive[] { circle, new LinePrim(new Vec2(-3.5, 3.5), new Vec2(3.5, -3.5), arrow with { ArrowStart = ArrowHead.Triangle }) }
                : new Primitive[] { circle, Prim.Circle(0, 0, 0.4, s with { Fill = Fill.Solid("#000000") }), new LinePrim(Vec2.Zero, new Vec2(3.5, -3.5), arrow) };
        }
    }

    /// <summary>
    /// Chain dimension (<c>ann.dimension.chain</c>): several points measured one after the other along one dimension line
    /// (horizontal or vertical, automatic by default), each stretch with its own value. The points are taken in their order
    /// along the measuring direction.
    /// </summary>
    public sealed class ChainDimensionType : AnnPointType
    {
        public ChainDimensionType() : base("ann.dimension.chain", new I18nText("Chain dimension", "Kettenmaß"), AnnCategories.Dimensions,
            "chain dimension", "dimension", "series", "continuous", "kettenmaß", "kettenbemaßung", "bemaßung", "maßkette")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(0, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>
                {
                    P.Num("offset", "Offset", "Abstand", -8, -500, 500),
                    P.Choice("axis", "Direction", "Richtung", "auto", ("auto", "Automatic", "Automatisch"), ("horizontal", "Horizontal", "Waagerecht"), ("vertical", "Vertical", "Senkrecht")),
                };
                // One value per stretch: no common override text.
                list.AddRange(Dim.Common().Where(d => d.Key != "text"));
                return list;
            }
        }

        /// <summary>Measuring direction: horizontal or vertical (automatic: along the larger extent of the points).</summary>
        public static Vec2 Axis(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var b = Rect.FromPoints(p);
            return o.Str("axis", "auto") switch
            {
                "horizontal" => new Vec2(1, 0),
                "vertical" => new Vec2(0, 1),
                _ => b.W >= b.H ? new Vec2(1, 0) : new Vec2(0, 1),
            };
        }

        /// <summary>The stretch lengths in mm (before the scale factor), in order along the measuring direction.</summary>
        public static IReadOnlyList<double> Measure(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var u = Axis(o, p);
            var s = p.Select(x => x.Dot(u)).OrderBy(x => x).ToList();
            return Enumerable.Range(0, Math.Max(0, s.Count - 1)).Select(i => s[i + 1] - s[i]).ToList();
        }

        private static string Label(SketchObject o, double measured)
        {
            var unit = o.Str("unit", "");
            return o.Str("prefix", "") + Ann.Fmt(measured * o.Num("scale", 1), o.Int("decimals", 1)) + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var u = Axis(o, p);
            var n = u.Perp;
            var offset = o.Num("offset", -8);
            var sides = p.Select(x => x.Dot(n)).ToList();
            var line = (offset < 0 ? sides.Min() : sides.Max()) + offset;
            var ordered = p.OrderBy(x => x.Dot(u)).ToList();
            var size = TextSizeOf(o, Dim.TextSize);
            var textSide = offset < 0 ? -n : n;
            if (Math.Abs(offset) < Geo.Eps) textSide = n.Y > 0 ? -n : n;
            var list = new List<Primitive>();
            var feet = new List<Vec2>();
            foreach (var pt in ordered)
            {
                var sp = pt.Dot(n);
                var q = pt + n * (line - sp);
                feet.Add(q);
                var dist = line - sp;
                if (Math.Abs(dist) < Dim.Gap + 0.1) continue;
                var sign = Math.Sign(dist);
                list.Add(new LinePrim(pt + n * (Dim.Gap * sign), q + n * (Dim.Overshoot * sign), Style.ThinLine));
            }
            for (var i = 0; i + 1 < feet.Count; i++)
            {
                var len = (feet[i + 1] - feet[i]).Dot(u);
                if (len < Geo.Eps) continue;
                list.Add(new LinePrim(feet[i], feet[i + 1], Dim.LineStyle(o)) { Tag = AnnTags.Main });
                list.Add(Ann.Text(Vec2.Lerp(feet[i], feet[i + 1], 0.5) + textSide * (0.8 + size * 0.45), Label(o, len), size, AnnTags.DimText, rotation: Ann.ReadableAngle(u)));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Dim.TerminatorActions(o)) yield return a;
            yield return new ContextAction("ann.dim.flip", new I18nText("Put on other side", "Auf die andere Seite"), IconIds.SideSwap, x => x.WithParam("offset", -x.Num("offset", -8))) { Group = "side" };
        }
    }
}
