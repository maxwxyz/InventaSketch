using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Measurements on a map sketch (2026-11-26, round 7): area and perimeter of a region, the straight-line distance and
    /// bearing between two points (distance measure), the ascent and descent of a route from the contour lines it crosses
    /// and the elevation profile drawn from them, and the scale of the scale bar against the document plan scale. Lengths
    /// come from the drawing at the map scale (<see cref="MapScale"/>) unless they are entered.
    /// </summary>
    internal static class MapMeasure
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static string F(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

        internal static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        /// <summary>A distance for a label: metres below 1 km, else kilometres ("850 m", "2.4 km"); miles are not offered here.</summary>
        public static string Distance(double metres, Lang lang)
            => metres < 1000 ? Fmt.Num(Math.Round(metres, metres < 10 ? 1 : 0), lang, 1) + " m" : Fmt.Num(metres / 1000, lang, metres < 10000 ? 2 : 1) + " km";

        /// <summary>Map objects of the page of <paramref name="o"/> that match <paramref name="pick"/> (empty when off a page).</summary>
        public static IEnumerable<SketchObject> OnPage(SketchObject o, SketchDocument doc, Func<SketchObject, bool> pick)
        {
            var page = doc.PageOf(o.Id);
            return page is null ? Enumerable.Empty<SketchObject>() : doc.ObjectsOn(page).Where(x => x.Id != o.Id && pick(x));
        }

        /// <summary>Objects that redraw when a scale bar changes: routes and regions showing values, distance measures, profiles.</summary>
        public static IEnumerable<string> ScaleReaders(SketchObject bar, RenderContext ctx)
            => OnPage(bar, ctx.Doc, x => (x.Type is RouteType.TypeId or RegionType.TypeId && ValueLabels.Shows(x)) || x.Type is MeasureType.TypeId or Profile.TypeId)
                .Select(x => x.Id);

        /// <summary>Objects that redraw when a route or a contour line changes: profiles, and routes showing values (for a contour).</summary>
        public static IEnumerable<string> ElevationReaders(SketchObject o, RenderContext ctx)
            => OnPage(o, ctx.Doc, x => x.Type == Profile.TypeId || (o.Type == ContourType.TypeId && x.Type == RouteType.TypeId && ValueLabels.Shows(x)))
                .Select(x => x.Id);

        /// <summary>A link relation that sets <paramref name="key"/> to a value read from the drawing.</summary>
        public static BoundRelation Drawn(string id, I18nText name, string key, string expr, double value, string objectId)
            => BoundRelation.Local(new Relation(id, name, "{" + key + "} = " + expr, key).Solve(key, expr, _ => value), objectId);
    }

    // ============================================================================================ region area

    /// <summary>
    /// Area <c>A</c> and perimeter <c>U</c> of a region from its drawn outline at the map scale (the smoothed outline when
    /// it is smoothed), unless entered. Without a scale both stay unknown.
    /// </summary>
    internal static class RegionValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText AreaName = T("Area (drawn outline at the map scale, or entered)", "Fläche (gezeichneter Umriss im Kartenmaßstab oder eingegeben)");
        private static readonly I18nText PerimeterName = T("Perimeter (drawn outline at the map scale, or entered)", "Umfang (gezeichneter Umriss im Kartenmaßstab oder eingegeben)");

        public static readonly IReadOnlyList<VarDef> Vars = new VarDef[]
        {
            new("A", AreaName, T("A", "A"), Quantities.Area) { Unit = "km²", Param = "area", Min = 0, Max = 1e14 },
            new("U", PerimeterName, T("U", "U"), Quantities.Length) { Unit = "km", Param = "perimeter", Min = 0, Max = 1e8 },
        };

        public static readonly IReadOnlyList<ParamDef> Params = new[]
        {
            MapMeasure.Text("area", AreaName, "km²"), MapMeasure.Text("perimeter", PerimeterName, "km"), MapScale.Def, ValueLabels.ShowValues,
        };

        /// <summary>The drawn outline (closed; the sampled spline when smoothed).</summary>
        public static IReadOnlyList<Vec2> Outline(SketchObject o, IReadOnlyList<Vec2> p)
        {
            if (!o.Flag("smooth", false) || p.Count < 3) return p;
            var line = Geom.Sample(Geom.Spline(p, closed: true), 16);
            if (line.Count > 1 && line[0].DistanceTo(line[line.Count - 1]) < 1e-9) line.RemoveAt(line.Count - 1);
            return line;
        }

        /// <summary>Drawn area (mm²) and perimeter (mm) of the outline.</summary>
        public static (double Area, double Perimeter) Drawn(SketchObject o, IReadOnlyList<Vec2> pts)
        {
            var p = Geom.Dedupe(pts);
            if (p.Count < 3) return (0, 0);
            var outline = Outline(o, p);
            var closed = outline.Concat(new[] { outline[0] }).ToList();
            return (Math.Abs(Geo.PolygonArea(outline)), Geom.Length(closed));
        }

        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var scale = MapScale.Of(o, ctx.Doc);
            if (scale is null) yield break;
            var (a, u) = Drawn(o, ctx.Doc.ResolvePoints(o));
            if (!(a > 0)) yield break;
            var k = scale.MetresPerMm;
            if (ValueModel.ReadEntered(o, Vars[0]).Value is null)
                yield return MapMeasure.Drawn("map.region.area", T("Area from the drawing at the map scale", "Fläche aus der Zeichnung im Kartenmaßstab"), "A",
                    MapMeasure.F(a, "0.#") + " mm² · (" + MapMeasure.F(k, "0.####") + " m/mm)²", a * k * k, o.Id);
            if (ValueModel.ReadEntered(o, Vars[1]).Value is null)
                yield return MapMeasure.Drawn("map.region.perimeter", T("Perimeter from the drawing at the map scale", "Umfang aus der Zeichnung im Kartenmaßstab"), "U",
                    MapMeasure.F(u, "0.#") + " mm · " + MapMeasure.F(k, "0.####") + " m/mm", u * k, o.Id);
        }
    }

    // ============================================================================================ distance measure

    /// <summary>
    /// Distance measure (point object): a dashed line with arrowheads between two points (they dock on markers, POIs and
    /// place dots and follow them) labelled with the straight-line distance at the map scale; optionally the bearing from
    /// north (the page's top, or the direction of a rotated north arrow on the page). Values: distance <c>d</c> (drawn unless
    /// entered) and bearing <c>θ</c> (read from the drawing).
    /// </summary>
    internal sealed class MeasureType : MapPointType, IValueType, IValueLinks
    {
        public const string TypeId = "map.measure";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef BearingDef = Prm.Bool("showBearing", "Show bearing", "Richtung (Kurswinkel) zeigen", false);
        private static readonly I18nText DistanceName = T("Straight-line distance (drawn at the map scale, or entered)", "Luftlinie (gezeichnet im Kartenmaßstab oder eingegeben)");
        private static readonly I18nText BearingName = T("Bearing (clockwise from north)", "Kurswinkel (von Norden im Uhrzeigersinn)");

        public static readonly IReadOnlyList<VarDef> Vars = new VarDef[]
        {
            new("d", DistanceName, T("d", "d"), Quantities.Length) { Unit = "km", Param = "distance", Min = 0, Max = 1e8, OnTypeLabel = true },
            new("theta", BearingName, T("θ", "θ"), Quantities.Angle) { Unit = "°", Param = "azimuth", Min = 0, Max = 2 * Math.PI, ReadOnly = true },
        };

        public MeasureType() : base(TypeId, T("Distance measure", "Entfernungsmessung"), MapCategories.Routes,
            "distance", "measure", "straight line", "as the crow flies", "bearing", "azimuth", "ruler",
            "entfernung", "messen", "luftlinie", "kurswinkel", "peilung", "lineal") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, 0), new Vec2(30, 0) };
        public override bool HasText => true;
        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            BearingDef, MapMeasure.Text("distance", DistanceName, "km"), MapMeasure.Text("azimuth", BearingName, "°"), MapScale.Def, ValueLabels.ShowValues,
        };
        public override IReadOnlyList<string> Companions => new[] { MarkerType.TypeId, PoiType.TypeId, Place.TypeId, ScaleBar.TypeId };

        /// <summary>The label reads the scale bar and the north arrow of the page.</summary>
        public override bool DependsOnNeighbours => true;

        public IReadOnlyList<VarDef> Variables => Vars;
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();

        /// <summary>The bearing (radians, clockwise from north, 0 … 2π) of the direction from the first to the last point.</summary>
        public static double Bearing(Vec2 a, Vec2 b, SketchObject o, SketchDocument doc)
        {
            var north = MapMeasure.OnPage(o, doc, x => x.Type == NorthArrow.TypeId).FirstOrDefault();
            var r = Geo.DegToRad(north?.Transform.Rotation ?? 0);
            var n = new Vec2(Math.Sin(r), -Math.Cos(r));
            var e = new Vec2(Math.Cos(r), Math.Sin(r));
            var d = b - a;
            var th = Math.Atan2(d.Dot(e), d.Dot(n));
            return th < 0 ? th + 2 * Math.PI : th;
        }

        /// <summary>Distance in metres: the entered one, else the drawn length at the map scale; null without either.</summary>
        public static double? Metres(SketchObject o, SketchDocument doc, IReadOnlyList<Vec2> p)
        {
            if (ValueModel.ReadEntered(o, Vars[0]).Value is double d) return d;
            var scale = MapScale.Of(o, doc);
            return scale is null || p.Count < 2 ? null : p[0].DistanceTo(p[p.Count - 1]) * scale.MetresPerMm;
        }

        /// <summary>The label: the text, else the distance (and bearing); "no scale" without a scale.</summary>
        public static string LabelOf(SketchObject o, SketchDocument doc, IReadOnlyList<Vec2> p, Lang lang)
        {
            if (!string.IsNullOrWhiteSpace(o.Text)) return o.Text!.Trim();
            var m = Metres(o, doc, p);
            var text = m is double v ? MapMeasure.Distance(v, lang) : lang == Lang.De ? "ohne Maßstab" : "no scale";
            if (o.Flag("showBearing", false) && p.Count >= 2)
                text += " · " + Math.Round(Geo.RadToDeg(Bearing(p[0], p[p.Count - 1], o, doc))).ToString("000", CultureInfo.InvariantCulture) + "°";
            return text;
        }

        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            var p = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            if (p.Count < 2) yield break;
            var a = p[0]; var b = p[p.Count - 1];
            var mm = a.DistanceTo(b);
            if (mm < 1e-6) yield break;
            if (ValueModel.ReadEntered(o, Vars[0]).Value is null && MapScale.Of(o, ctx.Doc) is { } scale)
                yield return MapMeasure.Drawn("map.measure.distance", T("Distance from the drawing at the map scale", "Entfernung aus der Zeichnung im Kartenmaßstab"), "d",
                    MapMeasure.F(mm, "0.#") + " mm · " + MapMeasure.F(scale.MetresPerMm, "0.####") + " m/mm", mm * scale.MetresPerMm, o.Id);
            var th = Bearing(a, b, o, ctx.Doc);
            yield return MapMeasure.Drawn("map.measure.bearing", T("Bearing from the drawing (north: page top or north arrow)", "Kurswinkel aus der Zeichnung (Norden: Blattoberkante oder Nordpfeil)"), "theta",
                MapMeasure.F(Geo.RadToDeg(th), "0.#") + "°", th, o.Id);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var list = new List<Primitive>();
            if (a.DistanceTo(b) < 0.5) return list;
            var d = (b - a).Normalized();
            var n = d.Perp;
            var style = S.Stroke(Palette.Ink, 0.3, DashStyle.Dash) with { ArrowStart = ArrowHead.Triangle, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            list.Add(new LinePrim(a, b, style) { Tag = MapTags.Route });
            foreach (var q in new[] { a, b }) list.Add(new LinePrim(q - n * 1.6, q + n * 1.6, S.Stroke(Palette.Ink, 0.3)) { Tag = MapTags.Tick });
            var text = LabelOf(o, ctx.Doc, p, ctx.Lang);
            var line = new[] { a, b };
            list.Add(Geom.LineLabel(line, text, 0.8, 2.6, Palette.Ink, bold: true));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Toggle(o, "map.measure.bearing", "showBearing", false, new I18nText("Show bearing", "Kurswinkel zeigen"));
            yield return new ContextAction("map.measure.reverse", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, Edits.ReversePoints) { Group = "points" };
        }
    }

    // ============================================================================================ elevation from contours

    /// <summary>
    /// Elevations along a route from the contour lines of its page: every crossing of the route's centre line with a contour
    /// gives the contour's elevation at that arc length. Ascent and descent sum the rises and falls between successive
    /// crossings (at least that much: a hill top between two crossings of the same contour is not counted), the steepest
    /// gradient divides the elevation step by the distance between the crossings at the map scale.
    /// </summary>
    internal static class Elevation
    {
        private static I18nText T(string en, string de) => new(en, de);

        public sealed record Crossing(double S, double Elevation, string ContourId);

        /// <summary>The crossings of the route with the contours of its page, ordered along the route (S in page mm).</summary>
        public static List<Crossing> Crossings(SketchObject route, SketchDocument doc)
        {
            var list = new List<Crossing>();
            var rp = Geom.Dedupe(doc.ResolvePoints(route));
            if (rp.Count < 2) return list;
            var line = RouteType.Line(route, rp);
            foreach (var c in MapMeasure.OnPage(route, doc, x => x.Type == ContourType.TypeId))
            {
                var cp = Geom.Dedupe(doc.ResolvePoints(c));
                if (cp.Count < 2) continue;
                var cl = ContourType.Line(c, cp);
                var e = c.Num("elevation", 100);
                double acc = 0;
                for (var i = 0; i + 1 < line.Count; i++)
                {
                    for (var j = 0; j + 1 < cl.Count; j++)
                        if (Geo.SegmentIntersection(line[i], line[i + 1], cl[j], cl[j + 1]) is { } x)
                            list.Add(new Crossing(acc + line[i].DistanceTo(x), e, c.Id));
                    acc += line[i].DistanceTo(line[i + 1]);
                }
            }
            list.Sort((x, y) => x.S.CompareTo(y.S));
            // A crossing exactly at a vertex of either line is found twice.
            var clean = new List<Crossing>();
            foreach (var x in list)
                if (clean.Count == 0 || Math.Abs(clean[clean.Count - 1].S - x.S) > 1e-6 || clean[clean.Count - 1].ContourId != x.ContourId) clean.Add(x);
            return clean;
        }

        /// <summary>Total drawn length of the route's centre line (page mm; the arc lengths of the crossings refer to it).</summary>
        public static double LineLength(SketchObject route, SketchDocument doc)
        {
            var rp = Geom.Dedupe(doc.ResolvePoints(route));
            return rp.Count < 2 ? 0 : Geom.Length(RouteType.Line(route, rp));
        }

        public static (double Up, double Down) UpDown(IReadOnlyList<Crossing> c)
        {
            double up = 0, down = 0;
            for (var i = 0; i + 1 < c.Count; i++)
            {
                var de = c[i + 1].Elevation - c[i].Elevation;
                if (de > 0) up += de; else down -= de;
            }
            return (up, down);
        }

        /// <summary>The steepest gradient between successive crossings with different elevations (ratio), or null.</summary>
        public static double? Steepest(IReadOnlyList<Crossing> c, double metresPerMm)
        {
            double? best = null;
            for (var i = 0; i + 1 < c.Count; i++)
            {
                var de = Math.Abs(c[i + 1].Elevation - c[i].Elevation);
                var ds = (c[i + 1].S - c[i].S) * metresPerMm;
                if (de <= 0 || ds <= 1e-9) continue;
                var g = de / ds;
                if (best is null || g > best) best = g;
            }
            return best;
        }

        private static readonly I18nText UpName = T("Ascent (from the contour lines crossed)", "Anstieg (aus den gekreuzten Höhenlinien)");
        private static readonly I18nText DownName = T("Descent (from the contour lines crossed)", "Abstieg (aus den gekreuzten Höhenlinien)");
        private static readonly I18nText GradeName = T("Steepest gradient between contour lines", "Größte Steigung zwischen Höhenlinien");

        public static readonly IReadOnlyList<VarDef> Vars = new VarDef[]
        {
            new("up", UpName, T("Δh↑", "Δh↑"), Quantities.Length) { Unit = "m", Param = "ascent", Relevance = VarRelevance.Panel, Min = 0, Max = 1e5, ReadOnly = true },
            new("down", DownName, T("Δh↓", "Δh↓"), Quantities.Length) { Unit = "m", Param = "descent", Relevance = VarRelevance.Panel, Min = 0, Max = 1e5, ReadOnly = true },
            new("gmax", GradeName, T("q_max", "q_max"), Quantities.Ratio) { Unit = "%", Param = "gradeMax", Relevance = VarRelevance.Panel, Min = 0, Max = 100, ReadOnly = true },
        };

        public static readonly IReadOnlyList<ParamDef> Params = new[]
        {
            MapMeasure.Text("ascent", UpName, "m"), MapMeasure.Text("descent", DownName, "m"), MapMeasure.Text("gradeMax", GradeName, "%"),
        };

        /// <summary>Ascent, descent and steepest gradient of a route that crosses at least two contour lines.</summary>
        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var c = Crossings(o, ctx.Doc);
            if (c.Count < 2) yield break;
            var (up, down) = UpDown(c);
            var chain = string.Join(" → ", c.Select(x => MapMeasure.F(x.Elevation, "0.#")));
            yield return MapMeasure.Drawn("map.route.ascent", T("Ascent from the contour lines crossed", "Anstieg aus den gekreuzten Höhenlinien"), "up", "Σ rises (" + chain + " m)", up, o.Id);
            yield return MapMeasure.Drawn("map.route.descent", T("Descent from the contour lines crossed", "Abstieg aus den gekreuzten Höhenlinien"), "down", "Σ falls (" + chain + " m)", down, o.Id);
            if (MapScale.Of(o, ctx.Doc) is { } scale && Steepest(c, scale.MetresPerMm) is double g)
                yield return MapMeasure.Drawn("map.route.gradient", T("Steepest gradient between contour lines", "Größte Steigung zwischen Höhenlinien"), "gmax",
                    "max |Δh|/Δs", g, o.Id);
        }
    }

    // ============================================================================================ elevation profile

    /// <summary>
    /// Elevation profile (box): elevation over the distance along a route, drawn from the contour lines the route crosses.
    /// It shows the route named in its <c>route</c> parameter, else the first route of the page that crosses two contour
    /// lines. Without such a route it draws the empty axes with a hint.
    /// </summary>
    internal static class Profile
    {
        public const string TypeId = "map.profile";

        private static readonly ParamDef RouteDef = Prm.Text("route", "Route (object id; empty: the first route across contour lines)", "Route (Objekt-ID; leer: die erste Route über Höhenlinien)", "") with { Default = null, Advanced = true };
        private static readonly ParamDef FillDef = Prm.Bool("fill", "Fill under the profile", "Fläche unter dem Profil füllen", true);

        /// <summary>The route the profile shows (see the type description), or null.</summary>
        public static SketchObject? RouteOf(SketchObject o, SketchDocument doc)
        {
            var id = o.Str("route", "").Trim();
            if (id.Length > 0 && doc.Get(id) is { Type: RouteType.TypeId } named) return named;
            var routes = MapMeasure.OnPage(o, doc, x => x.Type == RouteType.TypeId).ToList();
            return routes.FirstOrDefault(r => Elevation.Crossings(r, doc).Count >= 2) ?? routes.FirstOrDefault();
        }

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                double hw = p.W / 2, hh = p.H / 2;
                var de = ctx.Lang == Lang.De;
                var fs = Math.Min(2.4, Math.Min(p.W, p.H) * 0.09);
                var left = -hw + fs * 4.2;
                var right = hw - fs * 0.8;
                var top = -hh + fs * 1.9;
                var bottom = hh - fs * 1.8;
                var axis = S.Stroke(Palette.Ink, 0.3);
                p.Add(Prim.Polyline(new[] { new Vec2(left, top), new Vec2(left, bottom), new Vec2(right, bottom) }, axis), MapTags.Frame);
                p.TextAt(new Vec2(-hw, -hh + fs * 0.7), de ? "Höhe [m]" : "Elevation [m]", fs * 0.9, p.W * 0.5, fs * 1.2, TextAnchor.Start, color: Palette.Ink);
                var route = RouteOf(o, ctx.Doc);
                var c = route is null ? new List<Elevation.Crossing>() : Elevation.Crossings(route, ctx.Doc);
                if (c.Count < 2)
                {
                    p.TextAt(new Vec2((left + right) / 2, (top + bottom) / 2), de ? "Route über Höhenlinien zeichnen" : "Draw a route across contour lines",
                        fs, right - left - 1, fs * 1.4, color: Palette.Ink, italic: true);
                    return;
                }
                var total = Math.Max(1e-6, Elevation.LineLength(route!, ctx.Doc));
                var lo = c.Min(x => x.Elevation);
                var hi = c.Max(x => x.Elevation);
                if (hi - lo < 1e-9) { lo -= 1; hi += 1; }
                var pad = (hi - lo) * 0.12;
                double X(double s) => left + (right - left) * Geo.Clamp(s / total, 0, 1);
                double Y(double e) => bottom - (bottom - top) * (e - (lo - pad)) / (hi - lo + 2 * pad);
                var pts = c.Select(x => new Vec2(X(x.S), Y(x.Elevation))).ToList();
                if (o.Flag("fill", true))
                {
                    var poly = new List<Vec2> { new(pts[0].X, bottom) };
                    poly.AddRange(pts);
                    poly.Add(new Vec2(pts[pts.Count - 1].X, bottom));
                    p.Add(Prim.Polygon(poly, S.FillOnly("#efe3cf")), "area");
                }
                p.Add(Prim.Polyline(pts, S.Stroke(Palette.Contour, 0.5)), "profile");
                foreach (var q in pts) p.Add(Prim.Circle(q.X, q.Y, 0.45, S.Filled(Palette.White, S.Stroke(Palette.Contour, 0.3))), "point");
                // elevation ticks (lowest and highest contour) and the route length at the end of the distance axis
                foreach (var e in new[] { lo, hi }.Distinct())
                {
                    var y = Y(e);
                    p.Add(Prim.Line(new Vec2(left - 0.8, y), new Vec2(left, y), axis), MapTags.Tick);
                    p.TextAt(new Vec2(left - 1.1, y), Fmt.Num(e, ctx.Lang, 1), fs, left + hw - 1.2, fs * 1.2, TextAnchor.End, color: Palette.Ink);
                }
                var scale = MapScale.Of(route!, ctx.Doc);
                var end = scale is null ? "" : MapMeasure.Distance(total * scale.MetresPerMm, ctx.Lang);
                p.TextAt(new Vec2(left, bottom + fs * 0.95), "0", fs, fs * 3, fs * 1.2, TextAnchor.Start, color: Palette.Ink);
                if (end.Length > 0) p.TextAt(new Vec2(right, bottom + fs * 0.95), end, fs, (right - left) * 0.45, fs * 1.2, TextAnchor.End, color: Palette.Ink);
                var (up, down) = Elevation.UpDown(c);
                p.TextAt(new Vec2(right, -hh + fs * 0.7), "↑ " + Fmt.Num(up, ctx.Lang, 0) + " m  ↓ " + Fmt.Num(down, ctx.Lang, 0) + " m", fs * 0.9, p.W * 0.45, fs * 1.2,
                    TextAnchor.End, color: Palette.Ink, tag: MapTags.Label);
            }
            return new MapBoxType(TypeId, new I18nText("Elevation profile", "Höhenprofil"), MapCategories.Routes,
                new[] { "elevation profile", "height profile", "profile", "ascent", "descent", "gradient", "contour", "route", "hiking",
                        "höhenprofil", "profil", "anstieg", "abstieg", "steigung", "höhenlinie", "wandern" },
                MapViews.Plan(80, 40, Draw))
            {
                Params = new[] { RouteDef, FillDef },
                Uniform = false,
                NeedsNeighbours = true,
                CompanionIds = new[] { RouteType.TypeId, ContourType.TypeId, ScaleBar.TypeId },
                Extra = (o, lang) => new[] { Prm.Toggle(o, "map.profile.fill", "fill", true, new I18nText("Fill under the profile", "Fläche füllen")) },
            };
        }
    }

    // ============================================================================================ scale bar values

    /// <summary>
    /// The scale bar with values: the map scale 1:N it stands for (represented length over the drawn width) and, when the
    /// document has a plan scale, that scale; the check <c>map.scale.plan</c> notes when the two differ by more than 2 %
    /// (the scale bar wins for map lengths, see <see cref="MapScale"/>).
    /// </summary>
    internal sealed class ScaleBarType : MapBoxType, IValueType, IValueLinks, IValueChecks
    {
        private static I18nText T(string en, string de) => new(en, de);

        public ScaleBarType(string id, I18nText name, string category, string[] keywords, params MapView[] views) : base(id, name, category, keywords, views) { }

        private static readonly I18nText BarName = T("Map scale 1:N of the scale bar", "Kartenmaßstab 1:N der Maßstabsleiste");
        private static readonly I18nText PlanName = T("Plan scale 1:N of the document", "Planmaßstab 1:N des Dokuments");

        public static readonly IReadOnlyList<VarDef> Vars = new VarDef[]
        {
            new("N", BarName, T("N", "N"), Quantities.Ratio) { Unit = "", Param = "mapScale", Min = 1, Max = 1e10, ReadOnly = true },
            new("Np", PlanName, T("N_plan", "N_Plan"), Quantities.Ratio) { Unit = "", Param = "planScale", Relevance = VarRelevance.Panel, Min = 1, Max = 1e10, ReadOnly = true },
        };

        public static readonly IReadOnlyList<ParamDef> ValueParams = new[]
        {
            MapMeasure.Text("mapScale", BarName, ""), MapMeasure.Text("planScale", PlanName, ""), ValueLabels.ShowValues,
        };

        public IReadOnlyList<VarDef> Variables => Vars;
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();

        /// <summary>The scale denominator N of a scale bar (represented metres over drawn metres), or null for a zero width.</summary>
        public static double? Denominator(SketchObject bar)
        {
            var w = bar.Size.W;
            if (!(w > 1e-6)) return null;
            return ScaleBar.LengthOf(bar) * MapScale.MetresPer(ScaleBar.UnitOf(bar)) / (w / 1000);
        }

        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            if (Denominator(o) is double n)
                yield return MapMeasure.Drawn("map.scalebar.scale", T("Map scale from the represented length and the drawn width", "Kartenmaßstab aus dargestellter Länge und gezeichneter Breite"), "N",
                    MapMeasure.F(ScaleBar.LengthOf(o), "0.###") + " " + ScaleBar.UnitOf(o) + " / " + MapMeasure.F(o.Size.W, "0.#") + " mm", n, o.Id);
            if (ctx.Doc.Settings.PlanScale is double s && s >= 1)
                yield return MapMeasure.Drawn("map.scalebar.plan", T("Plan scale of the document", "Planmaßstab des Dokuments"), "Np", "1:" + MapMeasure.F(s, "0"), s, o.Id);
        }

        /// <summary>map.scale.plan: the scale bar and the document plan scale differ by more than 2 %.</summary>
        public static readonly ValueCheck PlanMismatch = new("map.scale.plan", CheckSeverity.Hint,
            T("The scale bar stands for a different scale than the document plan scale. Map lengths follow the scale bar; adjust its length or width, or the plan scale, so both agree.",
              "Die Maßstabsleiste steht für einen anderen Maßstab als der Planmaßstab des Dokuments. Kartenlängen folgen der Maßstabsleiste; ihre Länge oder Breite oder den Planmaßstab anpassen, damit beide übereinstimmen."),
            "|N/N_plan − 1| ≤ 2 %", c => c["N"] is double n && c["Np"] is double np && np > 0 && Math.Abs(n / np - 1) > 0.02);

        public IEnumerable<ValueCheck> ValueChecks => new[] { PlanMismatch };
    }
}
