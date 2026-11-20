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
    }
}
