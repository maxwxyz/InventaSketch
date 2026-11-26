using System;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>Four-bar linkage position: ground pivots O2 (crank) and O4 (rocker), crank pin A, coupler/rocker joint B.</summary>
    public readonly record struct FourBarPose(Vec2 O2, Vec2 A, Vec2 B, Vec2 O4);

    /// <summary>Slider-crank position: crank pivot O, crank pin A, slider (piston pin) B on the line y = offset.</summary>
    public readonly record struct SliderCrankPose(Vec2 O, Vec2 A, Vec2 B);

    /// <summary>
    /// Closed-form position analysis (loop closure) of the predefined planar mechanisms. Coordinates in model
    /// millimetres, y down; angles in degrees, positive = clockwise on screen (project convention). A null result
    /// means the loop cannot be closed at that input angle ("no assembly possible").
    /// </summary>
    public static class Linkages
    {
        /// <summary>Unit direction of a screen angle (degrees, clockwise positive with y down).</summary>
        public static Vec2 Dir(double deg) => new(Math.Cos(Geo.DegToRad(deg)), Math.Sin(Geo.DegToRad(deg)));

        /// <summary>
        /// Intersection of the circles (c0, r0) and (c1, r1). <paramref name="side"/> = -1 or +1 selects the solution
        /// on the left or right of the direction c0 → c1 (screen: -1 = counter-clockwise side). Null if they do not meet.
        /// </summary>
        public static Vec2? CircleCircle(Vec2 c0, double r0, Vec2 c1, double r1, int side)
        {
            var e = c1 - c0;
            var f = e.Length;
            if (f < 1e-12 || r0 < 0 || r1 < 0) return null;
            const double tol = 1e-9;
            if (f > r0 + r1 + tol || f < Math.Abs(r0 - r1) - tol) return null;
            var h = (r0 * r0 - r1 * r1 + f * f) / (2 * f);
            var k2 = r0 * r0 - h * h;
            var k = k2 <= 0 ? 0 : Math.Sqrt(k2);
            var u = e / f;
            return c0 + u * h + u.Perp * (side >= 0 ? k : -k);
        }

        /// <summary>
        /// Four-bar linkage with the ground pivots O2 = (0,0) and O4 = (ground, 0). <paramref name="crossed"/> selects
        /// the crossed assembly mode instead of the open one.
        /// </summary>
        public static FourBarPose? FourBar(double ground, double crank, double coupler, double rocker, double angleDeg, bool crossed = false)
        {
            var o2 = Vec2.Zero;
            var o4 = new Vec2(ground, 0);
            var a = o2 + Dir(angleDeg) * crank;
            var b = CircleCircle(a, coupler, o4, rocker, crossed ? 1 : -1);
            return b is null ? null : new FourBarPose(o2, a, b.Value, o4);
        }

        /// <summary>
        /// Slider-crank with the crank pivot O = (0,0), the slider moving on the line y = <paramref name="offset"/> on the
        /// side x &gt; A.x (the usual in-line or offset engine layout).
        /// </summary>
        public static SliderCrankPose? SliderCrank(double crank, double rod, double offset, double angleDeg)
        {
            var a = Dir(angleDeg) * crank;
            var dy = offset - a.Y;
            var k2 = rod * rod - dy * dy;
            if (k2 < -1e-9) return null;
            var b = new Vec2(a.X + Math.Sqrt(Math.Max(0, k2)), offset);
            return new SliderCrankPose(Vec2.Zero, a, b);
        }

        /// <summary>Grübler/Kutzbach mobility of a planar mechanism: F = 3(n − 1) − 2·j1 − j2.</summary>
        public static int Grubler(int links, int lowerPairs, int higherPairs = 0) => 3 * (links - 1) - 2 * lowerPairs - higherPairs;

        /// <summary>Grashof condition s + l ≤ p + q (at least one link can rotate fully).</summary>
        public static bool IsGrashof(double ground, double crank, double coupler, double rocker)
        {
            var l = new[] { ground, crank, coupler, rocker };
            Array.Sort(l);
            return l[0] + l[3] <= l[1] + l[2] + 1e-12;
        }

        // ------------------------------------------------------------------ four-bar analysis (textbook relations, [S24])

        /// <summary>
        /// (p + q) − (s + l) of the four link lengths: positive = Grashof (one link turns fully), zero = change point,
        /// negative = non-Grashof (no link turns fully).
        /// </summary>
        public static double GrashofMargin(double ground, double crank, double coupler, double rocker)
        {
            var l = new[] { ground, crank, coupler, rocker };
            Array.Sort(l);
            return l[1] + l[2] - l[0] - l[3];
        }

        /// <summary>
        /// Grashof class of a four-bar driven by its crank (link next to the frame): the shortest link decides which links
        /// turn fully when s + l &lt; p + q; s + l = p + q is the change point; s + l &gt; p + q gives a (triple) rocker.
        /// </summary>
        public static GrashofClass Classify(double ground, double crank, double coupler, double rocker)
        {
            var l = new[] { ground, crank, coupler, rocker };
            Array.Sort(l);
            var diff = l[0] + l[3] - (l[1] + l[2]);
            if (Math.Abs(diff) <= 1e-9 * Math.Max(1, l[3])) return GrashofClass.ChangePoint;
            if (diff > 0) return GrashofClass.TripleRocker;
            // Strictly Grashof: the shortest link is unique (two equal shortest links would need l < q).
            if (ground == l[0]) return GrashofClass.DoubleCrank;
            if (crank == l[0]) return GrashofClass.CrankRocker;
            if (coupler == l[0]) return GrashofClass.GrashofDoubleRocker;
            return GrashofClass.RockerCrank;
        }

        /// <summary>Bilingual name of a Grashof class.</summary>
        public static I18nText ClassName(GrashofClass c) => c switch
        {
            GrashofClass.CrankRocker => new I18nText("Crank-rocker", "Kurbelschwinge"),
            GrashofClass.DoubleCrank => new I18nText("Double-crank", "Doppelkurbel"),
            GrashofClass.RockerCrank => new I18nText("Rocker-crank", "Schwingkurbel"),
            GrashofClass.GrashofDoubleRocker => new I18nText("Double-rocker (coupler turns)", "Doppelschwinge (Koppel läuft um)"),
            GrashofClass.ChangePoint => new I18nText("Change point", "Durchschlaggetriebe"),
            _ => new I18nText("Double-rocker (non-Grashof)", "Doppelschwinge (Nicht-Grashof)"),
        };

        /// <summary>True if the input crank of the four-bar can turn fully (crank or frame is the shortest link of a Grashof chain).</summary>
        public static bool CrankTurnsFully(double ground, double crank, double coupler, double rocker)
        {
            var c = Classify(ground, crank, coupler, rocker);
            if (c is GrashofClass.CrankRocker or GrashofClass.DoubleCrank) return true;
            if (c != GrashofClass.ChangePoint) return false;
            var s = Math.Min(Math.Min(ground, crank), Math.Min(coupler, rocker));
            return crank == s || ground == s;
        }

        /// <summary>True if the crank turns fully and the rocker oscillates (crank-rocker, also at the change point with the crank shortest).</summary>
        public static bool IsCrankRocker(double ground, double crank, double coupler, double rocker)
        {
            var c = Classify(ground, crank, coupler, rocker);
            var s = Math.Min(Math.Min(ground, crank), Math.Min(coupler, rocker));
            return c == GrashofClass.CrankRocker || c == GrashofClass.ChangePoint && crank == s && ground != s;
        }

        private static double Acos(double x) => Geo.RadToDeg(Math.Acos(Math.Max(-1, Math.Min(1, x))));

        /// <summary>
        /// Smallest and largest transmission angle μ (degrees, angle between coupler and rocker at B) over all positions the
        /// four-bar reaches: μ follows from the cosine rule cos μ = (b² + c² − x²)/(2bc) with x = |A O4|, which ranges over
        /// [max(|d − a|, |b − c|), min(d + a, b + c)] (the crank's dead positions, or the limits where coupler and rocker are
        /// aligned). Null if the chain cannot be assembled at all.
        /// </summary>
        public static (double Min, double Max)? TransmissionAngles(double ground, double crank, double coupler, double rocker)
        {
            double a = crank, b = coupler, c = rocker, d = ground;
            var lo = Math.Max(Math.Abs(d - a), Math.Abs(b - c));
            var hi = Math.Min(d + a, b + c);
            if (lo > hi + 1e-12 || b <= 0 || c <= 0) return null;
            double Mu(double x) => Acos((b * b + c * c - x * x) / (2 * b * c));
            return (Mu(lo), Mu(hi));
        }

        /// <summary>
        /// Swing angle of the rocker of a crank-rocker (degrees): the rocker's dead positions have crank and coupler aligned,
        /// |O2 B| = b ± a, so ψ = arccos((d² + c² − (b + a)²)/(2dc)) − arccos((d² + c² − (b − a)²)/(2dc)). Null for other classes.
        /// </summary>
        public static double? RockerSwing(double ground, double crank, double coupler, double rocker)
        {
            if (!IsCrankRocker(ground, crank, coupler, rocker)) return null;
            double a = crank, b = coupler, c = rocker, d = ground;
            return Math.Abs(Acos((d * d + c * c - (b + a) * (b + a)) / (2 * d * c)) - Acos((d * d + c * c - (b - a) * (b - a)) / (2 * d * c)));
        }

        /// <summary>
        /// Crank angle α between the two dead positions of a crank-rocker (degrees, ≥ 0): the crank turns 180° + α for one
        /// stroke of the rocker and 180° − α for the other. α = arccos((d² + (b − a)² − c²)/(2d(b − a))) − arccos((d² + (b + a)² − c²)/(2d(b + a))).
        /// Null for other classes.
        /// </summary>
        public static double? CrankAngleDifference(double ground, double crank, double coupler, double rocker)
        {
            if (!IsCrankRocker(ground, crank, coupler, rocker)) return null;
            double a = crank, b = coupler, c = rocker, d = ground;
            if (b - a <= 0) return null;
            return Math.Abs(Acos((d * d + (b - a) * (b - a) - c * c) / (2 * d * (b - a))) - Acos((d * d + (b + a) * (b + a) - c * c) / (2 * d * (b + a))));
        }

        /// <summary>Time ratio (quick-return ratio) Q = (180° + α)/(180° − α) ≥ 1 from the crank angle difference α in degrees.</summary>
        public static double TimeRatio(double alphaDeg)
        {
            var al = Math.Abs(alphaDeg);
            return al >= 180 ? double.NaN : (180 + al) / (180 - al);
        }

        /// <summary>Transmission angle μ at one position (degrees): the angle at B between the coupler B→A and the rocker B→O4.</summary>
        public static double TransmissionAngle(FourBarPose p)
        {
            var u = p.A - p.B; var v = p.O4 - p.B;
            var n = u.Length * v.Length;
            return n < 1e-12 ? double.NaN : Acos(u.Dot(v) / n);
        }

        /// <summary>Rocker (output) angle at one position: direction of O4→B in screen degrees (clockwise from +x), in (−180°, 180°].</summary>
        public static double OutputAngle(FourBarPose p) => Geo.RadToDeg(Math.Atan2(p.B.Y - p.O4.Y, p.B.X - p.O4.X));

        /// <summary>
        /// Angular velocity ratio ω4/ω2 of rocker and crank at one position (same sense = positive):
        /// ω4/ω2 = a·sin(θ2 − θ3)/(c·sin(θ4 − θ3)) from the derivative of the loop equation. NaN at a toggle position.
        /// </summary>
        public static double VelocityRatio(FourBarPose p)
        {
            var ca = p.A - p.O2; var cb = p.B - p.A; var cr = p.B - p.O4;
            double t2 = Math.Atan2(ca.Y, ca.X), t3 = Math.Atan2(cb.Y, cb.X), t4 = Math.Atan2(cr.Y, cr.X);
            var den = cr.Length * Math.Sin(t4 - t3);
            return Math.Abs(den) < 1e-12 ? double.NaN : ca.Length * Math.Sin(t2 - t3) / den;
        }

        // ------------------------------------------------------------------ slider-crank analysis [S24]

        /// <summary>
        /// Stroke of an (offset) slider-crank: the dead positions have crank and rod aligned, so
        /// H = √((l + r)² − e²) − √((l − r)² − e²) (2r without offset). Null if the crank cannot turn fully (l &lt; r + |e|).
        /// </summary>
        public static double? SliderStroke(double crank, double rod, double offset)
        {
            double r = crank, l = rod, e = Math.Abs(offset);
            if (l < r + e - 1e-12) return null;
            return Math.Sqrt(Math.Max(0, (l + r) * (l + r) - e * e)) - Math.Sqrt(Math.Max(0, (l - r) * (l - r) - e * e));
        }

        /// <summary>Crank angle α between the dead positions of an offset slider-crank (degrees, ≥ 0): α = arcsin(e/(l − r)) − arcsin(e/(l + r)). Null if the crank cannot turn fully.</summary>
        public static double? SliderCrankAngleDifference(double crank, double rod, double offset)
        {
            double r = crank, l = rod, e = Math.Abs(offset);
            if (l < r + e - 1e-12 || l - r <= 0) return null;
            return Geo.RadToDeg(Math.Asin(Math.Min(1, e / (l - r))) - Math.Asin(Math.Min(1, e / (l + r))));
        }

        /// <summary>Smallest transmission angle of a slider-crank (degrees): 90° minus the largest rod obliquity, μ_min = 90° − arcsin((r + |e|)/l). Null if the crank cannot turn fully.</summary>
        public static double? SliderMinTransmission(double crank, double rod, double offset)
        {
            var k = (crank + Math.Abs(offset)) / rod;
            return k > 1 + 1e-12 ? null : 90 - Geo.RadToDeg(Math.Asin(Math.Min(1, k)));
        }

        /// <summary>
        /// dx_B/dφ of the slider-crank at crank angle φ (mm per radian, x along the guide):
        /// −r·sin φ + r·cos φ·(e − r·sin φ)/√(l² − (e − r·sin φ)²). NaN where the position cannot be assembled.
        /// </summary>
        public static double SliderSpeedFactor(double crank, double rod, double offset, double angleDeg)
        {
            var t = Geo.DegToRad(angleDeg);
            double r = crank, l = rod, e = offset;
            var dy = e - r * Math.Sin(t);
            var k2 = l * l - dy * dy;
            if (k2 <= 1e-12) return double.NaN;
            return -r * Math.Sin(t) + r * Math.Cos(t) * dy / Math.Sqrt(k2);
        }

        // ------------------------------------------------------------------ crank and slotted lever (quick return) [S24]

        /// <summary>
        /// Position of the ram B of the crank and slotted lever mechanism (lever pivot O4 = (0,0), crank pivot O2 = (0, −d),
        /// ram guide y = −h): x of B, or null where it cannot be assembled.
        /// </summary>
        public static double? QuickReturnRam(double ground, double crank, double lever, double link, double ram, double angleDeg)
        {
            var o2 = new Vec2(0, -ground);
            var a = o2 + Dir(angleDeg) * crank;
            if (a.Length <= 1e-9) return null;
            var t = a.Normalized() * lever;
            var dy = -ram - t.Y;
            var k2 = link * link - dy * dy;
            if (k2 < -1e-9) return null;
            return t.X + Math.Sqrt(Math.Max(0, k2));
        }

        /// <summary>Swing angle of the slotted lever (degrees): its dead positions have the crank at right angles to it, ψ = 2·arcsin(r/d). Null if r ≥ d (the lever turns fully).</summary>
        public static double? LeverSwing(double ground, double crank) => crank < ground ? 2 * Geo.RadToDeg(Math.Asin(crank / ground)) : null;

        /// <summary>Crank angle difference of the slotted lever (degrees): α = 180° − 2·arccos(r/d), so Q = (360° − 2·arccos(r/d))/(2·arccos(r/d)). Null if r ≥ d.</summary>
        public static double? LeverCrankAngleDifference(double ground, double crank) => crank < ground ? 180 - 2 * Acos(crank / ground) : null;

        /// <summary>
        /// Smallest and largest value of <paramref name="f"/> over a full turn of the input angle (degrees): sampled every
        /// 0.5°, then refined by golden-section search around the best samples. Null if f is never defined.
        /// </summary>
        public static (double Min, double Max)? Extremes(Func<double, double?> f)
        {
            double bestMin = double.MaxValue, bestMax = double.MinValue, atMin = 0, atMax = 0;
            for (var i = 0; i < 720; i++)
            {
                var a = i * 0.5;
                if (f(a) is not double v || double.IsNaN(v)) continue;
                if (v < bestMin) { bestMin = v; atMin = a; }
                if (v > bestMax) { bestMax = v; atMax = a; }
            }
            if (bestMin > bestMax) return null;
            double Refine(double at, double sign, double best)
            {
                double lo = at - 0.5, hi = at + 0.5;
                const double g = 0.6180339887498949;
                double G(double x) => f(x) is double v && !double.IsNaN(v) ? sign * v : double.MinValue;
                double x1 = hi - g * (hi - lo), x2 = lo + g * (hi - lo);
                double f1 = G(x1), f2 = G(x2);
                for (var k = 0; k < 60; k++)
                {
                    if (f1 >= f2) { hi = x2; x2 = x1; f2 = f1; x1 = hi - g * (hi - lo); f1 = G(x1); }
                    else { lo = x1; x1 = x2; f1 = f2; x2 = lo + g * (hi - lo); f2 = G(x2); }
                }
                var m = Math.Max(f1, f2);
                return m == double.MinValue ? best : sign > 0 ? Math.Max(best, m) : Math.Min(best, -m);
            }
            return (Refine(atMin, -1, bestMin), Refine(atMax, 1, bestMax));
        }

        // ------------------------------------------------------------------ Geneva drive and pantograph

        /// <summary>
        /// Geneva drive with <paramref name="slots"/> slots, centre distance C (driver at (0,0), wheel at (C,0)) and driver
        /// angle φ (degrees): crank radius a = C·sin(180°/z), and the wheel orientation in degrees (0 = a slot points at the
        /// driver). While the pin is in a slot (|φ| ≤ 90° − 180°/z) the slot follows the pin; otherwise the wheel is locked at
        /// 180°/z (the same picture as −180°/z, the wheel repeats every 360°/z).
        /// </summary>
        public static double GenevaWheelAngle(int slots, double centre, double angleDeg)
        {
            var z = Math.Max(3, slots);
            var a = centre * Math.Sin(Math.PI / z);
            var limit = 90 - 180.0 / z;
            var phi = Geo.NormDeg(angleDeg);
            if (phi > 180) phi -= 360;
            if (Math.Abs(phi) > limit) return 180.0 / z;
            var p = Dir(phi) * a;
            var s = Geo.RadToDeg(Math.Atan2(p.Y, p.X - centre)) - 180;
            while (s <= -180) s += 360;
            while (s > 180) s -= 360;
            return s;
        }

        /// <summary>
        /// Pantograph (scale k ≥ 1) with the fixed pivot O = (0,0), long bars O–B and B–P of length l, the short bars C–T and
        /// T–D of length l/k (C on O–B, D on B–P): for the tracer point T it returns C, B, D and the pen point P = k·T
        /// (O, T and P on one line). The knee C is on the side <paramref name="side"/> of O→T. Null if |OT| &gt; 2l/k.
        /// </summary>
        public static (Vec2 C, Vec2 B, Vec2 D, Vec2 P)? Pantograph(double length, double scale, Vec2 tracer, int side = -1)
        {
            var k = Math.Max(1.0001, scale);
            var s = length / k;
            var c = CircleCircle(Vec2.Zero, s, tracer, s, side);
            if (c is null) return null;
            var b = c.Value * k;
            var p = tracer * k;
            return (c.Value, b, tracer + c.Value * (k - 1), p);
        }

        // ------------------------------------------------------------------ instant centres and derivatives [S24]

        /// <summary>Intersection of the lines a0–a1 and b0–b1; null if they are (nearly) parallel or degenerate.</summary>
        public static Vec2? LineIntersection(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1)
        {
            var d1 = a1 - a0; var d2 = b1 - b0;
            var den = d1.Cross(d2);
            if (d1.Length < 1e-12 || d2.Length < 1e-12 || Math.Abs(den) < 1e-9 * d1.Length * d2.Length) return null;
            var t = (b0 - a0).Cross(d2) / den;
            return a0 + d1 * t;
        }

        /// <summary>
        /// Instant centre of the coupler A–B relative to the frame (Kennedy: on the line O2–A of the crank and on the line
        /// O4–B of the rocker); null when crank and rocker are parallel (the coupler translates for an instant).
        /// </summary>
        public static Vec2? CouplerPole(FourBarPose p) => LineIntersection(p.O2, p.A, p.O4, p.B);

        /// <summary>
        /// Instant centre of the connecting rod A–B of a slider-crank relative to the frame: on the line O–A of the crank and
        /// on the normal to the (horizontal) guide through the slider joint B; null when the crank is vertical.
        /// </summary>
        public static Vec2? RodPole(SliderCrankPose p) => LineIntersection(p.O, p.A, p.B, p.B + new Vec2(0, 1));

        /// <summary>Step (radians) of the central differences of the motion derivatives.</summary>
        public const double DerivativeStep = 1e-3;

        /// <summary>
        /// First and second derivative of a position function of the input angle by central differences (per radian, the
        /// angle in degrees); null where the function is undefined at one of the three samples.
        /// </summary>
        public static (double D1, double D2)? Derivatives(Func<double, double?> f, double angleDeg)
        {
            var h = DerivativeStep;
            var hd = Geo.RadToDeg(h);
            if (f(angleDeg) is not double y0 || f(angleDeg + hd) is not double yp || f(angleDeg - hd) is not double ym) return null;
            if (double.IsNaN(y0) || double.IsNaN(yp) || double.IsNaN(ym)) return null;
            return ((yp - ym) / (2 * h), (yp - 2 * y0 + ym) / (h * h));
        }

        /// <summary>
        /// First and second derivative (per radian of the input angle) of a point of the four-bar's coupler given in the
        /// coupler frame: P = A + u·(B − A) − v·(B − A)⊥ (as drawn: u along the coupler, v across it). Null where a sample
        /// cannot be assembled.
        /// </summary>
        public static (Vec2 D1, Vec2 D2)? CouplerPointDerivatives(double ground, double crank, double coupler, double rocker, double angleDeg, bool crossed, double u, double v)
        {
            Vec2? P(double a)
            {
                var p = FourBar(ground, crank, coupler, rocker, a, crossed);
                if (p is null) return null;
                var ab = p.Value.B - p.Value.A;
                return p.Value.A + ab * u - ab.Perp * v;
            }
            var dx = Derivatives(a => P(a)?.X, angleDeg);
            var dy = Derivatives(a => P(a)?.Y, angleDeg);
            if (dx is null || dy is null) return null;
            return (new Vec2(dx.Value.D1, dy.Value.D1), new Vec2(dx.Value.D2, dy.Value.D2));
        }

        /// <summary>
        /// dθ₃/dφ of the four-bar's coupler (its angular velocity per unit crank angular velocity, same sense positive): from
        /// the coupler direction A → B at neighbouring input angles. NaN where it cannot be assembled.
        /// </summary>
        public static double CouplerRate(double ground, double crank, double coupler, double rocker, double angleDeg, bool crossed)
        {
            var h = Geo.RadToDeg(DerivativeStep);
            double? T(double a) => FourBar(ground, crank, coupler, rocker, a, crossed) is { } p ? Math.Atan2(p.B.Y - p.A.Y, p.B.X - p.A.X) : null;
            if (T(angleDeg + h) is not double tp || T(angleDeg - h) is not double tm) return double.NaN;
            var d = tp - tm;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d <= -Math.PI) d += 2 * Math.PI;
            return d / (2 * DerivativeStep);
        }

        /// <summary>
        /// First and second derivative (per radian of the input angle) of the coupler angle θ₃ (direction A → B) or, with
        /// <paramref name="rocker"/>, of the rocker angle θ₄ (direction O4 → B), same sense as the input positive; at constant
        /// input speed n the angular acceleration is n²·d²θ/dφ². Null where a sample cannot be assembled.
        /// </summary>
        public static (double D1, double D2)? LinkAngleDerivatives(double ground, double crank, double coupler, double rocker, double angleDeg, bool crossed, bool ofRocker)
        {
            double? Theta(double a)
            {
                if (FourBar(ground, crank, coupler, rocker, a, crossed) is not { } p) return null;
                var from = ofRocker ? p.O4 : p.A;
                return Math.Atan2(p.B.Y - from.Y, p.B.X - from.X);
            }
            if (Theta(angleDeg) is not double t0) return null;
            // Unwrapped about the angle at the position itself (the samples lie within a few milliradians of it).
            double? Unwrapped(double a)
            {
                if (Theta(a) is not double t) return null;
                var d = t - t0;
                while (d > Math.PI) d -= 2 * Math.PI;
                while (d <= -Math.PI) d += 2 * Math.PI;
                return t0 + d;
            }
            return Derivatives(Unwrapped, angleDeg);
        }

        /// <summary>d²x_B/dφ² of the slider-crank (mm per radian²); NaN where it cannot be assembled.</summary>
        public static double SliderAccelerationFactor(double crank, double rod, double offset, double angleDeg)
        {
            double? X(double a) => SliderCrank(crank, rod, offset, a)?.B.X;
            return Derivatives(X, angleDeg)?.D2 ?? double.NaN;
        }

        /// <summary>Largest |dx_B/dφ| of the slider-crank over a turn (mm per radian); null if no position can be assembled.</summary>
        public static double? SliderMaxSpeedFactor(double crank, double rod, double offset)
            => Extremes(a => SliderSpeedFactor(crank, rod, offset, a) is var v && !double.IsNaN(v) ? Math.Abs(v) : null)?.Max;
    }

    /// <summary>Grashof class of a crank-driven four-bar (frame d, crank a, coupler b, rocker c).</summary>
    public enum GrashofClass
    {
        /// <summary>s + l &lt; p + q, crank shortest: the crank turns, the rocker oscillates.</summary>
        CrankRocker,
        /// <summary>s + l &lt; p + q, frame shortest: crank and rocker both turn (drag link).</summary>
        DoubleCrank,
        /// <summary>s + l &lt; p + q, rocker shortest: the input only swings, the output turns.</summary>
        RockerCrank,
        /// <summary>s + l &lt; p + q, coupler shortest: both side links swing, the coupler turns.</summary>
        GrashofDoubleRocker,
        /// <summary>s + l = p + q: the links can line up (change point).</summary>
        ChangePoint,
        /// <summary>s + l &gt; p + q: no link turns fully.</summary>
        TripleRocker,
    }
}
