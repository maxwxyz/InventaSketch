using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    public class AnchorAndLineTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void EveryEquipmentFittingAndInstrumentHasNamedPortsInsideItsBox()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                var o = t.Create(Vec2.Zero);
                var ports = Fx.Anchors(r, o, AnchorRole.Port);
                Assert.True(ports.Count >= 1, t.Id + ": no ports");
                Assert.Equal(ports.Count, ports.Select(p => p.Id).Distinct().Count());
                Assert.All(ports, p => Assert.False(string.IsNullOrEmpty(p.Id)));
                foreach (var p in ports)
                    Assert.True(Math.Abs(p.P.X) <= o.Size.W / 2 + 1e-6 && Math.Abs(p.P.Y) <= o.Size.H / 2 + 1e-6, $"{t.Id} port {p.Id} outside the box");
            }
        }

        [Fact]
        public void MostPortsLieOnTheBoxEdgeSoLinesLeavePerpendicular()
        {
            var r = Fx.Reg();
            var onEdge = 0;
            var total = 0;
            foreach (var t in Fx.BoxTypes(r))
            {
                var o = t.Create(Vec2.Zero);
                foreach (var p in Fx.Anchors(r, o, AnchorRole.Port))
                {
                    total++;
                    if (Math.Abs(Math.Abs(p.P.X) - o.Size.W / 2) < 1e-6 || Math.Abs(Math.Abs(p.P.Y) - o.Size.H / 2) < 1e-6) onEdge++;
                }
            }
            Assert.True(onEdge >= total * 0.85, $"{onEdge} of {total} ports on the box edge");
        }

        [Fact]
        public void PortIdsAreTheSameInEveryView()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r).Where(t => t.Views.Count > 1))
            {
                var o = t.Create(Vec2.Zero);
                var ids = t.Views.Select(v => string.Join(",", Fx.Anchors(r, r.WithView(o, v.Id, Fx.Ctx()), AnchorRole.Port).Select(a => a.Id))).Distinct().ToList();
                Assert.Single(ids);
                // In the block view every nozzle sits on the block edge.
                var block = r.WithView(o, ProcessViews.Block, Fx.Ctx());
                foreach (var a in Fx.Anchors(r, block, AnchorRole.Port))
                    Assert.True(Math.Abs(Math.Abs(a.P.X) - block.Size.W / 2) < 1e-6 || Math.Abs(Math.Abs(a.P.Y) - block.Size.H / 2) < 1e-6, $"{t.Id} {a.Id} not on the block edge");
            }
        }

        [Fact]
        public void SampleFlowSheetSurvivesTheJsonRoundTripAndRendersTheSame()
        {
            var r = Fx.Reg();
            var (_, doc, _) = Samples.Distillation(r);
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            foreach (var o in doc.Objects.Values)
            {
                var a = Fx.Signature(r.Render(o, new RenderContext(doc)));
                var b = Fx.Signature(r.Render(back.Get(o.Id)!, new RenderContext(back)));
                Assert.Equal(a, b);
            }
            Assert.All(back.Objects.Values.Where(o => o.IsPointGeometry), l =>
            {
                Assert.False(string.IsNullOrEmpty(l.Attach![0]!.AnchorId));
                Assert.False(string.IsNullOrEmpty(l.Attach[l.Attach.Count - 1]!.AnchorId));
            });
        }

        [Fact]
        public void NozzlesOnTheBoxEdgeKeepTheirPlaceInTheBlockView()
        {
            var r = Fx.Reg();
            var hx = Fx.Item(r, "process.hx.shelltube");
            var block = r.WithView(hx, ProcessViews.Block, Fx.Ctx());
            Assert.Equal(Fx.Anchors(r, hx, AnchorRole.Port).Select(a => a.P), Fx.Anchors(r, block, AnchorRole.Port).Select(a => a.P));
        }

        [Fact]
        public void PortsScaleWithTheBox()
        {
            var r = Fx.Reg();
            var hx = Fx.Item(r, "process.hx.shelltube");
            var big = hx with { Size = new Size2(hx.Size.W * 2, hx.Size.H * 1.5) };
            var a = Fx.Anchors(r, hx, AnchorRole.Port).Single(p => p.Id == "S1").P;
            var b = Fx.Anchors(r, big, AnchorRole.Port).Single(p => p.Id == "S1").P;
            Assert.Equal(a.X * 2, b.X, 6);
            Assert.Equal(a.Y * 1.5, b.Y, 6);
        }

        [Fact]
        public void JacketPortsAppearWithTheJacket()
        {
            var r = Fx.Reg();
            var reactor = Fx.Item(r, "process.reactor");
            Assert.DoesNotContain(Fx.Anchors(r, reactor, AnchorRole.Port), a => a.Id == "J1");
            var jacketed = Fx.Item(r, "process.reactor.jacketed");
            Assert.Contains(Fx.Anchors(r, jacketed, AnchorRole.Port), a => a.Id == "J1");
            Assert.Contains(Fx.Anchors(r, jacketed, AnchorRole.Port), a => a.Id == "J2");
        }

        [Fact]
        public void LineEndSnapsToAPumpNozzleWithPortPriority()
        {
            var r = Fx.Reg();
            var pump = Fx.Item(r, "process.pump.centrifugal", new Vec2(50, 50)) with { Transform = new Transform(50, 50, 30) };
            var line = Fx.Item(r, "process.line", new Vec2(100, 100));
            var (doc, _) = Fx.Doc(pump, line);
            var port = Fx.Port(r, doc, pump, "D").P;
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { line.Id }, new RenderContext(doc), Opt);
            var drag = port + new Vec2(1.1, -0.8);
            var res = Snapper.SnapPoint(drag, targets, Opt);
            Assert.Equal("port", res.Kind);
            Assert.True(port.DistanceTo(drag + res.Delta) < 1e-9);
            var attach = Attachments.FromSnap(doc, drag + res.Delta, res);
            Assert.Equal("D", attach!.AnchorId);
        }

        [Fact]
        public void DockedLineFollowsTheEquipmentAndLeavesTheNozzlePerpendicular()
        {
            var r = Fx.Reg();
            var vessel = Fx.Item(r, "process.vessel.vertical", new Vec2(100, 60));
            var pump = Fx.Item(r, "process.pump.centrifugal", new Vec2(140, 110));
            var line = Fx.Item(r, "process.line", Vec2.Zero);
            var (doc, _) = Fx.Doc(vessel, pump, line);
            var n2 = Fx.Port(r, doc, vessel, "N2");
            var s = Fx.Port(r, doc, pump, "S");
            doc = Edits.SetPoint(doc, line.Id, 0, n2.P, Attachments.For(vessel, n2.P, n2));
            doc = Edits.SetPoint(doc, line.Id, 1, s.P, Attachments.For(pump, s.P, s));
            var ln = doc.Get(line.Id)!;
            Assert.Equal("N2", ln.Attach![0]!.AnchorId);
            var lt = (ProcessLineBase)r.TypeOf(ln);
            var route = lt.Path(ln, doc.ResolvePoints(ln), new RenderContext(doc));
            // Leaves the bottom nozzle downwards, enters the side suction horizontally.
            Assert.Equal(route[0].X, route[1].X, 6);
            Assert.Equal(route[route.Count - 1].Y, route[route.Count - 2].Y, 6);
            Assert.All(Enumerable.Range(0, route.Count - 1), i => Assert.True(Math.Abs(route[i].X - route[i + 1].X) < 1e-6 || Math.Abs(route[i].Y - route[i + 1].Y) < 1e-6));

            doc = Edits.Move(doc, new[] { pump.Id }, new Vec2(10, -5));
            Assert.True((s.P + new Vec2(10, -5)).DistanceTo(doc.ResolvePoints(doc.Get(line.Id)!)[1]) < 1e-9);
        }

        [Fact]
        public void LinesOfferRailsForBranchesAndInstrumentConnections()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "process.line", "process.line.signal" })
            {
                var line = Fx.Item(r, id) with { Points = new[] { new Vec2(0, 0), new Vec2(30, 20) } };
                var (doc, _) = Fx.Doc(line);
                var rails = r.TypeOf(line).Anchors(line, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Rail).ToList();
                Assert.Equal(3, rails.Count); // orthogonal route: horizontal, vertical, horizontal
                Assert.True(r.TypeOf(line).InsertablePoints);
                Assert.True(Polylines.CanExtend(r, line));
            }
        }

        [Fact]
        public void ProcessLinesHopOverEarlierProcessLines()
        {
            var r = Fx.Reg();
            var a = Fx.Item(r, "process.line") with { Points = new[] { new Vec2(0, 10), new Vec2(40, 10) } };
            var b = Fx.Item(r, "process.line") with { Points = new[] { new Vec2(20, 0), new Vec2(20, 30) } };
            var (doc, _) = Fx.Doc(a, b);
            var ctx = new RenderContext(doc);
            var later = r.TypeOf(b).Render(doc.Get(b.Id)!, ctx).Single(p => p.Tag == ProcessTags.Line);
            Assert.Contains(((PathPrim)later).Segments, s => s.Op == SegOp.C);
            Assert.Contains(b.Id, r.TypeOf(a).Dependents(doc.Get(a.Id)!, ctx));
            var off = r.TypeOf(b).Render(doc.Get(b.Id)!.WithParam("hops", false), ctx).Single(p => p.Tag == ProcessTags.Line);
            Assert.DoesNotContain(((PathPrim)off).Segments, s => s.Op == SegOp.C);
        }

        [Fact]
        public void ProcessLineKindsArrowAndLineNumber()
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, "process.line") with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0) } };
            var t = r.TypeOf(line);
            var main = (PathPrim)t.Render(line, Fx.Ctx()).Single(p => p.Tag == ProcessTags.Line);
            var secondary = (PathPrim)t.Render(line.WithParam("kind", "secondary"), Fx.Ctx()).Single(p => p.Tag == ProcessTags.Line);
            Assert.True(main.Style.EffectiveWidth > secondary.Style.EffectiveWidth);
            Assert.Contains(t.Render(line.WithParam("kind", "traced"), Fx.Ctx()), p => p.Tag == "tracer");
            Assert.Equal(DashStyle.Dash, ((PathPrim)t.Render(line.WithParam("kind", "future"), Fx.Ctx()).Single(p => p.Tag == ProcessTags.Line)).Style.EffectiveDash);
            var arrow = Fx.Action(r, line, "process.line.arrow").Apply(line);
            Assert.Equal(ArrowHead.Triangle, ((PathPrim)t.Render(arrow, Fx.Ctx()).Single(p => p.Tag == ProcessTags.Line)).Style.ArrowEnd);
            var numbered = Fx.Action(r, line, "process.line.number").Input!.Apply(line, "100-P-001");
            Assert.Contains(t.Render(numbered, Fx.Ctx()).OfType<TextPrim>(), p => p.Text == "100-P-001");
        }

        [Fact]
        public void SignalLineKindsDrawTheirMarks()
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, "process.line.signal") with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0) } };
            var t = r.TypeOf(line);
            int Marks(string kind) => t.Render(line.WithParam("kind", kind), Fx.Ctx()).Count(p => p.Tag == "mark");
            Assert.Equal(0, Marks("electric"));
            Assert.Equal(0, Marks("connection"));
            Assert.True(Marks("pneumatic") >= 8); // two slashes every 8 mm
            Assert.True(Marks("capillary") >= 8);
            Assert.True(Marks("data") >= 4);
            var electric = (PathPrim)t.Render(line, Fx.Ctx()).Single(p => p.Tag == ProcessTags.Line);
            Assert.Equal(DashStyle.Dash, electric.Style.EffectiveDash);
        }

        [Fact]
        public void SignalLineDocksOnTheBubbleAndTheControlValveActuator()
        {
            var r = Fx.Reg();
            var bubble = Fx.Item(r, "process.instrument.bubble", new Vec2(50, 20));
            var valve = Fx.Item(r, "process.valve.control", new Vec2(50, 50));
            var signal = Fx.Item(r, "process.line.signal", Vec2.Zero);
            var (doc, _) = Fx.Doc(bubble, valve, signal);
            var b = Fx.Port(r, doc, bubble, "B");
            var sp = Fx.Port(r, doc, valve, "S");
            doc = Edits.SetPoint(doc, signal.Id, 0, b.P, Attachments.For(bubble, b.P, b));
            doc = Edits.SetPoint(doc, signal.Id, 1, sp.P, Attachments.For(valve, sp.P, sp));
            var pts = doc.ResolvePoints(doc.Get(signal.Id)!);
            Assert.True(pts[0].DistanceTo(b.P) < 1e-9 && pts[1].DistanceTo(sp.P) < 1e-9);
            Assert.Equal(b.P.X, sp.P.X, 6); // bubble above the valve: one straight vertical signal line
        }
    }
}
