using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Inserting parts where they fit (<see cref="Fitting"/>), from the insert box, the pie and insert at the pointer:
    /// the selected unit is tried object by object (shafts first, then the parts on them, then the shaft a selected part
    /// sits on), and while an item follows the pointer its ghost already shows the fitted result (a ball bearing on the
    /// nearest shaft, in the shaft's view and size) instead of the item's default look; a click inserts exactly that.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Reach of the fitted preview around the pointer (logical pixels).</summary>
        private const double FitReachPx = 40;

        /// <summary>The fitted object the placement ghost shows (inserted on click), or null for a free placement.</summary>
        private SketchObject? _placeFitted;
        /// <summary>The other units of the slide as one document (placement context), built when the placement starts.</summary>
        private SketchDocument? _placeContext;

        /// <summary>
        /// Objects to fit an insert to, in order: the unit's objects with an axis (shafts), its other parts, then the shafts in
        /// other units that a part of the unit sits on (a selected gear: its shaft).
        /// </summary>
        private List<string> FitAnchors(SketchUnit unit, SketchDocument full)
        {
            var registry = _registry();
            var ctx = new RenderContext(full, _lang());
            var axisOf = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool HasAxis(SketchObject o) => axisOf.TryGetValue(o.Id, out var v) ? v
                : axisOf[o.Id] = registry.PageAnchors(o, ctx).Any(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);
            var own = unit.Objects.Where(o => !o.IsPointGeometry && registry.Type(o.Type) is not null).ToList();
            var ids = own.Where(HasAxis).Concat(own.Where(o => !HasAxis(o))).Select(o => o.Id).ToList();
            var hubs = own.SelectMany(o => registry.PageAnchors(o, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub)).Select(a => a.P).ToList();
            if (hubs.Count == 0) return ids;
            foreach (var o in full.Objects.Values)
            {
                if (ids.Contains(o.Id) || o.IsPointGeometry || registry.Type(o.Type) is null) continue;
                var carries = registry.PageAnchors(o, ctx).Any(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis
                    && hubs.Any(h => Geo.ClosestOnSegment(h, a.P, a.Q).Point.DistanceTo(h) <= 0.3));
                if (carries) ids.Add(o.Id);
            }
            return ids;
        }

        /// <summary>
        /// Where the user asked for the insert (page mm): the point of the right-click, Ctrl+Space or "+" that opened the
        /// pie or the insert box. The fitted part goes to that side (maintainer 2026-11-25: "attached elements appear on the
        /// side where you clicked").
        /// </summary>
        private Vec2? _insertToward;

        /// <summary>The item placed where it fits the unit (first anchor of <see cref="FitAnchors"/> that takes it), or null.</summary>
        private SketchObject? FitToUnit(SketchUnit unit, SketchDocument full, LibraryItem item)
        {
            var registry = _registry();
            var toward = _insertToward;
            foreach (var id in FitAnchors(unit, full))
            {
                try
                {
                    if (Fitting.Place(registry, full, id, item, _lang(), toward) is { } placed) return placed;
                }
                catch (Exception ex)
                {
                    Log.Error("Fitting " + item.Id + " to " + id, ex);
                }
            }
            return null;
        }

        /// <summary>
        /// The item fitted near the pointer (page mm): on the closest shaft or part within <paramref name="maxDistance"/> that
        /// takes it, in that part's view and size; null if nothing near takes it (the item is then placed freely).
        /// </summary>
        private SketchObject? FitNear(LibraryItem item, Vec2 pointer, double maxDistance)
        {
            var doc = _placeContext;
            if (doc is null || doc.Objects.Count == 0) return null;
            var registry = _registry();
            var lang = _lang();
            try
            {
                // On a shaft: fitted to its diameter, in its view, at the pointer's projection (engine).
                if (Fitting.PlaceNear(registry, doc, item, pointer, maxDistance, lang) is { } onShaft) return onShaft;
                var ctx = new RenderContext(doc, lang);
                var near = new List<(string Id, double D)>();
                foreach (var o in doc.Objects.Values)
                {
                    if (o.IsPointGeometry || registry.Type(o.Type) is null) continue;
                    var d = double.MaxValue;
                    foreach (var a in registry.PageAnchors(o, ctx))
                    {
                        if (a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis) d = Math.Min(d, Geo.ClosestOnSegment(pointer, a.P, a.Q).Point.DistanceTo(pointer));
                        else if (a.Kind == AnchorKind.Point && a.Role is AnchorRole.Hub) d = Math.Min(d, a.P.DistanceTo(pointer));
                    }
                    if (d <= maxDistance) near.Add((o.Id, d));
                }
                // Other fits (a gear meshing with a gear near the pointer): on the pointer's side.
                foreach (var (id, _) in near.OrderBy(n => n.D).Take(4))
                    if (Fitting.Place(registry, doc, id, item, lang, pointer) is { } placed) return placed;
            }
            catch (Exception ex)
            {
                Log.Error("Fit near " + item.Id, ex);
            }
            return null;
        }

        /// <summary>The other units of the slide as one document for fitted placement previews.</summary>
        private void PreparePlacementContext()
        {
            _placeFitted = null;
            try
            {
                var w = (_view?.SlideWidth ?? 960) / Geo.PtPerMm;
                var h = (_view?.SlideHeight ?? 540) / Geo.PtPerMm;
                _placeContext = SketchUnits.WithContext(SketchDocument.Create(new Size2(w, h)), _otherUnits);
            }
            catch (Exception ex)
            {
                Log.Error("Placement context", ex);
                _placeContext = null;
            }
        }

        /// <summary>Inserts the fitted object of the placement preview (coupled, selected); one undo step.</summary>
        private void InsertFitted(SketchObject placed, LibraryItem item)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var lang = _lang();
            _app.StartNewUndoEntry();
            var doc = SingleUnitDoc(placed);
            SketchUnits.Draw(slide, registry, doc, lang);
            SketchUnits.RedrawDependents(slide, registry, lang, Array.Empty<SketchDocument>(), new[] { doc });
            SketchUnits.All(slide).FirstOrDefault(u => u.Objects.Any(o => o.Id == placed.Id))?.Shape.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Inserted {item.Id} where it fits (pointer)");
        }
    }
}
