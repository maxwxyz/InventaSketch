using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>Vessels, tanks, columns and reactors (flow sheet symbols drawn independently, no standard compliance claimed).</summary>
    internal static class Vessels
    {
        public const string Cat = ProcessCategories.Vessels;

        private static readonly string[] Companions = { "process.line", "process.pump.centrifugal", "process.valve.manual", "process.instrument.bubble" };

        internal static readonly ParamDef Heads = Choice("heads", "Heads", "Böden", "ellipsoidal",
            ("ellipsoidal", "Dished (ellipsoidal)", "Gewölbt (Klöpperboden)"), ("flat", "Flat", "Flach"), ("conical", "Conical bottom", "Kegelboden"));
        internal static readonly ParamDef Support = Choice("support", "Support", "Auflager", "none",
            ("none", "None", "Keines"), ("legs", "Legs", "Füße"), ("skirt", "Skirt", "Standzarge"));
        internal static readonly ParamDef Level = Flag("level", "Liquid level", "Flüssigkeitsstand", false);
        internal static readonly ParamDef Saddles = Flag("saddles", "Saddles", "Sättel", true);
        internal static readonly ParamDef Boot = Flag("boot", "Boot (water sump)", "Wassersack (Sumpf)", false);
        internal static readonly ParamDef Roof = Choice("roof", "Roof", "Dach", "cone",
            ("cone", "Cone roof", "Kegeldach"), ("dome", "Dome roof", "Kuppeldach"), ("floating", "Floating roof", "Schwimmdach"));
        internal static readonly ParamDef Internals = Choice("internals", "Internals", "Einbauten", "trays",
            ("trays", "Trays", "Böden"), ("packed", "Packing", "Packung"));
        internal static readonly ParamDef Trays = Int("trays", "Number of trays", "Anzahl Böden", 8, 2, 30);
        internal static readonly ParamDef Beds = Int("beds", "Packed beds", "Packungsbetten", 2, 1, 4);
        internal static readonly ParamDef Agitator = Flag("agitator", "Agitator", "Rührwerk", true);
        internal static readonly ParamDef Jacket = Flag("jacket", "Heating/cooling jacket", "Heiz-/Kühlmantel", false);

        public static IEnumerable<ProcessType> Types()
        {
            yield return new ProcessType("process.vessel.vertical", T("Vertical vessel", "Behälter, stehend"), Cat, 14, 30, VerticalVessel,
                "vessel", "vertical vessel", "drum", "separator", "receiver", "pressure vessel", "accumulator",
                "behälter", "stehender behälter", "druckbehälter", "abscheider", "vorlage", "apparat")
            {
                Params = new[] { Heads, Support, Level },
                DesignFor = o => new Size2(14, 30 + (o.Str("support", "none") == "none" ? 0 : 5)),
                Ports = o =>
                {
                    var h = 30 + (o.Str("support", "none") == "none" ? 0 : 5);
                    double top = -h / 2.0, bottom = top + 30;
                    return new[] { new Port("N1", 0, top), new Port("N2", 0, bottom), new Port("N3", -7, top + 10.5), new Port("N4", 7, top + 21) };
                },
                Block = true, TagPrefix = "V-101", Uniform = false, CompanionIds = Companions,
                Extra = o => new[]
                {
                    Cycle("process.vessel.heads", T("Heads", "Böden"), Heads),
                    Cycle("process.vessel.support", T("Support", "Auflager"), Support),
                    Toggle(o, "process.vessel.level", T("Liquid level", "Flüssigkeitsstand"), "level", false),
                },
            };

            yield return new ProcessType("process.vessel.horizontal", T("Horizontal vessel", "Behälter, liegend"), Cat, 32, 14, HorizontalVessel,
                "vessel", "horizontal vessel", "drum", "separator", "reflux drum", "knock-out drum", "pressure vessel",
                "behälter", "liegender behälter", "druckbehälter", "abscheider", "rücklaufbehälter", "trommel")
            {
                Params = new[] { Saddles, Boot, Level },
                DesignFor = o => new Size2(32, HorizontalHeight(o)),
                Ports = o =>
                {
                    var h = HorizontalHeight(o);
                    double top = -h / 2, yc = top + 5, bottom = top + 10;
                    return new[]
                    {
                        new Port("N1", -16, yc), new Port("N2", 16, yc), new Port("N3", 0, top),
                        o.Flag("boot", false) ? new Port("N4", 3.5, h / 2) : new Port("N4", 0, bottom),
                    };
                },
                Block = true, TagPrefix = "V-102", Uniform = false, CompanionIds = Companions,
                Extra = o => new[]
                {
                    Toggle(o, "process.vessel.saddles", T("Saddles", "Sättel"), "saddles", true),
                    Toggle(o, "process.vessel.boot", T("Boot (water sump)", "Wassersack"), "boot", false),
                    Toggle(o, "process.vessel.level", T("Liquid level", "Flüssigkeitsstand"), "level", false),
                },
            };

            yield return new ProcessType("process.vessel.tank", T("Storage tank", "Lagertank"), Cat, 24, 20, Tank,
                "tank", "storage tank", "atmospheric tank", "cone roof", "dome roof", "floating roof",
                "tank", "lagertank", "speichertank", "flachbodentank", "kegeldach", "schwimmdach", "behälter")
            {
                Params = new[] { Roof, Level },
                Ports = Fixed(new Port("N1", 0, -10), new Port("N2", -12, 7.5), new Port("N3", 12, 7.5)),
                Block = true, TagPrefix = "TK-101", Uniform = false, CompanionIds = Companions,
                Extra = o => new[]
                {
                    Cycle("process.tank.roof", T("Roof", "Dach"), Roof),
                    Toggle(o, "process.vessel.level", T("Liquid level", "Flüssigkeitsstand"), "level", false),
                },
            };

            yield return new ProcessType("process.column", T("Process column", "Kolonne"), Cat, 12, 50, Column,
                "column", "tower", "distillation", "rectification", "absorber", "stripper", "tray column", "packed column", "scrubber",
                "kolonne", "destillationskolonne", "rektifikation", "bodenkolonne", "füllkörperkolonne", "packungskolonne", "absorber", "wäscher")
            {
                Params = new[] { Internals, Trays, Beds },
                Ports = Fixed(new Port("N1", 0, -25), new Port("N2", 0, 25), new Port("N3", -6, 0), new Port("N4", 6, -17), new Port("N5", 6, 17)),
                Block = true, TagPrefix = "C-101", Uniform = false,
                CompanionIds = new[] { "process.line", "process.hx.shelltube", "process.vessel.horizontal", "process.pump.centrifugal", "process.instrument.bubble" },
                Extra = o => new[]
                {
                    Cycle("process.column.internals", T("Trays / packing", "Böden / Packung"), Internals),
                    o.Str("internals", "trays") == "packed" ? Step("process.column.more", T("More packed beds", "Mehr Packungsbetten"), Beds, 1) : Step("process.column.more", T("More trays", "Mehr Böden"), Trays, 2),
                    o.Str("internals", "trays") == "packed" ? Step("process.column.fewer", T("Fewer packed beds", "Weniger Packungsbetten"), Beds, -1) : Step("process.column.fewer", T("Fewer trays", "Weniger Böden"), Trays, -2),
                },
            };

            yield return new ProcessType("process.reactor", T("Stirred tank reactor", "Rührkesselreaktor"), Cat, 16, 30, Reactor,
                "reactor", "stirred tank", "cstr", "agitator", "mixer", "jacketed vessel", "batch reactor",
                "reaktor", "rührkessel", "rührbehälter", "rührwerk", "mischbehälter", "doppelmantel", "kessel")
            {
                Params = new[] { Agitator, Jacket },
                DesignFor = o => ReactorDesign(o),
                Ports = ReactorPorts,
                Block = true, TagPrefix = "R-101", Uniform = false, CompanionIds = Companions,
                Extra = o => new[]
                {
                    Toggle(o, "process.reactor.agitator", T("Agitator", "Rührwerk"), "agitator", true),
                    Toggle(o, "process.reactor.jacket", T("Jacket", "Doppelmantel"), "jacket", false),
                },
            };
        }

        // ------------------------------------------------------------------ vertical vessel

        /// <summary>Closed outline of a vertical shell from <paramref name="top"/> to <paramref name="bottom"/> with half width r.</summary>
        internal static List<Vec2> VerticalShell(double r, double top, double bottom, string heads)
        {
            var hd = heads == "flat" ? 0 : r / 2;
            var pts = new List<Vec2>();
            if (hd > 0) pts.AddRange(Pen.ArcPts(0, top + hd, r, hd, 180, 180));
            else { pts.Add(new Vec2(-r, top)); pts.Add(new Vec2(r, top)); }
            switch (heads)
            {
                case "conical":
                    pts.Add(new Vec2(r, bottom - r * 1.0));
                    pts.Add(new Vec2(r * 0.18, bottom));
                    pts.Add(new Vec2(-r * 0.18, bottom));
                    pts.Add(new Vec2(-r, bottom - r * 1.0));
                    break;
                case "flat":
                    pts.Add(new Vec2(r, bottom));
                    pts.Add(new Vec2(-r, bottom));
                    break;
                default:
                    pts.AddRange(Pen.ArcPts(0, bottom - hd, r, hd, 0, 180));
                    break;
            }
            return pts;
        }

        /// <summary>Lowest point of the straight shell (where supports attach).</summary>
        private static double StraightBottom(double r, double bottom, string heads) => heads switch
        {
            "conical" => bottom - r,
            "flat" => bottom,
            _ => bottom - r / 2,
        };

        private static void LevelMark(Pen p, double x0, double x1, double y)
        {
            p.Line(x0, y, x1, y, S.Dashed, "level");
            var mx = x0 + (x1 - x0) * 0.72;
            p.Poly(S.Thin, true, "level", mx - 0.9, y - 1.2, mx + 0.9, y - 1.2, mx, y);
        }

        private static void VerticalVessel(Pen p, SketchObject o)
        {
            const double r = 7;
            var heads = o.Str("heads", "ellipsoidal");
            var support = o.Str("support", "none");
            double top = -p.Hh, bottom = top + 30;
            p.PolyD(VerticalShell(r, top, bottom, heads), S.Sym, true, ProcessTags.Body);
            var sb = StraightBottom(r, bottom, heads);
            if (o.Flag("level", false)) LevelMark(p, -r, r, top + 16);
            switch (support)
            {
                case "legs":
                    p.Line(-r + 0.6, sb, -r + 0.6, p.Hh, S.Sym, "support");
                    p.Line(r - 0.6, sb, r - 0.6, p.Hh, S.Sym, "support");
                    p.Line(-r, p.Hh, -r + 1.8, p.Hh, S.Sym, "support");
                    p.Line(r - 1.8, p.Hh, r, p.Hh, S.Sym, "support");
                    break;
                case "skirt":
                    p.Line(-r, sb, -r, p.Hh, S.Sym, "support");
                    p.Line(r, sb, r, p.Hh, S.Sym, "support");
                    p.Line(-r, p.Hh, r, p.Hh, S.Sym, "support");
                    break;
            }
        }

        // ------------------------------------------------------------------ horizontal vessel

        private static double HorizontalHeight(SketchObject o) => 10 + (o.Flag("saddles", true) || o.Flag("boot", false) ? 4 : 0);

        private static void HorizontalVessel(Pen p, SketchObject o)
        {
            const double r = 5, hd = 2.5;
            double top = -p.Hh, yc = top + r, bottom = top + 2 * r;
            var pts = new List<Vec2>();
            pts.AddRange(Pen.ArcPts(16 - hd, yc, hd, r, 270, 180));
            pts.AddRange(Pen.ArcPts(-16 + hd, yc, hd, r, 90, 180));
            p.PolyD(pts, S.Sym, true, ProcessTags.Body);
            p.Line(-16 + hd, top, -16 + hd, bottom, S.Fine, "seam");
            p.Line(16 - hd, top, 16 - hd, bottom, S.Fine, "seam");
            if (o.Flag("level", false)) LevelMark(p, -16 + hd, 16 - hd, yc + 1.5);
            if (o.Flag("saddles", true))
                foreach (var x in new[] { -9.5, 9.5 })
                    p.Poly(S.Sym, false, "support", x - 1.6, bottom - 0.3, x - 2.4, p.Hh, x + 2.4, p.Hh, x + 1.6, bottom - 0.3);
            if (o.Flag("boot", false))
            {
                // Boot: small vertical sump below the shell.
                p.Poly(S.Sym, false, "boot", 1.5, bottom - 0.05, 1.5, p.Hh, 5.5, p.Hh, 5.5, bottom - 0.05);
            }
        }

        // ------------------------------------------------------------------ storage tank

        private static void Tank(Pen p, SketchObject o)
        {
            const double hw = 12;
            var roof = o.Str("roof", "cone");
            double top = -10, wallTop = top + 3, bottom = 10;
            switch (roof)
            {
                case "dome":
                    {
                        var pts = Pen.ArcPts(0, wallTop, hw, 3, 180, 180);
                        pts.Add(new Vec2(hw, bottom));
                        pts.Add(new Vec2(-hw, bottom));
                        p.PolyD(pts, S.Sym, true, ProcessTags.Body);
                        break;
                    }
                case "floating":
                    p.Poly(S.Sym, false, ProcessTags.Body, -hw, top, -hw, bottom, hw, bottom, hw, top);
                    p.Rect(-hw + 0.8, wallTop + 1.2, hw - 0.8, wallTop + 2.4, S.Thin, "roof");
                    p.Poly(S.Thin, false, "roof", -hw, wallTop + 1.2, -hw + 0.8, wallTop + 1.8, -hw, wallTop + 2.4);
                    p.Poly(S.Thin, false, "roof", hw, wallTop + 1.2, hw - 0.8, wallTop + 1.8, hw, wallTop + 2.4);
                    break;
                default:
                    p.Poly(S.Sym, true, ProcessTags.Body, -hw, wallTop, 0, top, hw, wallTop, hw, bottom, -hw, bottom);
                    break;
            }
            if (o.Flag("level", false)) LevelMark(p, -hw, hw, 1);
        }

        // ------------------------------------------------------------------ column

        private static void Column(Pen p, SketchObject o)
        {
            const double r = 6;
            p.PolyD(VerticalShell(r, -25, 25, "ellipsoidal"), S.Sym, true, ProcessTags.Body);
            double y0 = -19, y1 = 19;
            if (o.Str("internals", "trays") == "packed")
            {
                var beds = Math.Max(1, Math.Min(4, o.Int("beds", 2)));
                var gap = 2.0;
                var h = (y1 - y0 - gap * (beds - 1)) / beds;
                for (var i = 0; i < beds; i++)
                {
                    var a = y0 + i * (h + gap);
                    p.Line(-r, a, r, a, S.Thin, "packing");
                    p.Line(-r, a + h, r, a + h, S.Thin, "packing");
                    p.Line(-r, a, r, a + h, S.Fine, "packing");
                    p.Line(r, a, -r, a + h, S.Fine, "packing");
                }
            }
            else
            {
                var n = Math.Max(2, Math.Min(30, o.Int("trays", 8)));
                var step = (y1 - y0) / (n - 1);
                for (var i = 0; i < n; i++)
                {
                    var y = y0 + i * step;
                    // Segmental trays alternate from the left and the right wall.
                    if (i % 2 == 0) p.Line(-r, y, r * 0.45, y, S.Thin, "tray");
                    else p.Line(-r * 0.45, y, r, y, S.Thin, "tray");
                }
            }
        }

        // ------------------------------------------------------------------ reactor

        private static Size2 ReactorDesign(SketchObject o) => new(o.Flag("jacket", false) ? 19 : 16, 24 + AgitatorZone(o));

        /// <summary>Space above the body: the agitator drive, else room for the nozzle stubs.</summary>
        private static double AgitatorZone(SketchObject o) => o.Flag("agitator", true) ? 6 : 1.6;

        /// <summary>Top of the vessel body (below the agitator drive).</summary>
        private static double ReactorTop(SketchObject o) => -ReactorDesign(o).H / 2 + AgitatorZone(o);

        private static IReadOnlyList<Port> ReactorPorts(SketchObject o)
        {
            const double r = 8, hd = 3;
            var d = ReactorDesign(o);
            var top = ReactorTop(o);
            // Nozzles on the top head at x = ±4.5, 1.5 mm stubs.
            var yHead = top + hd - hd * Math.Sqrt(1 - 4.5 * 4.5 / (r * r));
            var list = new List<Port> { new("N1", -4.5, yHead - 1.5), new("N3", 4.5, yHead - 1.5), new("N2", 0, d.H / 2) };
            if (o.Flag("jacket", false))
            {
                list.Add(new Port("J1", -d.W / 2, d.H / 2 - 7));
                list.Add(new Port("J2", d.W / 2, top + 7));
            }
            return list;
        }

        private static void Reactor(Pen p, SketchObject o)
        {
            const double r = 8, hd = 3;
            var top = ReactorTop(o);
            var bottom = p.Hh;
            var pts = Pen.ArcPts(0, top + hd, r, hd, 180, 180);
            pts.AddRange(Pen.ArcPts(0, bottom - hd, r, hd, 0, 180));
            p.PolyD(pts, S.Sym, true, ProcessTags.Body);
            foreach (var port in ReactorPorts(o).Where(x => x.Id is "N1" or "N3"))
                p.Line(port.X, port.Y, port.X, port.Y + 1.7, S.Sym, "nozzle");
            if (o.Flag("jacket", false))
            {
                const double j = 1.5;
                var jt = top + 5.5;
                var jp = new List<Vec2> { new(-r - j, jt) };
                jp.AddRange(Pen.ArcPts(0, bottom - hd - j, r + j, hd + j * 0.6, 180, -180));
                jp.Add(new Vec2(r + j, jt));
                // Jacket hugs the lower shell; its bottom stays inside the box.
                var jb = jp.Select(q => new Vec2(q.X, Math.Min(q.Y, bottom - 0.2))).ToList();
                p.PolyD(jb, S.Sym, false, "jacket");
                p.Line(-r - j, jt, -r, jt, S.Sym, "jacket");
                p.Line(r, jt, r + j, jt, S.Sym, "jacket");
            }
            if (o.Flag("agitator", true))
            {
                var mc = top - 3.2;
                p.Circle(0, mc, 2.6, S.Sym, "motor");
                p.Text(0, mc, "M", 2.6, TextAnchor.Middle, "motor");
                var yi = bottom - hd - 4.5;
                p.Line(0, mc + 2.6, 0, yi, S.Sym, "shaft");
                p.Line(-4, yi, 4, yi, S.Bold, "impeller");
                p.Line(-4, yi - 1, -4, yi + 1, S.Bold, "impeller");
                p.Line(4, yi - 1, 4, yi + 1, S.Bold, "impeller");
            }
        }
    }
}
