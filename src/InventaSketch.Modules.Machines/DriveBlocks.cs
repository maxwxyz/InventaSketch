using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Electric drive system blocks (battery, inverter, DC link, e-motor, gearbox, differential, wheel). Every block has a
    /// block-diagram view and a symbol view; Port anchors on the block edges and symbol terminals let power-flow arrows
    /// and wires snap on.
    /// </summary>
    internal static class DriveBlocks
    {
        public const string Cat = "machines.drive";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VBlock = T("Block", "Block");
        private static readonly I18nText VSymbol = T("Symbol", "Symbol");

        public static IEnumerable<MachType> Types()
        {
            yield return Block("machines.drive.battery", T("Battery", "Batterie"),
                new[] { "battery", "traction battery", "hv battery", "accumulator", "energy storage", "batterie", "akku", "hochvoltbatterie", "traktionsbatterie", "energiespeicher" },
                BatteryGlyph, new ViewSpec("symbol", VSymbol, IconIds.Symbol, 30, 14, BatterySymbol)
                {
                    Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(-15, 0), "plus"), Anchor.Point(AnchorRole.Port, p.V(15, 0), "minus") },
                });
            yield return Block("machines.drive.inverter", T("Inverter", "Wechselrichter"),
                new[] { "inverter", "power electronics", "converter", "b6 bridge", "three-phase bridge", "igbt", "mosfet", "wechselrichter", "inverter", "leistungselektronik", "umrichter", "b6-brücke", "drehstrombrücke" },
                null, new ViewSpec("symbol", T("Three-phase bridge", "Drehstrombrückenschaltung"), IconIds.Symbol, 60, 40, InverterSymbol)
                {
                    Anchors = (p, o) => new[]
                    {
                        Anchor.Point(AnchorRole.Port, p.V(-30, -16), "dc+"), Anchor.Point(AnchorRole.Port, p.V(-30, 16), "dc-"),
                        Anchor.Point(AnchorRole.Port, p.V(LegX[0] + 8, 0), "u"), Anchor.Point(AnchorRole.Port, p.V(LegX[1] + 8, 0), "v"),
                        Anchor.Point(AnchorRole.Port, p.V(LegX[2] + 8, 0), "w"),
                    },
                }, extraParams: new[] { Common.Flag("dclink", T("DC-link capacitor in symbol", "Zwischenkreiskondensator im Symbol"), true) });
            yield return Block("machines.drive.dclink", T("DC link", "Zwischenkreis"),
                new[] { "dc link", "dc-link capacitor", "capacitor", "bus", "zwischenkreis", "zwischenkreiskondensator", "kondensator" },
                DcLinkGlyph, new ViewSpec("symbol", VSymbol, IconIds.Symbol, 20, 30, DcLinkSymbol)
                {
                    Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(0, -15), "plus"), Anchor.Point(AnchorRole.Port, p.V(0, 15), "minus") },
                });
            yield return Block("machines.drive.motor", T("E-motor", "E-Motor"),
                new[] { "e-motor", "electric motor", "traction motor", "drive unit", "e-machine", "e-motor", "elektromotor", "antriebsmaschine", "fahrmotor", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
                MotorGlyph, new ViewSpec("symbol", VSymbol, IconIds.Symbol, 50, 50, (p, o) => Mag.MotorSymbol(p, o)) { Anchors = (p, o) => Mag.MotorSymbolAnchors(p, o) },
                extraParams: new[] { Common.Mode, Common.Phases },
                companions: new[] { "machines.drive.inverter", "machines.drive.battery", "machines.drive.gearbox", "machines.drivetrain.emachine" });
            yield return Block("machines.drive.gearbox", T("Gearbox", "Getriebe"),
                new[] { "gearbox", "transmission", "reduction gear", "reducer", "getriebe", "untersetzung", "übersetzung", "reduktionsgetriebe", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
                GearboxGlyph, new ViewSpec("symbol", T("Schematic", "Schema"), IconIds.Schematic, 40, 30, GearboxSymbol)
                {
                    Anchors = (p, o) => new[]
                    {
                        Anchor.Point(AnchorRole.Port, p.V(-20, -6), "in"), Anchor.Point(AnchorRole.Port, p.V(20, 8), "out"),
                        Anchor.Segment(AnchorRole.Axis, p.V(-20, -6), p.V(4, -6)), Anchor.Segment(AnchorRole.Axis, p.V(-4, 8), p.V(20, 8)),
                    },
                }, companions: new[] { "machines.drive.motor", "machines.drive.differential", "machines.drivetrain.engine", "mech.clutch.disc" });
            yield return Block("machines.drive.differential", T("Differential", "Differential"),
                new[] { "differential", "final drive", "axle drive", "differential", "achsgetriebe", "ausgleichsgetriebe", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
                DifferentialGlyph, new ViewSpec("symbol", T("Schematic", "Schema"), IconIds.Schematic, 40, 30, DifferentialSymbol)
                {
                    Anchors = (p, o) => new[]
                    {
                        Anchor.Point(AnchorRole.Port, p.V(0, -15), "in"), Anchor.Point(AnchorRole.Port, p.V(-20, 4), "left"), Anchor.Point(AnchorRole.Port, p.V(20, 4), "right"),
                        Anchor.Segment(AnchorRole.Axis, p.V(-20, 4), p.V(20, 4)),
                    },
                }, companions: new[] { "machines.drive.wheel", "mech.shaft", "machines.drive.gearbox" });
            yield return Wheel();
        }

        private static readonly double[] LegX = { -16, -3, 10 };

        // ------------------------------------------------------------------ generic block

        private static IEnumerable<Anchor> EdgePorts(Pen p) => new[]
        {
            Anchor.Point(AnchorRole.Port, p.V(-15, 0), "w"), Anchor.Point(AnchorRole.Port, p.V(15, 0), "e"),
            Anchor.Point(AnchorRole.Port, p.V(0, -10), "n"), Anchor.Point(AnchorRole.Port, p.V(0, 10), "s"),
        };

        /// <summary>
        /// Block-diagram type: rounded box with a glyph and the (host-language) label, edge ports, plus a symbol view.
        /// <paramref name="label"/> replaces the type name as block label; <paramref name="blockAnchors"/> adds anchors
        /// to the block view (e.g. an output hub).
        /// </summary>
        internal static MachType Block(string id, I18nText name, string[] keywords, Action<Pen, SketchObject>? glyph, ViewSpec symbol,
            ParamDef[]? extraParams = null, string[]? companions = null, string category = Cat, I18nText? label = null,
            Func<Pen, SketchObject, IEnumerable<Anchor>>? blockAnchors = null)
        {
            var text = label ?? name;
            var block = new ViewSpec("block", VBlock, IconIds.Block, 30, 20, (p, o) =>
            {
                if (glyph is null) { InverterBlock(p, o); return; }
                p.RoundBox(-14.6, -9.6, 14.6, 9.6, 1.2, S.Vis, "block");
                glyph(p, o);
                var shown = string.IsNullOrWhiteSpace(o.Text) ? Common.L(p, text.En, text.De ?? text.En) : o.Text!;
                var size = Math.Min(3.0, 26.0 / Math.Max(1, shown.Length) / 0.55);
                p.Text(0, 5.6, shown, size, "label");
            })
            { Anchors = (p, o) => blockAnchors is null ? EdgePorts(p) : EdgePorts(p).Concat(blockAnchors(p, o)) };
            var t = new MachType(id, name, category, keywords, block, symbol)
            {
                Params = extraParams ?? Array.Empty<ParamDef>(),
                CompanionTypes = companions ?? Array.Empty<string>(),
                Extra = o => o.Type == "machines.drive.motor" && o.View == "symbol" ? Common.ModeChoice(o) : Array.Empty<ContextAction>(),
            };
            return t;
        }

        private static void InverterBlock(Pen p, SketchObject o)
        {
            // IEC 60617 style converter: square with diagonal, DC sign upper left, AC sign lower right
            p.RoundBox(-14.6, -9.6, 14.6, 9.6, 0, S.Vis, "block");
            p.Line(-14.6, 9.6, 14.6, -9.6, S.Thin, "diagonal");
            p.Line(-11.5, -6.2, -5.5, -6.2, S.Sym, "dc");
            p.Line(-11.5, -4.2, -5.5, -4.2, S.Sym, "dc");
            p.PolyD(Enumerable.Range(0, 21).Select(i => new Vec2(5 + 6.5 * i / 20.0, 5.2 - 1.5 * Math.Sin(2 * Math.PI * i / 20.0))), S.Sym, false, "ac");
        }

        private static void BatteryGlyph(Pen p, SketchObject o)
        {
            p.Box(-6, -7, 5, -0.5, S.Sym, "glyph");
            p.Box(5, -5, 6.6, -2.5, S.Sym, "glyph");
            p.Box(-5, -6, -1, -1.5, S.Thin with { Fill = Fill.Solid("#555555") }, "glyph");
        }

        private static void DcLinkGlyph(Pen p, SketchObject o)
        {
            p.Line(-6, -3.75, -1, -3.75, S.Wire, "glyph");
            p.Line(1, -3.75, 6, -3.75, S.Wire, "glyph");
            p.Line(-1, -7, -1, -0.5, S.Sym, "glyph");
            p.Line(1, -7, 1, -0.5, S.Sym, "glyph");
        }

        private static void MotorGlyph(Pen p, SketchObject o)
        {
            p.Round(new Vec2(0, -3.8), 3.6, S.Sym, "glyph");
            p.Text(0, -3.8, o.Str("mode", "motor") == "generator" ? "G" : "M", 3.6, "glyph", bold: true);
        }

        // Glyphs are drawn at the uniform detail scale around an anchor point, so they keep their shape when the block is stretched.
        internal static void GlyphCircle(Pen p, Vec2 at, double dx, double dy, double r)
        {
            var c = p.V(at) + new Vec2(dx * p.K, dy * p.K);
            p.Add(Prim.Circle(c.X, c.Y, r * p.K, S.Sym), "glyph");
        }

        internal static void GlyphLine(Pen p, Vec2 at, double x1, double y1, double x2, double y2)
            => p.LineP(p.V(at) + new Vec2(x1 * p.K, y1 * p.K), p.V(at) + new Vec2(x2 * p.K, y2 * p.K), S.Sym, "glyph");

        private static void GearboxGlyph(Pen p, SketchObject o)
        {
            var at = new Vec2(0, -3.8);
            GlyphCircle(p, at, -2.2, -0.75, 2.6); // two meshing gears: circles touch
            GlyphCircle(p, at, 2.03, 0.78, 1.9);
        }

        private static void DifferentialGlyph(Pen p, SketchObject o)
        {
            var at = new Vec2(0, -3.2);
            GlyphCircle(p, at, 0, 0, 2.6);
            GlyphLine(p, at, -6, 0, -2.6, 0);
            GlyphLine(p, at, 2.6, 0, 6, 0);
            GlyphLine(p, at, 0, -5, 0, -2.6);
        }

        // ------------------------------------------------------------------ symbols

        private static void BatterySymbol(Pen p, SketchObject o)
        {
            p.Line(-15, 0, -4.5, 0, S.Wire, "wire");
            p.Line(4, 0, 15, 0, S.Wire, "wire");
            foreach (var x in new[] { -4.5, 1.5 })
            {
                p.Line(x, -5, x, 5, S.Sym, "cell");
                p.Line(x + 2.5, -2.5, x + 2.5, 2.5, S.Vis with { StrokeWidth = 1.0 }, "cell");
            }
            p.Line(-2, 0, 1.5, 0, S.Wire with { Dash = DashStyle.Dash }, "wire");
            p.Text(-8, -3.6, "+", 3.2, "polarity");
        }

        private static void InverterSymbol(Pen p, SketchObject o)
        {
            p.Line(-30, -16, LegX[2], -16, S.Wire, "rail");
            p.Line(-30, 16, LegX[2], 16, S.Wire, "rail");
            p.Text(-27.5, -18.4, "+", 2.8, "polarity");
            p.Text(-27.5, 13.6, "−", 2.8, "polarity");
            if (o.Flag("dclink", true))
            {
                const double x = -23;
                p.Line(x, -16, x, -1, S.Wire, "dclink");
                p.Line(x, 1, x, 16, S.Wire, "dclink");
                p.Line(x - 2.8, -1, x + 2.8, -1, S.Sym, "dclink");
                p.Line(x - 2.8, 1, x + 2.8, 1, S.Sym, "dclink");
            }
            string[] names = { "U", "V", "W" };
            for (var i = 0; i < 3; i++)
            {
                var x = LegX[i];
                foreach (var sign in new[] { -1, 1 })
                {
                    // leg half: rail -> switch -> mid point
                    p.Line(x, sign * 16, x, sign * 12, S.Wire, "leg");
                    p.Box(x - 1.8, sign * 12, x + 1.8, sign * 4, S.Sym, "switch");
                    p.Line(x, sign * 4, x, 0, S.Wire, "leg");
                    // antiparallel freewheeling diode (conducts from the lower to the upper node)
                    var dx = x + 4.6;
                    p.Line(x, sign * 14, dx, sign * 14, S.Wire, "diode");
                    p.Line(x, sign * 2, dx, sign * 2, S.Wire, "diode");
                    double yTop = sign * 14, yBot = sign * 2;
                    double yc = Math.Min(yTop, yBot) + 4, ya = Math.Max(yTop, yBot) - 4; // cathode (upper) and anode (lower) ends
                    p.Line(dx, Math.Min(yTop, yBot), dx, yc, S.Wire, "diode");
                    p.Line(dx, ya, dx, Math.Max(yTop, yBot), S.Wire, "diode");
                    p.PolyD(new[] { new Vec2(dx - 1.6, ya), new Vec2(dx + 1.6, ya), new Vec2(dx, yc) }, S.Thin with { Fill = Fill.Solid("#000000") }, true, "diode");
                    p.Line(dx - 1.6, yc, dx + 1.6, yc, S.Sym, "diode");
                }
                p.Line(x, 0, x + 6.8, 0, S.Wire, "phase");
                p.Round(new Vec2(x + 8, 0), 1.2, S.White with { StrokeWidth = 0.3 }, "terminal");
                p.Text(x + 8, -2.8, names[i], 2.6, "phase");
                p.Round(new Vec2(x, 0), 0.5, S.Black, "node");
            }
        }

        private static void DcLinkSymbol(Pen p, SketchObject o)
        {
            p.Line(0, -15, 0, -1.6, S.Wire, "wire");
            p.Line(0, 1.6, 0, 15, S.Wire, "wire");
            p.Line(-5, -1.6, 5, -1.6, S.Sym, "plate");
            p.Line(-5, 1.6, 5, 1.6, S.Sym, "plate");
            p.Text(-5, -5, "+", 3, "polarity");
        }

        private static void GearboxSymbol(Pen p, SketchObject o)
        {
            var st = S.Vis with { StrokeWidth = 0.6 };
            p.Line(-20, -6, 4, -6, st, "shaft");
            p.Line(-4, 8, 20, 8, st, "shaft");
            // gear pair (kinematic schematic): two gear lines meeting at the pitch point
            p.Line(0, -12, 0, 1, st, "gear");
            p.Line(-2, -12, 2, -12, st, "gear");
            p.Line(0, 1, 0, 14, st, "gear");
            p.Line(-2, 14, 2, 14, st, "gear");
            p.Line(-2.5, 1, 2.5, 1, S.Thin, "pitch");
        }

        private static void DifferentialSymbol(Pen p, SketchObject o)
        {
            var st = S.Vis with { StrokeWidth = 0.6 };
            p.Line(-20, 4, -4.5, 4, st, "shaft");
            p.Line(4.5, 4, 20, 4, st, "shaft");
            p.Box(-7, -3, 7, 11, S.Thin, "cage");
            p.Line(-4.5, 0, -4.5, 8, st, "gear");
            p.Line(4.5, 0, 4.5, 8, st, "gear");
            p.Line(-2.6, 0.6, 2.6, 0.6, st, "gear");
            p.Line(-2.6, 7.4, 2.6, 7.4, st, "gear");
            p.Line(0, -15, 0, -5.2, st, "shaft");
            p.Line(-3, -5.2, 3, -5.2, st, "gear");
            p.Line(-7, -3.6, 7, -3.6, st, "gear");
        }

        // ------------------------------------------------------------------ wheel

        /// <summary>
        /// Wheel with tyre. Every view exposes a Hub at the wheel centre, so the wheel snaps onto an axle shaft and follows
        /// it (core <see cref="Coupling"/>, "carried"); top and schematic views also expose the wheel axis.
        /// </summary>
        private static MachType Wheel() => new(
            "machines.drive.wheel", T("Wheel", "Rad"), Cat,
            new[] { "wheel", "tyre", "tire", "rim", "drive wheel", "axle", "rad", "reifen", "felge", "antriebsrad", "achse", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
            new ViewSpec("side", T("Side view", "Seitenansicht"), IconIds.Side, 30, 30, (p, o) =>
            {
                p.Circle(0, 0, 15, S.Vis, "tyre");
                p.Circle(0, 0, 10, S.Vis, "rim");
                p.Circle(0, 0, 9, S.Thin, "rim");
                p.Round(new Vec2(0, 0), 3, S.Vis, "hub");
                for (var i = 0; i < 5; i++) p.Round(G.P(5.5, i * 72), 0.8, S.Thin, "bolt");
                p.CenterCross(0, 0, 15, 15);
            })
            {
                Hub = true,
                // Bore: the hub circle (radius 3 of the tyre radius 15), round in this face-on view.
                Bore = (p, o) => 3 * p.K,
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(-15, 0), "w"), Anchor.Point(AnchorRole.Port, p.V(15, 0), "e") },
            },
            new ViewSpec("top", T("Top view", "Draufsicht"), IconIds.Top, 12, 30, (p, o) =>
            {
                p.RoundBox(-6, -15, 6, 15, 2, S.Vis, "tyre");
                for (var i = 1; i < 8; i++) p.Line(-6, -15 + i * 30.0 / 8, 6, -15 + i * 30.0 / 8, S.Thin, "tread");
                p.Axis(-6, 6);
            })
            {
                Hub = true,
                Bore = (p, o) => 3 * p.Sy,
                Anchors = (p, o) => WheelAxisAnchors(p, 6),
            },
            new ViewSpec("schematic", T("Drivetrain schematic", "Antriebsstrangschema"), IconIds.Schematic, 8, 30, WheelSchematic)
            {
                Hub = true,
                Bore = (p, o) => 3 * p.Sy,
                Anchors = (p, o) => WheelAxisAnchors(p, 4),
            })
        {
            Scale = 1.0,
            CompanionTypes = new[] { "mech.shaft", "mech.gearset.differential", "machines.drive.differential", "mech.bearing.ball", "mech.brake" },
        };

        private static IEnumerable<Anchor> WheelAxisAnchors(Pen p, double hw) => new[]
        {
            Anchor.Point(AnchorRole.Port, p.V(-hw, 0), "w"), Anchor.Point(AnchorRole.Port, p.V(hw, 0), "e"),
            Anchor.Segment(AnchorRole.Axis, p.V(-hw, 0), p.V(hw, 0)),
        };

        /// <summary>Wheel in a drivetrain (top-view) schematic: dark tyre bar on the axle, as in powertrain layout sketches.</summary>
        private static void WheelSchematic(Pen p, SketchObject o)
        {
            p.RoundBox(-4, -15, 4, 15, 1.5, S.Vis with { Fill = Fill.Solid("#404040") }, "tyre");
            p.Line(-4, 0, 4, 0, S.Thin with { Stroke = "#ffffff" }, "axis");
        }
    }
}
