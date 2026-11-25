using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>Heat exchangers, air coolers and cooling towers (flow sheet symbols drawn independently).</summary>
    internal static class HeatTransfer
    {
        public const string Cat = ProcessCategories.Heat;

        private static readonly string[] Companions = { "process.line", "process.valve.control", "process.instrument.bubble", "process.pump.centrifugal" };

        internal static readonly ParamDef Bundle = Choice("bundle", "Tube bundle", "Rohrbündel", "utube",
            ("utube", "U-tubes", "U-Rohre"), ("fixed", "Straight tubes (fixed tube sheets)", "Gerade Rohre (feste Rohrböden)"));
        internal static readonly ParamDef Plates = Int("plates", "Plates shown", "Dargestellte Platten", 7, 3, 15);
        internal static readonly ParamDef Draft = Choice("draft", "Draft", "Zug", "natural",
            ("natural", "Natural draft (hyperbolic)", "Naturzug (Hyperboloid)"), ("mechanical", "Mechanical draft (fan)", "Zwangsbelüftet (Ventilator)"));

        public static IEnumerable<ProcessType> Types()
        {
            yield return new ProcessType("process.hx.shelltube", T("Shell-and-tube heat exchanger", "Rohrbündel-Wärmeübertrager"), Cat, 32, 12, ShellTube,
                "heat exchanger", "shell and tube", "shell-and-tube", "condenser", "reboiler", "cooler", "heater", "u-tube", "tube bundle",
                "wärmeübertrager", "wärmetauscher", "rohrbündel", "rohrbündelwärmeübertrager", "kondensator", "verdampfer", "kühler", "erhitzer")
            {
                Params = new[] { Bundle },
                Ports = o => o.Str("bundle", "utube") == "fixed"
                    ? new[] { new Port("T1", -14, -6), new Port("T2", 14, 6), new Port("S1", 7, -6), new Port("S2", -7, 6) }
                    : new[] { new Port("T1", -14, -6), new Port("T2", -14, 6), new Port("S1", 7, -6), new Port("S2", -7, 6) },
                Block = true, TagPrefix = "E-101", Uniform = false, CompanionIds = Companions,
                Extra = o => Choice(o, "process.hx.bundle", null, Bundle),
            };

            yield return new ProcessType("process.hx.plate", T("Plate heat exchanger", "Plattenwärmeübertrager"), Cat, 14, 16, Plate,
                "plate heat exchanger", "phe", "gasketed plate", "brazed plate", "heat exchanger",
                "plattenwärmeübertrager", "plattenwärmetauscher", "plattentauscher", "wärmeübertrager", "wärmetauscher")
            {
                Params = new[] { Plates },
                Ports = Fixed(new Port("A1", -7, -4.5), new Port("A2", -7, 4.5), new Port("B1", 7, 4.5), new Port("B2", 7, -4.5)),
                Block = true, TagPrefix = "E-102", Uniform = false, CompanionIds = Companions,
                Extra = o => new[]
                {
                    Step("process.hx.plates.more", T("More plates", "Mehr Platten"), Plates, 2),
                    Step("process.hx.plates.fewer", T("Fewer plates", "Weniger Platten"), Plates, -2),
                },
            };

            yield return new ProcessType("process.hx.general", T("Heat exchanger (general symbol)", "Wärmeübertrager (allgemein)"), Cat, 12, 12, General,
                "heat exchanger", "exchanger", "heater", "cooler", "coil", "heat transfer",
                "wärmeübertrager", "wärmetauscher", "erhitzer", "kühler", "heizschlange", "wärmeübertragung")
            {
                Ports = Fixed(new Port("P1", -6, 0), new Port("P2", 6, 0), new Port("S1", 0, -6), new Port("S2", 0, 6)),
                Block = true, TagPrefix = "E-103", CompanionIds = Companions,
            };

            yield return new ProcessType("process.hx.aircooler", T("Air cooler (fin-fan)", "Luftkühler (Rippenrohr mit Ventilator)"), Cat, 24, 14, AirCooler,
                "air cooler", "air-cooled heat exchanger", "fin fan", "fin-fan", "dry cooler", "condenser",
                "luftkühler", "luftgekühlter wärmeübertrager", "rippenrohr", "trockenkühler", "rückkühler", "verflüssiger")
            {
                Ports = Fixed(new Port("P1", -12, -4.5), new Port("P2", 12, -4.5)),
                Block = true, TagPrefix = "AC-101", Uniform = false, CompanionIds = Companions,
            };

            yield return new ProcessType("process.hx.coolingtower", T("Cooling tower", "Kühlturm"), Cat, 20, 24, CoolingTower,
                "cooling tower", "wet cooling tower", "natural draft", "hyperbolic", "induced draft", "evaporative cooling",
                "kühlturm", "nasskühlturm", "naturzug", "zwangsbelüftet", "verdunstungskühlung", "rückkühlwerk")
            {
                Params = new[] { Draft },
                Ports = Fixed(new Port("W1", 10, -1), new Port("W2", -10, 10.5)),
                Block = true, TagPrefix = "CT-101", Uniform = false, CompanionIds = Companions,
                Extra = o => Choice(o, "process.coolingtower.draft", null, Draft),
            };
        }

        private static void ShellTube(Pen p, SketchObject o)
        {
            const double r = 5;
            var fixedTubes = o.Str("bundle", "utube") == "fixed";
            // Channel (front head) with pass partition, shell, rear head.
            p.Rect(-16, -r, -12, r, S.Sym, "channel");
            p.Line(-12, -r, -12, r, S.Bold, "tubesheet");
            if (fixedTubes)
            {
                p.Rect(-12, -r, 12, r, S.Sym, ProcessTags.Body);
                p.Rect(12, -r, 16, r, S.Sym, "channel");
                p.Line(12, -r, 12, r, S.Bold, "tubesheet");
                for (var i = -1; i <= 1; i++) p.Line(-12, i * 2.4, 12, i * 2.4, S.Dashed, "tubes");
            }
            else
            {
                p.Line(-16, 0, -12, 0, S.Thin, "partition");
                var pts = new List<Vec2> { new(-12, -r), new(13, -r) };
                pts.AddRange(Pen.ArcPts(13, 0, 3, r, 270, 180));
                pts.Add(new Vec2(-12, r));
                p.PolyD(pts, S.Sym, false, ProcessTags.Body);
                var tube = new List<Vec2> { new(-12, -2.5), new(10, -2.5) };
                tube.AddRange(Pen.ArcPts(10, 0, 2.5, 2.5, 270, 180));
                tube.Add(new Vec2(-12, 2.5));
                p.PolyD(tube, S.Dashed, false, "tubes");
            }
            // Nozzle stubs to the box edge.
            p.Line(-14, -r, -14, -6, S.Sym, "nozzle");
            p.Line(fixedTubes ? 14 : -14, r, fixedTubes ? 14 : -14, 6, S.Sym, "nozzle");
            p.Line(7, -r, 7, -6, S.Sym, "nozzle");
            p.Line(-7, r, -7, 6, S.Sym, "nozzle");
        }

        private static void Plate(Pen p, SketchObject o)
        {
            var n = Math.Max(3, Math.Min(15, o.Int("plates", 7)));
            p.Rect(-6, -7, -4.6, 7, S.Sym, "frame");
            p.Rect(4.6, -7, 6, 7, S.Sym, "frame");
            p.Line(-6, -7.6, 6, -7.6, S.Sym, "bar");
            p.Line(-6, 7.6, 6, 7.6, S.Sym, "bar");
            for (var i = 0; i < n; i++)
            {
                var x = -3.6 + 7.2 * i / (n - 1);
                p.Line(x, -6.2, x, 6.2, S.Fine, "plate");
            }
            p.Line(-7, -4.5, -6, -4.5, S.Sym, "nozzle");
            p.Line(-7, 4.5, -6, 4.5, S.Sym, "nozzle");
            p.Line(6, 4.5, 7, 4.5, S.Sym, "nozzle");
            p.Line(6, -4.5, 7, -4.5, S.Sym, "nozzle");
        }

        private static void General(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 5, S.Sym, ProcessTags.Body);
            // Tube side as a coil through the shell, shell side nozzles at top and bottom.
            var pts = new List<Vec2> { new(-6, 0), new(-3.6, 0), new(-2.2, -2.4), new(-0.7, 2.4), new(0.7, -2.4), new(2.2, 2.4), new(3.6, 0), new(6, 0) };
            p.PolyD(pts, S.Sym, false, "coil");
            p.Line(0, -5, 0, -6, S.Sym, "nozzle");
            p.Line(0, 5, 0, 6, S.Sym, "nozzle");
        }

        private static void AirCooler(Pen p, SketchObject o)
        {
            // Finned tube bundle with headers, fan with motor below, supports.
            p.Rect(-10, -7, 10, -2, S.Sym, ProcessTags.Body);
            p.Line(-8.5, -7, -8.5, -2, S.Thin, "header");
            p.Line(8.5, -7, 8.5, -2, S.Thin, "header");
            p.ZigzagH(-8.5, 8.5, -4.5, 1.6, 5, S.Thin, "fins");
            p.Line(-12, -4.5, -10, -4.5, S.Sym, "nozzle");
            p.Line(10, -4.5, 12, -4.5, S.Sym, "nozzle");
            p.Ellipse(-3.6, 0.6, 3.2, 0.9, S.Thin, "fan");
            p.Ellipse(3.6, 0.6, 3.2, 0.9, S.Thin, "fan");
            p.Dot(0, 0.6, 0.5, "fan");
            p.Line(0, 0.6, 0, 2.6, S.Sym, "shaft");
            p.Circle(0, 4.6, 2.0, S.Sym, "motor");
            p.Text(0, 4.6, "M", 2.2, TextAnchor.Middle, "motor");
            p.Line(-10, -2, -10, 7, S.Sym, "support");
            p.Line(10, -2, 10, 7, S.Sym, "support");
        }

        /// <summary>Half width of the hyperbolic shell at height y (waist at y = −3).</summary>
        internal static double Hyperbola(double y) => 6 * Math.Sqrt(1 + Math.Pow((y + 3) / 9.4, 2));

        private static void CoolingTower(Pen p, SketchObject o)
        {
            double top = -12, basinTop = 9;
            if (o.Str("draft", "natural") == "mechanical")
            {
                p.Rect(-9, -7, 9, basinTop, S.Sym, ProcessTags.Body);
                // Fan stack with fan.
                p.Poly(S.Sym, false, "stack", -6, -7, -4.5, top, 4.5, top, 6, -7);
                p.Ellipse(-2.4, -9.6, 2.0, 0.6, S.Thin, "fan");
                p.Ellipse(2.4, -9.6, 2.0, 0.6, S.Thin, "fan");
                p.Dot(0, -9.6, 0.45, "fan");
                // Fill (packing).
                p.Rect(-9, 1.5, 9, 6, S.Thin, "fill");
                for (var x = -7.5; x <= 7.6; x += 3) p.Line(x - 1.5, 1.5, x + 1.5, 6, S.Fine, "fill");
                Spray(p, -7.5, 7.5, 10);
            }
            else
            {
                var left = new List<Vec2>();
                var right = new List<Vec2>();
                for (var i = 0; i <= 20; i++)
                {
                    var y = top + (basinTop - top) * i / 20.0;
                    var x = Math.Min(9.6, Hyperbola(y));
                    left.Add(new Vec2(-x, y));
                    right.Add(new Vec2(x, y));
                }
                p.PolyD(left, S.Sym, false, ProcessTags.Body);
                p.PolyD(right, S.Sym, false, ProcessTags.Body);
                var xs = Hyperbola(-1);
                Spray(p, -xs + 1, xs - 0.6, 10);
            }
            // Basin with water level.
            p.Rect(-10, basinTop, 10, 12, S.Sym, "basin");
            p.Line(-10, 10.5, -9.2, 10.5, S.Sym, "nozzle");
            p.Line(-8.5, basinTop + 0.9, 8.5, basinTop + 0.9, S.Dashed, "water");
        }

        /// <summary>Hot water distribution with spray nozzles at y = −1, fed from the right box edge.</summary>
        private static void Spray(Pen p, double x0, double x1, double feedX)
        {
            p.Line(x0, -1, feedX, -1, S.Sym, "spray");
            for (var x = x0 + 1; x < x1 - 0.5; x += 2.6)
                p.Poly(S.Black, true, "spray", x, -1, x - 0.7, 0.4, x + 0.7, 0.4);
        }
    }
}
