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
    public sealed class Connect : IDTExtensibility2, Office.IRibbonExtensibility
    {
        private PowerPoint.Application? _app;
        private Office.IRibbonUI? _ribbon;
        private Registry? _registry;
        private Lang _lang = Lang.En;
        private List<LibraryCategory> _categories = new();
        private readonly Dictionary<string, Bitmap> _images = new();

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

        // ------------------------------------------------------------------ Ribbon

        public string GetCustomUI(string ribbonId)
        {
            using var stream = typeof(Connect).Assembly.GetManifestResourceStream("InventaSketch.AddIn.Ribbon.xml")!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        public void OnRibbonLoad(Office.IRibbonUI ribbon) => _ribbon = ribbon;

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        public string GetLabel(Office.IRibbonControl control) => control.Id switch
        {
            "IskTab" => "InventaSketch",
            "IskLibrary" => T("Library", "Bibliothek"),
            "IskMore" => T("More elements", "Weitere Elemente"),
            "IskTools" => T("Tools", "Werkzeuge"),
            "IskMonochrome" => T("Black & white", "Schwarz-Weiß"),
            "IskAbout" => T("About", "Info"),
            _ => control.Id,
        };

        private LibraryCategory? CategoryOf(Office.IRibbonControl control)
        {
            var idx = control.Id.LastIndexOf('_');
            return idx >= 0 && int.TryParse(control.Id.Substring(idx + 1), out var n) && n < _categories.Count ? _categories[n] : null;
        }

        public bool GetCategoryVisible(Office.IRibbonControl control) => CategoryOf(control) is not null;

        public string GetCategoryLabel(Office.IRibbonControl control) => CategoryOf(control)?.Name.Get(_lang) ?? "";

        public object? GetCategoryImage(Office.IRibbonControl control)
        {
            try
            {
                var cat = CategoryOf(control);
                var item = cat is null ? null : _registry?.Library.FirstOrDefault(i => i.Category == cat.Id);
                return item is null ? null : PictureConverter.ToPicture(Thumbnail(item, 32));
            }
            catch (Exception ex)
            {
                Log.Error("GetCategoryImage", ex);
                return null;
            }
        }

        public object? GetItemImage(Office.IRibbonControl control)
        {
            try
            {
                var item = _registry?.Item(control.Tag);
                return item is null ? null : PictureConverter.ToPicture(Thumbnail(item, 32));
            }
            catch (Exception ex)
            {
                Log.Error("GetItemImage", ex);
                return null;
            }
        }

        public string GetCategoryContent(Office.IRibbonControl control)
        {
            var sb = new StringBuilder("<menu xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\" itemSize=\"large\">");
            try
            {
                var cat = CategoryOf(control);
                if (cat is not null && _registry is not null)
                {
                    var n = 0;
                    foreach (var item in _registry.Library.Where(i => i.Category == cat.Id))
                    {
                        sb.Append("<button id=\"IskItem_").Append(control.Id).Append('_').Append(n++.ToString(CultureInfo.InvariantCulture))
                          .Append("\" label=\"").Append(SecurityElement.Escape(item.Name.Get(_lang)))
                          .Append("\" tag=\"").Append(SecurityElement.Escape(item.Id))
                          .Append("\" getImage=\"GetItemImage\" onAction=\"OnInsertItem\" />");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("GetCategoryContent", ex);
            }
            return sb.Append("</menu>").ToString();
        }

        private Bitmap Thumbnail(LibraryItem item, int size)
        {
            if (_images.TryGetValue(item.Id, out var cached)) return cached;
            var registry = _registry!;
            var doc = SketchDocument.Create();
            var obj = registry.Create(item, new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, obj);
            var prims = registry.Render(obj, new RenderContext(doc, _lang));
            var bmp = ThumbnailRenderer.Render(prims, size, Color.FromArgb(40, 40, 40), Color.Transparent);
            _images[item.Id] = bmp;
            return bmp;
        }

        public void OnInsertItem(Office.IRibbonControl control)
        {
            try
            {
                InsertItem(control.Tag);
            }
            catch (Exception ex)
            {
                Log.Error("OnInsertItem " + control.Tag, ex);
                MessageBox.Show(T("The element could not be inserted. Details are in the log: ", "Das Element konnte nicht eingefügt werden. Details im Protokoll: ") + Log.File,
                    "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
            var session = new EditorSession("com", _registry, doc, _lang);
            var plan = DrawPlanBuilder.Build(session, new[] { obj.Id }, DrawCapabilities.Full);
            _app.StartNewUndoEntry();
            var renderer = new ComRenderer(slide);
            PowerPoint.Shape? last = null;
            foreach (var unit in plan.Units)
            {
                last = renderer.DrawUnit(unit);
                if (unit.Notes.Count > 0) Log.Info($"{unit.Type}: {string.Join("; ", unit.Notes)}");
            }
            last?.Select(Office.MsoTriState.msoTrue);
            Log.Info($"Inserted {item.Id} ({obj.Type}) as {(last?.Type == Office.MsoShapeType.msoGroup ? "group" : "shape")}");
            return last;
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
