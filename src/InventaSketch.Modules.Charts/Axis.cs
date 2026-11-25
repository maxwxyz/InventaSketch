using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    public enum AxisKind { Linear, Log, Category }

    public enum TickMark { None, Minor, Major }

    /// <summary>A tick of an axis: value, mark size and label (null = no label).</summary>
    public readonly record struct Tick(double Value, TickMark Mark, string? Label)
    {
        /// <summary>Major ticks get a grid line.</summary>
        public bool IsMajor => Mark == TickMark.Major;
    }

    /// <summary>
    /// Axis scale with tick generation. Linear axes use "nice" steps (1, 2, 2.5, 5 × 10ⁿ, or explicit steps such as 45° for
    /// phase); log axes put major ticks on decades and minor ticks on 2 … 9 × 10ⁿ (labelled at 2 and 5 when the axis spans
    /// at most two decades); category axes divide the range into equal bands, one per category.
    /// </summary>
    public sealed class Axis
    {
        private Axis(AxisKind kind, double min, double max, double step)
        {
            Kind = kind; Min = min; Max = max; Step = step;
        }

        public AxisKind Kind { get; }
        public double Min { get; }
        public double Max { get; }
        /// <summary>Major step of a linear axis (0 for log and category axes).</summary>
        public double Step { get; }
        /// <summary>Category labels (category axes only).</summary>
        public IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();
        /// <summary>True if larger values lie at the start (category axes running top-down).</summary>
        public bool Reversed { get; private set; }
        public bool IsLog => Kind == AxisKind.Log;

        // ------------------------------------------------------------------ axis breaks (charts v2 stage 3)

        private IReadOnlyList<AxisBreak> _breaks = Array.Empty<AxisBreak>();
        private BrokenAxis? _broken;
        private double _length = 100;

        /// <summary>Breaks of a linear axis (normalised: inside the range, sorted, merged); empty without breaks.</summary>
        public IReadOnlyList<AxisBreak> Breaks => _breaks;
        public bool HasBreaks => _breaks.Count > 0;
        /// <summary>Marker style of the breaks (set with <see cref="WithBreaks"/>).</summary>
        public BreakMarkerStyle BreakStyle { get; private set; } = BreakMarkerStyle.Zigzag;
        /// <summary>The broken mapping for the current length (null without breaks).</summary>
        public BrokenAxis? Broken => _broken;

        /// <summary>
        /// A copy of this linear axis with the value ranges <paramref name="ranges"/> compressed into marker bands of
        /// <paramref name="gap"/> mm (<see cref="AxisBreaks.RequiredGap"/> by default). The tick step is chosen for the visible
        /// span (about <paramref name="target"/> steps; null keeps the step). Log and category axes, and ranges outside the axis,
        /// are returned unchanged.
        /// </summary>
        public Axis WithBreaks(IEnumerable<(double Lo, double Hi)> ranges, BreakMarkerStyle style = BreakMarkerStyle.Zigzag, double? gap = null, int? target = 5)
        {
            if (Kind != AxisKind.Linear) return this;
            var g = gap ?? AxisBreaks.RequiredGap(style, AxisBreaks.AxisMarkerWidth);
            var list = ranges.Where(r => Fmt.Finite(r.Lo) && Fmt.Finite(r.Hi)).Select(r => new AxisBreak(Math.Min(r.Lo, r.Hi), Math.Max(r.Lo, r.Hi), g)).ToList();
            if (list.Count == 0) return this;
            var probe = new BrokenAxis(Min, Max, _length, list);
            // A break that would hide (almost) the whole axis is dropped.
            if (probe.Breaks.Count == 0 || probe.VisibleRanges.Sum(v => v.Hi - v.Lo) < (Max - Min) * 0.02) return this;
            var visible = probe.VisibleRanges.Sum(v => v.Hi - v.Lo);
            var step = target is int tg ? Math.Min(Step > 0 ? Step : double.MaxValue, NiceStep(visible / Math.Max(1, tg))) : Step;
            var a = new Axis(Kind, Min, Max, step) { Categories = Categories, Reversed = Reversed, BreakStyle = style, _breaks = probe.Breaks, _length = _length };
            a._broken = probe;
            return a;
        }

        /// <summary>Length of the axis in mm (set by the layout; the break bands have a fixed length in mm).</summary>
        internal void SetLength(double mm)
        {
            if (!(mm > 0) || !Fmt.Finite(mm)) return;
            _length = mm;
            if (_breaks.Count > 0) _broken = new BrokenAxis(Min, Max, mm, _breaks);
        }

        /// <summary>True if <paramref name="v"/> lies strictly inside a break (hidden).</summary>
        public bool InBreak(double v) => _broken is not null && _broken.BreakIndexOf(v) >= 0;

        /// <summary>Within the range and not hidden by a break.</summary>
        public bool Visible(double v) => Contains(v) && !InBreak(v);

        /// <summary>Normalised position 0 … 1 of <paramref name="v"/> (outside the range: below 0 / above 1; NaN for v ≤ 0 on log axes).</summary>
        public double T(double v)
        {
            if (_broken is not null) return _broken.T(v);
            double t;
            if (Kind == AxisKind.Log)
            {
                if (!(v > 0)) return double.NaN;
                t = (Math.Log10(v) - Math.Log10(Min)) / (Math.Log10(Max) - Math.Log10(Min));
            }
            else t = (v - Min) / (Max - Min);
            return Reversed ? 1 - t : t;
        }

        /// <summary>Value at the normalised position <paramref name="t"/> (inverse of <see cref="T"/>; sketched curves).</summary>
        public double V(double t)
        {
            if (_broken is not null) return _broken.Value(t * _broken.Length);
            if (Reversed) t = 1 - t;
            return Kind == AxisKind.Log
                ? Math.Pow(10, Math.Log10(Min) + t * (Math.Log10(Max) - Math.Log10(Min)))
                : Min + t * (Max - Min);
        }

        /// <summary>True if <paramref name="v"/> lies within the axis range (tolerant to rounding).</summary>
        public bool Contains(double v)
        {
            var t = T(v);
            return Fmt.Finite(t) && t >= -1e-9 && t <= 1 + 1e-9;
        }

        /// <summary>Number of decimals of the tick labels of a linear axis.</summary>
        public int Decimals
        {
            get
            {
                if (Kind != AxisKind.Linear || Step <= 0) return 0;
                for (var d = 0; d < 10; d++)
                {
                    var s = Step * Math.Pow(10, d);
                    if (Math.Abs(s - Math.Round(s)) < 1e-6 * Math.Max(1, s)) return d;
                }
                return 10;
            }
        }

        /// <summary>Ticks with labels in the given language (decimal comma in German).</summary>
        public IReadOnlyList<Tick> Ticks(Lang lang)
        {
            var list = new List<Tick>();
            switch (Kind)
            {
                case AxisKind.Linear:
                    {
                        var d = Decimals;
                        var first = Math.Ceiling(Min / Step - 1e-9);
                        for (var i = first; i * Step <= Max + Step * 1e-9 && list.Count < 200; i++)
                        {
                            var v = i * Step;
                            if (Math.Abs(v) < Step * 1e-9) v = 0;
                            if (InBreak(v)) continue; // tick labels skip hidden ranges
                            list.Add(new Tick(v, TickMark.Major, Fmt.Num(v, lang, d)));
                        }
                        break;
                    }
                case AxisKind.Log:
                    {
                        var lo = (int)Math.Floor(Math.Log10(Min) + 1e-9);
                        var hi = (int)Math.Ceiling(Math.Log10(Max) - 1e-9);
                        var decades = Math.Max(1, hi - lo);
                        var every = Math.Max(1, (int)Math.Ceiling(decades / 8.0));
                        var labelMinor = decades <= 2;
                        for (var e = lo; e <= hi; e++)
                        {
                            var dec = Math.Pow(10, e);
                            if (Contains(dec)) list.Add(new Tick(dec, TickMark.Major, (e - lo) % every == 0 ? LogLabel(dec, lang) : null));
                            if (e == hi) break;
                            for (var m = 2; m <= 9; m++)
                            {
                                var v = m * dec;
                                if (Contains(v)) list.Add(new Tick(v, TickMark.Minor, labelMinor && (m == 2 || m == 5) ? LogLabel(v, lang) : null));
                            }
                        }
                        break;
                    }
                case AxisKind.Category:
                    for (var i = 0; i <= Categories.Count; i++)
                    {
                        list.Add(new Tick(i, TickMark.Major, null));
                        if (i < Categories.Count) list.Add(new Tick(i + 0.5, TickMark.None, Categories[i]));
                    }
                    break;
            }
            return list;
        }

        /// <summary>Log axis label: plain number, SI prefix from 10 000 ("10k", "1M") and exponent form below 0.001.</summary>
        public static string LogLabel(double v, Lang lang)
        {
            var a = Math.Abs(v);
            if (a >= 1e4)
            {
                var (div, p) = a >= 1e9 ? (1e9, "G") : a >= 1e6 ? (1e6, "M") : (1e3, "k");
                return Fmt.Num(v / div, lang, 2) + p;
            }
            if (a > 0 && a < 1e-3)
            {
                var e = (int)Math.Floor(Math.Log10(a) + 1e-9);
                var m = v / Math.Pow(10, e);
                return (Math.Abs(m - 1) < 1e-9 ? "" : Fmt.Num(m, lang, 2) + "·") + "1e" + Fmt.I(e);
            }
            return Fmt.Num(v, lang, 4);
        }

        /// <summary>
        /// Linear axis over <paramref name="data"/>. Without a fixed range the bounds are extended to the next nice step
        /// (and to 0 with <paramref name="includeZero"/>); a fixed range is used as given. <paramref name="steps"/> lists
        /// allowed absolute steps (e.g. 15, 30, 45, 90 for phase); otherwise 1, 2, 2.5, 5 × 10ⁿ.
        /// </summary>
        public static Axis Linear(IEnumerable<double> data, (double Min, double Max)? range = null, bool includeZero = false, int target = 5, IReadOnlyList<double>? steps = null)
        {
            double lo, hi;
            if (range is { } r && r.Max > r.Min) { lo = r.Min; hi = r.Max; }
            else
            {
                var vals = data.Where(Fmt.Finite).ToList();
                if (includeZero) vals.Add(0);
                if (vals.Count == 0) { lo = 0; hi = 1; }
                else { lo = vals.Min(); hi = vals.Max(); }
                if (hi - lo < 1e-12 * Math.Max(1, Math.Abs(hi)))
                {
                    var span = Math.Abs(lo) > 1e-12 ? Math.Abs(lo) * 0.5 : 1;
                    lo -= span; hi += span;
                    if (includeZero && lo < 0 && hi > 0 && vals.All(v => v >= 0)) lo = 0;
                }
            }
            var step = NiceStep((hi - lo) / Math.Max(1, target), steps);
            if (range is null || !(range.Value.Max > range.Value.Min))
            {
                lo = Math.Floor(lo / step + 1e-9) * step;
                hi = Math.Ceiling(hi / step - 1e-9) * step;
                if (hi <= lo) hi = lo + step;
            }
            return new Axis(AxisKind.Linear, lo, hi, step);
        }

        /// <summary>Log axis over the positive values of <paramref name="data"/>, from decade to decade unless a positive range is fixed.</summary>
        public static Axis Logarithmic(IEnumerable<double> data, (double Min, double Max)? range = null)
        {
            if (range is { } r && r.Min > 0 && r.Max > r.Min) return new Axis(AxisKind.Log, r.Min, r.Max, 0);
            var vals = data.Where(v => Fmt.Finite(v) && v > 0).ToList();
            if (vals.Count == 0) return new Axis(AxisKind.Log, 1, 100, 0);
            var lo = Math.Pow(10, Math.Floor(Math.Log10(vals.Min()) + 1e-9));
            var hi = Math.Pow(10, Math.Ceiling(Math.Log10(vals.Max()) - 1e-9));
            if (hi <= lo * 1.000001) hi = lo * 10;
            return new Axis(AxisKind.Log, lo, hi, 0);
        }

        /// <summary>Linear or log axis.</summary>
        public static Axis Of(bool log, IEnumerable<double> data, (double Min, double Max)? range = null, bool includeZero = false, int target = 5)
            => log ? Logarithmic(data, range) : Linear(data, range, includeZero, target);

        /// <summary>Category axis with one band per label (<paramref name="reversed"/>: first category at the end, e.g. at the top).</summary>
        public static Axis Category(IReadOnlyList<string> labels, bool reversed = false)
            => new(AxisKind.Category, 0, Math.Max(1, labels.Count), 0) { Categories = labels.ToList(), Reversed = reversed };

        /// <summary>Smallest allowed step not below <paramref name="raw"/>.</summary>
        public static double NiceStep(double raw, IReadOnlyList<double>? steps = null)
        {
            if (!(raw > 0) || !Fmt.Finite(raw)) return 1;
            if (steps is { Count: > 0 })
            {
                foreach (var s in steps.OrderBy(x => x)) if (s >= raw * (1 - 1e-9)) return s;
                var top = steps.Max();
                return Math.Ceiling(raw / top) * top;
            }
            var e = Math.Floor(Math.Log10(raw));
            var b = Math.Pow(10, e);
            foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
                if (m * b >= raw * (1 - 1e-9)) return m * b;
            return 10 * b;
        }
    }
}
