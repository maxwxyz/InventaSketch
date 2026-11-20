using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new MechanicalModule());

        public static RenderContext Ctx() => new(SketchDocument.Create());

        /// <summary>All (library item, view) combinations.</summary>
        public static IEnumerable<(LibraryItem Item, ObjectType Type, string View)> ItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var b = Prim.Bounds(prims);
            Assert.True(b.HasValue, what + ": no bounds");
            var r = b!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }
    }

    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var r = Fx.Reg();
            var ids = r.Types.Select(t => t.Id).ToList();
            foreach (var id in new[]
            {
                "mech.gear.spur", "mech.gear.internal", "mech.gear.bevel", "mech.gear.worm", "mech.gear.wormwheel", "mech.gear.rack",
                "mech.gearset.planetary", "mech.gearset.differential", "mech.shaft",
                "mech.bearing.ball", "mech.bearing.angular", "mech.bearing.taperroller", "mech.bearing.cylroller", "mech.bearing.needle",
                "mech.bearing.thrust", "mech.bearing.plain", "mech.support.fixed", "mech.support.floating",
                "mech.coupling.rigid", "mech.coupling.flexible", "mech.joint.universal", "mech.clutch.disc", "mech.clutch.multidisc",
                "mech.brake", "mech.sync", "mech.housing.ground", "mech.pulley", "mech.belt", "mech.sprocket", "mech.chain",
                "mech.spring.compression", "mech.spring.tension", "mech.spring.torsion", "mech.cam", "mech.seal", "mech.key", "mech.spline",
                "mech.screw", "mech.nut", "mech.washer", "mech.rivet", "mech.join.weld", "mech.join.adhesive", "mech.join.solder",
                "mech.join.press", "mech.guide.linear", "mech.flywheel", "mech.arrow.rotation", "mech.arrow.torque", "mech.arrow.powerflow",
            })
                Assert.Contains(id, ids);
        }

        [Fact]
        public void EverythingIsBilingualAndCategorised()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.NotEmpty(t.Views);
                foreach (var v in t.Views) Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Theory]
        [InlineData("zahnrad")]
        [InlineData("gear")]
        [InlineData("lager")]
        [InlineData("losrad")]
        [InlineData("planeten")]
        [InlineData("kupplung")]
        [InlineData("feder")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                // A bare object without any parameters must render (quick sketch).
                var o = new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
        }
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideTheBox()
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.ItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero) with { View = view };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count > 200, "expected many item/view combinations, got " + count);
        }

        [Theory]
        [InlineData(2.5, 0.6)]
        [InlineData(0.4, 1.8)]
        [InlineData(3, 3)]
        [InlineData(0.3, 0.3)]
        public void FreeNonUniformScalingKeepsPrimitivesInsideTheBox(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.ItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero) with { View = view };
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
            }
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var gear = r.Create(r.Item("mech.spur.teeth")!, new Vec2(50, 50));
            doc = Edits.Add(doc, doc.Pages[0].Id, gear);
            doc = Edits.Scale(doc, new[] { gear.Id }, new Vec2(50, 50), 2, 0.5);
            var g = doc.Get(gear.Id)!;
            Assert.Equal(18, g.Int("teeth", 0));
            Assert.Equal(60, g.Size.W, 6);
            Assert.Equal(15, g.Size.H, 6);
        }

        [Fact]
        public void SpurGearViewsUseConventionalLineRoles()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gear.spur")!;
            var o = r.Create(r.Item("mech.spur.axial")!, Vec2.Zero);
            var prims = t.Render(o, Fx.Ctx());
            Assert.Contains(prims, p => p.Tag == "tip" && p is EllipsePrim e && e.Style.Role == LineRole.Visible);
            Assert.Contains(prims, p => p.Tag == "pitch" && p is EllipsePrim e && e.Style.Role == LineRole.Center);
            Assert.Contains(prims, p => p.Tag == "center" && p is LinePrim l && l.Style.Role == LineRole.Center);

            var section = t.Render(o with { View = "section", Size = new Size2(10, 30) }, Fx.Ctx());
            Assert.Equal(2, section.Count(p => p is RectPrim rp && rp.Style.Fill?.Kind == FillKind.Hatch));

            var side = t.Render(o with { View = "side", Size = new Size2(10, 30) }, Fx.Ctx());
            Assert.Contains(side, p => p.Tag == "bore" && p is LinePrim l && l.Style.Role == LineRole.Hidden);
            Assert.DoesNotContain(side, p => p.Tag == "helix");
            var helical = t.Render(o.WithParam("helix", true) with { View = "side", Size = new Size2(10, 30) }, Fx.Ctx());
            Assert.Equal(3, helical.Count(p => p.Tag == "helix"));
        }

        [Fact]
        public void TeethCountChangesToothOutline()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gear.spur")!;
            var o = r.Create(r.Item("mech.spur.teeth")!, Vec2.Zero);
            var a = (PathPrim)t.Render(o, Fx.Ctx()).First(p => p.Tag == "teeth");
            var b = (PathPrim)t.Render(o.WithParam("teeth", 36), Fx.Ctx()).First(p => p.Tag == "teeth");
            Assert.Equal(2 * (a.Segments.Count - 1), b.Segments.Count - 1); // twice the teeth, twice the outline points
        }

        [Theory]
        [InlineData("none", 0)]
        [InlineData("fixed", 2)]
        [InlineData("sliding", 1)]
        [InlineData("loose", 2)]
        public void HubSymbolsInStickView(string hub, int hubPrims)
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gear.spur")!;
            var o = r.Create(r.Item("mech.spur.stick")!, Vec2.Zero).WithParam("hub", hub);
            Assert.Equal(hubPrims, t.Render(o, Fx.Ctx()).Count(p => p.Tag == "hub"));
        }
    }

    public class SnappingTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        private static (Registry R, SketchDocument Doc) Doc(params SketchObject[] objs)
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, objs);
            return (r, doc);
        }

        [Fact]
        public void AxialViewsExposePitchCircleAndHub()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "mech.spur.axial", "mech.spur.teeth", "mech.wormwheel.axial", "mech.pulley.axial", "mech.sprocket.teeth" })
            {
                var o = r.Create(r.Item(id)!, new Vec2(10, 10));
                var anchors = r.PageAnchors(o, Fx.Ctx()).ToList();
                var pitch = Assert.Single(anchors, a => a.Role == AnchorRole.Pitch);
                Assert.InRange(pitch.Radius, 0.38 * o.Size.W, 0.46 * o.Size.W);
                Assert.Contains(anchors, a => a.Role == AnchorRole.Hub && a.P == new Vec2(10, 10));
            }
            var ring = r.Create(r.Item("mech.ring.teeth")!, Vec2.Zero);
            Assert.Contains(r.PageAnchors(ring, Fx.Ctx()), a => a.Role == AnchorRole.PitchInner);
            var set = r.PageAnchors(r.Create(r.Item("mech.planetary.axial")!, Vec2.Zero), Fx.Ctx()).ToList();
            Assert.Contains(set, a => a.Role == AnchorRole.PitchInner);
            Assert.Contains(set, a => a.Role == AnchorRole.Pitch);
            Assert.Contains(set, a => a.Role == AnchorRole.Hub);
        }

        [Fact]
        public void TwoSpurGearsSnapIntoMesh()
        {
            var r0 = Fx.Reg();
            var big = r0.Create(r0.Item("mech.spur.teeth")!, new Vec2(100, 80)) with { Size = new Size2(40, 40) };
            var small = r0.Create(r0.Item("mech.spur.teeth")!, new Vec2(125.5, 81)) with { Size = new Size2(20, 20) };
            var (r, doc) = Doc(big, small);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(small, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { small.Id }, ctx, Opt);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("mesh", res.Kind);
            var rBig = r.PageAnchors(big, ctx).Single(a => a.Role == AnchorRole.Pitch).Radius;
            var rSmall = moving.Single(a => a.Role == AnchorRole.Pitch).Radius;
            var newCentre = new Vec2(125.5, 81) + res.Delta;
            Assert.Equal(rBig + rSmall, newCentre.DistanceTo(new Vec2(100, 80)), 6);
        }

        [Fact]
        public void PlanetMeshesInsideRingGear()
        {
            var r0 = Fx.Reg();
            var ring = r0.Create(r0.Item("mech.ring.teeth")!, new Vec2(100, 100)) with { Size = new Size2(60, 60) };
            var ringPitch = r0.PageAnchors(ring, Fx.Ctx()).Single(a => a.Role == AnchorRole.PitchInner).Radius;
            var planet = r0.Create(r0.Item("mech.spur.teeth")!, new Vec2(100, 100 - ringPitch + 9)) with { Size = new Size2(16, 16) };
            var (r, doc) = Doc(ring, planet);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(planet, ctx).ToList();
            var rp = moving.Single(a => a.Role == AnchorRole.Pitch).Radius;
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { planet.Id }, ctx, Opt);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt with { Tolerance = 5 });
            Assert.Equal("mesh", res.Kind);
            var c = new Vec2(planet.Transform.X, planet.Transform.Y) + res.Delta;
            Assert.Equal(ringPitch - rp, c.DistanceTo(new Vec2(100, 100)), 6);
        }

        [Theory]
        [InlineData("mech.spur.stick")]
        [InlineData("mech.bevel.stick")]
        [InlineData("mech.bearing.ball.stick")]
        [InlineData("mech.support.fixed")]
        [InlineData("mech.clutch.disc")]
        [InlineData("mech.pulley.stick")]
        [InlineData("mech.arrow.rotation")]
        public void HubSnapsOntoShaftAxis(string itemId)
        {
            var r0 = Fx.Reg();
            var shaft = r0.Create(r0.Item("mech.shaft.line")!, new Vec2(100, 50));
            var part = r0.Create(r0.Item(itemId)!, new Vec2(90, 52.2));
            var (r, doc) = Doc(shaft, part);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(part, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { part.Id }, ctx, Opt);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-2.2, res.Delta.Y, 6);
            Assert.Equal(0, res.Delta.X, 6);
        }

        [Fact]
        public void ShaftExposesAxisAndEnds()
        {
            var r = Fx.Reg();
            var shaft = r.Create(r.Item("mech.shaft.body")!, new Vec2(100, 50));
            var anchors = r.PageAnchors(shaft, Fx.Ctx()).ToList();
            var axis = Assert.Single(anchors, a => a.Role == AnchorRole.Axis);
            Assert.Equal(new Vec2(40, 50), axis.P);
            Assert.Equal(new Vec2(160, 50), axis.Q);
            Assert.Equal(2, anchors.Count(a => a.Role == AnchorRole.End));
        }

        [Fact]
        public void RotatedShaftAxisFollowsRotation()
        {
            var r = Fx.Reg();
            var shaft = r.Create(r.Item("mech.shaft.line")!, new Vec2(0, 0)) with { Transform = new Transform(0, 0, 90) };
            var axis = r.PageAnchors(shaft, Fx.Ctx()).Single(a => a.Role == AnchorRole.Axis);
            Assert.Equal(0, axis.P.X, 6);
            Assert.Equal(60, Math.Abs(axis.P.Y), 6);
        }
    }

    public class ActionTests
    {
        [Fact]
        public void EveryMultiViewTypeOffersOneActionPerView()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Views.Count > 1))
            {
                var o = t.Create(Vec2.Zero);
                var views = t.Actions(o, Lang.En).Where(a => a.Group == "view").ToList();
                Assert.Equal(t.Views.Count, views.Count);
                Assert.Single(views, a => a.IsActive);
                foreach (var v in t.Views)
                {
                    var a = views.Single(x => x.Id == "view." + v.Id);
                    Assert.Equal("view-" + v.Id, a.Icon);
                    Assert.Equal(v.Id, a.Apply(o).View);
                }
            }
        }

        [Fact]
        public void SwitchingToAxialViewMakesTheGearRound()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gear.spur")!;
            var o = r.Create(r.Item("mech.spur.stick")!, Vec2.Zero);
            var axial = t.Actions(o, Lang.En).Single(a => a.Id == "view.axial").Apply(o);
            Assert.Equal(axial.Size.H, axial.Size.W, 6);
            var back = t.Actions(axial, Lang.De).Single(a => a.Id == "view.stick").Apply(axial);
            Assert.Equal(o.Size.W, back.Size.W, 6);
            // switching between views of similar aspect keeps the size
            var side = t.Actions(o, Lang.En).Single(a => a.Id == "view.side").Apply(o);
            Assert.Equal(o.Size, side.Size);
        }

        [Fact]
        public void HubCycleAndHelixToggle()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gear.spur")!;
            var o = r.Create(r.Item("mech.spur.stick")!, Vec2.Zero);
            var seen = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                o = t.Actions(o, Lang.En).Single(a => a.Id == "param.hub").Apply(o);
                seen.Add(o.Str("hub", ""));
            }
            Assert.Equal(new[] { "sliding", "loose", "none", "fixed" }, seen);
            var helix = t.Actions(o, Lang.En).Single(a => a.Id == "param.helix");
            Assert.False(helix.IsActive);
            Assert.True(helix.Apply(o).Flag("helix", false));
            Assert.Equal("param", helix.Group);
        }

        [Fact]
        public void TrueSizeFromModuleIsOfferedOnlyWithModule()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gear.spur")!;
            var o = r.Create(r.Item("mech.spur.axial")!, Vec2.Zero);
            Assert.DoesNotContain(t.Actions(o, Lang.En), a => a.Id == "param.truesize");
            o = o.WithParam("module", 2.0).WithParam("teeth", 20);
            var sized = t.Actions(o, Lang.En).Single(a => a.Id == "param.truesize").Apply(o);
            // tip diameter m (z + 2) = 44 mm
            var tip = (EllipsePrim)t.Render(sized, Fx.Ctx()).First(p => p.Tag == "tip");
            Assert.Equal(44, 2 * tip.Rx, 6);
        }

        [Fact]
        public void ShaftHollowToggle()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.shaft")!;
            var o = r.Create(r.Item("mech.shaft.body")!, Vec2.Zero);
            Assert.DoesNotContain(t.Render(o, Fx.Ctx()), p => p.Tag == "bore");
            var hollow = t.Actions(o, Lang.En).Single(a => a.Id == "param.hollow").Apply(o);
            Assert.Equal(2, t.Render(hollow, Fx.Ctx()).Count(p => p.Tag == "bore"));
            Assert.True(t.Actions(hollow, Lang.En).Single(a => a.Id == "param.hollow").IsActive);
            var viaView = r.Create(r.Item("mech.shaft.hollow")!, Vec2.Zero);
            var solid = t.Actions(viaView, Lang.En).Single(a => a.Id == "param.hollow").Apply(viaView);
            Assert.Equal("body", solid.View);
            Assert.DoesNotContain(t.Render(solid, Fx.Ctx()), p => p.Tag == "bore");
        }
    }

    public class GearboxDocumentTests
    {
        /// <summary>Two-stage spur gearbox: 2 shafts, 4 gears, 4 bearings, a housing and a power-flow arrow.</summary>
        internal static (Registry R, SketchDocument Doc) Gearbox()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Two-stage gearbox");
            SketchObject At(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = r.Create(r.Item(item)!, new Vec2(x, y));
                return f is null ? o : f(o);
            }
            var objs = new List<SketchObject>
            {
                At("mech.shaft.line", 160, 60, o => o with { Ref = "10" }),
                At("mech.shaft.line", 180, 110, o => o with { Ref = "12" }),
                At("mech.spur.stick", 140, 60, o => o with { Size = new Size2(8, 24), Ref = "14" }),
                At("mech.spur.stick", 140, 110 - 22, o => o with { Size = new Size2(8, 20) }),
                At("mech.spur.loose", 190, 60, o => o with { Size = new Size2(8, 36) }),
                At("mech.spur.stick", 190, 110, o => o with { Size = new Size2(8, 14) }),
                At("mech.support.fixed", 110, 60),
                At("mech.support.floating", 210, 60),
                At("mech.bearing.ball.stick", 125, 110),
                At("mech.bearing.ball.stick", 230, 110),
                At("mech.ground", 170, 30, o => o with { Size = new Size2(140, 5) }),
                At("mech.arrow.powerflow.u", 165, 85, o => o with { Size = new Size2(70, 50) }),
                At("mech.arrow.rotation", 100, 60),
            };
            doc = Edits.Add(doc, doc.Pages[0].Id, objs.ToArray());
            doc = Edits.SetReference(doc, "10", "input shaft");
            return (r, doc);
        }

        [Fact]
        public void GearboxJsonRoundTrip()
        {
            var (_, doc) = Gearbox();
            Assert.Equal(2, doc.Objects.Values.Count(o => o.Type == "mech.shaft"));
            Assert.Equal(4, doc.Objects.Values.Count(o => o.Type == "mech.gear.spur"));
            Assert.Equal(4, doc.Objects.Values.Count(o => o.Type.StartsWith("mech.bearing.") || o.Type.StartsWith("mech.support.")));
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var loose = back.Objects.Values.Single(o => o.Type == "mech.gear.spur" && o.Str("hub", "") == "loose");
            Assert.Equal(18, loose.Int("teeth", 0));
            Assert.Equal("stick", loose.View);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
        }

        [Fact]
        public void GearboxRendersToSvg()
        {
            var (r, doc) = Gearbox();
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
            foreach (var o in doc.Objects.Values) Assert.Contains("data-isk-id=\"" + o.Id + "\"", svg);
            Assert.Contains("stroke-dasharray", svg); // centre lines
            var mono = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.DoesNotContain("#C0392B", mono);
            Assert.Contains("#C0392B", svg);
        }
    }
}
