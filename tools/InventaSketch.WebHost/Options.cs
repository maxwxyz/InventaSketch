using System;
using System.Globalization;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    public enum Command
    {
        /// <summary>Run the server (default, also used by the autostart entry).</summary>
        Serve,
        Stop,
        Status,
        Install,
        Uninstall,
        Manifest,
        Help,
    }

    /// <summary>
    /// Command line. Switches start with "-", "--" or "/"; case and hyphens inside the name are ignored
    /// ("--no-autostart" = "-NoAutostart"). The German names of the original host remain accepted as aliases
    /// ("-OhneAutostart", "-OhneManifest", "-Probelauf", ...).
    /// </summary>
    public sealed class Options
    {
        public Command Command { get; private set; } = Command.Serve;
        public string? Folder { get; private set; }
        public int Port { get; private set; } = 3217;
        public bool NoTrust { get; private set; }
        public bool TemporaryCertificate { get; private set; }
        public bool Autostart { get; private set; } = true;
        public bool NoManifest { get; private set; }
        public bool NoStart { get; private set; }
        public bool DryRun { get; private set; }
        public string? Target { get; private set; }
        public string? Source { get; private set; }
        public int Wait { get; private set; }
        public string? Address { get; private set; }
        public string? Error { get; private set; }

        public static string HelpText => T(
            "InventaSketch.WebHost: local HTTPS server for the web add-in (https://localhost:3217)\n" +
            "  (no command)           run the server (folder App next to Server)\n" +
            "  --install              install for this user: certificate, autostart, manifest, start\n" +
            "  --uninstall            remove everything again\n" +
            "  --stop | --status      stop or check the running server (--wait <seconds>)\n" +
            "  --manifest <https://address>  write a manifest for your own server\n" +
            "Options: --folder <path> --port <n> --no-autostart --no-manifest --no-start --no-trust\n" +
            "         --temporary-certificate --target <path> --source <path> --dry-run\n" +
            "German aliases: --installieren --deinstallieren --beenden --hilfe --ordner --ziel --quelle --warten\n" +
            "         --ohne-autostart --ohne-manifest --ohne-start --ohne-vertrauen --temporaeres-zertifikat --probelauf",
            "InventaSketch.WebHost: lokaler HTTPS-Server für das Web-Add-in (https://localhost:3217)\n" +
            "  (ohne Befehl)          Server starten (Ordner App neben Server)\n" +
            "  --install              für diesen Benutzer installieren: Zertifikat, Autostart, Manifest, Start\n" +
            "  --uninstall            alles wieder entfernen\n" +
            "  --stop | --status      laufenden Server beenden bzw. prüfen (--wait <Sekunden>)\n" +
            "  --manifest <https://adresse>  Manifest für einen eigenen Server schreiben\n" +
            "Optionen: --folder <pfad> --port <n> --no-autostart --no-manifest --no-start --no-trust\n" +
            "          --temporary-certificate --target <pfad> --source <pfad> --dry-run\n" +
            "Deutsche Namen: --installieren --deinstallieren --beenden --hilfe --ordner --ziel --quelle --warten\n" +
            "          --ohne-autostart --ohne-manifest --ohne-start --ohne-vertrauen --temporaeres-zertifikat --probelauf");

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string raw = args[i];
                if (raw.Length < 2 || (raw[0] != '-' && raw[0] != '/'))
                {
                    o.Error = T("Unknown argument: ", "Unbekannte Angabe: ") + raw;
                    return o;
                }
                string name = raw.TrimStart('-', '/').Replace("-", "").ToLowerInvariant();
                string? Value()
                {
                    if (i + 1 >= args.Length)
                    {
                        o.Error = raw + T(" needs a value.", " braucht einen Wert.");
                        return null;
                    }
                    return args[++i];
                }
                switch (name)
                {
                    case "install": case "installieren": o.Command = Command.Install; break;
                    case "uninstall": case "deinstallieren": o.Command = Command.Uninstall; break;
                    case "stop": case "beenden": o.Command = Command.Stop; break;
                    case "status": o.Command = Command.Status; break;
                    case "help": case "hilfe": case "h": case "?": o.Command = Command.Help; break;
                    case "manifest": o.Command = Command.Manifest; o.Address = Value(); break;
                    case "folder": case "ordner": o.Folder = Value(); break;
                    case "target": case "ziel": o.Target = Value(); break;
                    case "source": case "quelle": o.Source = Value(); break;
                    case "port":
                    case "wait":
                    case "warten":
                        string? v = Value();
                        if (v == null) return o;
                        if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n > 65535)
                        {
                            o.Error = raw + T(": number expected.", ": Zahl erwartet.");
                            return o;
                        }
                        if (name == "port") o.Port = n; else o.Wait = n;
                        break;
                    case "autostart": o.Autostart = true; break;
                    case "noautostart": case "ohneautostart": o.Autostart = false; break;
                    case "nomanifest": case "ohnemanifest": o.NoManifest = true; break;
                    case "nostart": case "ohnestart": o.NoStart = true; break;
                    case "notrust": case "ohnevertrauen": o.NoTrust = true; break;
                    case "temporarycertificate": case "temporaereszertifikat": o.TemporaryCertificate = true; break;
                    case "dryrun": case "whatif": case "probelauf": o.DryRun = true; break;
                    default:
                        o.Error = T("Unknown argument: ", "Unbekannte Angabe: ") + raw;
                        return o;
                }
                if (o.Error != null) return o;
            }
            return o;
        }
    }
}
