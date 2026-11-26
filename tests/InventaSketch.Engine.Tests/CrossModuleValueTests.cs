using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Joins across modules (engineering-calculations.md, "Joins across modules"): a line of one module docked on an element
    /// of another links the variables of the same physical key and quantity kind (<see cref="NetKeys"/>). Real modules of the
    /// default catalogue; every expected value is a hand calculation.
    /// </summary>
    public class CrossModuleValueTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private const double C0 = 273.15;

        private static ValueTable Solve(SketchDocument doc, params string[] ids)
            => ValueSolver.Solve(doc, Reg, ids.Length == 0 ? null : ids, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(string item, string id) => Reg.Create(Reg.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Cross");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Line(string item, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => Reg.Create(Reg.Item(item)!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        /// <summary>10 modules of 400 Wp at U_mpp = 41.5 V: U_mpp,S = 415 V, P = 4000 W.</summary>
        private static SketchObject PvString()
            => Make("energy.pv.string", "str").WithParam("modules", 10).WithParam("power", 400.0).WithParam("vmp", "41,5 V");

        private static SketchDocument PvLamp()
            => Doc(PvString(), Make("elec.lamp", "lamp"), Line("energy.line.cable", "c1", ("str", "dc"), ("lamp", "1")).WithParam("dc", true));

        [Fact]
        public void PvStringFeedsAnElectricalLampThroughAnEnergyCable()
        {
            var t = Solve(PvLamp());
            Assert.Empty(t.Conflicts);
            // U = U_mpp,S = 10 · 41.5 V = 415 V and P = 10 · 400 W = 4000 W, passed on across the module border.
            var u = t.Get("lamp", "U")!;
            Assert.Equal(415, u.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, u.Provenance);
            Assert.Equal("net.cross.U", u.Via!.Relation.Id);
            Assert.False(string.IsNullOrEmpty(u.Via.Relation.Name.De));
            var p = t.Get("lamp", "P")!;
            Assert.Equal(4000, p.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, p.Provenance);
            // The lamp's own relations do the rest: I = P/U = 9.639 A, R = U/I = 43.06 Ω.
            Assert.Equal(4000 / 415.0, t.Get("lamp", "I")!.Value!.Value, 9);
            Assert.Equal(415 * 415 / 4000.0, t.Get("lamp", "R")!.Value!.Value, 9);
            // Limits (U_oc at θ_min) and the short-circuit current have no physical key: they stay in the energy module.
            Assert.DoesNotContain(t.Of("lamp"), v => v.Via is { } via && via.Map.Values.Any(r => r.Key is "UocMax" or "UmppMin" or "Isc"));
        }

        [Fact]
        public void TheLampAloneFindsTheStringThatFeedsIt()
        {
            // Value labels solve one element: the energy cable docked on the lamp brings the string into the scope.
            var t = Solve(PvLamp(), "lamp");
            Assert.Equal(415, t.Get("lamp", "U")!.Value!.Value, 9);
            Assert.Equal(4000, t.Get("lamp", "P")!.Value!.Value, 9);
        }

        [Fact]
        public void ProcessLinePassesFlowAndTemperatureToARadiator()
        {
            var doc = Doc(
                Make("process.pump.centrifugal", "pump").WithParam("flowRate", "1,2 m³/h"),
                Make("bs.heat.radiator", "rad"),
                Line("process.line", "l1", ("pump", "D"), ("rad", "flow")).WithParam("temperature", "70 °C"));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            // V̇ = 1.2 m³/h = 1/3000 m³/s from the pump; θ_F = 70 °C from the line, read through the radiator's own port (thf).
            var v = t.Get("rad", "Vdot")!;
            Assert.Equal(1.2 / 3600, v.Value!.Value, 12);
            Assert.Equal(Provenance.Linked, v.Provenance);
            var th = t.Get("rad", "thf")!;
            Assert.Equal(70 + C0, th.Value!.Value, 9);
            Assert.Equal("net.cross.theta", th.Via!.Relation.Id);
            // The return temperature is another port: untouched.
            Assert.NotEqual(Provenance.Linked, t.Get("rad", "thr")!.Provenance);
        }

        [Fact]
        public void BuildingPipeOnAProcessPumpTakesThePumpFlow()
        {
            var doc = Doc(
                Make("process.pump.centrifugal", "pump").WithParam("flowRate", "0,6 m³/h"),
                Make("bs.heat.radiator", "rad"),
                Line("bs.pipe", "p1", ("pump", "D"), ("rad", "flow")).WithParam("medium", "flow").WithParam("temperature", "55 °C"));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(0.6 / 3600, t.Get("rad", "Vdot")!.Value!.Value, 12);
            Assert.Equal(0.6 / 3600, t.Get("p1", "Vdot")!.Value!.Value, 12);
            // The pump's own nets name only V̇ at its port: no temperature is pushed onto it.
            Assert.DoesNotContain(t.Of("pump"), x => x.Via is { } via && via.Relation.Id == "net.cross.theta");
        }

        [Fact]
        public void ABranchedSumIsNotJoinedButTheSharedVoltageIs()
        {
            // Inverter and lamp on one busbar: the busbar's power is the sum of its feeders (a branched net), so the lamp's
            // P is not taken from it; the voltage is shared by the whole net.
            var doc = Doc(
                Make("energy.conv.inverter", "inv"),
                Make("energy.grid.busbar", "bus").WithParam("voltage", 0.4),
                Make("elec.lamp", "lamp"),
                Line("energy.line.cable", "c1", ("inv", "ac"), ("bus", "t1")),
                Line("energy.line.cable", "c2", ("bus", "t2"), ("lamp", "1")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(400, t.Get("lamp", "U")!.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, t.Get("lamp", "U")!.Provenance);
            Assert.Null(t.Get("lamp", "P")!.Value);
        }

        [Fact]
        public void WithoutACrossingLineNothingIsJoined()
        {
            var doc = Doc(PvString(), Make("elec.lamp", "lamp"));
            var t = Solve(doc);
            Assert.Null(t.Get("lamp", "U")!.Value);
            Assert.DoesNotContain(t.Of("str"), v => v.Via?.Relation.Id.StartsWith("net.cross.", System.StringComparison.Ordinal) == true);
        }
    }
}
