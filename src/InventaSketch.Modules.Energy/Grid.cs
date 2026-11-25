using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// Grid connection and single-line diagram elements: grid infeed, transformers, compact substation, meter, busbar,
    /// circuit breaker, disconnector, earthing switch.
    /// </summary>
    internal static class Grid
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly ParamDef GridVoltage = Pm.Num("voltage", "Nominal voltage", "Nennspannung", 20, 0.1, 1000, "kV", 0.1);
        internal static readonly ParamDef Hv = Pm.Num("hv", "High-voltage side", "Oberspannung", 20, 0.1, 1000, "kV", 0.1);
        internal static readonly ParamDef Lv = Pm.Num("lv", "Low-voltage side", "Unterspannung", 0.4, 0.1, 1000, "kV", 0.1);
        internal static readonly ParamDef TrPower = Pm.Num("power", "Rated power", "Bemessungsleistung", 630, 1, 2000000, "kVA", 10);
        internal static readonly ParamDef Vector = Pm.Text("vector", "Vector group", "Schaltgruppe", "");
        internal static readonly ParamDef Hv3 = Pm.Num("hv", "High-voltage winding", "Oberspannungswicklung", 110, 0.1, 1000, "kV", 1);
        internal static readonly ParamDef Mv3 = Pm.Num("mv", "Medium-voltage winding", "Mittelspannungswicklung", 20, 0.1, 1000, "kV", 1);
        internal static readonly ParamDef Lv3 = Pm.Num("lv", "Low-voltage winding", "Unterspannungswicklung", 10, 0.1, 1000, "kV", 1);
        internal static readonly ParamDef Tr3Power = Pm.Num("power", "Rated power", "Bemessungsleistung", 40, 0.1, 5000, "MVA", 1);
        internal static readonly ParamDef StationMv = Pm.Num("hv", "Medium voltage", "Mittelspannung", 20, 0.1, 1000, "kV", 0.1);
        internal static readonly ParamDef Bidirectional = Pm.Bool("bidirectional", "Bidirectional (import/export)", "Zweirichtungszähler (Bezug/Einspeisung)", true);
        internal static readonly ParamDef MeterUnit = Pm.Text("unit", "Unit shown", "Angezeigte Einheit", "kWh");
        internal static readonly ParamDef BusVoltage = Pm.Num("voltage", "Nominal voltage", "Nennspannung", 0.4, 0.01, 1000, "kV", 0.1);
        internal static readonly ParamDef Dc = Pm.Bool("dc", "Direct current", "Gleichstrom", false);
        internal static readonly ParamDef Taps = Pm.Int("taps", "Connection points", "Anschlusspunkte", 5, 2, 12);
        internal static readonly ParamDef Closed = Pm.Bool("closed", "Closed", "Geschlossen", true);
        internal static readonly ParamDef EarthClosed = Pm.Bool("closed", "Closed (earthed)", "Geschlossen (geerdet)", false);
        internal static readonly ParamDef BreakerSymbol = Pm.Choice("symbol", "Symbol", "Darstellung", "contact",
            ("contact", "Switch with breaker cross", "Schalter mit Leistungsschalterkreuz"), ("square", "Square (filled = closed)", "Quadrat (gefüllt = geschlossen)"));
        internal static readonly ParamDef DisconnectorKind = Pm.Choice("kind", "Kind", "Art", "disconnector",
            ("disconnector", "Disconnector", "Trennschalter"), ("loadbreak", "Switch-disconnector (load-break)", "Lasttrennschalter"),
            ("fuse", "Fuse switch-disconnector", "Sicherungslasttrennschalter"));

        private static readonly string[] SldKeywords = { "single-line", "single line diagram", "sld", "einlinienschaltbild", "übersichtsschaltplan", "einpolig" };

        private static string[] Kw(params string[] words) => words.Concat(SldKeywords).ToArray();

        private static bool NameEmpty(SketchObject o) => o.S(Pm.Name).Trim().Length == 0;

        public static IEnumerable<EnergyType> Types()
        {
            yield return new EnergyType("energy.grid.infeed", T("Utility grid (infeed)", "Netzeinspeisung (öffentliches Netz)"), EnCategories.Grid,
                Kw("grid", "utility grid", "grid connection", "infeed", "network", "point of connection", "power line", "pylon",
                    "netz", "stromnetz", "netzanschluss", "netzeinspeisung", "netzverknüpfungspunkt", "hausanschluss", "freileitung", "strommast"),
                ViewSpec.Pictorial(30, 34, InfeedPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("poc", 0, 17)), LabelBox = ViewSpec.LabelAt(6, 8.6, 15, 15.6),
                },
                ViewSpec.SingleLine(38, 18, InfeedSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("poc", 0, 9)), LabelBox = ViewSpec.LabelAt(8.5, -8, 19, 1),
                })
            {
                Params = new[] { GridVoltage },
                Rating = (o, l) => new[] { NameEmpty(o) ? (l == Lang.De ? "Netz" : "Grid") : "", Fmt.Kv(o.Clamped(GridVoltage), l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.transformer", "energy.grid.meter", "energy.grid.busbar" },
                Extra = (o, l) => new[] { Act.Number(o, "energy.voltage.set", T("Nominal voltage…", "Nennspannung…"), GridVoltage, l) },
            };

            yield return new EnergyType("energy.grid.transformer", T("Power transformer", "Leistungstransformator"), EnCategories.Grid,
                Kw("transformer", "power transformer", "distribution transformer", "mv/lv", "voltage", "kva",
                    "transformator", "trafo", "verteiltransformator", "umspanner", "ortsnetztrafo", "spannung"),
                ViewSpec.Pictorial(30, 34, (p, o) => TransformerPictorial(p, o, false)) with
                {
                    Ports = ViewSpec.Fixed(new Port("hv", 0, -17), new Port("lv", 0, 17)), LabelBox = ViewSpec.LabelAt(-6, -2.6, 6, 9.1), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(38, 26, TransformerSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("hv", 0, -13), new Port("lv", 0, 13)), LabelBox = ViewSpec.LabelAt(6.5, -12, 19, 12),
                })
            {
                Params = new[] { Hv, Lv, TrPower, Vector, Pm.LevelColors },
                Rating = (o, l) => new[] { Fmt.Ratio(l, o.Clamped(Hv), o.Clamped(Lv)), PowerLine(o.Clamped(TrPower) * 1000, o, l) },
                RatingSingleLine = (o, l) => new[] { Fmt.Kv(o.Clamped(Hv), l), PowerLine(o.Clamped(TrPower) * 1000, o, l), Fmt.Kv(o.Clamped(Lv), l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.busbar", "energy.grid.breaker", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    VoltagesAction(o, l, Hv, Lv),
                    Act.Number(o, "energy.power.set", T("Rated power…", "Bemessungsleistung…"), TrPower, l),
                    Act.Toggle(o, "energy.levelcolors", T("Colour by voltage level", "Farbe nach Spannungsebene"), Pm.LevelColors),
                },
            };

            yield return new EnergyType("energy.grid.transformer3", T("Three-winding transformer", "Dreiwicklungstransformator"), EnCategories.Grid,
                Kw("three-winding transformer", "transformer", "hv/mv", "substation", "tertiary",
                    "dreiwicklungstransformator", "dreiwicklungstrafo", "transformator", "trafo", "umspannwerk", "tertiärwicklung"),
                ViewSpec.Pictorial(30, 34, (p, o) => TransformerPictorial(p, o, true)) with
                {
                    Ports = ViewSpec.Fixed(new Port("hv", 0, -17), new Port("mv", -2.8, 17), new Port("lv", 2.8, 17)), LabelBox = ViewSpec.LabelAt(-6, -2.6, 6, 9.1), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(38, 28, Transformer3Symbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("hv", 0, -14), new Port("mv", -2.8, 14), new Port("lv", 2.8, 14)), LabelBox = ViewSpec.LabelAt(8, -13, 19, 13),
                })
            {
                Params = new[] { Hv3, Mv3, Lv3, Tr3Power, Vector, Pm.LevelColors },
                Rating = (o, l) => new[] { Fmt.Ratio(l, o.Clamped(Hv3), o.Clamped(Mv3), o.Clamped(Lv3)), PowerLine(o.Clamped(Tr3Power) * 1e6, o, l) },
                RatingSingleLine = (o, l) => new[]
                {
                    Fmt.Kv(o.Clamped(Hv3), l), Fmt.Kv(o.Clamped(Mv3), l) + " / " + Fmt.Kv(o.Clamped(Lv3), l), PowerLine(o.Clamped(Tr3Power) * 1e6, o, l),
                },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.busbar", "energy.grid.breaker", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    VoltagesAction(o, l, Hv3, Mv3, Lv3),
                    Act.Number(o, "energy.power.set", T("Rated power…", "Bemessungsleistung…"), Tr3Power, l),
                    Act.Toggle(o, "energy.levelcolors", T("Colour by voltage level", "Farbe nach Spannungsebene"), Pm.LevelColors),
                },
            };

            yield return new EnergyType("energy.grid.substation", T("Compact substation", "Ortsnetzstation (Kompaktstation)"), EnCategories.Grid,
                Kw("substation", "compact substation", "secondary substation", "kiosk", "transformer station", "grid connection",
                    "ortsnetzstation", "kompaktstation", "trafostation", "umspannstation", "netzstation", "netzanschluss"),
                ViewSpec.Pictorial(34, 30, SubstationPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("mv", 0, -15), new Port("lv", 0, 15)), LabelBox = ViewSpec.LabelAt(-13, -5.4, 13, -0.6), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(40, 30, SubstationSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("mv", 0, -15), new Port("lv", 0, 15)), LabelBox = ViewSpec.LabelAt(10.5, -10, 20, 10),
                })
            {
                Params = new[] { StationMv, Lv, TrPower, Pm.LevelColors },
                Rating = (o, l) => new[] { Fmt.Ratio(l, o.Clamped(StationMv), o.Clamped(Lv)) + " · " + Fmt.Si(o.Clamped(TrPower) * 1000, "VA", l) },
                RatingSingleLine = (o, l) => new[] { Fmt.Kv(o.Clamped(StationMv), l), Fmt.Si(o.Clamped(TrPower) * 1000, "VA", l), Fmt.Kv(o.Clamped(Lv), l) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.infeed", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    VoltagesAction(o, l, StationMv, Lv),
                    Act.Number(o, "energy.power.set", T("Rated power…", "Bemessungsleistung…"), TrPower, l),
                },
            };

            yield return new EnergyType("energy.grid.meter", T("Grid connection meter", "Netzanschlusszähler"), EnCategories.Grid,
                Kw("meter", "energy meter", "electricity meter", "smart meter", "bidirectional meter", "kwh", "grid connection",
                    "zähler", "stromzähler", "energiezähler", "zweirichtungszähler", "smart meter", "netzanschluss", "messung"),
                ViewSpec.Pictorial(20, 28, MeterPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -14), new Port("b", 0, 14)), LabelBox = ViewSpec.LabelAt(-7, 5.6, 7, 9.4), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(30, 24, MeterSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12), new Port("b", 0, 12)), LabelBox = ViewSpec.LabelAt(7.5, -5, 15, 5),
                })
            {
                Params = new[] { Bidirectional, MeterUnit },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.infeed", "energy.conv.inverter", "energy.line.cable" },
                Extra = (o, l) => new[] { Act.Toggle(o, "energy.bidirectional", T("Bidirectional meter", "Zweirichtungszähler"), Bidirectional) },
            };

            yield return new EnergyType(E.BusbarTypeId, T("Grid busbar", "Netz-Sammelschiene"), EnCategories.Grid,
                Kw("busbar", "bus bar", "bus", "node", "switchgear", "voltage level", "substation",
                    "sammelschiene", "schiene", "knoten", "schaltanlage", "spannungsebene"),
                ViewSpec.Pictorial(60, 10, BusbarPictorial) with
                {
                    Ports = o => TapPorts(o, 0.6), Rails = o => new[] { (new Vec2(-29.5, 0.6), new Vec2(29.5, 0.6)) },
                    LabelBox = ViewSpec.LabelAt(-29.5, -5, -2, -1.2),
                },
                ViewSpec.SingleLine(60, 8, BusbarSymbol) with
                {
                    Ports = o => TapPorts(o, 1.5), Rails = o => new[] { (new Vec2(-30, 1.5), new Vec2(30, 1.5)) },
                    LabelBox = ViewSpec.LabelAt(-30, -4, -2, 0.3),
                })
            {
                DefaultViewId = EnViews.SingleLine,
                KeepWidth = true,
                Params = new[] { BusVoltage, Dc, Taps, Pm.LevelColors },
                Rating = (o, l) => new[] { Fmt.Kv(o.Clamped(BusVoltage), l, o.B(Dc)) },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.breaker", "energy.grid.disconnector", "energy.grid.transformer", "energy.line.cable" },
                Extra = (o, l) => new[]
                {
                    Act.Number(o, "energy.voltage.set", T("Nominal voltage…", "Nennspannung…"), BusVoltage, l),
                    Act.Step(o, "energy.taps.more", T("One connection point more", "Ein Anschlusspunkt mehr"), Taps, 1),
                    Act.Step(o, "energy.taps.fewer", T("One connection point fewer", "Ein Anschlusspunkt weniger"), Taps, -1),
                    Act.Toggle(o, "energy.levelcolors", T("Colour by voltage level", "Farbe nach Spannungsebene"), Pm.LevelColors),
                },
            };

            yield return new EnergyType("energy.grid.breaker", T("Circuit breaker", "Leistungsschalter"), EnCategories.Grid,
                Kw("circuit breaker", "breaker", "cb", "switchgear", "protection", "switch",
                    "leistungsschalter", "schalter", "schaltanlage", "schutz", "lasttrenner"),
                ViewSpec.Pictorial(16, 24, BreakerPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12), new Port("b", 0, 12)), LabelBox = ViewSpec.LabelAt(-5, 2.6, 5, 5.6), LabelAlign = TextAnchor.Middle,
                },
                ViewSpec.SingleLine(26, 24, BreakerSymbol_) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12), new Port("b", 0, 12)), LabelBox = ViewSpec.LabelAt(5, -5, 13, 5),
                })
            {
                DefaultViewId = EnViews.SingleLine,
                Params = new[] { Closed, BreakerSymbol },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.disconnector", "energy.grid.busbar", "energy.line.cable" },
                Extra = (o, l) => new[] { SwitchToggle(o, Closed), Act.Cycle(o, "energy.symbol.next", BreakerSymbol) },
            };

            yield return new EnergyType("energy.grid.disconnector", T("Disconnector", "Trennschalter"), EnCategories.Grid,
                Kw("disconnector", "isolator", "switch-disconnector", "load-break switch", "fuse switch", "switchgear",
                    "trennschalter", "trenner", "lasttrennschalter", "sicherungslasttrennschalter", "schaltanlage"),
                ViewSpec.Pictorial(16, 24, DisconnectorPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12), new Port("b", 0, 12)),
                },
                ViewSpec.SingleLine(26, 24, DisconnectorSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12), new Port("b", 0, 12)), LabelBox = ViewSpec.LabelAt(5, -5, 13, 5),
                })
            {
                DefaultViewId = EnViews.SingleLine,
                Params = new[] { Closed, DisconnectorKind },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.breaker", "energy.grid.earthing", "energy.line.cable" },
                Extra = (o, l) => new[] { SwitchToggle(o, Closed), Act.Cycle(o, "energy.kind.next", DisconnectorKind) },
            };

            yield return new EnergyType("energy.grid.earthing", T("Earthing switch", "Erdungsschalter"), EnCategories.Grid,
                Kw("earthing switch", "grounding switch", "earth", "ground", "switchgear",
                    "erdungsschalter", "erder", "erdung", "schaltanlage"),
                ViewSpec.Pictorial(16, 24, EarthingPictorial) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12)),
                },
                ViewSpec.SingleLine(26, 24, EarthingSymbol) with
                {
                    Ports = ViewSpec.Fixed(new Port("a", 0, -12)), LabelBox = ViewSpec.LabelAt(5, -5, 13, 5),
                })
            {
                DefaultViewId = EnViews.SingleLine,
                Params = new[] { EarthClosed },
                Uniform = false,
                CompanionIds = new[] { "energy.grid.disconnector", "energy.grid.busbar" },
                Extra = (o, l) => new[] { SwitchToggle(o, EarthClosed) },
            };
        }

        private static class E
        {
            public const string BusbarTypeId = En.BusbarTypeId;
        }

        private static string PowerLine(double va, SketchObject o, Lang l)
        {
            var vector = o.S(Vector).Trim();
            return Fmt.Si(va, "VA", l) + (vector.Length > 0 ? " " + vector : "");
        }

        private static ContextAction SwitchToggle(SketchObject o, ParamDef closed)
            => new("energy.switch.toggle", T("Toggle state (open/closed)", "Schaltzustand umschalten (offen/geschlossen)"), IconIds.Toggle,
                x => x.WithParam(closed.Key, !x.B(closed))) { IsActive = o.B(closed), Group = "state", ParamKeys = new[] { closed.Key } };

        /// <summary>"Voltages…" prompt: "20/0.4" sets both windings at once.</summary>
        private static ContextAction VoltagesAction(SketchObject o, Lang lang, params ParamDef[] windings)
            => ContextAction.WithInput("energy.voltages.set", T("Voltages…", "Spannungen…"), IconIds.QtyVoltage,
                T("Voltages in kV (e.g. " + (windings.Length == 2 ? "20/0.4" : "110/20/10") + ")", "Spannungen in kV (z. B. " + (windings.Length == 2 ? "20/0,4" : "110/20/10") + ")"),
                string.Join("/", windings.Select(w => Fmt.Num(o.N(w), lang))),
                (x, text) =>
                {
                    var v = Fmt.ParseRatio(text, windings.Length, lang);
                    for (var i = 0; i < windings.Length; i++) x = x.WithParam(windings[i].Key, Math.Max(windings[i].Min ?? 0, Math.Min(windings[i].Max ?? 1e9, v[i])));
                    return x;
                }, "kV") with { Group = "rating", ParamKeys = windings.Select(w => w.Key).ToArray() };

        private static string LevelColor(SketchObject o, double kv, bool dc = false)
            => o.B(Pm.LevelColors) ? Voltage.Color(Voltage.Of(kv, dc)) : "#000000";

        private static Style Sym(string color) => St.Sym with { Stroke = color };

        // ------------------------------------------------------------------ infeed

        private static double LegX(double y, int side) => side * (5 - (15 - y) * (4.0 / 29));

        private static void InfeedPictorial(Pen p, SketchObject o)
        {
            var steel = St.Stroke("#546e7a", 0.35);
            var thin = St.Stroke("#546e7a", 0.18);
            p.Line(-8, 15, 8, 15, St.Stroke(Pal.Ink, 0.35), "ground");
            p.Line(0, 15, 0, 17, St.Stroke(Pal.Dark, 0.5), EnTags.Lead);
            p.Line(-5, 15, -1, -14, steel, "leg");
            p.Line(5, 15, 1, -14, steel, "leg");
            p.Poly(steel, false, "peak", -1, -14, 0, -16.5, 1, -14);
            var levels = new[] { 15.0, 9.0, 3.5, -1.5, -6.0, -10.0, -14.0 };
            for (var i = 0; i + 1 < levels.Length; i++)
            {
                p.Line(LegX(levels[i], -1), levels[i], LegX(levels[i + 1], 1), levels[i + 1], thin, "lattice");
                p.Line(LegX(levels[i], 1), levels[i], LegX(levels[i + 1], -1), levels[i + 1], thin, "lattice");
            }
            foreach (var (y, half) in new[] { (-10.0, 9.0), (-4.5, 7.0) })
            {
                p.Line(-half, y, half, y, steel, "arm");
                p.Line(-half, y, LegX(y - 2.5, -1), y - 2.5, thin, "strut");
                p.Line(half, y, LegX(y - 2.5, 1), y - 2.5, thin, "strut");
                foreach (var s in new[] { -1, 1 })
                {
                    var x = s * half;
                    p.Line(x, y, x, y + 1.6, St.Stroke(Pal.Porcelain, 0.55), "insulator");
                    p.Path(new[] { PathSeg.Move(new Vec2(x, y + 1.6)), PathSeg.Quad(new Vec2(x + s * 3, y + 3.4), new Vec2(s * 15, y + 2.6)) },
                        St.Stroke(Pal.Dark, 0.2), "conductor");
                }
            }
        }

        private static void InfeedSymbol(Pen p, SketchObject o)
        {
            p.Line(0, 1, 0, 9, St.Sym, EnTags.Lead);
            p.Rect(-7, -8, 7, 1, St.Sym with { Fill = Fill.Hatched(new HatchPattern(45, 1.2, true)) }, "network");
        }

        // ------------------------------------------------------------------ transformers

        private static void Bushing(Pen p, double x, double top, double bottom)
        {
            var shed = St.Stroke(Pal.Porcelain, 0.45);
            p.Rect(x - 0.55, top, x + 0.55, bottom, St.Fill(Pal.Porcelain), "bushing");
            for (var y = top + 1.2; y < bottom - 0.4; y += 1.4) p.Line(x - 1.1, y, x + 1.1, y, shed, "shed");
            p.Rect(x - 0.4, top - 0.6, x + 0.4, top, St.Fill(Pal.Steel), "terminal");
        }

        private static void TransformerPictorial(Pen p, SketchObject o, bool three)
        {
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -17, 0, -13.6, dark, EnTags.Lead);
            p.Line(6, -8.5, 6, -5, St.Out, "support");
            p.Rect(2.2, -11.5, 10.2, -8.5, St.Fill(Pal.Steel), "conservator", 1.5);
            foreach (var x in new[] { -6.0, -3.0, 0.0 }) Bushing(p, x, -13, -5);
            foreach (var x0 in new[] { -12.0, 8.0 })
            {
                p.Rect(x0, -3, x0 + 4, 10, St.Fill("#cfd8dc"), "radiator");
                for (var i = 1; i < 4; i++) p.Line(x0 + i, -2.6, x0 + i, 9.6, St.Detail(Pal.Ink), "fin");
            }
            p.Rect(-8, -5, 8, 12, St.Fill(Pal.Steel), "tank", 0.6);
            p.Rect(-6.5, -3, 6.5, 9.5, St.Fill(Pal.White), "nameplate", 0.4);
            if (three)
            {
                foreach (var x in new[] { -2.8, 2.8 })
                {
                    p.Rect(x - 1.9, 12, x + 1.9, 14.2, St.Fill(Pal.Dark), "cable-box");
                    p.Line(x, 14.2, x, 17, dark, EnTags.Lead);
                }
            }
            else
            {
                p.Rect(-3, 12, 3, 14.2, St.Fill(Pal.Dark), "cable-box");
                p.Line(0, 14.2, 0, 17, dark, EnTags.Lead);
            }
        }

        private static void TransformerSymbol(Pen p, SketchObject o)
        {
            const double cx = 0, r = 4.6, d = 3.3;
            var hv = LevelColor(o, o.Clamped(Hv));
            var lv = LevelColor(o, o.Clamped(Lv));
            Windings(p, cx, 13, r, d, hv, lv);
        }

        /// <summary>Two overlapping winding circles (shape kept under non-uniform scaling) with leads to the ports at ±portY.</summary>
        private static void Windings(Pen p, double cx, double portY, double r, double d, string upper, string lower)
        {
            var u = p.Uniform(cx, 0);
            p.Add(Prim.Line(p.V(cx, -portY), u.V(0, -d - r), Sym(upper)), EnTags.Lead);
            p.Add(Prim.Line(u.V(0, d + r), p.V(cx, portY), Sym(lower)), EnTags.Lead);
            u.Circle(0, -d, r, Sym(upper), "winding-hv");
            u.Circle(0, d, r, Sym(lower), "winding-lv");
        }

        private static void Transformer3Symbol(Pen p, SketchObject o)
        {
            const double r = 4.4;
            var hv = LevelColor(o, o.Clamped(Hv3));
            var mv = LevelColor(o, o.Clamped(Mv3));
            var lv = LevelColor(o, o.Clamped(Lv3));
            // Winding circles keep their shape; the leads run vertically from the circles and jog to the ports when the
            // box is scaled non-uniformly.
            var u = p.Uniform(0, 0);
            void Lead(Vec2 from, Vec2 port, string color)
            {
                var mid = (from.Y + port.Y) / 2;
                p.Add(Prim.Polyline(new[] { from, new Vec2(from.X, mid), new Vec2(port.X, mid), port }, Sym(color)), EnTags.Lead);
            }
            Lead(u.V(0, -3.2 - r), p.V(0, -14), hv);
            Lead(u.V(-2.8, 1.6 + r), p.V(-2.8, 14), mv);
            Lead(u.V(2.8, 1.6 + r), p.V(2.8, 14), lv);
            u.Circle(0, -3.2, r, Sym(hv), "winding-hv");
            u.Circle(-2.8, 1.6, r, Sym(mv), "winding-mv");
            u.Circle(2.8, 1.6, r, Sym(lv), "winding-lv");
        }

        private static void SubstationPictorial(Pen p, SketchObject o)
        {
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -15, 0, -11, dark, EnTags.Lead);
            p.Line(0, 12, 0, 15, dark, EnTags.Lead);
            p.Rect(-14, -6, 14, 12, St.Fill(Pal.Concrete), "wall");
            p.Poly(St.Fill(Pal.Roof), true, "roof", -15.5, -6, 0, -11, 15.5, -6);
            foreach (var x0 in new[] { -11.0, 4.0 })
            {
                p.Rect(x0, 0, x0 + 7, 12, St.Fill("#a1887f"), "door");
                for (var i = 0; i < 3; i++) p.Line(x0 + 1, 1.4 + i * 0.9, x0 + 6, 1.4 + i * 0.9, St.Detail(Pal.Ink), "louvre");
            }
            p.Poly(St.Fill(Pal.Amber), true, "warning", -7.5, 5, -9.4, 8.4, -5.6, 8.4);
            p.Poly(St.Detail(Pal.Dark, 0.25), false, "flash", -7.2, 5.9, -7.9, 7.0, -7.1, 7.0, -7.8, 8.0);
            p.Rect(-15, 12, 15, 12.8, St.Fill(Pal.Dark), "base");
        }

        private static void SubstationSymbol(Pen p, SketchObject o)
        {
            const double cx = 0, r = 4.4, d = 3.1;
            var mv = LevelColor(o, o.Clamped(StationMv));
            var lv = LevelColor(o, o.Clamped(Lv));
            p.Rect(-9, -10, 9, 10, St.Thin with { StrokeWidth = 0.25, Dash = DashStyle.Dash }, "boundary");
            Windings(p, cx, 15, r, d, mv, lv);
        }

        // ------------------------------------------------------------------ meter

        private static void MeterArrows(Pen p, double x0, double x1, double y, bool bidirectional, Style s)
        {
            if (bidirectional)
            {
                p.Arrow(x0, y - 0.8, x1, y - 0.8, 1.1, 0.45, s, "arrow-import");
                p.Arrow(x1, y + 0.8, x0, y + 0.8, 1.1, 0.45, s, "arrow-export");
            }
            else p.Arrow(x0, y, x1, y, 1.1, 0.45, s, "arrow-import");
        }

        private static void MeterPictorial(Pen p, SketchObject o)
        {
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -14, 0, -10, dark, EnTags.Lead);
            p.Line(0, 10, 0, 14, dark, EnTags.Lead);
            p.Rect(-8, -10, 8, 10, St.Fill(Pal.Light), "housing", 1.5);
            p.Rect(-6, -7.5, 6, -2.5, St.Fill("#cfd8dc"), "display", 0.3);
            p.Text(0, -5, "000000", 2.6, 11, 3, TextAnchor.Middle, false, Pal.Dark, "digits");
            p.Text(0, -0.2, o.S(MeterUnit), 2, 7, 2.2, TextAnchor.Middle, false, Pal.Ink, "unit");
            p.Dot(5.6, -0.2, 0.45, Pal.Red, "led");
            MeterArrows(p, -3.5, 3.5, 3.3, o.B(Bidirectional), St.Stroke(Pal.Ink, 0.25));
        }

        private static void MeterSymbol(Pen p, SketchObject o)
        {
            const double cx = 0;
            p.Line(cx, -12, cx, -5, St.Sym, EnTags.Lead);
            p.Line(cx, 5, cx, 12, St.Sym, EnTags.Lead);
            p.Rect(cx - 6, -5, cx + 6, 5, St.SymWhite, "body");
            p.Text(cx, -1.8, o.S(MeterUnit), 2.6, 10, 2.8, TextAnchor.Middle, false, null, "unit");
            MeterArrows(p, cx - 3.5, cx + 3.5, 2.4, o.B(Bidirectional), St.Thin);
        }

        // ------------------------------------------------------------------ busbar

        private static IReadOnlyList<Port> TapPorts(SketchObject o, double y)
        {
            var n = o.I(Taps);
            return Enumerable.Range(0, n).Select(i => new Port("t" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), -30 + 60 * (i + 0.5) / n, y)).ToList();
        }

        private static void BusbarPictorial(Pen p, SketchObject o)
        {
            foreach (var x in new[] { -20.0, 0.0, 20.0 })
            {
                p.Rect(x - 0.7, 2, x + 0.7, 4.6, St.Fill(Pal.Porcelain), "insulator");
                p.Line(x - 1.3, 3.1, x + 1.3, 3.1, St.Stroke(Pal.Porcelain, 0.45), "shed");
                p.Line(-1.5 + x, 4.6, 1.5 + x, 4.6, St.Stroke(Pal.Ink, 0.4), "foot");
            }
            p.Rect(-29.5, -0.8, 29.5, 2.0, St.Fill(Pal.Copper), EnTags.Busbar, 0.5);
            if (o.B(Pm.LevelColors)) p.Rect(-29.5, -0.8, -27.5, 2.0, St.Solid(Voltage.Color(Voltage.Of(o.Clamped(BusVoltage), o.B(Dc)))), "level");
        }

        private static void BusbarSymbol(Pen p, SketchObject o)
        {
            var color = LevelColor(o, o.Clamped(BusVoltage), o.B(Dc));
            p.Line(-30, 1.5, 30, 1.5, Style.Visible with { StrokeWidth = 1.2, Stroke = color }, EnTags.Busbar);
        }

        // ------------------------------------------------------------------ switchgear

        /// <summary>Blade end of a switch hinged at <paramref name="hinge"/>: straight to the contact when closed, swung out when open.</summary>
        private static Vec2 BladeEnd(Vec2 hinge, Vec2 contact, bool closed, double openDeg = 35)
        {
            if (closed) return contact;
            var len = hinge.DistanceTo(contact) * 0.98;
            var a = Geo.DegToRad(openDeg);
            return hinge + new Vec2(-Math.Sin(a), -Math.Cos(a)) * len;
        }

        private static void BreakerPictorial(Pen p, SketchObject o)
        {
            var closed = o.B(Closed);
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -12, 0, -8, dark, EnTags.Lead);
            p.Line(0, 8, 0, 12, dark, EnTags.Lead);
            p.Rect(-6, -8, 6, 8, St.Fill(Pal.Metal), "body", 1);
            p.Round(0, -6.6, 0.8, St.Fill(Pal.Steel), "terminal");
            p.Round(0, 6.6, 0.8, St.Fill(Pal.Steel), "terminal");
            p.Rect(-1.6, -4.6, 1.6, 1.6, St.Fill("#455a64"), "slot", 0.3);
            if (closed) p.Rect(-1.2, -4.3, 1.2, -1.6, St.Fill("#fafafa"), "lever", 0.3);
            else p.Rect(-1.2, -1.4, 1.2, 1.3, St.Fill("#fafafa"), "lever", 0.3);
            p.Text(3.7, -1.5, closed ? "I" : "O", 1.8, 2, 2, TextAnchor.Middle, true, Pal.Ink, "state");
        }

        private static void BreakerSymbol_(Pen p, SketchObject o)
        {
            const double cx = 0;
            var closed = o.B(Closed);
            if (o.S(BreakerSymbol) == "square")
            {
                p.Line(cx, -12, cx, -4, St.Sym, EnTags.Lead);
                p.Line(cx, 4, cx, 12, St.Sym, EnTags.Lead);
                p.Rect(cx - 4, -4, cx + 4, 4, closed ? St.Black with { StrokeWidth = 0.35 } : St.SymWhite, "breaker");
                return;
            }
            p.Line(cx, -12, cx, -5, St.Sym, EnTags.Lead);
            p.Cross(cx, -5, 1.0, St.Sym, "breaker");
            var end = BladeEnd(new Vec2(cx, 5), new Vec2(cx, -5), closed);
            p.Line(cx, 5, end.X, end.Y, St.Sym, "blade");
            p.Line(cx, 5, cx, 12, St.Sym, EnTags.Lead);
        }

        /// <summary>Knife switch on an insulating base: jaw at the top, hinge at the bottom, blade closed or swung open.</summary>
        private static void KnifeSwitch(Pen p, double hingeY, bool closed, string kind)
        {
            p.Rect(-1.6, -7.6, 1.6, -5.4, St.Fill(Pal.Steel), "jaw");
            if (kind == "loadbreak") p.Rect(2.2, -8, 4.4, -4.8, St.Fill(Pal.Dark), "arc-chute", 0.3);
            var hinge = new Vec2(0, hingeY);
            var end = BladeEnd(hinge, new Vec2(0, -6.5), closed);
            p.Bar(hinge, end, 1.0, St.Fill("#90a4ae"), "blade");
            if (kind == "fuse") p.Bar(Vec2.Lerp(hinge, end, 0.3), Vec2.Lerp(hinge, end, 0.75), 2.2, St.Fill(Pal.White), "fuse");
            p.Round(0, hingeY, 1.1, St.Fill(Pal.Steel), "hinge");
        }

        private static void DisconnectorPictorial(Pen p, SketchObject o)
        {
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -12, 0, -7.6, dark, EnTags.Lead);
            p.Line(0, 7, 0, 12, dark, EnTags.Lead);
            p.Rect(-5, -9, 5, 9, St.Fill(Pal.Concrete), "base", 1);
            KnifeSwitch(p, 6, o.B(Closed), o.S(DisconnectorKind));
        }

        private static void EarthingPictorial(Pen p, SketchObject o)
        {
            var dark = St.Stroke(Pal.Dark, 0.5);
            p.Line(0, -12, 0, -7.6, dark, EnTags.Lead);
            p.Rect(-5, -9, 5, 6.5, St.Fill(Pal.Concrete), "base", 1);
            p.Line(0, 5.1, 0, 8, dark, "earth-lead");
            p.Earth(0, 8, 3, St.Stroke(Pal.Dark, 0.45));
            KnifeSwitch(p, 4, o.B(EarthClosed), "earthing");
        }

        private static void DisconnectorSymbol(Pen p, SketchObject o)
        {
            const double cx = 0;
            var kind = o.S(DisconnectorKind);
            p.Line(cx, -12, cx, -5, St.Sym, EnTags.Lead);
            if (kind == "loadbreak") p.Round(cx, -5.9, 0.9, St.SymWhite, "contact");
            else p.Line(cx - 1.6, -5, cx + 1.6, -5, St.Sym, "contact");
            var hinge = new Vec2(cx, 5);
            var end = BladeEnd(hinge, new Vec2(cx, -5), o.B(Closed));
            p.Line(hinge.X, hinge.Y, end.X, end.Y, St.Sym, "blade");
            if (kind == "fuse") p.Bar(Vec2.Lerp(hinge, end, 0.3), Vec2.Lerp(hinge, end, 0.7), 2.2, St.SymWhite, "fuse");
            p.Line(cx, 5, cx, 12, St.Sym, EnTags.Lead);
        }

        private static void EarthingSymbol(Pen p, SketchObject o)
        {
            const double cx = 0;
            p.Line(cx, -12, cx, -5, St.Sym, EnTags.Lead);
            p.Line(cx - 1.6, -5, cx + 1.6, -5, St.Sym, "contact");
            var hinge = new Vec2(cx, 4);
            var end = BladeEnd(hinge, new Vec2(cx, -5), o.B(EarthClosed));
            p.Line(hinge.X, hinge.Y, end.X, end.Y, St.Sym, "blade");
            p.Line(cx, 4, cx, 6.5, St.Sym, EnTags.Lead);
            p.Earth(cx, 6.5, 2.6, St.Sym);
        }
    }
}
