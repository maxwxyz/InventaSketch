using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Maps and routes: schematic map sketches for presentations (no GIS). Map frames with grid or terrain hint, regions,
    /// contour lines, place labels, roads, rail lines and rivers (point objects with rails for docking), stations on lines,
    /// bridges and tunnels on roads, route lines with waypoints, direction arrows and values (length at the map scale,
    /// speed, travel time), start/finish/via markers, numbered stops, points of interest (independently drawn pictograms),
    /// distance/time callouts, scale bar, north arrow, compass rose and an automatic legend. Every object is insertable
    /// without parameters; box symbols are drawn inside their box and stretch with free scaling.
    /// </summary>
    public sealed class MapsModule : IModule, IExportProvider
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() => new ObjectType[]
        {
            Frame.Create(), new RegionType(), Place.Create(),
            new RoadType(), new RailType(), new RiverType(),
            StationType.Create(), new BridgeType(), new TunnelType(), new ContourType(),
            new RouteType(), MarkerType.Create(), StopType.Create(), Callout.Create(),
            PoiType.Create(),
            ScaleBar.Create(), NorthArrow.Create(), Compass.Create(), Legend.Create(),
        });

        private static readonly Lazy<IReadOnlyDictionary<string, ObjectType>> ById = new(() => AllTypes.Value.ToDictionary(t => t.Id, StringComparer.Ordinal));
        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "maps";
        public I18nText Name => new("Maps & routes", "Karten & Routen");
        public string Version => "0.3.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(MapCategories.Base, new I18nText("Map backgrounds, areas & places", "Kartengrund, Flächen & Orte"), Id, 10),
            new LibraryCategory(MapCategories.Lines, new I18nText("Roads, rail & water", "Straßen, Bahn & Gewässer"), Id, 20),
            new LibraryCategory(MapCategories.Routes, new I18nText("Routes & markers", "Routen & Marker"), Id, 30),
            new LibraryCategory(MapCategories.Poi, new I18nText("Points of interest", "Orte von Interesse (POI)"), Id, 40),
            new LibraryCategory(MapCategories.Marks, new I18nText("Scale, north & legend", "Maßstab, Nordpfeil & Legende"), Id, 50),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        /// <summary>Domain exports of this module (<see cref="IExportProvider"/>): the route stop list.</summary>
        public IEnumerable<IExporter> Exporters => new IExporter[] { new RouteStopsExporter() };

        /// <summary>A map type by id (used by the legend to draw samples with the real types).</summary>
        internal static ObjectType? TypeById(string id) => ById.Value.TryGetValue(id, out var t) ? t : null;

        private static IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, string? view = null, IReadOnlyDictionary<string, ParamValue>? ps = null,
                IReadOnlyList<Vec2>? pts = null, Size2? size = null, string? text = null, params string[] kw)
            {
                var t = ById.Value[type];
                Size2? s = size;
                var v = view;
                if (t is MapBoxType bt)
                {
                    var spec = bt.Spec(view);
                    v = spec.Id;
                    s ??= spec.Design;
                }
                list.Add(new LibraryItem("map." + id, type, new I18nText(en, de), t.Category) { View = v, Params = ps, Points = pts, Size = s, Text = text, Keywords = kw });
            }

            // Map backgrounds, areas and places
            Add("frame", Frame.TypeId, "Map frame", "Kartenrahmen");
            Add("frame.grid", Frame.TypeId, "Map frame with reference grid", "Kartenrahmen mit Planquadraten", ps: P(("style", "grid")), kw: new[] { "grid", "gitter" });
            Add("frame.terrain", Frame.TypeId, "Map frame with terrain", "Kartenrahmen mit Gelände", ps: P(("style", "terrain")), kw: new[] { "terrain", "hills", "gelände", "hügel" });
            Add("region", RegionType.TypeId, "Area", "Gebiet");
            Add("region.forest", RegionType.TypeId, "Forest", "Wald", ps: P(("kind", "forest"), ("smooth", true)), kw: new[] { "wood", "forst" });
            Add("region.water", RegionType.TypeId, "Lake", "See", ps: P(("kind", "water"), ("smooth", true)), kw: new[] { "lake", "pond", "sea", "teich", "meer" });
            Add("region.park", RegionType.TypeId, "Park", "Park", ps: P(("kind", "park")), kw: new[] { "green", "grün" });
            Add("region.urban", RegionType.TypeId, "Built-up area", "Bebaute Fläche", ps: P(("kind", "urban")), kw: new[] { "city", "town", "stadt", "siedlung" });
            Add("region.restricted", RegionType.TypeId, "Restricted zone / geofence", "Sperrzone / Geofence", ps: P(("kind", "restricted"), ("fill", "hatch")), kw: new[] { "no-go", "verbot" });
            Add("region.hatched", RegionType.TypeId, "Area (hatched)", "Gebiet (schraffiert)", ps: P(("fill", "hatch")), kw: new[] { "hatch", "schraffur" });
            Add("place.capital", Place.TypeId, "Capital", "Hauptstadt", ps: P(("rank", "capital")));
            Add("place.city", Place.TypeId, "City", "Stadt", ps: P(("rank", "city")));
            Add("place.town", Place.TypeId, "Town", "Ort", ps: P(("rank", "town")), size: new Size2(30, 7));
            Add("place.village", Place.TypeId, "Village", "Dorf", ps: P(("rank", "village")), size: new Size2(28, 6));
            Add("place.locality", Place.TypeId, "Locality", "Ortsteil", ps: P(("rank", "locality")), size: new Size2(26, 6));
            Add("contour", ContourType.TypeId, "Contour line", "Höhenlinie", kw: new[] { "isoline", "höhe", "elevation" });
            Add("contour.index", ContourType.TypeId, "Index contour (500 m)", "Zählkurve (500 m)", ps: P(("index", true), ("elevation", 500.0)), kw: new[] { "index", "zählkurve" });
            Add("contour.hill", ContourType.TypeId, "Hill (closed contour)", "Kuppe (geschlossene Höhenlinie)", ps: P(("closed", true), ("elevation", 250.0)),
                pts: new[] { new Vec2(-22, 0), new Vec2(-8, -13), new Vec2(14, -10), new Vec2(24, 3), new Vec2(8, 13), new Vec2(-14, 10) }, kw: new[] { "hill", "summit", "hügel", "berg" });

            // Roads, rail and water
            Add("road.motorway", RoadType.TypeId, "Motorway", "Autobahn", ps: P(("kind", "motorway")), kw: new[] { "highway", "freeway" });
            Add("road.main", RoadType.TypeId, "Main road", "Hauptstraße", ps: P(("kind", "main")));
            Add("road.side", RoadType.TypeId, "Side road", "Nebenstraße", ps: P(("kind", "side")), kw: new[] { "street", "gasse" });
            Add("road.path", RoadType.TypeId, "Path", "Weg", ps: P(("kind", "path")), kw: new[] { "footpath", "trail", "fußweg", "pfad" });
            Add("road.curve", RoadType.TypeId, "Main road (curved)", "Hauptstraße (Kurve)", ps: P(("kind", "main"), ("smooth", true)),
                pts: new[] { new Vec2(-50, 12), new Vec2(0, -12), new Vec2(50, 12) }, kw: new[] { "curve", "bend", "kurve" });
            Add("rail", RailType.TypeId, "Railway", "Eisenbahn");
            Add("rail.tram", RailType.TypeId, "Tram line", "Straßenbahnlinie", ps: P(("kind", "tram")));
            Add("river", RiverType.TypeId, "River", "Fluss");
            Add("river.stream", RiverType.TypeId, "Stream", "Bach", ps: P(("kind", "stream")), kw: new[] { "brook", "creek" });
            Add("river.canal", RiverType.TypeId, "Canal", "Kanal", ps: P(("kind", "canal"), ("smooth", false)), pts: new[] { new Vec2(-50, 0), new Vec2(50, 0) });
            Add("station", StationType.TypeId, "Railway station", "Bahnhof", kw: new[] { "train", "zug" });
            Add("station.interchange", StationType.TypeId, "Interchange station", "Umsteigebahnhof", ps: P(("kind", "interchange")), kw: new[] { "transfer", "umsteigen" });
            Add("station.tram", StationType.TypeId, "Tram stop", "Straßenbahnhaltestelle", ps: P(("kind", "tram")), kw: new[] { "light rail", "stadtbahn" });
            Add("station.bus", StationType.TypeId, "Bus stop", "Bushaltestelle", ps: P(("kind", "bus")), kw: new[] { "bus", "haltestelle" });
            Add("bridge", BridgeType.TypeId, "Bridge", "Brücke", kw: new[] { "viaduct", "viadukt" });
            Add("tunnel", TunnelType.TypeId, "Tunnel", "Tunnel", kw: new[] { "portal", "unterführung" });

            // Routes and markers
            Add("route", RouteType.TypeId, "Route", "Route");
            Add("route.dashed", RouteType.TypeId, "Alternative route (dashed)", "Alternativroute (gestrichelt)", ps: P(("style", "dashed")), kw: new[] { "alternative", "alternativ" });
            Add("route.dotted", RouteType.TypeId, "Walking route (dotted)", "Fußweg (gepunktet)", ps: P(("style", "dotted"), ("width", 0.9), ("mode", "walk")), kw: new[] { "walk", "zu fuß" });
            Add("route.values", RouteType.TypeId, "Route with length and travel time", "Route mit Länge und Reisezeit", ps: P((ValueLabels.ShowParam, true)),
                kw: new[] { "distance", "travel time", "duration", "speed", "entfernung", "reisezeit", "fahrzeit", "geschwindigkeit" });
            Add("route.bike", RouteType.TypeId, "Cycle route", "Radroute", ps: P(("style", "dashed"), ("mode", "bike")), kw: new[] { "bicycle", "bike", "fahrrad", "radweg" });
            Add("route.smooth", RouteType.TypeId, "Route (smooth)", "Route (glatt)", ps: P(("smooth", true)));
            Add("marker.start", MarkerType.TypeId, "Start marker", "Startmarker", "pin", P(("kind", "start")));
            Add("marker.finish", MarkerType.TypeId, "Finish marker", "Zielmarker", "pin", P(("kind", "finish")), kw: new[] { "destination", "goal", "chequered flag", "zielflagge" });
            Add("marker.via", MarkerType.TypeId, "Waypoint marker", "Zwischenzielmarker", "pin", P(("kind", "via")));
            Add("marker.start.symbol", MarkerType.TypeId, "Start (symbol)", "Start (Symbol)", "symbol", P(("kind", "start")));
            Add("marker.finish.symbol", MarkerType.TypeId, "Finish (symbol)", "Ziel (Symbol)", "symbol", P(("kind", "finish")));
            Add("marker.a", MarkerType.TypeId, "Marker A", "Marker A", "pin", P(("kind", "start")), text: "A", kw: new[] { "letter", "buchstabe" });
            Add("marker.b", MarkerType.TypeId, "Marker B", "Marker B", "pin", P(("kind", "finish")), text: "B", kw: new[] { "letter", "buchstabe" });
            Add("stop", StopType.TypeId, "Numbered stop", "Nummerierter Halt");
            Add("stop.square", StopType.TypeId, "Numbered stop (square)", "Nummerierter Halt (Quadrat)", ps: P(("shape", "square")));
            Add("callout", Callout.TypeId, "Distance/time callout", "Entfernungs-/Zeitangabe");
            Add("callout.walk", Callout.TypeId, "Walking distance callout", "Fußweg-Angabe", ps: P(("mode", "walk"), ("distance", 800.0), ("unit", "m"), ("time", 10.0)));

            // Points of interest (badge and pin)
            foreach (var (value, en, de, _) in PoiType.Kinds)
            {
                Add("poi." + value, PoiType.TypeId, en, de, "symbol", P(("kind", value)), kw: new[] { en.ToLowerInvariant(), de.ToLowerInvariant() });
                Add("poi." + value + ".pin", PoiType.TypeId, en + " (pin)", de + " (Stecknadel)", "pin", P(("kind", value)), kw: new[] { en.ToLowerInvariant(), de.ToLowerInvariant() });
            }

            // Scale, north and legend
            Add("scalebar", ScaleBar.TypeId, "Scale bar (km)", "Maßstabsleiste (km)");
            Add("scalebar.mi", ScaleBar.TypeId, "Scale bar (miles)", "Maßstabsleiste (Meilen)", ps: P(("unit", "mi")), kw: new[] { "miles", "meilen" });
            Add("scalebar.line", ScaleBar.TypeId, "Scale line (500 m)", "Maßstabslinie (500 m)", ps: P(("style", "line"), ("length", 500.0), ("unit", "m"), ("segments", 5)));
            Add("north", NorthArrow.TypeId, "North arrow", "Nordpfeil");
            Add("compass", Compass.TypeId, "Compass rose", "Windrose");
            Add("compass.4", Compass.TypeId, "Compass rose (4 points)", "Windrose (4 Strahlen)", ps: P(("points", "4")));
            Add("legend", Legend.TypeId, "Legend", "Legende");
            return list;
        }
    }
}
