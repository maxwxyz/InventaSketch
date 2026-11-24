using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>One row of a parts list: item number, reference numeral, name, type name, quantity, parameters.</summary>
    public sealed record PartsListRow(int Item, string Numeral, string Name, string TypeName, int Quantity, string Parameters);

    /// <summary>
    /// Parts list (bill of materials) of a document or selection: one row per kind of part – same type, view-independent
    /// parameters and reference numeral – with its quantity, ordered by numeral. Annotation and patent marks never count;
    /// map symbols, diagram shapes and charts are no parts (<see cref="ObjectType.IsPart"/>). Used by the CSV export and by
    /// the live parts-list table element.
    /// </summary>
    public static class PartsList
    {
        /// <summary>True if <paramref name="o"/> counts as a part.</summary>
        public static bool IsPart(Registry registry, SketchObject o)
            => !o.IsPointGeometry && registry.Type(o.Type) is { } t && t.IsPart && !t.DependsOnAllParts
               && !o.Type.StartsWith("ann.", StringComparison.Ordinal) && !o.Type.StartsWith("patent.", StringComparison.Ordinal);

        public static IReadOnlyList<PartsListRow> Rows(Registry registry, IEnumerable<SketchObject> objects, Lang lang)
        {
            var groups = objects.Where(o => IsPart(registry, o)).Select(o => new
            {
                Numeral = o.Ref ?? "",
                Name = Exports.Label(registry, o, lang),
                o.Type,
                TypeName = registry.Type(o.Type)?.Name.Get(lang) ?? o.Type,
                Params = string.Join("; ", o.Params.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + (p.Value.ToString() ?? ""))),
            })
            .GroupBy(r => (r.Numeral, r.Name, r.Type, r.Params))
            .OrderBy(g => int.TryParse(g.Key.Numeral, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : int.MaxValue)
            .ThenBy(g => g.Key.Numeral, StringComparer.Ordinal).ThenBy(g => g.Key.Name, StringComparer.CurrentCulture)
            .ToList();
            var rows = new List<PartsListRow>();
            var item = 1;
            foreach (var g in groups)
            {
                var first = g.First();
                rows.Add(new PartsListRow(item++, first.Numeral, first.Name, first.TypeName, g.Count(), first.Params));
            }
            return rows;
        }
    }
}
