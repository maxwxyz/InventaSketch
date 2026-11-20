using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Zertifikatspeicher des Benutzers unter Windows: CurrentUser\My (Zertifikat mit Schlüssel) und CurrentUser\Root
    /// (Vertrauen). Beim Eintragen in Root und beim Entfernen daraus zeigt Windows selbst eine Rückfrage; sie wird nicht
    /// unterdrückt. Keine Änderungen an LocalMachine.
    /// </summary>
    public static class Zertifikatspeicher
    {
        /// <summary>Eigene Zertifikate im Speicher des Benutzers.</summary>
        public static List<X509Certificate2> Eigene(StoreName name)
        {
            var ergebnis = new List<X509Certificate2>();
            using (var store = new X509Store(name, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                foreach (X509Certificate2 c in store.Certificates)
                {
                    if (Zertifikate.IstUnseres(c)) ergebnis.Add(c);
                }
            }
            return ergebnis;
        }

        /// <summary>Bestes eigenes Zertifikat mit Schlüssel in CurrentUser\My.</summary>
        public static X509Certificate2? Bestes() => Zertifikate.Auswaehlen(Eigene(StoreName.My));

        public static void Speichern(X509Certificate2 c)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(c);
            }
        }

        public static bool IstVertraut(X509Certificate2 c)
        {
            using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Find(X509FindType.FindByThumbprint, c.Thumbprint, false).Count > 0;
            }
        }

        /// <summary>
        /// Trägt den öffentlichen Teil in CurrentUser\Root ein (Windows fragt nach). Null bei Erfolg, sonst der Grund.
        /// </summary>
        public static string? Vertrauen(X509Certificate2 c)
        {
            try
            {
                using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                using (X509Certificate2 oeffentlich = Zertifikate.OeffentlicherTeil(c))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(oeffentlich);
                }
            }
            catch (CryptographicException e)
            {
                return "Eintragen abgelehnt oder nicht erlaubt (" + e.Message.Trim() + ")";
            }
            return IstVertraut(c) ? null : "Zertifikat wurde nicht eingetragen (Rückfrage abgelehnt oder durch eine Richtlinie verhindert)";
        }

        /// <summary>Entfernt das Zertifikat aus My (mit Schlüssel) und aus Root (Windows fragt nach). Gibt Fehler zurück.</summary>
        public static List<string> Entfernen(X509Certificate2 c)
        {
            var fehler = new List<string>();
            try
            {
                using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    foreach (X509Certificate2 r in store.Certificates.Find(X509FindType.FindByThumbprint, c.Thumbprint, false))
                        store.Remove(r);
                }
            }
            catch (CryptographicException e)
            {
                fehler.Add("Vertrauen nicht entfernt (" + e.Message.Trim() + ")");
            }
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    foreach (X509Certificate2 m in store.Certificates.Find(X509FindType.FindByThumbprint, c.Thumbprint, false))
                    {
                        SchluesselLoeschen(m);
                        store.Remove(m);
                    }
                }
            }
            catch (CryptographicException e)
            {
                fehler.Add("Zertifikat nicht entfernt (" + e.Message.Trim() + ")");
            }
            return fehler;
        }

        private static void SchluesselLoeschen(X509Certificate2 c)
        {
            if (!c.HasPrivateKey) return;
            try
            {
                using (RSA? rsa = c.GetRSAPrivateKey())
                {
                    if (rsa is RSACng cng) cng.Key.Delete();
                    else if (rsa is RSACryptoServiceProvider csp)
                    {
                        csp.PersistKeyInCsp = false;
                        csp.Clear();
                    }
                }
            }
            catch (CryptographicException) { }
        }

        /// <summary>
        /// Richtlinie "Benutzern erlauben, neuen Stammzertifizierungsstellen zu vertrauen" ausgeschaltet
        /// (ProtectedRoots\Flags mit CERT_PROT_ROOT_DISABLE_CURRENT_USER_FLAG): dann lässt sich kein eigenes Zertifikat
        /// vertrauen.
        /// </summary>
        public static bool StammGesperrt()
        {
            using (RegistryKey? k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\SystemCertificates\Root\ProtectedRoots"))
            {
                return k?.GetValue("Flags") is int flags && (flags & 1) != 0;
            }
        }
    }
}
