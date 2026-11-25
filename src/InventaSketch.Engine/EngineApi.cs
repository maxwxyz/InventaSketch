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
    public sealed partial class EngineApi
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
                ["importData"] = ImportDataCore,
                ["importGeo"] = ImportGeoCore,
                ["preflight"] = PreflightCore,
                ["sanitize"] = SanitizeCore,
                ["diff"] = DiffCore,
                ["importVector"] = ImportVectorCore,
                ["importCode"] = ImportCodeCore,
                ["patentProfiles"] = PatentProfilesCore,
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
                ["setMonochrome"] = SetMonochromeCore,
                ["setLineGroup"] = SetLineGroupCore,
                ["setAccent"] = SetAccentCore,
                ["accents"] = AccentsCore,
                ["setMainText"] = SetMainTextCore,
                ["suggestions"] = SuggestionsCore,
                ["quickAdd"] = QuickAddCore,
                ["followers"] = FollowersCore,
                ["nextItems"] = NextItemsCore,
                ["replaceItem"] = ReplaceItemCore,
                ["conversions"] = ConversionsCore,
                ["exporters"] = ExportersCore,
                ["export"] = ExportCore,
                ["convert"] = ConvertCore,
                ["applyAction"] = ApplyActionCore,
                ["parts"] = PartsCore,
                ["partAt"] = PartAtCore,
                ["partActions"] = PartActionsCore,
                ["applyPartAction"] = ApplyPartActionCore,
                ["setPartLook"] = SetPartLookCore,
                ["partOutline"] = PartOutlineCore,
                ["dragHandles"] = DragHandlesCore,
                ["moveHandle"] = MoveHandleCore,
                ["sketchCurve"] = SketchCurveCore,
                ["chartFromPicture"] = ChartFromPictureCore,
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
                ["search"] = SearchCore,
                ["transformEffect"] = TransformEffectCore,
                ["arrangeOptions"] = ArrangeOptionsCore,
                ["arrange"] = ArrangeCore,
                ["assembly"] = AssemblyCore,
                ["connected"] = ConnectedCore,
                ["connectOptions"] = ConnectOptionsCore,
                ["trace"] = TraceCore,
                ["insertFitting"] = InsertFittingCore,
                ["drawTrace"] = DrawTraceCore,
                ["explode"] = ExplodeCore,
                ["dof"] = DofCore,
                ["drawGhosts"] = DrawGhostsCore,
                ["dimensionBetween"] = DimensionBetweenCore,
                ["couplingPreview"] = CouplingPreviewCore,
                ["buildPorts"] = BuildPortsCore,
                ["buildItems"] = BuildItemsCore,
                ["buildPlace"] = BuildPlaceCore,
                ["buildStretch"] = BuildStretchCore,
                ["buildStretchPreview"] = BuildStretchPreviewCore,
                ["motionInfo"] = MotionInfoCore,
                ["motionTurn"] = MotionTurnCore,
                ["motionDrag"] = MotionDragCore,
                ["motionPreview"] = MotionPreviewCore,
            };
            RegisterValueMethods();
            _methods["setValueLevel"] = SetValueLevelCore;
            _methods["insertHandles"] = InsertHandlesCore;
            _methods["applyInsertHandle"] = ApplyInsertHandleCore;
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
        public string SetMonochrome(string json) => Invoke("setMonochrome", json);
        public string SetLineGroup(string json) => Invoke("setLineGroup", json);
        public string SetAccent(string json) => Invoke("setAccent", json);
        public string SetMainText(string json) => Invoke("setMainText", json);
        public string Suggestions(string json) => Invoke("suggestions", json);
        public string Undo(string json) => Invoke("undo", json);
        public string Redo(string json) => Invoke("redo", json);
        public string HitTest(string json) => Invoke("hitTest", json);
        public string SnapMove(string json) => Invoke("snapMove", json);
        public string SnapPoint(string json) => Invoke("snapPoint", json);
        public string ContextActions(string json) => Invoke("contextActions", json);
        public string RenderPageSvg(string json) => Invoke("renderPageSvg", json);
        public string DrawPlanJson(string json) => Invoke("drawPlan", json);

        // ------------------------------------------------------------------ helpers

        private EditorSession S(EngineRequest r)
        {
            var s = Sessions.Get(r.Session);
            s.Couple = r.Couple != false;
            return s;
        }

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
                Monochrome = doc.Settings.Monochrome,
                LineGroup = doc.Settings.LineGroup ?? Style.DesignLineGroup,
                Accent = doc.Settings.Accent,
                ValueLevel = ValueLevelId(doc.Settings.EffectiveValueLevel),
                Changed = s.LastChanged.ToList(),
                Dependents = s.LastDependents.ToList(),
            };
            var selected = new HashSet<string>(s.Selection, StringComparer.Ordinal);
            RenderContext? ctx = null;
            foreach (var o in doc.ObjectsOn(s.Page))
            {
                var type = Registry.Type(o.Type);
                var isSelected = type is not null && selected.Contains(o.Id);
                if (isSelected) ctx ??= s.Context();
                st.Objects.Add(new ObjectInfo
                {
                    Id = o.Id, Type = o.Type, Name = NameOf(Registry, o, s.Lang), Kind = o.IsPointGeometry ? "points" : "box", Known = type is not null,
                    Bounds = s.ObjectBounds(o), Cx = o.Transform.X, Cy = o.Transform.Y, W = o.Size.W, H = o.Size.H,
                    Rotation = o.Transform.Rotation, FlipX = o.Transform.FlipX, FlipY = o.Transform.FlipY,
                    Points = o.IsPointGeometry ? doc.ResolvePoints(o).ToList() : null,
                    GroupId = o.GroupId, Locked = o.Locked, View = o.View ?? type?.DefaultView, Uniform = type?.PreferUniformScale ?? false,
                    HasText = type?.HasText ?? false, Text = o.Text, Ref = o.Ref,
                    // Only for the selection: anchors are evaluated per object.
                    Centred = isSelected && IsCentred(o, ctx!),
                    MainText = isSelected ? MainTextOf(Registry, o, s.Lang) : null,
                    Chain = isSelected && Flow.CanChain(Registry, o),
                    HasValues = isSelected && ValueModel.HasValues(type),
                });
            }
            if (r?.IncludeSvg != false)
                st.Svg = SvgRenderer.RenderPage(Registry, doc, s.Page, new SvgOptions { Background = null, Monochrome = doc.Settings.Monochrome }, s.Lang);
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

        // ------------------------------------------------------------------ importing data files

        /// <summary>
        /// A CSV/TSV table (<c>text</c>) as the data of chart <c>id</c> (default: the selected element with chart data;
        /// none selected: a new XY chart in the middle of the page); separator and decimal comma are detected
        /// (<see cref="Imports.CsvToChartData"/>). One undo step.
        /// </summary>
        private object ImportDataCore(EngineRequest r)
        {
            var s = S(r);
            var data = Imports.CsvToChartData(r.Text ?? "");
            if (data.Length == 0) throw new ArgumentException("The file has no rows.");
            bool HasData(SketchObject? o) => o is not null && Registry.Type(o.Type)?.Parameters.Any(p => p.Key == "data") == true;
            var target = r.Id is { } id ? s.Doc.Get(id) : s.Selection.Select(s.Doc.Get).FirstOrDefault(HasData);
            if (HasData(target))
            {
                s.Apply("Import data", d => Edits.Update(d, new[] { target!.Id }, o => o.WithParam("data", data)));
                s.Select(new[] { target!.Id });
                return StateOf(s, r);
            }
            var item = Registry.Library.FirstOrDefault(i => i.TypeId.StartsWith("chart.", StringComparison.Ordinal) && Registry.Type(i.TypeId)!.Parameters.Any(p => p.Key == "data"))
                ?? throw new InvalidOperationException("No chart type is installed.");
            var chart = Registry.Create(item, new Vec2(s.Page.Size.W / 2, s.Page.Size.H / 2)).WithParam("data", data);
            s.Apply("Import data", d => Edits.Add(d, s.PageId, chart));
            s.Select(new[] { chart.Id });
            return StateOf(s, r);
        }

        /// <summary>
        /// GeoJSON or GPX (<c>text</c>) as map elements fitted into the page (inset by a tenth): every track, route, line
        /// string and polygon ring as a route line, every point or waypoint as a marker (its name as the text); the
        /// first line gets start and finish markers if the file has no points. One undo step; the new elements are selected.
        /// </summary>
        private object ImportGeoCore(EngineRequest r)
        {
            var s = S(r);
            var geo = Imports.ParseGeo(r.Text ?? "") ?? throw new ArgumentException("Neither GeoJSON nor GPX.");
            if (geo.IsEmpty) throw new ArgumentException("The file has no lines or points.");
            if (Registry.Library.All(i => i.TypeId != "map.route")) throw new InvalidOperationException("The maps module is not installed.");
            var created = Imports.GeoElements(Registry, geo, s.Page.Size);
            if (created.Count == 0) throw new ArgumentException("The file has no usable lines or points.");
            s.Apply("Import map data", d => Edits.Add(d, s.PageId, created.ToArray()));
            s.Select(created.Select(o => o.Id));
            return StateOf(s, r);
        }

        /// <summary>
        /// A vector drawing (<c>text</c>: SVG or ASCII DXF) as editable basic shapes on the page (<see cref="VectorImport"/>):
        /// real size when the file has units and fits, else fitted into the page; the new elements form one group and are
        /// selected. One undo step.
        /// </summary>
        private object ImportVectorCore(EngineRequest r)
        {
            var s = S(r);
            var drawing = VectorImport.Parse(r.Text ?? "") ?? throw new ArgumentException("Neither SVG nor DXF.");
            if (drawing.IsEmpty) throw new ArgumentException("The drawing has no lines, shapes or texts that can be imported.");
            var created = VectorImport.Elements(Registry, drawing, s.Page.Size);
            if (created.Count == 0) throw new ArgumentException("The drawing has no shapes that can be imported.");
            if (created.Count > 1)
            {
                var gid = Ids.New("g");
                created = created.Select(o => o with { GroupId = gid }).ToList();
            }
            s.Apply("Import drawing", d => Edits.Add(d, s.PageId, created.ToArray()));
            s.Select(created.Select(o => o.Id));
            return StateOf(s, r);
        }

        /// <summary>
        /// importCode (roadmap 4b): plotting or drawing code (<c>text</c>) as editable elements on the current page. <c>mode</c>:
        /// "auto" (default: detected from the content; "latex" is the same), "matplotlib" (a Python script), "pgfplots" (LaTeX axes; tikzpictures
        /// without an axis in the same text are imported as drawings too) or "tikz" (LaTeX drawings). The code is read, never
        /// executed: matplotlib and pgfplots plots become charts and figures, TikZ drawings become lines, arrows, basic shapes
        /// and texts (one group). One undo step; the new elements are selected. Returns {state, format, ids, imported, skipped,
        /// message}: the report lines of what was imported and of every part that was not ({line, code, reason}), in the request
        /// or session language. Nothing importable is not an error: the report says why.
        /// </summary>
        private object ImportCodeCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var text = r.Text ?? "";
            if (text.Length > 4_000_000) throw new ArgumentException("The code is too long to be imported.");
            var hint = (r.Mode ?? "auto").Trim().ToLowerInvariant();
            var format = CodeImportFormat.FromHint(hint);
            if (format is null && hint is not ("" or "auto" or "latex" or "tex")) throw new ArgumentException("Unknown format '" + r.Mode + "' (auto, matplotlib, pgfplots, tikz, latex).");
            format ??= CodeImportFormat.Detect(text);
            var report = new CodeImportReport();
            var created = new List<SketchObject>();
            if (format == CodeImportFormat.Matplotlib || format == CodeImportFormat.Pgfplots)
            {
                var result = format == CodeImportFormat.Matplotlib
                    ? InventaSketch.Modules.Charts.ChartCodeImport.Matplotlib(Registry, text, s.Page.Size)
                    : InventaSketch.Modules.Charts.ChartCodeImport.Pgfplots(Registry, text, s.Page.Size);
                created.AddRange(result.Charts);
                report.Add(result.Report);
            }
            if (format == CodeImportFormat.Tikz || format == CodeImportFormat.Pgfplots && text.Contains("\\begin{tikzpicture}"))
            {
                var tikzReport = new CodeImportReport();
                var drawing = TikzImport.Parse(text, tikzReport, skipAxisPictures: format == CodeImportFormat.Pgfplots);
                var shapes = TikzImport.Elements(Registry, drawing, s.Page.Size).ToList();
                if (shapes.Count > 1)
                {
                    var gid = Ids.New("g");
                    shapes = shapes.Select(o => o with { GroupId = gid }).ToList();
                }
                if (shapes.Count > 0)
                    tikzReport.Imported.Add(new I18nText("Drawing: " + shapes.Count + " element" + (shapes.Count == 1 ? "" : "s"), "Zeichnung: " + shapes.Count + " Element" + (shapes.Count == 1 ? "" : "e")));
                created.AddRange(shapes);
                report.Add(tikzReport);
            }
            if (created.Count > 0)
            {
                s.Apply("Import code", d => Edits.Add(d, s.PageId, created.ToArray()));
                s.Select(created.Select(o => o.Id));
            }
            var message = format is null
                ? new I18nText("The text is neither a matplotlib script nor pgfplots or TikZ code.", "Der Text ist weder ein matplotlib-Skript noch pgfplots- oder TikZ-Code.")
                : created.Count == 0
                    ? new I18nText("Nothing could be imported; see the lines that were not imported.", "Nichts konnte importiert werden; siehe die nicht importierten Zeilen.")
                    : report.Skipped.Count == 0
                        ? new I18nText("Imported completely.", "Vollständig importiert.")
                        : new I18nText("Imported; " + report.Skipped.Count + " part(s) were not imported.", "Importiert; " + report.Skipped.Count + " Teil(e) wurden nicht importiert.");
            return new
            {
                state = StateOf(s, r),
                format,
                ids = created.Select(o => o.Id).ToList(),
                imported = report.Imported.Select(t => t.Get(lang)).ToList(),
                skipped = report.Skipped.Select(k => new { line = k.Line, code = k.Code, reason = k.Reason.Get(lang) }).ToList(),
                message = message.Get(lang),
            };
        }

        // ------------------------------------------------------------------ variants

        /// <summary>
        /// Variants: the elements added, removed and changed from the state in <c>payload</c> (a compact model, e.g. the
        /// previous slide's) to the session's page (<see cref="Variants.Diff"/>), with their page bounds.
        /// </summary>
        private object DiffCore(EngineRequest r)
        {
            var s = S(r);
            var before = DocumentJson.FromCompact(r.Payload ?? throw new ArgumentException("Missing 'payload'."));
            var d = Variants.Diff(before, s.Doc);
            object Box(SketchDocument doc, string id) { var b = Variants.Bounds(doc, doc.Get(id)!); return new { id, x = b.X, y = b.Y, w = b.W, h = b.H }; }
            return new
            {
                added = d.Added.Select(id => Box(s.Doc, id)).ToList(),
                changed = d.Changed.Select(id => Box(s.Doc, id)).ToList(),
                removed = d.Removed.Select(id => Box(before, id)).ToList(),
            };
        }

        // ------------------------------------------------------------------ compatibility check (drawing rules)

        /// <summary>The drawing-rule profiles (offices) for <c>preflight</c>: id and name in the session language.</summary>
        private object PatentProfilesCore(EngineRequest r)
        {
            var lang = r.Lang is null ? (r.Session is null ? Lang.En : S(r).Lang) : Language.Detect(r.Lang);
            return InventaSketch.Modules.Patent.PatentProfiles.All.Select(p => new { id = p.Id, name = p.Name.Get(lang) }).ToList();
        }

        /// <summary>
        /// Compatibility check (preflight) of the session's page against profile <c>text</c> (an id of <c>patentProfiles</c>,
        /// default "common-safe"): the findings in page and rule order with severity, element, message, suggestion and the
        /// cited source, and the disclaimer – a drafting aid, not a legal certification.
        /// </summary>
        private object PreflightCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var id = string.IsNullOrWhiteSpace(r.Text) ? "common-safe" : r.Text!.Trim();
            var profile = InventaSketch.Modules.Patent.PatentProfiles.All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unknown office profile '" + id + "'.");
            var findings = InventaSketch.Modules.Patent.Preflight.Run(Registry, s.Doc, profile);
            return new
            {
                profile = profile.Id,
                disclaimer = InventaSketch.Modules.Patent.Preflight.Disclaimer.Get(lang),
                findings = findings.Select(f => new
                {
                    rule = f.RuleId,
                    severity = f.Severity.ToString().ToLowerInvariant(),
                    id = f.ObjectId,
                    page = f.PageId,
                    message = f.Message.Get(lang),
                    suggestion = f.Suggestion?.Get(lang),
                    source = f.Source,
                }).ToList(),
            };
        }

        /// <summary>
        /// Clean slides for sharing (<see cref="Sanitizer"/>): the elements <c>ids</c> (groups expanded; none = every element
        /// of the current page) with their values made unrecognisable. <c>mode</c>: "normalised" (default, same proportions
        /// on a neutral scale), "randomised" (plausible random values with the same look) or "none" (only the text and
        /// metadata options); <c>value</c>: an object {renameCategories, replaceTexts, stripMeta, seed, spread}. One undo step.
        /// Returns {state, report}: the editor state and the counts per kind, the seed used (pass it again to reproduce the
        /// result) and the report lines in the request or session language.
        /// </summary>
        private object SanitizeCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var mode = string.IsNullOrWhiteSpace(r.Mode) ? SanitizeMode.Normalised
                : SanitizeOptions.ParseMode(r.Mode) ?? throw new ArgumentException("Unknown mode '" + r.Mode + "' (normalised, randomised, none).");
            var opt = new SanitizeOptions { Mode = mode, Lang = lang };
            if (r.Value is { ValueKind: JsonValueKind.Object } v)
            {
                bool Flag(string name) => v.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
                int? seed = v.TryGetProperty("seed", out var sd) && sd.ValueKind == JsonValueKind.Number && sd.TryGetInt32(out var n) ? n : null;
                double spread = v.TryGetProperty("spread", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetDouble() : opt.Spread;
                opt = opt with { RenameCategories = Flag("renameCategories"), ReplaceTexts = Flag("replaceTexts"), StripMeta = Flag("stripMeta"), Seed = seed, Spread = spread };
            }
            // A fixed seed, so the report names the one that was used.
            opt = opt with { Seed = opt.Seed ?? new Random().Next(1, int.MaxValue) };
            var ids = r.Ids is { Count: > 0 } ? s.Resolve(r.Ids) : s.Page.Order.Where(s.Doc.Objects.ContainsKey).ToList();
            SanitizeReport? report = null;
            s.Apply("Clean slide", d =>
            {
                var (doc, rep) = Sanitizer.Apply(Registry, d, ids, opt);
                report = rep;
                return rep.Any ? doc : d;
            });
            report ??= Sanitizer.Apply(Registry, s.Doc, Array.Empty<string>(), opt).Report;
            return new
            {
                state = StateOf(s, r),
                report = new
                {
                    mode = report.Mode switch { SanitizeMode.Normalised => "normalised", SanitizeMode.Randomised => "randomised", _ => "none" },
                    seed = report.Seed,
                    elements = report.Elements,
                    changed = report.Changed,
                    values = report.Values,
                    texts = report.Texts,
                    metadata = report.Metadata,
                    references = report.References,
                    unknown = report.Unknown,
                    failed = report.Failed,
                    byType = report.ByType.Select(t => new { type = t.TypeId, name = t.Name.Get(lang), count = t.Count }).ToList(),
                    summary = report.Summary.Get(lang),
                    lines = report.Lines.Select(l => l.Get(lang)).ToList(),
                },
            };
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

        /// <summary>
        /// Switches the document's black-and-white setting (<see cref="DocumentSettings.Monochrome"/>, <c>value</c> true or
        /// false); one undo step. Page SVG, exports and draw plans then map every colour to black or white; the objects keep
        /// their colours, so switching it off restores them. Every object counts as changed (hosts redraw all units).
        /// </summary>
        private object SetMonochromeCore(EngineRequest r)
        {
            var s = S(r);
            var value = r.Value is { ValueKind: JsonValueKind.True };
            if (s.Doc.Settings.Monochrome != value)
                s.Apply("Black and white", d => d with { Settings = d.Settings with { Monochrome = value } });
            return StateOf(s, r);
        }

        /// <summary>
        /// setValueLevel: the value level of the drawing (<see cref="DocumentSettings.ValueLevel"/>, <c>text</c> = "quick",
        /// "explore" or "engineer"; empty = quick sketch), docs/en/design/engineering-calculations.md section 11. One undo step
        /// when it changes; nothing is redrawn (the level only changes what the hosts offer: the Values button, formula lines).
        /// </summary>
        private object SetValueLevelCore(EngineRequest r)
        {
            var s = S(r);
            var level = (r.Text ?? "").Trim().ToLowerInvariant() switch
            {
                "" or "quick" or "quicksketch" => Core.ValueLevel.QuickSketch,
                "explore" => Core.ValueLevel.Explore,
                "engineer" => Core.ValueLevel.Engineer,
                _ => throw new ArgumentException("Unknown value level '" + r.Text + "' (quick, explore, engineer)."),
            };
            if (s.Doc.Settings.EffectiveValueLevel != level)
                s.Apply("Value level", d => d with { Settings = d.Settings.WithValueLevel(level) });
            return StateOf(s, r);
        }

        /// <summary>The API id of a value level ("quick", "explore", "engineer").</summary>
        private static string ValueLevelId(ValueLevel level) => level switch
        {
            Core.ValueLevel.Explore => "explore",
            Core.ValueLevel.Engineer => "engineer",
            _ => "quick",
        };

        /// <summary>
        /// Sets the ISO 128 line group of the drawing (<see cref="DocumentSettings.LineGroup"/>, <c>value</c> = width of the
        /// thick line in mm, one of <see cref="Style.LineGroups"/>; 0.5 or absent = the design group); one undo step. Every
        /// stroke scales with it; hosts redraw all units.
        /// </summary>
        private object SetLineGroupCore(EngineRequest r)
        {
            var s = S(r);
            double? group = r.Value is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : null;
            if (group is double g && (g <= 0 || Math.Abs(g - Style.DesignLineGroup) < 1e-9)) group = null;
            if (group is double h && !Style.LineGroups.Any(x => Math.Abs(x - h) < 1e-9)) throw new ArgumentException("Unknown line group " + h.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            if (s.Doc.Settings.LineGroup != group)
                s.Apply("Line group", d => d with { Settings = d.Settings with { LineGroup = group } });
            return StateOf(s, r);
        }

        /// <summary>
        /// Sets the accent colour of the drawing (<see cref="DocumentSettings.Accent"/>, <c>text</c> = #rrggbb or a preset id
        /// of <c>accents</c>; empty or absent = each module's own colours); one undo step, every object counts as changed.
        /// </summary>
        private object SetAccentCore(EngineRequest r)
        {
            var s = S(r);
            var text = (r.Text ?? "").Trim();
            var preset = Accents.Presets.FirstOrDefault(p => string.Equals(p.Id, text, StringComparison.OrdinalIgnoreCase));
            var accent = text.Length == 0 ? null : preset.Id is not null ? preset.Color : Accents.Normalize(text) ?? throw new ArgumentException("Unknown accent colour '" + text + "'.");
            if (s.Doc.Settings.Accent != accent)
                s.Apply("Accent colour", d => d with { Settings = d.Settings with { Accent = accent } });
            return StateOf(s, r);
        }

        /// <summary>The offered accent colours: id, colour, name in the session language.</summary>
        private object AccentsCore(EngineRequest r)
        {
            var lang = r.Lang is null ? (r.Session is null ? Lang.En : S(r).Lang) : Language.Detect(r.Lang);
            return Accents.Presets.Select(p => new { id = p.Id, color = p.Color, name = p.Name.Get(lang) }).ToList();
        }

        /// <summary>Sets the main text (<see cref="MainTextOf"/>) of object <c>id</c> (default: the first selected object that has one); one undo step.</summary>
        private object SetMainTextCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Resolve(r.Ids).FirstOrDefault(x => s.Doc.Get(x) is { } o && MainTextOf(Registry, o, s.Lang) is not null);
            var target = id is null ? null : s.Doc.Get(id);
            if (target is null || MainTextOf(Registry, target, s.Lang) is null) return StateOf(s, r);
            var text = r.Text ?? "";
            s.Apply("Text", d => Edits.Update(d, new[] { target.Id }, o => WithMainText(Registry, o, text)));
            return StateOf(s, r);
        }

        /// <summary>
        /// Library items that fit the first selected object (<see cref="Registry.Suggestions"/>: companions, same category,
        /// annotations), in its kind of view; at most <c>max</c> (default 9). Empty without a known selected object.
        /// </summary>
        private object SuggestionsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var first = s.Resolve(r.Ids).Select(s.Doc.Get).FirstOrDefault(o => o is not null && Registry.Type(o.Type) is not null);
            if (first is null) return new List<SuggestionInfo>();
            return Registry.Suggestions(first, Math.Max(1, r.Max ?? 9)).Select(i => new SuggestionInfo
            {
                Id = i.Id, TypeId = i.TypeId, Name = i.Name.Get(lang), View = i.View, Category = i.Category,
                Points = Registry.Type(i.TypeId)?.Geometry == GeometryKind.Points,
            }).ToList();
        }

        /// <summary>
        /// "+" quick add: a copy of <c>id</c> (or <c>itemId</c>) on <c>side</c>, at <c>x</c>/<c>y</c> if given, joined by the
        /// type's connector (<see cref="Flow.QuickAdd"/>). One undo step; the new element becomes the selection.
        /// </summary>
        private object QuickAddCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var side = (r.Side ?? "right").ToLowerInvariant() switch
            {
                "left" => FlowSide.Left, "top" => FlowSide.Top, "bottom" => FlowSide.Bottom, _ => FlowSide.Right,
            };
            Vec2? at = r.X is double x && r.Y is double y ? new Vec2(x, y) : null;
            string? added = null;
            s.Apply("Add next", d =>
            {
                var res = Flow.QuickAdd(Registry, d, id, side, at, r.ItemId);
                added = res.ObjectId;
                return res.Doc;
            });
            if (added is not null) s.Select(new[] { added });
            return StateOf(s, r);
        }

        /// <summary>Objects that move rigidly along with <c>ids</c> (gears on a dragged shaft): hosts preview them during a drag.</summary>
        private object FollowersCore(EngineRequest r)
        {
            var s = S(r);
            return Coupling.RigidFollowers(Registry, s.Doc, s.Resolve(r.Ids), s.Lang).ToList();
        }

        /// <summary>
        /// Insert by typing: ranked library hits for <c>text</c> (<see cref="LibrarySearch"/>); with a selection (<c>ids</c>)
        /// the parts that fit it come first and are marked <c>fits</c>; <c>recent</c> ranks recently used items higher.
        /// </summary>
        private object SearchCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var first = s.Resolve(r.Ids).Select(s.Doc.Get).FirstOrDefault(o => o is not null && Registry.Type(o.Type) is not null);
            var fitting = first is null ? new List<LibraryItem>() : Registry.Suggestions(first, 12).ToList();
            var view = first is null ? null : Registry.Type(first.Type)?.ViewOf(first);
            var hits = LibrarySearch.Find(Registry, r.Text, lang, r.Recent ?? new List<string>(), view, Math.Max(1, r.Max ?? 30), fitting);
            return hits.Select(i => new SuggestionInfo
            {
                Id = i.Id, TypeId = i.TypeId, Name = i.Name.Get(lang), View = i.View, Category = i.Category,
                Points = Registry.Type(i.TypeId)?.Geometry == GeometryKind.Points, Fits = fitting.Any(f => f.Id == i.Id),
            }).ToList();
        }

        /// <summary>Which of mirror horizontally/vertically and rotate by 90° change the selection (<see cref="Symmetry"/>).</summary>
        private object TransformEffectCore(EngineRequest r)
        {
            var s = S(r);
            var ctx = s.Context();
            var prims = s.Resolve(r.Ids).Select(s.Doc.Get).Where(o => o is not null && Registry.Type(o.Type) is not null)
                .SelectMany(o => Registry.Render(o!, ctx)).ToList();
            var fx = Symmetry.Of(prims);
            return new TransformEffectInfo { MirrorH = fx.MirrorH, MirrorV = fx.MirrorV, Rotate90 = fx.Rotate90 };
        }

        /// <summary>Box objects of the selection grouped per top-level element (group or single object), first object each.</summary>
        private List<SketchObject> ArrangeUnits(EditorSession s, EngineRequest r)
            => s.Resolve(r.Ids).Select(s.Doc.Get).Where(o => o is not null && Registry.Type(o.Type) is not null).Select(o => o!).ToList();

        /// <summary>The selection per top-level element: a group (ghost positions, a grouped assembly) moves as one.</summary>
        private static List<(SketchObject Lead, List<string> Members, Rect Box)> ArrangeGroups(SketchDocument doc, IReadOnlyList<SketchObject> objs)
            => objs.GroupBy(o => o.GroupId ?? o.Id, StringComparer.Ordinal).Select(g =>
            {
                var boxes = g.Select(o => o.IsPointGeometry ? Rect.FromPoints(doc.ResolvePoints(o)) : o.Bounds()).ToList();
                double x0 = boxes.Min(b => b.X), y0 = boxes.Min(b => b.Y), x1 = boxes.Max(b => b.X + b.W), y1 = boxes.Max(b => b.Y + b.H);
                return (g.First(), g.Select(o => o.Id).ToList(), new Rect(x0, y0, x1 - x0, y1 - y0));
            }).ToList();

        /// <summary>
        /// Selection-wide operations that change something for the selection (<see cref="Arrange"/>): one view for all
        /// ("mview:&lt;view&gt;"), centres on a row/column ("align:row", "align:column"), even spacing ("dist:h", "dist:v"),
        /// "like the first" ("match"), renumber reference numerals ("renumber"), a layered diagram layout of connected
        /// elements ("layout:down", "layout:right", <see cref="DiagramLayout"/>).
        /// </summary>
        private object ArrangeOptionsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var de = lang == Lang.De;
            var objs = ArrangeUnits(s, r);
            var list = new List<ArrangeOption>();
            if (objs.Count < 2) return list;
            foreach (var v in Arrange.CommonViews(Registry, objs))
                list.Add(new ArrangeOption { Id = "mview:" + v.Id, Label = (de ? "Alle in Ansicht: " : "All in view: ") + v.Label.Get(lang), Icon = IconIds.ForView(v) });
            var groups = ArrangeGroups(s.Doc, objs);
            var boxes = groups.Select(g => g.Box).ToList();
            void Opt(string id, string en, string german, string icon) => list.Add(new ArrangeOption { Id = id, Label = de ? german : en, Icon = icon });
            if (Arrange.Changes(Arrange.AlignCentres(boxes, true))) Opt("align:row", "Centres on one horizontal line", "Mitten auf eine waagrechte Linie", IconIds.AlignRow);
            if (Arrange.Changes(Arrange.AlignCentres(boxes, false))) Opt("align:column", "Centres on one vertical line", "Mitten auf eine senkrechte Linie", IconIds.AlignColumn);
            if (Arrange.Changes(Arrange.Distribute(boxes, true))) Opt("dist:h", "Space evenly left to right", "Gleichmäßig von links nach rechts verteilen", IconIds.DistributeH);
            if (Arrange.Changes(Arrange.Distribute(boxes, false))) Opt("dist:v", "Space evenly top to bottom", "Gleichmäßig von oben nach unten verteilen", IconIds.DistributeV);
            if (Arrange.MatchFormat(Registry, objs[0], objs.Skip(1), s.Context()).Count > 0) Opt("match", "Like the first: view, parameters and style", "Wie das erste: Ansicht, Parameter und Stil", IconIds.MatchFormat);
            if (groups.Count == 2 && groups.All(g => !g.Lead.IsPointGeometry) && !HasDimensionBetween(s.Doc, groups[0].Lead.Id, groups[1].Lead.Id)) Opt("dim", "Dimension between the two (centre distance)", "Maß zwischen beiden (Achsabstand)", IconIds.Dimension);
            // Connected diagrams (flowchart, UML, BPMN, ER): a layered layout, top to bottom or left to right.
            foreach (var (id, down, en, german) in new[] { ("layout:down", true, "Lay out as a diagram, top to bottom", "Als Diagramm anordnen, von oben nach unten"), ("layout:right", false, "Lay out as a diagram, left to right", "Als Diagramm anordnen, von links nach rechts") })
                if (DiagramLayout.Layered(s.Doc, objs, down) is { } target && Arrange.Changes(objs.Where(o => target.ContainsKey(o.Id)).Select(o => target[o.Id] - new Vec2(o.Transform.X, o.Transform.Y))))
                    Opt(id, en, german, down ? IconIds.DistributeV : IconIds.DistributeH);
            if (Arrange.Renumber(Numerals(s, objs)).Count > 0) Opt("renumber", "Renumber reference numerals in reading order", "Bezugszeichen in Lesereihenfolge neu nummerieren", IconIds.Renumber);
            return list;
        }

        /// <summary>True if a dimension already docks on both objects.</summary>
        private static bool HasDimensionBetween(SketchDocument doc, string a, string b)
            => doc.Objects.Values.Any(o => o.Type.StartsWith("ann.dimension", StringComparison.Ordinal) && o.Attach is { Count: >= 2 } at
                && at.Any(x => x?.ObjectId == a) && at.Any(x => x?.ObjectId == b));

        private static List<(string Id, Vec2 At, string Numeral)> Numerals(EditorSession s, IEnumerable<SketchObject> objs)
            => objs.Where(o => o.Type == "patent.numeral" && o.Points is { Count: >= 2 })
                .Select(o => (o.Id, s.Doc.ResolvePoints(o)[1], !string.IsNullOrEmpty(o.Ref) ? o.Ref! : !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("numeral", "10")))
                .ToList();

        /// <summary>Runs a selection-wide operation <c>action</c> (see <c>arrangeOptions</c>); one undo step.</summary>
        private object ArrangeCore(EngineRequest r)
        {
            var action = r.Action ?? throw new ArgumentException("Missing 'action'.");
            return Edit(r, "Arrange", (s, ids) =>
            {
                var objs = ArrangeUnits(s, r);
                if (objs.Count < 2) return s.Doc;
                var doc = s.Doc;
                if (action.StartsWith("mview:", StringComparison.Ordinal))
                    return AssemblyViews.Switch(Registry, doc, objs.Select(o => o.Id).ToList(), action.Substring(6), s.Lang);
                if (action == "match")
                {
                    var changed = Arrange.MatchFormat(Registry, objs[0], objs.Skip(1), s.Context());
                    return Edits.Update(doc, changed.Select(o => o.Id).ToList(), o => changed.First(x => x.Id == o.Id));
                }
                if (action.StartsWith("layout:", StringComparison.Ordinal))
                {
                    // Docked connectors follow their elements (the session refreshes attachments after the edit).
                    var down = action == "layout:down";
                    if (DiagramLayout.Layered(doc, objs, down) is not { } target) return doc;
                    foreach (var o in objs.Where(o => target.ContainsKey(o.Id)))
                    {
                        var d = target[o.Id] - new Vec2(o.Transform.X, o.Transform.Y);
                        if (d.Length > Arrange.Tolerance) doc = Edits.Move(doc, new[] { o.Id }, d);
                    }
                    // Connectors dock on the facing sides (bottom → top, right → left).
                    var nodes = new HashSet<string>(target.Keys, StringComparer.Ordinal);
                    foreach (var line in doc.Objects.Values.Where(o => o.IsPointGeometry).ToList())
                        if (DiagramLayout.Redock(Registry, doc, line, nodes, down) is { } redocked && !ReferenceEquals(redocked, line)) doc = Edits.Replace(doc, redocked);
                    return doc;
                }
                var groups = ArrangeGroups(doc, objs);
                if (action == "dim")
                {
                    if (groups.Count != 2 || Registry.Item("ann.dimension.aligned") is not { } dimItem) return doc;
                    var ctx = s.Context();
                    var o1 = groups[0].Lead; var o2 = groups[1].Lead;
                    var m1 = Arrange.MeasurePoint(Registry, o1, ctx);
                    var m2 = Arrange.MeasurePoint(Registry, o2, ctx);
                    var dim = Registry.CreateBetween(dimItem, m1.P, m2.P) with { Attach = new[] { Attachments.For(o1, m1.P, m1), Attachments.For(o2, m2.P, m2) } };
                    return Edits.Add(doc, s.PageId, dim);
                }
                if (action == "renumber")
                {
                    var map = Arrange.Renumber(Numerals(s, objs));
                    return Edits.Update(doc, map.Keys.ToList(), o =>
                        !string.IsNullOrEmpty(o.Ref) ? o with { Ref = map[o.Id] } : !string.IsNullOrEmpty(o.Text) ? o with { Text = map[o.Id] } : o.WithParam("numeral", map[o.Id]));
                }
                var boxes = groups.Select(g => g.Box).ToList();
                var moves = action switch
                {
                    "align:row" => Arrange.AlignCentres(boxes, true),
                    "align:column" => Arrange.AlignCentres(boxes, false),
                    "dist:h" => Arrange.Distribute(boxes, true),
                    "dist:v" => Arrange.Distribute(boxes, false),
                    _ => throw new ArgumentException("Unknown arrange action: " + action),
                };
                for (var i = 0; i < groups.Count; i++)
                    if (moves[i].Length > Arrange.Tolerance) doc = Edits.Move(doc, groups[i].Members, moves[i]);
                return doc;
            });
        }

        /// <summary>The objects coupled with the selection, directly or through others (gear train, drive), selection first.</summary>
        private object AssemblyCore(EngineRequest r)
        {
            // What moves along (Connectivity.Coupled), the same rule as the COM add-in's "Select the assembly".
            var s = S(r);
            var seen = new List<string>();
            foreach (var id in s.Resolve(r.Ids))
                foreach (var c in Connectivity.Connected(Registry, s.Doc, id, ConnectKind.Coupled, new RenderContext(s.Doc, s.Lang)))
                    if (!seen.Contains(c)) seen.Add(c);
            return seen;
        }

        /// <summary>
        /// Select connected (<see cref="Connectivity"/>): the elements connected to <c>ids</c> by <c>mode</c> ("auto",
        /// "net", "coupled", "chain", "loop", "walls"), the given elements first. Returns the ids.
        /// </summary>
        private object ConnectedCore(EngineRequest r)
        {
            var s = S(r);
            var kind = (r.Mode ?? "auto").Trim().ToLowerInvariant() switch
            {
                "net" => ConnectKind.Net, "coupled" => ConnectKind.Coupled, "chain" => ConnectKind.Chain,
                "loop" => ConnectKind.Loop, "walls" => ConnectKind.Walls, "auto" or "" => ConnectKind.Auto,
                _ => throw new ArgumentException("Unknown mode '" + r.Mode + "' (auto, net, coupled, chain, loop, walls)."),
            };
            var ctx = new RenderContext(s.Doc, s.Lang);
            var seen = new List<string>();
            foreach (var id in s.Resolve(r.Ids))
                foreach (var c in Connectivity.Connected(Registry, s.Doc, id, kind, ctx))
                    if (!seen.Contains(c)) seen.Add(c);
            return seen;
        }

        /// <summary>The kinds of "select connected" that add something for the first of <c>ids</c>: {kind, label, hint, ids}.</summary>
        private object ConnectOptionsCore(EngineRequest r)
        {
            var s = S(r);
            var id = s.Resolve(r.Ids).FirstOrDefault() ?? throw new ArgumentException("Missing 'ids'.");
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            return Connectivity.Options(Registry, s.Doc, id, new RenderContext(s.Doc, lang))
                .Select(o => new { kind = o.Kind.ToString().ToLowerInvariant(), label = Connectivity.Label(o.Kind).Get(lang), hint = Connectivity.Hint(o.Kind).Get(lang), ids = o.Ids })
                .ToList();
        }

        /// <summary>The path of point <c>index</c> (default: last) of link <c>id</c> while its chain's crank turns (<see cref="Linkage.Trace"/>).</summary>
        private object TraceCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id) ?? throw new ArgumentException("Unknown object: " + id);
            var index = r.Index ?? (o.Points?.Count ?? 1) - 1;
            var path = Linkage.Trace(Registry, s.Doc, id, index, Math.Max(8, r.Max ?? 72), s.Lang);
            return path is { } p ? new TraceInfo { Points = p.Points.ToList(), Closed = p.Closed } : new TraceInfo();
        }

        /// <summary>
        /// Inserts library item <c>itemId</c> where it fits the selected object <c>id</c> (gear on the shaft, gear in mesh,
        /// bearing beside a gear: <see cref="Fitting.Place"/>), coupled; returns the state with <c>placed</c> = false when it
        /// fits nowhere (the host then lets the user place it).
        /// </summary>
        private object InsertFittingCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Resolve(r.Ids).FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var item = Registry.Item(r.ItemId ?? "") ?? throw new KeyNotFoundException($"Unknown library item '{r.ItemId}'.");
            var placed = Fitting.Place(Registry, s.Doc, id, item, s.Lang);
            if (placed is null) return new { placed = false, state = StateOf(s, r) };
            s.Apply("Insert " + item.Name.En, d => Edits.Add(d, s.PageId, placed));
            s.Select(new[] { placed.Id });
            return new { placed = true, state = StateOf(s, r) };
        }

        /// <summary>Inserts the coupler curve of link <c>id</c> (point <c>index</c>, default last) as a motion path; one undo step.</summary>
        private object DrawTraceCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id) ?? throw new ArgumentException("Unknown object: " + id);
            var path = Linkage.Trace(Registry, s.Doc, id, r.Index ?? (o.Points?.Count ?? 1) - 1, Math.Max(8, r.Max ?? 72), s.Lang);
            if (path is not { } p || p.Points.Count < 3) return StateOf(s, r);
            var item = Registry.Item(p.Closed ? "kin.path.closed" : "kin.path");
            if (item is null) return StateOf(s, r);
            var curve = Registry.Create(item, p.Points[0]) with { Points = p.Points.Select(q => new Vec2(Math.Round(q.X, 4), Math.Round(q.Y, 4))).ToList() };
            if (!p.Closed) curve = curve.WithParam("arrow", false);
            s.Apply("Coupler curve", d => Edits.Add(d, s.PageId, curve));
            s.Select(new[] { curve.Id });
            return StateOf(s, r);
        }

        /// <summary>Explodes assembly <c>id</c> into its parts (<see cref="Assemblies.Explode"/>), replacing it; one undo step.</summary>
        private object ExplodeCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id);
            if (o is null || !Assemblies.CanExplode(Registry, o)) return StateOf(s, r);
            var parts = Assemblies.Explode(Registry, s.Doc, id, s.Lang);
            if (parts.Count == 0) return StateOf(s, r);
            s.Apply("Explode", d => Edits.Add(Edits.Remove(d, new[] { id }), s.PageId, parts.ToArray()));
            s.Select(parts.Select(x => x.Id).ToList());
            return StateOf(s, r);
        }

        /// <summary>
        /// Degree of freedom of the self-built linkage of <c>id</c> (<see cref="Linkage.Dof"/>) and whether <c>id</c> can be
        /// exploded (<see cref="Assemblies.CanExplode"/>): {links, joints, dof} or nulls, plus <c>explodable</c>.
        /// </summary>
        private object DofCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id);
            var dof = o is null ? null : Linkage.Dof(Registry, s.Doc, id, s.Lang);
            return new
            {
                links = dof?.Links,
                joints = dof?.Joints,
                dof = dof?.Dof,
                explodable = o is not null && Assemblies.CanExplode(Registry, o),
            };
        }

        /// <summary>
        /// Ghost positions of the self-built linkage of link <c>id</c>: <c>max</c> (default 4) dashed grey copies of the chain's
        /// links spread over the crank's range, grouped; one undo step (<see cref="Linkage.Poses"/>).
        /// </summary>
        private object DrawGhostsCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var poses = Linkage.Poses(Registry, s.Doc, id, Math.Max(1, r.Max ?? 4), s.Lang);
            if (poses.Count == 0) return StateOf(s, r);
            var style = new Style { Stroke = "#8C8C8C", Dash = DashStyle.Dash, StrokeWidth = 0.18 };
            var ghosts = new List<SketchObject>();
            foreach (var pose in poses)
                foreach (var kv in pose)
                    if (s.Doc.Get(kv.Key) is { } src)
                        ghosts.Add(src with
                        {
                            Id = Ids.New(), Points = kv.Value.Select(q => new Vec2(Math.Round(q.X, 4), Math.Round(q.Y, 4))).ToList(),
                            Attach = null, Released = null, GroupId = null, Style = style,
                        });
            s.Apply("Ghost positions", d => Edits.Group(Edits.Add(d, s.PageId, ghosts.ToArray()), ghosts.Select(g => g.Id).ToList()));
            s.Select(ghosts.Select(g => g.Id).ToList());
            return StateOf(s, r);
        }

        /// <summary>An aligned dimension between the axes of the two objects <c>ids</c>, docked on both; one undo step.</summary>
        private object DimensionBetweenCore(EngineRequest r)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            if (ids.Count != 2 || s.Doc.Get(ids[0]) is not { } o1 || s.Doc.Get(ids[1]) is not { } o2 || Registry.Item("ann.dimension.aligned") is not { } item)
                return StateOf(s, r);
            var ctx = s.Context();
            var a1 = Arrange.MeasurePoint(Registry, o1, ctx);
            var a2 = Arrange.MeasurePoint(Registry, o2, ctx);
            var dim = Registry.CreateBetween(item, a1.P, a2.P);
            dim = dim with { Attach = new[] { Attachments.For(o1, a1.P, a1), Attachments.For(o2, a2.P, a2) } };
            s.Apply("Dimension", d => Edits.Add(d, s.PageId, dim));
            s.Select(new[] { dim.Id });
            return StateOf(s, r);
        }

        /// <summary>
        /// Live coupling preview: the neighbours the coupling would move or scale if <c>ids</c> moved by <c>dx</c>/<c>dy</c>
        /// (or scaled by <c>sx</c>/<c>sy</c> about <c>pivotX</c>/<c>pivotY</c>), as one SVG fragment in page millimetres
        /// (<c>svg</c>, empty when nothing follows) plus their ids. Nothing is changed.
        /// </summary>
        private object CouplingPreviewCore(EngineRequest r)
        {
            var s = S(r);
            var ids = s.Resolve(r.Ids);
            if (ids.Count == 0) return new { ids = Array.Empty<string>(), svg = "" };
            var after = r.Degrees is double deg
                ? Edits.Rotate(s.Doc, ids, new Vec2(r.PivotX ?? 0, r.PivotY ?? 0), deg)
                : r.Sx is not null || r.Sy is not null
                    ? Edits.Scale(s.Doc, ids, new Vec2(r.PivotX ?? 0, r.PivotY ?? 0), r.Sx ?? 1, r.Sy ?? 1)
                    : Edits.Move(s.Doc, ids, new Vec2(r.Dx ?? 0, r.Dy ?? 0));
            var result = Coupling.Propagate(Registry, s.Doc, after, s.Lang);
            var own = new HashSet<string>(ids, StringComparer.Ordinal);
            var changed = result.Objects.Values.Where(o => !own.Contains(o.Id) && after.Get(o.Id) is { } x
                && (x.Transform != o.Transform || x.Size != o.Size) && Registry.Type(o.Type) is not null).ToList();
            if (changed.Count == 0) return new { ids = Array.Empty<string>(), svg = "" };
            var ctx = new RenderContext(result, s.Lang);
            var prims = changed.SelectMany(o => Registry.Render(o, ctx)).ToList();
            return new { ids = changed.Select(o => o.Id).ToList(), svg = SvgRenderer.RenderPrimitives(prims) };
        }

        /// <summary>Build "+" ports of object <c>id</c> (<see cref="Building.Ports"/>): id, kind, position and outward direction.</summary>
        private object BuildPortsCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id);
            return Building.Ports(Registry, s.Doc, id, s.Lang).Select(p => new
            {
                id = p.Id, kind = p.Kind.ToString(), x = p.At.X, y = p.At.Y, dx = p.Dir.X, dy = p.Dir.Y,
                stretch = o is not null && Building.CanStretch(Registry, s.Doc, o, p, s.Lang),
                medium = p.Medium,
            }).ToList();
        }

        private BuildPort Port(EditorSession s, string id, string? portId)
            => Building.Ports(Registry, s.Doc, id, s.Lang).FirstOrDefault(p => p.Id == portId) ?? throw new ArgumentException("Unknown port: " + portId);

        /// <summary>Library items that dock at port <c>key</c> of object <c>id</c>, best first.</summary>
        private object BuildItemsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            return Building.Items(Registry, s.Doc, id, Port(s, id, r.Key), Math.Max(1, r.Max ?? 12), s.Lang).Select(i => new SuggestionInfo
            {
                Id = i.Id, TypeId = i.TypeId, Name = i.Name.Get(lang), View = i.View, Category = i.Category,
                Points = Registry.Type(i.TypeId)?.Geometry == GeometryKind.Points, Fits = true,
            }).ToList();
        }

        /// <summary>
        /// Docks library item <c>itemId</c> at port <c>key</c> of object <c>id</c>, coupled (<see cref="Building.Apply"/>):
        /// at a connection port the connector (or the next element joined by it), at a line end the line continued (or the
        /// element docked on the end); one undo step. The built part (the connector, the element, the longer line) is selected.
        /// </summary>
        private object BuildPlaceCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var item = Registry.Item(r.ItemId ?? "") ?? throw new KeyNotFoundException($"Unknown library item '{r.ItemId}'.");
            var port = Port(s, id, r.Key);
            var built = Building.Apply(Registry, s.Doc, id, port, item, s.Lang);
            if (built is null) return StateOf(s, r);
            s.Apply("Build " + item.Name.En, _ => built.Doc);
            s.Select(new[] { built.SelectId });
            return StateOf(s, r);
        }

        /// <summary>Stretches the shaft end at port <c>key</c> of object <c>id</c> to <c>x</c>/<c>y</c>; one undo step.</summary>
        private object BuildStretchCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var port = Port(s, id, r.Key);
            s.Apply("Stretch", d => Building.StretchEnd(Registry, d, id, port, new Vec2(r.X ?? port.At.X, r.Y ?? port.At.Y), s.Lang));
            return StateOf(s, r);
        }

        /// <summary>SVG (page mm) of object <c>id</c> with the shaft end at port <c>key</c> stretched to <c>x</c>/<c>y</c>; nothing changes.</summary>
        private object BuildStretchPreviewCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var port = Port(s, id, r.Key);
            var doc = Building.StretchEnd(Registry, s.Doc, id, port, new Vec2(r.X ?? port.At.X, r.Y ?? port.At.Y), s.Lang);
            var o = doc.Get(id);
            if (o is null) return new { svg = "" };
            return new { svg = SvgRenderer.RenderPrimitives(Registry.Render(o, new RenderContext(doc, s.Lang))) };
        }

        /// <summary>
        /// Motion mode: whether object <c>id</c> can move as a mechanism – <c>kind</c> "gear" (a part of a gear train that
        /// turns about its centre: <c>x</c>/<c>y</c>), "linkage" (a link of a self-built chain with F ≥ 1) or "none".
        /// </summary>
        private object MotionInfoCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id);
            if (o is null) return new { kind = "none" };
            if (o.IsPointGeometry && Linkage.IsPart(Registry, o) && Linkage.Dof(Registry, s.Doc, id, s.Lang) is { Dof: >= 1 } d)
                return new { kind = "linkage", dof = d.Dof };
            if (!o.IsPointGeometry && Registry.Type(o.Type) is { } mt && mt.Parameters.Any(p => p.Key == "angle") && mt.AnimationFrames(o, 2).Count > 1)
            {
                // A predefined mechanism: dragging turns its input crank about the mount (input pivot).
                var pivot = Registry.PageAnchors(o, s.Context()).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Mount)?.P
                    ?? new Vec2(o.Transform.X, o.Transform.Y);
                return new { kind = "mechanism", x = pivot.X, y = pivot.Y, coupled = true };
            }
            if (Cams.IsCam(Registry, o))
            {
                // A cam disc turns about its centre; its followers (sliders on it) move with it.
                return new { kind = "gear", x = o.Transform.X, y = o.Transform.Y, coupled = Cams.Followers(Registry, s.Doc, id, s.Lang).Count > 0 };
            }
            if (!o.IsPointGeometry && GearTrain.Spins(Registry, o))
            {
                var assembly = (List<string>)AssemblyCore(new EngineRequest { Session = r.Session, Ids = new List<string> { id } });
                return new { kind = "gear", x = o.Transform.X, y = o.Transform.Y, coupled = assembly.Count > 1 };
            }
            return new { kind = "none" };
        }

        /// <summary>A predefined mechanism whose input turns through its "angle" parameter.</summary>
        private bool IsMechanism(SketchObject o)
            => !o.IsPointGeometry && Registry.Type(o.Type) is { } t && t.Parameters.Any(p => p.Key == "angle") && t.AnimationFrames(o, 2).Count > 1;

        /// <summary>Motion mode: turns gear <c>id</c> by <c>degrees</c> with its whole train (ratios kept); one undo step.</summary>
        private object MotionTurnCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var deg = r.Degrees ?? 0;
            if (Math.Abs(deg) < 1e-9 || s.Doc.Get(id) is not { } o) return StateOf(s, r);
            if (IsMechanism(o))
                s.Apply("Turn", d => Edits.Update(d, new[] { id }, x => x.WithParam("angle", Math.Round(x.Num("angle", 0) + deg, 3))));
            else if (Cams.IsCam(Registry, o))
                s.Apply("Turn", d => Cams.Turn(Registry, d, id, deg, s.Lang));
            else
                s.Apply("Turn", d => GearTrain.Turn(Registry, d, id, deg, s.Lang));
            return StateOf(s, r);
        }

        /// <summary>
        /// Motion mode: drags joint <c>index</c> of link <c>id</c> towards <c>x</c>/<c>y</c>; the self-built chain follows
        /// with constant link lengths (<see cref="Linkage.Drag"/>); one undo step.
        /// </summary>
        private object MotionDragCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            if (s.Doc.Get(id) is null) return StateOf(s, r);
            var index = r.Index ?? 1;
            // Continue from the last previewed position of this drag (keeps the assembly mode), else from the document.
            var start = s.Motion is { } m && ReferenceEquals(m.Base, s.Doc) && m.Key == id + "#" + index ? m.State : s.Doc;
            s.Motion = null;
            var solved = Linkage.Drag(Registry, start, id, index, new Vec2(r.X ?? 0, r.Y ?? 0), s.Lang);
            s.Apply("Move linkage", _ => solved);
            return StateOf(s, r);
        }

        /// <summary>
        /// Motion mode live preview: with <c>degrees</c> the gear train of <c>id</c> turned, else the chain of link <c>id</c>
        /// with joint <c>index</c> dragged to <c>x</c>/<c>y</c>; returns the moved objects as one SVG (page mm) and their
        /// ids. Nothing changes.
        /// </summary>
        private object MotionPreviewCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            if (s.Doc.Get(id) is not { } target) return new { ids = Array.Empty<string>(), svg = "" };
            SketchDocument after;
            if (r.Degrees is double deg)
                after = IsMechanism(target)
                    ? Edits.Update(s.Doc, new[] { id }, x => x.WithParam("angle", Math.Round(x.Num("angle", 0) + deg, 3)))
                    : Cams.IsCam(Registry, target)
                        ? Cams.Turn(Registry, s.Doc, id, deg, s.Lang)
                        : GearTrain.Turn(Registry, s.Doc, id, deg, s.Lang);
            else
            {
                // Linkage drag: solve on from the last step of the same drag, so the chain cannot flip its assembly mode.
                var key = id + "#" + (r.Index ?? 1);
                var start = s.Motion is { } m && ReferenceEquals(m.Base, s.Doc) && m.Key == key ? m.State : s.Doc;
                after = Linkage.Drag(Registry, start, id, r.Index ?? 1, new Vec2(r.X ?? 0, r.Y ?? 0), s.Lang);
                s.Motion = (s.Doc, after, key);
            }
            // Compare resolved points: a link that follows only through a docked joint moves too.
            bool Moved(SketchObject o) => s.Doc.Get(o.Id) is { } b && Registry.Type(o.Type) is not null
                && (b.Transform != o.Transform || b.Size != o.Size || !ReferenceEquals(b.Params, o.Params)
                    || o.IsPointGeometry && !s.Doc.ResolvePoints(b).SequenceEqual(after.ResolvePoints(o)));
            var moved = after.Objects.Values.Where(Moved).ToList();
            if (moved.Count == 0) return new { ids = Array.Empty<string>(), svg = "" };
            var ctx = new RenderContext(after, s.Lang);
            return new { ids = moved.Select(o => o.Id).ToList(), svg = SvgRenderer.RenderPrimitives(moved.SelectMany(o => Registry.Render(o, ctx)).ToList()) };
        }

        /// <summary>Library items offered as the next element after a quick add (same category, then companions).</summary>
        private object NextItemsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var o = s.Doc.Get(r.Id ?? s.Selection.FirstOrDefault() ?? "");
            if (o is null) return new List<SuggestionInfo>();
            return Flow.NextItems(Registry, o, Math.Max(1, r.Max ?? 8)).Select(i => new SuggestionInfo
            {
                Id = i.Id, TypeId = i.TypeId, Name = i.Name.Get(lang), View = i.View, Category = i.Category, Points = false,
            }).ToList();
        }

        /// <summary>Turns <c>id</c> into library item <c>itemId</c> in place; attached connectors stay connected.</summary>
        private object ReplaceItemCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Selection.FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            if (r.ItemId is null) throw new ArgumentException("Missing 'itemId'.");
            s.Apply("Replace", d => Flow.Replace(Registry, d, id, r.ItemId));
            s.Select(new[] { id });
            return StateOf(s, r);
        }

        private static readonly string[] MainTextKeys = { "text", "label", "name", "title", "ref" };

        /// <summary>
        /// "Type ▸": the line types the selected line, arrow, dimension or lead line can be converted into (empty for other
        /// objects); the current type is marked active.
        /// </summary>
        private object ConversionsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var first = s.Resolve(r.Ids).Select(s.Doc.Get).FirstOrDefault(o => o is not null);
            if (first is null || !Registry.IsConvertible(first)) return new List<ActionInfo>();
            return Registry.ConversionTargets.Where(id => Registry.Type(id) is { Geometry: GeometryKind.Points })
                .Select(id => new ActionInfo { Id = id, Label = Registry.Type(id)!.Name.Get(lang), Kind = "convert", Group = "type", Active = first.Type == id })
                .ToList();
        }

        /// <summary>Objects an export works on: the selection (with its groups), else every object of the page.</summary>
        private static IReadOnlyList<SketchObject> ExportObjects(EditorSession s, IList<string>? ids)
        {
            var chosen = ids is { Count: > 0 } ? s.Resolve(ids) : s.Doc.ObjectsOn(s.Page).Select(o => o.Id).ToList();
            var set = new HashSet<string>(s.Doc.ExpandGroups(chosen));
            return s.Doc.ObjectsOn(s.Page).Where(o => set.Contains(o.Id)).ToList();
        }

        /// <summary>Domain exports that apply to the selection (or the page): id, name, extension, mime type, what they keep.</summary>
        private object ExportersCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            return Exports.For(Registry, s.Doc, ExportObjects(s, r.Ids))
                .Select(e => new { id = e.Id, name = e.Name.Get(lang), extension = e.Extension, mime = e.MimeType, preserves = e.Preserves.Get(lang) })
                .ToList();
        }

        /// <summary>Runs the domain export <c>itemId</c> on the selection (or the page) and returns its text.</summary>
        private object ExportCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var exporter = Exports.All(Registry).FirstOrDefault(e => e.Id == r.ItemId) ?? throw new ArgumentException("Unknown exporter: " + r.ItemId);
            var text = exporter.Export(Registry, s.Doc, ExportObjects(s, r.Ids), lang);
            return new { id = exporter.Id, name = exporter.Name.Get(lang), extension = exporter.Extension, mime = exporter.MimeType, text };
        }

        /// <summary>Converts the selected line-like objects into the type <c>itemId</c> (end points, numeral, colour kept); one undo step.</summary>
        private object ConvertCore(EngineRequest r)
            => Edit(r, "Type", (s, ids) => Edits.Update(s.Doc, ids, o => Registry.IsConvertible(o) && r.ItemId is not null ? Registry.Convert(o, r.ItemId) : o));

        /// <summary>
        /// The main text of an object for quick editing (double-click, bar, pie): the numeral of reference numerals and lead
        /// lines (shown from <see cref="SketchObject.Ref"/>, object text or the "numeral" parameter), else the object text of
        /// text types, else the first text parameter among text, label, name and title. Null if the type has no such text.
        /// Same idea as the COM add-in's on-slide editor.
        /// </summary>
        public static TextEditInfo? MainTextOf(Registry registry, SketchObject o, Lang lang)
        {
            var type = registry.Type(o.Type);
            if (type is null) return null;
            var numeral = type.Parameters.FirstOrDefault(p => p.Key == "numeral" && p.Kind == ParamKind.Text);
            if (numeral is not null)
            {
                var shown = !string.IsNullOrEmpty(o.Ref) ? o.Ref! : !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("numeral", numeral.Default?.Text ?? "");
                return new TextEditInfo { Key = "numeral", Label = numeral.Label.Get(lang), Value = shown };
            }
            if (type.HasText) return new TextEditInfo { Label = "Text", Value = o.Text ?? "" };
            foreach (var key in MainTextKeys)
            {
                var pd = type.Parameters.FirstOrDefault(p => p.Key == key && p.Kind == ParamKind.Text);
                if (pd is not null) return new TextEditInfo { Key = key, Label = pd.Label.Get(lang), Value = o.Str(key, pd.Default?.Text ?? "") };
            }
            return null;
        }

        /// <summary>The object with its main text (<see cref="MainTextOf"/>) set; a numeral also becomes the object's reference.</summary>
        public static SketchObject WithMainText(Registry registry, SketchObject o, string text)
        {
            var edit = MainTextOf(registry, o, Lang.En);
            if (edit is null) return o;
            if (edit.Key is null) return o with { Text = text };
            if (edit.Key == "numeral") return o.WithParam(edit.Key, text) with { Text = null, Ref = text };
            return o.WithParam(edit.Key, text);
        }

        /// <summary>
        /// Centred part: a box object with a pitch circle or hub anchor (gears, bearings, wheels). Hosts resize it about its
        /// centre by default (Ctrl: from the opposite edge), like the COM add-in.
        /// </summary>
        private bool IsCentred(SketchObject o, RenderContext ctx)
            => !o.IsPointGeometry && Registry.Type(o.Type) is not null
               && Registry.PageAnchors(o, ctx).Any(a => a.Role is AnchorRole.Pitch or AnchorRole.PitchInner or AnchorRole.Hub);

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
            return Edit(r, "Parameter", (s, ids) => Edits.Update(s.Doc, ids, o => Registry.SetParam(o, r.Key!, value)));
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
                attach = r.Anchor == false ? null : sp.Attach;
                // Annotation ends dock anywhere on the element below them (free docking on a surface).
                if (attach is null && r.Anchor != false && Registry.Type(o.Type)?.DocksOnSurface == true)
                    attach = Attachments.OnSurface(p, s.Doc.ObjectsOn(s.Page).Where(x => x.Id != o.Id));
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
            var ids = s.Resolve(r.Ids).ToList();
            // Coupled parts (gears on a dragged shaft) move along: their anchors snap too and they are no targets.
            if (s.Couple) ids.AddRange(Coupling.RigidFollowers(Registry, s.Doc, ids, s.Lang).Where(id => !ids.Contains(id)));
            var ctx = s.Context();
            var opts = Options(s, r.Tolerance);
            var moving = ids.Select(s.Doc.Get).Where(o => o is not null && Registry.Type(o.Type) is not null)
                .SelectMany(o => Registry.PageAnchors(o!, ctx)).ToList();
            var targets = Snapper.Targets(Registry, s.Doc, s.Page, new HashSet<string>(ids), ctx, opts);
            var delta = new Vec2(r.Dx ?? 0, r.Dy ?? 0);
            if (moving.Count == 0) return new SnapResultDto { Dx = delta.X, Dy = delta.Y };
            var res = Snapper.SnapMove(moving, delta, targets, opts);
            return new SnapResultDto { Dx = res.Delta.X, Dy = res.Delta.Y, Kind = res.Kind, Guides = res.Guides, Label = SnapLabel.For(res.Target, s.Lang) };
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
            var dto = new SnapResultDto { Dx = res.Delta.X, Dy = res.Delta.Y, X = q.X, Y = q.Y, Kind = res.Kind, Guides = res.Guides, Label = SnapLabel.For(res.Target, s.Lang) };
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
            return new SnapResultDto { Dx = res.Delta.X, Dy = res.Delta.Y, Kind = res.Kind, Guides = res.Guides, Label = SnapLabel.For(res.Target, s.Lang) };
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
                        ParamKeys = a.ParamKeys?.ToList(),
                        Choice = a.Choice?.Key, ChoiceLabel = a.Choice?.Label.Get(lang), ChoiceMenu = a.Choice?.IsMenu == true, ChoiceLabelInBar = a.Choice?.LabelInBar == true, ChoiceBarIcon = a.Choice?.BarIcon, PreviewSvg = PreviewSvg(a),
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

        /// <summary>Assigns the next free reference numeral to objects without one (and places a numeral label if the figures module provides "patent.numeral").</summary>
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

        // ---- Parts of an element (charts v2, docs/en/design/charts-v2.md) ----

        private SketchObject PartOwner(EditorSession s, EngineRequest r)
        {
            var id = r.Id ?? s.Resolve(r.Ids).FirstOrDefault();
            return s.Doc.Get(id ?? "") ?? throw new KeyNotFoundException($"Unknown object '{id}'.");
        }

        /// <summary>parts: the selectable parts of one element (id) with label, kind, hidden flag and own look.</summary>
        private object PartsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var o = PartOwner(s, r);
            if (Registry.Type(o.Type) is null) return new List<PartView>();
            return Registry.Parts(o, s.Context()).Select(p => new PartView
            {
                Key = p.Key, Label = p.Label.Get(lang), Kind = p.Kind, Hidden = p.Hidden,
                Style = o.Parts is not null && o.Parts.TryGetValue(p.Key, out var look) ? look.Style : null,
            }).ToList();
        }

        /// <summary>partAt: the part of element id under the page point (x, y) within tolerance (mm), or null.</summary>
        private object PartAtCore(EngineRequest r)
        {
            var s = S(r);
            var o = PartOwner(s, r);
            if (Registry.Type(o.Type) is null) return new { part = (string?)null };
            return new { part = Registry.PartAt(o, new Vec2(r.X ?? 0, r.Y ?? 0), s.Context(), r.Tolerance ?? 1.0) };
        }

        /// <summary>partActions: actions of one part of element id (type actions first, then show/hide, thicker, thinner, line style, reset).</summary>
        private object PartActionsCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var o = PartOwner(s, r);
            var part = r.Part ?? throw new ArgumentException("Missing 'part'.");
            if (Registry.Type(o.Type) is null) return new List<ActionInfo>();
            return Registry.PartActions(o, part, s.Context()).Select(a => new ActionInfo
            {
                Id = a.Id, Label = a.Label.Get(lang), Icon = a.Icon, Group = a.Group ?? "part", Kind = "part", Active = a.IsActive, Enabled = a.IsEnabled,
                ParamKeys = a.ParamKeys?.ToList(),
                Choice = a.Choice?.Key, ChoiceLabel = a.Choice?.Label.Get(lang), ChoiceMenu = a.Choice?.IsMenu == true, ChoiceLabelInBar = a.Choice?.LabelInBar == true, ChoiceBarIcon = a.Choice?.BarIcon, PreviewSvg = PreviewSvg(a),
                Input = a.Input is null ? null : new ActionInputInfo { Label = a.Input.Label.Get(lang), Value = a.Input.Value, Unit = a.Input.Unit },
            }).ToList();
        }

        /// <summary>The option preview of an action as a small SVG (<see cref="ContextAction.Preview"/>), or null.</summary>
        private static string? PreviewSvg(ContextAction a)
            => a.Preview is { Count: > 0 } prims ? SvgRenderer.RenderPrimitives(prims, padding: 0.4) : null;

        /// <summary>applyPartAction: runs action (id from partActions) on part of every listed element that offers it.</summary>
        private object ApplyPartActionCore(EngineRequest r)
        {
            var s = S(r);
            var part = r.Part ?? throw new ArgumentException("Missing 'part'.");
            var action = r.Action ?? throw new ArgumentException("Missing 'action'.");
            var ids = r.Id is not null ? new List<string> { r.Id } : s.Resolve(r.Ids).ToList();
            s.Apply("Part", d =>
            {
                var ctx = new RenderContext(d, s.Lang);
                return Edits.Update(d, ids, o =>
                {
                    if (Registry.Type(o.Type) is null) return o;
                    var a = Registry.PartActions(o, part, ctx).FirstOrDefault(x => x.Id == action && x.IsEnabled);
                    if (a is null) return o;
                    return a.Input is not null ? a.Input.Apply(o, r.Input ?? a.Input.Value) : a.Apply(o);
                });
            });
            return StateOf(s, r);
        }

        // ---- Drag handles (limit lines, value lines, markers of charts …) ----

        private static DragHandleInfo HandleInfo(DragHandle h, Lang lang) => new()
        {
            Key = h.Key,
            X = Math.Round(h.Position.X, 4),
            Y = Math.Round(h.Position.Y, 4),
            Constraint = h.Constraint switch { DragConstraint.Horizontal => "horizontal", DragConstraint.Vertical => "vertical", _ => "free" },
            Dx = Math.Round(h.Direction.X, 6),
            Dy = Math.Round(h.Direction.Y, 6),
            Label = h.Label.Get(lang),
            Part = h.Part,
            ShowWith = h.ShowWith,
            ByPart = h.ByPart,
        };

        /// <summary>
        /// insertHandles: the insert places of the listed elements (id or ids; default the selection) in page mm – key,
        /// position, outward direction (dx, dy) and label (<see cref="InsertHandles.Of"/>: the "+" dots between the rows and
        /// columns of a table). Hosts without an overlay list them ("Insert a row here (before row 3)"). Elements without
        /// insert places are left out.
        /// </summary>
        private object InsertHandlesCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var ids = r.Id is not null ? new List<string> { r.Id } : s.Resolve(r.Ids).ToList();
            var ctx = new RenderContext(s.Doc, lang);
            var list = new List<ObjectInsertHandles>();
            foreach (var id in ids)
            {
                if (s.Doc.Get(id) is not { } o || o.Locked) continue;
                var handles = InsertHandles.Of(Registry, o, ctx);
                if (handles.Count > 0)
                    list.Add(new ObjectInsertHandles
                    {
                        Id = id,
                        Handles = handles.Select(h => new InsertHandleInfo { Key = h.Key, X = h.Position.X, Y = h.Position.Y, Dx = h.Direction.X, Dy = h.Direction.Y, Label = h.Label.Get(lang) }).ToList(),
                    });
            }
            return list;
        }

        /// <summary>
        /// applyInsertHandle: inserts at the insert place <c>key</c> of element <c>id</c> (<see cref="InsertHandles.Apply"/>:
        /// a row or column exactly there); one undo step, returns the state. An unknown key is an error.
        /// </summary>
        private object ApplyInsertHandleCore(EngineRequest r)
        {
            var s = S(r);
            var id = r.Id ?? s.Resolve(r.Ids).FirstOrDefault() ?? throw new ArgumentException("Missing 'id'.");
            var o = s.Doc.Get(id) ?? throw new KeyNotFoundException($"Unknown object '{id}'.");
            var key = r.Key ?? throw new ArgumentException("Missing 'key'.");
            if (o.Locked) return StateOf(s, r);
            if (InsertHandles.Of(Registry, o, new RenderContext(s.Doc, s.Lang)).All(h => h.Key != key))
                throw new ArgumentException($"'{o.Type}' has no insert place '{key}'.");
            s.Apply("Insert here", d => d.Get(id) is { } x ? Edits.Replace(d, InsertHandles.Apply(Registry, x, key, new RenderContext(d, s.Lang))) : d);
            return StateOf(s, r);
        }

        /// <summary>
        /// dragHandles: the interactive handles of the listed elements (id or ids; default the selection) in page mm – key,
        /// position, constraint ("free", "horizontal", "vertical") with its page direction (dx, dy), label with the current value
        /// and part key. Elements without handles are left out.
        /// </summary>
        private object DragHandlesCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var ids = r.Id is not null ? new List<string> { r.Id } : s.Resolve(r.Ids).ToList();
            var ctx = s.Context();
            var list = new List<ObjectHandles>();
            foreach (var id in ids)
            {
                if (s.Doc.Get(id) is not { } o || o.Locked || Registry.Type(o.Type) is null) continue;
                var handles = Registry.DragHandles(o, ctx);
                if (handles.Count > 0) list.Add(new ObjectHandles { Id = id, Handles = handles.Select(h => HandleInfo(h, lang)).ToList() });
            }
            return list;
        }

        /// <summary>
        /// moveHandle: drags handle key of element id to the page point (x, y) – the element applies the handle's constraint
        /// and snaps the value; one undo step, returns the state. With preview=true the document stays unchanged and the
        /// result is <c>{ handle }</c>: the handle as it would be after the move (snapped position and label), or null.
        /// </summary>
        private object MoveHandleCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var o = PartOwner(s, r);
            var key = r.Key ?? throw new ArgumentException("Missing 'key'.");
            var at = new Vec2(r.X ?? 0, r.Y ?? 0);
            if (r.Preview == true)
            {
                if (o.Locked || Registry.Type(o.Type) is null) return new { handle = (DragHandleInfo?)null };
                var ctx = s.Context();
                var moved = Registry.MoveHandle(o, key, at, ctx);
                var h = Registry.DragHandles(moved, ctx).FirstOrDefault(x => x.Key == key);
                return new { handle = h is null ? null : HandleInfo(h, lang) };
            }
            if (!o.Locked && Registry.Type(o.Type) is not null)
            {
                s.Apply("Move handle", d =>
                {
                    if (d.Get(o.Id) is not { } x) return d;
                    var moved = Registry.MoveHandle(x, key, at, new RenderContext(d, s.Lang));
                    // Nothing changed (unknown key, value snapped to the same number): no undo step.
                    if (ReferenceEquals(moved, x) || moved.Params.Count == x.Params.Count && moved.Params.All(kv => x.Params.TryGetValue(kv.Key, out var v) && v.Equals(kv.Value)) && moved.Size == x.Size)
                        return d;
                    return Edits.Replace(d, moved);
                });
            }
            return StateOf(s, r);
        }

        /// <summary>
        /// partOutline: the drawn outline of one part of element id in page mm, as polylines [[x, y], …] (lines and curves
        /// sampled, sub-paths separate, texts and areas as their box), for hosts that highlight the selected part.
        /// </summary>
        /// <summary>
        /// sketchCurve: a curve drawn by hand into chart id (points = pointer trail in page mm, optionally [x, y, t] with the
        /// pointer time t in ms as a drawing-speed hint) is recognised or smoothed and
        /// added as a new series (charts v2, <see cref="InventaSketch.Modules.Charts.ChartSketchInput"/>); one undo step.
        /// Returns the state plus the message (what was recognised); unchanged state with an empty message if nothing fits.
        /// </summary>
        private object SketchCurveCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var chart = s.Doc.Get(r.Id ?? "") ?? throw new KeyNotFoundException($"Unknown object '{r.Id}'.");
            var pts = (r.Points ?? new List<double[]>()).Where(p => p.Length >= 2).ToList();
            var trail = pts.Select(p => new Vec2(p[0], p[1])).ToList();
            // Optional third value: the pointer time in ms (drawing speed as a detail hint), used when every point has one.
            var times = pts.Count > 0 && pts.All(p => p.Length >= 3) ? pts.Select(p => p[2]).ToList() : null;
            var result = InventaSketch.Modules.Charts.ChartSketchInput.Add(Registry, chart, trail, new RenderContext(s.Doc, lang), times);
            if (result is null) return new { state = StateOf(s, r), message = "" };
            s.Apply("Sketch", d => Edits.Update(d, new[] { chart.Id }, _ => result.Chart));
            return new { state = StateOf(s, r), message = result.Message.Get(lang), kind = result.Kind?.ToString() };
        }

        /// <summary>
        /// chartFromPicture: a picture (payload = base64 RGBA bytes, w × h pixels) becomes a chart on the page at (x, y) mm
        /// (charts v2 stage 5, <see cref="InventaSketch.Modules.Charts.Scanning.ChartFromPicture"/>); one undo step, selected.
        /// Returns the state plus the message for the user (also when nothing was found).
        /// </summary>
        private object ChartFromPictureCore(EngineRequest r)
        {
            var s = S(r);
            var lang = r.Lang is null ? s.Lang : Language.Detect(r.Lang);
            var w = (int)(r.W ?? 0); var h = (int)(r.H ?? 0);
            var bytes = string.IsNullOrEmpty(r.Payload) ? Array.Empty<byte>() : Convert.FromBase64String(r.Payload);
            var image = new InventaSketch.Modules.Charts.Scanning.PixelImage(w, h, bytes);
            var at = new Vec2(r.X ?? s.Page.Size.W / 2, r.Y ?? s.Page.Size.H / 2);
            var result = InventaSketch.Modules.Charts.Scanning.ChartFromPicture.Build(Registry, image, at, lang);
            if (result.Chart is not null)
            {
                var chart = result.Chart;
                s.Apply("Chart from picture", d => Edits.Add(d, s.PageId, chart));
                s.Select(new List<string> { chart.Id });
            }
            return new { state = StateOf(s, r), message = result.Message.Get(lang), id = result.Chart?.Id };
        }

        private object PartOutlineCore(EngineRequest r)
        {
            var s = S(r);
            var o = PartOwner(s, r);
            var part = r.Part ?? throw new ArgumentException("Missing 'part'.");
            var lines = new List<List<double[]>>();
            if (Registry.Type(o.Type) is null) return lines;
            static double[] P(Vec2 v) => new[] { Math.Round(v.X, 3), Math.Round(v.Y, 3) };
            // The drawn primitives of the part plus its part shapes (a data point: a small circle).
            foreach (var p in Registry.PartPrimitives(o, part, s.Context()))
            {
                switch (p)
                {
                    case LinePrim l: lines.Add(new List<double[]> { P(l.A), P(l.B) }); break;
                    case PathPrim path:
                        {
                            var sub = new List<PathSeg>();
                            void Flush() { if (sub.Count > 1) lines.Add(PartLooks.Sample(sub).Select(P).ToList()); sub.Clear(); }
                            foreach (var seg in path.Segments) { if (seg.Op == SegOp.M) Flush(); sub.Add(seg); }
                            Flush();
                            break;
                        }
                    default:
                        {
                            var pts = Prim.Points(p).ToList();
                            if (pts.Count == 0) break;
                            var b = Rect.FromPoints(pts);
                            if (p is TextPrim tp) b = b.Inflate(tp.TextStyle.Size * 0.3);
                            lines.Add(new List<double[]> { P(new Vec2(b.X, b.Y)), P(new Vec2(b.Right, b.Y)), P(new Vec2(b.Right, b.Bottom)), P(new Vec2(b.X, b.Bottom)), P(new Vec2(b.X, b.Y)) });
                            break;
                        }
                }
            }
            return lines;
        }

        /// <summary>
        /// setPartLook: merges style into the look of part (replace: sets it), and/or shows or hides it (hidden), on every
        /// listed element. Hosts call it when the user formats a part with the host's own tools.
        /// </summary>
        private object SetPartLookCore(EngineRequest r)
        {
            var part = r.Part ?? throw new ArgumentException("Missing 'part'.");
            return Edit(r, "Part look", (s, ids) => Edits.Update(s.Doc, ids, o => PartLooks.WithLook(o, part, p => p with
            {
                Style = r.Style is null ? (r.Replace == true ? null : p.Style) : r.Replace == true ? r.Style : (p.Style ?? new Style()).Merge(r.Style),
                Hidden = r.Hidden ?? p.Hidden,
            })));
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
                    Options = p.Options?.Select(o => new ActionInfo { Id = o.Value, Label = o.Label.Get(lang), Kind = "option", Icon = o.Icon ?? "" }).ToList(),
                    MirrorIcons = p.MirrorIcons,
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
            // mode "export": named groups per element and part for editing in Inkscape/Illustrator (texts stay text).
            var options = new SvgOptions { Monochrome = r.Monochrome ?? s.Doc.Settings.Monochrome, Background = r.Background ?? "#ffffff", Editable = r.Mode == "export" };
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
                // replace = false: objects already in the session stay (a unit's stored copies of its docking targets must not
                // overwrite the targets' own, newer models when several units are imported into one page session).
                if (r.Replace != false) foreach (var o in existing) d = Edits.Replace(d, o);
                if (fresh.Length > 0) d = Edits.Add(d, s.PageId, fresh);
                foreach (var kv in src.References) if (!d.References.ContainsKey(kv.Key)) d = Edits.SetReference(d, kv.Key, kv.Value.Label);
                // A plan scale stored with the slide model is adopted when the editor document has none.
                if (d.Settings.PlanScale is null && src.Settings.PlanScale is double ps) d = d with { Settings = d.Settings with { PlanScale = ps } };
                // Black and white is a slide-wide switch (the COM add-in sets it on every unit): the slide model decides, so
                // live redraws keep the look of the units on the slide.
                if (d.Settings.Monochrome != src.Settings.Monochrome) d = d with { Settings = d.Settings with { Monochrome = src.Settings.Monochrome } };
                // So are the line group and the accent colour.
                if (d.Settings.LineGroup != src.Settings.LineGroup) d = d with { Settings = d.Settings with { LineGroup = src.Settings.LineGroup } };
                if (d.Settings.Accent != src.Settings.Accent) d = d with { Settings = d.Settings with { Accent = src.Settings.Accent } };
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
            // Black and white changes the rendering of every object.
            if (before.Settings.Monochrome != after.Settings.Monochrome) changed.UnionWith(after.Objects.Keys);
            // So do the line group (every stroke width) and the accent colour.
            if (before.Settings.LineGroup != after.Settings.LineGroup || before.Settings.Accent != after.Settings.Accent) changed.UnionWith(after.Objects.Keys);
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
                var ctx = new RenderContext(doc, lang).For(registry);
                foreach (var id in touched)
                {
                    var o = doc.Get(id);
                    var type = o is null ? null : registry.Type(o.Type);
                    if (o is null || type is null) continue;
                    foreach (var dep in type.Dependents(o, ctx))
                        if (after.Objects.ContainsKey(dep) && !seen.Contains(dep)) found.Add(dep);
                }
            }
            // Parts lists follow every added, changed or removed part.
            if (touched.Any(id => (after.Get(id) ?? before.Get(id)) is { } t && PartsList.IsPart(registry, t)))
                foreach (var o in after.Objects.Values)
                    if (!seen.Contains(o.Id) && registry.Type(o.Type)?.DependsOnAllParts == true) found.Add(o.Id);
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
