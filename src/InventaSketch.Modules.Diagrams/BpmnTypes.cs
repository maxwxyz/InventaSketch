using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>BPMN event kinds (drawn after the BPMN 2.0 conventions: thin, double or thick circle).</summary>
    public enum EventKind { Start, Intermediate, End }

    /// <summary>
    /// BPMN event circle. An optional label (object text) is written below the circle, outside the symbol box, as is
    /// usual in BPMN.
    /// </summary>
    public sealed class BpmnEventType : DiagBoxType
    {
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
            new[] { "bpmn", "process", "prozess", "geschäftsprozess", "event", "ereignis", "circle", "kreis" }.Concat(kind switch
            {
                EventKind.End => new[] { "end", "ende", "endereignis" },
                EventKind.Intermediate => new[] { "intermediate", "zwischenereignis", "catch", "throw" },
                _ => new[] { "start", "startereignis", "trigger" },
            }).ToArray())
        {
            _kind = kind;
        }

        public override Size2 DefaultSize => new(9, 9);
        protected override bool LaneNode => true;
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.EllipsePorts(o.Size);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            var width = _kind == EventKind.End ? Math.Min(1.4, Math.Min(rx, ry) * 0.3) : D.Stroke;
            var list = new List<Primitive>
            {
                Prim.Ellipse(0, 0, Math.Max(0.1, rx - width / 2), Math.Max(0.1, ry - width / 2), D.Outline with { StrokeWidth = width }) with { Tag = DiagTags.Outline },
            };
            if (_kind == EventKind.Intermediate)
            {
                var g = Math.Min(1.0, Math.Min(rx, ry) * 0.2);
                list.Add(Prim.Ellipse(0, 0, Math.Max(0.1, rx - g), Math.Max(0.1, ry - g), D.OutlineOpen) with { Tag = DiagTags.Outline });
            }
            list.AddRange(BelowLabel(o.Text, ry, TextSizeOf(o)));
            return list;
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
}
