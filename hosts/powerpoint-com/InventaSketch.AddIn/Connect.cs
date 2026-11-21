using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;
using InventaSketch.AddIn.Interop;
using InventaSketch.Core;
using InventaSketch.Engine;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// Entry point of the PowerPoint COM add-in. PowerPoint creates this class through its ProgId and calls
    /// IDTExtensibility2 and IRibbonExtensibility. Ribbon callbacks are invoked via IDispatch and must be public.
    /// Every callback catches all exceptions: exceptions must never reach PowerPoint.
    /// </summary>
    [ComVisible(true)]
    [Guid("1D58D258-A932-4753-9EAA-309C5736FE34")]
    [ProgId("InventaSketch.AddIn.Connect")]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public sealed class Connect : IDTExtensibility2, Office.IRibbonExtensibility, Office.ICustomTaskPaneConsumer
    {
        private PowerPoint.Application? _app;
        private Office.IRibbonUI? _ribbon;
        /// <summary>
        /// Module library, built on a worker thread from <see cref="OnConnection"/> (about 0.7 s of cold JIT): Office measures
        /// the start time of add-ins and disables slow ones. Readers wait for it; at startup only a stale ribbon cache does.
        /// </summary>
        private System.Threading.Tasks.Task<Catalog>? _catalog;
        private Registry? _registry => _catalog?.Result.Registry;
        private List<LibraryCategory> _categories => _catalog?.Result.Categories ?? new List<LibraryCategory>();
        private Lang _lang = Lang.En;

        internal sealed record Catalog(Registry Registry, List<LibraryCategory> Categories);

        internal static Catalog BuildCatalog()
        {
            var registry = ModuleCatalog.CreateDefault();
            var used = new HashSet<string>(registry.Library.Select(i => i.Category));
            return new Catalog(registry, registry.Categories.Where(c => used.Contains(c.Id)).OrderBy(c => c.Order).ToList());
        }
        private readonly Dictionary<string, Bitmap> _images = new();
        private OnSlide.OnSlideController? _onSlide;
        private Office.ICTPFactory? _ctpFactory;
        private Office.CustomTaskPane? _pane;
        private Sidebar.SidebarBridge? _bridge;

        static Connect()
        {
            // Load dependencies (System.Text.Json and its helpers) from the add-in folder regardless of the requested
            // version: POWERPNT.EXE.config has no binding redirects for us.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAddInDirectory;
        }

        internal static Assembly? ResolveFromAddInDirectory(object? sender, ResolveEventArgs args)
        {
            try
            {
                var dir = Path.GetDirectoryName(typeof(Connect).Assembly.Location)!;
                var file = Path.Combine(dir, new AssemblyName(args.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ IDTExtensibility2

        public void OnConnection(object application, int connectMode, object addInInst, ref Array custom)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _app = (PowerPoint.Application)application;
                _lang = DetectLanguage(_app);
                _catalog = System.Threading.Tasks.Task.Run(BuildCatalog);
                // After startup: refresh the ribbon cache and keep Office from disabling the add-in (Slidemax B80).
                _catalog.ContinueWith(t =>
                {
                    if (t.Status != System.Threading.Tasks.TaskStatus.RanToCompletion) { Log.Error("Library", t.Exception); return; }
                    RibbonCache.Store(_lang, () => RibbonXml());
                    Resilience.Apply("startup");
                    Log.Info($"Library ready: {t.Result.Registry.Library.Count} items in {t.Result.Categories.Count} categories, {watch.ElapsedMilliseconds} ms after connect");
                });
                if (addInInst is Office.COMAddIn comAddIn) comAddIn.Object = new Automation(this);
                _onSlide = new OnSlide.OnSlideController(_app, () => _registry!, () => _lang);
                _bridge = new Sidebar.SidebarBridge(_app, () => _registry!, () => _lang, BeginPlacement);
                _onSlide.UnitArmed += u => _bridge.UnitSelected(u);
                // Units deleted on the slide (Delete key, cut, undo) also leave the sidebar editor.
                _app.WindowSelectionChange += _ =>
                {
                    try
                    {
                        if (_pane?.Visible == true && CurrentSlide(_app) is PowerPoint.Slide slide) _bridge.SlideUnits(slide);
                    }
                    catch (Exception ex) { Log.Error("Sidebar slide units", ex); }
                };
                // Model exchange with LibreOffice: mirror the models into document properties on save, adopt
                // LibreOffice-drawn units on open.
                _app.PresentationBeforeSave += (PowerPoint.Presentation pres, ref bool cancel) => MirrorModels(pres);
                _app.AfterPresentationOpen += AdoptUnits;
                Log.Info($"Connected: InventaSketch {typeof(Connect).Assembly.GetName().Version}, PowerPoint {_app.Version}, "
                    + $"{(Environment.Is64BitProcess ? 64 : 32)} bit, CLR {Environment.Version}, language {_lang}, "
                    + $"mode {connectMode}, {watch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Log.Error("OnConnection", ex);
            }
        }

        public void OnDisconnection(int removeMode, ref Array custom)
        {
            try { _onSlide?.Dispose(); } catch (Exception ex) { Log.Error("OnSlide dispose", ex); }
            _onSlide = null;
            foreach (var b in _images.Values) b.Dispose();
            _images.Clear();
            _app = null;
        }

        public void OnAddInsUpdate(ref Array custom) { }

        public void OnStartupComplete(ref Array custom)
        {
            try
            {
                if (_app is null) return;
                foreach (PowerPoint.Presentation pres in _app.Presentations) AdoptUnits(pres);
            }
            catch (Exception ex) { Log.Error("Adopt units at startup", ex); }
        }

        private static void MirrorModels(PowerPoint.Presentation pres)
        {
            try
            {
                var n = OnSlide.DocumentMirror.Write(pres);
                if (n > 0) Log.Info($"Mirrored {n} unit models into document properties");
            }
            catch (Exception ex) { Log.Error("Mirror unit models", ex); }
        }

        private static void AdoptUnits(PowerPoint.Presentation pres)
        {
            try
            {
                var n = OnSlide.DocumentMirror.Adopt(pres);
                if (n > 0) Log.Info($"Adopted {n} units drawn by LibreOffice");
            }
            catch (Exception ex) { Log.Error("Adopt units", ex); }
        }

        public void OnBeginShutdown(ref Array custom) => Resilience.Apply("shutdown");

        /// <summary>
        /// Writes the ribbon cache for both languages (called by install.ps1 right after registering, so even the first
        /// start after an install does not wait for the library). Returns the number of files written.
        /// </summary>
        public static int WarmRibbonCache()
        {
            var connect = new Connect { _catalog = System.Threading.Tasks.Task.FromResult(BuildCatalog()) };
            var n = 0;
            foreach (var lang in new[] { Lang.De, Lang.En })
            {
                connect._lang = lang;
                if (RibbonCache.Store(lang, () => connect.RibbonXml(), force: true)) n++;
            }
            return n;
        }

        private static Lang DetectLanguage(PowerPoint.Application app)
        {
            try
            {
                var lcid = app.LanguageSettings.get_LanguageID(Office.MsoAppLanguageID.msoLanguageIDUI);
                return (lcid & 0x3FF) == 0x07 ? Lang.De : Lang.En;
            }
            catch
            {
                return Language.FromCurrentCulture();
            }
        }

        // ------------------------------------------------------------------ Ribbon (generated from the module library)

        public string GetCustomUI(string ribbonId)
        {
            try
            {
                // Startup: the cached ribbon of this build and language, so PowerPoint does not wait for the library.
                return RibbonCache.Load(_lang) ?? RibbonXml();
            }
            catch (Exception ex)
            {
                Log.Error("GetCustomUI", ex);
                return "<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\"/>";
            }
        }

        private static string X(string s) => SecurityElement.Escape(s) ?? "";
        /// <summary>Ribbon label: "&" marks an access key in Office labels, so a literal ampersand is doubled.</summary>
        private static string L(string s) => X(s.Replace("&", "&&"));

        /// <summary>
        /// One group per module, one gallery per library category (thumbnail grid, tooltip = name), then the tools group.
        /// New modules and categories appear without changing this code.
        /// </summary>
        /// <summary>
        /// Ribbon groups in order: one "Annotate" group right after the tools (annotation, patent figures and basic
        /// shapes: the things every sketch needs), then one group per domain module. New modules appear automatically.
        /// </summary>
        private List<(string Label, List<IModule> Modules)> RibbonGroups()
        {
            var modules = (_registry?.Modules ?? Array.Empty<IModule>()).Where(mod => _categories.Any(c => c.ModuleId == mod.Id)).ToList();
            var annotate = new[] { "annotation", "patent", "basic" };
            var order = new[] { "mechanical", "kinematics", "electrical", "machines", "automotive", "architecture", "diagrams" };
            var groups = new List<(string, List<IModule>)>();
            var first = modules.Where(mod => Array.IndexOf(annotate, mod.Id) >= 0).OrderBy(mod => Array.IndexOf(annotate, mod.Id)).ToList();
            if (first.Count > 0) groups.Add((T("Annotate", "Beschriftung"), first));
            foreach (var mod in modules.Where(mod => Array.IndexOf(annotate, mod.Id) < 0)
                         .OrderBy(mod => Array.IndexOf(order, mod.Id) is var i && i >= 0 ? i : order.Length))
                groups.Add((mod.Name.Get(_lang), new List<IModule> { mod }));
            return groups;
        }
        private string RibbonXml()
        {
            var sb = new StringBuilder();
            sb.Append("<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\" onLoad=\"OnRibbonLoad\"><ribbon><tabs>");
            sb.Append("<tab id=\"IskTab\" label=\"InventaSketch\">");
            AppendStructureGroup(sb);
            sb.Append("<group id=\"IskTools\" getImage=\"GetLogoImage\" label=\"").Append(L(T("Tools", "Werkzeuge"))).Append("\">");
            sb.Append("<toggleButton id=\"IskEditor\" label=\"").Append(L(T("Editor", "Editor"))).Append("\" size=\"large\" imageMso=\"ControlsGallery\" getPressed=\"GetEditorPressed\" onAction=\"OnEditorToggle\" screentip=\"")
              .Append(X(T("Sidebar editor", "Seitenleisten-Editor"))).Append("\" supertip=\"").Append(X(T("Library, stage and properties in a side panel; changes appear on the slide immediately.", "Bibliothek, Bühne und Eigenschaften in der Seitenleiste; Änderungen erscheinen sofort auf der Folie."))).Append("\" />");
            sb.Append("<toggleButton id=\"IskSnap\" label=\"").Append(L(T("Snap", "Einrasten"))).Append("\" imageMso=\"SnapToGrid\" getPressed=\"GetTogglePressed\" onAction=\"OnToggle\" supertip=\"")
              .Append(X(T("Magnetic snapping: gear mesh, axes, points and alignment. Hold Alt to move freely.", "Magnetisches Einrasten: Zahneingriff, Achsen, Punkte und Fluchtlinien. Alt gedrückt halten für freies Bewegen."))).Append("\" />");
            sb.Append("<toggleButton id=\"IskBar\" label=\"").Append(L(T("Context bar", "Kontextleiste"))).Append("\" imageMso=\"QuickStylesGallery\" getPressed=\"GetTogglePressed\" onAction=\"OnToggle\" supertip=\"")
              .Append(X(T("Floating bar with views and commands next to the selected element.", "Schwebende Leiste mit Ansichten und Befehlen neben dem ausgewählten Element."))).Append("\" />");
            sb.Append("<button id=\"IskMonochrome\" label=\"").Append(L(T("Black & white", "Schwarz-Weiß"))).Append("\" imageMso=\"PictureRecolorBlackAndWhite\" onAction=\"OnMonochrome\" supertip=\"")
              .Append(X(T("Shows the InventaSketch elements of this slide in black and white (patent style): fills, text and highlighters included. Click again to restore the colours.", "Zeigt die InventaSketch-Elemente dieser Folie schwarz-weiß (Patentstil), einschließlich Füllungen, Text und Markierungen. Erneut klicken stellt die Farben wieder her."))).Append("\" />");
            sb.Append("<toggleButton id=\"IskAnchor\" label=\"").Append(L(T("Anchor", "Verankern"))).Append("\" getImage=\"GetAnchorImage\" getPressed=\"GetTogglePressed\" onAction=\"OnToggle\" supertip=\"")
              .Append(X(T("Line ends snapped to another element stay attached and follow when it is moved, rotated or resized. Moving the line itself detaches its ends.", "Eingerastete Linienenden bleiben am Element verankert und folgen, wenn es verschoben, gedreht oder skaliert wird. Wird die Linie selbst verschoben, lösen sich ihre Enden."))).Append("\" />");
            AppendToolsMenu(sb);
            sb.Append("<button id=\"IskAbout\" label=\"").Append(L(T("About", "Info"))).Append("\" getImage=\"GetLogoImage\" onAction=\"OnAbout\" />");
            sb.Append("</group>");
            if (_registry is not null)
            {
                var m = 0;
                var elementsOpen = false;
                void Gallery(LibraryCategory c, int i, bool inMenu, bool compact)
                {
                    // Inside a menu, galleries take no size/showLabel attributes (the menu shows their labels).
                    sb.Append("<gallery id=\"IskCat_").Append(i).Append("\" label=\"").Append(L(GalleryLabel(c))).Append('"').Append(" getVisible=\"GetModuleVisible\"");
                    if (!inMenu) sb.Append(" size=\"normal\" showLabel=\"").Append(compact ? "false" : "true").Append('"');
                    sb.Append(" getImage=\"GetCategoryImage\" columns=\"6\" itemWidth=\"40\" itemHeight=\"40\" showItemLabel=\"false\"")
                      .Append(" screentip=\"").Append(X(GalleryLabel(c))).Append("\" supertip=\"")
                      .Append(X(T("Pick an element, then click on the slide to place it (snaps to other elements). Esc cancels.",
                          "Element wählen, dann auf die Folie klicken (rastet an anderen Elementen ein). Esc bricht ab."))).Append('"')
                      .Append(" getItemCount=\"GetItemCount\" getItemImage=\"GetGalleryItemImage\" getItemScreentip=\"GetItemScreentip\"")
                      .Append(" getItemID=\"GetGalleryItemId\" onAction=\"OnGalleryAction\" />");
                }
                foreach (var (label, mods) in RibbonGroups())
                {
                    var ids = mods.Select(mod => mod.Id).ToList();
                    // Categories merged into another one's gallery get no gallery of their own.
                    var cats = _categories.Select((c, i) => (c, i)).Where(x => ids.Contains(x.c.ModuleId) && !MergedInto.ContainsKey(x.c.Id))
                        .OrderBy(x => ids.IndexOf(x.c.ModuleId)).ToList();
                    if (cats.Count == 0) continue;
                    if (m == 0)
                    {
                        // "Annotate" stays open as a group; many categories show icons only (name in the tooltip).
                        sb.Append("<group id=\"IskMod_").Append(m++).Append("\" getImage=\"GetGroupImage\" label=\"").Append(L(label)).Append("\">");
                        foreach (var (c, i) in cats) Gallery(c, i, inMenu: false, compact: cats.Count > 4);
                        sb.Append("</group>");
                        // Formatting groups (like Slidemax) right after "Annotate", before the domain modules.
                        AppendFormattingGroups(sb);
                        continue;
                    }
                    // Domain modules: one large menu each in the "Elements" group, so the tab fits on laptop screens.
                    if (!elementsOpen)
                    {
                        sb.Append("<group id=\"IskElements\" getImage=\"GetLogoImage\" label=\"").Append(L(T("Elements", "Elemente"))).Append("\">");
                        elementsOpen = true;
                    }
                    sb.Append("<menu id=\"IskMod_").Append(m++).Append("\" label=\"").Append(L(label)).Append("\" size=\"large\" itemSize=\"normal\" getImage=\"GetGroupImage\" getVisible=\"GetModuleVisible\">");
                    foreach (var (c, i) in cats) Gallery(c, i, inMenu: true, compact: false);
                    sb.Append("</menu>");
                }
                if (elementsOpen) sb.Append("</group>");
                if (m == 0) AppendFormattingGroups(sb);
            }
            else AppendFormattingGroups(sb);
            sb.Append("</tab></tabs></ribbon></customUI>");
            return sb.ToString();
        }

        /// <summary>
        /// The "Extras" menu of the tools group (like Slidemax's tools menu): insert symbol, decimal separator, proofing
        /// language, send and save slides, show/hide modules.
        /// </summary>
        private void AppendToolsMenu(StringBuilder sb)
        {
            void Item(string id, string label, string? imageMso)
                => sb.Append("<button id=\"").Append(id).Append("\" label=\"").Append(L(label)).Append('"')
                     .Append(imageMso is null ? "" : " imageMso=\"" + imageMso + "\"").Append(" onAction=\"OnToolsMenu\" />");
            sb.Append("<menu id=\"IskToolsMenu\" label=\"").Append(L(T("Extras", "Extras"))).Append("\" getImage=\"GetToolsMenuImage\" itemSize=\"normal\">");
            Item("IskMenuSymbol", T("Insert symbol \u2026", "Symbol einf\u00fcgen \u2026"), "SymbolInsert");
            Item("IskMenuDecimals", T("Change decimal separator \u2026", "Dezimaltrennzeichen \u00e4ndern \u2026"), null);
            sb.Append("<menu id=\"IskMenuLanguage\" label=\"").Append(L(T("Proofing language", "Korrektursprache"))).Append("\" imageMso=\"SetLanguage\">");
            Item("IskLangDe", T("German", "Deutsch"), null);
            Item("IskLangGb", T("English (UK)", "Englisch (UK)"), null);
            Item("IskLangUs", T("English (US)", "Englisch (USA)"), null);
            Item("IskLangMore", T("More languages \u2026", "Weitere Sprachen \u2026"), null);
            sb.Append("</menu>");
            sb.Append("<menuSeparator id=\"IskMenuSep1\" />");
            Item("IskMenuSend", T("Send slides \u2026", "Folien senden \u2026"), "FileSendAsAttachment");
            Item("IskMenuSave", T("Save slides \u2026", "Folien speichern \u2026"), "FileSaveAs");
            sb.Append("<dynamicMenu id=\"IskMenuExport\" label=\"").Append(L(T("Export data", "Daten exportieren"))).Append("\" imageMso=\"ExportTextFile\" getContent=\"GetExportMenu\" />");
            sb.Append("<menuSeparator id=\"IskMenuSep2\" />");
            Item("IskMenuPlanScale", T("Plan scale of the slide \u2026", "Planma\u00dfstab der Folie \u2026"), null);
            Item("IskMenuModules", T("Show and hide modules \u2026", "Module ein- und ausblenden \u2026"), null);
            sb.Append("</menu>");
        }

        public void OnToolsMenu(Office.IRibbonControl control)
        {
            if (_app is null) return;
            var german = _lang == Lang.De;
            try
            {
                switch (control.Id)
                {
                    case "IskMenuSymbol": Tools.InsertSymbol(_app); break;
                    case "IskMenuDecimals":
                        if (Tools.AskDecimals(german) is { } d)
                        {
                            _app.StartNewUndoEntry();
                            var n = Tools.SwitchDecimals(_app, d.Separator, d.WholePresentation);
                            MessageBox.Show(T($"{n} text(s) changed.", $"{n} Text(e) geändert."), "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;
                    case "IskLangDe": _app.StartNewUndoEntry(); Tools.SetLanguage(_app, Tools.German); break;
                    case "IskLangGb": _app.StartNewUndoEntry(); Tools.SetLanguage(_app, Tools.EnglishUk); break;
                    case "IskLangUs": _app.StartNewUndoEntry(); Tools.SetLanguage(_app, Tools.EnglishUs); break;
                    case "IskLangMore": Tools.LanguageDialog(_app); break;
                    case "IskMenuSend": Tools.SendSlides(_app, german); break;
                    case "IskMenuSave": Tools.SaveSlides(_app, german); break;
                    case "IskMenuPlanScale": SetPlanScale(); break;
                    case "IskMenuModules":
                        var modules = (_registry?.Modules ?? Array.Empty<IModule>()).Where(mod => _categories.Any(c => c.ModuleId == mod.Id))
                            .Select(mod => (mod.Id, mod.Name.Get(_lang))).ToList();
                        if (Tools.AskModules(modules, new HashSet<string>(AddInSettings.HiddenModules), german) is { } hidden)
                        {
                            AddInSettings.HiddenModules = hidden;
                            _ribbon?.Invalidate();
                            _bridge?.PushHiddenModules();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("OnToolsMenu " + control.Id, ex);
            }
        }

        /// <summary>
        /// Asks for the plan scale (1:n) of the current slide and sets it on every InventaSketch unit there
        /// (<see cref="DocumentSettings.PlanScale"/>; objects with an own scale keep it). One undo step.
        /// </summary>
        private void SetPlanScale()
        {
            if (_app is null || _registry is null || CurrentSlide(_app) is not PowerPoint.Slide slide) return;
            var units = OnSlide.SketchUnits.All(slide);
            if (units.Count == 0) return;
            var current = units.Select(u => u.Doc.Settings.PlanScale).FirstOrDefault(v => v is not null) ?? 100;
            var entered = OnSlide.TextPrompt.Ask(T("Plan scale 1:", "Planma\u00dfstab 1:"), current.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture), IntPtr.Zero);
            if (entered is null) return;
            if (!double.TryParse(entered.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
            {
                OnSlide.TextPrompt.Warn(T("Please enter a positive number, e.g. 50 or 100.", "Bitte eine positive Zahl eingeben, z. B. 50 oder 100."), IntPtr.Zero);
                return;
            }
            _app.StartNewUndoEntry();
            foreach (var u in units)
            {
                var doc = u.Doc with { Settings = u.Doc.Settings with { PlanScale = value } };
                OnSlide.SketchUnits.Replace(slide, u, doc, _registry, _lang);
            }
            Log.Info($"Plan scale 1:{value} for {units.Count} unit(s)");
        }

        /// <summary>Units the export works on: the selected InventaSketch units, else all units of the current slide.</summary>
        private List<OnSlide.SketchUnit> ExportUnits()
        {
            var units = new List<OnSlide.SketchUnit>();
            if (_app is null || CurrentSlide(_app) is not PowerPoint.Slide slide) return units;
            try
            {
                var sel = _app.ActiveWindow.Selection;
                if (sel.Type is PowerPoint.PpSelectionType.ppSelectionShapes)
                    foreach (PowerPoint.Shape s in sel.ShapeRange)
                        if (OnSlide.SketchUnits.Read(s) is { } u) units.Add(u);
            }
            catch (Exception ex) { Log.Error("Export selection", ex); }
            return units.Count > 0 ? units : OnSlide.SketchUnits.All(slide);
        }

        /// <summary>"Export data ▸": the domain exports that apply to the selection or the slide (built when the menu opens).</summary>
        public string GetExportMenu(Office.IRibbonControl control)
        {
            var sb = new StringBuilder("<menu xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\">");
            try
            {
                var doc = OnSlide.SketchUnits.Combined(ExportUnits());
                var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
                var exporters = _registry is null ? new List<IExporter>() : Exports.For(_registry, doc, objects).ToList();
                if (exporters.Count == 0)
                    sb.Append("<button id=\"IskExportNone\" label=\"").Append(L(T("Nothing to export on this slide", "Auf dieser Folie gibt es nichts zu exportieren"))).Append("\" enabled=\"false\" />");
                var i = 0;
                foreach (var e in exporters)
                    sb.Append("<button id=\"IskExport_").Append(i++).Append("\" tag=\"").Append(X(e.Id)).Append("\" label=\"").Append(L(e.Name.Get(_lang)))
                      .Append("\" supertip=\"").Append(X(e.Preserves.Get(_lang))).Append("\" onAction=\"OnExport\" />");
            }
            catch (Exception ex) { Log.Error("GetExportMenu", ex); }
            return sb.Append("</menu>").ToString();
        }

        /// <summary>Runs a domain export: saves the file (UTF-8; CSV with BOM for Excel) and copies the text to the clipboard.</summary>
        public void OnExport(Office.IRibbonControl control)
        {
            try
            {
                if (_registry is null || _app is null) return;
                var exporter = Exports.All(_registry).FirstOrDefault(e => e.Id == control.Tag);
                if (exporter is null) return;
                var doc = OnSlide.SketchUnits.Combined(ExportUnits());
                var text = exporter.Export(_registry, doc, doc.ObjectsOn(doc.Pages[0]).ToList(), _lang);
                Clipboard.SetText(text.Length == 0 ? " " : text);
                var presentation = _app.ActivePresentation;
                using var dialog = new SaveFileDialog
                {
                    Filter = $"{exporter.Name.Get(_lang)}|*.{exporter.Extension}|{T("All files", "Alle Dateien")}|*.*",
                    FileName = System.IO.Path.GetFileNameWithoutExtension(presentation.Name) + " - " + exporter.Name.Get(_lang).Split('(')[0].Trim() + "." + exporter.Extension,
                    InitialDirectory = string.IsNullOrEmpty(presentation.Path) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : presentation.Path,
                    OverwritePrompt = true,
                };
                if (dialog.ShowDialog() != DialogResult.OK) return;
                var encoding = new UTF8Encoding(exporter.Extension == "csv");
                System.IO.File.WriteAllText(dialog.FileName, text, encoding);
                Log.Info($"Exported {exporter.Id} to {dialog.FileName} ({text.Length} chars, also on the clipboard)");
            }
            catch (Exception ex)
            {
                Log.Error("OnExport " + control.Tag, ex);
            }
        }

        /// <summary>Hidden modules ("Show and hide modules") disappear from the ribbon: their menus and galleries.</summary>
        public bool GetModuleVisible(Office.IRibbonControl control)
        {
            try
            {
                var hidden = AddInSettings.HiddenModules;
                if (hidden.Count == 0) return true;
                var idx = control.Id.LastIndexOf('_');
                if (idx < 0 || !int.TryParse(control.Id.Substring(idx + 1), out var n)) return true;
                if (control.Id.StartsWith("IskCat_", StringComparison.Ordinal))
                    return n >= _categories.Count || !hidden.Contains(_categories[n].ModuleId);
                var groups = RibbonGroups();
                return n >= groups.Count || !groups[n].Modules.All(mod => hidden.Contains(mod.Id));
            }
            catch (Exception ex)
            {
                Log.Error("GetModuleVisible", ex);
                return true;
            }
        }

        /// <summary>The "Extras" menu pictogram: three lines (like the Slidemax tools menu) in the Office theme ink.</summary>
        public object? GetToolsMenuImage(Office.IRibbonControl control)
        {
            try
            {
                var size = (int)Math.Round(16 * DeviceScale());
                using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var k = size / 16f;
                    g.ScaleTransform(k, k);
                    using var pen = new Pen(OfficeTheme.IsDark() ? Color.FromArgb(230, 230, 230) : Color.FromArgb(40, 40, 40), 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                    g.DrawLine(pen, 2.5f, 4, 13.5f, 4); g.DrawLine(pen, 2.5f, 8, 13.5f, 8); g.DrawLine(pen, 2.5f, 12, 13.5f, 12);
                }
                return PictureConverter.ToPicture(bmp);
            }
            catch (Exception ex) { Log.Error("GetToolsMenuImage", ex); return null; }
        }

        /// <summary>Structure group like Slidemax: new slide, paste, format painter (Office's own controls).</summary>
        private void AppendStructureGroup(StringBuilder sb)
        {
            sb.Append("<group id=\"IskStructure\" label=\"").Append(L(T("Structure", "Struktur"))).Append("\" imageMso=\"SlideNewGallery\">");
            sb.Append("<box id=\"IskBoxStructure\" boxStyle=\"vertical\">");
            sb.Append("<control idMso=\"SlideNewGallery\" /><control idMso=\"PasteMenu\" /><control idMso=\"FormatPainter\" />");
            sb.Append("</box></group>");
        }

        /// <summary>
        /// Colour, text and layout groups like Slidemax: Office's own controls (they format native shapes; on InventaSketch
        /// units the changes are folded into the model, see NativeFormat) plus own buttons for align, distribute, same size
        /// and swap that work relative to the selection (one shape: relative to the slide).
        /// </summary>
        private void AppendFormattingGroups(StringBuilder sb)
        {
            sb.Append("<group id=\"IskColor\" label=\"").Append(L(T("Colour", "Farbe"))).Append("\" imageMso=\"ShapeFillColorPicker\">");
            sb.Append("<box id=\"IskBoxColor\" boxStyle=\"vertical\"><control idMso=\"FontColorPicker\" /><control idMso=\"ShapeFillColorPicker\" /><control idMso=\"ShapeOutlineColorPicker\" /></box>");
            sb.Append("</group>");

            sb.Append("<group id=\"IskText\" label=\"").Append(L(T("Text", "Text"))).Append("\" imageMso=\"Font\">");
            sb.Append("<box id=\"IskBoxFont\"><control idMso=\"Font\" showLabel=\"false\" /><control idMso=\"FontSize\" showLabel=\"false\" />")
              .Append("<control idMso=\"FontSizeIncrease\" showLabel=\"false\" /><control idMso=\"FontSizeDecrease\" showLabel=\"false\" />");
            LayoutButton(sb, "IskInsertSymbol", T("Insert symbol", "Symbol einf\u00fcgen"), "SymbolInsert");
            sb.Append("</box>");
            sb.Append("<box id=\"IskBoxFontStyle\"><control idMso=\"Bold\" showLabel=\"false\" /><control idMso=\"Italic\" showLabel=\"false\" />")
              .Append("<control idMso=\"Underline\" showLabel=\"false\" /><control idMso=\"Strikethrough\" showLabel=\"false\" />")
              .Append("<control idMso=\"Subscript\" showLabel=\"false\" /><control idMso=\"Superscript\" showLabel=\"false\" />")
              .Append("<control idMso=\"TextAlignGallery\" showLabel=\"false\" /></box>");
            sb.Append("<box id=\"IskBoxParagraph\"><control idMso=\"AlignLeft\" showLabel=\"false\" /><control idMso=\"AlignCenter\" showLabel=\"false\" />")
              .Append("<control idMso=\"AlignRight\" showLabel=\"false\" /><control idMso=\"IndentDecrease\" showLabel=\"false\" />")
              .Append("<control idMso=\"IndentIncrease\" showLabel=\"false\" /><control idMso=\"BulletsGallery\" showLabel=\"false\" />")
              .Append("<control idMso=\"NumberingGallery\" showLabel=\"false\" /></box>");
            sb.Append("</group>");

            sb.Append("<group id=\"IskLayout\" label=\"").Append(L(T("Layout", "Layout"))).Append("\" imageMso=\"ObjectsAlignLeft\">");
            sb.Append("<box id=\"IskBoxArrange\"><control idMso=\"ObjectBringToFrontMenu\" showLabel=\"false\" /><control idMso=\"ObjectSendToBackMenu\" showLabel=\"false\" /><control idMso=\"SelectionPane\" showLabel=\"false\" /></box>");
            sb.Append("<box id=\"IskBoxGroup\"><control idMso=\"ObjectsGroup\" showLabel=\"false\" /><control idMso=\"ObjectsUngroup\" showLabel=\"false\" /><control idMso=\"ObjectsRegroup\" showLabel=\"false\" /></box>");
            sb.Append("<box id=\"IskBoxSize\">");
            LayoutButton(sb, "IskSameHeight", T("Same height", "Gleiche H\u00f6he"), null);
            LayoutButton(sb, "IskSameWidth", T("Same width", "Gleiche Breite"), null);
            LayoutButton(sb, "IskSameSize", T("Same size", "Gleiche Gr\u00f6\u00dfe"), null);
            sb.Append("</box>");
            // Height above width (one column), so the group stays narrow.
            sb.Append("<control idMso=\"ShapeHeight\" showLabel=\"false\" /><control idMso=\"ShapeWidth\" showLabel=\"false\" />");
            sb.Append("<separator id=\"IskSepAlign\" />");
            sb.Append("<box id=\"IskBoxAlignH\">");
            LayoutButton(sb, "IskAlignLeft", T("Align left", "Linksb\u00fcndig ausrichten"), "ObjectsAlignLeft");
            LayoutButton(sb, "IskAlignCenter", T("Align centre", "Horizontal zentrieren"), "ObjectsAlignCenterHorizontal");
            LayoutButton(sb, "IskAlignRight", T("Align right", "Rechtsb\u00fcndig ausrichten"), "ObjectsAlignRight");
            LayoutButton(sb, "IskDistributeH", T("Distribute horizontally", "Horizontal verteilen"), "AlignDistributeHorizontally");
            sb.Append("</box><box id=\"IskBoxAlignV\">");
            LayoutButton(sb, "IskAlignTop", T("Align top", "Oben ausrichten"), "ObjectsAlignTop");
            LayoutButton(sb, "IskAlignMiddle", T("Align middle", "Vertikal zentrieren"), "ObjectsAlignMiddleVertical");
            LayoutButton(sb, "IskAlignBottom", T("Align bottom", "Unten ausrichten"), "ObjectsAlignBottom");
            LayoutButton(sb, "IskDistributeV", T("Distribute vertically", "Vertikal verteilen"), "AlignDistributeVertically");
            sb.Append("</box><box id=\"IskBoxSwap\">");
            LayoutButton(sb, "IskSwap", T("Swap positions", "Positionen tauschen"), null);
            sb.Append("</box></group>");
        }

        private void LayoutButton(StringBuilder sb, string id, string label, string? imageMso)
        {
            sb.Append("<button id=\"").Append(id).Append("\" showLabel=\"false\" label=\"").Append(L(label)).Append("\" screentip=\"").Append(X(label)).Append("\" ")
              .Append(imageMso is null ? "getImage=\"GetLayoutImage\"" : "imageMso=\"" + imageMso + "\"").Append(" onAction=\"OnLayout\" />");
        }

        public void OnRibbonLoad(Office.IRibbonUI ribbon) => _ribbon = ribbon;

        /// <summary>
        /// Layout buttons (ported from Slidemax's layout group): align and distribute relative to the selection (a single
        /// shape: relative to the slide), same height/width/size as the first selected shape, swap the positions of two
        /// shapes. One undo step each; moved InventaSketch units keep their model (bounds are folded in on the next read).
        /// </summary>
        public void OnLayout(Office.IRibbonControl control)
        {
            try
            {
                if (control.Id == "IskInsertSymbol") { _app?.CommandBars.ExecuteMso("SymbolInsert"); return; }
                var sel = _app?.ActiveWindow?.Selection;
                if (sel is null || sel.Type is not (PowerPoint.PpSelectionType.ppSelectionShapes or PowerPoint.PpSelectionType.ppSelectionText)) return;
                var range = sel.HasChildShapeRange ? sel.ChildShapeRange : sel.ShapeRange;
                var count = range.Count;
                if (count == 0) return;
                var toSlide = count == 1 ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse;
                _app!.StartNewUndoEntry();
                switch (control.Id)
                {
                    case "IskAlignLeft": range.Align(Office.MsoAlignCmd.msoAlignLefts, toSlide); break;
                    case "IskAlignCenter": range.Align(Office.MsoAlignCmd.msoAlignCenters, toSlide); break;
                    case "IskAlignRight": range.Align(Office.MsoAlignCmd.msoAlignRights, toSlide); break;
                    case "IskAlignTop": range.Align(Office.MsoAlignCmd.msoAlignTops, toSlide); break;
                    case "IskAlignMiddle": range.Align(Office.MsoAlignCmd.msoAlignMiddles, toSlide); break;
                    case "IskAlignBottom": range.Align(Office.MsoAlignCmd.msoAlignBottoms, toSlide); break;
                    case "IskDistributeH": range.Distribute(Office.MsoDistributeCmd.msoDistributeHorizontally, count < 3 ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse); break;
                    case "IskDistributeV": range.Distribute(Office.MsoDistributeCmd.msoDistributeVertically, count < 3 ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse); break;
                    case "IskSameHeight": SameSize(range, width: false, height: true); break;
                    case "IskSameWidth": SameSize(range, width: true, height: false); break;
                    case "IskSameSize": SameSize(range, width: true, height: true); break;
                    case "IskSwap":
                        if (count != 2) break;
                        PowerPoint.Shape a = range[1], b = range[2];
                        float ax = a.Left + a.Width / 2, ay = a.Top + a.Height / 2, bx = b.Left + b.Width / 2, by = b.Top + b.Height / 2;
                        a.Left = bx - a.Width / 2; a.Top = by - a.Height / 2;
                        b.Left = ax - b.Width / 2; b.Top = ay - b.Height / 2;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("OnLayout " + control.Id, ex);
            }
        }

        /// <summary>Gives every shape of the range the width and/or height of the first one (about each shape's centre).</summary>
        private static void SameSize(PowerPoint.ShapeRange range, bool width, bool height)
        {
            if (range.Count < 2) return;
            var first = range[1];
            for (var i = 2; i <= range.Count; i++)
            {
                var s = range[i];
                var lockRatio = s.LockAspectRatio;
                s.LockAspectRatio = Office.MsoTriState.msoFalse;
                float cx = s.Left + s.Width / 2, cy = s.Top + s.Height / 2;
                if (width) s.Width = first.Width;
                if (height) s.Height = first.Height;
                s.Left = cx - s.Width / 2;
                s.Top = cy - s.Height / 2;
                s.LockAspectRatio = lockRatio;
            }
        }

        /// <summary>Own layout pictograms (same height/width/size, swap) in the Office theme ink.</summary>
        public object? GetLayoutImage(Office.IRibbonControl control)
        {
            try
            {
                var size = (int)Math.Round(16 * DeviceScale());
                using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var k = size / 16f;
                    g.ScaleTransform(k, k);
                    var ink = OfficeTheme.IsDark() ? Color.FromArgb(230, 230, 230) : Color.FromArgb(40, 40, 40);
                    using var pen = new Pen(ink, 1.3f);
                    using var accent = new Pen(Color.FromArgb(31, 95, 191), 1.3f);
                    switch (control.Id)
                    {
                        case "IskSameHeight":
                            g.DrawRectangle(pen, 2, 3, 5, 10); g.DrawRectangle(pen, 9, 6, 5, 7); g.DrawLine(accent, 9, 3, 14, 3); break;
                        case "IskSameWidth":
                            g.DrawRectangle(pen, 3, 2, 10, 5); g.DrawRectangle(pen, 3, 9, 7, 5); g.DrawLine(accent, 13, 9, 13, 14); break;
                        case "IskSameSize":
                            g.DrawRectangle(pen, 1.5f, 2, 6, 6); g.DrawRectangle(pen, 8.5f, 8, 6, 6); g.DrawLine(accent, 8.5f, 2, 14.5f, 2); g.DrawLine(accent, 14.5f, 2, 14.5f, 6); break;
                        case "IskSwap":
                            g.DrawRectangle(pen, 1.5f, 5, 4, 6); g.DrawRectangle(pen, 10.5f, 5, 4, 6);
                            g.DrawLine(accent, 6.5f, 6, 9.5f, 6); g.DrawLine(accent, 8.5f, 5, 9.5f, 6);
                            g.DrawLine(accent, 9.5f, 10, 6.5f, 10); g.DrawLine(accent, 7.5f, 11, 6.5f, 10); break;
                    }
                }
                return PictureConverter.ToPicture(bmp);
            }
            catch (Exception ex) { Log.Error("GetLayoutImage", ex); return null; }
        }

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        /// <summary>
        /// Ribbon galleries that show several categories, so the "Annotate" group needs one column less: hatching with the
        /// basic shapes, patent marks and patent drawing with lines and arrows (secondary category → primary category).
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string> MergedInto = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ann.hatching"] = "basic",
            ["ann.patent"] = "ann.lines",
            ["patent.drawing"] = "ann.lines",
        };

        /// <summary>The category and the categories merged into it, in order.</summary>
        private List<LibraryCategory> GalleryCategories(LibraryCategory primary)
            => new[] { primary }.Concat(_categories.Where(c => MergedInto.TryGetValue(c.Id, out var p) && p == primary.Id)).ToList();

        private string GalleryLabel(LibraryCategory primary) => string.Join(" · ", GalleryCategories(primary).Select(c => c.Name.Get(_lang)));

        private LibraryCategory? CategoryOf(Office.IRibbonControl control)
        {
            var idx = control.Id.LastIndexOf('_');
            return idx >= 0 && int.TryParse(control.Id.Substring(idx + 1), out var n) && n < _categories.Count ? _categories[n] : null;
        }

        private List<LibraryItem> ItemsOf(Office.IRibbonControl control)
        {
            var cat = CategoryOf(control);
            if (cat is null || _registry is null) return new List<LibraryItem>();
            var ids = GalleryCategories(cat).Select(c => c.Id).ToList();
            return _registry.Library.Where(i => ids.Contains(i.Category)).OrderBy(i => ids.IndexOf(i.Category)).ToList();
        }

        public object? GetCategoryImage(Office.IRibbonControl control)
        {
            try
            {
                var item = ItemsOf(control).FirstOrDefault();
                return item is null ? null : PictureConverter.ToPicture(Thumbnail(item, 32));
            }
            catch (Exception ex)
            {
                Log.Error("GetCategoryImage", ex);
                return null;
            }
        }

        /// <summary>Icon of a module group when the ribbon collapses it: its first library item.</summary>
        /// <summary>Ribbon image of the about button and the collapsed tools group: the InventaSketch logo.</summary>
        public object? GetLogoImage(Office.IRibbonControl control)
        {
            try { return PictureConverter.ToPicture(Branding.Image(32)); }
            catch (Exception ex) { Log.Error("GetLogoImage", ex); return null; }
        }

        /// <summary>Ribbon image of the anchor toggle: the abstract "connect" pictogram in the Office theme ink.</summary>
        public object? GetAnchorImage(Office.IRibbonControl control)
        {
            try
            {
                var size = (int)Math.Round(16 * DeviceScale());
                using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    OnSlide.IconPainter.Draw(g, IconIds.Connect, new RectangleF(0, 0, size, size), OfficeTheme.IsDark() ? Color.FromArgb(230, 230, 230) : Color.FromArgb(40, 40, 40));
                return PictureConverter.ToPicture(bmp);
            }
            catch (Exception ex) { Log.Error("GetAnchorImage", ex); return null; }
        }

        public object? GetGroupImage(Office.IRibbonControl control)
        {
            try
            {
                var idx = control.Id.LastIndexOf('_');
                if (_registry is null || idx < 0 || !int.TryParse(control.Id.Substring(idx + 1), out var n)) return null;
                var groups = RibbonGroups();
                if (n >= groups.Count) return null;
                var cat = _categories.FirstOrDefault(c => c.ModuleId == groups[n].Modules[0].Id);
                var item = cat is null ? null : _registry.Library.FirstOrDefault(i => i.Category == cat.Id);
                return item is null ? null : PictureConverter.ToPicture(Thumbnail(item, 32));
            }
            catch (Exception ex)
            {
                Log.Error("GetGroupImage", ex);
                return null;
            }
        }

        public int GetItemCount(Office.IRibbonControl control) => ItemsOf(control).Count;

        public string GetGalleryItemId(Office.IRibbonControl control, int index)
        {
            var items = ItemsOf(control);
            return index >= 0 && index < items.Count ? items[index].Id : "";
        }

        public string GetItemScreentip(Office.IRibbonControl control, int index)
        {
            var items = ItemsOf(control);
            return index >= 0 && index < items.Count ? items[index].Name.Get(_lang) : "";
        }

        public object? GetGalleryItemImage(Office.IRibbonControl control, int index)
        {
            try
            {
                var items = ItemsOf(control);
                return index >= 0 && index < items.Count ? PictureConverter.ToPicture(Thumbnail(items[index], 40)) : null;
            }
            catch (Exception ex)
            {
                Log.Error("GetGalleryItemImage", ex);
                return null;
            }
        }

        public void OnGalleryAction(Office.IRibbonControl control, string selectedId, int selectedIndex)
        {
            try
            {
                var item = _registry?.Item(selectedId);
                if (item is null) return;
                if (_onSlide?.BeginPlacement(item) == true) return;
                InsertItem(item.Id);
            }
            catch (Exception ex)
            {
                Log.Error("OnGalleryAction " + selectedId, ex);
                MessageBox.Show(T("The element could not be inserted. Details are in the log: ", "Das Element konnte nicht eingefügt werden. Details im Protokoll: ") + Log.File,
                    "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public bool GetTogglePressed(Office.IRibbonControl control) => control.Id switch
        {
            "IskSnap" => AddInSettings.Snap,
            "IskBar" => AddInSettings.Bar,
            "IskAnchor" => AddInSettings.Anchor,
            _ => false,
        };

        public void OnToggle(Office.IRibbonControl control, bool pressed)
        {
            if (control.Id == "IskSnap") AddInSettings.Snap = pressed;
            else if (control.Id == "IskBar") AddInSettings.Bar = pressed;
            else if (control.Id == "IskAnchor") AddInSettings.Anchor = pressed;
        }

        /// <summary>Thumbnail of a library item (cached per item, size and theme); ink follows the Office theme.</summary>
        private Bitmap Thumbnail(LibraryItem item, int logicalSize)
        {
            var dark = OfficeTheme.IsDark();
            var size = (int)Math.Round(logicalSize * DeviceScale());
            var key = item.Id + "|" + size + "|" + (dark ? "d" : "l");
            if (_images.TryGetValue(key, out var cached)) return cached;
            var registry = _registry!;
            var doc = SketchDocument.Create();
            var obj = registry.Create(item, new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, obj);
            var prims = registry.Render(obj, new RenderContext(doc, _lang));
            var ink = dark ? Color.FromArgb(230, 230, 230) : Color.FromArgb(40, 40, 40);
            var bmp = ThumbnailRenderer.Render(prims, size, ink, Color.Transparent);
            _images[key] = bmp;
            return bmp;
        }

        /// <summary>Inserts a library item as native shapes in the middle of the current slide (one undo step).</summary>
        internal PowerPoint.Shape? InsertItem(string itemId)
        {
            if (_app is null || _registry is null) return null;
            var item = _registry.Item(itemId) ?? throw new KeyNotFoundException("Unknown library item " + itemId);
            var slide = CurrentSlide(_app);
            if (slide is null)
            {
                MessageBox.Show(T("Please select a slide first.", "Bitte zuerst eine Folie auswählen."), "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            var setup = slide.Parent is PowerPoint.Presentation p ? p.PageSetup : _app.ActivePresentation.PageSetup;
            var pageMm = new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm);
            var doc = SketchDocument.Create(pageMm);
            var obj = _registry.Create(item, new Vec2(pageMm.W / 2, pageMm.H / 2));
            doc = Edits.Add(doc, doc.Pages[0].Id, obj);
            _app.StartNewUndoEntry();
            var last = OnSlide.SketchUnits.Draw(slide, _registry, doc, _lang);
            last?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Inserted {item.Id} ({obj.Type}) as {(last?.Type == Office.MsoShapeType.msoGroup ? "group" : "shape")}");
            return last;
        }

        internal bool BeginPlacement(string itemId)
        {
            var item = _registry?.Item(itemId);
            return item is not null && _onSlide?.BeginPlacement(item) == true;
        }

        internal System.Collections.Generic.IEnumerable<string> LibraryItemIds() => _registry?.Library.Select(i => i.Id) ?? Enumerable.Empty<string>();

        private static PowerPoint.Slide? CurrentSlide(PowerPoint.Application app)
        {
            try
            {
                if (app.Windows.Count == 0) return null;
                return app.ActiveWindow.View.Slide as PowerPoint.Slide;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Recolours the InventaSketch shapes of the current slide to black/white (patent style).</summary>
        /// <summary>
        /// Switches the InventaSketch elements of the slide to black and white (patent style) or back: every unit's model
        /// gets <see cref="DocumentSettings.Monochrome"/> and is re-rendered, so all colours (fills, text, highlighters)
        /// are mapped by the engine, stay black and white after later edits and come back when switched off.
        /// </summary>
        public void OnMonochrome(Office.IRibbonControl control)
        {
            try
            {
                if (_app is null || _registry is null || CurrentSlide(_app) is not PowerPoint.Slide slide) return;
                var units = OnSlide.SketchUnits.All(slide);
                if (units.Count == 0) return;
                var on = units.Any(u => !u.Doc.Settings.Monochrome);
                _app.StartNewUndoEntry();
                foreach (var u in units)
                {
                    if (u.Doc.Settings.Monochrome == on) continue;
                    var doc = u.Doc with { Settings = u.Doc.Settings with { Monochrome = on } };
                    OnSlide.SketchUnits.Replace(slide, u, doc, _registry, _lang);
                }
                Log.Info($"Black and white {(on ? "on" : "off")} for {units.Count} unit(s)");
            }
            catch (Exception ex)
            {
                Log.Error("OnMonochrome", ex);
            }
        }
        // ------------------------------------------------------------------ sidebar editor (custom task pane)

        /// <summary>Shows or hides the sidebar editor (ribbon toggle and automation).</summary>
        internal bool ShowEditor(bool visible)
        {
            if (_onSlide is null) return false;
            return _onSlide.OnUi(() =>
            {
                OnEditorToggle(null!, visible);
                _ribbon?.InvalidateControl("IskEditor");
                return _pane?.Visible == true;
            });
        }

        /// <summary>Runs a bar/pie command on the selected unit (automation).</summary>
        internal bool RunUnitCommand(string commandId) => _onSlide?.RunCommand(commandId) ?? false;

        internal bool ActivateTab()
        {
            if (_onSlide is null || _ribbon is null) return false;
            return _onSlide.OnUi(() => { _ribbon.ActivateTab("IskTab"); return true; });
        }

        internal bool RunSidebarScript(string script)
        {
            if (_onSlide is null) return false;
            return _onSlide.OnUi(() =>
            {
                var pane = Sidebar.TaskPaneControl.Current;
                if (pane is null) return false;
                pane.Run(script);
                return true;
            });
        }

        private static double DeviceScale()
        {
            try
            {
                using var g = Graphics.FromHwnd(IntPtr.Zero);
                return Math.Max(1, g.DpiX / 96.0);
            }
            catch
            {
                return 1;
            }
        }

        public void CTPFactoryAvailable(Office.ICTPFactory CTPFactoryInst) => _ctpFactory = CTPFactoryInst;

        public bool GetEditorPressed(Office.IRibbonControl control)
        {
            try { return _pane?.Visible == true; }
            catch { return false; }
        }

        public void OnEditorToggle(Office.IRibbonControl control, bool pressed)
        {
            try
            {
                if (_pane is null)
                {
                    if (!pressed || _ctpFactory is null || _app is null) return;
                    var setup = _app.Presentations.Count > 0 ? _app.ActivePresentation.PageSetup : null;
                    var w = setup is null ? 338.667 : setup.SlideWidth / Geo.PtPerMm;
                    var h = setup is null ? 190.5 : setup.SlideHeight / Geo.PtPerMm;
                    Sidebar.TaskPaneControl.Query = string.Format(CultureInfo.InvariantCulture, "?lang={0}&theme={1}&w={2:0.###}&h={3:0.###}",
                        _lang == Lang.De ? "de" : "en", OfficeTheme.IsDark() ? "dark" : "light", w, h);
                    _pane = _ctpFactory.CreateCTP("InventaSketch.AddIn.TaskPane", "InventaSketch", Type.Missing);
                    _pane.DockPosition = Office.MsoCTPDockPosition.msoCTPDockPositionRight;
                    // CTP width is in device pixels: scale the 400 logical px the editor needs (compact layout).
                    _pane.Width = (int)Math.Round(400 * DeviceScale());
                    _pane.VisibleStateChange += p => _ribbon?.InvalidateControl("IskEditor");
                    if (_pane.ContentControl is Sidebar.TaskPaneControl content) _bridge?.Attach(content);
                    else Log.Info("Task pane content is " + (_pane.ContentControl?.GetType().FullName ?? "null"));
                }
                _pane.Visible = pressed;
            }
            catch (Exception ex)
            {
                Log.Error("OnEditorToggle", ex);
                MessageBox.Show(T("The editor could not be opened. Details are in the log: ", "Der Editor konnte nicht geöffnet werden. Details im Protokoll: ") + Log.File,
                    "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void OnAbout(Office.IRibbonControl control)
        {
            try { AboutDialog.Show(null, _lang == Lang.De); }
            catch (Exception ex) { Log.Error("OnAbout", ex); }
        }
    }

    /// <summary>
    /// Converts a bitmap to IPictureDisp for ribbon images as a 32-bit top-down DIB with premultiplied alpha
    /// (PICTYPE_BITMAP), so thumbnails stay transparent on light and dark Office themes (AxHost's converter drops the
    /// alpha channel and leaves a white box). Ported from Slidemax (Brand.PictureConverter) with the copyright holder's
    /// approval. The picture owns the bitmap handle.
    /// </summary>
    internal static class PictureConverter
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PictDesc
        {
            public int Size;
            public int Type;
            public IntPtr Handle;
            public IntPtr Palette;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public int Size;
            public int Width;
            public int Height;
            public short Planes;
            public short BitCount;
            public int Compression;
            public int SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public int ClrUsed;
            public int ClrImportant;
        }

        [System.Runtime.InteropServices.DllImport("oleaut32.dll")]
        private static extern int OleCreatePictureIndirect(ref PictDesc desc, ref Guid iid, bool own, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IDispatch)] out object picture);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        private static Guid _pictureDisp = new("7BF80981-BF32-101A-8BBB-00AA00300CAB");

        public static object ToPicture(Image image)
        {
            using var bmp = new Bitmap(image);
            int w = bmp.Width, h = bmp.Height;
            // Top-down (negative height), BI_RGB, pixels premultiplied as GDI+ delivers them in PArgb.
            var header = new BitmapInfoHeader { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(BitmapInfoHeader)), Width = w, Height = -h, Planes = 1, BitCount = 32 };
            var hbitmap = CreateDIBSection(IntPtr.Zero, ref header, 0, out var bits, IntPtr.Zero, 0);
            if (hbitmap == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed");
            try
            {
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    var row = new byte[w * 4];
                    for (var y = 0; y < h; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                        System.Runtime.InteropServices.Marshal.Copy(row, 0, bits + y * w * 4, row.Length);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                var desc = new PictDesc { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(PictDesc)), Type = 1, Handle = hbitmap };
                System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(OleCreatePictureIndirect(ref desc, ref _pictureDisp, true, out var picture));
                return picture;
            }
            catch
            {
                DeleteObject(hbitmap);
                throw;
            }
        }
    }}
