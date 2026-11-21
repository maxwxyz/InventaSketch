using System;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// The ribbon XML of the last start, per language, so PowerPoint gets the ribbon at startup without waiting for the
    /// module library (cold JIT of all modules takes about 0.7 s, and Office disables add-ins that start slowly). The
    /// first line holds a key of the installed build (the add-in's module version id plus name, size and time of every
    /// InventaSketch assembly next to it); a cache of another build is ignored. install.ps1 warms it right after
    /// registering, the add-in refreshes it once the library is ready.
    /// </summary>
    internal static class RibbonCache
    {
        private const string Prefix = "<!-- isk-ribbon ";

        internal static string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventaSketch");

        internal static string FileOf(Lang lang) => Path.Combine(Folder, "ribbon-" + (lang == Lang.De ? "de" : "en") + ".xml");

        /// <summary>Key of the installed build: changes with every build of the add-in or of a module.</summary>
        internal static string BuildKey()
        {
            var asm = typeof(RibbonCache).Assembly;
            var sb = new StringBuilder(asm.ManifestModule.ModuleVersionId.ToString("N"));
            var dir = Path.GetDirectoryName(asm.Location);
            if (dir is not null)
                foreach (var f in Directory.GetFiles(dir, "InventaSketch*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var info = new FileInfo(f);
                    sb.Append('|').Append(info.Name).Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks);
                }
            return sb.ToString();
        }

        /// <summary>The cached ribbon of this build, or null.</summary>
        public static string? Load(Lang lang)
        {
            try
            {
                var file = FileOf(lang);
                if (!File.Exists(file)) return null;
                var text = File.ReadAllText(file, Encoding.UTF8);
                var nl = text.IndexOf('\n');
                if (nl < 0 || !text.StartsWith(Prefix, StringComparison.Ordinal)) return null;
                var key = text.Substring(Prefix.Length, nl - Prefix.Length).TrimEnd('\r').Replace(" -->", "");
                return key == BuildKey() ? text.Substring(nl + 1) : null;
            }
            catch (Exception ex)
            {
                Log.Error("Ribbon cache load", ex);
                return null;
            }
        }

        /// <summary>Writes the ribbon of this build unless an identical cache exists (or <paramref name="force"/>); true if written.</summary>
        public static bool Store(Lang lang, Func<string> ribbonXml, bool force = false)
        {
            try
            {
                var xml = ribbonXml();
                if (!force && Load(lang) == xml) return false;
                Directory.CreateDirectory(Folder);
                var file = FileOf(lang);
                var tmp = file + ".tmp";
                File.WriteAllText(tmp, Prefix + BuildKey() + " -->\n" + xml, new UTF8Encoding(false));
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Ribbon cache store", ex);
                return false;
            }
        }
    }
}
