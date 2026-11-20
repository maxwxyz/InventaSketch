using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Route itinerary as CSV: for every selected route its start, the stops along it (route points that carry a marker,
    /// numbered stop, POI or place label, docked or within 3 mm) and its end, in route order, with leg and cumulative
    /// distances measured along the drawn route. Distances are converted with the scale bar on the page (its width
    /// represents its length parameter), otherwise they are millimetres on the page. Sketch maps have no geographic
    /// coordinates, so a GPX file would have to invent them; this export deliberately does not.
    /// </summary>
    internal sealed class RouteStopsExporter : IExporter
    {
        private const double MatchDistance = 3.0;

        public string Id => "map.route.stops";
        public I18nText Name => new("Route stops (CSV)", "Routenhalte (CSV)");
        public string Extension => "csv";
        public string MimeType => "text/csv";
        public I18nText Preserves => new(
            "Order of start, stops and destination with the names of the markers, stops and POIs on the route; leg and total distances along the drawn route (scale bar units, else mm on the page). No geographic coordinates.",
            "Reihenfolge von Start, Halten und Ziel mit den Namen der Marker, Halte und POIs auf der Route; Abschnitts- und Gesamtentfernungen entlang der gezeichneten Route (Einheit der Maßstabsleiste, sonst mm auf dem Blatt). Keine geografischen Koordinaten.");

        private static bool IsRoute(SketchObject o) => o.Type == RouteType.TypeId && o.Points is { Count: >= 2 };
        private static bool IsSymbol(SketchObject o) => o.Type is MarkerType.TypeId or StopType.TypeId or PoiType.TypeId or Place.TypeId;

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects) => objects.Any(IsRoute);

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var routes = objects.Where(IsRoute).ToList();
            var page = routes.Select(r => doc.PageOf(r.Id)).FirstOrDefault(p => p is not null);
            var pageObjects = page is null ? objects.ToList() : doc.ObjectsOn(page).ToList();
            var scale = objects.FirstOrDefault(o => o.Type == ScaleBar.TypeId) ?? pageObjects.FirstOrDefault(o => o.Type == ScaleBar.TypeId);
            var unit = scale is null ? "mm" : ScaleBar.UnitOf(scale);
            var mmPerUnit = scale is null ? 1.0 : Math.Max(1e-9, scale.Size.W) / ScaleBar.LengthOf(scale);
            var ctx = new RenderContext(doc, lang);
            var symbols = pageObjects.Where(IsSymbol).Concat(objects.Where(IsSymbol)).GroupBy(o => o.Id).Select(g => g.First())
                .Select(o => (Obj: o, Loc: registry.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port).Select(a => a.P).DefaultIfEmpty(new Vec2(o.Transform.X, o.Transform.Y)).First()))
                .ToList();

            var sb = new StringBuilder();
            sb.Append(lang == Lang.De
                ? $"Route,Nr,Name,Art,Abschnitt ({unit}),Gesamt ({unit}),X (mm),Y (mm)"
                : $"Route,No,Name,Kind,Leg ({unit}),Total ({unit}),X (mm),Y (mm)").Append("\r\n");
            var routeNo = 0;
            foreach (var r in routes)
            {
                routeNo++;
                var routeName = !string.IsNullOrWhiteSpace(r.Text) ? r.Text!.Trim() : "Route " + Geom.I(routeNo);
                var pts = doc.ResolvePoints(r);
                var seg = SegmentLengths(r, pts);
                var no = 0;
                double sinceLast = 0, total = 0;
                for (var i = 0; i < pts.Count; i++)
                {
                    if (i > 0) { sinceLast += seg[i - 1]; total += seg[i - 1]; }
                    var sym = Match(r, i, pts[i], symbols);
                    if (sym is null && i != 0 && i != pts.Count - 1) continue;
                    no++;
                    var name = sym is null
                        ? (i == 0 ? "Start" : (lang == Lang.De ? "Ziel" : "Destination"))
                        : SymbolName(sym, lang);
                    var kind = sym is null ? "" : SymbolKind(registry, sym, lang);
                    sb.Append(Exports.Csv(routeName)).Append(',').Append(Geom.I(no)).Append(',').Append(Exports.Csv(name)).Append(',').Append(Exports.Csv(kind)).Append(',')
                      .Append(F(i == 0 ? 0 : sinceLast / mmPerUnit)).Append(',').Append(F(total / mmPerUnit)).Append(',')
                      .Append(F(pts[i].X)).Append(',').Append(F(pts[i].Y)).Append("\r\n");
                    sinceLast = 0;
                }
            }
            return sb.ToString();
        }

        private static string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Length of each route segment as drawn (along the smooth curve when the route is smoothed).</summary>
        private static double[] SegmentLengths(SketchObject r, IReadOnlyList<Vec2> pts)
        {
            var result = new double[Math.Max(0, pts.Count - 1)];
            if (r.Flag("smooth", false) && pts.Count >= 3)
            {
                var segs = Geom.Spline(pts);
                for (var i = 0; i < result.Length; i++)
                    result[i] = Geom.Length(Geom.Sample(new[] { PathSeg.Move(pts[i]), segs[i + 1] }, 24));
            }
            else
                for (var i = 0; i < result.Length; i++) result[i] = pts[i].DistanceTo(pts[i + 1]);
            return result;
        }

        /// <summary>The symbol at route point <paramref name="i"/>: the docked one, else the nearest location within 3 mm.</summary>
        private static SketchObject? Match(SketchObject route, int i, Vec2 p, List<(SketchObject Obj, Vec2 Loc)> symbols)
        {
            var att = route.Attach is not null && i < route.Attach.Count ? route.Attach[i] : null;
            if (att is not null)
            {
                var docked = symbols.FirstOrDefault(s => s.Obj.Id == att.ObjectId);
                if (docked.Obj is not null) return docked.Obj;
            }
            var best = symbols.Where(s => s.Loc.DistanceTo(p) <= MatchDistance).OrderBy(s => s.Loc.DistanceTo(p)).FirstOrDefault();
            return best.Obj;
        }

        private static string SymbolName(SketchObject o, Lang lang)
        {
            if (!string.IsNullOrWhiteSpace(o.Text) && o.Type != StopType.TypeId) return o.Text!.Trim();
            return o.Type switch
            {
                StopType.TypeId => (lang == Lang.De ? "Halt " : "Stop ") + StopType.Label(o),
                MarkerType.TypeId => Prm.OptionLabel(MarkerType.KindDef, o.Str("kind", "start")).Get(lang),
                PoiType.TypeId => Prm.OptionLabel(PoiType.KindDef, o.Str("kind", "pin")).Get(lang),
                _ => Prm.OptionLabel(Place.RankDef, o.Str("rank", "city")).Get(lang),
            };
        }

        private static string SymbolKind(Registry registry, SketchObject o, Lang lang)
        {
            var typeName = registry.Type(o.Type)?.Name.Get(lang) ?? o.Type;
            return o.Type switch
            {
                MarkerType.TypeId => typeName + ": " + Prm.OptionLabel(MarkerType.KindDef, o.Str("kind", "start")).Get(lang),
                PoiType.TypeId => typeName + ": " + Prm.OptionLabel(PoiType.KindDef, o.Str("kind", "pin")).Get(lang),
                _ => typeName,
            };
        }
    }
}
