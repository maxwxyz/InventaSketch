using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// The appliance outlet is the canonical appliance connection of the house installation: room, circuit ("fed from") and
    /// phase are optional parameters with the names of the switchgear room devices, and it is an electrical load with power,
    /// current, voltage (default 230 V) and power factor (default 1), single-phase or three-phase. Hand calculations.
    /// </summary>
    public class ApplianceLoadTests
    {
        private static readonly Registry R = Fx.Reg();

        private static SketchObject Outlet() => R.Create(R.Item("bs.elec.appliance")!, Vec2.Zero) with { Id = "A1" };

        private static ValueTable Solve(SketchObject o)
        {
            var doc = SketchDocument.Create();
            return ValueSolver.Solve(Edits.Add(doc, doc.Pages[0].Id, o), R, null, new ValueSolveOptions { BudgetMs = 10000 });
        }

        [Fact]
        public void RoomCircuitPhaseAndLoadValuesAreOptionalParameters()
        {
            var t = R.Type("bs.elec.appliance")!;
            foreach (var key in new[] { "room", "feed", "power", "current", "voltage", "powerFactor" })
                Assert.Equal(ParamKind.Text, t.Parameters.Single(p => p.Key == key).Kind);
            Assert.Equal(new[] { "circuit", "L1", "L2", "L3", "L123" }, t.Parameters.Single(p => p.Key == "phase").Options!.Select(o => o.Value));
            Assert.Equal(new[] { "P", "I", "U", "pf" }, ValueModel.Variables(t).Select(v => v.Key));
            // Quick Sketch: inserted without values it shows no conflict and no check.
            foreach (var item in R.Library.Where(i => i.TypeId == "bs.elec.appliance"))
            {
                var o = R.Create(item, Vec2.Zero);
                Assert.True(o.Str("room", "") == "" && o.Str("feed", "") == "" && o.Str("phase", "circuit") == "circuit", item.Id);
                Assert.Empty(Solve(o).Conflicts);
            }
        }

        [Fact]
        public void RoomAndCircuitAreTypedFromTheBar()
        {
            var o = Outlet();
            var actions = R.Type("bs.elec.appliance")!.Actions(o, Lang.De).ToList();
            var placed = actions.Single(a => a.Id == "bs.appliance.feed").Input!.Apply(actions.Single(a => a.Id == "bs.appliance.room").Input!.Apply(o, " Kitchen "), "-F7");
            Assert.Equal("Kitchen", placed.Str("room", ""));
            Assert.Equal("-F7", placed.Str("feed", ""));
            Assert.Contains(actions, a => a.Choice?.Key == "bs.appliance.phase");
        }

        [Fact]
        public void PowerGivesTheCurrentSingleAndThreePhase()
        {
            // 2300 W at 230 V, cos φ = 1: I = 10 A.
            var single = Solve(Outlet().WithParam("power", "2300 W"));
            Assert.Equal(10, single.Get("A1", "I")!.Value!.Value, 9);
            // A cooker on three phases: 6900 W / (3 · 230 V · 1) = 10 A per phase.
            var three = Solve(Outlet().WithParam("power", "6900 W").WithParam("phase", "L123"));
            Assert.Equal(10, three.Get("A1", "I")!.Value!.Value, 9);
            // With a power factor of 0.8: 1840 W / (230 V · 0.8) = 10 A.
            var pf = Solve(Outlet().WithParam("power", "1840 W").WithParam("powerFactor", "0.8"));
            Assert.Equal(10, pf.Get("A1", "I")!.Value!.Value, 9);
        }
    }
}
