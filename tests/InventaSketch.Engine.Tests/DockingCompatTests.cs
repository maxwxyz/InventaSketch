using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Docking across the whole catalogue: compatible mount ids (<see cref="Anchor.Accepts"/>) keep mount ids unique per
    /// object, and pulleys, sprockets and idlers (<see cref="IPitchMesh"/>) neither mesh when snapped nor offer or take a mesh
    /// port with "+": they snap onto belt hubs.
    /// </summary>
    public class DockingCompatTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        /// <summary>The library item of a type in a view, by "type.view" (mech.pulley.axial, mech.gear.spur.axial).</summary>
        private static LibraryItem ItemOf(string key)
        {
            var dot = key.LastIndexOf('.');
            var (type, view) = (key.Substring(0, dot), key.Substring(dot + 1));
            return Reg.Library.FirstOrDefault(i => i.TypeId == type && i.View == view && i.Params is null) ?? throw new InvalidOperationException(key);
        }

        private static SketchObject Item(string key, Vec2 at) => Reg.Create(ItemOf(key), at);

        private static Anchor Pitch(SketchDocument doc, SketchObject o)
            => Reg.PageAnchors(o, new RenderContext(doc).For(Reg)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);

        [Fact]
        public void DirectedMountIdsAreUniquePerElement()
        {
            foreach (var item in Reg.Library)
            {
                var o = Reg.Create(item, new Vec2(100, 100));
                if (o.IsPointGeometry) continue;
                var doc = Doc(o);
                var ids = Reg.PageAnchors(o, new RenderContext(doc).For(Reg))
                    .Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Mount && a.Direction is not null && a.Id is not null)
                    .Select(a => a.Id!).ToList();
                Assert.True(ids.Distinct(StringComparer.Ordinal).Count() == ids.Count, $"{item.Id}: mount ids {string.Join(", ", ids)}");
            }
        }

        [Fact]
        public void EveryToolSideOffersTheToolsThatAcceptIt()
        {
            var changer = Reg.Create(Reg.Library.First(i => i.TypeId == "robot.tool.changer" && (i.View is null || i.View == "side")), new Vec2(100, 100));
            var doc = Doc(changer);
            var port = Building.Ports(Reg, doc, changer.Id).Single(p => p.Kind == PortKind.Mount && p.Id == "mount.tool");
            var items = Building.Items(Reg, doc, changer.Id, port, 40).Select(i => i.TypeId).ToList();
            Assert.Contains("robot.gripper.parallel", items);
            Assert.Contains("robot.sensor.ft", items);
            // Every offered part docks with a mount that accepts the tool side, facing it, and keeps its own unique ids.
            foreach (var typeId in items)
            {
                var placed = Building.Place(Reg, doc, changer.Id, port, Reg.Library.First(i => i.TypeId == typeId && (i.View is null || i.View == "side")))!;
                var withPart = Edits.Add(doc, doc.Pages[0].Id, placed);
                var mount = Reg.PageAnchors(placed, new RenderContext(withPart).For(Reg))
                    .Single(a => a.Role == AnchorRole.Mount && a.Direction is not null && a.P.DistanceTo(port.At) < 1e-6);
                Assert.True(mount.DocksOn("tool"), typeId);
                Assert.True((mount.Direction!.Value + port.Dir).Length < 1e-6, typeId + " faces the tool side");
            }
        }

        [Theory]
        [InlineData("mech.pulley.axial")]
        [InlineData("mech.sprocket.teeth")]
        [InlineData("mech.pulley.idler.axial")]
        public void StrandWheelsMarkTheirPitchCircleAndOfferNoMeshPort(string itemId)
        {
            var wheel = Item(itemId, new Vec2(100, 100));
            var doc = Doc(wheel);
            Assert.True(Pitch(doc, wheel).NoMesh, itemId);
            Assert.DoesNotContain(Building.Ports(Reg, doc, wheel.Id), p => p.Kind is PortKind.Mesh or PortKind.InnerMesh);
        }

        [Fact]
        public void AGearsMeshPortTakesGearsButNoPulleyOrSprocket()
        {
            var gear = Item("mech.gear.spur.axial", new Vec2(100, 100));
            var doc = Doc(gear);
            Assert.False(Pitch(doc, gear).NoMesh);
            var port = Building.Ports(Reg, doc, gear.Id).First(p => p.Kind == PortKind.Mesh);
            var items = Building.Items(Reg, doc, gear.Id, port, 20).Select(i => i.TypeId).ToList();
            Assert.Contains("mech.gear.spur", items);
            foreach (var strand in new[] { "mech.pulley.axial", "mech.sprocket.teeth", "mech.pulley.idler.axial" })
                Assert.Null(Building.Place(Reg, doc, gear.Id, port, ItemOf(strand)));
        }

        [Theory]
        [InlineData("mech.gear.spur.axial", "mesh")]
        [InlineData("mech.pulley.axial", "concentric")]
        [InlineData("mech.sprocket.teeth", "concentric")]
        public void APulleyDraggedNextToAnotherSnapsOntoTheBeltHub(string itemId, string kind)
        {
            // A belt between hubs at (50, 50) and (130, 50); a wheel C right of the second hub, so a wheel dragged near that
            // hub nearly touches C's pitch circle too.
            var belt = Reg.Create(Reg.Library.First(i => i.TypeId == "mech.belt" && i.View == "open"), new Vec2(90, 50));
            var probe = Doc(belt);
            var hubs = Reg.PageAnchors(belt, new RenderContext(probe).For(Reg)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).ToList();
            var span = hubs[0].P.DistanceTo(hubs[1].P);
            belt = belt with { Size = new Size2(belt.Size.W + (80 - span), belt.Size.H) };
            probe = Doc(belt);
            var h0 = Reg.PageAnchors(belt, new RenderContext(probe).For(Reg)).First(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).P;
            belt = belt with { Transform = belt.Transform with { X = belt.Transform.X + 50 - h0.X, Y = belt.Transform.Y + 50 - h0.Y } };
            var hub = new Vec2(130, 50);

            var c = Item(itemId, new Vec2(0, 0));
            var b = Item(itemId, new Vec2(300, 200));
            var r = Pitch(Doc(c), c).Radius;
            var rb = Pitch(Doc(b), b).Radius;
            c = c with { Transform = c.Transform with { X = hub.X + rb + r + 0.3, Y = hub.Y } };
            var doc = Doc(belt, c, b);
            var ctx = new RenderContext(doc).For(Reg);
            Assert.Contains(Reg.PageAnchors(belt, ctx), a => a.Role == AnchorRole.Hub && a.P.DistanceTo(hub) < 1e-6);
            var centre = Pitch(doc, b).P;
            var moving = Reg.PageAnchors(b, ctx).ToList();
            var targets = Snapper.Targets(Reg, doc, doc.Pages[0], new[] { b.Id }, ctx, new SnapOptions { Page = false });
            var raw = hub + new Vec2(0.4, 0.3) - centre;
            var snap = Snapper.SnapMove(moving, raw, targets, new SnapOptions { Grid = null, Alignment = false, Page = false, Tolerance = 2 });
            Assert.Equal(kind, snap.Kind);
            var at = centre + snap.Delta;
            if (kind == "mesh") Assert.Equal(rb + r, at.DistanceTo(Pitch(doc, c).P), 6);
            else Assert.True(at.DistanceTo(hub) < 1e-6, $"{itemId} on the belt hub: {at}");
        }
    }
}
