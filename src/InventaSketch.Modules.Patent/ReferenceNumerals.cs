using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;

namespace InventaSketch.Modules.Patent
{
    /// <summary>Numbering scheme: main parts Start, Start+Step, …; sub-parts parent+SubStep, … below the next main.</summary>
    public sealed record NumeralScheme(int Start = 10, int Step = 10, int SubStep = 1)
    {
        public static readonly NumeralScheme Default = new();
    }

    /// <summary>A drawn reference numeral on a page.</summary>
    public sealed record NumeralUse(string Numeral, string ObjectId, string PageId, string TypeId, string? Label);

    public enum ReferenceIssueKind
    {
        /// <summary>One numeral used for differently named parts (EP GL A-IX 7.5.4; 37 CFR 1.84(p)(4)).</summary>
        SameNumeralDifferentLabels,
        /// <summary>One part name with several numerals (same feature, same sign).</summary>
        LabelWithDifferentNumerals,
        /// <summary>Numeral in the drawings but not in the reference list (description).</summary>
        NumeralWithoutEntry,
        /// <summary>Reference list entry never drawn.</summary>
        EntryUnused,
    }

    public sealed record ReferenceIssue(ReferenceIssueKind Kind, string? Numeral, string? Label, IReadOnlyList<string> ObjectIds, I18nText Message);

    public enum ReferenceListFormat { Text, Csv }

    /// <summary>
    /// Reference numeral service: next free numeral, rename/renumber across all pages, reference list sync
    /// and export, and consistency checks. All operations are pure document edits (undo-safe).
    /// </summary>
    public static class ReferenceNumerals
    {
        private static readonly Regex NumeralPattern = new(@"^(\d+)(.*)$", RegexOptions.Compiled);

        /// <summary>True for object types that draw a reference numeral.</summary>
        public static bool IsNumeralType(string typeId) => typeId is PatentIds.Numeral or "ann.leader";

        /// <summary>Drawn numeral of an object, or null.</summary>
        public static string? NumeralOf(SketchObject o) => o.Type switch
        {
            PatentIds.Numeral => NumeralType.NumeralOf(o),
            "ann.leader" => LeaderType.NumeralOf(o),
            _ => null,
        };

        /// <summary>Part name given on a numeral object (param "label"), or null.</summary>
        public static string? LabelOf(SketchObject o) => o.Params.TryGetValue("label", out var v) && !string.IsNullOrWhiteSpace(v.Text) ? v.Text!.Trim() : null;

        /// <summary>Normalised key (enclosures removed) used for comparisons.</summary>
        public static string Key(string numeral) => NumeralType.StripEnclosure(numeral);

        /// <summary>All drawn numerals in page order and z-order.</summary>
        public static IReadOnlyList<NumeralUse> Uses(SketchDocument doc)
        {
            var list = new List<NumeralUse>();
            foreach (var page in doc.Pages)
                foreach (var o in doc.ObjectsOn(page))
                {
                    var n = NumeralOf(o);
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    list.Add(new NumeralUse(Key(n!), o.Id, page.Id, o.Type, LabelOf(o)));
                }
            return list;
        }

        private static (int? Number, string Suffix) Parse(string numeral)
        {
            var m = NumeralPattern.Match(numeral.Trim());
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? (n, m.Groups[2].Value) : (null, numeral);
        }

        private static HashSet<int> UsedNumbers(SketchDocument doc)
        {
            var set = new HashSet<int>();
            foreach (var n in Uses(doc).Select(u => u.Numeral).Concat(doc.References.Keys.Select(Key)))
                if (Parse(n).Number is int i) set.Add(i);
            return set;
        }

        /// <summary>
        /// Next free numeral. Without <paramref name="parent"/>: the next main numeral (multiple of the step from
        /// Start) whose whole sub-range is unused. With a parent ("10"): the next free sub-numeral (11, 12, …).
        /// </summary>
        public static string NextFree(SketchDocument doc, NumeralScheme? scheme = null, string? parent = null)
        {
            scheme ??= NumeralScheme.Default;
            var used = UsedNumbers(doc);
            if (parent is not null)
            {
                var p = Parse(Key(parent)).Number ?? throw new ArgumentException("Parent numeral must be numeric.", nameof(parent));
                for (var c = p + scheme.SubStep; c < p + scheme.Step; c += scheme.SubStep)
                    if (!used.Contains(c)) return c.ToString(CultureInfo.InvariantCulture);
                throw new InvalidOperationException($"No free sub-numeral left below {p + scheme.Step}.");
            }
            for (var c = scheme.Start; ; c += scheme.Step)
                if (!used.Any(u => u >= c && u < c + scheme.Step)) return c.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Inserts a reference numeral attached to a box object (or free when <paramref name="targetId"/> is null).</summary>
        public static (SketchDocument Doc, SketchObject Numeral) Insert(Registry registry, SketchDocument doc, string pageId, Vec2 objectPoint, Vec2 numeralPoint,
            string? label = null, string? targetId = null, string? numeral = null, NumeralScheme? scheme = null)
        {
            numeral ??= NextFree(doc, scheme);
            var type = registry.Type(PatentIds.Numeral) ?? throw new InvalidOperationException("Figures module is not registered.");
            var o = type.Create(objectPoint) with { Points = new[] { objectPoint, numeralPoint }, Ref = numeral };
            o = o.WithParam("numeral", numeral);
            if (label is not null) o = o.WithParam("label", label);
            doc = Edits.Add(doc, pageId, o);
            if (targetId is not null) doc = Ann.AttachPoint(doc, o.Id, 0, targetId, objectPoint);
            if (label is not null && !doc.References.ContainsKey(numeral)) doc = Edits.SetReference(doc, numeral, label);
            return (doc, doc.Get(o.Id)!);
        }

        /// <summary>Renames a numeral on all pages and in the reference list.</summary>
        /// <exception cref="InvalidOperationException">The new numeral is already used and <paramref name="merge"/> is false.</exception>
        public static SketchDocument Rename(SketchDocument doc, string oldNumeral, string newNumeral, bool merge = false)
        {
            oldNumeral = Key(oldNumeral); newNumeral = Key(newNumeral);
            if (oldNumeral == newNumeral) return doc;
            var exists = Uses(doc).Any(u => u.Numeral == newNumeral) || doc.References.Keys.Any(k => Key(k) == newNumeral);
            if (exists && !merge) throw new InvalidOperationException($"Numeral '{newNumeral}' is already used.");
            return Apply(doc, new Dictionary<string, string> { [oldNumeral] = newNumeral });
        }

        /// <summary>
        /// Renumbers all numerals in order of first appearance (pages, then z-order; unused list entries last)
        /// following the scheme. Sub-numerals keep their offset to the parent (13 → 23 when 10 → 20); suffixes
        /// and primes are kept. Returns the new document and the old→new map.
        /// </summary>
        public static (SketchDocument Doc, IReadOnlyDictionary<string, string> Map) Renumber(SketchDocument doc, NumeralScheme? scheme = null)
        {
            scheme ??= NumeralScheme.Default;
            var order = Uses(doc).Select(u => u.Numeral).Concat(doc.References.Keys.Select(Key)).Distinct().ToList();
            var numbers = new HashSet<int>(order.Select(n => Parse(n).Number).Where(n => n.HasValue).Select(n => n!.Value));
            int GroupBase(int n) { var b = n - (n % scheme.Step); return numbers.Contains(b) ? b : n; }
            var baseMap = new Dictionary<int, int>();
            var next = scheme.Start;
            foreach (var n in order)
            {
                if (Parse(n).Number is not int num) continue;
                var b = GroupBase(num);
                if (baseMap.ContainsKey(b)) continue;
                baseMap[b] = next;
                next += scheme.Step;
            }
            var map = new Dictionary<string, string>();
            foreach (var n in order)
            {
                var (num, suffix) = Parse(n);
                if (num is not int v) continue;
                var b = GroupBase(v);
                var nv = baseMap[b] + (v - b);
                map[n] = nv.ToString(CultureInfo.InvariantCulture) + suffix;
            }
            return (Apply(doc, map), map);
        }

        /// <summary>Applies an old→new numeral map simultaneously to objects and the reference list.</summary>
        public static SketchDocument Apply(SketchDocument doc, IReadOnlyDictionary<string, string> map)
        {
            if (map.Count == 0) return doc;
            string? M(string? s) => s is not null && map.TryGetValue(Key(s), out var n) ? n : s;
            var ids = doc.Objects.Values.Where(o => (o.Ref is not null && map.ContainsKey(Key(o.Ref))) || (IsNumeralType(o.Type) && NumeralOf(o) is string n && map.ContainsKey(Key(n)))).Select(o => o.Id).ToList();
            doc = Edits.Update(doc, ids, o =>
            {
                var r = o with { Ref = M(o.Ref) };
                if (IsNumeralType(o.Type))
                {
                    if (!string.IsNullOrEmpty(o.Text)) r = r with { Text = M(o.Text) };
                    if (o.Params.TryGetValue("numeral", out var pv) && pv.Text is not null) r = r.WithParam("numeral", M(pv.Text)!);
                }
                return r;
            });
            var refs = new Dictionary<string, ReferenceEntry>();
            foreach (var kv in doc.References)
            {
                var nk = M(kv.Key)!;
                var entry = kv.Value with { Numeral = nk };
                if (refs.TryGetValue(nk, out var existing) && !string.IsNullOrEmpty(existing.Label)) continue; // merge keeps the existing label
                refs[nk] = entry;
            }
            return doc with { References = refs };
        }

        /// <summary>Adds missing reference-list entries for drawn numerals (label from the numeral object if known).</summary>
        public static SketchDocument Sync(SketchDocument doc, bool removeUnused = false)
        {
            var refs = doc.References.ToDictionary(kv => kv.Key, kv => kv.Value);
            var uses = Uses(doc);
            foreach (var g in uses.GroupBy(u => u.Numeral))
            {
                var label = g.Select(u => u.Label).FirstOrDefault(l => l is not null) ?? "";
                if (!refs.TryGetValue(g.Key, out var e)) refs[g.Key] = new ReferenceEntry(g.Key, label);
                else if (string.IsNullOrEmpty(e.Label) && label.Length > 0) refs[g.Key] = e with { Label = label };
            }
            if (removeUnused)
                foreach (var k in refs.Keys.ToList()) if (uses.All(u => u.Numeral != Key(k))) refs.Remove(k);
            return doc with { References = refs };
        }

        /// <summary>Reference list sorted numerically ("10 housing").</summary>
        public static IReadOnlyList<ReferenceEntry> List(SketchDocument doc)
            => doc.References.Values.OrderBy(e => Parse(Key(e.Numeral)).Number ?? int.MaxValue).ThenBy(e => e.Numeral, StringComparer.Ordinal).ToList();

        /// <summary>Exports the reference sign list as plain text ("10 housing" per line) or CSV ("numeral,label").</summary>
        public static string Export(SketchDocument doc, ReferenceListFormat format = ReferenceListFormat.Text)
        {
            var sb = new StringBuilder();
            if (format == ReferenceListFormat.Csv) sb.Append("numeral,label\r\n");
            foreach (var e in List(doc))
            {
                if (format == ReferenceListFormat.Csv) sb.Append(Csv(e.Numeral)).Append(',').Append(Csv(e.Label)).Append("\r\n");
                else sb.Append(e.Numeral).Append(' ').Append(e.Label).Append('\n');
            }
            return sb.ToString();
        }

        private static string Csv(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        /// <summary>Consistency checks between drawn numerals, their part names and the reference list.</summary>
        public static IReadOnlyList<ReferenceIssue> Check(SketchDocument doc)
        {
            var issues = new List<ReferenceIssue>();
            var uses = Uses(doc);
            var refs = doc.References.Values.ToDictionary(e => Key(e.Numeral), e => e);
            foreach (var g in uses.GroupBy(u => u.Numeral))
            {
                var labels = g.Select(u => u.Label).Where(l => l is not null).Select(l => l!).ToList();
                if (refs.TryGetValue(g.Key, out var e) && !string.IsNullOrWhiteSpace(e.Label)) labels.Add(e.Label.Trim());
                var distinct = labels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (distinct.Count > 1)
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.SameNumeralDifferentLabels, g.Key, string.Join(" / ", distinct), g.Select(u => u.ObjectId).ToList(),
                        new I18nText($"Numeral {g.Key} is used for different parts: {string.Join(", ", distinct)}.",
                                     $"Bezugszeichen {g.Key} wird für verschiedene Teile verwendet: {string.Join(", ", distinct)}.")));
                if (!refs.ContainsKey(g.Key))
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.NumeralWithoutEntry, g.Key, null, g.Select(u => u.ObjectId).ToList(),
                        new I18nText($"Numeral {g.Key} is drawn but missing from the reference list.", $"Bezugszeichen {g.Key} ist gezeichnet, fehlt aber in der Bezugszeichenliste.")));
            }
            foreach (var e in refs.Values)
                if (uses.All(u => u.Numeral != Key(e.Numeral)))
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.EntryUnused, Key(e.Numeral), e.Label, Array.Empty<string>(),
                        new I18nText($"Reference list entry {e.Numeral} ({e.Label}) does not appear in any drawing.", $"Listeneintrag {e.Numeral} ({e.Label}) kommt in keiner Zeichnung vor.")));
            // Part names → numerals (drawn labels and list entries).
            var byLabel = uses.Where(u => u.Label is not null).Select(u => (Label: u.Label!, u.Numeral, Id: (string?)u.ObjectId))
                .Concat(refs.Values.Where(e => !string.IsNullOrWhiteSpace(e.Label)).Select(e => (Label: e.Label.Trim(), Numeral: Key(e.Numeral), Id: (string?)null)))
                .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase);
            foreach (var g in byLabel)
            {
                var nums = g.Select(x => x.Numeral).Distinct().ToList();
                if (nums.Count > 1)
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.LabelWithDifferentNumerals, string.Join(", ", nums), g.Key, g.Where(x => x.Id is not null).Select(x => x.Id!).Distinct().ToList(),
                        new I18nText($"Part \"{g.Key}\" has different numerals: {string.Join(", ", nums)}.", $"Teil \"{g.Key}\" hat verschiedene Bezugszeichen: {string.Join(", ", nums)}.")));
            }
            return issues;
        }
    }
}
