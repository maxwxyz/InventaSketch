using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Grips of dimensions (<see cref="ObjectType.DragHandles"/>): dimension line offset, text position, arc radius.</summary>
    public class DimensionGripTests
    {
        private static RenderContext Ctx() => new(SketchDocument.Create(PageSizes.A4Landscape));

        private static SketchObject Dim(Registry r, string type, params Vec2[] points)
            => r.Create(r.Item(type)!, Vec2.Zero) with { Points = points };

        private static void Near(double expected, double actual, double tol = 1e-9)
            => Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected}, got {actual}");

        private static void Near(Vec2 expected, Vec2 actual, double tol = 1e-9)
            => Assert.True(expected.DistanceTo(actual) <= tol, $"expected {expected}, got {actual}");

        [Fact]
        public void LinearDimensionLineAndTextAreDragged()
        {
            var r = T.Reg();
            // Horizontal dimension from (85, 100) to (115, 100), default offset −8: the line lies at y = 92.
            var o = Dim(r, "ann.dimension.linear", new Vec2(85, 100), new Vec2(115, 100));
            var handles = r.DragHandles(o, Ctx());
            Assert.Equal(new[] { "offset", "text" }, handles.Select(h => h.Key));

            var off = handles[0];
            Near(new Vec2(107.5, 92), off.Position); // three quarters along the line, away from the centred text
            Assert.Equal(DragConstraint.Vertical, off.Constraint);
            Near(new Vec2(0, 1), off.Direction);
            Assert.Equal("Dimension line offset: −8 mm", off.Label.En);
            Assert.Equal("Abstand der Maßlinie: −8 mm", off.Label.De);
            // 20.2 mm above the points: offset −20 (snapped to 0.5 mm).
            Near(-20, r.MoveHandle(o, "offset", new Vec2(107.5, 79.8), Ctx()).Num("offset", 0));
            // Below the points: positive offset, counted from the points.
            Near(10, r.MoveHandle(o, "offset", new Vec2(107.5, 110.1), Ctx()).Num("offset", 0));

            // Text centre: middle of the line, beside it away from the feature (0.8 + 3.5 · 0.45 mm).
            var text = handles[1];
            Near(new Vec2(100, 92 - 2.375), text.Position);
            Assert.Equal(DragConstraint.Horizontal, text.Constraint);
            Near(new Vec2(1, 0), text.Direction);
            Assert.Equal("Dimension text position: 50 %", text.Label.En);
            // x = 91 is 6 of 30 mm along the line: 20 %.
            var moved = r.MoveHandle(o, "text", new Vec2(91, 89), Ctx());
            Near(0.2, moved.Num("textAt", 0));
            var drawn = Prim.Flatten(r.Render(moved, Ctx())).OfType<TextPrim>().Single(t => t.Tag == AnnTags.DimText);
            Near(new Vec2(91, 92 - 2.375), drawn.Position);
            // The offset grip moves to the other half of the line, away from the text.
            Near(new Vec2(107.5, 92), r.DragHandles(moved, Ctx())[0].Position);
            var right = r.MoveHandle(o, "text", new Vec2(112, 89), Ctx());
            Near(new Vec2(92.5, 92), r.DragHandles(right, Ctx())[0].Position);
            // Far outside: clamped to the parameter range.
            Near(1.5, r.MoveHandle(o, "text", new Vec2(400, 89), Ctx()).Num("textAt", 0));

            Assert.Same(o, r.MoveHandle(o, "nothing", new Vec2(0, 0), Ctx()));
        }

        [Fact]
        public void AlignedDimensionDragsAcrossItsSlantedLine()
        {
            var r = T.Reg();
            // Diagonal from (0, 0) to (30, 40): u = (0.6, 0.8), n = (−0.8, 0.6).
            var o = Dim(r, "ann.dimension.aligned", new Vec2(0, 0), new Vec2(30, 40));
            var off = r.DragHandles(o, Ctx()).Single(h => h.Key == "offset");
            Near(new Vec2(-0.8, 0.6), off.Direction);
            // 12 mm along −n from the points: offset −12.
            var moved = r.MoveHandle(o, "offset", new Vec2(15, 20) + new Vec2(-0.8, 0.6) * -12, Ctx());
            Near(-12, moved.Num("offset", 0));
        }

        [Fact]
        public void AngleDimensionRadiusIsDraggedRadially()
        {
            var r = T.Reg();
            // Vertex (0, 0), legs to the right and up: the arc sweeps −90°, the grip sits at 0.3 of it (−27°).
            var o = Dim(r, "ann.dimension.angle", new Vec2(0, 0), new Vec2(20, 0), new Vec2(0, -20)).WithParam("radius", 10.0);
            var h = r.DragHandles(o, Ctx()).Single();
            Assert.Equal("radius", h.Key);
            var dir = new Vec2(Math.Cos(Geo.DegToRad(-27)), Math.Sin(Geo.DegToRad(-27)));
            Near(dir * 10, h.Position);
            Near(dir, h.Direction);
            Assert.Equal("Arc radius: 10 mm", h.Label.En);
            Assert.Equal("Bogenradius: 10 mm", h.Label.De);
            // 15.2 mm out (and 3 mm sideways, ignored): 15 mm.
            Near(15, r.MoveHandle(o, "radius", dir * 15.2 + dir.Perp * 3, Ctx()).Num("radius", 0));
            // Through the vertex: at least 1 mm.
            Near(1, r.MoveHandle(o, "radius", dir * -5, Ctx()).Num("radius", 0));
            // Without a radius parameter the grip sits on the default arc (0.65 of the shorter leg).
            var plain = Dim(r, "ann.dimension.angle", new Vec2(0, 0), new Vec2(20, 0), new Vec2(0, -20));
            Near(dir * 13, r.DragHandles(plain, Ctx()).Single().Position);
            Assert.Same(o, r.MoveHandle(o, "offset", dir, Ctx()));
        }

        [Fact]
        public void RadiusDimensionTextSlidesAlongTheLine()
        {
            var r = T.Reg();
            var o = Dim(r, "ann.dimension.radius", new Vec2(0, 0), new Vec2(20, 0));
            var h = r.DragHandles(o, Ctx()).Single();
            Assert.Equal("text", h.Key);
            Near(new Vec2(10, -2.375), h.Position);
            Near(new Vec2(1, 0), h.Direction);
            Near(0.25, r.MoveHandle(o, "text", new Vec2(5, -2), Ctx()).Num("textAt", 0));
            // A radius keeps its text on the radius; a diameter may move it to the other half.
            Near(0, r.MoveHandle(o, "text", new Vec2(-10, 0), Ctx()).Num("textAt", 1));
            var dia = o.WithParam("kind", "diameter");
            Near(new Vec2(5, -2.375), r.DragHandles(dia, Ctx()).Single().Position);
            Near(-0.5, r.MoveHandle(dia, "text", new Vec2(-10, 0), Ctx()).Num("textAt", 0));
            Assert.Same(o, r.MoveHandle(o, "radius", new Vec2(5, 5), Ctx()));
        }

        [Fact]
        public void DraggedDimensionValuesSurviveTheJsonRoundTrip()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var lin = Dim(r, "ann.dimension.linear", new Vec2(85, 100), new Vec2(115, 100));
            lin = r.MoveHandle(r.MoveHandle(lin, "offset", new Vec2(100, 79.8), Ctx()), "text", new Vec2(91, 0), Ctx());
            var ang = r.MoveHandle(Dim(r, "ann.dimension.angle", new Vec2(0, 0), new Vec2(20, 0), new Vec2(0, -20)), "radius", new Vec2(15, -1), Ctx());
            doc = Edits.Add(Edits.Add(doc, doc.Pages[0].Id, lin), doc.Pages[0].Id, ang);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Near(-20, back.Get(lin.Id)!.Num("offset", 0));
            Near(0.2, back.Get(lin.Id)!.Num("textAt", 0));
            Near(ang.Num("radius", 0), back.Get(ang.Id)!.Num("radius", -1));
        }
    }
}
