using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>Valves and pipe fittings (flow sheet symbols drawn independently, no standard compliance claimed).</summary>
    internal static class Valves
    {
        public const string Cat = ProcessCategories.Valves;

        private static readonly string[] Companions = { "process.line", "process.valve.manual", "process.instrument.bubble", "process.fitting.reducer" };

        internal static readonly ParamDef ValveKind = Choice("kind", "Valve type", "Armaturenart", "gate",
            ("gate", "Gate valve", "Absperrschieber"), ("globe", "Globe valve", "Absperrventil (Durchgang)"),
            ("ball", "Ball valve", "Kugelhahn"), ("butterfly", "Butterfly valve", "Absperrklappe"));
        internal static readonly ParamDef Closed = Flag("closed", "Normally closed", "Normal geschlossen", false);
        internal static readonly ParamDef Actuator = Choice("actuator", "Actuator", "Antrieb", "diaphragm",
            ("diaphragm", "Diaphragm (pneumatic)", "Membranantrieb (pneumatisch)"), ("motor", "Electric motor", "Elektromotor"),
            ("solenoid", "Solenoid", "Magnetantrieb"), ("piston", "Piston (cylinder)", "Kolbenantrieb (Zylinder)"));
        internal static readonly ParamDef Fail = Choice("fail", "Fail-safe position", "Sicherheitsstellung", "none",
            ("none", "Not shown", "Keine Angabe"), ("open", "Fail open (FO)", "Öffnet bei Ausfall (FO)"),
            ("close", "Fail closed (FC)", "Schließt bei Ausfall (FC)"), ("last", "Fail last (FL)", "Bleibt bei Ausfall (FL)"));
        internal static readonly ParamDef Actuated = Flag("actuated", "Motorised", "Mit Stellantrieb", false);
        internal static readonly ParamDef Eccentric = Flag("eccentric", "Eccentric (flat on top)", "Exzentrisch (oben eben)", false);
        internal static readonly ParamDef StrainerKind = Choice("kind", "Type", "Bauart", "filter",
            ("filter", "Filter (general)", "Filter (allgemein)"), ("y", "Y-strainer", "Schmutzfänger (Y-Form)"));

        public static IEnumerable<ProcessType> Types()
        {
            yield return new ProcessType("process.valve.manual", T("Valve (manual)", "Armatur (handbetätigt)"), Cat, 10, 6, Manual,
                "valve", "gate valve", "globe valve", "ball valve", "butterfly valve", "isolation valve", "shut-off valve", "block valve",
                "armatur", "ventil", "absperrventil", "schieber", "absperrschieber", "kugelhahn", "absperrklappe", "hahn")
            {
                Params = new[] { ValveKind, Closed },
                Ports = Fixed(new Port("1", -5, 0), new Port("2", 5, 0)),
                TagPrefix = "HV-101", CompanionIds = Companions,
                Extra = o => new[]
                {
                    Cycle("process.valve.kind", T("Valve type", "Armaturenart"), ValveKind),
                    Toggle(o, "process.valve.closed", T("Normally closed", "Normal geschlossen"), "closed", false),
                },
            };

            yield return new ProcessType("process.valve.control", T("Control valve", "Stellventil (Regelventil)"), Cat, 12, 13, Control,
                "control valve", "regulating valve", "actuated valve", "diaphragm actuator", "motor valve", "solenoid valve", "final control element",
                "stellventil", "regelventil", "stellglied", "membranventil", "motorventil", "magnetventil", "stellantrieb")
            {
                Params = new[] { Actuator, Fail },
                Ports = Fixed(new Port("1", -6, 3.5), new Port("2", 6, 3.5), new Port("S", 0, -6.5)),
                TagPrefix = "TV-101",
                CompanionIds = new[] { "process.instrument.bubble", "process.line.signal", "process.line", "process.valve.manual" },
                Extra = o => new[]
                {
                    Cycle("process.valve.actuator", T("Actuator", "Antrieb"), Actuator, IconIds.Actuation),
                    Cycle("process.valve.fail", T("Fail-safe position", "Sicherheitsstellung"), Fail),
                },
            };

            yield return new ProcessType("process.valve.check", T("Check valve", "Rückschlagarmatur"), Cat, 10, 6, Check,
                "check valve", "non-return valve", "nrv", "backflow preventer", "swing check",
                "rückschlagventil", "rückschlagklappe", "rückschlagarmatur", "rückflussverhinderer")
            {
                Ports = Fixed(new Port("1", -5, 0), new Port("2", 5, 0)),
                TagPrefix = "CV-101", CompanionIds = Companions,
            };

            yield return new ProcessType("process.valve.safety", T("Safety / relief valve", "Sicherheitsventil"), Cat, 10, 12, Safety,
                "safety valve", "relief valve", "pressure relief valve", "psv", "prv", "pressure safety",
                "sicherheitsventil", "überdruckventil", "druckbegrenzungsventil", "abblaseventil")
            {
                Ports = Fixed(new Port("in", 0, 6), new Port("out", 5, 1.5)),
                TagPrefix = "PSV-101", CompanionIds = new[] { "process.line", "process.vessel.vertical", "process.vessel.horizontal" },
            };

            yield return new ProcessType("process.valve.threeway", T("Three-way valve", "Dreiwegeventil"), Cat, 9, 7, ThreeWay,
                "three-way valve", "3-way valve", "mixing valve", "diverting valve", "hvac",
                "dreiwegeventil", "3-wege-ventil", "mischventil", "verteilventil", "umschaltventil", "heizung")
            {
                Params = new[] { Actuated },
                DesignFor = o => new Size2(9, o.Flag("actuated", false) ? 12 : 7),
                Ports = o =>
                {
                    var act = o.Flag("actuated", false);
                    var yc = act ? 1.5 : -1;
                    var list = new List<Port> { new("A", -4.5, yc), new("AB", 4.5, yc), new("B", 0, yc + 4.5) };
                    if (act) list.Add(new Port("S", 0, -6));
                    return list;
                },
                TagPrefix = "TV-102",
                CompanionIds = new[] { "process.line", "process.pump.centrifugal", "process.hvac.coil", "process.instrument.bubble" },
                Extra = o => new[] { Toggle(o, "process.valve.actuated", T("Motorised", "Mit Stellantrieb"), "actuated", false, IconIds.Actuation) },
            };

            yield return new ProcessType("process.fitting.reducer", T("Reducer", "Reduzierstück"), Cat, 8, 6, Reducer,
                "reducer", "concentric reducer", "eccentric reducer", "size change", "reduzierstück", "reduzierung", "übergangsstück")
            {
                Params = new[] { Eccentric },
                Ports = o => new[] { new Port("1", -4, 0), new Port("2", 4, o.Flag("eccentric", false) ? -1.5 : 0) },
                CompanionIds = Companions,
                Extra = o => new[] { Toggle(o, "process.reducer.eccentric", T("Eccentric", "Exzentrisch"), "eccentric", false) },
            };

            yield return new ProcessType("process.fitting.strainer", T("Strainer / filter", "Schmutzfänger / Filter"), Cat, 10, 8, Strainer,
                "strainer", "filter", "y-strainer", "basket strainer", "sieve",
                "schmutzfänger", "filter", "sieb", "siebfilter", "y-schmutzfänger", "korbfilter")
            {
                Params = new[] { StrainerKind },
                Ports = o => o.Str("kind", "filter") == "y"
                    ? new[] { new Port("1", -5, -2), new Port("2", 5, -2) }
                    : new[] { new Port("1", -5, 0), new Port("2", 5, 0) },
                TagPrefix = "S-101", CompanionIds = Companions,
                Extra = o => new[] { Cycle("process.strainer.kind", T("Filter / Y-strainer", "Filter / Y-Schmutzfänger"), StrainerKind) },
            };
        }

        private static void Manual(Pen p, SketchObject o)
        {
            var closed = o.Flag("closed", false);
            var fill = closed ? S.Black : S.Sym;
            switch (o.Str("kind", "gate"))
            {
                case "butterfly":
                    p.Line(-5, 0, -2.4, 0, S.Sym, "pipe");
                    p.Line(2.4, 0, 5, 0, S.Sym, "pipe");
                    p.Line(-2.4, -3, -2.4, 3, S.Sym, "valve");
                    p.Line(2.4, -3, 2.4, 3, S.Sym, "valve");
                    p.Line(-1.6, 2.4, 1.6, -2.4, closed ? S.Bold : S.Sym, "disc");
                    p.Dot(0, 0, 0.55, "disc");
                    break;
                case "globe":
                    p.BowTie(0, 0, 5, 3, fill, fill);
                    p.Round(0, 0, 0.9, S.Black, "plug");
                    break;
                case "ball":
                    p.BowTie(0, 0, 5, 3, fill, fill);
                    p.Round(0, 0, 1.5, closed ? S.Black : S.White, "ball");
                    break;
                default:
                    p.BowTie(0, 0, 5, 3, fill, fill);
                    break;
            }
        }

        private static void Control(Pen p, SketchObject o)
        {
            const double yc = 3.5;
            p.BowTie(0, yc, 6, 3);
            p.Line(0, yc, 0, -1.5, S.Sym, "stem");
            switch (o.Str("actuator", "diaphragm"))
            {
                case "motor":
                    p.Circle(0, -3.8, 2.3, S.Sym, "actuator");
                    p.Text(0, -3.8, "M", 2.4, TextAnchor.Middle, "actuator");
                    p.Line(0, -6.1, 0, -6.5, S.Sym, "signal");
                    break;
                case "solenoid":
                    p.Rect(-2.3, -6.1, 2.3, -1.5, S.Sym, "actuator");
                    p.Text(0, -3.8, "S", 2.4, TextAnchor.Middle, "actuator");
                    p.Line(0, -6.1, 0, -6.5, S.Sym, "signal");
                    break;
                case "piston":
                    p.Rect(-3, -5.6, 3, -1.5, S.Sym, "actuator");
                    p.Line(-3, -3.4, 3, -3.4, S.Thin, "actuator");
                    p.Line(0, -5.6, 0, -6.5, S.Sym, "signal");
                    break;
                default:
                    p.Arc(0, -1.5, 3.6, 3.6, 180, 180, S.Sym, "actuator");
                    p.Line(-3.6, -1.5, 3.6, -1.5, S.Sym, "actuator");
                    p.Line(0, -5.1, 0, -6.5, S.Sym, "signal");
                    break;
            }
            var fail = o.Str("fail", "none") switch { "open" => "FO", "close" => "FC", "last" => "FL", _ => "" };
            p.Text(3.9, -0.4, fail, 1.9, TextAnchor.Middle, "fail");
        }

        private static void Check(Pen p, SketchObject o)
        {
            // Flow from port 1 to port 2; the outlet half is filled, an arrow above names the flow direction.
            p.BowTie(0, 0.8, 5, 2.2, S.Sym, S.Black);
            p.Arrow(-2.4, -2.4, 2.4, -2.4, S.Thin, "flow", 1.2, 0.45);
        }

        private static void Safety(Pen p, SketchObject o)
        {
            const double cx = 0, cy = 1.5;
            p.Poly(S.Sym, true, "valve", cx, cy, cx - 2.6, 6, cx + 2.6, 6);
            p.Poly(S.Sym, true, "valve", cx, cy, 5, cy - 2.6, 5, cy + 2.6);
            // Spring above the seat with its cap, the adjustment arrow across it.
            p.Line(0, cy, 0, 0.2, S.Sym, "stem");
            p.ZigzagV(0, 0.2, -4.4, 1.0, 3, S.Thin, "spring");
            p.Line(-1.5, -4.4, 1.5, -4.4, S.Sym, "spring");
            p.Arrow(-2.0, -0.4, 2.2, -5.4, S.Fine, "adjust", 0.9, 0.35);
        }

        private static void ThreeWay(Pen p, SketchObject o)
        {
            var act = o.Flag("actuated", false);
            var yc = act ? 1.5 : -1;
            p.BowTie(0, yc, 4.5, 2.5);
            p.Poly(S.Sym, true, "valve", 0, yc, -2.5, yc + 4.5, 2.5, yc + 4.5);
            if (!act) return;
            p.Line(0, yc, 0, -1.9, S.Sym, "stem");
            p.Circle(0, -3.9, 2.0, S.Sym, "actuator");
            p.Text(0, -3.9, "M", 2.1, TextAnchor.Middle, "actuator");
        }

        private static void Reducer(Pen p, SketchObject o)
        {
            if (o.Flag("eccentric", false)) p.Poly(S.Sym, true, ProcessTags.Body, -4, -3, 4, -3, 4, 0, -4, 3);
            else p.Poly(S.Sym, true, ProcessTags.Body, -4, -3, 4, -1.5, 4, 1.5, -4, 3);
        }

        private static void Strainer(Pen p, SketchObject o)
        {
            if (o.Str("kind", "filter") == "y")
            {
                p.Line(-5, -2, 5, -2, S.Sym, "pipe");
                // Inclined screen chamber with a dashed screen and the blow-down cap.
                p.Poly(S.Sym, false, ProcessTags.Body, -1.8, -2, 0.9, 3.4, 3.3, 3.4, 0.6, -2);
                p.Line(0.9, 3.4, 3.3, 3.4, S.Bold, "cap");
                p.Line(-0.5, -1.4, 2.0, 3.0, S.Dashed, "screen");
                return;
            }
            p.Line(-5, 0, -3.5, 0, S.Sym, "pipe");
            p.Line(3.5, 0, 5, 0, S.Sym, "pipe");
            p.Poly(S.Sym, true, ProcessTags.Body, -3.5, 0, 0, -3.5, 3.5, 0, 0, 3.5);
            p.Line(0, -3.5, 0, 3.5, S.Dashed, "screen");
        }
    }
}
