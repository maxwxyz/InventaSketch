using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace InventaSketch.WebHost
{
    /// <summary>Was mit dem vorhandenen Zertifikat geschieht.</summary>
    public enum ZertifikatEntscheidung
    {
        /// <summary>Kein brauchbares Zertifikat: neu anlegen.</summary>
        Neu,
        /// <summary>Läuft bald ab: neues anlegen, altes entfernen.</summary>
        Erneuern,
        /// <summary>Gültig: weiterverwenden.</summary>
        Wiederverwenden,
    }

    /// <summary>
    /// Selbst ausgestelltes Zertifikat für "localhost" (wie die Entwicklerzertifikate von .NET): RSA 2048, SHA-256,
    /// kein CA-Zertifikat, Verwendung Serverauthentifizierung, alternative Namen localhost, 127.0.0.1 und ::1, ein Jahr gültig.
    /// Erkennbar an der Organisation "InventaSketch Web-Add-in" im Antragsteller.
    /// </summary>
    public static class Zertifikate
    {
        public const string Organisation = "InventaSketch Web-Add-in";
        public const string Antragsteller = "CN=localhost, O=" + Organisation;
        public const string AnzeigeName = "InventaSketch Web-Add-in (localhost)";
        public static readonly TimeSpan Gueltigkeit = TimeSpan.FromDays(365);

        /// <summary>So lange vor dem Ablauf wird erneuert.</summary>
        public static readonly TimeSpan Vorlauf = TimeSpan.FromDays(30);

        /// <summary>Entscheidung nach Ablaufdatum und privatem Schlüssel des besten vorhandenen Zertifikats.</summary>
        public static ZertifikatEntscheidung Entscheiden(DateTime? ablauf, bool hatSchluessel, DateTime jetzt)
        {
            if (ablauf == null || !hatSchluessel) return ZertifikatEntscheidung.Neu;
            return ablauf.Value - jetzt < Vorlauf ? ZertifikatEntscheidung.Erneuern : ZertifikatEntscheidung.Wiederverwenden;
        }

        /// <summary>Zertifikat dieses Programms (Antragsteller mit CN=localhost und der eigenen Organisation).</summary>
        public static bool IstUnseres(X509Certificate2 c) =>
            c.Subject.IndexOf("O=" + Organisation, StringComparison.Ordinal) >= 0
            && string.Equals(c.GetNameInfo(X509NameType.SimpleName, false), "localhost", StringComparison.OrdinalIgnoreCase);

        /// <summary>Das eigene Zertifikat mit privatem Schlüssel und dem spätesten Ablauf.</summary>
        public static X509Certificate2? Auswaehlen(IEnumerable<X509Certificate2> kandidaten) =>
            kandidaten.Where(c => IstUnseres(c) && c.HasPrivateKey).OrderByDescending(c => c.NotAfter).FirstOrDefault();

        /// <summary>
        /// Legt ein neues Zertifikat an. <paramref name="dauerhaft"/>: privater Schlüssel im Profil des Benutzers (für den
        /// Speicher CurrentUser\My); sonst nur für diesen Prozess (Tests).
        /// </summary>
        public static X509Certificate2 Erzeugen(DateTime jetzt, bool dauerhaft)
        {
            using (RSA rsa = RSA.Create(2048))
            {
                var antrag = new CertificateRequest(Antragsteller, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                antrag.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                antrag.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                antrag.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                var namen = new SubjectAlternativeNameBuilder();
                namen.AddDnsName("localhost");
                namen.AddIpAddress(IPAddress.Loopback);
                namen.AddIpAddress(IPAddress.IPv6Loopback);
                antrag.CertificateExtensions.Add(namen.Build());
                antrag.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(antrag.PublicKey, false));
                using (X509Certificate2 roh = antrag.CreateSelfSigned(jetzt.AddMinutes(-5), jetzt.Add(Gueltigkeit)))
                {
                    // Über PFX neu laden: SChannel (SslStream unter Windows) braucht einen gespeicherten Schlüssel.
                    string kennwort = Guid.NewGuid().ToString("N");
                    byte[] pfx = roh.Export(X509ContentType.Pfx, kennwort);
                    X509KeyStorageFlags flags = dauerhaft
                        ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet
                        : X509KeyStorageFlags.UserKeySet;
#if NET
                    var c = X509CertificateLoader.LoadPkcs12(pfx, kennwort, flags);
#else
                    var c = new X509Certificate2(pfx, kennwort, flags);
#endif
                    if (dauerhaft && Environment.OSVersion.Platform == PlatformID.Win32NT) c.FriendlyName = AnzeigeName;
                    return c;
                }
            }
        }

        /// <summary>Nur der öffentliche Teil (für den Speicher der vertrauenswürdigen Stammzertifizierungsstellen).</summary>
        public static X509Certificate2 OeffentlicherTeil(X509Certificate2 c)
        {
#if NET
            return X509CertificateLoader.LoadCertificate(c.RawData);
#else
            return new X509Certificate2(c.RawData);
#endif
        }
    }
}
