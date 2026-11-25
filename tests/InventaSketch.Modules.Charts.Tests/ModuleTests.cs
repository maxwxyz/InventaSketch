using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new ChartsModule());

        public static RenderContext Ctx(Lang lang = Lang.En, bool mono = false) => new(SketchDocument.Create(), lang, mono);

        public static SketchObject Item(Registry r, string id, Vec2 at = default) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static IReadOnlyList<Primitive> Render(Registry r, SketchObject o, RenderContext? ctx = null) => r.TypeOf(o).Render(o, ctx ?? Ctx());

        public static ChartType Type(Registry r, SketchObject o) => (ChartType)r.TypeOf(o);

        public static void AssertFinite(IEnumerable<Primitive> prims, string what)
        {
            foreach (var p in Prim.Flatten(prims))
            {
                foreach (var pt in Prim.Points(p))
                    Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN/Infinity in " + p.Tag);
                if (p is TextPrim t) Assert.True(t.TextStyle.Size > 0 && !double.IsNaN(t.TextStyle.Size), what + ": text size");
            }
        }

        /// <summary>
        /// Everything inside the box: shapes by their points, texts by their estimated extent (rotation-aware: the y-axis
        /// titles are turned by 90°).
        /// </summary>
        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            AssertFinite(prims, what);
            const double tol = 0.05;
            double hw = size.W / 2 + tol, hh = size.H / 2 + tol;
            foreach (var p in Prim.Flatten(prims))
            {
                if (p is TextPrim t)
                {
                    var w = Math.Max(1, t.Text.Length) * t.TextStyle.Size * 0.55;
                    var h = t.TextStyle.Size;
                    double x0 = t.TextStyle.Anchor switch { TextAnchor.Middle => -w / 2, TextAnchor.End => -w, _ => 0 };
                    var turned = Math.Abs(Math.Abs(t.Rotation) - 90) < 1;
                    double minX, maxX, minY, maxY;
                    if (turned) { minX = t.Position.X - h / 2; maxX = t.Position.X + h / 2; minY = t.Position.Y - (x0 + w); maxY = t.Position.Y - x0; }
                    else { minX = t.Position.X + x0; maxX = minX + w; minY = t.Position.Y - h / 2; maxY = t.Position.Y + h / 2; }
                    Assert.True(minX >= -hw && maxX <= hw && minY >= -hh && maxY <= hh,
                        $"{what}: text '{t.Text}' ({minX:0.##}..{maxX:0.##}, {minY:0.##}..{maxY:0.##}) outside box {size.W}x{size.H}");
                    continue;
                }
                foreach (var pt in Prim.Points(p))
                    Assert.True(pt.X >= -hw && pt.X <= hw && pt.Y >= -hh && pt.Y <= hh, $"{what}: {p.Tag} point {pt} outside box {size.W}x{size.H}");
            }
        }
    }

    public class ModuleTests
    {
        public static readonly string[] ExpectedTypes =
        {
            "chart.xy", "chart.bar", "chart.axes", "chart.torque", "chart.efficiency", "chart.pump", "chart.bode", "chart.band", "chart.figure",
            "chart.math.axes", "chart.math.function", "chart.timing",
        };

        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var r = Fx.Reg();
            var m = Assert.Single(r.Modules);
            Assert.Equal("charts", m.Id);
            Assert.Equal("Diagramme & Kennlinien", m.Name.De);
            Assert.Equal(ExpectedTypes.OrderBy(x => x), r.Types.Select(t => t.Id).OrderBy(x => x));
            Assert.All(r.Types, t => Assert.Equal(GeometryKind.Box, t.Geometry));
            Assert.All(r.Types, t => Assert.False(t.IsPart));
        }

        [Fact]
        public void EverythingIsBilingualAndCategorised()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            foreach (var c in r.Categories) Assert.False(string.IsNullOrEmpty(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.En), t.Id);
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.NotEqual(t.Name.En, t.Name.De);
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.Equal(t.Parameters.Count, t.Parameters.Select(p => p.Key).Distinct().Count());
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrEmpty(opt.Label.De), t.Id + "/" + p.Key + "/" + opt.Value);
                }
                var o = r.Create(r.Library.First(i => i.TypeId == t.Id), new Vec2(50, 50));
                foreach (var a in t.Actions(o, Fx.Ctx()))
                {
                    Assert.False(string.IsNullOrEmpty(a.Label.En), t.Id + "/" + a.Id);
                    Assert.False(string.IsNullOrEmpty(a.Label.De), t.Id + "/" + a.Id);
                    Assert.Contains(a.Icon, IconIds.All);
                    if (a.Input is not null) Assert.False(string.IsNullOrEmpty(a.Input.Label.De), t.Id + "/" + a.Id);
                }
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.En), item.Id);
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
                Assert.StartsWith("chart.", item.Id);
                var t = r.Type(item.TypeId)!;
                if (item.Params is not null)
                    foreach (var kv in item.Params) Assert.Contains(t.Parameters, p => p.Key == kv.Key);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void LibraryCoversTheScope()
        {
            var r = Fx.Reg();
            foreach (var id in new[]
            {
                "chart.line", "chart.line.multi", "chart.scatter", "chart.step", "chart.column", "chart.column.grouped", "chart.column.stacked",
                "chart.bar.horizontal", "chart.axes", "chart.torque", "chart.efficiency", "chart.pump", "chart.fan", "chart.bode", "chart.band", "chart.figure.phases",
                "chart.line.logx", "chart.limits",
            })
                Assert.NotNull(r.Item(id));
        }

        [Theory]
        [InlineData("liniendiagramm")]
        [InlineData("line chart")]
        [InlineData("scatter")]
        [InlineData("streudiagramm")]
        [InlineData("balkendiagramm")]
        [InlineData("column")]
        [InlineData("drehmoment")]
        [InlineData("torque")]
        [InlineData("wirkungsgrad")]
        [InlineData("efficiency map")]
        [InlineData("pumpe")]
        [InlineData("betriebspunkt")]
        [InlineData("bode")]
        [InlineData("frequenzgang")]
        [InlineData("toleranzband")]
        [InlineData("koordinatensystem")]
        [InlineData("excel")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void BareObjectsRenderWithoutParameters()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                var prims = t.Render(o, Fx.Ctx());
                Fx.AssertInsideBox(prims, o.Size, t.Id);
                Assert.Contains(Prim.Flatten(prims), p => ChartTags.Is(p.Tag, ChartTags.Axis));
                Assert.NotEmpty(t.Anchors(o, Fx.Ctx()));
            }
        }
    }
}
