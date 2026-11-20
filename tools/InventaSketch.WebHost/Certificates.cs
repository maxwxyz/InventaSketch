using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace InventaSketch.WebHost
{
    /// <summary>What happens with the existing certificate.</summary>
    public enum CertificateDecision
    {
        /// <summary>No usable certificate: create a new one.</summary>
        Create,
        /// <summary>Expires soon: create a new one, remove the old one.</summary>
        Renew,
        /// <summary>Valid: keep using it.</summary>
        Reuse,
    }

    /// <summary>
    /// Self-signed certificate for "localhost" (like the .NET developer certificates): RSA 2048, SHA-256, not a CA,
    /// usage server authentication, alternative names localhost, 127.0.0.1 and ::1, valid for one year.
    /// Recognised by the organisation "InventaSketch Web-Add-in" in the subject.
    /// </summary>
    public static class Certificates
    {
        /// <summary>Organisation in the subject; unchanged so that certificates of earlier installs are still found.</summary>
        public const string Organization = "InventaSketch Web-Add-in";
        public const string Subject = "CN=localhost, O=" + Organization;
        public const string FriendlyName = "InventaSketch Web-Add-in (localhost)";
        public static readonly TimeSpan Validity = TimeSpan.FromDays(365);

        /// <summary>Renew this long before expiry.</summary>
        public static readonly TimeSpan RenewBefore = TimeSpan.FromDays(30);

        /// <summary>Decision from expiry date and private key of the best existing certificate.</summary>
        public static CertificateDecision Decide(DateTime? expiry, bool hasPrivateKey, DateTime now)
        {
            if (expiry == null || !hasPrivateKey) return CertificateDecision.Create;
            return expiry.Value - now < RenewBefore ? CertificateDecision.Renew : CertificateDecision.Reuse;
        }

        /// <summary>Certificate of this program (subject with CN=localhost and our organisation).</summary>
        public static bool IsOurs(X509Certificate2 c) =>
            c.Subject.IndexOf("O=" + Organization, StringComparison.Ordinal) >= 0
            && string.Equals(c.GetNameInfo(X509NameType.SimpleName, false), "localhost", StringComparison.OrdinalIgnoreCase);

        /// <summary>Our certificate with a private key and the latest expiry.</summary>
        public static X509Certificate2? SelectBest(IEnumerable<X509Certificate2> candidates) =>
            candidates.Where(c => IsOurs(c) && c.HasPrivateKey).OrderByDescending(c => c.NotAfter).FirstOrDefault();

        /// <summary>
        /// Creates a new certificate. <paramref name="persistent"/>: private key in the user profile (for the store
        /// CurrentUser\My); otherwise only for this process (tests).
        /// </summary>
        public static X509Certificate2 Create(DateTime now, bool persistent)
        {
            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(Subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                var names = new SubjectAlternativeNameBuilder();
                names.AddDnsName("localhost");
                names.AddIpAddress(IPAddress.Loopback);
                names.AddIpAddress(IPAddress.IPv6Loopback);
                request.CertificateExtensions.Add(names.Build());
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
                using (X509Certificate2 raw = request.CreateSelfSigned(now.AddMinutes(-5), now.Add(Validity)))
                {
                    // Reload via PFX: SChannel (SslStream on Windows) needs a stored key.
                    string password = Guid.NewGuid().ToString("N");
                    byte[] pfx = raw.Export(X509ContentType.Pfx, password);
                    X509KeyStorageFlags flags = persistent
                        ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet
                        : X509KeyStorageFlags.UserKeySet;
#if NET
                    var c = X509CertificateLoader.LoadPkcs12(pfx, password, flags);
#else
                    var c = new X509Certificate2(pfx, password, flags);
#endif
                    if (persistent && Environment.OSVersion.Platform == PlatformID.Win32NT) c.FriendlyName = FriendlyName;
                    return c;
                }
            }
        }

        /// <summary>Public part only (for the store of trusted root certification authorities).</summary>
        public static X509Certificate2 PublicPart(X509Certificate2 c)
        {
#if NET
            return X509CertificateLoader.LoadCertificate(c.RawData);
#else
            return new X509Certificate2(c.RawData);
#endif
        }
    }
}
