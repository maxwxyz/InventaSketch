using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Minimal test module: a box, a gear-like circle and a shaft axis.</summary>
    internal sealed class TestModule : IModule
    {
        public string Id => "test";
        public I18nText Name => new("Test", "Test");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new BoxType(), new WheelType(), new AxisType(), new LineType() };
        public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("test", new I18nText("Test"), "test") };
        public IEnumerable<LibraryItem> Library => new[]
        {
            new LibraryItem("test.box", "test.box", new I18nText("Box", "Kasten"), "test") { Keywords = new[] { "rectangle" } },
            new LibraryItem("test.wheel", "test.wheel", new I18nText("Wheel", "Rad"), "test"),
        };

        internal sealed class BoxType : ObjectType
        {
            public override string Id => "test.box";
            public override I18nText Name => new("Box", "Kasten");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, Style.Visible with { Fill = Fill.Hatched(new HatchPattern(45, 2)) }) };
        }

        internal sealed class WheelType : ObjectType
        {
            public override string Id => "test.wheel";
            public override I18nText Name => new("Wheel");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.Ellipse(0, 0, o.Size.W / 2, o.Size.H / 2) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, o.Size.W / 2);
                yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero);
            }
        }

        internal sealed class AxisType : ObjectType
        {
            public override string Id => "test.axis";
            public override I18nText Name => new("Axis");
            public override Size2 DefaultSize => new(100, 4);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.Line(-o.Size.W / 2, 0, o.Size.W / 2, 0, Style.CenterLine) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Segment(AnchorRole.Axis, new Vec2(-o.Size.W / 2, 0), new Vec2(o.Size.W / 2, 0));
            }
        }

        internal sealed class LineType : ObjectType
        {
            public override string Id => "test.line";
            public override I18nText Name => new("Line");
            public override GeometryKind Geometry => GeometryKind.Points;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var p = ctx.Doc.ResolvePoints(o);
                return new Primitive[] { Prim.Line(p[0], p[1], Style.ThinLine with { ArrowEnd = ArrowHead.Triangle }) };
            }
        }
    }

    public class GeometryTests
    {
        [Fact]
        public void MatrixInvertRoundTrips()
        {
            var m = Matrix2D.Translation(10, 5) * Matrix2D.Rotation(0.7) * Matrix2D.Scaling(2, -1);
            var p = new Vec2(3, 4);
            var back = m.Invert().Apply(m.Apply(p));
            Assert.Equal(p.X, back.X, 9);
            Assert.Equal(p.Y, back.Y, 9);
        }

        [Fact]
        public void SegmentIntersectionFindsCrossing()
        {
            var x = Geo.SegmentIntersection(new Vec2(0, 0), new Vec2(10, 10), new Vec2(0, 10), new Vec2(10, 0));
            Assert.NotNull(x);
            Assert.Equal(5, x!.Value.X, 9);
        }

        [Fact]
        public void RotatedRectBecomesExactPath()
        {
            var p = Prim.Transform(Prim.Rect(-1, -1, 2, 2), Matrix2D.Rotation(Math.PI / 4));
            var path = Assert.IsType<PathPrim>(p);
            Assert.True(path.IsClosed);
            Assert.Equal(Math.Sqrt(2), path.Segments[0].P.Length, 9);
        }
    }

    public class HatchingTests
    {
        private static readonly IReadOnlyList<Vec2> Square = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10) };

        [Fact]
        public void HorizontalHatchFillsSquare()
        {
            var segs = Hatching.Segments(new[] { Square }, new HatchPattern(0, 2));
            Assert.Equal(4, segs.Count); // y = 2, 4, 6, 8
            Assert.All(segs, s => Assert.Equal(10, s.B.X - s.A.X, 6));
        }

        [Fact]
        public void HolesStayEmpty()
        {
            IReadOnlyList<Vec2> hole = new[] { new Vec2(3, 3), new Vec2(7, 3), new Vec2(7, 7), new Vec2(3, 7) };
            var segs = Hatching.Segments(new[] { Square, hole }, new HatchPattern(0, 1));
            foreach (var (a, b) in segs)
            {
                var m = Vec2.Lerp(a, b, 0.5);
                Assert.False(m.X > 3 && m.X < 7 && m.Y > 3 && m.Y < 7, "Hatch line inside hole");
            }
            Assert.Contains(segs, s => Math.Abs(s.A.Y - 5) < 1e-9 && s.B.X <= 3 + 1e-9);
        }

        [Fact]
        public void HatchAt45DegreesStaysInside()
        {
            var segs = Hatching.Segments(new[] { Square }, new HatchPattern(45, 1.5, Cross: true));
            Assert.NotEmpty(segs);
            Assert.All(segs, s =>
            {
                Assert.InRange(s.A.X, -1e-6, 10 + 1e-6); Assert.InRange(s.A.Y, -1e-6, 10 + 1e-6);
                Assert.InRange(s.B.X, -1e-6, 10 + 1e-6); Assert.InRange(s.B.Y, -1e-6, 10 + 1e-6);
            });
            // 45 degrees rises to the right on screen: dy has the opposite sign of dx.
            var first = segs.First(s => Math.Abs(s.B.X - s.A.X) > 1);
            Assert.True((first.B.X - first.A.X) * (first.B.Y - first.A.Y) < 0);
        }
    }

    public class RegistryAndRenderTests
    {
        private static Registry Reg() => new Registry().Register(new TestModule());

        [Fact]
        public void DuplicateModuleIsRejected()
        {
            var r = Reg();
            Assert.Throws<InvalidOperationException>(() => r.Register(new TestModule()));
        }

        [Fact]
        public void SearchFindsGermanAndKeywords()
        {
            var r = Reg();
            Assert.Single(r.Search("kasten"));
            Assert.Single(r.Search("rectangle"));
            Assert.Equal(2, r.Search("").Count());
        }

        [Fact]
        public void RenderAppliesTransformAndFreeScale()
        {
            var r = Reg();
            var o = r.Create(r.Item("test.box")!, new Vec2(50, 40)) with { Size = new Size2(40, 10), Transform = new Transform(50, 40, 90) };
            var doc = SketchDocument.Create();
            var prims = r.Render(o, new RenderContext(doc));
            var b = Prim.Bounds(prims)!.Value;
            Assert.Equal(10, b.W, 6);
            Assert.Equal(40, b.H, 6);
            Assert.Equal(50, b.Center.X, 6);
        }

        [Fact]
        public void SvgContainsHatchAndArrowheadAndIsMonochrome()
        {
            var r = Reg();
            var doc = SketchDocument.Create();
            var page = doc.Pages[0];
            var box = r.Create(r.Item("test.box")!, new Vec2(30, 30)) with { Style = new Style { Stroke = "#ff0000" } };
            var line = r.Type("test.line")!.Create(new Vec2(100, 50));
            doc = Edits.Add(doc, page.Id, box, line);
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.Contains("data-isk-id=\"" + box.Id, svg);
            Assert.DoesNotContain("#ff0000", svg);
            Assert.Matches("<path d=\"M[^\"]*Z\"", svg); // arrow head rendered as closed path
            Assert.Contains("viewBox=\"0 0 338.667 190.5\"", svg);
        }
    }

    public class EditingTests
    {
        [Fact]
        public void AttachPointFollowsTargetAndReverseKeepsAttachments()
        {
            var (_, doc, box, line) = Setup();
            var corner = box.BoxPoint(-0.5, 0.5);
            doc = Edits.AttachPoint(doc, line.Id, 1, box.Id, corner, "pin1");
            var att = doc.Get(line.Id)!.Attach![1]!;
            Assert.Equal("pin1", att.AnchorId);
            Assert.Equal(-0.5, att.U, 6);
            doc = Edits.Move(doc, new[] { box.Id }, new Vec2(10, 0));
            Assert.Equal(corner.X + 10, doc.ResolvePoints(doc.Get(line.Id)!)[1].X, 6);
            var reversed = Edits.ReversePoints(doc.Get(line.Id)!);
            Assert.Equal("pin1", reversed.Attach![0]!.AnchorId);
            Assert.Equal(box.Id, reversed.Attach[1]!.ObjectId); // the original first point was attached to the box
        }

        private static (Registry R, SketchDocument Doc, SketchObject Box, SketchObject Line) Setup()
        {
            var r = new Registry().Register(new TestModule());
            var doc = SketchDocument.Create();
            var box = r.Create(r.Item("test.box")!, new Vec2(50, 50));
            var line = r.Type("test.line")!.Create(new Vec2(100, 100)) with { Attach = new Attachment?[] { new Attachment(box.Id, 0.5, 0), null } };
            doc = Edits.Add(doc, doc.Pages[0].Id, box, line);
            return (r, doc, box, line);
        }

        [Fact]
        public void AttachedPointFollowsMovedObject()
        {
            var (_, doc, box, line) = Setup();
            var before = doc.ResolvePoints(doc.Get(line.Id)!)[0];
            doc = Edits.Move(doc, new[] { box.Id }, new Vec2(10, 5));
            var after = doc.ResolvePoints(doc.Get(line.Id)!)[0];
            Assert.Equal(before.X + 10, after.X, 9);
            Assert.Equal(before.Y + 5, after.Y, 9);
        }

        [Fact]
        public void MovingLeaderAloneDetachesIt()
        {
            var (_, doc, box, line) = Setup();
            var start = doc.ResolvePoints(doc.Get(line.Id)!)[0];
            doc = Edits.Move(doc, new[] { line.Id }, new Vec2(3, 3));
            var moved = doc.Get(line.Id)!;
            Assert.Null(moved.Attach);
            Assert.Equal(start + new Vec2(3, 3), moved.Points![0]);
        }

        [Fact]
        public void RemoveKeepsDependentPointsInPlace()
        {
            var (_, doc, box, line) = Setup();
            var p = doc.ResolvePoints(doc.Get(line.Id)!)[0];
            doc = Edits.Remove(doc, new[] { box.Id });
            Assert.Null(doc.Get(box.Id));
            Assert.Equal(p, doc.Get(line.Id)!.Points![0]);
            Assert.DoesNotContain(box.Id, doc.Pages[0].Order);
        }

        [Fact]
        public void FreeScaleChangesSizeNotParams()
        {
            var (_, doc, box, _) = Setup();
            doc = Edits.Update(doc, new[] { box.Id }, o => o.WithParam("teeth", 20));
            doc = Edits.Scale(doc, new[] { box.Id }, new Vec2(50, 50), 2, 1);
            var o = doc.Get(box.Id)!;
            Assert.Equal(60, o.Size.W, 9);
            Assert.Equal(30, o.Size.H, 9);
            Assert.Equal(20, o.Int("teeth", 0));
        }

        [Fact]
        public void FlipTwiceRestores()
        {
            var (_, doc, box, _) = Setup();
            doc = Edits.Rotate(doc, new[] { box.Id }, new Vec2(0, 0), 30);
            var before = doc.Get(box.Id)!.Corners();
            doc = Edits.Flip(doc, new[] { box.Id }, new Vec2(10, 10), true);
            doc = Edits.Flip(doc, new[] { box.Id }, new Vec2(10, 10), true);
            var after = doc.Get(box.Id)!.Corners();
            for (var i = 0; i < 4; i++) Assert.True(before[i].DistanceTo(after[i]) < 1e-9);
        }

        [Fact]
        public void DuplicateRemapsAttachmentsAndGroups()
        {
            var (_, doc, box, line) = Setup();
            doc = Edits.Group(doc, new[] { box.Id, line.Id });
            var (d2, ids) = Edits.Duplicate(doc, doc.Pages[0].Id, new[] { box.Id, line.Id }, new Vec2(5, 0));
            var copyLine = d2.Get(ids[1])!;
            Assert.Equal(ids[0], copyLine.Attach![0]!.ObjectId);
            Assert.NotEqual(d2.Get(box.Id)!.GroupId, d2.Get(ids[0])!.GroupId);
            Assert.Equal(d2.Get(ids[0])!.GroupId, copyLine.GroupId);
        }

        [Fact]
        public void HistoryUndoRedo()
        {
            var (_, doc, box, _) = Setup();
            var h = new History(doc);
            h.Apply("move", d => Edits.Move(d, new[] { box.Id }, new Vec2(1, 0)));
            Assert.True(h.CanUndo);
            h.Undo();
            Assert.Equal(50, h.Current.Get(box.Id)!.Transform.X);
            h.Redo();
            Assert.Equal(51, h.Current.Get(box.Id)!.Transform.X);
        }

        [Fact]
        public void ReorderBringsToFront()
        {
            var (_, doc, box, line) = Setup();
            doc = Edits.Reorder(doc, doc.Pages[0].Id, new[] { box.Id }, int.MaxValue);
            Assert.Equal(box.Id, doc.Pages[0].Order.Last());
        }
    }

    public class SnappingTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void GearsMeshOnPitchCircles()
        {
            var moving = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(0, 0), 10) };
            var targets = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(31, 0), 20) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("mesh", r.Kind);
            Assert.Equal(1, r.Delta.X, 9); // centre distance becomes 30 = 10 + 20
        }

        [Fact]
        public void PlanetMeshesInsideRing()
        {
            var moving = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(0, -28), 10) };
            var targets = new[] { Anchor.Circle(AnchorRole.PitchInner, new Vec2(0, 0), 40) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("mesh", r.Kind);
            Assert.Equal(-30, (new Vec2(0, -28) + r.Delta).Y, 9);
        }

        [Fact]
        public void HubSnapsOntoAxis()
        {
            var moving = new[] { Anchor.Point(AnchorRole.Hub, new Vec2(40, 2)) };
            var targets = new[] { Anchor.Segment(AnchorRole.Axis, new Vec2(0, 0), new Vec2(100, 0)) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("axis", r.Kind);
            Assert.Equal(-2, r.Delta.Y, 9);
            Assert.Equal(0, r.Delta.X, 9);
        }

        [Fact]
        public void FarAwayDoesNotSnapButUsesGrid()
        {
            var moving = new[] { Anchor.Point(AnchorRole.Center, new Vec2(12.2, 7.9)) };
            var targets = new[] { Anchor.Point(AnchorRole.Center, new Vec2(80, 80)) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt with { Grid = 5 });
            Assert.Equal("grid", r.Kind);
            Assert.Equal(-2.2, r.Delta.X, 9);
            Assert.Equal(2.1, r.Delta.Y, 9);
        }

        [Fact]
        public void AlignmentGuideOnX()
        {
            var moving = new[] { Anchor.Point(AnchorRole.Center, new Vec2(49, 10)) };
            var targets = new[] { Anchor.Point(AnchorRole.Center, new Vec2(50, 80)) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt with { Alignment = true });
            Assert.Equal("align", r.Kind);
            Assert.Equal(1, r.Delta.X, 9);
            Assert.Equal(GuideKind.Vertical, r.Guides[0].Kind);
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void RoundTripPreservesEverything()
        {
            var r = new Registry().Register(new TestModule());
            var doc = SketchDocument.Create(PageSizes.A4Portrait, "Gearbox");
            var box = r.Create(r.Item("test.box")!, new Vec2(10, 20)).WithParam("teeth", 24).WithParam("hub", "fixed").WithParam("helix", true) with { Ref = "10", View = "axial" };
            var line = r.Type("test.line")!.Create(new Vec2(0, 0)) with { Attach = new Attachment?[] { new Attachment(box.Id, 0.25, -0.5), null }, Style = new Style { ArrowEnd = ArrowHead.Stealth, Dash = DashStyle.DashDot } };
            doc = Edits.Add(doc, doc.Pages[0].Id, box, line);
            doc = Edits.SetReference(doc, "10", "housing");
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(24, back.Get(box.Id)!.Int("teeth", 0));
            Assert.Equal("fixed", back.Get(box.Id)!.Str("hub", ""));
            Assert.True(back.Get(box.Id)!.Flag("helix", false));
            Assert.Contains("\"arrowEnd\": \"stealth\"", json);
            var compact = DocumentJson.ToCompact(doc);
            Assert.StartsWith("isk1:", compact);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(compact)));
        }

        [Fact]
        public void ForeignAndNewerDocumentsAreRejected()
        {
            Assert.Throws<FormatException>(() => DocumentJson.Deserialize("{\"format\":\"other\"}"));
            Assert.Throws<FormatException>(() => DocumentJson.Deserialize("{\"format\":\"inventasketch-document\",\"version\":99}"));
        }

        [Fact]
        public void LanguageDetectionFallsBackToEnglish()
        {
            Assert.Equal(Lang.De, Language.Detect("de-AT"));
            Assert.Equal(Lang.En, Language.Detect("fr-FR"));
            Assert.Equal(Lang.En, Language.Detect(null));
            Assert.Equal("Kasten", new I18nText("Box", "Kasten").Get(Lang.De));
            Assert.Equal("Box", new I18nText("Box").Get(Lang.De));
        }
    }
}
