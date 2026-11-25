using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>Columns, heating/plumbing symbols and site/plan marks.</summary>
    internal static class Symbols
    {
        public static IEnumerable<ObjectType> Types()
        {
            yield return new BoxType("arch.column", new I18nText("Structural column", "Stütze / Säule"), ArchCategories.Structure, new Size2(3, 3), Column,
                "column", "pillar", "post", "stütze", "säule", "pfeiler")
            {
                Hub = true,
                Uniform = true,
                Params = new[]
                {
                    Pm.Choice("shape", "Shape", "Form", "square", ("square", "Square", "Rechteckig"), ("round", "Round", "Rund")).WithOptionIcons(IconIds.ForShape),
                    Pm.Choice("material", "Material", "Material", "concrete", ("concrete", "Reinforced concrete (solid)", "Stahlbeton (schwarz)"), ("masonry", "Masonry (hatched)", "Mauerwerk (schraffiert)"), ("steel", "Steel (outline)", "Stahl (Umriss)")),
                },
                Extra = o => Arch.Choice(o, "arch.column.shape", new I18nText("Shape", "Form"), "shape", "square",
                        new[] { ("square", new I18nText("Square", "Rechteckig")), ("round", new I18nText("Round", "Rund")) }, icon: IconIds.ForShape)
                    .Concat(Arch.Choice(o, "arch.column.material", new I18nText("Material", "Material"), "material", "concrete", new[]
                    {
                        ("concrete", new I18nText("Concrete (solid)", "Stahlbeton (schwarz)")), ("masonry", new I18nText("Masonry (hatched)", "Mauerwerk (schraffiert)")), ("steel", new I18nText("Steel (outline)", "Stahl (Umriss)")),
                    })),
            };

            var sv = ArchCategories.Services;
            // Flow and return connections: pipes snap and dock there.
            yield return new BoxType("arch.hvac.radiator", new I18nText("Radiator", "Heizkörper"), sv, new Size2(10, 1.5), Radiator, "radiator", "heizkörper", "heater", "heizung", "heating")
            { Ports = new[] { (-0.5, 0.0, "flow"), (0.5, 0.0, "return") } };
            yield return new BoxType("arch.hvac.heatpump", new I18nText("Heat pump (outdoor unit)", "Wärmepumpe (Außeneinheit)"), sv, new Size2(10, 5), HeatPump,
                "heat pump", "wärmepumpe", "outdoor unit", "außeneinheit", "hvac", "heating", "heizung")
            { Ports = new[] { (-0.5, -0.2, "flow"), (-0.5, 0.2, "return") } };
            yield return new BoxType("arch.hvac.boiler", new I18nText("Boiler", "Heizkessel / Therme"), sv, new Size2(6, 6), Boiler, "boiler", "heizkessel", "therme", "furnace", "gas", "heating", "heizung")
            { Ports = new[] { (-0.25, 0.5, "flow"), (0.25, 0.5, "return") } };
            yield return new PipeType();

            var st = ArchCategories.Site;
            yield return new BoxType("arch.site.north", new I18nText("North arrow", "Nordpfeil"), st, new Size2(10, 14), North, "north", "nord", "nordpfeil", "compass", "orientation")
            { Uniform = true };
            yield return new BoxType("arch.site.level", new I18nText("Level marker", "Höhenkote"), st, new Size2(20, 5), Level, "level", "höhe", "höhenkote", "elevation mark", "datum", "okff", "kote")
            {
                TextEditable = true,
                Params = new[] { Pm.Num("level", "Level", "Höhe", 0, -10000, 10000, "m", 0.01) },
            };
            yield return new SectionType();
            yield return new BoundaryType();
            yield return new BoxType("arch.site.parking", new I18nText("Parking space", "Stellplatz"), st, new Size2(25, 50), Parking, "parking", "stellplatz", "parkplatz", "car", "auto", "garage");
            yield return new BoxType("arch.site.tree", new I18nText("Tree", "Baum"), st, new Size2(30, 30), Tree, "tree", "baum", "landscape", "landschaft", "garden", "garten", "plant", "pflanze", "shrub", "strauch", "conifer", "nadelbaum")
            {
                Uniform = true,
                Params = new[] { Pm.Choice("kind", "Kind", "Art", "deciduous", ("deciduous", "Deciduous tree", "Laubbaum"), ("conifer", "Conifer", "Nadelbaum"), ("shrub", "Shrub", "Strauch")) },
                Extra = o => Arch.Choice(o, "arch.tree.kind", new I18nText("Kind", "Art"), "kind", "deciduous", TreeKinds),
            };
        }

        private static readonly (string, I18nText)[] TreeKinds =
        {
            ("deciduous", new I18nText("Deciduous tree", "Laubbaum")), ("conifer", new I18nText("Conifer", "Nadelbaum")), ("shrub", new I18nText("Shrub", "Strauch")),
        };

        private static void Column(Canvas c, SketchObject o, RenderContext ctx)
        {
            var style = o.Str("material", "concrete") switch
            {
                "masonry" => St.Wall with { Fill = Fill.Hatched(new HatchPattern(45, Math.Max(0.4, c.K * 0.2), Color: "#000000", Width: 0.13)) },
                "steel" => St.Wall,
                _ => St.Wall with { Fill = Fill.Solid("#000000") },
            };
            if (o.Str("shape", "square") == "round") c.Ellipse(Vec2.Zero, c.Hw, c.Hh, style, ArchTags.Outline);
            else c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, style, ArchTags.Outline);
            if (o.Str("material", "concrete") == "steel")
            {
                // I-section in the column outline.
                double fw = 0.8 * c.Hw, fh = 0.12 * c.H, tw = 0.08 * c.W;
                c.Poly(new[]
                {
                    new Vec2(-fw, -0.4 * c.H), new Vec2(fw, -0.4 * c.H), new Vec2(fw, -0.4 * c.H + fh), new Vec2(tw, -0.4 * c.H + fh),
                    new Vec2(tw, 0.4 * c.H - fh), new Vec2(fw, 0.4 * c.H - fh), new Vec2(fw, 0.4 * c.H), new Vec2(-fw, 0.4 * c.H),
                    new Vec2(-fw, 0.4 * c.H - fh), new Vec2(-tw, 0.4 * c.H - fh), new Vec2(-tw, -0.4 * c.H + fh), new Vec2(-fw, -0.4 * c.H + fh),
                }, St.Black, true, "profile");
            }
        }

        private static void Radiator(Canvas c, SketchObject o, RenderContext ctx)
        {
            var body = c.Hw - Math.Min(0.12 * c.W, c.H * 0.8);
            c.Rect(-c.Hw, -c.Hh, body, c.Hh, St.Thin, ArchTags.Outline);
            var n = Math.Max(3, (int)Math.Round((body + c.Hw) / 0.8));
            for (var i = 1; i < n; i++) { var x = -c.Hw + i * (body + c.Hw) / n; c.Line(new Vec2(x, -c.Hh), new Vec2(x, c.Hh), St.Fine, "rib"); }
            var r = Math.Min((c.Hw - body) / 2, c.Hh) * 0.8;
            c.Ellipse(new Vec2((body + c.Hw) / 2, 0), r, r, St.Thin, "valve");
        }

        private static void HeatPump(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var r = Math.Min(0.3 * c.W, 0.42 * c.H);
            var fc = new Vec2(-c.Hw + 0.08 * c.W + r, 0);
            c.Ellipse(fc, r, r, St.Thin, "fan");
            for (var i = 0; i < 4; i++)
            {
                var a = Geo.DegToRad(20 + i * 90);
                var tip = fc + Vec2.Polar(r * 0.85, a);
                var side = fc + Vec2.Polar(r * 0.55, a + 0.6);
                c.Poly(new[] { fc, side, tip }, St.Fine, true, "blade");
            }
            var x0 = fc.X + r + 0.04 * c.W;
            c.Text(new Vec2((x0 + c.Hw) / 2, 0), ctx.Lang == Lang.De ? "WP" : "HP", 2.5, (c.Hw - x0) * 0.85, c.H * 0.5, ArchTags.Label);
        }

        private static void Boiler(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            // Stylised flame.
            Vec2 P(double u, double v) => c.N(u, v);
            c.Add(new PathPrim(new[]
            {
                PathSeg.Move(P(0, 0.32)),
                PathSeg.Cubic(P(-0.3, 0.3), P(-0.28, 0), P(-0.06, -0.34)),
                PathSeg.Cubic(P(-0.06, -0.1), P(0.08, -0.08), P(0.06, -0.22)),
                PathSeg.Cubic(P(0.3, 0), P(0.3, 0.3), P(0, 0.32)),
                PathSeg.Close,
            }, St.Thin), "flame");
        }

        private static void North(Canvas c, SketchObject o, RenderContext ctx)
        {
            var ts = Math.Min(0.25 * c.H, 0.6 * c.W);
            c.Text(new Vec2(0, -c.Hh + ts / 2), "N", ts, c.W, ts, ArchTags.Label, bold: true);
            var cy = (-c.Hh + ts * 1.1 + c.Hh) / 2;
            var r = Math.Min(c.Hw, (c.H - ts * 1.1) / 2);
            c.Ellipse(new Vec2(0, cy), r, r, St.Thin, ArchTags.Outline);
            var tip = new Vec2(0, cy - r); var notch = new Vec2(0, cy + 0.35 * r);
            c.Poly(new[] { tip, new Vec2(-0.42 * r, cy + 0.75 * r), notch }, St.Black, true, "arrow");
            c.Poly(new[] { tip, new Vec2(0.42 * r, cy + 0.75 * r), notch }, St.ThinWhite, true, "arrow");
        }

        /// <summary>"+2.75", "-0.15", "±0.00" (decimal comma in German).</summary>
        public static string LevelText(double level, Lang lang)
        {
            var abs = Arch.Format(Math.Abs(level), 2, lang);
            if (Math.Abs(level) < 0.005) return "±" + abs;
            return (level > 0 ? "+" : "-") + abs;
        }

        private static void Level(Canvas c, SketchObject o, RenderContext ctx)
        {
            var t = Math.Min(0.75 * c.H, 0.3 * c.W);
            var hw = 0.577 * t;
            var apex = new Vec2(-c.Hw + hw, c.Hh);
            c.Line(new Vec2(-c.Hw, c.Hh), new Vec2(c.Hw, c.Hh), St.Thin, "datum");
            c.Poly(new[] { apex, new Vec2(-c.Hw, c.Hh - t), new Vec2(apex.X, c.Hh - t) }, St.Black, true, "marker");
            c.Poly(new[] { apex, new Vec2(apex.X, c.Hh - t), new Vec2(-c.Hw + 2 * hw, c.Hh - t) }, St.ThinWhite, true, "marker");
            var text = string.IsNullOrEmpty(o.Text) ? LevelText(o.Num("level", 0), ctx.Lang) : o.Text!;
            var x0 = -c.Hw + 2 * hw + 0.6;
            c.Text(new Vec2(x0, c.Hh - t / 2 - 0.1 * c.H), text, 3.5, c.Hw - x0, Math.Min(t, c.H * 0.8), ArchTags.Label, anchor: TextAnchor.Start);
        }

        private static void Parking(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Poly(new[] { new Vec2(-c.Hw, c.Hh), new Vec2(-c.Hw, -c.Hh), new Vec2(c.Hw, -c.Hh), new Vec2(c.Hw, c.Hh) }, St.Medium, false, ArchTags.Outline);
            c.Text(Vec2.Zero, "P", 0.4 * c.W, 0.6 * c.W, 0.4 * c.H, ArchTags.Label, bold: true);
        }

        /// <summary>Closed outline of bumps (cubic) between points on two radii (fractions of the half box).</summary>
        private static PathPrim Bumps(Canvas c, int n, double rIn, double rOut, Style s, double phase = 0, double jitter = 0)
        {
            Vec2 At(double deg, double r) { var a = Geo.DegToRad(deg); return new Vec2(c.Hw * r * Math.Cos(a), c.Hh * r * Math.Sin(a)); }
            var segs = new List<PathSeg>();
            for (var i = 0; i <= n; i++)
            {
                var deg = phase + i * 360.0 / n;
                var r = rIn * (1 - jitter * ((i * 7 % 5) / 5.0));
                var p = At(deg, i == n ? rIn : r);
                if (i == 0) { segs.Add(PathSeg.Move(p)); continue; }
                var prev = segs[segs.Count - 1].P;
                var ctrl = At(deg - 180.0 / n, rOut);
                segs.Add(PathSeg.Cubic(prev + (ctrl - prev) * (2.0 / 3), p + (ctrl - p) * (2.0 / 3), p));
            }
            segs.Add(PathSeg.Close);
            return new PathPrim(segs, s);
        }

        private static void Tree(Canvas c, SketchObject o, RenderContext ctx)
        {
            var canopy = St.Thin with { Fill = Fill.Solid("#e4efd9") };
            switch (o.Str("kind", "deciduous"))
            {
                case "conifer":
                    {
                        var pts = new List<Vec2>();
                        const int n = 14;
                        for (var i = 0; i < 2 * n; i++)
                        {
                            var a = Geo.DegToRad(i * 180.0 / n);
                            var r = i % 2 == 0 ? 1.0 : 0.72;
                            pts.Add(new Vec2(c.Hw * r * Math.Cos(a), c.Hh * r * Math.Sin(a)));
                        }
                        c.Poly(pts, canopy, true, "canopy");
                        for (var i = 0; i < 2 * n; i += 2) c.Line(Vec2.Zero, pts[i] * 0.72, St.Fine, "branch");
                        break;
                    }
                case "shrub":
                    c.Add(Bumps(c, 11, 0.7, 1.0, canopy, 10, 0.25), "canopy");
                    c.Add(Bumps(c, 7, 0.3, 0.45, St.Fine, 30), "canopy");
                    return;
                default:
                    c.Add(Bumps(c, 9, 0.8, 1.0, canopy), "canopy");
                    break;
            }
            var t = 0.07 * c.K;
            c.Ellipse(Vec2.Zero, t, t, St.Thin, "trunk");
            c.Line(new Vec2(-2 * t, 0), new Vec2(2 * t, 0), St.Fine, "trunk");
            c.Line(new Vec2(0, -2 * t), new Vec2(0, 2 * t), St.Fine, "trunk");
        }
    }

    /// <summary>Pipe run (heating flow/return, water, waste, gas) as a polyline with medium-specific colour and dash.</summary>
    internal sealed class PipeType : PointType
    {
        public static readonly (string Value, I18nText Label, string Color, DashStyle Dash, double Width)[] Media =
        {
            ("flow", new I18nText("Heating flow", "Heizung Vorlauf"), "#c62828", DashStyle.Solid, 0.5),
            ("return", new I18nText("Heating return", "Heizung Rücklauf"), "#1565c0", DashStyle.Dash, 0.5),
            ("cold", new I18nText("Cold water", "Kaltwasser"), "#2e7d32", DashStyle.Solid, 0.5),
            ("hot", new I18nText("Hot water", "Warmwasser"), "#ad1457", DashStyle.DashDot, 0.5),
            ("waste", new I18nText("Waste water", "Abwasser"), "#6d4c41", DashStyle.Solid, 0.8),
            ("gas", new I18nText("Gas", "Gas"), "#b28704", DashStyle.DashDotDot, 0.5),
        };

        public PipeType() : base("arch.hvac.pipe", new I18nText("Pipe", "Rohrleitung"), ArchCategories.Services,
            "pipe", "rohr", "leitung", "rohrleitung", "plumbing", "sanitär", "heating", "heizung", "water", "wasser", "flow", "return", "vorlauf", "rücklauf", "waste", "abwasser", "gas") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        /// <summary>Corners, extending, drawing on and branches (tees docked on the rail).</summary>
        public override bool InsertablePoints => true;

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > 1e-6) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Choice("medium", "Medium", "Medium", "flow", Media.Select(m => (m.Value, m.Label.En, m.Label.De ?? m.Label.En)).ToArray()),
            Pm.Bool("arrow", "Flow arrow", "Fließrichtungspfeil", false),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var key = o.Str("medium", "flow");
            var m = Media.FirstOrDefault(x => x.Value == key);
            if (m.Value is null) m = Media[0];
            var style = new Style { Stroke = m.Color, StrokeWidth = m.Width, Dash = m.Dash, Role = LineRole.Visible };
            if (o.Flag("arrow", false)) style = style with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            return new Primitive[] { Prim.Polyline(p, style) with { Tag = "pipe" } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Arch.Choice(o, "arch.pipe.medium", new I18nText("Medium", "Medium"), "medium", "flow", Media.Select(m => (m.Value, m.Label)).ToList(),
                preview: v => Media.FirstOrDefault(m => m.Value == v) is { Value: not null } m
                    ? new Primitive[] { Prim.Line(0, 0, 12, 0, new Style { Stroke = m.Color, StrokeWidth = m.Width * 2, Dash = m.Dash, Role = LineRole.Visible }) }
                    : null))
                yield return a;
            yield return Arch.Toggle(o, "arch.pipe.arrow", new I18nText("Flow arrow", "Fließrichtungspfeil"), IconIds.FlowArrow, "arrow", false);
        }
    }

    /// <summary>Building section marker: heavy line ends with view-direction arrows and letters, thin dash-dot line between.</summary>
    internal sealed class SectionType : PointType
    {
        public SectionType() : base("arch.site.section", new I18nText("Section marker", "Schnittlinie (Gebäudeschnitt)"), ArchCategories.Site,
            "section", "schnitt", "schnittlinie", "cut", "gebäudeschnitt", "a-a") { }

        public override bool HasText => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, 0), new Vec2(30, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Text("label", "Letter", "Buchstabe", "A"),
            Pm.Bool("flip", "Look the other way", "Blickrichtung umkehren", false),
            Pm.Bool("full", "Line between the ends", "Durchgehende Linie", true),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return Array.Empty<Primitive>();
            var d = (b - a) / len;
            var n = d.Perp * (o.Flag("flip", false) ? -1 : 1);
            var e = Math.Min(6, len / 4);
            var heavy = Style.Visible with { StrokeWidth = 0.7 };
            var list = new List<Primitive>
            {
                Prim.Line(a, a + d * e, heavy) with { Tag = "end" },
                Prim.Line(b - d * e, b, heavy) with { Tag = "end" },
            };
            if (o.Flag("full", true)) list.Add(Prim.Line(a + d * e, b - d * e, St.Center) with { Tag = "line" });
            var arrow = St.Medium with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
            list.Add(Prim.Line(a, a + n * 5, arrow) with { Tag = "view" });
            list.Add(Prim.Line(b, b + n * 5, arrow) with { Tag = "view" });
            var label = string.IsNullOrEmpty(o.Text) ? o.Str("label", "A") : o.Text!;
            if (!string.IsNullOrEmpty(label))
            {
                list.Add(Arch.Text(a + n * 3 - d * 3, label, 4, ArchTags.Label, bold: true));
                list.Add(Arch.Text(b + n * 3 + d * 3, label, 4, ArchTags.Label, bold: true));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Arch.Toggle(o, "arch.section.flip", new I18nText("Flip view direction", "Blickrichtung umkehren"), IconIds.Reverse, "flip", false, "flip");
        }
    }

    /// <summary>Site boundary: closed dash-dot-dot polygon with optional boundary point markers.</summary>
    internal sealed class BoundaryType : PointType
    {
        public BoundaryType() : base("arch.site.boundary", new I18nText("Site boundary", "Grundstücksgrenze"), ArchCategories.Site,
            "site", "boundary", "plot", "lot", "property line", "grundstück", "grenze", "flurstück") { }

        protected override int MinPoints => 2;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-60, -40), new Vec2(60, -40), new Vec2(60, 40), new Vec2(-60, 40) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Bool("closed", "Closed", "Geschlossen", true),
            Pm.Bool("markers", "Boundary point markers", "Grenzpunkte markieren", true),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var closed = o.Flag("closed", true) && p.Count >= 3;
            var list = new List<Primitive> { Prim.Polyline(p, Style.Of(LineRole.Phantom) with { StrokeWidth = 0.5 }, closed) with { Tag = ArchTags.Outline } };
            if (o.Flag("markers", true))
                foreach (var q in p) list.Add(Prim.Circle(q.X, q.Y, 0.7, St.ThinWhite) with { Tag = "marker" });
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Arch.Toggle(o, "arch.boundary.closed", new I18nText("Closed", "Geschlossen"), IconIds.Toggle, "closed", true);
            yield return Arch.Toggle(o, "arch.boundary.markers", new I18nText("Boundary point markers", "Grenzpunkte markieren"), IconIds.Toggle, "markers", true);
        }
    }
}
