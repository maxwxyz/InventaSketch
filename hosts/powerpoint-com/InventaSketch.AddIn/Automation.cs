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

        /// <summary>All library item ids, separated by newlines.</summary>
        public string LibraryItems() => string.Join("\n", _connect.LibraryItemIds());

        public string Version() => typeof(Automation).Assembly.GetName().Version.ToString();
    }
}
