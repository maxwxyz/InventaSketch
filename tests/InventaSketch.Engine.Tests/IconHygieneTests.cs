using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Pictogram and label hygiene of every context action (UX review 2026-11-25, findings G8/G9, follow-up F3): one pictogram
    /// one meaning, the "T" only for editing the text, every pictogram drawable by the hosts, short labels.
    /// <para>
    /// Rules. (1) Every action pictogram is one of <see cref="IconIds.All"/>. (2) <see cref="IconIds.Text"/> only for actions
    /// that edit the element's text: the action asks for the text and its id ends with ".text" or its label is "Text…". (3) Within one element (one
    /// state of it) or one part, a pictogram is not shared by actions of different meaning; the meaning is the action id
    /// without place qualifiers (".1", ".top", "Left", "At" …: "add a row at the top" and "add a row" mean the same).
    /// Exceptions: label-only markers (<see cref="IconIds.IsLabelOnly"/>: switch, next variant, pick option), which hosts
    /// never show without their label, and step pictograms (<see cref="IconIds.IsStep"/>: "+"/"−", one more/fewer) when each
    /// comes with its partner of the same value (".inc"/".dec", ".more"/".fewer" …), which hosts show with the label.
    /// (4) <see cref="IconIds.Option"/> only for actions that pick a choice (set one parameter, or an option of an
    /// <see cref="ActionChoice"/>). (5) Labels fit the bar.
    /// </para>
    /// </summary>
    public class IconHygieneTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public IconHygieneTests(ITestOutputHelper output) => _out = output;

        private static SketchDocument Doc(SketchObject o)
        {
            var doc = SketchDocument.Create(new Size2(1000, 800));
            return Edits.Add(doc, doc.Pages[0].Id, o);
        }

        /// <summary>One action list: an element type in one state (default or after one action), or one part.</summary>
        internal sealed record ActionList(string Module, string Type, string Scope, IReadOnlyList<ContextAction> En, IReadOnlyList<ContextAction> De);

        private static List<ActionList>? _lists;

        /// <summary>Every action list of every library element: default state, the state after each plain action, each part.</summary>
        internal static List<ActionList> Lists()
        {
            if (_lists is not null) return _lists;
            var lists = new List<ActionList>();
            var seen = new HashSet<string>();
            foreach (var item in Reg.AllItems)
            {
                SketchObject o;
                try { o = Reg.Create(item, new Vec2(500, 400)); } catch { continue; }
                var type = Reg.Type(o.Type);
                if (type is null) continue;
                var module = type.GetType().Assembly.GetName().Name!.Replace("InventaSketch.Modules.", "");
                void Add(SketchObject x, string scope)
                {
                    try
                    {
                        var en = type.Actions(x, new RenderContext(Doc(x), Lang.En)).ToList();
                        var de = type.Actions(x, new RenderContext(Doc(x), Lang.De)).ToList();
                        var key = type.Id + "|" + string.Join(";", en.Select(a => a.Id + "=" + a.Icon + "=" + a.Label.En));
                        if (seen.Add(key)) lists.Add(new ActionList(module, type.Id, item.Id + " " + scope, en, de));
                        var ctx = new RenderContext(Doc(x), Lang.En);
                        foreach (var part in type.Parts(x, ctx).Select(p => p.Key).ToList())
                        {
                            var pen = Reg.PartActions(x, part, new RenderContext(Doc(x), Lang.En));
                            var pde = Reg.PartActions(x, part, new RenderContext(Doc(x), Lang.De));
                            var pkey = type.Id + "|part|" + string.Join(";", pen.Select(a => a.Id + "=" + a.Icon + "=" + a.Label.En));
                            if (seen.Add(pkey)) lists.Add(new ActionList(module, type.Id, item.Id + " " + scope + " part " + part, pen, pde));
                        }
                    }
                    catch
                    {
                        // Broken action lists are reported by LibraryAuditTests.
                    }
                }
                Add(o, "default");
                List<ContextAction> first;
                try { first = type.Actions(o, new RenderContext(Doc(o))).Where(a => a.Input is null && a.ApplyInDocument is null).ToList(); }
                catch { continue; }
                foreach (var a in first)
                {
                    SketchObject next;
                    try { next = a.Apply(o); } catch { continue; }
                    Add(next, "after " + a.Id);
                }
            }
            return _lists = lists;
        }

        private static IEnumerable<(ActionList List, ContextAction Action)> AllActions() => Lists().SelectMany(l => l.En.Select(a => (l, a)));

        private static readonly HashSet<string> PlaceWords = new(StringComparer.Ordinal) { "top", "bottom", "left", "right", "start", "end", "above", "below", "at" };
        private static readonly Regex PlaceSuffix = new(@"(?<=[a-z])(Start|End|Left|Right|Top|Bottom|At)$", RegexOptions.Compiled);

        /// <summary>
        /// The meaning of an action: its id without place qualifiers, segment by segment ("ann.table.row.add.top" = "add a
        /// row", "diag.lanes.addStart" = "add a lane", "fluid.valve.actLeft.next" = "next actuation", "diag.lanes.rename.2").
        /// </summary>
        internal static string Meaning(string id)
            => string.Join(".", id.Split('.')
                .Where(seg => seg.Length > 0 && !seg.All(char.IsDigit) && !PlaceWords.Contains(seg))
                .Select(seg => PlaceSuffix.Replace(seg, "")));

        private static readonly Regex StepSuffix = new(@"\.(inc|dec|more|fewer|less|up|down|next|prev)$", RegexOptions.Compiled);

        /// <summary>The value a step action changes: "param.poles.inc" and "param.poles.dec" both change "param.poles".</summary>
        internal static string StepValue(string id) => StepSuffix.Replace(id, "");

        private static string? Partner(string icon) => icon switch
        {
            IconIds.Increase => IconIds.Decrease,
            IconIds.Decrease => IconIds.Increase,
            IconIds.CountMore => IconIds.CountFewer,
            IconIds.CountFewer => IconIds.CountMore,
            _ => null,
        };

        [Fact]
        public void EveryActionPictogramIsDrawnByTheHosts()
        {
            var bad = AllActions().Where(x => !IconIds.All.Contains(x.Action.Icon))
                .Select(x => $"{x.List.Type}: {x.Action.Id} uses unknown pictogram '{x.Action.Icon}'").Distinct().ToList();
            foreach (var b in bad) _out.WriteLine(b);
            Assert.Empty(bad);
        }

        /// <summary>An action that asks for the element's (or the part's) text: its id ends with ".text" or its label is "Text…".</summary>
        private static bool EditsTheText(ContextAction a)
            => a.Input is not null && (a.Id.EndsWith(".text", StringComparison.Ordinal) || a.Label.En.StartsWith("Text", StringComparison.Ordinal));

        [Fact]
        public void TextPictogramOnlyEditsTheText()
        {
            var bad = AllActions().Where(x => x.Action.Icon == IconIds.Text && !EditsTheText(x.Action))
                .Select(x => $"{x.List.Type}: {x.Action.Id} '{x.Action.Label.En}' uses the text pictogram").Distinct().ToList();
            foreach (var b in bad) _out.WriteLine(b);
            Assert.Empty(bad);
        }

        [Fact]
        public void OptionPictogramOnlyPicksAChoice()
        {
            // An option of a choice (ActionChoice: curve shape, fill pattern …) picks a choice too, also without a parameter.
            var bad = AllActions().Where(x => x.Action.Icon == IconIds.Option && x.Action.ParamKeys is not { Count: > 0 } && x.Action.Choice is null)
                .Select(x => $"{x.List.Type}: {x.Action.Id} '{x.Action.Label.En}' uses the option pictogram without setting a parameter").Distinct().ToList();
            foreach (var b in bad) _out.WriteLine(b);
            Assert.Empty(bad);
        }

        [Fact]
        public void OnePictogramOneMeaningPerElement()
        {
            var bad = new List<string>();
            foreach (var list in Lists())
            {
                // View switches are shown by the hosts' view control (Type.Views with labels), see ViewPictograms.
                var actions = list.En.Where(a => a.Group != "view").ToList();
                foreach (var g in actions.GroupBy(a => a.Icon))
                {
                    if (IconIds.IsLabelOnly(g.Key)) continue;
                    var meanings = g.Select(a => Meaning(a.Id)).Distinct().ToList();
                    if (meanings.Count < 2) continue;
                    if (IconIds.IsStep(g.Key))
                    {
                        // Several "+" (or "one more") are fine for explicit steps of one value each (".inc"/".dec", ".more"/".fewer" …,
                        // the partner may be missing at a limit) or when the "−" of the same value is there: hosts show them with labels.
                        var partner = Partner(g.Key)!;
                        var unpaired = g.Where(a => !StepSuffix.IsMatch(a.Id) && !actions.Any(b => b.Icon == partner && StepValue(b.Id) == StepValue(a.Id))).ToList();
                        if (unpaired.Count == 0) continue;
                        bad.Add($"{list.Type} ({list.Scope}): '{g.Key}' on unpaired steps {string.Join(", ", unpaired.Select(a => a.Id))}");
                        continue;
                    }
                    bad.Add($"{list.Type} ({list.Scope}): '{g.Key}' means {string.Join(" / ", g.Select(a => $"{a.Id} '{a.Label.En}'").Distinct())}");
                }
            }
            bad = bad.Distinct().ToList();
            foreach (var b in bad) _out.WriteLine(b);
            Assert.Empty(bad);
        }

        /// <summary>
        /// Views of one element that share a pictogram (hosts show views with their labels, so this is reported, not failed):
        /// the list for the next round of view pictograms. Every view pictogram is drawable.
        /// </summary>
        [Fact]
        public void ViewPictograms()
        {
            var types = Reg.AllItems.Select(i => Reg.Type(i.TypeId)).Where(t => t is not null).Select(t => t!).Distinct().ToList();
            var shared = new List<string>();
            foreach (var type in types.Where(t => t.Views.Count > 1))
                foreach (var g in type.Views.GroupBy(IconIds.ForView).Where(g => g.Count() > 1))
                    shared.Add($"{type.Id}: '{g.Key}' for {string.Join(", ", g.Select(v => v.Id))}");
            foreach (var s in shared) _out.WriteLine("SHARED VIEW PICTOGRAM " + s);
            _out.WriteLine($"{shared.Count} element(s) with views sharing a pictogram");
            Assert.All(types.SelectMany(t => t.Views), v => Assert.Contains(IconIds.ForView(v), IconIds.All));
        }

        /// <summary>Longest label the floating bar shows uncut (OnSlideController.ShortLabel; the web bar uses the same limit).</summary>
        private const int BarLabel = 22;

        /// <summary>Labels may be longer in the pie menu, but never more than this.</summary>
        private const int PieLabel = 34;

        [Fact]
        public void LabelsFit()
        {
            var tooLong = new List<string>();
            var over = new List<string>();
            foreach (var list in Lists())
                for (var i = 0; i < list.En.Count; i++)
                {
                    var a = list.En[i];
                    var de = list.De.FirstOrDefault(d => d.Id == a.Id)?.Label.Get(Lang.De) ?? a.Label.Get(Lang.De);
                    foreach (var label in new[] { a.Label.En, de })
                    {
                        // "Next variant" actions name the variant they switch to ("Machine type: Induction, squirrel cage"):
                        // the variant name is data, the wording is the prefix before the colon.
                        if (a.Icon == IconIds.Cycle && !label.Contains(": ")) continue;
                        var shown = a.Icon == IconIds.Cycle ? Shown(label.Substring(0, label.IndexOf(": ", StringComparison.Ordinal))) : Shown(label);
                        if (shown.Length > PieLabel) tooLong.Add($"{list.Type}: {a.Id} '{label}' ({shown.Length})");
                        else if (shown.Length > BarLabel) over.Add($"{list.Type}: {a.Id} '{label}' ({shown.Length})");
                    }
                }
            foreach (var b in over.Distinct()) _out.WriteLine("LONG " + b);
            foreach (var b in tooLong.Distinct()) _out.WriteLine("TOO LONG " + b);
            _out.WriteLine($"{over.Distinct().Count()} label(s) longer than {BarLabel} characters (pie only), {tooLong.Distinct().Count()} longer than {PieLabel}");
            Assert.Empty(tooLong.Distinct());
        }

        /// <summary>The label as the bar shows it (OnSlideController.ShortLabel): without "Next " and a parenthesised suffix (" (240 mm)").</summary>
        private static string Shown(string label)
        {
            foreach (var prefix in new[] { "Next ", "Nächste ", "Nächster ", "Nächstes " })
                if (label.StartsWith(prefix, StringComparison.Ordinal) && label.Length > prefix.Length + 2) { label = label.Substring(prefix.Length); break; }
            var cut = label.IndexOf(" (", StringComparison.Ordinal);
            return (cut > 0 ? label.Substring(0, cut) : label).Trim();
        }

        [Fact]
        public void DumpInventory()
        {
            var path = Environment.GetEnvironmentVariable("ICON_INVENTORY_OUT");
            if (string.IsNullOrEmpty(path)) return;
            var rows = new List<string>();
            foreach (var list in Lists())
                foreach (var a in list.En)
                {
                    var de = list.De.FirstOrDefault(d => d.Id == a.Id)?.Label.Get(Lang.De) ?? a.Label.Get(Lang.De);
                    rows.Add(string.Join("\t", list.Module, list.Type, a.Id, a.Icon, a.Label.En, de, string.Join(",", a.ParamKeys ?? Array.Empty<string>()), a.Input is null ? "" : "input"));
                }
            File.WriteAllLines(path, rows.Distinct().OrderBy(r => r, StringComparer.Ordinal));
        }
    }
}
