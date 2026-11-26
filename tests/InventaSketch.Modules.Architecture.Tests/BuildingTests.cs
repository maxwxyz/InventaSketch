using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    /// <summary>Lift, ramp, railing, shaft, slab opening and roof plan.</summary>
    public class BuildingElementTests
    {
        private static IReadOnlyList<Primitive> Render(Registry r, SketchObject o, Lang lang = Lang.En) => r.TypeOf(o).Render(o, Fx.Ctx(lang));

        private static SketchObject Item(Registry r, string id) => r.Create(r.Item(id)!, Vec2.Zero);

        [Theory]
        [InlineData("lift")]
        [InlineData("aufzug")]
        [InlineData("rampe")]
        [InlineData("geländer")]
        [InlineData("schacht")]
        [InlineData("deckenöffnung")]
        [InlineData("walmdach")]
        [InlineData("satteldach")]
        public void NewElementsAreFoundInBothLanguages(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void LiftShowsCarWithCrossCounterweightAndDoor()
        {
            var r = Fx.Reg();
            var lift = Item(r, "arch.lift");
            var prims = Render(r, lift);
            Assert.Single(prims, p => p.Tag == "car");
            Assert.Equal(2, prims.Count(p => p.Tag == "cross"));
            Assert.Single(prims, p => p.Tag == "counterweight");
            Assert.Equal(2, prims.Count(p => p.Tag == "door"));
            var bare = Render(r, lift.WithParam("counterweight", false).WithParam("door", false));
            Assert.DoesNotContain(bare, p => p.Tag is "counterweight" or "door");
            // The car grows into the space freed by the counterweight.
            var car = Prim.Bounds(prims.Where(p => p.Tag == "car"))!.Value;
            var carBare = Prim.Bounds(bare.Where(p => p.Tag == "car"))!.Value;
            Assert.True(carBare.H > car.H);
        }

        [Fact]
        public void RampShowsArrowAndSlopeInTheUiLanguage()
        {
            var r = Fx.Reg();
            var ramp = Item(r, "arch.ramp").WithParam("slope", 6.5);
            Assert.Contains(Render(r, ramp, Lang.En).OfType<TextPrim>(), t => t.Text == "6.5 %");
            Assert.Contains(Render(r, ramp, Lang.De).OfType<TextPrim>(), t => t.Text == "6,5 %");
            Assert.Contains(Render(r, Item(r, "arch.ramp")).OfType<TextPrim>(), t => t.Text == "6 %");
            var arrow = (LinePrim)Render(r, ramp).Single(p => p.Tag == ArchTags.WalkLine);
            Assert.Equal(ArrowHead.Triangle, arrow.Style.ArrowEnd);
            Assert.True(arrow.B.X > arrow.A.X, "arrow points up the ramp (to the right)");
            var down = (LinePrim)Render(r, ramp.WithParam("down", true)).Single(p => p.Tag == ArchTags.WalkLine);
            Assert.True(down.B.X < down.A.X, "reversed arrow");
            Assert.DoesNotContain(Render(r, ramp.WithParam("showSlope", false)), p => p is TextPrim);
            // A ramp drawn upright runs along y.
            var upright = (LinePrim)Render(r, ramp with { Size = new Size2(12, 60) }).Single(p => p.Tag == ArchTags.WalkLine);
            Assert.True(Math.Abs(upright.B.Y - upright.A.Y) > 40);
        }

        [Fact]
        public void RailingHasPostsAtTheSpacingAndScale()
        {
            var r = Fx.Reg();
            // 40 mm at 1:100 = 4 m: posts every 1 m → 5 posts.
            var railing = r.Create(r.Item("arch.railing")!, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0) } };
            var (_, doc) = Fx.Doc(railing);
            var ctx = new RenderContext(doc);
            Assert.Equal(5, r.TypeOf(railing).Render(railing, ctx).Count(p => p.Tag == "post"));
            Assert.Equal(3, r.TypeOf(railing).Render(railing.WithParam("spacing", 2000.0), ctx).Count(p => p.Tag == "post"));
            var at200 = doc with { Settings = doc.Settings with { PlanScale = 200 } }; // 8 m
            Assert.Equal(9, r.TypeOf(railing).Render(railing, new RenderContext(at200)).Count(p => p.Tag == "post"));
            Assert.DoesNotContain(r.TypeOf(railing).Render(railing.WithParam("posts", false), ctx), p => p.Tag == "post");
            Assert.True(r.TypeOf(railing).UsesPlanScale);
            Assert.Single(r.TypeOf(railing).Anchors(railing, ctx), a => a.Role == AnchorRole.Rail);
            var corner = r.Create(r.Item("arch.railing.l")!, Vec2.Zero);
            Assert.Equal(3, corner.Points!.Count);
        }

        [Fact]
        public void ShaftHasASolidAndTheVoidADashedCross()
        {
            var r = Fx.Reg();
            var shaft = Render(r, Item(r, "arch.shaft")).Where(p => p.Tag == "cross").Cast<LinePrim>().ToList();
            Assert.Equal(2, shaft.Count);
            Assert.All(shaft, l => Assert.NotEqual(LineRole.Hidden, l.Style.Role));
            Assert.DoesNotContain(Render(r, Item(r, "arch.shaft").WithParam("cross", false)), p => p.Tag == "cross");
            var voids = Render(r, Item(r, "arch.void")).Where(p => p.Tag == "cross").Cast<LinePrim>().ToList();
            Assert.Equal(2, voids.Count);
            Assert.All(voids, l => Assert.Equal(LineRole.Hidden, l.Style.Role));
        }

        [Fact]
        public void GableRoofHasOneRidgeAcrossTheLongSide()
        {
            var r = Fx.Reg();
            var roof = Item(r, "arch.roof"); // 100 x 70, gable
            var prims = Render(r, roof);
            var ridge = (LinePrim)prims.Single(p => p.Tag == "ridge");
            Assert.Equal(-50, Math.Min(ridge.A.X, ridge.B.X), 6);
            Assert.Equal(50, Math.Max(ridge.A.X, ridge.B.X), 6);
            Assert.Equal(0, ridge.A.Y, 6);
            Assert.DoesNotContain(prims, p => p.Tag == "hip");
            Assert.Equal(2, prims.Count(p => p.Tag == "fall"));
            var across = (LinePrim)Render(r, roof.WithParam("ridge", "short")).Single(p => p.Tag == "ridge");
            Assert.Equal(70, Math.Abs(across.B.Y - across.A.Y), 6);
        }

        [Fact]
        public void HipRoofHasFourHipsAt45DegreesAndAShorterRidge()
        {
            var r = Fx.Reg();
            var roof = r.Create(r.Item("arch.roof.hip")!, Vec2.Zero);
            var prims = Render(r, roof);
            var ridge = (LinePrim)prims.Single(p => p.Tag == "ridge");
            // Equal pitches: the ridge is the length minus the width (100 - 70 = 30 mm).
            Assert.Equal(30, ridge.A.DistanceTo(ridge.B), 6);
            var hips = prims.Where(p => p.Tag == "hip").Cast<LinePrim>().ToList();
            Assert.Equal(4, hips.Count);
            Assert.All(hips, h => Assert.Equal(Math.Abs(h.B.X - h.A.X), Math.Abs(h.B.Y - h.A.Y), 6));
            Assert.Equal(4, prims.Count(p => p.Tag == "fall"));
            // A square hip roof is a pyramid roof: no ridge length.
            var square = (LinePrim)Render(r, roof with { Size = new Size2(60, 60) }).Single(p => p.Tag == "ridge");
            Assert.Equal(0, square.A.DistanceTo(square.B), 6);
            Assert.Contains(Render(r, roof.WithParam("walls", true)), p => p.Tag == "wall-below");
        }

        [Fact]
        public void NewElementsHaveTheirActions()
        {
            var r = Fx.Reg();
            string[] Ids(string item) => r.TypeOf(Item(r, item)).Actions(Item(r, item), Lang.En).Select(a => a.Id).ToArray();
            Assert.Contains("arch.lift.counterweight", Ids("arch.lift"));
            Assert.Contains("arch.ramp.reverse", Ids("arch.ramp"));
            Assert.Contains("arch.railing.posts", Ids("arch.railing"));
            Assert.Contains("arch.shaft.cross", Ids("arch.shaft"));
            Assert.Contains(Ids("arch.roof"), id => id.StartsWith("arch.roof.kind", StringComparison.Ordinal));
            var ramp = Item(r, "arch.ramp");
            var reversed = r.TypeOf(ramp).Actions(ramp, Lang.En).Single(a => a.Id == "arch.ramp.reverse").Apply(ramp);
            Assert.True(reversed.Flag("down", false));
        }
    }

    /// <summary>Room values: area from the outline at the plan scale, height and volume.</summary>
    public class RoomValueTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Room(Registry r) =>
            r.Create(r.Item("arch.room")!, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30), new Vec2(0, 30) } };

        [Fact]
        public void RoomDeclaresAreaHeightAndVolumeWithTheBuildingServicesKeys()
        {
            var t = Fx.Reg().Type("arch.room")!;
            Assert.True(ValueModel.HasValues(t));
            Assert.Equal(Quantities.Area, ValueModel.Variable(t, "A")!.Kind);
            Assert.Equal(Quantities.Length, ValueModel.Variable(t, "h")!.Kind);
            Assert.Equal(Quantities.Volume, ValueModel.Variable(t, "V")!.Kind);
            Assert.Equal(2.5, ValueModel.Variable(t, "h")!.Default);
            Assert.Contains(t.Parameters, p => p.Key == "height" && p.Default is null);
        }

        [Fact]
        public void KnownRectangleAt1To100Gives12SquareMetresAnd30CubicMetres()
        {
            var r = Fx.Reg();
            var room = Room(r); // 40 x 30 mm at 1:100 = 4 m x 3 m
            var (_, doc) = Fx.Doc(room);
            var t = Solve(r, doc);
            var a = t.Get(room.Id, "A")!;
            Assert.Equal(12.0, a.Value!.Value, 9);
            Assert.Equal(Provenance.Derived, a.Provenance);
            Assert.Equal(2.5, t.Get(room.Id, "h")!.Value!.Value, 9);
            Assert.Equal(Provenance.Default, t.Get(room.Id, "h")!.Provenance);
            Assert.Equal(30.0, t.Get(room.Id, "V")!.Value!.Value, 9);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void EnteredHeightAndPlanScaleChangeTheValues()
        {
            var r = Fx.Reg();
            var room = Room(r).WithParam("height", 3.0);
            var (_, doc) = Fx.Doc(room);
            var t = Solve(r, doc);
            Assert.Equal(12.0, t.Get(room.Id, "A")!.Value!.Value, 9);
            Assert.Equal(36.0, t.Get(room.Id, "V")!.Value!.Value, 9);
            // Document plan scale 1:50: 2 m x 1.5 m = 3 m².
            var at50 = doc with { Settings = doc.Settings with { PlanScale = 50 } };
            var t50 = Solve(r, at50);
            Assert.Equal(3.0, t50.Get(room.Id, "A")!.Value!.Value, 9);
            Assert.Equal(9.0, t50.Get(room.Id, "V")!.Value!.Value, 9);
            // The object's own scale wins over the document plan scale.
            var own = room.WithParam("scale", 200.0);
            var (_, docOwn) = Fx.Doc(own);
            Assert.Equal(48.0, Solve(r, docOwn).Get(own.Id, "A")!.Value!.Value, 9);
        }

        [Fact]
        public void EnteredVolumeGivesTheHeight()
        {
            var r = Fx.Reg();
            var room = ValueEdits.Set(r, Room(r), "V", "33 m³", Lang.En);
            Assert.Equal("33 m³", room.Str("volume", ""));
            var (_, doc) = Fx.Doc(room);
            var t = Solve(r, doc);
            Assert.Equal(2.75, t.Get(room.Id, "h")!.Value!.Value, 9);
            Assert.Equal(Provenance.Derived, t.Get(room.Id, "h")!.Provenance);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void AnEnteredAreaThatContradictsTheOutlineIsAConflict()
        {
            var r = Fx.Reg();
            var room = ValueEdits.Set(r, Room(r), "A", "20 m²", Lang.En);
            var (_, doc) = Fx.Doc(room);
            Assert.NotEmpty(Solve(r, doc).Conflicts);
        }

        [Fact]
        public void ValuesLabelShowsTheRoomValues()
        {
            var r = Fx.Reg();
            var room = Room(r).WithParam(ValueLabels.ShowParam, true);
            var (_, doc) = Fx.Doc(room);
            var texts = Prim.Flatten(r.RenderLocal(room, new RenderContext(doc))).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains(texts, s => s.Contains("12") && s.Contains("m²") && s.Contains("30") && s.Contains("m³"));
        }

        [Fact]
        public void RoomValuesSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var room = ValueEdits.Set(r, Room(r).WithParam("height", 2.8), "V", "40 m³", Lang.En);
            var (_, doc) = Fx.Doc(room);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var o = back.Get(room.Id)!;
            Assert.Equal(2.8, o.Num("height", 0), 9);
            Assert.Equal("40 m³", o.Str("volume", ""));
        }
    }
}
