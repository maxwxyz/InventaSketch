using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Patent
{
    /// <summary>
    /// A reference numeral used on another page or slide of the same document: the numeral, the part name it stands for
    /// (numeral object or reference list) and the kind of part it points at (<see cref="ReferenceNumerals.PartKind"/>: the
    /// identity of the parts list, type, text and parameters such as the designation); <c>Kind</c> is null for a numeral
    /// that points at no part and for a reference list entry.
    /// </summary>
    public sealed record OtherNumeral(string Numeral, string? Label = null, string? Kind = null);

    /// <summary>
    /// The numerals of another page or slide (<see cref="Page"/>: the host's label of it, "2"). Hosts that keep every page
    /// or slide in a model of its own (the PowerPoint add-ins: one model per slide; LibreOffice: per page) pass these into
    /// automatic numbering (<see cref="NumberingOptions.Elsewhere"/>) and the preflight
    /// (<see cref="PreflightOptions.Elsewhere"/>), so numbering continues across the document and identical parts keep one
    /// numeral on every page.
    /// </summary>
    public sealed record OtherPage(string Page, IReadOnlyList<OtherNumeral> Numerals)
    {
        /// <summary>The numerals of <paramref name="doc"/> (all its pages) as the other page <paramref name="page"/>.</summary>
        public static OtherPage Of(string page, SketchDocument doc) => new(page, NumeralsElsewhere.Of(doc));
    }

    /// <summary>Summaries of the numerals of a document for <see cref="OtherPage"/>.</summary>
    public static class NumeralsElsewhere
    {
        /// <summary>
        /// The numerals of <paramref name="doc"/>: every numbered part (numeral, part name, part kind), every drawn numeral
        /// without a part and every reference list entry (numeral and name), each pair of numeral and kind once.
        /// </summary>
        public static IReadOnlyList<OtherNumeral> Of(SketchDocument doc)
        {
            var list = new List<OtherNumeral>();
            var seen = new HashSet<(string, string?)>();
            void Add(string numeral, string? label, string? kind)
            {
                numeral = ReferenceNumerals.Key(numeral ?? "").Trim();
                if (numeral.Length == 0 || !seen.Add((numeral, kind))) return;
                list.Add(new OtherNumeral(numeral, string.IsNullOrWhiteSpace(label) ? null : label!.Trim(), kind));
            }
            string? Listed(string n) => doc.References.Values.Where(e => ReferenceNumerals.Key(e.Numeral) == n).Select(e => e.Label).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            var parted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (n, part, numeralId) in ReferenceNumerals.NumberedParts(doc))
            {
                var label = (numeralId is not null && doc.Get(numeralId) is { } no ? ReferenceNumerals.LabelOf(no) : null) ?? Listed(n);
                Add(n, label, ReferenceNumerals.PartKind(part));
                if (numeralId is not null) parted.Add(numeralId);
            }
            foreach (var use in ReferenceNumerals.Uses(doc))
                if (!parted.Contains(use.ObjectId)) Add(use.Numeral, use.Label ?? Listed(use.Numeral), null);
            foreach (var e in doc.References.Values) Add(e.Numeral, e.Label, null);
            // A numeral listed with a part needs no second line without one.
            var withKind = new HashSet<string>(list.Where(x => x.Kind is not null).Select(x => x.Numeral), StringComparer.Ordinal);
            return list.Where(x => x.Kind is not null || !withKind.Contains(x.Numeral)).ToList();
        }

        /// <summary>
        /// <paramref name="doc"/> with the numerals of the other pages as reference list entries (an entry of its own wins):
        /// the document numbering works on, so new numerals continue after every numeral of the document.
        /// </summary>
        internal static SketchDocument WithUsed(SketchDocument doc, IReadOnlyList<OtherPage>? others)
        {
            if (others is null || others.Count == 0) return doc;
            var refs = doc.References.ToDictionary(kv => kv.Key, kv => kv.Value);
            var keys = new HashSet<string>(refs.Keys.Select(ReferenceNumerals.Key), StringComparer.Ordinal);
            foreach (var n in others.SelectMany(p => p.Numerals))
            {
                if (keys.Add(n.Numeral)) refs[n.Numeral] = new ReferenceEntry(n.Numeral, n.Label ?? "");
                else if (!string.IsNullOrWhiteSpace(n.Label) && refs.TryGetValue(n.Numeral, out var e) && string.IsNullOrWhiteSpace(e.Label)) refs[n.Numeral] = e with { Label = n.Label! };
            }
            return refs.Count == doc.References.Count && refs.All(kv => doc.References.TryGetValue(kv.Key, out var v) && v == kv.Value) ? doc : doc with { References = refs };
        }
    }
}
