using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    public class PortTests
    {
        private static bool OnGrid(double v, double grid = 2.5) => Math.Abs(v / grid - Math.Round(v / grid)) < 1e-9;

        [Fact]
        public void PortsLieOnTheGridAtDefaultSizeAndOnTheBoxEdge()
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = Fx.InView(r, item, view);
                var ports = Fx.Ports(r, o);
                foreach (var p in ports)
                {
                    Assert.True(OnGrid(p.P.X) && OnGrid(p.P.Y), $"{item.Id}/{view} port {p.Id} at {p.P} is off the 2.5 mm grid");
                    var onEdge = Math.Abs(Math.Abs(p.P.X) - o.Size.W / 2) < 1e-9 || Math.Abs(Math.Abs(p.P.Y) - o.Size.H / 2) < 1e-9;
                    Assert.True(onEdge || item.TypeId == "fluid.junction", $"{item.Id}/{view} port {p.Id} at {p.P} is not on the box edge");
                }
                Assert.Equal(ports.Count, ports.Select(p => p.Id).Distinct().Count());
            }
        }

        [Fact]
        public void EveryConnectableTypeHasNamedPorts()
        {
            var r = Fx.Reg();
            var unconnected = new[] { "fluid.enclosure" };
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box && !unconnected.Contains(t.Id)))
            {
                var o = t.Create(Vec2.Zero);
                Assert.True(Fx.Ports(r, o).Count > 0 || t.Id == "fluid.actuation", t.Id + " has no ports");
            }
        }

        [Theory]
        [InlineData("fluid.pump", "P,S")]
        [InlineData("fluid.motor", "A,B")]
        [InlineData("fluid.cylinder.double", "A,B")]
        [InlineData("fluid.cylinder.single", "A")]
        [InlineData("fluid.cylinder.single.extend", "B")]
        [InlineData("fluid.valve.22", "A,P")]
        [InlineData("fluid.valve.32", "A,P,T")]
        [InlineData("fluid.valve.42", "A,B,P,T")]
        [InlineData("fluid.valve.43", "A,B,P,T")]
        [InlineData("fluid.valve.52", "A,B,R,P,S")]
        [InlineData("fluid.valve.52.pilot", "A,B,R,P,S,X,Y")]
        [InlineData("fluid.valve.53", "A,B,R,P,S")]
        [InlineData("fluid.relief", "P,T")]
        [InlineData("fluid.reducing", "P,A")]
        [InlineData("fluid.sequence", "P,A,L")]
        [InlineData("fluid.check.piloted", "P,A,X")]
        [InlineData("fluid.shuttle", "X,Y,A")]
        [InlineData("fluid.quickexhaust", "P,A,R")]
        [InlineData("fluid.tank", "T")]
        [InlineData("fluid.frl", "P,A")]
        [InlineData("fluid.crossing", "W,E,N,S")]
        [InlineData("fluid.coupling", "1,2")]
        public void PortIdsAreTheConventionalNames(string itemId, string ids)
        {
            var r = Fx.Reg();
            Assert.Equal(ids.Split(','), Fx.Ports(r, Fx.Item(r, itemId)).Select(a => a.Id));
        }

        [Fact]
        public void ValvePortIdsAreStableAcrossActuationAndPositionChanges()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.42");
            var before = Fx.Ports(r, v).Select(a => a.Id).ToList();
            foreach (var id in new[] { "fluid.valve.actLeft.next", "fluid.valve.flip", "fluid.valve.detent", "fluid.medium" })
            {
                var changed = Fx.Action(r, v, id).Apply(v);
                Assert.Equal(before, Fx.Ports(r, changed).Select(a => a.Id).Where(x => x is not ("X" or "Y")));
            }
            // 4/2 -> 4/3: same four ports, now at the centre box.
            var three = Fx.Action(r, v, "fluid.valve.ways.next").Apply(v);
            Assert.Equal("4/3", three.Str("ways", ""));
            Assert.Equal(before, Fx.Ports(r, three).Select(a => a.Id));
        }

        [Fact]
        public void PortsScaleWithTheBox()
        {
            var r = Fx.Reg();
            var cyl = Fx.Item(r, "fluid.cylinder.double");
            var a = Fx.Ports(r, cyl).Single(p => p.Id == "A").P;
            var big = cyl with { Size = new Size2(cyl.Size.W * 2, cyl.Size.H * 3) };
            var b = Fx.Ports(r, big).Single(p => p.Id == "A").P;
            Assert.Equal(a.X * 2, b.X, 9);
            Assert.Equal(a.Y * 3, b.Y, 9);
            Assert.Equal(big.Size.H / 2, b.Y, 9);
        }
    }

    public class ValveTests
    {
        private static int Boxes(Registry r, SketchObject o) => Fx.Render(r, o).Count(p => p.Tag == "box");

        [Theory]
        [InlineData("2/2", 2)]
        [InlineData("3/2", 2)]
        [InlineData("4/2", 2)]
        [InlineData("4/3", 3)]
        [InlineData("5/2", 2)]
        [InlineData("5/3", 3)]
        public void WaysGiveTheNumberOfSwitchingPositions(string ways, int boxes)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "fluid.valve.43").WithParam("ways", ways);
            Assert.Equal(boxes, Boxes(r, o));
        }

        [Fact]
        public void PortsSitAtTheRestBoxNextToTheSpring()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.42");
            // Solenoid left, spring right: ports at the right box.
            var boxes = Fx.Render(r, v).OfType<RectPrim>().Where(p => p.Tag == "box").OrderBy(b => b.X).ToList();
            var p = Fx.Ports(r, v).Single(a => a.Id == "P").P;
            Assert.InRange(p.X, boxes[1].X, boxes[1].X + boxes[1].W);
            // Flip the spring side: the ports move to the (new) spring side box.
            var flipped = Fx.Action(r, v, "fluid.valve.flip").Apply(v);
            Assert.Equal("spring", flipped.Str("actLeft", ""));
            Assert.Equal("solenoid", flipped.Str("actRight", ""));
            var fb = Fx.Render(r, flipped).OfType<RectPrim>().Where(q => q.Tag == "box").OrderBy(b => b.X).ToList();
            var fp = Fx.Ports(r, flipped).Single(a => a.Id == "P").P;
            Assert.InRange(fp.X, fb[0].X, fb[0].X + fb[0].W);
            // The spring moved with it.
            var spring = Fx.Render(r, flipped).Single(q => q.Tag == "spring");
            Assert.True(Prim.Bounds(new[] { spring })!.Value.Right <= fb[0].X + 1e-9);
        }

        [Fact]
        public void CentreVariantsDrawDifferentPassages()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.43");
            var svgs = DirectionalValves.Centres4.Select(c => SvgRenderer.RenderPrimitives(Fx.Render(r, v.WithParam("centre", c)))).ToList();
            Assert.Equal(svgs.Count, svgs.Distinct().Count());
            var closed = Fx.Render(r, v);
            Assert.Equal(4, closed.Count(p => p.Tag == "block") / 2);
            var tandem = Fx.Render(r, v.WithParam("centre", "tandem"));
            Assert.Contains(tandem, p => p.Tag == "join");
            var open = Fx.Render(r, v.WithParam("centre", "open"));
            Assert.Equal(2, open.Count(p => p.Tag == "join-dot"));

            var cycle = Fx.Action(r, v, "fluid.valve.centre.next");
            Assert.True(cycle.IsEnabled);
            Assert.Equal("tandem", cycle.Apply(v).Str("centre", ""));
            var five = v.WithParam("ways", "5/3");
            Assert.Equal("float", Fx.Action(r, five, "fluid.valve.centre.next").Apply(five).Str("centre", ""));
            Assert.False(Fx.Action(r, Fx.Item(r, "fluid.valve.42"), "fluid.valve.centre.next").IsEnabled);
        }

        [Fact]
        public void InspectorParameterChangesKeepTheDrawingScale()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.42");
            var t = (FluidType)r.TypeOf(v);
            var scale = v.Size.W / t.Design(v).W;
            var wide = r.SetParam(v, "ways", "5/3");
            Assert.Equal("5/3", wide.Str("ways", ""));
            Assert.True(wide.Size.W > v.Size.W * 1.2, "a third position widens the valve");
            Assert.Equal(scale, wide.Size.W / t.Design(wide).W, 6);
            Assert.Equal(scale, wide.Size.H / t.Design(wide).H, 6);
            Assert.Equal(v.Size, r.SetParam(wide, "ways", "4/2").Size);
        }

        [Fact]
        public void CyclingWaysKeepsTheDrawingScale()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.42");
            var t = (FluidType)r.TypeOf(v);
            var scale = v.Size.W / t.Design(v).W;
            var seen = new List<string>();
            var o = v;
            for (var i = 0; i < 6; i++)
            {
                o = Fx.Action(r, o, "fluid.valve.ways.next").Apply(o);
                seen.Add(o.Str("ways", ""));
                Assert.Equal(scale, o.Size.W / t.Design(o).W, 9);
                Assert.Equal(1, o.Size.H / t.Design(o).H, 9);
            }
            Assert.Equal(new[] { "4/3", "5/2", "5/3", "2/2", "3/2", "4/2" }, seen);
        }

        [Fact]
        public void NormallyOpenToggleOnlyForTwoAndThreePortValves()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.32");
            var toggle = Fx.Action(r, v, "fluid.valve.normal");
            Assert.True(toggle.IsEnabled);
            Assert.False(toggle.IsActive);
            var open = toggle.Apply(v);
            Assert.Equal("open", open.Str("normal", ""));
            Assert.NotEqual(SvgRenderer.RenderPrimitives(Fx.Render(r, v)), SvgRenderer.RenderPrimitives(Fx.Render(r, open)));
            Assert.False(Fx.Action(r, Fx.Item(r, "fluid.valve.43"), "fluid.valve.normal").IsEnabled);
        }

        [Fact]
        public void ActuationsAreComposedByParameters()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.43");
            var prims = Fx.Render(r, v);
            Assert.Equal(2, prims.Count(p => p.Tag == "spring"));
            Assert.Equal(4, prims.Count(p => p.Tag == "solenoid"));
            var lever = Fx.Item(r, "fluid.valve.43.lever");
            var lp = Fx.Render(r, lever);
            Assert.Contains(lp, p => p.Tag == "detent");
            Assert.Contains(lp, p => p.Tag == "operator");
            Assert.DoesNotContain(lp, p => p.Tag == "spring");
            var cycle = Fx.Action(r, v, "fluid.valve.actLeft.next");
            Assert.Equal("pilot", cycle.Apply(v).Str("actLeft", ""));
            var pilot = Fx.Item(r, "fluid.valve.52.pilot");
            Assert.Equal(2, Fx.Render(r, pilot).Count(p => p.Tag == "pilot" && p is LinePrim));
            var detent = Fx.Action(r, v, "fluid.valve.detent").Apply(v);
            Assert.Contains(Fx.Render(r, detent), p => p.Tag == "detent");
            Assert.True(detent.Size.W > v.Size.W, "the detent widens the valve instead of squeezing it");
        }

        [Fact]
        public void SeparateActuationDocksOnTheValveMount()
        {
            var r = Fx.Reg();
            var valve = Fx.Item(r, "fluid.valve.42", new Vec2(100, 50));
            var act = Fx.Item(r, "fluid.actuation", new Vec2(80.6, 50.4));
            var (doc, _) = Fx.Doc(valve, act);
            var ctx = new RenderContext(doc);
            var opt = new SnapOptions { Tolerance = 3, Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(act, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { act.Id }, ctx, opt), opt);
            Assert.Equal("mount", res.Kind);
            var mount = r.PageAnchors(valve, ctx).Single(a => a.Role == AnchorRole.Mount && a.Id == "left").P;
            var actMount = r.PageAnchors(act, ctx).Single(a => a.Role == AnchorRole.Mount).P + res.Delta;
            Assert.True(mount.DistanceTo(actMount) < 1e-9);
            var pilot = Fx.Action(r, act, "fluid.actuation.next");
            Assert.Equal("lever", pilot.Apply(act).Str("kind", ""));
        }

        [Fact]
        public void ValveTogglesOnOtherValveTypes()
        {
            var r = Fx.Reg();
            var throttle = Fx.Item(r, "fluid.throttle");
            var adj = Fx.Action(r, throttle, "fluid.adjustable").Apply(throttle);
            Assert.Contains(Fx.Render(r, adj), p => p.Tag == "adjust");
            var withCheck = Fx.Action(r, adj, "fluid.check").Apply(adj);
            Assert.Contains(Fx.Render(r, withCheck), p => p.Tag == "ball");
            Assert.Equal(-2.5, Fx.Ports(r, withCheck).Single(p => p.Id == "P").P.Y, 9);

            var check = Fx.Item(r, "fluid.check");
            var piloted = Fx.Action(r, check, "fluid.piloted").Apply(check);
            Assert.Contains(Fx.Ports(r, piloted), p => p.Id == "X");
            Assert.Contains(Fx.Render(r, Fx.Action(r, check, "fluid.spring").Apply(check)), p => p.Tag == "spring");

            var pump = Fx.Item(r, "fluid.pump");
            Assert.Contains(Fx.Render(r, Fx.Action(r, pump, "fluid.variable").Apply(pump)), p => p.Tag == "variable");
            Assert.Equal(2, Fx.Render(r, Fx.Action(r, pump, "fluid.flow.next").Apply(pump)).Count(p => p.Tag == "triangle"));

            var cyl = Fx.Item(r, "fluid.cylinder.double");
            var through = Fx.Action(r, cyl, "fluid.cyl.rod").Apply(cyl);
            Assert.Equal("double", through.Str("rod", ""));
            Assert.Contains(Fx.Render(r, Fx.Action(r, cyl, "fluid.cyl.cushion.next").Apply(cyl)), p => p.Tag == "cushion");
            var single = Fx.Item(r, "fluid.cylinder.single");
            Assert.Equal("extend", Fx.Action(r, single, "fluid.cyl.spring.next").Apply(single).Str("spring", ""));
        }
    }
}
