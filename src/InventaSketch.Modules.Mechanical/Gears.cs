using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Gears and gear sets. Stick views follow the kinematic schematic conventions (VDI 2127 / ISO 3952 style),
    /// side/section/axial views the conventional representation of gears (ISO 2203 style): solid without teeth,
    /// pitch surface as thin dash-dot line, root line in section only, teeth unhatched in axial sections.
    /// </summary>
    internal static class Gears
    {
        public const string Cat = "mech.gears";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSide = T("Side view", "Seitenansicht");
        private static readonly I18nText VSection = T("Section", "Schnitt");
        private static readonly I18nText VAxial = T("Axial view", "Stirnansicht");
        private static readonly I18nText VTeeth = T("With teeth", "Mit Zähnen");

        private static string Hub(SketchObject o) => o.Str("hub", "fixed");
        private static bool BoreOn(SketchObject o) => o.Flag("bore", true);

        private static Anchor Pitch(Pen p, double r) => Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, r * p.K);
        private static Anchor PitchIn(Pen p, double r) => Anchor.Circle(AnchorRole.PitchInner, Vec2.Zero, r * p.K);

        /// <summary>
        /// Pitch circle of a gear seen from the side or as a schematic stick (axis horizontal in the box): the radius is the
        /// vertical half extent of the pitch lines (stick: the symbol height), so gears on parallel shafts mesh, snap and
        /// stay coupled in these views as in the axial view.
        /// </summary>
        private static Anchor SidePitch(Pen p, double halfHeight) => Anchor.SidePitch(Vec2.Zero, halfHeight * p.Sy, new Vec2(1, 0));

        /// <summary>Inner pitch circle of a ring gear seen from the side (axis horizontal in the box), like <see cref="SidePitch"/>.</summary>
        private static Anchor SidePitchIn(Pen p, double halfHeight)
            => new(AnchorKind.Circle, AnchorRole.PitchInner, Vec2.Zero, new Vec2(1, 0), halfHeight * p.Sy) { Id = "pitch-side" };

        public static IEnumerable<MechType> Types()
        {
            yield return Spur();
            yield return Internal();
            yield return Bevel();
            yield return Worm();
            yield return WormWheel();
            yield return Rack();
            yield return Planetary();
            yield return Ravigneaux();
            yield return Carrier();
            yield return Differential();
            yield return DiffCase();
        }

        // ------------------------------------------------------------------ spur gear

        private const double SpurTip = 14; // axial views: tip radius in a 30 x 30 design box

        private static MechType Spur() => new(
            "mech.gear.spur", T("Spur gear", "Stirnrad"), Cat,
            new[] { "gear", "spur", "cog", "toothed wheel", "pinion", "zahnrad", "stirnrad", "ritzel", "verzahnung", "getriebe", "gearbox", "transmission", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
            new ViewSpec("stick", VStick, 8, 30, (p, o) => GearStick(p, o, 4, 15)) { Anchors = (p, o) => new[] { SidePitch(p, 15) }, Bore = (p, o) => StickBore(p, o, 18) },
            new ViewSpec("side", VSide, 10, 30, SpurSide) { Anchors = (p, o) => new[] { SidePitch(p, 15 * GearMath.External(GearMath.Teeth(o, 18)).Pitch) }, Bore = SideBore },
            new ViewSpec("section", VSection, 10, 30, SpurSection) { Anchors = (p, o) => new[] { SidePitch(p, 15 * GearMath.External(GearMath.Teeth(o, 18)).Pitch) }, Bore = SideBore },
            new ViewSpec("axial", VAxial, 30, 30, SpurAxial) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 18)).Pitch) }, Bore = AxialBore },
            new ViewSpec("teeth", VTeeth, 30, 30, SpurTeeth) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 18)).Pitch) }, Bore = AxialBore })
        {
            Params = new[] { Common.Hub, Common.Teeth(18), Common.Module, Common.Helix, Common.Bore, Common.Keyway },
            Extra = o => GearActions(o, true, 18, true),
            // Inserted from the bar in the gear's view: a meshing gear, its shaft, bearings beside it on the shaft.
            CompanionTypes = new[] { "mech.gear.spur", "mech.shaft", "mech.carrier", "mech.gear.internal", "mech.bearing.ball" },
            // Teeth view: tooth 0 points up (GearMath.Outline phase -90 deg).
            Teeth = o => o.View == "teeth" ? (GearMath.Teeth(o, 18), -90.0) : null,
        };

        /// <summary>Bore radius of the side and section views (design 4.5 of the tip radius 15, across the axis).</summary>
        private const double SideBoreR = 4.5, AxialBoreR = 4.2;

        private static double SideBore(Pen p, SketchObject o) => SideBoreR * p.Sy;

        /// <summary>Round bore of the axial views (stays round: detail scale).</summary>
        private static double AxialBore(Pen p, SketchObject o) => AxialBoreR * p.K;

        /// <summary>
        /// Schematic bore: the stick symbol shows no bore. A view switch keeps the pitch circle (stick: the symbol height,
        /// side view: the pitch lines), so the schematic takes the side view's bore relative to the pitch circle and the fit
        /// carries over between the views.
        /// </summary>
        private static double StickBore(Pen p, SketchObject o, int defTeeth) => SideBoreR / GearMath.External(GearMath.Teeth(o, defTeeth)).Pitch * p.Sy;

        /// <summary>Hub cycle, helix toggle and (if a module is set) true size from module.</summary>
        private static IEnumerable<ContextAction> GearActions(SketchObject o, bool hub, int defTeeth, bool helix)
        {
            if (hub) yield return Common.HubCycle(o);
            if (helix) yield return Common.Toggle(o, "helix", false, T("Helical teeth on/off", "Schrägverzahnung an/aus"), IconIds.Helix);
            if (o.Num("module", 0) > 0)
                yield return new ContextAction("param.truesize", T("True size from module", "Echte Größe aus Modul"), "true-size", x => TrueSize(x, defTeeth)) { Group = "param" };
        }

        /// <summary>Sets the box so the tip diameter equals m (z + 2). Axial views stay round, side views keep their width.</summary>
        private static SketchObject TrueSize(SketchObject o, int defTeeth)
        {
            var m = o.Num("module", 0);
            if (m <= 0) return o;
            var da = m * (GearMath.Teeth(o, defTeeth) + 2);
            var v = o.View ?? "stick";
            if (v is "axial" or "teeth") { var s = da * 15 / SpurTip; return o with { Size = new Size2(s, s) }; }
            return o with { Size = new Size2(o.Size.W, da) };
        }

        /// <summary>Gear in the stick schematic: vertical line with end bars ("I"), hub symbol on the shaft axis.</summary>
        internal static void GearStick(Pen p, SketchObject o, double hw, double hh)
        {
            p.Line(-hw, -hh, hw, -hh, S.Stick, "tip");
            p.Line(-hw, hh, hw, hh, S.Stick, "tip");
            Frag.Hub(p, p.V(0, -hh), p.V(0, 0), p.V(0, hh), Hub(o), S.Stick);
        }

        private static void SpurSide(Pen p, SketchObject o)
        {
            var (pr, _) = GearMath.External(GearMath.Teeth(o, 18));
            p.Box(-4, -15, 4, 15, S.Vis, "tip");
            p.Line(-5, -15 * pr, 5, -15 * pr, S.Ctr, "pitch");
            p.Line(-5, 15 * pr, 5, 15 * pr, S.Ctr, "pitch");
            p.Axis(5);
            if (BoreOn(o))
            {
                p.Line(-4, -4.5, 4, -4.5, S.Hid, "bore");
                p.Line(-4, 4.5, 4, 4.5, S.Hid, "bore");
                if (o.Flag("keyway", false)) p.Line(-4, -6.2, 4, -6.2, S.Hid, "keyway");
            }
            if (o.Flag("helix", false))
                foreach (var dx in new[] { -2.0, 0.0, 2.0 })
                    p.Line(dx - 1.3, -7.5, dx + 1.3, -12.5, S.Thin, "helix");
        }

        private static void SpurSection(Pen p, SketchObject o)
        {
            var (pr, rr) = GearMath.External(GearMath.Teeth(o, 18));
            var rf = 15 * rr;
            p.Box(-4, -15, 4, 15, S.Vis, "tip");
            if (BoreOn(o))
            {
                var top = o.Flag("keyway", false) ? 6.2 : 4.5;
                p.Box(-4, -rf, 4, -top, p.Hatch(), "body");
                p.Box(-4, 4.5, 4, rf, p.Hatch(), "body");
            }
            else p.Box(-4, -rf, 4, rf, p.Hatch(), "body");
            p.Line(-5, -15 * pr, 5, -15 * pr, S.Ctr, "pitch");
            p.Line(-5, 15 * pr, 5, 15 * pr, S.Ctr, "pitch");
            p.Axis(5);
        }

        private static void SpurAxial(Pen p, SketchObject o)
        {
            var (pr, rr) = GearMath.External(GearMath.Teeth(o, 18));
            p.Circle(0, 0, SpurTip, S.Vis, "tip");
            p.Circle(0, 0, SpurTip * rr, S.Thin, "root");
            p.Circle(0, 0, SpurTip * pr, S.Ctr, "pitch");
            if (BoreOn(o)) Frag.Bore(p, 4.2, o.Flag("keyway", false));
            p.CenterCross(0, 0, 15, 15);
        }

        private static void SpurTeeth(Pen p, SketchObject o)
        {
            var z = GearMath.Teeth(o, 18);
            var (pr, rr) = GearMath.External(z);
            p.PolyD(GearMath.Outline(z, SpurTip * rr, SpurTip, SpurTip * pr, -Math.PI / 2), S.Vis, true, "teeth");
            p.Circle(0, 0, SpurTip * pr, S.Ctr, "pitch");
            if (BoreOn(o)) Frag.Bore(p, 4.2, o.Flag("keyway", false));
            p.CenterCross(0, 0, 15, 15);
        }

        // ------------------------------------------------------------------ internal (ring) gear

        private const double RingRim = 19, RingRoot = 16; // 40 x 40 design box

        private static MechType Internal() => new(
            "mech.gear.internal", T("Ring gear (internal)", "Hohlrad"), Cat,
            new[] { "ring gear", "internal gear", "annulus", "hohlrad", "innenverzahnung", "planetengetriebe" },
            new ViewSpec("stick", VStick, 10, 40, RingStick) { Anchors = (p, o) => new[] { SidePitchIn(p, 12) } },
            new ViewSpec("axial", VAxial, 40, 40, RingAxial) { Anchors = (p, o) => new[] { PitchIn(p, RingRoot * GearMath.Internal(GearMath.Teeth(o, 48)).Pitch) } },
            new ViewSpec("teeth", VTeeth, 40, 40, RingTeeth) { Anchors = (p, o) => new[] { PitchIn(p, RingRoot * GearMath.Internal(GearMath.Teeth(o, 48)).Pitch) } },
            new ViewSpec("section", VSection, 10, 40, RingSection) { Anchors = (p, o) => new[] { SidePitchIn(p, RingRoot * GearMath.Internal(GearMath.Teeth(o, 48)).Pitch) } })
        {
            Params = new[] { Common.Teeth(48), Common.Module },
            Extra = o => GearActions(o, false, 48, false),
            CompanionTypes = new[] { "mech.gear.spur", "mech.carrier" },
        };

        /// <summary>Ring gear schematic: opposing brackets, tooth bar facing the axis.</summary>
        private static void RingStick(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Poly(S.Stick, false, -4, s * 20, -4, s * 12, 4, s * 12, 4, s * 20);
            }
        }

        private static void RingAxial(Pen p, SketchObject o)
        {
            var (pr, tr) = GearMath.Internal(GearMath.Teeth(o, 48));
            p.Circle(0, 0, RingRim, S.Vis, "rim");
            p.Circle(0, 0, RingRoot, S.Thin, "root");
            p.Circle(0, 0, RingRoot * tr, S.Vis, "tip");
            p.Circle(0, 0, RingRoot * pr, S.Ctr, "pitch");
            p.CenterCross(0, 0, 20, 20);
        }

        private static void RingTeeth(Pen p, SketchObject o)
        {
            var z = GearMath.Teeth(o, 48);
            var (pr, tr) = GearMath.Internal(z);
            p.Circle(0, 0, RingRim, S.Vis, "rim");
            p.PolyD(GearMath.Outline(z, RingRoot, RingRoot * tr, RingRoot * pr, -Math.PI / 2), S.Vis, true, "teeth");
            p.Circle(0, 0, RingRoot * pr, S.Ctr, "pitch");
            p.CenterCross(0, 0, 20, 20);
        }

        private static void RingSection(Pen p, SketchObject o)
        {
            var (pr, tr) = GearMath.Internal(GearMath.Teeth(o, 48));
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Box(-4, s * RingRim, 4, s * RingRoot, p.Hatch(), "body");
                p.Box(-4, s * RingRoot, 4, s * RingRoot * tr, S.Vis, "teeth");
                p.Line(-5, s * RingRoot * pr, 5, s * RingRoot * pr, S.Ctr, "pitch");
            }
            p.Axis(5);
        }

        // ------------------------------------------------------------------ bevel gear

        private static MechType Bevel() => new(
            "mech.gear.bevel", T("Bevel gear", "Kegelrad"), Cat,
            new[] { "bevel", "bevel gear", "mitre", "angle drive", "kegelrad", "kegelradgetriebe", "winkelgetriebe" },
            new ViewSpec("stick", VStick, 12, 30, BevelStick) { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Apex, p.V(10.6, 0), "apex") }, Bore = BevelBore },
            new ViewSpec("side", VSide, 24, 30, (p, o) => BevelBody(p, o, false)) { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Apex, p.V(11.5, 0), "apex") }, Bore = BevelBore },
            new ViewSpec("section", VSection, 24, 30, (p, o) => BevelBody(p, o, true)) { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Apex, p.V(11.5, 0), "apex") }, Bore = BevelBore })
        {
            Params = new[] { Common.Hub, Common.Teeth(24), Common.Module, Common.Bore },
            Extra = o => GearActions(o, true, 24, false),
            // Mating bevel gears (angle drive, differential side and spider gears), the shaft, a carrier as the cage.
            CompanionTypes = new[] { "mech.gear.bevel", "mech.shaft", "mech.carrier", "mech.bearing.taperroller" },
        };

        /// <summary>Bore of the bevel gear (design 3 across the axis; the schematic has the same height and takes the same).</summary>
        private static double BevelBore(Pen p, SketchObject o) => 3 * p.Sy;

        /// <summary>Bevel gear schematic: gear line with slanted tooth bars pointing to the cone apex on the axis.</summary>
        private static void BevelStick(Pen p, SketchObject o)
        {
            p.Line(-3.4, -14, 2.6, -8, S.Stick, "tip");
            p.Line(-3.4, 14, 2.6, 8, S.Stick, "tip");
            Frag.Hub(p, p.V(-3.4, -14), p.V(-3.4, 0), p.V(-3.4, 14), Hub(o), S.Stick);
        }

        private static void BevelBody(Pen p, SketchObject o, bool section)
        {
            // Cone apex at (11.5, 0); back face at x = -11; hub extends to the right.
            const double xb = -11, xf = -2, apex = 11.5;
            double TipY(double x) => -15 * (apex - x) / (apex - xb);
            double RootY(double x) => -11.5 * (apex - x) / (apex - xb);
            double PitchY(double x) => -13.2 * (apex - x) / (apex - xb);
            const double hub = 4.5, bore = 3;
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (section)
                {
                    var rb = BoreOn(o) ? bore : 0;
                    p.Poly(p.Hatch(), true, xb, s * -RootY(xb), xf, s * -RootY(xf), xf, s * hub, 11, s * hub, 11, s * rb, xb, s * rb);
                    p.Poly(S.Vis, true, xb, s * -TipY(xb), xf, s * -TipY(xf), xf, s * -RootY(xf), xb, s * -RootY(xb));
                }
                p.Line(xb - 0.5, s * -PitchY(xb - 0.5), 11.4, s * -PitchY(11.4), S.Ctr, "pitch");
            }
            if (!section)
            {
                p.Poly(S.Vis, true, xb, TipY(xb), xf, TipY(xf), xf, -hub, 11, -hub, 11, hub, xf, hub, xf, -TipY(xf), xb, -TipY(xb));
                if (BoreOn(o)) { p.Line(xb, -bore, 11, -bore, S.Hid, "bore"); p.Line(xb, bore, 11, bore, S.Hid, "bore"); }
            }
            p.Axis(12);
        }

        // ------------------------------------------------------------------ worm and worm wheel

        // The worm is cut on its own shaft: no bore, it keeps its size on a shaft.
        private static MechType Worm() => new(
            "mech.gear.worm", T("Worm", "Schnecke"), Cat,
            new[] { "worm", "worm drive", "screw gear", "schnecke", "schneckengetriebe", "schneckenwelle" },
            new ViewSpec("side", VSide, 30, 12, WormSide) { Anchors = (p, o) => new[] { Pitch(p, 4.2) } },
            new ViewSpec("stick", VStick, 30, 12, WormStick) { Anchors = (p, o) => new[] { Pitch(p, 4.2) } },
            new ViewSpec("axial", VAxial, 12, 12, WormAxial) { Anchors = (p, o) => new[] { Pitch(p, 4.2) } })
        {
            Params = new[] { Common.Count("starts", T("Number of starts", "Gangzahl"), 1, 1, 8), Common.Module },
        };

        private static void WormSide(Pen p, SketchObject o)
        {
            p.Box(-13, -5.5, 13, 5.5, S.Vis, "tip");
            p.Line(-15, -4.2, 15, -4.2, S.Ctr, "pitch");
            p.Line(-15, 4.2, 15, 4.2, S.Ctr, "pitch");
            p.Line(-13, -3.2, 13, -3.2, S.Thin, "root");
            p.Line(-13, 3.2, 13, 3.2, S.Thin, "root");
            p.Axis(15);
        }

        private static void WormStick(Pen p, SketchObject o)
        {
            p.Box(-11, -4.5, 11, 4.5, S.Stick, "tip");
            for (var i = 0; i < 6; i++)
            {
                var x = -9.5 + i * 3.8;
                p.Line(x - 1.2, 4.5, x + 1.2, -4.5, S.Stick, "thread");
            }
            p.Axis(15);
        }

        private static void WormAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 5.5, S.Vis, "tip");
            p.Circle(0, 0, 4.2, S.Ctr, "pitch");
            p.Hole(0, 0, 1.8, S.Vis, "bore");
            p.CenterCross(0, 0, 6, 6);
        }

        private static MechType WormWheel() => new(
            "mech.gear.wormwheel", T("Worm wheel", "Schneckenrad"), Cat,
            new[] { "worm wheel", "worm gear", "schneckenrad", "schneckengetriebe" },
            new ViewSpec("stick", VStick, 8, 30, (p, o) => GearStick(p, o, 4, 15)) { Bore = SideBore },
            new ViewSpec("side", VSide, 10, 30, WormWheelSide) { Bore = SideBore },
            new ViewSpec("axial", VAxial, 30, 30, SpurAxial) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 30)).Pitch) }, Bore = AxialBore },
            new ViewSpec("teeth", VTeeth, 30, 30, SpurTeeth) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 30)).Pitch) }, Bore = AxialBore })
        {
            Params = new[] { Common.Hub, Common.Teeth(30), Common.Module, Common.Bore },
            Extra = o => GearActions(o, true, 30, false),
        };

        /// <summary>Worm wheel side view: concave throat at the tip.</summary>
        private static void WormWheelSide(Pen p, SketchObject o)
        {
            var (pr, _) = GearMath.External(GearMath.Teeth(o, 30));
            // Throat: circular arc centred on the (imaginary) worm axis, 1.5 deep, ending at the box corners.
            const double r = 6.083, c = 19.583;
            var outline = new List<Vec2>();
            for (var i = 0; i <= 16; i++) { var x = -4 + i * 0.5; outline.Add(new Vec2(x, -c + Math.Sqrt(r * r - x * x))); }
            for (var i = 16; i >= 0; i--) { var x = -4 + i * 0.5; outline.Add(new Vec2(x, c - Math.Sqrt(r * r - x * x))); }
            p.PolyD(outline, S.Vis, true, "tip");
            p.Line(-5, -15 * pr + 1.2, 5, -15 * pr + 1.2, S.Ctr, "pitch");
            p.Line(-5, 15 * pr - 1.2, 5, 15 * pr - 1.2, S.Ctr, "pitch");
            if (BoreOn(o)) { p.Line(-4, -4.5, 4, -4.5, S.Hid, "bore"); p.Line(-4, 4.5, 4, 4.5, S.Hid, "bore"); }
            p.Axis(5);
        }

        // ------------------------------------------------------------------ rack

        private static MechType Rack() => new(
            "mech.gear.rack", T("Gear rack", "Zahnstange"), Cat,
            new[] { "rack", "rack and pinion", "linear gear", "zahnstange", "zahnstangengetriebe" },
            new ViewSpec("teeth", VTeeth, 60, 10, (p, o) => RackDraw(p, o, true)) { Fluid = true, Anchors = RackAnchors },
            new ViewSpec("side", VSide, 60, 10, (p, o) => RackDraw(p, o, false)) { Fluid = true, Anchors = RackAnchors },
            new ViewSpec("stick", VStick, 60, 10, RackStick) { Fluid = true, Anchors = RackAnchors })
        {
            Params = new[] { Common.Module },
            Rotating = false,
        };

        private static IEnumerable<Anchor> RackAnchors(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.End, new Vec2(-p.W / 2, 0), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(p.W / 2, 0), "right");
        }

        /// <summary>Rack in actual mm: tooth pitch from the module if set, otherwise from the height.</summary>
        private static void RackDraw(Pen p, SketchObject o, bool teeth)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var depth = Math.Min(p.H * 0.4, 5);
            var m = o.Num("module", 0);
            var pitch = m > 0 ? Math.PI * m : Math.Max(1.5, depth * 2.2);
            var top = -hh;
            var rootY = top + depth;
            var pitchY = top + depth * 0.45;
            if (teeth)
            {
                var n = Math.Max(1, (int)Math.Floor(p.W / pitch));
                var x0 = -n * pitch / 2;
                var pts = new List<Vec2> { new(-hw, rootY), new(x0, rootY) };
                for (var i = 0; i < n; i++)
                {
                    var x = x0 + i * pitch;
                    pts.Add(new Vec2(x + pitch * 0.12, rootY));
                    pts.Add(new Vec2(x + pitch * 0.32, top));
                    pts.Add(new Vec2(x + pitch * 0.58, top));
                    pts.Add(new Vec2(x + pitch * 0.78, rootY));
                    pts.Add(new Vec2(x + pitch, rootY));
                }
                pts.Add(new Vec2(hw, rootY));
                pts.Add(new Vec2(hw, hh));
                pts.Add(new Vec2(-hw, hh));
                p.PolyP(pts, S.Vis, true, "teeth");
            }
            else
            {
                p.Add(Prim.Rect(-hw, top, p.W, p.H, S.Vis), "tip");
                p.LineP(new Vec2(-hw, rootY), new Vec2(hw, rootY), S.Thin, "root");
            }
            p.LineP(new Vec2(-hw, pitchY), new Vec2(hw, pitchY), S.Ctr, "pitch");
        }

        /// <summary>Rack schematic: body line with a row of tooth strokes.</summary>
        private static void RackStick(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var y = hh * 0.3;
            p.LineP(new Vec2(-hw, y), new Vec2(hw, y), S.Stick, "body");
            var step = Math.Max(1.5, p.H * 0.35);
            var n = Math.Max(1, (int)Math.Floor((p.W - step) / step));
            var x0 = -(n - 1) * step / 2;
            for (var i = 0; i < n; i++)
            {
                var x = x0 + i * step;
                p.LineP(new Vec2(x - step * 0.25, y), new Vec2(x, -hh * 0.8), S.Stick, "teeth");
                p.LineP(new Vec2(x, -hh * 0.8), new Vec2(x + step * 0.25, y), S.Stick, "teeth");
            }
        }

        // ------------------------------------------------------------------ planetary set

        // Axial design 50 x 50: normalised module 0.72 mm, ring 52 / sun 20 / planet 16 teeth.
        private const double PM = 0.72, PRing = PM * 52 / 2, PSun = PM * 20 / 2, PPlanet = PM * 16 / 2, PCarrier = PSun + PPlanet, PRim = 24;

        private static MechType Planetary() => new(
            "mech.gearset.planetary", T("Planetary gear set", "Planetenradsatz"), Cat,
            new[] { "planetary", "epicyclic", "sun", "planet", "carrier", "ring", "planetengetriebe", "umlaufgetriebe", "sonnenrad", "planetenrad", "steg", "hohlrad" },
            // Axial views: the sun's bore (the set sits on the sun shaft). The schematic shows the members on separate
            // shafts and has no single bore: it keeps its size.
            new ViewSpec("stick", VStick, 30, 50, PlanetStick),
            new ViewSpec("axial", VAxial, 50, 50, (p, o) => PlanetAxial(p, o, false)) { Anchors = PlanetAnchors, Bore = (p, o) => 2.6 * p.K },
            new ViewSpec("teeth", VTeeth, 50, 50, (p, o) => PlanetAxial(p, o, true)) { Anchors = PlanetAnchors, Bore = (p, o) => 2.6 * p.K })
        {
            Params = new[]
            {
                Common.Count("planets", T("Number of planets", "Anzahl Planeten"), 3, 2, 8),
                Common.Flag("stepped", T("Stepped planets", "Stufenplaneten"), false),
            },
            Extra = o => Common.Stepper(o, "planets", T("Planets", "Planeten"), 3, 1, 2, 8)
                .Append(Common.Toggle(o, "stepped", false, T("Stepped planets on/off", "Stufenplaneten an/aus"), IconIds.Toggle)),
            // Built from parts (sun, planets, ring, carrier) with "Explode" in the hosts.
            CompanionTypes = new[] { "mech.shaft", "mech.carrier", "mech.gear.spur", "mech.gear.internal" },
            PartsOf = PlanetaryParts,
        };

        /// <summary>
        /// The set as parts in the axial views: sun (20 teeth), planets (16) on a carrier with as many pins, ring gear (52);
        /// sizes from the set's box (design 50 x 50). Stepped planets are not exploded; the schematic view is exploded through
        /// the axial view and switched back (<see cref="Assemblies"/>, <see cref="AssemblyViews"/>).
        /// </summary>
        private static IReadOnlyList<PartSpec>? PlanetaryParts(SketchObject o)
        {
            var view = o.View ?? "stick";
            if (view is not ("axial" or "teeth") || Stepped(o)) return null;
            var k = Math.Min(o.Size.W, o.Size.H) / 50;
            var n = Planets(o);
            var list = new List<PartSpec>
            {
                new("mech.carrier", "axial", Vec2.Zero) { PinRadius = PCarrier * k, Params = new Dictionary<string, ParamValue> { ["arms"] = n } },
                new("mech.gear.spur", view, Vec2.Zero) { Pitch = PSun * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 20 } },
                new("mech.gear.internal", view, Vec2.Zero) { InnerPitch = PRing * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 52 } },
            };
            for (var i = 0; i < n; i++)
            {
                var c = Frag.Polar(PCarrier * k, -Math.PI / 2 + i * 2 * Math.PI / n);
                list.Add(new("mech.gear.spur", view, c) { Pitch = PPlanet * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 16 } });
            }
            return list;
        }

        private static IEnumerable<Anchor> PlanetAnchors(Pen p, SketchObject o)
        {
            yield return PitchIn(p, PRing);
            yield return Pitch(p, PSun);
        }

        private static int Planets(SketchObject o) => Math.Max(2, Math.Min(8, o.Int("planets", 3)));

        /// <summary>Stepped planets: a second, smaller gear stage on each planet pin meshes with a smaller ring.</summary>
        private static bool Stepped(SketchObject o) => o.Flag("stepped", false);

        /// <summary>Pitch radius of the second planet stage and the ring radius it meshes with (stepped planets).</summary>
        private const double PStep = PPlanet * 0.6, PRingStep = PCarrier + PStep;

        /// <summary>Stick schematic of a simple planetary set: sun on the central shaft, planets on a U-shaped carrier, ring brackets.</summary>
        private static void PlanetStick(Pen p, SketchObject o)
        {
            const double x = 4, fw = 3, carrierX = -8;
            // sun with its shaft to the right
            p.Line(x, 0, 15, 0, S.Heavy, "shaft");
            p.Line(x - fw, -PSun, x + fw, -PSun, S.Stick, "sun");
            p.Line(x - fw, PSun, x + fw, PSun, S.Stick, "sun");
            Frag.Hub(p, p.V(x, -PSun), p.V(x, 0), p.V(x, PSun), "fixed", S.Stick);
            // carrier with its shaft to the left
            p.Line(-15, 0, carrierX, 0, S.Heavy, "shaft");
            p.Line(carrierX, -PCarrier, carrierX, PCarrier, S.Stick, "carrier");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var yc = s * PCarrier;
                p.Line(carrierX, yc, x + fw * 0.5, yc, S.Stick, "carrier");
                if (Stepped(o))
                {
                    // Stage 1 (big) meshes with the sun, stage 2 (small) with the ring, both on one pin.
                    p.Line(x - fw, s * PSun, x, s * PSun, S.Stick, "planet");
                    p.Line(x - fw, s * PRing, x, s * PRing, S.Stick, "planet");
                    Frag.Hub(p, p.V(x - fw * 0.5, s * PSun), p.V(x - fw * 0.5, yc), p.V(x - fw * 0.5, s * PRing), "loose", S.Stick);
                    p.Line(x + 0.6, s * (PCarrier - PStep), x + fw + 1.2, s * (PCarrier - PStep), S.Stick, "planet");
                    p.Line(x + 0.6, s * PRingStep, x + fw + 1.2, s * PRingStep, S.Stick, "planet");
                    p.Line(x + fw * 0.6 + 0.6, s * (PCarrier - PStep), x + fw * 0.6 + 0.6, s * PRingStep, S.Stick, "planet");
                    p.Poly(S.Stick, false, x, s * PRim, x, s * (PRingStep + 0.9), x + fw + 2, s * (PRingStep + 0.9), x + fw + 2, s * PRim);
                    continue;
                }
                p.Line(x - fw, s * PSun, x + fw, s * PSun, S.Stick, "planet");
                p.Line(x - fw, s * PRing, x + fw, s * PRing, S.Stick, "planet");
                Frag.Hub(p, p.V(x, s * PSun), p.V(x, yc), p.V(x, s * PRing), "loose", S.Stick);
                // ring gear bracket
                p.Poly(S.Stick, false, x - fw - 1, s * PRim, x - fw - 1, s * (PRing + 0.9), x + fw + 1, s * (PRing + 0.9), x + fw + 1, s * PRim);
            }
            p.Axis(15);
        }

        private static void PlanetAxial(Pen p, SketchObject o, bool teeth)
        {
            var n = Planets(o);
            var planetAngles = Enumerable.Range(0, n).Select(i => -Math.PI / 2 + i * 2 * Math.PI / n).ToList();
            if (teeth)
            {
                const int zs = 20, zp = 16, zr = 52;
                double ps = 2 * Math.PI / zs, pp = 2 * Math.PI / zp, prr = 2 * Math.PI / zr;
                // sun: tooth at angle 0 (screen right)
                p.PolyD(GearMath.Outline(zs, PSun - 1.25 * PM, PSun + PM, PSun, 0), S.Vis, true, "sun");
                foreach (var phi in planetAngles)
                {
                    // Planet tooth must face the sun gap nearest to the contact direction.
                    var gap = (Math.Round(phi / ps - 0.5) + 0.5) * ps;
                    var delta = (gap - phi) * PSun;
                    var psi = phi + Math.PI - delta / PPlanet;
                    var c = Frag.Polar(PCarrier, phi);
                    p.PolyD(GearMath.Outline(zp, PPlanet - 1.25 * PM, PPlanet + PM, PPlanet, psi).Select(q => q + c), S.Vis, true, "planet");
                    p.Hole(c.X, c.Y, 1.6, S.Vis, "pin");
                }
                // ring: tooth where the first planet has a gap (contact on the outside of planet 0)
                {
                    var phi = planetAngles[0];
                    var gap = (Math.Round(phi / ps - 0.5) + 0.5) * ps;
                    var psi = phi + Math.PI - (gap - phi) * PSun / PPlanet;
                    // planet gap nearest to direction phi (outwards)
                    var rel = (phi - psi) / pp;
                    var gapAngle = psi + (Math.Round(rel - 0.5) + 0.5) * pp;
                    var d = (gapAngle - phi) * PPlanet;
                    var rho = phi + d / PRing;
                    p.PolyD(GearMath.Outline(zr, PRing + 1.25 * PM, PRing - PM, PRing, rho), S.Vis, true, "ring");
                    _ = prr;
                }
                p.Circle(0, 0, PRim, S.Vis, "rim");
            }
            else
            {
                p.Circle(0, 0, PRim, S.Vis, "rim");
                p.Circle(0, 0, PRing - PM, S.Vis, "ring");
                p.Circle(0, 0, PSun + PM, S.Vis, "sun");
                p.Circle(0, 0, PSun, S.Ctr, "pitch");
                foreach (var phi in planetAngles)
                {
                    var c = Frag.Polar(PCarrier, phi);
                    p.Circle(c.X, c.Y, PPlanet + PM, S.Vis, "planet");
                    p.Circle(c.X, c.Y, PPlanet, S.Ctr, "pitch");
                    if (Stepped(o)) p.Circle(c.X, c.Y, PStep + PM, S.Hid, "planet");
                    p.Hole(c.X, c.Y, 1.6, S.Vis, "pin");
                }
            }
            p.Circle(0, 0, PRing, S.Ctr, "pitch");
            p.Circle(0, 0, PCarrier, S.Ctr, "carrier");
            p.Hole(0, 0, 2.6, S.Vis, "bore");
            p.CenterCross(0, 0, 25, 25);
        }

        // ------------------------------------------------------------------ Ravigneaux set

        // Axial design 50 x 50, module PM: ring 52, large sun 20, long planet 16 (as the simple set), small sun 14, short
        // planet 10. Long planets mesh with the large sun, the ring and a short planet; short planets with the small sun.
        private const double RvSmallSun = PM * 14 / 2, RvShort = PM * 10 / 2, RvShortPin = RvSmallSun + RvShort;

        /// <summary>Angle (rad, clockwise) from a long planet's pin to its short planet's pin, so the two mesh.</summary>
        private static double RvPairAngle(double longPin, double shortPin, double contact)
            => Math.Acos((longPin * longPin + shortPin * shortPin - contact * contact) / (2 * longPin * shortPin));

        private static readonly double RvDelta = RvPairAngle(PCarrier, RvShortPin, RvShort + PPlanet);

        private static MechType Ravigneaux() => new(
            "mech.gearset.ravigneaux", T("Ravigneaux gear set", "Ravigneaux-Radsatz"), Cat,
            new[] { "ravigneaux", "planet pairs", "compound planetary", "automatic transmission", "two suns", "planetenpaare", "ravigneaux-satz", "automatikgetriebe", "zwei sonnen" },
            new ViewSpec("stick", VStick, 34, 50, RavigneauxStick),
            new ViewSpec("axial", VAxial, 50, 50, RavigneauxAxial) { Anchors = PlanetAnchors, Bore = (p, o) => 2.6 * p.K })
        {
            Params = new[] { Common.Count("pairs", T("Planet pairs", "Planetenpaare"), 3, 2, 3) },
            Extra = o => Common.Stepper(o, "pairs", T("Planet pairs", "Planetenpaare"), 3, 1, 2, 3),
            CompanionTypes = new[] { "mech.shaft", "mech.carrier", "mech.gear.spur", "mech.gear.internal" },
            PartsOf = RavigneauxParts,
        };

        private static int Pairs(SketchObject o) => Math.Max(2, Math.Min(3, o.Int("pairs", 3)));

        /// <summary>
        /// The set as parts in the axial view: a carrier with paired pins (long planets outside, short planets inside), the
        /// large sun (20), the small sun (14), the ring (52), the long (16) and short (10) planets; the schematic view explodes
        /// through the axial view.
        /// </summary>
        private static IReadOnlyList<PartSpec>? RavigneauxParts(SketchObject o)
        {
            if ((o.View ?? "stick") != "axial") return null;
            var k = Math.Min(o.Size.W, o.Size.H) / 50;
            var n = Pairs(o);
            var list = new List<PartSpec>
            {
                new("mech.carrier", "axial", Vec2.Zero)
                {
                    PinRadius = PCarrier * k,
                    Params = new Dictionary<string, ParamValue>
                    {
                        ["arms"] = n, ["paired"] = true, ["innerRatio"] = Math.Round(RvShortPin / PCarrier, 6),
                        ["innerAngle"] = Math.Round(RvDelta * 180 / Math.PI, 6),
                    },
                },
                new("mech.gear.spur", "axial", Vec2.Zero) { Pitch = PSun * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 20 } },
                new("mech.gear.spur", "axial", Vec2.Zero) { Pitch = RvSmallSun * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 14 } },
                new("mech.gear.internal", "axial", Vec2.Zero) { InnerPitch = PRing * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 52 } },
            };
            for (var i = 0; i < n; i++)
            {
                var phi = -Math.PI / 2 + i * 2 * Math.PI / n;
                list.Add(new("mech.gear.spur", "axial", Frag.Polar(PCarrier * k, phi)) { Pitch = PPlanet * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 16 } });
                list.Add(new("mech.gear.spur", "axial", Frag.Polar(RvShortPin * k, phi + RvDelta)) { Pitch = RvShort * k, Params = new Dictionary<string, ParamValue> { ["teeth"] = 10 } });
            }
            return list;
        }

        private static void RavigneauxAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, PRim, S.Vis, "rim");
            p.Circle(0, 0, PRing - PM, S.Vis, "ring");
            p.Circle(0, 0, PRing, S.Ctr, "pitch");
            // Large sun in front, small sun behind it (hidden).
            p.Circle(0, 0, PSun + PM, S.Vis, "sun");
            p.Circle(0, 0, PSun, S.Ctr, "pitch");
            p.Circle(0, 0, RvSmallSun + PM, S.Hid, "sun2");
            var n = Pairs(o);
            for (var i = 0; i < n; i++)
            {
                var phi = -Math.PI / 2 + i * 2 * Math.PI / n;
                var cl = Frag.Polar(PCarrier, phi);
                var cs = Frag.Polar(RvShortPin, phi + RvDelta);
                p.Circle(cl.X, cl.Y, PPlanet + PM, S.Vis, "planet");
                p.Circle(cl.X, cl.Y, PPlanet, S.Ctr, "pitch");
                p.Hole(cl.X, cl.Y, 1.6, S.Vis, "pin");
                p.Circle(cs.X, cs.Y, RvShort + PM, S.Vis, "planet2");
                p.Circle(cs.X, cs.Y, RvShort, S.Ctr, "pitch");
                p.Hole(cs.X, cs.Y, 1.2, S.Vis, "pin");
            }
            p.Circle(0, 0, PCarrier, S.Ctr, "carrier");
            p.Circle(0, 0, RvShortPin, S.Ctr, "carrier");
            p.Hole(0, 0, 2.6, S.Vis, "bore");
            p.CenterCross(0, 0, 25, 25);
        }

        /// <summary>
        /// Stick schematic: small sun (shaft to the left) with the short planets, the long planets meshing with the short
        /// ones, the large sun (shaft to the right) and the ring; the carrier holds both pins (hollow shaft to the left).
        /// </summary>
        private static void RavigneauxStick(Pen p, SketchObject o)
        {
            const double xs = -7, xl = 6, fw = 2.5, carrierX = -12.5;
            // Small sun on the central shaft to the left, large sun on the shaft to the right.
            p.Line(-17, 0, xs, 0, S.Heavy, "shaft");
            p.Line(xs - fw, -RvSmallSun, xs + fw, -RvSmallSun, S.Stick, "sun2");
            p.Line(xs - fw, RvSmallSun, xs + fw, RvSmallSun, S.Stick, "sun2");
            Frag.Hub(p, p.V(xs, -RvSmallSun), p.V(xs, 0), p.V(xs, RvSmallSun), "fixed", S.Stick);
            p.Line(xl, 0, 17, 0, S.Heavy, "shaft");
            p.Line(xl - fw, -PSun, xl + fw, -PSun, S.Stick, "sun");
            p.Line(xl - fw, PSun, xl + fw, PSun, S.Stick, "sun");
            Frag.Hub(p, p.V(xl, -PSun), p.V(xl, 0), p.V(xl, PSun), "fixed", S.Stick);
            // Carrier on a hollow shaft around the small sun's shaft.
            p.Line(-17, -1.4, carrierX, -1.4, S.Stick, "carrier");
            p.Line(-17, 1.4, carrierX, 1.4, S.Stick, "carrier");
            p.Line(carrierX, -PCarrier, carrierX, PCarrier, S.Stick, "carrier");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                // Short planet on its pin, meshing with the small sun and the long planet.
                var ys = s * RvShortPin;
                p.Line(carrierX, ys, xs + fw * 0.5, ys, S.Stick, "carrier");
                p.Line(xs - fw, s * RvSmallSun, xs + fw, s * RvSmallSun, S.Stick, "planet2");
                p.Line(xs - fw, s * (RvShortPin + RvShort), xs + fw, s * (RvShortPin + RvShort), S.Stick, "planet2");
                Frag.Hub(p, p.V(xs, s * RvSmallSun), p.V(xs, ys), p.V(xs, s * (RvShortPin + RvShort)), "loose", S.Stick);
                // Long planet: from the short planet across to the large sun and the ring.
                var yl = s * PCarrier;
                p.Line(carrierX, yl, xl + fw * 0.5, yl, S.Stick, "carrier");
                p.Line(xs - fw, s * (PCarrier - PPlanet), xl + fw, s * (PCarrier - PPlanet), S.Stick, "planet");
                p.Line(xl - fw, s * PRing, xl + fw, s * PRing, S.Stick, "planet");
                Frag.Hub(p, p.V(xl, s * PSun), p.V(xl, yl), p.V(xl, s * PRing), "loose", S.Stick);
                // Ring gear bracket.
                p.Poly(S.Stick, false, xl - fw - 1, s * PRim, xl - fw - 1, s * (PRing + 0.9), xl + fw + 1, s * (PRing + 0.9), xl + fw + 1, s * PRim);
            }
            p.Axis(17);
        }

        // ------------------------------------------------------------------ planet carrier

        // Axial design 50 x 50: pins on a circle of radius 18; schematic 20 x 50 with the pins at +-18.
        private const double CarrierR = 18;

        private static MechType Carrier() => new(
            "mech.carrier", T("Planet carrier", "Planetenträger (Steg)"), Cat,
            new[] { "carrier", "planet carrier", "spider", "arm", "steg", "planetenträger", "planetensteg", "umlaufgetriebe" },
            new ViewSpec("stick", VStick, 20, 50, CarrierStick) { Anchors = CarrierStickAnchors },
            new ViewSpec("axial", VAxial, 50, 50, CarrierAxial) { Anchors = CarrierAxialAnchors, Bore = (p, o) => 2.2 * p.K })
        {
            Params = new[]
            {
                Common.Count("arms", T("Planet pins", "Planetenbolzen"), 3, 2, 8),
                Common.Flag("disc", T("Disc instead of arms", "Scheibe statt Arme"), false),
                Common.Flag("fixed", T("Fixed carrier (grounded)", "Fester Steg (gestellfest)"), false),
                Common.Flag("paired", T("Paired pins (planet pairs, Ravigneaux)", "Bolzenpaare (Planetenpaare, Ravigneaux)"), false),
                new ParamDef("innerRatio", T("Inner pin radius / outer", "Innerer / äußerer Bolzenkreis"), ParamKind.Number) { Default = 0.6667, Min = 0.3, Max = 0.95, Step = 0.01, Advanced = true },
                new ParamDef("innerAngle", T("Inner pin offset (°)", "Versatz innerer Bolzen (°)"), ParamKind.Number) { Default = 46.2, Min = 5, Max = 90, Step = 0.5, Unit = "°", Advanced = true },
            },
            Extra = o => Common.Stepper(o, "arms", T("Pins", "Bolzen"), 3, 1, 2, 8)
                .Append(Common.Toggle(o, "fixed", false, T("Fixed carrier on/off", "Fester Steg an/aus"), IconIds.Ground)),
            // A sun gear goes in the centre, planets on the pins, a ring gear around them.
            CompanionTypes = new[] { "mech.gear.spur", "mech.gear.internal", "mech.shaft", "mech.bearing.ball" },
        };

        private static int Arms(SketchObject o) => Math.Max(2, Math.Min(8, o.Int("arms", 3)));

        private static List<double> ArmAngles(SketchObject o) => Enumerable.Range(0, Arms(o)).Select(i => -Math.PI / 2 + i * 2 * Math.PI / Arms(o)).ToList();

        /// <summary>Inner pins of a carrier with paired pins (radius, angle per arm), else none.</summary>
        private static IEnumerable<Vec2> InnerPins(SketchObject o)
        {
            if (!o.Flag("paired", false)) yield break;
            var r = CarrierR * Math.Max(0.3, Math.Min(0.95, o.Num("innerRatio", 0.6667)));
            var d = o.Num("innerAngle", 46.2) * Math.PI / 180;
            foreach (var a in ArmAngles(o)) yield return Frag.Polar(r, a + d);
        }

        private static void CarrierAxial(Pen p, SketchObject o)
        {
            if (o.Flag("disc", false)) p.Circle(0, 0, CarrierR + 3.5, S.Vis, "disc");
            foreach (var a in ArmAngles(o))
            {
                var c = Frag.Polar(CarrierR, a);
                var n = Frag.Polar(1, a + Math.PI / 2);
                if (!o.Flag("disc", false))
                {
                    // Arm: two lines from the hub to the pin boss.
                    p.Line(n.X * 2.2, n.Y * 2.2, c.X + n.X * 2.2, c.Y + n.Y * 2.2, S.Vis, "arm");
                    p.Line(-n.X * 2.2, -n.Y * 2.2, c.X - n.X * 2.2, c.Y - n.Y * 2.2, S.Vis, "arm");
                }
                p.Hole(c.X, c.Y, 2.6, S.Vis, "boss");
                p.Hole(c.X, c.Y, 1.2, S.Vis, "pin");
            }
            foreach (var c in InnerPins(o))
            {
                // Inner pin of a pair: its own short arm from the hub.
                var n = new Vec2(-c.Y, c.X) * (1.6 / Math.Max(1e-9, c.Length));
                if (!o.Flag("disc", false))
                {
                    p.Line(n.X, n.Y, c.X + n.X, c.Y + n.Y, S.Vis, "arm");
                    p.Line(-n.X, -n.Y, c.X - n.X, c.Y - n.Y, S.Vis, "arm");
                }
                p.Hole(c.X, c.Y, 2.2, S.Vis, "boss");
                p.Hole(c.X, c.Y, 1.0, S.Vis, "pin");
            }
            p.Hole(0, 0, 4.2, S.Vis, "hub");
            p.Hole(0, 0, 2.2, S.Vis, "bore");
            p.Circle(0, 0, CarrierR, S.Ctr, "pitch");
            if (o.Flag("fixed", false)) Frag.GroundStrokes(p, p.V(-6, 8), p.V(6, 8), new Vec2(0, 1), 1.6, 1.6);
        }

        private static IEnumerable<Anchor> CarrierAxialAnchors(Pen p, SketchObject o)
            => ArmAngles(o).Select((a, i) => Anchor.Point(AnchorRole.Hub, p.V(Frag.Polar(CarrierR, a)), "pin" + i))
                // Inner pins of pairs follow the outer ones ("pin<n+i>"), so "pin0" stays on the outer circle.
                .Concat(InnerPins(o).Select((c, i) => Anchor.Point(AnchorRole.Hub, p.V(c), "pin" + (Arms(o) + i))));

        private static void CarrierStick(Pen p, SketchObject o)
        {
            // Carrier: vertical bar, pins to the right, stub shaft to the left.
            p.Line(-6, -CarrierR, -6, CarrierR, S.Stick, "carrier");
            foreach (var s in new[] { -1.0, 1.0 })
                p.Line(-6, s * CarrierR, 8, s * CarrierR, S.Stick, "pin");
            p.Line(-10, 0, -6, 0, S.Heavy, "shaft");
            if (o.Flag("fixed", false)) Frag.GroundStrokes(p, p.V(-10, 0), p.V(-10, 6), new Vec2(-1, 0), 1.6, 1.6);
            p.Axis(10);
        }

        /// <summary>Schematic carrier: the two pins are axes (planets sit on them).</summary>
        private static IEnumerable<Anchor> CarrierStickAnchors(Pen p, SketchObject o) => new[]
        {
            Anchor.Segment(AnchorRole.Axis, p.V(-6, -CarrierR), p.V(8, -CarrierR)) with { Id = "pin0" },
            Anchor.Segment(AnchorRole.Axis, p.V(-6, CarrierR), p.V(8, CarrierR)) with { Id = "pin1" },
        };

        // ------------------------------------------------------------------ differential

        private static MechType Differential() => new(
            "mech.gearset.differential", T("Bevel differential", "Kegelraddifferential"), Cat,
            new[] { "differential", "diff", "axle", "spider gear", "side gear", "crown wheel", "differential", "ausgleichsgetriebe", "achsgetriebe", "tellerrad", "ausgleichsrad", "final drive", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
            new ViewSpec("stick", VStick, 40, 40, DiffStick),
            new ViewSpec("section", VSection, 40, 40, DiffSection))
        {
            // The differential sits on the axle shaft; the wheels sit on its half shafts.
            CompanionTypes = new[] { "mech.shaft", Drivetrain.Wheel, "mech.gear.bevel", "mech.gear.spur", "mech.bearing.taperroller" },
            // Built from parts with "Explode": case with cross pin and crown wheel, two side gears, two spider gears.
            PartsOf = DifferentialParts,
        };

        /// <summary>
        /// The differential as parts in the stick view: the case (cage, cross pin, crown wheel), the two side gears on the
        /// half shafts and the two spider gears on the pin, all bevel gears meeting at the centre (a common cone apex, so
        /// they mesh, <see cref="Coupling"/>). The section view explodes through the stick view.
        /// </summary>
        private static IReadOnlyList<PartSpec>? DifferentialParts(SketchObject o)
        {
            if ((o.View ?? "stick") != "stick") return null;
            var k = Math.Min(o.Size.W, o.Size.H) / 40;
            // Bevel stick design box 12 x 30 with the cone apex at x = 10.6: scaled so the apex lands on the centre.
            var f = 9 * k / 10.6;
            var bevel = new Size2(12 * f, 16 * k);
            return new[]
            {
                new PartSpec("mech.diffcase", "stick", Vec2.Zero) { Size = new Size2(40 * k, 40 * k) },
                new PartSpec("mech.gear.bevel", "stick", new Vec2(-9 * k, 0)) { Size = bevel },                  // left side gear
                new PartSpec("mech.gear.bevel", "stick", new Vec2(9 * k, 0)) { Size = bevel, Rotation = 180 },   // right side gear
                new PartSpec("mech.gear.bevel", "stick", new Vec2(0, -9 * k)) { Size = bevel, Rotation = 90 },   // upper spider gear
                new PartSpec("mech.gear.bevel", "stick", new Vec2(0, 9 * k)) { Size = bevel, Rotation = 270 },   // lower spider gear
            };
        }

        /// <summary>
        /// Differential case (cage) as a part: the cage running loose on the half shafts, the cross pin for the spider
        /// gears and the crown wheel on the cage; side and spider bevel gears dock inside.
        /// </summary>
        private static MechType DiffCase() => new(
            "mech.diffcase", T("Differential case (cage)", "Ausgleichsgehäuse (Korb)"), Cat,
            new[] { "differential case", "cage", "differential carrier", "cross pin", "crown wheel", "ausgleichsgehäuse", "korb", "differentialkorb", "tellerrad", "ausgleichsbolzen" },
            new ViewSpec("stick", VStick, 40, 40, DiffCaseStick)
            {
                Anchors = (p, o) => new[]
                {
                    Anchor.Point(AnchorRole.Hub, p.V(0, 0), "hub"),
                    Anchor.Segment(AnchorRole.Axis, p.V(-20, 0), p.V(20, 0)) with { Id = "axis" },
                    Anchor.Segment(AnchorRole.Axis, p.V(0, -14), p.V(0, 14)) with { Id = "pin" },
                },
            })
        {
            CompanionTypes = new[] { "mech.gear.bevel", "mech.shaft", "mech.bearing.taperroller" },
        };

        /// <summary>The cage, cross pin and crown wheel of <see cref="DiffStick"/> without the gears.</summary>
        private static void DiffCaseStick(Pen p, SketchObject o)
        {
            p.Line(0, -14, 0, 14, S.Stick, "pin");
            p.Line(-13, -14, 13, -14, S.Stick, "cage");
            p.Line(-13, 14, 13, 14, S.Stick, "cage");
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                p.Line(sx * 13, -14, sx * 13, -1.5, S.Stick, "cage");
                p.Line(sx * 13, 1.5, sx * 13, 14, S.Stick, "cage");
                p.Line(sx * 11.2, -1.5, sx * 14.8, -1.5, S.Stick, "cage");
                p.Line(sx * 11.2, 1.5, sx * 14.8, 1.5, S.Stick, "cage");
            }
            foreach (var sy in new[] { -1.0, 1.0 })
            {
                p.Line(-13, sy * 14, -13, sy * 19.5, S.Stick, "crown");
                p.Line(-13, sy * 19.5, -17.5, sy * 15, S.Stick, "crown");
            }
        }

        /// <summary>Classic stick schematic: side gears and spider gears form a closed chamfered square inside the cage.</summary>
        private static void DiffStick(Pen p, SketchObject o)
        {
            // half shafts
            p.Line(-20, 0, -9, 0, S.Heavy, "shaft");
            p.Line(9, 0, 20, 0, S.Heavy, "shaft");
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                // side gears (fixed on half shafts)
                Frag.Hub(p, p.V(sx * 9, -6), p.V(sx * 9, 0), p.V(sx * 9, 6), "fixed", S.Stick);
                foreach (var sy in new[] { -1.0, 1.0 })
                {
                    p.Line(sx * 9, sy * 6, sx * 7.5, sy * 7.5, S.Stick, "side-gear");
                    p.Line(sx * 6, sy * 9, sx * 7.5, sy * 7.5, S.Stick, "spider");
                }
            }
            // spider gears on the cross pin (loose)
            foreach (var sy in new[] { -1.0, 1.0 })
            {
                p.Line(-6, sy * 9, -1.5, sy * 9, S.Stick, "spider");
                p.Line(1.5, sy * 9, 6, sy * 9, S.Stick, "spider");
                p.Line(-1.5, sy * 7.6, -1.5, sy * 10.4, S.Stick, "spider");
                p.Line(1.5, sy * 7.6, 1.5, sy * 10.4, S.Stick, "spider");
            }
            p.Line(0, -14, 0, 14, S.Stick, "pin");
            // cage, running loose on the half shafts
            p.Line(-13, -14, 13, -14, S.Stick, "cage");
            p.Line(-13, 14, 13, 14, S.Stick, "cage");
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                p.Line(sx * 13, -14, sx * 13, -1.5, S.Stick, "cage");
                p.Line(sx * 13, 1.5, sx * 13, 14, S.Stick, "cage");
                p.Line(sx * 11.2, -1.5, sx * 14.8, -1.5, S.Stick, "cage");
                p.Line(sx * 11.2, 1.5, sx * 14.8, 1.5, S.Stick, "cage");
            }
            // crown wheel (bevel) on the cage
            foreach (var sy in new[] { -1.0, 1.0 })
            {
                p.Line(-13, sy * 14, -13, sy * 19.5, S.Stick, "crown");
                p.Line(-13, sy * 19.5, -17.5, sy * 15, S.Stick, "crown");
            }
        }

        /// <summary>Conventional section: hatched cage, bevel gears unhatched (teeth), shafts in view.</summary>
        private static void DiffSection(Pen p, SketchObject o)
        {
            var h = p.Hatch();
            var h2 = p.Hatch(135);
            // half shafts (not sectioned by convention)
            p.Box(-20, -2.5, -7, 2.5, S.Vis, "shaft");
            p.Box(7, -2.5, 20, 2.5, S.Vis, "shaft");
            // cage walls
            p.Poly(h, true, -16, -16, 16, -16, 16, -12, -12, -12, -12, -2.5, -16, -2.5);
            p.Poly(h, true, -16, 16, 16, 16, 16, 12, -12, 12, -12, 2.5, -16, 2.5);
            p.Box(12, -12, 16, -2.5, h);
            p.Box(12, 2.5, 16, 12, h);
            // crown wheel flange on the cage
            p.Poly(h2, true, -16, -16, -16, -19.5, -12, -19.5, -9, -16);
            p.Poly(h2, true, -16, 16, -16, 19.5, -12, 19.5, -9, 16);
            // side gears (bevel, toothed part unhatched)
            foreach (var sx in new[] { -1.0, 1.0 })
            {
                p.Poly(S.Vis, true, sx * 12, -8, sx * 7, -4.5, sx * 7, 4.5, sx * 12, 8);
                p.Line(sx * 12, -2.5, sx * 7, -2.5, S.Vis);
                p.Line(sx * 12, 2.5, sx * 7, 2.5, S.Vis);
            }
            // spider gears and cross pin
            foreach (var sy in new[] { -1.0, 1.0 })
                p.Poly(S.Vis, true, -6.5, sy * 11.6, 6.5, sy * 11.6, 3.5, sy * 6.5, -3.5, sy * 6.5);
            p.Box(-1.2, -12, 1.2, 12, S.Vis, "pin");
            p.Axis(20);
            p.Line(0, -16, 0, 16, S.Ctr, "center");
        }
    }
}
