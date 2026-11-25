using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Parts fit onto their shaft (<see cref="ShaftFit"/>): inserted on a selected shaft (<see cref="Fitting.Place"/>), placed
    /// near a shaft (<see cref="Fitting.PlaceNear"/>), built at an axis end (<see cref="Building"/>) or dragged and snapped
    /// onto it (<see cref="Coupling.Propagate"/>), their bore gets the shaft diameter, in every view.
    /// </summary>
    public class ShaftFitTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchDocument Add(SketchDocument doc, SketchObject o) => Edits.Add(doc, doc.Pages[0].Id, o);

        private static List<Anchor> Anchors(SketchDocument doc, string id) => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).ToList();

        private static Anchor Bore(SketchDocument doc, string id) => Anchors(doc, id).Single(ShaftFit.HasBore);

        private static Anchor Axis(SketchDocument doc, string id) => Anchors(doc, id).First(ShaftFit.HasRadius);

        /// <summary>A library item of the type in the view, sized for that view.</summary>
        private static LibraryItem Item(string typeId, string view)
        {
            var preset = Reg.Library.FirstOrDefault(i => i.TypeId == typeId && i.View == view);
            if (preset is not null) return preset;
            var first = Reg.Library.First(i => i.TypeId == typeId);
            var o = Reg.WithView(Reg.Create(first, Vec2.Zero), view, new RenderContext(SketchDocument.Create()));
            return first with { Id = first.Id + ".test." + view, View = view, Size = o.Size };
        }

        private static SketchObject Shaft(string view, double diameter, Vec2 at, double rotation = 0, double length = 120)
        {
            var o = Reg.Create(Item("mech.shaft", view), at);
            return o with { Size = new Size2(length, diameter), Transform = new Transform(at.X, at.Y, rotation) };
        }

        public static IEnumerable<object[]> PartViews()
        {
            foreach (var type in new[] { "mech.bearing.ball", "mech.gear.spur", "mech.pulley" })
                foreach (var view in Reg.Type(type)!.Views.Select(v => v.Id))
                    foreach (var shaftView in new[] { "line", "body" })
                        foreach (var d in new[] { 8.0, 16.0 })
                            yield return new object[] { type, view, shaftView, d };
        }

        [Theory]
        [MemberData(nameof(PartViews))]
        public void APartInsertedOnAShaftGetsTheShaftDiameterInEveryView(string type, string view, string shaftView, double diameter)
        {
            var shaft = Shaft(shaftView, diameter, new Vec2(100, 60), rotation: 30);
            var doc = Doc(shaft);
            var item = Item(type, view);
            var part = Fitting.Place(Reg, doc, shaft.Id, item);
            Assert.NotNull(part);
            doc = Add(doc, part!);
            var bore = Bore(doc, part!.Id);
            var axis = Axis(doc, shaft.Id);
            Assert.Equal(diameter / 2, bore.Radius, 6);
            Assert.True(Geo.ClosestOnSegment(bore.P, axis.P, axis.Q).Point.DistanceTo(bore.P) < 1e-6, "hub on the axis");
            // Uniform scaling: the proportions of the part stay (Size × k), parameters are untouched.
            var preset = item.Size ?? Reg.Type(type)!.DefaultSize;
            Assert.Equal(preset.W / preset.H, part.Size.W / part.Size.H, 6);
            Assert.Equal(view, part.View);
            Assert.Equal(30, part.Transform.Rotation, 6);
        }

        [Fact]
        public void AFitStaysWhenTheViewSwitchesBetweenSchematicAndSide()
        {
            // The schematic takes the bore of the side/section view after a view switch: the fit carries over (the host's view
            // switch, Registry.WithView, keeps the visual scale and the pitch circle).
            var shaft = Shaft("line", 10, new Vec2(100, 60));
            var doc = Doc(shaft);
            foreach (var (type, from, to) in new[]
            {
                ("mech.gear.spur", "stick", "side"), ("mech.gear.spur", "side", "axial"), ("mech.gear.spur", "stick", "teeth"),
                ("mech.bearing.ball", "stick", "detailed"), ("mech.bearing.ball", "simplified", "stick"),
                ("mech.pulley", "stick", "section"), ("mech.pulley", "section", "axial"), ("mech.sprocket", "stick", "teeth"),
                ("mech.coupling.rigid", "stick", "side"), ("mech.clutch.disc", "stick", "side"), ("mech.brake", "stick", "side"),
                ("mech.flywheel", "stick", "axial"), ("mech.flywheel", "stick", "section"), ("mech.gear.bevel", "stick", "section"),
                ("mech.nut", "side", "top"), ("mech.sync", "stick", "side"),
                ("mech.coupling.flexible", "stick", "side"), ("mech.clutch.freewheel", "stick", "section"),
            })
            {
                var part = Fitting.Place(Reg, doc, shaft.Id, Item(type, from))!;
                var d = Add(doc, part);
                Assert.Equal(5, Bore(d, part.Id).Radius, 6);
                var switched = Reg.WithView(part, to, new RenderContext(d));
                d = Edits.Replace(d, switched);
                Assert.True(Math.Abs(Bore(d, part.Id).Radius - 5) < 1e-6, type + " " + from + " → " + to + ": " + Bore(d, part.Id).Radius);
            }
        }

        [Fact]
        public void AStepShaftGivesTheDiameterAtTheHub()
        {
            var shaft = Shaft("stepped", 20, new Vec2(100, 60));
            var doc = Doc(shaft);
            var item = Item("mech.bearing.ball", "simplified");
            // Middle section (full diameter) and the thin end section (0.65 of it).
            var middle = Fitting.PlaceNear(Reg, doc, item, new Vec2(100, 62), 3)!;
            var end = Fitting.PlaceNear(Reg, doc, item, new Vec2(55, 62), 3)!;
            Assert.Equal(10, Bore(Add(doc, middle), middle.Id).Radius, 6);
            Assert.Equal(6.5, Bore(Add(doc, end), end.Id).Radius, 6);
        }

        [Fact]
        public void ShaftThroughAPartIsAsThickAsItsBore()
        {
            var gear = Reg.Create(Item("mech.gear.spur", "side"), new Vec2(50, 50));
            var doc = Doc(gear);
            var shaft = Fitting.Place(Reg, doc, gear.Id, Item("mech.shaft", "body"))!;
            doc = Add(doc, shaft);
            Assert.Equal(Bore(doc, gear.Id).Radius, Axis(doc, shaft.Id).Radius, 6);
            Assert.Equal(120, shaft.Size.W, 6); // the length stays
            Assert.Contains(gear.Id, Coupling.RigidFollowers(Reg, doc, new[] { shaft.Id }));
        }

        [Fact]
        public void ANutFitsTheBoltThread()
        {
            var bolt = Reg.Create(Item("mech.screw", "side"), new Vec2(60, 40)) with { Size = new Size2(84, 24) };
            var doc = Doc(bolt);
            var nut = Fitting.Place(Reg, doc, bolt.Id, Item("mech.nut", "side"))!;
            doc = Add(doc, nut);
            Assert.Equal(8, Bore(doc, nut.Id).Radius, 6); // thread radius 4 of 12 at twice the size
        }

        [Fact]
        public void ABearingBuiltAtAShaftEndFitsTheShaft()
        {
            var shaft = Shaft("body", 14, new Vec2(100, 60));
            var doc = Doc(shaft);
            var port = Building.Ports(Reg, doc, shaft.Id).First(p => p.Kind == PortKind.AxisEnd);
            var bearing = Building.Place(Reg, doc, shaft.Id, port, Item("mech.bearing.ball", "simplified"))!;
            doc = Add(doc, bearing);
            Assert.Equal(7, Bore(doc, bearing.Id).Radius, 6);
            // Still at the shaft end, on the axis.
            var axis = Axis(doc, shaft.Id);
            var hub = Bore(doc, bearing.Id).P;
            Assert.True(Geo.ClosestOnSegment(hub, axis.P, axis.Q).Point.DistanceTo(hub) < 1e-6);
            Assert.True(hub.DistanceTo(port.At) <= bearing.Size.W / 2 + 1e-6);
        }

        // ------------------------------------------------------------------ dragging and snapping

        private static SketchDocument Drag(SketchDocument doc, string id, Vec2 raw, out SnapResult snap)
        {
            var ctx = new RenderContext(doc);
            var o = doc.Get(id)!;
            var moving = Reg.PageAnchors(o, ctx).ToList();
            var targets = Snapper.Targets(Reg, doc, doc.Pages[0], new[] { id }, ctx, new SnapOptions { Tolerance = 3 });
            snap = Snapper.SnapMove(moving, raw, targets, new SnapOptions { Tolerance = 3 });
            var d = snap.Delta;
            var after = Edits.Update(doc, new[] { id }, x => x with { Transform = x.Transform with { X = x.Transform.X + d.X, Y = x.Transform.Y + d.Y } });
            return Coupling.Propagate(Reg, doc, after);
        }

        [Theory]
        [InlineData("mech.bearing.ball", "detailed")]
        [InlineData("mech.gear.spur", "section")]
        [InlineData("mech.gear.spur", "axial")]
        [InlineData("mech.pulley", "stick")]
        [InlineData("mech.coupling.flexible", "side")]
        [InlineData("machines.drive.wheel", "top")]
        public void APartDraggedOntoAShaftIsScaledToIt(string type, string view)
        {
            var shaft = Shaft("body", 12, new Vec2(100, 50));
            var part = Reg.Create(Item(type, view), new Vec2(70, 120));
            var doc = Doc(shaft, part);
            var hub0 = Anchors(doc, part.Id).First(a => a.Role == AnchorRole.Hub && a.Kind == AnchorKind.Point).P;
            doc = Drag(doc, part.Id, new Vec2(80, 51.5) - hub0, out var snap);
            Assert.Equal("axis", snap.Kind);
            var bore = Bore(doc, part.Id);
            Assert.Equal(6, bore.Radius, 6);
            Assert.Equal(50, bore.P.Y, 6);
            Assert.Equal(80, bore.P.X, 6);
            var fitted = doc.Get(part.Id)!;
            Assert.Equal(part.Size.W / part.Size.H, fitted.Size.W / fitted.Size.H, 6);

            // Sliding it along the shaft keeps its size; so does a size the user gives it on the shaft.
            var slid = Drag(doc, part.Id, new Vec2(15, 0), out _);
            Assert.Equal(fitted.Size, slid.Get(part.Id)!.Size);
            var resized = Edits.Update(slid, new[] { part.Id }, x => x with { Size = new Size2(x.Size.W * 1.5, x.Size.H * 1.5) });
            Assert.Equal(resized.Get(part.Id)!.Size, Coupling.Propagate(Reg, slid, resized).Get(part.Id)!.Size);
        }

        [Fact]
        public void AShaftDraggedThroughAPartFitsThePart()
        {
            var shaft = Shaft("line", 10, new Vec2(100, 20));
            var bearing = Reg.Create(Item("mech.bearing.ball", "stick"), new Vec2(100, 60));
            var doc = Doc(shaft, bearing);
            doc = Drag(doc, shaft.Id, new Vec2(0, 39), out var snap);
            Assert.Equal("axis", snap.Kind);
            Assert.Equal(5, Bore(doc, bearing.Id).Radius, 6);
            Assert.Equal(new Vec2(100, 60), Bore(doc, bearing.Id).P);
        }

        [Fact]
        public void APartInsertedOnTheAxisIsFitted()
        {
            var shaft = Shaft("body", 10, new Vec2(100, 50));
            var doc = Doc(shaft);
            var gear = Reg.Create(Item("mech.gear.spur", "side"), new Vec2(90, 50));
            var after = Coupling.Propagate(Reg, doc, Add(doc, gear));
            Assert.Equal(5, Bore(after, gear.Id).Radius, 6);
        }

        [Fact]
        public void PartsWithoutABoreOrShaftsWithoutARadiusAreLeftAlone()
        {
            // A support symbol touches the shaft line: no bore, it keeps its size.
            var shaft = Shaft("line", 10, new Vec2(100, 50));
            var support = Reg.Create(Reg.Library.First(i => i.TypeId == "mech.support.fixed"), new Vec2(90, 50));
            var doc = Doc(shaft);
            var after = Coupling.Propagate(Reg, doc, Add(doc, support));
            Assert.Equal(support.Size, after.Get(support.Id)!.Size);
        }

        // ------------------------------------------------------------------ live placement near a shaft

        [Fact]
        public void PlaceNearPutsTheHubOnTheProjectionInTheShaftsView()
        {
            var line = Shaft("line", 10, new Vec2(100, 50));
            var doc = Doc(line);
            var gear = Fitting.PlaceNear(Reg, doc, Item("mech.gear.spur", "axial"), new Vec2(80, 53), 4)!;
            Assert.NotNull(gear);
            Assert.Equal("stick", gear.View);
            var d = Add(doc, gear);
            Assert.Equal(new Vec2(80, 50), Bore(d, gear.Id).P);
            Assert.Equal(5, Bore(d, gear.Id).Radius, 6);

            var body = Shaft("body", 10, new Vec2(100, 50));
            doc = Doc(body);
            var bearing = Fitting.PlaceNear(Reg, doc, Item("mech.bearing.ball", "stick"), new Vec2(130, 46), 4)!;
            Assert.Equal("simplified", bearing.View);
            Assert.Equal(new Vec2(130, 50), Bore(Add(doc, bearing), bearing.Id).P);

            var hollow = Shaft("hollow", 10, new Vec2(100, 50));
            doc = Doc(hollow);
            Assert.Equal("detailed", Fitting.PlaceNear(Reg, doc, Item("mech.bearing.ball", "stick"), new Vec2(130, 46), 4)!.View);
            Assert.Equal("section", Fitting.PlaceNear(Reg, doc, Item("mech.gear.spur", "stick"), new Vec2(130, 46), 4)!.View);
        }

        [Fact]
        public void PlaceNearFollowsARotatedShaftAndClampsToItsEnds()
        {
            var shaft = Shaft("body", 8, new Vec2(100, 100), rotation: 90);
            var doc = Doc(shaft);
            var pulley = Fitting.PlaceNear(Reg, doc, Item("mech.pulley", "side"), new Vec2(103, 120), 2)!;
            Assert.Equal(90, pulley.Transform.Rotation, 6);
            var d = Add(doc, pulley);
            var hub = Bore(d, pulley.Id);
            Assert.Equal(100, hub.P.X, 6);
            Assert.Equal(120, hub.P.Y, 6);
            Assert.Equal(4, hub.Radius, 6);
            // Beyond the end the hub stays on the shaft end.
            var past = Fitting.PlaceNear(Reg, doc, Item("mech.pulley", "side"), new Vec2(100, 162), 3)!;
            Assert.Equal(160, Bore(Add(doc, past), past.Id).P.Y, 6);
        }

        [Fact]
        public void PlaceNearIsNullOutOfReachOrWithoutAHub()
        {
            var doc = Doc(Shaft("line", 10, new Vec2(100, 50)));
            Assert.Null(Fitting.PlaceNear(Reg, doc, Item("mech.gear.spur", "stick"), new Vec2(100, 70), 5));
            Assert.Null(Fitting.PlaceNear(Reg, doc, Reg.Library.First(i => i.TypeId == "mech.spring.compression"), new Vec2(100, 51), 5));
            Assert.Null(Fitting.PlaceNear(Reg, Doc(), Item("mech.gear.spur", "stick"), new Vec2(100, 51), 5));
        }

        [Fact]
        public void PlaceNearPicksTheClosestShaft()
        {
            var a = Shaft("body", 8, new Vec2(100, 50));
            var b = Shaft("body", 16, new Vec2(100, 70));
            var doc = Doc(a, b);
            var part = Fitting.PlaceNear(Reg, doc, Item("mech.bearing.ball", "simplified"), new Vec2(90, 63), 4)!;
            var bore = Bore(Add(doc, part), part.Id);
            Assert.Equal(70, bore.P.Y, 6);
            Assert.Equal(8, bore.Radius, 6);
        }

        // ------------------------------------------------------------------ preference point (where the user clicked)

        private static Anchor Pitch(SketchDocument doc, string id) => Anchors(doc, id).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch);

        [Fact]
        public void AMeshingGearGoesTowardsTheClick()
        {
            var a = Reg.Create(Item("mech.gear.spur", "axial"), new Vec2(100, 100));
            var doc = Doc(a);
            var pa = Pitch(doc, a.Id);

            var right = Fitting.Place(Reg, doc, a.Id, Item("mech.gear.spur", "axial"), Lang.En, new Vec2(220, 100))!;
            var d1 = Add(doc, right);
            var pr = Pitch(d1, right.Id);
            Assert.Equal(pa.P.Y, pr.P.Y, 6);
            Assert.True(pr.P.X > pa.P.X);
            Assert.Equal(pa.Radius + pr.Radius, pa.P.DistanceTo(pr.P), 6);

            // Up-left at 135°: the new pitch centre lies on that ray.
            var click = pa.P + new Vec2(-1, -1) * 40;
            var upLeft = Fitting.Place(Reg, doc, a.Id, Item("mech.gear.spur", "axial"), Lang.En, click)!;
            var d2 = Add(doc, upLeft);
            var pu = Pitch(d2, upLeft.Id);
            var dir = (pu.P - pa.P).Normalized();
            Assert.Equal(-Math.Sqrt(0.5), dir.X, 6);
            Assert.Equal(-Math.Sqrt(0.5), dir.Y, 6);
            Assert.Equal(pa.Radius + pu.Radius, pa.P.DistanceTo(pu.P), 6);
            Assert.Equal(LinkKind.Mesh, Links.For(Reg, d2, new[] { a.Id }).Single().Kind);
        }

        [Fact]
        public void AGearSeenFromTheSideGoesAcrossTheAxisOnTheClickedSide()
        {
            var a = Reg.Create(Item("mech.gear.spur", "side"), new Vec2(100, 100));
            var doc = Doc(a);
            var above = Fitting.Place(Reg, doc, a.Id, Item("mech.gear.spur", "side"), Lang.En, new Vec2(130, 20))!;
            Assert.Equal(100, above.Transform.X, 6);
            Assert.True(above.Transform.Y < 100);
        }

        [Fact]
        public void AnOverlappingClickSideFallsBackToAFreeSide()
        {
            var a = Reg.Create(Item("mech.gear.spur", "axial"), new Vec2(100, 100));
            var block = Reg.Create(Item("mech.gear.spur", "axial"), new Vec2(150, 100)) with { Size = new Size2(40, 40) };
            var doc = Doc(a, block);
            var b = Fitting.Place(Reg, doc, a.Id, Item("mech.gear.spur", "axial"), Lang.En, new Vec2(220, 100))!;
            Assert.True(b.Transform.X < 130, "not on the blocked right side");
        }

        [Fact]
        public void APartOnAShaftGoesToTheClickedPositionOffOtherHubs()
        {
            var shaft = Shaft("body", 10, new Vec2(100, 50));
            var doc = Doc(shaft);
            var gear = Fitting.Place(Reg, doc, shaft.Id, Item("mech.gear.spur", "side"), Lang.En, new Vec2(70, 80))!;
            doc = Add(doc, gear);
            Assert.Equal(new Vec2(70, 50), Bore(doc, gear.Id).P);
            Assert.Equal(5, Bore(doc, gear.Id).Radius, 6);

            // Clicking on the gear again puts the bearing beside it, not into it.
            var bearing = Fitting.Place(Reg, doc, shaft.Id, Item("mech.bearing.ball", "simplified"), Lang.En, new Vec2(72, 80))!;
            doc = Add(doc, bearing);
            var hb = Bore(doc, bearing.Id).P;
            Assert.Equal(50, hb.Y, 6);
            Assert.True(Math.Abs(hb.X - 70) >= (gear.Size.W + bearing.Size.W) / 2 - 1e-6);
            Assert.True(hb.X > 70, "on the clicked side of the gear");

            // Beyond the shaft end: clamped to the end.
            var end = Fitting.Place(Reg, doc, shaft.Id, Item("mech.pulley", "section"), Lang.En, new Vec2(400, 50))!;
            Assert.Equal(160, Bore(Add(doc, end), end.Id).P.X, 6);

            // A part beside a part on the shaft follows the click too.
            var beside = Fitting.Place(Reg, doc, gear.Id, Item("mech.bearing.ball", "simplified"), Lang.En, new Vec2(45, 40))!;
            Assert.Equal(new Vec2(45, 50), Bore(Add(doc, beside), beside.Id).P);
        }

        [Fact]
        public void PlaceNearAvoidsHubsAlreadyOnTheShaft()
        {
            var shaft = Shaft("body", 10, new Vec2(100, 50));
            var doc = Doc(shaft);
            var gear = Fitting.PlaceNear(Reg, doc, Item("mech.gear.spur", "side"), new Vec2(100, 52), 3)!;
            doc = Add(doc, gear);
            var next = Fitting.PlaceNear(Reg, doc, Item("mech.gear.spur", "side"), new Vec2(99, 52), 3)!;
            Assert.True(Math.Abs(Bore(Add(doc, next), next.Id).P.X - 100) >= gear.Size.W - 1e-6);
        }

        // ------------------------------------------------------------------ persistence

        [Fact]
        public void JsonRoundTripIsUnaffected()
        {
            var shaft = Shaft("stepped", 12, new Vec2(100, 50));
            var doc = Doc(shaft);
            var gear = Fitting.Place(Reg, doc, shaft.Id, Item("mech.gear.spur", "section"))!;
            doc = Add(doc, gear);
            var bearing = Fitting.Place(Reg, doc, shaft.Id, Item("mech.bearing.ball", "detailed"))!;
            doc = Add(doc, bearing);
            var json = DocumentJson.Serialize(doc);
            Assert.DoesNotContain("steps", json, StringComparison.OrdinalIgnoreCase);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            foreach (var id in new[] { shaft.Id, gear.Id, bearing.Id })
            {
                Assert.Equal(doc.Get(id)!.Size, back.Get(id)!.Size);
                Assert.Equal(doc.Get(id)!.Transform, back.Get(id)!.Transform);
            }
            Assert.Equal(Bore(doc, bearing.Id).Radius, Bore(back, bearing.Id).Radius, 9);
        }
    }
}
