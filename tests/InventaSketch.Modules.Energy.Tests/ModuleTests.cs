using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new EnergyModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        /// <summary>The item in a view, switched like a host does (size adapted to the view).</summary>
        public static SketchObject InView(Registry r, LibraryItem item, string view)
            => r.WithView(r.Create(item, Vec2.Zero), view, Ctx());

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

        public static IEnumerable<EnergyType> BoxTypes(Registry r) => r.Types.OfType<EnergyType>();

        public static IEnumerable<string> Texts(Registry r, SketchObject o, Lang lang = Lang.En)
            => r.TypeOf(o).Render(o, Ctx(lang)).OfType<TextPrim>().Select(t => t.Text);

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Geometry, text and colour signature of the drawing and the anchors (detects any visible change).</summary>
        public static string Signature(Registry r, SketchObject o)
        {
            var t = r.TypeOf(o);
            var sb = new StringBuilder();
            foreach (var p in Prim.Flatten(t.Render(o, Ctx())))
            {
                sb.Append(p.GetType().Name).Append(':').Append(p.Tag).Append('[');
                foreach (var pt in Prim.Points(p)) sb.Append(F(pt.X)).Append(',').Append(F(pt.Y)).Append(' ');
                sb.Append(']');
                if (p is TextPrim tp) sb.Append('"').Append(tp.Text).Append('"');
                var style = p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, RectPrim re => re.Style, EllipsePrim e => e.Style, _ => null };
                if (style is not null) sb.Append(style.Stroke).Append('/').Append(style.Fill?.Color).Append('/').Append(style.Dash);
                sb.Append('|');
            }
            foreach (var a in t.Anchors(o, Ctx())) sb.Append(a.Role).Append(a.Id).Append(F(a.P.X)).Append(',').Append(F(a.P.Y)).Append(';');
            return sb.ToString();
        }
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] AllTypeIds =
        {
            "energy.pv.module", "energy.pv.string", "energy.pv.array", "energy.wind.turbine", "energy.gen.genset", "energy.hydro.turbine",
            "energy.conv.inverter", "energy.conv.converter", "energy.storage.battery", "energy.storage.pumped",
            "energy.heat.heatpump", "energy.heat.chp", "energy.heat.storage", "energy.h2.electrolyser", "energy.h2.fuelcell", "energy.h2.tank",
            "energy.mobility.charger", "energy.mobility.ev", "energy.load.building", "energy.control.ems",
            "energy.grid.infeed", "energy.grid.transformer", "energy.grid.transformer3", "energy.grid.substation", "energy.grid.meter",
            "energy.grid.busbar", "energy.grid.breaker", "energy.grid.disconnector", "energy.grid.earthing",
            "energy.line.cable", "energy.line.pipe", "energy.line.flow",
        };

        [Fact]
        public void RegistersAllTypesAndCategories()
        {
            var r = Fx.Reg();
            Assert.Equal(AllTypeIds.OrderBy(x => x), r.Types.Select(t => t.Id).OrderBy(x => x));
            Assert.Equal(6, r.Categories.Count);
            Assert.All(r.Types, t => Assert.Contains(r.Categories, c => c.Id == t.Category));
            Assert.All(r.Library, i => Assert.Contains(r.Categories, c => c.Id == i.Category));
            Assert.All(r.Categories, c => Assert.Contains(r.Types, t => t.Category == c.Id));
            var m = new EnergyModule();
            Assert.Equal("energy", m.Id);
            Assert.False(string.IsNullOrWhiteSpace(m.Version));
        }

        [Fact]
        public void TypeIdsFollowTheModuleConvention()
        {
            var r = Fx.Reg();
            // <module>.<family>.<variant>
            Assert.All(r.Types, t => Assert.Matches(@"^energy\.[a-z0-9]+\.[a-z0-9]+$", t.Id));
            Assert.All(r.Library, i => Assert.StartsWith("energy.", i.Id));
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void EverythingIsBilingual()
        {
            var r = Fx.Reg();
            var m = new EnergyModule();
            Assert.False(string.IsNullOrWhiteSpace(m.Name.De));
            foreach (var c in r.Categories) Assert.False(string.IsNullOrWhiteSpace(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Name.De), t.Id);
                Assert.NotEqual(t.Name.En, t.Name.De);
                Assert.NotEmpty(t.Keywords);
                foreach (var v in t.Views) Assert.False(string.IsNullOrWhiteSpace(v.Label.De), t.Id + " view " + v.Id);
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrWhiteSpace(p.Label.De), t.Id + "." + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrWhiteSpace(opt.Label.De), t.Id + "." + p.Key + "=" + opt.Value);
                }
                var o = t.Create(Vec2.Zero);
                foreach (var a in t.Actions(o, Lang.En))
                {
                    Assert.False(string.IsNullOrWhiteSpace(a.Label.De), t.Id + " action " + a.Id);
                    if (a.Input is not null) Assert.False(string.IsNullOrWhiteSpace(a.Input.Label.De), t.Id + " input " + a.Id);
                }
            }
            foreach (var i in r.Library) Assert.False(string.IsNullOrWhiteSpace(i.Name.De), i.Id);
        }

        [Fact]
        public void EveryTypeOffersThePictorialAndTheSingleLineView()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                Assert.Equal(new[] { "pictorial", "single-line" }, t.Views.Select(v => v.Id));
                Assert.Equal(IconIds.Front, IconIds.ForView(t.Views[0]));
                Assert.Equal(IconIds.Symbol, IconIds.ForView(t.Views[1]));
                Assert.Contains(t.DefaultView, EnViews.All);
            }
            // Switchgear starts in the single-line view, devices in the pictorial view.
            Assert.Equal("single-line", r.Type("energy.grid.breaker")!.DefaultView);
            Assert.Equal("single-line", r.Type("energy.grid.busbar")!.DefaultView);
            Assert.Equal("pictorial", r.Type("energy.pv.module")!.DefaultView);
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
        public void EveryBoxTypeHasAPresetInItsOtherView()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                Assert.Contains(r.Library, i => i.Id == t.Id && i.View is null);
                var other = t.DefaultViewId == "pictorial" ? "single-line" : "pictorial";
                var item = r.Item(t.Id + (other == "single-line" ? ".sld" : ".pictorial"))!;
                Assert.Equal(other, item.View);
                Assert.Equal(t.Spec(other).Design, item.Size);
                Assert.Equal(other, r.Create(item, Vec2.Zero).View);
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
                }
                if (item.View is not null) Assert.Contains(t!.Views, v => v.Id == item.View);
            }
        }

        [Theory]
        [InlineData("photovoltaik", "energy.pv.module")]
        [InlineData("Wechselrichter", "energy.conv.inverter")]
        [InlineData("speicher", "energy.storage.battery")]
        [InlineData("Ladestation", "energy.mobility.charger")]
        [InlineData("netzanschluss", "energy.grid.infeed")]
        [InlineData("Elektrolyseur", "energy.h2.electrolyser")]
        [InlineData("brennstoffzelle", "energy.h2.fuelcell")]
        [InlineData("einlinienschaltbild", "energy.grid.busbar")]
        [InlineData("einlinienschaltbild", "energy.grid.transformer")]
        [InlineData("trafo", "energy.grid.transformer")]
        [InlineData("wind turbine", "energy.wind.turbine")]
        [InlineData("wallbox", "energy.mobility.charger")]
        [InlineData("bhkw", "energy.heat.chp")]
        [InlineData("wärmepumpe", "energy.heat.heatpump")]
        [InlineData("leistungsschalter", "energy.grid.breaker")]
        [InlineData("trennschalter", "energy.grid.disconnector")]
        [InlineData("mittelspannung", "energy.line.cable.mv")]
        [InlineData("wasserstoff", "energy.line.pipe.hydrogen")]
        [InlineData("solarpark", "energy.pv.park")]
        [InlineData("hausanschluss", "energy.grid.infeed.lv")]
        [InlineData("Wasserkraft", "energy.hydro.turbine")]
        [InlineData("hydropower", "energy.hydro.turbine")]
        [InlineData("pumpspeicher", "energy.storage.pumped")]
        [InlineData("pumped storage", "energy.storage.pumped")]
        public void SearchFindsElementsInBothLanguages(string query, string itemId)
            => Assert.Contains(Fx.Reg().Search(query), i => i.Id == itemId);

        [Fact]
        public void CircuitSymbolsOfTheElectricalModuleAreNotDuplicated()
        {
            // Resistors, capacitors, diodes, transistors, sockets, light switches, fuses and plain wires live in the
            // electrical module; this module only adds system-level energy elements.
            var r = Fx.Reg();
            foreach (var word in new[] { "resistor", "capacitor", "diode", "transistor", "socket", "light switch", "fuse", "wire", "inductor", "lamp" })
                Assert.DoesNotContain(r.Types, t => t.Name.En.Equals(word, StringComparison.OrdinalIgnoreCase));
            Assert.All(r.Types, t => Assert.DoesNotContain("elec.", t.Id));
        }
    }
}
