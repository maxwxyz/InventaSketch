using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Serpentine belt and chain drives over several pulleys and idlers (route geometry, hubs, speed ratio), the idler
    /// pulley and tensioner, the small parts (retaining ring, pins, O-ring) and the torsion spring rate.
    /// </summary>
    public class DriveRouteTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id) => t.Get(id, "n")!.Value!.Value / RpmToSi;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Drives");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Make(Registry r, string item, string id, Vec2 at) => r.Create(r.Item(item)!, at) with { Id = id };

        private static List<Vec2> Hubs(Registry r, SketchObject o)
            => r.PageAnchors(o, new RenderContext(SketchDocument.Create())).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).Select(a => a.P).ToList();

        // ------------------------------------------------------------------ geometry

        [Fact]
        public void TheDefaultRouteRunsTangentiallyAroundThreePulleysAndOverTheBackOfTheIdler()
        {
            var hubs = BeltRoute.Place(BeltRoute.Default, 80, 50);
            Assert.Equal(4, hubs.Count);
            Assert.Equal(new[] { false, false, false, true }, hubs.Select(h => h.Back).ToArray());
            var sense = BeltRoute.Sense(hubs);
            for (var i = 0; i < hubs.Count; i++)
            {
                BeltRoute.Placed a = hubs[i], b = hubs[(i + 1) % hubs.Count];
                var (from, to) = BeltRoute.Tangent(a, b, sense);
                var d = to - from;
                // Both ends on their circles, and the strand is perpendicular to both radii: a common tangent.
                Assert.Equal(a.R, from.DistanceTo(a.C), 9);
                Assert.Equal(b.R, to.DistanceTo(b.C), 9);
                Assert.Equal(0, d.Normalized().Dot((from - a.C).Normalized()), 9);
                Assert.Equal(0, d.Normalized().Dot((to - b.C).Normalized()), 9);
                // Outer tangent between two pulleys (both centres on the same side), crossing tangent next to the idler.
                var sa = Math.Sign(d.Cross(a.C - from));
                var sb = Math.Sign(d.Cross(b.C - to));
                Assert.True(a.Back == b.Back ? sa == sb : sa != sb, $"strand {i}");
            }

            var loop = BeltRoute.Loop(hubs);
            // The belt wraps the three pulleys (their centres inside the loop) and the idler from outside the loop.
            foreach (var h in hubs) Assert.Equal(!h.Back, Geo.PointInPolygon(h.C, loop));
            // It never cuts into a pulley or the idler.
            foreach (var h in hubs) Assert.True(loop.Min(q => q.DistanceTo(h.C)) >= h.R - 1e-6);
            // A closed loop turns once: the signed wraps add up to one full turn in the sense of the loop.
            var wraps = BeltRoute.Wraps(hubs);
            Assert.Equal(sense * 2 * Math.PI, wraps.Sum(w => w.Sweep), 9);
            Assert.True(wraps[3].Sweep * sense < 0, "the idler is wrapped the other way");
            Assert.All(wraps.Take(3), w => Assert.True(w.Sweep * sense > 0));
        }

        [Fact]
        public void TheRouteStaysInsideTheBoxAndScalingChangesOnlyTheSize()
        {
            var r = Fx.Reg();
            var type = r.Type("mech.belt")!;
            var belt = r.Create(r.Item("mech.belt.serpentine")!, Vec2.Zero);
            Assert.Equal("serpentine", belt.View);
            Assert.False(belt.Params.ContainsKey("route"));
            foreach (var size in new[] { new Size2(80, 50), new Size2(200, 40), new Size2(30, 90) })
                Fx.AssertInsideBox(type.Render(belt with { Size = size }, Fx.Ctx()), size, "belt " + size);
            // A route out of range is clamped into the box.
            var wild = belt.WithParam("route", "-2,3,0.9;0.4,-0.4,0.3,b;x;0.1,0.1");
            Fx.AssertInsideBox(type.Render(wild, Fx.Ctx()), wild.Size, "clamped route");
            Assert.Equal(2, BeltRoute.Of(wild).Count);
            // Too few hubs fall back to the default route.
            Assert.Same(BeltRoute.Default, BeltRoute.Parse("0,0,0.2"));
            Assert.Equal(BeltRoute.Default.Count, BeltRoute.Parse(BeltRoute.DefaultText).Count);
        }

        [Fact]
        public void TwoPulleyBeltsAreUnchanged()
        {
            var r = Fx.Reg();
            var type = r.Type("mech.belt")!;
            var open = r.Create(r.Item("mech.belt.open")!, Vec2.Zero) with { Size = new Size2(100, 24) };
            Assert.False(open.Params.ContainsKey("route"));
            var prims = type.Render(open, Fx.Ctx());
            Assert.Equal(2, prims.Count);
            Assert.Equal(new[] { new Vec2(-50, 0), new Vec2(50, 0) }, Hubs(r, open).ToArray());
            Assert.Empty(r.DragHandles(open, Fx.Ctx()));
            Assert.DoesNotContain(type.Actions(open, Lang.En), a => a.Id.StartsWith("route.", StringComparison.Ordinal));
        }

        [Fact]
        public void EveryRouteHubIsAHubAnchorAndCanBeDragged()
        {
            var r = Fx.Reg();
            var belt = Make(r, "mech.belt.serpentine", "belt", new Vec2(100, 80));
            var hubs = Hubs(r, belt);
            Assert.Equal(4, hubs.Count);
            var handles = r.DragHandles(belt, Fx.Ctx());
            Assert.Equal(8, handles.Count);
            Assert.Equal(hubs[0].X, handles.Single(h => h.Key == "hub.0").Position.X, 9);

            // Dragging hub 2 moves it (snapped to the box share) and keeps the others.
            var moved = r.Type("mech.belt")!.MoveHandle(belt, "hub.1", new Vec2(100 + 20, 80 + 5), Fx.Ctx());
            var route = BeltRoute.Of(moved);
            Assert.Equal(0.25, route[1].U, 9);
            Assert.Equal(0.1, route[1].V, 9);
            Assert.Equal(BeltRoute.Default[0].U, route[0].U, 9);

            // Add an idler, remove it again.
            var add = r.Type("mech.belt")!.Actions(belt, Lang.En).Single(a => a.Id == "route.add");
            var more = add.Apply(belt);
            Assert.Equal(5, Hubs(r, more).Count);
            Assert.True(BeltRoute.Of(more)[4].Back);
            var less = r.Type("mech.belt")!.Actions(more, Lang.En).Single(a => a.Id == "route.remove").Apply(more);
            Assert.Equal(4, BeltRoute.Of(less).Count);
        }

        [Fact]
        public void AChainRunsOverSeveralSprocketsWithPlatesAndRollers()
        {
            var r = Fx.Reg();
            var chain = r.Create(r.Item("mech.chain.serpentine")!, Vec2.Zero);
            var prims = r.Type("mech.chain")!.Render(chain, Fx.Ctx());
            Assert.Equal(2, prims.Count(p => p.Tag == "plate"));
            Assert.True(prims.Count(p => p.Tag == "roller") > 30);
            Assert.Equal(4, Hubs(r, chain).Count);
        }

        // ------------------------------------------------------------------ values

        /// <summary>A serpentine belt with a part on every hub (axial items, centred on the hub).</summary>
        private static SketchDocument Drive(Registry r, SketchObject belt, params (string Item, string Id, (string Key, string Text)[] Values)[] parts)
        {
            var hubs = Hubs(r, belt with { Id = "probe" });
            var objs = new List<SketchObject> { belt };
            for (var i = 0; i < parts.Length; i++)
            {
                var o = Make(r, parts[i].Item, parts[i].Id, hubs[i]);
                foreach (var (k, v) in parts[i].Values) o = o.WithParam(k, v);
                objs.Add(o);
            }
            return Doc(objs.ToArray());
        }

        [Fact]
        public void MovingAPulleyOnAnyHubReroutesTheSerpentine()
        {
            var r = Fx.Reg();
            var belt = Make(r, "mech.belt.serpentine", "belt", new Vec2(100, 80));
            var doc = Drive(r, belt,
                ("mech.pulley.axial", "crank", Array.Empty<(string, string)>()),
                ("mech.pulley.axial", "pump", Array.Empty<(string, string)>()),
                ("mech.pulley.axial", "alternator", Array.Empty<(string, string)>()),
                ("mech.idler.axial", "idler", Array.Empty<(string, string)>()));
            var before = Hubs(r, doc.Get("belt")!);
            // The idler sits on the fourth hub (the two-end-hub rule never saw it): moved 6 mm left and 25 mm up, out of the
            // old box, the belt's fourth hub goes with it and the other hubs stay. (Default pump and alternator pulleys touch,
            // so they couple as a mesh; the idler and the crank are free.)
            var shift = new Vec2(-6, -25);
            var after = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { "idler" }, shift));
            var now = Hubs(r, after.Get("belt")!);
            Assert.Equal(before.Count, now.Count);
            Assert.True(now[3].DistanceTo(before[3] + shift) < 0.05, $"hub 4 at {now[3]}, expected {before[3] + shift}");
            foreach (var i in new[] { 0, 1, 2 }) Assert.True(now[i].DistanceTo(before[i]) < 0.05, $"hub {i + 1} moved to {now[i]}");
            Fx.AssertInsideBox(r.Type("mech.belt")!.Render(after.Get("belt")!, Fx.Ctx()), after.Get("belt")!.Size, "rerouted belt");
            // A pulley on the first hub works the same way.
            var again = Coupling.Propagate(r, after, Edits.Move(after, new[] { "crank" }, new Vec2(-15, 0)));
            Assert.True(Hubs(r, again.Get("belt")!)[0].DistanceTo(now[0] + new Vec2(-15, 0)) < 0.05);
        }

        [Fact]
        public void ThreePulleysAndAnIdlerShareTheBeltSpeedAndTheRatioFollowsTheDiameters()
        {
            var r = Fx.Reg();
            var belt = Make(r, "mech.belt.serpentine", "belt", new Vec2(100, 80));
            var doc = Drive(r, belt,
                ("mech.pulley.axial", "crank", new[] { ("diameter", "150 mm"), ("speed", "3000 rpm"), ("torque", "40 N·m") }),
                ("mech.pulley.axial", "pump", new[] { ("diameter", "100 mm") }),
                ("mech.pulley.axial", "alternator", new[] { ("diameter", "60 mm") }),
                ("mech.idler.axial", "idler", new[] { ("diameter", "70 mm") }));
            var topo = MechTopology.Of(r, doc);
            Assert.Equal(4, topo.StrandParts("belt").Count);
            Assert.True(topo.IsIdler("belt", "idler"));
            Assert.False(topo.IsIdler("belt", "pump"));

            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // v = π·d·n = π·0.15 m·50 s⁻¹; n_i = n₁·d₁/d_i: 4500, 7500 and (idler) 6428.6 min⁻¹.
            Assert.Equal(Math.PI * 0.15 * 50, t.Get("belt", "v")!.Value!.Value, 9);
            Assert.Equal(4500, Rpm(t, "pump"), 9);
            Assert.Equal(7500, Rpm(t, "alternator"), 9);
            Assert.Equal(3000 * 150.0 / 70, Rpm(t, "idler"), 9);
            // Three pulleys take or give torque: one effective pull cannot stand for all strand sections, so none is derived.
            Assert.Null(t.Get("pump", "T")?.Value);
            Assert.Null(t.Get("belt", "F")?.Value);
        }

        [Fact]
        public void AnIdlerOnTheBackSideKeepsTheRatioAndTheTorqueOfATwoPulleyDrive()
        {
            var r = Fx.Reg();
            var belt = Make(r, "mech.belt.serpentine", "belt", new Vec2(100, 80)).WithParam("route", "-0.3,0.1,0.18;0.3,0.1,0.18;0,-0.02,0.08,b");
            // The idler is an ordinary pulley here: the back-side hub makes it an idler.
            var doc = Drive(r, belt,
                ("mech.pulley.axial", "p1", new[] { ("diameter", "100 mm"), ("speed", "1500 rpm"), ("torque", "10 N·m") }),
                ("mech.pulley.axial", "p2", new[] { ("diameter", "250 mm") }),
                ("mech.pulley.axial", "idler", new[] { ("diameter", "50 mm") }));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(600, Rpm(t, "p2"), 9);
            Assert.Equal(200, t.Get("belt", "F")!.Value!.Value, 9);
            Assert.Equal(25, t.Get("p2", "T")!.Value!.Value, 9);
            Assert.Equal(3000, Rpm(t, "idler"), 9);
            Assert.Null(t.Get("idler", "T")?.Value);
        }

        [Fact]
        public void TheTensionerCarriesItsIdlerOnTheBelt()
        {
            var r = Fx.Reg();
            var belt = Make(r, "mech.belt.serpentine", "belt", new Vec2(100, 80));
            var hubs = Hubs(r, belt);
            var tensioner = Make(r, "mech.tensioner", "ten", Vec2.Zero);
            var idlerHub = Hubs(r, tensioner).Single();
            tensioner = Edits.Move(Doc(tensioner), new[] { "ten" }, hubs[3] - idlerHub).Get("ten")!;
            var doc = Doc(belt,
                Make(r, "mech.pulley.axial", "p1", hubs[0]).WithParam("diameter", "120 mm").WithParam("speed", "1000 rpm"),
                Make(r, "mech.pulley.axial", "p2", hubs[1]).WithParam("diameter", "80 mm"),
                Make(r, "mech.pulley.axial", "p3", hubs[2]).WithParam("diameter", "60 mm"),
                tensioner.WithParam("diameter", "60 mm"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(2000, Rpm(t, "ten"), 9);
            Assert.Contains(r.PageAnchors(tensioner, Fx.Ctx()), a => a.Role == AnchorRole.Mount && a.Id == "pivot");
        }

        // ------------------------------------------------------------------ idler, small parts, torsion spring

        [Fact]
        public void TheIdlerIsFlatForTheBeltBackAndGroovedForTheRunningFace()
        {
            var r = Fx.Reg();
            var type = r.Type("mech.pulley.idler")!;
            var flat = r.Create(r.Item("mech.idler.axial")!, Vec2.Zero);
            var grooved = r.Create(r.Item("mech.idler.grooved")!, Vec2.Zero);
            Assert.DoesNotContain(type.Render(flat, Fx.Ctx()), p => p.Tag == "groove");
            Assert.Contains(type.Render(grooved, Fx.Ctx()), p => p.Tag == "groove");
            Assert.Equal("loose", ActiveOption(type, flat, "param.hub"));
            var toggle = type.Actions(flat, Lang.En).Single(a => a.Id == "param.back");
            Assert.True(toggle.IsActive);
            Assert.False(toggle.Apply(flat).Flag("back", true));
        }

        private static string? ActiveOption(ObjectType type, SketchObject o, string key)
            => type.Actions(o, Lang.En).Where(a => a.Id.StartsWith(key + "=", StringComparison.Ordinal) && a.IsActive).Select(a => ActionChoices.ValueOf(a.Id)).SingleOrDefault();

        [Fact]
        public void SmallPartsInsertWithoutParametersAndSitOnAShaft()
        {
            var r = Fx.Reg();
            foreach (var item in new[] { "mech.circlip", "mech.circlip.side", "mech.pin.cylindrical", "mech.pin.taper", "mech.pin.split", "mech.oring", "mech.oring.top" })
            {
                var i = r.Item(item);
                Assert.NotNull(i);
                var o = r.Create(i!, Vec2.Zero);
                var type = r.Type(o.Type)!;
                foreach (var v in type.Views) Fx.AssertInsideBox(type.Render(o with { View = v.Id }, Fx.Ctx()), o.Size, item + "/" + v.Id);
            }
            // Ring and O-ring have a bore: they fit onto a shaft like a hub.
            foreach (var item in new[] { "mech.circlip", "mech.circlip.side", "mech.oring", "mech.oring.top" })
            {
                var o = r.Create(r.Item(item)!, Vec2.Zero);
                var hub = r.PageAnchors(o, Fx.Ctx()).Single(a => a.Role == AnchorRole.Hub);
                Assert.True(hub.Radius > 0, item);
            }
        }

        [Fact]
        public void ThePinTypeIsAChoiceWithPictogramsAndChangesTheDrawing()
        {
            var r = Fx.Reg();
            var type = r.Type("mech.pin")!;
            var kind = type.Parameters.Single(p => p.Key == "kind");
            Assert.True(kind.HasOptionIcons);
            var pin = r.Create(r.Item("mech.pin.cylindrical")!, Vec2.Zero);
            var options = type.Actions(pin, Lang.En).Where(a => a.Id.StartsWith("param.kind=", StringComparison.Ordinal)).ToList();
            Assert.Equal(new[] { "cylindrical", "taper", "split" }, options.Select(a => ActionChoices.ValueOf(a.Id)).ToArray());
            var split = options.Single(a => a.Id.EndsWith("split", StringComparison.Ordinal)).Apply(pin);
            Assert.Equal("split", split.Str("kind", ""));
            Assert.Contains(type.Render(split, Fx.Ctx()), p => p.Tag == "eye");
            Assert.DoesNotContain(type.Render(pin, Fx.Ctx()), p => p.Tag == "eye");
        }

        [Fact]
        public void TheTorsionSpringRateGivesTheSpringTorque()
        {
            var r = Fx.Reg();
            var spring = Make(r, "mech.spring.torsion", "s", new Vec2(50, 50)).WithParam("rate", "2 N·m/rad").WithParam("angle", "90 deg");
            var t = Solve(Doc(spring), r);
            Assert.Empty(t.Conflicts);
            // M = c_φ·φ = 2 N·m/rad · π/2 rad.
            Assert.Equal(Math.PI, t.Get("s", "T")!.Value!.Value, 9);
            Assert.Equal(TorsionalRateKey, ValueModel.Variables(r.Type("mech.spring.torsion")).Single(v => v.Key == "c").Kind.Id);
        }

        private const string TorsionalRateKey = "torsionalRate";
    }
}
