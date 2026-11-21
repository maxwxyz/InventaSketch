using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Predefined linkages explode into pivots, links and sliders that form a self-built linkage (<see cref="Assemblies"/>).</summary>
    public class ExplodeMechanismTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static double Len(SketchDocument doc, SketchObject link)
        {
            var p = doc.ResolvePoints(doc.Get(link.Id)!);
            return p[0].DistanceTo(p[1]);
        }

        [Fact]
        public void AFourBarExplodesIntoALinkageThatMovesTheSameWay()
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.mech.fourbar"), new Vec2(120, 90)) with { Transform = new Transform(120, 90, 15) };
            var doc = Doc(mech);
            Assert.True(Assemblies.CanExplode(Reg, mech));
            var parts = Assemblies.Explode(Reg, doc, mech.Id);
            Assert.Equal(2, parts.Count(p => p.Type == "kin.joint.ground"));
            var links = parts.Where(p => p.Type == "kin.link.binary").ToList();
            Assert.Equal(3, links.Count);
            var exploded = Doc(parts.ToArray());

            // The joints of the exploded linkage sit where the mechanism's joints were.
            var ctx = new RenderContext(doc);
            var joints = Reg.PageAnchors(mech, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role is AnchorRole.Port or AnchorRole.Mount && a.Id is "O2" or "A" or "B" or "O4").GroupBy(a => a.Id!).ToDictionary(g => g.Key, g => g.First().P);
            var crank = exploded.ResolvePoints(exploded.Get(links[0].Id)!);
            Assert.True(crank[0].DistanceTo(joints["O2"]) < 1e-6, "crank on O2");
            Assert.True(crank[1].DistanceTo(joints["A"]) < 1e-6, "crank tip at A");
            var rocker = exploded.ResolvePoints(exploded.Get(links[2].Id)!);
            Assert.True(rocker[1].DistanceTo(joints["B"]) < 1e-6, "rocker at B");

            Assert.Equal(1, Linkage.Dof(Reg, exploded, links[1].Id)!.Value.Dof);
            var lengths = links.Select(l => Len(exploded, l)).ToList();
            var moved = Linkage.Drag(Reg, exploded, links[0].Id, 1, crank[1] + new Vec2(-2, 3));
            for (var i = 0; i < 3; i++) Assert.Equal(lengths[i], Len(moved, links[i]), 3);
        }

        [Fact]
        public void ASliderCrankExplodesWithItsSliderOnTheGuide()
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.mech.slidercrank"), new Vec2(100, 100));
            var parts = Assemblies.Explode(Reg, Doc(mech), mech.Id);
            Assert.Single(parts, p => p.Type == "kin.joint.prismatic");
            var exploded = Doc(parts.ToArray());
            var crank = parts.First(p => p.Type == "kin.link.binary");
            Assert.Equal(1, Linkage.Dof(Reg, exploded, crank.Id)!.Value.Dof);
            var rod = parts.Last(p => p.Type == "kin.link.binary");
            var before = exploded.ResolvePoints(exploded.Get(rod.Id)!)[1];
            var tip = exploded.ResolvePoints(exploded.Get(crank.Id)!)[1];
            var moved = Linkage.Drag(Reg, exploded, crank.Id, 1, tip + new Vec2(0, 4));
            var after = moved.ResolvePoints(moved.Get(rod.Id)!)[1];
            Assert.Equal(before.Y, after.Y, 3); // the slider stays on its guide
            Assert.Equal(Len(exploded, rod), Len(moved, rod), 3);
        }
    }
}
