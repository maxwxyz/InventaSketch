using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Hollow UML arrowheads, crow's-foot ends and text underline in Core and the SVG renderer.</summary>
    public class DrawingTests
    {
        private static readonly ArrowHead[] NewHeads =
        {
            ArrowHead.TriangleOpen, ArrowHead.DiamondOpen, ArrowHead.CrowOne, ArrowHead.CrowMany, ArrowHead.CrowZeroOne,
            ArrowHead.CrowOneOnly, ArrowHead.CrowZeroMany, ArrowHead.CrowOneMany,
        };

        private static LinePrim Arrow(ArrowHead end, string color = "#000000", ArrowSize size = ArrowSize.Medium)
            => Prim.Line(0, 0, 40, 0, Style.Visible with { Stroke = color, StrokeWidth = 0.3, ArrowEnd = end, ArrowSize = size });

        private static string Svg(Primitive p, bool mono = false)
            => SvgRenderer.RenderPrimitives(new[] { p }, new SvgOptions { Background = null, Monochrome = mono });

        [Fact]
        public void ExistingHeadNamesAndOrderAreUnchanged()
        {
            // JSON persists names; the original members keep their names and positions, new ones are appended.
            var names = Enum.GetNames(typeof(ArrowHead));
            Assert.Equal(new[] { "None", "Triangle", "Open", "Stealth", "Diamond", "Oval", "Tick", "Half" }, names.Take(8));
            Assert.Equal(NewHeads.Select(h => h.ToString()), names.Skip(8));
        }

        [Fact]
        public void NewHeadsRoundTripThroughJsonByName()
        {
            var style = new Style { ArrowStart = ArrowHead.CrowOneOnly, ArrowEnd = ArrowHead.TriangleOpen };
            var json = JsonSerializer.Serialize(style, DocumentJson.Options);
            Assert.Contains("\"crowOneOnly\"", json);
            Assert.Contains("\"triangleOpen\"", json);
            Assert.Equal(style, JsonSerializer.Deserialize<Style>(json, DocumentJson.Options));
        }

        [Fact]
        public void ClassificationSeparatesArrowsFromCrowsFeet()
        {
            Assert.False(ArrowHeads.IsArrow(ArrowHead.None));
            Assert.True(ArrowHeads.IsArrow(ArrowHead.TriangleOpen));
            Assert.True(ArrowHeads.IsArrow(ArrowHead.DiamondOpen));
            Assert.True(ArrowHeads.IsArrow(ArrowHead.Half));
            foreach (var h in NewHeads.Where(h => h.ToString().StartsWith("Crow", StringComparison.Ordinal)))
            {
                Assert.True(ArrowHeads.IsCrowsFoot(h));
                Assert.False(ArrowHeads.IsArrow(h));
            }
        }

        [Theory]
        [InlineData(ArrowHead.TriangleOpen)]
        [InlineData(ArrowHead.DiamondOpen)]
        public void HollowHeadsAreUnfilledOutlinesAndTheLineStopsAtTheirBase(ArrowHead head)
        {
            var (shaft, heads) = ArrowHeads.Explode(Arrow(head));
            var poly = Assert.IsType<PathPrim>(Assert.Single(heads));
            Assert.True(poly.IsClosed);
            Assert.Equal(FillKind.None, poly.Style.EffectiveFill.Kind);
            Assert.Equal(DashStyle.Solid, poly.Style.EffectiveDash);
            var pts = Prim.Points(poly).ToList();
            Assert.Contains(pts, p => p.DistanceTo(new Vec2(40, 0)) < 1e-9); // tip on the end point
            var back = pts.Min(p => p.X);
            var line = Assert.IsType<LinePrim>(shaft);
            Assert.Equal(back, line.B.X, 9); // no line inside the hollow head
            Assert.Null(line.Style.ArrowEnd);
            Assert.Equal(40 - back, ArrowHeads.Extent(head, ArrowSize.Medium, 0.3), 9);
        }

        [Fact]
        public void HollowAndFilledVariantsShareTheirSize()
        {
            Assert.Equal(ArrowHeads.Extent(ArrowHead.Diamond, ArrowSize.Large, 0.3), ArrowHeads.Extent(ArrowHead.DiamondOpen, ArrowSize.Large, 0.3), 9);
            Assert.Equal(ArrowHeads.Extent(ArrowHead.Triangle, ArrowSize.Large, 0.3), ArrowHeads.Extent(ArrowHead.TriangleOpen, ArrowSize.Large, 0.3), 9);
        }

        [Theory]
        [InlineData(ArrowHead.CrowOne, 1, 0, 0)]
        [InlineData(ArrowHead.CrowOneOnly, 2, 0, 0)]
        [InlineData(ArrowHead.CrowZeroOne, 2, 0, 1)]
        [InlineData(ArrowHead.CrowMany, 0, 1, 0)]
        [InlineData(ArrowHead.CrowOneMany, 1, 1, 0)]
        [InlineData(ArrowHead.CrowZeroMany, 1, 1, 1)]
        public void CrowsFootEndsHaveBarsFeetAndCircles(ArrowHead head, int lines, int feet, int circles)
        {
            var (shaft, heads) = ArrowHeads.Explode(Arrow(head));
            Assert.Equal(lines, heads.OfType<LinePrim>().Count());
            Assert.Equal(feet, heads.OfType<PathPrim>().Count());
            Assert.Equal(circles, heads.OfType<EllipsePrim>().Count());
            Assert.All(heads, h => Assert.NotEqual(FillKind.Solid, DrawStyle(h).EffectiveFill.Kind));
            var end = ((LinePrim)shaft).B;
            if (circles == 0)
            {
                Assert.Equal(40, end.X, 9); // the line runs into the foot / through the bars
            }
            else
            {
                // A circle interrupts the line: the shaft stops behind it, the head draws the piece to the tip.
                var c = heads.OfType<EllipsePrim>().Single();
                Assert.Equal(c.Center.X - c.Rx, end.X, 9);
                Assert.Contains(heads.OfType<LinePrim>(), l => Math.Abs(l.B.X - 40) < 1e-9 && Math.Abs(l.A.X - (c.Center.X + c.Rx)) < 1e-9);
            }
            if (feet == 1)
            {
                var foot = heads.OfType<PathPrim>().Single();
                Assert.False(foot.IsClosed);
                var toes = new[] { foot.Segments[0].P, foot.Segments[2].P };
                Assert.All(toes, t => Assert.Equal(40, t.X, 9)); // toes touch the entity
                Assert.True(foot.Segments[1].P.X < 40);
            }
            // Everything stays close to the end point.
            Assert.All(heads.SelectMany(Prim.Points), p => Assert.True(p.DistanceTo(new Vec2(40, 0)) < 3 * ArrowHeads.Length(ArrowSize.Medium, 0.3)));
        }

        private static Style DrawStyle(Primitive p) => p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, RectPrim r => r.Style, _ => Style.Default };

        [Fact]
        public void EveryNewHeadIsDistinctInSvg()
        {
            var svgs = NewHeads.Select(h => Svg(Arrow(h))).ToList();
            Assert.Equal(svgs.Count, svgs.Distinct().Count());
            foreach (var svg in svgs)
            {
                Assert.DoesNotContain("NaN", svg);
                Assert.DoesNotContain("marker", svg); // explicit geometry, no SVG markers
            }
        }

        [Fact]
        public void HollowHeadSvgHasNoFillAndCrowCircleIsUnfilled()
        {
            var tri = Svg(Arrow(ArrowHead.TriangleOpen, "#cc2200"));
            Assert.Matches("<path d=\"M40 0L[^\"]*Z\" fill=\"none\" stroke=\"#cc2200\"", tri);
            var circle = Svg(Arrow(ArrowHead.CrowZeroMany, "#cc2200"));
            Assert.Matches("<ellipse [^>]*fill=\"none\" stroke=\"#cc2200\"", circle);
        }

        [Fact]
        public void MonochromeHeadsStayBlackAndUnfilled()
        {
            foreach (var h in NewHeads.Concat(new[] { ArrowHead.Triangle, ArrowHead.Diamond }))
            {
                var svg = Svg(Arrow(h, "#2266cc"), mono: true);
                var colours = Regex.Matches(svg, "#[0-9a-fA-F]{6}").Cast<Match>().Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();
                Assert.Equal(new[] { "#000000" }, colours);
            }
        }

        [Fact]
        public void HeadsOnPathsFollowTheLastSegment()
        {
            var path = Prim.Polyline(new[] { new Vec2(0, 0), new Vec2(20, 0), new Vec2(20, 30) },
                Style.Visible with { StrokeWidth = 0.3, ArrowEnd = ArrowHead.DiamondOpen, ArrowStart = ArrowHead.CrowMany });
            var (shaft, heads) = ArrowHeads.Explode(path);
            var segs = ((PathPrim)shaft).Segments;
            Assert.Equal(30 - ArrowHeads.Extent(ArrowHead.DiamondOpen, ArrowSize.Medium, 0.3), segs[2].P.Y, 9); // stops at the diamond's back vertex
            Assert.Equal(20, segs[2].P.X, 9);
            Assert.Equal(new Vec2(0, 0), segs[0].P); // crow's foot keeps the line to the entity
            Assert.Equal(2, heads.Count);
            Assert.Contains(heads, h => Prim.Points(h).Any(p => p.DistanceTo(new Vec2(20, 30)) < 1e-9));
            Assert.Contains("fill=\"none\"", Svg(path));
        }

        [Fact]
        public void ExplodeLeavesPrimitivesWithoutHeadsUnchanged()
        {
            var line = Prim.Line(0, 0, 10, 0);
            Assert.Same(line, ArrowHeads.Explode(line).Shaft);
            Assert.Empty(ArrowHeads.Explode(line).Heads);
            var closed = Prim.Polygon(new[] { new Vec2(0, 0), new Vec2(5, 0), new Vec2(0, 5) }, Style.Visible with { ArrowEnd = ArrowHead.TriangleOpen });
            Assert.Empty(ArrowHeads.Explode(closed).Heads); // closed paths have no ends
            Assert.Equal(0, ArrowHeads.Extent(ArrowHead.None, ArrowSize.Medium, 0.5));
        }

        [Fact]
        public void HeadSizeScalesWithArrowSize()
        {
            foreach (var h in NewHeads)
            {
                var s = ArrowHeads.Extent(h, ArrowSize.Small, 0.3);
                var m = ArrowHeads.Extent(h, ArrowSize.Medium, 0.3);
                var l = ArrowHeads.Extent(h, ArrowSize.Large, 0.3);
                Assert.True(s < m && m < l, h.ToString());
            }
        }

        [Fact]
        public void UnderlinedTextUsesTextDecoration()
        {
            var plain = Svg(Prim.Text(0, 0, "id"));
            Assert.DoesNotContain("text-decoration", plain);
            var under = Svg(Prim.Text(0, 0, "id", new TextStyle { Underline = true }));
            Assert.Contains("text-decoration=\"underline\"", under);
            // Multi-line text: the decoration sits on the text element, so every line is underlined.
            var multi = Svg(Prim.Text(0, 0, "a\nb", new TextStyle { Underline = true }));
            Assert.Matches("<text [^>]*text-decoration=\"underline\"[^>]*><tspan", multi);
            Assert.True(new TextStyle { Underline = true } != new TextStyle());
        }
    }
}
