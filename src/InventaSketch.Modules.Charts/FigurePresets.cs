using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Library presets of the figure chart (the check figures of docs/en/design/charts-v2.md), built as
    /// <see cref="FigureSpec"/> objects and stored as JSON: the patent figure with phases and rotor temperature, the timing
    /// diagram of a decision logic and a temperature curve with activation thresholds. The curves are qualitative
    /// (first-order lags, a Gaussian bump, rectangle pulses) chosen to show the figure elements, not measured data.
    /// </summary>
    internal static class FigurePresets
    {
        public static readonly string Patent = BuildPatent().ToJson();
        public static readonly string Timing = BuildTiming().ToJson();
        public static readonly string Temperature = BuildTemperature().ToJson();

        /// <summary>Specification of a figure inserted without parameters: the patent figure.</summary>
        public static string Default => Patent;

        private static I18nText T(string en, string de) => new(en, de);

        private static double R(double v) => Math.Round(v, 4);

        /// <summary>Samples <paramref name="f"/> from <paramref name="a"/> to <paramref name="b"/> every <paramref name="step"/>, plus the extra x values.</summary>
        private static (List<double> X, List<double> Y) Sample(Func<double, double> f, double a, double b, double step, params double[] extra)
        {
            var xs = new List<double>();
            for (var i = 0; a + i * step <= b + 1e-9; i++) xs.Add(R(a + i * step));
            xs.AddRange(extra.Where(x => x > a && x < b).Select(R));
            xs = xs.Distinct().OrderBy(x => x).ToList();
            return (xs, xs.Select(x => R(f(x))).ToList());
        }

        private static FigSeries Curve((List<double> X, List<double> Y) pts, int panel, string line) => new() { X = pts.X, Y = pts.Y, Panel = panel, Line = line };

        /// <summary>Joins pieces with a gap (null in the JSON) between them.</summary>
        private static (List<double> X, List<double> Y) Gap(params (List<double> X, List<double> Y)[] pieces)
        {
            var x = new List<double>(); var y = new List<double>();
            for (var i = 0; i < pieces.Length; i++)
            {
                if (i > 0) { x.Add(double.NaN); y.Add(double.NaN); }
                x.AddRange(pieces[i].X); y.AddRange(pieces[i].Y);
            }
            return (x, y);
        }

        /// <summary>
        /// The maintainer's patent figure: power P with the phases "pre-conditioning" and "full load" (phase lines through
        /// both panels); rotor temperature without (dashed) and with pre-conditioning (solid) up to the limit temperature,
        /// the region between them hatched, the time gained as a dimension arrow, reference numerals 40–46.
        /// </summary>
        private static FigureSpec BuildPatent()
        {
            const double t0 = 1, t1 = 4, limit = 0.85, tau = 3;
            // Without pre-conditioning: constant until full load, then a first-order rise up to the limit (held there).
            double Plain(double t) => t <= t1 ? 0.5 : Math.Min(limit, 0.5 + 0.5 * (1 - Math.Exp(-(t - t1) / tau)));
            // With pre-conditioning: cooled down from t0, so it reaches the limit later.
            var cooled = 0.3 + 0.2 * Math.Exp(-(t1 - t0));
            double Pre(double t) => t <= t0 ? 0.5 : t <= t1 ? 0.3 + 0.2 * Math.Exp(-(t - t0)) : Math.Min(limit, cooled + (1 - cooled) * (1 - Math.Exp(-(t - t1) / tau)));
            var ta = t1 + tau * Math.Log(0.5 / (1 - limit));
            var tb = t1 + tau * Math.Log((1 - cooled) / (1 - limit));
            var s = new FigureSpec
            {
                Scheme = "mono",
                X = new FigAxis { Mode = "principle", Min = 0, Max = 10, Label = T("Time t", "Zeit t"), Marks = { new FigMark(t0, "t0"), new FigMark(t1, "t1") } },
                Panels =
                {
                    new FigPanel { Mode = "principle", Min = 0, Max = 1.3, Label = T("Power P", "Leistung P") },
                    new FigPanel { Mode = "principle", Min = 0, Max = 1.15, Share = 1.5, Label = T("Rotor temp. T", "Rotortemperatur T") },
                },
            };
            s.Series.Add(new FigSeries
            {
                X = { 0, t0, t0, t1, t1, 10 }, Y = { 0.12, 0.12, 0.35, 0.35, 1, 1 }, Line = "solid",
                Numeral = new FigNumeral { Text = "40", At = 9, Dx = 0.9, Dy = 1.6 },
            });
            var plain = Curve(Sample(Plain, 0, 10, 0.1, ta), 1, "dash");
            plain.Numeral = new FigNumeral { Text = "43", At = 2.5, Dx = 0, Dy = -1.6 };
            s.Series.Add(plain);
            var pre = Curve(Sample(Pre, 0, 10, 0.1, tb), 1, "solid");
            pre.Numeral = new FigNumeral { Text = "44", At = 2.5, Dx = 0, Dy = 1.6 };
            s.Series.Add(pre);
            // As usual in patent figures, the reference numerals name the curves (no text labels).

            s.Refs.Add(new FigRef { Kind = "v", X = t0, Line = "dash", Numeral = new FigNumeral { Text = "41", At = 0.75, Panel = 0, Dx = -1.3, Dy = -0.9 } });
            s.Refs.Add(new FigRef { Kind = "v", X = t1, Line = "dash", Numeral = new FigNumeral { Text = "42", At = 0.7, Panel = 0, Dx = 1.3, Dy = -0.9 } });
            s.Refs.Add(new FigRef
            {
                Kind = "h", Y = limit, Panel = 1, Line = "dashdot", Label = T("limit temperature", "Grenztemperatur"), LabelPos = "left",
                Numeral = new FigNumeral { Text = "46", At = 3.4, Dx = 0.6, Dy = -1.3 },
            });
            s.Regions.Add(new FigRegion { Kind = "between", A = 1, B = 2, Above = true, Numeral = new FigNumeral { Text = "45", X = 6, Y = 0.69, Panel = 1, Dx = 1.6, Dy = 1.6 } });
            s.Notes.Add(new FigNote { Kind = "text", P1 = new FigPoint { X = (t0 + t1) / 2, Y = 1.15 }, Text = T("pre-conditioning", "Vorkonditionierung"), MaxWidth = t1 - t0 - 0.1 });
            s.Notes.Add(new FigNote { Kind = "text", P1 = new FigPoint { X = (t1 + 10) / 2, Y = 1.15 }, Text = T("full load", "Volllastphase"), MaxWidth = 10 - t1 - 0.2 });
            s.Notes.Add(new FigNote
            {
                Kind = "dim", P1 = new FigPoint { X = R(ta), Y = 0.72, Panel = 1 }, P2 = new FigPoint { X = R(tb), Y = 0.72, Panel = 1 },
                Text = T("time gained", "Zeitgewinn"), Side = "below",
            });
            s.Notes.Add(new FigNote { Kind = "line", P1 = new FigPoint { X = R(ta), Y = limit, Panel = 1 }, P2 = new FigPoint { X = R(ta), Y = 0.69, Panel = 1 } });
            s.Notes.Add(new FigNote { Kind = "line", P1 = new FigPoint { X = R(tb), Y = limit, Panel = 1 }, P2 = new FigPoint { X = R(tb), Y = 0.69, Panel = 1 } });
            return s;
        }

        /// <summary>
        /// "Fig. 2 Timing of the decision logic": maturity mass rising towards a dashed limit (numeral 64 at the axis, 63 on
        /// the curve); heat demand with a bump and drive loss heat, hatched only where the demand is higher (65–67); sampling
        /// signal with a solid (68) and a dashed pulse (69); t_krit as a marker through all panels and as a time arrow.
        /// </summary>
        private static FigureSpec BuildTiming()
        {
            const double tk = 6;
            var s = new FigureSpec
            {
                Scheme = "mono",
                Title = T("Fig. 2 Timing of the decision logic", "Fig. 2 Zeitlicher Ablauf der Entscheidungslogik"),
                PanelGap = 3.2,
                X = new FigAxis { Mode = "principle", Min = 0, Max = 10, Label = T("Time t", "Zeit t"), Marks = { new FigMark(tk, "t_krit") } },
                Panels =
                {
                    new FigPanel { Mode = "principle", Min = 0, Max = 1.25, Label = T("Maturity mass m", "Reifemasse m") },
                    new FigPanel { Mode = "principle", Min = 0, Max = 1.0, Label = T("Power P", "Leistung P") },
                    new FigPanel { Mode = "principle", Min = 0, Max = 1.3, Label = T("Sampling signal s", "Abtastsignal s") },
                },
            };
            var norm = 1 - Math.Exp(-tk / 4);
            var maturity = Sample(t => 0.9 * (1 - Math.Exp(-t / 4)) / norm, 0, 10, 0.2);
            s.Series.Add(new FigSeries
            {
                X = maturity.X, Y = maturity.Y, Line = "solid", Numeral = new FigNumeral { Text = "63", At = 3, Dx = 1.2, Dy = 1.3 },
            });
            var demand = Curve(Sample(t => 0.25 + 0.55 * Math.Exp(-Math.Pow((t - 4) / 1.4, 2)), 0, 10, 0.2), 1, "solid");
            demand.Label = T("heat demand", "Wärmebedarf"); demand.LabelAt = 4; demand.LabelPos = "above";
            demand.Numeral = new FigNumeral { Text = "65", At = 5.2, Dx = 1.3, Dy = -1.0 };
            s.Series.Add(demand);
            var loss = Curve(Sample(t => 0.4 + 0.02 * t, 0, 10, 0.5), 1, "dash");
            loss.Label = T("drive loss heat", "Antriebsverlustwärme"); loss.LabelAt = 8.4; loss.LabelPos = "above";
            loss.Numeral = new FigNumeral { Text = "66", At = 9.4, Dx = 0, Dy = 1.4 };
            s.Series.Add(loss);
            s.Series.Add(new FigSeries
            {
                X = { 0, tk, tk, 7.5, 7.5, 10 }, Y = { 0, 0, 1, 1, 0, 0 }, Panel = 2, Line = "solid",
                Numeral = new FigNumeral { Text = "68", At = 6.75, Dx = 1.0, Dy = -1.0 },
            });
            s.Series.Add(new FigSeries
            {
                X = { 8, 8, 9.5, 9.5 }, Y = { 0, 1, 1, 0 }, Panel = 2, Line = "dash",
                Numeral = new FigNumeral { Text = "69", At = 8.75, Dx = 1.0, Dy = -1.0 },
            });
            s.Refs.Add(new FigRef { Kind = "h", Y = 0.9, Panel = 0, Line = "dash", Label = T("limit", "Grenzwert"), LabelPos = "left", Numeral = new FigNumeral { Text = "64", Axis = true } });
            s.Refs.Add(new FigRef { Kind = "v", X = tk, Line = "dash" });
            s.Regions.Add(new FigRegion { Kind = "between", A = 1, B = 2, Above = true, Numeral = new FigNumeral { Text = "67", X = 4.2, Y = 0.6, Panel = 1, Dx = -2.0, Dy = 2.4 } });
            s.Notes.Add(new FigNote
            {
                Kind = "dim", P1 = new FigPoint { X = 0, Gap = 0.55, Panel = 0 }, P2 = new FigPoint { X = tk, Gap = 0.55, Panel = 0 }, Text = new I18nText("t_krit"), Side = "above",
            });
            return s;
        }

        /// <summary>
        /// Temperature over time: data axes (0–200 s, 0–220 °C) with a dotted horizontal grid, dashed reference lines with
        /// labels, two first-order curves (one fast towards 160 °C, one slow towards 110 °C) with a gap each, open circle
        /// markers where they reach activation 14b ("31 s", "77 s"), a vertical dimension "35 K" and a caption.
        /// </summary>
        private static FigureSpec BuildTemperature()
        {
            var tauFast = 31 / Math.Log(140.0 / 65);
            var tauSlow = 77 / Math.Log(90.0 / 15);
            double Fast(double t) => 20 + 140 * (1 - Math.Exp(-t / tauFast));
            double Slow(double t) => 20 + 90 * (1 - Math.Exp(-t / tauSlow));
            var s = new FigureSpec
            {
                Caption = T("Fig. 3 Temperature over time with activation thresholds", "Fig. 3 Temperaturverlauf mit Aktivierungsschwellen"),
                GridStyle = "dot",
                X = new FigAxis { Min = 0, Max = 200, Label = T("Time t", "Zeit t"), Unit = new I18nText("s"), Grid = false },
                Panels = { new FigPanel { Min = 0, Max = 220, Label = T("Temperature T", "Temperatur T"), Unit = new I18nText("°C"), Grid = true } },
            };
            var fast = Curve(Gap(Sample(Fast, 0, 96, 4, 31), Sample(Fast, 124, 200, 4)), 0, "solid");
            fast.Label = T("fast", "schnell"); fast.LabelAt = 110; fast.LabelPos = "on";
            s.Series.Add(fast);
            var slow = Curve(Gap(Sample(Slow, 0, 124, 4, 77), Sample(Slow, 152, 200, 4)), 0, "dash");
            slow.Label = T("slow", "langsam"); slow.LabelAt = 138; slow.LabelPos = "on";
            s.Series.Add(slow);
            s.Refs.Add(new FigRef { Kind = "h", Y = 210, Label = T("heating ram (15)", "Heizstempel (15)") });
            s.Refs.Add(new FigRef { Kind = "h", Y = 150, Label = T("activation 14a", "Aktivierung 14a"), LabelPos = "left" });
            s.Refs.Add(new FigRef { Kind = "h", Y = 95, Label = T("activation 14b", "Aktivierung 14b"), LabelPos = "below" });
            s.Refs.Add(new FigRef { Kind = "h", Y = 60, Label = T("operating maximum 60 °C", "Betriebsmaximum 60 °C") });
            s.Refs.Add(new FigRef { Kind = "point", Series = 0, X = 31, Label = new I18nText("31 s"), LabelPos = "left" });
            s.Refs.Add(new FigRef { Kind = "point", Series = 1, X = 77, Label = new I18nText("77 s"), LabelPos = "above" });
            s.Notes.Add(new FigNote
            {
                Kind = "dim", P1 = new FigPoint { X = 110, Y = 60 }, P2 = new FigPoint { X = 110, Y = 95 }, Text = new I18nText("35 K"), Side = "right",
            });
            return s;
        }
    }
}
