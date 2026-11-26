using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Compatible mount ids (<see cref="Anchor.Accepts"/>) for "+" docking, placement, coupling and snapping, and pitch circles
    /// that do not mesh (<see cref="IPitchMesh"/>, <see cref="Anchor.NoMesh"/>) in <see cref="Building"/> and <see cref="Snapper"/>,
    /// with small test types: a flange host, an adapter (mount "flange" on top, "tool" underneath), a tool and a plain part
    /// (mount "flange" only), gears and pulleys (hub and pitch circle radius 10) and a belt with two hubs.
    /// </summary>
    public class DockingRobustnessTests
    {
        private static readonly IReadOnlyList<string> AcceptsTool = new[] { "tool" };

        private abstract class Box : ObjectType
        {
            private readonly string _id;
            private readonly Size2 _size;
            protected Box(string id, Size2 size) { _id = id; _size = size; }
            public override string Id => _id;
            public override I18nText Name => new(_id);
            public override string Category => "t";
            public override Size2 DefaultSize => _size;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        /// <summary>A robot-flange stand-in: directed mount "flange" at the bottom edge, pointing down.</summary>
        private sealed class FlangeHost : Box
        {
            public FlangeHost() : base("t.host", new Size2(20, 20)) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Directed(AnchorRole.Mount, new Vec2(0, o.Size.H / 2), new Vec2(0, 1), "flange", 5) };
        }

        /// <summary>A part docked by its mount "flange" on top (facing up); optionally a tool side "tool" underneath.</summary>
        private sealed class MountPart : Box
        {
            private readonly bool _accepts, _toolSide;
            public MountPart(string id, bool accepts, bool toolSide) : base(id, new Size2(20, toolSide ? 10 : 16)) { _accepts = accepts; _toolSide = toolSide; }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                var top = Anchor.Directed(AnchorRole.Mount, new Vec2(0, -o.Size.H / 2), new Vec2(0, -1), "flange", 5);
                yield return _accepts ? top with { Accepts = AcceptsTool } : top;
                if (_toolSide) yield return Anchor.Directed(AnchorRole.Mount, new Vec2(0, o.Size.H / 2), new Vec2(0, 1), "tool", 5);
            }
        }

        private class Wheel : Box
        {
            public Wheel(string id = "t.gear") : base(id, new Size2(20, 20)) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Hub(Vec2.Zero, 0), Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, 10) };
        }

        private sealed class Pulley : Wheel, IPitchMesh
        {
            public Pulley() : base("t.pulley") { }
            public bool MeshesAtPitch(SketchObject o) => false;
        }

        /// <summary>A belt stand-in: hubs at both ends of its box.</summary>
        private sealed class Belt : Box
        {
            public Belt() : base("t.belt", new Size2(60, 20)) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Hub(new Vec2(-o.Size.W / 2, 0), 0, "hub0"), Anchor.Hub(new Vec2(o.Size.W / 2, 0), 0, "hub1") };
        }

        private sealed class TestModule : IModule
        {
            private static readonly string[] Ids = { "t.host", "t.adapter", "t.tool", "t.plain", "t.gear", "t.pulley", "t.belt" };
            public string Id => "t";
            public I18nText Name => new("Docking test");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[]
            {
                new FlangeHost(), new MountPart("t.adapter", true, true), new MountPart("t.tool", true, false),
                new MountPart("t.plain", false, false), new Wheel(), new Pulley(), new Belt(),
            };
            public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("t", new I18nText("Test"), "t") };
            public IEnumerable<LibraryItem> Library => Ids.Select(id => new LibraryItem(id, id, new I18nText(id), "t"));
        }

        private static Registry Reg() => new Registry().Register(new TestModule());

        private static SketchObject Make(Registry r, string type, string id, double x, double y) => r.Type(type)!.Create(new Vec2(x, y)) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Docking");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static Anchor Mount(Registry r, SketchDocument doc, string id, string mount)
            => r.PageAnchors(doc.Get(id)!, new RenderContext(doc).For(r)).Single(a => a.Role == AnchorRole.Mount && a.Id == mount);

        private static (SketchDocument Doc, SketchObject Placed) Plus(Registry r, SketchDocument doc, string id, string portId, string typeId)
        {
            var port = Building.Ports(r, doc, id).Single(p => p.Id == portId);
            var items = Building.Items(r, doc, id, port, 20);
            Assert.True(items.Count(i => i.TypeId == typeId) == 1, $"{typeId} at {id}.{portId}: " + string.Join(", ", items.Select(i => i.TypeId)));
            var item = items.Single(i => i.TypeId == typeId);
            var placed = Building.Place(r, doc, id, port, item);
            Assert.NotNull(placed);
            return (Edits.Add(doc, doc.Pages[0].Id, placed!), placed!);
        }

        private static void AssertDocked(Registry r, SketchDocument doc, string host, string hostMount, string part, double tol = 1e-6)
        {
            var h = Mount(r, doc, host, hostMount);
            var m = Mount(r, doc, part, "flange");
            Assert.True(h.P.DistanceTo(m.P) < tol, $"{part} sits on {host}.{hostMount}: {m.P} vs {h.P}");
            Assert.True((h.Direction!.Value + m.Direction!.Value).Length < tol, $"{part} faces {host}.{hostMount}");
        }

        // ------------------------------------------------------------------ compatible mount ids

        [Fact]
        public void AMountDocksOnItsOwnIdAndTheIdsItAccepts()
        {
            var plain = Anchor.Directed(AnchorRole.Mount, Vec2.Zero, new Vec2(0, -1), "flange");
            var tool = plain with { Accepts = AcceptsTool };
            Assert.True(plain.DocksOn("flange"));
            Assert.False(plain.DocksOn("tool"));
            Assert.True(tool.DocksOn("flange"));
            Assert.True(tool.DocksOn("tool"));
            Assert.False(tool.DocksOn("deck"));
            Assert.False(tool.DocksOn(null));
            // Accepts survives the page transform.
            Assert.Same(AcceptsTool, tool.Transform(Matrix2D.Translation(5, 5)).Accepts);
        }

        [Fact]
        public void PlusOnAToolSideOffersOnlyPartsThatAcceptIt()
        {
            var r = Reg();
            var doc = Doc(Make(r, "t.host", "h", 100, 50), Make(r, "t.adapter", "a", 40, 50));
            var flangeItems = Building.Items(r, doc, "h", Building.Ports(r, doc, "h").Single(p => p.Id == "mount.flange"), 20).Select(i => i.TypeId).ToList();
            Assert.Contains("t.adapter", flangeItems);
            Assert.Contains("t.tool", flangeItems);
            Assert.Contains("t.plain", flangeItems);
            var toolItems = Building.Items(r, doc, "a", Building.Ports(r, doc, "a").Single(p => p.Id == "mount.tool"), 20).Select(i => i.TypeId).ToList();
            Assert.Contains("t.adapter", toolItems);
            Assert.Contains("t.tool", toolItems);
            Assert.DoesNotContain("t.plain", toolItems);
            // Mount ids stay unique per object: the accepted id is no extra anchor.
            var ids = r.PageAnchors(doc.Get("a")!, new RenderContext(doc).For(r)).Where(a => a.Role == AnchorRole.Mount).Select(a => a.Id).ToList();
            Assert.Equal(new[] { "flange", "tool" }, ids);
        }

        [Fact]
        public void AChainIsBuiltWithPlusAndFollowsItsHost()
        {
            var r = Reg();
            var doc = Doc(Make(r, "t.host", "h", 100, 50));
            var (d1, adapter) = Plus(r, doc, "h", "mount.flange", "t.adapter");
            var (d2, second) = Plus(r, d1, adapter.Id, "mount.tool", "t.adapter");
            var (d3, tool) = Plus(r, d2, second.Id, "mount.tool", "t.tool");
            AssertDocked(r, d3, "h", "flange", adapter.Id);
            // The second adapter docks with its input face (the flange that accepts "tool"), not with its own tool side.
            AssertDocked(r, d3, adapter.Id, "tool", second.Id);
            Assert.Equal(0, second.Transform.Rotation, 9);
            AssertDocked(r, d3, second.Id, "tool", tool.Id);
            // Taken mounts offer no "+" any more.
            foreach (var id in new[] { "h", adapter.Id, second.Id, tool.Id })
                Assert.DoesNotContain(Building.Ports(r, d3, id), p => p.Kind == PortKind.Mount);

            // The host turns and moves: the chain follows face to face.
            var after = Edits.Update(d3, new[] { "h" }, o => o with { Transform = o.Transform with { X = 120, Rotation = 30 } });
            var moved = Coupling.Propagate(r, d3, after);
            AssertDocked(r, moved, "h", "flange", adapter.Id, 1e-3);
            AssertDocked(r, moved, adapter.Id, "tool", second.Id, 1e-3);
            AssertDocked(r, moved, second.Id, "tool", tool.Id, 1e-3);
            Assert.Equal(30, moved.Get(tool.Id)!.Transform.Rotation, 6);
        }

        [Fact]
        public void SnappingPrefersAFaceThatFitsOverAnyOtherMount()
        {
            // A tool's flange (facing up) between a fitting tool side (facing down) and a plain mount point slightly closer.
            var flange = Anchor.Directed(AnchorRole.Mount, Vec2.Zero, new Vec2(0, -1), "flange") with { Accepts = AcceptsTool };
            var toolSide = Anchor.Directed(AnchorRole.Mount, new Vec2(0.2, 0), new Vec2(0, 1), "tool") with { Owner = "a" };
            var deck = Anchor.Point(AnchorRole.Mount, new Vec2(-0.18, 0), "deck") with { Owner = "b" };
            var opts = new SnapOptions { Grid = null, Alignment = false, Page = false, Tolerance = 3 };
            var snap = Snapper.SnapMove(new[] { flange }, Vec2.Zero, new[] { deck, toolSide }, opts);
            Assert.Equal("mount", snap.Kind);
            Assert.Equal("tool", snap.Target?.Id);
            // Without a fitting face the nearer mount wins as before.
            var plain = Anchor.Directed(AnchorRole.Mount, Vec2.Zero, new Vec2(0, -1), "flange");
            Assert.Equal("deck", Snapper.SnapMove(new[] { plain }, Vec2.Zero, new[] { deck, toolSide }, opts).Target?.Id);
            // A face pointing the same way does not fit (it would dock back to back).
            var sameWay = toolSide with { Q = toolSide.P + new Vec2(0, -1) };
            Assert.Equal("deck", Snapper.SnapMove(new[] { flange }, Vec2.Zero, new[] { deck, sameWay }, opts).Target?.Id);
        }

        // ------------------------------------------------------------------ pitch circles that do not mesh

        [Fact]
        public void PageAnchorsMarkPulleyPitchCirclesAsNoMesh()
        {
            var r = Reg();
            var doc = Doc(Make(r, "t.gear", "g", 50, 50), Make(r, "t.pulley", "p", 100, 50));
            var ctx = new RenderContext(doc).For(r);
            Assert.False(r.PageAnchors(doc.Get("g")!, ctx).Single(a => a.Role == AnchorRole.Pitch).NoMesh);
            Assert.True(r.PageAnchors(doc.Get("p")!, ctx).Single(a => a.Role == AnchorRole.Pitch).NoMesh);
            Assert.False(r.PageAnchors(doc.Get("p")!, ctx).Single(a => a.Role == AnchorRole.Hub).NoMesh);
        }

        [Theory]
        [InlineData("t.gear", "mesh")]
        [InlineData("t.pulley", "concentric")]
        public void APulleySnapsOntoABeltHubInsteadOfMeshing(string type, string kind)
        {
            var r = Reg();
            // Wheel A at x = 50; the belt's right hub at (70.9, 50.4): a wheel dragged to x = 70.6 nearly touches A's pitch
            // circle (mesh at x = 70) and nearly sits on the hub.
            var a = Make(r, type, "a", 50, 50);
            var belt = Make(r, "t.belt", "belt", 40.9, 50.4);
            var b = Make(r, type, "b", 200, 50);
            var doc = Doc(a, belt, b);
            var ctx = new RenderContext(doc).For(r);
            var moving = r.PageAnchors(b, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { "b" }, ctx, new SnapOptions { Page = false });
            var snap = Snapper.SnapMove(moving, new Vec2(70.6 - 200, 0), targets, new SnapOptions { Grid = null, Alignment = false, Page = false, Tolerance = 2 });
            Assert.Equal(kind, snap.Kind);
            var centre = new Vec2(200, 50) + snap.Delta;
            if (kind == "mesh") Assert.Equal(20, centre.DistanceTo(new Vec2(50, 50)), 6);
            else
            {
                Assert.Equal("belt", snap.Target?.Owner);
                Assert.True(centre.DistanceTo(new Vec2(70.9, 50.4)) < 1e-9, $"pulley on the belt hub: {centre}");
            }
        }

        [Fact]
        public void APulleyOffersItsHubButNoMeshPort()
        {
            var r = Reg();
            var doc = Doc(Make(r, "t.gear", "g", 50, 50), Make(r, "t.pulley", "p", 150, 50));
            Assert.Equal(4, Building.Ports(r, doc, "g").Count(p => p.Kind == PortKind.Mesh));
            var ports = Building.Ports(r, doc, "p");
            Assert.DoesNotContain(ports, p => p.Kind is PortKind.Mesh or PortKind.InnerMesh);
            Assert.Contains(ports, p => p.Kind == PortKind.Hub);
            // A gear's mesh port takes a gear, never a pulley.
            var mesh = Building.Ports(r, doc, "g").First(p => p.Kind == PortKind.Mesh);
            var items = Building.Items(r, doc, "g", mesh, 20).Select(i => i.TypeId).ToList();
            Assert.Contains("t.gear", items);
            Assert.DoesNotContain("t.pulley", items);
            Assert.Null(Building.Place(r, doc, "g", mesh, r.Library.Single(i => i.TypeId == "t.pulley")));
        }
    }
}
