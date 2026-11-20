using System;
using System.Text;
using System.Text.RegularExpressions;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Manifest für eine eigene HTTPS-Adresse: ersetzt https://localhost:3217. Dieselben Regeln wie manifest-erzeugen.ps1
    /// und inventasketch_web.py (manifest-erzeugen.sh); die Tests der Web-Fassung vergleichen die Ergebnisse.
    /// </summary>
    public static class ManifestAdresse
    {
        public const string Lokal = "https://localhost:3217";

        private static readonly Regex Form = new Regex(@"^https://[A-Za-z0-9.\-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~\-/%]*)?$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Prüft und vereinheitlicht die Adresse: muss mit https:// beginnen, Host aus Buchstaben, Ziffern, Punkt und
        /// Bindestrich, optional Port und Pfad, ohne Abfrage, Anker, Leer- oder Sonderzeichen; Schrägstriche am Ende
        /// entfallen, Schema und Host klein. Null mit Grund, wenn ungültig.
        /// </summary>
        public static string? Pruefen(string? eingabe, out string? fehler)
        {
            fehler = null;
            string a = (eingabe ?? "").Trim();
            if (a.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) a = "https://" + a.Substring(8);
            while (a.EndsWith("/", StringComparison.Ordinal)) a = a.Substring(0, a.Length - 1);
            if (!a.StartsWith("https://", StringComparison.Ordinal))
            {
                fehler = "Die Adresse muss mit https:// beginnen.";
                return null;
            }
            if (!Form.IsMatch(a))
            {
                fehler = "Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen).";
                return null;
            }
            int pfad = a.IndexOf('/', 8);
            string autoritaet = pfad < 0 ? a.Substring(8) : a.Substring(8, pfad - 8);
            string host = autoritaet.Split(':')[0];
            if (host.Length == 0 || host.StartsWith(".", StringComparison.Ordinal) || host.EndsWith(".", StringComparison.Ordinal)
                || host.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                fehler = "Ungültiger Hostname.";
                return null;
            }
            if (autoritaet.Contains(":") && (!int.TryParse(autoritaet.Split(':')[1], out int port) || port < 1 || port > 65535))
            {
                fehler = "Ungültiger Port.";
                return null;
            }
            return "https://" + autoritaet.ToLowerInvariant() + (pfad < 0 ? "" : a.Substring(pfad));
        }

        /// <summary>Ersetzt jede Nennung der lokalen Adresse durch die neue.</summary>
        public static string Umschreiben(string manifest, string adresse) => manifest.Replace(Lokal, adresse);

        /// <summary>Dateiname "manifest-host[-port].xml" für die Adresse (geprüft).</summary>
        public static string Dateiname(string adresse)
        {
            string rest = adresse.Substring("https://".Length);
            int pfad = rest.IndexOf('/');
            string autoritaet = pfad < 0 ? rest : rest.Substring(0, pfad);
            var sb = new StringBuilder();
            foreach (char c in autoritaet.Replace(':', '-'))
                sb.Append(char.IsLetterOrDigit(c) && c < 128 || c == '.' || c == '-' ? c : '-');
            return "manifest-" + sb + ".xml";
        }

        /// <summary>Kennung (Id) des Add-ins aus dem Manifest oder null.</summary>
        public static string? Kennung(string manifest)
        {
            Match m = Regex.Match(manifest, @"<Id>\s*([0-9A-Fa-f\-]{36})\s*</Id>");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
