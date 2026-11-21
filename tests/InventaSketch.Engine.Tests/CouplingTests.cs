using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Mechanical coupling: gears stay in mesh, shafts carry their gears, the rest of the train moves along.</summary>
    public class CouplingTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static (Vec2 C, double R) Pitch(SketchDocument doc, string id)
        {
            var a = Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(x => x.Kind == AnchorKind.Circle && x.Role == AnchorRole.Pitch);
            return (a.P, a.Radius);
        }

        private static SketchObject Gear(Vec2 at, double size) => Reg.Create(Reg.Item("mech.spur.axial")!, at) with { Size = new Size2(size, size) };

        /// <summary>Gear B placed in mesh to the right of A.</summary>
        private static SketchDocument Pair(out string a, out string b, double sizeA = 30, double sizeB = 20)
        {
            var ga = Gear(new Vec2(60, 60), sizeA);
            var doc = Doc(ga);
            var ra = Pitch(doc, ga.Id).R;
            var gb = Gear(new Vec2(0, 0), sizeB);
            var tmp = Edits.Add(doc, doc.Pages[0].Id, gb);
            var rb = Pitch(tmp, gb.Id).R;
            doc = Edits.Add(doc, doc.Pages[0].Id, gb with { Transform = gb.Transform with { X = 60 + ra + rb, Y = 60 } });
            a = ga.Id;
            b = gb.Id;
            return doc;
        }

        private static void AssertMeshed(SketchDocument doc, string a, string b)
        {
            var pa = Pitch(doc, a);
            var pb = Pitch(doc, b);
            Assert.Equal(pa.R + pb.R, pa.C.DistanceTo(pb.C), 3);
        }

        [Fact]
        public void PullingAGearAwayScalesBothAndKeepsTheMesh()
        {
            var before = Pair(out var a, out var b);
            var ratio = before.Get(a)!.Size.W / before.Get(b)!.Size.W;
            var after = Coupling.Propagate(Reg, before, Edits.Move(before, new[] { a }, new Vec2(-10, 0)));
            AssertMeshed(after, a, b);
            Assert.Equal(before.Get(b)!.Transform, after.Get(b)!.Transform);
            Assert.True(after.Get(b)!.Size.W > before.Get(b)!.Size.W, "partner grew");
            Assert.Equal(ratio, after.Get(a)!.Size.W / after.Get(b)!.Size.W, 6);
        }

        [Fact]
        public void MovingAroundThePartnerChangesNothing()
        {
            var before = Pair(out var a, out var b);
            var pa = Pitch(before, a);
            var pb = Pitch(before, b);
            // Orbit A by 90° about B: same centre distance.
            var d = pa.C - pb.C;
            var target = pb.C + new Vec2(-d.Y, d.X);
            var after = Coupling.Propagate(Reg, before, Edits.Move(before, new[] { a }, target - pa.C));
            Assert.Equal(before.Get(a)!.Size, after.Get(a)!.Size);
            Assert.Equal(before.Get(b)!.Size, after.Get(b)!.Size);
        }

        [Fact]
        public void AGrowingGearPushesItsTrainAlong()
        {
            var doc = Pair(out var a, out var b);
            // Third gear C meshing with B on the far side.
            var pb = Pitch(doc, b);
            var gc = Gear(Vec2.Zero, 16);
            var rc = Pitch(Edits.Add(doc, doc.Pages[0].Id, gc), gc.Id).R;
            doc = Edits.Add(doc, doc.Pages[0].Id, gc with { Transform = gc.Transform with { X = pb.C.X + pb.R + rc, Y = pb.C.Y } });
            var grown = Edits.Update(doc, new[] { a }, o => o with { Size = new Size2(o.Size.W * 1.2, o.Size.H * 1.2) });
            var after = Coupling.Propagate(Reg, doc, grown);
            AssertMeshed(after, a, b);
            AssertMeshed(after, b, gc.Id);
            Assert.Equal(doc.Get(b)!.Size, after.Get(b)!.Size);
            var shift = after.Get(b)!.Transform.X - doc.Get(b)!.Transform.X;
            Assert.True(shift > 0.5);
            Assert.Equal(shift, after.Get(gc.Id)!.Transform.X - doc.Get(gc.Id)!.Transform.X, 6);
        }

        [Fact]
        public void AShaftCarriesTheGearsOnItsAxis()
        {
            var shaft = Reg.Create(Reg.Library.First(i => i.TypeId == "mech.shaft"), new Vec2(100, 80));
            var doc = Doc(shaft);
            var axis = Reg.PageAnchors(shaft, new RenderContext(doc)).First(x => x.Kind == AnchorKind.Segment && x.Role == AnchorRole.Axis);
            var onAxis = Vec2.Lerp(axis.P, axis.Q, 0.3);
            var side = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "side");
            var gear = Reg.Create(side, onAxis);
            doc = Edits.Add(doc, doc.Pages[0].Id, gear);
            var moved = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { shaft.Id }, new Vec2(5, 12)));
            Assert.Equal(gear.Transform.X + 5, moved.Get(gear.Id)!.Transform.X, 6);
            Assert.Equal(gear.Transform.Y + 12, moved.Get(gear.Id)!.Transform.Y, 6);
            // Rotating the shaft turns the gear with it (position and orientation).
            var turned = Coupling.Propagate(Reg, doc, Edits.Rotate(doc, new[] { shaft.Id }, new Vec2(100, 80), 90));
            var g = turned.Get(gear.Id)!;
            Assert.Equal(gear.Transform.Rotation + 90, g.Transform.Rotation, 6);
            var expected = new Vec2(onAxis.X, onAxis.Y).RotateAround(new Vec2(100, 80), System.Math.PI / 2);
            Assert.Equal(expected.X, g.Transform.X, 3);
            Assert.Equal(expected.Y, g.Transform.Y, 3);
            // Moving the gear alone leaves the shaft where it is.
            var slid = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { gear.Id }, new Vec2(8, 0)));
            Assert.Equal(shaft.Transform, slid.Get(shaft.Id)!.Transform);
        }

        [Fact]
        public void GearsLyingOnTopOfEachOtherAreNotCoupled()
        {
            var ga = Gear(new Vec2(60, 60), 30);
            var gb = Gear(new Vec2(60, 60), 30);
            var doc = Doc(ga, gb);
            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { gb.Id }, new Vec2(25, 0)));
            Assert.Equal(ga.Transform, after.Get(ga.Id)!.Transform);
        }

        [Fact]
        public void EngineCouplesMovesUnlessSwitchedOff()
        {
            var api = new EngineApi(Reg);
            var s = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var a = EngineApiTests.Insert(api, s, "mech.spur.axial", 60, 60);
            var st = EngineApiTests.Call(api, "state", new { session = s });
            var w = EngineApiTests.Obj(st, a).GetProperty("w").GetDouble();
            // Partner in mesh to the right (pitch radii from the anchors).
            var doc = Doc(Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(60, 60)));
            var r = Pitch(doc, doc.Objects.Keys.First()).R;
            var b = EngineApiTests.Insert(api, s, "mech.spur.axial", 60 + 2 * r, 60);
            var free = EngineApiTests.Call(api, "move", new { session = s, ids = new[] { a }, dx = -5.0, dy = 0.0, couple = false });
            Assert.Equal(w, EngineApiTests.Obj(free, b).GetProperty("w").GetDouble(), 6);
            EngineApiTests.Call(api, "undo", new { session = s });
            var coupled = EngineApiTests.Call(api, "move", new { session = s, ids = new[] { a }, dx = -5.0, dy = 0.0 });
            Assert.True(EngineApiTests.Obj(coupled, b).GetProperty("w").GetDouble() > w + 0.5);
        }
    }
}
