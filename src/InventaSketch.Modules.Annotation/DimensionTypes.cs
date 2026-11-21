using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Shared dimension parameters and helpers (ISO 129-1 inspired; patents normally avoid dimensions).</summary>
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

        public static IEnumerable<ContextAction> TerminatorActions(SketchObject o)
        {
            yield return Ann.SetChoice(o, "ann.dim.arrow", new I18nText("Arrow terminators", "Pfeile als Begrenzung"), "dim-arrow", "terminator", "arrow", "arrow", "terminator");
            yield return Ann.SetChoice(o, "ann.dim.tick", new I18nText("Tick terminators", "Schrägstriche als Begrenzung"), "dim-tick", "terminator", "tick", "arrow", "terminator");
            yield return Ann.SetChoice(o, "ann.dim.dot", new I18nText("Dot terminators", "Punkte als Begrenzung"), "dim-dot", "terminator", "dot", "arrow", "terminator");
        }
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

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef> { P.Num("offset", "Offset", "Abstand", -8, -500, 500) };
                if (!_aligned) list.Add(P.Choice("axis", "Direction", "Richtung", "auto", ("auto", "Automatic", "Automatisch"), ("horizontal", "Horizontal", "Waagerecht"), ("vertical", "Vertical", "Senkrecht")));
                list.AddRange(Dim.Common());
                return list;
            }
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
            var u = Axis(o, a, b);
            var n = u.Perp; // (-u.y, u.x)
            var offset = o.Num("offset", -8);
            var sa = a.Dot(n); var sb = b.Dot(n);
            var s = (offset < 0 ? Math.Min(sa, sb) : Math.Max(sa, sb)) + offset;
            var qa = a + n * (s - sa);
            var qb = b + n * (s - sb);
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
            var side = offset < 0 ? -n : n;
            if (Math.Abs(offset) < Geo.Eps) side = n.Y > 0 ? -n : n;
            // Text sits beside the dimension line, on the side away from the feature; readable orientation.
            var pos = Vec2.Lerp(qa, qb, 0.5) + side * (0.8 + size * 0.45);
            list.Add(Ann.Text(pos, text, size, AnnTags.DimText, rotation: Ann.ReadableAngle(u)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Dim.TerminatorActions(o)) yield return a;
            yield return new ContextAction("ann.dim.flip", new I18nText("Flip side", "Seite wechseln"), "flip", x => x.WithParam("offset", -x.Num("offset", -8))) { Group = "side" };
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
            var r = o.Num("radius", 0.65 * Math.Min(a.Length, b.Length));
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

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Dim.TerminatorActions(o);
    }

    /// <summary>Radius or diameter dimension: point 0 = centre, point 1 = point on the circle.</summary>
    public sealed class RadiusDimensionType : AnnPointType
    {
        public RadiusDimensionType() : base("ann.dimension.radius", new I18nText("Radius dimension", "Radiusmaß"), AnnCategories.Dimensions, "radius", "diameter", "durchmesser", "dimension", "bemaßung") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(12, -9) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "radius", ("radius", "Radius", "Radius"), ("diameter", "Diameter", "Durchmesser")),
        }.Concat(Dim.Common()).ToList();

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var c = p[0]; var e = p[p.Count - 1];
            var r = c.DistanceTo(e);
            if (r < Geo.Eps) return Array.Empty<Primitive>();
            var dia = o.Str("kind", "radius") == "diameter";
            var u = (e - c) / r;
            var start = dia ? c - u * r : c;
            var list = new List<Primitive> { new LinePrim(start, e, Dim.LineStyle(o, start: dia)) { Tag = AnnTags.Main } };
            var size = TextSizeOf(o, Dim.TextSize);
            var text = Dim.Label(o, dia ? 2 * r : r, dia ? "Ø" : "R");
            var n = u.Perp; if (n.Y > 0) n = -n;
            var pos = Vec2.Lerp(c, e, dia ? 0.25 : 0.5) + n * (0.8 + size * 0.45);
            list.Add(Ann.Text(pos, text, size, AnnTags.DimText, rotation: Ann.ReadableAngle(u)));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.SetChoice(o, "ann.dim.radius", new I18nText("Radius", "Radius"), "dim-radius", "kind", "radius", "radius", "kind");
            yield return Ann.SetChoice(o, "ann.dim.diameter", new I18nText("Diameter", "Durchmesser"), "dim-diameter", "kind", "diameter", "radius", "kind");
            foreach (var a in Dim.TerminatorActions(o)) yield return a;
        }
    }
}
