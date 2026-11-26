using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A task of a Gantt chart: group (data block), name, start and duration (0 = milestone).</summary>
    internal sealed record GanttTask(int Group, string Name, double Start, double Duration)
    {
        public bool Milestone => !(Duration > 0);
        public double End => Start + (Milestone ? 0 : Duration);
    }

    /// <summary>
    /// Gantt chart: one row per task with a bar from its start over its duration, milestones as diamonds (rows with a start
    /// but no duration), an optional today line, the tasks top-down on a category axis and the time on a linear axis. Data:
    /// rows "task;start;duration" in the <see cref="ChartData"/> format; a blank line or a header line starts a new group
    /// (its own colour and legend entry). Times are numbers in any unit (days, weeks …, named by the x-axis title) or calendar
    /// dates (<see cref="ChartDates"/>: day numbers, durations in days, a date axis).
    /// </summary>
    internal sealed class GanttChart : ChartType
    {
        public const string TypeId = "chart.gantt";

        public const string DefaultData = "A;0;3\nB;2;4\nC;5;3\nD;7;3\nE;10";

        public static readonly ParamDef DataDef = Prm.Text("data", "Tasks (rows task;start;duration, no duration = milestone)", "Vorgänge (Zeilen Vorgang;Start;Dauer, ohne Dauer = Meilenstein)", DefaultData);
        public static readonly ParamDef TodayDef = Prm.Text("today", "Today line at (time, empty = none)", "Heute-Linie bei (Zeit, leer = keine)");

        public GanttChart() : base(TypeId, new I18nText("Gantt chart", "Gantt-Diagramm"), ChartCategories.Data,
            "gantt", "schedule", "timeline", "project plan", "tasks", "milestone", "roadmap", "today", "planning",
            "gantt-diagramm", "balkenplan", "zeitplan", "projektplan", "terminplan", "vorgänge", "meilenstein", "zeitstrahl", "planung")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, TodayDef, Common.Title, Common.XLabel, Common.XUnit, Common.Grid, Common.Legend, Common.XRange, Common.Patterns, Common.Box,
        };

        /// <summary>
        /// The tasks of the data and the group names (rows without a start are skipped; names default to "Task n"). Calendar
        /// dates (yyyy-mm-dd, dd.mm.yyyy) are read as day numbers (<see cref="ChartDates"/>).
        /// </summary>
        internal static (List<GanttTask> Tasks, List<string?> Groups) Tasks(string? data, Lang lang)
        {
            var tasks = new List<GanttTask>();
            var groups = new List<string?>();
            var n = 0;
            foreach (var b in ChartData.Parse(ChartDates.ToDays(data, out _)))
            {
                var g = groups.Count;
                groups.Add(b.Name);
                foreach (var r in b.Rows)
                {
                    n++;
                    if (r.Values.Count == 0 || !Fmt.Finite(r.Values[0])) continue;
                    var d = r.Values.Count > 1 && Fmt.Finite(r.Values[1]) ? r.Values[1] : 0;
                    var name = string.IsNullOrWhiteSpace(r.Label) ? (lang == Lang.De ? "Vorgang " : "Task ") + Fmt.I(n) : r.Label!;
                    tasks.Add(new GanttTask(g, name, r.Values[0], Math.Max(0, d)));
                }
            }
            return (tasks, groups);
        }

        /// <summary>The time of the today line (a number or a calendar date as its day number), or null.</summary>
        internal static double? Today(SketchObject o)
        {
            var t = ChartDates.ToDays(o.Str("today", ""), out _).Trim();
            return t.Length > 0 && ChartData.TryNumber(t, out var v) && Fmt.Finite(v) ? v : null;
        }

        /// <summary>True if the data (or the today line) use calendar dates: the time axis then shows dates.</summary>
        internal static bool UsesDates(SketchObject o)
        {
            ChartDates.ToDays(o.Str("data", DefaultData), out var dates);
            if (!dates) ChartDates.ToDays(o.Str("today", ""), out dates);
            return dates;
        }

        /// <summary>The fixed time range ("min;max", numbers or dates), or null.</summary>
        internal static (double Min, double Max)? Range(SketchObject o) => ChartData.Range(ChartDates.ToDays(o.Str("xrange", ""), out _));

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var (tasks, groups) = Tasks(o.Str("data", DefaultData), lang);
            var today = Today(o);
            var times = tasks.SelectMany(t => new[] { t.Start, t.End }).ToList();
            if (today is { } td) times.Add(td);
            var range = Range(o);
            var dates = UsesDates(o);
            // A milestone at the end of an automatic time axis: the axis grows by a step so the diamond stays inside the plot.
            var finite = times.Where(Fmt.Finite).ToList();
            var span = finite.Count > 0 ? finite.Max() - finite.Min() : 0;
            var pad = span > 0 ? span * 0.04 : 0.5;
            var lastMilestone = tasks.Where(t => t.Milestone && Fmt.Finite(t.Start)).Select(t => t.Start).DefaultIfEmpty(double.NegativeInfinity).Max();
            IReadOnlyList<Tick>? dateTicks = null;
            Axis x;
            if (dates)
            {
                var (lo, hi) = range ?? (finite.Count > 0 ? (finite.Min(), Math.Max(finite.Max(), lastMilestone + pad)) : (0, 1));
                dateTicks = ChartDates.Ticks(lo, hi, lang, out var tickLo, out var tickHi, range is not null);
                x = Axis.Linear(times, range ?? (tickLo, tickHi));
            }
            else
            {
                x = Axis.Linear(times, range);
                if (range is null && x.Step > 0 && lastMilestone >= x.Max - pad)
                    x = Axis.Linear(new[] { x.Min, x.Max + x.Step }, null, target: (int)Math.Round((x.Max + x.Step - x.Min) / x.Step));
            }
            var y = Axis.Category(tasks.Select(t => t.Name).ToList(), reversed: true);
            var panel = new Panel(y, null);
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", dates ? new I18nText("Date", "Datum") : new I18nText("Time", "Zeit"), null, lang), panel);
            if (dateTicks is not null) f.XTicks = dateTicks;
            Common.Apply(f, o, ctx);
            var mono = f.Mono || o.Flag("patterns", false);
            var used = tasks.Select(t => t.Group).Distinct().OrderBy(g => g).ToList();
            for (var g = 0; g < groups.Count; g++)
                f.Legend.Add(new LegendEntry(Common.SeriesName(groups[g], g, lang), SeriesLook.Of(g).Area(mono, 0.25), Area: true));
            if (used.Count < 2 && f.LegendMode == "auto") f.LegendMode = "none";
            f.Layout(lang);
            var c = new Chart(f, lang);
            var milestones = new Dictionary<int, List<Vec2>>();
            double X(double v) => Geo.Clamp(v, x.Min, x.Max);
            // Milestones: diamonds as high as a bar, kept inside the plot (a fixed range may end at a milestone).
            var r = MilestoneSize(f, panel, tasks.Count);
            for (var i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                var look = SeriesLook.Of(t.Group);
                if (t.Milestone)
                {
                    var p = f.P(panel, X(t.Start), i + 0.5);
                    p = new Vec2(Geo.Clamp(p.X, panel.Area.X + r, Math.Max(panel.Area.X + r, panel.Area.Right - r)), p.Y);
                    if (!milestones.TryGetValue(t.Group, out var l)) milestones[t.Group] = l = new List<Vec2>();
                    l.Add(p);
                    c.Port(p, "s" + Fmt.I(t.Group + 1) + "." + Fmt.I(i + 1));
                    continue;
                }
                var p0 = f.P(panel, X(t.Start), i + 0.2); var p1 = f.P(panel, X(t.End), i + 0.8);
                var rw = Math.Abs(p1.X - p0.X); var rh = Math.Abs(p1.Y - p0.Y);
                if (rw > 1e-6 && rh > 1e-6) c.Areas.Add(Prim.Rect(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), rw, rh, look.Area(mono, 0.25)) with { Tag = ChartTags.Nth(ChartTags.Bar, t.Group) });
                var end = f.P(panel, X(t.End), i + 0.5);
                c.Avoid.Add(new[] { f.P(panel, X(t.Start), i + 0.5), end });
                c.Port(end, "s" + Fmt.I(t.Group + 1) + "." + Fmt.I(i + 1));
            }
            foreach (var kv in milestones)
            {
                var segs = new List<PathSeg>();
                foreach (var p in kv.Value)
                {
                    segs.Add(PathSeg.Move(p + new Vec2(0, -r))); segs.Add(PathSeg.Line(p + new Vec2(r, 0)));
                    segs.Add(PathSeg.Line(p + new Vec2(0, r))); segs.Add(PathSeg.Line(p + new Vec2(-r, 0))); segs.Add(PathSeg.Close);
                }
                var color = mono ? Palette.Black : SeriesLook.Of(kv.Key).Color;
                c.Lines.Add(new PathPrim(segs, new Style { Stroke = color, StrokeWidth = 0.25, Role = LineRole.Visible, Fill = Fill.Solid(color) }) { Tag = ChartTags.Nth(ChartTags.Milestone, kv.Key) });
                c.Avoid.Add(kv.Value);
            }
            // Today line with its label at the top of the plot.
            if (today is { } now && x.Contains(now))
            {
                var a = f.P(panel, now, 0);
                var style = new Style { Stroke = mono ? Palette.Black : "#b03a2e", StrokeWidth = 0.4, Role = LineRole.Visible, Dash = DashStyle.Dash };
                c.Lines.Add(Prim.Line(new Vec2(a.X, panel.Area.Y), new Vec2(a.X, panel.Area.Bottom), style) with { Tag = ChartTags.Today });
                var text = lang == Lang.De ? "Heute" : "Today";
                var fs = f.Fs * 0.8;
                var w = Fmt.Width(text, fs);
                var right = a.X + fs * 0.3 + w <= panel.Area.Right;
                var at = new Vec2(right ? a.X + fs * 0.3 : a.X - fs * 0.3, panel.Area.Y + fs * 0.7);
                Draw.Label(c.Over, at, text, fs, ChartTags.Today, right ? TextAnchor.Start : TextAnchor.End);
                c.Anchors.Add(Anchor.Rail(new Vec2(a.X, panel.Area.Bottom), new Vec2(a.X, panel.Area.Y), "today"));
            }
            return c;
        }

        /// <summary>Half diagonal of a milestone diamond (mm): about a third of a row, at most 0.8 font heights.</summary>
        internal static double MilestoneSize(Frame f, Panel panel, int tasks)
        {
            var band = tasks > 0 ? panel.Area.H / Math.Max(1, tasks) : panel.Area.H;
            return Math.Min(band * 0.32, f.Fs * 0.8);
        }

        /// <summary>Appends a task after the last one (it starts where the last task ends).</summary>
        internal static SketchObject AddTask(SketchObject x, Lang lang)
        {
            var text = x.Str("data", DefaultData).TrimEnd();
            var (tasks, _) = Tasks(text, lang);
            var start = tasks.Count > 0 ? tasks.Max(t => t.End) : 0;
            var names = new HashSet<string>(tasks.Select(t => t.Name), StringComparer.Ordinal);
            var k = tasks.Count + 1;
            string Name(int i) => (lang == Lang.De ? "Vorgang " : "Task ") + Fmt.I(i);
            while (names.Contains(Name(k))) k++;
            // With calendar dates the new task starts on a date (ISO) and lasts two days.
            ChartDates.ToDays(text, out var dates);
            var at = dates ? ChartDates.Iso(start) : ExportText.N(start);
            return x.WithParam("data", (text.Length > 0 ? text + "\n" : "") + Name(k) + ";" + at + ";2");
        }

        /// <summary>Removes the last task row (the last remaining task stays).</summary>
        internal static SketchObject RemoveTask(SketchObject x, Lang lang)
        {
            var text = x.Str("data", DefaultData);
            if (Tasks(text, lang).Tasks.Count < 2) return x;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('|', '\n').Split('\n').ToList();
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var row = ChartData.Parse(ChartDates.ToDays(lines[i], out _));
                if (row.Count == 1 && row[0].Rows.Count == 1 && row[0].Rows[0].Values.Count > 0 && Fmt.Finite(row[0].Rows[0].Values[0])) { lines.RemoveAt(i); break; }
            }
            return x.WithParam("data", string.Join("\n", lines).TrimEnd('\n'));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var count = Tasks(o.Str("data", DefaultData), lang).Tasks.Count;
            return new[]
            {
                Common.DataInput(o, DefaultData) with { Input = Common.DataInput(o, DefaultData).Input! with
                {
                    Label = new I18nText("Rows “task;start;duration” separated by | (new group: ||)", "Zeilen „Vorgang;Start;Dauer“ getrennt durch | (neue Gruppe: ||)"),
                } },
                new ContextAction("chart.gantt.add", new I18nText("Add task", "Vorgang hinzufügen"), IconIds.RowAdd, x => AddTask(x, lang)) { Group = "data" },
                new ContextAction("chart.gantt.remove", new I18nText("Remove last task", "Letzten Vorgang entfernen"), IconIds.RowRemove, x => RemoveTask(x, lang))
                    { Group = "data", IsEnabled = count > 1 },
                ContextAction.WithInput("chart.today", new I18nText("Today line…", "Heute-Linie…"), IconIds.ValueLine,
                    new I18nText("Time of the today line, a number or a date (empty = none)", "Zeitpunkt der Heute-Linie, Zahl oder Datum (leer = keine)"), o.Str("today", ""),
                    (x, text) =>
                    {
                        var t = (text ?? "").Trim();
                        if (t.Length == 0) return x.WithoutParam("today");
                        if (ChartDates.TryDay(t, out var day)) return x.WithParam("today", ChartDates.Iso(day));
                        return x.WithParam("today", ExportText.N(Fmt.ParseInput(t, lang, -1e12, 1e12)));
                    }) with { Group = "param", ParamKeys = new[] { "today" } },
                Common.GridToggle(o),
            }.Concat(Common.LegendChoice(o));
        }
    }
}
