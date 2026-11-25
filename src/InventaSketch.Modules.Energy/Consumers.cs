using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>Consumers and e-mobility: charger (wallbox, charging station), electric vehicle, load, energy management.</summary>
    internal static class Consumers
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly ParamDef ChargePower = Pm.Num("power", "Charging power", "Ladeleistung", 11, 1, 1000, "kW", 1);
        internal static readonly ParamDef ChargerForm = Pm.Choice("form", "Design", "Bauform", "wallbox",
            ("wallbox", "Wallbox", "Wallbox"), ("pillar", "Charging station (pillar)", "Ladesäule"));
        internal static readonly ParamDef Dc = Pm.Bool("dc", "DC charging", "DC-Laden", false);
        internal static readonly ParamDef EvBattery = Pm.Num("battery", "Battery capacity", "Batteriekapazität", 60, 1, 2000, "kWh", 5);
        internal static readonly ParamDef LoadKind = Pm.Choice("kind", "Consumer", "Verbraucher", "house",
            ("house", "Single-family house", "Einfamilienhaus"), ("apartment", "Apartment building", "Mehrfamilienhaus"),
            ("commercial", "Commercial building", "Gewerbegebäude"), ("industry", "Industry", "Industrie"));
        internal static readonly ParamDef LoadPower = Pm.Num("power", "Peak load", "Spitzenlast", 10, 0.1, 10000000, "kW", 1);

        public static IEnumerable<EnergyType> Types()
        {
            yield return new EnergyType("energy.mobility.charger", T("EV charger", "Ladestation"), EnCategories.Consumers,
                new[]
                {
                    "charger", "ev charger", "wallbox", "charging station", "charging point", "e-mobility", "electric vehicle", "dc fast charger",
                    "ladestation", "ladesäule", "ladepunkt", "ladeinfrastruktur", "e-mobilität", "schnelllader", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(22, 30, ChargerPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -15), new Port("ev", 0, 15)),
                    LabelBox = o => o.S(ChargerForm) == "pillar" ? new Box(-4.5, -4.5, 4.5, 1) : new Box(-7, 0, 7, 4.3),
                    LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(38, 24, ChargerSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -12), new Port("ev", 0, 12)), LabelBox = ViewSpec.LabelAt(7, -6, 19, 6),
                })
            {
                Params = new[] { ChargePower, ChargerForm, Dc },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(ChargePower) * 1000, "W", l) + (o.B(Dc) ? " DC" : " AC") },
                Uniform = false,
                CompanionIds = new[] { "energy.mobility.ev", "energy.grid.meter", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Cycle(o, "energy.form.next", ChargerForm),
                    Act.Toggle(o, "energy.dc.toggle", T("DC charging", "DC-Laden"), Dc),
                    Act.Number(o, "energy.power.set", T("Charging power…", "Ladeleistung…"), ChargePower, l),
                },
            };

            yield return new EnergyType("energy.mobility.ev", T("Electric vehicle", "Elektrofahrzeug"), EnCategories.Consumers,
                new[]
                {
                    "ev", "electric vehicle", "electric car", "bev", "e-mobility", "vehicle to grid", "v2g",
                    "elektrofahrzeug", "elektroauto", "e-auto", "e-mobilität", "bidirektionales laden",
                },
                ViewSpec.Pictorial(40, 20, EvPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("charge", -15, -10)), LabelBox = ViewSpec.LabelAt(-7.2, -1.2, 7.2, 4.4), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(40, 24, EvSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("charge", -15, -12)), LabelBox = ViewSpec.LabelAt(-8.5, -5, 20, 5),
                })
            {
                Params = new[] { EvBattery },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(EvBattery) * 1000, "Wh", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.mobility.charger", "energy.line.cable" },
                Extra = (o, l) => new[] { Act.Number(o, "energy.capacity.set", T("Battery capacity…", "Batteriekapazität…"), EvBattery, l) },
            };

            yield return new EnergyType("energy.load.building", T("Consumer (load)", "Verbraucher (Last)"), EnCategories.Consumers,
                new[]
                {
                    "load", "consumer", "house", "building", "household", "apartment", "commercial", "industry", "factory", "demand",
                    "verbraucher", "last", "haus", "gebäude", "haushalt", "mehrfamilienhaus", "gewerbe", "industrie", "fabrik", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(28, 30, LoadPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -15)), LabelBox = ViewSpec.LabelAt(-14, 9.6, 14, 15), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(30, 24, LoadSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -12)), LabelBox = ViewSpec.LabelAt(3.5, -6, 15, 6),
                })
            {
                Params = new[] { LoadKind, LoadPower },
                Rating = (o, l) => new[] { KindShort(o.S(LoadKind), l), Fmt.Si(o.Clamped(LoadPower) * 1000, "W", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.meter", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[] { Act.Cycle(o, "energy.kind.next", LoadKind), Act.Number(o, "energy.power.set", T("Peak load…", "Spitzenlast…"), LoadPower, l) },
            };

            yield return new EnergyType("energy.control.ems", T("Energy management system", "Energiemanagementsystem"), EnCategories.Consumers,
                new[]
                {
                    "ems", "energy management", "controller", "smart home", "hems", "monitoring",
                    "energiemanagement", "energiemanagementsystem", "steuerung", "regler", "smart meter gateway",
                },
                ViewSpec.Pictorial(26, 22, EmsPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("data", 0, 11)), LabelBox = ViewSpec.LabelAt(1.5, 8.4, 13, 11),
                },
                ViewSpec.SingleLine(38, 16, EmsSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("data", 0, 8)), LabelBox = ViewSpec.LabelAt(9.5, -5, 19, 5),
                })
            {
                Uniform = false,
                CompanionIds = new[] { "energy.conv.inverter", "energy.storage.battery", "energy.mobility.charger", "energy.line.cable" },
            };
        }

        private static string KindShort(string kind, Lang l) => kind switch
        {
            "apartment" => l == Lang.De ? "Wohnungen" : "Apartments",
            "commercial" => l == Lang.De ? "Gewerbe" : "Commercial",
            "industry" => l == Lang.De ? "Industrie" : "Industry",
            _ => l == Lang.De ? "Haus" : "House",
        };

        // ------------------------------------------------------------------ charger

        private static void Plug(Pen p, double cx, double cy, double r, bool pictorial)
        {
            if (pictorial)
            {
                p.Round(cx, cy, r, St.Fill(Pal.Dark), "plug");
                p.Dot(cx - r * 0.35, cy - r * 0.2, r * 0.16, Pal.White, "pin");
                p.Dot(cx + r * 0.35, cy - r * 0.2, r * 0.16, Pal.White, "pin");
                p.Dot(cx, cy + r * 0.4, r * 0.16, Pal.White, "pin");
                return;
            }
            p.Round(cx, cy, r, St.Sym, "plug");
            p.Dot(cx - r * 0.38, cy - r * 0.22, r * 0.15);
            p.Dot(cx + r * 0.38, cy - r * 0.22, r * 0.15);
            p.Dot(cx, cy + r * 0.42, r * 0.15);
        }

        private static void ChargerPictorial(Pen p, SketchObject o)
        {
            var stub = St.Stroke(Pal.Dark, 0.5);
            var cable = St.Stroke(Pal.Dark, 0.6);
            if (o.S(ChargerForm) == "pillar")
            {
                // The tethered cable hangs in front of the column to a connector at the bottom centre.
                p.Line(0, -15, 0, -13, stub, EnTags.Lead);
                p.Rect(-7, 12, 7, 13.4, St.Fill(Pal.Dark), "base");
                p.Rect(-5.5, -13, 5.5, 12, St.Fill(Pal.Light), "column", 1);
                p.Rect(-4, -11, 4, -6, St.Fill(Pal.Dark), "screen", 0.3);
                p.Line(-2.6, -9.6, 2.6, -9.6, St.Detail(Pal.Green, 0.4), "screen");
                Plug(p, 0, 5, 2.2, true);
                p.Path(new[] { PathSeg.Move(new Vec2(0, 7.2)), PathSeg.Cubic(new Vec2(3.5, 9), new Vec2(-3.5, 10.5), new Vec2(0, 12.6)) }, cable, "cable");
                p.Rect(-1.3, 12.6, 1.3, 14.4, St.Fill(Pal.Dark), "connector", 0.4);
                p.Line(0, 14.4, 0, 15, stub, EnTags.Lead);
                return;
            }
            p.Line(0, -15, 0, -11, stub, EnTags.Lead);
            p.Rect(-8, -11, 8, 5, St.Fill(Pal.Light), "body", 2);
            p.Round(0, -4.5, 3.4, St.Stroke(Pal.Green, 0.6), "ring");
            Plug(p, 0, -4.5, 2, true);
            p.Path(new[] { PathSeg.Move(new Vec2(3, 5)), PathSeg.Cubic(new Vec2(6, 10), new Vec2(-6, 8), new Vec2(0, 11.5)) }, cable, "cable");
            p.Rect(-1.3, 11.5, 1.3, 14, St.Fill(Pal.Dark), "connector", 0.4);
            p.Line(0, 14, 0, 15, stub, EnTags.Lead);
        }

        private static void ChargerSymbol(Pen p, SketchObject o)
        {
            const double cx = 0;
            p.Line(cx, -12, cx, -5, St.Sym, EnTags.Lead);
            p.Line(cx, 5, cx, 12, St.Sym, EnTags.Lead);
            p.Rect(cx - 5, -5, cx + 5, 5, St.SymWhite, "body");
            Plug(p, cx, 0, 2.8, false);
        }

        // ------------------------------------------------------------------ EV

        private static void EvPictorial(Pen p, SketchObject o)
        {
            p.Line(-15, -10, -15, -0.9, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Poly(St.Fill("#90caf9"), true, "body",
                -18, 5, -18.4, 1.6, -17.6, 0, -15.5, -0.8, -9.2, -2.1, -5.4, -6.4, 4.6, -6.4, 9.6, -2.3, 15.8, -1.4, 18, 0.4, 18.4, 3, 18, 5);
            p.Poly(St.Fill("#e3f2fd"), true, "window", -4.6, -5.6, -0.6, -5.6, -0.6, -2.5, -7.7, -2.5);
            p.Poly(St.Fill("#e3f2fd"), true, "window", 0.6, -5.6, 4.2, -5.6, 7.7, -2.5, 0.6, -2.5);
            p.Rect(-16.1, -0.6, -13.9, 1.3, St.Fill(Pal.Dark), "flap", 0.3);
            foreach (var x in new[] { -11.0, 11.0 })
            {
                p.Round(x, 5.2, 3.2, St.Fill(Pal.Dark), "wheel");
                p.Round(x, 5.2, 1.3, St.Fill(Pal.Steel), "rim");
            }
        }

        private static void EvSymbol(Pen p, SketchObject o)
        {
            // Same x as the pictorial charge port (above the rear of the car).
            const double cx = -15;
            var u = p.Uniform(cx, 0);
            p.Add(Prim.Line(p.V(cx, -12), u.V(0, -5), St.Sym), EnTags.Lead);
            u.Circle(0, 0, 5, St.SymWhite, "body");
            u.Text(0, 0, "EV", 3, 6.5, 3, TextAnchor.Middle, true, null, "letter");
        }

        // ------------------------------------------------------------------ load

        private static void LoadPictorial(Pen p, SketchObject o)
        {
            var stub = St.Stroke(Pal.Dark, 0.5);
            switch (o.S(LoadKind))
            {
                case "apartment":
                    p.Line(0, -15, 0, -11, stub, EnTags.Lead);
                    p.Rect(-8, -11, 8, 9, St.Fill(Pal.Light), "wall");
                    for (var r = 0; r < 3; r++)
                        for (var c = 0; c < 3; c++)
                            p.Rect(-6.5 + c * 5, -9.5 + r * 4, -3.5 + c * 5, -7 + r * 4, St.Fill(Pal.Sky), "window");
                    p.Rect(-6.5, 2.5, -3.5, 5, St.Fill(Pal.Sky), "window");
                    p.Rect(3.5, 2.5, 6.5, 5, St.Fill(Pal.Sky), "window");
                    p.Rect(-1.5, 4.5, 1.5, 9, St.Fill(Pal.Wood), "door");
                    break;
                case "commercial":
                    p.Line(0, -15, 0, -12, stub, EnTags.Lead);
                    p.Rect(-7, -12, 7, 9, St.Fill("#cfd8dc"), "wall");
                    for (var i = 0; i < 5; i++) p.Rect(-6, -10.5 + i * 3.3, 6, -8.5 + i * 3.3, St.Fill(Pal.Sky), "window");
                    p.Rect(-2, 6, 2, 9, St.Fill(Pal.Dark), "door");
                    break;
                case "industry":
                    p.Line(0, -15, 0, -7, stub, EnTags.Lead);
                    p.Rect(7.5, -11, 10, -2, St.Fill(Pal.Roof), "chimney");
                    p.Rect(-12, -2, 12, 9, St.Fill(Pal.Light), "wall");
                    p.Poly(St.Fill(Pal.Steel), true, "roof", -12, -2, -12, -7, -6, -2, -6, -7, 0, -2, 0, -7, 6, -2);
                    p.Rect(-4, 3, 4, 9, St.Fill(Pal.Steel), "gate");
                    for (var i = 1; i < 4; i++) p.Line(-4, 3 + i * 1.5, 4, 3 + i * 1.5, St.Detail(Pal.Ink), "gate");
                    p.Rect(-10, 0, -6, 2, St.Fill(Pal.Sky), "window");
                    p.Rect(6, 0, 10, 2, St.Fill(Pal.Sky), "window");
                    break;
                default:
                    p.Line(0, -15, 0, -12, stub, EnTags.Lead);
                    p.Rect(5, -9.5, 7, -6, St.Fill(Pal.Roof), "chimney");
                    p.Rect(-9, -3, 9, 9, St.Fill(Pal.Light), "wall");
                    p.Poly(St.Fill(Pal.Roof), true, "roof", -11, -2.6, 0, -12, 11, -2.6);
                    p.Rect(-1.8, 3.5, 1.8, 9, St.Fill(Pal.Wood), "door");
                    p.Rect(-7, -0.5, -3.5, 3, St.Fill(Pal.Sky), "window");
                    p.Rect(3.5, -0.5, 7, 3, St.Fill(Pal.Sky), "window");
                    break;
            }
        }

        private static void LoadSymbol(Pen p, SketchObject o)
        {
            const double cx = 0;
            p.Line(cx, -12, cx, 5, St.Sym, EnTags.Lead);
            p.Head(cx, 8.5, cx, 4.5, 4, 1.6, null, "arrow");
        }

        // ------------------------------------------------------------------ EMS

        private static void EmsPictorial(Pen p, SketchObject o)
        {
            p.Line(0, 8, 0, 11, St.Data with { Stroke = Pal.Dark }, EnTags.Lead);
            p.Rect(-10, -8, 10, 8, St.Fill(Pal.Metal), "body", 1.5);
            p.Rect(-8, -6, 8, 2.5, St.Fill(Pal.Dark), "screen", 0.4);
            var heights = new[] { 3.0, 5.0, 4.0, 6.5 };
            for (var i = 0; i < heights.Length; i++)
            {
                var x = -6.5 + i * 3;
                p.Rect(x, 1.5 - heights[i], x + 2, 1.5, St.Solid(i % 2 == 0 ? Pal.Amber : Pal.Green), "bar");
            }
            p.Text(0, 5.2, "EMS", 2.4, 8, 2.4, TextAnchor.Middle, true, Pal.Ink, "letter");
        }

        private static void EmsSymbol(Pen p, SketchObject o)
        {
            p.Line(0, 5, 0, 8, St.Data, EnTags.Lead);
            p.Rect(-8, -5, 8, 5, St.SymWhite, "body");
            p.Text(0, 0, "EMS", 3.4, 12, 3.4, TextAnchor.Middle, true, null, "letter");
        }
    }
}
