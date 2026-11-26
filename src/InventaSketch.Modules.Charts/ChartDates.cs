using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Calendar dates in chart data (Gantt chart): ISO dates (yyyy-mm-dd) and German dates (dd.mm.yyyy) are read as day
    /// numbers, days since 1970-01-01 (the date numbers of matplotlib 3.3 and later), so durations are plain numbers of days.
    /// A date axis gets ticks on days, weeks (Mondays), months or years with date labels.
    /// </summary>
    internal static class ChartDates
    {
        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly Regex IsoDate = new(@"(?<![\d.\-/])(\d{4})-(\d{1,2})-(\d{1,2})(?![\d\-/]|\.\d)", RegexOptions.CultureInvariant);
        private static readonly Regex GermanDate = new(@"(?<![\d.\-/])(\d{1,2})\.(\d{1,2})\.(\d{4})(?![\d\-/]|\.\d)", RegexOptions.CultureInvariant);

        private static readonly string[] MonthsEn = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        private static readonly string[] MonthsDe = { "Jan.", "Feb.", "März", "Apr.", "Mai", "Juni", "Juli", "Aug.", "Sept.", "Okt.", "Nov.", "Dez." };

        private static bool Valid(int y, int m, int d) => y >= 1 && y <= 9999 && m >= 1 && m <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m);

        private static double Day(int y, int m, int d) => (new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Unspecified) - Epoch).TotalDays;

        /// <summary>The text with every valid date replaced by its day number; <paramref name="any"/> is true if one was found.</summary>
        public static string ToDays(string? text, out bool any)
        {
            any = false;
            if (string.IsNullOrEmpty(text) || (text!.IndexOf('-') < 0 && text.IndexOf('.') < 0)) return text ?? "";
            var found = false;
            string Replace(Match m, bool iso)
            {
                int y, mo, d;
                if (iso) { y = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); mo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); d = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture); }
                else { d = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); mo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); y = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture); }
                if (!Valid(y, mo, d)) return m.Value;
                found = true;
                return ((long)Day(y, mo, d)).ToString(CultureInfo.InvariantCulture);
            }
            var result = IsoDate.Replace(text, m => Replace(m, true));
            result = GermanDate.Replace(result, m => Replace(m, false));
            any = found;
            return result;
        }

        /// <summary>True if <paramref name="token"/> is one date (ISO or German) and its day number.</summary>
        public static bool TryDay(string? token, out double day)
        {
            day = double.NaN;
            var t = (token ?? "").Trim();
            if (t.Length == 0) return false;
            var s = ToDays(t, out var any);
            if (!any || !long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)) return false;
            day = v;
            return true;
        }

        private static DateTime Date(double day) => Epoch.AddDays(Math.Floor(Geo.Clamp(day, -719000, 2900000) + 1e-9));

        /// <summary>The day number as an ISO date (yyyy-mm-dd).</summary>
        public static string Iso(double day) => Date(Math.Round(day)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static int MonthIndex(DateTime d) => d.Year * 12 + d.Month - 1;

        private static double FromMonthIndex(long m) => Day((int)(m / 12), (int)(m % 12) + 1, 1);

        /// <summary>Label of a tick: day steps "Nov 2" / "2.11.", month steps "Nov" / "Nov." (with the year in January and at the first tick), years "2026".</summary>
        private static string Label(double day, string unit, bool first, Lang lang)
        {
            var d = Date(day);
            var months = lang == Lang.De ? MonthsDe : MonthsEn;
            return unit switch
            {
                "day" => lang == Lang.De
                    ? d.Day.ToString(CultureInfo.InvariantCulture) + "." + d.Month.ToString(CultureInfo.InvariantCulture) + "." + (first || d.Month == 1 && d.Day <= 7 ? d.Year.ToString(CultureInfo.InvariantCulture) : "")
                    : months[d.Month - 1] + " " + d.Day.ToString(CultureInfo.InvariantCulture) + (first || d.Month == 1 && d.Day <= 7 ? ", " + d.Year.ToString(CultureInfo.InvariantCulture) : ""),
                "month" => months[d.Month - 1] + (first || d.Month == 1 ? " " + d.Year.ToString(CultureInfo.InvariantCulture) : ""),
                _ => d.Year.ToString(CultureInfo.InvariantCulture),
            };
        }

        /// <summary>
        /// Date ticks over the days <paramref name="lo"/> … <paramref name="hi"/>: the finest step of 1, 2, 7 (Mondays) or 14 days,
        /// 1, 2, 3 or 6 months, or 1, 2, 5, 10 … years with at most seven intervals. <paramref name="tickLo"/> and
        /// <paramref name="tickHi"/> are the enclosing ticks (an automatic axis range); the ticks returned lie in that range. The
        /// first tick of the axis (the first one, or with a <paramref name="fixedRange"/> the first not before <paramref name="lo"/>)
        /// names the year.
        /// </summary>
        public static IReadOnlyList<Tick> Ticks(double lo, double hi, Lang lang, out double tickLo, out double tickHi, bool fixedRange = false)
        {
            if (!Fmt.Finite(lo) || !Fmt.Finite(hi)) { lo = 0; hi = 1; }
            if (hi < lo) (lo, hi) = (hi, lo);
            lo = Geo.Clamp(lo, -719000, 2900000); hi = Geo.Clamp(hi, -719000, 2900000);
            if (hi - lo < 1) hi = lo + 1;
            List<double> seq;
            // Day steps (weeks start on Mondays: 1970-01-05 is day 4).
            foreach (var step in new[] { 1, 2, 7, 14 })
            {
                var off = step >= 7 ? 4 : 0;
                var a = Math.Floor((lo - off) / step + 1e-9) * step + off;
                var b = Math.Ceiling((hi - off) / step - 1e-9) * step + off;
                if ((b - a) / step <= 7)
                {
                    seq = new List<double>();
                    for (var v = a; v <= b + 1e-9; v += step) seq.Add(v);
                    return Make(seq, "day", fixedRange ? lo : seq[0], lang, out tickLo, out tickHi);
                }
            }
            // Month and year steps.
            long m0 = MonthIndex(Date(lo)), m1 = MonthIndex(Date(hi));
            if (FromMonthIndex(m1) < hi - 1e-9) m1++;
            var steps = new List<long> { 1, 2, 3, 6 };
            for (long y = 12; y <= 12L * 100000; y *= 10) { steps.Add(y); steps.Add(2 * y); steps.Add(5 * y); }
            foreach (var step in steps)
            {
                var a = (long)Math.Floor((double)m0 / step) * step;
                var b = (long)Math.Ceiling((double)m1 / step) * step;
                if ((b - a) / step > 7) continue;
                seq = new List<double>();
                for (var m = a; m <= b; m += step) seq.Add(FromMonthIndex(m));
                return Make(seq, step < 12 ? "month" : "year", fixedRange ? lo : seq[0], lang, out tickLo, out tickHi);
            }
            tickLo = lo; tickHi = hi;
            return Array.Empty<Tick>();
        }

        /// <summary>The ticks with labels; the first tick not before <paramref name="from"/> names the year.</summary>
        private static IReadOnlyList<Tick> Make(List<double> seq, string unit, double from, Lang lang, out double tickLo, out double tickHi)
        {
            tickLo = seq[0]; tickHi = seq[seq.Count - 1];
            if (tickHi <= tickLo) tickHi = tickLo + 1;
            var first = seq.FindIndex(v => v >= from - 1e-9);
            return seq.Select((v, i) => new Tick(v, TickMark.Major, Label(v, unit, i == first, lang))).ToList();
        }
    }
}
