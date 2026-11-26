using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Technical drawing indications: geometric tolerances, datums, surface texture, welds, clouds, chain dimensions.</summary>
    public class DrawingSymbolTests
    {
        private static (Registry R, SketchDocument Doc, SketchObject O) Place(string item, IReadOnlyList<Vec2>? points = null, Func<SketchObject, SketchObject>? f = null)
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var o = r.Create(r.Item(item)!, new Vec2(100, 100));
            if (points is not null) o = o with { Points = points };
            if (f is not null) o = f(o);
            return (r, Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        private static IReadOnlyList<Primitive> Render((Registry R, SketchDocument Doc, SketchObject O) s) => T.Render(s.R, s.Doc, s.O.Id);

        private static List<TextPrim> Texts(IReadOnlyList<Primitive> prims, string tag) => T.All<TextPrim>(prims).Where(t => t.Tag == tag).ToList();

        // ---- Feature control frame -------------------------------------------------------------------------------------

        [Fact]
        public void DefaultFrameShowsPositionWithDiameterAndDatum()
        {
            var s = Place("ann.gdt.frame", new[] { new Vec2(100, 100), new Vec2(110, 90) });
            var prims = Render(s);
            Assert.Equal("Ø0.1", Texts(prims, "gdt.tolerance").Single().Text);
            Assert.Equal(new[] { "A" }, Texts(prims, "gdt.datum").Select(t => t.Text));
            Assert.Contains(Prim.Flatten(prims), p => p.Tag == "gdt.symbol");
            // The frame starts at point 1 and runs away from the tip; the leader carries the arrow at the tip.
            var frame = T.All<RectPrim>(prims).Single(p => p.Tag == AnnTags.Main);
            Assert.Equal(110, frame.X, 9);
            Assert.Equal(90, frame.Y + frame.H / 2, 9);
            var lead = T.All<LinePrim>(prims).Single(p => p.Tag == AnnTags.Lead);
            Assert.Equal(new Vec2(100, 100), lead.A);
            Assert.Equal(ArrowHead.Triangle, lead.Style.ArrowStart);
        }

        [Fact]
        public void FrameLeftOfTheTipEndsAtPointOne()
        {
            var s = Place("ann.gdt.frame", new[] { new Vec2(100, 100), new Vec2(90, 90) });
            var frame = T.All<RectPrim>(Render(s)).Single(p => p.Tag == AnnTags.Main);
            Assert.Equal(90, frame.X + frame.W, 9);
        }

        [Fact]
        public void FormTolerancesHaveNoDatumsAndThreeDatumsWidenTheFrame()
        {
            var flat = Place("ann.gdt.frame.flatness", new[] { new Vec2(100, 100), new Vec2(110, 90) });
            var prims = Render(flat);
            Assert.Empty(Texts(prims, "gdt.datum"));
            Assert.Equal("0.05", Texts(prims, "gdt.tolerance").Single().Text);
            var narrow = T.All<RectPrim>(prims).Single(p => p.Tag == AnnTags.Main).W;

            var three = Place("ann.gdt.frame", new[] { new Vec2(100, 100), new Vec2(110, 90) }, o => GdtFrameType.SetDatums(o, "A B C"));
            var p3 = Render(three);
            Assert.Equal(new[] { "A", "B", "C" }, Texts(p3, "gdt.datum").Select(t => t.Text));
            Assert.True(T.All<RectPrim>(p3).Single(p => p.Tag == AnnTags.Main).W > narrow + 10);
            // Five cells: four separators.
            Assert.Equal(4, T.All<LinePrim>(p3).Count(l => l.Tag is null && Math.Abs(l.A.X - l.B.X) < 1e-9));
        }

        [Fact]
        public void SetDatumsReadsSeparatorsAndClearsTheRest()
        {
            var o = new SketchObject().WithParam("datum3", "Z");
            o = GdtFrameType.SetDatums(o, "A, B");
            Assert.Equal("A", o.Str("datum1", "")); Assert.Equal("B", o.Str("datum2", "")); Assert.Equal("", o.Str("datum3", "x"));
            o = GdtFrameType.SetDatums(o, "A|B|C|D");
            Assert.Equal("C", o.Str("datum3", ""));
        }

        [Fact]
        public void CharacteristicIsADropdownWithPictureForEveryOption()
        {
            var s = Place("ann.gdt.frame");
            var options = s.R.Actions(s.O, new RenderContext(s.Doc)).Where(a => a.Choice?.Key == "ann.gdt.characteristic").ToList();
            Assert.Equal(14, options.Count);
            Assert.All(options, a => Assert.True(a.Preview is { Count: > 0 }, a.Id));
            Assert.Single(options, a => a.IsActive);
            var flat = options.Single(a => a.Id.EndsWith("=flatness", StringComparison.Ordinal)).Apply(s.O);
            Assert.Equal("flatness", flat.Str("characteristic", ""));
            // Datums are offered only where they apply.
            var datums = s.R.Actions(flat, new RenderContext(s.Doc)).Single(a => a.Id == "ann.gdt.datums");
            Assert.False(datums.IsEnabled);
        }

        [Fact]
        public void EveryCharacteristicPictogramStaysInItsCell()
        {
            foreach (var (c, _) in GdtSymbols.Characteristics)
            {
                var prims = GdtSymbols.Draw(c, new Vec2(10, 10), 4);
                Assert.True(prims.Count > 0, c);
                var b = Prim.Bounds(prims)!.Value;
                Assert.True(b.X >= 7.75 && b.Y >= 7.75 && b.X + b.W <= 12.25 && b.Y + b.H <= 12.25, c + " " + b);
            }
            // The pictograms differ from each other.
            var shapes = GdtSymbols.Characteristics.Select(x => string.Join(";", GdtSymbols.Draw(x.Value, Vec2.Zero, 4).SelectMany(p => Ann.Polylines(p)).Select(l => string.Join(" ", l))));
            Assert.Equal(14, shapes.Distinct().Count());
        }

        // ---- Datum -------------------------------------------------------------------------------------------------------

        [Fact]
        public void DatumHasTriangleOnTheFeatureAndLetterInItsFrame()
        {
            var s = Place("ann.gdt.datum", new[] { new Vec2(100, 100), new Vec2(100, 85) });
            var prims = Render(s);
            var tri = T.All<PathPrim>(prims).Single(p => p.Tag == "gdt.triangle");
            Assert.Equal("#000000", tri.Style.Fill!.Color);
            Assert.Equal(2, tri.Segments.Count(g => Math.Abs(g.P.Y - 100) < 1e-9)); // base on the feature
            Assert.Equal("A", Texts(prims, "gdt.datum").Single().Text);
            Assert.Equal(new Vec2(100, 85), Texts(prims, "gdt.datum").Single().Position);
            var open = Place("ann.gdt.datum", null, o => o.WithParam("filled", false) with { Text = "B" });
            var p2 = Render(open);
            Assert.Equal("#ffffff", T.All<PathPrim>(p2).Single(p => p.Tag == "gdt.triangle").Style.Fill!.Color);
            Assert.Equal("B", Texts(p2, "gdt.datum").Single().Text);
        }

        // ---- Surface texture ---------------------------------------------------------------------------------------------

        [Fact]
        public void SurfaceSymbolProcessesAndRequirement()
        {
            var removal = Render(Place("ann.surface"));
            Assert.Contains(Prim.Flatten(removal), p => p.Tag == "surface.bar");
            Assert.Equal("Ra 3.2", Texts(removal, "surface.value").Single().Text);
            Assert.Contains(Prim.Flatten(removal), p => p.Tag == "surface.extension");

            var any = Render(Place("ann.surface", null, o => o.WithParam("process", "any")));
            Assert.DoesNotContain(Prim.Flatten(any), p => p.Tag is "surface.bar" or "surface.circle");

            var none = Render(Place("ann.surface", null, o => o.WithParam("process", "noRemoval") with { Text = "" }));
            Assert.Contains(Prim.Flatten(none), p => p.Tag == "surface.circle");
            Assert.DoesNotContain(Prim.Flatten(none), p => p.Tag is "surface.extension" or "surface.value");

            var around = Render(Place("ann.surface", null, o => o.WithParam("allAround", true)));
            Assert.Contains(Prim.Flatten(around), p => p.Tag == "surface.allaround");
        }

        [Fact]
        public void SurfaceSymbolTipIsItsMountAnchorAndLowestPoint()
        {
            var s = Place("ann.surface");
            var tip = s.R.PageAnchors(s.O, new RenderContext(s.Doc)).Single(a => a.Id == "tip");
            Assert.Equal(AnchorRole.Mount, tip.Role);
            var legs = T.All<PathPrim>(Render(s)).Single(p => p.Tag == AnnTags.Main);
            var legTip = legs.Segments[1].P; // the tip of the legs (page coordinates)
            Assert.Equal(legTip.X, tip.P.X, 9);
            Assert.Equal(legTip.Y, tip.P.Y, 9);
            Assert.Equal(legTip.Y, legs.Segments.Max(g => g.P.Y), 9);
            Assert.Equal(s.O.Transform.Y + s.O.Size.H / 2, tip.P.Y, 9);
        }

        // ---- Weld --------------------------------------------------------------------------------------------------------

        [Fact]
        public void WeldSidesPlaceTheSymbolAndTheIdentificationLine()
        {
            var pts = new[] { new Vec2(100, 100), new Vec2(110, 90) };
            var arrow = Render(Place("ann.weld", pts));
            var glyph = Prim.Flatten(arrow).Where(p => p.Tag == "weld.symbol").ToList();
            Assert.NotEmpty(glyph);
            Assert.True(Prim.Bounds(glyph)!.Value.Y + Prim.Bounds(glyph)!.Value.H <= 90 + 1e-9, "arrow side: above the reference line");
            var ident = T.All<LinePrim>(arrow).Single(l => l.Tag == "weld.ident");
            Assert.Equal(DashStyle.Dash, ident.Style.Dash);
            Assert.Equal(90 + WeldSymbolType.IdentGap, ident.A.Y, 9);
            var reference = T.All<LinePrim>(arrow).Single(l => l.Tag == AnnTags.Main);
            Assert.True(reference.B.X > reference.A.X, "reference line runs away from the tip");
            Assert.Equal(ArrowHead.Triangle, T.All<LinePrim>(arrow).Single(l => l.Tag == "weld.arrow").Style.ArrowEnd);

            var other = Render(Place("ann.weld", pts, o => o.WithParam("side", "other")));
            var g2 = Prim.Bounds(Prim.Flatten(other).Where(p => p.Tag == "weld.symbol").ToList())!.Value;
            Assert.True(g2.Y >= 90 + WeldSymbolType.IdentGap - 1e-9, "other side: below the dashed line");

            var both = Render(Place("ann.weld", pts, o => o.WithParam("side", "both").WithParam("kind", "v")));
            Assert.DoesNotContain(T.All<LinePrim>(both), l => l.Tag == "weld.ident");
            var g3 = Prim.Bounds(Prim.Flatten(both).Where(p => p.Tag == "weld.symbol").ToList())!.Value;
            Assert.True(g3.Y < 90 && g3.Y + g3.H > 90);
        }

        [Fact]
        public void WeldKindsDifferAndExtrasAppear()
        {
            var shapes = WeldSymbolType.Kinds.Select(k => string.Join(";", WeldSymbolType.Glyph(k.Value, 0, 0, 4, -1).SelectMany(p => Ann.Polylines(p)).Select(l => string.Join(" ", l)))).ToList();
            Assert.Equal(WeldSymbolType.Kinds.Length, shapes.Distinct().Count());
            var left = Place("ann.weld", new[] { new Vec2(100, 100), new Vec2(90, 90) }, o => o.WithParam("allAround", true).WithParam("length", "100") with { Text = "a5" });
            var prims = Render(left);
            var reference = T.All<LinePrim>(prims).Single(l => l.Tag == AnnTags.Main);
            Assert.True(reference.B.X < reference.A.X, "reference line to the left when the knee is left of the tip");
            Assert.Contains(Prim.Flatten(prims), p => p.Tag == "weld.allaround");
            Assert.Equal("a5", Texts(prims, "weld.size").Single().Text);
            Assert.Equal("100", Texts(prims, "weld.length").Single().Text);
            var s = Place("ann.weld");
            var kinds = s.R.Actions(s.O, new RenderContext(s.Doc)).Where(a => a.Choice?.Key == "ann.weld.kind").ToList();
            Assert.Equal(5, kinds.Count);
            Assert.All(kinds, a => Assert.NotNull(a.Preview));
        }

        // ---- Revision cloud and chain dimension --------------------------------------------------------------------------

        [Fact]
        public void RevisionCloudIsAClosedChainOfArcsInsideItsBox()
        {
            var s = Place("ann.cloud");
            var path = T.All<PathPrim>(Render(s)).Single(p => p.Tag == AnnTags.Main);
            Assert.Equal(SegOp.Z, path.Segments[path.Segments.Count - 1].Op);
            Assert.True(path.Segments.Count(g => g.Op == SegOp.C) >= 16);
            var b = Prim.Bounds(Ann.Polylines(path).Select(l => (Primitive)Prim.Polyline(l)).ToList())!.Value;
            Assert.True(b.W <= s.O.Size.W + 0.01 && b.H <= s.O.Size.H + 0.01);
            Assert.True(b.W > s.O.Size.W - 1 && b.H > s.O.Size.H - 1);
        }

        [Fact]
        public void ChainDimensionMeasuresEveryStretch()
        {
            var s = Place("ann.dimension.chain", new[] { new Vec2(100, 100), new Vec2(135, 100), new Vec2(120, 104), new Vec2(80, 100) });
            var texts = Texts(Render(s), AnnTags.DimText).OrderBy(t => t.Position.X).Select(t => t.Text).ToList();
            Assert.Equal(new[] { "20", "20", "15" }, texts);
            Assert.Equal(3, T.All<LinePrim>(Render(s)).Count(l => l.Tag == AnnTags.Main));
            var vertical = Place("ann.dimension.chain", new[] { new Vec2(100, 100), new Vec2(101, 110), new Vec2(100, 130) }, o => o.WithParam("unit", "mm"));
            Assert.Equal(new[] { "10 mm", "20 mm" }, Texts(Render(vertical), AnnTags.DimText).OrderBy(t => t.Position.Y).Select(t => t.Text));
            Assert.DoesNotContain(s.R.Type("ann.dimension.chain")!.Parameters, p => p.Key == "text");
        }

        // ---- Common ------------------------------------------------------------------------------------------------------

        [Fact]
        public void NewElementsAreInTheLibraryWithGermanNamesAndKeywords()
        {
            var r = T.Reg();
            foreach (var id in new[] { "ann.gdt.frame", "ann.gdt.datum", "ann.surface", "ann.weld", "ann.beam.diagram", "ann.cloud", "ann.dimension.chain" })
            {
                var t = r.Type(id);
                Assert.NotNull(t);
                Assert.Contains(r.Library, i => i.TypeId == id);
                Assert.False(string.IsNullOrEmpty(t!.Name.De));
            }
            Assert.Contains(r.Search("toleranzrahmen"), i => i.TypeId == "ann.gdt.frame");
            Assert.Contains(r.Search("rauheit"), i => i.TypeId == "ann.surface");
            Assert.Contains(r.Search("kehlnaht"), i => i.TypeId == "ann.weld");
            Assert.Contains(r.Search("momentenverlauf"), i => i.TypeId == "ann.beam.diagram");
            Assert.Contains(r.Search("änderungswolke"), i => i.TypeId == "ann.cloud");
            Assert.Contains(r.Search("kettenmaß"), i => i.TypeId == "ann.dimension.chain");
            // No user-visible text of the new elements uses the word "patent".
            foreach (var i in r.Library.Where(i => i.TypeId.StartsWith("ann.gdt", StringComparison.Ordinal) || i.TypeId is "ann.surface" or "ann.weld" or "ann.beam.diagram" or "ann.cloud" or "ann.dimension.chain"))
            {
                Assert.DoesNotContain("patent", i.Name.En, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("patent", i.Name.De!, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void NewElementsSurviveTheDocumentRoundTrip()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var objects = new[]
            {
                r.Create(r.Item("ann.gdt.frame")!, new Vec2(50, 50)).WithParam("characteristic", "totalRunout").WithParam("diameter", false),
                GdtFrameType.SetDatums(r.Create(r.Item("ann.gdt.frame")!, new Vec2(50, 80)), "A B C"),
                r.Create(r.Item("ann.gdt.datum")!, new Vec2(80, 50)).WithParam("filled", false),
                r.Create(r.Item("ann.surface")!, new Vec2(110, 50)).WithParam("process", "noRemoval").WithParam("value", "Rz 6.3"),
                r.Create(r.Item("ann.weld")!, new Vec2(140, 50)).WithParam("kind", "bevel").WithParam("side", "both").WithParam("allAround", true),
                r.Create(r.Item("ann.beam.diagram")!, new Vec2(170, 50)).WithParam("support", "cantileverRight").WithParam("force", 2.5).WithParam("length", 1.2),
                r.Create(r.Item("ann.cloud")!, new Vec2(200, 50)).WithParam("arc", 7.0),
                r.Create(r.Item("ann.dimension.chain")!, new Vec2(230, 50)),
            };
            foreach (var o in objects) doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            Assert.Equal("totalRunout", back.Get(objects[0].Id)!.Str("characteristic", ""));
            Assert.Equal(1.2, back.Get(objects[5].Id)!.Num("length", 0));
        }
    }
}
