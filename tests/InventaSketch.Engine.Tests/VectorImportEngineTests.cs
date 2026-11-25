using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine <c>importVector</c>: SVG/DXF drawings become editable, grouped basic shapes on the page.</summary>
    public class VectorImportEngineTests
    {
        [Fact]
        public void AnSvgWithUnitsKeepsItsSizeAsGroupedEditableShapes()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en", pageWidth = 280.0, pageHeight = 157.5 }).GetProperty("session").GetString()!;
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='60mm' height='40mm' viewBox='0 0 60 40'>"
                + "<rect x='0' y='0' width='60' height='40'/><circle cx='30' cy='20' r='10'/>"
                + "<path d='M0 40 L30 0 L60 40'/><text x='5' y='35' font-size='5'>A</text></svg>";
            var state = EngineApiTests.Call(api, "importVector", new { session, text = svg });
            Assert.Equal(4, state.GetProperty("selection").GetArrayLength());
            var doc = api.Sessions.Get(session).Doc;
            var objs = doc.Objects.Values.ToList();
            Assert.Equal(new[] { "ann.polyline", "ann.text", "basic.ellipse", "basic.rect" }, objs.Select(o => o.Type).OrderBy(t => t));
            Assert.Single(objs.Select(o => o.GroupId).Distinct());
            // Real size (60 x 40 mm), centred on the page.
            var rect = objs.Single(o => o.Type == "basic.rect");
            Assert.Equal(60, rect.Size.W, 3); Assert.Equal(40, rect.Size.H, 3);
            Assert.Equal(140, rect.Transform.X, 3); Assert.Equal(78.75, rect.Transform.Y, 3);
            var circle = objs.Single(o => o.Type == "basic.ellipse");
            Assert.Equal(20, circle.Size.W, 3);
            Assert.Equal(140, circle.Transform.X, 3);
            Assert.Equal(3, objs.Single(o => o.Type == "ann.polyline").Points!.Count);
            Assert.Equal("A", objs.Single(o => o.Type == "ann.text").Text);
            // One undo step removes the whole drawing.
            EngineApiTests.Call(api, "undo", new { session });
            Assert.Empty(api.Sessions.Get(session).Doc.Objects);
        }

        [Fact]
        public void ADxfWithoutUnitsIsFittedIntoThePage()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en", pageWidth = 280.0, pageHeight = 157.5 }).GetProperty("session").GetString()!;
            var dxf = string.Join("\n", "0", "SECTION", "2", "ENTITIES", "0", "LINE", "10", "0", "20", "0", "11", "10000", "21", "0", "0", "ENDSEC", "0", "EOF");
            EngineApiTests.Call(api, "importVector", new { session, text = dxf });
            var line = Assert.Single(api.Sessions.Get(session).Doc.Objects.Values);
            Assert.Equal("ann.line", line.Type);
            Assert.Equal(224, line.Points![1].X - line.Points[0].X, 3);       // 80 % of the page width
        }
    }
}
