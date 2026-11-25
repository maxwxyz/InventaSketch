using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new MachinesModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        public static readonly SnapOptions Snap = new() { Tolerance = 2, Grid = null, Alignment = false, Page = false };

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
            var r = Prim.Bounds(prims)!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        public static IReadOnlyList<Primitive> Render(Registry r, SketchObject o, Lang lang = Lang.En) => r.TypeOf(o).Render(o, Ctx(lang));

        public static int Count(IEnumerable<Primitive> prims, string tag) => prims.Count(p => p.Tag == tag);

        public static int Subpaths(Primitive p) => ((PathPrim)p).Segments.Count(s => s.Op == SegOp.M);
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] TypeIds =
        {
            "machines.radial", "machines.axial",
            "machines.part.stator", "machines.part.rotor", "machines.part.magnet", "machines.part.coil", "machines.part.airgap",
            "machines.part.endwinding", "machines.part.housing", "machines.part.cooling", "machines.part.encoder", "machines.part.slipring",
            "machines.drive.battery", "machines.drive.inverter", "machines.drive.dclink", "machines.drive.motor", "machines.drive.gearbox",
            "machines.drive.differential", "machines.drive.wheel",
            "machines.drivetrain.emachine", "machines.drivetrain.engine",
            "machines.ann.flux", "machines.ann.rotation", "machines.ann.torque",
        };

        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var r = Fx.Reg();
            Assert.Equal("machines", r.Modules.Single().Id);
            var ids = r.Types.Select(t => t.Id).ToList();
            foreach (var id in TypeIds) Assert.Contains(id, ids);
            Assert.All(ids, id => Assert.StartsWith("machines.", id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasIcons()
        {
            var r = Fx.Reg();
            var icons = new HashSet<string>(typeof(IconIds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!));
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.NotEmpty(t.Views);
                foreach (var v in t.Views)
                {
                    Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                    Assert.True(v.Icon is not null && icons.Contains(v.Icon), $"{t.Id}/{v.Id} has no IconIds icon");
                }
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "." + p.Key);
                    if (p.Options is not null) Assert.All(p.Options, op => Assert.False(string.IsNullOrEmpty(op.Label.De)));
                }
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

        [Theory]
        [InlineData("motor")]
        [InlineData("elektromotor")]
        [InlineData("käfigläufer")]
        [InlineData("squirrel cage")]
        [InlineData("wechselrichter")]
        [InlineData("inverter")]
        [InlineData("axialfluss")]
        [InlineData("wickelkopf")]
        [InlineData("drehmoment")]
        [InlineData("resolver")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var v in t.Views)
                {
                    var o = new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize, View = v.Id };
                    Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, t.Id + "/" + v.Id + " (bare)");
                }
        }
    }

    public class RenderTests
    {
        [Theory]
        [InlineData(Lang.En)]
        [InlineData(Lang.De)]
        public void EveryLibraryItemRendersInEveryViewInsideTheBox(Lang lang)
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.ItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero) with { View = view };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx(lang)), o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count > 100, "expected many item/view combinations, got " + count);
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
        public void AllRotorTypesLayoutsAndOptionsRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var typeId in new[] { "machines.radial", "machines.part.rotor", "machines.part.stator" })
            {
                var t = r.Type(typeId)!;
                foreach (var view in t.Views.Select(v => v.Id))
                    foreach (var rotor in new[] { "spm", "ipm", "spoke", "eesm", "asm", "srm" })
                        foreach (var layout in new[] { "inner", "outer" })
                            foreach (var poles in new[] { 2, 4, 8, 16, 40 })
                                foreach (var flags in new[] { false, true })
                                {
                                    var o = t.Create(Vec2.Zero).WithParam("rotor", rotor).WithParam("layout", layout).WithParam("poles", poles)
                                        .WithParam("currents", flags).WithParam("cooling", flags).WithParam("housing", !flags).WithParam("hatch", !flags) with { View = view };
                                    Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{typeId}/{view}/{rotor}/{layout}/{poles}/{flags}");
                                }
            }
        }

        [Theory]
        [InlineData(6)]
        [InlineData(12)]
        [InlineData(36)]
        [InlineData(48)]
        [InlineData(72)]
        public void SlotCountChangesNumberOfSlotPaths(int slots)
        {
            var r = Fx.Reg();
            var machine = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero).WithParam("slots", slots);
            var prims = Fx.Render(r, machine);
            Assert.Equal(slots, Fx.Count(prims, "slot"));
            // the stator lamination is one path: yoke circle (4 cubic arcs) + toothed bore contour
            var stator = (PathPrim)prims.Single(p => p.Tag == "stator");
            Assert.Equal(2, Fx.Subpaths(stator));
            Assert.Equal(FillKind.Hatch, stator.Style.Fill!.Kind);

            var core = r.Create(r.Item("machines.stator")!, Vec2.Zero).WithParam("slots", slots);
            Assert.Equal(slots, Fx.Count(Fx.Render(r, core), "slot"));
            Assert.Equal(0, Fx.Count(Fx.Render(r, core.WithParam("winding", false)), "slot"));
        }

        [Theory]
        [InlineData("spm", 8, "magnet", 8)]
        [InlineData("spm", 12, "magnet", 12)]
        [InlineData("ipm", 8, "magnet", 16)]
        [InlineData("ipm", 6, "magnet", 12)]
        [InlineData("spoke", 10, "magnet", 10)]
        [InlineData("eesm", 4, "field", 8)]
        [InlineData("eesm", 2, "field", 8)] // salient-pole drawings use at least four poles
        [InlineData("asm", 8, "magnet", 0)]
        public void PoleCountChangesRotorGeometry(string rotor, int poles, string tag, int expected)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero).WithParam("rotor", rotor).WithParam("poles", poles);
            Assert.Equal(expected, Fx.Count(Fx.Render(r, o), tag));
        }

        [Fact]
        public void RotorVariantsHaveTheirCharacteristicParts()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.radial.asm")!, Vec2.Zero);
            Assert.Equal(28, Fx.Count(Fx.Render(r, o), "bar"));
            Assert.Equal(40, Fx.Count(Fx.Render(r, o.WithParam("bars", 40)), "bar"));
            // squirrel cage bars are holes in the hatched rotor lamination: outer circle + shaft bore + one hole per bar
            Assert.Equal(2 + 28, Fx.Subpaths(Fx.Render(r, o).Single(p => p.Tag == "rotor")));
            // switched reluctance: open slots with one dividing line per slot (two coil sides of adjacent tooth coils)
            var srm = r.Create(r.Item("machines.radial.srm")!, Vec2.Zero);
            Assert.Equal(12, Fx.Count(Fx.Render(r, srm), "coil-split"));
            Assert.Equal(0, Fx.Count(Fx.Render(r, srm), "magnet"));
            // IPM: the magnet pockets are holes in the rotor lamination
            var ipm = r.Create(r.Item("machines.radial.ipm")!, Vec2.Zero);
            Assert.Equal(2 + 16, Fx.Subpaths(Fx.Render(r, ipm).Single(p => p.Tag == "rotor")));
            // north and south magnets alternate (different fills)
            var fills = Fx.Render(r, r.Create(r.Item("machines.radial.spm")!, Vec2.Zero)).Where(p => p.Tag == "magnet")
                .Select(p => ((PathPrim)p).Style.Fill!.Color).ToList();
            Assert.Equal(4, fills.Count(c => c == fills[0]));
            Assert.Equal(2, fills.Distinct().Count());
        }

        [Fact]
        public void CurrentDirectionsToggle()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero);
            Assert.Equal(0, Fx.Count(Fx.Render(r, o), "current"));
            var on = Fx.Render(r, o.WithParam("currents", true));
            Assert.Equal(36, Fx.Count(on, "current"));
            // 8 poles, 36 slots: belts of 4.5 slots alternate between dots and crosses (two strokes per cross)
            var dots = Fx.Count(on, "current-dot");
            Assert.Equal(36, dots + Fx.Count(on, "current-cross") / 2);
            Assert.InRange(dots, 14, 22);
            var eesm = Fx.Render(r, o.WithParam("rotor", "eesm").WithParam("currents", true));
            Assert.Equal(36 + 16, Fx.Count(eesm, "current")); // stator slots + field coil sides
        }

        [Fact]
        public void CoolingAddsChannelsToHousing()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero);
            Assert.Equal(2, Fx.Subpaths(Fx.Render(r, o).Single(p => p.Tag == "housing")));
            Assert.Equal(2 + 24, Fx.Subpaths(Fx.Render(r, o.WithParam("cooling", true)).Single(p => p.Tag == "housing")));
            Assert.DoesNotContain(Fx.Render(r, o.WithParam("housing", false)), p => p.Tag == "housing");
            var side = o with { View = "side", Size = new Size2(72, 60) };
            Assert.Equal(2, Fx.Render(r, side).Count(p => p.Tag == "housing"));
            Assert.True(Fx.Render(r, side.WithParam("cooling", true)).Where(p => p.Tag == "housing").All(p => Fx.Subpaths(p) == 1 + 8));
        }

        [Fact]
        public void SideViewShowsStackEndWindingsBearingsAndShaft()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.radial.side")!, Vec2.Zero);
            var prims = Fx.Render(r, o);
            Assert.Equal(2, Fx.Count(prims, "stator"));
            Assert.Equal(2, Fx.Count(prims, "winding"));
            Assert.Equal(2, Fx.Count(prims, "rotor"));
            Assert.Equal(4, prims.Count(p => p.Tag == "bearing" && p is RectPrim));
            Assert.Single(prims, p => p.Tag == "shaft");
            Assert.Contains(prims, p => p.Tag == "axis" && p is LinePrim l && l.Style.Role == LineRole.Center);
            var asm = Fx.Render(r, o.WithParam("rotor", "asm"));
            Assert.Equal(4, Fx.Count(asm, "endring"));
            var eesm = Fx.Render(r, o.WithParam("rotor", "eesm"));
            Assert.Equal(2, Fx.Count(eesm, "slipring"));
        }

        [Fact]
        public void SchematicSymbolShowsLettersAndPhaseLeads()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.motor.symbol")!, Vec2.Zero);
            var prims = Fx.Render(r, o);
            Assert.Equal("M", ((TextPrim)prims.Single(p => p.Tag == "letter")).Text);
            Assert.Equal("3~", ((TextPrim)prims.Single(p => p.Tag == "phases")).Text);
            Assert.Equal(3, Fx.Count(prims, "lead"));
            var gen = Fx.Render(r, o.WithParam("mode", "generator").WithParam("phases", 6));
            Assert.Equal("G", ((TextPrim)gen.Single(p => p.Tag == "letter")).Text);
            Assert.Equal(6, Fx.Count(gen, "lead"));
        }

        [Fact]
        public void AxialFluxFaceAndSide()
        {
            var r = Fx.Reg();
            var face = r.Create(r.Item("machines.axial.face")!, Vec2.Zero);
            Assert.Equal(16, Fx.Count(Fx.Render(r, face), "magnet"));
            Assert.Equal(20, Fx.Count(Fx.Render(r, face.WithParam("poles", 20)), "magnet"));
            var stator = r.Create(r.Item("machines.axial.stator")!, Vec2.Zero);
            Assert.Equal(18, Fx.Count(Fx.Render(r, stator), "coil"));
            var side = Fx.Render(r, r.Create(r.Item("machines.axial.side")!, Vec2.Zero));
            Assert.Equal(4, Fx.Count(side, "rotor")); // two discs, upper and lower half
            Assert.Equal(4, Fx.Count(side, "magnet"));
            Assert.Equal(2, Fx.Count(side, "stator"));
        }

        [Fact]
        public void BlockLabelsAreBilingual()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.block.battery")!, Vec2.Zero);
            Assert.Equal("Battery", ((TextPrim)Fx.Render(r, o, Lang.En).Single(p => p.Tag == "label")).Text);
            Assert.Equal("Batterie", ((TextPrim)Fx.Render(r, o, Lang.De).Single(p => p.Tag == "label")).Text);
            Assert.Equal("HV", ((TextPrim)Fx.Render(r, o with { Text = "HV" }).Single(p => p.Tag == "label")).Text);
        }

        [Fact]
        public void InverterSymbolHasSixSwitchesAndDiodes()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.block.inverter.symbol")!, Vec2.Zero);
            var prims = Fx.Render(r, o);
            Assert.Equal(6, Fx.Count(prims, "switch"));
            Assert.Equal(6, prims.Count(p => p.Tag == "diode" && p is PathPrim pa && pa.IsClosed));
            Assert.Equal(3, Fx.Count(prims, "terminal"));
        }
    }

    public class SnappingTests
    {
        private static (Registry R, SketchDocument Doc) Doc(params SketchObject[] objs)
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, objs);
            return (r, doc);
        }

        private static SnapResult SnapOf(Registry r, SketchDocument doc, SketchObject moving, SnapOptions opt)
        {
            var ctx = new RenderContext(doc);
            var anchors = r.PageAnchors(moving, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { moving.Id }, ctx, opt);
            return Snapper.SnapMove(anchors, Vec2.Zero, targets, opt);
        }

        [Fact]
        public void RoundPartsExposeHubAndCircleAnchors()
        {
            var r = Fx.Reg();
            var stator = r.PageAnchors(r.Create(r.Item("machines.stator")!, new Vec2(50, 40)), Fx.Ctx()).ToList();
            Assert.Contains(stator, a => a.Role == AnchorRole.Hub && a.P == new Vec2(50, 40));
            var bore = Assert.Single(stator, a => a.Role == AnchorRole.PitchInner);
            Assert.Equal(AnchorKind.Circle, bore.Kind);
            var rotor = r.PageAnchors(r.Create(r.Item("machines.rotor.spm")!, new Vec2(50, 40)), Fx.Ctx()).ToList();
            var outer = Assert.Single(rotor, a => a.Role == AnchorRole.Pitch);
            // Real radii (no longer both on the air-gap centre circle): the rotor is about 1 mm smaller than the bore,
            // close enough to count as a fit for the snapper (concentric, not an internal gear mesh).
            Assert.Equal(24, bore.Radius, 9);
            Assert.Equal(23.03, outer.Radius, 9);
            Assert.True(outer.Radius >= Snapper.InternalMeshMaxRatio * bore.Radius);
            Assert.Contains(rotor, a => a.Role == AnchorRole.Hub);
            foreach (var id in new[] { "machines.radial.spm", "machines.airgap", "machines.housing", "machines.cooling.jacket", "machines.resolver", "machines.axial.face" })
                Assert.Contains(r.PageAnchors(r.Create(r.Item(id)!, Vec2.Zero), Fx.Ctx()), a => a.Role == AnchorRole.Hub);
        }

        [Theory]
        [InlineData(0.8, -0.5)]
        [InlineData(-1.2, 0.9)]
        [InlineData(0.05, 0.0)]
        public void RotorSnapsConcentricallyIntoStator(double dx, double dy)
        {
            var r0 = Fx.Reg();
            var stator = r0.Create(r0.Item("machines.stator")!, new Vec2(100, 90));
            var rotor = r0.Create(r0.Item("machines.rotor.spm")!, new Vec2(100 + dx, 90 + dy));
            var (r, doc) = Doc(stator, rotor);
            var res = SnapOf(r, doc, rotor, Fx.Snap);
            Assert.Equal("concentric", res.Kind);
            Assert.Equal(-dx, res.Delta.X, 9);
            Assert.Equal(-dy, res.Delta.Y, 9);
        }

        [Theory]
        [InlineData(0.95, 1.0, -0.6)]   // rotor scaled down: offset to the internal-mesh position would be within reach
        [InlineData(1.03, -0.7, 0.4)]   // rotor scaled up, slightly larger than the bore
        public void FreelyScaledRotorStillSnapsConcentricIntoStator(double scale, double dx, double dy)
        {
            var r0 = Fx.Reg();
            var stator = r0.Create(r0.Item("machines.stator")!, new Vec2(100, 90));
            var rotor = r0.Create(r0.Item("machines.rotor.spm")!, new Vec2(100 + dx, 90 + dy));
            rotor = rotor with { Size = new Size2(rotor.Size.W * scale, rotor.Size.H * scale) };
            var (r, doc) = Doc(stator, rotor);
            var res = SnapOf(r, doc, rotor, Fx.Snap);
            Assert.Equal("concentric", res.Kind);
            Assert.Equal(-dx, res.Delta.X, 9);
            Assert.Equal(-dy, res.Delta.Y, 9);
        }

        [Fact]
        public void RadialMachineExposesRealAirGapRadii()
        {
            var r = Fx.Reg();
            foreach (var (item, outerRotor) in new[] { ("machines.radial.spm", false), ("machines.radial.outer", true) })
            {
                var anchors = r.PageAnchors(r.Create(r.Item(item)!, Vec2.Zero), Fx.Ctx()).Where(a => a.Kind == AnchorKind.Circle).ToList();
                var stator = anchors.Single(a => a.Id == "stator");
                var rotor = anchors.Single(a => a.Id == "rotor");
                Assert.Equal(outerRotor ? AnchorRole.Pitch : AnchorRole.PitchInner, stator.Role);
                var (inner, ring) = outerRotor ? (stator, rotor) : (rotor, stator);
                Assert.True(inner.Radius < ring.Radius, item + ": air gap between the circles");
                Assert.True(inner.Radius >= Snapper.InternalMeshMaxRatio * ring.Radius, item + ": treated as a fit");
            }
        }

        [Fact]
        public void StatorSnapsConcentricallyAroundRotorAndMagnetSnapsOntoRotor()
        {
            var r0 = Fx.Reg();
            var rotor = r0.Create(r0.Item("machines.rotor.spm")!, new Vec2(100, 90));
            var stator = r0.Create(r0.Item("machines.stator")!, new Vec2(101, 91));
            var (r, doc) = Doc(rotor, stator);
            var res = SnapOf(r, doc, stator, Fx.Snap);
            Assert.Equal("concentric", res.Kind);
            Assert.Equal(new Vec2(-1, -1), res.Delta);

            // a magnet segment placed on top of the rotor: its inner arc centre snaps onto the rotor centre
            var magnet = r0.Create(r0.Item("machines.magnet")!, Vec2.Zero);
            var arcCentre = r0.PageAnchors(magnet, Fx.Ctx()).Single(a => a.Role == AnchorRole.PitchInner);
            magnet = magnet with { Transform = magnet.Transform with { X = 100 - arcCentre.P.X + 0.7, Y = 90 - arcCentre.P.Y - 0.4 } };
            var (r2, doc2) = Doc(rotor, magnet);
            var res2 = SnapOf(r2, doc2, magnet, Fx.Snap);
            Assert.Equal("concentric", res2.Kind);
            Assert.Equal(-0.7, res2.Delta.X, 9);
            Assert.Equal(0.4, res2.Delta.Y, 9);
        }

        [Fact]
        public void RotorSnapsIntoAirGapRingAndHousingAroundStator()
        {
            var r0 = Fx.Reg();
            var ring = r0.Create(r0.Item("machines.airgap")!, new Vec2(60, 60));
            var rotor = r0.Create(r0.Item("machines.rotor.spm")!, new Vec2(60.6, 60.5));
            var (r, doc) = Doc(ring, rotor);
            var res = SnapOf(r, doc, rotor, Fx.Snap);
            Assert.Equal("concentric", res.Kind);
            Assert.Equal(60, 60.6 + res.Delta.X, 9);
            Assert.Equal(60, 60.5 + res.Delta.Y, 9);

            var stator = r0.Create(r0.Item("machines.stator")!, new Vec2(60, 60));
            var housing = r0.Create(r0.Item("machines.housing")!, new Vec2(61, 60.5));
            var (r2, doc2) = Doc(stator, housing);
            var res2 = SnapOf(r2, doc2, housing, Fx.Snap);
            Assert.Equal("concentric", res2.Kind);
            Assert.Equal(new Vec2(60, 60), new Vec2(61, 60.5) + res2.Delta);
        }

        [Theory]
        [InlineData("machines.radial.side")]
        [InlineData("machines.axial.side")]
        [InlineData("machines.stator.side")]
        [InlineData("machines.rotor.side")]
        [InlineData("machines.housing.side")]
        public void SideViewsExposeShaftAxis(string item)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(item)!, new Vec2(100, 50));
            var axis = Assert.Single(r.PageAnchors(o, Fx.Ctx()), a => a.Role == AnchorRole.Axis);
            Assert.Equal(50, axis.P.Y, 9);
            Assert.Equal(50, axis.Q.Y, 9);
            Assert.Equal(-o.Size.W / 2 + 100, Math.Min(axis.P.X, axis.Q.X), 9);
        }

        [Fact]
        public void EncoderHubSnapsOntoMotorShaftAxis()
        {
            var r0 = Fx.Reg();
            var motor = r0.Create(r0.Item("machines.radial.side")!, new Vec2(100, 80));
            var encoder = r0.Create(r0.Item("machines.encoder.side")!, new Vec2(140, 81.3));
            var (r, doc) = Doc(motor, encoder);
            var res = SnapOf(r, doc, encoder, Fx.Snap);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-1.3, res.Delta.Y, 9);
        }

        [Fact]
        public void DriveBlocksExposePortsAndSnapTogether()
        {
            var r0 = Fx.Reg();
            foreach (var item in r0.Library.Where(i => i.Category == "machines.drive"))
                Assert.Contains(r0.PageAnchors(r0.Create(item, Vec2.Zero), Fx.Ctx()), a => a.Role == AnchorRole.Port);
            var inv = r0.Create(r0.Item("machines.block.inverter.symbol")!, new Vec2(100, 100));
            var ports = r0.PageAnchors(inv, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToList();
            Assert.Equal(new[] { "dc+", "dc-", "u", "v", "w" }, ports);
            // battery block dropped next to the inverter block: east port snaps onto west port
            var block = r0.Create(r0.Item("machines.block.inverter")!, new Vec2(100, 100));
            var battery = r0.Create(r0.Item("machines.block.battery")!, new Vec2(100 - 30 - 1.2, 100.5));
            var (r, doc) = Doc(block, battery);
            var res = SnapOf(r, doc, battery, Fx.Snap);
            Assert.Equal("port", res.Kind);
            Assert.Equal(1.2, res.Delta.X, 9);
            Assert.Equal(-0.5, res.Delta.Y, 9);
        }
    }

    public class ActionTests
    {
        [Fact]
        public void EveryMultiViewTypeOffersOneActionPerViewWithItsIcon()
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
                    Assert.Equal(v.Icon, a.Icon);
                    Assert.Equal(v.Id, a.Apply(o).View);
                }
            }
        }

        [Fact]
        public void SwitchingViewsAdaptsProportions()
        {
            var r = Fx.Reg();
            var t = r.Type("machines.radial")!;
            var o = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero);
            var side = t.Actions(o, Lang.En).Single(a => a.Id == "view.side").Apply(o);
            Assert.Equal(1.2, side.Size.W / side.Size.H, 6);
            var back = t.Actions(side, Lang.De).Single(a => a.Id == "view.axial").Apply(side);
            Assert.Equal(back.Size.H, back.Size.W, 6);
        }

        [Fact]
        public void RotorChoiceOffersAllRotorTypes()
        {
            var r = Fx.Reg();
            var t = r.Type("machines.radial")!;
            var o = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero);
            var seen = new List<string>();
            var options = t.Actions(o, Lang.En).Where(x => x.Choice?.Key == "param.rotor").ToList();
            Assert.Equal(6, options.Count);
            Assert.Equal("param.rotor=spm", Assert.Single(options, x => x.IsActive).Id);
            foreach (var rotor in new[] { "ipm", "spoke", "eesm", "asm", "srm", "spm" })
            {
                o = t.Actions(o, Lang.En).Single(x => x.Id == "param.rotor=" + rotor).Apply(o);
                seen.Add(o.Str("rotor", ""));
            }
            Assert.Equal(new[] { "ipm", "spoke", "eesm", "asm", "srm", "spm" }, seen);
        }

        [Fact]
        public void PoleAndSlotSteppersAndToggles()
        {
            var r = Fx.Reg();
            var t = r.Type("machines.radial")!;
            var o = r.Create(r.Item("machines.radial.spm")!, Vec2.Zero);
            ContextAction A(SketchObject x, string id) => t.Actions(x, Lang.En).Single(a => a.Id == id);
            Assert.Equal(10, A(o, "param.poles.inc").Apply(o).Int("poles", 0));
            Assert.Equal(6, A(o, "param.poles.dec").Apply(o).Int("poles", 0));
            Assert.Equal(39, A(o, "param.slots.inc").Apply(o).Int("slots", 0));
            Assert.Equal(33, A(o, "param.slots.dec").Apply(o).Int("slots", 0));
            var two = o.WithParam("poles", 2);
            Assert.DoesNotContain(t.Actions(two, Lang.En), a => a.Id == "param.poles.dec");
            var cur = A(o, "param.currents");
            Assert.False(cur.IsActive);
            var withCurrents = cur.Apply(o);
            Assert.True(withCurrents.Flag("currents", false));
            Assert.True(A(withCurrents, "param.currents").IsActive);
            var cooled = A(o, "param.cooling").Apply(o);
            Assert.True(cooled.Flag("cooling", false));
            Assert.Equal("outer", A(o, "param.layout=outer").Apply(o).Str("layout", ""));
            // the schematic symbol only offers motor/generator
            var sym = r.Create(r.Item("machines.motor.symbol")!, Vec2.Zero);
            Assert.Equal("generator", A(sym, "param.mode=generator").Apply(sym).Str("mode", ""));
            Assert.DoesNotContain(t.Actions(sym, Lang.En), a => a.Choice?.Key == "param.rotor");
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var m = r.Create(r.Item("machines.radial.ipm")!, new Vec2(50, 50)).WithParam("slots", 48);
            doc = Edits.Add(doc, doc.Pages[0].Id, m);
            doc = Edits.Scale(doc, new[] { m.Id }, new Vec2(50, 50), 2, 0.5);
            var g = doc.Get(m.Id)!;
            Assert.Equal(48, g.Int("slots", 0));
            Assert.Equal("ipm", g.Str("rotor", ""));
            Assert.Equal(120, g.Size.W, 6);
            Assert.Equal(30, g.Size.H, 6);
        }
    }

    public class DocumentTests
    {
        /// <summary>E-drive sketch: battery, DC link, inverter, motor, gearbox, differential, wheels plus a motor cross-section.</summary>
        internal static (Registry R, SketchDocument Doc) EDrive()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "E-drive");
            SketchObject At(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = r.Create(r.Item(item)!, new Vec2(x, y));
                return f is null ? o : f(o);
            }
            var objs = new List<SketchObject>
            {
                At("machines.block.battery", 30, 40, o => o with { Ref = "10" }),
                At("machines.block.dclink", 70, 40),
                At("machines.block.inverter", 110, 40),
                At("machines.block.motor", 150, 40),
                At("machines.block.gearbox", 190, 40),
                At("machines.block.differential", 230, 40),
                At("machines.wheel.top", 230, 70),
                At("machines.radial.ipm", 60, 125, o => o.WithParam("currents", true) with { Ref = "12" }),
                At("machines.radial.side", 160, 125, o => o.WithParam("rotor", "asm").WithParam("cooling", true)),
                At("machines.rotation", 60, 125),
                At("machines.axial.side", 270, 130, o => o.WithParam("currents", true)),
            };
            doc = Edits.Add(doc, doc.Pages[0].Id, objs.ToArray());
            doc = Edits.SetReference(doc, "10", "traction battery");
            doc = Edits.SetReference(doc, "12", "electric machine");
            return (r, doc);
        }

        [Fact]
        public void JsonRoundTrip()
        {
            var (_, doc) = EDrive();
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var ipm = back.Objects.Values.Single(o => o.Type == "machines.radial" && o.Str("rotor", "") == "ipm");
            Assert.True(ipm.Flag("currents", false));
            Assert.Equal(36, ipm.Int("slots", 0));
            Assert.Equal("axial", ipm.View);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
        }

        [Fact]
        public void RendersToSvgAndMonochrome()
        {
            var (r, doc) = EDrive();
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
            foreach (var o in doc.Objects.Values) Assert.Contains("data-isk-id=\"" + o.Id + "\"", svg);
            Assert.Contains(S_North, svg);
            var mono = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.DoesNotContain(S_North, mono);
        }

        private const string S_North = "#C0392B";
    }
}
