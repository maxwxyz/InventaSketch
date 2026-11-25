using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Charts;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class AxisBreaksTests
    {
        private static void Near(double expected, double actual, double tol = 1e-9) =>
            Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected}, got {actual}");

        [Fact]
        public void PositionsMatchTheSlidemaxScale()
        {
            // 0 … 100 on 100 mm, values 20 … 80 compressed into 10 mm: 90 mm for 40 units outside.
            var axis = new BrokenAxis(0, 100, 100, new[] { new AxisBreak(20, 80, 10) });
            Near(2.25, axis.UnitLength);
            Near(0, axis.Position(0));
            Near(45, axis.Position(20));
            Near(50, axis.Position(50));   // inside the break: linear in the gap
            Near(55, axis.Position(80));
            Near(100, axis.Position(100));
            Near(-4.5, axis.Position(-2)); // extrapolated
            Near(104.5, axis.Position(102));
            Assert.Equal(-1, axis.BreakIndexOf(20));
            Assert.Equal(0, axis.BreakIndexOf(50));
            Assert.False(axis.IsVisible(79.9));
            Assert.Equal(new[] { (0.0, 20.0), (80.0, 100.0) }, axis.VisibleRanges);
            Assert.Equal((45.0, 55.0), axis.GapPositions(0));
        }

        [Fact]
        public void MappingIsMonotonicAndInvertible()
        {
            var rnd = new Random(7);
            for (var trial = 0; trial < 50; trial++)
            {
                double min = rnd.NextDouble() * 200 - 100, max = min + 1 + rnd.NextDouble() * 1000, length = 20 + rnd.NextDouble() * 200;
                var breaks = new List<AxisBreak>();
                for (var i = rnd.Next(0, 4); i > 0; i--)
                {
                    var a = min + rnd.NextDouble() * (max - min);
                    var b = a + rnd.NextDouble() * (max - a) * 0.5;
                    breaks.Add(new AxisBreak(a, b, rnd.NextDouble() * 5));
                }
                var axis = new BrokenAxis(min, max, length, breaks);
                Near(0, axis.Position(min), 1e-9 * length);
                Near(length, axis.Position(max), 1e-7 * length);
                var prev = double.NegativeInfinity;
                for (var k = -10; k <= 1010; k++)
                {
                    var v = min + (max - min) * k / 1000.0;
                    var p = axis.Position(v);
                    Assert.True(p >= prev - 1e-9, "monotonic");
                    prev = p;
                    // Inverse holds for every value that is not inside a zero-length gap.
                    var i = axis.BreakIndexOf(v);
                    if (i < 0 || axis.Breaks[i].Gap > 1e-9) Near(v, axis.Value(p), 1e-7 * (max - min));
                }
                for (var k = 0; k <= 200; k++)
                {
                    var p = length * k / 200.0;
                    Near(p, axis.Position(axis.Value(p)), 1e-7 * length);
                }
            }
        }

        [Fact]
        public void BreaksAreNormalised()
        {
            var axis = new BrokenAxis(0, 100, 50, new[]
            {
                new AxisBreak(90, 70, 2),     // reversed limits
                new AxisBreak(60, 75, 3),     // overlaps → merged, larger gap
                new AxisBreak(-20, 10, 1),    // clipped at the start
                new AxisBreak(40, 40, 1),     // empty → dropped
                new AxisBreak(120, 130, 1),   // outside → dropped
            });
            Assert.Equal(new[] { new AxisBreak(0, 10, 1), new AxisBreak(60, 90, 3) }, axis.Breaks);
            Near(0, axis.Position(0));
            Near(1, axis.Position(10));
            Assert.Throws<ArgumentException>(() => new BrokenAxis(5, 5, 10));
            Assert.Throws<ArgumentException>(() => new BrokenAxis(0, 5, 0));
        }

        [Fact]
        public void ValuesInsideAZeroGapCollapse()
        {
            var axis = new BrokenAxis(0, 10, 10, new[] { new AxisBreak(4, 6, 0) });
            Near(axis.Position(4), axis.Position(5));
            Near(axis.Position(4), axis.Position(6));
            Near(4, axis.Value(axis.Position(5)));
            Near(7, axis.Value(axis.Position(7)), 1e-12);
        }

        [Fact]
        public void PageMappingOnAnyAxisDirection()
        {
            var axis = new BrokenAxis(0, 100, 80, new[] { new AxisBreak(30, 70, 8) });
            // Vertical value axis, y up on the page: Min at the bottom.
            Vec2 start = new(10, 90), end = new(10, 10);
            var p = axis.Point(80, start, end);
            Near(10, p.X);
            Near(90 - axis.Position(80), p.Y);
            Near(80, axis.ValueAt(p + new Vec2(3, 0), start, end), 1e-9);
            // Reversed x axis: Min on the right.
            var q = axis.Point(20, new Vec2(100, 0), new Vec2(20, 0));
            Near(100 - axis.Position(20), q.X);
            Near(20, axis.ValueAt(q, new Vec2(100, 0), new Vec2(20, 0)), 1e-9);
        }

        [Fact]
        public void PlotMapsBothAxesAndCutsCurvesAtBreaks()
        {
            var plot = new BrokenPlot(new Rect(10, 20, 100, 60), 0, 100, new[] { new AxisBreak(40, 60, 5) }, 0, 100, new[] { new AxisBreak(20, 80, 6) });
            Near(80, plot.ToPage(new Vec2(0, 0)).Y);
            Near(20, plot.ToPage(new Vec2(0, 100)).Y);
            Near(110, plot.ToPage(new Vec2(100, 0)).X);
            var rnd = new Random(3);
            for (var i = 0; i < 100; i++)
            {
                var d = new Vec2(rnd.NextDouble() * 100, rnd.NextDouble() * 100);
                var back = plot.ToData(plot.ToPage(d));
                Near(d.X, back.X, 1e-9);
                Near(d.Y, back.Y, 1e-9);
            }

            // Diagonal through both breaks: visible at x,y ∈ [0, 20] and [80, 100] (x 40 … 60 lies inside the y break anyway).
            var runs = plot.VisibleRuns(new[] { new Vec2(0, 0), new Vec2(100, 100) });
            Assert.Equal(2, runs.Count);
            Assert.Equal(new[] { new Vec2(0, 0), new Vec2(20, 20) }, runs[0]);
            Assert.Equal(new[] { new Vec2(80, 80), new Vec2(100, 100) }, runs[1]);

            // Flat line at y = 10 crossing the x break only.
            var flat = plot.VisibleRuns(new[] { new Vec2(0, 10), new Vec2(50, 10), new Vec2(100, 10) });
            Assert.Equal(2, flat.Count);
            Assert.Equal(new Vec2(40, 10), flat[0][flat[0].Count - 1]);
            Assert.Equal(new Vec2(60, 10), flat[1][0]);
            Assert.All(flat.SelectMany(r => r), v => Assert.True(plot.IsVisible(v)));

            // A curve completely inside a break disappears; a single visible point stays.
            Assert.Empty(plot.VisibleRuns(new[] { new Vec2(10, 30), new Vec2(20, 70) }));
            Assert.Single(plot.VisibleRuns(new[] { new Vec2(10, 10) }));
        }

        [Theory]
        [InlineData(BreakMarkerStyle.Zigzag)]
        [InlineData(BreakMarkerStyle.Slash)]
        public void MarkersLieInsideTheAxisArea(BreakMarkerStyle style)
        {
            var rect = new Rect(10, 20, 100, 60);
            var yGap = AxisBreaks.RequiredGap(style, rect.W);
            var xGap = AxisBreaks.RequiredGap(style, rect.H);
            var plot = new BrokenPlot(rect, 0, 100, new[] { new AxisBreak(40, 60, xGap) }, 0, 100, new[] { new AxisBreak(20, 80, yGap) });
            var across = plot.Markers(style, acrossPlot: true);
            Assert.Equal(2, across.Count);
            var (y0, y1) = plot.Y.GapPositions(0);
            var (x0, x1) = plot.X.GapPositions(0);
            foreach (var p in across[0].AllPoints)
            {
                Assert.True(p.X >= rect.X - 1e-9 && p.X <= rect.Right + 1e-9, "y marker within the plot width");
                var along = rect.Bottom - p.Y;
                Assert.True(along >= y0 + AxisBreaks.BreakMargin - 1e-9 && along <= y1 - AxisBreaks.BreakMargin + 1e-9, $"y marker within the gap: {along} not in {y0} … {y1}");
            }
            foreach (var p in across[1].AllPoints)
            {
                Assert.True(p.Y >= rect.Y - 1e-9 && p.Y <= rect.Bottom + 1e-9, "x marker within the plot height");
                var along = p.X - rect.X;
                Assert.True(along >= x0 + AxisBreaks.BreakMargin - 1e-9 && along <= x1 - AxisBreaks.BreakMargin + 1e-9, "x marker within the gap");
            }
            // Short markers sit on the axis lines within the gap.
            var onAxis = plot.Markers(style, acrossPlot: false);
            foreach (var p in onAxis[0].AllPoints)
            {
                Assert.True(Math.Abs(p.X - rect.X) <= AxisBreaks.AxisMarkerWidth / 2 + 1e-9);
                var along = rect.Bottom - p.Y;
                Assert.True(along >= y0 - 1e-9 && along <= y1 + 1e-9);
            }
            foreach (var p in onAxis[1].AllPoints) Assert.True(Math.Abs(p.Y - rect.Bottom) <= AxisBreaks.AxisMarkerWidth / 2 + 1e-9);
            // The band is closed polygon of both lines.
            Assert.Equal(across[0].Line1.Count + across[0].Line2.Count, across[0].Band.Count);
        }

        [Fact]
        public void ZigzagAndSlashGeometry()
        {
            var z = AxisBreaks.ZigzagPoints(new Vec2(0, 10), new Vec2(20, 10));
            var count = (int)Math.Floor(20 / AxisBreaks.WiggleStep);
            Assert.Equal(count + 1, z.Count);
            Assert.All(z, p => Assert.InRange(p.X, 0, 20));
            for (var k = 0; k < z.Count; k++) Near(10 + (k % 2 == 0 ? 1 : -1) * AxisBreaks.WiggleAmplitude / 2, z[k].Y, 1e-12);
            Near(20 - z[z.Count - 1].X, z[0].X, 1e-9); // centred
            // Short lines shrink the teeth instead of overshooting.
            var shortZ = AxisBreaks.ZigzagPoints(new Vec2(0, 0), new Vec2(0, 1));
            Assert.Equal(3, shortZ.Count);
            Assert.All(shortZ, p => Assert.InRange(p.Y, 0, 1));

            var slash = AxisBreaks.Marker(BreakMarkerStyle.Slash, new Vec2(0, 50), new Vec2(10, 50));
            // Rises to the right on the page (y down) by tan 15° × length; lines 4.5 pt apart.
            Near(Math.Tan(15 * Math.PI / 180) * 10, slash.Line1[0].Y - slash.Line1[1].Y, 1e-9);
            Near(AxisBreaks.BreakThickness, slash.Line2[0].Y - slash.Line1[0].Y, 1e-9);
            Near(AxisBreaks.BreakThickness + Math.Tan(15 * Math.PI / 180) * 10 + 2 * AxisBreaks.BreakMargin, AxisBreaks.RequiredGap(BreakMarkerStyle.Slash, 10), 1e-12);
            // 10.25 pt for the zigzag (Slidemax WiggleBreakGap).
            Near(10.25 * 25.4 / 72, AxisBreaks.RequiredGap(BreakMarkerStyle.Zigzag, 999), 1e-12);
            Assert.Equal("Zickzack", AxisBreaks.Name(BreakMarkerStyle.Zigzag).De);
        }

        [Fact]
        public void SpansPlaceBreaksInDataFreeGaps()
        {
            var band = AxisBreaks.RequiredGap(BreakMarkerStyle.Zigzag, 0);
            var keep = new[] { 0.0, 12, 90, 100 };
            var spans = AxisBreaks.Spans(0, 100, 100, keep, new[] { new AxisBreakRequest(50) });
            var s = Assert.Single(spans);
            Assert.Equal(0, s.Index);
            Near(12, s.Lo, 1e-9);
            Near(90, s.Hi, 1e-9);
            Near(band, s.Gap);

            // With an extent the whole free region 12 … 90 takes exactly that length, the band sits in its middle.
            var tall = Assert.Single(AxisBreaks.Spans(0, 100, 100, keep, new[] { new AxisBreakRequest(50, 20) }));
            var axis = new BrokenAxis(0, 100, 100, new[] { tall.ToBreak() });
            Near(20, axis.Position(90) - axis.Position(12), 1e-9);
            Near(tall.Lo - 12, 90 - tall.Hi, 1e-9);
            Near(100, axis.Position(100), 1e-9);

            // Hardly any free space: the break would gain nothing and is dropped.
            Assert.Empty(AxisBreaks.Spans(0, 100, 100, new[] { 49.0, 51 }, new[] { new AxisBreakRequest(50) }));
            // Too large an extent: compressed to the band (shrink to fit) or dropped.
            Assert.Single(AxisBreaks.Spans(0, 100, 100, keep, new[] { new AxisBreakRequest(50, 90) }));
            Assert.Empty(AxisBreaks.Spans(0, 100, 100, keep, new[] { new AxisBreakRequest(50, 90) }, shrinkToFit: false));
            // Two requests in the same gap count once; a request above all data has no gap.
            var two = AxisBreaks.Spans(0, 100, 100, keep, new[] { new AxisBreakRequest(30), new AxisBreakRequest(60), new AxisBreakRequest(100) });
            Assert.Equal(0, Assert.Single(two).Index);
            // A slash band grows with the marker length across the axis.
            var slash = Assert.Single(AxisBreaks.Spans(0, 100, 100, keep, new[] { new AxisBreakRequest(50, null, BreakMarkerStyle.Slash) }, crossLength: 20));
            Near(AxisBreaks.RequiredGap(BreakMarkerStyle.Slash, 20), slash.Gap);
        }

        [Fact]
        public void SpansWorkForContinuousXYData()
        {
            // A PT1 curve sampled on an XY chart: values cluster near 20 and near 160; break the y axis in the empty middle.
            var curve = CurveFunctions.Parse("pt1(y0=150, yinf=160, tau=5)").Sample(0, 50).Concat(new[] { new Vec2(0, 20), new Vec2(5, 25) }).ToList();
            var spans = AxisBreaks.Spans(0, 170, 60, curve.Select(p => p.Y), new[] { new AxisBreakRequest(80, 6) });
            var s = Assert.Single(spans);
            Assert.True(s.Lo > 25 && s.Hi < 150);
            var plot = new BrokenPlot(new Rect(0, 0, 100, 60), 0, 50, null, 0, 170, new[] { s.ToBreak() });
            Assert.All(curve, p => Assert.True(plot.IsVisible(p)));
            // The visible data now uses much more of the height than without the break.
            Assert.True(plot.Y.UnitLength > 60.0 / 170 * 2);
        }
    }
}
