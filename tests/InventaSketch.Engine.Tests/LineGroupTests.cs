using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>ISO 128 line groups as a drawing-wide setting (<see cref="DocumentSettings.LineGroup"/>, <c>setLineGroup</c>).</summary>
    public class LineGroupTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static double MaxWidth(SketchDocument doc, SketchObject o)
            => Reg.Render(o, new RenderContext(doc)).OfType<EllipsePrim>().Select(e => e.Style.EffectiveWidth)
                .Concat(Reg.Render(o, new RenderContext(doc)).OfType<LinePrim>().Select(l => l.Style.EffectiveWidth)).Max();

        [Fact]
        public void TheLineGroupScalesEveryStrokeAndKeepsTheRatio()
        {
            var gear = Reg.Create(Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial"), new Vec2(50, 50));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, gear);
            var thick = MaxWidth(doc, gear);
            var heavy = doc with { Settings = doc.Settings with { LineGroup = 1.0 } };
            Assert.Equal(thick * 2, MaxWidth(heavy, gear), 6);
            // Thin lines (centre lines) scale alike.
            double Thin(SketchDocument d) => Reg.Render(gear, new RenderContext(d)).Select(p => p switch { EllipsePrim e => e.Style, LinePrim l => l.Style, _ => null })
                .Where(s => s?.Role == LineRole.Center).Select(s => s!.EffectiveWidth).DefaultIfEmpty(0).Min();
            Assert.Equal(Thin(doc) * 2, Thin(heavy), 6);
            // An explicit width set on the object (its own formatting of the visible lines) still wins.
            var own = gear with { Style = new Style { StrokeWidth = 0.3 } };
            var withOwn = Edits.Replace(heavy, own);
            var visible = Reg.Render(own, new RenderContext(withOwn)).OfType<EllipsePrim>().Where(e => (e.Style.Role ?? LineRole.Visible) == LineRole.Visible).ToList();
            Assert.NotEmpty(visible);
            Assert.All(visible, e => Assert.Equal(0.3, e.Style.EffectiveWidth, 6));
        }

        [Fact]
        public void TheEngineSetsTheLineGroupAndAdoptsItFromTheSlideModel()
        {
            var (api, session) = EngineApiTests.NewSession();
            var gear = EngineApiTests.Insert(api, session, api.Registry.Library.First().Id, 60, 60);
            var st = EngineApiTests.Call(api, "setLineGroup", new { session, value = 0.7 });
            Assert.Equal(0.7, st.GetProperty("lineGroup").GetDouble(), 6);
            // Every object counts as changed, so hosts redraw all units.
            Assert.Contains(st.GetProperty("changed").EnumerateArray(), x => x.GetString() == gear);
            Assert.Equal(0.7, api.Sessions.Get(session).Doc.Settings.LineGroup);
            st = EngineApiTests.Call(api, "setLineGroup", new { session, value = 0.5 });
            Assert.Null(api.Sessions.Get(session).Doc.Settings.LineGroup);
            Assert.Equal(0.5, st.GetProperty("lineGroup").GetDouble(), 6);
            Assert.ThrowsAny<System.Exception>(() => EngineApiTests.Call(api, "setLineGroup", new { session, value = 0.6 }));

            // A slide model with another group sets it for the page session (slide-wide, like black and white).
            var src = SketchDocument.Create() with { Settings = new DocumentSettings { LineGroup = 0.35 } };
            var payload = EngineApi.CompactOf(src, src.Pages[0], System.Array.Empty<string>());
            EngineApiTests.Call(api, "importCompact", new { session, payload });
            Assert.Equal(0.35, api.Sessions.Get(session).Doc.Settings.LineGroup);
        }
    }
}
