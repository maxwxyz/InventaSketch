using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Junction ⇄ bridge (maintainer 2026-11-26, <see cref="Crossings"/>): a selected wire or pipe shows a bridge icon at each
    /// junction it takes part in ("Make bridge here (not connected)", next to the connect icons at its crossings); two
    /// selected lines that cross get "Connect (junction)" in the multi-selection bar, lines meeting at a junction "Make bridge
    /// (not connected)". Every unit is its own shape, so an edit that splits, joins or removes lines is written back across
    /// the units of the slide (<see cref="CommitAcrossUnits"/>).
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Junctions of the armed wire or pipe that can become a bridge (bridge icons).</summary>
        private List<Junction> _junctions = new();

        private const int BridgeCode = 300000;

        /// <summary>The first line of each unit that hops at crossings (wires, pipes), in selection order.</summary>
        private List<string> HopLines(IEnumerable<SketchUnit> units)
        {
            var registry = _registry();
            return units.Select(u => u.Objects.FirstOrDefault(o => o.IsPointGeometry && registry.Type(o.Type)?.HopsAtCrossings == true)?.Id)
                .Where(id => id is not null).Select(id => id!).ToList();
        }

        /// <summary>Multi-selection bar buttons "Connect (junction)" and "Make bridge (not connected)" where they change something.</summary>
        private IEnumerable<BarButton> JunctionButtons()
        {
            var lines = HopLines(_multi);
            var slide = CurrentSlide();
            if (lines.Count < 2 || slide is null) yield break;
            var registry = _registry();
            var full = SketchUnits.WithContext(_multi[0].Doc, SketchUnits.All(slide, _multi[0].Id));
            if (_multi.Count == 2 && lines.Count == 2 && Crossings.Between(registry, full, lines[0], lines[1], _lang()).Count > 0)
                yield return new BarButton("junction", "", T("Connect (junction)", "Verbinden (Abzweig)")) { IconId = IconIds.Junction };
            if (Crossings.SharedJunctions(registry, full, lines, _lang()).Count > 0)
                yield return new BarButton("bridge", "", T("Make bridge (not connected)", "Als Brücke (nicht verbunden)")) { IconId = IconIds.Bridge };
        }

        /// <summary>Runs "junction" or "bridge" on the selected units; returns the shapes to keep selected.</summary>
        private List<PowerPoint.Shape> ApplyJunctionAction(PowerPoint.Slide slide, IReadOnlyList<SketchUnit> units, string id)
        {
            var registry = _registry();
            var lang = _lang();
            var lines = HopLines(units);
            var full = SketchUnits.WithContext(units[0].Doc, SketchUnits.All(slide, units[0].Id));
            var after = id == "junction"
                ? lines.Count == 2 ? Crossings.ConnectBetween(registry, full, lines[0], lines[1], lang) : full
                : Crossings.DisconnectAll(registry, full, Crossings.SharedJunctions(registry, full, lines, lang), lines.FirstOrDefault(), lang);
            if (ReferenceEquals(after, full)) return units.Select(u => u.Shape).ToList();
            return CommitAcrossUnits(slide, SketchUnits.All(slide), full, after, new HashSet<string>(lines, StringComparer.Ordinal));
        }

        /// <summary>Makes the junction of the bridge icon a bridge again (one undo step) and keeps the armed line selected.</summary>
        private void ApplyBridge(SketchUnit stale, Junction junction)
        {
            var slide = CurrentSlide();
            var unit = SketchUnits.Read(stale.Shape);
            if (slide is null || unit is null) return;
            var registry = _registry();
            var line = unit.Objects.FirstOrDefault(x => x.IsPointGeometry);
            if (line is null) return;
            var full = SketchUnits.WithContext(unit.Doc, SketchUnits.All(slide, unit.Id));
            var doc = Crossings.Disconnect(registry, full, junction, line.Id, _lang());
            if (ReferenceEquals(doc, full)) return;
            _app.StartNewUndoEntry();
            var shapes = CommitAcrossUnits(slide, SketchUnits.All(slide), full, doc, new HashSet<string>(StringComparer.Ordinal) { line.Id });
            shapes.FirstOrDefault()?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Made a bridge of the junction of {line.Type} at {junction.Host}");
        }

        /// <summary>
        /// Writes an edit of the slide's combined model (<paramref name="before"/> → <paramref name="after"/>) back to the units:
        /// changed elements are replaced in their unit, removed ones taken out (a unit left empty is deleted), new elements
        /// (the parts of a split line) each become a unit of their own; lines hopping over the changed ones are redrawn.
        /// Returns the shapes of the units that hold one of <paramref name="selected"/> and of the new units.
        /// </summary>
        private List<PowerPoint.Shape> CommitAcrossUnits(PowerPoint.Slide slide, IReadOnlyList<SketchUnit> units, SketchDocument before, SketchDocument after, ICollection<string> selected)
        {
            var registry = _registry();
            var lang = _lang();
            after = Attachments.Refresh(registry, after, new RenderContext(after, lang));
            var shapes = new List<PowerPoint.Shape>();
            var befores = new List<SketchDocument?>();
            var afters = new List<SketchDocument?>();
            var touched = new List<string>();
            foreach (var u in units)
            {
                var ids = u.Doc.Objects.Keys.ToList();
                var removed = ids.Where(k => after.Get(k) is null).ToList();
                var changed = ids.Where(k => after.Get(k) is { } a && before.Get(k) is { } b && !ReferenceEquals(a, b) && !Equals(a, b)).ToList();
                var holds = ids.Any(selected.Contains);
                if (removed.Count == 0 && changed.Count == 0)
                {
                    if (holds) shapes.Add(u.Shape);
                    continue;
                }
                if (ids.All(k => removed.Contains(k) || registry.Type(u.Doc.Get(k)!.Type) is null))
                {
                    u.Shape.Delete();
                    Log.Info("Removed unit " + u.Id);
                    continue;
                }
                var doc = Edits.Remove(u.Doc, removed);
                doc = Edits.Update(doc, changed, o => after.Get(o.Id)!);
                befores.Add(u.Doc);
                afters.Add(doc);
                touched.Add(u.Id);
                if (SketchUnits.Replace(slide, u, doc, registry, lang) is { } s && holds) shapes.Add(s);
            }
            foreach (var added in after.Objects.Values.Where(o => before.Get(o.Id) is null).ToList())
            {
                var unitDoc = SingleUnitDoc(added);
                afters.Add(unitDoc);
                if (SketchUnits.Draw(slide, registry, unitDoc, lang) is { } s) shapes.Add(s);
            }
            if (afters.Count > 0) SketchUnits.RedrawDependents(slide, registry, lang, befores, afters, touched);
            return shapes;
        }
    }
}
