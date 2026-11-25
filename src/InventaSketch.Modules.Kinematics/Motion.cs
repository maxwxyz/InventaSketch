using System;
using System.Collections.Generic;
using System.Globalization;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Angle-limit annotation for a joint: rays at the minimum and maximum angle, an arc between them with tick marks
    /// and labels. Angles in degrees from the local x axis, clockwise on screen positive.
    /// </summary>
    internal sealed class AngleLimitType : ObjectType
    {
        public override string Id => "kin.motion.anglelimit";
        public override I18nText Name => new("Angle limits", "Winkelbegrenzung");
        public override string Category => KinCategories.Motion;
        public override IReadOnlyList<string> Keywords => new[] { "angle limit", "range of motion", "stop", "swing angle", "winkelbegrenzung", "schwenkwinkel", "anschlag", "bewegungsbereich", "winkel" };
        public override Size2 DefaultSize => new(34, 34);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Num("min", "Minimum angle", "Minimaler Winkel", -30, -360, 360, "°", 5),
            KP.Num("max", "Maximum angle", "Maximaler Winkel", 45, -360, 360, "°", 5),
            KP.Choice("labels", "Labels", "Beschriftung", "values", ("values", "Angle values", "Winkelwerte"), ("symbols", "Symbols (φmin, φmax)", "Symbole (φmin, φmax)"), ("none", "None", "Keine")),
            KP.Bool("joint", "Joint circle", "Gelenkkreis", true),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var min = o.Num("min", -30); var max = o.Num("max", 45);
            if (max < min) (min, max) = (max, min);
            if (max - min > 360) max = min + 360;
            var list = new List<Primitive>();
            Vec2 E(double deg, double f) => new(Math.Cos(Geo.DegToRad(deg)) * hw * f, Math.Sin(Geo.DegToRad(deg)) * hh * f);
            // zero reference (centre line) and limit rays
            list.Add(Prim.Line(Vec2.Zero, E(0, 0.96), KS.Ctr) with { Tag = "reference" });
            list.Add(Prim.Line(Vec2.Zero, E(min, 0.75), KS.Thin) with { Tag = "limit" });
            list.Add(Prim.Line(Vec2.Zero, E(max, 0.75), KS.Thin) with { Tag = "limit" });
            // arc with ticks at the limits
            list.Add(Prim.Arc(0, 0, hw * 0.55, hh * 0.55, Geo.DegToRad(min), Geo.DegToRad(max - min), KS.Thin with { StrokeWidth = 0.35 }) with { Tag = KinTags.Arc });
            foreach (var a in new[] { min, max })
                list.Add(Prim.Line(E(a, 0.47), E(a, 0.63), KS.Link with { StrokeWidth = 0.35 }) with { Tag = "tick" });
            var labels = o.Str("labels", "values");
            if (labels != "none")
            {
                var size = Geo.Clamp(0.1 * Math.Min(o.Size.W, o.Size.H), 1.2, 3.5);
                string Label(double v, string sym) => labels == "symbols" ? sym : Frag.Deg(v);
                list.Add(Frag.FitText(E(min, 0.9), Label(min, "φmin"), size, hw, hh, KinTags.Label, italic: labels == "symbols"));
                list.Add(Frag.FitText(E(max, 0.9), Label(max, "φmax"), size, hw, hh, KinTags.Label, italic: labels == "symbols"));
            }
            if (o.Flag("joint", true))
            {
                var r = Geo.Clamp(0.05 * Math.Min(o.Size.W, o.Size.H), 0.5, 2);
                list.Add(Prim.Circle(0, 0, r, KS.Joint) with { Tag = KinTags.Joint });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "c");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var cur = o.Str("labels", "values");
            var next = cur == "values" ? "symbols" : cur == "symbols" ? "none" : "values";
            yield return new ContextAction("kin.labels", new I18nText("Next label style", "Nächste Beschriftungsart"), IconIds.Cycle, x => x.WithParam("labels", next)) { Group = "param" };
            yield return KA.Toggle(o, "kin.limit.joint", new I18nText("Joint circle", "Gelenkkreis"), IconIds.Toggle, "joint", true, "param");
        }
    }

    /// <summary>Degree-of-freedom badge: Grübler/Kutzbach mobility F = 3(n − 1) − 2·j1 − j2 as text in a frame.</summary>
    internal sealed class DofBadgeType : ObjectType
    {
        public override string Id => "kin.motion.dof";
        public override I18nText Name => new("Degree-of-freedom badge", "Freiheitsgrad-Anzeige");
        public override string Category => KinCategories.Motion;
        public override IReadOnlyList<string> Keywords => new[] { "degree of freedom", "dof", "mobility", "grübler", "gruebler", "kutzbach", "freiheitsgrad", "laufgrad", "getriebefreiheitsgrad" };
        public override Size2 DefaultSize => new(24, 9);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Int("links", "Links n (incl. frame)", "Glieder n (mit Gestell)", 4, 1, 99),
            KP.Int("lower", "Lower pairs j1 (1 DOF joints)", "Gelenke j1 (Freiheitsgrad 1)", 4, 0, 99),
            KP.Int("higher", "Higher pairs j2 (2 DOF joints)", "Gelenke j2 (Freiheitsgrad 2)", 0, 0, 99),
            KP.Bool("formula", "Show formula", "Formel zeigen", false),
        };

        public static int Mobility(SketchObject o) => Linkages.Grubler(o.Int("links", 4), o.Int("lower", 4), o.Int("higher", 0));

        public static string Text(SketchObject o, Lang lang)
        {
            var f = Mobility(o).ToString(CultureInfo.InvariantCulture);
            var head = lang == Lang.De ? "F" : "DOF";
            if (!o.Flag("formula", false)) return head + " = " + f;
            string I(string k, int d) => o.Int(k, d).ToString(CultureInfo.InvariantCulture);
            return $"{head} = 3({I("links", 4)}−1) − 2·{I("lower", 4)} − {I("higher", 0)} = {f}".Replace("-", "−");
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var text = Text(o, ctx.Lang);
            var size = Math.Min(3.5, hh * 0.9);
            return new Primitive[]
            {
                Prim.Rect(-hw, -hh, 2 * hw, 2 * hh, KS.Thin with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") }) with { Tag = KinTags.Dof },
                Frag.FitText(Vec2.Zero, text, size, hw * 0.92, hh * 0.85, KinTags.Dof),
            };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return KA.Toggle(o, "kin.dof.formula", new I18nText("Show formula", "Formel zeigen"), IconIds.Formula, "formula", false, "param");
        }
    }
}
