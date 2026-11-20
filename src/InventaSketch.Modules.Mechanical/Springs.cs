using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Springs (in the style of the ISO 2162 simplified/view/section representations) and cams.</summary>
    internal static class Springs
    {
        public const string Cat = "mech.springs";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef Coils = Common.Count("coils", T("Number of coils", "Windungszahl"), 6, 2, 40);

        private static int CoilCount(SketchObject o) => Math.Max(2, Math.Min(40, o.Int("coils", 6)));

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType("mech.spring.compression", T("Compression spring", "Druckfeder"), Cat,
                new[] { "spring", "compression spring", "coil spring", "helical spring", "feder", "druckfeder", "schraubenfeder" },
                new ViewSpec("simplified", T("Simplified", "Vereinfacht"), 40, 12, CompressionSimplified) { Fluid = true, Anchors = EndAnchors },
                new ViewSpec("side", T("View", "Ansicht"), 40, 12, (p, o) => CoilView(p, o, 0)) { Fluid = true, Anchors = EndAnchors },
                new ViewSpec("section", T("Section", "Schnitt"), 40, 12, CoilSection) { Fluid = true, Anchors = EndAnchors })
            {
                Params = new[] { Coils },
                Rotating = false,
            };
            yield return new MechType("mech.spring.tension", T("Tension spring", "Zugfeder"), Cat,
                new[] { "spring", "tension spring", "extension spring", "feder", "zugfeder", "schraubenfeder" },
                new ViewSpec("simplified", T("Simplified", "Vereinfacht"), 50, 10, TensionSimplified) { Fluid = true, Anchors = EndAnchors },
                new ViewSpec("side", T("View", "Ansicht"), 50, 10, (p, o) => CoilView(p, o, Math.Min(p.H * 0.5, p.W * 0.15))) { Fluid = true, Anchors = EndAnchors })
            {
                Params = new[] { Coils },
                Rotating = false,
            };
            yield return new MechType("mech.spring.torsion", T("Torsion spring", "Drehfeder"), Cat,
                new[] { "spring", "torsion spring", "leg spring", "feder", "drehfeder", "schenkelfeder", "torsionsfeder" },
                new ViewSpec("side", T("Side view", "Seitenansicht"), 30, 24, TorsionSide),
                new ViewSpec("axial", T("Axial view", "Stirnansicht"), 30, 30, TorsionAxial))
            {
                Params = new[] { Coils },
            };
            yield return new MechType("mech.cam", T("Cam", "Nocken"), Cat,
                new[] { "cam", "cam disc", "follower", "eccentric", "nocken", "kurvenscheibe", "nockenscheibe", "stößel", "kurvengetriebe" },
                new ViewSpec("disc", T("Cam disc", "Kurvenscheibe"), 26, 36, (p, o) => CamDraw(p, 3, false)) { Anchors = (p, o) => CamAnchors(p, 3) },
                new ViewSpec("follower", T("With follower", "Mit Stößel"), 26, 52, (p, o) => CamDraw(p, 12, true)) { Anchors = (p, o) => CamAnchors(p, 12) })
            {
                Rotating = false,
            };
        }

        private static IEnumerable<Anchor> EndAnchors(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.End, new Vec2(-p.W / 2, 0), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(p.W / 2, 0), "right");
            yield return Anchor.Segment(AnchorRole.Axis, new Vec2(-p.W / 2, 0), new Vec2(p.W / 2, 0));
        }

        /// <summary>Simplified compression spring: zigzag with closed, ground ends.</summary>
        private static void CompressionSimplified(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var n = CoilCount(o);
            p.LineP(new Vec2(-hw, -hh), new Vec2(-hw, hh), S.Vis, "end");
            p.LineP(new Vec2(hw, -hh), new Vec2(hw, hh), S.Vis, "end");
            var pts = new List<Vec2> { new(-hw, hh) };
            var step = p.W / n;
            for (var i = 0; i < n; i++)
            {
                pts.Add(new Vec2(-hw + step * (i + 0.5), -hh));
                pts.Add(new Vec2(-hw + step * (i + 1), hh));
            }
            p.PolyP(pts, S.Vis, false, "coils");
        }

        /// <summary>View of a helical spring: front strokes thick, back strokes thin; optional hooks of radius <paramref name="hook"/>.</summary>
        private static void CoilView(Pen p, SketchObject o, double hook)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var n = CoilCount(o);
            double x0 = -hw + 2 * hook, x1 = hw - 2 * hook;
            var step = (x1 - x0) / n;
            for (var i = 0; i < n; i++)
            {
                var x = x0 + i * step;
                p.LineP(new Vec2(x, hh), new Vec2(x + step * 0.5, -hh), S.Vis, "front");
                p.LineP(new Vec2(x + step * 0.5, -hh), new Vec2(x + step, hh), S.Thin, "back");
            }
            p.LineP(new Vec2(x0, -hh), new Vec2(x1, -hh), S.Thin, "outline");
            p.LineP(new Vec2(x0, hh), new Vec2(x1, hh), S.Thin, "outline");
            if (hook > 0) Hooks(p, x0, x1, hook);
            else p.LineP(new Vec2(-hw, 0), new Vec2(hw, 0), S.Ctr, "axis");
        }

        private static void Hooks(Pen p, double x0, double x1, double r)
        {
            // loops centred on the axis just outside the coil body
            p.PolyP(Geo.ArcPoints(new Vec2(x0 - r, 0), r, r, 0, -1.75 * Math.PI, 32), S.Vis, false, "hook");
            p.PolyP(Geo.ArcPoints(new Vec2(x1 + r, 0), r, r, Math.PI, -1.75 * Math.PI, 32), S.Vis, false, "hook");
        }

        /// <summary>Section: wire cross-sections (black) at top and bottom, offset by half a pitch.</summary>
        private static void CoilSection(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var n = CoilCount(o);
            var r = Math.Min(p.H * 0.12, p.W / (2.2 * (n + 1)));
            var step = (p.W - 2 * r) / n;
            for (var i = 0; i <= n; i++)
            {
                var x = -hw + r + i * step;
                p.Add(Prim.Circle(x, -hh + r, r, S.Black), "wire");
                if (i < n) p.Add(Prim.Circle(x + step * 0.5, hh - r, r, S.Black), "wire");
            }
            p.LineP(new Vec2(-hw, 0), new Vec2(hw, 0), S.Ctr, "axis");
        }

        private static void TensionSimplified(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var r = Math.Min(hh, p.W * 0.08);
            double x0 = -hw + 2 * r, x1 = hw - 2 * r;
            var pts = new List<Vec2> { new(x0, 0) };
            pts.AddRange(Frag.Zigzag(x0, x1, 0, hh * 0.9, CoilCount(o)).Skip(1));
            p.PolyP(pts, S.Vis, false, "coils");
            Hooks(p, x0, x1, r);
        }

        /// <summary>Torsion (leg) spring: coil body in view with two legs.</summary>
        private static void TorsionSide(Pen p, SketchObject o)
        {
            var n = CoilCount(o);
            const double x0 = -6, x1 = 6, top = -4, bottom = 4;
            var step = (x1 - x0) / n;
            for (var i = 0; i < n; i++)
            {
                var x = x0 + i * step;
                p.Line(x, bottom, x + step * 0.5, top, S.Vis, "front");
                p.Line(x + step * 0.5, top, x + step, bottom, S.Thin, "back");
            }
            p.Line(x0, top, x1, top, S.Thin, "outline");
            p.Line(x0, bottom, x1, bottom, S.Thin, "outline");
            p.Line(x0, bottom, -15, -12, S.Vis, "leg");
            p.Line(x1, top, 15, 12, S.Vis, "leg");
            p.Line(-8, 0, 8, 0, S.Ctr, "axis");
        }

        private static void TorsionAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 7, S.Vis, "coil");
            p.Circle(0, 0, 5.6, S.Vis, "coil");
            p.Line(-6.3, 0, -6.3, -15, S.Vis, "leg");
            p.Line(6.3, 0, 15, 12, S.Vis, "leg");
            p.CenterCross(0, 0, 9, 9);
        }

        // ------------------------------------------------------------------ cam

        /// <summary>Cam profile: base circle 9 with a smooth nose (lift 6) pointing up.</summary>
        private static List<Vec2> CamProfile(double cy)
        {
            var pts = new List<Vec2>();
            for (var i = 0; i < 96; i++)
            {
                var phi = i * 2 * Math.PI / 96;
                var b = (1 + Math.Cos(phi)) / 2;
                var r = 9 + 6 * b * b;
                pts.Add(new Vec2(r * Math.Sin(phi), cy - r * Math.Cos(phi)));
            }
            return pts;
        }

        private static IEnumerable<Anchor> CamAnchors(Pen p, double cy)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(0, cy), "hub");
        }

        private static void CamDraw(Pen p, double cy, bool follower)
        {
            p.PolyD(CamProfile(cy), S.Vis, true, "profile");
            p.Circle(0, cy, 2.5, S.Vis, "bore");
            p.Line(-12, cy, 12, cy, S.Ctr, "center");
            p.Line(0, cy - (follower ? 13 : 16.5), 0, cy + 11.5, S.Ctr, "center");
            if (!follower) return;
            // roller follower on the nose, stem in a guide
            var rollerY = cy - 15 - 3;
            p.Circle(0, rollerY, 3, S.Vis, "roller");
            p.Round(p.V(0, rollerY), 0.6, S.Black, "pin");
            p.Box(-1.2, rollerY - 3, 1.2, -25, S.Vis, "stem");
            p.Box(-5, -21, -1.6, -17, p.Hatch(), "guide");
            p.Box(1.6, -21, 5, -17, p.Hatch(), "guide");
        }
    }
}
