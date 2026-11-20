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
        private Registry? _registry;
        private Lang _lang = Lang.En;
        private List<LibraryCategory> _categories = new();
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
                _registry = ModuleCatalog.CreateDefault();
                var used = new HashSet<string>(_registry.Library.Select(i => i.Category));
                _categories = _registry.Categories.Where(c => used.Contains(c.Id)).OrderBy(c => c.Order).ToList();
                if (addInInst is Office.COMAddIn comAddIn) comAddIn.Object = new Automation(this);
                _onSlide = new OnSlide.OnSlideController(_app, () => _registry!, () => _lang);
                _bridge = new Sidebar.SidebarBridge(_app, () => _registry!, () => _lang, BeginPlacement);
                _onSlide.UnitArmed += u => _bridge.UnitSelected(u);
                Log.Info($"Connected: InventaSketch {typeof(Connect).Assembly.GetName().Version}, PowerPoint {_app.Version}, "
                    + $"{(Environment.Is64BitProcess ? 64 : 32)} bit, CLR {Environment.Version}, language {_lang}, "
                    + $"{_registry.Library.Count} library items in {_categories.Count} categories, {watch.ElapsedMilliseconds} ms");
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

        public void OnStartupComplete(ref Array custom) { }

        public void OnBeginShutdown(ref Array custom) { }

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
                return RibbonXml();
            }
            catch (Exception ex)
            {
                Log.Error("GetCustomUI", ex);
                return "<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\"/>";
            }
        }

        private static string X(string s) => SecurityElement.Escape(s) ?? "";

        /// <summary>
        /// One group per module, one gallery per library category (thumbnail grid, tooltip = name), then the tools group.
        /// New modules and categories appear without changing this code.
        /// </summary>
        /// <summary>Modules with library categories in ribbon order: mechanics first, basic shapes last.</summary>
        private List<IModule> RibbonModules()
        {
            var order = new[] { "mechanical", "annotation", "patent" };
            return (_registry?.Modules ?? Array.Empty<IModule>())
                .Where(mod => _categories.Any(c => c.ModuleId == mod.Id))
                .OrderBy(mod => Array.IndexOf(order, mod.Id) is var i && i >= 0 ? i : order.Length + (mod.Id == "basic" ? 1 : 0))
                .ToList();
        }

        private string RibbonXml()
        {
            var sb = new StringBuilder();
            sb.Append("<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\" onLoad=\"OnRibbonLoad\"><ribbon><tabs>");
            sb.Append("<tab id=\"IskTab\" label=\"InventaSketch\">");
            sb.Append("<group id=\"IskTools\" imageMso=\"ControlsGallery\" label=\"").Append(X(T("Tools", "Werkzeuge"))).Append("\">");
            sb.Append("<toggleButton id=\"IskEditor\" label=\"").Append(X(T("Editor", "Editor"))).Append("\" size=\"large\" imageMso=\"ControlsGallery\" getPressed=\"GetEditorPressed\" onAction=\"OnEditorToggle\" screentip=\"")
              .Append(X(T("Sidebar editor", "Seitenleisten-Editor"))).Append("\" supertip=\"").Append(X(T("Library, stage and properties in a side panel; changes appear on the slide immediately.", "Bibliothek, Bühne und Eigenschaften in der Seitenleiste; Änderungen erscheinen sofort auf der Folie."))).Append("\" />");
            sb.Append("<toggleButton id=\"IskSnap\" label=\"").Append(X(T("Snap", "Einrasten"))).Append("\" imageMso=\"SnapToGrid\" getPressed=\"GetTogglePressed\" onAction=\"OnToggle\" supertip=\"")
              .Append(X(T("Magnetic snapping: gear mesh, axes, points and alignment. Hold Alt to move freely.", "Magnetisches Einrasten: Zahneingriff, Achsen, Punkte und Fluchtlinien. Alt gedrückt halten für freies Bewegen."))).Append("\" />");
            sb.Append("<toggleButton id=\"IskBar\" label=\"").Append(X(T("Context bar", "Kontextleiste"))).Append("\" imageMso=\"QuickStylesGallery\" getPressed=\"GetTogglePressed\" onAction=\"OnToggle\" supertip=\"")
              .Append(X(T("Floating bar with views and commands next to the selected element.", "Schwebende Leiste mit Ansichten und Befehlen neben dem ausgewählten Element."))).Append("\" />");
            sb.Append("<button id=\"IskMonochrome\" label=\"").Append(X(T("Black & white", "Schwarz-Weiß"))).Append("\" imageMso=\"PictureRecolorBlackAndWhite\" onAction=\"OnMonochrome\" supertip=\"")
              .Append(X(T("Recolour the InventaSketch elements of this slide in black and white (patent style).", "InventaSketch-Elemente dieser Folie schwarz-weiß umfärben (Patentstil)."))).Append("\" />");
            sb.Append("<button id=\"IskAbout\" label=\"").Append(X(T("About", "Info"))).Append("\" imageMso=\"Info\" onAction=\"OnAbout\" />");
            sb.Append("</group>");
            if (_registry is not null)
            {
                var m = 0;
                foreach (var module in RibbonModules())
                {
                    var cats = _categories.Select((c, i) => (c, i)).Where(x => x.c.ModuleId == module.Id).ToList();
                    if (cats.Count == 0) continue;
                    // Groups with many categories show icons only (name in the tooltip) so the tab fits on laptop screens.
                    var compact = cats.Count > 4;
                    sb.Append("<group id=\"IskMod_").Append(m++).Append("\" getImage=\"GetGroupImage\" label=\"").Append(X(module.Name.Get(_lang))).Append("\">");
                    foreach (var (c, i) in cats)
                    {
                        sb.Append("<gallery id=\"IskCat_").Append(i).Append("\" label=\"").Append(X(c.Name.Get(_lang)))
                          .Append("\" size=\"normal\" showLabel=\"").Append(compact ? "false" : "true").Append("\" getImage=\"GetCategoryImage\" columns=\"6\" itemWidth=\"40\" itemHeight=\"40\" showItemLabel=\"false\"")
                          .Append(" screentip=\"").Append(X(c.Name.Get(_lang))).Append("\" supertip=\"")
                          .Append(X(T("Pick an element, then click on the slide to place it (snaps to other elements). Esc cancels.",
                              "Element wählen, dann auf die Folie klicken (rastet an anderen Elementen ein). Esc bricht ab."))).Append('"')
                          .Append(" getItemCount=\"GetItemCount\" getItemImage=\"GetGalleryItemImage\" getItemScreentip=\"GetItemScreentip\"")
                          .Append(" getItemID=\"GetGalleryItemId\" onAction=\"OnGalleryAction\" />");
                    }
                    sb.Append("</group>");
                }
            }
            sb.Append("</tab></tabs></ribbon></customUI>");
            return sb.ToString();
        }

        public void OnRibbonLoad(Office.IRibbonUI ribbon) => _ribbon = ribbon;

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        private LibraryCategory? CategoryOf(Office.IRibbonControl control)
        {
            var idx = control.Id.LastIndexOf('_');
            return idx >= 0 && int.TryParse(control.Id.Substring(idx + 1), out var n) && n < _categories.Count ? _categories[n] : null;
        }

        private List<LibraryItem> ItemsOf(Office.IRibbonControl control)
        {
            var cat = CategoryOf(control);
            return cat is null || _registry is null ? new List<LibraryItem>() : _registry.Library.Where(i => i.Category == cat.Id).ToList();
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
        public object? GetGroupImage(Office.IRibbonControl control)
        {
            try
            {
                var idx = control.Id.LastIndexOf('_');
                if (_registry is null || idx < 0 || !int.TryParse(control.Id.Substring(idx + 1), out var n)) return null;
                var modules = RibbonModules();
                if (n >= modules.Count) return null;
                var cat = _categories.FirstOrDefault(c => c.ModuleId == modules[n].Id);
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
            _ => false,
        };

        public void OnToggle(Office.IRibbonControl control, bool pressed)
        {
            if (control.Id == "IskSnap") AddInSettings.Snap = pressed;
            else if (control.Id == "IskBar") AddInSettings.Bar = pressed;
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
        public void OnMonochrome(Office.IRibbonControl control)
        {
            try
            {
                if (_app is null || CurrentSlide(_app) is not PowerPoint.Slide slide) return;
                _app.StartNewUndoEntry();
                foreach (PowerPoint.Shape s in slide.Shapes)
                    if (!string.IsNullOrEmpty(s.Tags["ISK_ID"])) Monochrome(s);
            }
            catch (Exception ex)
            {
                Log.Error("OnMonochrome", ex);
            }
        }

        private static void Monochrome(PowerPoint.Shape s)
        {
            if (s.Type == Office.MsoShapeType.msoGroup)
            {
                foreach (PowerPoint.Shape c in s.GroupItems) Monochrome(c);
                return;
            }
            if (s.Line.Visible == Office.MsoTriState.msoTrue) s.Line.ForeColor.RGB = 0;
            if (s.Fill.Visible == Office.MsoTriState.msoTrue && s.Fill.Type == Office.MsoFillType.msoFillSolid)
            {
                var rgb = s.Fill.ForeColor.RGB;
                int r = rgb & 255, g = (rgb >> 8) & 255, b = (rgb >> 16) & 255;
                s.Fill.ForeColor.RGB = (0.2126 * r + 0.7152 * g + 0.0722 * b) < 90 ? 0 : 0xFFFFFF;
            }
            if (s.HasTextFrame == Office.MsoTriState.msoTrue) s.TextFrame.TextRange.Font.Color.RGB = 0;
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
            MessageBox.Show($"InventaSketch {typeof(Connect).Assembly.GetName().Version}\n{T("Licence", "Lizenz")}: Apache-2.0\nhttps://github.com/maxwxyz/InventaSketch\n\n{T("Log", "Protokoll")}: {Log.File}",
                "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>Converts a bitmap to IPictureDisp for ribbon images (AxHost exposes the protected converter).</summary>
    internal sealed class PictureConverter : AxHost
    {
        private PictureConverter() : base("") { }

        public static object ToPicture(Image image) => GetIPictureDispFromPicture(image);
    }
}
