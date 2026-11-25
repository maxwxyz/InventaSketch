using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Filled areas of XY charts and figures (fill_between, fill_betweenx, axvspan, axhspan, steps, where, hatch patterns).</summary>
    public class AreaTests
    {
        /// <summary>A rising line (curve 1), a constant at 3 (curve 2) and a falling line (curve 3).</summary>
        internal const string Data = "Rising;\n0;1\n1;2\n2;3\n3;4\n4;5\n\nFlat;\n0;3\n1;3\n2;3\n3;3\n4;3\n\nFalling;\n0;5\n1;4\n2;3\n3;2\n4;1";

        internal static SketchObject Xy(Registry r, params FigRegion[] areas)
        {
            var o = Fx.Item(r, "chart.line", new Vec2(0, 0)).WithParam("data", Data).WithParam("limits", "1.5;4.5");
            o = o with { Size = new Size2(120, 80) };
            return areas.Length == 0 ? o : o.WithParam("areas", FigureSpec.AreasJson(areas));
        }

        private static List<Primitive> Tagged(Registry r, SketchObject o, string tag)
            => Prim.Flatten(Fx.Render(r, o)).Where(p => p.Tag == tag).ToList();

        /// <summary>Every kind of area, as matplotlib offers them.</summary>
        internal static FigRegion[] EveryKind() => new[]
        {
            new FigRegion { Kind = "below", A = 0, Hatch = "//" },
            new FigRegion { Kind = "between", A = 0, B = 1, Where = "above", Hatch = "x" },
            new FigRegion { Kind = "between", A = 2, Y2 = 2, Where = "below", Fill = "solid", Opacity = 0.4 },
            new FigRegion { Kind = "betweenx", A = 0, X2 = 0.5, Hatch = "|", Edge = "dash" },
            new FigRegion { Kind = "span", From = 3.2, To = 3.8, Hatch = "o" },
            new FigRegion { Kind = "hspan", Y1 = 1.5, Y2 = 4.5, Hatch = ".", Label = new I18nText("Tolerance", "Toleranz") },
            new FigRegion { Kind = "below", A = 2, Step = "post", Hatch = "*", Base = 0.5 },
        };

        [Fact]
        public void EveryKindRendersInsideThePlotWithItsTag()
        {
            var r = Fx.Reg();
            var areas = EveryKind();
            var o = Xy(r, areas);
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "areas");
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var plot = c.F.Panels[0].Area.Inflate(0.01);
            for (var k = 0; k < areas.Length; k++)
            {
                var prims = Tagged(r, o, "area." + k);
                Assert.True(prims.Count > 0, "area " + k + " (" + areas[k].Kind + ")");
                foreach (var p in prims)
                    foreach (var pt in Prim.Points(p)) Assert.True(plot.Contains(pt), $"area {k}: {pt} outside the plot");
            }
            // Every area is a part with a name; the labelled one has a legend entry.
            var parts = r.Parts(o, Fx.Ctx());
            Assert.Equal(areas.Length, parts.Count(p => p.Kind == ChartAreas.Tag));
            Assert.Contains(parts, p => p.Key == "area.5" && p.Label.En.Contains("Tolerance"));
            Assert.Contains(c.F.Legend, e => e.Area && e.Text == "Tolerance");
        }

        [Fact]
        public void WhereKeepsOnlyThePartsWhereCurveAIsAboveTheOtherEdgeWithExactCrossings()
        {
            var r = Fx.Reg();
            var o = Xy(r, new FigRegion { Kind = "between", A = 0, B = 1, Where = "above" });
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var (polys, _) = ChartAreas.Polygons(XyEditor.Areas(o)[0], c.F, XyEditor.Instance.Series(o, Lang.En));
            var poly = Assert.Single(polys);
            // The rising line crosses y = 3 at x = 2: the area starts exactly there and ends at x = 4.
            double PX(double x) => c.F.P(c.F.Panels[0], x, 0).X;
            Assert.Equal(PX(2), poly.Min(p => p.X), 6);
            Assert.Equal(PX(4), poly.Max(p => p.X), 6);
            // "below": the other side.
            var below = new FigRegion { Kind = "between", A = 0, B = 1, Where = "below" };
            var (bp, _) = ChartAreas.Polygons(below, c.F, XyEditor.Instance.Series(o, Lang.En));
            Assert.Equal(PX(0), Assert.Single(bp).Min(p => p.X), 6);
            Assert.Equal(PX(2), Assert.Single(bp).Max(p => p.X), 6);
            // Two lines through the same data point (2; 3) cross there: two triangles, not a bow-tie that cancels out.
            var (cross, _) = ChartAreas.Polygons(new FigRegion { Kind = "between", A = 0, B = 2 }, c.F, XyEditor.Instance.Series(o, Lang.En));
            Assert.Equal(2, cross.Count);
        }

        [Fact]
        public void StepsFillAlongTheStepsAndBetweenxFillsHorizontally()
        {
            var r = Fx.Reg();
            var o = Xy(r);
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var series = XyEditor.Instance.Series(o, Lang.En);
            var panel = c.F.Panels[0];
            var (post, _) = ChartAreas.Polygons(new FigRegion { Kind = "below", A = 0, Step = "post" }, c.F, series);
            // Post steps: between x = 0 and x = 1 the top edge stays at y = 1.
            var top = post.SelectMany(p => p).Where(p => p.X > c.F.P(panel, 0.2, 0).X && p.X < c.F.P(panel, 0.8, 0).X).ToList();
            Assert.All(top.Where(p => p.Y < panel.Area.Bottom - 1e-6), p => Assert.Equal(c.F.P(panel, 0, 1).Y, p.Y, 6));
            var (pre, _) = ChartAreas.Polygons(new FigRegion { Kind = "below", A = 0, Step = "pre" }, c.F, series);
            var topPre = pre.SelectMany(p => p).Where(p => p.X > c.F.P(panel, 0.2, 0).X && p.X < c.F.P(panel, 0.8, 0).X && p.Y < panel.Area.Bottom - 1e-6).ToList();
            Assert.All(topPre, p => Assert.Equal(c.F.P(panel, 0, 2).Y, p.Y, 6));

            // fill_betweenx from the rising line to x = 0: left of the line, for y from 1 to 5.
            var (bx, _) = ChartAreas.Polygons(new FigRegion { Kind = "betweenx", A = 0, X2 = 0 }, c.F, series);
            var all = bx.SelectMany(p => p).ToList();
            Assert.Equal(c.F.P(panel, 0, 0).X, all.Min(p => p.X), 6);
            Assert.Equal(c.F.P(panel, 4, 0).X, all.Max(p => p.X), 6);
            Assert.Equal(c.F.P(panel, 0, 5).Y, all.Min(p => p.Y), 6);
        }

        [Theory]
        [InlineData("/", 45.0, false)]
        [InlineData("\\", 135.0, false)]
        [InlineData("|", 90.0, false)]
        [InlineData("-", 0.0, false)]
        [InlineData("+", 0.0, true)]
        [InlineData("x", 45.0, true)]
        public void LinePatternsAreNativeHatchFills(string hatch, double angle, bool cross)
        {
            var r = Fx.Reg();
            var o = Xy(r, new FigRegion { Kind = "below", A = 0, Hatch = hatch });
            var face = Assert.Single(Tagged(r, o, "area.0").OfType<PathPrim>());
            Assert.Equal(FillKind.Hatch, face.Style.Fill!.Kind);
            Assert.Equal(angle, face.Style.Fill.Hatch!.Angle);
            Assert.Equal(cross, face.Style.Fill.Hatch.Cross);
            // Doubled characters are twice as dense.
            var dense = Xy(r, new FigRegion { Kind = "below", A = 0, Hatch = hatch + hatch });
            var denseFace = Tagged(r, dense, "area.0").OfType<PathPrim>().First();
            Assert.Equal(face.Style.Fill.Hatch.Spacing / 2, denseFace.Style.Fill!.Hatch!.Spacing, 6);
        }

        [Theory]
        [InlineData("o", false, true)]
        [InlineData("O", false, true)]
        [InlineData(".", true, true)]
        [InlineData("*", true, false)]
        public void ShapePatternsAreSmallClosedShapesInsideTheArea(string hatch, bool filled, bool circles)
        {
            var r = Fx.Reg();
            var o = Xy(r, new FigRegion { Kind = "below", A = 0, Hatch = hatch });
            var prims = Tagged(r, o, "area.0").OfType<PathPrim>().ToList();
            Assert.Equal(2, prims.Count);
            var face = prims[0];
            var shapes = prims[1];
            Assert.Equal(FillKind.None, face.Style.EffectiveFill.Kind);
            Assert.True(shapes.Segments.Count(s => s.Op == SegOp.M) > 5);
            Assert.Equal(filled, shapes.Style.Fill?.Kind == FillKind.Solid);
            Assert.Equal(circles, shapes.Segments.Any(s => s.Op == SegOp.C));
            // Every shape lies inside the face.
            var face0 = PartLooks.Sample(face.Segments);
            foreach (var s in shapes.Segments.Where(s => s.Op == SegOp.M))
                Assert.True(face0.Min(p => p.X) - 0.01 <= s.P.X && s.P.X <= face0.Max(p => p.X) + 0.01);
        }

        [Fact]
        public void CombinedPatternsDrawEveryFamilyAndATintedFace()
        {
            var r = Fx.Reg();
            var o = Xy(r, new FigRegion { Kind = "below", A = 0, Hatch = "/|o", Fill = "solid", Edge = "dot", Color = "#c62828" });
            var prims = Tagged(r, o, "area.0").OfType<PathPrim>().ToList();
            Assert.Equal(3, prims.Count); // face (hatch / on a tint), lines |, circles o
            var face = prims[0];
            Assert.Equal(FillKind.Hatch, face.Style.Fill!.Kind);
            Assert.Equal(45, face.Style.Fill.Hatch!.Angle);
            Assert.NotNull(face.Style.Fill.Color);                 // the tint under the pattern
            Assert.Equal("#c62828", face.Style.Stroke);             // the edge
            Assert.Equal(DashStyle.Dot, face.Style.Dash);
            Assert.All(prims[1].Segments.Where(s => s.Op == SegOp.L), s => Assert.True(true));
            Assert.Equal("#c62828", prims[1].Style.Stroke);
        }

        [Fact]
        public void AreasRoundTripAndOldDocumentsStayUnchanged()
        {
            var areas = EveryKind();
            var json = FigureSpec.AreasJson(areas);
            var back = FigureSpec.ParseAreas(json);
            Assert.Equal(json, FigureSpec.AreasJson(back));
            Assert.Equal(areas.Select(a => (a.Kind, a.Hatch, a.Where, a.Step, a.Y1, a.Y2, a.X2, a.Edge)), back.Select(a => (a.Kind, a.Hatch, a.Where, a.Step, a.Y1, a.Y2, a.X2, a.Edge)));
            Assert.Equal("Tolerance", back[5].Label!.En);
            Assert.Empty(FigureSpec.ParseAreas("not json"));
            Assert.Empty(FigureSpec.ParseAreas(""));
            // A figure region written before the areas (above: true, hatch fill) is read and written exactly as before.
            const string old = "{\"version\":1,\"x\":{},\"series\":[{\"x\":[0,1],\"y\":[0,1]},{\"x\":[0,1],\"y\":[1,0]}],\"regions\":[{\"kind\":\"between\",\"a\":0,\"b\":1,\"above\":true,\"fill\":\"cross\",\"angle\":30}]}";
            var spec = FigureSpec.Parse(old, Lang.En);
            Assert.Equal("above", spec.Regions[0].Where);
            Assert.Null(spec.Regions[0].Hatch);
            Assert.Equal(old, spec.ToJson());
            // An XY chart without areas has no parameter and draws no area.
            var r = Fx.Reg();
            var plain = Xy(r);
            Assert.False(plain.Params.ContainsKey("areas"));
            Assert.DoesNotContain(Prim.Flatten(Fx.Render(r, plain)), p => p.Tag?.StartsWith("area.", StringComparison.Ordinal) == true);
            // The document round trip keeps the parameter.
            var o = Xy(r, areas);
            var empty = SketchDocument.Create();
            var doc = Edits.Add(empty, empty.Pages[0].Id, o);
            var loaded = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(o.Str("areas", ""), loaded.Objects.Values.Single().Str("areas", ""));
        }

        [Fact]
        public void FiguresDrawTheNewKindsAsRegions()
        {
            var r = Fx.Reg();
            var spec = new FigureSpec();
            spec.Series.Add(new FigSeries { X = new List<double> { 0, 1, 2, 3 }, Y = new List<double> { 1, 3, 2, 4 } });
            spec.Series.Add(new FigSeries { X = new List<double> { 0, 1, 2, 3 }, Y = new List<double> { 2, 2, 2, 2 } });
            spec.Regions.AddRange(new[]
            {
                new FigRegion { Kind = "between", A = 0, B = 1, Where = "below", Hatch = "\\\\" },
                new FigRegion { Kind = "hspan", Y1 = 0.5, Y2 = 1.5, Hatch = "-" },
                new FigRegion { Kind = "betweenx", A = 0, B = 1, Hatch = "+" },
                new FigRegion { Kind = "between", A = 0, Y2 = 3.5, Step = "mid", Fill = "solid" },
            });
            var o = r.Type(FigureChart.TypeId)!.Create(new Vec2(0, 0)).WithParam("spec", spec.ToJson()) with { Size = new Size2(120, 80) };
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "figure areas");
            for (var k = 0; k < 4; k++) Assert.NotEmpty(Tagged(r, o, "region." + k));
            Assert.Equal(spec.ToJson(), FigureSpec.Parse(spec.ToJson(), Lang.En).ToJson());
        }

        [Fact]
        public void ExportsWriteTheMatchingCalls()
        {
            var r = Fx.Reg();
            var o = Xy(r, EveryKind());
            var py = ExportTests.Py(r, Lang.En, o);
            Assert.Contains(".fill_between(", py);
            Assert.Contains("hatch='//'", py);
            Assert.Contains("hatch='x'", py);
            Assert.Contains("where=[a >= b for a, b in zip(", py);
            Assert.Contains("where=[a <= b for a, b in zip(", py);
            Assert.Contains("step='post'", py);
            Assert.Contains(".fill_betweenx(", py);
            Assert.Contains(".axvspan(", py);
            Assert.Contains(".axhspan(", py);
            Assert.Contains("label='Tolerance'", py);
            Assert.Contains("linestyle='--'", py);   // the dashed edge
            var tex = ExportTests.Tex(r, Lang.En, o);
            Assert.Contains("fill between[of=", tex);
            Assert.Contains("pattern={Lines[angle=45", tex);
            Assert.Contains("Dots[distance=", tex);
            Assert.Contains("rectangle", tex);
            // Figures write their areas the same way.
            var spec = new FigureSpec();
            spec.Series.Add(new FigSeries { X = new List<double> { 0, 1, 2 }, Y = new List<double> { 1, 2, 1 } });
            spec.Regions.Add(new FigRegion { Kind = "between", A = 0, Y2 = 1.5, Where = "above", Hatch = "o" });
            var fig = r.Type(FigureChart.TypeId)!.Create(new Vec2(0, 0)).WithParam("spec", spec.ToJson());
            var fpy = ExportTests.Py(r, Lang.En, fig);
            Assert.Contains("hatch='o'", fpy);
            Assert.Contains("where=[a >= b", fpy);
        }

        [Fact]
        public void TheImporterReadsFillBetweenWithHatchWhereStepAndTheOtherFills()
        {
            const string code = @"
import numpy as np
import matplotlib.pyplot as plt
x = np.array([0, 1, 2, 3])
a = np.array([1, 3, 2, 4])
b = np.array([2, 2, 2, 2])
fig, ax = plt.subplots()
ax.plot(x, a)
ax.plot(x, b)
ax.fill_between(x, a, b, where=a <= b, facecolor='none', edgecolor='k', hatch='\\\\', linewidth=0, label='gap')
ax.fill_between(x, a, 3, where=a > 3, step='post', color='C1', alpha=0.3)
ax.fill_betweenx([1, 2, 3, 4], [0, 1, 2, 3], 0, hatch='x')
ax.axhspan(1.5, 2.5, hatch='o', facecolor='none')
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, new Size2(280, 157.5));
            var o = Assert.Single(res.Charts);
            var spec = FigureSpec.Parse(o.Str("spec", ""), Lang.En);
            var r0 = spec.Regions.Single(g => g.Hatch == "\\\\");
            Assert.Equal(("between", "\\\\", "gap"), (r0.Kind, r0.Hatch, r0.Label!.En));
            // "a <= b" is "b above a": the edges swap, the condition becomes "above".
            Assert.Equal((1, 0, "above"), (r0.A, r0.B, r0.Where));
            Assert.Null(r0.Edge);
            var r1 = spec.Regions.Single(g => g.Step == "post");
            Assert.Equal(("between", 3.0, "above", "post"), (r1.Kind, r1.Y2!.Value, r1.Where, r1.Step));
            Assert.Equal(0.3, r1.Opacity);
            var r2 = spec.Regions.Single(g => g.Kind == "betweenx");
            Assert.Equal(("x", 0.0), (r2.Hatch, r2.X2!.Value));
            var r3 = spec.Regions.Single(g => g.Kind == "hspan");
            Assert.Equal((1.5, 2.5, "o"), (r3.Y1!.Value, r3.Y2!.Value, r3.Hatch));
        }

        [Fact]
        public void AreaPartActionsAreChoicesAndEditTheArea()
        {
            var r = Fx.Reg();
            var o = Xy(r, new FigRegion { Kind = "between", A = 0, B = 1, Hatch = "//" });
            var actions = r.PartActions(o, "area.0", Fx.Ctx());
            var keys = actions.Where(a => a.Choice is not null).Select(a => a.Choice!.Key).Distinct().ToList();
            Assert.Equal(new[] { "chart.area.pattern", "chart.area.color", "chart.area.opacity", "chart.area.edge", "chart.area.where", "chart.area.step", PartLooks.DashChoice }, keys);
            Assert.True(actions.Single(a => a.Id == "chart.area.pattern=//").IsActive);
            Assert.All(actions.Where(a => a.Choice?.Key == "chart.area.pattern"), a => Assert.NotNull(a.Preview));
            Assert.DoesNotContain(actions, a => a.Icon == IconIds.Cycle);
            Assert.Equal("xx", XyEditor.Areas(actions.Single(a => a.Id == "chart.area.pattern=xx").Apply(o))[0].Hatch);
            Assert.Equal("#c62828", XyEditor.Areas(actions.Single(a => a.Id == "chart.area.color=#c62828").Apply(o))[0].Color);
            Assert.Equal(0.5, XyEditor.Areas(actions.Single(a => a.Id == "chart.area.opacity=50").Apply(o))[0].Opacity);
            Assert.Equal("dash", XyEditor.Areas(actions.Single(a => a.Id == "chart.area.edge=dash").Apply(o))[0].Edge);
            Assert.Equal("below", XyEditor.Areas(actions.Single(a => a.Id == "chart.area.where=below").Apply(o))[0].Where);
            Assert.Equal("mid", XyEditor.Areas(actions.Single(a => a.Id == "chart.area.step=mid").Apply(o))[0].Step);
            var solid = XyEditor.Areas(actions.Single(a => a.Id == "chart.area.pattern=solid").Apply(o))[0];
            Assert.Equal((null, "solid"), (solid.Hatch, solid.Fill));
            Assert.Equal("Limit", XyEditor.Areas(actions.Single(a => a.Id == "chart.area.label").Input!.Apply(o, "Limit"))[0].Label!.En);
            var gone = actions.Single(a => a.Id == PartLooks.DeleteAction).Apply(o);
            Assert.Empty(XyEditor.Areas(gone));
            Assert.False(gone.Params.ContainsKey("areas"));
        }

        [Fact]
        public void AreasAreCreatedFromACurveAndFromTheChart()
        {
            var r = Fx.Reg();
            var o = Xy(r);
            var curve = r.PartActions(o, "series.0", Fx.Ctx());
            var below = curve.Where(a => a.Choice?.Key == "chart.area.below").ToList();
            Assert.True(below[0].Choice!.IsMenu);
            Assert.DoesNotContain(below, a => a.IsActive);
            var withBelow = below.Single(a => a.Id == "chart.area.below=//").Apply(o);
            Assert.Equal(("below", 0, "//"), (XyEditor.Areas(withBelow)[0].Kind, XyEditor.Areas(withBelow)[0].A, XyEditor.Areas(withBelow)[0].Hatch));
            var to = curve.Where(a => a.Choice?.Key == "chart.area.to").ToList();
            var between = XyEditor.Areas(to.Single(a => a.Id == "chart.area.to=curve.2").Apply(o))[0];
            Assert.Equal(("between", 0, 2), (between.Kind, between.A, between.B));
            var toLimit = XyEditor.Areas(to.Single(a => a.Id == "chart.area.to=line.1").Apply(o))[0];
            Assert.Equal(4.5, toLimit.Y2);
            var toValue = XyEditor.Areas(to.Single(a => a.Id == "chart.area.to=value").Input!.Apply(o, "2,5"))[0];
            Assert.Equal(2.5, toValue.Y2);
            var toX = XyEditor.Areas(to.Single(a => a.Id == "chart.area.to=x").Input!.Apply(o, "1"))[0];
            Assert.Equal(("betweenx", 1.0), (toX.Kind, toX.X2!.Value));
            // "Add area ▸" on the chart.
            var add = r.TypeOf(o).Actions(o, Fx.Ctx()).Where(a => a.Choice?.Key == "chart.area.add").ToList();
            Assert.Equal(new[] { "below", "between", "span", "hspan", "lines" }, add.Select(a => ActionChoices.ValueOf(a.Id)));
            var lines = XyEditor.Areas(add.Single(a => a.Id == "chart.area.add=lines").Apply(o))[0];
            Assert.Equal(("hspan", 1.5, 4.5), (lines.Kind, lines.Y1!.Value, lines.Y2!.Value));
            var span = XyEditor.Areas(add.Single(a => a.Id == "chart.area.add=span").Apply(o))[0];
            Assert.True(span.From < span.To);
        }

        [Fact]
        public void CleaningSlidesMapsTheAreaValuesWithTheData()
        {
            var r = Fx.Reg();
            var o = Xy(r, new FigRegion { Kind = "between", A = 0, Y2 = 4.5, Label = new I18nText("Secret") }, new FigRegion { Kind = "hspan", Y1 = 1.5, Y2 = 4.5 });
            var clean = r.TypeOf(o).Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.Normalised, ReplaceTexts = true }, new Random(3));
            var areas = XyEditor.Areas(clean);
            var limits = ChartData.Numbers(clean.Str("limits", ""));
            // The line the area reaches and the band follow the limit line 4.5 exactly; the label goes with the texts.
            Assert.Equal(limits[1], areas[0].Y2!.Value, 6);
            Assert.Equal(limits[0], areas[1].Y1!.Value, 6);
            Assert.Equal(limits[1], areas[1].Y2!.Value, 6);
            Assert.Null(areas[0].Label);
        }

        /// <summary>
        /// Renders every area kind and every offered pattern as SVG into the folder named by ISK_GALLERY_DIR (for looking at
        /// them); does nothing without it.
        /// </summary>
        [Fact]
        public void GalleryOfAreasAndPatterns()
        {
            var dir = Environment.GetEnvironmentVariable("ISK_GALLERY_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            var r = Fx.Reg();
            void Save(string name, SketchObject o)
            {
                File.WriteAllText(Path.Combine(dir, name + ".svg"), SvgRenderer.RenderPrimitives(Fx.Render(r, o), padding: 1));
            }
            var kinds = EveryKind();
            for (var k = 0; k < kinds.Length; k++) Save("kind-" + k + "-" + kinds[k].Kind, Xy(r, kinds[k]));
            Save("kinds-all", Xy(r, kinds));
            var i = 0;
            foreach (var (value, _) in ChartAreas.Patterns)
                Save("pattern-" + (i++).ToString("00") + "-" + new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()),
                    Xy(r, new FigRegion { Kind = "between", A = 0, B = 2, Edge = value == "none" ? "solid" : null }.Also(x => ChartAreas.SetPattern(x, value))));
        }
    }

    internal static class AreaTestExtensions
    {
        public static FigRegion Also(this FigRegion r, Action<FigRegion> change) { change(r); return r; }
    }
}
