using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Station on a line: railway station (white disc with a black ring), interchange (white capsule), tram stop (small
    /// ring) or bus stop (dark badge with a bus pictogram), with the station name above the line to the right of the symbol.
    /// The symbol centre is a docking port, so the station sits anywhere on a rail line, road or route (rail rule) and
    /// route points dock onto it.
    /// </summary>
    internal static class StationType
    {
        public const string TypeId = "map.station";

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Station type", "Stationsart", "rail",
            ("rail", "Railway station", "Bahnhof"), ("interchange", "Interchange", "Umsteigebahnhof"),
            ("tram", "Tram stop", "Straßenbahnhaltestelle"), ("bus", "Bus stop", "Bushaltestelle"));
        private static readonly ParamDef NameDef = Prm.Bool("name", "Show name", "Name zeigen", true);

        private const double W = 34, H = 10, SymX = -W / 2 + 3.6, SymY = 2.4, TextX = SymX + 3.6, TextY = -2.1, TextH = 5.0;

        /// <summary>Draws the station symbol of <paramref name="kind"/> centred at a design point.</summary>
        public static void Symbol(Pen p, string kind, double cx, double cy)
        {
            switch (kind)
            {
                case "interchange":
                    p.RBox(cx - 3.2, cy - 1.9, cx + 3.2, cy + 1.9, 1.9, S.Filled(Palette.White, S.Stroke(Palette.Black, 0.55)), MapTags.Station);
                    break;
                case "tram":
                    p.Round(cx, cy, 1.4, S.Filled(Palette.White, S.Stroke(Palette.Ink, 0.45)), MapTags.Station);
                    p.Round(cx, cy, 0.45, S.Filled(Palette.Ink), MapTags.Station);
                    break;
                case "bus":
                    p.Round(cx, cy, 2.5, S.Filled(Palette.Stop, S.Stroke(Palette.White, 0.35)), MapTags.Station);
                    Glyphs.Draw(p, "transit", p.V(cx, cy), 1.55 * p.K, Palette.White, Palette.Stop);
                    break;
                default:
                    p.Round(cx, cy, 2.0, S.Filled(Palette.White, S.Stroke(Palette.Black, 0.55)), MapTags.Station);
                    break;
            }
        }

        /// <summary>The symbol alone fitted into a legend cell (local coordinates of the legend).</summary>
        public static List<Primitive> Sample(string kind, Rect cell)
        {
            const double d = 8;
            var k = Math.Min(cell.W, cell.H) / d;
            var pen = new Pen(new Size2(d * k, d * k), new Size2(d, d));
            Symbol(pen, kind.Length == 0 ? "rail" : kind, 0, 0);
            var m = Matrix2D.Translation(cell.Center.X, cell.Center.Y);
            return pen.Items.Select(q => Prim.Transform(q, m)).ToList();
        }

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                var kind = o.Str("kind", "rail");
                Symbol(p, kind, SymX, SymY);
                if (!o.Flag("name", true)) return;
                var text = !string.IsNullOrWhiteSpace(o.Text) ? o.Text!.Trim() : Prm.OptionLabel(KindDef, kind).Get(ctx.Lang);
                var bold = kind is "rail" or "interchange";
                p.Text(TextX, TextY, text, 3.4, W / 2 - TextX, TextH, TextAnchor.Start, bold, Palette.Black);
            }
            return new MapBoxType(TypeId, new I18nText("Station", "Station"), MapCategories.Lines,
                new[] { "station", "railway station", "train station", "interchange", "tram stop", "bus stop", "stop", "transit", "map",
                        "station", "bahnhof", "haltestelle", "umsteigebahnhof", "straßenbahnhaltestelle", "bushaltestelle", "halt", "karte" },
                MapViews.Symbol(W, H, Draw) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(SymX, SymY), "loc") } })
            {
                Params = new[] { KindDef, NameDef },
                Uniform = false,
                TextEditable = true,
                CompanionIds = new[] { RailType.TypeId, RouteType.TypeId, Place.TypeId },
                Extra = (o, lang) => new[]
                {
                    Prm.Toggle(o, "map.station.name", "name", true, new I18nText("Show name", "Name zeigen"), IconIds.LabelShow),
                }.Concat(Prm.Choice(o, "map.station.kind", KindDef, new I18nText("Station type", "Stationsart"))),
            };
        }
    }

    /// <summary>
    /// Section of a road (bridge or tunnel) as a point object whose ends dock onto a road. The section follows the centre
    /// line of the road under its end points (also along curves) and takes the road's width; without a road beneath it
    /// spans its own points with the optional width (default: a main road).
    /// </summary>
    internal abstract class RoadSectionType : MapPointType
    {
        private static readonly ParamDef WidthDef = Prm.Num("width", "Road width on the page (empty: from the road)", "Straßenbreite auf dem Blatt (leer: von der Straße)", 0, 0, 30, "mm", 0.1, advanced: true) with { Default = null };

        /// <summary>Default width (mm) without a road beneath: a main road.</summary>
        public const double DefaultWidth = 3.0;

        protected RoadSectionType(string id, I18nText name, params string[] keywords) : base(id, name, MapCategories.Lines, keywords) { }

        public override bool HasText => true;
        /// <summary>Reads the road beneath (centre line, width and colours).</summary>
        public override bool DependsOnNeighbours => true;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-10, 0), new Vec2(10, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { WidthDef };
        public override IReadOnlyList<string> Companions => new[] { RoadType.TypeId, RiverType.TypeId, RailType.TypeId };

        /// <summary>The resolved section: its centre line, width (mm) and the road beneath (null if none).</summary>
        public sealed record Section(List<Vec2> Line, double Width, SketchObject? Road);

        /// <summary>The section of <paramref name="o"/> with the resolved points <paramref name="p"/>.</summary>
        public static Section Resolve(SketchObject o, SketchDocument doc, IReadOnlyList<Vec2> p)
        {
            var own = o.Num("width", 0);
            var a = p[0];
            var b = p[p.Count - 1];
            SketchObject? best = null;
            List<Vec2>? bestLine = null;
            double bestErr = double.MaxValue, s0 = 0, s1 = 0;
            if (doc.PageOf(o.Id) is not null)
            {
                foreach (var road in ObjectsNear(doc, o, 20))
                {
                    if (road.Type != RoadType.TypeId || road.Id == o.Id) continue;
                    var line = RoadType.CenterlineOf(road, doc);
                    if (line.Count < 2) continue;
                    var tol = RoadType.WidthOf(road) / 2 + 1.0;
                    var (sa, da) = Geom.Project(line, a);
                    var (sb, db) = Geom.Project(line, b);
                    if (da > tol || db > tol || Math.Abs(sb - sa) < 0.5 || da + db >= bestErr) continue;
                    best = road; bestLine = line; bestErr = da + db; s0 = sa; s1 = sb;
                }
            }
            if (best is not null && bestLine is not null)
                return new Section(Geom.SubLine(bestLine, s0, s1), own > 0.05 ? own : RoadType.WidthOf(best), best);
            return new Section(Geom.Dedupe(p), own > 0.05 ? own : DefaultWidth, null);
        }

        protected sealed override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var section = Resolve(o, ctx.Doc, p);
            var list = new List<Primitive>();
            if (section.Line.Count < 2 || Geom.Length(section.Line) < 0.05) return list;
            Draw(o, section, list);
            var text = o.Text?.Trim() ?? "";
            if (text.Length > 0) list.Add(Geom.LineLabel(section.Line, text, section.Width / 2 + 1.4, 2.2, Palette.Ink, italic: true));
            return list;
        }

        protected abstract void Draw(SketchObject o, Section s, List<Primitive> list);

        /// <summary>Unit tangent and left normal at the start and the end of a line.</summary>
        protected static (Vec2 T0, Vec2 T1) EndTangents(IReadOnlyList<Vec2> line)
        {
            var t0 = (line[1] - line[0]).Normalized();
            var t1 = (line[line.Count - 1] - line[line.Count - 2]).Normalized();
            return (t0.Length < 0.5 ? new Vec2(1, 0) : t0, t1.Length < 0.5 ? new Vec2(1, 0) : t1);
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            if (pts.Count < 2) yield break;
            var line = Resolve(o, ctx.Doc, pts).Line;
            if (line.Count < 2) yield break;
            yield return Anchor.Point(AnchorRole.End, line[0], "start");
            yield return Anchor.Point(AnchorRole.End, line[line.Count - 1], "end");
        }
    }

    /// <summary>
    /// Bridge on a road: the deck in the road's colour over the section (so the road passes over a river or rail drawn
    /// above it), its edge lines, and parapets just outside the road edges with flared wing ends.
    /// </summary>
    internal sealed class BridgeType : RoadSectionType
    {
        public const string TypeId = "map.bridge";

        public BridgeType() : base(TypeId, new I18nText("Bridge", "Brücke"),
            "bridge", "viaduct", "overpass", "flyover", "crossing", "road", "map",
            "brücke", "viadukt", "überführung", "straßenbrücke", "straße", "karte") { }

        protected override void Draw(SketchObject o, Section s, List<Primitive> list)
        {
            var line = s.Line;
            var hw = s.Width / 2;
            var casing = s.Road is null ? Palette.Ink : RoadType.CasingOf(s.Road);
            var fill = s.Road is null ? Palette.White : RoadType.FillOf(s.Road);
            if (fill is not null)
            {
                var left = Geom.Offset(line, hw);
                var right = Geom.Offset(line, -hw);
                list.Add(Prim.Polygon(left.Concat(Enumerable.Reverse(right)), S.FillOnly(fill)) with { Tag = MapTags.Band });
                var edges = Geom.MultiPath(new IReadOnlyList<Vec2>[] { left, right }, S.Stroke(casing, 0.35), MapTags.Edge);
                if (edges is not null) list.Add(edges);
            }
            // Parapets with wings flaring away from the road at both ends.
            var off = hw + 0.5;
            const double wing = 1.0;
            var (t0, t1) = EndTangents(line);
            var pieces = new List<IReadOnlyList<Vec2>>();
            foreach (var sgn in new[] { 1.0, -1.0 })
            {
                var par = Geom.Offset(line, sgn * off);
                var n0 = t0.Perp * sgn;
                var n1 = t1.Perp * sgn;
                var poly = new List<Vec2> { par[0] - t0 * wing + n0 * wing };
                poly.AddRange(par);
                poly.Add(par[par.Count - 1] + t1 * wing + n1 * wing);
                pieces.Add(poly);
            }
            list.Add(Geom.MultiPath(pieces, S.Stroke(Palette.Ink, 0.4), MapTags.Parapet)!);
        }
    }

    /// <summary>
    /// Tunnel on a road: the road section is covered in the land colour, its edges are drawn dashed and both ends carry
    /// portal marks (a bar across the road with short ears pointing out of the tunnel).
    /// </summary>
    internal sealed class TunnelType : RoadSectionType
    {
        public const string TypeId = "map.tunnel";

        public TunnelType() : base(TypeId, new I18nText("Tunnel", "Tunnel"),
            "tunnel", "underpass", "portal", "road", "map",
            "tunnel", "unterführung", "tunnelportal", "straßentunnel", "straße", "karte") { }

        protected override void Draw(SketchObject o, Section s, List<Primitive> list)
        {
            var line = s.Line;
            var hw = s.Width / 2;
            var casing = s.Road is null ? Palette.Ink : RoadType.CasingOf(s.Road);
            var cover = hw + 0.35;
            list.Add(Prim.Polygon(Geom.Offset(line, cover).Concat(Enumerable.Reverse(Geom.Offset(line, -cover))), S.FillOnly(Palette.Land)) with { Tag = MapTags.Mask });
            var dashes = Geom.Dashes(Geom.Offset(line, hw), 1.0, 0.8).Concat(Geom.Dashes(Geom.Offset(line, -hw), 1.0, 0.8)).Cast<IReadOnlyList<Vec2>>();
            var edges = Geom.MultiPath(dashes, S.Stroke(casing, 0.3), MapTags.Edge);
            if (edges is not null) list.Add(edges);
            var (t0, t1) = EndTangents(line);
            var reach = hw + 0.8;
            const double ear = 0.8;
            IReadOnlyList<Vec2> Portal(Vec2 c, Vec2 t, Vec2 outward)
            {
                var n = t.Perp;
                return new[] { c + n * reach + outward * ear, c + n * reach, c - n * reach, c - n * reach + outward * ear };
            }
            var portals = new[] { Portal(line[0], t0, -t0), Portal(line[line.Count - 1], t1, t1) };
            list.Add(Geom.MultiPath(portals, S.Stroke(Palette.Ink, 0.45), MapTags.Portal)!);
        }
    }

    /// <summary>
    /// Contour line (Höhenlinie) as a point object: a smooth line in the terrain colour, open or closed, with its elevation
    /// written into a gap of the line (upright, along the line). Index contours are drawn thicker.
    /// </summary>
    internal sealed class ContourType : MapPointType
    {
        public const string TypeId = "map.contour";
        public override bool InsertablePoints => true;

        private static readonly ParamDef ElevationDef = Prm.Num("elevation", "Elevation", "Höhe", 100, -11000, 9000, "m", 10);
        private static readonly ParamDef IndexDef = Prm.Bool("index", "Index contour (thicker)", "Zählkurve (kräftiger)", false);
        private static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curve", "Glatte Kurve", true);
        private static readonly ParamDef ClosedDef = Prm.Bool("closed", "Closed (hill or hollow)", "Geschlossen (Kuppe oder Senke)", false);
        private static readonly ParamDef LabelDef = Prm.Bool("label", "Elevation label", "Höhenangabe", true);

        public ContourType() : base(TypeId, new I18nText("Contour line", "Höhenlinie"), MapCategories.Base,
            "contour", "contour line", "elevation", "height", "isoline", "terrain", "hill", "topography", "map",
            "höhenlinie", "isohypse", "höhe", "gelände", "hügel", "kuppe", "topografie", "karte") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-45, 8), new Vec2(-15, -6), new Vec2(15, 6), new Vec2(45, -6) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { ElevationDef, IndexDef, SmoothDef, ClosedDef, LabelDef };
        public override IReadOnlyList<string> Companions => new[] { RegionType.TypeId, PoiType.TypeId, Place.TypeId };
        public override bool IsClosed(SketchObject o) => o.Flag("closed", false);
        protected override int MinPoints => 2;

        /// <summary>The elevation text ("250", "1250"; no unit, as on maps).</summary>
        public static string Label(SketchObject o, Lang lang) => Fmt.Num(o.Num("elevation", 100), lang, 1);

        public static List<Vec2> Line(SketchObject o, IReadOnlyList<Vec2> pts)
        {
            var closed = o.Flag("closed", false) && pts.Count >= 3;
            var line = Geom.Centerline(pts, o.Flag("smooth", true), closed);
            if (closed && line.Count >= 2 && line[0].DistanceTo(line[line.Count - 1]) > 1e-6) line.Add(line[0]);
            return line;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var line = Line(o, p);
            var list = new List<Primitive>();
            if (line.Count < 2) return list;
            var index = o.Flag("index", false);
            var style = S.Stroke(Palette.Contour, index ? 0.5 : 0.25);
            var size = index ? 2.4 : 2.0;
            var text = o.Flag("label", true) ? Label(o, ctx.Lang) : "";
            var total = Geom.Length(line);
            var gap = Geom.TextWidth(text, size) / 2 + 0.6;
            if (text.Length == 0 || total < 4 * gap)
            {
                list.Add(Prim.Polyline(line, style) with { Tag = MapTags.Contour });
                return list;
            }
            var (mid, _) = Geom.At(line, total / 2);
            var pieces = Geom.Outside(line, q => q.DistanceTo(mid) < gap);
            var path = Geom.MultiPath(pieces, style, MapTags.Contour);
            if (path is not null) list.Add(path);
            list.Add(Geom.LineLabel(line, text, 0, size, Palette.Contour, bold: index));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            return pts.Count < 2 ? Array.Empty<Anchor>() : Geom.Rails(Line(o, pts)).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return ContextAction.WithInput("map.contour.elevation", new I18nText("Elevation…", "Höhe…"), IconIds.QtyHeight,
                new I18nText("Elevation in metres (e.g. 250)", "Höhe in Metern (z. B. 250)"), Fmt.Num(o.Num("elevation", 100), lang, 1),
                (x, text) =>
                {
                    var t = (text ?? "").Trim();
                    if (t.EndsWith("m", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 1);
                    return x.WithParam("elevation", Fmt.Parse(t, lang, -11000, 9000));
                }, "m") with { Group = "value" };
            yield return Prm.Toggle(o, "map.contour.index", "index", false, new I18nText("Index contour", "Zählkurve"));
            yield return Prm.Toggle(o, "map.contour.label", "label", true, new I18nText("Elevation label", "Höhenangabe"), IconIds.LabelShow);
            yield return Prm.Toggle(o, "map.contour.smooth", "smooth", true, new I18nText("Smooth curve", "Glatte Kurve"), IconIds.Smooth);
        }
    }
}
