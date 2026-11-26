using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    /// <summary>Stair step rule, ramp rise, wall transmission heat loss and the schedules of a floor plan page (round 7).</summary>
    public class ValueTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double V(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)!.Value!.Value;

        private static bool Fires(Registry r, SketchDocument doc, string check)
            => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc)).Any(f => f.CheckId == check);

        // ------------------------------------------------------------------ stairs

        [Fact]
        public void StraightStairHasRiserTreadAndStepLength()
        {
            // 36 mm at 1:100 = 3.60 m run, 14 risers: g = 25.7 cm; H = 2.75 m (default): h = 19.6 cm; 2h + g = 65 cm.
            var r = Fx.Reg();
            var s = r.Create(r.Item("arch.stair.straight")!, new Vec2(50, 50));
            var (_, doc) = Fx.Doc(s);
            var t = Solve(r, doc);
            Assert.Equal(3.6 / 14, V(t, s, "g"), 9);
            Assert.Equal(2.75 / 14, V(t, s, "h"), 9);
            Assert.Equal(0.65, V(t, s, "step"), 9);
            Assert.Equal(System.Math.Atan2(2.75, 3.6), V(t, s, "alpha"), 9);
            Assert.Equal(Provenance.Default, t.Get(s.Id, "H")!.Provenance);
            Assert.Empty(t.Conflicts);
            Assert.False(Fires(r, doc, "arch.stair.steprule")); // no height given yet
        }

        [Fact]
        public void TheStepRuleHintFiresForAGivenStoreyHeightAndTheFitActionCuresIt()
        {
            // H = 3.00 m over 3.60 m with 14 risers: 2·21.4 + 25.7 = 68.6 cm (too steep).
            var r = Fx.Reg();
            var s = r.Create(r.Item("arch.stair.straight")!, new Vec2(50, 50)).WithParam("storeyHeight", 3.0);
            var (_, doc) = Fx.Doc(s);
            Assert.True(Fires(r, doc, "arch.stair.steprule"));
            // Best n: (2·3.00 + 3.60)/0.63 ≈ 15.2 → 15 risers, 2h + g = 64 cm.
            var fit = r.TypeOf(s).Actions(s, new RenderContext(doc)).Single(a => a.Id == "arch.stair.fit");
            Assert.True(fit.IsEnabled);
            Assert.Contains("15", fit.Label.En);
            var fixedDoc = fit.Run(doc, s, new RenderContext(doc), null);
            var s2 = fixedDoc.Get(s.Id)!;
            Assert.Equal(15, s2.Int("steps", 0));
            Assert.Equal(0.64, V(Solve(r, fixedDoc), s2, "step"), 9);
            Assert.False(Fires(r, fixedDoc, "arch.stair.steprule"));
            Assert.Equal(14, r.TypeOf(s2).Render(s2, new RenderContext(fixedDoc)).Count(p => p.Tag == ArchTags.Tread));
        }

        [Fact]
        public void AnEnteredRiserGivesTheStoreyHeight()
        {
            var r = Fx.Reg();
            var s = r.Create(r.Item("arch.stair.l")!, new Vec2(50, 50)).WithParam("riser", "17,5 cm");
            var (_, doc) = Fx.Doc(s);
            var t = Solve(r, doc);
            Assert.Equal(16 * 0.175, V(t, s, "H"), 9);
            Assert.Equal(Provenance.Derived, t.Get(s.Id, "H")!.Provenance);
            // L stair 30 x 30: first flight 20 mm over 8 treads = 25 cm at 1:100.
            Assert.Equal(0.25, V(t, s, "g"), 9);
        }

        [Fact]
        public void TheTreadFollowsThePlanScale()
        {
            var r = Fx.Reg();
            var s = r.Create(r.Item("arch.stair.straight")!, new Vec2(50, 50));
            var (_, doc) = Fx.Doc(s);
            var at50 = doc with { Settings = doc.Settings with { PlanScale = 50 } };
            Assert.Equal(1.8 / 14, V(Solve(r, at50), s, "g"), 9);
            Assert.True(r.TypeOf(s).UsesPlanScale);
        }

        [Fact]
        public void StairVariablesAreBilingualWithGermanStepRuleSymbols()
        {
            var t = Fx.Reg().Type("arch.stair.u")!;
            var step = ValueModel.Variable(t, "step")!;
            Assert.Equal("2h + g", step.Symbol.En);
            Assert.Equal("2s + a", step.Symbol.De);
            Assert.True(ValueModel.Variable(t, "g")!.IsReadOnly);
            Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4);
        }

        // ------------------------------------------------------------------ ramp

        [Fact]
        public void RampRiseFromSlopeAndDrawnLength()
        {
            // 60 mm at 1:100 = 6 m at 6 %: Δh = 0.36 m.
            var r = Fx.Reg();
            var ramp = r.Create(r.Item("arch.ramp")!, new Vec2(50, 50));
            var (_, doc) = Fx.Doc(ramp);
            var t = Solve(r, doc);
            Assert.Equal(6.0, V(t, ramp, "l"), 9);
            Assert.Equal(0.36, V(t, ramp, "dh"), 9);
            Assert.False(Fires(r, doc, "arch.ramp.slope"));
            var (_, steep) = Fx.Doc(ramp.WithParam("slope", 8.0));
            Assert.Equal(0.48, V(Solve(r, steep), ramp, "dh"), 9);
            Assert.True(Fires(r, steep, "arch.ramp.slope"));
        }

        // ------------------------------------------------------------------ walls

        [Fact]
        public void WallAreaLessDoorsAndWindowsGivesTheTransmissionHeatLoss()
        {
            // 60 mm wall at 1:100 = 6 m, 2.75 m high: 16.5 m². Door 0.90 x 2.10 m = 1.89 m² in it.
            var r = Fx.Reg();
            var wall = Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0)).WithParam("uValue", "0,28 W/(m²K)").WithParam("deltaT", "32 K");
            var door = r.Create(r.Item("arch.door.single")!, new Vec2(20, 0));
            var (_, doc) = Fx.Doc(wall, door);
            var t = Solve(r, doc);
            Assert.Equal(6.0, V(t, wall, "l"), 9);
            Assert.Equal(16.5, V(t, wall, "A"), 9);
            Assert.Equal(1.89, V(t, wall, "Aopen"), 9);
            Assert.Equal(14.61, V(t, wall, "Anet"), 9);
            Assert.Equal(0.28 * 14.61 * 32, V(t, wall, "PhiT"), 6);
            Assert.Equal("arch.wall.transmission", t.Get(wall.Id, "PhiT")!.Via!.Relation.Id);
            Assert.Empty(t.Conflicts);
            // An entered heat loss gives the U-value back.
            var (_, back) = Fx.Doc(Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0)).WithParam("deltaT", "30 K").WithParam("heatLoss", "495 W"));
            var tb = Solve(r, back);
            Assert.Equal(1.0, tb.Get(tb.ObjectIds[0], "U")!.Value!.Value, 9);
        }

        [Fact]
        public void ADoorInAWallWithValuesLabelNamesTheWallAsDependent()
        {
            var r = Fx.Reg();
            var wall = Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0)).WithParam(ValueLabels.ShowParam, true);
            var door = r.Create(r.Item("arch.door.single")!, new Vec2(20, 0));
            var (_, doc) = Fx.Doc(wall, door);
            Assert.Contains(wall.Id, r.TypeOf(door).Dependents(door, new RenderContext(doc)));
            var (_, plain) = Fx.Doc(Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0)), door);
            Assert.Empty(r.TypeOf(door).Dependents(door, new RenderContext(plain)));
        }

        // ------------------------------------------------------------------ schedules

        private static SketchObject Room(Registry r, string item, params Vec2[] pts) => r.Create(r.Item(item)!, Vec2.Zero) with { Points = pts };

        [Fact]
        public void TheRoomScheduleListsTheRoomsOfThePageWithTheirSum()
        {
            var r = Fx.Reg();
            var living = Room(r, "arch.room.living", new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30), new Vec2(0, 30));   // 12 m²
            var bath = Room(r, "arch.room.bath", new Vec2(40, 0), new Vec2(60, 0), new Vec2(60, 30), new Vec2(40, 30));     // 6 m²
            var list = r.Create(r.Item("arch.schedule")!, new Vec2(150, 50));
            var (_, doc) = Fx.Doc(living, bath, list);
            var texts = Prim.Flatten(r.TypeOf(list).Render(list, new RenderContext(doc))).OfType<TextPrim>().Select(x => x.Text).ToList();
            Assert.Contains("Living room", texts);
            Assert.Contains("12.00 m²", texts);
            Assert.Contains("6.00 m²", texts);
            Assert.Contains("18.00 m²", texts);
            var de = Prim.Flatten(r.TypeOf(list).Render(list, new RenderContext(doc, Lang.De))).OfType<TextPrim>().Select(x => x.Text).ToList();
            Assert.Contains("Raumliste", de);
            Assert.Contains("18,00 m²", de);
            var t = Solve(r, doc);
            Assert.Equal(18.0, V(t, list, "Asum"), 9);
            Assert.Equal(2.0, V(t, list, "count"), 9);
            Assert.Null(t.Get(list.Id, "Agross")!.Value); // no closed exterior wall
            // Rooms redraw the schedule when they change.
            Assert.Contains(list.Id, r.TypeOf(living).Dependents(living, new RenderContext(doc)));
            Assert.True(r.TypeOf(living).HasDependents);
        }

        [Fact]
        public void TheGrossFloorAreaComesFromTheClosedExteriorWalls()
        {
            // 100 x 70 mm centre line, 365 mm thick at 1:100: outer face 103.65 x 73.65 mm = 76.34 m².
            var r = Fx.Reg();
            var walls = r.Create(r.Item("arch.wall.rect")!, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 70), new Vec2(0, 70) } };
            var list = r.Create(r.Item("arch.schedule")!, new Vec2(200, 50));
            var (_, doc) = Fx.Doc(walls, list);
            var t = Solve(r, doc);
            Assert.Equal(103.65 * 73.65 / 100, V(t, list, "Agross"), 6);
            Assert.Contains(list.Id, r.TypeOf(walls).Dependents(walls, new RenderContext(doc)));
            var texts = Prim.Flatten(r.TypeOf(list).Render(list, new RenderContext(doc))).OfType<TextPrim>().Select(x => x.Text).ToList();
            Assert.Contains("Gross floor area", texts);
        }

        [Fact]
        public void TheDoorAndWindowScheduleGroupsEqualOpenings()
        {
            var r = Fx.Reg();
            var d1 = r.Create(r.Item("arch.door.single")!, new Vec2(10, 10));
            var d2 = r.Create(r.Item("arch.door.single")!, new Vec2(40, 10));
            var w1 = r.Create(r.Item("arch.window.double")!, new Vec2(70, 10));
            var list = r.Create(r.Item("arch.schedule.openings")!, new Vec2(150, 50));
            var (_, doc) = Fx.Doc(d1, d2, w1, list);
            var texts = Prim.Flatten(r.TypeOf(list).Render(list, new RenderContext(doc))).OfType<TextPrim>().Select(x => x.Text).ToList();
            Assert.Contains("2×", texts);
            Assert.Contains("0.90 × 2.10 m", texts);
            Assert.Contains("1.60 × 1.25 m", texts);
            Assert.Contains("3×", texts);
            var t = Solve(r, doc);
            Assert.Equal(3.0, V(t, list, "count"), 9);
            Assert.Equal(2 * 0.9 * 2.1 + 1.6 * 1.25, V(t, list, "Aopen"), 9);
            Assert.Contains(list.Id, r.TypeOf(d1).Dependents(d1, new RenderContext(doc)));
        }

        [Fact]
        public void SchedulesStayInsideTheirBoxWithManyRows()
        {
            var r = Fx.Reg();
            var objs = Enumerable.Range(0, 20).Select(i => Room(r, "arch.room.office", new Vec2(i * 10, 0), new Vec2(i * 10 + 10, 0), new Vec2(i * 10 + 10, 10), new Vec2(i * 10, 10))).ToList();
            var list = r.Create(r.Item("arch.schedule")!, new Vec2(300, 50));
            objs.Add(list);
            var (_, doc) = Fx.Doc(objs.ToArray());
            Fx.AssertInsideBox(r.TypeOf(list).Render(list, new RenderContext(doc)), list.Size, "20 rooms");
            var squashed = list with { Size = new Size2(20, 8) };
            Fx.AssertInsideBox(r.TypeOf(list).Render(squashed, new RenderContext(doc)), squashed.Size, "squashed");
        }

        [Fact]
        public void ValueParametersSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var wall = Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0)).WithParam("uValue", "0,28 W/(m²K)").WithParam("deltaT", "32 K");
            var stair = r.Create(r.Item("arch.stair.straight")!, new Vec2(50, 50)).WithParam("storeyHeight", 2.9).WithParam("riser", "18 cm");
            var (_, doc) = Fx.Doc(wall, stair);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal("0,28 W/(m²K)", back.Get(wall.Id)!.Str("uValue", ""));
            Assert.Equal(2.9, back.Get(stair.Id)!.Num("storeyHeight", 0));
            Assert.Equal(V(Solve(r, doc), wall, "PhiT"), V(Solve(r, back), wall, "PhiT"), 9);
        }
    }
}
