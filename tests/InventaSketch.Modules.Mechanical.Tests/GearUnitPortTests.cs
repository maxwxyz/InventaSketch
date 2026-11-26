using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Round 6 of the gear units: ports on the outer contour (ring gear of the planetary and Ravigneaux sets, Ravigneaux
    /// carrier, differential case), a gear meshing with the outer teeth of a set's ring, and a pinion rolling along a fixed
    /// rack. Every expected value is a hand calculation (Willis, z·n, v = ω·r).
    /// </summary>
    public class GearUnitPortTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id, string key = "n") => t.Get(id, key)!.Value!.Value / RpmToSi;
        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

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
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Ports");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static RenderContext Ctx(Registry r) => new RenderContext(SketchDocument.Create()).For(r);

        private static Vec2 Port(Registry r, SketchObject o, string id)
            => r.PageAnchors(o, Ctx(r)).Single(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == id).P;

        private static double PitchRadius(Registry r, SketchObject o)
            => r.PageAnchors(o, Ctx(r)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch).Radius;

        // ------------------------------------------------------------------ ring gear and case ports

        /// <summary>
        /// Planetary set, carrier held: sun shaft at 1000 min⁻¹ with 10 N·m, a drum shaft on the ring port. Willis:
        /// n_r = −1000·20/52 = −384.6 min⁻¹, so the drum runs at 384.6 min⁻¹; T_r = T_s·z_r/z_s = 26 N·m (P = 1047 W in and out).
        /// </summary>
        [Fact]
        public void ADrumOnThePlanetaryRingPortTakesTheRing()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0).WithParam("speedc", "0 rpm");
            var ring = Port(r, set, "ring");
            Assert.Equal(-24, ring.Y, 9);
            var input = Shaft(r, "in", 15).WithParam("speed", "1000 rpm").WithParam("torque", "10 N·m");
            var drum = Shaft(r, "drum", ring.X, ring.Y);
            var doc = Doc(set, input, drum);
            Assert.Contains(("drum", "ring"), MechTopology.Of(r, doc).DockedOn("p"));
            Assert.Equal("ring", GearUnitDocking.DockedPort(r, doc, set, drum)!.Member);

            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(-1000.0 * 20 / 52, Rpm(t, "p", "nr"), 9);
            Assert.Equal(1000.0 * 20 / 52, Rpm(t, "drum"), 9);
            Assert.Equal(26, Val(t, "drum", "T"), 9);
            Assert.Equal(Val(t, "in", "P"), Val(t, "drum", "P"), 6);
            Assert.Equal(Provenance.Linked, t.Get("drum", "n")!.Provenance);

            // The moving gear train turns the drum with the ring member: backwards at 20/52.
            Assert.Equal(-20.0 / 52, GearTrain.Speeds(r, doc, "in")["drum"], 9);
        }

        /// <summary>
        /// Ravigneaux set, ring held: small sun shaft at 1000 min⁻¹, a drum on the carrier port. Willis with planet pairs:
        /// 1000 − n_c = (52/14)·(0 − n_c) → n_c = −1000·14/38 = −368.4 min⁻¹.
        /// </summary>
        [Fact]
        public void ADrumOnTheRavigneauxCarrierPortTakesTheCarrier()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.ravigneaux.stick", "rv", 0, 0).WithParam("speedr", "0 rpm");
            var carrier = Port(r, set, "carrier");
            var small = Shaft(r, "s1", -97).WithParam("speed", "1000 rpm");
            var drum = Shaft(r, "cd", carrier.X - 80, carrier.Y);
            var doc = Doc(set, small, drum);
            Assert.Contains(("cd", "carrier"), MechTopology.Of(r, doc).DockedOn("rv"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(-1000.0 * 14 / 38, Rpm(t, "rv", "nc"), 9);
            Assert.Equal(1000.0 * 14 / 38, Rpm(t, "cd"), 9);
            Assert.Equal(-14.0 / 38, GearTrain.Speeds(r, doc, "s1")["cd"], 9);
        }

        /// <summary>
        /// Ravigneaux set, carrier held: a drum on the ring port at 400 min⁻¹ drives the ring (positive sense), the large sun
        /// turns at −(52/20)·400 = −1040 min⁻¹ and its shaft at 1040 min⁻¹.
        /// </summary>
        [Fact]
        public void ADrumOnTheRavigneauxRingPortDrivesTheRing()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.ravigneaux.stick", "rv", 0, 0).WithParam("speedc", "0 rpm");
            var ring = Port(r, set, "ring");
            var drum = Shaft(r, "rd", ring.X, ring.Y).WithParam("speed", "400 rpm");
            var large = Shaft(r, "s2", 17);
            var doc = Doc(set, drum, large);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(400, Rpm(t, "rv", "nr"), 9);
            Assert.Equal(1040, Rpm(t, "s2"), 9);
        }

        /// <summary>
        /// Differential: a drive on the case port (crown wheel) at 300 min⁻¹ with 500 N·m, left half shaft at 400 min⁻¹ (in a
        /// curve): right half shaft 2·300 − 400 = 200 min⁻¹, 250 N·m on each. With the left wheel held the right one turns
        /// twice as fast as the case.
        /// </summary>
        [Fact]
        public void ADriveOnTheDifferentialCasePortTurnsTheCase()
        {
            var r = Fx.Reg();
            foreach (var view in new[] { "mech.differential.stick", "mech.differential.section" })
            {
                var d = Make(r, view, "d", 0, 0);
                var casePort = Port(r, d, "case");
                Assert.Equal(-13, casePort.X, 9);
                Assert.Equal(-19.5, casePort.Y, 9);
                var drive = Shaft(r, "cs", casePort.X - 80, casePort.Y).WithParam("speed", "300 rpm").WithParam("torque", "500 N·m");
                var left = Shaft(r, "l", -100).WithParam("speed", "400 rpm");
                var right = Shaft(r, "rt", 20);
                var doc = Doc(d, drive, left, right);
                Assert.Contains(("cs", "case"), MechTopology.Of(r, doc).DockedOn("d"));
                var t = Solve(doc, r);
                Assert.Empty(t.Conflicts);
                Assert.Equal(300, Rpm(t, "d", "nc"), 9);
                Assert.Equal(200, Rpm(t, "rt"), 9);
                Assert.Equal(250, Val(t, "l", "T"), 9);
                Assert.Equal(250, Val(t, "rt", "T"), 9);

                var held = Doc(d.WithParam("speed1", "0 rpm"), drive, Shaft(r, "l", -100), right);
                Assert.Equal(2, GearTrain.Speeds(r, held, "cs")["rt"], 9);
            }
        }

        // ------------------------------------------------------------------ outer teeth on the ring

        /// <summary>The outer teeth (parameter "outer") add a pitch circle with the ring member's port id first; off by default.</summary>
        [Fact]
        public void OuterTeethAreAnOptionWithTheirOwnPitchCircle()
        {
            var r = Fx.Reg();
            foreach (var item in new[] { "mech.planetary.axial", "mech.planetary.stick", "mech.ravigneaux.axial", "mech.ravigneaux.stick" })
            {
                var plain = Make(r, item, "p", 0, 0);
                Assert.DoesNotContain(r.PageAnchors(plain, Ctx(r)), a => a.Id == Gears.OuterTeethId);
                var outer = plain.WithParam("outer", true);
                var first = r.PageAnchors(outer, Ctx(r)).First(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner);
                Assert.Equal(Gears.OuterTeethId, first.Id);
                Assert.Equal(AnchorRole.Pitch, first.Role);
                Assert.Equal("ring", GearUnitDocking.MeshPort(r, outer, r.PageAnchors(outer, Ctx(r)).ToList())!.Member);
                Assert.Null(GearUnitDocking.MeshPort(r, plain, r.PageAnchors(plain, Ctx(r)).ToList()));
                // Not exploded: the ring gear part has inner teeth only.
                Assert.Null(r.Type(outer.Type)!.Parts(outer));
            }
            // The toggle in the context bar switches them.
            var set = Make(r, "mech.planetary.axial", "p", 0, 0);
            var toggle = r.Type(set.Type)!.Actions(set, Ctx(r)).Single(a => a.Id == "param.outer");
            Assert.True(toggle.Apply(set).Flag("outer", false));
        }

        /// <summary>
        /// Planetary set with outer teeth on the ring (z_o = 66), carrier held; a spur gear z = 30 meshing outside at
        /// 600 min⁻¹ with 5 N·m: n_r = 600·30/66 = 272.7 min⁻¹, n_s = −(52/20)·n_r = −709.1 min⁻¹; T_r = 5·66/30 = 11 N·m.
        /// </summary>
        [Fact]
        public void AGearOnTheOuterTeethDrivesTheRing()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.axial", "p", 0, 0).WithParam("outer", true).WithParam("speedc", "0 rpm");
            var ro = PitchRadius(r, set);
            var g = Make(r, "mech.spur.axial", "g", 0, 0).WithParam("teeth", 30);
            var rg = PitchRadius(r, g);
            g = g.WithParam("speed", "600 rpm").WithParam("torque", "5 N·m") with { Transform = g.Transform with { X = ro + rg } };
            var doc = Doc(set, g);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Mesh && new[] { l.A, l.B }.OrderBy(x => x).SequenceEqual(new[] { "g", "p" }));
            Assert.Contains(MechTopology.Of(r, doc).UnitMeshesOf("g"), m => m.Unit == "p" && m.Member.Name == "ring");

            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(600.0 * 30 / 66, Rpm(t, "p", "nr"), 9);
            Assert.Equal(-600.0 * 30 / 66 * 52 / 20, Rpm(t, "p", "ns"), 9);
            Assert.Equal(11, Val(t, "p", "Tr"), 9);
            Assert.Contains(t.Explain("p", "nr", Lang.En), s => s.RelationId == "mech.unit.mesh.speed");

            // Moving gear train: the ring turns −r_g/R_o of the gear, the set (drawn turning with its sun) −(52/20) of that.
            var w = GearTrain.Speeds(r, doc, "g");
            Assert.Equal(rg / ro * 52 / 20, w["p"], 9);
            // Backwards: the ring's speed on the set gives the gear its speed.
            var back = Doc(Make(r, "mech.planetary.axial", "p", 0, 0).WithParam("outer", true).WithParam("speedr", "-110 rpm"),
                Make(r, "mech.spur.axial", "g", ro + rg, 0).WithParam("teeth", 30));
            Assert.Equal(110.0 * 66 / 30, Rpm(Solve(back, r), "g"), 9);
        }

        /// <summary>Without outer teeth a gear touching the set's rim does not mesh with the ring.</summary>
        [Fact]
        public void WithoutOuterTeethNoGearMeshesWithTheRing()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.axial", "p", 0, 0);
            var g = Make(r, "mech.spur.axial", "g", 0, 0).WithParam("teeth", 30);
            var ro = PitchRadius(r, set.WithParam("outer", true));
            g = g with { Transform = g.Transform with { X = ro + PitchRadius(r, g) } };
            var doc = Doc(set, g);
            Assert.Empty(MechTopology.Of(r, doc).UnitMeshesOf("g"));
        }

        /// <summary>Schematic: a stick spur gear above the ring bracket meshes with the outer teeth (parallel axes).</summary>
        [Fact]
        public void AStickGearMeshesWithTheOuterTeethInTheSchematic()
        {
            var r = Fx.Reg();
            var set = Make(r, "mech.planetary.stick", "p", 0, 0).WithParam("outer", true);
            var teeth = r.PageAnchors(set, Ctx(r)).First(a => a.Id == Gears.OuterTeethId);
            var g = Make(r, "mech.spur.stick", "g", 0, 0, 8, 20).WithParam("teeth", 30);
            var rg = PitchRadius(r, g);
            g = g with { Transform = g.Transform with { X = teeth.P.X, Y = teeth.P.Y - teeth.Radius - rg } };
            var doc = Doc(set, g);
            Assert.Contains(MechTopology.Of(r, doc).UnitMeshesOf("p"), m => m.Gear == "g" && m.Member.Name == "ring");
        }

        // ------------------------------------------------------------------ fixed rack

        private static SketchObject PinionOn(Registry r, SketchObject rack, string id, double x)
        {
            var g = Make(r, "mech.spur.axial", id, 0, -60).WithParam("teeth", 20);
            var pitchY = rack.Transform.Y - 5 + 4 * 0.45;
            return g with { Transform = g.Transform with { X = x, Y = pitchY - PitchRadius(r, g) } };
        }

        /// <summary>
        /// A fixed rack stays; turning its pinion by a quarter turn moves the pinion's centre r·π/2 to the right (v = ω·r), the
        /// other way than a free rack would go. The values still take the pinion from the mesh.
        /// </summary>
        [Fact]
        public void APinionRollsAlongAFixedRack()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0).WithParam("fixed", true);
            var g = PinionOn(r, rack, "g", 5);
            var doc = Doc(rack, g);
            var radius = PitchRadius(r, g);
            Assert.Equal(radius, GearTrain.Travel(r, doc, "g")!.Value.X, 9);
            var turned = Coupling.Propagate(r, doc, Edits.Update(doc, new[] { "g" }, o => o with { Transform = o.Transform with { Rotation = o.Transform.Rotation + 90 } }));
            Assert.Equal(0, turned.Get("k")!.Transform.X, 9);
            Assert.Equal(5 + radius * Math.PI / 2, turned.Get("g")!.Transform.X, 6);
            Assert.Equal(g.Transform.Y, turned.Get("g")!.Transform.Y, 5);
            Assert.Equal("g", MechTopology.Of(r, doc).PinionOf("k"));
            // The fixed flag is a toggle in the context bar and draws ground strokes on the back of the rack.
            var free = Make(r, "mech.rack.teeth", "k", 0, 0);
            var toggle = r.Type(free.Type)!.Actions(free, Ctx(r)).Single(a => a.Id == "param.fixed");
            Assert.True(toggle.Apply(free).Flag("fixed", false));
            int Strokes(SketchObject o) => Prim.Flatten(r.RenderLocal(o, new RenderContext(Doc(o)))).Count(p => p.Tag == "ground");
            Assert.Equal(0, Strokes(free));
            foreach (var view in new[] { "teeth", "side", "stick" })
                Assert.True(Strokes(r.WithView(rack, view, Ctx(r))) > 0, view);
        }
    }
}
