using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Entry point. Exit codes: 0 success, 1 server not running (status, stop), 2 invalid arguments, 3 folder without task
    /// pane, 4 port in use, 5 installation aborted (policy, rejected certificate, incomplete package).
    /// </summary>
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int handle);

        [DllImport("kernel32.dll")]
        private static extern int GetFileType(IntPtr handle);

        public static int Main(string[] args)
        {
            Options o = Options.Parse(args);
            Log.FilePath = Log.DefaultFile;
            if (o.Command != Command.Serve || o.Error != null) AttachToConsole();
            if (o.Error != null)
            {
                Log.Report(o.Error);
                Log.Report(Options.HelpText);
                return 2;
            }
            try
            {
                switch (o.Command)
                {
                    case Command.Help:
                        Log.ToConsole = true;
                        Console.WriteLine(Options.HelpText);
                        return 0;
                    case Command.Status:
                        if (SingleInstance.WaitUntilReady(o.Port, TimeSpan.FromSeconds(o.Wait)))
                        {
                            Log.Report(T("InventaSketch-Web is running: ", "InventaSketch-Web läuft: ")
                                + "https://localhost:" + o.Port + "/taskpane.html");
                            return 0;
                        }
                        Log.Report(T("InventaSketch-Web is not running (port ", "InventaSketch-Web läuft nicht (Port ") + o.Port
                            + T("). Log: ", "). Protokoll: ") + Log.DefaultFile);
                        return 1;
                    case Command.Stop:
                        if (SingleInstance.Stop(o.Port, TimeSpan.FromSeconds(15)))
                        {
                            Log.Report(T("InventaSketch-Web stopped.", "InventaSketch-Web beendet."));
                            return 0;
                        }
                        Log.Report(T("No server running on port " + o.Port + " (or it does not stop).",
                            "Kein laufender Server auf Port " + o.Port + " (oder er endet nicht)."));
                        return 1;
                    case Command.Manifest:
                        return WriteManifest(o);
                    case Command.Install:
                    {
                        string source = Path.GetFullPath(o.Source ?? Setup.PackageFolder);
                        string target = Path.GetFullPath(o.Target ?? Installer.DefaultTarget);
                        Log.Report(T("Installing InventaSketch-Web for " + Environment.UserName + " (no administrator rights)",
                            "InventaSketch-Web installieren für " + Environment.UserName + " (ohne Administratorrechte)"));
                        MachineState state = Setup.DetectState(source, target, o.Port);
                        int rc = Setup.Run(Installer.Install(state, o.Autostart, !o.NoManifest, !o.NoStart), state, o.Port, o.DryRun);
                        if (rc == 0 && !o.DryRun)
                        {
                            string uninstall = "\"" + Path.Combine(target, Installer.UninstallScript) + "\"";
                            Log.Report(T("Done. Check in the browser: https://localhost:" + o.Port + "/taskpane.html; uninstall: "
                                    + uninstall + ".",
                                "Fertig. Prüfen im Browser: https://localhost:" + o.Port + "/taskpane.html; Deinstallieren: "
                                    + uninstall + "."));
                        }
                        return rc;
                    }
                    case Command.Uninstall:
                    {
                        string target = Path.GetFullPath(o.Target ?? Installer.DefaultTarget);
                        Log.Report(T("Uninstalling InventaSketch-Web for ", "InventaSketch-Web deinstallieren für ") + Environment.UserName);
                        MachineState state = Setup.DetectState(target, target, o.Port);
                        return Setup.Run(Installer.Uninstall(state), state, o.Port, o.DryRun);
                    }
                    default:
                        return Setup.Serve(o);
                }
            }
            catch (Exception e)
            {
                Log.Report(T("Error: ", "Fehler: ") + e.Message);
                Log.Write(e.ToString());
                return 5;
            }
        }

        private static int WriteManifest(Options o)
        {
            string? address = ManifestAddress.Validate(o.Address, out string? error);
            if (address == null)
            {
                Log.Report(error ?? T("Invalid address.", "Adresse ungültig."));
                return 2;
            }
            string source = Installer.Manifest(o.Source ?? Setup.PackageFolder);
            string target = Path.Combine(o.Target ?? Path.GetDirectoryName(source) ?? ".", ManifestAddress.FileName(address));
            File.WriteAllText(target, ManifestAddress.Rewrite(File.ReadAllText(source, Encoding.UTF8), address), new UTF8Encoding(false));
            Log.Report(T("Manifest for ", "Manifest für ") + address + ": " + target);
            return 0;
        }

        /// <summary>
        /// The program has no window. Started from a console (cmd with "start /wait"), messages go there; redirected
        /// output (pipe, file) stays redirected.
        /// </summary>
        private static void AttachToConsole()
        {
            Log.ToConsole = true;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            IntPtr stdout = GetStdHandle(-11);
            if (stdout != IntPtr.Zero && stdout != new IntPtr(-1) && GetFileType(stdout) != 2)
            {
                // Redirected to a file or pipe: write there, as UTF-8.
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
                return;
            }
            // Console (possibly inherited): attach first, only then does the process know its code page (umlauts).
            AttachConsole(-1);
        }
    }
}
