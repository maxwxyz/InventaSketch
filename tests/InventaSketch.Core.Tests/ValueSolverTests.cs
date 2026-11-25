using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Calc 1: relations, propagation in any direction, provenance, conflicts, defaults, links, persistence.</summary>
    public class ValueSolverTests
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Ohm's law with power, R as text (letter code), U stored as a number in kV (stored-unit binding).</summary>
        internal static readonly IReadOnlyList<Relation> Ohm = new[]
        {
            new Relation("t.ohm", T("Ohm's law", "Ohmsches Gesetz"), "{U} = {R}·{I}", "U", "R", "I") { Source = "S7" }
                .Solve("U", "{R}·{I}", v => v("R") * v("I")).Solve("R", "{U}/{I}", v => v("U") / v("I")).Solve("I", "{U}/{R}", v => v("U") / v("R")),
            new Relation("t.power", T("Power", "Leistung"), "{P} = {U}·{I}", "P", "U", "I")
                .Solve("P", "{U}·{I}", v => v("U") * v("I")).Solve("U", "{P}/{I}", v => v("P") / v("I")).Solve("I", "{P}/{U}", v => v("P") / v("U")),
            new Relation("t.power.ri", T("Power", "Leistung"), "{P} = {I}²·{R}", "P", "I", "R")
                .Solve("P", "{I}²·{R}", v => v("I") * v("I") * v("R")).Solve("I", "√({P}/{R})", v => Math.Sqrt(v("P") / v("R"))).Solve("R", "{P}/{I}²", v => v("P") / (v("I") * v("I"))),
            new Relation("t.power.ur", T("Power", "Leistung"), "{P} = {U}²/{R}", "P", "U", "R")
                .Solve("P", "{U}²/{R}", v => v("U") * v("U") / v("R")).Solve("U", "√({P}·{R})", v => Math.Sqrt(v("P") * v("R"))).Solve("R", "{U}²/{P}", v => v("U") * v("U") / v("P")),
        };

        internal sealed class ResType : ObjectType, IValueType, IValueChecks
        {
            public override string Id => "t.res";
            public override I18nText Name => new("Resistor");
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("value", T("Value", "Wert"), ParamKind.Text),
                new ParamDef("voltage", T("Voltage", "Spannung"), ParamKind.Number) { Unit = "kV" },
                new ParamDef("current", T("Current", "Strom"), ParamKind.Text),
                new ParamDef("power", T("Power", "Leistung"), ParamKind.Text),
            };
            public IReadOnlyList<VarDef> Variables { get; } = new[]
            {
                new VarDef("R", T("Resistance", "Widerstand"), T("R", "R"), Quantities.Resistance) { Param = "value" },
                new VarDef("U", T("Voltage", "Spannung"), T("U", "U"), Quantities.Voltage) { Param = "voltage", StoredUnit = "kV" },
                new VarDef("I", T("Current", "Strom"), T("I", "I"), Quantities.Current) { Param = "current" },
                new VarDef("P", T("Power", "Leistung"), T("P", "P"), Quantities.Power) { Param = "power" },
            };
            public IReadOnlyList<Relation> Relations => Ohm;
            public IEnumerable<ValueCheck> ValueChecks => new[]
            {
                new ValueCheck("t.r.power", CheckSeverity.Warning, T("Power above 1 W", "Leistung über 1 W"), "P > 1 W", c => c["P"] > 1) { Source = "S7" },
            };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        /// <summary>A lamp with a default power (gives way) and a series link of its current to the element named in "next".</summary>
        internal sealed class LampType : ObjectType, IValueType, IValueLinks
        {
            public override string Id => "t.lamp";
            public override I18nText Name => new("Lamp");
            public IReadOnlyList<VarDef> Variables { get; } = new[]
            {
                new VarDef("U", T("Voltage", "Spannung"), T("U", "U"), Quantities.Voltage),
                new VarDef("I", T("Current", "Strom"), T("I", "I"), Quantities.Current),
                new VarDef("P", T("Power", "Leistung"), T("P", "P"), Quantities.Power) { Default = 60 },
            };
            public IReadOnlyList<Relation> Relations => new[] { Ohm[1] };
            public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
            {
                var next = o.Str("next", "");
                if (next.Length == 0) yield break;
                yield return new BoundRelation(Relation.Equality("t.series", T("Series current", "Reihenstrom")),
                    new Dictionary<string, VarRef> { ["a"] = new(o.Id, "I"), ["b"] = new(next, "I") });
            }
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        /// <summary>A relation without an explicit inverse for x: x³ + x = y (numeric root search).</summary>
        internal sealed class CubicType : ObjectType, IValueType
        {
            public override string Id => "t.cubic";
            public override I18nText Name => new("Cubic");
            public IReadOnlyList<VarDef> Variables { get; } = new[]
            {
                new VarDef("x", T("x", "x"), T("x", "x"), Quantities.Ratio) { Min = 0, Max = 100 },
                new VarDef("y", T("y", "y"), T("y", "y"), Quantities.Ratio),
            };
            public IReadOnlyList<Relation> Relations { get; } = new[]
            {
                new Relation("t.cubic", T("Cubic", "Kubisch"), "{y} = {x}³ + {x}", "x", "y")
                    .Solve("y", "{x}³ + {x}", v => Math.Pow(v("x"), 3) + v("x"))
                    .WithResidual(v => Math.Pow(v("x"), 3) + v("x") - v("y")),
            };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        private sealed class ValueModule : IModule
        {
            public string Id => "tv";
            public I18nText Name => new("Values");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[] { new ResType(), new LampType(), new CubicType() };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        private static readonly Registry Reg = new Registry().Register(new ValueModule());

        private static (SketchDocument Doc, SketchObject O) One(string type, params (string Key, ParamValue Value)[] ps)
        {
            var o = new SketchObject { Id = "o1", Type = type };
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            var doc = SketchDocument.Create();
            doc = doc with { Objects = new Dictionary<string, SketchObject> { [o.Id] = o }, Pages = new[] { doc.Pages[0] with { Order = new[] { o.Id } } } };
            return (doc, o);
        }

        private static ValueTable Solve(SketchDocument doc, ValueSolveOptions? opt = null) => ValueSolver.Solve(doc, Reg, null, opt);

        private static void Near(double e, double? a) => Assert.True(a is double x && Math.Abs(x - e) <= 1e-9 * Math.Abs(e), $"expected {e}, got {a}");

        [Theory]
        [InlineData("voltage", 0.23, "current", "2 A")]
        [InlineData("value", "115", "current", "2")]
        [InlineData("value", "115 Ω", "power", "460 W")]
        [InlineData("voltage", 0.23, "power", "0,46 kW")]
        [InlineData("current", "2 A", "power", "460 W")]
        [InlineData("value", "115R", "voltage", 0.23)]
        public void AnyTwoDetermineTheOthers(string k1, object v1, string k2, object v2)
        {
            static ParamValue Pv(object v) => v is string s ? s : (double)v;
            var (doc, _) = One("t.res", (k1, Pv(v1)), (k2, Pv(v2)));
            var t = Solve(doc);
            Near(115, t.Get("o1", "R")!.Value);
            Near(230, t.Get("o1", "U")!.Value);
            Near(2, t.Get("o1", "I")!.Value);
            Near(460, t.Get("o1", "P")!.Value);
            Assert.Equal(ValueStatus.Determined, t.Status("o1"));
            Assert.Equal(2, t.Of("o1").Count(v => v.Provenance == Provenance.Entered));
            Assert.Equal(2, t.Of("o1").Count(v => v.Provenance == Provenance.Derived));
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void ThreeConsistentEntriesAreChecked()
        {
            var (doc, _) = One("t.res", ("value", "115"), ("voltage", 0.23), ("current", "2 A"));
            var t = Solve(doc);
            Assert.Equal(ValueStatus.Checked, t.Status("o1"));
            Assert.Empty(t.Conflicts);
            Near(460, t.Get("o1", "P")!.Value);
        }

        [Fact]
        public void InconsistentEntriesGiveAConflictAndChangeNothing()
        {
            var (doc, o) = One("t.res", ("value", "100"), ("voltage", 0.23), ("current", "2 A"));
            var t = Solve(doc);
            Assert.Equal(ValueStatus.Conflict, t.Status("o1"));
            var c = t.Conflicts.First();
            Assert.Equal("t.ohm", c.Relation.Relation.Id);
            Assert.Equal(new VarRef("o1", "U"), c.Var);
            Near(230, c.Value);
            Near(200, c.Implied);
            Assert.Contains(new VarRef("o1", "R"), c.Involved);
            Assert.Contains(new VarRef("o1", "I"), c.Involved);
            // Entered values are never overwritten: the table keeps them, the document is untouched.
            Near(230, t.Get("o1", "U")!.Value);
            Near(100, t.Get("o1", "R")!.Value);
            Assert.Equal(Provenance.Entered, t.Get("o1", "U")!.Provenance);
            Assert.Equal(o.Params.Count, doc.Get("o1")!.Params.Count);
        }

        [Fact]
        public void MissingNamesTheSmallestSets()
        {
            var (doc, _) = One("t.res", ("voltage", 0.23));
            var t = Solve(doc);
            Assert.Equal(ValueStatus.Under, t.Status("o1"));
            var missing = t.Missing("o1");
            Assert.Equal(3, missing.Count);
            Assert.All(missing, m => Assert.Single(m));
            Assert.Equal(new[] { "R", "I", "P" }, missing.Select(m => m[0]).ToArray());
            var (empty, _) = One("t.res");
            var te = Solve(empty);
            Assert.Equal(ValueStatus.Empty, te.Status("o1"));
            Assert.All(te.Missing("o1"), m => Assert.Equal(2, m.Count));
        }

        [Fact]
        public void TextAndToleranceAreKept()
        {
            var (doc, _) = One("t.res", ("value", "4k7 ±5 %"), ("voltage", 0.005));
            var t = Solve(doc);
            var r = t.Get("o1", "R")!;
            Assert.Equal("4k7 ±5 %", r.Text);
            Assert.Equal(new Tolerance(0.05, 0.05, true), r.Tolerance);
            Near(5 / 4700.0, t.Get("o1", "I")!.Value);
        }

        [Fact]
        public void InvalidTextCountsAsNotEntered()
        {
            var (doc, _) = One("t.res", ("value", "BC547"), ("voltage", 0.23));
            var t = Solve(doc);
            var r = t.Get("o1", "R")!;
            Assert.Null(r.Value);
            Assert.NotNull(r.Error);
            Assert.Equal(Provenance.Unknown, r.Provenance);
        }

        [Fact]
        public void DefaultsGiveWay()
        {
            // Lamp with only U: P takes its default 60 W, I follows.
            var (doc, _) = One("t.lamp", ("U", "230 V"));
            var t = Solve(doc);
            Assert.Equal(Provenance.Default, t.Get("o1", "P")!.Provenance);
            Near(60 / 230.0, t.Get("o1", "I")!.Value);
            // U and I entered: P is derived, the default is not used and no conflict appears.
            var (doc2, _) = One("t.lamp", ("U", "230 V"), ("I", "1 A"));
            var t2 = Solve(doc2);
            Near(230, t2.Get("o1", "P")!.Value);
            Assert.Equal(Provenance.Derived, t2.Get("o1", "P")!.Provenance);
            Assert.Empty(t2.Conflicts);
            // Without defaults nothing is guessed.
            var t3 = Solve(doc, new ValueSolveOptions { UseDefaults = false });
            Assert.Null(t3.Get("o1", "P")!.Value);
        }

        [Fact]
        public void LinksPropagateAndPullConnectedObjectsIntoScope()
        {
            var lamp = new SketchObject { Id = "L", Type = "t.lamp" }.WithParam("next", "R").WithParam("U", "12 V").WithParam("P", "6 W");
            var res = new SketchObject { Id = "R", Type = "t.res" }.WithParam("value", "10");
            var doc = SketchDocument.Create();
            doc = doc with { Objects = new Dictionary<string, SketchObject> { ["L"] = lamp, ["R"] = res }, Pages = new[] { doc.Pages[0] with { Order = new[] { "L", "R" } } } };
            var t = ValueSolver.Solve(doc, Reg, new[] { "L" });
            Assert.Equal(new[] { "L", "R" }, t.ObjectIds);
            var i = t.Get("R", "I")!;
            Near(0.5, i.Value);
            Assert.Equal(Provenance.Linked, i.Provenance);
            Near(5, t.Get("R", "U")!.Value);
            Near(2.5, t.Get("R", "P")!.Value);
            var steps = t.Explain("R", "U", Lang.En);
            Assert.Equal("U = R·I = 10.0 Ω·500 mA = 5 V", steps.Last().Text);
        }

        [Fact]
        public void ExplainGivesStepsInputsFirst()
        {
            var (doc, _) = One("t.res", ("voltage", 0.23), ("current", "2 A"));
            var t = Solve(doc);
            var steps = t.Explain("o1", "R", Lang.En);
            Assert.Single(steps);
            Assert.Equal("R = U/I = 230 V/2.00 A = 115 Ω", steps[0].Text);
            Assert.Equal("t.ohm", steps[0].RelationId);
            Assert.Equal("S7", steps[0].Source);
            Assert.Equal("P = U·I = 230 V·2.00 A = 460 W", t.Explain("o1", "P", Lang.En).Single().Text);
            Assert.Equal("P = U·I = 230 V·2,00 A = 460 W", t.Explain("o1", "P", Lang.De).Single().Text);
            Assert.Equal("I = 2 A", t.Explain("o1", "I", Lang.En).Single().Text);
            // A chain: U, R entered → I derived → P derived from U·I or I²R: inputs come first.
            var (doc2, _) = One("t.res", ("value", "115"), ("power", "460 W"));
            var chain = Solve(doc2).Explain("o1", "U", Lang.En);
            Assert.True(chain.Count >= 1);
            Assert.Equal(new VarRef("o1", "U"), chain.Last().Var);
        }

        [Fact]
        public void NumericRootSearch()
        {
            var (doc, _) = One("t.cubic", ("y", "10"));
            var t = Solve(doc);
            Near(2, t.Get("o1", "x")!.Value);
            Assert.Contains("numerically", t.Explain("o1", "x", Lang.En).Single().Text);
            Assert.Equal(2.0, RootFinder.Solve(x => x * x - 4), 9);
        }

        [Fact]
        public void OverridesPreviewWithoutChangingTheDocument()
        {
            var (doc, _) = One("t.res", ("value", "115"), ("voltage", 0.23));
            var t = Solve(doc, new ValueSolveOptions { Overrides = new Dictionary<VarRef, double> { [new VarRef("o1", "R")] = 230 } });
            Near(1, t.Get("o1", "I")!.Value);
            Assert.Equal("115", doc.Get("o1")!.Str("value", ""));
        }

        [Fact]
        public void StorageFollowsTheParameterKind()
        {
            var type = (ResType)Reg.Type("t.res")!;
            var (_, o) = One("t.res");
            var u = type.Variables.Single(v => v.Key == "U");
            var r = type.Variables.Single(v => v.Key == "R");
            var pu = ValueModel.ToParam(type, o, u, ValueParser.Parse("230 V", u.Kind));
            Assert.Equal(0.23, pu.Number!.Value, 12); // number parameter in kV
            var pr = ValueModel.ToParam(type, o, r, ValueParser.Parse("4k7", r.Kind));
            Assert.Equal("4k7", pr.Text); // text parameter: as typed
            Assert.Equal("120 Ω", ValueModel.ToParam(type, o, r, 120, Lang.En).Text);
        }

        [Fact]
        public void JsonRoundTripKeepsEnteredValuesAndText()
        {
            var (doc, _) = One("t.res", ("value", "4k7"), ("voltage", 0.005), ("current", "1,064 mA"));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal("4k7", back.Get("o1")!.Str("value", ""));
            Assert.Equal("1,064 mA", back.Get("o1")!.Str("current", ""));
            var a = Solve(doc);
            var b = Solve(back);
            foreach (var v in a.Of("o1")) Assert.Equal(v.Value, b.Get("o1", v.Def.Key)!.Value);
            Assert.Equal(a.Status("o1"), b.Status("o1"));
        }

        [Fact]
        public void DeterministicTrace()
        {
            var (doc, _) = One("t.res", ("value", "115"), ("power", "460 W"));
            var a = Solve(doc);
            var b = Solve(doc);
            Assert.Equal(a.Of("o1").Select(v => (v.Value, v.Provenance, v.Via?.Relation.Id)), b.Of("o1").Select(v => (v.Value, v.Provenance, v.Via?.Relation.Id)));
            Assert.Equal(a.Explain("o1", "U", Lang.En).Select(s => s.Text), b.Explain("o1", "U", Lang.En).Select(s => s.Text));
        }

        [Fact]
        public void ChecksRunOnTheSolvedTable()
        {
            var (doc, _) = One("t.res", ("voltage", 0.23), ("current", "2 A"));
            var t = Solve(doc);
            var findings = ValueSolver.Checks(t, Reg, new RenderContext(doc, Lang.En));
            var f = Assert.Single(findings);
            Assert.Equal("t.r.power", f.CheckId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
        }

        [Fact]
        public void ObjectsWithoutValuesAreIgnored()
        {
            var (doc, _) = One("t.res", ("value", "1"));
            doc = doc with { Objects = new Dictionary<string, SketchObject>(doc.Objects) { ["x"] = new SketchObject { Id = "x", Type = "unknown" } } };
            var t = ValueSolver.Solve(doc, Reg, new[] { "x", "o1" });
            Assert.Equal(new[] { "o1" }, t.ObjectIds);
            Assert.Empty(t.Of("x"));
        }
    }
}
