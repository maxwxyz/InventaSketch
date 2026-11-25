using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Figure (charts v2 stage 2): one chart type for principle and technical figures, driven by a versioned JSON
    /// specification in the parameter <c>spec</c> (<see cref="FigureSpec"/>): stacked panels on a shared x axis, data or
    /// principle axes, curves with gaps, value lines, vertical markers through all panels, point markers, hatched regions,
    /// notes, dimension arrows and reference numerals. A specification that cannot be read renders an empty frame with a
    /// message; rendering never throws.
    /// </summary>
    internal sealed class FigureChart : ChartType
    {
        public const string TypeId = "chart.figure";
        /// <summary>Library category of the figure presets.</summary>
        public const string CategoryId = "chart.figure";

        public static readonly ParamDef SpecDef = Prm.Text("spec", "Figure specification (JSON)", "Bildspezifikation (JSON)", FigurePresets.Default, advanced: true);

        private static readonly I18nText Unreadable = new("Figure specification cannot be read (JSON)", "Bildspezifikation nicht lesbar (JSON)");

        public FigureChart() : base(TypeId, new I18nText("Figure (principle, technical)", "Prinzip- und technische Abbildung"), CategoryId,
            "figure", "principle", "technical figure", "timing diagram", "phase", "panels", "stacked", "reference numeral", "hatched", "dimension", "time gained",
            "bild", "prinzipbild", "abbildung", "strichzeichnung", "zeitdiagramm", "zeitverlauf", "phasen", "bezugszeichen", "schraffur", "zeitgewinn")
        { }

        public override Size2 Design => new(100, 80);

        protected override IReadOnlyList<ParamDef> BuildParams() => new[] { SpecDef };

        internal static string SpecText(SketchObject o) => o.Str("spec", FigurePresets.Default);

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            if (!FigureSpec.TryParse(SpecText(o), out var spec, out _) || spec is null) return ErrorChart(o, ctx);
            try { return new FigureComposer(o, ctx, spec, Design).Compose(); }
            catch (Exception)
            {
                // Rendering must never throw (every host renders on every redraw): an unexpected specification shows the
                // message frame instead.
                return ErrorChart(o, ctx);
            }
        }

        /// <summary>Value lines, markers, axis marks and span edges as drag handles (<see cref="ChartHandles.Figure"/>).</summary>
        internal override IEnumerable<ChartHandle> LocalHandles(SketchObject o, RenderContext ctx)
        {
            if (!FigureSpec.TryParse(SpecText(o), out var spec, out _) || spec is null) return Enumerable.Empty<ChartHandle>();
            Chart c;
            try { c = new FigureComposer(o, ctx, spec, Design).Compose(); }
            catch (Exception) { return Enumerable.Empty<ChartHandle>(); }
            return ChartHandles.Figure(c, spec, ctx.Lang).ToList();
        }

        /// <summary>Empty principle axes with the message that the specification cannot be read.</summary>
        private Chart ErrorChart(SketchObject o, RenderContext ctx)
        {
            var none = Array.Empty<Tick>();
            var panel = new Panel(Axis.Linear(Array.Empty<double>(), (0, 1)), null) { Ticks = none, Arrow = true, Grid = false };
            var f = new Frame(o.Size, Design, Axis.Linear(Array.Empty<double>(), (0, 1)), null, panel)
            {
                XTicks = none, XArrow = true, Grid = false, LegendMode = "none", Mono = Mono(ctx),
            };
            f.Layout(ctx.Lang);
            var c = new Chart(f, ctx.Lang);
            var text = Unreadable.Get(ctx.Lang);
            var a = panel.Area;
            var fs = Math.Max(0.3, Math.Min(f.Fs * 0.9, a.W * 0.9 / (text.Length * Fmt.CharWidth)));
            c.Over.Add(new TextPrim(a.Center, text, new TextStyle { Size = fs, Color = Palette.Helper, Italic = true }) { Tag = ChartTags.Error });
            return c;
        }

        /// <summary>Part names with the texts of the figure ("Reference line 2 – limit temperature", "Reference numeral 44").</summary>
        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            var parts = base.Parts(o, ctx).ToList();
            if (!FigureSpec.TryParse(SpecText(o), out var spec, out _) || spec is null) return parts;
            var numerals = FigureComposer.NumeralIndex(spec).Texts;
            return parts.Select(p =>
            {
                var seg = p.Key.Split('.');
                if (seg.Length != 2 || !int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) || k < 0) return p;
                I18nText? text = seg[0] switch
                {
                    ChartTags.Ref when k < spec.Refs.Count => spec.Refs[k].Label ?? (spec.Refs[k].Numeral is { } rn ? new I18nText(rn.Text) : null),
                    ChartTags.Region when k < spec.Regions.Count => spec.Regions[k].Numeral is { } gn ? new I18nText(gn.Text) : null,
                    ChartTags.Note when k < spec.Notes.Count => spec.Notes[k].Text,
                    _ => null,
                };
                if (seg[0] == ChartTags.Numeral && k < numerals.Count)
                    return p with { Label = new I18nText("Reference numeral " + numerals[k], "Bezugszeichen " + numerals[k]) };
                return text is null ? p : p with { Label = new I18nText(p.Label.En + " – " + text.En, (p.Label.De ?? p.Label.En) + " – " + text.Get(Lang.De)) };
            }).ToList();
        }

        // ------------------------------------------------------------------ actions

        /// <summary>An action that changes the specification (throws a bilingual <see cref="FormatException"/> when it cannot be read).</summary>
        private static ContextAction Edit(string id, I18nText label, string icon, Lang lang, Action<FigureSpec> change, string group = "figure")
            => new(id, label, icon, x =>
            {
                var spec = FigureSpec.Parse(SpecText(x), lang);
                change(spec);
                return x.WithParam("spec", spec.ToJson());
            }) { Group = group };

        private static ContextAction SpecInput(SketchObject o, Lang lang)
            => ContextAction.WithInput("chart.figure.spec", new I18nText("Specification (JSON)…", "Spezifikation (JSON)…"), IconIds.Text,
                new I18nText("Figure specification as JSON (see the charts documentation, section Figure)", "Bildspezifikation als JSON (siehe Diagramm-Dokumentation, Abschnitt Bild)"),
                SpecText(o),
                (x, text) =>
                {
                    FigureSpec.Parse(text, lang);
                    return x.WithParam("spec", (text ?? "").Trim());
                }) with { Group = "data" };

        private static bool CanRegion(FigureSpec? s)
            => s is not null && s.Series.Count >= 2 && FigureComposer.PanelsOf(s) is var ps && Math.Min(ps.Count - 1, Math.Max(0, s.Series[0].Panel)) == Math.Min(ps.Count - 1, Math.Max(0, s.Series[1].Panel));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            FigureSpec.TryParse(SpecText(o), out var spec, out _);
            return new[]
            {
                Edit("chart.figure.hline", new I18nText("Add value line", "Wertlinie hinzufügen"), IconIds.Insert, lang, s => AddHLine(s, 0)),
                Edit("chart.figure.vline", new I18nText("Add vertical marker", "Senkrechte Markierung hinzufügen"), IconIds.Insert, lang, AddVLine),
                Edit("chart.figure.region", new I18nText("Hatch between curves 1 and 2", "Zwischen Kurve 1 und 2 schraffieren"), IconIds.Insert, lang, AddRegion) with { IsEnabled = CanRegion(spec) },
                Edit("chart.figure.principle", new I18nText("Principle axes", "Prinzipachsen"), IconIds.Toggle, lang, TogglePrinciple, "param") with { IsActive = spec?.AllPrinciple == true },
                Edit("chart.figure.mono", new I18nText("Monochrome", "Schwarz-Weiß"), IconIds.Toggle, lang, s => s.Scheme = s.Mono ? "color" : "mono", "param") with { IsActive = spec?.Mono == true },
                SpecInput(o, lang),
            }.Concat(spec is null ? Array.Empty<ContextAction>() : new[]
            {
                BreakInput(o, lang, spec, false, 0), BreakRemove(lang, spec, false, 0),
                BreakInput(o, lang, spec, true, 0), BreakRemove(lang, spec, true, 0),
            });
        }

        // ------------------------------------------------------------------ axis breaks

        /// <summary>The specification axis of the x axis or of the y axis of panel <paramref name="panel"/> (created if needed).</summary>
        private static FigAxis BreakAxis(FigureSpec s, bool x, int panel)
        {
            if (x) return s.X;
            if (s.Panels.Count == 0) s.Panels.Add(new FigPanel());
            return s.Panels[Math.Max(0, Math.Min(s.Panels.Count - 1, panel))];
        }

        private static IReadOnlyList<(double Lo, double Hi)> BreaksOf(FigureSpec s, bool x, int panel)
        {
            if (x) return s.X.Breaks;
            var ps = FigureComposer.PanelsOf(s);
            return ps[Math.Max(0, Math.Min(ps.Count - 1, panel))].Breaks;
        }

        /// <summary>"Axis break here…" on the x axis or the y axis of a panel: the hidden ranges, prefilled with the current ones or a suggestion.</summary>
        private static ContextAction BreakInput(SketchObject o, Lang lang, FigureSpec spec, bool x, int panel)
        {
            var current = BreaksOf(spec, x, panel);
            string value;
            if (current.Count > 0) value = ChartBreaks.Format(current);
            else
            {
                var n = FigureComposer.PanelsOf(spec).Count;
                var axis = x ? FigureComposer.XAxis(spec) : FigureComposer.YAxis(spec, panel);
                var data = x ? spec.Series.SelectMany(se => se.X)
                    : spec.Series.Where(se => Math.Max(0, Math.Min(n - 1, se.Panel)) == panel).SelectMany(se => se.Y);
                value = ChartBreaks.Suggest(axis, data) is { } sug ? ChartBreaks.Format(new[] { sug }) : "";
            }
            var id = x ? "chart.figure.xbreak" : "chart.figure.ybreak";
            return ContextAction.WithInput(id, ChartBreaks.Label(x), IconIds.Text, ChartBreaks.Prompt(x), value,
                (obj, text) => SetSpec(obj, lang, s => BreakAxis(s, x, panel).Breaks = ChartBreaks.ParseInput(text, lang))) with { Group = "breaks" };
        }

        private static ContextAction BreakRemove(Lang lang, FigureSpec spec, bool x, int panel)
            => Edit(x ? "chart.figure.xbreak.remove" : "chart.figure.ybreak.remove", ChartBreaks.RemoveLabel(x), IconIds.Delete, lang,
                s => BreakAxis(s, x, panel).Breaks.Clear(), "breaks") with { IsEnabled = BreaksOf(spec, x, panel).Count > 0 };

        /// <summary>Adds a dashed value line at three quarters of the y range of panel <paramref name="panel"/>.</summary>
        internal static void AddHLine(FigureSpec s, int panel)
        {
            var n = FigureComposer.PanelsOf(s).Count;
            panel = Math.Max(0, Math.Min(n - 1, panel));
            var y = FigureComposer.YAxis(s, panel);
            var v = y.IsLog ? Math.Pow(10, Math.Log10(y.Min) + 0.75 * (Math.Log10(y.Max) - Math.Log10(y.Min))) : y.Min + 0.75 * (y.Max - y.Min);
            s.Refs.Add(new FigRef { Kind = "h", Y = Round(v), Panel = panel == 0 ? null : panel });
        }

        /// <summary>Adds a dashed vertical marker through all panels in the middle of the x range.</summary>
        internal static void AddVLine(FigureSpec s)
        {
            var x = FigureComposer.XAxis(s);
            var v = x.IsLog ? Math.Sqrt(x.Min * x.Max) : (x.Min + x.Max) / 2;
            s.Refs.Add(new FigRef { Kind = "v", X = Round(v) });
        }

        /// <summary>Adds a hatched region between the first two curves (nothing without two curves in one panel).</summary>
        internal static void AddRegion(FigureSpec s)
        {
            if (!CanRegion(s)) return;
            s.Regions.Add(new FigRegion { Kind = "between", A = 0, B = 1 });
        }

        /// <summary>All axes principle axes, or all data axes when they already are.</summary>
        internal static void TogglePrinciple(FigureSpec s)
        {
            var to = s.AllPrinciple ? "data" : "principle";
            if (s.Panels.Count == 0) s.Panels.Add(new FigPanel());
            s.X.Mode = to;
            foreach (var p in s.Panels) p.Mode = to;
        }

        private static double Round(double v) => v == 0 || !Fmt.Finite(v) ? v : Math.Round(v, Math.Max(0, Math.Min(12, 2 - (int)Math.Floor(Math.Log10(Math.Abs(v))))));

        private static readonly (string Value, string En, string De)[] LineStyles =
        {
            ("solid", "solid", "durchgezogen"), ("dash", "dashed", "gestrichelt"), ("dot", "dotted", "gepunktet"), ("dashdot", "dash-dot", "strichpunktiert"),
        };

        private static readonly (string Value, string En, string De)[] Fills =
        {
            ("hatch", "hatched", "schraffiert"), ("cross", "cross-hatched", "kreuzschraffiert"), ("solid", "solid", "flächig"), ("dots", "dotted", "gepunktet"),
        };

        private static (string Value, string En, string De) Next((string Value, string En, string De)[] list, string? current)
        {
            var i = Array.FindIndex(list, x => x.Value == current);
            return list[(i + 1 + list.Length) % list.Length];
        }

        /// <summary>
        /// Actions of one part: curves (line style, markers, smoothing, hatch below), value lines and markers (line style,
        /// label, remove), regions (fill, remove), notes (text, remove), axes (principle/data, add a value line or marker).
        /// </summary>
        protected override IEnumerable<ContextAction> OwnPartActions(SketchObject o, string part, RenderContext ctx)
        {
            var lang = ctx.Lang;
            if (!FigureSpec.TryParse(SpecText(o), out var spec, out _) || spec is null) return new[] { SpecInput(o, lang) };
            var seg = part.Split('.');
            var kind = seg[0];
            int k = seg.Length > 1 && int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;
            var list = new List<ContextAction>();
            switch (kind)
            {
                case ChartTags.Series or ChartTags.Marker or ChartTags.CurveLabel when k >= 0 && k < spec.Series.Count:
                    {
                        var s = spec.Series[k];
                        // Sketched curves first: switch their shape (smoothed, interpolated, fitted, segments, as drawn).
                        list.AddRange(ChartSketchInput.ShapeActions(o, k));
                        var next = Next(LineStyles, s.Line ?? "solid");
                        list.Add(Edit("chart.figure.line", new I18nText("Line: " + next.En, "Linie: " + next.De), IconIds.LineDash, lang, x => x.Series[k].Line = next.Value));
                        list.Add(Edit("chart.figure.markers", new I18nText("Markers", "Marker"), IconIds.Toggle, lang, x => x.Series[k].Marker = x.Series[k].Marker is null ? "circle" : null) with { IsActive = s.Marker is not null });
                        list.Add(Edit("chart.figure.smooth", new I18nText("Smooth curve", "Geglättete Kurve"), IconIds.Toggle, lang, x => x.Series[k].Smooth = !x.Series[k].Smooth) with { IsActive = s.Smooth });
                        list.Add(Edit("chart.figure.below", new I18nText("Hatch below the curve", "Fläche unter der Kurve schraffieren"), IconIds.Insert, lang,
                            x => x.Regions.Add(new FigRegion { Kind = "below", A = k })));
                        if (spec.Series.Count >= 2)
                            list.Add(Edit("chart.figure.region", new I18nText("Hatch between curves 1 and 2", "Zwischen Kurve 1 und 2 schraffieren"), IconIds.Insert, lang, AddRegion) with { IsEnabled = CanRegion(spec) });
                        break;
                    }
                case ChartTags.Ref when k >= 0 && k < spec.Refs.Count:
                    {
                        var r = spec.Refs[k];
                        if (r.Kind != "point")
                        {
                            var next = Next(LineStyles, r.Line ?? "dash");
                            list.Add(Edit("chart.figure.line", new I18nText("Line: " + next.En, "Linie: " + next.De), IconIds.LineDash, lang, x => x.Refs[k].Line = next.Value));
                        }
                        else list.Add(Edit("chart.figure.fill", new I18nText("Filled circle", "Gefüllter Kreis"), IconIds.Toggle, lang, x => x.Refs[k].Filled = !x.Refs[k].Filled) with { IsActive = r.Filled });
                        list.Add(ContextAction.WithInput("chart.figure.label", new I18nText("Label…", "Beschriftung…"), IconIds.Text, new I18nText("Label", "Beschriftung"),
                            r.Label?.Get(lang) ?? "", (x, text) => SetSpec(x, lang, s => s.Refs[k].Label = string.IsNullOrWhiteSpace(text) ? null : new I18nText(text.Trim()))) with { Group = "figure" });
                        list.Add(Edit("chart.figure.remove", new I18nText("Remove", "Entfernen"), IconIds.Delete, lang, x => x.Refs.RemoveAt(k)));
                        break;
                    }
                case ChartTags.Region when k >= 0 && k < spec.Regions.Count:
                    {
                        var next = Next(Fills, spec.Regions[k].Fill);
                        list.Add(Edit("chart.figure.fill", new I18nText("Fill: " + next.En, "Füllung: " + next.De), IconIds.Cycle, lang, x => x.Regions[k].Fill = next.Value));
                        list.Add(Edit("chart.figure.remove", new I18nText("Remove", "Entfernen"), IconIds.Delete, lang, x => x.Regions.RemoveAt(k)));
                        break;
                    }
                case ChartTags.Note when k >= 0 && k < spec.Notes.Count:
                    {
                        var n = spec.Notes[k];
                        list.Add(ContextAction.WithInput("chart.figure.label", new I18nText("Text…", "Text…"), IconIds.Text, new I18nText("Text", "Text"),
                            n.Text?.Get(lang) ?? "", (x, text) => SetSpec(x, lang, s => s.Notes[k].Text = string.IsNullOrWhiteSpace(text) ? null : new I18nText(text.Trim()))) with { Group = "figure" });
                        list.Add(Edit("chart.figure.remove", new I18nText("Remove", "Entfernen"), IconIds.Delete, lang, x => x.Notes.RemoveAt(k)));
                        break;
                    }
                case ChartTags.Axis or ChartTags.Tick or ChartTags.TickLabel or ChartTags.AxisTitle:
                    {
                        var dir = seg.Length > 1 ? seg[1] : "";
                        list.Add(Edit("chart.figure.principle", new I18nText("Principle axes", "Prinzipachsen"), IconIds.Toggle, lang, TogglePrinciple, "param") with { IsActive = spec.AllPrinciple });
                        if (dir == "x")
                        {
                            list.Add(Edit("chart.figure.vline", new I18nText("Add vertical marker", "Senkrechte Markierung hinzufügen"), IconIds.Insert, lang, AddVLine));
                            list.Add(BreakInput(o, lang, spec, true, 0));
                            list.Add(BreakRemove(lang, spec, true, 0));
                        }
                        else if (dir == "y")
                        {
                            // "axis.y" (one panel) or "axis.y.2" (second of stacked panels).
                            var panel = seg.Length > 2 && int.TryParse(seg[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pn) ? pn - 1 : 0;
                            list.Add(Edit("chart.figure.hline", new I18nText("Add value line", "Wertlinie hinzufügen"), IconIds.Insert, lang, x => AddHLine(x, panel)));
                            list.Add(BreakInput(o, lang, spec, false, panel));
                            list.Add(BreakRemove(lang, spec, false, panel));
                        }
                        break;
                    }
                case ChartTags.AxisBreak:
                    {
                        // "axisbreak.x", "axisbreak.y" or "axisbreak.y.2".
                        var x = seg.Length > 1 && seg[1] == "x";
                        var panel = !x && seg.Length > 2 && int.TryParse(seg[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pn) ? pn - 1 : 0;
                        list.Add(BreakInput(o, lang, spec, x, panel));
                        list.Add(BreakRemove(lang, spec, x, panel));
                        break;
                    }
            }
            return list;
        }

        private static SketchObject SetSpec(SketchObject x, Lang lang, Action<FigureSpec> change)
        {
            var spec = FigureSpec.Parse(SpecText(x), lang);
            change(spec);
            return x.WithParam("spec", spec.ToJson());
        }
    }
}
