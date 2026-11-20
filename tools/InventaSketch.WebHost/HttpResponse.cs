using System;
using System.Globalization;
using System.Text;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    /// <summary>Response to a request, decided without network access (testable): status, file, content type, connection.</summary>
    public sealed class HttpResponse
    {
        private HttpResponse(int status, string reason, string? file, string contentType, bool close, bool withBody, string? body)
        {
            Status = status;
            Reason = reason;
            File = file;
            ContentType = contentType;
            Close = close;
            WithBody = withBody;
            Body = body;
        }

        public int Status { get; }
        public string Reason { get; }

        /// <summary>File to serve (status 200 only).</summary>
        public string? File { get; }

        public string ContentType { get; }

        /// <summary>Close the connection after the response.</summary>
        public bool Close { get; }

        /// <summary>Send the body (GET); HEAD sends the head only.</summary>
        public bool WithBody { get; }

        /// <summary>Short text body for errors.</summary>
        public string? Body { get; }

        /// <summary>Allowed host names: the server listens on the loopback interface only (protection against DNS rebinding).</summary>
        public static bool IsHostAllowed(string? host, int port)
        {
            if (string.IsNullOrEmpty(host)) return false;
            string h = host!.Trim().ToLowerInvariant();
            string p = ":" + port.ToString(CultureInfo.InvariantCulture);
            foreach (string name in new[] { "localhost", "127.0.0.1", "[::1]" })
            {
                if (h == name || h == name + p) return true;
            }
            return false;
        }

        /// <summary>Decides the response: only GET and HEAD, only loopback hosts, only files below the root.</summary>
        public static HttpResponse Decide(HttpRequest? request, string root, int port)
        {
            if (request == null) return Error(400, "Bad Request", T("Invalid request", "Anfrage ungültig"), true, true);
            bool keepAlive = request.KeepAlive;
            bool head = request.Method == "HEAD";
            if (request.Method != "GET" && !head)
                return Error(405, "Method Not Allowed", T("Only GET and HEAD", "Nur GET und HEAD"), true, true);
            if (request.HasBody) return Error(400, "Bad Request", T("Request with body", "Anfrage mit Inhalt"), true, !head);
            string? host = request.Header("Host");
            if (host != null && !IsHostAllowed(host, port))
                return Error(400, "Bad Request", T("Host not allowed", "Host nicht zulässig"), true, !head);
            string? file = StaticFiles.Resolve(root, request.Target);
            if (file == null) return Error(404, "Not Found", T("Not found", "Nicht gefunden"), !keepAlive, !head);
            return new HttpResponse(200, "OK", file, StaticFiles.ContentType(file), !keepAlive, !head, null);
        }

        private static HttpResponse Error(int status, string reason, string body, bool close, bool withBody) =>
            new HttpResponse(status, reason, null, "text/plain; charset=utf-8", close, withBody, body);

        /// <summary>Head of the response (up to and including the empty line).</summary>
        public string Head(long length)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(Status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Reason).Append("\r\n");
            sb.Append("Content-Type: ").Append(ContentType).Append("\r\n");
            sb.Append("Content-Length: ").Append(length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("Cache-Control: no-cache\r\n");
            sb.Append("X-Content-Type-Options: nosniff\r\n");
            sb.Append("Date: ").Append(DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
            if (Status == 405) sb.Append("Allow: GET, HEAD\r\n");
            sb.Append(Close ? "Connection: close\r\n" : "Connection: keep-alive\r\n");
            sb.Append("\r\n");
            return sb.ToString();
        }

        /// <summary>Error body as bytes.</summary>
        public byte[] BodyBytes() => Body == null ? new byte[0] : Encoding.UTF8.GetBytes(Body);

        public override string ToString() => Status.ToString(CultureInfo.InvariantCulture) + " " + Reason;
    }
}
