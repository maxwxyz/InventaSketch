using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;
using static InventaSketch.Engine.Tests.EngineApiTests;

namespace InventaSketch.Engine.Tests
{
    public class DrawPlanTests
    {
        private const double Pt = 72.0 / 25.4;

        private static JsonElement Plan(EngineApi api, string s, string id, object? caps = null)
            => Call(api, "drawPlan", new { session = s, ids = new[] { id }, capabilities = caps });

        private static JsonElement[] Ops(JsonElement unit) => unit.GetProperty("ops").EnumerateArray().ToArray();

        private static JsonElement[] Flat(JsonElement unit) => Ops(unit).SelectMany(Expand).ToArray();

        private static System.Collections.Generic.IEnumerable<JsonElement> Expand(JsonElement op)
        {
            if (op.GetProperty("op").GetString() == "group") return op.GetProperty("children").EnumerateArray().SelectMany(Expand);
            return new[] { op };
        }

        [Fact]
        public void RectangleIsConvertedToPoints()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40); // 20 x 10 mm centred at (50, 40)
            var plan = Plan(api, s, id);
            Assert.Equal("pt", plan.GetProperty("unit").GetString());
            Assert.Equal(338.667 * Pt, plan.GetProperty("pageWidth").GetDouble(), 1);
            var unit = plan.GetProperty("units")[0];
            Assert.Equal("native", unit.GetProperty("mode").GetString());
            Assert.Equal(id, unit.GetProperty("id").GetString());
            Assert.Equal("ISK:etest.block:" + id, unit.GetProperty("name").GetString());
            var rect = Ops(unit).Single();
            Assert.Equal("rect", rect.GetProperty("op").GetString());
            Assert.Equal(40 * Pt, rect.GetProperty("x").GetDouble(), 2);
            Assert.Equal(35 * Pt, rect.GetProperty("y").GetDouble(), 2);
            Assert.Equal(20 * Pt, rect.GetProperty("w").GetDouble(), 2);
            Assert.Equal(10 * Pt, rect.GetProperty("h").GetDouble(), 2);
            Assert.Equal(0.5 * Pt, rect.GetProperty("stroke").GetProperty("weight").GetDouble(), 2);
            Assert.Equal("solid", rect.GetProperty("stroke").GetProperty("dash").GetString());
            Assert.Equal("none", rect.GetProperty("fill").GetProperty("kind").GetString());
            Assert.False(rect.TryGetProperty("rotation", out _));
            // Payload is the compact model of exactly this object.
            var doc = DocumentJson.FromCompact(unit.GetProperty("payload").GetString()!);
            Assert.Single(doc.Objects);
            Assert.Equal(id, doc.Objects.Keys.Single());
        }

        [Fact]
        public void RotatedRectangleKeepsRotationWhenHostSupportsIt()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            Call(api, "rotate", new { session = s, ids = new[] { id }, degrees = 30 });
            var rect = Ops(Plan(api, s, id).GetProperty("units")[0]).Single();
            Assert.Equal("rect", rect.GetProperty("op").GetString());
            Assert.Equal(30, rect.GetProperty("rotation").GetDouble(), 6);
            Assert.Equal(40 * Pt, rect.GetProperty("x").GetDouble(), 2); // unrotated box around the same centre

            var old = Plan(api, s, id, new { rotation = false }).GetProperty("units")[0];
            Assert.Equal("svg", old.GetProperty("mode").GetString());
            var noShapes = Plan(api, s, id, new { shapes = false }).GetProperty("units")[0];
            Assert.Equal("svg", noShapes.GetProperty("mode").GetString());
            var full = Plan(api, s, id, new { freeform = true, arrowheads = true, rotation = false }).GetProperty("units")[0];
            Assert.Equal("polyline", Ops(full).Single().GetProperty("op").GetString());
        }

        [Fact]
        public void HatchIsLoweredToLineSegmentsUnlessPatternFillIsSupported()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block.hatched", 50, 40);
            var unit = Plan(api, s, id).GetProperty("units")[0];
            Assert.Equal("native", unit.GetProperty("mode").GetString());
            var hatch = Ops(unit).First(o => o.GetProperty("op").GetString() == "group");
            Assert.Equal("hatch", hatch.GetProperty("tag").GetString());
            Assert.True(hatch.GetProperty("children").GetArrayLength() > 5);
            Assert.All(hatch.GetProperty("children").EnumerateArray(), l => Assert.Equal("line", l.GetProperty("op").GetString()));
            Assert.Equal("rect", Ops(unit).Last().GetProperty("op").GetString()); // outline above hatch

            var lo = Plan(api, s, id, new { patternFill = true }).GetProperty("units")[0];
            var rect = Ops(lo).Single();
            Assert.Equal("hatch", rect.GetProperty("fill").GetProperty("kind").GetString());
            Assert.Equal(2 * Pt, rect.GetProperty("fill").GetProperty("hatch").GetProperty("spacing").GetDouble(), 2);
        }

        [Fact]
        public void ArrowheadsBecomeRotatedTrianglesWithoutNativeArrowheads()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.arrow", 100, 40); // (85,40) -> (115,40), pointing right
            var unit = Plan(api, s, id).GetProperty("units")[0];
            Assert.Equal("native", unit.GetProperty("mode").GetString());
            var ops = Flat(unit);
            var tri = ops.Single(o => o.GetProperty("op").GetString() == "shape");
            Assert.Equal("triangle", tri.GetProperty("shape").GetString());
            Assert.Equal(90, tri.GetProperty("rotation").GetDouble(), 6);
            Assert.Equal("arrowEnd", tri.GetProperty("tag").GetString());
            var line = ops.Single(o => o.GetProperty("op").GetString() == "line");
            Assert.True(line.GetProperty("x2").GetDouble() < 115 * Pt); // shortened under the head
            Assert.Equal(85 * Pt, line.GetProperty("x1").GetDouble(), 2);

            var native = Flat(Plan(api, s, id, new { arrowheads = true }).GetProperty("units")[0]).Single();
            Assert.Equal("triangle", native.GetProperty("stroke").GetProperty("arrowEnd").GetString());
            Assert.Equal(115 * Pt, native.GetProperty("x2").GetDouble(), 2);
        }

        [Fact]
        public void CurvesFallBackToSvgOrFreeform()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.curve", 100, 60);
            var unit = Plan(api, s, id).GetProperty("units")[0];
            Assert.Equal("svg", unit.GetProperty("mode").GetString());
            var svg = Ops(unit).Single();
            Assert.Equal("svg", svg.GetProperty("op").GetString());
            Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"", svg.GetProperty("svg").GetString());
            Assert.True(svg.GetProperty("w").GetDouble() > 30 * Pt);
            Assert.NotEmpty(unit.GetProperty("notes").EnumerateArray());

            var full = Plan(api, s, id, new { freeform = true, arrowheads = true }).GetProperty("units")[0];
            var ff = Ops(full).Single();
            Assert.Equal("freeform", ff.GetProperty("op").GetString());
            var segs = ff.GetProperty("segments").EnumerateArray().ToArray();
            Assert.Equal("M", segs[0].GetProperty("op").GetString());
            Assert.Equal("C", segs[1].GetProperty("op").GetString());
            Assert.Equal(85 * Pt, segs[0].GetProperty("x").GetDouble(), 2);
        }

        [Fact]
        public void AssemblyBecomesOneUnitWithSharedPayload()
        {
            var (api, s) = NewSession();
            var a = Insert(api, s, "etest.block", 50, 40);
            var b = Insert(api, s, "etest.wheel", 80, 40);
            var st = Call(api, "group", new { session = s, ids = new[] { a, b } });
            var plan = Call(api, "drawPlan", new { session = s });
            var unit = plan.GetProperty("units").EnumerateArray().Single();
            Assert.Equal(2, unit.GetProperty("objectIds").GetArrayLength());
            Assert.Equal(st.GetProperty("objects")[0].GetProperty("groupId").GetString(), unit.GetProperty("id").GetString());
            Assert.Equal(2, Ops(unit).Length);
            Assert.All(Ops(unit), g => Assert.Equal("group", g.GetProperty("op").GetString()));
            Assert.Equal(2, DocumentJson.FromCompact(unit.GetProperty("payload").GetString()!).Objects.Count);
        }

        [Fact]
        public void TextOpUsesPointFontSize()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "basic.text", 50, 40);
            Call(api, "setText", new { session = s, id, text = "Gear" });
            var text = Ops(Plan(api, s, id).GetProperty("units")[0]).Single();
            Assert.Equal("text", text.GetProperty("op").GetString());
            Assert.Equal("Gear", text.GetProperty("text").GetString());
            Assert.Equal(3.5 * Pt, text.GetProperty("fontSize").GetDouble(), 2);
            Assert.Equal("center", text.GetProperty("align").GetString());
            var cx = text.GetProperty("x").GetDouble() + text.GetProperty("w").GetDouble() / 2;
            Assert.Equal(50 * Pt, cx, 2);
        }
    }
}
