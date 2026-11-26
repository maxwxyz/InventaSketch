using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Combine what overlaps: elements offered by several modules (house installation devices of electrical, switchgear and
    /// building services; the AHU fan of the process module) appear once in galleries and search, the others give way
    /// (<see cref="LibraryItem.Alternative"/>), and documents that use the hidden items keep loading and rendering.
    /// </summary>
    public class DuplicateItemsTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;
        public DuplicateItemsTests(ITestOutputHelper output) => _out = output;

        /// <summary>Hidden item → canonical item (the chosen mapping).</summary>
        public static readonly IReadOnlyDictionary<string, string> Hidden = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sw.house.socket"] = "elec.house.socket",
            ["sw.house.socket.double"] = "elec.house.socket.double",
            ["sw.house.switch"] = "elec.house.switch",
            ["sw.house.switch.twoway"] = "elec.house.switch.twoway",
            ["sw.house.switch.push"] = "elec.house.switch.push",
            ["sw.house.light"] = "elec.house.light",
            ["sw.house.appliance"] = "bs.elec.appliance",
            ["sw.house.appliance.washer"] = "bs.elec.appliance.washer",
            ["process.hvac.fan"] = "process.fan",
        };

        /// <summary>Types of different modules that draw the same element (type id → meaning).</summary>
        private static readonly IReadOnlyDictionary<string, string> SameElement = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elec.house.socket"] = "socket outlet", ["sw.house.socket"] = "socket outlet",
            ["elec.house.switch"] = "light switch", ["sw.house.switch"] = "light switch",
            ["elec.house.light"] = "light outlet", ["sw.house.light"] = "light outlet",
            ["bs.elec.appliance"] = "appliance outlet", ["sw.house.appliance"] = "appliance outlet",
        };

        /// <summary>What an item means: the element (shared across modules where they overlap), its preset parameters and view.</summary>
        private static string Meaning(LibraryItem i)
        {
            var element = SameElement.TryGetValue(i.TypeId, out var m) ? m : i.TypeId;
            var ps = string.Join(";", (i.Params ?? new Dictionary<string, ParamValue>()).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value));
            var view = i.View;
            if (view is null || view == Reg.Type(i.TypeId)?.DefaultView) view = "default";
            return element + "|" + ps + "|" + view;
        }

        [Fact]
        public void HiddenItemsGiveWayToTheirCanonicalItemAndStayFoundById()
        {
            foreach (var (hidden, canonical) in Hidden)
            {
                Assert.DoesNotContain(Reg.Library, i => i.Id == hidden);
                Assert.Contains(Reg.Library, i => i.Id == canonical);
                Assert.Equal(canonical, Reg.Item(hidden)?.Alternative);   // still found by id (recent lists, existing slides)
                Assert.NotNull(Reg.Type(Reg.Item(hidden)!.TypeId));       // the type stays registered
            }
            // The wall light outlet has no counterpart and stays.
            Assert.Contains(Reg.Library, i => i.Id == "sw.house.light.wall");
        }

        [Fact]
        public void ModulesOnTheirOwnKeepTheirItems()
        {
            var sw = new Registry().Register(new InventaSketch.Modules.Switchgear.SwitchgearModule());
            foreach (var id in Hidden.Keys.Where(k => k.StartsWith("sw.", StringComparison.Ordinal)))
                Assert.Contains(sw.Library, i => i.Id == id);
            Assert.Contains(sw.Categories, c => c.Id == "sw.house");
        }

        [Theory]
        [InlineData("socket", Lang.En, "socket outlet")]
        [InlineData("Steckdose", Lang.De, "socket outlet")]
        [InlineData("switch", Lang.En, "light switch")]
        [InlineData("Schalter", Lang.De, "light switch")]
        [InlineData("light", Lang.En, "light outlet")]
        [InlineData("Herd", Lang.De, "appliance outlet")]
        [InlineData("fan", Lang.En, "process.fan")]
        [InlineData("Ventilator", Lang.De, "process.fan")]
        [InlineData("motor", Lang.En, "process.motor")]
        [InlineData("Motor", Lang.De, "process.motor")]
        public void SearchReturnsOneItemPerMeaning(string query, Lang lang, string element)
        {
            var hits = LibrarySearch.Find(Reg, query, lang, max: 500);
            // No element is offered by two modules. (Within a module one symbol may carry two names for two uses, e.g. a
            // switch contact and a relay contact; those are told apart by name below.)
            var moduleOf = Reg.Categories.ToDictionary(c => c.Id, c => c.ModuleId);
            foreach (var g in hits.GroupBy(Meaning).Where(g => g.Count() > 1))
                _out.WriteLine("SAME MEANING " + g.Key + ": " + string.Join(", ", g.Select(i => i.Id)));
            Assert.All(hits.GroupBy(Meaning), g => Assert.Single(g.Select(i => moduleOf[i.Category]).Distinct()));
            // The overlapping element is found, once in its default form, and never from a module that gives way.
            Assert.Single(hits, i => Meaning(i).StartsWith(element + "||default", StringComparison.Ordinal));
            Assert.DoesNotContain(hits, i => Hidden.ContainsKey(i.Id));
            // Search tells the remaining items apart by their names.
            foreach (var g in hits.GroupBy(i => LibrarySearch.Fold(i.Name.Get(lang))).Where(g => g.Count() > 1))
                _out.WriteLine("SAME NAME " + g.Key + ": " + string.Join(", ", g.Select(i => i.Id)));
            Assert.All(hits.GroupBy(i => LibrarySearch.Fold(i.Name.Get(lang))), g => Assert.Single(g));
        }

        [Fact]
        public void TheCanonicalItemsAnswerToTheNamesOfTheItemsThatGaveWay()
        {
            foreach (var (hidden, canonical) in Hidden)
            {
                var item = Reg.Item(hidden)!;
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    // The distinctive word of the old name ("Zweifachsteckdose", "Deckenauslass", "Herdanschluss") finds the canonical item.
                    var word = item.Name.Get(lang).Split(new[] { ' ', '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    Assert.Contains(LibrarySearch.Find(Reg, word, lang, max: 500), i => i.Id == canonical);
                }
            }
        }

        [Fact]
        public void TheAirHandlingAndDriveSymbolsAreNamedApartFromTheirLookalikes()
        {
            // Not merged (they differ in purpose), but search shows which is which.
            Assert.Contains("AHU", Reg.Item("process.hvac.damper")!.Name.En);
            Assert.Contains("RLT", Reg.Item("process.hvac.damper")!.Name.De);
            Assert.Contains("P&ID", Reg.Item("process.motor")!.Name.En);
            Assert.NotNull(Reg.Library.FirstOrDefault(i => i.Id == "bs.vent.damper"));
            Assert.NotNull(Reg.Library.FirstOrDefault(i => i.Id == "process.hvac.damper"));
            Assert.NotNull(Reg.Library.FirstOrDefault(i => i.Id == "machines.block.motor"));
            Assert.NotNull(Reg.Library.FirstOrDefault(i => i.Id == "process.motor"));
            var damper = LibrarySearch.Find(Reg, "damper", Lang.En, max: 100).Select(i => i.Name.En).ToList();
            Assert.Equal(damper.Count, damper.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void ADocumentWithHiddenItemsStillLoadsAndRenders()
        {
            var doc = SketchDocument.Create();
            var objs = Hidden.Keys.Select((id, n) => Reg.Create(Reg.Item(id)!, new Vec2(30 + n % 5 * 30, 30 + n / 5 * 30))
                .WithParam("room", "Kitchen").WithParam("feed", "-F2")).ToArray();
            doc = Edits.Add(doc, doc.Pages[0].Id, objs);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var ctx = new RenderContext(back);
            foreach (var o in objs)
            {
                var loaded = back.Get(o.Id);
                Assert.NotNull(loaded);
                Assert.Equal(o.Type, loaded!.Type);
                Assert.Equal("Kitchen", loaded.Str("room", ""));
                Assert.NotEmpty(Prim.Flatten(Reg.Render(loaded, ctx)));
            }
        }
    }
}
