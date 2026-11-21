using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Drives take their shaft along; belts and chains follow their pulleys and sprockets (<see cref="Coupling"/>).</summary>
    public class DriveTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static Vec2 Hub(SketchDocument doc, string id, int index = 0)
            => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).ElementAt(index).P;

        [Fact]
        public void AMovedMotorTakesItsShaftAndTheGearsOnIt()
        {
            var motor = Reg.Create(Reg.Library.First(i => i.TypeId == "machines.drivetrain.emachine"), new Vec2(60, 50));
            var probe = Doc(motor);
            var h = Hub(probe, motor.Id);
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, h + new Vec2(30, 0));
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, h + new Vec2(45, 0));
            var doc = Doc(motor, shaft, gear);
            Assert.True(Reg.Type(motor.Type)!.DrivesShaft);
            var followers = Coupling.RigidFollowers(Reg, doc, new[] { motor.Id });
            Assert.Contains(shaft.Id, followers);
            Assert.Contains(gear.Id, followers);

            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { motor.Id }, new Vec2(5, 12)));
            Assert.Equal(shaft.Transform.X + 5, after.Get(shaft.Id)!.Transform.X, 6);
            Assert.Equal(shaft.Transform.Y + 12, after.Get(shaft.Id)!.Transform.Y, 6);
            Assert.Equal(gear.Transform.Y + 12, after.Get(gear.Id)!.Transform.Y, 6);
        }

        [Fact]
        public void AGearStillSlidesAlongItsShaft()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(70, 40));
            var doc = Doc(shaft, gear);
            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { gear.Id }, new Vec2(10, 0)));
            Assert.Equal(shaft.Transform, after.Get(shaft.Id)!.Transform);
        }

        [Theory]
        [InlineData("mech.belt", "open", "mech.pulley", "axial")]
        [InlineData("mech.belt", "loop", "mech.pulley", "axial")]
        [InlineData("mech.chain", "links", "mech.sprocket", "teeth")]
        public void ABeltFollowsAMovedPulleyTheOtherStays(string strandType, string strandView, string wheelType, string wheelView)
        {
            var a = Reg.Create(Reg.Library.First(i => i.TypeId == wheelType && i.View == wheelView), new Vec2(50, 50));
            var b = Reg.Create(Reg.Library.First(i => i.TypeId == wheelType && i.View == wheelView), new Vec2(130, 50));
            var strand = Reg.Create(Reg.Library.First(i => i.TypeId == strandType && i.View == strandView), new Vec2(90, 50));
            var doc = Doc(a, b, strand);
            // Fit the strand so its end hubs lie on both wheel centres.
            var l = Hub(doc, strand.Id, 0);
            var r = Hub(doc, strand.Id, 1);
            doc = Edits.Update(doc, new[] { strand.Id }, o => o with { Size = new Size2(o.Size.W + (80 - l.DistanceTo(r)), o.Size.H) });
            var off = new Vec2(50, 50) - Hub(doc, strand.Id, 0);
            doc = Edits.Move(doc, new[] { strand.Id }, off);
            Assert.Equal(130, Hub(doc, strand.Id, 1).X, 6);
            Assert.Equal(2, Links.For(Reg, doc, new[] { strand.Id }).Count(x => x.Kind == LinkKind.Spanned));

            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { b.Id }, new Vec2(10, 30)));
            Assert.Equal(doc.Get(a.Id)!.Transform, after.Get(a.Id)!.Transform);
            var l1 = Hub(after, strand.Id, 0);
            var r1 = Hub(after, strand.Id, 1);
            Assert.True(l1.DistanceTo(new Vec2(50, 50)) < 1e-6, $"left end {l1}");
            Assert.True(r1.DistanceTo(new Vec2(140, 80)) < 1e-6, $"right end {r1}");

            // Moving the strand itself leaves the wheels where they are.
            var moved = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { strand.Id }, new Vec2(0, 20)));
            Assert.Equal(doc.Get(a.Id)!.Transform, moved.Get(a.Id)!.Transform);
            Assert.Equal(doc.Get(b.Id)!.Transform, moved.Get(b.Id)!.Transform);
        }
    }
}
