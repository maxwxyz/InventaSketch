using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Road as a point object (centre line): motorway, main road, side road or path. Band roads render as a filled band
    /// (one native polygon) plus their edge lines; where the band of another road overlaps, the edge lines are interrupted,
    /// so crossing and joining roads form clean junctions in any z-order (unless one of them is a bridge). The centre line
    /// is exposed as <see cref="AnchorRole.Rail"/> segments: markers, POIs, stops and route points dock anywhere on it.
    /// </summary>
    internal sealed class RoadType : MapPointType
    {
        public const string TypeId = "map.road";

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Road type", "Straßenart", "main",
            ("motorway", "Motorway", "Autobahn"), ("main", "Main road", "Hauptstraße"), ("side", "Side road", "Nebenstraße"), ("path", "Path", "Weg"));
        private static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curve", "Glatte Kurve", false);
        private static readonly ParamDef BridgeDef = Prm.Bool("bridge", "Bridge (passes over other roads)", "Brücke (über andere Straßen)", false);
        private static readonly ParamDef WidthDef = Prm.Num("width", "Width on the page (empty: by road type)", "Breite auf dem Blatt (leer: nach Straßenart)", 0, 0, 30, "mm", 0.1, advanced: true) with { Default = null };

        private sealed record KindSpec(string Fill, string Casing, double Width);

        private static KindSpec Spec(string kind) => kind switch
        {
            "motorway" => new KindSpec("#f7b267", "#9c5a00", 4.2),
            "side" => new KindSpec("#ffffff", "#8a8a8a", 2.0),
            "path" => new KindSpec("#6d5b4b", "#6d5b4b", 0.45),
            _ => new KindSpec("#ffe9a8", "#a0832a", 3.0),
        };

        public RoadType() : base(TypeId, new I18nText("Road", "Straße"), MapCategories.Lines,
            "road", "street", "motorway", "highway", "freeway", "main road", "side road", "path", "track", "map",
            "straße", "autobahn", "hauptstraße", "nebenstraße", "weg", "pfad", "karte") { }

        public override bool HasText => true;
        public override bool DependsOnNeighbours => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-50, 0), new Vec2(50, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { KindDef, SmoothDef, BridgeDef, WidthDef };
        public override IReadOnlyList<string> Companions => new[] { PoiType.TypeId, MarkerType.TypeId, StopType.TypeId, RouteType.TypeId, Place.TypeId };

        public static string KindOf(SketchObject o) => o.Str("kind", "main");
        private static bool IsBand(SketchObject o) => KindOf(o) != "path";

        /// <summary>Width of the road on the page (mm): the optional width parameter, else the road type's width.</summary>
        public static double WidthOf(SketchObject o)
        {
            var w = o.Num("width", 0);
            return w > 0.05 ? w : Spec(KindOf(o)).Width;
        }

        private static List<Vec2> Line(SketchObject o, IReadOnlyList<Vec2> pts) => Geom.Centerline(pts, o.Flag("smooth", false));

        /// <summary>Bands of other roads on the page that interrupt the edge lines of <paramref name="o"/>.</summary>
        private static List<(List<Vec2> Line, double Hw)> Holes(SketchObject o, RenderContext ctx)
        {
            var list = new List<(List<Vec2>, double)>();
            if (o.Flag("bridge", false) || !IsBand(o) || ctx.Doc.PageOf(o.Id) is null) return list;
            foreach (var other in ObjectsNear(ctx.Doc, o, WidthOf(o) + 15))
            {
                if (other.Type != TypeId || !IsBand(other) || other.Flag("bridge", false)) continue;
                var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(other));
                if (pts.Count < 2) continue;
                list.Add((Line(other, pts), WidthOf(other) / 2));
            }
            return list;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var line = Line(o, p);
            var list = new List<Primitive>();
            if (line.Count < 2) return list;
            var kind = KindOf(o);
            var spec = Spec(kind);
            var text = o.Text?.Trim() ?? "";
            if (kind == "path")
            {
                list.Add(Prim.Polyline(line, S.Stroke(spec.Casing, WidthOf(o), DashStyle.Dash)) with { Tag = MapTags.Track });
                if (text.Length > 0) list.Add(Geom.LineLabel(line, text, WidthOf(o) / 2 + 0.6, 2.2, Palette.Ink, italic: true));
                return list;
            }
            var hw = WidthOf(o) / 2;
            var left = Geom.Offset(line, hw);
            var right = Geom.Offset(line, -hw);
            list.Add(Prim.Polygon(left.Concat(Enumerable.Reverse(right)), S.FillOnly(spec.Fill)) with { Tag = MapTags.Band });
            var holes = Holes(o, ctx);
            bool InHole(Vec2 q) => holes.Any(h => Geom.DistanceToPolyline(q, h.Line) < h.Hw - 1e-6);
            List<List<Vec2>> Cut(List<Vec2> l) => holes.Count == 0 ? new List<List<Vec2>> { l } : Geom.Outside(l, InHole);
            var pieces = Cut(left).Concat(Cut(right)).Cast<IReadOnlyList<Vec2>>().ToList();
            // Free ends get an end line; ends inside another road stay open (junction).
            if (!InHole(line[0])) pieces.Add(new[] { left[0], right[0] });
            if (!InHole(line[line.Count - 1])) pieces.Add(new[] { left[left.Count - 1], right[right.Count - 1] });
            var edges = Geom.MultiPath(pieces, S.Stroke(spec.Casing, 0.35), MapTags.Edge);
            if (edges is not null) list.Add(edges);
            if (kind == "motorway")
            {
                var divider = Geom.MultiPath(Cut(line), S.Stroke(spec.Casing, 0.2), MapTags.Divider);
                if (divider is not null) list.Add(divider);
            }
            if (text.Length > 0)
            {
                if (kind is "motorway" or "main") AddShield(list, line, text, kind == "motorway");
                else list.Add(Geom.LineLabel(line, text, hw + 0.5, 2.2, Palette.Ink));
            }
            return list;
        }

        /// <summary>Road number shield (upright rounded box) at the middle of the road.</summary>
        private static void AddShield(List<Primitive> list, IReadOnlyList<Vec2> line, string text, bool motorway)
        {
            var (mid, _) = Geom.At(line, Geom.Length(line) / 2);
            const double fs = 2.4;
            var w = Geom.TextWidth(text, fs) + 1.6;
            var h = fs + 1.0;
            var style = motorway ? S.Filled("#0d47a1", S.Stroke(Palette.White, 0.3)) : S.Filled("#ffd54f", S.Stroke("#5d4a00", 0.25));
            list.Add(Geom.RoundedRect(mid.X - w / 2, mid.Y - h / 2, w, h, 0.8, style) with { Tag = MapTags.Shield });
            list.Add(new TextPrim(mid, text, new TextStyle { Size = fs, Bold = true, Color = motorway ? Palette.White : Palette.Black }) { Tag = MapTags.Label });
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            return pts.Count < 2 ? Array.Empty<Anchor>() : Geom.Rails(Line(o, pts)).ToList();
        }

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            foreach (var id in base.Dependents(o, ctx)) yield return id;
            if (ctx.Doc.PageOf(o.Id) is null) yield break;
            foreach (var other in ObjectsNear(ctx.Doc, o, WidthOf(o) + 15))
                if (other.Type == TypeId) yield return other.Id;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Cycle(o, "map.road.kind", KindDef, new I18nText("Road type", "Straßenart"));
            yield return Prm.Toggle(o, "map.road.smooth", "smooth", false, new I18nText("Smooth curve", "Glatte Kurve"));
            yield return Prm.Toggle(o, "map.road.bridge", "bridge", false, new I18nText("Bridge", "Brücke"));
            foreach (var a in PointActions(o, new I18nText("Add point", "Punkt hinzufügen"), new I18nText("Remove point", "Punkt entfernen"))) yield return a;
        }
    }

    /// <summary>Rail line (black/white band) or tram line (line with sleepers) as a point object with rails for docking.</summary>
    internal sealed class RailType : MapPointType
    {
        public const string TypeId = "map.rail";

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Line type", "Linienart", "rail",
            ("rail", "Railway", "Eisenbahn"), ("tram", "Tram / light rail", "Straßenbahn / Stadtbahn"));
        private static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curve", "Glatte Kurve", false);

        public RailType() : base(TypeId, new I18nText("Rail line", "Bahnstrecke"), MapCategories.Lines,
            "rail", "railway", "railroad", "train", "tram", "light rail", "track", "map",
            "bahn", "eisenbahn", "bahnstrecke", "gleis", "zug", "straßenbahn", "stadtbahn", "karte") { }

        public override bool HasText => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-50, 0), new Vec2(50, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { KindDef, SmoothDef };
        public override IReadOnlyList<string> Companions => new[] { PoiType.TypeId, StopType.TypeId, MarkerType.TypeId, Place.TypeId };

        private static List<Vec2> Line(SketchObject o, IReadOnlyList<Vec2> pts) => Geom.Centerline(pts, o.Flag("smooth", false));

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var line = Line(o, p);
            var list = new List<Primitive>();
            if (line.Count < 2) return list;
            double half;
            if (o.Str("kind", "rail") == "tram")
            {
                list.Add(Prim.Polyline(line, S.Stroke(Palette.Ink, 0.35)) with { Tag = MapTags.Track });
                var total = Geom.Length(line);
                var ticks = new List<IReadOnlyList<Vec2>>();
                for (var s = 1.0; s < total - 0.5; s += 2.0)
                {
                    var (q, t) = Geom.At(line, s);
                    ticks.Add(new[] { q + t.Perp * 0.8, q - t.Perp * 0.8 });
                }
                var tp = Geom.MultiPath(ticks, S.Stroke(Palette.Ink, 0.25), MapTags.Track);
                if (tp is not null) list.Add(tp);
                half = 0.8;
            }
            else
            {
                list.Add(Prim.Polyline(line, S.Stroke(Palette.Ink, 1.2)) with { Tag = MapTags.Track });
                var dashes = Geom.MultiPath(Geom.Dashes(line, 2.4, 2.4, -1.2), S.Stroke(Palette.White, 0.55), MapTags.Inlay);
                if (dashes is not null) list.Add(dashes);
                half = 0.6;
            }
            var text = o.Text?.Trim() ?? "";
            if (text.Length > 0) list.Add(Geom.LineLabel(line, text, half + 0.5, 2.2, Palette.Ink));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            return pts.Count < 2 ? Array.Empty<Anchor>() : Geom.Rails(Line(o, pts)).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Cycle(o, "map.rail.kind", KindDef, new I18nText("Line type", "Linienart"));
            yield return Prm.Toggle(o, "map.rail.smooth", "smooth", false, new I18nText("Smooth curve", "Glatte Kurve"));
            foreach (var a in PointActions(o, new I18nText("Add point", "Punkt hinzufügen"), new I18nText("Remove point", "Punkt entfernen"))) yield return a;
        }
    }

    /// <summary>River (band with bank and water lines), stream (single line) or canal (straight band) as a point object.</summary>
    internal sealed class RiverType : MapPointType
    {
        public const string TypeId = "map.river";

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Water type", "Gewässerart", "river",
            ("river", "River", "Fluss"), ("stream", "Stream", "Bach"), ("canal", "Canal", "Kanal"));
        private static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curve", "Glatte Kurve", true);
        private static readonly ParamDef WidthDef = Prm.Num("width", "Width on the page (empty: by water type)", "Breite auf dem Blatt (leer: nach Gewässerart)", 0, 0, 40, "mm", 0.1, advanced: true) with { Default = null };

        public RiverType() : base(TypeId, new I18nText("River", "Fluss"), MapCategories.Lines,
            "river", "stream", "brook", "creek", "canal", "water", "waterway", "map",
            "fluss", "bach", "kanal", "gewässer", "wasser", "wasserstraße", "karte") { }

        public override bool HasText => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-50, 8), new Vec2(-18, -6), new Vec2(16, 6), new Vec2(50, -8) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { KindDef, SmoothDef, WidthDef };
        public override IReadOnlyList<string> Companions => new[] { PoiType.TypeId, Place.TypeId, MarkerType.TypeId };

        public static double WidthOf(SketchObject o)
        {
            var w = o.Num("width", 0);
            if (w > 0.05) return w;
            return o.Str("kind", "river") switch { "stream" => 0.6, "canal" => 2.4, _ => 3.0 };
        }

        private static List<Vec2> Line(SketchObject o, IReadOnlyList<Vec2> pts) => Geom.Centerline(pts, o.Flag("smooth", true));

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var line = Line(o, p);
            var list = new List<Primitive>();
            if (line.Count < 2) return list;
            var kind = o.Str("kind", "river");
            var w = WidthOf(o);
            var hw = w / 2;
            if (kind == "stream")
            {
                var smooth = o.Flag("smooth", true) && p.Count >= 3;
                var style = S.Stroke(Palette.WaterLine, w);
                list.Add(smooth ? new PathPrim(Geom.Spline(p), style) { Tag = MapTags.Water } : Prim.Polyline(line, style) with { Tag = MapTags.Water });
            }
            else
            {
                var left = Geom.Offset(line, hw);
                var right = Geom.Offset(line, -hw);
                list.Add(Prim.Polygon(left.Concat(Enumerable.Reverse(right)), S.FillOnly(Palette.Water)) with { Tag = MapTags.Water });
                var canal = kind == "canal";
                var banks = Geom.MultiPath(new IReadOnlyList<Vec2>[] { left, right }, S.Stroke(Palette.WaterLine, canal ? 0.35 : 0.25), MapTags.Edge);
                if (banks is not null) list.Add(banks);
                // Water lines parallel to the banks (classic monochrome water representation).
                var inner = canal
                    ? new IReadOnlyList<Vec2>[] { line }
                    : new IReadOnlyList<Vec2>[] { Geom.Offset(line, hw * 0.45), Geom.Offset(line, -hw * 0.45) };
                var lines = Geom.MultiPath(inner, S.Stroke("#5b9fd6", 0.15), MapTags.Water);
                if (lines is not null) list.Add(lines);
            }
            var text = o.Text?.Trim() ?? "";
            if (text.Length > 0) list.Add(Geom.LineLabel(line, text, hw + 0.5, 2.4, Palette.WaterLine, italic: true));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            return pts.Count < 2 ? Array.Empty<Anchor>() : Geom.Rails(Line(o, pts)).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Cycle(o, "map.river.kind", KindDef, new I18nText("Water type", "Gewässerart"));
            yield return Prm.Toggle(o, "map.river.smooth", "smooth", true, new I18nText("Smooth curve", "Glatte Kurve"));
            foreach (var a in PointActions(o, new I18nText("Add point", "Punkt hinzufügen"), new I18nText("Remove point", "Punkt entfernen"))) yield return a;
        }
    }

    /// <summary>
    /// Route line through waypoints: white halo, coloured line (solid, dashed or dotted), end arrow and direction chevrons,
    /// waypoint dots. Its segments are rails, so markers and stops dock onto the route, and route points dock onto roads
    /// (rails) and onto markers, stops and POIs (ports; the point then follows the symbol).
    /// </summary>
    internal sealed class RouteType : MapPointType
    {
        public const string TypeId = "map.route";

        public static readonly ParamDef StyleDef = Prm.Choice("style", "Line style", "Linienstil", "solid",
            ("solid", "Solid", "Durchgezogen"), ("dashed", "Dashed (alternative)", "Gestrichelt (Alternative)"), ("dotted", "Dotted (walk)", "Gepunktet (Fußweg)"));
        public static readonly ParamDef ArrowsDef = Prm.Choice("arrows", "Direction arrows", "Richtungspfeile", "along",
            ("along", "Along the route", "Entlang der Route"), ("end", "At the end", "Am Ende"), ("none", "None", "Keine"));
        private static readonly ParamDef WaypointsDef = Prm.Bool("waypoints", "Waypoint dots", "Wegpunkte zeigen", true);
        private static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curve", "Glatte Kurve", false);
        private static readonly ParamDef WidthDef = Prm.Num("width", "Line width", "Linienbreite", 1.2, 0.2, 6, "mm", 0.1);

        public RouteType() : base(TypeId, new I18nText("Route", "Route"), MapCategories.Routes,
            "route", "path", "itinerary", "directions", "waypoint", "trip", "journey", "navigation", "map",
            "route", "wegpunkt", "strecke", "fahrtroute", "reiseroute", "navigation", "weg", "karte") { }

        public override bool HasText => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-45, 12), new Vec2(-15, -10), new Vec2(15, 8), new Vec2(45, -12) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { StyleDef, ArrowsDef, WaypointsDef, SmoothDef, WidthDef };
        public override IReadOnlyList<string> Companions => new[] { MarkerType.TypeId, StopType.TypeId, PoiType.TypeId, Callout.TypeId, Place.TypeId };

        public static List<Vec2> Line(SketchObject o, IReadOnlyList<Vec2> pts) => Geom.Centerline(pts, o.Flag("smooth", false));

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var line = Line(o, p);
            var list = new List<Primitive>();
            if (line.Count < 2) return list;
            var w = Geo.Clamp(o.Num("width", 1.2), 0.2, 6);
            var style = o.Str("style", "solid");
            var arrows = o.Str("arrows", "along");
            var smooth = o.Flag("smooth", false) && p.Count >= 3;
            var color = Palette.Route;
            list.Add(Prim.Polyline(line, S.Stroke(Palette.White, w + 1.0)) with { Tag = MapTags.Halo });
            var dash = style switch { "dashed" => DashStyle.Dash, "dotted" => DashStyle.Dot, _ => DashStyle.Solid };
            var main = S.Stroke(color, w, dash);
            if (arrows != "none") main = main with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            list.Add((smooth ? new PathPrim(Geom.Spline(p), main) : Prim.Polyline(p, main)) with { Tag = MapTags.Route });
            if (arrows == "along") AddChevrons(list, line, p, w, style == "solid", color);
            if (o.Flag("waypoints", true))
                for (var i = 1; i + 1 < p.Count; i++)
                    list.Add(Prim.Circle(p[i].X, p[i].Y, 0.45 * w + 0.35, S.Filled(Palette.White, S.Stroke(color, 0.3))) with { Tag = MapTags.Waypoint });
            var text = o.Text?.Trim() ?? "";
            if (text.Length > 0) list.Add(Geom.LineLabel(line, text, w / 2 + 1.0, 2.6, Palette.RouteDark, bold: true));
            return list;
        }

        /// <summary>
        /// Direction chevrons every 20 mm: white inlays inside a solid line, larger chevrons in the route colour over a
        /// dashed or dotted line (strokes only, so a recoloured route recolours them and monochrome keeps them black).
        /// </summary>
        private static void AddChevrons(List<Primitive> list, IReadOnlyList<Vec2> line, IReadOnlyList<Vec2> vertices, double w, bool solid, string color)
        {
            var total = Geom.Length(line);
            const double spacing = 20;
            var count = (int)Math.Floor((total - 8) / spacing);
            if (count < 1) return;
            var start = (total - (count - 1) * spacing) / 2;
            var marks = new List<IReadOnlyList<Vec2>>();
            for (var i = 0; i < count; i++)
            {
                var (q, t) = Geom.At(line, start + i * spacing);
                // Keep clear of bends and waypoint dots.
                var clear = 1.2 * w + 1.5;
                if (vertices.Skip(1).Take(Math.Max(0, vertices.Count - 2)).Any(v => v.DistanceTo(q) < clear)) continue;
                var n = t.Perp;
                if (solid)
                {
                    // Small white triangle inside the line.
                    var a = 0.36 * w;
                    marks.Add(new[] { q + t * a, q - t * (a * 0.8) + n * (a * 0.85), q - t * (a * 0.8) - n * (a * 0.85) });
                }
                else
                {
                    var a = 0.8 * w + 0.5;
                    marks.Add(new[] { q - t * a + n * a, q + t * (a * 0.6), q - t * a - n * a });
                }
            }
            if (marks.Count == 0) return;
            if (solid)
            {
                var segs = new List<PathSeg>();
                foreach (var m in marks) { segs.Add(PathSeg.Move(m[0])); segs.Add(PathSeg.Line(m[1])); segs.Add(PathSeg.Line(m[2])); segs.Add(PathSeg.Close); }
                list.Add(new PathPrim(segs, S.FillOnly(Palette.White)) { Tag = MapTags.Inlay });
            }
            else list.Add(Geom.MultiPath(marks, S.Stroke(color, Math.Max(0.3, 0.45 * w)), MapTags.Arrow)!);
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            return pts.Count < 2 ? Array.Empty<Anchor>() : Geom.Rails(Line(o, pts), 24, "route").ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Cycle(o, "map.route.style", StyleDef, new I18nText("Route style", "Routenstil"));
            yield return Prm.Cycle(o, "map.route.arrows", ArrowsDef, new I18nText("Direction arrows", "Richtungspfeile"));
            yield return Prm.Toggle(o, "map.route.waypoints", "waypoints", true, new I18nText("Waypoint dots", "Wegpunkte zeigen"));
            yield return Prm.Toggle(o, "map.route.smooth", "smooth", false, new I18nText("Smooth curve", "Glatte Kurve"));
            foreach (var a in PointActions(o, new I18nText("Add waypoint", "Wegpunkt hinzufügen"), new I18nText("Remove waypoint", "Wegpunkt entfernen"))) yield return a;
            yield return new ContextAction("map.route.reverse", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Swap, Edits.ReversePoints) { Group = "points" };
        }
    }
}
