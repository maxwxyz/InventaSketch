using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInsideItsBoxInBothLanguages()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = r.Create(item, Vec2.Zero);
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var prims = r.TypeOf(o).Render(o, Fx.Ctx(lang));
                    var what = $"{item.Id} [{lang}]";
                    if (o.IsPointGeometry)
                    {
                        Assert.NotEmpty(prims);
                        Fx.AssertFinite(prims, what);
                    }
                    else Fx.AssertInsideBox(prims, o.Size, what);
                }
            }
        }

        [Fact]
        public void StretchedAndSquashedBoxesStayInsideTheirBox()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => i.Size is not null))
                foreach (var (sx, sy) in new[] { (2.0, 0.6), (0.5, 1.8), (3.0, 3.0), (0.3, 0.3), (0.15, 1.0), (4.0, 0.25) })
                {
                    var o = r.Create(item, Vec2.Zero);
                    o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                    Fx.AssertInsideBox(r.TypeOf(o).Render(o, Fx.Ctx(Lang.De)), o.Size, $"{item.Id} {sx}x{sy}");
                }
        }

        private static IEnumerable<ParamValue> Variants(ParamDef p) => p.Kind switch
        {
            ParamKind.Choice => p.Options!.Select(x => (ParamValue)x.Value),
            ParamKind.Boolean => new ParamValue[] { true, false },
            ParamKind.Integer => new ParamValue[] { p.Min ?? 0, p.Max ?? 10 },
            ParamKind.Number => new ParamValue[] { p.Min ?? -1000, p.Max ?? 1000, 0, (p.Default?.Number ?? 0) + 0.37 },
            _ => new ParamValue[] { "", "A long label 123" },
        };

        [Fact]
        public void ParameterVariantsRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                foreach (var p in t.Parameters)
                    foreach (var val in Variants(p))
                    {
                        var o = r.SetParam(r.Create(item, Vec2.Zero), p.Key, val);
                        var prims = t.Render(o, Fx.Ctx(Lang.De));
                        var what = $"{item.Id}.{p.Key}={val}";
                        if (o.IsPointGeometry) Fx.AssertFinite(prims, what);
                        else Fx.AssertInsideBox(prims, o.Size, what);
                    }
            }
        }

        /// <summary>A different, valid value for a parameter.</summary>
        private static ParamValue Other(ParamDef p, SketchObject o)
        {
            switch (p.Kind)
            {
                case ParamKind.Boolean: return !o.Flag(p.Key, p.Default?.Bool ?? false);
                case ParamKind.Choice: return p.Options!.First(x => x.Value != o.Str(p.Key, p.Default?.Text ?? "")).Value;
                case ParamKind.Text: return o.Str(p.Key, "") + "Q";
                default:
                    {
                        var cur = o.Num(p.Key, p.Default?.Number ?? 0);
                        var step = p.Kind == ParamKind.Integer ? 1 : Math.Max(p.Step ?? 1, 0.5);
                        var next = cur + step <= (p.Max ?? double.MaxValue) ? cur + step : cur - step;
                        return p.Kind == ParamKind.Integer ? (int)next : next;
                    }
            }
        }

        [Fact]
        public void EveryParameterChangesTheDrawing()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var item = r.Item(t.Id)!;
                foreach (var p in t.Parameters)
                {
                    var o = r.Create(item, Vec2.Zero);
                    // Some parameters only show in a state that is off by default.
                    if (t.Id == "math.vector.arrow" && p.Key is "notation" or "textSize") o = o.WithParam("components", true);
                    if (t.Id == "math.circle.unit" && p.Key == "values") o = o.WithParam("tan", true);
                    if (t.Id == "math.venn.diagram" && p.Key == "labelC") o = o.WithParam("sets", 3);
                    if (t.Id == "math.tree.probability" && p.Key is "e3" or "p3") o = o.WithParam("levels", 3);
                    var before = Fx.Signature(r, o);
                    // "auto" draws an arc at 45°, like "arc": compare with the double arc instead.
                    var value = t.Id == "math.angle.mark" && p.Key == "mark" ? (ParamValue)"double"
                        : t.Id == "math.tree.probability" && p.Key == "p3" ? "0.1" // a typed probability (text that is no number is ignored)
                        : Other(p, o);
                    var after = Fx.Signature(r, r.SetParam(o, p.Key, value));
                    Assert.True(before != after, $"{t.Id}.{p.Key} does not change the drawing");
                }
            }
        }

        [Fact]
        public void FreeScalingChangesTheSizeNotTheParameters()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var doc = SketchDocument.Create();
                var o = r.Create(item, new Vec2(100, 80));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                var scaled = Edits.Scale(doc, new[] { o.Id }, new Vec2(100, 80), 1.7, 0.6).Get(o.Id)!;
                Assert.Equal(o.Params.OrderBy(kv => kv.Key), scaled.Params.OrderBy(kv => kv.Key));
                if (o.IsPointGeometry)
                {
                    Assert.NotEqual(o.Points, scaled.Points);
                    Fx.AssertFinite(r.TypeOf(scaled).Render(scaled, Fx.Ctx()), item.Id);
                }
                else
                {
                    Assert.Equal(o.Size.W * 1.7, scaled.Size.W, 6);
                    Assert.Equal(o.Size.H * 0.6, scaled.Size.H, 6);
                    Fx.AssertInsideBox(r.TypeOf(scaled).Render(scaled, Fx.Ctx()), scaled.Size, item.Id + " scaled");
                }
            }
        }

        [Fact]
        public void EveryElementOffersSnapAnchorsInsideItsBox()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = r.Create(item, Vec2.Zero);
                var anchors = r.TypeOf(o).Anchors(o, Fx.Ctx()).ToList();
                Assert.True(anchors.Count > 0, item.Id + ": no anchors");
                Assert.Equal(anchors.Where(a => a.Id is not null).Select(a => a.Id).Distinct().Count(), anchors.Count(a => a.Id is not null));
                // Point-index ids ("p3", "j3") are reserved for line points.
                Assert.All(anchors, a => Assert.Null(Attachments.PointIndex(a.Id)));
                if (o.IsPointGeometry) continue;
                double hw = o.Size.W / 2 + 0.02, hh = o.Size.H / 2 + 0.02;
                foreach (var a in anchors)
                {
                    Assert.True(Math.Abs(a.P.X) <= hw && Math.Abs(a.P.Y) <= hh, $"{item.Id}: anchor {a.Id} at {a.P} outside the box");
                    if (a.Kind == AnchorKind.Segment) Assert.True(Math.Abs(a.Q.X) <= hw && Math.Abs(a.Q.Y) <= hh, $"{item.Id}: rail {a.Id} outside the box");
                }
            }
        }

        [Fact]
        public void KeyPointsAreConnectionPoints()
        {
            var r = Fx.Reg();
            void Has(string item, params string[] ids)
            {
                var o = Fx.Item(r, item);
                var anchors = r.TypeOf(o).Anchors(o, Fx.Ctx()).ToList();
                foreach (var id in ids) Assert.Contains(anchors, a => a.Id == id && a.Role == AnchorRole.Port);
            }
            Has("math.axes.cartesian", "origin");
            Has("math.axes.spatial", "origin");
            Has("math.axes.polar", "pole");
            Has("math.point.labelled", "pt");
            Has("math.shape.triangle", "A", "B", "C");
            Has("math.shape.triangle.incircle", "A", "B", "C", "centre");
            Has("math.circle.basic", "centre");
            Has("math.circle.tangent", "centre", "contact");
            Has("math.circle.thales", "A", "B", "C", "centre");
            Has("math.circle.unit", "origin", "P");
            Has("math.construct.perpendicular", "A", "B", "M");
            Has("math.lines.parallel", "s1", "s2");
            Has("math.vector.sum", "sum");
            Has("math.angle.mark", "vertex");
        }

        [Fact]
        public void AStyleOverrideRecoloursStrokesTextsAndPoints()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "math.shape.triangle.incircle", "math.vector.sum", "math.circle.unit", "math.graph.function" })
            {
                var o = Fx.Item(r, id) with { Style = new Style { Stroke = "#ff0000" } };
                var prims = Prim.Flatten(r.RenderLocal(o, Fx.Ctx())).ToList();
                foreach (var p in prims)
                {
                    switch (p)
                    {
                        case TextPrim t: Assert.Equal("#ff0000", t.TextStyle.Color); break;
                        case LinePrim l: Assert.Equal("#ff0000", l.Style.Stroke); break;
                        case PathPrim pa: Assert.Equal("#ff0000", pa.Style.Stroke); break;
                        case EllipsePrim e:
                            Assert.Equal("#ff0000", e.Style.Stroke);
                            if (e.Tag == MathTags.Dot) Assert.Equal("#ff0000", e.Style.Fill!.Color);
                            break;
                    }
                }
            }
        }

        [Fact]
        public void AFillOverrideFillsTheOutlineOnly()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.triangle.incircle") with { Style = new Style { Fill = Fill.Solid("#ffee88") } };
            var prims = Prim.Flatten(r.RenderLocal(o, Fx.Ctx())).ToList();
            var outline = prims.OfType<PathPrim>().Single(p => p.Tag == MathTags.Outline);
            Assert.Equal("#ffee88", outline.Style.Fill!.Color);
            Assert.Equal(0, prims.IndexOf(outline)); // drawn first, so the fill never hides constructions
            Assert.All(prims.OfType<EllipsePrim>().Where(e => e.Tag != MathTags.Dot), e => Assert.True(e.Style.Fill is null || e.Style.Fill.Kind == FillKind.None));
        }
    }
}
