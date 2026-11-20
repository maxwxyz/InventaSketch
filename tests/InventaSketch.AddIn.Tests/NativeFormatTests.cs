using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using Xunit;
using Look = InventaSketch.AddIn.OnSlide.NativeFormat.Look;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Formatting done with PowerPoint's own tools is folded into the model as object style overrides.</summary>
    public class NativeFormatTests
    {
        private static Look L(string? line = "#000000", double? weight = 0.75, string? fill = "none", string? text = null, double? size = null, string? font = null, bool? bold = null)
            => new(line, weight, fill, text, size, font, bold, bold is null ? null : false, bold is null ? null : false);

        [Fact]
        public void LookRoundTripsThroughTheTag()
        {
            var look = new Look("#1F5FBF", 1.5, "#FFFFFF", "#000000", 10, "Segoe UI", true, false, true);
            Assert.Equal(look, Look.Decode(look.Encode()));
            Assert.Null(Look.Decode(""));
        }

        [Fact]
        public void UnchangedShapesGiveNoOverride()
        {
            var a = L();
            Assert.Null(NativeFormat.Decide(new[] { (a, a), (a, a) }));
        }

        [Fact]
        public void RecolouredGroupBecomesStrokeAndFillOverride()
        {
            var before = L(fill: "#FFFFFF");
            var after = L(line: "#C00000", weight: 2.25, fill: "#FFD34D");
            var thin = L(weight: 0.5, fill: "#FFFFFF");
            var thinAfter = L(line: "#C00000", weight: 2.25, fill: "#FFD34D");
            var style = NativeFormat.Decide(new[] { (before, after), (thin, thinAfter) })!;
            Assert.Equal("#C00000", style.Stroke);
            Assert.Equal(2.25 / Geo.PtPerMm, style.StrokeWidth!.Value, 3);
            Assert.Equal(FillKind.Solid, style.Fill!.Kind);
            Assert.Equal("#FFD34D", style.Fill.Color);
            Assert.Null(style.TextColor);
        }

        [Fact]
        public void TextChangesBecomeTextOverridesWithSizeFactor()
        {
            var before = L(text: "#000000", size: 10, font: "Segoe UI", bold: false);
            var after = L(text: "#1F5FBF", size: 15, font: "Arial", bold: true);
            var style = NativeFormat.Decide(new[] { (before, after) })!;
            Assert.Equal("#1F5FBF", style.TextColor);
            Assert.Equal("Arial", style.Font);
            Assert.Equal(1.5, style.FontScale!.Value, 6);
            Assert.True(style.Bold);
            // A later change multiplies the existing factor.
            var merged = NativeFormat.Apply(new Style { FontScale = 2 }, new Style { FontScale = 1.5 });
            Assert.Equal(3, merged.FontScale!.Value, 6);
        }

        [Fact]
        public void GradientFillIsNotTakenOver()
        {
            var style = NativeFormat.Decide(new[] { (L(fill: "#FFFFFF"), L(fill: "other")) });
            Assert.True(style is null || style.Fill is null);
        }

        [Fact]
        public void TextOverridesReachEveryTextPrimitive()
        {
            var prims = Prim.Group(Prim.Text(0, 0, "A", new TextStyle { Size = 4 }), Prim.Line(0, 0, 1, 1));
            var styled = (GroupPrim)Prim.TextOverride(prims, new Style { TextColor = "#C00000", FontScale = 2, Bold = true });
            var text = styled.Children.OfType<TextPrim>().Single();
            Assert.Equal("#C00000", text.TextStyle.Color);
            Assert.Equal(8, text.TextStyle.Size, 6);
            Assert.True(text.TextStyle.Bold);
            Assert.Same(prims, Prim.TextOverride(prims, new Style { Stroke = "#000000" }));
        }
    }
}
