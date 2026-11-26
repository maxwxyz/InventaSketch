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

            yield return new FluidType("fluid.valve.pilotcheck", T("Pilot-operated check valve", "Entsperrbares Rückschlagventil"), Cat,
                new[] { "pilot operated check valve", "pilot check", "load holding valve", "double pilot check", "lock valve",
                    "entsperrbares rückschlagventil", "entsperrbar", "doppelt entsperrbares rückschlagventil", "sperrblock", "lasthalteventil" },
                new ViewSpec("symbol", T("Circuit symbol", "Schaltzeichen"), IconIds.Symbol, 15, 15, PilotCheck)
                {
                    DesignFor = o => new Size2(15, o.Flag("twin", false) ? 20 : 15),
                })
            {
                Params = new[] { Twin, SpringOn }, RefPrefix = "1V2",
                Ports = o => o.Flag("twin", false)
                    ? new[] { new Port("A1", -7.5, -5), new Port("A2", 7.5, -5), new Port("B1", -7.5, 5), new Port("B2", 7.5, 5) }
                    : new[] { new Port("P", -7.5, -2.5), new Port("A", 7.5, -2.5), new Port("X", 0, 7.5) },
                CompanionIds = new[] { "fluid.cylinder.double", "fluid.valve.directional", "fluid.line", "fluid.gauge" },
                Extra = o => new[]
                {
                    Toggle(o, "fluid.pilotcheck.twin", T("Single / double (cross-piloted)", "Einfach / doppelt (gegenseitig entsperrt)"), "twin", false),
                    Toggle(o, "fluid.spring", T("Spring loaded", "Federbelastet"), "spring", true),
                },
            };

            yield return new FluidType("fluid.valve.counterbalance", T("Counterbalance valve", "Senkbremsventil"), Cat,
                new[] { "counterbalance valve", "overcentre valve", "over-center valve", "load control valve", "brake valve",
                    "senkbremsventil", "lasthalteventil", "gegenhalteventil", "bremsventil", "senkbremse" },
                Views.Symbol(25, 15, Counterbalance))
            {
                Params = new[] { AdjustableOn }, RefPrefix = "1V9",
                Ports = Fixed(new Port("C", -2.5, 7.5), new Port("V", -2.5, -7.5), new Port("X", -12.5, -2.5)),
                CompanionIds = new[] { "fluid.cylinder.double", "fluid.motor", "fluid.valve.directional", "fluid.line" },
                Extra = o => new[] { Toggle(o, "fluid.adjustable", T("Fixed / adjustable", "Fest / einstellbar"), "adjustable", true) },
            };
        }

        private static readonly ParamDef Twin = Flag("twin", T("Double, cross-piloted (two channels)", "Doppelt, gegenseitig entsperrt (zwei Kanäle)"), false);
        private static readonly ParamDef SpringOn = Flag("spring", T("Spring loaded", "Federbelastet"), true);

        /// <summary>
        /// Check valve channel at height cy with the ball centred at x = 0 (free flow left to right) and an optional spring
        /// behind the ball.
        /// </summary>
        private static void CheckChannel(Pen p, double cy, bool spring)
        {
            const double r = 1.8;
            var vx = -SeatOffset(r);
            p.Line(-7.5, cy, vx, cy, null, "line");
            var edge = Seat(p, vx, cy, 1, r);
            if (spring)
            {
                p.SpringH(edge, 5.2, cy, 1.2, 3);
                p.Line(5.2, cy, 7.5, cy, null, "line");
            }
            else p.Line(edge, cy, 7.5, cy, null, "line");
        }

        /// <summary>
        /// Pilot-operated check valve: the check valve with its pilot piston, whose push rod lifts the ball off the seat when the
        /// pilot pressure at X rises. Double (twin): two channels A1→A2 and B1→B2, each unlocked by the pressure in the other
        /// one through a common floating pilot piston between them.
        /// </summary>
        private static void PilotCheck(Pen p, SketchObject o)
        {
            var spring = o.Flag("spring", true);
            if (o.Flag("twin", false))
            {
                CheckChannel(p, -5, spring);
                CheckChannel(p, 5, spring);
                // Floating pilot piston with push rods to both balls; each side is piloted from the other channel's inlet.
                p.Rect(-1.6, -1.0, 1.6, 1.0, S.Thin, "piston");
                p.Line(0, -1.0, 0, -5 + 1.8, S.Thin, "rod");
                p.Line(0, 1.0, 0, 5 - 1.8, S.Thin, "rod");
                p.Poly(S.Pilot, false, "pilot", -5.5, -5, -5.5, -0.5, -1.6, -0.5);
                p.Poly(S.Pilot, false, "pilot", -4.2, 5, -4.2, 0.5, -1.6, 0.5);
                p.Dot(-5.5, -5, 0.45);
                p.Dot(-4.2, 5, 0.45);
                p.Rect(-6.6, -7.6, 6.9, 7.6, S.Enclosure, "enclosure");
                return;
            }
            CheckChannel(p, -2.5, spring);
            // Pilot piston below the ball, its push rod against the ball, piloted from X.
            p.Rect(-1.8, 2.0, 1.8, 3.6, S.Thin, "piston");
            p.Line(0, 2.0, 0, -2.5 + 1.8, S.Thin, "rod");
            p.Line(0, 7.5, 0, 3.6, S.Pilot, "pilot");
            p.Rect(-5.0, -5.6, 6.0, 5.0, S.Thin, "piloted");
        }

        /// <summary>
        /// Counterbalance valve: a normally closed pressure valve between the load port C (below) and the valve port V (above),
        /// opened by the load pressure (internal pilot) and assisted by the external pilot X; spring on the right; a bypass
        /// check valve gives free flow from V to C (lifting the load).
        /// </summary>
        private static void Counterbalance(Pen p, SketchObject o)
        {
            const double cx = -2.5;
            p.Rect(cx - 5, -5, cx + 5, 5, null, "box");
            p.Line(cx, 5, cx, 7.5, null, "port");
            p.Line(cx, -5, cx, -7.5, null, "port");
            // Normally closed: flow arrow offset from the port axis (C → V).
            p.Arrow(cx - 2.5, 5, cx - 2.5, -5);
            // Internal pilot from the load port, external pilot from X.
            p.Poly(S.Pilot, false, "pilot", cx, 6.25, cx - 7.5, 6.25, cx - 7.5, 2.0, cx - 5, 2.0);
            p.Line(-12.5, -2.5, cx - 5, -2.5, S.Pilot, "pilot");
            p.SpringH(cx + 5, cx + 9, 0, 2, 3);
            if (o.Flag("adjustable", true)) p.Adjust(cx + 5.4, 3.6, cx + 8.8, -3.6);
            // Bypass check valve (free flow V → C, i.e. downwards) on the right.
            const double bx = 9.5, r = 1.3;
            p.Line(cx, 6.25, bx, 6.25, null, "bypass");
            p.Line(cx, -6.25, bx, -6.25, null, "bypass");
            // Seat vertex upstream (top), the V opening downwards with the ball resting in it.
            const double vy = -2.0, arm = 2.4;
            var t = Math.Tan(35 * Math.PI / 180);
            var by = vy + SeatOffset(r);
            p.Line(bx, -6.25, bx, vy, null, "bypass");
            p.Line(bx, vy, bx - arm * t, vy + arm, null, "seat");
            p.Line(bx, vy, bx + arm * t, vy + arm, null, "seat");
            p.Circle(bx, by, r, null, "ball");
            p.Line(bx, by + r, bx, 6.25, null, "bypass");
            p.Dot(cx, 6.25, 0.5);
            p.Dot(cx, -6.25, 0.5);
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
