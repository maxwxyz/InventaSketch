using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// ADAS sensor symbols (camera, radar, lidar, ultrasonic) and perception/motion overlays that are box objects:
    /// 2D bounding box, 3D cuboid, occupancy grid patch, conflict point marker and TTC label. Sensors look along +x;
    /// their back is a <see cref="AnchorRole.Mount"/> point and their aperture a <see cref="AnchorRole.Port"/>, which is
    /// where a field of view (<c>auto.fov</c>) is attached.
    /// </summary>
    internal static class Adas
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Aperture position of each sensor type in normalised box coordinates (FoV attachment point).</summary>
        public static readonly IReadOnlyDictionary<string, (double U, double V)> Apertures = new Dictionary<string, (double, double)>
        {
            ["auto.sensor.camera"] = (0.5, 0),
            ["auto.sensor.radar"] = (-0.5 + 2.8 / 8, 0),
            ["auto.sensor.lidar"] = (0, 0),
            ["auto.sensor.ultrasonic"] = (-0.6 / 5, 0),
        };

        public static IEnumerable<AutoType> Types()
        {
            yield return Sensor("auto.sensor.camera", T("Camera", "Kamera"), 8, 6,
                new[] { "camera", "mono camera", "stereo", "front camera", "sensor", "kamera", "frontkamera", "bildsensor" }, Camera);
            yield return Sensor("auto.sensor.radar", T("Radar sensor", "Radarsensor"), 8, 6,
                new[] { "radar", "sensor", "long range radar", "corner radar", "radarsensor", "abstandsradar" }, Radar);
            yield return Sensor("auto.sensor.lidar", T("Lidar sensor", "Lidarsensor"), 6, 6,
                new[] { "lidar", "laser scanner", "sensor", "point cloud", "lidarsensor", "laserscanner", "punktwolke" }, Lidar);
            yield return Sensor("auto.sensor.ultrasonic", T("Ultrasonic sensor", "Ultraschallsensor"), 5, 5,
                new[] { "ultrasonic", "parking sensor", "uss", "sensor", "ultraschall", "parksensor", "einparkhilfe" }, Ultrasonic);
            yield return BBox2D();
            yield return BBox3D();
            yield return Grid();
            yield return Conflict();
            yield return Ttc();
        }

        // ------------------------------------------------------------------ sensors

        private static AutoType Sensor(string id, I18nText name, double w, double h, string[] kw, Action<Pen, SketchObject> draw)
        {
            var (u, v) = Apertures[id];
            return new AutoType(id, name, AutoCategories.Sensors, kw,
                Views.SymbolV(w, h, draw) with
                {
                    Anchors = (p, o) => new[]
                    {
                        Anchor.Point(AnchorRole.Mount, p.V(-w / 2, 0), "mount"),
                        Anchor.Point(AnchorRole.Port, p.V(u * w, v * h), "aperture"),
                    },
                });
        }

        private static void Camera(Pen p, SketchObject o)
        {
            p.Poly(S.Opaque with { StrokeWidth = 0.35 }, true, 0.6, -1.0, 4.0, -2.4, 4.0, 2.4, 0.6, 1.0);
            p.RBox(-4, -2.2, 0.8, 2.2, 0.5, S.Opaque with { StrokeWidth = 0.35 }, AutoTags.Body);
            p.Round(-2.6, -1.0, 0.45, S.Fine, "indicator");
        }

        private static void Radar(Pen p, SketchObject o)
        {
            p.RBox(-4, -2.6, -1.2, 2.6, 0.4, S.Opaque with { StrokeWidth = 0.35 }, AutoTags.Body);
            p.Line(-3.2, -1.6, -3.2, 1.6, S.Thin, "antenna");
            p.Line(-2.2, -1.6, -2.2, 1.6, S.Thin, "antenna");
            foreach (var r in new[] { 1.6, 3.0, 4.4 }) p.Arc(-1.2, 0, r, r, -40, 80, S.Fine, "wave");
        }

        private static void Lidar(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 2.8, S.Opaque with { StrokeWidth = 0.35 }, AutoTags.Body);
            p.Circle(0, 0, 1.0, S.Fine, "window");
            p.PolyD(Pen.ArcD(0, 0, 2.0, 2.0, 200, 250, 32), S.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }, false, "rotation");
            p.Line(1.0, 0, 2.8, 0, S.Fine, "beam");
        }

        private static void Ultrasonic(Pen p, SketchObject o)
        {
            p.Circle(-1.1, 0, 1.4, S.Opaque with { StrokeWidth = 0.35 }, AutoTags.Body);
            p.Round(-1.1, 0, 0.4, S.Fine, "membrane");
            foreach (var r in new[] { 1.6, 2.6 }) p.Arc(-0.6, 0, r, r, -45, 90, S.Thin, "wave");
        }

        // ------------------------------------------------------------------ detections

        private static string Conf(double c) => c.ToString("0.00", CultureInfo.InvariantCulture);

        private static readonly ParamDef Label = Prm.Text("label", "Class label", "Klassenlabel", "car");
        private static readonly ParamDef Confidence = Prm.Num("confidence", "Confidence", "Konfidenz", 0.92, 0, 1, null, 0.01);
        private static readonly ParamDef ShowConfidence = Prm.Bool("showConfidence", "Show confidence", "Konfidenz zeigen", true);

        private static string DetText(SketchObject o)
        {
            var label = o.Str("label", "car");
            return o.Flag("showConfidence", true) ? (label + " " + Conf(Geo.Clamp(o.Num("confidence", 0.92), 0, 1))).Trim() : label;
        }

        /// <summary>Label tab in the top-left corner (actual coordinates, kept inside the given rectangle).</summary>
        private static void Tab(Pen p, double x0, double y0, double maxW, double maxH, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var fs = Shapes.FitText(text, Math.Min(3.0, maxH * 0.22), maxW - 1.2, maxH * 0.3);
            if (fs < 0.3) return;
            var tw = text.Length * fs * Shapes.CharWidth + 1.2;
            var th = fs + 0.8;
            p.Add(Prim.Rect(x0, y0, tw, th, S.Thin with { Fill = Fill.Solid("#ffffff") }), "tab");
            p.Add(new TextPrim(new Vec2(x0 + 0.6, y0 + th / 2), text, new TextStyle { Size = fs, Anchor = TextAnchor.Start }) { Tag = AutoTags.Label });
        }

        private static AutoType BBox2D()
        {
            var dashed = Prm.Bool("dashed", "Dashed outline", "Gestrichelter Rahmen", false);
            void Draw(Pen p, SketchObject o)
            {
                double hw = p.W / 2, hh = p.H / 2;
                p.Add(Prim.Rect(-hw, -hh, p.W, p.H, o.Flag("dashed", false) ? S.Vis with { Dash = DashStyle.Dash } : S.Vis), "bbox");
                var c = Math.Min(p.W, p.H) * 0.12;
                foreach (var sx in new[] { -1.0, 1.0 })
                    foreach (var sy in new[] { -1.0, 1.0 })
                    {
                        var corner = new Vec2(sx * hw, sy * hh);
                        p.Add(Prim.Polyline(new[] { corner - new Vec2(0, sy * c), corner, corner - new Vec2(sx * c, 0) }, S.Vis with { StrokeWidth = 0.8 }), "corner");
                    }
                Tab(p, -hw, -hh, p.W, p.H, DetText(o));
            }
            return new AutoType("auto.det.bbox2d", T("Bounding box (2D)", "Begrenzungsrahmen (2D)"), AutoCategories.Perception,
                new[] { "bounding box", "detection", "object detection", "bbox", "2d box", "begrenzungsrahmen", "detektion", "objekterkennung" },
                new AutoView("image", T("Image plane", "Bildebene"), IconIds.Front, 30, 20, Draw) { Fluid = true })
            {
                Params = new[] { Label, Confidence, ShowConfidence, dashed },
                Uniform = false,
                Extra = o => new[] { Prm.Toggle(o, "showConfidence", true, T("Show confidence", "Konfidenz zeigen")), Prm.Toggle(o, "dashed", false, T("Dashed outline", "Gestrichelter Rahmen")) },
            };
        }

        private static AutoType BBox3D()
        {
            void Iso(Pen p, SketchObject o)
            {
                double hw = p.W / 2, hh = p.H / 2;
                double ox = p.W * 0.28, oy = p.H * 0.3;
                // front face (lower left), back face offset up-right
                var f = new[] { new Vec2(-hw, -hh + oy), new Vec2(hw - ox, -hh + oy), new Vec2(hw - ox, hh), new Vec2(-hw, hh) };
                var b = f.Select(v => v + new Vec2(ox, -oy)).ToArray();
                p.Add(Prim.Polygon(f, S.Vis), "face");
                p.Add(Prim.Polyline(new[] { f[0], b[0], b[1], b[2], f[2] }, S.Vis), "edge");
                p.Add(Prim.Line(f[1], b[1], S.Vis), "edge");
                p.Add(Prim.Polyline(new[] { f[3], b[3], b[2] }, S.Hid), "hidden");
                p.Add(Prim.Line(b[0], b[3], S.Hid), "hidden");
                // heading arrow on the top face
                var mid = (f[0] + f[1] + b[1] + b[0]) * 0.25;
                var dir = (f[1] - f[0]) * 0.3;
                p.Add(Prim.Line(mid - dir, mid + dir, S.Fine with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }), "heading");
                Tab(p, -hw, -hh + oy, p.W - ox, p.H - oy, DetText(o));
            }
            void Top(Pen p, SketchObject o)
            {
                double hw = p.W / 2, hh = p.H / 2;
                p.Add(Prim.Rect(-hw, -hh, p.W, p.H, S.Vis), "bbox");
                p.Add(Prim.Line(new Vec2(-hw, -hh), new Vec2(hw, hh), S.Thin), "diagonal");
                p.Add(Prim.Line(new Vec2(-hw, hh), new Vec2(hw, -hh), S.Thin), "diagonal");
                var tip = Math.Min(p.W * 0.25, p.H * 0.6);
                p.Add(Prim.Polygon(new[] { new Vec2(hw, 0), new Vec2(hw - tip, -tip * 0.6), new Vec2(hw - tip, tip * 0.6) }, S.Fine), "heading");
            }
            return new AutoType("auto.det.bbox3d", T("3D cuboid", "3D-Quader"), AutoCategories.Perception,
                new[] { "3d box", "cuboid", "bounding box", "detection", "object", "3d-box", "quader", "detektion", "objektliste" },
                new AutoView("iso", Views.Iso, IconIds.Iso, 30, 22, Iso) { Fluid = true },
                new AutoView("top", Views.Top, IconIds.Top, 30, 14, Top) { Fluid = true })
            {
                Params = new[] { Label, Confidence, ShowConfidence },
                Uniform = false,
                Extra = o => new[] { Prm.Toggle(o, "showConfidence", true, T("Show confidence", "Konfidenz zeigen")) },
            };
        }

        /// <summary>Deterministic pseudo-random value in [0, 1) per cell (stable across hosts and runtimes).</summary>
        private static double Hash(int seed, int r, int c)
        {
            unchecked
            {
                var h = (uint)(seed * 73856093) ^ (uint)(r * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xffffff) / (double)0x1000000;
            }
        }

        private static AutoType Grid()
        {
            var rows = Prm.Int("rows", "Rows", "Zeilen", 8, 1, 40);
            var cols = Prm.Int("cols", "Columns", "Spalten", 8, 1, 40);
            var occ = Prm.Num("occupancy", "Occupied share", "Belegter Anteil", 0.3, 0, 1, null, 0.05);
            var seed = Prm.Int("seed", "Pattern", "Muster", 1, 0, 999);
            void Draw(Pen p, SketchObject o)
            {
                int nr = Math.Max(1, Math.Min(40, o.Int("rows", 8))), nc = Math.Max(1, Math.Min(40, o.Int("cols", 8)));
                double hw = p.W / 2, hh = p.H / 2, cw = p.W / nc, ch = p.H / nr;
                var share = Geo.Clamp(o.Num("occupancy", 0.3), 0, 1);
                var sd = o.Int("seed", 1);
                var hatch = S.Thin with { Fill = Fill.Hatched(new HatchPattern(45, Geo.Clamp(Math.Min(cw, ch) / 4, 0.5, 2.5))) };
                for (var r = 0; r < nr; r++)
                    for (var c = 0; c < nc; c++)
                        if (Hash(sd, r, c) < share) p.Add(Prim.Rect(-hw + c * cw, -hh + r * ch, cw, ch, hatch), "occupied");
                for (var c = 1; c < nc; c++) p.Add(Prim.Line(-hw + c * cw, -hh, -hw + c * cw, hh, S.Thin), "grid");
                for (var r = 1; r < nr; r++) p.Add(Prim.Line(-hw, -hh + r * ch, hw, -hh + r * ch, S.Thin), "grid");
                p.Add(Prim.Rect(-hw, -hh, p.W, p.H, S.Vis), "border");
            }
            return new AutoType("auto.det.grid", T("Occupancy grid", "Belegungsgitter"), AutoCategories.Perception,
                new[] { "occupancy grid", "grid map", "free space", "environment model", "belegungsgitter", "belegungskarte", "freiraum", "umfeldmodell" },
                Views.TopV(40, 40, Draw) with { Fluid = true })
            {
                Params = new[] { rows, cols, occ, seed },
                Uniform = false,
                Extra = o => new[] { new ContextAction("param.seed.next", T("Other pattern", "Anderes Muster"), IconIds.Increase, x => x.WithParam("seed", (x.Int("seed", 1) + 1) % 1000)) { Group = "param" } }
                    .Concat(Prm.PlusMinus("occupancy", 0.3, 0, 1, 0.1, false, T("More occupied cells", "Mehr belegte Zellen"), T("Fewer occupied cells", "Weniger belegte Zellen"), "param")),
            };
        }

        // ------------------------------------------------------------------ conflict point and TTC

        private static AutoType Conflict()
        {
            void Draw(Pen p, SketchObject o)
            {
                p.Circle(0, 0, 4.6, S.Fine, "ring");
                p.Line(-2.8, -2.8, 2.8, 2.8, S.Vis with { StrokeWidth = 0.7 }, "cross");
                p.Line(-2.8, 2.8, 2.8, -2.8, S.Vis with { StrokeWidth = 0.7 }, "cross");
                for (var i = 0; i < 8; i++)
                {
                    var a = Geo.DegToRad(i * 45 + 22.5);
                    p.Line(Math.Cos(a) * 4.6, Math.Sin(a) * 4.6, Math.Cos(a) * 5, Math.Sin(a) * 5, S.Fine, "tick");
                }
            }
            return new AutoType("auto.motion.conflict", T("Conflict point", "Konfliktpunkt"), AutoCategories.Motion,
                new[] { "conflict point", "collision point", "crash", "impact", "konfliktpunkt", "kollisionspunkt", "kollision", "aufprall" },
                Views.SymbolV(10, 10, Draw) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, Vec2.Zero, "point") } });
        }

        /// <summary>
        /// The label text: the TTC computed from the entered gap and speeds ("TTC = 1.8 s", "TTC = ∞" when the ego vehicle
        /// is not closing in), else the TTC parameter.
        /// </summary>
        public static string TtcText(SketchObject o)
        {
            var computed = AutoValues.EnteredTtc(o);
            if (computed is double c) return double.IsPositiveInfinity(c) ? "TTC = ∞" : "TTC = " + Math.Min(c, 999).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            return "TTC = " + Geo.Clamp(o.Num("ttc", 2.4), 0, 99).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        private static AutoType Ttc()
        {
            // No default value: an inserted label shows 2.4 s, but the TTC counts as entered only once it is set, so it can
            // follow from entered speeds and gap.
            var ttc = Prm.Num("ttc", "Time to collision", "Zeit bis zur Kollision", 2.4, 0, 99, "s", 0.1) with { Default = null };
            var frame = Prm.Bool("frame", "Frame", "Rahmen", true);
            void Draw(Pen p, SketchObject o)
            {
                if (o.Flag("frame", true)) p.RBox(-17, -4.5, 17, 4.5, 1.5, S.Fine, "frame");
                p.Circle(-13.2, 0.5, 2.4, S.Fine, "watch");
                p.Line(-13.2, -1.9, -13.2, -2.9, S.Fine, "watch");
                p.Line(-14.0, -2.9, -12.4, -2.9, S.Fine, "watch");
                p.Line(-13.2, 0.5, -12.1, -0.6, S.Fine, "watch");
                var text = o.Text ?? TtcText(o);
                p.Text(-10, 0, text, 4.5, 26, 7, TextAnchor.Start);
            }
            return new AutoType("auto.motion.ttc", T("TTC label", "TTC-Angabe"), AutoCategories.Motion,
                new[] { "ttc", "time to collision", "label", "criticality", "zeit bis zur kollision", "kritikalität", "angabe" },
                Views.SymbolV(34, 9, Draw))
            {
                Params = new[] { ttc, frame }.Concat(AutoValues.TtcParams).ToList(),
                ValueVars = AutoValues.TtcVars,
                ValueRelations = AutoValues.TtcRelations,
                Checks = AutoValues.TtcChecks,
                TextEditable = true,
                Extra = o => Prm.PlusMinus("ttc", 2.4, 0, 99, 0.5, false, T("Increase TTC", "TTC erhöhen"), T("Decrease TTC", "TTC verringern"), "param"),
            };
        }
    }
}
