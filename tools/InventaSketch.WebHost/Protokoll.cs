using System;
using System.IO;
using System.Text;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Protokoll in %LOCALAPPDATA%\InventaSketch\webhost.log (bei 1 MB wird die alte Datei zu webhost.log.1) und Ausgaben an die
    /// aufrufende Konsole. Fehler beim Schreiben werden verschluckt: das Protokoll darf den Server nie anhalten.
    /// </summary>
    public static class Protokoll
    {
        private static readonly object Sperre = new object();
        private const long Grenze = 1024 * 1024;

        /// <summary>Protokolldatei; null schreibt keine Datei (Tests).</summary>
        public static string? Datei { get; set; }

        /// <summary>Ausgaben zusätzlich an die Konsole (Installieren, Status, Beenden).</summary>
        public static bool Konsole { get; set; }

        public static string StandardDatei =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventaSketch", "webhost.log");

        /// <summary>Nur ins Protokoll.</summary>
        public static void Schreiben(string text)
        {
            string? datei = Datei;
            if (datei == null) return;
            lock (Sperre)
            {
                try
                {
                    string? ordner = Path.GetDirectoryName(datei);
                    if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);
                    var info = new FileInfo(datei);
                    if (info.Exists && info.Length > Grenze)
                    {
                        string alt = datei + ".1";
                        if (File.Exists(alt)) File.Delete(alt);
                        File.Move(datei, alt);
                    }
                    File.AppendAllText(datei, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + text + Environment.NewLine,
                        new UTF8Encoding(false));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>An die Konsole (falls vorhanden) und ins Protokoll.</summary>
        public static void Melden(string text)
        {
            if (Konsole)
            {
                try { Console.WriteLine(text); }
                catch (IOException) { }
            }
            Schreiben(text);
        }
    }
}
