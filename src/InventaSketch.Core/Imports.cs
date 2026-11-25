using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace InventaSketch.Core
{
    /// <summary>
    /// Importing data files: CSV tables for chart data, GeoJSON and GPX for map routes and markers. Parsing only; the
    /// engine turns the result into elements.
    /// </summary>
    public static class Imports
    {
        /// <summary>
        /// A CSV/TSV table as chart data text (fields separated by ";", decimal point, one row per line). The separator is
        /// detected (tab, ";" or ","), quoted fields keep their separators, and a decimal comma is turned into a point when
        /// the separator is not a comma. Empty lines are dropped.
        /// </summary>
        public static string CsvToChartData(string text)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Trim().Length > 0).ToList();
            if (lines.Count == 0) return "";
            var sample = lines.Take(5).ToList();
            char sep = sample.All(l => l.Contains('\t')) ? '\t' : sample.All(l => l.Contains(';')) ? ';' : ',';
            var rows = new List<string>();
            foreach (var line in lines)
            {
                var fields = Split(line, sep).Select(f =>
                {
                    var v = f.Trim();
                    // Decimal comma (German Excel) unless the comma separates fields.
                    if (sep != ',' && Number(v.Replace(',', '.'))) v = v.Replace(',', '.');
                    return v.Replace(";", ",");
                });
                rows.Add(string.Join(";", fields));
            }
            return string.Join("\n", rows);
        }

        private static bool Number(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        private static IEnumerable<string> Split(string line, char sep)
        {
            var sb = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == sep && !quoted) { yield return sb.ToString(); sb.Clear(); }
                else sb.Append(c);
            }
            yield return sb.ToString();
        }

        /// <summary>Geodata read from a file: lines (tracks, routes, line strings, polygon rings) and named points, lon/lat in degrees.</summary>
        public sealed record GeoData(IReadOnlyList<(string? Name, IReadOnlyList<Vec2> LonLat)> Lines, IReadOnlyList<(string? Name, Vec2 LonLat)> Points)
        {
            public bool IsEmpty => Lines.Count == 0 && Points.Count == 0;
        }

        /// <summary>GeoJSON (FeatureCollection, Feature or geometry) or GPX (tracks, routes, waypoints); null if neither.</summary>
        public static GeoData? ParseGeo(string text)
        {
            var t = (text ?? "").TrimStart('﻿', ' ', '\t', '\r', '\n');
            if (t.StartsWith("<", StringComparison.Ordinal)) return ParseGpx(t);
            if (t.StartsWith("{", StringComparison.Ordinal)) return ParseGeoJson(t);
            return null;
        }

        private static GeoData? ParseGpx(string text)
        {
            var doc = new XmlDocument { XmlResolver = null };
            try { doc.LoadXml(text); } catch (XmlException) { return null; }
            if (doc.DocumentElement?.LocalName != "gpx") return null;
            var lines = new List<(string?, IReadOnlyList<Vec2>)>();
            var points = new List<(string?, Vec2)>();
            Vec2? Pt(XmlElement e) => double.TryParse(e.GetAttribute("lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                && double.TryParse(e.GetAttribute("lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ? new Vec2(lon, lat) : null;
            string? Name(XmlElement e) => e.ChildNodes.OfType<XmlElement>().FirstOrDefault(c => c.LocalName == "name")?.InnerText;
            foreach (var trk in doc.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                if (trk.LocalName == "trk")
                    foreach (var seg in trk.ChildNodes.OfType<XmlElement>().Where(c => c.LocalName == "trkseg"))
                    {
                        var pts = seg.ChildNodes.OfType<XmlElement>().Where(c => c.LocalName == "trkpt").Select(Pt).Where(p => p is not null).Select(p => p!.Value).ToList();
                        if (pts.Count >= 2) lines.Add((Name(trk), pts));
                    }
                else if (trk.LocalName == "rte")
                {
                    var pts = trk.ChildNodes.OfType<XmlElement>().Where(c => c.LocalName == "rtept").Select(Pt).Where(p => p is not null).Select(p => p!.Value).ToList();
                    if (pts.Count >= 2) lines.Add((Name(trk), pts));
                }
                else if (trk.LocalName == "wpt" && Pt(trk) is { } w) points.Add((Name(trk), w));
            }
            return new GeoData(lines, points);
        }

        private static GeoData? ParseGeoJson(string text)
        {
            JsonDocument json;
            try { json = JsonDocument.Parse(text); } catch (JsonException) { return null; }
            var lines = new List<(string?, IReadOnlyList<Vec2>)>();
            var points = new List<(string?, Vec2)>();
            static Vec2 P(JsonElement c) => new(c[0].GetDouble(), c[1].GetDouble());
            static List<Vec2> Ring(JsonElement c) => c.EnumerateArray().Select(P).ToList();
            void Geometry(JsonElement g, string? name)
            {
                if (g.ValueKind != JsonValueKind.Object || !g.TryGetProperty("type", out var type)) return;
                var c = g.TryGetProperty("coordinates", out var cc) ? cc : default;
                switch (type.GetString())
                {
                    case "Point": points.Add((name, P(c))); break;
                    case "MultiPoint": foreach (var p in c.EnumerateArray()) points.Add((name, P(p))); break;
                    case "LineString": lines.Add((name, Ring(c))); break;
                    case "MultiLineString": foreach (var l in c.EnumerateArray()) lines.Add((name, Ring(l))); break;
                    case "Polygon": foreach (var r in c.EnumerateArray()) lines.Add((name, Ring(r))); break;
                    case "MultiPolygon": foreach (var poly in c.EnumerateArray()) foreach (var r in poly.EnumerateArray()) lines.Add((name, Ring(r))); break;
                    case "GeometryCollection":
                        if (g.TryGetProperty("geometries", out var gs)) foreach (var x in gs.EnumerateArray()) Geometry(x, name);
                        break;
                }
            }
            void Feature(JsonElement f)
            {
                string? name = null;
                if (f.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                    foreach (var key in new[] { "name", "title", "label" })
                        if (props.TryGetProperty(key, out var n) && n.ValueKind == JsonValueKind.String) { name = n.GetString(); break; }
                if (f.TryGetProperty("geometry", out var g)) Geometry(g, name);
            }
            using (json)
            {
                var root = json.RootElement;
                var kind = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                try
                {
                    if (kind == "FeatureCollection" && root.TryGetProperty("features", out var fs)) foreach (var f in fs.EnumerateArray()) Feature(f);
                    else if (kind == "Feature") Feature(root);
                    else Geometry(root, null);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IndexOutOfRangeException or FormatException) { return null; }
            }
            return new GeoData(lines.Where(l => l.Item2.Count >= 2).ToList(), points);
        }

        /// <summary>
        /// Map elements for <paramref name="geo"/> fitted into the page (inset by a tenth), using the maps module's types
        /// when it is installed (route <c>map.route</c>, marker <c>map.marker</c>): every line as a route (dense tracks thinned
        /// to points at least 0.5 mm apart), every point as a waypoint marker with its name as text; a file with lines only
        /// gets start and finish markers on its first line. Empty if the maps module is missing.
        /// </summary>
        public static IReadOnlyList<SketchObject> GeoElements(Registry registry, GeoData geo, Size2 page)
        {
            var created = new List<SketchObject>();
            var routeItem = registry.Library.FirstOrDefault(i => i.TypeId == "map.route");
            if (routeItem is null) return created;
            LibraryItem? Marker(string kind) => registry.Library.FirstOrDefault(i => i.TypeId == "map.marker" && i.Params is { } ps && ps.TryGetValue("kind", out var k) && k.Text == kind)
                ?? registry.Library.FirstOrDefault(i => i.TypeId == "map.marker");
            var map = Fit(geo, new Rect(page.W * 0.1, page.H * 0.1, page.W * 0.8, page.H * 0.8));
            SketchObject Pin(LibraryItem m, Vec2 at)
            {
                // The pin's tip on the place (the marker stands on its bottom edge).
                var o = registry.Create(m, Vec2.Zero);
                return o with { Transform = o.Transform with { X = at.X, Y = Math.Round(at.Y - o.Size.H / 2, 6) } };
            }
            foreach (var (_, lonLat) in geo.Lines)
            {
                var pts = lonLat.Select(map).ToList();
                var thin = new List<Vec2> { pts[0] };
                foreach (var p in pts.Skip(1)) if (p.DistanceTo(thin[thin.Count - 1]) >= 0.5) thin.Add(p);
                if (thin[thin.Count - 1] != pts[pts.Count - 1]) thin.Add(pts[pts.Count - 1]);
                if (thin.Count >= 2) created.Add(registry.Create(routeItem, thin[0]) with { Points = thin, Attach = null });
            }
            foreach (var (name, lonLat) in geo.Points)
                if (Marker("via") is { } m)
                {
                    var o = Pin(m, map(lonLat));
                    created.Add(string.IsNullOrWhiteSpace(name) ? o : o with { Text = name });
                }
            if (geo.Points.Count == 0 && created.FirstOrDefault(o => o.Points is { Count: >= 2 }) is { } first)
            {
                if (Marker("start") is { } s) created.Add(Pin(s, first.Points![0]));
                if (Marker("finish") is { } f) created.Add(Pin(f, first.Points![first.Points.Count - 1]));
            }
            return created;
        }

        /// <summary>True if <paramref name="fileName"/> looks like geodata (GeoJSON or GPX) rather than a table.</summary>
        public static bool IsGeoFile(string fileName, string text)
        {
            var ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (ext is ".gpx" or ".geojson") return true;
            if (ext is ".csv" or ".tsv" or ".txt") return false;
            return ParseGeo(text) is { IsEmpty: false };
        }

        /// <summary>
        /// Projects lon/lat onto the page: an equirectangular projection around the data's mean latitude (fine for the
        /// city and region scale of slides), north up, fitted into <paramref name="area"/> keeping the aspect ratio.
        /// </summary>
        public static Func<Vec2, Vec2> Fit(GeoData data, Rect area)
        {
            var all = data.Lines.SelectMany(l => l.LonLat).Concat(data.Points.Select(p => p.LonLat)).ToList();
            if (all.Count == 0) return p => new Vec2(area.X + area.W / 2, area.Y + area.H / 2);
            var lat0 = all.Average(p => p.Y) * Math.PI / 180;
            var k = Math.Cos(lat0);
            double minX = all.Min(p => p.X * k), maxX = all.Max(p => p.X * k), minY = all.Min(p => p.Y), maxY = all.Max(p => p.Y);
            double w = Math.Max(1e-9, maxX - minX), h = Math.Max(1e-9, maxY - minY);
            var s = Math.Min(area.W / w, area.H / h);
            double ox = area.X + (area.W - w * s) / 2, oy = area.Y + (area.H - h * s) / 2;
            return p => new Vec2(Math.Round(ox + (p.X * k - minX) * s, 6), Math.Round(oy + (maxY - p.Y) * s, 6));
        }
    }
}
