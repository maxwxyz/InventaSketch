using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// Hydropower: a hydro power plant (reservoir, dam, penstock, powerhouse with turbine and generator, tailwater) and a
    /// pumped storage plant (upper and lower reservoir, reversible machine). Drawn independently with plain primitives.
    /// </summary>
    internal static class Hydro
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly ParamDef HydroPower = Pm.Num("power", "Rated power", "Nennleistung", 10, 0.001, 25000, "MW", 1);
        internal static readonly ParamDef PumpedPower = Pm.Num("power", "Rated power (generating)", "Nennleistung (Turbinenbetrieb)", 300, 1, 10000, "MW", 10);
        internal static readonly ParamDef PumpedCapacity = Pm.Num("capacity", "Storage capacity", "Speicherkapazität", 2400, 1, 1000000, "MWh", 100);

        private const string WaterFill = "#81d4fa";
        private const string Earth = "#bcaaa4";

        public static IEnumerable<EnergyType> Types()
        {
            yield return new EnergyType("energy.hydro.turbine", T("Hydro power plant", "Wasserkraftanlage"), EnCategories.Generation,
                new[]
                {
                    "hydro", "hydropower", "hydroelectric", "water turbine", "run-of-river", "dam", "renewable", "generator",
                    "wasserkraft", "wasserkraftwerk", "wasserkraftanlage", "laufwasserkraftwerk", "talsperre", "wasserturbine", "erneuerbar", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(40, 30, HydroPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -15), new Port("water", -20, 4), new Port("tail", 20, 10)),
                    LabelBox = ViewSpec.LabelAt(9.8, -14, 19.8, 7),
                },
                ViewSpec.SingleLine(38, 24, HydroSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -12), new Port("water", -19, 4), new Port("tail", 19, 10)),
                    LabelBox = ViewSpec.LabelAt(6.5, -11, 19, 1.5),
                })
            {
                Params = new[] { HydroPower },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(HydroPower) * 1e6, "W", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.transformer", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[] { Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), HydroPower, l) },
            };

            yield return new EnergyType("energy.storage.pumped", T("Pumped storage plant", "Pumpspeicherkraftwerk"), EnCategories.Storage,
                new[]
                {
                    "pumped storage", "pumped hydro", "phs", "upper reservoir", "lower reservoir", "storage", "hydro",
                    "pumpspeicher", "pumpspeicherkraftwerk", "pumpspeicherwerk", "oberbecken", "unterbecken", "speicher", "wasserkraft", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(44, 30, PumpedPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -15)), LabelBox = ViewSpec.LabelAt(2, -14, 21.5, -1),
                },
                ViewSpec.SingleLine(38, 26, PumpedSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -13)), LabelBox = ViewSpec.LabelAt(6, -12.5, 19, -2),
                })
            {
                Params = new[] { PumpedPower, PumpedCapacity },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(PumpedPower) * 1e6, "W", l), Fmt.Si(o.Clamped(PumpedCapacity) * 1e6, "Wh", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.transformer", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), PumpedPower, l),
                    Act.Number(o, "energy.capacity.set", T("Storage capacity…", "Speicherkapazität…"), PumpedCapacity, l),
                },
            };
        }

        // ------------------------------------------------------------------ hydro power plant

        private static void HydroPictorial(Pen p, SketchObject o)
        {
            var water = St.Fill(WaterFill) with { Stroke = Pal.Water };
            p.Line(0, -15, 0, -8, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Line(-20, 4, -16.6, 4, St.Stroke(Pal.Water, 0.6), EnTags.Lead);
            p.Line(17, 10, 20, 10, St.Stroke(Pal.Water, 0.6), EnTags.Lead);
            // Reservoir behind the dam, tailwater below the powerhouse.
            p.Poly(water, true, "reservoir", -17, -6, -12, -6, -9.6, 12, -17, 12);
            p.Rect(-5.5, 9, 17, 12, water, "tailwater");
            p.Poly(St.Fill(Pal.Concrete), true, "dam", -12, -9, -9, -9, -5.5, 12, -9.6, 12);
            p.Line(-10.4, 0.5, -2.2, 8, St.Stroke(Pal.Steel, 1.1), "penstock");
            p.Rect(-5, -8, 9, 6, St.Fill(Pal.Light), "powerhouse", 0.6);
            p.Line(0, 1.4, 0, 6.6, St.Stroke(Pal.Ink, 0.6), "shaft");
            p.Round(0, -2, 3.4, St.Fill(Pal.White), "generator");
            p.Text(0, -2, "G", 3, 4, 3, TextAnchor.Middle, true, Pal.Ink, "letter");
            p.Round(0, 8.6, 2.4, St.Fill(Pal.Steel), "turbine");
            for (var i = 0; i < 3; i++)
            {
                var d = Vec2.Polar(2.1, Geo.DegToRad(60 * i + 15));
                p.Add(Prim.Line(p.V(0, 8.6) + d * p.K, p.V(0, 8.6) - d * p.K, St.Detail(Pal.Ink, 0.2)), "runner");
            }
            p.Line(-17, 12, 17, 12, St.Stroke(Pal.Ink, 0.35), "ground");
        }

        private static void HydroSymbol(Pen p, SketchObject o)
        {
            // Generator above, turbine below on one shaft; headwater in from the left, tailwater out to the right.
            var g = Generation.Generator(p, 0, -4, -12, 4.2);
            p.Add(Prim.Line(g.V(0, 4.2), p.V(0, 2.5), St.Sym), "shaft");
            p.Line(-19, 4, -4.5, 4, St.Thin, EnTags.Lead);
            p.Line(4.5, 10, 19, 10, St.Thin, EnTags.Lead);
            p.Rect(-4.5, 2.5, 4.5, 11, St.SymWhite, "turbine");
            p.Circle(0, 6.75, 2.6, St.Thin, "runner");
            for (var i = 0; i < 3; i++)
            {
                var d = Vec2.Polar(2.6, Geo.DegToRad(60 * i + 30));
                p.Line(d.X, 6.75 + d.Y, -d.X, 6.75 - d.Y, St.Thin, "runner");
            }
        }

        // ------------------------------------------------------------------ pumped storage

        private static void PumpedPictorial(Pen p, SketchObject o)
        {
            var water = St.Fill(WaterFill) with { Stroke = Pal.Water };
            p.Line(0, -15, 0, 1, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            // Hill with the upper reservoir, lower reservoir in the valley.
            p.Poly(St.Fill(Earth), true, "hill", -22, -5, -12, -5, -3, 12, -22, 12);
            p.Poly(water, true, "upper-reservoir", -21, -8.5, -12, -8.5, -12.6, -5, -20.4, -5);
            p.Poly(water, true, "lower-reservoir", 7, 7.5, 21, 7.5, 20, 12, 8, 12);
            p.Line(-12.3, -6.4, -5, 3, St.Stroke(Pal.Steel, 1.1), "penstock");
            p.Line(5, 6.5, 7.6, 8.4, St.Stroke(Pal.Steel, 0.8), "tailrace");
            // Generating (down) and pumping (up) along the penstock.
            p.Arrow(-9.4, -5.6, -5.4, -0.5, 1.3, 0.55, St.Stroke(Pal.Water, 0.3), "generating");
            p.Arrow(-4.1, -1.4, -8.1, -6.6, 1.3, 0.55, St.Stroke(Pal.Water, 0.3), "pumping");
            p.Rect(-5, 1, 5, 9, St.Fill(Pal.Light), "powerhouse", 0.5);
            p.Round(0, 5, 2.8, St.Fill(Pal.White), "machine");
            p.Text(0, 5, "M/G", 1.6, 4.6, 1.6, TextAnchor.Middle, true, Pal.Ink, "letter");
            p.Line(-22, 12, 22, 12, St.Stroke(Pal.Ink, 0.35), "ground");
        }

        private static void PumpedSymbol(Pen p, SketchObject o)
        {
            // Reversible machine (motor/generator) between the upper reservoir (left, high) and the lower one (right, low).
            var u = p.Uniform(0, -4);
            p.Add(Prim.Line(p.V(0, -13), u.V(0, -4.2), St.Sym), EnTags.Lead);
            p.Poly(St.Sym, false, "upper-reservoir", -16, -3, -16, 2, -9, 2, -9, -3);
            p.Line(-15.4, -1.2, -9.6, -1.2, St.Thin, "water");
            p.Poly(St.Sym, false, "lower-reservoir", 9, 5, 9, 10, 16, 10, 16, 5);
            p.Line(9.6, 6.8, 15.4, 6.8, St.Thin, "water");
            p.Line(-9, 0.5, 0, 0.5, St.Sym, "penstock");
            p.Line(0, 0.5, 0, 7.5, St.Sym, "penstock");
            p.Line(0, 7.5, 9, 7.5, St.Sym, "tailrace");
            p.Arrow(-8.4, -0.9, -5, -0.9, 1, 0.45, St.Thin, "generating");
            p.Arrow(-5, 1.9, -8.4, 1.9, 1, 0.45, St.Thin, "pumping");
            u.Circle(0, 0, 4.2, St.SymWhite, "body");
            u.Text(0, -0.6, "M/G", 2.2, 6.4, 2.2, TextAnchor.Middle, false, null, "letter");
        }
    }
}
