using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Insert by typing: ranked library hits for a query typed at the pointer. Every term must occur in the item's names
    /// (both languages), keywords or category; names in the UI language that start with the query rank first, then names
    /// with a word starting with it, then other name matches, then keyword/category matches. Recently used items and
    /// items in the view of the selection rank higher. Umlauts and ß match their spelled-out forms ("stoessel" finds
    /// "Stößel"). Items that fit the selection (preferred) always come
    /// first. An empty query lists the preferred items, then the recent ones.
    /// </summary>
    public static class LibrarySearch
    {
        public static IReadOnlyList<LibraryItem> Find(Registry registry, string? query, Lang lang, IReadOnlyList<string>? recent = null, string? view = null, int max = 12, IReadOnlyList<LibraryItem>? preferred = null)
        {
            recent ??= Array.Empty<string>();
            preferred ??= Array.Empty<LibraryItem>();
            var q = Fold(query ?? "").Trim();
            if (q.Length == 0)
                return preferred.Concat(recent.Select(registry.Item).Where(i => i is not null).Select(i => i!))
                    .GroupBy(i => i.Id).Select(g => g.First()).Take(max).ToList();
            var liked = new HashSet<string>(preferred.Select(i => i.Id));
            var terms = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var hits = new List<(LibraryItem Item, double Score, int Order)>();
            var order = 0;
            foreach (var item in registry.Library)
            {
                order++;
                var type = registry.Type(item.TypeId);
                var name = Fold(item.Name.Get(lang));
                var names = Fold(string.Join(" ", item.Name.En, item.Name.De ?? "", type?.Name.En ?? "", type?.Name.De ?? ""));
                var rest = Fold(string.Join(" ", item.Keywords.Concat(type?.Keywords ?? Array.Empty<string>()).Concat(new[] { item.Category })));
                if (!terms.All(t => names.Contains(t) || rest.Contains(t))) continue;
                double score;
                if (name.StartsWith(q, StringComparison.Ordinal)) score = 100;
                else if (WordStarts(name, terms[0])) score = 70;
                else if (terms.All(names.Contains)) score = 45;
                else score = 15;
                var r = IndexOf(recent, item.Id);
                if (r >= 0) score += 30 - Math.Min(r, 10);
                if (view is not null && item.View == view) score += 5;
                // Parts that fit the selection always come first (the insert box's promise), ranked among themselves as above.
                if (liked.Contains(item.Id)) score += 1000;
                hits.Add((item, score - name.Length * 0.01, order));
            }
            return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Order).Take(max).Select(h => h.Item).ToList();
        }

        /// <summary>The recent list with <paramref name="id"/> moved to the front (at most <paramref name="max"/> entries).</summary>
        public static IReadOnlyList<string> Remember(IEnumerable<string>? recent, string id, int max = 12)
            => new[] { id }.Concat((recent ?? Array.Empty<string>()).Where(x => x != id)).Take(max).ToList();

        /// <summary>Lower case with umlauts and ß spelled out, so both spellings match.</summary>
        public static string Fold(string s)
            => s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

        private static bool WordStarts(string text, string term)
        {
            for (var i = text.IndexOf(term, StringComparison.Ordinal); i >= 0; i = text.IndexOf(term, i + 1, StringComparison.Ordinal))
                if (i == 0 || !char.IsLetterOrDigit(text[i - 1])) return true;
            return false;
        }

        private static int IndexOf(IReadOnlyList<string> list, string id)
        {
            for (var i = 0; i < list.Count; i++) if (list[i] == id) return i;
            return -1;
        }
    }
}
