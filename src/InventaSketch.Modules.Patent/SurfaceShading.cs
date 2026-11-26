using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;

namespace InventaSketch.Modules.Patent
{
    /// <summary>
    /// Surface shading laid over a part: thin, spaced black lines that show the form of a surface instead of grey tones
    /// (37 CFR 1.84(m); EP Guidelines A-IX 7.2 – rows US-15 and EP-20 of the drawing-rules research). Light falls from
    /// the upper left, so the lines gather on the lower right: parallel lines closing up towards the edge of a cylinder,
    /// lines fanning from the apex of a cone, concentric arcs on a sphere, evenly spaced lines on a flat face. Box
    /// object: scale and rotate it onto the surface; mirror it for light from the other side. Not a part.
    /// </summary>
    public sealed class ShadingType : AnnBoxType
    {
        public const double DefaultLines = 5;

        public ShadingType() : base(PatentIds.Shading, new I18nText("Surface shading", "Oberflächenschattierung"), PatentIds.Category,
            "shading", "shade", "surface", "cylinder", "sphere", "cone", "schattierung", "oberfläche", "zylinder", "kugel", "kegel", "hatch", "schraffur") { }

        public override Size2 DefaultSize => new(30, 30);
        protected override bool EdgePorts => false;
        public override bool IsPart => false;

        private static readonly (string Value, I18nText Label)[] Forms =
        {
            ("cylinder", new I18nText("Cylinder", "Zylinder")),
            ("cone", new I18nText("Cone", "Kegel")),
            ("sphere", new I18nText("Sphere", "Kugel")),
            ("flat", new I18nText("Flat face", "Ebene Fläche")),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            new ParamDef("form", new I18nText("Surface", "Fläche"), ParamKind.Choice)
            {
                Default = "cylinder",
                Options = Forms.Select(f => new ParamOption(f.Value, f.Label)).ToList(),
            },
            new ParamDef("lines", new I18nText("Lines", "Linien"), ParamKind.Number) { Default = DefaultLines, Min = 2, Max = 20, Step = 1, Unit = null },
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var n = Math.Max(2, Math.Min(20, o.Int("lines", (int)DefaultLines)));
            var style = Style.ThinLine;
            var list = new List<Primitive>();
            // Positions from the shaded edge inwards, closing up towards the edge (quadratic spacing).
            double Gather(int i) => 0.45 * ((double)i / n) * ((double)i / n);
            switch (o.Str("form", "cylinder"))
            {
                case "cone":
                    for (var i = 1; i <= n; i++)
                    {
                        var x = w / 2 - w * Gather(i);
                        list.Add(Prim.Line(new Vec2(0, -h / 2), new Vec2(x, h / 2), style));
                    }
                    break;
                case "sphere":
                    for (var i = 1; i <= n; i++)
                    {
                        var k = 1 - Gather(i) * 0.9;
                        list.Add(Prim.Arc(0, 0, w / 2 * k, h / 2 * k, -0.35, Math.PI / 2 + 0.7, style));
                    }
                    break;
                case "flat":
                    for (var i = 1; i <= n; i++)
                    {
                        var x = -w / 2 + w * i / (n + 1);
                        list.Add(Prim.Line(new Vec2(x, -h / 2), new Vec2(x, h / 2), style));
                    }
                    break;
                default:
                    for (var i = 1; i <= n; i++)
                    {
                        var x = w / 2 - w * Gather(i);
                        list.Add(Prim.Line(new Vec2(x, -h / 2), new Vec2(x, h / 2), style));
                    }
                    break;
            }
            return list.Select(p => p with { Tag = AnnTags.Hatch }).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "patent.shading.form", Parameters[0], null, "shading", null)) yield return a;
            yield return new ContextAction("patent.shading.more", new I18nText("More lines", "Mehr Linien"), IconIds.CountMore,
                x => x.WithParam("lines", Math.Min(20, x.Int("lines", (int)DefaultLines) + 1))) { Group = "shading", IsEnabled = o.Int("lines", (int)DefaultLines) < 20, ParamKeys = new[] { "lines" } };
            yield return new ContextAction("patent.shading.fewer", new I18nText("Fewer lines", "Weniger Linien"), IconIds.CountFewer,
                x => x.WithParam("lines", Math.Max(2, x.Int("lines", (int)DefaultLines) - 1))) { Group = "shading", IsEnabled = o.Int("lines", (int)DefaultLines) > 2, ParamKeys = new[] { "lines" } };
        }
    }
}
