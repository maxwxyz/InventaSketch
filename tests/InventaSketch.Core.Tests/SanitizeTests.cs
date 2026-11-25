using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Clean slides for sharing: options, the generic text/metadata pass, the report and reference names.</summary>
    public class SanitizeTests
    {
        private sealed class LabelModule : IModule
        {
            public string Id => "sanitest";
            public I18nText Name => new("Sanitize test");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[] { new LabelType(), new NumeralType(), new ValueType() };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        /// <summary>A label with free text, a title parameter and a structured list parameter.</summary>
        private sealed class LabelType : ObjectType
        {
            public override string Id => "sani.label";
            public override I18nText Name => new("Label", "Beschriftung");
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("title", new I18nText("Title"), ParamKind.Text),
                new ParamDef("items", new I18nText("Items"), ParamKind.Text),
            };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, Style.Visible) };
        }

        /// <summary>A numeral: its object text is the numeral, not prose.</summary>
        private sealed class NumeralType : ObjectType
        {
            public override string Id => "sani.numeral";
            public override I18nText Name => new("Numeral");
            public override IReadOnlyList<ParamDef> Parameters => new[] { new ParamDef("numeral", new I18nText("Numeral"), ParamKind.Text) };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, Style.Visible) };
        }

        /// <summary>A type with a value: doubled when randomised.</summary>
        private sealed class ValueType : ObjectType
        {
            public override string Id => "sani.value";
            public override I18nText Name => new("Value", "Wert");
            public override IReadOnlyList<ParamDef> Parameters => new[] { new ParamDef("v", new I18nText("Value"), ParamKind.Number) { Default = 1.0 } };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, Style.Visible) };
            public override SketchObject Sanitize(SketchObject o, SanitizeOptions opt, Random rng)
            {
                var r = base.Sanitize(o, opt, rng);
                return opt.Mode == SanitizeMode.Randomised ? r.WithParam("v", o.Num("v", 1) * SanitizeText.Away(rng, opt.Variation)) : r;
            }
        }

        private static Registry Reg() => new Registry().Register(new LabelModule());

        private static (SketchDocument Doc, string Label, string Numeral, string Value) Doc()
        {
            var doc = SketchDocument.Create();
            var label = new SketchObject { Id = "a", Type = "sani.label", Text = "Secret rotor\nsecond line", Meta = new Dictionary<string, string> { ["import.source"] = "C:/secret.xlsx" } }
                .WithParam("title", "Project Falcon").WithParam("items", "a;b;c");
            var numeral = new SketchObject { Id = "b", Type = "sani.numeral", Text = "10", Ref = "10" }.WithParam("numeral", "10");
            var value = new SketchObject { Id = "c", Type = "sani.value" }.WithParam("v", 42.0);
            doc = Edits.Add(doc, doc.Pages[0].Id, label, numeral, value);
            doc = doc with { References = new Dictionary<string, ReferenceEntry> { ["10"] = new("10", "secret rotor", "made of unobtainium") } };
            return (doc, "a", "b", "c");
        }

        [Theory]
        [InlineData("normalised", SanitizeMode.Normalised)]
        [InlineData("Normalized", SanitizeMode.Normalised)]
        [InlineData("randomised", SanitizeMode.Randomised)]
        [InlineData("randomized", SanitizeMode.Randomised)]
        [InlineData("none", SanitizeMode.None)]
        public void ModeNamesAreRead(string text, SanitizeMode mode) => Assert.Equal(mode, SanitizeOptions.ParseMode(text));

        [Fact]
        public void UnknownModeIsNullAndSpreadIsClamped()
        {
            Assert.Null(SanitizeOptions.ParseMode("shuffle"));
            Assert.Equal(0.9, new SanitizeOptions { Spread = 5 }.Variation);
            Assert.Equal(0.02, new SanitizeOptions { Spread = -1 }.Variation);
            Assert.Equal(SanitizeMode.Normalised, new SanitizeOptions().Mode);
        }

        [Fact]
        public void PlaceholderKeepsLinesAndWordCounts()
        {
            var p = SanitizeText.Placeholder("Secret rotor temperature\nsecond line", new Random(1));
            var lines = p.Split('\n');
            Assert.Equal(2, lines.Length);
            Assert.Equal(3, lines[0].Split(' ').Length);
            Assert.Equal(2, lines[1].Split(' ').Length);
            Assert.True(char.IsUpper(lines[0][0]));
            Assert.DoesNotContain("rotor", p);
            Assert.Equal("", SanitizeText.Placeholder("", new Random(1)));
        }

        [Fact]
        public void LettersAndNumbers()
        {
            Assert.Equal("A", SanitizeText.Letter(0));
            Assert.Equal("Z", SanitizeText.Letter(25));
            Assert.Equal("AA", SanitizeText.Letter(26));
            Assert.Equal("12 kΩ", SanitizeText.MapNumbers("10 kΩ", v => v * 1.2));
            Assert.Equal("3,1 m", SanitizeText.MapNumbers("2,5 m", v => v * 1.25));
            Assert.Equal("R1 = 20", SanitizeText.MapNumbers("R1 = 10", v => v * 2));
            Assert.Equal("0.5", SanitizeText.MapNumbers("10", v => v / 20, 2));
            Assert.Equal(1230, SanitizeText.Round(1234.5, 3));
            Assert.Equal(0.0123, SanitizeText.Round(0.012345, 3), 10);
            for (var i = 0; i < 200; i++)
            {
                var k = SanitizeText.Away(new Random(i), 0.25);
                Assert.True(k >= 1.2 - 1e-9 || k <= 1 / 1.2 + 1e-9, "factor " + k);
            }
        }

        [Fact]
        public void TextsMetadataAndReferenceNamesAreReplaced()
        {
            var (doc, a, b, c) = Doc();
            var opt = new SanitizeOptions { Mode = SanitizeMode.None, ReplaceTexts = true, StripMeta = true, Seed = 7 };
            var (clean, report) = Sanitizer.Apply(Reg(), doc, new[] { a, b, c }, opt);
            var la = clean.Get(a)!;
            Assert.DoesNotContain("Secret", la.Text);
            Assert.Equal(2, la.Text!.Split('\n').Length);
            Assert.DoesNotContain("Falcon", la.Str("title", ""));
            Assert.Equal("a;b;c", la.Str("items", ""));       // structured parameters are left to the type
            Assert.Null(la.Meta);
            Assert.Equal("10", clean.Get(b)!.Text);           // the numeral is not prose
            Assert.Equal(42, clean.Get(c)!.Num("v", 0));      // mode none: no values
            Assert.Equal("Part 10", clean.References["10"].Label);
            Assert.Null(clean.References["10"].Note);

            Assert.Equal(3, report.Elements);
            Assert.Equal(1, report.Changed);
            Assert.Equal(1, report.Texts);
            Assert.Equal(1, report.Metadata);
            Assert.Equal(0, report.Values);
            Assert.Equal(1, report.References);
            Assert.Equal(7, report.Seed);
            Assert.Equal("sani.label", Assert.Single(report.ByType).TypeId);
            Assert.Contains("1 of 3 elements cleaned", report.Summary.Get(Lang.En));
            Assert.Contains("1 von 3 Elementen bereinigt", report.Summary.Get(Lang.De));
            Assert.Contains(report.Lines, l => l.Get(Lang.De).StartsWith("Beschriftung: 1", StringComparison.Ordinal));
            // The original document is untouched.
            Assert.Equal("Secret rotor\nsecond line", doc.Get(a)!.Text);
        }

        [Fact]
        public void ValuesAreCountedAndReproducibleWithTheSeed()
        {
            var (doc, _, _, c) = Doc();
            var opt = new SanitizeOptions { Mode = SanitizeMode.Randomised, Seed = 11 };
            var (one, report) = Sanitizer.Apply(Reg(), doc, new[] { c }, opt);
            var (two, _) = Sanitizer.Apply(Reg(), doc, new[] { c }, opt);
            var (other, _) = Sanitizer.Apply(Reg(), doc, new[] { c }, opt with { Seed = 12 });
            Assert.NotEqual(42, one.Get(c)!.Num("v", 0));
            Assert.Equal(one.Get(c)!.Num("v", 0), two.Get(c)!.Num("v", 0));
            Assert.NotEqual(one.Get(c)!.Num("v", 0), other.Get(c)!.Num("v", 0));
            Assert.Equal(1, report.Values);
            Assert.Equal(0, report.Texts);
            Assert.Contains("randomised", report.Summary.Get(Lang.En));
        }

        [Fact]
        public void NothingToCleanKeepsTheDocumentAndUnknownTypesAreCounted()
        {
            var (doc, a, b, _) = Doc();
            doc = Edits.Add(doc, doc.Pages[0].Id, new SketchObject { Id = "x", Type = "nobody.knows" });
            var (same, report) = Sanitizer.Apply(Reg(), doc, new[] { a, b, "x", "missing" }, new SanitizeOptions { Mode = SanitizeMode.Normalised, Seed = 1 });
            Assert.Same(doc, same);
            Assert.False(report.Any);
            Assert.Equal(1, report.Unknown);
            Assert.Equal(3, report.Elements);
            Assert.StartsWith("Nothing to clean", report.Summary.Get(Lang.En));
            Assert.StartsWith("Nichts zu bereinigen", report.Summary.Get(Lang.De));
        }

        [Fact]
        public void ApplyToPageCleansEveryElementOfThePage()
        {
            var (doc, _, _, _) = Doc();
            var (_, report) = Sanitizer.ApplyToPage(Reg(), doc, doc.Pages[0].Id, new SanitizeOptions { Mode = SanitizeMode.Randomised, ReplaceTexts = true, Seed = 3 });
            Assert.Equal(3, report.Elements);
            Assert.Equal(2, report.Changed);
        }
    }
}
