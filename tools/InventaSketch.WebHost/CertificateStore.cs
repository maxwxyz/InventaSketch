using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using static InventaSketch.WebHost.Text;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// The user's certificate stores on Windows: CurrentUser\My (certificate with key) and CurrentUser\Root (trust).
    /// Adding to Root and removing from it makes Windows show its own confirmation prompt; it is never suppressed.
    /// No changes to LocalMachine.
    /// </summary>
    public static class CertificateStore
    {
        /// <summary>Our certificates in the user's store.</summary>
        public static List<X509Certificate2> Ours(StoreName name)
        {
            var result = new List<X509Certificate2>();
            using (var store = new X509Store(name, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                foreach (X509Certificate2 c in store.Certificates)
                {
                    if (Certificates.IsOurs(c)) result.Add(c);
                }
            }
            return result;
        }

        /// <summary>Best own certificate with key in CurrentUser\My.</summary>
        public static X509Certificate2? Best() => Certificates.SelectBest(Ours(StoreName.My));

        public static void Save(X509Certificate2 c)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(c);
            }
        }

        public static bool IsTrusted(X509Certificate2 c)
        {
            using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Find(X509FindType.FindByThumbprint, c.Thumbprint, false).Count > 0;
            }
        }

        /// <summary>
        /// Adds the public part to CurrentUser\Root (Windows asks for confirmation). Null on success, otherwise the reason.
        /// </summary>
        public static string? Trust(X509Certificate2 c)
        {
            try
            {
                using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                using (X509Certificate2 publicPart = Certificates.PublicPart(c))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(publicPart);
                }
            }
            catch (CryptographicException e)
            {
                return T("Adding was declined or is not allowed (", "Eintragen abgelehnt oder nicht erlaubt (") + e.Message.Trim() + ")";
            }
            return IsTrusted(c) ? null
                : T("The certificate was not added (prompt declined or prevented by a policy)",
                    "Zertifikat wurde nicht eingetragen (Rückfrage abgelehnt oder durch eine Richtlinie verhindert)");
        }

        /// <summary>Removes the certificate from My (with key) and from Root (Windows asks). Returns errors.</summary>
        public static List<string> Remove(X509Certificate2 c)
        {
            var errors = new List<string>();
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
                errors.Add(T("Trust not removed (", "Vertrauen nicht entfernt (") + e.Message.Trim() + ")");
            }
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    foreach (X509Certificate2 m in store.Certificates.Find(X509FindType.FindByThumbprint, c.Thumbprint, false))
                    {
                        DeleteKey(m);
                        store.Remove(m);
                    }
                }
            }
            catch (CryptographicException e)
            {
                errors.Add(T("Certificate not removed (", "Zertifikat nicht entfernt (") + e.Message.Trim() + ")");
            }
            return errors;
        }

        private static void DeleteKey(X509Certificate2 c)
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
        /// Policy "Allow users to select new root certification authorities (CAs) to trust" switched off
        /// (ProtectedRoots\Flags with CERT_PROT_ROOT_DISABLE_CURRENT_USER_FLAG): then no own certificate can be trusted.
        /// </summary>
        public static bool RootTrustBlocked()
        {
            using (RegistryKey? k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\SystemCertificates\Root\ProtectedRoots"))
            {
                return k?.GetValue("Flags") is int flags && (flags & 1) != 0;
            }
        }
    }
}
