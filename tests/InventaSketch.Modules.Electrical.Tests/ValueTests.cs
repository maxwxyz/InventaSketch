using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>Calc 1 demonstrator: Ohm's law and power on the resistor.</summary>
    public class ValueTests
    {
        private static ValueTable Solve(params (string Key, string Text)[] ps)
        {
            var reg = Fx.Reg();
            var o = new SketchObject { Id = "r", Type = "elec.resistor" };
            foreach (var (k, t) in ps) o = o.WithParam(k, t);
            var doc = SketchDocument.Create();
            doc = doc with { Objects = new Dictionary<string, SketchObject> { ["r"] = o }, Pages = new[] { doc.Pages[0] with { Order = new[] { "r" } } } };
            return ValueSolver.Solve(doc, reg, null);
        }

        [Fact]
        public void ResistorDeclaresRUIPBoundToItsParameters()
        {
            var t = Fx.Reg().Type("elec.resistor")!;
            var vars = ValueModel.Variables(t);
            Assert.Equal(new[] { "R", "U", "I", "P" }, vars.Select(v => v.Key).ToArray());
            Assert.Equal(new[] { "value", "voltage", "current", "power" }, vars.Select(v => v.ParamKey).ToArray());
            Assert.All(vars, v => Assert.Equal(VarRelevance.Card, v.Relevance));
            Assert.All(vars.Skip(1), v => Assert.Contains(t.Parameters, p => p.Key == v.ParamKey && p.Kind == ParamKind.Text && p.Unit is not null));
            Assert.Equal("Widerstand", vars[0].Name.De);
            Assert.All(ValueModel.Relations(t), r => Assert.Equal("S7", r.Source));
            // The rating is a plain parameter for the power check, not a variable (Calc 3).
            Assert.Contains(t.Parameters, p => p.Key == "prated" && p.Unit == "W");
            // Symbols without relations have no values.
            Assert.False(ValueModel.HasValues(Fx.Reg().Type("elec.opamp")));
        }

        [Fact]
        public void ValueTextIsTheResistance()
        {
            var t = Solve(("value", "4k7"), ("voltage", "5 V"));
            Assert.Equal(4700, t.Get("r", "R")!.Value!.Value, 9);
            Assert.Equal(5 / 4700.0, t.Get("r", "I")!.Value!.Value, 12);
            Assert.Equal(25 / 4700.0, t.Get("r", "P")!.Value!.Value, 12);
            Assert.Equal(Provenance.Entered, t.Get("r", "R")!.Provenance);
            Assert.Equal("4k7", t.Get("r", "R")!.Text);
        }

        [Theory]
        [InlineData("value", "10 kΩ", "current", "1 mA")]
        [InlineData("voltage", "10 V", "power", "10 mW")]
        [InlineData("current", "1 mA", "power", "10 mW")]
        [InlineData("value", "10k", "power", "10 mW")]
        public void AnyTwoGiveTheOthers(string k1, string v1, string k2, string v2)
        {
            var t = Solve((k1, v1), (k2, v2));
            Assert.Equal(10000, t.Get("r", "R")!.Value!.Value, 6);
            Assert.Equal(10, t.Get("r", "U")!.Value!.Value, 9);
            Assert.Equal(0.001, t.Get("r", "I")!.Value!.Value, 12);
            Assert.Equal(0.01, t.Get("r", "P")!.Value!.Value, 12);
            Assert.Equal(ValueStatus.Determined, t.Status("r"));
        }

        [Fact]
        public void ThreeThatDisagreeGiveAConflict()
        {
            var t = Solve(("value", "10k"), ("voltage", "10 V"), ("current", "2 mA"));
            Assert.Equal(ValueStatus.Conflict, t.Status("r"));
            Assert.NotEmpty(t.Conflicts);
            var ok = Solve(("value", "10k"), ("voltage", "10 V"), ("current", "1 mA"));
            Assert.Equal(ValueStatus.Checked, ok.Status("r"));
        }

        [Fact]
        public void ComponentLabelTextThatIsNoResistanceIsIgnored()
        {
            var t = Solve(("value", "1/4 W"), ("voltage", "5 V"));
            Assert.Null(t.Get("r", "R")!.Value);
            Assert.NotNull(t.Get("r", "R")!.Error);
            Assert.Equal(ValueStatus.Under, t.Status("r"));
        }
    }
}
