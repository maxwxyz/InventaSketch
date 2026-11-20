using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// HTTPS server for a folder of static files. Listens on 127.0.0.1 and ::1 only (TcpListener with SslStream: no
    /// administrator rights and no URL reservation, unlike HttpListener with https). HTTP/1.1 with persistent
    /// connections, GET and HEAD only.
    /// </summary>
    public sealed class Server : IDisposable
    {
        private readonly string root;
        private readonly X509Certificate2 certificate;
        private readonly List<TcpListener> listeners = new List<TcpListener>();
        private volatile bool stopped;
        private int requests;

        public Server(string root, X509Certificate2 certificate, int port)
        {
            this.root = Path.GetFullPath(root);
            this.certificate = certificate;
            Port = port;
        }

        /// <summary>Port; after the start the one actually bound (assigned by the system for 0).</summary>
        public int Port { get; private set; }

        /// <summary>Timeout for handshake and reads per connection.</summary>
        public int Timeout { get; set; } = 30000;

        public int Requests => requests;

        /// <summary>Starts listening; throws SocketException if the port is in use on 127.0.0.1.</summary>
        public void Start()
        {
            var v4 = new TcpListener(IPAddress.Loopback, Port);
            MakeExclusive(v4);
            v4.Start();
            listeners.Add(v4);
            Port = ((IPEndPoint)v4.LocalEndpoint).Port;
            if (Socket.OSSupportsIPv6)
            {
                try
                {
                    var v6 = new TcpListener(IPAddress.IPv6Loopback, Port);
                    MakeExclusive(v6);
                    v6.Start();
                    listeners.Add(v6);
                }
                catch (SocketException e)
                {
                    Log.Write("IPv6 (::1) not available: " + e.Message);
                }
            }
            foreach (TcpListener l in listeners)
            {
                TcpListener own = l;
                Task.Run(() => Accept(own));
            }
        }

        /// <summary>On Windows no other process may share the same port (SO_EXCLUSIVEADDRUSE).</summary>
        private static void MakeExclusive(TcpListener l)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try { l.ExclusiveAddressUse = true; }
            catch (SocketException) { }
            catch (PlatformNotSupportedException) { }
        }

        private async Task Accept(TcpListener l)
        {
            while (!stopped)
            {
                TcpClient client;
                try { client = await l.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException e)
                {
                    if (stopped) return;
                    Log.Write("Accept: " + e.Message);
                    continue;
                }
                catch (InvalidOperationException) { return; }
                _ = Task.Run(() => Serve(client));
            }
        }

        private void Serve(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = Timeout;
                    client.SendTimeout = Timeout;
                    using (var ssl = new SslStream(client.GetStream(), false))
                    {
                        ssl.ReadTimeout = Timeout;
                        ssl.WriteTimeout = Timeout;
                        // SslProtocols.None: the operating system chooses (TLS 1.2 or 1.3).
                        ssl.AuthenticateAsServer(certificate, false, SslProtocols.None, false);
                        while (!stopped)
                        {
                            string? head = ReadHead(ssl, out bool tooLarge);
                            if (head == null)
                            {
                                if (tooLarge) Send(ssl, null, true);
                                return;
                            }
                            HttpRequest? request = HttpRequest.Parse(head, out string? error);
                            if (request == null) Log.Write("Invalid request: " + error);
                            HttpResponse response = HttpResponse.Decide(request, root, Port);
                            Interlocked.Increment(ref requests);
                            if (response.Status != 200 && request != null)
                                Log.Write(request.Method + " " + request.Target + " -> " + response);
                            Send(ssl, response, false);
                            if (response.Close) return;
                        }
                    }
                }
            }
            catch (IOException) { }
            catch (AuthenticationException e) { Log.Write("TLS: " + e.Message); }
            catch (ObjectDisposedException) { }
            catch (Exception e) { Log.Write("Connection: " + e); }
        }

        /// <summary>Reads up to the empty line; null at the end of the connection or if the head is too large.</summary>
        private static string? ReadHead(Stream s, out bool tooLarge)
        {
            tooLarge = false;
            var buffer = new MemoryStream();
            var one = new byte[1];
            while (true)
            {
                int n = s.Read(one, 0, 1);
                if (n == 0) return null;
                byte b = one[0];
                // Skip empty lines before the request line (RFC 9112, 2.2).
                if (buffer.Length == 0 && (b == '\r' || b == '\n')) continue;
                buffer.WriteByte(b);
                if (b == '\n' && EndsWithEmptyLine(buffer)) break;
                if (buffer.Length > HttpRequest.MaxHeadSize)
                {
                    tooLarge = true;
                    return null;
                }
            }
            return Encoding.ASCII.GetString(buffer.ToArray()).TrimEnd('\r', '\n');
        }

        /// <summary>Buffer ends with an empty line ("\r\n\r\n" or "\n\n").</summary>
        private static bool EndsWithEmptyLine(MemoryStream m)
        {
            byte[] b = m.GetBuffer();
            long n = m.Length;
            if (n >= 2 && b[n - 2] == '\n') return true;
            return n >= 4 && b[n - 2] == '\r' && b[n - 3] == '\n' && b[n - 4] == '\r';
        }

        private static void Send(Stream s, HttpResponse? response, bool tooLarge)
        {
            if (tooLarge)
            {
                byte[] h = Encoding.ASCII.GetBytes("HTTP/1.1 431 Request Header Fields Too Large\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                s.Write(h, 0, h.Length);
                s.Flush();
                return;
            }
            if (response == null) return;
            if (response.File == null)
            {
                byte[] body = response.BodyBytes();
                byte[] head = Encoding.ASCII.GetBytes(response.Head(body.Length));
                s.Write(head, 0, head.Length);
                if (response.WithBody) s.Write(body, 0, body.Length);
                s.Flush();
                return;
            }
            using (var file = new FileStream(response.File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
            {
                byte[] head = Encoding.ASCII.GetBytes(response.Head(file.Length));
                s.Write(head, 0, head.Length);
                if (response.WithBody) file.CopyTo(s, 65536);
                s.Flush();
            }
        }

        public void Dispose()
        {
            stopped = true;
            foreach (TcpListener l in listeners)
            {
                try { l.Stop(); }
                catch (SocketException) { }
            }
            listeners.Clear();
        }
    }
}
