using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Charts;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class CurveFunctionsTests
    {
        private static void Near(double expected, double actual, double tol = 1e-9) =>
            Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected}, got {actual}");

        [Fact]
        public void EveryKindHasBilingualNamesAndValidDefaults()
        {
            Assert.Equal(Enum.GetValues(typeof(CurveKind)).Length, CurveFunctions.Kinds.Count);
            foreach (var info in CurveFunctions.Kinds)
            {
                Assert.Equal(info, CurveFunctions.Info(info.Kind));
                Assert.False(string.IsNullOrEmpty(info.Name.De));
                Assert.NotEmpty(info.Params);
                foreach (var p in info.Params)
                {
                    Assert.True(p.IsValid(p.Default), info.Id + "." + p.Name);
                    Assert.False(string.IsNullOrEmpty(p.Label.De));
                    var def = p.ToParamDef();
                    Assert.Equal(p.Name, def.Key);
                    Assert.Equal(p.Default, def.Default!.Value.Number);
                }
                var spec = CurveFunctions.Default(info.Kind);
                Assert.Equal(spec, CurveFunctions.Parse(spec.Format()));
            }
        }

        [Fact]
        public void PiecewiseKindsSampleExactCorners()
        {
            var step = CurveFunctions.Parse("step(x0=2, y0=1, y1=5)");
            Assert.Equal(new[] { new Vec2(0, 1), new Vec2(2, 1), new Vec2(2, 5), new Vec2(10, 5) }, step.Sample(0, 10));
            Near(1, step.Evaluate(1.999));
            Near(5, step.Evaluate(2));

            var pulse = CurveFunctions.Parse("pulse(start=3, width=2, low=0, high=4)");
            Assert.Equal(new[] { new Vec2(0, 0), new Vec2(3, 0), new Vec2(3, 4), new Vec2(5, 4), new Vec2(5, 0), new Vec2(10, 0) }, pulse.Sample(0, 10));

            // Pulse train: period 0.1, duty 0.3 – edges at multiples of 0.1 must not get lost through rounding.
            var train = CurveFunctions.Parse("pulsetrain(period=0.1, duty=0.3, low=1, high=2)");
            var pts = train.Sample(0, 1);
            Assert.Equal(2, pts[0].Y);
            var rising = pts.Where((p, i) => i > 0 && pts[i - 1].X == p.X && p.Y > pts[i - 1].Y).Select(p => p.X).ToList();
            var falling = pts.Where((p, i) => i > 0 && pts[i - 1].X == p.X && p.Y < pts[i - 1].Y).Select(p => p.X).ToList();
            Assert.Equal(9, rising.Count);
            Assert.Equal(10, falling.Count);
            for (var k = 0; k < 9; k++) Near(0.1 * (k + 1), rising[k], 1e-12);
            for (var k = 0; k < 10; k++) Near(0.1 * k + 0.03, falling[k], 1e-12);

            var saw = CurveFunctions.Parse("sawtooth(period=2, low=1, high=3, phase=0.5)");
            var s = saw.Sample(0, 5);
            Assert.Equal(new Vec2(0, 2.5), s[0]);
            Assert.Equal(new[] { new Vec2(0.5, 3), new Vec2(0.5, 1), new Vec2(2.5, 3), new Vec2(2.5, 1), new Vec2(4.5, 3), new Vec2(4.5, 1) }, s.Skip(1).Take(6));
            Assert.Equal(new Vec2(5, 1.5), s[s.Count - 1]);
            // A range ending exactly on a tooth boundary ends at the top of the last tooth (left limit), not flat.
            var full = CurveFunctions.Parse("sawtooth(period=30, low=15, high=80, phase=5)").Sample(5, 95);
            Assert.Equal(new[] { new Vec2(5, 15), new Vec2(35, 80), new Vec2(35, 15), new Vec2(65, 80), new Vec2(65, 15), new Vec2(95, 80) }, full);

            var tri = CurveFunctions.Parse("triangle(period=4, low=-1, high=1)");
            Assert.Equal(new[] { new Vec2(0, -1), new Vec2(2, 1), new Vec2(4, -1), new Vec2(6, 1), new Vec2(7, 0) }, tri.Sample(0, 7));
            Near(0, tri.Evaluate(1));

            Assert.Equal(new[] { new Vec2(-1, 3), new Vec2(1, 3) }, CurveFunctions.Parse("constant(y=3)").Sample(-1, 1));
            Assert.Equal(new[] { new Vec2(0, 1), new Vec2(4, 9) }, CurveFunctions.Parse("ramp(x0=1, y0=3, slope=2)").Sample(0, 4));
        }

        [Fact]
        public void SinePeriodicityAndDensity()
        {
            var sine = CurveFunctions.Parse("sine(amplitude=2, period=5, phase=1, offset=3)");
            Near(3, sine.Evaluate(1));
            Near(5, sine.Evaluate(2.25), 1e-12);
            Near(1, sine.Evaluate(4.75), 1e-12);
            for (var x = -7.3; x < 9; x += 0.71) Near(sine.Evaluate(x), sine.Evaluate(x + 5), 1e-9);
            // 20 periods: at least SegmentsPerPeriod segments each.
            var pts = sine.Sample(0, 100);
            Assert.True(pts.Count >= 20 * CurveFunctions.SegmentsPerPeriod + 1);
            Assert.Equal(0, pts[0].X);
            Assert.Equal(100, pts[pts.Count - 1].X);
            Assert.All(pts, p => Near(sine.Evaluate(p.X), p.Y, 1e-12));
        }

        [Fact]
        public void ExponentialsApproachTheirAsymptote()
        {
            var pt1 = CurveFunctions.Parse("pt1(y0=20, yinf=160, tau=12, t0=5)");
            Near(20, pt1.Evaluate(0));
            Near(20, pt1.Evaluate(5));
            Near(160 - 140 / Math.E, pt1.Evaluate(17), 1e-9);
            Near(160, pt1.Evaluate(5 + 12 * 30), 1e-6);
            var pts = pt1.Sample(0, 100);
            Assert.Contains(new Vec2(5, 20), pts);           // exact corner at t0
            Assert.True(pts.Zip(pts.Skip(1), (a, b) => b.Y >= a.Y - 1e-12).All(x => x)); // monotonic
            Assert.All(pts, p => Assert.True(p.Y <= 160));

            var decay = CurveFunctions.Parse("decay(y0=80, tau=10)");
            Near(80, decay.Evaluate(0));
            Near(80 / Math.E, decay.Evaluate(10), 1e-9);
            Near(0, decay.Evaluate(1000), 1e-9);

            // PT2: underdamped overshoots, critically and overdamped do not; all settle at yinf.
            var under = CurveFunctions.Parse("pt2(y0=0, yinf=1, zeta=0.2, omega=2)");
            var overshoot = under.Sample(0, 20).Max(p => p.Y);
            Near(1 + Math.Exp(-Math.PI * 0.2 / Math.Sqrt(1 - 0.04)), overshoot, 2e-3);
            Near(1, under.Evaluate(200), 1e-6);
            Near(0, under.Evaluate(0));
            foreach (var zeta in new[] { 1.0, 1.00001, 2.5 })
            {
                var s = CurveFunctions.Create(CurveKind.Pt2, 0, 1, zeta, 2);
                var p = s.Sample(0, 30);
                Assert.True(p.Max(q => q.Y) <= 1 + 1e-9);
                Near(1, s.Evaluate(60), 1e-4);
            }
            // The critical formula is continuous with its neighbours.
            Near(CurveFunctions.Create(CurveKind.Pt2, 0, 1, 1.0, 2).Evaluate(1.3), CurveFunctions.Create(CurveKind.Pt2, 0, 1, 1.001, 2).Evaluate(1.3), 5e-3);
            Near(CurveFunctions.Create(CurveKind.Pt2, 0, 1, 1.0, 2).Evaluate(1.3), CurveFunctions.Create(CurveKind.Pt2, 0, 1, 0.999, 2).Evaluate(1.3), 5e-3);
        }

        [Fact]
        public void GaussAndEllipse()
        {
            var g = CurveFunctions.Parse("gauss(centre=50, width=0.5, height=3, offset=1)");
            Near(4, g.Evaluate(50));
            Near(1 + 3 * Math.Exp(-0.5), g.Evaluate(50.5), 1e-12);
            // A narrow bump in a wide range is still resolved.
            var pts = g.Sample(0, 1000);
            Near(4, pts.Max(p => p.Y), 0.02);

            var e = CurveFunctions.Parse("ellipse(cx=1, cy=2, rx=3, ry=1)");
            Assert.True(e.Info.Closed);
            Assert.True(double.IsNaN(e.Evaluate(1)));
            var ring = e.Sample(0, 0);
            Assert.Equal(CurveFunctions.EllipseSegments + 1, ring.Count);
            Assert.Equal(ring[0], ring[ring.Count - 1]);
            Assert.All(ring, p => Near(1, Math.Pow((p.X - 1) / 3, 2) + Math.Pow(p.Y - 2, 2), 1e-9));

            var circle = CurveFunctions.Parse("circle(cx=0, cy=0, r=2)");
            Assert.Equal(CurveKind.Ellipse, circle.Kind);
            Assert.Equal(2, circle["rx"]);
            Assert.Equal(2, circle["ry"]);
            Assert.Equal(circle, CurveFunctions.Parse("circle(0, 0, 2)"));
        }

        [Theory]
        [InlineData("pt1(y0=20, yinf=160, tau=12)")]
        [InlineData("sine(amplitude=0.1, period=0.3, phase=1.0000000000000002, offset=-7)")]
        [InlineData("step(x0=-1E-07, y0=1234567.891, y1=0)")]
        [InlineData("pulsetrain(start=0.25, period=2, duty=0.125, low=0, high=1)")]
        [InlineData("gauss(centre=3, width=0.2, height=-1)")]
        [InlineData("ellipse(cx=0, cy=0, rx=1, ry=0.5)")]
        public void FormatParseRoundTrip(string text)
        {
            var spec = CurveFunctions.Parse(text);
            var again = CurveFunctions.Parse(spec.Format());
            Assert.Equal(spec, again);
            Assert.Equal(spec.Format(), again.Format());
        }

        [Fact]
        public void FormatIsCompactAndInvariant()
        {
            var spec = CurveFunctions.Create(CurveKind.Pt1, 20, 160, 12);
            Assert.Equal("pt1(y0=20, yinf=160, tau=12)", spec.Format());
            Assert.Equal("pt1(y0=20, yinf=160, tau=12, t0=2.5)", spec.With("t0", 2.5).Format());
            var prev = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.Equal("sine(amplitude=1.5, period=6.283185307179586)", CurveFunctions.Create(CurveKind.Sine, 1.5).Format());
                Assert.Equal(1.5, CurveFunctions.Parse("sine(amplitude=1.5)")["amplitude"]);
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = prev; }
        }

        [Fact]
        public void ParseIsLenient()
        {
            Assert.Equal(CurveFunctions.Create(CurveKind.Sine, 2, 10), CurveFunctions.Parse("  SINE( 2 , 10 ) "));
            Assert.Equal(CurveFunctions.Create(CurveKind.Sine, 2, 10, 0, 1), CurveFunctions.Parse("sine(2, offset=1, Period=10)"));
            Assert.Equal(CurveFunctions.Default(CurveKind.Triangle), CurveFunctions.Parse("triangle"));
            Assert.Equal(CurveFunctions.Default(CurveKind.Triangle), CurveFunctions.Parse("triangle()"));
            Assert.Equal(CurveKind.Ramp, CurveFunctions.Parse("line(y0=1, slope=2)").Kind);
            Assert.Equal(-3, CurveFunctions.Parse("constant(y=−3)")["y"]);
            Assert.Equal(7, CurveFunctions.Parse("gauss(center=7)")["centre"]);
            // German decimal comma: between digits inside a named value, or everything with semicolons.
            var de = CurveFunctions.Parse("pt1(y0=20,5, yinf=160, tau=1,25)");
            Assert.Equal(20.5, de["y0"]);
            Assert.Equal(1.25, de["tau"]);
            var semi = CurveFunctions.Parse("sine(2,5; 10; 0; 1,5)");
            Assert.Equal(CurveFunctions.Create(CurveKind.Sine, 2.5, 10, 0, 1.5), semi);
            // Without names commas separate values.
            Assert.Equal(CurveFunctions.Create(CurveKind.Sine, 2, 5), CurveFunctions.Parse("sine(2,5)"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("wobble(a=1)")]
        [InlineData("sine(amplitude=1")]
        [InlineData("sine amplitude=1)")]
        [InlineData("sine(amplitude=1) x")]
        [InlineData("sine(foo=1)")]
        [InlineData("sine(amplitude=x)")]
        [InlineData("sine(amplitude=1, amplitude=2)")]
        [InlineData("sine(period=0)")]
        [InlineData("pt1(tau=-1)")]
        [InlineData("pulsetrain(duty=1.5)")]
        [InlineData("sine(1, 2, 3, 4, 5)")]
        [InlineData("sine(amplitude=1, 2)")]
        [InlineData("sine(1,,2)")]
        [InlineData("circle(cx=0, cy=0, r=1, rx=2)")]
        [InlineData("constant(y=NaN)")]
        public void ParseErrorsAreBilingualFormatExceptions(string text)
        {
            var ex = Assert.ThrowsAny<FormatException>(() => CurveFunctions.Parse(text));
            var bilingual = Assert.IsType<CurveSpecFormatException>(ex);
            Assert.False(string.IsNullOrEmpty(bilingual.Text.De));
            Assert.NotEqual(bilingual.Text.En, bilingual.Text.De);
            Assert.Equal(bilingual.Text.En, ex.Message);
            Assert.False(CurveFunctions.TryParse(text, out var spec, out var error));
            Assert.Null(spec);
            Assert.NotNull(error);
        }

        [Fact]
        public void CreateValidates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CurveFunctions.Create(CurveKind.Sine, 1, -2));
            Assert.Throws<ArgumentException>(() => CurveFunctions.Create(CurveKind.Constant, 1, 2));
            Assert.Throws<ArgumentException>(() => CurveFunctions.Default(CurveKind.Sine).With("nope", 1));
            var named = CurveFunctions.Create(CurveKind.Pulse, new Dictionary<string, double> { ["width"] = 3, ["high"] = 2 });
            Assert.Equal("pulse(start=0, width=3, low=0, high=2)", named.Format());
        }

        [Fact]
        public void SamplingIsBoundedAndHandlesDegenerateRanges()
        {
            var many = CurveFunctions.Parse("pulsetrain(period=1e-6)");
            Assert.True(many.Sample(0, 1).Count <= CurveFunctions.MaxPoints);
            Assert.True(CurveFunctions.Parse("sawtooth(period=1e-6)").Sample(0, 1).Count <= CurveFunctions.MaxPoints);
            Assert.True(CurveFunctions.Parse("sine(period=1e-6)").Sample(0, 1).Count <= CurveFunctions.MaxPoints);
            Assert.Single(CurveFunctions.Parse("sine").Sample(1, 1));
            Assert.Single(CurveFunctions.Parse("sine").Sample(2, 1));
        }

        [Fact]
        public void AffineMapKeepsTheCurve()
        {
            foreach (var info in CurveFunctions.Kinds.Where(k => k.Kind != CurveKind.Decay && !k.Closed))
            {
                var spec = CurveFunctions.Default(info.Kind);
                var mapped = CurveFunctions.MapAffine(spec, 100, 0.5, -20, 3);
                for (var x = -3.0; x <= 3; x += 0.37)
                    Near(-20 + 3 * spec.Evaluate(x), mapped.Evaluate(100 + 0.5 * x), 1e-9);
            }
        }
    }
}
