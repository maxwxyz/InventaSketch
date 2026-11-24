using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Torque envelope of a drive: constant torque up to the corner (base) speed, constant power above it.</summary>
    internal static class Drive
    {
        public static readonly ParamDef TMax = Prm.Num("tmax", "Maximum torque", "Maximales Drehmoment", 300, 0.001, 1e9, "Nm", 10);
        public static readonly ParamDef NBase = Prm.Num("nbase", "Corner speed (base speed)", "Eckdrehzahl (Nenndrehzahl)", 4000, 0.001, 1e9, "rpm", 100);
        public static readonly ParamDef NMax = Prm.Num("nmax", "Maximum speed", "Maximaldrehzahl", 12000, 0.001, 1e9, "rpm", 100);

        public static readonly I18nText SpeedLabel = new("Speed n", "Drehzahl n");
        public static readonly I18nText SpeedUnit = new("rpm", "1/min");
        public static readonly I18nText TorqueLabel = new("Torque T", "Drehmoment M");
        public static readonly I18nText TorqueUnit = new("Nm", "Nm");

        public static (double TMax, double NBase, double NMax) Read(SketchObject o)
        {
            var tmax = Math.Max(1e-6, o.Num("tmax", 300));
            var nmax = Math.Max(1e-6, o.Num("nmax", 12000));
            var nb = Geo.Clamp(o.Num("nbase", 4000), nmax * 0.02, nmax);
            return (tmax, nb, nmax);
        }

        /// <summary>Maximum torque at speed <paramref name="n"/>.</summary>
        public static double Torque(double n, double tmax, double nb) => n <= nb ? tmax : tmax * nb / n;

        /// <summary>The envelope as data points (constant part plus a sampled hyperbola).</summary>
        public static List<Vec2> Envelope(double tmax, double nb, double nmax, int samples = 40)
        {
            var list = new List<Vec2> { new(0, tmax), new(nb, tmax) };
            if (nmax > nb * 1.0001)
                for (var i = 1; i <= samples; i++)
                {
                    var n = nb + (nmax - nb) * i / samples;
                    list.Add(new Vec2(n, Torque(n, tmax, nb)));
                }
            return list;
        }

        /// <summary>Mechanical power in kW at torque <paramref name="t"/> (Nm) and speed <paramref name="n"/> (rpm).</summary>
        public static double PowerKw(double t, double n) => t * n * 2 * Math.PI / 60 / 1000;
    }

    /// <summary>
    /// Torque–speed characteristic of a motor: constant torque up to the corner speed, constant power (field weakening)
    /// up to the maximum speed; optional power curve on a secondary axis and region notes.
    /// </summary>
    internal sealed class TorqueChart : ChartType
    {
        public const string TypeId = "chart.torque";

        public static readonly ParamDef PowerDef = Prm.Bool("power", "Power curve", "Leistungskurve", true);
        public static readonly ParamDef RegionsDef = Prm.Bool("regions", "Region notes", "Bereichshinweise", true);

        private static readonly I18nText PowerLabel = new("Power P", "Leistung P");
        private static readonly I18nText ConstTorque = new("constant torque", "konstantes Moment");
        private static readonly I18nText ConstPower = new("constant power", "konstante Leistung");

        public TorqueChart() : base(TypeId, new I18nText("Torque–speed curve", "Drehmoment-Drehzahl-Kennlinie"), ChartCategories.Engineering,
            "torque", "speed", "motor", "characteristic", "field weakening", "power", "base speed", "corner speed", "e-machine",
            "drehmoment", "drehzahl", "kennlinie", "motorkennlinie", "feldschwächung", "leistung", "eckdrehzahl", "e-maschine")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            Drive.TMax, Drive.NBase, Drive.NMax, PowerDef, RegionsDef, Common.Title, Common.XLabel, Common.XUnit, Common.YLabel, Common.YUnit,
            Common.Grid, Common.Legend, Common.Box,
        };

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var (tmax, nb, nmax) = Drive.Read(o);
            var power = o.Flag("power", true);
            var pmax = Drive.PowerKw(tmax, nb);
            var x = Axis.Linear(new[] { 0, nmax }, includeZero: true);
            var y = Axis.Linear(new[] { 0, tmax * 1.08 }, includeZero: true);
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", Drive.TorqueLabel, Drive.TorqueUnit, lang));
            if (power)
            {
                panel.Y2 = Axis.Linear(new[] { 0, pmax * 1.08 }, includeZero: true);
                panel.Y2Title = Common.AxisTitle(o, "plabel-none", "punit-none", PowerLabel, new I18nText("kW"), lang);
            }
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", Drive.SpeedLabel, Drive.SpeedUnit, lang), panel);
            Common.Apply(f, o, ctx);
            var tLook = SeriesLook.Of(0);
            var pLook = SeriesLook.Of(1);
            f.Legend.Add(new LegendEntry(Drive.TorqueLabel.Get(lang), tLook.Line(0.6)));
            if (power) f.Legend.Add(new LegendEntry(PowerLabel.Get(lang), pLook.Line(0.5)));
            f.Layout(lang);
            var c = new Chart(f, lang);
            var a = panel.Area;

            if (o.Flag("regions", true) && nb < nmax * 0.98)
            {
                var top = f.P(panel, nb, y.Max);
                var bottom = f.P(panel, nb, 0);
                c.Lines.Add(Prim.Line(bottom, top, S.Helper) with { Tag = ChartTags.Helper });
                var fs = f.Fs * 0.8;
                var yNote = a.Bottom - Math.Min(a.H * 0.12, fs * 1.6);
                void Note(string text, double x0, double x1)
                {
                    var w = x1 - x0;
                    var size = Math.Min(fs, w * 0.9 / (Math.Max(1, text.Length) * Fmt.CharWidth));
                    if (size < 0.6) return;
                    c.Over.Add(new TextPrim(new Vec2((x0 + x1) / 2, yNote), text, new TextStyle { Size = size, Italic = true, Color = Palette.Helper }) { Tag = ChartTags.Note });
                    var hw = Fmt.Width(text, size) / 2;
                    c.Avoid.Add(new[] { new Vec2((x0 + x1) / 2 - hw, yNote), new Vec2((x0 + x1) / 2 + hw, yNote) });
                }
                Note(ConstTorque.Get(lang), a.X, bottom.X);
                Note(ConstPower.Get(lang), bottom.X, a.Right);
            }

            var env = Drive.Envelope(tmax, nb, nmax).Select(p => f.P(panel, p.X, p.Y)).ToList();
            if (Draw.Series(env, a, tLook.Line(0.6), false) is { } tl) c.Lines.Add(tl);
            c.Avoid.Add(env);
            if (power)
            {
                var pts = new List<Vec2> { f.P(panel, 0, 0, true), f.P(panel, nb, pmax, true), f.P(panel, nmax, pmax, true) };
                if (Draw.Series(pts, a, pLook.Line(0.5), false) is { } pl) c.Lines.Add(pl);
                c.Avoid.Add(pts);
                c.Port(f.P(panel, nmax, pmax, true), "pmax");
            }
            c.Port(f.P(panel, 0, tmax), "tmax");
            c.Port(f.P(panel, nb, tmax), "base");
            c.Port(f.P(panel, nmax, Drive.Torque(nmax, tmax, nb)), "end");
            return c;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Prm.Toggle(o, "chart.power", "power", true, new I18nText("Power curve", "Leistungskurve")),
            Prm.Toggle(o, "chart.regions", "regions", true, new I18nText("Region notes", "Bereichshinweise")),
            Common.GridToggle(o),
            Common.LegendCycle(o),
        };
    }

    /// <summary>
    /// Efficiency map: contour lines of the efficiency over speed and torque inside the torque envelope. The field comes
    /// from a qualitative loss model (constant, copper ∝ T², iron ∝ n, windage and eddy currents ∝ n³) scaled to the given peak
    /// efficiency; it illustrates the typical shape and is not measured data.
    /// </summary>
    internal sealed class EfficiencyChart : ChartType
    {
        public const string TypeId = "chart.efficiency";

        public const string DefaultLevels = "80;85;88;90;92;94;95";
        public static readonly ParamDef PeakDef = Prm.Num("etamax", "Peak efficiency", "Bestwirkungsgrad", 96, 1, 99.9, "%", 0.5);
        public static readonly ParamDef LevelsDef = Prm.Text("levels", "Contour levels in % (e.g. 80;90;95)", "Höhenlinien in % (z. B. 80;90;95)", DefaultLevels);
        public static readonly ParamDef LabelsDef = Prm.Bool("labels", "Contour labels", "Beschriftung der Linien", true);

        public EfficiencyChart() : base(TypeId, new I18nText("Efficiency map", "Wirkungsgradkennfeld"), ChartCategories.Engineering,
            "efficiency", "map", "contour", "motor", "e-machine", "operating range", "isolines",
            "wirkungsgrad", "kennfeld", "muscheldiagramm", "höhenlinien", "e-maschine", "betriebsbereich")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            Drive.TMax, Drive.NBase, Drive.NMax, PeakDef, LevelsDef, LabelsDef, Common.Title, Common.XLabel, Common.XUnit,
            Common.YLabel, Common.YUnit, Common.Grid, Common.Box,
        };

        /// <summary>Normalised losses at speed fraction <paramref name="s"/> and torque fraction <paramref name="t"/>.</summary>
        private static double Loss(double s, double t) => 0.005 + 0.15 * t * t + 0.01 * s + 0.16 * s * s * s;

        /// <summary>
        /// Efficiency field in % on a grid over (n, T) with the loss scale chosen so the maximum inside the envelope equals
        /// the peak efficiency. Returns the grid axes, the values and the location of the peak.
        /// </summary>
        public static (double[] Ns, double[] Ts, double[,] Eta, Vec2 Peak) Field(double tmax, double nb, double nmax, double peak, int nx = 49, int ny = 37)
        {
            var ns = Enumerable.Range(0, nx).Select(i => nmax * i / (nx - 1)).ToArray();
            var ts = Enumerable.Range(0, ny).Select(j => tmax * j / (ny - 1)).ToArray();
            double best = 0; var at = new Vec2(nb, tmax / 2);
            // Ratio P/L is independent of the loss scale: find its maximum inside the envelope (fine search plus the
            // drawing grid, so no grid value exceeds the peak).
            void Probe(double n, double t)
            {
                if (n <= 0 || t <= 0 || t > Drive.Torque(n, tmax, nb) + 1e-9) return;
                var r = (n / nmax) * (t / tmax) / Loss(n / nmax, t / tmax);
                if (r > best) { best = r; at = new Vec2(n, t); }
            }
            for (var i = 1; i <= 200; i++)
                for (var j = 1; j <= 100; j++) Probe(nmax * i / 200, tmax * j / 100);
            foreach (var n in ns) foreach (var t in ts) Probe(n, t);
            var eta = Geo.Clamp(peak, 1, 99.9) / 100;
            var k = best > 0 ? best * (1 - eta) / eta : 1;
            var v = new double[nx, ny];
            for (var i = 0; i < nx; i++)
                for (var j = 0; j < ny; j++)
                {
                    double s = ns[i] / nmax, t = ts[j] / tmax, p = s * t;
                    v[i, j] = p <= 0 ? 0 : 100 * p / (p + k * Loss(s, t));
                }
            return (ns, ts, v, at);
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var (tmax, nb, nmax) = Drive.Read(o);
            var peak = Geo.Clamp(o.Num("etamax", 96), 1, 99.9);
            var x = Axis.Linear(new[] { 0, nmax }, includeZero: true);
            var y = Axis.Linear(new[] { 0, tmax * 1.08 }, includeZero: true);
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", Drive.TorqueLabel, Drive.TorqueUnit, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", Drive.SpeedLabel, Drive.SpeedUnit, lang), panel);
            Common.Apply(f, o, ctx, legendDefault: "none");
            f.Layout(lang);
            var c = new Chart(f, lang);
            var a = panel.Area;
            var (ns, ts, eta, at) = Field(tmax, nb, nmax, peak);
            bool Inside(Vec2 d) => d.Y <= Drive.Torque(d.X, tmax, nb) * (1 + 1e-6) && d.X <= nmax * (1 + 1e-9);
            var levels = ChartData.Numbers(o.Str("levels", DefaultLevels)).Where(l => l > 0 && l < peak).Distinct().OrderBy(l => l).Take(20).ToList();
            var style = new Style { Stroke = Palette.Ink, StrokeWidth = 0.3, Role = LineRole.Visible };
            var labels = o.Flag("labels", true);
            var fs = f.Fs * 0.72;
            var placed = new List<Rect>();
            foreach (var level in levels)
            {
                var pieces = Contours.Trace(eta, ns, ts, level).SelectMany(l => Contours.Keep(l, Inside)).ToList();
                var mapped = pieces.Select(l => (IReadOnlyList<Vec2>)l.Select(p => f.P(panel, p.X, p.Y)).ToList()).ToList();
                if (Draw.MultiPath(mapped, style, ChartTags.Contour) is { } path) c.Lines.Add(path);
                if (!labels || mapped.Count == 0) continue;
                // One label per level on the longest piece, at the point farthest from labels already placed.
                var longest = mapped.OrderByDescending(Length).First();
                var text = Fmt.Num(level, lang, 1) + " %";
                var w = Fmt.Width(text, fs);
                Vec2? best = null; var bestScore = double.NegativeInfinity;
                for (var i = 1; i + 1 < longest.Count; i++)
                {
                    var p = longest[i];
                    var r = new Rect(p.X - w / 2 - fs * 0.15, p.Y - fs * 0.6, w + fs * 0.3, fs * 1.2);
                    if (!a.Contains(r)) continue;
                    var clearance = placed.Count == 0 ? 1e6 : placed.Min(q => q.Center.DistanceTo(r.Center));
                    var score = Math.Min(clearance, 1e6) - Math.Abs(i - longest.Count / 2.0) * 0.01;
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best is Vec2 bp && (placed.Count == 0 || placed.All(q => !q.Intersects(new Rect(bp.X - w / 2, bp.Y - fs * 0.6, w, fs * 1.2)))))
                {
                    Draw.Label(c.Over, bp, text, fs, ChartTags.ContourLabel);
                    placed.Add(new Rect(bp.X - w / 2 - fs * 0.15, bp.Y - fs * 0.6, w + fs * 0.3, fs * 1.2));
                }
            }
            var env = Drive.Envelope(tmax, nb, nmax).Select(p => f.P(panel, p.X, p.Y)).ToList();
            if (Draw.Series(env, a, SeriesLook.Of(0).Line(0.6) with { Stroke = Palette.Ink }, false, ChartTags.Envelope) is { } el) c.Lines.Add(el);
            var pk = f.P(panel, at.X, at.Y);
            c.Lines.Add(Draw.Markers(new[] { pk }, MarkerKind.Plus, false, Palette.Ink, f.Fs * 0.35));
            Draw.Label(c.Over, pk + new Vec2(f.Fs * 0.5, -f.Fs * 0.6), "η max " + Fmt.Num(peak, lang, 1) + " %", fs, ChartTags.Note, TextAnchor.Start, backing: false);
            c.Port(pk, "peak");
            c.Port(f.P(panel, nb, tmax), "base");
            c.Port(f.P(panel, nmax, Drive.Torque(nmax, tmax, nb)), "end");
            return c;
        }

        private static double Length(IReadOnlyList<Vec2> l)
        {
            double s = 0;
            for (var i = 0; i + 1 < l.Count; i++) s += l[i].DistanceTo(l[i + 1]);
            return s;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            ContextAction.WithInput("chart.levels", new I18nText("Contour levels…", "Höhenlinien…"), IconIds.Text,
                new I18nText("Efficiency levels in % (e.g. 80;90;95)", "Wirkungsgradstufen in % (z. B. 80;90;95)"), o.Str("levels", DefaultLevels),
                (x, text) =>
                {
                    if (ChartData.Numbers(text).Count == 0)
                        throw new FormatException(lang == Lang.De ? "Keine gültigen Werte: „" + text + "“" : "No valid values: “" + text + "”");
                    return x.WithParam("levels", text.Trim());
                }) with { Group = "data" },
            Prm.Toggle(o, "chart.labels", "labels", true, new I18nText("Contour labels", "Beschriftung der Linien")),
            Common.GridToggle(o),
        };
    }

    /// <summary>
    /// Pump or fan curve with system curve and operating point: H(Q) = H₀ (1 − (Q/Q_max)²), system curve through the static
    /// head and the operating point at the chosen flow, drop lines to both axes.
    /// </summary>
    internal sealed class PumpChart : ChartType
    {
        public const string TypeId = "chart.pump";

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Machine", "Maschine", "pump", ("pump", "Pump", "Pumpe"), ("fan", "Fan", "Ventilator"));
        public static readonly ParamDef H0Def = Prm.Num("h0", "Shut-off head / pressure", "Nullförderhöhe / Nulldruck", 40, 1e-6, 1e9, null, 1);
        public static readonly ParamDef QMaxDef = Prm.Num("qmax", "Maximum flow", "Maximaler Förderstrom", 100, 1e-6, 1e9, null, 1);
        public static readonly ParamDef HStatDef = Prm.Num("hstat", "Static head / pressure of the system", "Statischer Anteil der Anlage", 10, 0, 1e9, null, 1);
        public static readonly ParamDef QOpDef = Prm.Num("qop", "Flow at the operating point", "Förderstrom im Betriebspunkt", 70, 1e-6, 1e9, null, 1);
        public static readonly ParamDef SystemDef = Prm.Bool("system", "System curve", "Anlagenkennlinie", true);

        private static readonly I18nText PumpCurve = new("Pump curve", "Pumpenkennlinie");
        private static readonly I18nText FanCurve = new("Fan curve", "Ventilatorkennlinie");
        private static readonly I18nText SystemCurve = new("System curve", "Anlagenkennlinie");
        private static readonly I18nText OpShort = new("OP", "BP");

        public PumpChart() : base(TypeId, new I18nText("Pump / fan curve", "Pumpen-/Ventilatorkennlinie"), ChartCategories.Engineering,
            "pump", "fan", "curve", "operating point", "system curve", "head", "flow", "duty point",
            "pumpe", "ventilator", "lüfter", "kennlinie", "betriebspunkt", "anlagenkennlinie", "förderhöhe", "förderstrom", "drosselkurve")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            KindDef, H0Def, QMaxDef, HStatDef, QOpDef, SystemDef, Common.Title, Common.XLabel, Common.XUnit, Common.YLabel, Common.YUnit,
            Common.Grid, Common.Legend, Common.Box,
        };

        /// <summary>Operating point and system curve coefficient (static head kept below the operating head).</summary>
        public static (double Q, double H, double HStat, double K) OperatingPoint(double h0, double qmax, double hstat, double qop)
        {
            var q = Geo.Clamp(qop, qmax * 0.02, qmax * 0.98);
            var h = h0 * (1 - q * q / (qmax * qmax));
            var hs = Geo.Clamp(hstat, 0, h * 0.95);
            return (q, h, hs, (h - hs) / (q * q));
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var fan = o.Str("kind", "pump") == "fan";
            var h0 = Math.Max(1e-6, o.Num("h0", 40));
            var qmax = Math.Max(1e-6, o.Num("qmax", 100));
            var (q, h, hs, k) = OperatingPoint(h0, qmax, o.Num("hstat", 10), o.Num("qop", 70));
            var x = Axis.Linear(new[] { 0, qmax }, includeZero: true);
            var y = Axis.Linear(new[] { 0, h0 * 1.08 }, includeZero: true);
            var yl = fan ? new I18nText("Pressure Δp", "Druckerhöhung Δp") : new I18nText("Head H", "Förderhöhe H");
            var yu = fan ? new I18nText("Pa") : new I18nText("m");
            var xl = fan ? new I18nText("Volume flow V̇", "Volumenstrom V̇") : new I18nText("Flow rate Q", "Förderstrom Q");
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", yl, yu, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", xl, new I18nText("m³/h"), lang), panel);
            Common.Apply(f, o, ctx);
            var system = o.Flag("system", true);
            var pLook = SeriesLook.Of(0); var sLook = SeriesLook.Of(1);
            f.Legend.Add(new LegendEntry((fan ? FanCurve : PumpCurve).Get(lang), pLook.Line(0.6)));
            if (system) f.Legend.Add(new LegendEntry(SystemCurve.Get(lang), sLook.Line(0.5)));
            f.Layout(lang);
            var c = new Chart(f, lang);
            var a = panel.Area;
            var pump = Enumerable.Range(0, 41).Select(i => qmax * i / 40).Select(v => f.P(panel, v, h0 * (1 - v * v / (qmax * qmax)))).ToList();
            if (Draw.Series(pump, a, pLook.Line(0.6), true) is { } pl) c.Lines.Add(pl);
            c.Avoid.Add(pump);
            if (system)
            {
                var sys = Enumerable.Range(0, 41).Select(i => qmax * i / 40).Select(v => f.P(panel, v, hs + k * v * v)).ToList();
                if (Draw.Series(sys, a, sLook.Line(0.5), true) is { } sl) c.Lines.Add(sl);
                c.Avoid.Add(sys);
                c.Port(f.P(panel, 0, hs), "hstat");
            }
            var op = f.P(panel, q, h);
            var drops = new List<IReadOnlyList<Vec2>> { new[] { f.P(panel, q, y.Min), op }, new[] { f.P(panel, x.Min, h), op } };
            if (Draw.MultiPath(drops, S.Helper, ChartTags.Helper) is { } dl) c.Lines.Add(dl);
            c.Lines.Add(Draw.Markers(new[] { op }, MarkerKind.Circle, true, Palette.Ink, f.Fs * 0.38) with { Tag = ChartTags.Point });
            Draw.Label(c.Over, op + new Vec2(f.Fs * 0.6, -f.Fs * 0.75), OpShort.Get(lang), f.Fs * 0.85, ChartTags.Note, TextAnchor.Start);
            c.Port(op, "op");
            c.Port(f.P(panel, 0, h0), "h0");
            c.Port(f.P(panel, qmax, 0), "qmax");
            return c;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Prm.Cycle(o, "chart.kind", KindDef, new I18nText("Machine", "Maschine")),
            ContextAction.WithInput("chart.qop", new I18nText("Operating flow…", "Förderstrom im Betriebspunkt…"), IconIds.Text,
                new I18nText("Flow at the operating point", "Förderstrom im Betriebspunkt"), Fmt.Num(o.Num("qop", 70), lang),
                (x, text) => x.WithParam("qop", Fmt.ParseInput(text, lang, 1e-6, 1e9, "m³/h", "m3/h"))) with { Group = "data" },
            Prm.Toggle(o, "chart.system", "system", true, new I18nText("System curve", "Anlagenkennlinie")),
            Common.GridToggle(o),
            Common.LegendCycle(o),
        };
    }

    /// <summary>
    /// Bode plot: magnitude (dB) above phase (°) over a logarithmic frequency axis, for a first- or second-order low or
    /// high pass with corner frequency, damping and gain, or for measured data "f;dB;°".
    /// </summary>
    internal sealed class BodeChart : ChartType
    {
        public const string TypeId = "chart.bode";

        public static readonly ParamDef SystemDef = Prm.Choice("system", "System", "System", "lowpass2",
            ("lowpass1", "Low pass, 1st order", "Tiefpass 1. Ordnung"), ("lowpass2", "Low pass, 2nd order", "Tiefpass 2. Ordnung"),
            ("highpass1", "High pass, 1st order", "Hochpass 1. Ordnung"), ("highpass2", "High pass, 2nd order", "Hochpass 2. Ordnung"));
        public static readonly ParamDef FcDef = Prm.Num("fc", "Corner frequency", "Eckfrequenz", 1000, 1e-9, 1e15, "Hz");
        public static readonly ParamDef ZetaDef = Prm.Num("zeta", "Damping ratio (2nd order)", "Dämpfungsgrad (2. Ordnung)", 0.3, 0.01, 5, null, 0.05);
        public static readonly ParamDef GainDef = Prm.Num("gain", "Gain", "Verstärkung", 0, -200, 200, "dB", 1);
        public static readonly ParamDef ShowDef = Prm.Choice("show", "Show", "Anzeigen", "both",
            ("both", "Magnitude and phase", "Betrag und Phase"), ("magnitude", "Magnitude", "Betrag"), ("phase", "Phase", "Phase"));
        public static readonly ParamDef MarkerDef = Prm.Bool("marker", "Mark the corner frequency", "Eckfrequenz markieren", true);
        public static readonly ParamDef DataDef = Prm.Text("data", "Measured data (rows f;dB;°, empty = system)", "Messdaten (Zeilen f;dB;°, leer = System)", advanced: true);

        private static readonly I18nText MagLabel = new("Magnitude", "Betrag");
        private static readonly I18nText PhaseLabel = new("Phase φ", "Phase φ");
        private static readonly I18nText FreqLabel = new("Frequency f", "Frequenz f");
        private static readonly double[] PhaseSteps = { 5, 10, 15, 30, 45, 90, 180 };

        public BodeChart() : base(TypeId, new I18nText("Bode plot", "Bode-Diagramm"), ChartCategories.Engineering,
            "bode", "frequency response", "magnitude", "phase", "filter", "low pass", "high pass", "control", "decibel", "transfer function",
            "frequenzgang", "amplitudengang", "phasengang", "tiefpass", "hochpass", "regelung", "übertragungsfunktion", "dezibel")
        { }

        public override Size2 Design => new(100, 80);

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            SystemDef, FcDef, ZetaDef, GainDef, ShowDef, MarkerDef, Common.Title, Common.XRange, Common.Grid, Common.Box with { Default = true }, DataDef,
        };

        /// <summary>Magnitude (dB) and phase (°) of the system at frequency <paramref name="f"/>.</summary>
        public static (double Db, double Deg) Response(string system, double f, double fc, double zeta, double gain)
        {
            var r = f / fc;
            double rad = 180 / Math.PI;
            switch (system)
            {
                case "lowpass1": return (gain - 10 * Math.Log10(1 + r * r), -Math.Atan(r) * rad);
                case "highpass1": return (gain + 20 * Math.Log10(r) - 10 * Math.Log10(1 + r * r), 90 - Math.Atan(r) * rad);
                case "highpass2":
                    {
                        var den = (1 - r * r) * (1 - r * r) + 4 * zeta * zeta * r * r;
                        return (gain + 40 * Math.Log10(r) - 10 * Math.Log10(den), 180 - Math.Atan2(2 * zeta * r, 1 - r * r) * rad);
                    }
                default:
                    {
                        var den = (1 - r * r) * (1 - r * r) + 4 * zeta * zeta * r * r;
                        return (gain - 10 * Math.Log10(den), -Math.Atan2(2 * zeta * r, 1 - r * r) * rad);
                    }
            }
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var system = o.Str("system", "lowpass2");
            var fc = Math.Max(1e-9, o.Num("fc", 1000));
            var zeta = Geo.Clamp(o.Num("zeta", 0.3), 0.01, 5);
            var gain = o.Num("gain", 0);
            var range = ChartData.Range(o.Str("xrange", ""));
            var measured = ChartData.Parse(o.Str("data", "")).SelectMany(b => b.Rows)
                .Where(r => r.Label is null && r.Values.Count >= 2 && r.Values[0] > 0 && Fmt.Finite(r.Values[0]) && Fmt.Finite(r.Values[1]))
                .Select(r => (F: r.Values[0], Db: r.Values[1], Deg: r.Values.Count > 2 ? r.Values[2] : double.NaN)).OrderBy(r => r.F).ToList();
            var useData = measured.Count >= 2;
            List<(double F, double Db, double Deg)> pts;
            if (useData) pts = measured;
            else
            {
                double f0 = range is { } rr && rr.Min > 0 ? rr.Min : fc / 100, f1 = range is { } rr2 && rr2.Min > 0 ? rr2.Max : fc * 100;
                pts = Enumerable.Range(0, 241).Select(i => f0 * Math.Pow(f1 / f0, i / 240.0)).Select(fq => { var (db, deg) = Response(system, fq, fc, zeta, gain); return (fq, db, deg); }).ToList();
            }
            var show = o.Str("show", "both");
            var hasPhase = pts.Any(p => Fmt.Finite(p.Deg));
            var showMag = show != "phase" || !hasPhase;
            var showPhase = show != "magnitude" && hasPhase;
            var x = Axis.Logarithmic(pts.Select(p => p.F), range);
            var panels = new List<Panel>();
            Panel? mag = null, phase = null;
            if (showMag)
            {
                var dbs = pts.Where(p => x.Contains(p.F)).Select(p => p.Db).Where(Fmt.Finite).Select(v => Math.Max(v, gain - 120)).ToList();
                mag = new Panel(Axis.Linear(dbs, target: 4, steps: dbs.Count > 0 && dbs.Max() - dbs.Min() > 30 ? new double[] { 10, 20, 40, 60, 80, 100 } : null), Common.AxisTitle(o, "maglabel-none", "magunit-none", MagLabel, new I18nText("dB"), lang));
                panels.Add(mag);
            }
            if (showPhase)
            {
                phase = new Panel(Axis.Linear(pts.Select(p => p.Deg), target: 4, steps: PhaseSteps), Common.AxisTitle(o, "phaselabel-none", "phaseunit-none", PhaseLabel, new I18nText("°"), lang));
                panels.Add(phase);
            }
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel-none", "xunit-none", FreqLabel, new I18nText("Hz"), lang), panels.ToArray());
            Common.Apply(f, o, ctx, boxDefault: true, legendDefault: "none");
            f.Layout(lang);
            var c = new Chart(f, lang);
            var look = SeriesLook.Of(0);
            if (mag is not null)
            {
                var line = pts.Select(p => f.P(mag, p.F, p.Db)).ToList();
                if (Draw.Series(line, mag.Area, look.Line(0.5), false) is { } ml) c.Lines.Add(ml);
                if (useData) c.Lines.Add(Draw.Markers(line.Where(p => Frame.Ok(p) && mag.Area.Inflate(0.01).Contains(p)), look.Marker, true, look.Color, f.Fs * 0.25));
            }
            if (phase is not null)
            {
                var line = pts.Where(p => Fmt.Finite(p.Deg)).Select(p => f.P(phase, p.F, p.Deg)).ToList();
                if (Draw.Series(line, phase.Area, look.Line(0.5), false) is { } pl) c.Lines.Add(pl);
                if (useData) c.Lines.Add(Draw.Markers(line.Where(p => Frame.Ok(p) && phase.Area.Inflate(0.01).Contains(p)), look.Marker, true, look.Color, f.Fs * 0.25));
            }
            if (!useData && x.Contains(fc))
            {
                var (db, deg) = Response(system, fc, fc, zeta, gain);
                if (o.Flag("marker", true))
                {
                    var helper = new List<IReadOnlyList<Vec2>>();
                    foreach (var p in panels) helper.Add(new[] { f.P(p, fc, p.Y.Min), f.P(p, fc, p.Y.Max) });
                    if (Draw.MultiPath(helper, S.Helper, ChartTags.Helper) is { } hp) c.Lines.Add(hp);
                    var first = panels[0];
                    var label = "f_c = " + Axis.LogLabel(fc, lang) + " Hz";
                    var fs = f.Fs * 0.75;
                    var at = f.P(first, fc, first.Y.Min);
                    var right = at.X + fs * 0.4 + Fmt.Width(label, fs) <= first.Area.Right;
                    Draw.Label(c.Over, new Vec2(at.X + (right ? 1 : -1) * fs * 0.4, first.Area.Y + fs * 0.9), label, fs, ChartTags.Note, right ? TextAnchor.Start : TextAnchor.End);
                }
                if (mag is not null) c.Port(f.P(mag, fc, db), "fc");
                if (phase is not null) c.Port(f.P(phase, fc, deg), "fc.phase");
            }
            if (useData)
                for (var i = 0; i < pts.Count; i++)
                {
                    if (mag is not null) c.Port(f.P(mag, pts[i].F, pts[i].Db), "s1." + Fmt.I(i + 1));
                    if (phase is not null && Fmt.Finite(pts[i].Deg)) c.Port(f.P(phase, pts[i].F, pts[i].Deg), "s2." + Fmt.I(i + 1));
                }
            return c;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Prm.Cycle(o, "chart.system", SystemDef, new I18nText("System", "System")),
            ContextAction.WithInput("chart.fc", new I18nText("Corner frequency…", "Eckfrequenz…"), IconIds.Text,
                new I18nText("Corner frequency (e.g. 1 kHz, 250 Hz, 2.5k)", "Eckfrequenz (z. B. 1 kHz, 250 Hz, 2,5k)"), Axis.LogLabel(o.Num("fc", 1000), lang) + " Hz",
                (x, text) => x.WithParam("fc", Fmt.ParseInput(text, lang, 1e-9, 1e15, "Hz"))) with { Group = "data" },
            Prm.Cycle(o, "chart.show", ShowDef, new I18nText("Show", "Anzeigen")),
            Prm.Toggle(o, "chart.marker", "marker", true, new I18nText("Mark the corner frequency", "Eckfrequenz markieren")),
            Common.GridToggle(o),
        };
    }

    /// <summary>
    /// Tolerance band: a nominal curve with an upper and lower limit curve and the band between them (tint, or hatch in
    /// monochrome output); further data blocks are drawn as measured series with markers.
    /// </summary>
    internal sealed class BandChart : ChartType
    {
        public const string TypeId = "chart.band";

        public const string DefaultData = "0;0\n1;1.9\n2;3.4\n3;4.4\n4;5.0\n5;5.3\n6;5.4";
        public static readonly ParamDef DataDef = Prm.Text("data", "Data (rows x;y, or x;y;±tol, or x;y;lower;upper)", "Daten (Zeilen x;y, oder x;y;±Tol, oder x;y;unten;oben)", DefaultData);
        public static readonly ParamDef TolDef = Prm.Num("tol", "Tolerance (rows x;y)", "Toleranz (Zeilen x;y)", 10, 0, 1e9, null, 1);
        public static readonly ParamDef TolModeDef = Prm.Choice("tolmode", "Tolerance as", "Toleranz als", "percent", ("percent", "Percent of y", "Prozent von y"), ("absolute", "Absolute value", "Absolutwert"));
        public static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curves", "Geglättete Kurven", true);

        private static readonly I18nText Nominal = new("Nominal", "Sollwert");
        private static readonly I18nText Band = new("Tolerance band", "Toleranzband");

        public BandChart() : base(TypeId, new I18nText("Tolerance band", "Toleranzband"), ChartCategories.Engineering,
            "tolerance", "band", "limits", "upper limit", "lower limit", "envelope", "corridor", "specification",
            "toleranz", "toleranzband", "grenzen", "obergrenze", "untergrenze", "hüllkurve", "korridor", "grenzwert")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, TolDef, TolModeDef, SmoothDef, Common.Title, Common.XLabel, Common.XUnit, Common.YLabel, Common.YUnit,
            Common.Grid, Common.Legend, Common.Limits, Common.XRange, Common.YRange, Common.Patterns, Common.Box,
        };

        /// <summary>Nominal, lower and upper points of the first block (see the data parameter for the column meaning).</summary>
        public static (List<Vec2> Nominal, List<Vec2> Lower, List<Vec2> Upper) Curves(DataBlock b, double tol, bool percent)
        {
            var nom = new List<Vec2>(); var lo = new List<Vec2>(); var hi = new List<Vec2>();
            var rows = b.Rows.Where(r => r.Label is null && r.Values.Count >= 2 && Fmt.Finite(r.Values[0]) && Fmt.Finite(r.Values[1])).OrderBy(r => r.Values[0]);
            foreach (var r in rows)
            {
                double x = r.Values[0], y = r.Values[1], l, u;
                if (r.Values.Count >= 4 && Fmt.Finite(r.Values[2]) && Fmt.Finite(r.Values[3])) { l = Math.Min(r.Values[2], r.Values[3]); u = Math.Max(r.Values[2], r.Values[3]); }
                else if (r.Values.Count >= 3 && Fmt.Finite(r.Values[2])) { l = y - Math.Abs(r.Values[2]); u = y + Math.Abs(r.Values[2]); }
                else { var d = percent ? Math.Abs(y) * tol / 100 : Math.Abs(tol); l = y - d; u = y + d; }
                nom.Add(new Vec2(x, y)); lo.Add(new Vec2(x, l)); hi.Add(new Vec2(x, u));
            }
            return (nom, lo, hi);
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var blocks = ChartData.Parse(o.Str("data", DefaultData));
            var (nom, lo, hi) = blocks.Count > 0 ? Curves(blocks[0], o.Num("tol", 10), o.Str("tolmode", "percent") == "percent") : (new List<Vec2>(), new List<Vec2>(), new List<Vec2>());
            var extra = ChartData.Xy(blocks.Skip(1).ToList());
            var limits = ChartData.Numbers(o.Str("limits", ""));
            var xs = nom.Select(p => p.X).Concat(extra.SelectMany(s => s.Points.Select(p => p.X)));
            var ys = lo.Concat(hi).Concat(nom).Select(p => p.Y).Concat(extra.SelectMany(s => s.Points.Select(p => p.Y))).Concat(limits);
            var x = Axis.Linear(xs, ChartData.Range(o.Str("xrange", "")));
            var y = Axis.Linear(ys, ChartData.Range(o.Str("yrange", "")));
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", "y", null, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", "x", null, lang), panel);
            Common.Apply(f, o, ctx);
            var mono = f.Mono || o.Flag("patterns", false);
            var look = SeriesLook.Of(0);
            var bandStyle = mono
                ? new Style { Stroke = "none", Role = LineRole.Visible, Fill = Fill.Hatched(new HatchPattern(45, 1.5)) }
                : S.FillOnly(Palette.Band);
            var edge = new Style { Stroke = look.Color, StrokeWidth = 0.3, Role = LineRole.Thin, Dash = DashStyle.Dash };
            f.Legend.Add(new LegendEntry(Nominal.Get(lang), look.Line(0.5)));
            f.Legend.Add(new LegendEntry(Band.Get(lang), bandStyle with { Stroke = look.Color, StrokeWidth = 0.25 }, Area: true));
            for (var k = 0; k < extra.Count; k++)
            {
                var l = SeriesLook.Of(k + 1);
                f.Legend.Add(new LegendEntry(Common.SeriesName(extra[k].Name, k + 1, lang), l.Line(0.35), l.Marker, l.FilledMarker));
            }
            f.Layout(lang);
            var c = new Chart(f, lang);
            var a = panel.Area;
            var smooth = o.Flag("smooth", true);
            List<Vec2> Curve(List<Vec2> d)
            {
                var m = d.Select(p => f.P(panel, p.X, p.Y)).ToList();
                return smooth && m.Count >= 3 ? Draw.Sample(Draw.Spline(m), 8) : m;
            }
            if (nom.Count >= 2)
            {
                var up = Curve(hi); var dn = Curve(lo);
                var poly = up.Concat(Enumerable.Reverse(dn)).ToList();
                var clipped = ClipPolygon(poly, a);
                if (clipped.Count >= 3) c.Areas.Add(Prim.Polygon(clipped, bandStyle) with { Tag = ChartTags.Band });
                if (Draw.MultiPath(Draw.Clip(up, a).Concat(Draw.Clip(dn, a)), edge, ChartTags.Limit) is { } ep) c.Lines.Add(ep);
                var nm = nom.Select(p => f.P(panel, p.X, p.Y)).ToList();
                if (Draw.Series(nm, a, look.Line(0.5), smooth) is { } nl) c.Lines.Add(nl);
                c.Avoid.Add(up); c.Avoid.Add(dn);
                for (var i = 0; i < nm.Count; i++) c.Port(nm[i], "s1." + Fmt.I(i + 1));
                c.Port(f.P(panel, hi[hi.Count / 2].X, hi[hi.Count / 2].Y), "upper");
                c.Port(f.P(panel, lo[lo.Count / 2].X, lo[lo.Count / 2].Y), "lower");
            }
            Common.DrawLimits(c, o);
            for (var k = 0; k < extra.Count; k++)
            {
                var l = SeriesLook.Of(k + 1);
                var pts = extra[k].Points.Select(p => f.P(panel, p.X, p.Y)).ToList();
                if (Draw.Series(pts, a, l.Line(0.35), false) is { } sl) c.Lines.Add(sl);
                var visible = pts.Where(p => Frame.Ok(p) && a.Inflate(0.01).Contains(p)).ToList();
                if (visible.Count > 0) c.Lines.Add(Draw.Markers(visible, l.Marker, l.FilledMarker, l.Color, f.Fs * 0.3));
                c.Avoid.Add(pts);
                for (var i = 0; i < pts.Count; i++) c.Port(pts[i], "s" + Fmt.I(k + 2) + "." + Fmt.I(i + 1));
            }
            return c;
        }

        /// <summary>Sutherland–Hodgman clipping of a polygon to a rectangle.</summary>
        public static List<Vec2> ClipPolygon(IReadOnlyList<Vec2> poly, Rect r)
        {
            var output = poly.Where(Frame.Ok).ToList();
            foreach (var (inside, cross) in new (Func<Vec2, bool>, Func<Vec2, Vec2, Vec2>)[]
            {
                (p => p.X >= r.X, (a, b) => Vec2.Lerp(a, b, (r.X - a.X) / (b.X - a.X))),
                (p => p.X <= r.Right, (a, b) => Vec2.Lerp(a, b, (r.Right - a.X) / (b.X - a.X))),
                (p => p.Y >= r.Y, (a, b) => Vec2.Lerp(a, b, (r.Y - a.Y) / (b.Y - a.Y))),
                (p => p.Y <= r.Bottom, (a, b) => Vec2.Lerp(a, b, (r.Bottom - a.Y) / (b.Y - a.Y))),
            })
            {
                if (output.Count == 0) break;
                var input = output;
                output = new List<Vec2>();
                for (var i = 0; i < input.Count; i++)
                {
                    var cur = input[i]; var prev = input[(i + input.Count - 1) % input.Count];
                    if (inside(cur))
                    {
                        if (!inside(prev)) output.Add(cross(prev, cur));
                        output.Add(cur);
                    }
                    else if (inside(prev)) output.Add(cross(prev, cur));
                }
            }
            return output;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Common.DataInput(o, DefaultData),
            ContextAction.WithInput("chart.tol", new I18nText("Tolerance…", "Toleranz…"), IconIds.Text,
                new I18nText("Tolerance (e.g. 10 % or 0.5)", "Toleranz (z. B. 10 % oder 0,5)"),
                Fmt.Num(o.Num("tol", 10), lang) + (o.Str("tolmode", "percent") == "percent" ? " %" : ""),
                (x, text) =>
                {
                    var t = (text ?? "").Trim();
                    var percent = t.EndsWith("%", StringComparison.Ordinal);
                    var v = Fmt.ParseInput(t.TrimEnd('%').Trim(), lang, 0, 1e9);
                    return x.WithParam("tol", v).WithParam("tolmode", percent ? "percent" : "absolute");
                }) with { Group = "data" },
            Prm.Toggle(o, "chart.smooth", "smooth", true, new I18nText("Smooth curves", "Geglättete Kurven")),
            Common.GridToggle(o),
            Common.LegendCycle(o),
        };
    }
}
