using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Einstieg. Rückgabewerte: 0 erfolgreich, 1 Server läuft nicht (Status, Beenden), 2 ungültige Angaben, 3 Ordner ohne
    /// Aufgabenbereich, 4 Port belegt, 5 Abbruch der Installation (Richtlinie, abgelehntes Zertifikat, unvollständiges Paket).
    /// </summary>
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int prozess);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int art);

        public static int Main(string[] args)
        {
            Optionen o = Optionen.Lesen(args);
            Protokoll.Datei = Protokoll.StandardDatei;
            if (o.Befehl != Befehl.Starten || o.Fehler != null) KonsoleVerbinden();
            if (o.Fehler != null)
            {
                Protokoll.Melden(o.Fehler);
                Protokoll.Melden(Optionen.Hilfetext);
                return 2;
            }
            try
            {
                switch (o.Befehl)
                {
                    case Befehl.Hilfe:
                        Protokoll.Konsole = true;
                        Console.WriteLine(Optionen.Hilfetext);
                        return 0;
                    case Befehl.Status:
                        if (Instanz.WartenBereit(o.Port, TimeSpan.FromSeconds(o.Warten)))
                        {
                            Protokoll.Melden("InventaSketch-Web läuft: https://localhost:" + o.Port + "/taskpane.html");
                            return 0;
                        }
                        Protokoll.Melden("InventaSketch-Web läuft nicht (Port " + o.Port + "). Protokoll: " + Protokoll.StandardDatei);
                        return 1;
                    case Befehl.Beenden:
                        if (Instanz.Beenden(o.Port, TimeSpan.FromSeconds(15)))
                        {
                            Protokoll.Melden("InventaSketch-Web beendet.");
                            return 0;
                        }
                        Protokoll.Melden("Kein laufender Server auf Port " + o.Port + " (oder er endet nicht).");
                        return 1;
                    case Befehl.Manifest:
                        return ManifestSchreiben(o);
                    case Befehl.Installieren:
                    {
                        string quelle = Path.GetFullPath(o.Quelle ?? Ablauf.Paketordner);
                        string ziel = Path.GetFullPath(o.Ziel ?? Installation.ZielStandard);
                        Protokoll.Melden("InventaSketch-Web installieren für " + Environment.UserName + " (ohne Administratorrechte)");
                        Zustand z = Ablauf.ZustandErmitteln(quelle, ziel, o.Port);
                        int rc = Ablauf.Ausfuehren(Installation.Installieren(z, o.Autostart, !o.OhneManifest, !o.OhneStart), z, o.Port, o.Probelauf);
                        if (rc == 0 && !o.Probelauf)
                            Protokoll.Melden("Fertig. Prüfen im Browser: https://localhost:" + o.Port + "/taskpane.html; Deinstallieren: \""
                                + Path.Combine(ziel, "Web-Add-in deinstallieren.cmd") + "\".");
                        return rc;
                    }
                    case Befehl.Deinstallieren:
                    {
                        string ziel = Path.GetFullPath(o.Ziel ?? Installation.ZielStandard);
                        Protokoll.Melden("InventaSketch-Web deinstallieren für " + Environment.UserName);
                        Zustand z = Ablauf.ZustandErmitteln(ziel, ziel, o.Port);
                        return Ablauf.Ausfuehren(Installation.Deinstallieren(z), z, o.Port, o.Probelauf);
                    }
                    default:
                        return Ablauf.Dienst(o);
                }
            }
            catch (Exception e)
            {
                Protokoll.Melden("Fehler: " + e.Message);
                Protokoll.Schreiben(e.ToString());
                return 5;
            }
        }

        private static int ManifestSchreiben(Optionen o)
        {
            string? adresse = ManifestAdresse.Pruefen(o.Adresse, out string? fehler);
            if (adresse == null)
            {
                Protokoll.Melden(fehler ?? "Adresse ungültig.");
                return 2;
            }
            string quelle = Installation.Manifest(o.Quelle ?? Ablauf.Paketordner);
            string ziel = Path.Combine(o.Ziel ?? Path.GetDirectoryName(quelle) ?? ".", ManifestAdresse.Dateiname(adresse));
            File.WriteAllText(ziel, ManifestAdresse.Umschreiben(File.ReadAllText(quelle, Encoding.UTF8), adresse), new UTF8Encoding(false));
            Protokoll.Melden("Manifest für " + adresse + ": " + ziel);
            return 0;
        }

        /// <summary>
        /// Das Programm ist fensterlos. Aus einer Konsole (cmd mit "start /wait") aufgerufen, gehen die Meldungen dorthin;
        /// bei umgeleiteter Ausgabe (Pipe, Datei) bleibt die Umleitung.
        /// </summary>
        private static void KonsoleVerbinden()
        {
            Protokoll.Konsole = true;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            IntPtr aus = GetStdHandle(-11);
            if (aus != IntPtr.Zero && aus != new IntPtr(-1) && GetFileType(aus) != 2)
            {
                // In Datei oder Pipe umgeleitet: dorthin, als UTF-8.
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
                return;
            }
            // Konsole (auch geerbt): anhängen, erst dann kennt der Prozess ihre Codepage (Umlaute).
            AttachConsole(-1);
        }

        [DllImport("kernel32.dll")]
        private static extern int GetFileType(IntPtr handle);
    }
}
