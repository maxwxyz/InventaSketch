using System;
using System.IO;
using System.Text;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Log in %LOCALAPPDATA%\InventaSketch\webhost.log (at 1 MB the old file becomes webhost.log.1) plus output to the
    /// calling console. Write errors are swallowed: logging must never stop the server.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private const long MaxSize = 1024 * 1024;

        /// <summary>Log file; null writes no file (tests).</summary>
        public static string? FilePath { get; set; }

        /// <summary>Also write messages to the console (install, status, stop).</summary>
        public static bool ToConsole { get; set; }

        public static string DefaultFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventaSketch", "webhost.log");

        /// <summary>Writes to the log file only.</summary>
        public static void Write(string text)
        {
            string? file = FilePath;
            if (file == null) return;
            lock (Gate)
            {
                try
                {
                    string? folder = Path.GetDirectoryName(file);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                    var info = new FileInfo(file);
                    if (info.Exists && info.Length > MaxSize)
                    {
                        string old = file + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(file, old);
                    }
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + text + Environment.NewLine,
                        new UTF8Encoding(false));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>Writes to the console (if attached) and to the log file.</summary>
        public static void Report(string text)
        {
            if (ToConsole)
            {
                try { Console.WriteLine(text); }
                catch (IOException) { }
            }
            Write(text);
        }
    }
}
