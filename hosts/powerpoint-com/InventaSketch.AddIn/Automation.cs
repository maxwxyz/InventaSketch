using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// Automation object for VBA, scripts and integration tests:
    /// <c>Application.COMAddIns("InventaSketch.AddIn.Connect").Object</c>.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public sealed class Automation
    {
        private readonly Connect _connect;

        internal Automation(Connect connect) => _connect = connect;

        /// <summary>Inserts a library item on the current slide; returns the new shape name or "" on failure.</summary>
        public string Insert(string itemId)
        {
            try
            {
                return _connect.InsertItem(itemId)?.Name ?? "";
            }
            catch (Exception ex)
            {
                Log.Error("Automation.Insert " + itemId, ex);
                return "";
            }
        }

        /// <summary>Starts insert-at-pointer for a library item (as the ribbon does); false if no slide is visible.</summary>
        public bool Place(string itemId)
        {
            try
            {
                return _connect.BeginPlacement(itemId);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.Place " + itemId, ex);
                return false;
            }
        }

        /// <summary>Shows or hides the sidebar editor; returns whether it is visible.</summary>
        public bool Editor(bool visible)
        {
            try
            {
                return _connect.ShowEditor(visible);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.Editor", ex);
                return false;
            }
        }

        /// <summary>Runs a script in the sidebar editor (tests); false if the sidebar is not open.</summary>
        public bool SidebarScript(string script)
        {
            try
            {
                return _connect.RunSidebarScript(script);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.SidebarScript", ex);
                return false;
            }
        }

        /// <summary>Activates the InventaSketch ribbon tab.</summary>
        public bool ShowTab()
        {
            try
            {
                return _connect.ActivateTab();
            }
            catch (Exception ex)
            {
                Log.Error("Automation.ShowTab", ex);
                return false;
            }
        }

        /// <summary>
        /// Runs a floating-bar or pie-menu command on the selected unit ("dup", "rot90", "flipH", "view:&lt;id&gt;",
        /// "act:&lt;action id&gt;", "anim" …); false if no InventaSketch unit is selected. Runs asynchronously on the UI thread.
        /// </summary>
        public bool Command(string commandId)
        {
            try
            {
                return _connect.RunUnitCommand(commandId);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.Command " + commandId, ex);
                return false;
            }
        }

        /// <summary>Screen rectangle "x,y,w,h" (device pixels) of a shape on the current slide, for UI tests; "" if not visible.</summary>
        public string ScreenRect(string shapeName)
        {
            try
            {
                return _connect.ScreenRectOf(shapeName);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.ScreenRect " + shapeName, ex);
                return "";
            }
        }

        /// <summary>Sets the ISO 128 line group of the current slide (as the ribbon menu "Line weights"); false on failure.</summary>
        public bool SetLineGroup(double group)
        {
            try
            {
                return _connect.ApplyLineGroup(group);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.SetLineGroup", ex);
                return false;
            }
        }

        /// <summary>Sets the accent colour of the current slide (preset id, #rrggbb, "" = original colours); false on failure.</summary>
        public bool SetAccent(string value)
        {
            try
            {
                return _connect.ApplyAccent(value);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.SetAccent", ex);
                return false;
            }
        }

        /// <summary>True once PowerPoint has loaded the InventaSketch ribbon tab (false: the ribbon XML was rejected).</summary>
        public bool RibbonLoaded() => _connect.RibbonLoaded;

        /// <summary>Button ids of the floating bar as last shown, comma-separated (UI tests; no log needed).</summary>
        public string BarIds() => _connect.LastBarIds();

        /// <summary>
        /// Build ports of the InventaSketch unit shapeName (UI tests): one line per free port "id|kind|item ids offered"
        /// (comma separated, best first); "" if none.
        /// </summary>
        public string BuildPorts(string shapeName)
        {
            try
            {
                return _connect.BuildPortsOf(shapeName);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.BuildPorts " + shapeName, ex);
                return "";
            }
        }

        /// <summary>Clean slides without the dialog (UI tests): scope "selection"|"slide"|"presentation", mode "normalised"|"randomised"|"none"; returns the report.</summary>
        public string CleanSlides(string scope, string mode, int seed)
        {
            try
            {
                return _connect.CleanSlidesAuto(scope, mode, seed);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.CleanSlides", ex);
                return "error: " + ex.Message;
            }
        }

        /// <summary>Chart from a picture file onto the current slide (UI tests); returns the note shown to the user.</summary>
        public string ChartFromPicture(string path)
        {
            try
            {
                return _connect.ChartFromPictureFile(path) ?? "";
            }
            catch (Exception ex)
            {
                Log.Error("Automation.ChartFromPicture " + path, ex);
                return "error: " + ex.Message;
            }
        }

        /// <summary>Part key of the InventaSketch unit shapeName at the slide point (xPt, yPt) in points (charts v2), "" if none.</summary>
        public string PartAt(string shapeName, double xPt, double yPt)
        {
            try
            {
                return _connect.PartAtOf(shapeName, xPt, yPt);
            }
            catch (Exception ex)
            {
                Log.Error("Automation.PartAt " + shapeName, ex);
                return "";
            }
        }

        /// <summary>Visible InventaSketch handles of the selected element as last drawn: "id@x,y,r;…" (device pixels).</summary>
        public string Handles() => _connect.LastHandles();

        /// <summary>Handles shown at least half right now (progressive disclosure): "id;id;…".</summary>
        public string ShownHandles() => _connect.LastShownHandles();

        /// <summary>Every command of the selected element (bar and pie, all submenus), comma-separated.</summary>
        public string CommandIds() => _connect.LastCommandIds();

        /// <summary>Pie menu entries as last shown: "id	label[	(off)]" per line.</summary>
        public string PieItems() => _connect.LastPieItems();

        /// <summary>Drag handles (grips) of a shape's element: "key:x,y;…" in slide points.</summary>
        public string DragHandlesOf(string shapeName)
        {
            try { return _connect.DragHandlesOf(shapeName); }
            catch (Exception ex) { Log.Error("Automation.DragHandlesOf " + shapeName, ex); return ""; }
        }

        /// <summary>Imports plotting or drawing code (matplotlib, pgfplots, TikZ) onto the current slide; returns the report.</summary>
        public string ImportCode(string text)
        {
            try { return _connect.ImportCode(text); }
            catch (Exception ex) { Log.Error("Automation.ImportCode", ex); return "error: " + ex.Message; }
        }

        /// <summary>The ribbon's "All caps" toggle on the current selection: sets it and returns the pressed state read back.</summary>
        public bool SetAllCaps(bool on)
        {
            try
            {
                _connect.OnAllCapsToggle(null!, on);
                return _connect.GetAllCapsPressed(null!);
            }
            catch (Exception ex) { Log.Error("Automation.SetAllCaps", ex); return false; }
        }

        /// <summary>Key of the selected chart part ("" = the whole element or no part selection).</summary>
        public string SelectedPart()
        {
            try { return _connect.SelectedPartKey(); }
            catch (Exception ex) { Log.Error("Automation.SelectedPart", ex); return ""; }
        }

        /// <summary>Screen rectangle "x,y,w,h" (device pixels) of the floating bar as last shown; "" if none yet.</summary>
        public string BarRect() => _connect.LastBarRect();

        /// <summary>Imports a data file's text (CSV/TSV, GeoJSON, GPX) as "Import data …" does; "" on success, else the message.</summary>
        public string ImportData(string fileName, string text)
        {
            try
            {
                return _connect.ImportData(fileName, text) ?? "";
            }
            catch (Exception ex)
            {
                Log.Error("Automation.ImportData", ex);
                return ex.Message;
            }
        }

        /// <summary>Compatibility check of the presentation: one line per finding "slide|severity|rule|shape name" (UI tests).</summary>
        public string PatentCheck(string profileId)
        {
            try
            {
                return string.Join("\n", _connect.RunPatentCheck(profileId).Select(i => i.Slide + "|" + i.Finding.Severity + "|" + i.Finding.RuleId + "|" + (i.ShapeName ?? "")));
            }
            catch (Exception ex)
            {
                Log.Error("Automation.PatentCheck", ex);
                return "error: " + ex.Message;
            }
        }

        /// <summary>Opens (true) or closes (false) the compatibility check window; true if it is open.</summary>
        public bool ShowPatentCheck(bool visible)
        {
            try { return _connect.ShowPatentCheck(visible); }
            catch (Exception ex) { Log.Error("Automation.ShowPatentCheck", ex); return false; }
        }

        /// <summary>Compares the current slide with the previous one (or removes the marks); "" on success, else the message.</summary>
        public string CompareWithPrevious()
        {
            try { return _connect.CompareWithPrevious() ?? ""; }
            catch (Exception ex) { Log.Error("Automation.CompareWithPrevious", ex); return ex.Message; }
        }

        /// <summary>All library item ids, separated by newlines.</summary>
        public string LibraryItems() => string.Join("\n", _connect.LibraryItemIds());

        /// <summary>The library for UI reviews: "id\tcategory\ttype\tmodule\tname" per line.</summary>
        public string LibraryCatalog() => string.Join("\n", _connect.LibraryCatalog());

        public string Version() => typeof(Automation).Assembly.GetName().Version.ToString();
    }
}
