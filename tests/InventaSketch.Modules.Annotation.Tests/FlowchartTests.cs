using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Flowcharts as in method figures of applications (maintainer example 2026-11-25): text fitting, symbols, routing, labels.</summary>
    public class FlowchartTests
    {
        [Fact]
        public void LongStepTextIsWrappedAndStaysInsideItsBox()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.flow.process", new Vec2(100, 100));
            doc = Edits.Update(doc, new[] { o.Id }, x => x with { Text = "Zielwiderstand aus angeforderter Rekuperationsleistung und Spannungsgrenze berechnen" });
            var text = T.All<TextPrim>(T.Render(r, doc, o.Id)).Single();
            Assert.Contains("\n", text.Text);
            var box = doc.Get(o.Id)!.Bounds();
            var extent = Ann.TextExtent(text.Text, text.TextStyle.Size);
            Assert.True(extent.X <= box.W + 1e-6, $"width {extent.X} in {box.W}");
            Assert.True(extent.Y <= box.H + 1e-6, $"height {extent.Y} in {box.H}");
            // Explicit line breaks stay; a word longer than a line is split with a hyphen.
            Assert.Equal("a\nb", Ann.Wrap("a\nb", 20));
            var split = Ann.Wrap("Rekuperationsleistung", 15).Split('\n');
            Assert.True(split.Length == 2 && split.All(l => l.Length <= 15) && split[0].EndsWith("-"));
            Assert.Equal("Rekuperationsleistung", split[0].TrimEnd('-') + split[1]);
            Assert.Equal("kurz", Ann.Fit("kurz", 5, 50, 18).Text);
        }

        [Fact]
        public void StartCircleEndSymbolAndYesNoConnectorsAreInTheLibrary()
        {
            var r = T.Reg();
            var (doc, start) = T.Insert(r, "ann.flow.start", new Vec2(50, 30));
            Assert.Equal("Start", T.All<TextPrim>(T.Render(r, doc, start.Id)).Single().Text);
            var (doc2, end) = T.Insert(r, "ann.flow.end", new Vec2(50, 30));
            var circles = T.All<EllipsePrim>(T.Render(r, doc2, end.Id)).ToList();
            Assert.Equal(2, circles.Count);
            Assert.Empty(T.All<TextPrim>(T.Render(r, doc2, end.Id)));
            // Exit labels are stored language-neutral and written in the language of the drawing.
            Assert.Equal("yes", r.Item("ann.arrow.elbow.yes")!.Params!["exit"].Text);
            Assert.Equal("no", r.Item("ann.arrow.elbow.no")!.Params!["exit"].Text);
        }

        [Fact]
        public void SameSideExitsRunAroundWithClearance()
        {
            // "yes" out of the right corner of a decision into the right side of a step further down.
            var route = Annotation.ElbowArrowType.RouteSides(new Vec2(125, 50), new Vec2(1, 0), new Vec2(120, 200), new Vec2(1, 0), 8);
            Assert.Equal(new[] { new Vec2(125, 50), new Vec2(133, 50), new Vec2(133, 200), new Vec2(120, 200) }, route);
            // Loop back: out of a lower step on the left, into an earlier step on the left.
            var loop = Annotation.ElbowArrowType.RouteSides(new Vec2(60, 220), new Vec2(-1, 0), new Vec2(75, 90), new Vec2(-1, 0), 8);
            Assert.Equal(new[] { new Vec2(60, 220), new Vec2(52, 220), new Vec2(52, 90), new Vec2(75, 90) }, loop);
            // Straight down, bottom to top: one run.
            Assert.Equal(2, Annotation.ElbowArrowType.RouteSides(new Vec2(100, 60), new Vec2(0, 1), new Vec2(100, 80), new Vec2(0, -1), 8).Count);
            // Branch: out of the side, into the top of a step below and beside it (one bend).
            var branch = Annotation.ElbowArrowType.RouteSides(new Vec2(125, 50), new Vec2(1, 0), new Vec2(160, 90), new Vec2(0, -1), 8);
            Assert.Equal(new[] { new Vec2(125, 50), new Vec2(160, 50), new Vec2(160, 90) }, branch);
        }

        [Fact]
        public void ADockedElbowConnectorUsesTheSidesAndShowsItsLabel()
        {
            var r = T.Reg();
            var (doc, a) = T.Insert(r, "ann.flow.decision", new Vec2(100, 50));
            var (doc2, b) = T.Insert(r, "ann.flow.process", new Vec2(100, 200), doc);
            var (doc3, c) = T.Insert(r, "ann.arrow.elbow.yes", new Vec2(0, 0), doc2);
            doc3 = Edits.Update(doc3, new[] { c.Id }, x => x with
            {
                Points = new List<Vec2> { a.BoxPoint(0.5, 0), b.BoxPoint(0.5, 0) },
                Attach = new[] { new Attachment(a.Id, 0.5, 0) { AnchorId = "right" }, new Attachment(b.Id, 0.5, 0) { AnchorId = "right" } },
            });
            var prims = r.Render(doc3.Get(c.Id)!, new RenderContext(doc3, Lang.De));
            var line = T.All<PathPrim>(prims).Single(p => p.Tag == AnnTags.Main);
            var xs = PartLooks.Sample(line.Segments).Select(p => p.X).ToList();
            Assert.True(xs.Max() >= a.Bounds().Right + 7.9, "runs to the right of both boxes");
            Assert.Equal("ja", T.All<TextPrim>(prims).Single().Text);
            Assert.Equal("yes", T.All<TextPrim>(T.Render(r, doc3, c.Id)).Single().Text);
        }

        [Fact]
        public void ADashedNoteFrameTakesTheStyle()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.label", new Vec2(50, 50));
            doc = Edits.Update(doc, new[] { o.Id }, x => x with { Style = new Style { Dash = DashStyle.Dash } });
            var frame = Prim.Flatten(T.Render(r, doc, o.Id)).First(p => p is not TextPrim);
            var style = frame switch { RectPrim rp => rp.Style, PathPrim pp => pp.Style, EllipsePrim ep => ep.Style, _ => new Style() };
            Assert.Equal(DashStyle.Dash, style.Dash);
        }
    }
}
