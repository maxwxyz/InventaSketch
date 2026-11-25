using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Calc 0: quantity kinds, units, the shared value parser, formatting, conversion and the unit calculator.</summary>
    public class QuantityTests
    {
        private static double P(string text, QuantityKind kind, string? unit = null, Lang lang = Lang.En) => ValueParser.Parse(text, kind, lang, unit).Value;

        private static void Near(double expected, double actual, double rel = 1e-9) => Assert.True(Math.Abs(expected - actual) <= rel * Math.Max(1e-300, Math.Abs(expected)), $"expected {expected}, got {actual}");

        [Fact]
        public void LetterCodesAndPrefixes()
        {
            Near(4700, P("4k7", Quantities.Resistance));
            Near(4.7, P("4R7", Quantities.Resistance));
            Near(0.47, P("R47", Quantities.Resistance));
            Near(2.2e-9, P("2n2", Quantities.Capacitance));
            Near(2.2e-6, P("2u2", Quantities.Capacitance));
            Near(10e-6, P("10 µF", Quantities.Capacitance));
            Near(10e-6, P("10uF", Quantities.Capacitance));
            Near(10e-6, P("10μF", Quantities.Capacitance)); // Greek mu
            Near(10e3, P("10k", Quantities.Resistance));
            Near(4700, P("4,7 kΩ", Quantities.Resistance));
            Near(100, P("100R", Quantities.Resistance));
            Near(1e6, P("1 MOhm", Quantities.Resistance));
            Near(2500, P("2.5k", Quantities.Frequency));
        }

        [Fact]
        public void UnitsWithAndWithoutBlankAndDecimalComma()
        {
            Near(1500, P("1,5 kW", Quantities.Power));
            Near(1500, P("1.5kW", Quantities.Power));
            Near(16, P("16 A", Quantities.Current));
            Near(1500 * 2 * Math.PI / 60, P("1500 rpm", Quantities.RotationalSpeed));
            Near(1500 * 2 * Math.PI / 60, P("1500/min", Quantities.RotationalSpeed));
            Near(1500 * 2 * Math.PI / 60, P("1500 min⁻¹", Quantities.RotationalSpeed));
            Near(1500 * 2 * Math.PI / 60, P("1500 1/min", Quantities.RotationalSpeed));
            Near(1500 * 2 * Math.PI / 60, P("1500", Quantities.RotationalSpeed)); // bare: display unit min⁻¹
            Near(40e-3 / 60, P("40 l/min", Quantities.VolumeFlow));
            Near(2.5e-6, P("2,5 mm²", Quantities.Area));
            Near(2.5e-6, P("2.5 mm2", Quantities.Area));
            Near(2.5e-6, P("2.5 mm^2", Quantities.Area));
            Near(12.5, P("12,5 N·m", Quantities.Torque));
            Near(12.5, P("12.5 Nm", Quantities.Torque));
            Near(1e5 * 2.5, P("2,5 bar", Quantities.Pressure));
            Near(1234.5, P("1.234,5 W", Quantities.Power));
            Near(1234.5, P("1,234.5 W", Quantities.Power));
            Near(0.97, P("97 %", Quantities.Efficiency));
            Near(5000, P("10 KW", Quantities.Power) / 2); // case-insensitive fallback
        }

        [Fact]
        public void TemperatureAndDifferenceAreKeptApart()
        {
            Near(293.15, P("20 °C", Quantities.Temperature));
            Near(20, P("20 K", Quantities.Temperature));
            Near(293.15, P("20", Quantities.Temperature)); // bare number: °C
            Near(20, P("20 K", Quantities.TemperatureDifference));
            Near(20, P("20", Quantities.TemperatureDifference));
            Assert.False(ValueParser.TryParse("20 °C", Quantities.TemperatureDifference, out _, out var e, Lang.En));
            Assert.Contains("temperature difference", e!.En);
            Near(20, P("20 °C", Quantities.TemperatureDifference, null, Lang.De)); // German: accepted, shown as K
            Near(273.15 + 100, P("212 °F", Quantities.Temperature));
            Assert.Equal("20 °C", ValueFormat.Format(293.15, Quantities.Temperature, Lang.En));
            Assert.Equal("20 K", ValueFormat.Format(20, Quantities.TemperatureDifference, Lang.En));
        }

        [Fact]
        public void CoefficientsRatesAndTorsion()
        {
            // PV temperature coefficient (Calc 4), air changes per hour (building services), torsion springs (Calc 3).
            Assert.Equal(-0.0027, P("-0,27 %/K", Quantities.TemperatureCoefficient, lang: Lang.De), 12);
            Assert.Equal(-0.0027, P("-0.27 %/K", Quantities.TemperatureCoefficient), 12);
            Assert.Equal(4e-6, P("4 ppm/K", Quantities.TemperatureCoefficient), 15);
            Assert.Equal(0.5 / 3600, P("0.5 1/h", Quantities.Frequency), 12);
            Assert.Equal(2.0 / 3600, P("2 h⁻¹", Quantities.Frequency), 12);
            Assert.Equal(50, P("50 N·m/rad", Quantities.TorsionalRate), 9);
            Assert.Equal(180 / Math.PI, P("1 Nm/°", Quantities.TorsionalRate), 9);
        }

        [Fact]
        public void FractionsAndLists()
        {
            Near(50, P("20/0,4", Quantities.Ratio));
            Near(3, P("3:1", Quantities.Ratio));
            var v = ValueParser.Parse("230/12 V", Quantities.Voltage);
            Assert.Equal(2, v.Parts.Count);
            Near(230, v.Parts[0]);
            Near(12, v.Parts[1]);
            Near(230, v.Value);
            Assert.Equal("V", v.Unit.Symbol);
            var kv = ValueParser.Parse("20/0,4 kV", Quantities.Voltage);
            Near(400, kv.Parts[1]);
        }

        [Fact]
        public void Tolerances()
        {
            var a = ValueParser.Parse("4k7 ±5 %", Quantities.Resistance);
            Near(4700, a.Value);
            Assert.Equal(new Tolerance(0.05, 0.05, true), a.Tolerance);
            var b = ValueParser.Parse("10k 1%", Quantities.Resistance);
            Near(10000, b.Value);
            Assert.Equal(new Tolerance(0.01, 0.01, true), b.Tolerance);
            var c = ValueParser.Parse("100 µF −20/+80 %", Quantities.Capacitance);
            Near(100e-6, c.Value);
            Near(0.2, c.Tolerance!.Minus);
            Near(0.8, c.Tolerance.Plus);
            var d = ValueParser.Parse("5 V ±0,1 V", Quantities.Voltage);
            Assert.False(d.Tolerance!.Relative);
            Near(0.1, d.Tolerance.Plus);
            var (lo, hi) = a.Tolerance!.Bounds(a.Value);
            Near(4465, lo);
            Near(4935, hi);
            Near(0.97, P("97 %", Quantities.Efficiency)); // a percent on a percent field is the value, not a tolerance
        }

        [Fact]
        public void WrongKindIsRejectedBilingually()
        {
            Assert.False(ValueParser.TryParse("16 A", Quantities.Voltage, out _, out var e));
            Assert.Equal("“16 A” is a current; this field expects a voltage.", e!.En);
            Assert.Contains("Strom", e.De);
            Assert.Contains("Spannung", e.De);
            var ex = Assert.Throws<FormatException>(() => ValueParser.Parse("16 A", Quantities.Voltage, Lang.De));
            Assert.Contains("Spannung", ex.Message);
            Assert.False(ValueParser.TryParse("12 xyz", Quantities.Voltage, out _, out var u));
            Assert.Contains("Unknown unit", u!.En);
            Assert.False(ValueParser.TryParse("abc", Quantities.Voltage, out _, out _));
            Assert.False(ValueParser.TryParse("", Quantities.Voltage, out _, out _));
            Assert.False(ValueParser.TryParse("2 N·m", Quantities.Energy, out _, out var t)); // torque is not energy
            Assert.Contains("torque", t!.En);
        }

        [Fact]
        public void SignificantDigitsAndPrefixes()
        {
            Assert.Equal("4.7 kΩ", ValueFormat.Format(4700, Quantities.Resistance, Lang.En));
            Assert.Equal("4,7 kΩ", ValueFormat.Format(4700, Quantities.Resistance, Lang.De));
            Assert.Equal("2.2 µF", ValueFormat.Format(2.2e-6, Quantities.Capacitance, Lang.En));
            Assert.Equal("1.2 MW", ValueFormat.Format(1.2e6, Quantities.Power, Lang.En));
            Assert.Equal("2.00 A", ValueFormat.Format(2, Quantities.Current, Lang.En, keepZeros: true));
            Assert.Equal("115 Ω", ValueFormat.Format(115, Quantities.Resistance, Lang.En));
            Assert.Equal("1 kW", ValueFormat.Format(999.7, Quantities.Power, Lang.En)); // rounding moves to the next prefix
            Assert.Equal("1500 min⁻¹", ValueFormat.Format(1500 * 2 * Math.PI / 60, Quantities.RotationalSpeed, Lang.En));
            Assert.Equal("97 %", ValueFormat.Format(0.97, Quantities.Efficiency, Lang.En));
            Assert.Equal("48.9", ValueFormat.Number(48.87, Lang.En));
            Assert.Equal("0.0022", ValueFormat.Number(0.0022, Lang.En));
            Assert.Equal("12300", ValueFormat.Number(12345, Lang.En));
            Assert.Equal("1.235", ValueFormat.Number(1.23456, Lang.En, 4));
            Assert.Equal("0", ValueFormat.Number(-0.0, Lang.En));
            Assert.Equal("400 mm", ValueFormat.Format(0.4, Quantities.Length, Lang.En, unit: "mm"));
        }

        [Theory]
        [InlineData(4700.0, "resistance", 3)]
        [InlineData(2.2e-9, "capacitance", 3)]
        [InlineData(0.000123456, "current", 4)]
        [InlineData(1234567.0, "power", 3)]
        [InlineData(293.15, "temperature", 5)]
        [InlineData(157.0796, "rotationalSpeed", 3)]
        [InlineData(0.0123, "volumeFlow", 3)]
        public void FormatParseRoundTripWithinShownDigits(double si, string kindId, int digits)
        {
            var kind = Quantities.Get(kindId)!;
            foreach (var lang in new[] { Lang.En, Lang.De })
            {
                var text = ValueFormat.Format(si, kind, lang, digits);
                var back = ValueParser.Parse(text, kind, lang).Value;
                Assert.True(Math.Abs(back - si) <= Math.Abs(si) * 5 * Math.Pow(10, -digits), $"{text} → {back} vs {si}");
            }
        }

        [Fact]
        public void ConvertChecksKindsAndOffsets()
        {
            Near(1500 * 2 * Math.PI / 60, Units.Convert(1500, "rpm", "rad/s"));
            Near(68, Units.Convert(20, "°C", "°F"));
            Near(293.15, Units.Convert(20, "°C", "K"));
            Near(2.5, Units.Convert(2500, "W", "kW"));
            Near(25.4, Units.Convert(1, "in", "mm"));
            Near(3.6, Units.Convert(1, "kWh", "MJ"));
            Near(100, Units.Convert(1, "bar", "kPa"));
            Assert.Throws<FormatException>(() => Units.Convert(1, "m", "kg"));
            Assert.Throws<FormatException>(() => Units.Convert(1, "N·m", "J"));
            Assert.Throws<FormatException>(() => Units.Convert(1, "foo", "m"));
            Assert.False(Units.TryConvert(1, "m", "kg", out _, out var e));
            Assert.Contains("Länge", e!.De);
            Assert.Equal(new[] { "°C", "K", "°F" }, Units.DisplayUnits(Quantities.Temperature));
            Assert.True(Quantities.All.All(k => k.Units.Count > 0));
            Assert.Equal(Quantities.Torque.Dimension, Quantities.Energy.Dimension);
            Assert.NotEqual(Quantities.Frequency.Dimension, Quantities.RotationalSpeed.Dimension);
        }

        [Fact]
        public void SharedNumberAndLengthHelpers()
        {
            Assert.True(ValueParser.TryNumber("1.234.567", out var a)); Near(1234567, a);
            Assert.True(ValueParser.TryNumber("2,5", out var b)); Near(2.5, b);
            Assert.False(ValueParser.TryNumber("x", out _));
            Near(2500, ValueParser.ParseNumber("2.5 kHz", Lang.En, 0, 1e9, "Hz"));
            Assert.Throws<FormatException>(() => ValueParser.ParseNumber("abc", Lang.En, 0, 1));
            Assert.Equal(1010, ValueParser.ParseMillimetres("1010", Lang.En));
            Assert.Equal(1010, ValueParser.ParseMillimetres("1,01 m", Lang.De));
            Assert.Equal(1010, ValueParser.ParseMillimetres("101 cm", Lang.En));
            Assert.Equal(175, ValueParser.ParseMillimetres("175 mm", Lang.En));
            Assert.Equal(-50, ValueParser.ParseMillimetres("-50", Lang.En));
            Assert.Equal(1010, ValueParser.ParseMillimetres("1010 MM", Lang.En));
            Assert.Throws<FormatException>(() => ValueParser.ParseMillimetres("1 kg", Lang.En));
        }

        // ------------------------------------------------------------------ calculator

        private static CalcResult C(string text, Lang lang = Lang.En) => UnitCalculator.Evaluate(text, lang);

        [Fact]
        public void CalculatorExamples()
        {
            Assert.Equal("3.68 kW", C("230 V * 16 A").Text);
            Assert.Equal(Quantities.Power, C("230 V * 16 A").Kind);
            Assert.Equal("24.7 mm", C("12 mm + 0.5 in").Text);
            Assert.Equal("157.1 rad/s", C("1500 rpm in rad/s").Text);
            Assert.Equal("3.197 kΩ", C("(4k7 Ω || 10 kΩ)").Text);
            Assert.Equal("3197", C("4k7 || 10k").Text);
            Assert.Equal("8.696 Ah", C("2 kWh / 230 V").Text);
            Assert.Equal("8,696 Ah", C("2 kWh / 230 V", Lang.De).Text);
            Assert.Equal("3,68 kW", C("230 V · 16 A", Lang.De).Text);
            Assert.Equal("5,5 kW", C("2,5 kW + 3 kW", Lang.De).Text);
            Assert.Equal("68 °F", C("20 °C in °F").Text);
            Assert.Equal("25 °C", C("20 °C + 5 K").Text);
            Assert.Equal("10 K", C("30 °C - 20 °C").Text);
            Assert.Equal("1.571 kW", C("10 N·m * 1500 rpm").Text);
            Assert.Equal("1.571 kW", C("10 N·m * 1500 rpm in kW").Text);
            Assert.Equal("115 Ω", C("230 V / 2 A").Text);
            Assert.Equal("20 J", C("10 N * 2 m").Text);
            Assert.Equal("4", C("2^2").Text);
            Assert.Equal("100 mm²", C("(10 mm)²").Text);
            Assert.Equal("1.414", C("sqrt(2)").Text);
            Assert.Equal("0.5", C("sin(30 °)").Text);
            Assert.Equal("3.6 MJ", C("1 kWh in MJ").Text);
            Assert.Equal("10 m/s", C("36 km/h in m/s").Text);
            Assert.Equal("2.4 m³/h", C("40 l/min in m³/h").Text);
            Assert.Equal("9.81 m/s²", C("9.81 kg*m/s^2 / 1 kg").Text);
            Assert.True(C("230 V * 16 A").Steps.Count >= 2);
            Assert.Equal("= 3.68 kW", C("230 V * 16 A").Steps.Last());
        }

        [Fact]
        public void CalculatorErrors()
        {
            Assert.False(UnitCalculator.TryEvaluate("12 mm + 3 kg", out _, out var e));
            Assert.Contains("length", e!.En);
            Assert.Contains("Masse", e.De);
            Assert.False(UnitCalculator.TryEvaluate("230 V in m", out _, out var c));
            Assert.Contains("convert", c!.En);
            Assert.False(UnitCalculator.TryEvaluate("1500 rpm in Hz", out _, out _)); // 2π apart
            Assert.False(UnitCalculator.TryEvaluate("2 +", out _, out _));
            Assert.False(UnitCalculator.TryEvaluate("1/0", out _, out var z));
            Assert.Contains("zero", z!.En);
            Assert.False(UnitCalculator.TryEvaluate("system(\"x\")", out _, out _));
            Assert.False(UnitCalculator.TryEvaluate(new string('(', 200) + "1" + new string(')', 200), out _, out _));
            Assert.False(UnitCalculator.TryEvaluate(new string('1', 600), out _, out _));
            Assert.False(UnitCalculator.TryEvaluate("5 foo", out _, out var u));
            Assert.Contains("Unknown unit", u!.En);
            Assert.False(UnitCalculator.TryEvaluate("20 °C + 20 °C", out _, out _));
            Assert.Throws<FormatException>(() => UnitCalculator.Evaluate("", Lang.De));
        }
    }
}
