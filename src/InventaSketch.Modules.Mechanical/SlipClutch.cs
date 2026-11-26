using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Slip clutch (torque limiter, Rutschkupplung): two friction discs pressed together by a spring; it passes the torque of
    /// its shaft line up to the slip torque T_slip and slips above it. In the shaft line it is a pass-through member like a
    /// coupling (<see cref="MechValues.PassThrough"/>); the check <c>mech.clutch.slip</c> warns when the transmitted torque
    /// exceeds T_slip. Drawn in the style of the disc clutch (ISO 3952 style) with the pressure spring added.
    /// </summary>
    internal static class SlipClutch
    {
        public const string Id = "mech.clutch.slip";

        private static I18nText T(string en, string de) => new(en, de);

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType(Id, T("Slip clutch (torque limiter)", "Rutschkupplung"), Couplings.Cat,
                new[]
                {
                    "slip clutch", "torque limiter", "overload clutch", "friction clutch", "safety clutch", "slipping clutch",
                    "rutschkupplung", "überlastkupplung", "drehmomentbegrenzer", "sicherheitskupplung", "reibkupplung",
                    Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3,
                },
                new ViewSpec("stick", T("Schematic", "Schema"), 16, 20, Stick) { Bore = (p, o) => 2.5 * p.Sy },
                new ViewSpec("side", T("Side view", "Seitenansicht"), 20, 24, Side) { Bore = (p, o) => 3 * p.Sy })
            {
                CompanionTypes = new[] { "mech.shaft", Drivetrain.EMachine, "mech.gear.spur", "mech.pulley", "mech.sprocket" },
            };
        }

        /// <summary>
        /// Schematic: shaft stubs, the two friction discs with the lining between them, the pressure spring (zigzag) behind
        /// the right disc against a collar on the right shaft.
        /// </summary>
        private static void Stick(Pen p, SketchObject o)
        {
            p.Line(-8, 0, -1.2, 0, S.Heavy, "shaft");
            p.Line(1.2, 0, 8, 0, S.Heavy, "shaft");
            p.Line(-1.2, -7, -1.2, 7, S.Stick, "disc");
            p.Line(1.2, -7, 1.2, 7, S.Stick, "disc");
            p.Box(-1.2, -7, 1.2, -3.5, S.Dotted, "lining");
            p.Box(-1.2, 3.5, 1.2, 7, S.Dotted, "lining");
            foreach (var s in new[] { -1.0, 1.0 })
                p.PolyD(Frag.Zigzag(1.2, 6, s * 5, 0.9, 3), S.Vis, false, "spring");
            p.Line(6, -6.8, 6, 6.8, S.Stick, "collar");
        }

        /// <summary>Side view: discs with the lining, hubs, the spring pack and the adjusting nut on the right hub.</summary>
        private static void Side(Pen p, SketchObject o)
        {
            foreach (var sx in new[] { -1.0, 1.0 })
                p.Box(sx * 1.2, -11, sx * 3.2, 11, S.Vis, "disc");
            p.Box(-1.2, -11, 1.2, -6, S.Dotted, "lining");
            p.Box(-1.2, 6, 1.2, 11, S.Dotted, "lining");
            p.Box(-3.2, -4, -9.5, 4, S.Vis, "hub");
            p.Box(3.2, -4, 9.5, 4, S.Vis, "hub");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.PolyD(Frag.Zigzag(3.2, 7.4, s * 7.5, 1.2, 3), S.Vis, false, "spring");
                p.Box(7.4, s * 4, 9, s * 10, S.Vis, "nut");
            }
            p.Axis(10);
        }
    }
}
