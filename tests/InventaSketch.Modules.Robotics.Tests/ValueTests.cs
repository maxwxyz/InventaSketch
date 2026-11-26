using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    /// <summary>Payload, load and reach of robots, masses of tools and workpieces, and the checks robot.payload and robot.reach.</summary>
    public class ValueTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        private static SketchDocument With(SketchDocument doc, params (SketchObject O, string Key, string Text)[] values)
        {
            foreach (var (o, key, text) in values) doc = Edits.Replace(doc, doc.Get(o.Id)!.WithParam(key, text));
            return doc;
        }

        [Fact]
        public void TypesDeclareTheirValues()
        {
            var r = Fx.Reg();
            foreach (var arm in Fx.Arms(r))
            {
                Assert.Equal(new[] { "payload", "load", "reach" }, ValueModel.Variables(arm).Select(v => v.Key));
                Assert.Contains(arm.Parameters, p => p.Key == ValueLabels.ShowParam);
                Assert.Equal(new[] { "robot.payload", "robot.reach" }, ((IValueChecks)arm).ValueChecks.Select(c => c.Id));
            }
            foreach (var id in new[] { "robot.gripper.parallel", "robot.tool.changer", "robot.sensor.ft", "robot.sensor.vision", "robot.tool.weldgun", "robot.tool.dispenser", "robot.cell.workpiece" })
                Assert.Equal("mass", Assert.Single(ValueModel.Variables(r.Type(id))).Key);
            Assert.False(ValueModel.HasValues(r.Type("robot.safety.estop")));
            Assert.False(ValueModel.HasValues(r.Type(Track.TrackId)));
        }

        [Fact]
        public void TheLoadSumsTheMassesOnTheDockedChainAndWorkpiece()
        {
            var r = Fx.Reg();
            var (doc, arm, changer, sensor, gripper) = ToolChainTests.Chain(r);
            var ctx = new RenderContext(doc).For(r);
            var tcp = r.PageAnchors(doc.Get(gripper.Id)!, ctx).Single(a => a.Role == AnchorRole.Port && a.Id == "tcp").P;
            var wp = r.WithView(Fx.Item(r, "robot.cell.workpiece"), "side", Fx.Ctx());
            var grip = r.PageAnchors(wp, Fx.Ctx()).Single(a => a.Id == "grip").P;
            wp = wp with { Transform = wp.Transform with { X = wp.Transform.X + tcp.X - grip.X, Y = wp.Transform.Y + tcp.Y - grip.Y } };
            doc = Edits.Add(doc, doc.Pages[0].Id, wp);
            doc = With(doc, (changer, "mass", "1.2 kg"), (sensor, "mass", "0,4"), (gripper, "mass", "0.9 kg"), (wp, "mass", "3 kg"));
            var t = Solve(r, doc);
            Assert.Equal(1.2 + 0.4 + 0.9 + 3, Val(t, arm, "load")!.Value, 9);
            Assert.Equal(Provenance.Derived, t.Get(arm.Id, "load")!.Provenance);

            // Payload above the load: no warning; below: robot.payload.
            Assert.DoesNotContain(Checks(r, With(doc, (arm, "payload", "10 kg"))), f => f.CheckId == "robot.payload");
            var f = Checks(r, With(doc, (arm, "payload", "5 kg"))).Single(x => x.CheckId == "robot.payload");
            Assert.Equal(arm.Id, f.ObjectId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.False(string.IsNullOrWhiteSpace(f.Message.De));
            // Without a payload entered there is nothing to compare.
            Assert.DoesNotContain(Checks(r, doc), x => x.CheckId == "robot.payload");
        }

        [Fact]
        public void PartsWithoutAMassAreLeftOutOfTheLoad()
        {
            var r = Fx.Reg();
            var (doc, arm, _, _, gripper) = ToolChainTests.Chain(r);
            Assert.Null(Val(Solve(r, doc), arm, "load"));
            doc = With(doc, (gripper, "mass", "2 kg"));
            Assert.Equal(2, Val(Solve(r, doc), arm, "load")!.Value, 9);
        }

        [Fact]
        public void TheReachFollowsTheGeometryUnlessEntered()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.Arms(r))
            {
                var o = t.Create(new Vec2(100, 100));
                var (doc, _) = Fx.Doc(o);
                var reach = Val(Solve(r, doc), o, "reach");
                if (t.Id == "robot.arm.gantry") { Assert.Null(reach); continue; }
                Assert.Equal(t.ReachOf(o)!.Value / 1000, reach!.Value, 9);
            }
            // Articulated: the wrist centre reaches a1 + √(a2² + …) horizontally; longer links reach further.
            var arm = Fx.Item(r, "robot.arm.articulated", new Vec2(100, 100));
            var type = Fx.Type<RobotArmType>(r, arm.Type);
            Assert.True(type.ReachOf(arm.WithParam("upper", 900.0)) > type.ReachOf(arm));
            var (d2, _) = Fx.Doc(arm.WithParam("reach", "2.1 m"));
            var v = Solve(r, d2).Get(arm.Id, "reach")!;
            Assert.Equal(2.1, v.Value!.Value, 9);
            Assert.Equal(Provenance.Entered, v.Provenance);
        }

        [Fact]
        public void AWorkpieceOutsideTheEnvelopeIsReported()
        {
            var r = Fx.Reg();
            foreach (var view in new[] { "top", "side" })
            {
                var arm = r.WithView(Fx.Item(r, "robot.arm.articulated", new Vec2(100, 100)), view, Fx.Ctx());
                var type = Fx.Type<RobotArmType>(r, arm.Type);
                var basePt = r.PageAnchors(arm, Fx.Ctx()).Single(a => a.Id == "base").P;
                var s = type.SceneFor(arm, out _, out _);
                var reach = type.ReachOf(arm)!.Value * s.S;
                SketchObject Workpiece(double dx)
                {
                    var wp = r.WithView(Fx.Item(r, "robot.cell.workpiece"), view, Fx.Ctx());
                    var grip = r.PageAnchors(wp, Fx.Ctx()).Single(a => a.Id == "grip").P;
                    // Side view: at the shoulder height, where the envelope is widest; top view: on the x axis.
                    var y = view == "side" ? basePt.Y - 500 * s.S : basePt.Y;
                    return wp with { Transform = wp.Transform with { X = wp.Transform.X + basePt.X + dx - grip.X, Y = wp.Transform.Y + y - grip.Y } };
                }
                var near = Fx.Doc(arm, Workpiece(0.6 * reach)).Doc;
                Assert.DoesNotContain(Checks(r, near), f => f.CheckId == "robot.reach");
                var far = Fx.Doc(arm, Workpiece(1.6 * reach)).Doc;
                var finding = Checks(r, far).Single(f => f.CheckId == "robot.reach");
                Assert.Equal(arm.Id, finding.ObjectId);
                Assert.Equal(CheckSeverity.Warning, finding.Severity);
            }
        }

        [Fact]
        public void AWorkpieceIsCheckedAgainstTheNearestRobotOnly()
        {
            var r = Fx.Reg();
            var a = r.WithView(Fx.Item(r, "robot.arm.articulated", new Vec2(100, 100)), "top", Fx.Ctx());
            var b = r.WithView(Fx.Item(r, "robot.arm.articulated", new Vec2(400, 100)), "top", Fx.Ctx());
            var bBase = r.PageAnchors(b, Fx.Ctx()).Single(x => x.Id == "base").P;
            var wp = r.WithView(Fx.Item(r, "robot.cell.workpiece"), "top", Fx.Ctx());
            wp = wp with { Transform = wp.Transform with { X = bBase.X + 5, Y = bBase.Y } };
            var findings = Checks(r, Fx.Doc(a, b, wp).Doc);
            Assert.DoesNotContain(findings, f => f.CheckId == "robot.reach");
        }

        [Fact]
        public void MassesAndCarriagePositionSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var grip = Fx.Item(r, "robot.gripper.parallel").WithParam("mass", "0,8 kg");
            var arm = Fx.Item(r, "robot.arm.cobot").WithParam("payload", "5 kg");
            var track = Fx.Item(r, Track.TrackId).WithParam("pos", 0.7);
            var (doc, _) = Fx.Doc(grip, arm, track);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal("0,8 kg", back.Get(grip.Id)!.Str("mass", ""));
            Assert.Equal("5 kg", back.Get(arm.Id)!.Str("payload", ""));
            Assert.Equal(0.7, back.Get(track.Id)!.Num("pos", 0), 9);
            Assert.Equal(0.8, Val(Solve(r, back), back.Get(grip.Id)!, "mass")!.Value, 9);
        }
    }
}
