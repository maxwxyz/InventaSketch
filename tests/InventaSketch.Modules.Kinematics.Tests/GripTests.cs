using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>
    /// Grips of kinematic elements (<see cref="ObjectType.DragHandles"/>): the input angle of mechanisms, piston position,
    /// jaw opening, pivot stops and angle limits.
    /// </summary>
    public class GripTests
    {
        /// <summary>The first plain library item (no preset parameters) of type <paramref name="typeId"/>, created at <paramref name="at"/>.</summary>
        private static SketchObject Item(Registry r, string typeId, Vec2 at)
            => r.Create(r.Library.FirstOrDefault(i => i.TypeId == typeId && i.Params is null) ?? throw new KeyNotFoundException(typeId), at);

        private static void Near(double expected, double actual, double tol = 1e-9, string what = "")
            => Assert.True(Math.Abs(expected - actual) <= tol, $"{what}: expected {expected}, got {actual}");

        private static void Near(Vec2 expected, Vec2 actual, double tol = 1e-9)
            => Assert.True(expected.DistanceTo(actual) <= tol, $"expected {expected}, got {actual}");

        /// <summary>Page position of a named anchor of the element.</summary>
        private static Vec2 AnchorAt(Registry r, SketchObject o, string id)
            => o.Matrix().Apply(r.Type(o.Type)!.Anchors(o, Fx.Ctx()).First(a => a.Id == id).P);

        /// <summary><paramref name="p"/> turned by <paramref name="deg"/> about <paramref name="c"/> (clockwise on screen for positive degrees).</summary>
        private static Vec2 Turn(Vec2 p, Vec2 c, double deg)
        {
            var a = Geo.DegToRad(deg);
            var d = p - c;
            return c + new Vec2(d.X * Math.Cos(a) - d.Y * Math.Sin(a), d.X * Math.Sin(a) + d.Y * Math.Cos(a));
        }

        [Fact]
        public void FourBarCrankIsTurnedByItsGrip()
        {
            var r = Fx.Reg();
            var o = Item(r, "kin.mech.fourbar", new Vec2(100, 80));
            var h = r.DragHandles(o, Fx.Ctx()).Single();
            Assert.Equal("angle", h.Key);
            Assert.Equal(DragConstraint.Free, h.Constraint);
            Assert.Equal("Input angle: −60°", h.Label.En);
            Assert.Equal("Antriebswinkel: −60°", h.Label.De);
            // The grip sits on the crank pin A and turns about the frame pivot O2.
            Near(AnchorAt(r, o, "A"), h.Position, 1e-6);
            var o2 = AnchorAt(r, o, "O2");
            Near(-30, r.MoveHandle(o, "angle", Turn(h.Position, o2, 30), Fx.Ctx()).Num("angle", 0));
            // Snapped to whole degrees; half a turn and more is wrapped to (−180°, 180°].
            Near(-48, r.MoveHandle(o, "angle", Turn(h.Position, o2, 12.4), Fx.Ctx()).Num("angle", 0));
            Near(150, r.MoveHandle(o, "angle", Turn(h.Position, o2, -150), Fx.Ctx()).Num("angle", 0));
            // Only the distance to the pivot differs: same angle.
            Near(-30, r.MoveHandle(o, "angle", o2 + (Turn(h.Position, o2, 30) - o2) * 2.5, Fx.Ctx()).Num("angle", 0));
            Assert.Same(o, r.MoveHandle(o, "crank", h.Position, Fx.Ctx()));
            // The grip follows the crank.
            var moved = r.MoveHandle(o, "angle", Turn(h.Position, o2, 30), Fx.Ctx());
            Near(AnchorAt(r, moved, "A"), r.DragHandles(moved, Fx.Ctx()).Single().Position, 1e-6);
        }

        [Fact]
        public void OtherMechanismsTurnTheirInputLink()
        {
            var r = Fx.Reg();
            // Toggle: the knee B turns about the upper pivot A.
            var toggle = Item(r, "kin.mech.toggle", new Vec2(100, 80));
            var h = r.DragHandles(toggle, Fx.Ctx()).Single();
            Near(10, r.MoveHandle(toggle, "angle", Turn(h.Position, AnchorAt(r, toggle, "A"), -20), Fx.Ctx()).Num("angle", 0));
            // Cam follower: the cam nose turns about the cam pivot O.
            var cam = Item(r, "kin.cam.follower", new Vec2(100, 80));
            h = r.DragHandles(cam, Fx.Ctx()).Single();
            Near(45, r.MoveHandle(cam, "angle", Turn(h.Position, AnchorAt(r, cam, "O"), 45), Fx.Ctx()).Num("angle", 0));
            // Every mechanism offers the input grip.
            foreach (var id in new[] { "kin.mech.slidercrank", "kin.mech.scotchyoke", "kin.mech.quickreturn" })
                Assert.Equal("angle", r.DragHandles(Item(r, id, new Vec2(100, 80)), Fx.Ctx()).Single().Key);
        }

        [Fact]
        public void PistonAndJawsAreDraggedAlongTheirStroke()
        {
            var r = Fx.Reg();
            // Piston: default box = design box (44 × 14), rod end at design x = 6 + 12 · 0.45.
            var piston = Item(r, "kin.link.piston", new Vec2(100, 80));
            var h = r.DragHandles(piston, Fx.Ctx()).Single();
            Assert.Equal("stroke", h.Key);
            Assert.Equal(DragConstraint.Horizontal, h.Constraint);
            Near(piston.Matrix().Apply(new Vec2(6 + 12 * 0.45, 0)), h.Position);
            Near(AnchorAt(r, piston, "rod"), h.Position);
            Assert.Equal("Piston position: 45 %", h.Label.En);
            Assert.Equal("Kolbenstellung: 45 %", h.Label.De);
            Near(0.8, r.MoveHandle(piston, "stroke", piston.Matrix().Apply(new Vec2(6 + 12 * 0.8, 3)), Fx.Ctx()).Num("stroke", 0));
            Near(1, r.MoveHandle(piston, "stroke", piston.Matrix().Apply(new Vec2(80, 0)), Fx.Ctx()).Num("stroke", 0));

            // Gripper jaws (design 30 × 26): right jaw at x = 3.5 + 7.5 · 0.6.
            var jaws = Item(r, "kin.gripper", new Vec2(100, 80));
            var g = r.DragHandles(jaws, Fx.Ctx()).Single();
            Assert.Equal("open", g.Key);
            Near(jaws.Matrix().Apply(new Vec2(8, 4)), g.Position);
            Assert.Equal("Jaw opening: 60 %", g.Label.En);
            Near(0.2, r.MoveHandle(jaws, "open", jaws.Matrix().Apply(new Vec2(5, 0)), Fx.Ctx()).Num("open", 0));
            Assert.Same(jaws, r.MoveHandle(jaws, "stroke", g.Position, Fx.Ctx()));
        }

        [Fact]
        public void PivotStopsAndAngleLimitsAreTurned()
        {
            var r = Fx.Reg();
            var pivot = Item(r, "kin.joint.ground", new Vec2(100, 80));
            Assert.Empty(r.DragHandles(pivot, Fx.Ctx())); // no stops, no grips
            pivot = pivot.WithParam("limited", true);
            var hs = r.DragHandles(pivot, Fx.Ctx());
            Assert.Equal(new[] { "limitFrom", "limitTo" }, hs.Select(h => h.Key));
            // Pivot centre at design (0, −2.5), stop radius 4 · 1.5 mm (default box = design box 12 × 11).
            var c = pivot.Matrix().Apply(new Vec2(0, -2.5));
            Near(c + new Vec2(Math.Cos(Geo.DegToRad(-135)), Math.Sin(Geo.DegToRad(-135))) * 6, hs[0].Position);
            Assert.Equal("Stop from: −135°", hs[0].Label.En);
            Assert.Equal("Anschlag von: −135°", hs[0].Label.De);
            Near(0, r.MoveHandle(pivot, "limitTo", c + new Vec2(4, 0), Fx.Ctx()).Num("limitTo", 1));
            // Snapped to 5°: 172° from −135° is the short way round, to −190° (≡ 170°).
            Near(-190, r.MoveHandle(pivot, "limitFrom", c + new Vec2(Math.Cos(Geo.DegToRad(172)), Math.Sin(Geo.DegToRad(172))) * 5, Fx.Ctx()).Num("limitFrom", 0));

            var limit = Item(r, "kin.motion.anglelimit", new Vec2(100, 80));
            var lh = r.DragHandles(limit, Fx.Ctx());
            Assert.Equal(new[] { "min", "max" }, lh.Select(h => h.Key));
            // Box 34 × 34: grips at 0.75 of the half size along the limit rays.
            Near(limit.Matrix().Apply(new Vec2(Math.Cos(Geo.DegToRad(-30)), Math.Sin(Geo.DegToRad(-30))) * 12.75), lh[0].Position);
            Assert.Equal("Maximum angle: 45°", lh[1].Label.En);
            Near(90, r.MoveHandle(limit, "max", limit.Matrix().Apply(new Vec2(0, 10)), Fx.Ctx()).Num("max", 0));
            Near(-100, r.MoveHandle(limit, "min", limit.Matrix().Apply(new Vec2(Math.Cos(Geo.DegToRad(-101)), Math.Sin(Geo.DegToRad(-101))) * 9), Fx.Ctx()).Num("min", 0));
            Assert.Same(limit, r.MoveHandle(limit, "mid", lh[0].Position, Fx.Ctx()));
        }

        [Fact]
        public void DraggedValuesSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var bar = Item(r, "kin.mech.fourbar", new Vec2(100, 80));
            var h = r.DragHandles(bar, Fx.Ctx()).Single();
            bar = r.MoveHandle(bar, "angle", Turn(h.Position, AnchorAt(r, bar, "O2"), 30), Fx.Ctx());
            var piston = Item(r, "kin.link.piston", new Vec2(40, 40));
            piston = r.MoveHandle(piston, "stroke", piston.Matrix().Apply(new Vec2(6 + 12 * 0.8, 0)), Fx.Ctx());
            var doc = SketchDocument.Create();
            doc = Edits.Add(Edits.Add(doc, doc.Pages[0].Id, bar), doc.Pages[0].Id, piston);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Near(-30, back.Get(bar.Id)!.Num("angle", 0));
            Near(0.8, back.Get(piston.Id)!.Num("stroke", 0));
        }

        [Fact]
        public void AssemblyModeToggleDefersToTheDropdown()
        {
            // The four-bar's "crossed" toggle sets the assembly mode the hosts show as a dropdown: it names the parameter.
            var r = Fx.Reg();
            var o = Item(r, "kin.mech.fourbar", new Vec2(100, 80));
            var a = r.Type(o.Type)!.Actions(o, Lang.En).Single(x => x.Id == "kin.branch");
            Assert.Equal(new[] { "branch" }, a.ParamKeys);
        }
    }
}
