using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Pure document edits. Every edit is a function SketchDocument -> SketchDocument, which makes
    /// undo/redo trivial and keeps hosts free of model logic.
    /// </summary>
    public static class Edits
    {
        private static Dictionary<string, SketchObject> Copy(SketchDocument doc) => new(doc.Objects.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal);

        public static SketchDocument Add(SketchDocument doc, string pageId, params SketchObject[] objects)
        {
            var objs = Copy(doc);
            foreach (var o in objects)
            {
                if (string.IsNullOrEmpty(o.Id)) throw new ArgumentException("Object id must be set.");
                objs[o.Id] = o;
            }
            var pages = doc.Pages.Select(p => p.Id == pageId ? p with { Order = p.Order.Concat(objects.Select(o => o.Id).Where(id => !p.Order.Contains(id))).ToList() } : p).ToList();
            if (pages.All(p => p.Id != pageId)) throw new KeyNotFoundException($"Page '{pageId}' not found.");
            return doc with { Objects = objs, Pages = pages };
        }

        public static SketchDocument Update(SketchDocument doc, IEnumerable<string> ids, Func<SketchObject, SketchObject> f)
        {
            var objs = Copy(doc);
            foreach (var id in ids)
                if (objs.TryGetValue(id, out var o)) objs[id] = f(o) with { Id = o.Id };
            return doc with { Objects = objs };
        }

        public static SketchDocument Replace(SketchDocument doc, SketchObject o) => Update(doc, new[] { o.Id }, _ => o);

        /// <summary>Removes objects and detaches attachments that pointed to them (points keep their last position).</summary>
        public static SketchDocument Remove(SketchDocument doc, IEnumerable<string> ids)
        {
            var remove = new HashSet<string>(ids);
            var objs = Copy(doc);
            foreach (var id in remove) objs.Remove(id);
            foreach (var key in objs.Keys.ToList())
            {
                var o = objs[key];
                if (o.Attach is null || !o.Attach.Any(a => a is not null && remove.Contains(a.ObjectId))) continue;
                var resolved = doc.ResolvePoints(o);
                objs[key] = o with { Points = resolved.ToList(), Attach = o.Attach.Select(a => a is not null && remove.Contains(a.ObjectId) ? null : a).ToList() };
            }
            var pages = doc.Pages.Select(p => p with { Order = p.Order.Where(id => !remove.Contains(id)).ToList() }).ToList();
            return doc with { Objects = objs, Pages = pages };
        }

        /// <summary>Moves objects by delta. Point objects move their unattached points.</summary>
        public static SketchDocument Move(SketchDocument doc, IEnumerable<string> ids, Vec2 delta)
        {
            var set = new HashSet<string>(ids);
            return Update(doc, set, o =>
            {
                if (o.Locked) return o;
                if (!o.IsPointGeometry) return o with { Transform = o.Transform with { X = o.Transform.X + delta.X, Y = o.Transform.Y + delta.Y } };
                // Points attached to a co-moving object keep their attachment (they follow it);
                // points attached to a static object are detached and moved with the selection.
                var resolved = doc.ResolvePoints(o);
                var pts = new List<Vec2>(resolved.Count);
                var attach = new List<Attachment?>(resolved.Count);
                for (var i = 0; i < resolved.Count; i++)
                {
                    var a = o.Attach is not null && i < o.Attach.Count ? o.Attach[i] : null;
                    var follows = a is not null && set.Contains(a.ObjectId);
                    pts.Add(follows ? o.Points![i] : resolved[i] + delta);
                    attach.Add(follows ? a : null);
                }
                return o with { Points = pts, Attach = attach.Any(x => x is not null) ? attach : null };
            });
        }

        /// <summary>Rotates objects around a pivot by an angle in degrees (clockwise on screen).</summary>
        public static SketchDocument Rotate(SketchDocument doc, IEnumerable<string> ids, Vec2 pivot, double degrees)
        {
            var rad = Geo.DegToRad(degrees);
            return Update(doc, ids, o =>
            {
                if (o.Locked) return o;
                if (o.IsPointGeometry) return o with { Points = doc.ResolvePoints(o).Select(p => p.RotateAround(pivot, rad)).ToList(), Attach = null };
                var c = new Vec2(o.Transform.X, o.Transform.Y).RotateAround(pivot, rad);
                return o with { Transform = o.Transform with { X = c.X, Y = c.Y, Rotation = Geo.NormDeg(o.Transform.Rotation + degrees) } };
            });
        }

        /// <summary>Mirrors objects horizontally (axis = vertical line through pivot.X) or vertically.</summary>
        public static SketchDocument Flip(SketchDocument doc, IEnumerable<string> ids, Vec2 pivot, bool horizontal)
        {
            return Update(doc, ids, o =>
            {
                if (o.Locked) return o;
                Vec2 M(Vec2 p) => horizontal ? new Vec2(2 * pivot.X - p.X, p.Y) : new Vec2(p.X, 2 * pivot.Y - p.Y);
                if (o.IsPointGeometry) return o with { Points = doc.ResolvePoints(o).Select(M).ToList(), Attach = null };
                var c = M(new Vec2(o.Transform.X, o.Transform.Y));
                var t = o.Transform;
                // Mirroring = flip in local x plus negated rotation (keeps the visual result exact).
                return horizontal
                    ? o with { Transform = t with { X = c.X, Y = c.Y, Rotation = Geo.NormDeg(-t.Rotation), FlipX = !t.FlipX } }
                    : o with { Transform = t with { X = c.X, Y = c.Y, Rotation = Geo.NormDeg(-t.Rotation), FlipY = !t.FlipY } };
            });
        }

        /// <summary>
        /// Free (non-uniform) scaling of box objects in the selection frame. <paramref name="sx"/>/<paramref name="sy"/>
        /// are applied in page axes relative to <paramref name="pivot"/>; parameters are never changed.
        /// </summary>
        public static SketchDocument Scale(SketchDocument doc, IEnumerable<string> ids, Vec2 pivot, double sx, double sy)
        {
            return Update(doc, ids, o =>
            {
                if (o.Locked) return o;
                Vec2 S(Vec2 p) => new(pivot.X + (p.X - pivot.X) * sx, pivot.Y + (p.Y - pivot.Y) * sy);
                if (o.IsPointGeometry) return o with { Points = o.Points!.Select(S).ToList() };
                var c = S(new Vec2(o.Transform.X, o.Transform.Y));
                // Project the page-axis scale onto the object's rotated local axes.
                var rad = Geo.DegToRad(o.Transform.Rotation);
                var ux = new Vec2(Math.Cos(rad), Math.Sin(rad));
                var uy = ux.Perp;
                var fx = new Vec2(ux.X * sx, ux.Y * sy).Length;
                var fy = new Vec2(uy.X * sx, uy.Y * sy).Length;
                return o with
                {
                    Transform = o.Transform with { X = c.X, Y = c.Y },
                    Size = new Size2(Math.Max(0.5, Math.Abs(o.Size.W * fx)), Math.Max(0.5, Math.Abs(o.Size.H * fy))),
                };
            });
        }

        /// <summary>Inserts a free point at <paramref name="index"/> (0 … count) of a point object; later points shift by one.</summary>
        public static SketchDocument InsertPoint(SketchDocument doc, string id, int index, Vec2 p)
            => Update(doc, new[] { id }, o =>
            {
                if (o.Points is null || index < 0 || index > o.Points.Count) return o;
                var pts = doc.ResolvePoints(o).ToList();
                pts.Insert(index, p);
                if (o.Attach is null) return o with { Points = pts };
                var att = o.Attach.ToList();
                while (att.Count < o.Points.Count) att.Add(null);
                att.Insert(index, null);
                return o with { Points = pts, Attach = att };
            });

        public static SketchDocument SetPoint(SketchDocument doc, string id, int index, Vec2 p, Attachment? attach = null)
            => Update(doc, new[] { id }, o =>
            {
                if (o.Points is null || index < 0 || index >= o.Points.Count) return o;
                var pts = o.Points.ToList(); pts[index] = p;
                var att = (o.Attach ?? new Attachment?[o.Points.Count]).ToList();
                while (att.Count < pts.Count) att.Add(null);
                att[index] = attach;
                return o with { Points = pts, Attach = att.Any(a => a is not null) ? att : null };
            });

        /// <summary>
        /// Attaches point <paramref name="index"/> of a point object to a box object at a page position (e.g. a snapped
        /// port). The attachment is stored in normalised box coordinates so it follows moving, rotating and scaling of
        /// the target; <paramref name="anchorId"/> optionally names the target anchor (pin/port id) it belongs to.
        /// </summary>
        public static SketchDocument AttachPoint(SketchDocument doc, string objectId, int index, string targetId, Vec2 pagePoint, string? anchorId = null)
        {
            var target = doc.Get(targetId) ?? throw new KeyNotFoundException(targetId);
            if (target.IsPointGeometry) throw new ArgumentException("Attachments target box objects only.", nameof(targetId));
            var (u, v) = target.ToBox(pagePoint);
            return SetPoint(doc, objectId, index, pagePoint, new Attachment(targetId, u, v) { AnchorId = anchorId });
        }

        /// <summary>Returns a copy with points (and attachments) in reverse order (swap direction of a point object).</summary>
        public static SketchObject ReversePoints(SketchObject o)
        {
            if (o.Points is null) return o;
            var pts = o.Points.Reverse().ToList();
            IReadOnlyList<Attachment?>? att = null;
            if (o.Attach is not null)
            {
                var a = o.Attach.ToList();
                while (a.Count < o.Points.Count) a.Add(null);
                a.Reverse();
                att = a;
            }
            return o with { Points = pts, Attach = att };
        }

        /// <summary>Duplicates objects with new ids (groups get a new group id, attachments are remapped).</summary>
        public static (SketchDocument Doc, IReadOnlyList<string> NewIds) Duplicate(SketchDocument doc, string pageId, IEnumerable<string> ids, Vec2 offset)
        {
            var list = ids.Select(doc.Get).Where(o => o is not null).Cast<SketchObject>().ToList();
            var map = list.ToDictionary(o => o.Id, _ => Ids.New());
            var groupMap = new Dictionary<string, string>();
            var copies = list.Select(o =>
            {
                string? gid = null;
                if (o.GroupId is not null && !groupMap.TryGetValue(o.GroupId, out gid)) { gid = Ids.New("g"); groupMap[o.GroupId] = gid; }
                var c = o with
                {
                    Id = map[o.Id], GroupId = gid, Ref = null,
                    Released = o.Released?.Where(map.ContainsKey).Select(r => map[r]).ToList() is { Count: > 0 } rel ? rel : null,
                };
                if (c.IsPointGeometry)
                    c = c with
                    {
                        Points = doc.ResolvePoints(o).Select(p => p + offset).ToList(),
                        Attach = o.Attach?.Select(a => a is not null && map.TryGetValue(a.ObjectId, out var nid) ? a with { ObjectId = nid } : null).ToList(),
                    };
                else c = c with { Transform = c.Transform with { X = c.Transform.X + offset.X, Y = c.Transform.Y + offset.Y } };
                return c;
            }).ToArray();
            return (Add(doc, pageId, copies), copies.Select(c => c.Id).ToList());
        }

        public static SketchDocument Group(SketchDocument doc, IEnumerable<string> ids)
        {
            var gid = Ids.New("g");
            return Update(doc, ids, o => o with { GroupId = gid });
        }

        public static SketchDocument Ungroup(SketchDocument doc, IEnumerable<string> ids) => Update(doc, ids, o => o with { GroupId = null });

        /// <summary>Changes z-order: +1 forward, -1 backward, +inf front, -inf back.</summary>
        public static SketchDocument Reorder(SketchDocument doc, string pageId, IEnumerable<string> ids, int step)
        {
            var set = new HashSet<string>(ids);
            return doc with
            {
                Pages = doc.Pages.Select(p =>
                {
                    if (p.Id != pageId) return p;
                    var order = p.Order.ToList();
                    if (step >= order.Count) order = order.Where(i => !set.Contains(i)).Concat(order.Where(set.Contains)).ToList();
                    else if (step <= -order.Count) order = order.Where(set.Contains).Concat(order.Where(i => !set.Contains(i))).ToList();
                    else
                    {
                        var idx = Enumerable.Range(0, order.Count).Where(i => set.Contains(order[i]));
                        foreach (var i in step > 0 ? idx.Reverse() : idx)
                        {
                            var j = Math.Max(0, Math.Min(order.Count - 1, i + step));
                            if (set.Contains(order[j])) continue;
                            (order[i], order[j]) = (order[j], order[i]);
                        }
                    }
                    return p with { Order = order };
                }).ToList(),
            };
        }

        public static SketchDocument SetReference(SketchDocument doc, string numeral, string label)
        {
            var refs = doc.References.ToDictionary(kv => kv.Key, kv => kv.Value);
            refs[numeral] = new ReferenceEntry(numeral, label);
            return doc with { References = refs };
        }
    }

    /// <summary>Linear undo/redo history of immutable documents.</summary>
    public sealed class History
    {
        private readonly Stack<(SketchDocument Doc, string Label)> _undo = new();
        private readonly Stack<(SketchDocument Doc, string Label)> _redo = new();
        private readonly int _limit;

        public History(SketchDocument initial, int limit = 200) { Current = initial; _limit = limit; }

        public SketchDocument Current { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string? UndoLabel => _undo.Count > 0 ? _undo.Peek().Label : null;

        public SketchDocument Apply(string label, Func<SketchDocument, SketchDocument> edit)
        {
            var next = edit(Current);
            if (ReferenceEquals(next, Current)) return Current;
            _undo.Push((Current, label));
            if (_undo.Count > _limit)
            {
                var keep = _undo.Take(_limit).Reverse().ToList();
                _undo.Clear();
                foreach (var k in keep) _undo.Push(k);
            }
            _redo.Clear();
            Current = next;
            return Current;
        }

        /// <summary>Replaces the current state without a new undo step (live drag preview).</summary>
        public void Preview(SketchDocument doc) => Current = doc;

        public SketchDocument Undo()
        {
            if (_undo.Count == 0) return Current;
            var (doc, label) = _undo.Pop();
            _redo.Push((Current, label));
            Current = doc;
            return Current;
        }

        public SketchDocument Redo()
        {
            if (_redo.Count == 0) return Current;
            var (doc, label) = _redo.Pop();
            _undo.Push((Current, label));
            Current = doc;
            return Current;
        }
    }
}
