using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Host-independent engine facade. Every method takes a JSON request (camelCase, <see cref="EngineRequest"/>)
    /// and returns a JSON result serialised with <see cref="DocumentJson.Options"/>. Errors are thrown as
    /// exceptions by the typed methods; <see cref="Call"/> wraps results and errors in an envelope
    /// <c>{"ok":true,"result":…}</c> / <c>{"ok":false,"error":"…"}</c> for hosts that cannot catch .NET exceptions.
    /// </summary>
    public sealed class EngineApi
    {
        private static readonly Lazy<EngineApi> LazyDefault = new(() => new EngineApi(ModuleCatalog.CreateDefault()));
        private readonly Dictionary<string, Func<EngineRequest, object?>> _methods;
        private readonly Dictionary<Lang, Catalog> _catalogs = new();

        public EngineApi(Registry registry)
        {
            Registry = registry;
            _methods = new Dictionary<string, Func<EngineRequest, object?>>(StringComparer.Ordinal)
            {
                ["version"] = _ => VersionInfo(),
                ["createSession"] = CreateSessionCore,
                ["closeSession"] = r => Sessions.Remove(r.Session ?? ""),
                ["newDocument"] = NewDocumentCore,
                ["openDocument"] = OpenDocumentCore,
                ["saveDocument"] = r => new { json = DocumentJson.Serialize(S(r).Doc, r.Indented ?? false) },
                ["setPage"] = SetPageCore,
                ["state"] = r => StateOf(S(r), r),
                ["catalog"] = r => CatalogOf(Language.Detect(r.Lang)),
                ["insert"] = InsertCore,
                ["update"] = UpdateCore,
                ["move"] = MoveCore,
                ["scale"] = ScaleCore,
                ["resize"] = ResizeCore,
                ["rotate"] = RotateCore,
                ["flip"] = FlipCore,
                ["duplicate"] = DuplicateCore,
                ["delete"] = DeleteCore,
                ["group"] = r => Edit(r, "Group", (s, ids) => ids.Count >= 2 ? Edits.Group(s.Doc, ids) : s.Doc),
                ["ungroup"] = r => Edit(r, "Ungroup", (s, ids) => Edits.Ungroup(s.Doc, ids)),
                ["reorder"] = ReorderCore,
                ["setView"] = SetViewCore,
                ["setParam"] = SetParamCore,
                ["setStyle"] = SetStyleCore,
                ["setText"] = SetTextCore,
                ["setPoint"] = SetPointCore,
                ["setPlanScale"] = SetPlanScaleCore,
                ["applyAction"] = ApplyActionCore,
                ["undo"] = r => { var s = S(r); s.Undo(); return StateOf(s, r); },
                ["redo"] = r => { var s = S(r); s.Redo(); return StateOf(s, r); },
                ["select"] = r => { var s = S(r); s.Select(r.Ids); return StateOf(s, r); },
                ["selectRect"] = SelectRectCore,
                ["hitTest"] = HitTestCore,
                ["snapMove"] = SnapMoveCore,
                ["snapPoint"] = SnapPointCore,
                ["snapInsert"] = SnapInsertCore,
                ["setSnap"] = r => { var s = S(r); s.Snap = r.SnapOptions ?? new SnapOptions(); return s.Snap; },
                ["contextActions"] = ContextActionsCore,
                ["inspector"] = InspectorCore,
                ["renderPageSvg"] = RenderSvgCore,
                ["drawPlan"] = DrawPlanCore,
                ["exportCompact"] = r => { var s = S(r); return new { payload = CompactOf(s.Doc, s.Page, s.Resolve(r.Ids)) }; },
                ["importCompact"] = ImportCompactCore,
                ["hostTransform"] = HostTransformCore,
            };
        }

        /// <summary>Shared instance with <see cref="ModuleCatalog.CreateDefault"/>.</summary>
        public static EngineApi Default => LazyDefault.Value;

        public Registry Registry { get; }
        public SessionStore Sessions { get; } = new();

        public IEnumerable<string> Methods => _methods.Keys;

        // ------------------------------------------------------------------ envelope + typed entry points

        /// <summary>Dispatches a method by name and returns <c>{"ok":…}</c>; never throws.</summary>
        public string Call(string method, string requestJson)
        {
            try
            {
                var result = Invoke(method, requestJson);
                return "{\"ok\":true,\"result\":" + result + "}";
            }
            catch (Exception ex)
            {
                var msg = ex is TargetInvocationException { InnerException: not null } t ? t.InnerException!.Message : ex.Message;
                return "{\"ok\":false,\"error\":" + JsonSerializer.Serialize(msg) + "}";
            }
        }

        /// <summary>Dispatches a method by name and returns the bare result JSON; throws on errors.</summary>
        public string Invoke(string method, string requestJson)
        {
            if (!_methods.TryGetValue(method, out var f)) throw new ArgumentException($"Unknown method '{method}'.");
            var req = string.IsNullOrWhiteSpace(requestJson) ? new EngineRequest() : JsonSerializer.Deserialize<EngineRequest>(requestJson, DocumentJson.Options) ?? new EngineRequest();
            return JsonSerializer.Serialize(f(req), DocumentJson.Options);
        }

        public string Version() => Invoke("version", "{}");
        public string CreateSession(string json) => Invoke("createSession", json);
        public string OpenDocument(string json) => Invoke("openDocument", json);
        public string SaveDocument(string json) => Invoke("saveDocument", json);
        public string State(string json) => Invoke("state", json);
        public string CatalogJson(string lang) => Invoke("catalog", "{\"lang\":" + JsonSerializer.Serialize(lang) + "}");
        public string Insert(string json) => Invoke("insert", json);
        public string Update(string json) => Invoke("update", json);
        public string Move(string json) => Invoke("move", json);
        public string Scale(string json) => Invoke("scale", json);
        public string Rotate(string json) => Invoke("rotate", json);
        public string Flip(string json) => Invoke("flip", json);
        public string Duplicate(string json) => Invoke("duplicate", json);
        public string Delete(string json) => Invoke("delete", json);
        public string Group(string json) => Invoke("group", json);
        public string Reorder(string json) => Invoke("reorder", json);
        public string SetView(string json) => Invoke("setView", json);
        public string SetParam(string json) => Invoke("setParam", json);
        public string SetStyle(string json) => Invoke("setStyle", json);
        public string SetText(string json) => Invoke("setText", json);
        public string ApplyAction(string json) => Invoke("applyAction", json);
        public string SetPlanScale(string json) => Invoke("setPlanScale", json);
        public string Undo(string json) => Invoke("undo", json);
        public string Redo(string json) => Invoke("redo", json);
        public string HitTest(string json) => Invoke("hitTest", json);
        public string SnapMove(string json) => Invoke("snapMove", json);
        public string SnapPoint(string json) => Invoke("snapPoint", json);
        public string ContextActions(string json) => Invoke("contextActions", json);
        public string RenderPageSvg(string json) => Invoke("renderPageSvg", json);
        public string DrawPlanJson(string json) => Invoke("drawPlan", json);

        // ------------------------------------------------------------------ helpers

        private EditorSession S(EngineRequest r) => Sessions.Get(r.Session);

        private static Vec2 Pivot(EngineRequest r, EditorSession s, IReadOnlyList<string> ids)
        {
            if (r.PivotX is double px && r.PivotY is double py) return new Vec2(px, py);
            var b = s.Bounds(ids);
            return b?.Center ?? Vec2.Zero;
        }

        private EditorState Edit(EngineRequest r, string label, Func<EditorSession, IReadOnlyList<string>, SketchDocument> edit)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            if (ids.Count > 0) s.Apply(label, _ => edit(s, ids));
            if (r.Ids is not null && ids.Count > 0) s.Select(ids);
            return StateOf(s, r);
        }

        private static string NameOf(Registry reg, SketchObject o, Lang lang) => reg.Type(o.Type)?.Name.Get(lang) ?? o.Type;

        public EditorState StateOf(EditorSession s, EngineRequest? r = null)
        {
            var doc = s.Doc;
            var st = new EditorState
            {
                Session = s.Id,
                Revision = s.Revision,
                PageId = s.Page.Id,
                PageWidth = s.Page.Size.W,
                PageHeight = s.Page.Size.H,
                Grid = s.Snap.Grid ?? 0,
                Selection = s.Selection.ToList(),
                SelectionBounds = s.Bounds(s.Selection),
                CanUndo = s.History.CanUndo,
                CanRedo = s.History.CanRedo,
                UndoLabel = s.History.UndoLabel,
                PlanScale = doc.Settings.PlanScale,
                Changed = s.LastChanged.ToList(),
                Dependents = s.LastDependents.ToList(),
            };
            foreach (var o in doc.ObjectsOn(s.Page))
            {
                var type = Registry.Type(o.Type);
                st.Objects.Add(new ObjectInfo
                {
                    Id = o.Id, Type = o.Type, Name = NameOf(Registry, o, s.Lang), Kind = o.IsPointGeometry ? "points" : "box", Known = type is not null,
                    Bounds = s.ObjectBounds(o), Cx = o.Transform.X, Cy = o.Transform.Y, W = o.Size.W, H = o.Size.H,
                    Rotation = o.Transform.Rotation, FlipX = o.Transform.FlipX, FlipY = o.Transform.FlipY,
                    Points = o.IsPointGeometry ? doc.ResolvePoints(o).ToList() : null,
                    GroupId = o.GroupId, Locked = o.Locked, View = o.View ?? type?.DefaultView, Uniform = type?.PreferUniformScale ?? false,
                    HasText = type?.HasText ?? false, Text = o.Text, Ref = o.Ref,
                });
            }
            if (r?.IncludeSvg != false)
                st.Svg = SvgRenderer.RenderPage(Registry, doc, s.Page, new SvgOptions { Background = null }, s.Lang);
            return st;
        }

        private object VersionInfo() => new
        {
            engine = typeof(EngineApi).Assembly.GetName().Version?.ToString(),
            format = SketchDocument.CurrentVersion,
            modules = Registry.Modules.Select(m => new { id = m.Id, name = m.Name.En, version = m.Version, types = m.Types.Count() }).ToList(),
            methods = _methods.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
        };

        // ------------------------------------------------------------------ sessions + documents

        private object CreateSessionCore(EngineRequest r)
        {
            var size = r.PageWidth is double w && r.PageHeight is double h && w > 0 && h > 0 ? new Size2(w, h) : (Size2?)null;
            var doc = SketchDocument.Create(size, r.Title);
            var id = string.IsNullOrEmpty(r.Session) ? Ids.New("s") : r.Session!;
            var s = Sessions.Add(new EditorSession(id, Registry, doc, Language.Detect(r.Lang)));
            if (r.Json is not null) s.Reset(DocumentJson.Deserialize(r.Json));
            return StateOf(s, r);
        }

        private object NewDocumentCore(EngineRequest r)
        {
            var s = S(r);
            var size = r.PageWidth is double w && r.PageHeight is double h && w > 0 && h > 0 ? new Size2(w, h) : (Size2?)null;
            s.Reset(SketchDocument.Create(size, r.Title));
            return StateOf(s, r);
        }

        private object OpenDocumentCore(EngineRequest r)
        {
            if (r.Json is null) throw new ArgumentException("Missing 'json'.");
            var doc = DocumentJson.Deserialize(r.Json);
            if (string.IsNullOrEmpty(r.Session) || r.Session is not null && !SessionExists(r.Session))
            {
                var s0 = Sessions.Add(new EditorSession(string.IsNullOrEmpty(r.Session) ? Ids.New("s") : r.Session!, Registry, doc, Language.Detect(r.Lang)));
                return StateOf(s0, r);
            }
            var s = S(r);
            s.Reset(doc);
            return StateOf(s, r);
        }

        private bool SessionExists(string id)
        {
            try { Sessions.Get(id); return true; } catch (KeyNotFoundException) { return false; }
        }

        private object SetPageCore(EngineRequest r)
        {
            var s = S(r);
            if (r.PageWidth is double w && r.PageHeight is double h && w > 0 && h > 0)
            {
                var size = new Size2(w, h);
                if (s.Page.Size != size)
                    s.Apply("Page size", d => d with { Pages = d.Pages.Select(p => p.Id == s.PageId ? p with { Size = size } : p).ToList() });
            }
            if (r.Lang is not null) s.Lang = Language.Detect(r.Lang);
            return StateOf(s, r);
        }

        // ------------------------------------------------------------------ catalog

        public Catalog CatalogOf(Lang lang)
        {
            if (_catalogs.TryGetValue(lang, out var cached)) return cached;
            var cat = new Catalog { Lang = lang == Lang.De ? "de" : "en" };
            foreach (var c in Registry.Categories.OrderBy(c => c.Order).ThenBy(c => c.Name.Get(lang), StringComparer.CurrentCulture))
                cat.Categories.Add(new CatalogCategory { Id = c.Id, Name = c.Name.Get(lang), Module = c.ModuleId, Order = c.Order });
            foreach (var item in Registry.Library)
            {
                var type = Registry.Type(item.TypeId)!;
                string thumb;
                try
                {
                    var o = Registry.Create(item, Vec2.Zero);
                    var doc = SketchDocument.Create();
                    doc = Edits.Add(doc, doc.Pages[0].Id, o);
                    var prims = Registry.Render(o, new RenderContext(doc, lang));
                    thumb = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null }, padding: 1.5);
                }
                catch (Exception ex)
                {
                    thumb = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><title>" + SvgRenderer.Esc(ex.Message) + "</title></svg>";
                }
                cat.Items.Add(new CatalogItem
                {
                    Id = item.Id, TypeId = item.TypeId, Name = item.Name.Get(lang), Category = item.Category,
                    Keywords = item.Keywords.Concat(type.Keywords).Concat(new[] { item.Name.En, item.Name.De ?? "" }).Where(k => k.Length > 0).Distinct().ToList(),
                    Thumbnail = thumb, Points = type.Geometry == GeometryKind.Points,
                });
            }
            _catalogs[lang] = cat;
            return cat;
        }

        // ------------------------------------------------------------------ editing

        private object InsertCore(EngineRequest r)
        {
            var s = S(r);
            var item = Registry.Item(r.ItemId ?? "") ?? throw new KeyNotFoundException($"Unknown library item '{r.ItemId}'.");
            var at = new Vec2(r.X ?? s.Page.Size.W / 2, r.Y ?? s.Page.Size.H / 2);
            var o = Registry.Create(item, at);
            if (r.Snap == true)
            {
                var snap = SnapNew(s, o, r.Tolerance);
                o = Translate(o, new Vec2(snap.Dx, snap.Dy));
            }
            s.Apply("Insert " + item.Name.En, d => Edits.Add(d, s.PageId, o));
            s.Select(new[] { o.Id });
            return StateOf(s, r);
        }

        private static SketchObject Translate(SketchObject o, Vec2 d) => o.IsPointGeometry
            ? o with { Points = o.Points!.Select(p => p + d).ToList() }
            : o with { Transform = o.Transform with { X = o.Transform.X + d.X, Y = o.Transform.Y + d.Y } };

        private object UpdateCore(EngineRequest r)
        {
            var s = S(r);
            var o = r.Object ?? throw new ArgumentException("Missing 'object'.");
            if (!s.Doc.Objects.ContainsKey(o.Id)) throw new KeyNotFoundException($"Unknown object '{o.Id}'.");
            if (Registry.Type(o.Type) is null) throw new ArgumentException($"Unknown type '{o.Type}'.");
            s.Apply("Edit", d => Edits.Replace(d, o));
            return StateOf(s, r);
        }

        private object MoveCore(EngineRequest r)
            => Edit(r, "Move", (s, ids) => Edits.Move(s.Doc, ids, new Vec2(r.Dx ?? 0, r.Dy ?? 0)));

        private object ScaleCore(EngineRequest r)
            => Edit(r, "Scale", (s, ids) => Edits.Scale(s.Doc, ids, Pivot(r, s, ids), r.Sx ?? 1, r.Sy ?? r.Sx ?? 1));

        /// <summary>Sets size and centre of one box object (handle drag in the object's own frame).</summary>
        private object ResizeCore(EngineRequest r)
        {
            var s = S(r);
            var o = s.Doc.Get(r.Id ?? "") ?? throw new KeyNotFoundException($"Unknown object '{r.Id}'.");
            if (o.IsPointGeometry || o.Locked) return StateOf(s, r);
            var w = Math.Max(0.5, r.W ?? o.Size.W);
            var h = Math.Max(0.5, r.H ?? o.Size.H);
            s.Apply("Resize", d => Edits.Update(d, new[] { o.Id }, x => x with
            {
                Size = new Size2(w, h),
                Transform = x.Transform with { X = r.X ?? x.Transform.X, Y = r.Y ?? x.Transform.Y },
            }));
            return StateOf(s, r);
        }

        private object RotateCore(EngineRequest r)
            => Edit(r, "Rotate", (s, ids) => Edits.Rotate(s.Doc, ids, Pivot(r, s, ids), r.Degrees ?? 90));

        private object FlipCore(EngineRequest r)
            => Edit(r, "Flip", (s, ids) => Edits.Flip(s.Doc, ids, Pivot(r, s, ids), r.Horizontal ?? true));

        private object DuplicateCore(EngineRequest r)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            if (ids.Count == 0) return StateOf(s, r);
            IReadOnlyList<string> newIds = Array.Empty<string>();
            s.Apply("Duplicate", d =>
            {
                var (doc, created) = Edits.Duplicate(d, s.PageId, ids, new Vec2(r.Dx ?? 5, r.Dy ?? 5));
                newIds = created;
                return doc;
            });
            s.Select(newIds);
            return StateOf(s, r);
        }

        private object DeleteCore(EngineRequest r)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            if (ids.Count > 0) s.Apply("Delete", d => Edits.Remove(d, ids));
            s.Select(Array.Empty<string>());
            return StateOf(s, r);
        }

        private object ReorderCore(EngineRequest r)
        {
            const int Far = 1_000_000;
            var step = (r.To ?? "front") switch { "back" => -Far, "forward" => 1, "backward" => -1, _ => Far };
            return Edit(r, "Order", (s, ids) => Edits.Reorder(s.Doc, s.PageId, ids, step));
        }

        /// <summary>Switches the view; box objects adapt their size to the new view (<see cref="ObjectType.SizeForView"/>).</summary>
        private object SetViewCore(EngineRequest r)
            => Edit(r, "View", (s, ids) =>
            {
                var ctx = s.Context();
                return Edits.Update(s.Doc, ids, o => Registry.WithView(o, r.View, ctx));
            });

        /// <summary>Sets (value &gt; 0) or clears (null, absent or &lt;= 0) the document plan scale; one undo step.</summary>
        private object SetPlanScaleCore(EngineRequest r)
        {
            var s = S(r);
            double? scale = r.Value is { ValueKind: JsonValueKind.Number } v && v.GetDouble() > 0 ? v.GetDouble() : null;
            if (s.Doc.Settings.PlanScale != scale)
                s.Apply("Plan scale", d => d with { Settings = d.Settings with { PlanScale = scale } });
            return StateOf(s, r);
        }

        private static ParamValue? ToParam(JsonElement? v) => v?.ValueKind switch
        {
            JsonValueKind.Number => new ParamValue { Number = v.Value.GetDouble() },
            JsonValueKind.String => new ParamValue { Text = v.Value.GetString() },
            JsonValueKind.True => new ParamValue { Bool = true },
            JsonValueKind.False => new ParamValue { Bool = false },
            _ => (ParamValue?)null,
        };

        private object SetParamCore(EngineRequest r)
        {
            if (string.IsNullOrEmpty(r.Key)) throw new ArgumentException("Missing 'key'.");
            var value = ToParam(r.Value);
            return Edit(r, "Parameter", (s, ids) => Edits.Update(s.Doc, ids, o =>
            {
                if (value is ParamValue pv) return o.WithParam(r.Key!, pv);
                var d = o.Params.Where(kv => kv.Key != r.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
                return o with { Params = d };
            }));
        }

        private object SetStyleCore(EngineRequest r)
        {
            var style = r.Style ?? new Style();
            return Edit(r, "Style", (s, ids) => Edits.Update(s.Doc, ids, o => o with
            {
                Style = r.Replace == true ? style : (o.Style ?? new Style()).Merge(style),
            }));
        }

        private object SetTextCore(EngineRequest r)
        {
            var ids = r.Ids ?? (r.Id is not null ? new List<string> { r.Id } : null);
            return Edit(new EngineRequest { Session = r.Session, Ids = ids, IncludeSvg = r.IncludeSvg }, "Text",
                (s, list) => Edits.Update(s.Doc, list, o => o with { Text = r.Text }));
        }

        private object SetPointCore(EngineRequest r)
        {
            var s = S(r);
            var o = s.Doc.Get(r.Id ?? "") ?? throw new KeyNotFoundException($"Unknown object '{r.Id}'.");
            var p = new Vec2(r.X ?? 0, r.Y ?? 0);
            Attachment? attach = null;
            if (r.Snap == true)
            {
                var sp = SnapPointAt(s, p, new[] { o.Id }, r.Tolerance);
                p = new Vec2(sp.X, sp.Y);
                attach = sp.Attach;
            }
            s.Apply("Point", d => Edits.SetPoint(d, o.Id, r.Index ?? 0, p, attach));
            return StateOf(s, r);
        }

        private object HostTransformCore(EngineRequest r)
        {
            var from = r.From ?? throw new ArgumentException("Missing 'from'.");
            var to = r.Target ?? throw new ArgumentException("Missing 'target'.");
            const double pt = Geo.PtPerMm;
            return Edit(r, "Edit on slide", (s, ids) =>
            {
                var doc = s.Doc;
                var sx = from.W > 1e-6 ? to.W / from.W : 1;
                var sy = from.H > 1e-6 ? to.H / from.H : 1;
                if (Math.Abs(sx - 1) > 1e-4 || Math.Abs(sy - 1) > 1e-4)
                    doc = Edits.Scale(doc, ids, new Vec2(from.X / pt, from.Y / pt), sx, sy);
                var d = new Vec2((to.X - from.X) / pt, (to.Y - from.Y) / pt);
                if (d.Length > 1e-6) doc = Edits.Move(doc, ids, d);
                if (r.Rotation is double rot && Math.Abs(rot) > 1e-6)
                    doc = Edits.Rotate(doc, ids, new Vec2((to.X + to.W / 2) / pt, (to.Y + to.H / 2) / pt), rot);
                return doc;
            });
        }

        // ------------------------------------------------------------------ selection + hit-testing

        private object SelectRectCore(EngineRequest r)
        {
            var s = S(r);
            var rect = Normalize(new Rect(r.X ?? 0, r.Y ?? 0, r.W ?? 0, r.H ?? 0));
            var touching = string.Equals(r.Mode, "touching", StringComparison.OrdinalIgnoreCase);
            var hits = new List<string>();
            foreach (var o in s.Doc.ObjectsOn(s.Page))
            {
                if (Registry.Type(o.Type) is null) continue;
                var b = s.ObjectBounds(o);
                if (touching ? rect.Intersects(b) && Touches(s, o, rect) : rect.Contains(b)) hits.Add(o.Id);
            }
            s.Select(r.Add == true ? s.Selection.Concat(hits) : hits);
            return StateOf(s, r);
        }

        private static Rect Normalize(Rect r) => new(Math.Min(r.X, r.X + r.W), Math.Min(r.Y, r.Y + r.H), Math.Abs(r.W), Math.Abs(r.H));

        private bool Touches(EditorSession s, SketchObject o, Rect rect)
        {
            if (!o.IsPointGeometry) return Geo.PointInPolygon(rect.Center, o.Corners()) || Polyline(o.Corners(), true).Any(seg => SegmentTouchesRect(seg.A, seg.B, rect)) || o.Corners().Any(rect.Contains);
            foreach (var poly in Outlines(Registry.Render(o, s.Context())))
                foreach (var seg in Polyline(poly.Points, poly.Closed))
                    if (SegmentTouchesRect(seg.A, seg.B, rect)) return true;
            return false;
        }

        private static IEnumerable<(Vec2 A, Vec2 B)> Polyline(IReadOnlyList<Vec2> pts, bool closed)
        {
            for (var i = 0; i + 1 < pts.Count; i++) yield return (pts[i], pts[i + 1]);
            if (closed && pts.Count > 2) yield return (pts[pts.Count - 1], pts[0]);
        }

        private static bool SegmentTouchesRect(Vec2 a, Vec2 b, Rect r)
        {
            if (r.Contains(a) || r.Contains(b)) return true;
            var c = r.Corners();
            for (var i = 0; i < 4; i++) if (Geo.SegmentIntersection(a, b, c[i], c[(i + 1) % 4]) is not null) return true;
            return false;
        }

        /// <summary>Flattened outlines (curves sampled) of rendered primitives; text yields its approximate box.</summary>
        private static IEnumerable<(IReadOnlyList<Vec2> Points, bool Closed, bool Filled)> Outlines(IEnumerable<Primitive> prims)
        {
            foreach (var p in Prim.Flatten(prims))
            {
                switch (p)
                {
                    case LinePrim l:
                        yield return (new[] { l.A, l.B }, false, false);
                        break;
                    case TextPrim t:
                        {
                            var b = Prim.Bounds(new[] { t }) ?? default;
                            yield return (b.Corners(), true, true);
                            break;
                        }
                    case PathPrim path:
                        {
                            var filled = path.Style.EffectiveFill.Kind != FillKind.None;
                            List<Vec2>? cur = null;
                            var last = Vec2.Zero;
                            foreach (var sg in path.Segments)
                            {
                                switch (sg.Op)
                                {
                                    case SegOp.M:
                                        if (cur is { Count: >= 2 }) yield return (cur, false, false);
                                        cur = new List<Vec2> { sg.P }; last = sg.P; break;
                                    case SegOp.L: (cur ??= new List<Vec2>()).Add(sg.P); last = sg.P; break;
                                    case SegOp.C:
                                        cur ??= new List<Vec2>();
                                        for (var i = 1; i <= 12; i++) cur.Add(Geo.CubicAt(last, sg.C1, sg.C2, sg.P, i / 12.0));
                                        last = sg.P; break;
                                    case SegOp.Q:
                                        cur ??= new List<Vec2>();
                                        for (var i = 1; i <= 12; i++) cur.Add(Geo.QuadAt(last, sg.C1, sg.P, i / 12.0));
                                        last = sg.P; break;
                                    case SegOp.Z:
                                        if (cur is { Count: >= 2 }) yield return (cur, true, filled);
                                        cur = null; break;
                                }
                            }
                            if (cur is { Count: >= 2 }) yield return (cur, false, false);
                            break;
                        }
                    default:
                        foreach (var c in Hatching.Contours(p)) yield return (c, true, (DrawPlanBuilder.StyleOf(p)?.EffectiveFill.Kind ?? FillKind.None) != FillKind.None);
                        break;
                }
            }
        }

        private object HitTestCore(EngineRequest r)
        {
            var s = S(r);
            var p = new Vec2(r.X ?? 0, r.Y ?? 0);
            var tol = r.Tolerance ?? 1.5;
            var res = new HitResult();
            var ctx = s.Context();
            var hidden = new HashSet<string>(s.Doc.Layers.Where(l => !l.Visible).Select(l => l.Id));
            foreach (var o in s.Doc.ObjectsOn(s.Page).Reverse())
            {
                if (Registry.Type(o.Type) is null || (o.Layer is not null && hidden.Contains(o.Layer))) continue;
                if (Hit(s, o, p, tol, ctx)) res.Stack.Add(o.Id);
            }
            res.Id = res.Stack.FirstOrDefault();
            if (res.Id is not null) res.Ids = s.Doc.ExpandGroups(new[] { res.Id }).ToList();
            return res;
        }

        private bool Hit(EditorSession s, SketchObject o, Vec2 p, double tol, RenderContext ctx)
        {
            if (!o.IsPointGeometry)
            {
                // Box objects are hit anywhere inside their (rotated) box: quick sketches have thin strokes.
                var local = o.Matrix().Invert().Apply(p);
                return Math.Abs(local.X) <= o.Size.W / 2 + tol && Math.Abs(local.Y) <= o.Size.H / 2 + tol;
            }
            foreach (var (pts, closed, filled) in Outlines(Registry.Render(o, ctx)))
            {
                if (filled && closed && pts.Count >= 3 && Geo.PointInPolygon(p, pts)) return true;
                foreach (var (a, b) in Polyline(pts, closed))
                    if (Geo.ClosestOnSegment(p, a, b).Point.DistanceTo(p) <= tol) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ snapping

        private SnapOptions Options(EditorSession s, double? tolerance) => tolerance is double t && t > 0 ? s.Snap with { Tolerance = t } : s.Snap;

        private object SnapMoveCore(EngineRequest r)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            var ctx = s.Context();
            var opts = Options(s, r.Tolerance);
            var moving = ids.Select(s.Doc.Get).Where(o => o is not null && Registry.Type(o.Type) is not null)
                .SelectMany(o => Registry.PageAnchors(o!, ctx)).ToList();
            var targets = Snapper.Targets(Registry, s.Doc, s.Page, new HashSet<string>(ids), ctx, opts);
            var delta = new Vec2(r.Dx ?? 0, r.Dy ?? 0);
            if (moving.Count == 0) return new SnapResultDto { Dx = delta.X, Dy = delta.Y };
            var res = Snapper.SnapMove(moving, delta, targets, opts);
            return new SnapResultDto { Dx = res.Delta.X, Dy = res.Delta.Y, Kind = res.Kind, Guides = res.Guides };
        }

        private object SnapPointCore(EngineRequest r)
        {
            var s = S(r);
            return SnapPointAt(s, new Vec2(r.X ?? 0, r.Y ?? 0), r.Exclude ?? new List<string>(), r.Tolerance);
        }

        private SnapResultDto SnapPointAt(EditorSession s, Vec2 p, IReadOnlyCollection<string> exclude, double? tolerance)
        {
            var ctx = s.Context();
            var opts = Options(s, tolerance);
            var targets = Snapper.Targets(Registry, s.Doc, s.Page, new HashSet<string>(exclude), ctx, opts);
            var res = Snapper.SnapPoint(p, targets, opts);
            var q = p + res.Delta;
            var dto = new SnapResultDto { Dx = res.Delta.X, Dy = res.Delta.Y, X = q.X, Y = q.Y, Kind = res.Kind, Guides = res.Guides };
            // Attach the point to the box object that owns the snapped anchor (it then follows that object); pins keep
            // their anchor id, rails (lifelines, lanes) and mounts attach at the snapped position.
            if (res.Kind is "point" or "port" or "rail" or "mount")
                dto.Attach = Attachments.FromSnap(s.Doc, q, res);
            return dto;
        }

        private SnapResultDto SnapNew(EditorSession s, SketchObject o, double? tolerance)
        {
            var doc = Edits.Add(s.Doc, s.PageId, o);
            var ctx = new RenderContext(doc, s.Lang);
            var opts = Options(s, tolerance);
            var page = doc.Pages.First(p => p.Id == s.PageId);
            var moving = Registry.PageAnchors(o, ctx).ToList();
            var targets = Snapper.Targets(Registry, doc, page, new HashSet<string> { o.Id }, ctx, opts);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, opts);
            return new SnapResultDto { Dx = res.Delta.X, Dy = res.Delta.Y, Kind = res.Kind, Guides = res.Guides };
        }

        /// <summary>Snap preview for "insert at pointer": where the new item would land, without inserting it.</summary>
        private object SnapInsertCore(EngineRequest r)
        {
            var s = S(r);
            var item = Registry.Item(r.ItemId ?? "") ?? throw new KeyNotFoundException($"Unknown library item '{r.ItemId}'.");
            var at = new Vec2(r.X ?? 0, r.Y ?? 0);
            var o = Registry.Create(item, at);
            var dto = SnapNew(s, o, r.Tolerance);
            dto.X = at.X + dto.Dx;
            dto.Y = at.Y + dto.Dy;
            return dto;
        }

        // ------------------------------------------------------------------ context actions + inspector

        private static ActionInfo A(string id, string en, string de, string icon, string group, Lang lang, bool enabled = true, bool active = false)
            => new() { Id = id, Label = lang == Lang.De ? de : en, Icon = icon, Group = group, Enabled = enabled, Active = active };

        public List<ActionInfo> ActionsFor(EditorSession s, IReadOnlyList<string> ids, Lang lang)
        {
            var ctx = new RenderContext(s.Doc, lang);
            var objs = ids.Select(s.Doc.Get).Where(o => o is not null).Cast<SketchObject>().ToList();
            var any = objs.Count > 0;
            var groups = new HashSet<string?>(objs.Select(o => o.GroupId));
            var list = new List<ActionInfo>
            {
                // Delete is always first (top of the pie menu).
                A("delete", "Delete", "Löschen", "delete", "edit", lang, any),
                A("duplicate", "Duplicate", "Duplizieren", "duplicate", "edit", lang, any),
                A("rotate90", "Rotate 90°", "90° drehen", "rotate", "transform", lang, any),
                A("flipH", "Flip horizontal", "Horizontal spiegeln", "flipH", "transform", lang, any),
                A("flipV", "Flip vertical", "Vertikal spiegeln", "flipV", "transform", lang, any),
                A("front", "Bring to front", "In den Vordergrund", "front", "order", lang, any),
                A("back", "Send to back", "In den Hintergrund", "back", "order", lang, any),
                A("group", "Group", "Gruppieren", "group", "group", lang, objs.Count >= 2 && (groups.Count > 1 || groups.Contains(null))),
                A("ungroup", "Ungroup", "Gruppierung aufheben", "ungroup", "group", lang, objs.Any(o => o.GroupId is not null)),
                A("numeral", "Reference numeral", "Bezugszeichen", "numeral", "patent", lang, any && objs.Any(o => o.Ref is null)),
            };
            if (objs.Count == 0) return list;
            // Views and type actions: offered when all selected objects share the type.
            var typeId = objs[0].Type;
            if (objs.All(o => o.Type == typeId) && Registry.Type(typeId) is ObjectType type)
            {
                var current = type.ViewOf(objs[0]);
                foreach (var v in type.Views)
                    list.Add(new ActionInfo { Id = "view:" + v.Id, Label = v.Label.Get(lang), Icon = IconIds.ForView(v), Group = "view", Kind = "view", Active = v.Id == current });
                foreach (var a in type.Actions(objs[0], ctx))
                    list.Add(new ActionInfo
                    {
                        Id = "t:" + a.Id, Label = a.Label.Get(lang), Icon = a.Icon, Group = a.Group ?? "type", Kind = "type", Active = a.IsActive, Enabled = a.IsEnabled,
                        Input = a.Input is null ? null : new ActionInputInfo { Label = a.Input.Label.Get(lang), Value = a.Input.Value, Unit = a.Input.Unit },
                    });
            }
            return list;
        }

        private object ContextActionsCore(EngineRequest r)
        {
            var s = S(r);
            return ActionsFor(s, s.Resolve(r.Ids), r.Lang is null ? s.Lang : Language.Detect(r.Lang));
        }

        private object ApplyActionCore(EngineRequest r)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            var action = r.Action ?? throw new ArgumentException("Missing 'action'.");
            var sub = new EngineRequest { Session = r.Session, Ids = ids.ToList(), IncludeSvg = r.IncludeSvg };
            switch (action)
            {
                case "delete": return DeleteCore(sub);
                case "duplicate": return DuplicateCore(sub);
                case "rotate90": sub.Degrees = 90; return RotateCore(sub);
                case "flipH": sub.Horizontal = true; return FlipCore(sub);
                case "flipV": sub.Horizontal = false; return FlipCore(sub);
                case "front": case "back": case "forward": case "backward": sub.To = action; return ReorderCore(sub);
                case "group": return _methods["group"](sub)!;
                case "ungroup": return _methods["ungroup"](sub)!;
                case "numeral": return NumeralCore(s, ids, r);
            }
            if (action.StartsWith("view:", StringComparison.Ordinal)) { sub.View = action.Substring(5); return SetViewCore(sub); }
            if (action.StartsWith("t:", StringComparison.Ordinal))
            {
                var id = action.Substring(2);
                s.Apply("Action", d => InventaSketch.Core.ContextActions.Run(Registry, d, ids, id, r.Input, s.Lang));
                return StateOf(s, r);
            }
            throw new ArgumentException($"Unknown action '{action}'.");
        }

        /// <summary>Assigns the next free reference numeral to objects without one (and places a numeral label if the patent module provides "patent.numeral").</summary>
        private object NumeralCore(EditorSession s, IReadOnlyList<string> ids, EngineRequest r)
        {
            var targets = ids.Select(s.Doc.Get).Where(o => o is not null && o.Ref is null).Cast<SketchObject>().ToList();
            if (targets.Count == 0) return StateOf(s, r);
            var numeralType = Registry.Type("patent.numeral");
            var created = new List<string>();
            s.Apply("Reference numeral", d =>
            {
                var next = d.References.Keys.Select(k => int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
                foreach (var o in targets)
                {
                    var numeral = next.ToString(CultureInfo.InvariantCulture);
                    next++;
                    d = Edits.SetReference(d, numeral, NameOf(Registry, o, s.Lang));
                    d = Edits.Update(d, new[] { o.Id }, x => x with { Ref = numeral });
                    if (numeralType is not null)
                    {
                        var b = o.IsPointGeometry ? Rect.FromPoints(d.ResolvePoints(o)) : o.Bounds();
                        var label = numeralType.Create(new Vec2(b.Right + 6, b.Y - 4)) with { Text = numeral, Ref = numeral };
                        d = Edits.Add(d, s.PageId, label);
                        created.Add(label.Id);
                    }
                }
                return d;
            });
            return StateOf(s, r);
        }

        private object InspectorCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var ids = s.Resolve(r.Ids);
            var info = new InspectorInfo { Ids = ids.ToList() };
            var objs = ids.Select(s.Doc.Get).Where(o => o is not null).Cast<SketchObject>().ToList();
            if (objs.Count == 0) return info;
            var first = objs[0];
            info.Style = first.Style;
            info.Points = first.IsPointGeometry;
            if (!objs.All(o => o.Type == first.Type) || Registry.Type(first.Type) is not ObjectType type) return info;
            info.TypeId = type.Id;
            info.TypeName = type.Name.Get(lang);
            info.View = type.ViewOf(first);
            info.Views = type.Views.Select(v => new ActionInfo { Id = v.Id, Label = v.Label.Get(lang), Icon = IconIds.ForView(v), Kind = "view", Group = "view", Active = v.Id == info.View }).ToList();
            info.HasText = type.HasText;
            info.Text = first.Text;
            info.Ref = first.Ref;
            info.UsesPlanScale = type.UsesPlanScale;
            info.PlanScale = s.Doc.Settings.PlanScale;
            foreach (var p in type.Parameters)
            {
                info.Params.Add(new ParamInfo
                {
                    Key = p.Key, Label = p.Label.Get(lang), Kind = char.ToLowerInvariant(p.Kind.ToString()[0]) + p.Kind.ToString().Substring(1),
                    Min = p.Min, Max = p.Max, Step = p.Step, Unit = p.Unit, Advanced = p.Advanced,
                    Options = p.Options?.Select(o => new ActionInfo { Id = o.Value, Label = o.Label.Get(lang), Kind = "option" }).ToList(),
                    Value = first.Params.TryGetValue(p.Key, out var v) ? v : (ParamValue?)null,
                    Default = p.Default,
                });
            }
            return info;
        }

        // ------------------------------------------------------------------ output

        private object RenderSvgCore(EngineRequest r)
        {
            var s = S(r);
            var options = new SvgOptions { Monochrome = r.Monochrome ?? false, Background = r.Background ?? "#ffffff" };
            var doc = s.Doc;
            var page = s.Page;
            if (r.Ids is { Count: > 0 })
            {
                var ids = s.Resolve(r.Ids);
                var keep = new HashSet<string>(ids);
                page = page with { Order = page.Order.Where(keep.Contains).ToList() };
                var b = s.Bounds(ids) ?? new Rect(0, 0, page.Size.W, page.Size.H);
                options = options with { ViewBox = b.Inflate(r.Padding ?? 3) };
                doc = doc with { Pages = doc.Pages.Select(p => p.Id == page.Id ? page : p).ToList() };
            }
            var svg = SvgRenderer.RenderPage(Registry, doc, page, options, s.Lang);
            var vb = options.ViewBox ?? new Rect(0, 0, page.Size.W, page.Size.H);
            return new { svg, width = vb.W, height = vb.H };
        }

        private object DrawPlanCore(EngineRequest r)
        {
            var s = S(r);
            var ids = r.Ids is { Count: > 0 } ? s.Resolve(r.Ids) : s.Page.Order.ToList();
            return DrawPlanBuilder.Build(s, ids, r.Capabilities ?? DrawCapabilities.OfficeJs);
        }

        /// <summary>Compact payload of the given objects (one-page document with their references).</summary>
        public static string CompactOf(SketchDocument doc, Page page, IEnumerable<string> ids)
        {
            var keep = new HashSet<string>(doc.ExpandGroups(ids));
            // Keep attachment targets so attached points still resolve.
            foreach (var id in keep.ToList())
                if (doc.Get(id)?.Attach is { } att)
                    foreach (var a in att) if (a is not null && doc.Objects.ContainsKey(a.ObjectId)) keep.Add(a.ObjectId);
            var objs = doc.Objects.Where(kv => keep.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            var refs = doc.References.Where(kv => objs.Values.Any(o => o.Ref == kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            var sub = doc with
            {
                Objects = objs,
                References = refs,
                Pages = new[] { page with { Order = page.Order.Where(keep.Contains).ToList() } },
            };
            return DocumentJson.ToCompact(sub);
        }

        private object ImportCompactCore(EngineRequest r)
        {
            var s = S(r);
            var src = DocumentJson.FromCompact(r.Payload ?? throw new ArgumentException("Missing 'payload'."));
            var objs = src.Pages.SelectMany(p => src.ObjectsOn(p)).ToList();
            s.Apply("Load from slide", d =>
            {
                var existing = objs.Where(o => d.Objects.ContainsKey(o.Id)).ToList();
                var fresh = objs.Where(o => !d.Objects.ContainsKey(o.Id)).ToArray();
                foreach (var o in existing) d = Edits.Replace(d, o);
                if (fresh.Length > 0) d = Edits.Add(d, s.PageId, fresh);
                foreach (var kv in src.References) if (!d.References.ContainsKey(kv.Key)) d = Edits.SetReference(d, kv.Key, kv.Value.Label);
                // A plan scale stored with the slide model is adopted when the editor document has none.
                if (d.Settings.PlanScale is null && src.Settings.PlanScale is double ps) d = d with { Settings = d.Settings with { PlanScale = ps } };
                return d;
            });
            if (r.Add != false) s.Select(objs.Select(o => o.Id));
            return StateOf(s, r);
        }
    }

    /// <summary>
    /// What a document change touched: the added/modified objects and the existing objects whose rendering depends on the
    /// touched ones (<see cref="ObjectType.Dependents"/>, evaluated in the document before and after the change). Hosts use
    /// it to redraw exactly the affected units in one undo step.
    /// </summary>
    public static class ChangeSet
    {
        public sealed class Result
        {
            public List<string> Changed { get; } = new();
            public List<string> Removed { get; } = new();
            public List<string> Dependents { get; } = new();
        }

        public static Result Compute(Registry registry, SketchDocument before, SketchDocument after, Lang lang)
        {
            var res = new Result();
            var changed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in after.Objects)
                if (!before.Objects.TryGetValue(kv.Key, out var old) || !ReferenceEquals(old, kv.Value)) changed.Add(kv.Key);
            // A new plan scale changes the rendering of every object that uses it.
            if (before.Settings.PlanScale != after.Settings.PlanScale)
                foreach (var o in after.Objects.Values)
                    if (registry.Type(o.Type)?.UsesPlanScale == true) changed.Add(o.Id);
            res.Changed.AddRange(InPageOrder(after, changed));
            foreach (var id in before.Objects.Keys) if (!after.Objects.ContainsKey(id)) res.Removed.Add(id);
            res.Dependents.AddRange(DependentsOf(registry, before, after, res.Changed.Concat(res.Removed).ToList(), lang));
            return res;
        }

        /// <summary>
        /// Existing objects (in <paramref name="after"/>) that depend on any of <paramref name="touched"/> before or after the
        /// change, excluding the touched objects themselves.
        /// </summary>
        public static List<string> DependentsOf(Registry registry, SketchDocument before, SketchDocument after, IReadOnlyCollection<string> touched, Lang lang)
        {
            var seen = new HashSet<string>(touched, StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var doc in new[] { before, after })
            {
                var ctx = new RenderContext(doc, lang);
                foreach (var id in touched)
                {
                    var o = doc.Get(id);
                    var type = o is null ? null : registry.Type(o.Type);
                    if (o is null || type is null) continue;
                    foreach (var dep in type.Dependents(o, ctx))
                        if (after.Objects.ContainsKey(dep) && !seen.Contains(dep)) found.Add(dep);
                }
            }
            return InPageOrder(after, found).ToList();
        }

        private static IEnumerable<string> InPageOrder(SketchDocument doc, ICollection<string> ids)
        {
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in doc.Pages)
                foreach (var id in p.Order)
                    if (ids.Contains(id) && emitted.Add(id)) yield return id;
            foreach (var id in ids.OrderBy(x => x, StringComparer.Ordinal))
                if (emitted.Add(id)) yield return id;
        }
    }
}
