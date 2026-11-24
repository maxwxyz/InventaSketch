using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Building services switch plan ↔ section through the generic assembly view switch.</summary>
    public class BuildingServicesViewTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void AnInstallationGoesToSectionAndBack()
        {
            var basin = Reg.Library.First(i => i.TypeId == "bs.san.basin" && (i.View ?? "plan") == "plan");
            var wc = Reg.Library.First(i => i.TypeId == "bs.san.wc" && (i.View ?? "plan") == "plan");
            var a = Reg.Create(basin, new Vec2(40, 40));
            var b = Reg.Create(wc, new Vec2(90, 40));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, a, b);
            var ids = new[] { a.Id, b.Id };
            var section = AssemblyViews.Switch(Reg, doc, ids, "section");
            Assert.All(ids, id => Assert.Equal("section", section.Get(id)!.View));
            // x stays, the plan comes back exactly.
            Assert.Equal(a.Transform.X, section.Get(a.Id)!.Transform.X, 6);
            var plan = AssemblyViews.Switch(Reg, section, ids, "plan");
            Assert.Equal(a.Transform.Y, plan.Get(a.Id)!.Transform.Y, 6);
            Assert.Equal(b.Transform.Y, plan.Get(b.Id)!.Transform.Y, 6);
            Assert.Contains(Arrange.CommonViews(Reg, new[] { a, b }), v => v.Id == "section");
        }
    }
}
