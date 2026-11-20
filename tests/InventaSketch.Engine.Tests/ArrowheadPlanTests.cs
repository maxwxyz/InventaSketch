using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Draw-plan lowering of hollow UML heads, crow's-foot ends and underlined text.</summary>
    public class ArrowheadPlanTests
    {
        private const double Pt = 72.0 / 25.4;

        /// <summary>Plan-test module: a two-point line, an open curve and an underlined label.</summary>
        private sealed class PlanModule : IModule
        {
            public string Id => "ptest";
            public I18nText Name => new("Plan test", "Plan-Test");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[] { new Line(), new Curve(), new Label() };
            public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("ptest", new I18nText("Plan test", "Plan-Test"), Id, 99) };
            public IEnumerable<LibraryItem> Library => new[]
            {
                new LibraryItem("ptest.line", "ptest.line", new I18nText("Line", "Linie"), "ptest"),
                new LibraryItem("ptest.curve", "ptest.curve", new I18nText("Curve", "Kurve"), "ptest"),
                new LibraryItem("ptest.label", "ptest.label", new I18nText("Label", "Beschriftung"), "ptest"),
            };

            private sealed class Line : ObjectType
            {
                public override string Id => "ptest.line";
                public override I18nText Name => new("Line", "Linie");
                public override GeometryKind Geometry => GeometryKind.Points;
                public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
                public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                {
                    var p = ctx.Doc.ResolvePoints(o);
                    return new Primitive[] { Prim.Line(p[0], p[p.Count - 1], (Style.Visible with { StrokeWidth = 0.3 }).Merge(o.Style)) };
                }
            }

            private sealed class Curve : ObjectType
            {
                public override string Id => "ptest.curve";
                public override I18nText Name => new("Curve", "Kurve");
                public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                {
                    double w = o.Size.W / 2, h = o.Size.H / 2;
                    return new Primitive[] { Prim.Path(new[] { PathSeg.Move(new Vec2(-w, h)), PathSeg.Cubic(new Vec2(-w, -h), new Vec2(w, -h), new Vec2(w, h)) }, Style.Visible.Merge(o.Style)) };
                }
            }

            private sealed class Label : ObjectType
            {
                public override string Id => "ptest.label";
                public override I18nText Name => new("Label", "Beschriftung");
                public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                    => new Primitive[] { Prim.Text(0, 0, "key", new TextStyle { Underline = true }), Prim.Text(0, 5, "plain") };
            }
        }

        private static readonly string[] NativeNames = { "triangle", "open", "stealth", "diamond", "oval", "tick", "half" };

        private static (EditorSession Session, string Id) One(string item, Style? style = null, double x = 100, double y = 50)
        {
            var registry = ModuleCatalog.Create(new IModule[] { new PlanModule() });
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var o = registry.Create(registry.Item(item)!, new Vec2(x, y));
            if (style is not null) o = o with { Style = style };
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return (new EditorSession("t", registry, doc, Lang.En), o.Id);
        }

        private static PlanUnit Unit(string item, Style? style, DrawCapabilities caps)
        {
            var (s, id) = One(item, style);
            return DrawPlanBuilder.Build(s, new[] { id }, caps).Units.Single();
        }

        private static IEnumerable<PlanOp> Flat(IEnumerable<PlanOp> ops) => ops.SelectMany(o => o.Op == "group" ? Flat(o.Children ?? new List<PlanOp>()) : new[] { o });

        private static Style Ends(ArrowHead? start, ArrowHead? end) => new() { ArrowStart = start, ArrowEnd = end };

        public static IEnumerable<object[]> AllHeads() => Enum.GetValues(typeof(ArrowHead)).Cast<ArrowHead>().Where(h => h != ArrowHead.None).Select(h => new object[] { h });

        [Theory]
        [MemberData(nameof(AllHeads))]
        public void OfficeJsNeverGetsNativeArrowheadsOrFreeforms(ArrowHead head)
        {
            var unit = Unit("ptest.line", Ends(head, head), DrawCapabilities.OfficeJs);
            Assert.Equal("native", unit.Mode);
            var ops = Flat(unit.Ops).ToList();
            Assert.All(ops, o => Assert.True(o.Stroke?.ArrowStart is null && o.Stroke?.ArrowEnd is null, o.Op));
            Assert.All(ops, o => Assert.Contains(o.Op, new[] { "line", "rect", "ellipse", "shape" }));
            Assert.Contains(ops, o => o.Tag == "arrowEnd");
            Assert.Contains(ops, o => o.Tag == "arrowStart");
        }

        [Theory]
        [MemberData(nameof(AllHeads))]
        public void FullHostGetsNativeLineEndsOnlyWherePowerPointHasThem(ArrowHead head)
        {
            var unit = Unit("ptest.line", Ends(null, head), DrawCapabilities.Full);
            Assert.Equal("native", unit.Mode);
            var ops = Flat(unit.Ops).ToList();
            var line = ops.Single(o => o.Op == "line" && o.Tag is null);
            // Diamonds are 1.5x longer than the other heads, so at the default weight the native line end is too small.
            if (DrawPlanBuilder.HasNativeLineEnd(head) && head != ArrowHead.Diamond)
            {
                Assert.Contains(line.Stroke!.ArrowEnd, NativeNames);
                Assert.Equal(115 * Pt, line.X2!.Value, 2);
                Assert.DoesNotContain(ops, o => o.Tag == "arrowEnd");
            }
            else
            {
                Assert.Null(line.Stroke!.ArrowEnd);
                Assert.Null(line.Stroke.ArrowSize);
                Assert.Contains(ops, o => o.Tag == "arrowEnd");
                Assert.Contains(unit.Notes, n => n.Contains("drawn as shapes", StringComparison.Ordinal));
                Assert.DoesNotContain(ops, o => o.Op == "shape"); // freeform host: exact geometry
            }
        }

        [Fact]
        public void NativeLineEndsAreUsedOnlyWhereTheyReachTheHeadSize()
        {
            // 0.5 mm line: native medium/long heads are as long as the engine's heads.
            Assert.True(DrawPlanBuilder.NativeHeadFits(ArrowHead.Triangle, ArrowSize.Medium, 0.5));
            Assert.True(DrawPlanBuilder.NativeHeadFits(ArrowHead.Triangle, ArrowSize.Large, 0.5));
            // Thin diagram lines keep a 1.6 mm minimum head: native heads would be much smaller.
            Assert.False(DrawPlanBuilder.NativeHeadFits(ArrowHead.Triangle, ArrowSize.Large, 0.3));
            Assert.False(DrawPlanBuilder.NativeHeadFits(ArrowHead.Diamond, ArrowSize.Large, 0.3));
            // Thick lines: native everywhere, diamonds too.
            Assert.True(DrawPlanBuilder.NativeHeadFits(ArrowHead.Diamond, ArrowSize.Medium, 1.0));
        }

        [Fact]
        public void HollowHeadsLowerToUnfilledExactPolygonsOnFreeformHosts()
        {
            var unit = Unit("ptest.line", Ends(ArrowHead.Triangle, ArrowHead.TriangleOpen), DrawCapabilities.Full);
            var ops = Flat(unit.Ops).ToList();
            var line = ops.Single(o => o.Op == "line");
            Assert.Equal("triangle", line.Stroke!.ArrowStart); // native end and lowered end on one line
            Assert.Null(line.Stroke.ArrowEnd);
            Assert.Equal("medium", line.Stroke.ArrowSize);
            var head = ops.Single(o => o.Tag == "arrowEnd");
            Assert.Equal("polyline", head.Op);
            Assert.True(head.Closed);
            Assert.Equal("none", head.Fill!.Kind);
            Assert.Equal(3, head.Points!.Count);
            var l = ArrowHeads.Length(ArrowSize.Medium, 0.3);
            Assert.Equal((115 - l) * Pt, line.X2!.Value, 2); // line stops at the base
            Assert.Contains(head.Points, p => Math.Abs(p[0] - 115 * Pt) < 0.01 && Math.Abs(p[1] - 50 * Pt) < 0.01);
        }

        [Theory]
        [InlineData(ArrowHead.TriangleOpen, "triangle")]
        [InlineData(ArrowHead.DiamondOpen, "diamond")]
        public void HollowHeadsBecomeUnfilledRotatedShapesInOfficeJs(ArrowHead head, string shape)
        {
            var ops = Flat(Unit("ptest.line", Ends(null, head), DrawCapabilities.OfficeJs).Ops).ToList();
            var op = ops.Single(o => o.Tag == "arrowEnd");
            Assert.Equal("shape", op.Op);
            Assert.Equal(shape, op.Shape);
            Assert.Equal("none", op.Fill!.Kind);
            Assert.Equal(90, op.Rotation!.Value, 6); // apex points right, along the line
            var l = ArrowHeads.Extent(head, ArrowSize.Medium, 0.3);
            Assert.Equal(l * Pt, op.H!.Value, 2);
            Assert.Equal(115 * Pt, op.X!.Value + op.W!.Value / 2 + op.H!.Value / 2, 2); // box centre l/2 before the tip
            Assert.Equal((115 - l) * Pt, ops.Single(o => o.Op == "line" && o.Tag is null).X2!.Value, 2);
        }

        [Fact]
        public void CrowsFootEndsBecomeLinesAndUnfilledEllipses()
        {
            foreach (var caps in new[] { DrawCapabilities.OfficeJs, DrawCapabilities.Full })
            {
                var ops = Flat(Unit("ptest.line", Ends(ArrowHead.CrowOneOnly, ArrowHead.CrowZeroMany), caps).Ops).ToList();
                var end = ops.Where(o => o.Tag == "arrowEnd").ToList();
                var circle = Assert.Single(end, o => o.Op == "ellipse");
                Assert.Equal("none", circle.Fill!.Kind);
                Assert.Contains(end, o => o.Op == (caps.Freeform ? "polyline" : "line")); // the foot
                Assert.Equal(2, ops.Count(o => o.Tag == "arrowStart" && o.Op == "line")); // two bars
                var line = ops.Single(o => o.Op == "line" && o.Tag is null);
                Assert.Equal(85 * Pt, line.X1!.Value, 2); // bars sit on the line, which reaches the entity
                Assert.True(line.X2!.Value <= circle.X!.Value + 0.01); // the line stops behind the circle
                Assert.Null(line.Stroke!.ArrowStart);
                Assert.Null(line.Stroke.ArrowEnd);
            }
        }

        [Fact]
        public void CurvedPathKeepsNativeEndAndLowersHollowEnd()
        {
            var unit = Unit("ptest.curve", Ends(ArrowHead.Stealth, ArrowHead.DiamondOpen), DrawCapabilities.Full);
            var ops = Flat(unit.Ops).ToList();
            var ff = ops.Single(o => o.Op == "freeform");
            Assert.Equal("stealth", ff.Stroke!.ArrowStart);
            Assert.Null(ff.Stroke.ArrowEnd);
            var head = ops.Single(o => o.Tag == "arrowEnd");
            Assert.Equal("polyline", head.Op);
            Assert.Equal(4, head.Points!.Count);
            Assert.Equal("none", head.Fill!.Kind);

            // Office.js: curves still need a picture, which draws the heads itself.
            var web = Unit("ptest.curve", Ends(ArrowHead.Stealth, ArrowHead.DiamondOpen), DrawCapabilities.OfficeJs);
            Assert.Equal("svg", web.Mode);
            Assert.Contains("fill=\"none\"", web.Ops.Single().Svg);
        }

        [Fact]
        public void UnderlinedTextIsANativeFontAttribute()
        {
            foreach (var caps in new[] { DrawCapabilities.OfficeJs, DrawCapabilities.Full })
            {
                var unit = Unit("ptest.label", null, caps);
                var texts = Flat(unit.Ops).Where(o => o.Op == "text").ToList();
                Assert.Equal(2, texts.Count);
                Assert.True(texts.Single(t => t.Text == "key").Underline);
                Assert.Null(texts.Single(t => t.Text == "plain").Underline);
                Assert.DoesNotContain(Flat(unit.Ops), o => o.Op == "line"); // no drawn underline
            }
            var json = JsonSerializer.Serialize(Unit("ptest.label", null, DrawCapabilities.OfficeJs), DocumentJson.Options);
            Assert.Contains("\"underline\":true", json);
        }
    }
}
