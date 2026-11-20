using System;
using System.Collections.Generic;
using System.IO;

namespace InventaSketch.WebHost
{
    public enum SchrittArt
    {
        /// <summary>Hält an: eine Richtlinie oder ein fehlender Teil verhindert den Rest (nichts wird umgangen).</summary>
        Abbruch,
        Hinweis,
        ServerBeenden,
        DateienKopieren,
        ZertifikatAnlegen,
        ZertifikatErneuern,
        ZertifikatWiederverwenden,
        ZertifikatVertrauen,
        AutostartEintragen,
        AutostartEntfernen,
        ManifestRegistrieren,
        ManifestAbmelden,
        ServerStarten,
        ZertifikatEntfernen,
        OrdnerEntfernen,
    }

    public sealed class Schritt
    {
        public Schritt(SchrittArt art, string text)
        {
            Art = art;
            Text = text;
        }

        public SchrittArt Art { get; }
        public string Text { get; }
        public override string ToString() => Art + ": " + Text;
    }

    /// <summary>Was auf dem Rechner vorgefunden wurde (gesammelt in <see cref="Ablauf"/>, in Tests frei gesetzt).</summary>
    public sealed class Zustand
    {
        public string Quelle { get; set; } = "";
        public string Ziel { get; set; } = "";
        public string? PaketFehlt { get; set; }
        public bool ServerLaeuft { get; set; }
        public ZertifikatEntscheidung Zertifikat { get; set; }
        public bool ZertifikatVertraut { get; set; }
        public bool StammGesperrt { get; set; }
        public List<string> WebAddinSperren { get; set; } = new List<string>();
        public bool AutostartGesperrt { get; set; }
        public string? AutostartWert { get; set; }
        public bool ComAddin { get; set; }
        public string? Kennung { get; set; }
        public List<string> ManifestEintraege { get; set; } = new List<string>();
        public int EigeneZertifikate { get; set; }
        public bool ZielVorhanden { get; set; }

        public bool QuelleIstZiel =>
            string.Equals(Path.GetFullPath(Quelle).TrimEnd('\\', '/'), Path.GetFullPath(Ziel).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Plan für Installieren und Deinstallieren je Benutzer unter Windows (ohne Administratorrechte). Rein aus dem
    /// <see cref="Zustand"/> bestimmt, damit der Probelauf genau zeigt, was geschieht, und die Tests ihn prüfen können.
    /// </summary>
    public static class Installation
    {
        public static string ZielStandard =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventaSketch", "Web");

        public static string Exe(string ordner) => Path.Combine(ordner, "Server", "InventaSketch.WebHost.exe");
        public static string Manifest(string ordner) => Path.Combine(ordner, "manifest.xml");
        public static string App(string ordner) => Path.Combine(ordner, "App");

        /// <summary>Fehlender Teil des Pakets (relativ) oder null.</summary>
        public static string? PaketPruefen(string quelle)
        {
            foreach (string teil in new[] { @"App\taskpane.html", @"App\_framework", @"Server\InventaSketch.WebHost.exe", "manifest.xml" })
            {
                string pfad = Path.Combine(quelle, teil.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(pfad) && !Directory.Exists(pfad)) return teil;
            }
            return null;
        }

        public static List<Schritt> Installieren(Zustand z, bool autostart, bool manifest, bool starten)
        {
            var plan = new List<Schritt>();
            if (z.PaketFehlt != null)
            {
                plan.Add(new Schritt(SchrittArt.Abbruch, "Paket unvollständig, es fehlt " + z.PaketFehlt + ". ZIP-Datei vollständig entpacken."));
                return plan;
            }
            if (z.StammGesperrt)
            {
                plan.Add(new Schritt(SchrittArt.Abbruch, "Eine Richtlinie erlaubt Benutzern nicht, eigenen Stammzertifikaten zu vertrauen "
                    + "(ProtectedRoots). Ohne vertrautes Zertifikat lädt Office das Add-in nicht. Bitte die IT, InventaSketch-Web auf "
                    + "einem HTTPS-Server bereitzustellen (LIESMICH.md, Abschnitt \"Für die IT\")."));
                return plan;
            }
            if (manifest && z.WebAddinSperren.Count > 0)
            {
                plan.Add(new Schritt(SchrittArt.Abbruch, "Eine Richtlinie sperrt Web-Add-ins in Office: "
                    + string.Join("; ", z.WebAddinSperren) + ". InventaSketch ändert keine Richtlinien; bitte die IT fragen."));
                return plan;
            }
            if (manifest && z.Kennung == null)
            {
                plan.Add(new Schritt(SchrittArt.Abbruch, "manifest.xml ohne Kennung (Id)."));
                return plan;
            }
            if (z.ComAddin)
                plan.Add(new Schritt(SchrittArt.Hinweis, "Das COM-Add-in von InventaSketch ist in PowerPoint eingetragen; solange es "
                    + "installiert ist, blendet Office das Web-Add-in aus. Dann zuerst das COM-Add-in deinstallieren."));
            if (z.ServerLaeuft) plan.Add(new Schritt(SchrittArt.ServerBeenden, "Laufenden Server beenden"));
            if (!z.QuelleIstZiel) plan.Add(new Schritt(SchrittArt.DateienKopieren, "Dateien nach " + z.Ziel + " kopieren"));
            switch (z.Zertifikat)
            {
                case ZertifikatEntscheidung.Neu:
                    plan.Add(new Schritt(SchrittArt.ZertifikatAnlegen, "Zertifikat für localhost anlegen (Zertifikatspeicher des Benutzers, Eigene Zertifikate)"));
                    break;
                case ZertifikatEntscheidung.Erneuern:
                    plan.Add(new Schritt(SchrittArt.ZertifikatErneuern, "Zertifikat läuft bald ab: neues anlegen, altes danach entfernen"));
                    break;
                default:
                    plan.Add(new Schritt(SchrittArt.ZertifikatWiederverwenden, "Vorhandenes Zertifikat weiterverwenden"));
                    break;
            }
            if (z.Zertifikat != ZertifikatEntscheidung.Wiederverwenden || !z.ZertifikatVertraut)
                plan.Add(new Schritt(SchrittArt.ZertifikatVertrauen, "Zertifikat als vertrauenswürdig eintragen (Windows fragt nach: "
                    + "\"Sicherheitswarnung\" mit Ja bestätigen)"));
            if (autostart)
            {
                if (z.AutostartGesperrt)
                    plan.Add(new Schritt(SchrittArt.Hinweis, "Autostart ist durch eine Richtlinie gesperrt (DisableCurrentUserRun): "
                        + "nach jeder Anmeldung \"Web-Add-in starten.cmd\" im Ordner " + z.Ziel + " aufrufen."));
                else
                    plan.Add(new Schritt(SchrittArt.AutostartEintragen, "Autostart bei der Anmeldung eintragen (HKCU\\" + Autostart.Schluessel + ")"));
            }
            else if (z.AutostartWert != null)
            {
                plan.Add(new Schritt(SchrittArt.AutostartEntfernen, "Autostart entfernen"));
            }
            if (manifest)
                plan.Add(new Schritt(SchrittArt.ManifestRegistrieren, "Add-in für PowerPoint eintragen (HKCU\\" + OfficeAddins.Entwickler
                    + ", " + z.Kennung + ")"));
            if (starten) plan.Add(new Schritt(SchrittArt.ServerStarten, "Server starten (https://localhost:3217)"));
            plan.Add(new Schritt(SchrittArt.Hinweis, manifest
                ? "PowerPoint schließen und neu starten; InventaSketch erscheint im Reiter InventaSketch (sonst Start > Add-Ins)."
                : "Manifest selbst hochladen: Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins > Mein Add-In hochladen, "
                  + Manifest(z.Ziel) + "."));
            return plan;
        }

        public static List<Schritt> Deinstallieren(Zustand z)
        {
            var plan = new List<Schritt>();
            if (z.ServerLaeuft) plan.Add(new Schritt(SchrittArt.ServerBeenden, "Laufenden Server beenden"));
            if (z.AutostartWert != null) plan.Add(new Schritt(SchrittArt.AutostartEntfernen, "Autostart entfernen"));
            if (z.ManifestEintraege.Count > 0)
                plan.Add(new Schritt(SchrittArt.ManifestAbmelden, "Add-in aus PowerPoint austragen (" + string.Join(", ", z.ManifestEintraege) + ")"));
            if (z.EigeneZertifikate > 0)
                plan.Add(new Schritt(SchrittArt.ZertifikatEntfernen, "Zertifikat für localhost entfernen (" + z.EigeneZertifikate
                    + " Einträge; Windows fragt beim Entfernen aus den Stammzertifizierungsstellen nach)"));
            if (z.ZielVorhanden) plan.Add(new Schritt(SchrittArt.OrdnerEntfernen, "Ordner " + z.Ziel + " löschen"));
            plan.Add(new Schritt(SchrittArt.Hinweis, plan.Count == 0
                ? "Nichts zu entfernen: InventaSketch-Web ist für diesen Benutzer nicht installiert."
                : "PowerPoint neu starten, damit InventaSketch aus dem Menüband verschwindet."));
            return plan;
        }
    }
}
