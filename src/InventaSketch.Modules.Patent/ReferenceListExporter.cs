using System.Collections.Generic;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Patent
{
    /// <summary>
    /// Reference sign list as a domain export: the document's reference list (numeral and part name, numerically
    /// sorted); without a stored list, the numerals drawn on the selection with the names found on them.
    /// </summary>
    internal sealed class ReferenceListExporter : IExporter
    {
        public string Id => "patent.reference-list";
        public I18nText Name => new("Reference sign list (text)", "Bezugszeichenliste (Text)");
        public string Extension => "txt";
        public string MimeType => "text/plain";
        public I18nText Preserves => new("Numerals and part names, sorted numerically.", "Bezugszeichen und Teilenamen, numerisch sortiert.");

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
            => doc.References.Count > 0 || objects.Any(o => ReferenceNumerals.IsNumeralType(o.Type) || !string.IsNullOrEmpty(o.Ref));

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            if (doc.References.Count > 0) return ReferenceNumerals.Export(doc);
            var ids = new HashSet<string>(objects.Select(o => o.Id));
            var uses = ReferenceNumerals.Uses(doc).Where(u => ids.Contains(u.ObjectId)).ToList();
            var sb = new StringBuilder();
            foreach (var g in uses.GroupBy(u => u.Numeral).OrderBy(g => int.TryParse(g.Key, out var n) ? n : int.MaxValue).ThenBy(g => g.Key, System.StringComparer.Ordinal))
            {
                var label = g.Select(u => u.Label).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
                sb.Append(g.Key).Append(label.Length > 0 ? " " + label.Trim() : "").Append('\n');
            }
            return sb.ToString();
        }
    }
}
