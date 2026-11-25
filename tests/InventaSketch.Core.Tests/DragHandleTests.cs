using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>A box with one vertical handle: a level line at parameter "level" (mm below the top edge, local frame).</summary>
    internal sealed class LevelModule : IModule
    {
        public string Id => "level";
        public I18nText Name => new("Level", "Pegel");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new LevelBox(), new TestModule.BoxType() };
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();

        internal sealed class LevelBox : ObjectType
        {
            public override string Id => "level.box";
            public override I18nText Name => new("Level box", "Pegelkasten");
            public override Size2 DefaultSize => new(40, 20);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H) };

            private static double Level(SketchObject o) => o.Num("level", 5);

            public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
            {
                var local = new DragHandle("level", new Vec2(0, -o.Size.H / 2 + Level(o)), DragConstraint.Vertical, new I18nText("Level", "Pegel"));
                yield return Dragging.ToPage(local, o);
            }

            public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
            {
                if (key != "level") return o;
                var local = o.Matrix().Invert().Apply(pagePos);
                return o.WithParam("level", Dragging.Snap(Geo.Clamp(local.Y + o.Size.H / 2, 0, o.Size.H), 0.5));
            }
        }
    }

    public class DragHandleTests
    {
        private static Registry Reg() => new Registry().Register(new LevelModule());

        private static SketchObject Box(Registry r, double rotation = 0)
            => r.TypeOf(new SketchObject { Type = "level.box" }).Create(new Vec2(100, 50)) with { Transform = new Transform(100, 50, rotation) };

        [Fact]
        public void HandlesArePlacedOnThePageAndFollowTheRotation()
        {
            var r = Reg();
            var ctx = new RenderContext(SketchDocument.Create());
            var h = r.DragHandles(Box(r), ctx).Single();
            Assert.Equal("level", h.Key);
            Assert.Equal(DragConstraint.Vertical, h.Constraint);
            Assert.True(h.Position.DistanceTo(new Vec2(100, 45)) < 1e-9, h.Position.ToString());
            Assert.True(h.Direction.DistanceTo(new Vec2(0, 1)) < 1e-9);

            var turned = r.DragHandles(Box(r, 90), ctx).Single();
            // Rotated by 90° clockwise: local "down" points to page left.
            Assert.True(Math.Abs(Math.Abs(turned.Direction.X) - 1) < 1e-9 && Math.Abs(turned.Direction.Y) < 1e-9, turned.Direction.ToString());
            Assert.True(turned.Position.DistanceTo(new Vec2(100, 50)) > 4.9);
        }

        [Fact]
        public void MovingAHandleChangesTheValueWithSnapping()
        {
            var r = Reg();
            var ctx = new RenderContext(SketchDocument.Create());
            var box = Box(r);
            var moved = r.MoveHandle(box, "level", new Vec2(130, 52.3), ctx);
            // Local y = 2.3 → level 12.3 → snapped to 0.5 steps.
            Assert.Equal(12.5, moved.Num("level", 0), 9);
            Assert.Same(box, r.MoveHandle(box, "unknown", new Vec2(0, 0), ctx));

            // The rotated box takes the pointer along its own vertical axis.
            var turned = Box(r, 90);
            var h = r.DragHandles(turned, ctx).Single();
            var target = h.Position + h.Direction * 4;
            Assert.Equal(9, r.MoveHandle(turned, "level", target, ctx).Num("level", 0), 9);
        }

        [Fact]
        public void TypesWithoutHandlesHaveNone()
        {
            var r = Reg();
            var plain = r.TypeOf(new SketchObject { Type = "test.box" }).Create(new Vec2(10, 10));
            var ctx = new RenderContext(SketchDocument.Create());
            Assert.Empty(r.DragHandles(plain, ctx));
            Assert.Same(plain, r.MoveHandle(plain, "x", new Vec2(1, 1), ctx));
            Assert.Empty(r.DragHandles(new SketchObject { Type = "missing.type" }, ctx));
        }

        [Fact]
        public void ConstrainAndSnapHelpers()
        {
            var h = new DragHandle("k", new Vec2(10, 10), DragConstraint.Horizontal, new I18nText("k")) { Direction = new Vec2(1, 0) };
            Assert.Equal(new Vec2(17, 10), Dragging.Constrain(h, new Vec2(17, 3)));
            var free = h with { Constraint = DragConstraint.Free, Direction = Vec2.Zero };
            Assert.Equal(new Vec2(17, 3), Dragging.Constrain(free, new Vec2(17, 3)));
            Assert.Equal(0.3, Dragging.Snap(0.1 + 0.2, 0.1));
            Assert.Equal(2.5, Dragging.Snap(2.4, 0.25));
            Assert.Equal(-1.5, Dragging.Snap(-1.52, 0.5));
            Assert.Equal(7.3, Dragging.Snap(7.3, 0));
            Assert.Equal(1200, Dragging.SnapSignificant(1234, 2));
            Assert.Equal(0.047, Dragging.SnapSignificant(0.04712, 2));
            Assert.Equal(0, Dragging.SnapSignificant(0, 2));
        }
    }
}
