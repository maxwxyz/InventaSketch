using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// The room devices give way to the canonical installation symbols of the electrical module (socket outlet, light switch,
    /// luminaire) and the appliance outlet of the building services. The reports and circuit values read those symbols by
    /// type id and parameters (room, feed, phase, power, current). Here they stand in as stubs with the same ids, parameters
    /// and variables (P = U·I), as this test project references the switchgear module only.
    /// </summary>
    public class CanonicalHouseDeviceTests
    {
        /// <summary>A load symbol of another module: U, I, P bound to voltage, current, power with P = U·I.</summary>
        private sealed class StubLoad : ObjectType, IValueType
        {
            private readonly string _id;
            private readonly bool _values;
            public StubLoad(string id, bool values) { _id = id; _values = values; }
            public override string Id => _id;
            public override I18nText Name => new(_id, _id);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new[] { Prim.Circle(0, 0, 2, Style.Visible) };

            private static VarDef V(string key, string param, QuantityKind kind, string unit) => new(key, new I18nText(key, key), new I18nText(key, key), kind) { Param = param, Unit = unit };

            public IReadOnlyList<VarDef> Variables => _values
                ? new[] { V("U", "voltage", Quantities.Voltage, "V"), V("I", "current", Quantities.Current, "A"), V("P", "power", Quantities.Power, "W") }
                : Array.Empty<VarDef>();

            public IReadOnlyList<Relation> Relations => _values
                ? new[]
                {
                    new Relation("stub.power", new I18nText("P", "P"), "{P} = {U}·{I}", "P", "U", "I")
                        .Solve("P", "{U}·{I}", v => v("U") * v("I")).Solve("U", "{P}/{I}", v => v("P") / v("I")).Solve("I", "{P}/{U}", v => v("P") / v("U")),
                }
                : Array.Empty<Relation>();
        }

        private sealed class StubModule : IModule
        {
            public string Id => "stub";
            public I18nText Name => new("Stub", "Stub");
            public string Version => "1";
            public IEnumerable<ObjectType> Types => new ObjectType[]
            {
                new StubLoad("elec.house.socket", true), new StubLoad("elec.house.switch", false), new StubLoad("elec.house.light", true), new StubLoad("bs.elec.appliance", true),
            };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        private static readonly Registry Reg = new Registry().Register(new SwitchgearModule()).Register(new StubModule());

        /// <summary>A symbol of another module, made from a room device's object with the foreign type id.</summary>
        private static SketchObject Foreign(string typeId, string room, string feed, double x = 300, double y = 300)
            => (Fx.Item("sw.house.socket", x, y) with { Type = typeId, Params = new Dictionary<string, ParamValue>() }).WithParam("room", room).WithParam("feed", feed);

        private static string Run(string id, SketchDocument doc, Lang lang = Lang.En)
        {
            var e = Exports.All(Reg).Single(x => x.Id == id);
            var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
            Assert.True(e.Applies(Reg, doc, objects), id + " does not apply");
            return e.Export(Reg, doc, objects, lang);
        }

        private static List<string> Lines(string csv) => csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

        private static ValueTable Solve(SketchDocument doc) => ValueSolver.Solve(doc, Reg, null, new ValueSolveOptions { BudgetMs = 10000 });

        /// <summary>MCB -F2 (kitchen) with a double socket outlet, a dimmer and a cooker outlet of the canonical modules on it.</summary>
        private static (SketchDocument Doc, SketchObject Mcb, SketchObject Socket, SketchObject Dimmer, SketchObject Cooker) Kitchen(string socketPower = "2300 W")
        {
            var mcb = Fx.Item("sw.protection.mcb", 200, 100).WithParam("ref", "-F2").WithParam("circuit", "Kitchen sockets");
            var socket = Foreign("elec.house.socket", "Kitchen", "-F2").WithParam("gangs", 2).WithParam("power", socketPower);
            var dimmer = Foreign("elec.house.switch", "Kitchen", "-F2", 320).WithParam("kind", "dimmer");
            var cooker = Foreign("bs.elec.appliance", "Kitchen", "-F2", 340);
            return (Fx.Doc(mcb, socket, dimmer, cooker), mcb, socket, dimmer, cooker);
        }

        [Fact]
        public void TheInstallationListAndCircuitPlanNameTheCanonicalSymbols()
        {
            var (doc, _, _, _, _) = Kitchen();
            var csv = Lines(Run("sw.installation.csv", doc));
            Assert.Contains("Kitchen,-F2,Kitchen sockets,Miniature circuit breaker (DIN rail),B16,Double socket outlet,,1", csv);
            Assert.Contains("Kitchen,-F2,Kitchen sockets,Miniature circuit breaker (DIN rail),B16,Dimmer,,1", csv);
            Assert.Contains("Kitchen,-F2,Kitchen sockets,Miniature circuit breaker (DIN rail),B16,Cooker connection,,1", csv);
            var md = Run("sw.installation.md", doc, Lang.De);
            Assert.Contains("  - 1 × Zweifachsteckdose", md);
            Assert.Contains("  - 1 × Herdanschluss", md);
            var plan = Lines(Run("sw.circuits.csv", doc));
            Assert.Contains(plan, l => l.StartsWith("0,-F2,", StringComparison.Ordinal) && l.Contains("1 × Cooker connection; 1 × Dimmer; 1 × Double socket outlet"));
        }

        [Fact]
        public void ALoadOfAnotherModuleCountsInTheOperatingCurrentOfItsBreaker()
        {
            var (doc, mcb, socket, dimmer, cooker) = Kitchen();
            var t = Solve(doc);
            // 2300 W at the nominal 230 V of the circuit: I = 10 A; the switch and the cooker without values do not count.
            Assert.Equal(230, t.Get(socket.Id, "U")!.Value!.Value, 9);
            Assert.Equal(10, t.Get(socket.Id, "I")!.Value!.Value, 9);
            Assert.Equal(10, t.Get(mcb.Id, "IB")!.Value!.Value, 9);
            Assert.Equal(2300, t.Get(mcb.Id, "P")!.Value!.Value, 9);
            Assert.Empty(t.Conflicts);
            var map = CircuitMap.Of(doc);
            Assert.Equal(new[] { socket.Id }, map.LoadsOf(mcb.Id).Select(l => l.Id));
            Assert.Equal("L1", map.PhaseOf(socket.Id));
            Assert.DoesNotContain(map.LoadsOf(mcb.Id), l => l.Id == dimmer.Id || l.Id == cooker.Id);

            // A voltage entered on the load is kept (no nominal voltage then): 2000 W at 200 V = 10 A, too.
            var at200 = Edits.Update(doc, new[] { socket.Id }, o => o.WithParam("power", "2000 W").WithParam("voltage", "200 V"));
            var t2 = Solve(at200);
            Assert.Equal(10, t2.Get(mcb.Id, "IB")!.Value!.Value, 9);
            Assert.Empty(t2.Conflicts);
        }

        [Fact]
        public void TheBreakerPhaseDecidesAndAThreePoleBreakerKeepsSinglePhaseSymbolsOnL1()
        {
            var (doc, mcb, socket, _, cooker) = Kitchen();
            var onL2 = Edits.Update(doc, new[] { mcb.Id }, o => o.WithParam("phase", "L2"));
            Assert.Equal("L2", CircuitMap.Of(onL2).PhaseOf(socket.Id));
            // On a three-pole breaker a symbol of another module is three-phase only when it says so itself (a cooker).
            var threePole = Edits.Update(doc, new[] { mcb.Id }, o => o.WithParam("poles", "3"));
            Assert.Equal("L1", CircuitMap.Of(threePole).PhaseOf(socket.Id));
            Assert.Equal("L1", CircuitMap.Of(threePole).PhaseOf(cooker.Id));
            var cooker3 = Edits.Update(threePole, new[] { cooker.Id }, o => o.WithParam("phase", "L123").WithParam("power", "6900 W"));
            Assert.Equal("L123", CircuitMap.Of(cooker3).PhaseOf(cooker.Id));
            Assert.Contains(CircuitMap.Of(cooker3).LoadsOf(mcb.Id), l => l.Id == cooker.Id && l.Phase == "L123");
        }

        [Fact]
        public void AnInstallationCableFromTheSymbolToTheBreakerDecidesTheFeed()
        {
            var mcb = Fx.Item("sw.protection.mcb", 200, 100).WithParam("ref", "-F4");
            var socket = Foreign("elec.house.socket", "Hall", "").WithParam("current", "5 A");
            var doc = Fx.Doc(mcb, socket);
            var cable = Fx.Line("sw.line.cable", doc, mcb, "2", mcb, "1") with
            {
                Type = "elec.house.cable",
                Attach = new Attachment?[] { new Attachment(socket.Id, 0, 0.5) { AnchorId = "feed" }, new Attachment(mcb.Id, 0, 0.5) { AnchorId = "2" } },
            };
            doc = Edits.Add(doc, doc.Pages[0].Id, cable);
            var plan = Lines(Run("sw.circuits.csv", doc));
            Assert.Contains(plan, l => l.StartsWith("0,-F4,", StringComparison.Ordinal) && l.EndsWith("Hall,1 × Single socket outlet", StringComparison.Ordinal));
            Assert.Equal(5, Solve(doc).Get(mcb.Id, "IB")!.Value!.Value, 9);
        }

        [Fact]
        public void DocumentsWithRoomDevicesOfThisModuleKeepTheirReportsAndValues()
        {
            // The same sample board with and without the canonical modules registered gives the same reports and values.
            var doc = ExportTests.Sample();
            var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
            foreach (var id in new[] { "sw.installation.csv", "sw.installation.md", "sw.circuits.csv", "sw.circuits.md" })
            {
                var alone = Exports.All(Fx.Reg).Single(x => x.Id == id).Export(Fx.Reg, doc, objects, Lang.En);
                var with = Exports.All(Reg).Single(x => x.Id == id).Export(Reg, doc, objects, Lang.En);
                Assert.Equal(alone, with);
            }
            var loads = Edits.Update(doc, objects.Where(o => o.Type.StartsWith("sw.house.", StringComparison.Ordinal)).Select(o => o.Id).ToArray(), o => o.WithParam("power", "1150 W"));
            var a = ValueSolver.Solve(loads, Fx.Reg, null, new ValueSolveOptions { BudgetMs = 10000 });
            var b = Solve(loads);
            var f2 = objects.Single(o => o.Str("ref", "") == "-F2");
            Assert.Equal(15, a.Get(f2.Id, "IB")!.Value!.Value, 9);   // three sockets × 5 A
            Assert.Equal(a.Get(f2.Id, "IB")!.Value, b.Get(f2.Id, "IB")!.Value);
            Assert.False(Board.House(objects.First(o => o.Type == "sw.house.socket"))!.IsForeign);
        }
    }
}
