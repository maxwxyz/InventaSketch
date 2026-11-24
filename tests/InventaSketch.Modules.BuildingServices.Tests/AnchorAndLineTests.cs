using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    public class AnchorTests
    {
        [Fact]
        public void EveryBoxElementHasPortsInEveryViewWithTheSameIds()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                var o = t.Create(Vec2.Zero);
                var ids = t.Views.Select(v => Fx.Anchors(r, r.WithView(o, v.Id, Fx.Ctx()), AnchorRole.Port).Select(a => a.Id!).OrderBy(x => x).ToList()).ToList();
                foreach (var (list, v) in ids.Zip(t.Views, (l, v) => (l, v)))
                {
                    Assert.True(list.Count > 0, $"{t.Id} [{v.Id}]: no ports");
                    Assert.Equal(list.Count, list.Distinct().Count());
                }
                if (t.Id == "bs.san.riser") continue; // plan: one riser port; elevation: bottom and top on a rail
                Assert.Equal(ids[0], ids[1]);
                Assert.Equal(ids[0], ids[2]);
            }
        }

        [Fact]
        public void PortsLieOnOrInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var v in t.Views)
                {
                    var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                    foreach (var a in t.Anchors(o, Fx.Ctx()))
                        Assert.True(Math.Abs(a.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(a.P.Y) <= o.Size.H / 2 + 1e-9, $"{t.Id} [{v.Id}] {a.Id} at {a.P}");
                }
        }

        [Fact]
        public void MountedElementsHaveMountsInlineFittingsDoNot()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var v in t.Views)
                {
                    var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                    var mounts = Fx.Anchors(r, o, AnchorRole.Mount);
                    if (t.Mount == Mounting.Inline) Assert.Empty(mounts);
                    else Assert.Single(mounts);
                }
        }

        [Theory]
        [InlineData("bs.heat.radiator", "wall", "wall")]
        [InlineData("bs.san.wc", "wall", "floor")]
        [InlineData("bs.heat.floorloop", "floor", "floor")]
        [InlineData("bs.vent.terminal", "ceiling", "ceiling")]
        [InlineData("bs.elec.detector", "ceiling", "ceiling")]
        public void MountIdsTellWhereTheElementSits(string typeId, string plan, string section)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, typeId);
            Assert.Equal(plan, Fx.Anchors(r, o, AnchorRole.Mount).Single().Id);
            Assert.Equal(section, Fx.Anchors(r, r.WithView(o, "section", Fx.Ctx()), AnchorRole.Mount).Single().Id);
        }

        [Fact]
        public void TheSectionMountIsTheHeightReference()
        {
            // Bottom edge centre (top edge for ceiling elements): the point the mounting height refers to.
            var r = Fx.Reg();
            var basin = r.WithView(Fx.Item(r, "bs.san.basin"), "section", Fx.Ctx());
            Assert.Equal(new Vec2(0, basin.Size.H / 2), Fx.Anchors(r, basin, AnchorRole.Mount).Single().P);
        }

        [Fact]
        public void ManifoldPortsFollowTheNumberOfCircuits()
        {
            var r = Fx.Reg();
            var m = Fx.Item(r, "bs.heat.manifold.8");
            var ports = Fx.Anchors(r, m, AnchorRole.Port).Select(a => a.Id).ToList();
            Assert.Equal(2 + 16, ports.Count);
            Assert.Contains("f8", ports);
            Assert.Contains("r8", ports);
            var more = r.TypeOf(m).Actions(m, Lang.En).Single(a => a.Id == "bs.manifold.more").Apply(m);
            Assert.Equal(9, more.Int("circuits", 0));
        }

        [Fact]
        public void RiserOffersARailInElevationViews()
        {
            var r = Fx.Reg();
            var riser = Fx.Item(r, "bs.san.riser");
            Assert.Empty(Fx.Anchors(r, riser, AnchorRole.Rail));
            foreach (var v in new[] { "section", "schematic" })
            {
                var rail = Fx.Anchors(r, r.WithView(riser, v, Fx.Ctx()), AnchorRole.Rail).Single();
                Assert.Equal(28, rail.P.DistanceTo(rail.Q), 6);
            }
        }

        [Fact]
        public void HeightCanBeTypedInMillimetresOrMetres()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "bs.heat.radiator");
            var a = r.TypeOf(o).Actions(o, Lang.En).Single(x => x.Id == "bs.height.set");
            Assert.Equal("150", a.Input!.Value);
            Assert.Equal(1200, a.Input.Apply(o, "1,2 m").Num("height", 0), 6);
            Assert.Equal(-50, a.Input.Apply(o, "-50").Num("height", 0), 6);
            Assert.Throws<FormatException>(() => a.Input.Apply(o, "abc"));
        }
    }

    public class LineTests
    {
        [Theory]
        [InlineData("bs.pipe")]
        [InlineData("bs.duct")]
        public void LinesTakeTheLineLanguage(string typeId)
        {
            var r = Fx.Reg();
            var line = Fx.Item(r, typeId);
            var (doc, _) = Fx.Doc(line);
            Assert.True(Polylines.CanInsert(r, line));
            Assert.True(Polylines.CanExtend(r, line));
            Assert.True(Polylines.CanBranch(r, doc, line));
            doc = Polylines.InsertCorner(r, doc, line.Id, 1, new Vec2(0, -10));
            Assert.Equal(3, doc.Get(line.Id)!.Points!.Count);
            var (doc2, branchId) = Polylines.Branch(r, doc, line.Id, new Vec2(-10, -5), new Vec2(-10, 20));
            Assert.NotNull(branchId);
            var branch = doc2.Get(branchId!)!;
            Assert.Equal(typeId, branch.Type);
            Assert.StartsWith("rail", branch.Attach![0]!.AnchorId);
        }

        [Fact]
        public void APipeBranchShowsAJunctionDotAndPipesHopOverEachOther()
        {
            var r = Fx.Reg();
            var a = Fx.Item(r, "bs.pipe.cold");
            var (doc, _) = Fx.Doc(a);
            var (doc2, branchId) = Polylines.Branch(r, doc, a.Id, new Vec2(5, 0), new Vec2(5, 15));
            var branch = doc2.Get(branchId!)!;
            Assert.Contains(r.Render(branch, new RenderContext(doc2)), p => p.Tag == BsTags.Junction);

            var crossing = Fx.Item(r, "bs.pipe.hot") with { Points = new[] { new Vec2(-5, -10), new Vec2(-5, 10) } };
            var doc3 = Edits.Add(doc2, doc2.Pages[0].Id, crossing);
            Assert.True(r.TypeOf(crossing).HopsAtCrossings);
            var main = r.Render(crossing, new RenderContext(doc3)).Single(p => p.Tag == BsTags.Pipe);
            Assert.Contains(((PathPrim)main).Segments, s => s.Op == SegOp.C);
            Assert.Contains(crossing.Id, r.TypeOf(a).Dependents(a, new RenderContext(doc3)));
        }

        [Fact]
        public void DuctIsADoubleLineOfItsWidthAtThePlanScale()
        {
            var r = Fx.Reg();
            var duct = Fx.Item(r, "bs.duct.supply");
            var prims = r.Render(duct, Fx.Ctx());
            var edges = prims.Where(p => p.Tag == BsTags.Duct).Cast<PathPrim>().ToList();
            Assert.Equal(2, edges.Count);
            var gap = Math.Abs(edges[0].Segments[0].P.Y - edges[1].Segments[0].P.Y);
            Assert.Equal(1.6, gap, 6); // 160 mm at 1:100
            var doc = SketchDocument.Create() with { Settings = new DocumentSettings { PlanScale = 50 } };
            var at50 = r.Render(duct, new RenderContext(doc)).Where(p => p.Tag == BsTags.Duct).Cast<PathPrim>().ToList();
            Assert.Equal(3.2, Math.Abs(at50[0].Segments[0].P.Y - at50[1].Segments[0].P.Y), 6);
            // Schematic: one line with the air code.
            var sch = r.Render(duct with { View = "schematic" }, Fx.Ctx());
            Assert.Single(sch, p => p.Tag == BsTags.Duct);
            Assert.Contains(sch, p => p is TextPrim { Text: "SUP" });
        }

        [Fact]
        public void DuctCornersAreMitred()
        {
            var left = DuctType.Offset(new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10) }, 1);
            Assert.Equal(new Vec2(0, -1), left[0]);
            Assert.Equal(11, left[1].X, 6);
            Assert.Equal(-1, left[1].Y, 6);
        }

        [Fact]
        public void PipeSchematicNamesMediumAndNominalSize()
        {
            var r = Fx.Reg();
            var pipe = Fx.Item(r, "bs.pipe.flow").WithParam("dn", 20.0) with { View = "schematic" };
            Assert.Contains(r.Render(pipe, Fx.Ctx(Lang.De)), p => p is TextPrim { Text: "VL DN 20" });
            Assert.DoesNotContain(r.Render(pipe with { View = "plan" }, Fx.Ctx()), p => p is TextPrim);
        }

        [Fact]
        public void PipeEndsDockOnPorts()
        {
            var r = Fx.Reg();
            var radiator = Fx.Item(r, "bs.heat.radiator", new Vec2(50, 50));
            var flow = r.PageAnchors(radiator, Fx.Ctx()).Single(a => a.Role == AnchorRole.Port && a.Id == "flow");
            Assert.Equal(new Vec2(45, 50), flow.P);
        }
    }
}
