using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>The "next arrowhead style" action and the head choice parameters (arrow-like heads only).</summary>
    public class ArrowheadCycleTests
    {
        [Fact]
        public void NextStyleCyclesThroughArrowLikeHeadsOnly()
        {
            var r = T.Reg();
            var (doc, arrow) = T.Insert(r, "ann.arrow", new Vec2(100, 100));
            var seen = new List<ArrowHead>();
            var o = arrow;
            for (var i = 0; i < Ann.HeadCycle.Length; i++)
            {
                o = r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == "ann.head.style").Apply(o);
                var line = Assert.IsType<LinePrim>(T.Render(r, Edits.Replace(doc, o), arrow.Id).Single());
                seen.Add(line.Style.ArrowEnd!.Value);
            }
            // Starts after the default triangle, visits every arrow-like head once and returns to the triangle.
            Assert.Equal(Ann.HeadCycle.Skip(1).Concat(new[] { ArrowHead.Triangle }), seen);
            Assert.Contains(ArrowHead.TriangleOpen, seen);
            Assert.Contains(ArrowHead.DiamondOpen, seen);
            Assert.All(seen, h => Assert.True(ArrowHeads.IsArrow(h)));
            Assert.Equal(Ann.HeadCycle.Length, Ann.HeadCycle.Distinct().Count());
        }

        [Fact]
        public void CycleCoversEveryArrowLikeHeadAndRestartsFromCrowsFeet()
        {
            var arrowLike = System.Enum.GetValues(typeof(ArrowHead)).Cast<ArrowHead>().Where(ArrowHeads.IsArrow).OrderBy(h => h).ToList();
            Assert.Equal(arrowLike, Ann.HeadCycle.OrderBy(h => h));
            Assert.Equal(ArrowHead.Triangle, Ann.NextHead(ArrowHead.None));
            Assert.Equal(ArrowHead.Triangle, Ann.NextHead(ArrowHead.CrowMany));
            Assert.Equal(ArrowHead.TriangleOpen, Ann.NextHead(ArrowHead.Triangle));
        }

        [Fact]
        public void HollowHeadsAreChoicesAndRoundTripAsParameters()
        {
            var r = T.Reg();
            var (doc, arrow) = T.Insert(r, "ann.arrow", new Vec2(100, 100));
            var choice = r.TypeOf(arrow).Parameters.Single(p => p.Key == "headEnd");
            var values = choice.Options!.Select(o => o.Value).ToList();
            Assert.Contains("triangleOpen", values);
            Assert.Contains("diamondOpen", values);
            Assert.DoesNotContain(values, v => v.StartsWith("crow", System.StringComparison.Ordinal));
            Assert.All(choice.Options!, o => Assert.False(string.IsNullOrEmpty(o.Label.De)));
            Assert.Equal("triangleOpen", Ann.HeadName(ArrowHead.TriangleOpen));
            var o = arrow.WithParam("headEnd", "diamondOpen").WithParam("headStart", "triangleOpen");
            var line = Assert.IsType<LinePrim>(T.Render(r, Edits.Replace(doc, o), arrow.Id).Single());
            Assert.Equal(ArrowHead.DiamondOpen, line.Style.ArrowEnd);
            Assert.Equal(ArrowHead.TriangleOpen, line.Style.ArrowStart);
        }
    }
}
