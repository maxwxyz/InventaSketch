using System;
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
