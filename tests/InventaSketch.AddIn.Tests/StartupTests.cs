using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InventaSketch.AddIn;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Fast, robust start: Office must neither wait for the module library nor disable the add-in as slow.</summary>
    public class StartupTests
    {
        [Fact]
        public void ResilienceRepairsLoadBehaviorAndTheDoNotDisableEntry()
        {
            var store = new Dictionary<(string, string), int>();
            int? Read(string k, string n) => store.TryGetValue((k, n), out var v) ? v : null;
            bool registered = true;
            bool Exists(string k) => registered && k == Resilience.AddInKey;
            store[(Resilience.AddInKey, "LoadBehavior")] = 2; // Office reset it after a slow start
            var writes = Resilience.Repairs(Read, Exists);
            Assert.Equal(new[]
            {
                new RegistryWrite(Resilience.AddInKey, "LoadBehavior", 3),
                new RegistryWrite(@"Software\Microsoft\Office\16.0\PowerPoint\Resiliency\DoNotDisableAddinList", "InventaSketch.AddIn.Connect", 1),
            }, writes);
            foreach (var w in writes) store[(w.Key, w.Name)] = w.Value;
            Assert.Empty(Resilience.Repairs(Read, Exists));
            registered = false; // registered for all users only: nothing to repair here
            store.Clear();
            Assert.Empty(Resilience.Repairs(Read, Exists));
        }

        [Fact]
        public void OwnDisabledItemsAreRecognised()
        {
            Assert.True(Resilience.IsOwnDisabledItem(System.Text.Encoding.Unicode.GetBytes(@"C:\Users\x\AppData\Local\InventaSketch\AddIn\InventaSketch.AddIn.dll")));
            Assert.False(Resilience.IsOwnDisabledItem(System.Text.Encoding.Unicode.GetBytes("Slidemax.AddIn.Connect")));
            Assert.False(Resilience.IsOwnDisabledItem(null));
        }

        [Fact]
        public void RibbonCacheIsBoundToTheBuild()
        {
            var old = RibbonCache.Folder;
            var dir = Path.Combine(Path.GetTempPath(), "isk-ribbon-" + Guid.NewGuid().ToString("N"));
            try
            {
                RibbonCache.Folder = dir;
                Assert.Null(RibbonCache.Load(Lang.De));
                Assert.True(RibbonCache.Store(Lang.De, () => "<customUI/>"));
                Assert.Equal("<customUI/>", RibbonCache.Load(Lang.De));
                Assert.False(RibbonCache.Store(Lang.De, () => "<customUI/>")); // unchanged: no write
                Assert.Null(RibbonCache.Load(Lang.En));
                // A cache of another build is ignored.
                var file = RibbonCache.FileOf(Lang.De);
                File.WriteAllText(file, "<!-- isk-ribbon other-build -->\n<customUI/>");
                Assert.Null(RibbonCache.Load(Lang.De));
            }
            finally
            {
                RibbonCache.Folder = old;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void WarmingWritesAValidRibbonForBothLanguages()
        {
            var old = RibbonCache.Folder;
            var dir = Path.Combine(Path.GetTempPath(), "isk-ribbon-" + Guid.NewGuid().ToString("N"));
            try
            {
                RibbonCache.Folder = dir;
                Assert.Equal(2, Connect.WarmRibbonCache());
                foreach (var lang in new[] { Lang.De, Lang.En })
                {
                    var xml = RibbonCache.Load(lang);
                    Assert.NotNull(xml);
                    var doc = System.Xml.Linq.XDocument.Parse(xml!);
                    Assert.Contains(doc.Descendants(), e => (string?)e.Attribute("id") == "IskTab");
                    Assert.True(doc.Descendants().Count(e => e.Name.LocalName == "gallery") > 10, "galleries of the library categories");
                }
                Assert.Contains("Werkzeuge", RibbonCache.Load(Lang.De));
                Assert.Contains("Tools", RibbonCache.Load(Lang.En));
            }
            finally
            {
                RibbonCache.Folder = old;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
