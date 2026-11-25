using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>Pumps, compressors, turbines, fans and drives (flow sheet symbols drawn independently).</summary>
    internal static class Machines
    {
        public const string Cat = ProcessCategories.Machines;

        private static readonly string[] Companions = { "process.line", "process.valve.manual", "process.valve.check", "process.motor", "process.instrument.bubble" };

        internal static readonly ParamDef Base = Flag("base", "Base frame", "Grundrahmen", true);
        internal static readonly ParamDef PdKind = Choice("kind", "Type", "Bauart", "gear",
            ("gear", "Gear pump", "Zahnradpumpe"), ("screw", "Screw pump", "Schraubenspindelpumpe"),
            ("piston", "Piston (reciprocating) pump", "Kolbenpumpe"), ("diaphragm", "Diaphragm pump", "Membranpumpe"));
        internal static readonly ParamDef CompKind = Choice("kind", "Type", "Bauart", "dynamic",
            ("dynamic", "Turbo (centrifugal/axial)", "Turboverdichter"), ("reciprocating", "Reciprocating (piston)", "Kolbenverdichter"));
        internal static readonly ParamDef FanKind = Choice("kind", "Type", "Bauart", "radial",
            ("radial", "Centrifugal (radial) fan", "Radialventilator"), ("axial", "Axial fan", "Axialventilator"));

        public static IEnumerable<ProcessType> Types()
        {
            yield return new ProcessType("process.pump.centrifugal", T("Centrifugal pump", "Kreiselpumpe"), Cat, 12, 12, Centrifugal,
                "pump", "centrifugal pump", "process pump", "circulation pump", "feed pump",
                "pumpe", "kreiselpumpe", "zentrifugalpumpe", "förderpumpe", "umwälzpumpe", "speisepumpe")
            {
                Params = new[] { Base },
                Ports = Fixed(new Port("S", -6, 0.6), new Port("D", 6, -4)),
                Block = true, TagPrefix = "P-101", CompanionIds = Companions,
                Extra = o => new[] { Toggle(o, "process.pump.base", T("Base frame", "Grundrahmen"), "base", true) },
            };

            yield return new ProcessType("process.pump.displacement", T("Positive displacement pump", "Verdrängerpumpe"), Cat, 12, 12, Displacement,
                "pump", "positive displacement", "gear pump", "screw pump", "piston pump", "reciprocating pump", "diaphragm pump", "metering pump", "dosing pump",
                "pumpe", "verdrängerpumpe", "zahnradpumpe", "schraubenspindelpumpe", "kolbenpumpe", "membranpumpe", "dosierpumpe")
            {
                Params = new[] { PdKind },
                Ports = Fixed(new Port("S", -6, 0), new Port("D", 6, 0)),
                Block = true, TagPrefix = "P-102", CompanionIds = Companions,
                Extra = o => Choice(o, "process.pump.kind", T("Pump type", "Pumpenbauart"), PdKind),
            };

            yield return new ProcessType("process.compressor", T("Compressor (process)", "Verdichter (Prozess)"), Cat, 14, 12, Compressor,
                "compressor", "turbo compressor", "centrifugal compressor", "reciprocating compressor", "blower",
                "verdichter", "kompressor", "turboverdichter", "kolbenverdichter", "gebläse")
            {
                Params = new[] { CompKind },
                Ports = Fixed(new Port("S", -7, 0), new Port("D", 7, 0)),
                Block = true, TagPrefix = "K-101", CompanionIds = Companions,
                Extra = o => Choice(o, "process.compressor.kind", T("Compressor type", "Verdichterbauart"), CompKind),
            };

            yield return new ProcessType("process.turbine", T("Turbine / expander", "Turbine / Entspannungsmaschine"), Cat, 14, 12, Turbine,
                "turbine", "steam turbine", "gas turbine", "expander", "turboexpander",
                "turbine", "dampfturbine", "gasturbine", "entspannungsturbine", "expander")
            {
                Ports = Fixed(new Port("I", -7, 0), new Port("O", 7, 0)),
                Block = true, TagPrefix = "TB-101", CompanionIds = new[] { "process.line", "process.motor", "process.hx.shelltube", "process.pump.centrifugal" },
            };

            yield return new ProcessType("process.fan", T("Fan", "Ventilator"), Cat, 12, 12, Fan,
                "fan", "blower", "ventilator", "centrifugal fan", "axial fan", "air handling", "hvac",
                "ventilator", "lüfter", "gebläse", "radialventilator", "axialventilator", "lüftung", "rlt")
            {
                Params = new[] { FanKind },
                Ports = o => o.Str("kind", "radial") == "axial"
                    ? new[] { new Port("I", -6, 0), new Port("O", 6, 0) }
                    : new[] { new Port("I", -6, 0.5), new Port("O", 6, -4.5) },
                Block = true, TagPrefix = "B-101",
                CompanionIds = new[] { "process.line", "process.hvac.filter", "process.hvac.coil", "process.hvac.damper", "process.motor" },
                Extra = o => Choice(o, "process.fan.kind", null, FanKind),
            };

            yield return new ProcessType("process.motor", T("Electric motor (drive)", "Elektromotor (Antrieb)"), Cat, 10, 10, Motor,
                "motor", "electric motor", "drive", "driver", "antrieb", "elektromotor", "motor", "e-motor")
            {
                Ports = Fixed(new Port("shaft", 5, 0)),
                Block = true, TagPrefix = "M-101", CompanionIds = new[] { "process.pump.centrifugal", "process.compressor", "process.fan" },
            };
        }

        private static void Centrifugal(Pen p, SketchObject o)
        {
            const double r = 4.6, cy = 0.6;
            p.Circle(0, cy, r, S.Sym, ProcessTags.Body);
            // Tangential discharge at the top, axial suction from the left.
            p.Line(0, cy - r, 6, cy - r, S.Sym, "discharge");
            p.Line(-6, cy, -r, cy, S.Sym, "suction");
            if (o.Flag("base", true))
            {
                var a = Geo.DegToRad(125);
                var b = Geo.DegToRad(55);
                p.Line(Math.Cos(a) * r, cy + Math.Sin(a) * r, -4.4, 6, S.Sym, "base");
                p.Line(Math.Cos(b) * r, cy + Math.Sin(b) * r, 4.4, 6, S.Sym, "base");
                p.Line(-5, 6, 5, 6, S.Sym, "base");
            }
        }

        private static void Displacement(Pen p, SketchObject o)
        {
            const double r = 5;
            p.Circle(0, 0, r, S.Sym, ProcessTags.Body);
            p.Line(-6, 0, -r, 0, S.Sym, "suction");
            p.Line(r, 0, 6, 0, S.Sym, "discharge");
            switch (o.Str("kind", "gear"))
            {
                case "screw":
                    {
                        var pts = new List<Vec2>();
                        for (var i = 0; i <= 24; i++)
                        {
                            var x = -3.4 + 6.8 * i / 24.0;
                            pts.Add(new Vec2(x, 1.4 * Math.Sin(i / 24.0 * 3 * 2 * Math.PI)));
                        }
                        p.PolyD(pts, S.Thin, false, "rotor");
                        p.Line(-3.8, 0, 3.8, 0, S.Fine, "rotor");
                        break;
                    }
                case "piston":
                    p.Rect(-2.8, -1.8, 0.4, 1.8, S.Thin, "piston");
                    p.Line(0.4, 0, 3.4, 0, S.Thin, "piston");
                    p.Arrow(-1.6, -3.0, 1.6, -3.0, S.Fine, "stroke", 0.9, 0.35);
                    p.Head(-1.6, -3.0, 1.6, -3.0, 0.9, 0.35, "stroke");
                    break;
                case "diaphragm":
                    p.Arc(-3.5, 0, 3.4, 3.6, -60, 120, S.Thin, "diaphragm");
                    p.Line(-3.5, 0, 1.4, 0, S.Fine, "diaphragm");
                    break;
                default:
                    p.Circle(0, -1.7, 1.7, S.Thin, "gear");
                    p.Circle(0, 1.7, 1.7, S.Thin, "gear");
                    break;
            }
        }

        private static void Compressor(Pen p, SketchObject o)
        {
            const double r = 5;
            p.Circle(0, 0, r, S.Sym, ProcessTags.Body);
            p.Line(-7, 0, -r, 0, S.Sym, "suction");
            p.Line(r, 0, 7, 0, S.Sym, "discharge");
            if (o.Str("kind", "dynamic") == "reciprocating")
            {
                p.Rect(-3, -2.2, 3, 2.2, S.Thin, "cylinder");
                p.Line(-0.6, -2.2, -0.6, 2.2, S.Bold, "piston");
                p.Line(-0.6, 0, -3, 0, S.Thin, "rod");
                return;
            }
            // Converging flow channel: wide at the suction, narrow at the discharge.
            var a = Geo.DegToRad(118);
            var b = Geo.DegToRad(25);
            p.Line(-Math.Cos(Math.PI - a) * r, -Math.Sin(a) * r, Math.Cos(b) * r, -Math.Sin(b) * r, S.Sym, "channel");
            p.Line(-Math.Cos(Math.PI - a) * r, Math.Sin(a) * r, Math.Cos(b) * r, Math.Sin(b) * r, S.Sym, "channel");
        }

        private static void Turbine(Pen p, SketchObject o)
        {
            const double r = 5;
            p.Circle(0, 0, r, S.Sym, ProcessTags.Body);
            p.Line(-7, 0, -r, 0, S.Sym, "inlet");
            p.Line(r, 0, 7, 0, S.Sym, "outlet");
            // Diverging flow channel: narrow at the inlet, wide at the outlet.
            var a = Geo.DegToRad(155);
            var b = Geo.DegToRad(62);
            p.Line(Math.Cos(a) * r, -Math.Sin(a) * r, Math.Cos(b) * r, -Math.Sin(b) * r, S.Sym, "channel");
            p.Line(Math.Cos(a) * r, Math.Sin(a) * r, Math.Cos(b) * r, Math.Sin(b) * r, S.Sym, "channel");
        }

        private static void Fan(Pen p, SketchObject o)
        {
            if (o.Str("kind", "radial") == "axial")
            {
                p.Circle(0, 0, 5, S.Sym, ProcessTags.Body);
                p.Line(-6, 0, -5, 0, S.Sym, "inlet");
                p.Line(5, 0, 6, 0, S.Sym, "outlet");
                // Impeller plane and the flow direction.
                p.Line(0, -5, 0, 5, S.Thin, "impeller");
                p.Poly(S.Sym, true, "flow", -2.6, -2.4, 2.8, 0, -2.6, 2.4);
                return;
            }
            const double r = 5, cy = 0.5;
            p.Circle(0, cy, r, S.Sym, ProcessTags.Body);
            p.Line(0, cy - r, 6, cy - r, S.Sym, "outlet");
            p.Line(6, cy - r, 6, cy - r + 2.2, S.Sym, "outlet");
            p.Line(-6, cy, -r, cy, S.Sym, "inlet");
            p.Circle(0, cy, 1.2, S.Thin, "impeller");
            // Backward-curved blades between the hub and the wheel rim.
            for (var i = 0; i < 8; i++)
            {
                var a = Geo.DegToRad(i * 45);
                var blade = new List<Vec2>();
                for (var k = 0; k <= 4; k++)
                {
                    var rr = 1.2 + 2.4 * k / 4.0;
                    var aa = a + 0.45 * Math.Sqrt(k / 4.0);
                    blade.Add(new Vec2(Math.Cos(aa) * rr, cy + Math.Sin(aa) * rr));
                }
                p.PolyD(blade, S.Fine, false, "impeller");
            }
        }

        private static void Motor(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 4.5, S.Sym, ProcessTags.Body);
            p.Line(4.5, 0, 5, 0, S.Bold, "shaft");
            p.Text(0, 0, "M", 4.2, TextAnchor.Middle, "symbol");
        }
    }
}
