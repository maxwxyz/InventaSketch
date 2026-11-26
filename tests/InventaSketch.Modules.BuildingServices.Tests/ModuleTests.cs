using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new BuildingServicesModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

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

        public static IReadOnlyList<Anchor> Anchors(Registry r, SketchObject o, AnchorRole role)
            => r.TypeOf(o).Anchors(o, Ctx()).Where(a => a.Role == role).ToList();

        public static SketchObject Item(Registry r, string id, Vec2 at = default) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static IEnumerable<BsBoxType> BoxTypes(Registry r) => r.Types.OfType<BsBoxType>();
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] AllTypeIds =
        {
            "bs.pipe", "bs.duct",
            "bs.heat.boiler", "bs.heat.heatpump", "bs.heat.radiator", "bs.heat.floorloop", "bs.heat.manifold", "bs.heat.pump",
            "bs.heat.mixer", "bs.heat.expansion", "bs.heat.trv",
            "bs.valve.ball", "bs.valve.check", "bs.valve.safety", "bs.strainer", "bs.meter.heat", "bs.meter.water",
            "bs.heat.buffer", "bs.heat.separator", "bs.heat.solarthermal",
            "bs.san.wc", "bs.san.basin", "bs.san.shower", "bs.san.bathtub", "bs.san.heater", "bs.san.drain", "bs.san.riser",
            "bs.vent.terminal", "bs.vent.unit", "bs.vent.damper", "bs.vent.firedamper", "bs.vent.silencer",
            "bs.cool.split",
            "bs.elec.data", "bs.elec.detector", "bs.elec.appliance", "bs.elec.bonding",
        };

        [Fact]
        public void RegistersAllTypesAndCategories()
        {
            var r = Fx.Reg();
            Assert.Equal(AllTypeIds.OrderBy(x => x), r.Types.Select(t => t.Id).OrderBy(x => x));
            Assert.Equal(5, r.Categories.Count);
            Assert.All(r.Types, t => Assert.Contains(r.Categories, c => c.Id == t.Category));
            Assert.All(r.Library, i => Assert.Contains(r.Categories, c => c.Id == i.Category));
        }

        [Fact]
        public void TypeIdsFollowTheModuleConvention()
        {
            var r = Fx.Reg();
            Assert.All(r.Types, t => Assert.StartsWith("bs.", t.Id));
            Assert.All(r.Library, i => Assert.StartsWith("bs.", i.Id));
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void EverythingIsBilingual()
        {
            var r = Fx.Reg();
            var m = new BuildingServicesModule();
            Assert.False(string.IsNullOrWhiteSpace(m.Name.De));
            foreach (var c in r.Categories) Assert.False(string.IsNullOrWhiteSpace(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Name.De), t.Id);
                Assert.NotEmpty(t.Keywords);
                foreach (var v in t.Views) Assert.False(string.IsNullOrWhiteSpace(v.Label.De), t.Id + " view " + v.Id);
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrWhiteSpace(p.Label.De), t.Id + "." + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrWhiteSpace(opt.Label.De), t.Id + "." + p.Key + "=" + opt.Value);
                }
                var o = t.Create(Vec2.Zero);
                foreach (var a in t.Actions(o, Lang.En)) Assert.False(string.IsNullOrWhiteSpace(a.Label.De), t.Id + " action " + a.Id);
            }
            foreach (var i in r.Library) Assert.False(string.IsNullOrWhiteSpace(i.Name.De), i.Id);
        }

        [Fact]
        public void EveryElementOffersPlanSectionAndSchematicWithSharedPictograms()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                Assert.Equal(new[] { "plan", "section", "schematic" }, t.Views.Select(v => v.Id));
                Assert.Equal(IconIds.Plan, IconIds.ForView(t.Views[0]));
                Assert.Equal(IconIds.Front, IconIds.ForView(t.Views[1]));
                Assert.Equal(IconIds.Schematic, IconIds.ForView(t.Views[2]));
                Assert.Equal("plan", t.DefaultView);
                Assert.True(ServiceViews.TakesPart(t), t.Id);
            }
        }

        [Fact]
        public void EveryElementUsesThePlanScaleAndHasAMountingHeight()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                Assert.True(t.UsesPlanScale, t.Id);
                Assert.Contains(t.Parameters, p => p.Key == "height" && p.Default?.Number is not null && p.Unit == "mm");
                Assert.Contains(t.Parameters, p => p.Key == "scale" && p.Default is null && p.Advanced);
            }
        }

        [Fact]
        public void EveryBoxElementHasSectionAndSchematicPresets()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                Assert.Contains(r.Library, i => i.Id == t.Id && i.View is null);
                var section = r.Item(t.Id + ".section")!;
                Assert.Equal("section", section.View);
                Assert.Equal(t.PreferredSize("section"), section.Size);
                Assert.Equal("schematic", r.Item(t.Id + ".schematic")!.View);
            }
        }

        [Theory]
        [InlineData("heizkörper", "bs.heat.radiator")]
        [InlineData("fußbodenheizung", "bs.heat.floorloop")]
        [InlineData("zirkulation", "bs.pipe.circulation")]
        [InlineData("zuluft", "bs.duct.supply")]
        [InlineData("wärmerückgewinnung", "bs.vent.unit")]
        [InlineData("rauchmelder", "bs.elec.detector")]
        [InlineData("fallleitung", "bs.san.riser.waste")]
        [InlineData("heat pump", "bs.heat.heatpump")]
        [InlineData("kugelhahn", "bs.valve.ball")]
        [InlineData("rückschlagventil", "bs.valve.check")]
        [InlineData("sicherheitsventil", "bs.valve.safety")]
        [InlineData("schmutzfänger", "bs.strainer")]
        [InlineData("wärmemengenzähler", "bs.meter.heat")]
        [InlineData("wasserzähler", "bs.meter.water")]
        [InlineData("pufferspeicher", "bs.heat.buffer")]
        [InlineData("solar collector", "bs.heat.solarthermal")]
        [InlineData("brandschutzklappe", "bs.vent.firedamper")]
        [InlineData("schalldämpfer", "bs.vent.silencer")]
        [InlineData("klappe", "bs.vent.damper")]
        [InlineData("split", "bs.cool.split")]
        public void SearchFindsElementsInBothLanguages(string query, string itemId)
            => Assert.Contains(Fx.Reg().Search(query), i => i.Id == itemId);

        [Fact]
        public void ElectricalElementsOfTheElectricalModuleAreNotDuplicated()
        {
            // Socket, switch, light, junction box, distribution board and cable route live in the electrical module.
            var r = Fx.Reg();
            foreach (var word in new[] { "socket", "switch", "luminaire", "junction", "distribution board", "cable route" })
                Assert.DoesNotContain(r.Types, t => t.Name.En.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
