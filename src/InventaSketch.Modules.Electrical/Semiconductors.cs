using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Diodes, transistors and thyristors (drawn independently in IEC 60617 style).</summary>
    internal static class Semiconductors
    {
        public const string Cat = "elec.semi";

        private static readonly ParamDef Envelope = Flag("envelope", T("Envelope circle", "Gehäusekreis"), false);
        private static readonly ParamDef BjtPolarity = Choice("polarity", T("Type", "Typ"), "npn", ("npn", "NPN", "NPN"), ("pnp", "PNP", "PNP"));
        private static readonly ParamDef Channel = Choice("channel", T("Channel", "Kanal"), "n", ("n", "N-channel", "N-Kanal"), ("p", "P-channel", "P-Kanal"));

        private static readonly Pin[] DiodePins = { new("A", -0.5, 0), new("K", 0.5, 0) };

        /// <summary>A diode's value text as a marking ("2.1 V 20 mA"); a part number ("1N4148") gives nothing.</summary>
        private static readonly Func<SketchObject, IEnumerable<MarkingValue>> DiodeMarking = ElecValues.ByKind((Quantities.Voltage, "U"), (Quantities.Current, "I"));
        private static readonly Pin[] TransistorPins = { new("B", -0.5, 0), new("C", 0.25, -0.5), new("E", 0.25, 0.5) };
        private static readonly Pin[] FetPins = { new("G", -0.5, 0), new("D", 0.25, -0.5), new("S", 0.25, 0.5) };
        private static readonly Pin[] IgbtPins = { new("G", -0.5, 0), new("C", 0.25, -0.5), new("E", 0.25, 0.5) };

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.diode", T("Diode", "Diode"), Cat,
                new[] { "diode", "rectifier diode", "gleichrichterdiode", "d" },
                Views.Symbol(20, 8, (p, o) => Diode(p)))
            {
                Pins = Fixed(DiodePins), Polarized = true, RefPrefix = "D", Params = ElecValues.DiodeParams, Variables = ElecValues.DiodeVariables,
                Relations = ElecValues.PowerRelations, Marking = DiodeMarking, Net = ElecValues.Two("A", "K", ElecValues.Drop), Checks = ElecValues.ReducibleOnly,
            };

            yield return new ElecType("elec.diode.led", T("LED", "Leuchtdiode (LED)"), Cat,
                new[] { "led", "light emitting diode", "leuchtdiode", "diode", "licht" },
                Views.Symbol(20, 12, (p, o) => { Diode(p); Rays(p, false); }))
            {
                Pins = Fixed(DiodePins), Polarized = true, RefPrefix = "D", Params = ElecValues.LedParams, Variables = ElecValues.DiodeVariables,
                Relations = ElecValues.PowerRelations, Marking = DiodeMarking, Net = ElecValues.Two("A", "K", ElecValues.Drop),
                Checks = new[] { ElecValues.LedCurrent, ElecValues.NotReducible },
            };

            yield return new ElecType("elec.diode.zener", T("Zener diode", "Z-Diode"), Cat,
                new[] { "zener", "z-diode", "zenerdiode", "diode", "reference" },
                Views.Symbol(20, 8, (p, o) => Diode(p, zener: true)))
            {
                Pins = Fixed(DiodePins), Polarized = true, RefPrefix = "D", Params = ElecValues.ZenerParams, Variables = ElecValues.ZenerVariables,
                Relations = ElecValues.PowerRelations, Marking = DiodeMarking, Net = ElecValues.Two("A", "K", ElecValues.Drop), Checks = ElecValues.ReducibleOnly,
            };

            yield return new ElecType("elec.diode.photo", T("Photodiode", "Fotodiode"), Cat,
                new[] { "photodiode", "fotodiode", "photo", "light sensor", "lichtsensor", "diode" },
                Views.Symbol(20, 12, (p, o) => { Diode(p); Rays(p, true); }))
            { Pins = Fixed(DiodePins), Polarized = true, RefPrefix = "D", Net = ElecValues.Two("A", "K", ElecValues.Opaque) };

            yield return new ElecType("elec.transistor.bjt", T("Bipolar transistor", "Bipolartransistor"), Cat,
                new[] { "transistor", "bjt", "npn", "pnp", "bipolar", "bipolartransistor" },
                Views.Symbol(20, 20, Bjt))
            {
                Pins = Fixed(TransistorPins), Params = new[] { BjtPolarity, Envelope }, RefPrefix = "Q",
                Extra = o => new[] { Swap(o, "polarity", "npn", "pnp", T("Swap NPN/PNP", "NPN/PNP wechseln")), Toggle(o, "elec.envelope", T("Envelope circle", "Gehäusekreis"), "envelope", false) },
            };

            yield return new ElecType("elec.transistor.mosfet", T("MOSFET", "MOSFET"), Cat,
                new[] { "mosfet", "fet", "transistor", "n-channel", "p-channel", "n-kanal", "p-kanal", "feldeffekttransistor" },
                Views.Symbol(20, 20, Mosfet))
            {
                Pins = Fixed(FetPins), Params = new[] { Channel, Envelope }, RefPrefix = "Q",
                Extra = o => new[] { Swap(o, "channel", "n", "p", T("Swap N/P channel", "N/P-Kanal wechseln")), Toggle(o, "elec.envelope", T("Envelope circle", "Gehäusekreis"), "envelope", false) },
            };

            yield return new ElecType("elec.transistor.igbt", T("IGBT", "IGBT"), Cat,
                new[] { "igbt", "insulated gate bipolar transistor", "transistor", "power switch", "leistungshalbleiter" },
                Views.Symbol(20, 20, Igbt))
            {
                Pins = Fixed(IgbtPins), Params = new[] { Envelope }, RefPrefix = "Q",
                Extra = o => new[] { Toggle(o, "elec.envelope", T("Envelope circle", "Gehäusekreis"), "envelope", false) },
            };

            yield return new ElecType("elec.thyristor", T("Thyristor", "Thyristor"), Cat,
                new[] { "thyristor", "scr", "silicon controlled rectifier", "thyristor" },
                Views.Symbol(20, 10, (p, o) => Thyristor(p)))
            { Pins = Fixed(new Pin("A", -0.5, 0), new Pin("K", 0.5, 0), new Pin("G", 0.25, 0.5)), Polarized = true, RefPrefix = "Q" };
        }

        /// <summary>Two-way switch of a choice parameter (polarity/channel swap).</summary>
        private static ContextAction Swap(SketchObject o, string key, string a, string b, I18nText label)
            => new("elec." + key, label, IconIds.Polarity, x => x.WithParam(key, x.Str(key, a) == a ? b : a)) { IsActive = o.Str(key, a) == b, Group = "polarity" };

        internal static void DiodeBody(Pen p, double cx, double h, bool filled = false)
        {
            p.Poly(filled ? S.Black : S.Sym, true, "body", cx - h, -h, cx - h, h, cx + h, 0);
            p.Line(cx + h, -h, cx + h, h, null, "bar");
        }

        private static void Diode(Pen p, bool zener = false)
        {
            p.Line(-10, 0, -3, 0, null, "lead");
            p.Line(3, 0, 10, 0, null, "lead");
            p.Poly(S.Sym, true, "body", -3, -3, -3, 3, 3, 0);
            if (zener) p.Poly(S.Sym, false, "bar", 2, -3.6, 3, -3, 3, 3, 4, 3.6);
            else p.Line(3, -3, 3, 3, null, "bar");
        }

        /// <summary>Two light rays: emitted (LED) or received (photodiode).</summary>
        private static void Rays(Pen p, bool inward)
        {
            void Ray(double x0, double y0, double x1, double y1)
            {
                if (inward) p.Arrow(x1, y1, x0, y0, 1.2, 0.5, null, "ray");
                else p.Arrow(x0, y0, x1, y1, 1.2, 0.5, null, "ray");
            }
            Ray(-0.8, -3.4, 1.8, -5.6);
            Ray(1.4, -3.4, 4.0, -5.6);
        }

        private static void Bjt(Pen p, SketchObject o)
        {
            if (o.Flag("envelope", false)) p.Circle(1, 0, 7.5, S.Sym, "envelope");
            p.Line(-10, 0, -2, 0, null, "lead");
            p.Line(-2, -4.5, -2, 4.5, S.Bold, "base");
            p.Poly(S.Sym, false, "collector", -2, -1.5, 5, -5.5, 5, -10);
            p.Poly(S.Sym, false, "emitter", -2, 1.5, 5, 5.5, 5, 10);
            if (o.Str("polarity", "npn") == "pnp") p.Head(0.45, 2.9, 5, 5.5, 2, 0.8, "arrow");
            else p.Head(3.6, 4.7, -2, 1.5, 2, 0.8, "arrow");
        }

        private static void Mosfet(Pen p, SketchObject o)
        {
            if (o.Flag("envelope", false)) p.Circle(1.5, 0, 7.5, S.Sym, "envelope");
            p.Line(-10, 0, -3.5, 0, null, "lead");
            p.Line(-3.5, -4, -3.5, 4, S.Sym, "gate");
            p.Line(-2, -4.5, -2, -2.3, S.Bold, "channel");
            p.Line(-2, -1.1, -2, 1.1, S.Bold, "channel");
            p.Line(-2, 2.3, -2, 4.5, S.Bold, "channel");
            p.Poly(S.Sym, false, "drain", -2, -3.4, 5, -3.4, 5, -10);
            p.Poly(S.Sym, false, "source", -2, 3.4, 5, 3.4, 5, 10);
            p.Poly(S.Sym, false, "body", -2, 0, 5, 0, 5, 3.4);
            if (o.Str("channel", "n") == "p") p.Head(3, 0, -1.8, 0, 1.8, 0.75, "arrow");
            else p.Head(-1.8, 0, 3, 0, 1.8, 0.75, "arrow");
        }

        private static void Igbt(Pen p, SketchObject o)
        {
            if (o.Flag("envelope", false)) p.Circle(1.5, 0, 7.5, S.Sym, "envelope");
            p.Line(-10, 0, -3.5, 0, null, "lead");
            p.Line(-3.5, -4, -3.5, 4, S.Sym, "gate");
            p.Line(-2, -4.5, -2, 4.5, S.Bold, "base");
            p.Poly(S.Sym, false, "collector", -2, -2, 5, -5.5, 5, -10);
            p.Poly(S.Sym, false, "emitter", -2, 2, 5, 5.5, 5, 10);
            p.Head(3.6, 4.8, -2, 2, 2, 0.8, "arrow");
        }

        private static void Thyristor(Pen p)
        {
            p.Line(-10, 0, -3, 0, null, "lead");
            p.Line(3, 0, 10, 0, null, "lead");
            p.Poly(S.Sym, true, "body", -3, -3, -3, 3, 3, 0);
            p.Line(3, -3, 3, 3, null, "bar");
            p.Poly(S.Sym, false, "gate", 3, 1.5, 5, 3.5, 5, 5);
        }
    }
}
