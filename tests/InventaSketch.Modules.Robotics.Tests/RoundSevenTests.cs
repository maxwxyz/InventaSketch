using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    /// <summary>
    /// Round seven: flange position from forward kinematics for every robot, the static load moment at the flange (check
    /// robot.moment), the planar 2R/3R arm with forward and inverse kinematics (values, "Move to target", check
    /// robot.ik.reach), gripping forces of friction and vacuum grippers (checks robot.grip.force, robot.vacuum.force) and the
    /// compressed-air and vacuum ports of the grippers. Expected values are hand calculations.
    /// </summary>
    public class RoundSevenTests
    {
        private const double Deg = Math.PI / 180;
        private const double G = 9.80665;

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;


        // ------------------------------------------------------------------ forward kinematics and load moment of all robots

        [Fact]
        public void EveryRobotReportsItsFlangePosition()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.Arms(r).Where(t => t is not PlanarArmType))
            {
                var o = t.Create(new Vec2(100, 100));
                var f = t.Pose(o, t.Q(o)).Flange;
                var v = Solve(r, Fx.Doc(o).Doc);
                Assert.Equal(f.X / 1000, Val(v, o, "fx")!.Value, 9);
                Assert.Equal(f.Y / 1000, Val(v, o, "fy")!.Value, 9);
                Assert.Equal(f.Z / 1000, Val(v, o, "fz")!.Value, 9);
                Assert.Equal(Provenance.Derived, v.Get(o.Id, "fx")!.Provenance);
                Assert.True(ValueModel.Variable(t, "fx")!.IsReadOnly);
            }
            // Articulated arm, all joints 0: flange at (a1 + d4 + d6, 0, d1 + a2 + c3) = (970, 0, 1247.5) mm.
            var arm = Fx.Item(r, "robot.arm.articulated", new Vec2(100, 100));
            foreach (var j in new[] { "j1", "j2", "j3", "j4", "j5", "j6" }) arm = arm.WithParam(j, 0.0);
            var t0 = Solve(r, Fx.Doc(arm).Doc);
            Assert.Equal(0.970, Val(t0, arm, "fx")!.Value, 9);
            Assert.Equal(0, Val(t0, arm, "fy")!.Value, 9);
            Assert.Equal(1.2475, Val(t0, arm, "fz")!.Value, 9);
        }

        [Fact]
        public void TheLoadMomentAtTheFlangeIsChecked()
        {
            var r = Fx.Reg();
            var arm = Fx.Item(r, "robot.arm.cobot", new Vec2(100, 100)).WithParam("load", "3 kg").WithParam("cog", "100 mm");
            var (doc, _) = Fx.Doc(arm);
            // M_L = 3 kg · 9.80665 m/s² · 0.1 m = 2.942 N·m.
            Assert.Equal(3 * G * 0.1, Val(Solve(r, doc), arm, "moment")!.Value, 9);
            Assert.DoesNotContain(Checks(r, Fx.Doc(arm.WithParam("momentMax", "5 N·m")).Doc), f => f.CheckId == "robot.moment");
            var f = Checks(r, Fx.Doc(arm.WithParam("momentMax", "2 N·m")).Doc).Single(x => x.CheckId == "robot.moment");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S1", f.Source);
            // The other way round: the permissible distance for a given moment.
            var back = Fx.Item(r, "robot.arm.cobot").WithParam("load", "4 kg").WithParam("moment", "7.8453 N·m");
            Assert.Equal(0.2, Val(Solve(r, Fx.Doc(back).Doc), back, "cog")!.Value, 5);
        }

        // ------------------------------------------------------------------ planar arm: forward kinematics

        private static double[] Fk(double[] l, double[] q)
        {
            double x = 0, z = 0, sum = 0;
            for (var i = 0; i < l.Length; i++)
            {
                sum += q[i];
                x += l[i] * Math.Cos(sum * Deg);
                z -= l[i] * Math.Sin(sum * Deg);
            }
            return new[] { x, z, sum };
        }

        [Theory]
        [InlineData(PlanarArmType.Id2)]
        [InlineData(PlanarArmType.Id3)]
        public void PlanarForwardKinematicsMatchesThePose(string id)
        {
            var r = Fx.Reg();
            var t = Fx.Type<PlanarArmType>(r, id);
            var rnd = new Random(3);
            for (var k = 0; k < 10; k++)
            {
                var o = t.Create(new Vec2(100, 100)).WithParam("l1", 350.0).WithParam("l2", 280.0).WithParam("l3", 90.0);
                foreach (var j in t.Joints) o = o.WithParam(j.Key, Math.Round(-150 + 300 * rnd.NextDouble(), 1));
                var q = t.Q(o);
                var l = t.Lengths(o);
                var fk = Fk(l, q);
                // The drawn flange relative to joint 1 (height "base" above the floor).
                var f = t.Pose(o, q).Flange;
                Assert.Equal(fk[0], f.X, 6);
                Assert.Equal(fk[1], f.Z - 150, 6);
                var v = Solve(r, Fx.Doc(o).Doc);
                Assert.Equal(fk[0] / 1000, Val(v, o, "x")!.Value, 9);
                Assert.Equal(fk[1] / 1000, Val(v, o, "z")!.Value, 9);
                Assert.Equal(fk[2] * Deg, Val(v, o, "phi")!.Value, 9);
                Assert.Equal(l.Sum() / 1000, Val(v, o, "reach")!.Value, 9);
            }
            // Default 2R: x = 400·cos 40° + 300·cos 30°, z = 400·sin 40° − 300·sin 30°.
            if (id == PlanarArmType.Id2)
            {
                var o = t.Create(Vec2.Zero);
                var v = Solve(r, Fx.Doc(o).Doc);
                Assert.Equal((400 * Math.Cos(40 * Deg) + 300 * Math.Cos(30 * Deg)) / 1000, Val(v, o, "x")!.Value, 9);
                Assert.Equal((400 * Math.Sin(40 * Deg) - 300 * Math.Sin(30 * Deg)) / 1000, Val(v, o, "z")!.Value, 9);
            }
        }

        // ------------------------------------------------------------------ planar arm: inverse kinematics

        [Fact]
        public void InverseKinematicsIsTheInverseOfForwardKinematics()
        {
            var rnd = new Random(5);
            var l2 = new[] { 400.0, 300.0 };
            var l3 = new[] { 400.0, 300.0, 120.0 };
            for (var k = 0; k < 200; k++)
            {
                var q = new[] { -170 + 340 * rnd.NextDouble(), -165 + 330 * rnd.NextDouble(), -165 + 330 * rnd.NextDouble() };
                foreach (var l in new[] { l2, l3 })
                {
                    var fk = Fk(l, q);
                    var up = q[1] >= 0;
                    var ik = PlanarValues.Ik(l, fk[0], -fk[1], l.Length == 3 ? fk[2] : null, up)!;
                    var back = Fk(l, ik);
                    Assert.Equal(fk[0], back[0], 6);
                    Assert.Equal(fk[1], back[1], 6);
                    Assert.Equal(Rb.NormAngle(q[1]), ik[1], 6);
                    if (l.Length == 3) Assert.Equal(Rb.NormAngle(fk[2]), Rb.NormAngle(back[2]), 6);
                }
            }
            // Out of reach: beyond L1 + L2 and inside |L1 − L2|.
            Assert.Null(PlanarValues.Ik(l2, 701, 0, null, true));
            Assert.Null(PlanarValues.Ik(l2, 99, 0, null, true));
            Assert.NotNull(PlanarValues.Ik(l2, 699, 0, null, true));
        }

        [Fact]
        public void MoveToTargetPutsTheFlangeOnTheEnteredTarget()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { PlanarArmType.Id2, PlanarArmType.Id3 })
                foreach (var elbow in new[] { "up", "down" })
                {
                    var t = Fx.Type<PlanarArmType>(r, id);
                    var o = t.Create(new Vec2(100, 100)).WithParam("targetX", "450 mm").WithParam("targetZ", "200 mm").WithParam("elbow", elbow);
                    if (id == PlanarArmType.Id3) o = o.WithParam("targetPhi", "30°");
                    var v = Solve(r, Fx.Doc(o).Doc);
                    var expected = t.TargetJoints(o)!;
                    for (var i = 0; i < t.Joints.Count; i++)
                        Assert.Equal(expected[i] * Deg, Val(v, o, "q" + (i + 1))!.Value, 9);
                    Assert.Equal(elbow == "up", expected[1] >= 0);
                    var action = t.Actions(o, Lang.En).Single(a => a.Id == "robot.ik.apply");
                    Assert.True(action.IsEnabled);
                    var moved = action.Apply(o);
                    var f = t.Pose(moved, t.Q(moved)).Flange;
                    Assert.Equal(450, f.X, 2);
                    Assert.Equal(200, f.Z - 150, 2);
                    if (id == PlanarArmType.Id3) Assert.Equal(30, Rb.NormAngle(t.Q(moved).Sum()), 2);
                    // The target marker is drawn and stays inside the box; the base does not move.
                    var prims = t.Render(moved, Fx.Ctx());
                    Assert.Contains(prims, p => p.Tag == PlanarValues.TargetTag);
                    Fx.AssertInsideBox(prims, moved.Size, id + " " + elbow);
                    Assert.True(r.PageAnchors(o, Fx.Ctx()).Single(a => a.Id == "base").P.DistanceTo(r.PageAnchors(moved, Fx.Ctx()).Single(a => a.Id == "base").P) < 1e-6);
                    // After the move the forward kinematics shows the target.
                    var after = Solve(r, Fx.Doc(moved).Doc);
                    Assert.Equal(0.45, Val(after, moved, "x")!.Value, 4);
                    Assert.Equal(0.2, Val(after, moved, "z")!.Value, 4);
                }
        }

        [Fact]
        public void AnUnreachableTargetIsReported()
        {
            var r = Fx.Reg();
            var t = Fx.Type<PlanarArmType>(r, PlanarArmType.Id2);
            var far = t.Create(Vec2.Zero).WithParam("targetX", "800 mm").WithParam("targetZ", "0 mm");
            var f = Checks(r, Fx.Doc(far).Doc).Single(x => x.CheckId == "robot.ik.reach");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.False(t.Actions(far, Lang.En).Single(a => a.Id == "robot.ik.apply").IsEnabled);
            Assert.Null(Solve(r, Fx.Doc(far).Doc).Get(far.Id, "q1")!.Value);
            var near = t.Create(Vec2.Zero).WithParam("targetX", "500 mm").WithParam("targetZ", "100 mm");
            Assert.DoesNotContain(Checks(r, Fx.Doc(near).Doc), x => x.CheckId == "robot.ik.reach");
            // 3R without a direction: only a target beyond L1 + L2 + L3 is certainly out of reach.
            var t3 = Fx.Type<PlanarArmType>(r, PlanarArmType.Id3);
            Assert.DoesNotContain(Checks(r, Fx.Doc(t3.Create(Vec2.Zero).WithParam("targetX", "800 mm").WithParam("targetZ", "0 mm")).Doc), x => x.CheckId == "robot.ik.reach");
            Assert.Contains(Checks(r, Fx.Doc(t3.Create(Vec2.Zero).WithParam("targetX", "900 mm").WithParam("targetZ", "0 mm")).Doc), x => x.CheckId == "robot.ik.reach");
            // Without a target the action does nothing.
            var plain = t.Create(Vec2.Zero);
            Assert.False(t.Actions(plain, Lang.En).Single(a => a.Id == "robot.ik.apply").IsEnabled);
        }

        [Fact]
        public void PlanarArmsRenderInBothViewsAndRoundTrip()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => i.TypeId is PlanarArmType.Id2 or PlanarArmType.Id3))
            {
                var o = Fx.Item(r, item.Id);
                var t = r.TypeOf(o);
                foreach (var v in t.Views)
                    foreach (var lang in new[] { Lang.En, Lang.De })
                    {
                        var x = r.WithView(o, v.Id, Fx.Ctx());
                        Fx.AssertInsideBox(t.Render(x, Fx.Ctx(lang)), x.Size, item.Id + " " + v.Id);
                    }
            }
            Assert.Contains(r.Library, i => i.Id == "robot.arm.planar2r.envelope");
            Assert.Contains(r.Search("zweigelenk"), i => i.TypeId == PlanarArmType.Id2);
            var o2 = Fx.Item(r, PlanarArmType.Id3).WithParam("targetX", "0,45 m").WithParam("targetPhi", "30°");
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(Fx.Doc(o2).Doc));
            Assert.Equal("0,45 m", back.Get(o2.Id)!.Str("targetX", ""));
            Assert.Equal("30°", back.Get(o2.Id)!.Str("targetPhi", ""));
        }

        // ------------------------------------------------------------------ grippers

        private static (SketchDocument Doc, SketchObject Gripper, SketchObject Workpiece) Held(Registry r, string gripper, string mass)
        {
            var g = r.WithView(Fx.Item(r, gripper, new Vec2(80, 60)), "side", Fx.Ctx());
            var tcp = r.PageAnchors(g, Fx.Ctx()).Single(a => a.Role == AnchorRole.Port && a.Id == "tcp").P;
            var wp = r.WithView(Fx.Item(r, "robot.cell.workpiece"), "side", Fx.Ctx());
            var grip = r.PageAnchors(wp, Fx.Ctx()).Single(a => a.Id == "grip").P;
            wp = wp with { Transform = wp.Transform with { X = wp.Transform.X + tcp.X - grip.X, Y = wp.Transform.Y + tcp.Y - grip.Y } };
            wp = wp.WithParam("mass", mass);
            return (Fx.Doc(g, wp).Doc, g, wp);
        }

        [Theory]
        [InlineData("robot.gripper.parallel")]
        [InlineData("robot.gripper.threefinger")]
        public void TheFrictionGripNeedsEnoughGrippingForce(string id)
        {
            var r = Fx.Reg();
            var (doc, g, _) = Held(r, id, "2 kg");
            doc = Edits.Replace(doc, doc.Get(g.Id)!.WithParam("mu", "0,1").WithParam("force", "300 N"));
            var t = Solve(r, doc);
            // m_W linked from the workpiece; F_req = 2 kg · 9.80665 m/s² · 2 / 0.1 = 392.27 N (S = 2, a = 0 by default).
            Assert.Equal(2, Val(t, g, "held")!.Value, 9);
            Assert.Equal(Provenance.Linked, t.Get(g.Id, "held")!.Provenance);
            Assert.Equal(2 * G * 2 / 0.1, Val(t, g, "required")!.Value, 6);
            var f = Checks(r, doc).Single(x => x.CheckId == "robot.grip.force");
            Assert.Equal(g.Id, f.ObjectId);
            Assert.DoesNotContain(Checks(r, Edits.Replace(doc, doc.Get(g.Id)!.WithParam("force", "500 N"))), x => x.CheckId == "robot.grip.force");
            // With an acceleration of 5 m/s² and S = 1.5: F_req = 2·(9.80665 + 5)·1.5/0.1.
            var fast = Edits.Replace(doc, doc.Get(g.Id)!.WithParam("acc", "5 m/s²").WithParam("safety", "1,5"));
            Assert.Equal(2 * (G + 5) * 1.5 / 0.1, Val(Solve(r, fast), g, "required")!.Value, 6);
            // Without a friction coefficient nothing is required or checked.
            var (doc2, g2, _) = Held(r, id, "2 kg");
            Assert.Null(Val(Solve(r, doc2), g2, "required"));
            Assert.DoesNotContain(Checks(r, doc2), x => x.CheckId == "robot.grip.force");
        }

        [Fact]
        public void TheVacuumGripperHoldsWithItsCups()
        {
            var r = Fx.Reg();
            var (doc, g, wp) = Held(r, "robot.gripper.vacuum", "5 kg");
            doc = Edits.Replace(doc, doc.Get(g.Id)!.WithParam("cups", 2).WithParam("cupDia", "40 mm").WithParam("vacuum", "0,6 bar"));
            var t = Solve(r, doc);
            // F_th = 0.6·10⁵ Pa · 2 · π·(0.04 m)²/4 = 150.80 N; F_req = 5 kg · 9.80665 · 2 = 98.07 N.
            var holding = 0.6e5 * 2 * Math.PI * 0.04 * 0.04 / 4;
            Assert.Equal(holding, Val(t, g, "holding")!.Value, 6);
            Assert.Equal(5 * G * 2, Val(t, g, "required")!.Value, 6);
            Assert.DoesNotContain(Checks(r, doc), x => x.CheckId == "robot.vacuum.force");
            var heavy = Edits.Replace(doc, doc.Get(wp.Id)!.WithParam("mass", "10 kg"));
            Assert.Contains(Checks(r, heavy), x => x.CheckId == "robot.vacuum.force" && x.ObjectId == g.Id);
            // The cup diameter for a required holding force.
            var sized = Edits.Replace(doc, doc.Get(g.Id)!.WithParam("cupDia", "").WithParam("holding", "300 N"));
            Assert.Equal(Math.Sqrt(4 * 300 / (2 * Math.PI * 0.6e5)), Val(Solve(r, sized), g, "cupDia")!.Value, 6);
        }

        [Fact]
        public void GrippersOfferCompressedAirAndVacuumPorts()
        {
            var r = Fx.Reg();
            foreach (var (id, port) in new[] { ("robot.gripper.parallel", "air"), ("robot.gripper.threefinger", "air"), ("robot.gripper.vacuum", "vacuum") })
            {
                var o = r.WithView(Fx.Item(r, id), "side", Fx.Ctx());
                var t = r.TypeOf(o);
                var a = t.Anchors(o, Fx.Ctx()).Single(x => x.Role == AnchorRole.Port && x.Id == port);
                Assert.True(Math.Abs(a.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(a.P.Y) <= o.Size.H / 2 + 1e-9, id);
                var c = t.Connection(o, a)!;
                Assert.Equal(PortMedia.FluidPneumatic, c.Medium);
                Assert.Equal("fluid.line", c.Connector);
                // A fluid power line ("fluid") and the plant's compressed air fit; electric wires do not.
                Assert.True(PortMedia.Fits(c.Medium, PortMedia.Fluid));
                Assert.True(PortMedia.Fits(c.Medium, PortMedia.AirCompressed));
                Assert.False(PortMedia.Fits(c.Medium, PortMedia.Electric));
                Assert.Null(t.Connection(o, t.Anchors(o, Fx.Ctx()).Single(x => x.Id == "tcp")));
            }
            // The magnetic gripper has no air port.
            var m = r.WithView(Fx.Item(r, "robot.gripper.magnetic"), "side", Fx.Ctx());
            Assert.DoesNotContain(r.TypeOf(m).Anchors(m, Fx.Ctx()), x => x.Id == "air");
        }
    }
}
