using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Value net follow-ups (engineering-calculations.md section 6.1): lines of different media meeting only at a port of
    /// another module's element stay apart (flow and return on an energy heat port), room values reach the building
    /// services elements placed inside the room (containment), and the text-valued Grashof class of the four-bar.
    /// Default catalogue; every expected value is a hand calculation.
    /// </summary>
    public class ValueNetFollowUpTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private const double C0 = 273.15;
        private const double RhoC = 1000 * 4190.0;

        private static ValueTable Solve(SketchDocument doc, params string[] ids)
            => ValueSolver.Solve(doc, Reg, ids.Length == 0 ? null : ids, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(string item, string id) => Reg.Create(Reg.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Follow-ups");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Line(string item, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => Make(item, id) with
            {
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        // ------------------------------------------------------------------ 1. flow and return on one foreign port

        /// <summary>
        /// Energy heat pump (8 kW, 55/45 °C) with a building flow pipe AND a return pipe on its single heat port, a radiator
        /// between them (Φ₅₀ = 1500 W, n = 1.3, θ_i = 20 °C). Before the fix both pipes met at the heat port node, so the
        /// radiator's return temperature was spread from its flow temperature (θ_R = θ_F).
        /// </summary>
        [Fact]
        public void FlowAndReturnPipesOnOneEnergyHeatPortStayApart()
        {
            var doc = Doc(
                Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "55 °C").WithParam("returnTemp", "45 °C"),
                Make("bs.heat.radiator", "rad").WithParam("phi50", "1500 W"),
                Line("bs.pipe.flow", "pf", ("hp", "heat"), ("rad", "flow")),
                Line("bs.pipe.return", "pr", ("rad", "return"), ("hp", "heat")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);

            // The flow pipe still takes the heat port's values (V̇ = 8 kW/(ρ·c·10 K), θ_F = 55 °C).
            var vdot = 8000 / (RhoC * 10);
            Assert.Equal(55 + C0, t.Get("rad", "thf")!.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, t.Get("rad", "thf")!.Provenance);
            Assert.Equal(vdot, t.Get("rad", "Vdot")!.Value!.Value, 12);
            Assert.Equal(55 + C0, t.Get("pf", "theta")!.Value!.Value, 9);

            // The return side is the radiator's own operating point, below the flow temperature, and the return pipe carries it.
            var thr = t.Get("rad", "thr")!.Value!.Value;
            // About 53.8 °C: 800 W/K · (55 °C − θ_R) = 1500 W · (Δθ_ln/50 K)^1.3 with Δθ_ln ≈ 34.4 K.
            Assert.True(thr < 55 + C0 - 0.5, "θ_R must stay below θ_F, was " + (thr - C0) + " °C");
            Assert.True(thr > 20 + C0, "θ_R must stay above the room temperature, was " + (thr - C0) + " °C");
            Assert.NotEqual(Provenance.Linked, t.Get("rad", "thr")!.Provenance);
            Assert.Equal(thr, t.Get("pr", "theta")!.Value!.Value, 9);
            // Energy balance of the radiator: Φ = ρ·c·V̇·(θ_F − θ_R).
            Assert.Equal(RhoC * vdot * (55 + C0 - thr), t.Get("rad", "Phi")!.Value!.Value, 6);
            // The heat pump's flow temperature is not taken over by the return pipe.
            Assert.DoesNotContain(t.Of("pr"), v => v.Via?.Relation.Id == "net.cross.theta");
        }

        [Fact]
        public void TheReturnPipeAloneOnAHeatPortStillJoins()
        {
            // One medium at the port: the junction is joined as before (only the mix of media is held apart).
            var doc = Doc(
                Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "55 °C").WithParam("returnTemp", "45 °C"),
                Make("bs.heat.radiator", "rad"),
                Line("bs.pipe.return", "pr", ("rad", "return"), ("hp", "heat")));
            var t = Solve(doc);
            Assert.Equal(8000 / (RhoC * 10), t.Get("pr", "Vdot")!.Value!.Value, 12);
        }

        [Fact]
        public void WithoutTheRadiatorCharacteristicTheReturnStaysOpen()
        {
            // Nothing determines θ_R: it must stay unknown rather than take θ_F (55 °C) through the shared heat port.
            var doc = Doc(
                Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "55 °C"),
                Make("bs.heat.radiator", "rad"),
                Line("bs.pipe.flow", "pf", ("hp", "heat"), ("rad", "flow")),
                Line("bs.pipe.return", "pr", ("rad", "return"), ("hp", "heat")));
            var t = Solve(doc);
            Assert.Equal(55 + C0, t.Get("rad", "thf")!.Value!.Value, 9);
            Assert.Null(t.Get("rad", "thr")!.Value);
            Assert.Null(t.Get("pr", "theta")!.Value);
        }

        // ------------------------------------------------------------------ 2. room values by containment

        /// <summary>A 40 × 30 mm room at 1:100: 4 m × 3 m = 12 m², h = 2.5 m (default), V = 30 m³.</summary>
        private static SketchObject Room(string id, double x = 0, double y = 0)
            => Reg.Create(Reg.Item("arch.room")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(x - 20, y - 15), new Vec2(x + 20, y - 15), new Vec2(x + 20, y + 15), new Vec2(x - 20, y + 15) },
            };

        private static SketchObject At(SketchObject o, double x, double y) => o with { Transform = o.Transform with { X = x, Y = y } };

        [Fact]
        public void AirTerminalInsideARoomTakesItsAreaHeightAndVolume()
        {
            // n = 0.5 h⁻¹: V̇ = n·V = 0.5 · 30 m³ / 3600 s = 4.17e-3 m³/s = 15 m³/h.
            var doc = Doc(Room("room"), At(Make("bs.vent.terminal.extract", "term"), 5, 3).WithParam("airChange", "0,5"));
            var t = Solve(doc, "term");
            Assert.Empty(t.Conflicts);
            Assert.Equal(12.0, t.Get("term", "A")!.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, t.Get("term", "A")!.Provenance);
            Assert.Equal("bs.room.link", t.Get("term", "A")!.Via!.Relation.Id);
            Assert.Equal(2.5, t.Get("term", "h")!.Value!.Value, 9);
            Assert.Equal(30.0, t.Get("term", "V")!.Value!.Value, 9);
            Assert.Equal(15.0 / 3600, t.Get("term", "Vdot")!.Value!.Value, 12);
        }

        [Fact]
        public void EnteredRoomHeightReachesTheTerminal()
        {
            // h = 3 m on the room: V = 36 m³, V̇ = 2 h⁻¹ · 36 m³ = 72 m³/h.
            var doc = Doc(Room("room").WithParam("height", 3.0), At(Make("bs.vent.terminal.extract", "term"), 0, 0).WithParam("airChange", "2"));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(3.0, t.Get("term", "h")!.Value!.Value, 9);
            Assert.Equal(36.0, t.Get("term", "V")!.Value!.Value, 9);
            Assert.Equal(72.0 / 3600, t.Get("term", "Vdot")!.Value!.Value, 12);
        }

        [Fact]
        public void TerminalOutsideEveryRoomOrOnAnotherPageTakesNothing()
        {
            var outside = Doc(Room("room"), At(Make("bs.vent.terminal.extract", "term"), 60, 0).WithParam("airChange", "2"));
            var t = Solve(outside);
            Assert.Null(t.Get("term", "A")!.Value);
            Assert.DoesNotContain(t.Of("term"), v => v.Via?.Relation.Id == "bs.room.link");

            // The room on page 1, the terminal on page 2 at the same coordinates.
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Two pages");
            doc = Edits.Add(doc, doc.Pages[0].Id, Room("room"));
            doc = doc with { Pages = doc.Pages.Append(new Page { Id = "page2", Name = "Page 2" }).ToList() };
            doc = Edits.Add(doc, "page2", At(Make("bs.vent.terminal.extract", "term"), 0, 0));
            var t2 = Solve(doc, "term");
            Assert.DoesNotContain(t2.Of("term"), v => v.Via?.Relation.Id == "bs.room.link");
        }

        [Fact]
        public void NestedRoomsGiveTheSmallestContainingRoom()
        {
            // A big room 40 × 30 mm and a small 10 × 10 mm room (1 m²) inside it around the terminal.
            var small = Reg.Create(Reg.Item("arch.room")!, Vec2.Zero) with
            {
                Id = "small",
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10) },
            };
            var doc = Doc(Room("big"), small, At(Make("bs.vent.terminal.extract", "term"), 5, 5));
            var t = Solve(doc, "term");
            Assert.Equal(1.0, t.Get("term", "A")!.Value!.Value, 9);
        }

        [Fact]
        public void RadiatorTakesTheRoomHeatingLoadAndHintsWhenItGivesLess()
        {
            // q = 100 W/m² on the 12 m² room: Φ_HL = 1200 W.
            var room = Room("room").WithParam("heatLoadDensity", "100 W/m²");
            var weak = Doc(room, At(Make("bs.heat.radiator", "rad"), 0, 10).WithParam("heatOutput", "1000 W"));
            var t = Solve(weak);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1200.0, t.Get("room", "PhiHL")!.Value!.Value, 9);
            Assert.Equal(1200.0, t.Get("rad", "PhiHL")!.Value!.Value, 9);
            Assert.Equal(Provenance.Linked, t.Get("rad", "PhiHL")!.Provenance);
            var findings = ValueSolver.Checks(t, Reg, new RenderContext(weak, Lang.En));
            Assert.Contains(findings, f => f.CheckId == "heat.roomload" && f.ObjectId == "rad");

            // Two radiators of 700 W each cover the load: no hint.
            var enough = Doc(room,
                At(Make("bs.heat.radiator", "r1"), -10, 10).WithParam("heatOutput", "700 W"),
                At(Make("bs.heat.radiator", "r2"), 10, 10).WithParam("heatOutput", "700 W"));
            var t2 = Solve(enough);
            Assert.DoesNotContain(ValueSolver.Checks(t2, Reg, new RenderContext(enough, Lang.En)), f => f.CheckId == "heat.roomload");
        }

        // ------------------------------------------------------------------ 3. text-valued Grashof class

        [Fact]
        public void TheValuesApiShowsTheGrashofClassAsTextAndRefusesToStoreIt()
        {
            var api = new EngineApi(Reg);
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "kin.fourbar", 100, 80);
            var cls = EngineApiTests.Call(api, "values", new { session, id }).GetProperty("objects").EnumerateArray().Single()
                .GetProperty("vars").EnumerateArray().Single(v => v.GetProperty("key").GetString() == "cls");
            Assert.Equal("Kurbelschwinge", cls.GetProperty("display").GetString());
            Assert.Equal("derived", cls.GetProperty("provenance").GetString());
            Assert.Equal("kin.grashofClass", cls.GetProperty("kind").GetString());
            Assert.ThrowsAny<Exception>(() => EngineApiTests.Call(api, "setValue", new { session, id, key = "cls", text = "Doppelkurbel" }));
        }

        [Fact]
        public void TheGrashofClassIsShownAsTextAndNeverEntered()
        {
            var o =Reg.Create(Reg.Item("kin.fourbar")!, new Vec2(100, 80)) with { Id = "m" }; // a 12, b 36, c 30, d 40: crank-rocker
            var doc = Doc(o);
            var t = Solve(doc);
            var cls = t.Get("m", "cls")!;
            Assert.Equal(Provenance.Derived, cls.Provenance);
            Assert.Equal("Crank-rocker", ValueFormat.Format(cls.Value!.Value, t.UnitOf(cls), Lang.En));
            Assert.Equal("Kurbelschwinge", ValueFormat.Format(cls.Value!.Value, t.UnitOf(cls), Lang.De));
            Assert.Throws<FormatException>(() => ValueParser.Parse("1", cls.Def.Kind, Lang.En));
            Assert.Null(cls.Interval);
            Assert.DoesNotContain(t.Missing("m").SelectMany(m => m), k => k == "cls");
            Assert.Contains("Crank-rocker", CalcSheet.Csv(CalcSheet.Rows(t, Reg, new[] { "m" }, Lang.En), Lang.En));
            // Something stored under its key is ignored: the class stays computed.
            var stored = Doc(o.WithParam("grashofClass", 5.0));
            Assert.Equal(cls.Value, Solve(stored).Get("m", "cls")!.Value);
        }
    }
}
