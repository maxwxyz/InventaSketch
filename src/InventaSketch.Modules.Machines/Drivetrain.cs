using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Drivetrain schematic parts that sit on mechanical shafts: the electric machine as a gearbox-schematic symbol
    /// (rotor on the shaft, stator around it; schematic, side and axial view) and the internal combustion engine (block and
    /// crankshaft schematic with an output hub). Hub anchors let them snap onto a <c>mech.shaft</c> axis and follow the
    /// shaft through the core <see cref="Coupling"/> ("carried"), like gears and bearings of the mechanical module.
    /// </summary>
    internal static class Drivetrain
    {
        public const string Cat = "machines.drivetrain";

        /// <summary>Search keywords shared by all drivetrain parts (both languages).</summary>
        public const string Kw1 = "drivetrain", Kw2 = "powertrain", Kw3 = "antriebsstrang";

        private static I18nText T(string en, string de) => new(en, de);

        public static IEnumerable<MachType> Types()
        {
            yield return EMachineSchematic.Type();
            yield return Engine();
        }

        public const string EMachineId = EMachineSchematic.Id;
        public const string EngineId = "machines.drivetrain.engine";

        /// <summary>Stick schematic line weights, matching the mechanical module (gears 0.6 mm, shafts 0.7 mm).</summary>
        internal static readonly Style Stick = Style.Visible with { StrokeWidth = 0.6 };
        internal static readonly Style Heavy = Style.Visible with { StrokeWidth = 0.7 };

        // ================================================================== internal combustion engine

        private static readonly ParamDef Cylinders = Common.Count("cylinders", T("Cylinders (schematic)", "Zylinder (Schema)"), 4, 1, 6);

        /// <summary>Crank axis height and output shaft end in the 50 x 30 schematic design box.</summary>
        private const double CrankY = 6, OutX = 25;

        private static MachType Engine() => DriveBlocks.Block(
            EngineId, T("Internal combustion engine", "Verbrennungsmotor"),
            new[]
            {
                "engine", "ice", "internal combustion engine", "combustion engine", "petrol engine", "gasoline engine", "diesel engine",
                "crankshaft", "piston", "cylinder", "hybrid",
                "verbrennungsmotor", "verbrenner", "vkm", "ottomotor", "benzinmotor", "dieselmotor", "kurbelwelle", "kolben", "zylinder",
                Kw1, Kw2, Kw3,
            },
            EngineGlyph,
            new ViewSpec("schematic", T("Crankshaft schematic", "Kurbelwellenschema"), IconIds.Schematic, 50, 30, EngineSchematic)
            {
                Anchors = (p, o) => new[]
                {
                    Anchor.Point(AnchorRole.Hub, p.V(OutX, CrankY), "hub"),
                    Anchor.Point(AnchorRole.Port, p.V(OutX, CrankY), "out"),
                    Anchor.Segment(AnchorRole.Axis, p.V(-23, CrankY), p.V(OutX, CrankY)),
                    Anchor.Point(AnchorRole.Mount, p.V(-3.5, 13), "mount"),
                },
            },
            extraParams: new[] { Cylinders },
            companions: new[] { "mech.clutch.disc", "mech.flywheel", "mech.shaft", "machines.drive.gearbox", EMachineId, "mech.coupling.flexible" },
            category: Cat,
            label: T("Engine (ICE)", "Verbrennungsmotor"),
            // Output shaft at the right edge: the block follows a shaft that starts there.
            blockAnchors: (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(15, 0), "hub") });

        /// <summary>Block glyph: cylinder with piston, connecting rod and crank.</summary>
        private static void EngineGlyph(Pen p, SketchObject o)
        {
            var at = new Vec2(0, -4.2);
            DriveBlocks.GlyphLine(p, at, -2.4, -4.4, -2.4, 0.6);
            DriveBlocks.GlyphLine(p, at, 2.4, -4.4, 2.4, 0.6);
            DriveBlocks.GlyphLine(p, at, -2.4, -4.4, 2.4, -4.4);
            DriveBlocks.GlyphLine(p, at, -2.0, -2.8, 2.0, -2.8);
            DriveBlocks.GlyphLine(p, at, -2.0, -0.8, 2.0, -0.8);
            DriveBlocks.GlyphLine(p, at, -2.0, -2.8, -2.0, -0.8);
            DriveBlocks.GlyphLine(p, at, 2.0, -2.8, 2.0, -0.8);
            DriveBlocks.GlyphLine(p, at, 0, -1.8, 1.1, 2.6);
            DriveBlocks.GlyphCircle(p, at, 0, 2.6, 1.1);
        }

        /// <summary>
        /// Inline engine schematic: crankcase, cylinders with pistons and connecting rods, crankshaft with throws (outer and
        /// inner pins opposite, as in a four-cylinder engine) and the output shaft leaving on the right at the crank axis.
        /// </summary>
        private static void EngineSchematic(Pen p, SketchObject o)
        {
            var n = Math.Max(1, Math.Min(6, o.Int("cylinders", 4)));
            const double x0 = -21, x1 = 13, bore = -13.5, throwR = 3, rod = 11;
            var pitch = (x1 - x0) / n;
            p.RoundBox(-23, -14.5, 16, 13, 1.5, S.Vis, "crankcase");
            var crank = new List<Vec2> { new(-23, CrankY) };
            for (var i = 0; i < n; i++)
            {
                var cx = x0 + pitch * (i + 0.5);
                var hw = pitch * 0.32;
                // throws: outer cylinders up (top dead centre), inner ones down; single cylinder up, odd counts alternate
                var up = n == 4 ? i == 0 || i == 3 : i % 2 == 0;
                var pinY = CrankY + (up ? -throwR : throwR);
                var pistonY = pinY - rod;
                p.Line(cx - hw - 0.4, bore, cx - hw - 0.4, CrankY - throwR - 0.8, S.Thin, "cylinder");
                p.Line(cx + hw + 0.4, bore, cx + hw + 0.4, CrankY - throwR - 0.8, S.Thin, "cylinder");
                p.Line(cx - hw - 0.4, bore, cx + hw + 0.4, bore, S.Thin, "cylinder");
                p.Box(cx - hw, pistonY - 1.8, cx + hw, pistonY + 1.8, Stick with { Fill = Fill.Solid("#ffffff"), StrokeWidth = 0.4 }, "piston");
                p.Line(cx, pistonY, cx, pinY, Stick with { StrokeWidth = 0.45 }, "rod");
                var cw = pitch * 0.22;
                crank.Add(new Vec2(cx - cw, CrankY));
                crank.Add(new Vec2(cx - cw, pinY));
                crank.Add(new Vec2(cx + cw, pinY));
                crank.Add(new Vec2(cx + cw, CrankY));
            }
            crank.Add(new Vec2(16, CrankY));
            p.PolyD(crank, Stick, false, "crankshaft");
            p.Line(16, CrankY, OutX, CrankY, Heavy, "output");
            p.Axis(-23, OutX, CrankY);
        }
    }
}
