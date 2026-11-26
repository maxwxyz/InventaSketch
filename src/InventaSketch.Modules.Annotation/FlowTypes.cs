using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Flowchart symbol shapes (conventions of ISO 5807; drawn independently). Members are persisted through the type ids
    /// (<c>ann.flow.&lt;shape&gt;</c>); new members are appended at the end.
    /// </summary>
    public enum FlowShape
    {
        Process, Decision, Terminator, Data, Document, Connector, End,
        /// <summary>Predefined process (sub-process): rectangle with two inner vertical lines.</summary>
        Predefined,
        /// <summary>Off-page connector: pentagon pointing down with a letter.</summary>
        OffPage,
        /// <summary>Loop limit: rectangle with chamfered top corners (begin) or bottom corners (end, parameter <c>edge</c>).</summary>
        Loop,
        /// <summary>Preparation: hexagon.</summary>
        Preparation,
        /// <summary>Manual operation: trapezoid, wide side up.</summary>
        Manual,
        /// <summary>Stored data / database: upright cylinder.</summary>
        Database,
        /// <summary>Delay: "D" shape.</summary>
        Delay,
    }

    /// <summary>
    /// Flowchart box with text and an optional step number ("S100") written outside the upper-right corner,
    /// the usual layout of method flowcharts in applications (practice, see EP GL A-IX 8). An optional step (parameter
    /// <c>optional</c>) is drawn with a dashed outline.
    /// </summary>
    public sealed class FlowBoxType : AnnBoxType
    {
        private readonly FlowShape _shape;

        /// <summary>Flowchart steps chain with an elbow connector (quick add, <see cref="Flow"/>).</summary>
        public override string? ChainConnector(SketchObject o) => "ann.arrow.elbow";

        /// <summary>
        /// Exits of a decision: the first connector added with "+" is labelled "yes", the second "no" (written in the language
        /// the drawing is rendered in), or "1", "2", "3" … (<c>exitStyle = numbers</c>), or the labels of the parameter
        /// <c>exitLabels</c> ("&lt;|=|&gt;").
        /// </summary>
        public override IReadOnlyDictionary<string, ParamValue>? ChainConnectorParams(SketchObject o, int index)
        {
            if (_shape != FlowShape.Decision) return null;
            var label = ExitLabel(o, index);
            return label.Length > 0 ? new Dictionary<string, ParamValue> { ["exit"] = label } : null;
        }

        /// <summary>Exit labels of a decision in quick-add order ("yes", "no" unless <c>exitLabels</c> gives others).</summary>
        public static IReadOnlyList<string> ExitLabels(SketchObject o)
        {
            var raw = o.Str("exitLabels", "");
            if (string.IsNullOrWhiteSpace(raw)) return new[] { "yes", "no" };
            return raw.Split(new[] { '|', ';', '\n' }, StringSplitOptions.None).Select(s => s.Trim()).ToList();
        }

        /// <summary>
        /// The label of the <paramref name="index"/>-th exit of a decision (0 = first): the <c>exitLabels</c> entry if given, else
        /// "1", "2", "3" … with <c>exitStyle = numbers</c>, else "yes"/"no" for the first two exits; "" if there is none.
        /// </summary>
        public static string ExitLabel(SketchObject o, int index)
        {
            if (index < 0) return "";
            if (string.IsNullOrWhiteSpace(o.Str("exitLabels", "")) && o.Str(FlowNumbering.ExitStyleParam, "yesno") == "numbers")
                return (index + 1).ToString(CultureInfo.InvariantCulture);
            var labels = ExitLabels(o);
            return index < labels.Count ? labels[index] : "";
        }

        /// <summary>The flowchart shape of a type id ("ann.flow.process" → process); null for other types.</summary>
        public static FlowShape? ShapeOf(string typeId)
            => typeId.StartsWith("ann.flow.", StringComparison.Ordinal) && ShapesById.TryGetValue(typeId, out var s) ? s : null;

        private static readonly Dictionary<string, FlowShape> ShapesById =
            Enum.GetValues(typeof(FlowShape)).Cast<FlowShape>().ToDictionary(s => "ann.flow." + s.ToString().ToLowerInvariant(), s => s, StringComparer.Ordinal);

        /// <summary>A flow box rereads the flow when step numbering or automatic exit labels are on (see <see cref="FlowNumbering"/>).</summary>
        public override bool DependsOnNeighbours => true;

        /// <summary>Only while step numbering or automatic exit labels are on somewhere in the document.</summary>
        public override bool RendersFromNeighbours(SketchObject o, SketchDocument doc) => FlowNumbering.IsActive(doc);

        /// <summary>The other numbered steps of its flow and, for a decision, its exit connectors (their labels follow it).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => FlowNumbering.DependentsOfBox(o, ctx);

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
            FlowShape.Predefined => new I18nText("Predefined process (sub-process)", "Vordefinierter Prozess (Unterprogramm)"),
            FlowShape.OffPage => new I18nText("Off-page connector", "Seitenverbinder"),
            FlowShape.Loop => new I18nText("Loop begin (loop limit)", "Schleifenanfang (Schleifenbegrenzung)"),
            FlowShape.Preparation => new I18nText("Preparation (hexagon)", "Vorbereitung (Sechseck)"),
            FlowShape.Manual => new I18nText("Manual operation", "Manuelle Verarbeitung"),
            FlowShape.Database => new I18nText("Stored data (cylinder)", "Datenspeicher (Zylinder)"),
            FlowShape.Delay => new I18nText("Delay", "Verzögerung (Wartezeit)"),
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
            FlowShape.Predefined => new[] { "predefined", "sub-process", "subprocess", "subroutine", "unterprogramm", "teilprozess", "vordefiniert" },
            FlowShape.OffPage => new[] { "off-page", "offpage", "page connector", "seitenverbinder", "übergang", "fortsetzung", "pentagon" },
            FlowShape.Loop => new[] { "loop", "schleife", "loop limit", "schleifenbegrenzung", "repeat", "wiederholung", "for", "while" },
            FlowShape.Preparation => new[] { "preparation", "vorbereitung", "hexagon", "sechseck", "initialisation", "initialisierung" },
            FlowShape.Manual => new[] { "manual", "manuell", "hand", "trapezoid", "trapez", "operator" },
            FlowShape.Database => new[] { "database", "datenbank", "storage", "speicher", "cylinder", "zylinder", "stored data" },
            FlowShape.Delay => new[] { "delay", "verzögerung", "wait", "warten", "wartezeit", "timer" },
            _ => new[] { "process", "step", "schritt", "box", "rechteck" },
        }).ToArray();

        public FlowShape Shape => _shape;

        public override Size2 DefaultSize => _shape switch
        {
            FlowShape.Decision => new Size2(50, 26),
            FlowShape.Terminator => new Size2(44, 14),
            FlowShape.Connector => new Size2(10, 10),
            FlowShape.End => new Size2(9, 9),
            FlowShape.OffPage => new Size2(12, 12),
            FlowShape.Database => new Size2(36, 24),
            FlowShape.Delay => new Size2(40, 18),
            _ => new Size2(50, 18),
        };

        public override bool HasText => _shape != FlowShape.End;
        public override bool PreferUniformScale => _shape is FlowShape.Connector or FlowShape.End or FlowShape.OffPage;

        private bool SmallText => _shape is FlowShape.Connector or FlowShape.OffPage;

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>
                {
                    P.Text("step", "Step number", "Schrittnummer", null),
                    P.TextSize(SmallText ? 4.5 : Ann.TextSize),
                    P.Bool("optional", "Optional step (dashed)", "Optionaler Schritt (gestrichelt)", false),
                };
                if (_shape == FlowShape.Loop)
                    list.Add(P.Choice("edge", "Loop limit", "Schleifenbegrenzung", "begin", ("begin", "Begin (top corners cut)", "Anfang (oben abgeschrägt)"), ("end", "End (bottom corners cut)", "Ende (unten abgeschrägt)")));
                if (_shape == FlowShape.Decision)
                {
                    list.Add(P.Text("exitLabels", "Exit labels (a|b|c)", "Ausgangsbeschriftungen (a|b|c)", null));
                    list.Add(P.Choice(FlowNumbering.ExitStyleParam, "Exit labels", "Ausgangsbeschriftung", "yesno",
                        ("yesno", "yes / no", "ja / nein"), ("numbers", "1 / 2 / 3", "1 / 2 / 3")));
                    list.Add(P.Bool(FlowNumbering.AutoExitsParam, "Label drawn exits automatically", "Gezeichnete Ausgänge automatisch beschriften", false));
                }
                // Step numbering along the flow, set on the first element of the flow (off by default).
                list.Add(P.Bool(FlowNumbering.NumberParam, "Number the steps along the flow", "Schritte entlang des Ablaufs nummerieren", false));
                list.Add(P.Text(FlowNumbering.PrefixParam, "Step number prefix", "Präfix der Schrittnummer", "S") with { Advanced = true });
                list.Add(P.Int(FlowNumbering.FirstParam, "First step number", "Erste Schrittnummer", 100, 0, 1000000) with { Advanced = true });
                list.Add(P.Int(FlowNumbering.IncrementParam, "Step number increment", "Schrittweite der Nummern", 10, 1, 100000) with { Advanced = true });
                return list;
            }
        }

        /// <summary>
        /// Document-aware actions: "Number steps along the flow" on the first element of a flow (no connector leads into it,
        /// at least one leads out) or wherever it is already on.
        /// </summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            foreach (var a in Actions(o, ctx.Lang)) yield return a;
            if (o.Flag(FlowNumbering.NumberParam, false) || FlowNumbering.IsFlowStart(o, ctx))
                yield return Ann.Toggle(o, "ann.flow.number", new I18nText("Number steps along the flow", "Schritte automatisch nummerieren"),
                    IconIds.Renumber, FlowNumbering.NumberParam, false, "flow");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.flow.optional", new I18nText("Optional step (dashed)", "Optionaler Schritt (gestrichelt)"), IconIds.LineDash, "optional", false, "flow");
            if (_shape == FlowShape.Decision)
            {
                yield return Ann.Toggle(o, "ann.flow.exits.auto", new I18nText("Label exits automatically", "Ausgänge automatisch beschriften"),
                    IconIds.LabelShow, FlowNumbering.AutoExitsParam, false, "exits");
                foreach (var a in Ann.Choice(o, "ann.flow.exits.style", new I18nText("Exit labels", "Ausgangsbeschriftung"), FlowNumbering.ExitStyleParam, "yesno", "exits",
                    new (string, I18nText, string?)[]
                    {
                        ("yesno", new I18nText("yes / no", "ja / nein"), null),
                        ("numbers", new I18nText("1 / 2 / 3", "1 / 2 / 3"), null),
                    })) yield return a;
            }
            if (_shape == FlowShape.Loop)
            {
                // Option pictures: the loop limit with its top (begin) or bottom (end) corners cut.
                foreach (var a in Ann.Choice(o, "ann.flow.loop.edge", new I18nText("Loop limit", "Schleifengrenze"), "edge", "begin", "edge", new (string, I18nText, string?)[]
                {
                    ("begin", new I18nText("Begin", "Anfang"), null),
                    ("end", new I18nText("End", "Ende"), null),
                }, v => new Primitive[]
                {
                    Prim.Polygon(v == "end"
                        ? new[] { new Vec2(-6, -3), new Vec2(6, -3), new Vec2(6, 1.5), new Vec2(4.5, 3), new Vec2(-4.5, 3), new Vec2(-6, 1.5) }
                        : new[] { new Vec2(-4.5, -3), new Vec2(4.5, -3), new Vec2(6, -1.5), new Vec2(6, 3), new Vec2(-6, 3), new Vec2(-6, -1.5) }, Style.Visible with { StrokeWidth = 0.4 }),
                })) yield return a;
            }
        }

        private static double PredefinedInset(double w, double h) => Math.Max(1, Math.Min(Math.Min(w * 0.08, 5), h * 0.4));
        private static double OffPageTip(double h) => h * 0.35;
        private static double LoopChamfer(double w, double h) => Math.Min(h * 0.35, w * 0.2);
        private static double PreparationInset(double w, double h) => Math.Min(w * 0.15, h * 0.5);
        private static double ManualInset(double w, double h) => Math.Min(w * 0.15, h * 0.6);
        private static double CylinderCap(double w, double h) => Math.Min(h * 0.13, w * 0.25);
        private static double DelayRadius(double w, double h) => Math.Min(h / 2, w / 2);

        /// <summary>Outline of the symbol in local coordinates.</summary>
        public Primitive Outline(SketchObject o)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var s = Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") };
            if (o.Flag("optional", false)) s = s with { Dash = DashStyle.Dash };
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
                case FlowShape.Predefined:
                    {
                        var k = PredefinedInset(w, h);
                        var inner = s with { Fill = null };
                        return new GroupPrim(new Primitive[]
                        {
                            Prim.Polygon(new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw, hh), new Vec2(-hw, hh) }, s),
                            Prim.Line(-hw + k, -hh, -hw + k, hh, inner), Prim.Line(hw - k, -hh, hw - k, hh, inner),
                        });
                    }
                case FlowShape.OffPage:
                    {
                        var t = OffPageTip(h);
                        return Prim.Polygon(new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw, hh - t), new Vec2(0, hh), new Vec2(-hw, hh - t) }, s);
                    }
                case FlowShape.Loop:
                    {
                        var c = LoopChamfer(w, h);
                        var end = o.Str("edge", "begin") == "end";
                        var pts = end
                            ? new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw, hh - c), new Vec2(hw - c, hh), new Vec2(-hw + c, hh), new Vec2(-hw, hh - c) }
                            : new[] { new Vec2(-hw + c, -hh), new Vec2(hw - c, -hh), new Vec2(hw, -hh + c), new Vec2(hw, hh), new Vec2(-hw, hh), new Vec2(-hw, -hh + c) };
                        return Prim.Polygon(pts, s);
                    }
                case FlowShape.Preparation:
                    {
                        var k = PreparationInset(w, h);
                        return Prim.Polygon(new[] { new Vec2(-hw, 0), new Vec2(-hw + k, -hh), new Vec2(hw - k, -hh), new Vec2(hw, 0), new Vec2(hw - k, hh), new Vec2(-hw + k, hh) }, s);
                    }
                case FlowShape.Manual:
                    {
                        var k = ManualInset(w, h);
                        return Prim.Polygon(new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw - k, hh), new Vec2(-hw + k, hh) }, s);
                    }
                case FlowShape.Database:
                    {
                        // Body (top half of the upper ellipse, sides, lower half of the bottom ellipse) as one closed path, plus
                        // the front half of the upper ellipse as an open line.
                        var ry = CylinderCap(w, h);
                        const double k = 0.5522847498307936;
                        double top = -hh + ry, bottom = hh - ry;
                        var body = new PathPrim(new[]
                        {
                            PathSeg.Move(new Vec2(-hw, top)),
                            PathSeg.Cubic(new Vec2(-hw, top - ry * k), new Vec2(-hw * k, -hh), new Vec2(0, -hh)),
                            PathSeg.Cubic(new Vec2(hw * k, -hh), new Vec2(hw, top - ry * k), new Vec2(hw, top)),
                            PathSeg.Line(new Vec2(hw, bottom)),
                            PathSeg.Cubic(new Vec2(hw, bottom + ry * k), new Vec2(hw * k, hh), new Vec2(0, hh)),
                            PathSeg.Cubic(new Vec2(-hw * k, hh), new Vec2(-hw, bottom + ry * k), new Vec2(-hw, bottom)),
                            PathSeg.Close,
                        }, s);
                        var front = new PathPrim(new[]
                        {
                            PathSeg.Move(new Vec2(-hw, top)),
                            PathSeg.Cubic(new Vec2(-hw, top + ry * k), new Vec2(-hw * k, top + ry), new Vec2(0, top + ry)),
                            PathSeg.Cubic(new Vec2(hw * k, top + ry), new Vec2(hw, top + ry * k), new Vec2(hw, top)),
                        }, s with { Fill = null });
                        return new GroupPrim(new Primitive[] { body, front });
                    }
                case FlowShape.Delay:
                    {
                        var r = DelayRadius(w, h);
                        const double k = 0.5522847498307936;
                        var cx = hw - r;
                        return new PathPrim(new[]
                        {
                            PathSeg.Move(new Vec2(-hw, -hh)), PathSeg.Line(new Vec2(cx, -hh)),
                            PathSeg.Cubic(new Vec2(cx + r * k, -hh), new Vec2(hw, -hh * k), new Vec2(hw, 0)),
                            PathSeg.Cubic(new Vec2(hw, hh * k), new Vec2(cx + r * k, hh), new Vec2(cx, hh)),
                            PathSeg.Line(new Vec2(-hw, hh)), PathSeg.Close,
                        }, s);
                    }
                default:
                    return Prim.Rect(-hw, -hh, w, h, s);
            }
        }

        /// <summary>Width and height available for the text inside the symbol (a rectangle centred at <see cref="TextCenter"/>).</summary>
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
                FlowShape.Predefined => (Math.Max(1, (w - 2 * PredefinedInset(w, h)) * 0.9), h * 0.84),
                FlowShape.OffPage => (w * 0.8, (h - OffPageTip(h)) * 0.9),
                FlowShape.Loop => (Math.Max(1, (w - 2 * LoopChamfer(w, h) * 0.6) * 0.92), (h - LoopChamfer(w, h) * 0.5) * 0.84),
                FlowShape.Preparation => (Math.Max(1, (w - 1.8 * PreparationInset(w, h)) * 0.94), h * 0.8),
                FlowShape.Manual => (Math.Max(1, (w - 2 * ManualInset(w, h)) * 0.94), h * 0.8),
                FlowShape.Database => (w * 0.9, Math.Max(1, (h - 3 * CylinderCap(w, h)) * 0.9)),
                FlowShape.Delay => (Math.Max(1, (w - 0.6 * DelayRadius(w, h)) * 0.88), h * 0.7),
                _ => (w * 0.92, h * 0.84),
            };
        }

        /// <summary>Centre of the text area in local coordinates.</summary>
        public Vec2 TextCenter(SketchObject o)
        {
            double w = o.Size.W, h = o.Size.H;
            return _shape switch
            {
                FlowShape.Document => new Vec2(0, -h * 0.06),
                FlowShape.OffPage => new Vec2(0, -OffPageTip(h) / 2),
                FlowShape.Loop => new Vec2(0, (o.Str("edge", "begin") == "end" ? -1 : 1) * LoopChamfer(w, h) * 0.25),
                FlowShape.Database => new Vec2(0, CylinderCap(w, h) * 0.5),
                FlowShape.Delay => new Vec2(-DelayRadius(w, h) * 0.3, 0),
                _ => Vec2.Zero,
            };
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var list = new List<Primitive> { Outline(o) with { Tag = AnnTags.Main } };
            var text = o.Text ?? "";
            var size = TextSizeOf(o, SmallText ? 4.5 : Ann.TextSize);
            if (!string.IsNullOrEmpty(text) && _shape != FlowShape.End)
            {
                // Wrapped and reduced to fit the inside of the symbol (diamond and circle: the inscribed area).
                var (w, h) = TextArea(o);
                var (fitted, fs) = Ann.Fit(text, size, w, h);
                list.Add(Ann.Text(TextCenter(o), fitted, fs));
            }
            // An entered step number wins; else the number along the flow when numbering is on.
            var step = o.Str("step", "");
            if (string.IsNullOrEmpty(step)) step = FlowNumbering.StepOf(o, ctx) ?? "";
            if (!string.IsNullOrEmpty(step))
                list.Add(Ann.Text(new Vec2(o.Size.W / 2 + 1.5, -o.Size.H / 2 - size * 0.45), step, size, AnnTags.Numeral, TextAnchor.Start));
            return list;
        }
    }

    /// <summary>
    /// Parallel mode (fork / join): a filled bar or the two parallel lines of ISO 5807 across several flow lines. Its long
    /// sides carry 2–6 evenly spaced ports each; wider than high it lies horizontally (ports on top and bottom), else it
    /// stands (ports left and right). Chains with the elbow connector.
    /// </summary>
    public sealed class FlowParallelType : AnnBoxType
    {
        public FlowParallelType() : base("ann.flow.parallel", new I18nText("Parallel mode (fork / join)", "Parallelbetrieb (Aufteilung / Zusammenführung)"), AnnCategories.Flowchart,
            "parallel", "fork", "join", "synchronisation", "synchronization", "parallelbetrieb", "gabelung", "zusammenführung", "aufteilung", "flowchart", "flussdiagramm", "ablaufdiagramm")
        { }

        public override Size2 DefaultSize => new(60, 3);
        protected override bool EdgePorts => false;
        public override string? ChainConnector(SketchObject o) => "ann.arrow.elbow";

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("style", "Style", "Darstellung", "bar", ("bar", "Filled bar", "Gefüllter Balken"), ("doubleline", "Two parallel lines (ISO 5807)", "Zwei parallele Linien (ISO 5807)")),
            P.Int("ports", "Ports per side", "Anschlüsse je Seite", 3, 2, 6),
        };

        public static bool Horizontal(SketchObject o) => o.Size.W >= o.Size.H;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            if (o.Str("style", "bar") == "doubleline")
            {
                var s = Style.Visible with { StrokeWidth = 0.35 };
                return Horizontal(o)
                    ? new Primitive[] { Prim.Line(-hw, -hh, hw, -hh, s) with { Tag = AnnTags.Main }, Prim.Line(-hw, hh, hw, hh, s) with { Tag = AnnTags.Main } }
                    : new Primitive[] { Prim.Line(-hw, -hh, -hw, hh, s) with { Tag = AnnTags.Main }, Prim.Line(hw, -hh, hw, hh, s) with { Tag = AnnTags.Main } };
            }
            return new Primitive[] { Prim.Rect(-hw, -hh, w, h, Style.Visible with { StrokeWidth = 0.25, Fill = Fill.Solid("#000000") }) with { Tag = AnnTags.Main } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var n = Math.Max(2, Math.Min(6, o.Int("ports", 3)));
            var horizontal = Horizontal(o);
            for (var i = 0; i < n; i++)
            {
                var f = (i + 0.5) / n - 0.5;
                var k = i.ToString(CultureInfo.InvariantCulture);
                if (horizontal)
                {
                    yield return Anchor.Point(AnchorRole.Port, new Vec2(f * 2 * hw, -hh), "top" + k);
                    yield return Anchor.Point(AnchorRole.Port, new Vec2(f * 2 * hw, hh), "bottom" + k);
                }
                else
                {
                    yield return Anchor.Point(AnchorRole.Port, new Vec2(-hw, f * 2 * hh), "left" + k);
                    yield return Anchor.Point(AnchorRole.Port, new Vec2(hw, f * 2 * hh), "right" + k);
                }
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.parallel.style", new I18nText("Bar", "Balken"), "style", "bar", "style", new (string, I18nText, string?)[]
            {
                ("bar", new I18nText("Filled bar", "Gefüllter Balken"), null),
                ("doubleline", new I18nText("Two parallel lines", "Zwei parallele Linien"), null),
            }, v => v == "doubleline"
                ? new Primitive[] { new LinePrim(new Vec2(-6, -0.8), new Vec2(6, -0.8), Style.Visible with { StrokeWidth = 0.4 }), new LinePrim(new Vec2(-6, 0.8), new Vec2(6, 0.8), Style.Visible with { StrokeWidth = 0.4 }) }
                : new Primitive[] { new RectPrim(-6, -0.8, 12, 1.6, Style.Visible with { StrokeWidth = 0.3, Fill = Fill.Solid("#000000") }) })) yield return a;
            yield return new ContextAction("ann.parallel.more", new I18nText("Add a port per side", "Anschluss je Seite dazu"), IconIds.PortAdd,
                x => x.WithParam("ports", Math.Min(6, x.Int("ports", 3) + 1))) { Group = "ports", IsEnabled = o.Int("ports", 3) < 6 };
            yield return new ContextAction("ann.parallel.fewer", new I18nText("Remove a port per side", "Anschluss je Seite weg"), IconIds.PortRemove,
                x => x.WithParam("ports", Math.Max(2, x.Int("ports", 3) - 1))) { Group = "ports", IsEnabled = o.Int("ports", 3) > 2 };
        }
    }

    /// <summary>
    /// Flowchart numbering read from the drawn flow (round 7, 2026-11-26). The flow is the directed graph of the flowchart
    /// symbols (<c>ann.flow.*</c>) joined by connectors whose first and last points are docked on them (from the first to the
    /// last). Both features are off by default, so existing drawings stay as they are:
    /// <list type="bullet">
    /// <item><b>Step numbers</b> (<c>numberSteps</c> on the first element of a flow, usually the start): the steps reachable
    /// from it are numbered <c>stepPrefix</c> + <c>stepFirst</c>, + <c>stepIncrement</c> … ("S100", "S110" …) in flow order:
    /// depth first along the exits in drawing order, loop-backs ignored, and a step where branches meet only after all of
    /// them (topological order). Process, decision, data, document, predefined process, manual operation, preparation,
    /// delay and loop limit are numbered; start/end, connectors, off-page connectors, stored data and the parallel bar are
    /// passed through. An entered <c>step</c> wins over the automatic number.</item>
    /// <item><b>Exit labels</b> (<c>autoExits</c> on a decision): every elbow connector leaving the decision without its own
    /// text or <c>exit</c> shows the label of its position among the decision's exits in drawing order ("yes", "no", or
    /// "1", "2", "3" … with <c>exitStyle = numbers</c>, or the <c>exitLabels</c>).</item>
    /// </list>
    /// The graph is built once per document (cached weakly).
    /// </summary>
    public static class FlowNumbering
    {
        public const string NumberParam = "numberSteps", PrefixParam = "stepPrefix", FirstParam = "stepFirst", IncrementParam = "stepIncrement";
        public const string AutoExitsParam = "autoExits", ExitStyleParam = "exitStyle";

        private sealed class FlowGraph
        {
            /// <summary>Flow nodes (flowchart symbols and the parallel bar) → shape (null: the parallel bar).</summary>
            public readonly Dictionary<string, FlowShape?> Nodes = new(StringComparer.Ordinal);
            /// <summary>Outgoing (connector id, target) per node, in drawing order.</summary>
            public readonly Dictionary<string, List<(string Edge, string To)>> Out = new(StringComparer.Ordinal);
            public readonly HashSet<string> HasIncoming = new(StringComparer.Ordinal);
            /// <summary>Connector id → (source node, index among the source's exits).</summary>
            public readonly Dictionary<string, (string From, int Index)> Exits = new(StringComparer.Ordinal);
            /// <summary>Automatic step number per node, and the nodes numbered from each start.</summary>
            public readonly Dictionary<string, string> Numbers = new(StringComparer.Ordinal);
            public readonly Dictionary<string, List<string>> NumberedFrom = new(StringComparer.Ordinal);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SketchDocument, FlowGraph> Cache = new();

        private static FlowGraph Graph(SketchDocument doc) => Cache.GetValue(doc, Build);

        /// <summary>True if the step is numbered (process, decision, data …; not start/end, connectors, stored data).</summary>
        public static bool IsNumbered(FlowShape? shape) => shape is FlowShape s && s is not (FlowShape.Terminator or FlowShape.Connector or FlowShape.End or FlowShape.OffPage or FlowShape.Database);

        private static FlowGraph Build(SketchDocument doc)
        {
            var g = new FlowGraph();
            var order = new List<SketchObject>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in doc.Pages)
                foreach (var id in p.Order)
                    if (seen.Add(id) && doc.Get(id) is { } o) order.Add(o);
            foreach (var o in order)
            {
                if (FlowBoxType.ShapeOf(o.Type) is FlowShape s) g.Nodes[o.Id] = s;
                else if (o.Type == "ann.flow.parallel") g.Nodes[o.Id] = null;
            }
            foreach (var c in order)
            {
                if (c.Attach is not { Count: >= 2 } at || at[0]?.ObjectId is not string from || at[at.Count - 1]?.ObjectId is not string to) continue;
                if (from == to || !g.Nodes.ContainsKey(from) || !g.Nodes.ContainsKey(to)) continue;
                if (!g.Out.TryGetValue(from, out var list)) g.Out[from] = list = new List<(string, string)>();
                g.Exits[c.Id] = (from, list.Count);
                list.Add((c.Id, to));
                g.HasIncoming.Add(to);
            }
            foreach (var o in order)
                if (g.Nodes.ContainsKey(o.Id) && o.Flag(NumberParam, false) && !g.NumberedFrom.ContainsKey(o.Id))
                    NumberFrom(g, o);
            return g;
        }

        /// <summary>Numbers the steps reachable from <paramref name="start"/> that no earlier start has numbered.</summary>
        private static void NumberFrom(FlowGraph g, SketchObject start)
        {
            // Depth-first order and loop-back edges (to a node still on the path).
            var pre = new Dictionary<string, int>(StringComparer.Ordinal);
            var onPath = new HashSet<string>(StringComparer.Ordinal);
            var back = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<(string Node, int Next)>();
            pre[start.Id] = 0; onPath.Add(start.Id); stack.Push((start.Id, 0));
            while (stack.Count > 0)
            {
                var (n, i) = stack.Pop();
                var outs = g.Out.TryGetValue(n, out var l) ? l : null;
                if (outs is null || i >= outs.Count) { onPath.Remove(n); continue; }
                stack.Push((n, i + 1));
                var (edge, to) = outs[i];
                if (onPath.Contains(to)) { back.Add(edge); continue; }
                if (pre.ContainsKey(to)) continue;
                pre[to] = pre.Count; onPath.Add(to); stack.Push((to, 0));
            }
            // Topological order without the loop-backs; among the ready steps the one met first depth-first comes first.
            var inDeg = pre.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
            foreach (var n in pre.Keys)
                if (g.Out.TryGetValue(n, out var outs))
                    foreach (var (edge, to) in outs)
                        if (!back.Contains(edge) && inDeg.ContainsKey(to)) inDeg[to]++;
            var ready = new SortedSet<(int, string)>(pre.Where(kv => inDeg[kv.Key] == 0).Select(kv => (kv.Value, kv.Key)));
            var prefix = start.Str(PrefixParam, "S");
            var number = (long)start.Int(FirstParam, 100);
            var step = (long)Math.Max(1, start.Int(IncrementParam, 10));
            var numbered = new List<string>();
            while (ready.Count > 0)
            {
                var first = ready.Min;
                ready.Remove(first);
                var n = first.Item2;
                if (IsNumbered(g.Nodes[n]) && !g.Numbers.ContainsKey(n))
                {
                    g.Numbers[n] = prefix + number.ToString(CultureInfo.InvariantCulture);
                    numbered.Add(n);
                    number += step;
                }
                if (g.Out.TryGetValue(n, out var outs))
                    foreach (var (edge, to) in outs)
                        if (!back.Contains(edge) && inDeg.ContainsKey(to) && --inDeg[to] == 0) ready.Add((pre[to], to));
            }
            g.NumberedFrom[start.Id] = numbered;
        }

        /// <summary>The automatic step number of a flow box ("S110"), or null (numbering off, not reachable, not a numbered step).</summary>
        public static string? StepOf(SketchObject o, RenderContext ctx) => Graph(ctx.Doc).Numbers.TryGetValue(o.Id, out var s) ? s : null;

        /// <summary>True if <paramref name="o"/> starts a flow: no connector leads into it and at least one leads out.</summary>
        public static bool IsFlowStart(SketchObject o, RenderContext ctx)
        {
            var g = Graph(ctx.Doc);
            return !g.HasIncoming.Contains(o.Id) && g.Out.ContainsKey(o.Id);
        }

        /// <summary>
        /// The automatic exit label of a connector leaving a decision with <c>autoExits</c> on ("yes", "no", "1" …, not yet
        /// translated), or null.
        /// </summary>
        public static string? AutoExit(SketchObject connector, RenderContext ctx)
        {
            if (!Graph(ctx.Doc).Exits.TryGetValue(connector.Id, out var exit) || ctx.Doc.Get(exit.From) is not { } d) return null;
            if (FlowBoxType.ShapeOf(d.Type) != FlowShape.Decision || !d.Flag(AutoExitsParam, false)) return null;
            var label = FlowBoxType.ExitLabel(d, exit.Index);
            return label.Length > 0 ? label : null;
        }

        /// <summary>Objects to redraw when a flow box changes: the steps numbered with it, and a decision's exit connectors.</summary>
        /// <summary>True if some flow element of <paramref name="doc"/> numbers steps or labels exits automatically.</summary>
        public static bool IsActive(SketchDocument doc)
            => doc.Objects.Values.Any(o => (FlowBoxType.ShapeOf(o.Type) is not null || o.Type == "ann.flow.parallel")
                && (o.Flag(NumberParam, false) || o.Flag(AutoExitsParam, false)));

        public static IEnumerable<string> DependentsOfBox(SketchObject o, RenderContext ctx)
        {
            var g = Graph(ctx.Doc);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var list in g.NumberedFrom.Where(kv => kv.Key == o.Id || kv.Value.Contains(o.Id)).Select(kv => kv.Value))
                result.UnionWith(list);
            if (g.Out.TryGetValue(o.Id, out var outs) && FlowBoxType.ShapeOf(o.Type) == FlowShape.Decision)
                foreach (var (edge, _) in outs) result.Add(edge);
            result.Remove(o.Id);
            return result;
        }

        /// <summary>
        /// Objects to redraw when a connector of the flow changes: the steps numbered along its flow and the other exits of
        /// the decision it leaves (their positions shift).
        /// </summary>
        public static IEnumerable<string> DependentsOfConnector(SketchObject connector, RenderContext ctx)
        {
            var g = Graph(ctx.Doc);
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (connector.Attach is not { Count: >= 2 } at) return result;
            var ends = new[] { at[0]?.ObjectId, at[at.Count - 1]?.ObjectId }.OfType<string>().ToList();
            // A connector of the flow may join, split or reorder numbered flows: the steps of every numbered flow follow it
            // (evaluated before and after the edit, so steps that drop out are redrawn too).
            if (ends.Any(e => g.Nodes.ContainsKey(e)))
                foreach (var kv in g.NumberedFrom) result.UnionWith(kv.Value);
            if (at[0]?.ObjectId is string from && g.Out.TryGetValue(from, out var outs))
                foreach (var (edge, _) in outs) result.Add(edge);
            result.Remove(connector.Id);
            return result;
        }
    }

    /// <summary>Stand-alone step number label ("S100") for method flowcharts.</summary>
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
