using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Fluid.Common;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>Energy conversion: pumps/compressors, motors, cylinders and the rotary actuator.</summary>
    internal static class Energy
    {
        public const string Cat = "fluid.energy";

        private static readonly ParamDef Variable = Flag("variable", T("Variable displacement", "Verstellbar"), false);
        private static readonly ParamDef Flow = Choice("flow", T("Flow directions", "Stromrichtungen"), "one",
            ("one", "One flow direction", "Eine Stromrichtung"), ("two", "Two flow directions", "Zwei Stromrichtungen"));
        private static readonly ParamDef Rod = Choice("rod", T("Piston rod", "Kolbenstange"), "single",
            ("single", "Single rod", "Einseitig"), ("double", "Through rod (double)", "Durchgehend (beidseitig)"));
        private static readonly ParamDef Cushion = Choice("cushion", T("End-position cushioning", "Endlagendämpfung"), "none",
            ("none", "None", "Keine"), ("fixed", "Fixed, both ends", "Fest, beidseitig"), ("adjustable", "Adjustable, both ends", "Einstellbar, beidseitig"));
        private static readonly ParamDef Spring = Choice("spring", T("Spring", "Feder"), "return",
            ("return", "Spring return (push type)", "Federrückstellung (drückend)"),
            ("extend", "Spring extend (pull type)", "Federvorhub (ziehend)"),
            ("none", "No spring (external return)", "Ohne Feder (Rückstellung durch äußere Kraft)"));

        private static readonly string[] CylCompanions = { "fluid.valve.directional", "fluid.valve.throttle", "fluid.valve.check", "fluid.line", "fluid.gauge" };
        private static readonly string[] RotCompanions = { "fluid.line", "fluid.valve.relief", "fluid.valve.directional", "fluid.tank", "fluid.gauge" };

        public static IEnumerable<FluidType> Types()
        {
            yield return new FluidType("fluid.pump", T("Pump / compressor", "Pumpe / Verdichter"), Cat,
                new[] { "pump", "hydraulic pump", "compressor", "air compressor", "displacement", "pumpe", "hydropumpe", "verdichter", "kompressor", "verstellpumpe", "konstantpumpe" },
                Views.Symbol(15, 10, (p, o) => Machine(p, o, pump: true)))
            {
                MediumDefault = "hydraulic", Params = new[] { Variable, Flow }, RefPrefix = "0P1",
                Ports = o => new[] { new Port("P", -2.5, -5), new Port("S", -2.5, 5) },
                CompanionIds = RotCompanions,
                Extra = o => new[]
                {
                    Toggle(o, "fluid.variable", T("Fixed / variable displacement", "Konstant / verstellbar"), "variable", false),
                    Cycle("fluid.flow.next", T("One / two flow directions", "Eine / zwei Stromrichtungen"), Flow),
                },
            };

            yield return new FluidType("fluid.motor", T("Motor (hydraulic/pneumatic)", "Motor (Hydro-/Druckluftmotor)"), Cat,
                new[] { "motor", "hydraulic motor", "air motor", "pneumatic motor", "rotary", "hydromotor", "druckluftmotor", "konstantmotor", "verstellmotor" },
                Views.Symbol(15, 10, (p, o) => Machine(p, o, pump: false)))
            {
                MediumDefault = "hydraulic", Params = new[] { Variable, Flow }, RefPrefix = "1A1",
                Ports = o => new[] { new Port("A", -2.5, -5), new Port("B", -2.5, 5) },
                CompanionIds = RotCompanions,
                Extra = o => new[]
                {
                    Toggle(o, "fluid.variable", T("Fixed / variable displacement", "Konstant / verstellbar"), "variable", false),
                    Cycle("fluid.flow.next", T("One / two flow directions", "Eine / zwei Stromrichtungen"), Flow),
                },
            };

            yield return new FluidType("fluid.cylinder.double", T("Double-acting cylinder", "Doppeltwirkender Zylinder"), Cat,
                new[] { "cylinder", "double-acting", "actuator", "piston", "ram", "zylinder", "doppeltwirkend", "arbeitszylinder", "kolben" },
                Views.Symbol(45, 10, DoubleCylinder))
            {
                Params = new[] { Rod, Cushion }, RefPrefix = "1A1", Uniform = false,
                Ports = o => o.Str("rod", "single") == "double"
                    ? new[] { new Port("A", -12.5, 5), new Port("B", 12.5, 5) }
                    : new[] { new Port("A", -20, 5), new Port("B", 5, 5) },
                CompanionIds = CylCompanions,
                Extra = o => new[]
                {
                    new ContextAction("fluid.cyl.rod", T("Through rod", "Durchgehende Stange"), IconIds.Toggle,
                        x => x.WithParam("rod", x.Str("rod", "single") == "double" ? "single" : "double")) { IsActive = o.Str("rod", "single") == "double", Group = "param" },
                    Cycle("fluid.cyl.cushion.next", T("Next cushioning (none / fixed / adjustable)", "Nächste Dämpfung (keine / fest / einstellbar)"), Cushion),
                },
            };

            yield return new FluidType("fluid.cylinder.single", T("Single-acting cylinder", "Einfachwirkender Zylinder"), Cat,
                new[] { "cylinder", "single-acting", "spring return", "plunger", "zylinder", "einfachwirkend", "federrückstellung", "plunger" },
                Views.Symbol(45, 10, SingleCylinder))
            {
                Params = new[] { Spring }, RefPrefix = "1A1", Uniform = false,
                Ports = o => o.Str("spring", "return") == "extend" ? new[] { new Port("B", 5, 5) } : new[] { new Port("A", -20, 5) },
                CompanionIds = CylCompanions,
                Extra = o => new[] { Cycle("fluid.cyl.spring.next", T("Next spring arrangement", "Nächste Federanordnung"), Spring) },
            };

            yield return new FluidType("fluid.rotary", T("Rotary actuator (limited angle)", "Schwenkantrieb (begrenzter Winkel)"), Cat,
                new[] { "rotary actuator", "semi-rotary", "swivel", "limited angle", "schwenkantrieb", "schwenkmotor", "drehantrieb" },
                Views.Symbol(15, 10, Rotary))
            {
                MediumDefault = "pneumatic", RefPrefix = "1A1",
                Ports = Fixed(new Port("A", -5, 5), new Port("B", 0, 5)),
                CompanionIds = RotCompanions,
            };
        }

        /// <summary>Pump or motor: circle with energy triangles (out for pumps, in for motors), shaft and optional variable arrow.</summary>
        private static void Machine(Pen p, SketchObject o, bool pump)
        {
            const double cx = -2.5, r = 5;
            var hollow = Pneumatic(o);
            p.Circle(cx, 0, r, null, "body");
            // Drive / output shaft: double line to the right.
            var sy = 0.6;
            var sx = cx + Math.Sqrt(r * r - sy * sy);
            p.Line(sx, -sy, 7.5, -sy, null, "shaft");
            p.Line(sx, sy, 7.5, sy, null, "shaft");
            var two = o.Str("flow", "one") == "two";
            if (pump)
            {
                p.Triangle(cx, -r, cx, 0, hollow);
                if (two) p.Triangle(cx, r, cx, 0, hollow);
            }
            else
            {
                p.Triangle(cx, -r + 2.4, cx, -r, hollow);
                if (two) p.Triangle(cx, r - 2.4, cx, r, hollow);
            }
            if (o.Flag("variable", false)) p.Adjust(cx - 4.3, 4.3, cx + 4.3, -4.3, "variable");
        }

        private static void Cushions(Pen p, double piston, double left, double right, bool leftRod, bool rightRod, string mode)
        {
            if (mode == "none") return;
            // Cushion spigots on both sides of the piston.
            p.Rect(piston - 2.5, leftRod ? -1.8 : -1.5, piston, leftRod ? 1.8 : 1.5, S.Thin, "cushion");
            p.Rect(piston, rightRod ? -1.8 : -1.5, piston + 2.5, rightRod ? 1.8 : 1.5, S.Thin, "cushion");
            if (mode == "adjustable")
            {
                p.Adjust(left + 0.8, 4.2, left + 4.2, -4.2, "cushion-adjust");
                p.Adjust(right - 4.2, 4.2, right - 0.8, -4.2, "cushion-adjust");
            }
        }

        private static void DoubleCylinder(Pen p, SketchObject o)
        {
            if (o.Str("rod", "single") == "double")
            {
                p.Rect(-15, -5, 15, 5, null, "barrel");
                p.Rect(-22.5, -0.9, 22.5, 0.9, null, "rod");
                p.Line(0, -5, 0, 5, S.Bold, "piston");
                Cushions(p, 0, -15, 15, true, true, o.Str("cushion", "none"));
                return;
            }
            p.Rect(-22.5, -5, 7.5, 5, null, "barrel");
            p.Rect(-15, -0.9, 22.5, 0.9, null, "rod");
            p.Line(-15, -5, -15, 5, S.Bold, "piston");
            Cushions(p, -15, -22.5, 7.5, false, true, o.Str("cushion", "none"));
        }

        private static void SingleCylinder(Pen p, SketchObject o)
        {
            p.Rect(-22.5, -5, 7.5, 5, null, "barrel");
            p.Rect(-15, -0.9, 22.5, 0.9, null, "rod");
            p.Line(-15, -5, -15, 5, S.Bold, "piston");
            switch (o.Str("spring", "return"))
            {
                case "return": p.SpringH(-14.5, 7.5, 0, 3.5, 4); break;
                case "extend": p.SpringH(-22.5, -15.5, 0, 3.5, 3); break;
            }
        }

        private static void Rotary(Pen p, SketchObject o)
        {
            const double cx = -2.5, r = 5;
            var hollow = Pneumatic(o);
            // Semicircular housing (limited swivel angle) with the flat side down and the two ports below.
            p.Arc(cx, 2, r, 6.5, 180, 180, null, "body");
            p.Line(cx - r, 2, cx + r, 2, null, "body");
            p.Line(-5, 2, -5, 5, null, "port");
            p.Line(0, 2, 0, 5, null, "port");
            p.Triangle(-5, -0.4, -5, 2, hollow, 2.0, 1.1);
            p.Triangle(0, -0.4, 0, 2, hollow, 2.0, 1.1);
            // Swivel arrow (both directions) and output shaft.
            var arc = Geo.ArcPoints(new Vec2(cx, 2), 4, 5, Geo.DegToRad(235), Geo.DegToRad(70), 12);
            p.PolyD(arc, S.Thin, false, "swivel");
            p.Head(arc[0].X, arc[0].Y, arc[1].X, arc[1].Y, 1.1, 0.45, "swivel");
            p.Head(arc[arc.Count - 1].X, arc[arc.Count - 1].Y, arc[arc.Count - 2].X, arc[arc.Count - 2].Y, 1.1, 0.45, "swivel");
            p.Line(cx + r, 1.4, 7.5, 1.4, null, "shaft");
            p.Line(cx + r - 0.2, 0.2, 7.5, 0.2, null, "shaft");
        }
    }
}
