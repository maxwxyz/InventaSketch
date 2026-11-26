using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace InventaSketch.Core
{
    /// <summary>
    /// Tolerance of an entered value: relative (fractions, "±5 %" → 0.05/0.05, "−20/+80 %" → 0.2/0.8) or absolute (SI,
    /// "±0,1 V"). Kept with the value; interval results follow in Calc 6.
    /// </summary>
    public sealed record Tolerance(double Minus, double Plus, bool Relative)
    {
        /// <summary>Lower and upper bound of a value with this tolerance.</summary>
        public (double Lo, double Hi) Bounds(double value) => Relative
            ? (value - Math.Abs(value) * Minus, value + Math.Abs(value) * Plus)
            : (value - Minus, value + Plus);
    }

    /// <summary>A parsed value: SI number(s), the unit it was typed in (or the field's), tolerance and the text as typed.</summary>
    public sealed record ParsedValue
    {
        /// <summary>The value in coherent SI. For a ratio typed as a fraction ("20/0,4", "3:1") the quotient; for several
        /// values of another kind ("230/12 V") the first one.</summary>
        public double Value { get; init; }
        /// <summary>Every value of a fraction or list ("230/12 V" → 230, 12), SI; a single value otherwise.</summary>
        public IReadOnlyList<double> Parts { get; init; } = Array.Empty<double>();
        /// <summary>The unit of the (last) part: typed, or the field's display unit for a bare number.</summary>
        public Unit Unit { get; init; } = null!;
        /// <summary>True if a unit (or IEC 60062 letter code / prefix) was typed.</summary>
        public bool UnitTyped { get; init; }
        public Tolerance? Tolerance { get; init; }
        public QuantityKind Kind { get; init; } = Quantities.Ratio;
        /// <summary>The input text, trimmed.</summary>
        public string Text { get; init; } = "";
    }

    /// <summary>
    /// The one value parser (Calc 0, engineering-calculations.md section 1.3). Accepts a decimal comma or point (both
    /// present: the later one is the decimal separator), SI prefixes p n µ/u m k M G, the IEC 60062 letter code with the
    /// letter as decimal marker ("4k7", "2n2", "2u2", "4R7", "R47"; R for resistances only), units with or without a blank
    /// ("10µF", "1,5 kW", "1500 rpm", "1500/min", "40 l/min", "2,5 mm²", "20 °C"), fractions ("20/0,4", "3:1", "230/12 V")
    /// and tolerances ("4k7 ±5 %", "10k 1%", "100 µF −20/+80 %", "5 V ±0,1 V"). A bare number takes the field's display
    /// unit; a letter code or bare prefix ("4k7", "2.5k") applies to the kind's coherent unit (Ω, F, …). A unit of another
    /// kind is rejected with a bilingual message.
    /// </summary>
    public static class ValueParser
    {
        private const string Num = @"\d+(?:[.,]\d+)?";
        private static readonly Regex AsymTol = new(@"^(?<main>.*?\S)\s*-\s*(?<m>" + Num + @")\s*/\s*\+\s*(?<p>" + Num + @")\s*%\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex SymTol = new(@"^(?<main>.*?\S)\s*(?:±|\+/-|\+-)\s*(?<t>" + Num + @")\s*(?<tu>.*?)\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex PctTol = new(@"^(?<main>.*\S)\s+(?<t>" + Num + @")\s*%\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex LeadingR = new(@"^[Rr](?<b>\d+)\s*(?<u>.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Parses <paramref name="text"/> for a field of <paramref name="kind"/>; throws <see cref="FormatException"/> with the message in <paramref name="lang"/>.</summary>
        public static ParsedValue Parse(string? text, QuantityKind kind, Lang lang = Lang.En, string? displayUnit = null)
            => TryParse(text, kind, out var v, out var error, lang, displayUnit) ? v! : throw new FormatException(error!.Get(lang));

        /// <summary>
        /// Parses <paramref name="text"/> for a field of <paramref name="kind"/> whose bare numbers are in
        /// <paramref name="displayUnit"/> (null = the kind's default unit). <paramref name="lang"/> German also accepts
        /// "°C" for a temperature difference.
        /// </summary>
        public static bool TryParse(string? text, QuantityKind kind, out ParsedValue? value, out I18nText? error, Lang lang = Lang.En, string? displayUnit = null)
        {
            value = null; error = null;
            var t = Clean(text);
            if (t.Length == 0) { error = new I18nText("Enter a value.", "Bitte einen Wert eingeben."); return false; }
            if (kind.IsText)
            {
                // A text-valued variable (a class such as the Grashof class) is only ever computed from other values.
                error = new I18nText("This value is determined from other values and cannot be entered.",
                                     "Dieser Wert ergibt sich aus anderen Werten und lässt sich nicht eingeben.");
                return false;
            }
            var display =(displayUnit is null ? null : UnitCatalog.Find(displayUnit, kind)) ?? kind.DefaultUnit;

            // Tolerance at the end.
            var main = t;
            string? tolAbsText = null;
            Tolerance? tol = null;
            var hasPercent = kind.Units.Count > 0 && (kind.Units[0].Symbol == "%" || kind.Units.Count > 1 && kind.Units[1].Symbol == "%");
            Match m;
            var mayHaveTolerance = t.IndexOf('%') >= 0 || t.IndexOf('±') >= 0 || t.Contains("+/-") || t.Contains("+-");
            if (!mayHaveTolerance) { }
            else if ((m = AsymTol.Match(t)).Success)
            {
                main = m.Groups["main"].Value;
                tol = new Tolerance(Dec(m.Groups["m"].Value) / 100, Dec(m.Groups["p"].Value) / 100, true);
            }
            else if ((m = SymTol.Match(t)).Success)
            {
                main = m.Groups["main"].Value;
                var tu = m.Groups["tu"].Value.Trim();
                if (tu == "%") tol = new Tolerance(Dec(m.Groups["t"].Value) / 100, Dec(m.Groups["t"].Value) / 100, true);
                else tolAbsText = m.Groups["t"].Value + " " + tu;
            }
            else if (!hasPercent && (m = PctTol.Match(t)).Success)
            {
                main = m.Groups["main"].Value;
                tol = new Tolerance(Dec(m.Groups["t"].Value) / 100, Dec(m.Groups["t"].Value) / 100, true);
            }

            // Fractions and lists: "20/0,4", "3:1", "230/12 V" (a '/' followed by a letter belongs to a unit: "1500/min").
            // A unit with a number after its slash ("16 kWh/100 km") is one value, not a fraction.
            var parts = ScanNumber(main.Trim(), out _, out var unitAfter) && unitAfter.IndexOf('/') > 0 && UnitCatalog.Find(unitAfter, kind) is not null
                ? new List<string> { main.Trim() }
                : SplitParts(main);
            var raw = new List<(double Number, Unit? Unit, bool Typed)>();
            foreach (var p in parts)
            {
                if (!TryPart(p, kind, lang, out var number, out var unit, out var typed, out error, t)) return false;
                raw.Add((number, unit, typed));
            }
            // Parts without a unit take the unit typed last ("230/12 V"), else the display unit.
            var lastUnit = raw.LastOrDefault(r => r.Unit is not null).Unit;
            var si = raw.Select(r => (r.Unit ?? lastUnit ?? display).ToSi(r.Number)).ToList();
            var unitUsed = lastUnit ?? display;
            if (!si.All(Finite)) { error = NotValid(t); return false; }

            double result;
            if (si.Count > 1 && kind.Dimension.IsNone)
            {
                if (si.Count != 2 || si[1] == 0) { error = NotValid(t); return false; }
                result = si[0] / si[1];
            }
            else result = si[0];

            if (tolAbsText is not null)
            {
                if (!TryPart(tolAbsText.Trim(), kind, lang, out var tn, out var tunit, out _, out error, t)) return false;
                var tf = (tunit ?? unitUsed).Factor;
                tol = new Tolerance(tn * tf, tn * tf, false);
            }

            value = new ParsedValue
            {
                Value = result, Parts = si, Unit = unitUsed, UnitTyped = raw.Any(r => r.Typed), Tolerance = tol, Kind = kind, Text = (text ?? "").Trim(),
            };
            return true;
        }

        /// <summary>One number with an optional letter code, prefix or unit. Unit null = none typed.</summary>
        private static bool TryPart(string p, QuantityKind kind, Lang lang, out double number, out Unit? unit, out bool typed, out I18nText? error, string whole)
        {
            number = double.NaN; unit = null; typed = false; error = null;
            p = p.Trim();
            string unitText;
            Match m;
            var coherent = Coherent(kind);
            if (ScanLetterCode(p, out var a, out var l, out var b, out var rest) && (l is not ('R' or 'r') || kind == Quantities.Resistance))
            {
                var f = l switch { 'R' or 'r' => 1.0, 'u' or 'μ' => 1e-6, 'K' => 1e3, _ => UnitCatalog.PrefixFactors.TryGetValue(l.ToString(), out var pf) ? pf : 1.0 };
                number = double.Parse(a + "." + b, CultureInfo.InvariantCulture) * f;
                unitText = rest;
                typed = true;
                if (unitText.Length == 0) { unit = coherent; return true; }
            }
            else if (kind == Quantities.Resistance && (m = LeadingR.Match(p)).Success)
            {
                number = double.Parse("0." + m.Groups["b"].Value, CultureInfo.InvariantCulture);
                unitText = m.Groups["u"].Value.Trim();
                typed = true;
                if (unitText.Length == 0) { unit = coherent; return true; }
            }
            else if (ScanNumber(p, out var num, out var after) && TryNumber(num, out number))
            {
                unitText = after;
                if (unitText.Length == 0) return true;
                typed = true;
            }
            else { error = NotValid(whole); return false; }

            // Unit of this kind (German: "°C" also for a temperature difference).
            if (kind.IsDifference && lang == Lang.De && UnitCatalog.Norm(unitText) == "°C") { unit = kind.DefaultUnit; return true; }
            var u = UnitCatalog.Find(unitText, kind) ?? kind.LegacyUnit(unitText);
            if (u is not null) { unit = u; return true; }
            // A bare prefix ("2.5k", "100n") multiplies the kind's coherent unit.
            var px = unitText == "u" || unitText == "μ" ? "µ" : unitText == "K" ? "k" : unitText;
            if (UnitCatalog.PrefixFactors.TryGetValue(px, out var factor) && coherent is not null)
            {
                number *= factor;
                unit = coherent;
                return true;
            }
            var other = UnitCatalog.FindAll(unitText).FirstOrDefault();
            error = other is null ? Units.UnknownUnit(unitText) : WrongKind(whole, other.Kind, kind);
            return false;
        }

        /// <summary>The kind's unit with factor 1 and no offset (Ω, F, V …), or null (e.g. temperature in °C only).</summary>
        private static Unit? Coherent(QuantityKind kind) => kind.Units.FirstOrDefault(u => u.Factor == 1 && u.Offset == 0);

        private static List<string> SplitParts(string s)
        {
            var parts = new List<string>();
            var start = 0;
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] != '/' && s[i] != ':') continue;
                var j = i + 1;
                while (j < s.Length && s[j] == ' ') j++;
                var k = i - 1;
                while (k >= start && s[k] == ' ') k--;
                if (k < start || j >= s.Length) continue;
                var c = s[j];
                var numberFollows = char.IsDigit(c) || (c is '.' or ',' or '+' or '-') && j + 1 < s.Length && char.IsDigit(s[j + 1]);
                if (!numberFollows) continue;
                // "1/min" style units after a number ("1500 1/min") stay whole: a part needs a digit before the separator
                // or a complete quantity ("230 V/12 V").
                parts.Add(s.Substring(start, i - start));
                start = i + 1;
            }
            parts.Add(s.Substring(start));
            return parts.Select(x => x.Trim()).ToList();
        }

        /// <summary>IEC 60062 letter code at the start: digits, a letter of p n u µ m R k K M G, digits; the rest (trimmed) is a unit.</summary>
        private static bool ScanLetterCode(string p, out string a, out char l, out string b, out string rest)
        {
            a = b = rest = ""; l = '\0';
            var i = 0;
            while (i < p.Length && char.IsDigit(p[i])) i++;
            if (i == 0 || i + 1 >= p.Length || "pnuµμmRrkKMG".IndexOf(p[i]) < 0 || !char.IsDigit(p[i + 1])) return false;
            a = p.Substring(0, i);
            l = p[i];
            var j = i + 1;
            while (j < p.Length && char.IsDigit(p[j])) j++;
            b = p.Substring(i + 1, j - i - 1);
            rest = p.Substring(j).Trim();
            return true;
        }

        /// <summary>A number at the start (sign, digits with separators, exponent); the rest (trimmed) is a unit.</summary>
        private static bool ScanNumber(string p, out string number, out string rest)
        {
            number = rest = "";
            var i = 0;
            if (i < p.Length && (p[i] == '+' || p[i] == '-')) i++;
            if (i < p.Length && (p[i] == '.' || p[i] == ',') && i + 1 < p.Length && char.IsDigit(p[i + 1])) i++;
            if (i >= p.Length || !char.IsDigit(p[i])) return false;
            var lastDigit = i;
            while (i < p.Length && (char.IsDigit(p[i]) || p[i] is '.' or ',' or '\''))
            {
                if (char.IsDigit(p[i])) lastDigit = i;
                i++;
            }
            i = lastDigit + 1;
            if (i + 1 < p.Length && (p[i] is 'e' or 'E') && (char.IsDigit(p[i + 1]) || (p[i + 1] is '+' or '-') && i + 2 < p.Length && char.IsDigit(p[i + 2])))
            {
                i += 2;
                while (i < p.Length && char.IsDigit(p[i])) i++;
            }
            number = p.Substring(0, i);
            rest = p.Substring(i).Trim();
            return true;
        }

        private static string Clean(string? text)
        {
            var t = (text ?? "").Trim();
            var plain = true;
            foreach (var ch in t) if (ch is ' ' or ' ' or ' ' or '−' or '–') { plain = false; break; }
            if (plain) return t;
            return t.Replace(' ', ' ').Replace(' ', ' ').Replace(' ', ' ').Replace('−', '-').Replace('–', '-');
        }

        private static double Dec(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static I18nText NotValid(string t) => new($"Not a valid value: “{t}”.", $"Kein gültiger Wert: „{t}“.");

        private static I18nText WrongKind(string t, QuantityKind found, QuantityKind expected) => new(
            $"“{t}” is a {found.Name.En.ToLowerInvariant()}; this field expects a {expected.Name.En.ToLowerInvariant()}.",
            $"„{t}“ ist eine Angabe für {found.Name.De}; dieses Feld erwartet {expected.Name.De}.");

        /// <summary>
        /// A plain number as typed in either language: blanks, thin spaces and apostrophes are ignored, a trailing '%' is
        /// dropped, a decimal comma or point is accepted; with both, the later one is the decimal separator; several commas
        /// or several points are thousands separators ("1.234.567").
        /// </summary>
        public static bool TryNumber(string? token, out double value)
        {
            value = double.NaN;
            if (token is null) return false;
            var t = token.Trim();
            if (t.Length == 0 || t[0] == '"') return false;
            t = t.Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace("'", "").Replace(" ", "").Replace('−', '-');
            if (t.EndsWith("%", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);
            if (t.Length == 0) return false;
            var comma = t.LastIndexOf(',');
            var dot = t.LastIndexOf('.');
            if (comma >= 0 && dot >= 0)
                t = comma > dot ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
            else if (comma >= 0)
                t = t.Count(c => c == ',') == 1 ? t.Replace(',', '.') : t.Replace(",", "");
            else if (dot >= 0 && t.Count(c => c == '.') > 1)
                t = t.Replace(".", "");
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !Finite(v)) return false;
            value = v;
            return true;
        }

        /// <summary>
        /// A number for a plain input field ("2.5", "2,5", "1 kHz", "2.5k"): a unit suffix in <paramref name="units"/> is
        /// ignored, a trailing SI prefix k/K, M, m, µ/u multiplies. Throws a bilingual <see cref="FormatException"/> for
        /// anything else or a value outside [<paramref name="min"/>, <paramref name="max"/>].
        /// </summary>
        public static double ParseNumber(string? text, Lang lang, double min, double max, params string[] units)
        {
            var t = (text ?? "").Trim();
            foreach (var u in units.OrderByDescending(u => u.Length))
                if (u.Length > 0 && t.EndsWith(u, StringComparison.OrdinalIgnoreCase)) { t = t.Substring(0, t.Length - u.Length).Trim(); break; }
            var factor = 1.0;
            if (t.Length > 1)
            {
                var last = t[t.Length - 1];
                var f = last switch { 'k' or 'K' => 1e3, 'M' => 1e6, 'm' => 1e-3, 'µ' or 'u' => 1e-6, _ => 0.0 };
                if (f > 0) { factor = f; t = t.Substring(0, t.Length - 1).Trim(); }
            }
            if (!TryNumber(t, out var v) || !Finite(v * factor) || v * factor < min || v * factor > max)
                throw new FormatException(lang == Lang.De ? "Keine gültige Zahl: „" + text + "“" : "Not a valid number: “" + text + "”");
            return v * factor;
        }

        /// <summary>A length typed by the user in millimetres ("1010", "1010 mm", "1,01 m", "101 cm", "-50"); throws a bilingual <see cref="FormatException"/>.</summary>
        public static double ParseMillimetres(string? text, Lang lang)
        {
            // Blanks between digits are thousands separators here ("1 010 mm").
            var t = Regex.Replace(text ?? "", @"(?<=\d)\s+(?=\d)", "");
            if (TryParse(t, Quantities.Length, out var v, out _, lang, "mm") && v!.Parts.Count == 1 && v.Tolerance is null)
                return Math.Round(v.Value * 1000, 9);
            throw new FormatException(lang == Lang.De ? "Keine gültige Länge: „" + text + "“" : "Not a valid length: “" + text + "”");
        }
    }

    /// <summary>
    /// Value display (Calc 0): significant digits (3 in Explore, 2–6 in Engineer), the SI prefix that keeps the number
    /// between 1 and 1000 within the unit's prefix list (4.7 kΩ, 2.2 µF, 1.2 MW), the decimal separator of the language.
    /// </summary>
    public static class ValueFormat
    {
        /// <summary>A number rounded to <paramref name="digits"/> significant digits ("4.7", "1500", "0.0022"); trailing zeros
        /// are kept only with <paramref name="keepZeros"/> ("2.00").</summary>
        public static string Number(double v, Lang lang, int digits = 3, bool keepZeros = false)
        {
            if (double.IsNaN(v)) return "–";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "−∞";
            digits = Math.Max(1, Math.Min(15, digits));
            string s;
            if (v == 0) s = keepZeros && digits > 1 ? "0." + new string('0', digits - 1) : "0";
            else
            {
                var r = RoundSig(v, digits);
                var exp = (int)Math.Floor(Math.Log10(Math.Abs(r)));
                if (exp >= 15 || exp < -12) s = r.ToString("0." + new string(keepZeros ? '0' : '#', Math.Max(1, digits - 1)) + "E+0", CultureInfo.InvariantCulture);
                else
                {
                    var decimals = Math.Max(0, digits - 1 - exp);
                    s = r.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                    if (!keepZeros && s.Contains(".")) s = s.TrimEnd('0').TrimEnd('.');
                }
            }
            if (s.StartsWith("-", StringComparison.Ordinal) && s.Trim('-', '0', '.') == "") s = s.Substring(1);
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>Rounds to significant digits.</summary>
        public static double RoundSig(double v, int digits)
        {
            if (v == 0 || double.IsNaN(v) || double.IsInfinity(v)) return v;
            var exp = (int)Math.Floor(Math.Log10(Math.Abs(v)));
            var scale = Math.Pow(10, digits - 1 - exp);
            var r = Math.Round(v * scale) / scale;
            return double.IsNaN(r) || double.IsInfinity(r) ? v : r;
        }

        /// <summary>
        /// An SI value of <paramref name="kind"/> in <paramref name="unit"/> (a spelling; null = the kind's default) with the
        /// best prefix when the unit takes prefixes: "4.7 kΩ", "20 °C", "1500 min⁻¹", "97 %".
        /// </summary>
        public static string Format(double si, QuantityKind kind, Lang lang, int digits = 3, string? unit = null, bool keepZeros = false)
            => Format(si, (unit is null ? null : UnitCatalog.Find(unit, kind)) ?? kind.DefaultUnit, lang, digits, keepZeros);

        /// <summary>An SI value in <paramref name="unit"/> (with the best prefix if the unit takes prefixes).</summary>
        public static string Format(double si, Unit unit, Lang lang, int digits = 3, bool keepZeros = false)
        {
            // A text-valued kind shows the label of the value ("Crank-rocker"), never the number behind it.
            if (unit.Kind.IsText) return unit.Kind.LabelOf(si)?.Get(lang) ?? "–";
            var (number, shown) = Scale(si, unit, digits);
            var n = Number(number, lang, digits, keepZeros);
            return shown.Symbol.Length == 0 ? n : n + " " + shown.Symbol;
        }

        /// <summary>The number and the (prefixed) unit an SI value is shown in.</summary>
        public static (double Number, Unit Unit) Scale(double si, Unit unit, int digits = 3)
        {
            var v = unit.FromSi(si);
            if (!unit.Prefixable || v == 0 || double.IsNaN(v) || double.IsInfinity(v)) return (v, unit);
            var options = new List<(string P, double F)> { ("", 1) };
            foreach (var ch in unit.Prefixes)
            {
                var p = ch.ToString();
                if (UnitCatalog.PrefixFactors.TryGetValue(p, out var f)) options.Add((p, f));
            }
            options.Sort((a, b) => a.F.CompareTo(b.F));
            var a = Math.Abs(v);
            var best = options[0];
            foreach (var o in options)
                if (Math.Abs(RoundSig(a / o.F, digits)) >= 1 - 1e-12) best = o;
            return (v / best.F, best.P.Length == 0 ? unit : unit.WithPrefix(best.P));
        }
    }
}
