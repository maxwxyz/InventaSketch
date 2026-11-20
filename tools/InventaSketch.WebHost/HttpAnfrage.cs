using System;
using System.Collections.Generic;
using System.Globalization;

namespace InventaSketch.WebHost
{
    /// <summary>Kopf einer HTTP/1.x-Anfrage (Anfragezeile und Kopfzeilen, ohne Inhalt).</summary>
    public sealed class HttpAnfrage
    {
        /// <summary>Größte zulässige Länge des Kopfs in Bytes.</summary>
        public const int MaxKopf = 16 * 1024;

        private HttpAnfrage(string methode, string ziel, string version, Dictionary<string, string> kopfzeilen)
        {
            Methode = methode;
            Ziel = ziel;
            Version = version;
            Kopfzeilen = kopfzeilen;
        }

        public string Methode { get; }
        public string Ziel { get; }
        public string Version { get; }
        public IReadOnlyDictionary<string, string> Kopfzeilen { get; }

        public string? Kopfzeile(string name) => Kopfzeilen.TryGetValue(name, out string? wert) ? wert : null;

        /// <summary>Verbindung offen halten: HTTP/1.1 ohne "Connection: close" oder HTTP/1.0 mit "keep-alive".</summary>
        public bool OffenHalten
        {
            get
            {
                string verbindung = (Kopfzeile("Connection") ?? "").ToLowerInvariant();
                return Version == "HTTP/1.1" ? verbindung.IndexOf("close", StringComparison.Ordinal) < 0
                    : verbindung.IndexOf("keep-alive", StringComparison.Ordinal) >= 0;
            }
        }

        /// <summary>Die Anfrage trägt einen Inhalt (den der Server nicht liest).</summary>
        public bool HatInhalt
        {
            get
            {
                if (Kopfzeile("Transfer-Encoding") != null) return true;
                string? laenge = Kopfzeile("Content-Length");
                return laenge != null && (!long.TryParse(laenge, NumberStyles.None, CultureInfo.InvariantCulture, out long n) || n != 0);
            }
        }

        /// <summary>
        /// Liest den Kopf (ohne die abschließende Leerzeile). Gibt null und einen Grund zurück, wenn er nicht dem Format
        /// entspricht (Anfragezeile mit drei Teilen, Version HTTP/1.0 oder HTTP/1.1, Kopfzeilen "Name: Wert").
        /// </summary>
        public static HttpAnfrage? Lesen(string kopf, out string? fehler)
        {
            fehler = null;
            string[] zeilen = kopf.Replace("\r\n", "\n").Split('\n');
            string[] teile = zeilen[0].Split(' ');
            if (teile.Length != 3 || teile[0].Length == 0 || teile[1].Length == 0)
            {
                fehler = "Anfragezeile ungültig";
                return null;
            }
            foreach (char c in teile[0])
            {
                if (c < 'A' || c > 'Z') { fehler = "Methode ungültig"; return null; }
            }
            if (teile[2] != "HTTP/1.1" && teile[2] != "HTTP/1.0")
            {
                fehler = "Version nicht unterstützt";
                return null;
            }
            var kopfzeilen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < zeilen.Length; i++)
            {
                string zeile = zeilen[i];
                if (zeile.Length == 0) continue;
                int p = zeile.IndexOf(':');
                if (p <= 0 || zeile[0] == ' ' || zeile[0] == '\t' || zeile.Substring(0, p).IndexOf(' ') >= 0)
                {
                    fehler = "Kopfzeile ungültig";
                    return null;
                }
                string name = zeile.Substring(0, p);
                string wert = zeile.Substring(p + 1).Trim();
                kopfzeilen[name] = kopfzeilen.TryGetValue(name, out string? alt) ? alt + ", " + wert : wert;
            }
            if (teile[2] == "HTTP/1.1" && !kopfzeilen.ContainsKey("Host"))
            {
                fehler = "Host fehlt";
                return null;
            }
            return new HttpAnfrage(teile[0], teile[1], teile[2], kopfzeilen);
        }
    }
}
