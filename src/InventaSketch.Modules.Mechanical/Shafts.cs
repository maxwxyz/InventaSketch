using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Shafts and shaft-hub connections (key, spline).</summary>
    internal static class Shafts
    {
        public const string Cat = "mech.shafts";

        private static I18nText T(string en, string de) => new(en, de);

        public static IEnumerable<MechType> Types()
        {
            yield return Shaft();
            yield return Key();
            yield return Spline();
        }

        /// <summary>
        /// Shaft axis with the shaft radius (half the box height) for <see cref="ShaftFit"/>. The schematic line carries the
        /// same diameter as the body although it is drawn as a line, so parts keep fitting when the drawing switches between
        /// schematic and side views. A stepped shaft reports the radius of each section.
        /// </summary>
        private static IEnumerable<Anchor> AxisAnchors(Pen p, SketchObject o, bool stepped)
        {
            var hw = p.W / 2;
            var r = p.H / 2;
            IReadOnlyList<AxisStep>? steps = null;
            if (stepped)
            {
                var (x0, x1, x2, x3) = StepXs(p);
                double T(double x) => p.W > Geo.Eps ? (x + hw) / p.W : 0;
                var thin = r * StepRatio;
                steps = new[] { new AxisStep(0, T(x1), thin), new AxisStep(T(x1), T(x2), r), new AxisStep(T(x2), 1, thin) };
            }
            yield return Anchor.Shaft(new Vec2(-hw, 0), new Vec2(hw, 0), r, steps);
            yield return Anchor.Point(AnchorRole.End, new Vec2(-hw, 0), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(hw, 0), "right");
        }

        /// <summary>Radius of the thin end sections of a stepped shaft relative to the middle section.</summary>
        private const double StepRatio = 0.65;

        /// <summary>Section limits of a stepped shaft (actual x): body start, shoulders, body end.</summary>
        private static (double X0, double X1, double X2, double X3) StepXs(Pen p)
        {
            var hw = p.W / 2;
            var ext = Math.Min(3, p.W * 0.06);
            double x0 = -hw + ext, x3 = hw - ext;
            var len = x3 - x0;
            return (x0, x0 + len * 0.3, x0 + len * 0.7, x3);
        }

        private static MechType Shaft() => new(
            "mech.shaft", T("Shaft", "Welle"), Cat,
            new[] { "shaft", "axle", "spindle", "drive shaft", "propeller shaft", "half shaft", "welle", "achse", "antriebswelle", "getriebewelle", "hohlwelle", "gelenkwelle", "seitenwelle", Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3 },
            new ViewSpec("line", T("Schematic line", "Schema-Linie"), 120, 8, ShaftLine) { Fluid = true, Anchors = (p, o) => AxisAnchors(p, o, false) },
            new ViewSpec("body", T("Body", "Körper"), 120, 8, (p, o) => ShaftBody(p, o, false, Hollow(o, false))) { Fluid = true, Anchors = (p, o) => AxisAnchors(p, o, false) },
            new ViewSpec("hollow", T("Hollow shaft", "Hohlwelle"), 120, 8, (p, o) => ShaftBody(p, o, false, true)) { Fluid = true, Anchors = (p, o) => AxisAnchors(p, o, false) },
            new ViewSpec("stepped", T("Stepped", "Abgesetzt"), 120, 8, (p, o) => ShaftBody(p, o, true, Hollow(o, false))) { Fluid = true, Anchors = (p, o) => AxisAnchors(p, o, true) })
        {
            Params = new[] { Common.Flag("hollow", T("Hollow", "Hohl"), false) },
            Rotating = false,
            // On a shaft one adds gears, bearings, an e-machine, wheels and couplings/clutches (the first six fit the
            // "Insert ▸" menu next to the annotation items; machines.* ids come from the machines module when it is loaded).
            CompanionTypes = new[]
            {
                "mech.gear.spur", "mech.bearing.ball", Drivetrain.EMachine, Drivetrain.Wheel, "mech.clutch.disc", "mech.coupling.flexible",
                "mech.gear.bevel", "mech.bearing.cylroller", "mech.coupling.rigid",
            },
            Extra = o => new[] { HollowToggle(o) },
        };

        private static bool Hollow(SketchObject o, bool def) => o.Flag("hollow", def);

        private static ContextAction HollowToggle(SketchObject o)
        {
            var on = o.View == "hollow" || o.Flag("hollow", false);
            return new ContextAction("param.hollow", T("Hollow on/off", "Hohl an/aus"), "hollow", x =>
            {
                var now = x.View == "hollow" || x.Flag("hollow", false);
                var next = x.WithParam("hollow", !now);
                return now && x.View == "hollow" ? next with { View = "body" } : next;
            }) { IsActive = on, Group = "param" };
        }

        /// <summary>Thick solid line with dash-dot centre line extensions at both ends.</summary>
        private static void ShaftLine(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            var ext = Math.Min(3, p.W * 0.06);
            p.LineP(new Vec2(-hw, 0), new Vec2(hw, 0), S.Ctr, "axis");
            p.LineP(new Vec2(-hw + ext, 0), new Vec2(hw - ext, 0), S.Heavy, "shaft");
        }

        private static void ShaftBody(Pen p, SketchObject o, bool stepped, bool hollow)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var ext = Math.Min(3, p.W * 0.06);
            double x0 = -hw + ext, x3 = hw - ext;
            p.LineP(new Vec2(-hw, 0), new Vec2(hw, 0), S.Ctr, "axis");
            if (!stepped)
            {
                p.Add(Prim.Rect(x0, -hh, x3 - x0, p.H, S.Vis), "body");
                if (hollow)
                {
                    p.LineP(new Vec2(x0, -hh * 0.5), new Vec2(x3, -hh * 0.5), S.Hid, "bore");
                    p.LineP(new Vec2(x0, hh * 0.5), new Vec2(x3, hh * 0.5), S.Hid, "bore");
                }
                return;
            }
            var (_, x1, x2, _) = StepXs(p);
            var h1 = hh * StepRatio;
            p.PolyP(new[]
            {
                new Vec2(x0, -h1), new Vec2(x1, -h1), new Vec2(x1, -hh), new Vec2(x2, -hh), new Vec2(x2, -h1), new Vec2(x3, -h1),
                new Vec2(x3, h1), new Vec2(x2, h1), new Vec2(x2, hh), new Vec2(x1, hh), new Vec2(x1, h1), new Vec2(x0, h1),
            }, S.Vis, true, "body");
            p.LineP(new Vec2(x1, -h1), new Vec2(x1, h1), S.Vis, "shoulder");
            p.LineP(new Vec2(x2, -h1), new Vec2(x2, h1), S.Vis, "shoulder");
            if (hollow)
            {
                p.LineP(new Vec2(x0, -hh * 0.35), new Vec2(x3, -hh * 0.35), S.Hid, "bore");
                p.LineP(new Vec2(x0, hh * 0.35), new Vec2(x3, hh * 0.35), S.Hid, "bore");
            }
        }

        // ------------------------------------------------------------------ key (Passfeder)

        private static MechType Key() => new(
            "mech.key", T("Parallel key", "Passfeder"), Cat,
            new[] { "key", "feather key", "parallel key", "keyway", "passfeder", "feder", "nut", "welle-nabe" },
            new ViewSpec("top", T("Top view", "Draufsicht"), 24, 8, KeyTop),
            new ViewSpec("side", VSideLabel, 24, 6, (p, o) => { p.Box(-12, -3, 12, 3, S.Vis, "body"); p.Line(-12, -2.4, 12, -2.4, S.Thin, "chamfer"); p.Line(-12, 2.4, 12, 2.4, S.Thin, "chamfer"); }),
            new ViewSpec("section", T("Cross-section", "Querschnitt"), 8, 7, KeySection))
        {
            Rotating = false,
        };

        private static readonly I18nText VSideLabel = T("Side view", "Seitenansicht");

        /// <summary>Round-ended key (form A) seen from above.</summary>
        private static void KeyTop(Pen p, SketchObject o)
        {
            var pts = new List<Vec2>();
            pts.AddRange(Pen.ArcD(8, 0, 4, 4, -90, 180, 16));
            pts.AddRange(Pen.ArcD(-8, 0, 4, 4, 90, 180, 16));
            p.PolyD(pts, S.Vis, true, "body");
        }

        private static void KeySection(Pen p, SketchObject o)
        {
            const double c = 0.6;
            p.Poly(p.Hatch(), true, -4 + c, -3.5, 4 - c, -3.5, 4, -3.5 + c, 4, 3.5 - c, 4 - c, 3.5, -4 + c, 3.5, -4, 3.5 - c, -4, -3.5 + c);
        }

        // ------------------------------------------------------------------ spline shaft

        private static MechType Spline() => new(
            "mech.spline", T("Spline shaft", "Keilwelle"), Cat,
            new[] { "spline", "splined shaft", "keilwelle", "vielkeilwelle", "zahnwelle", "profilwelle" },
            new ViewSpec("side", T("Side view", "Seitenansicht"), 40, 10, SplineSide) { Anchors = SplineAxis },
            new ViewSpec("axial", T("Axial view", "Stirnansicht"), 16, 16, SplineAxial))
        {
            Params = new[] { Common.Count("splines", T("Number of splines", "Anzahl Keile"), 6, 3, 24) },
            Rotating = true,
        };

        private static IEnumerable<Anchor> SplineAxis(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            // Outer radius of the splines: hubs with a splined bore fit onto it.
            yield return Anchor.Shaft(new Vec2(-hw, 0), new Vec2(hw, 0), 5 * p.Sy);
        }

        /// <summary>Side view following the simplified spline convention: thin root line over the splined length.</summary>
        private static void SplineSide(Pen p, SketchObject o)
        {
            p.Box(-19, -5, 19, 5, S.Vis, "body");
            p.Line(-19, -3.8, 4, -3.8, S.Thin, "root");
            p.Line(-19, 3.8, 4, 3.8, S.Thin, "root");
            p.Line(4, -5, 4, 5, S.Vis, "spline-end");
            p.Axis(20);
            // small spline mark (square-wave) on the body
            p.Poly(S.Thin, false, 8, 2, 9, 2, 9, -2, 11, -2, 11, 2, 13, 2, 13, -2, 14, -2);
        }

        private static void SplineAxial(Pen p, SketchObject o)
        {
            var n = Math.Max(3, Math.Min(24, o.Int("splines", 6)));
            const double R = 7, r = 5.8;
            var w = 0.45 * 2 * Math.PI * r / n;
            var pts = new List<Vec2>();
            for (var i = 0; i < n; i++)
            {
                var a = -Math.PI / 2 + i * 2 * Math.PI / n;
                var ar = Math.Asin(Math.Min(1, w / 2 / r));
                var aR = Math.Asin(Math.Min(1, w / 2 / R));
                pts.Add(Frag.Polar(r, a - ar));
                pts.Add(Frag.Polar(R, a - aR));
                pts.AddRange(Geo.ArcPoints(Vec2.Zero, R, R, a - aR, 2 * aR, 4).Skip(1));
                pts.Add(Frag.Polar(r, a + ar));
                var next = a + 2 * Math.PI / n;
                pts.AddRange(Geo.ArcPoints(Vec2.Zero, r, r, a + ar, next - ar - (a + ar), 6).Skip(1).Take(5));
            }
            p.PolyD(pts, S.Vis, true, "splines");
            p.CenterCross(0, 0, 8, 8);
        }
    }
}
