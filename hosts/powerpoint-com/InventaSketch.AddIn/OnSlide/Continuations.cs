using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Names that belong together across slides (ROADMAP plan review 2026-11-25 night, item 12): every unit keeps the
    /// designations of its lines and elements in a shape tag (<see cref="RefsTag"/>, comparison form, written when it is
    /// drawn), so the add-in finds on which other slides a designation occurs without reading every model. The map
    /// designation → other slide numbers feeds the continuation markers ("→ W3, slide 4", <see cref="Designations"/>) and
    /// "Select connected ▸ Same name" ("also on slides 2, 4").
    /// </summary>
    internal static class Continuations
    {
        /// <summary>Shape tag with the unit's designations in comparison form, "|"-separated; "-" when it has none.</summary>
        public const string RefsTag = "ISK_REFS";

        /// <summary>The designations (comparison form) of a unit model.</summary>
        public static IReadOnlyList<string> Keys(Registry registry, SketchDocument doc, Lang lang)
        {
            try { return Designations.Index(registry, doc, lang).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(); }
            catch (Exception ex) { Log.Error("Designations", ex); return Array.Empty<string>(); }
        }

        /// <summary>Stores the designations of a unit in its tag.</summary>
        public static void Tag(PowerPoint.Shape shape, IReadOnlyList<string> keys)
        {
            try
            {
                try { shape.Tags.Delete(RefsTag); } catch { /* not set */ }
                shape.Tags.Add(RefsTag, keys.Count == 0 ? "-" : string.Join("|", keys));
            }
            catch (Exception ex)
            {
                Log.Error("Designation tag", ex);
            }
        }

        /// <summary>The designations of a unit shape: its tag, else (units drawn before the tag existed) read from its model.</summary>
        private static IEnumerable<string> KeysOf(PowerPoint.Shape s, Registry registry, Lang lang)
        {
            string raw;
            try { raw = s.Tags[RefsTag] ?? ""; } catch { raw = ""; }
            if (raw == "-") return Array.Empty<string>();
            if (raw.Length > 0) return raw.Split('|').Where(k => k.Length > 0);
            if (!SketchUnits.IsUnit(s)) return Array.Empty<string>();
            try { return Keys(registry, DocumentJson.FromCompact(SketchUnits.ModelTag(s)), lang); }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>
        /// For each of <paramref name="keys"/> the numbers of the other slides of the presentation where it occurs (slide
        /// numbers as labels "2", "4"); designations found nowhere else are left out.
        /// </summary>
        public static Dictionary<string, IReadOnlyList<string>> Elsewhere(PowerPoint.Slide slide, IReadOnlyCollection<string> keys, Registry registry, Lang lang)
        {
            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            if (keys.Count == 0) return result;
            try
            {
                if (slide.Parent is not PowerPoint.Presentation pres) return result;
                var own = slide.SlideIndex;
                var found = keys.ToDictionary(k => k, _ => new SortedSet<int>(), StringComparer.Ordinal);
                foreach (PowerPoint.Slide other in pres.Slides)
                {
                    var index = other.SlideIndex;
                    if (index == own) continue;
                    foreach (PowerPoint.Shape s in other.Shapes)
                    {
                        if (!SketchUnits.IsUnit(s)) continue;
                        foreach (var k in KeysOf(s, registry, lang))
                            if (found.TryGetValue(k, out var set)) set.Add(index);
                    }
                }
                foreach (var kv in found)
                    if (kv.Value.Count > 0) result[kv.Key] = Designations.PageLabels(kv.Value);
            }
            catch (Exception ex)
            {
                Log.Error("Continuations", ex);
            }
            return result;
        }

        /// <summary>The continuation map for a unit model on <paramref name="slide"/>, or null when it has no designation found elsewhere.</summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>>? For(PowerPoint.Slide slide, SketchDocument doc, Registry registry, Lang lang)
        {
            var keys = Keys(registry, doc, lang);
            if (keys.Count == 0) return null;
            var map = Elsewhere(slide, keys, registry, lang);
            return map.Count == 0 ? null : map;
        }

        /// <summary>"also on slides 2, 4" / "auch auf Folien 2, 4" (one designation's other slides), or "".</summary>
        public static string AlsoOn(IReadOnlyList<string>? slides, Lang lang)
        {
            if (slides is null || slides.Count == 0) return "";
            var names = PageNames.Slides;
            return (lang == Lang.De ? "auch auf " : "also on ") + (slides.Count == 1 ? names.One : names.Many).Get(lang) + " " + string.Join(", ", slides);
        }
    }
}
