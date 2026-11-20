using System;
using System.Globalization;
using System.Text;

namespace InventaSketch.WebHost
{
    /// <summary>Antwort auf eine Anfrage, ohne Netz bestimmt (prüfbar): Status, Datei, Inhaltstyp, Verbindung.</summary>
    public sealed class HttpAntwort
    {
        private HttpAntwort(int status, string grund, string? datei, string typ, bool schliessen, bool mitInhalt, string? text)
        {
            Status = status;
            Grund = grund;
            Datei = datei;
            Typ = typ;
            Schliessen = schliessen;
            MitInhalt = mitInhalt;
            Text = text;
        }

        public int Status { get; }
        public string Grund { get; }

        /// <summary>Auszuliefernde Datei (nur Status 200).</summary>
        public string? Datei { get; }

        public string Typ { get; }

        /// <summary>Nach der Antwort die Verbindung schließen.</summary>
        public bool Schliessen { get; }

        /// <summary>Inhalt senden (GET), bei HEAD nur den Kopf.</summary>
        public bool MitInhalt { get; }

        /// <summary>Kurzer Text als Inhalt bei Fehlern.</summary>
        public string? Text { get; }

        /// <summary>Zulässige Hostnamen: der Server hört nur auf der Loopback-Schnittstelle (Schutz vor DNS-Rebinding).</summary>
        public static bool HostZulaessig(string? host, int port)
        {
            if (string.IsNullOrEmpty(host)) return false;
            string h = host!.Trim().ToLowerInvariant();
            string p = ":" + port.ToString(CultureInfo.InvariantCulture);
            foreach (string name in new[] { "localhost", "127.0.0.1", "[::1]" })
            {
                if (h == name || h == name + p) return true;
            }
            return false;
        }

        /// <summary>Bestimmt die Antwort: nur GET und HEAD, nur Loopback-Hosts, nur Dateien unter der Wurzel.</summary>
        public static HttpAntwort Bestimmen(HttpAnfrage? anfrage, string wurzel, int port)
        {
            if (anfrage == null) return Fehler(400, "Bad Request", "Anfrage ungültig", true, true);
            bool offen = anfrage.OffenHalten;
            bool kopf = anfrage.Methode == "HEAD";
            if (anfrage.Methode != "GET" && !kopf) return Fehler(405, "Method Not Allowed", "Nur GET und HEAD", true, true);
            if (anfrage.HatInhalt) return Fehler(400, "Bad Request", "Anfrage mit Inhalt", true, !kopf);
            string? host = anfrage.Kopfzeile("Host");
            if (host != null && !HostZulaessig(host, port)) return Fehler(400, "Bad Request", "Host nicht zulässig", true, !kopf);
            string? datei = StatischeDateien.Aufloesen(wurzel, anfrage.Ziel);
            if (datei == null) return Fehler(404, "Not Found", "Nicht gefunden", !offen, !kopf);
            return new HttpAntwort(200, "OK", datei, StatischeDateien.Typ(datei), !offen, !kopf, null);
        }

        private static HttpAntwort Fehler(int status, string grund, string text, bool schliessen, bool mitInhalt) =>
            new HttpAntwort(status, grund, null, "text/plain; charset=utf-8", schliessen, mitInhalt, text);

        /// <summary>Kopf der Antwort (bis einschließlich der Leerzeile).</summary>
        public string Kopf(long laenge)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(Status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Grund).Append("\r\n");
            sb.Append("Content-Type: ").Append(Typ).Append("\r\n");
            sb.Append("Content-Length: ").Append(laenge.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("Cache-Control: no-cache\r\n");
            sb.Append("X-Content-Type-Options: nosniff\r\n");
            sb.Append("Date: ").Append(DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
            if (Status == 405) sb.Append("Allow: GET, HEAD\r\n");
            sb.Append(Schliessen ? "Connection: close\r\n" : "Connection: keep-alive\r\n");
            sb.Append("\r\n");
            return sb.ToString();
        }

        /// <summary>Inhalt eines Fehlertexts als Bytes.</summary>
        public byte[] TextBytes() => Text == null ? new byte[0] : Encoding.UTF8.GetBytes(Text);

        public override string ToString() => Status.ToString(CultureInfo.InvariantCulture) + " " + Grund;
    }
}
