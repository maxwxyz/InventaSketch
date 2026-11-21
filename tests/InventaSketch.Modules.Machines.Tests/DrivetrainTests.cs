using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Mechanical;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    /// <summary>
    /// Drivetrain schematic parts (e-machine, combustion engine, wheel) on mechanical shafts: registration, search, rendering,
    /// hub anchors, coupling with <c>mech.shaft</c> and the "Insert ▸" companions. Uses a registry with the mechanical and
    /// machines modules only (no module catalog).
    /// </summary>
    public class DrivetrainTests
    {
        internal static Registry Both() => new Registry().Register(new MechanicalModule()).Register(new MachinesModule());

        private static readonly string[] NewItems =
        {
            "machines.emachine.stick", "machines.emachine.side", "machines.emachine.axial",
            "machines.engine.block", "machines.engine.schematic", "machines.engine.schematic.3cyl", "machines.wheel.schematic",
        };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Drivetrain");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Item(Registry r, string id, double x, double y) => r.Create(r.Item(id)!, new Vec2(x, y));

        private static Vec2 Pos(SketchDocument doc, string id) => new(doc.Get(id)!.Transform.X, doc.Get(id)!.Transform.Y);

        private static Vec2 HubOf(Registry r, SketchDocument doc, string id)
            => r.PageAnchors(doc.Get(id)!, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Hub).P;

        private static void Near(Vec2 expected, Vec2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) < 1e-6, $"{what}: expected {expected}, got {actual}");

        // ------------------------------------------------------------------ registration, names, search

        [Fact]
        public void DrivetrainTypesAndItemsAreRegisteredAndBilingual()
        {
            var r = Fx.Reg();
            Assert.Contains(r.Categories, c => c.Id == "machines.drivetrain" && c.Name.De == "Antriebsstrang (Schema)");
            foreach (var id in new[] { "machines.drivetrain.emachine", "machines.drivetrain.engine" })
            {
                var t = r.Type(id)!;
                Assert.Equal("machines.drivetrain", t.Category);
                Assert.False(string.IsNullOrEmpty(t.Name.De));
                Assert.Contains("antriebsstrang", t.Keywords);
                Assert.Contains("drivetrain", t.Keywords);
                Assert.Contains("powertrain", t.Keywords);
            }
            Assert.Equal("E-Maschine (Getriebeschema)", r.Type("machines.drivetrain.emachine")!.Name.De);
            Assert.Equal("Verbrennungsmotor", r.Type("machines.drivetrain.engine")!.Name.De);
            foreach (var id in NewItems)
            {
                var item = r.Item(id);
                Assert.NotNull(item);
                Assert.Equal("machines.drivetrain", item!.Category);
                Assert.False(string.IsNullOrEmpty(item.Name.De));
            }
            Assert.Equal(new[] { "side", "top", "schematic" }, r.Type("machines.drive.wheel")!.Views.Select(v => v.Id));
        }

        [Theory]
        [InlineData("verbrennungsmotor", "machines.drivetrain.engine")]
        [InlineData("engine", "machines.drivetrain.engine")]
        [InlineData("ice", "machines.drivetrain.engine")]
        [InlineData("kurbelwelle", "machines.drivetrain.engine")]
        [InlineData("e-maschine", "machines.drivetrain.emachine")]
        [InlineData("getriebeschema", "machines.drivetrain.emachine")]
        [InlineData("reifen", "machines.drive.wheel")]
        public void SearchFindsDrivetrainParts(string query, string typeId)
            => Assert.Contains(Fx.Reg().Search(query), i => i.TypeId == typeId);

        [Theory]
        [InlineData("antriebsstrang")]
        [InlineData("drivetrain")]
        [InlineData("powertrain")]
        public void DrivetrainSearchFindsMotorGearboxDifferentialWheelsShaftsAndClutch(string query)
        {
            var found = Both().Search(query).Select(i => i.TypeId).ToHashSet();
            foreach (var id in new[]
            {
                "machines.drivetrain.emachine", "machines.drivetrain.engine", "machines.drive.motor", "machines.drive.gearbox",
                "machines.drive.differential", "machines.drive.wheel", "mech.shaft", "mech.gear.spur", "mech.gearset.differential",
                "mech.clutch.disc", "mech.clutch.multidisc", "mech.joint.universal",
            })
                Assert.Contains(id, found);
        }

        // ------------------------------------------------------------------ rendering

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1.8, 0.7)]
        [InlineData(0.5, 2)]
        [InlineData(0.3, 0.3)]
        [InlineData(4, 4)]
        public void NewItemsRenderInsideTheirBoxInEveryViewAlsoStretched(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var id in NewItems)
            {
                var item = r.Item(id)!;
                var t = r.Type(item.TypeId)!;
                foreach (var v in t.Views)
                    foreach (var lang in new[] { Lang.En, Lang.De })
                    {
                        var o = r.Create(item, Vec2.Zero) with { View = v.Id };
                        o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                        Fx.AssertInsideBox(t.Render(o, Fx.Ctx(lang)), o.Size, $"{id}/{v.Id} {sx}x{sy} {lang}");
                    }
            }
        }

        [Fact]
        public void OptionsAndCylinderCountsRenderInsideTheBox()
        {
            var r = Fx.Reg();
            var em = r.Type("machines.drivetrain.emachine")!;
            foreach (var v in em.Views)
                foreach (var shaft in new[] { true, false })
                    foreach (var ground in new[] { true, false })
                    {
                        var o = em.Create(Vec2.Zero).WithParam("shaft", shaft).WithParam("ground", ground).WithParam("hatch", shaft) with { View = v.Id };
                        Fx.AssertInsideBox(em.Render(o, Fx.Ctx()), o.Size, $"emachine/{v.Id}/{shaft}/{ground}");
                    }
            var engine = r.Create(r.Item("machines.engine.schematic")!, Vec2.Zero);
            for (var n = 0; n <= 8; n++)
            {
                var o = engine.WithParam("cylinders", n);
                var prims = Fx.Render(r, o);
                Fx.AssertInsideBox(prims, o.Size, "engine cylinders " + n);
                Assert.Equal(Math.Max(1, Math.Min(6, n)), Fx.Count(prims, "piston"));
            }
            Assert.Equal(4, Fx.Count(Fx.Render(r, engine), "rod"));
        }

        [Fact]
        public void EMachineShowsRotorStatorAndToggles()
        {
            var r = Fx.Reg();
            var t = r.Type("machines.drivetrain.emachine")!;
            var o = r.Create(r.Item("machines.emachine.stick")!, Vec2.Zero);
            var prims = Fx.Render(r, o);
            Assert.Equal(2, Fx.Count(prims, "rotor"));
            Assert.Equal(2, Fx.Count(prims, "stator"));
            Assert.Equal(1, Fx.Count(prims, "shaft"));
            Assert.Equal(2, Fx.Count(prims, "hub"));
            Assert.True(Fx.Count(prims, "ground") > 0);
            ContextAction A(SketchObject x, string id) => t.Actions(x, Lang.En).Single(a => a.Id == id);
            var noShaft = A(o, "param.shaft").Apply(o);
            Assert.Equal(0, Fx.Count(Fx.Render(r, noShaft), "shaft"));
            var noGround = A(o, "param.ground").Apply(o);
            Assert.Equal(0, Fx.Count(Fx.Render(r, noGround), "ground"));
            // the stator is hatched in every view
            foreach (var view in new[] { "stick", "side", "axial" })
                Assert.Contains(Fx.Render(r, o with { View = view }), p => p.Tag == "stator" && FillOf(p) == FillKind.Hatch);
        }

        private static FillKind? FillOf(Primitive p) => p switch
        {
            RectPrim x => x.Style.Fill?.Kind,
            PathPrim x => x.Style.Fill?.Kind,
            EllipsePrim x => x.Style.Fill?.Kind,
            _ => null,
        };

        // ------------------------------------------------------------------ anchors

        [Fact]
        public void EMachineAndWheelExposeAHubAtTheCentreInEveryView()
        {
            var r = Fx.Reg();
            foreach (var typeId in new[] { "machines.drivetrain.emachine", "machines.drive.wheel" })
            {
                var t = r.Type(typeId)!;
                foreach (var v in t.Views)
                {
                    var o = t.Create(new Vec2(40, 30)) with { View = v.Id };
                    var hub = Assert.Single(r.PageAnchors(o, Fx.Ctx()), a => a.Role == AnchorRole.Hub);
                    Near(new Vec2(40, 30), hub.P, typeId + "/" + v.Id);
                }
            }
            var em = r.Create(r.Item("machines.emachine.stick")!, new Vec2(40, 30));
            var anchors = r.PageAnchors(em, Fx.Ctx()).ToList();
            Assert.Contains(anchors, a => a.Role == AnchorRole.Port && a.Id == "w" && Math.Abs(a.P.X - (40 - em.Size.W / 2)) < 1e-9);
            Assert.Contains(anchors, a => a.Role == AnchorRole.Port && a.Id == "e");
            Assert.Contains(anchors, a => a.Role == AnchorRole.Mount && a.Id == "mount");
        }

        [Fact]
        public void EngineExposesItsOutputHubPortAndCrankAxis()
        {
            var r = Fx.Reg();
            var sch = r.Create(r.Item("machines.engine.schematic")!, new Vec2(100, 50));
            var a = r.PageAnchors(sch, Fx.Ctx()).ToList();
            var hub = Assert.Single(a, x => x.Role == AnchorRole.Hub);
            Near(new Vec2(100 + sch.Size.W / 2, 50 + 6 * sch.Size.H / 30), hub.P, "schematic hub at the output shaft end");
            Assert.Contains(a, x => x.Role == AnchorRole.Port && x.Id == "out" && x.P.DistanceTo(hub.P) < 1e-9);
            var axis = Assert.Single(a, x => x.Role == AnchorRole.Axis);
            Assert.Equal(hub.P.Y, axis.P.Y, 9);
            var block = r.Create(r.Item("machines.engine.block")!, new Vec2(100, 50));
            var bh = Assert.Single(r.PageAnchors(block, Fx.Ctx()), x => x.Role == AnchorRole.Hub);
            Near(new Vec2(100 + block.Size.W / 2, 50), bh.P, "block hub at the right edge");
        }

        // ------------------------------------------------------------------ coupling with mech.shaft

        public static IEnumerable<object[]> CarriedParts() => new[]
        {
            new object[] { "machines.emachine.stick", "mech.shaft.line" },
            new object[] { "machines.emachine.side", "mech.shaft.body" },
            new object[] { "machines.wheel.schematic", "mech.shaft.line" },
            new object[] { "machines.wheel.top", "mech.shaft.body" },
            new object[] { "machines.engine.schematic", "mech.shaft.line" },
            new object[] { "machines.engine.block", "mech.shaft.line" },
        };

        /// <summary>Shaft (120 mm) whose left end lies on the part's hub for output hubs, else the hub 30 mm right of its left end.</summary>
        private static (Registry R, SketchDocument Doc, string Part, string Shaft, Vec2 Hub) OnShaft(string partItem, string shaftItem, double offAxis = 0)
        {
            var r = Both();
            var part = Item(r, partItem, 80, 60);
            var hub = r.PageAnchors(part, Fx.Ctx()).Single(a => a.Role == AnchorRole.Hub).P;
            var output = partItem.StartsWith("machines.engine", StringComparison.Ordinal);
            var left = output ? hub.X : hub.X - 30;
            var shaft = Item(r, shaftItem, left + 60, hub.Y + offAxis);
            Assert.Equal(120, shaft.Size.W, 9);
            return (r, Doc(part, shaft), part.Id, shaft.Id, hub);
        }

        [Theory]
        [MemberData(nameof(CarriedParts))]
        public void MovingTheShaftCarriesThePart(string partItem, string shaftItem)
        {
            var (r, before, part, shaft, _) = OnShaft(partItem, shaftItem);
            var delta = new Vec2(17, -9);
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { shaft }, delta));
            Near(Pos(before, part) + delta, Pos(after, part), partItem + " follows " + shaftItem);
        }

        [Theory]
        [MemberData(nameof(CarriedParts))]
        public void StretchingTheShaftKeepsThePartAtItsPlaceOnTheAxis(string partItem, string shaftItem)
        {
            var (r, before, part, shaft, hub) = OnShaft(partItem, shaftItem);
            var s0 = before.Get(shaft)!;
            var leftEnd = new Vec2(s0.Transform.X - s0.Size.W / 2, s0.Transform.Y);
            var after = Coupling.Propagate(r, before, Edits.Scale(before, new[] { shaft }, leftEnd, 1.5, 1));
            var expected = leftEnd + (hub - leftEnd) * 1.5;
            Near(expected, HubOf(r, after, part), partItem + " hub after stretching " + shaftItem);
        }

        [Theory]
        [MemberData(nameof(CarriedParts))]
        public void RotatingTheShaftTakesThePartAround(string partItem, string shaftItem)
        {
            var (r, before, part, shaft, hub) = OnShaft(partItem, shaftItem);
            var s0 = before.Get(shaft)!;
            var centre = new Vec2(s0.Transform.X, s0.Transform.Y);
            var after = Coupling.Propagate(r, before, Edits.Rotate(before, new[] { shaft }, centre, 90));
            var d = hub - centre;
            var expected = centre + new Vec2(-d.Y, d.X); // 90° clockwise on screen (y down)
            Near(expected, HubOf(r, after, part), partItem + " hub after rotating " + shaftItem);
        }

        [Theory]
        [MemberData(nameof(CarriedParts))]
        public void APartBesideTheShaftStaysAndMovingThePartAloneSlidesIt(string partItem, string shaftItem)
        {
            var (r, before, part, shaft, _) = OnShaft(partItem, shaftItem, offAxis: 5);
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { shaft }, new Vec2(10, 0)));
            Assert.Equal(Pos(before, part), Pos(after, part));

            // A wheel slides along its shaft; a drive (e-machine, engine) leads its shaft (ObjectType.DrivesShaft).
            var (r2, on, part2, shaft2, _) = OnShaft(partItem, shaftItem);
            var slid = Coupling.Propagate(r2, on, Edits.Move(on, new[] { part2 }, new Vec2(-6, 0)));
            var drives = r2.Type(on.Get(part2)!.Type)!.DrivesShaft;
            Assert.Equal(partItem.StartsWith("machines.emachine") || partItem.StartsWith("machines.engine"), drives);
            Assert.Equal(drives ? Pos(on, shaft2) + new Vec2(-6, 0) : Pos(on, shaft2), Pos(slid, shaft2));
        }

        [Fact]
        public void WheelSideViewMovesWithAConcentricGear()
        {
            var r = Both();
            var wheel = Item(r, "machines.wheel", 100, 80);
            var gear = Item(r, "mech.spur.axial", 100, 80);
            var before = Doc(wheel, gear);
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { gear.Id }, new Vec2(5, 5)));
            Near(new Vec2(105, 85), Pos(after, wheel.Id), "wheel bonded to the gear on the same axle");
        }

        [Fact]
        public void EMachineHubSnapsOntoTheShaftAxis()
        {
            var r = Both();
            var shaft = Item(r, "mech.shaft.line", 100, 50);
            var em = Item(r, "machines.emachine.stick", 92, 51.3);
            var doc = Doc(shaft, em);
            var ctx = new RenderContext(doc);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { em.Id }, ctx, Fx.Snap);
            var res = Snapper.SnapMove(r.PageAnchors(em, ctx).ToList(), Vec2.Zero, targets, Fx.Snap);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-1.3, res.Delta.Y, 9);
        }

        [Fact]
        public void SampleDrivetrainFollowsItsShafts()
        {
            var (r, doc, ids) = SampleDrivetrain();
            // Moving the motor shaft carries engine, clutch, e-machine and the input gear.
            var after = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { ids["shaft1"] }, new Vec2(0, -4)));
            foreach (var k in new[] { "engine", "clutch", "emachine", "gear1" })
                Near(Pos(doc, ids[k]) + new Vec2(0, -4), Pos(after, ids[k]), k);
            // Moving the axle carries the differential, the final gear and both wheels.
            after = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { ids["axle"] }, new Vec2(3, 6)));
            foreach (var k in new[] { "diff", "gear4", "wheelL", "wheelR" })
                Near(Pos(doc, ids[k]) + new Vec2(3, 6), Pos(after, ids[k]), k);
            Assert.Equal(Pos(doc, ids["emachine"]), Pos(after, ids["emachine"]));
        }

        // ------------------------------------------------------------------ e-machine variants

        public static readonly string[] Layouts = { "inner", "outer", "axial", "axial-2r", "axial-2s" };
        public static readonly string[] Kinds = { "pmsm", "eesm", "asm", "srm" };

        [Fact]
        public void EveryArrangementAndMachineTypeRendersInsideTheBoxWithTheHubOnTheAxis()
        {
            var r = Fx.Reg();
            var t = r.Type("machines.drivetrain.emachine")!;
            foreach (var layout in Layouts)
                foreach (var kind in Kinds)
                    foreach (var v in t.Views)
                        foreach (var (sx, sy) in new[] { (1.0, 1.0), (1.8, 0.7), (0.4, 1.6), (0.3, 0.3), (3.0, 3.0) })
                            foreach (var flags in new[] { true, false })
                            {
                                var o = t.Create(new Vec2(50, 40)).WithParam("layout", layout).WithParam("kind", kind)
                                    .WithParam("ground", flags).WithParam("shaft", flags).WithParam("hatch", flags) with { View = v.Id };
                                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                                var what = $"{layout}/{kind}/{v.Id} {sx}x{sy} {flags}";
                                Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, what);
                                var hub = Assert.Single(r.PageAnchors(o, Fx.Ctx()), a => a.Role == AnchorRole.Hub);
                                Near(new Vec2(50, 40), hub.P, what);
                            }
        }

        [Theory]
        [InlineData("pmsm", "magnet")]
        [InlineData("eesm", "field")]
        [InlineData("eesm", "slipring")]
        [InlineData("eesm", "brush")]
        [InlineData("asm", "cage")]
        [InlineData("srm", "teeth")]
        public void MachineTypesCarryTheirMarkInEveryArrangementAndView(string kind, string tag)
        {
            var r = Fx.Reg();
            var t = r.Type("machines.drivetrain.emachine")!;
            foreach (var layout in Layouts)
                foreach (var v in t.Views)
                {
                    if (tag is "slipring" or "brush" && v.Id == "axial") continue; // slip rings sit beside the machine
                    var o = t.Create(Vec2.Zero).WithParam("layout", layout).WithParam("kind", kind) with { View = v.Id };
                    var prims = Fx.Render(r, o);
                    Assert.True(Fx.Count(prims, tag) > 0, $"{kind} {layout}/{v.Id}: no {tag}");
                    foreach (var other in new[] { "magnet", "field", "cage", "teeth" }.Where(x => x != tag && !(kind == "eesm" && x == "field")))
                        Assert.Equal(0, Fx.Count(prims, other));
                }
        }

        [Theory]
        [InlineData("inner", 2, 2)]
        [InlineData("outer", 2, 2)]
        [InlineData("axial", 2, 2)]
        [InlineData("axial-2r", 4, 2)]
        [InlineData("axial-2s", 2, 4)]
        public void ArrangementsShowTheirRotorsAndStators(string layout, int rotors, int stators)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("machines.emachine.stick")!, Vec2.Zero).WithParam("layout", layout);
            foreach (var view in new[] { "stick", "side" })
            {
                var prims = Fx.Render(r, o with { View = view });
                // outer rotor section: rotor stack plus bell
                Assert.Equal(view == "side" && layout == "outer" ? 4 : rotors, Fx.Count(prims, "rotor"));
                Assert.Equal(stators, Fx.Count(prims, "stator"));
            }
            // one hub cross (two strokes) per rotor carrier in the schematic
            Assert.Equal(layout == "axial-2r" ? 4 : 2, Fx.Count(Fx.Render(r, o), "hub"));
        }

        [Fact]
        public void ArrangementAndMachineTypeCycle()
        {
            var r = Fx.Reg();
            var t = r.Type("machines.drivetrain.emachine")!;
            var o = r.Create(r.Item("machines.emachine.stick")!, Vec2.Zero);
            var layouts = new List<string>();
            var kinds = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var a = t.Actions(o, Lang.De).Single(x => x.Id == "param.layout");
                Assert.Equal(IconIds.Cycle, a.Icon);
                Assert.StartsWith("Nächste Bauform", a.Label.De);
                o = a.Apply(o);
                layouts.Add(o.Str("layout", ""));
            }
            for (var i = 0; i < 4; i++)
            {
                o = t.Actions(o, Lang.En).Single(x => x.Id == "param.kind").Apply(o);
                kinds.Add(o.Str("kind", ""));
            }
            Assert.Equal(new[] { "outer", "axial", "axial-2r", "axial-2s", "inner" }, layouts);
            Assert.Equal(new[] { "eesm", "asm", "srm", "pmsm" }, kinds);
        }

        [Theory]
        [InlineData("yasa")]
        [InlineData("doppelläufer")]
        [InlineData("außenläufer")]
        [InlineData("axialfluss")]
        [InlineData("schleifring")]
        [InlineData("käfigläufer")]
        public void SearchFindsEMachineVariants(string query)
            => Assert.Contains(Fx.Reg().Search(query), i => i.TypeId == "machines.drivetrain.emachine");

        [Fact]
        public void EveryEMachineVariantFollowsItsShaft()
        {
            foreach (var layout in Layouts)
                foreach (var kind in Kinds)
                    foreach (var (view, shaftItem) in new[] { ("stick", "mech.shaft.line"), ("side", "mech.shaft.body") })
                    {
                        var r = Both();
                        var shaft = Item(r, shaftItem, 100, 60);
                        var em = Item(r, "machines.emachine.stick", 90, 60).WithParam("layout", layout).WithParam("kind", kind) with { View = view };
                        var before = Doc(shaft, em);
                        var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { shaft.Id }, new Vec2(-7, 12)));
                        Near(new Vec2(83, 72), Pos(after, em.Id), $"{layout}/{kind}/{view}");
                    }
        }

        /// <summary>
        /// One page per arrangement: the four machine types side by side, each on a shaft next to a gear pair, in the
        /// schematic (top row) and in the side view (bottom row).
        /// </summary>
        internal static IEnumerable<(string Layout, Registry R, SketchDocument Doc)> VariantPages()
        {
            foreach (var layout in Layouts)
            {
                var r = Both();
                var objs = new List<SketchObject>();
                for (var k = 0; k < Kinds.Length; k++)
                {
                    var x0 = 45 + k * 82.0;
                    var kind = Kinds[k];
                    SketchObject Em(string view, double y) => Item(r, "machines.emachine.stick", x0 - 12, y)
                        .WithParam("layout", layout).WithParam("kind", kind) with { View = view };
                    // schematic row
                    objs.Add(Item(r, "mech.shaft.line", x0, 50) with { Size = new Size2(76, 8) });
                    objs.Add(Em("stick", 50));
                    objs.Add(Item(r, "mech.spur.stick", x0 + 20, 50) with { Size = new Size2(6, 20) });
                    objs.Add(Item(r, "mech.shaft.line", x0 + 22, 74) with { Size = new Size2(32, 8) });
                    objs.Add(Item(r, "mech.spur.stick", x0 + 20, 74) with { Size = new Size2(6, 28) });
                    // side row
                    var side = Em("side", 140).WithParam("shaft", false); // the drawn shaft replaces the stubs
                    objs.Add(Item(r, "mech.shaft.body", x0, 140) with { Size = new Size2(76, 6) });
                    objs.Add(side with { Size = new Size2(side.Size.W * 0.85, side.Size.H * 0.85) });
                    objs.Add(Item(r, "mech.spur.side", x0 + 22, 140) with { Size = new Size2(7, 20) });
                    objs.Add(Item(r, "mech.shaft.body", x0 + 24, 164) with { Size = new Size2(28, 6) });
                    objs.Add(Item(r, "mech.spur.side", x0 + 22, 164) with { Size = new Size2(7, 28) });
                }
                var doc = SketchDocument.Create(new Size2(340, 190), "E-machine " + layout);
                yield return (layout, r, Edits.Add(doc, doc.Pages[0].Id, objs.ToArray()));
            }
        }

        // ------------------------------------------------------------------ companions ("Insert ▸")

        [Fact]
        public void CompanionsAreRegisteredTypes()
        {
            var r = Both();
            foreach (var t in r.Types)
                foreach (var c in t.Companions) Assert.True(r.Type(c) is not null, $"{t.Id} companion {c} is not registered");
        }

        [Fact]
        public void ShaftOffersGearsBearingsEMachineWheelAndClutch()
        {
            var r = Both();
            var shaft = Item(r, "mech.shaft.line", 100, 50);
            var s = r.Suggestions(shaft);
            Assert.Equal("mech.gear.spur", s[0].TypeId);
            Assert.Contains(s, i => i.TypeId == "mech.bearing.ball");
            Assert.Contains(s, i => i.Id == "machines.emachine.stick");
            Assert.Contains(s, i => i.Id == "machines.wheel.schematic");
            Assert.Contains(s, i => i.TypeId == "mech.clutch.disc");
            Assert.Contains(s, i => i.TypeId == "mech.coupling.flexible");
            // a shaft in body view gets the e-machine in its side view
            var body = r.Suggestions(Item(r, "mech.shaft.body", 100, 50));
            Assert.Contains(body, i => i.Id == "machines.emachine.side");
        }

        [Fact]
        public void EMachineOffersInverterAndBatteryBlocks()
        {
            var r = Both();
            var s = r.Suggestions(Item(r, "machines.emachine.stick", 100, 50));
            Assert.Equal("machines.block.inverter", s[0].Id);
            Assert.Equal("machines.block.battery", s[1].Id);
            Assert.Contains(s, i => i.TypeId == "mech.shaft");
            var engine = r.Suggestions(Item(r, "machines.engine.schematic", 100, 50));
            Assert.Contains(engine, i => i.TypeId == "mech.clutch.disc");
            Assert.Contains(engine, i => i.TypeId == "mech.flywheel");
            var wheel = r.Suggestions(Item(r, "machines.wheel.schematic", 100, 50));
            Assert.Contains(wheel, i => i.TypeId == "mech.shaft");
            Assert.Contains(wheel, i => i.TypeId == "mech.gearset.differential");
            var diff = r.Suggestions(Item(r, "mech.differential.stick", 100, 50));
            Assert.Contains(diff, i => i.Id == "machines.wheel.schematic");
        }

        [Fact]
        public void MachinesCompanionsAreSkippedWithoutTheMachinesModule()
        {
            var r = new Registry().Register(new MechanicalModule());
            var s = r.Suggestions(r.Create(r.Item("mech.shaft.line")!, Vec2.Zero));
            Assert.DoesNotContain(s, i => i.TypeId.StartsWith("machines.", StringComparison.Ordinal));
            Assert.Contains(s, i => i.TypeId == "mech.clutch.disc");
        }

        // ------------------------------------------------------------------ sample drivetrain

        /// <summary>
        /// P2-hybrid e-axle sketch: engine → clutch → e-machine on the input shaft → spur gear pair → intermediate shaft →
        /// final gear on the axle with differential → wheels; battery and inverter blocks above the e-machine.
        /// </summary>
        internal static (Registry R, SketchDocument Doc, Dictionary<string, string> Ids) SampleDrivetrain()
        {
            var r = Both();
            var ids = new Dictionary<string, string>();
            var objs = new List<SketchObject>();
            SketchObject Put(string key, string item, double x, double y, double? w = null, double? h = null)
            {
                var o = Item(r, item, x, y);
                if (w is not null || h is not null) o = o with { Size = new Size2(w ?? o.Size.W, h ?? o.Size.H) };
                ids[key] = o.Id;
                objs.Add(o);
                return o;
            }
            const double y1 = 60, y2 = 88, y3 = 120;
            // engine: output hub at (70, y1)
            Put("engine", "machines.engine.schematic", 45, y1 - 6);
            Put("shaft1", "mech.shaft.line", 145, y1, 150, 8);
            Put("clutch", "mech.clutch.disc", 84, y1);
            Put("emachine", "machines.emachine.stick", 112, y1);
            Put("gear1", "mech.spur.stick", 160, y1, 8, 20);
            Put("bearing1", "mech.bearing.ball.stick", 205, y1);
            Put("shaft2", "mech.shaft.line", 190, y2, 100, 8);
            Put("gear2", "mech.spur.stick", 160, y2, 8, 36);
            Put("gear3", "mech.spur.stick", 215, y2, 8, 16);
            Put("axle", "mech.shaft.line", 190, y3, 210, 8);
            Put("gear4", "mech.spur.stick", 215, y3, 8, 48);
            Put("diff", "mech.differential.stick", 245, y3, 36, 36);
            Put("wheelL", "machines.wheel.schematic", 85, y3, 9, 34);
            Put("wheelR", "machines.wheel.schematic", 295, y3, 9, 34);
            Put("battery", "machines.block.battery", 70, 22);
            Put("inverter", "machines.block.inverter", 112, 22);
            var doc = Doc(objs.ToArray());
            return (r, doc, ids);
        }

        [Fact]
        public void SampleDrivetrainJsonRoundTrip()
        {
            var (_, doc, ids) = SampleDrivetrain();
            var engine = doc.Get(ids["engine"])!.WithParam("cylinders", 3);
            doc = Edits.Replace(doc, engine);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc, indented: true));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(3, back.Get(ids["engine"])!.Int("cylinders", 0));
            Assert.Equal("schematic", back.Get(ids["wheelL"])!.View);
            Assert.Equal("stick", back.Get(ids["emachine"])!.View);
        }

        [Fact]
        public void SampleDrivetrainRendersToSvg()
        {
            var (r, doc, ids) = SampleDrivetrain();
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
            foreach (var id in ids.Values) Assert.Contains("data-isk-id=\"" + id + "\"", svg);
            // engine output on the input shaft, wheels on the axle ends
            Near(new Vec2(70, 60), HubOf(r, doc, ids["engine"]), "engine output");
            var axle = doc.Get(ids["axle"])!;
            Assert.Equal(axle.Transform.X - axle.Size.W / 2, Pos(doc, ids["wheelL"]).X, 9);
            Assert.Equal(axle.Transform.X + axle.Size.W / 2, Pos(doc, ids["wheelR"]).X, 9);
        }
    }
}
