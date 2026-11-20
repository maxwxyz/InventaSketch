using System;
using System.Collections.Generic;
using System.IO;

namespace InventaSketch.WebHost
{
    /// <summary>Zuordnung von Anfragepfaden zu Dateien unter einem Wurzelordner und Inhaltstypen.</summary>
    public static class StatischeDateien
    {
        /// <summary>Datei für "/" (wie scripts/serve.mjs).</summary>
        public const string Startseite = "taskpane.html";

        private static readonly Dictionary<string, string> Typen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".htm"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".mjs"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".map"] = "application/json; charset=utf-8",
            [".wasm"] = "application/wasm",
            [".dat"] = "application/octet-stream",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon",
            [".xml"] = "application/xml; charset=utf-8",
            [".txt"] = "text/plain; charset=utf-8",
            [".md"] = "text/markdown; charset=utf-8",
            [".pdf"] = "application/pdf",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
        };

        /// <summary>Inhaltstyp nach der Endung; unbekannte Endungen als application/octet-stream.</summary>
        public static string Typ(string datei)
        {
            string endung = Path.GetExtension(datei);
            return Typen.TryGetValue(endung, out string? typ) ? typ : "application/octet-stream";
        }

        /// <summary>
        /// Zerlegt das Anfrageziel (nur "/pfad", Abfrage und Anker entfallen) in Pfadteile. Null, wenn das Ziel aus der
        /// Wurzel hinausführen könnte oder unter Windows etwas anderes als eine Datei meint: "..", "\", ":" (Laufwerk,
        /// alternativer Datenstrom), Steuerzeichen, Teile mit Punkt oder Leerzeichen am Ende. Leer: Startseite.
        /// </summary>
        public static string[]? Zerlegen(string ziel)
        {
            if (ziel.Length == 0 || ziel[0] != '/') return null;
            int ende = ziel.IndexOfAny(new[] { '?', '#' });
            string pfad = ende >= 0 ? ziel.Substring(0, ende) : ziel;
            string entschluesselt;
            try { entschluesselt = Uri.UnescapeDataString(pfad); }
            catch (UriFormatException) { return null; }
            var teile = new List<string>();
            foreach (string teil in entschluesselt.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (teil == ".") continue;
                if (teil == "..") return null;
                foreach (char c in teil)
                {
                    if (c < 0x20 || c == 0x7f || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                        return null;
                }
                char letztes = teil[teil.Length - 1];
                if (letztes == '.' || letztes == ' ') return null;
                teile.Add(teil);
            }
            return teile.ToArray();
        }

        /// <summary>
        /// Vollständiger Pfad der Datei zum Anfrageziel oder null (unzulässig oder nicht vorhanden). Ein Ordner liefert
        /// seine index.html, "/" die Startseite. Das Ergebnis liegt immer unter <paramref name="wurzel"/>.
        /// </summary>
        public static string? Aufloesen(string wurzel, string ziel)
        {
            string[]? teile = Zerlegen(ziel);
            if (teile == null) return null;
            string basis = Path.GetFullPath(wurzel).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string datei;
            try
            {
                datei = teile.Length == 0 ? Path.Combine(basis, Startseite)
                    : Path.GetFullPath(Path.Combine(basis, string.Join(Path.DirectorySeparatorChar.ToString(), teile)));
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return null;
            }
            StringComparison vergleich = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!datei.StartsWith(basis + Path.DirectorySeparatorChar, vergleich)) return null;
            if (Directory.Exists(datei)) datei = Path.Combine(datei, "index.html");
            return File.Exists(datei) ? datei : null;
        }
    }
}
