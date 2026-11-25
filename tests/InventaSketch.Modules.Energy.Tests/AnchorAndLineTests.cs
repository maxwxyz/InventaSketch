using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    public class AnchorTests
    {
        [Fact]
        public void EveryBoxElementHasPortsWithTheSameIdsInBothViews()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                var o = t.Create(Vec2.Zero);
                var ids = t.Views.Select(v => Fx.Anchors(r, r.WithView(o, v.Id, Fx.Ctx()), AnchorRole.Port).Select(a => a.Id!).OrderBy(x => x).ToList()).ToList();
                Assert.True(ids[0].Count > 0, t.Id + ": no ports");
                Assert.Equal(ids[0].Count, ids[0].Distinct().Count());
                Assert.Equal(ids[0], ids[1]);
                Assert.DoesNotContain("c", ids[0]); // reserved for the generic box centre
            }
        }

        [Fact]
        public void PortsSitOnTheBoxEdgeExceptBusbarTaps()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var v in t.Views)
                {
                    var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                    foreach (var a in Fx.Anchors(r, o, AnchorRole.Port))
                    {
                        var inside = Math.Abs(a.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(a.P.Y) <= o.Size.H / 2 + 1e-9;
                        Assert.True(inside, $"{t.Id} [{v.Id}] {a.Id} at {a.P}");
                        if (t.Id == "energy.grid.busbar") continue;
                        var onEdge = Math.Abs(Math.Abs(a.P.X) - o.Size.W / 2) < 1e-6 || Math.Abs(Math.Abs(a.P.Y) - o.Size.H / 2) < 1e-6;
                        Assert.True(onEdge, $"{t.Id} [{v.Id}] {a.Id} at {a.P} is not on the box edge");
                    }
                }
        }

        [Fact]
        public void EveryPortIsReachedByADrawnLead()
        {
            // A cable docked on a port meets the drawing: some primitive point lies on the port.
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var v in t.Views)
                {
                    var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                    var points = Prim.Flatten(t.Render(o, Fx.Ctx())).Where(p => p is not TextPrim).SelectMany(Prim.Points).ToList();
                    foreach (var a in Fx.Anchors(r, o, AnchorRole.Port))
                        Assert.True(points.Any(q => q.DistanceTo(a.P) < 1e-6) || t.Id == "energy.grid.busbar", $"{t.Id} [{v.Id}] port {a.Id} has no lead");
                }
        }

        [Fact]
        public void PortsKeepTheirOffsetFromTheCentreAcrossTheViewSwitch()
        {
            // A diagram switched pictorial <-> single-line keeps straight cables straight: boxes keep their centres
            // (Registry.WithView only changes the size), so a port on the top/bottom edge must keep its x offset and a
            // port on the left/right edge its y offset from the box centre.
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var item in r.Library.Where(i => i.TypeId == t.Id && i.View is null))
                {
                    var pic = r.WithView(r.Create(item, Vec2.Zero), "pictorial", Fx.Ctx());
                    var sld = r.WithView(pic, "single-line", Fx.Ctx());
                    var a = Fx.Anchors(r, pic, AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
                    var b = Fx.Anchors(r, sld, AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
                    foreach (var (id, p) in a)
                    {
                        var q = b[id];
                        var side = Math.Abs(Math.Abs(p.X) - pic.Size.W / 2) < 1e-6;
                        if (side) Assert.True(Math.Abs(p.Y - q.Y) < 1e-6, $"{item.Id} port {id}: y {p.Y} -> {q.Y}");
                        else Assert.True(Math.Abs(p.X - q.X) < 1e-6, $"{item.Id} port {id}: x {p.X} -> {q.X}");
                    }
                }
        }

        [Fact]
        public void PortsFollowParameters()
        {
            var r = Fx.Reg();
            var inv = Fx.Item(r, "energy.conv.inverter");
            Assert.Equal(new[] { "ac", "dc" }, Fx.Anchors(r, inv, AnchorRole.Port).Select(a => a.Id));
            var hybrid = Fx.Item(r, "energy.conv.inverter.hybrid");
            Assert.Equal(new[] { "ac", "dc", "bat" }, Fx.Anchors(r, hybrid, AnchorRole.Port).Select(a => a.Id));

            var bus = Fx.Item(r, "energy.grid.busbar");
            Assert.Equal(5, Fx.Anchors(r, bus, AnchorRole.Port).Count);
            var more = r.TypeOf(bus).Actions(bus, Lang.En).Single(a => a.Id == "energy.taps.more").Apply(bus);
            Assert.Equal(6, Fx.Anchors(r, more, AnchorRole.Port).Count);
            var max = bus.WithParam("taps", 12);
            Assert.False(r.TypeOf(max).Actions(max, Lang.En).Single(a => a.Id == "energy.taps.more").IsEnabled);
        }

        [Fact]
        public void BusbarOffersARailAlongTheWholeBar()
        {
            var r = Fx.Reg();
            foreach (var view in EnViews.All)
            {
                var bus = r.WithView(Fx.Item(r, "energy.grid.busbar"), view, Fx.Ctx());
                var rail = Fx.Anchors(r, bus, AnchorRole.Rail).Single();
                Assert.True(rail.P.DistanceTo(rail.Q) > bus.Size.W * 0.95, view);
                Assert.All(Fx.Anchors(r, bus, AnchorRole.Port), a => Assert.Equal(rail.P.Y, a.P.Y, 6));
            }
        }

        [Fact]
        public void ACableDockedOnAPortFollowsMovesAndViewSwitches()
        {
            var r = Fx.Reg();
            var inv = Fx.Item(r, "energy.conv.inverter", new Vec2(50, 50));
            var port = r.PageAnchors(inv, Fx.Ctx()).Single(a => a.Role == AnchorRole.Port && a.Id == "ac");
            Assert.Equal(new Vec2(50, 35), port.P);
            var cable = Fx.Item(r, "energy.line.cable") with { Points = new[] { port.P, new Vec2(50, 10) } };
            var (doc, _) = Fx.Doc(inv, cable);
            doc = Edits.AttachPoint(doc, cable.Id, 0, inv.Id, port.P, "ac");

            doc = Edits.Move(doc, new[] { inv.Id }, new Vec2(10, 0));
            Assert.Equal(new Vec2(60, 35), doc.ResolvePoints(doc.Get(cable.Id)!)[0]);

            var sld = r.WithView(doc.Get(inv.Id)!, "single-line", new RenderContext(doc));
            doc = Attachments.Refresh(r, Edits.Replace(doc, sld));
            var newPort = r.PageAnchors(doc.Get(inv.Id)!, new RenderContext(doc)).Single(a => a.Id == "ac").P;
            var end = doc.ResolvePoints(doc.Get(cable.Id)!)[0];
            Assert.True(end.DistanceTo(newPort) < 1e-6, $"{end} != {newPort}");
        }
    }

    public class LineTests
    {
        [Theory]
        [InlineData("energy.line.cable")]
        [InlineData("energy.line.pipe")]
        [InlineData("energy.line.flow")]
        public void LinesTakeTheLineLanguage(string typeId)
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, typeId);
            var (doc, _) = Fx.Doc(line);
            Assert.True(Polylines.CanInsert(r, line));
            Assert.True(Polylines.CanExtend(r, line));
            Assert.True(Polylines.CanBranch(r, doc, line));
            doc = Polylines.InsertCorner(r, doc, line.Id, 1, new Vec2(0, -10));
            Assert.Equal(3, doc.Get(line.Id)!.Points!.Count);
            var (doc2, branchId) = Polylines.Branch(r, doc, line.Id, new Vec2(-10, -5), new Vec2(-10, 20));
            Assert.NotNull(branchId);
            Assert.StartsWith("rail", doc2.Get(branchId!)!.Attach![0]!.AnchorId);
        }

        [Theory]
        [InlineData(0.4, false, VoltageLevel.Low)]
        [InlineData(1.0, false, VoltageLevel.Low)]
        [InlineData(10, false, VoltageLevel.Medium)]
        [InlineData(20, false, VoltageLevel.Medium)]
        [InlineData(110, false, VoltageLevel.High)]
        [InlineData(220, false, VoltageLevel.ExtraHigh)]
        [InlineData(380, false, VoltageLevel.ExtraHigh)]
        [InlineData(0.8, true, VoltageLevel.Dc)]
        public void VoltageLevelsAreClassified(double kv, bool dc, VoltageLevel level) => Assert.Equal(level, Voltage.Of(kv, dc));

        [Fact]
        public void CableColourFollowsTheVoltageLevel()
        {
            var r = Fx.Reg();
            string Stroke(SketchObject o) => ((PathPrim)r.Render(o, Fx.Ctx()).Single(p => p.Tag == EnTags.Cable)).Style.Stroke!;
            Assert.Equal(Voltage.Color(VoltageLevel.Low), Stroke(Fx.Item(r, "energy.line.cable.lv")));
            Assert.Equal(Voltage.Color(VoltageLevel.Medium), Stroke(Fx.Item(r, "energy.line.cable.mv")));
            Assert.Equal(Voltage.Color(VoltageLevel.High), Stroke(Fx.Item(r, "energy.line.cable.hv")));
            Assert.Equal(Voltage.Color(VoltageLevel.ExtraHigh), Stroke(Fx.Item(r, "energy.line.cable.ehv")));
            Assert.Equal(Voltage.Color(VoltageLevel.Dc), Stroke(Fx.Item(r, "energy.line.cable.dc")));
            Assert.Equal("#000000", Stroke(Fx.Item(r, "energy.line.cable.mv").WithParam("levelColors", false)));
            var data = r.Render(Fx.Item(r, "energy.line.cable.data"), Fx.Ctx()).Single(p => p.Tag == EnTags.Cable);
            Assert.Equal(DashStyle.Dash, ((PathPrim)data).Style.Dash);
        }

        [Fact]
        public void CableIsThickerInThePictorialViewAndShowsMarksAndLabels()
        {
            var r = Fx.Reg();
            var cable = Fx.Item(r, "energy.line.cable.mv");
            double Width(SketchObject o) => ((PathPrim)r.Render(o, Fx.Ctx()).Single(p => p.Tag == EnTags.Cable)).Style.StrokeWidth!.Value;
            Assert.True(Width(cable) > Width(cable with { View = "single-line" }));
            var marked = cable.WithParam("conductors", 3).WithParam("showVoltage", true).WithParam("label", "NA2XS2Y 3x1x150");
            var prims = r.Render(marked with { View = "single-line" }, Fx.Ctx(Lang.De));
            Assert.Equal(3, prims.Count(p => p.Tag == EnTags.Tick));
            Assert.Contains(prims, p => p is TextPrim { Text: "NA2XS2Y 3x1x150 · 20 kV" });
        }

        [Fact]
        public void OrthogonalRoutingDrawsRightAngles()
        {
            var r = Fx.Reg();
            var cable = Fx.Item(r, "energy.line.cable") with { Points = new[] { new Vec2(0, 0), new Vec2(20, 10) } };
            var free = (PathPrim)r.Render(cable, Fx.Ctx()).Single(p => p.Tag == EnTags.Cable);
            Assert.Equal(2, free.Segments.Count);
            var ortho = (PathPrim)r.Render(cable.WithParam("route", "orthogonal"), Fx.Ctx()).Single(p => p.Tag == EnTags.Cable);
            Assert.Equal(4, ortho.Segments.Count);
            var pts = ortho.Segments.Select(s => s.P).ToList();
            for (var i = 0; i + 1 < pts.Count; i++)
                Assert.True(Math.Abs(pts[i].X - pts[i + 1].X) < 1e-9 || Math.Abs(pts[i].Y - pts[i + 1].Y) < 1e-9);
        }

        [Fact]
        public void ACableDockedOnABusbarShowsAJunctionDot()
        {
            var r = Fx.Reg();
            var bus = Fx.Item(r, "energy.grid.busbar", new Vec2(100, 50));
            var rail = r.PageAnchors(bus, Fx.Ctx()).Single(a => a.Role == AnchorRole.Rail);
            var at = Vec2.Lerp(rail.P, rail.Q, 0.37);
            var cable = Fx.Item(r, "energy.line.cable") with { Points = new[] { at, at + new Vec2(0, 20) } };
            var (doc, _) = Fx.Doc(bus, cable);
            var attach = Attachments.For(bus, at, rail)!;
            doc = Edits.SetPoint(doc, cable.Id, 0, at, attach);
            Assert.Contains(r.Render(doc.Get(cable.Id)!, new RenderContext(doc)), p => p.Tag == EnTags.Junction);
            // Moving the busbar takes the cable end along.
            doc = Edits.Move(doc, new[] { bus.Id }, new Vec2(0, 5));
            Assert.Equal(at + new Vec2(0, 5), doc.ResolvePoints(doc.Get(cable.Id)!)[0]);
        }

        [Fact]
        public void PipeShowsTheCarrierCodeInTheSingleLineView()
        {
            var r = Fx.Reg();
            var pipe = Fx.Item(r, "energy.line.pipe.hydrogen");
            Assert.DoesNotContain(r.Render(pipe, Fx.Ctx()), p => p is TextPrim);
            var sld = r.Render(pipe with { View = "single-line" }, Fx.Ctx());
            Assert.Contains(sld, p => p is TextPrim { Text: "H₂" });
            Assert.Equal(DashStyle.DashDot, ((PathPrim)sld.Single(p => p.Tag == EnTags.Pipe)).Style.Dash);
        }

        [Fact]
        public void FlowArrowIsABandOfItsWeightWithAHead()
        {
            var r = Fx.Reg();
            var flow = Fx.Item(r, "energy.line.flow.heat").WithParam("weight", 4.0).WithParam("label", "12 MWh/a");
            var prims = r.Render(flow, Fx.Ctx());
            var band = (PathPrim)prims.Single(p => p.Tag == EnTags.Flow);
            Assert.True(band.IsClosed);
            var b = Prim.Bounds(new[] { band })!.Value;
            Assert.Equal(30, b.W, 6); // from the start to the arrow tip
            Assert.True(b.H > 4); // the head is wider than the band
            var ys = band.Segments.Where(s => s.Op != SegOp.Z).Select(s => s.P).Where(p => Math.Abs(p.X + 15) < 1e-9).Select(p => p.Y).OrderBy(y => y).ToList();
            Assert.Equal(4, ys[1] - ys[0], 6); // band width at the start
            Assert.Contains(prims, p => p is TextPrim { Text: "12 MWh/a" });
            Assert.Equal("#c62828", band.Style.Fill!.Color);
            var thin = r.Render(flow with { View = "single-line" }, Fx.Ctx());
            Assert.Contains(thin, p => p.Tag == "head");
        }

        [Fact]
        public void LinesRenderNothingWithoutTwoDistinctPoints()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "energy.line.cable", "energy.line.pipe", "energy.line.flow" })
            {
                var o = Fx.Item(r, id) with { Points = new[] { new Vec2(1, 1), new Vec2(1, 1) } };
                Assert.Empty(r.Render(o, Fx.Ctx()));
            }
        }
    }

    public class FormatTests
    {
        [Theory]
        [InlineData(430, "Wp", "430 Wp")]
        [InlineData(2580, "Wp", "2.58 kWp")]
        [InlineData(630000, "VA", "630 kVA")]
        [InlineData(40000000, "VA", "40 MVA")]
        [InlineData(2000000, "Wh", "2 MWh")]
        public void SiPrefixes(double value, string unit, string expected) => Assert.Equal(expected, Fmt.Si(value, unit, Lang.En));

        [Fact]
        public void VoltagesAndDecimalCommas()
        {
            Assert.Equal("0.4 kV", Fmt.Kv(0.4, Lang.En));
            Assert.Equal("0,4 kV", Fmt.Kv(0.4, Lang.De));
            Assert.Equal("800 V DC", Fmt.Kv(0.8, Lang.En, dc: true));
            Assert.Equal("1.5 kV DC", Fmt.Kv(1.5, Lang.En, dc: true));
            Assert.Equal("110/20/10 kV", Fmt.Ratio(Lang.En, 110, 20, 10));
        }

        [Fact]
        public void VoltagesCanBeTypedAsARatio()
        {
            Assert.Equal(new[] { 20, 0.4 }, Fmt.ParseRatio("20/0,4", 2, Lang.De));
            Assert.Equal(new[] { 110.0, 20, 10 }, Fmt.ParseRatio(" 110 / 20 / 10 kV", 3, Lang.En));
            Assert.Throws<FormatException>(() => Fmt.ParseRatio("20", 2, Lang.En));
            Assert.Throws<FormatException>(() => Fmt.ParseRatio("abc/1", 2, Lang.En));

            var r = Fx.Reg();
            var tr = Fx.Item(r, "energy.grid.transformer");
            var a = r.TypeOf(tr).Actions(tr, Lang.De).Single(x => x.Id == "energy.voltages.set");
            Assert.Equal("20/0,4", a.Input!.Value);
            var hv = a.Input.Apply(tr, "110/20");
            Assert.Equal(110, hv.Num("hv", 0), 6);
            Assert.Equal(20, hv.Num("lv", 0), 6);
            var t3 = Fx.Item(r, "energy.grid.transformer3");
            var a3 = r.TypeOf(t3).Actions(t3, Lang.En).Single(x => x.Id == "energy.voltages.set");
            Assert.Equal(30, a3.Input!.Apply(t3, "380/110/30").Num("lv", 0), 6);
        }

        [Fact]
        public void RatingsCanBeTyped()
        {
            var r = Fx.Reg();
            var bat = Fx.Item(r, "energy.storage.battery");
            var a = r.TypeOf(bat).Actions(bat, Lang.De).Single(x => x.Id == "energy.capacity.set");
            Assert.Equal(13.5, a.Input!.Apply(bat, "13,5 kWh").Num("capacity", 0), 6);
            Assert.Throws<FormatException>(() => a.Input.Apply(bat, "viel"));
        }
    }
}
