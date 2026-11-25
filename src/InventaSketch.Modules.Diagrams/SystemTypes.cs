using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// System block with ports: rectangle with name and optional stereotype; small port squares sit on the edges
    /// (count per side as parameters) and are exposed as Port anchors, so connectors snap to them.
    /// </summary>
    public sealed class BlockType : DiagBoxType
    {
        /// <summary>Edge length of a port square (mm).</summary>
        public const double PortSize = 2.4;

        public BlockType() : base("diag.sys.block", new I18nText("Block with ports", "Block mit Ports"), DiagCategories.System,
            "block", "system", "subsystem", "teilsystem", "port", "ports", "anschluss", "sysml", "architecture", "architektur", "blockdiagramm", "block diagram", "module", "modul") { }

        public override Size2 DefaultSize => new(40, 24);
        protected override I18nText Placeholder => new("Block", "Block");

        public static readonly (string Value, string En, string De)[] Roles =
        {
            ("none", "None (Block)", "Keine (Block)"), ("processor", "Processor", "Prozessor"), ("memory", "Memory", "Speicher"),
            ("storage", "Storage", "Massenspeicher"), ("network", "Network interface", "Netzwerkschnittstelle"), ("io", "I/O interface", "E/A-Schnittstelle"),
            ("display", "Display", "Anzeige"), ("sensor", "Sensor(s)", "Sensor(en)"), ("input", "Input device", "Eingabegerät"),
        };

        /// <summary>The name shown: the object text, else the localised name of the role (computer block diagrams), else "Block".</summary>
        public static string NameOf(SketchObject o, Lang lang)
        {
            if (o.Text is not null) return o.Text;
            var role = o.Str("role", "none");
            var r = Array.Find(Roles, x => x.Value == role);
            return r.Value is null || role == "none" ? "Block" : new I18nText(r.En, r.De).Get(lang);
        }

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Stereotype(),
            P.Choice("role", "Role (placeholder name)", "Rolle (Platzhaltername)", "none", Roles),
            P.Int("portsLeft", "Ports left", "Ports links", 1, 0, 12),
            P.Int("portsRight", "Ports right", "Ports rechts", 1, 0, 12),
            P.Int("portsTop", "Ports top", "Ports oben", 0, 0, 12),
            P.Int("portsBottom", "Ports bottom", "Ports unten", 0, 0, 12),
            P.TextSize(),
        };

        private static readonly (string Key, string Side, int Def)[] Sides = { ("portsLeft", "L", 1), ("portsRight", "R", 1), ("portsTop", "T", 0), ("portsBottom", "B", 0) };

        /// <summary>Port positions on the box edge (local), with ids "L1", "R2", ... and the inward normal.</summary>
        public static IEnumerable<(string Id, Vec2 P, Vec2 Inward)> PortPositions(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            foreach (var (key, side, def) in Sides)
            {
                var n = Math.Max(0, Math.Min(12, o.Int(key, def)));
                for (var i = 0; i < n; i++)
                {
                    var t = (i + 1.0) / (n + 1) - 0.5;
                    var id = side + D.Inv(i + 1);
                    yield return side switch
                    {
                        "L" => (id, new Vec2(-hw, t * o.Size.H), new Vec2(1, 0)),
                        "R" => (id, new Vec2(hw, t * o.Size.H), new Vec2(-1, 0)),
                        "T" => (id, new Vec2(t * o.Size.W, -hh), new Vec2(0, 1)),
                        _ => (id, new Vec2(t * o.Size.W, hh), new Vec2(0, -1)),
                    };
                }
            }
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var m = PortSize / 2;
            // The body is inset by half a port so the port squares straddle its edge yet stay inside the object box.
            var list = new List<Primitive> { Prim.Rect(-w / 2 + m, -h / 2 + m, w - 2 * m, h - 2 * m, D.Outline) with { Tag = DiagTags.Outline } };
            var rows = D.Header(o.Str("stereotype", ""), NameOf(o, ctx.Lang));
            list.AddRange(D.Compartments(w - 2 * PortSize - 2, h - 2 * PortSize, new[] { rows }, TextSizeOf(o)));
            foreach (var (_, p, inward) in PortPositions(o))
            {
                var c = p + inward * m;
                list.Add(Prim.Rect(c.X - m, c.Y - m, PortSize, PortSize, D.Outline with { StrokeWidth = 0.25 }) with { Tag = DiagTags.Port });
            }
            return list;
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
            => PortPositions(o).Select(p => (p.Id, p.P)).Concat(D.RectPorts(o.Size));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            ContextAction Step(string id, string en, string de, string key, int def, int delta) =>
                new(id, new I18nText(en, de), delta > 0 ? IconIds.PortAdd : IconIds.PortRemove, x => x.WithParam(key, Math.Max(0, Math.Min(12, x.Int(key, def) + delta)))) { Group = "ports" };
            yield return Step("diag.block.addLeft", "Add port left", "Port links hinzufügen", "portsLeft", 1, 1);
            yield return Step("diag.block.removeLeft", "Remove port left", "Port links entfernen", "portsLeft", 1, -1);
            yield return Step("diag.block.addRight", "Add port right", "Port rechts hinzufügen", "portsRight", 1, 1);
            yield return Step("diag.block.removeRight", "Remove port right", "Port rechts entfernen", "portsRight", 1, -1);
        }
    }

    /// <summary>
    /// Container / group frame with a title: notched title tab (UML/SysML frame style) or a full-width title band.
    /// Not filled, so the grouped objects stay visible.
    /// </summary>
    public sealed class ContainerType : DiagBoxType
    {
        public ContainerType() : base("diag.sys.container", new I18nText("Container / group frame", "Container / Gruppenrahmen"), DiagCategories.System,
            "container", "group", "gruppe", "frame", "rahmen", "boundary", "grenze", "system boundary", "systemgrenze", "zone", "cluster", "vpc", "subsystem") { }

        public override Size2 DefaultSize => new(100, 60);
        protected override I18nText Placeholder => new("Group", "Gruppe");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("title", "Title style", "Titelform", "tab", ("tab", "Notched tab", "Reiter"), ("band", "Full-width band", "Volle Breite")),
            P.Bool("dashed", "Dashed frame", "Gestrichelter Rahmen", true),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var frame = o.Flag("dashed", true) ? D.Dashed(D.OutlineOpen) : D.OutlineOpen;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, frame) with { Tag = DiagTags.Outline } };
            var label = Label(o, ctx);
            var size = D.Fit(TextSizeOf(o), new[] { label }, w * 0.8 - 4, h * 0.25, 1);
            var th = size * D.LineFactor + 1.5;
            if (o.Str("title", "tab") == "band")
            {
                list.Add(new LinePrim(new Vec2(-hw, -hh + th), new Vec2(hw, -hh + th), D.OutlineOpen) { Tag = DiagTags.Divider });
                list.Add(D.Text(new Vec2(0, -hh + th / 2), label, size, DiagTags.Name, TextAnchor.Middle, bold: true));
            }
            else
            {
                var tw = Math.Min(w - 1, D.Width(label, size) + 4);
                var notch = Math.Min(th * 0.5, Math.Max(0, w - tw));
                list.Add(Prim.Polyline(new[] { new Vec2(-hw, -hh + th), new Vec2(-hw + tw, -hh + th), new Vec2(-hw + tw + notch, -hh + th - notch), new Vec2(-hw + tw + notch, -hh) }, D.OutlineOpen) with { Tag = DiagTags.Divider });
                list.Add(D.Text(new Vec2(-hw + 2, -hh + th / 2), label, size, DiagTags.Name, TextAnchor.Start, bold: true));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.container.dashed", new I18nText("Dashed frame", "Gestrichelter Rahmen"), "dashed", true);
            yield return new ContextAction("diag.container.band", new I18nText("Full-width title band", "Titel über volle Breite"), IconIds.Toggle,
                x => x.WithParam("title", x.Str("title", "tab") == "band" ? "tab" : "band")) { IsActive = o.Str("title", "tab") == "band" };
        }
    }

    /// <summary>Cloud (internet, external network, cloud provider) with centred text.</summary>
    public sealed class CloudType : DiagBoxType
    {
        public CloudType() : base("diag.sys.cloud", new I18nText("Cloud", "Wolke"), DiagCategories.System,
            "cloud", "wolke", "internet", "network", "netzwerk", "external", "extern", "saas", "architecture", "architektur") { }

        public override Size2 DefaultSize => new(40, 24);
        protected override I18nText Placeholder => new("Internet", "Internet");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.EllipsePorts(new Size2(o.Size.W * 0.86, o.Size.H * 0.82));

        /// <summary>Cloud outline: bumps (cubic arcs) around an ellipse; all control points stay inside the box.</summary>
        public static PathPrim Outline(double w, double h, Style style)
        {
            double rx = w * 0.38, ry = h * 0.36;
            const int bumps = 9;
            const double bulge = 1.3;
            var segs = new List<PathSeg>();
            Vec2 E(double a, double k) => new(rx * k * Math.Cos(a), ry * k * Math.Sin(a));
            for (var i = 0; i < bumps; i++)
            {
                var a0 = -Math.PI / 2 + 2 * Math.PI * i / bumps;
                var a1 = a0 + 2 * Math.PI / bumps;
                var span = a1 - a0;
                if (i == 0) segs.Add(PathSeg.Move(E(a0, 1)));
                segs.Add(PathSeg.Cubic(E(a0 + span * 0.05, bulge), E(a1 - span * 0.05, bulge), E(a1, 1)));
            }
            segs.Add(PathSeg.Close);
            return new PathPrim(segs, style);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var list = new List<Primitive> { Outline(o.Size.W, o.Size.H, D.Outline) with { Tag = DiagTags.Outline } };
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), Vec2.Zero, o.Size.W * 0.62, o.Size.H * 0.55);
            return list;
        }
    }

    /// <summary>Database cylinder with centred text.</summary>
    public sealed class DatabaseType : DiagBoxType
    {
        public DatabaseType() : base("diag.sys.database", new I18nText("Database", "Datenbank"), DiagCategories.System,
            "database", "datenbank", "db", "storage", "speicher", "cylinder", "zylinder", "sql", "data store", "datenspeicher") { }

        public override Size2 DefaultSize => new(22, 26);
        protected override I18nText Placeholder => new("Database", "Datenbank");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var ry = Math.Min(h * 0.14, w * 0.25);
            const double k = 0.5522847498307936;
            double top = -hh + ry, bot = hh - ry;
            var body = new PathPrim(new[]
            {
                PathSeg.Move(new Vec2(-hw, top)), PathSeg.Line(new Vec2(-hw, bot)),
                PathSeg.Cubic(new Vec2(-hw, bot + ry * k), new Vec2(-hw * k, hh), new Vec2(0, hh)),
                PathSeg.Cubic(new Vec2(hw * k, hh), new Vec2(hw, bot + ry * k), new Vec2(hw, bot)),
                PathSeg.Line(new Vec2(hw, top)),
                PathSeg.Cubic(new Vec2(hw, top - ry * k), new Vec2(hw * k, -hh), new Vec2(0, -hh)),
                PathSeg.Cubic(new Vec2(-hw * k, -hh), new Vec2(-hw, top - ry * k), new Vec2(-hw, top)),
                PathSeg.Close,
            }, D.Outline) { Tag = DiagTags.Outline };
            var list = new List<Primitive>
            {
                body,
                new PathPrim(new[]
                {
                    PathSeg.Move(new Vec2(-hw, top)),
                    PathSeg.Cubic(new Vec2(-hw, top + ry * k), new Vec2(-hw * k, top + ry), new Vec2(0, top + ry)),
                    PathSeg.Cubic(new Vec2(hw * k, top + ry), new Vec2(hw, top + ry * k), new Vec2(hw, top)),
                }, D.OutlineOpen) { Tag = DiagTags.Marker },
            };
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2(0, ry / 2 + ry * 0.2), w - 2, h - 3 * ry);
            return list;
        }
    }

    /// <summary>Message queue: horizontal cylinder with message slots near the inlet and centred text.</summary>
    public sealed class QueueType : DiagBoxType
    {
        public QueueType() : base("diag.sys.queue", new I18nText("Queue", "Warteschlange"), DiagCategories.System,
            "queue", "warteschlange", "message queue", "nachrichtenwarteschlange", "broker", "topic", "kafka", "mq", "fifo", "event bus") { }

        public override Size2 DefaultSize => new(40, 14);
        protected override I18nText Placeholder => new("Queue", "Warteschlange");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var rx = Math.Min(w * 0.12, h * 0.3);
            const double k = 0.5522847498307936;
            double left = -hw + rx, right = hw - rx;
            var body = new PathPrim(new[]
            {
                PathSeg.Move(new Vec2(left, -hh)), PathSeg.Line(new Vec2(right, -hh)),
                PathSeg.Cubic(new Vec2(right + rx * k, -hh), new Vec2(hw, -hh * k), new Vec2(hw, 0)),
                PathSeg.Cubic(new Vec2(hw, hh * k), new Vec2(right + rx * k, hh), new Vec2(right, hh)),
                PathSeg.Line(new Vec2(left, hh)),
                PathSeg.Cubic(new Vec2(left - rx * k, hh), new Vec2(-hw, hh * k), new Vec2(-hw, 0)),
                PathSeg.Cubic(new Vec2(-hw, -hh * k), new Vec2(left - rx * k, -hh), new Vec2(left, -hh)),
                PathSeg.Close,
            }, D.Outline) { Tag = DiagTags.Outline };
            var list = new List<Primitive> { body };
            // Visible end cap on the right (outlet) and three slot arcs near the left (inlet).
            PathPrim Arc(double cx, Style s) => new(new[]
            {
                PathSeg.Move(new Vec2(cx, -hh)),
                PathSeg.Cubic(new Vec2(cx - rx * k, -hh), new Vec2(cx - rx, -hh * k), new Vec2(cx - rx, 0)),
                PathSeg.Cubic(new Vec2(cx - rx, hh * k), new Vec2(cx - rx * k, hh), new Vec2(cx, hh)),
            }, s) { Tag = DiagTags.Marker };
            list.Add(Arc(right, D.OutlineOpen));
            var slot = Math.Min(2.5, w * 0.06);
            for (var i = 1; i <= 3; i++) list.Add(Arc(left + rx + i * slot, D.Thin));
            var textLeft = left + rx + 3 * slot + 1;
            var textRight = right - rx - 0.5;
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2((textLeft + textRight) / 2, 0), Math.Max(1, textRight - textLeft), h - 2);
            return list;
        }
    }

    /// <summary>Server: stacked rack units with the name below.</summary>
    public sealed class ServerType : DiagBoxType
    {
        public ServerType() : base("diag.sys.server", new I18nText("Server", "Server"), DiagCategories.System,
            "server", "host", "rack", "backend", "machine", "maschine", "rechner", "computer", "infrastructure", "infrastruktur") { }

        public override Size2 DefaultSize => new(20, 26);
        protected override I18nText Placeholder => new("Server", "Server");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Int("units", "Rack units", "Höheneinheiten", 3, 1, 8), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive>();
            var label = Label(o, ctx);
            var lines = D.Lines(label);
            var size = D.Fit(TextSizeOf(o), lines, w, h * 0.3, Math.Max(1, lines.Count));
            var labelH = lines.Count * size * D.LineFactor;
            var iconH = h - labelH - (lines.Count > 0 ? 1 : 0);
            var units = Math.Max(1, Math.Min(8, o.Int("units", 3)));
            var iw = Math.Min(w, iconH * 1.1);
            var uh = iconH / units;
            for (var i = 0; i < units; i++)
            {
                var y = -hh + i * uh;
                list.Add(D.RoundedRect(-iw / 2, y, iw, uh, Math.Min(1, uh * 0.2), D.Outline) with { Tag = DiagTags.Outline });
                var cy = y + uh / 2;
                var r = Math.Min(uh * 0.15, 0.9);
                list.Add(Prim.Circle(-iw / 2 + uh * 0.35 + r, cy, r, D.Solid with { StrokeWidth = 0.18 }) with { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(-iw / 2 + iw * 0.4, cy), new Vec2(iw / 2 - uh * 0.35, cy), D.Thin) { Tag = DiagTags.Marker });
            }
            D.CenterText(list, label, size, new Vec2(0, hh - labelH / 2), w, labelH);
            return list;
        }
    }

    /// <summary>Client / browser window: title bar with window dots and address bar; name in the content area.</summary>
    public sealed class ClientType : DiagBoxType
    {
        public ClientType() : base("diag.sys.client", new I18nText("Client / browser", "Client / Browser"), DiagCategories.System,
            "client", "browser", "web", "frontend", "window", "fenster", "app", "ui", "user interface", "benutzeroberfläche") { }

        public override Size2 DefaultSize => new(36, 24);
        protected override I18nText Placeholder => new("Web client", "Web-Client");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var bar = Math.Min(5, h * 0.22);
            var list = new List<Primitive>
            {
                D.RoundedRect(-hw, -hh, w, h, Math.Min(1.5, Math.Min(w, h) * 0.1), D.Outline) with { Tag = DiagTags.Outline },
                new LinePrim(new Vec2(-hw, -hh + bar), new Vec2(hw, -hh + bar), D.OutlineOpen) { Tag = DiagTags.Divider },
            };
            var r = Math.Min(bar * 0.16, 0.8);
            for (var i = 0; i < 3; i++)
                list.Add(Prim.Circle(-hw + bar * 0.45 + i * r * 3, -hh + bar / 2, r, D.Thin) with { Tag = DiagTags.Marker });
            var ax = -hw + bar * 0.45 + 9 * r;
            if (hw - 1.5 - ax > 2)
                list.Add(D.RoundedRect(ax, -hh + bar * 0.25, hw - 1.5 - ax, bar * 0.5, bar * 0.25, D.Thin) with { Tag = DiagTags.Marker });
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2(0, bar / 2), w - 3, h - bar - 2);
            return list;
        }
    }

    /// <summary>User / person icon (head and shoulders) with the name below.</summary>
    public sealed class UserType : DiagBoxType
    {
        public UserType() : base("diag.sys.user", new I18nText("User (person)", "Benutzer (Person)"), DiagCategories.System,
            "user", "benutzer", "person", "nutzer", "customer", "kunde", "operator", "bediener", "human", "mensch") { }

        public override Size2 DefaultSize => new(16, 20);
        protected override I18nText Placeholder => new("User", "Benutzer");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hh = h / 2;
            var label = Label(o, ctx);
            var lines = D.Lines(label);
            var size = D.Fit(TextSizeOf(o), lines, w, h * 0.3, Math.Max(1, lines.Count));
            var labelH = lines.Count * size * D.LineFactor;
            var avail = h - labelH - (lines.Count > 0 ? 0.8 : 0);
            var iw = Math.Min(w, avail * 0.95);
            var ih = iw / 0.95;
            var r = ih * 0.24;
            var top = -hh + (avail - ih) / 2;
            const double k = 0.5522847498307936;
            var sy = top + ih * 0.58;
            var bw = iw / 2;
            var shoulders = new PathPrim(new[]
            {
                PathSeg.Move(new Vec2(-bw, top + ih)),
                PathSeg.Line(new Vec2(-bw, sy + (ih - (sy - top)) * 0.45)),
                PathSeg.Cubic(new Vec2(-bw, sy + (ih - (sy - top)) * 0.45 * (1 - k)), new Vec2(-bw * k, sy), new Vec2(0, sy)),
                PathSeg.Cubic(new Vec2(bw * k, sy), new Vec2(bw, sy + (ih - (sy - top)) * 0.45 * (1 - k)), new Vec2(bw, sy + (ih - (sy - top)) * 0.45)),
                PathSeg.Line(new Vec2(bw, top + ih)),
                PathSeg.Close,
            }, D.Outline) { Tag = DiagTags.Outline };
            var list = new List<Primitive> { shoulders, Prim.Circle(0, top + r + ih * 0.04, r, D.Outline) with { Tag = DiagTags.Outline } };
            D.CenterText(list, label, size, new Vec2(0, hh - labelH / 2), w, labelH);
            return list;
        }
    }

    /// <summary>
    /// Interface lollipop: a line from the left edge (the component side) to a ball (provided interface) or a socket
    /// (required interface); the interface name is written above.
    /// </summary>
    public sealed class LollipopType : DiagBoxType
    {
        public LollipopType() : base("diag.sys.lollipop", new I18nText("Interface lollipop", "Schnittstellen-Lolli"), DiagCategories.System,
            "uml", "interface", "schnittstelle", "lollipop", "lolli", "ball", "socket", "provided", "required", "bereitgestellt", "benötigt", "port") { }

        public override Size2 DefaultSize => new(22, 12);
        protected override I18nText Placeholder => new("IService", "IService");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "provided", ("provided", "Provided (ball)", "Bereitgestellt (Kugel)"), ("required", "Required (socket)", "Benötigt (Buchse)")),
            P.TextSize(),
        };

        private static (Vec2 C, double R, double LabelH, double Size) Layout(SketchObject o, string label)
        {
            double w = o.Size.W, h = o.Size.H;
            var size = D.Fit(Math.Min(o.Num("textSize", D.TextSize), 3.2), new[] { label }, w, h * 0.45, 1);
            var labelH = label.Length > 0 ? size * D.LineFactor : 0;
            var r = Math.Max(0.5, Math.Min((h - labelH) / 2 - 0.2, Math.Min(w * 0.2, 2.6)));
            return (new Vec2(w / 2 - r - 0.2, -h / 2 + labelH + (h - labelH) / 2), r, labelH, size);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var label = Label(o, ctx);
            var (c, r, labelH, size) = Layout(o, label);
            var required = o.Str("kind", "provided") == "required";
            var list = new List<Primitive>
            {
                new LinePrim(new Vec2(-o.Size.W / 2, c.Y), new Vec2(c.X - r, c.Y), D.OutlineOpen) { Tag = DiagTags.Main },
                required
                    ? Prim.Arc(c.X, c.Y, r, r, Math.PI / 2, Math.PI, D.OutlineOpen) with { Tag = DiagTags.Outline }
                    : Prim.Circle(c.X, c.Y, r, D.Outline) with { Tag = DiagTags.Outline },
            };
            if (label.Length > 0)
            {
                var x = Math.Min(c.X, o.Size.W / 2 - D.Width(label, size) / 2);
                list.Add(D.Text(new Vec2(x, -o.Size.H / 2 + labelH / 2), label, size, DiagTags.Name));
            }
            return list;
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            var (c, r, _, _) = Layout(o, o.Text ?? "IService");
            yield return ("in", new Vec2(-o.Size.W / 2, c.Y));
            yield return ("ball", new Vec2(c.X + r, c.Y));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.lollipop.required", new I18nText("Required (socket)", "Benötigt (Buchse)"), IconIds.Toggle,
                x => x.WithParam("kind", x.Str("kind", "provided") == "required" ? "provided" : "required"))
            { IsActive = o.Str("kind", "provided") == "required" };
        }
    }
}
