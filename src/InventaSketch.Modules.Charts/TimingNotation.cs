using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>State of a digital signal during a segment of a timing diagram.</summary>
    internal enum WaveKind
    {
        /// <summary>Logic low (lower level).</summary>
        Low,
        /// <summary>Logic high (upper level).</summary>
        High,
        /// <summary>Undefined / don't care: both levels, hatched between them.</summary>
        Undefined,
        /// <summary>High impedance: a line in the middle.</summary>
        HighZ,
        /// <summary>Bus or data value: both levels with crossing edges (hexagon-shaped box), optional value text.</summary>
        Bus,
    }

    /// <summary>One segment of a wave: a state from <see cref="Start"/> to <see cref="End"/> (time steps).</summary>
    internal sealed record WaveSegment(WaveKind Kind, double Start, double End, string? Value = null)
    {
        public double Length => End - Start;
        /// <summary>True for the states drawn with both levels (bus, undefined).</summary>
        public bool TwoRail => Kind is WaveKind.Bus or WaveKind.Undefined;
    }

    /// <summary>A problem in the signal notation (line and column 1-based); reported, never thrown by the parser.</summary>
    internal sealed record TimingError(int Line, int Column, I18nText Message)
    {
        public string Text(Lang lang) => (lang == Lang.De ? "Zeile " : "Row ") + Fmt.I(Line) + (Column > 0 ? (lang == Lang.De ? ", Zeichen " : ", character ") + Fmt.I(Column) : "") + ": " + Message.Get(lang);
    }

    /// <summary>One signal row of a timing diagram: its name and its wave.</summary>
    internal sealed class TimingSignal
    {
        public string Name { get; set; } = "";
        /// <summary>Line of the notation (1-based).</summary>
        public int Line { get; set; }
        /// <summary>A clock defined by the keyword "clock": its segments follow the length of the diagram.</summary>
        public bool IsClock { get; set; }
        /// <summary>Clock period in time steps.</summary>
        public double Period { get; set; } = 1;
        /// <summary>Clock duty cycle (share of the period at high level).</summary>
        public double Duty { get; set; } = 0.5;
        /// <summary>Clock starts with the low phase.</summary>
        public bool Inverted { get; set; }
        public List<WaveSegment> Segments { get; } = new();
        /// <summary>Length of the written wave (time steps; 0 for a clock).</summary>
        public double Length => Segments.Count == 0 ? 0 : Segments[Segments.Count - 1].End;
    }

    /// <summary>The parsed notation: signals on a shared time axis of <see cref="Steps"/> time steps, and the problems found.</summary>
    internal sealed class TimingSet
    {
        public List<TimingSignal> Signals { get; } = new();
        public List<TimingError> Errors { get; } = new();
        public int Steps { get; set; }
    }

    /// <summary>A vertical marker at a time (axis units) with a label ("t1").</summary>
    internal sealed record TimingMarker(string Label, double Time);

    /// <summary>A dimension arrow between two markers (indices into the marker list) with its text.</summary>
    internal sealed record TimingSpan(int From, int To, string Text);

    /// <summary>
    /// Compact text notation of a digital timing diagram (drawn independently; the single-character wave idea is the one
    /// of WaveDrom). One signal per line, <c>name: wave</c> (a semicolon instead of the colon works too, so a two-column
    /// table pastes directly); empty lines and lines starting with <c>#</c> are ignored. A wave is either
    /// <list type="bullet">
    /// <item>the keyword <c>clock</c> (also <c>clk</c>, <c>takt</c>) with an optional period in time steps (default 1), an
    /// optional duty cycle (<c>25%</c> or <c>0.25</c>, default 50 %) and <c>inv</c> to start low – a clock runs over the
    /// whole diagram; or</item>
    /// <item>one character per time step: <c>0</c>/<c>l</c> low, <c>1</c>/<c>h</c> high, <c>x</c> undefined (hatched),
    /// <c>z</c> high impedance, <c>=</c> a data value without text, <c>[A5]</c> a data value with text, <c>p</c>/<c>n</c>
    /// one clock period starting high/low, and <c>.</c> to hold the previous state one more step (after <c>p</c>/<c>n</c>:
    /// one more period). Spaces are ignored.</item>
    /// </list>
    /// The diagram is as long as the longest written wave (8 steps when there are only clocks); shorter waves hold their
    /// last state to the end.
    /// </summary>
    internal static class TimingNotation
    {
        /// <summary>Most time steps of a diagram (longer waves are cut).</summary>
        public const int MaxSteps = 400;
        /// <summary>Most signals drawn.</summary>
        public const int MaxSignals = 60;
        private const int MaxSegments = 2000;

        private static readonly string[] ClockWords = { "clock", "clk", "takt" };
        private static readonly string[] InvertWords = { "inv", "inverted", "invertiert", "neg", "n" };

        /// <summary>The non-empty, non-comment lines with their 1-based line numbers.</summary>
        public static IEnumerable<(int Line, string Text)> Lines(string? text)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length == 0 || t[0] == '#') continue;
                yield return (i + 1, t);
            }
        }

        /// <summary>Splits a line into name and wave at the first ':' or ';' (no separator: no name).</summary>
        public static (string Name, string Wave, int WaveColumn) Split(string line)
        {
            var i = line.IndexOfAny(new[] { ':', ';' });
            if (i < 0) return ("", line.Trim(), 1);
            var wave = line.Substring(i + 1);
            var lead = wave.Length - wave.TrimStart().Length;
            return (line.Substring(0, i).Trim(), wave.Trim(), i + 2 + lead);
        }

        /// <summary>Parses the notation. Never throws: problems are listed in <see cref="TimingSet.Errors"/>.</summary>
        public static TimingSet Parse(string? text)
        {
            var set = new TimingSet();
            foreach (var (line, t) in Lines(text))
            {
                if (set.Signals.Count >= MaxSignals)
                {
                    set.Errors.Add(new TimingError(line, 0, new I18nText("More than " + Fmt.I(MaxSignals) + " signals; the rest is left out", "Mehr als " + Fmt.I(MaxSignals) + " Signale; der Rest wird weggelassen")));
                    break;
                }
                var (name, wave, col) = Split(t);
                var s = new TimingSignal { Name = name, Line = line };
                if (IsClock(wave)) ParseClock(s, wave, col, set.Errors);
                else ParseWave(s, wave, col, set.Errors);
                set.Signals.Add(s);
            }
            var written = set.Signals.Where(s => !s.IsClock).Select(s => s.Length).DefaultIfEmpty(0).Max();
            var steps = written > 0 ? (int)Math.Ceiling(written - 1e-9)
                : (int)Math.Ceiling(Math.Max(8, 4 * set.Signals.Where(s => s.IsClock).Select(s => s.Period).DefaultIfEmpty(1).Max()) - 1e-9);
            set.Steps = Math.Max(1, Math.Min(MaxSteps, steps));
            foreach (var s in set.Signals) Complete(s, set.Steps);
            return set;
        }

        private static bool IsClock(string wave)
        {
            var first = wave.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return ClockWords.Any(w => string.Equals(w, first, StringComparison.OrdinalIgnoreCase));
        }

        private static void ParseClock(TimingSignal s, string wave, int col, List<TimingError> errors)
        {
            s.IsClock = true;
            var tokens = wave.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
            var numbers = 0;
            foreach (var tok in tokens)
            {
                if (InvertWords.Any(w => string.Equals(w, tok, StringComparison.OrdinalIgnoreCase))) { s.Inverted = true; continue; }
                var percent = tok.EndsWith("%", StringComparison.Ordinal);
                if (ChartData.TryNumber(tok, out var v) && Fmt.Finite(v))
                {
                    if (!percent && numbers == 0)
                    {
                        if (v < 0.05 || v > MaxSteps) errors.Add(new TimingError(s.Line, col, new I18nText("Clock period must be between 0.05 and " + Fmt.I(MaxSteps) + " steps", "Taktperiode muss zwischen 0,05 und " + Fmt.I(MaxSteps) + " Schritten liegen")));
                        else s.Period = v;
                        numbers++;
                        continue;
                    }
                    var duty = percent || v > 1 ? v / 100 : v;
                    if (duty <= 0 || duty >= 1) errors.Add(new TimingError(s.Line, col, new I18nText("Duty cycle must be between 0 and 100 %", "Tastgrad muss zwischen 0 und 100 % liegen")));
                    else s.Duty = duty;
                    numbers = 2;
                    continue;
                }
                errors.Add(new TimingError(s.Line, col, new I18nText("Unknown clock option “" + tok + "” (period, duty cycle in %, inv)", "Unbekannte Taktangabe „" + tok + "“ (Periode, Tastgrad in %, inv)")));
            }
        }

        private static void ParseWave(TimingSignal s, string wave, int col, List<TimingError> errors)
        {
            var segs = s.Segments;
            double t = 0;
            char? clock = null; // 'p' or 'n' while a '.' repeats a clock period
            void Add(WaveKind k, double len, string? value = null, bool merge = true)
            {
                if (segs.Count >= MaxSegments) return;
                if (merge && segs.Count > 0 && segs[segs.Count - 1] is { } last && last.Kind == k && k != WaveKind.Bus && Math.Abs(last.End - t) < 1e-9)
                    segs[segs.Count - 1] = last with { End = t + len };
                else segs.Add(new WaveSegment(k, t, t + len, value));
                t += len;
            }
            void Period(char c)
            {
                Add(c == 'p' ? WaveKind.High : WaveKind.Low, 0.5);
                Add(c == 'p' ? WaveKind.Low : WaveKind.High, 0.5);
            }
            if (wave.Length == 0) errors.Add(new TimingError(s.Line, col, new I18nText("No wave given", "Kein Signalverlauf angegeben")));
            for (var i = 0; i < wave.Length && t < MaxSteps; i++)
            {
                var c = wave[i];
                var at = col + i;
                if (char.IsWhiteSpace(c)) continue;
                switch (c)
                {
                    case '0': case 'l': case 'L': clock = null; Add(WaveKind.Low, 1); break;
                    case '1': case 'h': case 'H': clock = null; Add(WaveKind.High, 1); break;
                    case 'x': case 'X': clock = null; Add(WaveKind.Undefined, 1); break;
                    case 'z': case 'Z': clock = null; Add(WaveKind.HighZ, 1); break;
                    case '=': clock = null; Add(WaveKind.Bus, 1, "", merge: false); break;
                    case 'p': case 'P': clock = 'p'; Period('p'); break;
                    case 'n': case 'N': clock = 'n'; Period('n'); break;
                    case '[':
                        {
                            clock = null;
                            var end = wave.IndexOf(']', i + 1);
                            if (end < 0)
                            {
                                errors.Add(new TimingError(s.Line, at, new I18nText("Missing “]” after a data value", "Fehlendes „]“ nach einem Datenwert")));
                                end = wave.Length;
                            }
                            Add(WaveKind.Bus, 1, wave.Substring(i + 1, Math.Max(0, end - i - 1)).Trim(), merge: false);
                            i = end;
                            break;
                        }
                    case '.':
                        if (clock is { } cc) Period(cc);
                        else if (segs.Count == 0)
                        {
                            errors.Add(new TimingError(s.Line, at, new I18nText("“.” has no state before it to hold", "„.“ hat keinen Zustand davor, der gehalten werden kann")));
                            Add(WaveKind.Undefined, 1);
                        }
                        else
                        {
                            var last = segs[segs.Count - 1];
                            segs[segs.Count - 1] = last with { End = last.End + 1 };
                            t += 1;
                        }
                        break;
                    default:
                        clock = null;
                        errors.Add(new TimingError(s.Line, at, new I18nText("Unknown wave character “" + c + "” (0 1 x z = [value] p n .)", "Unbekanntes Zeichen „" + c + "“ im Signalverlauf (0 1 x z = [Wert] p n .)")));
                        Add(WaveKind.Undefined, 1);
                        break;
                }
            }
            if (t >= MaxSteps && segs.Count > 0)
            {
                var last = segs[segs.Count - 1];
                if (last.End > MaxSteps) segs[segs.Count - 1] = last with { End = MaxSteps };
            }
        }

        /// <summary>Clocks get their segments over the whole diagram; other waves are cut or extended to <paramref name="steps"/>.</summary>
        private static void Complete(TimingSignal s, int steps)
        {
            var segs = s.Segments;
            if (s.IsClock)
            {
                segs.Clear();
                var high = s.Period * s.Duty;
                var first = s.Inverted ? WaveKind.Low : WaveKind.High;
                var second = s.Inverted ? WaveKind.High : WaveKind.Low;
                var firstLen = s.Inverted ? s.Period - high : high;
                for (double t = 0; t < steps - 1e-9 && segs.Count < MaxSegments; t += s.Period)
                {
                    var a = Math.Min(steps, t + firstLen);
                    segs.Add(new WaveSegment(first, t, a));
                    var b = Math.Min(steps, t + s.Period);
                    if (b > a + 1e-9) segs.Add(new WaveSegment(second, a, b));
                }
                return;
            }
            if (segs.Count == 0) { segs.Add(new WaveSegment(WaveKind.Undefined, 0, steps)); return; }
            segs.RemoveAll(x => x.Start >= steps - 1e-9);
            if (segs.Count == 0) { segs.Add(new WaveSegment(WaveKind.Undefined, 0, steps)); return; }
            var lastSeg = segs[segs.Count - 1];
            segs[segs.Count - 1] = lastSeg with { End = steps };
        }

        // ------------------------------------------------------------------ rewriting (actions, cleaning)

        /// <summary>
        /// The notation with names and data values rewritten (everything else kept as written): <paramref name="name"/>
        /// gets the signal index (0-based) and the old name, <paramref name="value"/> each bracketed data value.
        /// </summary>
        public static string Rewrite(string? text, Func<int, string, string>? name, Func<string, string>? value)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var k = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length == 0 || t[0] == '#') continue;
                var sep = t.IndexOfAny(new[] { ':', ';' });
                string head, sepText, wave;
                if (sep < 0) { head = ""; sepText = ""; wave = t; }
                else { head = t.Substring(0, sep).Trim(); sepText = t.Substring(sep, 1); wave = t.Substring(sep + 1).TrimStart(); }
                var newName = name is null ? head : name(k, head);
                var newWave = value is null || IsClock(wave) ? wave : RewriteValues(wave, value);
                lines[i] = sep < 0 && newName.Length == 0 ? newWave : newName + (sepText.Length > 0 ? sepText : ":") + " " + newWave;
                k++;
            }
            return string.Join("\n", lines);
        }

        private static string RewriteValues(string wave, Func<string, string> value)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < wave.Length; i++)
            {
                if (wave[i] != '[') { sb.Append(wave[i]); continue; }
                var end = wave.IndexOf(']', i + 1);
                if (end < 0) end = wave.Length;
                var v = wave.Substring(i + 1, Math.Max(0, end - i - 1)).Trim();
                sb.Append('[').Append(v.Length == 0 ? v : Clean(value(v))).Append(']');
                i = end;
            }
            return sb.ToString();
        }

        /// <summary>A data value safe for the notation (no brackets, row separators or line breaks).</summary>
        public static string Clean(string v) => new string((v ?? "").Where(c => c is not ('[' or ']' or '|' or '\n' or '\r')).ToArray()).Trim();

        /// <summary>A signal name safe for the notation (no separators).</summary>
        public static string CleanName(string v) => new string((v ?? "").Where(c => c is not (':' or ';' or '|' or '\n' or '\r' or '#')).ToArray()).Trim();

        // ------------------------------------------------------------------ markers and dimension arrows

        /// <summary>
        /// Markers "t1=2; t2=5.5" (label=time or label@time; a bare time gets the label t1, t2 …), separated by ';' or line
        /// breaks. Times in axis units (time steps times the time per step); German decimal commas are accepted.
        /// </summary>
        public static List<TimingMarker> Markers(string? text, List<TimingError>? errors = null)
        {
            var list = new List<TimingMarker>();
            var entries = (text ?? "").Split(new[] { ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var at = e.LastIndexOfAny(new[] { '=', '@' });
                var label = at < 0 ? "t" + Fmt.I(list.Count + 1) : e.Substring(0, at).Trim();
                var num = at < 0 ? e : e.Substring(at + 1).Trim();
                if (!ChartData.TryNumber(num, out var v) || !Fmt.Finite(v))
                {
                    errors?.Add(new TimingError(i + 1, 0, new I18nText("Marker “" + e + "” needs a time (e.g. t1=2)", "Markierung „" + e + "“ braucht eine Zeit (z. B. t1=2)")));
                    continue;
                }
                list.Add(new TimingMarker(label, v));
            }
            return list;
        }

        /// <summary>
        /// Dimension arrows "t1-t2" or "t1-t2: t_su" between markers (by label, or by number 1, 2 …), separated by ';' or
        /// line breaks; the text defaults to "Δt".
        /// </summary>
        public static List<TimingSpan> Spans(string? text, IReadOnlyList<TimingMarker> markers, List<TimingError>? errors = null)
        {
            var list = new List<TimingSpan>();
            var entries = (text ?? "").Split(new[] { ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var colon = e.IndexOf(':');
                var label = colon < 0 ? "Δt" : e.Substring(colon + 1).Trim();
                if (label.Length == 0) label = "Δt";
                var refs = colon < 0 ? e : e.Substring(0, colon);
                var parts = refs.Split(new[] { "..", "–", "-" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                int? a = parts.Count == 2 ? Find(parts[0], markers) : null;
                int? b = parts.Count == 2 ? Find(parts[1], markers) : null;
                if (a is null || b is null || a == b)
                {
                    errors?.Add(new TimingError(i + 1, 0, new I18nText("Dimension arrow “" + e + "” needs two markers (e.g. t1-t2)", "Maßpfeil „" + e + "“ braucht zwei Markierungen (z. B. t1-t2)")));
                    continue;
                }
                list.Add(new TimingSpan(a.Value, b.Value, label));
            }
            return list;
        }

        private static int? Find(string r, IReadOnlyList<TimingMarker> markers)
        {
            for (var i = 0; i < markers.Count; i++)
                if (string.Equals(markers[i].Label, r, StringComparison.OrdinalIgnoreCase)) return i;
            if (int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= markers.Count) return n - 1;
            return null;
        }

        /// <summary>Markers written back as "label=time; …" (invariant numbers).</summary>
        public static string FormatMarkers(IEnumerable<TimingMarker> markers)
            => string.Join("; ", markers.Select(m => m.Label + "=" + m.Time.ToString("0.######", CultureInfo.InvariantCulture)));
    }
}
