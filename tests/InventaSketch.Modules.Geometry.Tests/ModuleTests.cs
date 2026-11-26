using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new GeometryModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var r = Prim.Bounds(prims)!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static void AssertFinite(IReadOnlyList<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        public static SketchObject Item(Registry r, string id, Vec2 at = default) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static IReadOnlyList<Primitive> Render(Registry r, SketchObject o, Lang lang = Lang.En) => r.TypeOf(o).Render(o, Ctx(lang));

        public static IEnumerable<Primitive> Tagged(Registry r, SketchObject o, string tag, Lang lang = Lang.En)
            => Prim.Flatten(Render(r, o, lang)).Where(p => p.Tag == tag);

        public static IEnumerable<string> Texts(Registry r, SketchObject o, Lang lang = Lang.En)
            => Prim.Flatten(Render(r, o, lang)).OfType<TextPrim>().Select(t => t.Text);

        public static Anchor Anchor(Registry r, SketchObject o, string id)
            => r.TypeOf(o).Anchors(o, Ctx()).First(a => a.Id == id);

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Geometry, text and style signature of the drawing and the anchors (detects any visible change).</summary>
        public static string Signature(Registry r, SketchObject o)
        {
            var t = r.TypeOf(o);
            var sb = new StringBuilder();
            foreach (var p in Prim.Flatten(t.Render(o, Ctx())))
            {
                sb.Append(p.GetType().Name).Append(':').Append(p.Tag).Append('[');
                foreach (var pt in Prim.Points(p)) sb.Append(F(pt.X)).Append(',').Append(F(pt.Y)).Append(' ');
                sb.Append(']');
                if (p is TextPrim tp) sb.Append('"').Append(tp.Text).Append('"').Append(tp.TextStyle.Bold);
                var style = p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, RectPrim re => re.Style, EllipsePrim e => e.Style, _ => null };
                if (style is not null) sb.Append(style.Stroke).Append('/').Append(style.Fill?.Color).Append('/').Append(style.Dash);
                sb.Append('|');
            }
            foreach (var a in t.Anchors(o, Ctx())) sb.Append(a.Role).Append(a.Id).Append(F(a.P.X)).Append(',').Append(F(a.P.Y)).Append(';');
            return sb.ToString();
        }

        /// <summary>A sample value for a text input of an action.</summary>
        public static string SampleInput(ContextAction a) => a.Id switch
        {
            var id when id.EndsWith("range", StringComparison.Ordinal) => "-3;7",
            "math.graph.coefficients" => "2; 1; 1; -3",
            "math.angle.set" => "30",
            var id when id.EndsWith(".angle", StringComparison.Ordinal) => "50",
            _ => "Q",
        };
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] AllTypeIds =
        {
            "math.axes.cartesian", "math.axes.number", "math.axes.spatial", "math.axes.polar",
            "math.graph.function",
            "math.point.labelled", "math.vector.arrow", "math.vector.sum",
            "math.angle.mark",
            "math.shape.triangle", "math.shape.polygon", "math.circle.basic", "math.circle.tangent", "math.circle.thales", "math.circle.unit",
            "math.construct.perpendicular", "math.construct.anglebisector", "math.lines.parallel",
            "math.arc.sector", "math.shape.pythagoras",
            "math.solid.cube", "math.solid.cylinder", "math.solid.cone", "math.solid.sphere", "math.solid.pyramid",
            "math.venn.diagram", "math.tree.probability",
            "math.free.triangle", "math.free.polygon", "math.free.bisector", "math.free.anglebisector", "math.free.tangents", "math.conic.section",
        };

        [Fact]
        public void RegistersAllTypesAndCategories()
        {
            var r = Fx.Reg();
            Assert.Equal(AllTypeIds.OrderBy(x => x), r.Types.Select(t => t.Id).OrderBy(x => x));
            Assert.Equal(8, r.Categories.Count);
            Assert.All(r.Types, t => Assert.Contains(r.Categories, c => c.Id == t.Category));
            Assert.All(r.Library, i => Assert.Contains(r.Categories, c => c.Id == i.Category));
            Assert.All(r.Categories, c => Assert.Contains(r.Types, t => t.Category == c.Id));
            var m = new GeometryModule();
            Assert.Equal("math", m.Id);
            Assert.Equal("Math & geometry", m.Name.En);
            Assert.Equal("Mathematik & Geometrie", m.Name.De);
            Assert.False(string.IsNullOrWhiteSpace(m.Version));
            Assert.InRange(r.Library.Count, 20, 80);
        }

        [Fact]
        public void TypeIdsFollowTheModuleConvention()
        {
            var r = Fx.Reg();
            Assert.All(r.Types, t => Assert.Matches(@"^math\.[a-z0-9]+\.[a-z0-9]+$", t.Id));
            Assert.All(r.Library, i => Assert.StartsWith("math.", i.Id));
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
            // Every type has a default item with the type id (conversions and suggestions rely on it).
            Assert.All(r.Types, t => Assert.Contains(r.Library, i => i.Id == t.Id && i.TypeId == t.Id));
        }

        [Fact]
        public void FiguresAreNotPartsOfAProduct()
        {
            var r = Fx.Reg();
            Assert.All(r.Types, t => Assert.False(t.IsPart, t.Id));
        }

        [Fact]
        public void EverythingIsBilingual()
        {
            var r = Fx.Reg();
            foreach (var c in r.Categories) Assert.False(string.IsNullOrWhiteSpace(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Name.De), t.Id);
                Assert.NotEqual(t.Name.En, t.Name.De);
                Assert.NotEmpty(t.Keywords);
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrWhiteSpace(p.Label.De), t.Id + "." + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrWhiteSpace(opt.Label.De), t.Id + "." + p.Key + "=" + opt.Value);
                }
                var o = t.Create(Vec2.Zero);
                foreach (var a in t.Actions(o, Fx.Ctx()))
                {
                    Assert.False(string.IsNullOrWhiteSpace(a.Label.De), t.Id + " action " + a.Id);
                    if (a.Input is not null) Assert.False(string.IsNullOrWhiteSpace(a.Input.Label.De), t.Id + " input " + a.Id);
                }
            }
            foreach (var i in r.Library) Assert.False(string.IsNullOrWhiteSpace(i.Name.De), i.Id);
        }

        [Fact]
        public void EveryTypeIsInsertableWithoutParameters()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Create(new Vec2(10, 20));
                Assert.Equal(t.Id, o.Type);
                Assert.All(t.Parameters.Where(p => p.Default.HasValue), p => Assert.True(o.Params.ContainsKey(p.Key), t.Id + "." + p.Key));
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
        }

        [Fact]
        public void EveryLibraryItemResolvesToARegisteredTypeAndValidParameters()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId);
                Assert.NotNull(t);
                foreach (var kv in item.Params ?? new Dictionary<string, ParamValue>())
                {
                    var def = t!.Parameters.SingleOrDefault(p => p.Key == kv.Key);
                    Assert.True(def is not null, item.Id + ": unknown parameter " + kv.Key);
                    if (def!.Kind == ParamKind.Choice) Assert.Contains(def.Options!, o => o.Value == kv.Value.Text);
                    if (def.Kind is ParamKind.Number or ParamKind.Integer)
                        Assert.True(kv.Value.Number >= (def.Min ?? double.MinValue) && kv.Value.Number <= (def.Max ?? double.MaxValue), item.Id + "." + kv.Key);
                    if (def.Kind == ParamKind.Boolean) Assert.NotNull(kv.Value.Bool);
                }
                if (t!.Geometry == GeometryKind.Box) Assert.NotNull(item.Size);
                else Assert.Null(item.Size);
                if (item.Points is not null) Assert.True(item.Points.Count >= t.DefaultPoints.Count, item.Id + ": too few points");
            }
        }

        [Theory]
        [InlineData("koordinatensystem", "math.axes.cartesian")]
        [InlineData("erster quadrant", "math.axes.cartesian.quadrant")]
        [InlineData("Zahlenstrahl", "math.axes.number")]
        [InlineData("schrägbild", "math.axes.spatial")]
        [InlineData("isometric", "math.axes.spatial.isometric")]
        [InlineData("polarkoordinaten", "math.axes.polar")]
        [InlineData("parabel", "math.graph.function")]
        [InlineData("sinus", "math.graph.function.sine")]
        [InlineData("lineare funktion", "math.graph.function.linear")]
        [InlineData("exponential", "math.graph.function.exponential")]
        [InlineData("Punkt", "math.point.labelled")]
        [InlineData("Vektor", "math.vector.arrow")]
        [InlineData("ortsvektor", "math.vector.arrow.position")]
        [InlineData("komponenten", "math.vector.arrow.components")]
        [InlineData("parallelogramm", "math.vector.sum")]
        [InlineData("head to tail", "math.vector.sum.tail")]
        [InlineData("rechter winkel", "math.angle.mark.right")]
        [InlineData("gradzahl", "math.angle.mark.value")]
        [InlineData("dreieck", "math.shape.triangle")]
        [InlineData("pythagoras", "math.shape.triangle.right")]
        [InlineData("inkreis", "math.shape.triangle.incircle")]
        [InlineData("umkreis", "math.shape.triangle.circumcircle")]
        [InlineData("sechseck", "math.shape.polygon")]
        [InlineData("durchmesser", "math.circle.basic")]
        [InlineData("tangente", "math.circle.tangent")]
        [InlineData("Thaleskreis", "math.circle.thales")]
        [InlineData("einheitskreis", "math.circle.unit")]
        [InlineData("mittelsenkrechte", "math.construct.perpendicular")]
        [InlineData("winkelhalbierende", "math.construct.anglebisector")]
        [InlineData("stufenwinkel", "math.lines.parallel")]
        [InlineData("kreisausschnitt", "math.arc.sector")]
        [InlineData("kreisabschnitt", "math.arc.sector.segment")]
        [InlineData("satz des pythagoras", "math.shape.pythagoras")]
        [InlineData("würfel", "math.solid.cube")]
        [InlineData("quader", "math.solid.cuboid")]
        [InlineData("zylinder", "math.solid.cylinder")]
        [InlineData("kegel", "math.solid.cone")]
        [InlineData("kugel", "math.solid.sphere")]
        [InlineData("pyramid", "math.solid.pyramid")]
        [InlineData("schrägbild", "math.solid.cube")]
        [InlineData("mengendiagramm", "math.venn.diagram")]
        [InlineData("venn", "math.venn.diagram.three")]
        [InlineData("baumdiagramm", "math.tree.probability")]
        [InlineData("probability tree", "math.tree.probability.dice")]
        [InlineData("kosinussatz", "math.shape.triangle.values")]
        [InlineData("law of sines", "math.shape.triangle.values")]
        [InlineData("dreieck durch drei punkte", "math.free.triangle")]
        [InlineData("umkreis", "math.free.triangle.circles")]
        [InlineData("schwerpunkt", "math.free.polygon")]
        [InlineData("mittelsenkrechte", "math.free.bisector")]
        [InlineData("winkelhalbierende", "math.free.anglebisector")]
        [InlineData("tangenten", "math.free.tangents")]
        [InlineData("ellipse", "math.conic.section")]
        [InlineData("hyperbel", "math.conic.section.hyperbola")]
        [InlineData("leitlinie", "math.conic.section.parabola")]
        public void SearchFindsElementsInBothLanguages(string query, string itemId)
            => Assert.Contains(Fx.Reg().Search(query), i => i.Id == itemId);

        [Fact]
        public void EveryActionUsesAKnownIconAndRunsWithoutErrors()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var doc = SketchDocument.Create();
                var o = r.Create(item, new Vec2(100, 80));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                var t = r.TypeOf(o);
                foreach (var a in t.Actions(o, new RenderContext(doc)))
                {
                    Assert.Contains(a.Icon, IconIds.All);
                    if (!a.IsEnabled) continue;
                    var input = a.Input is null ? null : Fx.SampleInput(a);
                    var after = a.Run(doc, o, new RenderContext(doc), input);
                    var changed = after.Get(o.Id)!;
                    var prims = t.Render(changed, new RenderContext(after));
                    Assert.NotEmpty(prims);
                    if (!changed.IsPointGeometry) Fx.AssertInsideBox(prims, changed.Size, item.Id + " after " + a.Id);
                }
            }
        }
    }
}
