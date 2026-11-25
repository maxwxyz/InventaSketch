using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Complete machines: radial-flux machine (inner or outer rotor, six rotor types) and axial-flux machine
    /// (single stator, double rotor, YASA style). Cross-section, longitudinal section and schematic symbol.
    /// </summary>
    internal static class MachineTypes
    {
        public const string Cat = "machines.machines";

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly I18nText VAxial = T("Cross-section", "Querschnitt");
        public static readonly I18nText VSide = T("Longitudinal section", "Längsschnitt");
        public static readonly I18nText VSchematic = T("Schematic symbol", "Schaltzeichen");

        public static IEnumerable<MachType> Types()
        {
            yield return Radial();
            yield return AxialFlux();
        }

        // ================================================================== radial-flux machine

        /// <summary>Design radii (box 100 x 100) of the radial machine.</summary>
        private static class RI
        {
            // inner rotor
            public const double HousingOut = 49, HousingIn = 44.5, StatorOut = 44.5, SlotBottom = 36.5, Bore = 27, RotorOut = 26, Shaft = 8;
            // outer rotor
            public const double ORotorOut = 46, ORotorIn = 37, OStatorOut = 36, OSlotBottom = 27, OStatorIn = 15, OAxle = 7;
        }

        private static MachType Radial() => new(
            "machines.radial", T("Radial-flux machine", "Radialflussmaschine"), Cat,
            new[]
            {
                "electric motor", "e-machine", "motor", "generator", "radial flux", "pmsm", "spm", "ipm", "eesm", "induction", "asm",
                "squirrel cage", "srm", "reluctance", "stator", "rotor", "cross-section", "outer rotor", "inner rotor",
                "elektromotor", "e-maschine", "radialfluss", "synchronmaschine", "asynchronmaschine", "käfigläufer", "reluktanz",
                "ständer", "läufer", "querschnitt", "außenläufer", "innenläufer", "fremderregt",
            },
            new ViewSpec("axial", VAxial, IconIds.Axial, 100, 100, RadialAxial) { Hub = true, Anchors = RadialAxialAnchors },
            new ViewSpec("side", VSide, IconIds.Section, 120, 100, RadialSide) { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-60, 0), p.V(60, 0)) } },
            new ViewSpec("schematic", VSchematic, IconIds.Schematic, 50, 50, (p, o) => Mag.MotorSymbol(p, o)) { Hub = true, Anchors = (p, o) => Mag.MotorSymbolAnchors(p, o) })
        {
            Scale = 0.6,
            Uniform = true,
            Params = new[]
            {
                Common.Rotor, Common.Layout, Common.Slots(36), Common.Poles(8), Common.Bars, Common.Winding, Common.Currents,
                Common.Housing, Common.Cooling, Common.Mode, Common.Phases, Common.Hatch,
            },
            Extra = RadialActions,
        };

        internal static IEnumerable<ContextAction> RadialActions(SketchObject o)
        {
            var schematic = o.View == "schematic";
            if (schematic)
            {
                yield return Common.ModeSwap(o);
                yield break;
            }
            yield return Common.RotorCycle(o);
            foreach (var a in Common.Stepper(o, "poles", T("Poles", "Pole"), 8, 2, 2, 64)) yield return a;
            foreach (var a in Common.Stepper(o, "slots", T("Slots", "Nuten"), 36, 3, 6, 96)) yield return a;
            yield return Common.Toggle(o, "currents", false, T("Current directions on/off", "Stromrichtungen an/aus"));
            yield return Common.Toggle(o, "cooling", false, T("Cooling channels on/off", "Kühlkanäle an/aus"));
            yield return Common.LayoutSwap(o);
        }

        private static int RotorPoles(SketchObject o, string kind)
        {
            var p = Common.PolesOf(o);
            return kind == "ipm" || kind == "spoke" || kind == "eesm" ? Math.Max(4, p) : p;
        }

        private static void RadialAxial(Pen p, SketchObject o)
        {
            var kind = Common.RotorKind(o);
            var q = Common.SlotsOf(o);
            var poles = RotorPoles(o, kind);
            var bars = Math.Max(8, Math.Min(96, o.Int("bars", 28)));
            var winding = o.Flag("winding", true);
            var currents = o.Flag("currents", false);
            var cooling = o.Flag("cooling", false);
            var open = kind == "srm";
            if (!Common.Outer(o))
            {
                if (o.Flag("housing", true)) Mag.HousingRing(p, o, RI.HousingIn, RI.HousingOut, cooling);
                Mag.Stator(p, o, q, RI.Bore, RI.SlotBottom, RI.StatorOut, open, poles, null, winding, currents);
                Mag.Rotor(p, o, kind, poles, bars, RI.RotorOut, RI.Shaft, currents);
                Mag.ShaftSection(p, o, RI.Shaft);
            }
            else
            {
                Mag.Rotor(p, o, kind, poles, bars, RI.ORotorIn, RI.ORotorOut, currents);
                Mag.Stator(p, o, q, RI.OStatorOut, RI.OSlotBottom, RI.OStatorIn, open, poles, null, winding, currents);
                // stationary stator carrier on the axle, optionally with coolant channels
                var segs = new List<PathSeg>(G.CircleSegs(RI.OStatorIn));
                segs.AddRange(G.CircleSegs(RI.OAxle, reverse: true));
                if (cooling) foreach (var h in Mag.RoundHoles((RI.OStatorIn + RI.OAxle) / 2, 1.6, 8)) segs.AddRange(G.Contour(h, reverse: true));
                p.PathD(segs, Mag.Iron(p, o, Mag.HousingHatch), "carrier");
                Mag.ShaftSection(p, o, RI.OAxle);
            }
            p.CenterCross(0, 0, 50, 50);
        }

        private static IEnumerable<Anchor> RadialAxialAnchors(Pen p, SketchObject o)
        {
            // Real air-gap radii: stator bore and rotor outer radius (outer rotor: stator outer and rotor inner radius). Their
            // radii are almost equal, so separate rotor/stator parts snap concentric, not like an internal gear mesh.
            if (!Common.Outer(o))
            {
                yield return Anchor.Circle(AnchorRole.PitchInner, Vec2.Zero, RI.Bore * p.K) with { Id = "stator" };
                yield return Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, RI.RotorOut * p.K) with { Id = "rotor" };
            }
            else
            {
                yield return Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, RI.OStatorOut * p.K) with { Id = "stator" };
                yield return Anchor.Circle(AnchorRole.PitchInner, Vec2.Zero, RI.ORotorIn * p.K) with { Id = "rotor" };
            }
        }

        private static void RadialSide(Pen p, SketchObject o)
        {
            var kind = Common.RotorKind(o);
            var winding = o.Flag("winding", true);
            var cooling = o.Flag("cooling", false);
            const double xs = 22;
            if (!Common.Outer(o))
            {
                var housing = o.Flag("housing", true);
                if (housing)
                {
                    Mag.HousingSide(p, o, 40, 36, RI.HousingOut, RI.HousingIn, 12, cooling);
                    Mag.Bearings(p, 36, 40, RI.Shaft, 12);
                }
                foreach (var sign in new[] { -1, 1 })
                {
                    Mag.Band(p, -xs, xs, winding ? RI.SlotBottom : RI.Bore, RI.StatorOut, sign, Mag.Iron(p, o, Mag.StatorHatch), "stator");
                    if (winding) p.PolyD(Mag.WindingWithEnds(xs, RI.Bore + 0.4, RI.SlotBottom, sign), p.Copper(6), true, "winding");
                }
                Mag.RotorSide(p, o, kind, -xs, xs, RI.RotorOut, RI.Shaft);
                p.Box(-46, -RI.Shaft, 57, RI.Shaft, S.Vis, "shaft");
                if (kind == "eesm") SlipRings(p, 45, RI.Shaft);
            }
            else
            {
                // stationary flange + carrier tube on the axle
                foreach (var sign in new[] { -1, 1 })
                {
                    var carrier = new List<Vec2>
                    {
                        new(-44, sign * 40), new(-40, sign * 40), new(-40, sign * RI.OStatorIn), new(xs, sign * RI.OStatorIn),
                        new(xs, sign * RI.OAxle), new(-44, sign * RI.OAxle),
                    };
                    var segs = new List<PathSeg>(G.Contour(carrier));
                    if (cooling)
                        for (var i = 0; i < 6; i++)
                        {
                            var cx = -35 + i * 9.5;
                            var r0 = RI.OAxle + 2.4; var r1 = RI.OStatorIn - 2.4;
                            segs.AddRange(G.Contour(new List<Vec2> { new(cx - 2, sign * r0), new(cx + 2, sign * r0), new(cx + 2, sign * r1), new(cx - 2, sign * r1) }, reverse: true));
                        }
                    p.PathD(segs, Mag.Iron(p, o, Mag.HousingHatch), "carrier");
                    Mag.Band(p, -xs, xs, RI.OStatorIn, winding ? RI.OSlotBottom : RI.OStatorOut, sign, Mag.Iron(p, o, Mag.StatorHatch), "stator");
                    if (winding) p.PolyD(Mag.WindingWithEnds(xs, RI.OSlotBottom, RI.OStatorOut - 0.4, sign), p.Copper(6), true, "winding");
                    // rotor bell: shell and end disc
                    var bell = new List<Vec2>
                    {
                        new(-32, sign * RI.ORotorOut), new(40, sign * RI.ORotorOut), new(40, sign * 12), new(36, sign * 12),
                        new(36, sign * RI.ORotorIn), new(-32, sign * RI.ORotorIn),
                    };
                    p.PolyD(bell, Mag.Iron(p, o, Mag.RotorHatch), true, "rotor");
                }
                Mag.RotorSide(p, o, kind, -xs, xs, RI.ORotorIn, RI.ORotorOut, core: false);
                Mag.Bearings(p, 36, 40, RI.OAxle, 12, mirrored: false);
                p.Box(-56, -RI.OAxle, 50, RI.OAxle, S.Vis, "shaft");
            }
            p.Axis(-60, 60);
        }

        /// <summary>Two slip rings with brushes on the shaft (excitation of a wound rotor), side view.</summary>
        private static void SlipRings(Pen p, double x, double rShaft)
        {
            foreach (var dx in new[] { 0.0, 5.0 })
            {
                p.Box(x + dx, -rShaft - 2.5, x + dx + 3, rShaft + 2.5, S.Thin with { Fill = Fill.Solid(S.CopperColor) }, "slipring");
                p.Box(x + dx + 0.4, -rShaft - 6, x + dx + 2.6, -rShaft - 2.5, S.Thin with { Fill = Fill.Solid("#555555") }, "brush");
            }
        }

        // ================================================================== axial-flux machine

        private static MachType AxialFlux() => new(
            "machines.axial", T("Axial-flux machine", "Axialflussmaschine"), Cat,
            new[]
            {
                "axial flux", "afpm", "yasa", "pancake motor", "disc motor", "double rotor", "magnet disc", "in-wheel",
                "axialfluss", "scheibenläufer", "doppelrotor", "magnetscheibe", "radnabenmotor", "elektromotor",
            },
            new ViewSpec("side", VSide, IconIds.Section, 80, 100, AxialSide) { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-40, 0), p.V(40, 0)) } },
            new ViewSpec("face", T("Face view of disc", "Stirnansicht Scheibe"), IconIds.Axial, 100, 100, AxialFace) { Hub = true, Anchors = (p, o) => new[] { Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, 46 * p.K) } },
            new ViewSpec("schematic", VSchematic, IconIds.Schematic, 50, 50, (p, o) => Mag.MotorSymbol(p, o)) { Hub = true, Anchors = (p, o) => Mag.MotorSymbolAnchors(p, o) })
        {
            Scale = 0.6,
            Params = new[]
            {
                Common.Poles(16), Common.Count("coils", T("Stator coils", "Statorspulen"), 18, 3, 96, 3),
                Common.Choice("disc", T("Face view shows", "Stirnansicht zeigt"), "rotor",
                    new ParamOption("rotor", T("Rotor magnet disc", "Rotor-Magnetscheibe")), new ParamOption("stator", T("Stator coil ring", "Stator-Spulenkranz"))),
                Common.Currents, Common.Housing, Common.Cooling, Common.Mode, Common.Phases, Common.Hatch,
            },
            Extra = o =>
            {
                var list = new List<ContextAction>();
                if (o.View == "schematic") { list.Add(Common.ModeSwap(o)); return list; }
                list.AddRange(Common.Stepper(o, "poles", T("Poles", "Pole"), 16, 2, 2, 64));
                list.AddRange(Common.Stepper(o, "coils", T("Coils", "Spulen"), 18, 3, 3, 96));
                list.Add(new ContextAction("param.disc", o.Str("disc", "rotor") == "rotor" ? T("Show stator disc", "Statorscheibe zeigen") : T("Show rotor disc", "Rotorscheibe zeigen"),
                    IconIds.Cycle, x => x.WithParam("disc", x.Str("disc", "rotor") == "rotor" ? "stator" : "rotor")) { Group = "param" });
                list.Add(Common.Toggle(o, "currents", false, T("Current directions on/off", "Stromrichtungen an/aus")));
                list.Add(Common.Toggle(o, "cooling", false, T("Cooling channels on/off", "Kühlkanäle an/aus")));
                return list;
            },
        };

        private static void AxialSide(Pen p, SketchObject o)
        {
            var currents = o.Flag("currents", false);
            if (o.Flag("housing", true))
            {
                Mag.HousingSide(p, o, 22, 18, 49, 45, 11, o.Flag("cooling", false), 6);
                Mag.Bearings(p, 18, 22, 6, 11);
            }
            var stator = Mag.Iron(p, o, Mag.StatorHatch);
            var rotor = Mag.Iron(p, o, Mag.RotorHatch);
            var copper = p.Copper(2.6);
            foreach (var sign in new[] { -1, 1 })
            {
                // stator segment (I-shaped core with pole shoes at both air gaps) and the coil around it
                var core = new List<Vec2>
                {
                    new(-6, sign * 38), new(-4.6, sign * 38), new(-4.6, sign * 35), new(4.6, sign * 35), new(4.6, sign * 38), new(6, sign * 38),
                    new(6, sign * 18), new(4.6, sign * 18), new(4.6, sign * 21), new(-4.6, sign * 21), new(-4.6, sign * 18), new(-6, sign * 18),
                };
                p.PolyD(core, stator, true, "stator");
                p.Box(-4.3, sign * 35.2, 4.3, sign * 37.8, copper, "coil");
                p.Box(-4.3, sign * 18.2, 4.3, sign * 20.8, copper, "coil");
                if (currents)
                {
                    p.Current(new Vec2(0, sign * 36.5), 1.0, sign < 0);
                    p.Current(new Vec2(0, sign * 19.5), 1.0, sign > 0);
                }
                // stator holder ring to the housing
                p.Box(-3, sign * 38, 3, sign * 45, Mag.Iron(p, o, Mag.HousingHatch), "holder");
                // two rotor discs: back iron on the shaft, magnets facing the stator (opposite polarity across the stator)
                foreach (var side in new[] { -1, 1 })
                {
                    p.Box(side * 9.5, sign * 6, side * 13, sign * 40, rotor, "rotor");
                    p.Box(side * 7, sign * 18.5, side * 9.5, sign * 37.5, S.Magnet((side < 0) == (sign < 0)), "magnet");
                }
            }
            p.Box(-40, -6, 40, 6, S.Vis, "shaft");
            p.Axis(-40, 40);
        }

        private static void AxialFace(Pen p, SketchObject o)
        {
            if (o.Str("disc", "rotor") == "stator")
            {
                var n = Math.Max(3, Math.Min(96, o.Int("coils", 18)));
                var pitch = 360.0 / n;
                p.Circle(0, 0, 46, S.Vis, "holder");
                p.Circle(0, 0, 44.5, S.Thin, "holder");
                var copper = p.Copper(4);
                for (var i = 0; i < n; i++)
                {
                    var c = i * pitch;
                    p.PolyD(G.Sector(17, 43.5, c - 0.44 * pitch, c + 0.44 * pitch), copper, true, "coil");
                    var core = G.Sector(21, 39.5, c - 0.25 * pitch, c + 0.25 * pitch);
                    p.PolyD(core, Mag.Underlay, true, "underlay");
                    p.PolyD(core, Mag.Iron(p, o, Mag.StatorHatch), true, "core");
                }
                p.Round(new Vec2(0, 0), 6, S.Vis, "shaft");
            }
            else
            {
                var poles = Common.PolesOf(o, 16);
                var alpha = 360.0 / poles;
                p.Circle(0, 0, 46, S.Vis, "rotor");
                for (var k = 0; k < poles; k++)
                {
                    var c = k * alpha;
                    p.PolyD(G.Sector(18, 43, c - 0.4 * alpha, c + 0.4 * alpha), S.Magnet(k % 2 == 0), true, "magnet");
                }
                var chord = 2 * 30.5 * Math.Sin(Geo.DegToRad(0.4 * alpha));
                var ts = Math.Min(6, 0.55 * chord);
                if (ts >= 2.2)
                    for (var k = 0; k < poles; k++)
                    {
                        var at = G.P(30.5, k * alpha);
                        p.Add(Prim.Text(at.X * p.Sx, at.Y * p.Sy, k % 2 == 0 ? "N" : "S", new TextStyle { Size = ts * p.K, Bold = true, Color = k % 2 == 0 ? "#ffffff" : "#000000" }), "pole");
                    }
                p.Round(new Vec2(0, 0), 9, S.Vis, "hub");
                p.Round(new Vec2(0, 0), 6, S.Vis, "shaft");
            }
            p.CenterCross(0, 0, 50, 50);
        }
    }
}
