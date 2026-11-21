using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Abstract pictograms for markers and points of interest, drawn independently from simple primitives (no third-party
    /// icon set). Each glyph is authored in unit coordinates (-1..1, y down) and drawn at a centre with a radius in actual
    /// millimetres, in <c>ink</c> on a background of colour <c>bg</c> (cut-outs such as windows use <c>bg</c>).
    /// </summary>
    internal static class Glyphs
    {
        public static void Draw(Pen p, string kind, Vec2 c, double r, string ink, string bg)
        {
            Vec2 U(double u, double v) => c + new Vec2(u * r, v * r);
            List<Vec2> Us(params double[] uv)
            {
                var l = new List<Vec2>(uv.Length / 2);
                for (var i = 0; i + 1 < uv.Length; i += 2) l.Add(U(uv[i], uv[i + 1]));
                return l;
            }
            var solid = S.FillOnly(ink);
            var hole = S.FillOnly(bg);
            Style Line(double w) => S.Stroke(ink, Math.Max(0.12, w * r));
            void Poly(Style s, bool closed, params double[] uv) => p.Add(Prim.Polyline(Us(uv), s, closed), MapTags.Glyph);
            void Box(double u0, double v0, double u1, double v1, Style s) => p.Add(Prim.Rect(c.X + u0 * r, c.Y + v0 * r, (u1 - u0) * r, (v1 - v0) * r, s), MapTags.Glyph);
            void Dot(double u, double v, double rr, Style s) { var q = U(u, v); p.Add(Prim.Circle(q.X, q.Y, rr * r, s), MapTags.Glyph); }
            void Letter(string t) => p.Add(new TextPrim(U(0, 0.04), t, new TextStyle { Size = 1.45 * r, Bold = true, Color = ink }) { Tag = MapTags.Glyph });

            switch (kind)
            {
                case "start": // "go" triangle
                    Poly(solid, true, -0.38, -0.55, 0.6, 0, -0.38, 0.55);
                    break;
                case "finish": // chequered flag on a pole
                    Poly(Line(0.12), false, -0.55, 0.8, -0.55, -0.75);
                    {
                        double x0 = -0.5, y0 = -0.72, cw = 0.36, ch = 0.3;
                        Box(x0, y0, x0 + 3 * cw, y0 + 2 * ch, S.Filled(bg, S.Stroke(ink, Math.Max(0.1, 0.06 * r))));
                        for (var i = 0; i < 3; i++)
                            for (var j = 0; j < 2; j++)
                                if ((i + j) % 2 == 0) Box(x0 + i * cw, y0 + j * ch, x0 + (i + 1) * cw, y0 + (j + 1) * ch, solid);
                    }
                    break;
                case "via":
                    Dot(0, 0, 0.36, solid);
                    break;
                case "pin":
                    Dot(0, 0, 0.38, solid);
                    break;
                case "building": // house with door
                    Poly(solid, true, -0.62, 0.66, -0.62, -0.05, 0, -0.66, 0.62, -0.05, 0.62, 0.66);
                    Box(-0.17, 0.2, 0.17, 0.66, hole);
                    break;
                case "parking":
                    Letter("P");
                    break;
                case "fuel": // pump with window and hose
                    Box(-0.62, -0.66, 0.16, 0.7, solid);
                    Box(-0.47, -0.5, 0.01, -0.12, hole);
                    Poly(Line(0.12), false, 0.16, -0.1, 0.44, -0.1, 0.44, 0.42, 0.62, 0.42, 0.62, -0.38, 0.46, -0.56);
                    break;
                case "charging": // lightning bolt
                    Poly(solid, true, 0.14, -0.82, -0.46, 0.1, -0.06, 0.1, -0.16, 0.82, 0.46, -0.12, 0.06, -0.12);
                    break;
                case "hospital": // "H" sign (deliberately not the protected Red Cross emblem)
                    Letter("H");
                    break;
                case "airport": // aircraft seen from above, nose up
                    Poly(solid, true,
                        0, -0.86, 0.1, -0.74, 0.1, -0.26, 0.82, 0.14, 0.82, 0.3, 0.1, 0.1, 0.1, 0.5, 0.32, 0.67, 0.32, 0.8, 0, 0.71,
                        -0.32, 0.8, -0.32, 0.67, -0.1, 0.5, -0.1, 0.1, -0.82, 0.3, -0.82, 0.14, -0.1, -0.26, -0.1, -0.74);
                    break;
                case "harbour": // anchor
                    {
                        var ring = U(0, -0.62);
                        p.Add(Prim.Circle(ring.X, ring.Y, 0.15 * r, Line(0.11)), MapTags.Glyph);
                        Poly(Line(0.12), false, 0, -0.47, 0, 0.72);
                        Poly(Line(0.12), false, -0.34, -0.26, 0.34, -0.26);
                        p.Add(Prim.Polyline(Geo.ArcPoints(U(0, 0.08), 0.62 * r, 0.62 * r, Geo.DegToRad(15), Geo.DegToRad(150), 16), Line(0.12)), MapTags.Glyph);
                        Poly(solid, true, -0.72, 0.1, -0.5, 0.3, -0.75, 0.38);
                        Poly(solid, true, 0.72, 0.1, 0.5, 0.3, 0.75, 0.38);
                    }
                    break;
                case "station": // train front on rails
                    p.Add(Geom.RoundedRect(c.X - 0.5 * r, c.Y - 0.78 * r, r, 1.22 * r, 0.22 * r, solid), MapTags.Glyph);
                    Box(-0.34, -0.58, 0.34, -0.12, hole);
                    Dot(-0.24, 0.2, 0.09, hole);
                    Dot(0.24, 0.2, 0.09, hole);
                    Poly(Line(0.11), false, -0.28, 0.5, -0.52, 0.84);
                    Poly(Line(0.11), false, 0.28, 0.5, 0.52, 0.84);
                    break;
                // transport modes of the distance callout
                case "car":
                    Poly(solid, true, -0.9, 0.25, -0.9, -0.05, -0.62, -0.12, -0.36, -0.48, 0.34, -0.48, 0.6, -0.12, 0.9, -0.04, 0.9, 0.25);
                    Box(-0.3, -0.38, 0.0, -0.12, hole);
                    Box(0.06, -0.38, 0.3, -0.12, hole);
                    Dot(-0.5, 0.28, 0.2, S.Filled(bg, S.Stroke(ink, Math.Max(0.12, 0.1 * r))));
                    Dot(0.5, 0.28, 0.2, S.Filled(bg, S.Stroke(ink, Math.Max(0.12, 0.1 * r))));
                    break;
                case "walk":
                    Dot(0.05, -0.72, 0.17, solid);
                    Poly(Line(0.14), false, 0.02, -0.48, -0.06, 0.12);
                    Poly(Line(0.14), false, -0.06, 0.12, -0.36, 0.8);
                    Poly(Line(0.14), false, -0.06, 0.12, 0.22, 0.44, 0.3, 0.8);
                    Poly(Line(0.12), false, -0.36, -0.08, -0.12, -0.36, 0.24, -0.2, 0.4, 0.06);
                    break;
                case "bike":
                    {
                        var w = Line(0.1);
                        var a = U(-0.48, 0.3); var b = U(0.48, 0.3);
                        p.Add(Prim.Circle(a.X, a.Y, 0.36 * r, w), MapTags.Glyph);
                        p.Add(Prim.Circle(b.X, b.Y, 0.36 * r, w), MapTags.Glyph);
                        Poly(w, false, -0.48, 0.3, -0.14, -0.22, 0.32, -0.22, 0.48, 0.3);
                        Poly(w, false, -0.14, -0.22, 0.04, 0.3, 0.32, -0.22);
                        Poly(w, false, 0.26, -0.42, 0.42, -0.42);
                        Poly(w, false, -0.22, -0.36, -0.04, -0.36);
                    }
                    break;
                case "transit":
                    p.Add(Geom.RoundedRect(c.X - 0.56 * r, c.Y - 0.8 * r, 1.12 * r, 1.4 * r, 0.2 * r, solid), MapTags.Glyph);
                    Box(-0.42, -0.6, 0.42, -0.1, hole);
                    Dot(-0.3, 0.3, 0.09, hole);
                    Dot(0.3, 0.3, 0.09, hole);
                    Poly(Line(0.12), false, -0.36, 0.6, -0.36, 0.86);
                    Poly(Line(0.12), false, 0.36, 0.6, 0.36, 0.86);
                    break;
                default:
                    Dot(0, 0, 0.3, solid);
                    break;
            }
        }
    }

    /// <summary>Teardrop pin and round badge outlines shared by markers and POIs (design coordinates).</summary>
    internal static class PinShape
    {
        /// <summary>Design box of the pin view.</summary>
        public const double W = 8, H = 11;
        /// <summary>Pin head centre (design) and radius; the tip is at the bottom centre of the box.</summary>
        public static readonly Vec2 Head = new(0, -1.5);
        public const double R = 4;
        public static readonly Vec2 Tip = new(0, 5.5);
        /// <summary>Design box of the badge (symbol) view.</summary>
        public const double Badge = 7;

        public static List<Vec2> Outline()
        {
            var d = Tip.DistanceTo(Head);
            var a = Math.Acos(R / d);
            var pts = new List<Vec2> { Tip };
            pts.AddRange(Geo.ArcPoints(Head, R, R, Math.PI / 2 + a, 2 * Math.PI - 2 * a, 32));
            return pts;
        }

        /// <summary>True if the colour is dark (becomes black in monochrome output; glyphs on it are white).</summary>
        public static bool IsDark(string color) => SvgRenderer.Luminance(color) < 0.35;

        /// <summary>Draws a pin with a glyph (or text) in its head.</summary>
        public static void DrawPin(Pen p, string fill, string glyph, string? text)
        {
            var dark = IsDark(fill);
            var outline = dark ? Palette.White : Palette.Ink;
            p.Poly(Outline(), S.Filled(fill, S.Stroke(outline, 0.35)), true, MapTags.Marker);
            DrawCenter(p, p.V(Head), 2.5 * p.K, fill, glyph, text);
        }

        /// <summary>Draws a round badge with a glyph (or text).</summary>
        public static void DrawBadge(Pen p, string fill, string glyph, string? text)
        {
            var dark = IsDark(fill);
            p.Ellipse(0, 0, Badge / 2, Badge / 2, S.Filled(fill, S.Stroke(dark ? Palette.White : Palette.Ink, 0.35)), MapTags.Marker);
            DrawCenter(p, Vec2.Zero, 2.35 * p.K, fill, glyph, text);
        }

        private static void DrawCenter(Pen p, Vec2 c, double r, string fill, string glyph, string? text)
        {
            var ink = IsDark(fill) ? Palette.White : Palette.Ink;
            if (!string.IsNullOrWhiteSpace(text))
            {
                p.TextAt(c, text!.Trim(), 1.5 * r, 1.7 * r, 1.5 * r, bold: true, color: ink, tag: MapTags.Glyph);
                return;
            }
            Glyphs.Draw(p, glyph, c, r, ink, fill);
        }

        public static IEnumerable<Anchor> PinAnchors(Pen p, SketchObject o) => new[] { Anchor.Point(AnchorRole.Port, p.V(Tip), "loc") };
        public static IEnumerable<Anchor> CenterAnchors(Pen p, SketchObject o) => new[] { Anchor.Point(AnchorRole.Port, Vec2.Zero, "loc") };
    }

    /// <summary>Start, finish and via markers as pin or badge; the location (pin tip / badge centre) is a docking port.</summary>
    internal static class MarkerType
    {
        public const string TypeId = "map.marker";

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Marker", "Marker", "start",
            ("start", "Start", "Start"), ("finish", "Finish", "Ziel"), ("via", "Waypoint (via)", "Zwischenziel"));

        private static (string Fill, string Glyph) Spec(string kind) => kind switch
        {
            "finish" => (Palette.Finish, "finish"),
            "via" => (Palette.White, "via"),
            _ => (Palette.Start, "start"),
        };

        public static MapBoxType Create()
        {
            void Pin(Pen p, SketchObject o, RenderContext ctx) { var (f, g) = Spec(o.Str("kind", "start")); PinShape.DrawPin(p, f, g, o.Text); }
            void Badge(Pen p, SketchObject o, RenderContext ctx) { var (f, g) = Spec(o.Str("kind", "start")); PinShape.DrawBadge(p, f, g, o.Text); }
            return new MapBoxType(TypeId, new I18nText("Route marker", "Routenmarker"), MapCategories.Routes,
                new[] { "marker", "start", "finish", "destination", "goal", "waypoint", "via", "pin", "flag", "origin",
                        "marker", "start", "ziel", "zwischenziel", "wegpunkt", "stecknadel", "flagge", "ausgangspunkt" },
                MapViews.Pin(PinShape.W, PinShape.H, Pin) with { Anchors = PinShape.PinAnchors },
                MapViews.Symbol(PinShape.Badge, PinShape.Badge, Badge) with { Anchors = PinShape.CenterAnchors })
            {
                Params = new[] { KindDef },
                TextEditable = true,
                CompanionIds = new[] { RouteType.TypeId, Callout.TypeId, StopType.TypeId },
                Extra = (o, lang) => new[] { Prm.Cycle(o, "map.marker.kind", KindDef, new I18nText("Marker", "Marker")) },
            };
        }
    }

    /// <summary>Points of interest: abstract pictograms as badge or pin; the location is a docking port.</summary>
    internal static class PoiType
    {
        public const string TypeId = "map.poi";

        public static readonly (string Value, string En, string De, string Color)[] Kinds =
        {
            ("pin", "Location", "Ort", "#c62828"),
            ("building", "Building", "Gebäude", "#424242"),
            ("parking", "Parking", "Parkplatz", "#0d47a1"),
            ("fuel", "Fuel station", "Tankstelle", "#4e342e"),
            ("charging", "Charging station", "Ladestation", "#1b5e20"),
            ("hospital", "Hospital", "Krankenhaus", "#b71c1c"),
            ("airport", "Airport", "Flughafen", "#283593"),
            ("harbour", "Harbour", "Hafen", "#01579b"),
            ("station", "Station", "Bahnhof", "#37474f"),
        };

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Place type", "Ortsart", "pin", Kinds.Select(k => (k.Value, k.En, k.De)).ToArray());

        public static string ColorOf(string kind) => Kinds.FirstOrDefault(k => k.Value == kind).Color ?? Kinds[0].Color;

        public static MapBoxType Create()
        {
            void Badge(Pen p, SketchObject o, RenderContext ctx) { var k = o.Str("kind", "pin"); PinShape.DrawBadge(p, ColorOf(k), k, null); }
            void Pin(Pen p, SketchObject o, RenderContext ctx) { var k = o.Str("kind", "pin"); PinShape.DrawPin(p, ColorOf(k), k, null); }
            return new MapBoxType(TypeId, new I18nText("Point of interest", "Ort von Interesse (POI)"), MapCategories.Poi,
                new[] { "poi", "point of interest", "pin", "location", "building", "parking", "fuel", "gas station", "petrol", "charging", "ev",
                        "hospital", "airport", "harbour", "harbor", "port", "station", "train", "icon",
                        "ort", "standort", "gebäude", "parkplatz", "tankstelle", "ladestation", "laden", "krankenhaus", "klinik", "flughafen",
                        "hafen", "bahnhof", "symbol" },
                MapViews.Symbol(PinShape.Badge, PinShape.Badge, Badge) with { Anchors = PinShape.CenterAnchors },
                MapViews.Pin(PinShape.W, PinShape.H, Pin) with { Anchors = PinShape.PinAnchors })
            {
                Params = new[] { KindDef },
                CompanionIds = new[] { Place.TypeId, Callout.TypeId, RouteType.TypeId },
                Extra = (o, lang) => new[] { Prm.Cycle(o, "map.poi.kind", KindDef, new I18nText("Place type", "Ortsart")) },
            };
        }
    }

    /// <summary>Numbered stop (circle or rounded square); the centre is a docking port.</summary>
    internal static class StopType
    {
        public const string TypeId = "map.stop";

        private static readonly ParamDef NumberDef = Prm.Int("number", "Number", "Nummer", 1, 0, 999);
        private static readonly ParamDef ShapeDef = Prm.Choice("shape", "Shape", "Form", "circle", ("circle", "Circle", "Kreis"), ("square", "Square", "Quadrat")).WithOptionIcons(IconIds.ForShape);

        public static string Label(SketchObject o) => !string.IsNullOrWhiteSpace(o.Text) ? o.Text!.Trim() : Geom.I(o.Int("number", 1));

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                var style = S.Filled(Palette.Stop, S.Stroke(Palette.White, 0.4));
                if (o.Str("shape", "circle") == "square") p.RBox(-3, -3, 3, 3, 1.0, style, MapTags.Marker);
                else p.Ellipse(0, 0, 3, 3, style, MapTags.Marker);
                p.Text(0, 0.05, Label(o), 3.4, 4.6, 4.0, bold: true, color: Palette.White, tag: MapTags.Glyph);
            }
            return new MapBoxType(TypeId, new I18nText("Numbered stop", "Nummerierter Halt"), MapCategories.Routes,
                new[] { "stop", "numbered", "number", "station", "waypoint", "sequence", "halt", "haltestelle", "nummer", "wegpunkt", "reihenfolge" },
                MapViews.Symbol(6, 6, Draw) with { Anchors = PinShape.CenterAnchors })
            {
                Params = new[] { NumberDef, ShapeDef },
                TextEditable = true,
                CompanionIds = new[] { RouteType.TypeId, MarkerType.TypeId, Callout.TypeId },
                Extra = (o, lang) => new[]
                {
                    new ContextAction("map.stop.next", new I18nText("Next number", "Nächste Nummer"), IconIds.Increase, x => x.WithParam("number", Math.Min(999, x.Int("number", 1) + 1))) { Group = "number" },
                    new ContextAction("map.stop.prev", new I18nText("Previous number", "Vorige Nummer"), IconIds.Decrease, x => x.WithParam("number", Math.Max(0, x.Int("number", 1) - 1)))
                    {
                        Group = "number",
                        IsEnabled = o.Int("number", 1) > 0,
                    },
                    Prm.Cycle(o, "map.stop.shape", ShapeDef, new I18nText("Shape", "Form")),
                },
            };
        }
    }

    /// <summary>Distance/time callout with a pointer tail (tail tip is a docking port) and an optional transport mode pictogram.</summary>
    internal static class Callout
    {
        public const string TypeId = "map.callout";

        public static readonly string[] Units = { "km", "mi", "m" };
        private static readonly ParamDef DistanceDef = Prm.Num("distance", "Distance", "Entfernung", 12, 0, 100000, null, 0.1);
        private static readonly ParamDef UnitDef = Prm.Choice("unit", "Unit", "Einheit", "km", ("km", "Kilometres", "Kilometer"), ("mi", "Miles", "Meilen"), ("m", "Metres", "Meter"));
        private static readonly ParamDef TimeDef = Prm.Num("time", "Travel time", "Fahrzeit", 15, 0, 100000, "min", 1);
        private static readonly ParamDef ModeDef = Prm.Choice("mode", "Mode", "Verkehrsmittel", "car",
            ("car", "Car", "Auto"), ("walk", "Walking", "Zu Fuß"), ("bike", "Bicycle", "Fahrrad"), ("transit", "Public transport", "ÖPNV"), ("none", "None", "Keins"));
        private static readonly ParamDef TailDef = Prm.Choice("tail", "Pointer", "Zeiger", "left", ("left", "Bottom left", "Unten links"), ("right", "Bottom right", "Unten rechts"), ("none", "None", "Keiner"));

        public static string DistanceText(SketchObject o, Lang lang)
        {
            var d = o.Num("distance", 12);
            return d <= 0 ? "" : Fmt.Num(d, lang, d < 100 ? 1 : 0) + " " + o.Str("unit", "km");
        }

        public static string TimeText(SketchObject o)
        {
            var t = Math.Round(o.Num("time", 15));
            if (t <= 0) return "";
            if (t < 60) return Geom.I((int)t) + " min";
            var h = (int)(t / 60);
            var m = (int)(t - h * 60);
            return m == 0 ? Geom.I(h) + " h" : Geom.I(h) + " h " + m.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + " min";
        }

        /// <summary>Parses "90", "90 min", "1:30", "1 h 30", "1.5 h" into minutes.</summary>
        public static double ParseMinutes(string text, Lang lang)
        {
            var t = (text ?? "").Trim().ToLowerInvariant().Replace(" ", "");
            FormatException Bad() => new(lang == Lang.De ? "Keine gültige Zeit: „" + text + "“" : "Not a valid time: “" + text + "”");
            try
            {
                if (t.Contains(":"))
                {
                    var parts = t.Split(':');
                    if (parts.Length != 2) throw Bad();
                    return Fmt.Parse(parts[0], lang, 0, 10000) * 60 + Fmt.Parse(parts[1], lang, 0, 59.999);
                }
                if (t.EndsWith("min", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 3);
                var hi = t.IndexOf('h');
                if (hi >= 0)
                {
                    var hours = Fmt.Parse(t.Substring(0, hi), lang, 0, 10000);
                    var rest = t.Substring(hi + 1);
                    return hours * 60 + (rest.Length == 0 ? 0 : Fmt.Parse(rest, lang, 0, 59.999));
                }
                return Fmt.Parse(t, lang, 0, 100000);
            }
            catch (FormatException) { throw Bad(); }
        }

        public static MapBoxType Create()
        {
            void Draw(Pen p, SketchObject o, RenderContext ctx)
            {
                var tail = o.Str("tail", "left");
                var bottom = tail == "none" ? 7.0 : 3.0;
                var body = S.Filled(Palette.White, S.Stroke(Palette.Ink, 0.35));
                p.RBox(-16, -7, 16, bottom, 1.6, body, MapTags.Frame);
                if (tail != "none")
                {
                    var s = tail == "right" ? -1 : 1;
                    var a = new Vec2(-12 * s, bottom); var b = new Vec2(-7 * s, bottom); var tip = new Vec2(-14.5 * s, 7);
                    // White fill covers the body border between the tail's base points; the two outer edges are drawn.
                    p.Poly(new[] { a + new Vec2(0, -0.3), tip, b + new Vec2(0, -0.3) }, S.FillOnly(Palette.White), true, MapTags.Tail);
                    p.Poly(new[] { a, tip, b }, S.Stroke(Palette.Ink, 0.35), false, MapTags.Tail);
                }
                var lines = !string.IsNullOrWhiteSpace(o.Text)
                    ? o.Text!.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).Take(3).ToList()
                    : new[] { DistanceText(o, ctx.Lang), TimeText(o) }.Where(l => l.Length > 0).ToList();
                var mode = o.Str("mode", "car");
                var bodyH = bottom + 7;
                var cy = (-7 + bottom) / 2;
                var x0 = -16 + 1.4;
                if (mode != "none")
                {
                    var g = Math.Min(bodyH - 2.4, 7.5);
                    var gc = p.V(x0 + g / 2, cy);
                    Glyphs.Draw(p, mode, gc, g / 2 * p.K, Palette.Ink, Palette.White);
                    x0 += g + 1.2;
                }
                var maxW = 16 - 1.2 - x0;
                if (lines.Count == 0) return;
                if (lines.Count == 1) { p.Text(x0, cy, lines[0], 3.6, maxW, bodyH - 2, TextAnchor.Start, bold: true, color: Palette.Ink); return; }
                var step = Math.Min(4.4, (bodyH - 1.6) / lines.Count);
                for (var i = 0; i < lines.Count; i++)
                {
                    var y = cy + (i - (lines.Count - 1) / 2.0) * step;
                    p.Text(x0, y, lines[i].Trim(), i == 0 ? 3.4 : 2.8, maxW, step * 0.95, TextAnchor.Start, bold: i == 0, color: Palette.Ink);
                }
            }
            IEnumerable<Anchor> Anchors(Pen p, SketchObject o)
            {
                var tail = o.Str("tail", "left");
                var tip = tail switch { "right" => new Vec2(14.5, 7), "none" => new Vec2(0, 7), _ => new Vec2(-14.5, 7) };
                yield return Anchor.Point(AnchorRole.Port, p.V(tip), "tip");
            }
            return new MapBoxType(TypeId, new I18nText("Distance/time callout", "Entfernungs-/Zeitangabe"), MapCategories.Routes,
                new[] { "callout", "distance", "time", "duration", "travel time", "km", "miles", "eta",
                        "entfernung", "distanz", "zeit", "dauer", "fahrzeit", "reisezeit", "angabe", "sprechblase" },
                MapViews.Plan(32, 14, Draw) with { Anchors = Anchors })
            {
                Params = new[] { DistanceDef, UnitDef, TimeDef, ModeDef, TailDef },
                Uniform = false,
                TextEditable = true,
                CompanionIds = new[] { RouteType.TypeId, MarkerType.TypeId },
                Extra = (o, lang) => new[]
                {
                    ContextAction.WithInput("map.callout.distance", new I18nText("Distance…", "Entfernung…"), IconIds.Text,
                        new I18nText("Distance (e.g. 12.5 km, 8 mi)", "Entfernung (z. B. 12,5 km, 8 mi)"), Fmt.Num(o.Num("distance", 12), lang) + " " + o.Str("unit", "km"),
                        (x, text) =>
                        {
                            var (v, unit) = Fmt.ParseWithUnit(text, lang, 0, 100000, Units);
                            var y = x.WithParam("distance", v);
                            return unit is null ? y : y.WithParam("unit", unit);
                        }) with { Group = "value" },
                    ContextAction.WithInput("map.callout.time", new I18nText("Travel time…", "Fahrzeit…"), IconIds.Text,
                        new I18nText("Travel time in minutes (or h:mm)", "Fahrzeit in Minuten (oder h:mm)"), Fmt.Num(o.Num("time", 15), lang, 0),
                        (x, text) => x.WithParam("time", Math.Round(ParseMinutes(text, lang), 1)), "min") with { Group = "value" },
                    Prm.Cycle(o, "map.callout.unit", UnitDef, new I18nText("Unit", "Einheit")),
                    Prm.Cycle(o, "map.callout.mode", ModeDef, new I18nText("Mode", "Verkehrsmittel")),
                    Prm.Cycle(o, "map.callout.tail", TailDef, new I18nText("Pointer", "Zeiger")),
                },
            };
        }
    }
}
