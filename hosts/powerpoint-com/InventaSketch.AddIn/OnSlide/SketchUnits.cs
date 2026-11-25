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

        /// <summary>
        /// Joins a tag value that the Office web add-in split into chunks: KEY, KEY_2 … KEY_n plus KEY_N = n (see
        /// <c>chunkTag</c> in hosts/office-web/src/office/powerpoint.ts); a single tag is returned as it is.
        /// </summary>
        internal static string? JoinChunks(Func<string, string?> read, string key)
        {
            var first = read(key);
            if (string.IsNullOrEmpty(first)) return first;
            if (!int.TryParse(read(key + "_N"), System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 2) return first;
            var sb = new System.Text.StringBuilder(first);
            for (var i = 2; i <= n; i++) sb.Append(read(key + "_" + i.ToString(CultureInfo.InvariantCulture)) ?? "");
            return sb.ToString();
        }

        /// <summary>The unit model payload of a shape (joined when the web add-in stored it in chunks).</summary>
        public static string ModelTag(PowerPoint.Shape s) => JoinChunks(k => s.Tags[k], "ISK_MODEL") ?? "";

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
                var doc = DocumentJson.FromCompact(ModelTag(s));
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
                // Formatting done with PowerPoint's own tools (colours, line weight, fonts) becomes part of the model.
                doc = NativeFormat.Fold(s, doc);
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

        /// <summary>
        /// Draws a model as one new unit on the slide (native shapes, tags, bounds) and returns it. Models with
        /// neighbour-dependent objects (<see cref="ObjectType.DependsOnNeighbours"/>: wall joints, openings in walls) are
        /// rendered with the other units of the slide as context; the stored model stays the unit's own.
        /// </summary>
        public static PowerPoint.Shape? Draw(PowerPoint.Slide slide, Registry registry, SketchDocument doc, Lang lang, int? zOrder = null)
        {
            var ids = doc.ObjectsOn(doc.Pages[0]).Select(o => o.Id).ToList();
            if (ids.Count == 0) return null;
            // The slide-wide style (black and white, line group) applies to every unit drawn on the slide.
            doc = SlideStyle.Apply(slide, doc);
            // One unit: all objects of the model belong together (make them an assembly if there are several).
            if (ids.Count > 1 && doc.Objects.Values.Select(o => o.GroupId).Distinct().Count() != 1)
                doc = Edits.Group(doc, ids);
            // The context keeps the slide's stacking order (a new unit is on top): wires drawn later hop over earlier ones.
            var context = NeedsContext(registry, doc) ? WithContext(doc, All(slide), zOrder ?? int.MaxValue) : doc;
            var session = new EditorSession("com", registry, context, lang);
            var plan = DrawPlanBuilder.Build(session, ids, DrawCapabilities.Full);
            var renderer = new ComRenderer(slide);
            PowerPoint.Shape? result = null;
            foreach (var unit in plan.Units)
            {
                result = renderer.DrawUnit(unit);
                if (result is null) continue;
                // The context only shapes the drawing; the stored model holds the unit's own objects.
                if (!ReferenceEquals(context, doc)) result.Tags.Add("ISK_MODEL", EngineApi.CompactOf(doc, doc.Pages[0], unit.ObjectIds));
                result.Tags.Add(BoundsTag, FormatBounds(result));
                NativeFormat.Snapshot(result);
            }
            return result;
        }

        /// <summary>True if any object of the model renders from its neighbours.</summary>
        public static bool NeedsContext(Registry registry, SketchDocument? doc)
            => doc is not null && (doc.Objects.Values.Any(o => registry.Type(o.Type)?.DependsOnNeighbours == true) || HasExternalAttachments(doc));

        /// <summary>True if a point of the model is anchored to an object of another unit ("Anchor").</summary>
        public static bool HasExternalAttachments(SketchDocument doc)
            => doc.Objects.Values.Any(o => o.Attach is not null && o.Attach.Any(a => a is not null && !doc.Objects.ContainsKey(a.ObjectId)));

        /// <summary>
        /// The model with the points of anchored objects moved to where their targets (other units) now are; the
        /// attachments stay. Keeps the stored points current, so the unit is right even when edited on its own.
        /// </summary>
        public static SketchDocument ResolveAnchors(Registry registry, SketchDocument own, IEnumerable<SketchUnit> others)
        {
            if (!HasExternalAttachments(own)) return own;
            var full = WithContext(own, others);
            full = Attachments.Refresh(registry, full);
            var ids = own.Objects.Values.Where(o => o.IsPointGeometry && o.Attach is not null).Select(o => o.Id).ToList();
            return Edits.Update(own, ids, o => full.Get(o.Id) is { } f ? o with { Points = full.ResolvePoints(f), Attach = f.Attach } : o);
        }

        /// <summary>The unit model plus the objects of the other units (on the model's page, same settings) for rendering.</summary>
        /// <summary>
        /// The unit model plus the objects of the other units in the slide's stacking order, the unit's own objects at
        /// <paramref name="ownZ"/> (its z-order position; on top when larger than all). Rendering that depends on the
        /// order (wire hops: the upper line hops) then matches the slide.
        /// </summary>
        public static SketchDocument WithContext(SketchDocument own, IEnumerable<SketchUnit> others, int ownZ)
        {
            var doc = WithContext(own, others);
            if (ReferenceEquals(doc, own)) return doc;
            var page = doc.Pages[0];
            var z = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var u in others)
            {
                int pos;
                try { pos = u.Shape.ZOrderPosition; } catch { continue; }
                foreach (var o in u.Objects) z[o.Id] = pos;
            }
            foreach (var o in own.Objects.Values) z[o.Id] = ownZ;
            var ordered = page.Order.Select((id, i) => (id, i)).OrderBy(t => z.TryGetValue(t.id, out var v) ? v : int.MaxValue).ThenBy(t => t.i).Select(t => t.id).ToList();
            return doc with { Pages = doc.Pages.Select((p, i) => i == 0 ? p with { Order = ordered } : p).ToList() };
        }

        public static SketchDocument WithContext(SketchDocument own, IEnumerable<SketchUnit> others)
        {
            var extra = others.SelectMany(u => u.Objects).Where(o => !own.Objects.ContainsKey(o.Id)).GroupBy(o => o.Id).Select(g => g.First()).ToArray();
            return extra.Length == 0 ? own : Edits.Add(own, own.Pages[0].Id, extra);
        }

        /// <summary>The models of the given units as one document (objects, groups, attachments across units and reference lists) for exports.</summary>
        public static SketchDocument Combined(IEnumerable<SketchUnit> units)
        {
            var list = units.ToList();
            var doc = SketchDocument.Create();
            if (list.Count > 0) doc = doc with { Settings = list[0].Doc.Settings };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var objs = list.SelectMany(u => u.Objects).Where(o => seen.Add(o.Id)).ToArray();
            if (objs.Length > 0) doc = Edits.Add(doc, doc.Pages[0].Id, objs);
            var refs = doc.References.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var u in list) foreach (var kv in u.Doc.References) refs[kv.Key] = kv.Value;
            return doc with { References = refs };
        }
        /// <summary>Models of several units merged into one document (first model's settings) for dependency queries.</summary>
        private static SketchDocument Merge(IEnumerable<SketchDocument> models, IEnumerable<SketchUnit> others)
        {
            var list = models.ToList();
            var doc = SketchDocument.Create();
            if (list.Count > 0) doc = doc with { Settings = list[0].Settings };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var objs = list.SelectMany(m => m.ObjectsOn(m.Pages[0])).Concat(others.SelectMany(u => u.Objects)).Where(o => seen.Add(o.Id)).ToArray();
            return objs.Length == 0 ? doc : Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        /// <summary>
        /// Redraws the other units whose objects depend on an edited unit (<see cref="ObjectType.Dependents"/>, evaluated with
        /// the slide before and after the edit), e.g. joined walls and doors in a wall whose thickness changed. Runs inside the
        /// caller's undo entry. <paramref name="before"/>/<paramref name="after"/> are the edited models (empty when inserted or
        /// deleted); units with ids in <paramref name="skip"/> are not redrawn. Returns the number of redrawn units.
        /// </summary>
        public static int RedrawDependents(PowerPoint.Slide slide, Registry registry, Lang lang, IEnumerable<SketchDocument?> before, IEnumerable<SketchDocument?> after, ICollection<string>? skip = null)
        {
            var b = before.Where(d => d is not null).Cast<SketchDocument>().ToList();
            var a = after.Where(d => d is not null).Cast<SketchDocument>().ToList();
            var touched = new HashSet<string>(b.Concat(a).SelectMany(d => d.Objects.Keys), StringComparer.Ordinal);
            var coupledIds = CouplingEnabled() ? Couple(slide, registry, lang, b, a, touched) : new HashSet<string>();
            var coupled = coupledIds.Count;
            // Line ends anchored to coupled gears follow too.
            touched.UnionWith(coupledIds);
            var all = All(slide);
            var others = all.Where(u => (skip is null || !skip.Contains(u.Id)) && !u.Objects.Any(o => touched.Contains(o.Id))).ToList();
            // Units with line ends anchored to an edited object follow it ("Anchor").
            var anchored = others.Where(u => u.Objects.Any(o => o.Attach is not null && o.Attach.Any(x => x is not null && touched.Contains(x.ObjectId)))).ToList();
            // Parts lists on the slide follow every part edit.
            var lists = others.Any(u => u.Objects.Any(o => registry.Type(o.Type)?.DependsOnAllParts == true));
            if (anchored.Count == 0 && !lists && !b.Concat(a).Any(d => NeedsContext(registry, d))) return coupled;
            var deps = new HashSet<string>(ChangeSet.DependentsOf(registry, Merge(b, others), Merge(a, others), touched.ToList(), lang), StringComparer.Ordinal);
            var count = 0;
            foreach (var unit in others)
            {
                var isAnchored = anchored.Contains(unit);
                if (!isAnchored && !unit.Objects.Any(o => deps.Contains(o.Id))) continue;
                var doc = isAnchored ? ResolveAnchors(registry, unit.Doc, all.Where(x => x.Id != unit.Id)) : unit.Doc;
                Replace(slide, unit, doc, registry, lang);
                count++;
            }
            if (count > 0) Log.Info("Redrew " + count + " dependent unit(s)");
            return count + coupled;
        }

        /// <summary>
        /// Mechanical coupling across units (<see cref="Coupling"/>): gears in mesh with an edited gear scale or move, gears on
        /// an edited shaft follow it. Off while "Anchor" is off or the edit was made with Alt (free move).
        /// </summary>
        public static Func<bool> CouplingEnabled { get; set; } = () => true;

        /// <summary>
        /// Applies the coupling of an edit to the other units of the slide (and to the edited unit itself when it has to
        /// scale): every unit whose objects change is replaced. Returns the ids of changed objects in other units.
        /// </summary>
        private static HashSet<string> Couple(PowerPoint.Slide slide, Registry registry, Lang lang, List<SketchDocument> before, List<SketchDocument> after, HashSet<string> touched)
        {
            var changed = new HashSet<string>(StringComparer.Ordinal);
            // Inserted units have no earlier model: everything is new, so their teeth still interlock with what they mesh with.
            if (after.Count == 0) return changed;
            var all = All(slide);
            var others = all.Where(u => !u.Objects.Any(o => touched.Contains(o.Id))).ToList();
            var fullBefore = Merge(before, others);
            var fullAfter = Merge(after, others);
            var result = Coupling.Propagate(registry, fullBefore, fullAfter, lang);
            if (ReferenceEquals(result, fullAfter)) return changed;
            var count = 0;
            foreach (var unit in all)
            {
                // Edited units compare with their new model, the others with what is on the slide.
                var basis = unit.Objects.Any(o => touched.Contains(o.Id)) ? fullAfter : unit.Doc;
                var ids = unit.Objects.Where(o => result.Get(o.Id) is { } r && basis.Get(o.Id) is { } x && !ReferenceEquals(r, x)
                    && (r.Transform != x.Transform || r.Size != x.Size || !SameIds(r.Released, x.Released))).Select(o => o.Id).ToList();
                if (ids.Count == 0) continue;
                // Position and size follow; released pairs that no longer touch are forgotten (Coupling.PruneReleased).
                Replace(slide, unit, Edits.Update(unit.Doc, ids, o => o with
                {
                    Transform = result.Get(o.Id)!.Transform, Size = result.Get(o.Id)!.Size, Released = result.Get(o.Id)!.Released,
                }), registry, lang);
                if (!unit.Objects.Any(o => touched.Contains(o.Id))) changed.UnionWith(ids);
                count++;
            }
            if (count > 0) Log.Info("Coupling: redrew " + count + " unit(s)");
            return changed;
        }

        private static bool SameIds(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
            => (a is null || a.Count == 0) ? (b is null || b.Count == 0) : b is not null && a.SequenceEqual(b);

        /// <summary>The same model with new object/group ids (copies must never share ids), optionally moved.</summary>
        public static SketchDocument WithNewIds(SketchDocument doc, Vec2 offset)
        {
            var page = doc.Pages[0];
            var ids = page.Order.ToList();
            var (dup, _) = Edits.Duplicate(doc, page.Id, ids, offset);
            return Edits.Remove(dup, ids);
        }

        /// <summary>True if another shape on the slide carries the same unit id (PowerPoint copy/paste or Ctrl+drag).</summary>
        public static bool HasTwin(PowerPoint.Slide slide, SketchUnit unit)
        {
            foreach (PowerPoint.Shape s in slide.Shapes)
            {
                if (!IsUnit(s) || s.Tags["ISK_ID"] != unit.Id) continue;
                if (s.Name != unit.Shape.Name || s.Id != unit.Shape.Id) return true;
            }
            return false;
        }

        /// <summary>Replaces a unit by a re-rendered model at the same z-position (one undo step for the caller).</summary>
        public static PowerPoint.Shape? Replace(PowerPoint.Slide slide, SketchUnit unit, SketchDocument doc, Registry registry, Lang lang)
        {
            var z = unit.Shape.ZOrderPosition;
            unit.Shape.Delete();
            var shape = Draw(slide, registry, doc, lang, z);
            if (shape is not null)
                while (shape.ZOrderPosition > z) shape.ZOrder(Office.MsoZOrderCmd.msoSendBackward);
            return shape;
        }
    }
}
