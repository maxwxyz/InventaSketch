using System;
using System.Collections.Generic;
using System.IO;

namespace InventaSketch.WebHost
{
    /// <summary>Maps request paths to files below a root folder, and file extensions to content types.</summary>
    public static class StaticFiles
    {
        /// <summary>File served for "/" (like scripts/serve.mjs).</summary>
        public const string StartPage = "taskpane.html";

        private static readonly Dictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".htm"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".mjs"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".map"] = "application/json; charset=utf-8",
            [".wasm"] = "application/wasm",
            [".dat"] = "application/octet-stream",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon",
            [".xml"] = "application/xml; charset=utf-8",
            [".txt"] = "text/plain; charset=utf-8",
            [".md"] = "text/markdown; charset=utf-8",
            [".pdf"] = "application/pdf",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
        };

        /// <summary>Content type by extension; unknown extensions as application/octet-stream.</summary>
        public static string ContentType(string file)
        {
            string extension = Path.GetExtension(file);
            return Types.TryGetValue(extension, out string? type) ? type : "application/octet-stream";
        }

        /// <summary>
        /// Splits the request target (only "/path"; query and fragment are dropped) into path segments. Null if the target
        /// could lead outside the root or means something other than a file on Windows: "..", "\", ":" (drive, alternate
        /// data stream), control characters, segments ending in a dot or space. Empty: start page.
        /// </summary>
        public static string[]? Split(string target)
        {
            if (target.Length == 0 || target[0] != '/') return null;
            int end = target.IndexOfAny(new[] { '?', '#' });
            string path = end >= 0 ? target.Substring(0, end) : target;
            string decoded;
            try { decoded = Uri.UnescapeDataString(path); }
            catch (UriFormatException) { return null; }
            var segments = new List<string>();
            foreach (string segment in decoded.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == ".") continue;
                if (segment == "..") return null;
                foreach (char c in segment)
                {
                    if (c < 0x20 || c == 0x7f || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                        return null;
                }
                char last = segment[segment.Length - 1];
                if (last == '.' || last == ' ') return null;
                segments.Add(segment);
            }
            return segments.ToArray();
        }

        /// <summary>
        /// Full path of the file for the request target, or null (not allowed or not present). A folder yields its
        /// index.html, "/" the start page. The result always lies below <paramref name="root"/>.
        /// </summary>
        public static string? Resolve(string root, string target)
        {
            string[]? segments = Split(target);
            if (segments == null) return null;
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string file;
            try
            {
                file = segments.Length == 0 ? Path.Combine(basePath, StartPage)
                    : Path.GetFullPath(Path.Combine(basePath, string.Join(Path.DirectorySeparatorChar.ToString(), segments)));
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return null;
            }
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!file.StartsWith(basePath + Path.DirectorySeparatorChar, comparison)) return null;
            if (Directory.Exists(file)) file = Path.Combine(file, "index.html");
            return File.Exists(file) ? file : null;
        }
    }
}
