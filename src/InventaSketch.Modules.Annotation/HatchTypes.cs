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
            // Material patterns (simple, drawn independently; a section shows what the part is made of).
            ("liquid", "Liquid", "Flüssigkeit"),
            ("glass", "Glass", "Glas"),
            ("plastic", "Rubber / plastic", "Gummi / Kunststoff"),
            ("wood", "Wood", "Holz"),
            ("concrete", "Concrete", "Beton"),
            ("earth", "Earth / soil", "Erdreich"),
        };

        /// <summary>Material patterns drawn as explicit clipped geometry.</summary>
        public static readonly string[] Materials = { "liquid", "glass", "plastic", "wood", "concrete", "earth" };

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
                "dotted" or "insulation" or "liquid" or "glass" or "plastic" or "wood" or "concrete" or "earth" => null,
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
            else if (Array.IndexOf(Materials, preset) >= 0)
                list.AddRange(Material(preset, contours, angle, spacing, stroke));
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

        /// <summary>
        /// Material pattern clipped to the region (angle rotates the pattern, spacing scales it):
        /// liquid – rows of dashes; glass – pairs of close 45° lines; rubber/plastic – 45° lines alternately solid and dashed;
        /// wood – wavy grain lines; concrete – small triangles and dots (aggregate); earth – groups of three short 45° strokes.
        /// </summary>
        public static List<Primitive> Material(string preset, IReadOnlyList<IReadOnlyList<Vec2>> contours, double? angle, double? spacing, string stroke)
        {
            var list = new List<Primitive>();
            var rad = -Geo.DegToRad(angle ?? 0);
            var rot = Matrix2D.Rotation(rad);
            var inv = Matrix2D.Rotation(-rad);
            var b = Rect.FromPoints(contours.SelectMany(c => c).Select(inv.Apply));
            var hs = Style.ThinLine with { Stroke = stroke, StrokeWidth = 0.25 };
            // Pattern coordinates (u along the rows, v across them) to page coordinates.
            Vec2 At(double u, double v) => rot.Apply(new Vec2(u, v));
            void Clip(Vec2 a, Vec2 c, Style s)
            {
                foreach (var (p0, p1) in Ann.ClipSegment(a, c, contours)) list.Add(new LinePrim(p0, p1, s) { Tag = AnnTags.Hatch });
            }
            // Diagonal families: a 45° line through (u0, v) in pattern coordinates spans the whole box.
            var diag = b.W + b.H;
            // The 45° line rising to the right through (b.X + offset, b.Y); offsets 0 … W + H cover the box, a step of
            // s·√2 gives lines s apart.
            void Diagonal(double offset, Style s) => Clip(At(b.X + offset - diag, b.Y + diag), At(b.X + offset + diag, b.Y - diag), s);
            // Deterministic jitter for scattered patterns.
            double Hash(int i, int j, int k) { var x = Math.Sin(i * 127.1 + j * 311.7 + k * 74.7) * 43758.5453; return x - Math.Floor(x); }
            switch (preset)
            {
                case "liquid":
                    {
                        var sp = spacing ?? 2.5;
                        var dash = sp * 2; var gap = sp;
                        var row = 0;
                        for (var v = b.Y + sp / 2; v < b.Bottom; v += sp, row++)
                            for (var u = b.X - (row % 2) * (dash + gap) / 2; u < b.Right; u += dash + gap)
                                Clip(At(u, v), At(u + dash, v), hs);
                        break;
                    }
                case "glass":
                    {
                        var sp = spacing ?? 5;
                        for (var off = -b.H; off < b.W + b.H; off += sp * Math.Sqrt(2))
                        {
                            Diagonal(off, hs);
                            Diagonal(off + Math.Min(0.9, sp * 0.18) * Math.Sqrt(2), hs);
                        }
                        break;
                    }
                case "plastic":
                    {
                        var sp = spacing ?? 2;
                        var i = 0;
                        for (var off = -b.H; off < b.W + b.H; off += sp * Math.Sqrt(2), i++)
                            Diagonal(off, i % 2 == 0 ? hs : hs with { Dash = DashStyle.Dash });
                        break;
                    }
                case "wood":
                    {
                        var sp = spacing ?? 2.5;
                        var amp = sp * 0.22; var wave = sp * 4;
                        var row = 0;
                        for (var v = b.Y + sp / 2; v < b.Bottom; v += sp, row++)
                        {
                            var phase = Hash(row, 3, 1) * 2 * Math.PI;
                            Vec2? last = null;
                            for (var u = b.X - 1; u <= b.Right + 1; u += 0.8)
                            {
                                var p = At(u, v + amp * Math.Sin(2 * Math.PI * u / wave + phase));
                                if (last is Vec2 q) Clip(q, p, hs);
                                last = p;
                            }
                        }
                        break;
                    }
                case "concrete":
                    {
                        var sp = spacing ?? 4;
                        var dot = Style.ThinLine with { Stroke = "none", Fill = Fill.Solid(stroke) };
                        for (var i = 0; b.X + i * sp < b.Right; i++)
                            for (var j = 0; b.Y + j * sp < b.Bottom; j++)
                            {
                                var c = new Vec2(b.X + (i + 0.2 + 0.6 * Hash(i, j, 1)) * sp, b.Y + (j + 0.2 + 0.6 * Hash(i, j, 2)) * sp);
                                var r = sp * (0.16 + 0.08 * Hash(i, j, 3));
                                var a0 = Hash(i, j, 4) * 2 * Math.PI;
                                var tri = Enumerable.Range(0, 3).Select(k => At(c.X + r * Math.Cos(a0 + k * 2 * Math.PI / 3), c.Y + r * Math.Sin(a0 + k * 2 * Math.PI / 3))).ToList();
                                if (tri.All(p => Ann.Inside(p, contours))) list.Add(Prim.Polygon(tri, hs) with { Tag = AnnTags.Hatch });
                                var d = At(b.X + (i + Hash(i, j, 5)) * sp, b.Y + (j + Hash(i, j, 6)) * sp);
                                if (Ann.Inside(d, contours) && d.DistanceTo(At(c.X, c.Y)) > r * 1.5) list.Add(Prim.Circle(d.X, d.Y, Math.Max(0.15, sp * 0.05), dot) with { Tag = AnnTags.Hatch });
                            }
                        break;
                    }
                case "earth":
                    {
                        var sp = spacing ?? 5;
                        const double len = 2.2, step = 0.7;
                        var dir = new Vec2(1, -1).Normalized();
                        var across = new Vec2(1, 1).Normalized();
                        var row = 0;
                        for (var v = b.Y + sp / 2; v < b.Bottom + sp / 2; v += sp * 0.8, row++)
                            for (var u = b.X + (row % 2) * sp / 2; u < b.Right + sp / 2; u += sp)
                                for (var k = -1; k <= 1; k++)
                                {
                                    var c = new Vec2(u, v) + across * (k * step);
                                    Clip(At(c.X - dir.X * len / 2, c.Y - dir.Y * len / 2), At(c.X + dir.X * len / 2, c.Y + dir.Y * len / 2), hs);
                                }
                        break;
                    }
            }
            return list;
        }

        public static IEnumerable<ContextAction> Actions(SketchObject o)
        {
            foreach (var (value, en, de) in Options)
                yield return Ann.SetChoice(o, "ann.hatch." + value, new I18nText(en, de), IconIds.Option, "pattern", value, "steel", "pattern");
            yield return Ann.Toggle(o, "ann.hatch.outline", new I18nText("Outline", "Umriss"), IconIds.Toggle, "outline", true);
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
        public override bool InsertablePoints => true;
        public override bool IsClosed(SketchObject o) => true;
        /// <summary>A hatched region is drawn, not docked.</summary>
        public override bool DocksOnSurface => false;
        public override IReadOnlyList<ParamDef> Parameters => HatchPresets.Params;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => HatchPresets.Region(o, Prim.Polygon(p));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => HatchPresets.Actions(o);
    }
}
