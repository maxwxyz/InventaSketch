using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>The arrowhead form and size choices (dropdowns with the head pictograms) and the head choice parameters.</summary>
    public class ArrowheadCycleTests
    {
        [Fact]
        public void HeadFormAndSizeAreChoicesWithPictograms()
        {
            var r = T.Reg();
            var (doc, arrow) = T.Insert(r, "ann.arrow", new Vec2(100, 100));
            var actions = r.TypeOf(arrow).Actions(arrow, Lang.En).ToList();
            Assert.DoesNotContain(actions, a => a.Icon == IconIds.Cycle);
            var forms = actions.Where(a => a.Choice?.Key == "ann.head.style").ToList();
            Assert.Equal(r.TypeOf(arrow).Parameters.Single(p => p.Key == "headEnd").Options!.Count, forms.Count);
            Assert.All(forms, a => Assert.StartsWith("head-", a.Icon));
            Assert.Equal("ann.head.style=triangle", Assert.Single(forms, a => a.IsActive).Id);
            Assert.Equal(new[] { "headEnd" }, forms[0].ParamKeys);
            var sizes = actions.Where(a => a.Choice?.Key == "ann.head.size").ToList();
            Assert.Equal(new[] { IconIds.SizeSmall, IconIds.SizeMedium, IconIds.SizeLarge }, sizes.Select(a => a.Icon));
            Assert.Equal("ann.head.size=medium", Assert.Single(sizes, a => a.IsActive).Id);
            // Every arrow-like head can be picked and is drawn.
            foreach (var head in Ann.HeadCycle)
            {
                var o = forms.Single(a => a.Id == "ann.head.style=" + Ann.HeadName(head)).Apply(arrow);
                var line = Assert.IsType<LinePrim>(T.Render(r, Edits.Replace(doc, o), arrow.Id).Single());
                Assert.Equal(head, line.Style.ArrowEnd);
            }
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
