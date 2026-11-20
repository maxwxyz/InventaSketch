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

        [Fact]
        public void RoadTypeCyclesThroughAllKinds()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.road.motorway", new Vec2(50, 50));
            var seen = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var a = Act(r, o, "map.road.kind");
                o = a.Apply(o);
                seen.Add(o.Str("kind", ""));
            }
            Assert.Equal(new[] { "main", "side", "path", "motorway" }, seen);
            Assert.Equal("Straßenart: Hauptstraße", Act(r, o, "map.road.kind", Lang.De).Label.De);
        }

        [Fact]
        public void RouteStyleAndArrowsCycle()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.route", new Vec2(50, 50));
            o = Act(r, o, "map.route.style").Apply(o);
            Assert.Equal("dashed", o.Str("style", ""));
            o = Act(r, o, "map.route.style").Apply(o);
            Assert.Equal("dotted", o.Str("style", ""));
            o = Act(r, o, "map.route.arrows").Apply(o);
            Assert.Equal("end", o.Str("arrows", ""));
            var reversed = Act(r, o, "map.route.reverse").Apply(o);
            Assert.Equal(o.Points![0], reversed.Points![reversed.Points.Count - 1]);
        }

        [Fact]
        public void AddAndRemoveWaypointKeepAttachmentsAligned()
        {
            var r = Fx.Reg();
            var stop = Fx.Item(r, "map.stop", new Vec2(100, 0));
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(20, 0), new Vec2(100, 0)) with
            {
                Attach = new Attachment?[] { null, null, new Attachment(stop.Id, 0, 0) { AnchorId = "loc" } },
            };
            var added = Act(r, route, "map.point.add").Apply(route);
            Assert.Equal(4, added.Points!.Count);
            Assert.Equal(new Vec2(60, 0), added.Points[2]); // midpoint of the longest segment
            Assert.Equal(4, added.Attach!.Count);
            Assert.Null(added.Attach[2]);
            Assert.Equal(stop.Id, added.Attach[3]!.ObjectId);

            // Removing takes out the least significant (collinear) point: the one just added.
            var removed = Act(r, added, "map.point.remove").Apply(added);
            Assert.Equal(3, removed.Points!.Count);
            Assert.Equal(stop.Id, removed.Attach![2]!.ObjectId);

            // A two-point route cannot lose a point: the action is offered but disabled.
            var two = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(10, 0));
            var remove = Act(r, two, "map.point.remove");
            Assert.False(remove.IsEnabled);
            var after = ContextActions.Run(r, Fx.Doc(two).Doc, new[] { two.Id }, "map.point.remove", null, Lang.En);
            Assert.Equal(2, after.Get(two.Id)!.Points!.Count);
        }

        [Fact]
        public void RemovingACornerKeepsRegionsClosedWithAtLeastThreePoints()
        {
            var r = Fx.Reg();
            var region = Fx.Line(r, "map.region", new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10));
            var three = Act(r, region, "map.point.remove").Apply(region);
            Assert.Equal(3, three.Points!.Count);
            Assert.False(Act(r, three, "map.point.remove").IsEnabled);
            var five = Act(r, region, "map.point.add").Apply(region);
            Assert.Equal(5, five.Points!.Count);
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
            for (var i = 0; i < 4; i++) { o = Act(r, o, "map.scalebar.unit").Apply(o); units.Add(o.Str("unit", "")); }
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
        public void PoiKindCycleNamesTheNextKind()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.poi.pin", new Vec2(0, 0));
            var a = Act(r, o, "map.poi.kind");
            Assert.Equal("Place type: Building", a.Label.En);
            Assert.Equal("building", a.Apply(o).Str("kind", ""));
        }

        [Fact]
        public void ViewSwitchKeepsTheVisualScale()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.poi.fuel", new Vec2(0, 0)) with { Size = new Size2(14, 14) };
            var pin = Act(r, o, "view.pin").Apply(o);
            Assert.Equal("pin", pin.View);
            Assert.Equal(Math.Sqrt(14 * 14 / 49.0) * 8, pin.Size.W, 6);
            // Registry.WithView (host view bar) gives the same size.
            Assert.Equal(pin.Size, r.WithView(o, "pin", Fx.Ctx()).Size);
        }
    }
}
