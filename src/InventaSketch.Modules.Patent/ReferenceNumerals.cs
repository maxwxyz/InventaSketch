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
        /// <summary>10, 20, 30 … with sub-numerals 11, 12 … in between.</summary>
        public static readonly NumeralScheme Default = new();

        /// <summary>1, 2, 3 … (no room for sub-numerals).</summary>
        public static readonly NumeralScheme Consecutive = new(1, 1, 1);
    }

    /// <summary>
    /// A line of the grouped reference list: the variants of one numeral that name the same part ("10, 10a, 10' housing").
    /// </summary>
    public sealed record ReferenceGroup(string Base, IReadOnlyList<string> Numerals, string Label);

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
        /// <summary>One numeral on parts of different kinds (same character for different parts, 37 CFR 1.84(p)(4)).</summary>
        SameNumeralDifferentParts,
        /// <summary>
        /// The same kind of part with unrelated numerals on different pages (same feature, same sign throughout the
        /// drawings): no numeral (variants count as one) of a page is used for that part on another page.
        /// </summary>
        SamePartDifferentNumerals,
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

        private static readonly Regex VariantPattern = new(@"^(\d+)([A-Za-z]?)(['’′″]*)$", RegexOptions.Compiled);

        /// <summary>
        /// Variant suffixes offered for a numeral: none, letters a–d and primes (10a, 10b, 10', 10''). Primes are written
        /// as ASCII apostrophes so they compare equal however they were typed.
        /// </summary>
        public static readonly IReadOnlyList<string> Suffixes = new[] { "", "a", "b", "c", "d", "'", "''" };

        /// <summary>
        /// Splits a numeral into its number and its variant suffix ("10a" → ("10", "a"), "12'" → ("12", "'")). Numerals
        /// that are not a number with an optional letter and primes are returned whole with an empty suffix.
        /// </summary>
        public static (string Base, string Suffix) SplitSuffix(string numeral)
        {
            var k = Key(numeral);
            var m = VariantPattern.Match(k);
            if (!m.Success) return (k, "");
            var primes = new string('\'', m.Groups[3].Value.Sum(c => c == '″' ? 2 : 1));
            return (m.Groups[1].Value, m.Groups[2].Value + primes);
        }

        /// <summary>The number a variant belongs to ("10a" → "10").</summary>
        public static string BaseOf(string numeral) => SplitSuffix(numeral).Base;

        /// <summary>The numeral <paramref name="numeral"/> with its variant suffix replaced ("10a", "b" → "10b").</summary>
        public static string WithSuffix(string numeral, string suffix) => BaseOf(numeral) + suffix;

        /// <summary>
        /// Sets the variant suffix of a numeral object: the numeral (Ref, text and parameter), the part it points at when
        /// that part carries the old numeral, and a reference list entry for the variant named like its base entry.
        /// </summary>
        public static SketchDocument SetSuffix(SketchDocument doc, string numeralObjectId, string suffix)
        {
            var o = doc.Get(numeralObjectId);
            if (o is null || !IsNumeralType(o.Type)) return doc;
            var old = Key(NumeralOf(o) ?? "");
            var next = WithSuffix(old, suffix);
            if (old.Length == 0 || next == old) return doc;
            doc = Edits.Replace(doc, SetNumeral(o, next));
            if (o.Attach is { Count: > 0 } att && att[0] is { } a && doc.Get(a.ObjectId) is { IsPointGeometry: false } part && part.Ref is not null && Key(part.Ref) == old)
                doc = Edits.Replace(doc, part with { Ref = next });
            if (!doc.References.Keys.Any(k => Key(k) == next))
            {
                var label = LabelOf(o) ?? doc.References.Values.Where(e => Key(e.Numeral) == old || Key(e.Numeral) == BaseOf(old)).Select(e => e.Label).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                if (label is not null) doc = Edits.SetReference(doc, next, label);
            }
            return doc;
        }

        /// <summary>A numeral object showing <paramref name="numeral"/> (Ref, text if set, and the "numeral" parameter).</summary>
        public static SketchObject SetNumeral(SketchObject o, string numeral)
        {
            var r = o with { Ref = numeral };
            if (!string.IsNullOrEmpty(o.Text)) r = r with { Text = numeral };
            return r.WithParam("numeral", numeral);
        }

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

        /// <summary>
        /// The reference list with variants grouped: entries of one number whose part names agree form one line
        /// ("10, 10a, 10b housing"); variants with their own name stay separate lines below their number.
        /// </summary>
        public static IReadOnlyList<ReferenceGroup> Groups(SketchDocument doc)
        {
            var groups = new List<ReferenceGroup>();
            foreach (var byBase in List(doc).GroupBy(e => BaseOf(e.Numeral)))
                foreach (var byLabel in byBase.GroupBy(e => (e.Label ?? "").Trim(), StringComparer.OrdinalIgnoreCase))
                    groups.Add(new ReferenceGroup(byBase.Key, byLabel.Select(e => e.Numeral).ToList(), byLabel.First().Label ?? ""));
            return groups;
        }

        /// <summary>
        /// Exports the reference sign list as plain text (one line per part, variants grouped: "10, 10a housing") or CSV
        /// ("numeral,label", one row per numeral, variants below their number).
        /// </summary>
        public static string Export(SketchDocument doc, ReferenceListFormat format = ReferenceListFormat.Text)
        {
            var sb = new StringBuilder();
            if (format == ReferenceListFormat.Csv)
            {
                sb.Append("numeral,label\r\n");
                foreach (var e in List(doc)) sb.Append(Csv(e.Numeral)).Append(',').Append(Csv(e.Label)).Append("\r\n");
                return sb.ToString();
            }
            foreach (var g in Groups(doc)) sb.Append(string.Join(", ", g.Numerals)).Append(' ').Append(g.Label).Append('\n');
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
                // Variants of one number (10, 10a, 10') may share the part name.
                if (nums.Select(BaseOf).Distinct().Count() > 1)
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.LabelWithDifferentNumerals, string.Join(", ", nums), g.Key, g.Where(x => x.Id is not null).Select(x => x.Id!).Distinct().ToList(),
                        new I18nText($"Part \"{g.Key}\" has different numerals: {string.Join(", ", nums)}.", $"Teil \"{g.Key}\" hat verschiedene Bezugszeichen: {string.Join(", ", nums)}.")));
            }
            // One numeral on parts of different kinds (identical parts may share a numeral).
            foreach (var g in NumberedParts(doc).GroupBy(x => x.Numeral))
            {
                var parts = g.Select(x => x.Part).GroupBy(p => p.Id).Select(p => p.First()).ToList();
                if (parts.Select(PartKind).Distinct().Count() < 2) continue;
                var ids = parts.Select(p => p.Id).Concat(g.Select(x => x.NumeralId).Where(id => id is not null).Select(id => id!)).Distinct().ToList();
                issues.Add(new ReferenceIssue(ReferenceIssueKind.SameNumeralDifferentParts, g.Key, null, ids,
                    new I18nText($"Numeral {g.Key} points at {parts.Count} different parts. Use one numeral per part, or variants (10a, 10b) for related parts.",
                                 $"Bezugszeichen {g.Key} zeigt auf {parts.Count} verschiedene Teile. Ein Bezugszeichen je Teil verwenden oder Varianten (10a, 10b) für verwandte Teile.")));
            }
            return issues;
        }

        /// <summary>A numbered part on a page for <see cref="CheckAcrossPages"/>: numeral, kind, name and the objects (own pages only).</summary>
        private sealed record PagePart(string Numeral, string Kind, string? Label, IReadOnlyList<string> Ids);

        /// <summary>
        /// Consistency of numerals between pages: the pages of <paramref name="doc"/> among each other and with the other
        /// pages or slides a host keeps in models of their own (<paramref name="others"/>). Reported on the pages of
        /// <paramref name="doc"/>: a numeral that names a different kind of part on another (separately kept) page
        /// (<see cref="ReferenceIssueKind.SameNumeralDifferentParts"/>; within <paramref name="doc"/> <see cref="Check"/> reports
        /// it), and a kind of part (<see cref="PartKind"/>) whose numerals on a page share no number with its numerals on
        /// another page (<see cref="ReferenceIssueKind.SamePartDifferentNumerals"/>; parts numbered separately on purpose, 10
        /// and 20 for two identical gears, stay fine as long as the pages use the same numbers for them). Page labels are
        /// the 1-based page numbers of <paramref name="doc"/> and the hosts' labels of the other pages.
        /// </summary>
        public static IReadOnlyList<ReferenceIssue> CheckAcrossPages(SketchDocument doc, IReadOnlyList<OtherPage>? others = null, PageNames? names = null)
        {
            names ??= PageNames.Pages;
            var issues = new List<ReferenceIssue>();
            string? Listed(string n) => doc.References.Values.Where(e => Key(e.Numeral) == n).Select(e => e.Label).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            // Own pages with their numbered parts (one line per numeral and part kind).
            var own = doc.Pages.Select((p, i) => (Page: p, Label: (i + 1).ToString(CultureInfo.InvariantCulture), Parts: new List<PagePart>())).ToList();
            foreach (var (n, part, numeralId) in NumberedParts(doc))
            {
                var page = doc.PageOf(numeralId ?? part.Id) ?? doc.PageOf(part.Id);
                var entry = own.FirstOrDefault(x => x.Page.Id == page?.Id);
                if (entry.Page is null) continue;
                var label = (numeralId is not null && doc.Get(numeralId) is { } no ? LabelOf(no) : null) ?? Listed(n);
                entry.Parts.Add(new PagePart(n, PartKind(part), label, numeralId is null ? new[] { part.Id } : new[] { part.Id, numeralId }));
            }
            var foreign = (others ?? Array.Empty<OtherPage>())
                .Select(p => (p.Page, Parts: p.Numerals.Where(x => x.Kind is not null).Select(x => new PagePart(Key(x.Numeral), x.Kind!, x.Label, Array.Empty<string>())).ToList()))
                .Where(p => p.Parts.Count > 0).ToList();
            foreach (var (page, label, parts) in own)
            {
                if (parts.Count == 0) continue;
                // A numeral that names another kind of part on a separately kept page.
                foreach (var g in parts.GroupBy(p => p.Numeral))
                {
                    var kinds = new HashSet<string>(g.Select(p => p.Kind), StringComparer.Ordinal);
                    var hits = foreign.Select(f => (f.Page, Other: f.Parts.Where(x => x.Numeral == g.Key && !kinds.Contains(x.Kind)).ToList())).Where(f => f.Other.Count > 0).ToList();
                    if (hits.Count == 0) continue;
                    var what = hits.SelectMany(h => h.Other).Select(x => x.Label).Where(l => l is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var (en, de) = PagesText(names, hits.Select(h => h.Page));
                    var suffix = what.Count > 0 ? " (" + string.Join(", ", what) + ")" : "";
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.SameNumeralDifferentParts, g.Key, null, g.SelectMany(p => p.Ids).Distinct().ToList(),
                        new I18nText($"Numeral {g.Key} names a different part on {en}{suffix}. Use one numeral per part throughout the drawings.",
                                     $"Bezugszeichen {g.Key} bezeichnet auf {de} ein anderes Teil{suffix}. Ein Bezugszeichen je Teil in allen Zeichnungen verwenden.")));
                }
                // The same kind of part with unrelated numerals on another page.
                foreach (var g in parts.GroupBy(p => p.Kind))
                {
                    var bases = new HashSet<string>(g.Select(p => BaseOf(p.Numeral)), StringComparer.Ordinal);
                    var mine = g.Select(p => p.Numeral).Distinct().ToList();
                    var myNames = new HashSet<string>(g.Select(p => p.Label).Where(l => l is not null).Select(l => l!), StringComparer.OrdinalIgnoreCase);
                    // Unrelated numerals for this kind on another page – unless both pages name the parts and the names differ:
                    // generic shapes (a hatched rectangle) are one kind, but "housing" and "cover" are different parts.
                    List<string>? Unrelated(IEnumerable<PagePart> other)
                    {
                        var same = other.Where(x => x.Kind == g.Key).ToList();
                        if (same.Count == 0 || same.Any(t => bases.Contains(BaseOf(t.Numeral)))) return null;
                        var names = same.Select(x => x.Label).Where(l => l is not null).ToList();
                        if (myNames.Count > 0 && names.Count > 0 && !names.Any(myNames.Contains)) return null;
                        return same.Select(x => x.Numeral).Distinct().ToList();
                    }
                    var hits = new List<(string Page, List<string> Numerals)>();
                    foreach (var (otherPage, otherLabel, otherParts) in own)
                    {
                        if (otherPage.Id == page.Id || Unrelated(otherParts) is not { } theirs) continue;
                        // Within the document a shared part name already says it (LabelWithDifferentNumerals of Check).
                        if (SameName(doc, mine.Concat(theirs))) continue;
                        hits.Add((otherLabel, theirs));
                    }
                    foreach (var (otherPage, otherParts) in foreign)
                        if (Unrelated(otherParts) is { } theirs) hits.Add((otherPage, theirs));
                    if (hits.Count == 0) continue;
                    var name = g.Select(p => p.Label).FirstOrDefault(l => l is not null);
                    var here = string.Join(", ", mine);
                    var there = string.Join(", ", hits.SelectMany(h => h.Numerals).Distinct());
                    var (en, de) = PagesText(names, hits.Select(h => h.Page));
                    issues.Add(new ReferenceIssue(ReferenceIssueKind.SamePartDifferentNumerals, here + " / " + there, name, g.SelectMany(p => p.Ids).Distinct().ToList(),
                        name is null
                            ? new I18nText($"This part has numeral {here} here but {there} on {en}. The same part keeps its numeral throughout the drawings.",
                                           $"Dieses Teil hat hier Bezugszeichen {here}, auf {de} aber {there}. Dasselbe Teil behält sein Bezugszeichen in allen Zeichnungen.")
                            : new I18nText($"Part \"{name}\" has numeral {here} here but {there} on {en}. The same part keeps its numeral throughout the drawings.",
                                           $"Teil \"{name}\" hat hier Bezugszeichen {here}, auf {de} aber {there}. Dasselbe Teil behält sein Bezugszeichen in allen Zeichnungen.")));
                }
            }
            return issues;
        }

        /// <summary>True if all <paramref name="numerals"/> carry one and the same part name in the document.</summary>
        private static bool SameName(SketchDocument doc, IEnumerable<string> numerals)
        {
            var uses = Uses(doc);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in numerals.Distinct())
            {
                var label = uses.Where(u => u.Numeral == n && u.Label is not null).Select(u => u.Label!).FirstOrDefault()
                    ?? doc.References.Values.Where(e => Key(e.Numeral) == n).Select(e => e.Label).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                if (label is null) return false;
                names.Add(label.Trim());
            }
            return names.Count == 1;
        }

        /// <summary>"slide 3" / "slides 2, 4" in both languages.</summary>
        private static (string En, string De) PagesText(PageNames names, IEnumerable<string> pages)
        {
            var list = pages.Distinct().ToList();
            var en = (list.Count == 1 ? names.One.En : names.Many.En) + " " + string.Join(", ", list);
            var de = (list.Count == 1 ? names.One.Get(Lang.De) : names.Many.Get(Lang.De)) + " " + string.Join(", ", list);
            return (en, de);
        }

        /// <summary>
        /// Numbered parts: box objects a numeral's lead line is attached to, and box objects carrying a numeral themselves
        /// (<see cref="SketchObject.Ref"/>, set by automatic numbering). <c>NumeralId</c> is the numeral object, if any.
        /// </summary>
        public static IReadOnlyList<(string Numeral, SketchObject Part, string? NumeralId)> NumberedParts(SketchDocument doc)
        {
            var list = new List<(string, SketchObject, string?)>();
            foreach (var page in doc.Pages)
                foreach (var o in doc.ObjectsOn(page))
                {
                    if (IsNumeralType(o.Type))
                    {
                        if (TargetOf(doc, o) is { } part && NumeralOf(o) is { } n && !string.IsNullOrWhiteSpace(n)) list.Add((Key(n), part, o.Id));
                    }
                    else if (!o.IsPointGeometry && !string.IsNullOrWhiteSpace(o.Ref)) list.Add((Key(o.Ref!), o, null));
                }
            return list;
        }

        /// <summary>The existing box object the lead line of a numeral is attached to, or null.</summary>
        public static SketchObject? TargetOf(SketchDocument doc, SketchObject numeral)
            => numeral.Attach is { Count: > 0 } att && att[0] is { } a && doc.Get(a.ObjectId) is { IsPointGeometry: false } t ? t : null;

        /// <summary>
        /// Kind of a part for numbering: type, text and semantic parameters (size, position and view do not count, so copies
        /// of one part and the same part in another figure are the same kind, like one row of the parts list).
        /// </summary>
        public static string PartKind(SketchObject o)
            => o.Type + "|" + (o.Text ?? "") + "|" + string.Join(";", o.Params.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + (p.Value.ToString() ?? "")));
    }
}
