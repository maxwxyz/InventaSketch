using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Frame variants of text-bearing boxes.</summary>
    public enum TextFrame { None, Rect, Rounded, Circle }

    /// <summary>Text box, framed label, rounded callout and numbered balloon.</summary>
    public sealed class TextBoxType : AnnBoxType
    {
        private readonly TextFrame _frame;
        private readonly string _defaultText;

        private TextBoxType(string id, I18nText name, TextFrame frame, string defaultText, Size2 size, params string[] keywords)
            : base(id, name, AnnCategories.Labels, keywords)
        {
            _frame = frame; _defaultText = defaultText; DefaultSize = size;
        }

        public static TextBoxType Text() => new("ann.text", new I18nText("Text", "Text"), TextFrame.None, "Text", new Size2(40, 10), "text", "note", "notiz", "beschriftung");
        public static TextBoxType Label() => new("ann.label", new I18nText("Label", "Beschriftungsfeld"), TextFrame.Rect, "Label", new Size2(36, 12), "label", "frame", "rahmen", "schild", "box");
        public static TextBoxType Callout() => new("ann.callout", new I18nText("Callout", "Hinweisfeld"), TextFrame.Rounded, "Note", new Size2(44, 16), "callout", "note", "hinweis", "sprechblase", "rounded");
        /// <summary>Numbered circle. The offices forbid encircled reference signs (EP A49 Art. 1(2)(d), PCT R. 11.13(e), 37 CFR 1.84(p)(1)); use for presentations.</summary>
        public static TextBoxType Balloon() => new("ann.balloon", new I18nText("Balloon", "Positionsnummer (Kreis)"), TextFrame.Circle, "1", new Size2(9, 9), "balloon", "item", "position", "kreis", "nummer");

        public override Size2 DefaultSize { get; }
        public override bool HasText => true;
        public override bool PreferUniformScale => _frame == TextFrame.Circle;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.TextSize(_frame == TextFrame.Circle ? 4.5 : Ann.TextSize),
            P.Choice("align", "Alignment", "Ausrichtung", _frame == TextFrame.None ? "left" : "center", ("left", "Left", "Links"), ("center", "Centre", "Mitte"), ("right", "Right", "Rechts")),
            P.Bool("bold", "Bold", "Fett", false),
            P.Bool("italic", "Italic", "Kursiv", false),
            P.Bool("frame", "Frame", "Rahmen", _frame != TextFrame.None),
        };

        public string TextOf(SketchObject o) => o.Text ?? _defaultText;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive>();
            if (o.Flag("frame", _frame != TextFrame.None))
            {
                var style = Style.ThinLine with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") };
                list.Add(_frame switch
                {
                    TextFrame.Circle => (Primitive)Prim.Ellipse(0, 0, w / 2, h / 2, style),
                    TextFrame.Rounded => Ann.RoundedRect(-w / 2, -h / 2, w, h, Math.Min(3, Math.Min(w, h) / 4), style),
                    _ => Prim.Rect(-w / 2, -h / 2, w, h, style),
                } with { Tag = AnnTags.Main }); // tagged: a dashed or recoloured frame (note box) takes the style override
            }
            var text = TextOf(o);
            if (!string.IsNullOrEmpty(text))
            {
                var align = o.Str("align", _frame == TextFrame.None ? "left" : "center");
                var pad = _frame == TextFrame.Circle ? 0 : 1.5;
                var (x, anchor) = align switch
                {
                    "left" => (-w / 2 + pad, TextAnchor.Start),
                    "right" => (w / 2 - pad, TextAnchor.End),
                    _ => (0.0, TextAnchor.Middle),
                };
                list.Add(Ann.Text(new Vec2(x, 0), text, TextSizeOf(o, _frame == TextFrame.Circle ? 4.5 : Ann.TextSize), _frame == TextFrame.Circle ? AnnTags.Numeral : null, anchor, o.Flag("bold", false), o.Flag("italic", false)));
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var a in base.Anchors(o, ctx)) yield return a;
            if (_frame == TextFrame.Circle) yield return Anchor.Circle(AnchorRole.Quadrant, Vec2.Zero, Math.Min(o.Size.W, o.Size.H) / 2);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var def = _frame != TextFrame.None;
            yield return Ann.Toggle(o, "ann.text.frame", new I18nText("Frame", "Rahmen"), "frame", "frame", def, "frame");
            yield return Ann.Toggle(o, "ann.text.bold", new I18nText("Bold", "Fett"), "bold", "bold", false, "font");
            yield return Ann.Toggle(o, "ann.text.italic", new I18nText("Italic", "Kursiv"), "italic", "italic", false, "font");
            var ad = _frame == TextFrame.None ? "left" : "center";
            yield return Ann.SetChoice(o, "ann.align.left", new I18nText("Align left", "Linksbündig"), "align-left", "align", "left", ad, "align");
            yield return Ann.SetChoice(o, "ann.align.center", new I18nText("Centre", "Zentriert"), "align-center", "align", "center", ad, "align");
            yield return Ann.SetChoice(o, "ann.align.right", new I18nText("Align right", "Rechtsbündig"), "align-right", "align", "right", ad, "align");
        }
    }

    /// <summary>Figure label ("FIG. 1"). Larger than reference numerals (EP GL A-IX 5.2; 37 CFR 1.84(u)).</summary>
    public sealed class FigLabelType : AnnBoxType
    {
        public const double DefaultTextSize = 7.0;

        public FigLabelType() : base("ann.figlabel", new I18nText("Figure label", "Figurenbezeichnung"), AnnCategories.PatentMarks, "fig", "figure", "figur", "abbildung", "label") { }

        public override Size2 DefaultSize => new(30, 10);
        public override bool HasText => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("label", "Label", "Bezeichnung", "FIG. 1"),
            P.TextSize(DefaultTextSize),
            P.Bool("underline", "Underline", "Unterstreichen", false),
        };

        public static string LabelOf(SketchObject o) => !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("label", "FIG. 1");

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var text = LabelOf(o);
            var size = TextSizeOf(o, DefaultTextSize);
            var label = Ann.Text(Vec2.Zero, text, size, AnnTags.FigLabel, bold: true);
            // Underline as a font attribute (hosts use their native underline, placed by the real font).
            if (o.Flag("underline", false)) label = label with { TextStyle = label.TextStyle with { Underline = true } };
            return new List<Primitive> { label };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.fig.underline", new I18nText("Underline", "Unterstreichen"), "underline", "underline", false);
        }
    }

    /// <summary>Detail view marker: thin (or dash-dot) circle with a letter (EP GL A-IX 7.6).</summary>
    public sealed class DetailCircleType : AnnBoxType
    {
        public DetailCircleType() : base("ann.detail.circle", new I18nText("Detail marker", "Einzelheit-Kreis"), AnnCategories.PatentMarks, "detail", "einzelheit", "circle", "kreis", "enlarged", "vergrößert") { }

        public override Size2 DefaultSize => new(24, 24);
        public override bool PreferUniformScale => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("letter", "Letter", "Buchstabe", "A"),
            P.Bool("dashdot", "Dash-dot circle", "Strichpunkt-Kreis", false),
            P.TextSize(Ann.TextSize),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            var style = o.Flag("dashdot", false) ? Style.CenterLine : Style.ThinLine with { StrokeWidth = 0.35 };
            var list = new List<Primitive> { Prim.Ellipse(0, 0, rx, ry, style) with { Tag = AnnTags.Main } };
            var letter = o.Text ?? o.Str("letter", "A");
            if (!string.IsNullOrEmpty(letter))
            {
                var size = TextSizeOf(o);
                var dir = new Vec2(Math.Cos(-Math.PI / 4), Math.Sin(-Math.PI / 4));
                var edge = new Vec2(rx * dir.X, ry * dir.Y);
                list.Add(Ann.Text(edge + dir * (1.2 + Ann.HalfExtentAlong(letter, size, dir)), letter, size));
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Circle(AnchorRole.Quadrant, Vec2.Zero, Math.Min(o.Size.W, o.Size.H) / 2);
        }

        /// <summary>Detail views without an own letter take this marker's letter (<see cref="DetailViewType.LetterOf"/>).</summary>
        public override bool DependsOnNeighbours => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var page = ctx.Doc.Pages.FirstOrDefault(pg => pg.Order.Contains(o.Id));
            return page is null ? Enumerable.Empty<string>() : ctx.Doc.ObjectsOn(page).Where(x => x.Type == "ann.detail.view").Select(x => x.Id).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.detail.dashdot", new I18nText("Dash-dot circle", "Strichpunkt-Kreis"), "dashdot", "dashdot", false);
        }
    }

    /// <summary>
    /// Section (cutting-plane) line: thin chain line, thick ends, view arrows and letters ("A–A")
    /// (37 CFR 1.84(h)(3); EP GL A-IX 7.3.1).
    /// </summary>
    public sealed class SectionLineType : AnnPointType
    {
        public SectionLineType() : base("ann.sectionline", new I18nText("Section line", "Schnittlinie"), AnnCategories.PatentMarks, "section", "cut", "schnitt", "cutting plane", "schnittebene", "a-a") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-25, 0), new Vec2(25, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("letter", "Letter", "Buchstabe", "A"),
            P.Bool("flip", "Flip view direction", "Blickrichtung umkehren", false),
            P.Bool("continuous", "Chain line through", "Durchgehende Strichpunktlinie", true),
            P.TextSize(Ann.TextSize),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return Array.Empty<Primitive>();
            var d = (b - a) / len;
            var n = d.Perp * (o.Flag("flip", false) ? -1 : 1);
            var thick = Math.Min(7, len / 3);
            var list = new List<Primitive>();
            var cut = Style.Of(LineRole.Cut) with { Dash = DashStyle.Solid };
            if (o.Flag("continuous", true) && len > 2 * thick)
                list.Add(new LinePrim(a + d * thick, b - d * thick, Style.ThinLine with { Dash = DashStyle.DashDot }) { Tag = AnnTags.Main });
            list.Add(new LinePrim(a, a + d * thick, cut));
            list.Add(new LinePrim(b - d * thick, b, cut));
            var letter = o.Text ?? o.Str("letter", "A");
            var size = TextSizeOf(o);
            foreach (var (end, inward) in new[] { (a, d), (b, -d) })
            {
                var basePt = end + inward * (thick * 0.35);
                var tip = basePt + n * 9;
                list.Add(new LinePrim(basePt, tip, Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Large }));
                if (!string.IsNullOrEmpty(letter))
                    list.Add(Ann.Text(tip - inward * (1.5 + Ann.HalfExtentAlong(letter, size, d)), letter, size, bold: true));
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.section.flip", new I18nText("Flip view direction", "Blickrichtung umkehren"), "flip", "flip", false);
            yield return Ann.Toggle(o, "ann.section.continuous", new I18nText("Chain line through", "Durchgehende Strichpunktlinie"), "dashdot", "continuous", true);
        }
    }
}
