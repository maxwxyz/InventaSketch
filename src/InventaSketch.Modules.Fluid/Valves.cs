using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Fluid.Common;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>Non-return, flow and pressure valves and the pneumatic logic valves (shuttle, two-pressure, quick exhaust).</summary>
    internal static class Valves
    {
        public const string Cat = "fluid.valves";

        private static readonly ParamDef SpringLoaded = Flag("spring", T("Spring loaded", "Federbelastet"), false);
        private static readonly ParamDef Piloted = Flag("piloted", T("Pilot operated (unlockable)", "Entsperrbar (vorgesteuert)"), false);
        private static readonly ParamDef Adjustable = Flag("adjustable", T("Adjustable", "Einstellbar"), false);
        private static readonly ParamDef AdjustableOn = Flag("adjustable", T("Adjustable", "Einstellbar"), true);
        private static readonly ParamDef WithCheck = Flag("check", T("With bypass check valve", "Mit Rückschlagventil (Bypass)"), false);

        private static readonly string[] LineCompanions = { "fluid.line", "fluid.valve.directional", "fluid.cylinder.double", "fluid.gauge", "fluid.tank" };

        public static IEnumerable<FluidType> Types()
        {
            yield return new FluidType("fluid.valve.check", T("Non-return (check) valve", "Rückschlagventil"), Cat,
                new[] { "check valve", "non-return valve", "one-way", "pilot operated check", "rückschlagventil", "sperrventil", "entsperrbar", "rückschlag" },
                Views.Symbol(15, 10, Check))
            {
                Params = new[] { SpringLoaded, Piloted }, RefPrefix = "1V2",
                Ports = o => o.Flag("piloted", false)
                    ? new[] { new Port("P", -7.5, 0), new Port("A", 7.5, 0), new Port("X", 0, 5) }
                    : new[] { new Port("P", -7.5, 0), new Port("A", 7.5, 0) },
                CompanionIds = LineCompanions,
                Extra = o => new[]
                {
                    Toggle(o, "fluid.spring", T("Spring loaded", "Federbelastet"), "spring", false),
                    Toggle(o, "fluid.piloted", T("Pilot operated", "Entsperrbar"), "piloted", false),
                },
            };

            yield return new FluidType("fluid.valve.throttle", T("Throttle / flow control valve", "Drossel / Stromventil"), Cat,
                new[] { "throttle", "flow control", "restrictor", "orifice", "needle valve", "one-way flow control", "drossel", "drosselventil", "stromventil", "drosselrückschlagventil", "blende" },
                Views.Symbol(15, 10, Throttle))
            {
                Params = new[] { Adjustable, WithCheck }, RefPrefix = "1V3",
                Ports = o =>
                {
                    var y = o.Flag("check", false) ? -2.5 : 0;
                    return new[] { new Port("P", -7.5, y), new Port("A", 7.5, y) };
                },
                CompanionIds = LineCompanions,
                Extra = o => new[]
                {
                    Toggle(o, "fluid.adjustable", T("Fixed / adjustable", "Fest / einstellbar"), "adjustable", false),
                    Toggle(o, "fluid.check", T("With bypass check valve", "Mit Rückschlagventil"), "check", false),
                },
            };

            yield return new FluidType("fluid.valve.relief", T("Pressure relief valve", "Druckbegrenzungsventil"), Cat,
                new[] { "pressure relief valve", "relief valve", "safety valve", "prv", "druckbegrenzungsventil", "dbv", "überdruckventil", "sicherheitsventil" },
                Views.Symbol(20, 15, (p, o) => PressureValve(p, o, normallyOpen: false, pilotFromInlet: true, drain: false)))
            {
                Params = new[] { AdjustableOn }, RefPrefix = "0V1",
                Ports = Fixed(new Port("P", 0, 7.5), new Port("T", 0, -7.5)),
                CompanionIds = LineCompanions,
                Extra = o => new[] { Toggle(o, "fluid.adjustable", T("Fixed / adjustable", "Fest / einstellbar"), "adjustable", true) },
            };

            yield return new FluidType("fluid.valve.reducing", T("Pressure reducing valve", "Druckminderventil"), Cat,
                new[] { "pressure reducing valve", "pressure regulator", "reducer", "druckminderventil", "druckregler", "druckregelventil" },
                Views.Symbol(20, 15, (p, o) => PressureValve(p, o, normallyOpen: true, pilotFromInlet: false, drain: false)))
            {
                Params = new[] { AdjustableOn }, RefPrefix = "1V4",
                Ports = Fixed(new Port("P", 0, 7.5), new Port("A", 0, -7.5)),
                CompanionIds = LineCompanions,
                Extra = o => new[] { Toggle(o, "fluid.adjustable", T("Fixed / adjustable", "Fest / einstellbar"), "adjustable", true) },
            };

            yield return new FluidType("fluid.valve.sequence", T("Sequence valve", "Zuschaltventil (Folgeventil)"), Cat,
                new[] { "sequence valve", "sequencing", "zuschaltventil", "folgeventil", "druckfolgeventil" },
                Views.Symbol(20, 15, (p, o) => PressureValve(p, o, normallyOpen: false, pilotFromInlet: true, drain: true)))
            {
                Params = new[] { AdjustableOn }, RefPrefix = "1V5",
                Ports = Fixed(new Port("P", 0, 7.5), new Port("A", 0, -7.5), new Port("L", 7.5, -7.5)),
                CompanionIds = LineCompanions,
                Extra = o => new[] { Toggle(o, "fluid.adjustable", T("Fixed / adjustable", "Fest / einstellbar"), "adjustable", true) },
            };

            yield return new FluidType("fluid.valve.shuttle", T("Shuttle valve (OR)", "Wechselventil (ODER)"), Cat,
                new[] { "shuttle valve", "or valve", "or", "wechselventil", "oder", "oder-ventil" },
                Views.Symbol(15, 10, (p, o) => Housing(p, o, "shuttle")))
            {
                RefPrefix = "1V6",
                Ports = Fixed(new Port("X", -7.5, 0), new Port("Y", 7.5, 0), new Port("A", 0, -5)),
                CompanionIds = LineCompanions,
            };

            yield return new FluidType("fluid.valve.twopressure", T("Two-pressure valve (AND)", "Zweidruckventil (UND)"), Cat,
                new[] { "two-pressure valve", "and valve", "and", "zweidruckventil", "und", "und-ventil" },
                Views.Symbol(15, 10, (p, o) => Housing(p, o, "and")))
            {
                RefPrefix = "1V7",
                Ports = Fixed(new Port("X", -7.5, 0), new Port("Y", 7.5, 0), new Port("A", 0, -5)),
                CompanionIds = LineCompanions,
            };

            yield return new FluidType("fluid.valve.quickexhaust", T("Quick exhaust valve", "Schnellentlüftungsventil"), Cat,
                new[] { "quick exhaust valve", "quick exhaust", "schnellentlüftungsventil", "schnellentlüfter", "entlüftung" },
                Views.Symbol(15, 10, (p, o) => Housing(p, o, "exhaust")))
            {
                RefPrefix = "1V8",
                Ports = Fixed(new Port("P", -7.5, 0), new Port("A", 7.5, 0), new Port("R", 0, 5)),
                CompanionIds = LineCompanions,
            };
        }

        /// <summary>
        /// Seat (V) with its vertex at design (vx, cy), opening in direction <paramref name="dir"/>, and the ball resting in
        /// it. Returns the x of the ball edge facing away from the seat. Free flow is from the vertex side.
        /// </summary>
        public static double Seat(Pen p, double vx, double cy, int dir, double r, double arm = 3.6)
        {
            const double half = 35 * Math.PI / 180;
            var t = Math.Tan(half);
            p.Line(vx, cy, vx + dir * arm, cy - arm * t, null, "seat");
            p.Line(vx, cy, vx + dir * arm, cy + arm * t, null, "seat");
            var bx = vx + dir * r / Math.Sin(half);
            p.Circle(bx, cy, r, null, "ball");
            return bx + dir * r;
        }

        /// <summary>Distance from the seat vertex to the ball centre for a ball of radius r.</summary>
        public static double SeatOffset(double r) => r / Math.Sin(35 * Math.PI / 180);

        private static void Check(Pen p, SketchObject o)
        {
            const double r = 1.8;
            var vx = -SeatOffset(r);
            p.Line(-7.5, 0, vx, 0, null, "line");
            var edge = Seat(p, vx, 0, 1, r);
            if (o.Flag("spring", false))
            {
                p.SpringH(edge, 5.2, 0, 1.4, 3);
                p.Line(5.2, 0, 7.5, 0, null, "line");
            }
            else p.Line(edge, 0, 7.5, 0, null, "line");
            if (o.Flag("piloted", false))
            {
                p.Line(0, 5, 0, r + 0.3, S.Pilot, "pilot");
                p.Rect(-4.6, -3.6, 5.8, 3.6, S.Thin, "piloted");
            }
        }

        /// <summary>Restriction: two bows facing each other around the line at height cy.</summary>
        private static void Bows(Pen p, double cy)
        {
            List<Vec2> Bow(int side)
            {
                var pts = new List<Vec2>();
                for (var i = 0; i <= 12; i++)
                {
                    var x = -2.5 + 5.0 * i / 12;
                    var u = x / 2.5;
                    pts.Add(new Vec2(x, cy + side * (0.6 + 1.1 * u * u)));
                }
                return pts;
            }
            p.PolyD(Bow(-1), null, false, "restriction");
            p.PolyD(Bow(1), null, false, "restriction");
        }

        private static void Throttle(Pen p, SketchObject o)
        {
            var check = o.Flag("check", false);
            var cy = check ? -2.5 : 0;
            p.Line(-7.5, cy, 7.5, cy, null, "line");
            Bows(p, cy);
            if (o.Flag("adjustable", false))
            {
                if (check) p.Adjust(-2.6, 0.4, 2.6, -4.8);
                else p.Adjust(-3, 3.6, 3, -3.6);
            }
            if (!check) return;
            // Bypass check valve (free flow from A to P) below the restriction.
            const double r = 1.2, by = 2.5;
            p.Line(-5, cy, -5, by, null, "bypass");
            p.Line(5, cy, 5, by, null, "bypass");
            var vx = SeatOffset(r);
            p.Line(5, by, vx, by, null, "bypass");
            var edge = Seat(p, vx, by, -1, r, 2.6);
            p.Line(edge, by, -5, by, null, "bypass");
            p.Dot(-5, cy, 0.5);
            p.Dot(5, cy, 0.5);
        }

        /// <summary>
        /// Pressure valve body: square box with ports on the axis (inlet below, outlet above), flow arrow offset (normally
        /// closed) or on the axis (normally open), pilot line on the left, spring on the right and optional drain line.
        /// </summary>
        private static void PressureValve(Pen p, SketchObject o, bool normallyOpen, bool pilotFromInlet, bool drain)
        {
            p.Rect(-5, -5, 5, 5, null, "box");
            p.Line(0, 5, 0, 7.5, null, "port");
            p.Line(0, -5, 0, -7.5, null, "port");
            var ax = normallyOpen ? 0 : -2.5;
            p.Arrow(ax, 5, ax, -5);
            var py = pilotFromInlet ? 6.25 : -6.25;
            p.Poly(S.Pilot, false, "pilot", 0, py, -7.5, py, -7.5, 0, -5, 0);
            p.SpringH(5, 9.5, 0, 2, 3);
            if (o.Flag("adjustable", true)) p.Adjust(5.6, 3.6, 9.4, -3.6);
            if (drain) p.Line(7.5, -2.4, 7.5, -7.5, S.Drain, "drain");
        }

        /// <summary>Logic valve housing with seats at both ends: shuttle (ball), two-pressure (spool) or quick exhaust (ball and exhaust).</summary>
        private static void Housing(Pen p, SketchObject o, string kind)
        {
            p.Rect(-5, -2.5, 5, 2.5, null, "housing");
            p.Line(-7.5, 0, -5, 0, null, "port");
            p.Line(5, 0, 7.5, 0, null, "port");
            // Seats: lips narrowing towards the inside at both ends.
            p.Line(-5, -2.5, -3.5, -1.0, null, "seat");
            p.Line(-5, 2.5, -3.5, 1.0, null, "seat");
            p.Line(5, -2.5, 3.5, -1.0, null, "seat");
            p.Line(5, 2.5, 3.5, 1.0, null, "seat");
            switch (kind)
            {
                case "shuttle":
                    p.Line(0, -2.5, 0, -5, null, "port");
                    p.Circle(-2.4, 0, 1.5, null, "ball");
                    break;
                case "and":
                    p.Line(0, -2.5, 0, -5, null, "port");
                    p.Line(-2.6, 0, 2.6, 0, null, "spool");
                    p.Line(-2.6, -1.3, -2.6, 1.3, S.Bold, "spool");
                    p.Line(2.6, -1.3, 2.6, 1.3, S.Bold, "spool");
                    break;
                default:
                    p.Circle(-2.4, 0, 1.5, null, "ball");
                    p.Line(0, 2.5, 0, 3.2, null, "port");
                    p.Triangle(0, 5, 0, 2.5, true, 1.8, 1.2, "exhaust");
                    break;
            }
        }
    }
}
