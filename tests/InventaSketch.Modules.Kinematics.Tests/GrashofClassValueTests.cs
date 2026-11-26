using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>
    /// The Grashof class of the four-bar as a text-valued element value (engineering-calculations.md, "Text-valued
    /// variables"): computed from the link lengths, shown as its label in the panel, on the values label and in the
    /// calculation sheet, never entered.
    /// </summary>
    public class GrashofClassValueTests
    {
        private static ValueTable Solve(Registry r, SketchObject o)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Grashof");
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });
        }

        private static SketchObject FourBar(Registry r, double d, double a, double b, double c)
            => (r.Create(r.Item("kin.fourbar")!, new Vec2(100, 80)) with { Id = "m" })
                .WithParam("ground", d).WithParam("crank", a).WithParam("coupler", b).WithParam("rocker", c);

        [Theory]
        [InlineData(40, 12, 36, 30, "Crank-rocker", "Kurbelschwinge")]
        [InlineData(10, 30, 40, 35, "Double-crank", "Doppelkurbel")]
        [InlineData(40, 30, 35, 10, "Rocker-crank", "Schwingkurbel")]
        [InlineData(40, 20, 40, 20, "Change point", "Durchschlaggetriebe")]
        [InlineData(40, 30, 12, 20, "Double-rocker (non-Grashof)", "Doppelschwinge (Nicht-Grashof)")]
        public void TheClassIsAValueShownAsText(double d, double a, double b, double c, string en, string de)
        {
            var r = Fx.Reg();
            var t = Solve(r, FourBar(r, d, a, b, c));
            var cls = t.Get("m", "cls")!;
            Assert.Equal(Provenance.Derived, cls.Provenance);
            Assert.Equal("kin.fourbar.class", cls.Via!.Relation.Id);
            Assert.Equal(en, ValueFormat.Format(cls.Value!.Value, t.UnitOf(cls), Lang.En));
            Assert.Equal(de, ValueFormat.Format(cls.Value!.Value, t.UnitOf(cls), Lang.De));
            Assert.Equal((double)(int)Linkages.Classify(d, a, b, c), cls.Value);
        }

        [Fact]
        public void TheClassIsTextValuedAndAppearsOnTheValuesLabelWhenChosen()
        {
            var r = Fx.Reg();
            var def = ValueModel.Variable(r.Type("kin.mech.fourbar"), "cls")!;
            Assert.True(def.IsText);
            Assert.Equal(VarRelevance.Panel, def.Relevance);
            Assert.DoesNotContain(r.Type("kin.mech.fourbar")!.Parameters, p => p.Key == def.ParamKey);
            var o = FourBar(r, 40, 12, 36, 30).WithParam(ValueLabels.ShowParam, true).WithParam(ValueLabels.KeysParam, "cls");
            var t = Solve(r, o);
            Assert.Equal("Class = Crank-rocker", ValueLabels.Text(o, t, Lang.En));
            Assert.Equal("Klasse = Kurbelschwinge", ValueLabels.Text(o, t, Lang.De));
        }

        [Fact]
        public void TheClassIsNeverAMissingValueAndNeverHasARange()
        {
            var r = Fx.Reg();
            var o = FourBar(r, 40, 12, 36, 30);
            var t = Solve(r, o);
            Assert.DoesNotContain(t.Missing("m").SelectMany(m => m), k => k == "cls");
            Assert.Null(t.Get("m", "cls")!.Interval);
            Assert.Empty(t.ConflictsOf("m"));
        }
    }
}
