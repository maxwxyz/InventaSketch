using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Charts v2 stage 2: the figure specification (JSON) – round trip and tolerant parsing.</summary>
    public class FigureSpecTests
    {
        public static readonly string[] Presets = { FigurePresets.Patent, FigurePresets.Timing, FigurePresets.Temperature };

        [Fact]
        public void PresetsRoundTripCanonically()
        {
            foreach (var json in Presets)
            {
                Assert.True(FigureSpec.TryParse(json, out var spec, out var error), error);
                Assert.Equal(json, spec!.ToJson());
                Assert.Equal(json, spec.Clone().ToJson());
                Assert.StartsWith("{\"version\":1", json);
            }
        }

        [Fact]
        public void RoundTripKeepsEveryElement()
        {
            var s = new FigureSpec
            {
                Title = new I18nText("Fig. 1", "Abb. 1"), Caption = new I18nText("below"), Scheme = "mono", Legend = "inside", GridStyle = "dot", PanelGap = 2.5,
                X = new FigAxis { Mode = "principle", Min = 0, Max = 10, Label = new I18nText("Time t", "Zeit t"), Unit = new I18nText("s"), Grid = true, Marks = { new FigMark(3, "t1") } },
                Panels = { new FigPanel { Scale = "log", Min = 1, Max = 100, Share = 2, Title = new I18nText("A") }, new FigPanel { Mode = "principle" } },
                Series =
                {
                    new FigSeries { X = { 0, 1, double.NaN, 3 }, Y = { 1, 2, double.NaN, 4.5 }, Panel = 1, Line = "dashdot", Width = 0.7, Color = "#123456", Marker = "square", MarkerOpen = true,
                        Step = true, Smooth = true, Label = new I18nText("curve", "Kurve"), LabelAt = 2, LabelPos = "below", Numeral = new FigNumeral { Text = "12", At = 1.5, Dx = 2, Dy = 1 } },
                },
                Refs = { new FigRef { Kind = "h", Y = 3, Panel = -1, Label = new I18nText("max"), LabelPos = "end", Line = "dot", Width = 0.4, Color = "#ff0000", Numeral = new FigNumeral { Text = "9", Axis = true } },
                         new FigRef { Kind = "point", X = 1, Series = 0, Filled = true } },
                Regions = { new FigRegion { Kind = "between", A = 0, B = 1, Above = true, From = 1, To = 2, Panel = 1, Fill = "cross", Angle = 30, Spacing = 2, Color = "#00ff00", Opacity = 0.5,
                    Numeral = new FigNumeral { Text = "7", X = 1, Y = 2, Panel = 1 } } },
                Notes = { new FigNote { Kind = "dim", P1 = new FigPoint { X = 1, Gap = 0.5 }, P2 = new FigPoint { X = 4, Y = 2, Panel = 1 }, Text = new I18nText("35 K"), Anchor = "end",
                    Italic = true, Size = 1.2, Line = "dash", Side = "left", Dx = 0.5, Dy = 0.5, MaxWidth = 3, Box = true } },
            };
            var json = s.ToJson();
            Assert.True(FigureSpec.TryParse(json, out var back, out _));
            Assert.Equal(json, back!.ToJson());
            Assert.Contains("null", json);                                // the gap
            Assert.Equal("Zeit t", back.X.Label!.Get(Lang.De));
            Assert.True(double.IsNaN(back.Series[0].X[2]));
            Assert.Equal(4.5, back.Series[0].Y[3]);
            Assert.Equal(-1, back.Refs[0].Panel);
            Assert.True(back.Notes[0].P1.Gap is 0.5);
        }

        [Fact]
        public void ParsingIsTolerant()
        {
            const string json = @"{
                // comments and trailing commas are accepted
                ""version"": ""1"", ""unknown"": [1, 2], ""scheme"": ""MONO"", ""legend"": 5,
                ""x"": { ""mode"": ""principle"", ""min"": ""0"", ""max"": ""2,5"", ""label"": { ""de"": ""nur deutsch"" }, ""marks"": [ { ""at"": 1 }, ""x"", { ""at"": 2, ""text"": 7 } ] },
                ""panels"": [ { ""share"": -3 }, 5, { ""mode"": ""nonsense"" } ],
                ""series"": [ { ""y"": [1, ""2"", null, ""n/a"", 3] }, { ""points"": [[0, 1], [1], [2, 2]] }, 7 ],
                ""refs"": [ { ""kind"": ""v"", ""x"": 1 }, { ""kind"": ""weird"" } ],
                ""regions"": [ { ""fill"": ""plaid"", ""opacity"": 4 } ],
                ""notes"": [ { ""kind"": ""text"", ""text"": 42 } ],
            }";
            Assert.True(FigureSpec.TryParse(json, out var s, out var error), error);
            Assert.Equal("mono", s!.Scheme);
            Assert.Equal("none", s.Legend);
            Assert.Equal(2.5, s.X.Max);
            Assert.Equal("nur deutsch", s.X.Label!.En);
            Assert.Single(s.X.Marks);                                     // marks without text are skipped
            Assert.Equal("7", s.X.Marks[0].Text.En);
            Assert.Equal(2, s.Panels.Count);
            Assert.Equal(1, s.Panels[0].Share);
            Assert.Equal("data", s.Panels[1].Mode);
            Assert.Equal(2, s.Series.Count);
            Assert.Equal(new double[] { 0, 1, 2, 3, 4 }, s.Series[0].X);  // x defaults to 0, 1, 2 …
            Assert.True(double.IsNaN(s.Series[0].Y[2]) && double.IsNaN(s.Series[0].Y[3]));
            Assert.True(double.IsNaN(s.Series[1].X[1]));                  // a point with one value is a gap
            Assert.Equal("h", s.Refs[1].Kind);
            Assert.Equal("hatch", s.Regions[0].Fill);
            Assert.Null(s.Regions[0].Opacity);
            Assert.Equal("42", s.Notes[0].Text!.En);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("{\"x\": ")]
        [InlineData("[1, 2]")]
        [InlineData("42")]
        public void InvalidJsonFailsWithoutThrowing(string text)
        {
            Assert.False(FigureSpec.TryParse(text, out var s, out var error));
            Assert.Null(s);
            Assert.False(string.IsNullOrEmpty(error));
            var ex = Assert.Throws<FormatException>(() => FigureSpec.Parse(text, Lang.De));
            Assert.StartsWith("Ungültige Bildspezifikation", ex.Message);
            Assert.StartsWith("Invalid figure specification", Assert.Throws<FormatException>(() => FigureSpec.Parse(text, Lang.En)).Message);
        }

        [Fact]
        public void EmptyTextIsAnEmptyFigure()
        {
            Assert.True(FigureSpec.TryParse("  ", out var s, out _));
            Assert.Empty(s!.Series);
            Assert.Equal("{\"version\":1,\"x\":{}}", s.ToJson());
        }

        [Fact]
        public void DefaultSpecIsThePatentFigure()
        {
            Assert.Equal(FigurePresets.Patent, FigureChart.SpecDef.Default?.Text);
            var o = new SketchObject { Id = "f", Type = FigureChart.TypeId, Size = new Size2(100, 80) };
            Assert.Equal(FigurePresets.Patent, FigureChart.SpecText(o));
        }
    }
}
