using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.AddIn.OnSlide;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>
    /// Handles never lie on top of each other (maintainer report 2026-11-25: a short pipe showed rotate handle, lock badge,
    /// two "+", the diamonds and the move cross within a few pixels). Also: one "+" per free end, light dismiss of the
    /// insert box.
    /// </summary>
    public class HandleLayoutTests
    {
        private const float Scale = 2f; // 200 % display, as in the report

        /// <summary>The short horizontal pipe of the report: 64 px long, free start, docked end with a lock badge.</summary>
        private static List<HandleSpec> ShortPipe()
        {
            var a = new PointF(143, 175);
            var b = new PointF(207, 175);
            var frame = RectangleF.FromLTRB(a.X, a.Y, b.X, b.Y);
            var mid = new PointF((a.X + b.X) / 2, a.Y);
            var specs = HandleLayout.NativeHandles(frame, Scale).ToList();
            specs.Add(new HandleSpec("pt:0", HandleKind.Point, a, default, 0, 5, 100) { Fixed = true });
            specs.Add(new HandleSpec("pt:1", HandleKind.Point, b, default, 0, 5, 100) { Fixed = true });
            specs.Add(new HandleSpec("port:0", HandleKind.Add, a, new PointF(-1, 0), 15, 6.5f, 90) { MaxPush = 72 });
            specs.Add(new HandleSpec("lock:0", HandleKind.Lock, b, new PointF(0.7071f, -0.7071f), 18.4f, 7.5f, 70) { Orbit = true, MaxPush = 48 });
            specs.Add(new HandleSpec("seg:0", HandleKind.Corner, mid, new PointF(1, 0), 0, 3.6f, 40) { Optional = true, Along = true, MaxPush = 8 });
            specs.Add(new HandleSpec("branch:0", HandleKind.Branch, mid, new PointF(0, -1), 16, 5, 30) { Optional = true, Orbit = true, MaxPush = 10 });
            return specs;
        }

        [Fact]
        public void TheShortPipeOfTheReportHasNoOverlappingHandles()
        {
            var placed = HandleLayout.Resolve(ShortPipe(), Scale);
            Assert.False(HandleLayout.AnyOverlap(placed, Scale), HandleLayout.Describe(placed));
            // The "+" stays beyond the free end, the badge beside the docked end.
            var plus = placed.Single(h => h.Id == "port:0");
            Assert.True(plus.Visible && plus.At.X < 143, "the + lies beyond the free start");
            Assert.True(placed.Single(h => h.Id == "lock:0").Visible);
            // Every visible handle keeps the minimum gap to every other one (fixed pairs aside).
            var shown = placed.Where(h => h.Visible || h.Kind == HandleKind.Native).ToList();
            foreach (var h in shown)
                foreach (var o in shown)
                {
                    if (ReferenceEquals(h, o) || (h.Spec.Fixed && o.Spec.Fixed)) continue;
                    var d = Distance(h.At, o.At);
                    Assert.True(d >= (h.Spec.Radius + o.Spec.Radius + HandleLayout.Gap) * Scale - 0.5, $"{h.Id} and {o.Id}: {d:0.0} px");
                }
        }

        [Fact]
        public void OptionalHandlesWithoutRoomAreHiddenOthersArePushedOut()
        {
            var anchor = new PointF(100, 100);
            var specs = new List<HandleSpec>
            {
                new("blocker", HandleKind.Point, anchor, default, 0, 20, 100) { Fixed = true },
                new("corner", HandleKind.Corner, anchor, default, 0, 4, 40) { Optional = true, MaxPush = 0 },
                new("plus", HandleKind.Add, anchor, new PointF(1, 0), 10, 6.5f, 90) { MaxPush = 72 },
            };
            var placed = HandleLayout.Resolve(specs, 1);
            Assert.False(placed.Single(h => h.Id == "corner").Visible);
            var plus = placed.Single(h => h.Id == "plus");
            Assert.True(plus.Visible);
            Assert.True(Distance(plus.At, anchor) >= 20 + 6.5 + HandleLayout.Gap - 0.01);
        }

        [Fact]
        public void HigherPriorityKeepsItsPreferredPlace()
        {
            var at = new PointF(0, 0);
            var specs = new List<HandleSpec>
            {
                new("lock", HandleKind.Lock, at, new PointF(1, 0), 15, 7.5f, 70) { Orbit = true },
                new("plus", HandleKind.Add, at, new PointF(1, 0), 15, 6.5f, 90),
            };
            var placed = HandleLayout.Resolve(specs, 1);
            Assert.Equal(new PointF(15, 0), placed.Single(h => h.Id == "plus").At);
            Assert.NotEqual(new PointF(15, 0), placed.Single(h => h.Id == "lock").At);
            Assert.False(HandleLayout.AnyOverlap(placed, 1));
        }

        [Fact]
        public void ThinFramesShowNoEdgeHandlesButTheMoveHandle()
        {
            var ids = HandleLayout.NativeHandles(new RectangleF(0, 100, 200, 0), 1).Select(h => h.Id).ToList();
            Assert.DoesNotContain("native:l", ids);
            Assert.Contains("native:t", ids);
            Assert.Contains("native:rotate", ids);
            Assert.Contains("native:move", ids);
            Assert.DoesNotContain("native:move", HandleLayout.NativeHandles(new RectangleF(0, 0, 200, 100), 1).Select(h => h.Id));
        }

        [Fact]
        public void SecondaryHandlesFadeInNearThePointer()
        {
            // Progressive disclosure: fully shown within 24 logical px, gone beyond 56, linear in between.
            Assert.Equal(1f, OnSlideController.RevealFor(0));
            Assert.Equal(1f, OnSlideController.RevealFor(OnSlideController.RevealNear));
            Assert.Equal(0f, OnSlideController.RevealFor(OnSlideController.RevealFar));
            Assert.Equal(0f, OnSlideController.RevealFor(500));
            Assert.Equal(0.5f, OnSlideController.RevealFor((OnSlideController.RevealNear + OnSlideController.RevealFar) / 2), 3);
        }

        [Fact]
        public void SegmentCodesMapHandleIds()
        {
            Assert.Equal(4, OnSlideController.SegmentCode("seg:2"));
            Assert.Equal(5, OnSlideController.SegmentCode("branch:2"));
            Assert.Equal(-1, OnSlideController.SegmentCode("port:2"));
            Assert.Equal(-1, OnSlideController.SegmentCode("pt:0"));
            // Connect icons at crossings and bridge icons at junctions have codes of their own, bridges above crossings.
            var cross = OnSlideController.SegmentCode("cross:1");
            var bridge = OnSlideController.SegmentCode("bridge:1");
            Assert.True(cross > OnSlideController.SegmentCode("end:1"));
            Assert.True(bridge > cross);
            Assert.Equal(bridge + 1, OnSlideController.SegmentCode("bridge:2"));
        }

        [Fact]
        public void InsertBoxClosesOnAClickOutside()
        {
            var box = new Rectangle(100, 100, 300, 400);
            Assert.True(InsertSearch.ClickedOutside(false, true, box, new Point(50, 50)));
            Assert.False(InsertSearch.ClickedOutside(false, true, box, new Point(150, 150)), "a click inside");
            Assert.False(InsertSearch.ClickedOutside(true, true, box, new Point(50, 50)), "a press dragged out of the box");
            Assert.False(InsertSearch.ClickedOutside(false, false, box, new Point(50, 50)), "no button");
        }

        [Fact]
        public void SideActionsAreRecognised()
        {
            static InventaSketch.Core.ContextAction A(string group) => new("a", new InventaSketch.Core.I18nText("a", "a"), "", o => o) { Group = group };
            Assert.Equal("top", OnSlideController.SideOf(A("side.top")));
            Assert.Equal("right", OnSlideController.SideOf(A("side.right")));
            Assert.Null(OnSlideController.SideOf(A("side.middle")));
            Assert.Null(OnSlideController.SideOf(A("cells")));
        }

        private static double Distance(PointF a, PointF b) => System.Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
