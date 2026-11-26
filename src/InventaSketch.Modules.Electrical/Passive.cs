using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Passive components: resistor, potentiometer, capacitors, inductor, transformer (drawn independently in IEC 60617 style).</summary>
    internal static class Passive
    {
        public const string Cat = "elec.passive";

        private static readonly ParamDef Core = Choice("core", T("Core", "Kern"), "none",
            ("none", "Air (none)", "Luft (ohne)"), ("iron", "Iron core", "Eisenkern"), ("ferrite", "Ferrite core", "Ferritkern"));

        private static readonly ParamDef Coefficient = Choice("coefficient", T("Temperature coefficient", "Temperaturkoeffizient"), "ntc",
            ("ntc", "NTC (resistance falls when hot)", "Heißleiter (NTC)"), ("ptc", "PTC (resistance rises when hot)", "Kaltleiter (PTC)"));

        private static readonly IReadOnlyList<Relation> CapacitorRelations =
            ElecValues.Capacitor.Concat(ElecValues.CapacitorDynamics).Concat(AcValues.ReactiveRelations).ToList();
        private static readonly IReadOnlyList<ValueCheck> CapacitorChecks = new[] { ElecValues.NotReducible, AcValues.NearResonance };

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.resistor", T("Resistor", "Widerstand"), Cat,
                new[] { "resistor", "widerstand", "ohm", "r" },
                Views.Symbol(20, 8, ResistorIec), Views.Ansi(20, 8, ResistorAnsi))
            {
                Pins = Fixed(TwoH().ToArray()), RefPrefix = "R", Params = ElecValues.ResistorParams, Variables = ElecValues.ResistorVariables, Relations = ElecValues.Ohm,
                Net = ElecValues.Two("1", "2", ElecValues.Resistive), Checks = new[] { ElecValues.ResistorPower, ElecValues.NotReducible },
            };

            // Resistors with an inherent variability (drawn independently): the oblique stroke with a foot marks a
            // non-linear dependence on temperature (thermistor) or voltage (varistor); the arrows a light-dependent one.
            yield return new ElecType("elec.resistor.thermistor", T("Thermistor (NTC/PTC)", "Thermistor (Heißleiter/Kaltleiter)"), Cat,
                new[] { "thermistor", "ntc", "ptc", "heißleiter", "kaltleiter", "temperature", "temperatur", "temperaturabhängiger widerstand", "widerstand", "resistor" },
                Views.Symbol(20, 10, (p, o) => { ResistorIec(p, o); Variability(p, CoefficientMark(o)); }),
                Views.Ansi(20, 10, (p, o) => { ResistorAnsi(p, o); Variability(p, CoefficientMark(o)); }))
            {
                Pins = Fixed(TwoH().ToArray()), RefPrefix = "R", Params = new[] { Coefficient }.Concat(ElecValues.ResistorParams).ToList(),
                Variables = ElecValues.ResistorVariables, Relations = ElecValues.Ohm,
                Net = ElecValues.Two("1", "2", ElecValues.Resistive), Checks = new[] { ElecValues.ResistorPower, ElecValues.NotReducible },
                Extra = o => Choice(o, "elec.thermistor.kind", T("Temperature coefficient", "Temperaturkoeffizient"), Coefficient),
            };

            yield return new ElecType("elec.resistor.ldr", T("Photoresistor (LDR)", "Fotowiderstand (LDR)"), Cat,
                new[] { "ldr", "photoresistor", "light dependent resistor", "photocell", "fotowiderstand", "lichtabhängiger widerstand", "licht", "widerstand", "resistor" },
                Views.Symbol(20, 12, (p, o) => { ResistorIec(p, o); LightArrows(p); }),
                Views.Ansi(20, 12, (p, o) => { ResistorAnsi(p, o); LightArrows(p); }))
            {
                Pins = Fixed(TwoH().ToArray()), RefPrefix = "R", Params = ElecValues.ResistorParams,
                Variables = ElecValues.ResistorVariables, Relations = ElecValues.Ohm,
                Net = ElecValues.Two("1", "2", ElecValues.Resistive), Checks = new[] { ElecValues.ResistorPower, ElecValues.NotReducible },
            };

            // The varistor's value text is its rated voltage ("275 V"), not a resistance, and it does not conduct in normal
            // operation: no R variable, open in a circuit.
            yield return new ElecType("elec.resistor.varistor", T("Varistor (VDR)", "Varistor (VDR)"), Cat,
                new[] { "varistor", "vdr", "mov", "voltage dependent resistor", "surge", "overvoltage", "spannungsabhängiger widerstand", "überspannung", "überspannungsschutz", "widerstand" },
                Views.Symbol(20, 10, (p, o) => { ResistorIec(p, o); Variability(p, "U"); }),
                Views.Ansi(20, 10, (p, o) => { ResistorAnsi(p, o); Variability(p, "U"); }))
            { Pins = Fixed(TwoH().ToArray()), RefPrefix = "RV", Net = _ => NetSpec.Open("1", "2") };

            yield return new ElecType("elec.crystal", T("Quartz crystal", "Schwingquarz"), Cat,
                new[] { "crystal", "quartz", "xtal", "resonator", "oscillator", "clock", "quarz", "schwingquarz", "taktgeber", "takt" },
                Views.Symbol(20, 10, Crystal))
            // A crystal blocks direct current.
            { Pins = Fixed(TwoH().ToArray()), RefPrefix = "Y", Net = _ => NetSpec.Open("1", "2") };

            yield return new ElecType("elec.potentiometer", T("Potentiometer", "Potentiometer"), Cat,
                new[] { "potentiometer", "poti", "trimmer", "variable resistor", "einstellbarer widerstand", "schleifer", "wiper" },
                Views.Symbol(20, 10, (p, o) => Pot(p, false)), Views.Ansi(20, 10, (p, o) => Pot(p, true)))
            {
                Pins = Fixed(new Pin("1", -0.5, 0), new Pin("2", 0.5, 0), new Pin("wiper", 0, 0.5)), RefPrefix = "R",
                Params = ElecValues.PotentiometerParams, Variables = ElecValues.PotentiometerVariables, Relations = ElecValues.Potentiometer,
                // The track is a resistive branch; the wiper is a terminal of unknown current when something is wired to it, so
                // a loaded divider is not reduced (the unloaded divider relation stays on the element).
                Net = ElecValues.Two("1", "2", ElecValues.Resistive), Checks = ElecValues.ReducibleOnly,
            };

            yield return new ElecType("elec.capacitor", T("Capacitor", "Kondensator"), Cat,
                new[] { "capacitor", "kondensator", "kapazität", "c" },
                Views.Symbol(20, 10, Capacitor))
            {
                Pins = Fixed(TwoH().ToArray()), RefPrefix = "C", Params = ElecValues.CapacitorParams, Variables = ElecValues.CapacitorVariables,
                Relations = CapacitorRelations, Net = ElecValues.Two("1", "2", ElecValues.Capacitive), Checks = CapacitorChecks,
            };

            yield return new ElecType("elec.capacitor.polarized", T("Polarized capacitor", "Gepolter Kondensator"), Cat,
                new[] { "electrolytic", "polarized", "capacitor", "elko", "elektrolytkondensator", "gepolt", "kondensator" },
                Views.Symbol(20, 10, (p, o) => PolarCap(p, false)), Views.Ansi(20, 10, (p, o) => PolarCap(p, true)))
            {
                Pins = Fixed(TwoH("+", "-").ToArray()), Polarized = true, RefPrefix = "C", Params = ElecValues.CapacitorParams, Variables = ElecValues.CapacitorVariables,
                Relations = CapacitorRelations, Net = ElecValues.Two("+", "-", ElecValues.Capacitive), Checks = CapacitorChecks,
            };

            yield return new ElecType("elec.inductor", T("Inductor", "Spule"), Cat,
                new[] { "inductor", "coil", "choke", "spule", "induktivität", "drossel", "l" },
                Views.Symbol(20, 8, Inductor))
            {
                Pins = Fixed(TwoH().ToArray()), Params = new[] { Core }.Concat(ElecValues.InductorParams).ToList(), RefPrefix = "L",
                Variables = ElecValues.InductorVariables, Relations = ElecValues.Inductor.Concat(AcValues.ReactiveRelations).ToList(), Net = ElecValues.Two("1", "2", ElecValues.Inductive), Checks = ElecValues.ReducibleOnly,
            };

            yield return new ElecType("elec.transformer", T("Transformer", "Transformator"), Cat,
                new[] { "transformer", "transformator", "trafo", "übertrager", "wandler" },
                Views.Symbol(20, 20, TransformerCoils),
                new ViewSpec("circles", T("Circles (single-line)", "Kreise (einpolig)"), IconIds.Simplified, 20, 20, TransformerCircles))
            {
                Pins = Fixed(new Pin("p1", -0.5, -0.25), new Pin("p2", -0.5, 0.25), new Pin("s1", 0.5, -0.25), new Pin("s2", 0.5, 0.25)),
                Params = new[] { Flag("core", T("Iron core", "Eisenkern"), true) }.Concat(ElecValues.TransformerParams).ToList(),
                RefPrefix = "T",
                Variables = ElecValues.TransformerVariables, Relations = ElecValues.Transformer, Marking = ElecValues.TransformerMarking,
                // Primary: a load of the primary circuit; secondary: the source of the secondary circuit.
                Net = _ =>
                {
                    var n = new NetSpec();
                    n.Branches.Add(new BranchDef("p1", "p2") { U = "U1", I = "I1" });
                    n.Branches.Add(new BranchDef("s1", "s2") { U = "U2", I = "I2", Source = true });
                    return n;
                },
                Checks = ElecValues.ReducibleOnly,
            };
        }

        private static void ResistorIec(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -6, 0, null, "lead");
            p.Line(6, 0, 10, 0, null, "lead");
            p.Rect(-6, -2, 6, 2, null, "body");
        }

        private static void ResistorAnsi(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -6, 0, null, "lead");
            p.Line(6, 0, 10, 0, null, "lead");
            p.PolyD(Frag.Zigzag(-6, 6, 0, 2.5, 4), S.Sym, false, "body");
        }

        private static string CoefficientMark(SketchObject o) => o.Str("coefficient", "ntc") == "ptc" ? "+ϑ" : "−ϑ";

        /// <summary>Oblique stroke with a foot across the resistor body and the quantity it depends on (ϑ, U) top right.</summary>
        private static void Variability(Pen p, string mark)
        {
            p.Poly(S.Sym, false, "variability", -7.5, 3.6, -5, 3.6, 5, -3.6);
            p.Text(8.2, -3.2, mark, 2.4, TextAnchor.Middle, false, "quantity");
        }

        /// <summary>Two light rays falling onto the body (photoresistor).</summary>
        private static void LightArrows(Pen p)
        {
            p.Arrow(-5, -5.7, -2.4, -2.9, 1.2, 0.5, null, "ray");
            p.Arrow(-1.8, -5.7, 0.8, -2.9, 1.2, 0.5, null, "ray");
        }

        private static void Crystal(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -3, 0, null, "lead");
            p.Line(3, 0, 10, 0, null, "lead");
            p.Line(-3, -3.5, -3, 3.5, S.Sym, "plate");
            p.Line(3, -3.5, 3, 3.5, S.Sym, "plate");
            p.Rect(-1.7, -4.2, 1.7, 4.2, null, "body");
        }

        private static void Pot(Pen p, bool ansi)
        {
            p.Line(-10, 0, -6, 0, null, "lead");
            p.Line(6, 0, 10, 0, null, "lead");
            if (ansi) p.PolyD(Frag.Zigzag(-6, 6, 0, 2.5, 4), S.Sym, false, "body");
            else p.Rect(-6, -2, 6, 2, null, "body");
            // Wiper: arrow from the wiper terminal onto the resistive element.
            p.Arrow(0, 5, 0, ansi ? 2.6 : 2.1, 1.6, 0.75, null, "wiper");
        }

        private static void Capacitor(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -1, 0, null, "lead");
            p.Line(1, 0, 10, 0, null, "lead");
            p.Line(-1, -4, -1, 4, S.Bold, "plate");
            p.Line(1, -4, 1, 4, S.Bold, "plate");
        }

        private static void PolarCap(Pen p, bool ansi)
        {
            p.Line(-10, 0, -1, 0, null, "lead");
            p.Line(-1, -4, -1, 4, S.Bold, "plate");
            if (ansi)
            {
                // Curved negative plate.
                p.Arc(7, 0, 6, 6, 140, 80, S.Bold, "plate");
                p.Line(1, 0, 10, 0, null, "lead");
            }
            else
            {
                p.Rect(0.8, -4, 2, 4, S.Black, "plate");
                p.Line(2, 0, 10, 0, null, "lead");
            }
            p.Text(-4, -3, "+", 3, TextAnchor.Middle, false, "polarity");
        }

        private static void Inductor(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -6, 0, null, "lead");
            p.Line(6, 0, 10, 0, null, "lead");
            p.PolyD(Frag.HumpsH(-6, 6, 0, 4), S.Sym, false, "coil");
            switch (o.Str("core", "none"))
            {
                case "iron": p.Line(-6, -2.8, 6, -2.8, S.Sym, "core"); break;
                case "ferrite": p.Line(-6, -2.8, 6, -2.8, S.Link with { StrokeWidth = 0.35 }, "core"); break;
            }
        }

        private static void TransformerCoils(Pen p, SketchObject o)
        {
            p.Line(-10, -5, -4, -5, null, "lead");
            p.Line(-10, 5, -4, 5, null, "lead");
            p.Line(4, -5, 10, -5, null, "lead");
            p.Line(4, 5, 10, 5, null, "lead");
            p.PolyD(Frag.HumpsV(-4, -5, 5, 4, 1), S.Sym, false, "coil");
            p.PolyD(Frag.HumpsV(4, -5, 5, 4, -1), S.Sym, false, "coil");
            if (o.Flag("core", true))
            {
                p.Line(-0.6, -6.5, -0.6, 6.5, S.Sym, "core");
                p.Line(0.6, -6.5, 0.6, 6.5, S.Sym, "core");
            }
        }

        private static void TransformerCircles(Pen p, SketchObject o)
        {
            p.Circle(-3, 0, 5, null, "winding");
            p.Circle(3, 0, 5, null, "winding");
            p.Poly(S.Sym, false, "lead", -10, -5, -9, -5, -9, 5, -10, 5);
            p.Line(-9, 0, -8, 0, null, "lead");
            p.Poly(S.Sym, false, "lead", 10, -5, 9, -5, 9, 5, 10, 5);
            p.Line(9, 0, 8, 0, null, "lead");
        }
    }
}
