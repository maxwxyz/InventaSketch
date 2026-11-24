using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Selection-wide operations for several selected elements (units): align their centres on one horizontal or vertical
    /// line, space their centres evenly, switch them all to a view they share, renumber reference numerals in reading
    /// order. Each operation reports whether it would change anything, so hosts offer only the useful ones.
    /// </summary>
    public static class Arrange
    {
        /// <summary>Below this many millimetres a move counts as no change.</summary>
        public const double Tolerance = 0.05;

        /// <summary>Moves that put every centre on the middle line of the selection (row: horizontal line, else vertical).</summary>
        public static IReadOnlyList<Vec2> AlignCentres(IReadOnlyList<Rect> boxes, bool row)
        {
            if (boxes.Count < 2) return boxes.Select(_ => Vec2.Zero).ToList();
            var all = Union(boxes);
            var mid = row ? all.Y + all.H / 2 : all.X + all.W / 2;
            return boxes.Select(b => row ? new Vec2(0, mid - (b.Y + b.H / 2)) : new Vec2(mid - (b.X + b.W / 2), 0)).ToList();
        }

        /// <summary>Moves that space the centres evenly between the outermost two (horizontally or vertically); needs three.</summary>
        public static IReadOnlyList<Vec2> Distribute(IReadOnlyList<Rect> boxes, bool horizontal)
        {
            var moves = boxes.Select(_ => Vec2.Zero).ToList();
            if (boxes.Count < 3) return moves;
            double C(Rect b) => horizontal ? b.X + b.W / 2 : b.Y + b.H / 2;
            var order = Enumerable.Range(0, boxes.Count).OrderBy(i => C(boxes[i])).ToList();
            var first = C(boxes[order[0]]);
            var step = (C(boxes[order[order.Count - 1]]) - first) / (order.Count - 1);
            for (var k = 1; k < order.Count - 1; k++)
            {
                var d = first + k * step - C(boxes[order[k]]);
                moves[order[k]] = horizontal ? new Vec2(d, 0) : new Vec2(0, d);
            }
            return moves;
        }

        /// <summary>True if any move is longer than <see cref="Tolerance"/>.</summary>
        public static bool Changes(IEnumerable<Vec2> moves) => moves.Any(m => m.Length > Tolerance);

        /// <summary>
        /// Views every given object offers (in the order of the first object's type), without the views all of them already
        /// show; empty if the objects share no view or are all in it.
        /// </summary>
        public static IReadOnlyList<ViewDef> CommonViews(Registry registry, IReadOnlyList<SketchObject> objects)
        {
            var types = objects.Select(o => (Object: o, Type: registry.Type(o.Type))).ToList();
            if (types.Count < 2 || types.Any(t => t.Type is null || t.Type.Views.Count < 2)) return Array.Empty<ViewDef>();
            var first = types[0].Type!;
            return first.Views
                .Where(v => types.All(t => t.Type!.Views.Any(w => w.Id == v.Id)))
                .Where(v => types.Any(t => t.Type!.ViewOf(t.Object) != v.Id))
                .ToList();
        }

        /// <summary>
        /// New numerals for reference numerals at <c>At</c> in reading order (rows top to bottom – positions within
        /// <paramref name="rowTolerance"/> mm form a row – then left to right). Numbering starts at the smallest numeric
        /// numeral (10 if none) and steps by 2 when all numeric numerals are even, else by 1. Returns id → numeral only for
        /// numerals that change.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Renumber(IReadOnlyList<(string Id, Vec2 At, string Numeral)> numerals, double rowTolerance = 5)
        {
            var numbers = numerals.Select(n => int.TryParse(n.Numeral, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : (int?)null).Where(v => v is not null).Select(v => v!.Value).ToList();
            var start = numbers.Count > 0 ? numbers.Min() : 10;
            var step = numbers.Count > 0 && numbers.All(v => v % 2 == 0) ? 2 : 1;
            var rows = new List<List<(string Id, Vec2 At, string Numeral)>>();
            foreach (var n in numerals.OrderBy(n => n.At.Y))
            {
                var row = rows.LastOrDefault();
                if (row is not null && Math.Abs(n.At.Y - row[0].At.Y) <= rowTolerance) row.Add(n);
                else rows.Add(new List<(string, Vec2, string)> { n });
            }
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var next = start;
            foreach (var n in rows.SelectMany(r => r.OrderBy(x => x.At.X)))
            {
                var text = next.ToString(CultureInfo.InvariantCulture);
                if (text != n.Numeral) result[n.Id] = text;
                next += step;
            }
            return result;
        }

        private static Rect Union(IEnumerable<Rect> boxes)
        {
            var list = boxes.ToList();
            double x0 = list.Min(b => b.X), y0 = list.Min(b => b.Y), x1 = list.Max(b => b.X + b.W), y1 = list.Max(b => b.Y + b.H);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
