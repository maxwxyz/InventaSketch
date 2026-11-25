using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>Sample circuit: pump with tank, relief valve, 4/3 valve, double-acting cylinder and lines docked to ports.</summary>
    internal static class Circuit
    {
        public static (Registry R, SketchDocument Doc) Build()
        {
            var r = Fx.Reg();
            SketchObject Ref(SketchObject o, string designator) => o.WithParam("ref", designator);
            var cyl = Ref(Fx.Item(r, "fluid.cylinder.double", new Vec2(150, 40)), "1A1");
            var valve = Ref(Fx.Item(r, "fluid.valve.43", new Vec2(140, 75)), "1V1").WithParam("showPorts", true);
            var pump = Ref(Fx.Item(r, "fluid.pump", new Vec2(100, 120)), "0P1");
            var relief = Ref(Fx.Item(r, "fluid.relief", new Vec2(85, 97.5)), "0V1") with { Transform = new Transform(85, 97.5, 270) };
            var tank1 = Fx.Item(r, "fluid.tank", new Vec2(97.5, 135));
            var tank2 = Fx.Item(r, "fluid.tank", new Vec2(70, 112.5));
            var tank3 = Fx.Item(r, "fluid.tank", new Vec2(155, 95));
            var dot = Fx.Item(r, "fluid.junction", new Vec2(97.5, 97.5));
            var (doc, pageId) = Fx.Doc(cyl, valve, pump, relief, tank1, tank2, tank3, dot);
            SketchObject Line(Vec2 a, Vec2 b, string item = "fluid.line") => r.Create(r.Item(item)!, Vec2.Zero) with { Points = new[] { a, b } };
            var lines = new[]
            {
                Line(new Vec2(97.5, 115), new Vec2(97.5, 97.5)),   // pump P -> junction
                Line(new Vec2(97.5, 97.5), new Vec2(137.5, 80)),   // junction -> valve P
                Line(new Vec2(97.5, 97.5), new Vec2(92.5, 97.5)),  // junction -> relief P
                Line(new Vec2(77.5, 97.5), new Vec2(70, 110)),     // relief T -> tank
                Line(new Vec2(97.5, 125), new Vec2(97.5, 132.5)),  // pump S -> tank
                Line(new Vec2(137.5, 70), new Vec2(130, 45)),      // valve A -> cylinder A
                Line(new Vec2(142.5, 70), new Vec2(155, 45)),      // valve B -> cylinder B
                Line(new Vec2(142.5, 80), new Vec2(155, 92.5)),    // valve T -> tank
            };
            doc = Edits.Add(doc, pageId, lines);
            foreach (var l in lines)
            {
                doc = FluidModule.ConnectToPort(r, doc, l.Id, 0);
                doc = FluidModule.ConnectToPort(r, doc, l.Id, 1);
            }
            return (r, doc);
        }
    }

    public class LineTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void EveryLineEndOfTheCircuitIsDockedToANamedPort()
        {
            var (_, doc) = Circuit.Build();
            var lines = doc.Objects.Values.Where(o => o.Type == "fluid.line").ToList();
            Assert.Equal(8, lines.Count);
            Assert.All(lines, l => Assert.All(l.Attach!, a => Assert.False(string.IsNullOrEmpty(a!.AnchorId))));
        }

        [Fact]
        public void LineEndSnapsToAValvePortWithPortPriority()
        {
            var r = Fx.Reg();
            var valve = Fx.Item(r, "fluid.valve.43", new Vec2(50, 50)) with { Transform = new Transform(50, 50, 30) };
            var line = Fx.Item(r, "fluid.line", new Vec2(100, 100));
            var (doc, _) = Fx.Doc(valve, line);
            var port = Fx.Port(r, doc, valve, "B").P;
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { line.Id }, new RenderContext(doc), Opt);
            var drag = port + new Vec2(1.1, -0.8);
            var res = Snapper.SnapPoint(drag, targets, Opt);
            Assert.Equal("port", res.Kind);
            Assert.True(port.DistanceTo(drag + res.Delta) < 1e-9);
            var attach = Attachments.FromSnap(doc, drag + res.Delta, res);
            Assert.Equal("B", attach!.AnchorId);
        }

        [Fact]
        public void DockedLineFollowsTheSymbolAndItsPortChanges()
        {
            var r = Fx.Reg();
            var valve = Fx.Item(r, "fluid.valve.42", new Vec2(100, 50));
            var line = Fx.Item(r, "fluid.line", Vec2.Zero) with { Points = new[] { new Vec2(60, 90), new Vec2(80, 80) } };
            var (doc, _) = Fx.Doc(valve, line);
            var p = Fx.Port(r, doc, valve, "P").P;
            doc = Edits.SetPoint(doc, line.Id, 1, p + new Vec2(0.8, 0.9));
            Assert.Same(doc, FluidModule.ConnectToPort(r, doc, line.Id, 0));
            doc = FluidModule.ConnectToPort(r, doc, line.Id, 1);
            Assert.Equal("P", doc.Get(line.Id)!.Attach![1]!.AnchorId);
            Assert.True(p.DistanceTo(doc.ResolvePoints(doc.Get(line.Id)!)[1]) < 1e-9);

            doc = Edits.Move(doc, new[] { valve.Id }, new Vec2(10, -5));
            Assert.True((p + new Vec2(10, -5)).DistanceTo(doc.ResolvePoints(doc.Get(line.Id)!)[1]) < 1e-9);

            // Flip the spring side: port P moves to the other box; the refreshed attachment follows it by id.
            var v = doc.Get(valve.Id)!;
            var flipped = Edits.Replace(doc, Fx.Action(r, v, "fluid.valve.flip").Apply(v));
            var moved = Fx.Port(r, flipped, flipped.Get(valve.Id)!, "P").P;
            Assert.True(moved.DistanceTo(p + new Vec2(10, -5)) > 5);
            var refreshed = Attachments.Refresh(r, flipped);
            Assert.True(moved.DistanceTo(refreshed.ResolvePoints(refreshed.Get(line.Id)!)[1]) < 1e-9);
        }

        [Fact]
        public void OrthogonalLinesLeavePortsPerpendicular()
        {
            var (r, doc) = Circuit.Build();
            var toCylA = doc.Objects.Values.Single(o => o.Type == "fluid.line" && o.Attach![1]!.AnchorId == "A" && doc.Get(o.Attach[1]!.ObjectId)!.Type == "fluid.cylinder.double");
            var pts = ((PathPrim)r.TypeOf(toCylA).Render(toCylA, new RenderContext(doc)).Single()).Segments.Select(s => s.P).ToList();
            Assert.Equal(new[] { new Vec2(137.5, 70), new Vec2(137.5, 57.5), new Vec2(130, 57.5), new Vec2(130, 45) }, pts);
            // Relief valve rotated by 270 degrees: its T port exits horizontally.
            var reliefT = doc.Objects.Values.Single(o => o.Type == "fluid.line" && o.Attach![0]!.AnchorId == "T" && doc.Get(o.Attach[0]!.ObjectId)!.Type == "fluid.valve.relief");
            var rp = ((PathPrim)r.TypeOf(reliefT).Render(reliefT, new RenderContext(doc)).Single()).Segments.Select(s => s.P).ToList();
            Assert.Equal(new[] { new Vec2(77.5, 97.5), new Vec2(70, 97.5), new Vec2(70, 110) }, rp.Select(v => new Vec2(Math.Round(v.X, 6), Math.Round(v.Y, 6))));
        }

        [Fact]
        public void LinesAreRailsAndJunctionDotsDockOnThem()
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, "fluid.line", Vec2.Zero) with { Points = new[] { new Vec2(100, 100), new Vec2(140, 100) } };
            var dot = Fx.Item(r, "fluid.junction", new Vec2(117.6, 98.9));
            var (doc, _) = Fx.Doc(line, dot);
            var ctx = new RenderContext(doc);
            Assert.Single(r.PageAnchors(line, ctx), a => a.Role == AnchorRole.Rail);
            var res = Snapper.SnapMove(r.PageAnchors(dot, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { dot.Id }, ctx, Opt), Opt);
            Assert.Equal("rail", res.Kind);
            Assert.Equal(100, 98.9 + res.Delta.Y, 9);
        }

        [Fact]
        public void LineActionsCycleKindAndRouting()
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, "fluid.line");
            // Line type and routing are choices (dropdowns), the current option marked.
            var kind = Fx.Action(r, line, "fluid.line.kind=pilot");
            Assert.Equal("fluid.line.kind=working", Assert.Single(Fx.Options(r, line, "fluid.line.kind"), a => a.IsActive).Id);
            Assert.Equal("pilot", kind.Apply(line).Str("kind", ""));
            Assert.True(Fx.Action(r, line, "fluid.line.route=orthogonal").IsActive);
            var route = Fx.Action(r, line, "fluid.line.route=free");
            Assert.Equal(IconIds.RouteFree, route.Icon);
            var free = route.Apply(line) with { Points = new[] { new Vec2(0, 0), new Vec2(20, 10) } };
            Assert.Equal(2, ((PathPrim)Fx.Render(r, free).Single()).Segments.Count);
            Assert.Equal(4, ((PathPrim)Fx.Render(r, free.WithParam("route", "orthogonal")).Single()).Segments.Count);
        }
    }

    public class ExportTests
    {
        [Fact]
        public void ConnectionListJoinsPortsByLines()
        {
            var (r, doc) = Circuit.Build();
            var objects = doc.Objects.Values.ToList();
            var exporter = Exports.For(r, doc, objects).Single(e => e.Id == "fluid.connections");
            Assert.Equal("csv", exporter.Extension);
            Assert.False(string.IsNullOrEmpty(exporter.Preserves.De));
            var csv = exporter.Export(r, doc, objects, Lang.En);
            var rows = csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Connection,Line type,Ports", rows[0]);
            Assert.Equal(7, rows.Length);
            Assert.Equal("C1,working,\"0P1.P; 0V1.P; 1V1.P\"", rows[1]);
            Assert.Equal("C2,working,\"0P1.S; Tank / reservoir.T\"", rows[2]);
            Assert.Equal("C3,working,\"0V1.T; Tank / reservoir.T\"", rows[3]);
            Assert.Equal("C4,working,\"1A1.A; 1V1.A\"", rows[4]);
            Assert.Equal("C5,working,\"1A1.B; 1V1.B\"", rows[5]);
            Assert.Equal("C6,working,\"1V1.T; Tank / reservoir.T\"", rows[6]);
            Assert.StartsWith("Verbindung,Leitungsart,Anschlüsse", exporter.Export(r, doc, objects, Lang.De));
        }

        [Fact]
        public void PneumaticReturnPortIsExportedAsR()
        {
            var (r, doc) = Circuit.Build();
            var valve = doc.Objects.Values.Single(o => o.Type == "fluid.valve.directional");
            doc = Edits.Replace(doc, valve.WithParam("medium", "pneumatic"));
            var csv = Exports.For(r, doc, doc.Objects.Values.ToList()).Single(e => e.Id == "fluid.connections").Export(r, doc, doc.Objects.Values.ToList(), Lang.En);
            Assert.Contains("1V1.R;", csv);
            Assert.DoesNotContain("1V1.T", csv);
        }

        [Fact]
        public void CrossingKeepsLinesApartAndCouplingJoinsThemWhenCoupled()
        {
            var r = Fx.Reg();
            var a = Fx.Item(r, "fluid.gauge", new Vec2(0, 0)).WithParam("ref", "G1");    // P at (0, 5)
            var b = Fx.Item(r, "fluid.gauge", new Vec2(0, 40)).WithParam("ref", "G2");   // P at (0, 45)
            var c = Fx.Item(r, "fluid.filter", new Vec2(-20, 20)).WithParam("ref", "F1"); // A at (-15, 20)
            var d = Fx.Item(r, "fluid.filter", new Vec2(20, 20)).WithParam("ref", "F2");  // P at (15, 20)
            var x = Fx.Item(r, "fluid.crossing", new Vec2(0, 20));                       // W (-5,20) E (5,20) N (0,15) S (0,25)
            var (doc, pageId) = Fx.Doc(a, b, c, d, x);
            SketchObject L(Vec2 p, Vec2 q, string kind = "working") => Fx.Item(r, "fluid.line").WithParam("kind", kind) with { Points = new[] { p, q } };
            var lines = new[]
            {
                L(new Vec2(0, 5), new Vec2(0, 15)), L(new Vec2(0, 25), new Vec2(0, 45), "pilot"),
                L(new Vec2(-15, 20), new Vec2(-5, 20)), L(new Vec2(5, 20), new Vec2(15, 20)),
            };
            doc = Edits.Add(doc, pageId, lines);
            foreach (var l in lines) { doc = FluidModule.ConnectToPort(r, doc, l.Id, 0); doc = FluidModule.ConnectToPort(r, doc, l.Id, 1); }
            var exp = new FluidModule().Exporters.Single();
            var csv = exp.Export(r, doc, doc.Objects.Values.ToList(), Lang.En);
            Assert.Contains("C1,working,\"F1.A; F2.P\"", csv);
            Assert.Contains("C2,pilot working,\"G1.P; G2.P\"", csv);

            // An uncoupled quick coupling separates, a coupled one joins.
            var k = Fx.Item(r, "fluid.coupling", new Vec2(100, 0)).WithParam("ref", "X1");
            var g = Fx.Item(r, "fluid.gauge", new Vec2(80, -5)).WithParam("ref", "G3"); // P at (80, 0)
            var h = Fx.Item(r, "fluid.flowmeter", new Vec2(125, 0)).WithParam("ref", "Q1"); // P at (120, 0)
            var (d2, p2) = Fx.Doc(k, g, h);
            var l1 = L(new Vec2(80, 0), new Vec2(92.5, 0));
            var l2 = L(new Vec2(107.5, 0), new Vec2(120, 0));
            d2 = Edits.Add(d2, p2, l1, l2);
            foreach (var l in new[] { l1, l2 }) { d2 = FluidModule.ConnectToPort(r, d2, l.Id, 0); d2 = FluidModule.ConnectToPort(r, d2, l.Id, 1); }
            Assert.Contains("\"G3.P; Q1.P; X1.1; X1.2\"", exp.Export(r, d2, d2.Objects.Values.ToList(), Lang.En));
            d2 = Edits.Replace(d2, k.WithParam("coupled", false));
            var split = exp.Export(r, d2, d2.Objects.Values.ToList(), Lang.En);
            Assert.Contains("\"G3.P; X1.1\"", split);
            Assert.Contains("\"Q1.P; X1.2\"", split);
        }

        [Fact]
        public void ExporterAppliesOnlyToDockedLines()
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, "fluid.line", new Vec2(50, 50));
            var (doc, _) = Fx.Doc(line);
            Assert.DoesNotContain(Exports.For(r, doc, doc.Objects.Values.ToList()), e => e.Id == "fluid.connections");
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void JsonRoundTrip()
        {
            var (r, doc) = Circuit.Build();
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            var valve = back.Objects.Values.Single(o => o.Type == "fluid.valve.directional");
            Assert.Equal("1V1", valve.Str("ref", ""));
            Assert.True(valve.Flag("showPorts", false));
            var line = back.Objects.Values.First(o => o.Type == "fluid.line");
            Assert.All(line.Attach!, a => Assert.NotNull(a!.AnchorId));
        }

        [Fact]
        public void ActionsRunThroughTheSharedContextActionPath()
        {
            var (r, doc) = Circuit.Build();
            var valve = doc.Objects.Values.Single(o => o.Type == "fluid.valve.directional");
            var next = ContextActions.Run(r, doc, new[] { valve.Id }, "fluid.valve.centre=tandem", null, Lang.En);
            Assert.Equal("tandem", next.Get(valve.Id)!.Str("centre", ""));
            var named = ContextActions.Run(r, doc, new[] { valve.Id }, "fluid.ref.set", "2V1", Lang.En);
            Assert.Equal("2V1", named.Get(valve.Id)!.Str("ref", ""));
        }
    }
}
