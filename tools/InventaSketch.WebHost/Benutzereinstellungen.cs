using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace InventaSketch.WebHost
{
    /// <summary>Autostart je Benutzer über HKCU\...\Run (ohne Administratorrechte).</summary>
    public static class Autostart
    {
        public const string Schluessel = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string Name = "InventaSketch Web-Add-in";

        /// <summary>Befehlszeile des Eintrags: das installierte Programm ohne Argumente (Ordner App daneben).</summary>
        public static string Befehl(string exe) => "\"" + exe + "\"";

        public static string? Lesen()
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Schluessel))
                return k?.GetValue(Name) as string;
        }

        public static void Eintragen(string exe)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Schluessel))
                k.SetValue(Name, Befehl(exe), RegistryValueKind.String);
        }

        public static void Entfernen()
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Schluessel, true))
                k?.DeleteValue(Name, false);
        }

        /// <summary>Richtlinie "Liste ‚Ausführen' des Benutzers nicht verarbeiten" (DisableCurrentUserRun).</summary>
        public static bool Gesperrt()
        {
            const string p = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
            foreach (RegistryKey wurzel in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using (RegistryKey? k = wurzel.OpenSubKey(p))
                {
                    if (k?.GetValue("DisableCurrentUserRun") is int v && v != 0) return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Web-Add-ins in Office für Windows: Seitenladen für Entwickler über HKCU\Software\Microsoft\Office\16.0\WEF\Developer
    /// (Name = Kennung des Add-ins, Wert = Pfad des Manifests, wie office-addin-dev-settings). Office lädt die dort
    /// eingetragenen Manifeste beim nächsten Start.
    /// </summary>
    public static class OfficeAddins
    {
        public const string Entwickler = @"Software\Microsoft\Office\16.0\WEF\Developer";
        private const string Richtlinien = @"Software\Policies\Microsoft\Office\16.0\WEF";

        public static void Registrieren(string kennung, string manifest)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Entwickler))
            {
                // Ältere Fassungen von office-addin-dev-settings nahmen den Pfad als Namen.
                k.DeleteValue(manifest, false);
                k.SetValue(kennung, manifest, RegistryValueKind.String);
            }
        }

        public static string? Wert(string name)
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Entwickler))
                return k?.GetValue(name) as string;
        }

        /// <summary>Namen der Einträge, die auf das Manifest zeigen (Wert oder, ältere Form, Name gleich dem Pfad).</summary>
        public static List<string> Eintraege(string manifest)
        {
            var namen = new List<string>();
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Entwickler))
            {
                if (k == null) return namen;
                foreach (string name in k.GetValueNames())
                {
                    if (string.Equals(k.GetValue(name) as string, manifest, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, manifest, StringComparison.OrdinalIgnoreCase))
                        namen.Add(name);
                }
            }
            return namen;
        }

        public static void Abmelden(IEnumerable<string> namen)
        {
            using (RegistryKey? k = Registry.CurrentUser.OpenSubKey(Entwickler, true))
            {
                if (k == null) return;
                foreach (string name in namen) k.DeleteValue(name, false);
            }
        }

        /// <summary>
        /// Richtlinien für Web-Add-ins (HKLM und HKCU unter Software\Policies\Microsoft\Office\16.0\WEF, mit
        /// Unterschlüsseln). Sperrend gilt ein Wert ungleich 0, dessen Name "Disable" oder "Block" enthält und nicht nur den
        /// Store betrifft ("Omex", "Store"). Rückgabe: (sperrende, übrige) als lesbare Zeilen.
        /// </summary>
        public static (List<string> Sperrend, List<string> Uebrige) Richtlinie()
        {
            var sperrend = new List<string>();
            var uebrige = new List<string>();
            foreach (var (wurzel, name) in new[] { (Registry.LocalMachine, "HKLM"), (Registry.CurrentUser, "HKCU") })
            {
                using (RegistryKey? k = wurzel.OpenSubKey(Richtlinien))
                {
                    if (k != null) Sammeln(k, name + @"\" + Richtlinien, sperrend, uebrige);
                }
            }
            return (sperrend, uebrige);
        }

        private static void Sammeln(RegistryKey k, string pfad, List<string> sperrend, List<string> uebrige)
        {
            foreach (string name in k.GetValueNames())
            {
                object? wert = k.GetValue(name);
                string zeile = pfad + ": " + name + " = " + wert;
                if (IstSperre(name, wert)) sperrend.Add(zeile);
                else uebrige.Add(zeile);
            }
            foreach (string unter in k.GetSubKeyNames())
            {
                using (RegistryKey? u = k.OpenSubKey(unter))
                {
                    if (u != null) Sammeln(u, pfad + @"\" + unter, sperrend, uebrige);
                }
            }
        }

        /// <summary>Ob ein Richtlinienwert Web-Add-ins sperrt (siehe <see cref="Richtlinie"/>).</summary>
        public static bool IstSperre(string name, object? wert)
        {
            string n = name.ToLowerInvariant();
            if (!(n.Contains("disable") || n.Contains("block"))) return false;
            if (n.Contains("omex") || n.Contains("store")) return false;
            return wert is int i ? i != 0 : wert is string s && s.Trim() != "" && s.Trim() != "0";
        }

        /// <summary>Das COM-Add-in ist in PowerPoint eingetragen (dann blendet Office das Web-Add-in aus, EquivalentAddins).</summary>
        public static bool ComAddinEingetragen()
        {
            const string p = @"Software\Microsoft\Office\PowerPoint\Addins\InventaSketch.AddIn.Connect";
            foreach (RegistryKey wurzel in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using (RegistryKey? k = wurzel.OpenSubKey(p))
                {
                    if (k != null) return true;
                }
            }
            return false;
        }
    }
}
