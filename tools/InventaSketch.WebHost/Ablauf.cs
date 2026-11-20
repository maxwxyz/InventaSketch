using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace InventaSketch.WebHost
{
    /// <summary>Führt Server, Installation und Deinstallation unter Windows aus (die Pläne kommen aus <see cref="Installation"/>).</summary>
    public static class Ablauf
    {
        /// <summary>Paketordner: der Ordner über Server\InventaSketch.WebHost.exe.</summary>
        public static string Paketordner =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Ablauf).Assembly.Location) ?? ".", ".."));

        // ------------------------------------------------------------------ Zertifikat

        /// <summary>
        /// Liefert das Zertifikat aus CurrentUser\My (legt es bei Bedarf an oder erneuert es) und trägt es auf Wunsch als
        /// vertrauenswürdig ein. Fehler beim Vertrauen stehen in <paramref name="fehler"/>; das Zertifikat kommt trotzdem.
        /// </summary>
        public static X509Certificate2 ZertifikatBereitstellen(bool vertrauen, out string? fehler)
        {
            fehler = null;
            List<X509Certificate2> vorhanden = Zertifikatspeicher.Eigene(StoreName.My);
            X509Certificate2? gefunden = Zertifikate.Auswaehlen(vorhanden);
            ZertifikatEntscheidung e = Zertifikate.Entscheiden(gefunden?.NotAfter, gefunden?.HasPrivateKey ?? false, DateTime.Now);
            X509Certificate2 bestes;
            if (e == ZertifikatEntscheidung.Wiederverwenden && gefunden != null)
            {
                bestes = gefunden;
            }
            else
            {
                bestes = Zertifikate.Erzeugen(DateTime.Now, true);
                Zertifikatspeicher.Speichern(bestes);
                Protokoll.Melden("Zertifikat angelegt: " + bestes.Subject + ", gültig bis " + bestes.NotAfter.ToString("yyyy-MM-dd")
                    + ", Fingerabdruck " + bestes.Thumbprint);
            }
            if (vertrauen && !Zertifikatspeicher.IstVertraut(bestes))
            {
                if (Zertifikatspeicher.StammGesperrt())
                    fehler = "Eine Richtlinie erlaubt nicht, eigenen Stammzertifikaten zu vertrauen (ProtectedRoots).";
                else
                {
                    Protokoll.Melden("Windows fragt jetzt, ob dem Zertifikat \"localhost\" vertraut werden soll: mit Ja bestätigen.");
                    fehler = Zertifikatspeicher.Vertrauen(bestes);
                }
                if (fehler == null) Protokoll.Melden("Zertifikat ist als vertrauenswürdig eingetragen.");
            }
            if (vertrauen && fehler == null)
            {
                // Abgelaufene oder bald ablaufende eigene Zertifikate aufräumen, wenn das neue vertraut ist.
                string aktuell = bestes.Thumbprint;
                var alte = Zertifikatspeicher.Eigene(StoreName.My).Concat(Zertifikatspeicher.Eigene(StoreName.Root))
                    .Where(c => c.Thumbprint != aktuell
                        && Zertifikate.Entscheiden(c.NotAfter, true, DateTime.Now) != ZertifikatEntscheidung.Wiederverwenden)
                    .GroupBy(c => c.Thumbprint).Select(g => g.First());
                foreach (X509Certificate2 alt in alte)
                {
                    Protokoll.Melden("Altes Zertifikat entfernen (gültig bis " + alt.NotAfter.ToString("yyyy-MM-dd") + ").");
                    foreach (string f in Zertifikatspeicher.Entfernen(alt)) Protokoll.Melden("Hinweis: " + f);
                }
            }
            return bestes;
        }

        // ------------------------------------------------------------------ Server

        public static int Dienst(Optionen o)
        {
            using (Mutex? sperre = Instanz.Sperren(o.Port))
            {
                if (sperre == null)
                {
                    Protokoll.Melden("Server läuft bereits (Port " + o.Port + ").");
                    return 0;
                }
                string wurzel = o.Ordner ?? Installation.App(Paketordner);
                if (!File.Exists(Path.Combine(wurzel, StatischeDateien.Startseite)))
                {
                    Protokoll.Melden("Ordner ohne " + StatischeDateien.Startseite + ": " + wurzel);
                    return 3;
                }
                X509Certificate2 zertifikat;
                if (o.TemporaeresZertifikat)
                {
                    zertifikat = Zertifikate.Erzeugen(DateTime.Now, false);
                }
                else
                {
                    zertifikat = ZertifikatBereitstellen(!o.OhneVertrauen, out string? fehler);
                    if (fehler != null)
                        Protokoll.Melden("Warnung: Zertifikat nicht vertraut, Office lädt das Add-in dann nicht. " + fehler);
                }
                using (var server = new Server(wurzel, zertifikat, o.Port))
                using (EventWaitHandle beenden = Instanz.BeendenAnlegen(o.Port))
                {
                    try { server.Starten(); }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        Protokoll.Melden("Port " + o.Port + " ist belegt (" + e.Message + "). Läuft schon ein anderer Server, z. B. npm run dev?");
                        return 4;
                    }
                    using (EventWaitHandle bereit = Instanz.BereitAnlegen(server.Port))
                    {
                        bereit.Set();
                        Protokoll.Melden("Server läuft: https://localhost:" + server.Port + "/ (Ordner " + wurzel + ", Prozess "
                            + Process.GetCurrentProcess().Id + ")");
                        beenden.WaitOne();
                        bereit.Reset();
                    }
                    Protokoll.Melden("Server beendet nach " + server.Anfragen + " Anfragen.");
                }
                return 0;
            }
        }

        // ------------------------------------------------------------------ Installation

        public static Zustand ZustandErmitteln(string quelle, string ziel, int port)
        {
            var z = new Zustand { Quelle = quelle, Ziel = ziel };
            z.PaketFehlt = Installation.PaketPruefen(quelle);
            z.ServerLaeuft = Instanz.Laeuft(port);
            List<X509Certificate2> eigene = Zertifikatspeicher.Eigene(StoreName.My);
            X509Certificate2? bestes = Zertifikate.Auswaehlen(eigene);
            z.Zertifikat = Zertifikate.Entscheiden(bestes?.NotAfter, bestes?.HasPrivateKey ?? false, DateTime.Now);
            z.ZertifikatVertraut = bestes != null && Zertifikatspeicher.IstVertraut(bestes);
            z.EigeneZertifikate = eigene.Count + Zertifikatspeicher.Eigene(StoreName.Root).Count;
            z.StammGesperrt = Zertifikatspeicher.StammGesperrt();
            z.WebAddinSperren = OfficeAddins.Richtlinie().Sperrend;
            z.AutostartGesperrt = Autostart.Gesperrt();
            z.AutostartWert = Autostart.Lesen();
            z.ComAddin = OfficeAddins.ComAddinEingetragen();
            string manifest = Installation.Manifest(quelle);
            if (File.Exists(manifest)) z.Kennung = ManifestAdresse.Kennung(File.ReadAllText(manifest));
            z.ManifestEintraege = OfficeAddins.Eintraege(Installation.Manifest(ziel));
            z.ZielVorhanden = Directory.Exists(ziel);
            return z;
        }

        /// <summary>Gibt den Plan aus und führt ihn aus (ohne Probelauf). Rückgabe 0 oder 5 bei Abbruch.</summary>
        public static int Ausfuehren(List<Schritt> plan, Zustand z, int port, bool probelauf)
        {
            int nr = 0;
            foreach (Schritt s in plan)
            {
                string kennung = s.Art == SchrittArt.Abbruch ? "ABBRUCH" : s.Art == SchrittArt.Hinweis ? "Hinweis" : (++nr).ToString();
                Protokoll.Melden((probelauf ? "[Probelauf] " : "") + kennung + ". " + s.Text);
                if (s.Art == SchrittArt.Abbruch) return 5;
                if (probelauf) continue;
                string? fehler = SchrittAusfuehren(s, z, port);
                if (fehler != null)
                {
                    Protokoll.Melden("ABBRUCH: " + fehler);
                    return 5;
                }
            }
            return 0;
        }

        private static string? SchrittAusfuehren(Schritt s, Zustand z, int port)
        {
            switch (s.Art)
            {
                case SchrittArt.ServerBeenden:
                    return Instanz.Beenden(port, TimeSpan.FromSeconds(15)) ? null : "Der laufende Server endet nicht (Task-Manager: InventaSketch.WebHost).";
                case SchrittArt.DateienKopieren:
                    foreach (string teil in new[] { "App", "Server" })
                    {
                        string alt = Path.Combine(z.Ziel, teil);
                        if (Directory.Exists(alt)) OrdnerLoeschen(alt);
                    }
                    Kopieren(z.Quelle, z.Ziel);
                    return null;
                case SchrittArt.ZertifikatAnlegen:
                case SchrittArt.ZertifikatErneuern:
                case SchrittArt.ZertifikatWiederverwenden:
                    // Anlegen und Vertrauen gemeinsam; das Vertrauen folgt im nächsten Schritt.
                    ZertifikatBereitstellen(false, out _);
                    return null;
                case SchrittArt.ZertifikatVertrauen:
                    ZertifikatBereitstellen(true, out string? fehler);
                    return fehler == null ? null : fehler + " Ohne vertrautes Zertifikat lädt Office das Add-in nicht.";
                case SchrittArt.AutostartEintragen:
                    Autostart.Eintragen(Installation.Exe(z.Ziel));
                    return null;
                case SchrittArt.AutostartEntfernen:
                    Autostart.Entfernen();
                    return null;
                case SchrittArt.ManifestRegistrieren:
                    OfficeAddins.Registrieren(z.Kennung!, Installation.Manifest(z.Ziel));
                    return null;
                case SchrittArt.ManifestAbmelden:
                    OfficeAddins.Abmelden(z.ManifestEintraege);
                    return null;
                case SchrittArt.ServerStarten:
                    string exe = Installation.Exe(z.Ziel);
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) })?.Dispose();
                    return Instanz.WartenBereit(port, TimeSpan.FromSeconds(60)) ? null
                        : "Der Server startet nicht; Protokoll: " + Protokoll.StandardDatei;
                case SchrittArt.ZertifikatEntfernen:
                    var fehlerListe = new List<string>();
                    foreach (StoreName name in new[] { StoreName.My, StoreName.Root })
                    {
                        foreach (X509Certificate2 c in Zertifikatspeicher.Eigene(name)) fehlerListe.AddRange(Zertifikatspeicher.Entfernen(c));
                    }
                    foreach (string f in fehlerListe.Distinct()) Protokoll.Melden("Hinweis: " + f);
                    return null;
                case SchrittArt.OrdnerEntfernen:
                    OrdnerLoeschen(z.Ziel);
                    return null;
                default:
                    return null;
            }
        }

        private static void Kopieren(string quelle, string ziel)
        {
            Directory.CreateDirectory(ziel);
            foreach (string datei in Directory.GetFiles(quelle))
                File.Copy(datei, Path.Combine(ziel, Path.GetFileName(datei)), true);
            foreach (string ordner in Directory.GetDirectories(quelle))
                Kopieren(ordner, Path.Combine(ziel, Path.GetFileName(ordner)));
        }

        /// <summary>Löscht einen Ordner; wiederholt kurz, solange ein eben beendeter Prozess noch Dateien hält.</summary>
        private static void OrdnerLoeschen(string ordner)
        {
            for (int versuch = 0; ; versuch++)
            {
                try
                {
                    if (Directory.Exists(ordner)) Directory.Delete(ordner, true);
                    return;
                }
                catch (Exception e) when ((e is IOException || e is UnauthorizedAccessException) && versuch < 20)
                {
                    Thread.Sleep(250);
                }
            }
        }
    }
}
