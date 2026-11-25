using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Charts and characteristic curves for slides: XY line, scatter and step charts, bar/column charts, axes with grid,
    /// torque–speed curve, efficiency map, pump/fan curve with operating point, Bode plot and tolerance band. Every chart is
    /// insertable without parameters and drawn with native primitives (lines, freeforms, rectangles, text); data are a
    /// text parameter (typed in or pasted from a spreadsheet, see <see cref="ChartData"/>). Series stay distinguishable in
    /// monochrome output through dash patterns, markers and fill patterns.
    /// </summary>
    public sealed class ChartsModule : IModule, IExportProvider
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() => new ObjectType[]
        {
            new XyChart(), new BarChart(), new AxesChart(),
            new TorqueChart(), new EfficiencyChart(), new PumpChart(), new BodeChart(), new BandChart(),
            new FigureChart(),
        });

        private static readonly Lazy<IReadOnlyDictionary<string, ObjectType>> ById = new(() => AllTypes.Value.ToDictionary(t => t.Id, StringComparer.Ordinal));
        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "charts";
        public I18nText Name => new("Charts & characteristic curves", "Diagramme & Kennlinien");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(ChartCategories.Data, new I18nText("Data charts", "Datendiagramme"), Id, 10),
            new LibraryCategory(ChartCategories.Engineering, new I18nText("Characteristic curves", "Kennlinien & Kennfelder"), Id, 20),
            new LibraryCategory(FigureChart.CategoryId, new I18nText("Principle and technical figures", "Prinzip- und technische Abbildungen"), Id, 30),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        /// <summary>Domain exports of this module (<see cref="IExportProvider"/>): charts as matplotlib and pgfplots code.</summary>
        public IEnumerable<IExporter> Exporters => new IExporter[] { new MatplotlibExporter(), new PgfplotsExporter() };

        private static IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, Size2? size = null, params string[] kw)
            {
                var t = ById.Value[type];
                list.Add(new LibraryItem("chart." + id, type, new I18nText(en, de), t.Category) { Params = ps, Size = size, Keywords = kw });
            }

            // Data charts
            Add("line", XyChart.TypeId, "Line chart", "Liniendiagramm", kw: new[] { "line", "linie" });
            Add("line.markers", XyChart.TypeId, "Line chart with markers", "Liniendiagramm mit Markern", P(("style", "linepoints")), kw: new[] { "markers", "marker" });
            Add("line.multi", XyChart.TypeId, "Line chart (three series)", "Liniendiagramm (drei Reihen)",
                P(("style", "linepoints"), ("data", "x;A;B;C\n0;1.0;0.5;0.2\n1;2.2;1.4;0.8\n2;3.0;2.1;1.5\n3;3.6;2.9;2.0\n4;4.0;3.4;2.6\n5;4.3;3.8;3.0")),
                kw: new[] { "series", "multiple", "reihen", "mehrere" });
            Add("line.smooth", XyChart.TypeId, "Smooth curve", "Geglättete Kurve", P(("smooth", true)), kw: new[] { "spline", "curve", "kurve" });
            Add("line.logx", XyChart.TypeId, "Line chart, log x-axis", "Liniendiagramm, log. x-Achse",
                P(("xscale", "log"), ("style", "linepoints"), ("data", "10;0.5\n30;1.4\n100;4\n300;9\n1000;16\n3000;21\n10000;24")),
                kw: new[] { "logarithmic", "log", "logarithmisch", "decade", "dekade" });
            Add("scatter", XyChart.TypeId, "Scatter plot", "Streudiagramm",
                P(("style", "points"), ("data", "1.2;2.1\n1.8;2.9\n2.3;2.6\n2.9;3.8\n3.4;3.5\n3.9;4.6\n4.4;4.2\n4.8;5.3\n5.5;5.1\n6.1;6.2\n6.6;5.9\n7.2;6.8")),
                kw: new[] { "scatter", "points", "punkte", "punktwolke" });
            Add("step", XyChart.TypeId, "Step chart", "Stufendiagramm", P(("style", "step"), ("data", "0;0\n1;2\n2;2\n3;5\n4;3\n5;4\n6;4")),
                kw: new[] { "step", "staircase", "stufen", "treppe" });
            Add("column", BarChart.TypeId, "Column chart", "Säulendiagramm", kw: new[] { "column", "säule" });
            Add("column.grouped", BarChart.TypeId, "Column chart (grouped)", "Säulendiagramm (gruppiert)",
                P(("data", "Quarter;A;B\nQ1;4;3\nQ2;5;4.5\nQ3;3.5;5\nQ4;6;5.5")), kw: new[] { "grouped", "gruppiert", "quarter", "quartal" });
            Add("column.stacked", BarChart.TypeId, "Column chart (stacked)", "Säulendiagramm (gestapelt)",
                P(("mode", "stacked"), ("data", "Quarter;A;B;C\nQ1;2;1.5;1\nQ2;2.5;2;1\nQ3;2;2.5;1.5\nQ4;3;2;2")), kw: new[] { "stacked", "gestapelt" });
            Add("bar.horizontal", BarChart.TypeId, "Bar chart (horizontal)", "Balkendiagramm (waagerecht)", P(("orient", "bar"), ("values", true)),
                kw: new[] { "horizontal", "waagerecht", "ranking" });
            Add("axes", AxesChart.TypeId, "Axes with grid", "Achsen mit Gitter", kw: new[] { "coordinate system", "koordinatensystem" });
            Add("axes.plain", AxesChart.TypeId, "Axes without grid", "Achsen ohne Gitter", P(("grid", false)), kw: new[] { "plain", "ohne gitter" });
            Add("axes.loglog", AxesChart.TypeId, "Log–log axes", "Doppelt logarithmische Achsen",
                P(("xscale", "log"), ("yscale", "log"), ("xrange", "1;1000"), ("yrange", "0.1;100"), ("arrows", false), ("box", true)),
                kw: new[] { "log", "logarithmic", "logarithmisch", "doppeltlogarithmisch" });

            // Characteristic curves and engineering charts
            Add("torque", TorqueChart.TypeId, "Torque–speed curve with power", "Drehmoment-Drehzahl-Kennlinie mit Leistung");
            Add("torque.plain", TorqueChart.TypeId, "Torque–speed curve", "Drehmoment-Drehzahl-Kennlinie", P(("power", false)));
            Add("efficiency", EfficiencyChart.TypeId, "Efficiency map", "Wirkungsgradkennfeld");
            Add("pump", PumpChart.TypeId, "Pump curve with operating point", "Pumpenkennlinie mit Betriebspunkt");
            Add("fan", PumpChart.TypeId, "Fan curve with operating point", "Ventilatorkennlinie mit Betriebspunkt",
                P(("kind", "fan"), ("h0", 800.0), ("qmax", 5000.0), ("hstat", 0.0), ("qop", 3500.0)), kw: new[] { "fan", "blower", "ventilator", "gebläse" });
            Add("bode", BodeChart.TypeId, "Bode plot (2nd-order low pass)", "Bode-Diagramm (Tiefpass 2. Ordnung)");
            Add("bode.lowpass1", BodeChart.TypeId, "Bode plot (1st-order low pass)", "Bode-Diagramm (Tiefpass 1. Ordnung)", P(("system", "lowpass1")), kw: new[] { "rc", "pt1" });
            Add("bode.highpass1", BodeChart.TypeId, "Bode plot (1st-order high pass)", "Bode-Diagramm (Hochpass 1. Ordnung)", P(("system", "highpass1")), kw: new[] { "high pass", "hochpass" });
            Add("band", BandChart.TypeId, "Tolerance band", "Toleranzband");
            Add("band.measured", BandChart.TypeId, "Tolerance band with measurement", "Toleranzband mit Messung",
                P(("data", "0;0\n1;1.9\n2;3.4\n3;4.4\n4;5.0\n5;5.3\n6;5.4\n\n0.5;1.1\n1.5;2.6\n2.5;4.1\n3.5;4.6\n4.5;5.3\n5.5;5.2")),
                kw: new[] { "measurement", "measured", "messung", "messwerte" });
            Add("limits", XyChart.TypeId, "Curve with limit lines", "Kurve mit Grenzlinien", P(("limits", "1.5;5.5")),
                kw: new[] { "limit", "threshold", "grenzwert", "schwelle", "grenzlinie" });

            // Figures (charts v2 stage 2): principle and technical figures from a JSON specification.
            Add("figure.phases", FigureChart.TypeId, "Phases with rotor temperature (technical figure)", "Phasen mit Rotortemperatur (technische Abbildung)",
                P(("spec", FigurePresets.Patent)), kw: new[] { "phase", "pre-conditioning", "full load", "time gained", "vorkonditionierung", "volllast", "zeitgewinn", "rotortemperatur" });
            Add("figure.timing", FigureChart.TypeId, "Timing diagram of a decision logic", "Zeitablauf einer Entscheidungslogik",
                P(("spec", FigurePresets.Timing)), new Size2(100, 110), "timing", "pulse", "t_krit", "decision", "zeitdiagramm", "impuls", "entscheidung", "abtastsignal");
            Add("figure.temperature", FigureChart.TypeId, "Temperature over time with thresholds", "Temperaturverlauf mit Schwellen",
                P(("spec", FigurePresets.Temperature)), new Size2(110, 75), "temperature", "threshold", "activation", "temperatur", "schwelle", "aktivierung", "aufheizen");
            return list;
        }
    }
}
