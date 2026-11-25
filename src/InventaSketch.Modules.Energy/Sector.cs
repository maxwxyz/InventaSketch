using System;
using System.Collections.Generic;
using System.Globalization;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>Sector coupling: heat pump, CHP, thermal storage, electrolyser, fuel cell, hydrogen tank.</summary>
    internal static class Sector
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly ParamDef HeatPower = Pm.Num("power", "Heating power", "Heizleistung", 8, 0.5, 100000, "kW", 1);
        internal static readonly ParamDef Source = Pm.Choice("source", "Heat source", "Wärmequelle", "air",
            ("air", "Air", "Luft"), ("ground", "Ground (brine)", "Erdreich (Sole)"), ("water", "Groundwater", "Grundwasser"));
        internal static readonly ParamDef ElPower = Pm.Num("power", "Electrical power", "Elektrische Leistung", 50, 1, 1000000, "kW", 5);
        internal static readonly ParamDef ThPower = Pm.Num("thermal", "Thermal power", "Thermische Leistung", 80, 1, 1000000, "kW", 5);
        internal static readonly ParamDef ChpFuel = Pm.Choice("fuel", "Fuel", "Brennstoff", "gas",
            ("gas", "Natural gas", "Erdgas"), ("biogas", "Biogas", "Biogas"), ("h2", "Hydrogen", "Wasserstoff"));
        internal static readonly ParamDef Volume = Pm.Num("volume", "Volume", "Volumen", 1000, 50, 100000000, "l", 50);
        internal static readonly ParamDef ElyPower = Pm.Num("power", "Rated power", "Nennleistung", 1000, 0.1, 10000000, "kW", 10);
        internal static readonly ParamDef ElyTech = Pm.Choice("tech", "Technology", "Technologie", "pem",
            ("pem", "PEM", "PEM"), ("alkaline", "Alkaline", "Alkalisch"), ("soec", "Solid oxide (SOEC)", "Festoxid (SOEC)"));
        internal static readonly ParamDef FcPower = Pm.Num("power", "Electrical power", "Elektrische Leistung", 100, 0.1, 1000000, "kW", 5);
        internal static readonly ParamDef Mass = Pm.Num("mass", "Hydrogen capacity", "Wasserstoffmenge", 500, 1, 10000000, "kg", 10);
        internal static readonly ParamDef Pressure = Pm.Num("pressure", "Pressure", "Druck", 350, 1, 1000, "bar", 10);

        private const string Ely = "#00695c";

        public static IEnumerable<EnergyType> Types()
        {
            yield return new EnergyType("energy.heat.heatpump", T("Heat pump", "Wärmepumpe"), EnCategories.Sector,
                new[]
                {
                    "heat pump", "air source", "ground source", "sector coupling", "power to heat", "heating",
                    "wärmepumpe", "luftwärmepumpe", "sole", "sektorkopplung", "power-to-heat", "heizung", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(34, 24, HeatPumpPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", 9, -12), new Port("heat", 17, 3)), LabelBox = ViewSpec.LabelAt(5, -6.5, 13, 8.5), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(30, 24, HeatPumpSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", -6, -12), new Port("heat", 15, 3)), LabelBox = ViewSpec.LabelAt(1.5, -11, 15, 1.5),
                })
            {
                Params = new[] { HeatPower, Source },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(HeatPower) * 1000, "W", l), Pm.OptionLabel(Source, o.S(Source)).Get(l) },
                Uniform = false,
                CompanionIds = new[] { "energy.heat.storage", "energy.grid.meter", "energy.line.pipe" },
                Extra = (o, l) => new[] { Act.Cycle(o, "energy.source.next", Source), Act.Number(o, "energy.power.set", T("Heating power…", "Heizleistung…"), HeatPower, l) },
            };

            yield return new EnergyType("energy.heat.chp", T("CHP unit", "Blockheizkraftwerk (BHKW)"), EnCategories.Sector,
                new[]
                {
                    "chp", "combined heat and power", "cogeneration", "gas engine", "sector coupling",
                    "bhkw", "blockheizkraftwerk", "kraft-wärme-kopplung", "kwk", "gasmotor", "sektorkopplung", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(40, 24, ChpPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", 0, -12), new Port("fuel", -20, 5), new Port("heat", 20, 3)), LabelBox = ViewSpec.LabelAt(5, -5, 16, 8.5),
                },
                ViewSpec.SingleLine(34, 28, ChpSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", -6, -14), new Port("fuel", -17, 7.5), new Port("heat", 17, 7.5)), LabelBox = ViewSpec.LabelAt(1, -12, 16, 4.5),
                })
            {
                Params = new[] { ElPower, ThPower, ChpFuel },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(ElPower) * 1000, "W", l) + "el", Fmt.Si(o.Clamped(ThPower) * 1000, "W", l) + "th", Pm.OptionLabel(ChpFuel, o.S(ChpFuel)).Get(l) },
                Uniform = false,
                CompanionIds = new[] { "energy.heat.storage", "energy.line.pipe", "energy.grid.meter" },
                Extra = (o, l) => new[] { Act.Cycle(o, "energy.fuel.next", ChpFuel), Act.Number(o, "energy.power.set", T("Electrical power…", "Elektrische Leistung…"), ElPower, l) },
            };

            yield return new EnergyType("energy.heat.storage", T("Thermal storage tank", "Wärmespeicher"), EnCategories.Sector,
                new[]
                {
                    "thermal storage", "heat storage", "buffer tank", "hot water tank", "stratified", "storage",
                    "wärmespeicher", "pufferspeicher", "schichtenspeicher", "speicher", "warmwasserspeicher",
                },
                ViewSpec.Pictorial(24, 32, HeatStoragePictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("charge", -12, -8), new Port("discharge", 12, -8)), LabelBox = ViewSpec.LabelAt(-5, -2.2, 5, 2.2), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(28, 26, HeatStorageSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("charge", -14, -5), new Port("discharge", 14, -5)), LabelBox = ViewSpec.LabelAt(0, -2, 14, 9),
                })
            {
                Params = new[] { Volume },
                Rating = (o, l) => new[] { Litres(o.Clamped(Volume), l) },
                Uniform = false,
                CompanionIds = new[] { "energy.heat.heatpump", "energy.heat.chp", "energy.line.pipe" },
                Extra = (o, l) => new[] { Act.Number(o, "energy.volume.set", T("Volume…", "Volumen…"), Volume, l) },
            };

            yield return new EnergyType("energy.h2.electrolyser", T("Electrolyser", "Elektrolyseur"), EnCategories.Sector,
                new[]
                {
                    "electrolyser", "electrolyzer", "hydrogen", "h2", "power to gas", "power-to-x", "pem", "green hydrogen", "sector coupling",
                    "elektrolyseur", "elektrolyse", "wasserstoff", "power-to-gas", "grüner wasserstoff", "sektorkopplung", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(36, 26, ElectrolyserPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", 0, -13), new Port("water", -18, 4), new Port("h2", 18, 0)), LabelBox = ViewSpec.LabelAt(-12, 8.6, 12, 13), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(30, 26, ElectrolyserSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", -4, -13), new Port("water", -15, 0), new Port("h2", 15, 0)), LabelBox = ViewSpec.LabelAt(3, -12, 15, -1.5),
                })
            {
                Params = new[] { ElyPower, ElyTech },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(ElyPower) * 1000, "W", l), Pm.OptionLabel(ElyTech, o.S(ElyTech)).Get(l) },
                Uniform = false,
                CompanionIds = new[] { "energy.h2.tank", "energy.conv.converter", "energy.line.pipe" },
                Extra = (o, l) => new[] { Act.Cycle(o, "energy.tech.next", ElyTech), Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), ElyPower, l) },
            };

            yield return new EnergyType("energy.h2.fuelcell", T("Fuel cell", "Brennstoffzelle"), EnCategories.Sector,
                new[]
                {
                    "fuel cell", "hydrogen", "h2", "pem", "sofc", "reconversion", "sector coupling",
                    "brennstoffzelle", "wasserstoff", "rückverstromung", "sektorkopplung", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(36, 26, FuelCellPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", 0, -13), new Port("h2", -18, 1), new Port("heat", 18, 1)), LabelBox = ViewSpec.LabelAt(-12, 9.4, 12, 13), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(30, 26, FuelCellSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("el", -4, -13), new Port("h2", -15, 0), new Port("heat", 15, 3)), LabelBox = ViewSpec.LabelAt(3, -12, 15, 1.5),
                })
            {
                Params = new[] { FcPower },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(FcPower) * 1000, "W", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.h2.tank", "energy.conv.inverter", "energy.line.pipe" },
                Extra = (o, l) => new[] { Act.Number(o, "energy.power.set", T("Electrical power…", "Elektrische Leistung…"), FcPower, l) },
            };

            yield return new EnergyType("energy.h2.tank", T("Hydrogen tank", "Wasserstofftank"), EnCategories.Sector,
                new[]
                {
                    "hydrogen tank", "h2 tank", "pressure vessel", "hydrogen storage", "storage", "hydrogen",
                    "wasserstofftank", "wasserstoffspeicher", "druckbehälter", "drucktank", "speicher", "wasserstoff",
                },
                ViewSpec.Pictorial(36, 22, TankPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("h2", 0, -11)), LabelBox = ViewSpec.LabelAt(-9, -0.4, 9, 5.2), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(26, 24, TankSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("h2", -6, -12)), LabelBox = ViewSpec.LabelAt(0.5, -5, 13, 5),
                })
            {
                Params = new[] { Mass, Pressure },
                Rating = (o, l) => new[] { Fmt.Num(o.Clamped(Mass), l) + " kg", Fmt.Num(o.Clamped(Pressure), l) + " bar" },
                Uniform = false,
                CompanionIds = new[] { "energy.h2.electrolyser", "energy.h2.fuelcell", "energy.line.pipe" },
                Extra = (o, l) => new[]
                {
                    Act.Number(o, "energy.mass.set", T("Hydrogen capacity…", "Wasserstoffmenge…"), Mass, l),
                    Act.Number(o, "energy.pressure.set", T("Pressure…", "Druck…"), Pressure, l),
                },
            };
        }

        /// <summary>Volume label: litres below 10 000 l, cubic metres above.</summary>
        internal static string Litres(double l, Lang lang) => l < 10000 - 1e-6 ? Fmt.Num(l, lang) + " l" : Fmt.Num(l / 1000, lang) + " m³";

        // ------------------------------------------------------------------ heat pump, CHP, storage

        private static void HeatPumpPictorial(Pen p, SketchObject o)
        {
            p.Line(9, -12, 9, -8, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Line(14, 3, 17, 3, St.Stroke(Pal.Heat, 0.6), EnTags.Lead);
            p.Rect(-12, 10, -9, 11.5, St.Fill(Pal.Dark), "foot");
            p.Rect(9, 10, 12, 11.5, St.Fill(Pal.Dark), "foot");
            p.Rect(-14, -8, 14, 10, St.Fill(Pal.Light), "body", 1);
            p.Line(4, -8, 4, 10, St.Out, "panel");
            p.Round(-5, 1, 7, St.Fill(Pal.Metal), "fan");
            p.Round(-5, 1, 5, St.Detail(Pal.Ink, 0.2), "grille");
            p.Round(-5, 1, 3, St.Detail(Pal.Ink, 0.2), "grille");
            for (var i = 0; i < 4; i++)
            {
                var d = Vec2.Polar(7, Geo.DegToRad(45 * i));
                p.Add(Prim.Line(p.V(-5, 1) + d * p.K, p.V(-5, 1) - d * p.K, St.Detail(Pal.Ink, 0.2)), "grille");
            }
            p.Round(-5, 1, 1, St.Fill(Pal.Steel), "hub");
        }

        private static void HeatPumpSymbol(Pen p, SketchObject o)
        {
            const double cx = -6;
            p.Line(cx, -12, cx, -6, St.Sym, EnTags.Lead);
            p.Line(0, 3, 15, 3, St.Thin, EnTags.Lead);
            p.Rect(cx - 6, -6, cx + 6, 6, St.SymWhite, "body");
            p.Text(cx, -1.5, p.Lang == Lang.De ? "WP" : "HP", 3.4, 8, 3.4, TextAnchor.Middle, true, null, "letter");
            // Heat lifted from the source (bottom) to the useful temperature (top).
            p.Arrow(cx, 4.8, cx, 1.4, 1.2, 0.5, St.Thin, "lift");
        }

        private static void ChpPictorial(Pen p, SketchObject o)
        {
            p.Line(0, -12, 0, -7, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Line(-20, 5, -17, 5, St.Stroke(Pal.Gas, 0.6), EnTags.Lead);
            p.Line(17, 3, 20, 3, St.Stroke(Pal.Heat, 0.6), EnTags.Lead);
            p.Rect(12, -11.5, 14.5, -7, St.Fill(Pal.Dark), "exhaust");
            p.Rect(-17, -7, 17, 10, St.Fill(Pal.Metal), "enclosure", 0.8);
            for (var i = 0; i < 3; i++) p.Rect(-14 + 3 * i, -5, -12 + 3 * i, -3, St.Fill(Pal.Steel), "cylinder");
            p.Rect(-15, -3, -5, 7, St.Fill(Pal.Steel), "engine", 0.5);
            p.Line(-5, 2, -3.4, 2, St.Stroke(Pal.Ink, 0.6), "shaft");
            p.Round(0, 2, 3.4, St.Fill(Pal.White), "generator");
            p.Text(0, 2, "G", 3, 4, 3, TextAnchor.Middle, true, Pal.Ink, "letter");
            p.Rect(-18, 10, 18, 11.4, St.Fill(Pal.Dark), "skid");
        }

        private static void ChpSymbol(Pen p, SketchObject o)
        {
            p.Line(-17, 7.5, -10.5, 7.5, St.Thin, EnTags.Lead);
            p.Line(-1.5, 7.5, 17, 7.5, St.Thin, EnTags.Lead);
            var g = Generation.Generator(p, -6, -4, -14, 4.4);
            p.Add(Prim.Line(g.V(-0.5, 4.4), p.V(-6.5, 4), St.Sym), "shaft");
            p.Add(Prim.Line(g.V(0.5, 4.4), p.V(-5.5, 4), St.Sym), "shaft");
            p.Rect(-10.5, 4, -1.5, 11, St.SymWhite, "engine");
            p.Poly(St.Thin, true, "flame", -6, 5.3, -4.5, 7.7, -5.1, 9.9, -6.9, 9.9, -7.5, 7.7);
        }

        private static readonly string[] Layers = { "#ef5350", "#ffa726", "#90caf9", "#42a5f5" };

        private static void HeatStoragePictorial(Pen p, SketchObject o)
        {
            p.Line(-12, -8, -7, -8, St.Stroke(Pal.Heat, 0.6), EnTags.Lead);
            p.Line(7, -8, 12, -8, St.Stroke(Pal.Heat, 0.6), EnTags.Lead);
            p.Rect(-5.5, 11.5, -4, 14.5, St.Fill(Pal.Dark), "leg");
            p.Rect(4, 11.5, 5.5, 14.5, St.Fill(Pal.Dark), "leg");
            const double top = -11.6, h = 5.55;
            for (var i = 0; i < Layers.Length; i++) p.Rect(-6.3, top + i * h, 6.3, top + (i + 1) * h, St.Solid(Layers[i]), "layer");
            p.Rect(-7, -13, 7, 12, St.Out with { StrokeWidth = 0.35 }, "tank", 3);
            p.Rect(-5.5, -2.6, 5.5, 2.6, St.Fill(Pal.White), "plate", 0.4);
        }

        private static void HeatStorageSymbol(Pen p, SketchObject o)
        {
            p.Line(-14, -5, -10, -5, St.Thin, EnTags.Lead);
            p.Line(-2, -5, 14, -5, St.Thin, EnTags.Lead);
            p.Rect(-10, -9, -2, 9, St.SymWhite, "tank", 2.5);
            foreach (var y in new[] { -3.0, 1.0, 5.0 }) p.Line(-9.2, y, -2.8, y, St.Thin with { Dash = DashStyle.Dash }, "layer");
        }

        // ------------------------------------------------------------------ hydrogen

        private static void ElectrolyserPictorial(Pen p, SketchObject o)
        {
            p.Line(0, -13, 0, -6, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Line(-18, 4, -11, 4, St.Stroke(Pal.Water, 0.6), EnTags.Lead);
            p.Line(11, 0, 18, 0, St.Stroke(Pal.Teal, 0.6), EnTags.Lead);
            for (var i = 0; i < 12; i++)
            {
                var x = -9 + i * 1.5;
                p.Rect(x, -5, x + 1.5, 7, St.Fill(i % 2 == 0 ? "#90a4ae" : "#cfd8dc") with { StrokeWidth = 0.12 }, "cell");
            }
            p.Rect(-11, -6, -9, 8, St.Fill(Pal.Dark), "endplate");
            p.Rect(9, -6, 11, 8, St.Fill(Pal.Dark), "endplate");
            var bubble = St.Detail(Pal.Teal, 0.2);
            p.Round(12.6, -2.2, 0.5, bubble, "bubble");
            p.Round(14.1, -3.6, 0.7, bubble, "bubble");
            p.Round(12.9, -5.3, 0.45, bubble, "bubble");
            p.Text(15.2, -8, "H₂", 2.4, 4, 2.6, TextAnchor.Middle, true, Ely, "medium");
        }

        private static void ElectrolyserSymbol(Pen p, SketchObject o)
        {
            const double cx = -4;
            p.Line(cx, -13, cx, -6, St.Sym, EnTags.Lead);
            p.Line(-15, 0, cx - 6, 0, St.Thin, EnTags.Lead);
            p.Line(cx + 6, 0, 15, 0, St.Thin, EnTags.Lead);
            p.Rect(cx - 6, -6, cx + 6, 6, St.SymWhite, "body");
            p.Line(cx - 6, 6, cx + 6, -6, St.Sym, "diagonal");
            p.DcSign(cx - 2.7, -2.7, 2, St.Thin);
            p.Text(cx + 2.7, 2.7, "H₂", 2.6, 4.4, 2.6, TextAnchor.Middle, false, null, "medium");
        }

        private static void FuelCellPictorial(Pen p, SketchObject o)
        {
            p.Line(0, -13, 0, -7, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Line(-18, 1, -12, 1, St.Stroke(Pal.Teal, 0.6), EnTags.Lead);
            p.Line(12, 1, 18, 1, St.Stroke(Pal.Heat, 0.6), EnTags.Lead);
            p.Rect(-12, -7, 12, 9, St.Fill("#e0f2f1"), "housing", 1);
            p.Rect(-8.4, -4.9, 8.4, 5.9, St.Fill(Pal.White), "window", 0.4);
            for (var i = 0; i < 10; i++)
            {
                var x = -8 + i * 1.6;
                p.Rect(x, -4.5, x + 1.6, 5.5, St.Solid(i % 2 == 0 ? "#80cbc4" : "#b2dfdb"), "cell");
            }
            p.Text(-12 + 2.2, -5.2, "+", 2, 2, 2, TextAnchor.Middle, false, Pal.Ink, "polarity");
            p.Text(12 - 2.2, -5.2, "−", 2, 2, 2, TextAnchor.Middle, false, Pal.Ink, "polarity");
        }

        private static void FuelCellSymbol(Pen p, SketchObject o)
        {
            const double cx = -4;
            p.Line(cx, -13, cx, -6, St.Sym, EnTags.Lead);
            p.Line(-15, 0, cx - 6, 0, St.Thin, EnTags.Lead);
            p.Line(cx + 6, 3, 15, 3, St.Thin, EnTags.Lead);
            p.Rect(cx - 6, -6, cx + 6, 6, St.SymWhite, "body");
            p.Line(cx - 6, -6, cx + 6, 6, St.Sym, "diagonal");
            p.Text(cx - 2.9, 2.8, "H₂", 2.6, 4.4, 2.6, TextAnchor.Middle, false, null, "medium");
            p.DcSign(cx + 2.9, -2.8, 2, St.Thin);
        }

        private static void TankPictorial(Pen p, SketchObject o)
        {
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -11, 0, -5, dark, EnTags.Lead);
            p.Poly(St.Fill(Pal.White), true, "valve", -1.2, -9.4, 1.2, -9.4, 0, -8);
            p.Poly(St.Fill(Pal.White), true, "valve", -1.2, -6.6, 1.2, -6.6, 0, -8);
            p.Line(-16, 9, 16, 9, St.Stroke(Pal.Ink, 0.3), "ground");
            p.Poly(St.Fill(Pal.Steel), true, "saddle", -11, 5, -7, 5, -6.3, 9, -11.7, 9);
            p.Poly(St.Fill(Pal.Steel), true, "saddle", 7, 5, 11, 5, 11.7, 9, 6.3, 9);
            p.Rect(-15, -5, 15, 6, St.Fill("#e0f7fa") with { Stroke = Ely, StrokeWidth = 0.3 }, "vessel", 5.5);
            p.Line(-9, -4.7, -9, 5.7, St.Detail(Ely, 0.2), "band");
            p.Line(9, -4.7, 9, 5.7, St.Detail(Ely, 0.2), "band");
            p.Text(0, -2.4, "H₂", 3, 8, 3, TextAnchor.Middle, true, Ely, "medium");
        }

        private static void TankSymbol(Pen p, SketchObject o)
        {
            p.Line(-6, -12, -6, -5, St.Sym, EnTags.Lead);
            p.Rect(-9.5, -5, -2.5, 9, St.SymWhite, "vessel", 3.5);
            p.Text(-6, 2, "H₂", 2.6, 5.5, 2.6, TextAnchor.Middle, false, null, "medium");
        }

        internal static string Inv(double v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
