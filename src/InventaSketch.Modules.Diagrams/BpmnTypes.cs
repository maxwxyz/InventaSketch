using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>BPMN event kinds (drawn after the BPMN 2.0 conventions: thin, double or thick circle).</summary>
    public enum EventKind { Start, Intermediate, End }

    /// <summary>
    /// BPMN event circle with an optional trigger marker (message, timer, conditional, signal, error, escalation,
    /// compensation, link, terminate; drawn independently after the BPMN 2.0 conventions): a catching marker is outlined,
    /// a throwing one filled (end events always throw, start events always catch), and a non-interrupting start or boundary
    /// event has a dashed circle. An optional label (object text) is written below the circle, outside the symbol box, as is
    /// usual in BPMN.
    /// </summary>
    public sealed class BpmnEventType : DiagBoxType
    {
        /// <summary>Trigger markers (stored values of the <c>trigger</c> parameter).</summary>
        public static readonly (string Value, string En, string De)[] Triggers =
        {
            ("none", "None", "Keiner"), ("message", "Message", "Nachricht"), ("timer", "Timer", "Zeit"), ("conditional", "Conditional", "Bedingung"),
            ("signal", "Signal", "Signal"), ("error", "Error", "Fehler"), ("escalation", "Escalation", "Eskalation"),
            ("compensation", "Compensation", "Kompensation"), ("link", "Link", "Link"), ("terminate", "Terminate", "Terminierung"),
        };

        private readonly EventKind _kind;

        public BpmnEventType(EventKind kind) : base(
            "diag.bpmn." + kind.ToString().ToLowerInvariant(),
            kind switch
            {
                EventKind.End => new I18nText("End event (BPMN)", "Endereignis (BPMN)"),
                EventKind.Intermediate => new I18nText("Intermediate event (BPMN)", "Zwischenereignis (BPMN)"),
                _ => new I18nText("Start event (BPMN)", "Startereignis (BPMN)"),
            },
            DiagCategories.Bpmn,
            new[] { "bpmn", "process", "prozess", "geschäftsprozess", "event", "ereignis", "circle", "kreis", "message", "nachricht", "timer", "zeit",
                "signal", "error", "fehler", "escalation", "eskalation", "conditional", "bedingung", "compensation", "kompensation", "link", "trigger", "auslöser" }.Concat(kind switch
            {
                EventKind.End => new[] { "end", "ende", "endereignis", "terminate", "terminierung" },
                EventKind.Intermediate => new[] { "intermediate", "zwischenereignis", "catch", "throw", "boundary", "angeheftet", "non-interrupting", "nicht unterbrechend" },
                _ => new[] { "start", "startereignis", "non-interrupting", "nicht unterbrechend" },
            }).ToArray())
        {
            _kind = kind;
        }

        public EventKind Kind => _kind;

        public override Size2 DefaultSize => new(9, 9);
        protected override bool LaneNode => true;
        public override bool PreferUniformScale => true;
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.EllipsePorts(o.Size);

        /// <summary>The triggers this kind of event can have (terminate only at the end; timer and conditional never throw).</summary>
        private IEnumerable<(string Value, string En, string De)> Allowed => Triggers.Where(t => t.Value switch
        {
            "terminate" => _kind == EventKind.End,
            "timer" or "conditional" => _kind != EventKind.End,
            "link" => _kind == EventKind.Intermediate,
            _ => true,
        });

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef> { P.Choice("trigger", "Trigger", "Auslöser", "none", Allowed.ToArray()) };
                if (_kind == EventKind.Intermediate) list.Add(P.Bool("throwing", "Throwing (filled marker)", "Auslösend (gefüllte Markierung)", false));
                if (_kind != EventKind.End) list.Add(P.Bool("interrupting", "Interrupting (solid circle)", "Unterbrechend (durchgezogener Kreis)", true));
                list.Add(P.TextSize());
                return list;
            }
        }

        /// <summary>True if the marker is filled: end events always, intermediate events when throwing.</summary>
        public bool Throwing(SketchObject o) => _kind == EventKind.End || _kind == EventKind.Intermediate && o.Flag("throwing", false);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            var width = _kind == EventKind.End ? Math.Min(1.4, Math.Min(rx, ry) * 0.3) : D.Stroke;
            var dashed = _kind != EventKind.End && !o.Flag("interrupting", true);
            var circle = D.Outline with { StrokeWidth = width };
            var list = new List<Primitive>
            {
                Prim.Ellipse(0, 0, Math.Max(0.1, rx - width / 2), Math.Max(0.1, ry - width / 2), dashed ? D.Dashed(circle) : circle) with { Tag = DiagTags.Outline },
            };
            if (_kind == EventKind.Intermediate)
            {
                var g = Math.Min(1.0, Math.Min(rx, ry) * 0.2);
                list.Add(Prim.Ellipse(0, 0, Math.Max(0.1, rx - g), Math.Max(0.1, ry - g), dashed ? D.Dashed(D.OutlineOpen) : D.OutlineOpen) with { Tag = DiagTags.Outline });
            }
            var trigger = o.Str("trigger", "none");
            if (Allowed.Any(t => t.Value == trigger))
                list.AddRange(Marker(trigger, Math.Min(rx, ry) * (_kind == EventKind.Start ? 0.62 : 0.55), Throwing(o)));
            list.AddRange(BelowLabel(o.Text, ry, TextSizeOf(o)));
            return list;
        }

        /// <summary>The trigger marker inside a circle of radius <paramref name="r"/>/0.6, filled when throwing.</summary>
        internal static IEnumerable<Primitive> Marker(string trigger, double r, bool filled)
        {
            var line = Style.Visible with { StrokeWidth = Math.Max(0.15, r * 0.09) };
            var shape = line with { Fill = Fill.Solid(filled ? D.Ink : D.Paper) };
            var inner = line with { Stroke = filled ? D.Paper : D.Ink };
            Primitive Poly(params double[] xy)
            {
                var pts = new List<Vec2>();
                for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vec2(xy[i] * r, xy[i + 1] * r));
                return Prim.Polygon(pts, shape) with { Tag = DiagTags.Marker };
            }
            switch (trigger)
            {
                case "message":
                    yield return Prim.Rect(-0.8 * r, -0.55 * r, 1.6 * r, 1.1 * r, shape) with { Tag = DiagTags.Marker };
                    yield return Prim.Polyline(new[] { new Vec2(-0.8 * r, -0.55 * r), new Vec2(0, 0.05 * r), new Vec2(0.8 * r, -0.55 * r) }, inner) with { Tag = DiagTags.Marker };
                    break;
                case "timer":
                    yield return Prim.Ellipse(0, 0, 0.85 * r, 0.85 * r, line with { Fill = Fill.Solid(D.Paper) }) with { Tag = DiagTags.Marker };
                    for (var i = 0; i < 12; i++)
                    {
                        var a = i * Math.PI / 6;
                        yield return new LinePrim(new Vec2(Math.Sin(a) * 0.62 * r, -Math.Cos(a) * 0.62 * r), new Vec2(Math.Sin(a) * 0.82 * r, -Math.Cos(a) * 0.82 * r), line) { Tag = DiagTags.Marker };
                    }
                    yield return Prim.Polyline(new[] { new Vec2(0, -0.55 * r), new Vec2(0, 0), new Vec2(0.4 * r, 0.1 * r) }, line) with { Tag = DiagTags.Marker };
                    break;
                case "conditional":
                    yield return Prim.Rect(-0.55 * r, -0.75 * r, 1.1 * r, 1.5 * r, line with { Fill = Fill.Solid(D.Paper) }) with { Tag = DiagTags.Marker };
                    for (var i = 0; i < 4; i++)
                    {
                        var y = -0.45 * r + i * 0.3 * r;
                        yield return new LinePrim(new Vec2(-0.38 * r, y), new Vec2(0.38 * r, y), line) { Tag = DiagTags.Marker };
                    }
                    break;
                case "signal":
                    yield return Poly(0, -0.8, 0.75, 0.55, -0.75, 0.55);
                    break;
                case "error":
                    yield return Poly(-0.7, 0.75, -0.3, -0.7, 0.15, 0.1, 0.7, -0.75, 0.3, 0.7, -0.15, -0.1);
                    break;
                case "escalation":
                    yield return Poly(0, -0.8, 0.6, 0.75, 0, 0.25, -0.6, 0.75);
                    break;
                case "compensation":
                    yield return Poly(-0.8, 0, 0, -0.55, 0, 0.55);
                    yield return Poly(0, 0, 0.8, -0.55, 0.8, 0.55);
                    break;
                case "link":
                    yield return Poly(-0.75, -0.3, 0.15, -0.3, 0.15, -0.7, 0.8, 0, 0.15, 0.7, 0.15, 0.3, -0.75, 0.3);
                    break;
                case "terminate":
                    yield return Prim.Ellipse(0, 0, 0.85 * r, 0.85 * r, D.Solid) with { Tag = DiagTags.Marker };
                    break;
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var def = Parameters.First(p => p.Key == "trigger");
            foreach (var a in ActionChoices.FromParam(o, "diag.bpmn.trigger", def, null, "marker", null)) yield return a;
            if (_kind == EventKind.Intermediate)
                yield return D.Toggle(o, "diag.bpmn.throwing", new I18nText("Throwing", "Auslösend"), "throwing", false, "marker");
            if (_kind != EventKind.End)
                yield return D.Toggle(o, "diag.bpmn.interrupting", new I18nText("Interrupting", "Unterbrechend"), "interrupting", true, "marker");
        }

        /// <summary>BPMN-style label centred below the symbol (outside the box).</summary>
        internal static IEnumerable<Primitive> BelowLabel(string? text, double hh, double size)
        {
            var lines = D.Lines(text);
            var rowH = size * D.LineFactor;
            for (var i = 0; i < lines.Count; i++) yield return D.Text(new Vec2(0, hh + 1 + (i + 0.5) * rowH), lines[i], size, DiagTags.Label);
        }
    }

    /// <summary>BPMN task (rounded rectangle) or collapsed sub-process (with the [+] marker).</summary>
    public sealed class BpmnTaskType : DiagBoxType
    {
        public BpmnTaskType() : base("diag.bpmn.task", new I18nText("Task (BPMN)", "Aufgabe (BPMN)"), DiagCategories.Bpmn,
            "bpmn", "process", "prozess", "geschäftsprozess", "task", "aufgabe", "activity", "aktivität", "subprocess", "teilprozess") { }

        public override Size2 DefaultSize => new(32, 20);
        protected override bool LaneNode => true;
        protected override I18nText Placeholder => new("Task", "Aufgabe");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("subprocess", "Collapsed sub-process [+]", "Zugeklappter Teilprozess [+]", false),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hh = h / 2;
            var list = new List<Primitive> { D.RoundedRect(-w / 2, -hh, w, h, Math.Min(2.5, Math.Min(w, h) * 0.2), D.Outline) with { Tag = DiagTags.Outline } };
            var sub = o.Flag("subprocess", false);
            var m = Math.Min(3.2, h * 0.22);
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2(0, sub ? -m / 2 : 0), w - 3, h - 2 - (sub ? m + 1 : 0));
            if (sub)
            {
                var y = hh - 0.8 - m;
                list.Add(Prim.Rect(-m / 2, y, m, m, D.Thin) with { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(-m * 0.3, y + m / 2), new Vec2(m * 0.3, y + m / 2), D.Thin) { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(0, y + m * 0.2), new Vec2(0, y + m * 0.8), D.Thin) { Tag = DiagTags.Marker });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.bpmn.subprocess", new I18nText("Sub-process", "Teilprozess"), "subprocess", false);
        }
    }

    /// <summary>BPMN gateway: diamond with an exclusive (X), parallel (+), inclusive (O) or event-based marker.</summary>
    public sealed class BpmnGatewayType : DiagBoxType
    {
        public static readonly string[] Markers = { "exclusive", "parallel", "inclusive", "event", "none" };

        public BpmnGatewayType() : base("diag.bpmn.gateway", new I18nText("Gateway (BPMN)", "Gateway (BPMN)"), DiagCategories.Bpmn,
            "bpmn", "process", "prozess", "gateway", "verzweigung", "xor", "and", "or", "exclusive", "exklusiv", "parallel", "inclusive", "inklusiv", "diamond", "raute") { }

        public override Size2 DefaultSize => new(12, 12);
        protected override bool LaneNode => true;
        public override bool PreferUniformScale => true;
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.DiamondPorts(o.Size);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("marker", "Marker", "Markierung", "exclusive", ("exclusive", "Exclusive (X)", "Exklusiv (X)"), ("parallel", "Parallel (+)", "Parallel (+)"),
                ("inclusive", "Inclusive (O)", "Inklusiv (O)"), ("event", "Event-based", "Ereignisbasiert"), ("none", "None", "Keine")),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var list = new List<Primitive> { D.Diamond(hw, hh, D.Outline) with { Tag = DiagTags.Outline } };
            double mx = hw * 0.36, my = hh * 0.36;
            var thick = Style.Visible with { StrokeWidth = Math.Max(0.5, Math.Min(hw, hh) * 0.12) };
            switch (o.Str("marker", "exclusive"))
            {
                case "parallel":
                    list.Add(new LinePrim(new Vec2(-mx * 1.1, 0), new Vec2(mx * 1.1, 0), thick) { Tag = DiagTags.Marker });
                    list.Add(new LinePrim(new Vec2(0, -my * 1.1), new Vec2(0, my * 1.1), thick) { Tag = DiagTags.Marker });
                    break;
                case "inclusive":
                    list.Add(Prim.Ellipse(0, 0, mx, my, thick) with { Tag = DiagTags.Marker });
                    break;
                case "event":
                    list.Add(Prim.Ellipse(0, 0, mx * 1.15, my * 1.15, D.Thin) with { Tag = DiagTags.Marker });
                    list.Add(Prim.Ellipse(0, 0, mx * 0.95, my * 0.95, D.Thin) with { Tag = DiagTags.Marker });
                    list.Add(Prim.Polygon(Enumerable.Range(0, 5).Select(i => new Vec2(mx * 0.6 * Math.Sin(i * 2 * Math.PI / 5), -my * 0.6 * Math.Cos(i * 2 * Math.PI / 5))), D.Thin) with { Tag = DiagTags.Marker });
                    break;
                case "none":
                    break;
                default:
                    list.Add(new LinePrim(new Vec2(-mx * 0.8, -my * 0.8), new Vec2(mx * 0.8, my * 0.8), thick) { Tag = DiagTags.Marker });
                    list.Add(new LinePrim(new Vec2(-mx * 0.8, my * 0.8), new Vec2(mx * 0.8, -my * 0.8), thick) { Tag = DiagTags.Marker });
                    break;
            }
            list.AddRange(BpmnEventType.BelowLabel(o.Text, hh, TextSizeOf(o)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "diag.gateway.marker", Parameters.First(p => p.Key == "marker"), null, "marker", null)) yield return a;
        }
    }

    /// <summary>
    /// BPMN data object (page with a folded corner) or data store (cylinder): optional input/output arrow and collection
    /// marker (three bars) on the data object; the label (object text) stands below, as on events.
    /// </summary>
    public sealed class BpmnDataType : DiagBoxType
    {
        private readonly bool _store;

        public BpmnDataType(bool store) : base(store ? "diag.bpmn.datastore" : "diag.bpmn.dataobject",
            store ? new I18nText("Data store (BPMN)", "Datenspeicher (BPMN)") : new I18nText("Data object (BPMN)", "Datenobjekt (BPMN)"), DiagCategories.Bpmn,
            new[] { "bpmn", "process", "prozess", "data", "daten" }.Concat(store
                ? new[] { "data store", "datenspeicher", "database", "datenbank", "cylinder", "zylinder" }
                : new[] { "data object", "datenobjekt", "document", "dokument", "input", "eingabe", "output", "ausgabe", "collection", "sammlung" }).ToArray())
        {
            _store = store;
        }

        public override Size2 DefaultSize => _store ? new Size2(14, 12) : new Size2(10, 13);

        public override IReadOnlyList<ParamDef> Parameters => _store
            ? new[] { P.TextSize() }
            : new[]
            {
                P.Choice("role", "Role", "Rolle", "none", ("none", "Data object", "Datenobjekt"), ("input", "Data input", "Dateneingabe"), ("output", "Data output", "Datenausgabe")),
                P.Bool("collection", "Collection (three bars)", "Sammlung (drei Striche)", false),
                P.TextSize(),
            };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var list = new List<Primitive>();
            if (_store)
            {
                // Bottom ellipse, the body covering its upper half, the sides, the top ellipse and two rings.
                var ry = Math.Min(hh * 0.3, hw * 0.35);
                list.Add(Prim.Ellipse(0, hh - ry, hw, ry, D.Outline) with { Tag = DiagTags.Outline });
                list.Add(Prim.Rect(-hw, -hh + ry, 2 * hw, 2 * hh - 2 * ry, Style.Visible with { Stroke = "none", Fill = Fill.Solid(D.Paper) }) with { Tag = DiagTags.Outline });
                list.Add(new LinePrim(new Vec2(-hw, -hh + ry), new Vec2(-hw, hh - ry), D.OutlineOpen) { Tag = DiagTags.Outline });
                list.Add(new LinePrim(new Vec2(hw, -hh + ry), new Vec2(hw, hh - ry), D.OutlineOpen) { Tag = DiagTags.Outline });
                list.Add(Prim.Ellipse(0, -hh + ry, hw, ry, D.Outline) with { Tag = DiagTags.Outline });
                for (var i = 1; i <= 2; i++)
                {
                    var y = -hh + ry + i * ry * 0.6;
                    list.Add(Prim.Path(new[] { PathSeg.Move(new Vec2(-hw, y)), PathSeg.Quad(new Vec2(0, y + 2 * ry), new Vec2(hw, y)) }, D.OutlineOpen) with { Tag = DiagTags.Marker });
                }
            }
            else
            {
                var fold = Math.Min(hw, hh) * 0.55;
                list.Add(Prim.Polygon(new[] { new Vec2(-hw, -hh), new Vec2(hw - fold, -hh), new Vec2(hw, -hh + fold), new Vec2(hw, hh), new Vec2(-hw, hh) }, D.Outline)
                    with { Tag = DiagTags.Outline });
                list.Add(Prim.Polyline(new[] { new Vec2(hw - fold, -hh), new Vec2(hw - fold, -hh + fold), new Vec2(hw, -hh + fold) }, D.Thin) with { Tag = DiagTags.Outline });
                var role = o.Str("role", "none");
                if (role is "input" or "output")
                {
                    var s = Math.Min(hw, hh) * 0.35;
                    double x = -hw + s * 0.4, y = -hh + s * 0.5;
                    var pts = new[]
                    {
                        new Vec2(x, y + s * 0.3), new Vec2(x + s, y + s * 0.3), new Vec2(x + s, y), new Vec2(x + s * 1.7, y + s * 0.6),
                        new Vec2(x + s, y + s * 1.2), new Vec2(x + s, y + s * 0.9), new Vec2(x, y + s * 0.9),
                    };
                    list.Add(Prim.Polygon(pts, Style.Visible with { StrokeWidth = 0.2, Fill = Fill.Solid(role == "output" ? D.Ink : D.Paper) }) with { Tag = DiagTags.Marker });
                }
                if (o.Flag("collection", false))
                {
                    var bh = Math.Min(hh * 0.35, 2.4);
                    for (var i = -1; i <= 1; i++)
                        list.Add(new LinePrim(new Vec2(i * bh * 0.45, hh - 0.6 - bh), new Vec2(i * bh * 0.45, hh - 0.6), Style.Visible with { StrokeWidth = 0.35 }) { Tag = DiagTags.Marker });
                }
            }
            list.AddRange(BpmnEventType.BelowLabel(o.Text, hh, TextSizeOf(o)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            if (_store) yield break;
            foreach (var a in ActionChoices.FromParam(o, "diag.bpmn.data.role", Parameters.First(p => p.Key == "role"), null, "marker", null)) yield return a;
            yield return D.Toggle(o, "diag.bpmn.data.collection", new I18nText("Collection", "Sammlung"), "collection", false, "marker");
        }
    }

    /// <summary>
    /// BPMN connecting objects: sequence flow (solid, filled arrowhead), default flow (slash at the start), conditional flow
    /// (hollow diamond at the start), message flow (dashed, small circle at the start, hollow arrowhead), association
    /// (dotted, no head) and data association (dotted, open arrowhead). The object text is the condition or message name.
    /// </summary>
    public sealed class BpmnFlowType : DiagConnectorType
    {
        public static readonly (string Value, string En, string De)[] Kinds =
        {
            ("sequence", "Sequence flow", "Sequenzfluss"), ("default", "Default flow", "Standardfluss"), ("conditional", "Conditional flow", "Bedingter Fluss"),
            ("message", "Message flow", "Nachrichtenfluss"), ("association", "Association", "Assoziation"), ("data", "Data association", "Datenassoziation"),
        };

        public BpmnFlowType() : base("diag.bpmn.flow", new I18nText("Flow (BPMN)", "Fluss (BPMN)"), DiagCategories.Bpmn,
            "bpmn", "process", "prozess", "flow", "fluss", "sequence flow", "sequenzfluss", "message flow", "nachrichtenfluss", "default flow", "standardfluss",
            "conditional", "bedingt", "association", "assoziation", "data association", "datenassoziation", "connector", "verbinder", "arrow", "pfeil") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Choice("kind", "Kind", "Art", "sequence", Kinds), P.Route(), P.TextSize() };

        private static string KindOf(SketchObject o) => o.Str("kind", "sequence");

        protected override DiagEnd StartEnd(SketchObject o) => KindOf(o) == "conditional" ? DiagEnd.HollowDiamond : DiagEnd.None;

        protected override DiagEnd EndEnd(SketchObject o) => KindOf(o) switch
        {
            "message" => DiagEnd.Hollow,
            "association" => DiagEnd.None,
            "data" => DiagEnd.Open,
            _ => DiagEnd.Filled,
        };

        protected override bool IsDashed(SketchObject o) => KindOf(o) is "message" or "association" or "data";

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var list = base.RenderPoints(o, ctx, p).ToList();
            var path = Path(o, p);
            if (path.Count < 2) return list;
            var dir = (path[1] - path[0]).Normalized();
            var n = new Vec2(-dir.Y, dir.X);
            switch (KindOf(o))
            {
                case "message":
                    {
                        // A small open circle at the start of a message flow.
                        const double r = 0.9;
                        list.Add(Prim.Ellipse(path[0].X + dir.X * r, path[0].Y + dir.Y * r, r, r, Style.ThinLine with { StrokeWidth = D.LineWidth, Fill = Fill.Solid(D.Paper) })
                            with { Tag = DiagTags.Marker });
                        break;
                    }
                case "default":
                    {
                        // A short slash across the line near its start.
                        var c = path[0] + dir * 2.5;
                        list.Add(new LinePrim(c - dir * 1.0 - n * 1.3, c + dir * 1.0 + n * 1.3, Style.ThinLine with { StrokeWidth = D.LineWidth }) { Tag = DiagTags.Marker });
                        break;
                    }
            }
            if (KindOf(o) is "association" or "data")
                list = list.Select(x => x.Tag == DiagTags.Main ? Prim.Restyle(x, s => s with { Dash = DashStyle.Dot }) : x).ToList();
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "diag.bpmn.flow.kind", Parameters.First(p => p.Key == "kind"), null, "kind", null)) yield return a;
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
            foreach (var a in RouteActions(o)) yield return a;
        }
    }
}
