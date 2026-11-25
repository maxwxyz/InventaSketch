using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class EngineeringTests
    {
        [Theory]
        [InlineData("lowpass1", -3.0103, -45)]
        [InlineData("highpass1", -3.0103, 45)]
        [InlineData("lowpass2", 4.4370, -90)]   // ζ = 0.3: |H(fc)| = 1/(2ζ)
        [InlineData("highpass2", 4.4370, 90)]
        public void BodeResponseAtTheCornerFrequency(string system, double db, double deg)
        {
            var (m, p) = BodeChart.Response(system, 1000, 1000, 0.3, 0);
            Assert.Equal(db, m, 3);
            Assert.Equal(deg, p, 6);
            // Two decades above: first order −20 dB/decade, second order −40 dB/decade.
            var (hi, _) = BodeChart.Response(system, 100000, 1000, 0.3, 0);
            var slope = system.EndsWith("2", StringComparison.Ordinal) ? -80 : -40;
            if (system.StartsWith("low", StringComparison.Ordinal)) Assert.InRange(hi, slope - 1, slope + 1);
            else Assert.InRange(hi, -0.1, 0.1);
        }

        [Fact]
        public void BodePlotStacksMagnitudeAndPhaseOnALogAxis()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.bode");
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToDictionary(a => a.Id!);
            Assert.Contains("origin", anchors.Keys);
            Assert.Contains("origin2", anchors.Keys);
            Assert.True(anchors["fc.phase"].P.Y > anchors["fc"].P.Y, "phase panel below the magnitude panel");
            Assert.Equal(anchors["fc"].P.X, anchors["fc.phase"].P.X, 6);
            var texts = Prim.Flatten(Fx.Render(r, o)).OfType<TextPrim>().ToList();
            Assert.Contains(texts, t => t.Text == "1000");
            Assert.Contains(texts, t => t.Text == "Magnitude (dB)");
            Assert.Contains(texts, t => t.Text == "Phase φ (°)");
            Assert.Contains(texts, t => t.Text == "-90" || t.Text == "-180");
            // Magnitude only: one panel.
            var mag = o.WithParam("show", "magnitude");
            Assert.DoesNotContain(Fx.Type(r, mag).Anchors(mag, Fx.Ctx()), a => a.Id == "fc.phase");
            // Measured data replace the system.
            var data = o.WithParam("data", "10;0;-5\n100;-1;-30\n1000;-6;-70\n10000;-25;-88");
            var ports = Fx.Type(r, data).Anchors(data, Fx.Ctx()).Where(a => a.Id!.StartsWith("s", StringComparison.Ordinal)).ToList();
            Assert.Equal(8, ports.Count);
            Fx.AssertInsideBox(Fx.Render(r, data), data.Size, "bode data");
        }

        [Fact]
        public void TorqueCurveHasConstantTorqueThenConstantPower()
        {
            Assert.Equal(300, Drive.Torque(2000, 300, 4000));
            Assert.Equal(100, Drive.Torque(12000, 300, 4000), 9);
            var p1 = Drive.PowerKw(Drive.Torque(6000, 300, 4000), 6000);
            var p2 = Drive.PowerKw(Drive.Torque(11000, 300, 4000), 11000);
            Assert.Equal(p1, p2, 9);
            Assert.Equal(125.66, Drive.PowerKw(300, 4000), 2);
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.torque");
            var a = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToDictionary(x => x.Id!);
            Assert.Equal(a["tmax"].P.Y, a["base"].P.Y, 6);
            Assert.True(a["end"].P.Y > a["base"].P.Y);
            var prims = Prim.Flatten(Fx.Render(r, o)).ToList();
            Assert.Equal(2, prims.Count(p => ChartTags.Is(p.Tag, ChartTags.Series)));
            Assert.Equal(2, prims.Count(p => ChartTags.Is(p.Tag, ChartTags.Note)));
            Assert.Contains(prims, p => ChartTags.Is(p.Tag, ChartTags.Helper));
            var plain = Prim.Flatten(Fx.Render(r, Fx.Item(r, "chart.torque.plain"))).ToList();
            Assert.Equal(1, plain.Count(p => ChartTags.Is(p.Tag, ChartTags.Series)));
        }

        [Fact]
        public void EfficiencyMapPeaksAtTheGivenEfficiency()
        {
            var (ns, ts, eta, peak) = EfficiencyChart.Field(300, 4000, 12000, 96);
            var max = 0.0;
            for (var i = 0; i < ns.Length; i++)
                for (var j = 0; j < ts.Length; j++)
                    if (ts[j] <= Drive.Torque(ns[i], 300, 4000)) max = Math.Max(max, eta[i, j]);
            Assert.InRange(max, 95, 96.0001);
            Assert.True(peak.Y <= Drive.Torque(peak.X, 300, 4000) + 1e-9);
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.efficiency");
            var prims = Prim.Flatten(Fx.Render(r, o)).ToList();
            var contours = prims.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Contour)).ToList();
            Assert.True(contours.Count >= 5, "contour count " + contours.Count);
            Assert.Contains(prims, p => ChartTags.Is(p.Tag, ChartTags.Envelope));
            Assert.Contains(prims.OfType<TextPrim>(), t => ChartTags.Is(t.Tag, ChartTags.ContourLabel) && t.Text == "90 %");
            Assert.Contains(Fx.Type(r, o).Anchors(o, Fx.Ctx()), a => a.Id == "peak");
            // Levels at or above the peak draw nothing; the German labels use a decimal comma.
            var high = o.WithParam("levels", "97;99");
            Assert.DoesNotContain(Prim.Flatten(Fx.Render(r, high)), p => ChartTags.Is(p.Tag, ChartTags.Contour));
            var de = Prim.Flatten(Fx.Render(r, o.WithParam("levels", "92,5"), Fx.Ctx(Lang.De))).OfType<TextPrim>();
            Assert.Contains(de, t => t.Text == "92,5 %");
        }

        [Fact]
        public void ContoursOfACircularFieldAreClosedCircles()
        {
            var xs = Enumerable.Range(0, 41).Select(i => -2 + i * 0.1).ToArray();
            var ys = xs;
            var v = new double[41, 41];
            for (var i = 0; i < 41; i++) for (var j = 0; j < 41; j++) v[i, j] = Math.Sqrt(xs[i] * xs[i] + ys[j] * ys[j]);
            var line = Assert.Single(Contours.Trace(v, xs, ys, 1.0));
            Assert.True(line.Count > 20);
            Assert.Equal(line[0], line[line.Count - 1]);
            Assert.All(line, p => Assert.InRange(p.Length, 0.98, 1.02));
            var kept = Contours.Keep(line, p => p.X >= 0);
            Assert.All(kept.SelectMany(k => k), p => Assert.True(p.X >= -1e-4));
        }

        [Fact]
        public void PumpOperatingPointLiesOnBothCurves()
        {
            var (q, h, hs, k) = PumpChart.OperatingPoint(40, 100, 10, 70);
            Assert.Equal(70, q);
            Assert.Equal(40 * (1 - 0.49), h, 9);
            Assert.Equal(h, hs + k * q * q, 9);
            // A static head above the operating head is limited, so the system curve still rises.
            var (_, h2, hs2, k2) = PumpChart.OperatingPoint(40, 100, 50, 70);
            Assert.True(hs2 < h2 && k2 > 0);
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.pump");
            var a = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToDictionary(x => x.Id!);
            Assert.True(a["op"].P.X > a["h0"].P.X && a["op"].P.Y < a["qmax"].P.Y);
            var prims = Prim.Flatten(Fx.Render(r, o)).ToList();
            Assert.Contains(prims, p => ChartTags.Is(p.Tag, ChartTags.Point));
            Assert.Equal(2, prims.Count(p => ChartTags.Is(p.Tag, ChartTags.Series)));
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Text == "OP");
            Assert.Contains(Prim.Flatten(Fx.Render(r, o, Fx.Ctx(Lang.De))).OfType<TextPrim>(), t => t.Text == "BP");
            var fan = Prim.Flatten(Fx.Render(r, Fx.Item(r, "chart.fan"))).OfType<TextPrim>().ToList();
            Assert.Contains(fan, t => t.Text == "Pressure Δp (Pa)");
        }

        [Fact]
        public void ToleranceBandEnclosesTheNominalCurve()
        {
            var block = ChartData.Parse("0;10\n1;20\n2;30").Single();
            var (nom, lo, hi) = BandChart.Curves(block, 10, percent: true);
            Assert.Equal(new[] { 9.0, 18, 27 }, lo.Select(p => p.Y).Select(v => Math.Round(v, 9)));
            Assert.Equal(new[] { 11.0, 22, 33 }, hi.Select(p => p.Y).Select(v => Math.Round(v, 9)));
            var (_, lo2, hi2) = BandChart.Curves(ChartData.Parse("0;10;1\n1;20;2").Single(), 10, true);
            Assert.Equal(new[] { 9.0, 18 }, lo2.Select(p => p.Y));
            Assert.Equal(new[] { 11.0, 22 }, hi2.Select(p => p.Y));
            var (_, lo3, hi3) = BandChart.Curves(ChartData.Parse("0;10;8;13").Single(), 10, true);
            Assert.Equal(8, lo3[0].Y);
            Assert.Equal(13, hi3[0].Y);
            var (_, lo4, _) = BandChart.Curves(block, 2, percent: false);
            Assert.Equal(8, lo4[0].Y);

            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.band");
            var prims = Prim.Flatten(Fx.Render(r, o)).ToList();
            var band = (PathPrim)prims.First(p => ChartTags.Is(p.Tag, ChartTags.Band));
            Assert.True(band.IsClosed);
            Assert.Equal(FillKind.Solid, band.Style.EffectiveFill.Kind);
            var mono = (PathPrim)Prim.Flatten(Fx.Render(r, o, Fx.Ctx(mono: true))).First(p => ChartTags.Is(p.Tag, ChartTags.Band));
            Assert.Equal(FillKind.Hatch, mono.Style.EffectiveFill.Kind);
            // Every nominal port lies inside the band polygon.
            var poly = Draw.Sample(band.Segments.Where(s => s.Op != SegOp.Z).ToList());
            foreach (var port in Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Id!.StartsWith("s1.", StringComparison.Ordinal) && a.Id != "s1.1" && a.Id != "s1.7"))
                Assert.True(Geo.PointInPolygon(port.P, poly), "nominal point outside the band " + port.Id);
            var measured = Fx.Item(r, "chart.band.measured");
            var mp = Prim.Flatten(Fx.Render(r, measured)).ToList();
            Assert.Contains(mp, p => ChartTags.Is(p.Tag, ChartTags.Marker));
            Assert.Equal(3, mp.Count(p => ChartTags.Is(p.Tag, ChartTags.LegendText)));
        }

        [Fact]
        public void BandIsClippedToAFixedRange()
        {
            var poly = new List<Vec2> { new(-5, -5), new(5, -5), new(5, 5), new(-5, 5) };
            var clipped = BandChart.ClipPolygon(poly, new Rect(-1, -2, 2, 4));
            Assert.All(clipped, p => Assert.True(p.X >= -1 - 1e-9 && p.X <= 1 + 1e-9 && p.Y >= -2 - 1e-9 && p.Y <= 2 + 1e-9));
            Assert.Equal(8, Math.Abs(Geo.PolygonArea(clipped)), 9);
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.band").WithParam("yrange", "2;4");
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "band clipped");
        }
    }
}
