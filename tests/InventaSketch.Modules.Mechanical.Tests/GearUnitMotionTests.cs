using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// The mechanical gear units and the rack in the core hooks of round 4: the moving gear train (<see cref="GearTrain"/>)
    /// turns the shafts docked on a gear set or CVT with the unit's ratio instead of rigidly, leaves a planetary set without a
    /// held member unresolved and moves a rack with its pinion; the core shaft lines count a docked member like the
    /// mechanical topology; the rack's pitch line snaps a pinion tangent to it.
    /// </summary>
    public class GearUnitMotionTests
    {
        private static SketchObject Make(Registry r, string item, string id, double x, double y, double? w = null, double? h = null)
        {
            var o = r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };
            return w is null ? o : o with { Size = new Size2(w.Value, h!.Value) };
        }

        /// <summary>A schematic shaft 80 mm long whose axis runs from <paramref name="x0"/> to x0 + 80 at height <paramref name="y"/>.</summary>
        private static SketchObject Shaft(Registry r, string id, double x0, double y = 0) => Make(r, "mech.shaft.line", id, x0 + 40, y, 80, 8);

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Motion");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static RenderContext Ctx(Registry r) => new RenderContext(SketchDocument.Create()).For(r);

        // ------------------------------------------------------------------ gear units in the moving gear train

        /// <summary>
        /// Sun shaft and carrier shaft on a schematic planetary set: with the ring held (n_r = 0) the carrier shaft turns at
        /// z_s/(z_s + z_r) = 20/72 of the sun shaft; with no member held it stays unresolved (it used to turn with the sun).
        /// </summary>
        [Fact]
        public void APlanetarySetTurnsTheCarrierShaftByWillisOnlyWithAMemberHeld()
        {
            var r = Fx.Reg();
            var free = Doc(Make(r, "mech.planetary.stick", "p", 0, 0), Shaft(r, "in", 15), Shaft(r, "out", -95));
            var wf = GearTrain.Speeds(r, free, "in");
            Assert.Equal(1, wf["in"], 9);
            Assert.False(wf.ContainsKey("out"));

            var held = Doc(Make(r, "mech.planetary.stick", "p", 0, 0).WithParam("speedr", "0 rpm"), Shaft(r, "in", 15), Shaft(r, "out", -95));
            Assert.Equal(20.0 / 72, GearTrain.Speeds(r, held, "in")["out"], 9);
            // Carrier held instead: the ring turns, the carrier shaft stands.
            var carrier = Doc(Make(r, "mech.planetary.stick", "p", 0, 0).WithParam("speedc", "0 rpm"), Shaft(r, "in", 15), Shaft(r, "out", -95));
            Assert.Equal(0, GearTrain.Speeds(r, carrier, "in")["out"], 9);
        }

        /// <summary>Strain wave gear i = −z_f/(z_c − z_f) = −100 and CVT at ratio 2: fixed-ratio units turn the output shaft by 1/i.</summary>
        [Fact]
        public void FixedRatioUnitsTurnTheOutputShaftByTheirRatio()
        {
            var r = Fx.Reg();
            var harmonic = Doc(Make(r, "mech.harmonic.stick", "h", 0, 0), Shaft(r, "in", -92), Shaft(r, "out", 12));
            Assert.Equal(-0.01, GearTrain.Speeds(r, harmonic, "in")["out"], 12);
            var cycloidal = Doc(Make(r, "mech.cycloidal.stick", "c", 0, 0), Shaft(r, "in", -92), Shaft(r, "out", 12));
            Assert.Equal(-1.0 / 11, GearTrain.Speeds(r, cycloidal, "in")["out"], 12);
            var cvt = Doc(Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("ratio", 2.0), Shaft(r, "in", -40, -15), Shaft(r, "out", -40, 15));
            Assert.Equal(0.5, GearTrain.Speeds(r, cvt, "in")["out"], 12);
            // The differential with its case free leaves the second half shaft open; with the case held it turns backwards.
            var diff = Doc(Make(r, "mech.differential.stick", "d", 0, 0), Shaft(r, "l", -100), Shaft(r, "rt", 20));
            Assert.False(GearTrain.Speeds(r, diff, "l").ContainsKey("rt"));
            var held = Doc(Make(r, "mech.differential.stick", "d", 0, 0).WithParam("speedc", "0 rpm"), Shaft(r, "l", -100), Shaft(r, "rt", 20));
            Assert.Equal(-1, GearTrain.Speeds(r, held, "l")["rt"], 12);
        }

        /// <summary>An axial planetary set on a spline at its bore turns as a whole with the sun (its drawing has no separate members).</summary>
        [Fact]
        public void AnAxialSetTurnsWithTheMemberAtItsBore()
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "mech.planetary.axial", "p", 0, 0), Make(r, "mech.spline.axial", "k", 0, 0));
            Assert.Equal(1, GearTrain.Speeds(r, doc, "k")["p"], 9);
            Assert.Equal(1, GearTrain.Speeds(r, doc, "p")["k"], 9);
        }

        // ------------------------------------------------------------------ core shaft lines

        /// <summary>The core shaft lines count the sun docked on the motor shaft as a torque part, as the mechanical topology does.</summary>
        [Fact]
        public void CoreShaftLinesCountADockedMemberLikeTheMechanicalTopology()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0);
            var s = Shaft(r, "s", 15);
            var g1 = Make(r, "mech.spur.stick", "g1", 60, 0, 8, 20);
            var g2 = Make(r, "mech.spur.stick", "g2", 80, 0, 8, 20);
            foreach (var doc in new[] { Doc(set, s, g1), Doc(set, s, g1, g2) })
            {
                var core = ShaftLines.Of(r, doc).LineOf("s")!;
                var mech = MechTopology.Of(r, doc).LineOf("s")!;
                Assert.Equal(1, core.Ports);
                Assert.Equal(mech.Ports, core.Ports);
                Assert.Equal(mech.CarriesTorque, core.CarriesTorque);
            }
            Assert.True(ShaftLines.Of(r, Doc(set, s, g1)).LineOf("s")!.CarriesTorque);
            Assert.False(ShaftLines.Of(r, Doc(set, s, g1, g2)).LineOf("s")!.CarriesTorque);
        }

        // ------------------------------------------------------------------ rack and pinion

        /// <summary>The pinion z = 20 placed tangent to the rack's pitch line (teeth view, height 10: 45 % of 4 mm below the tips).</summary>
        private static SketchObject PinionOn(Registry r, SketchObject rack, string id, double x)
        {
            var g = Make(r, "mech.spur.axial", id, 0, -60).WithParam("teeth", 20);
            var pitch = r.PageAnchors(g, Ctx(r)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            var pitchY = rack.Transform.Y - 5 + 4 * 0.45;
            return g with { Transform = g.Transform with { X = x, Y = pitchY - pitch.Radius } };
        }

        [Fact]
        public void EveryRackViewHasItsPitchLineWithTheTeethUp()
        {
            var r = Fx.Reg();
            foreach (var view in new[] { "teeth", "side", "stick" })
            {
                var rack = r.WithView(Make(r, "mech.rack.teeth", "k", 0, 0), view, Ctx(r));
                var line = Assert.Single(r.PageAnchors(rack, Ctx(r)), a => a.Role == AnchorRole.PitchLine);
                Assert.Equal(Gears.RackPitchY(rack.Size.H, view), line.P.Y, 9);
                Assert.Equal(rack.Size.W, line.P.DistanceTo(line.Q), 9);
                Assert.Equal(-1, line.TeethSide()!.Value.Y, 9);
            }
        }

        /// <summary>Turning the pinion by a quarter turn moves the rack by r·π/2 along its pitch line (v = ω·r).</summary>
        [Fact]
        public void TurningThePinionMovesTheRack()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0);
            var g = PinionOn(r, rack, "g", 5);
            var doc = Doc(rack, g);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Mesh && l.A == "g" && l.B == "k");
            var radius = r.PageAnchors(g, Ctx(r)).First(a => a.Role == AnchorRole.Pitch).Radius;
            Assert.Equal(radius, GearTrain.Speeds(r, doc, "g")["k"], 9);
            // The gear above the rack turns clockwise on screen: its lowest point, on the rack, moves to the left.
            var turned = Coupling.Propagate(r, doc, Edits.Update(doc, new[] { "g" }, o => o with { Transform = o.Transform with { Rotation = o.Transform.Rotation + 90 } }));
            Assert.Equal(-radius * Math.PI / 2, turned.Get("k")!.Transform.X, 6);
            Assert.Equal(0, turned.Get("k")!.Transform.Y, 9);
            // The values still find the same pinion.
            Assert.Equal("g", MechTopology.Of(r, doc).PinionOf("k"));
        }

        /// <summary>A pinion dragged near the rack snaps tangent to its pitch line, where the values find it as the rack's pinion.</summary>
        [Fact]
        public void APinionSnapsOntoTheRackPitchLine()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0);
            var placed = PinionOn(r, rack, "g", 5);
            var near = placed with { Transform = placed.Transform with { Y = placed.Transform.Y - 0.8, X = 6 } };
            var doc = Doc(rack);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], Array.Empty<string>(), Ctx(r), new SnapOptions());
            var moving = r.PageAnchors(near, Ctx(r)).ToList();
            var s = Snapper.SnapMove(moving, Vec2.Zero, targets, new SnapOptions { Tolerance = 2 });
            Assert.Equal("mesh", s.Kind);
            Assert.Equal(AnchorRole.PitchLine, s.Target!.Role);
            var snapped = near with { Transform = near.Transform with { X = near.Transform.X + s.Delta.X, Y = near.Transform.Y + s.Delta.Y } };
            Assert.Equal(placed.Transform.Y, snapped.Transform.Y, 9);
            Assert.Equal(6, snapped.Transform.X, 9);
            Assert.Equal("g", MechTopology.Of(r, Doc(rack, snapped)).PinionOf("k"));
        }
    }
}
