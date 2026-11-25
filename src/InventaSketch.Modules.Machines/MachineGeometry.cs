using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Reusable cross-section and longitudinal-section geometry of rotating electric machines (stators, rotors,
    /// housings, windings). All functions draw in design coordinates of a <see cref="Pen"/>; radii are design mm.
    /// The proportions are illustrative and are not the result of an electromagnetic design.
    /// </summary>
    internal static class Mag
    {
        public const double StatorHatch = 45;
        public const double RotorHatch = 135;
        public const double ShaftHatch = 45;
        public const double HousingHatch = 135;

        private static double Rad(double deg) => Geo.DegToRad(deg);

        /// <summary>Sectioned iron style (hatched unless the "hatch" parameter is off).</summary>
        public static Style Iron(Pen p, SketchObject o, double angle) => o.Flag("hatch", true) ? p.Hatch(angle) : S.Vis;

        /// <summary>White underlay without outline (covers hatching below an overlaid part, also in monochrome output).</summary>
        public static readonly Style Underlay = new() { Stroke = "none", Fill = Fill.Solid("#ffffff"), Role = LineRole.Thin };

        // ------------------------------------------------------------------ stator

        private readonly record struct SlotShape(double Tau, double Hb, double Ho, double H1, double H2, int D, double Depth);

        private static SlotShape Shape(int q, double rs, double rb, bool open)
        {
            var tau = 360.0 / q;
            var depth = Math.Abs(rb - rs);
            var hb = 0.25 * tau;
            return new SlotShape(tau, hb, open ? hb : 0.1 * tau, open ? 0 : 0.06 * depth, open ? 0 : 0.08 * depth, Math.Sign(rb - rs), depth);
        }

        /// <summary>
        /// Toothed air-gap contour of a stator with <paramref name="q"/> slots. <paramref name="rs"/> is the air-gap
        /// surface (tooth tips), <paramref name="rb"/> the slot bottom (larger for an inner-rotor stator, smaller for an
        /// outer-rotor stator). Open slots (switched reluctance) have no tooth-tip lips.
        /// </summary>
        public static List<Vec2> StatorContour(int q, double rs, double rb, bool open)
        {
            var sh = Shape(q, rs, rb, open);
            var pts = new List<Vec2>();
            for (var i = 0; i < q; i++)
            {
                var t = (i + 0.5) * sh.Tau;
                if (open)
                {
                    pts.Add(G.P(rs, t - sh.Hb));
                    pts.AddRange(G.ArcPts(rb, t - sh.Hb, t + sh.Hb, 4));
                    pts.Add(G.P(rs, t + sh.Hb));
                }
                else
                {
                    pts.Add(G.P(rs, t - sh.Ho));
                    pts.Add(G.P(rs + sh.D * sh.H1, t - sh.Ho));
                    pts.Add(G.P(rs + sh.D * (sh.H1 + sh.H2), t - sh.Hb));
                    pts.AddRange(G.ArcPts(rb, t - sh.Hb, t + sh.Hb, 4));
                    pts.Add(G.P(rs + sh.D * (sh.H1 + sh.H2), t + sh.Hb));
                    pts.Add(G.P(rs + sh.D * sh.H1, t + sh.Ho));
                    pts.Add(G.P(rs, t + sh.Ho));
                }
                var tip = G.ArcPts(rs, t + sh.Ho, t + sh.Tau - sh.Ho, 4).ToList();
                pts.AddRange(tip.Skip(1).Take(tip.Count - 2));
            }
            return pts;
        }

        /// <summary>
        /// Stator lamination (yoke + teeth) with slots, optional holes (cooling channels) and the winding in the slots.
        /// <paramref name="ry"/> is the yoke back surface (outer for inner-rotor stators, inner for outer-rotor stators).
        /// </summary>
        public static void Stator(Pen p, SketchObject o, int q, double rs, double rb, double ry, bool open, int poles,
            IEnumerable<IReadOnlyList<Vec2>>? holes = null, bool winding = true, bool currents = false)
        {
            var contour = StatorContour(q, rs, rb, open);
            var segs = new List<PathSeg>();
            if (ry > rs) { segs.AddRange(G.CircleSegs(ry)); segs.AddRange(G.Contour(contour, reverse: true)); }
            else { segs.AddRange(G.Contour(contour)); segs.AddRange(G.CircleSegs(ry, reverse: true)); }
            if (holes is not null) foreach (var h in holes) segs.AddRange(G.Contour(h, reverse: true));
            p.PathD(segs, Iron(p, o, StatorHatch), "stator");
            if (winding) SlotWinding(p, q, rs, rb, open, poles, currents);
        }

        /// <summary>One copper region per slot ("slot" tag); open slots (concentrated coils) get a dividing line.</summary>
        public static void SlotWinding(Pen p, int q, double rs, double rb, bool open, int poles, bool currents)
        {
            var sh = Shape(q, rs, rb, open);
            var r1 = rs + sh.D * (open ? 0.1 * sh.Depth : sh.H1 + sh.H2 + 0.04 * sh.Depth);
            var r2 = rb - sh.D * 0.04 * sh.Depth;
            var rm = (r1 + r2) / 2;
            var hc = sh.Hb - Geo.RadToDeg(0.3 / rm);
            if (hc <= 0.05 * sh.Tau) hc = 0.15 * sh.Tau;
            var width = 2 * rm * Math.Sin(Rad(hc));
            var depth = Math.Abs(r2 - r1);
            var copper = p.Copper(Math.Min(width, depth));
            for (var i = 0; i < q; i++)
            {
                var t = (i + 0.5) * sh.Tau;
                p.PolyD(G.Sector(r1, r2, t - hc, t + hc), copper, true, "slot");
                if (open) p.LineD(G.P(r1, t), G.P(r2, t), S.Thin, "coil-split");
                if (!currents) continue;
                if (open)
                {
                    var mr = Math.Min(width / 2, depth) * 0.3;
                    p.Current(G.P(rm, t - hc / 2), mr, true);
                    p.Current(G.P(rm, t + hc / 2), mr, false);
                }
                else
                {
                    // Illustrative instantaneous pattern: conductors under a north pole carry current out of the plane.
                    var belt = (int)Math.Floor((i + 0.5) * poles / q);
                    p.Current(G.P(rm, t), Math.Min(width, depth) * 0.3, belt % 2 == 0);
                }
            }
        }

        // ------------------------------------------------------------------ rotors

        /// <summary>
        /// Rotor cross-section. <paramref name="ra"/> is the air-gap surface, <paramref name="rb"/> the other surface (shaft bore for
        /// an inner rotor, outer surface for an outer rotor). <paramref name="poles"/> is the pole count (rotor teeth for SRM),
        /// <paramref name="bars"/> the cage bar count (ASM).
        /// </summary>
        public static void Rotor(Pen p, SketchObject o, string kind, int poles, int bars, double ra, double rb, bool currents)
        {
            var s = Math.Sign(ra - rb); // +1: inner rotor (air gap outside)
            var h = Math.Abs(ra - rb);
            var rmax = Math.Max(ra, rb);
            var hd = Math.Min(h, 0.6 * rmax);
            double R(double d) => ra - s * d;
            var alpha = 360.0 / poles;
            var iron = Iron(p, o, RotorHatch);
            var segs = new List<PathSeg>();
            var overlays = new List<Action>();

            void Round(double r, bool hole) => segs.AddRange(G.CircleSegs(r, hole));
            void Shape(List<Vec2> contour)
            {
                if (s > 0) { segs.AddRange(G.Contour(contour)); Round(rb, true); }
                else { Round(rb, false); segs.AddRange(G.Contour(contour, reverse: true)); }
            }
            void Plain() { Round(Math.Max(ra, rb), false); Round(Math.Min(ra, rb), true); }

            switch (kind)
            {
                case "ipm":
                    {
                        Plain();
                        var rA = R(0.42 * hd); var rB = R(0.17 * hd);
                        var wA = 0.10 * Rad(alpha) * rA;
                        var wB = Math.Min(0.36 * Rad(alpha), 0.8 * Math.Sin(Rad(alpha) / 2)) * rB;
                        for (var k = 0; k < poles; k++)
                        {
                            var phi = k * alpha;
                            foreach (var side in new[] { -1, 1 })
                            {
                                var a = G.PO(rA, phi, side * wA);
                                var b = G.PO(rB, phi, side * wB);
                                var len = (b - a).Length;
                                var dir = (b - a) / len;
                                var t = Math.Min(Math.Min(0.22 * len, 0.08 * rmax), 0.18 * h);
                                segs.AddRange(G.Contour(G.Bar(a - dir * (0.25 * t), b + dir * (0.35 * t), t), reverse: true));
                                var mag = G.Bar(a + dir * (0.15 * t), b - dir * (0.2 * t), t * 0.86);
                                var north = k % 2 == 0;
                                overlays.Add(() => p.PolyD(mag, S.Magnet(north), true, "magnet"));
                            }
                        }
                        break;
                    }
                case "spoke":
                    {
                        Plain();
                        var r1 = R(0.05 * hd); var r2 = R(0.75 * hd);
                        var w = Math.Min(0.22 * Rad(alpha) * Math.Min(r1, r2), 0.1 * rmax);
                        for (var k = 0; k < poles; k++)
                        {
                            var psi = k * alpha + alpha / 2;
                            var a = G.P(r1, psi); var b = G.P(r2, psi);
                            segs.AddRange(G.Contour(G.Bar(a, b, w), reverse: true));
                            var dir = (b - a).Normalized();
                            var mag = G.Bar(a + dir * (0.1 * w), b - dir * (0.1 * w), w * 0.86);
                            var north = k % 2 == 0;
                            overlays.Add(() => p.PolyD(mag, S.Magnet(north), true, "magnet"));
                        }
                        break;
                    }
                case "eesm":
                    {
                        var ry = R(0.5 * hd); var rsu = R(0.15 * hd);
                        var hs = 0.37 * alpha;
                        var ws = Math.Min(0.17 * Rad(alpha) * Math.Min(ry, rsu), 0.6 * Math.Min(ry, rsu) * Math.Sin(Rad(alpha) / 2));
                        var pts = new List<Vec2>();
                        for (var k = 0; k < poles; k++)
                        {
                            var phi = k * alpha;
                            pts.Add(G.PO(ry, phi, -ws));
                            pts.AddRange(G.ArcPts(rsu, G.Off(rsu, phi, -ws), phi - hs, 3));
                            pts.AddRange(G.ArcPts(ra, phi - hs, phi + hs));
                            pts.AddRange(G.ArcPts(rsu, phi + hs, G.Off(rsu, phi, ws), 3));
                            pts.Add(G.PO(ry, phi, ws));
                            var yoke = G.ArcPts(ry, G.Off(ry, phi, ws), G.Off(ry, phi + alpha, -ws)).ToList();
                            pts.AddRange(yoke.Skip(1).Take(yoke.Count - 2));
                        }
                        Shape(pts);
                        // Field winding around each pole shank.
                        var c1 = R(0.46 * hd); var c2 = R(0.2 * hd);
                        var wi = ws + 0.25;
                        var wmax = Math.Min(c1, c2) * Math.Sin(Rad(alpha) / 2) - 0.35;
                        var wo = wi + 0.8 * (wmax - wi);
                        if (wo - wi > 0.3)
                        {
                            var copper = p.Copper(Math.Min(wo - wi, Math.Abs(c2 - c1)));
                            for (var k = 0; k < poles; k++)
                            {
                                var phi = k * alpha;
                                foreach (var side in new[] { -1, 1 })
                                {
                                    var poly = new List<Vec2> { G.PO(c1, phi, side * wi), G.PO(c2, phi, side * wi), G.PO(c2, phi, side * wo), G.PO(c1, phi, side * wo) };
                                    var kk = k; var sd = side;
                                    overlays.Add(() =>
                                    {
                                        p.PolyD(poly, copper, true, "field");
                                        if (currents)
                                        {
                                            var c = new Vec2(poly.Average(v => v.X), poly.Average(v => v.Y));
                                            p.Current(c, Math.Min(wo - wi, Math.Abs(c2 - c1)) * 0.3, (kk % 2 == 0) == (sd < 0));
                                        }
                                    });
                                }
                            }
                        }
                        break;
                    }
                case "asm":
                    {
                        Plain();
                        var pitch = 2 * Math.PI * R(0.1 * hd) / bars;
                        var rr = Math.Min(0.28 * pitch, 0.06 * hd);
                        var a = R(0.04 * hd + rr); var b = R(0.3 * hd - rr);
                        var fill = S.Thin with { Fill = Fill.Solid(S.CopperColor) };
                        for (var j = 0; j < bars; j++)
                        {
                            var ang = (j + 0.5) * 360.0 / bars;
                            var bar = G.Stadium(G.P(a, ang), G.P(b, ang), rr);
                            segs.AddRange(G.Contour(bar, reverse: true));
                            overlays.Add(() => p.PolyD(bar, fill, true, "bar"));
                        }
                        break;
                    }
                case "srm":
                    {
                        var beta = 0.21 * alpha; var betaR = 0.27 * alpha;
                        var rr = R(0.3 * hd);
                        var pts = new List<Vec2>();
                        for (var k = 0; k < poles; k++)
                        {
                            var phi = k * alpha;
                            pts.Add(G.P(rr, phi - betaR));
                            pts.AddRange(G.ArcPts(ra, phi - beta, phi + beta, 3));
                            pts.Add(G.P(rr, phi + betaR));
                            var root = G.ArcPts(rr, phi + betaR, phi + alpha - betaR).ToList();
                            pts.AddRange(root.Skip(1).Take(root.Count - 2));
                        }
                        Shape(pts);
                        break;
                    }
                default: // spm
                    {
                        var tm = Math.Min(0.11 * rmax, 0.45 * h);
                        Round(Math.Max(R(tm), rb), false);
                        Round(Math.Min(R(tm), rb), true);
                        var span = 0.82 * alpha;
                        for (var k = 0; k < poles; k++)
                        {
                            var phi = k * alpha;
                            var mag = G.Sector(ra, R(tm), phi - span / 2, phi + span / 2);
                            var north = k % 2 == 0;
                            overlays.Add(() => p.PolyD(mag, S.Magnet(north), true, "magnet"));
                        }
                        break;
                    }
            }
            p.PathD(segs, iron, "rotor");
            foreach (var a in overlays) a();
        }

        /// <summary>Shaft cross-section (hatched).</summary>
        public static void ShaftSection(Pen p, SketchObject o, double r) => p.Round(new Vec2(0, 0), r, Iron(p, o, ShaftHatch), "shaft");

        /// <summary>Housing ring (section) with optional cooling-jacket channels and cooling fins.</summary>
        public static void HousingRing(Pen p, SketchObject o, double ri, double ro, bool cooling, int channels = 24, bool fins = false, int finCount = 36)
        {
            var segs = new List<PathSeg>();
            if (fins)
            {
                var rBase = ri + 0.55 * (ro - ri);
                var pitch = 360.0 / finCount;
                var pts = new List<Vec2>();
                for (var i = 0; i < finCount; i++)
                {
                    var c = i * pitch;
                    var fw = Geo.RadToDeg(0.35 * (ro - ri) / ro);
                    pts.AddRange(G.ArcPts(rBase, c - pitch / 2, c - fw, 2));
                    pts.AddRange(G.ArcPts(ro, c - fw, c + fw, 1));
                    pts.AddRange(G.ArcPts(rBase, c + fw, c + pitch / 2, 2).Take(2));
                }
                segs.AddRange(G.Contour(pts));
                ro = rBase;
            }
            else segs.AddRange(G.CircleSegs(ro));
            segs.AddRange(G.CircleSegs(ri, reverse: true));
            if (cooling) foreach (var h in JacketChannels(ri, ro, channels)) segs.AddRange(G.Contour(h, reverse: true));
            p.PathD(segs, Iron(p, o, HousingHatch), "housing");
        }

        /// <summary>Axial cooling channels evenly spaced in a ring between ri and ro.</summary>
        public static List<IReadOnlyList<Vec2>> JacketChannels(double ri, double ro, int n)
        {
            var list = new List<IReadOnlyList<Vec2>>();
            var w = ro - ri;
            var pitch = 360.0 / n;
            for (var i = 0; i < n; i++)
            {
                var c = (i + 0.5) * pitch;
                list.Add(G.Sector(ri + 0.25 * w, ro - 0.25 * w, c - 0.3 * pitch, c + 0.3 * pitch));
            }
            return list;
        }

        /// <summary>Round holes (cooling channels in a stator carrier) at radius r.</summary>
        public static List<IReadOnlyList<Vec2>> RoundHoles(double r, double hr, int n)
            => Enumerable.Range(0, n).Select(i => (IReadOnlyList<Vec2>)Geo.ArcPoints(G.P(r, (i + 0.5) * 360.0 / n), hr, hr, 0, 2 * Math.PI, 16).Take(16).ToList()).ToList();

        // ------------------------------------------------------------------ longitudinal sections

        /// <summary>Upper (sign -1) or lower (sign +1) half of a rectangle band x0..x1 between radii r0 and r1.</summary>
        public static void Band(Pen p, double x0, double x1, double r0, double r1, int sign, Style st, string tag)
            => p.Box(x0, sign * r0, x1, sign * r1, st, tag);

        /// <summary>
        /// Coil sides through the stack plus end windings on both sides (one half of the section).
        /// </summary>
        public static List<Vec2> WindingWithEnds(double xs, double rIn, double rOut, int sign)
        {
            var mid = (rIn + rOut) / 2;
            var rr = (rOut - rIn) / 2 + 0.6;
            var pts = new List<Vec2> { new(-xs, sign * rOut), new(xs, sign * rOut) };
            var c = new Vec2(xs + 2, sign * mid);
            // right end winding: top -> right -> bottom (in screen terms, for the upper half)
            var right = Geo.ArcPoints(c, rr, rr, -Math.PI / 2, Math.PI, 16);
            var left = Geo.ArcPoints(new Vec2(-xs - 2, sign * mid), rr, rr, Math.PI / 2, Math.PI, 16);
            if (sign > 0) { right.Reverse(); left.Reverse(); }
            pts.AddRange(right);
            pts.Add(new Vec2(xs, sign * rIn));
            pts.Add(new Vec2(-xs, sign * rIn));
            pts.AddRange(left);
            return pts;
        }

        /// <summary>
        /// Longitudinal rotor section (both halves) for a rotor type: hatched core, magnets / bars / pockets as an overlaid band.
        /// ra = air-gap radius, rb = other radius; x0..x1 stack length.
        /// </summary>
        public static void RotorSide(Pen p, SketchObject o, string kind, double x0, double x1, double ra, double rb, bool core = true)
        {
            var s = Math.Sign(ra - rb);
            var h = Math.Abs(ra - rb);
            var rmax = Math.Max(ra, rb);
            var hd = Math.Min(h, 0.6 * rmax);
            double R(double d) => ra - s * d;
            var iron = Iron(p, o, RotorHatch);
            foreach (var sign in new[] { -1, 1 })
            {
                if (core) Band(p, x0, x1, Math.Min(ra, rb), Math.Max(ra, rb), sign, iron, "rotor");
                var north = sign < 0;
                void Over(double d0, double d1, Style st, string tag, double ext = 0)
                {
                    Band(p, x0 - ext, x1 + ext, R(d0), R(d1), sign, Underlay, "underlay");
                    Band(p, x0 - ext, x1 + ext, R(d0), R(d1), sign, st, tag);
                }
                switch (kind)
                {
                    case "spm": Over(0, Math.Min(0.11 * rmax, 0.45 * h), S.Magnet(north), "magnet"); break;
                    case "ipm": Over(0.2 * hd, 0.36 * hd, S.Magnet(north), "magnet"); break;
                    case "spoke": Over(0.08 * hd, 0.72 * hd, S.Magnet(north), "magnet"); break;
                    case "asm":
                        {
                            var bar = S.Thin with { Fill = Fill.Solid(S.CopperColor) };
                            Over(0.06 * hd, 0.28 * hd, bar, "bar");
                            // short-circuit end rings
                            var len = x1 - x0;
                            foreach (var (a, b) in new[] { (x0 - 0.07 * len, x0), (x1, x1 + 0.07 * len) })
                                Band(p, a, b, R(0.02 * hd), R(0.32 * hd), sign, bar, "endring");
                            break;
                        }
                    case "eesm":
                        {
                            var len = x1 - x0;
                            var copper = p.Copper(0.25 * hd);
                            foreach (var (a, b) in new[] { (x0 - 0.09 * len, x0), (x1, x1 + 0.09 * len) })
                                Band(p, a, b, R(0.2 * hd), R(0.46 * hd), sign, copper, "field");
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Simplified rolling bearings (rectangle with a ball) between radii r0 and r1 at x0..x1, upper and lower half;
        /// <paramref name="mirrored"/> adds the bearing at -x1..-x0 (both end shields).
        /// </summary>
        public static void Bearings(Pen p, double x0, double x1, double r0, double r1, bool mirrored = true)
        {
            foreach (var m in mirrored ? new[] { 1, -1 } : new[] { 1 })
                foreach (var sign in new[] { -1, 1 })
                {
                    double a = m * x0, b = m * x1;
                    p.Box(a, sign * r0, b, sign * r1, S.Vis, "bearing");
                    var rb = Math.Min(x1 - x0, r1 - r0) * 0.3;
                    p.Round(new Vec2((a + b) / 2, sign * (r0 + r1) / 2), rb, S.Thin, "bearing");
                }
        }

        /// <summary>Housing half section: shell plus end shields with bearing seats, optional jacket channels.</summary>
        public static void HousingSide(Pen p, SketchObject o, double xo, double xi, double rOut, double rIn, double rSeat, bool cooling, int channels = 8)
        {
            foreach (var sign in new[] { -1, 1 })
            {
                var pts = new List<Vec2>
                {
                    new(-xo, sign * rOut), new(xo, sign * rOut), new(xo, sign * rSeat), new(xi, sign * rSeat),
                    new(xi, sign * rIn), new(-xi, sign * rIn), new(-xi, sign * rSeat), new(-xo, sign * rSeat),
                };
                var segs = new List<PathSeg>(G.Contour(pts));
                if (cooling)
                {
                    var w = rOut - rIn;
                    var span = 2 * xi * 0.92;
                    var pitch = span / channels;
                    for (var i = 0; i < channels; i++)
                    {
                        var cx = -span / 2 + (i + 0.5) * pitch;
                        var hole = new List<Vec2>
                        {
                            new(cx - 0.32 * pitch, sign * (rIn + 0.25 * w)), new(cx + 0.32 * pitch, sign * (rIn + 0.25 * w)),
                            new(cx + 0.32 * pitch, sign * (rOut - 0.25 * w)), new(cx - 0.32 * pitch, sign * (rOut - 0.25 * w)),
                        };
                        segs.AddRange(G.Contour(hole, reverse: true));
                    }
                }
                p.PathD(segs, Iron(p, o, HousingHatch), "housing");
            }
        }

        // ------------------------------------------------------------------ schematic symbol

        private static readonly string[] PhaseNames = { "U", "V", "W", "X", "Y", "Z" };

        /// <summary>Lead x positions of the motor symbol (design 50 x 50, circle centre (-5, 6), radius 16).</summary>
        public static double[] LeadXs(int phases)
        {
            phases = Math.Max(1, Math.Min(6, phases));
            var spacing = phases <= 3 ? 8.0 : 4.6;
            return Enumerable.Range(0, phases).Select(i => -5 + (i - (phases - 1) / 2.0) * spacing).ToArray();
        }

        /// <summary>
        /// Motor/generator symbol (IEC 60617 style): circle with "M"/"G" and the phase number, phase leads with terminals at
        /// the top and the shaft stub to the right. Design box 50 x 50.
        /// </summary>
        public static void MotorSymbol(Pen p, SketchObject o, bool shaft = true)
        {
            var phases = Math.Max(1, Math.Min(6, o.Int("phases", 3)));
            var gen = o.Str("mode", "motor") == "generator";
            const double cx = -5, cy = 6, r = 16;
            p.Circle(cx, cy, r, S.Sym, "symbol");
            p.Text(cx, cy - 3, gen ? "G" : "M", 11, "letter", bold: true);
            p.Text(cx, cy + 8, phases == 1 ? "1~" : phases + "~", 5.5, "phases");
            var xs = LeadXs(phases);
            for (var i = 0; i < xs.Length; i++)
            {
                var x = xs[i];
                var y0 = cy - Math.Sqrt(r * r - (x - cx) * (x - cx));
                p.Line(x, y0, x, -21.8, S.Wire, "lead");
                p.Round(new Vec2(x, -23), 1.2, S.White with { StrokeWidth = 0.3 }, "terminal");
                if (phases <= 3) p.Text(x + 2.4, -17, PhaseNames[i], 3, "phase", anchor: TextAnchor.Start);
            }
            if (shaft) p.Line(cx + r, cy, 25, cy, S.Vis with { StrokeWidth = 0.9 }, "shaft");
        }

        public static IEnumerable<Anchor> MotorSymbolAnchors(Pen p, SketchObject o, bool shaft = true)
        {
            var xs = LeadXs(o.Int("phases", 3));
            for (var i = 0; i < xs.Length; i++) yield return Anchor.Point(AnchorRole.Port, p.V(xs[i], -23), PhaseNames[i].ToLowerInvariant());
            if (shaft) yield return Anchor.Segment(AnchorRole.Axis, p.V(11, 6), p.V(25, 6));
        }
    }
}
