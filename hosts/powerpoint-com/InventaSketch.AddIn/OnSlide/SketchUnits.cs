using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Engine;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>An InventaSketch unit on a slide: the host shape plus its model, reconciled with native edits.</summary>
    internal sealed class SketchUnit
    {
        public SketchUnit(PowerPoint.Shape shape, string id, SketchDocument doc, System.Drawing.RectangleF boundsPt)
        {
            Shape = shape; Id = id; Doc = doc; BoundsPt = boundsPt;
        }

        public PowerPoint.Shape Shape { get; }
        public string Id { get; }
        /// <summary>Model in slide millimetres, already adjusted to native moves and resizes of the shape.</summary>
        public SketchDocument Doc { get; }
        /// <summary>Current shape bounds in points.</summary>
        public System.Drawing.RectangleF BoundsPt { get; }
        public IEnumerable<SketchObject> Objects => Doc.ObjectsOn(Doc.Pages[0]);
    }

    /// <summary>Reads, reconciles and re-renders InventaSketch units (tags ISK_ID, ISK_MODEL, ISK_BOUNDS).</summary>
    internal static class SketchUnits
    {
        public const string BoundsTag = "ISK_BOUNDS";

        public static bool IsUnit(PowerPoint.Shape s)
        {
            try { return !string.IsNullOrEmpty(s.Tags["ISK_ID"]) && !string.IsNullOrEmpty(s.Tags["ISK_MODEL"]); }
            catch { return false; }
        }

        public static string FormatBounds(PowerPoint.Shape s) =>
            string.Join(";", new[] { s.Left, s.Top, s.Width, s.Height }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));

        /// <summary>
        /// Reads a unit. Native moves and (non-uniform) resizes since the last render are applied to the model as an affine
        /// map of the recorded bounds onto the current bounds, so parameters stay untouched and only sizes/positions change.
        /// </summary>
        public static SketchUnit? Read(PowerPoint.Shape s)
        {
            if (!IsUnit(s)) return null;
            try
            {
                var doc = DocumentJson.FromCompact(s.Tags["ISK_MODEL"]);
                var cur = new System.Drawing.RectangleF(s.Left, s.Top, s.Width, s.Height);
                var parts = (s.Tags[BoundsTag] ?? "").Split(';');
                if (parts.Length == 4 && parts.All(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                {
                    var o = parts.Select(p => float.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                    double sx = o[2] > 0.01 ? cur.Width / o[2] : 1, sy = o[3] > 0.01 ? cur.Height / o[3] : 1;
                    var ids = doc.Objects.Keys.ToList();
                    var pivot = new Vec2(o[0] / Geo.PtPerMm, o[1] / Geo.PtPerMm);
                    if (Math.Abs(sx - 1) > 1e-4 || Math.Abs(sy - 1) > 1e-4) doc = Edits.Scale(doc, ids, pivot, sx, sy);
                    var delta = new Vec2((cur.X - o[0]) / Geo.PtPerMm, (cur.Y - o[1]) / Geo.PtPerMm);
                    if (delta.Length > 1e-6) doc = Edits.Move(doc, ids, delta);
                }
                return new SketchUnit(s, s.Tags["ISK_ID"], doc, cur);
            }
            catch (Exception ex)
            {
                Log.Error("Read unit " + s.Name, ex);
                return null;
            }
        }

        public static List<SketchUnit> All(PowerPoint.Slide slide, string? exceptId = null)
        {
            var list = new List<SketchUnit>();
            foreach (PowerPoint.Shape s in slide.Shapes)
            {
                if (!IsUnit(s) || s.Tags["ISK_ID"] == exceptId) continue;
                var u = Read(s);
                if (u is not null) list.Add(u);
            }
            return list;
        }

        /// <summary>Draws a model as one new unit on the slide (native shapes, tags, bounds) and returns it.</summary>
        public static PowerPoint.Shape? Draw(PowerPoint.Slide slide, Registry registry, SketchDocument doc, Lang lang)
        {
            var session = new EditorSession("com", registry, doc, lang);
            var ids = doc.ObjectsOn(doc.Pages[0]).Select(o => o.Id).ToList();
            if (ids.Count == 0) return null;
            // One unit: all objects of the model belong together (make them an assembly if there are several).
            if (ids.Count > 1 && doc.Objects.Values.Select(o => o.GroupId).Distinct().Count() != 1)
                session.Apply("group", d => Edits.Group(d, ids));
            var plan = DrawPlanBuilder.Build(session, ids, DrawCapabilities.Full);
            var renderer = new ComRenderer(slide);
            PowerPoint.Shape? result = null;
            foreach (var unit in plan.Units)
            {
                result = renderer.DrawUnit(unit);
                if (result is not null) result.Tags.Add(BoundsTag, FormatBounds(result));
            }
            return result;
        }

        /// <summary>Replaces a unit by a re-rendered model at the same z-position (one undo step for the caller).</summary>
        public static PowerPoint.Shape? Replace(PowerPoint.Slide slide, SketchUnit unit, SketchDocument doc, Registry registry, Lang lang)
        {
            var z = unit.Shape.ZOrderPosition;
            unit.Shape.Delete();
            var shape = Draw(slide, registry, doc, lang);
            if (shape is not null)
                while (shape.ZOrderPosition > z) shape.ZOrder(Office.MsoZOrderCmd.msoSendBackward);
            return shape;
        }
    }
}
