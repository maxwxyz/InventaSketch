using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// The energy module's heat side meets building heating (engineering-calculations.md, "Joins across modules"): energy
    /// heat ports give the heating water's V̇ and flow temperature θ, the energy heat pipe is a net line, and both modules'
    /// heat pumps name the same variables. Default catalogue; every expected value is a hand calculation.
    /// </summary>
    public class EnergyHeatJoinTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private const double C0 = 273.15;
        private const double RhoC = 1000 * 4190.0;

        private static ValueTable Solve(SketchDocument doc, params string[] ids)
            => ValueSolver.Solve(doc, Reg, ids.Length == 0 ? null : ids, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(string item, string id) => Reg.Create(Reg.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Heat");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Line(string item, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => Make(item, id) with
            {
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        /// <summary>8 kW (default) at 55/45 °C: V̇ = 8 kW/(1000 kg/m³ · 4190 J/(kg·K) · 10 K) = 1.909e-4 m³/s = 0.687 m³/h.</summary>
        private static SketchObject EnergyHeatPump()
            => Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "55 °C").WithParam("returnTemp", "45 °C");

        private const double Vdot = 8000 / (RhoC * 10);

        [Fact]
        public void EnergyHeatPumpFeedsABuildingRadiatorThroughAHeatingPipe()
        {
            var doc = Doc(EnergyHeatPump(), Make("bs.heat.radiator", "rad"), Line("bs.pipe.flow", "p1", ("hp", "heat"), ("rad", "flow")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            var v = t.Get("rad", "Vdot")!;
            Assert.Equal(Vdot, v.Value!.Value, 12);
            Assert.Equal(Provenance.Linked, v.Provenance);
            var th = t.Get("rad", "thf")!;
            Assert.Equal(55 + C0, th.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, th.Provenance);
            // The pipe carries both values along.
            Assert.Equal(Vdot, t.Get("p1", "Vdot")!.Value!.Value, 12);
            Assert.Equal(55 + C0, t.Get("p1", "theta")!.Value!.Value, 9);
            Assert.Contains(t.Of("rad").Concat(t.Of("p1")), x => x.Via?.Relation.Id == "net.cross.Vdot");
            Assert.Contains(t.Of("rad").Concat(t.Of("p1")), x => x.Via?.Relation.Id == "net.cross.theta");
            // The radiator's return is another port: untouched.
            Assert.NotEqual(Provenance.Linked, t.Get("rad", "thr")!.Provenance);
        }

        [Fact]
        public void TheRadiatorAloneFindsTheEnergyHeatPump()
        {
            var doc = Doc(EnergyHeatPump(), Make("bs.heat.radiator", "rad"), Line("bs.pipe.flow", "p1", ("hp", "heat"), ("rad", "flow")));
            var t = Solve(doc, "rad");
            Assert.Equal(Vdot, t.Get("rad", "Vdot")!.Value!.Value, 12);
            Assert.Equal(55 + C0, t.Get("rad", "thf")!.Value!.Value, 9);
        }

        [Fact]
        public void EnergyHeatPipeDockedOnARadiatorJoinsToo()
        {
            var doc = Doc(EnergyHeatPump(), Make("bs.heat.radiator", "rad"), Line("energy.line.pipe.heat", "p1", ("hp", "heat"), ("rad", "flow")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            // The energy net reaches the radiator: its own port names the variables (V̇ and θ_F).
            Assert.Equal(Vdot, t.Get("rad", "Vdot")!.Value!.Value, 12);
            Assert.Equal(55 + C0, t.Get("rad", "thf")!.Value!.Value, 9);
            Assert.Equal("net.cross.theta", t.Get("rad", "thf")!.Via!.Relation.Id);
        }

        [Fact]
        public void RadiatorValuesFlowBackIntoTheEnergyHeatPump()
        {
            // Entered at the radiator: V̇ = 0.36 m³/h and θ_F = 50 °C; the energy heat pump (8 kW) then has its return temperature.
            var doc = Doc(
                Make("energy.heat.heatpump", "hp"),
                Make("bs.heat.radiator", "rad").WithParam("flowRate", "360 l/h").WithParam("flowTemp", "50 °C"),
                Line("bs.pipe.flow", "p1", ("hp", "heat"), ("rad", "flow")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(0.36 / 3600, t.Get("hp", "Vdot")!.Value!.Value, 12);
            Assert.Equal(50 + C0, t.Get("hp", "thf")!.Value!.Value, 9);
            // θ_R = θ_F − Φ/(ρ·c·V̇) = 50 °C − 8000/(4.19e6 · 1e-4) K = 30.9 °C.
            Assert.Equal(50 + C0 - 8000 / (RhoC * 1e-4), t.Get("hp", "thr")!.Value!.Value, 9);
        }

        [Fact]
        public void BuildingHeatPumpChargesAnEnergyHeatStorage()
        {
            // The two heat pumps stay separate types; their values meet at a junction: the building heat pump's flow
            // temperature becomes the energy storage's top temperature. The storage names no volume flow, so none is joined.
            var doc = Doc(
                Make("bs.heat.heatpump", "bshp").WithParam("flowTemp", "60 °C").WithParam("flowRate", "1 m³/h"),
                Make("energy.heat.storage", "st"),
                Line("bs.pipe.flow", "p1", ("bshp", "flow"), ("st", "charge")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            var th = t.Get("st", "th")!;
            Assert.Equal(60 + C0, th.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, th.Provenance);
            Assert.DoesNotContain(t.Of("st"), x => x.Via?.Relation.Id == "net.cross.Vdot");
        }

        [Fact]
        public void BothHeatPumpsNameTheSameVariables()
        {
            var energy = ValueModel.Variables(Reg.Type("energy.heat.heatpump")).ToDictionary(v => v.Key);
            var building = ValueModel.Variables(Reg.Type("bs.heat.heatpump")).ToDictionary(v => v.Key);
            foreach (var key in new[] { "Phi", "Pel", "cop", "thf", "thr", "ths", "Vdot", "rho", "c" })
            {
                Assert.True(energy.ContainsKey(key), "energy: " + key);
                Assert.True(building.ContainsKey(key), "bs: " + key);
                Assert.Equal(building[key].Kind.Id, energy[key].Kind.Id);
            }
        }

        [Fact]
        public void BuildingPipeOnAHydroTurbineTakesItsFlow()
        {
            // P = 1000 · 9.80665 · Q · 10 m · 0.8 = 1 MW gives Q = 12.75 m³/s at the headwater port.
            var doc = Doc(
                Make("energy.hydro.turbine", "h").WithParam("powerHydro", "1 MW").WithParam("head", "10 m").WithParam("efficiency", "80 %"),
                Make("bs.heat.pump", "pump"),
                Line("bs.pipe.cold", "p1", ("pump", "out"), ("h", "water")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            var q = 1e6 / (1000 * 9.80665 * 10 * 0.8);
            Assert.Equal(q, t.Get("h", "Q")!.Value!.Value, 9);
            Assert.Equal(q, t.Get("p1", "Vdot")!.Value!.Value, 9);
        }
    }
}
