using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Angle between two rays: points (point on the first ray, vertex, point on the second ray). Draws the rays (optional),
    /// an arc, a double arc or the right-angle square, and the name and/or the measured value. The value is measured
    /// from the points, so it is always true to the drawing.
    /// </summary>
    internal sealed class AngleMarkType : MathPointType
    {
        public AngleMarkType() : base("math.angle.mark", new I18nText("Angle", "Winkel"), MathCategories.Angles,
            "angle", "arc", "alpha", "right angle", "winkel", "winkelbogen", "rechter winkel", "winkelmaß", "grad", "degree") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(18, 8), new Vec2(-12, 8), new Vec2(-12 + 30 * Math.Cos(Math.PI / 4), 8 - 30 * Math.Sin(Math.PI / 4)) };
        protected override int MinPoints => 3;

        public static readonly ParamDef MarkDef = Pm.Choice("mark", "Mark", "Markierung", "auto",
            ("auto", "Automatic (square at 90°)", "Automatisch (Quadrat bei 90°)"), ("arc", "Arc", "Bogen"), ("double", "Double arc", "Doppelbogen"), ("right", "Right-angle square", "Rechter-Winkel-Quadrat"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Text("label", "Name", "Name", "α"),
            MarkDef,
            Pm.Bool("value", "Show the measured value", "Gemessenen Wert anzeigen", false),
            Pm.Bool("rays", "Draw the rays", "Schenkel zeichnen", true),
            Pm.Bool("reflex", "Other side (reflex angle)", "Andere Seite (überstumpfer Winkel)", false),
            Pm.Num("radius", "Arc radius", "Bogenradius", 7, 1, 100, "mm", 0.5),
            Pm.Num("textSize", "Text height", "Schrifthöhe", Mx.LabelSize, 1, 20, "mm", 0.5),
        };

        /// <summary>Start angle and signed sweep (screen radians) of the marked angle, and its size in degrees.</summary>
        internal static (double Start, double Sweep, double Degrees) Measure(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var u1 = (p[0] - p[1]).Normalized();
            var u2 = (p[2] - p[1]).Normalized();
            var (start, sweep) = Mx.SmallSweep(u1, u2);
            if (o.Flag("reflex", false)) sweep = sweep > 0 ? sweep - 2 * Math.PI : sweep + 2 * Math.PI;
            return (start, sweep, Math.Abs(Geo.RadToDeg(sweep)));
        }

        /// <summary>Displayed text: "α", "α = 45°" or "45°".</summary>
        internal static string Caption(string label, bool value, double degrees, Lang lang)
        {
            if (!value) return label;
            var v = Mx.Num(degrees, lang, 1) + "°";
            return label.Length > 0 ? label + " = " + v : v;
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            Vec2 a = p[0], v = p[1], b = p[2];
            if (a.DistanceTo(v) < 1e-6 || b.DistanceTo(v) < 1e-6) { f.Dot(v); return; }
            if (o.Flag("rays", true))
            {
                f.Line(v, a, Style.Visible, MathTags.Main);
                f.Line(v, b, Style.Visible, MathTags.Main);
            }
            var (start, sweep, deg) = Measure(o, p);
            var r = o.Num("radius", 7);
            var mark = o.Str("mark", "auto");
            var right = !o.Flag("reflex", false) && (mark == "right" || mark == "auto" && Math.Abs(deg - 90) < 0.05);
            var u1 = (a - v).Normalized();
            var u2 = (b - v).Normalized();
            if (right) Mx.RightAngle(f, v, u1, u2, r * 0.6);
            else if (Math.Abs(sweep) > 1e-6)
            {
                f.Arc(v, r, start, sweep, Mx.Mark, MathTags.Mark);
                if (mark == "double") f.Arc(v, Math.Max(0.5, r - 1.0), start, sweep, Mx.Mark, MathTags.Mark);
            }
            var text = Caption(o.Str("label", "α"), o.Flag("value", false), deg, lang);
            if (text.Length > 0)
            {
                var ts = o.Num("textSize", Mx.LabelSize);
                var mid = start + sweep / 2;
                var dir = new Vec2(Math.Cos(mid), Math.Sin(mid));
                var reach = right ? r * 0.6 * Math.Sqrt(2) : r;
                f.Text(Mx.Beside(v + dir * reach, dir, text, ts, 0.8), text, ts, italic: o.Flag("value", false) == false);
            }
            f.Port("vertex", v);
        }

        /// <summary>The object with the second ray turned about the vertex so that the angle is <paramref name="degrees"/>.</summary>
        internal static SketchObject SetAngle(SketchObject o, double degrees)
        {
            if (o.Points is not { Count: >= 3 } p) return o;
            var reflex = degrees > 180;
            var o2 = o.WithParam("reflex", reflex);
            var u1 = (p[0] - p[1]).Normalized();
            var u2 = (p[2] - p[1]).Normalized();
            // Keep the second ray on its side of the first one (the side of the smaller angle).
            var (_, small0) = Mx.SmallSweep(u1, u2);
            var sign = small0 >= 0 ? 1 : -1;
            var target = Geo.DegToRad(Geo.Clamp(degrees, 0.1, 359.9));
            var small = reflex ? 2 * Math.PI - target : target;
            var dist = p[2].DistanceTo(p[1]);
            if (dist < 1e-6) dist = p[0].DistanceTo(p[1]);
            var a1 = Math.Atan2(u1.Y, u1.X);
            var nb = p[1] + new Vec2(Math.Cos(a1 + sign * small), Math.Sin(a1 + sign * small)) * dist;
            var pts = new List<Vec2>(p);
            pts[2] = nb;
            var att = o.Attach is null ? null : new List<Attachment?>(o.Attach);
            if (att is not null && att.Count > 2) att[2] = null;
            return o2 with { Points = pts, Attach = att };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var deg = p.Count >= 3 ? Measure(o, p).Degrees : 0;
            var label = new I18nText("Set angle…", "Winkel festlegen…");
            yield return ContextAction.WithInput("math.angle.set", label, IconIds.QtyAngle, new I18nText("Angle in degrees", "Winkel in Grad"),
                Mx.Num(deg, Lang.En, 2), (x, t) =>
                {
                    var val = Mx.ParseNumber(t);
                    if (val <= 0 || val >= 360) throw new FormatException("Angle must lie between 0° and 360°.");
                    return SetAngle(x, val);
                }, "°") with { Group = "angle" };
            yield return Mx.Toggle(o, "math.angle.value", new I18nText("Show value", "Wert anzeigen"), "value", false, "angle");
            foreach (var opt in Mx.Choice(o, "math.angle.mark", new I18nText("Mark", "Markierung"), MarkDef, "angle")) yield return opt;
            yield return Mx.Toggle(o, "math.angle.rays", new I18nText("Draw the rays", "Schenkel zeichnen"), "rays", true, "angle");
            yield return Mx.Toggle(o, "math.angle.reflex", new I18nText("Other side", "Andere Seite"), "reflex", false, "angle");
            yield return Mx.TextInput(o, "math.angle.label", new I18nText("Name…", "Name…"), "label", "α", "label");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Actions(o, new RenderContext(SketchDocument.Create(), lang));
    }
}
