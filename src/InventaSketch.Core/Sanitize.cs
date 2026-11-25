using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Clean slides for sharing: before slides leave a company, charts and other elements must not
    // reveal real values. Each type decides what its values are (ObjectType.Sanitize); this file
    // holds the options, the generic text/metadata handling, the report and the document pass.
    // =============================================================================================

    /// <summary>How values are made unrecognisable.</summary>
    public enum SanitizeMode
    {
        /// <summary>Values stay; only the text and metadata options apply.</summary>
        None,
        /// <summary>Same proportions and shapes, values mapped to a neutral scale (0…1, relative order).</summary>
        Normalised,
        /// <summary>Plausible random values with the same look (same counts, similar range and trend).</summary>
        Randomised,
    }

    /// <summary>Options of <see cref="Sanitizer.Apply"/>.</summary>
    public sealed record SanitizeOptions
    {
        public SanitizeMode Mode { get; init; } = SanitizeMode.Normalised;
        /// <summary>Category names of charts become "A", "B", "C" …</summary>
        public bool RenameCategories { get; init; }
        /// <summary>Titles, labels, notes, series names and free texts become neutral placeholders.</summary>
        public bool ReplaceTexts { get; init; }
        /// <summary>Element metadata (<see cref="SketchObject.Meta"/>: provenance, import sources) is removed.</summary>
        public bool StripMeta { get; init; }
        /// <summary>Random seed for reproducible results; null = a new seed (reported in <see cref="SanitizeReport.Seed"/>).</summary>
        public int? Seed { get; init; }
        /// <summary>Relative variation of randomised values (0.25 = ±25 %), clamped to 0.02…0.9.</summary>
        public double Spread { get; init; } = 0.25;
        /// <summary>Language of placeholders ("Series 1" / "Reihe 1").</summary>
        public Lang Lang { get; init; } = Lang.En;

        /// <summary><see cref="Spread"/> clamped to its valid range.</summary>
        public double Variation => Geo.Clamp(double.IsNaN(Spread) ? 0.25 : Spread, 0.02, 0.9);

        /// <summary>Reads a mode name: "normalised"/"normalized", "randomised"/"randomized", "none" (anything else: null).</summary>
        public static SanitizeMode? ParseMode(string? text)
        {
            var t = (text ?? "").Trim().ToLowerInvariant();
            if (t.StartsWith("normali", StringComparison.Ordinal) || t == "normalise" || t == "normalize") return SanitizeMode.Normalised;
            if (t.StartsWith("randomi", StringComparison.Ordinal) || t == "random") return SanitizeMode.Randomised;
            if (t == "none" || t == "texts" || t == "off") return SanitizeMode.None;
            return null;
        }
    }

    /// <summary>One line of the report: the changed elements of one type.</summary>
    public sealed record SanitizeTypeCount(string TypeId, I18nText Name, int Count);

    /// <summary>What <see cref="Sanitizer.Apply"/> changed (counts per kind and per element type).</summary>
    public sealed record SanitizeReport
    {
        public SanitizeMode Mode { get; init; }
        /// <summary>Seed used for the random numbers (pass it again to reproduce the result).</summary>
        public int Seed { get; init; }
        /// <summary>Elements looked at.</summary>
        public int Elements { get; init; }
        /// <summary>Elements changed in any way.</summary>
        public int Changed { get; init; }
        /// <summary>Elements whose values or data changed (chart data, rated values, dimension values …).</summary>
        public int Values { get; init; }
        /// <summary>Elements whose texts were replaced.</summary>
        public int Texts { get; init; }
        /// <summary>Elements whose metadata was removed.</summary>
        public int Metadata { get; init; }
        /// <summary>Entries of the reference numeral list whose names were replaced.</summary>
        public int References { get; init; }
        /// <summary>Elements of a type that is not registered (left as they are).</summary>
        public int Unknown { get; init; }
        /// <summary>Elements whose type failed to clean (left as they are).</summary>
        public int Failed { get; init; }
        /// <summary>Changed elements per type, most frequent first.</summary>
        public IReadOnlyList<SanitizeTypeCount> ByType { get; init; } = Array.Empty<SanitizeTypeCount>();

        public bool Any => Changed > 0 || References > 0;

        /// <summary>One sentence: "5 of 7 elements cleaned (normalised): 3 with values, 2 with texts …".</summary>
        public I18nText Summary => new(Sentence(Lang.En), Sentence(Lang.De));

        /// <summary>The summary followed by one line per element type.</summary>
        public IReadOnlyList<I18nText> Lines
        {
            get
            {
                var list = new List<I18nText> { Summary };
                foreach (var t in ByType) list.Add(new I18nText(t.Name.Get(Lang.En) + ": " + N(t.Count), t.Name.Get(Lang.De) + ": " + N(t.Count)));
                if (Unknown > 0) list.Add(new I18nText(N(Unknown) + " element(s) of unknown type left unchanged", N(Unknown) + " Element(e) unbekannten Typs unverändert"));
                if (Failed > 0) list.Add(new I18nText(N(Failed) + " element(s) could not be cleaned and were left unchanged", N(Failed) + " Element(e) konnten nicht bereinigt werden und blieben unverändert"));
                return list;
            }
        }

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        private string Sentence(Lang lang)
        {
            var de = lang == Lang.De;
            var mode = Mode switch
            {
                SanitizeMode.Normalised => de ? "normiert" : "normalised",
                SanitizeMode.Randomised => de ? "zufällig" : "randomised",
                _ => de ? "nur Texte" : "texts only",
            };
            if (!Any) return de ? "Nichts zu bereinigen (" + mode + ")." : "Nothing to clean (" + mode + ").";
            var parts = new List<string>();
            if (Values > 0) parts.Add(de ? N(Values) + " mit Werten" : N(Values) + " with values");
            if (Texts > 0) parts.Add(de ? N(Texts) + " mit Texten" : N(Texts) + " with texts");
            if (Metadata > 0) parts.Add(de ? N(Metadata) + " mit Metadaten" : N(Metadata) + " with metadata");
            if (References > 0) parts.Add(de ? N(References) + " Bezugszeichennamen" : N(References) + " reference names");
            var head = de
                ? N(Changed) + " von " + N(Elements) + " Elementen bereinigt (" + mode + ")"
                : N(Changed) + " of " + N(Elements) + " elements cleaned (" + mode + ")";
            return head + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "") + (de ? "; Startwert " : "; seed ") + Seed.ToString(CultureInfo.InvariantCulture) + ".";
        }
    }

    /// <summary>Text helpers of the cleaning pass: placeholders, letters, number scaling and rounding.</summary>
    public static class SanitizeText
    {
        /// <summary>
        /// Text parameters that hold free wording (titles, labels, names, notes). The generic pass replaces them; structured
        /// text parameters (data, lists, ranges, specifications) are left to the types.
        /// </summary>
        public static readonly IReadOnlyCollection<string> LabelKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "text", "label", "name", "title", "caption", "note", "labelA", "labelB", "tipLabel", "tailLabel", "labelForward", "labelBack",
            "xlabel", "ylabel",
        };

        private static readonly string[] Words =
        {
            "lorem", "ipsum", "dolor", "sit", "amet", "consetetur", "sadipscing", "elitr", "sed", "diam", "nonumy", "eirmod",
            "tempor", "invidunt", "ut", "labore", "et", "dolore", "magna", "aliquyam", "erat", "voluptua", "at", "vero", "eos",
            "accusam", "justo", "duo", "dolores", "ea", "rebum", "stet", "clita", "kasd", "gubergren",
        };

        /// <summary>
        /// A neutral placeholder of about the same shape: the same lines, the same number of words per line (at most 12), in
        /// "lorem ipsum" words; a capital first letter stays capital. Empty text stays empty.
        /// </summary>
        public static string Placeholder(string? text, Random rng)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var lines = text!.Replace("\r\n", "\n").Split('\n');
            var start = rng.Next(Words.Length);
            var sb = new StringBuilder();
            for (var l = 0; l < lines.Length; l++)
            {
                if (l > 0) sb.Append('\n');
                var count = Math.Min(12, lines[l].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length);
                for (var i = 0; i < count; i++)
                {
                    var w = Words[(start + i) % Words.Length];
                    if (i == 0 && lines[l].TrimStart() is { Length: > 0 } t && char.IsUpper(t[0])) w = char.ToUpperInvariant(w[0]) + w.Substring(1);
                    if (i > 0) sb.Append(' ');
                    sb.Append(w);
                }
                start += count;
            }
            var result = sb.ToString();
            return result.Trim().Length == 0 ? "Lorem" : result;
        }

        /// <summary>
        /// True for designators and numbers rather than wording: no letter at all ("10", "1.2"), or short (at most 8
        /// characters) with a digit ("FIG. 1", "R1", "U12"). Such texts are not replaced by placeholders.
        /// </summary>
        public static bool IsIdentifier(string? text)
        {
            var t = (text ?? "").Trim();
            return !t.Any(char.IsLetter) || t.Length <= 8 && t.Any(char.IsDigit);
        }

        /// <summary>"A", "B" … "Z", "AA", "AB" … for index 0, 1 …</summary>
        public static string Letter(int index)
        {
            var s = "";
            for (var i = index; i >= 0; i = i / 26 - 1) s = (char)('A' + i % 26) + s;
            return s;
        }

        /// <summary>Rounds to <paramref name="digits"/> significant digits (0 stays 0; NaN and infinity unchanged).</summary>
        public static double Round(double v, int digits = 3)
        {
            if (v == 0 || double.IsNaN(v) || double.IsInfinity(v)) return v;
            var mag = Math.Floor(Math.Log10(Math.Abs(v)));
            var scale = Math.Pow(10, digits - 1 - mag);
            var r = Math.Round(v * scale) / scale;
            return Math.Abs(r) < 1e-300 ? 0 : r;
        }

        /// <summary>Uniform random number in [a, b).</summary>
        public static double Uniform(Random rng, double a, double b) => a + (b - a) * rng.NextDouble();

        /// <summary>Random factor between 1/<paramref name="max"/> and <paramref name="max"/>, uniform on a log scale.</summary>
        public static double Factor(Random rng, double max) => Math.Exp(Uniform(rng, -Math.Log(max), Math.Log(max)));

        /// <summary>
        /// Random factor at least 20 % away from 1: between 1.2 and 1.2 + 2·spread·<paramref name="reach"/>, or the inverse
        /// (values made unrecognisable, never almost unchanged).
        /// </summary>
        public static double Away(Random rng, double spread, double reach = 1)
        {
            var lo = Math.Log(1.2);
            var hi = Math.Max(lo + 0.01, Math.Log(1.2 + 2 * spread * reach));
            var m = Uniform(rng, lo, hi);
            return Math.Exp(rng.NextDouble() < 0.5 ? -m : m);
        }

        /// <summary>Random factor 1 ± <paramref name="spread"/> (at least 0.05).</summary>
        public static double Vary(Random rng, double spread) => Math.Max(0.05, 1 + Uniform(rng, -spread, spread));

        private static readonly Regex NumberPattern = new(@"(?<![\p{L}\d.,])[-−]?\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant);

        /// <summary>
        /// Every number in <paramref name="text"/> mapped through <paramref name="map"/> ("10 kΩ" → "12 kΩ", "2,5 m" → "3,1 m"):
        /// the decimal separator and the number of decimals are kept (an integer stays an integer). Numbers glued to a
        /// preceding letter (designators such as "R1") are left alone. <paramref name="decimals"/> overrides the number of
        /// decimals (normalised values 0…1).
        /// </summary>
        public static string MapNumbers(string? text, Func<double, double> map, int? decimals = null)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            return NumberPattern.Replace(text!, m =>
            {
                var s = m.Value;
                var comma = s.IndexOf(',') >= 0;
                var t = s.Replace('−', '-').Replace(',', '.');
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return s;
                var dot = t.IndexOf('.');
                var dec = decimals ?? (dot < 0 ? 0 : t.Length - dot - 1);
                var w = map(v);
                if (double.IsNaN(w) || double.IsInfinity(w)) return s;
                var r = Math.Round(w, dec).ToString(dec == 0 ? "0" : "0." + new string(decimals is null ? '0' : '#', dec), CultureInfo.InvariantCulture);
                if (r.Trim('-', '0', '.').Length == 0 && r.StartsWith("-", StringComparison.Ordinal)) r = r.Substring(1);
                if (s.StartsWith("−", StringComparison.Ordinal) && r.StartsWith("-", StringComparison.Ordinal)) r = "−" + r.Substring(1);
                return comma ? r.Replace('.', ',') : r;
            });
        }

        /// <summary>True if <paramref name="text"/> contains a number <see cref="MapNumbers"/> would change.</summary>
        public static bool HasNumber(string? text) => !string.IsNullOrEmpty(text) && NumberPattern.IsMatch(text!);

        /// <summary>Stable 32-bit hash of a string (FNV-1a; <see cref="string.GetHashCode()"/> differs between runs).</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var c in s) { h ^= c; h *= 16777619u; }
                return (int)(h & 0x7fffffff);
            }
        }
    }

    /// <summary>
    /// The cleaning pass ("Clean slides for sharing"): every element is passed to its type's
    /// <see cref="ObjectType.Sanitize"/>, which normalises or randomises its values and applies the text and metadata
    /// options. The result is a new document (one undo step in the engine) and a report of what changed.
    /// </summary>
    public static class Sanitizer
    {
        /// <summary>
        /// The generic part used by <see cref="ObjectType.Sanitize"/>: replaces the free texts (<see cref="SketchObject.Text"/>,
        /// except on numeral types where it is the numeral, and the <see cref="SanitizeText.LabelKeys"/> text parameters that
        /// are set; designators such as "FIG. 1" or "R1" stay, <see cref="SanitizeText.IsIdentifier"/>) when
        /// <see cref="SanitizeOptions.ReplaceTexts"/> is on, and removes <see cref="SketchObject.Meta"/> when
        /// <see cref="SanitizeOptions.StripMeta"/> is on. Returns <paramref name="o"/> itself when nothing changes.
        /// </summary>
        public static SketchObject Basic(ObjectType type, SketchObject o, SanitizeOptions opt, Random rng)
        {
            var result = o;
            if (opt.ReplaceTexts)
            {
                var numeral = type.Parameters.Any(p => p.Key == "numeral");
                if (!numeral && !string.IsNullOrWhiteSpace(o.Text) && !SanitizeText.IsIdentifier(o.Text)) result = result with { Text = SanitizeText.Placeholder(o.Text, rng) };
                Dictionary<string, ParamValue>? ps = null;
                foreach (var p in type.Parameters)
                {
                    if (p.Kind != ParamKind.Text || !SanitizeText.LabelKeys.Contains(p.Key)) continue;
                    if (!o.Params.TryGetValue(p.Key, out var v) || string.IsNullOrWhiteSpace(v.Text) || SanitizeText.IsIdentifier(v.Text)) continue;
                    ps ??= o.Params.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                    ps[p.Key] = SanitizeText.Placeholder(v.Text, rng);
                }
                if (ps is not null) result = result with { Params = ps };
            }
            if (opt.StripMeta && o.Meta is not null) result = result with { Meta = null };
            return result;
        }

        /// <summary>
        /// Cleans the elements <paramref name="ids"/> of <paramref name="doc"/> (unknown ids ignored). Every element gets its
        /// own random generator from the seed and its id, so the result is reproducible for a given seed. With
        /// <see cref="SanitizeOptions.ReplaceTexts"/> the names of the reference numerals used by these elements are
        /// replaced too ("Part 10"). Never throws for a type that fails: that element stays as it is and is counted in
        /// <see cref="SanitizeReport.Failed"/>. Returns <paramref name="doc"/> itself when nothing changes.
        /// </summary>
        public static (SketchDocument Doc, SanitizeReport Report) Apply(Registry registry, SketchDocument doc, IEnumerable<string> ids, SanitizeOptions options)
        {
            var seed = options.Seed ?? new Random().Next(1, int.MaxValue);
            var list = ids.Where(doc.Objects.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
            var replaced = new Dictionary<string, SketchObject>(StringComparer.Ordinal);
            int values = 0, texts = 0, meta = 0, unknown = 0, failed = 0;
            var byType = new Dictionary<string, int>(StringComparer.Ordinal);
            var numerals = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in list)
            {
                var o = doc.Objects[id];
                if (!string.IsNullOrEmpty(o.Ref)) numerals.Add(o.Ref!);
                if (o.Str("numeral", "") is { Length: > 0 } nm) numerals.Add(nm);
                var type = registry.Type(o.Type);
                if (type is null) { unknown++; continue; }
                var rng = new Random(unchecked(seed * 31 + SanitizeText.StableHash(o.Id)));
                SketchObject s;
                try { s = type.Sanitize(o, options, rng); }
                catch (Exception) { failed++; continue; }
                if (s is null || ReferenceEquals(s, o) || Same(o, s)) continue;
                s = s with { Id = o.Id };
                var (v, t, m) = Classify(o, s);
                if (v) values++;
                if (t) texts++;
                if (m) meta++;
                replaced[id] = s;
                byType[o.Type] = byType.TryGetValue(o.Type, out var c) ? c + 1 : 1;
            }

            var refs = 0;
            var result = doc;
            if (replaced.Count > 0) result = Edits.Update(result, replaced.Keys.ToList(), o => replaced[o.Id]);
            if (options.ReplaceTexts && numerals.Count > 0 && doc.References.Count > 0)
            {
                var entries = doc.References.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                foreach (var n in numerals)
                {
                    if (!entries.TryGetValue(n, out var e)) continue;
                    var label = (options.Lang == Lang.De ? "Teil " : "Part ") + n;
                    if (e.Label == label && e.Note is null) continue;
                    entries[n] = e with { Label = label, Note = null };
                    refs++;
                }
                if (refs > 0) result = result with { References = entries };
            }

            var report = new SanitizeReport
            {
                Mode = options.Mode,
                Seed = seed,
                Elements = list.Count,
                Changed = replaced.Count,
                Values = values,
                Texts = texts,
                Metadata = meta,
                References = refs,
                Unknown = unknown,
                Failed = failed,
                ByType = byType.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => new SanitizeTypeCount(kv.Key, registry.Type(kv.Key)?.Name ?? new I18nText(kv.Key), kv.Value)).ToList(),
            };
            return (result, report);
        }

        /// <summary>All elements of a page (convenience for "clean this slide").</summary>
        public static (SketchDocument Doc, SanitizeReport Report) ApplyToPage(Registry registry, SketchDocument doc, string pageId, SanitizeOptions options)
        {
            var page = doc.Pages.FirstOrDefault(p => p.Id == pageId) ?? throw new KeyNotFoundException($"Page '{pageId}' not found.");
            return Apply(registry, doc, page.Order, options);
        }

        private static bool Same(SketchObject a, SketchObject b)
            => a.Text == b.Text && ReferenceEquals(a.Meta, b.Meta) && a.Ref == b.Ref && a.Size == b.Size && a.Transform == b.Transform
               && ParamsEqual(a.Params, b.Params) && SamePoints(a.Points, b.Points) && ReferenceEquals(a.Parts, b.Parts);

        private static bool SamePoints(IReadOnlyList<Vec2>? a, IReadOnlyList<Vec2>? b)
            => ReferenceEquals(a, b) || a is not null && b is not null && a.SequenceEqual(b);

        private static bool ParamsEqual(IReadOnlyDictionary<string, ParamValue> a, IReadOnlyDictionary<string, ParamValue> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            foreach (var kv in a) if (!b.TryGetValue(kv.Key, out var v) || !v.Equals(kv.Value)) return false;
            return true;
        }

        /// <summary>What kind of change turned <paramref name="a"/> into <paramref name="b"/>: values, texts, metadata.</summary>
        private static (bool Values, bool Texts, bool Meta) Classify(SketchObject a, SketchObject b)
        {
            var texts = a.Text != b.Text;
            var values = a.Size != b.Size || !SamePoints(a.Points, b.Points) || !ReferenceEquals(a.Parts, b.Parts);
            foreach (var key in a.Params.Keys.Union(b.Params.Keys))
            {
                a.Params.TryGetValue(key, out var x);
                b.Params.TryGetValue(key, out var y);
                if (x.Equals(y)) continue;
                if (SanitizeText.LabelKeys.Contains(key)) texts = true;
                else values = true;
            }
            var meta = !ReferenceEquals(a.Meta, b.Meta);
            return (values, texts, meta);
        }
    }
}
