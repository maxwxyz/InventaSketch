using System.Collections.Generic;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Black-and-white plans: every colour becomes black or white, translucent fills (highlighters) disappear.</summary>
    public class MonochromePlanTests
    {
        [Fact]
        public void ColoursMapToBlackAndWhite()
        {
            var line = new PlanOp { Op = "line", Stroke = new PlanStroke { Color = "#1f5fbf" } };
            var dark = new PlanOp { Op = "rect", Fill = new PlanFill { Kind = "solid", Color = "#202020" }, Stroke = new PlanStroke { Color = "#ff0000" } };
            var light = new PlanOp { Op = "rect", Fill = new PlanFill { Kind = "solid", Color = "#ffd34d" } };
            var marker = new PlanOp { Op = "rect", Fill = new PlanFill { Kind = "solid", Color = "#ffff00" }, Opacity = 0.4 };
            var text = new PlanOp { Op = "text", Color = "#c00000" };
            var white = new PlanOp { Op = "text", Color = "#ffffff" };
            var group = new PlanOp { Op = "group", Children = new List<PlanOp> { text, white } };
            DrawPlanBuilder.Monochrome(new[] { line, dark, light, marker, group });
            Assert.Equal("#000000", line.Stroke!.Color);
            Assert.Equal("#000000", dark.Fill!.Color);
            Assert.Equal("#000000", dark.Stroke!.Color);
            Assert.Equal("#FFFFFF", light.Fill!.Color);
            Assert.Null(marker.Fill!.Color);
            Assert.Null(marker.Opacity);
            Assert.Equal("#000000", text.Color);
            Assert.Equal("#FFFFFF", white.Color);
        }
    }
}
