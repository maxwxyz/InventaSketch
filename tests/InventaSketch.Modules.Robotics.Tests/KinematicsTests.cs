using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    public class ForwardKinematicsTests
    {
        private static void Near(double expected, double actual, double tol = 1e-6, string what = "")
            => Assert.True(Math.Abs(expected - actual) <= tol, $"{what}: expected {expected}, got {actual}");

        private static void Near(V3 e, V3 a, double tol = 1e-6, string what = "")
        {
            Near(e.X, a.X, tol, what + ".x"); Near(e.Y, a.Y, tol, what + ".y"); Near(e.Z, a.Z, tol, what + ".z");
        }

        private static (RobotArmType T, SketchObject O) Arm(string id)
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, id);
            return (t, t.Create(Vec2.Zero));
        }

        private static V3 Flange(RobotArmType t, SketchObject o, params double[] q) => t.Pose(o, q).Flange;

        [Fact]
        public void ArticulatedArmFlangeForKnownAngles()
        {
            var (t, o) = Arm("robot.arm.articulated");
            // Defaults: shoulder height 500, offset 150, upper arm 650, forearm 700, wrist 120, elbow offset c3 = 0.15 · 650.
            const double d1 = 500, a1 = 150, a2 = 650, d4 = 700, d6 = 120, c3 = 97.5;
            Near(new V3(a1 + d4 + d6, 0, d1 + a2 + c3), Flange(t, o, 0, 0, 0, 0, 0, 0), what: "zero pose");
            // j2 = 90: upper arm horizontal forward, forearm points down, the elbow offset now points forward.
            Near(new V3(a1 + a2 + c3, 0, d1 - d4 - d6), Flange(t, o, 0, 90, 0, 0, 0, 0), what: "j2 = 90");
            // j1 = 90 turns clockwise in the top view: the arm points to −y (down on the page).
            var f = Flange(t, o, 90, 0, 0, 0, 0, 0);
            Near(new V3(0, -(a1 + d4 + d6), d1 + a2 + c3), f, what: "j1 = 90");
            Assert.True(Proj.To2(f, RoboViews.Top).Y > 0, "clockwise on the page");
            // j5 = 90: the tool points straight down.
            Near(new V3(a1 + d4, 0, d1 + a2 + c3 - d6), Flange(t, o, 0, 0, 0, 0, 90, 0), what: "j5 = 90");
            Near(new V3(0, 0, -1), t.Pose(o, new double[] { 0, 0, 0, 0, 90, 0 }).ToolDir, what: "tool dir");
            // j4 = 90 then j5 = 90: the wrist bends sideways (towards +y).
            Near(new V3(a1 + d4, d6, d1 + a2 + c3), Flange(t, o, 0, 0, 0, 90, 90, 0), what: "j4 = j5 = 90");
            // j3 = −90 + atan(c3/d4) stretches the arm: wrist centre straight above the shoulder.
            var stretch = -90 + Geo.RadToDeg(Math.Atan2(c3, d4));
            var w = t.Pose(o, new[] { 0, 0, stretch, 0, 0, 0 }).Joints.Single(j => j.Key == "j5").P;
            Near(new V3(a1, 0, d1 + a2 + Math.Sqrt(d4 * d4 + c3 * c3)), w, 1e-6, "stretched");
        }

        [Fact]
        public void LinkLengthParametersChangeTheChain()
        {
            var (t, o) = Arm("robot.arm.articulated");
            var o2 = o.WithParam("upper", 800.0).WithParam("fore", 900.0).WithParam("base", 300.0).WithParam("offset", 0.0).WithParam("wrist", 50.0);
            Near(new V3(900 + 50, 0, 300 + 800 + 0.15 * 800), Flange(t, o2, 0, 0, 0, 0, 0, 0));
        }

        [Fact]
        public void CobotFlangeForKnownAngles()
        {
            var (t, o) = Arm("robot.arm.cobot");
            // Base 180, upper 425, forearm 390, wrist link 110, J5 to flange 100.
            Near(new V3(390 + 110 + 100, 0, 180 + 425), Flange(t, o, 0, 0, 0, 0, 0, 0), what: "zero");
            Near(new V3(390, 0, 180 + 425 - 110 - 100), Flange(t, o, 0, 0, 0, 90, 0, 0), what: "j4 = 90");
            Near(new V3(390 + 110, 100, 180 + 425), Flange(t, o, 0, 0, 0, 0, 90, 0), what: "j5 = 90 (right-handed about local z)");
        }

        [Fact]
        public void ScaraFlangeForKnownAngles()
        {
            var (t, o) = Arm("robot.arm.scara");
            const double a1 = 325, a2 = 275, h = 400;
            var z0 = h - 130;
            Near(new V3(a1 + a2, 0, z0), Flange(t, o, 0, 0, 0, 0), what: "stretched");
            Near(new V3(0, -(a1 + a2), z0 - 150), Flange(t, o, 90, 0, 150, 0), what: "j1 = 90, stroke 150");
            Near(new V3(a1, -a2, z0), Flange(t, o, 0, 90, 0, 0), what: "j2 = 90");
        }

        [Fact]
        public void DeltaPlatformClosesAllThreeLoops()
        {
            var (t, o) = Arm("robot.arm.delta");
            const double h = 1000, rb = 200, l = 300, lf = 750, rp = 60;
            var rnd = new Random(3);
            for (var k = 0; k < 50; k++)
            {
                var q = new[] { -30 + 110 * rnd.NextDouble(), -30 + 110 * rnd.NextDouble(), -30 + 110 * rnd.NextDouble() };
                var p = DeltaType.Platform(h, rb, l, lf, rp, q, out var el);
                Assert.NotNull(p);
                for (var i = 0; i < 3; i++)
                {
                    var ui = new V3(Math.Cos(Geo.DegToRad(120 * i)), Math.Sin(Geo.DegToRad(120 * i)), 0);
                    Near(lf, (p!.Value + ui * rp).DistanceTo(el[i]), 1e-6, "forearm " + i);
                    Near(l, el[i].DistanceTo(ui * rb + new V3(0, 0, h)), 1e-6, "upper arm " + i);
                }
                Assert.True(p!.Value.Z < h, "platform below the base");
            }
            // Equal angles: the platform is centred.
            var c = DeltaType.Platform(h, rb, l, lf, rp, new[] { 30.0, 30, 30 }, out _)!.Value;
            Near(0, c.X, 1e-9); Near(0, c.Y, 1e-9);
            Assert.True(t.Pose(o, new[] { 30.0, 30, 30 }).Ok);
        }

        [Fact]
        public void GantryFlangeFollowsTheAxes()
        {
            var (t, o) = Arm("robot.arm.gantry");
            var a = Flange(t, o, 0, 0, 0);
            var b = Flange(t, o, 300, 200, 100);
            Near(new V3(300, 200, -100), b - a);
        }

        [Fact]
        public void JointValuesAreClampedToTheLimits()
        {
            var (t, o) = Arm("robot.arm.articulated");
            var q = t.Q(o.WithParam("j2", 170.0).WithParam("j1", -400.0));
            Assert.Equal(110, q[1]);
            Assert.Equal(-170, q[0]);
            Assert.Equal(-90, t.Q(o.WithParam("j2min", -90.0).WithParam("j2", -120.0))[1]);
        }
    }

    public class ReachEnvelopeTests
    {
        private static double MaxDist(Area2 a, Vec2 c) => a.Fill.Max(p => p.DistanceTo(c));
        private static double MinDist(Area2 a, Vec2 c) => a.Fill.Min(p => p.DistanceTo(c));

        [Fact]
        public void TwoLinkRadiiFollowFromTheLinkLengths()
        {
            // Elbow range including the stretched position: outer radius L1 + L2, inner radius from the fold limit.
            var a = Reach.TwoLink(Vec2.Zero, 325, 275, -132, 132, -145, 145);
            Assert.Equal(600, MaxDist(a, Vec2.Zero), 6);
            var rin = Math.Sqrt(325 * 325 + 275 * 275 + 2 * 325 * 275 * Math.Cos(Geo.DegToRad(145)));
            Assert.Equal(rin, MinDist(a, Vec2.Zero), 6);
            // One-sided elbow range: outer radius from the most stretched angle.
            var b = Reach.TwoLink(new Vec2(10, 20), 600, 500, -150, 0, 20, 150);
            Assert.Equal(Reach.Rho(600, 500, 20), MaxDist(b, new Vec2(10, 20)), 6);
            Assert.Equal(Reach.Rho(600, 500, 150), MinDist(b, new Vec2(10, 20)), 6);
            var (mn, mx) = Reach.RhoRange(600, 500, 20, 150);
            Assert.Equal(Reach.Rho(600, 500, 150), mn, 6);
            Assert.Equal(Reach.Rho(600, 500, 20), mx, 6);
        }

        [Fact]
        public void TwoLinkAreaStaysWithinItsReachableDirections()
        {
            // Shoulder −90 … 0, elbow 0 … 90: every point lies within [θmin + β, θmax + β] at its distance.
            var a = Reach.TwoLink(Vec2.Zero, 1, 1, -90, 0, 0, 90);
            foreach (var p in a.Fill)
            {
                var ang = Geo.RadToDeg(Math.Atan2(p.Y, p.X));
                Assert.InRange(ang, -90 - 1e-6, 45 + 1e-6);
            }
            Assert.Contains(a.Strokes, s => !s.Closed);
        }

        [Fact]
        public void FullTurnGivesAnAnnulusWithTwoCircles()
        {
            var a = Reach.Sector(Vec2.Zero, 200, 1000, -180, 180);
            Assert.Equal(2, a.Strokes.Count(s => s.Closed));
            Assert.Equal(1000, MaxDist(a, Vec2.Zero), 6);
            Assert.Equal(200, MinDist(a, Vec2.Zero), 6);
            var sector = Reach.Sector(Vec2.Zero, 0, 1000, -170, 170);
            Assert.Equal(0, sector.Strokes.Count(s => s.Closed));
        }

        [Fact]
        public void ArticulatedEnvelopeComesFromTheLinkLengths()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.articulated");
            // Elbow range including the stretched position.
            var o = t.Create(Vec2.Zero).WithParam("j3min", -120.0);
            var side = t.Envelope(o, RoboViews.Side, t.Q(o))!;
            var l3 = Math.Sqrt(700 * 700 + 97.5 * 97.5);
            Assert.Equal(650 + l3, MaxDist(side, new Vec2(150, -500)), 6);
            // Top view: horizontal reach from the J1 axis (shoulder offset + stretched arm, J2 = 90 lies in the range).
            var top = t.Envelope(o, RoboViews.Top, t.Q(o))!;
            var reach = 150 + 650 + l3;
            Assert.InRange(MaxDist(top, Vec2.Zero), reach * 0.995, reach + 1e-6);
            // The J1 limit of ±170° leaves a gap behind the robot: no closed circles.
            Assert.DoesNotContain(top.Strokes, s => s.Closed);
            var full = o.WithParam("j1lim", 180.0);
            Assert.Contains(t.Envelope(full, RoboViews.Top, t.Q(full))!.Strokes, s => s.Closed);
        }

        [Fact]
        public void ScaraEnvelopeIsTheClassicTwoLinkArea()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.scara");
            var o = t.Create(Vec2.Zero);
            var top = t.Envelope(o, RoboViews.Top, t.Q(o))!;
            Assert.Equal(600, MaxDist(top, Vec2.Zero), 6);
            var side = t.Envelope(o, RoboViews.Side, t.Q(o))!;
            var b = Rect.FromPoints(side.Fill);
            Assert.Equal(600, b.Right, 6);
            Assert.Equal(200, b.H, 6); // the quill stroke
        }

        [Fact]
        public void GantryEnvelopeIsTheStrokeBox()
        {
            var r = Fx.Reg();
            var t = Fx.Type<RobotArmType>(r, "robot.arm.gantry");
            var o = t.Create(Vec2.Zero);
            var top = Rect.FromPoints(t.Envelope(o, RoboViews.Top, t.Q(o))!.Fill);
            Assert.Equal(1200, top.W, 6);
            Assert.Equal(800, top.H, 6);
            var side = Rect.FromPoints(t.Envelope(o, RoboViews.Side, t.Q(o))!.Fill);
            Assert.Equal(500, side.H, 6);
        }

        [Fact]
        public void EnvelopeIsDrawnWhenSwitchedOn()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.Arms(r))
                foreach (var v in t.Views)
                {
                    var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                    Assert.DoesNotContain(t.Render(o, Fx.Ctx()), p => p.Tag == RoboTags.Envelope);
                    var on = o.WithParam("envelope", true);
                    Assert.Contains(t.Render(on, Fx.Ctx()), p => p.Tag == RoboTags.Envelope);
                }
        }

        [Fact]
        public void StandAloneEnvelopeLabelsItsRadius()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "robot.reach.envelope.top");
            var texts = r.TypeOf(o).Render(o, Fx.Ctx()).OfType<TextPrim>().Select(x => x.Text).ToList();
            var l3 = Math.Sqrt(700 * 700 + 97.5 * 97.5);
            // Default elbow range starts 2° short of stretched: the horizontal reach is a bit below a1 + a2 + l3.
            Assert.Contains(texts, x => x.StartsWith("R ") && Math.Abs(double.Parse(x.Substring(2)) - (150 + 650 + l3)) < 10);
        }
    }

    public class FramingAndMotionTests
    {
        [Fact]
        public void TheFrameContainsEveryPositionOfTheMovedJoint()
        {
            var r = Fx.Reg();
            var rnd = new Random(11);
            foreach (var t in Fx.Arms(r))
                foreach (var v in t.Views)
                    for (var k = 0; k < 30; k++)
                    {
                        var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                        var q = t.Joints.Select(j => { var (lo, hi) = t.Limit(o, j); return lo + (hi - lo) * rnd.NextDouble(); }).ToArray();
                        var frame = t.Frame(o, v.Id, q).Inflate(1e-6);
                        var mj = t.IndexOf(t.MotionJoint(o));
                        var (a, b) = t.Limit(o, t.MotionJoint(o));
                        for (var m = 0; m < 20; m++)
                        {
                            var qq = (double[])q.Clone();
                            qq[mj] = a + (b - a) * rnd.NextDouble();
                            var pose = t.Pose(o, qq);
                            if (!pose.Ok) continue;
                            foreach (var p in pose.Bodies.SelectMany(x => x.BoundPoints(v.Id)))
                                Assert.True(frame.Contains(p), $"{t.Id} [{v.Id}] point {p} outside frame {frame}");
                        }
                    }
        }

        [Fact]
        public void ChangingParametersKeepsScaleAndBaseInPlace()
        {
            var r = Fx.Reg();
            var ctx = Fx.Ctx();
            foreach (var t in Fx.Arms(r))
                foreach (var key in t.Joints.Select(j => j.Key).Concat(new[] { "envelope", "motion" }))
                {
                    var o = t.Create(new Vec2(80, 60)) with { Transform = new Transform(80, 60, 20) };
                    var b0 = r.PageAnchors(o, ctx).Single(a => a.Id == "base").P;
                    var s0 = t.SceneFor(o, out _, out _).S;
                    var j = t.Joints.FirstOrDefault(x => x.Key == key);
                    ParamValue value = key == "envelope" ? (ParamValue)true : key == "motion" ? (ParamValue)t.Joints.Last().Key : (ParamValue)(t.Limit(o, j!).Min * 0.3 + t.Limit(o, j!).Max * 0.7);
                    var changed = r.SetParam(o, key, value);
                    Assert.True(b0.DistanceTo(r.PageAnchors(changed, ctx).Single(a => a.Id == "base").P) < 1e-6, t.Id + "." + key + ": base moved");
                    Assert.Equal(s0, t.SceneFor(changed, out _, out _).S, 6);
                    Fx.AssertInsideBox(t.Render(changed, ctx), changed.Size, t.Id + "." + key);
                }
        }

        [Fact]
        public void AnimationFramesSweepTheMovedJointInTheSameBox()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.Arms(r))
            {
                var o = t.Create(new Vec2(50, 50));
                var frames = t.AnimationFrames(o, 12);
                Assert.Equal(12, frames.Count);
                Assert.Same(o, frames[0]);
                var key = t.MotionJoint(o).Key;
                Assert.True(frames.Select(f => Math.Round(f.Num(key, double.NaN), 3)).Distinct().Count() > 5, t.Id);
                var scale = t.SceneFor(o, out _, out _).S;
                foreach (var f in frames)
                {
                    Assert.Equal(o.Size, f.Size);
                    Assert.Equal(scale, t.SceneFor(f, out _, out _).S, 9);
                    Fx.AssertInsideBox(t.Render(f, Fx.Ctx()), f.Size, t.Id + " frame");
                }
            }
            var gripper = Fx.Item(r, "robot.gripper.parallel");
            Assert.Empty(r.TypeOf(gripper).AnimationFrames(gripper, 12));
        }

        [Fact]
        public void GhostPositionsArePhantomGroups()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "robot.arm.articulated").WithParam("ghosts", 4);
            var ghosts = r.TypeOf(o).Render(o, Fx.Ctx()).OfType<GroupPrim>().Where(g => g.Tag == RoboTags.Ghost).ToList();
            Assert.Equal(4, ghosts.Count);
            Assert.All(ghosts.SelectMany(g => Prim.Flatten(g.Children)), p => Assert.Equal(LineRole.Phantom, ((p as PathPrim)?.Style ?? ((EllipsePrim)p).Style).Role));
        }

        [Fact]
        public void ActionsStepTheMovedJointAndCycleIt()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "robot.arm.articulated");
            var t = r.TypeOf(o);
            var next = t.Actions(o, Lang.En).Single(a => a.Id == "robot.joint.next");
            Assert.Equal("J5 +15°", next.Label.En);
            Assert.Equal("A5 +15°", next.Label.De);
            Assert.Equal(85, next.Apply(o).Num("j5", 0), 9);
            Assert.Equal(o.Size, next.Apply(o).Size);
            // The moved joint is a choice among the joints, the current one marked.
            var joints = t.Actions(o, Lang.En).Where(a => a.Choice?.Key == "robot.motion").ToList();
            Assert.Equal("robot.motion=j5", Assert.Single(joints, a => a.IsActive).Id);
            var cycled = joints.Single(a => a.Id == "robot.motion=j6").Apply(o);
            Assert.Equal("j6", cycled.Str("motion", ""));
            var home = t.Actions(o, Lang.En).Single(a => a.Id == "robot.home").Apply(o.WithParam("j1", 50.0));
            Assert.Equal(0, home.Num("j1", 99));
            var env = t.Actions(o, Lang.En).Single(a => a.Id == "robot.envelope").Apply(o);
            Assert.True(env.Flag("envelope", false));
        }

        [Fact]
        public void GeometryChangesKeepScaleAndBasePosition()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "robot.arm.articulated", new Vec2(100, 80)) with { Transform = new Transform(100, 80, 30) };
            var t = Fx.Type<RobotArmType>(r, o.Type);
            var ctx = Fx.Ctx();
            var before = r.PageAnchors(o, ctx).Single(a => a.Id == "base").P;
            var s0 = t.SceneFor(o, out _, out _).S;
            var bigger = r.SetParam(o, "upper", 900.0);
            Assert.True(bigger.Size.H > t.SceneFor(o, out _, out _).S * 1000, "taller box for the longer arm");
            Assert.Equal(s0, t.SceneFor(bigger, out _, out _).S, 9);
            Assert.True(before.DistanceTo(r.PageAnchors(bigger, ctx).Single(a => a.Id == "base").P) < 1e-6);
            // Moving the moved joint (J5 by default) leaves the box alone.
            Assert.Equal(o.Size, r.SetParam(o, "j5", 40.0).Size);
        }

        [Fact]
        public void ViewSwitchKeepsTheDrawingScale()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.Arms(r))
            {
                var o = t.Create(Vec2.Zero) with { Size = new Size2(t.DefaultSize.W * 1.5, t.DefaultSize.H * 1.5) };
                var other = t.Views[1].Id;
                var switched = r.WithView(o, other, Fx.Ctx());
                Assert.Equal(other, switched.View);
                Assert.Equal(t.SceneFor(o, out _, out _).S, t.SceneFor(switched, out _, out _).S, 6);
            }
        }
    }
}
