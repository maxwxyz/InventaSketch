using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Connectors and marks for patent figures (2026-11-25): hops, junctions, block/double-line/pair/heat arrows, bus marks, table, view marks.</summary>
    public class ConnectorMarkTests
    {
        private static (SketchDocument Doc, SketchObject Obj) Line(Registry r, string item, SketchDocument? doc, params Vec2[] pts)
        {
            var (d, o) = T.Insert(r, item, pts[0], doc);
            o = o with { Points = pts.ToList() };
            return (Edits.Replace(d, o), o);
        }

        private static List<Vec2> Sample(PathPrim p) => PartLooks.Sample(p.Segments).ToList();

        [Fact]
        public void ALaterConnectorHopsOverAnEarlierOneAndPlainPolylinesStayPlain()
        {
            var r = T.Reg();
            var (doc, a) = Line(r, "ann.arrow.elbow", null, new Vec2(0, 50), new Vec2(100, 50));
            (doc, var b) = Line(r, "ann.arrow.elbow", doc, new Vec2(50, 0), new Vec2(50, 100));
            var mainA = T.All<PathPrim>(T.Render(r, doc, a.Id)).Single(p => p.Tag == AnnTags.Main);
            var mainB = T.All<PathPrim>(T.Render(r, doc, b.Id)).Single(p => p.Tag == AnnTags.Main);
            Assert.DoesNotContain(mainA.Segments, s => s.Op == SegOp.C);          // drawn first: no hop
            Assert.Contains(mainB.Segments, s => s.Op == SegOp.C);                // drawn later: hops over a
            Assert.Contains(Sample(mainB), p => p.X < 50 - 0.5 && Math.Abs(p.Y - 50) < Crossings.HopRadius + 0.01); // bulges left
            Assert.Equal(new[] { a.Id }, r.Type("ann.arrow.elbow")!.Dependents(doc.Get(b.Id)!, new RenderContext(doc)));
            // Switched off: straight through.
            var off = Edits.Replace(doc, doc.Get(b.Id)!.WithParam("hops", false));
            Assert.DoesNotContain(T.All<PathPrim>(T.Render(r, off, b.Id)).Single(p => p.Tag == AnnTags.Main).Segments, s => s.Op == SegOp.C);
            // A polyline with an arrowhead is a connector and hops too; a plain polyline neither hops nor is hopped over.
            (doc, var arrow) = Line(r, "ann.polyline", doc, new Vec2(30, 0), new Vec2(30, 100));
            doc = Edits.Replace(doc, doc.Get(arrow.Id)!.WithParam("headEnd", "triangle"));
            Assert.Contains(T.All<PathPrim>(T.Render(r, doc, arrow.Id)).Single(p => p.Tag == AnnTags.Main).Segments, s => s.Op == SegOp.C);
            (doc, var plain) = Line(r, "ann.polyline", doc, new Vec2(70, 0), new Vec2(70, 100));
            Assert.DoesNotContain(T.All<PathPrim>(T.Render(r, doc, plain.Id)).Single(p => p.Tag == AnnTags.Main).Segments, s => s.Op == SegOp.C);
            (doc, var late) = Line(r, "ann.arrow.elbow", doc, new Vec2(60, 20), new Vec2(80, 20));
            Assert.DoesNotContain(T.All<PathPrim>(T.Render(r, doc, late.Id)).Single(p => p.Tag == AnnTags.Main).Segments, s => s.Op == SegOp.C);
        }

        [Fact]
        public void AConnectorEndDockedOnAnotherConnectorGetsAJunctionDot()
        {
            var r = T.Reg();
            var (doc, main) = Line(r, "ann.arrow.elbow", null, new Vec2(0, 50), new Vec2(100, 50));
            var ctx = new RenderContext(doc);
            var rail = r.PageAnchors(doc.Get(main.Id)!, ctx).First(x => x.Role == AnchorRole.Rail);
            Assert.Equal("rail0", rail.Id);
            var at = new Vec2(30, 50);
            (doc, var branch) = Line(r, "ann.arrow.elbow", doc, new Vec2(30, 90), at);
            doc = Edits.Replace(doc, doc.Get(branch.Id)! with { Attach = new[] { null, Attachments.For(doc.Get(main.Id)!, at, rail) } });
            doc = Attachments.Refresh(r, doc, new RenderContext(doc));
            var dots = T.Render(r, doc, branch.Id).Where(p => p.Tag == AnnConnectors.JunctionTag).OfType<EllipsePrim>().ToList();
            Assert.Single(dots);
            Assert.True(dots[0].Center.DistanceTo(at) < 1e-6);
            // The docked end follows the main line.
            doc = Edits.Replace(doc, doc.Get(main.Id)! with { Points = new List<Vec2> { new(0, 60), new(100, 60) } });
            doc = Attachments.Refresh(r, doc, new RenderContext(doc));
            Assert.Equal(60, doc.ResolvePoints(doc.Get(branch.Id)!).Last().Y, 6);
            // The branch end on a line is not mistaken for a box side: the route still ends there.
            var route = T.All<PathPrim>(T.Render(r, doc, branch.Id)).Single(p => p.Tag == AnnTags.Main);
            Assert.Equal(60, Sample(route).Last().Y, 6);
            // A stand-alone junction dot docks with its centre port.
            var (doc2, j) = T.Insert(r, "ann.junction", new Vec2(10, 10));
            Assert.Equal(AnchorRole.Port, r.Type("ann.junction")!.Anchors(j, new RenderContext(doc2)).Single().Role);
            Assert.Equal(FillKind.Solid, T.All<EllipsePrim>(T.Render(r, doc2, j.Id)).Single().Style.Fill!.Kind);
        }

        [Fact]
        public void TheDoubleLineArrowRunsStraightAroundABendAndAsAUTurn()
        {
            var r = T.Reg();
            // Straight: two strokes 2 mm apart, chevron at the end.
            var (doc, s) = T.Insert(r, "ann.arrow.doubleline", new Vec2(50, 50));
            var prims = T.Render(r, doc, s.Id);
            var strokes = prims.Where(p => p.Tag == AnnTags.Main).Cast<PathPrim>().Select(Sample).ToList();
            Assert.Equal(2, strokes.Count);
            Assert.Equal(2, Math.Abs(strokes[0][0].Y - strokes[1][0].Y), 6);
            var head = prims.Single(p => p.Tag == "head");
            Assert.Equal(new Vec2(65, 50), Sample((PathPrim)head)[1]);              // chevron tip at the end point
            Assert.True(strokes.All(l => l.Last().X < 65 - 1), "strokes stop at the chevron legs");
            // Bend: free ends, wider than high: horizontal-vertical-horizontal, strokes stay 1 mm off the centre.
            (doc, var bend) = Line(r, "ann.arrow.doubleline", doc, new Vec2(0, 0), new Vec2(40, 30));
            var c = DoubleLineArrowType.RouteOf(doc.Get(bend.Id)!, doc.ResolvePoints(doc.Get(bend.Id)!));
            Assert.Equal(4, c.Count);
            // Every run of both strokes keeps 1 mm from the centre line (mitred corners lie on both offsets).
            foreach (var l in T.Render(r, doc, bend.Id).Where(p => p.Tag == AnnTags.Main).Cast<PathPrim>().Select(Sample))
                for (var i = 0; i + 1 < l.Count; i++)
                {
                    var m = Vec2.Lerp(l[i], l[i + 1], 0.5);
                    var dd = DistanceToPolyline(m, c);
                    Assert.True(Math.Abs(dd - 1) < 1e-6, $"{m} off by {dd}");
                }
            // U-turn: both ends docked on the right sides of two boxes.
            var (d2, top) = T.Insert(r, "ann.flow.process", new Vec2(100, 50), doc);
            var (d3, bottom) = T.Insert(r, "ann.flow.process", new Vec2(100, 120), d2);
            var (d4, u) = T.Insert(r, "ann.arrow.doubleline", new Vec2(0, 0), d3);
            d4 = Edits.Update(d4, new[] { u.Id }, x => x with
            {
                Points = new List<Vec2> { top.BoxPoint(0.5, 0), bottom.BoxPoint(0.5, 0) },
                Attach = new[] { new Attachment(top.Id, 0.5, 0) { AnchorId = "right" }, new Attachment(bottom.Id, 0.5, 0) { AnchorId = "right" } },
            });
            var ur = DoubleLineArrowType.RouteOf(d4.Get(u.Id)!, d4.ResolvePoints(d4.Get(u.Id)!));
            Assert.Equal(4, ur.Count);
            Assert.True(ur[1].X > top.Bounds().Right + 7.9 && Math.Abs(ur[1].X - ur[2].X) < 1e-9, "runs around on the right");
            var ustrokes = T.Render(r, d4, u.Id).Where(p => p.Tag == AnnTags.Main).Cast<PathPrim>().Select(Sample).ToList();
            Assert.Equal(2, ustrokes.Count);
            Assert.All(ustrokes, l => Assert.All(l.Skip(1).Take(l.Count - 2), p => Assert.True(p.X > top.Bounds().Right, "corners outside the boxes")));
        }

        private static double DistanceToPolyline(Vec2 p, IReadOnlyList<Vec2> c)
        {
            var best = double.MaxValue;
            for (var i = 0; i + 1 < c.Count; i++) best = Math.Min(best, Geo.ClosestOnSegment(p, c[i], c[i + 1]).Point.DistanceTo(p));
            return best;
        }

        public static IEnumerable<object[]> WideItems => new[] { "ann.arrow.wide", "ann.arrow.wide.double", "ann.arrow.wide.elbow", "ann.arrow.wide.uturn" }.Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(WideItems))]
        public void TheBlockArrowIsOneSimpleClosedOutline(string item)
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, item, new Vec2(80, 80));
            var main = T.All<PathPrim>(T.Render(r, doc, o.Id)).Single(p => p.Tag == AnnTags.Main);
            Assert.Equal(SegOp.Z, main.Segments.Last().Op);
            Assert.Equal(1, main.Segments.Count(s => s.Op == SegOp.M));
            var poly = Ann.Polylines(main).Single();
            poly.RemoveAt(poly.Count - 1);
            // No two non-adjacent edges cross (also around the corners of the elbow and the U-turn).
            for (var i = 0; i < poly.Count; i++)
                for (var j = i + 2; j < poly.Count; j++)
                {
                    if (i == 0 && j == poly.Count - 1) continue;
                    Assert.False(Cross(poly[i], poly[(i + 1) % poly.Count], poly[j], poly[(j + 1) % poly.Count]), $"{item}: edges {i} and {j} cross");
                }
            // The centre line runs inside the outline; the shaft is 6 mm wide, the head twice that.
            var c = WideArrowType.CenterLine(o, doc.ResolvePoints(o));
            for (var i = 0; i + 1 < c.Count; i++)
                Assert.True(Geo.PointInPolygon(Vec2.Lerp(c[i], c[i + 1], 0.5), poly), $"{item}: centre of run {i} inside");
            if (item == "ann.arrow.wide")
            {
                var b = Prim.Bounds(new[] { main })!.Value;
                Assert.Equal(12, b.H, 6);
                Assert.Equal(40, b.W, 6);
                Assert.Equal(7, poly.Count); // shaft corners, head wings and tip
            }
            if (item == "ann.arrow.wide.double") Assert.Equal(10, poly.Count);
            Assert.Equal(FillKind.Hatch, T.All<PathPrim>(T.Render(r, Edits.Replace(doc, o.WithParam("fill", "hatch")), o.Id)).Single(p => p.Tag == AnnTags.Main).Style.Fill!.Kind);
            // Text along the shaft, readable.
            var withText = Edits.Replace(doc, o with { Text = "Flow" });
            Assert.Equal("Flow", T.All<TextPrim>(T.Render(r, withText, o.Id)).Single().Text);
        }

        private static bool Cross(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            var r = b - a; var s = d - c;
            var den = r.Cross(s);
            if (Math.Abs(den) < 1e-12) return false;
            var t = (c - a).Cross(s) / den; var u = (c - a).Cross(r) / den;
            return t > 1e-9 && t < 1 - 1e-9 && u > 1e-9 && u < 1 - 1e-9;
        }

        [Fact]
        public void TheArrowPairRunsBothWaysAndSwaps()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.arrow.pair", new Vec2(50, 50));
            o = o.WithParam("labelForward", "request").WithParam("labelBack", "response");
            doc = Edits.Replace(doc, o);
            var lines = T.All<LinePrim>(T.Render(r, doc, o.Id)).ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal(3, Math.Abs(lines[0].A.Y - lines[1].A.Y), 6);
            Assert.True(lines[0].B.X > lines[0].A.X && lines[1].B.X < lines[1].A.X, "opposite directions");
            Assert.All(lines, l => Assert.Equal(ArrowHead.Triangle, l.Style.ArrowEnd));
            var texts = T.All<TextPrim>(T.Render(r, doc, o.Id)).ToList();
            var req = texts.Single(t => t.Text == "request"); var resp = texts.Single(t => t.Text == "response");
            Assert.True(req.Position.Y < lines.Min(l => l.A.Y) && resp.Position.Y > lines.Max(l => l.A.Y), "labels outside the pair");
            // Swap: the forward arrow changes sides (with its label).
            var swapped = r.Type(o.Type)!.Actions(o, Lang.En).Single(a => a.Id == "ann.pair.swap").Apply(o);
            var sl = T.All<LinePrim>(T.Render(r, Edits.Replace(doc, swapped), o.Id)).ToList();
            Assert.True(sl[0].B.X > sl[0].A.X && sl[0].A.Y > sl[1].A.Y, "forward arrow now below");
        }

        [Fact]
        public void BusMarksSitOnTheMiddleOfTheLongestSegment()
        {
            var r = T.Reg();
            foreach (var item in new[] { "ann.line", "ann.arrow", "ann.arrow.elbow", "ann.polyline" })
            {
                var (doc, o) = Line(r, item, null, new Vec2(0, 0), new Vec2(60, 0));
                Assert.Empty(T.Render(r, doc, o.Id).Where(p => p.Tag == AnnConnectors.BusTag));
                var slash = Edits.Replace(doc, doc.Get(o.Id)!.WithParam("busMark", "slash").WithParam("busCount", "8"));
                var marks = T.Render(r, slash, o.Id).Where(p => p.Tag == AnnConnectors.BusTag).ToList();
                var stroke = marks.OfType<LinePrim>().Single();
                Assert.Equal(new Vec2(30, 0), Vec2.Lerp(stroke.A, stroke.B, 0.5));
                Assert.True(Math.Abs(stroke.A.X - stroke.B.X) > 0.5 && Math.Abs(stroke.A.Y - stroke.B.Y) > 0.5, item + ": oblique");
                Assert.Equal("8", marks.OfType<TextPrim>().Single().Text);
                var strokes = Edits.Replace(doc, doc.Get(o.Id)!.WithParam("busMark", "strokes").WithParam("busCount", "4"));
                Assert.Equal(4, T.Render(r, strokes, o.Id).Count(p => p.Tag == AnnConnectors.BusTag && p is LinePrim));
                Assert.Contains(r.Type(o.Type)!.Actions(o, Lang.En), a => a.Id == "ann.bus.next");
            }
            Assert.Equal("n", r.Item("ann.line.bus")!.Params!["busCount"].Text);
        }

        [Fact]
        public void TheHeatArrowHasAWavyShaftAndTheLabelQ()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.arrow.heat", new Vec2(50, 50));
            var prims = T.Render(r, doc, o.Id);
            var shaft = T.All<PathPrim>(prims).Single(p => p.Tag == AnnTags.Main);
            Assert.True(shaft.Segments.Count(s => s.Op == SegOp.C) >= 4);
            Assert.Equal(SegOp.L, shaft.Segments.Last().Op); // straight into the head
            Assert.Equal(ArrowHead.Triangle, shaft.Style.ArrowEnd);
            var q = T.All<TextPrim>(prims).Single();
            Assert.Equal("Q", q.Text);
            Assert.True(q.TextStyle.Italic);
        }

        [Fact]
        public void TheTableLaysOutCellsHeaderAndBorders()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.table", new Vec2(100, 100));
            // Without cells: a small table in the drawing language.
            Assert.Contains(T.All<TextPrim>(r.Render(o, new RenderContext(doc, Lang.De))), t => t.Text == "Merkmal");
            o = o.WithParam("cells", "Step|Description of the step|t\nS1|Measure|10\nS2|Compare the values|20");
            doc = Edits.Replace(doc, o);
            var type = r.Type(o.Type)!;
            var ctx = new RenderContext(doc);
            var prims = type.Render(o, ctx);
            var texts = prims.OfType<TextPrim>().ToList();
            Assert.Equal(9, texts.Count);
            Assert.All(texts.Take(3), t => Assert.True(t.TextStyle.Bold));
            Assert.All(texts.Skip(3), t => Assert.False(t.TextStyle.Bold));
            var rows = TableType.Parse(o.Str("cells", ""));
            Assert.Equal("Compare the values", rows[2][1]);
            Assert.Equal(new[] { "a", "b" }, TableType.Parse("a\tb")[0]);                 // tab separates columns too
            Assert.Equal(2, TableType.Parse("a|b\nc|d").Count);                         // a written "\n" breaks the row
            // Columns from the content: the description column is the widest; texts stay inside their cells.
            var widths = TableType.ColumnWidths(rows, o.Size.W);
            Assert.Equal(o.Size.W, widths.Sum(), 6);
            Assert.True(widths[1] > widths[0] && widths[1] > widths[2]);
            var xs = new List<double> { -o.Size.W / 2 };
            foreach (var w in widths) xs.Add(xs[xs.Count - 1] + w);
            for (var i = 0; i < texts.Count; i++)
            {
                var col = i % 3;
                var e = Ann.TextExtent(texts[i].Text, texts[i].TextStyle.Size);
                var left = texts[i].TextStyle.Anchor == TextAnchor.Start ? texts[i].Position.X : texts[i].Position.X - e.X / 2;
                Assert.True(left >= xs[col] - 1e-6 && left + e.X <= xs[col + 1] + 1e-6, $"cell {i} '{texts[i].Text}' in column {col}");
            }
            int Rules(string borders) => type.Render(o.WithParam("borders", borders), ctx).Count(p => p.Tag == "rule");
            Assert.Equal(2 + 2, Rules("all"));        // two row lines, two column lines
            Assert.Equal(0, Rules("outer"));
            Assert.Equal(2 + 2, Rules("horizontal")); // top, bottom and two row lines
            Assert.Equal(0, Rules("none"));
            Assert.Contains(type.Render(o.WithParam("borders", "outer"), ctx), p => p.Tag == AnnTags.Main && p is RectPrim);
            // Add a row: the box grows by one row height.
            var added = type.Actions(o, Lang.En).Single(a => a.Id == "ann.table.row.add").Apply(o);
            Assert.Equal(4, TableType.Parse(added.Str("cells", "")).Count);
            Assert.Equal(o.Size.H * 4 / 3, added.Size.H, 6);
        }

        [Fact]
        public void ViewArrowAndDetailViewCarryTheirLetters()
        {
            var r = T.Reg();
            var (doc, v) = T.Insert(r, "ann.viewarrow", new Vec2(50, 50));
            var touching = T.All<TextPrim>(T.Render(r, doc, v.Id)).Single();
            Assert.Equal("A", touching.Text);
            Assert.True(touching.Position.Y > 35 && touching.Position.Y < 50, "beside the shaft");
            var (doc2, f) = T.Insert(r, "ann.viewarrow.free", new Vec2(50, 50));
            Assert.True(T.All<TextPrim>(T.Render(r, doc2, f.Id)).Single().Position.Y < 35, "beyond the tail");
            // The detail view takes the letter of the detail marker on its page.
            var (d1, circle) = T.Insert(r, "ann.detail.circle", new Vec2(40, 40));
            d1 = Edits.Replace(d1, circle.WithParam("letter", "B"));
            var (d2, view) = T.Insert(r, "ann.detail.view", new Vec2(150, 80), d1);
            Assert.Equal("B", T.All<TextPrim>(T.Render(r, d2, view.Id)).Single().Text);
            Assert.Contains(view.Id, r.Type("ann.detail.circle")!.Dependents(d2.Get(circle.Id)!, new RenderContext(d2)));
            Assert.Single(T.All<EllipsePrim>(T.Render(r, d2, view.Id)));
            Assert.Single(T.All<RectPrim>(T.Render(r, Edits.Replace(d2, view.WithParam("frame", "rect")), view.Id)));
            // Graphic scale bar: 5 mm at 2:1 is drawn 10 mm long, labelled with the real length.
            var bar = Edits.Replace(d2, view.WithParam("scaleBar", true));
            var prims = T.Render(r, bar, view.Id);
            var main = prims.OfType<LinePrim>().Where(p => p.Tag == "scalebar").First();
            Assert.Equal(10, main.A.DistanceTo(main.B), 6);
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Text == "5 mm");
            Assert.Equal(5, DetailViewType.NiceBelow(7.3));
            Assert.Equal(0.2, DetailViewType.NiceBelow(0.27), 9);
        }

        [Fact]
        public void MaterialHatchPresetsAreInTheLibraryAndDrawInside()
        {
            var r = T.Reg();
            foreach (var m in HatchPresets.Materials)
            {
                var item = r.Item("ann.hatch.rect." + m);
                Assert.NotNull(item);
                var (doc, o) = T.Insert(r, item!.Id, new Vec2(60, 60));
                var hatch = T.Render(r, doc, o.Id).Where(p => p.Tag == AnnTags.Hatch).ToList();
                Assert.NotEmpty(hatch);
                var box = o.Bounds();
                Assert.All(hatch, p => Assert.True(Prim.Bounds(new[] { p })!.Value is var b && b.X >= box.X - 0.3 && b.Right <= box.Right + 0.3 && b.Y >= box.Y - 0.3 && b.Bottom <= box.Bottom + 0.3, m));
            }
        }
    }
}
