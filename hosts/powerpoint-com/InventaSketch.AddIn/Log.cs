using System;
using System.IO;

namespace InventaSketch.AddIn
{
    /// <summary>Minimal file log in %LOCALAPPDATA%\InventaSketch\addin.log (rotated at 2 MB). Never throws.</summary>
    internal static class Log
    {
        private static readonly object Gate = new();
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventaSketch");
        public static string File => Path.Combine(Folder, "addin.log");

        public static void Info(string message) => Write("INFO ", message);

        public static void Error(string context, Exception ex) => Write("ERROR", context + ": " + ex);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    var fi = new FileInfo(File);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024) System.IO.File.Copy(File, File + ".1", overwrite: true);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024) System.IO.File.Delete(File);
                    System.IO.File.AppendAllText(File, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never break PowerPoint.
            }
        }
    }
}
