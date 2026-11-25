using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// One choice among several options of an element or of a part (curve shape, line style, marker, scale, fill pattern …).
    /// Rule (maintainer 2026-11-25, docs/en/design/interaction-ux.md "Choices are dropdowns"): a choice among several options
    /// is never a cycle button; it is offered as one action per option (<see cref="ActionChoices.Option"/>), every option
    /// carrying the same <see cref="ActionChoice"/>, the current one marked <see cref="ContextAction.IsActive"/>. Hosts show
    /// the options together: a dropdown in the floating bar ("Curve shape: Smoothed ▾"), "Label: Current ▸" in the pie menu
    /// opening the options, the current one marked. Hosts that do not group simply list the options.
    /// </summary>
    /// <param name="Key">Stable key of the choice within its element or part ("chart.curve.shape"); option ids are "key=value".</param>
    /// <param name="Label">What is chosen ("Curve shape").</param>
    public sealed record ActionChoice(string Key, I18nText Label)
    {
        /// <summary>
        /// The options are commands rather than states (nothing is current): "Fill to ▸ curve 2 / a value …", "Add area ▸".
        /// Hosts show "Label ▸" (no current value) and put such menus with the element's commands, not with its style.
        /// </summary>
        public bool IsMenu { get; init; }
    }

    /// <summary>Building and grouping choice options (<see cref="ActionChoice"/>).</summary>
    public static class ActionChoices
    {
        /// <summary>Separator between the choice key and the option value in an option's action id ("chart.curve.shape=fitted").</summary>
        public const char Separator = '=';

        /// <summary>
        /// One option of <paramref name="choice"/>: id "key=value", the option's label, its pictogram (an <see cref="IconIds"/>
        /// id, or "" when no pictogram fits – hosts then show the label or <see cref="ContextAction.Preview"/>), current or not.
        /// </summary>
        public static ContextAction Option(ActionChoice choice, string value, I18nText label, string icon, bool current, Func<SketchObject, SketchObject> apply)
            => new(choice.Key + Separator + value, label, icon ?? "", apply) { Choice = choice, IsActive = current && !choice.IsMenu, Group = "choice:" + choice.Key };

        /// <summary>One command of a menu choice (<see cref="ActionChoice.IsMenu"/>) that asks for a text value first.</summary>
        public static ContextAction InputOption(ActionChoice choice, string value, I18nText label, string icon, I18nText prompt, string current, Func<SketchObject, string, SketchObject> apply, string? unit = null)
            => ContextAction.WithInput(choice.Key + Separator + value, label, icon ?? "", prompt, current, apply, unit) with { Choice = choice, Group = "choice:" + choice.Key };

        /// <summary>
        /// The options of a choice parameter (<see cref="ParamKind.Choice"/>) as choice options: each sets the parameter to its
        /// value (<see cref="ContextAction.ParamKeys"/> is set, so hosts that show the parameter as a dropdown already leave
        /// them out). The option pictograms of the parameter are used.
        /// </summary>
        public static IEnumerable<ContextAction> FromParam(SketchObject o, string key, ParamDef def, I18nText label, string? group = null)
        {
            var options = def.Options ?? Array.Empty<ParamOption>();
            var fallback = def.Default?.Text ?? (options.Count > 0 ? options[0].Value : "");
            var current = o.Str(def.Key, fallback);
            var choice = new ActionChoice(key, label);
            foreach (var opt in options)
            {
                var value = opt.Value;
                var a = Option(choice, value, opt.Label, opt.Icon ?? "", value == current, x => x.WithParam(def.Key, value)) with { ParamKeys = new[] { def.Key } };
                yield return group is null ? a : a with { Group = group };
            }
        }

        /// <summary>The option value of an option id ("fitted" of "chart.curve.shape=fitted"); null for other ids.</summary>
        public static string? ValueOf(string actionId)
        {
            var i = actionId.IndexOf(Separator);
            return i < 0 ? null : actionId.Substring(i + 1);
        }

        /// <summary>
        /// The choices among <paramref name="actions"/> in order of their first option, each with its options (in their order).
        /// Actions without a choice are not returned (see <see cref="Plain"/>).
        /// </summary>
        public static IReadOnlyList<(ActionChoice Choice, IReadOnlyList<ContextAction> Options)> Group(IEnumerable<ContextAction> actions)
        {
            var order = new List<string>();
            var map = new Dictionary<string, (ActionChoice Choice, List<ContextAction> Options)>(StringComparer.Ordinal);
            foreach (var a in actions)
            {
                if (a.Choice is not { } c) continue;
                if (!map.TryGetValue(c.Key, out var entry)) { entry = (c, new List<ContextAction>()); map[c.Key] = entry; order.Add(c.Key); }
                entry.Options.Add(a);
            }
            return order.Select(k => (map[k].Choice, (IReadOnlyList<ContextAction>)map[k].Options)).ToList();
        }

        /// <summary>The actions that are no choice option.</summary>
        public static IEnumerable<ContextAction> Plain(IEnumerable<ContextAction> actions) => actions.Where(a => a.Choice is null);

        /// <summary>The current option of a choice (the active one), or null (menus, or no option current).</summary>
        public static ContextAction? Current(IEnumerable<ContextAction> options) => options.FirstOrDefault(o => o.IsActive);

        /// <summary>
        /// "Label: Current" for a state choice, "Label" for a menu (what hosts show on the dropdown button or the pie entry).
        /// </summary>
        public static I18nText Caption(ActionChoice choice, IEnumerable<ContextAction> options)
        {
            if (choice.IsMenu || Current(options) is not { } cur) return choice.Label;
            return new I18nText(choice.Label.En + ": " + cur.Label.En, choice.Label.Get(Lang.De) + ": " + cur.Label.Get(Lang.De));
        }
    }
}
