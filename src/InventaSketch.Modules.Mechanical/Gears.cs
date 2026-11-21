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

        public static IEnumerable<MechType> Types()
        {
            yield return Spur();
            yield return Internal();
            yield return Bevel();
            yield return Worm();
            yield return WormWheel();
            yield return Rack();
            yield return Planetary();
            yield return Differential();
        }

        // ------------------------------------------------------------------ spur gear

        private const double SpurTip = 14; // axial views: tip radius in a 30 x 30 design box

        private static MechType Spur() => new(
            "mech.gear.spur", T("Spur gear", "Stirnrad"), Cat,
            new[] { "gear", "spur", "cog", "toothed wheel", "pinion", "zahnrad", "stirnrad", "ritzel", "verzahnung", "getriebe", "gearbox", "transmission", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
            new ViewSpec("stick", VStick, 8, 30, (p, o) => GearStick(p, o, 4, 15)),
            new ViewSpec("side", VSide, 10, 30, SpurSide),
            new ViewSpec("section", VSection, 10, 30, SpurSection),
            new ViewSpec("axial", VAxial, 30, 30, SpurAxial) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 18)).Pitch) } },
            new ViewSpec("teeth", VTeeth, 30, 30, SpurTeeth) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 18)).Pitch) } })
        {
            Params = new[] { Common.Hub, Common.Teeth(18), Common.Module, Common.Helix, Common.Bore, Common.Keyway },
            Extra = o => GearActions(o, true, 18, true),
        };

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
            new ViewSpec("stick", VStick, 10, 40, RingStick),
            new ViewSpec("axial", VAxial, 40, 40, RingAxial) { Anchors = (p, o) => new[] { PitchIn(p, RingRoot * GearMath.Internal(GearMath.Teeth(o, 48)).Pitch) } },
            new ViewSpec("teeth", VTeeth, 40, 40, RingTeeth) { Anchors = (p, o) => new[] { PitchIn(p, RingRoot * GearMath.Internal(GearMath.Teeth(o, 48)).Pitch) } },
            new ViewSpec("section", VSection, 10, 40, RingSection))
        {
            Params = new[] { Common.Teeth(48), Common.Module },
            Extra = o => GearActions(o, false, 48, false),
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
            new ViewSpec("stick", VStick, 12, 30, BevelStick),
            new ViewSpec("side", VSide, 24, 30, (p, o) => BevelBody(p, o, false)),
            new ViewSpec("section", VSection, 24, 30, (p, o) => BevelBody(p, o, true)))
        {
            Params = new[] { Common.Hub, Common.Teeth(24), Common.Module, Common.Bore },
            Extra = o => GearActions(o, true, 24, false),
        };

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
            p.Circle(0, 0, 1.8, S.Vis, "bore");
            p.CenterCross(0, 0, 6, 6);
        }

        private static MechType WormWheel() => new(
            "mech.gear.wormwheel", T("Worm wheel", "Schneckenrad"), Cat,
            new[] { "worm wheel", "worm gear", "schneckenrad", "schneckengetriebe" },
            new ViewSpec("stick", VStick, 8, 30, (p, o) => GearStick(p, o, 4, 15)),
            new ViewSpec("side", VSide, 10, 30, WormWheelSide),
            new ViewSpec("axial", VAxial, 30, 30, SpurAxial) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 30)).Pitch) } },
            new ViewSpec("teeth", VTeeth, 30, 30, SpurTeeth) { Anchors = (p, o) => new[] { Pitch(p, SpurTip * GearMath.External(GearMath.Teeth(o, 30)).Pitch) } })
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
            new ViewSpec("stick", VStick, 30, 50, PlanetStick),
            new ViewSpec("axial", VAxial, 50, 50, (p, o) => PlanetAxial(p, o, false)) { Anchors = PlanetAnchors },
            new ViewSpec("teeth", VTeeth, 50, 50, (p, o) => PlanetAxial(p, o, true)) { Anchors = PlanetAnchors })
        {
            Params = new[] { Common.Count("planets", T("Number of planets", "Anzahl Planeten"), 3, 3, 6) },
        };

        private static IEnumerable<Anchor> PlanetAnchors(Pen p, SketchObject o)
        {
            yield return PitchIn(p, PRing);
            yield return Pitch(p, PSun);
        }

        private static int Planets(SketchObject o) => Math.Max(3, Math.Min(6, o.Int("planets", 3)));

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
                    p.Circle(c.X, c.Y, 1.6, S.Vis, "pin");
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
                    p.Circle(c.X, c.Y, 1.6, S.Vis, "pin");
                }
            }
            p.Circle(0, 0, PRing, S.Ctr, "pitch");
            p.Circle(0, 0, PCarrier, S.Ctr, "carrier");
            p.Circle(0, 0, 2.6, S.Vis, "bore");
            p.CenterCross(0, 0, 25, 25);
        }

        // ------------------------------------------------------------------ differential

        private static MechType Differential() => new(
            "mech.gearset.differential", T("Bevel differential", "Kegelraddifferential"), Cat,
            new[] { "differential", "diff", "axle", "spider gear", "side gear", "crown wheel", "differential", "ausgleichsgetriebe", "achsgetriebe", "tellerrad", "ausgleichsrad", "final drive", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
            new ViewSpec("stick", VStick, 40, 40, DiffStick),
            new ViewSpec("section", VSection, 40, 40, DiffSection))
        {
            // The differential sits on the axle shaft; the wheels sit on its half shafts.
            CompanionTypes = new[] { "mech.shaft", Drivetrain.Wheel, "mech.gear.bevel", "mech.gear.spur", "mech.bearing.taperroller" },
        };

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
