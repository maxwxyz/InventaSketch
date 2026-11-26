using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// Schematic gap fillers (2026-11-26): valves, strainer, meters, buffer tank, solar collector, duct parts and the split
    /// air conditioner. Inserted without parameters, drawn in every view, and their ports dock on the existing pipes and
    /// ducts (a free line end takes the element with a fitting port; "+" at the element's port draws the matching line).
    /// </summary>
    public class FittingTests
    {
        public static readonly string[] NewTypes =
        {
            "bs.valve.ball", "bs.valve.check", "bs.valve.safety", "bs.strainer", "bs.meter.heat", "bs.meter.water",
            "bs.heat.buffer", "bs.heat.solarthermal", "bs.vent.damper", "bs.vent.firedamper", "bs.vent.silencer", "bs.cool.split",
        };

        private static SketchDocument Page(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(new Size2(1000, 800));
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        /// <summary>The medium of port <paramref name="port"/> of a freshly inserted <paramref name="item"/>.</summary>
        private static string? MediumOf(Registry r, string item, string port, params (string Key, ParamValue Value)[] ps)
        {
            var o = Fx.Item(r, item);
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            var a = Fx.Anchors(r, o, AnchorRole.Port).Single(x => x.Id == port);
            return r.TypeOf(o).Connection(o, a)?.Medium;
        }

        /// <summary>
        /// A free end of <paramref name="lineItem"/> takes <paramref name="item"/>: the element is placed with a fitting port
        /// on the end and the line end is docked there. Returns the port id it docked with.
        /// </summary>
        private static string DockOnLineEnd(Registry r, string lineItem, string item)
        {
            var line = r.Create(r.Item(lineItem)!, new Vec2(500, 400));
            var doc = Page(line);
            var end = Building.Ports(r, doc, line.Id).First(p => p.Kind == PortKind.LineEnd);
            var built = Building.Apply(r, doc, line.Id, end, r.Item(item)!);
            Assert.True(built is not null, $"{item} does not dock on a free end of {lineItem}");
            var l2 = built!.Doc.Get(line.Id)!;
            var index = Attachments.PointIndex(end.AnchorId)!.Value;
            var att = l2.Attach![index]!;
            Assert.Equal(built.SelectId, att.ObjectId);
            var el = built.Doc.Get(built.SelectId)!;
            var port = r.PageAnchors(el, new RenderContext(built.Doc)).Single(a => a.Role == AnchorRole.Port && a.Id == att.AnchorId);
            Assert.True(port.P.DistanceTo(built.Doc.ResolvePoints(l2)[index]) < 1e-6, $"{item}: line end off the port {port.Id}");
            return port.Id!;
        }

        [Fact]
        public void NewElementsInsertWithoutParametersAndDrawInEveryView()
        {
            var r = Fx.Reg();
            foreach (var id in NewTypes)
            {
                var t = Assert.IsType<BsBoxType>(r.Type(id));
                var o = Fx.Item(r, id);
                // Quick Sketch: no engineering value is set at insert.
                Assert.DoesNotContain(o.Params, kv => t.ValueSpec.Params.Any(p => p.Key == kv.Key) && kv.Key != ValueLabels.ShowParam);
                foreach (var v in t.Views)
                {
                    var x = r.WithView(o, v.Id, Fx.Ctx());
                    foreach (var lang in new[] { Lang.En, Lang.De }) Fx.AssertInsideBox(t.Render(x, Fx.Ctx(lang)), x.Size, $"{id} [{v.Id}, {lang}]");
                    Assert.NotEmpty(Fx.Anchors(r, x, AnchorRole.Port));
                }
            }
        }

        [Theory]
        [InlineData("bs.valve.ball")]
        [InlineData("bs.valve.check")]
        [InlineData("bs.strainer")]
        [InlineData("bs.meter.heat")]
        public void InlineHeatingFittingsTakeFlowAndReturnPipesLikeThePump(string item)
        {
            var r = Fx.Reg();
            foreach (var port in new[] { "in", "out" })
            {
                var m = MediumOf(r, item, port);
                Assert.Equal(MediumOf(r, "bs.heat.pump", port), m);
                Assert.True(PortMedia.Fits(m, PortMedia.HeatingFlow) && PortMedia.Fits(m, PortMedia.HeatingReturn), item + " " + port);
            }
            Assert.Contains(DockOnLineEnd(r, "bs.pipe.flow", item), new[] { "in", "out" });
            Assert.Contains(DockOnLineEnd(r, "bs.pipe.return", item), new[] { "in", "out" });
            // A heating fitting does not take a drinking water pipe (its "service" would have to be switched first).
            var cold = r.Create(r.Item("bs.pipe.cold")!, new Vec2(500, 400));
            var doc = Page(cold);
            var end = Building.Ports(r, doc, cold.Id).First(p => p.Kind == PortKind.LineEnd);
            Assert.Null(Building.Apply(r, doc, cold.Id, end, r.Item(item)!));
        }

        [Theory]
        [InlineData("bs.valve.ball.water")]
        [InlineData("bs.valve.check.water")]
        [InlineData("bs.strainer.water")]
        [InlineData("bs.meter.water")]
        public void DrinkingWaterFittingsTakeEveryWaterPipe(string item)
        {
            var r = Fx.Reg();
            foreach (var port in new[] { "in", "out" })
            {
                var m = MediumOf(r, item, port);
                Assert.Equal(PortMedia.Water, m);
                Assert.False(PortMedia.Fits(m, PortMedia.HeatingFlow));
            }
            foreach (var pipe in new[] { "bs.pipe.cold", "bs.pipe.hot", "bs.pipe.circulation" })
                Assert.Contains(DockOnLineEnd(r, pipe, item), new[] { "in", "out" });
        }

        [Fact]
        public void TheServiceChoiceSwitchesAValveBetweenHeatingAndDrinkingWater()
        {
            var r = Fx.Reg();
            var valve = Fx.Item(r, "bs.valve.ball");
            var choice = r.TypeOf(valve).Actions(valve, Lang.En).Single(a => a.Id == "bs.fitting.service=water");
            var water = choice.Apply(valve);
            Assert.Equal("water", water.Str("service", ""));
            var a = Fx.Anchors(r, water, AnchorRole.Port).Single(x => x.Id == "in");
            Assert.Equal(PortMedia.Water, r.TypeOf(water).Connection(water, a)!.Medium);
            // "+" at the port draws a cold water pipe.
            var doc = Page(water);
            var port = Building.Ports(r, doc, water.Id).Single(p => p.Kind == PortKind.Connect && p.AnchorId == "in");
            var pipeItem = Building.Items(r, doc, water.Id, port, 6).First(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Points);
            var built = Building.Apply(r, doc, water.Id, port, pipeItem)!;
            Assert.Equal("cold", built.Doc.Get(built.SelectId)!.Str("medium", ""));
        }

        [Fact]
        public void SafetyValveTakesTheSystemBelowAndBlowsOffIntoTheWastePipe()
        {
            var r = Fx.Reg();
            Assert.Equal(PortMedia.Heating, MediumOf(r, "bs.valve.safety", "in"));
            Assert.Equal(PortMedia.Waste, MediumOf(r, "bs.valve.safety", "waste"));
            Assert.Equal(PortMedia.Water, MediumOf(r, "bs.valve.safety", "in", ("service", "water")));
            Assert.Equal("waste", DockOnLineEnd(r, "bs.pipe.waste", "bs.valve.safety"));
            Assert.Equal("in", DockOnLineEnd(r, "bs.pipe.flow", "bs.valve.safety"));
            var sv = Fx.Item(r, "bs.valve.safety");
            var ports = Fx.Anchors(r, sv, AnchorRole.Port).ToDictionary(a => a.Id!, a => a.P);
            Assert.Equal(new Vec2(0, sv.Size.H / 2), ports["in"]);
            Assert.Equal(new Vec2(sv.Size.W / 2, 0), ports["waste"]);
        }

        [Fact]
        public void BufferTankHasAGeneratorAndAConsumerCircuit()
        {
            var r = Fx.Reg();
            var buffer = Fx.Item(r, "bs.heat.buffer");
            foreach (var view in BsViews.All)
            {
                var o = r.WithView(buffer, view, Fx.Ctx());
                var ports = Fx.Anchors(r, o, AnchorRole.Port).ToDictionary(a => a.Id!, a => a);
                Assert.Equal(new[] { "f1", "f2", "r1", "r2" }, ports.Keys.OrderBy(k => k));
                Assert.True(ports["f1"].P.X < 0 && ports["r1"].P.X < 0 && ports["f2"].P.X > 0 && ports["r2"].P.X > 0, view);
                Assert.True(ports["f1"].P.Y < ports["r1"].P.Y && ports["f2"].P.Y < ports["r2"].P.Y, view + ": flow above return");
                Assert.Equal(PortMedia.HeatingFlow, r.TypeOf(o).Connection(o, ports["f2"])!.Medium);
                Assert.Equal(PortMedia.HeatingReturn, r.TypeOf(o).Connection(o, ports["r1"])!.Medium);
            }
            Assert.StartsWith("f", DockOnLineEnd(r, "bs.pipe.flow", "bs.heat.buffer"));
            Assert.StartsWith("r", DockOnLineEnd(r, "bs.pipe.return", "bs.heat.buffer"));
        }

        [Fact]
        public void SolarCollectorFeedsFlowAndReturnPipes()
        {
            var r = Fx.Reg();
            Assert.Equal(PortMedia.HeatingFlow, MediumOf(r, "bs.heat.solarthermal", "flow"));
            Assert.Equal(PortMedia.HeatingReturn, MediumOf(r, "bs.heat.solarthermal", "return"));
            Assert.Equal("flow", DockOnLineEnd(r, "bs.pipe.flow", "bs.heat.solarthermal"));
            Assert.Equal("return", DockOnLineEnd(r, "bs.pipe.return", "bs.heat.solarthermal"));
        }

        [Theory]
        [InlineData("bs.vent.damper")]
        [InlineData("bs.vent.firedamper")]
        [InlineData("bs.vent.silencer")]
        public void DuctPartsSitInEveryDuct(string item)
        {
            var r = Fx.Reg();
            foreach (var port in new[] { "inlet", "outlet" }) Assert.Equal(PortMedia.Air, MediumOf(r, item, port));
            foreach (var kind in new[] { "supply", "extract", "outdoor", "exhaust" })
                Assert.Contains(DockOnLineEnd(r, "bs.duct." + kind, item), new[] { "inlet", "outlet" });
            // On the duct axis in every view, so a straight duct runs through.
            var o = Fx.Item(r, item);
            foreach (var view in BsViews.All)
            {
                var x = r.WithView(o, view, Fx.Ctx());
                var ports = Fx.Anchors(r, x, AnchorRole.Port);
                Assert.All(ports, p => Assert.Equal(0, p.P.Y, 9));
            }
            // "+" draws a duct of the part's air kind.
            var part = o.WithParam("kind", "extract");
            var doc = Page(part);
            var bp = Building.Ports(r, doc, part.Id).First(p => p.Kind == PortKind.Connect);
            var duct = Building.Items(r, doc, part.Id, bp, 6).First(i => i.TypeId == DuctType.TypeId);
            var built = Building.Apply(r, doc, part.Id, bp, duct)!;
            Assert.Equal("extract", built.Doc.Get(built.SelectId)!.Str("kind", ""));
        }

        [Fact]
        public void SplitAirConditionerDrainsCondensateAndTakesAPowerCable()
        {
            var r = Fx.Reg();
            Assert.Equal(PortMedia.Waste, MediumOf(r, "bs.cool.split", "waste"));
            Assert.Equal(PortMedia.ElectricInstallation, MediumOf(r, "bs.cool.split", "cable"));
            Assert.Equal("waste", DockOnLineEnd(r, "bs.pipe.waste", "bs.cool.split"));
            var texts = r.Render(r.WithView(Fx.Item(r, "bs.cool.split"), "schematic", Fx.Ctx()), Fx.Ctx());
            Assert.Contains(texts, p => p.Tag == "snowflake");
            Assert.Contains(texts, p => p.Tag == "refrigerant");
        }

        [Fact]
        public void LabelsOfMetersAndTheFireDamperFollowTheUiLanguage()
        {
            var r = Fx.Reg();
            string Texts(string item, string view, Lang lang)
            {
                var o = r.WithView(Fx.Item(r, item), view, Fx.Ctx());
                return string.Join(",", r.TypeOf(o).Render(o, Fx.Ctx(lang)).OfType<TextPrim>().Select(t => t.Text));
            }
            Assert.Contains("HM", Texts("bs.meter.heat", "plan", Lang.En));
            Assert.Contains("WMZ", Texts("bs.meter.heat", "plan", Lang.De));
            Assert.Contains("WM", Texts("bs.meter.water", "schematic", Lang.En));
            Assert.Contains("WZ", Texts("bs.meter.water", "schematic", Lang.De));
            Assert.Contains("FD", Texts("bs.vent.firedamper", "schematic", Lang.En));
            Assert.Contains("BSK", Texts("bs.vent.firedamper", "schematic", Lang.De));
            Assert.Contains("PS", Texts("bs.heat.buffer", "section", Lang.De));
        }

        [Fact]
        public void EveryNewElementOffersItsLineAtEveryPort()
        {
            // Build "+": every port of every new element offers its connector, which starts docked on the port.
            var r = Fx.Reg();
            foreach (var id in NewTypes)
            {
                var o = Fx.Item(r, id, new Vec2(500, 400));
                var doc = Page(o);
                var ports = Building.Ports(r, doc, o.Id).Where(p => p.Kind == PortKind.Connect).ToList();
                var expected = Fx.Anchors(r, o, AnchorRole.Port).Select(a => a.Id).Where(p => p != "cable").ToList();
                foreach (var pid in expected)
                {
                    var port = ports.SingleOrDefault(p => p.AnchorId == pid);
                    Assert.True(port is not null, $"{id} {pid}: no \"+\"");
                    var connector = Building.Items(r, doc, o.Id, port!, 6).FirstOrDefault(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Points);
                    Assert.True(connector is not null, $"{id} {pid}: no connector offered");
                    var built = Building.Apply(r, doc, o.Id, port!, connector!)!;
                    var line = built.Doc.Get(built.SelectId)!;
                    Assert.Equal(o.Id, line.Attach![0]!.ObjectId);
                    Assert.Equal(pid, line.Attach[0]!.AnchorId);
                    Assert.True(PortMedia.Fits(Building.LineMedium(r, built.Doc, line), port!.Medium), $"{id} {pid}");
                }
            }
        }

        [Fact]
        public void NewTypesRoundTripThroughJson()
        {
            var r = Fx.Reg();
            var objs = NewTypes.Select((id, i) => Fx.Item(r, id, new Vec2(i * 30, 0))).ToList();
            objs[0] = objs[0].WithParam("service", "water");
            objs[NewTypes.ToList().IndexOf("bs.vent.damper")] = objs[NewTypes.ToList().IndexOf("bs.vent.damper")].WithParam("kind", "exhaust");
            var doc = Page(objs.ToArray());
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            foreach (var o in objs)
            {
                var b = back.Get(o.Id)!;
                Assert.Equal(o.Type, b.Type);
                Assert.Equal(o.Params.OrderBy(k => k.Key), b.Params.OrderBy(k => k.Key));
            }
        }
    }
}
