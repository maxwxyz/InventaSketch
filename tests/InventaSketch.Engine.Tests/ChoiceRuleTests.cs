using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// "A choice among several options is always a dropdown, never a cycle button" (maintainer 2026-11-25,
    /// docs/en/design/interaction-ux.md section 5a) for every element of every module: no action steps through variants
    /// (<see cref="IconIds.Cycle"/>, "Next …" labels), every choice has at least two options with exactly one current (menus
    /// excepted), and a choice over a parameter the hosts already show as a dropdown names it in
    /// <see cref="ContextAction.ParamKeys"/> so that it is shown in one place only.
    /// </summary>
    public class ChoiceRuleTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public ChoiceRuleTests(ITestOutputHelper output) => _out = output;

        private void Report(List<string> bad)
        {
            bad = bad.Distinct().ToList();
            foreach (var b in bad) _out.WriteLine(b);
            Assert.Empty(bad);
        }

        [Fact]
        public void NoActionStepsThroughVariants()
        {
            var bad = new List<string>();
            foreach (var list in IconHygieneTests.Lists())
                foreach (var a in list.En)
                {
                    if (a.Icon == IconIds.Cycle) bad.Add($"{list.Type} ({list.Scope}): {a.Id} '{a.Label.En}' uses the cycle pictogram");
                    var de = list.De.FirstOrDefault(d => d.Id == a.Id)?.Label.Get(Lang.De) ?? "";
                    if (a.Label.En.StartsWith("Next ", StringComparison.Ordinal) || de.StartsWith("Nächst", StringComparison.Ordinal))
                        bad.Add($"{list.Type} ({list.Scope}): {a.Id} '{a.Label.En}' / '{de}' steps to the next variant");
                }
            Report(bad);
        }

        [Fact]
        public void EveryChoiceHasOptionsAndOneCurrent()
        {
            var bad = new List<string>();
            foreach (var list in IconHygieneTests.Lists())
                foreach (var (choice, options) in ActionChoices.Group(list.En))
                {
                    if (options.Select(a => a.Id).Distinct().Count() != options.Count)
                        bad.Add($"{list.Type} ({list.Scope}): choice {choice.Key} repeats an option");
                    if (options.Any(a => !a.Id.StartsWith(choice.Key + ActionChoices.Separator, StringComparison.Ordinal)))
                        bad.Add($"{list.Type} ({list.Scope}): choice {choice.Key} has an option id not of the form key=value");
                    if (choice.IsMenu) continue;
                    if (options.Count < 2) bad.Add($"{list.Type} ({list.Scope}): choice {choice.Key} has {options.Count} option(s)");
                    var current = options.Count(a => a.IsActive);
                    // A part's line style shows no option current while the part keeps the type's own dash (PartLooks).
                    var partOwnLook = list.Scope.Contains(" part ") && current == 0;
                    if (current != 1 && !partOwnLook) bad.Add($"{list.Type} ({list.Scope}): choice {choice.Key} has {current} current option(s)");
                    if (choice.Label.En.Length == 0 || string.IsNullOrEmpty(choice.Label.De))
                        bad.Add($"{list.Type} ({list.Scope}): choice {choice.Key} has no bilingual label");
                }
            Report(bad);
        }

        [Fact]
        public void ChoicesOverShownParametersNameThem()
        {
            var bad = new List<string>();
            foreach (var item in Reg.AllItems)
            {
                SketchObject o;
                try { o = Reg.Create(item, new Vec2(500, 400)); } catch { continue; }
                var type = Reg.Type(o.Type);
                if (type is null) continue;
                // The parameters hosts show as dropdowns (OnSlideController.BarChoices, the web context bar).
                var shown = new HashSet<string>(type.Parameters.Where(p => p.Kind == ParamKind.Choice && p.Options is { Count: > 1 } && !p.Advanced).Select(p => p.Key));
                List<ContextAction> actions;
                var doc = SketchDocument.Create(new Size2(1000, 800));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                try { actions = type.Actions(o, new RenderContext(doc, Lang.En)).ToList(); } catch { continue; }
                foreach (var a in actions.Where(a => a.Choice is { IsMenu: false } && a.Input is null && a.ApplyInDocument is null))
                {
                    SketchObject next;
                    try { next = a.Apply(o); } catch { continue; }
                    var changed = next.Params.Keys.Concat(o.Params.Keys).Distinct()
                        .Where(k => !(o.Params.TryGetValue(k, out var x) && next.Params.TryGetValue(k, out var y) && Equals(x, y)));
                    foreach (var k in changed.Where(shown.Contains))
                        if (a.ParamKeys is null || !a.ParamKeys.Contains(k))
                            bad.Add($"{type.Id}: option {a.Id} sets the shown choice parameter '{k}' without naming it (two places for one choice)");
                }
            }
            Report(bad);
        }
    }
}
