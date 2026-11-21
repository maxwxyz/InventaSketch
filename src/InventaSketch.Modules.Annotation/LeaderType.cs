using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Course of a lead line.</summary>
    public enum LeaderCurve { Straight, Curved, Wavy }

    /// <summary>Marker at the object end of a lead line.</summary>
    public enum LeaderEnd { None, Arrow, Dot }

    /// <summary>
    /// Geometry of reference-numeral lead lines, shared by <c>ann.leader</c> and the patent module's
    /// <c>patent.numeral</c>. Point A touches the part, point B is the numeral position (text centre).
    /// The line stops short of the numeral so it never runs into the text (EP GL A-IX 7.5.1; 37 CFR 1.84(q)).
    /// </summary>
    public static class LeaderGeometry
    {
        /// <summary>Gap between line end and numeral text in mm.</summary>
        public const double TextGap = 1.2;

        public static LeaderCurve ParseCurve(string? s) => s?.ToLowerInvariant() switch
        {
            "curved" => LeaderCurve.Curved,
            "wavy" => LeaderCurve.Wavy,
            _ => LeaderCurve.Straight,
        };

        public static LeaderEnd ParseEnd(string? s) => s?.ToLowerInvariant() switch
        {
            "arrow" => LeaderEnd.Arrow,
            "dot" => LeaderEnd.Dot,
            _ => LeaderEnd.None,
        };

        /// <summary>The lead line itself (no text). <paramref name="bend"/> flips the bulge side of curved/wavy lines.</summary>
        public static Primitive Line(Vec2 a, Vec2 b, LeaderCurve curve, LeaderEnd end, bool bend = false, double width = 0.25)
        {
            var style = Style.ThinLine with
            {
                StrokeWidth = width,
                ArrowStart = end switch { LeaderEnd.Arrow => ArrowHead.Triangle, LeaderEnd.Dot => ArrowHead.Oval, _ => ArrowHead.None },
            };
            var d = b - a;
            var len = d.Length;
            if (curve == LeaderCurve.Straight || len < 3) return new LinePrim(a, b, style) { Tag = AnnTags.Lead };
            var n = d.Perp.Normalized() * (bend ? -1 : 1);
            if (curve == LeaderCurve.Curved)
            {
                var k = len * 0.22;
                return Ann.Cubic(a, a + d / 3 + n * k, a + d * (2.0 / 3) + n * k, b, style, AnnTags.Lead);
            }
            // Wavy "S" curve, the common patent-drawing style that separates lead lines from object edges.
            var w = len * 0.28;
            return Ann.Cubic(a, a + d / 3 + n * w, a + d * (2.0 / 3) - n * w, b, style, AnnTags.Lead);
        }

        /// <summary>
        /// Builds a complete leader: line from <paramref name="objectEnd"/> towards <paramref name="textCenter"/>
        /// ending <see cref="TextGap"/> before the numeral, plus the numeral text. Underlined numerals have no line
        /// (surface reference, 37 CFR 1.84(q)).
        /// </summary>
        public static List<Primitive> Build(Vec2 objectEnd, Vec2 textCenter, string text, double textSize, LeaderCurve curve, LeaderEnd end,
            bool bend = false, bool underline = false, string textTag = AnnTags.Numeral)
        {
            var list = new List<Primitive>();
            var dir = textCenter - objectEnd;
            var len = dir.Length;
            var hasText = !string.IsNullOrEmpty(text);
            if (!underline && len > Geo.Eps)
            {
                var u = dir / len;
                // Curved and wavy lines arrive at an angle to the chord, so keep clear of the whole text box
                // (half diagonal) instead of its extent along the chord.
                var extent = Ann.TextExtent(text, textSize);
                var clearance = curve == LeaderCurve.Straight
                    ? Ann.HalfExtentAlong(text, textSize, u)
                    : Math.Sqrt(extent.X * extent.X + extent.Y * extent.Y) / 2 + TextGap;
                var cut = hasText ? clearance + TextGap : 0;
                if (len - cut > 0.5) list.Add(Line(objectEnd, textCenter - u * cut, curve, end, bend));
            }
            if (hasText)
            {
                var numeral = Ann.Text(textCenter, text, textSize, textTag);
                // Underlined numeral (no lead line) as a font attribute, placed by the real font on every host.
                if (underline) numeral = numeral with { TextStyle = numeral.TextStyle with { Underline = true } };
                list.Add(numeral);
            }
            return list;
        }
    }

    /// <summary>
    /// Lead line for reference numerals: thin, straight/curved/wavy, numeral text at the free end, no arrowhead
    /// by default (optional arrow or dot at the object end). Point 0 touches the part, point 1 is the numeral.
    /// </summary>
    public sealed class LeaderType : AnnPointType
    {
        public LeaderType() : base("ann.leader", new I18nText("Leader with numeral", "Bezugslinie mit Bezugszeichen"), AnnCategories.PatentMarks,
            "leader", "lead line", "reference", "numeral", "bezugszeichen", "bezugslinie", "hinweislinie") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(14, -10) };
        public override bool HasText => true;

        public static IReadOnlyList<ParamDef> LeaderParams(string defaultCurve) => new[]
        {
            P.Choice("curve", "Line course", "Linienverlauf", defaultCurve, ("straight", "Straight", "Gerade"), ("curved", "Curved", "Gebogen"), ("wavy", "Wavy (S-curve)", "Wellig (S-Kurve)"))
                .WithOptionIcons(v => v switch { "curved" => IconIds.CourseCurved, "wavy" => IconIds.CourseWavy, _ => IconIds.CourseStraight }),
            P.Choice("end", "Object end", "Objektende", "none", ("none", "Plain", "Ohne"), ("arrow", "Arrow", "Pfeil"), ("dot", "Dot", "Punkt")).WithOptionIcons(IconIds.ForHead),
            P.Bool("bend", "Bend to other side", "Zur anderen Seite biegen", false),
            P.TextSize(Ann.TextSize),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Text("numeral", "Numeral", "Bezugszeichen", "10") }.Concat(LeaderParams("straight")).ToList();

        public static string NumeralOf(SketchObject o) => !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("numeral", o.Ref ?? "10");

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => LeaderGeometry.Build(p[0], p[p.Count - 1], NumeralOf(o), TextSizeOf(o),
                LeaderGeometry.ParseCurve(o.Str("curve", "straight")), LeaderGeometry.ParseEnd(o.Str("end", "none")), o.Flag("bend", false));

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public static IEnumerable<ContextAction> LeaderActions(SketchObject o, string defaultCurve)
        {
            yield return Ann.SetChoice(o, "ann.curve.straight", new I18nText("Straight lead line", "Gerade Bezugslinie"), "line-straight", "curve", "straight", defaultCurve, "curve");
            yield return Ann.SetChoice(o, "ann.curve.curved", new I18nText("Curved lead line", "Gebogene Bezugslinie"), "line-curved", "curve", "curved", defaultCurve, "curve");
            yield return Ann.SetChoice(o, "ann.curve.wavy", new I18nText("Wavy lead line", "Wellige Bezugslinie"), "line-wavy", "curve", "wavy", defaultCurve, "curve");
            yield return Ann.Toggle(o, "ann.curve.bend", new I18nText("Bend to other side", "Zur anderen Seite biegen"), "flip", "bend", false, "curve");
            yield return Ann.SetChoice(o, "ann.end.none", new I18nText("Plain end", "Ende ohne Markierung"), "end-none", "end", "none", "none", "end");
            yield return Ann.SetChoice(o, "ann.end.arrow", new I18nText("Arrow at object", "Pfeil am Objekt"), "end-arrow", "end", "arrow", "none", "end");
            yield return Ann.SetChoice(o, "ann.end.dot", new I18nText("Dot at object", "Punkt am Objekt"), "end-dot", "end", "dot", "none", "end");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => LeaderActions(o, "straight");
    }
}
