using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Selection-wide operations (<see cref="Arrange"/>).</summary>
    public class ArrangeTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void AlignAndDistributeOnlyWhenItChangesSomething()
        {
            var boxes = new[] { new Rect(0, 0, 10, 10), new Rect(30, 6, 10, 10), new Rect(50, 2, 20, 10) };
            var row = Arrange.AlignCentres(boxes, row: true);
            Assert.True(Arrange.Changes(row));
            var aligned = boxes.Select((b, i) => new Rect(b.X + row[i].X, b.Y + row[i].Y, b.W, b.H)).ToList();
            Assert.All(aligned, b => Assert.Equal(aligned[0].Y + aligned[0].H / 2, b.Y + b.H / 2, 6));
            Assert.False(Arrange.Changes(Arrange.AlignCentres(aligned, row: true)));

            var spread = Arrange.Distribute(boxes, horizontal: true);
            var spaced = boxes.Select((b, i) => b.X + b.W / 2 + spread[i].X).OrderBy(x => x).ToList();
            Assert.Equal(spaced[1] - spaced[0], spaced[2] - spaced[1], 6);
            Assert.False(Arrange.Changes(Arrange.Distribute(boxes.Take(2).ToList(), horizontal: true)));
        }

        [Fact]
        public void CommonViewsLeaveOutTheViewAllAlreadyShow()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "stick");
            var a = Reg.Create(item, new Vec2(10, 10));
            var b = Reg.Create(item, new Vec2(40, 10));
            var views = Arrange.CommonViews(Reg, new[] { a, b });
            Assert.NotEmpty(views);
            Assert.DoesNotContain(views, v => v.Id == "stick");
            var arrow = Reg.Create(Reg.Library.First(i => i.TypeId == "ann.arrow"), new Vec2(0, 0));
            Assert.Empty(Arrange.CommonViews(Reg, new[] { a, arrow }));
        }

        [Fact]
        public void RenumberInReadingOrderKeepingTheScheme()
        {
            var numerals = new[]
            {
                ("a", new Vec2(50, 10), "14"),
                ("b", new Vec2(10, 12), "10"),
                ("c", new Vec2(10, 40), "12"),
                ("d", new Vec2(80, 8), "16"),
            };
            var result = Arrange.Renumber(numerals);
            // Row 1 (y ≈ 10): b, a, d → 10, 12, 14; row 2: c → 16.
            Assert.False(result.ContainsKey("b"));
            Assert.Equal("12", result["a"]);
            Assert.Equal("14", result["d"]);
            Assert.Equal("16", result["c"]);
            Assert.Empty(Arrange.Renumber(new[] { ("x", new Vec2(0, 0), "1"), ("y", new Vec2(10, 0), "2") }));
        }
    }
}
