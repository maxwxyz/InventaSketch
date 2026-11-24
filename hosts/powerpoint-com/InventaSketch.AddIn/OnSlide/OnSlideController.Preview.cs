using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Live coupling preview while dragging or resizing: what the coupling will do to the neighbours on release (a meshing
    /// gear that scales or moves along, a belt that stretches, a shaft that follows its motor) is drawn in a second ghost
    /// colour, so the result is visible before the mouse is let go.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private static readonly Color Partner = Color.FromArgb(0, 120, 215);

        /// <summary>The unit and its neighbours at the drag start (the coupling's "before").</summary>
        private SketchDocument? _previewBase;
        private readonly Stopwatch _previewClock = new();
        private List<Primitive> _previewPrims = new();

        private void ResetCouplingPreview()
        {
            _previewBase = null;
            _previewPrims = new List<Primitive>();
            _previewClock.Reset();
        }

        /// <summary>
        /// Neighbours changed by the coupling when the unit becomes <paramref name="unitAfter"/>, rendered; the unit itself
        /// and its rigid followers (already in the ghost) are left out. Throttled to about 15 updates a second.
        /// </summary>
        private IReadOnlyList<Primitive> CouplingPreview(SketchDocument unitAfter)
        {
            if (_unit is null || _otherUnits.Count == 0 || !AddInSettings.Anchor) return Array.Empty<Primitive>();
            if (_previewClock.IsRunning && _previewClock.ElapsedMilliseconds < 66) return _previewPrims;
            _previewClock.Restart();
            try
            {
                var registry = _registry();
                _previewBase ??= SketchUnits.WithContext(_unit.Doc, _otherUnits);
                var after = SketchUnits.WithContext(unitAfter, _otherUnits);
                var result = Coupling.Propagate(registry, _previewBase, after, _lang());
                if (ReferenceEquals(result, after)) return _previewPrims = new List<Primitive>();
                var own = new HashSet<string>(_unit.Objects.Select(o => o.Id), StringComparer.Ordinal);
                var changed = result.Objects.Values.Where(o => !own.Contains(o.Id) && !_followerIds.Contains(o.Id)
                    && after.Get(o.Id) is { } x && (x.Transform != o.Transform || x.Size != o.Size) && registry.Type(o.Type) is not null).ToList();
                var ctx = new RenderContext(result, _lang());
                _previewPrims = changed.SelectMany(o => registry.Render(o, ctx)).ToList();
            }
            catch (Exception ex)
            {
                Log.Error("Coupling preview", ex);
                _previewPrims = new List<Primitive>();
            }
            return _previewPrims;
        }
    }
}
