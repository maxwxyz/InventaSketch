using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace InventaSketch.AddIn
{
    /// <summary>Per-user add-in settings in %LOCALAPPDATA%\InventaSketch\settings.json (ribbon toggles).</summary>
    internal static class AddInSettings
    {
        private sealed class Data
        {
            public bool Snap { get; set; } = true;
            public bool Bar { get; set; } = true;
            public bool Anchor { get; set; } = true;
            public List<string> HiddenModules { get; set; } = new();
        }

        private static Data _data = Load();

        private static string FilePath => Path.Combine(Log.Folder, "settings.json");

        /// <summary>Magnetic snapping on the slide (Alt still suspends it temporarily).</summary>
        public static bool Snap
        {
            get => _data.Snap;
            set { _data.Snap = value; Save(); }
        }

        /// <summary>Floating context bar next to the selection.</summary>
        public static bool Bar
        {
            get => _data.Bar;
            set { _data.Bar = value; Save(); }
        }

        /// <summary>Module ids hidden in the ribbon ("Show and hide modules").</summary>
        public static IReadOnlyCollection<string> HiddenModules
        {
            get => _data.HiddenModules ??= new List<string>();
            set { _data.HiddenModules = value.ToList(); Save(); }
        }

        /// <summary>
        /// Motion mode: dragging turns gear trains and linkages the way they can move instead of editing them. Not saved:
        /// PowerPoint always starts in edit mode.
        /// </summary>
        public static bool Motion { get; set; }

        /// <summary>Line ends snapped to another element stay attached to it and follow when it moves.</summary>
        public static bool Anchor
        {
            get => _data.Anchor;
            set { _data.Anchor = value; Save(); }
        }

        private static Data Load()
        {
            try
            {
                return File.Exists(FilePath) ? JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data() : new Data();
            }
            catch (Exception ex)
            {
                Log.Error("Settings load", ex);
                return new Data();
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Log.Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(_data));
            }
            catch (Exception ex)
            {
                Log.Error("Settings save", ex);
            }
        }
    }
}
