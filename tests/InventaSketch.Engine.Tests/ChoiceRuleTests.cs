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

        /// <summary>Step actions ("+1", "one more"): they set the next value, not an option.</summary>
        private static readonly System.Text.RegularExpressions.Regex StepId = new(@"\.(inc|dec|more|fewer|less|up|down|next|prev)$");

        /// <summary>
        /// The parameters a plain action sets to a fixed value: the parameters it changes, provided applying it a second
        /// time changes none of them again (steps such as "+1" and toggles change them again; they are no options).
        /// Null when the action changes no parameter or is not idempotent.
        /// </summary>
        private static Dictionary<string, string>? FixedValues(ContextAction a, SketchObject o)
        {
            if (a.Input is not null || a.ApplyInDocument is not null || !a.IsEnabled) return null;
            SketchObject once, twice;
            try { once = a.Apply(o); twice = a.Apply(once); } catch { return null; }
            var changed = once.Params.Keys.Concat(o.Params.Keys).Distinct(StringComparer.Ordinal)
                .Where(k => !(o.Params.TryGetValue(k, out var x) && once.Params.TryGetValue(k, out var y) && Equals(x, y)))
                .ToList();
            if (changed.Count == 0) return null;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in changed)
            {
                var has1 = once.Params.TryGetValue(k, out var v1);
                var has2 = twice.Params.TryGetValue(k, out var v2);
                if (has1 != has2 || (has1 && !Equals(v1, v2))) return null;
                result[k] = has1 ? v1.ToString() ?? "" : "(removed)";
            }
            return result;
        }

        /// <summary>
        /// The radio pattern (maintainer 2026-11-25): two or more enabled actions of one element (or one part) that each set
        /// the same parameter to a different fixed value are options of one choice and belong to one <see cref="ActionChoice"/>
        /// (one dropdown), not separate buttons. Checked for every element in its default state, after each plain action
        /// and for each of its parts.
        /// <para>
        /// An action sets a fixed value when it sets the parameter to the same value however the element looks: applied twice
        /// it changes nothing more (no step, no toggle), and the action of the same id offered once another option is chosen
        /// sets the same value (row and column edits, which compute the new content from the current one, do not).
        /// The option that is already current changes nothing; it is seen once another option of the set has been chosen.
        /// </para>
        /// </summary>
        [Fact]
        public void OptionsSettingOneParameterFormAChoice()
        {
            var bad = new List<string>();
            static List<ContextAction> Plain(IEnumerable<ContextAction> actions)
                => actions.Where(a => a.Choice is null && a.IsEnabled && a.Input is null && a.ApplyInDocument is null
                    && !IconIds.IsStep(a.Icon) && !StepId.IsMatch(a.Id)).ToList();
            static string? Value(SketchObject x, string key) => x.Params.TryGetValue(key, out var v) ? v.ToString() : null;

            // actionsOf: the action list (of the element or of one part) of a state of the element.
            void Check(string type, string scope, SketchObject o, Func<SketchObject, IEnumerable<ContextAction>> actionsOf)
            {
                List<ContextAction> plain;
                try { plain = Plain(actionsOf(o)); } catch { return; }
                var byKey = new Dictionary<string, HashSet<(string Id, string Value)>>(StringComparer.Ordinal);
                void Note(string key, string id, string value)
                {
                    if (!byKey.TryGetValue(key, out var l)) byKey[key] = l = new HashSet<(string, string)>();
                    l.Add((id, value));
                }
                foreach (var a in plain)
                {
                    if (FixedValues(a, o) is not { } set) continue;
                    SketchObject after;
                    List<ContextAction> plainAfter;
                    try { after = a.Apply(o); plainAfter = Plain(actionsOf(after)); } catch { continue; }
                    // a offered in the new state, applied to the old one, must set the same values.
                    var again = plainAfter.FirstOrDefault(x => x.Id == a.Id);
                    SketchObject back;
                    try { back = again?.Apply(o) ?? o; } catch { continue; }
                    var keys = set.Where(kv => again is not null && Value(back, kv.Key) == kv.Value).Select(kv => kv.Key).ToList();
                    if (keys.Count == 0) continue;
                    foreach (var b in plainAfter.Where(x => x.Id != a.Id))
                    {
                        if (FixedValues(b, after) is not { } other) continue;
                        // b as offered before must set the same value in the new state.
                        var before = plain.FirstOrDefault(x => x.Id == b.Id);
                        SketchObject viaBefore;
                        try { viaBefore = before?.Apply(after) ?? after; } catch { continue; }
                        foreach (var k in keys.Where(other.ContainsKey))
                            if (before is not null && Value(viaBefore, k) == other[k])
                            {
                                Note(k, a.Id, set[k]);
                                Note(k, b.Id, other[k]);
                            }
                    }
                }
                foreach (var kv in byKey)
                {
                    if (kv.Value.Select(x => x.Value).Distinct().Count() < 2 || kv.Value.Select(x => x.Id).Distinct().Count() < 2) continue;
                    bad.Add($"{type} ({scope}): '{kv.Key}' set by separate actions {string.Join(", ", kv.Value.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => $"{x.Id}={x.Value}"))} (radio pattern, use an ActionChoice)");
                }
            }
            static RenderContext Ctx(SketchObject x)
            {
                var doc = SketchDocument.Create(new Size2(1000, 800));
                return new RenderContext(Edits.Add(doc, doc.Pages[0].Id, x), Lang.En);
            }
            foreach (var item in Reg.AllItems)
            {
                SketchObject o;
                try { o = Reg.Create(item, new Vec2(500, 400)); } catch { continue; }
                var type = Reg.Type(o.Type);
                if (type is null) continue;
                void All(SketchObject x, string scope)
                {
                    Check(type.Id, item.Id + " " + scope, x, y => type.Actions(y, Ctx(y)).ToList());
                    List<string> parts;
                    try { parts = type.Parts(x, Ctx(x)).Select(p => p.Key).ToList(); } catch { return; }
                    foreach (var part in parts)
                        Check(type.Id, item.Id + " " + scope + " part " + part, x, y => Reg.PartActions(y, part, Ctx(y)));
                }
                All(o, "default");
                List<ContextAction> first;
                try { first = type.Actions(o, Ctx(o)).Where(a => a.Input is null && a.ApplyInDocument is null).ToList(); }
                catch { continue; }
                foreach (var a in first)
                {
                    SketchObject next;
                    try { next = a.Apply(o); } catch { continue; }
                    All(next, "after " + a.Id);
                }
            }
            Report(bad);
        }
    }
}
