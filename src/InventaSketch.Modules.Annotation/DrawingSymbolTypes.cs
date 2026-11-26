using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Pictograms of the geometrical characteristics used in feature control frames (symbol family of ISO 1101, drawn
    /// independently from simple lines, circles and arcs).
    /// </summary>
    public static class GdtSymbols
    {
        /// <summary>The characteristics in their usual order: form, profile, orientation, location, run-out.</summary>
        public static readonly (string Value, I18nText Label)[] Characteristics =
        {
            ("straightness", new I18nText("Straightness", "Geradheit")),
            ("flatness", new I18nText("Flatness", "Ebenheit")),
            ("circularity", new I18nText("Circularity (roundness)", "Rundheit")),
            ("cylindricity", new I18nText("Cylindricity", "Zylinderform")),
            ("profileLine", new I18nText("Profile of a line", "Linienform")),
            ("profileSurface", new I18nText("Profile of a surface", "Flächenform")),
            ("parallelism", new I18nText("Parallelism", "Parallelität")),
            ("perpendicularity", new I18nText("Perpendicularity", "Rechtwinkligkeit")),
            ("angularity", new I18nText("Angularity", "Neigung")),
            ("position", new I18nText("Position", "Position")),
            ("concentricity", new I18nText("Concentricity / coaxiality", "Konzentrizität / Koaxialität")),
            ("symmetry", new I18nText("Symmetry", "Symmetrie")),
            ("circularRunout", new I18nText("Circular run-out", "Lauf")),
            ("totalRunout", new I18nText("Total run-out", "Gesamtlauf")),
        };

        /// <summary>Form tolerances never refer to a datum.</summary>
        public static bool IsForm(string c) => c is "straightness" or "flatness" or "circularity" or "cylindricity";

        /// <summary>Characteristics whose tolerance zone is usually cylindrical (diameter sign before the value).</summary>
        public static bool DiameterByDefault(string c) => c is "position" or "concentricity";

        /// <summary>
        /// The pictogram of characteristic <paramref name="c"/> centred at <paramref name="at"/> in a square of
        /// <paramref name="k"/> mm. Unknown values give no primitives.
        /// </summary>
        public static List<Primitive> Draw(string c, Vec2 at, double k, double width = 0.3)
        {
            var s = Style.ThinLine with { StrokeWidth = width };
            Vec2 P(double x, double y) => at + new Vec2(x * k, y * k);
            var list = new List<Primitive>();
            void Line(double x1, double y1, double x2, double y2) => list.Add(new LinePrim(P(x1, y1), P(x2, y2), s));
            void Circle(double r) => list.Add(Prim.Circle(at.X, at.Y, r * k, s));
            void Arrow(double x1, double y1, double x2, double y2)
            {
                var a = P(x1, y1); var b = P(x2, y2);
                list.Add(new LinePrim(a, b, s));
                var d = (b - a).Normalized(); var n = d.Perp;
                var hl = 0.3 * k; var hw = 0.11 * k;
                list.Add(Prim.Polygon(new[] { b, b - d * hl + n * hw, b - d * hl - n * hw }, s with { Fill = Fill.Solid("#000000") }));
            }
            switch (c)
            {
                case "straightness": Line(-0.5, 0, 0.5, 0); break;
                case "flatness": list.Add(Prim.Polygon(new[] { P(-0.5, 0.22), P(0.22, 0.22), P(0.5, -0.22), P(-0.22, -0.22) }, s)); break;
                case "circularity": Circle(0.42); break;
                case "cylindricity":
                    {
                        Circle(0.3);
                        var d = new Vec2(0.5, -0.866); var n = new Vec2(0.866, 0.5);
                        foreach (var side in new[] { -1.0, 1.0 })
                        {
                            var m = n * (0.3 * side);
                            Line(m.X - d.X * 0.4, m.Y - d.Y * 0.4, m.X + d.X * 0.4, m.Y + d.Y * 0.4);
                        }
                        break;
                    }
                case "profileLine": list.Add(Prim.Arc(at.X, at.Y + 0.2 * k, 0.45 * k, 0.45 * k, Math.PI, Math.PI, s)); break;
                case "profileSurface":
                    list.Add(Prim.Arc(at.X, at.Y + 0.2 * k, 0.45 * k, 0.45 * k, Math.PI, Math.PI, s));
                    Line(-0.45, 0.2, 0.45, 0.2);
                    break;
                case "parallelism": Line(-0.38, 0.45, 0.02, -0.45); Line(-0.02, 0.45, 0.38, -0.45); break;
                case "perpendicularity": Line(0, -0.45, 0, 0.45); Line(-0.45, 0.45, 0.45, 0.45); break;
                case "angularity": list.Add(Prim.Polyline(new[] { P(0.45, -0.4), P(-0.45, 0.42), P(0.45, 0.42) }, s)); break;
                case "position": Circle(0.28); Line(-0.5, 0, 0.5, 0); Line(0, -0.5, 0, 0.5); break;
                case "concentricity": Circle(0.44); Circle(0.2); break;
                case "symmetry": Line(-0.28, -0.28, 0.28, -0.28); Line(-0.5, 0, 0.5, 0); Line(-0.28, 0.28, 0.28, 0.28); break;
                case "circularRunout": Arrow(-0.3, 0.45, 0.3, -0.45); break;
                case "totalRunout": Arrow(-0.48, 0.45, 0.02, -0.45); Arrow(-0.02, 0.45, 0.48, -0.45); Line(-0.48, 0.45, -0.02, 0.45); break;
            }
            return list;
        }
    }

    /// <summary>
    /// Geometrical tolerance indication (<c>ann.gdt.frame</c>): a feature control frame with the characteristic pictogram,
    /// the tolerance value and up to three datum letters, and a leader with an arrow to the toleranced feature. Point 0 is the
    /// arrow tip on the feature, point 1 the end of the frame the leader starts from; the frame extends away from the tip.
    /// Symbol family of ISO 1101, drawn independently.
    /// </summary>
    public sealed class GdtFrameType : AnnPointType
    {
        public GdtFrameType() : base("ann.gdt.frame", new I18nText("Geometric tolerance frame", "Form- und Lagetoleranz (Toleranzrahmen)"), AnnCategories.Dimensions,
            "gd&t", "gdt", "geometric tolerance", "feature control frame", "tolerance", "position", "flatness", "run-out",
            "form- und lagetoleranz", "toleranzrahmen", "toleranz", "ebenheit", "rundlauf", "iso 1101")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(8, -10) };
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("characteristic", "Characteristic", "Eigenschaft", "position", GdtSymbols.Characteristics.Select(x => (x.Value, x.Label.En, x.Label.De!)).ToArray()),
            P.Text("tolerance", "Tolerance value", "Toleranzwert", "0.1"),
            new ParamDef("diameter", new I18nText("Diameter sign (cylindrical zone)", "Durchmesserzeichen (zylindrische Zone)"), ParamKind.Boolean),
            P.Text("datum1", "Primary datum", "Primärbezug", "A"),
            P.Text("datum2", "Secondary datum", "Sekundärbezug", null),
            P.Text("datum3", "Tertiary datum", "Tertiärbezug", null),
            P.TextSize(Dim.TextSize),
        };

        public static string CharacteristicOf(SketchObject o) => o.Str("characteristic", "position");

        public static string ToleranceText(SketchObject o)
        {
            var c = CharacteristicOf(o);
            var value = !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("tolerance", "0.1");
            return (o.Flag("diameter", GdtSymbols.DiameterByDefault(c)) ? "Ø" : "") + value;
        }

        /// <summary>The datum letters shown (none for form tolerances).</summary>
        public static IReadOnlyList<string> Datums(SketchObject o)
        {
            if (GdtSymbols.IsForm(CharacteristicOf(o))) return Array.Empty<string>();
            return new[] { o.Str("datum1", "A"), o.Str("datum2", ""), o.Str("datum3", "") }.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).ToList();
        }

        /// <summary>Frame cells (x offset from the frame's left end, width) and the total width and height.</summary>
        public static (List<(double X, double W)> Cells, double Width, double Height) Layout(SketchObject o, double ts)
        {
            var h = ts * 2;
            var cells = new List<(double, double)>();
            var x = 0.0;
            void Add(double w) { cells.Add((x, w)); x += w; }
            Add(h);
            Add(Math.Max(h, Ann.TextExtent(ToleranceText(o), ts).X + ts * 0.9));
            foreach (var d in Datums(o)) Add(Math.Max(h, Ann.TextExtent(d, ts).X + ts * 0.7));
            return (cells, x, h);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var tip = p[0]; var attach = p[p.Count - 1];
            var ts = TextSizeOf(o, Dim.TextSize);
            var (cells, width, h) = Layout(o, ts);
            var left = attach.X >= tip.X ? attach.X : attach.X - width;
            var top = attach.Y - h / 2;
            var frame = Style.ThinLine with { StrokeWidth = 0.35 };
            var list = new List<Primitive>();
            if (tip.DistanceTo(attach) > 0.5) list.Add(LeaderGeometry.Line(tip, attach, LeaderCurve.Straight, LeaderEnd.Arrow));
            list.Add(Prim.Rect(left, top, width, h, frame with { Fill = Fill.Solid("#ffffff") }) with { Tag = AnnTags.Main });
            for (var i = 1; i < cells.Count; i++) list.Add(Prim.Line(left + cells[i].X, top, left + cells[i].X, top + h, frame));
            var cy = attach.Y;
            list.AddRange(GdtSymbols.Draw(CharacteristicOf(o), new Vec2(left + cells[0].W / 2, cy), h * 0.62).Select(x => x with { Tag = "gdt.symbol" }));
            list.Add(Ann.Text(new Vec2(left + cells[1].X + cells[1].W / 2, cy), ToleranceText(o), ts, "gdt.tolerance"));
            var datums = Datums(o);
            for (var i = 0; i < datums.Count; i++)
                list.Add(Ann.Text(new Vec2(left + cells[i + 2].X + cells[i + 2].W / 2, cy), datums[i], ts, "gdt.datum"));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.gdt.characteristic", new I18nText("Characteristic", "Eigenschaft"), "characteristic", "position", "characteristic",
                GdtSymbols.Characteristics.Select(x => (x.Value, x.Label, (string?)null)).ToList(), v => GdtSymbols.Draw(v, Vec2.Zero, 5)))
                yield return a;
            var c = CharacteristicOf(o);
            yield return Ann.Toggle(o, "ann.gdt.diameter", new I18nText("Diameter sign", "Durchmesserzeichen"), IconIds.Toggle, "diameter", GdtSymbols.DiameterByDefault(c));
            var current = string.Join(" ", new[] { o.Str("datum1", "A"), o.Str("datum2", ""), o.Str("datum3", "") }.Where(d => d.Length > 0));
            yield return ContextAction.WithInput("ann.gdt.datums", new I18nText("Datums", "Bezüge"), IconIds.Value,
                new I18nText("Datum letters (up to three, e.g. A B C)", "Bezugsbuchstaben (bis zu drei, z. B. A B C)"), current, SetDatums) with { IsEnabled = !GdtSymbols.IsForm(c) };
        }

        /// <summary>Sets the datums from text such as "A B C", "A|B" or "A-B, C" (at most three; separators: space, comma, |, ;).</summary>
        public static SketchObject SetDatums(SketchObject o, string text)
        {
            var parts = (text ?? "").Split(new[] { ' ', ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries).Take(3).ToList();
            for (var i = 0; i < 3; i++) o = o.WithParam("datum" + (i + 1), i < parts.Count ? parts[i] : "");
            return o;
        }
    }

    /// <summary>
    /// Datum feature indicator (<c>ann.gdt.datum</c>): a triangle on the feature joined to a square frame with the datum
    /// letter. Point 0 is the middle of the triangle's base on the feature, point 1 the centre of the frame.
    /// </summary>
    public sealed class GdtDatumType : AnnPointType
    {
        public GdtDatumType() : base("ann.gdt.datum", new I18nText("Datum feature", "Bezugselement"), AnnCategories.Dimensions,
            "datum", "datum feature", "datum triangle", "reference", "bezug", "bezugselement", "bezugsdreieck", "bezugsbuchstabe")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(0, -12) };
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("letter", "Datum letter", "Bezugsbuchstabe", "A"),
            P.Bool("filled", "Filled triangle", "Gefülltes Dreieck", true),
            P.TextSize(Dim.TextSize),
        };

        public static string LetterOf(SketchObject o) => !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("letter", "A");

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var foot = p[0]; var centre = p[p.Count - 1];
            var ts = TextSizeOf(o, Dim.TextSize);
            var h = ts * 2;
            var d = centre - foot;
            var u = d.Length < Geo.Eps ? new Vec2(0, -1) : d.Normalized();
            var n = u.Perp;
            var b = h * 0.32;
            var th = b * 1.6 * 0.866;
            var apex = foot + u * th;
            var line = Style.ThinLine with { StrokeWidth = 0.35 };
            var list = new List<Primitive>
            {
                Prim.Polygon(new[] { foot + n * b, foot - n * b, apex }, line with { Fill = Fill.Solid(o.Flag("filled", true) ? "#000000" : "#ffffff") }) with { Tag = "gdt.triangle" },
            };
            // The joining line ends at the frame's edge.
            var edge = h / 2 / Math.Max(Math.Abs(u.X), Math.Abs(u.Y));
            var end = centre - u * edge;
            if ((end - apex).Dot(u) > 0.2) list.Add(new LinePrim(apex, end, line) { Tag = AnnTags.Main });
            list.Add(Prim.CRect(centre.X, centre.Y, h, h, line with { Fill = Fill.Solid("#ffffff") }));
            list.Add(Ann.Text(centre, LetterOf(o), ts, "gdt.datum"));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.gdt.filled", new I18nText("Filled triangle", "Gefülltes Dreieck"), IconIds.Toggle, "filled", true);
        }
    }

    /// <summary>
    /// Surface texture symbol (<c>ann.surface</c>): two legs of unequal length at 60° to the surface, optionally closed by a
    /// bar (material removal required) or with a circle (removal not permitted), with the requirement ("Ra 3.2") under a
    /// line extending the long leg. Symbol family of ISO 1302, drawn independently. The tip touches the surface.
    /// </summary>
    public sealed class SurfaceTextureType : AnnBoxType
    {
        public SurfaceTextureType() : base("ann.surface", new I18nText("Surface texture symbol", "Oberflächensymbol"), AnnCategories.Dimensions,
            "surface", "surface texture", "roughness", "ra", "finish", "machining", "oberfläche", "oberflächenangabe", "rauheit", "rauigkeit", "bearbeitung", "iso 1302")
        { }

        public override Size2 DefaultSize => new(24, 10);
        public override bool HasText => true;
        public override bool IsPart => false;
        protected override bool EdgePorts => false;

        private static readonly (string, string, string)[] ProcessOptions =
        {
            ("any", "Any process", "Beliebiges Verfahren"), ("removal", "Material removal required", "Materialabtrag gefordert"),
            ("noRemoval", "Material removal not permitted", "Materialabtrag unzulässig"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("process", "Manufacturing process", "Fertigungsverfahren", "removal", ProcessOptions),
            P.Text("value", "Requirement", "Anforderung", "Ra 3.2"),
            P.Bool("allAround", "All around", "Ringsum", false),
            P.TextSize(Dim.TextSize),
        };

        public static string RequirementOf(SketchObject o) => o.Text ?? o.Str("value", "Ra 3.2");

        /// <summary>The symbol of height <paramref name="hgt"/> with its tip at <paramref name="tip"/>: legs, bar or circle; returns the top of the long leg.</summary>
        public static Vec2 Symbol(List<Primitive> list, Vec2 tip, double hgt, string process, double width = 0.35)
        {
            var s = Style.ThinLine with { StrokeWidth = width };
            var t30 = Math.Tan(Math.PI / 6);
            var top = tip + new Vec2(hgt * t30, -hgt);
            var shortTop = tip + new Vec2(-hgt / 2 * t30, -hgt / 2);
            list.Add(Prim.Polyline(new[] { shortTop, tip, top }, s) with { Tag = AnnTags.Main });
            if (process == "removal") list.Add(new LinePrim(shortTop, tip + new Vec2(hgt / 2 * t30, -hgt / 2), s) { Tag = "surface.bar" });
            else if (process == "noRemoval") list.Add(Prim.Circle(tip.X, tip.Y - hgt / 3, hgt / 6, s) with { Tag = "surface.circle" });
            return top;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var ts = TextSizeOf(o, Dim.TextSize);
            var tip = new Vec2(-w / 2 + h / 2 * Math.Tan(Math.PI / 6), h / 2);
            var list = new List<Primitive>();
            var top = Symbol(list, tip, h, o.Str("process", "removal"));
            var text = RequirementOf(o);
            var s = Style.ThinLine with { StrokeWidth = 0.35 };
            var right = Math.Max(top.X + 2, w / 2);
            if (!string.IsNullOrEmpty(text))
            {
                right = Math.Max(right, top.X + Ann.TextExtent(text, ts).X + 1.6);
                list.Add(new LinePrim(top, new Vec2(right, top.Y), s) { Tag = "surface.extension" });
                list.Add(Ann.Text(new Vec2(top.X + 0.8, top.Y + ts * 0.75), text, ts, "surface.value", TextAnchor.Start));
            }
            if (o.Flag("allAround", false))
            {
                if (string.IsNullOrEmpty(text)) list.Add(new LinePrim(top, new Vec2(right, top.Y), s) { Tag = "surface.extension" });
                list.Add(Prim.Circle(top.X, top.Y, Math.Max(0.8, h * 0.1), s) with { Tag = "surface.allaround" });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Mech.Mount(new Vec2(-o.Size.W / 2 + o.Size.H / 2 * Math.Tan(Math.PI / 6), o.Size.H / 2), "tip");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.surface.process", new I18nText("Process", "Verfahren"), "process", "removal", "process",
                ProcessOptions.Select(x => (x.Item1, new I18nText(x.Item2, x.Item3), (string?)null)).ToList(), Preview)) yield return a;
            yield return Ann.Toggle(o, "ann.surface.allaround", new I18nText("All around", "Ringsum"), IconIds.Toggle, "allAround", false);
        }

        private static IReadOnlyList<Primitive> Preview(string process)
        {
            var list = new List<Primitive>();
            Symbol(list, new Vec2(-0.8, 2.5), 5, process, 0.3);
            return list;
        }
    }

    /// <summary>
    /// Weld symbol (<c>ann.weld</c>): arrow line to the joint, reference line with a dashed identification line and the
    /// elementary weld symbol (fillet, square butt, V, bevel, plug) on the arrow side, the other side or both sides, with an
    /// optional all-around circle and size/length texts. Point 0 is the arrow tip at the joint, point 1 the start of the
    /// reference line, which runs away from the tip. Symbol family of ISO 2553 (system A idea), drawn independently.
    /// </summary>
    public sealed class WeldSymbolType : AnnPointType
    {
        public WeldSymbolType() : base("ann.weld", new I18nText("Weld symbol", "Schweißsymbol"), AnnCategories.Dimensions,
            "weld", "welding", "fillet", "butt weld", "v weld", "bevel", "plug", "schweißen", "schweißnaht", "kehlnaht", "v-naht", "i-naht", "hv-naht", "lochnaht", "iso 2553")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(10, -10) };
        public override bool HasText => true;

        public static readonly (string Value, I18nText Label)[] Kinds =
        {
            ("fillet", new I18nText("Fillet weld", "Kehlnaht")),
            ("square", new I18nText("Square butt weld", "I-Naht")),
            ("v", new I18nText("V butt weld", "V-Naht")),
            ("bevel", new I18nText("Bevel butt weld", "HV-Naht")),
            ("plug", new I18nText("Plug / slot weld", "Loch-/Schlitznaht")),
        };

        private static readonly (string, string, string)[] SideOptions =
        {
            ("arrow", "Arrow side", "Pfeilseite"), ("other", "Other side", "Gegenseite"), ("both", "Both sides", "Beidseitig"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Weld", "Naht", "fillet", Kinds.Select(k => (k.Value, k.Label.En, k.Label.De!)).ToArray()),
            P.Choice("side", "Side", "Seite", "arrow", SideOptions),
            P.Bool("allAround", "All around", "Ringsum", false),
            P.Text("size", "Size (e.g. a5)", "Nahtmaß (z. B. a5)", null),
            P.Text("length", "Length", "Nahtlänge", null),
            P.Num("refLength", "Reference line length", "Länge der Bezugslinie", 24, 8, 200),
            P.TextSize(Dim.TextSize),
        };

        /// <summary>Gap between the reference line and the dashed identification line (mm).</summary>
        public const double IdentGap = 1.2;

        /// <summary>
        /// The elementary weld symbol of size <paramref name="g"/> centred at <paramref name="x"/>, standing on the line
        /// <paramref name="y"/> and growing towards <paramref name="dir"/> (−1 up, +1 down).
        /// </summary>
        public static List<Primitive> Glyph(string kind, double x, double y, double g, int dir, double width = 0.35)
        {
            var s = Style.ThinLine with { StrokeWidth = width };
            Vec2 P(double dx, double dy) => new(x + dx * g, y + dir * dy * g);
            var list = new List<Primitive>();
            switch (kind)
            {
                case "square": list.Add(new LinePrim(P(-0.2, 0), P(-0.2, 1), s)); list.Add(new LinePrim(P(0.2, 0), P(0.2, 1), s)); break;
                case "v": list.Add(Prim.Polyline(new[] { P(-0.5, 1), P(0, 0), P(0.5, 1) }, s)); break;
                case "bevel": list.Add(new LinePrim(P(-0.25, 0), P(-0.25, 1), s)); list.Add(new LinePrim(P(-0.25, 0), P(0.4, 1), s)); break;
                case "plug": list.Add(Prim.Polyline(new[] { P(-0.5, 0), P(-0.5, 0.6), P(0.5, 0.6), P(0.5, 0) }, s)); break;
                default: list.Add(Prim.Polygon(new[] { P(-0.5, 0), P(-0.5, 1), P(0.5, 0) }, s)); break;
            }
            return list;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var tip = p[0]; var knee = p[p.Count - 1];
            var ts = TextSizeOf(o, Dim.TextSize);
            var dirX = knee.X >= tip.X ? 1 : -1;
            var len = Geo.Clamp(o.Num("refLength", 24), 8, 200);
            var end = knee + new Vec2(dirX * len, 0);
            var side = o.Str("side", "arrow");
            var kind = o.Str("kind", "fillet");
            var line = Style.ThinLine with { StrokeWidth = 0.35 };
            var list = new List<Primitive>();
            if (tip.DistanceTo(knee) > 0.5)
                list.Add(new LinePrim(knee, tip, line with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium }) { Tag = "weld.arrow" });
            list.Add(new LinePrim(knee, end, line) { Tag = AnnTags.Main });
            var identY = knee.Y + IdentGap;
            if (side != "both") list.Add(new LinePrim(new Vec2(knee.X, identY), new Vec2(end.X, identY), line with { Dash = DashStyle.Dash }) { Tag = "weld.ident" });
            var g = ts;
            var gx = knee.X + dirX * Math.Max(g, len * 0.45);
            var glyphs = new List<(double Y, int Dir)>();
            if (side is "arrow" or "both") glyphs.Add((knee.Y, -1));
            if (side == "other") glyphs.Add((identY, 1));
            if (side == "both") glyphs.Add((knee.Y, 1));
            foreach (var (y, dir) in glyphs)
            {
                list.AddRange(Glyph(kind, gx, y, g, dir).Select(x => x with { Tag = "weld.symbol" }));
                var mid = y + dir * g / 2;
                var size = !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("size", "");
                var length = o.Str("length", "");
                if (size.Length > 0) list.Add(Ann.Text(new Vec2(gx - g * 0.5 - 0.8, mid), size, ts, "weld.size", TextAnchor.End));
                if (length.Length > 0) list.Add(Ann.Text(new Vec2(gx + g * 0.5 + 0.8, mid), length, ts, "weld.length", TextAnchor.Start));
            }
            if (o.Flag("allAround", false)) list.Add(Prim.Circle(knee.X, knee.Y, 1.5, line) with { Tag = "weld.allaround" });
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.weld.kind", new I18nText("Weld", "Naht"), "kind", "fillet", "kind",
                Kinds.Select(k => (k.Value, k.Label, (string?)null)).ToList(), v => Glyph(v, 0, 1.5, 4, -1, 0.3).Prepend(new LinePrim(new Vec2(-3, 1.5), new Vec2(3, 1.5), Style.ThinLine with { StrokeWidth = 0.3 })).ToList()))
                yield return a;
            foreach (var a in Ann.Choice(o, "ann.weld.side", new I18nText("Side", "Seite"), "side", "arrow", "side",
                SideOptions.Select(x => (x.Item1, new I18nText(x.Item2, x.Item3), (string?)null)).ToList())) yield return a;
            yield return Ann.Toggle(o, "ann.weld.allaround", new I18nText("All around", "Ringsum"), IconIds.Toggle, "allAround", false);
        }
    }

    /// <summary>Revision cloud (<c>ann.cloud</c>): a closed chain of outward arcs along the box outline marking a changed area.</summary>
    public sealed class RevisionCloudType : AnnBoxType
    {
        public RevisionCloudType() : base("ann.cloud", new I18nText("Revision cloud", "Änderungswolke"), AnnCategories.PatentMarks,
            "revision cloud", "cloud", "change", "revision", "änderungswolke", "wolke", "änderung", "revision")
        { }

        public override Size2 DefaultSize => new(40, 26);
        public override bool IsPart => false;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Num("arc", "Arc length", "Bogenlänge", 5, 1, 100) };

        /// <summary>The cloud outline: arcs of about <paramref name="arc"/> mm bulging outwards, staying inside the box.</summary>
        public static PathPrim Outline(double w, double h, double arc, Style style)
        {
            arc = Math.Max(1, arc);
            var bulge = Math.Min(arc * 0.35, Math.Min(w, h) / 4);
            double hw = w / 2 - bulge, hh = h / 2 - bulge;
            var corners = new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw, hh), new Vec2(-hw, hh) };
            var segs = new List<PathSeg> { PathSeg.Move(corners[0]) };
            for (var i = 0; i < 4; i++)
            {
                Vec2 a = corners[i], b = corners[(i + 1) % 4];
                var side = a.DistanceTo(b);
                var n = Math.Max(1, (int)Math.Round(side / arc));
                var d = (b - a) / n;
                var outward = new Vec2(d.Y, -d.X).Normalized() * (bulge * 4.0 / 3.0); // y down: left normal of a clockwise walk points out
                for (var k = 0; k < n; k++)
                {
                    var p = a + d * k; var q = p + d;
                    segs.Add(PathSeg.Cubic(p + outward, q + outward, q));
                }
            }
            segs.Add(PathSeg.Close);
            return new PathPrim(segs, style);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Outline(o.Size.W, o.Size.H, o.Num("arc", 5), Style.Visible with { StrokeWidth = 0.35 }) with { Tag = AnnTags.Main } };
    }
}
