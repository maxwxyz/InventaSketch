using System;
using System.Collections.Generic;
using InventaSketch.AddIn;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Bildschirme mit unterschiedlicher Skalierung (docs/PLAN.md 0b, B1). PowerPoint und sein Folienbereich laufen je Monitor
    /// (echte Pixel); Office ruft Add-ins aber mit Systemskalierung auf (geprüft 2026-10-29: eigene Fenster entstanden
    /// "system", PowerPoint "permonitor"). Auf einem Monitor mit anderer Skalierung streckt Windows solche Fenster als Bitmap
    /// und liefert virtualisierte Koordinaten, Overlay und Treffer lagen versetzt. Slidemax rechnet deshalb in einem Bereich
    /// mit Skalierung je Monitor: eigene Fenster entstehen darin, Hooks, Auswahlwechsel, Befehle und Zeitgeber laufen darin.
    /// </summary>
    internal readonly struct DpiScope : IDisposable
    {
        private const int Unaware = 0, SystemAware = 1, PerMonitorAware = 2;
        private static readonly HashSet<string> Noted = new HashSet<string>(StringComparer.Ordinal);

        private readonly IntPtr _previous;

        private DpiScope(IntPtr previous)
        {
            _previous = previous;
        }

        /// <summary>Bis zum Ende des Bereichs echte Pixel (Skalierung je Monitor); ohne Unterstützung (vor Windows 10 1703) wirkungslos.</summary>
        public static DpiScope PerMonitor()
        {
            try
            {
                return new DpiScope(NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2));
            }
            catch (EntryPointNotFoundException)
            {
                return default;
            }
        }

        public void Dispose()
        {
            if (_previous == IntPtr.Zero) return;
            try
            {
                NativeMethods.SetThreadDpiAwarenessContext(_previous);
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        /// <summary>Skalierung, mit der der Thread gerade rechnet (0 unaware, 1 system, 2 je Monitor); -1 unbekannt.</summary>
        public static int ThreadAwareness
        {
            get
            {
                try
                {
                    return NativeMethods.GetAwarenessFromDpiAwarenessContext(NativeMethods.GetThreadDpiAwarenessContext());
                }
                catch (EntryPointNotFoundException)
                {
                    return -1;
                }
            }
        }

        /// <summary>Skalierung, mit der ein Fenster läuft; -1 unbekannt.</summary>
        public static int WindowAwareness(IntPtr hwnd)
        {
            try
            {
                return NativeMethods.GetAwarenessFromDpiAwarenessContext(NativeMethods.GetWindowDpiAwarenessContext(hwnd));
            }
            catch (EntryPointNotFoundException)
            {
                return -1;
            }
        }

        /// <summary>
        /// Punkt aus dem Koordinatensystem, mit dem der Thread gerade rechnet, in echte Pixel (am Fenster <paramref name="hwnd"/>);
        /// unverändert, wenn der Thread schon je Monitor rechnet oder Windows die Umrechnung nicht kennt.
        /// </summary>
        public static NativeMethods.POINT ToPhysical(IntPtr hwnd, NativeMethods.POINT pt)
        {
            int awareness = ThreadAwareness;
            if (awareness == PerMonitorAware || awareness < 0 || hwnd == IntPtr.Zero) return pt;
            try
            {
                var p = pt;
                return NativeMethods.LogicalToPhysicalPointForPerMonitorDPI(hwnd, ref p) ? p : pt;
            }
            catch (EntryPointNotFoundException)
            {
                return pt;
            }
        }

        /// <summary>Skalierung einmal je Ort und Wert ins Protokoll (Diagnose bei mehreren Bildschirmen).</summary>
        public static void Note(string where, int awareness)
        {
            string key = where + ":" + awareness;
            lock (Noted)
                if (!Noted.Add(key)) return;
            Log.Info("Skalierung " + where + ": " + Name(awareness));
        }

        public static string Name(int awareness) => awareness switch
        {
            Unaware => "ohne",
            SystemAware => "System",
            PerMonitorAware => "je Monitor",
            _ => "unbekannt",
        };
    }
}
