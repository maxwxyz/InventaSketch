using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Moving gear trains: turning one part turns the train with the right ratios (<see cref="GearTrain"/>).</summary>
    public class GearMotionTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchDocument Add(SketchDocument doc, SketchObject o) => Edits.Add(doc, doc.Pages[0].Id, o);

        private static LibraryItem Item(string typeId, string view) => Reg.Library.First(i => i.TypeId == typeId && i.View == view);

        private static double R(SketchDocument doc, string id, AnchorRole role = AnchorRole.Pitch)
            => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(a => a.Role == role).Radius;

        /// <summary>Carrier with sun, three planets and ring, built with the build ports.</summary>
        private static (SketchDocument Doc, string Carrier, string Sun, string Ring, string[] Planets) Stage(bool fixedCarrier = false)
        {
            var carrier = Reg.Create(Item("mech.carrier", "axial"), new Vec2(100, 100));
            if (fixedCarrier) carrier = carrier.WithParam("fixed", true);
            var doc = Doc(carrier);
            var sun = Building.Place(Reg, doc, carrier.Id, Building.Ports(Reg, doc, carrier.Id).Single(p => p.Kind == PortKind.Hub), Item("mech.gear.spur", "axial"))!;
            doc = Add(doc, sun);
            var planets = new System.Collections.Generic.List<string>();
            foreach (var pin in Building.Ports(Reg, doc, carrier.Id).Where(p => p.Kind == PortKind.Pin).ToList())
            {
                var pl = Building.Place(Reg, doc, carrier.Id, pin, Item("mech.gear.spur", "axial"))!;
                doc = Add(doc, pl);
                planets.Add(pl.Id);
            }
            var ring = Building.Place(Reg, doc, carrier.Id, Building.Ports(Reg, doc, carrier.Id).Single(p => p.Kind == PortKind.Hub), Item("mech.gear.internal", "axial"))!;
            doc = Add(doc, ring);
            return (doc, carrier.Id, sun.Id, ring.Id, planets.ToArray());
        }

        [Fact]
        public void APairTurnsAgainstEachOtherWithTheRatio()
        {
            var a = Reg.Create(Item("mech.gear.spur", "axial"), new Vec2(60, 60)) with { Size = new Size2(30, 30) };
            var doc = Doc(a);
            var b = Building.Place(Reg, doc, a.Id, Building.Ports(Reg, doc, a.Id).First(p => p.Kind == PortKind.Mesh), Item("mech.gear.spur", "axial"))!;
            b = Reg.Type(b.Type)!.ScalePitch(b, 0.5);
            // Keep b in mesh after scaling: put it right below a.
            doc = Add(doc, b);
            var ra = R(doc, a.Id);
            var rb = R(doc, b.Id);
            doc = Edits.Update(doc, new[] { b.Id }, o => o with { Transform = o.Transform with { X = 60, Y = 60 + ra + rb } });
            var w = GearTrain.Speeds(Reg, doc, a.Id);
            Assert.Equal(1, w[a.Id], 9);
            Assert.Equal(-ra / rb, w[b.Id], 9);

            // Turning a by 30 degrees (a rotation edit) turns b by -30 ra/rb; nothing moves or scales.
            var turned = Coupling.Propagate(Reg, doc, Edits.Update(doc, new[] { a.Id }, o => o with { Transform = o.Transform with { Rotation = 30 } }));
            Assert.Equal(-30 * ra / rb, turned.Get(b.Id)!.Transform.Rotation, 4);
            Assert.Equal(doc.Get(b.Id)!.Transform.X, turned.Get(b.Id)!.Transform.X, 9);
            Assert.Equal(doc.Get(b.Id)!.Size, turned.Get(b.Id)!.Size);
        }

        [Fact]
        public void APlanetaryStageWithTheRingFixedDrivesTheCarrier()
        {
            var (doc, carrier, sun, ring, planets) = Stage();
            var rs = R(doc, sun);
            var rr = R(doc, ring, AnchorRole.PitchInner);
            var w = GearTrain.Speeds(Reg, doc, sun);
            Assert.Equal(0, w[ring], 9);
            Assert.Equal(rs / (rs + rr), w[carrier], 6);
            // Planets orbit with the carrier.
            var turned = GearTrain.Turn(Reg, doc, sun, 60);
            var p0 = doc.Get(planets[0])!.Transform;
            var p1 = turned.Get(planets[0])!.Transform;
            var c = new Vec2(100, 100);
            var a0 = Math.Atan2(p0.Y - c.Y, p0.X - c.X);
            var a1 = Math.Atan2(p1.Y - c.Y, p1.X - c.X);
            Assert.Equal(60 * rs / (rs + rr), (a1 - a0) * 180 / Math.PI, 4);
            Assert.Equal(new Vec2(p0.X, p0.Y).DistanceTo(c), new Vec2(p1.X, p1.Y).DistanceTo(c), 6);
        }

        [Fact]
        public void AFixedCarrierTurnsTheRingBackwards()
        {
            var (doc, carrier, sun, ring, planets) = Stage(fixedCarrier: true);
            var rs = R(doc, sun);
            var rr = R(doc, ring, AnchorRole.PitchInner);
            var w = GearTrain.Speeds(Reg, doc, sun);
            Assert.Equal(0, w[carrier], 9);
            Assert.Equal(-rs / rr, w[ring], 6);
            // Planets stay in place.
            var turned = GearTrain.Turn(Reg, doc, sun, 45);
            Assert.Equal(doc.Get(planets[0])!.Transform.X, turned.Get(planets[0])!.Transform.X, 9);
        }

        [Fact]
        public void TurningTheCarrierDrivesTheSun()
        {
            var (doc, carrier, sun, ring, _) = Stage();
            var rs = R(doc, sun);
            var rr = R(doc, ring, AnchorRole.PitchInner);
            var w = GearTrain.Speeds(Reg, doc, carrier);
            Assert.Equal(0, w[ring], 9);
            Assert.Equal((rs + rr) / rs, w[sun], 6);
        }

        [Fact]
        public void SideViewsDoNotSpinButPassTheirSpeedOn()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(70, 40));
            var loose = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(100, 40)).WithParam("hub", "loose");
            var doc = Doc(shaft, gear, loose);
            Assert.False(GearTrain.Spins(Reg, gear));
            var w = GearTrain.Speeds(Reg, doc, shaft.Id);
            Assert.Equal(1, w[gear.Id], 9);
            Assert.False(w.ContainsKey(loose.Id) && Math.Abs(w[loose.Id]) > 1e-9, "a loose gear does not turn with the shaft");
            // A side-view rotation is a re-orientation, not a turn: the train does not react.
            var rotated = Coupling.Propagate(Reg, doc, Edits.Update(doc, new[] { gear.Id }, o => o with { Transform = o.Transform with { Rotation = 10 } }));
            Assert.Equal(0, rotated.Get(loose.Id)!.Transform.Rotation, 9);
        }
    }
}
