using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    /// <summary>Grips of robots and grippers (<see cref="ObjectType.DragHandles"/>): joint angles, linear axes, jaw opening.</summary>
    public class GripTests
    {
        private static void Near(double expected, double actual, double tol = 1e-9, string what = "")
            => Assert.True(Math.Abs(expected - actual) <= tol, $"{what}: expected {expected}, got {actual}");

        /// <summary>Page position of a schematic joint's centre.</summary>
        private static Vec2 JointCentre(RobotArmType t, SketchObject o, string key)
        {
            var s = t.SceneFor(o, out var pose, out _);
            return o.Matrix().Apply(s.L(Proj.To2(pose.Joints.Single(j => j.Key == key).P, t.ViewOf(o))));
        }

        private static Vec2 Turn(Vec2 p, Vec2 c, double deg)
        {
            var a = Geo.DegToRad(deg);
            var d = p - c;
            return c + new Vec2(d.X * Math.Cos(a) - d.Y * Math.Sin(a), d.X * Math.Sin(a) + d.Y * Math.Cos(a));
        }

        private static double Angle(Vec2 v) => Geo.RadToDeg(Math.Atan2(v.Y, v.X));

        /// <summary>Dragging the grip by <paramref name="deg"/> about the joint turns the joint by that much, the grip follows.</summary>
        private static void AssertTurns(Registry r, RobotArmType t, SketchObject o, string joint, double deg)
        {
            var h = r.DragHandles(o, Fx.Ctx()).Single(x => x.Key == "joint." + joint);
            Assert.Equal(DragConstraint.Free, h.Constraint);
            var c = JointCentre(t, o, joint);
            var target = Turn(h.Position, c, deg);
            var moved = r.MoveHandle(o, "joint." + joint, target, Fx.Ctx());
            var before = o.Num(joint, t.Joints.Single(j => j.Key == joint).Default);
            Near(Math.Abs(deg), Math.Abs(moved.Num(joint, 0) - before), 1e-9, joint + " turned");
            // The grip lies in the pointer's direction again (the joint centre stays on the page: the base keeps its place).
            var after = r.DragHandles(moved, Fx.Ctx()).Single(x => x.Key == "joint." + joint);
            var c2 = JointCentre(t, moved, joint);
            Assert.True(c2.DistanceTo(c) < 1e-6, $"{joint} centre moved from {c} to {c2}");
            var diff = Math.Abs(Rb.NormAngle(Angle(after.Position - c) - Angle(target - c)));
            Assert.True(diff < 0.6, $"{joint}: grip at {Angle(after.Position - c)}°, pointer at {Angle(target - c)}°");
        }

        [Fact]
        public void ArticulatedArmJointsAreTurnedByTheirGrips()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.articulated");
            var side = Fx.Item(r, "robot.arm.articulated", new Vec2(150, 100));
            var keys = r.DragHandles(side, Fx.Ctx()).Select(h => h.Key).ToList();
            // Side view: shoulder and elbow turn in the drawing plane; base, forearm roll and flange roll do not show.
            Assert.Contains("joint.j2", keys);
            Assert.Contains("joint.j3", keys);
            Assert.DoesNotContain("joint.j1", keys);
            Assert.DoesNotContain("joint.j4", keys);
            Assert.DoesNotContain("joint.j6", keys);
            var j2 = r.DragHandles(side, Fx.Ctx()).Single(h => h.Key == "joint.j2");
            Assert.Equal("Joint angle J2: 15°", j2.Label.En);
            Assert.Equal("Gelenkwinkel A2: 15°", j2.Label.De);
            AssertTurns(r, t, side, "j2", 20);
            AssertTurns(r, t, side, "j3", -30);

            // Top view: the base axis faces the viewer.
            var top = r.WithView(side, RoboViews.Top, Fx.Ctx());
            Assert.Contains("joint.j1", r.DragHandles(top, Fx.Ctx()).Select(h => h.Key));
            AssertTurns(r, t, top, "j1", 40);

            Assert.Same(side, r.MoveHandle(side, "joint.j9", new Vec2(0, 0), Fx.Ctx()));
            Assert.Same(side, r.MoveHandle(side, "joint.j1", new Vec2(0, 0), Fx.Ctx())); // no grip in this view
        }

        [Fact]
        public void JointGripsSnapToWholeDegreesAndStayWithinTheLimits()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.articulated");
            var o = Fx.Item(r, "robot.arm.articulated", new Vec2(150, 100));
            var h = r.DragHandles(o, Fx.Ctx()).Single(x => x.Key == "joint.j2");
            var c = JointCentre(t, o, "j2");
            var v = r.MoveHandle(o, "joint.j2", Turn(h.Position, c, 12.4), Fx.Ctx()).Num("j2", 0);
            Near(Math.Round(v), v, 1e-9, "whole degrees");
            Near(12, Math.Abs(v - 15), 1e-9, "12.4° → 12°");
            // Half a turn either way ends at a limit of the shoulder (−70° … 110°).
            foreach (var deg in new[] { 170.0, -170.0 })
            {
                var w = r.MoveHandle(o, "joint.j2", Turn(h.Position, c, deg), Fx.Ctx()).Num("j2", 0);
                Assert.True(w >= -70 && w <= 110, $"{deg}: {w}");
            }
        }

        [Fact]
        public void ScaraJointsTurnInTheTopView()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.scara");
            var o = Fx.Item(r, "robot.arm.scara", new Vec2(150, 100));
            if (t.ViewOf(o) != RoboViews.Top) o = r.WithView(o, RoboViews.Top, Fx.Ctx());
            var keys = r.DragHandles(o, Fx.Ctx()).Select(h => h.Key).ToList();
            Assert.Contains("joint.j1", keys);
            Assert.Contains("joint.j2", keys);
            Assert.DoesNotContain("joint.j3", keys); // the quill moves along the line of sight
            AssertTurns(r, t, o, "j1", 25);
            AssertTurns(r, t, o, "j2", -35);
        }

        [Fact]
        public void GantryAxisIsDraggedAlongItsDirection()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.gantry");
            var o = Fx.Item(r, "robot.arm.gantry", new Vec2(150, 100));
            var h = r.DragHandles(o, Fx.Ctx()).FirstOrDefault(x => x.Key == "joint.x")
                ?? r.DragHandles(o = r.WithView(o, RoboViews.Side, Fx.Ctx()), Fx.Ctx()).Single(x => x.Key == "joint.x");
            Assert.Equal(DragConstraint.Horizontal, h.Constraint);
            Near(1, Math.Abs(h.Direction.X), 1e-9, "along x");
            Assert.Equal("Axis position X: 600 mm", h.Label.En);
            Assert.Equal("Achsstellung X: 600 mm", h.Label.De);
            // 5 page mm along the axis = 5 / S model mm (S = page mm per model mm), snapped to 5 mm.
            var s = t.SceneFor(o, out _, out _);
            var sign = Math.Sign(h.Direction.X);
            var moved = r.MoveHandle(o, "joint.x", h.Position + new Vec2(5 * sign, 4), Fx.Ctx());
            Near(Dragging.Snap(600 + 5 / s.S, 5), moved.Num("x", 0), 1e-9, "x");
            // Far out: the stroke ends the travel (X: 0 … 1200 mm).
            Near(1200, r.MoveHandle(o, "joint.x", h.Position + new Vec2(300 * sign, 0), Fx.Ctx()).Num("x", 0), 1e-9, "stroke end");
            Assert.Same(o, r.MoveHandle(o, "joint.q", h.Position, Fx.Ctx()));
        }

        [Fact]
        public void GripperOpeningIsDraggedOnAJaw()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "robot.gripper.parallel", new Vec2(100, 100));
            // Default box = design box (20 × 22): local = design; right jaw finger at x = 1.9 + 5.4 · 0.6.
            var h = r.DragHandles(o, Fx.Ctx()).Single();
            Assert.Equal("open", h.Key);
            Assert.True(h.Position.DistanceTo(o.Matrix().Apply(new Vec2(1.9 + 5.4 * 0.6, 9.5))) < 1e-9, h.Position.ToString());
            Assert.Equal(DragConstraint.Horizontal, h.Constraint);
            Assert.Equal("Gripper opening: 60 %", h.Label.En);
            Assert.Equal("Greiferöffnung: 60 %", h.Label.De);
            Near(0.25, r.MoveHandle(o, "open", o.Matrix().Apply(new Vec2(1.9 + 5.4 * 0.25, 9)), Fx.Ctx()).Num("open", 0));
            Near(0, r.MoveHandle(o, "open", o.Matrix().Apply(new Vec2(-30, 9)), Fx.Ctx()).Num("open", 1));
            Assert.Same(o, r.MoveHandle(o, "close", h.Position, Fx.Ctx()));

            // Three-finger gripper seen along the tool axis: the upper finger moves up as it opens.
            var three = r.WithView(Fx.Item(r, "robot.gripper.threefinger", new Vec2(100, 100)), RoboViews.Axial, Fx.Ctx());
            var g = r.DragHandles(three, Fx.Ctx()).Single();
            Assert.Equal(DragConstraint.Vertical, g.Constraint);
            var k = three.Size.H / 20; // design 20 × 20
            Assert.True(g.Position.DistanceTo(three.Matrix().Apply(new Vec2(0, (-3.5 - 3.4 * 0.6) * k))) < 1e-9, g.Position.ToString());
            Near(0.9, r.MoveHandle(three, "open", three.Matrix().Apply(new Vec2(0, (-3.5 - 3.4 * 0.9) * k)), Fx.Ctx()).Num("open", 0));
            // Grippers without an opening have no grip.
            Assert.Empty(r.DragHandles(Fx.Item(r, "robot.gripper.vacuum"), Fx.Ctx()));
        }

        [Fact]
        public void DraggedValuesSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.articulated");
            var arm = Fx.Item(r, "robot.arm.articulated", new Vec2(150, 100));
            var h = r.DragHandles(arm, Fx.Ctx()).Single(x => x.Key == "joint.j2");
            arm = r.MoveHandle(arm, "joint.j2", Turn(h.Position, JointCentre(t, arm, "j2"), 20), Fx.Ctx());
            var grip = Fx.Item(r, "robot.gripper.parallel", new Vec2(40, 40));
            grip = r.MoveHandle(grip, "open", grip.Matrix().Apply(new Vec2(1.9 + 5.4 * 0.25, 9)), Fx.Ctx());
            var doc = SketchDocument.Create();
            doc = Edits.Add(Edits.Add(doc, doc.Pages[0].Id, arm), doc.Pages[0].Id, grip);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Near(arm.Num("j2", 0), back.Get(arm.Id)!.Num("j2", -1));
            Assert.Equal(arm.Size, back.Get(arm.Id)!.Size);
            Near(0.25, back.Get(grip.Id)!.Num("open", 0));
        }
    }
}
