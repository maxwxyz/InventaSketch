using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using InventaSketch.Modules.Patent;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// The engine's page commands (<see cref="PageActions"/>: "Number all parts ▸ 10, 20, 30 … / 1, 2, 3 …") on a slide,
    /// without a selected element ("More" menu). The slide's units form one page; the numerals of the other slides count as
    /// used, so new numerals continue after them. Parts that get a numeral are redrawn in their units, every new numeral
    /// becomes a unit of its own (it moves on its own and follows its part, like a numeral placed by hand), one undo step.
    /// </summary>
    internal static class SlideNumbering
    {
        /// <summary>
        /// The slide as one page for the page commands: every unit's objects (slide size) with the units' reference lists,
        /// plus the numerals and reference entries of the other slides of the presentation as reference entries.
        /// </summary>
        public static SketchDocument SlideDoc(PowerPoint.Slide slide, IReadOnlyList<SketchUnit> units, bool otherSlides = true)
        {
            var doc = SketchUnits.Combined(units);
            try
            {
                var setup = ((PowerPoint.Presentation)slide.Parent).PageSetup;
                var size = new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm);
                doc = doc with { Pages = doc.Pages.Select((p, i) => i == 0 ? p with { Size = size } : p).ToList() };
                if (!otherSlides) return doc;
                foreach (PowerPoint.Slide other in ((PowerPoint.Presentation)slide.Parent).Slides)
                {
                    if (other.SlideID == slide.SlideID) continue;
                    var others = SketchUnits.All(other);
                    if (others.Count == 0) continue;
                    doc = WithUsedNumerals(doc, SketchUnits.Combined(others));
                }
            }
            catch (Exception ex)
            {
                // The slide alone still numbers correctly; only the continuation after other slides is lost.
                Log.Error("Number all parts: other slides", ex);
            }
            return doc;
        }

        /// <summary><paramref name="doc"/> with the reference entries and the numerals used in <paramref name="other"/> as reference entries.</summary>
        internal static SketchDocument WithUsedNumerals(SketchDocument doc, SketchDocument other)
        {
            var refs = doc.References.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var kv in other.References)
                if (!refs.ContainsKey(kv.Key)) refs[kv.Key] = kv.Value;
            foreach (var use in ReferenceNumerals.Uses(other))
                if (!refs.ContainsKey(use.Numeral)) refs[use.Numeral] = new ReferenceEntry(use.Numeral, use.Label ?? "");
            return refs.Count == doc.References.Count ? doc : doc with { References = refs };
        }

        /// <summary>The page commands of the slide (empty without parts); whether they apply depends on this slide only.</summary>
        public static IReadOnlyList<PageCommand> Commands(Registry registry, PowerPoint.Slide slide)
        {
            var units = SketchUnits.All(slide);
            if (units.Count == 0) return Array.Empty<PageCommand>();
            var doc = SlideDoc(slide, units, otherSlides: false);
            return PageActions.For(registry, doc, doc.Pages[0].Id);
        }

        /// <summary>
        /// Runs page command <paramref name="actionId"/> on the slide in one undo step. Returns the number of new numerals
        /// (0: nothing to do).
        /// </summary>
        public static int Run(PowerPoint.Application app, Registry registry, Lang lang, PowerPoint.Slide slide, string actionId)
        {
            var units = SketchUnits.All(slide);
            if (units.Count == 0) return 0;
            var doc = SlideDoc(slide, units);
            var ran = PageActions.Run(registry, doc, doc.Pages[0].Id, actionId, lang);
            if (ReferenceEquals(ran, doc)) return 0;
            var (changed, added) = Split(doc, ran, units);
            if (changed.Count == 0 && added.Count == 0) return 0;
            app.StartNewUndoEntry();
            var before = new List<SketchDocument>();
            var after = new List<SketchDocument>();
            // The parts first (their numeral is part of the model, the parts list shows it), then the numerals that point at them.
            foreach (var (unit, ids) in changed)
            {
                var next = Edits.Update(unit.Doc, ids, o => ran.Get(o.Id) ?? o);
                SketchUnits.Replace(slide, unit, next, registry, lang);
                before.Add(unit.Doc);
                after.Add(next);
            }
            foreach (var numeral in added)
                SketchUnits.Draw(slide, registry, NumeralUnit(units, ran, numeral), lang);
            // Parts lists and other dependents on the slide follow the new numerals of the parts.
            SketchUnits.RedrawDependents(slide, registry, lang, before, after);
            Log.Info($"Number all parts ({actionId}): {added.Count} numeral(s), {changed.Count} unit(s) redrawn");
            return added.Count;
        }

        /// <summary>
        /// What a page command changed: per unit the ids of its objects that changed (a twin unit with the same objects counts
        /// once), and the objects it added (in the command's order).
        /// </summary>
        internal static (List<(SketchUnit Unit, List<string> Ids)> Changed, List<SketchObject> Added) Split(SketchDocument before, SketchDocument after, IEnumerable<SketchUnit> units)
        {
            var changed = new List<(SketchUnit, List<string>)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in units)
            {
                var ids = unit.Doc.Objects.Keys.Where(k => seen.Add(k) && after.Get(k) is { } a && before.Get(k) is { } b && !ReferenceEquals(a, b) && !Equals(a, b)).ToList();
                if (ids.Count > 0) changed.Add((unit, ids));
            }
            var page = after.Pages[0];
            var added = after.ObjectsOn(page).Where(o => !before.Objects.ContainsKey(o.Id)).ToList();
            return (changed, added);
        }

        /// <summary>A unit model for one new numeral: the slide's settings and the numeral's reference entry.</summary>
        private static SketchDocument NumeralUnit(IReadOnlyList<SketchUnit> units, SketchDocument ran, SketchObject numeral)
        {
            var doc = SketchDocument.Create(ran.Pages[0].Size) with { Settings = units.Count > 0 ? units[0].Doc.Settings : ran.Settings };
            doc = Edits.Add(doc, doc.Pages[0].Id, numeral);
            var key = ReferenceNumerals.NumeralOf(numeral);
            if (key is not null && ran.References.TryGetValue(key, out var entry)) doc = Edits.SetReference(doc, entry.Numeral, entry.Label);
            return doc;
        }
    }
}
