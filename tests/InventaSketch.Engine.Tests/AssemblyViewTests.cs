using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>A gear train switched between axial and stick view keeps its mechanism (<see cref="AssemblyViews"/>).</summary>
    public class AssemblyViewTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Gear(string view, Vec2 at, int teeth)
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == view);
            return Reg.Create(item, at).WithParam("teeth", teeth);
        }

        private static double Pitch(SketchDocument doc, string id)
            => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch).Radius;

        private static Vec2 C(SketchDocument doc, string id) => new(doc.Get(id)!.Transform.X, doc.Get(id)!.Transform.Y);

        [Fact]
        public void ATwoStageTrainUnfoldsIntoStickViewAndFoldsBack()
        {
            // Axial view: A meshes B, C sits on B's shaft, D meshes C.
            var a = Gear("axial", new Vec2(100, 100), 20);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, a);
            var ra = Pitch(doc, a.Id);
            var b = Gear("axial", new Vec2(100, 100), 40);
            doc = Edits.Add(doc, doc.Pages[0].Id, b);
            var rb = Pitch(doc, b.Id);
            doc = Edits.Update(doc, new[] { b.Id }, o => o with { Transform = o.Transform with { X = 100 + ra + rb } });
            var c = Gear("axial", new Vec2(100 + ra + rb, 100), 18);
            doc = Edits.Add(doc, doc.Pages[0].Id, c);
            var rc = Pitch(doc, c.Id);
            var d = Gear("axial", new Vec2(100 + ra + rb, 100), 36);
            doc = Edits.Add(doc, doc.Pages[0].Id, d);
            var rd = Pitch(doc, d.Id);
            doc = Edits.Update(doc, new[] { d.Id }, o => o with { Transform = o.Transform with { Y = 100 + rc + rd } });
            var ids = new[] { a.Id, b.Id, c.Id, d.Id };
            Assert.Equal(2, Coupling.Graph(Reg, doc).Count(g => g.Kind == LinkKind.Mesh));

            var stick = AssemblyViews.Switch(Reg, doc, ids, "stick");
            Assert.All(ids, id => Assert.Equal("stick", Reg.Type(stick.Get(id)!.Type)!.ViewOf(stick.Get(id)!)));
            // Meshing gears share their place along the (horizontal) axis; C is stacked beside B on B's shaft.
            Assert.Equal(C(stick, a.Id).X, C(stick, b.Id).X, 6);
            Assert.Equal(C(stick, c.Id).X, C(stick, d.Id).X, 6);
            Assert.True(C(stick, c.Id).X > C(stick, b.Id).X + 1);
            Assert.Equal(C(stick, b.Id).Y, C(stick, c.Id).Y, 6);
            Assert.Equal(ra + rb, C(stick, a.Id).DistanceTo(C(stick, b.Id)), 3);
            Assert.Equal(rc + rd, C(stick, c.Id).DistanceTo(C(stick, d.Id)), 3);
            // The couplings hold in the new view: two mesh pairs.
            Assert.Equal(2, Coupling.Graph(Reg, stick).Count(g => g.Kind == LinkKind.Mesh));

            var back = AssemblyViews.Switch(Reg, stick, ids, "axial");
            Assert.Equal(C(back, b.Id).X, C(back, c.Id).X, 6);
            Assert.Equal(C(back, b.Id).Y, C(back, c.Id).Y, 6);
            Assert.Equal(ra + rb, C(back, a.Id).DistanceTo(C(back, b.Id)), 3);
            Assert.Equal(rc + rd, C(back, c.Id).DistanceTo(C(back, d.Id)), 3);
            Assert.Equal(2, Coupling.Graph(Reg, back).Count(g => g.Kind == LinkKind.Mesh));
        }

        [Fact]
        public void AStickPlanetarySetExplodesIntoStickParts()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gearset.planetary" && (i.View ?? "stick") == "stick");
            var set = Reg.Create(item, new Vec2(120, 90));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, set);
            Assert.True(Assemblies.CanExplode(Reg, set));
            var parts = Assemblies.Explode(Reg, doc, set.Id);
            Assert.True(parts.Count >= 5);
            Assert.All(parts.Where(p => p.Type.StartsWith("mech.gear", System.StringComparison.Ordinal)), p => Assert.True(AssemblyViews.InPlane(Reg, p, new RenderContext(doc)), p.Type + " in " + p.View));
            var exploded = SketchDocument.Create();
            exploded = Edits.Add(exploded, exploded.Pages[0].Id, parts.ToArray());
            // Sun and planets still mesh in the stick view.
            Assert.Contains(Coupling.Graph(Reg, exploded), g => g.Kind == LinkKind.Mesh);
        }

        [Fact]
        public void ADifferentialExplodesIntoCaseSideAndSpiderGearsThatMesh()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gearset.differential" && i.View == "stick");
            var diff = Reg.Create(item, new Vec2(100, 100));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, diff);
            Assert.True(Assemblies.CanExplode(Reg, diff));
            var parts = Assemblies.Explode(Reg, doc, diff.Id);
            Assert.Single(parts, p => p.Type == "mech.diffcase");
            Assert.Equal(4, parts.Count(p => p.Type == "mech.gear.bevel"));
            var exploded = SketchDocument.Create();
            exploded = Edits.Add(exploded, exploded.Pages[0].Id, parts.ToArray());
            // Every side gear meshes with both spider gears (common cone apex in the centre).
            Assert.Equal(4, Coupling.Graph(Reg, exploded).Count(g => g.Kind == LinkKind.Mesh));
            // It works as a differential: the case drives both side gears alike when driving straight (spider gears still);
            // with the case held the side gears turn opposite ways; with one side held the other turns twice as fast.
            var cage = parts.Single(p => p.Type == "mech.diffcase").Id;
            var left = parts.Where(p => p.Type == "mech.gear.bevel").OrderBy(p => p.Transform.X).First().Id;
            var right = parts.Where(p => p.Type == "mech.gear.bevel").OrderBy(p => p.Transform.X).Last().Id;
            var spider = parts.Where(p => p.Type == "mech.gear.bevel").OrderBy(p => p.Transform.Y).First().Id;
            var straight = GearTrain.Speeds(Reg, exploded, cage);
            Assert.Equal(1, straight[left], 6);
            Assert.Equal(-1, straight[right], 6); // about its own axis to the apex = same sense as the case
            Assert.Equal(0, straight[spider], 6);
            var held = GearTrain.Speeds(Reg, Edits.Update(exploded, new[] { cage }, o => o.WithParam("fixed", true)), left);
            Assert.Equal(0, held[cage], 6);
            Assert.Equal(1, held[right], 6);      // opposite sense on the axle
            var oneWheel = Edits.Update(exploded, new[] { right }, o => o.WithParam("fixed", true));
            Assert.Equal(2, GearTrain.Speeds(Reg, oneWheel, cage)[left], 6);
            // The section view explodes through the stick view.
            var section = Reg.Library.First(i => i.TypeId == "mech.gearset.differential" && i.View == "section");
            Assert.True(Assemblies.CanExplode(Reg, Reg.Create(section, new Vec2(0, 0))));
        }

        [Fact]
        public void ARavigneauxSetExplodesIntoPlanetPairsWithTheRightRatios()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gearset.ravigneaux" && i.View == "axial");
            var set = Reg.Create(item, new Vec2(100, 100));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, set);
            var parts = Assemblies.Explode(Reg, doc, set.Id);
            Assert.Single(parts, p => p.Type == "mech.carrier");
            Assert.Single(parts, p => p.Type == "mech.gear.internal");
            Assert.Equal(8, parts.Count(p => p.Type == "mech.gear.spur")); // two suns, three long and three short planets
            var ex = SketchDocument.Create();
            ex = Edits.Add(ex, ex.Pages[0].Id, parts.ToArray());
            // Per pair: short–small sun, short–long, long–large sun, long–ring.
            Assert.Equal(12, Coupling.Graph(Reg, ex).Count(g => g.Kind == LinkKind.Mesh));

            string Teeth(int z) => parts.First(p => p.Type == "mech.gear.spur" && p.Transform.X == 100 && p.Transform.Y == 100 && p.Num("teeth", 0) == z).Id;
            var small = Teeth(14);
            var large = Teeth(20);
            var ring = parts.Single(p => p.Type == "mech.gear.internal").Id;
            var carrier = parts.Single(p => p.Type == "mech.carrier").Id;
            // Ring held, small sun in: reverse, ω_c = −z_s / (z_r − z_s) = −14/38.
            var reverse = Edits.Update(ex, new[] { ring }, o => o.WithParam("fixed", true));
            Assert.Equal(-14.0 / 38, GearTrain.Speeds(Reg, reverse, small)[carrier], 6);
            // Large sun held, small sun in: ω_c = z_s / (z_s + z_L) = 14/34.
            var held = Edits.Update(ex, new[] { large }, o => o.WithParam("fixed", true));
            Assert.Equal(14.0 / 34, GearTrain.Speeds(Reg, held, small)[carrier], 6);
            // The schematic explodes through the axial view.
            Assert.True(Assemblies.CanExplode(Reg, Reg.Create(Reg.Library.First(i => i.TypeId == "mech.gearset.ravigneaux" && i.View == "stick"), new Vec2(0, 0))));
        }

        [Fact]
        public void WithinTheSameKindOfViewNothingMoves()
        {
            var a = Gear("stick", new Vec2(50, 50), 20);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, a);
            var side = Reg.Type("mech.gear.spur")!.Views.FirstOrDefault(v => v.Id == "side");
            if (side is null) return;
            var moved = AssemblyViews.Switch(Reg, doc, new[] { a.Id }, "side");
            Assert.Equal(C(doc, a.Id), C(moved, a.Id));
        }
    }
}
