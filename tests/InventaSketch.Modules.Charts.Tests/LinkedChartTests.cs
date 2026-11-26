using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>
    /// A test element with values and a characteristic curve (Calc 5 contract, <see cref="ICharacteristics"/>): y = k·x with
    /// k a curve parameter, the operating point at the element's x, dragged along x.
    /// </summary>
    internal sealed class CurveElement : ObjectType, IValueType, ICharacteristics
    {
        public const string TypeId = "ctest.element";

        public override string Id => TypeId;
        public override I18nText Name => new("Curve element", "Kennlinienelement");
        public override Size2 DefaultSize => new(20, 10);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            new ParamDef("x", new I18nText("x", "x"), ParamKind.Text),
            new ParamDef("y", new I18nText("y", "y"), ParamKind.Text),
            new ParamDef("k", new I18nText("Slope", "Steigung"), ParamKind.Text),
        };

        public IReadOnlyList<VarDef> Variables { get; } = new[]
        {
            new VarDef("x", new I18nText("Input", "Eingang"), new I18nText("x", "x"), Quantities.Ratio) { Min = 0, Max = 100 },
            new VarDef("y", new I18nText("Output", "Ausgang"), new I18nText("y", "y"), Quantities.Ratio) { Min = 0, Max = 1000 },
        };

        public IReadOnlyList<Relation> Relations { get; } = new[]
        {
            new Relation("ctest.double", new I18nText("Double", "Doppelt"), "{y} = 2·{x}", "y", "x") { Source = "S1" }
                .Solve("y", "2·{x}", v => 2 * v("x"))
                .Solve("x", "{y}/2", v => v("y") / 2),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, Style.Visible) };

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
            => new[] { new CharacteristicInfo("line", new I18nText("Line", "Gerade")) };

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
        {
            if (key != "line") return null;
            var k = c.Param("k", Quantities.Ratio) ?? 1;
            var x = c.Value("x") ?? 5;
            var set = new CharacteristicSet("line", new I18nText("Line", "Gerade"),
                new CharacteristicAxis(new I18nText("Input x", "Eingang x"), "") { Min = 0, Max = 10 },
                new CharacteristicAxis(new I18nText("Output", "Ausgang"), "") { Min = 0 });
            set.Curves.Add(new CharacteristicCurve("line", new I18nText("y = k·x", "y = k·x"), new[] { new Vec2(0, 0), new Vec2(10, 10 * k) }));
            set.Bands.Add(new CharacteristicBand("band", new I18nText("Window", "Fenster"), 2, 4));
            set.Lines.Add(new CharacteristicLine("max", new I18nText("Limit", "Grenze"), 9));
            var id = o.Id;
            set.Points.Add(new CharacteristicPoint("op", new I18nText("Operating point", "Betriebspunkt"), x, k * x)
            {
                Motion = PointMotion.AlongX,
                Write = nx => new[] { new ValueWrite(id, "x", nx) },
            });
            return set;
        }

        public override bool DependsOnNeighbours => true;
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Characteristics.LinkedCharts(ctx.Doc, o.Id);
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx) => Characteristics.InsertActions(this, o, ctx);
    }

    internal sealed class CurveTestModule : IModule
    {
        public string Id => "ctest";
        public I18nText Name => new("Curve test", "Kennlinientest");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new CurveElement() };
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }

    /// <summary>Calc 5: linked characteristic charts – link, render, grip, unlink, persistence (host-free).</summary>
    public class LinkedChartTests
    {
        private static Registry Reg() => new Registry().Register(new ChartsModule()).Register(new CurveTestModule());

        private static SketchDocument WithElement(Registry r, string x = "3", string? k = "2")
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Linked");
            var e = new SketchObject { Id = "e1", Type = CurveElement.TypeId, Transform = new Transform(50, 50), Size = new Size2(20, 10) }.WithParam("x", x);
            if (k is not null) e = e.WithParam("k", k);
            return Edits.Add(doc, doc.Pages[0].Id, e);
        }

        /// <summary>The element's "Characteristic chart ▸" command run like a host does (one document change).</summary>
        private static SketchDocument Insert(Registry r, SketchDocument doc)
            => RunAction(r, doc, "e1", Characteristics.InsertChoice + "=line");

        /// <summary>Runs an action like a host with the registry in the context (the chart types are shared by every registry of the test run).</summary>
        private static SketchDocument RunAction(Registry r, SketchDocument doc, string id, string actionId)
        {
            var o = doc.Get(id)!;
            var ctx = new RenderContext(doc, Lang.En).For(r);
            return r.Type(o.Type)!.Actions(o, ctx).Single(a => a.Id == actionId).Run(doc, o, ctx);
        }

        private static SketchObject Chart(SketchDocument doc) => doc.Objects.Values.Single(o => o.Type == LinkedChart.TypeId || o.Type == FigureChart.TypeId);

        [Fact]
        public void InsertAddsALinkedChartRightOfTheElementAsItsDependent()
        {
            var r = Reg();
            var doc = WithElement(r);
            var e = doc.Get("e1")!;
            var actions = r.Type(e.Type)!.Actions(e, new RenderContext(doc).For(r)).ToList();
            var insert = Assert.Single(actions);
            Assert.Equal("values.chart=line", insert.Id);
            Assert.True(insert.Choice!.IsMenu);
            doc = Insert(r, doc);
            var chart = Chart(doc);
            Assert.Equal(LinkedChart.TypeId, chart.Type);
            Assert.Equal("e1#line", chart.Str("link", ""));
            Assert.True(chart.Bounds().X > e.Bounds().Right);
            Assert.Equal(new[] { chart.Id }, r.Type(e.Type)!.Dependents(e, new RenderContext(doc)).ToArray());
            Assert.True(r.Type(chart.Type)!.DependsOnNeighbours);
        }

        [Fact]
        public void LinkedChartRendersTheSetAndFollowsTheElement()
        {
            var r = Reg();
            var doc = Insert(r, WithElement(r));
            var chart = Chart(doc);
            var type = (LinkedChart)r.Type(chart.Type)!;
            var set = type.SetOf(chart, new RenderContext(doc).For(r))!;
            Assert.Equal(3, set.Point("op")!.X);
            Assert.Equal(6, set.Point("op")!.Y);
            var parts = r.Parts(chart, new RenderContext(doc)).Select(p => p.Label.En).ToList();
            Assert.Contains("y = k·x", parts);
            Assert.Contains(parts, p => p.Contains("Window"));
            // The element changes: the chart (re-rendered from the new document) follows.
            var changed = Edits.Replace(doc, doc.Get("e1")!.WithParam("k", "3"));
            var set2 = type.SetOf(chart, new RenderContext(changed).For(r))!;
            Assert.Equal(9, set2.Point("op")!.Y);
            Assert.NotEqual(Svg(r, doc, chart), Svg(r, changed, chart));
        }

        private static string Svg(Registry r, SketchDocument doc, SketchObject o)
            => string.Join("|", Prim.Flatten(r.Render(o, new RenderContext(doc))).Select(p => p.ToString()));

        [Fact]
        public void DraggingTheOperatingPointWritesTheElementValueInOneDocumentChange()
        {
            var r = Reg();
            var doc = Insert(r, WithElement(r));
            var chart = Chart(doc);
            var ctx = new RenderContext(doc).For(r);
            var handle = Assert.Single(r.DragHandles(chart, ctx), h => h.Key == "op.op");
            Assert.Equal(DragConstraint.Horizontal, handle.Constraint);
            // Find the page position of x = 7 by moving along the handle's direction (axis 0 … 10 over the plot width).
            var type = (LinkedChart)r.Type(chart.Type)!;
            var c = type.Compose(chart, ctx);
            var local = new Vec2(ChartHandles.PX(c.F, 7), 0);
            var page = chart.Matrix().Apply(local);
            var after = DocumentHandles.Move(r, doc, chart.Id, handle.Key, new Vec2(page.X, handle.Position.Y), Lang.En);
            Assert.NotSame(doc, after);
            var t = ValueSolver.Solve(after, r, new[] { "e1" });
            Assert.Equal(7, t.Get("e1", "x")!.Value!.Value, 6);
            Assert.Equal(Provenance.Entered, t.Get("e1", "x")!.Provenance);
            // The chart object itself is unchanged; it follows through the link.
            Assert.Same(chart, after.Get(chart.Id));
            Assert.Equal(7, type.SetOf(chart, new RenderContext(after).For(r))!.Point("op")!.X, 6);
            // The object-level move changes nothing (hosts call DocumentHandles.Move).
            Assert.Same(chart, r.MoveHandle(chart, handle.Key, page, ctx));
        }

        [Fact]
        public void UnlinkFreezesTheCurvesAsAFigure()
        {
            var r = Reg();
            var doc = Insert(r, WithElement(r));
            var chart = Chart(doc);
            var unlink = r.Type(chart.Type)!.Actions(chart, new RenderContext(doc).For(r)).Single(a => a.Id == "chart.unlink");
            Assert.True(unlink.IsEnabled);
            var frozenDoc = RunAction(r, doc, chart.Id, "chart.unlink");
            var frozen = frozenDoc.Get(chart.Id)!;
            Assert.Equal(FigureChart.TypeId, frozen.Type);
            Assert.Equal(chart.Size, frozen.Size);
            Assert.True(FigureSpec.TryParse(frozen.Str("spec", ""), out var spec, out _));
            Assert.Contains(spec!.Series, s => s.Label?.En == "y = k·x" && s.Y.Last() == 20);
            Assert.Contains(spec.Series, s => s.Label?.En == "Operating point" && s.X.Single() == 3 && s.Y.Single() == 6);
            Assert.Contains(spec.Regions, g => g.Kind == "span" && g.From == 2 && g.To == 4);
            Assert.Contains(spec.Refs, x => x.Kind == "v" && x.X == 9);
            // Frozen: a later change of the element no longer reaches the chart.
            var changed = Edits.Replace(frozenDoc, frozenDoc.Get("e1")!.WithParam("k", "5"));
            Assert.Equal(Svg(r, frozenDoc, frozen), Svg(r, changed, frozen));
            Assert.Empty(r.Type(CurveElement.TypeId)!.Dependents(changed.Get("e1")!, new RenderContext(changed)));
        }

        [Fact]
        public void LinkToElementTurnsAPumpChartIntoALinkedChart()
        {
            var r = Reg();
            var doc = WithElement(r);
            var pump = r.Create(r.Item("chart.pump")!, new Vec2(150, 60)) with { Id = "c1" };
            doc = Edits.Add(doc, doc.Pages[0].Id, pump);
            var option = r.Type(pump.Type)!.Actions(pump, new RenderContext(doc).For(r)).Single(a => a.Choice?.Key == "chart.link");
            Assert.Equal("chart.link=e1#line", option.Id);
            var linked = RunAction(r, doc, "c1", option.Id).Get("c1")!;
            Assert.Equal(LinkedChart.TypeId, linked.Type);
            Assert.Equal("e1#line", linked.Str("link", ""));
            // Alone on a slide a chart offers nothing to link to.
            var single = SketchDocument.Create();
            single = Edits.Add(single, single.Pages[0].Id, pump);
            Assert.DoesNotContain(r.Type(pump.Type)!.Actions(pump, new RenderContext(single).For(r)), a => a.Choice?.Key == "chart.link");
        }

        [Fact]
        public void MissingElementShowsAMessageAndCannotBeFrozen()
        {
            var r = Reg();
            var doc = Insert(r, WithElement(r));
            var chart = Chart(doc);
            var gone = Edits.Remove(doc, new[] { "e1" });
            var prims = Prim.Flatten(r.Render(chart, new RenderContext(gone))).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains(prims, t => t.Contains("not found"));
            Assert.False(r.Type(chart.Type)!.Actions(chart, new RenderContext(gone).For(r)).Single(a => a.Id == "chart.unlink").IsEnabled);
            Assert.Empty(r.DragHandles(chart, new RenderContext(gone)));
            // An unreadable link renders too (library audit tries "Test 12").
            Assert.NotEmpty(r.Render(chart.WithParam("link", "Test 12"), new RenderContext(doc)));
        }

        [Fact]
        public void TheLinkSurvivesTheJsonRoundTrip()
        {
            var r = Reg();
            var doc = Insert(r, WithElement(r));
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            var chart = Chart(back);
            Assert.Equal(Chart(doc).Str("link", ""), chart.Str("link", ""));
            var set = ((LinkedChart)r.Type(chart.Type)!).SetOf(chart, new RenderContext(back).For(r))!;
            Assert.Equal(6, set.Point("op")!.Y);
        }

        [Fact]
        public void LinkTextsParseAndCharts()
        {
            Assert.True(Characteristics.TryParseLink("o123#pump", out var id, out var key));
            Assert.Equal(("o123", "pump"), (id, key));
            Assert.False(Characteristics.TryParseLink("o123", out _, out _));
            Assert.False(Characteristics.TryParseLink("#pump", out _, out _));
            Assert.False(Characteristics.TryParseLink(null, out _, out _));
            Assert.Equal("a#b", Characteristics.Link("a", "b"));
        }
    }
}
