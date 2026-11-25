using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Earth symbols, sources, batteries, loads, machines, sensors, actuators and transducers.</summary>
    internal static class Sources
    {
        public const string Cat = "elec.sources";

        private static readonly ParamDef GroundKind = Choice("kind", T("Kind", "Art"), "earth",
            ("earth", "Earth / ground", "Erde / Masse"), ("pe", "Protective earth (PE)", "Schutzerde (PE)"), ("chassis", "Chassis / frame", "Gehäuse / Masse (Rahmen)"));
        private static readonly ParamDef SourceKind = Choice("kind", T("Kind", "Art"), "dc",
            ("dc", "DC voltage source", "Gleichspannungsquelle"), ("ac", "AC voltage source", "Wechselspannungsquelle"), ("current", "Current source", "Stromquelle"));
        private static readonly ParamDef Cells = Int("cells", T("Cells", "Zellen"), 1, 1, 4);
        private static readonly ParamDef Supply = Choice("supply", T("Supply", "Stromart"), "none",
            ("none", "Unspecified", "Ohne Angabe"), ("dc", "DC (=)", "Gleichstrom (=)"), ("ac", "AC (~)", "Wechselstrom (~)"), ("ac3", "Three-phase (3~)", "Drehstrom (3~)"));
        private static readonly ParamDef Label = Text("label", T("Label", "Beschriftung"), "");

        /// <summary>A source's value text as a marking: "5 V" (voltage source, battery), "10 mA" (current source), "12 V 2 A".</summary>
        private static readonly Func<SketchObject, IEnumerable<MarkingValue>> SourceMarking =
            ElecValues.ByKind((Quantities.Voltage, "U"), (Quantities.Current, "I"), (Quantities.Power, "P"));

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.ground", T("Ground / earth", "Erde / Masse"), Cat,
                new[] { "ground", "earth", "gnd", "pe", "protective earth", "chassis", "frame", "erde", "masse", "schutzleiter", "schutzerde", "gehäuse" },
                Views.Symbol(10, 10, Ground))
            {
                Pins = Fixed(new Pin("1", 0, -0.5)), Params = new[] { GroundKind }, Labeled = false,
                // Every earth symbol of one kind on a page is the same node (schematic convention).
                Net = o => new NetSpec { Global = "earth:" + o.Str("kind", "earth") },
                Extra = o => Choice(o, "elec.ground", T("Earth symbol", "Erdungszeichen"), GroundKind),
            };

            yield return new ElecType("elec.source", T("Voltage/current source", "Spannungs-/Stromquelle"), Cat,
                new[] { "source", "voltage source", "current source", "dc", "ac", "supply", "quelle", "spannungsquelle", "stromquelle", "gleichspannung", "wechselspannung", "netz" },
                Views.Symbol(20, 10, Source))
            {
                Pins = Fixed(TwoH("+", "-").ToArray()), Params = new[] { SourceKind }.Concat(ElecValues.SourceParams).ToList(), Polarized = true, RefPrefix = "G",
                Variables = ElecValues.SourceVariables, Relations = ElecValues.PowerRelations, Marking = SourceMarking,
                Net = _ => NetSpec.Branch(ElecValues.SourceBranch), Checks = ElecValues.ReducibleOnly,
                Extra = o => Choice(o, "elec.source", T("Source type", "Quellenart"), SourceKind),
            };

            yield return new ElecType("elec.battery", T("Battery", "Batterie"), Cat,
                new[] { "battery", "cell", "accumulator", "pack", "batterie", "zelle", "akku", "akkumulator", "hv battery" },
                Views.Symbol(20, 10, Battery))
            {
                Pins = Fixed(TwoH("+", "-").ToArray()), Params = new[] { Cells }.Concat(ElecValues.SourceParams).ToList(), Polarized = true, RefPrefix = "G",
                Variables = ElecValues.SourceVariables, Relations = ElecValues.PowerRelations, Marking = SourceMarking,
                Net = _ => NetSpec.Branch(ElecValues.SourceBranch), Checks = ElecValues.ReducibleOnly,
            };

            yield return new ElecType("elec.lamp", T("Lamp", "Lampe"), Cat,
                new[] { "lamp", "light", "bulb", "indicator", "lampe", "leuchte", "glühlampe", "meldeleuchte" },
                Views.Symbol(20, 10, (p, o) =>
                {
                    p.Line(-10, 0, -4.5, 0, null, "lead");
                    p.Line(4.5, 0, 10, 0, null, "lead");
                    p.Circle(0, 0, 4.5, null, "body");
                    var d = 4.5 * Math.Sqrt(0.5);
                    p.Line(-d, -d, d, d, null, "cross");
                    p.Line(-d, d, d, -d, null, "cross");
                }))
            {
                Pins = Fixed(TwoH().ToArray()), RefPrefix = "E", Params = ElecValues.LampParams, Variables = ElecValues.LampVariables, Relations = ElecValues.Ohm,
                Marking = ElecValues.ByKind((Quantities.Voltage, "U"), (Quantities.Current, "I"), (Quantities.Power, "P"), (Quantities.Resistance, "R")),
                Net = ElecValues.Two("1", "2", ElecValues.Resistive), Checks = ElecValues.ReducibleOnly,
            };

            yield return Machine("elec.motor", T("Motor", "Motor"), "M", new[] { "motor", "drive", "antrieb", "elektromotor", "m", "dc motor", "ac motor", "drehstrommotor" });
            yield return Machine("elec.generator", T("Generator", "Generator"), "G", new[] { "generator", "alternator", "lichtmaschine", "g", "dynamo" });

            yield return new ElecType("elec.sensor", T("Sensor", "Sensor"), Cat,
                new[] { "sensor", "transducer", "measurement", "probe", "messfühler", "messwertgeber", "geber", "fühler", "aufnehmer" },
                Views.Symbol(20, 10, Sensor))
            { Pins = Fixed(new Pin("out", 0.5, 0), new Pin("gnd", 0, 0.5)), Params = new[] { Label }, RefPrefix = "B" };

            yield return new ElecType("elec.actuator", T("Actuator", "Aktor"), Cat,
                new[] { "actuator", "solenoid", "valve", "aktor", "aktuator", "stellglied", "magnet", "hubmagnet", "ventil" },
                Views.Symbol(20, 10, Actuator))
            { Pins = Fixed(new Pin("in", -0.5, 0), new Pin("gnd", 0, 0.5)), Params = new[] { Label }, RefPrefix = "Y" };

            yield return new ElecType("elec.speaker", T("Loudspeaker", "Lautsprecher"), Cat,
                new[] { "speaker", "loudspeaker", "buzzer", "lautsprecher", "summer", "audio" },
                Views.Symbol(20, 20, (p, o) =>
                {
                    TwoLeads(p, -5);
                    p.Rect(-5, -3, -1, 3, null, "body");
                    p.Poly(S.Sym, true, "cone", -1, -3, 5, -8, 5, 8, -1, 3);
                }))
            { Pins = Fixed(new Pin("1", -0.5, -0.25), new Pin("2", -0.5, 0.25)), RefPrefix = "LS" };

            yield return new ElecType("elec.microphone", T("Microphone", "Mikrofon"), Cat,
                new[] { "microphone", "mic", "mikrofon", "audio" },
                Views.Symbol(20, 20, (p, o) =>
                {
                    TwoLeads(p, -3);
                    p.Circle(2, 0, 5, null, "body");
                    p.Line(-3, -5, -3, 5, S.Bold, "membrane");
                }))
            { Pins = Fixed(new Pin("1", -0.5, -0.25), new Pin("2", -0.5, 0.25)), RefPrefix = "MK" };

            yield return new ElecType("elec.antenna", T("Antenna", "Antenne"), Cat,
                new[] { "antenna", "aerial", "antenne", "radio", "funk", "wireless" },
                Views.Symbol(10, 20, (p, o) =>
                {
                    p.Line(0, 10, 0, -9.5, null, "mast");
                    p.Line(0, -2, -4.5, -9.5, null, "element");
                    p.Line(0, -2, 4.5, -9.5, null, "element");
                }))
            { Pins = Fixed(new Pin("1", 0, 0.5)), RefPrefix = "AE", LabelSide = LabelSide.Right };
        }

        /// <summary>Two leads from the left edge (y = ±5) bent onto a body edge at x = xBody (y = ±2).</summary>
        private static void TwoLeads(Pen p, double xBody)
        {
            p.Poly(S.Sym, false, "lead", -10, -5, -7, -5, -7, -2, xBody, -2);
            p.Poly(S.Sym, false, "lead", -10, 5, -7, 5, -7, 2, xBody, 2);
        }

        private static ElecType Machine(string id, I18nText name, string letter, string[] keywords)
            => new(id, name, Cat, keywords, Views.Symbol(20, 15, (p, o) => MachineDraw(p, o, letter)))
            {
                Pins = MachinePins, Params = new[] { Supply }, RefPrefix = letter,
                // Machine values (P, n, T) belong to the Machines module; in a circuit the symbol only keeps the network reducible.
                Net = o => o.Str("supply", "none") == "ac3" ? new NetSpec() : NetSpec.Branch(ElecValues.Opaque),
                Extra = o => Choice(o, id + ".supply", T("Supply", "Stromart"), Supply),
            };

        private static IReadOnlyList<Pin> MachinePins(SketchObject o) => o.Str("supply", "none") == "ac3"
            ? new[] { new Pin("U", -0.25, -0.5), new Pin("V", 0, -0.5), new Pin("W", 0.25, -0.5) }
            : TwoH();

        private static void MachineDraw(Pen p, SketchObject o, string letter)
        {
            const double r = 6.5;
            p.Circle(0, 0, r, null, "body");
            var supply = o.Str("supply", "none");
            if (supply == "ac3")
                foreach (var x in new[] { -5.0, 0, 5.0 }) p.Line(x, -7.5, x, -Math.Sqrt(r * r - x * x), null, "lead");
            else
            {
                p.Line(-10, 0, -r, 0, null, "lead");
                p.Line(r, 0, 10, 0, null, "lead");
            }
            var mark = supply switch { "dc" => "=", "ac" => "~", "ac3" => "3~", _ => "" };
            if (mark.Length == 0) p.Text(0, 0, letter, 5, TextAnchor.Middle, false, "letter");
            else
            {
                p.Text(0, -1.6, letter, 4.5, TextAnchor.Middle, false, "letter");
                p.Text(0, 3, mark, 3, TextAnchor.Middle, false, "supply");
            }
        }

        private static void Ground(Pen p, SketchObject o)
        {
            switch (o.Str("kind", "earth"))
            {
                case "pe":
                    p.Line(0, -5, 0, -0.6, null, "lead");
                    p.Circle(0, 0.6, 4.2, null, "pe");
                    p.Line(-2.8, -0.6, 2.8, -0.6, null, "earth");
                    p.Line(-1.8, 0.8, 1.8, 0.8, null, "earth");
                    p.Line(-0.8, 2.2, 0.8, 2.2, null, "earth");
                    break;
                case "chassis":
                    p.Line(0, -5, 0, 0, null, "lead");
                    p.Line(-4, 0, 4, 0, null, "earth");
                    foreach (var x in new[] { -2.0, 0, 2.0, 4.0 }) p.Line(x, 0, x - 2, 2.8, null, "earth");
                    break;
                default:
                    p.Line(0, -5, 0, -1, null, "lead");
                    p.Line(-4, -1, 4, -1, null, "earth");
                    p.Line(-2.6, 0.6, 2.6, 0.6, null, "earth");
                    p.Line(-1.2, 2.2, 1.2, 2.2, null, "earth");
                    break;
            }
        }

        private static void Source(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -4.5, 0, null, "lead");
            p.Line(4.5, 0, 10, 0, null, "lead");
            p.Circle(0, 0, 4.5, null, "body");
            switch (o.Str("kind", "dc"))
            {
                case "ac":
                    p.PolyD(Frag.Sine(0, 0, 2.8, 1.6), S.Sym, false, "sine");
                    break;
                case "current":
                    p.Line(0, -4.5, 0, 4.5, null, "inner");
                    p.Text(-6.5, -3, "+", 3, TextAnchor.Middle, false, "polarity");
                    break;
                default:
                    p.Line(-4.5, 0, 4.5, 0, null, "inner");
                    p.Text(-6.5, -3, "+", 3, TextAnchor.Middle, false, "polarity");
                    break;
            }
        }

        private static void Battery(Pen p, SketchObject o)
        {
            var n = Count(o, "cells", 1, 1, 4);
            var x0 = -(n * 3 - 1.5) / 2;
            p.Line(-10, 0, x0, 0, null, "lead");
            for (var i = 0; i < n; i++)
            {
                var x = x0 + 3 * i;
                p.Line(x, -4, x, 4, S.Sym, "plus");
                p.Line(x + 1.5, -2, x + 1.5, 2, S.Bold, "minus");
                if (i + 1 < n) p.Line(x + 1.5, 0, x + 3, 0, null, "link");
            }
            p.Line(x0 + 3 * (n - 1) + 1.5, 0, 10, 0, null, "lead");
            p.Text(x0 - 1.6, -3.4, "+", 3, TextAnchor.Middle, false, "polarity");
        }

        private static void Sensor(Pen p, SketchObject o)
        {
            p.Rect(-4.5, -4.5, 4.5, 4.5, null, "body");
            p.Arrow(-9.5, 0, -4.5, 0, 1.5, 0.6, S.Thin, "measurand");
            p.Line(4.5, 0, 10, 0, null, "lead");
            p.Line(0, 4.5, 0, 5, null, "lead");
            var label = o.Str("label", "");
            if (label.Length > 0) p.Text(0, 0, label, Math.Min(4, 7.0 / (label.Length * 0.55)), TextAnchor.Middle, false, "name");
            else
            {
                p.Arc(0, 1.8, 3, 3, 180, 180, null, "gauge");
                p.Line(0, 1.8, 1.9, -0.6, null, "needle");
            }
        }

        private static void Actuator(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -4.5, 0, null, "lead");
            p.Rect(-4.5, -4.5, 4.5, 4.5, null, "body");
            p.Line(0, 4.5, 0, 5, null, "lead");
            p.Arrow(4.5, 0, 9.8, 0, 1.6, 0.7, S.Sym, "motion");
            var label = o.Str("label", "");
            if (label.Length > 0) p.Text(0, 0, label, Math.Min(4, 7.0 / (label.Length * 0.55)), TextAnchor.Middle, false, "name");
            else p.PolyD(Frag.HumpsH(-3, 3, 1.2, 3), S.Sym, false, "coil");
        }
    }
}
