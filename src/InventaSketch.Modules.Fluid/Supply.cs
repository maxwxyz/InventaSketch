using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Fluid.Common;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>Supply (tank, pressure source, exhaust, silencer), conditioning (filter … heater, FRL unit) and accumulator.</summary>
    internal static class Supply
    {
        public const string Cat = "fluid.supply";

        private static readonly ParamDef TankKind = Choice("kind", T("Reservoir", "Behälter"), "open",
            ("open", "Open (vented)", "Offen (belüftet)"), ("closed", "Closed (pressurised)", "Geschlossen (mit Überdruck)"));
        private static readonly ParamDef AccuKind = Choice("kind", T("Loading", "Vorspannung"), "gas",
            ("gas", "Gas loaded", "Gasbelastet"), ("spring", "Spring loaded", "Federbelastet"), ("weight", "Weight loaded", "Gewichtsbelastet"));

        private static readonly string[] Companions = { "fluid.line", "fluid.pump", "fluid.valve.relief", "fluid.filter", "fluid.gauge" };
        private static readonly string[] PneuCompanions = { "fluid.line", "fluid.frl", "fluid.valve.directional", "fluid.gauge", "fluid.silencer" };

        public static IEnumerable<FluidType> Types()
        {
            yield return new FluidType("fluid.tank", T("Tank / reservoir", "Tank / Behälter"), Cat,
                new[] { "tank", "reservoir", "oil tank", "return", "tank", "behälter", "öltank", "rücklauf" },
                Views.Symbol(10, 5, Tank))
            {
                Params = new[] { TankKind }, Labeled = false,
                Ports = Fixed(new Port("T", 0, -2.5)),
                CompanionIds = Companions,
                Extra = o => ChoiceActions(o, "fluid.tank", null, TankKind),
            };

            yield return new FluidType("fluid.source", T("Pressure source", "Druckquelle"), Cat,
                new[] { "pressure source", "energy source", "supply", "compressed air supply", "druckquelle", "energiequelle", "druckluftversorgung", "versorgung" },
                Views.Symbol(10, 10, Source))
            {
                MediumDefault = "hydraulic", RefPrefix = "0Z1",
                Ports = Fixed(new Port("P", 0, -5)),
                CompanionIds = Companions,
            };

            yield return new FluidType("fluid.exhaust", T("Exhaust port", "Entlüftung"), Cat,
                new[] { "exhaust", "vent", "outlet to atmosphere", "entlüftung", "abluft", "auslass" },
                Views.Symbol(5, 5, Exhaust))
            {
                Labeled = false,
                Ports = Fixed(new Port("R", 0, -2.5)),
                CompanionIds = PneuCompanions,
            };

            yield return new FluidType("fluid.silencer", T("Silencer", "Schalldämpfer"), Cat,
                new[] { "silencer", "muffler", "exhaust silencer", "schalldämpfer", "dämpfer" },
                Views.Symbol(5, 10, Silencer))
            {
                Labeled = false,
                Ports = Fixed(new Port("R", 0, -5)),
                CompanionIds = PneuCompanions,
            };

            yield return Inline("fluid.filter", T("Filter", "Filter"), new[] { "filter", "strainer", "filter", "sieb", "filterelement" },
                (p, o) => { Diamond(p); p.Line(0, -5, 0, 5, S.Pilot, "element"); }, "0Z2");

            yield return Inline("fluid.separator", T("Water separator", "Wasserabscheider"), new[] { "water separator", "separator", "drain", "wasserabscheider", "abscheider", "kondensat" },
                Separator, "0Z3");

            yield return Inline("fluid.lubricator", T("Lubricator", "Öler"), new[] { "lubricator", "oiler", "öler", "nebelöler", "schmierung" },
                (p, o) =>
                {
                    Diamond(p);
                    p.Line(0, -5, 0, 5, null, "element");
                    p.Line(-1.6, 3.2, 1.6, 3.2, null, "element");
                }, "0Z4");

            yield return Inline("fluid.cooler", T("Cooler", "Kühler"), new[] { "cooler", "heat exchanger", "oil cooler", "kühler", "wärmetauscher", "ölkühler" },
                (p, o) =>
                {
                    Diamond(p);
                    p.Line(0, -2.4, 0, 2.4, null, "element");
                    p.Triangle(0, -4.4, 0, 0, false, 2.0, 1.1, "heat");
                    p.Triangle(0, 4.4, 0, 0, false, 2.0, 1.1, "heat");
                }, "0Z5");

            yield return Inline("fluid.heater", T("Heater", "Heizer"), new[] { "heater", "heating", "heizer", "heizung", "vorwärmer" },
                (p, o) =>
                {
                    Diamond(p);
                    p.Line(0, -4.6, 0, 4.6, null, "element");
                    p.Triangle(0, -0.5, 0, -4.6, false, 2.0, 1.1, "heat");
                    p.Triangle(0, 0.5, 0, 4.6, false, 2.0, 1.1, "heat");
                }, "0Z6");

            yield return new FluidType("fluid.frl", T("Service unit (FRL)", "Wartungseinheit (FRL)"), Cat,
                new[] { "frl", "service unit", "air preparation", "filter regulator lubricator", "wartungseinheit", "aufbereitung", "druckluftaufbereitung" },
                Views.Detailed(40, 20, FrlDetailed), Views.Simplified(15, 10, FrlSimplified))
            {
                RefPrefix = "0Z1", Uniform = false,
                Ports = o => o.View == "simplified"
                    ? new[] { new Port("P", -7.5, 0), new Port("A", 7.5, 0) }
                    : new[] { new Port("P", -20, 2.5), new Port("A", 20, 2.5) },
                CompanionIds = PneuCompanions,
            };

            yield return new FluidType("fluid.accumulator", T("Accumulator", "Hydrospeicher"), Cat,
                new[] { "accumulator", "bladder", "diaphragm", "gas accumulator", "hydrospeicher", "speicher", "blasenspeicher", "membranspeicher" },
                Views.Symbol(10, 15, Accumulator))
            {
                Params = new[] { AccuKind }, RefPrefix = "0Z7",
                Ports = Fixed(new Port("P", 0, 7.5)),
                CompanionIds = Companions,
                Extra = o => ChoiceActions(o, "fluid.accumulator", null, AccuKind),
            };

            yield return new FluidType(FluidValues.PowerUnitId, T("Hydraulic power unit", "Hydraulikaggregat"), Cat,
                new[] { "hydraulic power unit", "power unit", "power pack", "hpu", "pump unit", "motor pump group", "hydraulikaggregat", "aggregat",
                    "hydraulikeinheit", "pumpenaggregat", "motor-pumpen-einheit", "versorgungseinheit" },
                Views.Symbol(30, 25, PowerUnit))
            {
                RefPrefix = "0Z1",
                Ports = Fixed(new Port("P", -5, -12.5), new Port("T", 10, -12.5)),
                CompanionIds = new[] { "fluid.line", "fluid.valve.directional", "fluid.gauge", "fluid.accumulator", "fluid.valve.check" },
            };

            yield return new FluidType("fluid.vacuum.generator", T("Vacuum generator (ejector)", "Vakuumerzeuger (Ejektor)"), Cat,
                new[] { "vacuum generator", "ejector", "venturi nozzle", "vacuum", "vacuum ejector", "vakuumerzeuger", "ejektor", "vakuumdüse", "venturidüse", "vakuum" },
                Views.Symbol(15, 10, VacuumGenerator))
            {
                RefPrefix = "1Z2",
                Ports = Fixed(new Port("P", -7.5, 0), new Port("R", 7.5, 0), new Port("V", 0, 5)),
                CompanionIds = new[] { "fluid.suctioncup", "fluid.line", "fluid.silencer", "fluid.valve.directional", "fluid.pressureswitch" },
            };
        }

        /// <summary>
        /// Vacuum generator (ejector): housing with the driving nozzle (compressed air in at P), the diffuser towards the exhaust R
        /// and the suction channel from the vacuum port V into the gap between them.
        /// </summary>
        private static void VacuumGenerator(Pen p, SketchObject o)
        {
            p.Rect(-5, -3.5, 5, 3.5, null, "body");
            p.Line(-7.5, 0, -5, 0, null, "port");
            p.Line(5, 0, 7.5, 0, null, "port");
            p.Line(0, 3.5, 0, 5, null, "port");
            // Driving nozzle (converging) and diffuser (diverging).
            p.Line(-5, -2.2, -0.9, -0.6, null, "nozzle");
            p.Line(-5, 2.2, -0.9, 0.6, null, "nozzle");
            p.Line(0.9, -0.6, 5, -2.2, null, "diffuser");
            p.Line(0.9, 0.6, 5, 2.2, null, "diffuser");
            // Suction into the jet.
            p.Line(0, 3.5, 0, 1.6, S.Thin, "suction");
            p.Head(0, 0.8, 0, 3.0, 1.1, 0.45, "suction");
        }

        /// <summary>
        /// Hydraulic power unit as an assembly in a dash-dot enclosure: electric motor (M) driving a fixed displacement pump
        /// that draws from the tank, the pressure line to P with a relief valve back to the tank, and the return line from T
        /// through a filter into the tank (drawn independently, simplified).
        /// </summary>
        private static void PowerUnit(Pen p, SketchObject o)
        {
            p.Rect(-15, -12.5, 15, 12.5, S.Enclosure, "enclosure");
            // Tank across the bottom.
            p.Poly(S.Sym, false, "tank", -12.5, 7.5, -12.5, 11, 12.5, 11, 12.5, 7.5);
            // Electric motor driving the pump (shaft between them).
            p.Circle(-11.5, 1, 2.5, null, "motor");
            p.Text(-11.5, 1, "M", 2.6, TextAnchor.Middle, "motor");
            p.Line(-9, 1, -8, 1, S.Bold, "shaft");
            // Pump with the energy triangle pointing to the pressure port; suction line from the tank.
            p.Circle(-5, 1, 3, null, "pump");
            p.Triangle(-5, -2, -5, 1, false, 2.0, 1.2, "pump");
            p.Line(-5, 4, -5, 9.5, null, "suction");
            // Pressure line to P with the relief valve branching to the tank.
            p.Line(-5, -2, -5, -12.5, null, "pressure");
            p.Dot(-5, -5.5, 0.45);
            p.Line(-5, -5.5, 0, -5.5, null, "pressure");
            p.Rect(0, -8, 5, -3, null, "relief");
            p.Arrow(1.6, -7.2, 1.6, -3.8, S.Sym, "relief", 1.2, 0.5);
            p.SpringH(5, 7, -5.5, 0.9, 2, "relief");
            p.Line(2.5, -3, 2.5, 9.5, null, "relief");
            // Return line from T through the filter into the tank.
            p.Line(10, -12.5, 10, -2, null, "return");
            p.PolyD(Frag.Diamond(10, 1, 3, 3), null, true, "filter");
            p.Line(10, -2, 10, 4, S.Pilot, "filter");
            p.Line(10, 4, 10, 9.5, null, "return");
        }

        /// <summary>Inline conditioning element (diamond with ports left and right).</summary>
        private static FluidType Inline(string id, I18nText name, string[] keywords, Action<Pen, SketchObject> draw, string prefix)
            => new(id, name, Cat, keywords, Views.Symbol(10, 10, draw))
            {
                RefPrefix = prefix,
                Ports = Fixed(new Port("P", -5, 0), new Port("A", 5, 0)),
                CompanionIds = Companions,
            };

        private static void Diamond(Pen p, double cx = 0, double cy = 0, double rx = 5, double ry = 5)
            => p.PolyD(Frag.Diamond(cx, cy, rx, ry), null, true, "body");

        private static void Tank(Pen p, SketchObject o)
        {
            if (o.Str("kind", "open") == "closed")
            {
                p.Rect(-5, -2.5, 5, 2.5, null, "tank", 1.2);
                p.Line(0, -2.5, 0, 0.5, null, "line");
                return;
            }
            p.Poly(S.Sym, false, "tank", -5, -2.5, -5, 2.5, 5, 2.5, 5, -2.5);
            p.Line(0, -2.5, 0, 1.2, null, "line");
        }

        private static void Source(Pen p, SketchObject o)
        {
            p.Circle(0, 1.25, 3.75, null, "body");
            p.Line(0, -2.5, 0, -5, null, "line");
            p.Triangle(0, -2.3, 0, 1.25, Pneumatic(o), 2.6, 1.6);
        }

        private static void Exhaust(Pen p, SketchObject o)
        {
            p.Line(0, -2.5, 0, -0.6, null, "line");
            p.Triangle(0, 2.4, 0, -2.5, true, 3.0, 1.7, "exhaust");
        }

        private static void Silencer(Pen p, SketchObject o)
        {
            p.Line(0, -5, 0, -2, null, "line");
            p.Rect(-2.1, -2, 2.1, 4.8, null, "body");
            for (var i = 0; i < 3; i++)
            {
                var y = -0.5 + i * 1.8;
                p.Line(-2.1, y, 2.1, y, S.Thin, "baffle");
            }
        }

        private static void Separator(Pen p, SketchObject o)
        {
            p.PolyD(Frag.Diamond(0, 0, 5, 4), null, true, "body");
            // Collected condensate level and the manual drain at the bottom vertex.
            p.Line(-2.6, 1.6, 2.6, 1.6, null, "element");
            p.Line(0, 1.6, 0, 4, S.Thin, "element");
            p.Line(0, 4, 0, 5, null, "drain");
            p.Line(-1.2, 5, 1.2, 5, null, "drain");
        }

        private static void FrlDetailed(Pen p, SketchObject o)
        {
            const double y = 2.5;
            p.Rect(-20, -10, 20, 10, S.Enclosure, "enclosure");
            // Filter with water separator.
            Diamond(p, -13.5, y, 3.75, 3.75);
            p.Line(-13.5, y - 3.75, -13.5, y + 3.75, S.Pilot, "filter");
            p.Line(-15.3, y + 1.6, -11.7, y + 1.6, S.Thin, "filter");
            // Pressure regulator: box with arrow (normally open), pilot from the outlet, adjustable spring below.
            p.Rect(-4, y - 4, 4, y + 4, null, "regulator");
            p.Arrow(-4, y, 4, y);
            p.Poly(S.Pilot, false, "pilot", 6, y, 6, -4, 0, -4, 0, y - 4);
            p.SpringV(0, y + 4, 9.6, 1.4, 2);
            p.Adjust(-2, 9.4, 2, y + 4.6);
            // Gauge on the outlet.
            p.Line(9, y, 9, -3.3, null, "gauge");
            p.Circle(9, -5.8, 2.5, null, "gauge");
            p.Line(7.8, -4.6, 10.2, -7.0, S.Thin, "gauge");
            // Lubricator.
            Diamond(p, 14.75, y, 3.75, 3.75);
            p.Line(14.75, y - 3.75, 14.75, y + 3.75, null, "lubricator");
            p.Line(13.5, y + 2.2, 16, y + 2.2, null, "lubricator");
            // Working line through all elements.
            p.Line(-20, y, -17.25, y, null, "line");
            p.Line(-9.75, y, -4, y, null, "line");
            p.Line(4, y, 11, y, null, "line");
            p.Line(18.5, y, 20, y, null, "line");
            p.Dot(6, y, 0.45);
            p.Dot(9, y, 0.45);
        }

        private static void FrlSimplified(Pen p, SketchObject o)
        {
            p.Rect(-5, -5, 5, 5, null, "body");
            p.Line(-7.5, 0, -5, 0, null, "line");
            p.Line(5, 0, 7.5, 0, null, "line");
            Diamond(p, 0, 0, 3.4, 3.4);
            p.Line(0, -3.4, 0, 3.4, null, "element");
            p.Adjust(-3.6, 4.2, 3.6, -4.2);
        }

        private static void Accumulator(Pen p, SketchObject o)
        {
            p.Rect(-3.75, -7.5, 3.75, 5, null, "body", 3.75);
            p.Line(0, 5, 0, 7.5, null, "line");
            switch (o.Str("kind", "gas"))
            {
                case "spring":
                    p.SpringV(0, -6, -0.5, 2.2, 3);
                    p.Line(-3.75, -0.5, 3.75, -0.5, null, "separator");
                    break;
                case "weight":
                    p.Rect(-2.2, -5.5, 2.2, -1.8, S.Black, "weight");
                    p.Line(-3.75, -0.5, 3.75, -0.5, null, "separator");
                    break;
                default:
                    p.Line(-3.75, -1.25, 3.75, -1.25, null, "separator");
                    p.Triangle(0, -5.5, 0, -2.2, true, 2.6, 1.5, "gas");
                    break;
            }
        }
    }
}
