using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    /// <summary>Room heating load estimate Φ_HL = q·A (optional; building services radiators in the room take it over).</summary>
    public class RoomHeatLoadTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Room(Registry r) =>
            r.Create(r.Item("arch.room")!, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30), new Vec2(0, 30) } };

        [Fact]
        public void SpecificHeatingLoadGivesTheRoomHeatingLoad()
        {
            // 12 m² · 80 W/m² = 960 W.
            var r = Fx.Reg();
            var room = Room(r).WithParam("heatLoadDensity", "80 W/m²");
            var (_, doc) = Fx.Doc(room);
            var t = Solve(r, doc);
            var load = t.Get(room.Id, "PhiHL")!;
            Assert.Equal(960.0, load.Value!.Value, 9);
            Assert.Equal(Provenance.Derived, load.Provenance);
            Assert.Equal("arch.room.heatload", load.Via!.Relation.Id);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void EnteredHeatingLoadGivesTheSpecificLoadAndNothingIsRequired()
        {
            var r = Fx.Reg();
            var room = Room(r).WithParam("heatLoad", "1,2 kW");
            var (_, doc) = Fx.Doc(room);
            var t = Solve(r, doc);
            Assert.Equal(100.0, t.Get(room.Id, "q")!.Value!.Value, 9);
            // Without either, the room's other values are unchanged and the load stays open.
            var (_, plain) = Fx.Doc(Room(r));
            var t2 = Solve(r, plain);
            Assert.Null(t2.Get(t2.ObjectIds[0], "PhiHL")!.Value);
            Assert.Equal(30.0, t2.Get(t2.ObjectIds[0], "V")!.Value!.Value, 9);
        }

        [Fact]
        public void HeatingLoadParametersAreTextWithUnits()
        {
            var t = Fx.Reg().Type("arch.room")!;
            Assert.Contains(t.Parameters, p => p.Key == "heatLoadDensity" && p.Kind == ParamKind.Text && p.Unit == "W/m²");
            Assert.Contains(t.Parameters, p => p.Key == "heatLoad" && p.Kind == ParamKind.Text && p.Unit == "W");
            Assert.Equal(Quantities.HeatFlow, ValueModel.Variable(t, "PhiHL")!.Kind);
            Assert.Equal(Quantities.Irradiance, ValueModel.Variable(t, "q")!.Kind);
        }
    }
}
