using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>Geneva drive, ratchet and pawl, Watt and Stephenson six-bar, pantograph.</summary>
    public class MoreMechanismTests
    {
        private static readonly string[] Ids = { "kin.mech.geneva", "kin.ratchet", "kin.mech.sixbar", "kin.mech.pantograph" };

        private static Dictionary<string, Vec2> Ports(ObjectType t, SketchObject o)
            => t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).GroupBy(a => a.Id!).ToDictionary(g => g.Key, g => g.First().P);

        [Fact]
        public void NewMechanismsStayInsideTheirBoxOverAFullTurn()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => Ids.Contains(i.TypeId)))
                foreach (var view in new[] { "schematic", "detailed" })
                    for (var a = -180; a < 180; a += 10)
                    {
                        var o = r.Create(item, Vec2.Zero) with { View = view };
                        o = o.WithParam("angle", a).WithParam("dof", true);
                        if (r.TypeOf(o).Parameters.Any(p => p.Key == "ghosts")) o = o.WithParam("ghosts", 3).WithParam("path", true);
                        var prims = Fx.Render(r, o);
                        Fx.AssertInsideBox(prims, o.Size, $"{item.Id}/{view}@{a}");
                        Assert.DoesNotContain(prims, p => p.Tag == KinTags.NoAssembly);
                    }
        }

        [Theory]
        [InlineData("kin.mech.geneva", 1)]
        [InlineData("kin.ratchet", 1)]
        [InlineData("kin.mech.sixbar", 1)]
        [InlineData("kin.mech.pantograph", 2)]
        public void DegreesOfFreedom(string id, int dof)
        {
            var r = Fx.Reg();
            var t = r.Type(id)!;
            var o = t.Create(Vec2.Zero).WithParam("dof", true);
            Assert.Contains(t.Render(o, Fx.Ctx()), p => p is TextPrim tp && tp.Tag == KinTags.Dof && tp.Text == "DOF = " + dof);
        }

        // ------------------------------------------------------------------ Geneva

        [Theory]
        [InlineData(4)]
        [InlineData(6)]
        public void GenevaWheelIndexesOneSlotPerTurnAndDwells(int z)
        {
            const double c = 30;
            var limit = 90 - 180.0 / z;
            // Locked outside the engagement, the same picture at both ends (the wheel repeats every 360°/z).
            Assert.Equal(180.0 / z, Linkages.GenevaWheelAngle(z, c, 150), 9);
            Assert.Equal(180.0 / z, Linkages.GenevaWheelAngle(z, c, -150), 9);
            Assert.Equal(-180.0 / z, Linkages.GenevaWheelAngle(z, c, limit), 6);
            Assert.Equal(180.0 / z, Linkages.GenevaWheelAngle(z, c, -limit), 6);
            Assert.Equal(0, Linkages.GenevaWheelAngle(z, c, 0), 9);
            // Opposite sense of rotation, monotonic while engaged, the pin always on the slot axis.
            var a = c * Math.Sin(Math.PI / z);
            var prev = double.MaxValue;
            for (var t = -limit; t <= limit; t += 1)
            {
                var w = Linkages.GenevaWheelAngle(z, c, t);
                Assert.True(w < prev + 1e-9);
                prev = w;
                var pin = Linkages.Dir(t) * a;
                var slot = Linkages.Dir(w + 180);
                Assert.True(Math.Abs(slot.Cross(pin - new Vec2(c, 0))) < 1e-9, $"pin on the slot axis at {t}");
            }
        }

        [Fact]
        public void GenevaPinRunsInsideTheSlot()
        {
            var r = Fx.Reg();
            foreach (var slots in new[] { "4", "6" })
            {
                var o = r.Create(r.Item("kin.geneva")!, Vec2.Zero).WithParam("slots", slots);
                var shape = GenevaType.ShapeOf(o);
                for (var t = -40; t <= 40; t += 5)
                {
                    var wheel = GenevaType.WheelOutline(shape, Linkages.GenevaWheelAngle(shape.Z, shape.C, t));
                    var pin = Linkages.Dir(t) * shape.A;
                    // The pin centre is outside the wheel body (in a slot or in front of it).
                    Assert.False(Geo.PointInPolygon(pin, wheel), $"{slots} slots: pin inside the body at {t}°");
                }
                // Locked: the locking disc and the wheel's concave arc are concentric with the driver.
                var locked = GenevaType.WheelOutline(shape, 180.0 / shape.Z);
                var nearest = locked.Min(p => p.Length);
                Assert.True(nearest > shape.Lock, "the wheel clears the locking disc while locked");
                Assert.True(nearest < shape.Lock + 0.1 * shape.A);
            }
        }

        [Fact]
        public void GenevaSlotsChoiceAndAnchors()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.geneva")!;
            var o = r.Create(r.Item("kin.geneva")!, Vec2.Zero);
            var choice = t.Actions(o, Lang.En).Where(a => a.Id.StartsWith("kin.slots=")).ToList();
            Assert.Equal(2, choice.Count);
            Assert.Single(choice, a => a.IsActive);
            Assert.Equal("6", choice.Single(a => a.Id == "kin.slots=6").Apply(o).Str("slots", ""));
            var anchors = t.Anchors(o, Fx.Ctx()).ToList();
            Assert.Equal(2, anchors.Count(a => a.Role == AnchorRole.Hub));
            Assert.Equal(2, anchors.Count(a => a.Role == AnchorRole.Mount));
            var h = r.DragHandles(o, Fx.Ctx()).Single();
            Assert.Equal("angle", h.Key);
            Assert.Single(t.Render(o, Fx.Ctx()), p => p.Tag == KinTags.Wheel);
        }

        // ------------------------------------------------------------------ ratchet

        [Fact]
        public void PawlRestsOnTheTeethAndDropsBehindEachTooth()
        {
            foreach (var z in new[] { 8, 12, 24 })
            {
                var drops = 0;
                double? prevR = null;
                for (var t = 0.0; t < 360; t += 0.5)
                {
                    var outline = RatchetType.Outline(z, t);
                    var tip = RatchetType.PawlTip(outline);
                    // On the outline: tip at the pawl length from its pivot, on the tooth contour.
                    Assert.Equal(RatchetType.PawlLength, tip.DistanceTo(RatchetType.PawlPivot), 9);
                    var dist = Enumerable.Range(0, outline.Count).Min(i => Geo.ClosestOnSegment(tip, outline[i], outline[(i + 1) % outline.Count]).Point.DistanceTo(tip));
                    Assert.True(dist < 1e-6, $"z={z} @ {t}: tip off the contour by {dist}");
                    Assert.True(tip.Length >= RatchetType.Root(z) - 1e-6 && tip.Length <= RatchetType.Tip + 1e-6);
                    if (prevR is double pr && pr - tip.Length > 0.5 * (RatchetType.Tip - RatchetType.Root(z))) drops++;
                    prevR = tip.Length;
                }
                // One drop per tooth that passes under the pawl in a full turn.
                Assert.InRange(drops, z - 1, z);
            }
        }

        [Fact]
        public void RatchetRendersWheelPawlAndSpring()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("kin.ratchet")!, Vec2.Zero);
            var prims = Fx.Render(r, o);
            Assert.Single(prims, p => p.Tag == KinTags.Wheel);
            Assert.Contains(prims, p => p.Tag == KinTags.Pawl);
            Assert.Contains(prims, p => p.Tag == KinTags.Element);
            Assert.DoesNotContain(Fx.Render(r, o.WithParam("spring", false)), p => p.Tag == KinTags.Element);
            var teeth = ((PathPrim)prims.First(p => p.Tag == KinTags.Wheel)).Segments.Count(s => s.Op != SegOp.Z);
            Assert.Equal(24, teeth); // 12 teeth × (tip, root)
            Assert.Equal(12, r.TypeOf(o).AnimationFrames(o, 12).Count);
        }

        // ------------------------------------------------------------------ six-bar

        [Theory]
        [InlineData("watt")]
        [InlineData("stephenson")]
        public void SixBarClosesItsLoopsOverAFullTurn(string kind)
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.sixbar")!;
            for (var a = -180; a < 180; a += 5)
            {
                var o = t.Create(Vec2.Zero).WithParam("kind", kind).WithParam("angle", a);
                var p = Ports(t, o);
                var k = p["O2"].DistanceTo(p["O4"]) / 40; // model-to-box scale
                Assert.Equal(12, p["O2"].DistanceTo(p["A"]) / k, 6);
                Assert.Equal(36, p["A"].DistanceTo(p["B"]) / k, 6);
                Assert.Equal(30, p["O4"].DistanceTo(p["B"]) / k, 6);
                if (kind == "watt")
                {
                    Assert.Equal(24, p["O4"].DistanceTo(p["C"]) / k, 6);
                    Assert.Equal(28, p["C"].DistanceTo(p["D"]) / k, 6);
                    Assert.Equal(22, p["O6"].DistanceTo(p["D"]) / k, 6);
                    // The rocker arms keep their angle: one rigid ternary link.
                    var ang = Math.Atan2((p["C"] - p["O4"]).Y, (p["C"] - p["O4"]).X) - Math.Atan2((p["B"] - p["O4"]).Y, (p["B"] - p["O4"]).X);
                    Assert.Equal(60, Geo.NormDeg(ang * 180 / Math.PI), 6);
                }
                else
                {
                    Assert.Equal(40, p["P"].DistanceTo(p["C"]) / k, 6);
                    Assert.Equal(22, p["O6"].DistanceTo(p["C"]) / k, 6);
                }
            }
        }

        [Theory]
        [InlineData("watt")]
        [InlineData("stephenson")]
        public void SixBarExplodesIntoTernaryLinks(string kind)
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.sixbar")!;
            var o = t.Create(Vec2.Zero).WithParam("kind", kind);
            var parts = t.Parts(o)!;
            Assert.Equal(3, parts.Count(p => p.TypeId == "kin.joint.ground"));
            Assert.Single(parts, p => p.TypeId == "kin.link.ternary");
            Assert.Equal(4, parts.Count(p => p.TypeId == "kin.link.binary"));
            var choice = t.Actions(o, Lang.En).Where(a => a.Id.StartsWith("kin.sixbar.kind=")).ToList();
            Assert.Equal(2, choice.Count);
            Assert.All(choice, a => Assert.Contains("kind", a.ParamKeys!));
        }

        // ------------------------------------------------------------------ pantograph

        [Fact]
        public void PantographPenCopiesTheTracerAtScale()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.pantograph")!;
            foreach (var k in new[] { 1.5, 2.0, 3.0 })
                for (var a = -180; a < 180; a += 15)
                {
                    var o = t.Create(Vec2.Zero).WithParam("scale", k).WithParam("angle", a);
                    var p = Ports(t, o);
                    // O, T and P on one line with OP = k·OT.
                    Assert.True(Math.Abs((p["T"] - p["O"]).Cross(p["P"] - p["O"])) < 1e-6 * (1 + (p["P"] - p["O"]).Length));
                    Assert.Equal(k, p["O"].DistanceTo(p["P"]) / p["O"].DistanceTo(p["T"]), 6);
                    // Parallelogram C, B, D, T and equal long bars.
                    Assert.True((p["B"] - p["C"]).DistanceTo(p["D"] - p["T"]) < 1e-6);
                    Assert.Equal(p["O"].DistanceTo(p["B"]), p["B"].DistanceTo(p["P"]), 6);
                }
            var path = r.Create(r.Item("kin.pantograph.path")!, Vec2.Zero);
            var prims = Fx.Render(r, path);
            Assert.Equal(2, prims.Count(p => p.Tag == KinTags.Path)); // traced circle and the pen's copy
            Assert.Single(prims, p => p.Tag == KinTags.Pen);
        }

        [Fact]
        public void PantographSolverRejectsTooFarTracer()
        {
            Assert.Null(Linkages.Pantograph(40, 2, new Vec2(50, 0)));
            var s = Linkages.Pantograph(40, 2, new Vec2(20, 0))!.Value;
            Assert.Equal(new Vec2(40, 0), s.P);
            Assert.True(s.C.Y < 0, "knee above the line");
        }
    }
}
