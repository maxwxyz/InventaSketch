using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// UML state machine pseudo states (drawn independently after the UML conventions): shallow history (H), deep history
    /// (H*), junction (small filled dot), choice (diamond), entry point (open circle on a state's border), exit point
    /// (circle with a cross) and terminate (a cross). Initial and final states reuse the activity control nodes, fork and
    /// join the synchronisation bar. The object text (a guard or a name) stands below.
    /// </summary>
    public sealed class PseudoStateType : DiagBoxType
    {
        public const string TypeId = "diag.state.pseudo";

        public static readonly (string Value, string En, string De)[] Kinds =
        {
            ("history", "Shallow history (H)", "Flache Historie (H)"), ("deepHistory", "Deep history (H*)", "Tiefe Historie (H*)"),
            ("junction", "Junction", "Kreuzung (Junction)"), ("choice", "Choice", "Auswahl (Choice)"),
            ("entry", "Entry point", "Eintrittspunkt"), ("exit", "Exit point", "Austrittspunkt"), ("terminate", "Terminate", "Terminierung"),
        };

        public PseudoStateType() : base(TypeId, new I18nText("Pseudo state (UML)", "Pseudozustand (UML)"), DiagCategories.State,
            "uml", "state", "zustand", "state machine", "zustandsautomat", "pseudo state", "pseudozustand", "history", "historie", "deep history",
            "tiefe historie", "h*", "junction", "kreuzung", "choice", "auswahl", "entry point", "eintrittspunkt", "exit point", "austrittspunkt",
            "terminate", "terminierung") { }

        public override Size2 DefaultSize => new(7, 7);
        public override bool PreferUniformScale => true;

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Choice("kind", "Kind", "Art", "history", Kinds), P.TextSize() };

        private static string KindOf(SketchObject o) => o.Str("kind", "history");

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => KindOf(o) == "choice" ? D.DiamondPorts(o.Size) : D.EllipsePorts(o.Size);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2, r = Math.Min(rx, ry);
            var list = new List<Primitive>();
            var cross = Style.Visible with { StrokeWidth = Math.Max(D.Stroke, r * 0.12) };
            void Cross(double k)
            {
                list.Add(new LinePrim(new Vec2(-rx * k, -ry * k), new Vec2(rx * k, ry * k), cross) { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(-rx * k, ry * k), new Vec2(rx * k, -ry * k), cross) { Tag = DiagTags.Marker });
            }
            switch (KindOf(o))
            {
                case "deepHistory":
                case "history":
                    list.Add(Prim.Ellipse(0, 0, rx, ry, D.Outline) with { Tag = DiagTags.Outline });
                    list.Add(D.Text(Vec2.Zero, KindOf(o) == "history" ? "H" : "H*", Math.Max(D.MinTextSize, r * 1.1), DiagTags.Marker, bold: true));
                    break;
                case "junction":
                    list.Add(Prim.Ellipse(0, 0, rx * 0.55, ry * 0.55, D.Solid) with { Tag = DiagTags.Outline });
                    break;
                case "choice":
                    list.Add(D.Diamond(rx, ry, D.Outline) with { Tag = DiagTags.Outline });
                    break;
                case "entry":
                    list.Add(Prim.Ellipse(0, 0, rx, ry, D.Outline) with { Tag = DiagTags.Outline });
                    break;
                case "exit":
                    list.Add(Prim.Ellipse(0, 0, rx, ry, D.Outline) with { Tag = DiagTags.Outline });
                    Cross(Math.Sqrt(0.5));
                    break;
                case "terminate":
                    Cross(0.8);
                    break;
            }
            list.AddRange(BpmnEventType.BelowLabel(o.Text, ry, TextSizeOf(o)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "diag.state.pseudo.kind", Parameters.First(p => p.Key == "kind"), null, "kind", null)) yield return a;
        }
    }
}
