using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using InventaSketch.Engine;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// Writes engine draw-plan units as native PowerPoint shapes (rectangles, ovals, lines with arrowheads, freeforms,
    /// text boxes) and groups them. All coordinates in the plan are slide points.
    /// </summary>
    internal sealed class ComRenderer
    {
        private readonly PowerPoint.Shapes _shapes;
        private int _counter;

        public ComRenderer(PowerPoint.Slide slide) => _shapes = slide.Shapes;

        /// <summary>Draws a unit and returns the resulting shape (a group if the unit has more than one shape).</summary>
        public PowerPoint.Shape? DrawUnit(PlanUnit unit)
        {
            var created = new List<PowerPoint.Shape>();
            foreach (var op in unit.Ops) Draw(op, created);
            if (created.Count == 0) return null;
            PowerPoint.Shape result;
            if (created.Count == 1) result = created[0];
            else
            {
                var names = created.Select(s => s.Name).ToArray();
                result = _shapes.Range(names).Group();
            }
            result.Name = unit.Name;
            result.Tags.Add("ISK_ID", unit.Id);
            result.Tags.Add("ISK_TYPE", unit.Type);
            result.Tags.Add("ISK_MODEL", unit.Payload);
            return result;
        }

        private void Draw(PlanOp op, List<PowerPoint.Shape> created)
        {
            switch (op.Op)
            {
                case "group":
                    {
                        var inner = new List<PowerPoint.Shape>();
                        foreach (var c in op.Children ?? new List<PlanOp>()) Draw(c, inner);
                        if (inner.Count == 1) created.Add(inner[0]);
                        else if (inner.Count > 1) created.Add(Named(_shapes.Range(inner.Select(s => s.Name).ToArray()).Group()));
                        break;
                    }
                case "rect":
                    {
                        var type = op.Radius is > 0 ? Office.MsoAutoShapeType.msoShapeRoundedRectangle : Office.MsoAutoShapeType.msoShapeRectangle;
                        var s = Named(_shapes.AddShape(type, F(op.X), F(op.Y), Math.Max(0.1f, F(op.W)), Math.Max(0.1f, F(op.H))));
                        Style(s, op, closed: true);
                        Rotate(s, op);
                        created.Add(s);
                        break;
                    }
                case "ellipse":
                    {
                        var s = Named(_shapes.AddShape(Office.MsoAutoShapeType.msoShapeOval, F(op.X), F(op.Y), Math.Max(0.1f, F(op.W)), Math.Max(0.1f, F(op.H))));
                        Style(s, op, closed: true);
                        Rotate(s, op);
                        created.Add(s);
                        break;
                    }
                case "shape":
                    {
                        var type = op.Shape == "diamond" ? Office.MsoAutoShapeType.msoShapeDiamond : Office.MsoAutoShapeType.msoShapeIsoscelesTriangle;
                        var s = Named(_shapes.AddShape(type, F(op.X), F(op.Y), Math.Max(0.1f, F(op.W)), Math.Max(0.1f, F(op.H))));
                        Style(s, op, closed: true);
                        Rotate(s, op);
                        created.Add(s);
                        break;
                    }
                case "line":
                    {
                        var s = Named(_shapes.AddLine(F(op.X1), F(op.Y1), F(op.X2), F(op.Y2)));
                        Style(s, op, closed: false);
                        created.Add(s);
                        break;
                    }
                case "polyline":
                    {
                        var pts = op.Points ?? new List<double[]>();
                        if (pts.Count < 2) break;
                        var b = _shapes.BuildFreeform(Office.MsoEditingType.msoEditingAuto, (float)pts[0][0], (float)pts[0][1]);
                        for (var i = 1; i < pts.Count; i++)
                            b.AddNodes(Office.MsoSegmentType.msoSegmentLine, Office.MsoEditingType.msoEditingAuto, (float)pts[i][0], (float)pts[i][1]);
                        if (op.Closed == true) b.AddNodes(Office.MsoSegmentType.msoSegmentLine, Office.MsoEditingType.msoEditingAuto, (float)pts[0][0], (float)pts[0][1]);
                        var s = Named(b.ConvertToShape());
                        Style(s, op, closed: op.Closed == true);
                        created.Add(s);
                        break;
                    }
                case "freeform":
                    foreach (var s in Freeforms(op))
                    {
                        Style(s.Shape, op, closed: s.Closed);
                        created.Add(s.Shape);
                    }
                    break;
                case "text":
                    {
                        var s = Named(_shapes.AddTextbox(Office.MsoTextOrientation.msoTextOrientationHorizontal, F(op.X), F(op.Y), Math.Max(1f, F(op.W)), Math.Max(1f, F(op.H))));
                        var tf = s.TextFrame;
                        // AutoSize first: a new text box fits itself to its (empty) text, so switching off word wrap
                        // while AutoSize is on shrinks it, and centred text would end up half a box too far left.
                        tf.AutoSize = PowerPoint.PpAutoSize.ppAutoSizeNone;
                        tf.MarginLeft = tf.MarginRight = tf.MarginTop = tf.MarginBottom = 0;
                        tf.WordWrap = Office.MsoTriState.msoFalse;
                        tf.VerticalAnchor = Office.MsoVerticalAnchor.msoAnchorMiddle;
                        s.Left = F(op.X); s.Top = F(op.Y); s.Width = Math.Max(1f, F(op.W)); s.Height = Math.Max(1f, F(op.H));
                        var range = tf.TextRange;
                        range.Text = op.Text ?? "";
                        range.Font.Size = (float)Math.Max(1, op.FontSize ?? 10);
                        if (!string.IsNullOrEmpty(op.Font)) range.Font.Name = op.Font!.Split(',')[0].Trim();
                        range.Font.Bold = op.Bold == true ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse;
                        range.Font.Italic = op.Italic == true ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse;
                        range.Font.Underline = op.Underline == true ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse;
                        range.Font.Color.RGB = Rgb(op.Color ?? "#000000");
                        range.ParagraphFormat.Alignment = op.Align switch
                        {
                            "left" => PowerPoint.PpParagraphAlignment.ppAlignLeft,
                            "right" => PowerPoint.PpParagraphAlignment.ppAlignRight,
                            _ => PowerPoint.PpParagraphAlignment.ppAlignCenter,
                        };
                        Rotate(s, op);
                        created.Add(s);
                        break;
                    }
                case "svg":
                    {
                        if (string.IsNullOrEmpty(op.Svg)) break;
                        var file = Path.Combine(Path.GetTempPath(), "isk-" + Guid.NewGuid().ToString("N") + ".svg");
                        File.WriteAllText(file, op.Svg);
                        try
                        {
                            var s = Named(_shapes.AddPicture(file, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, F(op.X), F(op.Y), F(op.W), F(op.H)));
                            created.Add(s);
                        }
                        finally
                        {
                            try { File.Delete(file); } catch { /* temp file */ }
                        }
                        break;
                    }
                default:
                    Log.Info("Unknown draw op ignored: " + op.Op);
                    break;
            }
        }

        /// <summary>Splits a segment list at every move-to and builds one freeform per sub-path.</summary>
        private IEnumerable<(PowerPoint.Shape Shape, bool Closed)> Freeforms(PlanOp op)
        {
            var segs = op.Segments ?? new List<PlanSeg>();
            var start = 0;
            while (start < segs.Count)
            {
                var end = start + 1;
                while (end < segs.Count && segs[end].Op != "M") end++;
                var part = segs.Skip(start).Take(end - start).ToList();
                start = end;
                if (part.Count < 2 || part[0].Op != "M") continue;
                var b = _shapes.BuildFreeform(Office.MsoEditingType.msoEditingCorner, (float)part[0].X, (float)part[0].Y);
                var closed = false;
                foreach (var s in part.Skip(1))
                {
                    switch (s.Op)
                    {
                        case "L":
                            b.AddNodes(Office.MsoSegmentType.msoSegmentLine, Office.MsoEditingType.msoEditingCorner, (float)s.X, (float)s.Y);
                            break;
                        case "C":
                            b.AddNodes(Office.MsoSegmentType.msoSegmentCurve, Office.MsoEditingType.msoEditingCorner,
                                (float)(s.X1 ?? s.X), (float)(s.Y1 ?? s.Y), (float)(s.X2 ?? s.X), (float)(s.Y2 ?? s.Y), (float)s.X, (float)s.Y);
                            break;
                        case "Z":
                            closed = true;
                            b.AddNodes(Office.MsoSegmentType.msoSegmentLine, Office.MsoEditingType.msoEditingCorner, (float)part[0].X, (float)part[0].Y);
                            break;
                    }
                }
                yield return (Named(b.ConvertToShape()), closed || op.Closed == true);
            }
        }

        private PowerPoint.Shape Named(PowerPoint.Shape s)
        {
            s.Name = "isk-part-" + (++_counter).ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            return s;
        }

        private static void Style(PowerPoint.Shape s, PlanOp op, bool closed)
        {
            var stroke = op.Stroke;
            if (stroke is null || stroke.Weight <= 0) s.Line.Visible = Office.MsoTriState.msoFalse;
            else
            {
                s.Line.Visible = Office.MsoTriState.msoTrue;
                s.Line.ForeColor.RGB = Rgb(stroke.Color);
                s.Line.Weight = (float)stroke.Weight;
                s.Line.DashStyle = stroke.Dash switch
                {
                    "dash" => Office.MsoLineDashStyle.msoLineDash,
                    "dashDot" => Office.MsoLineDashStyle.msoLineDashDot,
                    "dashDotDot" => Office.MsoLineDashStyle.msoLineDashDotDot,
                    "dot" => Office.MsoLineDashStyle.msoLineRoundDot,
                    "longDash" => Office.MsoLineDashStyle.msoLineLongDash,
                    _ => Office.MsoLineDashStyle.msoLineSolid,
                };
                if (!closed)
                {
                    if (stroke.ArrowStart is string a) { s.Line.BeginArrowheadStyle = Arrow(a); Size(s.Line, stroke.ArrowSize, begin: true); }
                    if (stroke.ArrowEnd is string e) { s.Line.EndArrowheadStyle = Arrow(e); Size(s.Line, stroke.ArrowSize, begin: false); }
                }
            }
            if (!closed) return;
            var fill = op.Fill;
            if (fill is null || fill.Kind != "solid" || fill.Color is null) s.Fill.Visible = Office.MsoTriState.msoFalse;
            else
            {
                s.Fill.Visible = Office.MsoTriState.msoTrue;
                s.Fill.Solid();
                s.Fill.ForeColor.RGB = Rgb(fill.Color);
                if (op.Opacity is double o && o < 1) s.Fill.Transparency = (float)(1 - o);
            }
        }

        private static void Size(PowerPoint.LineFormat line, string? size, bool begin)
        {
            // One step larger than the names suggest: PowerPoint's line ends are about 3× (medium) and 5× (long) the line
            // weight, the engine's heads about 3.5×, 5× and 7.5× (DrawPlanBuilder.NativeHeadLength).
            var len = size switch { "small" => Office.MsoArrowheadLength.msoArrowheadLengthMedium, _ => Office.MsoArrowheadLength.msoArrowheadLong };
            var wid = size switch { "small" => Office.MsoArrowheadWidth.msoArrowheadWidthMedium, "large" => Office.MsoArrowheadWidth.msoArrowheadWide, _ => Office.MsoArrowheadWidth.msoArrowheadWide };
            if (begin) { line.BeginArrowheadLength = len; line.BeginArrowheadWidth = wid; }
            else { line.EndArrowheadLength = len; line.EndArrowheadWidth = wid; }
        }

        /// <summary>
        /// Native line ends. Hollow triangle/diamond and crow's-foot ends never arrive here: PowerPoint has no such
        /// arrowheads, so the engine lowers them to explicit shapes (DrawPlanBuilder.HasNativeLineEnd).
        /// </summary>
        private static Office.MsoArrowheadStyle Arrow(string name) => name switch
        {
            "triangle" or "half" => Office.MsoArrowheadStyle.msoArrowheadTriangle,
            "open" or "tick" => Office.MsoArrowheadStyle.msoArrowheadOpen,
            "stealth" => Office.MsoArrowheadStyle.msoArrowheadStealth,
            "diamond" => Office.MsoArrowheadStyle.msoArrowheadDiamond,
            "oval" => Office.MsoArrowheadStyle.msoArrowheadOval,
            _ => Office.MsoArrowheadStyle.msoArrowheadNone,
        };

        private static void Rotate(PowerPoint.Shape s, PlanOp op)
        {
            if (op.Rotation is double r && Math.Abs(r) > 1e-6) s.Rotation = (float)r;
        }

        private static float F(double? v) => (float)(v ?? 0);

        /// <summary>#rrggbb to the BGR integer used by Office.</summary>
        public static int Rgb(string hex)
        {
            if (hex.Length < 7 || !int.TryParse(hex.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return 0;
            int r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255;
            return r | (g << 8) | (b << 16);
        }
    }
}
