using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Hatch pattern presets (section hatching per EP GL A-IX 7.3.2, PCT Guide 5.149, 37 CFR 1.84(h)(3)).</summary>
    public static class HatchPresets
    {
        public static readonly (string Value, string En, string De)[] Options =
        {
            ("steel", "Steel 45°", "Stahl 45°"),
            ("cross", "Cross 45°/135°", "Kreuz 45°/135°"),
            ("horizontal", "Horizontal", "Waagerecht"),
            ("vertical", "Vertical", "Senkrecht"),
            ("dense", "Dense 45°", "Dicht 45°"),
            ("wide", "Wide 45°", "Weit 45°"),
            ("reverse", "Steel 135°", "Stahl 135°"),
            ("dotted", "Dotted", "Punktiert"),
            ("insulation", "Insulation (zig-zag)", "Dämmung (Zickzack)"),
        };

        /// <summary>Line pattern for a preset (null for dotted/insulation). Angle/spacing overrides win.</summary>
        public static HatchPattern? Pattern(string preset, double? angle = null, double? spacing = null)
        {
            HatchPattern? p = preset switch
            {
                "cross" => new HatchPattern(45, 2.5, Cross: true),
                "horizontal" => new HatchPattern(0, 2),
                "vertical" => new HatchPattern(90, 2),
                "dense" => new HatchPattern(45, 1.0),
                "wide" => new HatchPattern(45, 4),
                "reverse" => new HatchPattern(135, 2),
                "dotted" or "insulation" => null,
                _ => new HatchPattern(45, 2),
            };
            if (p is null) return null;
            return p with { Angle = angle ?? p.Angle, Spacing = Math.Max(0.3, spacing ?? p.Spacing), Width = 0.25 };
        }

        public static IReadOnlyList<ParamDef> Params => new[]
        {
            P.Choice("pattern", "Pattern", "Muster", "steel", Options),
            P.Num("angle", "Angle", "Winkel", null, -180, 180, "°", 5),
            P.Num("spacing", "Spacing", "Abstand", null, 0.3, 50, "mm", 0.25),
            P.Bool("outline", "Outline", "Umriss", true),
        };

        /// <summary>
        /// Renders a hatched region for the given outline primitive (local or page coordinates). Line patterns
        /// use a native hatch fill (hosts may map it to pattern fills; SVG writes explicit clipped lines);
        /// dots without outline and the insulation zig-zag are emitted as explicit clipped geometry.
        /// </summary>
        public static List<Primitive> Region(SketchObject o, Primitive outline)
        {
            var preset = o.Str("pattern", "steel");
            double? angle = o.Params.ContainsKey("angle") ? o.Num("angle", 45) : null;
            double? spacing = o.Params.ContainsKey("spacing") ? o.Num("spacing", 2) : null;
            var showOutline = o.Flag("outline", true);
            var stroke = o.Style?.Stroke is string s && !string.Equals(s, "none", StringComparison.OrdinalIgnoreCase) ? s : "#000000";
            var outlineStyle = showOutline ? Style.ThinLine with { StrokeWidth = 0.35 } : Style.ThinLine with { Stroke = "none" };
            var list = new List<Primitive>();
            var pattern = Pattern(preset, angle, spacing);
            if (pattern is not null)
            {
                list.Add(Prim.Restyle(outline, _ => outlineStyle with { Fill = Fill.Hatched(pattern with { Color = stroke }) }) with { Tag = AnnTags.Region });
                return list;
            }
            var contours = Hatching.Contours(outline);
            if (preset == "dotted" && showOutline)
            {
                list.Add(Prim.Restyle(outline, _ => outlineStyle with { Fill = Fill.Dotted(spacing ?? 1.5) }) with { Tag = AnnTags.Region });
                return list;
            }
            if (preset == "dotted")
            {
                var sp = spacing ?? 1.5;
                var b = Rect.FromPoints(contours.SelectMany(c => c));
                var dot = Style.ThinLine with { Stroke = "none", Fill = Fill.Solid(stroke) };
                var row = 0;
                for (var y = b.Y + sp / 2; y < b.Bottom; y += sp, row++)
                    for (var x = b.X + sp / 2 + (row % 2) * sp / 2; x < b.Right; x += sp)
                        if (Ann.Inside(new Vec2(x, y), contours)) list.Add(Prim.Circle(x, y, Math.Max(0.12, sp * 0.12), dot) with { Tag = AnnTags.Hatch });
            }
            else
            {
                // Insulation: parallel zig-zag rows (angle rotates the rows), clipped to the region.
                var sp = spacing ?? 3.0;
                var rad = -Geo.DegToRad(angle ?? 0);
                var rot = Matrix2D.Rotation(rad);
                var inv = Matrix2D.Rotation(-rad);
                var local = contours.SelectMany(c => c).Select(inv.Apply).ToList();
                var b = Rect.FromPoints(local);
                var hs = Style.ThinLine with { Stroke = stroke };
                for (var y = b.Y + sp / 2; y < b.Bottom + sp; y += sp)
                {
                    var pts = new List<Vec2>();
                    var i = 0;
                    for (var x = b.X - sp; x <= b.Right + sp; x += sp / 2, i++) pts.Add(rot.Apply(new Vec2(x, y + (i % 2 == 0 ? -sp / 2 : sp / 2) * 0.9)));
                    for (var k = 0; k + 1 < pts.Count; k++)
                        foreach (var (p0, p1) in Ann.ClipSegment(pts[k], pts[k + 1], contours))
                            list.Add(new LinePrim(p0, p1, hs) { Tag = AnnTags.Hatch });
                }
            }
            if (showOutline) list.Insert(0, Prim.Restyle(outline, _ => outlineStyle with { Fill = Fill.None }) with { Tag = AnnTags.Region });
            return list;
        }

        public static IEnumerable<ContextAction> Actions(SketchObject o)
        {
            foreach (var (value, en, de) in Options)
                yield return Ann.SetChoice(o, "ann.hatch." + value, new I18nText(en, de), "hatch-" + value, "pattern", value, "steel", "pattern");
            yield return Ann.Toggle(o, "ann.hatch.outline", new I18nText("Outline", "Umriss"), "outline", "outline", true);
        }
    }

    /// <summary>Hatched rectangular or elliptical region (box geometry).</summary>
    public sealed class HatchBoxType : AnnBoxType
    {
        private readonly bool _ellipse;

        public HatchBoxType(bool ellipse)
            : base(ellipse ? "ann.hatch.ellipse" : "ann.hatch.rect",
                   ellipse ? new I18nText("Hatched ellipse", "Schraffierte Ellipse") : new I18nText("Hatched rectangle", "Schraffiertes Rechteck"),
                   AnnCategories.Hatching, "hatch", "schraffur", "section", "schnitt", ellipse ? "ellipse" : "rectangle", ellipse ? "kreis" : "rechteck")
        {
            _ellipse = ellipse;
        }

        public override Size2 DefaultSize => new(30, 20);
        public override IReadOnlyList<ParamDef> Parameters => HatchPresets.Params;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            Primitive outline = _ellipse ? Prim.Ellipse(0, 0, w / 2, h / 2) : Prim.Rect(-w / 2, -h / 2, w, h);
            return HatchPresets.Region(o, outline);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => HatchPresets.Actions(o);
    }

    /// <summary>Hatched free polygon (point geometry: each point is a vertex).</summary>
    public sealed class HatchPolygonType : AnnPointType
    {
        public HatchPolygonType() : base("ann.hatch.polygon", new I18nText("Hatched polygon", "Schraffiertes Polygon"), AnnCategories.Hatching, "hatch", "schraffur", "polygon", "region", "fläche", "section", "schnitt") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, -8), new Vec2(8, -12), new Vec2(16, 2), new Vec2(4, 12), new Vec2(-13, 8) };
        protected override int MinPoints => 3;
        public override IReadOnlyList<ParamDef> Parameters => HatchPresets.Params;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => HatchPresets.Region(o, Prim.Polygon(p));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => HatchPresets.Actions(o);
    }
}
