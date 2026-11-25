using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Calc 0 – quantities (docs/en/design/engineering-calculations.md, section 1.3). Internal values are coherent SI
    // (m, kg, s, A, K) plus rad for plane angles. A quantity kind has a dimension vector and a list of display units.
    // Kinds that share a dimension (torque/energy, power/heat flow, temperature/temperature difference) are kept apart
    // by the kind, not by the dimension.
    // =============================================================================================

    /// <summary>
    /// Dimension vector: exponents of length, mass, time, electric current, temperature and plane angle. The angle is
    /// tracked as its own exponent (SI treats the radian as dimensionless) so that "1500 rpm in rad/s" converts with 2π
    /// and a speed of rotation is never mistaken for a frequency.
    /// </summary>
    public readonly struct Dimension : IEquatable<Dimension>
    {
        public Dimension(int l = 0, int m = 0, int t = 0, int i = 0, int th = 0, int a = 0)
        {
            L = (sbyte)l; M = (sbyte)m; T = (sbyte)t; I = (sbyte)i; Th = (sbyte)th; A = (sbyte)a;
        }

        public int L { get; }
        public int M { get; }
        public int T { get; }
        public int I { get; }
        public int Th { get; }
        public int A { get; }

        public static readonly Dimension None = default;

        public bool IsNone => L == 0 && M == 0 && T == 0 && I == 0 && Th == 0 && A == 0;

        /// <summary>The same dimension with the plane angle dropped (the SI view: rad = 1).</summary>
        public Dimension WithoutAngle => new(L, M, T, I, Th, 0);

        public static Dimension operator *(Dimension a, Dimension b) => new(a.L + b.L, a.M + b.M, a.T + b.T, a.I + b.I, a.Th + b.Th, a.A + b.A);
        public static Dimension operator /(Dimension a, Dimension b) => new(a.L - b.L, a.M - b.M, a.T - b.T, a.I - b.I, a.Th - b.Th, a.A - b.A);
        public Dimension Pow(int n) => new(L * n, M * n, T * n, I * n, Th * n, A * n);

        /// <summary>Integer root (null when an exponent is not divisible, e.g. √m).</summary>
        public Dimension? Root(int n)
        {
            if (n == 0 || L % n != 0 || M % n != 0 || T % n != 0 || I % n != 0 || Th % n != 0 || A % n != 0) return null;
            return new Dimension(L / n, M / n, T / n, I / n, Th / n, A / n);
        }

        public bool Equals(Dimension o) => L == o.L && M == o.M && T == o.T && I == o.I && Th == o.Th && A == o.A;
        public override bool Equals(object? obj) => obj is Dimension d && Equals(d);
        public override int GetHashCode() => ((((L * 31 + M) * 31 + T) * 31 + I) * 31 + Th) * 31 + A;
        public static bool operator ==(Dimension a, Dimension b) => a.Equals(b);
        public static bool operator !=(Dimension a, Dimension b) => !a.Equals(b);

        /// <summary>The dimension written in coherent SI units, e.g. "kg·m²·s⁻³" (empty when dimensionless).</summary>
        public override string ToString()
        {
            var parts = new List<string>();
            void Add(string sym, int e)
            {
                if (e == 0) return;
                parts.Add(e == 1 ? sym : sym + Superscript(e));
            }
            Add("kg", M); Add("m", L); Add("s", T); Add("A", I); Add("K", Th); Add("rad", A);
            return string.Join("·", parts);
        }

        internal static string Superscript(int e)
        {
            var sb = new StringBuilder();
            foreach (var ch in e.ToString(System.Globalization.CultureInfo.InvariantCulture))
                sb.Append(ch switch
                {
                    '-' => '⁻', '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴',
                    '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', '9' => '⁹', _ => ch,
                });
            return sb.ToString();
        }
    }

    /// <summary>
    /// A quantity kind (voltage, torque, temperature difference …): a dimension and the units a field of this kind accepts.
    /// The first unit is the default display unit. Instances live in <see cref="Quantities"/>.
    /// </summary>
    public sealed class QuantityKind
    {
        private readonly List<Unit> _units = new();

        internal QuantityKind(string id, I18nText name, Dimension dimension, bool difference = false)
        {
            Id = id; Name = name; Dimension = dimension; IsDifference = difference;
        }

        /// <summary>Stable id, e.g. "voltage", "temperatureDifference".</summary>
        public string Id { get; }
        public I18nText Name { get; }
        public Dimension Dimension { get; }
        /// <summary>Temperature difference: no offset (°C is accepted on input in German only, shown as K).</summary>
        public bool IsDifference { get; }
        /// <summary>Allowed units in display order; the first one is the default display unit.</summary>
        public IReadOnlyList<Unit> Units => _units;
        public Unit DefaultUnit => _units[0];

        internal Unit Add(string symbol, double factor, string[]? spellings = null, double offset = 0, string? prefixes = null)
        {
            var u = new Unit(symbol, factor, offset, this, spellings ?? Array.Empty<string>(), prefixes ?? "");
            _units.Add(u);
            return u;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// A unit of a quantity kind: SI value = number · <see cref="Factor"/> + <see cref="Offset"/>. A unit with
    /// <see cref="Prefixes"/> takes SI prefixes on input ("kΩ", "µF") and, when shown, the prefix that keeps the number
    /// between 1 and 1000 (only from that list).
    /// </summary>
    public sealed class Unit
    {
        internal Unit(string symbol, double factor, double offset, QuantityKind kind, IReadOnlyList<string> spellings, string prefixes)
        {
            Symbol = symbol; Factor = factor; Offset = offset; Kind = kind; Prefixes = prefixes;
            var all = new List<string> { symbol };
            foreach (var s in spellings) if (!all.Contains(s)) all.Add(s);
            Spellings = all;
        }

        /// <summary>Display symbol, e.g. "kΩ", "min⁻¹", "°C".</summary>
        public string Symbol { get; }
        public double Factor { get; }
        public double Offset { get; }
        public QuantityKind Kind { get; }
        /// <summary>Accepted input spellings (the symbol first).</summary>
        public IReadOnlyList<string> Spellings { get; }
        /// <summary>SI prefixes this unit accepts and shows ("pnµmkMG"); empty = none.</summary>
        public string Prefixes { get; }
        public bool Prefixable => Prefixes.Length > 0;
        /// <summary>The unit this one was made from by a prefix ("kΩ" → "Ω"); null for catalogue units.</summary>
        public Unit? Base { get; private set; }
        public string? Prefix { get; private set; }

        public Dimension Dimension => Kind.Dimension;

        public double ToSi(double value) => value * Factor + Offset;
        public double FromSi(double si) => (si - Offset) / Factor;

        /// <summary>This unit with an SI prefix ("k" → kΩ); the prefixed unit takes no further prefixes.</summary>
        public Unit WithPrefix(string prefix)
        {
            if (prefix.Length == 0) return Base ?? this;
            var root = Base ?? this;
            var f = UnitCatalog.PrefixFactor(prefix);
            return new Unit(prefix + root.Symbol, root.Factor * f, root.Offset, root.Kind, Array.Empty<string>(), "") { Base = root, Prefix = prefix };
        }

        public override string ToString() => Symbol;
    }

    /// <summary>Catalogue of quantity kinds (Calc 0). Ids are stable and used by <see cref="VarDef"/> and the engine API.</summary>
    public static class Quantities
    {
        private static I18nText T(string en, string de) => new(en, de);

        // Dimensions.
        private static readonly Dimension Len = new(l: 1), Mass = new(m: 1), Time = new(t: 1), Cur = new(i: 1), Temp = new(th: 1), Ang = new(a: 1);
        private static readonly Dimension Force_ = Mass * Len / Time.Pow(2);
        private static readonly Dimension Energy_ = Force_ * Len;
        private static readonly Dimension Power_ = Energy_ / Time;
        private static readonly Dimension Volt_ = Power_ / Cur;

        public static readonly QuantityKind Ratio = new("ratio", T("Ratio", "Verhältnis"), Dimension.None);
        public static readonly QuantityKind Efficiency = new("efficiency", T("Efficiency", "Wirkungsgrad"), Dimension.None);
        public static readonly QuantityKind Count = new("count", T("Number", "Anzahl"), Dimension.None);
        public static readonly QuantityKind Voltage = new("voltage", T("Voltage", "Spannung"), Volt_);
        public static readonly QuantityKind Current = new("current", T("Current", "Strom"), Cur);
        public static readonly QuantityKind Power = new("power", T("Power", "Leistung"), Power_);
        public static readonly QuantityKind ApparentPower = new("apparentPower", T("Apparent power", "Scheinleistung"), Power_);
        public static readonly QuantityKind ReactivePower = new("reactivePower", T("Reactive power", "Blindleistung"), Power_);
        public static readonly QuantityKind HeatFlow = new("heatFlow", T("Heat flow", "Wärmestrom"), Power_);
        public static readonly QuantityKind Resistance = new("resistance", T("Resistance", "Widerstand"), Volt_ / Cur);
        public static readonly QuantityKind Capacitance = new("capacitance", T("Capacitance", "Kapazität"), Cur * Time / Volt_);
        public static readonly QuantityKind Inductance = new("inductance", T("Inductance", "Induktivität"), Volt_ * Time / Cur);
        public static readonly QuantityKind Charge = new("charge", T("Electric charge", "Ladung"), Cur * Time);
        public static readonly QuantityKind Resistivity = new("resistivity", T("Resistivity", "Spezifischer Widerstand"), Volt_ / Cur * Len);
        public static readonly QuantityKind Frequency = new("frequency", T("Frequency", "Frequenz"), Dimension.None / Time);
        public static readonly QuantityKind RotationalSpeed = new("rotationalSpeed", T("Speed of rotation", "Drehzahl"), Ang / Time);
        public static readonly QuantityKind Angle = new("angle", T("Angle", "Winkel"), Ang);
        public static readonly QuantityKind Time_ = new("time", T("Time", "Zeit"), Time);
        public static readonly QuantityKind Length = new("length", T("Length", "Länge"), Len);
        public static readonly QuantityKind Area = new("area", T("Area", "Fläche"), Len.Pow(2));
        public static readonly QuantityKind Volume = new("volume", T("Volume", "Volumen"), Len.Pow(3));
        public static readonly QuantityKind Mass_ = new("mass", T("Mass", "Masse"), Mass);
        public static readonly QuantityKind Force = new("force", T("Force", "Kraft"), Force_);
        public static readonly QuantityKind Torque = new("torque", T("Torque", "Drehmoment"), Energy_);
        public static readonly QuantityKind Energy = new("energy", T("Energy", "Energie"), Energy_);
        public static readonly QuantityKind Speed = new("speed", T("Speed", "Geschwindigkeit"), Len / Time);
        public static readonly QuantityKind Acceleration = new("acceleration", T("Acceleration", "Beschleunigung"), Len / Time.Pow(2));
        public static readonly QuantityKind Pressure = new("pressure", T("Pressure", "Druck"), Force_ / Len.Pow(2));
        public static readonly QuantityKind VolumeFlow = new("volumeFlow", T("Volume flow", "Volumenstrom"), Len.Pow(3) / Time);
        public static readonly QuantityKind MassFlow = new("massFlow", T("Mass flow", "Massenstrom"), Mass / Time);
        public static readonly QuantityKind Temperature = new("temperature", T("Temperature", "Temperatur"), Temp);
        public static readonly QuantityKind TemperatureDifference = new("temperatureDifference", T("Temperature difference", "Temperaturdifferenz"), Temp, difference: true);
        public static readonly QuantityKind Density = new("density", T("Density", "Dichte"), Mass / Len.Pow(3));
        public static readonly QuantityKind SpecificHeat = new("specificHeat", T("Specific heat capacity", "Spezifische Wärmekapazität"), Energy_ / Mass / Temp);
        public static readonly QuantityKind HeatTransfer = new("heatTransfer", T("Heat transfer coefficient", "Wärmedurchgangskoeffizient"), Power_ / Len.Pow(2) / Temp);
        public static readonly QuantityKind Irradiance = new("irradiance", T("Irradiance", "Bestrahlungsstärke"), Power_ / Len.Pow(2));
        public static readonly QuantityKind Viscosity = new("kinematicViscosity", T("Kinematic viscosity", "Kinematische Viskosität"), Len.Pow(2) / Time);
        public static readonly QuantityKind Inertia = new("inertia", T("Moment of inertia", "Trägheitsmoment"), Mass * Len.Pow(2));
        public static readonly QuantityKind SpringRate = new("springRate", T("Spring rate", "Federrate"), Force_ / Len);
        /// <summary>Rate of a torsion spring or shaft (torque per angle).</summary>
        public static readonly QuantityKind TorsionalRate = new("torsionalRate", T("Torsional spring rate", "Drehfederrate"), Energy_ / Ang);
        /// <summary>Relative change per kelvin (PV temperature coefficients, resistance coefficients): %/K, 1/K, ppm/K.</summary>
        public static readonly QuantityKind TemperatureCoefficient = new("temperatureCoefficient", T("Temperature coefficient", "Temperaturkoeffizient"), Dimension.None / Temp);

        private static readonly List<QuantityKind> AllKinds = new();

        static Quantities()
        {
            const double Pi = Math.PI;
            Ratio.Add("", 1, new[] { "1", "x", "×" });
            Ratio.Add("%", 0.01);
            Efficiency.Add("%", 0.01);
            Efficiency.Add("", 1, new[] { "1" });
            Count.Add("", 1, new[] { "x", "×", "pcs", "Stk", "Stk." });
            Voltage.Add("V", 1, null, 0, "nµmkMG");
            Current.Add("A", 1, null, 0, "pnµmk");
            Power.Add("W", 1, null, 0, "nµmkMGT");
            Power.Add("Wp", 1, null, 0, "kMG");
            Power.Add("hp", 745.69987158227022);
            Power.Add("PS", 735.49875);
            ApparentPower.Add("VA", 1, null, 0, "kMG");
            ReactivePower.Add("var", 1, new[] { "VAr", "Var", "VAR" }, 0, "kMG");
            HeatFlow.Add("W", 1, null, 0, "mkMG");
            Resistance.Add("Ω", 1, new[] { "Ohm", "ohm", "R" }, 0, "µmkMG");
            Capacitance.Add("F", 1, null, 0, "pnµm");
            Inductance.Add("H", 1, null, 0, "pnµm");
            Charge.Add("Ah", 3600, null, 0, "mk");
            Charge.Add("C", 1, null, 0, "pnµm");
            Resistivity.Add("Ω·mm²/m", 1e-6, new[] { "Ohm·mm²/m", "ohm·mm²/m" });
            Resistivity.Add("Ω·m", 1, new[] { "Ohm·m", "ohm·m" });
            Frequency.Add("Hz", 1, null, 0, "mkMG");
            Frequency.Add("s⁻¹", 1, new[] { "1/s" });
            // Air changes and other rates per hour (building services).
            Frequency.Add("h⁻¹", 1.0 / 3600, new[] { "1/h" });
            RotationalSpeed.Add("min⁻¹", 2 * Pi / 60, new[] { "rpm", "RPM", "1/min", "/min", "U/min", "r/min", "rev/min" });
            RotationalSpeed.Add("rad/s", 1);
            RotationalSpeed.Add("rev/s", 2 * Pi, new[] { "rps", "r/s", "U/s" });
            Angle.Add("°", Pi / 180, new[] { "deg" });
            Angle.Add("rad", 1, null, 0, "m");
            Angle.Add("rev", 2 * Pi, new[] { "turn" });
            Time_.Add("s", 1, new[] { "sec" }, 0, "pnµm");
            Time_.Add("min", 60);
            Time_.Add("h", 3600);
            Time_.Add("d", 86400);
            Length.Add("m", 1, null, 0, "nµmk");
            Length.Add("mm", 1e-3);
            Length.Add("cm", 1e-2);
            Length.Add("km", 1e3);
            Length.Add("µm", 1e-6);
            Length.Add("in", 0.0254, new[] { "″", "inch" });
            Length.Add("ft", 0.3048, new[] { "′" });
            Area.Add("m²", 1);
            Area.Add("mm²", 1e-6);
            Area.Add("cm²", 1e-4);
            Area.Add("km²", 1e6);
            Area.Add("ha", 1e4);
            Volume.Add("m³", 1);
            Volume.Add("l", 1e-3, new[] { "L", "ℓ" }, 0, "mcdh");
            Volume.Add("dm³", 1e-3);
            Volume.Add("cm³", 1e-6, new[] { "ccm" });
            Volume.Add("mm³", 1e-9);
            Mass_.Add("kg", 1);
            Mass_.Add("g", 1e-3, null, 0, "µm");
            Mass_.Add("t", 1e3);
            Mass_.Add("lb", 0.45359237);
            Force.Add("N", 1, null, 0, "mkM");
            Force.Add("lbf", 4.4482216152605);
            Torque.Add("N·m", 1, new[] { "Nm" });
            Torque.Add("kN·m", 1e3, new[] { "kNm" });
            Torque.Add("mN·m", 1e-3, new[] { "mNm" });
            Energy.Add("J", 1, null, 0, "mkMG");
            Energy.Add("Wh", 3600, null, 0, "mkMGT");
            Energy.Add("cal", 4.184, null, 0, "k");
            Energy.Add("eV", 1.602176634e-19, null, 0, "kMG");
            Speed.Add("m/s", 1);
            Speed.Add("km/h", 1 / 3.6, new[] { "kmh" });
            Speed.Add("m/min", 1 / 60.0);
            Speed.Add("mm/s", 1e-3);
            Speed.Add("mph", 0.44704);
            Acceleration.Add("m/s²", 1);
            Pressure.Add("bar", 1e5, null, 0, "m");
            Pressure.Add("Pa", 1, null, 0, "kMG");
            Pressure.Add("hPa", 100);
            Pressure.Add("N/mm²", 1e6);
            Pressure.Add("psi", 6894.757293168361);
            VolumeFlow.Add("m³/h", 1 / 3600.0);
            VolumeFlow.Add("l/min", 1e-3 / 60, new[] { "L/min" });
            VolumeFlow.Add("l/s", 1e-3, new[] { "L/s" });
            VolumeFlow.Add("l/h", 1e-3 / 3600, new[] { "L/h" });
            VolumeFlow.Add("m³/s", 1);
            MassFlow.Add("kg/s", 1);
            MassFlow.Add("kg/h", 1 / 3600.0);
            MassFlow.Add("g/s", 1e-3);
            MassFlow.Add("t/h", 1 / 3.6);
            Temperature.Add("°C", 1, new[] { "degC" }, 273.15);
            Temperature.Add("K", 1);
            Temperature.Add("°F", 5.0 / 9.0, new[] { "degF" }, 273.15 - 32 * 5.0 / 9.0);
            TemperatureDifference.Add("K", 1, null, 0, "m");
            Density.Add("kg/m³", 1);
            Density.Add("g/cm³", 1e3);
            Density.Add("kg/l", 1e3, new[] { "kg/L" });
            SpecificHeat.Add("kJ/(kg·K)", 1e3, new[] { "kJ/kgK", "kJ/(kgK)" });
            SpecificHeat.Add("J/(kg·K)", 1, new[] { "J/kgK", "J/(kgK)" });
            HeatTransfer.Add("W/(m²·K)", 1, new[] { "W/m²K", "W/(m²K)" });
            Irradiance.Add("W/m²", 1);
            Irradiance.Add("kW/m²", 1e3);
            Viscosity.Add("mm²/s", 1e-6, new[] { "cSt" });
            Viscosity.Add("m²/s", 1);
            Inertia.Add("kg·m²", 1, new[] { "kgm²" });
            SpringRate.Add("N/mm", 1e3);
            SpringRate.Add("N/m", 1);
            TorsionalRate.Add("N·m/rad", 1, new[] { "Nm/rad" });
            TorsionalRate.Add("N·m/°", 180 / Pi, new[] { "Nm/°", "Nm/deg", "N·m/deg" });
            TemperatureCoefficient.Add("%/K", 0.01, new[] { "%/°C" });
            TemperatureCoefficient.Add("1/K", 1, new[] { "K⁻¹", "1/°C" });
            TemperatureCoefficient.Add("ppm/K", 1e-6, new[] { "ppm/°C" });

            // Preference order when a calculation result has to be named by its dimension (power before heat flow,
            // energy before torque, frequency before nothing).
            AllKinds.AddRange(new[]
            {
                Ratio, Efficiency, Count, Voltage, Current, Power, ApparentPower, ReactivePower, HeatFlow, Resistance, Capacitance,
                Inductance, Charge, Resistivity, Frequency, RotationalSpeed, Angle, Time_, Length, Area, Volume, Mass_, Force,
                Energy, Torque, Speed, Acceleration, Pressure, VolumeFlow, MassFlow, Temperature, TemperatureDifference, Density,
                SpecificHeat, HeatTransfer, Irradiance, Viscosity, Inertia, SpringRate, TorsionalRate, TemperatureCoefficient,
            });
        }

        /// <summary>All kinds in preference order (the first kind with a dimension names calculation results).</summary>
        public static IReadOnlyList<QuantityKind> All => AllKinds;

        /// <summary>The kind with this id, or null.</summary>
        public static QuantityKind? Get(string? id) => id is null ? null : AllKinds.FirstOrDefault(k => k.Id == id);

        /// <summary>The preferred kind for a dimension (exact, then with the angle dropped), or null.</summary>
        public static QuantityKind? ForDimension(Dimension d)
        {
            if (d.IsNone) return Ratio;
            return AllKinds.FirstOrDefault(k => k.Dimension == d && !k.IsDifference)
                ?? AllKinds.FirstOrDefault(k => k.Dimension.WithoutAngle == d.WithoutAngle && k.Dimension.A == 0 && !k.IsDifference);
        }
    }
}
