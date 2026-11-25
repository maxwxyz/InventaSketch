using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A stroke of an exported item: colour (#rrggbb), width (mm), dash pattern and optional opacity.</summary>
    internal sealed record XStroke(string Color, double Width, DashStyle Dash, double? Opacity = null);

    /// <summary>A marker of an exported series: kind (circle, square, triangle, diamond, cross, plus), filled, colour, radius (mm).</summary>
    internal sealed record XMarker(string Kind, bool Filled, string Color, double Size);

    /// <summary>A named tick of an axis (principle axes, figure marks).</summary>
    internal sealed record XMark(double At, string Text);

    /// <summary>
    /// Fill of a region or a bar: solid, hatch, cross, dots, none or "pattern" (a matplotlib hatch pattern, <see cref="HatchCode"/>);
    /// hatch angle (°, counter-clockwise), spacing (mm), colour, opacity; a solid face under a pattern and an edge line.
    /// </summary>
    internal sealed record XFillStyle(string Kind, string Color, double Angle = 45, double Spacing = 1.2, double? Opacity = null)
    {
        /// <summary>The matplotlib hatch pattern ("//", "x.", "o") of a "pattern" fill.</summary>
        public string? HatchCode { get; init; }
        /// <summary>Solid face colour under a pattern, or null.</summary>
        public string? Face { get; init; }
        /// <summary>Edge line of the area, or null.</summary>
        public XStroke? Edge { get; init; }
    }

    /// <summary>An axis of an exported chart: scale, range, ticks, title, grid and the visibility of its pieces.</summary>
    internal sealed class XAxis
    {
        public string? Title { get; set; }
        public AxisKind Kind { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        /// <summary>Major tick step of a linear axis (0 = automatic).</summary>
        public double Step { get; set; }
        /// <summary>Category labels (category axes: category i is centred at i, the range is −0.5 … n − 0.5).</summary>
        public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
        /// <summary>True if the first category lies at the end (horizontal bars: first category at the top).</summary>
        public bool Reversed { get; set; }
        /// <summary>Explicit ticks with labels (named marks); null = automatic ticks, empty = no ticks.</summary>
        public List<XMark>? Marks { get; set; }
        /// <summary>Arrowhead at the end of the axis line (principle axes).</summary>
        public bool Arrow { get; set; }
        public bool GridMajor { get; set; }
        public bool GridMinor { get; set; }
        public bool LineHidden { get; set; }
        public bool TicksHidden { get; set; }
        public bool TickLabelsHidden { get; set; }

        public bool IsLog => Kind == AxisKind.Log;
        public bool IsCategory => Kind == AxisKind.Category;

        /// <summary>Data value at the fraction <paramref name="frac"/> (0 … 1) of the axis.</summary>
        public double At(double frac) => IsLog
            ? Math.Pow(10, Math.Log10(Min) + frac * (Math.Log10(Max) - Math.Log10(Min)))
            : Min + frac * (Max - Min);
    }

    /// <summary>A stacked panel: its y axis, an optional secondary y axis (right) and its relative height.</summary>
    internal sealed class XPanel
    {
        public XAxis Y { get; set; } = new();
        public XAxis? Y2 { get; set; }
        public double Share { get; set; } = 1;
        /// <summary>Plot area size of the panel as drawn (mm).</summary>
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        /// <summary>Gap to the next panel as drawn (mm).</summary>
        public double GapMm { get; set; }
    }

    /// <summary>An item of an exported chart in data coordinates of its panel; <see cref="Key"/> is its part key.</summary>
    internal abstract class XItem
    {
        public int Panel { get; set; }
        public string Key { get; set; } = "";
    }

    /// <summary>A curve: points (NaN = gap), line and/or markers, steps, legend label, secondary axis.</summary>
    internal sealed class XSeries : XItem
    {
        public List<double> X { get; set; } = new();
        public List<double> Y { get; set; } = new();
        public XStroke? Line { get; set; }
        public XMarker? Marker { get; set; }
        /// <summary>Each value is held until the next x (steps-post / const plot).</summary>
        public bool Step { get; set; }
        public string? Legend { get; set; }
        public bool Y2 { get; set; }
    }

    /// <summary>A horizontal value line across the panel.</summary>
    internal sealed class XHLine : XItem
    {
        public double Y { get; set; }
        public XStroke Stroke { get; set; } = null!;
    }

    /// <summary>A vertical marker line from the top of <see cref="XItem.Panel"/> to the bottom of <see cref="LastPanel"/>.</summary>
    internal sealed class XVLine : XItem
    {
        public double X { get; set; }
        public int LastPanel { get; set; }
        public XStroke Stroke { get; set; } = null!;
    }

    /// <summary>
    /// A filled region: "between" two curves on a common x grid (<see cref="Lo"/>, <see cref="Hi"/>, optionally only where
    /// <see cref="Where"/> is true), or a "span" between two x values. <see cref="Polygons"/> are the filled areas in data
    /// coordinates (gaps and the where-mask already applied).
    /// </summary>
    internal sealed class XFill : XItem
    {
        public string Kind { get; set; } = "between";
        public List<double> X { get; set; } = new();
        public List<double> Lo { get; set; } = new();
        public List<double> Hi { get; set; } = new();
        public List<bool>? Where { get; set; }
        /// <summary>The where-mask keeps curve A below (left of) the other edge; false: above (right of).</summary>
        public bool WhereBelow { get; set; }
        /// <summary>Steps of the edges (pre, post, mid) for fill_between step=; null: straight.</summary>
        public string? Step { get; set; }
        /// <summary>hspan: lower and upper y.</summary>
        public double Y0 { get; set; }
        public double Y1 { get; set; }
        /// <summary>hspan over the whole x range (axhspan); else from <see cref="X0"/> to <see cref="X1"/>.</summary>
        public bool FullWidth { get; set; }
        /// <summary>Description of the where-mask for comments ("curve 2 above curve 3").</summary>
        public string? WhereText { get; set; }
        public List<List<Vec2>> Polygons { get; set; } = new();
        public double X0 { get; set; }
        public double X1 { get; set; }
        /// <summary>The panels a span covers.</summary>
        public List<int> Panels { get; set; } = new();
        public XFillStyle Fill { get; set; } = null!;
        public string? Legend { get; set; }
    }

    /// <summary>
    /// A text at a point (data coordinates; <see cref="XFrac"/>/<see cref="YFrac"/>: fraction of the axis instead), shifted
    /// by (<see cref="Dx"/>, <see cref="Dy"/>) points (y up), aligned left/center/right and top/center/bottom.
    /// </summary>
    internal sealed class XLabel : XItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool XFrac { get; set; }
        public bool YFrac { get; set; }
        public double Dx { get; set; }
        public double Dy { get; set; }
        public string HAlign { get; set; } = "center";
        public string VAlign { get; set; } = "center";
        public string Text { get; set; } = "";
        public bool Italic { get; set; }
        public bool Bold { get; set; }
        /// <summary>White backing behind the text.</summary>
        public bool Box { get; set; }
        /// <summary>Text size relative to the chart font.</summary>
        public double Size { get; set; } = 1;
        public string Color { get; set; } = Palette.Ink;
    }

    /// <summary>A reference numeral: text shifted by (Dx, Dy) font heights (y up) from the target, with a thin leader line to it.</summary>
    internal sealed class XLeader : XItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool XFrac { get; set; }
        public bool YFrac { get; set; }
        public double Dx { get; set; }
        public double Dy { get; set; }
        public string Text { get; set; } = "";
        public string Color { get; set; } = Palette.Ink;
    }

    /// <summary>A line, arrow (to B) or dimension arrow (both ends) from A in <see cref="XItem.Panel"/> to B in <see cref="Panel2"/>.</summary>
    internal sealed class XSegment : XItem
    {
        public int Panel2 { get; set; }
        public Vec2 A { get; set; }
        public Vec2 B { get; set; }
        /// <summary>line, arrow, dim.</summary>
        public string Kind { get; set; } = "line";
        public XStroke Stroke { get; set; } = null!;
    }

    /// <summary>Bars of a bar chart: category i is centred at i; every series has its own offset, width and base values.</summary>
    internal sealed class XBars
    {
        public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
        public bool Stacked { get; set; }
        public bool Horizontal { get; set; }
        public double Width { get; set; }
        public List<XBarSeries> Series { get; } = new();
    }

    internal sealed class XBarSeries : XItem
    {
        public string Name { get; set; } = "";
        public List<double> Values { get; set; } = new();
        public List<double> Bottoms { get; set; } = new();
        public double Offset { get; set; }
        public XFillStyle Fill { get; set; } = null!;
        public string Edge { get; set; } = Palette.Black;
        public double EdgeWidth { get; set; } = 0.25;
        public bool InLegend { get; set; }
        /// <summary>Value labels (null = none; an empty text = no label at that bar).</summary>
        public List<string>? Labels { get; set; }
    }

    /// <summary>Contour lines of a field Z[i, j] over X[i], Y[j] (NaN = outside), with the traced pieces and label positions.</summary>
    internal sealed class XContours : XItem
    {
        public double[] X { get; set; } = Array.Empty<double>();
        public double[] Y { get; set; } = Array.Empty<double>();
        public double[,] Z { get; set; } = new double[0, 0];
        public List<double> Levels { get; set; } = new();
        public XStroke Stroke { get; set; } = null!;
        public bool Labels { get; set; }
        public string LabelSuffix { get; set; } = "";
        public List<(double Level, List<Vec2> Points)> Pieces { get; } = new();
        public List<XLabel> LabelTexts { get; } = new();
    }

    /// <summary>
    /// A chart prepared for export as code (charts v2 stage 4): figure size, title, caption, the shared x axis, the stacked
    /// panels and every item in data coordinates. Built from the chart models (data, specification) and the composed frame
    /// (axis ranges, ticks, layout); part looks are already applied (hidden parts left out, styles overridden).
    /// </summary>
    internal sealed class XFigure
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        /// <summary>Chart font size (mm).</summary>
        public double FontMm { get; set; } = 2.8;
        public Lang Lang { get; set; }
        public bool Mono { get; set; }
        public bool Box { get; set; }
        public string? Title { get; set; }
        public string? Caption { get; set; }
        /// <summary>none, inside or right.</summary>
        public string Legend { get; set; } = "none";
        /// <summary>Corner of an inside legend: upper right, upper left, lower right, lower left.</summary>
        public string LegendCorner { get; set; } = "upper right";
        public XAxis X { get; set; } = new();
        public List<XPanel> Panels { get; } = new();
        public XStroke GridStroke { get; set; } = new(Palette.Grid, 0.13, DashStyle.Solid);
        public XStroke MinorGridStroke { get; set; } = new(Palette.GridMinor, 0.1, DashStyle.Dot);
        public List<XItem> Items { get; } = new();
        public XBars? Bars { get; set; }
        public List<XLabel> PanelTitles { get; } = new();
        /// <summary>Remarks written as comments at the start of the chart (what the export cannot reproduce, e.g. axis breaks).</summary>
        public List<string> Notes { get; } = new();
    }
}
