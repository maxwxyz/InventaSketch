using System;
using System.Collections.Generic;
using System.Globalization;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>Storage and power conversion: inverter, power converter, battery storage.</summary>
    internal static class Conversion
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly ParamDef InverterPower = Pm.Num("power", "Rated power", "Nennleistung", 10, 0.1, 1000000, "kW", 1);
        internal static readonly ParamDef Phases = Pm.Choice("phases", "AC phases", "AC-Phasen", "3", ("3", "Three-phase", "Dreiphasig"), ("1", "Single-phase", "Einphasig"));
        internal static readonly ParamDef InverterKind = Pm.Choice("kind", "Inverter kind", "Wechselrichterart", "pv",
            ("pv", "PV inverter", "PV-Wechselrichter"), ("hybrid", "Hybrid inverter (PV + battery)", "Hybridwechselrichter (PV + Batterie)"),
            ("battery", "Battery inverter", "Batteriewechselrichter"));
        internal static readonly ParamDef ConverterKind = Pm.Choice("kind", "Conversion", "Umformung", "acdc",
            ("acdc", "Rectifier (AC/DC)", "Gleichrichter (AC/DC)"), ("dcdc", "DC/DC converter", "Gleichspannungswandler (DC/DC)"),
            ("acac", "Frequency converter (AC/AC)", "Frequenzumrichter (AC/AC)"));
        internal static readonly ParamDef Capacity = Pm.Num("capacity", "Usable capacity", "Nutzbare Kapazität", 10, 0.1, 10000000, "kWh", 1);
        internal static readonly ParamDef Soc = Pm.Num("soc", "State of charge", "Ladezustand", 75, 0, 100, "%", 5);
        internal static readonly ParamDef BatteryForm = Pm.Choice("form", "Design", "Bauform", "wall",
            ("wall", "Home storage (wall)", "Heimspeicher (Wand)"), ("cabinet", "Storage cabinet", "Speicherschrank"),
            ("container", "Container storage (BESS)", "Containerspeicher (BESS)"));

        private const string Display = "#b2dfdb";

        public static IEnumerable<EnergyType> Types()
        {
            yield return new EnergyType("energy.conv.inverter", T("Inverter", "Wechselrichter"), EnCategories.Storage,
                new[]
                {
                    "inverter", "pv inverter", "hybrid inverter", "battery inverter", "dc/ac", "string inverter", "converter",
                    "wechselrichter", "hybridwechselrichter", "batteriewechselrichter", "stringwechselrichter", "umrichter", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(24, 30, InverterPictorial) with
                {
                    Ports = InverterPorts(new Port("ac", 0, -15), new Port("dc", 0, 15), new Port("bat", 12, 2.5)),
                    LabelBox = ViewSpec.LabelAt(-7.5, -1, 7.5, 8.6), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(38, 24, InverterSymbol) with
                {
                    Ports = InverterPorts(new Port("ac", 0, -12), new Port("dc", 0, 12), new Port("bat", 19, 2.5)),
                    LabelBox = ViewSpec.LabelAt(6.5, -11, 19, 0.8),
                })
            {
                Params = new[] { InverterPower, Phases, InverterKind },
                Rating = (o, l) => InverterRating(o, l),
                Uniform = false,
                CompanionIds = new[] { "energy.pv.string", "energy.storage.battery", "energy.grid.meter", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Cycle(o, "energy.kind.next", InverterKind),
                    Act.Cycle(o, "energy.phases.next", Phases),
                    Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), InverterPower, l),
                },
            };

            yield return new EnergyType("energy.conv.converter", T("Power converter", "Stromrichter"), EnCategories.Storage,
                new[]
                {
                    "converter", "rectifier", "dc/dc", "ac/dc", "frequency converter", "power electronics", "charger",
                    "stromrichter", "gleichrichter", "gleichspannungswandler", "frequenzumrichter", "leistungselektronik", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(24, 30, ConverterPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -15), new Port("b", 0, 15)),
                    LabelBox = ViewSpec.LabelAt(-7.5, -1, 7.5, 8.6), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(38, 24, ConverterSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12), new Port("b", 0, 12)),
                    LabelBox = ViewSpec.LabelAt(6.5, -6, 19, 6),
                })
            {
                Params = new[] { InverterPower, ConverterKind },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(InverterPower) * 1000, "W", l), Short(o.S(ConverterKind)) },
                Uniform = false,
                CompanionIds = new[] { "energy.h2.electrolyser", "energy.storage.battery", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Cycle(o, "energy.kind.next", ConverterKind),
                    Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), InverterPower, l),
                },
            };

            yield return new EnergyType("energy.storage.battery", T("Battery storage", "Batteriespeicher"), EnCategories.Storage,
                new[]
                {
                    "battery", "storage", "battery storage", "energy storage", "home storage", "bess", "lithium", "accumulator",
                    "batterie", "speicher", "batteriespeicher", "stromspeicher", "heimspeicher", "energiespeicher", "akku", "großspeicher", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(24, 30, BatteryPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", 0, -15)),
                    LabelBox = o => o.S(BatteryForm) switch
                    {
                        "container" => new Box(-8.6, -4.4, 3.6, 3.4),
                        "cabinet" => new Box(-8.5, 8, 8.5, 12.3),
                        _ => new Box(-7, 7, 7, 12),
                    },
                    LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(36, 24, BatterySymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", 0, -12)), LabelBox = ViewSpec.LabelAt(6.5, -6, 18, 6),
                })
            {
                Params = new[] { Capacity, Soc, BatteryForm },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(Capacity) * 1000, "Wh", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.conv.inverter", "energy.grid.meter", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Cycle(o, "energy.form.next", BatteryForm),
                    Act.Number(o, "energy.capacity.set", T("Capacity…", "Kapazität…"), Capacity, l),
                    Act.Number(o, "energy.soc.set", T("State of charge…", "Ladezustand…"), Soc, l),
                },
            };
        }

        private static Func<SketchObject, IReadOnlyList<Port>> InverterPorts(Port ac, Port dc, Port bat)
            => o => o.S(InverterKind) == "hybrid" ? new[] { ac, dc, bat } : new[] { ac, dc };

        private static IEnumerable<string> InverterRating(SketchObject o, Lang l)
        {
            yield return Fmt.Si(o.Clamped(InverterPower) * 1000, "W", l) + " " + (o.S(Phases) == "1" ? "1~" : "3~");
            switch (o.S(InverterKind))
            {
                case "hybrid": yield return "Hybrid"; break;
                case "battery": yield return l == Lang.De ? "Batterie" : "Battery"; break;
            }
        }

        private static string Short(string kind) => kind switch { "dcdc" => "DC/DC", "acac" => "AC/AC", _ => "AC/DC" };

        // ------------------------------------------------------------------ shared pieces

        /// <summary>Wall-mounted power electronics housing with a display showing input sign → output sign.</summary>
        private static void Housing(Pen p, bool inDc, bool outDc)
        {
            var stub = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -15, 0, -11, stub, EnTags.Lead);
            p.Line(0, 11, 0, 15, stub, EnTags.Lead);
            p.Rect(-9, -11, 9, 11, St.Fill(Pal.Metal), "body", 1.5);
            p.Rect(-6.5, -8.5, 6.5, -2.5, St.Fill(Pal.Dark), "display", 0.4);
            var s = St.Detail(Display, 0.3);
            // Energy flows from the device side (bottom, left on the display) to the grid side (top, right).
            Sign(p, -3.9, -5.5, 1.5, inDc, s);
            p.Arrow(-1.5, -5.5, 1.5, -5.5, 0.9, 0.45, s, "flow");
            Sign(p, 3.9, -5.5, 1.5, outDc, s);
            p.Dot(6, 8.8, 0.45, Pal.Green, "led");
        }

        private static void Sign(Pen p, double cx, double cy, double w, bool dc, Style s)
        {
            if (dc) p.DcSign(cx, cy, w, s);
            else p.Sine(cx, cy, w, w * 0.45, s);
        }

        /// <summary>
        /// Single-line converter symbol: a square with a diagonal, the sign of the top (grid-side) connection in the upper
        /// left triangle and the sign of the bottom connection in the lower right one.
        /// </summary>
        internal static void ConverterBox(Pen p, double cx, double cy, double half, bool topDc, bool bottomDc, double portTop, double portBottom)
        {
            p.Line(cx, portTop, cx, cy - half, St.Sym, EnTags.Lead);
            p.Line(cx, cy + half, cx, portBottom, St.Sym, EnTags.Lead);
            p.Rect(cx - half, cy - half, cx + half, cy + half, St.SymWhite, "body");
            p.Line(cx - half, cy + half, cx + half, cy - half, St.Sym, "diagonal");
            var w = half * 0.36;
            Sign(p, cx - half * 0.45, cy - half * 0.45, w, topDc, St.Thin);
            Sign(p, cx + half * 0.45, cy + half * 0.45, w, bottomDc, St.Thin);
        }

        private static void InverterPictorial(Pen p, SketchObject o)
        {
            if (o.S(InverterKind) == "hybrid") p.Line(9, 2.5, 12, 2.5, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            Housing(p, true, false);
        }

        private static void InverterSymbol(Pen p, SketchObject o)
        {
            if (o.S(InverterKind) == "hybrid")
            {
                p.Line(5, 2.5, 19, 2.5, St.Sym, EnTags.Lead);
                p.DcSign(15, 5, 1.3, St.Thin);
            }
            ConverterBox(p, 0, 0, 5, false, true, -12, 12);
        }

        private static void ConverterPictorial(Pen p, SketchObject o)
        {
            var kind = o.S(ConverterKind);
            Housing(p, kind != "acdc" && kind != "acac", kind != "acac");
        }

        private static void ConverterSymbol(Pen p, SketchObject o)
        {
            var kind = o.S(ConverterKind);
            ConverterBox(p, 0, 0, 5, kind == "dcdc", kind != "acac", -12, 12);
        }

        // ------------------------------------------------------------------ battery

        private static string LevelColor(double soc) => soc < 20 ? Pal.Red : Pal.Green;

        private static void BatteryPictorial(Pen p, SketchObject o)
        {
            var soc = o.Clamped(Soc) / 100;
            var stub = St.Stroke(Pal.Dark, 0.5);
            switch (o.S(BatteryForm))
            {
                case "cabinet":
                    p.Line(0, -15, 0, -12.5, stub, EnTags.Lead);
                    p.Rect(-10.5, -12.5, 10.5, 13, St.Fill(Pal.Metal), "body", 0.8);
                    p.Rect(-8.5, -11, 8.5, -7.5, St.Fill(Pal.Dark), "display", 0.3);
                    p.Rect(-7.5, -9.9, 7.5, -8.6, St.Detail(Display, 0.2), "soc-frame");
                    if (soc > 0.005) p.Rect(-7.3, -9.7, -7.3 + 14.6 * soc, -8.8, St.Solid(LevelColor(soc * 100)), "soc");
                    for (var i = 0; i < 4; i++)
                    {
                        var y = -6.5 + i * 3.6;
                        p.Rect(-8.5, y, 8.5, y + 3, St.Fill("#cfd8dc"), "module", 0.3);
                        p.Dot(7, y + 1.5, 0.35, Pal.Green, "led");
                    }
                    break;
                case "container":
                    p.Line(0, -15, 0, -9, stub, EnTags.Lead);
                    p.Rect(-11.5, -9, 11.5, 11, St.Fill("#e0e0e0"), "container");
                    for (var x = -10.4; x < 5.6; x += 1.6) p.Line(x, -8.2, x, 10.2, St.Detail("#9e9e9e"), "corrugation");
                    p.Line(6, -9, 6, 11, St.Out, "door");
                    p.Line(8.75, -9, 8.75, 11, St.Detail(Pal.Ink), "door");
                    p.Line(7.4, -1, 7.4, 3, St.Stroke(Pal.Ink, 0.35), "handle");
                    p.Line(10.1, -1, 10.1, 3, St.Stroke(Pal.Ink, 0.35), "handle");
                    p.Rect(-9, -5, 4, 4, St.Fill(Pal.White), "placard", 0.3);
                    p.Rect(-11.5, 11, 11.5, 12, St.Fill(Pal.Dark), "base");
                    break;
                default:
                    p.Line(0, -15, 0, -12, stub, EnTags.Lead);
                    p.Rect(-8, -12, 8, 13, St.Fill(Pal.Light), "body", 1.5);
                    p.Rect(-1.5, -7.6, 1.5, -6.5, St.Solid(Pal.Ink), "terminal");
                    p.Rect(-3.5, -6.5, 3.5, 5.5, St.Fill(Pal.White), "cell", 0.6);
                    if (soc > 0.005) p.Rect(-2.8, 4.8 - 10.6 * soc, 2.8, 4.8, St.Solid(LevelColor(soc * 100)), "soc");
                    break;
            }
        }

        private static void BatterySymbol(Pen p, SketchObject o)
        {
            const double cx = 0;
            var thick = Style.Visible with { StrokeWidth = 0.9 };
            p.Line(cx, -12, cx, -3.5, St.Sym, EnTags.Lead);
            p.Line(cx - 4, -3.5, cx + 4, -3.5, St.Sym, "plate+");
            p.Line(cx - 2, -1.5, cx + 2, -1.5, thick, "plate-");
            p.Line(cx - 4, 0.5, cx + 4, 0.5, St.Sym, "plate+");
            p.Line(cx - 2, 2.5, cx + 2, 2.5, thick, "plate-");
            p.Line(cx, 2.5, cx, 5.5, St.Sym, EnTags.Lead);
            p.Text(cx - 5.4, -5, "+", 2, 2, 2, TextAnchor.Middle, false, null, "polarity");
        }

        internal static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
