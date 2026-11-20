using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Fluid.Common;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>Measuring and signalling devices: pressure gauge, pressure switch, flow meter, thermometer.</summary>
    internal static class Measurement
    {
        public const string Cat = "fluid.measure";

        private static readonly string[] Companions = { "fluid.line", "fluid.junction", "fluid.valve.relief", "fluid.pump" };

        public static IEnumerable<FluidType> Types()
        {
            yield return new FluidType("fluid.gauge", T("Pressure gauge", "Manometer"), Cat,
                new[] { "pressure gauge", "manometer", "gauge", "pressure indicator", "manometer", "druckanzeige", "druckmesser" },
                Views.Symbol(10, 10, (p, o) =>
                {
                    Dial(p);
                    p.Arrow(-2.2, 0.9, 2.2, -3.4, null, "needle", 1.4, 0.55);
                }))
            {
                RefPrefix = "1Z1",
                Ports = Fixed(new Port("P", 0, 5)),
                CompanionIds = Companions,
            };

            yield return new FluidType("fluid.pressureswitch", T("Pressure switch", "Druckschalter"), Cat,
                new[] { "pressure switch", "pressure sensor", "electrical signal", "druckschalter", "druckwächter", "drucksensor" },
                Views.Symbol(10, 10, PressureSwitch))
            {
                RefPrefix = "1B1",
                Ports = Fixed(new Port("P", 0, 5)),
                CompanionIds = Companions,
            };

            yield return new FluidType("fluid.flowmeter", T("Flow meter", "Durchflussmesser"), Cat,
                new[] { "flow meter", "flowmeter", "flow sensor", "durchflussmesser", "volumenstrommesser", "durchfluss" },
                Views.Symbol(10, 10, (p, o) =>
                {
                    p.Circle(0, 0, 3.75, null, "body");
                    p.Line(-5, 0, 5, 0, null, "line");
                    p.Arrow(-1.9, 3.0, 1.9, -3.0, S.Thin, "indicator", 1.3, 0.5);
                }))
            {
                RefPrefix = "1Z2",
                Ports = Fixed(new Port("P", -5, 0), new Port("A", 5, 0)),
                CompanionIds = Companions,
            };

            yield return new FluidType("fluid.thermometer", T("Thermometer", "Thermometer"), Cat,
                new[] { "thermometer", "temperature", "temperature gauge", "thermometer", "temperatur", "temperaturanzeige" },
                Views.Symbol(10, 10, (p, o) =>
                {
                    Dial(p);
                    p.Line(0, -4.0, 0, -0.2, null, "stem");
                    p.Round(0, 0.5, 0.9, S.Black, "bulb");
                }))
            {
                RefPrefix = "1Z3",
                Ports = Fixed(new Port("P", 0, 5)),
                CompanionIds = Companions,
            };
        }

        /// <summary>Instrument circle above the connection at the bottom edge.</summary>
        private static void Dial(Pen p)
        {
            p.Circle(0, -1.25, 3.75, null, "body");
            p.Line(0, 2.5, 0, 5, null, "line");
        }

        private static void PressureSwitch(Pen p, SketchObject o)
        {
            // Pilot (dashed) from the port into the switch housing with a make contact.
            p.Line(0, 5, 0, 0, S.Pilot, "pilot");
            p.Rect(-4.5, -5, 4.5, 0, null, "housing");
            p.Line(-3.5, -1.6, -1.4, -1.6, null, "contact");
            p.Line(-1.4, -1.6, 1.8, -3.7, null, "contact");
            p.Line(1.4, -1.6, 3.5, -1.6, null, "contact");
            p.Line(1.4, -1.6, 1.4, -2.6, null, "contact");
        }
    }
}
