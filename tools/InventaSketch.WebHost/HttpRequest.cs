using System;
using System.Collections.Generic;
using System.Globalization;

namespace InventaSketch.WebHost
{
    /// <summary>Head of an HTTP/1.x request (request line and header fields, without body).</summary>
    public sealed class HttpRequest
    {
        /// <summary>Largest allowed head size in bytes.</summary>
        public const int MaxHeadSize = 16 * 1024;

        private HttpRequest(string method, string target, string version, Dictionary<string, string> headers)
        {
            Method = method;
            Target = target;
            Version = version;
            Headers = headers;
        }

        public string Method { get; }
        public string Target { get; }
        public string Version { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        public string? Header(string name) => Headers.TryGetValue(name, out string? value) ? value : null;

        /// <summary>Keep the connection open: HTTP/1.1 without "Connection: close" or HTTP/1.0 with "keep-alive".</summary>
        public bool KeepAlive
        {
            get
            {
                string connection = (Header("Connection") ?? "").ToLowerInvariant();
                return Version == "HTTP/1.1" ? connection.IndexOf("close", StringComparison.Ordinal) < 0
                    : connection.IndexOf("keep-alive", StringComparison.Ordinal) >= 0;
            }
        }

        /// <summary>The request carries a body (which the server does not read).</summary>
        public bool HasBody
        {
            get
            {
                if (Header("Transfer-Encoding") != null) return true;
                string? length = Header("Content-Length");
                return length != null && (!long.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out long n) || n != 0);
            }
        }

        /// <summary>
        /// Parses the head (without the final empty line). Returns null and a reason if it does not match the format
        /// (request line with three parts, version HTTP/1.0 or HTTP/1.1, header fields "Name: value").
        /// </summary>
        public static HttpRequest? Parse(string head, out string? error)
        {
            error = null;
            string[] lines = head.Replace("\r\n", "\n").Split('\n');
            string[] parts = lines[0].Split(' ');
            if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                error = "invalid request line";
                return null;
            }
            foreach (char c in parts[0])
            {
                if (c < 'A' || c > 'Z') { error = "invalid method"; return null; }
            }
            if (parts[2] != "HTTP/1.1" && parts[2] != "HTTP/1.0")
            {
                error = "version not supported";
                return null;
            }
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                int p = line.IndexOf(':');
                if (p <= 0 || line[0] == ' ' || line[0] == '\t' || line.Substring(0, p).IndexOf(' ') >= 0)
                {
                    error = "invalid header field";
                    return null;
                }
                string name = line.Substring(0, p);
                string value = line.Substring(p + 1).Trim();
                headers[name] = headers.TryGetValue(name, out string? previous) ? previous + ", " + value : value;
            }
            if (parts[2] == "HTTP/1.1" && !headers.ContainsKey("Host"))
            {
                error = "Host missing";
                return null;
            }
            return new HttpRequest(parts[0], parts[1], parts[2], headers);
        }
    }
}
