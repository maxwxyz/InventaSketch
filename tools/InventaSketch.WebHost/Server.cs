using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// HTTPS-Server für einen Ordner mit statischen Dateien. Hört nur auf 127.0.0.1 und ::1 (TcpListener mit SslStream:
    /// ohne Administratorrechte und ohne URL-Reservierung, anders als HttpListener mit https). HTTP/1.1 mit offenen
    /// Verbindungen, nur GET und HEAD.
    /// </summary>
    public sealed class Server : IDisposable
    {
        private readonly string wurzel;
        private readonly X509Certificate2 zertifikat;
        private readonly List<TcpListener> lauscher = new List<TcpListener>();
        private volatile bool beendet;
        private int anfragen;

        public Server(string wurzel, X509Certificate2 zertifikat, int port)
        {
            this.wurzel = Path.GetFullPath(wurzel);
            this.zertifikat = zertifikat;
            Port = port;
        }

        /// <summary>Port; nach dem Start der tatsächlich belegte (bei 0 vom System vergeben).</summary>
        public int Port { get; private set; }

        /// <summary>Zeitlimit für Handshake und Lesen je Verbindung.</summary>
        public int Zeitlimit { get; set; } = 30000;

        public int Anfragen => anfragen;

        /// <summary>Beginnt zu lauschen; wirft SocketException, wenn der Port auf 127.0.0.1 belegt ist.</summary>
        public void Starten()
        {
            var v4 = new TcpListener(IPAddress.Loopback, Port);
            Exklusiv(v4);
            v4.Start();
            lauscher.Add(v4);
            Port = ((IPEndPoint)v4.LocalEndpoint).Port;
            if (Socket.OSSupportsIPv6)
            {
                try
                {
                    var v6 = new TcpListener(IPAddress.IPv6Loopback, Port);
                    Exklusiv(v6);
                    v6.Start();
                    lauscher.Add(v6);
                }
                catch (SocketException e)
                {
                    Protokoll.Schreiben("IPv6 (::1) nicht verfügbar: " + e.Message);
                }
            }
            foreach (TcpListener l in lauscher)
            {
                TcpListener eigener = l;
                Task.Run(() => Annehmen(eigener));
            }
        }

        /// <summary>Unter Windows darf kein anderer Prozess denselben Port mitbenutzen (SO_EXCLUSIVEADDRUSE).</summary>
        private static void Exklusiv(TcpListener l)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try { l.ExclusiveAddressUse = true; }
            catch (SocketException) { }
            catch (PlatformNotSupportedException) { }
        }

        private async Task Annehmen(TcpListener l)
        {
            while (!beendet)
            {
                TcpClient client;
                try { client = await l.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException e)
                {
                    if (beendet) return;
                    Protokoll.Schreiben("Annehmen: " + e.Message);
                    continue;
                }
                catch (InvalidOperationException) { return; }
                _ = Task.Run(() => Bedienen(client));
            }
        }

        private void Bedienen(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = Zeitlimit;
                    client.SendTimeout = Zeitlimit;
                    using (var ssl = new SslStream(client.GetStream(), false))
                    {
                        ssl.ReadTimeout = Zeitlimit;
                        ssl.WriteTimeout = Zeitlimit;
                        // SslProtocols.None: das Betriebssystem wählt (TLS 1.2 oder 1.3).
                        ssl.AuthenticateAsServer(zertifikat, false, SslProtocols.None, false);
                        while (!beendet)
                        {
                            string? kopf = KopfLesen(ssl, out bool zuGross);
                            if (kopf == null)
                            {
                                if (zuGross) Senden(ssl, null, true);
                                return;
                            }
                            HttpAnfrage? anfrage = HttpAnfrage.Lesen(kopf, out string? fehler);
                            if (anfrage == null) Protokoll.Schreiben("Ungültige Anfrage: " + fehler);
                            HttpAntwort antwort = HttpAntwort.Bestimmen(anfrage, wurzel, Port);
                            Interlocked.Increment(ref anfragen);
                            if (antwort.Status != 200 && anfrage != null)
                                Protokoll.Schreiben(anfrage.Methode + " " + anfrage.Ziel + " -> " + antwort);
                            Senden(ssl, antwort, false);
                            if (antwort.Schliessen) return;
                        }
                    }
                }
            }
            catch (IOException) { }
            catch (AuthenticationException e) { Protokoll.Schreiben("TLS: " + e.Message); }
            catch (ObjectDisposedException) { }
            catch (Exception e) { Protokoll.Schreiben("Verbindung: " + e); }
        }

        /// <summary>Liest bis zur Leerzeile; null bei Ende der Verbindung oder wenn der Kopf zu groß ist.</summary>
        private static string? KopfLesen(Stream s, out bool zuGross)
        {
            zuGross = false;
            var puffer = new MemoryStream();
            var ein = new byte[1];
            while (true)
            {
                int n = s.Read(ein, 0, 1);
                if (n == 0) return null;
                byte b = ein[0];
                // Leere Zeilen vor der Anfragezeile übergehen (RFC 9112, 2.2).
                if (puffer.Length == 0 && (b == '\r' || b == '\n')) continue;
                puffer.WriteByte(b);
                if (b == '\n' && KopfEnde(puffer)) break;
                if (puffer.Length > HttpAnfrage.MaxKopf)
                {
                    zuGross = true;
                    return null;
                }
            }
            return Encoding.ASCII.GetString(puffer.ToArray()).TrimEnd('\r', '\n');
        }

        /// <summary>Puffer endet mit einer Leerzeile ("\r\n\r\n" oder "\n\n").</summary>
        private static bool KopfEnde(MemoryStream m)
        {
            byte[] b = m.GetBuffer();
            long n = m.Length;
            if (n >= 2 && b[n - 2] == '\n') return true;
            return n >= 4 && b[n - 2] == '\r' && b[n - 3] == '\n' && b[n - 4] == '\r';
        }

        private static void Senden(Stream s, HttpAntwort? antwort, bool zuGross)
        {
            if (zuGross)
            {
                byte[] k = Encoding.ASCII.GetBytes("HTTP/1.1 431 Request Header Fields Too Large\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                s.Write(k, 0, k.Length);
                s.Flush();
                return;
            }
            if (antwort == null) return;
            if (antwort.Datei == null)
            {
                byte[] text = antwort.TextBytes();
                byte[] kopf = Encoding.ASCII.GetBytes(antwort.Kopf(text.Length));
                s.Write(kopf, 0, kopf.Length);
                if (antwort.MitInhalt) s.Write(text, 0, text.Length);
                s.Flush();
                return;
            }
            using (var datei = new FileStream(antwort.Datei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
            {
                byte[] kopf = Encoding.ASCII.GetBytes(antwort.Kopf(datei.Length));
                s.Write(kopf, 0, kopf.Length);
                if (antwort.MitInhalt) datei.CopyTo(s, 65536);
                s.Flush();
            }
        }

        public void Dispose()
        {
            beendet = true;
            foreach (TcpListener l in lauscher)
            {
                try { l.Stop(); }
                catch (SocketException) { }
            }
            lauscher.Clear();
        }
    }
}
