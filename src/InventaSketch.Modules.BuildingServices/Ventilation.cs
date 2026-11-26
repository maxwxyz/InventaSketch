using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>Ventilation: air terminal (supply/extract valve or grille) and ventilation unit with heat recovery.</summary>
    internal static class Ventilation
    {
        private const string Cat = BsCategories.Ventilation;

        private static readonly ParamDef TerminalKind = Pm.Choice("kind", "Air", "Luftart", "supply",
            ("supply", "Supply air", "Zuluft"), ("extract", "Extract air", "Abluft"));

        /// <summary>Order of the unit's spigots from left to right (plan and section share it; the schematic keeps the sides).</summary>
        private static readonly string[] Spigots = { "outdoor", "exhaust", "extract", "supply" };

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.vent.terminal", new I18nText("Air terminal (valve / grille)", "Luftdurchlass (Ventil / Gitter)"), Cat,
                new Size2(3, 3), new Size2(3, 1.6), new Size2(3, 3), TerminalPlan, TerminalSection, TerminalSchematic,
                "air terminal", "air valve", "grille", "diffuser", "supply air", "extract air", "luftdurchlass", "tellerventil", "lüftungsgitter",
                "zuluft", "abluft", "kwl", "lüftung", "tga")
            {
                Mount = Mounting.Ceiling, WallBacked = false, ToScale = false, Uniform = true, DefaultHeight = 2450,
                Params = new[] { TerminalKind },
                Ports = BsBoxType.Uv(new[] { (0.0, 0.0, "duct") }, new[] { (0.0, -0.5, "duct") }),
                Extra = o => Bs.Choice(o, "bs.terminal.kind", TerminalKind),
                CompanionIds = new[] { DuctType.TypeId, "bs.vent.unit" },
            };

            yield return new BsBoxType("bs.vent.unit", new I18nText("Ventilation unit with heat recovery", "Lüftungsgerät mit Wärmerückgewinnung"), Cat,
                new Size2(7, 4.5), new Size2(7, 8), new Size2(7, 5), UnitPlan, UnitSection, UnitSchematic,
                "ventilation unit", "mvhr", "heat recovery", "hrv", "erv", "air handling unit", "lüftungsgerät", "kwl", "wärmerückgewinnung",
                "wrg", "kontrollierte wohnraumlüftung", "lüftung", "tga")
            {
                Mount = Mounting.Wall, DefaultHeight = 1000,
                Ports = UnitPorts,
                CompanionIds = new[] { DuctType.TypeId, "bs.vent.terminal" },
            };
        }

        // ------------------------------------------------------------------ air terminal

        private static bool Supply(SketchObject o) => o.Str("kind", "supply") != "extract";

        private static Style AirStyle(SketchObject o, double width) => new() { Stroke = Media.AirOf(Supply(o) ? "supply" : "extract").Color, StrokeWidth = width, Role = LineRole.Visible };

        private static void TerminalPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-0.3 * c.W, -0.3 * c.H, 0.3 * c.W, 0.3 * c.H, St.Thin, BsTags.Outline);
            c.Circle(Vec2.Zero, 0.17 * c.K, AirStyle(o, 0.25), "valve");
            // Air direction: outward (supply) or inward (extract) at the four corners.
            var supply = Supply(o);
            foreach (var (dx, dy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var d = new Vec2(dx, dy).Normalized();
                var a = d * (0.3 * c.K * Math.Sqrt(2) + 0.02 * c.K);
                var b = new Vec2(dx * 0.49 * c.K, dy * 0.49 * c.K);
                if (supply) c.Arrow(a, b, St.Fine, 0.1 * c.K, "air");
                else c.Arrow(b, a, St.Fine, 0.1 * c.K, "air");
            }
        }

        private static void TerminalSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Neck from the ceiling, disc valve, air direction below.
            var neckBottom = -0.12 * c.H;
            c.Rect(-0.18 * c.W, -c.Hh, 0.18 * c.W, neckBottom, St.Thin, "neck");
            c.Rect(-0.45 * c.W, neckBottom, 0.45 * c.W, neckBottom + 0.12 * c.H, AirStyle(o, 0.25), BsTags.Outline);
            var y0 = neckBottom + 0.16 * c.H;
            var supply = Supply(o);
            foreach (var u in new[] { -0.32, 0.32 })
            {
                Vec2 a = c.N(u, y0 / c.H), b = c.N(u * 1.3, 0.48);
                var head = Math.Min(0.18 * c.H, 0.12 * c.W);
                if (supply) c.Arrow(a, b, St.Fine, head, "air");
                else c.Arrow(b, a, St.Fine, head, "air");
            }
        }

        private static void TerminalSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var supply = Supply(o);
            c.LineN(0, -0.5, 0, -0.3, St.Medium, BsTags.Stub);
            c.Rect(-0.35 * c.W, -0.3 * c.H, 0.35 * c.W, 0.05 * c.H, AirStyle(o, 0.35), BsTags.Outline);
            var code = Media.AirOf(supply ? "supply" : "extract").Code.Get(ctx.Lang);
            c.Text(c.N(0, -0.125), code, 1.2, 0.6 * c.W, 0.25 * c.H);
            if (supply) c.Arrow(c.N(0, 0.1), c.N(0, 0.48), St.Thin, 0.16 * c.K, "air");
            else c.Arrow(c.N(0, 0.48), c.N(0, 0.1), St.Thin, 0.16 * c.K, "air");
        }

        // ------------------------------------------------------------------ ventilation unit

        private static double SpigotU(int i) => -0.33 + i * 0.22;

        private static IEnumerable<PortAt> UnitPorts(Canvas c, SketchObject o, string view)
        {
            switch (view)
            {
                case BsViews.Schematic:
                    yield return new PortAt(c.N(-0.5, -0.25), "outdoor");
                    yield return new PortAt(c.N(-0.5, 0.25), "exhaust");
                    yield return new PortAt(c.N(0.5, 0.25), "extract");
                    yield return new PortAt(c.N(0.5, -0.25), "supply");
                    break;
                case BsViews.Section:
                    for (var i = 0; i < Spigots.Length; i++) yield return new PortAt(c.N(SpigotU(i), -0.5), Spigots[i]);
                    break;
                default:
                    for (var i = 0; i < Spigots.Length; i++) yield return new PortAt(c.N(SpigotU(i), 0.05), Spigots[i]);
                    break;
            }
        }

        private static void UnitPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            var r = Math.Min(0.09 * c.W, 0.3 * c.H);
            for (var i = 0; i < Spigots.Length; i++)
            {
                var k = Media.AirOf(Spigots[i]);
                c.Circle(c.N(SpigotU(i), 0.05), r, new Style { Stroke = k.Color, StrokeWidth = 0.25, Role = LineRole.Visible }, "spigot");
            }
            c.Text(c.N(0, -0.34), ctx.Lang == Lang.De ? "WRG" : "HRV", 2, 0.5 * c.W, 0.18 * c.H);
        }

        /// <summary>Plate heat exchanger: a square on its corner with both diagonals.</summary>
        private static void Exchanger(Canvas c, Vec2 centre, double d, Style s)
        {
            c.Poly(new[] { centre + new Vec2(0, -d), centre + new Vec2(d, 0), centre + new Vec2(0, d), centre + new Vec2(-d, 0) }, s, true, "exchanger");
            c.Line(centre + new Vec2(-d / 2, -d / 2), centre + new Vec2(d / 2, d / 2), St.Fine, "exchanger");
            c.Line(centre + new Vec2(-d / 2, d / 2), centre + new Vec2(d / 2, -d / 2), St.Fine, "exchanger");
        }

        private static void Fan(Canvas c, Vec2 centre, double r, Style s, int dir)
        {
            c.Circle(centre, r, s, "fan");
            c.Poly(new[] { centre + new Vec2(dir * r, 0), centre + new Vec2(-dir * r * 0.5, -r * 0.866), centre + new Vec2(-dir * r * 0.5, r * 0.866) }, s, true, "fan");
        }

        private static void UnitSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var top = -0.4 * c.H;
            c.Rect(-c.Hw, top, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            for (var i = 0; i < Spigots.Length; i++)
            {
                var k = Media.AirOf(Spigots[i]);
                c.RectN(SpigotU(i) - 0.07, -0.5, SpigotU(i) + 0.07, -0.4, new Style { Stroke = k.Color, StrokeWidth = 0.25, Role = LineRole.Visible }, "spigot");
            }
            var d = Math.Min(0.22 * c.W, 0.2 * c.H);
            Exchanger(c, c.N(0, 0.02), d, St.Thin);
            var fr = Math.Min(0.1 * c.W, 0.09 * c.H);
            c.Circle(c.N(-0.32, 0.3), fr, St.Fine, "fan");
            c.Circle(c.N(0.32, 0.3), fr, St.Fine, "fan");
            c.LineN(-0.48, 0.44, 0.48, 0.44, St.Fine, "filter");
        }

        private static void UnitSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            double x0 = -0.4 * c.W, x1 = 0.4 * c.W, q = 0.25 * c.H;
            c.Rect(x0, -c.Hh, x1, c.Hh, St.Medium, BsTags.Outline);
            var d = Math.Min(0.18 * c.W, 0.3 * c.H);
            Exchanger(c, Vec2.Zero, d, St.Medium);
            Style Air(string kind) => new() { Stroke = Media.AirOf(kind).Color, StrokeWidth = 0.35, Role = LineRole.Visible };
            // Outdoor air → supply air (upper path) and extract air → exhaust air (lower path) cross in the exchanger.
            c.Poly(new[] { new Vec2(-c.Hw, -q), new Vec2(-d * 0.9, -q), new Vec2(-d / 2, -d / 2) }, Air("outdoor"), false, "air");
            c.Poly(new[] { new Vec2(d / 2, d / 2), new Vec2(d * 0.9, -q), new Vec2(c.Hw, -q) }, Air("supply"), false, "air");
            c.Poly(new[] { new Vec2(c.Hw, q), new Vec2(d * 0.9, q), new Vec2(d / 2, -d / 2) }, Air("extract"), false, "air");
            c.Poly(new[] { new Vec2(-d / 2, d / 2), new Vec2(-d * 0.9, q), new Vec2(-c.Hw, q) }, Air("exhaust"), false, "air");
            var fr = Math.Min(0.07 * c.W, 0.12 * c.H);
            Fan(c, new Vec2(0.3 * c.W, -q), fr, St.MediumWhite, 1);
            Fan(c, new Vec2(-0.3 * c.W, q), fr, St.MediumWhite, -1);
        }
    }

    /// <summary>
    /// Electrical installation elements the electrical module does not have yet (socket, switch, light, junction box,
    /// distribution board and cable route live there): communication outlet, detector, appliance outlet, equipotential
    /// bonding bar.
    /// </summary>
    internal static class ElectricalAdditions
    {
        private const string Cat = BsCategories.Electrical;

        private static readonly ParamDef DataKind = Pm.Choice("kind", "Outlet", "Anschluss", "data",
            ("data", "Data (network)", "Daten (Netzwerk)"), ("phone", "Telephone", "Telefon"), ("tv", "TV / satellite", "TV / SAT"));
        private static readonly ParamDef DetectorKind = Pm.Choice("kind", "Detector", "Melder", "smoke",
            ("smoke", "Smoke detector", "Rauchmelder"), ("heat", "Heat detector", "Wärmemelder"), ("motion", "Motion detector", "Bewegungsmelder"));
        private static readonly ParamDef ApplianceKind = Pm.Choice("kind", "Appliance", "Gerät", "cooker",
            ("cooker", "Cooker", "Herd"), ("washer", "Washing machine", "Waschmaschine"), ("dryer", "Tumble dryer", "Wäschetrockner"),
            ("dishwasher", "Dishwasher", "Geschirrspüler"), ("generic", "Appliance (generic)", "Gerät (allgemein)"));

        // Room, circuit and phase of the appliance outlet (optional): the same parameters as on the switchgear room devices, so
        // the switchgear reports (house installation list, circuit plan) and circuit values (I_B and power of the feeding
        // device) read this outlet too. "feed" names the protective device that feeds the circuit ("-F3").
        private static readonly ParamDef Room = new("room", new I18nText("Room", "Raum"), ParamKind.Text);
        private static readonly ParamDef Feed = new("feed", new I18nText("Circuit (fed from, e.g. -F3)", "Stromkreis (gespeist von, z. B. -F3)"), ParamKind.Text);

        /// <summary>Phase of the appliance: from its circuit (single-phase), L1, L2, L3 or three-phase (a cooker); see <see cref="BsValues"/>.</summary>
        internal static readonly ParamDef AppliancePhase = Pm.Choice(BsValues.PhaseKey, "Phase", "Phase", "circuit",
            ("circuit", "From the circuit", "Vom Stromkreis"), ("L1", "L1", "L1"), ("L2", "L2", "L2"), ("L3", "L3", "L3"), (BsValues.ThreePhase, "Three-phase", "Dreiphasig"));

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.elec.data", new I18nText("Communication outlet", "Kommunikationsanschluss"), Cat,
                new Size2(4, 4), new Size2(4, 4), new Size2(4, 4), DataPlan, DataSection, DataSchematic,
                "data outlet", "network", "lan", "rj45", "telephone", "tv", "antenna", "satellite", "datendose", "netzwerkdose",
                "telefondose", "antennendose", "tae", "sat", "installation", "tga")
            {
                Mount = Mounting.Wall, ToScale = false, Uniform = true, DefaultHeight = 300,
                Params = new[] { DataKind },
                Ports = BsBoxType.Same((0, -0.5, "cable")),
                Extra = o => Bs.Choice(o, "bs.data.kind", DataKind, new I18nText("Outlet kind", "Dosenart")),
            };

            yield return new BsBoxType("bs.elec.detector", new I18nText("Detector (smoke / heat / motion)", "Melder (Rauch / Wärme / Bewegung)"), Cat,
                new Size2(4, 4), new Size2(4, 1.4), new Size2(4, 4), DetectorPlan, DetectorSection, DetectorSchematic,
                "smoke detector", "smoke alarm", "heat detector", "motion detector", "pir", "rauchmelder", "rauchwarnmelder", "wärmemelder",
                "bewegungsmelder", "präsenzmelder", "installation", "tga")
            {
                Mount = Mounting.Ceiling, WallBacked = false, ToScale = false, Uniform = true, DefaultHeight = 2440,
                Params = new[] { DetectorKind },
                Ports = BsBoxType.Uv(new[] { (0.0, -0.5, "cable") }, new[] { (0.0, -0.5, "cable") }),
                Extra = o => Bs.Choice(o, "bs.detector.kind", DetectorKind, new I18nText("Detector kind", "Melderart")),
            };

            yield return new BsBoxType("bs.elec.appliance", new I18nText("Appliance outlet", "Geräteanschlussdose"), Cat,
                new Size2(4, 4), new Size2(4, 4), new Size2(4, 4), AppliancePlan, ApplianceSection, ApplianceSchematic,
                "appliance outlet", "cooker outlet", "connection unit", "washing machine", "dishwasher", "dryer", "geräteanschlussdose",
                "herdanschluss", "herdanschlussdose", "waschmaschine", "geschirrspüler", "trockner", "installation", "tga")
            {
                Mount = Mounting.Wall, ToScale = false, Uniform = true, DefaultHeight = 300,
                Params = new[] { ApplianceKind, Room, Feed, AppliancePhase },
                Ports = BsBoxType.Same((0, -0.5, "cable")),
                Extra = o => Bs.Choice(o, "bs.appliance.kind", ApplianceKind, new I18nText("Appliance", "Gerät"))
                    .Concat(Bs.Choice(o, "bs.appliance.phase", AppliancePhase))
                    .Concat(new[]
                    {
                        ContextAction.WithInput("bs.appliance.room", new I18nText("Room …", "Raum …"), IconIds.Home, new I18nText("Room", "Raum"), o.Str("room", ""),
                            (x, t) => x.WithParam("room", t.Trim())) with { Group = "label" },
                        ContextAction.WithInput("bs.appliance.feed", new I18nText("Circuit …", "Stromkreis …"), IconIds.Name, new I18nText("Circuit (fed from)", "Stromkreis (gespeist von)"),
                            o.Str("feed", ""), (x, t) => x.WithParam("feed", t.Trim())) with { Group = "label" },
                    }),
            };

            yield return new BsBoxType("bs.elec.bonding", new I18nText("Equipotential bonding bar", "Potentialausgleichsschiene"), Cat,
                new Size2(6, 2), new Size2(6, 2), new Size2(6, 3), BondingPlan, BondingSection, BondingSchematic,
                "equipotential bonding", "bonding bar", "earth bar", "earthing", "potentialausgleich", "potentialausgleichsschiene", "pas",
                "erdung", "haupterdungsschiene", "installation", "tga")
            {
                Mount = Mounting.Wall, ToScale = false, DefaultHeight = 300,
                Ports = BsBoxType.Uv(new[] { (0.0, 0.5, "earth") }, new[] { (0.0, 0.5, "earth") }, new[] { (0.0, 0.5, "earth") }),
                Rails = (c, _, view) => { var y = view switch { BsViews.Schematic => -0.2 * c.H, BsViews.Plan => -0.32 * c.H, _ => 0.0 }; return new[] { (new Vec2(-0.42 * c.W, y), new Vec2(0.42 * c.W, y)) }; },
            };
        }

        private static string DataCode(SketchObject o, Lang lang) => o.Str("kind", "data") switch
        {
            "phone" => lang == Lang.De ? "TAE" : "TEL",
            "tv" => lang == Lang.De ? "SAT" : "TV",
            _ => "LAN",
        };

        private static string DetectorCode(SketchObject o, Lang lang) => o.Str("kind", "smoke") switch
        {
            "heat" => lang == Lang.De ? "WM" : "H",
            "motion" => lang == Lang.De ? "BM" : "PIR",
            _ => lang == Lang.De ? "RM" : "S",
        };

        private static string ApplianceCode(SketchObject o, Lang lang) => o.Str("kind", "cooker") switch
        {
            "washer" => lang == Lang.De ? "WM" : "WM",
            "dryer" => lang == Lang.De ? "WT" : "TD",
            "dishwasher" => lang == Lang.De ? "GS" : "DW",
            "generic" => "",
            _ => lang == Lang.De ? "HE" : "CK",
        };

        // ------------------------------------------------------------------ communication outlet

        private static void DataPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Stem from the wall (top edge) to the outlet symbol in the room.
            c.LineN(0, -0.5, 0, -0.15, St.Medium, BsTags.Stub);
            c.RectN(-0.4, -0.15, 0.4, 0.4, St.Medium, BsTags.Outline);
            c.Text(c.N(0, 0.125), DataCode(o, ctx.Lang), 1.6, 0.7 * c.W, 0.4 * c.H, bold: true);
        }

        private static void DataSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RoundRect(-0.45 * c.W, -0.45 * c.H, 0.45 * c.W, 0.45 * c.H, 0.08 * c.K, St.Thin, BsTags.Outline);
            c.RoundRect(-0.3 * c.W, -0.3 * c.H, 0.3 * c.W, 0.3 * c.H, 0.05 * c.K, St.Fine, "insert");
            switch (o.Str("kind", "data"))
            {
                case "tv":
                    c.Circle(Vec2.Zero, 0.12 * c.K, St.Thin, "jack");
                    c.Circle(Vec2.Zero, 0.03 * c.K, St.Black, "jack");
                    break;
                case "phone":
                    c.RectN(-0.15, -0.12, 0.15, 0.12, St.Thin, "jack");
                    c.LineN(-0.08, 0.12, 0.08, 0.12, St.Thin, "jack");
                    break;
                default:
                    c.PolyN(St.Thin, true, "jack", -0.15, -0.1, 0.15, -0.1, 0.15, 0.08, 0.06, 0.08, 0.06, 0.14, -0.06, 0.14, -0.06, 0.08, -0.15, 0.08);
                    break;
            }
        }

        private static void DataSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(0, -0.5, 0, -0.25, St.Medium, BsTags.Stub);
            c.RectN(-0.45, -0.25, 0.45, 0.35, St.Medium, BsTags.Outline);
            c.LineN(-0.45, 0.35, 0.45, -0.25, St.Fine, "diagonal");
            c.Text(c.N(-0.22, -0.13), DataCode(o, ctx.Lang), 1.3, 0.36 * c.W, 0.16 * c.H);
        }

        // ------------------------------------------------------------------ detector

        private static void DetectorPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.4 * c.K;
            c.Circle(Vec2.Zero, r, St.Medium, BsTags.Outline);
            if (o.Str("kind", "smoke") == "motion")
            {
                // Sensor: dot with two detection arcs.
                c.Circle(new Vec2(0, 0.18 * c.K), 0.05 * c.K, St.Black, "sensor");
                c.Arc(new Vec2(0, 0.18 * c.K), 0.16 * c.K, 0.16 * c.K, 225, 90, St.Thin, "sensor");
                c.Arc(new Vec2(0, 0.18 * c.K), 0.28 * c.K, 0.28 * c.K, 225, 90, St.Thin, "sensor");
                return;
            }
            c.Text(Vec2.Zero, DetectorCode(o, ctx.Lang), 1.6, 1.4 * r, 0.9 * r, bold: true);
        }

        private static void DetectorSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Base under the ceiling and the dome below it.
            c.RectN(-0.4, -0.5, 0.4, -0.2, St.Thin, "base");
            c.Arc(c.N(0, -0.2), 0.3 * c.W, 0.7 * c.H, 0, 180, St.Thin, BsTags.Outline);
            c.LineN(-0.3, -0.2, 0.3, -0.2, St.Thin, BsTags.Outline);
            c.Circle(c.N(0.12, 0.1), Math.Min(0.04 * c.W, 0.08 * c.H), St.Black, "led");
        }

        private static void DetectorSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(0, -0.5, 0, -0.3, St.Medium, BsTags.Stub);
            c.RectN(-0.4, -0.3, 0.4, 0.45, St.Medium, BsTags.Outline);
            c.Text(c.N(0, 0.075), DetectorCode(o, ctx.Lang), 1.6, 0.7 * c.W, 0.5 * c.H, bold: true);
        }

        // ------------------------------------------------------------------ appliance outlet

        private static void AppliancePlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(0, -0.5, 0, -0.18, St.Medium, BsTags.Stub);
            var centre = c.N(0, 0.14);
            var r = 0.32 * c.K;
            c.Circle(centre, r, St.Medium, BsTags.Outline);
            var code = ApplianceCode(o, ctx.Lang);
            if (code.Length == 0) c.Circle(centre, 0.08 * c.K, St.Black, "terminal");
            else c.Text(centre, code, 1.5, 1.5 * r, r, bold: true);
        }

        private static void ApplianceSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RoundRect(-0.42 * c.W, -0.42 * c.H, 0.42 * c.W, 0.36 * c.H, 0.06 * c.K, St.Thin, BsTags.Outline);
            c.Circle(c.N(0, -0.03), 0.05 * c.K, St.Thin, "screw");
            c.RectN(-0.08, 0.36, 0.08, 0.5, St.Thin, "gland");
            var code = ApplianceCode(o, ctx.Lang);
            if (code.Length > 0) c.Text(c.N(0, -0.25), code, 1.0, 0.6 * c.W, 0.2 * c.H);
        }

        private static void ApplianceSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(0, -0.5, 0, -0.3, St.Medium, BsTags.Stub);
            var centre = c.N(0, 0.1);
            var r = 0.38 * c.K;
            c.Circle(centre, r, St.Medium, BsTags.Outline);
            c.Line(centre + new Vec2(-r, 0), centre + new Vec2(r, 0), St.Thin, "terminal");
            var code = ApplianceCode(o, ctx.Lang);
            if (code.Length > 0) c.Text(centre + new Vec2(0, r * 0.45), code, 1.2, 1.2 * r, 0.6 * r);
        }

        // ------------------------------------------------------------------ equipotential bonding bar

        private static void Terminals(Canvas c, double y, double r, Style s)
        {
            for (var i = 0; i < 5; i++) c.Circle(new Vec2(-0.32 * c.W + i * 0.16 * c.W, y), r, s, "terminal");
        }

        private static void BondingPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            // The bar seen from above on the wall, the earthing conductor leaves to the room side.
            c.RectN(-0.45, -0.5, 0.45, -0.14, St.Medium with { Fill = Fill.Solid("#ffffff") }, BsTags.Outline);
            c.LineN(0, -0.14, 0, 0.5, St.Thin, BsTags.Stub);
        }

        private static void BondingSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RectN(-0.45, -0.25, 0.45, 0.25, St.Thin, BsTags.Outline);
            Terminals(c, 0, Math.Min(0.12 * c.H, 0.05 * c.W), St.Fine);
            c.LineN(0, 0.25, 0, 0.5, St.Thin, BsTags.Stub);
        }

        private static void BondingSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var y = -0.2 * c.H;
            c.Line(new Vec2(-0.45 * c.W, y), new Vec2(0.45 * c.W, y), St.Medium with { StrokeWidth = 0.7 }, "bar");
            // Earth symbol below the middle.
            var g = 0.22 * c.H;
            c.Line(new Vec2(0, y), new Vec2(0, g), St.Medium, BsTags.Stub);
            c.Line(new Vec2(-0.14 * c.W, g), new Vec2(0.14 * c.W, g), St.Medium, "earth");
            c.Line(new Vec2(-0.09 * c.W, g + 0.1 * c.H), new Vec2(0.09 * c.W, g + 0.1 * c.H), St.Medium, "earth");
            c.Line(new Vec2(-0.04 * c.W, g + 0.2 * c.H), new Vec2(0.04 * c.W, g + 0.2 * c.H), St.Medium, "earth");
            for (var i = 0; i < 5; i++)
            {
                var x = -0.36 * c.W + i * 0.18 * c.W;
                c.Line(new Vec2(x, y), new Vec2(x, -c.Hh), St.Thin, "terminal");
            }
        }
    }
}
