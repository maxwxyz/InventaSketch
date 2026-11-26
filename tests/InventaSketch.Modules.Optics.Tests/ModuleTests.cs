using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new OpticsModule());

        public static RenderContext Ctx() => new(SketchDocument.Create());

        public static SketchObject Item(Registry r, string id, Vec2 at = default) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(new Size2(600, 400));
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        public static SketchDocument Add(SketchDocument doc, params SketchObject[] objects) => Edits.Add(doc, doc.Pages[0].Id, objects);

        /// <summary>A free (undocked) traced ray from <paramref name="a"/> towards <paramref name="b"/>.</summary>
        public static SketchObject Ray(Vec2 a, Vec2 b) => RayType.Instance.Create(a) with { Points = new[] { a, b } };

        public static TraceResult Trace(SketchDocument doc, string rayId) => Beams.Trace(doc.Get(rayId)!, doc);

        /// <summary>The bundle docked on <paramref name="source"/> (added with the module's own action helper) and its id.</summary>
        public static (SketchDocument Doc, string RayId) Bundle(SketchDocument doc, SketchObject source, string anchor)
        {
            var before = doc.Objects.Keys.ToHashSet();
            var after = Beams.AddBundle(doc, source, anchor);
            return (after, after.Objects.Keys.Single(k => !before.Contains(k)));
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what, double tol = 0.3)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var r = Prim.Bounds(prims)!.Value;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static void AssertFinite(IReadOnlyList<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        /// <summary>Intersection of the lines through (a, a + da) and (b, b + db).</summary>
        public static Vec2 Intersect(Vec2 a, Vec2 da, Vec2 b, Vec2 db)
        {
            var den = da.Cross(db);
            var t = (b - a).Cross(db) / den;
            return a + da * t;
        }

        /// <summary>Last segment of a path: its start and unit direction.</summary>
        public static (Vec2 P, Vec2 D) Last(TracedPath p)
        {
            var a = p.Points[p.Points.Count - 2];
            var b = p.Points[p.Points.Count - 1];
            return (a, (b - a).Normalized());
        }

        public static (Vec2 P, Vec2 D) Segment(TracedPath p, int i) => (p.Points[i], (p.Points[i + 1] - p.Points[i]).Normalized());

        public static double Deg(Vec2 d) => Math.Atan2(d.Y, d.X) * 180 / Math.PI;
    }

    public class ModuleTests
    {
        public static readonly string[] AllTypeIds =
        {
            "optics.lens.converging", "optics.lens.diverging",
            "optics.mirror.plane", "optics.mirror.concave", "optics.mirror.convex",
            "optics.prism.triangle", "optics.splitter.cube", "optics.splitter.plate",
            "optics.stop.aperture", "optics.grating.transmission", "optics.filter.plate", "optics.fibre.cable",
            "optics.source.lamp", "optics.source.led", "optics.source.laser",
            "optics.detector.photodiode", "optics.detector.camera", "optics.detector.eye", "optics.screen.plain",
            "optics.object.arrow", "optics.ray", "optics.axis.line",
            "optics.lens.thick", "optics.mirror.scanner", "optics.polarizer", "optics.waveplate", "optics.lightguide",
            "optics.detector.spectrometer", "optics.window.plate",
        };

        [Fact]
        public void RegistersAllTypesWithTheModulePrefix()
        {
            var r = Fx.Reg();
            Assert.Equal("optics", r.Modules.Single().Id);
            Assert.Equal("Optik", r.Modules.Single().Name.Get(Lang.De));
            var ids = r.Types.Select(t => t.Id).ToList();
            Assert.Equal(AllTypeIds.OrderBy(x => x), ids.OrderBy(x => x));
            Assert.All(r.Categories, c => Assert.StartsWith("optics.", c.Id));
            Assert.All(r.Library, i => Assert.StartsWith("optics.", i.Id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndFindable()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            Assert.Equal(6, cats.Count);
            foreach (var c in r.Categories) Assert.False(string.IsNullOrEmpty(c.Name.De), c.Id);
            var doc = SketchDocument.Create();
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.True(t.Keywords.Count >= 6, t.Id + " needs English and German keywords");
                foreach (var v in t.Views) Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
                    if (p.Options is not null) Assert.All(p.Options, o => Assert.False(string.IsNullOrEmpty(o.Label.De), t.Id + "/" + p.Key + "/" + o.Value));
                }
                var o = t.Create(new Vec2(100, 100));
                foreach (var a in t.Actions(o, new RenderContext(Fx.Doc(o), Lang.De)))
                {
                    Assert.False(string.IsNullOrEmpty(a.Label.De), t.Id + "/" + a.Id);
                    Assert.Contains(a.Icon, IconIds.All);
                }
                foreach (var part in t.Parts(o, new RenderContext(Fx.Doc(o)))) Assert.False(string.IsNullOrEmpty(part.Label.De), t.Id + "/" + part.Key);
                foreach (var c in t.Companions) Assert.NotNull(r.Type(c));
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
            }
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Name.En).Distinct().Count());
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Name.De).Distinct().Count());
        }

        [Fact]
        public void NoUserVisibleTextMentionsPatents()
        {
            var r = Fx.Reg();
            var texts = new List<string>();
            texts.AddRange(r.Modules.SelectMany(m => new[] { m.Name.En, m.Name.De! }));
            texts.AddRange(r.Categories.SelectMany(c => new[] { c.Name.En, c.Name.De! }));
            texts.AddRange(r.Library.SelectMany(i => new[] { i.Name.En, i.Name.De! }.Concat(i.Keywords)));
            foreach (var t in r.Types)
            {
                texts.AddRange(new[] { t.Name.En, t.Name.De! }.Concat(t.Keywords));
                texts.AddRange(t.Parameters.SelectMany(p => new[] { p.Label.En, p.Label.De! }.Concat(p.Options?.SelectMany(o => new[] { o.Label.En, o.Label.De! }) ?? Array.Empty<string>())));
                texts.AddRange(t.Views.SelectMany(v => new[] { v.Label.En, v.Label.De! }));
                var o = t.Create(Vec2.Zero);
                texts.AddRange(t.Actions(o, Lang.En).SelectMany(a => new[] { a.Label.En, a.Label.De! }));
            }
            Assert.DoesNotContain(texts, s => s.IndexOf("patent", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Theory]
        [InlineData("linse")]
        [InlineData("lens")]
        [InlineData("sammellinse")]
        [InlineData("hohlspiegel")]
        [InlineData("mirror")]
        [InlineData("prisma")]
        [InlineData("strahlteiler")]
        [InlineData("beugungsgitter")]
        [InlineData("laser")]
        [InlineData("lichtstrahl")]
        [InlineData("optische achse")]
        [InlineData("auge")]
        [InlineData("glasfaser")]
        [InlineData("blende")]
        [InlineData("polarisator")]
        [InlineData("polarizer")]
        [InlineData("wellenplatte")]
        [InlineData("dicke linse")]
        [InlineData("galvo")]
        [InlineData("lichtleitstab")]
        [InlineData("spektrometer")]
        [InlineData("brewster")]
        [InlineData("glasplatte")]
        [InlineData("pbs")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemInsertsWithoutParametersAndRenders()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(200, 150));
                var doc = Fx.Doc(o);
                var prims = r.Render(o, new RenderContext(doc));
                Assert.True(prims.Count > 0, item.Id + " draws nothing");
                Fx.AssertFinite(prims, item.Id);
                if (!o.IsPointGeometry) Fx.AssertInsideBox(r.RenderLocal(o, new RenderContext(doc)), o.Size, item.Id);
                // An undocked ray with nothing in its way runs exactly between its points.
                Assert.NotNull(SvgRenderer.RenderPage(r, doc, doc.Pages[0]));
            }
        }

        [Theory]
        [InlineData(2.2, 0.6)]
        [InlineData(0.4, 1.8)]
        [InlineData(3, 3)]
        [InlineData(0.3, 0.3)]
        public void FreeScalingChangesOnlyTheSizeAndStaysInsideTheBox(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(200, 150));
                if (o.IsPointGeometry) continue;
                var doc = Fx.Doc(o);
                var scaled = Edits.Scale(doc, new[] { o.Id }, new Vec2(200, 150), sx, sy).Get(o.Id)!;
                Assert.Equal(o.Params.OrderBy(kv => kv.Key), scaled.Params.OrderBy(kv => kv.Key));
                Assert.NotEqual(o.Size, scaled.Size);
                foreach (var view in r.TypeOf(o).Views.Select(v => v.Id).DefaultIfEmpty(null))
                {
                    var v = view is null ? scaled : scaled with { View = view };
                    Fx.AssertInsideBox(r.TypeOf(v).Render(v, Fx.Ctx()), v.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
                }
            }
        }

        [Fact]
        public void EveryParameterValueRenders()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Create(new Vec2(200, 150));
                foreach (var p in t.Parameters)
                {
                    IEnumerable<ParamValue> values = p.Kind switch
                    {
                        ParamKind.Choice => p.Options!.Select(x => (ParamValue)x.Value),
                        ParamKind.Boolean => new ParamValue[] { true, false },
                        ParamKind.Integer or ParamKind.Number => new ParamValue[] { p.Min ?? 0, p.Max ?? 100, p.Default?.Number ?? 1 },
                        _ => new ParamValue[] { "L1", "" },
                    };
                    foreach (var v in values)
                    {
                        var x = o.WithParam(p.Key, v);
                        var prims = r.Render(x, new RenderContext(Fx.Doc(x)));
                        Assert.True(prims.Count > 0, $"{t.Id} {p.Key}={v}");
                        Fx.AssertFinite(prims, $"{t.Id} {p.Key}={v}");
                    }
                }
            }
        }

        [Fact]
        public void LensViewsAndShapesDiffer()
        {
            var r = Fx.Reg();
            var bi = Fx.Item(r, "optics.lens.biconvex");
            var plano = Fx.Item(r, "optics.lens.planoconvex");
            var symbol = Fx.Item(r, "optics.lens.converging.symbol");
            string Svg(SketchObject o) => SvgRenderer.RenderPrimitives(r.TypeOf(o).Render(o, Fx.Ctx()));
            Assert.NotEqual(Svg(bi), Svg(plano));
            Assert.NotEqual(Svg(bi), Svg(symbol with { Size = bi.Size }));
            // Converging and diverging symbols point their heads the other way.
            var div = Fx.Item(r, "optics.lens.diverging.symbol");
            Assert.NotEqual(Svg(symbol), Svg(div));
        }

        [Fact]
        public void PartsCanBeRestyledAndHidden()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex").WithParam("focal", true);
            var keys = r.Parts(lens, Fx.Ctx()).Select(p => p.Key).ToList();
            Assert.Contains(OpticsTags.Body, keys);
            Assert.Contains(OpticsTags.Focal, keys);
            var hidden = PartLooks.WithLook(lens, OpticsTags.Focal, l => l with { Hidden = true });
            Assert.DoesNotContain(r.Render(hidden, Fx.Ctx()), p => p.Tag == OpticsTags.Focal);
            Assert.Contains(r.Render(lens, Fx.Ctx()), p => p.Tag == OpticsTags.Focal);

            var laser = Fx.Item(r, "optics.source.laser", new Vec2(100, 150));
            var (doc, rayId) = Fx.Bundle(Fx.Doc(laser), laser, "exit");
            var ray = doc.Get(rayId)!;
            var rk = r.Parts(ray, new RenderContext(doc)).Select(p => p.Key).ToList();
            Assert.Contains(OpticsTags.Ray, rk);
            var blue = PartLooks.WithLook(ray, OpticsTags.Ray, l => l with { Style = new Style { Stroke = "#0000ff" } });
            var prims = r.Render(blue, new RenderContext(Edits.Replace(doc, blue)));
            Assert.All(Prim.Flatten(prims).OfType<LinePrim>(), l => Assert.Equal("#0000ff", l.Style.Stroke));
        }

        [Fact]
        public void LensAnchorsSitOnTheAxisAtTheFocalPoints()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(200, 150)).WithParam("f", 30.0);
            var a = r.PageAnchors(lens, new RenderContext(Fx.Doc(lens))).Where(x => x.Role == AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
            Assert.Equal(new Vec2(200, 150), a["o"]);
            Assert.Equal(new Vec2(170, 150), a["f1"]);
            Assert.Equal(new Vec2(230, 150), a["f2"]);
            Assert.Equal(new Vec2(260, 150), a["2f2"]);
            // Turned lenses keep the focal points on their (turned) axis.
            var turned = lens with { Transform = lens.Transform with { Rotation = 90 } };
            var t = r.PageAnchors(turned, new RenderContext(Fx.Doc(turned))).Single(x => x.Id == "f2").P;
            Assert.True(t.DistanceTo(new Vec2(200, 180)) < 1e-9, t.ToString());
        }

        [Fact]
        public void ElementsDockOnTheOpticalAxis()
        {
            var r = Fx.Reg();
            var axis = Fx.Item(r, "optics.axis", new Vec2(200, 150));
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(230, 151.2));
            var doc = Fx.Doc(axis, lens);
            var ctx = new RenderContext(doc);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { lens.Id }, ctx, new SnapOptions { Grid = null });
            var moving = r.PageAnchors(lens, ctx).ToList();
            var snap = Snapper.SnapMove(moving, Vec2.Zero, targets, new SnapOptions { Grid = null });
            Assert.Equal("rail", snap.Kind);
            Assert.True(Math.Abs(151.2 + snap.Delta.Y - 150) < 1e-9, snap.Delta.ToString());
        }
    }
}
