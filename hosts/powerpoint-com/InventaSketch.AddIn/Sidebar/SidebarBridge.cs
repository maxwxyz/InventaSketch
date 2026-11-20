using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.Sidebar
{
    /// <summary>
    /// Message protocol between the sidebar editor (TypeScript, combridge.ts) and the COM add-in:
    /// editor → add-in: {type:"ready"} and {type:"units", id, units:[{id, payload}], remove:[unitId]};
    /// add-in → editor: {type:"ack", id, ok, error?} and {type:"selection", unitId, payload}.
    /// Payloads are compact InventaSketch documents; the add-in draws them natively with the full C# engine.
    /// </summary>
    internal sealed class SidebarBridge
    {
        private readonly PowerPoint.Application _app;
        private readonly Func<Registry> _registry;
        private readonly Func<Lang> _lang;
        private TaskPaneControl? _pane;
        private bool _applying;

        public SidebarBridge(PowerPoint.Application app, Func<Registry> registry, Func<Lang> lang)
        {
            _app = app; _registry = registry; _lang = lang;
        }

        public void Attach(TaskPaneControl pane)
        {
            if (ReferenceEquals(_pane, pane)) return;
            _pane = pane;
            pane.MessageReceived += OnMessage;
        }

        /// <summary>Called by the on-slide controller when an InventaSketch unit is selected.</summary>
        public void UnitSelected(SketchUnit unit)
        {
            if (_pane is null || _applying) return;
            var json = JsonSerializer.Serialize(new { type = "selection", unitId = unit.Id, payload = DocumentJson.ToCompact(unit.Doc) });
            _pane.Post(json);
        }

        private void OnMessage(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            switch (type)
            {
                case "ready":
                    Log.Info("Sidebar ready");
                    PushCurrentSelection();
                    break;
                case "units":
                    {
                        var id = root.GetProperty("id").GetInt32();
                        try
                        {
                            ApplyUnits(root);
                            _pane?.Post(JsonSerializer.Serialize(new { type = "ack", id, ok = true }));
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Sidebar units", ex);
                            _pane?.Post(JsonSerializer.Serialize(new { type = "ack", id, ok = false, error = ex.Message }));
                        }
                        break;
                    }
            }
        }

        private void PushCurrentSelection()
        {
            try
            {
                var sel = _app.ActiveWindow.Selection;
                if (sel.Type != PowerPoint.PpSelectionType.ppSelectionShapes || sel.ShapeRange.Count != 1) return;
                var unit = SketchUnits.Read(sel.ShapeRange[1]);
                if (unit is not null) UnitSelected(unit);
            }
            catch (Exception ex)
            {
                Log.Error("Sidebar selection", ex);
            }
        }

        /// <summary>Draws new units, replaces existing ones (same ISK_ID) and removes deleted ones — one undo step.</summary>
        private void ApplyUnits(JsonElement root)
        {
            if (_app.Windows.Count == 0 || _app.ActiveWindow.View.Slide is not PowerPoint.Slide slide) throw new InvalidOperationException("No slide is shown.");
            var registry = _registry();
            var existing = new Dictionary<string, PowerPoint.Shape>(StringComparer.Ordinal);
            foreach (PowerPoint.Shape s in slide.Shapes)
                if (SketchUnits.IsUnit(s)) existing[s.Tags["ISK_ID"]] = s;
            _applying = true;
            try
            {
                _app.StartNewUndoEntry();
                if (root.TryGetProperty("remove", out var remove))
                    foreach (var r in remove.EnumerateArray())
                        if (r.GetString() is string rid && existing.TryGetValue(rid, out var gone)) { gone.Delete(); existing.Remove(rid); }
                PowerPoint.Shape? last = null;
                foreach (var u in root.GetProperty("units").EnumerateArray())
                {
                    var unitId = u.GetProperty("id").GetString() ?? "";
                    var model = DocumentJson.FromCompact(u.GetProperty("payload").GetString() ?? "");
                    if (existing.TryGetValue(unitId, out var shape) && SketchUnits.Read(shape) is SketchUnit unit)
                        last = SketchUnits.Replace(slide, unit, model, registry, _lang());
                    else
                        last = SketchUnits.Draw(slide, registry, model, _lang());
                }
                last?.Select(Office.MsoTriState.msoTrue);
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
