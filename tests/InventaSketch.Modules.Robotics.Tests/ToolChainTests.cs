using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    /// <summary>
    /// Docking chains at the flange (arm → tool changer → force-torque sensor → gripper), robots on carriers (mobile robot,
    /// linear axis), the energy chain and the reach envelope swept along a linear axis.
    /// </summary>
    public class ToolChainTests
    {
        private static Anchor MountOf(Registry r, SketchDocument doc, SketchObject o, string id)
            => r.PageAnchors(o, new RenderContext(doc).For(r)).Single(a => a.Role == AnchorRole.Mount && a.Id == id);

        private static SketchObject Side(Registry r, string typeId, Vec2 at)
            => r.Create(r.Library.First(i => i.TypeId == typeId && (i.View is null || i.View == "side")), at);

        /// <summary>Places the library item <paramref name="typeId"/> at the single free mount port of <paramref name="id"/>.</summary>
        private static (SketchDocument Doc, SketchObject Placed) Build(Registry r, SketchDocument doc, string id, string portId, string typeId)
        {
            var port = Building.Ports(r, doc, id).Single(p => p.Kind == PortKind.Mount && p.Id == portId);
            var item = Assert.Single(Building.Items(r, doc, id, port), i => i.TypeId == typeId);
            var placed = Building.Place(r, doc, id, port, item)!;
            Assert.NotNull(placed);
            return (Edits.Add(doc, doc.Pages[0].Id, placed), placed);
        }

        /// <summary>Arm with changer, sensor and parallel gripper, each placed with "+" on the previous part.</summary>
        internal static (SketchDocument Doc, SketchObject Arm, SketchObject Changer, SketchObject Sensor, SketchObject Gripper) Chain(Registry r, double j5 = -30)
        {
            var arm = Side(r, "robot.arm.articulated", new Vec2(100, 100)).WithParam("j5", j5);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, arm);
            var (d1, changer) = Build(r, doc, arm.Id, "mount.flange", "robot.tool.changer");
            var (d2, sensor) = Build(r, d1, changer.Id, "mount.tool", "robot.sensor.ft");
            var (d3, gripper) = Build(r, d2, sensor.Id, "mount.tool", "robot.gripper.parallel");
            return (d3, arm, changer, sensor, gripper);
        }

        private static void AssertDocked(Registry r, SketchDocument doc, SketchObject host, string hostMount, SketchObject part, double tol = 1e-6)
        {
            var h = MountOf(r, doc, doc.Get(host.Id)!, hostMount);
            var m = MountOf(r, doc, doc.Get(part.Id)!, "flange");
            Assert.True(h.P.DistanceTo(m.P) < tol, $"{part.Type} sits on {host.Type}.{hostMount}: {m.P} vs {h.P}");
            Assert.True((h.Direction!.Value + m.Direction!.Value).Length < tol, $"{part.Type} faces {host.Type}.{hostMount}");
        }

        [Fact]
        public void TheChainIsBuiltWithPlusAtEachToolSideMount()
        {
            var r = Fx.Reg();
            var (doc, arm, changer, sensor, gripper) = Chain(r);
            AssertDocked(r, doc, arm, "flange", changer);
            AssertDocked(r, doc, changer, "tool", sensor);
            AssertDocked(r, doc, sensor, "tool", gripper);
            // Every face is scaled to the robot's flange: the gripper as wide as on the bare flange.
            var flange = MountOf(r, doc, arm, "flange");
            Assert.Equal(flange.Radius, MountOf(r, doc, doc.Get(gripper.Id)!, "flange").Radius, 6);
            // Taken mounts offer no "+" any more; the gripper's TCP is the end of the chain.
            Assert.DoesNotContain(Building.Ports(r, doc, arm.Id), p => p.Kind == PortKind.Mount);
            Assert.DoesNotContain(Building.Ports(r, doc, changer.Id), p => p.Kind == PortKind.Mount);
            Assert.DoesNotContain(Building.Ports(r, doc, sensor.Id), p => p.Kind == PortKind.Mount);
        }

        [Fact]
        public void MountIdsAreUniqueInTheDocumentAndAFreeChangerOffersItsToolSide()
        {
            var r = Fx.Reg();
            var changer = Side(r, "robot.tool.changer", new Vec2(50, 50));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, changer);
            var mounts = r.PageAnchors(changer, new RenderContext(doc).For(r)).Where(a => a.Role == AnchorRole.Mount && a.Id is not null).Select(a => a.Id).ToList();
            Assert.Equal(mounts.Distinct().Count(), mounts.Count);
            Assert.Equal(new[] { "flange", "tool" }, mounts);
            var ports = Building.Ports(r, doc, changer.Id).Where(p => p.Kind == PortKind.Mount).Select(p => p.Id).ToList();
            Assert.Equal(ports.Distinct().Count(), ports.Count);
            Assert.Contains("mount.tool", ports);
            // A tool-side part keeps the single mount id "flange" wherever it is; that mount accepts the tool side
            // (Anchor.Accepts), so "+" on a tool side finds grippers, sensors and changers.
            var free = Side(r, "robot.gripper.parallel", new Vec2(150, 50));
            var withFree = Edits.Add(doc, doc.Pages[0].Id, free);
            var freeMounts = r.PageAnchors(free, new RenderContext(withFree).For(r)).Where(a => a.Role == AnchorRole.Mount && a.Id is not null).ToList();
            Assert.Equal("flange", Assert.Single(freeMounts).Id);
            Assert.True(freeMounts[0].DocksOn("tool"));
            Assert.Single(Building.Ports(r, withFree, free.Id), p => p.Kind == PortKind.Mount);
            var port = Building.Ports(r, doc, changer.Id).Single(p => p.Id == "mount.tool");
            var items = Building.Items(r, doc, changer.Id, port, 20).Select(i => i.TypeId).ToList();
            Assert.Contains("robot.gripper.parallel", items);
            Assert.Contains("robot.sensor.ft", items);
            Assert.Contains("robot.tool.changer", items);
        }

        [Fact]
        public void AGripperDraggedNearAToolSideSnapsItsFlangeOntoIt()
        {
            var r = Fx.Reg();
            var (doc0, _, _, sensor, gripper) = Chain(r);
            // Without the gripper: drag a new gripper next to the sensor's tool side.
            var doc = Edits.Remove(doc0, new[] { gripper.Id });
            var tool = MountOf(r, doc, doc.Get(sensor.Id)!, "tool");
            var item = r.Library.First(i => i.TypeId == "robot.gripper.parallel" && (i.View is null || i.View == "side"));
            var placed = Building.Place(r, doc, sensor.Id, Building.Ports(r, doc, sensor.Id).Single(p => p.Id == "mount.tool"), item)!;
            // Same pose as "+" would give, but away from the sensor; then dragged back with a small offset.
            var away = placed with { Transform = placed.Transform with { X = placed.Transform.X + 200, Y = placed.Transform.Y + 200 } };
            doc = Edits.Add(doc, doc.Pages[0].Id, away);
            var ctx = new RenderContext(doc).For(r);
            var moving = r.PageAnchors(away, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { away.Id }, ctx, new SnapOptions { Grid = null, Alignment = false, Page = false });
            var snap = Snapper.SnapMove(moving, new Vec2(-200.4, -199.7), targets, new SnapOptions { Grid = null, Alignment = false, Page = false, Tolerance = 3 });
            Assert.Equal("mount", snap.Kind);
            Assert.Equal("tool", snap.Target?.Id);
            Assert.Equal(sensor.Id, snap.Target?.Owner);
            var flange = moving.Single(a => a.Role == AnchorRole.Mount && a.Id == "flange");
            Assert.True((flange.P + snap.Delta).DistanceTo(tool.P) < 1e-9);
        }

        [Fact]
        public void TheWholeChainFollowsWhenAJointTurns()
        {
            var r = Fx.Reg();
            var (doc, arm, changer, sensor, gripper) = Chain(r, -30);
            var after = Edits.Update(doc, new[] { arm.Id }, o => o.WithParam("j5", 10.0));
            var result = Coupling.Propagate(r, doc, after);
            AssertDocked(r, result, arm, "flange", changer, 1e-3);
            AssertDocked(r, result, changer, "tool", sensor, 1e-3);
            AssertDocked(r, result, sensor, "tool", gripper, 1e-3);
            Assert.NotEqual(gripper.Transform.Rotation, result.Get(gripper.Id)!.Transform.Rotation);
        }

        [Fact]
        public void TheChainIsFoundInOrderWithTheWorkpieceAtItsTcp()
        {
            var r = Fx.Reg();
            var (doc, arm, changer, sensor, gripper) = Chain(r);
            var ctx = new RenderContext(doc).For(r);
            var tcp = r.PageAnchors(doc.Get(gripper.Id)!, ctx).Single(a => a.Role == AnchorRole.Port && a.Id == "tcp").P;
            var wp = r.WithView(Fx.Item(r, "robot.cell.workpiece"), "side", Fx.Ctx());
            var grip = r.PageAnchors(wp, Fx.Ctx()).Single(a => a.Id == "grip").P;
            wp = wp with { Transform = wp.Transform with { X = wp.Transform.X + tcp.X - grip.X, Y = wp.Transform.Y + tcp.Y - grip.Y } };
            doc = Edits.Add(doc, doc.Pages[0].Id, wp);
            var chain = RoboValues.Chain(r, doc, doc.Get(arm.Id)!, new RenderContext(doc).For(r));
            Assert.Equal(new[] { changer.Id, sensor.Id, gripper.Id }, chain.Parts.Select(p => p.Id));
            Assert.Equal(wp.Id, chain.Workpiece?.Id);
            Assert.True(chain.Tcp!.Value.DistanceTo(tcp) < 1e-9);
        }

        [Fact]
        public void ACobotBaseDocksOnTheMobileRobotAndRidesOnIt()
        {
            var r = Fx.Reg();
            foreach (var view in new[] { "side", "top" })
            {
                var agv = r.WithView(Fx.Item(r, "robot.mobile.agv", new Vec2(100, 100)), view, Fx.Ctx());
                var cobot = r.WithView(Fx.Item(r, "robot.arm.cobot", new Vec2(100, 60)), view, Fx.Ctx());
                var doc = SketchDocument.Create();
                doc = Edits.Add(doc, doc.Pages[0].Id, agv, cobot);
                var ctx = new RenderContext(doc).For(r);
                var deck = r.PageAnchors(agv, ctx).Single(a => a.Id == "deck").P;
                var basePt = r.PageAnchors(cobot, ctx).Single(a => a.Id == "base").P;
                // Drag the cobot near the deck: the base snaps onto it (mount onto mount).
                var moving = r.PageAnchors(cobot, ctx).ToList();
                var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { cobot.Id }, ctx, new SnapOptions { Grid = null, Alignment = false, Page = false });
                var raw = deck - basePt + new Vec2(0.6, -0.5);
                var snap = Snapper.SnapMove(moving, raw, targets, new SnapOptions { Grid = null, Alignment = false, Page = false, Tolerance = 2 });
                Assert.Equal("mount", snap.Kind);
                Assert.True((basePt + snap.Delta).DistanceTo(deck) < 1e-9, view + ": base on the deck");
                doc = Edits.Move(doc, new[] { cobot.Id }, snap.Delta);

                // The mobile robot drives (and turns): the cobot stays on its deck.
                var after = Edits.Move(doc, new[] { agv.Id }, new Vec2(30, 12));
                var moved = Coupling.Propagate(r, doc, after);
                var ctx2 = new RenderContext(moved).For(r);
                var deck2 = r.PageAnchors(moved.Get(agv.Id)!, ctx2).Single(a => a.Id == "deck").P;
                var base2 = r.PageAnchors(moved.Get(cobot.Id)!, ctx2).Single(a => a.Id == "base").P;
                Assert.True(deck2.DistanceTo(base2) < 1e-3, view + ": the cobot rides on the mobile robot");
                Assert.True(deck2.DistanceTo(deck) > 10);
            }
        }

        [Fact]
        public void ARobotOnTheLinearAxisMovesWithTheCarriage()
        {
            var r = Fx.Reg();
            foreach (var view in new[] { "side", "top" })
            {
                var track = r.WithView(Fx.Item(r, Track.TrackId, new Vec2(100, 120)), view, Fx.Ctx());
                var arm = r.WithView(Fx.Item(r, "robot.arm.articulated", new Vec2(80, 80)), view, Fx.Ctx());
                var doc = SketchDocument.Create();
                doc = Edits.Add(doc, doc.Pages[0].Id, track, arm);
                var ctx = new RenderContext(doc).For(r);
                var carriage = r.PageAnchors(track, ctx).Single(a => a.Id == "carriage").P;
                var basePt = r.PageAnchors(arm, ctx).Single(a => a.Id == "base").P;
                doc = Edits.Move(doc, new[] { arm.Id }, carriage - basePt);

                // The carriage is dragged to the end of the travel: the robot rides along.
                var h = r.DragHandles(track, ctx).Single(x => x.Key == "pos");
                Assert.Equal(DragConstraint.Horizontal, h.Constraint);
                Assert.Equal("Carriage position: 25 %", h.Label.En);
                Assert.Equal("Schlittenposition: 25 %", h.Label.De);
                var end = r.MoveHandle(track, "pos", h.Position + new Vec2(500, 0), ctx);
                Assert.Equal(1.0, end.Num("pos", 0), 9);
                var after = Edits.Replace(doc, end);
                var moved = Coupling.Propagate(r, doc, after);
                var ctx2 = new RenderContext(moved).For(r);
                var c2 = r.PageAnchors(moved.Get(track.Id)!, ctx2).Single(a => a.Id == "carriage").P;
                var b2 = r.PageAnchors(moved.Get(arm.Id)!, ctx2).Single(a => a.Id == "base").P;
                Assert.True(c2.DistanceTo(carriage) > 10, view + ": the carriage moved");
                Assert.True(c2.DistanceTo(b2) < 1e-3, view + ": the robot rides on the carriage");
            }
        }

        [Fact]
        public void TheEnergyChainRoundsItsCornersWithTheBendRadius()
        {
            var r = Fx.Reg();
            var chain = Fx.Item(r, "robot.cable.dragchain", new Vec2(50, 50));
            var pts = Fx.Ctx().Doc.ResolvePoints(chain);
            var path = DragChainType.Fillet(pts, 4);
            // The default U: two quarter arcs of radius 4 make a semicircle around (12, 0) + offset.
            var centre = new Vec2((pts[1].X + pts[2].X) / 2 - 4, (pts[1].Y + pts[2].Y) / 2);
            var arc = path.Where(p => p.X > pts[1].X - 4 + 1e-6).ToList();
            Assert.NotEmpty(arc);
            Assert.All(arc, p => Assert.Equal(4, p.DistanceTo(centre), 6));
            var prims = r.TypeOf(chain).Render(chain, Fx.Ctx());
            Assert.Single(prims, p => p is PathPrim pp && pp.IsClosed && pp.Tag == RoboTags.Chain);
            Assert.True(prims.Count(p => p.Tag == RoboTags.Chain) > 20, "links along the chain");
            Assert.Equal(2, prims.Count(p => p.Tag == RoboTags.Base));
        }

        [Fact]
        public void AnEnergyChainEndDocksOnTheCarriageAndFollowsIt()
        {
            var r = Fx.Reg();
            var track = Fx.Item(r, Track.TrackId, new Vec2(100, 120));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, track);
            var port = r.PageAnchors(track, new RenderContext(doc).For(r)).Single(a => a.Role == AnchorRole.Port && a.Id == "chain");
            var chain = Fx.Item(r, "robot.cable.dragchain", new Vec2(100, 140));
            doc = Edits.Add(doc, doc.Pages[0].Id, chain);
            var last = chain.Points!.Count - 1;
            doc = Edits.SetPoint(doc, chain.Id, last, port.P, Attachments.For(track, port.P, port with { Owner = track.Id }));
            // The port moves inside the box: the attachment names it, so the refresh after the edit re-resolves it.
            var moved = Attachments.Refresh(r, Edits.Replace(doc, doc.Get(track.Id)!.WithParam("pos", 0.9)));
            var port2 = r.PageAnchors(moved.Get(track.Id)!, new RenderContext(moved).For(r)).Single(a => a.Role == AnchorRole.Port && a.Id == "chain").P;
            Assert.True(moved.ResolvePoints(moved.Get(chain.Id)!)[last].DistanceTo(port2) < 1e-6);
            Assert.True(port2.DistanceTo(port.P) > 10);
        }

        [Fact]
        public void TheReachEnvelopeIsSweptAlongTheTravel()
        {
            var r = Fx.Reg();
            var env = r.WithView(Fx.Item(r, "robot.reach.envelope"), "top", Fx.Ctx());
            var top = ArticulatedArmType.TopRegion(env);
            var swept = ReachEnvelopeType.Sweep(top, 2000, true);
            var b0 = Rect.FromPoints(top.Fill);
            var b1 = Rect.FromPoints(swept.Fill);
            Assert.Equal(b0.W + 2000, b1.W, 6);
            Assert.Equal(b0.H, b1.H, 6);
            var side = ArticulatedArmType.SideRegion(env);
            var sideSwept = ReachEnvelopeType.Sweep(side, 1500, false);
            Assert.Equal(Rect.FromPoints(side.Fill).W + 1500, Rect.FromPoints(sideSwept.Fill).W, 0);
            // The library preset draws inside its box.
            var preset = Fx.Item(r, "robot.reach.envelope.track");
            Fx.AssertInsideBox(r.TypeOf(preset).Render(preset, Fx.Ctx()), preset.Size, "swept envelope");
            Assert.True(preset.Size.W > preset.Size.H);
        }
    }
}
