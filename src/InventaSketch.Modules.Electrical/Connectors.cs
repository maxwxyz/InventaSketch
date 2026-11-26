using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Connectors, terminals, test points, junction dots and net labels (box types; wires and buses are point types).</summary>
    internal static class Connectors
    {
        public const string Cat = "elec.connectors";

        private static readonly ParamDef Gender = Choice("gender", T("Kind", "Art"), "pair",
            ("pair", "Plug and socket (mated)", "Steckverbindung (gesteckt)"), ("plug", "Plug (male)", "Stecker"), ("socket", "Socket (female)", "Buchse"));
        private static readonly ParamDef Poles = Int("poles", T("Poles", "Pole"), 1, 1, 12);
        private static readonly ParamDef Net = Text("net", T("Net name", "Netzname"), "NET");

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.connector", T("Connector (plug/socket)", "Steckverbinder (Stecker/Buchse)"), Cat,
                new[] { "connector", "plug", "socket", "jack", "header", "steckverbinder", "stecker", "buchse", "steckverbindung", "x" },
                Views.Symbol(20, 10, Connector))
            {
                Pins = ConnectorPins, Params = new[] { Gender, Poles }, Uniform = false, RefPrefix = "X",
                // A mated pair joins socket and plug pole by pole; a lone plug or socket leads elsewhere (unknown current).
                Net = o =>
                {
                    var n = new NetSpec();
                    if (o.Str("gender", "pair") == "pair")
                        for (var i = 1; i <= Count(o, "poles", 1, 1, 12); i++) n.Shorts.Add(new[] { "s" + N(i), "p" + N(i) });
                    return n;
                },
                Extra = o => Choice(o, "elec.connector", T("Plug / socket", "Stecker / Buchse"), Gender),
            };

            yield return new ElecType("elec.terminal", T("Terminal", "Anschlussklemme"), Cat,
                new[] { "terminal", "clamp", "connection point", "klemme", "anschluss", "anschlussstelle" },
                Views.Symbol(6, 6, (p, o) => p.Round(0, 0, 1.2, null, "terminal")))
            { Pins = Fixed(new Pin("1", 0, 0)), RefPrefix = "X", LabelSide = LabelSide.Right };

            yield return new ElecType("elec.testpoint", T("Test point", "Messpunkt"), Cat,
                new[] { "test point", "testpoint", "tp", "probe", "messpunkt", "prüfpunkt" },
                Views.Symbol(10, 10, (p, o) =>
                {
                    p.Line(0, 5, 0, -0.5, null, "lead");
                    p.Round(0, -2.5, 2, null, "body");
                    p.Dot(0, -2.5, 0.7);
                }))
            { Pins = Fixed(new Pin("1", 0, 0.5)), RefPrefix = "TP", LabelSide = LabelSide.Right, Net = _ => NetSpec.Open("1") };

            yield return new ElecType("elec.junction", T("Junction dot", "Verbindungspunkt"), Cat,
                new[] { "junction", "dot", "node", "connection", "verbindungspunkt", "knoten", "abzweig" },
                Views.Symbol(2, 2, (p, o) => p.Dot(0, 0, 0.8, "junction")))
            { Pins = Fixed(new Pin("1", 0, 0)), Labeled = false, Net = _ => NetSpec.Short("1"), JunctionNode = true };

            yield return new ElecType("elec.netlabel", T("Net label", "Netzname"), Cat,
                new[] { "net label", "net", "label", "netzname", "signalname", "potential", "vcc", "gnd" },
                Views.Symbol(20, 6, NetLabel))
            {
                Pins = Fixed(new Pin("1", -0.5, 0)), Params = new[] { Net }, Labeled = false, TextObject = true, Uniform = false,
                // Net labels of the same name join; a name used once leads elsewhere.
                Net = o => new NetSpec { Global = "label:" + (!string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("net", "NET")).Trim(), GlobalNeedsPartner = true },
            };
        }

        private static double PoleY(int i, int n) => ((i + 0.5) / n - 0.5) * 10;

        private static IReadOnlyList<Pin> ConnectorPins(SketchObject o)
        {
            var n = Count(o, "poles", 1, 1, 12);
            var g = o.Str("gender", "pair");
            var pins = new List<Pin>();
            if (g != "plug") for (var i = 0; i < n; i++) pins.Add(new Pin("s" + N(i + 1), -0.5, PoleY(i, n) / 10));
            if (g != "socket") for (var i = 0; i < n; i++) pins.Add(new Pin("p" + N(i + 1), 0.5, PoleY(i, n) / 10));
            return pins;
        }

        private static void Connector(Pen p, SketchObject o)
        {
            var n = Count(o, "poles", 1, 1, 12);
            var g = o.Str("gender", "pair");
            var r = Math.Min(2, 3.8 / n);
            for (var i = 0; i < n; i++)
            {
                var y = PoleY(i, n);
                if (g != "plug")
                {
                    p.Line(-10, y, -1 - r, y, null, "lead");
                    p.Arc(-1, y, r, r, 90, 180, null, "socket");
                }
                if (g != "socket")
                {
                    p.Line(-1, y, 2.5, y, S.Bold, "plug");
                    p.Line(2.5, y, 10, y, null, "lead");
                }
            }
        }

        private static void NetLabel(Pen p, SketchObject o)
        {
            p.Poly(S.Sym, true, "flag", -10, 0, -7, -3, 10, -3, 10, 3, -7, 3);
            var text = !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("net", "NET");
            if (text.Length > 0) p.Text(1.5, 0, text, Math.Min(3, 15.0 / (text.Length * 0.55)), TextAnchor.Middle, false, "net");
        }
    }
}
