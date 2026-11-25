using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>UML activity action: rounded rectangle with centred text.</summary>
    public sealed class ActionType : DiagBoxType
    {
        public ActionType() : base("diag.activity.action", new I18nText("Action (activity)", "Aktion (Aktivität)"), DiagCategories.Activity,
            "uml", "activity", "aktivität", "aktivitätsdiagramm", "action", "aktion", "step", "schritt") { }

        public override Size2 DefaultSize => new(36, 14);
        protected override bool LaneNode => true;
        protected override I18nText Placeholder => new("Action", "Aktion");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { D.RoundedRect(-w / 2, -h / 2, w, h, Math.Min(4, h * 0.35), D.Outline) with { Tag = DiagTags.Outline } };
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), Vec2.Zero, w - 4, h - 2);
            return list;
        }
    }

    /// <summary>Initial node, activity final, flow final (UML activity and state machines).</summary>
    public enum NodeKind { Initial, Final, FlowFinal }

    /// <summary>Round control node: filled dot (initial), bull's eye (final) or circle with a cross (flow final).</summary>
    public sealed class ControlNodeType : DiagBoxType
    {
        private readonly NodeKind _kind;

        public ControlNodeType(NodeKind kind) : base(
            kind switch { NodeKind.Final => "diag.activity.final", NodeKind.FlowFinal => "diag.activity.flowfinal", _ => "diag.activity.initial" },
            kind switch
            {
                NodeKind.Final => new I18nText("Final node", "Endknoten"),
                NodeKind.FlowFinal => new I18nText("Flow final node", "Ablaufende"),
                _ => new I18nText("Initial node", "Startknoten"),
            },
            DiagCategories.Activity,
            new[] { "uml", "activity", "aktivität", "state", "zustand", "node", "knoten" }.Concat(kind switch
            {
                NodeKind.Final => new[] { "final", "end", "ende", "endzustand", "bullseye" },
                NodeKind.FlowFinal => new[] { "flow final", "ablaufende", "cross", "kreuz" },
                _ => new[] { "initial", "start", "anfang", "startzustand", "dot", "punkt" },
            }).ToArray())
        {
            _kind = kind;
        }

        public override Size2 DefaultSize => _kind == NodeKind.Initial ? new Size2(6, 6) : new Size2(7, 7);
        protected override bool LaneNode => true;
        public override bool PreferUniformScale => true;
        public override bool HasText => false;
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.EllipsePorts(o.Size);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            switch (_kind)
            {
                case NodeKind.Final:
                    return new Primitive[]
                    {
                        Prim.Ellipse(0, 0, rx, ry, D.Outline) with { Tag = DiagTags.Outline },
                        Prim.Ellipse(0, 0, rx * 0.6, ry * 0.6, D.Solid) with { Tag = DiagTags.Marker },
                    };
                case NodeKind.FlowFinal:
                    {
                        var k = Math.Sqrt(0.5);
                        return new Primitive[]
                        {
                            Prim.Ellipse(0, 0, rx, ry, D.Outline) with { Tag = DiagTags.Outline },
                            new LinePrim(new Vec2(-rx * k, -ry * k), new Vec2(rx * k, ry * k), D.OutlineOpen) { Tag = DiagTags.Marker },
                            new LinePrim(new Vec2(-rx * k, ry * k), new Vec2(rx * k, -ry * k), D.OutlineOpen) { Tag = DiagTags.Marker },
                        };
                    }
                default:
                    return new Primitive[] { Prim.Ellipse(0, 0, rx, ry, D.Solid) with { Tag = DiagTags.Outline } };
            }
        }
    }

    /// <summary>Decision / merge node: diamond with an optional condition text.</summary>
    public sealed class DecisionType : DiagBoxType
    {
        public DecisionType() : base("diag.activity.decision", new I18nText("Decision / merge", "Verzweigung / Zusammenführung"), DiagCategories.Activity,
            "uml", "activity", "aktivität", "decision", "entscheidung", "verzweigung", "merge", "zusammenführung", "diamond", "raute", "if") { }

        public override Size2 DefaultSize => new(12, 12);
        protected override bool LaneNode => true;
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.DiamondPorts(o.Size);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { D.Diamond(w / 2, h / 2, D.Outline) with { Tag = DiagTags.Outline } };
            D.CenterText(list, o.Text, TextSizeOf(o), Vec2.Zero, w * 0.5, h * 0.5);
            return list;
        }
    }

    /// <summary>Fork / join synchronisation bar (horizontal or vertical by its aspect ratio).</summary>
    public sealed class BarType : DiagBoxType
    {
        public BarType() : base("diag.activity.bar", new I18nText("Fork / join bar", "Gabelung / Vereinigung"), DiagCategories.Activity,
            "uml", "activity", "aktivität", "fork", "join", "gabelung", "vereinigung", "parallel", "synchronisation", "bar", "balken") { }

        public override Size2 DefaultSize => new(40, 2.5);
        protected override bool LaneNode => true;
        public override bool HasText => false;

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            // Five ports along each long side so several flows can fork/join.
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var horizontal = o.Size.W >= o.Size.H;
            for (var i = 0; i < 5; i++)
            {
                var t = -0.4 + 0.2 * i;
                if (horizontal)
                {
                    yield return ("top." + D.Inv(i), new Vec2(t * 2 * hw, -hh));
                    yield return ("bottom." + D.Inv(i), new Vec2(t * 2 * hw, hh));
                }
                else
                {
                    yield return ("left." + D.Inv(i), new Vec2(-hw, t * 2 * hh));
                    yield return ("right." + D.Inv(i), new Vec2(hw, t * 2 * hh));
                }
            }
            foreach (var p in D.RectPorts(o.Size)) yield return p;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H, D.Solid with { Stroke = "none" }) with { Tag = DiagTags.Outline } };
    }

    /// <summary>
    /// UML state: rounded rectangle with the name and optional internal activities (entry / do / exit), each a text
    /// parameter; several lines per activity are allowed.
    /// </summary>
    public sealed class StateType : DiagBoxType
    {
        public StateType() : base("diag.state.state", new I18nText("State", "Zustand"), DiagCategories.State,
            "uml", "state", "zustand", "state machine", "zustandsautomat", "zustandsdiagramm", "entry", "exit", "do") { }

        public override Size2 DefaultSize => new(36, 16);
        protected override I18nText Placeholder => new("State", "Zustand");

        /// <summary>Rounded rectangle (UML) or circle with the name (black-and-white state diagrams of method figures).</summary>
        public override IReadOnlyList<ViewDef> Views => new[]
        {
            new ViewDef("rounded", new I18nText("Rounded rectangle", "Abgerundetes Rechteck")) { Icon = IconIds.ShapeSquare, Size = new Size2(36, 16) },
            new ViewDef("circle", new I18nText("Circle", "Kreis")) { Icon = IconIds.ShapeCircle, Size = new Size2(20, 20) },
        };

        public static bool IsCircle(SketchObject o) => o.View == "circle";

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => IsCircle(o) ? D.EllipsePorts(o.Size) : D.RectPorts(o.Size);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("entry", "Entry activity", "Eintrittsaktivität", null),
            P.Text("do", "Do activity", "Zustandsaktivität", null),
            P.Text("exit", "Exit activity", "Austrittsaktivität", null),
            P.Bool("showActivities", "Show activities", "Aktivitäten anzeigen", true),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            if (IsCircle(o))
            {
                // Circle (ellipse when stretched) with the name in the inscribed square; activities are not shown.
                var c = new List<Primitive> { Prim.Ellipse(0, 0, w / 2, h / 2, D.Outline) with { Tag = DiagTags.Outline } };
                D.CenterText(c, Label(o, ctx), TextSizeOf(o), Vec2.Zero, w * 0.68, h * 0.68, DiagTags.Name);
                return c;
            }
            var list = new List<Primitive> { D.RoundedRect(-w / 2, -h / 2, w, h, Math.Min(4, Math.Min(w, h) * 0.3), D.Outline) with { Tag = DiagTags.Outline } };
            var sections = new List<IReadOnlyList<DiagRow>> { D.Header(null, Label(o, ctx)) };
            if (o.Flag("showActivities", true))
            {
                var rows = new List<DiagRow>();
                foreach (var key in new[] { "entry", "do", "exit" })
                    foreach (var line in D.Lines(o.Str(key, ""))) rows.Add(new DiagRow(key + " / " + line));
                if (rows.Count > 0) sections.Add(rows);
            }
            var pad = Math.Min(2, w * 0.05);
            list.AddRange(D.Compartments(w - 2 * pad, h, sections, TextSizeOf(o)).Select(p => p is LinePrim l && l.Tag == DiagTags.Divider
                ? l with { A = new Vec2(-w / 2, l.A.Y), B = new Vec2(w / 2, l.B.Y) } : p));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.state.activities", new I18nText("Show activities", "Aktivitäten anzeigen"), "showActivities", true, "compartments");
            yield return D.AddRow("diag.state.addEntry", new I18nText("Add entry activity", "Eintrittsaktivität hinzufügen"), "entry", "", "action()");
            yield return D.AddRow("diag.state.addExit", new I18nText("Add exit activity", "Austrittsaktivität hinzufügen"), "exit", "", "action()");
            yield return D.RemoveRow("diag.state.removeExit", new I18nText("Remove last exit activity", "Letzte Austrittsaktivität entfernen"), "exit", "");
        }
    }

    /// <summary>Composite state: name compartment and one or more orthogonal regions separated by dashed lines.</summary>
    public sealed class CompositeStateType : DiagBoxType
    {
        public CompositeStateType() : base("diag.state.composite", new I18nText("Composite state", "Zusammengesetzter Zustand"), DiagCategories.State,
            "uml", "state", "zustand", "composite", "zusammengesetzt", "region", "orthogonal", "superstate", "oberzustand", "container") { }

        public override Size2 DefaultSize => new(90, 56);
        protected override I18nText Placeholder => new("Composite state", "Zusammengesetzter Zustand");

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Int("regions", "Regions", "Regionen", 1, 1, 6), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var size = TextSizeOf(o);
            var nameH = Math.Min(h * 0.3, size * D.LineFactor + 2);
            var list = new List<Primitive>
            {
                D.RoundedRect(-hw, -hh, w, h, Math.Min(5, Math.Min(w, h) * 0.2), D.OutlineOpen) with { Tag = DiagTags.Outline },
                new LinePrim(new Vec2(-hw, -hh + nameH), new Vec2(hw, -hh + nameH), D.OutlineOpen) { Tag = DiagTags.Divider },
            };
            D.CenterText(list, Label(o, ctx), size, new Vec2(0, -hh + nameH / 2), w - 6, nameH - 1, DiagTags.Name, bold: true);
            var regions = Math.Max(1, Math.Min(6, o.Int("regions", 1)));
            var rh = (h - nameH) / regions;
            for (var i = 1; i < regions; i++)
            {
                var y = -hh + nameH + i * rh;
                list.Add(new LinePrim(new Vec2(-hw, y), new Vec2(hw, y), D.Dashed(D.Thin)) { Tag = DiagTags.Divider });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.state.addRegion", new I18nText("Add region", "Region hinzufügen"), IconIds.Text,
                x => x.WithParam("regions", Math.Min(6, x.Int("regions", 1) + 1))) { Group = "rows" };
            yield return new ContextAction("diag.state.removeRegion", new I18nText("Remove region", "Region entfernen"), IconIds.Delete,
                x => x.WithParam("regions", Math.Max(1, x.Int("regions", 1) - 1))) { Group = "rows" };
        }
    }
}
