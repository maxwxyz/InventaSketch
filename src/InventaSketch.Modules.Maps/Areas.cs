using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>Map frame / background tile: plain, with a labelled reference grid, or with a terrain hint (contour lines).</summary>
    internal static class Frame
    {
        public const string TypeId = "map.frame";

        private static readonly ParamDef StyleDef = Prm.Choice("style", "Background", "Hintergrund", "plain",
            ("plain", "Plain", "Schlicht"), ("grid", "Reference grid", "Planquadrate"), ("terrain", "Terrain hint", "Geländeandeutung"));
        private static readonly ParamDef CellsDef = Prm.Int("cells", "Grid columns", "Gitterspalten", 6, 1, 26);
        private static readonly ParamDef LabelsDef = Prm.Bool("labels", "Grid labels (A, B … / 1, 2 …)", "Gitterbeschriftung (A, B … / 1, 2 …)", true);
        private static readonly ParamDef TintDef = Prm.Bool("tint", "Land tint", "Landfarbe", true);

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                double hw = p.W / 2, hh = p.H / 2;
                if (o.Flag("tint", true)) p.Add(Prim.Rect(-hw, -hh, p.W, p.H, S.FillOnly(Palette.Land)), MapTags.Background);
                var d = Math.Min(1.0, 0.03 * Math.Min(p.W, p.H));
                switch (o.Str("style", "plain"))
                {
                    case "grid": Grid(p, o, hw, hh, d); break;
                    case "terrain": Terrain(p, hw, hh); break;
                }
                p.Add(Prim.Rect(-hw, -hh, p.W, p.H, S.Stroke(Palette.Ink, 0.5)), MapTags.Frame);
                if (d > 0.15) p.Add(Prim.Rect(-hw + d, -hh + d, p.W - 2 * d, p.H - 2 * d, S.Stroke(Palette.Ink, 0.18)), MapTags.Frame);
            }
            return new MapBoxType(TypeId, new I18nText("Map frame", "Kartenrahmen"), MapCategories.Base,
                new[] { "map", "frame", "background", "tile", "grid", "terrain", "contour", "base map",
                        "karte", "rahmen", "kartenrahmen", "hintergrund", "kachel", "gitter", "planquadrat", "gelände", "höhenlinien" },
                MapViews.Plan(160, 100, Draw))
            {
                Params = new[] { StyleDef, CellsDef, LabelsDef, TintDef },
                Uniform = false,
                CompanionIds = new[] { RoadType.TypeId, RouteType.TypeId, RegionType.TypeId, Place.TypeId, ScaleBar.TypeId, NorthArrow.TypeId, Legend.TypeId },
                Extra = (o, lang) => new[]
                {
                    Prm.Cycle(o, "map.frame.style", StyleDef, new I18nText("Background", "Hintergrund")),
                    Prm.Toggle(o, "map.frame.labels", "labels", true, new I18nText("Grid labels", "Gitterbeschriftung")),
                    Prm.Toggle(o, "map.frame.tint", "tint", true, new I18nText("Land tint", "Landfarbe")),
                    new ContextAction("map.frame.cells.inc", new I18nText("More grid cells", "Mehr Planquadrate"), IconIds.Increase, x => x.WithParam("cells", Math.Min(26, x.Int("cells", 6) + 1))) { Group = "cells" },
                    new ContextAction("map.frame.cells.dec", new I18nText("Fewer grid cells", "Weniger Planquadrate"), IconIds.Decrease, x => x.WithParam("cells", Math.Max(1, x.Int("cells", 6) - 1))) { Group = "cells" },
                },
            };
        }

        /// <summary>Reference grid; labels sit inside the neat line (inset <paramref name="d"/>).</summary>
        private static void Grid(Pen p, SketchObject o, double hw, double hh, double d)
        {
            var cols = Math.Max(1, Math.Min(26, o.Int("cells", 6)));
            var rows = Math.Max(1, (int)Math.Round(cols * p.H / p.W));
            double cw = p.W / cols, ch = p.H / rows;
            var lines = new List<IReadOnlyList<Vec2>>();
            for (var i = 1; i < cols; i++) lines.Add(new[] { new Vec2(-hw + i * cw, -hh), new Vec2(-hw + i * cw, hh) });
            for (var j = 1; j < rows; j++) lines.Add(new[] { new Vec2(-hw, -hh + j * ch), new Vec2(hw, -hh + j * ch) });
            var path = Geom.MultiPath(lines, S.Stroke(Palette.GridLine, 0.18), MapTags.Grid);
            if (path is not null) p.Add(path);
            if (!o.Flag("labels", true)) return;
            var fs = Math.Min(2.6, Math.Min(cw, ch) * 0.3);
            if (fs < 0.6) return;
            const string color = "#616161";
            for (var i = 0; i < cols; i++)
                p.TextAt(new Vec2(-hw + (i + 0.5) * cw, -hh + d + 0.5 + fs / 2), ((char)('A' + i)).ToString(), fs, cw * 0.8, fs, color: color, tag: MapTags.Grid);
            for (var j = 0; j < rows; j++)
                p.TextAt(new Vec2(-hw + d + 0.7, -hh + (j + 0.5) * ch), Geom.I(j + 1), fs, cw * 0.4, ch * 0.8, TextAnchor.Start, color: color, tag: MapTags.Grid);
        }

        /// <summary>Two hills as sets of wobbly closed contour lines (normalised to the box, so they stretch with it).</summary>
        private static void Terrain(Pen p, double hw, double hh)
        {
            void Hill(double cx, double cy, double rx, double ry, int rings, double phase)
            {
                for (var k = 1; k <= rings; k++)
                {
                    var f = (double)k / rings;
                    var pts = new List<Vec2>();
                    for (var j = 0; j < 10; j++)
                    {
                        var th = 2 * Math.PI * j / 10;
                        var r = f * (1 + 0.12 * Math.Sin(3 * th + phase) + 0.06 * Math.Cos(5 * th + 2 * phase));
                        pts.Add(new Vec2((cx + rx * r * Math.Cos(th)) * hw, (cy + ry * r * Math.Sin(th)) * hh));
                    }
                    p.Add(new PathPrim(Geom.Spline(pts, closed: true), S.Stroke(Palette.Contour, k == rings ? 0.25 : 0.18)), MapTags.Terrain);
                }
                var top = new Vec2(cx * hw, cy * hh);
                var t = Math.Min(0.06 * Math.Min(hw, hh), 1.2);
                p.Add(Prim.Polygon(new[] { top + new Vec2(0, -t), top + new Vec2(t, t * 0.7), top + new Vec2(-t, t * 0.7) }, S.FillOnly(Palette.Contour)), MapTags.Terrain);
            }
            Hill(-0.4, -0.15, 0.42, 0.55, 4, 0.4);
            Hill(0.45, 0.35, 0.3, 0.4, 3, 1.9);
        }
    }

    /// <summary>Region / area as a closed polygon (optionally smooth) with tint, hatch or no fill and an optional name.</summary>
    internal sealed class RegionType : MapPointType
    {
        public const string TypeId = "map.region";
        public override bool InsertablePoints => true;

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Area type", "Flächenart", "area",
            ("area", "Area", "Gebiet"), ("forest", "Forest", "Wald"), ("water", "Lake / water", "See / Gewässer"), ("park", "Park / green space", "Park / Grünfläche"),
            ("urban", "Built-up area", "Bebaute Fläche"), ("restricted", "Restricted zone / geofence", "Sperrzone / Geofence"));
        public static readonly ParamDef FillDef = Prm.Choice("fill", "Fill", "Füllung", "tint", ("tint", "Tint", "Farbton"), ("hatch", "Hatch", "Schraffur"), ("none", "None", "Keine")).WithOptionIcons(IconIds.ForFill);
        private static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth outline", "Glatter Umriss", false);

        private sealed record KindSpec(string Tint, string Stroke, DashStyle Dash, double Width, Fill Hatch);

        private static KindSpec Spec(string kind) => kind switch
        {
            "forest" => new("#cfe8c4", "#4f7a3a", DashStyle.Solid, 0.3, Fill.Dotted(1.6)),
            "water" => new(Palette.Water, Palette.WaterLine, DashStyle.Solid, 0.3, Fill.Hatched(new HatchPattern(0, 1.2, Color: Palette.WaterLine, Width: 0.15))),
            "park" => new("#dcedc8", "#689f38", DashStyle.Solid, 0.3, Fill.Hatched(new HatchPattern(135, 1.8, Color: "#689f38", Width: 0.15))),
            "urban" => new("#e8e1d9", "#8d8079", DashStyle.Solid, 0.3, Fill.Hatched(new HatchPattern(45, 1.6, Cross: true, Color: "#8d8079", Width: 0.12))),
            "restricted" => new("#fde0dc", "#c62828", DashStyle.Dash, 0.5, Fill.Hatched(new HatchPattern(45, 2.5, Color: "#c62828", Width: 0.2))),
            _ => new("#fff3c4", "#8d6e00", DashStyle.Dash, 0.35, Fill.Hatched(new HatchPattern(45, 2.0, Color: "#8d6e00", Width: 0.15))),
        };

        public RegionType() : base(TypeId, new I18nText("Region", "Gebiet"), MapCategories.Base,
            "region", "area", "zone", "polygon", "forest", "lake", "water", "park", "urban", "district", "geofence", "restricted", "map",
            "gebiet", "fläche", "zone", "bereich", "wald", "see", "park", "grünfläche", "stadtgebiet", "sperrzone", "geofence", "karte") { }

        public override bool HasText => true;
        protected override bool ClosedOutline => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, -16), new Vec2(8, -22), new Vec2(32, -6), new Vec2(26, 18), new Vec2(-8, 22), new Vec2(-32, 8) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { KindDef, FillDef, SmoothDef };
        public override IReadOnlyList<string> Companions => new[] { Place.TypeId, PoiType.TypeId, RoadType.TypeId, Legend.TypeId };

        /// <summary>Area centroid of the polygon (mean of the points for degenerate polygons).</summary>
        public static Vec2 Centroid(IReadOnlyList<Vec2> p)
        {
            double a = 0, cx = 0, cy = 0;
            for (var i = 0; i < p.Count; i++)
            {
                var q = p[i]; var r = p[(i + 1) % p.Count];
                var c = q.Cross(r);
                a += c; cx += (q.X + r.X) * c; cy += (q.Y + r.Y) * c;
            }
            if (Math.Abs(a) < 1e-9) return new Vec2(p.Average(v => v.X), p.Average(v => v.Y));
            return new Vec2(cx / (3 * a), cy / (3 * a));
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var kind = o.Str("kind", "area");
            var spec = Spec(kind);
            var mode = o.Str("fill", "tint");
            // Black-and-white output: a tint would turn white, so the kind's hatch keeps the area visible.
            if (mode == "tint" && MapDeps.Mono(ctx)) mode = "hatch";
            var fill = mode switch { "hatch" => spec.Hatch, "none" => Fill.None, _ => Fill.Solid(spec.Tint) };
            var style = S.Stroke(spec.Stroke, spec.Width, spec.Dash) with { Fill = fill };
            var list = new List<Primitive>();
            if (p.Count < 3) list.Add(Prim.Polyline(p, style with { Fill = Fill.None }) with { Tag = MapTags.Region });
            else if (o.Flag("smooth", false)) list.Add(new PathPrim(Geom.Spline(p, closed: true), style) { Tag = MapTags.Region });
            else list.Add(Prim.Polygon(p, style) with { Tag = MapTags.Region });
            var text = o.Text?.Trim() ?? "";
            if (text.Length > 0)
            {
                var b = Rect.FromPoints(p);
                var fs = Geom.FitText(text, 3.0, b.W * 0.8, Math.Max(b.H * 0.5, 1));
                if (fs > 0.3)
                    list.Add(new TextPrim(Centroid(p), text, new TextStyle { Size = fs, Italic = kind == "water", Color = kind == "water" ? Palette.WaterLine : Palette.Ink }) { Tag = MapTags.Label });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            if (p.Count >= 3) yield return Anchor.Point(AnchorRole.Center, Centroid(p), "centroid");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Cycle(o, "map.region.kind", KindDef, new I18nText("Area type", "Flächenart"));
            yield return Prm.Cycle(o, "map.region.fill", FillDef, new I18nText("Fill", "Füllung"));
            yield return Prm.Toggle(o, "map.region.smooth", "smooth", false, new I18nText("Smooth outline", "Glatter Umriss"));
            foreach (var a in PointActions(o, new I18nText("Add corner", "Ecke hinzufügen"), new I18nText("Remove corner", "Ecke entfernen"))) yield return a;
        }
    }

    /// <summary>City / place label: settlement dot by rank plus name; the dot is a docking port.</summary>
    internal static class Place
    {
        public const string TypeId = "map.place";

        public static readonly ParamDef RankDef = Prm.Choice("rank", "Place rank", "Ortsgröße", "city",
            ("capital", "Capital", "Hauptstadt"), ("city", "City", "Stadt"), ("town", "Town", "Ort"), ("village", "Village", "Dorf"), ("locality", "Locality", "Ortsteil"));
        private static readonly ParamDef DotDef = Prm.Bool("dot", "Settlement dot", "Ortspunkt", true);

        private const double W = 34, H = 8, DotX = -W / 2 + 2.2;

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                var rank = o.Str("rank", "city");
                var dot = o.Flag("dot", true) && rank != "locality";
                if (dot)
                {
                    switch (rank)
                    {
                        case "capital":
                            p.Round(DotX, 0, 1.7, S.Filled(Palette.White, S.Stroke(Palette.Ink, 0.35)), MapTags.Dot);
                            p.Round(DotX, 0, 0.85, S.Black, MapTags.Dot);
                            break;
                        case "town": p.Round(DotX, 0, 1.0, S.Black, MapTags.Dot); break;
                        case "village": p.Round(DotX, 0, 0.85, S.Filled(Palette.White, S.Stroke(Palette.Ink, 0.35)), MapTags.Dot); break;
                        default: p.Round(DotX, 0, 1.4, S.Black, MapTags.Dot); break;
                    }
                }
                var x0 = dot ? -W / 2 + 4.8 : -W / 2;
                var text = !string.IsNullOrWhiteSpace(o.Text) ? o.Text!.Trim() : Prm.OptionLabel(RankDef, rank).Get(ctx.Lang);
                var (size, bold, italic) = rank switch
                {
                    "capital" => (4.4, true, false),
                    "town" => (3.4, false, false),
                    "village" => (3.0, false, false),
                    "locality" => (2.8, false, true),
                    _ => (4.0, true, false),
                };
                p.Text(x0, 0, text, size, W / 2 - x0, H, TextAnchor.Start, bold, Palette.Black, italic);
            }
            return new MapBoxType(TypeId, new I18nText("Place label", "Ortsbeschriftung"), MapCategories.Base,
                new[] { "place", "city", "town", "village", "capital", "label", "settlement", "name",
                        "ort", "stadt", "dorf", "hauptstadt", "ortsname", "beschriftung", "siedlung" },
                MapViews.Plan(W, H, Draw) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(DotX, 0), "loc") } })
            {
                Params = new[] { RankDef, DotDef },
                Uniform = false,
                TextEditable = true,
                CompanionIds = new[] { PoiType.TypeId, MarkerType.TypeId, RoadType.TypeId },
                Extra = (o, lang) => new[]
                {
                    Prm.Cycle(o, "map.place.rank", RankDef, new I18nText("Place rank", "Ortsgröße")),
                    Prm.Toggle(o, "map.place.dot", "dot", true, new I18nText("Settlement dot", "Ortspunkt")),
                },
            };
        }
    }
}
