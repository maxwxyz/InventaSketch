using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new ProcessModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        /// <summary>The item in a view, switched like a host does.</summary>
        public static SketchObject InView(Registry r, LibraryItem item, string view)
        {
            var o = r.Create(item, Vec2.Zero);
            return r.WithView(o, view, Ctx());
        }

        /// <summary>Primitives without the external equipment tag (it is written outside the box on purpose).</summary>
        public static IReadOnlyList<Primitive> WithoutTag(IReadOnlyList<Primitive> prims, SketchObject o)
            => o.View == ProcessViews.Block ? prims : prims.Where(p => !(p is TextPrim && p.Tag == ProcessTags.Tag && o.Type != "process.instrument.bubble")).ToList();

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

        public static Anchor Port(Registry r, SketchDocument doc, SketchObject o, string id)
            => r.PageAnchors(o, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port && a.Id == id);

        public static ContextAction Action(Registry r, SketchObject o, string id) => r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == id);

        /// <summary>Geometry signature of a rendering (for "the parameter changes the drawing").</summary>
        public static string Signature(IReadOnlyList<Primitive> prims)
            => string.Join("|", Prim.Flatten(prims).Select(p => p.GetType().Name + ":" + p.Tag + ":" + string.Join(";", Prim.Points(p).Select(q => q.ToString()))
                + (p is TextPrim t ? ":" + t.Text : "") + ":" + StyleOf(p)));

        private static string StyleOf(Primitive p)
        {
            var s = p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, RectPrim r => r.Style, _ => null };
            return s is null ? "" : s.EffectiveFill.Kind + "/" + s.EffectiveFill.Color + "/" + s.EffectiveDash + "/" + s.EffectiveWidth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static IEnumerable<ProcessType> BoxTypes(Registry r) => r.Types.OfType<ProcessType>();
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] AllTypeIds =
        {
            "process.vessel.vertical", "process.vessel.horizontal", "process.vessel.tank", "process.column", "process.reactor",
            "process.cyclone", "process.silo", "process.valve.steamtrap", "process.rupturedisc", "process.sightglass",
            "process.hx.shelltube", "process.hx.plate", "process.hx.general", "process.hx.aircooler", "process.hx.coolingtower",
            "process.pump.centrifugal", "process.pump.displacement", "process.compressor", "process.turbine", "process.fan", "process.motor", "process.generator",
            "process.valve.manual", "process.valve.control", "process.valve.check", "process.valve.safety", "process.valve.threeway",
            "process.fitting.reducer", "process.fitting.strainer",
            "process.instrument.bubble", "process.instrument.orifice",
            "process.line", "process.line.signal",
            "process.hvac.coil", "process.hvac.damper", "process.hvac.filter", "process.hvac.recovery", "process.hvac.humidifier",
            "process.chart.ts", "process.chart.pv", "process.chart.ph",
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
        }

        [Fact]
        public void TypeIdsFollowTheModuleConvention()
        {
            var r = Fx.Reg();
            Assert.All(r.Types, t => Assert.Matches(@"^process\.[a-z]+(\.[a-z]+)?$", t.Id));
            Assert.All(r.Library, i => Assert.StartsWith("process.", i.Id));
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void EveryTypeHasADefaultLibraryItemAndEveryItemResolves()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.Id == t.Id && i.TypeId == t.Id);
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId);
                Assert.NotNull(t);
                var o = r.Create(item, new Vec2(10, 10));
                Assert.Equal(item.TypeId, o.Type);
                if (item.Params is not null)
                    foreach (var kv in item.Params)
                    {
                        Assert.Contains(t!.Parameters, p => p.Key == kv.Key);
                        Assert.Equal(kv.Value, o.Params[kv.Key]);
                    }
                if (t!.Geometry == GeometryKind.Box) Assert.NotNull(item.Size);
                else Assert.Null(item.Size);
            }
        }

        [Fact]
        public void EverythingIsBilingual()
        {
            var r = Fx.Reg();
            var m = new ProcessModule();
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
        public void ViewsAndActionsUseTheSharedPictograms()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                Assert.NotEmpty(t.Views);
                foreach (var v in t.Views) Assert.Contains(IconIds.ForView(v), IconIds.All);
                foreach (var a in t.Actions(t.Create(Vec2.Zero), Lang.En)) Assert.Contains(a.Icon, IconIds.All);
                foreach (var p in t.Parameters.Where(p => p.Options is not null))
                    foreach (var opt in p.Options!) Assert.True(opt.Icon is null || IconIds.All.Contains(opt.Icon), t.Id + "." + p.Key);
            }
        }

        [Fact]
        public void EquipmentHasABlockViewAndChartsAChartView()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                Assert.Equal(ProcessViews.Symbol, t.Views[0].Id == ProcessViews.Chart ? ProcessViews.Symbol : t.Views[0].Id);
                var hasBlock = t.Views.Any(v => v.Id == ProcessViews.Block);
                var equipment = t.Category is ProcessCategories.Vessels or ProcessCategories.Heat or ProcessCategories.Machines;
                Assert.True(hasBlock == equipment, t.Id + ": block view only for equipment");
            }
            foreach (var id in new[] { "process.chart.ts", "process.chart.pv", "process.chart.ph" })
            {
                var t = r.Type(id)!;
                Assert.Equal(new[] { ProcessViews.Chart }, t.Views.Select(v => v.Id));
                Assert.Equal(IconIds.Chart, IconIds.ForView(t.Views[0]));
                Assert.False(t.IsPart);
            }
        }

        [Fact]
        public void AllParametersAreOptionalAndHaveDefaults()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var p in t.Parameters)
                {
                    // Engineering values (Calc 4) are optional texts: empty means "not entered".
                    if (p.Kind == ParamKind.Text && ValueModel.Variables(t).Any(v => v.ParamKey == p.Key)) continue;
                    Assert.True(p.Default.HasValue, t.Id + "." + p.Key + " needs a default (Quick Sketch first)");
                    if (p.Kind == ParamKind.Choice) Assert.Contains(p.Options!, o => o.Value == p.Default!.Value.Text);
                    if (p.Kind is ParamKind.Number or ParamKind.Integer)
                        Assert.InRange(p.Default!.Value.Number!.Value, p.Min ?? double.MinValue, p.Max ?? double.MaxValue);
                }
        }

        [Theory]
        [InlineData("behälter", "process.vessel.vertical")]
        [InlineData("kolonne", "process.column")]
        [InlineData("wärmeübertrager", "process.hx.shelltube")]
        [InlineData("wärmetauscher", "process.hx.plate")]
        [InlineData("pumpe", "process.pump.centrifugal")]
        [InlineData("messstelle", "process.instrument.bubble")]
        [InlineData("kühlturm", "process.hx.coolingtower")]
        [InlineData("stellventil", "process.valve.control")]
        [InlineData("kugelhahn", "process.valve.ball")]
        [InlineData("signalleitung", "process.line.signal")]
        [InlineData("wärmerückgewinnung", "process.hvac.recovery")]
        [InlineData("kreisprozess", "process.chart.ts")]
        [InlineData("wärmepumpe", "process.chart.ph.heatpump")]
        [InlineData("heat exchanger", "process.hx.general")]
        [InlineData("distillation", "process.column")]
        [InlineData("rankine", "process.chart.ts")]
        [InlineData("otto", "process.chart.pv")]
        [InlineData("refrigeration", "process.chart.ph")]
        [InlineData("tic", "process.instrument.tic")]
        public void SearchFindsElementsInBothLanguages(string query, string itemId)
            => Assert.Contains(Fx.Reg().Search(query), i => i.Id == itemId);

        [Fact]
        public void CompanionsReferToRegisteredTypes()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var c in t.Companions) Assert.NotNull(r.Type(c));
        }

        [Fact]
        public void SuggestionsOfferCompanionsInTheModule()
        {
            var r = Fx.Reg();
            var pump = Fx.Item(r, "process.pump.centrifugal");
            var s = r.Suggestions(pump);
            Assert.Contains(s, i => i.TypeId == "process.line");
        }
    }
}
