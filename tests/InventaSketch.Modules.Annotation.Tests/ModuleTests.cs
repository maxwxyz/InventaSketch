using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    internal static class T
    {
        public static Registry Reg() => new Registry().Register(new AnnotationModule());

        public static (SketchDocument Doc, SketchObject Obj) Insert(Registry r, string itemId, Vec2 at, SketchDocument? doc = null)
        {
            doc ??= SketchDocument.Create(PageSizes.A4Landscape);
            var o = r.Create(r.Item(itemId) ?? throw new KeyNotFoundException(itemId), at);
            return (Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        public static IReadOnlyList<Primitive> Render(Registry r, SketchDocument doc, string id) => r.Render(doc.Get(id)!, new RenderContext(doc));

        public static IEnumerable<T2> All<T2>(IEnumerable<Primitive> prims) where T2 : Primitive => Prim.Flatten(prims).OfType<T2>();
    }

    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => T.Reg();

        [Fact]
        public void AllTypesAreBilingualAndUseAnnPrefix()
        {
            var r = T.Reg();
            Assert.True(r.Types.Count() >= 45);
            foreach (var t in r.Types)
            {
                Assert.StartsWith("ann.", t.Id);
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                foreach (var p in t.Parameters) Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "." + p.Key);
            }
            foreach (var i in r.Library) Assert.False(string.IsNullOrEmpty(i.Name.De), i.Id);
        }

        [Fact]
        public void EveryTypeHasALibraryItemInAKnownCategory()
        {
            var r = T.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            Assert.Equal(7, cats.Count);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            foreach (var i in r.Library) Assert.Contains(i.Category, cats);
            Assert.Contains(r.Library, i => i.Category == AnnCategories.Mechanics && i.TypeId == "ann.spring");
        }

        [Fact]
        public void SearchFindsGermanKeywords()
        {
            var r = T.Reg();
            Assert.Contains(r.Search("schraffur"), i => i.TypeId == "ann.hatch.rect");
            Assert.Contains(r.Search("bezugszeichen"), i => i.TypeId == "ann.leader");
            Assert.Contains(r.Search("loslager"), i => i.TypeId == "ann.support.roller");
        }
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersWithoutParameters()
        {
            var r = T.Reg();
            foreach (var item in r.Library)
            {
                var (doc, o) = T.Insert(r, item.Id, new Vec2(120, 100));
                var prims = T.Render(r, doc, o.Id);
                Assert.True(prims.Count > 0, item.Id + " renders nothing");
                var b = Prim.Bounds(prims);
                Assert.NotNull(b);
                Assert.True(b!.Value.W + b.Value.H > 2, item.Id + " is degenerate");
                Assert.True(double.IsFinite(b.Value.X) && double.IsFinite(b.Value.Y), item.Id);
                var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
                Assert.Contains("data-isk-type=\"" + item.TypeId + "\"", svg);
                Assert.DoesNotContain("NaN", svg);
            }
        }

        [Fact]
        public void FreeScalingKeepsParametersAndTextSize()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.label", new Vec2(50, 50));
            doc = Edits.Scale(doc, new[] { o.Id }, new Vec2(50, 50), 3, 0.5);
            var text = T.All<TextPrim>(T.Render(r, doc, o.Id)).Single();
            Assert.Equal(Ann.TextSize, text.TextStyle.Size, 9);
            Assert.Equal(108, doc.Get(o.Id)!.Size.W, 9);
        }

        [Fact]
        public void MonochromeSvgHasNoColours()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            foreach (var item in r.Library.Take(20))
            {
                var o = r.Create(item, new Vec2(100, 100)) with { Style = new Style { Stroke = "#cc2200" } };
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
            }
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.DoesNotContain("#cc2200", svg);
            var colour = System.Text.RegularExpressions.Regex.Matches(svg, "#[0-9a-fA-F]{6}").Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();
            Assert.All(colour, c => Assert.True(c is "#000000" or "#ffffff", c));
        }

        [Fact]
        public void StyleOverrideColoursTextAndArrowsOnlyMainStroke()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.dimension.linear", new Vec2(50, 50));
            doc = Edits.Update(doc, new[] { o.Id }, x => x with { Style = new Style { Stroke = "#0000ff", ArrowEnd = ArrowHead.Diamond } });
            var prims = T.Render(r, doc, o.Id);
            Assert.All(T.All<TextPrim>(prims), t => Assert.Equal("#0000ff", t.TextStyle.Color));
            var lines = T.All<LinePrim>(prims).ToList();
            Assert.Equal(ArrowHead.Diamond, lines.Single(l => l.Tag == AnnTags.Main).Style.ArrowEnd);
            Assert.All(lines.Where(l => l.Tag != AnnTags.Main), l => Assert.Null(l.Style.ArrowEnd));
        }
    }

    public class LeaderAndConnectorTests
    {
        [Fact]
        public void LeaderAttachedToBoxFollowsWhenBoxMoves()
        {
            var r = T.Reg();
            var (doc, box) = T.Insert(r, "ann.label", new Vec2(60, 60));
            var (doc2, leader) = T.Insert(r, "ann.leader", new Vec2(100, 100), doc);
            var edge = box.BoxPoint(0.5, 0);
            doc2 = Ann.AttachPoint(doc2, leader.Id, 0, box.Id, edge);
            Assert.Equal(edge, doc2.ResolvePoints(doc2.Get(leader.Id)!)[0]);

            doc2 = Edits.Move(doc2, new[] { box.Id }, new Vec2(12, -7));
            var start = doc2.ResolvePoints(doc2.Get(leader.Id)!)[0];
            Assert.Equal(edge.X + 12, start.X, 9);
            Assert.Equal(edge.Y - 7, start.Y, 9);
            var lead = T.Render(r, doc2, leader.Id).Single(p => p.Tag == AnnTags.Lead);
            var a = lead is LinePrim l ? l.A : ((PathPrim)lead).Segments[0].P;
            Assert.True(a.DistanceTo(start) < 1e-9);

            // Rotating the box keeps the attachment on the rotated edge.
            doc2 = Edits.Rotate(doc2, new[] { box.Id }, new Vec2(72, 53), 90);
            var rotated = doc2.ResolvePoints(doc2.Get(leader.Id)!)[0];
            Assert.True(rotated.DistanceTo(doc2.Get(box.Id)!.BoxPoint(0.5, 0)) < 1e-9);
        }

        [Fact]
        public void LeaderLineStopsBeforeNumeralAndHasNoHeadByDefault()
        {
            var r = T.Reg();
            var (doc, leader) = T.Insert(r, "ann.leader", new Vec2(50, 50));
            var prims = T.Render(r, doc, leader.Id);
            var text = T.All<TextPrim>(prims).Single();
            Assert.Equal("10", text.Text);
            var line = Assert.IsType<LinePrim>(prims.Single(p => p.Tag == AnnTags.Lead));
            Assert.Equal(ArrowHead.None, line.Style.ArrowStart ?? ArrowHead.None);
            Assert.True(line.B.DistanceTo(text.Position) > LeaderGeometry.TextGap);
            Assert.True(line.Style.EffectiveWidth < 0.3);
        }

        [Fact]
        public void LeaderCurveActionsChangeGeometry()
        {
            var r = T.Reg();
            var (doc, leader) = T.Insert(r, "ann.leader", new Vec2(50, 50));
            var type = r.TypeOf(leader);
            var actions = type.Actions(leader, Lang.De).ToList();
            Assert.Contains(actions, a => a.Id == "ann.curve.straight" && a.IsActive);
            var wavy = actions.Single(a => a.Id == "ann.curve.wavy").Apply(leader);
            doc = Edits.Replace(doc, wavy);
            var path = Assert.IsType<PathPrim>(T.Render(r, doc, leader.Id).Single(p => p.Tag == AnnTags.Lead));
            Assert.Equal(SegOp.C, path.Segments[1].Op);
            var dot = actions.Single(a => a.Id == "ann.end.dot").Apply(wavy);
            doc = Edits.Replace(doc, dot);
            Assert.Equal(ArrowHead.Oval, ((PathPrim)T.Render(r, doc, leader.Id).Single(p => p.Tag == AnnTags.Lead)).Style.ArrowStart);
        }

        [Fact]
        public void BoxesExposePortsAtEdgeMidpoints()
        {
            var r = T.Reg();
            var (doc, box) = T.Insert(r, "ann.flow.process", new Vec2(80, 40));
            var ports = r.PageAnchors(doc.Get(box.Id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).Select(a => a.P).ToList();
            Assert.Equal(4, ports.Count);
            Assert.Contains(ports, p => p.DistanceTo(new Vec2(80 + box.Size.W / 2, 40)) < 1e-9);
        }

        [Fact]
        public void ArrowEndSnapsToPortAndElbowLeavesEdgePerpendicular()
        {
            var r = T.Reg();
            var (doc, a) = T.Insert(r, "ann.flow.process", new Vec2(50, 50));
            var (doc2, b) = T.Insert(r, "ann.flow.process", new Vec2(150, 90), doc);
            var (doc3, elbow) = T.Insert(r, "ann.arrow.elbow", new Vec2(100, 70), doc2);
            var targets = Snapper.Targets(r, doc3, doc3.Pages[0], new[] { elbow.Id }, new RenderContext(doc3), new SnapOptions { Grid = null });
            var raw = new Vec2(50 + a.Size.W / 2 + 0.8, 50.6);
            var snap = Snapper.SnapPoint(raw, targets, new SnapOptions { Grid = null });
            var port = raw + snap.Delta;
            Assert.True(port.DistanceTo(new Vec2(50 + a.Size.W / 2, 50)) < 1e-9);
            doc3 = Ann.AttachPoint(doc3, elbow.Id, 0, a.Id, port);
            doc3 = Ann.AttachPoint(doc3, elbow.Id, 1, b.Id, b.BoxPoint(0, -0.5));
            var path = Assert.IsType<PathPrim>(T.Render(r, doc3, elbow.Id).Single());
            var pts = path.Segments.Select(s => s.P).ToList();
            Assert.Equal(3, pts.Count); // one bend: horizontal out of the right edge, vertical into the top edge
            Assert.Equal(pts[0].Y, pts[1].Y, 9);
            Assert.Equal(pts[1].X, pts[2].X, 9);
        }

        [Fact]
        public void SwapReversesPointsAndAttachments()
        {
            var r = T.Reg();
            var (doc, box) = T.Insert(r, "ann.label", new Vec2(60, 60));
            var (doc2, arrow) = T.Insert(r, "ann.arrow", new Vec2(100, 100), doc);
            doc2 = Ann.AttachPoint(doc2, arrow.Id, 0, box.Id, box.BoxPoint(0.5, 0));
            var o = doc2.Get(arrow.Id)!;
            var swapped = r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == "ann.swap").Apply(o);
            Assert.Null(swapped.Attach![0]);
            Assert.Equal(box.Id, swapped.Attach[1]!.ObjectId);
            Assert.Equal(o.Points![0], swapped.Points![1]);
        }

        [Fact]
        public void ArrowHeadParametersAndToggles()
        {
            var r = T.Reg();
            var (doc, arrow) = T.Insert(r, "ann.arrow", new Vec2(100, 100));
            var line = Assert.IsType<LinePrim>(T.Render(r, doc, arrow.Id).Single());
            Assert.Equal(ArrowHead.Triangle, line.Style.ArrowEnd);
            Assert.Equal(ArrowHead.None, line.Style.ArrowStart);
            var o = arrow.WithParam("headStart", "stealth").WithParam("headSize", "large");
            var actions = r.TypeOf(o).Actions(o, Lang.En).ToList();
            o = actions.Single(a => a.Id == "ann.head.end").Apply(o);
            doc = Edits.Replace(doc, o);
            line = Assert.IsType<LinePrim>(T.Render(r, doc, arrow.Id).Single());
            Assert.Equal(ArrowHead.Stealth, line.Style.ArrowStart);
            Assert.Equal(ArrowHead.None, line.Style.ArrowEnd);
            Assert.Equal(ArrowSize.Large, line.Style.ArrowSize);
            var dbl = T.Insert(r, "ann.arrow.double", new Vec2(0, 0));
            var dl = Assert.IsType<LinePrim>(T.Render(r, dbl.Doc, dbl.Obj.Id).Single());
            Assert.Equal(ArrowHead.Triangle, dl.Style.ArrowStart);
        }

        [Fact]
        public void CurvedArrowPassesThroughMiddlePoint()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.arrow.curved", new Vec2(0, 0));
            var path = Assert.IsType<PathPrim>(T.Render(r, doc, o.Id).Single());
            var c = path.Segments[1];
            var mid = Geo.CubicAt(path.Segments[0].P, c.C1, c.C2, c.P, 0.5);
            Assert.True(mid.DistanceTo(o.Points![1]) < 1e-9);
        }
    }

    public class DimensionTests
    {
        private static string DimText(Registry r, SketchDocument doc, string id) => T.All<TextPrim>(T.Render(r, doc, id)).Single(t => t.Tag == AnnTags.DimText).Text;

        [Fact]
        public void DimensionTextEqualsDistance()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.dimension.aligned", new Vec2(0, 0));
            doc = Edits.Replace(doc, o with { Points = new[] { new Vec2(10, 10), new Vec2(40, 50) } });
            Assert.Equal("50", DimText(r, doc, o.Id));

            var (doc2, lin) = T.Insert(r, "ann.dimension.linear", new Vec2(0, 0));
            doc2 = Edits.Replace(doc2, lin with { Points = new[] { new Vec2(10, 10), new Vec2(40, 50) } });
            Assert.Equal("40", DimText(r, doc2, lin.Id)); // auto: vertical (|dy| > |dx|)
            doc2 = Edits.Replace(doc2, doc2.Get(lin.Id)!.WithParam("axis", "horizontal"));
            Assert.Equal("30", DimText(r, doc2, lin.Id));
            doc2 = Edits.Replace(doc2, doc2.Get(lin.Id)!.WithParam("scale", 2.0).WithParam("unit", "mm"));
            Assert.Equal("60 mm", DimText(r, doc2, lin.Id));
            doc2 = Edits.Replace(doc2, doc2.Get(lin.Id)! with { Text = "L" });
            Assert.Equal("L", DimText(r, doc2, lin.Id));
        }

        [Fact]
        public void DefaultLinearDimensionMeasuresThirtyAndDrawsExtensionLines()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.dimension.linear", new Vec2(50, 50));
            var prims = T.Render(r, doc, o.Id);
            Assert.Equal("30", T.All<TextPrim>(prims).Single().Text);
            var lines = T.All<LinePrim>(prims).ToList();
            Assert.Equal(3, lines.Count);
            var main = lines.Single(l => l.Tag == AnnTags.Main);
            Assert.Equal(42, main.A.Y, 9); // offset -8 above the points
            Assert.Equal(ArrowHead.Triangle, main.Style.ArrowStart);
        }

        [Fact]
        public void AngleAndRadiusDimensions()
        {
            var r = T.Reg();
            var (doc, a) = T.Insert(r, "ann.dimension.angle", new Vec2(0, 0));
            doc = Edits.Replace(doc, a with { Points = new[] { new Vec2(0, 0), new Vec2(20, 0), new Vec2(0, -20) } });
            Assert.Equal("90°", DimText(r, doc, a.Id));
            var (doc2, rad) = T.Insert(r, "ann.dimension.radius", new Vec2(0, 0));
            Assert.Equal("R15", DimText(r, doc2, rad.Id));
            doc2 = Edits.Replace(doc2, rad.WithParam("kind", "diameter"));
            Assert.Equal("Ø30", DimText(r, doc2, rad.Id));
        }
    }

    public class HatchTests
    {
        private static IReadOnlyList<IReadOnlyList<Vec2>> Region(IEnumerable<Primitive> prims)
            => Hatching.Contours(prims.First(p => p.Tag == AnnTags.Region || p is EllipsePrim || p is RectPrim));

        private static void AssertInside(IEnumerable<(Vec2 A, Vec2 B)> segs, IReadOnlyList<IReadOnlyList<Vec2>> contours, string what)
        {
            var bounds = Rect.FromPoints(contours.SelectMany(c => c)).Inflate(1e-6);
            var n = 0;
            foreach (var (a, b) in segs)
            {
                n++;
                Assert.True(bounds.Contains(a) && bounds.Contains(b), what + ": hatch end outside region bounds");
                Assert.True(Ann.Inside(Vec2.Lerp(a, b, 0.5), contours), what + ": hatch segment outside region");
            }
            Assert.True(n > 0, what + ": no hatch generated");
        }

        [Theory]
        [InlineData("ann.hatch.rect")]
        [InlineData("ann.hatch.ellipse")]
        [InlineData("ann.hatch.polygon")]
        public void HatchRegionsHatchOnlyInside(string typeId)
        {
            var r = T.Reg();
            foreach (var (pattern, _, _) in HatchPresets.Options)
            {
                var (doc, o) = T.Insert(r, typeId, new Vec2(80, 60));
                var obj = o.WithParam("pattern", pattern);
                if (!obj.IsPointGeometry) obj = obj with { Transform = obj.Transform with { Rotation = 30 }, Size = new Size2(40, 22) };
                doc = Edits.Replace(doc, obj);
                foreach (var outline in new[] { true, false })
                {
                    doc = Edits.Replace(doc, doc.Get(o.Id)!.WithParam("outline", outline));
                    var prims = T.Render(r, doc, o.Id);
                    var what = typeId + "/" + pattern + "/" + outline;
                    var region = prims.FirstOrDefault(p => p.Tag == AnnTags.Region);
                    var fill = region switch { PathPrim pp => pp.Style.Fill, RectPrim rp => rp.Style.Fill, EllipsePrim ep => ep.Style.Fill, _ => null };
                    if (fill?.Kind == FillKind.Hatch)
                    {
                        var contours = Hatching.Contours(region!);
                        AssertInside(Hatching.Segments(contours, fill.Hatch!), contours, what);
                    }
                    else if (fill?.Kind == FillKind.Dots)
                    {
                        Assert.Equal("dotted", pattern);
                    }
                    else
                    {
                        // Explicit geometry: compare against the true outline rendered with the outline switched on.
                        var withOutline = T.Render(r, Edits.Replace(doc, doc.Get(o.Id)!.WithParam("outline", true)), o.Id);
                        var contours = Hatching.Contours(withOutline.First(p => p.Tag == AnnTags.Region));
                        var lines = prims.Where(p => p.Tag == AnnTags.Hatch).ToList();
                        Assert.NotEmpty(lines);
                        if (pattern == "dotted")
                            Assert.All(lines, e => Assert.True(Ann.Inside(Prim.Bounds(new[] { e })!.Value.Center, contours), what)); // rotated dots become paths
                        else
                            AssertInside(lines.Cast<LinePrim>().Select(l => (l.A, l.B)), contours, what);
                    }
                }
            }
        }

        [Fact]
        public void SvgHatchStaysInsideRectangle()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.hatch.rect", new Vec2(50, 50));
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
            var path = System.Text.RegularExpressions.Regex.Match(svg, "<path fill=\"none\" stroke=\"#000000\" stroke-width=\"0.25\" d=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(path);
            var nums = System.Text.RegularExpressions.Regex.Matches(path, "-?[0-9.]+").Cast<System.Text.RegularExpressions.Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            for (var i = 0; i + 1 < nums.Count; i += 2)
            {
                Assert.InRange(nums[i], 35 - 1e-3, 65 + 1e-3);
                Assert.InRange(nums[i + 1], 40 - 1e-3, 60 + 1e-3);
            }
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void JsonRoundTripPreservesAllItemsAndRendering()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var x = 10.0;
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(x % 280, 20 + 30 * Math.Floor(x / 280)));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                x += 40;
            }
            var box = doc.Objects.Values.First(o => o.Type == "ann.label");
            var leader = doc.Objects.Values.First(o => o.Type == "ann.leader");
            doc = Ann.AttachPoint(doc, leader.Id, 0, box.Id, box.BoxPoint(0.5, 0.5));
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
        }
    }

    public class ActionTests
    {
        [Fact]
        public void AllContextActionsApplyAndKeepObjectRenderable()
        {
            var r = T.Reg();
            foreach (var item in r.Library)
            {
                var (doc, o) = T.Insert(r, item.Id, new Vec2(100, 100));
                foreach (var a in r.TypeOf(o).Actions(o, Lang.De))
                {
                    Assert.False(string.IsNullOrEmpty(a.Label.De), a.Id);
                    var changed = a.Apply(o);
                    Assert.Equal(o.Id, changed.Id);
                    var d = Edits.Replace(doc, changed);
                    Assert.NotEmpty(T.Render(r, d, o.Id));
                }
            }
        }
    }

    /// <summary>Writes an HTML/SVG gallery of all library items to the test output for visual inspection.</summary>
    public class GalleryTests
    {
        public static string GalleryDir => Path.Combine(AppContext.BaseDirectory, "gallery");

        [Fact]
        public void WriteGallery()
        {
            var r = T.Reg();
            var html = new StringBuilder();
            html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Annotation gallery</title><style>")
                .Append("body{font-family:Arial,sans-serif;margin:16px;background:#fff}h2{margin:18px 0 6px;font-size:16px}")
                .Append(".grid{display:flex;flex-wrap:wrap;gap:10px}.cell{border:1px solid #ddd;padding:6px;width:220px;height:170px;display:flex;flex-direction:column;align-items:center;justify-content:space-between}")
                .Append(".cell svg{max-width:208px;max-height:132px}.cap{font-size:11px;color:#333;text-align:center}</style></head><body>");
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                html.Append("<h2>").Append(cat.Name.En).Append(" / ").Append(cat.Name.De).Append("</h2><div class=\"grid\">");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var (doc, o) = T.Insert(r, item.Id, new Vec2(100, 100));
                    if (item.TypeId.StartsWith("ann.flow.", StringComparison.Ordinal) && item.TypeId != "ann.flow.step" && o.Text is null)
                    {
                        o = o with { Text = item.TypeId == "ann.flow.connector" ? "A" : "Step" };
                        if (item.TypeId == "ann.flow.process") o = o.WithParam("step", "S100");
                        doc = Edits.Replace(doc, o);
                    }
                    var prims = T.Render(r, doc, o.Id);
                    var b = Prim.Bounds(prims)!.Value.Inflate(3);
                    var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = b });
                    const double scale = 4; // px per mm
                    svg = svg.Replace("<svg ", "<svg width=\"" + (b.W * scale).ToString("0", CultureInfo.InvariantCulture) + "\" height=\"" + (b.H * scale).ToString("0", CultureInfo.InvariantCulture) + "\" ");
                    html.Append("<div class=\"cell\">").Append(svg).Append("<div class=\"cap\">").Append(item.Id).Append("<br>").Append(item.Name.En).Append("</div></div>");
                }
                html.Append("</div>");
            }
            html.Append("</body></html>");
            Directory.CreateDirectory(GalleryDir);
            File.WriteAllText(Path.Combine(GalleryDir, "annotation-scene.html"), "<!doctype html><html><body style=\"margin:0;background:#fff\">" + Scene(r) + "</body></html>");
            var file = Path.Combine(GalleryDir, "annotation-gallery.html");
            File.WriteAllText(file, html.ToString());
            Assert.True(new FileInfo(file).Length > 10000);
        }

        /// <summary>A small free-body diagram plus a labelled section to check composition.</summary>
        private static string Scene(Registry r)
        {
            var doc = SketchDocument.Create(new Size2(200, 110));
            var pid = doc.Pages[0].Id;
            SketchObject Add(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = r.Create(r.Item(item)!, new Vec2(x, y));
                if (f is not null) o = f(o);
                doc = Edits.Add(doc, pid, o);
                return o;
            }
            var beam = Add("ann.beam", 60, 40, o => o with { Size = new Size2(80, 4) });
            Add("ann.support.pin", 20, 49);
            Add("ann.support.roller", 100, 50);
            Add("ann.load.distributed", 60, 30, o => o with { Size = new Size2(80, 12) });
            Add("ann.force", 90, 58, o => o with { Points = new[] { new Vec2(75, 75), new Vec2(75, 42) } });
            Add("ann.dimension.linear", 0, 0, o => o with { Points = new[] { new Vec2(20, 70), new Vec2(100, 70) }, Params = new Dictionary<string, ParamValue>(o.Params) { ["offset"] = 12.0 } });
            var part = Add("ann.hatch.rect", 160, 45, o => o with { Size = new Size2(40, 26) });
            var leader = Add("ann.leader.wavy", 0, 0, o => o with { Points = new[] { new Vec2(170, 50), new Vec2(185, 85) }, Text = "12" });
            doc = Ann.AttachPoint(doc, leader.Id, 0, part.Id, new Vec2(170, 50));
            Add("ann.sectionline", 0, 0, o => o with { Points = new[] { new Vec2(160, 22), new Vec2(160, 68) } });
            Add("ann.figlabel", 160, 100);
            return SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ObjectIds = false }).Replace("width=\"200mm\" height=\"110mm\"", "width=\"800\" height=\"440\"");
        }
    }
}
