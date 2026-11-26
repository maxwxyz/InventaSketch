using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Text-valued variables (engineering-calculations.md, "Text-valued variables"): a class computed by a relation, shown as
    /// its label, never entered, never used to derive another value, never checked, never given a default or a range.
    /// </summary>
    public class TextValuedKindTests
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly QuantityKind SignKind = Quantities.Text("t.sign", T("Sign", "Vorzeichen"),
            new[] { T("negative", "negativ"), T("zero", "null"), T("positive", "positiv") });

        private sealed class SignType : ObjectType, IValueType
        {
            public override string Id => "tt.sign";
            public override I18nText Name => new("Sign");
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("x", T("x", "x"), ParamKind.Text),
                new ParamDef("y", T("y", "y"), ParamKind.Text),
            };
            public IReadOnlyList<VarDef> Variables { get; } = new[]
            {
                new VarDef("x", T("x", "x"), T("x", "x"), Quantities.Length) { Unit = "m" },
                new VarDef("y", T("y", "y"), T("y", "y"), Quantities.Length) { Unit = "m" },
                new VarDef("s", T("Sign of x", "Vorzeichen von x"), T("s", "s"), SignKind) { Param = "sign" },
            };
            public IReadOnlyList<Relation> Relations { get; } = new[]
            {
                new Relation("tt.sign", T("Sign", "Vorzeichen"), "{s} = sign({x})", "s", "x")
                    .Solve("s", "sign({x})", v => Math.Sign(v("x")) + 1),
                // A relation that would use the class in arithmetic: never solved for y, never checked.
                new Relation("tt.bad", T("Bad", "Falsch"), "{y} = {s}", "y", "s")
                    .Solve("y", "{s}", v => v("s"))
                    .Solve("s", "{y}", v => v("y")),
            };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        private sealed class SignModule : IModule
        {
            public string Id => "tt";
            public I18nText Name => new("Text values");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[] { new SignType() };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        private static readonly Registry Reg = new Registry().Register(new SignModule());

        private static ValueTable Solve(params (string Key, ParamValue Value)[] ps)
        {
            var o = new SketchObject { Id = "o1", Type = "tt.sign" };
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            var doc = SketchDocument.Create();
            doc = doc with { Objects = new Dictionary<string, SketchObject> { [o.Id] = o }, Pages = new[] { doc.Pages[0] with { Order = new[] { o.Id } } } };
            return ValueSolver.Solve(doc, Reg, null);
        }

        [Fact]
        public void TheKindHasLabelsAndIsNotACalculatorKind()
        {
            Assert.True(SignKind.IsText);
            Assert.False(Quantities.Length.IsText);
            Assert.Equal("positiv", SignKind.LabelOf(2)!.De);
            Assert.Null(SignKind.LabelOf(1.5));
            Assert.Null(SignKind.LabelOf(3));
            Assert.Equal(1, SignKind.IndexOf(T("zero", "null")));
            Assert.DoesNotContain(SignKind, Quantities.All);
            Assert.Throws<ArgumentException>(() => Quantities.Text("t.empty", T("Empty", "Leer"), Array.Empty<I18nText>()));
        }

        [Fact]
        public void TheClassIsComputedAndShownAsItsLabel()
        {
            var t = Solve(("x", "-3 m"));
            var s = t.Get("o1", "s")!;
            Assert.Equal(Provenance.Derived, s.Provenance);
            Assert.Equal("negative", ValueFormat.Format(s.Value!.Value, t.UnitOf(s), Lang.En));
            Assert.Equal("negativ", ValueFormat.Format(s.Value!.Value, s.Def.Kind, Lang.De));
            Assert.Equal("–", ValueFormat.Format(7, s.Def.DisplayUnit, Lang.En));
            var step = t.Explain("o1", "s", Lang.En).Last();
            Assert.StartsWith("s = sign(x) = sign(", step.Text);
            Assert.EndsWith(" = negative", step.Text);
        }

        [Fact]
        public void TheClassNeverDerivesAnotherValueAndIsNeverChecked()
        {
            var t = Solve(("x", "2 m"));
            Assert.Equal(2, t.Get("o1", "s")!.Value);
            // y = s would give 2 m: the class is not a number for arithmetic.
            Assert.Null(t.Get("o1", "y")!.Value);
            // y entered against the class: no conflict either (the relation is not a check).
            var t2 = Solve(("x", "2 m"), ("y", "5 m"));
            Assert.Empty(t2.Conflicts);
        }

        [Fact]
        public void TheClassIsNeverEnteredNorOfferedAsMissing()
        {
            Assert.False(ValueParser.TryParse("2", SignKind, out _, out var error, Lang.En));
            Assert.Contains("cannot be entered", error!.En);
            var t = Solve(("sign", 2.0));
            Assert.Null(t.Get("o1", "s")!.Value);
            Assert.Equal(Provenance.Unknown, t.Get("o1", "s")!.Provenance);
            Assert.DoesNotContain(t.Missing("o1").SelectMany(m => m), k => k == "s");
            Assert.Contains(t.Missing("o1"), m => m.Contains("x"));
            // A drag preview of the class is ignored as well.
            var o = new SketchObject { Id = "o1", Type = "tt.sign" }.WithParam("x", "1 m");
            var doc = SketchDocument.Create();
            doc = doc with { Objects = new Dictionary<string, SketchObject> { [o.Id] = o }, Pages = new[] { doc.Pages[0] with { Order = new[] { o.Id } } } };
            var preview = ValueSolver.Solve(doc, Reg, null, new ValueSolveOptions { Overrides = new Dictionary<VarRef, double> { [new VarRef("o1", "s")] = 0 } });
            Assert.Equal(2, preview.Get("o1", "s")!.Value);
        }
    }
}
