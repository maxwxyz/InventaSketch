using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Instrumentation: the instrument bubble with its tag (function letters above, loop number below) and the inline flow
    /// element. The bubble shapes and location bars follow common P&amp;ID practice (the conventions known from ISA-5.1 and
    /// ISO 14617-6); they are drawn independently and no compliance is claimed.
    /// </summary>
    internal static class Instruments
    {
        public const string Cat = ProcessCategories.Instruments;

        public const string BubbleId = "process.instrument.bubble";

        internal static readonly ParamDef Tag = Text(ProcessType.TagKey, "Tag (function letters and loop number)", "Kennzeichen (Funktionsbuchstaben und Nummer)", "TIC 101");
        internal static readonly ParamDef Location = Choice("location", "Location", "Anordnung", "field",
            ("field", "Field mounted (no line)", "Vor Ort (ohne Strich)"),
            ("central", "Control room, accessible (solid line)", "Leitwarte, zugänglich (Vollstrich)"),
            ("local", "Local panel, accessible (double line)", "Örtliche Tafel, zugänglich (Doppelstrich)"),
            ("behind", "Behind panel, not accessible (dashed line)", "Hinter der Tafel, nicht zugänglich (Strichlinie)"));
        internal static readonly ParamDef Shape = Choice("shape", "Device", "Gerät", "discrete",
            ("discrete", "Discrete instrument (circle)", "Einzelgerät (Kreis)"),
            ("shared", "Shared display / DCS (circle in square)", "Prozessleitsystem (Kreis im Quadrat)"),
            ("computer", "Computer function (hexagon)", "Rechnerfunktion (Sechseck)"),
            ("plc", "PLC (diamond in square)", "SPS (Raute im Quadrat)"));
        internal static readonly ParamDef ElementKind = Choice("kind", "Flow element", "Durchflussmessglied", "orifice",
            ("orifice", "Orifice plate", "Messblende"), ("venturi", "Venturi tube", "Venturirohr"));

        public static IEnumerable<ProcessType> Types()
        {
            yield return new ProcessType(BubbleId, T("Instrument (tag bubble)", "Messstelle (PLT-Stelle)"), Cat, 12, 12, Bubble,
                "instrument", "instrument bubble", "tag", "balloon", "measuring point", "controller", "transmitter", "indicator",
                "tic", "fic", "pic", "lic", "pi", "ti", "dcs", "plc", "isa",
                "messstelle", "plt-stelle", "emsr-stelle", "msr", "regler", "messumformer", "anzeige", "prozessleittechnik", "sps", "pls")
            {
                Params = new[] { Tag, Location, Shape },
                Ports = Fixed(new Port("B", 0, 6), new Port("T", 0, -6), new Port("L", -6, 0), new Port("R", 6, 0)),
                CompanionIds = new[] { "process.line.signal", "process.valve.control", "process.instrument.orifice", BubbleId },
                Extra = o => new[]
                {
                    ContextAction.WithInput("process.instrument.tag", T("Tag …", "Kennzeichen …"), IconIds.Text,
                        T("Tag (e.g. TIC 101, PT-205)", "Kennzeichen (z. B. TIC 101, PT-205)"), o.Str(ProcessType.TagKey, "TIC 101"),
                        (x, t) => x.WithParam(ProcessType.TagKey, t.Trim())) with { Group = "label", ParamKeys = new[] { ProcessType.TagKey } },
                    Cycle("process.instrument.location", T("Location", "Anordnung"), Location),
                    Cycle("process.instrument.shape", T("Device", "Gerät"), Shape),
                },
            };

            yield return new ProcessType("process.instrument.orifice", T("Flow element (orifice / venturi)", "Durchflussmessglied (Blende / Venturi)"), Cat, 10, 6, FlowElement,
                "orifice", "orifice plate", "flow element", "venturi", "flow measurement", "differential pressure",
                "blende", "messblende", "venturi", "venturirohr", "durchflussmessung", "wirkdruck", "messstelle")
            {
                Params = new[] { ElementKind },
                Ports = Fixed(new Port("1", -5, 0), new Port("2", 5, 0)),
                TagPrefix = "FE-101",
                CompanionIds = new[] { BubbleId, "process.line.signal", "process.line" },
                Extra = o => new[] { Cycle("process.orifice.kind", T("Orifice / venturi", "Blende / Venturi"), ElementKind) },
            };
        }

        /// <summary>
        /// Splits a tag into the function letters and the loop number: "TIC 101" → ("TIC", "101"), "PT-205A" → ("PT", "205A"),
        /// "FI" → ("FI", "").
        /// </summary>
        public static (string Letters, string Number) SplitTag(string tag)
        {
            var t = (tag ?? "").Trim();
            var i = 0;
            while (i < t.Length && char.IsLetter(t[i])) i++;
            var letters = t.Substring(0, i);
            var number = t.Substring(i).Trim().TrimStart('-', '_', '/').Trim();
            if (letters.Length == 0) return (number, "");
            return (letters, number);
        }

        private static void Bubble(Pen p, SketchObject o)
        {
            const double r = 6;
            var shape = o.Str("shape", "discrete");
            switch (shape)
            {
                case "shared":
                    p.Rect(-r, -r, r, r, S.White, "frame");
                    p.Circle(0, 0, r, S.Sym, ProcessTags.Body);
                    break;
                case "computer":
                    p.Poly(S.White, true, ProcessTags.Body, -r, 0, -r / 2, -r, r / 2, -r, r, 0, r / 2, r, -r / 2, r);
                    break;
                case "plc":
                    p.Rect(-r, -r, r, r, S.White, "frame");
                    p.Poly(S.Sym, true, ProcessTags.Body, -r, 0, 0, -r, r, 0, 0, r);
                    break;
                default:
                    p.Circle(0, 0, r, S.White, ProcessTags.Body);
                    break;
            }
            // Location bar across the middle (the inner width of the shape).
            var half = shape == "plc" ? r / 2 : r;
            switch (o.Str("location", "field"))
            {
                case "central": p.Line(-half, 0, half, 0, S.Sym, "location"); break;
                case "local":
                    p.Line(-half + 0.1, -0.45, half - 0.1, -0.45, S.Thin, "location");
                    p.Line(-half + 0.1, 0.45, half - 0.1, 0.45, S.Thin, "location");
                    break;
                case "behind": p.Line(-half, 0, half, 0, S.Dashed, "location"); break;
            }
            var (letters, number) = SplitTag(o.Str(ProcessType.TagKey, "TIC 101"));
            // Text width budget: the chord of the shape at the text height.
            var budget = shape == "plc" ? 5.4 : 8.6;
            if (number.Length == 0 && o.Str("location", "field") == "field") p.FitText(0, 0, letters, 3.4, budget, TextAnchor.Middle, ProcessTags.Tag, true);
            else
            {
                p.FitText(0, -2.3, letters, 3.0, budget, TextAnchor.Middle, ProcessTags.Tag, true);
                p.FitText(0, 2.4, number, 2.7, budget, TextAnchor.Middle, ProcessTags.Tag);
            }
        }

        private static void FlowElement(Pen p, SketchObject o)
        {
            if (o.Str("kind", "orifice") == "venturi")
            {
                p.Poly(S.Sym, false, ProcessTags.Body, -5, -2.6, -1.6, -1.1, 1.0, -1.1, 5, -2.6);
                p.Poly(S.Sym, false, ProcessTags.Body, -5, 2.6, -1.6, 1.1, 1.0, 1.1, 5, 2.6);
                p.Line(-5, -2.6, -5, 2.6, S.Fine, "flange");
                p.Line(5, -2.6, 5, 2.6, S.Fine, "flange");
                return;
            }
            p.Line(-5, 0, -0.6, 0, S.Sym, "pipe");
            p.Line(0.6, 0, 5, 0, S.Sym, "pipe");
            p.Line(-0.6, -3, -0.6, 3, S.Sym, "flange");
            p.Line(0.6, -3, 0.6, 3, S.Sym, "flange");
            p.Line(0, -1.6, 0, 1.6, S.Bold, "plate");
        }
    }
}
