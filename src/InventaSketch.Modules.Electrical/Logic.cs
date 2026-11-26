using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Logic gates (IEC rectangular and ANSI distinctive shapes), flip-flops, op-amp and control-unit blocks.</summary>
    internal static class Logic
    {
        public const string Cat = "elec.logic";

        public static readonly string[] GateKinds = { "and", "or", "not", "nand", "nor", "xor", "xnor", "buffer" };

        private static readonly ParamDef Gate = Choice("gate", T("Function", "Funktion"), "and",
            ("and", "AND", "UND"), ("or", "OR", "ODER"), ("not", "NOT", "NICHT"), ("nand", "NAND", "NAND"), ("nor", "NOR", "NOR"),
            ("xor", "XOR", "XOR (Antivalenz)"), ("xnor", "XNOR", "XNOR (Äquivalenz)"), ("buffer", "Buffer", "Puffer"));
        private static readonly ParamDef Inputs = Int("inputs", T("Inputs", "Eingänge"), 2, 2, 4);
        private static readonly ParamDef FlipFlop = Choice("kind", T("Type", "Typ"), "d",
            ("d", "D flip-flop", "D-Flipflop"), ("jk", "JK flip-flop", "JK-Flipflop"), ("sr", "SR latch", "RS-Flipflop"), ("t", "T flip-flop", "T-Flipflop"));
        private static readonly ParamDef PinsLeft = Int("pinsLeft", T("Pins left", "Pins links"), 3, 0, 16);
        private static readonly ParamDef PinsRight = Int("pinsRight", T("Pins right", "Pins rechts"), 3, 0, 16);
        private static readonly ParamDef Label = Text("label", T("Label", "Beschriftung"), "ECU");

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.gate", T("Logic gate", "Logikgatter"), Cat,
                new[] { "gate", "logic", "and", "or", "not", "nand", "nor", "xor", "xnor", "inverter", "buffer", "gatter", "und", "oder", "nicht", "logik", "verknüpfung" },
                Views.Symbol(20, 20, GateIec), Views.Ansi(20, 20, GateAnsi))
            {
                Pins = GatePins, Params = new[] { Gate, Inputs }, RefPrefix = "U",
                Extra = o => Choice(o, "elec.gate", T("Gate function", "Gatterfunktion"), Gate),
            };

            yield return new ElecType("elec.flipflop", T("Flip-flop", "Flipflop"), Cat,
                new[] { "flip-flop", "flipflop", "latch", "d flip-flop", "jk", "sr", "register", "speicher", "kippglied" },
                Views.Symbol(20, 30, FlipFlopDraw))
            {
                Pins = FlipFlopPins, Params = new[] { FlipFlop }, RefPrefix = "U",
                Extra = o => Choice(o, "elec.flipflop", T("Flip-flop type", "Flipflop-Typ"), FlipFlop),
            };

            yield return new ElecType("elec.opamp", T("Operational amplifier", "Operationsverstärker"), Cat,
                new[] { "op-amp", "opamp", "operational amplifier", "amplifier", "operationsverstärker", "opv", "verstärker", "comparator", "komparator" },
                Views.Symbol(30, 20, (p, o) => OpAmp(p, false)), Views.Ansi(30, 20, (p, o) => OpAmp(p, true)))
            { Pins = Fixed(new Pin("-", -0.5, -0.25), new Pin("+", -0.5, 0.25), new Pin("out", 0.5, 0)), RefPrefix = "U" };

            // Three-terminal voltage regulator (78xx/LM317 style block). In a circuit it is a multi-pin part (terminal of
            // unknown current); its own relation links the input/output voltages, the output current and the power loss.
            yield return new ElecType("elec.regulator", T("Voltage regulator", "Spannungsregler"), Cat,
                new[] { "voltage regulator", "regulator", "ldo", "linear regulator", "78xx", "7805", "lm317", "spannungsregler", "festspannungsregler", "linearregler", "spannungsstabilisierung" },
                Views.Symbol(20, 15, (p, o) => Regulator(p)))
            {
                Pins = Fixed(new Pin("IN", -0.5, 0), new Pin("OUT", 0.5, 0), new Pin("GND", 0, 0.5)), RefPrefix = "U",
                Params = ElecValues.RegulatorParams, Variables = ElecValues.RegulatorVariables, Relations = ElecValues.Regulator,
                // A voltage in the value text ("5 V", "LM7805 5 V") is the output voltage; a part number alone gives nothing.
                Marking = ElecValues.ByKind((Quantities.Voltage, "Uout")),
            };

            yield return new ElecType("elec.block.ecu",T("Control unit (ECU) block", "Steuergerät (ECU) Block"), Cat,
                new[] { "ecu", "control unit", "steuergerät", "controller", "ic", "chip", "microcontroller", "mikrocontroller", "block", "module", "baustein" },
                Views.Block(30, 20, Ecu))
            {
                Pins = EcuPins, Params = new[] { Label, PinsLeft, PinsRight }, Uniform = false, RefPrefix = "A",
            };
        }

        private static void Regulator(Pen p)
        {
            p.Line(-10, 0, -6, 0, null, "lead");
            p.Line(6, 0, 10, 0, null, "lead");
            p.Line(0, 4.5, 0, 7.5, null, "lead");
            p.Rect(-6, -4.5, 6, 4.5, null, "body");
            p.Text(-5.2, -1.6, "IN", 2, TextAnchor.Start, false, "pin");
            p.Text(5.2, -1.6, "OUT", 2, TextAnchor.End, false, "pin");
            p.Text(0, 2.9, "GND", 2, TextAnchor.Middle, false, "pin");
        }

        // ---------------------------------------------------------------- gates

        private static string Kind(SketchObject o) => GateKinds.Contains(o.Str("gate", "and")) ? o.Str("gate", "and") : "and";
        private static bool Single(string kind) => kind is "not" or "buffer";
        private static bool Negated(string kind) => kind is "not" or "nand" or "nor" or "xnor";
        private static int InputCount(SketchObject o) => Single(Kind(o)) ? 1 : Count(o, "inputs", 2, 2, 4);

        /// <summary>Input y positions in design mm (grid-friendly for a 20 mm box: 2 inputs at ±5, 3 at -5/0/5).</summary>
        private static double[] InputYs(int n) => n switch
        {
            1 => new[] { 0.0 },
            2 => new[] { -5.0, 5.0 },
            3 => new[] { -5.0, 0, 5.0 },
            _ => new[] { -7.5, -2.5, 2.5, 7.5 },
        };

        private static IReadOnlyList<Pin> GatePins(SketchObject o)
        {
            var ys = InputYs(InputCount(o));
            var pins = ys.Select((y, i) => new Pin("in" + N(i + 1), -0.5, y / 20)).ToList();
            pins.Add(new Pin("out", 0.5, 0));
            return pins;
        }

        private static void GateIec(Pen p, SketchObject o)
        {
            var kind = Kind(o);
            var ys = InputYs(InputCount(o));
            var h = ys.Length == 4 ? 9.5 : 8;
            p.Rect(-6, -h, 6, h, null, "body");
            foreach (var y in ys) p.Line(-10, y, -6, y, null, "lead");
            var mark = kind switch { "and" or "nand" => "&", "or" or "nor" => "≥1", "xor" => "=1", "xnor" => "=", _ => "1" };
            p.Text(0, -h + 3, mark, 4, TextAnchor.Middle, false, "function");
            OutputLead(p, 6, Negated(kind));
        }

        private static void OutputLead(Pen p, double x, bool negated)
        {
            if (negated)
            {
                p.Circle(x + 1.2, 0, 1.2, null, "negation");
                x += 2.4;
            }
            p.Line(x, 0, 10, 0, null, "lead");
        }

        private static void GateAnsi(Pen p, SketchObject o)
        {
            var kind = Kind(o);
            var ys = InputYs(InputCount(o));
            var h = ys.Length == 4 ? 9.5 : 7.5;
            double front;
            Func<double, double> back = _ => -6;
            switch (kind)
            {
                case "and":
                case "nand":
                    {
                        var segs = new List<PathSeg> { PathSeg.Move(new Vec2(-6, -h)), PathSeg.Line(new Vec2(-1, -h)) };
                        segs.AddRange(Geo.ArcPoints(new Vec2(-1, 0), 8, h, -Math.PI / 2, Math.PI, 24).Skip(1).Select(PathSeg.Line));
                        segs.Add(PathSeg.Line(new Vec2(-6, h)));
                        segs.Add(PathSeg.Close);
                        p.Path(segs, null, "body");
                        front = 7;
                        break;
                    }
                case "not":
                case "buffer":
                    p.Poly(S.Sym, true, "body", -6, -6, -6, 6, 6, 0);
                    front = 6;
                    break;
                default:
                    {
                        // OR family: concave back, pointed front.
                        var b0 = new Vec2(-6, -h); var b1 = new Vec2(-6, h); var c = new Vec2(-2.5, 0);
                        p.Path(new[]
                        {
                            PathSeg.Move(b0),
                            PathSeg.Cubic(new Vec2(0, -h), new Vec2(5, -h * 0.55), new Vec2(7, 0)),
                            PathSeg.Cubic(new Vec2(5, h * 0.55), new Vec2(0, h), b1),
                            PathSeg.Quad(c, b0),
                        }, null, "body");
                        front = 7;
                        back = y => QuadX(b0, c, b1, y, h);
                        if (kind is "xor" or "xnor")
                        {
                            var e0 = new Vec2(-8.2, -h); var e1 = new Vec2(-8.2, h); var ec = new Vec2(-4.7, 0);
                            p.Path(new[] { PathSeg.Move(e0), PathSeg.Quad(ec, e1) }, null, "body");
                            back = y => QuadX(e0, ec, e1, y, h);
                        }
                        break;
                    }
            }
            foreach (var y in ys) p.Line(-10, y, back(y), y, null, "lead");
            OutputLead(p, front, Negated(kind));
        }

        /// <summary>x of a symmetric quadratic back curve (ends at y = ±h) at height y.</summary>
        private static double QuadX(Vec2 a, Vec2 c, Vec2 b, double y, double h)
        {
            var t = Geo.Clamp((y / h + 1) / 2, 0, 1);
            return Geo.QuadAt(a, c, b, t).X;
        }

        // ---------------------------------------------------------------- flip-flops

        private static (string Label, double Y, bool Clock)[] FlipFlopInputs(SketchObject o) => o.Str("kind", "d") switch
        {
            "jk" => new[] { ("J", -10.0, false), ("", 0.0, true), ("K", 10.0, false) },
            "sr" => new[] { ("S", -10.0, false), ("R", 10.0, false) },
            "t" => new[] { ("T", -10.0, false), ("", 0.0, true) },
            _ => new[] { ("D", -10.0, false), ("", 0.0, true) },
        };

        private static IReadOnlyList<Pin> FlipFlopPins(SketchObject o)
        {
            var pins = FlipFlopInputs(o).Select(i => new Pin(i.Clock ? "C" : i.Label, -0.5, i.Y / 30)).ToList();
            pins.Add(new Pin("Q", 0.5, -10.0 / 30));
            pins.Add(new Pin("Qn", 0.5, 10.0 / 30));
            return pins;
        }

        private static void FlipFlopDraw(Pen p, SketchObject o)
        {
            p.Rect(-6, -13.5, 6, 13.5, null, "body");
            foreach (var (label, y, clock) in FlipFlopInputs(o))
            {
                p.Line(-10, y, -6, y, null, "lead");
                if (clock) p.Poly(S.Sym, false, "clock", -6, y - 1.5, -4, y, -6, y + 1.5);
                else p.Text(-5, y, label, 3, TextAnchor.Start, false, "pinlabel");
            }
            p.Line(6, -10, 10, -10, null, "lead");
            p.Line(6, 10, 10, 10, null, "lead");
            p.Text(5, -10, "Q", 3, TextAnchor.End, false, "pinlabel");
            p.Text(5, 10, "Q", 3, TextAnchor.End, false, "pinlabel");
            p.Line(3.3, 8.2, 4.9, 8.2, S.Thin, "overbar");
        }

        // ---------------------------------------------------------------- op-amp

        private static void OpAmp(Pen p, bool ansi)
        {
            p.Line(-15, -5, -10, -5, null, "lead");
            p.Line(-15, 5, -10, 5, null, "lead");
            p.Line(10, 0, 15, 0, null, "lead");
            if (ansi) p.Poly(S.Sym, true, "body", -10, -9.5, -10, 9.5, 10, 0);
            else
            {
                p.Rect(-10, -9.5, 10, 9.5, null, "body");
                p.Poly(S.Sym, true, "function", 1, -7.5, 1, -3.5, 4.5, -5.5);
                p.Text(7.5, -5.5, "∞", 3.5, TextAnchor.Middle, false, "function");
            }
            p.Text(-8, -5, "−", 3.5, TextAnchor.Middle, false, "pinlabel");
            p.Text(-8, 5, "+", 3.5, TextAnchor.Middle, false, "pinlabel");
        }

        // ---------------------------------------------------------------- ECU / IC block

        /// <summary>Pin v positions spread evenly: (i + 1) / (n + 1) - 0.5 (n = 3 in a 20 mm box gives -5/0/5 mm).</summary>
        private static double Spread(int i, int n) => (i + 1.0) / (n + 1) - 0.5;

        private static IReadOnlyList<Pin> EcuPins(SketchObject o)
        {
            int l = Count(o, "pinsLeft", 3, 0, 16), r = Count(o, "pinsRight", 3, 0, 16);
            var pins = new List<Pin>();
            for (var i = 0; i < l; i++) pins.Add(new Pin(N(i + 1), -0.5, Spread(i, l)));
            for (var i = 0; i < r; i++) pins.Add(new Pin(N(l + i + 1), 0.5, Spread(i, r)));
            return pins;
        }

        private static void Ecu(Pen p, SketchObject o)
        {
            int l = Count(o, "pinsLeft", 3, 0, 16), r = Count(o, "pinsRight", 3, 0, 16);
            p.Rect(-12, -9, 12, 9, null, "body");
            var numSize = Math.Min(2.2, 20.0 / (Math.Max(l, r) + 1) * 0.6);
            for (var i = 0; i < l; i++)
            {
                var y = Spread(i, l) * 20;
                p.Line(-15, y, -12, y, null, "lead");
                p.Text(-11.3, y, N(i + 1), numSize, TextAnchor.Start, false, "pinnumber");
            }
            for (var i = 0; i < r; i++)
            {
                var y = Spread(i, r) * 20;
                p.Line(12, y, 15, y, null, "lead");
                p.Text(11.3, y, N(l + i + 1), numSize, TextAnchor.End, false, "pinnumber");
            }
            var label = !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("label", "ECU");
            if (label.Length > 0)
                p.Text(0, 0, label, Math.Min(3.5, 16.0 / (label.Length * 0.55)), TextAnchor.Middle, true, "name");
        }
    }
}
