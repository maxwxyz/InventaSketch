using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new FluidModule());

        public static RenderContext Ctx() => new(SketchDocument.Create());

        public static IEnumerable<(LibraryItem Item, ObjectType Type, string View)> BoxItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                if (t.Geometry != GeometryKind.Box) continue;
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        /// <summary>The item in a view, switched like a host does (size adapted to the view).</summary>
        public static SketchObject InView(Registry r, LibraryItem item, string view)
        {
            var o = r.Create(item, Vec2.Zero);
            return r.WithView(o, view, Ctx());
        }

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

        public static (SketchDocument Doc, string PageId) Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return (Edits.Add(doc, doc.Pages[0].Id, objects), doc.Pages[0].Id);
        }

        public static Anchor Port(Registry r, SketchDocument doc, SketchObject o, string id)
            => r.PageAnchors(o, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port && a.Id == id);

        public static IReadOnlyList<Anchor> Ports(Registry r, SketchObject o)
            => r.TypeOf(o).Anchors(o, Ctx()).Where(a => a.Role == AnchorRole.Port).ToList();

        public static IReadOnlyList<Primitive> Render(Registry r, SketchObject o) => r.TypeOf(o).Render(o, Ctx());

        public static SketchObject Item(Registry r, string id, Vec2 at = default) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static ContextAction Action(Registry r, SketchObject o, string id) => r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == id);

        /// <summary>The options of choice <paramref name="key"/> ("fluid.medium": one action per value, the current one active).</summary>
        public static List<ContextAction> Options(Registry r, SketchObject o, string key) => r.TypeOf(o).Actions(o, Lang.En).Where(a => a.Choice?.Key == key).ToList();

        /// <summary>The first option of choice <paramref name="key"/> that is not current (what a switch would have switched to).</summary>
        public static ContextAction Other(Registry r, SketchObject o, string key) => Options(r, o, key).First(a => !a.IsActive);
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] AllTypeIds =
        {
            "fluid.pump", "fluid.motor", "fluid.cylinder.double", "fluid.cylinder.single", "fluid.rotary",
            "fluid.valve.directional", "fluid.actuation",
            "fluid.valve.check", "fluid.valve.throttle", "fluid.valve.relief", "fluid.valve.reducing", "fluid.valve.sequence",
            "fluid.valve.shuttle", "fluid.valve.twopressure", "fluid.valve.quickexhaust", "fluid.valve.pilotcheck", "fluid.valve.counterbalance",
            "fluid.suctioncup",
            "fluid.tank", "fluid.source", "fluid.exhaust", "fluid.silencer", "fluid.filter", "fluid.separator", "fluid.lubricator",
            "fluid.cooler", "fluid.heater", "fluid.frl", "fluid.accumulator", "fluid.vacuum.generator",
            "fluid.gauge", "fluid.pressureswitch", "fluid.flowmeter", "fluid.thermometer",
            "fluid.line", "fluid.junction", "fluid.crossing", "fluid.plug", "fluid.coupling", "fluid.enclosure",
        };

        [Fact]
        public void RegistersWithoutCollisions()
        {
            var r = Fx.Reg();
            Assert.Contains(r.Modules, m => m.Id == "fluid");
            Assert.Equal("Fluidtechnik", r.Modules.Single().Name.Get(Lang.De));
        }

        [Fact]
        public void ModuleRegistersAllTypes()
        {
            var ids = Fx.Reg().Types.Select(t => t.Id).ToList();
            foreach (var id in AllTypeIds) Assert.Contains(id, ids);
            Assert.Equal(AllTypeIds.Length, ids.Count);
            Assert.All(ids, id => Assert.StartsWith("fluid.", id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasKeywords()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            Assert.Equal(6, cats.Count);
            foreach (var c in r.Categories) Assert.False(string.IsNullOrEmpty(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.True(t.Keywords.Count >= 4, t.Id + " needs English and German keywords");
                Assert.NotEmpty(t.Views);
                foreach (var v in t.Views) Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
                    if (p.Options is not null) Assert.All(p.Options, o => Assert.False(string.IsNullOrEmpty(o.Label.De), t.Id + "/" + p.Key + "/" + o.Value));
                }
                foreach (var a in t.Actions(t.Create(new Vec2(10, 10)), Lang.De))
                    Assert.False(string.IsNullOrEmpty(a.Label.De), t.Id + "/" + a.Id);
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
                Assert.StartsWith("fluid.", item.Id);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void ViewIconsComeFromIconIds()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var v in t.Views)
                {
                    Assert.Contains(v.Icon, IconIds.All);
                    Assert.Equal(v.Icon, IconIds.ForView(v));
                }
            Assert.Equal(new[] { IconIds.Detailed, IconIds.Simplified }, r.Type("fluid.frl")!.Views.Select(v => v.Icon));
            Assert.Equal(IconIds.Symbol, r.Type("fluid.valve.directional")!.Views.Single().Icon);
            foreach (var t in r.Types)
                foreach (var a in t.Actions(t.Create(Vec2.Zero), Lang.En))
                    Assert.Contains(a.Icon, IconIds.All);
        }

        [Theory]
        [InlineData("wegeventil")]
        [InlineData("4/3")]
        [InlineData("directional valve")]
        [InlineData("zylinder")]
        [InlineData("cylinder")]
        [InlineData("druckbegrenzungsventil")]
        [InlineData("relief")]
        [InlineData("pumpe")]
        [InlineData("compressor")]
        [InlineData("manometer")]
        [InlineData("wartungseinheit")]
        [InlineData("schlauch")]
        [InlineData("rückschlagventil")]
        [InlineData("tank")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Geometry == GeometryKind.Points
                    ? new SketchObject { Id = "x", Type = t.Id, Points = t.DefaultPoints }
                    : new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
                // Without parameters the object renders exactly like a fresh library default.
                var created = t.Create(Vec2.Zero);
                if (t.Geometry == GeometryKind.Box)
                    Assert.Equal(SvgRenderer.RenderPrimitives(t.Render(o, Fx.Ctx())), SvgRenderer.RenderPrimitives(t.Render(created with { Id = "x" }, Fx.Ctx())));
            }
        }

        [Fact]
        public void CompanionsAreRegisteredTypes()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var c in t.Companions) Assert.NotNull(r.Type(c));
            Assert.Contains("fluid.valve.directional", r.Type("fluid.line")!.Companions);
            Assert.Contains("fluid.cylinder.double", r.Type("fluid.line")!.Companions);
            Assert.Contains("fluid.gauge", r.Type("fluid.line")!.Companions);
            Assert.Contains("fluid.valve.directional", r.Type("fluid.cylinder.double")!.Companions);
            Assert.Contains("fluid.valve.throttle", r.Type("fluid.cylinder.double")!.Companions);
            Assert.Contains("fluid.actuation", r.Type("fluid.valve.directional")!.Companions);
            Assert.Contains("fluid.line", r.Type("fluid.valve.directional")!.Companions);

            var line = Fx.Item(r, "fluid.line");
            var suggestions = r.Suggestions(line).Select(i => i.TypeId).ToList();
            Assert.Equal("fluid.valve.directional", suggestions[0]);
        }
    }
}
