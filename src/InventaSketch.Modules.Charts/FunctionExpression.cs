using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// A function y = f(x) typed as a formula ("x^2 - 2", "2 sin(x) + 1", "1/x", "e^(-x/2)·cos(3x)"), for function graphs.
    /// A small, safe evaluator: numbers (decimal point, or a decimal comma between digits), the variable x, the constants
    /// π/pi and e, + − · × * / ^ ² ³, parentheses, |…| for the absolute value, implicit multiplication ("2x", "3(x+1)",
    /// "x sin(x)") and the functions sin, cos, tan, asin, acos, atan, sinh, cosh, tanh, exp, ln, log/lg (base 10), log2,
    /// sqrt/√, cbrt, abs, sign, floor, ceil, round. A leading "f(x) =" or "y =" is ignored. No names other than these are
    /// accepted, nothing is executed but arithmetic; the text length and nesting depth are limited.
    /// </summary>
    public sealed class FunctionExpression
    {
        public const int MaxLength = 400;
        private const int MaxDepth = 60;

        private readonly Node _root;

        private FunctionExpression(string text, Node root) { Text = text; _root = root; }

        /// <summary>The formula as typed (without a leading "f(x) =").</summary>
        public string Text { get; }

        /// <summary>f(x); NaN outside the domain (ln of a negative number, division by zero …).</summary>
        public double Evaluate(double x)
        {
            var v = _root.Eval(x);
            return double.IsInfinity(v) ? double.NaN : v;
        }

        /// <summary>The formula in school notation: "·" for products, superscript squares and cubes, π.</summary>
        public string Pretty()
        {
            var t = Text.Replace("**", "^").Replace("*", "·").Replace("^2", "²").Replace("^3", "³").Replace("sqrt", "√").Replace("pi", "π");
            var sb = new StringBuilder();
            foreach (var ch in t) if (ch != ' ' || sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(ch);
            return sb.ToString().Replace("-", "−").Trim();
        }

        /// <summary>Parses a formula; throws <see cref="FormatException"/> with a bilingual message (<see cref="CurveSpecFormatException"/>).</summary>
        public static FunctionExpression Parse(string? text)
        {
            if (TryParse(text, out var e, out var error)) return e!;
            throw new CurveSpecFormatException(error!);
        }

        public static bool TryParse(string? text, out FunctionExpression? expression, out I18nText? error)
        {
            expression = null; error = null;
            var t = (text ?? "").Trim();
            var eq = t.IndexOf('=');
            if (eq >= 0) t = t.Substring(eq + 1).Trim();
            if (t.Length == 0) { error = new I18nText("Empty formula.", "Leere Funktionsgleichung."); return false; }
            if (t.Length > MaxLength) { error = new I18nText("The formula is too long.", "Die Funktionsgleichung ist zu lang."); return false; }
            try
            {
                var p = new Parser(t);
                var root = p.ParseAll();
                expression = new FunctionExpression(t, root);
                return true;
            }
            catch (ParseError ex)
            {
                error = ex.Text;
                return false;
            }
        }

        // ------------------------------------------------------------------ tree

        private abstract class Node { public abstract double Eval(double x); }

        private sealed class Num : Node { public double V; public override double Eval(double x) => V; }

        private sealed class Var : Node { public override double Eval(double x) => x; }

        private sealed class Unary : Node
        {
            public Func<double, double> F = null!;
            public Node A = null!;
            public override double Eval(double x) => F(A.Eval(x));
        }

        private sealed class Binary : Node
        {
            public char Op;
            public Node A = null!, B = null!;
            public override double Eval(double x)
            {
                double a = A.Eval(x), b = B.Eval(x);
                switch (Op)
                {
                    case '+': return a + b;
                    case '-': return a - b;
                    case '*': return a * b;
                    case '/': return b == 0 ? double.NaN : a / b;
                    default:
                        // Odd roots of negative numbers: (−8)^(1/3) = −2.
                        if (a < 0 && Math.Abs(b) < 1 && b != 0 && Math.Abs(1 / b - Math.Round(1 / b)) < 1e-9 && ((long)Math.Round(1 / b)) % 2 != 0)
                            return -Math.Pow(-a, b);
                        return Math.Pow(a, b);
                }
            }
        }

        private static readonly Dictionary<string, Func<double, double>> Functions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sin"] = Math.Sin, ["cos"] = Math.Cos, ["tan"] = Math.Tan,
            ["asin"] = Math.Asin, ["acos"] = Math.Acos, ["atan"] = Math.Atan, ["arcsin"] = Math.Asin, ["arccos"] = Math.Acos, ["arctan"] = Math.Atan,
            ["sinh"] = Math.Sinh, ["cosh"] = Math.Cosh, ["tanh"] = Math.Tanh,
            ["exp"] = v => v > 700 ? double.NaN : Math.Exp(v),
            ["ln"] = v => v > 0 ? Math.Log(v) : double.NaN,
            ["log"] = v => v > 0 ? Math.Log10(v) : double.NaN,
            ["lg"] = v => v > 0 ? Math.Log10(v) : double.NaN,
            ["log2"] = v => v > 0 ? Math.Log(v, 2) : double.NaN,
            ["sqrt"] = v => v >= 0 ? Math.Sqrt(v) : double.NaN,
            ["cbrt"] = v => v < 0 ? -Math.Pow(-v, 1.0 / 3) : Math.Pow(v, 1.0 / 3),
            ["abs"] = Math.Abs,
            ["sign"] = v => Math.Sign(v), ["sgn"] = v => Math.Sign(v),
            ["floor"] = Math.Floor, ["ceil"] = Math.Ceiling, ["round"] = v => Math.Round(v, MidpointRounding.AwayFromZero),
        };

        /// <summary>The function names the evaluator knows.</summary>
        public static IReadOnlyCollection<string> FunctionNames => Functions.Keys;

        private sealed class ParseError : Exception
        {
            public ParseError(I18nText text) : base(text.En) { Text = text; }
            public I18nText Text { get; }
        }

        // ------------------------------------------------------------------ parser (recursive descent)

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            private int _depth;
            private bool _inAbs;

            public Parser(string s) { _s = s; }

            private ParseError Error(string en, string de) => new(new I18nText(en + " (“" + _s + "”, position " + (_i + 1).ToString(CultureInfo.InvariantCulture) + ")",
                de + " („" + _s + "“, Stelle " + (_i + 1).ToString(CultureInfo.InvariantCulture) + ")"));

            private void Skip() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

            private char Peek() { Skip(); return _i < _s.Length ? _s[_i] : '\0'; }

            public Node ParseAll()
            {
                var n = Sum();
                if (Peek() != '\0') throw Error("Unexpected “" + _s[_i] + "”", "Unerwartetes „" + _s[_i] + "“");
                return n;
            }

            private Node Enter(Func<Node> f)
            {
                if (++_depth > MaxDepth) throw Error("The formula is nested too deeply", "Die Funktionsgleichung ist zu tief verschachtelt");
                try { return f(); } finally { _depth--; }
            }

            private Node Sum() => Enter(() =>
            {
                var a = Product();
                while (true)
                {
                    var c = Peek();
                    if (c is '+' or '-' or '−' or '–') { _i++; a = new Binary { Op = c == '+' ? '+' : '-', A = a, B = Product() }; }
                    else return a;
                }
            });

            private Node Product() => Enter(() =>
            {
                var a = Signed();
                while (true)
                {
                    var c = Peek();
                    if (c is '*' or '·' or '×' or '⋅' or '/' or ':' or '÷')
                    {
                        _i++;
                        if (c == '*' && Peek() == '*') { _i++; a = new Binary { Op = '^', A = a, B = Signed() }; continue; } // "**" as power
                        a = new Binary { Op = c is '/' or ':' or '÷' ? '/' : '*', A = a, B = Signed() };
                    }
                    else if (StartsPrimary(c)) a = new Binary { Op = '*', A = a, B = Power() }; // implicit: 2x, 3(x+1), x sin(x)
                    else return a;
                }
            });

            private bool StartsPrimary(char c) => c == '(' || c == '√' || c == 'π' || char.IsLetter(c) || char.IsDigit(c) || c == '.' || c == '|' && !_inAbs;

            private Node Signed() => Enter(() =>
            {
                var c = Peek();
                if (c is '-' or '−' or '–') { _i++; return new Unary { F = v => -v, A = Signed() }; }
                if (c == '+') { _i++; return Signed(); }
                return Power();
            });

            private Node Power() => Enter(() =>
            {
                var a = Postfix();
                if (Peek() == '^') { _i++; return new Binary { Op = '^', A = a, B = Signed() }; }
                return a;
            });

            private Node Postfix()
            {
                var a = Primary();
                while (true)
                {
                    var c = Peek();
                    if (c == '²') { _i++; a = new Binary { Op = '^', A = a, B = new Num { V = 2 } }; }
                    else if (c == '³') { _i++; a = new Binary { Op = '^', A = a, B = new Num { V = 3 } }; }
                    else return a;
                }
            }

            private Node Primary()
            {
                var c = Peek();
                if (c == '\0') throw Error("The formula ends too early", "Die Funktionsgleichung endet zu früh");
                if (c == '(' || c == '[')
                {
                    _i++;
                    var close = c == '(' ? ')' : ']';
                    var outer = _inAbs;
                    _inAbs = false;
                    var n = Sum();
                    _inAbs = outer;
                    if (Peek() != close) throw Error("“" + close + "” expected", "„" + close + "“ erwartet");
                    _i++;
                    return n;
                }
                if (c == '|')
                {
                    _i++;
                    var outer = _inAbs;
                    _inAbs = true;
                    var n = Sum();
                    _inAbs = outer;
                    if (Peek() != '|') throw Error("Closing “|” expected", "Schließendes „|“ erwartet");
                    _i++;
                    return new Unary { F = Math.Abs, A = n };
                }
                if (c == '√') { _i++; return new Unary { F = Functions["sqrt"], A = Postfix() }; }
                if (c == 'π') { _i++; return new Num { V = Math.PI }; }
                if (char.IsDigit(c) || c == '.') return Number();
                if (char.IsLetter(c))
                {
                    var start = _i;
                    while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '_')) _i++;
                    var name = _s.Substring(start, _i - start);
                    // A function name, else a run of single letters ("xe" is not split: only x, pi and e are known).
                    if (Functions.TryGetValue(name, out var f))
                    {
                        if (Peek() == '^')
                        {
                            // sin^2(x) = (sin x)^2
                            _i++;
                            var e = Postfix();
                            var arg = Postfix();
                            return new Binary { Op = '^', A = new Unary { F = f, A = arg }, B = e };
                        }
                        return new Unary { F = f, A = Postfix() };
                    }
                    switch (name.ToLowerInvariant())
                    {
                        case "x": return new Var();
                        case "pi": return new Num { V = Math.PI };
                        case "e": return new Num { V = Math.E };
                    }
                    // "2xsin(x)" style runs: x followed by a known name.
                    if (name.Length > 1 && (name[0] == 'x' || name[0] == 'X'))
                    {
                        _i = start + 1;
                        return new Var();
                    }
                    _i = start;
                    throw Error("Unknown name “" + name + "” (known: x, π, e, " + string.Join(", ", Functions.Keys.OrderBy(k => k)) + ")",
                        "Unbekannter Name „" + name + "“ (bekannt: x, π, e, " + string.Join(", ", Functions.Keys.OrderBy(k => k)) + ")");
                }
                throw Error("Unexpected “" + c + "”", "Unerwartetes „" + c + "“");
            }

            private Node Number()
            {
                var start = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.' ||
                    _s[_i] == ',' && _i > start && char.IsDigit(_s[_i - 1]) && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1]))) _i++;
                // Exponent: 1e-3, 2.5E4 (not "2e" = 2·e).
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    var j = _i + 1;
                    if (j < _s.Length && (_s[j] == '+' || _s[j] == '-')) j++;
                    if (j < _s.Length && char.IsDigit(_s[j]))
                    {
                        _i = j;
                        while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                    }
                }
                var text = _s.Substring(start, _i - start).Replace(',', '.');
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsInfinity(v))
                {
                    _i = start;
                    throw Error("Not a number: “" + text + "”", "Keine Zahl: „" + text + "“");
                }
                return new Num { V = v };
            }
        }
    }
}
