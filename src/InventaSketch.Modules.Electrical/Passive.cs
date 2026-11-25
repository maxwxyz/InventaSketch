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

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.resistor", T("Resistor", "Widerstand"), Cat,
                new[] { "resistor", "widerstand", "ohm", "r" },
                Views.Symbol(20, 8, ResistorIec), Views.Ansi(20, 8, ResistorAnsi))
            { Pins = Fixed(TwoH().ToArray()), RefPrefix = "R", Params = ElecValues.ResistorParams, Variables = ElecValues.ResistorVariables, Relations = ElecValues.Ohm };

            yield return new ElecType("elec.potentiometer", T("Potentiometer", "Potentiometer"), Cat,
                new[] { "potentiometer", "poti", "trimmer", "variable resistor", "einstellbarer widerstand", "schleifer", "wiper" },
                Views.Symbol(20, 10, (p, o) => Pot(p, false)), Views.Ansi(20, 10, (p, o) => Pot(p, true)))
            { Pins = Fixed(new Pin("1", -0.5, 0), new Pin("2", 0.5, 0), new Pin("wiper", 0, 0.5)), RefPrefix = "R" };

            yield return new ElecType("elec.capacitor", T("Capacitor", "Kondensator"), Cat,
                new[] { "capacitor", "kondensator", "kapazität", "c" },
                Views.Symbol(20, 10, Capacitor))
            { Pins = Fixed(TwoH().ToArray()), RefPrefix = "C" };

            yield return new ElecType("elec.capacitor.polarized", T("Polarized capacitor", "Gepolter Kondensator"), Cat,
                new[] { "electrolytic", "polarized", "capacitor", "elko", "elektrolytkondensator", "gepolt", "kondensator" },
                Views.Symbol(20, 10, (p, o) => PolarCap(p, false)), Views.Ansi(20, 10, (p, o) => PolarCap(p, true)))
            { Pins = Fixed(TwoH("+", "-").ToArray()), Polarized = true, RefPrefix = "C" };

            yield return new ElecType("elec.inductor", T("Inductor", "Spule"), Cat,
                new[] { "inductor", "coil", "choke", "spule", "induktivität", "drossel", "l" },
                Views.Symbol(20, 8, Inductor))
            { Pins = Fixed(TwoH().ToArray()), Params = new[] { Core }, RefPrefix = "L" };

            yield return new ElecType("elec.transformer", T("Transformer", "Transformator"), Cat,
                new[] { "transformer", "transformator", "trafo", "übertrager", "wandler" },
                Views.Symbol(20, 20, TransformerCoils),
                new ViewSpec("circles", T("Circles (single-line)", "Kreise (einpolig)"), IconIds.Simplified, 20, 20, TransformerCircles))
            {
                Pins = Fixed(new Pin("p1", -0.5, -0.25), new Pin("p2", -0.5, 0.25), new Pin("s1", 0.5, -0.25), new Pin("s2", 0.5, 0.25)),
                Params = new[] { Flag("core", T("Iron core", "Eisenkern"), true) },
                RefPrefix = "T",
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
