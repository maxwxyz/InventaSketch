using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InventaSketch.Core
{
    /// <summary>
    /// Unit lookup (Calc 0): input spellings to <see cref="Unit"/>s, SI prefixes, and the normalisation that makes "mm2",
    /// "mm^2" and "mm²", "Nm" and "N·m", "uF" and "µF" the same. Lookup order: exact spelling, prefix + exact spelling,
    /// then case-insensitive spelling (and prefix + case-insensitive spelling), so "10 KW" still reads as kilowatt.
    /// </summary>
    public static class UnitCatalog
    {
        private static readonly Dictionary<string, List<Unit>> Exact = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<Unit>> Folded = new(StringComparer.Ordinal);

        /// <summary>SI prefixes accepted on input (µ also as u).</summary>
        public static readonly IReadOnlyDictionary<string, double> PrefixFactors = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["p"] = 1e-12, ["n"] = 1e-9, ["µ"] = 1e-6, ["m"] = 1e-3, ["c"] = 1e-2, ["d"] = 1e-1,
            ["h"] = 1e2, ["k"] = 1e3, ["M"] = 1e6, ["G"] = 1e9, ["T"] = 1e12,
        };

        static UnitCatalog()
        {
            foreach (var kind in Quantities.All)
                foreach (var u in kind.Units)
                    foreach (var s in u.Spellings)
                    {
                        var n = Norm(s);
                        AddTo(Exact, n, u);
                        AddTo(Exact, n.Replace("·", ""), u);
                        AddTo(Folded, n.ToLowerInvariant(), u);
                        AddTo(Folded, n.Replace("·", "").ToLowerInvariant(), u);
                    }
        }

        private static void AddTo(Dictionary<string, List<Unit>> d, string key, Unit u)
        {
            if (!d.TryGetValue(key, out var list)) d[key] = list = new List<Unit>();
            if (!list.Contains(u)) list.Add(u);
        }

        public static double PrefixFactor(string prefix) => PrefixFactors.TryGetValue(prefix, out var f) ? f : throw new ArgumentException("Unknown SI prefix '" + prefix + "'.");

        /// <summary>
        /// Canonical spelling: no blanks or '^', superscript digits as digits, '*', '⋅', '×' and '.' between letters as '·',
        /// Greek mu as micro sign, Ohm sign as Omega, º as °.
        /// </summary>
        public static string Norm(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                switch (c)
                {
                    case ' ': case ' ': case ' ': case ' ': case '^': continue;
                    case '²': sb.Append('2'); break;
                    case '³': sb.Append('3'); break;
                    case '¹': sb.Append('1'); break;
                    case '⁻': case '−': sb.Append('-'); break;
                    case '*': case '⋅': case '×': case '·': sb.Append('·'); break;
                    case '.' when i > 0 && i < s.Length - 1 && char.IsLetter(s[i - 1]) && char.IsLetter(s[i + 1]): sb.Append('·'); break;
                    case 'μ': sb.Append('µ'); break;
                    case 'Ω': sb.Append('Ω'); break;
                    case 'º': sb.Append('°'); break;
                    case '℃': sb.Append("°C"); break;
                    case '℉': sb.Append("°F"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// The unit spelled <paramref name="spelling"/>, restricted to <paramref name="kind"/> when given (null = any kind,
        /// the first kind in <see cref="Quantities.All"/> order wins). Null if unknown.
        /// </summary>
        public static Unit? Find(string spelling, QuantityKind? kind = null)
            => FindAll(spelling).FirstOrDefault(u => kind is null || u.Kind == kind);

        /// <summary>Every unit (of any kind) spelled like this, in kind preference order; empty if unknown.</summary>
        public static IReadOnlyList<Unit> FindAll(string spelling) => FindAll(spelling, true);

        /// <summary>Every unit spelled like this; <paramref name="foldCase"/> false skips the case-insensitive fallback.</summary>
        public static IReadOnlyList<Unit> FindAll(string spelling, bool foldCase)
        {
            var cacheKey = (foldCase ? "1" : "0") + (spelling ?? "");
            if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
            var result = FindAllCore(spelling ?? "", foldCase);
            if (Cache.Count < 4096) Cache[cacheKey] = result;
            return result;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyList<Unit>> Cache = new(StringComparer.Ordinal);

        private static IReadOnlyList<Unit> FindAllCore(string spelling, bool foldCase)
        {
            var n = Norm(spelling ?? "");
            if (n.Length == 0) return Array.Empty<Unit>();
            var found = Lookup(Exact, n, false);
            if (found.Count == 0) found = WithPrefix(n, Exact, false);
            if (found.Count == 0 && foldCase) found = Lookup(Folded, n, true);
            if (found.Count == 0 && foldCase) found = WithPrefix(n, Folded, true);
            return found.OrderBy(u => IndexOf(u.Kind)).ToArray();
        }

        private static int IndexOf(QuantityKind k)
        {
            for (var i = 0; i < Quantities.All.Count; i++) if (Quantities.All[i] == k) return i;
            return int.MaxValue;
        }

        private static List<Unit> Lookup(Dictionary<string, List<Unit>> d, string n, bool fold)
        {
            var key = fold ? n.ToLowerInvariant() : n;
            if (d.TryGetValue(key, out var l)) return l.ToList();
            if (d.TryGetValue(key.Replace("·", ""), out l)) return l.ToList();
            return new List<Unit>();
        }

        private static List<Unit> WithPrefix(string n, Dictionary<string, List<Unit>> d, bool fold)
        {
            var result = new List<Unit>();
            if (n.Length < 2) return result;
            var p = n.Substring(0, 1);
            if (p == "u") p = "µ";
            if (fold && p == "K") p = "k";
            if (!PrefixFactors.ContainsKey(p)) return result;
            foreach (var u in Lookup(d, n.Substring(1), fold))
                if (u.Prefixes.Contains(p)) result.Add(u.WithPrefix(p));
            return result;
        }
    }

    /// <summary>Unit conversion (Calc 0, built-in converter).</summary>
    public static class Units
    {
        /// <summary>The display units of a kind, default first.</summary>
        public static IReadOnlyList<string> DisplayUnits(QuantityKind kind) => kind.Units.Select(u => u.Symbol).ToList();

        /// <summary>
        /// Converts <paramref name="value"/> from one unit to another of the same quantity kind ("1500 rpm" → rad/s,
        /// "20 °C" → °F). Kinds are strict: m → kg and N·m → J are rejected. Throws <see cref="FormatException"/>
        /// (English message; <see cref="TryConvert"/> gives the bilingual one).
        /// </summary>
        public static double Convert(double value, string fromUnit, string toUnit)
            => TryConvert(value, fromUnit, toUnit, out var r, out var error) ? r : throw new FormatException(error!.En);

        public static bool TryConvert(double value, string fromUnit, string toUnit, out double result, out I18nText? error)
        {
            result = double.NaN; error = null;
            var from = UnitCatalog.FindAll(fromUnit);
            var to = UnitCatalog.FindAll(toUnit);
            if (from.Count == 0) { error = UnknownUnit(fromUnit); return false; }
            if (to.Count == 0) { error = UnknownUnit(toUnit); return false; }
            foreach (var f in from)
                foreach (var t in to)
                    if (f.Kind == t.Kind)
                    {
                        result = t.FromSi(f.ToSi(value));
                        return true;
                    }
            error = new I18nText(
                $"Cannot convert {from[0].Symbol} ({from[0].Kind.Name.En.ToLowerInvariant()}) to {to[0].Symbol} ({to[0].Kind.Name.En.ToLowerInvariant()}).",
                $"{from[0].Symbol} ({from[0].Kind.Name.De}) lässt sich nicht in {to[0].Symbol} ({to[0].Kind.Name.De}) umrechnen.");
            return false;
        }

        internal static I18nText UnknownUnit(string spelling)
            => new($"Unknown unit “{spelling}”.", $"Unbekannte Einheit „{spelling}“.");
    }
}
