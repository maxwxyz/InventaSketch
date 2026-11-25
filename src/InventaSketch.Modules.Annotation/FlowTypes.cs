using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Flowchart symbol shapes (conventions of ISO 5807; drawn independently).</summary>
    public enum FlowShape { Process, Decision, Terminator, Data, Document, Connector, End }

    /// <summary>
    /// Flowchart box with text and an optional step number ("S100") written outside the upper-right corner,
    /// the usual layout of patent method flowcharts (practice, see EP GL A-IX 8).
    /// </summary>
    public sealed class FlowBoxType : AnnBoxType
    {
        private readonly FlowShape _shape;

        /// <summary>Flowchart steps chain with an elbow connector (quick add, <see cref="Flow"/>).</summary>
        public override string? ChainConnector(SketchObject o) => "ann.arrow.elbow";

        public FlowBoxType(FlowShape shape)
            : base("ann.flow." + shape.ToString().ToLowerInvariant(), Names(shape), AnnCategories.Flowchart, Keys(shape))
        {
            _shape = shape;
        }

        private static I18nText Names(FlowShape s) => s switch
        {
            FlowShape.Decision => new I18nText("Decision", "Verzweigung"),
            FlowShape.Terminator => new I18nText("Start / end", "Start / Ende"),
            FlowShape.Data => new I18nText("Data (input/output)", "Daten (Ein-/Ausgabe)"),
            FlowShape.Document => new I18nText("Document", "Dokument"),
            FlowShape.Connector => new I18nText("Connector", "Verbindungsstelle"),
            FlowShape.End => new I18nText("End (double circle)", "Ende (Doppelkreis)"),
            _ => new I18nText("Process step", "Verarbeitungsschritt"),
        };

        private static string[] Keys(FlowShape s) => new[] { "flowchart", "flussdiagramm", "ablaufdiagramm", "method", "verfahren" }.Concat(s switch
        {
            FlowShape.Decision => new[] { "decision", "verzweigung", "diamond", "raute", "if" },
            FlowShape.Terminator => new[] { "start", "end", "terminator", "ende", "grenzstelle" },
            FlowShape.Data => new[] { "data", "daten", "input", "output", "parallelogram" },
            FlowShape.Document => new[] { "document", "dokument" },
            FlowShape.Connector => new[] { "connector", "verbindung", "circle", "kreis" },
            FlowShape.End => new[] { "end", "ende", "stop", "final", "double circle", "doppelkreis", "bullseye" },
            _ => new[] { "process", "step", "schritt", "box", "rechteck" },
        }).ToArray();

        public FlowShape Shape => _shape;

        public override Size2 DefaultSize => _shape switch
        {
            FlowShape.Decision => new Size2(50, 26),
            FlowShape.Terminator => new Size2(44, 14),
            FlowShape.Connector => new Size2(10, 10),
            FlowShape.End => new Size2(9, 9),
            _ => new Size2(50, 18),
        };

        public override bool HasText => _shape != FlowShape.End;
        public override bool PreferUniformScale => _shape is FlowShape.Connector or FlowShape.End;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("step", "Step number", "Schrittnummer", null),
            P.TextSize(_shape == FlowShape.Connector ? 4.5 : Ann.TextSize),
        };

        /// <summary>Outline of the symbol in local coordinates.</summary>
        public Primitive Outline(SketchObject o)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var s = Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") };
            switch (_shape)
            {
                case FlowShape.Decision:
                    return Prim.Polygon(new[] { new Vec2(0, -hh), new Vec2(hw, 0), new Vec2(0, hh), new Vec2(-hw, 0) }, s);
                case FlowShape.Terminator:
                    return Ann.RoundedRect(-hw, -hh, w, h, Math.Min(hh, hw), s);
                case FlowShape.Data:
                    {
                        var k = Math.Min(w * 0.15, h * 0.6);
                        return Prim.Polygon(new[] { new Vec2(-hw + k, -hh), new Vec2(hw, -hh), new Vec2(hw - k, hh), new Vec2(-hw, hh) }, s);
                    }
                case FlowShape.Document:
                    {
                        var wave = h * 0.12;
                        var by = hh - wave;
                        return new PathPrim(new[]
                        {
                            PathSeg.Move(new Vec2(-hw, -hh)), PathSeg.Line(new Vec2(hw, -hh)), PathSeg.Line(new Vec2(hw, by)),
                            PathSeg.Cubic(new Vec2(hw / 2, by - 2 * wave), new Vec2(-hw / 2, by + 2 * wave), new Vec2(-hw, by)),
                            PathSeg.Close,
                        }, s);
                    }
                case FlowShape.Connector:
                    return Prim.Ellipse(0, 0, hw, hh, s);
                case FlowShape.End:
                    // Outer and inner circle (end of the method), both outlined.
                    return new GroupPrim(new Primitive[] { Prim.Ellipse(0, 0, hw, hh, s), Prim.Ellipse(0, 0, hw * 0.62, hh * 0.62, s with { Fill = null }) });
                default:
                    return Prim.Rect(-hw, -hh, w, h, s);
            }
        }

        /// <summary>Width and height available for the text inside the symbol.</summary>
        public (double W, double H) TextArea(SketchObject o)
        {
            double w = o.Size.W, h = o.Size.H;
            return _shape switch
            {
                FlowShape.Decision => (w * 0.58, h * 0.56),
                FlowShape.Terminator => (Math.Max(1, w - h * 0.7), h * 0.8),
                FlowShape.Data => (w * 0.72, h * 0.8),
                FlowShape.Connector => (w * 0.74, h * 0.74),
                FlowShape.Document => (w * 0.9, h * 0.7),
                _ => (w * 0.92, h * 0.84),
            };
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var list = new List<Primitive> { Outline(o) with { Tag = AnnTags.Main } };
            var text = o.Text ?? "";
            var size = TextSizeOf(o, _shape == FlowShape.Connector ? 4.5 : Ann.TextSize);
            var cy = _shape == FlowShape.Document ? -o.Size.H * 0.06 : 0;
            if (!string.IsNullOrEmpty(text) && _shape != FlowShape.End)
            {
                // Wrapped and reduced to fit the inside of the symbol (diamond and circle: the inscribed area).
                var (w, h) = TextArea(o);
                var (fitted, fs) = Ann.Fit(text, size, w, h);
                list.Add(Ann.Text(new Vec2(0, cy), fitted, fs));
            }
            var step = o.Str("step", "");
            if (!string.IsNullOrEmpty(step))
                list.Add(Ann.Text(new Vec2(o.Size.W / 2 + 1.5, -o.Size.H / 2 - size * 0.45), step, size, AnnTags.Numeral, TextAnchor.Start));
            return list;
        }
    }

    /// <summary>Stand-alone step number label ("S100") for patent flowcharts.</summary>
    public sealed class FlowStepType : AnnBoxType
    {
        public FlowStepType() : base("ann.flow.step", new I18nText("Step number", "Schrittnummer"), AnnCategories.Flowchart, "step", "schritt", "s100", "number", "nummer", "flowchart") { }

        public override Size2 DefaultSize => new(14, 7);
        public override bool HasText => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Text("step", "Step number", "Schrittnummer", "S100"), P.TextSize(Ann.TextSize) };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Ann.Text(Vec2.Zero, o.Text ?? o.Str("step", "S100"), TextSizeOf(o), AnnTags.Numeral) };
    }
}
