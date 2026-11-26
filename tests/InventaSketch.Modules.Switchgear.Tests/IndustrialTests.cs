using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// The industrial devices (motor protection, overload relay, NH fuse switch-disconnector, frequency inverter, PLC
    /// modules, safety relay) and the board accessories (comb busbar, blanking plate): size, pins, views, actions, and the
    /// five reports with all of them on the page.
    /// </summary>
    public class IndustrialTests
    {
        private static readonly Registry Reg = Fx.Reg;

        private static List<string?> Ports(SketchObject o)
            => Reg.Type(o.Type)!.Anchors(o, Fx.Ctx(Fx.Doc(o))).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

        private static string Texts(SketchObject o) => string.Join("|", Fx.Local(o).OfType<TextPrim>().Select(t => t.Text));

        [Fact]
        public void NewTypesAreRegisteredWithBilingualNamesAndCategories()
        {
            var expected = new Dictionary<string, string>
            {
                ["sw.protection.mpcb"] = "Motorschutzschalter",
                ["sw.protection.overload"] = "Überlastrelais (thermisch)",
                ["sw.fuse.nh"] = "NH-Sicherungslasttrenner",
                ["sw.drive.vfd"] = "Frequenzumrichter",
                ["sw.plc.module"] = "SPS-Baugruppe (Hutschiene)",
                ["sw.safety.relay"] = "Sicherheitsrelais (Hutschiene)",
                ["sw.busbar.comb"] = "Phasenschiene (Kammschiene)",
                ["sw.blank"] = "Abdeckstreifen",
            };
            foreach (var kv in expected)
            {
                var t = Reg.Type(kv.Key);
                Assert.NotNull(t);
                Assert.Equal(kv.Value, t!.Name.Get(Lang.De));
                Assert.Contains(Reg.Library, i => i.Id == kv.Key && i.TypeId == kv.Key);
                Assert.Contains(Reg.Categories, c => c.Id == t.Category);
                Assert.NotEmpty(t.Keywords);
            }
            Assert.Equal(SwCategories.Automation, Reg.Type("sw.drive.vfd")!.Category);
            Assert.Contains(Reg.Categories, c => c.Id == SwCategories.Automation && c.Name.Get(Lang.De).StartsWith("Antriebe", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("sw.protection.mpcb", 3)]
        [InlineData("sw.protection.overload", 2)]
        [InlineData("sw.fuse.nh", 6)]
        [InlineData("sw.drive.vfd", 4)]
        [InlineData("sw.drive.vfd.4kw", 5)]
        [InlineData("sw.plc.module", 2)]
        [InlineData("sw.plc.module.cpu", 4)]
        [InlineData("sw.safety.relay", 2)]
        public void IndustrialDevicesAreWholeModulesWide(string itemId, int modules)
        {
            var o = Fx.Item(itemId);
            Assert.Equal(modules * Modular.Unit, o.Size.W, 6);
            Assert.Equal(Modular.DeviceHeight, o.Size.H, 6);
        }

        [Fact]
        public void PinsFollowTheUsualMarkings()
        {
            Assert.Equal(new[] { "1", "2", "3", "4", "5", "6" }, Ports(Fx.Item("sw.protection.mpcb")));
            Assert.Equal(new[] { "1", "2", "3", "4", "5", "6", "95", "96", "97", "98" }, Ports(Fx.Item("sw.protection.overload")));
            Assert.Equal(new[] { "1", "2", "3", "4", "5", "6" }, Ports(Fx.Item("sw.fuse.nh")));
            Assert.Equal(new[] { "L1", "L2", "L3", "U", "V", "W" }, Ports(Fx.Item("sw.drive.vfd")));
            Assert.Equal(new[] { "L", "N", "U", "V", "W" }, Ports(Fx.Item("sw.drive.vfd.1ph")));
            Assert.Equal(new[] { "A1", "A2", "S11", "S12", "S21", "S22", "13", "14", "23", "24" }.OrderBy(x => x, StringComparer.Ordinal), Ports(Fx.Item("sw.safety.relay")));
            var di = Ports(Fx.Item("sw.plc.module"));
            Assert.Equal(10, di.Count);
            Assert.Contains("L+", di);
            Assert.Contains("I7", di);
            Assert.Contains("Q15", Ports(Reg.SetParam(Fx.Item("sw.plc.module.do"), "channels", "16")));
            // The CPU's network port is a data port (bus cable connector), the supply pins are electric.
            var cpu = Fx.Item("sw.plc.module.cpu");
            var anchors = Reg.Type(cpu.Type)!.Anchors(cpu, Fx.Ctx(Fx.Doc(cpu))).Where(a => a.Role == AnchorRole.Port).ToList();
            Assert.Equal(PortMedia.Data, Reg.Type(cpu.Type)!.Connection(cpu, anchors.Single(a => a.Id == "X1"))!.Medium);
            Assert.Equal(PortMedia.Electric, Reg.Type(cpu.Type)!.Connection(cpu, anchors.Single(a => a.Id == "L+"))!.Medium);
        }

        [Fact]
        public void SymbolViewKeepsThePinIds()
        {
            foreach (var id in new[] { "sw.protection.mpcb", "sw.protection.overload", "sw.fuse.nh", "sw.drive.vfd", "sw.plc.module", "sw.plc.module.cpu", "sw.safety.relay" })
            {
                var o = Fx.Item(id);
                var sym = Reg.WithView(o, DeviceType.SymbolView, Fx.Ctx(Fx.Doc(o)));
                Assert.Equal(Ports(o), Ports(sym));
                Assert.Contains(Fx.Local(sym), p => p.Tag == SwTags.Symbol);
            }
        }

        [Fact]
        public void FrontViewsShowRatingsAndKinds()
        {
            Assert.Contains("4 A", Texts(Fx.Item("sw.protection.mpcb")));
            Assert.Contains("10 A", Texts(Fx.Item("sw.protection.mpcb.10")));
            Assert.Contains("95 96 97 98", Texts(Fx.Item("sw.protection.overload")));
            var nh = Texts(Fx.Item("sw.fuse.nh"));
            Assert.Contains("NH00", nh);
            Assert.Contains("63 A", nh);
            Assert.Contains("NH000", Texts(Fx.Item("sw.fuse.nh.000.35")));
            Assert.Contains("0.75 kW 3~", Texts(Fx.Item("sw.drive.vfd")));
            Assert.Contains("DI 8", Texts(Fx.Item("sw.plc.module")));
            Assert.Contains("CPU", Texts(Fx.Item("sw.plc.module.cpu")));
            Assert.Contains("24 V DC", Texts(Fx.Item("sw.safety.relay")));
            Assert.Equal("NH00 63 A", Board.Rating(Fx.Item("sw.fuse.nh")));
        }

        [Fact]
        public void SwitchingTheMotorProtectionTurnsTheHandle()
        {
            var q = Fx.Item("sw.protection.mpcb");
            var toggle = Reg.Type(q.Type)!.Actions(q, Lang.En).Single(a => a.Id == "sw.on");
            var off = toggle.Apply(q);
            Assert.False(off.Flag("on", true));
            // The handle bar: upright when on (I), across when off (O).
            Rect Bar(SketchObject x) => Prim.Bounds(new[] { Fx.Local(x).Last(p => p.Tag == SwTags.Lever) })!.Value;
            Assert.True(Bar(q).H > Bar(q).W);
            Assert.True(Bar(off).W > Bar(off).H);
        }

        [Fact]
        public void CombBusbarHasATeethPortPerModuleAndNoMounts()
        {
            var comb = Fx.Item("sw.busbar.comb");
            Assert.Equal(12 * Modular.Unit, comb.Size.W, 6);
            Assert.Equal(12, comb.Size.H, 6);
            var anchors = Reg.Type(comb.Type)!.Anchors(comb, Fx.Ctx(Fx.Doc(comb))).ToList();
            Assert.DoesNotContain(anchors, a => a.Role == AnchorRole.Mount);
            var ports = anchors.Where(a => a.Role == AnchorRole.Port).OrderBy(a => a.P.X).ToList();
            Assert.Equal(12, ports.Count);
            // Teeth at the module centres, on the bottom edge: they meet the top pins of single-module devices.
            Assert.Equal(-comb.Size.W / 2 + Modular.Unit / 2, ports[0].P.X, 6);
            Assert.All(ports, p => Assert.Equal(comb.Size.H / 2, p.P.Y, 6));
            Assert.Equal(new[] { "L1", "L2", "L3", "L1" }, Fx.Local(comb).OfType<TextPrim>().Select(t => t.Text).Take(4));
            Assert.Equal(new[] { "L", "N" }, Fx.Local(Fx.Item("sw.busbar.comb.1n")).OfType<TextPrim>().Select(t => t.Text).Take(2));
            Assert.Single(Reg.Type(comb.Type)!.Views);
        }

        [Fact]
        public void CountActionsResizeAndKeepTheLeftEdge()
        {
            foreach (var (item, id, key, before) in new[] { ("sw.busbar.comb", "sw.comb.positions", "positions", 12), ("sw.blank", "sw.blank.modules", "modules", 1) })
            {
                var o = Fx.Item(item, 200, 100);
                var left = o.Transform.X - o.Size.W / 2;
                var actions = Reg.Type(o.Type)!.Actions(o, Lang.En).ToList();
                var more = actions.Single(a => a.Id == id + ".more");
                var fewer = actions.Single(a => a.Id == id + ".fewer");
                Assert.Equal(IconIds.CountMore, more.Icon);
                var bigger = more.Apply(o);
                Assert.Equal(before + 1, bigger.Int(key, 0));
                Assert.Equal((before + 1) * Modular.Unit, bigger.Size.W, 6);
                Assert.Equal(left, bigger.Transform.X - bigger.Size.W / 2, 6);
                Assert.Equal(before == 1, !fewer.IsEnabled);
            }
            var six = Fx.Item("sw.blank.6");
            Assert.Equal(6 * Modular.Unit, six.Size.W, 6);
            Assert.Equal(45, six.Size.H, 6);
            Assert.Empty(Ports(six));
            Assert.Contains(Reg.Type(six.Type)!.Anchors(six, Fx.Ctx(Fx.Doc(six))), a => a.Role == AnchorRole.Mount);
        }

        /// <summary>The export sample plus every new device: on the rail (blank, NH, MPCB, overload, VFD, PLC, safety relay) and a comb busbar above.</summary>
        private static SketchDocument SampleWithIndustrial()
        {
            var doc = ExportTests.Sample();
            var rail = Fx.Item("sw.rail.ts35.24", 300, 250).WithParam("ref", "Row 2");
            var grid = RailType.Grid(rail);
            double X(int module, double width) => 300 + grid[0] + module * Modular.Unit + width / 2;
            var nh = Fx.Item("sw.fuse.nh", X(0, 105), 250).WithParam("ref", "-F10");
            var q = Fx.Item("sw.protection.mpcb", X(6, 52.5), 250).WithParam("ref", "-Q2").WithParam("circuit", "Pump");
            var ol = Fx.Item("sw.protection.overload", X(9, 35), 250).WithParam("ref", "-F11");
            var blank = Fx.Item("sw.blank", X(11, 17.5), 250);
            var vfd = Fx.Item("sw.drive.vfd", X(12, 70), 250).WithParam("ref", "-T1").WithParam("feed", "-Q2");
            var plc = Fx.Item("sw.plc.module", X(16, 35), 250).WithParam("ref", "-K10");
            var safety = Fx.Item("sw.safety.relay", X(18, 35), 250).WithParam("ref", "-K11");
            var comb = Fx.Item("sw.busbar.comb", X(0, 210), 250 - 45 - 6);
            return Edits.Add(doc, doc.Pages[0].Id, rail, nh, q, ol, blank, vfd, plc, safety, comb);
        }

        [Fact]
        public void AllFiveReportsRunWithTheNewDevices()
        {
            var doc = SampleWithIndustrial();
            var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
            foreach (var e in Exports.All(Reg).Where(x => x.Id.StartsWith("sw.", StringComparison.Ordinal)))
                foreach (var lang in new[] { Lang.En, Lang.De })
                    if (e.Applies(Reg, doc, objects)) Assert.False(string.IsNullOrEmpty(e.Export(Reg, doc, objects, lang)), e.Id);

            string Run(string id) => Exports.All(Reg).Single(x => x.Id == id).Export(Reg, doc, objects, Lang.En);
            var layout = Run("sw.layout.csv").Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            Assert.Contains("Row 2,1,1,6,-F10,NH fuse switch-disconnector,NH00 63 A,", layout);
            Assert.Contains("Row 2,2,7,3,-Q2,Motor protection circuit breaker,4 A,Pump", layout);
            Assert.Contains(layout, l => l.StartsWith("Row 2,4,13,4,-T1,Frequency inverter,0.75 kW,", StringComparison.Ordinal));
            Assert.Contains(layout, l => l.Contains(",-K10,PLC module (DIN rail),DI 8,"));
            Assert.Contains(layout, l => l.Contains(",-K11,Safety relay (DIN rail),24 V DC,"));
            // Blanking plates and comb busbars are accessories: not listed, not counted as used modules.
            Assert.DoesNotContain(layout, l => l.Contains("Blanking") || l.Contains("Comb"));
            Assert.Contains("Modules used: 19 / 24", Run("sw.layout.md"));
            // The inverter hangs below its motor protection in the circuit plan.
            var circuits = Run("sw.circuits.csv").Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            Assert.Contains(circuits, l => l.Contains(",-Q2,Motor protection circuit breaker,4 A,") && l.EndsWith("1 × Frequency inverter", StringComparison.Ordinal));
            Assert.Contains(circuits, l => l.Contains(",-F10,NH fuse switch-disconnector,NH00 63 A,"));
        }

        [Fact]
        public void LabellingStripSkipsTheBlankingPlate()
        {
            var (rail, blank, mcb) = (Fx.Item("sw.rail.ts35", 200, 100), Fx.Item("sw.blank", 200 - 70 + 8.75, 100), Fx.Item("sw.protection.mcb", 200 - 70 + 17.5 + 8.75, 100).WithParam("ref", "-F7"));
            var strip = Fx.Item("sw.label.strip", 200, 100 - 45 - 8);
            var doc = Fx.Doc(rail, blank, mcb, strip);
            var texts = Reg.Render(doc.Get(strip.Id)!, Fx.Ctx(doc)).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Equal(new[] { "-F7", "B16" }, texts);
        }
    }
}
