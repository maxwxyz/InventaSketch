using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    /// <summary>Runs the server, install and uninstall on Windows (the plans come from <see cref="Installer"/>).</summary>
    public static class Setup
    {
        /// <summary>Package folder: the folder above Server\InventaSketch.WebHost.exe.</summary>
        public static string PackageFolder =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Setup).Assembly.Location) ?? ".", ".."));

        // ------------------------------------------------------------------ Certificate

        /// <summary>
        /// Returns the certificate from CurrentUser\My (creating or renewing it if needed) and, if requested, trusts it.
        /// Trust errors are returned in <paramref name="error"/>; the certificate is returned anyway.
        /// </summary>
        public static X509Certificate2 ProvideCertificate(bool trust, out string? error)
        {
            error = null;
            List<X509Certificate2> existing = CertificateStore.Ours(StoreName.My);
            X509Certificate2? found = Certificates.SelectBest(existing);
            CertificateDecision d = Certificates.Decide(found?.NotAfter, found?.HasPrivateKey ?? false, DateTime.Now);
            X509Certificate2 best;
            if (d == CertificateDecision.Reuse && found != null)
            {
                best = found;
            }
            else
            {
                best = Certificates.Create(DateTime.Now, true);
                CertificateStore.Save(best);
                string expiry = best.NotAfter.ToString("yyyy-MM-dd");
                Log.Report(T("Certificate created: " + best.Subject + ", valid until " + expiry + ", thumbprint " + best.Thumbprint,
                    "Zertifikat angelegt: " + best.Subject + ", gültig bis " + expiry + ", Fingerabdruck " + best.Thumbprint));
            }
            if (trust && !CertificateStore.IsTrusted(best))
            {
                if (CertificateStore.RootTrustBlocked())
                    error = T("A policy does not allow trusting own root certificates (ProtectedRoots).",
                        "Eine Richtlinie erlaubt nicht, eigenen Stammzertifikaten zu vertrauen (ProtectedRoots).");
                else
                {
                    Log.Report(T("Windows now asks whether to trust the certificate \"localhost\": confirm with Yes.",
                        "Windows fragt jetzt, ob dem Zertifikat \"localhost\" vertraut werden soll: mit Ja bestätigen."));
                    error = CertificateStore.Trust(best);
                }
                if (error == null) Log.Report(T("The certificate is now trusted.", "Zertifikat ist als vertrauenswürdig eingetragen."));
            }
            if (trust && error == null)
            {
                // Clean up expired or soon-expiring own certificates once the new one is trusted.
                string current = best.Thumbprint;
                var old = CertificateStore.Ours(StoreName.My).Concat(CertificateStore.Ours(StoreName.Root))
                    .Where(c => c.Thumbprint != current
                        && Certificates.Decide(c.NotAfter, true, DateTime.Now) != CertificateDecision.Reuse)
                    .GroupBy(c => c.Thumbprint).Select(g => g.First());
                foreach (X509Certificate2 c in old)
                {
                    string expiry = c.NotAfter.ToString("yyyy-MM-dd");
                    Log.Report(T("Removing old certificate (valid until " + expiry + ").",
                        "Altes Zertifikat entfernen (gültig bis " + expiry + ")."));
                    foreach (string e in CertificateStore.Remove(c)) Log.Report(T("Note: ", "Hinweis: ") + e);
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ Server

        public static int Serve(Options o)
        {
            using (Mutex? instanceLock = SingleInstance.Acquire(o.Port))
            {
                if (instanceLock == null)
                {
                    Log.Report(T("Server is already running (port " + o.Port + ").", "Server läuft bereits (Port " + o.Port + ")."));
                    return 0;
                }
                string root = o.Folder ?? Installer.App(PackageFolder);
                if (!File.Exists(Path.Combine(root, StaticFiles.StartPage)))
                {
                    Log.Report(T("Folder without ", "Ordner ohne ") + StaticFiles.StartPage + ": " + root);
                    return 3;
                }
                X509Certificate2 certificate;
                if (o.TemporaryCertificate)
                {
                    certificate = Certificates.Create(DateTime.Now, false);
                }
                else
                {
                    certificate = ProvideCertificate(!o.NoTrust, out string? error);
                    if (error != null)
                        Log.Report(T("Warning: certificate not trusted, Office will not load the add-in. ",
                            "Warnung: Zertifikat nicht vertraut, Office lädt das Add-in dann nicht. ") + error);
                }
                using (var server = new Server(root, certificate, o.Port))
                using (EventWaitHandle stop = SingleInstance.CreateStop(o.Port))
                {
                    try { server.Start(); }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        Log.Report(T("Port " + o.Port + " is in use (" + e.Message + "). Is another server running, e.g. npm run dev?",
                            "Port " + o.Port + " ist belegt (" + e.Message + "). Läuft schon ein anderer Server, z. B. npm run dev?"));
                        return 4;
                    }
                    using (EventWaitHandle ready = SingleInstance.CreateReady(server.Port))
                    {
                        ready.Set();
                        string pid = Process.GetCurrentProcess().Id.ToString();
                        Log.Report(T("Server running: https://localhost:" + server.Port + "/ (folder " + root + ", process " + pid + ")",
                            "Server läuft: https://localhost:" + server.Port + "/ (Ordner " + root + ", Prozess " + pid + ")"));
                        stop.WaitOne();
                        ready.Reset();
                    }
                    Log.Report(T("Server stopped after " + server.Requests + " requests.",
                        "Server beendet nach " + server.Requests + " Anfragen."));
                }
                return 0;
            }
        }

        // ------------------------------------------------------------------ Install

        public static MachineState DetectState(string source, string target, int port)
        {
            var s = new MachineState { Source = source, Target = target };
            s.MissingPackagePart = Installer.CheckPackage(source);
            s.ServerRunning = SingleInstance.IsRunning(port);
            List<X509Certificate2> own = CertificateStore.Ours(StoreName.My);
            X509Certificate2? best = Certificates.SelectBest(own);
            s.Certificate = Certificates.Decide(best?.NotAfter, best?.HasPrivateKey ?? false, DateTime.Now);
            s.CertificateTrusted = best != null && CertificateStore.IsTrusted(best);
            s.OwnCertificates = own.Count + CertificateStore.Ours(StoreName.Root).Count;
            s.RootTrustBlocked = CertificateStore.RootTrustBlocked();
            s.WebAddinBlocks = OfficeAddins.Policies().Blocking;
            s.AutostartBlocked = Autostart.IsBlocked();
            s.AutostartValue = Autostart.Read();
            s.ComAddin = OfficeAddins.ComAddinRegistered();
            string manifest = Installer.Manifest(source);
            if (File.Exists(manifest)) s.AddinId = ManifestAddress.AddinId(File.ReadAllText(manifest));
            s.ManifestEntries = OfficeAddins.Entries(Installer.Manifest(target));
            s.TargetExists = Directory.Exists(target);
            return s;
        }

        /// <summary>Prints the plan and runs it (unless dry run). Returns 0, or 5 on abort.</summary>
        public static int Run(List<Step> plan, MachineState s, int port, bool dryRun)
        {
            int number = 0;
            foreach (Step step in plan)
            {
                string label = step.Kind == StepKind.Abort ? T("ABORT", "ABBRUCH")
                    : step.Kind == StepKind.Note ? T("Note", "Hinweis")
                    : (++number).ToString();
                Log.Report((dryRun ? T("[Dry run] ", "[Probelauf] ") : "") + label + ". " + step.Description);
                if (step.Kind == StepKind.Abort) return 5;
                if (dryRun) continue;
                string? error = Execute(step, s, port);
                if (error != null)
                {
                    Log.Report(T("ABORT: ", "ABBRUCH: ") + error);
                    return 5;
                }
            }
            return 0;
        }

        private static string? Execute(Step step, MachineState s, int port)
        {
            switch (step.Kind)
            {
                case StepKind.StopServer:
                    return SingleInstance.Stop(port, TimeSpan.FromSeconds(15)) ? null
                        : T("The running server does not stop (Task Manager: InventaSketch.WebHost).",
                            "Der laufende Server endet nicht (Task-Manager: InventaSketch.WebHost).");
                case StepKind.CopyFiles:
                    foreach (string part in new[] { "App", "Server" })
                    {
                        string old = Path.Combine(s.Target, part);
                        if (Directory.Exists(old)) DeleteFolder(old);
                    }
                    Copy(s.Source, s.Target);
                    return null;
                case StepKind.CreateCertificate:
                case StepKind.RenewCertificate:
                case StepKind.ReuseCertificate:
                    // Create here; trust follows in the next step.
                    ProvideCertificate(false, out _);
                    return null;
                case StepKind.TrustCertificate:
                    ProvideCertificate(true, out string? error);
                    return error == null ? null
                        : error + T(" Without a trusted certificate Office does not load the add-in.",
                            " Ohne vertrautes Zertifikat lädt Office das Add-in nicht.");
                case StepKind.AddAutostart:
                    Autostart.Add(Installer.Exe(s.Target));
                    return null;
                case StepKind.RemoveAutostart:
                    Autostart.Remove();
                    return null;
                case StepKind.RegisterManifest:
                    OfficeAddins.Register(s.AddinId!, Installer.Manifest(s.Target));
                    return null;
                case StepKind.UnregisterManifest:
                    OfficeAddins.Unregister(s.ManifestEntries);
                    return null;
                case StepKind.StartServer:
                    string exe = Installer.Exe(s.Target);
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) })?.Dispose();
                    return SingleInstance.WaitUntilReady(port, TimeSpan.FromSeconds(60)) ? null
                        : T("The server does not start; log: ", "Der Server startet nicht; Protokoll: ") + Log.DefaultFile;
                case StepKind.RemoveCertificate:
                    var errors = new List<string>();
                    foreach (StoreName name in new[] { StoreName.My, StoreName.Root })
                    {
                        foreach (X509Certificate2 c in CertificateStore.Ours(name)) errors.AddRange(CertificateStore.Remove(c));
                    }
                    foreach (string e in errors.Distinct()) Log.Report(T("Note: ", "Hinweis: ") + e);
                    return null;
                case StepKind.RemoveFolder:
                    DeleteFolder(s.Target);
                    return null;
                default:
                    return null;
            }
        }

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string folder in Directory.GetDirectories(source))
                Copy(folder, Path.Combine(target, Path.GetFileName(folder)));
        }

        /// <summary>Deletes a folder; retries briefly while a just-stopped process still holds files.</summary>
        private static void DeleteFolder(string folder)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                    return;
                }
                catch (Exception e) when ((e is IOException || e is UnauthorizedAccessException) && attempt < 20)
                {
                    Thread.Sleep(250);
                }
            }
        }
    }
}
