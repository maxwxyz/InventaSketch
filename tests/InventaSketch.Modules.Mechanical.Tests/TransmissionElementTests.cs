using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Screw drives, freewheel, torque converter, park lock, CVT, strain wave and cycloidal drives, gearbox housing and
    /// oil sump: quick sketch without parameters, anchors, coupling (nut on the spindle), parameters and persistence.
    /// </summary>
    public class TransmissionElementTests
    {
        public static readonly string[] NewItems =
        {
            "mech.ballscrew.stick", "mech.ballscrew.side", "mech.ballscrew.profile", "mech.ballnut.stick", "mech.ballnut.side", "mech.ballnut.section",
            "mech.leadscrew.stick", "mech.leadscrew.side", "mech.leadscrew.profile", "mech.leadnut.stick", "mech.leadnut.side", "mech.leadnut.section",
            "mech.freewheel", "mech.freewheel.section", "mech.freewheel.axial", "mech.converter", "mech.converter.lockup", "mech.converter.section",
            "mech.parklock", "mech.parklock.axial", "mech.cvt.stick", "mech.cvt.axial", "mech.cvt.chain", "mech.harmonic.stick", "mech.harmonic.axial",
            "mech.cycloidal.stick", "mech.cycloidal.axial", "mech.housing", "mech.housing.stick", "mech.sump", "mech.sump.stick",
        };

        public static IEnumerable<object[]> Items() => NewItems.Select(i => new object[] { i });

        /// <summary>Items whose element sits on a shaft with its hub at the box centre.</summary>
        public static IEnumerable<object[]> CentreHubItems() => new[]
        {
            "mech.ballnut.stick", "mech.ballnut.side", "mech.ballnut.section", "mech.leadnut.stick", "mech.freewheel", "mech.freewheel.section", "mech.freewheel.axial",
            "mech.converter", "mech.converter.section", "mech.parklock", "mech.parklock.axial", "mech.harmonic.stick", "mech.harmonic.axial",
            "mech.cycloidal.stick", "mech.cycloidal.axial",
        }.Select(i => new object[] { i });

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static List<Anchor> Anchors(Registry r, SketchObject o) => r.PageAnchors(o, new RenderContext(Doc(o))).ToList();

        [Fact]
        public void NewTypesAreRegisteredInTheirCategories()
        {
            var r = Fx.Reg();
            var expected = new Dictionary<string, string>
            {
                ["mech.screwdrive.screw"] = "mech.screwdrives",
                ["mech.screwdrive.nut"] = "mech.screwdrives",
                ["mech.clutch.freewheel"] = "mech.couplings",
                ["mech.converter.torque"] = "mech.couplings",
                ["mech.parklock"] = "mech.couplings",
                ["mech.cvt"] = "mech.drives",
                ["mech.gearset.harmonic"] = "mech.gears",
                ["mech.gearset.cycloidal"] = "mech.gears",
                ["mech.housing.gearbox"] = "mech.housings",
                ["mech.housing.sump"] = "mech.housings",
            };
            foreach (var kv in expected) Assert.Equal(kv.Value, r.Type(kv.Key)!.Category);
            foreach (var id in NewItems) Assert.NotNull(r.Item(id));
        }

        [Theory]
        [MemberData(nameof(Items))]
        public void RendersWithoutParametersWithFiniteCoordinates(string itemId)
        {
            var r = Fx.Reg();
            var item = r.Item(itemId)!;
            var t = r.Type(item.TypeId)!;
            // Bare object as a quick sketch: no parameters at all, only the view and its preset size.
            var bare = new SketchObject { Id = "x", Type = t.Id, View = item.View, Size = item.Size ?? t.DefaultSize };
            Fx.AssertInsideBox(t.Render(bare, Fx.Ctx()), bare.Size, itemId + " (bare)");
            var inserted = r.Create(item, new Vec2(40, 30));
            Fx.AssertInsideBox(t.Render(inserted, Fx.Ctx()), inserted.Size, itemId);
            foreach (var a in Anchors(r, inserted))
                Assert.False(double.IsNaN(a.P.X + a.P.Y + a.Q.X + a.Q.Y + a.Radius) || double.IsInfinity(a.P.X + a.P.Y + a.Q.X + a.Q.Y + a.Radius), itemId + ": anchor");
        }

        [Theory]
        [MemberData(nameof(Items))]
        public void HasBilingualNamesAndKeywords(string itemId)
        {
            var r = Fx.Reg();
            var item = r.Item(itemId)!;
            var t = r.Type(item.TypeId)!;
            Assert.False(string.IsNullOrWhiteSpace(item.Name.En));
            Assert.False(string.IsNullOrWhiteSpace(item.Name.De));
            Assert.NotEqual(item.Name.En, item.Name.De);
            Assert.False(string.IsNullOrWhiteSpace(t.Name.De));
            Assert.NotEmpty(t.Keywords);
            foreach (var p in t.Parameters) Assert.False(string.IsNullOrWhiteSpace(p.Label.De), t.Id + "/" + p.Key);
            foreach (var a in t.Actions(r.Create(item, Vec2.Zero), Lang.De)) Assert.False(string.IsNullOrWhiteSpace(a.Label.De), t.Id + "/" + a.Id);
        }

        [Theory]
        [MemberData(nameof(Items))]
        public void ScalingChangesSizeNotParameters(string itemId)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(itemId)!, new Vec2(50, 50));
            var doc = Doc(o);
            var scaled = Edits.Scale(doc, new[] { o.Id }, new Vec2(50, 50), 1.7, 0.6).Get(o.Id)!;
            Assert.Equal(o.Size.W * 1.7, scaled.Size.W, 6);
            Assert.Equal(o.Size.H * 0.6, scaled.Size.H, 6);
            Assert.Equal(o.Params.Count, scaled.Params.Count);
            foreach (var kv in o.Params) Assert.Equal(kv.Value, scaled.Params[kv.Key]);
            Assert.Equal(o.View, scaled.View);
        }

        [Theory]
        [MemberData(nameof(CentreHubItems))]
        public void HubLiesOnTheAxisAtTheBoxCentre(string itemId)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(itemId)!, new Vec2(70, 40));
            Assert.Contains(Anchors(r, o), a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub && a.Id == "hub" && a.P == new Vec2(70, 40));
        }

        [Theory]
        [InlineData("mech.ballnut.stick")]
        [InlineData("mech.leadnut.section")]
        [InlineData("mech.freewheel")]
        [InlineData("mech.converter")]
        [InlineData("mech.parklock")]
        [InlineData("mech.harmonic.stick")]
        [InlineData("mech.cycloidal.stick")]
        public void HubSnapsOntoShaftAxis(string itemId)
        {
            var r = Fx.Reg();
            var shaft = r.Create(r.Item("mech.shaft.line")!, new Vec2(100, 50));
            var part = r.Create(r.Item(itemId)!, new Vec2(90, 52.2));
            var doc = Doc(shaft, part);
            var ctx = new RenderContext(doc);
            var opt = new SnapOptions { Tolerance = 3, Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(part, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { part.Id }, ctx, opt), opt);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-2.2, res.Delta.Y, 6);
        }

        [Theory]
        [InlineData("mech.ballnut.stick", "mount")]
        [InlineData("mech.ballnut.side", "mount")]
        [InlineData("mech.leadnut.section", "mount")]
        [InlineData("mech.converter", "ground")]
        [InlineData("mech.parklock", "pivot")]
        [InlineData("mech.parklock.axial", "pivot")]
        [InlineData("mech.harmonic.stick", "ground")]
        [InlineData("mech.harmonic.axial", "ground")]
        [InlineData("mech.cycloidal.stick", "ground")]
        [InlineData("mech.cycloidal.axial", "ground")]
        [InlineData("mech.housing", "base")]
        [InlineData("mech.sump", "mount")]
        public void OffersMountPoints(string itemId, string mountId)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(itemId)!, new Vec2(0, 0));
            var m = Assert.Single(Anchors(r, o), a => a.Role == AnchorRole.Mount && a.Kind == AnchorKind.Point && a.Id == mountId);
            Assert.True(Math.Abs(m.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(m.P.Y) <= o.Size.H / 2 + 1e-9, itemId + ": mount inside the box");
        }

        // ------------------------------------------------------------------ screw drive

        [Theory]
        [InlineData("mech.ballscrew.stick")]
        [InlineData("mech.ballscrew.side")]
        [InlineData("mech.leadscrew.profile")]
        public void SpindleExposesAxisOverItsLengthAndBothEnds(string itemId)
        {
            var r = Fx.Reg();
            var screw = r.Create(r.Item(itemId)!, new Vec2(100, 50));
            var anchors = Anchors(r, screw);
            var axis = Assert.Single(anchors, a => a.Role == AnchorRole.Axis);
            Assert.Equal(new Vec2(100 - screw.Size.W / 2, 50), axis.P);
            Assert.Equal(new Vec2(100 + screw.Size.W / 2, 50), axis.Q);
            Assert.Equal(2, anchors.Count(a => a.Role == AnchorRole.End));
            Assert.DoesNotContain(anchors, a => a.Role == AnchorRole.Hub);
        }

        private static (Registry R, SketchDocument Doc, SketchObject Screw, SketchObject Nut) ScrewWithNut(string screwItem = "mech.ballscrew.side", string nutItem = "mech.ballnut.side")
        {
            var r = Fx.Reg();
            var screw = r.Create(r.Item(screwItem)!, new Vec2(100, 50));
            var nut = r.Create(r.Item(nutItem)!, new Vec2(85, 50));
            return (r, Doc(screw, nut), screw, nut);
        }

        [Fact]
        public void NutSnapsOntoTheSpindleAxis()
        {
            var r = Fx.Reg();
            var screw = r.Create(r.Item("mech.leadscrew.stick")!, new Vec2(100, 50));
            var nut = r.Create(r.Item("mech.leadnut.stick")!, new Vec2(80, 51.5));
            var doc = Doc(screw, nut);
            var ctx = new RenderContext(doc);
            var opt = new SnapOptions { Tolerance = 3, Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(nut, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { nut.Id }, ctx, opt), opt);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-1.5, res.Delta.Y, 6);
            Assert.Equal(0, res.Delta.X, 6);
        }

        [Fact]
        public void SpindleCarriesItsNut()
        {
            var (r, doc, screw, nut) = ScrewWithNut();
            Assert.Contains(Links.For(r, doc, new[] { nut.Id }), l => l.Kind == LinkKind.Carried && l.A == screw.Id && l.B == nut.Id);
            Assert.Contains(nut.Id, Coupling.RigidFollowers(r, doc, new[] { screw.Id }));
        }

        [Fact]
        public void NutFollowsTheSpindleWhenTheSpindleMoves()
        {
            var (r, doc, screw, nut) = ScrewWithNut();
            var after = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { screw.Id }, new Vec2(12, 25)));
            var n = after.Get(nut.Id)!;
            Assert.Equal(97, n.Transform.X, 6);
            Assert.Equal(75, n.Transform.Y, 6);
            Assert.Equal(nut.Size, n.Size);
        }

        [Fact]
        public void NutFollowsTheSpindleWhenItTurns()
        {
            var (r, doc, screw, nut) = ScrewWithNut();
            var after = Coupling.Propagate(r, doc, Edits.Rotate(doc, new[] { screw.Id }, new Vec2(100, 50), 90));
            var n = after.Get(nut.Id)!;
            // The nut 15 mm left of the spindle centre is now 15 mm above it, still on the (turned) axis.
            Assert.Equal(100, n.Transform.X, 6);
            Assert.Equal(35, n.Transform.Y, 6);
            Assert.Equal(90, n.Transform.Rotation, 6);
            var axis = r.PageAnchors(after.Get(screw.Id)!, new RenderContext(after)).Single(a => a.Role == AnchorRole.Axis);
            var hub = r.PageAnchors(n, new RenderContext(after)).Single(a => a.Role == AnchorRole.Hub);
            Assert.True(Geo.ClosestOnSegment(hub.P, axis.P, axis.Q).Point.DistanceTo(hub.P) < 1e-6);
        }

        [Fact]
        public void NutSlidesAlongTheSpindleWithoutMovingIt()
        {
            var (r, doc, screw, nut) = ScrewWithNut();
            var after = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { nut.Id }, new Vec2(30, 0)));
            Assert.Equal(screw.Transform, after.Get(screw.Id)!.Transform);
            Assert.Equal(115, after.Get(nut.Id)!.Transform.X, 6);
            // Still docked: the spindle carries the nut at its new place.
            Assert.Contains(Links.For(r, after, new[] { nut.Id }), l => l.Kind == LinkKind.Carried && l.A == screw.Id);
        }

        [Fact]
        public void NutKindShowsBallsOrThreadAndFlangeIsOptional()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.screwdrive.nut")!;
            var ball = r.Create(r.Item("mech.ballnut.section")!, Vec2.Zero);
            var lead = r.Create(r.Item("mech.leadnut.section")!, Vec2.Zero);
            Assert.Equal(10, t.Render(ball, Fx.Ctx()).Count(p => p.Tag == "ball"));
            Assert.DoesNotContain(t.Render(lead, Fx.Ctx()), p => p.Tag == "ball");
            var stick = r.Create(r.Item("mech.leadnut.stick")!, Vec2.Zero);
            Assert.Equal(6, t.Render(stick, Fx.Ctx()).Count(p => p.Tag == "thread"));
            var side = r.Create(r.Item("mech.ballnut.side")!, Vec2.Zero);
            Assert.Contains(t.Render(side, Fx.Ctx()), p => p.Tag == "flange");
            var noFlange = t.Actions(side, Lang.En).Single(a => a.Id == "param.flange").Apply(side);
            Assert.DoesNotContain(t.Render(noFlange, Fx.Ctx()), p => p.Tag == "flange");
        }

        [Fact]
        public void LeadParameterSetsTheThreadPitchAndHandFlipsTheStrokes()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.screwdrive.screw")!;
            var o = r.Create(r.Item("mech.leadscrew.stick")!, Vec2.Zero) with { Size = new Size2(100, 10) };
            var fine = t.Render(o.WithParam("pitch", 2.0), Fx.Ctx()).Count(p => p.Tag == "thread");
            var coarse = t.Render(o.WithParam("pitch", 8.0), Fx.Ctx()).Count(p => p.Tag == "thread");
            Assert.True(fine > 3 * coarse, $"fine {fine}, coarse {coarse}");
            var right = (LinePrim)t.Render(o, Fx.Ctx()).First(p => p.Tag == "thread");
            var left = (LinePrim)t.Render(o.WithParam("lefthand", true), Fx.Ctx()).First(p => p.Tag == "thread");
            Assert.True((right.B.X - right.A.X) * (left.B.X - left.A.X) < 0, "left-hand strokes lean the other way");
        }

        // ------------------------------------------------------------------ transmission elements

        [Fact]
        public void CvtSitsOnTwoShaftsAndTheRatioMovesTheBelt()
        {
            var r = Fx.Reg();
            var cvt = r.Create(r.Item("mech.cvt.stick")!, new Vec2(100, 100));
            var hubs = Anchors(r, cvt).Where(a => a.Role == AnchorRole.Hub).ToList();
            Assert.Equal(new[] { "hub", "hub2" }, hubs.Select(h => h.Id));
            Assert.Equal(100, hubs[0].P.X, 6);
            Assert.True(hubs[0].P.Y < 100 && hubs[1].P.Y > 100);
            Assert.Contains(Anchors(r, r.Create(r.Item("mech.cvt.axial")!, Vec2.Zero)), a => a.Role == AnchorRole.Hub && a.Id == "hub2");

            var (p1, s1) = Transmissions.CvtRadii(cvt);
            Assert.Equal(p1, s1, 6);
            var t = r.Type("mech.cvt")!;
            var slower = t.Actions(cvt, Lang.En).Single(a => a.Id == "param.ratio.inc").Apply(cvt);
            var (p2, s2) = Transmissions.CvtRadii(slower);
            Assert.True(p2 < p1 && s2 > s1, "a larger ratio puts the belt lower on the primary, higher on the secondary");
            Assert.Equal(p1 + s1, p2 + s2, 6);
            Assert.Equal(1.2, slower.Num("ratio", 0), 6);

            // Both shafts carry the variator: moving the input shaft takes it (and the output shaft) along.
            var input = r.Create(r.Item("mech.shaft.line")!, hubs[0].P);
            var output = r.Create(r.Item("mech.shaft.line")!, hubs[1].P);
            var doc = Doc(input, output, cvt);
            var after = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { input.Id }, new Vec2(0, -10)));
            Assert.Equal(90, after.Get(cvt.Id)!.Transform.Y, 6);
        }

        [Fact]
        public void ChainVariatorDrawsLinkPins()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.cvt")!;
            Assert.Contains(t.Render(r.Create(r.Item("mech.cvt.chain")!, Vec2.Zero), Fx.Ctx()), p => p.Tag == "pin");
            Assert.DoesNotContain(t.Render(r.Create(r.Item("mech.cvt.axial")!, Vec2.Zero), Fx.Ctx()), p => p.Tag == "pin");
        }

        [Fact]
        public void TorqueConverterLockUpClutchIsOptional()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.converter.torque")!;
            var plain = r.Create(r.Item("mech.converter")!, Vec2.Zero);
            var lockup = r.Create(r.Item("mech.converter.lockup")!, Vec2.Zero);
            Assert.DoesNotContain(t.Render(plain, Fx.Ctx()), p => p.Tag == "lockup");
            Assert.Contains(t.Render(lockup, Fx.Ctx()), p => p.Tag == "lockup");
            foreach (var part in new[] { "pump", "turbine", "stator" }) Assert.Contains(t.Render(plain, Fx.Ctx()), p => p.Tag == part);
            var toggled = t.Actions(plain, Lang.En).Single(a => a.Id == "param.lockup").Apply(plain);
            Assert.True(toggled.Flag("lockup", false));
        }

        [Fact]
        public void FreewheelReverseMirrorsTheLockingDirection()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.clutch.freewheel")!;
            var o = r.Create(r.Item("mech.freewheel")!, Vec2.Zero);
            var rollers = t.Render(o, Fx.Ctx()).OfType<EllipsePrim>().Where(e => e.Tag == "roller").ToList();
            var reversed = t.Actions(o, Lang.En).Single(a => a.Id == "param.reverse").Apply(o);
            var mirrored = t.Render(reversed, Fx.Ctx()).OfType<EllipsePrim>().Where(e => e.Tag == "roller").ToList();
            Assert.Equal(2, rollers.Count);
            Assert.Equal(-rollers[0].Center.X, mirrored[0].Center.X, 6);
        }

        [Fact]
        public void ParkLockPawlEngagesAndReleases()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.parklock")!;
            var o = r.Create(r.Item("mech.parklock.axial")!, Vec2.Zero);
            Assert.True(o.Flag("engaged", false));
            var engaged = (PathPrim)t.Render(o, Fx.Ctx()).First(p => p.Tag == "pawl");
            var released = t.Actions(o, Lang.En).Single(a => a.Id == "param.engaged").Apply(o);
            var lifted = (PathPrim)t.Render(released, Fx.Ctx()).First(p => p.Tag == "pawl");
            Assert.True(Prim.Bounds(new[] { lifted })!.Value.Bottom < Prim.Bounds(new[] { engaged })!.Value.Bottom, "the released pawl tooth leaves the gap");
            var more = t.Actions(o, Lang.En).Single(a => a.Id == "param.teeth.inc").Apply(o);
            Assert.Equal(13, more.Int("teeth", 0));
        }

        [Fact]
        public void CycloidalDiscFollowsTheLobeCount()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gearset.cycloidal")!;
            var o = r.Create(r.Item("mech.cycloidal.axial")!, Vec2.Zero);
            Assert.Equal(12, t.Render(o, Fx.Ctx()).Count(p => p.Tag == "pin"));
            Assert.Equal(16, t.Render(o.WithParam("lobes", 15), Fx.Ctx()).Count(p => p.Tag == "pin"));
            Assert.Single(t.Render(o, Fx.Ctx()), p => p.Tag == "disc");
        }

        [Fact]
        public void StrainWaveGearShowsItsThreeMembers()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.gearset.harmonic")!;
            var prims = t.Render(r.Create(r.Item("mech.harmonic.axial")!, Vec2.Zero), Fx.Ctx());
            foreach (var part in new[] { "generator", "flexspline", "circular-spline" }) Assert.Contains(prims, p => p.Tag == part);
        }

        // ------------------------------------------------------------------ housing and sump

        [Fact]
        public void HousingOffersBearingSeatsInBothWalls()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.housing.gearbox")!;
            var o = r.Create(r.Item("mech.housing")!, new Vec2(100, 100));
            var seats = Anchors(r, o).Where(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Mount).ToList();
            Assert.Equal(new[] { "seat1l", "seat1r", "seat2l", "seat2r" }, seats.Select(s => s.Id));
            Assert.Equal(seats[0].P.Y, seats[1].P.Y, 6);
            Assert.True(seats[0].P.X < 100 && seats[1].P.X > 100);
            Assert.DoesNotContain(Anchors(r, o), a => a.Role is AnchorRole.Hub or AnchorRole.Pitch or AnchorRole.Axis);

            var three = t.Actions(o, Lang.En).Single(a => a.Id == "param.seats.inc").Apply(o);
            Assert.Equal(6, Anchors(r, three).Count(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Mount));
            var placed = o.WithParam("seatpos", "0.25;0.8");
            var ys = Anchors(r, placed).Where(a => a.Id == "seat1l" || a.Id == "seat2l").Select(a => a.P.Y).ToList();
            var wall = Housings.WallFor(o.Size.W, o.Size.H);
            var top = 100 - o.Size.H / 2 + wall;
            var inner = o.Size.H - 2 * wall;
            Assert.Equal(top + 0.25 * inner, ys[0], 6);
            Assert.Equal(top + 0.8 * inner, ys[1], 6);
        }

        [Fact]
        public void BearingHubSnapsIntoAHousingSeat()
        {
            var r = Fx.Reg();
            var housing = r.Create(r.Item("mech.housing")!, new Vec2(100, 100));
            var seat = Anchors(r, housing).First(a => a.Id == "seat1l");
            var bearing = r.Create(r.Item("mech.bearing.ball.stick")!, seat.P + new Vec2(1, 1.2));
            var doc = Doc(housing, bearing);
            var ctx = new RenderContext(doc);
            var opt = new SnapOptions { Tolerance = 3, Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(bearing, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { bearing.Id }, ctx, opt), opt);
            Assert.Equal("concentric", res.Kind);
            Assert.Equal(seat.P, new Vec2(bearing.Transform.X, bearing.Transform.Y) + res.Delta);
            // A seat is no shaft: the housing neither carries the bearing nor is carried by it.
            var placed = Edits.Move(doc, new[] { bearing.Id }, res.Delta);
            Assert.DoesNotContain(Links.For(r, placed, new[] { housing.Id }), l => l.Kind is LinkKind.Carried or LinkKind.Mesh);
        }

        [Fact]
        public void HousingFitsAroundItsContentsWithSeatsAtTheShafts()
        {
            var r = Fx.Reg();
            var g1 = r.Create(r.Item("mech.spur.stick")!, new Vec2(100, 80)) with { Size = new Size2(8, 24) };
            var g2 = r.Create(r.Item("mech.spur.stick")!, new Vec2(110, 104)) with { Size = new Size2(8, 24) };
            var s1 = r.Create(r.Item("mech.shaft.line")!, new Vec2(105, 80));
            var s2 = r.Create(r.Item("mech.shaft.line")!, new Vec2(105, 104));
            var housing = r.Create(r.Item("mech.housing")!, new Vec2(105, 92));
            var doc = Doc(g1, g2, s1, s2, housing);
            var t = r.Type("mech.housing.gearbox")!;
            var fit = t.Actions(housing, new RenderContext(doc)).Single(a => a.Id == "param.fit");
            var after = fit.Run(doc, housing, new RenderContext(doc));
            var h = after.Get(housing.Id)!;
            var b = h.Bounds();
            foreach (var g in new[] { g1, g2 })
            {
                var gb = g.Bounds();
                Assert.True(gb.X > b.X && gb.Right < b.Right && gb.Y > b.Y && gb.Bottom < b.Bottom, "gear inside the housing");
            }
            Assert.True(b.W < s1.Size.W, "shafts do not size the housing");
            Assert.Equal(2, h.Int("seats", 0));
            var seats = r.PageAnchors(h, new RenderContext(after)).Where(a => a.Id is "seat1l" or "seat2l").Select(a => a.P.Y).ToList();
            Assert.Equal(80, seats[0], 3);
            Assert.Equal(104, seats[1], 3);
            // Nothing to fit around: unchanged.
            var lone = r.Create(r.Item("mech.housing")!, new Vec2(400, 400));
            var doc2 = Edits.Add(after, after.Pages[0].Id, lone);
            Assert.Same(doc2, t.Actions(lone, new RenderContext(doc2)).Single(a => a.Id == "param.fit").Run(doc2, lone, new RenderContext(doc2)));
        }

        [Fact]
        public void SumpLevelMovesTheLevelLineAndItsRail()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.housing.sump")!;
            var o = r.Create(r.Item("mech.sump")!, new Vec2(0, 0));
            double LevelY(SketchObject x) => Anchors(r, x).Single(a => a.Role == AnchorRole.Rail && a.Id == "level").P.Y;
            var up = t.Actions(o, Lang.En).Single(a => a.Id == "param.level.inc").Apply(o);
            Assert.True(LevelY(up) < LevelY(o), "a higher level is further up");
            var line = (LinePrim)t.Render(o, Fx.Ctx()).Single(p => p.Tag == "level");
            Assert.Equal(LevelY(o), line.A.Y, 6);
            Assert.Contains(t.Render(o, Fx.Ctx()), p => p.Tag == "oil");
            Assert.Contains(t.Render(o, Fx.Ctx()), p => p.Tag == "level-mark");
            Assert.Contains(t.Render(o, Fx.Ctx()), p => p.Tag == "drain");
            var noDrain = t.Actions(o, Lang.En).Single(a => a.Id == "param.drain").Apply(o);
            Assert.DoesNotContain(t.Render(noDrain, Fx.Ctx()), p => p.Tag == "drain");
        }

        [Fact]
        public void SumpDocksUnderTheHousing()
        {
            var r = Fx.Reg();
            var housing = r.Create(r.Item("mech.housing")!, new Vec2(100, 100));
            var baseP = Anchors(r, housing).Single(a => a.Id == "base").P;
            var sump = r.Create(r.Item("mech.sump")!, new Vec2(101.5, 100 + housing.Size.H / 2 + 10 + 1));
            var doc = Doc(housing, sump);
            var ctx = new RenderContext(doc);
            var opt = new SnapOptions { Tolerance = 3, Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(sump, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { sump.Id }, ctx, opt), opt);
            Assert.Equal("mount", res.Kind);
            var mount = r.PageAnchors(sump, ctx).Single(a => a.Id == "mount").P + res.Delta;
            Assert.Equal(baseP, mount);
        }

        // ------------------------------------------------------------------ persistence

        [Fact]
        public void DocumentJsonRoundTripKeepsAllNewElements()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Transmission elements");
            var objs = new List<SketchObject>();
            var x = 20.0;
            foreach (var id in NewItems)
            {
                var o = r.Create(r.Item(id)!, new Vec2(x, 60));
                objs.Add(o);
                x += 12;
            }
            objs[0] = objs[0].WithParam("pitch", 5.0).WithParam("lefthand", true);
            objs[NewItems.ToList().IndexOf("mech.cvt.stick")] = objs[NewItems.ToList().IndexOf("mech.cvt.stick")].WithParam("ratio", 1.8);
            objs[NewItems.ToList().IndexOf("mech.housing")] = objs[NewItems.ToList().IndexOf("mech.housing")].WithParam("seats", 3).WithParam("seatpos", "0.2;0.5;0.9");
            objs[NewItems.ToList().IndexOf("mech.sump")] = objs[NewItems.ToList().IndexOf("mech.sump")].WithParam("level", 0.35);
            doc = Edits.Add(doc, doc.Pages[0].Id, objs.ToArray());

            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));

            var screw = back.Get(objs[0].Id)!;
            Assert.Equal("ball", screw.Str("kind", ""));
            Assert.Equal(5.0, screw.Num("pitch", 0), 9);
            Assert.True(screw.Flag("lefthand", false));
            var housing = back.Get(objs[NewItems.ToList().IndexOf("mech.housing")].Id)!;
            Assert.Equal("0.2;0.5;0.9", housing.Str("seatpos", ""));
            Assert.Equal(6, r.PageAnchors(housing, new RenderContext(back)).Count(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Mount));
            Assert.Equal(0.35, back.Get(objs[NewItems.ToList().IndexOf("mech.sump")].Id)!.Num("level", 0), 9);
            foreach (var o in back.Objects.Values) Assert.NotEmpty(r.Render(o, new RenderContext(back)));
        }

        [Fact]
        public void ScrewDriveDocumentRoundTripKeepsTheNutOnTheAxis()
        {
            var (r, doc, screw, nut) = ScrewWithNut("mech.leadscrew.side", "mech.leadnut.section");
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Contains(Links.For(r, back, new[] { nut.Id }), l => l.Kind == LinkKind.Carried && l.A == screw.Id);
            var after = Coupling.Propagate(r, back, Edits.Move(back, new[] { screw.Id }, new Vec2(0, 7)));
            Assert.Equal(57, after.Get(nut.Id)!.Transform.Y, 6);
        }
    }
}
