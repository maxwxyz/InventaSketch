using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>Kinds of UML classifier boxes.</summary>
    public enum ClassKind { Class, Interface, Enum }

    /// <summary>
    /// UML classifier box (class, interface, enumeration) with name, attribute and operation compartments. Compartment
    /// contents are multi-line text parameters; each line is one member, a leading "_" marks a static (underlined) member.
    /// Visibility markers (+ - # ~) are written as part of the line.
    /// </summary>
    public sealed class ClassType : DiagBoxType
    {
        private readonly ClassKind _kind;

        public ClassType(ClassKind kind) : base(IdOf(kind), NameOf(kind), DiagCategories.Structure, KeysOf(kind))
        {
            _kind = kind;
        }

        private static string IdOf(ClassKind k) => k switch { ClassKind.Interface => "diag.uml.interface", ClassKind.Enum => "diag.uml.enum", _ => "diag.uml.class" };

        private static I18nText NameOf(ClassKind k) => k switch
        {
            ClassKind.Interface => new I18nText("Interface (UML)", "Schnittstelle (UML)"),
            ClassKind.Enum => new I18nText("Enumeration (UML)", "Aufzählung (UML)"),
            _ => new I18nText("Class (UML)", "Klasse (UML)"),
        };

        private static string[] KeysOf(ClassKind k) => new[] { "uml", "class diagram", "klassendiagramm", "classifier" }.Concat(k switch
        {
            ClassKind.Interface => new[] { "interface", "schnittstelle", "contract" },
            ClassKind.Enum => new[] { "enum", "enumeration", "aufzählung", "literal" },
            _ => new[] { "class", "klasse", "attribute", "attribut", "operation", "methode", "method" },
        }).ToArray();

        public ClassKind Kind => _kind;

        public override Size2 DefaultSize => _kind switch { ClassKind.Interface => new Size2(44, 22), ClassKind.Enum => new Size2(34, 28), _ => new Size2(44, 32) };

        public override IReadOnlyList<ViewDef> Views => new[]
        {
            new ViewDef("full", new I18nText("Full (compartments)", "Vollständig (Abschnitte)")) { Icon = IconIds.Detailed },
            new ViewDef("compact", new I18nText("Compact (name only)", "Kompakt (nur Name)")) { Icon = IconIds.Simplified },
        };

        protected override I18nText Placeholder => _kind switch
        {
            ClassKind.Interface => new I18nText("Interface", "Schnittstelle"),
            ClassKind.Enum => new I18nText("Enumeration", "Aufzählung"),
            _ => new I18nText("Class", "Klasse"),
        };

        private string DefaultAttributes => _kind switch { ClassKind.Interface => "", ClassKind.Enum => "RED\nGREEN\nBLUE", _ => "- id: int\n- name: String" };
        private string DefaultOperations => _kind switch { ClassKind.Interface => "+ execute(): void", ClassKind.Enum => "", _ => "+ getName(): String" };
        private bool DefaultShowAttributes => _kind != ClassKind.Interface;
        private bool DefaultShowOperations => _kind != ClassKind.Enum;
        private string? DefaultStereotype => _kind switch { ClassKind.Interface => "interface", ClassKind.Enum => "enumeration", _ => null };

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>
                {
                    P.Stereotype(DefaultStereotype),
                    _kind == ClassKind.Enum
                        ? P.Text("attributes", "Literals (one per line)", "Literale (eine pro Zeile)", DefaultAttributes)
                        : P.Text("attributes", "Attributes (one per line)", "Attribute (eins pro Zeile)", DefaultAttributes),
                    P.Text("operations", "Operations (one per line)", "Operationen (eine pro Zeile)", DefaultOperations),
                    P.Bool("showAttributes", "Show attributes", "Attribute anzeigen", DefaultShowAttributes),
                    P.Bool("showOperations", "Show operations", "Operationen anzeigen", DefaultShowOperations),
                };
                if (_kind == ClassKind.Class) list.Add(P.Bool("abstract", "Abstract (italic name)", "Abstrakt (kursiver Name)", false));
                list.Add(P.TextSize());
                return list;
            }
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            var sections = new List<IReadOnlyList<DiagRow>>
            {
                D.Header(o.Str("stereotype", DefaultStereotype ?? ""), Label(o, ctx), o.Flag("abstract", false)),
            };
            if (ViewOf(o) != "compact")
            {
                if (o.Flag("showAttributes", DefaultShowAttributes)) sections.Add(D.Lines(o.Str("attributes", DefaultAttributes)).Select(D.MemberRow).ToList());
                if (o.Flag("showOperations", DefaultShowOperations)) sections.Add(D.Lines(o.Str("operations", DefaultOperations)).Select(D.MemberRow).ToList());
            }
            list.AddRange(D.Compartments(w, h, sections, TextSizeOf(o)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var attr = _kind == ClassKind.Enum ? new I18nText("literal", "Literal") : new I18nText("attribute", "Attribut");
            yield return D.Toggle(o, "diag.class.attributes", _kind == ClassKind.Enum ? new I18nText("Show literals", "Literale anzeigen") : new I18nText("Show attributes", "Attribute anzeigen"),
                "showAttributes", DefaultShowAttributes, "compartments");
            yield return D.Toggle(o, "diag.class.operations", new I18nText("Show operations", "Operationen anzeigen"), "showOperations", DefaultShowOperations, "compartments");
            yield return D.AddRow("diag.class.addAttribute", new I18nText("Add " + attr.En, attr.De + " hinzufügen"), "attributes", DefaultAttributes,
                _kind == ClassKind.Enum ? "VALUE" : "- attribute: Type");
            yield return D.RemoveRow("diag.class.removeAttribute", new I18nText("Remove last " + attr.En, "Letztes " + attr.De + " entfernen"), "attributes", DefaultAttributes);
            yield return D.AddRow("diag.class.addOperation", new I18nText("Add operation", "Operation hinzufügen"), "operations", DefaultOperations, "+ operation(): void");
            yield return D.RemoveRow("diag.class.removeOperation", new I18nText("Remove last operation", "Letzte Operation entfernen"), "operations", DefaultOperations);
            if (_kind == ClassKind.Class)
                yield return D.Toggle(o, "diag.class.abstract", new I18nText("Abstract", "Abstrakt"), "abstract", false);
        }
    }

    /// <summary>UML package: folder shape with a tab; name (and optional stereotype) in the body.</summary>
    public sealed class PackageType : DiagBoxType
    {
        public PackageType() : base("diag.uml.package", new I18nText("Package (UML)", "Paket (UML)"), DiagCategories.Structure,
            "uml", "package", "paket", "namespace", "folder", "ordner", "module", "modul") { }

        public override Size2 DefaultSize => new(44, 28);
        protected override I18nText Placeholder => new("Package", "Paket");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Stereotype(), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var tabH = Math.Min(5, h * 0.2);
            var tabW = Math.Min(w * 0.4, 22);
            var list = new List<Primitive>
            {
                Prim.Rect(-hw, -hh, tabW, tabH, D.Outline) with { Tag = DiagTags.Outline },
                Prim.Rect(-hw, -hh + tabH, w, h - tabH, D.Outline) with { Tag = DiagTags.Outline },
            };
            var rows = D.Header(o.Str("stereotype", ""), Label(o, ctx));
            list.AddRange(D.Compartments(w, h - tabH, new[] { rows }, TextSizeOf(o)).Select(p => Prim.Transform(p, Matrix2D.Translation(0, tabH / 2))));
            return list;
        }
    }

    /// <summary>UML component: box with the component icon in the upper right corner.</summary>
    public sealed class ComponentType : DiagBoxType
    {
        public ComponentType() : base("diag.uml.component", new I18nText("Component (UML)", "Komponente (UML)"), DiagCategories.Structure,
            "uml", "component", "komponente", "module", "modul", "service", "dienst", "architecture", "architektur") { }

        public override Size2 DefaultSize => new(44, 22);
        protected override I18nText Placeholder => new("Component", "Komponente");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Stereotype("component"), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            // Component icon: body with two small tabs on its left side.
            var iw = Math.Min(5, Math.Min(w * 0.15, h * 0.3));
            var ih = iw * 1.2;
            var ix = hw - iw - Math.Min(2, w * 0.05);
            var iy = -hh + Math.Min(2, h * 0.1);
            var thin = D.Thin with { Fill = Fill.Solid(D.Paper) };
            list.Add(Prim.Rect(ix, iy, iw, ih, thin) with { Tag = DiagTags.Marker });
            list.Add(Prim.Rect(ix - iw * 0.25, iy + ih * 0.18, iw * 0.5, ih * 0.2, thin) with { Tag = DiagTags.Marker });
            list.Add(Prim.Rect(ix - iw * 0.25, iy + ih * 0.58, iw * 0.5, ih * 0.2, thin) with { Tag = DiagTags.Marker });
            var rows = D.Header(o.Str("stereotype", "component"), Label(o, ctx));
            var inset = iw * 1.5 + 2;
            list.AddRange(D.Compartments(w - 2 * inset, h, new[] { rows }, TextSizeOf(o)));
            return list;
        }
    }

    /// <summary>UML deployment node: three-dimensional box (device, execution environment).</summary>
    public sealed class NodeType : DiagBoxType
    {
        public NodeType() : base("diag.uml.node", new I18nText("Node / deployment (UML)", "Knoten / Verteilung (UML)"), DiagCategories.Structure,
            "uml", "node", "knoten", "deployment", "verteilung", "device", "gerät", "server", "execution environment", "laufzeitumgebung") { }

        public override Size2 DefaultSize => new(44, 26);
        protected override I18nText Placeholder => new("Node", "Knoten");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Stereotype("device"), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var d = Math.Min(4.5, Math.Min(w * 0.15, h * 0.2));
            var list = new List<Primitive>
            {
                Prim.Polygon(new[] { new Vec2(-hw, -hh + d), new Vec2(-hw + d, -hh), new Vec2(hw, -hh), new Vec2(hw - d, -hh + d) }, D.Outline) with { Tag = DiagTags.Outline },
                Prim.Polygon(new[] { new Vec2(hw - d, -hh + d), new Vec2(hw, -hh), new Vec2(hw, hh - d), new Vec2(hw - d, hh) }, D.Outline) with { Tag = DiagTags.Outline },
                Prim.Rect(-hw, -hh + d, w - d, h - d, D.Outline) with { Tag = DiagTags.Outline },
            };
            var rows = D.Header(o.Str("stereotype", "device"), Label(o, ctx));
            list.AddRange(D.Compartments(w - d, h - d, new[] { rows }, TextSizeOf(o)).Select(p => Prim.Transform(p, Matrix2D.Translation(-d / 2, d / 2))));
            return list;
        }
    }

    /// <summary>UML actor: stick figure with the name below.</summary>
    public sealed class ActorType : DiagBoxType
    {
        public ActorType() : base("diag.uml.actor", new I18nText("Actor (UML)", "Akteur (UML)"), DiagCategories.UseCase,
            "uml", "actor", "akteur", "use case", "anwendungsfall", "user", "benutzer", "stick figure", "strichmännchen", "person") { }

        public override Size2 DefaultSize => new(16, 26);
        protected override I18nText Placeholder => new("Actor", "Akteur");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hh = h / 2;
            var label = Label(o, ctx);
            var lines = D.Lines(label);
            var size = D.Fit(TextSizeOf(o), lines, w, h * 0.3, Math.Max(1, lines.Count));
            var labelH = lines.Count * size * D.LineFactor;
            var figH = h - labelH - (lines.Count > 0 ? 0.6 : 0);
            var list = new List<Primitive>(D.StickFigure(new Vec2(0, -hh + figH / 2), w, figH, Style.Visible with { StrokeWidth = D.Stroke }));
            D.CenterText(list, label, size, new Vec2(0, hh - labelH / 2), w, labelH);
            return list;
        }
    }

    /// <summary>UML use case: ellipse with the centred name.</summary>
    public sealed class UseCaseType : DiagBoxType
    {
        public UseCaseType() : base("diag.uml.usecase", new I18nText("Use case (UML)", "Anwendungsfall (UML)"), DiagCategories.UseCase,
            "uml", "use case", "usecase", "anwendungsfall", "ellipse", "oval") { }

        public override Size2 DefaultSize => new(40, 18);
        protected override I18nText Placeholder => new("Use case", "Anwendungsfall");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.EllipsePorts(o.Size);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { Prim.Ellipse(0, 0, w / 2, h / 2, D.Outline) with { Tag = DiagTags.Outline } };
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), Vec2.Zero, w * 0.7, h * 0.7);
            return list;
        }
    }

    /// <summary>UML note (comment) with a folded corner; multi-line text, left-aligned.</summary>
    public sealed class NoteType : DiagBoxType
    {
        public NoteType() : base("diag.uml.note", new I18nText("Note (UML)", "Notiz (UML)"), DiagCategories.Structure,
            "uml", "note", "notiz", "comment", "kommentar", "annotation", "anmerkung", "constraint") { }

        public override Size2 DefaultSize => new(36, 16);
        protected override I18nText Placeholder => new("Note", "Notiz");
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var f = Math.Min(4, Math.Min(w, h) * 0.3);
            var list = new List<Primitive>
            {
                Prim.Polygon(new[] { new Vec2(-hw, -hh), new Vec2(hw - f, -hh), new Vec2(hw, -hh + f), new Vec2(hw, hh), new Vec2(-hw, hh) }, D.Outline) with { Tag = DiagTags.Outline },
                Prim.Polyline(new[] { new Vec2(hw - f, -hh), new Vec2(hw - f, -hh + f), new Vec2(hw, -hh + f) }, D.Thin) with { Tag = DiagTags.Marker },
            };
            var lines = D.Lines(Label(o, ctx));
            if (lines.Count > 0)
            {
                const double pad = 1.5;
                var size = D.Fit(TextSizeOf(o), lines, w - 2 * pad - f * 0.5, h - 2 * pad, lines.Count);
                var rowH = size * D.LineFactor;
                for (var i = 0; i < lines.Count; i++)
                    list.Add(D.Text(new Vec2(-hw + pad, -hh + pad + (i + 0.5) * rowH), lines[i], size, DiagTags.Row, TextAnchor.Start));
            }
            return list;
        }
    }
}
