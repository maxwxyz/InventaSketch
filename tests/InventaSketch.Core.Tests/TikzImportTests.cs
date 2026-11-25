using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;
using K = InventaSketch.Core.TikzImport.ShapeKind;

namespace InventaSketch.Core.Tests
{
    /// <summary>Roadmap 4b: TikZ drawings read (never executed) into neutral shapes in mm, y down (<see cref="TikzImport"/>).</summary>
    public class TikzImportTests
    {
        private static void Near(double expected, double actual, double tol = 1e-3) => Assert.InRange(actual, expected - tol, expected + tol);

        private static TikzImport.Drawing Parse(string tex, out CodeImportReport report)
        {
            report = new CodeImportReport();
            return TikzImport.Parse(tex, report);
        }

        [Fact]
        public void LinesArrowsColoursWidthsAndDashesAreRead()
        {
            const string tex = @"\documentclass{standalone}
\usepackage{tikz}
\definecolor{brand}{HTML}{1F4E9A}
\begin{document}
\begin{tikzpicture}
  \draw[->, brand, very thick, dashed] (0,0) -- (2,1);
  \draw[{Stealth[length=2mm]}-{Stealth}, line width=0.5mm, red!50] (0,2) -- (3,2);
  \draw[dash pattern=on 0.1mm off 1mm] (0,0) -- (1,0) -- (1,1);
\end{tikzpicture}
\end{document}";
            var d = Parse(tex, out var report);
            Assert.Empty(report.Skipped);
            Assert.Equal(new[] { K.Line, K.Line, K.Polyline }, d.Shapes.Select(s => s.Kind));
            var a = d.Shapes[0];
            // cm units, y up in TikZ → y down on the page.
            Near(20, a.Points[1].X); Near(-10, a.Points[1].Y);
            Assert.Equal(ArrowHead.None, a.HeadStart);
            Assert.Equal(ArrowHead.Open, a.HeadEnd);
            Assert.Equal("#1f4e9a", a.Stroke);
            Near(1.2 * 25.4 / 72.27, a.Width!.Value);
            Assert.Equal(DashStyle.Dash, a.Dash);
            var b = d.Shapes[1];
            Assert.Equal(ArrowHead.Stealth, b.HeadStart); Assert.Equal(ArrowHead.Stealth, b.HeadEnd);
            Assert.Equal("#ff8080", b.Stroke);
            Near(0.5, b.Width!.Value);
            Assert.Equal(DashStyle.Dot, d.Shapes[2].Dash);
            Assert.Equal(3, d.Shapes[2].Points.Count);
        }

        [Fact]
        public void RectanglesCirclesEllipsesArcsAndCurvesAreRead()
        {
            const string tex = @"\begin{tikzpicture}[x=1mm, y=-1mm]
  \draw[fill=blue!20, rounded corners=1mm] (10,10) rectangle (40,30);
  \filldraw[fill=yellow, draw=black] (50,20) circle (5);
  \draw (70,20) ellipse [x radius=8, y radius=4];
  \draw (80,20) arc[start angle=0, end angle=180, radius=10];
  \draw (0,50) .. controls (10,40) and (20,60) .. (30,50) -- cycle;
  \fill[pattern=north east lines] (0,0) -- (5,0) -- (5,5) -- cycle;
\end{tikzpicture}";
            var d = Parse(tex, out var report);
            Assert.Empty(report.Skipped);
            Assert.Equal(new[] { K.Rect, K.Ellipse, K.Ellipse, K.Polyline, K.Polyline, K.Polyline }, d.Shapes.Select(s => s.Kind));
            var r = d.Shapes[0];
            // x=1mm, y=-1mm: the page coordinates of the TikZ export (y down).
            Near(10, r.Box.X); Near(10, r.Box.Y); Near(30, r.Box.W); Near(20, r.Box.H);
            Near(1, r.Radius);
            Assert.Equal(FillKind.Solid, r.Fill!.Kind); Assert.Equal("#ccccff", r.Fill.Color);
            Assert.Equal("#000000", r.Stroke);
            var c = d.Shapes[1];
            Near(45, c.Box.X); Near(10, c.Box.W);
            Assert.Equal("#ffff00", c.Fill!.Color);
            Near(16, d.Shapes[2].Box.W); Near(8, d.Shapes[2].Box.H);
            var arc = d.Shapes[3];
            Near(60, arc.Points[arc.Points.Count - 1].X);
            Near(20, arc.Points[arc.Points.Count - 1].Y);
            Assert.True(d.Shapes[4].Closed);
            Assert.True(d.Shapes[4].Points.Count > 10);
            var hatch = d.Shapes[5];
            Assert.Null(hatch.Stroke);
            Assert.Equal(FillKind.Hatch, hatch.Fill!.Kind);
            Near(45, hatch.Fill.Hatch!.Angle);
        }

        [Fact]
        public void NodesBecomeTextsWithPositionSizeAndStyle()
        {
            const string tex = @"\begin{tikzpicture}
  \node[anchor=west, font=\small, text=red] at (1,1) {\textbf{Pump} $\eta_{max}$ 5\,\%};
  \node (A) at (0,0) {A};
  \draw (A) -- (2,0) node[midway, above] {x\textsuperscript{2}};
  \node[draw, circle] at (3,3) {1};
\end{tikzpicture}";
            var d = Parse(tex, out var report);
            Assert.Empty(report.Skipped);
            var texts = d.Shapes.Where(s => s.Kind == K.Text).ToList();
            Assert.Equal(4, texts.Count);
            var t = texts[0];
            Assert.Equal("Pump η_max 5 %", t.Text);
            Assert.True(t.Bold);
            Assert.Equal(TextAnchor.Start, t.Anchor);
            Assert.Equal("#ff0000", t.TextColor);
            Near(9 * 25.4 / 72.27, t.TextSize);
            Near(10, t.Points[0].X);
            Assert.Equal("x²", texts[2].Text);
            Near(10, texts[2].Points[0].X);           // midway
            Assert.True(texts[2].Points[0].Y < 0);    // above the line (y down)
            Assert.Contains(d.Shapes, s => s.Kind == K.Ellipse);   // the circle node's circle
        }

        [Fact]
        public void ScopesShiftScaleAndRotate()
        {
            const string tex = @"\begin{tikzpicture}
  \begin{scope}[shift={(1,0)}, scale=2]
    \draw (0,0) -- (1,0);
  \end{scope}
  \begin{scope}[rotate=90]
    \draw (0,0) rectangle (1,2);
  \end{scope}
  \draw (0,0) -- ++(1,0) -- +(0,1) -- (5,5);
\end{tikzpicture}";
            var d = Parse(tex, out var report);
            Assert.Empty(report.Skipped);
            Near(10, d.Shapes[0].Points[0].X); Near(30, d.Shapes[0].Points[1].X);
            // A rotated rectangle becomes a closed polyline.
            Assert.Equal(K.Polyline, d.Shapes[1].Kind);
            Assert.True(d.Shapes[1].Closed);
            Near(-20, d.Shapes[1].Points[2].X, 1e-6); Near(-10, d.Shapes[1].Points[2].Y, 1e-6);
            var rel = d.Shapes[2].Points;
            Near(10, rel[1].X); Near(10, rel[2].X); Near(-10, rel[2].Y); Near(50, rel[3].X);
        }

        [Fact]
        public void UnsupportedCommandsAreReportedNotGuessed()
        {
            const string tex = @"\begin{tikzpicture}
  \foreach \i in {1,...,3} { \draw (\i,0) -- (\i,1); }
  \draw (0,0) to[bend left] (1,1);
  \draw ($(0,0)!0.5!(1,1)$) -- (2,2);
  \pic {angle};
  \draw[unknowncolor] (0,0) -- (1,0);
  \draw (0,0) -- (1,1);
\end{tikzpicture}";
            var d = Parse(tex, out var report);
            var lines = report.Skipped.Select(s => s.Line).ToList();
            Assert.Contains(2, lines);
            Assert.Contains(3, lines);
            Assert.Contains(4, lines);
            Assert.Contains(5, lines);
            Assert.All(report.Skipped, s => Assert.False(string.IsNullOrEmpty(s.Reason.Get(Lang.De))));
            // The plain lines are imported (an unknown colour name keeps the default colour).
            Assert.Equal(2, d.Shapes.Count);
        }

        [Fact]
        public void PicturesWithAPgfplotsAxisAreLeftToThePgfplotsImport()
        {
            const string tex = @"\begin{tikzpicture}\begin{axis}\addplot coordinates {(0,0) (1,1)};\end{axis}\end{tikzpicture}
\begin{tikzpicture}\draw (0,0) -- (1,1);\end{tikzpicture}";
            var d = Parse(tex, out var report);
            Assert.Single(d.Shapes);
            Assert.Empty(report.Skipped);
            var r2 = new CodeImportReport();
            TikzImport.Parse(tex, r2, skipAxisPictures: false);
            Assert.Single(r2.Skipped);
        }

        [Fact]
        public void TheFormatIsDetectedFromTheContent()
        {
            Assert.Equal("pgfplots", CodeImportFormat.Detect(@"\begin{axis}\addplot {x};\end{axis}"));
            Assert.Equal("tikz", CodeImportFormat.Detect(@"\begin{tikzpicture}\draw (0,0) -- (1,1);\end{tikzpicture}"));
            Assert.Equal("matplotlib", CodeImportFormat.Detect("import matplotlib.pyplot as plt\nplt.plot([1, 2])"));
            Assert.Equal("matplotlib", CodeImportFormat.Detect("fig, ax = plt.subplots()\nax.plot(x, y)"));
            Assert.Null(CodeImportFormat.Detect("Hello world"));
            Assert.Equal("tikz", CodeImportFormat.FromHint("TikZ"));
            Assert.Null(CodeImportFormat.FromHint("auto"));
        }

        [Fact]
        public void LatexTextIsConvertedToPlainText()
        {
            Assert.Equal("Temperature ϑ in °C", TikzImport.PlainText(@"Temperature $\vartheta$ in \textdegree{}C", out _, out _));
            Assert.Equal("line 1\nline 2", TikzImport.PlainText(@"line 1\\ line 2", out _, out _));
            Assert.Equal("V̇ in m³/h", TikzImport.PlainText(@"$\dot{V}$ in m\textsuperscript{3}/h", out _, out _));
            Assert.Equal("50 % – a_b", TikzImport.PlainText(@"50~\% -- a\_b", out _, out _).Replace(' ', ' '));
        }
    }
}
