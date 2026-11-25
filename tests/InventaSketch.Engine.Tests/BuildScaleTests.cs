using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// "+" on a scaled element: the new part takes over the element's scale (lane widths, tooth size) and a road continues
    /// on the lane whose "+" was used (maintainer report 2026-11-25: scaled roads did not fit the new segment).
    /// </summary>
    public class BuildScaleTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static (SketchDocument Doc, SketchObject Obj) One(string itemId, Vec2 at, double sx = 1, double sy = 1)
        {
            var o = Reg.Create(Reg.Item(itemId)!, at);
            o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
            var doc = SketchDocument.Create(new Size2(800, 500));
            return (Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        private static Anchor Lane(SketchDocument doc, SketchObject o, string id)
            => Reg.PageAnchors(o, new RenderContext(doc)).First(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && a.Id == id);

        [Fact]
        public void AScaledRoadContinuesWithTheSameLaneWidthOnTheChosenLane()
        {
            var (doc, road) = One("auto.road.2", new Vec2(200, 200), sx: 1.25, sy: 1.5);
            var lane1 = Lane(doc, road, "lane1");
            var port = Building.Ports(Reg, doc, road.Id).First(p => p.Kind == PortKind.AxisEnd && p.At.DistanceTo(lane1.Q) < 1e-6);
            var placed = Building.Place(Reg, doc, road.Id, port, Reg.Item("auto.road.2")!)!;
            var def = Reg.Item("auto.road.2")!.Size ?? Reg.Type("auto.road.straight")!.DefaultSize;
            // The width across the road is 1.5 times the library width (the same lane width).
            Assert.Equal(def.H * 1.5, placed.Size.H, 6);
            var after = Edits.Add(doc, doc.Pages[0].Id, placed);
            // Lane 2 continues lane 2: its start sits on the "+" that was used, lane 1 on lane 1.
            Assert.True(Lane(after, placed, "lane1").P.DistanceTo(lane1.Q) < 1e-6);
            Assert.True(Lane(after, placed, "lane0").P.DistanceTo(Lane(doc, road, "lane0").Q) < 1e-6);
        }

        [Fact]
        public void RoadsDockOnIntersectionAndRoundaboutArmsLaneForLane()
        {
            foreach (var junction in new[] { "auto.intersection", "auto.roundabout" })
            {
                var (doc, j) = One(junction, new Vec2(300, 300));
                var ends = Reg.PageAnchors(j, new RenderContext(doc)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && a.Id!.StartsWith("lane"))
                    .SelectMany(a => new[] { a.P, a.Q }).ToList();
                var port = Building.Ports(Reg, doc, j.Id).First(p => p.Kind == PortKind.AxisEnd && p.Id.StartsWith("axis"));
                var placed = Building.Place(Reg, doc, j.Id, port, Reg.Item("auto.road.2")!);
                Assert.NotNull(placed);
                var after = Edits.Add(doc, doc.Pages[0].Id, placed!);
                // Both lanes of the road start on lane ends of the arm (same lane width).
                var starts = Reg.PageAnchors(placed!, new RenderContext(after)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis)
                    .Select(a => ends.Any(e => e.DistanceTo(a.P) < 0.05) || ends.Any(e => e.DistanceTo(a.Q) < 0.05)).ToList();
                Assert.True(starts.Count == 2 && starts.All(x => x), junction + ": both lanes meet the arm");
            }
        }

        [Fact]
        public void CurvesAndJunctionsKeepTheRoadLaneWidthForAnyLaneCount()
        {
            var straight = Reg.Create(Reg.Item("auto.road.2")!, Vec2.Zero);
            var lane = straight.Size.H / 2;
            foreach (var id in new[] { "auto.curve", "auto.intersection" })
            {
                var o = Reg.Create(Reg.Item(id)!, new Vec2(400, 400));
                for (var n = 2; n <= 6; n++)
                {
                    var doc = SketchDocument.Create(new Size2(1000, 800));
                    doc = Edits.Add(doc, doc.Pages[0].Id, o);
                    var ends = Reg.PageAnchors(o, new RenderContext(doc)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.End && a.Id!.EndsWith(".end")).Take(2).ToList();
                    Assert.Equal(lane, ends[0].P.DistanceTo(ends[1].P), 3);
                    o = Reg.Type(o.Type)!.Actions(o, Lang.En).Single(a => a.Id == "param.lanes.inc").Apply(o);
                }
            }
            // A 4-lane road docks lane for lane on a 4-lane intersection.
            var x = Reg.Create(Reg.Item("auto.intersection")!, new Vec2(400, 400));
            var t = Reg.Type(x.Type)!;
            for (var k = 0; k < 2; k++) x = t.Actions(x, Lang.En).Single(a => a.Id == "param.lanes.inc").Apply(x);
            var d4 = SketchDocument.Create(new Size2(1000, 800));
            d4 = Edits.Add(d4, d4.Pages[0].Id, x);
            var port = Building.Ports(Reg, d4, x.Id).First(p => p.Kind == PortKind.AxisEnd);
            var road4 = Reg.Item("auto.road.4")!;
            var placed = Building.Place(Reg, d4, x.Id, port, road4)!;
            var after = Edits.Add(d4, d4.Pages[0].Id, placed);
            var armEnds = Reg.PageAnchors(x, new RenderContext(d4)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.End).Select(a => a.P).ToList();
            var met = Reg.PageAnchors(placed, new RenderContext(after)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis)
                .Count(a => armEnds.Any(e => e.DistanceTo(a.P) < 0.05 || e.DistanceTo(a.Q) < 0.05));
            Assert.Equal(4, met);
        }

        [Fact]
        public void RoadElementsFitASlide()
        {
            // A 16:9 slide is 280 x 157.5 mm (13.333 x 7.5 in); every road element fits on it with room to spare.
            foreach (var item in Reg.Library.Where(i => i.TypeId.StartsWith("auto.road", System.StringComparison.Ordinal)))
            {
                var o = Reg.Create(item, Vec2.Zero);
                Assert.True(o.Size.W <= 140 && o.Size.H <= 140, $"{item.Id}: {o.Size.W} x {o.Size.H} mm");
            }
        }

        [Fact]
        public void AnUnscaledRoadGetsALibrarySizedSegment()
        {
            var (doc, road) = One("auto.road.2", new Vec2(200, 200));
            var port = Building.Ports(Reg, doc, road.Id).First(p => p.Kind == PortKind.AxisEnd);
            var placed = Building.Place(Reg, doc, road.Id, port, Reg.Item("auto.road.2")!)!;
            Assert.Equal(road.Size, placed.Size);
        }

        [Fact]
        public void AScaledGearGetsAMatingGearOfTheSameScale()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial");
            var (doc, gear) = One(item.Id, new Vec2(200, 200), 2, 2);
            var port = Building.Ports(Reg, doc, gear.Id).First(p => p.Kind == PortKind.Mesh);
            var placed = Building.Place(Reg, doc, gear.Id, port, item)!;
            var reference = Reg.Create(item, Vec2.Zero).Size;
            Assert.Equal(reference.W * 2, placed.Size.W, 6);
            // Still in mesh: centre distance = sum of the pitch radii.
            var after = Edits.Add(doc, doc.Pages[0].Id, placed);
            double Pitch(SketchObject o) => Reg.PageAnchors(o, new RenderContext(after)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch).Radius;
            var d = new Vec2(gear.Transform.X, gear.Transform.Y).DistanceTo(new Vec2(placed.Transform.X, placed.Transform.Y));
            Assert.Equal(Pitch(gear) + Pitch(placed), d, 3);
        }
    }
}
