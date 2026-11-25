using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new MapsModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        /// <summary>All (library item, view) combinations of box types.</summary>
        public static IEnumerable<(LibraryItem Item, ObjectType Type, string View)> BoxItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                if (t.Geometry != GeometryKind.Box) continue;
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        /// <summary>The item's object switched to <paramref name="view"/> the way hosts switch views (keeps the scale).</summary>
        public static SketchObject InView(ObjectType type, SketchObject o, string view)
            => o.View == view ? o : type.WithView(o, view, Ctx());

        public static void AssertFinite(IEnumerable<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN/Infinity");
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var b = Prim.Bounds(prims);
            Assert.True(b.HasValue, what + ": no bounds");
            var r = b!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static (SketchDocument Doc, Page Page) Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, objects);
            return (doc, doc.Pages[0]);
        }

        public static SketchObject Item(Registry r, string id, Vec2 at) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static SketchObject Line(Registry r, string id, params Vec2[] pts) => Item(r, id, Vec2.Zero) with { Points = pts };
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] ExpectedTypes =
        {
            "map.frame", "map.region", "map.place", "map.road", "map.rail", "map.river", "map.route", "map.marker", "map.stop", "map.callout",
            "map.poi", "map.scalebar", "map.north", "map.compass", "map.legend",
        };

        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var r = Fx.Reg();
            var m = Assert.Single(r.Modules);
            Assert.Equal("maps", m.Id);
            Assert.Equal("Karten & Routen", m.Name.De);
            var ids = r.Types.Select(t => t.Id).ToList();
            Assert.Equal(ExpectedTypes.OrderBy(x => x), ids.OrderBy(x => x));
            Assert.All(ids, id => Assert.StartsWith("map.", id));
            Assert.IsAssignableFrom<IExportProvider>(m);
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasViewIcons()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            foreach (var c in r.Categories) Assert.False(string.IsNullOrEmpty(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.NotEmpty(t.Views);
                foreach (var v in t.Views)
                {
                    Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                    Assert.NotNull(v.Icon);
                    Assert.Contains(v.Icon, IconIds.All);
                    Assert.Equal(v.Icon, IconIds.ForView(v));
                }
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrEmpty(opt.Label.De), t.Id + "/" + p.Key + "/" + opt.Value);
                }
                foreach (var a in t.Actions(r.Create(r.Library.First(i => i.TypeId == t.Id), new Vec2(50, 50)), Fx.Ctx()))
                {
                    Assert.False(string.IsNullOrEmpty(a.Label.De), t.Id + "/" + a.Id);
                    Assert.Contains(a.Icon, IconIds.All);
                    if (a.Input is not null) Assert.False(string.IsNullOrEmpty(a.Input.Label.De), t.Id + "/" + a.Id);
                }
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
                Assert.StartsWith("map.", item.Id);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void MultiViewTypesUseAbstractIcons()
        {
            var r = Fx.Reg();
            var poi = r.Type("map.poi")!;
            Assert.Equal(new[] { IconIds.Symbol, IconIds.Pin }, poi.Views.Select(v => v.Icon));
            Assert.All(r.Types.Where(t => t.Geometry == GeometryKind.Points), t => Assert.Equal(IconIds.Plan, t.Views.Single().Icon));
        }

        [Theory]
        [InlineData("autobahn")]
        [InlineData("motorway")]
        [InlineData("route")]
        [InlineData("wegpunkt")]
        [InlineData("tankstelle")]
        [InlineData("charging")]
        [InlineData("krankenhaus")]
        [InlineData("flughafen")]
        [InlineData("hafen")]
        [InlineData("bahnhof")]
        [InlineData("maßstab")]
        [InlineData("scale bar")]
        [InlineData("nordpfeil")]
        [InlineData("windrose")]
        [InlineData("legende")]
        [InlineData("fluss")]
        [InlineData("sperrzone")]
        [InlineData("hauptstadt")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                // A bare object without any parameters must render (quick sketch).
                var o = t.Geometry == GeometryKind.Points
                    ? new SketchObject { Id = "x", Type = t.Id, Points = t.DefaultPoints.ToList() }
                    : new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
        }

        [Fact]
        public void CompanionsAreRegisteredAndOfferedInTheInsertMenu()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types) foreach (var c in t.Companions) Assert.NotNull(r.Type(c));
            var route = Fx.Item(r, "map.route", new Vec2(100, 100));
            var routeSuggestions = r.Suggestions(route).Select(i => i.TypeId).ToList();
            Assert.Contains("map.marker", routeSuggestions);
            Assert.Contains("map.poi", routeSuggestions);
            Assert.Contains("map.callout", routeSuggestions);
            var road = Fx.Item(r, "map.road.main", new Vec2(100, 100));
            var roadSuggestions = r.Suggestions(road).Select(i => i.TypeId).ToList();
            Assert.Contains("map.poi", roadSuggestions);
            Assert.Contains("map.marker", roadSuggestions);
        }
    }
}
