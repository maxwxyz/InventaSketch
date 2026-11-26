using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// The scale of a map sketch: how many metres one millimetre on the page stands for. A map has no scale of its own; it
    /// is taken, in this order, from the object's own <c>scale</c> parameter (map scale 1:N), from the first scale bar on the
    /// same page (its box width represents its length parameter), or from the document plan scale. Without any of them
    /// the map is a sketch without scale and drawn lengths stay unknown.
    /// </summary>
    internal static class MapScale
    {
        /// <summary>Parameter key of the optional map scale denominator (1:N) of routes.</summary>
        public const string Param = "scale";

        public static readonly ParamDef Def = new(Param, new I18nText("Map scale 1:N (empty: scale bar on the page)", "Kartenmaßstab 1:N (leer: Maßstabsleiste auf dem Blatt)"), ParamKind.Number)
        {
            Min = 1, Max = 1e9, Step = 1000, Advanced = true,
        };

        /// <summary>Metres per unit of a scale bar unit (km, mi, m, ft).</summary>
        public static double MetresPer(string unit) => unit switch
        {
            "km" => 1000,
            "mi" => 1609.344,
            "ft" => 0.3048,
            _ => 1,
        };

        /// <summary>How many metres one page millimetre stands for and where that comes from ("own", "bar" or "plan").</summary>
        public sealed record Info(double MetresPerMm, string Origin);

        /// <summary>The scale that applies to <paramref name="o"/>, or null for a sketch without scale.</summary>
        public static Info? Of(SketchObject o, SketchDocument doc)
        {
            var own = o.Num(Param, 0);
            if (own >= 1 && !double.IsInfinity(own)) return new Info(own / 1000, "own");
            if (Bar(o, doc) is { } bar)
            {
                var w = bar.Size.W;
                var m = ScaleBar.LengthOf(bar) * MetresPer(ScaleBar.UnitOf(bar));
                if (w > 1e-6 && m > 0) return new Info(m / w, "bar");
            }
            if (doc.Settings.PlanScale is double s && s >= 1) return new Info(s / 1000, "plan");
            return null;
        }

        /// <summary>The first scale bar on the page of <paramref name="o"/> (null if there is none or the object is on no page).</summary>
        public static SketchObject? Bar(SketchObject o, SketchDocument doc)
        {
            var page = doc.PageOf(o.Id);
            return page is null ? null : doc.ObjectsOn(page).FirstOrDefault(x => x.Type == ScaleBar.TypeId);
        }

        /// <summary>Length of each segment of a point object as drawn (along the smooth curve when it is smoothed).</summary>
        public static double[] SegmentLengths(SketchObject o, IReadOnlyList<Vec2> pts)
        {
            var result = new double[Math.Max(0, pts.Count - 1)];
            if (o.Flag("smooth", false) && pts.Count >= 3)
            {
                var segs = Geom.Spline(pts);
                for (var i = 0; i < result.Length; i++)
                    result[i] = Geom.Length(Geom.Sample(new[] { PathSeg.Move(pts[i]), segs[i + 1] }, 24));
            }
            else
                for (var i = 0; i < result.Length; i++) result[i] = pts[i].DistanceTo(pts[i + 1]);
            return result;
        }

        /// <summary>Total drawn length of a point object in page millimetres.</summary>
        public static double DrawnLength(SketchObject o, IReadOnlyList<Vec2> pts) => SegmentLengths(o, Geom.Dedupe(pts)).Sum();
    }

    /// <summary>
    /// Element values of a route (engineering calculations, section 1): route length <c>s</c>, mean speed <c>v</c> and travel
    /// time <c>t</c> with <c>t = s / v</c>. The length comes from the drawn route at the map scale (<see cref="MapScale"/>)
    /// unless it is entered (a sketch is rarely true to scale, so the entered road distance wins); the speed comes from
    /// the travel mode (walk 5 km/h, bike 15 km/h, car 50 km/h) unless it is entered or follows from an entered time.
    /// </summary>
    internal static class RouteValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        public const string LengthParam = "length";
        public const string SpeedParam = "speed";
        public const string TimeParam = "time";
        public const string ModeParam = "mode";

        /// <summary>Travel modes with their preset mean speeds (km/h); typical planning values, not taken from a standard.</summary>
        public static readonly (string Value, string En, string De, double Kmh)[] Modes =
        {
            ("walk", "Walking (5 km/h)", "Zu Fuß (5 km/h)", 5),
            ("bike", "Bicycle (15 km/h)", "Fahrrad (15 km/h)", 15),
            ("car", "Car (50 km/h)", "Auto (50 km/h)", 50),
        };

        public static readonly ParamDef ModeDef = Prm.Choice(ModeParam, "Travel mode (speed preset)", "Verkehrsmittel (Geschwindigkeitsvorgabe)", "car",
            Modes.Select(m => (m.Value, m.En, m.De)).ToArray());

        /// <summary>Preset speed of a travel mode in km/h (car for unknown values).</summary>
        public static double PresetKmh(string mode) => Modes.FirstOrDefault(m => m.Value == mode).Kmh is double v && v > 0 ? v : 50;

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static readonly I18nText LengthName = T("Route length (drawn length at the map scale, or entered)", "Routenlänge (gezeichnete Länge im Kartenmaßstab oder eingegeben)");
        private static readonly I18nText SpeedName = T("Mean speed (travel mode preset, or entered)", "Mittlere Geschwindigkeit (Vorgabe des Verkehrsmittels oder eingegeben)");
        private static readonly I18nText TimeName = T("Travel time", "Reisezeit");

        public static readonly IReadOnlyList<VarDef> Vars = new VarDef[]
        {
            new("s", LengthName, T("s", "s"), Quantities.Length) { Unit = "km", Param = LengthParam, Min = 0, Max = 1e8 },
            new("v", SpeedName, T("v", "v"), Quantities.Speed) { Unit = "km/h", Param = SpeedParam, Min = 1e-3, Max = 1e3 },
            new("t", TimeName, T("t", "t"), Quantities.Time_) { Unit = "min", Param = TimeParam, Min = 0, Max = 1e9 },
        };

        public static readonly IReadOnlyList<ParamDef> Params = new[]
        {
            ModeDef, Text(SpeedParam, SpeedName, "km/h"), Text(LengthParam, LengthName, "km"), Text(TimeParam, TimeName, "min"), MapScale.Def, ValueLabels.ShowValues,
        };

        /// <summary>map.route.time: t = s / v (definition of the mean speed) [S2].</summary>
        public static readonly Relation TravelTime =
            new Relation("map.route.time", T("Travel time from route length and mean speed", "Reisezeit aus Routenlänge und mittlerer Geschwindigkeit"), "{t} = {s}/{v}", "t", "s", "v") { Source = "S2" }
                .Solve("t", "{s}/{v}", v => v("s") / v("v"))
                .Solve("s", "{t}·{v}", v => v("t") * v("v"))
                .Solve("v", "{s}/{t}", v => v("s") / v("t"));

        public static readonly IReadOnlyList<Relation> Rels = new[] { TravelTime };

        private static bool Entered(SketchObject o, string key) => ValueModel.ReadEntered(o, Vars.First(x => x.Key == key)).Value is not null;

        private static string F(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>The route length from the drawing at the map scale (metres), or null without a scale.</summary>
        public static double? DrawnMetres(SketchObject o, SketchDocument doc)
        {
            var scale = MapScale.Of(o, doc);
            if (scale is null) return null;
            var pts = doc.ResolvePoints(o);
            if (pts.Count < 2) return null;
            var m = MapScale.DrawnLength(o, pts) * scale.MetresPerMm;
            return m > 0 && !double.IsInfinity(m) ? m : null;
        }

        /// <summary>
        /// The route's own relations: the length from the drawing (unless entered) and the speed preset of the travel mode
        /// (unless the speed is entered, or an entered time and a known length determine it).
        /// </summary>
        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var lengthEntered = Entered(o, "s");
            var drawn = lengthEntered ? null : DrawnMetres(o, ctx.Doc);
            if (drawn is double m)
            {
                var scale = MapScale.Of(o, ctx.Doc)!;
                var mm = MapScale.DrawnLength(o, ctx.Doc.ResolvePoints(o));
                var expr = F(mm, "0.#") + " mm · " + F(scale.MetresPerMm, "0.####") + " m/mm";
                var rel = new Relation("map.route.length", T("Route length from the drawing at the map scale", "Routenlänge aus der Zeichnung im Kartenmaßstab"), "{s} = " + expr, "s")
                    .Solve("s", expr, _ => m);
                yield return BoundRelation.Local(rel, o.Id);
            }
            var lengthKnown = lengthEntered || drawn is not null;
            if (!Entered(o, "v") && !(Entered(o, "t") && lengthKnown))
            {
                var mode = o.Str(ModeParam, "car");
                var kmh = PresetKmh(mode);
                var expr = F(kmh, "0.##") + " km/h";
                var rel = new Relation("map.route.speed", T("Speed preset of the travel mode", "Geschwindigkeitsvorgabe des Verkehrsmittels"), "{v} = " + expr, "v")
                    .Solve("v", expr, _ => kmh / 3.6);
                yield return BoundRelation.Local(rel, o.Id);
            }
        }
    }
}
