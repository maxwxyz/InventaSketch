using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    public class SnapLabelTests
    {
        [Fact]
        public void LabelsNameTheTargetInBothLanguages()
        {
            Assert.Null(SnapLabel.For(null, Lang.En));
            Assert.Equal("Teilkreis", SnapLabel.For(new Anchor(AnchorKind.Circle, AnchorRole.Pitch, Vec2.Zero, default, 5), Lang.De));
            Assert.Equal("axis", SnapLabel.For(Anchor.Segment(AnchorRole.Axis, Vec2.Zero, new Vec2(1, 0)), Lang.En));
            Assert.Equal("Bolzen 3", SnapLabel.For(Anchor.Point(AnchorRole.Hub, Vec2.Zero, "pin2"), Lang.De));
            Assert.Equal("joint 1", SnapLabel.For(Anchor.Point(AnchorRole.Port, Vec2.Zero, "j0"), Lang.En));
        }
    }
}
