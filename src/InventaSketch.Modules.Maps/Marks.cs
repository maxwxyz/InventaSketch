using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Scale bar: alternating black/white segments (or a ticked line) labelled from 0 to the represented length. The bar
    /// spans the box width, so free scaling changes the drawn length while the represented length (parameter) stays.
    /// </summary>
    internal static class ScaleBar
    {
        public const string TypeId = "map.scalebar";

        public static readonly string[] Units = { "km", "mi", "m", "ft" };
        private static readonly ParamDef LengthDef = Prm.Num("length", "Represented length", "Dargestellte Länge", 1, 0.001, 1000000, null, 0.1);
        private static readonly ParamDef UnitDef = Prm.Choice("unit", "Unit", "Einheit", "km",
            ("km", "Kilometres", "Kilometer"), ("mi", "Miles", "Meilen"), ("m", "Metres", "Meter"), ("ft", "Feet", "Fuß"));
        private static readonly ParamDef SegmentsDef = Prm.Int("segments", "Segments", "Abschnitte", 4, 1, 10);
        private static readonly ParamDef StyleDef = Prm.Choice("style", "Style", "Stil", "alternating", ("alternating", "Alternating bar", "Wechselbalken"), ("line", "Ticked line", "Linie mit Strichen"));

        public static double LengthOf(SketchObject o) => Math.Max(0.001, o.Num("length", 1));
        public static string UnitOf(SketchObject o) => o.Str("unit", "km");

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                double hw = p.W / 2, hh = p.H / 2;
                var n = Math.Max(1, Math.Min(10, o.Int("segments", 4)));
                var length = LengthOf(o);
                var unit = UnitOf(o);
                var bh = Math.Min(0.3 * p.H, 2.0 * p.K);
                var barTop = hh - bh;
                var fs = Math.Min(2.4 * p.K, Math.Max(0.1, barTop + hh - 0.6 * p.K));
                double X(int i) => -hw + p.W * i / n;
                if (o.Str("style", "alternating") == "line")
                {
                    var ticks = new List<IReadOnlyList<Vec2>> { new[] { new Vec2(-hw, hh), new Vec2(hw, hh) } };
                    for (var i = 0; i <= n; i++) ticks.Add(new[] { new Vec2(X(i), hh), new Vec2(X(i), hh - (i == 0 || i == n || 2 * i == n ? bh : bh * 0.6)) });
                    p.Add(Geom.MultiPath(ticks, S.Stroke(Palette.Ink, 0.3), MapTags.Bar)!);
                }
                else
                {
                    for (var i = 0; i < n; i++)
                        p.Add(Prim.Rect(X(i), barTop, p.W / n, bh, S.Filled(i % 2 == 0 ? Palette.Black : Palette.White, S.Stroke(Palette.Ink, 0.25))), MapTags.Bar);
                }
                var labels = Labels(n, length, unit, ctx.Lang, p.W, fs);
                var y = Math.Max(-hh + fs / 2, barTop - 0.5 * p.K - fs / 2);
                foreach (var (i, text) in labels)
                {
                    var anchor = i == 0 ? TextAnchor.Start : i == n ? TextAnchor.End : TextAnchor.Middle;
                    p.TextAt(new Vec2(X(i), y), text, fs, p.W, fs, anchor, color: Palette.Ink);
                }
            }
            return new ScaleBarType(TypeId, new I18nText("Scale bar", "Maßstabsleiste"), MapCategories.Marks,
                new[] { "scale", "scale bar", "distance", "km", "miles", "metres", "feet", "map",
                        "maßstab", "maßstabsleiste", "maßstabsbalken", "entfernung", "kilometer", "meilen", "karte" },
                MapViews.Plan(40, 8, Draw))
            {
                Params = new[] { LengthDef, UnitDef, SegmentsDef, StyleDef }.Concat(ScaleBarType.ValueParams).ToList(),
                Uniform = false,
                CompanionIds = new[] { NorthArrow.TypeId, Compass.TypeId, Legend.TypeId },
                // Routes and regions on the page measure with this scale bar (those showing their values redraw with it), and
                // so do distance measures and elevation profiles.
                MoreDependents = MapMeasure.ScaleReaders,
                Extra = (o, lang) => new[]
                {
                    ContextAction.WithInput("map.scalebar.length", new I18nText("Length…", "Länge…"), IconIds.QtyLength,
                        new I18nText("Represented length (e.g. 2 km, 1.5 mi, 500 m)", "Dargestellte Länge (z. B. 2 km, 1,5 mi, 500 m)"),
                        Fmt.Num(LengthOf(o), lang) + " " + UnitOf(o),
                        (x, text) =>
                        {
                            var (v, unit) = Fmt.ParseWithUnit(text, lang, 0.001, 1000000, Units);
                            var y = x.WithParam("length", v);
                            return unit is null ? y : y.WithParam("unit", unit);
                        }) with { Group = "length" },
                    new ContextAction("map.scalebar.segments.inc", new I18nText("More segments", "Mehr Abschnitte"), IconIds.CountMore, x => x.WithParam("segments", Math.Min(10, x.Int("segments", 4) + 1))) { Group = "segments" },
                    new ContextAction("map.scalebar.segments.dec", new I18nText("Fewer segments", "Weniger Abschnitte"), IconIds.CountFewer, x => x.WithParam("segments", Math.Max(1, x.Int("segments", 4) - 1))) { Group = "segments" },
                }.Concat(Prm.Choice(o, "map.scalebar.unit", UnitDef, new I18nText("Unit", "Einheit")))
                    .Concat(Prm.Choice(o, "map.scalebar.style", StyleDef, new I18nText("Style", "Stil"))),
            };
        }

        /// <summary>
        /// Labelled segment boundaries: every k-th boundary (smallest k whose labels do not overlap) plus the end, which
        /// carries the unit ("0", "0.5", "1 km").
        /// </summary>
        public static List<(int Index, string Text)> Labels(int n, double length, string unit, Lang lang, double width, double fs)
        {
            string T(int i) => Fmt.Num(length * i / n, lang) + (i == n ? " " + unit : "");
            (double L, double R) Extent(int i)
            {
                var x = width * i / n;
                var w = Geom.TextWidth(T(i), fs);
                return i == 0 ? (x, x + w) : i == n ? (x - w, x) : (x - w / 2, x + w / 2);
            }
            for (var k = 1; k <= n; k++)
            {
                var idx = Enumerable.Range(0, n + 1).Where(i => i % k == 0 || i == n).ToList();
                // Drop a multiple of k that would crowd the end label.
                if (idx.Count > 2 && n % k != 0 && Extent(idx[idx.Count - 2]).R + fs * 0.6 > Extent(n).L) idx.RemoveAt(idx.Count - 2);
                var ok = true;
                for (var j = 0; j + 1 < idx.Count && ok; j++) ok = Extent(idx[j]).R + fs * 0.6 <= Extent(idx[j + 1]).L;
                if (ok) return idx.Select(i => (i, T(i))).ToList();
            }
            return new List<(int, string)> { (0, T(0)), (n, T(n)) };
        }
    }

    /// <summary>North arrow: split arrowhead (black/white halves) under an "N".</summary>
    internal static class NorthArrow
    {
        public const string TypeId = "map.north";

        private static readonly ParamDef LetterDef = Prm.Bool("letter", "Show “N”", "„N“ zeigen", true);

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                var letter = o.Flag("letter", true);
                var tipY = letter ? -3.6 : -8.0;
                if (letter) p.Text(0, -6.0, "N", 3.8, 9, 4.0, bold: true, color: Palette.Black, tag: MapTags.Label);
                Vec2 tip = new(0, tipY), left = new(-3.6, 7.8), right = new(3.6, 7.8), notch = new(0, 4.6);
                p.Poly(new[] { tip, left, notch }, S.Filled(Palette.Black, S.Stroke(Palette.Black, 0.3)), true, MapTags.Needle);
                p.Poly(new[] { tip, notch, right }, S.Filled(Palette.White, S.Stroke(Palette.Black, 0.3)), true, MapTags.Needle);
            }
            return new MapBoxType(TypeId, new I18nText("North arrow", "Nordpfeil"), MapCategories.Marks,
                new[] { "north", "north arrow", "orientation", "compass", "nord", "nordpfeil", "norden", "ausrichtung", "kompass" },
                MapViews.Plan(10, 16, Draw))
            {
                Params = new[] { LetterDef },
                CompanionIds = new[] { ScaleBar.TypeId, Compass.TypeId, Legend.TypeId },
                Extra = (o, lang) => new[] { Prm.Toggle(o, "map.north.letter", "letter", true, new I18nText("Show “N”", "„N“ zeigen")) },
            };
        }
    }

    /// <summary>Compass rose with 4 or 8 points (black/white halves) and cardinal letters (German: N, O, S, W).</summary>
    internal static class Compass
    {
        public const string TypeId = "map.compass";

        private static readonly ParamDef PointsDef = Prm.Choice("points", "Points", "Strahlen", "8", ("8", "8 points", "8 Strahlen"), ("4", "4 points", "4 Strahlen"));
        private static readonly ParamDef LabelsDef = Prm.Bool("labels", "Cardinal letters", "Himmelsrichtungen beschriften", true);

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                var labels = o.Flag("labels", true);
                var scale = labels ? 1.0 : 1.3;
                Vec2 Pol(double r, double deg) => Vec2.Polar(r * scale, Geo.DegToRad(deg));
                p.Ellipse(0, 0, 7.6 * scale, 7.6 * scale, S.Stroke(Palette.Ink, 0.2), MapTags.Frame);
                void Ray(double deg, double len, double side)
                {
                    var tip = Pol(len, deg);
                    p.Poly(new[] { Vec2.Zero, tip, Pol(side, deg - 45) }, S.Filled(Palette.Black, S.Stroke(Palette.Black, 0.25)), true, MapTags.Needle);
                    p.Poly(new[] { Vec2.Zero, tip, Pol(side, deg + 45) }, S.Filled(Palette.White, S.Stroke(Palette.Black, 0.25)), true, MapTags.Needle);
                }
                if (o.Str("points", "8") != "4")
                    foreach (var d in new[] { -45.0, 45, 135, 225 }) Ray(d, 6.2, 1.5);
                foreach (var d in new[] { -90.0, 0, 90, 180 }) Ray(d, 9.4, 2.2);
                p.Round(0, 0, 0.6, S.Filled(Palette.White, S.Stroke(Palette.Black, 0.25)), MapTags.Needle);
                if (!labels) return;
                var east = ctx.Lang == Lang.De ? "O" : "E";
                foreach (var (deg, t) in new[] { (-90.0, "N"), (0.0, east), (90.0, "S"), (180.0, "W") })
                {
                    var q = Pol(11.4, deg);
                    p.Text(q.X, q.Y, t, 2.7, 3.0, 2.8, bold: t == "N", color: Palette.Black, tag: MapTags.Label);
                }
            }
            return new MapBoxType(TypeId, new I18nText("Compass rose", "Windrose"), MapCategories.Marks,
                new[] { "compass", "compass rose", "wind rose", "north", "cardinal", "orientation",
                        "kompass", "kompassrose", "windrose", "himmelsrichtung", "nord", "ausrichtung" },
                MapViews.Plan(26, 26, Draw))
            {
                Params = new[] { PointsDef, LabelsDef },
                CompanionIds = new[] { ScaleBar.TypeId, NorthArrow.TypeId, Legend.TypeId },
                Extra = (o, lang) => new[]
                {
                    Prm.Toggle(o, "map.compass.labels", "labels", true, new I18nText("Cardinal letters", "Himmelsrichtungen beschriften")),
                }.Concat(Prm.Choice(o, "map.compass.points", PointsDef, new I18nText("Points", "Strahlen"))),
            };
        }
    }

    /// <summary>
    /// Legend box. Entries come, in this order of precedence, from the object text (one entry per line, optional sample key
    /// in brackets: "[road.motorway] A8"), from the map objects on the same page (automatic, default) or from a default
    /// set. Samples are drawn by the real map types, so the legend always matches the map.
    /// </summary>
    internal static class Legend
    {
        public const string TypeId = MapDeps.LegendId;

        private static readonly ParamDef TitleDef = Prm.Text("title", "Title (empty: “Legend”)", "Titel (leer: „Legende“)", "") with { Default = null };
        private static readonly ParamDef AutoDef = Prm.Bool("auto", "List the map objects of the page", "Kartenobjekte der Seite auflisten", true);
        private const int MaxEntries = 16;

        /// <summary>True if the legend lists the page content (automatic and no manual text).</summary>
        public static bool IsAuto(SketchObject o) => o.Flag("auto", true) && string.IsNullOrWhiteSpace(o.Text);

        public sealed record Entry(string Key, string Label);

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                double hw = p.W / 2, hh = p.H / 2;
                p.Add(Geom.RoundedRect(-hw, -hh, p.W, p.H, Math.Min(1.5, 0.08 * Math.Min(p.W, p.H)), S.Filled(Palette.White, S.Stroke(Palette.Ink, 0.35))), MapTags.Frame);
                var pad = Math.Min(2.0, 0.07 * Math.Min(p.W, p.H));
                var title = o.Str("title", "").Trim();
                if (title.Length == 0) title = ctx.Lang == Lang.De ? "Legende" : "Legend";
                var titleH = Math.Min(4.6, p.H * 0.16);
                var top = -hh + pad;
                p.TextAt(new Vec2(-hw + pad, top + titleH / 2), title, Math.Min(3.4, titleH * 0.8), p.W - 2 * pad, titleH, TextAnchor.Start, bold: true, color: Palette.Black);
                var entries = Entries(o, ctx);
                var areaTop = top + titleH + pad * 0.6;
                var area = hh - pad - areaTop;
                if (entries.Count == 0 || area <= 0.2) return;
                var rowH = Math.Min(6.0, area / entries.Count);
                var sampleW = Math.Min(11, p.W * 0.3);
                var sampleH = rowH * 0.78;
                var labelX = -hw + pad + sampleW + pad * 0.8;
                for (var j = 0; j < entries.Count; j++)
                {
                    var cy = areaTop + rowH * (j + 0.5);
                    var cell = new Rect(-hw + pad, cy - sampleH / 2, sampleW, sampleH);
                    var sample = Sample(entries[j].Key, cell, ctx);
                    if (sample.Count > 0) p.Add(new GroupPrim(sample) { Tag = MapTags.Sample });
                    p.TextAt(new Vec2(labelX, cy), entries[j].Label, Math.Min(2.8, rowH * 0.62), hw - pad - labelX, rowH * 0.9, TextAnchor.Start, color: Palette.Black);
                }
            }
            return new MapBoxType(TypeId, new I18nText("Legend", "Legende"), MapCategories.Marks,
                new[] { "legend", "key", "map key", "symbols", "explanation", "legende", "zeichenerklärung", "kartenlegende", "erklärung" },
                MapViews.Plan(46, 40, Draw))
            {
                Params = new[] { TitleDef, AutoDef },
                Uniform = false,
                TextEditable = true,
                NeedsNeighbours = true,
                CompanionIds = new[] { ScaleBar.TypeId, NorthArrow.TypeId, Compass.TypeId },
                Extra = (o, lang) => new[]
                {
                    Prm.Toggle(o, "map.legend.auto", "auto", true, new I18nText("List the map objects", "Kartenobjekte auflisten"), IconIds.Legend),
                    ContextAction.WithInput("map.legend.title", new I18nText("Title…", "Titel…"), IconIds.Name,
                        new I18nText("Legend title", "Titel der Legende"), o.Str("title", ""), (x, text) => x.WithParam("title", (text ?? "").Trim())) with { Group = "text" },
                },
            };
        }

        private static readonly string[] FamilyOrder = { "route", "marker", "stop", "station", "poi", "road", "bridge", "tunnel", "rail", "river", "region", "contour", "place" };

        /// <summary>The entries the legend shows (manual text, page content or defaults), at most 16.</summary>
        public static IReadOnlyList<Entry> Entries(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            if (!string.IsNullOrWhiteSpace(o.Text))
                return o.Text!.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(MaxEntries).Select(Parse).ToList();
            var list = new List<Entry>();
            var page = o.Flag("auto", true) ? ctx.Doc.PageOf(o.Id) : null;
            if (page is not null)
            {
                foreach (var x in ctx.Doc.ObjectsOn(page))
                {
                    var e = EntryOf(x, lang);
                    if (e is not null && !list.Contains(e)) list.Add(e);
                }
                list = list.Select((e, i) => (e, i)).OrderBy(t => Array.IndexOf(FamilyOrder, Family(t.e.Key))).ThenBy(t => t.i).Select(t => t.e).Take(MaxEntries).ToList();
            }
            if (list.Count > 0) return list;
            return new[] { "route", "road.motorway", "road.main", "marker.start", "marker.finish", "poi.pin" }.Select(k => new Entry(k, KeyLabel(k, lang))).ToList();
        }

        private static string Family(string key) { var i = key.IndexOf('.'); return i < 0 ? key : key.Substring(0, i); }
        private static string Variant(string key) { var i = key.IndexOf('.'); return i < 0 ? "" : key.Substring(i + 1); }

        /// <summary>"[road.main] B 17" → sample key and label; lines without a key get a bullet.</summary>
        private static Entry Parse(string line)
        {
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                var close = line.IndexOf(']');
                if (close > 1) return new Entry(line.Substring(1, close - 1).Trim().ToLowerInvariant(), line.Substring(close + 1).Trim());
            }
            return new Entry("bullet", line);
        }

        private static Entry? EntryOf(SketchObject x, Lang lang)
        {
            string key;
            switch (x.Type)
            {
                case RoadType.TypeId: key = "road." + x.Str("kind", "main"); break;
                case RailType.TypeId: key = "rail." + x.Str("kind", "rail"); break;
                case RiverType.TypeId: key = "river." + x.Str("kind", "river"); break;
                case RegionType.TypeId: key = "region." + x.Str("kind", "area"); break;
                case MarkerType.TypeId: key = "marker." + x.Str("kind", "start"); break;
                case StopType.TypeId: key = "stop"; break;
                case StationType.TypeId: key = "station." + x.Str("kind", "rail"); break;
                case BridgeType.TypeId: key = "bridge"; break;
                case TunnelType.TypeId: key = "tunnel"; break;
                case ContourType.TypeId: key = "contour"; break;
                case PoiType.TypeId: key = "poi." + x.Str("kind", "pin"); break;
                case RouteType.TypeId:
                    {
                        var style = x.Str("style", "solid");
                        key = style == "solid" ? "route" : "route." + style;
                        var text = x.Text?.Trim() ?? "";
                        return new Entry(key, text.Length > 0 ? text : KeyLabel(key, lang));
                    }
                default: return null;
            }
            return new Entry(key, KeyLabel(key, lang));
        }

        /// <summary>Bilingual label of a sample key (the option label of the kind parameter).</summary>
        public static string KeyLabel(string key, Lang lang)
        {
            var v = Variant(key);
            I18nText label = Family(key) switch
            {
                "route" => v switch { "dashed" => new I18nText("Alternative route", "Alternativroute"), "dotted" => new I18nText("Walking route", "Fußweg"), _ => new I18nText("Route", "Route") },
                "road" => Prm.OptionLabel(RoadType.KindDef, v),
                "rail" => Prm.OptionLabel(RailType.KindDef, v),
                "river" => Prm.OptionLabel(RiverType.KindDef, v),
                "region" => Prm.OptionLabel(RegionType.KindDef, v),
                "marker" => Prm.OptionLabel(MarkerType.KindDef, v),
                "stop" => new I18nText("Stop", "Halt"),
                "station" => Prm.OptionLabel(StationType.KindDef, v),
                "bridge" => new I18nText("Bridge", "Brücke"),
                "tunnel" => new I18nText("Tunnel", "Tunnel"),
                "contour" => new I18nText("Contour line", "Höhenlinie"),
                "poi" => Prm.OptionLabel(PoiType.KindDef, v),
                "place" => Prm.OptionLabel(Place.RankDef, v),
                _ => new I18nText(key),
            };
            return label.Get(lang);
        }

        /// <summary>Sample drawing of a key fitted into <paramref name="cell"/> (local coordinates of the legend).</summary>
        public static List<Primitive> Sample(string key, Rect cell, RenderContext ctx)
        {
            var family = Family(key);
            var v = Variant(key);
            var center = cell.Center;
            IReadOnlyList<Primitive> PointSample(string type, IReadOnlyList<Vec2> pts, params (string Key, ParamValue Value)[] ps)
            {
                var t = MapsModule.TypeById(type);
                if (t is null) return Array.Empty<Primitive>();
                var obj = new SketchObject { Id = "legend-sample", Type = type, Points = pts, Params = ps.ToDictionary(x => x.Key, x => x.Value) };
                var prims = t.Render(obj, ctx);
                var b = Prim.Bounds(prims);
                if (b is not Rect r || r.W < 1e-6 && r.H < 1e-6) return prims;
                // Line samples always span the cell width; wide bands (motorway) are flattened to the cell height.
                var sx = Math.Min(cell.W / Math.Max(r.W, 1e-6), 1.5);
                var sy = Math.Min(sx, cell.H / Math.Max(r.H, 1e-6));
                var m = Matrix2D.Translation(center.X, center.Y) * Matrix2D.Scaling(sx, sy) * Matrix2D.Translation(-r.Center.X, -r.Center.Y);
                return prims.Select(q => Prim.Transform(q, m)).ToList();
            }
            IReadOnlyList<Primitive> BoxSample(string type, string? view, params (string Key, ParamValue Value)[] ps)
            {
                if (MapsModule.TypeById(type) is not MapBoxType t) return Array.Empty<Primitive>();
                var d = t.Spec(view).Design;
                var k = Math.Min(cell.W / d.W, cell.H / d.H);
                var obj = new SketchObject { Id = "legend-sample", Type = type, View = t.Spec(view).Id, Size = new Size2(d.W * k, d.H * k), Params = ps.ToDictionary(x => x.Key, x => x.Value) };
                var m = Matrix2D.Translation(center.X, center.Y);
                return t.Render(obj, ctx).Select(q => Prim.Transform(q, m)).ToList();
            }
            var line = new[] { new Vec2(0, 0), new Vec2(12, 0) };
            var result = family switch
            {
                "route" => PointSample(RouteType.TypeId, line, ("style", v.Length == 0 ? "solid" : v), ("arrows", "none"), ("waypoints", false)),
                "road" => PointSample(RoadType.TypeId, line, ("kind", v.Length == 0 ? "main" : v)),
                "rail" => PointSample(RailType.TypeId, line, ("kind", v.Length == 0 ? "rail" : v)),
                "river" => PointSample(RiverType.TypeId, line, ("kind", v.Length == 0 ? "river" : v), ("smooth", false)),
                "region" => PointSample(RegionType.TypeId, new[] { new Vec2(0, -3), new Vec2(12, -3), new Vec2(12, 3), new Vec2(0, 3) }, ("kind", v.Length == 0 ? "area" : v)),
                "marker" => BoxSample(MarkerType.TypeId, "pin", ("kind", v.Length == 0 ? "start" : v)),
                "stop" => BoxSample(StopType.TypeId, null),
                "station" => StationType.Sample(v, cell),
                "bridge" => PointSample(BridgeType.TypeId, line),
                "tunnel" => PointSample(TunnelType.TypeId, line),
                "contour" => PointSample(ContourType.TypeId, line, ("label", false), ("smooth", false)),
                "poi" => BoxSample(PoiType.TypeId, "symbol", ("kind", v.Length == 0 ? "pin" : v)),
                "place" => new Primitive[] { Prim.Circle(center.X, center.Y, Math.Min(cell.H * 0.25, 1.4), S.Black) },
                _ => new Primitive[] { Prim.Circle(center.X, center.Y, Math.Min(cell.H * 0.18, 0.9), S.Black) },
            };
            return result.ToList();
        }
    }
}
