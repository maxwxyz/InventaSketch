using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// HVAC process items of an air handling unit schematic: coil, damper, filter, heat recovery and humidifier, each a
    /// section of the air path (air enters at the left, leaves at the right). Drawn independently following common
    /// schematic practice; no standard compliance is claimed.
    /// </summary>
    internal static class Hvac
    {
        public const string Cat = ProcessCategories.Hvac;

        private static readonly string[] Companions = { "process.fan", "process.hvac.filter", "process.hvac.coil", "process.hvac.damper", "process.hvac.recovery" };

        internal static readonly ParamDef CoilKind = Choice("kind", "Coil", "Register", "heating",
            ("heating", "Heating coil (+)", "Lufterhitzer (+)"), ("cooling", "Cooling coil (−)", "Luftkühler (−)"));
        internal static readonly ParamDef Motorised = Flag("motorised", "Motorised", "Mit Stellantrieb", false);
        internal static readonly ParamDef FilterKind = Choice("kind", "Filter type", "Filterart", "panel",
            ("panel", "Panel filter", "Plattenfilter"), ("bag", "Bag filter", "Taschenfilter"));
        internal static readonly ParamDef RecoveryKind = Choice("kind", "Heat recovery", "Wärmerückgewinnung", "plate",
            ("plate", "Cross-flow plate exchanger", "Kreuzstrom-Plattenwärmeübertrager"), ("wheel", "Rotary wheel", "Rotationswärmeübertrager"));
        internal static readonly ParamDef HumidifierKind = Choice("kind", "Humidifier", "Befeuchter", "steam",
            ("steam", "Steam humidifier", "Dampfbefeuchter"), ("spray", "Spray (evaporative) humidifier", "Sprühbefeuchter"));

        public static IEnumerable<ProcessType> Types()
        {
            yield return new ProcessType("process.hvac.coil", T("Air heating / cooling coil", "Lufterhitzer / Luftkühler"), Cat, 10, 12, Coil,
                "coil", "heating coil", "cooling coil", "air heater", "air cooler", "ahu", "hvac",
                "register", "lufterhitzer", "luftkühler", "heizregister", "kühlregister", "rlt", "lüftungsgerät", "klima")
            {
                Params = new[] { CoilKind },
                Ports = Fixed(new Port("A1", -5, 0), new Port("A2", 5, 0), new Port("W1", -2.5, 6), new Port("W2", 2.5, 6)),
                Uniform = false, CompanionIds = Companions,
                Extra = o => new[] { Cycle("process.coil.kind", T("Heating / cooling", "Heizen / Kühlen"), CoilKind) },
            };

            yield return new ProcessType("process.hvac.damper", T("Air damper", "Luftklappe"), Cat, 10, 10, Damper,
                "damper", "air damper", "shut-off damper", "volume control damper", "fire damper", "ahu", "hvac",
                "luftklappe", "jalousieklappe", "absperrklappe", "regelklappe", "rlt", "lüftung")
            {
                Params = new[] { Motorised },
                Ports = Fixed(new Port("A1", -5, 0), new Port("A2", 5, 0)),
                Uniform = false, CompanionIds = Companions,
                Extra = o => new[] { Toggle(o, "process.damper.motorised", T("Motorised", "Mit Stellantrieb"), "motorised", false, IconIds.Actuation) },
            };

            yield return new ProcessType("process.hvac.filter", T("Air filter", "Luftfilter"), Cat, 10, 10, Filter,
                "filter", "air filter", "panel filter", "bag filter", "ahu", "hvac",
                "luftfilter", "filter", "taschenfilter", "plattenfilter", "rlt", "lüftung")
            {
                Params = new[] { FilterKind },
                Ports = Fixed(new Port("A1", -5, 0), new Port("A2", 5, 0)),
                Uniform = false, CompanionIds = Companions,
                Extra = o => new[] { Cycle("process.filter.kind", T("Panel / bag filter", "Platten- / Taschenfilter"), FilterKind) },
            };

            yield return new ProcessType("process.hvac.recovery", T("Heat recovery", "Wärmerückgewinnung"), Cat, 10, 20, Recovery,
                "heat recovery", "heat recovery unit", "cross-flow", "plate heat exchanger", "thermal wheel", "rotary wheel", "ahu", "hvac",
                "wärmerückgewinnung", "wrg", "kreuzstrom", "plattenwärmeübertrager", "rotationswärmeübertrager", "rotor", "rlt")
            {
                Params = new[] { RecoveryKind },
                Ports = Fixed(new Port("S1", -5, 5), new Port("S2", 5, 5), new Port("E1", 5, -5), new Port("E2", -5, -5)),
                Uniform = false, CompanionIds = Companions,
                Extra = o => new[] { Cycle("process.recovery.kind", T("Plate / wheel", "Platte / Rotor"), RecoveryKind) },
            };

            yield return new ProcessType("process.hvac.humidifier", T("Humidifier", "Luftbefeuchter"), Cat, 10, 12, Humidifier,
                "humidifier", "steam humidifier", "spray humidifier", "evaporative", "ahu", "hvac",
                "luftbefeuchter", "befeuchter", "dampfbefeuchter", "sprühbefeuchter", "rlt", "klima")
            {
                Params = new[] { HumidifierKind },
                Ports = Fixed(new Port("A1", -5, 0), new Port("A2", 5, 0), new Port("W1", 0, 6)),
                Uniform = false, CompanionIds = Companions,
                Extra = o => new[] { Cycle("process.humidifier.kind", T("Steam / spray", "Dampf / Sprüh"), HumidifierKind) },
            };
        }

        /// <summary>The air section: a square duct section of the box width, vertically centred.</summary>
        private static void Section(Pen p, double top = -5, double bottom = 5) => p.Rect(-5, top, 5, bottom, S.Sym, ProcessTags.Body);

        private static void Coil(Pen p, SketchObject o)
        {
            Section(p);
            p.Line(-5, 5, 5, -5, S.Sym, "coil");
            var heating = o.Str("kind", "heating") != "cooling";
            // Sign in the upper left triangle.
            p.Line(-3.6, -2.4, -1.2, -2.4, S.Sym, "sign");
            if (heating) p.Line(-2.4, -3.6, -2.4, -1.2, S.Sym, "sign");
            p.Line(-2.5, 5, -2.5, 6, S.Sym, "nozzle");
            p.Line(2.5, 5, 2.5, 6, S.Sym, "nozzle");
        }

        private static void Damper(Pen p, SketchObject o)
        {
            Section(p);
            p.Line(-2.6, 2.6, 2.6, -2.6, S.Bold, "blade");
            p.Dot(0, 0, 0.5, "blade");
            if (o.Flag("motorised", false))
            {
                p.Circle(-3.0, -3.0, 1.5, S.Sym, "actuator");
                p.Text(-3.0, -3.0, "M", 1.6, TextAnchor.Middle, "actuator");
            }
        }

        private static void Filter(Pen p, SketchObject o)
        {
            Section(p);
            if (o.Str("kind", "panel") == "bag")
            {
                for (var i = 0; i < 3; i++)
                {
                    var y = -5 + i * 10 / 3.0;
                    p.Poly(S.Thin, false, "media", -3.4, y, 3.4, y + 10 / 6.0, -3.4, y + 10 / 3.0);
                }
                p.Line(-3.4, -5, -3.4, 5, S.Thin, "frame");
            }
            else p.ZigzagV(0, -5, 5, 2.4, 4, S.Thin, "media");
        }

        private static void Recovery(Pen p, SketchObject o)
        {
            p.Rect(-5, -10, 5, 10, S.Sym, ProcessTags.Body);
            p.Line(-5, 0, 5, 0, S.Thin, "partition");
            if (o.Str("kind", "plate") == "wheel")
            {
                p.Ellipse(0, 0, 2.2, 8.4, S.Sym, "wheel");
                p.Line(0, -8.4, 0, 8.4, S.Fine, "wheel");
                p.Arc(0, 0, 3.6, 3.6, 200, 140, S.Fine, "rotation");
                p.Head(Math.Cos(Geo.DegToRad(340)) * 3.6, Math.Sin(Geo.DegToRad(340)) * 3.6,
                    Math.Cos(Geo.DegToRad(320)) * 3.6, Math.Sin(Geo.DegToRad(320)) * 3.6, 1.0, 0.4, "rotation");
            }
            else
            {
                // Cross-flow plate pack seen as a square on its corner.
                p.Poly(S.Sym, true, "plates", 0, -4.8, 4.8, 0, 0, 4.8, -4.8, 0);
                p.Line(-2.4, -2.4, 2.4, 2.4, S.Fine, "plates");
                p.Line(2.4, -2.4, -2.4, 2.4, S.Fine, "plates");
            }
            // Air directions: supply air (lower) left to right, extract air (upper) right to left.
            p.Arrow(-4.4, 7.6, -2.0, 7.6, S.Thin, "flow", 1.0, 0.4);
            p.Arrow(4.4, -7.6, 2.0, -7.6, S.Thin, "flow", 1.0, 0.4);
        }

        private static void Humidifier(Pen p, SketchObject o)
        {
            Section(p);
            if (o.Str("kind", "steam") == "spray")
            {
                p.Line(0, 6, 0, -2.6, S.Sym, "lance");
                foreach (var y in new[] { -2.6, 0.4, 3.2 })
                {
                    p.Line(0, y, 2.4, y - 1.2, S.Fine, "spray");
                    p.Line(0, y, 2.6, y, S.Fine, "spray");
                    p.Line(0, y, 2.4, y + 1.2, S.Fine, "spray");
                }
                return;
            }
            p.Line(0, 6, 0, -3.6, S.Sym, "lance");
            foreach (var y in new[] { -3.0, -0.6, 1.8 })
            {
                // Steam outlets: short wavy plumes downstream.
                p.Arc(1.2, y, 1.2, 0.6, 180, 180, S.Fine, "steam");
                p.Arc(3.6, y, 1.2, 0.6, 180, -180, S.Fine, "steam");
            }
        }
    }
}
