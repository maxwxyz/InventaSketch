using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Couplings, clutches, brakes, synchronisers and flywheels (kinematic symbols in the style of ISO 3952).</summary>
    internal static class Couplings
    {
        public const string Cat = "mech.couplings";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSide = T("Side view", "Seitenansicht");

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType("mech.coupling.rigid", T("Rigid coupling", "Starre Kupplung"), Cat,
                new[] { "coupling", "rigid coupling", "flange coupling", "kupplung", "starre kupplung", "scheibenkupplung", "flanschkupplung" },
                new ViewSpec("stick", VStick, 12, 20, RigidStick),
                new ViewSpec("side", VSide, 20, 24, (p, o) => CouplingSide(p, false)));
            yield return new MechType("mech.coupling.flexible", T("Flexible coupling", "Elastische Kupplung"), Cat,
                new[] { "coupling", "flexible coupling", "elastic coupling", "kupplung", "elastische kupplung", "nachgiebige kupplung" },
                new ViewSpec("stick", VStick, 12, 20, FlexibleStick),
                new ViewSpec("side", VSide, 20, 24, (p, o) => CouplingSide(p, true)));
            yield return new MechType("mech.joint.universal", T("Universal joint", "Kardangelenk"), Cat,
                new[] { "universal joint", "cardan", "u-joint", "hooke", "kardangelenk", "kreuzgelenk", "gelenkwelle" },
                new ViewSpec("stick", VStick, 12, 20, UniversalStick));
            yield return new MechType("mech.clutch.disc", T("Disc clutch", "Scheibenkupplung (schaltbar)"), Cat,
                new[] { "clutch", "disc clutch", "friction clutch", "kupplung", "schaltkupplung", "reibungskupplung", "einscheibenkupplung" },
                new ViewSpec("stick", VStick, 12, 20, DiscClutchStick),
                new ViewSpec("side", VSide, 16, 24, DiscClutchSide));
            yield return new MechType("mech.clutch.multidisc", T("Multi-disc clutch", "Lamellenkupplung"), Cat,
                new[] { "clutch", "multi-disc", "multiplate", "wet clutch", "lamellenkupplung", "lamellen", "schaltkupplung" },
                new ViewSpec("stick", VStick, 20, 24, MultiDiscStick));
            yield return new MechType("mech.brake", T("Brake", "Bremse"), Cat,
                new[] { "brake", "disc brake", "holding brake", "bremse", "scheibenbremse", "lamellenbremse" },
                new ViewSpec("stick", VStick, 16, 20, BrakeStick),
                new ViewSpec("side", VSide, 16, 30, BrakeSide));
            yield return new MechType("mech.sync", T("Synchroniser / shift sleeve", "Synchronisierung / Schaltmuffe"), Cat,
                new[] { "synchroniser", "synchronizer", "shift sleeve", "dog clutch", "gear shift", "synchronisierung", "schaltmuffe", "klauenkupplung", "schaltgabel" },
                new ViewSpec("stick", VStick, 12, 24, SyncStick),
                new ViewSpec("side", VSide, 10, 16, SyncSide));
            yield return new MechType("mech.flywheel", T("Flywheel", "Schwungrad"), Cat,
                new[] { "flywheel", "inertia", "mass", "schwungrad", "schwungscheibe", "schwungmasse" },
                new ViewSpec("stick", VStick, 10, 40, FlywheelStick),
                new ViewSpec("axial", T("Axial view", "Stirnansicht"), 40, 40, FlywheelAxial),
                new ViewSpec("section", T("Section", "Schnitt"), 12, 40, FlywheelSection))
            {
                Params = new[] { Common.Hub },
                Extra = o => new[] { Common.HubCycle(o) },
            };
        }

        private static void Stubs(Pen p, double gap)
        {
            var hw = p.DW / 2;
            p.Line(-hw, 0, -gap, 0, S.Heavy, "shaft");
            p.Line(gap, 0, hw, 0, S.Heavy, "shaft");
        }

        private static void RigidStick(Pen p, SketchObject o)
        {
            Stubs(p, 1.5);
            p.Box(-1.5, -7, 1.5, 7, S.Stick, "flanges");
        }

        private static void FlexibleStick(Pen p, SketchObject o)
        {
            Stubs(p, 2.5);
            p.Line(-2.5, -7, -2.5, 7, S.Stick, "flange");
            p.Line(2.5, -7, 2.5, 7, S.Stick, "flange");
            foreach (var s in new[] { -1.0, 1.0 })
                p.PolyD(Frag.Zigzag(-2.5, 2.5, s * 5, 1.1, 2), S.Vis, false, "elastic");
        }

        private static void UniversalStick(Pen p, SketchObject o)
        {
            p.Line(-6, 0, -3.5, 0, S.Heavy, "shaft");
            p.Line(3.5, 0, 6, 0, S.Heavy, "shaft");
            p.Poly(S.Stick, false, 0.8, -3.5, -3.5, -3.5, -3.5, 3.5, 0.8, 3.5);
            p.Poly(S.Stick, false, -0.8, -2, 3.5, -2, 3.5, 2, -0.8, 2);
            p.Round(p.V(0, 0), 1.0, S.Vis, "spider");
        }

        private static void CouplingSide(Pen p, bool flexible)
        {
            var gap = flexible ? 2 : 0.25;
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                p.Box(sx * gap, -11, sx * 6, 11, S.Vis, "flange");
                p.Box(sx * 6, -5.5, sx * 9.5, 5.5, S.Vis, "hub");
            }
            if (flexible) p.Box(-gap, -9, gap, 9, S.Dotted, "elastic");
            p.Line(-7, -8, 7, -8, S.Ctr, "bolt");
            p.Line(-7, 8, 7, 8, S.Ctr, "bolt");
            p.Axis(10);
        }

        private static void DiscClutchStick(Pen p, SketchObject o)
        {
            Stubs(p, 1.2);
            p.Line(-1.2, -7, -1.2, 7, S.Stick, "disc");
            p.Line(1.2, -7, 1.2, 7, S.Stick, "disc");
        }

        private static void DiscClutchSide(Pen p, SketchObject o)
        {
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                p.Box(sx * 1.2, -11, sx * 3.2, 11, S.Vis, "disc");
                p.Box(sx * 3.2, -4, sx * 8, 4, S.Vis, "hub");
            }
            p.Box(-1.2, -11, -0.4, -6, S.Dotted, "lining");
            p.Box(-1.2, 6, -0.4, 11, S.Dotted, "lining");
            p.Axis(8);
        }

        /// <summary>Outer basket with plates interleaved with the plates of the inner hub.</summary>
        private static void MultiDiscStick(Pen p, SketchObject o)
        {
            p.Line(-10, 0, -7, 0, S.Heavy, "shaft");
            p.Line(7.5, 0, 10, 0, S.Heavy, "shaft");
            // outer basket
            p.Line(-7, -10, -7, 10, S.Stick, "basket");
            p.Line(-7, -10, 6, -10, S.Stick, "basket");
            p.Line(-7, 10, 6, 10, S.Stick, "basket");
            foreach (var x in new[] { -4.0, 0.0, 4.0 })
            {
                p.Line(x, -10, x, -3.5, S.Stick, "outer-plate");
                p.Line(x, 10, x, 3.5, S.Stick, "outer-plate");
            }
            // inner hub
            p.Line(7.5, -2, 7.5, 2, S.Stick, "hub");
            p.Line(-2, -2, 7.5, -2, S.Stick, "hub");
            p.Line(-2, 2, 7.5, 2, S.Stick, "hub");
            foreach (var x in new[] { -2.0, 2.0 })
            {
                p.Line(x, -2, x, -8, S.Stick, "inner-plate");
                p.Line(x, 2, x, 8, S.Stick, "inner-plate");
            }
        }

        private static void BrakeStick(Pen p, SketchObject o)
        {
            p.Line(-8, 0, -2, 0, S.Heavy, "shaft");
            p.Line(-2, -7, -2, 7, S.Stick, "disc");
            p.Line(1, -7, 1, 7, S.Stick, "shoe");
            p.Line(1, -5.5, 5, -5.5, S.Stick, "shoe");
            p.Line(1, 5.5, 5, 5.5, S.Stick, "shoe");
            var a = p.V(5, -8);
            var b = p.V(5, 8);
            p.LineP(a, b, S.Vis, "ground");
            Frag.GroundStrokes(p, a, b, new Vec2(1, 0), 2.6 * p.Sx, 1.6 * p.Sy);
        }

        private static void BrakeSide(Pen p, SketchObject o)
        {
            p.Box(-1, -11.5, 1, -4, S.Vis, "disc");
            p.Box(-1, 4, 1, 11.5, S.Vis, "disc");
            p.Box(-4, -4, 4, 4, S.Vis, "hub");
            p.Poly(S.Vis, true, -4.5, -12.5, 4.5, -12.5, 4.5, -6.5, 2.6, -6.5, 2.6, -12, -2.6, -12, -2.6, -6.5, -4.5, -6.5);
            p.Box(-2.6, -11, -1, -7, S.Dotted, "pad");
            p.Box(1, -11, 2.6, -7, S.Dotted, "pad");
            var a = p.V(-6, -12.5);
            var b = p.V(6, -12.5);
            p.LineP(a, b, S.Vis, "ground");
            Frag.GroundStrokes(p, a, b, new Vec2(0, -1), 2.4 * p.Sy, 1.6 * p.Sx);
            p.Line(-8, 0, 8, 0, S.Ctr, "axis");
        }

        /// <summary>Shift sleeve on the shaft with a stem and fork knob; dog teeth on both sides.</summary>
        private static void SyncStick(Pen p, SketchObject o)
        {
            p.Box(-3, -2.2, 3, 2.2, S.Stick, "sleeve");
            p.Line(0, -2.2, 0, -8.5, S.Stick, "stem");
            p.Round(p.V(0, -10.2), 1.6, S.Stick, "knob");
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                p.Line(sx * 4.6, -3.6, sx * 4.6, 3.6, S.Stick, "dog");
                p.Line(sx * 5.8, -3.6, sx * 5.8, 3.6, S.Stick, "dog");
            }
        }

        private static void SyncSide(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
                p.Poly(S.Vis, false, -4, s * 2.6, -4, s * 7, -1.4, s * 7, -1.4, s * 5.4, 1.4, s * 5.4, 1.4, s * 7, 4, s * 7, 4, s * 2.6);
            p.Line(-4, -2.6, 4, -2.6, S.Vis, "bore");
            p.Line(-4, 2.6, 4, 2.6, S.Vis, "bore");
            p.Line(-4, -3.6, 4, -3.6, S.Thin, "teeth");
            p.Line(-4, 3.6, 4, 3.6, S.Thin, "teeth");
            p.Axis(5);
        }

        private static void FlywheelStick(Pen p, SketchObject o)
        {
            p.Box(-4, -20, 4, -16, S.Black, "mass");
            p.Box(-4, 16, 4, 20, S.Black, "mass");
            Frag.Hub(p, p.V(0, -16), p.V(0, 0), p.V(0, 16), o.Str("hub", "fixed"), S.Stick);
        }

        private static void FlywheelAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 19, S.Vis, "rim");
            p.Circle(0, 0, 15.5, S.Vis, "rim");
            p.Circle(0, 0, 5, S.Vis, "hub");
            p.Circle(0, 0, 2.5, S.Vis, "bore");
            for (var i = 0; i < 4; i++)
            {
                var a = Math.PI / 4 + i * Math.PI / 2;
                var d = new Vec2(Math.Cos(a), Math.Sin(a));
                var n = d.Perp * 1.1;
                foreach (var off in new[] { n, -n })
                {
                    double r0 = Math.Sqrt(25 - 1.21), r1 = Math.Sqrt(15.5 * 15.5 - 1.21);
                    p.PolyD(new[] { d * r0 + off, d * r1 + off }, S.Vis, false, "spoke");
                }
            }
            p.CenterCross(0, 0, 20, 20);
        }

        private static void FlywheelSection(Pen p, SketchObject o)
        {
            var h = p.Hatch();
            foreach (var s in new[] { -1.0, 1.0 })
                p.Poly(h, true, -5, s * 19, 5, s * 19, 5, s * 15, 1.2, s * 15, 1.2, s * 5, 3, s * 5, 3, s * 2.5, -3, s * 2.5, -3, s * 5, -1.2, s * 5, -1.2, s * 15, -5, s * 15);
            p.Axis(6);
        }
    }
}
