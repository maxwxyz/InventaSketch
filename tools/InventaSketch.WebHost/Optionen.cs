using System;
using System.Globalization;

namespace InventaSketch.WebHost
{
    public enum Befehl
    {
        /// <summary>Server betreiben (Standard, auch für den Autostart).</summary>
        Starten,
        Beenden,
        Status,
        Installieren,
        Deinstallieren,
        Manifest,
        Hilfe,
    }

    /// <summary>
    /// Befehlszeile. Schalter mit "-", "--" oder "/", Groß- und Kleinschreibung und Bindestriche im Namen gleichgültig
    /// ("-OhneAutostart" = "--ohne-autostart").
    /// </summary>
    public sealed class Optionen
    {
        public Befehl Befehl { get; private set; } = Befehl.Starten;
        public string? Ordner { get; private set; }
        public int Port { get; private set; } = 3217;
        public bool OhneVertrauen { get; private set; }
        public bool TemporaeresZertifikat { get; private set; }
        public bool Autostart { get; private set; } = true;
        public bool OhneManifest { get; private set; }
        public bool OhneStart { get; private set; }
        public bool Probelauf { get; private set; }
        public string? Ziel { get; private set; }
        public string? Quelle { get; private set; }
        public int Warten { get; private set; }
        public string? Adresse { get; private set; }
        public string? Fehler { get; private set; }

        public const string Hilfetext =
            "InventaSketch.WebHost: lokaler HTTPS-Server für das Web-Add-in (https://localhost:3217)\n" +
            "  (ohne Befehl)          Server starten (Ordner App neben Server)\n" +
            "  --installieren         für diesen Benutzer installieren, Zertifikat, Autostart, Manifest, Start\n" +
            "  --deinstallieren       alles wieder entfernen\n" +
            "  --beenden | --status   laufenden Server beenden bzw. prüfen (--warten <Sekunden>)\n" +
            "  --manifest <https://adresse>  Manifest für einen eigenen Server schreiben\n" +
            "Optionen: --ordner <pfad> --port <n> --ohne-autostart --ohne-manifest --ohne-start --ohne-vertrauen\n" +
            "          --temporaeres-zertifikat --ziel <pfad> --quelle <pfad> --probelauf";

        public static Optionen Lesen(string[] args)
        {
            var o = new Optionen();
            for (int i = 0; i < args.Length; i++)
            {
                string roh = args[i];
                if (roh.Length < 2 || (roh[0] != '-' && roh[0] != '/'))
                {
                    o.Fehler = "Unbekannte Angabe: " + roh;
                    return o;
                }
                string name = roh.TrimStart('-', '/').Replace("-", "").ToLowerInvariant();
                string? Wert()
                {
                    if (i + 1 >= args.Length) { o.Fehler = roh + " braucht einen Wert."; return null; }
                    return args[++i];
                }
                switch (name)
                {
                    case "installieren": case "install": o.Befehl = Befehl.Installieren; break;
                    case "deinstallieren": case "uninstall": o.Befehl = Befehl.Deinstallieren; break;
                    case "beenden": case "stop": o.Befehl = Befehl.Beenden; break;
                    case "status": o.Befehl = Befehl.Status; break;
                    case "hilfe": case "help": case "h": case "?": o.Befehl = Befehl.Hilfe; break;
                    case "manifest": o.Befehl = Befehl.Manifest; o.Adresse = Wert(); break;
                    case "ordner": o.Ordner = Wert(); break;
                    case "ziel": o.Ziel = Wert(); break;
                    case "quelle": o.Quelle = Wert(); break;
                    case "port":
                    case "warten":
                        string? w = Wert();
                        if (w == null) return o;
                        if (!int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n > 65535)
                        {
                            o.Fehler = roh + ": Zahl erwartet.";
                            return o;
                        }
                        if (name == "port") o.Port = n; else o.Warten = n;
                        break;
                    case "autostart": o.Autostart = true; break;
                    case "ohneautostart": o.Autostart = false; break;
                    case "ohnemanifest": o.OhneManifest = true; break;
                    case "ohnestart": o.OhneStart = true; break;
                    case "ohnevertrauen": o.OhneVertrauen = true; break;
                    case "temporaereszertifikat": o.TemporaeresZertifikat = true; break;
                    case "probelauf": case "dryrun": case "whatif": o.Probelauf = true; break;
                    default:
                        o.Fehler = "Unbekannte Angabe: " + roh;
                        return o;
                }
                if (o.Fehler != null) return o;
            }
            return o;
        }
    }
}
