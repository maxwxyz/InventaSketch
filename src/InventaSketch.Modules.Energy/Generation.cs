using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>Generation: PV module, string and array, wind turbine, generator set.</summary>
    internal static class Generation
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly ParamDef ModulePower = Pm.Num("power", "Module power", "Modulleistung", 430, 10, 1000, "Wp", 5);
        internal static readonly ParamDef Modules = Pm.Int("modules", "Modules in the string", "Module im String", 6, 2, 24);
        internal static readonly ParamDef ArrayPower = Pm.Num("power", "Peak power", "Spitzenleistung", 10, 0.1, 1000000, "kWp", 1);
        internal static readonly ParamDef Rows = Pm.Int("rows", "Rows", "Reihen", 2, 1, 6);
        internal static readonly ParamDef Columns = Pm.Int("columns", "Modules per row", "Module je Reihe", 5, 1, 12);
        internal static readonly ParamDef WindPower = Pm.Num("power", "Rated power", "Nennleistung", 3, 0.01, 30, "MW", 0.1);
        internal static readonly ParamDef Rotor = Pm.Num("rotor", "Rotor position", "Rotorstellung", 0, -360, 360, "°", 5);
        internal static readonly ParamDef GensetPower = Pm.Num("power", "Rated power", "Nennleistung", 100, 1, 100000, "kVA", 5);
        internal static readonly ParamDef Fuel = Pm.Choice("fuel", "Fuel", "Kraftstoff", "diesel",
            ("diesel", "Diesel", "Diesel"), ("gas", "Natural gas", "Erdgas"), ("biogas", "Biogas", "Biogas"), ("h2", "Hydrogen", "Wasserstoff"));

        private static readonly string[] PvKeywords =
        {
            "pv", "photovoltaic", "solar", "solar panel", "solar module", "renewable", "generation",
            "photovoltaik", "solarmodul", "solaranlage", "erneuerbar", "erzeugung", "einlinienschaltbild",
        };

        public static IEnumerable<EnergyType> Types()
        {
            yield return new EnergyType("energy.pv.module", T("PV module", "PV-Modul"), EnCategories.Generation,
                PvKeywords.Concat(new[] { "module", "panel", "modul", "paneel" }).ToArray(),
                ViewSpec.Pictorial(20, 34, ModulePictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", 0, -17)), LabelBox = ViewSpec.LabelAt(-10, 11.8, 10, 17), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(26, 22, (p, o) => PvSymbol(p, -6, -11, false)) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", -6, -11)), LabelBox = ViewSpec.LabelAt(3, -5, 13, 7.5),
                })
            {
                Params = new[] { ModulePower },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(ModulePower), "Wp", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.conv.inverter", "energy.pv.string", "energy.line.cable" },
                Extra = (o, l) => new[] { Act.Number(o, "energy.power.set", T("Module power…", "Modulleistung…"), ModulePower, l) },
            };

            yield return new EnergyType("energy.pv.string", T("PV string", "PV-String"), EnCategories.Generation,
                PvKeywords.Concat(new[] { "string", "strang", "modulstrang", "series" }).ToArray(),
                ViewSpec.Pictorial(50, 26, StringPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", 0, -13)), LabelBox = ViewSpec.LabelAt(-25, 7.2, 25, 13), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(26, 22, (p, o) => PvSymbol(p, -6, -11, false, o.I(Modules))) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", -6, -11)), LabelBox = ViewSpec.LabelAt(3, -5, 13, 7.5),
                })
            {
                Params = new[] { Modules, ModulePower },
                Rating = (o, l) => new[]
                {
                    o.I(Modules).ToString(System.Globalization.CultureInfo.InvariantCulture) + " × " + Fmt.Si(o.Clamped(ModulePower), "Wp", l),
                    Fmt.Si(o.I(Modules) * o.Clamped(ModulePower), "Wp", l),
                },
                Uniform = false,
                CompanionIds = new[] { "energy.conv.inverter", "energy.pv.array", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Step(o, "energy.modules.more", T("One module more", "Ein Modul mehr"), Modules, 1),
                    Act.Step(o, "energy.modules.fewer", T("One module fewer", "Ein Modul weniger"), Modules, -1),
                },
            };

            yield return new EnergyType("energy.pv.array", T("PV array", "PV-Generator (Feld)"), EnCategories.Generation,
                PvKeywords.Concat(new[] { "array", "field", "solar park", "solar farm", "ground-mounted", "feld", "solarpark", "freifläche", "pv-anlage", "pv-generator" }).ToArray(),
                ViewSpec.Pictorial(50, 34, ArrayPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", 0, -17)), LabelBox = ViewSpec.LabelAt(-25, 9, 25, 17), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(26, 22, (p, o) => PvSymbol(p, -6, -11, true)) with
                {
                    Ports = ViewSpec.Fixed(new Port("dc", -6, -11)), LabelBox = ViewSpec.LabelAt(3, -5, 13, 7.5),
                })
            {
                Params = new[] { ArrayPower, Rows, Columns },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(ArrayPower) * 1000, "Wp", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.conv.inverter", "energy.grid.transformer", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Number(o, "energy.power.set", T("Peak power…", "Spitzenleistung…"), ArrayPower, l),
                    Act.Step(o, "energy.rows.more", T("One row more", "Eine Reihe mehr"), Rows, 1),
                    Act.Step(o, "energy.rows.fewer", T("One row fewer", "Eine Reihe weniger"), Rows, -1),
                },
            };

            yield return new EnergyType("energy.wind.turbine", T("Wind turbine", "Windenergieanlage"), EnCategories.Generation,
                new[]
                {
                    "wind", "wind turbine", "wind power", "wind farm", "renewable", "generator", "rotor", "onshore", "offshore",
                    "windrad", "windkraft", "windkraftanlage", "windenergieanlage", "windpark", "wea", "erneuerbar", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(30, 44, WindPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, 22)), LabelBox = ViewSpec.LabelAt(2.5, 8.5, 15, 18.5),
                },
                ViewSpec.SingleLine(26, 24, WindSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", -6, 12)), LabelBox = ViewSpec.LabelAt(1.5, -6, 13, 6),
                })
            {
                Params = new[] { WindPower, Rotor },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(WindPower) * 1e6, "W", l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.transformer", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), WindPower, l),
                    new ContextAction("energy.rotor.turn", T("Turn rotor by 30°", "Rotor um 30° drehen"), IconIds.Rotate,
                        x => x.WithParam(Rotor.Key, Geo.NormDeg(x.N(Rotor) + 30))) { Group = "param", ParamKeys = new[] { Rotor.Key } },
                },
                Frames = (o, count) => Enumerable.Range(0, count)
                    .Select(i => o.WithParam(Rotor.Key, Geo.NormDeg(o.N(Rotor) + 120.0 * i / count))).ToList(),
            };

            yield return new EnergyType("energy.gen.genset", T("Generator set (genset)", "Stromaggregat"), EnCategories.Generation,
                new[]
                {
                    "genset", "generator set", "diesel generator", "backup power", "emergency power", "standby", "generator",
                    "stromaggregat", "notstrom", "notstromaggregat", "netzersatzanlage", "nea", "dieselgenerator", "einlinienschaltbild",
                },
                ViewSpec.Pictorial(36, 24, GensetPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", 0, -12)), LabelBox = ViewSpec.LabelAt(3.5, -5, 15, 8),
                },
                ViewSpec.SingleLine(26, 24, (p, o) => Generator(p, -6, 0, -12)) with
                {
                    Ports = ViewSpec.Fixed(new Port("ac", -6, -12)), LabelBox = ViewSpec.LabelAt(1.5, -6, 13, 6),
                })
            {
                Params = new[] { GensetPower, Fuel },
                Rating = (o, l) => new[] { Fmt.Si(o.Clamped(GensetPower) * 1000, "VA", l), Pm.OptionLabel(Fuel, o.S(Fuel)).Get(l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.breaker", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[] { Act.Cycle(o, "energy.fuel.next", Fuel), Act.Number(o, "energy.power.set", T("Rated power…", "Nennleistung…"), GensetPower, l) },
            };
        }

        // ------------------------------------------------------------------ PV

        /// <summary>Framed panel with glass and a cell grid.</summary>
        internal static void Panel(Pen p, double x0, double y0, double x1, double y1, int cols, int rows)
        {
            p.Rect(x0, y0, x1, y1, St.Fill(Pal.Frame), "frame", 0.3);
            var i = Math.Min(0.6, Math.Min(x1 - x0, y1 - y0) * 0.08);
            double gx0 = x0 + i, gy0 = y0 + i, gx1 = x1 - i, gy1 = y1 - i;
            p.Rect(gx0, gy0, gx1, gy1, St.Solid(Pal.Glass), "glass");
            var grid = St.Detail(Pal.Cell, 0.15);
            if ((gx1 - gx0) / cols >= 1.0)
                for (var c = 1; c < cols; c++) { var x = gx0 + (gx1 - gx0) * c / cols; p.Line(x, gy0, x, gy1, grid, "cells"); }
            if ((gy1 - gy0) / rows >= 1.0)
                for (var r = 1; r < rows; r++) { var y = gy0 + (gy1 - gy0) * r / rows; p.Line(gx0, y, gx1, y, grid, "cells"); }
        }

        private static void ModulePictorial(Pen p, SketchObject o)
        {
            p.Line(0, -17, 0, -13, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            Panel(p, -8, -13, 8, 11, 4, 6);
            p.Rect(-1.2, -13.6, 1.2, -12.4, St.Solid(Pal.Dark), "junction-box");
        }

        private static void StringPictorial(Pen p, SketchObject o)
        {
            var n = o.I(Modules);
            const double x0 = -24, x1 = 24, y0 = -7, y1 = 6;
            var w = (x1 - x0) / n;
            var gap = Math.Min(1.0, w * 0.15);
            var cable = St.Stroke(Pal.Dark, 0.45);
            double First(int i) => x0 + i * w + gap / 2;
            for (var i = 0; i < n; i++)
            {
                var a = First(i);
                Panel(p, a, y0, a + w - gap, y1, w - gap >= 4 ? 2 : 1, 4);
                if (i > 0) p.Line(a - gap - 0.1, -0.5, a + 0.1, -0.5, St.Stroke(Pal.Dark, 0.3), "connector");
            }
            var c0 = First(0) + (w - gap) / 2;
            var c1 = First(n - 1) + (w - gap) / 2;
            p.Line(0, -13, 0, -10, cable, EnTags.Lead);
            p.Line(c0, -10, c1, -10, cable, EnTags.Lead);
            p.Line(c0, -10, c0, y0, cable, EnTags.Lead);
            p.Line(c1, -10, c1, y0, cable, EnTags.Lead);
            p.Dot(0, -10, 0.45, Pal.Dark, "split");
        }

        private static void ArrayPictorial(Pen p, SketchObject o)
        {
            int rows = o.I(Rows), cols = o.I(Columns);
            const double top = -12, height = 20, left = -23, right = 23;
            var hr = height / rows;
            var bh = hr * 0.62;
            var sk = bh * 0.45;
            var w = (right - left - sk) / cols;
            var gap = Math.Min(0.6, w * 0.1);
            var leg = St.Stroke(Pal.Ink, 0.3);
            p.Line(0, -17, 0, top, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            for (var r = 0; r < rows; r++)
            {
                var y0 = top + r * hr;
                // Legs first so the panels lie on top.
                p.Line(left + 1.5, y0 + bh * 0.8, left + 1.5, y0 + hr * 0.92, leg, "leg");
                p.Line(right - sk - 1.5, y0 + bh * 0.8, right - sk - 1.5, y0 + hr * 0.92, leg, "leg");
                p.Line(left + sk + 1.5, y0 + bh * 0.3, left + sk + 1.5, y0 + hr * 0.8, leg, "leg");
                p.Line(right - 1.5, y0 + bh * 0.3, right - 1.5, y0 + hr * 0.8, leg, "leg");
                for (var c = 0; c < cols; c++)
                {
                    var a = left + c * w + gap / 2;
                    var b = a + w - gap;
                    p.Poly(St.Fill(Pal.Glass) with { Stroke = Pal.Frame, StrokeWidth = 0.25 }, true, "panel", a + sk, y0, b + sk, y0, b, y0 + bh, a, y0 + bh);
                    p.Line(a + sk / 2, y0 + bh / 2, b + sk / 2, y0 + bh / 2, St.Detail(Pal.Cell), "cells");
                    if (w > 3) p.Line((a + b) / 2 + sk, y0, (a + b) / 2, y0 + bh, St.Detail(Pal.Cell), "cells");
                }
            }
        }

        /// <summary>
        /// Single-line PV generator symbol (drawn independently): a square with a diagonal, incident light arrows in the
        /// upper triangle and the DC sign in the lower one; <paramref name="stacked"/> adds a second square behind (array),
        /// <paramref name="count"/> &gt; 0 writes the number of modules (string).
        /// </summary>
        internal static void PvSymbol(Pen p, double cx, double portY, bool stacked, int count = 0)
        {
            const double side = 12, top = -4.5;
            var x0 = cx - side / 2;
            if (stacked) p.Rect(x0 + 1.6, top - 1.6, x0 + side + 1.6, top + side - 1.6, St.SymWhite, "stack");
            p.Line(cx, portY, cx, top, St.Sym, EnTags.Lead);
            p.Rect(x0, top, x0 + side, top + side, St.SymWhite, "body");
            p.Line(x0, top + side, x0 + side, top, St.Sym, "diagonal");
            p.Arrow(x0 + 0.8, top + 0.9, x0 + 3.3, top + 3.4, 1.2, 0.5, St.Thin, "light");
            p.Arrow(x0 + 2.9, top + 0.7, x0 + 5.4, top + 3.2, 1.2, 0.5, St.Thin, "light");
            p.DcSign(x0 + 9.5, top + 6.5, 1.4, St.Thin);
            if (count > 0) p.Text(x0 + 7, top + 10.4, count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "×", 2.2, 4, 2.2, TextAnchor.Middle, false, null, "count");
        }

        // ------------------------------------------------------------------ wind

        private static void Blade(Pen p, Vec2 hub, double angleDeg, double len, double chord, Style s)
        {
            var d = Vec2.Polar(1, Geo.DegToRad(angleDeg));
            var n = d.Perp;
            var root = hub + d * (len * 0.1);
            p.PolyD(new[]
            {
                root + n * (chord * 0.45), hub + d * (len * 0.28) + n * chord, hub + d * len, hub + d * (len * 0.28) - n * (chord * 0.4), root - n * (chord * 0.45),
            }, s, true, "blade");
        }

        private static void WindPictorial(Pen p, SketchObject o)
        {
            var rot = o.N(Rotor);
            var hub = new Vec2(0, -9);
            p.Line(-6, 19, 6, 19, St.Stroke(Pal.Ink, 0.35), "ground");
            p.Line(0, 19, 0, 22, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Poly(St.Fill(Pal.Metal), true, "tower", -0.7, -8.5, 0.7, -8.5, 1.6, 19, -1.6, 19);
            p.Rect(-1.7, -10.4, 1.7, -7.6, St.Fill(Pal.Light), "nacelle", 0.6);
            var blade = St.Fill(Pal.White) with { Stroke = "#546e7a" };
            for (var i = 0; i < 3; i++) Blade(p, hub, rot - 90 + 120 * i, 12.5, 1.15, blade);
            p.Round(hub.X, hub.Y, 1.3, St.Fill(Pal.Light), "hub");
        }

        private static void WindSymbol(Pen p, SketchObject o)
        {
            const double cx = -6;
            // The symbol core keeps its shape under non-uniform scaling; only the lead stretches.
            var u = p.Uniform(cx, 0);
            u.Line(0, -2.2, 0, -6.8, St.Sym, "mast");
            var hub = new Vec2(0, -7.4);
            for (var i = 0; i < 3; i++) Blade(u, hub, -90 + 120 * i, 3.8, 0.55, St.Sym);
            u.Dot(hub.X, hub.Y, 0.45);
            p.Add(Prim.Line(u.V(0, 7), p.V(cx, 12), St.Sym), EnTags.Lead);
            u.Circle(0, 2.4, 4.6, St.SymWhite, "body");
            u.Text(0, 1.4, "G", 3.2, 5, 3.2, TextAnchor.Middle, false, null, "letter");
            u.Sine(0, 4.3, 1.5, 0.55, St.Thin);
        }

        // ------------------------------------------------------------------ genset

        private static void GensetPictorial(Pen p, SketchObject o)
        {
            p.Line(0, -12, 0, -7, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Rect(10, -11, 12.4, -7, St.Fill(Pal.Dark), "exhaust");
            p.Line(9.6, -11, 12.8, -11, St.Stroke(Pal.Ink, 0.4), "exhaust");
            p.Rect(-16, -7, 16, 10, St.Fill("#f2c14e"), "enclosure", 0.8);
            for (var i = 0; i < 6; i++) p.Line(-14, -4.5 + i * 1.9, -5, -4.5 + i * 1.9, St.Detail(Pal.Ink, 0.2), "louvre");
            p.Line(2, -6, 2, 9, St.Detail(Pal.Ink, 0.2), "door");
            p.Rect(-17, 10, 17, 11.6, St.Fill(Pal.Dark), "skid");
        }

        /// <summary>
        /// Single-line generator: circle with "G" and "~" (shape kept under non-uniform scaling), lead up to
        /// <paramref name="portY"/>. Returns the uniform pen centred on the circle.
        /// </summary>
        internal static Pen Generator(Pen p, double cx, double cy, double portY, double r = 4.6)
        {
            var u = p.Uniform(cx, cy);
            p.Add(Prim.Line(p.V(cx, portY), u.V(0, -r), St.Sym), EnTags.Lead);
            u.Circle(0, 0, r, St.SymWhite, "body");
            u.Text(0, -r * 0.2, "G", r * 0.72, r * 1.1, r * 0.72, TextAnchor.Middle, false, null, "letter");
            u.Sine(0, r * 0.42, r * 0.33, r * 0.12, St.Thin);
            return u;
        }
    }
}
