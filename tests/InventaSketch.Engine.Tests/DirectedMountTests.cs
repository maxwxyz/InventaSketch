using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Directed mounts (<see cref="Anchor.Directed"/>): a tool docked on a robot flange with "+" faces along the tool axis.</summary>
    public class DirectedMountTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Theory]
        [InlineData(0.0)]
        [InlineData(-60.0)]
        public void AGripperOnTheFlangeFacesAlongTheToolAxis(double j5)
        {
            var robot = Reg.Create(Reg.Library.First(i => i.TypeId == "robot.arm.articulated" && i.View == "side"), new Vec2(100, 100)).WithParam("j5", j5);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, robot);
            var ctx = new RenderContext(doc).For(Reg);
            var flange = Reg.PageAnchors(robot, ctx).Single(a => a.Role == AnchorRole.Mount && a.Id == "flange");
            Assert.NotNull(flange.Direction);

            var port = Assert.Single(Building.Ports(Reg, doc, robot.Id), p => p.Kind == PortKind.Mount);
            Assert.Equal("mount.flange", port.Id);
            var items = Building.Items(Reg, doc, robot.Id, port);
            var gripper = Assert.Single(items, i => i.TypeId == "robot.gripper.parallel");
            Assert.DoesNotContain(items, i => i.TypeId.StartsWith("mech.", System.StringComparison.Ordinal));

            var placed = Building.Place(Reg, doc, robot.Id, port, gripper)!;
            var withTool = Edits.Add(doc, doc.Pages[0].Id, placed);
            var mount = Reg.PageAnchors(placed, new RenderContext(withTool).For(Reg)).Single(a => a.Role == AnchorRole.Mount && a.Id == "flange");
            Assert.True(mount.P.DistanceTo(flange.P) < 1e-6, "the gripper's mount sits on the flange");
            Assert.True((mount.Direction!.Value + flange.Direction!.Value).Length < 1e-6, "the gripper faces the flange (points along the tool axis)");
            // The flange is taken now.
            Assert.DoesNotContain(Building.Ports(Reg, withTool, robot.Id), p => p.Kind == PortKind.Mount);
            // The gripper is scaled so its flange plate is as wide as the robot's flange.
            Assert.Equal(flange.Radius, mount.Radius, 3);
        }

        [Fact]
        public void ADockedGripperFollowsTheFlangeWhenAJointTurns()
        {
            var robot = Reg.Create(Reg.Library.First(i => i.TypeId == "robot.arm.articulated" && i.View == "side"), new Vec2(100, 100));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, robot);
            var port = Building.Ports(Reg, doc, robot.Id).Single(p => p.Kind == PortKind.Mount);
            var gripper = Building.Place(Reg, doc, robot.Id, port, Reg.Library.First(i => i.TypeId == "robot.gripper.parallel" && i.View == "side"))!;
            doc = Edits.Add(doc, doc.Pages[0].Id, gripper);

            var after = Edits.Update(doc, new[] { robot.Id }, o => o.WithParam("j5", -50.0));
            var result = Coupling.Propagate(Reg, doc, after);
            var ctx = new RenderContext(result).For(Reg);
            var flange = Reg.PageAnchors(result.Get(robot.Id)!, ctx).Single(a => a.Role == AnchorRole.Mount && a.Id == "flange");
            var mount = Reg.PageAnchors(result.Get(gripper.Id)!, ctx).Single(a => a.Role == AnchorRole.Mount && a.Id == "flange");
            Assert.True(mount.P.DistanceTo(flange.P) < 1e-3, "the gripper stays on the flange");
            Assert.True((mount.Direction!.Value + flange.Direction!.Value).Length < 1e-3, "the gripper turns with the tool axis");
            Assert.NotEqual(gripper.Transform.Rotation, result.Get(gripper.Id)!.Transform.Rotation);
        }

        [Fact]
        public void AnUndirectedMountTransformsAsBefore()
        {
            var a = Anchor.Point(AnchorRole.Mount, new Vec2(1, 2), "m");
            var m = Matrix2D.Translation(10, 0) * Matrix2D.Rotation(System.Math.PI / 2);
            Assert.Null(a.Transform(m).Direction);
            var d = Anchor.Directed(AnchorRole.Mount, new Vec2(0, 0), new Vec2(1, 0), "m").Transform(m);
            Assert.True(d.Direction!.Value.DistanceTo(new Vec2(0, 1)) < 1e-9);
        }
    }
}
