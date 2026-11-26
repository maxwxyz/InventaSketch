using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Round-4 core hooks with small test types: gear units (<see cref="IGearUnit"/>, <see cref="GearUnitDocking"/>) in the
    /// moving gear train and the torque rule of shaft lines, the rack pitch line (<see cref="AnchorRole.PitchLine"/>) in
    /// snapping, coupling and the gear train, the specific energy kind and the thermodynamics source S40.
    /// </summary>
    public class GearUnitAndRackTests
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static VarDef N => new("n", T("n", "n"), T("n", "n"), Quantities.RotationalSpeed) { Param = "speed" };
        private static VarDef Tq => new("T", T("T", "T"), T("T", "T"), Quantities.Torque) { Param = "torque" };

        private abstract class Part : ObjectType, IValueType, IShaftPart
        {
            private readonly string _id;
            private readonly ShaftPart _part;
            protected Part(string id, ShaftPart part) { _id = id; _part = part; }
            public override string Id => _id;
            public override I18nText Name => new(_id);
            public override Size2 DefaultSize => new(20, 20);
            public virtual IReadOnlyList<VarDef> Variables => new[] { N, Tq };
            public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();
            ShaftPart IShaftPart.ShaftPart(SketchObject o) => _part;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        private sealed class ShaftType : Part
        {
            public ShaftType() : base("t.shaft", ShaftPart.PassThrough) { }
            public override Size2 DefaultSize => new(100, 6);
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Shaft(new Vec2(-o.Size.W / 2, 0), new Vec2(o.Size.W / 2, 0), o.Size.H / 2) };
        }

        private sealed class MachineType : Part
        {
            public MachineType() : base("t.machine", ShaftPart.Member) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => new[] { Anchor.Hub(Vec2.Zero, 0) };
        }

        /// <summary>A gear seen along its axis: hub at the centre, pitch radius = half the box width.</summary>
        private sealed class GearType : Part
        {
            public GearType() : base("t.gear", ShaftPart.Member) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Hub(Vec2.Zero, 0), Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, o.Size.W / 2) };
        }

        /// <summary>A rack 100 mm long whose pitch line runs along its box centre, teeth up (−y).</summary>
        private sealed class RackType : ObjectType
        {
            public override string Id => "t.rack";
            public override I18nText Name => new("Rack");
            public override Size2 DefaultSize => new(100, 10);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.PitchLine(new Vec2(-o.Size.W / 2, 0), new Vec2(o.Size.W / 2, 0)) };
        }

        /// <summary>
        /// A gear unit with member ports "in" (x = −10) and "out" (x = +10). Fixed ratio: ω_in = 3·ω_out. Two-DOF variant
        /// ("t.diff"): ω_in + ω_out − 2·ω_case = 0, the case held when the object's flag "held" is set.
        /// </summary>
        private sealed class UnitType : ObjectType, IGearUnit, IShaftPart
        {
            private readonly bool _diff;
            public UnitType(bool diff) { _diff = diff; }
            public override string Id => _diff ? "t.diff" : "t.unit";
            public override I18nText Name => new(Id);
            public override Size2 DefaultSize => new(20, 20);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Point(AnchorRole.Hub, new Vec2(-10, 0), "in"), Anchor.Point(AnchorRole.Hub, new Vec2(10, 0), "out") };
            ShaftPart IShaftPart.ShaftPart(SketchObject o) => ShaftPart.Outside;
            public IReadOnlyList<GearUnitPort> MemberPorts(SketchObject o) => new[] { new GearUnitPort("in", "in"), new GearUnitPort("out", "out") };
            public IReadOnlyList<IReadOnlyDictionary<string, double>> SpeedRelations(SketchObject o)
            {
                if (!_diff) return new[] { new Dictionary<string, double> { ["in"] = 1, ["out"] = -3 } };
                var rows = new List<IReadOnlyDictionary<string, double>> { new Dictionary<string, double> { ["in"] = 1, ["out"] = 1, ["case"] = -2 } };
                if (o.Flag("held", false)) rows.Add(new Dictionary<string, double> { ["case"] = 1 });
                return rows;
            }
        }

        private sealed class TestModule : IModule
        {
            public string Id => "tg";
            public I18nText Name => new("Gear unit test");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[]
            {
                new ShaftType(), new MachineType(), new GearType(), new RackType(), new UnitType(false), new UnitType(true),
            };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        private static Registry Reg() => new Registry().Register(new TestModule());

        private static SketchObject Make(Registry r, string type, string id, double x, double y) => r.Type(type)!.Create(new Vec2(x, y)) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Gear units");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        /// <summary>
        /// Input shaft s1 (x 0 … 100) docked on the unit's "in" port (x = 95), output shaft s2 (x 110 … 210) on its "out" port
        /// (x = 115), all at y = 50.
        /// </summary>
        private static SketchDocument TwoShafts(Registry r, string unitType, params SketchObject[] more)
            => Doc(new[] { Make(r, "t.shaft", "s1", 50, 50), Make(r, unitType, "u", 105, 50), Make(r, "t.shaft", "s2", 160, 50) }.Concat(more).ToArray());

        // ------------------------------------------------------------------ gear units

        [Fact]
        public void ADockedPartSitsOnTheMemberWhosePortItTouches()
        {
            var r = Reg();
            var doc = TwoShafts(r, "t.unit");
            Assert.Equal("in", GearUnitDocking.DockedPort(r, doc, doc.Get("u")!, doc.Get("s1")!)!.Member);
            Assert.Equal("out", GearUnitDocking.DockedPort(r, doc, doc.Get("u")!, doc.Get("s2")!)!.Member);
            // One shaft through both ports would lock the unit: docked on none.
            var through = Doc(Make(r, "t.unit", "u", 105, 50), Make(r, "t.shaft", "s", 105, 50));
            Assert.Null(GearUnitDocking.DockedPort(r, through, through.Get("u")!, through.Get("s")!));
            Assert.False(GearUnitDocking.IsUnit(r, doc.Get("s1")));
        }

        [Fact]
        public void TheGearTrainTurnsTheOutputShaftWithTheUnitRatioNotRigidly()
        {
            var r = Reg();
            var doc = TwoShafts(r, "t.unit");
            var w = GearTrain.Speeds(r, doc, "s1");
            Assert.Equal(1, w["s1"], 9);
            // ω_in = 3·ω_out: the output shaft turns at a third, not with the input.
            Assert.Equal(1.0 / 3, w["s2"], 9);
            Assert.DoesNotContain(w.Keys, k => k.Contains('\u0001'));
        }

        [Fact]
        public void ATwoDegreeOfFreedomUnitStaysUnresolvedUnlessAMemberIsHeld()
        {
            var r = Reg();
            var free = GearTrain.Speeds(r, TwoShafts(r, "t.diff"), "s1");
            Assert.True(free.ContainsKey("s1"));
            Assert.False(free.ContainsKey("s2"));
            // Case held: the output turns backwards at the same speed (ω_in + ω_out = 0).
            var held = TwoShafts(r, "t.diff");
            held = Edits.Update(held, new[] { "u" }, o => o.WithParam("held", true));
            Assert.Equal(-1, GearTrain.Speeds(r, held, "s1")["s2"], 9);
        }

        [Fact]
        public void ADockedMemberCountsAsATorquePartOfTheShaftLine()
        {
            var r = Reg();
            // One machine on the input shaft and the unit's input member: two parts take or give torque.
            var one = TwoShafts(r, "t.unit", Make(r, "t.machine", "m", 20, 50));
            var line = ShaftLines.Of(r, one).LineOf("s1")!;
            Assert.Equal(1, line.Ports);
            Assert.True(line.CarriesTorque);
            // A second machine makes three: a branch, the torque is not passed (as the mechanical topology counts).
            var two = TwoShafts(r, "t.unit", Make(r, "t.machine", "m", 20, 50), Make(r, "t.machine", "m2", 60, 50));
            var branch = ShaftLines.Of(r, two).LineOf("s1")!;
            Assert.Equal(1, branch.Ports);
            Assert.False(branch.CarriesTorque);
            // Without a unit nothing changes: two machines on a shaft still pass the torque.
            var plain = Doc(Make(r, "t.shaft", "s1", 50, 50), Make(r, "t.machine", "m", 20, 50), Make(r, "t.machine", "m2", 60, 50));
            var l = ShaftLines.Of(r, plain).LineOf("s1")!;
            Assert.Equal(0, l.Ports);
            Assert.True(l.CarriesTorque);
        }

        // ------------------------------------------------------------------ rack and pinion

        /// <summary>A rack whose pitch line runs at y = 100 from x = 50 to 150 (teeth up) and a gear of radius 10 above it.</summary>
        private static SketchDocument RackAndGear(Registry r, double gearY = 90)
            => Doc(Make(r, "t.rack", "rack", 100, 100), Make(r, "t.gear", "g", 100, gearY));

        [Fact]
        public void APitchLineKeepsItsTeethSideWhenMirrored()
        {
            var line = Anchor.PitchLine(new Vec2(-50, 0), new Vec2(50, 0));
            Assert.Equal(new Vec2(0, -1), line.TeethSide());
            var mirrored = line.Transform(Matrix2D.Scaling(1, -1));
            Assert.Equal(0, mirrored.TeethSide()!.Value.X, 9);
            Assert.Equal(1, mirrored.TeethSide()!.Value.Y, 9);
            Assert.Null(Anchor.Segment(AnchorRole.Axis, Vec2.Zero, new Vec2(1, 0)).TeethSide());
        }

        [Fact]
        public void APinionSnapsTangentToThePitchLineAndTheRackOntoAPinion()
        {
            var line = Anchor.PitchLine(new Vec2(50, 100), new Vec2(150, 100));
            var gear = Anchor.Circle(AnchorRole.Pitch, new Vec2(100, 89), 10);
            var s = Snapper.SnapMove(new[] { gear }, Vec2.Zero, new[] { line }, new SnapOptions { Tolerance = 2 });
            Assert.Equal("mesh", s.Kind);
            Assert.Equal(0, s.Delta.X, 9);
            Assert.Equal(1, s.Delta.Y, 9);
            // Below the rack (back side) it does not snap to the teeth.
            Assert.NotEqual("mesh", Snapper.SnapMove(new[] { gear with { P = new Vec2(100, 111) } }, Vec2.Zero, new[] { line }, new SnapOptions { Tolerance = 2 }).Kind);
            // The rack moved onto a gear: its pitch line becomes tangent below the gear.
            var target = Anchor.Circle(AnchorRole.Pitch, new Vec2(100, 88.5), 10);
            var m = Snapper.SnapMove(new[] { line }, Vec2.Zero, new[] { target }, new SnapOptions { Tolerance = 2 });
            Assert.Equal("mesh", m.Kind);
            Assert.Equal(-1.5, m.Delta.Y, 9);
            Assert.Equal("pitch line", SnapLabel.For(line, Lang.En));
            Assert.Equal("Wälzgerade", SnapLabel.For(line, Lang.De));
        }

        [Fact]
        public void TheCouplingMeshesAPinionWithTheRackAndAGrownPinionPushesTheRack()
        {
            var r = Reg();
            var doc = RackAndGear(r);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Mesh && l.A == "g" && l.B == "rack");
            Assert.DoesNotContain(Coupling.Graph(r, RackAndGear(r, 110)), l => l.Kind == LinkKind.Mesh);
            // The pinion grows from radius 10 to 12: the rack moves 2 mm away, the pinion stays.
            var after = Coupling.Propagate(r, doc, Edits.Update(doc, new[] { "g" }, o => o with { Size = new Size2(24, 24) }));
            Assert.Equal(102, after.Get("rack")!.Transform.Y, 9);
            Assert.Equal(100, after.Get("rack")!.Transform.X, 9);
            Assert.Equal(90, after.Get("g")!.Transform.Y, 9);
        }

        [Fact]
        public void TurningThePinionMovesTheRackByOmegaTimesR()
        {
            var r = Reg();
            var doc = RackAndGear(r);
            var w = GearTrain.Speeds(r, doc, "g");
            Assert.Equal(10, w["rack"], 9);
            // A quarter turn clockwise on screen rolls the contact point (bottom of the gear) to the left: s = r·π/2.
            var turned = GearTrain.Turn(r, doc, "g", 90);
            Assert.Equal(100 - 10 * Math.PI / 2, turned.Get("rack")!.Transform.X, 6);
            Assert.Equal(100, turned.Get("rack")!.Transform.Y, 9);
        }

        // ------------------------------------------------------------------ quantities and sources

        [Fact]
        public void SpecificEnergyParsesKilojoulePerKilogram()
        {
            var kind = Quantities.SpecificEnergy;
            Assert.Equal(178.9e3, ValueParser.Parse("178.9 kJ/kg", kind, Lang.En, null).Value, 6);
            Assert.Equal(250, ValueParser.Parse("250 J/kg", kind, Lang.En, null).Value, 9);
            Assert.Equal(1.2e6, ValueParser.Parse("1,2 MJ/kg", kind, Lang.De, null).Value, 6);
            Assert.Equal("kJ/kg", kind.DefaultUnit.Symbol);
            Assert.Contains(kind, Quantities.All);
            Assert.Same(kind, Quantities.Get("specificEnergy"));
            Assert.Same(kind, Quantities.ForDimension(Quantities.Energy.Dimension / Quantities.Mass_.Dimension));
        }

        [Fact]
        public void TheThermodynamicsTextbooksAreSourceS40()
        {
            var s = ValueSources.Get("S40")!;
            Assert.Contains("Baehr", s.En);
            Assert.Contains("Çengel", s.En);
            Assert.Contains("Thermodynamik", s.De);
            Assert.Contains("S40", ValueSources.Ids);
            Assert.Contains("Hecht", ValueSources.Get("S41")!.En);
        }
    }
}
