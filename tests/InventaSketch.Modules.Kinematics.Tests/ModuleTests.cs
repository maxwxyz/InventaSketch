using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new KinematicsModule());

        public static RenderContext Ctx(SketchDocument? doc = null, Lang lang = Lang.En) => new(doc ?? SketchDocument.Create(), lang);

        /// <summary>All (library item, view) combinations; types without views yield one entry with a null view.</summary>
        public static IEnumerable<(LibraryItem Item, ObjectType Type, string? View)> ItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                if (t.Views.Count == 0) { yield return (item, t, null); continue; }
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        public static void AssertFinite(IEnumerable<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            AssertFinite(prims, what);
            var r = Prim.Bounds(prims)!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
        }

        /// <summary>Render via the type (doc needed for point objects).</summary>
        public static IReadOnlyList<Primitive> Render(Registry r, SketchObject o)
        {
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return r.TypeOf(o).Render(o, Ctx(doc));
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
                "kin.joint.revolute", "kin.joint.ground", "kin.joint.prismatic", "kin.joint.cam", "kin.joint.spherical", "kin.joint.gear",
                "kin.link.binary", "kin.link.ternary", "kin.link.crank", "kin.link.rocker", "kin.link.slider", "kin.link.piston",
                "kin.cam.follower", "kin.spring", "kin.damper", "kin.rope.pulley", "kin.gripper",
                "kin.mech.fourbar", "kin.mech.slidercrank", "kin.mech.scotchyoke", "kin.mech.toggle", "kin.mech.quickreturn",
                "kin.motion.path", "kin.motion.anglelimit", "kin.motion.dof",
            })
                Assert.Contains(id, ids);
            Assert.All(ids, id => Assert.StartsWith("kin.", id));
        }

        [Fact]
        public void RegistersTogetherWithTheEngineCatalog()
        {
            // The module must not collide with other modules' ids (same registry rules as the engine).
            var r = new Registry().Register(new KinematicsModule());
            Assert.Equal(4, r.Categories.Count);
            Assert.Equal(new[] { "kin.joints", "kin.links", "kin.mechanisms", "kin.motion" }, r.Categories.OrderBy(c => c.Order).Select(c => c.Id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasKeywords()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.True(t.Keywords.Count >= 4, t.Id + " needs EN and DE keywords");
                foreach (var v in t.Views)
                {
                    Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                    Assert.Contains(v.Icon, new[] { IconIds.Schematic, IconIds.Detailed });
                }
                foreach (var p in t.Parameters) Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "." + p.Key);
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void ContextActionsUseKnownIconIds()
        {
            var r = Fx.Reg();
            var known = typeof(IconIds).GetFields().Select(f => (string)f.GetValue(null)!).ToHashSet();
            foreach (var item in r.Library)
            {
                var o = r.Create(item, Vec2.Zero);
                foreach (var a in r.TypeOf(o).Actions(o, Lang.De))
                {
                    Assert.Contains(a.Icon, known);
                    Assert.False(string.IsNullOrEmpty(a.Label.De), a.Id);
                    Assert.NotNull(a.Apply(o));
                }
            }
        }

        [Theory]
        [InlineData("viergelenk")]
        [InlineData("four-bar")]
        [InlineData("schubkurbel")]
        [InlineData("gelenk")]
        [InlineData("kniehebel")]
        [InlineData("bahnkurve")]
        [InlineData("freiheitsgrad")]
        [InlineData("gripper")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Geometry == GeometryKind.Points
                    ? new SketchObject { Id = "x", Type = t.Id, Points = t.DefaultPoints.ToList() }
                    : new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                var prims = Fx.Render(r, o);
                Assert.NotEmpty(prims);
                Fx.AssertFinite(prims, t.Id);
            }
        }
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideItsBox()
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.ItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero);
                if (view is not null) o = o with { View = view };
                var prims = Fx.Render(r, o);
                if (o.IsPointGeometry)
                {
                    // point objects: everything near their points
                    Fx.AssertFinite(prims, item.Id);
                    var near = Rect.FromPoints(o.Points!).Inflate(6);
                    var b = Prim.Bounds(prims)!.Value;
                    Assert.True(near.Contains(b), $"{item.Id}/{view}: {b} not near points {near}");
                }
                else Fx.AssertInsideBox(prims, o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count >= 90, "expected many item/view combinations, got " + count);
        }

        [Theory]
        [InlineData(2.5, 0.6)]
        [InlineData(0.5, 1.8)]
        [InlineData(3, 3)]
        public void FreeNonUniformScalingKeepsBoxItemsInside(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.ItemViews(r))
            {
                if (type.Geometry == GeometryKind.Points) continue;
                var o = r.Create(item, Vec2.Zero);
                if (view is not null) o = o with { View = view };
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                Fx.AssertInsideBox(Fx.Render(r, o), o.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
            }
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var m = r.Create(r.Item("kin.fourbar")!, new Vec2(80, 80)).WithParam("crank", 10);
            doc = Edits.Add(doc, doc.Pages[0].Id, m);
            doc = Edits.Scale(doc, new[] { m.Id }, new Vec2(80, 80), 2, 0.5);
            var g = doc.Get(m.Id)!;
            Assert.Equal(10, g.Num("crank", 0));
            Assert.Equal(-60, g.Num("angle", 0));
            Assert.Equal(120, g.Size.W, 6);
            Assert.Equal(22, g.Size.H, 6);
        }

        [Fact]
        public void MechanismsStayInsideTheirBoxOverAFullTurnWithGhostsAndPaths()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "kin.mech.fourbar", "kin.mech.slidercrank", "kin.mech.scotchyoke", "kin.mech.toggle", "kin.mech.quickreturn", "kin.cam.follower" })
            {
                var t = r.Type(id)!;
                foreach (var view in new[] { "schematic", "detailed" })
                    for (var a = -180; a < 180; a += 15)
                    {
                        var o = t.Create(Vec2.Zero) with { View = view };
                        o = o.WithParam("angle", a).WithParam("ghosts", 3).WithParam("path", true).WithParam("dof", true);
                        Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{id}/{view}@{a}");
                    }
            }
        }

        [Fact]
        public void LinkEndsHaveJointCircles()
        {
            var r = Fx.Reg();
            var link = r.Create(r.Item("kin.link")!, new Vec2(50, 50));
            Assert.Equal(2, Fx.Render(r, link).Count(p => p.Tag == KinTags.Joint));
            var open = r.Create(r.Item("kin.link.open")!, new Vec2(50, 50));
            Assert.Equal(1, Fx.Render(r, open).Count(p => p.Tag == KinTags.Joint));
            var ternary = r.Create(r.Item("kin.ternary")!, new Vec2(50, 50));
            Assert.Equal(3, Fx.Render(r, ternary).Count(p => p.Tag == KinTags.Joint));
        }

        [Fact]
        public void InfeasiblePositionIsMarkedGracefully()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.fourbar")!;
            // Non-Grashof chain where the crank cannot turn fully: some angles cannot be assembled.
            var o = t.Create(Vec2.Zero).WithParam("ground", 40).WithParam("crank", 30).WithParam("coupler", 12).WithParam("rocker", 20).WithParam("angle", 180);
            var prims = t.Render(o, Fx.Ctx());
            Assert.Contains(prims, p => p.Tag == KinTags.NoAssembly && p is TextPrim tp && tp.Text == "no assembly possible");
            Assert.Contains(t.Render(o, Fx.Ctx(lang: Lang.De)), p => p is TextPrim tp && tp.Text == "keine Montage möglich");
            Fx.AssertInsideBox(prims, o.Size, "infeasible four-bar");
            // the input crank is still drawn
            Assert.Contains(prims, p => p.Tag == KinTags.Link);
            var toggle = r.Type("kin.mech.toggle")!;
            var bad = toggle.Create(Vec2.Zero).WithParam("link1", 30).WithParam("link2", 10).WithParam("angle", 60);
            Assert.Contains(toggle.Render(bad, Fx.Ctx()), p => p.Tag == KinTags.NoAssembly);
        }

        [Fact]
        public void DofBadgeShowsGruebler()
        {
            var r = Fx.Reg();
            var badge = r.Create(r.Item("kin.dof")!, Vec2.Zero);
            var t = r.TypeOf(badge);
            Assert.Contains(t.Render(badge, Fx.Ctx()), p => p is TextPrim tp && tp.Text == "DOF = 1");
            Assert.Contains(t.Render(badge, Fx.Ctx(lang: Lang.De)), p => p is TextPrim tp && tp.Text == "F = 1");
            var five = badge.WithParam("links", 5).WithParam("lower", 5);
            Assert.Contains(t.Render(five, Fx.Ctx()), p => p is TextPrim tp && tp.Text == "DOF = 2");
            Assert.Equal(1, Linkages.Grubler(6, 7));
            Assert.Equal(0, Linkages.Grubler(3, 3));
            foreach (var id in new[] { "kin.mech.fourbar", "kin.mech.slidercrank", "kin.mech.scotchyoke", "kin.mech.toggle", "kin.mech.quickreturn" })
            {
                var m = r.Type(id)!;
                var o = m.Create(Vec2.Zero).WithParam("dof", true);
                Assert.Contains(m.Render(o, Fx.Ctx()), p => p is TextPrim tp && tp.Tag == KinTags.Dof && tp.Text == "DOF = 1");
            }
        }

        [Fact]
        public void AngleLimitDrawsArcTicksAndLabels()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("kin.anglelimit")!, Vec2.Zero);
            var prims = Fx.Render(r, o);
            Assert.Single(prims, p => p.Tag == KinTags.Arc);
            Assert.Equal(2, prims.Count(p => p.Tag == "tick"));
            var labels = prims.OfType<TextPrim>().Select(p => p.Text).ToList();
            Assert.Equal(new[] { "−30°", "45°" }, labels);
            var sym = Fx.Render(r, o.WithParam("labels", "symbols")).OfType<TextPrim>().Select(p => p.Text).ToList();
            Assert.Equal(new[] { "φmin", "φmax" }, sym);
            Assert.Empty(Fx.Render(r, o.WithParam("labels", "none")).OfType<TextPrim>());
        }

        [Fact]
        public void CamFollowerTouchesTheCam()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.cam.follower")!;
            foreach (var f in new[] { "roller", "flat", "knife" })
                for (var a = 0; a < 360; a += 30)
                {
                    var o = t.Create(Vec2.Zero).WithParam("follower", f).WithParam("angle", a);
                    var prims = t.Render(o, Fx.Ctx());
                    var cam = ((PathPrim)prims.First(p => p.Tag == "cam")).Segments.Where(s => s.Op != SegOp.Z).Select(s => s.P).ToList();
                    var follower = prims.First(p => p.Tag == "follower");
                    var fb = Prim.Bounds(new[] { follower })!.Value;
                    switch (f)
                    {
                        case "flat":
                            Assert.True(Math.Abs(fb.Bottom - cam.Min(q => q.Y)) < 1e-6, $"flat@{a}: face on the highest cam point");
                            break;
                        case "knife":
                            {
                                var top = double.MaxValue;
                                for (var i = 0; i < cam.Count; i++)
                                {
                                    var p0 = cam[i]; var p1 = cam[(i + 1) % cam.Count];
                                    if ((p0.X <= 0) == (p1.X <= 0)) continue;
                                    top = Math.Min(top, p0.Y + (0 - p0.X) * (p1.Y - p0.Y) / (p1.X - p0.X));
                                }
                                Assert.True(Math.Abs(fb.Bottom - top) < 1e-6, $"knife@{a}: tip on the contour");
                                break;
                            }
                        default:
                            {
                                var roller = (EllipsePrim)follower;
                                var dist = Enumerable.Range(0, cam.Count).Min(i => Geo.ClosestOnSegment(roller.Center, cam[i], cam[(i + 1) % cam.Count]).Point.DistanceTo(roller.Center));
                                Assert.True(Math.Abs(dist - roller.Rx) < 0.05, $"roller@{a}: distance {dist} vs radius {roller.Rx}");
                                Assert.False(Geo.PointInPolygon(roller.Center, cam), $"roller@{a}: centre inside the cam");
                                Assert.True(roller.Center.Y < cam.Min(q => q.Y) + roller.Rx, $"roller@{a}: roller rests on top");
                                break;
                            }
                    }
                }
        }
    }

    public class LoopClosureTests
    {
        [Theory]
        [InlineData(40, 12, 36, 30, false)]
        [InlineData(40, 12, 36, 30, true)]
        [InlineData(50, 15, 45, 35, false)]
        [InlineData(30, 10, 25, 28, false)]
        public void FourBarPreservesLinkLengthsOverASweep(double d, double a, double b, double c, bool crossed)
        {
            Assert.True(Linkages.IsGrashof(d, a, b, c));
            var n = 0;
            for (var deg = -180.0; deg <= 180; deg += 2.5)
            {
                var p = Linkages.FourBar(d, a, b, c, deg, crossed);
                Assert.True(p.HasValue, $"crank-rocker must assemble at {deg}°");
                var q = p!.Value;
                Assert.Equal(d, q.O2.DistanceTo(q.O4), 6);
                Assert.Equal(a, q.O2.DistanceTo(q.A), 6);
                Assert.Equal(b, q.A.DistanceTo(q.B), 6);
                Assert.Equal(c, q.O4.DistanceTo(q.B), 6);
                Assert.True(Math.Abs(q.A.DistanceTo(q.B) - b) < 1e-6 && Math.Abs(q.O4.DistanceTo(q.B) - c) < 1e-6);
                // assembly mode is kept over the sweep (sign of the triangle A, O4, B)
                var side = Math.Sign((q.O4 - q.A).Cross(q.B - q.A));
                Assert.Equal(crossed ? 1 : -1, side);
                n++;
            }
            Assert.Equal(145, n);
        }

        [Theory]
        [InlineData(12, 36, 0)]
        [InlineData(12, 36, 6)]
        [InlineData(20, 50, -8)]
        public void SliderCrankPreservesLinkLengthsOverASweep(double r, double l, double e)
        {
            for (var deg = -180.0; deg <= 180; deg += 2.5)
            {
                var p = Linkages.SliderCrank(r, l, e, deg);
                Assert.True(p.HasValue);
                var q = p!.Value;
                Assert.True(Math.Abs(q.O.DistanceTo(q.A) - r) < 1e-6);
                Assert.True(Math.Abs(q.A.DistanceTo(q.B) - l) < 1e-6);
                Assert.True(Math.Abs(q.B.Y - e) < 1e-9);
            }
        }

        [Fact]
        public void NoAssemblyIsReportedAsNull()
        {
            // coupler + rocker shorter than the distance between A and O4
            Assert.Null(Linkages.FourBar(40, 30, 12, 20, 180));
            Assert.Null(Linkages.SliderCrank(20, 10, 0, 90));
            Assert.Null(Linkages.CircleCircle(Vec2.Zero, 1, new Vec2(5, 0), 1, 1));
        }

        /// <summary>The rendered mechanism keeps the link length ratios (uniform model-to-box scale, even when stretched).</summary>
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2.2, 0.7)]
        public void RenderedFourBarKeepsLinkRatiosAtEveryAngle(double sx, double sy)
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.fourbar")!;
            for (var a = -180; a < 180; a += 10)
            {
                var o = t.Create(Vec2.Zero).WithParam("angle", a);
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                var anchors = t.Anchors(o, Fx.Ctx()).Where(x => x.Role == AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
                var crank = anchors["O2"].DistanceTo(anchors["A"]);
                Assert.True(Math.Abs(anchors["A"].DistanceTo(anchors["B"]) / crank - 36.0 / 12) < 1e-6, $"coupler at {a}");
                Assert.True(Math.Abs(anchors["O4"].DistanceTo(anchors["B"]) / crank - 30.0 / 12) < 1e-6, $"rocker at {a}");
                Assert.True(Math.Abs(anchors["O2"].DistanceTo(anchors["O4"]) / crank - 40.0 / 12) < 1e-6, $"frame at {a}");
            }
        }

        [Fact]
        public void RenderedSliderCrankKeepsRodLengthAndGuide()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.slidercrank")!;
            for (var a = -180; a < 180; a += 10)
            {
                var o = t.Create(Vec2.Zero).WithParam("angle", a).WithParam("offset", 5);
                var anchors = t.Anchors(o, Fx.Ctx()).ToList();
                var ports = anchors.Where(x => x.Role == AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
                var crank = ports["O"].DistanceTo(ports["A"]);
                Assert.True(Math.Abs(ports["A"].DistanceTo(ports["B"]) / crank - 3) < 1e-6);
                var axis = anchors.Single(x => x.Role == AnchorRole.Axis);
                Assert.True(Math.Abs(axis.P.Y - ports["B"].Y) < 1e-6 && Math.Abs(axis.Q.Y - ports["B"].Y) < 1e-6, "slider on its guide axis");
            }
        }

        [Fact]
        public void ToggleAndQuickReturnCloseTheirLoops()
        {
            var r = Fx.Reg();
            var toggle = r.Type("kin.mech.toggle")!;
            for (var a = -60; a <= 60; a += 5)
            {
                var o = toggle.Create(Vec2.Zero).WithParam("angle", a);
                var p = toggle.Anchors(o, Fx.Ctx()).Where(x => x.Role == AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
                Assert.True(Math.Abs(p["A"].DistanceTo(p["B"]) - p["B"].DistanceTo(p["C"])) < 1e-6, "equal toggle links");
                Assert.True(Math.Abs(p["C"].X - p["A"].X) < 1e-6, "slider on the axis through the pivot");
            }
            var qr = r.Type("kin.mech.quickreturn")!;
            for (var a = -180; a < 180; a += 10)
            {
                var o = qr.Create(Vec2.Zero).WithParam("angle", a);
                var p = qr.Anchors(o, Fx.Ctx()).Where(x => x.Role == AnchorRole.Port).ToDictionary(x => x.Id!, x => x.P);
                var k = p["O2"].DistanceTo(p["O4"]) / 20; // model-to-box scale
                Assert.True(Math.Abs(p["O2"].DistanceTo(p["A"]) / k - 10) < 1e-6);
                Assert.True(Math.Abs(p["O4"].DistanceTo(p["T"]) / k - 40) < 1e-6);
                Assert.True(Math.Abs(p["T"].DistanceTo(p["B"]) / k - 16) < 1e-6);
                // crank pin A lies on the lever line O4-T
                Assert.True(Math.Abs((p["T"] - p["O4"]).Normalized().Cross(p["A"] - p["O4"])) < 1e-6);
            }
        }
    }

    public class MotionTests
    {
        [Theory]
        [InlineData("kin.mech.fourbar", 0)]
        [InlineData("kin.mech.fourbar", 1)]
        [InlineData("kin.mech.fourbar", 5)]
        [InlineData("kin.mech.slidercrank", 3)]
        [InlineData("kin.mech.scotchyoke", 6)]
        [InlineData("kin.mech.toggle", 2)]
        [InlineData("kin.mech.quickreturn", 4)]
        public void GhostCountMatchesParameter(string id, int n)
        {
            var r = Fx.Reg();
            var t = r.Type(id)!;
            var o = t.Create(Vec2.Zero).WithParam("ghosts", n);
            var prims = t.Render(o, Fx.Ctx());
            var ghosts = prims.Where(p => p.Tag == KinTags.Ghost).ToList();
            Assert.Equal(n, ghosts.Count);
            foreach (var g in ghosts.Cast<GroupPrim>())
                Assert.All(Prim.Flatten(g.Children).Where(c => c is LinePrim), c => Assert.Equal(LineRole.Phantom, ((LinePrim)c).Style.Role));
        }

        [Fact]
        public void GhostToggleAndAngleStepActions()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.fourbar")!;
            var o = t.Create(Vec2.Zero);
            ContextAction A(SketchObject x, string id) => t.Actions(x, Lang.En).Single(a => a.Id == id);
            var on = A(o, "kin.ghosts").Apply(o);
            Assert.Equal(4, on.Int("ghosts", 0));
            Assert.True(A(on, "kin.ghosts").IsActive);
            var off = A(on, "kin.ghosts").Apply(on);
            Assert.Equal(0, off.Int("ghosts", -1));
            Assert.Equal(-45, A(o, "kin.angle.next").Apply(o).Num("angle", 0), 9);
            Assert.Equal(-75, A(o, "kin.angle.prev").Apply(o).Num("angle", 0), 9);
            var wrap = o.WithParam("angle", 175);
            Assert.Equal(-170, A(wrap, "kin.angle.next").Apply(wrap).Num("angle", 0), 9);
            var path = A(o, "kin.path").Apply(o);
            Assert.True(path.Flag("path", false));
            Assert.Contains(t.Render(path, Fx.Ctx()), p => p.Tag == KinTags.Path);
            Assert.DoesNotContain(t.Render(o, Fx.Ctx()), p => p.Tag == KinTags.Path);
            var crossed = A(o, "kin.branch").Apply(o);
            Assert.Equal("crossed", crossed.Str("branch", ""));
        }

        [Fact]
        public void CouplerCurveIsClosedForACrankRocker()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.fourbar")!;
            var o = r.Create(r.Item("kin.fourbar.coupler")!, Vec2.Zero);
            var paths = t.Render(o, Fx.Ctx()).Where(p => p.Tag == KinTags.Path).Cast<PathPrim>().ToList();
            var path = Assert.Single(paths);
            Assert.True(path.Segments.Count > 60);
            Assert.True(path.Segments[0].P.DistanceTo(path.Segments[path.Segments.Count - 1].P) < 1e-6, "full turn returns to the start");
        }

        [Fact]
        public void MotionPathIsDashedWithArrow()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("kin.path")!, new Vec2(50, 50));
            var p = (PathPrim)Assert.Single(Fx.Render(r, o));
            Assert.Equal(DashStyle.Dash, p.Style.Dash);
            Assert.Equal(ArrowHead.Triangle, p.Style.ArrowEnd);
            Assert.Contains(p.Segments, s => s.Op == SegOp.C);
            var closed = r.Create(r.Item("kin.path.closed")!, new Vec2(50, 50));
            var pc = (PathPrim)Assert.Single(Fx.Render(r, closed));
            Assert.True(pc.IsClosed);
            Assert.Null(pc.Style.ArrowEnd);
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
        public void LinksSnapJointToJoint()
        {
            var r0 = Fx.Reg();
            var a = r0.Create(r0.Item("kin.link")!, new Vec2(100, 100)); // ends (85,100) and (115,100)
            var b = r0.Create(r0.Item("kin.link")!, new Vec2(131.2, 98.4)); // start near (116.2, 98.4)
            var (r, doc) = Doc(a, b);
            var ctx = new RenderContext(doc);
            var anchors = r.PageAnchors(a, ctx).ToList();
            Assert.Equal(2, anchors.Count(x => x.Role == AnchorRole.Port));
            var moving = r.PageAnchors(b, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { b.Id }, ctx, Opt);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("port", res.Kind);
            Assert.Equal(-1.2, res.Delta.X, 6);
            Assert.Equal(1.6, res.Delta.Y, 6);
        }

        [Fact]
        public void RevoluteJointSnapsOntoLinkEnd()
        {
            var r0 = Fx.Reg();
            var link = r0.Create(r0.Item("kin.link")!, new Vec2(100, 100));
            var joint = r0.Create(r0.Item("kin.revolute")!, new Vec2(86.5, 101));
            var (r, doc) = Doc(link, joint);
            var ctx = new RenderContext(doc);
            var res = Snapper.SnapMove(r.PageAnchors(joint, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { joint.Id }, ctx, Opt), Opt);
            Assert.Equal("port", res.Kind);
            Assert.Equal(new Vec2(85, 100), new Vec2(86.5, 101) + res.Delta);
        }

        [Fact]
        public void LinkEndpointSnapsToMechanismJoint()
        {
            var r0 = Fx.Reg();
            var mech = r0.Create(r0.Item("kin.fourbar")!, new Vec2(100, 100));
            var (r, doc) = Doc(mech);
            var ctx = new RenderContext(doc);
            var b = r.PageAnchors(mech, ctx).Single(x => x.Role == AnchorRole.Port && x.Id == "B").P;
            var targets = Snapper.Targets(r, doc, doc.Pages[0], Array.Empty<string>(), ctx, Opt);
            var res = Snapper.SnapPoint(b + new Vec2(1, -1), targets, Opt);
            Assert.Equal(b, b + new Vec2(1, -1) + res.Delta);
            var mounts = r.PageAnchors(mech, ctx).Where(x => x.Role == AnchorRole.Mount).ToList();
            Assert.Equal(2, mounts.Count);
            Assert.Equal(mounts[0].P.Y, mounts[1].P.Y, 6);
        }

        [Fact]
        public void SliderBlockSnapsOntoGuideAxis()
        {
            var r0 = Fx.Reg();
            var guide = r0.Create(r0.Item("kin.prismatic")!, new Vec2(100, 50));
            var slider = r0.Create(r0.Item("kin.slider")!, new Vec2(92, 51.8));
            var (r, doc) = Doc(guide, slider);
            var ctx = new RenderContext(doc);
            Assert.Contains(r.PageAnchors(guide, ctx), a => a.Role == AnchorRole.Axis);
            var res = Snapper.SnapMove(r.PageAnchors(slider, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { slider.Id }, ctx, Opt), Opt);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-1.8, res.Delta.Y, 6);
            Assert.Equal(0, res.Delta.X, 6);
        }

        [Fact]
        public void GroundPivotExposesPortAndMount()
        {
            var r = Fx.Reg();
            var g = r.Create(r.Item("kin.ground")!, new Vec2(20, 20));
            var anchors = r.PageAnchors(g, Fx.Ctx()).ToList();
            var port = Assert.Single(anchors, a => a.Role == AnchorRole.Port);
            var mount = Assert.Single(anchors, a => a.Role == AnchorRole.Mount);
            Assert.True(mount.P.Y > port.P.Y);
        }
    }

    public class PersistenceTests
    {
        internal static (Registry R, SketchDocument Doc) Sample()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Kinematics sample");
            SketchObject At(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = r.Create(r.Item(item)!, new Vec2(x, y));
                return f is null ? o : f(o);
            }
            var objs = new List<SketchObject>
            {
                At("kin.fourbar", 60, 50, o => o.WithParam("ghosts", 3).WithParam("dof", true) with { Size = new Size2(90, 66), Ref = "10" }),
                At("kin.slidercrank", 180, 50, o => o.WithParam("path", true) with { Size = new Size2(100, 48) }),
                At("kin.toggle.ghosts", 280, 60),
                At("kin.quickreturn", 60, 145, o => o with { Size = new Size2(70, 75) }),
                At("kin.cam.roller", 140, 145),
                At("kin.link", 210, 120),
                At("kin.ternary", 260, 140),
                At("kin.spring", 210, 160),
                At("kin.path", 280, 160),
                At("kin.anglelimit", 310, 120),
            };
            doc = Edits.Add(doc, doc.Pages[0].Id, objs.ToArray());
            doc = Edits.SetReference(doc, "10", "four-bar linkage");
            return (r, doc);
        }

        [Fact]
        public void JsonRoundTrip()
        {
            var (r, doc) = Sample();
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var fb = back.Objects.Values.Single(o => o.Type == "kin.mech.fourbar");
            Assert.Equal(3, fb.Int("ghosts", 0));
            Assert.Equal(-60, fb.Num("angle", 0));
            Assert.Equal("schematic", fb.View);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
            // rendering is identical after the round trip
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
            Assert.Equal(svg, SvgRenderer.RenderPage(r, back, back.Pages[0]));
            foreach (var o in doc.Objects.Values) Assert.Contains("data-isk-id=\"" + o.Id + "\"", svg);
        }
    }
}
