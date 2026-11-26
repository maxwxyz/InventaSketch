using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Patent
{
    /// <summary>How automatic numbering treats several parts of the same kind (same type and parameters).</summary>
    public enum RepeatedParts
    {
        /// <summary>Identical parts share one numeral (same feature, same sign).</summary>
        Shared,
        /// <summary>Identical parts get variants of one numeral: 10a, 10b, …</summary>
        Suffixed,
        /// <summary>Every part gets its own numeral.</summary>
        Separate,
    }

    /// <summary>Options of <see cref="PartNumbering.NumberParts"/>.</summary>
    public sealed record NumberingOptions
    {
        public NumeralScheme Scheme { get; init; } = NumeralScheme.Default;
        public RepeatedParts Repeated { get; init; } = RepeatedParts.Shared;
        /// <summary>Language of the part names written into the reference list.</summary>
        public Lang Lang { get; init; } = Lang.En;
        /// <summary>Distance of the numeral from the part's bounding box (mm).</summary>
        public double Offset { get; init; } = 10;
    }

    /// <summary>A part numbered by <see cref="PartNumbering.NumberParts"/>.</summary>
    public sealed record NumberedPart(string PartId, string Numeral, string NumeralObjectId);

    /// <summary>
    /// Automatic numerals from the parts list: every part on a page (<see cref="PartsList.IsPart"/>) gets a reference
    /// numeral with a lead line, in reading order (rows top to bottom, left to right within a row), following the numbering
    /// scheme. Parts that already have a numeral are skipped. The result is one document edit, so hosts record it as one
    /// undo step.
    /// </summary>
    public static class PartNumbering
    {
        public const string ActionKey = "patent.numerals.auto";

        /// <summary>
        /// The parts of a page in reading order: parts form a row when their vertical centre lies within the height of the
        /// row's first (topmost) part; rows run top to bottom, parts within a row left to right (z-order breaks ties).
        /// </summary>
        public static IReadOnlyList<SketchObject> PartsInReadingOrder(Registry registry, SketchDocument doc, Page page)
        {
            var parts = doc.ObjectsOn(page).Select((o, i) => (Obj: o, Z: i)).Where(x => PartsList.IsPart(registry, x.Obj))
                .Select(x => (x.Obj, x.Z, B: x.Obj.Bounds())).OrderBy(x => x.B.Y + x.B.H / 2).ThenBy(x => x.B.X).ThenBy(x => x.Z).ToList();
            var result = new List<SketchObject>();
            var i = 0;
            while (i < parts.Count)
            {
                var first = parts[i].B;
                var row = new List<(SketchObject Obj, int Z, Rect B)> { parts[i++] };
                while (i < parts.Count && parts[i].B.Y + parts[i].B.H / 2 <= first.Y + first.H + Geo.Eps) row.Add(parts[i++]);
                result.AddRange(row.OrderBy(x => x.B.X + x.B.W / 2).ThenBy(x => x.Z).Select(x => x.Obj));
            }
            return result;
        }

        /// <summary>True if any part on the page still lacks a numeral.</summary>
        public static bool HasUnnumbered(Registry registry, SketchDocument doc, Page page)
        {
            var numbered = NumberedPartIds(doc);
            return PartsInReadingOrder(registry, doc, page).Any(p => !numbered.Contains(p.Id));
        }

        /// <summary>
        /// Numbers all parts on <paramref name="pageId"/> that have no numeral yet (see <see cref="PartNumbering"/>). A part
        /// that already carries a numeral of its own (<see cref="SketchObject.Ref"/>) keeps it and only gets the lead line.
        /// New numerals are written to the part (parts list), to the numeral object and to the reference list (part name).
        /// Returns the unchanged document instance when there is nothing to number.
        /// </summary>
        public static (SketchDocument Doc, IReadOnlyList<NumberedPart> Numbered) NumberParts(Registry registry, SketchDocument doc, string pageId, NumberingOptions? options = null)
        {
            options ??= new NumberingOptions();
            var page = doc.Pages.FirstOrDefault(p => p.Id == pageId) ?? throw new KeyNotFoundException($"Page '{pageId}' not found.");
            var parts = PartsInReadingOrder(registry, doc, page);
            var numbered = NumberedPartIds(doc);
            // Numerals already used per part kind (shared or suffixed numbering continues them).
            var byKind = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (n, part, _) in ReferenceNumerals.NumberedParts(doc))
            {
                var k = ReferenceNumerals.PartKind(part);
                if (!byKind.ContainsKey(k)) byKind[k] = n;
            }
            var open = parts.Where(p => !numbered.Contains(p.Id)).ToList();
            var openPerKind = open.GroupBy(ReferenceNumerals.PartKind).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var result = new List<NumberedPart>();
            foreach (var part in open)
            {
                var kind = ReferenceNumerals.PartKind(part);
                string numeral;
                if (!string.IsNullOrWhiteSpace(part.Ref)) numeral = ReferenceNumerals.Key(part.Ref!);
                else if (options.Repeated == RepeatedParts.Shared && byKind.TryGetValue(kind, out var shared)) numeral = shared;
                else if (options.Repeated == RepeatedParts.Suffixed && (byKind.TryGetValue(kind, out var known) || openPerKind[kind] > 1))
                {
                    var number = known is null ? ReferenceNumerals.NextFree(doc, options.Scheme) : ReferenceNumerals.BaseOf(known);
                    numeral = NextVariant(doc, number);
                }
                else numeral = ReferenceNumerals.NextFree(doc, options.Scheme);
                if (!byKind.ContainsKey(kind)) byKind[kind] = numeral;

                var label = UniqueLabel(doc, Exports.Label(registry, part, options.Lang), numeral);
                var (attach, at) = Placement(doc, page, part, options.Offset);
                var (d, n) = ReferenceNumerals.Insert(registry, doc, pageId, attach, at, label, part.Id, numeral, options.Scheme);
                doc = Edits.Replace(d, d.Get(part.Id)! with { Ref = numeral });
                result.Add(new NumberedPart(part.Id, numeral, n.Id));
            }
            return (doc, result);
        }

        /// <summary>
        /// The part name for the reference list: the element's name, made unique with a counter ("Spur gear 2") when another
        /// number already uses it (different kinds of one element type would otherwise share a name). The variants of
        /// one number share their name.
        /// </summary>
        private static string UniqueLabel(SketchDocument doc, string name, string numeral)
        {
            var number = ReferenceNumerals.BaseOf(numeral);
            var taken = new HashSet<string>(doc.References.Values.Where(e => ReferenceNumerals.BaseOf(e.Numeral) != number).Select(e => (e.Label ?? "").Trim())
                .Concat(ReferenceNumerals.Uses(doc).Where(u => u.Label is not null && ReferenceNumerals.BaseOf(u.Numeral) != number).Select(u => u.Label!)), StringComparer.OrdinalIgnoreCase);
            var own = doc.References.Values.Where(e => ReferenceNumerals.BaseOf(e.Numeral) == number && !string.IsNullOrWhiteSpace(e.Label)).Select(e => e.Label.Trim()).FirstOrDefault()
                ?? ReferenceNumerals.Uses(doc).Where(u => u.Label is not null && ReferenceNumerals.BaseOf(u.Numeral) == number).Select(u => u.Label).FirstOrDefault();
            if (own is not null) return own;
            if (!taken.Contains(name)) return name;
            for (var k = 2; ; k++)
                if (!taken.Contains(name + " " + k.ToString(System.Globalization.CultureInfo.InvariantCulture))) return name + " " + k.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The next unused letter variant of a number ("10a", then "10b" …); "10" itself counts as taken.</summary>
        private static string NextVariant(SketchDocument doc, string number)
        {
            var used = new HashSet<string>(ReferenceNumerals.Uses(doc).Select(u => u.Numeral)
                .Concat(doc.References.Keys.Select(ReferenceNumerals.Key))
                .Concat(doc.Objects.Values.Where(o => !o.IsPointGeometry && !string.IsNullOrEmpty(o.Ref)).Select(o => ReferenceNumerals.Key(o.Ref!))), StringComparer.Ordinal);
            for (var c = 'a'; c <= 'z'; c++)
                if (!used.Contains(number + c)) return number + c;
            for (var primes = "'"; ; primes += "'")
                if (!used.Contains(number + primes)) return number + primes;
        }

        /// <summary>
        /// Lead line of a new numeral: it starts inside the part (a quarter towards the outer upper corner, so it also
        /// meets round parts) and ends beyond that corner, on the side of the page half the part sits in.
        /// </summary>
        private static (Vec2 Attach, Vec2 Numeral) Placement(SketchDocument doc, Page page, SketchObject part, double offset)
        {
            var b = part.Bounds();
            var side = b.X + b.W / 2 <= page.Size.W / 2 ? -1 : 1;
            var attach = part.BoxPoint(side * 0.2, -0.2);
            var x = side > 0 ? b.X + b.W + offset : b.X - offset;
            var y = b.Y - offset * 0.6;
            const double edge = 8;
            x = Math.Max(edge, Math.Min(page.Size.W - edge, x));
            y = Math.Max(edge, Math.Min(page.Size.H - edge, y));
            return (attach, new Vec2(x, y));
        }

        /// <summary>Ids of parts with a numeral object attached (on any page).</summary>
        private static HashSet<string> NumberedPartIds(SketchDocument doc)
            => new(ReferenceNumerals.NumberedParts(doc).Where(x => x.NumeralId is not null).Select(x => x.Part.Id), StringComparer.Ordinal);

        /// <summary>The menu "Number all parts ▸" (one menu of commands, nothing current).</summary>
        public static readonly ActionChoice Menu = new(ActionKey, new I18nText("Number all parts", "Alle Teile nummerieren")) { IsMenu = true };

        /// <summary>The options of <see cref="Menu"/>: value, numbering scheme and label.</summary>
        private static readonly (string Value, NumeralScheme Scheme, I18nText Label)[] Schemes =
        {
            ("tens", NumeralScheme.Default, new I18nText("10, 20, 30 …", "10, 20, 30 …")),
            ("ones", NumeralScheme.Consecutive, new I18nText("1, 2, 3 …", "1, 2, 3 …")),
        };

        /// <summary>
        /// "Number all parts ▸ 10, 20, 30 … / 1, 2, 3 …" as commands of the whole page, without a selected element (hosts
        /// show them with their page-level commands; the reference numeral offers the same menu, see <see cref="Actions"/>).
        /// Only for a page with parts (nothing otherwise: no command that cannot do anything); greyed out
        /// (<see cref="PageCommand.IsEnabled"/> false) when every part of the page already has a numeral. Ids are
        /// "patent.numerals.auto=tens" and "=ones", as on the numeral.
        /// </summary>
        public static IReadOnlyList<PageCommand> PageCommands(Registry registry, SketchDocument doc, Page page)
        {
            var parts = PartsInReadingOrder(registry, doc, page);
            if (parts.Count == 0) return Array.Empty<PageCommand>();
            var numbered = NumberedPartIds(doc);
            var enabled = parts.Any(p => !numbered.Contains(p.Id));
            return Schemes.Select(s =>
            {
                var scheme = s.Scheme;
                return new PageCommand(ActionKey + ActionChoices.Separator + s.Value, s.Label, Menu, enabled,
                    (reg, d, pageId, lang) => NumberParts(reg, d, pageId, new NumberingOptions { Scheme = scheme, Lang = lang }).Doc);
            }).ToList();
        }

        /// <summary>
        /// "Number all parts ▸ 10, 20, 30 … / 1, 2, 3 …": document actions on the page of <paramref name="o"/>; greyed out
        /// when every part on the page already has a numeral.
        /// </summary>
        public static IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx, Registry? registry)
        {
            registry = ctx.Registry ?? registry;
            var page = ctx.Doc.PageOf(o.Id);
            if (registry is null || page is null) yield break;
            var enabled = HasUnnumbered(registry, ctx.Doc, page);
            foreach (var (value, scheme, label) in Schemes)
            {
                var reg = registry;
                yield return ActionChoices.Option(Menu, value, label, IconIds.Option, false, x => x) with
                {
                    IsEnabled = enabled,
                    ApplyInDocument = (d, x, c) => d.PageOf(x.Id) is { } p
                        ? NumberParts(c.Registry ?? reg, d, p.Id, new NumberingOptions { Scheme = scheme, Lang = c.Lang }).Doc
                        : d,
                };
            }
        }
    }

    /// <summary>
    /// A command of a whole page, run without a selected element (<see cref="PartNumbering.PageCommands"/>): id
    /// ("key=value" of its <see cref="Choice"/>), label, the menu it belongs to, whether it can do anything now, and the
    /// edit (registry, document, page id, language of new texts) – one document edit, so hosts record one undo step.
    /// </summary>
    public sealed record PageCommand(string Id, I18nText Label, ActionChoice Choice, bool IsEnabled, Func<Registry, SketchDocument, string, Lang, SketchDocument> Run);
}
