using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    // =============================================================================================
    // A safe reader for the literal subset of Python used by plotting scripts (roadmap 4b). The code is
    // READ, NEVER EXECUTED: there is no access to files, modules, attributes of real objects or the
    // runtime. Supported: numbers, strings, lists, tuples, dicts, True/False/None, variables assigned
    // from these, arithmetic, comparisons, simple list comprehensions, bounded for loops, and a small
    // numpy/math subset (linspace, arange, array, logspace, zeros, ones, elementwise sin, cos, exp, log,
    // sqrt …). Everything else raises PyUnsupported with a bilingual reason; the caller reports the
    // statement as not imported. Sizes and loop counts are limited.
    // =============================================================================================

    /// <summary>The value of a "return" statement (ends the reading of a function body).</summary>
    internal sealed class PyReturn : Exception
    {
        public PyReturn(PyVal value) { Value = value; }
        public PyVal Value { get; }
    }

    /// <summary>A construct outside the supported subset (reported as not imported, never guessed).</summary>
    internal sealed class PyUnsupported : Exception
    {
        public PyUnsupported(I18nText reason) : base(reason.En) { Reason = reason; }
        public PyUnsupported(string en, string de) : this(new I18nText(en, de)) { }
        public I18nText Reason { get; }
    }

    internal enum PyTok { Name, Number, String, Op }

    internal sealed class PyToken
    {
        public PyTok Kind;
        public string Text = "";
        public double Number;
        public bool Int;
        public bool Imaginary;
        public string Str = "";
        public bool FString;
        public bool Bytes;
        public int Line;

        public bool Is(string op) => Kind == PyTok.Op && Text == op;
        public bool IsName(string name) => Kind == PyTok.Name && Text == name;
        public override string ToString() => Text;
    }

    /// <summary>One statement (a logical line) with its block body for compound statements.</summary>
    internal sealed class PyStmt
    {
        public int Line;
        public int Indent;
        public List<PyToken> Tokens = new();
        public string Source = "";
        public List<PyStmt> Body = new();
        /// <summary>The compound keyword (def, for, if, while, with, try, class, else, elif, except, finally) or null.</summary>
        public string? Keyword;
        /// <summary>Header tokens without the keyword and the colon (compound statements).</summary>
        public List<PyToken> Header = new();
    }

    /// <summary>Splits Python source into statements (indentation blocks, bracket continuation, ";" separators).</summary>
    internal static class PyLexer
    {
        public const int MaxTokens = 2_000_000;

        private static readonly string[] Ops =
        {
            "**=", "//=", ">>=", "<<=", "...", "**", "//", "==", "!=", "<=", ">=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "->", ":=", "<<", ">>",
            "+", "-", "*", "/", "%", "@", "<", ">", "=", "(", ")", "[", "]", "{", "}", ",", ":", ".", ";", "~", "&", "|", "^", "!",
        };

        private static readonly HashSet<string> Compound = new(StringComparer.Ordinal)
        {
            "def", "for", "if", "elif", "else", "while", "with", "try", "except", "finally", "class", "async",
        };

        /// <summary>The statements of the source as a block tree.</summary>
        public static List<PyStmt> Parse(string source)
        {
            var lines = Logical(source);
            var flat = new List<PyStmt>();
            foreach (var (line, indent, tokens, text) in lines)
            {
                // ";" separates simple statements on one line.
                var parts = new List<List<PyToken>> { new() };
                foreach (var t in tokens)
                {
                    if (t.Is(";")) { parts.Add(new List<PyToken>()); continue; }
                    parts[parts.Count - 1].Add(t);
                }
                foreach (var p in parts.Where(p => p.Count > 0))
                    flat.Add(new PyStmt { Line = line, Indent = indent, Tokens = p, Source = text });
            }
            var pos = 0;
            return Block(flat, ref pos, -1);
        }

        private static List<PyStmt> Block(List<PyStmt> flat, ref int pos, int parentIndent)
        {
            var list = new List<PyStmt>();
            var indent = -1;
            while (pos < flat.Count)
            {
                var s = flat[pos];
                if (s.Indent <= parentIndent) break;
                if (indent < 0) indent = s.Indent;
                if (s.Indent < indent) break;
                pos++;
                var first = s.Tokens[0];
                if (first.Kind == PyTok.Name && Compound.Contains(first.Text))
                {
                    s.Keyword = first.Text;
                    var start = 1;
                    if (first.Text == "async" && s.Tokens.Count > 1) { s.Keyword = s.Tokens[1].Text; start = 2; }
                    var colon = TopLevelColon(s.Tokens, start);
                    if (colon >= 0)
                    {
                        s.Header = s.Tokens.GetRange(start, colon - start);
                        if (colon < s.Tokens.Count - 1)
                        {
                            // Body on the same line: "if x: y = 1".
                            s.Body.Add(new PyStmt { Line = s.Line, Indent = s.Indent + 1, Tokens = s.Tokens.GetRange(colon + 1, s.Tokens.Count - colon - 1), Source = s.Source });
                        }
                        else s.Body = Block(flat, ref pos, s.Indent);
                    }
                    else s.Body = Block(flat, ref pos, s.Indent);
                }
                else if (pos < flat.Count && flat[pos].Indent > s.Indent)
                {
                    // Unexpected indentation (broken file): keep the lines as a nested block of an unknown statement.
                    s.Body = Block(flat, ref pos, s.Indent);
                }
                list.Add(s);
            }
            return list;
        }

        /// <summary>Index of the block colon (":" at bracket depth 0, not part of a lambda or slice).</summary>
        private static int TopLevelColon(List<PyToken> tokens, int start)
        {
            var depth = 0;
            var lambdas = 0;
            for (var i = start; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Kind == PyTok.Op)
                {
                    if (t.Text is "(" or "[" or "{") depth++;
                    else if (t.Text is ")" or "]" or "}") depth--;
                    else if (t.Text == ":" && depth == 0)
                    {
                        if (lambdas > 0) { lambdas--; continue; }
                        return i;
                    }
                }
                else if (t.IsName("lambda") && depth == 0) lambdas++;
            }
            return -1;
        }

        /// <summary>Logical lines: (first physical line, indentation, tokens, source text).</summary>
        private static List<(int Line, int Indent, List<PyToken> Tokens, string Text)> Logical(string src)
        {
            var result = new List<(int, int, List<PyToken>, string)>();
            src = (src ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            var i = 0;
            var line = 1;
            var total = 0;
            while (i < src.Length)
            {
                // Start of a logical line: indentation.
                var indent = 0;
                while (i < src.Length && (src[i] == ' ' || src[i] == '\t' || src[i] == '\f'))
                {
                    indent = src[i] == '\t' ? (indent / 8 + 1) * 8 : indent + 1;
                    i++;
                }
                if (i >= src.Length) break;
                if (src[i] == '\n') { i++; line++; continue; }
                if (src[i] == '#') { while (i < src.Length && src[i] != '\n') i++; continue; }
                var startLine = line;
                var startPos = i;
                var tokens = new List<PyToken>();
                var depth = 0;
                while (i < src.Length)
                {
                    var c = src[i];
                    if (c == '\n')
                    {
                        if (depth > 0) { i++; line++; continue; }
                        break;
                    }
                    if (c == ' ' || c == '\t' || c == '\f') { i++; continue; }
                    if (c == '#') { while (i < src.Length && src[i] != '\n') i++; continue; }
                    if (c == '\\' && i + 1 < src.Length && src[i + 1] == '\n') { i += 2; line++; continue; }
                    if (++total > MaxTokens) throw new PyUnsupported("The script is too long to be read.", "Das Skript ist zu lang zum Einlesen.");
                    // Strings (with prefixes r, b, u, f and combinations).
                    var pre = 0;
                    while (pre < 2 && i + pre < src.Length && "rRbBuUfF".IndexOf(src[i + pre]) >= 0) pre++;
                    if (i + pre < src.Length && (src[i + pre] == '\'' || src[i + pre] == '"') && (pre == 0 || !IsNameChar(i > 0 ? src[i - 1] : ' ')))
                    {
                        var prefix = src.Substring(i, pre).ToLowerInvariant();
                        i += pre;
                        tokens.Add(ReadString(src, ref i, ref line, prefix));
                        continue;
                    }
                    if (char.IsDigit(c) || c == '.' && i + 1 < src.Length && char.IsDigit(src[i + 1]))
                    {
                        tokens.Add(ReadNumber(src, ref i, line));
                        continue;
                    }
                    if (IsNameStart(c))
                    {
                        var s = i;
                        while (i < src.Length && IsNameChar(src[i])) i++;
                        tokens.Add(new PyToken { Kind = PyTok.Name, Text = src.Substring(s, i - s), Line = line });
                        continue;
                    }
                    var op = Ops.FirstOrDefault(o => string.CompareOrdinal(src, i, o, 0, o.Length) == 0);
                    if (op is null)
                    {
                        // Unknown character: keep it as an operator token, the parser reports it.
                        op = c.ToString();
                    }
                    if (op is "(" or "[" or "{") depth++;
                    else if (op is ")" or "]" or "}") depth = Math.Max(0, depth - 1);
                    tokens.Add(new PyToken { Kind = PyTok.Op, Text = op, Line = line });
                    i += op.Length;
                }
                var text = src.Substring(startPos, i - startPos);
                if (tokens.Count > 0) result.Add((startLine, indent, tokens, text));
            }
            return result;
        }

        private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';
        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static PyToken ReadNumber(string src, ref int i, int line)
        {
            var s = i;
            if (src[i] == '0' && i + 1 < src.Length && (src[i + 1] is 'x' or 'X' or 'o' or 'O' or 'b' or 'B'))
            {
                var radix = src[i + 1] is 'x' or 'X' ? 16 : src[i + 1] is 'o' or 'O' ? 8 : 2;
                i += 2;
                var ds = i;
                while (i < src.Length && (Uri.IsHexDigit(src[i]) || src[i] == '_')) i++;
                var digits = src.Substring(ds, i - ds).Replace("_", "");
                double v = 0;
                foreach (var d in digits) v = v * radix + Convert.ToInt32(d.ToString(), 16);
                return new PyToken { Kind = PyTok.Number, Text = src.Substring(s, i - s), Number = v, Int = true, Line = line };
            }
            var isInt = true;
            while (i < src.Length && (char.IsDigit(src[i]) || src[i] == '_')) i++;
            if (i < src.Length && src[i] == '.') { isInt = false; i++; while (i < src.Length && (char.IsDigit(src[i]) || src[i] == '_')) i++; }
            if (i < src.Length && (src[i] == 'e' || src[i] == 'E'))
            {
                var j = i + 1;
                if (j < src.Length && (src[j] == '+' || src[j] == '-')) j++;
                if (j < src.Length && char.IsDigit(src[j]))
                {
                    isInt = false;
                    i = j;
                    while (i < src.Length && char.IsDigit(src[i])) i++;
                }
            }
            var text = src.Substring(s, i - s);
            var imag = false;
            if (i < src.Length && (src[i] == 'j' || src[i] == 'J')) { imag = true; i++; }
            double.TryParse(text.Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            return new PyToken { Kind = PyTok.Number, Text = text, Number = value, Int = isInt, Imaginary = imag, Line = line };
        }

        private static PyToken ReadString(string src, ref int i, ref int line, string prefix)
        {
            var startLine = line;
            var q = src[i];
            var triple = i + 2 < src.Length && src[i + 1] == q && src[i + 2] == q;
            var raw = prefix.Contains('r');
            i += triple ? 3 : 1;
            var sb = new StringBuilder();
            while (i < src.Length)
            {
                var c = src[i];
                if (triple ? (c == q && i + 2 < src.Length && src[i + 1] == q && src[i + 2] == q) : c == q)
                {
                    i += triple ? 3 : 1;
                    break;
                }
                if (c == '\n')
                {
                    if (!triple) break; // unterminated: stop at the line end
                    line++;
                }
                if (c == '\\' && i + 1 < src.Length)
                {
                    var n = src[i + 1];
                    if (raw) { sb.Append(c).Append(n); if (n == '\n') line++; i += 2; continue; }
                    i += 2;
                    switch (n)
                    {
                        case '\n': line++; break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '0': sb.Append('\0'); break;
                        case '\\': sb.Append('\\'); break;
                        case '\'': sb.Append('\''); break;
                        case '"': sb.Append('"'); break;
                        case 'x' when i + 1 < src.Length && Uri.IsHexDigit(src[i]) && Uri.IsHexDigit(src[i + 1]):
                            sb.Append((char)Convert.ToInt32(src.Substring(i, 2), 16)); i += 2; break;
                        case 'u' when i + 3 < src.Length && src.Substring(i, 4).All(Uri.IsHexDigit):
                            sb.Append((char)Convert.ToInt32(src.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append('\\').Append(n); break;
                    }
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return new PyToken { Kind = PyTok.String, Text = "'" + sb + "'", Str = sb.ToString(), FString = prefix.Contains('f'), Bytes = prefix.Contains('b'), Line = startLine };
        }
    }

    // ------------------------------------------------------------------ expressions

    internal abstract class PyExpr { public int Line; }
    internal sealed class PyConstE : PyExpr { public PyVal Value = PyNone.Instance; }
    internal sealed class PyNameE : PyExpr { public string Name = ""; }
    internal sealed class PyAttrE : PyExpr { public PyExpr Obj = null!; public string Name = ""; }
    internal sealed class PyCallE : PyExpr
    {
        public PyExpr Func = null!;
        public List<PyExpr> Args = new();
        public List<(string Name, PyExpr Value)> Kwargs = new();
        public bool Star;
    }
    internal sealed class PyIndexE : PyExpr { public PyExpr Obj = null!; public PyExpr Index = null!; }
    internal sealed class PySliceE : PyExpr { public PyExpr? Lo, Hi, Step; }
    internal sealed class PyUnaryE : PyExpr { public string Op = ""; public PyExpr A = null!; }
    internal sealed class PyBinaryE : PyExpr { public string Op = ""; public PyExpr A = null!, B = null!; }
    internal sealed class PyCompareE : PyExpr { public List<string> Ops = new(); public List<PyExpr> Items = new(); }
    internal sealed class PyBoolE : PyExpr { public string Op = ""; public List<PyExpr> Items = new(); }
    internal sealed class PySeqE : PyExpr { public List<PyExpr> Items = new(); public bool Tuple; }
    internal sealed class PyDictE : PyExpr { public List<(PyExpr Key, PyExpr Value)> Items = new(); }
    internal sealed class PyCompE : PyExpr { public PyExpr Element = null!; public PyExpr Target = null!; public PyExpr Iter = null!; public PyExpr? Cond; public bool Dict; public PyExpr? Value; }
    internal sealed class PyUnsupportedE : PyExpr { public I18nText Reason = new(""); }

    /// <summary>Recursive-descent parser of Python expressions (the full precedence ladder; unsupported forms become <see cref="PyUnsupportedE"/>).</summary>
    internal sealed class PyParser
    {
        private const int MaxDepth = 200;
        private readonly List<PyToken> _t;
        private int _i;
        private int _depth;

        public PyParser(List<PyToken> tokens, int start = 0) { _t = tokens; _i = start; }

        public int Position => _i;
        public bool AtEnd => _i >= _t.Count;
        public PyToken? Peek => _i < _t.Count ? _t[_i] : null;
        private int Line => _i < _t.Count ? _t[_i].Line : _t.Count > 0 ? _t[_t.Count - 1].Line : 0;

        private static PyUnsupported Syntax(string near) => new("Cannot read the code near “" + near + "”.", "Der Code bei „" + near + "“ ist nicht lesbar.");

        private bool Op(string op)
        {
            if (_i < _t.Count && _t[_i].Is(op)) { _i++; return true; }
            return false;
        }

        private bool Kw(string name)
        {
            if (_i < _t.Count && _t[_i].IsName(name)) { _i++; return true; }
            return false;
        }

        private void Expect(string op)
        {
            if (!Op(op)) throw Syntax(_i < _t.Count ? _t[_i].Text : "end of line");
        }

        /// <summary>A whole expression list (tuple when it has commas).</summary>
        public PyExpr ExprList()
        {
            var line = Line;
            var first = Test();
            if (_i >= _t.Count || !_t[_i].Is(",")) return first;
            var items = new List<PyExpr> { first };
            while (Op(","))
            {
                if (_i >= _t.Count || IsListEnd(_t[_i])) break;
                items.Add(Test());
            }
            return new PySeqE { Items = items, Tuple = true, Line = line };
        }

        /// <summary>Target list of a for loop ("a, b" up to "in").</summary>
        public PyExpr TargetList()
        {
            var line = Line;
            var first = BitOr();
            if (_i >= _t.Count || !_t[_i].Is(",")) return first;
            var items = new List<PyExpr> { first };
            while (Op(","))
            {
                if (_i >= _t.Count || _t[_i].IsName("in")) break;
                items.Add(BitOr());
            }
            return new PySeqE { Items = items, Tuple = true, Line = line };
        }

        private static bool IsListEnd(PyToken t) => t.Kind == PyTok.Op && (t.Text is ")" or "]" or "}" or "=" or ":" || t.Text.EndsWith("=", StringComparison.Ordinal) && t.Text.Length == 2 && t.Text != "==" && t.Text != "!=" && t.Text != "<=" && t.Text != ">=");

        public void ExpectEnd()
        {
            if (_i < _t.Count) throw Syntax(_t[_i].Text);
        }

        private T Enter<T>(Func<T> f)
        {
            if (++_depth > MaxDepth) throw new PyUnsupported("The expression is nested too deeply.", "Der Ausdruck ist zu tief verschachtelt.");
            try { return f(); } finally { _depth--; }
        }

        public PyExpr Test() => Enter(() =>
        {
            var line = Line;
            if (Kw("lambda"))
            {
                // Skip the lambda (parameters, colon, body) and report it.
                var depth = 0;
                while (_i < _t.Count)
                {
                    var t = _t[_i];
                    if (t.Kind == PyTok.Op && t.Text is "(" or "[" or "{") depth++;
                    else if (t.Kind == PyTok.Op && t.Text is ")" or "]" or "}") { if (depth == 0) break; depth--; }
                    else if (t.Is(",") && depth == 0) break;
                    _i++;
                }
                return new PyUnsupportedE { Line = line, Reason = new I18nText("lambda functions are not read", "lambda-Funktionen werden nicht gelesen") };
            }
            var a = OrExpr();
            if (Kw("if"))
            {
                OrExpr();
                if (!Kw("else")) throw Syntax("if");
                Test();
                return new PyUnsupportedE { Line = line, Reason = new I18nText("conditional expressions (… if … else …) are not evaluated", "Bedingte Ausdrücke (… if … else …) werden nicht ausgewertet") };
            }
            return a;
        });

        private PyExpr OrExpr()
        {
            var line = Line;
            var a = AndExpr();
            if (_i >= _t.Count || !_t[_i].IsName("or")) return a;
            var e = new PyBoolE { Op = "or", Line = line, Items = { a } };
            while (Kw("or")) e.Items.Add(AndExpr());
            return e;
        }

        private PyExpr AndExpr()
        {
            var line = Line;
            var a = NotExpr();
            if (_i >= _t.Count || !_t[_i].IsName("and")) return a;
            var e = new PyBoolE { Op = "and", Line = line, Items = { a } };
            while (Kw("and")) e.Items.Add(NotExpr());
            return e;
        }

        private PyExpr NotExpr() => Enter(() =>
        {
            var line = Line;
            if (Kw("not")) return new PyUnaryE { Op = "not", A = NotExpr(), Line = line };
            return Comparison();
        });

        private PyExpr Comparison()
        {
            var line = Line;
            var a = BitOr();
            PyCompareE? c = null;
            while (_i < _t.Count)
            {
                var t = _t[_i];
                string? op = null;
                if (t.Kind == PyTok.Op && t.Text is "<" or ">" or "==" or "!=" or "<=" or ">=") { op = t.Text; _i++; }
                else if (t.IsName("in")) { op = "in"; _i++; }
                else if (t.IsName("not") && _i + 1 < _t.Count && _t[_i + 1].IsName("in")) { op = "not in"; _i += 2; }
                else if (t.IsName("is")) { _i++; op = Kw("not") ? "is not" : "is"; }
                if (op is null) break;
                c ??= new PyCompareE { Line = line, Items = { a } };
                c.Ops.Add(op);
                c.Items.Add(BitOr());
            }
            return c ?? a;
        }

        private PyExpr BitOr()
        {
            var line = Line;
            var a = Arith();
            while (_i < _t.Count && _t[_i].Kind == PyTok.Op && _t[_i].Text is "|" or "^" or "&" or "<<" or ">>")
            {
                var op = _t[_i++].Text;
                a = new PyBinaryE { Op = op, A = a, B = Arith(), Line = line };
            }
            return a;
        }

        private PyExpr Arith()
        {
            var line = Line;
            var a = Term();
            while (_i < _t.Count && _t[_i].Kind == PyTok.Op && _t[_i].Text is "+" or "-")
            {
                var op = _t[_i++].Text;
                a = new PyBinaryE { Op = op, A = a, B = Term(), Line = line };
            }
            return a;
        }

        private PyExpr Term()
        {
            var line = Line;
            var a = Factor();
            while (_i < _t.Count && _t[_i].Kind == PyTok.Op && _t[_i].Text is "*" or "/" or "//" or "%" or "@")
            {
                var op = _t[_i++].Text;
                a = new PyBinaryE { Op = op, A = a, B = Factor(), Line = line };
            }
            return a;
        }

        private PyExpr Factor() => Enter(() =>
        {
            var line = Line;
            if (_i < _t.Count && _t[_i].Kind == PyTok.Op && _t[_i].Text is "-" or "+" or "~")
            {
                var op = _t[_i++].Text;
                return new PyUnaryE { Op = op, A = Factor(), Line = line };
            }
            return Power();
        });

        private PyExpr Power()
        {
            var line = Line;
            if (Kw("await")) throw Syntax("await");
            var a = Primary();
            if (Op("**")) return new PyBinaryE { Op = "**", A = a, B = Factor(), Line = line };
            return a;
        }

        private PyExpr Primary()
        {
            var e = Atom();
            while (_i < _t.Count)
            {
                var line = Line;
                if (Op("."))
                {
                    if (_i >= _t.Count || _t[_i].Kind != PyTok.Name) throw Syntax(".");
                    e = new PyAttrE { Obj = e, Name = _t[_i++].Text, Line = line };
                }
                else if (Op("(")) e = Call(e, line);
                else if (Op("[")) e = new PyIndexE { Obj = e, Index = Subscript(), Line = line };
                else break;
            }
            return e;
        }

        private PyExpr Call(PyExpr f, int line)
        {
            var call = new PyCallE { Func = f, Line = line };
            while (!Op(")"))
            {
                if (_i >= _t.Count) throw Syntax("(");
                if (Op("*") || Op("**")) { call.Star = true; Test(); }
                else if (_i + 1 < _t.Count && _t[_i].Kind == PyTok.Name && _t[_i + 1].Is("="))
                {
                    var name = _t[_i].Text;
                    _i += 2;
                    call.Kwargs.Add((name, Test()));
                }
                else
                {
                    var arg = Test();
                    if (_i < _t.Count && _t[_i].IsName("for"))
                    {
                        // Generator argument: f(x for x in y).
                        arg = Comprehension(arg, line, false, null);
                    }
                    call.Args.Add(arg);
                }
                if (!Op(","))
                {
                    Expect(")");
                    break;
                }
            }
            return call;
        }

        private PyExpr Subscript()
        {
            var line = Line;
            var items = new List<PyExpr>();
            var tuple = false;
            while (true)
            {
                items.Add(SliceItem());
                if (Op(",")) { tuple = true; if (_i < _t.Count && _t[_i].Is("]")) break; continue; }
                break;
            }
            Expect("]");
            return tuple ? new PySeqE { Items = items, Tuple = true, Line = line } : items[0];
        }

        private PyExpr SliceItem()
        {
            var line = Line;
            PyExpr? lo = null;
            if (_i < _t.Count && !_t[_i].Is(":")) lo = Test();
            if (!Op(":")) return lo ?? throw Syntax("[");
            var s = new PySliceE { Lo = lo, Line = line };
            if (_i < _t.Count && !_t[_i].Is(":") && !_t[_i].Is("]") && !_t[_i].Is(",")) s.Hi = Test();
            if (Op(":") && _i < _t.Count && !_t[_i].Is("]") && !_t[_i].Is(",")) s.Step = Test();
            return s;
        }

        private PyExpr Comprehension(PyExpr element, int line, bool dict, PyExpr? value)
        {
            if (!Kw("for")) throw Syntax("for");
            var target = TargetList();
            if (!Kw("in")) throw Syntax("in");
            var iter = OrExpr();
            PyExpr? cond = null;
            if (Kw("if")) cond = OrExpr();
            if (_i < _t.Count && (_t[_i].IsName("for") || _t[_i].IsName("if")))
                return new PyUnsupportedE { Line = line, Reason = new I18nText("nested comprehensions are not evaluated", "Verschachtelte Comprehensions werden nicht ausgewertet") };
            return new PyCompE { Element = element, Target = target, Iter = iter, Cond = cond, Dict = dict, Value = value, Line = line };
        }

        private PyExpr Atom()
        {
            if (_i >= _t.Count) throw Syntax("end of line");
            var t = _t[_i];
            var line = t.Line;
            switch (t.Kind)
            {
                case PyTok.Number:
                    _i++;
                    if (t.Imaginary) return new PyUnsupportedE { Line = line, Reason = new I18nText("complex numbers are not read", "Komplexe Zahlen werden nicht gelesen") };
                    return new PyConstE { Value = new PyNum(t.Number, t.Int), Line = line };
                case PyTok.String:
                    {
                        var sb = new StringBuilder();
                        var f = false; var b = false;
                        while (_i < _t.Count && _t[_i].Kind == PyTok.String) { sb.Append(_t[_i].Str); f |= _t[_i].FString; b |= _t[_i].Bytes; _i++; }
                        if (f) return new PyUnsupportedE { Line = line, Reason = new I18nText("f-strings are not evaluated", "f-Strings werden nicht ausgewertet") };
                        if (b) return new PyUnsupportedE { Line = line, Reason = new I18nText("byte strings are not read", "Byte-Strings werden nicht gelesen") };
                        return new PyConstE { Value = new PyStr(sb.ToString()), Line = line };
                    }
                case PyTok.Name:
                    _i++;
                    return t.Text switch
                    {
                        "True" => new PyConstE { Value = PyBool.True, Line = line },
                        "False" => new PyConstE { Value = PyBool.False, Line = line },
                        "None" => new PyConstE { Value = PyNone.Instance, Line = line },
                        _ => new PyNameE { Name = t.Text, Line = line },
                    };
            }
            _i++;
            switch (t.Text)
            {
                case "(":
                    {
                        if (Op(")")) return new PySeqE { Tuple = true, Line = line };
                        var first = Test();
                        if (_i < _t.Count && _t[_i].IsName("for")) { var c = Comprehension(first, line, false, null); Expect(")"); return c; }
                        if (Op(")")) return first;
                        var items = new List<PyExpr> { first };
                        while (Op(","))
                        {
                            if (_i < _t.Count && _t[_i].Is(")")) break;
                            items.Add(Test());
                        }
                        Expect(")");
                        return new PySeqE { Items = items, Tuple = true, Line = line };
                    }
                case "[":
                    {
                        var list = new PySeqE { Line = line };
                        if (Op("]")) return list;
                        var first = Test();
                        if (_i < _t.Count && _t[_i].IsName("for")) { var c = Comprehension(first, line, false, null); Expect("]"); return c; }
                        list.Items.Add(first);
                        while (Op(","))
                        {
                            if (_i < _t.Count && _t[_i].Is("]")) break;
                            list.Items.Add(Test());
                        }
                        Expect("]");
                        return list;
                    }
                case "{":
                    {
                        var dict = new PyDictE { Line = line };
                        if (Op("}")) return dict;
                        if (Op("**")) throw new PyUnsupported("Dictionary unpacking (**) is not read.", "Dictionary-Entpacken (**) wird nicht gelesen.");
                        var key = Test();
                        if (!Op(":"))
                        {
                            // A set: read like a list.
                            var set = new PySeqE { Line = line, Items = { key } };
                            while (Op(",")) { if (_i < _t.Count && _t[_i].Is("}")) break; set.Items.Add(Test()); }
                            Expect("}");
                            return set;
                        }
                        var value = Test();
                        if (_i < _t.Count && _t[_i].IsName("for")) { var c = Comprehension(key, line, true, value); Expect("}"); return c; }
                        dict.Items.Add((key, value));
                        while (Op(","))
                        {
                            if (_i < _t.Count && _t[_i].Is("}")) break;
                            var k = Test();
                            Expect(":");
                            dict.Items.Add((k, Test()));
                        }
                        Expect("}");
                        return dict;
                    }
                case "...":
                    return new PyConstE { Value = PyNone.Instance, Line = line };
            }
            throw Syntax(t.Text);
        }
    }

    // ------------------------------------------------------------------ values

    internal abstract class PyVal
    {
        public abstract string TypeName { get; }
    }

    internal sealed class PyNone : PyVal
    {
        public static readonly PyNone Instance = new();
        public override string TypeName => "None";
    }

    internal sealed class PyBool : PyVal
    {
        public static readonly PyBool True = new(true);
        public static readonly PyBool False = new(false);
        private PyBool(bool v) { V = v; }
        public bool V { get; }
        public static PyBool Of(bool v) => v ? True : False;
        public override string TypeName => "bool";
    }

    internal sealed class PyNum : PyVal
    {
        public PyNum(double v, bool isInt = false) { V = v; Int = isInt && Math.Abs(v) < 9e15 && Math.Floor(v) == v; }
        public double V { get; }
        public bool Int { get; }
        public override string TypeName => Int ? "int" : "float";
    }

    internal sealed class PyStr : PyVal
    {
        public PyStr(string v) { V = v; }
        public string V { get; }
        public override string TypeName => "str";
    }

    /// <summary>A Python list or tuple.</summary>
    internal sealed class PySeq : PyVal
    {
        public PySeq(List<PyVal> items, bool tuple = false) { Items = items; Tuple = tuple; }
        public List<PyVal> Items { get; }
        public bool Tuple { get; }
        public override string TypeName => Tuple ? "tuple" : "list";
    }

    /// <summary>A one-dimensional numpy array of numbers (or booleans).</summary>
    internal sealed class PyArr : PyVal
    {
        public PyArr(double[] v, bool isBool = false) { V = v; Bool = isBool; }
        public double[] V { get; }
        public bool Bool { get; }
        public override string TypeName => "ndarray";
    }

    internal sealed class PyDict : PyVal
    {
        public List<KeyValuePair<PyVal, PyVal>> Items { get; } = new();
        public override string TypeName => "dict";

        public PyVal? Get(string key) => Items.FirstOrDefault(kv => kv.Key is PyStr s && s.V == key).Value;
    }

    /// <summary>A host object (module, figure, axes, artist …) whose attributes and methods the host interprets.</summary>
    internal sealed class PyObj : PyVal
    {
        public PyObj(string kind, object? data = null) { Kind = kind; Data = data; }
        public string Kind { get; }
        public object? Data { get; }
        public override string TypeName => Kind;
    }

    /// <summary>A bound method or function of a host object or module.</summary>
    internal sealed class PyFunc : PyVal
    {
        public PyFunc(PyVal? self, string name) { Self = self; Name = name; }
        public PyVal? Self { get; }
        public string Name { get; }
        public override string TypeName => "function";
    }

    /// <summary>A function defined in the script (only called, never executed beyond the supported subset).</summary>
    internal sealed class PyUserFunc : PyVal
    {
        public PyUserFunc(string name, PyStmt def) { Name = name; Def = def; }
        public string Name { get; }
        public PyStmt Def { get; }
        public override string TypeName => "function";
    }

    /// <summary>Arguments of a call.</summary>
    internal sealed class PyArgs
    {
        public List<PyVal> Pos { get; } = new();
        public List<(string Name, PyVal Value)> Kw { get; } = new();
        public int Line { get; set; }

        public PyVal? this[string name] => Kw.LastOrDefault(k => k.Name == name).Value;
        public bool Has(string name) => Kw.Any(k => k.Name == name);

        /// <summary>Positional argument i or keyword <paramref name="name"/>.</summary>
        public PyVal? Arg(int i, string? name = null) => name is not null && this[name] is { } v ? v : i >= 0 && i < Pos.Count ? Pos[i] : null;

        public PyVal? Any(params string[] names)
        {
            foreach (var n in names) if (this[n] is { } v) return v;
            return null;
        }
    }

    /// <summary>The interpreter of host objects (matplotlib).</summary>
    internal interface IPyHost
    {
        /// <summary>A name not bound in the script (e.g. "plt" without import); null if unknown.</summary>
        PyVal? Global(string name);
        /// <summary>The module bound by an import ("matplotlib.pyplot", "numpy" …); null for a module that is not read.</summary>
        PyVal? Module(string dotted);
        /// <summary>An attribute of a host object (a method is returned as <see cref="PyFunc"/>).</summary>
        PyVal Attr(PyObj obj, string name);
        /// <summary>A call of a host function or method.</summary>
        PyVal Call(PyFunc f, PyArgs args);
        /// <summary>Assignment to an item or attribute of a host object (rcParams['font.size'] = 9).</summary>
        void SetItem(PyVal target, PyVal key, PyVal value);
    }

    // ------------------------------------------------------------------ evaluator

    /// <summary>Evaluates expressions of the subset against a scope; numpy and math are built in, everything else is the host's.</summary>
    internal sealed class PyEval
    {
        public const int MaxArray = 200_000;
        public const int MaxLoop = 10_000;
        private long _work;
        private const long MaxWork = 50_000_000;

        private readonly IPyHost _host;
        public Dictionary<string, PyVal> Globals { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, PyVal>? Locals { get; set; }

        public PyEval(IPyHost host) { _host = host; }

        /// <summary>Functions without parameters whose bodies were read in place (calling them again does nothing).</summary>
        public HashSet<PyStmt> Ran { get; } = new();

        /// <summary>Reads the body of a function defined in the script for a call (set by the runner).</summary>
        public Func<PyUserFunc, PyArgs, PyVal>? Invoke { get; set; }

        public static PyUnsupported No(string en, string de) => new(en, de);

        private void Work(long n)
        {
            _work += n;
            if (_work > MaxWork) throw No("The script needs too many computation steps to be read.", "Das Skript braucht zu viele Rechenschritte zum Einlesen.");
        }

        public void Set(string name, PyVal v)
        {
            if (Locals is not null) Locals[name] = v;
            else Globals[name] = v;
        }

        public PyVal? Lookup(string name)
        {
            if (Locals is not null && Locals.TryGetValue(name, out var l)) return l;
            if (Globals.TryGetValue(name, out var g)) return g;
            return null;
        }

        public PyVal Eval(PyExpr e)
        {
            switch (e)
            {
                case PyConstE c: return c.Value;
                case PyNameE n: return Name(n.Name);
                case PyAttrE a: return Attr(Eval(a.Obj), a.Name);
                case PyCallE c: return CallExpr(c);
                case PyIndexE ix: return Index(Eval(ix.Obj), ix.Index);
                case PyUnaryE u: return Unary(u.Op, Eval(u.A));
                case PyBinaryE b: return Binary(b.Op, Eval(b.A), Eval(b.B));
                case PyCompareE c: return Compare(c);
                case PyBoolE b: return BoolOp(b);
                case PySeqE s:
                    {
                        var items = s.Items.Select(Eval).ToList();
                        Work(items.Count);
                        return new PySeq(items, s.Tuple);
                    }
                case PyDictE d:
                    {
                        var dict = new PyDict();
                        foreach (var (k, v) in d.Items) dict.Items.Add(new KeyValuePair<PyVal, PyVal>(Eval(k), Eval(v)));
                        return dict;
                    }
                case PyCompE c: return Comprehension(c);
                case PySliceE: throw No("A slice is only allowed in an index.", "Ein Slice ist nur in einem Index erlaubt.");
                case PyUnsupportedE u: throw new PyUnsupported(u.Reason);
            }
            throw No("Unknown expression.", "Unbekannter Ausdruck.");
        }

        private PyVal Name(string name)
        {
            if (Lookup(name) is { } v) return v;
            switch (name)
            {
                case "nan": case "NaN": return new PyNum(double.NaN);
                case "inf": return new PyNum(double.PositiveInfinity);
            }
            if (Builtins.Contains(name)) return new PyFunc(null, name);
            if (_host.Global(name) is { } h) return h;
            throw No("The name “" + name + "” is not defined by a value that can be read.", "Der Name „" + name + "“ ist nicht durch einen lesbaren Wert definiert.");
        }

        private static readonly HashSet<string> Builtins = new(StringComparer.Ordinal)
        {
            "float", "int", "len", "range", "list", "tuple", "min", "max", "sum", "abs", "round", "zip", "enumerate", "dict", "str", "reversed", "sorted", "bool",
        };

        // ------------------------------------------------------------------ attributes and calls

        private PyVal Attr(PyVal obj, string name)
        {
            if (obj is PyObj o)
            {
                if (o.Kind == "module:numpy") return NumpyAttr(name);
                if (o.Kind == "module:math") return MathAttr(name);
                return _host.Attr(o, name);
            }
            if (obj is PyArr arr)
            {
                switch (name)
                {
                    case "size": return new PyNum(arr.V.Length, true);
                    case "shape": return new PySeq(new List<PyVal> { new PyNum(arr.V.Length, true) }, true);
                    case "T": return arr;
                    case "min": case "max": case "sum": case "mean": case "tolist": case "copy": case "flatten": case "ravel": case "astype": case "cumsum":
                        return new PyFunc(arr, name);
                }
            }
            if (obj is PySeq seq && name is "append" or "extend" or "index" or "count" or "copy" or "flat" or "flatten" or "ravel")
            {
                if (name == "flat") return Flatten(seq);
                return new PyFunc(seq, name);
            }
            if (obj is PyDict dict && name is "get" or "update" or "items" or "keys" or "values") return new PyFunc(dict, name);
            if (obj is PyStr && name is "replace" or "format" or "upper" or "lower" or "strip" or "join" or "split") return new PyFunc(obj, name);
            throw No("The attribute “" + name + "” of a " + obj.TypeName + " is not read.", "Das Attribut „" + name + "“ eines Werts vom Typ " + obj.TypeName + " wird nicht gelesen.");
        }

        private static PySeq Flatten(PySeq seq)
        {
            var list = new List<PyVal>();
            void Add(PyVal v) { if (v is PySeq s) foreach (var x in s.Items) Add(x); else list.Add(v); }
            Add(seq);
            return new PySeq(list);
        }

        private PyVal CallExpr(PyCallE c)
        {
            if (c.Star) throw No("Calls with *args or **kwargs are not read.", "Aufrufe mit *args oder **kwargs werden nicht gelesen.");
            var f = Eval(c.Func);
            var args = new PyArgs { Line = c.Line };
            foreach (var a in c.Args) args.Pos.Add(Eval(a));
            foreach (var (n, v) in c.Kwargs) args.Kw.Add((n, Eval(v)));
            return Call(f, args);
        }

        public PyVal Call(PyVal f, PyArgs args)
        {
            switch (f)
            {
                case PyFunc fn when fn.Self is null && Builtins.Contains(fn.Name): return Builtin(fn.Name, args);
                case PyFunc fn when fn.Self is PyObj { Kind: "module:numpy" }: return Numpy(fn.Name, args);
                case PyFunc fn when fn.Self is PyObj { Kind: "module:math" }: return MathCall(fn.Name, args);
                case PyFunc fn when fn.Self is PyArr arr: return ArrayMethod(arr, fn.Name, args);
                case PyFunc fn when fn.Self is PySeq seq: return SeqMethod(seq, fn.Name, args);
                case PyFunc fn when fn.Self is PyDict dict: return DictMethod(dict, fn.Name, args);
                case PyFunc fn when fn.Self is PyStr str: return StrMethod(str, fn.Name, args);
                case PyFunc fn: return _host.Call(fn, args);
                case PyUserFunc uf when Ran.Contains(uf.Def):
                    return PyNone.Instance;
                case PyUserFunc uf when Invoke is not null:
                    return Invoke(uf, args);
                case PyUserFunc uf:
                    throw No("The function “" + uf.Name + "” defined in the script is not called (only the plotting calls in it are read where possible).",
                        "Die im Skript definierte Funktion „" + uf.Name + "“ wird nicht aufgerufen (nur ihre Zeichenbefehle werden, wo möglich, gelesen).");
            }
            throw No("A " + f.TypeName + " cannot be called.", "Ein Wert vom Typ " + f.TypeName + " kann nicht aufgerufen werden.");
        }

        // ------------------------------------------------------------------ conversions

        public static double Num(PyVal v, string what = "value")
        {
            switch (v)
            {
                case PyNum n: return n.V;
                case PyBool b: return b.V ? 1 : 0;
            }
            throw No("A number was expected for " + what + ", not a " + v.TypeName + ".", "Für " + what + " wurde eine Zahl erwartet, kein Wert vom Typ " + v.TypeName + ".");
        }

        public static bool IsNumber(PyVal v) => v is PyNum or PyBool;

        public static string Str(PyVal v, string what = "text")
        {
            switch (v)
            {
                case PyStr s: return s.V;
                case PyNum n: return n.Int ? ((long)n.V).ToString(CultureInfo.InvariantCulture) : n.V.ToString("R", CultureInfo.InvariantCulture);
            }
            throw No("A text was expected for " + what + ", not a " + v.TypeName + ".", "Für " + what + " wurde ein Text erwartet, kein Wert vom Typ " + v.TypeName + ".");
        }

        /// <summary>A number list from a number, list, tuple or array (nested lists are not flattened).</summary>
        public static double[] Numbers(PyVal v, string what = "data")
        {
            switch (v)
            {
                case PyArr a: return a.V;
                case PySeq s:
                    {
                        var r = new double[s.Items.Count];
                        for (var i = 0; i < r.Length; i++)
                        {
                            var it = s.Items[i];
                            r[i] = it switch
                            {
                                PyNum n => n.V,
                                PyBool b => b.V ? 1 : 0,
                                PyNone => double.NaN,
                                _ => throw No("The " + what + " contain a " + it.TypeName + " instead of a number.", "Die " + what + " enthalten einen Wert vom Typ " + it.TypeName + " statt einer Zahl."),
                            };
                        }
                        return r;
                    }
                case PyNum n: return new[] { n.V };
                case PyBool b: return new[] { b.V ? 1.0 : 0 };
            }
            throw No("Numbers were expected for " + what + ", not a " + v.TypeName + ".", "Für " + what + " wurden Zahlen erwartet, kein Wert vom Typ " + v.TypeName + ".");
        }

        public static bool Truth(PyVal v) => v switch
        {
            PyBool b => b.V,
            PyNum n => n.V != 0,
            PyNone => false,
            PyStr s => s.V.Length > 0,
            PySeq s => s.Items.Count > 0,
            PyDict d => d.Items.Count > 0,
            PyArr a when a.V.Length == 1 => a.V[0] != 0,
            PyArr => throw No("The truth value of an array is ambiguous.", "Der Wahrheitswert eines Arrays ist mehrdeutig."),
            _ => true,
        };

        public IEnumerable<PyVal> Iterate(PyVal v)
        {
            switch (v)
            {
                case PySeq s: return s.Items;
                case PyArr a: return a.V.Select(x => (PyVal)new PyNum(x));
                case PyStr s: return s.V.Select(c => (PyVal)new PyStr(c.ToString()));
                case PyDict d: return d.Items.Select(kv => kv.Key);
            }
            throw No("A " + v.TypeName + " cannot be iterated.", "Über einen Wert vom Typ " + v.TypeName + " kann nicht iteriert werden.");
        }

        private static PyArr Arr(IEnumerable<double> v, bool isBool = false)
        {
            var a = v.ToArray();
            if (a.Length > MaxArray) throw No("The array is too large to be read.", "Das Array ist zu groß zum Einlesen.");
            return new PyArr(a, isBool);
        }

        // ------------------------------------------------------------------ indexing

        private PyVal Index(PyVal obj, PyExpr ix)
        {
            if (ix is PySliceE sl)
            {
                int? I(PyExpr? e) => e is null ? null : (int)Math.Round(Num(Eval(e), "the slice"));
                var (lo, hi, step) = (I(sl.Lo), I(sl.Hi), I(sl.Step));
                switch (obj)
                {
                    case PyArr a: return Arr(Slice(a.V.Length, lo, hi, step).Select(i => a.V[i]), a.Bool);
                    case PySeq s: return new PySeq(Slice(s.Items.Count, lo, hi, step).Select(i => s.Items[i]).ToList(), s.Tuple);
                    case PyStr str: return new PyStr(new string(Slice(str.V.Length, lo, hi, step).Select(i => str.V[i]).ToArray()));
                }
                throw No("A " + obj.TypeName + " cannot be sliced.", "Ein Wert vom Typ " + obj.TypeName + " kann nicht geteilt werden.");
            }
            if (ix is PySeqE { Tuple: true } tup)
            {
                // axs[0, 1] on a nested list of axes; arr[:, 0] is not read.
                var cur = obj;
                foreach (var part in tup.Items) cur = Index(cur, part);
                return cur;
            }
            var key = Eval(ix);
            switch (obj)
            {
                case PyArr a: return new PyNum(a.V[Pos(Num(key, "the index"), a.V.Length)]);
                case PySeq s: return s.Items[Pos(Num(key, "the index"), s.Items.Count)];
                case PyStr str: return new PyStr(str.V[Pos(Num(key, "the index"), str.V.Length)].ToString());
                case PyDict d:
                    {
                        foreach (var kv in d.Items) if (Same(kv.Key, key)) return kv.Value;
                        throw No("The key is not in the dictionary.", "Der Schlüssel steht nicht im Dictionary.");
                    }
                case PyObj o: return _host.Attr(o, "[" + (key is PyStr ks ? ks.V : Str(key)) + "]");
            }
            throw No("A " + obj.TypeName + " cannot be indexed.", "Ein Wert vom Typ " + obj.TypeName + " kann nicht indiziert werden.");
        }

        private static int Pos(double index, int count)
        {
            var i = (int)index;
            if (i < 0) i += count;
            if (i < 0 || i >= count) throw No("The index is out of range.", "Der Index liegt außerhalb des Bereichs.");
            return i;
        }

        private static IEnumerable<int> Slice(int n, int? lo, int? hi, int? step)
        {
            var st = step ?? 1;
            if (st == 0) throw No("A slice step of 0 is not allowed.", "Eine Slice-Schrittweite von 0 ist nicht erlaubt.");
            int Norm(int v, int def) { if (v < 0) v += n; return Math.Max(st > 0 ? 0 : -1, Math.Min(st > 0 ? n : n - 1, v)); }
            var a = lo is { } l ? Norm(l, 0) : st > 0 ? 0 : n - 1;
            var b = hi is { } h ? Norm(h, n) : st > 0 ? n : -1;
            if (st > 0) for (var i = a; i < b; i += st) yield return i;
            else for (var i = a; i > b; i += st) yield return i;
        }

        public static bool Same(PyVal a, PyVal b) => (a, b) switch
        {
            (PyStr x, PyStr y) => x.V == y.V,
            (PyNum x, PyNum y) => x.V == y.V,
            (PyBool x, PyBool y) => x.V == y.V,
            (PyNone, PyNone) => true,
            _ => ReferenceEquals(a, b),
        };

        // ------------------------------------------------------------------ operators

        private PyVal Unary(string op, PyVal a)
        {
            switch (op)
            {
                case "not": return PyBool.Of(!Truth(a));
                case "-":
                    if (a is PyArr arr) { Work(arr.V.Length); return Arr(arr.V.Select(v => -v)); }
                    if (a is PyNum n) return new PyNum(-n.V, n.Int);
                    if (a is PyBool b) return new PyNum(b.V ? -1 : 0, true);
                    break;
                case "+":
                    if (a is PyArr or PyNum) return a;
                    break;
            }
            throw No("The operator “" + op + "” is not read for a " + a.TypeName + ".", "Der Operator „" + op + "“ wird für einen Wert vom Typ " + a.TypeName + " nicht gelesen.");
        }

        private static double Op(string op, double a, double b) => op switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" => b == 0 ? (a == 0 || double.IsNaN(a) ? double.NaN : a > 0 ? double.PositiveInfinity : double.NegativeInfinity) : a / b,
            "//" => b == 0 ? double.NaN : Math.Floor(a / b),
            "%" => b == 0 ? double.NaN : a - b * Math.Floor(a / b),
            "**" => Math.Pow(a, b),
            _ => throw No("The operator “" + op + "” is not read.", "Der Operator „" + op + "“ wird nicht gelesen."),
        };

        public PyVal Binary(string op, PyVal a, PyVal b)
        {
            if (op is "|" or "&" or "^" or "<<" or ">>" or "@")
            {
                if (op is "&" or "|" && a is PyArr { Bool: true } ba && b is PyArr { Bool: true } bb && ba.V.Length == bb.V.Length)
                    return Arr(ba.V.Zip(bb.V, (x, y) => op == "&" ? (x != 0 && y != 0 ? 1.0 : 0) : (x != 0 || y != 0 ? 1.0 : 0)), true);
                throw No("The operator “" + op + "” is not read.", "Der Operator „" + op + "“ wird nicht gelesen.");
            }
            // Strings and lists.
            if (op == "+" && a is PyStr sa && b is PyStr sb) return new PyStr(sa.V + sb.V);
            if (op == "%" && a is PyStr) throw No("String formatting with % is not evaluated.", "Textformatierung mit % wird nicht ausgewertet.");
            if (op == "*" && (a is PyStr || b is PyStr))
            {
                var (s, n) = a is PyStr x ? (x.V, b) : (((PyStr)b).V, a);
                var k = (int)Num(n);
                if (k * (long)s.Length > MaxArray) throw No("The text is too long.", "Der Text ist zu lang.");
                return new PyStr(string.Concat(Enumerable.Repeat(s, Math.Max(0, k))));
            }
            if (a is PySeq la && b is PySeq lb && op == "+") { Work(la.Items.Count + lb.Items.Count); return new PySeq(la.Items.Concat(lb.Items).ToList(), la.Tuple && lb.Tuple); }
            if (op == "*" && (a is PySeq && IsNumber(b) || b is PySeq && IsNumber(a)))
            {
                var (s, n) = a is PySeq x ? (x, b) : ((PySeq)b, a);
                var k = Math.Max(0, (int)Num(n));
                if ((long)k * s.Items.Count > MaxArray) throw No("The list is too large to be read.", "Die Liste ist zu groß zum Einlesen.");
                Work((long)k * s.Items.Count);
                return new PySeq(Enumerable.Range(0, k).SelectMany(_ => s.Items).ToList(), s.Tuple);
            }
            // Arrays: elementwise (a list next to an array counts as an array, as in numpy).
            if (a is PyArr || b is PyArr)
            {
                var x = a is PySeq ? Numbers(a) : a is PyArr ax ? ax.V : null;
                var y = b is PySeq ? Numbers(b) : b is PyArr bx ? bx.V : null;
                if (x is not null && y is not null)
                {
                    if (x.Length != y.Length && x.Length != 1 && y.Length != 1)
                        throw No("Arrays of different lengths (" + x.Length + ", " + y.Length + ") cannot be combined.", "Arrays verschiedener Länge (" + x.Length + ", " + y.Length + ") können nicht verknüpft werden.");
                    var n = Math.Max(x.Length, y.Length);
                    Work(n);
                    return Arr(Enumerable.Range(0, n).Select(i => Op(op, x[x.Length == 1 ? 0 : i], y[y.Length == 1 ? 0 : i])));
                }
                if (x is not null && IsNumber(b)) { var v = Num(b); Work(x.Length); return Arr(x.Select(t => Op(op, t, v))); }
                if (y is not null && IsNumber(a)) { var v = Num(a); Work(y.Length); return Arr(y.Select(t => Op(op, v, t))); }
            }
            if (IsNumber(a) && IsNumber(b))
            {
                var r = Op(op, Num(a), Num(b));
                var ints = (a is PyNum { Int: true } || a is PyBool) && (b is PyNum { Int: true } || b is PyBool) && op is "+" or "-" or "*" or "//" or "%" || op == "**" && a is PyNum { Int: true } && b is PyNum { Int: true } && Num(b) >= 0;
                return new PyNum(r, ints);
            }
            throw No("The operator “" + op + "” is not read for " + a.TypeName + " and " + b.TypeName + ".",
                "Der Operator „" + op + "“ wird für " + a.TypeName + " und " + b.TypeName + " nicht gelesen.");
        }

        private PyVal Compare(PyCompareE c)
        {
            PyVal? result = null;
            var left = Eval(c.Items[0]);
            for (var k = 0; k < c.Ops.Count; k++)
            {
                var right = Eval(c.Items[k + 1]);
                var r = Compare(c.Ops[k], left, right);
                if (r is PyArr) { if (c.Ops.Count > 1) throw No("Chained comparisons of arrays are not read.", "Verkettete Vergleiche von Arrays werden nicht gelesen."); return r; }
                if (!Truth(r)) return PyBool.False;
                result = r;
                left = right;
            }
            return result ?? PyBool.True;
        }

        private static bool Cmp(string op, double a, double b) => op switch
        {
            "<" => a < b, ">" => a > b, "<=" => a <= b, ">=" => a >= b, "==" => a == b, "!=" => a != b, _ => false,
        };

        private PyVal Compare(string op, PyVal a, PyVal b)
        {
            switch (op)
            {
                case "in": case "not in":
                    {
                        var found = b switch
                        {
                            PyStr s when a is PyStr sub => s.V.Contains(sub.V),
                            _ => Iterate(b).Any(x => Same(x, a)),
                        };
                        return PyBool.Of(op == "in" ? found : !found);
                    }
                case "is": return PyBool.Of(Same(a, b) && (a is PyNone || ReferenceEquals(a, b) || a is PyBool));
                case "is not": return PyBool.Of(!(Same(a, b) && (a is PyNone || ReferenceEquals(a, b) || a is PyBool)));
            }
            if (a is PyArr || b is PyArr)
            {
                var x = a is PyArr ax ? ax.V : a is PySeq ? Numbers(a) : null;
                var y = b is PyArr bx ? bx.V : b is PySeq ? Numbers(b) : null;
                if (x is not null && y is not null && x.Length == y.Length) return Arr(x.Zip(y, (p, q) => Cmp(op, p, q) ? 1.0 : 0), true);
                if (x is not null && IsNumber(b)) { var v = Num(b); return Arr(x.Select(p => Cmp(op, p, v) ? 1.0 : 0), true); }
                if (y is not null && IsNumber(a)) { var v = Num(a); return Arr(y.Select(q => Cmp(op, v, q) ? 1.0 : 0), true); }
            }
            if (IsNumber(a) && IsNumber(b)) return PyBool.Of(Cmp(op, Num(a), Num(b)));
            if (a is PyStr sa && b is PyStr sb)
            {
                var c = string.CompareOrdinal(sa.V, sb.V);
                return PyBool.Of(op switch { "==" => c == 0, "!=" => c != 0, "<" => c < 0, ">" => c > 0, "<=" => c <= 0, _ => c >= 0 });
            }
            if (op == "==") return PyBool.Of(Same(a, b));
            if (op == "!=") return PyBool.Of(!Same(a, b));
            throw No("The comparison “" + op + "” is not read for " + a.TypeName + " and " + b.TypeName + ".", "Der Vergleich „" + op + "“ wird für " + a.TypeName + " und " + b.TypeName + " nicht gelesen.");
        }

        private PyVal BoolOp(PyBoolE b)
        {
            PyVal last = PyNone.Instance;
            foreach (var item in b.Items)
            {
                last = Eval(item);
                var t = Truth(last);
                if (b.Op == "and" && !t) return last;
                if (b.Op == "or" && t) return last;
            }
            return last;
        }

        // ------------------------------------------------------------------ comprehensions and binding

        private PyVal Comprehension(PyCompE c)
        {
            var iter = Iterate(Eval(c.Iter)).ToList();
            if (iter.Count > MaxArray) throw No("The comprehension is too large.", "Die Comprehension ist zu groß.");
            Work(iter.Count);
            // The comprehension variables live in their own scope (copied locals), as in Python 3.
            var saved = Locals;
            Locals = saved is null ? new Dictionary<string, PyVal>(StringComparer.Ordinal) : new Dictionary<string, PyVal>(saved, StringComparer.Ordinal);
            try
            {
                var items = new List<PyVal>();
                var dict = new PyDict();
                foreach (var v in iter)
                {
                    Bind(c.Target, v);
                    if (c.Cond is not null && !Truth(Eval(c.Cond))) continue;
                    if (c.Dict) dict.Items.Add(new KeyValuePair<PyVal, PyVal>(Eval(c.Element), Eval(c.Value!)));
                    else items.Add(Eval(c.Element));
                }
                return c.Dict ? dict : new PySeq(items);
            }
            finally { Locals = saved; }
        }

        /// <summary>Binds a value to an assignment target (name, tuple/list of targets, item of a host object).</summary>
        public void Bind(PyExpr target, PyVal v)
        {
            switch (target)
            {
                case PyNameE n:
                    Set(n.Name, v);
                    return;
                case PySeqE s:
                    {
                        var items = Iterate(v).ToList();
                        if (items.Count != s.Items.Count)
                            throw No("Cannot unpack " + items.Count + " values into " + s.Items.Count + " names.", items.Count + " Werte können nicht auf " + s.Items.Count + " Namen verteilt werden.");
                        for (var i = 0; i < items.Count; i++) Bind(s.Items[i], items[i]);
                        return;
                    }
                case PyIndexE ix:
                    {
                        var obj = Eval(ix.Obj);
                        if (ix.Index is PySliceE) throw No("Assignment to a slice is not read.", "Zuweisung an einen Slice wird nicht gelesen.");
                        var key = Eval(ix.Index);
                        switch (obj)
                        {
                            case PyDict d:
                                d.Items.RemoveAll(kv => Same(kv.Key, key));
                                d.Items.Add(new KeyValuePair<PyVal, PyVal>(key, v));
                                return;
                            case PySeq seq when !seq.Tuple:
                                seq.Items[Pos(Num(key), seq.Items.Count)] = v;
                                return;
                            case PyArr arr:
                                arr.V[Pos(Num(key), arr.V.Length)] = Num(v);
                                return;
                            case PyObj:
                                _host.SetItem(obj, key, v);
                                return;
                        }
                        break;
                    }
                case PyAttrE a:
                    _host.SetItem(Eval(a.Obj), new PyStr("." + a.Name), v);
                    return;
            }
            throw No("This assignment target is not read.", "Dieses Zuweisungsziel wird nicht gelesen.");
        }

        // ------------------------------------------------------------------ builtins

        private PyVal Builtin(string name, PyArgs a)
        {
            PyVal A(int i) => i < a.Pos.Count ? a.Pos[i] : throw No(name + "() needs more arguments.", name + "() braucht mehr Argumente.");
            switch (name)
            {
                case "float":
                    {
                        if (a.Pos.Count == 0) return new PyNum(0);
                        var v = A(0);
                        if (v is PyStr s)
                        {
                            var t = s.V.Trim().ToLowerInvariant();
                            if (t is "nan" or "+nan" or "-nan") return new PyNum(double.NaN);
                            if (t is "inf" or "+inf" or "infinity") return new PyNum(double.PositiveInfinity);
                            if (t is "-inf" or "-infinity") return new PyNum(double.NegativeInfinity);
                            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return new PyNum(d);
                            throw No("float() of this text is not a number.", "float() dieses Texts ist keine Zahl.");
                        }
                        return new PyNum(Num(v));
                    }
                case "int":
                    {
                        if (a.Pos.Count == 0) return new PyNum(0, true);
                        var v = A(0);
                        if (v is PyStr s && long.TryParse(s.V.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return new PyNum(l, true);
                        return new PyNum(Math.Truncate(Num(v)), true);
                    }
                case "bool": return PyBool.Of(a.Pos.Count > 0 && Truth(A(0)));
                case "str": return new PyStr(Str(A(0)));
                case "abs":
                    {
                        var v = A(0);
                        if (v is PyArr arr) return Arr(arr.V.Select(Math.Abs));
                        return new PyNum(Math.Abs(Num(v)), v is PyNum { Int: true });
                    }
                case "round":
                    {
                        var d = a.Pos.Count > 1 ? (int)Num(A(1)) : 0;
                        var r = Math.Round(Num(A(0)), Math.Max(0, Math.Min(15, d)), MidpointRounding.ToEven);
                        return new PyNum(r, a.Pos.Count < 2);
                    }
                case "len":
                    return A(0) switch
                    {
                        PySeq s => new PyNum(s.Items.Count, true),
                        PyArr r => new PyNum(r.V.Length, true),
                        PyStr s => new PyNum(s.V.Length, true),
                        PyDict d => new PyNum(d.Items.Count, true),
                        var v => throw No("len() of a " + v.TypeName + " is not read.", "len() eines Werts vom Typ " + v.TypeName + " wird nicht gelesen."),
                    };
                case "range":
                    {
                        double start = 0, stop, step = 1;
                        if (a.Pos.Count == 1) stop = Num(A(0));
                        else { start = Num(A(0)); stop = Num(A(1)); if (a.Pos.Count > 2) step = Num(A(2)); }
                        if (step == 0) throw No("range() with step 0.", "range() mit Schrittweite 0.");
                        var n = Math.Max(0, (long)Math.Ceiling((stop - start) / step));
                        if (n > MaxArray) throw No("The range is too large.", "Der Bereich ist zu groß.");
                        Work(n);
                        return new PySeq(Enumerable.Range(0, (int)n).Select(i => (PyVal)new PyNum(start + i * step, true)).ToList());
                    }
                case "list": return new PySeq(a.Pos.Count == 0 ? new List<PyVal>() : Iterate(A(0)).ToList());
                case "tuple": return new PySeq(a.Pos.Count == 0 ? new List<PyVal>() : Iterate(A(0)).ToList(), true);
                case "reversed": return new PySeq(Iterate(A(0)).Reverse().ToList());
                case "sorted":
                    {
                        var items = Iterate(A(0)).ToList();
                        if (items.All(IsNumber)) items = items.OrderBy(x => Num(x)).ToList();
                        else if (items.All(x => x is PyStr)) items = items.OrderBy(x => ((PyStr)x).V, StringComparer.Ordinal).ToList();
                        else throw No("sorted() of mixed values is not read.", "sorted() gemischter Werte wird nicht gelesen.");
                        return new PySeq(items);
                    }
                case "min": case "max": case "sum":
                    {
                        var vals = (a.Pos.Count == 1 ? Iterate(A(0)) : a.Pos).Select(v => Num(v)).ToList();
                        if (vals.Count == 0) throw No(name + "() of an empty list.", name + "() einer leeren Liste.");
                        return new PyNum(name == "min" ? vals.Min() : name == "max" ? vals.Max() : vals.Sum());
                    }
                case "zip":
                    {
                        var lists = a.Pos.Select(p => Iterate(p).ToList()).ToList();
                        var n = lists.Count == 0 ? 0 : lists.Min(l => l.Count);
                        Work(n);
                        return new PySeq(Enumerable.Range(0, n).Select(i => (PyVal)new PySeq(lists.Select(l => l[i]).ToList(), true)).ToList());
                    }
                case "enumerate":
                    {
                        var start = a.Pos.Count > 1 ? (int)Num(A(1)) : 0;
                        var items = Iterate(A(0)).ToList();
                        return new PySeq(items.Select((v, i) => (PyVal)new PySeq(new List<PyVal> { new PyNum(start + i, true), v }, true)).ToList());
                    }
                case "dict":
                    {
                        var d = new PyDict();
                        foreach (var (k, v) in a.Kw) d.Items.Add(new KeyValuePair<PyVal, PyVal>(new PyStr(k), v));
                        return d;
                    }
            }
            throw No(name + "() is not read.", name + "() wird nicht gelesen.");
        }

        private PyVal SeqMethod(PySeq s, string name, PyArgs a)
        {
            switch (name)
            {
                case "append" when !s.Tuple && a.Pos.Count == 1: s.Items.Add(a.Pos[0]); Work(1); return PyNone.Instance;
                case "extend" when !s.Tuple && a.Pos.Count == 1: s.Items.AddRange(Iterate(a.Pos[0])); return PyNone.Instance;
                case "copy": return new PySeq(s.Items.ToList(), s.Tuple);
                case "flatten": case "ravel": return Flatten(s);
                case "count" when a.Pos.Count == 1: return new PyNum(s.Items.Count(x => Same(x, a.Pos[0])), true);
                case "index" when a.Pos.Count == 1: return new PyNum(s.Items.FindIndex(x => Same(x, a.Pos[0])), true);
            }
            throw No("list." + name + "() is not read.", "list." + name + "() wird nicht gelesen.");
        }

        private static PyVal DictMethod(PyDict d, string name, PyArgs a)
        {
            switch (name)
            {
                case "get" when a.Pos.Count >= 1:
                    foreach (var kv in d.Items) if (Same(kv.Key, a.Pos[0])) return kv.Value;
                    return a.Pos.Count > 1 ? a.Pos[1] : PyNone.Instance;
                case "keys": return new PySeq(d.Items.Select(kv => kv.Key).ToList());
                case "values": return new PySeq(d.Items.Select(kv => kv.Value).ToList());
                case "items": return new PySeq(d.Items.Select(kv => (PyVal)new PySeq(new List<PyVal> { kv.Key, kv.Value }, true)).ToList());
                case "update":
                    foreach (var src in a.Pos.OfType<PyDict>())
                        foreach (var kv in src.Items) { d.Items.RemoveAll(x => Same(x.Key, kv.Key)); d.Items.Add(kv); }
                    foreach (var (k, v) in a.Kw) { d.Items.RemoveAll(x => x.Key is PyStr s && s.V == k); d.Items.Add(new KeyValuePair<PyVal, PyVal>(new PyStr(k), v)); }
                    return PyNone.Instance;
            }
            throw No("dict." + name + "() is not read.", "dict." + name + "() wird nicht gelesen.");
        }

        private PyVal StrMethod(PyStr s, string name, PyArgs a)
        {
            switch (name)
            {
                case "replace" when a.Pos.Count >= 2: return new PyStr(s.V.Replace(Str(a.Pos[0]), Str(a.Pos[1])));
                case "upper": return new PyStr(s.V.ToUpperInvariant());
                case "lower": return new PyStr(s.V.ToLowerInvariant());
                case "strip": return new PyStr(s.V.Trim());
                case "join" when a.Pos.Count == 1: return new PyStr(string.Join(s.V, Iterate(a.Pos[0]).Select(x => Str(x))));
                case "split":
                    return new PySeq((a.Pos.Count > 0 ? s.V.Split(new[] { Str(a.Pos[0]) }, StringSplitOptions.None) : s.V.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(x => (PyVal)new PyStr(x)).ToList());
            }
            throw No("str." + name + "() is not read.", "str." + name + "() wird nicht gelesen.");
        }

        private PyVal ArrayMethod(PyArr arr, string name, PyArgs a)
        {
            var v = arr.V;
            switch (name)
            {
                case "min": return new PyNum(v.Length == 0 ? double.NaN : v.Min());
                case "max": return new PyNum(v.Length == 0 ? double.NaN : v.Max());
                case "sum": return new PyNum(v.Sum());
                case "mean": return new PyNum(v.Length == 0 ? double.NaN : v.Average());
                case "tolist": return new PySeq(v.Select(x => (PyVal)new PyNum(x)).ToList());
                case "copy": case "flatten": case "ravel": case "astype": return Arr(v, arr.Bool && name != "astype");
                case "cumsum": { var s = 0.0; return Arr(v.Select(x => s += x)); }
            }
            throw No("ndarray." + name + "() is not read.", "ndarray." + name + "() wird nicht gelesen.");
        }

        // ------------------------------------------------------------------ numpy and math

        private static readonly Dictionary<string, Func<double, double>> Elementwise = new(StringComparer.Ordinal)
        {
            ["sin"] = Math.Sin, ["cos"] = Math.Cos, ["tan"] = Math.Tan,
            ["arcsin"] = Math.Asin, ["arccos"] = Math.Acos, ["arctan"] = Math.Atan, ["asin"] = Math.Asin, ["acos"] = Math.Acos, ["atan"] = Math.Atan,
            ["sinh"] = Math.Sinh, ["cosh"] = Math.Cosh, ["tanh"] = Math.Tanh,
            ["exp"] = Math.Exp, ["log"] = Math.Log, ["log10"] = Math.Log10, ["log2"] = v => Math.Log(v, 2), ["log1p"] = v => Math.Log(1 + v), ["expm1"] = v => Math.Exp(v) - 1,
            ["sqrt"] = Math.Sqrt, ["cbrt"] = v => v < 0 ? -Math.Pow(-v, 1.0 / 3) : Math.Pow(v, 1.0 / 3), ["square"] = v => v * v,
            ["abs"] = Math.Abs, ["absolute"] = Math.Abs, ["fabs"] = Math.Abs, ["sign"] = v => double.IsNaN(v) ? double.NaN : Math.Sign(v),
            ["floor"] = Math.Floor, ["ceil"] = Math.Ceiling, ["trunc"] = Math.Truncate, ["rint"] = v => Math.Round(v, MidpointRounding.ToEven),
            ["deg2rad"] = v => v * Math.PI / 180, ["rad2deg"] = v => v * 180 / Math.PI, ["radians"] = v => v * Math.PI / 180, ["degrees"] = v => v * 180 / Math.PI,
        };

        private static PyVal NumpyAttr(string name) => name switch
        {
            "pi" => new PyNum(Math.PI),
            "e" => new PyNum(Math.E),
            "inf" or "Inf" or "infty" => new PyNum(double.PositiveInfinity),
            "nan" or "NaN" or "NAN" => new PyNum(double.NaN),
            "newaxis" => throw No("numpy.newaxis is not read.", "numpy.newaxis wird nicht gelesen."),
            "random" => throw No("Random numbers (numpy.random) are not imported: the values would differ on every run.", "Zufallszahlen (numpy.random) werden nicht importiert: die Werte wären bei jedem Lauf anders."),
            _ => new PyFunc(new PyObj("module:numpy"), name),
        };

        private static PyVal MathAttr(string name) => name switch
        {
            "pi" => new PyNum(Math.PI),
            "e" => new PyNum(Math.E),
            "tau" => new PyNum(2 * Math.PI),
            "inf" => new PyNum(double.PositiveInfinity),
            "nan" => new PyNum(double.NaN),
            _ => new PyFunc(new PyObj("module:math"), name),
        };

        private PyVal MathCall(string name, PyArgs a)
        {
            if (Elementwise.TryGetValue(name, out var f) && a.Pos.Count >= 1)
            {
                if (name == "log" && a.Pos.Count == 2) return new PyNum(Math.Log(Num(a.Pos[0])) / Math.Log(Num(a.Pos[1])));
                return new PyNum(f(Num(a.Pos[0])));
            }
            switch (name)
            {
                case "pow" when a.Pos.Count == 2: return new PyNum(Math.Pow(Num(a.Pos[0]), Num(a.Pos[1])));
                case "hypot" when a.Pos.Count == 2: return new PyNum(Math.Sqrt(Num(a.Pos[0]) * Num(a.Pos[0]) + Num(a.Pos[1]) * Num(a.Pos[1])));
                case "atan2" when a.Pos.Count == 2: return new PyNum(Math.Atan2(Num(a.Pos[0]), Num(a.Pos[1])));
                case "isnan" when a.Pos.Count == 1: return PyBool.Of(double.IsNaN(Num(a.Pos[0])));
            }
            throw No("math." + name + "() is not read.", "math." + name + "() wird nicht gelesen.");
        }

        /// <summary>An array argument as numbers (a list or a single number is converted).</summary>
        private static double[] ArrArg(PyArgs a, int i, string name)
        {
            var v = a.Arg(i, name) ?? throw No("An argument is missing.", "Ein Argument fehlt.");
            return Numbers(v);
        }

        private PyVal Numpy(string name, PyArgs a)
        {
            if (Elementwise.TryGetValue(name, out var f))
            {
                if (a.Pos.Count != 1 || a.Kw.Count > 0) throw No("numpy." + name + "() with these arguments is not read.", "numpy." + name + "() mit diesen Argumenten wird nicht gelesen.");
                var v = a.Pos[0];
                if (IsNumber(v)) return new PyNum(f(Num(v)));
                var x = Numbers(v);
                Work(x.Length);
                return Arr(x.Select(f));
            }
            int Count(PyVal? v, int def)
            {
                if (v is null) return def;
                var n = Num(v, "the number of points");
                if (n < 0 || n > MaxArray) throw No("The number of points is out of range.", "Die Anzahl der Punkte liegt außerhalb des Bereichs.");
                return (int)n;
            }
            switch (name)
            {
                case "array": case "asarray": case "asanyarray": case "float64": case "double":
                    {
                        var v = a.Arg(0, "object") ?? throw No("numpy.array() needs a value.", "numpy.array() braucht einen Wert.");
                        if (v is PySeq s && s.Items.Count > 0 && s.Items.All(x => x is PySeq))
                            throw No("Two-dimensional arrays are not read.", "Zweidimensionale Arrays werden nicht gelesen.");
                        if (IsNumber(v) && name is "float64" or "double") return new PyNum(Num(v));
                        return Arr(Numbers(v));
                    }
                case "linspace":
                    {
                        var start = Num(a.Arg(0, "start") ?? throw No("linspace needs start and stop.", "linspace braucht Anfang und Ende."));
                        var stop = Num(a.Arg(1, "stop") ?? throw No("linspace needs start and stop.", "linspace braucht Anfang und Ende."));
                        var n = Count(a.Arg(2, "num"), 50);
                        var endpoint = a["endpoint"] is not { } ep || Truth(ep);
                        if (a.Has("retstep")) throw No("linspace(retstep=…) is not read.", "linspace(retstep=…) wird nicht gelesen.");
                        var div = endpoint ? n - 1 : n;
                        Work(n);
                        return Arr(Enumerable.Range(0, n).Select(i => div <= 0 ? start : i == div && endpoint ? stop : start + (stop - start) * i / div));
                    }
                case "logspace": case "geomspace":
                    {
                        var start = Num(a.Arg(0, "start") ?? throw No("A start is needed.", "Ein Anfang wird gebraucht."));
                        var stop = Num(a.Arg(1, "stop") ?? throw No("A stop is needed.", "Ein Ende wird gebraucht."));
                        var n = Count(a.Arg(2, "num"), 50);
                        var endpoint = a["endpoint"] is not { } ep || Truth(ep);
                        var div = endpoint ? n - 1 : n;
                        Work(n);
                        if (name == "logspace")
                        {
                            var b = a["base"] is { } bv ? Num(bv) : 10;
                            return Arr(Enumerable.Range(0, n).Select(i => Math.Pow(b, div <= 0 ? start : start + (stop - start) * i / div)));
                        }
                        if (start <= 0 || stop <= 0) throw No("geomspace needs positive bounds.", "geomspace braucht positive Grenzen.");
                        return Arr(Enumerable.Range(0, n).Select(i => Math.Exp(div <= 0 ? Math.Log(start) : Math.Log(start) + (Math.Log(stop) - Math.Log(start)) * i / div)));
                    }
                case "arange":
                    {
                        double start = 0, stop, step = 1;
                        var pos = a.Pos.Select(p => Num(p)).ToList();
                        if (a["start"] is { } st) start = Num(st);
                        if (a["stop"] is { } sp) stop = Num(sp);
                        else if (pos.Count == 1) stop = pos[0];
                        else if (pos.Count >= 2) { start = pos[0]; stop = pos[1]; if (pos.Count > 2) step = pos[2]; }
                        else throw No("arange needs a stop value.", "arange braucht einen Endwert.");
                        if (a["step"] is { } stp) step = Num(stp);
                        if (step == 0 || !Fmt.Finite(step) || !Fmt.Finite(start) || !Fmt.Finite(stop)) throw No("arange with these values is not read.", "arange mit diesen Werten wird nicht gelesen.");
                        var n = (long)Math.Ceiling((stop - start) / step);
                        if (n > MaxArray) throw No("The array is too large to be read.", "Das Array ist zu groß zum Einlesen.");
                        Work(Math.Max(0, n));
                        return Arr(Enumerable.Range(0, (int)Math.Max(0, n)).Select(i => start + i * step));
                    }
                case "zeros": case "ones": case "empty":
                    {
                        var n = Count(a.Arg(0, "shape"), 0);
                        return Arr(Enumerable.Repeat(name == "ones" ? 1.0 : 0.0, n));
                    }
                case "full":
                    {
                        var n = Count(a.Arg(0, "shape"), 0);
                        var v = Num(a.Arg(1, "fill_value") ?? throw No("full needs a value.", "full braucht einen Wert."));
                        return Arr(Enumerable.Repeat(v, n));
                    }
                case "zeros_like": case "ones_like": case "full_like":
                    {
                        var n = ArrArg(a, 0, "a").Length;
                        var v = name == "ones_like" ? 1 : name == "full_like" ? Num(a.Arg(1, "fill_value") ?? new PyNum(0)) : 0;
                        return Arr(Enumerable.Repeat(v, n));
                    }
                case "concatenate": case "hstack": case "append": case "r_":
                    {
                        if (name == "append") return Arr(ArrArg(a, 0, "arr").Concat(ArrArg(a, 1, "values")));
                        var parts = Iterate(a.Arg(0, "arrays") ?? throw No("Arrays are needed.", "Arrays werden gebraucht.")).Select(p => Numbers(p));
                        return Arr(parts.SelectMany(p => p));
                    }
                case "minimum": case "maximum": case "power": case "arctan2": case "hypot": case "add": case "subtract": case "multiply": case "divide":
                    {
                        var x = a.Arg(0, "x1") ?? throw No("Two arguments are needed.", "Zwei Argumente werden gebraucht.");
                        var y = a.Arg(1, "x2") ?? throw No("Two arguments are needed.", "Zwei Argumente werden gebraucht.");
                        Func<double, double, double> op = name switch
                        {
                            "minimum" => Math.Min, "maximum" => Math.Max, "power" => Math.Pow, "arctan2" => Math.Atan2,
                            "hypot" => (p, q) => Math.Sqrt(p * p + q * q), "add" => (p, q) => p + q, "subtract" => (p, q) => p - q,
                            "multiply" => (p, q) => p * q, _ => (p, q) => p / q,
                        };
                        if (IsNumber(x) && IsNumber(y)) return new PyNum(op(Num(x), Num(y)));
                        var xs = Numbers(x); var ys = Numbers(y);
                        var n = Math.Max(xs.Length, ys.Length);
                        if (xs.Length != n && xs.Length != 1 || ys.Length != n && ys.Length != 1) throw No("Arrays of different lengths.", "Arrays verschiedener Länge.");
                        Work(n);
                        return Arr(Enumerable.Range(0, n).Select(i => op(xs[xs.Length == 1 ? 0 : i], ys[ys.Length == 1 ? 0 : i])));
                    }
                case "clip":
                    {
                        var x = ArrArg(a, 0, "a");
                        var lo = a.Arg(1, "a_min") is { } l && l is not PyNone ? Num(l) : double.NegativeInfinity;
                        var hi = a.Arg(2, "a_max") is { } h && h is not PyNone ? Num(h) : double.PositiveInfinity;
                        return Arr(x.Select(v => Math.Max(lo, Math.Min(hi, v))));
                    }
                case "round": case "around":
                    {
                        var d = a.Arg(1, "decimals") is { } dv ? (int)Num(dv) : 0;
                        var v = a.Arg(0, "a") ?? throw No("A value is needed.", "Ein Wert wird gebraucht.");
                        if (IsNumber(v)) return new PyNum(Math.Round(Num(v), Math.Max(0, Math.Min(15, d)), MidpointRounding.ToEven));
                        return Arr(Numbers(v).Select(x => Math.Round(x, Math.Max(0, Math.Min(15, d)), MidpointRounding.ToEven)));
                    }
                case "cumsum": { var s = 0.0; return Arr(ArrArg(a, 0, "a").Select(x => s += x)); }
                case "diff": { var x = ArrArg(a, 0, "a"); return Arr(Enumerable.Range(1, Math.Max(0, x.Length - 1)).Select(i => x[i] - x[i - 1])); }
                case "flip": return Arr(Enumerable.Reverse(ArrArg(a, 0, "m")));
                case "min": case "amin": case "nanmin": { var x = ArrArg(a, 0, "a").Where(v => name != "nanmin" || !double.IsNaN(v)).ToList(); return new PyNum(x.Count == 0 ? double.NaN : x.Min()); }
                case "max": case "amax": case "nanmax": { var x = ArrArg(a, 0, "a").Where(v => name != "nanmax" || !double.IsNaN(v)).ToList(); return new PyNum(x.Count == 0 ? double.NaN : x.Max()); }
                case "sum": return new PyNum(ArrArg(a, 0, "a").Sum());
                case "mean": { var x = ArrArg(a, 0, "a"); return new PyNum(x.Length == 0 ? double.NaN : x.Average()); }
                case "isnan": { var v = a.Arg(0, "x")!; return IsNumber(v) ? PyBool.Of(double.IsNaN(Num(v))) : Arr(Numbers(v).Select(x => double.IsNaN(x) ? 1.0 : 0), true); }
                case "where":
                    {
                        if (a.Pos.Count != 3) throw No("numpy.where() with one argument is not read.", "numpy.where() mit einem Argument wird nicht gelesen.");
                        var c = Numbers(a.Pos[0]); var x = a.Pos[1]; var y = a.Pos[2];
                        double At(PyVal v, int i) => IsNumber(v) ? Num(v) : Numbers(v)[i];
                        return Arr(Enumerable.Range(0, c.Length).Select(i => c[i] != 0 ? At(x, i) : At(y, i)));
                    }
                case "interp":
                    {
                        var x = a.Arg(0, "x")!; var xp = ArrArg(a, 1, "xp"); var fp = ArrArg(a, 2, "fp");
                        double I(double v)
                        {
                            if (xp.Length == 0) return double.NaN;
                            if (v <= xp[0]) return fp[0];
                            for (var i = 1; i < xp.Length; i++) if (v <= xp[i]) return fp[i - 1] + (fp[i] - fp[i - 1]) * (v - xp[i - 1]) / (xp[i] - xp[i - 1]);
                            return fp[fp.Length - 1];
                        }
                        return IsNumber(x) ? new PyNum(I(Num(x))) : Arr(Numbers(x).Select(I));
                    }
            }
            throw No("numpy." + name + "() is not read.", "numpy." + name + "() wird nicht gelesen.");
        }
    }

    /// <summary>Executes the statements of a script in the subset: assignments, calls, bounded for loops, with blocks; everything else is reported.</summary>
    internal sealed class PyRunner
    {
        private readonly PyEval _eval;
        private readonly IPyHost _host;
        private readonly CodeImportReport _report;

        public PyRunner(PyEval eval, IPyHost host, CodeImportReport report)
        {
            _eval = eval; _host = host; _report = report;
            eval.Invoke = Call;
        }

        public PyEval Eval => _eval;

        /// <summary>Called for every def that is not run (helpers with parameters): the host may run its body itself.</summary>
        public Func<PyStmt, bool>? RunDefinition { get; set; }

        /// <summary>The statement currently run (for reports of the host).</summary>
        public PyStmt? Current { get; private set; }

        public void Skip(PyStmt s, I18nText reason) => _report.Skip(s.Line, Snippet(s.Source), reason);

        public static string Snippet(string source)
        {
            var t = string.Join(" ", source.Replace("\r", "").Split('\n').Select(l => l.Trim())).Trim();
            return t.Length > 90 ? t.Substring(0, 87) + "…" : t;
        }

        public void Run(IEnumerable<PyStmt> stmts)
        {
            foreach (var s in stmts) Statement(s);
        }

        private const long MaxSteps = 1_000_000;
        private long _steps;
        private bool _stopped;

        public void Statement(PyStmt s)
        {
            if (_stopped) return;
            if (++_steps > MaxSteps)
            {
                _stopped = true;
                Skip(s, new I18nText("The script runs too many statements to be read; the rest is not imported.", "Das Skript führt zu viele Anweisungen aus; der Rest wird nicht importiert."));
                return;
            }
            Current = s;
            try { Exec(s); }
            catch (PyUnsupported ex) { Skip(s, ex.Reason); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or NullReferenceException or InvalidCastException or KeyNotFoundException)
            {
                Skip(s, new I18nText("Cannot be read: " + ex.Message, "Nicht lesbar: " + ex.Message));
            }
        }

        private void Exec(PyStmt s)
        {
            var t = s.Tokens;
            if (s.Keyword is { } kw)
            {
                switch (kw)
                {
                    case "def": Def(s); return;
                    case "for": For(s); return;
                    case "with":
                        // The context manager is not entered (plt.style.context …); its body is read.
                        Run(s.Body);
                        return;
                    case "if":
                        if (IsMainGuard(s.Header)) { Run(s.Body); return; }
                        if (!Plots(s)) return;
                        throw new PyUnsupported("Conditions (if) are not evaluated; the plotting code in this block is not imported.", "Bedingungen (if) werden nicht ausgewertet; der Zeichencode in diesem Block wird nicht importiert.");
                    case "elif": case "else":
                        if (!Plots(s)) return;
                        throw new PyUnsupported("Conditions (" + kw + ") are not evaluated; the plotting code in this block is not imported.", "Bedingungen (" + kw + ") werden nicht ausgewertet; der Zeichencode in diesem Block wird nicht importiert.");
                    case "class":
                        if (!Plots(s)) return;
                        throw new PyUnsupported("Class definitions are not read.", "Klassendefinitionen werden nicht gelesen.");
                    case "try":
                        // The protected block is read; except/finally blocks are not.
                        Run(s.Body);
                        return;
                    case "except": case "finally":
                        if (!Plots(s)) return;
                        throw new PyUnsupported("“" + kw + "” blocks are not read; the plotting code in this block is not imported.", "„" + kw + "“-Blöcke werden nicht gelesen; der Zeichencode in diesem Block wird nicht importiert.");
                    default:
                        if (!Plots(s)) return;
                        throw new PyUnsupported("“" + kw + "” blocks are not read; the plotting code in this block is not imported.", "„" + kw + "“-Blöcke werden nicht gelesen; der Zeichencode in diesem Block wird nicht importiert.");
                }
            }
            var first = t[0];
            if (first.Kind == PyTok.Name)
            {
                switch (first.Text)
                {
                    case "import": Import(s); return;
                    case "from": FromImport(s); return;
                    case "return":
                        throw new PyReturn(t.Count > 1 ? _eval.Eval(ParseRange(t, 1, t.Count)) : PyNone.Instance);
                    case "pass": case "global": case "nonlocal": case "break": case "continue": case "assert": case "del":
                        return;
                    case "raise":
                        return;
                    case "print":
                        return;
                }
            }
            // Docstrings and other bare constants.
            if (t.Count == 1 && first.Kind is PyTok.String or PyTok.Number) return;
            if (t.All(x => x.Kind == PyTok.String)) return;

            // Assignment: targets = value (chained), augmented assignment, annotated assignment.
            var eqs = TopLevel(t, "=");
            var aug = t.FindIndex(x => x.Kind == PyTok.Op && x.Text.Length >= 2 && x.Text.EndsWith("=", StringComparison.Ordinal) && x.Text is not ("==" or "!=" or "<=" or ">=" or ":="));
            if (eqs.Count > 0)
            {
                var value = ParseRange(t, eqs[eqs.Count - 1] + 1, t.Count);
                var v = _eval.Eval(value);
                var start = 0;
                foreach (var e in eqs)
                {
                    var targetTokens = t.GetRange(start, e - start);
                    var colon = TopLevel(targetTokens, ":");
                    if (colon.Count > 0) targetTokens = targetTokens.GetRange(0, colon[0]); // x: float = 1
                    _eval.Bind(ParseTarget(targetTokens), v);
                    start = e + 1;
                }
                return;
            }
            if (aug > 0 && TopLevel(t, t[aug].Text).Count == 1)
            {
                var target = ParseRange(t, 0, aug);
                var value = _eval.Eval(ParseRange(t, aug + 1, t.Count));
                var op = t[aug].Text.Substring(0, t[aug].Text.Length - 1);
                var cur = _eval.Eval(target);
                PyVal result;
                if (op == "+" && cur is PySeq list && !list.Tuple) { list.Items.AddRange(_eval.Iterate(value)); result = list; }
                else result = _eval.Binary(op, cur, value);
                _eval.Bind(target, result);
                return;
            }
            if (TopLevel(t, ":").Count > 0 && TopLevel(t, ":")[0] > 0)
                return; // a bare annotation "x: int"
            _eval.Eval(ParseRange(t, 0, t.Count));
        }

        private static readonly HashSet<string> PlotNames = new(StringComparer.Ordinal)
        {
            "plot", "scatter", "step", "bar", "barh", "fill_between", "axhline", "axvline", "axvspan", "hlines", "vlines", "annotate", "text", "errorbar",
            "semilogx", "semilogy", "loglog", "set_xlabel", "set_ylabel", "set_title", "xlabel", "ylabel", "title", "suptitle", "set_xlim", "set_ylim", "xlim",
            "ylim", "set_xscale", "set_yscale", "xscale", "yscale", "legend", "grid", "subplots", "subplot", "add_subplot", "figure", "set", "add_artist",
            "set_xticks", "set_yticks", "xticks", "yticks", "set_xticklabels", "set_yticklabels", "bar_label",
        };

        /// <summary>True if the block calls a plotting function or method (then skipping it loses content and is reported).</summary>
        private static bool Plots(PyStmt s)
        {
            bool Any(PyStmt x)
            {
                var t = x.Tokens;
                for (var i = 0; i + 1 < t.Count; i++)
                    if (t[i].Kind == PyTok.Name && PlotNames.Contains(t[i].Text) && t[i + 1].Is("(") && (i == 0 || t[i - 1].Is("."))) return true;
                return x.Body.Any(Any);
            }
            return Any(s);
        }

        private static bool IsMainGuard(List<PyToken> h)
            => h.Count == 3 && h[0].IsName("__name__") && h[1].Is("==") && h[2].Kind == PyTok.String && h[2].Str == "__main__";

        /// <summary>Indices of an operator at bracket depth 0.</summary>
        private static List<int> TopLevel(List<PyToken> t, string op)
        {
            var list = new List<int>();
            var depth = 0;
            var lambda = 0;
            for (var i = 0; i < t.Count; i++)
            {
                var x = t[i];
                if (x.IsName("lambda")) lambda++;
                if (x.Kind != PyTok.Op) continue;
                if (x.Text is "(" or "[" or "{") depth++;
                else if (x.Text is ")" or "]" or "}") depth--;
                else if (depth == 0 && x.Text == op)
                {
                    if (op == ":" && lambda > 0) { lambda--; continue; }
                    list.Add(i);
                }
            }
            return list;
        }

        private static PyExpr ParseRange(List<PyToken> t, int from, int to)
        {
            var p = new PyParser(t.GetRange(from, to - from));
            var e = p.ExprList();
            p.ExpectEnd();
            return e;
        }

        private static PyExpr ParseTarget(List<PyToken> t)
        {
            var p = new PyParser(t);
            var e = p.ExprList();
            p.ExpectEnd();
            return e;
        }

        private void Import(PyStmt s)
        {
            // import a.b.c as x, d
            var t = s.Tokens;
            var i = 1;
            while (i < t.Count)
            {
                var name = new StringBuilder();
                while (i < t.Count && (t[i].Kind == PyTok.Name && !t[i].IsName("as") || t[i].Is(".")))
                    name.Append(t[i++].Text);
                string? alias = null;
                if (i < t.Count && t[i].IsName("as")) { alias = t[i + 1].Text; i += 2; }
                var dotted = name.ToString();
                var module = _host.Module(dotted);
                if (module is null)
                {
                    Skip(s, new I18nText("The module “" + dotted + "” is not read; code that uses it is not imported.", "Das Modul „" + dotted + "“ wird nicht gelesen; Code, der es verwendet, wird nicht importiert."));
                }
                else if (alias is not null) _eval.Set(alias, module);
                else
                {
                    // "import matplotlib.pyplot" binds "matplotlib"; attribute access then resolves the submodule.
                    var top = dotted.Split('.')[0];
                    if (_host.Module(top) is { } topModule) _eval.Set(top, topModule);
                }
                if (i < t.Count && t[i].Is(",")) i++;
                else break;
            }
        }

        private void FromImport(PyStmt s)
        {
            // from a.b import c as d, e
            var t = s.Tokens;
            var i = 1;
            var name = new StringBuilder();
            while (i < t.Count && !t[i].IsName("import")) name.Append(t[i++].Text);
            i++;
            var dotted = name.ToString();
            var module = _host.Module(dotted);
            if (module is null)
            {
                Skip(s, new I18nText("The module “" + dotted + "” is not read; code that uses it is not imported.", "Das Modul „" + dotted + "“ wird nicht gelesen; Code, der es verwendet, wird nicht importiert."));
                return;
            }
            while (i < t.Count)
            {
                if (t[i].Is("(") || t[i].Is(")") || t[i].Is(",")) { i++; continue; }
                if (t[i].Is("*")) { Skip(s, new I18nText("“import *” is not read; use the module name.", "„import *“ wird nicht gelesen; bitte den Modulnamen verwenden.")); return; }
                var n = t[i++].Text;
                var alias = n;
                if (i < t.Count && t[i].IsName("as")) { alias = t[i + 1].Text; i += 2; }
                if (module is PyObj o)
                {
                    var sub = _host.Module(dotted + "." + n);
                    PyVal v;
                    if (sub is not null) v = sub;
                    else if (o.Kind == "module:numpy") v = NumpyName(n);
                    else if (o.Kind == "module:math") v = MathName(n);
                    else v = _host.Attr(o, n);
                    _eval.Set(alias, v);
                }
            }
        }

        private static PyVal NumpyName(string n) => n switch
        {
            "pi" => new PyNum(Math.PI), "e" => new PyNum(Math.E), "nan" => new PyNum(double.NaN), "inf" => new PyNum(double.PositiveInfinity),
            _ => new PyFunc(new PyObj("module:numpy"), n),
        };

        private static PyVal MathName(string n) => n switch
        {
            "pi" => new PyNum(Math.PI), "e" => new PyNum(Math.E), "tau" => new PyNum(2 * Math.PI), "nan" => new PyNum(double.NaN), "inf" => new PyNum(double.PositiveInfinity),
            _ => new PyFunc(new PyObj("module:math"), n),
        };

        private void Def(PyStmt s)
        {
            var h = s.Header;
            if (h.Count == 0 || h[0].Kind != PyTok.Name) throw new PyUnsupported("Cannot read the function definition.", "Die Funktionsdefinition ist nicht lesbar.");
            var name = h[0].Text;
            _eval.Set(name, new PyUserFunc(name, s));
            // Parameters: a function without required parameters (figure functions) is read in place.
            var required = 0;
            var depth = 0;
            var inParam = false;
            var hasDefault = false;
            for (var i = 2; i < h.Count; i++)
            {
                var x = h[i];
                if (x.Kind == PyTok.Op && x.Text is "(" or "[" or "{") { depth++; continue; }
                if (x.Kind == PyTok.Op && x.Text is ")" or "]" or "}") { if (depth == 0) break; depth--; continue; }
                if (depth > 0) continue;
                if (x.Is(",")) { if (inParam && !hasDefault) required++; inParam = false; hasDefault = false; continue; }
                if (x.Is("=")) hasDefault = true;
                else if (x.Is("*") || x.Is("**")) hasDefault = true;
                else if (x.Is(":")) continue;
                else if (!inParam) inParam = true;
            }
            if (inParam && !hasDefault) required++;
            if (required == 0)
            {
                _eval.Ran.Add(s);
                var saved = _eval.Locals;
                _eval.Locals = new Dictionary<string, PyVal>(StringComparer.Ordinal);
                try { Run(s.Body); }
                catch (PyReturn) { }
                finally { _eval.Locals = saved; }
                return;
            }
            if (RunDefinition?.Invoke(s) == true) return;
        }

        private int _calls;

        /// <summary>
        /// A call of a function defined in the script: the parameters are bound (positional, keyword, defaults) and its
        /// statements are read like the rest of the script (nesting limited); the value of "return" is the result.
        /// </summary>
        public PyVal Call(PyUserFunc f, PyArgs args)
        {
            if (_calls >= 16) throw new PyUnsupported("Functions nested too deeply are not read.", "Zu tief verschachtelte Funktionen werden nicht gelesen.");
            var h = f.Def.Header;
            var open = h.FindIndex(x => x.Is("("));
            var close = h.FindLastIndex(x => x.Is(")"));
            var ps = new List<(string Name, List<PyToken>? Default, string Star)>();
            if (open >= 0 && close > open)
            {
                var depth = 0;
                var cur = new List<PyToken>();
                void Flush()
                {
                    if (cur.Count == 0) return;
                    var star = cur[0].Is("*") ? "*" : cur[0].Is("**") ? "**" : "";
                    var start = star.Length > 0 ? 1 : 0;
                    if (start < cur.Count && cur[start].Kind == PyTok.Name)
                    {
                        var eq = cur.FindIndex(x => x.Is("="));
                        ps.Add((cur[start].Text, eq >= 0 ? cur.GetRange(eq + 1, cur.Count - eq - 1) : null, star));
                    }
                    cur = new List<PyToken>();
                }
                for (var i = open + 1; i < close; i++)
                {
                    var x = h[i];
                    if (x.Kind == PyTok.Op && x.Text is "(" or "[" or "{") depth++;
                    else if (x.Kind == PyTok.Op && x.Text is ")" or "]" or "}") depth--;
                    if (depth == 0 && x.Is(",")) { Flush(); continue; }
                    cur.Add(x);
                }
                Flush();
            }
            var locals = new Dictionary<string, PyVal>(StringComparer.Ordinal);
            var pos = 0;
            foreach (var (name, def, star) in ps)
            {
                if (star == "*") { locals[name] = new PySeq(args.Pos.Skip(pos).ToList(), true); pos = args.Pos.Count; continue; }
                if (star == "**") { var d = new PyDict(); foreach (var (k, v) in args.Kw.Where(k => ps.All(p => p.Name != k.Name))) d.Items.Add(new KeyValuePair<PyVal, PyVal>(new PyStr(k), v)); locals[name] = d; continue; }
                if (args[name] is { } kv) locals[name] = kv;
                else if (pos < args.Pos.Count) locals[name] = args.Pos[pos++];
                else if (def is not null)
                {
                    var p = new PyParser(def);
                    locals[name] = _eval.Eval(p.ExprList());
                }
                else throw new PyUnsupported("The call of “" + f.Name + "” lacks the argument “" + name + "”.", "Dem Aufruf von „" + f.Name + "“ fehlt das Argument „" + name + "“.");
            }
            var saved = _eval.Locals;
            var savedCurrent = Current;
            _eval.Locals = locals;
            _calls++;
            try { Run(f.Def.Body); return PyNone.Instance; }
            catch (PyReturn r) { return r.Value; }
            finally { _eval.Locals = saved; _calls--; Current = savedCurrent; }
        }

        private void For(PyStmt s)
        {
            var h = s.Header;
            var p = new PyParser(h);
            var target = p.TargetList();
            var inPos = p.Position;
            if (inPos >= h.Count || !h[inPos].IsName("in")) throw new PyUnsupported("Cannot read the for loop.", "Die for-Schleife ist nicht lesbar.");
            var iterExpr = ParseRange(h, inPos + 1, h.Count);
            var items = _eval.Iterate(_eval.Eval(iterExpr)).ToList();
            if (items.Count > PyEval.MaxLoop) throw new PyUnsupported("The loop runs too often to be read.", "Die Schleife läuft zu oft zum Einlesen.");
            if (s.Body.Any(b => b.Keyword is null && b.Tokens.Count > 0 && (b.Tokens[0].IsName("break") || b.Tokens[0].IsName("continue"))))
                throw new PyUnsupported("Loops with break or continue are not read.", "Schleifen mit break oder continue werden nicht gelesen.");
            foreach (var item in items)
            {
                _eval.Bind(target, item);
                Run(s.Body);
            }
        }
    }
}
