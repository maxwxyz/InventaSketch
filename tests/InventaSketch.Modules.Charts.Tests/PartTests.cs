using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Charts v2 stage 1: every part of a chart can be listed, hit, hidden and styled on its own.</summary>
    public class PartTests
    {
        private static IReadOnlyList<Primitive> Drawn(Registry r, SketchObject o) => Prim.Flatten(r.RenderLocal(o, Fx.Ctx())).ToList();

        [Fact]
        public void XyChartListsEachCurveAxisAndGridAsAPart()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("data", "0;1\n1;2\n\n0;2\n1;3").WithParam("style", "linepoints");
            var parts = r.Parts(o, Fx.Ctx());
            var keys = parts.Select(p => p.Key).ToList();
            foreach (var k in new[] { "series.0", "series.1", "marker.0", "marker.1", "axis.x", "axis.y", "label.x", "label.y", "tick.x", "tick.y", "grid.major", "legend" })
                Assert.Contains(k, keys);
            Assert.Equal("series.0", keys[0]);
            Assert.Equal("Series 1", parts.Single(p => p.Key == "series.0").Label.En);
            Assert.Equal("Achse x", parts.Single(p => p.Key == "axis.x").Label.De);
            Assert.All(parts, p => Assert.False(p.Hidden));
        }

        [Fact]
        public void EveryPartOfEveryChartCanBeHiddenAndStyled()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = Fx.Item(r, item.Id);
                var parts = r.Parts(o, Fx.Ctx());
                Assert.NotEmpty(parts);
                foreach (var part in parts)
                {
                    var hidden = PartLooks.WithLook(o, part.Key, p => p with { Hidden = true });
                    Assert.DoesNotContain(Drawn(r, hidden), p => p.Tag is { } t && Under(t, part.Key));
                    Assert.True(r.Parts(hidden, Fx.Ctx()).Single(p => p.Key == part.Key).Hidden, item.Id + " " + part.Key);

                    var red = PartLooks.WithLook(o, part.Key, p => p with { Style = new Style { Stroke = "#ff0000" } });
                    var mine = Drawn(r, red).Where(p => p.Tag is { } t && Under(t, part.Key) && p is not TextPrim).ToList();
                    Assert.All(mine, p => Assert.Equal("#ff0000", Stroke(p)));
                    // Everything else keeps its look.
                    Assert.Equal(Drawn(r, o).Count(p => p.Tag is { } t && !Under(t, part.Key)), Drawn(r, red).Count(p => p.Tag is { } t && !Under(t, part.Key)));
                }
            }
        }

        private static bool Under(string tag, string key) => key == ChartTags.Legend ? ChartTags.Is(tag, key) : tag == key || tag.StartsWith(key + ".");

        private static string? Stroke(Primitive p) => p switch
        {
            LinePrim l => l.Style.Stroke, PathPrim a => a.Style.Stroke, RectPrim b => b.Style.Stroke, EllipsePrim e => e.Style.Stroke, _ => null,
        };

        [Fact]
        public void KindLookAppliesToAllAndTheIndexedKeyWins()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("data", "0;1\n1;2\n\n0;2\n1;3");
            o = PartLooks.WithLook(o, "series", p => p with { Style = new Style { StrokeWidth = 1.2 } });
            o = PartLooks.WithLook(o, "series.1", p => p with { Style = new Style { StrokeWidth = 0.2 } });
            var lines = Drawn(r, o).OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Series)).ToDictionary(p => p.Tag!);
            Assert.Equal(1.2, lines["series.0"].Style.StrokeWidth);
            Assert.Equal(0.2, lines["series.1"].Style.StrokeWidth);
        }

        [Fact]
        public void PartLooksSurviveSaveAndLoad()
        {
            var r = Fx.Reg();
            var o = PartLooks.WithLook(Fx.Item(r, "chart.line"), "grid", p => p with { Hidden = true });
            o = PartLooks.WithLook(o, "series.0", p => p with { Style = new Style { Stroke = "#00aa00", Dash = DashStyle.Dash } });
            var doc0 = SketchDocument.Create(); var doc = Edits.Add(doc0, doc0.Pages[0].Id, o);
            var back = DocumentJson.FromCompact(DocumentJson.ToCompact(doc)).Get(o.Id)!;
            Assert.True(back.Parts!["grid"].Hidden);
            Assert.Equal("#00aa00", back.Parts["series.0"].Style!.Stroke);
            Assert.Equal(DashStyle.Dash, back.Parts["series.0"].Style!.Dash);
            Assert.DoesNotContain(Drawn(r, back), p => ChartTags.Is(p.Tag, "grid"));
            // Resetting the last look removes the dictionary again.
            var plain = PartLooks.WithLook(PartLooks.WithLook(back, "grid", _ => new PartLook()), "series.0", _ => new PartLook());
            Assert.Null(plain.Parts);
        }

        [Fact]
        public void PartAtFindsTheCurveUnderThePointer()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("data", "0;0\n10;10").WithParam("grid", "false");
            var line = Drawn(r, o).OfType<PathPrim>().Single(p => p.Tag == "series.0");
            var pts = PartLooks.Sample(line.Segments);
            var mid = (pts[0] + pts[pts.Count - 1]) * 0.5;
            Assert.Equal("series.0", r.PartAt(o, mid, Fx.Ctx(), 0.5));
            var label = Drawn(r, o).OfType<TextPrim>().First(t => t.Tag == "label.x");
            Assert.Equal("label.x", r.PartAt(o, label.Position, Fx.Ctx(), 0.5));
        }

        [Fact]
        public void PartActionsOfferTypeActionsThenGenericOnes()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line");
            var ids = r.PartActions(o, "series.0", Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Contains("chart.smooth", ids);
            Assert.True(ids.IndexOf("chart.smooth") < ids.IndexOf("part.hide"));
            Assert.Contains("part.thicker", ids);
            Assert.DoesNotContain("part.reset", ids);
            Assert.Contains("chart.xscale=log", r.PartActions(o, "axis.x", Fx.Ctx()).Select(a => a.Id));
            Assert.Contains("chart.grid", r.PartActions(o, "grid.major", Fx.Ctx()).Select(a => a.Id));

            var thicker = r.PartActions(o, "series.0", Fx.Ctx()).Single(a => a.Id == "part.thicker").Apply(o);
            var before = Drawn(r, o).OfType<PathPrim>().Single(p => p.Tag == "series.0").Style.EffectiveWidth;
            var after = Drawn(r, thicker).OfType<PathPrim>().Single(p => p.Tag == "series.0").Style.EffectiveWidth;
            Assert.True(after > before);
            Assert.Contains("part.reset", r.PartActions(thicker, "series.0", Fx.Ctx()).Select(a => a.Id));
            var hidden = r.PartActions(o, "series.0", Fx.Ctx()).Single(a => a.Id == "part.hide").Apply(o);
            Assert.Equal("Show", r.PartActions(hidden, "series.0", Fx.Ctx()).Single(a => a.Id == "part.hide").Label.En);
        }
    }
}
