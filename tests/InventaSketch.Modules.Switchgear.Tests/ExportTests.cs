using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    public class ExportTests
    {
        private static readonly Registry Reg = Fx.Reg;

        /// <summary>
        /// A small board: rail with main switch -Q1, RCD -F1, MCBs -F2 (kitchen) and -F3 (light, fed from -F1 by parameter);
        /// terminal strip -X1; rooms with sockets and a light; a wire -F2:2 → -X1:1 and a cable -W1 from -X1:1 to a socket.
        /// </summary>
        internal static SketchDocument Sample()
        {
            var rail = Fx.Item("sw.rail.ts35", 200, 100).WithParam("ref", "Row 1");
            var grid = RailType.Grid(rail);
            double X(int module, double width) => 200 + grid[0] + module * Modular.Unit + width / 2;
            var q1 = Fx.Item("sw.switch.main", X(0, 52.5), 100).WithParam("ref", "-Q1");
            var f1 = Fx.Item("sw.protection.rcd", X(3, 35), 100).WithParam("ref", "-F1");
            var f2 = Fx.Item("sw.protection.mcb", X(5, 17.5), 100).WithParam("ref", "-F2").WithParam("circuit", "Kitchen sockets").WithParam("room", "Kitchen");
            var f3 = Fx.Item("sw.protection.mcb.b10", X(6, 17.5), 100).WithParam("ref", "-F3").WithParam("circuit", "Light living").WithParam("feed", "-F1");
            var x1 = Fx.Item("sw.terminal.feedthrough", 150, 220).WithParam("ref", "-X1").WithParam("number", "1");
            var x2 = Fx.Item("sw.terminal.n", 155.2, 220).WithParam("ref", "-X1").WithParam("number", "2").WithParam("target", "Kitchen N");
            var s1 = Fx.Item("sw.house.socket.double", 300, 300).WithParam("room", "Kitchen").WithParam("feed", "-F2");
            var s2 = Fx.Item("sw.house.socket", 320, 300).WithParam("room", "Kitchen").WithParam("feed", "-F2");
            var s3 = Fx.Item("sw.house.socket", 340, 300).WithParam("room", "Kitchen").WithParam("feed", "-F2");
            var l1 = Fx.Item("sw.house.light", 360, 300).WithParam("room", "Living room").WithParam("feed", "-F3");
            var doc = Fx.Doc(rail, q1, f1, f2, f3, x1, x2, s1, s2, s3, l1);
            var wire = Fx.Line("sw.line.wire.bu", doc, f2, "2", x1, "1").WithParam("ref", "W12").WithParam("section", "2.5");
            var cable = Fx.Line("sw.line.cable", doc, x1, "2", s1, "c").WithParam("ref", "-W1").WithParam("length", 12.5);
            var cable2 = Fx.Item("sw.line.cable.5x25", 400, 300).WithParam("ref", "-W2").WithParam("from", "-X1").WithParam("to", "Cooker").WithParam("length", 8.0);
            return Edits.Add(doc, doc.Pages[0].Id, wire, cable, cable2);
        }

        private static string Run(string id, SketchDocument doc, Lang lang = Lang.En)
        {
            var e = Exports.All(Reg).Single(x => x.Id == id);
            var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
            Assert.True(e.Applies(Reg, doc, objects), id + " does not apply");
            return e.Export(Reg, doc, objects, lang);
        }

        private static List<string> Lines(string csv) => csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

        [Fact]
        public void TheModuleOffersFiveReportsInCsvAndMarkdown()
        {
            var ours = Exports.All(Reg).Where(e => e.Id.StartsWith("sw.", StringComparison.Ordinal)).ToList();
            Assert.Equal(10, ours.Count);
            Assert.Equal(5, ours.Count(e => e.Extension == "csv" && e.MimeType == "text/csv"));
            Assert.Equal(5, ours.Count(e => e.Extension == "md" && e.MimeType == "text/markdown"));
            Assert.All(ours, e => Assert.False(string.IsNullOrEmpty(e.Name.Get(Lang.De)) || string.IsNullOrEmpty(e.Preserves.Get(Lang.De))));
            var doc = Sample();
            var applying = Exports.For(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList()).Select(e => e.Id).ToList();
            Assert.Contains("sw.installation.csv", applying);
            Assert.Contains("sw.cables.md", applying);
            // A page with a lone rail and no installation has no installation list.
            var empty = Fx.Doc(Fx.Item("sw.rail.ts35"));
            Assert.DoesNotContain(Exports.For(Reg, empty, empty.ObjectsOn(empty.Pages[0]).ToList()), e => e.Id == "sw.installation.csv");
        }

        [Fact]
        public void HouseInstallationListGroupsRoomsCircuitsAndDevices()
        {
            var csv = Lines(Run("sw.installation.csv", Sample()));
            Assert.Equal("Room,Circuit,Circuit name,Protective device,Rating,Device,Designation,Quantity", csv[0]);
            Assert.Contains("Kitchen,-F2,Kitchen sockets,Miniature circuit breaker (DIN rail),B16,Single socket outlet,,2", csv);
            Assert.Contains("Kitchen,-F2,Kitchen sockets,Miniature circuit breaker (DIN rail),B16,Double socket outlet,,1", csv);
            Assert.Contains(csv, l => l.StartsWith("Living room,-F3,Light living,", StringComparison.Ordinal) && l.EndsWith(",B10,Ceiling light outlet,,1", StringComparison.Ordinal));
            var md = Run("sw.installation.md", Sample(), Lang.De);
            Assert.StartsWith("# Hausinstallationsliste", md);
            Assert.Contains("## Kitchen", md);
            Assert.Contains("- -F2 B16 – Kitchen sockets", md);
            Assert.Contains("  - 2 × Einfachsteckdose", md);
        }

        [Fact]
        public void CircuitPlanShowsTheFeedTreeAndWhereEachLinkComesFrom()
        {
            var csv = Lines(Run("sw.circuits.csv", Sample()));
            Assert.StartsWith("Level,Designation,Device,Rating,Fed from,Source", csv[0]);
            // -F2 lies right of the RCD -F1 on the rail (assumed comb busbar); -F3 names -F1 itself.
            Assert.Equal("1,-F2,Miniature circuit breaker (DIN rail),B16,-F1,rail order (assumed),Kitchen sockets,Kitchen,\"1 × Double socket outlet; 2 × Single socket outlet\"",
                csv.Single(l => l.StartsWith("1,-F2,", StringComparison.Ordinal)));
            Assert.Contains(csv, l => l.StartsWith("1,-F3,", StringComparison.Ordinal) && l.Contains(",-F1,parameter,Light living,Living room,1 × Ceiling light outlet"));
            Assert.Contains(csv, l => l.StartsWith("0,-F1,", StringComparison.Ordinal));
            Assert.Contains(csv, l => l.StartsWith("0,-Q1,", StringComparison.Ordinal));
            var md = Run("sw.circuits.md", Sample());
            Assert.Contains("- **-F1** Residual current device (DIN rail) 40 A 30 mA", md);
            Assert.Contains("  - **-F2** Miniature circuit breaker (DIN rail) B16 – Kitchen sockets _(rail order (assumed))_", md);
            Assert.Contains("    - Kitchen: 1 × Double socket outlet, 2 × Single socket outlet", md);
        }

        [Fact]
        public void WiringDecidesTheFeedWhenNoParameterIsGiven()
        {
            var a = Fx.Item("sw.protection.rcd", 100, 100).WithParam("ref", "-F10");
            var b = Fx.Item("sw.protection.mcb", 200, 100).WithParam("ref", "-F11");
            var doc = Fx.Doc(a, b);
            var wire = Fx.Line("sw.line.wire", doc, a, "2", b, "1");
            doc = Edits.Add(doc, doc.Pages[0].Id, wire);
            var csv = Lines(Run("sw.circuits.csv", doc));
            Assert.Contains(csv, l => l.StartsWith("1,-F11,", StringComparison.Ordinal) && l.Contains(",-F10,wiring,"));
        }

        [Fact]
        public void BoardLayoutListsEachRailInOrderWithModules()
        {
            var csv = Lines(Run("sw.layout.csv", Sample()));
            Assert.Equal("Rail,Position,From module,Width (modules),Designation,Device,Rating,Label", csv[0]);
            Assert.Equal("Row 1,1,1,3,-Q1,Main switch (switch-disconnector),63 A,", csv[1]);
            Assert.Equal("Row 1,2,4,2,-F1,Residual current device (DIN rail),40 A 30 mA,", csv[2]);
            Assert.Equal("Row 1,3,6,1,-F2,Miniature circuit breaker (DIN rail),B16,Kitchen sockets", csv[3]);
            Assert.Equal("Row 1,4,7,1,-F3,Miniature circuit breaker (DIN rail),B10,Light living", csv[4]);
            Assert.Contains(csv, l => l.StartsWith("(not on a rail),", StringComparison.Ordinal) && l.Contains("-X1:1"));
            var md = Run("sw.layout.md", Sample());
            Assert.Contains("## Row 1", md);
            Assert.Contains("Modules used: 7 / 12", md);
            Assert.Contains("| -Q1 | -F1 | -F2 | -F3 |", md);
            Assert.Contains("| 63 A | 40 A 30 mA | Kitchen sockets | Light living |", md);
        }

        [Fact]
        public void TerminalPlanListsWiresTargetsSectionsAndColours()
        {
            var csv = Lines(Run("sw.terminals.csv", Sample()));
            Assert.Equal("Strip,Terminal,Level,Side,Wire,Target,Cross-section (mm²),Colour,Bridge", csv[0]);
            Assert.Contains("-X1,1,,top,W12,-F2:2,2.5,BU,", csv);
            Assert.Contains(csv, l => l.StartsWith("-X1,1,,bottom,-W1 NYM-J 3×1.5,Double socket outlet (Kitchen),1.5,,", StringComparison.Ordinal));
            Assert.Contains("-X1,2,,bottom,,Kitchen N,2.5,,", csv);
            var md = Run("sw.terminals.md", Sample(), Lang.De);
            Assert.StartsWith("# Klemmenplan", md);
            Assert.Contains("## -X1", md);
            Assert.Contains("| 1 |  | oben | W12 | -F2:2 | 2.5 | BU |  |", md);
        }

        [Fact]
        public void CableListJoinsSegmentsOfOneDesignation()
        {
            var doc = Sample();
            var csv = Lines(Run("sw.cables.csv", doc));
            Assert.Equal("Designation,Type,Cores × cross-section (mm²),Length (m),From,To,Segments", csv[0]);
            Assert.Contains("-W1,NYM-J,3×1.5,12.5,-X1:1,Double socket outlet (Kitchen),1", csv);
            Assert.Contains("-W2,NYM-J,5×2.5,8,-X1,Cooker,1", csv);
            // A second segment of -W2 (e.g. on the next slide) adds its length.
            var more = Fx.Item("sw.line.cable.5x25", 450, 300).WithParam("ref", "-W2").WithParam("length", 4.0).WithParam("from", "Cooker").WithParam("to", "Hob");
            doc = Edits.Add(doc, doc.Pages[0].Id, more);
            Assert.Contains("-W2,NYM-J,5×2.5,12,-X1,Hob,2", Lines(Run("sw.cables.csv", doc)));
            var md = Run("sw.cables.md", doc);
            Assert.Contains("| -W1 | NYM-J | 3×1.5 | 12.5 | -X1:1 | Double socket outlet (Kitchen) | 1 |", md);
        }

        [Fact]
        public void LabellingStripReadsTheSampleBoard()
        {
            var doc = Sample();
            var strip = Fx.Item("sw.label.strip", 200, 47);
            doc = Edits.Add(doc, doc.Pages[0].Id, strip);
            var texts = Reg.Render(doc.Get(strip.Id)!, Fx.Ctx(doc)).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Equal(new[] { "-Q1", "63 A", "-F1", "40 A 30 mA", "-F2", "Kitchen sockets", "-F3", "Light living" }, texts);
            Assert.Contains("data-isk-type=\"sw.label.strip\"", SvgRenderer.RenderPage(Reg, doc, doc.Pages[0]));
        }

        [Fact]
        public void CsvQuotesFieldsWithSeparators()
        {
            var doc = Fx.Doc(Fx.Item("sw.house.socket").WithParam("room", "Bath, upstairs").WithParam("feed", "-F9"));
            var csv = Lines(Run("sw.installation.csv", doc));
            Assert.StartsWith("\"Bath, upstairs\",-F9,", csv[1]);
            // The feeding device is not drawn: the circuit plan says so instead of inventing it.
            var plan = Lines(Run("sw.circuits.csv", doc));
            Assert.Contains(plan, l => l.StartsWith("0,-F9,(not drawn)", StringComparison.Ordinal));
        }
    }
}
