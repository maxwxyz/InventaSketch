using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;
using K = InventaSketch.Core.VectorImport.ShapeKind;

namespace InventaSketch.Core.Tests
{
    /// <summary>Vector import (SVG, ASCII DXF) into neutral shapes in mm, y down (<see cref="VectorImport"/>).</summary>
    public class VectorImportTests
    {
        private static void Near(double expected, double actual, double tol = 1e-3) => Assert.InRange(actual, expected - tol, expected + tol);

        [Fact]
        public void SvgUnitsViewBoxAndTransformsGiveMillimetres()
        {
            // 100 x 50 mm sheet, viewBox in tenths of a millimetre; a group moved by 100 user units (10 mm).
            const string svg = @"<?xml version='1.0'?>
<svg xmlns='http://www.w3.org/2000/svg' width='100mm' height='50mm' viewBox='0 0 1000 500'>
  <defs><rect id='hidden' x='0' y='0' width='5' height='5'/></defs>
  <line x1='0' y1='0' x2='200' y2='0'/>
  <g transform='translate(100,100)'>
    <rect x='0' y='0' width='300' height='100' rx='20'/>
    <circle cx='500' cy='100' r='50'/>
  </g>
  <polygon points='0,400 100,400 50,300'/>
  <text x='10' y='480' font-size='35'>Fig. 1</text>
</svg>";
            var d = VectorImport.Parse(svg)!;
            Assert.True(d.RealScale);
            Assert.Equal(new[] { K.Line, K.Rect, K.Ellipse, K.Polyline, K.Text }, d.Shapes.Select(s => s.Kind));
            var line = d.Shapes[0];
            Near(20, line.Points[1].X);
            var rect = d.Shapes[1].Box;
            Near(10, rect.X); Near(10, rect.Y); Near(30, rect.W); Near(10, rect.H);
            Near(2, d.Shapes[1].Radius);
            var circle = d.Shapes[2].Box;
            Near(55, circle.X); Near(15, circle.Y); Near(10, circle.W);
            Assert.True(d.Shapes[3].Closed);
            var text = d.Shapes[4];
            Assert.Equal("Fig. 1", text.Text);
            Near(3.5, text.Height);
            Near(48, text.Points[0].Y);
        }

        [Fact]
        public void SvgPathsFlattenCurvesAndArcs()
        {
            // A half circle of radius 10 from (0,0) to (20,0) through (10,10) (sweep 0 = counter-clockwise in y-down).
            var parts = VectorImport.PathPoints("M0,0 A10,10 0 0 0 20,0 L20,-5 Z m5,5 h10 v10");
            Assert.Equal(2, parts.Count);
            var (arc, closed) = parts[0];
            Assert.True(closed);
            Assert.True(arc.Count > 10);
            Assert.Contains(arc, p => Math.Abs(p.X - 10) < 0.2 && Math.Abs(p.Y - 10) < 0.2);
            Assert.All(arc.Take(arc.Count - 1), p => Assert.InRange((p - new Vec2(10, 0)).Length, 9.99, 10.01));
            // The relative move after Z starts from the sub-path start; h/v are relative.
            var (open, closed2) = parts[1];
            Assert.False(closed2);
            Assert.Equal(new[] { new Vec2(5, 5), new Vec2(15, 5), new Vec2(15, 15) }, open);
            // A cubic curve ends exactly on its end point.
            var cubic = VectorImport.PathPoints("M0 0 C 0 10 10 10 10 0")[0].Points;
            Near(10, cubic[cubic.Count - 1].X); Near(0, cubic[cubic.Count - 1].Y);
        }

        [Fact]
        public void RotatedSvgBoxesBecomePolylines()
        {
            var d = VectorImport.Parse("<svg xmlns='http://www.w3.org/2000/svg'><rect width='10' height='10' transform='rotate(45)'/></svg>")!;
            Assert.False(d.RealScale);
            var s = Assert.Single(d.Shapes);
            Assert.Equal(K.Polyline, s.Kind);
            Assert.True(s.Closed);
            Assert.Equal(4, s.Points.Count);
        }

        private static string Dxf(params string[] entities)
            => string.Join("\n", new[] { "0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", "5", "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES" }
                .Concat(entities.SelectMany(e => e.Split('|'))).Concat(new[] { "0", "ENDSEC", "0", "EOF" }));

        [Fact]
        public void DxfEntitiesInCentimetresWithYUp()
        {
            var dxf = Dxf(
                "0|LINE|8|0|10|0|20|0|11|3|21|4",
                "0|CIRCLE|10|5|20|5|40|1",
                "0|ARC|10|0|20|0|40|2|50|0|51|90",
                "0|LWPOLYLINE|90|2|70|0|10|0|20|0|42|1|10|2|20|0",
                "0|TEXT|10|1|20|2|40|0.5|1|Hello",
                "0|MTEXT|10|0|20|10|40|1|1|{\\fArial;A}\\PB");
            var d = VectorImport.Parse(dxf)!;
            Assert.True(d.RealScale);
            Assert.Equal(new[] { K.Line, K.Ellipse, K.Polyline, K.Polyline, K.Text, K.Text }, d.Shapes.Select(s => s.Kind));
            // Centimetres → mm, y flipped.
            Near(30, d.Shapes[0].Points[1].X); Near(-40, d.Shapes[0].Points[1].Y);
            var circle = d.Shapes[1].Box;
            Near(40, circle.X); Near(-60, circle.Y); Near(20, circle.W);
            // Quarter arc from 0° to 90° (counter-clockwise, y up): from (20,0) to (0,-20) on screen.
            var arc = d.Shapes[2].Points;
            Near(20, arc[0].X); Near(0, arc[0].Y);
            Near(0, arc[arc.Count - 1].X); Near(-20, arc[arc.Count - 1].Y);
            // Bulge 1 = half circle over the chord (0,0)-(2,0) cm, counter-clockwise: through (1,-1) cm, i.e. y = +10 mm on screen.
            var bulge = d.Shapes[3].Points;
            Assert.Contains(bulge, p => Math.Abs(p.X - 10) < 0.3 && Math.Abs(p.Y - 10) < 0.3);
            Assert.Equal("Hello", d.Shapes[4].Text);
            Near(5, d.Shapes[4].Height);
            Assert.Equal("A B", d.Shapes[5].Text);
        }

        [Fact]
        public void NeitherSvgNorDxfIsRejected()
        {
            Assert.Null(VectorImport.Parse("a;b\n1;2"));
            Assert.Null(VectorImport.Parse("<gpx></gpx>"));
        }
    }
}
