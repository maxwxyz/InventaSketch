using System;
using System.Globalization;
using System.Threading;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Benannte Objekte der Sitzung je Port: Sperre (eine Instanz), Bereit (Server lauscht), Beenden (Signal von außen).
    /// </summary>
    public static class Instanz
    {
        public static string Name(int port, string art) =>
            @"Local\InventaSketch.WebHost." + port.ToString(CultureInfo.InvariantCulture) + "." + art;

        /// <summary>Sperre für diesen Port oder null, wenn schon ein Server läuft.</summary>
        public static Mutex? Sperren(int port)
        {
            var m = new Mutex(false, Name(port, "Sperre"));
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

        public static EventWaitHandle BereitAnlegen(int port) => new EventWaitHandle(false, EventResetMode.ManualReset, Name(port, "Bereit"));

        public static EventWaitHandle BeendenAnlegen(int port) => new EventWaitHandle(false, EventResetMode.AutoReset, Name(port, "Beenden"));

        public static bool Laeuft(int port)
        {
            if (!EventWaitHandle.TryOpenExisting(Name(port, "Bereit"), out EventWaitHandle? e)) return false;
            using (e) return e.WaitOne(0);
        }

        /// <summary>Wartet, bis ein Server auf dem Port bereit ist.</summary>
        public static bool WartenBereit(int port, TimeSpan zeit)
        {
            DateTime ende = DateTime.UtcNow + zeit;
            do
            {
                if (Laeuft(port)) return true;
                Thread.Sleep(200);
            }
            while (DateTime.UtcNow < ende);
            return Laeuft(port);
        }

        /// <summary>Signalisiert dem Server das Ende und wartet, bis er weg ist. False, wenn keiner lief oder er nicht endet.</summary>
        public static bool Beenden(int port, TimeSpan zeit)
        {
            if (!EventWaitHandle.TryOpenExisting(Name(port, "Beenden"), out EventWaitHandle? e)) return false;
            using (e) e.Set();
            DateTime ende = DateTime.UtcNow + zeit;
            while (DateTime.UtcNow < ende)
            {
                if (!EventWaitHandle.TryOpenExisting(Name(port, "Bereit"), out EventWaitHandle? b)) return true;
                b.Dispose();
                Thread.Sleep(200);
            }
            return false;
        }
    }
}
