using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Characteristic chart linked to an element (Calc 5, docs/en/design/engineering-calculations.md section 14): the
    /// parameter <c>link</c> ("&lt;element id&gt;#&lt;set key&gt;") names a curve set the element computes from its solved values
    /// (<see cref="ICharacteristics"/>: pump and system curve, torque–speed, PV I–V and P–V, Bode plot, step response, gear
    /// train diagram, heating curve). The chart is drawn like a figure (<see cref="FigureComposer"/>) from that set at every
    /// render, so it follows every change of the element; the element lists the chart as a dependent. Operating points have
    /// grips: dragging one writes the value into the element (<see cref="IDocumentHandles"/>, one document change). "Unlink"
    /// turns the chart into a plain figure that keeps the curves and points as data.
    /// </summary>
    internal sealed class LinkedChart : ChartType, IDocumentHandles
    {
        public const string TypeId = Characteristics.ChartTypeId;

        public static readonly ParamDef LinkDef = Prm.Text(Characteristics.LinkParam, "Linked element and curve set", "Verknüpftes Element und Kennlinie", advanced: true);

        private static readonly I18nText NotLinked = new("Not linked: use “Link to element”", "Nicht verknüpft: „Mit Element verknüpfen“ wählen");
        private static readonly I18nText Missing = new("Linked element not found: unlink or link again", "Verknüpftes Element fehlt: lösen oder neu verknüpfen");

        public LinkedChart() : base(TypeId, new I18nText("Characteristic chart (linked)", "Kennlinie (verknüpft)"), ChartCategories.Engineering,
            "characteristic", "linked", "operating point", "curve", "kennlinie", "verknüpft", "betriebspunkt")
        { }

        public override Size2 Design => new(100, 70);

        /// <summary>The chart renders from the linked element (another object of the document).</summary>
        public override bool DependsOnNeighbours => true;

        protected override IReadOnlyList<ParamDef> BuildParams() => new[] { LinkDef, Common.Title, Common.Grid, Common.Legend };

        private Registry? RegistryOf(RenderContext ctx) => ctx.Registry ?? Owner;

        /// <summary>The linked curve set in the context's document, or null (no link, element missing, set unknown).</summary>
        internal CharacteristicSet? SetOf(SketchObject o, RenderContext ctx)
            => RegistryOf(ctx) is { } r ? Characteristics.Resolve(r, ctx.Doc, Characteristics.LinkOf(o)) : null;

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var set = SetOf(o, ctx);
            if (set is null) return Message(o, ctx, Characteristics.LinkOf(o) is null ? NotLinked : Missing);
            var (spec, _) = LinkedFigure.Build(set, o);
            try { return new FigureComposer(o, ctx, spec, Design).Compose(); }
            catch (Exception)
            {
                return Message(o, ctx, Missing);
            }
        }

        /// <summary>Empty principle axes with a message (no link, or the element is gone).</summary>
        private Chart Message(SketchObject o, RenderContext ctx, I18nText message)
        {
            var none = Array.Empty<Tick>();
            var panel = new Panel(Axis.Linear(Array.Empty<double>(), (0, 1)), null) { Ticks = none, Arrow = true, Grid = false };
            var f = new Frame(o.Size, Design, Axis.Linear(Array.Empty<double>(), (0, 1)), null, panel)
            {
                XTicks = none, XArrow = true, Grid = false, LegendMode = "none", Mono = Mono(ctx),
            };
            f.Layout(ctx.Lang);
            var c = new Chart(f, ctx.Lang);
            var text = message.Get(ctx.Lang);
            var a = panel.Area;
            var fs = Math.Max(0.3, Math.Min(f.Fs * 0.9, a.W * 0.9 / (text.Length * Fmt.CharWidth)));
            c.Over.Add(new TextPrim(a.Center, text, new TextStyle { Size = fs, Color = Palette.Helper, Italic = true }) { Tag = ChartTags.Error });
            return c;
        }

        // ------------------------------------------------------------------ operating point grips

        /// <summary>One grip per draggable point of the set, on its marker ("op.&lt;point key&gt;").</summary>
        internal override IEnumerable<ChartHandle> LocalHandles(SketchObject o, RenderContext ctx)
        {
            var set = SetOf(o, ctx);
            if (set is null) return Enumerable.Empty<ChartHandle>();
            var (spec, points) = LinkedFigure.Build(set, o);
            Chart c;
            try { c = new FigureComposer(o, ctx, spec, Design).Compose(); }
            catch (Exception) { return Enumerable.Empty<ChartHandle>(); }
            var prims = Prim.Flatten(c.Lines).ToList();
            var list = new List<ChartHandle>();
            foreach (var p in set.Points.Where(p => p.Draggable))
            {
                if (!points.TryGetValue(p.Key, out var k)) continue;
                var tag = ChartTags.Nth(ChartTags.Marker, k);
                var drawn = prims.Where(x => x.Tag == tag).ToList();
                if (drawn.Count == 0 || Prim.Bounds(drawn) is not { } b) continue;
                var value = p.Motion == PointMotion.AlongX ? p.X : p.Y;
                var unit = p.Motion == PointMotion.AlongX ? set.X.Unit : set.Panels[Math.Max(0, Math.Min(set.Panels.Count - 1, p.Panel))].Unit;
                var label = new I18nText(p.Name.En + ": " + Fmt.Num(value, Lang.En, 3) + " " + unit, p.Name.Get(Lang.De) + ": " + Fmt.Num(value, Lang.De, 3) + " " + unit);
                var handle = new DragHandle(HandleKey(p.Key), b.Center, p.Motion == PointMotion.AlongX ? DragConstraint.Horizontal : DragConstraint.Vertical, label) { Part = tag };
                // The chart itself does not change: the element does (MoveHandleInDocument).
                list.Add(new ChartHandle(handle, (x, _) => x));
            }
            return list;
        }

        public static string HandleKey(string pointKey) => "op." + pointKey;

        /// <summary>
        /// Dropping an operating point: the new coordinate (snapped to the axis resolution) is handed to the point, which
        /// returns the values to write; they are stored in the linked element (one document change).
        /// </summary>
        public SketchDocument MoveHandleInDocument(SketchDocument doc, SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            if (!key.StartsWith("op.", StringComparison.Ordinal) || RegistryOf(ctx) is not { } registry) return doc;
            var set = Characteristics.Resolve(registry, doc, Characteristics.LinkOf(o));
            var point = set?.Point(key.Substring(3));
            if (set is null || point is null || !point.Draggable) return doc;
            var (spec, _) = LinkedFigure.Build(set, o);
            Chart c;
            try { c = new FigureComposer(o, new RenderContext(doc, ctx.Lang).For(registry), spec, Design).Compose(); }
            catch (Exception) { return doc; }
            var local = o.Matrix().Invert().Apply(pagePos);
            var f = c.F;
            double value;
            if (point.Motion == PointMotion.AlongX) value = ChartHandles.XValue(f, local);
            else
            {
                var panel = f.Panels[Math.Max(0, Math.Min(f.Panels.Count - 1, point.Panel))];
                value = ChartHandles.YValue(panel, local);
            }
            if (!Fmt.Finite(value)) return doc;
            IReadOnlyList<ValueWrite> writes;
            try { writes = point.Write!(value); }
            catch (Exception ex) when (ex is ArithmeticException or ArgumentException or InvalidOperationException) { return doc; }
            return writes.Count == 0 ? doc : Characteristics.Apply(registry, doc, writes, ctx.Lang);
        }

        // ------------------------------------------------------------------ unlink

        /// <summary>
        /// The chart unlinked: a figure (<see cref="FigureChart"/>) whose specification holds the current curves, points,
        /// bands and limit lines as plain data; position, size and part looks stay. Null when the set cannot be resolved.
        /// </summary>
        internal SketchObject? Frozen(SketchObject o, RenderContext ctx)
        {
            var set = SetOf(o, ctx);
            if (set is null) return null;
            var (spec, _) = LinkedFigure.Build(set, o);
            return o with { Type = FigureChart.TypeId, Params = new Dictionary<string, ParamValue>(StringComparer.Ordinal) { ["spec"] = spec.ToJson() } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[] { Common.GridToggle(o) }.Concat(Common.LegendChoice(o));

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var linked = SetOf(o, ctx) is not null;
            // The registry of the listing context is kept: ContextActions.Run applies with a context without one, and the
            // type's Owner is shared by every registry the (static) chart types are registered with.
            var listed = RegistryOf(ctx);
            yield return ContextAction.InDocument("chart.unlink", new I18nText("Unlink (freeze values)", "Lösen (Werte einfrieren)"), IconIds.Toggle,
                (doc, x, c) => (c.Registry ?? listed ?? Owner) is { } reg && Frozen(x, new RenderContext(doc, c.Lang).For(reg)) is { } frozen ? Edits.Replace(doc, frozen) : doc) with { IsEnabled = linked, Group = "link" };
            foreach (var a in base.Actions(o, ctx)) yield return a;
        }

        // ------------------------------------------------------------------ parts

        /// <summary>Part names with the names of the curves and points of the set.</summary>
        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            var parts = base.Parts(o, ctx).ToList();
            var set = SetOf(o, ctx);
            if (set is null) return parts;
            var (spec, _) = LinkedFigure.Build(set, o);
            return parts.Select(p =>
            {
                var seg = p.Key.Split('.');
                if (seg.Length != 2 || !int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) || k < 0) return p;
                I18nText? text = seg[0] switch
                {
                    ChartTags.Series or ChartTags.Marker when k < spec.Series.Count => spec.Series[k].Label,
                    ChartTags.Ref when k < spec.Refs.Count => spec.Refs[k].Label,
                    ChartTags.Region when k < spec.Regions.Count => spec.Regions[k].Label,
                    _ => null,
                };
                return text is null ? p : p with { Label = seg[0] == ChartTags.Marker ? new I18nText("Markers – " + text.En, "Marker – " + text.Get(Lang.De)) : text };
            }).ToList();
        }
    }

    /// <summary>A curve set (<see cref="CharacteristicSet"/>) as a figure specification (shared by the linked chart and unlink).</summary>
    internal static class LinkedFigure
    {
        /// <summary>The figure of <paramref name="set"/> with the chart's title, grid and legend, and the series index of every point.</summary>
        public static (FigureSpec Spec, Dictionary<string, int> Points) Build(CharacteristicSet set, SketchObject o)
        {
            var grid = o.Flag("grid", true);
            var title = Common.TitleOf(o);
            var s = new FigureSpec
            {
                Title = title is null ? null : new I18nText(title),
                Legend = o.Str("legend", "auto") switch { "none" => "none", "inside" => "inside", "right" => "right", _ => "auto" },
            };
            s.X = Axis(new FigAxis(), set.X, grid);
            foreach (var a in set.Panels) s.Panels.Add((FigPanel)Axis(new FigPanel(), a, grid));
            var n = set.Panels.Count;
            int Panel(int p) => Math.Max(0, Math.Min(n - 1, p));
            foreach (var cv in set.Curves.Take(FigureSpec.MaxSeries / 2))
            {
                // NaN points are gaps (bars of the gear train diagram); a curve needs at least one real point.
                var pts = cv.Points.Take(FigureSpec.MaxPoints).Select(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y) ? p : new Vec2(double.NaN, double.NaN)).ToList();
                if (!pts.Any(p => Fmt.Finite(p.X))) continue;
                s.Series.Add(new FigSeries
                {
                    X = pts.Select(p => p.X).ToList(),
                    Y = pts.Select(p => p.Y).ToList(),
                    Panel = Panel(cv.Panel),
                    Line = cv.Look switch { CurveLook.Load => "dash", CurveLook.Helper => "dot", _ => null },
                    Width = cv.Look == CurveLook.Main ? 0.5 : 0.35,
                    Label = cv.Name,
                });
            }
            foreach (var b in set.Bands.Where(b => Fmt.Finite(b.From) && Fmt.Finite(b.To) && b.To != b.From))
                s.Regions.Add(new FigRegion
                {
                    Kind = "span", From = Math.Min(b.From, b.To), To = Math.Max(b.From, b.To), Panel = b.Panel < 0 ? -1 : Panel(b.Panel),
                    Fill = "solid", Opacity = 0.18, Edge = "dash", Label = b.Name,
                });
            foreach (var l in set.Lines.Where(l => Fmt.Finite(l.Value)))
                s.Refs.Add(l.Vertical
                    ? new FigRef { Kind = "v", X = l.Value, Panel = l.Panel < 0 ? null : Panel(l.Panel), Label = l.Name, Line = "dashdot" }
                    : new FigRef { Kind = "h", Y = l.Value, Panel = l.Panel < 0 ? 0 : Panel(l.Panel), Label = l.Name, Line = "dashdot" });
            var points = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var p in set.Points.Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)))
            {
                points[p.Key] = s.Series.Count;
                s.Series.Add(new FigSeries
                {
                    X = new List<double> { p.X }, Y = new List<double> { p.Y }, Panel = Panel(p.Panel),
                    Marker = "circle", MarkerOpen = p.Open, Color = Palette.Ink, Label = p.Label ?? p.Name, LabelAt = p.X, LabelPos = "right",
                });
            }
            if (set.Notes.Count > 0)
                s.Caption = new I18nText(string.Join("; ", set.Notes.Select(t => t.En)), string.Join("; ", set.Notes.Select(t => t.Get(Lang.De))));
            return (s, points);
        }

        private static FigAxis Axis(FigAxis target, CharacteristicAxis a, bool grid)
        {
            target.Label = a.Label;
            target.Unit = new I18nText(a.Unit);
            target.Scale = a.Log ? "log" : "linear";
            target.Min = a.Min;
            target.Max = a.Max;
            target.Grid = grid;
            return target;
        }
    }

    /// <summary>
    /// "Link to element ▸" of the characteristic charts (pump, torque–speed, Bode, figure, linked): one command per curve set
    /// offered by the elements on the chart's page (nearest first); choosing one turns the chart into a linked chart.
    /// </summary>
    internal static class ChartLinks
    {
        private static readonly ActionChoice Menu = new("chart.link", new I18nText("Link to element", "Mit Element verknüpfen")) { IsMenu = true };

        private static readonly HashSet<string> Linkable = new(StringComparer.Ordinal)
        {
            PumpChart.TypeId, TorqueChart.TypeId, BodeChart.TypeId, FigureChart.TypeId, LinkedChart.TypeId,
        };

        public static IEnumerable<ContextAction> Offer(ChartType type, SketchObject o, RenderContext ctx)
        {
            if (!Linkable.Contains(type.Id)) return Array.Empty<ContextAction>();
            var registry = ctx.Registry ?? type.Owner;
            var page = ctx.Doc.PageOf(o.Id);
            if (registry is null || page is null) return Array.Empty<ContextAction>();
            var current = Characteristics.LinkOf(o);
            var centre = o.Bounds().Center;
            var list = new List<ContextAction>();
            var elements = ctx.Doc.ObjectsOn(page)
                .Where(x => x.Id != o.Id && registry.Type(x.Type) is ICharacteristics)
                .OrderBy(x => (x.IsPointGeometry ? Rect.FromPoints(ctx.Doc.ResolvePoints(x)) : x.Bounds()).Center.DistanceTo(centre))
                .Take(8);
            foreach (var e in elements)
                foreach (var s in Characteristics.SetsOf(registry, e, ctx))
                {
                    var link = Characteristics.Link(e.Id, s.Key);
                    if (link == current) continue;
                    var who = Designation(e, registry);
                    var label = new I18nText(s.Name.En + " – " + who.En, s.Name.Get(Lang.De) + " – " + who.Get(Lang.De));
                    list.Add(ActionChoices.Option(Menu, link, label, IconIds.Option, false, x => LinkTo(x, link)));
                }
            return list;
        }

        /// <summary>The chart linked to <paramref name="link"/>: a linked chart keeps its settings, other charts become one (title kept).</summary>
        public static SketchObject LinkTo(SketchObject chart, string link)
        {
            if (chart.Type == LinkedChart.TypeId) return chart.WithParam(Characteristics.LinkParam, link);
            var ps = new Dictionary<string, ParamValue>(StringComparer.Ordinal) { [Characteristics.LinkParam] = link };
            if (Common.TitleOf(chart) is { } title) ps["title"] = title;
            return chart with { Type = LinkedChart.TypeId, Params = ps };
        }

        /// <summary>How the element is named in the menu: its designation or tag, else its type name.</summary>
        private static I18nText Designation(SketchObject e, Registry registry)
        {
            foreach (var key in new[] { "ref", "tag", "name", "label" })
                if (e.Params.TryGetValue(key, out var v) && v.Text is { Length: > 0 } t && t.Trim().Length > 0) return new I18nText(t.Trim());
            return registry.Type(e.Type)?.Name ?? new I18nText(e.Type);
        }
    }
}
