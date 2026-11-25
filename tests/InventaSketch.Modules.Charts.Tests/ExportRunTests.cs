using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>External tools for the optional export tests (Python, matplotlib, pdflatex); found once per test run.</summary>
    internal static class Tools
    {
        private static readonly Lazy<string?> PythonExe = new(() => new[] { "python3", "python", "py" }.FirstOrDefault(p => Run(p, "-c \"import sys\"", null, 20000).Code == 0));
        private static readonly Lazy<bool> HasMatplotlib = new(() => Python is { } p && Run(p, "-c \"import matplotlib\"", null, 60000).Code == 0);
        private static readonly Lazy<bool> HasPdflatex = new(() => Run("pdflatex", "--version", null, 20000).Code == 0);

        public static string? Python => PythonExe.Value;
        public static bool Matplotlib => HasMatplotlib.Value;
        public static bool Pdflatex => HasPdflatex.Value;

        /// <summary>Runs a program; exit code -1 when it cannot be started or times out.</summary>
        public static (int Code, string Output) Run(string exe, string args, string? workDir, int timeoutMs, IDictionary<string, string>? env = null)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                    WorkingDirectory = workDir ?? Directory.GetCurrentDirectory(),
                };
                if (env is not null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
                using var p = Process.Start(psi);
                if (p is null) return (-1, "");
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch (InvalidOperationException) { } return (-1, "timeout"); }
                return (p.ExitCode, stdout.Result + stderr.Result);
            }
            catch (System.ComponentModel.Win32Exception) { return (-1, ""); }
        }

        /// <summary>A fresh temporary folder for one test.</summary>
        public static string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "inventasketch-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static SketchObject[] AllItems(Registry r) => r.Library.Where(i => i.TypeId != TimingChart.TypeId).Select(i => Fx.Item(r, i.Id)).ToArray();
    }

    /// <summary>A test that needs Python (skipped when it is not installed).</summary>
    public sealed class PythonFactAttribute : FactAttribute
    {
        public PythonFactAttribute() { if (Tools.Python is null) Skip = "Python is not installed."; }
    }

    /// <summary>A test that needs Python with matplotlib (skipped otherwise).</summary>
    public sealed class MatplotlibFactAttribute : FactAttribute
    {
        public MatplotlibFactAttribute() { if (!Tools.Matplotlib) Skip = "matplotlib is not installed."; }
    }

    /// <summary>A test that needs pdflatex with pgfplots (skipped otherwise).</summary>
    public sealed class PdflatexFactAttribute : FactAttribute
    {
        public PdflatexFactAttribute() { if (!Tools.Pdflatex) Skip = "pdflatex is not installed."; }
    }

    /// <summary>Optional checks of the exports with the real tools: Python syntax, matplotlib rendering, pdflatex.</summary>
    public class ExportRunTests
    {
        [PythonFact]
        public void GeneratedScriptsAreValidPython()
        {
            var r = Fx.Reg();
            var dir = Tools.TempDir();
            try
            {
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var file = Path.Combine(dir, "charts-" + lang + ".py");
                    File.WriteAllText(file, ExportTests.Py(r, lang, Tools.AllItems(r)));
                    var (code, output) = Tools.Run(Tools.Python!, "-c \"import ast, sys; ast.parse(open(sys.argv[1], encoding='utf-8').read())\" \"" + file + "\"", dir, 60000);
                    Assert.True(code == 0, lang + ": " + output);
                }
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }

        [MatplotlibFact]
        public void GeneratedScriptsRunHeadless()
        {
            var r = Fx.Reg();
            var dir = Tools.TempDir();
            try
            {
                var items = Tools.AllItems(r);
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var file = Path.Combine(dir, "charts-" + lang + ".py");
                    File.WriteAllText(file, ExportTests.Py(r, lang, items));
                    var output = Path.Combine(dir, "out-" + lang + ".png");
                    var (code, log) = Tools.Run(Tools.Python!, "\"" + file + "\" \"" + output + "\"", dir, 300000, new Dictionary<string, string> { ["MPLBACKEND"] = "Agg" });
                    Assert.True(code == 0, lang + ": " + log);
                    for (var i = 1; i <= items.Length; i++)
                        Assert.True(File.Exists(Path.Combine(dir, "out-" + lang + "-" + i + ".png")), lang + ": figure " + i + " (" + items[i - 1].Type + ") not saved");
                }
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }

        [PdflatexFact]
        public void GeneratedDocumentsCompile()
        {
            var r = Fx.Reg();
            var dir = Tools.TempDir();
            try
            {
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var name = "charts-" + lang;
                    File.WriteAllText(Path.Combine(dir, name + ".tex"), ExportTests.Tex(r, lang, Tools.AllItems(r)));
                    var (code, log) = Tools.Run("pdflatex", "-interaction=nonstopmode -halt-on-error " + name + ".tex", dir, 600000);
                    Assert.True(code == 0, lang + ": " + (log.Length > 4000 ? log.Substring(log.Length - 4000) : log));
                    Assert.True(File.Exists(Path.Combine(dir, name + ".pdf")));
                }
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }
    }
}
