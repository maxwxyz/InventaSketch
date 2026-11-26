using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Stub wire that hops at crossings (rails along its points) and a junction dot element with one port.</summary>
    internal sealed class HopModule : IModule
    {
        public string Id => "hop";
        public I18nText Name => new("Hop test");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new HopWire(), new Dot() };
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();

        internal sealed class HopWire : ObjectType
        {
            public override string Id => "hop.wire";
            public override I18nText Name => new("Wire");
            public override GeometryKind Geometry => GeometryKind.Points;
            public override bool InsertablePoints => true;
            public override bool HopsAtCrossings => true;
            public override string? LineMedium(SketchObject line) => PortMedia.Electric;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.Polyline(ctx.Doc.ResolvePoints(o)) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                var p = ctx.Doc.ResolvePoints(o);
                for (var i = 0; i + 1 < p.Count; i++) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i);
            }
        }

        internal sealed class Dot : ObjectType
        {
            public override string Id => "hop.dot";
            public override I18nText Name => new("Junction dot");
            public override Size2 DefaultSize => new(2, 2);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) { yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "1"); }
        }
    }

    /// <summary>Junction ⇄ bridge (maintainer 2026-11-26): <see cref="Crossings.Disconnect"/> and <see cref="Crossings.ConnectBetween"/>.</summary>
    public class BridgeJunctionTests
    {
        private static readonly Registry Reg = new Registry().Register(new HopModule());

        private static SketchObject Wire(string id, params Vec2[] p) => new() { Id = id, Type = "hop.wire", Points = p };

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static IEnumerable<SketchObject> Wires(SketchDocument doc) => doc.Objects.Values.Where(o => o.Type == "hop.wire");

        [Fact]
        public void ConnectedCrossingBecomesABridgeAgain()
        {
            var v = Wire("v", new Vec2(50, 0), new Vec2(50, 100));
            var h = Wire("h", new Vec2(0, 50), new Vec2(100, 50));
            var doc = Doc(v, h);
            var connected = Crossings.ConnectBetween(Reg, doc, "h", "v");
            Assert.Equal(3, Wires(connected).Count());
            Assert.Empty(Crossings.Between(Reg, connected, "h", "v"));
            var j = Assert.Single(Crossings.Junctions(Reg, connected, connected.Get("v")!));
            Assert.Equal("v", j.Host);
            Assert.Equal(2, j.Ends.Count);
            Assert.True(Crossings.CanDisconnect(Reg, connected, j));
            // The same junction seen from one of the halves.
            Assert.Equal(j.At, Assert.Single(Crossings.Junctions(Reg, connected, connected.Get("h")!)).At);

            var bridged = Crossings.Disconnect(Reg, connected, j, keep: "h");
            Assert.Equal(new[] { "h", "v" }, Wires(bridged).Select(w => w.Id).OrderBy(x => x));
            var line = bridged.Get("h")!;
            Assert.Equal(new[] { new Vec2(0, 50), new Vec2(100, 50) }, line.Points);
            Assert.True(line.Attach is null || line.Attach.All(a => a is null));
            var c = Assert.Single(Crossings.Find(Reg, bridged, line));
            Assert.Equal("v", c.Other);
            Assert.Empty(Crossings.Junctions(Reg, bridged, bridged.Get("v")!));
        }

        [Fact]
        public void ABranchIsCarriedBeyondTheOtherLine()
        {
            var v = Wire("v", new Vec2(50, 0), new Vec2(50, 100));
            var (doc, branch) = Polylines.Branch(Reg, Doc(v), "v", new Vec2(50, 50), new Vec2(90, 50));
            var j = Assert.Single(Crossings.Junctions(Reg, doc, doc.Get(branch!)!));
            var bridged = Crossings.Disconnect(Reg, doc, j);
            var b = bridged.Get(branch!)!;
            Assert.Equal(50 - Crossings.BridgeOvershoot, b.Points![0].X, 6);
            Assert.True(b.Attach is null || b.Attach[0] is null);
            Assert.Equal("v", Assert.Single(Crossings.Find(Reg, bridged, b)).Other);
        }

        [Fact]
        public void ABentPairCrossesBesideItsCorner()
        {
            var v = Wire("v", new Vec2(50, 0), new Vec2(50, 100));
            var doc = Doc(v);
            var rail = Reg.PageAnchors(v, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Rail);
            var at = new Vec2(50, 50);
            var a = Wire("a", new Vec2(20, 30), at) with { Attach = new[] { null, Attachments.For(v, at, rail) } };
            var b = Wire("b", at, new Vec2(80, 60)) with { Attach = new[] { Attachments.For(v, at, rail), null } };
            doc = Edits.Add(doc, doc.Pages[0].Id, a, b);
            var j = Assert.Single(Crossings.Junctions(Reg, doc, v));
            var bridged = Crossings.Disconnect(Reg, doc, j, keep: "a");
            Assert.Null(bridged.Get("b"));
            var joined = bridged.Get("a")!;
            Assert.Equal(3, joined.Points!.Count);
            Assert.Equal(new Vec2(50 + Crossings.BridgeOvershoot, 50), joined.Points[1]);
            Assert.Equal("v", Assert.Single(Crossings.Find(Reg, bridged, joined)).Other);
        }

        [Fact]
        public void AJunctionDotWithFourWiresBecomesTwoCrossingLines()
        {
            var dot = new SketchObject { Id = "dot", Type = "hop.dot", Transform = new Transform(50, 50), Size = new Size2(2, 2) };
            var doc = Doc(dot);
            var port = Reg.PageAnchors(dot, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port);
            var c = port.P;
            Attachment On() => Attachments.For(dot, c, port);
            var left = Wire("l", new Vec2(0, 50), c) with { Attach = new[] { null, On() } };
            var right = Wire("r", c, new Vec2(100, 50)) with { Attach = new[] { On(), null } };
            var top = Wire("t", new Vec2(50, 0), c) with { Attach = new[] { null, On() } };
            var bottom = Wire("b", new Vec2(50, 100), c) with { Attach = new[] { null, On() } };
            var full = Edits.Add(doc, doc.Pages[0].Id, left, right, top, bottom);
            var j = Assert.Single(Crossings.Junctions(Reg, full, dot));
            Assert.Equal(4, j.Ends.Count);
            Assert.True(Crossings.CanDisconnect(Reg, full, j));
            var bridged = Crossings.Disconnect(Reg, full, j);
            Assert.Null(bridged.Get("dot"));
            var lines = Wires(bridged).ToList();
            Assert.Equal(2, lines.Count);
            Assert.All(lines, l => Assert.Equal(2, l.Points!.Count));
            Assert.Single(Crossings.Find(Reg, bridged, lines[0]));

            // A dot that only joins two wires passing straight on, or two at a corner, is no crossing to make.
            var pass = Edits.Add(doc, doc.Pages[0].Id, left, right);
            Assert.False(Crossings.CanDisconnect(Reg, pass, Crossings.Junctions(Reg, pass, dot).Single()));
            var corner = Edits.Add(doc, doc.Pages[0].Id, left, top);
            Assert.False(Crossings.CanDisconnect(Reg, corner, Crossings.Junctions(Reg, corner, dot).Single()));
        }

        [Fact]
        public void LinesThatDoNotCrossStayAsTheyAre()
        {
            var a = Wire("a", new Vec2(0, 10), new Vec2(100, 10));
            var b = Wire("b", new Vec2(0, 50), new Vec2(100, 50));
            var doc = Doc(a, b);
            Assert.Empty(Crossings.Between(Reg, doc, "a", "b"));
            Assert.Same(doc, Crossings.ConnectBetween(Reg, doc, "a", "b"));
            Assert.Empty(Crossings.Junctions(Reg, doc, a));
        }
    }
}
