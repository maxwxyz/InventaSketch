using System;
using System.Numerics;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>How the glass faces of an element treat the light (parameter <c>coating</c>).</summary>
    internal enum Coating
    {
        /// <summary>Idealised faces: no reflection losses and no change of the polarisation (the drawing as before).</summary>
        Ideal,
        /// <summary>Bare glass: Fresnel reflection losses for s and p, Brewster angle, phase shift on total internal reflection.</summary>
        Uncoated,
        /// <summary>Single-layer quarter-wave anti-reflection coating (index √n, designed for 550 nm), traced as a thin film.</summary>
        AntiReflection,
    }

    /// <summary>How a beam splitter divides the light (parameter <c>split</c>).</summary>
    internal enum SplitMode
    {
        /// <summary>Schematic: both rays keep the full intensity (the drawing as before).</summary>
        Schematic,
        /// <summary>Non-polarising: the share <see cref="Surface.Reflectance"/> is reflected, the rest transmitted; polarisation kept.</summary>
        NonPolarizing,
        /// <summary>Polarising (PBS): p is transmitted, s is reflected.</summary>
        Polarizing,
    }

    /// <summary>
    /// Fresnel equations at a plane interface between two loss-free media (indices n1 on the incident side, n2 behind),
    /// for s (perpendicular to the plane of incidence = to the drawing) and p (in the drawing plane). The amplitude
    /// coefficients use the convention in which an ideal metal mirror keeps the polarisation (r_p = (n1·cos θt − n2·cos θi) /
    /// (n1·cos θt + n2·cos θi), so r_p = r_s at normal incidence); reflectances and transmittances do not depend on it.
    /// </summary>
    public static class Fresnel
    {
        /// <summary>Brewster angle (degrees from the normal) of the interface n1 → n2: tan θB = n2 / n1 (r_p = 0).</summary>
        public static double BrewsterAngle(double n1, double n2) => Geo.RadToDeg(Math.Atan2(n2, n1));

        /// <summary>Critical angle of total internal reflection (degrees) for n1 &gt; n2: sin θc = n2 / n1; NaN otherwise.</summary>
        public static double CriticalAngle(double n1, double n2) => n2 < n1 ? Geo.RadToDeg(Math.Asin(n2 / n1)) : double.NaN;

        /// <summary>Power reflectances R_s, R_p at <paramref name="incidenceDeg"/> (1 and 1 beyond the critical angle).</summary>
        public static (double Rs, double Rp) Reflectance(double n1, double n2, double incidenceDeg)
        {
            var (rs, rp) = Amplitudes(n1, n2, Math.Cos(Geo.DegToRad(incidenceDeg)));
            return (Sq(rs.Magnitude), Sq(rp.Magnitude));
        }

        /// <summary>Power transmittances T_s = 1 − R_s, T_p = 1 − R_p (0 beyond the critical angle).</summary>
        public static (double Ts, double Tp) Transmittance(double n1, double n2, double incidenceDeg)
        {
            var (rs, rp) = Reflectance(n1, n2, incidenceDeg);
            return (1 - rs, 1 - rp);
        }

        /// <summary>
        /// Phase of p relative to s (degrees, −180 … 180) after one reflection at <paramref name="incidenceDeg"/>; beyond the
        /// critical angle the phase of total internal reflection. Reflection keeps the frame of the ray in which an ideal
        /// mirror changes nothing.
        /// </summary>
        public static double ReflectionPhase(double n1, double n2, double incidenceDeg)
        {
            var (rs, rp) = Amplitudes(n1, n2, Math.Cos(Geo.DegToRad(incidenceDeg)));
            return Wrap(Geo.RadToDeg(rp.Phase - rs.Phase));
        }

        /// <summary>
        /// Amplitude reflection coefficients r_s, r_p for the cosine of the angle of incidence <paramref name="cosI"/>; beyond
        /// the critical angle cos θt = i·√(sin²θt − 1) (evanescent wave decaying behind the face), so |r| = 1 with a phase.
        /// </summary>
        internal static (Complex Rs, Complex Rp) Amplitudes(double n1, double n2, double cosI)
        {
            cosI = Math.Min(1, Math.Abs(cosI));
            var cosT = CosT(n1, n2, cosI);
            Complex ci = cosI;
            var rs = (n1 * ci - n2 * cosT) / (n1 * ci + n2 * cosT);
            var rp = (n1 * cosT - n2 * ci) / (n1 * cosT + n2 * ci);
            return (rs, rp);
        }

        /// <summary>Cosine of the refraction angle (imaginary beyond the critical angle).</summary>
        private static Complex CosT(double n1, double n2, double cosI)
        {
            var sinT = n1 / n2 * Math.Sqrt(Math.Max(0, 1 - cosI * cosI));
            var k = 1 - sinT * sinT;
            return k >= 0 ? new Complex(Math.Sqrt(k), 0) : new Complex(0, Math.Sqrt(-k));
        }

        /// <summary>
        /// Power transmittances (T_s, T_p) of a face carrying a single quarter-wave layer of index <paramref name="nc"/>
        /// (optical thickness λ0/4 at <paramref name="design"/> nm), traced at <paramref name="lambda"/> nm: Airy sum
        /// r = (r01 + r12·e^{2iβ}) / (1 + r01·r12·e^{2iβ}) with β = (π/2)·(λ0/λ)·cos θc, T = 1 − |r|² (no absorption).
        /// </summary>
        internal static (double Ts, double Tp) CoatedTransmittance(double n1, double n2, double cosI, double nc, double lambda, double design = 550)
        {
            cosI = Math.Min(1, Math.Abs(cosI));
            var sin2 = 1 - cosI * cosI;
            var cosC = Math.Sqrt(Math.Max(0, 1 - n1 * n1 * sin2 / (nc * nc)));
            var cos2 = Math.Sqrt(Math.Max(0, 1 - n1 * n1 * sin2 / (n2 * n2)));
            var beta = Math.PI / 2 * design / (lambda > 0 ? lambda : design) * cosC;
            var e = Complex.FromPolarCoordinates(1, 2 * beta);
            double Rs(double ni, double nt, double a, double b) => (ni * a - nt * b) / (ni * a + nt * b);
            double Rp(double ni, double nt, double a, double b) => (ni * b - nt * a) / (ni * b + nt * a);
            Complex Film(double r01, double r12) => (r01 + r12 * e) / (1 + r01 * r12 * e);
            var s = Film(Rs(n1, nc, cosI, cosC), Rs(nc, n2, cosC, cos2));
            var p = Film(Rp(n1, nc, cosI, cosC), Rp(nc, n2, cosC, cos2));
            return (1 - Sq(s.Magnitude), 1 - Sq(p.Magnitude));
        }

        /// <summary>
        /// Complex amplitude factors (p, s) a face applies to the light passing it (<paramref name="total"/> = false) or
        /// totally reflected at it: power-normalised √T for transmission, r for total internal reflection; (1, 1) for ideal faces.
        /// </summary>
        internal static (Complex P, Complex S) Factors(Coating coating, double n1, double n2, double cosI, bool total, double glass, double lambda)
        {
            if (coating == Coating.Ideal) return (1, 1);
            if (total)
            {
                var (rs, rp) = Amplitudes(n1, n2, cosI);
                return (rp, rs);
            }
            double ts, tp;
            if (coating == Coating.AntiReflection) (ts, tp) = CoatedTransmittance(n1, n2, cosI, Math.Sqrt(Math.Max(1, glass)), lambda);
            else
            {
                var (rs, rp) = Amplitudes(n1, n2, cosI);
                (ts, tp) = (1 - Sq(rs.Magnitude), 1 - Sq(rp.Magnitude));
            }
            return (Math.Sqrt(Math.Max(0, tp)), Math.Sqrt(Math.Max(0, ts)));
        }

        /// <summary>The coating parameter value of an element.</summary>
        internal static Coating Of(SketchObject o, string fallback = "ideal") => o.Str("coating", fallback) switch
        {
            "none" => Coating.Uncoated,
            "ar" => Coating.AntiReflection,
            _ => Coating.Ideal,
        };

        private static double Sq(double v) => v * v;

        private static double Wrap(double deg)
        {
            deg %= 360;
            if (deg > 180) deg -= 360;
            else if (deg <= -180) deg += 360;
            return deg;
        }
    }

    /// <summary>
    /// Polarisation state of partially polarised light as its coherency matrix J = ⟨E·E†⟩ in the (p, s) frame of the ray,
    /// normalised to trace 1: A = ⟨|Ep|²⟩, B = ⟨|Es|²⟩, C = ⟨Ep·Es*⟩ = Cr + i·Ci. Fully polarised light is a pure state
    /// (a Jones vector), unpolarised light A = B = ½, C = 0. A Jones matrix M acts as J′ = M·J·M†; its trace is the share of
    /// the intensity that passes.
    /// </summary>
    internal readonly struct Coherency
    {
        public Coherency(double a, double b, double cr, double ci)
        {
            var t = a + b;
            if (t < 1e-18) { A = 0.5; B = 0.5; Cr = 0; Ci = 0; return; }
            A = a / t; B = b / t; Cr = cr / t; Ci = ci / t;
        }

        public double A { get; }
        public double B { get; }
        public double Cr { get; }
        public double Ci { get; }

        public static Coherency Unpolarized => new(0.5, 0.5, 0, 0);

        /// <summary>Linear polarisation at <paramref name="deg"/> degrees from p towards s.</summary>
        public static Coherency Linear(double deg) => From(Jones.Linear(deg));

        /// <summary>The pure state of a Jones vector.</summary>
        public static Coherency From(Jones j) => new(j.Pr * j.Pr + j.Pi * j.Pi, j.Sr * j.Sr + j.Si * j.Si,
            j.Pr * j.Sr + j.Pi * j.Si, j.Pi * j.Sr - j.Pr * j.Si);

        /// <summary>Share a linear polariser with its axis at <paramref name="deg"/> passes (Malus's law for the polarised part).</summary>
        public double Transmission(double deg)
        {
            var a = Geo.DegToRad(deg);
            double c = Math.Cos(a), s = Math.Sin(a);
            return Math.Max(0, c * c * A + s * s * B + 2 * c * s * Cr);
        }

        /// <summary>Degree of polarisation √((A − B)² + 4|C|²) (0 unpolarised, 1 fully polarised).</summary>
        public double Degree => Math.Min(1, Math.Sqrt((A - B) * (A - B) + 4 * (Cr * Cr + Ci * Ci)));

        /// <summary>Normalised Stokes parameter S3/S0 (±1 circular, 0 linear or unpolarised).</summary>
        public double Circularity => -2 * Ci;

        /// <summary>Orientation of the polarised part (degrees, −90 … 90, from p towards s).</summary>
        public double Orientation => Geo.RadToDeg(0.5 * Math.Atan2(2 * Cr, A - B));

        /// <summary>The state behind the Jones matrix [[m11, m12], [m21, m22]] and the share of the intensity that passes.</summary>
        public (Coherency State, double Share) Apply(Complex m11, Complex m12, Complex m21, Complex m22)
        {
            Complex c = new(Cr, Ci);
            // N = M·J
            var n11 = m11 * A + m12 * Complex.Conjugate(c);
            var n12 = m11 * c + m12 * B;
            var n21 = m21 * A + m22 * Complex.Conjugate(c);
            var n22 = m21 * c + m22 * B;
            // J′ = N·M†
            var a = (n11 * Complex.Conjugate(m11) + n12 * Complex.Conjugate(m12)).Real;
            var b = (n21 * Complex.Conjugate(m21) + n22 * Complex.Conjugate(m22)).Real;
            var cc = n11 * Complex.Conjugate(m21) + n12 * Complex.Conjugate(m22);
            var share = a + b;
            return (new Coherency(a, b, cc.Real, cc.Imaginary), Math.Max(0, share));
        }

        /// <summary>The state behind a diagonal Jones matrix (factors on p and s, e.g. Fresnel coefficients).</summary>
        public (Coherency State, double Share) Apply(Complex p, Complex s) => Apply(p, Complex.Zero, Complex.Zero, s);

        /// <summary>The state behind a wave plate with its fast axis at <paramref name="fastDeg"/> and retardance <paramref name="gamma"/> (radians).</summary>
        public Coherency Retard(double fastDeg, double gamma)
        {
            var a = Geo.DegToRad(fastDeg);
            double c = Math.Cos(a), s = Math.Sin(a);
            var e = Complex.FromPolarCoordinates(1, gamma);
            var off = c * s * (1 - e);
            return Apply(c * c + s * s * e, off, off, s * s + c * c * e).State;
        }

        /// <summary>Null for (practically) unpolarised light, else this state.</summary>
        public Coherency? OrUnpolarized() => Degree < 1e-9 ? null : this;
    }
}
