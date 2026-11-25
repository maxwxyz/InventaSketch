using System.Linq;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Every source a value relation or a plausibility check cites ("S7") is in the source catalogue
    /// (<see cref="ValueSources"/>, section 9 of docs/en/design/engineering-calculations.md), so the calculation sheet
    /// and the value card can name it in full (Calc 6).
    /// </summary>
    public class ValueSourceTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void EveryCitedSourceIsInTheCatalogue()
        {
            var cited = Reg.Types
                .SelectMany(t => ValueModel.Relations(t).Select(r => r.Source)
                    .Concat((t as IValueChecks)?.ValueChecks.Select(c => c.Source) ?? Enumerable.Empty<string?>()))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim().Trim('[', ']'))
                .Distinct()
                .ToList();
            Assert.NotEmpty(cited);
            var missing = cited.Where(s => ValueSources.Get(s) is null).ToList();
            Assert.True(missing.Count == 0, "Not in ValueSources: " + string.Join(", ", missing));
        }

        [Fact]
        public void SourcesAreBilingual()
        {
            foreach (var id in ValueSources.Ids)
            {
                var t = ValueSources.Get(id)!;
                Assert.False(string.IsNullOrWhiteSpace(t.En), id);
                Assert.False(string.IsNullOrWhiteSpace(t.De), id);
            }
            Assert.Equal(ValueSources.Get("S7"), ValueSources.Get("[S7]"));
            Assert.Null(ValueSources.Get("S999"));
        }
    }
}
