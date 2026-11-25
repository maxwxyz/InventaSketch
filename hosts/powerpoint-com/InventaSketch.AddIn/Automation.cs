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

        /// <summary>All library item ids, separated by newlines.</summary>
        public string LibraryItems() => string.Join("\n", _connect.LibraryItemIds());

        public string Version() => typeof(Automation).Assembly.GetName().Version.ToString();
    }
}
