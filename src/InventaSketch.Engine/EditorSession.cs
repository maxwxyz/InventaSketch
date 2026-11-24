using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// State of one editor (task pane, COM overlay, LibreOffice window): document history, current page,
    /// selection and snap options. Sessions are independent; a host may keep several (one per slide).
    /// </summary>
    public sealed class EditorSession
    {
        private List<string> _selection = new();

        public EditorSession(string id, Registry registry, SketchDocument doc, Lang lang)
        {
            Id = id;
            Registry = registry;
            History = new History(doc);
            PageId = doc.Pages.Count > 0 ? doc.Pages[0].Id : throw new ArgumentException("Document has no page.");
            Lang = lang;
        }

        public string Id { get; }
        public Registry Registry { get; }

        /// <summary>
        /// Motion mode: the last solved position of a running linkage drag (<c>motionPreview</c>), the document it started
        /// from and the dragged joint; the next step solves from there, so the chain keeps its assembly mode.
        /// </summary>
        internal (SketchDocument Base, SketchDocument State, string Key)? Motion { get; set; }
        public History History { get; private set; }
        public string PageId { get; set; }
        public Lang Lang { get; set; }
        public SnapOptions Snap { get; set; } = new();
        /// <summary>Incremented on every document change (edits, undo, redo, open).</summary>
        public int Revision { get; private set; }
        /// <summary>Objects added or modified by the last document change (edit, undo, redo); empty after open/new.</summary>
        public IReadOnlyList<string> LastChanged { get; private set; } = Array.Empty<string>();
        /// <summary>Existing objects whose rendering depends on the objects touched by the last change (<see cref="ObjectType.Dependents"/>).</summary>
        public IReadOnlyList<string> LastDependents { get; private set; } = Array.Empty<string>();

        public SketchDocument Doc => History.Current;

        public Page Page => Doc.Pages.FirstOrDefault(p => p.Id == PageId) ?? Doc.Pages[0];

        public IReadOnlyList<string> Selection => _selection;

        public RenderContext Context(bool monochrome = false) => new(Doc, Lang, monochrome);

        /// <summary>Mechanical coupling for the next edits (<see cref="Coupling"/>); hosts switch it off for free moves (Alt).</summary>
        public bool Couple { get; set; } = true;

        /// <summary>Sets the selection (unknown ids dropped, groups expanded).</summary>
        public void Select(IEnumerable<string>? ids)
        {
            var doc = Doc;
            var list = (ids ?? Array.Empty<string>()).Where(id => doc.Objects.ContainsKey(id)).Distinct().ToList();
            _selection = doc.ExpandGroups(list).Where(id => doc.Objects.ContainsKey(id)).Distinct().ToList();
        }

        /// <summary>
        /// Applies an undoable edit. Returns true if the document changed. Attachments bound to a pin/port id are
        /// re-resolved afterwards (<see cref="Attachments.Refresh"/>), in the same undo step.
        /// </summary>
        public bool Apply(string label, Func<SketchDocument, SketchDocument> edit)
        {
            var before = History.Current;
            var after = History.Apply(label, d =>
            {
                var next = edit(d);
                if (ReferenceEquals(next, d)) return d;
                // Gears stay in mesh, shafts carry their gears (Coupling), in the same undo step.
                if (Couple) next = Coupling.Propagate(Registry, d, next, Lang);
                return Attachments.Refresh(Registry, next, new RenderContext(next, Lang));
            });
            if (ReferenceEquals(before, after)) return false;
            Revision++;
            Track(before, after);
            Select(_selection);
            return true;
        }

        public bool Undo()
        {
            if (!History.CanUndo) return false;
            var before = History.Current;
            History.Undo();
            Revision++;
            Track(before, History.Current);
            Select(_selection);
            return true;
        }

        public bool Redo()
        {
            if (!History.CanRedo) return false;
            var before = History.Current;
            History.Redo();
            Revision++;
            Track(before, History.Current);
            Select(_selection);
            return true;
        }

        /// <summary>Replaces the document and clears the history (open/new).</summary>
        public void Reset(SketchDocument doc)
        {
            if (doc.Pages.Count == 0) doc = doc with { Pages = new[] { new Page { Id = Ids.New("p") } } };
            History = new History(doc);
            PageId = doc.Pages[0].Id;
            Revision++;
            _selection = new List<string>();
            LastChanged = Array.Empty<string>();
            LastDependents = Array.Empty<string>();
        }

        private void Track(SketchDocument before, SketchDocument after)
        {
            var set = ChangeSet.Compute(Registry, before, after, Lang);
            LastChanged = set.Changed;
            LastDependents = set.Dependents;
        }

        /// <summary>Ids on the current page, filtered to existing objects (null/empty = current selection).</summary>
        public IReadOnlyList<string> Resolve(IEnumerable<string>? ids, bool expandGroups = true)
        {
            var doc = Doc;
            var list = (ids ?? _selection).Where(id => doc.Objects.ContainsKey(id)).Distinct().ToList();
            return expandGroups ? doc.ExpandGroups(list).Distinct().ToList() : list;
        }

        /// <summary>Union of the page bounds of the given objects (mm).</summary>
        public Rect? Bounds(IEnumerable<string> ids)
        {
            Rect? r = null;
            foreach (var id in ids)
            {
                var o = Doc.Get(id);
                if (o is null) continue;
                var b = ObjectBounds(o);
                r = r is null ? b : r.Value.Union(b);
            }
            return r;
        }

        /// <summary>Bounds of an object: box corners for box objects, resolved points/primitives for point objects.</summary>
        public Rect ObjectBounds(SketchObject o)
        {
            if (!o.IsPointGeometry) return o.Bounds();
            var pts = Doc.ResolvePoints(o);
            var r = Rect.FromPoints(pts);
            if (Registry.Type(o.Type) is not null)
            {
                var prim = Prim.Bounds(Registry.Render(o, Context()));
                if (prim is not null) r = pts.Count > 0 ? r.Union(prim.Value) : prim.Value;
            }
            return r;
        }
    }

    /// <summary>Thread-safe store of editor sessions by id.</summary>
    public sealed class SessionStore
    {
        private readonly Dictionary<string, EditorSession> _sessions = new(StringComparer.Ordinal);
        private readonly object _lock = new();

        public EditorSession Add(EditorSession s)
        {
            lock (_lock) _sessions[s.Id] = s;
            return s;
        }

        public EditorSession Get(string? id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Missing 'session'.");
            lock (_lock)
                return _sessions.TryGetValue(id!, out var s) ? s : throw new KeyNotFoundException($"Unknown session '{id}'.");
        }

        public bool Remove(string id)
        {
            lock (_lock) return _sessions.Remove(id);
        }

        public int Count { get { lock (_lock) return _sessions.Count; } }
    }
}
