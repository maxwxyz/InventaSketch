using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Screw drives: ball screw and lead (trapezoidal) screw as a shaft-like spindle with an axis, and the matching nut
    /// whose hub docks on the spindle axis. The nut is carried by the spindle (moving the spindle takes the nut along) and
    /// slides along it when moved alone, like a gear on a shaft (<see cref="Coupling"/>).
    /// The side view follows the conventional thread representation (major diameter as visible line, minor diameter as thin
    /// line); the profile view shows the thread form (trapezoidal teeth or ball grooves) drawn independently.
    /// </summary>
    internal static class ScrewDrives
    {
        public const string Cat = "mech.screwdrives";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSide = T("Side view", "Seitenansicht");

        public static readonly ParamDef Kind = new ParamDef("kind", T("Screw type", "Gewindetrieb-Art"), ParamKind.Choice)
        {
            Default = "ball",
            Options = new[]
            {
                new ParamOption("ball", T("Ball screw", "Kugelgewindetrieb")),
                new ParamOption("lead", T("Lead screw (trapezoidal)", "Trapezgewindetrieb")),
            },
        };

        private static bool Ball(SketchObject o) => o.Str("kind", "ball") != "lead";

        public static IEnumerable<MechType> Types()
        {
            yield return Screw();
            yield return Nut();
        }

        // ------------------------------------------------------------------ screw (spindle)

        private static MechType Screw() => new(
            "mech.screwdrive.screw", T("Ball screw / lead screw", "Gewindespindel (Kugel- / Trapezgewinde)"), Cat,
            new[]
            {
                "ball screw", "lead screw", "leadscrew", "spindle", "screw drive", "trapezoidal", "acme", "linear actuator",
                "kugelgewindetrieb", "kugelgewindespindel", "gewindespindel", "trapezgewinde", "gewindetrieb", "spindeltrieb", "spindel",
            },
            new ViewSpec("stick", VStick, 120, 10, ScrewStick) { Fluid = true, Anchors = AxisAnchors },
            new ViewSpec("side", VSide, 120, 10, ScrewSide) { Fluid = true, Anchors = AxisAnchors },
            new ViewSpec("detailed", T("Thread profile", "Gewindeprofil"), 120, 10, ScrewProfile) { Fluid = true, Anchors = AxisAnchors })
        {
            Params = new[]
            {
                Kind,
                new ParamDef("pitch", T("Lead (mm per turn)", "Steigung (mm je Umdrehung)"), ParamKind.Number) { Unit = "mm", Min = 0.5, Max = 100, Step = 0.5, Advanced = true },
                Common.Flag("lefthand", T("Left-hand thread", "Linksgewinde"), false),
            },
            Rotating = false,
            // A nut goes on the spindle, bearings at its journals, a coupling or motor at its end.
            CompanionTypes = new[] { "mech.screwdrive.nut", "mech.bearing.angular", "mech.bearing.ball", "mech.coupling.flexible", Drivetrain.EMachine, "mech.guide.linear" },
            Extra = o => new[] { Common.Toggle(o, "lefthand", false, T("Left-hand thread on/off", "Linksgewinde an/aus"), IconIds.Toggle) },
        };

        /// <summary>Axis over the full length (nuts, bearings and couplings dock on it) and the two ends.</summary>
        private static IEnumerable<Anchor> AxisAnchors(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            yield return Anchor.Segment(AnchorRole.Axis, new Vec2(-hw, 0), new Vec2(hw, 0)) with { Id = "axis" };
            yield return Anchor.Point(AnchorRole.End, new Vec2(-hw, 0), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(hw, 0), "right");
        }

        /// <summary>Spindle layout in actual mm: centre-line extension, journals (bearing seats) and the threaded length.</summary>
        private readonly struct Layout
        {
            public Layout(Pen p, SketchObject o)
            {
                HW = p.W / 2;
                HH = p.H / 2;
                var ext = Math.Min(3, p.W * 0.06);
                X0 = -HW + ext;
                X3 = HW - ext;
                var jl = Math.Min((X3 - X0) * 0.12, Math.Max(2, p.H * 1.5));
                X1 = X0 + jl;
                X2 = X3 - jl;
                Journal = HH * 0.6;
                Root = HH * (Ball(o) ? 0.8 : 0.76);
                var m = o.Num("pitch", 0);
                Pitch = m > 0 ? m : Math.Max(1.0, p.H * 0.55);
                Hand = o.Flag("lefthand", false) ? -1 : 1;
            }

            public double HW { get; }
            public double HH { get; }
            public double X0 { get; }
            public double X1 { get; }
            public double X2 { get; }
            public double X3 { get; }
            public double Journal { get; }
            public double Root { get; }
            public double Pitch { get; }
            public int Hand { get; }

            /// <summary>Number of thread periods over the threaded length (at least one) and their actual length.</summary>
            public (int N, double P) Periods()
            {
                var len = X2 - X1;
                var n = Math.Max(1, (int)Math.Round(len / Pitch));
                return (n, len / n);
            }
        }

        /// <summary>Schematic: shaft line with slanted thread strokes over the threaded length.</summary>
        private static void ScrewStick(Pen p, SketchObject o)
        {
            var l = new Layout(p, o);
            p.LineP(new Vec2(-l.HW, 0), new Vec2(l.HW, 0), S.Ctr, "axis");
            p.LineP(new Vec2(l.X0, 0), new Vec2(l.X3, 0), S.Heavy, "shaft");
            var (n, pitch) = l.Periods();
            var dy = l.HH * 0.7;
            var dx = Math.Min(pitch * 0.25, dy);
            for (var i = 0; i < n; i++)
            {
                var x = l.X1 + (i + 0.5) * pitch;
                p.LineP(new Vec2(x - l.Hand * dx, dy), new Vec2(x + l.Hand * dx, -dy), S.Stick, "thread");
            }
        }

        /// <summary>Conventional thread: major diameter visible, minor diameter thin, journals at both ends.</summary>
        private static void ScrewSide(Pen p, SketchObject o)
        {
            var l = new Layout(p, o);
            p.LineP(new Vec2(-l.HW, 0), new Vec2(l.HW, 0), S.Ctr, "axis");
            p.Add(Prim.Rect(l.X0, -l.Journal, l.X1 - l.X0, 2 * l.Journal, S.Vis), "journal");
            p.Add(Prim.Rect(l.X2, -l.Journal, l.X3 - l.X2, 2 * l.Journal, S.Vis), "journal");
            p.Add(Prim.Rect(l.X1, -l.HH, l.X2 - l.X1, 2 * l.HH, S.Vis), "body");
            p.LineP(new Vec2(l.X1, -l.Root), new Vec2(l.X2, -l.Root), S.Thin, "root");
            p.LineP(new Vec2(l.X1, l.Root), new Vec2(l.X2, l.Root), S.Thin, "root");
        }

        /// <summary>Thread profile: trapezoidal teeth (lead screw) or circular ball grooves (ball screw) on both edges.</summary>
        private static void ScrewProfile(Pen p, SketchObject o)
        {
            var l = new Layout(p, o);
            var (n, pitch) = l.Periods();
            var ball = Ball(o);
            List<Vec2> Edge(double s)
            {
                // From X1 to X2 along the edge on side s (-1 top, +1 bottom).
                var pts = new List<Vec2> { new(l.X1, s * l.Root) };
                for (var i = 0; i < n; i++)
                {
                    var x = l.X1 + i * pitch;
                    if (ball)
                    {
                        var rg = Math.Min(pitch * 0.36, (l.HH - l.Journal) * 0.95);
                        var xc = x + pitch / 2;
                        pts.Add(new Vec2(x, s * l.HH));
                        for (var k = 0; k <= 12; k++)
                        {
                            var t = Math.PI * k / 12;
                            pts.Add(new Vec2(xc - rg * Math.Cos(t), s * (l.HH - rg * Math.Sin(t))));
                        }
                        pts.Add(new Vec2(x + pitch, s * l.HH));
                    }
                    else
                    {
                        pts.Add(new Vec2(x + 0.2 * pitch, s * l.Root));
                        pts.Add(new Vec2(x + 0.35 * pitch, s * l.HH));
                        pts.Add(new Vec2(x + 0.65 * pitch, s * l.HH));
                        pts.Add(new Vec2(x + 0.8 * pitch, s * l.Root));
                        pts.Add(new Vec2(x + pitch, s * l.Root));
                    }
                }
                pts.Add(new Vec2(l.X2, s * l.Root));
                return pts;
            }
            var outline = new List<Vec2> { new(l.X0, -l.Journal), new(l.X1, -l.Journal) };
            outline.AddRange(Edge(-1));
            outline.Add(new Vec2(l.X2, -l.Journal));
            outline.Add(new Vec2(l.X3, -l.Journal));
            outline.Add(new Vec2(l.X3, l.Journal));
            outline.Add(new Vec2(l.X2, l.Journal));
            var bottom = Edge(1);
            bottom.Reverse();
            outline.AddRange(bottom);
            outline.Add(new Vec2(l.X1, l.Journal));
            outline.Add(new Vec2(l.X0, l.Journal));
            p.PolyP(outline, S.Vis, true, "profile");
            p.LineP(new Vec2(-l.HW, 0), new Vec2(l.HW, 0), S.Ctr, "axis");
        }

        // ------------------------------------------------------------------ nut

        private static MechType Nut() => new(
            "mech.screwdrive.nut", T("Screw-drive nut (ball / lead)", "Gewindetrieb-Mutter (Kugel- / Trapezgewinde)"), Cat,
            new[]
            {
                "ball nut", "lead screw nut", "spindle nut", "flange nut", "screw drive", "ball screw", "lead screw",
                "kugelgewindemutter", "spindelmutter", "trapezgewindemutter", "flanschmutter", "gewindetrieb", "kugelgewindetrieb",
            },
            new ViewSpec("stick", VStick, 14, 16, NutStick) { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(0, -6.5), "mount") } },
            new ViewSpec("side", VSide, 24, 30, NutSide) { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(2, Ball(o) ? -13.5 : -10), "mount") } },
            new ViewSpec("section", T("Section", "Schnitt"), 24, 30, NutSection) { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(2, -10), "mount") } })
        {
            Params = new[] { Kind, Common.Flag("flange", T("Flange", "Flansch"), true) },
            Extra = o => new[] { Common.Toggle(o, "flange", true, T("Flange on/off", "Flansch an/aus"), IconIds.Toggle) },
            CompanionTypes = new[] { "mech.screwdrive.screw", "mech.guide.linear" },
        };

        /// <summary>Schematic nut: box around the spindle axis with thread strokes (lead) or balls, stem to the driven part.</summary>
        private static void NutStick(Pen p, SketchObject o)
        {
            p.Box(-6, -4, 6, 4, S.Stick, "nut");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (Ball(o))
                    foreach (var x in new[] { -3.0, 0.0, 3.0 }) p.Round(p.V(x, s * 2.1), 0.9, S.Vis, "ball");
                else
                    foreach (var x in new[] { -2.5, 0.0, 2.5 }) p.Line(x - 0.8, s * 3.2, x + 0.8, s * 1.0, S.Thin, "thread");
            }
            p.Line(0, -4, 0, -6.5, S.Stick, "mount");
            p.Line(-4, -6.5, 4, -6.5, S.Stick, "mount");
        }

        private static void NutSide(Pen p, SketchObject o)
        {
            var flange = o.Flag("flange", true);
            var xl = flange ? -8.0 : -12.0;
            if (flange)
            {
                p.Box(-12, -15, -8, 15, S.Vis, "flange");
                p.Line(-12, -12.5, -8, -12.5, S.Ctr, "bolt");
                p.Line(-12, 12.5, -8, 12.5, S.Ctr, "bolt");
            }
            p.Box(xl, -10, 12, 10, S.Vis, "body");
            p.Line(-12, -5, 12, -5, S.Hid, "bore");
            p.Line(-12, 5, 12, 5, S.Hid, "bore");
            if (Ball(o))
            {
                // Ball return tube on top of the nut.
                p.Poly(S.Vis, false, -5, -10, -5, -13.5, 9, -13.5, 9, -10);
                p.Poly(S.Thin, false, -3, -10, -3, -12, 7, -12, 7, -10);
            }
            p.Axis(12);
        }

        /// <summary>Section: hatched nut halves; balls in circular grooves, or trapezoidal internal thread teeth.</summary>
        private static void NutSection(Pen p, SketchObject o)
        {
            var flange = o.Flag("flange", true);
            var ball = Ball(o);
            var h = p.Hatch();
            const double rs = 5.0, rb = 1.2, rCrest = 5.4, rRoot = 5.6, rTip = 4.4;
            var balls = new[] { 8.0, 4.0, 0.0, -4.0, -8.0 };
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var pts = new List<Vec2>();
                if (flange)
                {
                    pts.Add(new Vec2(-12, s * 15));
                    pts.Add(new Vec2(-8, s * 15));
                    pts.Add(new Vec2(-8, s * 10));
                }
                else pts.Add(new Vec2(-12, s * 10));
                pts.Add(new Vec2(12, s * 10));
                if (ball)
                {
                    pts.Add(new Vec2(12, s * rCrest));
                    var dy = rCrest - rs;
                    var a0 = Math.Asin(dy / rb);
                    foreach (var xc in balls)
                        for (var k = 0; k <= 12; k++)
                        {
                            var t = a0 + (Math.PI - 2 * a0) * k / 12;
                            pts.Add(new Vec2(xc + rb * Math.Cos(t), s * (rs + rb * Math.Sin(t))));
                        }
                    pts.Add(new Vec2(-12, s * rCrest));
                }
                else
                {
                    pts.Add(new Vec2(12, s * rRoot));
                    for (var i = 0; i < 6; i++)
                    {
                        var xs = 12 - i * 4.0;
                        pts.Add(new Vec2(xs - 0.8, s * rRoot));
                        pts.Add(new Vec2(xs - 1.4, s * rTip));
                        pts.Add(new Vec2(xs - 2.6, s * rTip));
                        pts.Add(new Vec2(xs - 3.2, s * rRoot));
                    }
                    pts.Add(new Vec2(-12, s * rRoot));
                }
                p.PolyD(pts, h, true, "body");
                if (ball)
                    foreach (var xc in balls) p.Round(p.V(xc, s * rs), rb, S.Vis, "ball");
            }
            p.Axis(12);
        }
    }
}
