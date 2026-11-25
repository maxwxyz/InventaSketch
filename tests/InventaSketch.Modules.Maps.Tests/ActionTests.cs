using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    public class ActionTests
    {
        private static ContextAction Act(Registry r, SketchObject o, string id, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Single(a => a.Id == id);

        private static List<ContextAction> Options(Registry r, SketchObject o, string key, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Where(a => a.Choice?.Key == key).ToList();

        [Fact]
        public void RoadTypeIsAChoiceOfAllKinds()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.road.motorway", new Vec2(50, 50));
            // One option per kind (dropdown "Road type: Motorway"), the current one marked.
            var options = Options(r, o, "map.road.kind", Lang.De);
            Assert.Equal(new[] { "motorway", "main", "side", "path" }, options.Select(a => ActionChoices.ValueOf(a.Id)));
            Assert.Equal("Straßenart", options[0].Choice!.Label.De);
            Assert.Equal("Straßenart: Autobahn", ActionChoices.Caption(options[0].Choice!, options).De);
            var seen = new List<string>();
            foreach (var kind in new[] { "main", "side", "path", "motorway" })
            {
                o = Act(r, o, "map.road.kind=" + kind).Apply(o);
                seen.Add(o.Str("kind", ""));
            }
            Assert.Equal(new[] { "main", "side", "path", "motorway" }, seen);
            Assert.Equal("Hauptstraße", Act(r, o, "map.road.kind=main", Lang.De).Label.De);
        }

        [Fact]
        public void RouteStyleAndArrowsAreChoices()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.route", new Vec2(50, 50));
            o = Act(r, o, "map.route.style=dashed").Apply(o);
            Assert.Equal("dashed", o.Str("style", ""));
            o = Act(r, o, "map.route.style=dotted").Apply(o);
            Assert.Equal("dotted", o.Str("style", ""));
            Assert.True(Act(r, o, "map.route.style=dotted").IsActive);
            o = Act(r, o, "map.route.arrows=end").Apply(o);
            Assert.Equal("end", o.Str("arrows", ""));
            var reversed = Act(r, o, "map.route.reverse").Apply(o);
            Assert.Equal(o.Points![0], reversed.Points![reversed.Points.Count - 1]);
        }

        /// <summary>
        /// Each thing once (interaction concept, section 9 item 4): points are added and removed with the hosts' corner dots
        /// and corner commands, views with the hosts' view dropdown, so no map element repeats them as its own actions.
        /// </summary>
        [Fact]
        public void PointsAndViewsAreNotRepeatedAsActions()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(50, 50));
                var ids = r.TypeOf(o).Actions(o, Fx.Ctx()).Select(a => a.Id).ToList();
                Assert.DoesNotContain(ids, id => id.StartsWith("view.", StringComparison.Ordinal) || id.StartsWith("map.point.", StringComparison.Ordinal));
            }
        }

        [Theory]
        [InlineData("2 km", 2.0, "km")]
        [InlineData("1,5 mi", 1.5, "mi")]
        [InlineData("500m", 500.0, "m")]
        [InlineData("3", 3.0, "km")]
        public void ScaleBarLengthInputAcceptsUnits(string input, double length, string unit)
        {
            var r = Fx.Reg();
            var bar = Fx.Item(r, "map.scalebar", new Vec2(50, 50));
            var (doc, _) = Fx.Doc(bar);
            doc = ContextActions.Run(r, doc, new[] { bar.Id }, "map.scalebar.length", input, Lang.De);
            var o = doc.Get(bar.Id)!;
            Assert.Equal(length, o.Num("length", 0), 6);
            Assert.Equal(unit, o.Str("unit", ""));
            Assert.Equal(bar.Size, o.Size);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("2 parsec")]
        [InlineData("-1 km")]
        public void ScaleBarLengthInputRejectsInvalidValues(string input)
        {
            var r = Fx.Reg();
            var bar = Fx.Item(r, "map.scalebar", new Vec2(50, 50));
            var (doc, _) = Fx.Doc(bar);
            Assert.Throws<FormatException>(() => ContextActions.Run(r, doc, new[] { bar.Id }, "map.scalebar.length", input, Lang.En));
        }

        [Fact]
        public void ScaleBarUnitCyclesAndInputShowsTheCurrentValue()
        {
            var r = Fx.Reg();
            var bar = Fx.Item(r, "map.scalebar", new Vec2(50, 50));
            var input = Act(r, bar, "map.scalebar.length", Lang.De).Input!;
            Assert.Equal("1 km", input.Value);
            var units = new List<string>();
            var o = bar;
            foreach (var unit in new[] { "mi", "m", "ft", "km" }) { o = Act(r, o, "map.scalebar.unit=" + unit).Apply(o); units.Add(o.Str("unit", "")); }
            Assert.Equal(new[] { "mi", "m", "ft", "km" }, units);
        }

        [Theory]
        [InlineData("90", 90)]
        [InlineData("1:30", 90)]
        [InlineData("1 h 30", 90)]
        [InlineData("2h", 120)]
        [InlineData("45 min", 45)]
        public void CalloutTimeInputParsesMinutes(string input, double minutes)
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "map.callout", new Vec2(50, 50));
            var (doc, _) = Fx.Doc(c);
            doc = ContextActions.Run(r, doc, new[] { c.Id }, "map.callout.time", input, Lang.En);
            Assert.Equal(minutes, doc.Get(c.Id)!.Num("time", 0), 6);
        }

        [Fact]
        public void CalloutShowsDistanceAndTimeInTheUiLanguage()
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "map.callout", new Vec2(0, 0)).WithParam("distance", 12.5).WithParam("time", 95);
            var de = r.TypeOf(c).Render(c, Fx.Ctx(Lang.De)).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Equal(new[] { "12,5 km", "1 h 35 min" }, de);
            var (doc, _) = Fx.Doc(c);
            doc = ContextActions.Run(r, doc, new[] { c.Id }, "map.callout.distance", "8 mi", Lang.En);
            var en = r.TypeOf(c).Render(doc.Get(c.Id)!, Fx.Ctx()).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Equal("8 mi", en[0]);
        }

        [Fact]
        public void StopNumbersStepAndNeverGoNegative()
        {
            var r = Fx.Reg();
            var s = Fx.Item(r, "map.stop", new Vec2(0, 0));
            s = Act(r, s, "map.stop.next").Apply(s);
            Assert.Equal(2, s.Int("number", 0));
            Assert.Contains(r.TypeOf(s).Render(s, Fx.Ctx()).OfType<TextPrim>(), t => t.Text == "2");
            s = s.WithParam("number", 0);
            Assert.False(Act(r, s, "map.stop.prev").IsEnabled);
        }

        [Fact]
        public void PoiKindIsAChoiceNamingEachKind()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.poi.pin", new Vec2(0, 0));
            var a = Act(r, o, "map.poi.kind=building");
            Assert.Equal("Building", a.Label.En);
            Assert.Equal("Place type", a.Choice!.Label.En);
            Assert.Equal("building", a.Apply(o).Str("kind", ""));
        }

        [Fact]
        public void ViewSwitchKeepsTheVisualScale()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.poi.fuel", new Vec2(0, 0)) with { Size = new Size2(14, 14) };
            // Registry.WithView (the host's view dropdown) keeps the drawing scale through the views' design sizes.
            var pin = r.WithView(o, "pin", Fx.Ctx());
            Assert.Equal("pin", pin.View);
            Assert.Equal(Math.Sqrt(14 * 14 / 49.0) * 8, pin.Size.W, 6);
        }
    }
}
