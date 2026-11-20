using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>Rails on wires and buses, pin priority on tiny symbols and pin-id attachments.</summary>
    public class SnappingTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        private static void Near(Vec2 expected, Vec2 actual) => Assert.True(expected.DistanceTo(actual) < 1e-9, $"expected {expected}, got {actual}");

        private static SketchObject Wire(Registry r, params Vec2[] points)
            => r.Create(r.Item("elec.wire.free")!, Vec2.Zero) with { Points = points };

        [Fact]
        public void WiresAndBusesExposeRailsAlongTheirPath()
        {
            var r = Fx.Reg();
            var wire = r.Create(r.Item("elec.wire")!, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(20, 15) } };
            var (doc, _) = Fx.Doc(wire);
            var rails = r.PageAnchors(wire, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Rail).ToList();
            // Orthogonal routing: the rails follow the routed path (three legs), not the straight chord.
            Assert.Equal(3, rails.Count);
            Assert.All(rails, a => Assert.True(Math.Abs(a.P.X - a.Q.X) < 1e-9 || Math.Abs(a.P.Y - a.Q.Y) < 1e-9));
            Assert.Equal(new[] { "rail0", "rail1", "rail2" }, rails.Select(a => a.Id));

            var bus = r.Create(r.Item("elec.bus.can")!, new Vec2(100, 50));
            (doc, _) = Fx.Doc(bus);
            var anchors = r.PageAnchors(bus, new RenderContext(doc)).ToList();
            Assert.Single(anchors, a => a.Role == AnchorRole.Rail);
            Assert.Equal(7, anchors.Count(a => a.Role == AnchorRole.Port)); // the taps stay
        }

        [Fact]
        public void WireEndDocksAnywhereOnAnotherWire()
        {
            var r = Fx.Reg();
            var main = Wire(r, new Vec2(100, 100), new Vec2(140, 100));
            var branch = Wire(r, new Vec2(121, 130), new Vec2(121, 110));
            var (doc, _) = Fx.Doc(main, branch);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { branch.Id }, new RenderContext(doc), Opt);
            var drag = new Vec2(121.3, 101.4);
            var res = Snapper.SnapPoint(drag, targets, Opt);
            Assert.Equal("rail", res.Kind);
            Near(new Vec2(121.3, 100), drag + res.Delta);
            Assert.Equal(main.Id, res.Target!.Owner);

            // Near the end of the other wire it docks exactly on that end.
            var nearEnd = new Vec2(139.4, 100.8);
            Near(new Vec2(140, 100), nearEnd + Snapper.SnapPoint(nearEnd, targets, Opt).Delta);
        }

        [Fact]
        public void JunctionDotDocksOntoTheMiddleOfAWire()
        {
            var r = Fx.Reg();
            var wire = Wire(r, new Vec2(100, 100), new Vec2(140, 100));
            var dot = r.Create(r.Item("elec.junction")!, new Vec2(117.6, 98.9));
            var (doc, _) = Fx.Doc(wire, dot);
            var ctx = new RenderContext(doc);
            var res = Snapper.SnapMove(r.PageAnchors(dot, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { dot.Id }, ctx, Opt), Opt);
            Assert.Equal("rail", res.Kind);
            Near(new Vec2(117.6, 100), new Vec2(117.6, 98.9) + res.Delta);
        }

        [Fact]
        public void JunctionDotPinBeatsItsCloserCorner()
        {
            var r = Fx.Reg();
            // Wire end at (85, 100); the dot's corner (85.2, 100.1) is much closer to it than the dot's pin (86.2, 101.1).
            var wire = r.Create(r.Item("elec.wire")!, new Vec2(100, 100));
            var dot = r.Create(r.Item("elec.junction")!, new Vec2(86.2, 101.1));
            var (doc, _) = Fx.Doc(wire, dot);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(dot, ctx).ToList();
            Assert.Contains(moving, a => a.Role == AnchorRole.Corner && a.P.DistanceTo(new Vec2(85.2, 100.1)) < 1e-9);
            var res = Snapper.SnapMove(moving, Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { dot.Id }, ctx, Opt), Opt);
            Assert.NotEqual("point", res.Kind);
            Near(new Vec2(85, 100), new Vec2(86.2, 101.1) + res.Delta);

            // And a wire end dragged near a placed dot lands on its pin, not on a corner.
            var placed = r.Create(r.Item("elec.junction")!, new Vec2(50, 50));
            (doc, _) = Fx.Doc(placed, wire);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { wire.Id }, new RenderContext(doc), Opt);
            var p = new Vec2(51.1, 51.2);
            var snap = Snapper.SnapPoint(p, targets, Opt);
            Assert.Equal("port", snap.Kind);
            Near(new Vec2(50, 50), p + snap.Delta);
        }

        [Fact]
        public void ConnectToPinStoresThePinIdAndFollowsAPolarityFlip()
        {
            var r = Fx.Reg();
            var diode = r.Create(r.Item("elec.diode")!, new Vec2(50, 50));
            var wire = r.Create(r.Item("elec.wire")!, Vec2.Zero) with { Points = new[] { new Vec2(39.5, 50.5), new Vec2(20, 70) } };
            var (doc, _) = Fx.Doc(diode, wire);
            var anode = Fx.Port(r, doc, diode, "A").P;
            doc = ElectricalModule.ConnectToPin(r, doc, wire.Id, 0);
            var att = doc.Get(wire.Id)!.Attach![0]!;
            Assert.Equal("A", att.AnchorId);
            Near(anode, doc.ResolvePoints(doc.Get(wire.Id)!)[0]);

            var flip = r.TypeOf(diode).Actions(diode, Lang.En).Single(a => a.Id == "elec.polarity");
            var flipped = Edits.Update(doc, new[] { diode.Id }, flip.Apply);
            var movedAnode = Fx.Port(r, flipped, flipped.Get(diode.Id)!, "A").P;
            Assert.True(movedAnode.DistanceTo(anode) > 10, "the anode pin moved to the other side");
            var refreshed = Attachments.Refresh(r, flipped);
            Near(movedAnode, refreshed.ResolvePoints(refreshed.Get(wire.Id)!)[0]);
            Assert.Equal("A", refreshed.Get(wire.Id)!.Attach![0]!.AnchorId);

            // Still attached: moving the diode drags the wire end along.
            var moved = Edits.Move(refreshed, new[] { diode.Id }, new Vec2(5, 5));
            Near(movedAnode + new Vec2(5, 5), moved.ResolvePoints(moved.Get(wire.Id)!)[0]);
        }

        [Fact]
        public void PinIdsAreUniqueAndStablePerSymbol()
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = r.Create(item, new Vec2(50, 50)) with { View = view };
                var ids = type.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToList();
                Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id), item.Id + " has a pin without id"));
                Assert.Equal(ids.Count, ids.Distinct().Count());
                // Parameter changes that only move pins keep their ids.
                if (type.Parameters.Any(p => p.Key == "reverse"))
                    Assert.Equal(ids, type.Anchors(o.WithParam("reverse", true), Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id));
            }
        }
    }
}
