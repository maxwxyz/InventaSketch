using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Layout of a chart without Cartesian axes (pie, polar, Sankey) inside its box, local coordinates centred at (0, 0):
    /// title on top, an optional legend at the right, the drawing area in between. Text sizes follow the box scale like
    /// <see cref="Frame"/> (<see cref="Fs"/>), so free scaling changes the size, never the data.
    /// </summary>
    internal sealed class Canvas
    {
        public Canvas(Size2 size, Size2 design, Lang lang, bool mono)
        {
            W = Math.Max(size.W, 1); H = Math.Max(size.H, 1);
            K = Math.Min(W / design.W, H / design.H);
            Fs = Geo.Clamp(2.8 * K, 0.8, 8);
            Lang = lang; Mono = mono;
        }

        public double W { get; }
        public double H { get; }
        /// <summary>Detail scale (box size relative to the design size).</summary>
        public double K { get; }
        /// <summary>Font size of labels (mm).</summary>
        public double Fs { get; }
        public Lang Lang { get; }
        public bool Mono { get; }
        public string? Title { get; set; }
        /// <summary>Legend mode: auto (shown with two or more entries), right, none.</summary>
        public string LegendMode { get; set; } = "auto";
        public List<LegendEntry> Legend { get; } = new();
        /// <summary>The drawing area (after <see cref="Layout"/>).</summary>
        public Rect Area { get; private set; }
        /// <summary>Fills behind everything (grid, slices, flows).</summary>
        public List<Primitive> Back { get; } = new();
        /// <summary>Data drawn above the fills (lines, nodes, markers).</summary>
        public List<Primitive> Main { get; } = new();
        /// <summary>Labels above the data.</summary>
        public List<Primitive> Over { get; } = new();
        public List<Anchor> Anchors { get; } = new();
        /// <summary>What the chart was drawn from (for part names), set by the chart type.</summary>
        public object? Model { get; set; }

        public double Pad => 0.35 * Fs;
        public double Gap => 0.3 * Fs;
        private double Row => Fs * 1.3;
        private int _ports;
        private double _legendFs;
        private Rect _legendBox;

        public bool ShowLegend => Legend.Count > 0 && (LegendMode is "right" or "inside" || LegendMode == "auto" && Legend.Count >= 2);

        /// <summary>Computes the drawing area (title on top, legend at the right).</summary>
        public void Layout()
        {
            double hw = W / 2, hh = H / 2;
            var top = Pad + (string.IsNullOrEmpty(Title) ? 0 : Fs * 1.3 + Gap);
            var right = Pad;
            if (ShowLegend)
            {
                var fs = Fs * 0.9;
                var sample = Fs * 1.6;
                var frame = Fs * 0.4 + sample + Gap + Fs * 0.4;
                var text = Legend.Max(e => Fmt.Width(e.Text, fs));
                var lw = frame + text;
                var max = W * 0.42;
                if (lw > max)
                {
                    fs *= Math.Max(0.05, (max - frame) / Math.Max(text, 1e-9));
                    lw = max;
                }
                _legendFs = Math.Max(fs, 0.05);
                var lh = Math.Min(Legend.Count * Row + Fs * 0.5, Math.Max(H - top - Pad, Row));
                _legendBox = new Rect(hw - Pad - lw, -hh + top, lw, lh);
                right += lw + Gap;
            }
            Area = new Rect(-hw + Pad, -hh + top, Math.Max(W - Pad - right, 1), Math.Max(H - top - Pad, 1));
        }

        /// <summary>A port (at most 160 per chart, only inside the box).</summary>
        public void Port(Vec2 p, string id)
        {
            if (_ports >= 160 || !Frame.Ok(p) || Math.Abs(p.X) > W / 2 + 1e-6 || Math.Abs(p.Y) > H / 2 + 1e-6) return;
            _ports++;
            Anchors.Add(Anchor.Point(AnchorRole.Port, p, id));
        }

        /// <summary>Corners and centre of the drawn figure ("plot.tl" … "plot.c"), like the plot anchors of the axis charts.</summary>
        public void PlotAnchors(Rect r)
        {
            Anchors.Add(Anchor.Point(AnchorRole.Corner, new Vec2(r.X, r.Y), "plot.tl"));
            Anchors.Add(Anchor.Point(AnchorRole.Corner, new Vec2(r.Right, r.Y), "plot.tr"));
            Anchors.Add(Anchor.Point(AnchorRole.Corner, new Vec2(r.Right, r.Bottom), "plot.br"));
            Anchors.Add(Anchor.Point(AnchorRole.Corner, new Vec2(r.X, r.Bottom), "plot.bl"));
            Anchors.Add(Anchor.Point(AnchorRole.Center, r.Center, "plot.c"));
        }

        /// <summary>Font size so <paramref name="text"/> fits <paramref name="width"/> (never above <paramref name="fs"/>).</summary>
        public static double Fit(string text, double fs, double width)
            => Math.Max(0.05, Math.Min(fs, Math.Max(width, 0.05) / (Math.Max(1, text.Length) * Fmt.CharWidth)));

        /// <summary>A text that never leaves the box: its size shrinks to the room between its anchor and the box edge.</summary>
        public TextPrim Text(Vec2 at, string text, double fs, TextAnchor anchor, string tag, string? color = null)
        {
            double hw = W / 2 - 0.02, hh = H / 2 - 0.02;
            var room = anchor switch
            {
                TextAnchor.Start => hw - at.X,
                TextAnchor.End => at.X + hw,
                _ => 2 * Math.Min(hw - at.X, at.X + hw),
            };
            var size = Fit(text, fs, room);
            size = Math.Min(size, Math.Max(0.05, 2 * Math.Min(hh - at.Y, at.Y + hh)));
            return new TextPrim(at, text, new TextStyle { Size = size, Anchor = anchor, Color = color ?? Palette.Ink }) { Tag = tag };
        }

        public IReadOnlyList<Primitive> Render()
        {
            var list = new List<Primitive>();
            list.AddRange(Back);
            list.AddRange(Main);
            list.AddRange(Over);
            if (!string.IsNullOrEmpty(Title))
            {
                var fs = Fit(Title!, Fs * 1.1, W - 2 * Pad);
                list.Add(new TextPrim(new Vec2(0, -H / 2 + Pad + Fs * 0.65), Title!, new TextStyle { Size = fs, Bold = true, Color = Palette.Ink }) { Tag = ChartTags.Title });
            }
            DrawLegend(list);
            return list;
        }

        private void DrawLegend(List<Primitive> into)
        {
            if (!ShowLegend) return;
            var box = _legendBox;
            into.Add(Prim.Rect(box.X, box.Y, box.W, box.H, S.LegendBox) with { Tag = ChartTags.Legend });
            var sample = Math.Min(Fs * 1.6, box.W * 0.3);
            for (var i = 0; i < Legend.Count; i++)
            {
                var e = Legend[i];
                var cy = box.Y + Fs * 0.25 + Row * (i + 0.5);
                if (cy + Row * 0.4 > box.Bottom) break;
                var x0 = box.X + Fs * 0.4;
                if (e.Area)
                {
                    var h = Math.Min(Row * 0.65, Fs * 0.9);
                    into.Add(Prim.Rect(x0, cy - h / 2, sample, h, e.Style) with { Tag = ChartTags.LegendSample });
                }
                else
                {
                    into.Add(Prim.Line(new Vec2(x0, cy), new Vec2(x0 + sample, cy), e.Style) with { Tag = ChartTags.LegendSample });
                    if (e.Marker is MarkerKind mk)
                        into.Add(Draw.Markers(new[] { new Vec2(x0 + sample / 2, cy) }, mk, e.FilledMarker, e.Style.EffectiveStroke, Fs * 0.35) with { Tag = ChartTags.LegendSample });
                }
                var tx = x0 + sample + Gap;
                var size = Fit(e.Text, _legendFs, box.Right - Fs * 0.2 - tx);
                into.Add(new TextPrim(new Vec2(tx, cy), e.Text, new TextStyle { Size = size, Anchor = TextAnchor.Start, Color = Palette.Ink }) { Tag = ChartTags.LegendText });
            }
        }
    }

    /// <summary>
    /// Base of the chart types without Cartesian axes (pie, polar, Sankey): drawn on a <see cref="Canvas"/>, never throwing
    /// while rendering, with every tagged piece a part. They have no axis frame: pie and polar charts have their own code
    /// export models (<see cref="CanvasExport"/>), the Sankey diagram is reported as not exportable by the matplotlib and
    /// pgfplots exports (the TikZ and SVG exports draw it from the primitives).
    /// </summary>
    internal abstract class CanvasChart : ChartType
    {
        protected CanvasChart(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        internal sealed override Chart Compose(SketchObject o, RenderContext ctx) => throw NotExportable(Name, ctx.Lang);

        /// <summary>The exception of the code exports for a chart that has no matplotlib/pgfplots form yet.</summary>
        internal static InvalidOperationException NotExportable(I18nText name, Lang lang)
            => new(lang == Lang.De
                ? name.Get(Lang.De) + " lässt sich nicht als matplotlib- oder pgfplots-Code ausgeben; bitte als TikZ oder SVG exportieren"
                : name.En + " cannot be exported as matplotlib or pgfplots code; export it as TikZ or SVG instead");

        /// <summary>Lays out and draws the chart onto the canvas (title and legend mode are set already).</summary>
        protected abstract void Paint(SketchObject o, RenderContext ctx, Canvas cv);

        /// <summary>The drawn chart; a broken chart shows "?" instead of throwing (hosts render on every redraw).</summary>
        internal Canvas DrawCanvas(SketchObject o, RenderContext ctx)
        {
            var cv = new Canvas(o.Size, Design, ctx.Lang, Mono(ctx)) { Title = Common.TitleOf(o), LegendMode = o.Str("legend", "auto") };
            try
            {
                Paint(o, ctx, cv);
                return cv;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException or FormatException or OverflowException)
            {
                var d = new Canvas(o.Size, Design, ctx.Lang, Mono(ctx));
                d.Over.Add(new TextPrim(new Vec2(0, 0), "?", new TextStyle { Size = d.Fs, Color = Palette.Helper }) { Tag = ChartTags.Error });
                d.PlotAnchors(new Rect(-d.W / 2, -d.H / 2, d.W, d.H));
                return d;
            }
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => DrawCanvas(o, ctx).Render();

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => DrawCanvas(o, ctx).Anchors.ToList();

        protected static string KindOf(string key) => key.IndexOf('.') is var i and > 0 ? key.Substring(0, i) : key;

        protected static int IndexOf(string key)
            => key.Split('.') is { Length: 2 } seg && int.TryParse(seg[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : -1;

        /// <summary>Display name of a part (default: the shared chart part names with the legend texts).</summary>
        protected virtual I18nText PartName(string key, Canvas cv)
            => PartLabel(key, cv.Legend.Where(e => !e.Area).Select(e => e.Text).ToList(), cv.Legend.Where(e => e.Area).Select(e => e.Text).ToList(), cv.Lang);

        /// <summary>Sort rank of a part kind in the parts list (data first, then labels, axes, grid, legend, title).</summary>
        protected virtual int PartOrder(string kind) => kind switch
        {
            ChartTags.Slice or ChartTags.Node or ChartTags.Series => 0,
            ChartTags.Flow or ChartTags.Marker => 1,
            ChartTags.SliceLabel or ChartTags.NodeLabel or ChartTags.CenterText => 2,
            ChartTags.Axis => 4, ChartTags.TickLabel => 5, "grid" => 8, ChartTags.Legend => 9, ChartTags.Title => 10, _ => 11,
        };

        /// <summary>Every tagged piece as a part; the legend pieces form one part.</summary>
        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            var cv = DrawCanvas(o, ctx);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<PartInfo>();
            foreach (var p in Prim.Flatten(cv.Render()))
            {
                if (p.Tag is not { Length: > 0 } tag || tag == ChartTags.Error) continue;
                var key = ChartTags.Is(tag, ChartTags.Legend) ? ChartTags.Legend : tag;
                if (seen.Add(key)) list.Add(new PartInfo(key, PartName(key, cv), KindOf(key)));
            }
            return list.OrderBy(p => PartOrder(p.Kind)).ToList();
        }
    }
}
