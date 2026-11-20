using System;
using System.Globalization;
using System.Threading;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Named session objects per port: a lock (one instance), "ready" (server is listening) and "stop" (signal from
    /// outside).
    /// </summary>
    public static class SingleInstance
    {
        // Object name suffixes ("lock", "ready", "stop") kept from the first, German-language version of this host so that
        // a newer build can still detect and stop a server started by an older one.
        private const string LockSuffix = "Sperre";
        private const string ReadySuffix = "Bereit";
        private const string StopSuffix = "Beenden";

        public static string Name(int port, string suffix) =>
            @"Local\InventaSketch.WebHost." + port.ToString(CultureInfo.InvariantCulture) + "." + suffix;

        /// <summary>Lock for this port, or null if a server is already running.</summary>
        public static Mutex? Acquire(int port)
        {
            var m = new Mutex(false, Name(port, LockSuffix));
            try
            {
                if (m.WaitOne(0)) return m;
            }
            catch (AbandonedMutexException)
            {
                return m;
            }
            m.Dispose();
            return null;
        }

        public static EventWaitHandle CreateReady(int port) => new EventWaitHandle(false, EventResetMode.ManualReset, Name(port, ReadySuffix));

        public static EventWaitHandle CreateStop(int port) => new EventWaitHandle(false, EventResetMode.AutoReset, Name(port, StopSuffix));

        public static bool IsRunning(int port)
        {
            if (!EventWaitHandle.TryOpenExisting(Name(port, ReadySuffix), out EventWaitHandle? e)) return false;
            using (e) return e.WaitOne(0);
        }

        /// <summary>Waits until a server on the port is ready.</summary>
        public static bool WaitUntilReady(int port, TimeSpan timeout)
        {
            DateTime end = DateTime.UtcNow + timeout;
            do
            {
                if (IsRunning(port)) return true;
                Thread.Sleep(200);
            }
            while (DateTime.UtcNow < end);
            return IsRunning(port);
        }

        /// <summary>Signals the server to stop and waits until it is gone. False if none was running or it does not stop.</summary>
        public static bool Stop(int port, TimeSpan timeout)
        {
            if (!EventWaitHandle.TryOpenExisting(Name(port, StopSuffix), out EventWaitHandle? e)) return false;
            using (e) e.Set();
            DateTime end = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < end)
            {
                if (!EventWaitHandle.TryOpenExisting(Name(port, ReadySuffix), out EventWaitHandle? ready)) return true;
                ready.Dispose();
                Thread.Sleep(200);
            }
            return false;
        }
    }
}
