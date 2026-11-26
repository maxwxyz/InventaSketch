using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Gear sets, the CVT and the rack linked to the shafts and gears they sit on (MechTopology, GearUnits): a member port
    /// docked on a shaft shares its speed and torque, the rack takes its pinion values from the meshing gear. Every expected
    /// value is a hand calculation with textbook formulas (Willis, v = ω·r, T₂ = i·η·T₁).
    /// </summary>
    public class GearUnitLinkTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id, string key = "n") => t.Get(id, key)!.Value!.Value / RpmToSi;
        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;
        private static bool Known(ValueTable t, string id, string key) => t.Get(id, key)?.Value is double;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(Registry r, string item, string id, double x, double y, double? w = null, double? h = null)
        {
            var o = r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };
            return w is null ? o : o with { Size = new Size2(w.Value, h!.Value) };
        }

        /// <summary>A schematic shaft 80 mm long whose axis runs from <paramref name="x0"/> to x0 + 80 at height <paramref name="y"/>.</summary>
        private static SketchObject Shaft(Registry r, string id, double x0, double y = 0) => Make(r, "mech.shaft.line", id, x0 + 40, y, 80, 8);

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Links");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        // ------------------------------------------------------------------ planetary set

        /// <summary>
        /// Motor shaft on the sun (z_s = 20) at 1000 min⁻¹ with 10 N·m, ring held (z_r = 52): the carrier turns at
        /// n_c = 1000·20/72 = 277.78 min⁻¹ and takes T_c = −T_s·(1 + 52/20) = −36 N·m, so the output shaft runs at 277.78 min⁻¹
        /// with 36 N·m (P = 1047 W on both shafts).
        /// </summary>
        [Fact]
        public void MotorShaftOnThePlanetarySunDrivesTheCarrierShaft()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0).WithParam("speedr", "0 rpm");
            var motor = Shaft(r, "in", 15).WithParam("speed", "1000 rpm").WithParam("torque", "10 N·m");
            var output = Shaft(r, "out", -95);
            var doc = Doc(set, motor, output);
            var topo = MechTopology.Of(r, doc);
            Assert.Contains(("in", "sun"), topo.DockedOn("p"));
            Assert.Contains(("out", "carrier"), topo.DockedOn("p"));

            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1000, Rpm(t, "p", "ns"), 9);
            Assert.Equal(1000.0 * 20 / 72, Rpm(t, "p", "nc"), 9);
            Assert.Equal(1000.0 * 20 / 72, Rpm(t, "out"), 9);
            // Taken as the magnitude of the signed carrier speed: passed on through the port, so it shows as linked (as the
            // CVT's plain equalities), while the carrier's own speed is derived by Willis.
            Assert.Equal(Provenance.Linked, t.Get("out", "n")!.Provenance);
            Assert.Equal(Provenance.Linked, t.Get("out", "T")!.Provenance);
            Assert.Equal(Provenance.Derived, t.Get("p", "nc")!.Provenance);
            Assert.Equal(-36, Val(t, "p", "Tc"), 9);
            Assert.Equal(36, Val(t, "out", "T"), 9);
            Assert.Equal(Val(t, "in", "P"), Val(t, "out", "P"), 6);
            Assert.Contains(t.Explain("out", "n", Lang.En), s => s.RelationId == "mech.unit.speed.magnitude");
        }

        /// <summary>Every unit's schematic shows its member ports as hubs on the axis, inside the box; a docked shaft takes the unit along.</summary>
        [Fact]
        public void SchematicUnitsExposeMemberPortsThatCouple()
        {
            var r = Fx.Reg();
            var ctx = new RenderContext(SketchDocument.Create()).For(r);
            foreach (var (item, ids) in new[]
            {
                ("mech.planetary.stick", new[] { "sun", "carrier", "ring" }), ("mech.ravigneaux.stick", new[] { "sun1", "sun2", "carrier", "ring" }),
                ("mech.differential.stick", new[] { "left", "right", "case" }), ("mech.differential.section", new[] { "left", "right", "case" }),
                ("mech.harmonic.stick", new[] { "in", "out" }), ("mech.cycloidal.stick", new[] { "in", "out" }), ("mech.cvt.stick", new[] { "hub", "hub2" }),
            })
            {
                var o = Make(r, item, "u", 0, 0);
                var hubs = r.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).ToList();
                foreach (var id in ids)
                {
                    var h = hubs.Single(a => a.Id == id);
                    Assert.True(Math.Abs(h.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(h.P.Y) <= o.Size.H / 2 + 1e-9, item + " " + id);
                }
                Assert.True(GearUnits.IsUnit(o.Type), item);
            }
            var set = Make(r, "mech.planetary.stick", "p", 0, 0);
            var doc = Doc(set, Shaft(r, "in", 15));
            Assert.Contains("p", Coupling.RigidFollowers(r, doc, new[] { "in" }));
        }

        /// <summary>One shaft through both member ports would lock the set: it is docked on no member.</summary>
        [Fact]
        public void AShaftThroughTwoMembersDocksOnNone()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0).WithParam("speedr", "0 rpm");
            var through = Shaft(r, "s", -40).WithParam("speed", "1000 rpm");
            var doc = Doc(set, through);
            Assert.Empty(MechTopology.Of(r, doc).DockedOn("p"));
            var t = Solve(doc, r);
            Assert.False(Known(t, "p", "ns"));
            Assert.False(Known(t, "p", "nc"));
        }

        /// <summary>A shaft from the set's centre bore out to the right touches the bore and the sun stub: the sun.</summary>
        [Fact]
        public void TheShaftStubPortWinsOverTheCentreBore()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0);
            var s = Shaft(r, "s", 0).WithParam("speed", "500 rpm");
            var doc = Doc(set, s);
            Assert.Equal(new[] { ("s", "sun") }, MechTopology.Of(r, doc).DockedOn("p").ToArray());
            Assert.Equal(500, Rpm(Solve(doc, r), "p", "ns"), 9);
        }

        /// <summary>Axial view: a spline profile concentric with the set's bore sits on the sun.</summary>
        [Fact]
        public void AxialPlanetarySetTakesTheSunSpeedFromItsBore()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.axial", "p", 0, 0).WithParam("speedc", "0 rpm");
            var spline = Make(r, "mech.spline.axial", "k", 0, 0).WithParam("speed", "1300 rpm");
            var doc = Doc(set, spline);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1300, Rpm(t, "p", "ns"), 9);
            // Carrier held: the ring turns backwards, n_r = −1300·20/52 = −500 min⁻¹.
            Assert.Equal(-500, Rpm(t, "p", "nr"), 9);
        }

        /// <summary>
        /// The docked member counts as a torque part of the line: one gear with 10 N·m on the motor shaft and the sun pass the
        /// torque (T_s = 10 N·m); with a second gear on the shaft the torque splits in an unknown way and nothing is linked.
        /// </summary>
        [Fact]
        public void ADockedMemberCountsTowardsTheLineTorqueRule()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0);
            var s = Shaft(r, "s", 15).WithParam("speed", "1000 rpm");
            var g1 = Make(r, "mech.spur.stick", "g1", 60, 0, 8, 20).WithParam("teeth", 20).WithParam("torque", "10 N·m");
            var t1 = Solve(Doc(set, s, g1), r);
            Assert.Empty(t1.Conflicts);
            Assert.Equal(10, Val(t1, "p", "Ts"), 9);
            Assert.Equal(10, Val(t1, "s", "T"), 9);
            Assert.Equal(1000, Rpm(t1, "g1"), 9);

            var g2 = Make(r, "mech.spur.stick", "g2", 80, 0, 8, 20).WithParam("teeth", 30);
            var t2 = Solve(Doc(set, s, g1, g2), r);
            Assert.Empty(t2.Conflicts);
            Assert.Equal(1000, Rpm(t2, "p", "ns"), 9);
            Assert.False(Known(t2, "p", "Ts"));
            Assert.False(Known(t2, "s", "T"));
        }

        // ------------------------------------------------------------------ Ravigneaux and differential

        /// <summary>Ravigneaux set, carrier held: small sun shaft at 1000 min⁻¹ → large sun at −700 min⁻¹, its shaft at 700 min⁻¹.</summary>
        [Fact]
        public void RavigneauxSunShaftsFollowBothWillisEquations()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.ravigneaux.stick", "rv", 0, 0).WithParam("speedc", "0 rpm");
            var small = Shaft(r, "s1", -97).WithParam("speed", "1000 rpm");
            var large = Shaft(r, "s2", 17);
            var doc = Doc(set, small, large);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(-700, Rpm(t, "rv", "ns2"), 9);
            Assert.Equal(700, Rpm(t, "s2"), 9);
        }

        /// <summary>
        /// Differential with the case at 300 min⁻¹ and the left half shaft at 400 min⁻¹ (in a curve): the right half shaft runs
        /// at 2·300 − 400 = 200 min⁻¹; 500 N·m on the case split into 250 N·m on each half shaft.
        /// </summary>
        [Fact]
        public void DifferentialHalfShaftsTakeTheSpeedsAndTheTorqueSplit()
        {
            var r = Fx.Reg();
            var d = Make(r, "mech.differential.stick", "d", 0, 0).WithParam("speedc", "300 rpm").WithParam("torquec", "500 N·m");
            var left = Shaft(r, "l", -100).WithParam("speed", "400 rpm");
            var right = Shaft(r, "rt", 20);
            var doc = Doc(d, left, right);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(400, Rpm(t, "d", "n1"), 9);
            Assert.Equal(200, Rpm(t, "rt"), 9);
            Assert.Equal(250, Val(t, "l", "T"), 9);
            Assert.Equal(250, Val(t, "rt", "T"), 9);
        }

        // ------------------------------------------------------------------ strain wave and cycloidal drives

        /// <summary>Strain wave gear i = −100: motor shaft 3000 min⁻¹, 2 N·m, η = 80 % → output shaft 30 min⁻¹ with 160 N·m.</summary>
        [Fact]
        public void StrainWaveGearDrivesItsOutputShaft()
        {
            var r = Fx.Reg();
            var h = Make(r, "mech.harmonic.stick", "h", 0, 0).WithParam("efficiency", "80 %");
            var motor = Shaft(r, "in", -92).WithParam("speed", "3000 rpm").WithParam("torque", "2 N·m");
            var output = Shaft(r, "out", 12);
            var doc = Doc(h, motor, output);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(-30, Rpm(t, "h", "n2"), 9);
            Assert.Equal(30, Rpm(t, "out"), 9);
            Assert.Equal(160, Val(t, "out", "T"), 9);
        }

        /// <summary>Cycloidal drive i = −11 (11 lobes): the output shaft at 100 min⁻¹ gives the motor shaft 1100 min⁻¹.</summary>
        [Fact]
        public void CycloidalDriveLinksBothShaftsBackwards()
        {
            var r = Fx.Reg();
            var c = Make(r, "mech.cycloidal.stick", "c", 0, 0);
            var motor = Shaft(r, "in", -92);
            var output = Shaft(r, "out", 12).WithParam("speed", "100 rpm");
            var doc = Doc(c, motor, output);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(100, Rpm(t, "c", "n2"), 9);
            Assert.Equal(1100, Rpm(t, "in"), 9);
        }

        // ------------------------------------------------------------------ CVT

        /// <summary>CVT at ratio 2: input shaft 3000 min⁻¹, 100 N·m, η = 90 % → output shaft 1500 min⁻¹ with 180 N·m.</summary>
        [Fact]
        public void CvtRatioIsCarriedToTheOutputShaft()
        {
            var r = Fx.Reg();
            var cvt = Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("ratio", 2.0).WithParam("efficiency", "90 %");
            var input = Shaft(r, "in", -40, -15).WithParam("speed", "3000 rpm").WithParam("torque", "100 N·m");
            var output = Shaft(r, "out", -40, 15);
            var doc = Doc(cvt, input, output);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(3000, Rpm(t, "v", "n1"), 9);
            Assert.Equal(1500, Rpm(t, "out"), 9);
            Assert.Equal(180, Val(t, "out", "T"), 9);
            Assert.Equal(Provenance.Linked, t.Get("out", "T")!.Provenance);
        }

        /// <summary>
        /// CVT (ratio 2, η = 90 %) and a planetary set on one intermediate shaft: the shaft takes the CVT output (1500 min⁻¹,
        /// 180 N·m) to the carrier; with the ring held the sun overdrives, n_s = 1500·72/20 = 5400 min⁻¹ with
        /// T_s = −180/3.6 = −50 N·m, so its shaft runs at 5400 min⁻¹ with 50 N·m (270 kW·s⁻¹ in and out of the set).
        /// </summary>
        [Fact]
        public void TwoUnitsOnOneShaftPassTheTorqueThrough()
        {
            var r = Fx.Reg();
            var cvt = Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("ratio", 2.0).WithParam("efficiency", "90 %");
            var input = Shaft(r, "in", -40, -15).WithParam("speed", "3000 rpm").WithParam("torque", "100 N·m");
            var middle = Shaft(r, "mid", -40, 15);
            var set = Make(r, "mech.planetary.stick", "p", 55, 15).WithParam("speedr", "0 rpm");
            var output = Shaft(r, "out", 70, 15);
            var doc = Doc(cvt, input, middle, set, output);
            var topo = MechTopology.Of(r, doc);
            Assert.Contains(("mid", "carrier"), topo.DockedOn("p"));
            Assert.Contains(("mid", "out"), topo.DockedOn("v"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1500, Rpm(t, "mid"), 9);
            Assert.Equal(180, Val(t, "mid", "T"), 9);
            Assert.Equal(5400, Rpm(t, "out"), 6);
            Assert.Equal(50, Val(t, "out", "T"), 6);
        }

        // ------------------------------------------------------------------ rack and pinion

        private static SketchObject PinionOn(Registry r, SketchObject rack, string id, double x, double gap = 0)
        {
            var g = Make(r, "mech.spur.axial", id, 0, -60).WithParam("teeth", 20);
            var pitch = r.PageAnchors(g, new RenderContext(SketchDocument.Create()).For(r)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);
            // The rack's pitch line (teeth view, height 10): 45 % of the 4 mm tooth depth below the tips at −5.
            var pitchY = rack.Transform.Y - 5 + 4 * 0.45;
            return g with { Transform = g.Transform with { X = x, Y = pitchY - pitch.Radius - gap } };
        }

        /// <summary>
        /// Pinion z = 20, m = 2 mm (d = 40 mm) at 60 min⁻¹ (ω = 2π s⁻¹) and 10 N·m meshing with the rack: v = ω·r =
        /// 2π·0.02 = 0.1257 m/s and F = T/r = 500 N on the rack; P = 62.83 W.
        /// </summary>
        [Fact]
        public void RackTakesItsSpeedFromTheMeshingPinion()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0).WithParam("module", 2.0);
            var pinion = PinionOn(r, rack, "g", 5).WithParam("module", 2.0).WithParam("speed", "60 rpm").WithParam("torque", "10 N·m");
            var doc = Doc(rack, pinion);
            Assert.Equal("g", MechTopology.Of(r, doc).PinionOf("k"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(60, Rpm(t, "k", "np"), 9);
            Assert.Equal(20, Val(t, "k", "zp"), 9);
            Assert.Equal(2 * Math.PI * 0.02, Val(t, "k", "v"), 12);
            Assert.Equal(500, Val(t, "k", "F"), 9);
            Assert.Equal(Provenance.Linked, t.Get("k", "np")!.Provenance);

            // Backwards: the rack speed gives the pinion (and its shaft line) the speed.
            var back = PinionOn(r, rack, "g", 5).WithParam("module", 2.0);
            var t2 = Solve(Doc(rack.WithParam("linSpeed", "0.5 m/s"), back), r);
            Assert.Equal(0.5 / 0.02 / RpmToSi, Rpm(t2, "g"), 9);
        }

        [Fact]
        public void AGearAwayFromThePitchLineOrASecondPinionIsNoMesh()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0);
            var off = PinionOn(r, rack, "g", 5, gap: 3);
            Assert.Null(MechTopology.Of(r, Doc(rack, off)).PinionOf("k"));
            var beyond = PinionOn(r, rack, "g", 45);
            Assert.Null(MechTopology.Of(r, Doc(rack, beyond)).PinionOf("k"));
            // Below the rack (back side, no teeth): no mesh.
            var below = PinionOn(r, rack, "g", 5);
            var mirrored = below with { Transform = below.Transform with { Y = -below.Transform.Y + 2 * (4 * 0.45 - 5) } };
            Assert.Null(MechTopology.Of(r, Doc(rack, mirrored)).PinionOf("k"));
            // Two pinions on one rack: the rack takes neither.
            var a = PinionOn(r, rack, "a", -15);
            var b = PinionOn(r, rack, "b", 15);
            Assert.Null(MechTopology.Of(r, Doc(rack, a, b)).PinionOf("k"));
            Assert.Equal("a", MechTopology.Of(r, Doc(rack, a)).PinionOf("k"));
        }

        /// <summary>Turned racks keep their pitch line: a rack turned by 180° meshes with a pinion below it.</summary>
        [Fact]
        public void ATurnedRackMeshesOnItsTeethSide()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0);
            var turned = rack with { Transform = rack.Transform with { Rotation = 180 } };
            var g = PinionOn(r, rack, "g", 5);
            var below = g with { Transform = g.Transform with { X = -5, Y = -g.Transform.Y } };
            Assert.Equal("g", MechTopology.Of(r, Doc(turned, below)).PinionOf("k"));
        }
    }
}
