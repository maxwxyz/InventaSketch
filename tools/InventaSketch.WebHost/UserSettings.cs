using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace InventaSketch.WebHost
{
    /// <summary>Per-user autostart via HKCU\...\Run (no administrator rights).</summary>
    public static class Autostart
    {
        public const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Value name; unchanged so that entries of earlier installs are still found and removed.</summary>
        public const string Name = "InventaSketch Web-Add-in";

        /// <summary>Command line of the entry: the installed program without arguments (folder App next to it).</summary>
        public static string CommandLine(string exe) => "\"" + exe + "\"";

        public static string? Read()
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Key))
                return k?.GetValue(Name) as string;
        }

        public static void Add(string exe)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Key))
                k.SetValue(Name, CommandLine(exe), RegistryValueKind.String);
        }

        public static void Remove()
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Key, true))
                k?.DeleteValue(Name, false);
        }

        /// <summary>Policy "Do not process the legacy run list" for the user (DisableCurrentUserRun).</summary>
        public static bool IsBlocked()
        {
            const string p = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
            foreach (RegistryKey hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using (RegistryKey? k = hive.OpenSubKey(p))
                {
                    if (k?.GetValue("DisableCurrentUserRun") is int v && v != 0) return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Web add-ins in Office for Windows: developer sideloading via HKCU\Software\Microsoft\Office\16.0\WEF\Developer
    /// (name = add-in id, value = manifest path, like office-addin-dev-settings). Office loads the manifests listed there
    /// on its next start.
    /// </summary>
    public static class OfficeAddins
    {
        public const string DeveloperKey = @"Software\Microsoft\Office\16.0\WEF\Developer";
        private const string PolicyKey = @"Software\Policies\Microsoft\Office\16.0\WEF";

        public static void Register(string addinId, string manifest)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(DeveloperKey))
            {
                // Older versions of office-addin-dev-settings used the path as the value name.
                k.DeleteValue(manifest, false);
                k.SetValue(addinId, manifest, RegistryValueKind.String);
            }
        }

        public static string? Value(string name)
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(DeveloperKey))
                return k?.GetValue(name) as string;
        }

        /// <summary>Names of the entries pointing to the manifest (value, or in the older form, name equal to the path).</summary>
        public static List<string> Entries(string manifest)
        {
            var names = new List<string>();
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(DeveloperKey))
            {
                if (k == null) return names;
                foreach (string name in k.GetValueNames())
                {
                    if (string.Equals(k.GetValue(name) as string, manifest, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, manifest, StringComparison.OrdinalIgnoreCase))
                        names.Add(name);
                }
            }
            return names;
        }

        public static void Unregister(IEnumerable<string> names)
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(DeveloperKey, true))
            {
                if (k == null) return;
                foreach (string name in names) k.DeleteValue(name, false);
            }
        }

        /// <summary>
        /// Policies for web add-ins (HKLM and HKCU under Software\Policies\Microsoft\Office\16.0\WEF, including subkeys).
        /// A value counts as blocking if it is non-zero, its name contains "Disable" or "Block" and it does not only concern
        /// the Store ("Omex", "Store"). Returns (blocking, other) as readable lines.
        /// </summary>
        public static (List<string> Blocking, List<string> Other) Policies()
        {
            var blocking = new List<string>();
            var other = new List<string>();
            foreach (var (hive, name) in new[] { (Registry.LocalMachine, "HKLM"), (Registry.CurrentUser, "HKCU") })
            {
                using (RegistryKey? k = hive.OpenSubKey(PolicyKey))
                {
                    if (k != null) Collect(k, name + @"\" + PolicyKey, blocking, other);
                }
            }
            return (blocking, other);
        }

        private static void Collect(RegistryKey k, string path, List<string> blocking, List<string> other)
        {
            foreach (string name in k.GetValueNames())
            {
                object? value = k.GetValue(name);
                string line = path + ": " + name + " = " + value;
                if (IsBlocking(name, value)) blocking.Add(line);
                else other.Add(line);
            }
            foreach (string subkey in k.GetSubKeyNames())
            {
                using (RegistryKey? s = k.OpenSubKey(subkey))
                {
                    if (s != null) Collect(s, path + @"\" + subkey, blocking, other);
                }
            }
        }

        /// <summary>Whether a policy value blocks web add-ins (see <see cref="Policies"/>).</summary>
        public static bool IsBlocking(string name, object? value)
        {
            string n = name.ToLowerInvariant();
            if (!(n.Contains("disable") || n.Contains("block"))) return false;
            if (n.Contains("omex") || n.Contains("store")) return false;
            return value is int i ? i != 0 : value is string s && s.Trim() != "" && s.Trim() != "0";
        }

        /// <summary>The COM add-in is registered in PowerPoint (then Office hides the web add-in, EquivalentAddins).</summary>
        public static bool ComAddinRegistered()
        {
            const string p = @"Software\Microsoft\Office\PowerPoint\Addins\InventaSketch.AddIn.Connect";
            foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using (RegistryKey? k = hive.OpenSubKey(p))
                {
                    if (k != null) return true;
                }
            }
            return false;
        }
    }
}
