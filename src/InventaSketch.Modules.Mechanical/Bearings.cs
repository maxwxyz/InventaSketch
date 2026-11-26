using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Bearings (simplified representation in the style of ISO 8826: outline with free-standing upright cross,
    /// plus a detailed view with hatched rings and rolling elements), kinematic support symbols, seals,
    /// housing/ground and linear guides. All bearing views are symmetric about the shaft axis (box centre).
    /// </summary>
    internal static class Bearings
    {
        public const string Cat = "mech.bearings";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSimple = T("Simplified", "Vereinfacht");
        private static readonly I18nText VDetail = T("Detailed", "Detailliert");

        private enum Kind { Ball, Angular, TaperRoller, CylRoller, Needle, Thrust, Plain }

        public static IEnumerable<MechType> Types()
        {
            yield return Bearing("mech.bearing.ball", T("Deep groove ball bearing", "Rillenkugellager"), Kind.Ball,
                "ball bearing", "deep groove", "rolling bearing", "kugellager", "rillenkugellager", "wälzlager", "lager");
            yield return Bearing("mech.bearing.angular", T("Angular contact ball bearing", "Schrägkugellager"), Kind.Angular,
                "angular contact", "ball bearing", "schrägkugellager", "wälzlager", "lager");
            yield return Bearing("mech.bearing.taperroller", T("Tapered roller bearing", "Kegelrollenlager"), Kind.TaperRoller,
                "taper roller", "tapered roller", "kegelrollenlager", "wälzlager", "lager");
            yield return Bearing("mech.bearing.cylroller", T("Cylindrical roller bearing", "Zylinderrollenlager"), Kind.CylRoller,
                "cylindrical roller", "roller bearing", "zylinderrollenlager", "rollenlager", "wälzlager", "lager");
            yield return Bearing("mech.bearing.needle", T("Needle roller bearing", "Nadellager"), Kind.Needle,
                "needle bearing", "needle roller", "nadellager", "wälzlager", "lager");
            yield return Bearing("mech.bearing.thrust", T("Thrust ball bearing", "Axial-Rillenkugellager"), Kind.Thrust,
                "thrust bearing", "axial bearing", "axiallager", "axialkugellager", "wälzlager", "lager");
            yield return Bearing("mech.bearing.plain", T("Plain bearing (bushing)", "Gleitlager (Buchse)"), Kind.Plain,
                "plain bearing", "bushing", "sleeve bearing", "journal", "gleitlager", "lagerbuchse", "buchse", "lager");
            yield return Support("mech.support.fixed", T("Fixed bearing (locating)", "Festlager"), false,
                "fixed bearing", "locating bearing", "support", "festlager", "lagerung", "auflager");
            yield return Support("mech.support.floating", T("Floating bearing (non-locating)", "Loslager"), true,
                "floating bearing", "non-locating bearing", "support", "loslager", "lagerung", "auflager");
            yield return Seal();
            yield return ORing();
            yield return Ground();
            yield return Guide();
        }

        // ------------------------------------------------------------------ rolling and plain bearings

        // Design of the simplified/detailed views: 8 x 30, rings from the bore (|y| = 6) to |y| = 15.
        private const double Ro = 15, Rb = 6, Yc = 10.5;

        private static MechType Bearing(string id, I18nText name, Kind kind, params string[] keywords) => new(
            id, name, Cat, keywords,
            new ViewSpec("stick", VStick, 6, 16, (p, o) => BearingStick(p, kind)) { Bore = RingBore },
            new ViewSpec("simplified", VSimple, 8, 30, (p, o) => BearingSimplified(p, kind)) { Bore = RingBore },
            new ViewSpec("detailed", VDetail, 8, 30, (p, o) => BearingDetailed(p, kind)) { Bore = RingBore });

        /// <summary>
        /// Bore across the axis: the inner edge of the rings in the simplified and detailed views (and the seal). The
        /// schematic shows no bore; it takes the same design value, the bore the bearing has after a view switch
        /// (<see cref="Registry.WithView"/> keeps the visual scale between the 6 x 16 schematic and the 8 x 30 views), so the
        /// fit carries over and the symbol sits close around the shaft line.
        /// </summary>
        private static double RingBore(Pen p, SketchObject o) => Rb * p.Sy;

        /// <summary>Schematic: race lines with the rolling element between, above and below the shaft.</summary>
        private static void BearingStick(Pen p, Kind kind)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var c = p.V(0, s * 4);
                var r = 2.4 * p.K;
                double xl = -3 * p.Sx, xr = 3 * p.Sx;
                if (kind == Kind.Thrust)
                {
                    p.LineP(new Vec2(c.X - r, c.Y - r * 1.2), new Vec2(c.X - r, c.Y + r * 1.2), S.Stick, "race");
                    p.LineP(new Vec2(c.X + r, c.Y - r * 1.2), new Vec2(c.X + r, c.Y + r * 1.2), S.Stick, "race");
                    p.Round(c, 2.3, S.Vis, "rolling");
                    continue;
                }
                p.LineP(new Vec2(xl, c.Y - r), new Vec2(xr, c.Y - r), S.Stick, "race");
                p.LineP(new Vec2(xl, c.Y + r), new Vec2(xr, c.Y + r), S.Stick, "race");
                switch (kind)
                {
                    case Kind.Ball:
                        p.Round(c, 2.3, S.Vis, "rolling");
                        break;
                    case Kind.Angular:
                        p.Round(c, 2.3, S.Vis, "rolling");
                        p.Seg(c, -1.3, -2.25 * -s, 1.3, 2.25 * -s, S.Thin);
                        break;
                    case Kind.CylRoller:
                        p.Add(Prim.Rect(c.X - 1.8 * p.K, c.Y - r, 3.6 * p.K, 2 * r, S.Vis), "rolling");
                        break;
                    case Kind.Needle:
                        p.Add(Prim.Rect(xl + 0.4 * p.Sx, c.Y - r * 0.45, xr - xl - 0.8 * p.Sx, r * 0.9, S.Vis), "rolling");
                        break;
                    case Kind.TaperRoller:
                        {
                            var a = Geo.DegToRad(18);
                            var u = new Vec2(Math.Cos(a), s * Math.Sin(a));
                            var n = u.Perp;
                            double l = 2.2 * p.K, w1 = 1.5 * p.K, w2 = 1.0 * p.K;
                            p.PolyP(new[] { c - u * l + n * w1, c + u * l + n * w2, c + u * l - n * w2, c - u * l - n * w1 }, S.Vis, true, "rolling");
                            break;
                        }
                    case Kind.Plain:
                        p.Add(Prim.Rect(xl, c.Y - r, xr - xl, 2 * r, p.Hatch()), "bushing");
                        break;
                }
            }
        }

        /// <summary>General simplified representation: outline with a free-standing upright cross in each half.</summary>
        private static void BearingSimplified(Pen p, Kind kind)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (kind == Kind.Plain)
                {
                    p.Box(-4, s * Ro, 4, s * Rb, p.Hatch(), "bushing");
                    continue;
                }
                p.Box(-4, s * Ro, 4, s * Rb, S.Vis, "outline");
                var c = p.V(0, s * Yc);
                p.Seg(c, -2.4, 0, 2.4, 0, S.Vis);
                p.Seg(c, 0, -2.4, 0, 2.4, S.Vis);
            }
        }

        /// <summary>Detailed view: hatched rings (opposite hatch directions), rolling element unhatched.</summary>
        private static void BearingDetailed(Pen p, Kind kind)
        {
            var hOut = p.Hatch(45);
            var hIn = p.Hatch(135);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                double Y(double y) => s * y;
                var c = new Vec2(0, Y(Yc));
                switch (kind)
                {
                    case Kind.Ball:
                    case Kind.Angular:
                        {
                            // Balls stay round when the bearing is stretched: rings and bites in actual coordinates, the ball
                            // radius at the detail scale (the rings stretch).
                            var r = 2.6 * p.K;
                            var ca = p.V(c);
                            double X(double x) => x * p.Sx;
                            double YA(double y) => s * y * p.Sy;
                            p.PolyP(Frag.NotchedRect(X(-4), X(4), YA(Ro), YA(Yc + 0.5), ca, r), hOut, true, "outer-ring");
                            p.PolyP(Frag.NotchedRect(X(-4), X(4), YA(Rb), YA(Yc - 0.5), ca, r), hIn, true, "inner-ring");
                            p.Round(ca, 2.6, S.Vis, "rolling");
                            if (kind == Kind.Angular)
                            {
                                var d = new Vec2(Math.Sin(Geo.DegToRad(30)), Math.Cos(Geo.DegToRad(30)) * s) * p.K;
                                p.PolyP(new[] { ca - d * 3.4, ca + d * 3.4 }, S.Thin, false, "contact");
                            }
                            break;
                        }
                    case Kind.CylRoller:
                        p.Poly(hOut, true, -4, Y(Ro), 4, Y(Ro), 4, Y(Yc), 2.6, Y(Yc), 2.6, Y(12.3), -2.6, Y(12.3), -2.6, Y(Yc), -4, Y(Yc));
                        p.Box(-4, Y(Rb), 4, Y(8.7), hIn, "inner-ring");
                        p.Box(-2.3, Y(12.3), 2.3, Y(8.7), S.Vis, "rolling");
                        break;
                    case Kind.Needle:
                        p.Box(-4, Y(Ro), 4, Y(11.6), hOut, "outer-ring");
                        p.Box(-4, Y(Rb), 4, Y(9.4), hIn, "inner-ring");
                        p.Box(-3.4, Y(11.6), 3.4, Y(9.4), S.Vis, "rolling");
                        break;
                    case Kind.TaperRoller:
                        {
                            var a = Geo.DegToRad(15);
                            var u = new Vec2(Math.Cos(a), s < 0 ? -Math.Sin(a) : Math.Sin(a));
                            var n = new Vec2(-u.Y, u.X);
                            if (n.Y * s < 0) n = -n; // n points away from the axis
                            const double l = 2.6, w1 = 1.75, w2 = 1.25;
                            var roller = new[] { c - u * l - n * w1, c + u * l - n * w2, c + u * l + n * w2, c - u * l + n * w1 };
                            // Ring raceways run along the roller's outer and inner generatrix (cone).
                            double Through(Vec2 p1, Vec2 p2, double x) => p1.Y + (x - p1.X) * (p2.Y - p1.Y) / (p2.X - p1.X);
                            double OutY(double x) => Through(roller[3], roller[2], x);
                            double InY(double x) => Through(roller[0], roller[1], x);
                            p.PolyD(new[] { new Vec2(-4, Y(Ro)), new Vec2(4, Y(Ro)), new Vec2(4, OutY(4)), new Vec2(-4, OutY(-4)) }, hOut, true, "outer-ring");
                            p.PolyD(new[] { new Vec2(-4, Y(Rb)), new Vec2(4, Y(Rb)), new Vec2(4, InY(4)), new Vec2(-4, InY(-4)) }, hIn, true, "inner-ring");
                            p.PolyD(roller, S.Vis, true, "rolling");
                            break;
                        }
                    case Kind.Thrust:
                        p.Box(-4, Y(Rb + 0.6), -2.4, Y(13), hIn, "shaft-washer");
                        p.Box(2.4, Y(8), 4, Y(Ro), hOut, "housing-washer");
                        p.Hole(0, c.Y, 2.35, S.Vis, "rolling");
                        break;
                    case Kind.Plain:
                        p.Box(-4, Y(Ro), 4, Y(Rb), hOut, "bushing");
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ kinematic supports

        // Support symbols touch the shaft line with their tip: they have no meaningful bore and keep their size on a shaft.
        private static MechType Support(string id, I18nText name, bool floating, params string[] keywords) => new(
            id, name, Cat, keywords,
            new ViewSpec("stick", VStick, 12, 20, (p, o) => SupportDraw(p, floating)));

        /// <summary>Support symbol above and below the shaft: triangle on the housing; floating adds rollers.</summary>
        private static void SupportDraw(Pen p, bool floating)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var baseY = floating ? 5.2 : 6.2;
                p.Poly(S.Vis, true, 0, s * 0.9, -3.4, s * baseY, 3.4, s * baseY);
                if (floating)
                {
                    p.Round(p.V(-1.8, s * 5.9), 0.65, S.Vis, "roller");
                    p.Round(p.V(1.8, s * 5.9), 0.65, S.Vis, "roller");
                }
                var a = p.V(-6, s * 6.6);
                var b = p.V(6, s * 6.6);
                p.LineP(a, b, S.Vis, "ground");
                Frag.GroundStrokes(p, a, b, new Vec2(0, s), 3.2 * p.Sy, 1.6 * p.Sx);
            }
        }

        // ------------------------------------------------------------------ seal

        private static MechType Seal() => new(
            "mech.seal", T("Radial shaft seal", "Radialwellendichtring"), Cat,
            new[] { "seal", "lip seal", "oil seal", "shaft seal", "dichtung", "wellendichtring", "simmerring", "radialwellendichtring" },
            new ViewSpec("simplified", VSimple, 6, 30, SealSimplified) { Bore = RingBore },
            new ViewSpec("detailed", VDetail, 6, 30, SealDetailed) { Bore = RingBore });

        private static void SealSimplified(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Box(-3, s * Ro, 3, s * Rb, S.Vis, "outline");
                var c = p.V(0, s * Yc);
                p.Seg(c, -1.8, -1.8, 1.8, 1.8, S.Vis);
                p.Seg(c, -1.8, 1.8, 1.8, -1.8, S.Vis);
            }
        }

        private static void SealDetailed(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Poly(p.Hatch(45), true, -3, s * Ro, 3, s * Ro, 3, s * 12, 0.9, s * 12, 2.2, s * 6.2, 0.4, s * 6.2, -1.4, s * 10.4, -3, s * 10.4);
                p.Box(-3, s * Ro, -2.2, s * 10.4, S.Black, "case");
                p.Round(p.V(2.2, s * 9.8), 0.7, S.Vis, "spring");
            }
        }

        // ------------------------------------------------------------------ O-ring

        /// <summary>O-ring proportions (design mm): centre radius of the cord and cord radius; inner radius = bore.</summary>
        private const double OrC = 9.5, OrCord = 2.4;

        private static MechType ORing() => new(
            "mech.oring", T("O-ring", "O-Ring"), Cat,
            new[] { "o-ring", "oring", "o ring", "seal", "toroidal seal", "rundring", "dichtring", "dichtung" },
            // Section: the axis horizontal, the two cord sections blackened (thin sections, as small cut faces are drawn).
            new ViewSpec("section", T("Section", "Schnitt"), 6, 24, ORingSection) { Bore = (p, o) => (OrC - OrCord) * p.Sy },
            new ViewSpec("top", T("Top view", "Draufsicht"), 24, 24, ORingTop) { Bore = (p, o) => (OrC - OrCord) * p.K });

        private static void ORingSection(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 }) p.Round(p.V(0, s * OrC), OrCord, S.Black, "cord");
            p.Axis(3);
        }

        private static void ORingTop(Pen p, SketchObject o)
        {
            p.Circle(0, 0, OrC + OrCord, S.Vis, "outer");
            p.Circle(0, 0, OrC - OrCord, S.Vis, "inner");
            p.Circle(0, 0, OrC, S.Ctr, "cord-centre");
            p.CenterCross(0, 0, 12, 12);
        }

        // ------------------------------------------------------------------ housing / ground

        private static MechType Ground() => new(
            "mech.housing.ground", T("Housing / ground", "Gehäuse / Gestell"), Cat,
            new[] { "ground", "housing", "frame", "fixed", "gehäuse", "gestell", "festpunkt", "boden", "wand" },
            new ViewSpec("hatch", T("Line with hatch", "Linie mit Schraffur"), 40, 5, GroundHatch) { Fluid = true, Anchors = GroundAnchors },
            new ViewSpec("block", T("Hatched block", "Schraffierter Block"), 40, 5, (p, o) => p.Add(Prim.Rect(-p.W / 2, -p.H / 2, p.W, p.H, p.Hatch()), "ground")) { Fluid = true, Anchors = GroundAnchors })
        {
            Rotating = false,
        };

        private static IEnumerable<Anchor> GroundAnchors(Pen p, SketchObject o)
        {
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-p.W / 2, -p.H / 2), new Vec2(p.W / 2, -p.H / 2));
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, -p.H / 2), "mount");
        }

        private static void GroundHatch(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var a = new Vec2(-hw, -hh);
            var b = new Vec2(hw, -hh);
            p.LineP(a, b, S.Vis, "ground");
            Frag.GroundStrokes(p, a, b, new Vec2(0, 1), p.H, Math.Max(1.5, Math.Min(3, p.H * 0.6)));
        }

        // ------------------------------------------------------------------ linear guide

        private static MechType Guide() => new(
            "mech.guide.linear", T("Linear guide", "Linearführung"), Cat,
            new[] { "linear guide", "rail", "slider", "carriage", "prismatic", "linearführung", "schiene", "schlitten", "führung", "schubgelenk" },
            new ViewSpec("stick", VStick, 40, 12, GuideStick) { Fluid = true, Anchors = GuideAnchors },
            new ViewSpec("profile", T("Cross-section", "Querschnitt"), 20, 16, GuideProfile),
            new ViewSpec("side", T("Side view", "Seitenansicht"), 40, 12, GuideSide) { Fluid = true, Anchors = GuideAnchors })
        {
            Rotating = false,
        };

        private static IEnumerable<Anchor> GuideAnchors(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.End, new Vec2(-p.W / 2, 0), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(p.W / 2, 0), "right");
        }

        /// <summary>Prismatic pair: slider block on a guide line, guide fixed to ground.</summary>
        private static void GuideStick(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var sw = Math.Min(p.W * 0.3, 12);
            var y = hh * 0.25;
            p.Add(Prim.Rect(-sw / 2, -hh, sw, hh + y, S.Vis), "slider");
            p.LineP(new Vec2(-hw, y), new Vec2(hw, y), S.Stick, "rail");
            Frag.GroundStrokes(p, new Vec2(-hw, y), new Vec2(hw, y), new Vec2(0, 1), hh - y, Math.Max(1.5, Math.Min(3, p.H * 0.25)));
        }

        private static void GuideProfile(Pen p, SketchObject o)
        {
            p.Poly(p.Hatch(135), true, -9, -8, 9, -8, 9, 2, 6, 2, 6, -3, -6, -3, -6, 2, -9, 2);
            p.Box(-4.5, -2, 4.5, 8, p.Hatch(45), "rail");
            p.Hole(-5.25, 0, 0.7, S.Vis, "ball");
            p.Hole(5.25, 0, 0.7, S.Vis, "ball");
            p.Line(0, -8, 0, 8, S.Ctr, "center");
        }

        private static void GuideSide(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var y = -hh + p.H * 0.45;
            var cw = Math.Min(p.W * 0.35, 16);
            p.Add(Prim.Rect(-hw, y, p.W, hh - y, S.Vis), "rail");
            p.Add(Prim.Rect(-cw / 2, -hh, cw, y + hh, S.Vis), "carriage");
            p.LineP(new Vec2(-cw / 2, y - p.H * 0.08), new Vec2(cw / 2, y - p.H * 0.08), S.Hid, "balls");
        }
    }
}
