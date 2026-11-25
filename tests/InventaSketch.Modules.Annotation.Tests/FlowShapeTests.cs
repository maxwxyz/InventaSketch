using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Method flowchart gaps (2026-11-25): further ISO 5807 shapes, optional steps, parallel mode, decision exits.</summary>
    public class FlowShapeTests
    {
        public static readonly string[] NewShapes =
        {
            "ann.flow.predefined", "ann.flow.offpage", "ann.flow.loop", "ann.flow.preparation", "ann.flow.manual", "ann.flow.database", "ann.flow.delay",
        };

        public static IEnumerable<object[]> Shapes => NewShapes.Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(Shapes))]
        public void TheShapeDrawsInsideItsBoxAndHasFourPorts(string id)
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, id, new Vec2(100, 80));
            var b = Prim.Bounds(r.RenderLocal(o, new RenderContext(doc)))!.Value;
            Assert.True(b.X >= -o.Size.W / 2 - 0.5 && b.Right <= o.Size.W / 2 + 0.5 && b.Y >= -o.Size.H / 2 - 0.5 && b.Bottom <= o.Size.H / 2 + 0.5, $"{id}: {b}");
            var ports = r.Type(id)!.Anchors(o, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToList();
            Assert.Equal(new[] { "top", "right", "bottom", "left" }, ports);
            Assert.Equal("ann.arrow.elbow", r.Type(id)!.ChainConnector(o));
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void LongTextIsFittedIntoTheInnerAreaOfTheSymbol(string id)
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, id, new Vec2(100, 80));
            o = o with { Text = id == "ann.flow.offpage" ? "AB" : "Zielwiderstand aus angeforderter Rekuperationsleistung berechnen" };
            var type = (FlowBoxType)r.Type(id)!;
            var text = type.Render(o, new RenderContext(doc)).OfType<TextPrim>().Single();
            var e = Ann.TextExtent(text.Text, text.TextStyle.Size);
            var c = text.Position;
            Assert.Equal(type.TextCenter(o), c);
            var (aw, ah) = type.TextArea(o);
            Assert.True(e.X <= aw + 1e-6 && e.Y <= ah + 1e-6, $"{id}: text {e} in area {aw} x {ah}");
            // Every corner of the text box lies inside the symbol outline.
            var outline = Ann.Polylines(type.Outline(o)).First();
            foreach (var corner in new[] { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 1), new Vec2(-1, 1) })
            {
                var p = c + new Vec2(corner.X * e.X / 2, corner.Y * e.Y / 2);
                Assert.True(Geo.PointInPolygon(p, outline), $"{id}: corner {p} outside");
            }
            if (id == "ann.flow.predefined")
            {
                // Between the two inner lines.
                var inner = type.Outline(o) is GroupPrim g ? g.Children.OfType<LinePrim>().Select(l => Math.Abs(l.A.X)).Min() : 0;
                Assert.True(e.X / 2 <= inner, "text between the side lines");
            }
        }

        [Fact]
        public void LoopLimitChamfersTheTopForBeginAndTheBottomForEnd()
        {
            var r = T.Reg();
            var (doc, begin) = T.Insert(r, "ann.flow.loop", new Vec2(100, 80));
            var (_, end) = T.Insert(r, "ann.flow.loop.end", new Vec2(100, 80));
            var type = (FlowBoxType)r.Type("ann.flow.loop")!;
            double hw = begin.Size.W / 2, hh = begin.Size.H / 2;
            var pb = Ann.Polylines(type.Outline(begin)).First();
            var pe = Ann.Polylines(type.Outline(end)).First();
            Assert.DoesNotContain(pb, p => p == new Vec2(-hw, -hh));   // top corners cut
            Assert.Contains(pb, p => p == new Vec2(-hw, hh));
            Assert.Contains(pe, p => p == new Vec2(-hw, -hh));         // bottom corners cut
            Assert.DoesNotContain(pe, p => p == new Vec2(-hw, hh));
            Assert.Equal("end", type.Actions(begin, Lang.En).Single(a => a.Id == "ann.flow.loop.edge=end").Apply(begin).Str("edge", ""));
            Assert.NotNull(doc);
        }

        [Fact]
        public void OffPageConnectorIsAUniformPentagonWithALetter()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.flow.offpage", new Vec2(50, 50));
            Assert.True(r.Type(o.Type)!.PreferUniformScale);
            Assert.Equal("A", T.All<TextPrim>(T.Render(r, doc, o.Id)).Single().Text);
            var outline = Ann.Polylines(((FlowBoxType)r.Type(o.Type)!).Outline(o)).First();
            Assert.Equal(6, outline.Count); // five corners, closed
            Assert.Contains(outline, p => p == new Vec2(0, o.Size.H / 2)); // tip down
        }

        [Fact]
        public void AnOptionalStepHasADashedOutline()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.flow.process.optional", new Vec2(50, 50));
            Assert.True(o.Flag("optional", false));
            var outline = T.Render(r, doc, o.Id).First(p => p.Tag == AnnTags.Main);
            Assert.Equal(DashStyle.Dash, outline switch { RectPrim rp => rp.Style.Dash, PathPrim pp => pp.Style.Dash, _ => null });
            foreach (var id in FlowchartIds(r))
            {
                var t = r.Type(id)!;
                var x = r.Create(r.Library.First(i => i.TypeId == id), Vec2.Zero);
                var toggle = t.Actions(x, Lang.De).Single(a => a.Id == "ann.flow.optional");
                Assert.Equal("Optionaler Schritt (gestrichelt)", toggle.Label.De);
                Assert.True(toggle.Apply(x).Flag("optional", false), id);
            }
            // Off by default: solid outline.
            var (doc2, plain) = T.Insert(r, "ann.flow.process", new Vec2(50, 50));
            var solid = T.Render(r, doc2, plain.Id).First(p => p.Tag == AnnTags.Main);
            Assert.NotEqual(DashStyle.Dash, solid switch { RectPrim rp => rp.Style.Dash, PathPrim pp => pp.Style.Dash, _ => null });
        }

        private static IEnumerable<string> FlowchartIds(Registry r)
            => r.Types.OfType<FlowBoxType>().Select(t => t.Id);

        [Fact]
        public void ParallelModeHasEvenlySpacedPortsOnItsLongSides()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.flow.parallel", new Vec2(100, 80));
            var type = r.Type(o.Type)!;
            var ctx = new RenderContext(doc);
            var ports = type.Anchors(o, ctx).ToList();
            Assert.Equal(6, ports.Count); // 3 per side by default
            var top = ports.Where(a => a.Id!.StartsWith("top")).Select(a => a.P.X).OrderBy(x => x).ToList();
            Assert.Equal(new[] { -20.0, 0, 20 }, top.Select(x => Math.Round(x, 6)));
            Assert.All(ports.Where(a => a.Id!.StartsWith("bottom")), a => Assert.Equal(o.Size.H / 2, a.P.Y, 6));
            // Two to six ports; upright it puts them on the left and right.
            var five = o.WithParam("ports", 5);
            Assert.Equal(10, type.Anchors(five, ctx).Count());
            var upright = o with { Size = new Size2(3, 60) };
            Assert.All(type.Anchors(upright, ctx), a => Assert.True(a.Id!.StartsWith("left") || a.Id.StartsWith("right")));
            // Bar: one filled rectangle; ISO style: two parallel lines.
            var bar = type.Render(o, ctx).Single();
            Assert.Equal(FillKind.Solid, ((RectPrim)bar).Style.Fill!.Kind);
            Assert.Equal(2, type.Render(o.WithParam("style", "doubleline"), ctx).OfType<LinePrim>().Count());
            Assert.Equal("ann.arrow.elbow", type.ChainConnector(o));
            // Quick add below the bar joins a new bar with an elbow connector.
            var res = Flow.QuickAdd(r, doc, o.Id, FlowSide.Bottom);
            Assert.NotNull(res.ConnectorId);
            Assert.Equal("ann.arrow.elbow", res.Doc.Get(res.ConnectorId!)!.Type);
        }

        [Fact]
        public void DecisionExitsAddedWithPlusAreLabelledYesThenNoInTheDrawingLanguage()
        {
            var r = T.Reg();
            var (doc, d) = T.Insert(r, "ann.flow.decision", new Vec2(100, 60));
            var first = Flow.QuickAdd(r, doc, d.Id, FlowSide.Bottom);
            var second = Flow.QuickAdd(r, first.Doc, d.Id, FlowSide.Right);
            var third = Flow.QuickAdd(r, second.Doc, d.Id, FlowSide.Left);
            var c1 = third.Doc.Get(first.ConnectorId!)!;
            var c2 = third.Doc.Get(second.ConnectorId!)!;
            var c3 = third.Doc.Get(third.ConnectorId!)!;
            Assert.Equal("yes", c1.Str("exit", ""));
            Assert.Equal("no", c2.Str("exit", ""));
            Assert.Equal("", c3.Str("exit", ""));
            string Label(SketchObject c, Lang lang) => T.All<TextPrim>(r.Render(c, new RenderContext(third.Doc, lang))).Select(t => t.Text).SingleOrDefault() ?? "";
            Assert.Equal("ja", Label(c1, Lang.De));
            Assert.Equal("nein", Label(c2, Lang.De));
            Assert.Equal("yes", Label(c1, Lang.En));
            Assert.Equal("no", Label(c2, Lang.En));
            Assert.Equal("", Label(c3, Lang.De));
            // An edited text wins over the exit label.
            Assert.Equal("OK", Label(c1 with { Text = "OK" }, Lang.De));
            // Steps chain without labels.
            var (doc2, step) = T.Insert(r, "ann.flow.process", new Vec2(100, 60));
            var s1 = Flow.QuickAdd(r, doc2, step.Id, FlowSide.Bottom);
            Assert.Equal("", s1.Doc.Get(s1.ConnectorId!)!.Str("exit", ""));
        }

        [Fact]
        public void TheCompareDecisionLabelsItsThreeExits()
        {
            var r = T.Reg();
            var (doc, d) = T.Insert(r, "ann.flow.decision.compare", new Vec2(100, 60));
            Assert.Equal(new[] { "<", "=", ">" }, FlowBoxType.ExitLabels(d));
            var labels = new List<string>();
            foreach (var side in new[] { FlowSide.Left, FlowSide.Bottom, FlowSide.Right })
            {
                var res = Flow.QuickAdd(r, doc, d.Id, side);
                doc = res.Doc;
                labels.Add(T.All<TextPrim>(T.Render(r, doc, res.ConnectorId!)).Single().Text);
            }
            Assert.Equal(new[] { "<", "=", ">" }, labels);
        }

        [Fact]
        public void NewFlowParametersSurviveTheJsonRoundTrip()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            foreach (var (item, key, value) in new (string, string, ParamValue)[]
            {
                ("ann.flow.process.optional", "optional", true), ("ann.flow.loop.end", "edge", "end"), ("ann.flow.decision.compare", "exitLabels", "<|=|>"),
                ("ann.flow.parallel", "ports", 4), ("ann.arrow.elbow.no", "exit", "no"),
            })
            {
                var o = r.Create(r.Item(item)!, new Vec2(50, 50)).WithParam(key, value);
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
            }
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Contains(back.Objects.Values, o => o.Type == "ann.flow.parallel" && o.Int("ports", 0) == 4);
            Assert.Contains(back.Objects.Values, o => o.Type == "ann.flow.loop" && o.Str("edge", "") == "end");
        }
    }
}
