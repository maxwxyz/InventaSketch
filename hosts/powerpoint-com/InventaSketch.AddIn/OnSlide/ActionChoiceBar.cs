using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Pure helpers for choices among actions (<see cref="ActionChoice"/>, docs/en/design/interaction-ux.md "Choices are
    /// dropdowns"): the bar and pie entries of an action list (one entry per plain action, one per choice at the place of its
    /// first option), the keys and commands of their dropdowns, which grips a part selection shows, and what the Del key does
    /// with a selected part. Unit tested without PowerPoint.
    /// </summary>
    internal static class ActionChoiceBar
    {
        /// <summary>Key prefix of a part's choices in the bar's dropdown machinery ("@p.chart.curve.shape").</summary>
        public const string PartPrefix = "@p.";

        /// <summary>Key prefix of an element's action choices that are no parameter ("@a.chart.area.add").</summary>
        public const string ElementPrefix = "@a.";

        /// <summary>One entry of a bar or pie: a plain action, or a choice with all its options.</summary>
        internal sealed record Entry(ContextAction? Action, ActionChoice? Choice, IReadOnlyList<ContextAction> Options)
        {
            public bool IsChoice => Choice is not null;
        }

        /// <summary>The entries in the order of the actions: every plain action, every choice once (where its first option is).</summary>
        public static List<Entry> Entries(IEnumerable<ContextAction> actions)
        {
            var list = actions.ToList();
            var groups = ActionChoices.Group(list).ToDictionary(g => g.Choice.Key, g => g.Options, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<Entry>();
            foreach (var a in list)
            {
                if (a.Choice is not { } c) { entries.Add(new Entry(a, null, Array.Empty<ContextAction>())); continue; }
                if (seen.Add(c.Key)) entries.Add(new Entry(null, c, groups[c.Key]));
            }
            return entries;
        }

        /// <summary>The dropdown key of a choice with <paramref name="prefix"/>.</summary>
        public static string Key(string prefix, ActionChoice choice) => prefix + choice.Key;

        /// <summary>
        /// The edit command of a picked option: a part choice runs the part action ("part:&lt;id&gt;"), an element action
        /// choice the type action ("act:&lt;id&gt;"); null for other keys.
        /// </summary>
        public static string? Command(string key, string optionId)
        {
            if (key.StartsWith(PartPrefix, StringComparison.Ordinal)) return "part:" + optionId;
            if (key.StartsWith(ElementPrefix, StringComparison.Ordinal)) return "act:" + optionId;
            return null;
        }

        /// <summary>"Label: Current" (state choice) or "Label ▸" (menu) for the pie, with the current option's label.</summary>
        public static string PieLabel(ActionChoice choice, IReadOnlyList<ContextAction> options, Lang lang)
            => ActionChoices.Caption(choice, options).Get(lang) + " ▸";

        /// <summary>
        /// True if a grip is shown in full while <paramref name="selectedPart"/> is selected: the grip of that part, and the
        /// grips that come with it (the point handles of a selected curve, and of the curve of a selected point).
        /// </summary>
        public static bool ShownWith(DragHandle grip, string? selectedPart, IEnumerable<DragHandle> grips)
        {
            if (selectedPart is null) return false;
            if (grip.Part == selectedPart) return true;
            if (grip.ShowWith is null) return false;
            var group = grips.FirstOrDefault(g => g.Part == selectedPart && g.ShowWith is not null)?.ShowWith ?? selectedPart;
            return grip.ShowWith == group;
        }

        /// <summary>The grip dragged by pressing on the selected part (<see cref="DragHandle.ByPart"/>), or null.</summary>
        public static DragHandle? BodyGrip(IEnumerable<DragHandle> grips, string? selectedPart)
            => selectedPart is null ? null : grips.FirstOrDefault(g => g.ByPart && g.Part == selectedPart);

        /// <summary>What the Del key runs on a selected part: its own delete action, else hiding it; null when neither applies.</summary>
        public static string? DeleteActionId(IEnumerable<ContextAction> partActions)
        {
            var list = partActions.ToList();
            if (list.FirstOrDefault(a => a.Id == PartLooks.DeleteAction && a.IsEnabled) is { } del) return del.Id;
            if (list.FirstOrDefault(a => a.Id == "part.hide" && a.IsEnabled && !a.IsActive) is { } hide) return hide.Id;
            return null;
        }

        /// <summary>The part selected after <paramref name="part"/> was deleted: a point's curve ("point.0.3" → "series.0"), else none.</summary>
        public static string? AfterDelete(string part)
        {
            var seg = part.Split('.');
            return seg.Length == 3 && seg[0] == "point" ? "series." + seg[1] : null;
        }
    }
}
