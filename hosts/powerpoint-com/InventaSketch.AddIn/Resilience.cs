using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace InventaSketch.AddIn
{
    /// <summary>A DWORD value the add-in writes below HKEY_CURRENT_USER.</summary>
    internal sealed record RegistryWrite(string Key, string Name, int Value);

    /// <summary>
    /// Protection against automatic disabling by Office (ported from Slidemax, B80). Office measures the start time of
    /// add-ins, disables slow or crashed ones and resets their registration on the way. Counter-measures, as other Office
    /// add-ins use them, for the current user only: the entry in "DoNotDisableAddinList" (value 1), LoadBehavior back to 3
    /// when it was changed, and removal of a hard disable after a crash. Written after startup and at shutdown. Group
    /// policies (Software\Policies) are never touched.
    /// </summary>
    internal static class Resilience
    {
        public const string App = "PowerPoint";
        public const string ProgId = "InventaSketch.AddIn.Connect";
        public const int LoadAtStartup = 3;
        public const string OfficeVersion = "16.0";

        public static string AddInKey => @"Software\Microsoft\Office\" + App + @"\Addins\" + ProgId;
        public static string DoNotDisableKey => @"Software\Microsoft\Office\" + OfficeVersion + @"\" + App + @"\Resiliency\DoNotDisableAddinList";
        public static string DisabledItemsKey => @"Software\Microsoft\Office\" + OfficeVersion + @"\" + App + @"\Resiliency\DisabledItems";
        public static string CrashingKey => @"Software\Microsoft\Office\" + OfficeVersion + @"\" + App + @"\Resiliency\CrashingAddinList";

        /// <summary>
        /// The repairs needed: LoadBehavior back to 3 when the per-user registration exists and differs (a registration for all
        /// users stays untouched), and the "DoNotDisableAddinList" entry. <paramref name="read"/> reads a DWORD (null: key or
        /// value missing), <paramref name="exists"/> tells whether a key exists.
        /// </summary>
        public static IReadOnlyList<RegistryWrite> Repairs(Func<string, string, int?> read, Func<string, bool> exists)
        {
            var writes = new List<RegistryWrite>();
            if (!exists(AddInKey)) return writes;
            if (read(AddInKey, "LoadBehavior") != LoadAtStartup) writes.Add(new RegistryWrite(AddInKey, "LoadBehavior", LoadAtStartup));
            if (read(DoNotDisableKey, ProgId) != 1) writes.Add(new RegistryWrite(DoNotDisableKey, ProgId, 1));
            return writes;
        }

        /// <summary>Whether an Office "DisabledItems" entry (path or id of the add-in as UTF-16 bytes) belongs to InventaSketch.</summary>
        public static bool IsOwnDisabledItem(object? data)
        {
            var text = data is byte[] bytes ? System.Text.Encoding.Unicode.GetString(bytes) : data?.ToString() ?? string.Empty;
            return text.IndexOf("inventasketch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Apply(string when)
        {
            try
            {
                var writes = Repairs(Read, Exists);
                foreach (var w in writes)
                    using (var key = Registry.CurrentUser.CreateSubKey(w.Key))
                        key?.SetValue(w.Name, w.Value, RegistryValueKind.DWord);
                using (var key = Registry.CurrentUser.OpenSubKey(DisabledItemsKey, writable: true))
                    if (key is not null)
                        foreach (var name in key.GetValueNames().Where(n => IsOwnDisabledItem(key.GetValue(n))))
                        {
                            key.DeleteValue(name, false);
                            Log.Info("Resilience (" + when + "): cleared a hard disable by Office");
                        }
                using (var key = Registry.CurrentUser.OpenSubKey(CrashingKey, writable: true))
                    if (key?.GetValue(ProgId) is not null)
                    {
                        key.DeleteValue(ProgId, false);
                        Log.Info("Resilience (" + when + "): cleared the crashing add-in entry");
                    }
                if (writes.Count > 0)
                    Log.Info("Resilience (" + when + "): " + string.Join(", ", writes.Select(w => w.Key + "\\" + w.Name + "=" + w.Value)));
            }
            catch (Exception ex)
            {
                Log.Error("Resilience", ex);
            }
        }

        private static int? Read(string key, string name)
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            return k?.GetValue(name) is int i ? i : null;
        }

        private static bool Exists(string key)
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            return k is not null;
        }
    }
}
