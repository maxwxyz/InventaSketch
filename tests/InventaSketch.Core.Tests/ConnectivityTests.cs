using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Stub elements for "select connected": components with connection ports, wires, flow steps, arrows, walls, gears.</summary>
    internal sealed class ConnectModule : IModule
    {
        public string Id => "conn";
        public I18nText Name => new("Connectivity test");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new PartType(), new WireType(), new FlowType(), new ArrowType(), new WallType(), new GearType(), new ShaftType() };
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();

        /// <summary>A component with electric ports "a" (left) and "b" (right).</summary>
        internal sealed class PartType : ObjectType
        {
            public override string Id => "conn.part";
            public override I18nText Name => new("Part");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Point(AnchorRole.Port, new Vec2(-o.Size.W / 2, 0), "a");
                yield return Anchor.Point(AnchorRole.Port, new Vec2(o.Size.W / 2, 0), "b");
            }
            public override PortConnection? Connection(SketchObject o, Anchor port) => PortConnection.Of(PortMedia.Electric);
        }

        internal abstract class LineBase : ObjectType
        {
            public override GeometryKind Geometry => GeometryKind.Points;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.Polyline(ctx.Doc.ResolvePoints(o)) };
        }

        internal sealed class WireType : LineBase
        {
            public override string Id => "conn.wire";
            public override I18nText Name => new("Wire");
            public override string? LineMedium(SketchObject line) => PortMedia.Electric;
        }

        /// <summary>A flowchart step: chains with arrows, ports on the four sides.</summary>
        internal sealed class FlowType : ObjectType
        {
            public override string Id => "conn.flow";
            public override I18nText Name => new("Step");
            public override string? ChainConnector(SketchObject o) => "conn.arrow";
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Point(AnchorRole.Port, new Vec2(0, -o.Size.H / 2), "top");
                yield return Anchor.Point(AnchorRole.Port, new Vec2(0, o.Size.H / 2), "bottom");
            }
        }

        /// <summary>An annotation arrow: no medium.</summary>
        internal sealed class ArrowType : LineBase
        {
            public override string Id => "conn.arrow";
            public override I18nText Name => new("Arrow");
            public override bool DocksOnSurface => true;
        }

        /// <summary>A wall: joins walls whose ends touch its ends.</summary>
        internal sealed class WallType : LineBase
        {
            public override string Id => "conn.wall";
            public override I18nText Name => new("Wall");
            public override bool DependsOnNeighbours => true;
            public override bool UsesPlanScale => true;
            public override bool InsertablePoints => true;
            public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            {
                var ends = new[] { o.Points![0], o.Points[o.Points.Count - 1] };
                foreach (var w in ctx.Doc.Objects.Values.Where(x => x.Type == Id && x.Id != o.Id))
                    if (ends.Any(e => e.DistanceTo(w.Points![0]) < 0.25 || e.DistanceTo(w.Points[w.Points.Count - 1]) < 0.25)) yield return w.Id;
            }
        }

        /// <summary>A gear: pitch circle and hub.</summary>
        internal sealed class GearType : ObjectType
        {
            public override string Id => "conn.gear";
            public override I18nText Name => new("Gear");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.Ellipse(0, 0, o.Size.W / 2, o.Size.H / 2) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, o.Size.W / 2);
                yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero);
            }
        }

        /// <summary>A shaft seen from the side: its axis carries hubs.</summary>
        internal sealed class ShaftType : ObjectType
        {
            public override string Id => "conn.shaft";
            public override I18nText Name => new("Shaft");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.Line(-o.Size.W / 2, 0, o.Size.W / 2, 0) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Segment(AnchorRole.Axis, new Vec2(-o.Size.W / 2, 0), new Vec2(o.Size.W / 2, 0));
            }
        }
    }

    public class ConnectivityTests
    {
        private static readonly Registry Reg = new Registry().Register(new ConnectModule());

        private static SketchObject Box(string type, string id, double x, double y, double w = 20, double h = 10)
            => new() { Id = id, Type = type, Transform = new Transform(x, y), Size = new Size2(w, h) };

        private static SketchObject Line(string type, string id, Vec2 a, Vec2 b, Attachment? from = null, Attachment? to = null)
            => new() { Id = id, Type = type, Points = new[] { a, b }, Attach = new[] { from, to } };

        private static Attachment Port(string obj, string port, double u) => new(obj, u, 0) { AnchorId = port };

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        /// <summary>Source S → wire 1 → junction part J; J → wires 2, 3 → loads L, M; an arrow on L and a lone part X stay out.</summary>
        private static SketchDocument Star() => Doc(
            Box("conn.part", "S", 20, 50), Box("conn.part", "J", 80, 50), Box("conn.part", "L", 140, 20), Box("conn.part", "M", 140, 80), Box("conn.part", "X", 200, 50),
            Line("conn.wire", "w1", new(30, 50), new(70, 50), Port("S", "b", 0.5), Port("J", "a", -0.5)),
            Line("conn.wire", "w2", new(90, 50), new(130, 20), Port("J", "b", 0.5), Port("L", "a", -0.5)),
            // A branch docked on the rail of wire 2 (T joint) reaches M.
            Line("conn.wire", "w3", new(110, 35), new(130, 80), new Attachment("w2", 0.5, 0) { AnchorId = "rail0" }, Port("M", "a", -0.5)),
            Line("conn.arrow", "note", new(160, 0), new(140, 20), null, new Attachment("L", 0, 0)));

        [Fact]
        public void NetFollowsWiresThroughJunctionsAndBranches()
        {
            var doc = Star();
            var net = Connectivity.Connected(Reg, doc, "w1", ConnectKind.Net);
            Assert.Equal("w1", net[0]);
            Assert.Equal(new[] { "J", "L", "M", "S", "w1", "w2", "w3" }, net.OrderBy(x => x, StringComparer.Ordinal));
            Assert.DoesNotContain("note", net);
            Assert.DoesNotContain("X", net);
            Assert.Equal(ConnectKind.Net, Connectivity.AutoKind(Reg, doc, "w1"));
            // From a component the same net (it continues through the elements on it).
            Assert.Equal(net.Count, Connectivity.Connected(Reg, doc, "M", ConnectKind.Auto).Count);
            Assert.Equal(new[] { "X" }, Connectivity.Connected(Reg, doc, "X", ConnectKind.Net));
        }

        [Fact]
        public void LoopOfACircuitLeavesOutDanglingLines()
        {
            // Battery B → w1 → R → w2 → back to B; w3 leaves R to Z (a stub, not on the loop).
            var doc = Doc(
                Box("conn.part", "B", 20, 50), Box("conn.part", "R", 100, 50), Box("conn.part", "Z", 100, 120),
                Line("conn.wire", "w1", new(30, 50), new(90, 50), Port("B", "b", 0.5), Port("R", "a", -0.5)),
                Line("conn.wire", "w2", new(110, 50), new(10, 50), Port("R", "b", 0.5), Port("B", "a", -0.5)),
                Line("conn.wire", "w3", new(90, 50), new(90, 120), Port("R", "a", -0.5), Port("Z", "a", -0.5)));
            var loop = Connectivity.Connected(Reg, doc, "w1", ConnectKind.Loop);
            Assert.Equal(new[] { "B", "R", "w1", "w2" }, loop.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal("w1", loop[0]);
            Assert.Equal(new[] { "w3" }, Connectivity.Connected(Reg, doc, "w3", ConnectKind.Loop));
            Assert.Contains(Connectivity.Options(Reg, doc, "w1"), o => o.Kind == ConnectKind.Loop);
            Assert.DoesNotContain(Connectivity.Options(Reg, doc, "w3"), o => o.Kind == ConnectKind.Loop);
        }

        [Fact]
        public void GearTrainOnAShaftMovesAlong()
        {
            // Gear A sits on the (vertical) shaft, gear B meshes with A (pitch radii 10 + 5 = centre distance 15); gear C is apart.
            var doc = Doc(
                Box("conn.shaft", "shaft", 50, 50, 80, 4) with { Transform = new Transform(50, 50, 90) },
                Box("conn.gear", "A", 50, 50, 20, 20), Box("conn.gear", "B", 65, 50, 10, 10), Box("conn.gear", "C", 150, 150, 10, 10));
            var moves = Connectivity.Connected(Reg, doc, "B", ConnectKind.Coupled);
            Assert.Equal("B", moves[0]);
            Assert.Equal(new[] { "A", "B", "shaft" }, moves.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(ConnectKind.Coupled, Connectivity.AutoKind(Reg, doc, "shaft"));
            Assert.Equal(new[] { "C" }, Connectivity.Connected(Reg, doc, "C", ConnectKind.Auto));
            Assert.Empty(Connectivity.Options(Reg, doc, "C"));
        }

        /// <summary>Steps F1 → F2 → F3 and a loop back F3 → F2.</summary>
        private static SketchDocument Flowchart() => Doc(
            Box("conn.flow", "F1", 50, 20), Box("conn.flow", "F2", 50, 60), Box("conn.flow", "F3", 50, 100),
            Line("conn.arrow", "a1", new(50, 25), new(50, 55), Port("F1", "bottom", 0.5), Port("F2", "top", -0.5)),
            Line("conn.arrow", "a2", new(50, 65), new(50, 95), Port("F2", "bottom", 0.5), Port("F3", "top", -0.5)),
            Line("conn.arrow", "back", new(50, 105), new(50, 55), Port("F3", "bottom", 0.5), Port("F2", "top", -0.5)));

        [Fact]
        public void FollowingElementsGoDownstreamWithTheirConnectors()
        {
            var doc = Flowchart();
            Assert.Equal(ConnectKind.Chain, Connectivity.AutoKind(Reg, doc, "F1"));
            Assert.Equal(ConnectKind.Chain, Connectivity.AutoKind(Reg, doc, "a1"));
            Assert.Equal(new[] { "F1", "a1", "F2", "a2", "F3", "back" }, Connectivity.Connected(Reg, doc, "F1", ConnectKind.Chain));
            var fromF2 = Connectivity.Connected(Reg, doc, "F2", ConnectKind.Chain);
            Assert.DoesNotContain("F1", fromF2);
            Assert.DoesNotContain("a1", fromF2);
            // A connector selects what follows it.
            Assert.Equal(new[] { "a2", "F3", "back", "F2" }, Connectivity.Connected(Reg, doc, "a2", ConnectKind.Chain));
        }

        [Fact]
        public void LoopOfAFlowchartIsWhatLeadsBack()
        {
            var doc = Flowchart();
            var loop = Connectivity.Connected(Reg, doc, "F3", ConnectKind.Loop);
            Assert.Equal("F3", loop[0]);
            Assert.Equal(new[] { "F2", "F3", "a2", "back" }, loop.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(new[] { "F1" }, Connectivity.Connected(Reg, doc, "F1", ConnectKind.Loop));
            Assert.Equal(new[] { "a1" }, Connectivity.Connected(Reg, doc, "a1", ConnectKind.Loop));
            Assert.Equal(4, Connectivity.Connected(Reg, doc, "back", ConnectKind.Loop).Count);
            var kinds = Connectivity.Options(Reg, doc, "F2").Select(o => o.Kind).ToList();
            Assert.Equal(new[] { ConnectKind.Chain, ConnectKind.Loop }, kinds);
        }

        [Fact]
        public void RoomWallsAndTheirClosedLoop()
        {
            // A room of four walls, a wall sticking out from a corner, and a wall elsewhere.
            Vec2 a = new(0, 0), b = new(100, 0), c = new(100, 60), d = new(0, 60);
            var doc = Doc(
                Line("conn.wall", "n", a, b), Line("conn.wall", "e", b, c), Line("conn.wall", "s", c, d), Line("conn.wall", "w", d, a),
                Line("conn.wall", "out", c, new(160, 60)), Line("conn.wall", "far", new(300, 0), new(300, 80)));
            Assert.Equal(ConnectKind.Walls, Connectivity.AutoKind(Reg, doc, "n"));
            var walls = Connectivity.Connected(Reg, doc, "n", ConnectKind.Walls);
            Assert.Equal(new[] { "e", "n", "out", "s", "w" }, walls.OrderBy(x => x, StringComparer.Ordinal));
            var room = Connectivity.Connected(Reg, doc, "n", ConnectKind.Loop);
            Assert.Equal(new[] { "e", "n", "s", "w" }, room.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(new[] { "out" }, Connectivity.Connected(Reg, doc, "out", ConnectKind.Loop));
            Assert.Equal(new[] { "far" }, Connectivity.Connected(Reg, doc, "far", ConnectKind.Walls));
        }

        [Fact]
        public void LabelsAreBilingualAndUnknownIdsGiveNothing()
        {
            foreach (var k in Connectivity.Offered)
            {
                Assert.False(string.IsNullOrEmpty(Connectivity.Label(k).De));
                Assert.NotEqual(Connectivity.Label(k).En, Connectivity.Label(k).De);
                Assert.False(string.IsNullOrEmpty(Connectivity.Hint(k).De));
            }
            Assert.Empty(Connectivity.Connected(Reg, Star(), "nope", ConnectKind.Net));
            Assert.Equal(ConnectKind.Auto, Connectivity.AutoKind(Reg, Star(), "X"));
        }
    }
}
