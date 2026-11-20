using System;
using System.Text;
using System.Text.RegularExpressions;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Manifest for your own HTTPS address: replaces https://localhost:3217. Same rules as create-manifest.ps1 and
    /// inventasketch_web.py (create-manifest.sh).
    /// </summary>
    public static class ManifestAddress
    {
        public const string Local = "https://localhost:3217";

        private static readonly Regex Shape = new Regex(@"^https://[A-Za-z0-9.\-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~\-/%]*)?$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Validates and normalises the address: must start with https://, host made of letters, digits, dot and hyphen,
        /// optional port and path, no query, fragment, spaces or special characters; trailing slashes are dropped, scheme
        /// and host lower case. Null with a reason if invalid.
        /// </summary>
        public static string? Validate(string? input, out string? error)
        {
            error = null;
            string a = (input ?? "").Trim();
            if (a.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) a = "https://" + a.Substring(8);
            while (a.EndsWith("/", StringComparison.Ordinal)) a = a.Substring(0, a.Length - 1);
            if (!a.StartsWith("https://", StringComparison.Ordinal))
            {
                error = T("The address must start with https://.", "Die Adresse muss mit https:// beginnen.");
                return null;
            }
            if (!Shape.IsMatch(a))
            {
                error = T("Invalid address (allowed: https://host[:port][/path], without ? # spaces or special characters).",
                    "Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen).");
                return null;
            }
            int path = a.IndexOf('/', 8);
            string authority = path < 0 ? a.Substring(8) : a.Substring(8, path - 8);
            string host = authority.Split(':')[0];
            if (host.Length == 0 || host.StartsWith(".", StringComparison.Ordinal) || host.EndsWith(".", StringComparison.Ordinal)
                || host.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                error = T("Invalid host name.", "Ungültiger Hostname.");
                return null;
            }
            if (authority.Contains(":") && (!int.TryParse(authority.Split(':')[1], out int port) || port < 1 || port > 65535))
            {
                error = T("Invalid port.", "Ungültiger Port.");
                return null;
            }
            return "https://" + authority.ToLowerInvariant() + (path < 0 ? "" : a.Substring(path));
        }

        /// <summary>Replaces every occurrence of the local address with the new one.</summary>
        public static string Rewrite(string manifest, string address) => manifest.Replace(Local, address);

        /// <summary>File name "manifest-host[-port].xml" for the (validated) address.</summary>
        public static string FileName(string address)
        {
            string rest = address.Substring("https://".Length);
            int path = rest.IndexOf('/');
            string authority = path < 0 ? rest : rest.Substring(0, path);
            var sb = new StringBuilder();
            foreach (char c in authority.Replace(':', '-'))
                sb.Append(char.IsLetterOrDigit(c) && c < 128 || c == '.' || c == '-' ? c : '-');
            return "manifest-" + sb + ".xml";
        }

        /// <summary>Add-in id from the manifest, or null.</summary>
        public static string? AddinId(string manifest)
        {
            Match m = Regex.Match(manifest, @"<Id>\s*([0-9A-Fa-f\-]{36})\s*</Id>");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
