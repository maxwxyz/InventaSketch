using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Power electronics blocks: converters, DC link, gate driver and a half-bridge power module.</summary>
    internal static class Power
    {
        public const string Cat = "elec.power";

        private static readonly ParamDef Phases = Choice("phases", T("AC phases", "AC-Phasen"), "3", ("3", "Three-phase", "Dreiphasig"), ("1", "Single-phase", "Einphasig"));

        private enum Side { Dc, Ac }

        public static IEnumerable<ElecType> Types()
        {
            yield return Converter("elec.pe.inverter", T("Inverter (DC/AC)", "Wechselrichter (DC/AC)"), Side.Dc, Side.Ac,
                "inverter", "wechselrichter", "umrichter", "dc/ac", "traction inverter", "pulswechselrichter", "converter", "stromrichter");
            yield return Converter("elec.pe.rectifier", T("Rectifier (AC/DC)", "Gleichrichter (AC/DC)"), Side.Ac, Side.Dc,
                "rectifier", "gleichrichter", "ac/dc", "charger", "ladegerät", "converter", "stromrichter");
            yield return Converter("elec.pe.dcdc", T("DC/DC converter", "Gleichspannungswandler (DC/DC)"), Side.Dc, Side.Dc,
                "dc/dc", "dcdc", "converter", "buck", "boost", "gleichspannungswandler", "wandler", "stromrichter");

            yield return new ElecType("elec.pe.dclink", T("DC link capacitor", "Zwischenkreiskondensator"), Cat,
                new[] { "dc link", "dc-link", "capacitor", "intermediate circuit", "zwischenkreis", "zwischenkreiskondensator", "kondensator" },
                Views.Symbol(20, 20, DcLink))
            {
                Pins = Fixed(new Pin("+1", -0.5, -0.25), new Pin("+2", 0.5, -0.25), new Pin("-1", -0.5, 0.25), new Pin("-2", 0.5, 0.25)),
                RefPrefix = "C",
            };

            yield return new ElecType("elec.pe.gatedriver", T("Gate driver", "Gate-Treiber"), Cat,
                new[] { "gate driver", "driver", "gate-treiber", "treiber", "ansteuerung", "isolated driver" },
                Views.Block(20, 20, (p, o) =>
                {
                    p.Rect(-7, -7, 7, 7, null, "body");
                    p.Poly(S.Sym, true, "amplifier", -3.5, -4, -3.5, 3, 3.5, -0.5);
                    p.Text(0, 5.2, "GD", 2.2, TextAnchor.Middle, false, "name");
                    p.Line(-10, 0, -7, 0, null, "lead");
                    p.Line(7, -5, 10, -5, null, "lead");
                    p.Line(7, 5, 10, 5, null, "lead");
                }))
            { Pins = Fixed(new Pin("in", -0.5, 0), new Pin("out", 0.5, -0.25), new Pin("ref", 0.5, 0.25)), RefPrefix = "U" };

            yield return new ElecType("elec.pe.module", T("Power module (half bridge)", "Leistungsmodul (Halbbrücke)"), Cat,
                new[] { "power module", "half bridge", "igbt module", "leistungsmodul", "halbbrücke", "brückenzweig", "phase leg" },
                Views.Symbol(30, 40, PowerModule))
            {
                Pins = Fixed(new Pin("P", 0, -0.5), new Pin("N", 0, 0.5), new Pin("AC", 0.5, 0), new Pin("G1", -0.5, -0.25), new Pin("G2", -0.5, 0.25)),
                Uniform = false, RefPrefix = "A",
            };
        }

        private static (string Id, double Y)[] SidePins(Side side, SketchObject o, bool left)
        {
            if (side == Side.Dc) return left ? new[] { ("+", -5.0), ("-", 5.0) } : new[] { ("out+", -5.0), ("out-", 5.0) };
            return o.Str("phases", "3") == "1" ? new[] { ("L", -5.0), ("N", 5.0) } : new[] { ("U", -5.0), ("V", 0.0), ("W", 5.0) };
        }

        private static ElecType Converter(string id, I18nText name, Side left, Side right, params string[] keywords)
        {
            var hasAc = left == Side.Ac || right == Side.Ac;
            return new ElecType(id, name, Cat, keywords, Views.Block(20, 20, (p, o) => ConverterDraw(p, o, left, right)))
            {
                Pins = o => SidePins(left, o, true).Select(x => new Pin(x.Id, -0.5, x.Y / 20))
                    .Concat(SidePins(right, o, false).Select(x => new Pin(x.Id, 0.5, x.Y / 20))).ToList(),
                Params = hasAc ? new[] { Phases } : Array.Empty<ParamDef>(),
                RefPrefix = "A",
            };
        }

        private static void Mark(Pen p, Side side, double cx, double cy)
        {
            if (side == Side.Ac) p.PolyD(Frag.Sine(cx, cy, 2, 1), S.Sym, false, "ac");
            else
            {
                p.Line(cx - 2, cy - 0.7, cx + 2, cy - 0.7, null, "dc");
                p.Line(cx - 2, cy + 0.7, cx + 2, cy + 0.7, null, "dc");
            }
        }

        private static void ConverterDraw(Pen p, SketchObject o, Side left, Side right)
        {
            p.Rect(-7, -7, 7, 7, null, "body");
            p.Line(-7, 7, 7, -7, null, "diagonal");
            Mark(p, left, -3.5, -3.5);
            Mark(p, right, 3.5, 3.5);
            foreach (var (_, y) in SidePins(left, o, true)) p.Line(-10, y, -7, y, null, "lead");
            foreach (var (_, y) in SidePins(right, o, false)) p.Line(7, y, 10, y, null, "lead");
        }

        private static void DcLink(Pen p, SketchObject o)
        {
            p.Line(-10, -5, 10, -5, null, "rail");
            p.Line(-10, 5, 10, 5, null, "rail");
            p.Line(0, -5, 0, -1, null, "lead");
            p.Line(-3, -1, 3, -1, S.Bold, "plate");
            p.Rect(-3, 0.6, 3, 1.6, S.Black, "plate");
            p.Line(0, 1.6, 0, 5, null, "lead");
            p.Dot(0, -5);
            p.Dot(0, 5);
            p.Text(4.6, -2.6, "+", 2.6, TextAnchor.Middle, false, "polarity");
        }

        private static void Switch(Pen p, double cy)
        {
            p.Line(-15, cy, -5.5, cy, null, "lead");
            p.Line(-5.5, cy - 3, -5.5, cy + 3, null, "gate");
            p.Line(-4, cy - 3.5, -4, cy + 3.5, S.Bold, "base");
            p.Line(-4, cy - 1.5, 0, cy - 4, null, "collector");
            p.Line(-4, cy + 1.5, 0, cy + 4, null, "emitter");
            p.Head(-0.8, cy + 3.5, -4, cy + 1.5, 1.6, 0.7, "arrow");
            // Antiparallel freewheeling diode (cathode towards the collector).
            p.Poly(S.Sym, false, "diode", 0, cy - 4, 5, cy - 4, 5, cy - 1.8);
            p.Line(3.5, cy - 1.8, 6.5, cy - 1.8, null, "diode");
            p.Poly(S.Sym, true, "diode", 3.5, cy + 1.8, 6.5, cy + 1.8, 5, cy - 1.8);
            p.Poly(S.Sym, false, "diode", 5, cy + 1.8, 5, cy + 4, 0, cy + 4);
        }

        private static void PowerModule(Pen p, SketchObject o)
        {
            p.Rect(-11, -17, 11, 17, S.Link, "housing");
            Switch(p, -10);
            Switch(p, 10);
            p.Line(0, -20, 0, -14, null, "lead");
            p.Line(0, -6, 0, 6, null, "lead");
            p.Line(0, 14, 0, 20, null, "lead");
            p.Line(0, 0, 15, 0, null, "lead");
            p.Dot(0, 0);
        }
    }
}
