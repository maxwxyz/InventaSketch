using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Calc 5 accept items with the full module catalogue (docs/en/design/engineering-calculations.md section 7): changing
    /// the pump's H₀ redraws the linked chart and moves the operating point; dragging the point changes the pump flow in one
    /// document change; unlinking freezes the values; the PV chart shows the MPP and the inverter window.
    /// </summary>
    public class LinkedChartAcceptTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Linked");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Make(string item, string id, double x = 60, double y = 60) => Reg.Create(Reg.Item(item)!, new Vec2(x, y)) with { Id = id };

        /// <summary>A pump (H₀ 40 m, V̇_max 100 m³/h, H_stat 10 m) at 70 m³/h with a linked chart inserted from its menu.</summary>
        private static (SketchDocument Doc, string Chart) PumpWithChart()
        {
            var pump = Make("process.pump.centrifugal", "p").WithParam("curveH0", "40 m").WithParam("curveQmax", "100 m³/h").WithParam("curveHstat", "10 m").WithParam("flowRate", "70 m³/h");
            var doc = Doc(pump);
            var menu = Reg.Type(pump.Type)!.Actions(pump, new RenderContext(doc).For(Reg)).Where(a => a.Choice?.Key == Characteristics.InsertChoice).ToList();
            Assert.Equal("values.chart=pump", Assert.Single(menu).Id);
            var after = RunAction(doc, "p", "values.chart=pump");
            var chart = after.Objects.Values.Single(o => o.Type == Characteristics.ChartTypeId);
            return (after, chart.Id);
        }

        /// <summary>Runs an action like a host with the registry in the context (the static chart types are shared by every registry of the test run, so their Owner is not this one).</summary>
        private static SketchDocument RunAction(SketchDocument doc, string id, string actionId)
        {
            var o = doc.Get(id)!;
            var ctx = new RenderContext(doc, Lang.En).For(Reg);
            return Reg.Type(o.Type)!.Actions(o, ctx).Single(a => a.Id == actionId).Run(doc, o, ctx);
        }

        private static CharacteristicSet Set(SketchDocument doc, string chartId) => Characteristics.Resolve(Reg, doc, Characteristics.LinkOf(doc.Get(chartId)!))!;

        private static string Drawing(SketchDocument doc, string id) => string.Join("|", Prim.Flatten(Reg.Render(doc.Get(id)!, new RenderContext(doc))).Select(p => p.ToString()));

        [Fact]
        public void ChangingThePumpsShutOffHeadRedrawsTheChartAndMovesTheOperatingPoint()
        {
            var (doc, chart) = PumpWithChart();
            Assert.Equal(20.4, Set(doc, chart).Point("op")!.Y, 9);
            var after = Edits.Replace(doc, doc.Get("p")!.WithParam("curveH0", "50 m"));
            var change = ChangeSet.Compute(Reg, doc, after, Lang.En);
            Assert.Equal(new[] { "p" }, change.Changed.ToArray());
            Assert.Contains(chart, change.Dependents);
            var op = Set(after, chart).Point("op")!;
            Assert.Equal(70, op.X, 9);
            Assert.Equal(25.5, op.Y, 9);
            Assert.NotEqual(Drawing(doc, chart), Drawing(after, chart));
        }

        [Fact]
        public void DraggingTheOperatingPointChangesThePumpFlowInOneDocumentChange()
        {
            var (doc, chart) = PumpWithChart();
            var ctx = new RenderContext(doc).For(Reg);
            var handle = Assert.Single(Reg.DragHandles(doc.Get(chart)!, ctx), h => h.Key == "op.op");
            // The pump curve's 21st point is at 50 m³/h (41 points from 0 to 100 m³/h): drop the grip above it.
            var port = Reg.PageAnchors(doc.Get(chart)!, ctx).Single(a => a.Id == "s1.21");
            var after = DocumentHandles.Move(Reg, doc, chart, handle.Key, new Vec2(port.P.X, handle.Position.Y), Lang.En);
            var change = ChangeSet.Compute(Reg, doc, after, Lang.En);
            // One document change touches the pump; the chart follows as its dependent (hosts redraw both in one undo step).
            Assert.Equal(new[] { "p" }, change.Changed.ToArray());
            Assert.Contains(chart, change.Dependents);
            var t = ValueSolver.Solve(after, Reg, new[] { "p" });
            Assert.Equal(50 / 3600.0, t.Get("p", "Vdot")!.Value!.Value, 9);
            Assert.Equal(30, t.Get("p", "H")!.Value!.Value, 9);
            Assert.Equal(50, Set(after, chart).Point("op")!.X, 9);
        }

        [Fact]
        public void UnlinkingFreezesTheValues()
        {
            var (doc, chart) = PumpWithChart();
            var frozen = RunAction(doc, chart, "chart.unlink");
            Assert.Equal("chart.figure", frozen.Get(chart)!.Type);
            var drawing = Drawing(frozen, chart);
            var changed = Edits.Replace(frozen, frozen.Get("p")!.WithParam("curveH0", "50 m"));
            Assert.Equal(drawing, Drawing(changed, chart));
            Assert.DoesNotContain(chart, ChangeSet.Compute(Reg, frozen, changed, Lang.En).Dependents);
            // The frozen figure still shows the operating point at 70 m³/h and 20.4 m as data.
            Assert.Contains("\"x\":[70]", frozen.Get(chart)!.Str("spec", "").Replace(" ", ""));
        }

        [Fact]
        public void PvChartShowsTheMppAndTheInverterWindow()
        {
            var str = Make("energy.pv.string", "s").WithParam("modules", 10).WithParam("voc", "49,5 V").WithParam("vmp", "41,6 V").WithParam("isc", "13,9 A").WithParam("imp", "13,0 A");
            var inv = Make("energy.conv.inverter", "inv", 160).WithParam("mpptMin", "200 V").WithParam("mpptMax", "480 V").WithParam("udcMax", "600 V");
            var cable = Reg.Create(Reg.Item("energy.line.cable")!, Vec2.Zero) with
            {
                Id = "c1",
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment("s", 0, 0) { AnchorId = "dc" }, new Attachment("inv", 0, 0) { AnchorId = "dc" } },
            };
            var doc = RunAction(Doc(str, inv, cable), "s", "values.chart=pv");
            var chart = doc.Objects.Values.Single(o => o.Type == Characteristics.ChartTypeId);
            var parts = Reg.Parts(chart, new RenderContext(doc)).Select(p => p.Label.En).ToList();
            Assert.Contains(parts, p => p.Contains("Inverter MPP window"));
            Assert.Contains(parts, p => p.Contains("MPP"));
            Assert.Contains(parts, p => p.Contains("U_DC,max"));
            var set = Set(doc, chart.Id);
            Assert.InRange(set.Point("mpp.p")!.X, 200, 480);
        }

        [Fact]
        public void InsertedChartsSurviveTheCompactRoundTrip()
        {
            var (doc, chart) = PumpWithChart();
            var back = DocumentJson.FromCompact(DocumentJson.ToCompact(doc));
            Assert.Equal(doc.Get(chart)!.Str("link", ""), back.Get(chart)!.Str("link", ""));
            Assert.Equal(20.4, Set(back, chart).Point("op")!.Y, 9);
        }
    }
}
