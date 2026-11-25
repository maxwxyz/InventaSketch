using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace InventaSketch.Core
{
    /// <summary>Result of <see cref="UnitCalculator"/>.</summary>
    public sealed record CalcResult
    {
        /// <summary>The value in coherent SI (rad for angles).</summary>
        public double Value { get; init; }
        public Dimension Dimension { get; init; }
        /// <summary>The quantity kind the result is shown as; null when no kind has this dimension.</summary>
        public QuantityKind? Kind { get; init; }
        /// <summary>The unit of <see cref="Number"/> (prefixed, e.g. "kW"); empty for a pure number or a composite SI unit.</summary>
        public string Unit { get; init; } = "";
        /// <summary>The value in <see cref="Unit"/>.</summary>
        public double Number { get; init; }
        /// <summary>Formatted result, e.g. "3.68 kW".</summary>
        public string Text { get; init; } = "";
        /// <summary>Calculation steps: the expression as understood, the SI result, the shown result.</summary>
        public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Built-in unit-aware calculator (Calc 0/1, maintainer request 2026-11-25): "230 V * 16 A" → 3.68 kW,
    /// "12 mm + 0.5 in" → 24.7 mm, "1500 rpm in rad/s" → 157.1 rad/s, "(4k7 Ω || 10 kΩ)" → 3.197 kΩ (parallel),
    /// "2 kWh / 230 V" → 8.696 Ah. Numbers in either language (decimal comma or point), IEC 60062 letter codes, every unit
    /// of <see cref="UnitCatalog"/> and products/quotients of them ("kg·m/s²", "V/A"). Operators + − · × * / ÷ ^ ² ³ and ||
    /// (parallel: 1/(1/a + 1/b)), parentheses, the functions sqrt/√, abs, sin, cos, tan (radians or an angle), ln, lg/log,
    /// exp, the constant π/pi, and a trailing conversion "in/to/as/-&gt;/→ unit". Sums need equal dimensions; a conversion
    /// needs the same dimension (rpm ↔ Hz is refused because of the 2π). Temperatures in °C/°F are absolute: their
    /// difference is a temperature difference in K. A safe recursive-descent parser: nothing but arithmetic is executed,
    /// length and nesting are limited.
    /// </summary>
    public static class UnitCalculator
    {
        public const int MaxLength = 500;
        private const int MaxDepth = 64;

        private sealed class CalcError : Exception
        {
            public CalcError(I18nText text) : base(text.En) { Text = text; }
            public I18nText Text { get; }
        }

        private readonly struct Q
        {
            public Q(double si, Dimension dim, Unit? hint = null, bool affine = false) { Si = si; Dim = dim; Hint = hint; Affine = affine; }
            public double Si { get; }
            public Dimension Dim { get; }
            /// <summary>Unit the value was typed in (kept through sums), for showing the result.</summary>
            public Unit? Hint { get; }
            /// <summary>Absolute temperature typed with an offset unit (°C, °F).</summary>
            public bool Affine { get; }
        }

        /// <summary>Evaluates; throws <see cref="FormatException"/> with the message in <paramref name="lang"/>.</summary>
        public static CalcResult Evaluate(string? text, Lang lang = Lang.En, int digits = 4)
            => TryEvaluate(text, out var r, out var e, lang, digits) ? r! : throw new FormatException(e!.Get(lang));

        public static bool TryEvaluate(string? text, out CalcResult? result, out I18nText? error, Lang lang = Lang.En, int digits = 4)
        {
            result = null; error = null;
            var t = (text ?? "").Trim();
            if (t.Length == 0) { error = new I18nText("Enter an expression, e.g. 230 V * 16 A.", "Bitte einen Ausdruck eingeben, z. B. 230 V * 16 A."); return false; }
            if (t.Length > MaxLength) { error = new I18nText("The expression is too long.", "Der Ausdruck ist zu lang."); return false; }
            try
            {
                var (expr, target) = SplitTarget(t);
                var p = new Parser(expr);
                var q = p.ParseAll();
                if (double.IsNaN(q.Si)) throw new CalcError(new I18nText("The result is not defined (e.g. a root of a negative number).", "Das Ergebnis ist nicht definiert (z. B. Wurzel aus einer negativen Zahl)."));
                if (double.IsInfinity(q.Si)) throw new CalcError(new I18nText("Division by zero.", "Division durch null."));
                result = Present(q, target, lang, digits, expr);
                return true;
            }
            catch (CalcError ex) { error = ex.Text; return false; }
        }

        // ------------------------------------------------------------------ result

        private static CalcResult Present(Q q, string? target, Lang lang, int digits, string expr)
        {
            var steps = new List<string> { Pretty(expr) + (target is null ? "" : " → " + target.Trim()) };
            var siText = ValueFormat.Number(q.Si, lang, Math.Max(digits, 6)) + (q.Dim.IsNone ? "" : " " + q.Dim);
            if (target is not null)
            {
                var (factor, offset, dim, single) = ResolveUnit(target.Trim()) ?? throw new CalcError(Units.UnknownUnit(target.Trim()));
                if (!Compatible(q.Dim, dim)) throw Incompatible(q.Dim, dim, conversion: true);
                var number = (q.Si - (q.Affine || offset == 0 ? offset : 0)) / factor;
                var unitText = single?.Symbol ?? target.Trim();
                var text = ValueFormat.Number(number, lang, digits) + (unitText.Length == 0 ? "" : " " + unitText);
                steps.Add("= " + siText);
                steps.Add("= " + text);
                return new CalcResult { Value = q.Si, Dimension = q.Dim, Kind = single?.Kind ?? Quantities.ForDimension(dim), Unit = unitText, Number = number, Text = text, Steps = steps };
            }

            Unit? unit = null;
            QuantityKind? kind = null;
            if (q.Hint is not null && q.Hint.Dimension == q.Dim && (q.Hint.Offset == 0 || q.Affine))
            {
                unit = q.Hint;
                kind = q.Hint.Kind;
            }
            else if (q.Dim == Quantities.Temperature.Dimension && !q.Affine && q.Hint is not null)
            {
                kind = Quantities.TemperatureDifference;
                unit = kind.DefaultUnit;
            }
            else
            {
                kind = Quantities.ForDimension(q.Dim);
                if (kind == Quantities.Temperature && !q.Affine) kind = Quantities.TemperatureDifference;
                if (kind is not null && kind.Dimension == q.Dim) unit = kind == Quantities.Temperature ? UnitCatalog.Find("K", kind) : kind.DefaultUnit;
                else if (kind is not null && kind.Dimension.WithoutAngle == q.Dim.WithoutAngle) unit = kind.DefaultUnit;
            }
            string shownUnit;
            double shown;
            string resultText;
            if (unit is not null)
            {
                var (n, u) = ValueFormat.Scale(q.Si, unit, digits);
                shown = n; shownUnit = u.Symbol;
                resultText = ValueFormat.Format(q.Si, unit, lang, digits);
            }
            else
            {
                shown = q.Si; shownUnit = q.Dim.ToString();
                resultText = ValueFormat.Number(q.Si, lang, digits) + (shownUnit.Length == 0 ? "" : " " + shownUnit);
            }
            if (siText != resultText) steps.Add("= " + siText);
            steps.Add("= " + resultText);
            return new CalcResult { Value = q.Si, Dimension = q.Dim, Kind = kind, Unit = shownUnit, Number = shown, Text = resultText, Steps = steps };
        }

        /// <summary>Same dimension; the plane angle may be dropped (rad = 1), except between a speed of rotation and a frequency (2π).</summary>
        private static bool Compatible(Dimension a, Dimension b)
        {
            if (a == b) return true;
            if (a.WithoutAngle != b.WithoutAngle) return false;
            var perTime = new Dimension(t: -1);
            return !(a.WithoutAngle == perTime && (a.A != 0 || b.A != 0));
        }

        private static CalcError Incompatible(Dimension a, Dimension b, bool conversion)
        {
            string Name(Dimension d, Lang l) => Quantities.ForDimension(d) is { } k && (k.Dimension == d) ? k.Name.Get(l) : (d.IsNone ? (l == Lang.De ? "Zahl" : "number") : d.ToString());
            return conversion
                ? new CalcError(new I18nText($"Cannot convert a {Name(a, Lang.En).ToLowerInvariant()} into a {Name(b, Lang.En).ToLowerInvariant()}.",
                    $"{Name(a, Lang.De)} lässt sich nicht in {Name(b, Lang.De)} umrechnen."))
                : new CalcError(new I18nText($"Cannot add or compare a {Name(a, Lang.En).ToLowerInvariant()} and a {Name(b, Lang.En).ToLowerInvariant()}.",
                    $"{Name(a, Lang.De)} und {Name(b, Lang.De)} lassen sich nicht addieren oder vergleichen."));
        }

        private static string Pretty(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s.Replace("**", "^").Replace("||", "∥"))
            {
                var c = ch switch { '*' => '·', '×' => '·', _ => ch };
                if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] == ' ') continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>Splits a trailing "in/to/as/-&gt;/→ unit" (the last one whose rest is a unit), else (text, null).</summary>
        private static (string Expr, string? Target) SplitTarget(string t)
        {
            var keys = new[] { " in ", " to ", " as ", " into ", " nach ", "->", "→", "=>" };
            var best = -1;
            var bestLen = 0;
            foreach (var k in keys)
            {
                var i = t.LastIndexOf(k, StringComparison.OrdinalIgnoreCase);
                while (i > 0)
                {
                    var rest = t.Substring(i + k.Length).Trim();
                    if (rest.Length > 0 && ResolveUnit(rest) is not null)
                    {
                        if (i > best) { best = i; bestLen = k.Length; }
                        break;
                    }
                    i = i > 0 ? t.LastIndexOf(k, i - 1, StringComparison.OrdinalIgnoreCase) : -1;
                }
            }
            return best > 0 ? (t.Substring(0, best).Trim(), t.Substring(best + bestLen).Trim()) : (t, null);
        }

        /// <summary>A unit spelling or a product/quotient of units ("kg·m/s²", "V/A", "m3/h"); null if unknown.</summary>
        internal static (double Factor, double Offset, Dimension Dim, Unit? Single)? ResolveUnit(string text)
        {
            var s = text.Trim();
            if (s.Length == 0) return null;
            if (s == "1" ) return (1, 0, Dimension.None, null);
            var single = UnitCatalog.FindAll(s);
            if (single.Count > 0)
            {
                var u = single[0];
                return (u.Factor, u.Offset, u.Dimension, u);
            }
            // Composite: tokens joined by '·' (product) and '/' (each '/' divides the following token).
            var n = UnitCatalog.Norm(s).Replace("(", "").Replace(")", "");
            double factor = 1;
            var dim = Dimension.None;
            var i = 0;
            var sign = 1;
            var any = false;
            while (i < n.Length)
            {
                var j = i;
                while (j < n.Length && n[j] != '·' && n[j] != '/') j++;
                var token = n.Substring(i, j - i);
                if (token.Length == 0) return null;
                if (!(TokenUnit(token) is { } tu)) return null;
                factor = sign > 0 ? factor * tu.F : factor / tu.F;
                dim = sign > 0 ? dim * tu.D : dim / tu.D;
                any = true;
                if (j >= n.Length) break;
                sign = n[j] == '/' ? -1 : 1;
                i = j + 1;
            }
            return any ? (factor, 0, dim, null) : null;
        }

        private static (double F, Dimension D)? TokenUnit(string token)
        {
            var all = UnitCatalog.FindAll(token);
            if (all.Count > 0) return all[0].Offset == 0 ? (all[0].Factor, all[0].Dimension) : null;
            // Trailing exponent: "s2", "s-2", "m3".
            var k = token.Length;
            while (k > 0 && char.IsDigit(token[k - 1])) k--;
            if (k > 0 && token[k - 1] == '-') k--;
            if (k == 0 || k == token.Length) return null;
            if (!int.TryParse(token.Substring(k), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var e) || e == 0 || Math.Abs(e) > 9) return null;
            var b = UnitCatalog.FindAll(token.Substring(0, k));
            if (b.Count == 0 || b[0].Offset != 0) return null;
            return (Math.Pow(b[0].Factor, e), b[0].Dimension.Pow(e));
        }

        // ------------------------------------------------------------------ parser

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            private int _depth;

            public Parser(string s) { _s = s; }

            public Q ParseAll()
            {
                var q = Parallel();
                Skip();
                if (_i < _s.Length) throw Unexpected();
                return q;
            }

            private void Skip() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

            private char Peek(int k = 0) => _i + k < _s.Length ? _s[_i + k] : '\0';

            private CalcError Unexpected() => _i >= _s.Length
                ? new CalcError(new I18nText("The expression ends too early.", "Der Ausdruck endet zu früh."))
                : new CalcError(new I18nText($"Unexpected “{_s[_i]}” at position {_i + 1}.", $"Unerwartetes „{_s[_i]}“ an Position {_i + 1}."));

            private void Enter()
            {
                if (++_depth > MaxDepth) throw new CalcError(new I18nText("The expression is nested too deeply.", "Der Ausdruck ist zu tief verschachtelt."));
            }

            private Q Parallel()
            {
                Enter();
                var a = Sum();
                while (true)
                {
                    Skip();
                    if (Peek() == '|' && Peek(1) == '|') _i += 2;
                    else if (Peek() == '∥') _i++;
                    else break;
                    var b = Sum();
                    if (a.Dim != b.Dim) throw Incompatible(a.Dim, b.Dim, false);
                    a = new Q(1 / (1 / a.Si + 1 / b.Si), a.Dim, a.Hint ?? b.Hint);
                }
                _depth--;
                return a;
            }

            private Q Sum()
            {
                var a = Term();
                while (true)
                {
                    Skip();
                    var c = Peek();
                    if (c != '+' && c != '-' && c != '−') break;
                    _i++;
                    var b = Term();
                    var minus = c != '+';
                    if (a.Dim != b.Dim) throw Incompatible(a.Dim, b.Dim, false);
                    if (a.Affine && b.Affine && !minus)
                        throw new CalcError(new I18nText("Two temperatures cannot be added; write a difference in K.", "Zwei Temperaturen lassen sich nicht addieren; eine Differenz in K schreiben."));
                    var si = minus ? a.Si - b.Si : a.Si + b.Si;
                    a = a.Affine && b.Affine ? new Q(si, a.Dim, UnitCatalog.Find("K", Quantities.TemperatureDifference))
                        : a.Affine || b.Affine ? new Q(si, a.Dim, a.Affine ? a.Hint : b.Hint, true)
                        : new Q(si, a.Dim, a.Hint ?? b.Hint);
                }
                return a;
            }

            private Q Term()
            {
                var a = Unary();
                while (true)
                {
                    Skip();
                    var c = Peek();
                    if (c is not ('*' or '·' or '×' or '/' or '÷' or '⋅')) break;
                    if (c == '*' && Peek(1) == '*') break; // power, handled below
                    _i++;
                    var b = Unary();
                    a = c is '/' or '÷' ? new Q(a.Si / b.Si, a.Dim / b.Dim) : new Q(a.Si * b.Si, a.Dim * b.Dim);
                }
                return a;
            }

            private Q Unary()
            {
                Skip();
                if (Peek() is '-' or '−') { _i++; var q = Unary(); return new Q(-q.Si, q.Dim, q.Hint, q.Affine); }
                if (Peek() == '+') { _i++; return Unary(); }
                return Power();
            }

            private Q Power()
            {
                var b = Primary();
                Skip();
                if (Peek() == '²') { _i++; return Pow(b, 2); }
                if (Peek() == '³') { _i++; return Pow(b, 3); }
                if (Peek() == '^' || Peek() == '*' && Peek(1) == '*')
                {
                    _i += Peek() == '^' ? 1 : 2;
                    var e = Unary();
                    if (!e.Dim.IsNone) throw new CalcError(new I18nText("An exponent must be a pure number.", "Ein Exponent muss eine reine Zahl sein."));
                    return Pow(b, e.Si);
                }
                return b;
            }

            private static Q Pow(Q b, double e)
            {
                if (b.Dim.IsNone) return new Q(Math.Pow(b.Si, e), b.Dim);
                var n = Math.Round(e);
                if (Math.Abs(e - n) < 1e-12)
                {
                    // Keep a typed length unit for areas and volumes: (10 mm)² → mm².
                    var hint = b.Hint is { } h && n is 2 or 3 ? UnitCatalog.FindAll(h.Symbol + (n == 2 ? "²" : "³"), false).FirstOrDefault() : null;
                    return new Q(Math.Pow(b.Si, e), b.Dim.Pow((int)n), hint);
                }
                var inv = Math.Round(1 / e);
                if (Math.Abs(1 / e - inv) < 1e-9 && b.Dim.Root((int)inv) is { } r) return new Q(Math.Pow(b.Si, e), r);
                throw new CalcError(new I18nText("This power of a unit is not defined.", "Diese Potenz einer Einheit ist nicht definiert."));
            }

            private Q Primary()
            {
                Skip();
                var c = Peek();
                if (c == '(')
                {
                    _i++;
                    var q = Parallel();
                    Skip();
                    if (Peek() != ')') throw Unexpected();
                    _i++;
                    return WithUnit(q);
                }
                if (c == '√')
                {
                    _i++;
                    return Pow(Power(), 0.5);
                }
                if (char.IsDigit(c) || (c is '.' or ',') && char.IsDigit(Peek(1))) return WithUnit(NumberLiteral());
                if (char.IsLetter(c) || c is 'π' or '°' or 'µ' or 'Ω' or '%')
                {
                    var start = _i;
                    while (_i < _s.Length && char.IsLetter(_s[_i])) _i++;
                    var name = _s.Substring(start, _i - start);
                    Skip();
                    if (Peek() == '(' && Function(name) is { } f)
                    {
                        _i++;
                        Enter();
                        var arg = Parallel();
                        _depth--;
                        Skip();
                        if (Peek() != ')') throw Unexpected();
                        _i++;
                        return f(arg);
                    }
                    if (name is "pi" or "PI" or "π") return new Q(Math.PI, Dimension.None);
                    // A unit on its own counts once ("kWh in J").
                    _i = start;
                    return WithUnit(new Q(1, Dimension.None), required: true);
                }
                throw Unexpected();
            }

            private static Func<Q, Q>? Function(string name)
            {
                Q Pure(Q a, Func<double, double> f)
                {
                    if (!a.Dim.IsNone) throw new CalcError(new I18nText("This function needs a pure number.", "Diese Funktion braucht eine reine Zahl."));
                    return new Q(f(a.Si), Dimension.None);
                }
                Q Trig(Q a, Func<double, double> f)
                {
                    if (!(a.Dim.IsNone || a.Dim == Quantities.Angle.Dimension)) throw new CalcError(new I18nText("This function needs an angle or a number.", "Diese Funktion braucht einen Winkel oder eine Zahl."));
                    return new Q(f(a.Si), Dimension.None);
                }
                return name.ToLowerInvariant() switch
                {
                    "sqrt" or "wurzel" => a => Pow(a, 0.5),
                    "abs" or "betrag" => a => new Q(Math.Abs(a.Si), a.Dim, a.Hint),
                    "sin" => a => Trig(a, Math.Sin),
                    "cos" => a => Trig(a, Math.Cos),
                    "tan" => a => Trig(a, Math.Tan),
                    "ln" => a => Pure(a, Math.Log),
                    "lg" or "log" => a => Pure(a, Math.Log10),
                    "exp" => a => Pure(a, Math.Exp),
                    _ => null,
                };
            }

            /// <summary>A number with a decimal comma or point, an exponent or an IEC 60062 letter code ("4k7").</summary>
            private Q NumberLiteral()
            {
                var start = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || ((_s[_i] is '.' or ',') && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1])))) _i++;
                var digits = _s.Substring(start, _i - start);
                // Letter code: digits, prefix letter, digits ("4k7", "2u2", "1M5").
                if (_i + 1 < _s.Length && "pnuµμmkKMG".IndexOf(_s[_i]) >= 0 && char.IsDigit(_s[_i + 1]) && digits.All(char.IsDigit))
                {
                    var l = _s[_i];
                    _i++;
                    var fs = _i;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                    var f = l switch { 'u' or 'μ' => 1e-6, 'K' => 1e3, _ => UnitCatalog.PrefixFactors[l.ToString()] };
                    return new Q(double.Parse(digits + "." + _s.Substring(fs, _i - fs), CultureInfo.InvariantCulture) * f, Dimension.None);
                }
                if (_i + 1 < _s.Length && (_s[_i] is 'e' or 'E') && (char.IsDigit(_s[_i + 1]) || (_s[_i + 1] is '+' or '-') && _i + 2 < _s.Length && char.IsDigit(_s[_i + 2])))
                {
                    _i += 2;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }
                var token = _s.Substring(start, _i - start);
                if (!ValueParser.TryNumber(token, out var v)) throw new CalcError(new I18nText($"Not a number: “{token}”.", $"Keine Zahl: „{token}“."));
                return new Q(v, Dimension.None);
            }

            /// <summary>Applies a unit written right after a number or a bracket ("230 V", "(2+3) mm").</summary>
            private Q WithUnit(Q q, bool required = false)
            {
                var save = _i;
                Skip();
                var start = _i;
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if (char.IsLetter(c) || c is '°' or 'º' or '%' or 'µ' or 'Ω' or '′' or '″') { _i++; continue; }
                    // Superscript exponents only after a unit letter ("m²", "s⁻¹"); "(10 mm)²" squares the bracket.
                    if (c is '²' or '³' or '⁻' or '¹' && _i > start) { _i++; continue; }
                    // '/', '·' and '^' continue a unit only between unit characters ("l/min", "N·m", "m^2"); "kWh / 230 V" is a division.
                    if ((c is '/' or '·' or '⋅') && _i > start && _i + 1 < _s.Length && char.IsLetter(_s[_i + 1])) { _i++; continue; }
                    if (c == '^' && _i > start && _i + 1 < _s.Length && (char.IsDigit(_s[_i + 1]) || _s[_i + 1] == '-')) { _i += 2; continue; }
                    if (char.IsDigit(c) && _i > start && char.IsLetter(_s[_i - 1]) && (_i + 1 >= _s.Length || !char.IsLetterOrDigit(_s[_i + 1]))) { _i++; continue; }
                    break;
                }
                var text = _s.Substring(start, _i - start);
                // A function call or nothing: no unit.
                if (text.Length == 0 || (!required && Peek() == '(') || (!required && Function(text) is not null))
                {
                    if (required) throw Unexpected();
                    _i = save;
                    return q;
                }
                // A bare SI prefix after a number multiplies ("10k", "2.5M", "100n"), unless it is a unit itself ("m", "h").
                if (!required && text.Length == 1 && "pnuµμkKMG".IndexOf(text[0]) >= 0 && UnitCatalog.FindAll(text, false).Count == 0)
                {
                    var f = text[0] switch { 'u' or 'μ' => 1e-6, 'K' => 1e3, _ => UnitCatalog.PrefixFactors[text] };
                    return new Q(q.Si * f, q.Dim, q.Hint, q.Affine);
                }
                if (ResolveUnit(text) is not { } u)
                {
                    if (!required && (text == "pi" || text == "π")) { _i = save; return q; }
                    throw new CalcError(Units.UnknownUnit(text));
                }
                var single = u.Single;
                return new Q(q.Si * u.Factor + u.Offset, q.Dim * u.Dim, single ?? q.Hint, u.Offset != 0);
            }
        }
    }
}
