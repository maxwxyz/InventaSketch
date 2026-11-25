using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Keeps formatting done with PowerPoint's own tools (line/fill/text colour, line weight, font, size, bold, italic,
    /// underline) on InventaSketch units. When a unit is drawn, every native shape remembers how it was rendered (tag
    /// <see cref="Tag"/>); when the unit is read back, the differences are folded into the model as object style
    /// overrides, so they survive the next re-render (view switch, rotation, actions) like native moves and resizes do.
    /// </summary>
    internal static class NativeFormat
    {
        public const string Tag = "ISK_FMT";
        /// <summary>Part key of a leaf shape (the primitive tag, e.g. "series.0"), set by <see cref="ComRenderer"/>.</summary>
        public const string PartTag = "ISK_PART";

        /// <summary>Native formatting of one shape as rendered or as found now.</summary>
        internal sealed record Look(string? Line, double? Weight, string? Fill, string? TextColor, double? TextSize, string? Font, bool? Bold, bool? Italic, bool? Underline)
        {
            public string Encode() => string.Join("|", Line ?? "", N(Weight), Fill ?? "", TextColor ?? "", N(TextSize), Font ?? "", B(Bold), B(Italic), B(Underline));

            public static Look? Decode(string? s)
            {
                if (string.IsNullOrEmpty(s)) return null;
                var p = s!.Split('|');
                if (p.Length != 9) return null;
                return new Look(E(p[0]), D(p[1]), E(p[2]), E(p[3]), D(p[4]), E(p[5]), Bo(p[6]), Bo(p[7]), Bo(p[8]));
            }

            private static string N(double? v) => v is double d ? d.ToString("0.###", CultureInfo.InvariantCulture) : "";
            private static string B(bool? v) => v is bool b ? (b ? "1" : "0") : "";
            private static string? E(string v) => v.Length == 0 ? null : v;
            private static double? D(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
            private static bool? Bo(string v) => v.Length == 0 ? null : v == "1";
        }

        /// <summary>Remembers the rendered formatting on every leaf shape of a freshly drawn unit.</summary>
        public static void Snapshot(PowerPoint.Shape unit)
        {
            foreach (var leaf in Leaves(unit))
            {
                try { leaf.Tags.Add(Tag, Read(leaf).Encode()); }
                catch (Exception ex) { Log.Error("Format snapshot", ex); return; }
            }
        }

        /// <summary>
        /// The model with the formatting changes made in PowerPoint folded in (unchanged if there are none). Changes on some
        /// parts of a single element (one curve, the grid …) go into the look of exactly those parts
        /// (<see cref="SketchObject.Parts"/>); changes on all of it into the element style.
        /// </summary>
        public static SketchDocument Fold(PowerPoint.Shape unit, SketchDocument doc)
        {
            var leaves = new List<(string? Part, Look Before, Look Now)>();
            try
            {
                foreach (var leaf in Leaves(unit))
                {
                    var before = Look.Decode(leaf.Tags[Tag]);
                    if (before is null) continue;
                    var part = leaf.Tags[PartTag];
                    leaves.Add((string.IsNullOrEmpty(part) ? null : part, before, Read(leaf)));
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // Right after an undo PowerPoint restores the group but its items are not reachable yet (0x800A01A8): read
                // the model as stored; native formatting is folded in at the next read (maintainer: values lost after Ctrl+Z).
                Log.Info("Format fold skipped (group items not reachable yet): 0x" + ex.ErrorCode.ToString("X8"));
                return doc;
            }
            return FoldLooks(doc, leaves);
        }

        /// <summary>Folds the before/now looks of the leaves (with their part keys) into the model (see <see cref="Fold"/>).</summary>
        internal static SketchDocument FoldLooks(SketchDocument doc, IReadOnlyList<(string? Part, Look Before, Look Now)> leaves)
        {
            var changed = leaves.Where(l => Decide(new[] { (l.Before, l.Now) }) is not null).ToList();
            if (changed.Count == 0) return doc;
            var parts = changed.Select(l => l.Part).Distinct().ToList();
            var allParts = leaves.Select(l => l.Part).Distinct().Count();
            if (doc.Objects.Count == 1 && parts.All(p => p is not null) && parts.Count < allParts)
            {
                var id = doc.Objects.Keys.First();
                foreach (var part in parts)
                {
                    var delta = Decide(leaves.Where(l => l.Part == part).Select(l => (l.Before, l.Now)).ToList());
                    if (delta is null) continue;
                    doc = Edits.Update(doc, new[] { id }, o => PartLooks.WithLook(o, part!, p => p with { Style = Apply(p.Style, delta) }));
                }
                return doc;
            }
            var all = Decide(leaves.Select(l => (l.Before, l.Now)).ToList());
            return all is null ? doc : Edits.Update(doc, doc.Objects.Keys.ToList(), o => o with { Style = Apply(o.Style, all) });
        }

        /// <summary>
        /// The style change implied by the shapes that changed: per property the most common new value among the shapes
        /// whose value differs from the rendered one (formatting applied to the whole group or to most of it wins).
        /// Text size is returned as a factor. Null if nothing changed.
        /// </summary>
        internal static Style? Decide(IReadOnlyList<(Look Before, Look Now)> pairs)
        {
            T? Majority<T>(Func<Look, T?> get, Func<T?, T?, bool> differs)
            {
                var changed = pairs.Where(p => differs(get(p.Before), get(p.Now))).Select(p => get(p.Now)).ToList();
                return changed.Count == 0 ? default : changed.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
            }
            bool Str(string? a, string? b) => !string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            bool Num(double? a, double? b) => a is double x && b is double y && Math.Abs(x - y) > 0.05;
            bool Flag(bool? a, bool? b) => a is not null && b is not null && a != b;

            var line = pairs.Any(p => Str(p.Before.Line, p.Now.Line)) ? Majority(l => l.Line, Str) : null;
            var weight = pairs.Any(p => Num(p.Before.Weight, p.Now.Weight)) ? pairs.Where(p => Num(p.Before.Weight, p.Now.Weight)).GroupBy(p => p.Now.Weight).OrderByDescending(g => g.Count()).First().Key : null;
            var fill = pairs.Any(p => Str(p.Before.Fill, p.Now.Fill)) ? Majority(l => l.Fill, Str) : null;
            var textColor = pairs.Any(p => Str(p.Before.TextColor, p.Now.TextColor)) ? Majority(l => l.TextColor, Str) : null;
            var font = pairs.Any(p => Str(p.Before.Font, p.Now.Font)) ? Majority(l => l.Font, Str) : null;
            var ratios = pairs.Where(p => Num(p.Before.TextSize, p.Now.TextSize) && p.Before.TextSize > 0).Select(p => p.Now.TextSize!.Value / p.Before.TextSize!.Value).OrderBy(r => r).ToList();
            double? scale = ratios.Count == 0 ? null : ratios[ratios.Count / 2];
            bool? Changed(Func<Look, bool?> get) => pairs.Where(p => Flag(get(p.Before), get(p.Now))).Select(p => get(p.Now)).FirstOrDefault();
            var bold = Changed(l => l.Bold);
            var italic = Changed(l => l.Italic);
            var underline = Changed(l => l.Underline);

            if (line is null && weight is null && fill is null && textColor is null && font is null && scale is null && bold is null && italic is null && underline is null)
                return null;
            return new Style
            {
                Stroke = line,
                StrokeWidth = weight is double w ? Math.Round(w / Geo.PtPerMm, 3) : null,
                Fill = fill is null || fill == "other" ? null : fill == "none" ? Core.Fill.None : Core.Fill.Solid(fill),
                TextColor = textColor,
                Font = font,
                FontScale = scale,
                Bold = bold,
                Italic = italic,
                Underline = underline,
            };
        }

        /// <summary>Merges a decided change into an object style; the text size factor multiplies the existing one.</summary>
        internal static Style Apply(Style? current, Style delta)
        {
            var merged = (current ?? new Style()).Merge(delta with { FontScale = null });
            if (delta.FontScale is double k) merged = merged with { FontScale = Math.Round((current?.FontScale ?? 1) * k, 4) };
            return merged;
        }

        private static IEnumerable<PowerPoint.Shape> Leaves(PowerPoint.Shape s)
        {
            if (s.Type == Office.MsoShapeType.msoGroup)
            {
                foreach (PowerPoint.Shape c in s.GroupItems) yield return c;
            }
            else yield return s;
        }

        private static string Hex(int rgb) => "#" + (rgb & 255).ToString("X2") + ((rgb >> 8) & 255).ToString("X2") + ((rgb >> 16) & 255).ToString("X2");

        private static Look Read(PowerPoint.Shape s)
        {
            string? line = null, fill = null, textColor = null, font = null;
            double? weight = null, size = null;
            bool? bold = null, italic = null, underline = null;
            try
            {
                if (s.Type != Office.MsoShapeType.msoPicture)
                {
                    line = s.Line.Visible == Office.MsoTriState.msoTrue ? Hex(s.Line.ForeColor.RGB) : "none";
                    if (line != "none") weight = s.Line.Weight;
                    if (s.Type is not Office.MsoShapeType.msoLine)
                        fill = s.Fill.Visible != Office.MsoTriState.msoTrue ? "none"
                            : s.Fill.Type == Office.MsoFillType.msoFillSolid ? Hex(s.Fill.ForeColor.RGB) : "other";
                }
                if (s.HasTextFrame == Office.MsoTriState.msoTrue && s.TextFrame.HasText == Office.MsoTriState.msoTrue)
                {
                    var f = s.TextFrame.TextRange.Font;
                    textColor = Hex(f.Color.RGB);
                    size = f.Size;
                    font = f.Name;
                    bold = f.Bold == Office.MsoTriState.msoTrue;
                    italic = f.Italic == Office.MsoTriState.msoTrue;
                    underline = f.Underline == Office.MsoTriState.msoTrue;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Format read", ex);
            }
            return new Look(line, weight, fill, textColor, size, font, bold, italic, underline);
        }
    }
}
