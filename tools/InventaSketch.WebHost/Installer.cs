using System;
using System.Collections.Generic;
using System.IO;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    public enum StepKind
    {
        /// <summary>Stops here: a policy or a missing part prevents the rest (nothing is bypassed).</summary>
        Abort,
        Note,
        StopServer,
        CopyFiles,
        CreateCertificate,
        RenewCertificate,
        ReuseCertificate,
        TrustCertificate,
        AddAutostart,
        RemoveAutostart,
        RegisterManifest,
        UnregisterManifest,
        StartServer,
        RemoveCertificate,
        RemoveFolder,
    }

    public sealed class Step
    {
        public Step(StepKind kind, string description)
        {
            Kind = kind;
            Description = description;
        }

        public StepKind Kind { get; }

        /// <summary>User-facing description (bilingual).</summary>
        public string Description { get; }

        public override string ToString() => Kind + ": " + Description;
    }

    /// <summary>What was found on the machine (collected in <see cref="Setup"/>, set freely in tests).</summary>
    public sealed class MachineState
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
        public string? MissingPackagePart { get; set; }
        public bool ServerRunning { get; set; }
        public CertificateDecision Certificate { get; set; }
        public bool CertificateTrusted { get; set; }
        public bool RootTrustBlocked { get; set; }
        public List<string> WebAddinBlocks { get; set; } = new List<string>();
        public bool AutostartBlocked { get; set; }
        public string? AutostartValue { get; set; }
        public bool ComAddin { get; set; }
        public string? AddinId { get; set; }
        public List<string> ManifestEntries { get; set; } = new List<string>();
        public int OwnCertificates { get; set; }
        public bool TargetExists { get; set; }

        public bool SourceIsTarget =>
            string.Equals(Path.GetFullPath(Source).TrimEnd('\\', '/'), Path.GetFullPath(Target).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Plan for per-user install and uninstall on Windows (no administrator rights). Derived purely from the
    /// <see cref="MachineState"/>, so the dry run shows exactly what will happen and tests can check it.
    /// </summary>
    public static class Installer
    {
        /// <summary>Uninstall script in the package and in the installed folder.</summary>
        public const string UninstallScript = "Uninstall web add-in.cmd";

        /// <summary>Start script in the package and in the installed folder.</summary>
        public const string StartScript = "Start web add-in.cmd";

        public static string DefaultTarget =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventaSketch", "Web");

        public static string Exe(string folder) => Path.Combine(folder, "Server", "InventaSketch.WebHost.exe");
        public static string Manifest(string folder) => Path.Combine(folder, "manifest.xml");
        public static string App(string folder) => Path.Combine(folder, "App");

        /// <summary>Missing part of the package (relative path) or null.</summary>
        public static string? CheckPackage(string source)
        {
            foreach (string part in new[] { @"App\taskpane.html", @"App\_framework", @"Server\InventaSketch.WebHost.exe", "manifest.xml" })
            {
                string path = Path.Combine(source, part.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(path) && !Directory.Exists(path)) return part;
            }
            return null;
        }

        public static List<Step> Install(MachineState s, bool autostart, bool manifest, bool start)
        {
            var plan = new List<Step>();
            if (s.MissingPackagePart != null)
            {
                plan.Add(new Step(StepKind.Abort, T(
                    "Package incomplete, " + s.MissingPackagePart + " is missing. Extract the complete ZIP file.",
                    "Paket unvollständig, es fehlt " + s.MissingPackagePart + ". ZIP-Datei vollständig entpacken.")));
                return plan;
            }
            if (s.RootTrustBlocked)
            {
                plan.Add(new Step(StepKind.Abort, T(
                    "A policy does not allow users to trust their own root certificates (ProtectedRoots). Without a trusted "
                    + "certificate Office does not load the add-in. Please ask IT to host InventaSketch-Web on an HTTPS server "
                    + "(README.md, section \"For IT\").",
                    "Eine Richtlinie erlaubt Benutzern nicht, eigenen Stammzertifikaten zu vertrauen (ProtectedRoots). Ohne "
                    + "vertrautes Zertifikat lädt Office das Add-in nicht. Bitte die IT, InventaSketch-Web auf einem "
                    + "HTTPS-Server bereitzustellen (README.de.md, Abschnitt \"Für die IT\").")));
                return plan;
            }
            if (manifest && s.WebAddinBlocks.Count > 0)
            {
                string blocks = string.Join("; ", s.WebAddinBlocks);
                plan.Add(new Step(StepKind.Abort, T(
                    "A policy blocks web add-ins in Office: " + blocks + ". InventaSketch does not change policies; please ask IT.",
                    "Eine Richtlinie sperrt Web-Add-ins in Office: " + blocks + ". InventaSketch ändert keine Richtlinien; "
                    + "bitte die IT fragen.")));
                return plan;
            }
            if (manifest && s.AddinId == null)
            {
                plan.Add(new Step(StepKind.Abort, T("manifest.xml without add-in id (Id).", "manifest.xml ohne Kennung (Id).")));
                return plan;
            }
            if (s.ComAddin)
                plan.Add(new Step(StepKind.Note, T(
                    "The InventaSketch COM add-in is registered in PowerPoint; as long as it is installed, Office hides the web "
                    + "add-in. Uninstall the COM add-in first.",
                    "Das COM-Add-in von InventaSketch ist in PowerPoint eingetragen; solange es installiert ist, blendet Office "
                    + "das Web-Add-in aus. Dann zuerst das COM-Add-in deinstallieren.")));
            if (s.ServerRunning) plan.Add(new Step(StepKind.StopServer, T("Stop the running server", "Laufenden Server beenden")));
            if (!s.SourceIsTarget)
                plan.Add(new Step(StepKind.CopyFiles, T("Copy files to " + s.Target, "Dateien nach " + s.Target + " kopieren")));
            switch (s.Certificate)
            {
                case CertificateDecision.Create:
                    plan.Add(new Step(StepKind.CreateCertificate, T(
                        "Create a certificate for localhost (user certificate store, Personal)",
                        "Zertifikat für localhost anlegen (Zertifikatspeicher des Benutzers, Eigene Zertifikate)")));
                    break;
                case CertificateDecision.Renew:
                    plan.Add(new Step(StepKind.RenewCertificate, T(
                        "Certificate expires soon: create a new one, remove the old one afterwards",
                        "Zertifikat läuft bald ab: neues anlegen, altes danach entfernen")));
                    break;
                default:
                    plan.Add(new Step(StepKind.ReuseCertificate, T("Keep using the existing certificate",
                        "Vorhandenes Zertifikat weiterverwenden")));
                    break;
            }
            if (s.Certificate != CertificateDecision.Reuse || !s.CertificateTrusted)
                plan.Add(new Step(StepKind.TrustCertificate, T(
                    "Trust the certificate (Windows asks: confirm the \"Security Warning\" with Yes)",
                    "Zertifikat als vertrauenswürdig eintragen (Windows fragt nach: \"Sicherheitswarnung\" mit Ja bestätigen)")));
            if (autostart)
            {
                if (s.AutostartBlocked)
                    plan.Add(new Step(StepKind.Note, T(
                        "Autostart is blocked by a policy (DisableCurrentUserRun): after each sign-in run \"" + StartScript
                        + "\" in the folder " + s.Target + ".",
                        "Autostart ist durch eine Richtlinie gesperrt (DisableCurrentUserRun): nach jeder Anmeldung \"" + StartScript
                        + "\" im Ordner " + s.Target + " aufrufen.")));
                else
                    plan.Add(new Step(StepKind.AddAutostart, T("Add autostart at sign-in (HKCU\\" + Autostart.Key + ")",
                        "Autostart bei der Anmeldung eintragen (HKCU\\" + Autostart.Key + ")")));
            }
            else if (s.AutostartValue != null)
            {
                plan.Add(new Step(StepKind.RemoveAutostart, T("Remove autostart", "Autostart entfernen")));
            }
            if (manifest)
                plan.Add(new Step(StepKind.RegisterManifest, T("Register the add-in for PowerPoint (HKCU\\", "Add-in für PowerPoint eintragen (HKCU\\")
                    + OfficeAddins.DeveloperKey + ", " + s.AddinId + ")"));
            if (start) plan.Add(new Step(StepKind.StartServer, T("Start the server (https://localhost:3217)", "Server starten (https://localhost:3217)")));
            plan.Add(new Step(StepKind.Note, manifest
                ? T("Close and restart PowerPoint; InventaSketch appears on the InventaSketch tab (otherwise Home > Add-ins).",
                    "PowerPoint schließen und neu starten; InventaSketch erscheint im Reiter InventaSketch (sonst Start > Add-Ins).")
                : T("Upload the manifest yourself: Home > Add-ins > More Add-ins > My Add-ins > Upload My Add-in, "
                    + Manifest(s.Target) + ".",
                    "Manifest selbst hochladen: Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins > Mein Add-In hochladen, "
                    + Manifest(s.Target) + ".")));
            return plan;
        }

        public static List<Step> Uninstall(MachineState s)
        {
            var plan = new List<Step>();
            if (s.ServerRunning) plan.Add(new Step(StepKind.StopServer, T("Stop the running server", "Laufenden Server beenden")));
            if (s.AutostartValue != null) plan.Add(new Step(StepKind.RemoveAutostart, T("Remove autostart", "Autostart entfernen")));
            if (s.ManifestEntries.Count > 0)
            {
                string entries = string.Join(", ", s.ManifestEntries);
                plan.Add(new Step(StepKind.UnregisterManifest, T("Unregister the add-in from PowerPoint (" + entries + ")",
                    "Add-in aus PowerPoint austragen (" + entries + ")")));
            }
            if (s.OwnCertificates > 0)
                plan.Add(new Step(StepKind.RemoveCertificate, T(
                    "Remove the certificate for localhost (" + s.OwnCertificates
                    + " entries; Windows asks before removing from the trusted root certification authorities)",
                    "Zertifikat für localhost entfernen (" + s.OwnCertificates
                    + " Einträge; Windows fragt beim Entfernen aus den Stammzertifizierungsstellen nach)")));
            if (s.TargetExists)
                plan.Add(new Step(StepKind.RemoveFolder, T("Delete folder " + s.Target, "Ordner " + s.Target + " löschen")));
            plan.Add(new Step(StepKind.Note, plan.Count == 0
                ? T("Nothing to remove: InventaSketch-Web is not installed for this user.",
                    "Nichts zu entfernen: InventaSketch-Web ist für diesen Benutzer nicht installiert.")
                : T("Restart PowerPoint so that InventaSketch disappears from the ribbon.",
                    "PowerPoint neu starten, damit InventaSketch aus dem Menüband verschwindet.")));
            return plan;
        }
    }
}
