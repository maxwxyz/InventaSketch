using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// View arrow with a capital letter (direction of a view "A", ISO 128 idea, drawn independently). Touching: the head
    /// touches the outline and the letter stands beside the shaft; freestanding: the letter stands beyond the tail, in line
    /// with the arrow.
    /// </summary>
    public sealed class ViewArrowType : AnnPointType
    {
        public ViewArrowType() : base("ann.viewarrow", new I18nText("View arrow with letter", "Ansichtspfeil mit Buchstabe"), AnnCategories.PatentMarks,
            "view", "view arrow", "direction of view", "ansicht", "ansichtspfeil", "blickrichtung", "letter", "buchstabe")
        { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, -15), new Vec2(0, 0) };
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("letter", "Letter", "Buchstabe", "A"),
            P.Choice("mode", "Letter position", "Lage des Buchstabens", "touching", ("touching", "Beside the shaft (head touches)", "Neben dem Schaft (Spitze berührt)"),
                ("freestanding", "Beyond the tail (freestanding)", "Hinter dem Pfeilende (freistehend)")),
            P.TextSize(Ann.TextSize),
        };

        public static string LetterOf(SketchObject o) => !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("letter", "A");

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return Array.Empty<Primitive>();
            var d = (b - a) / len;
            var list = new List<Primitive>
            {
                new LinePrim(a, b, Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Large }) { Tag = AnnTags.Main },
            };
            var letter = LetterOf(o);
            if (!string.IsNullOrEmpty(letter))
            {
                var size = TextSizeOf(o);
                Vec2 at;
                if (o.Str("mode", "touching") == "freestanding")
                    at = a - d * (1.2 + Ann.HalfExtentAlong(letter, size, d));
                else
                {
                    var n = d.Perp;
                    if (n.X < 0 || (Math.Abs(n.X) < 1e-9 && n.Y > 0)) n = n * -1; // right of / above the shaft
                    at = Vec2.Lerp(a, b, 0.35) + n * (1.5 + Ann.HalfExtentAlong(letter, size, n));
                }
                list.Add(Ann.Text(at, letter, size, "letter", bold: true));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.viewarrow.mode", new I18nText("Letter", "Buchstabe"), "mode", "touching", "mode", new (string, I18nText, string?)[]
            {
                ("touching", new I18nText("Beside the shaft", "Neben dem Schaft"), null),
                ("freestanding", new I18nText("Beyond the tail", "Hinter dem Pfeilende"), null),
            })) yield return a;
            yield return Ann.SwapAction();
        }
    }

    /// <summary>
    /// Detail view frame (circle or rectangle) for the enlarged drawing of a detail, labelled with the letter of its detail
    /// marker (<c>ann.detail.circle</c>): without an own letter the n-th detail view on the page takes the letter of the n-th
    /// marker. An optional graphic scale bar replaces a written scale ("2:1"), which the offices' drawing rules do not allow.
    /// </summary>
    public sealed class DetailViewType : AnnBoxType
    {
        public DetailViewType() : base("ann.detail.view", new I18nText("Detail view (enlarged)", "Einzelheit (vergrößerte Ansicht)"), AnnCategories.PatentMarks,
            "detail", "detail view", "enlarged", "einzelheit", "vergrößert", "vergrößerung", "ausschnitt", "scale bar", "maßstabsbalken")
        { }

        public override Size2 DefaultSize => new(40, 48);
        public override bool DependsOnNeighbours => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("frame", "Frame", "Rahmen", "circle", ("circle", "Circle", "Kreis"), ("rect", "Rectangle", "Rechteck"))
                .WithOptionIcons(v => v == "rect" ? IconIds.ShapeSquare : IconIds.ShapeCircle),
            P.Text("letter", "Letter (empty: from the detail marker)", "Buchstabe (leer: vom Einzelheit-Kreis)", null),
            P.Bool("scaleBar", "Graphic scale bar", "Grafischer Maßstab", false),
            P.Num("scale", "Enlargement", "Vergrößerung", 2, 0.1, 100, "×", 0.5),
            P.Num("barLength", "Length shown by the bar", "Länge des Maßstabsbalkens", 5, 0.01, 10000, "mm", 1),
            P.TextSize(Ann.TextSize),
        };

        /// <summary>
        /// The letter of a detail view: its own (text or parameter), else that of the detail marker with the same rank on the
        /// page (n-th view without own letter ↔ n-th marker), else "A".
        /// </summary>
        public static string LetterOf(SketchObject o, SketchDocument doc)
        {
            if (!string.IsNullOrEmpty(o.Text)) return o.Text!;
            var own = o.Str("letter", "");
            if (own.Length > 0) return own;
            var page = doc.Pages.FirstOrDefault(pg => pg.Order.Contains(o.Id));
            if (page is null) return "A";
            var objects = doc.ObjectsOn(page).ToList();
            var views = objects.Where(x => x.Type == "ann.detail.view" && string.IsNullOrEmpty(x.Text) && x.Str("letter", "").Length == 0).Select(x => x.Id).ToList();
            var markers = objects.Where(x => x.Type == "ann.detail.circle").ToList();
            var k = views.IndexOf(o.Id);
            if (k < 0 || markers.Count == 0) return "A";
            var m = markers[Math.Min(k, markers.Count - 1)];
            return !string.IsNullOrEmpty(m.Text) ? m.Text! : m.Str("letter", "A");
        }

        /// <summary>A "nice" length (1, 2 or 5 × 10^k) not above <paramref name="max"/>.</summary>
        public static double NiceBelow(double max)
        {
            if (max <= 0) return 0;
            var e = Math.Pow(10, Math.Floor(Math.Log10(max)));
            foreach (var f in new[] { 5.0, 2.0, 1.0 }) if (f * e <= max + 1e-12) return f * e;
            return e;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var size = TextSizeOf(o);
            var band = Math.Min(h * 0.4, size * 1.6);
            var fh = Math.Max(1, h - band);
            var style = Style.ThinLine with { StrokeWidth = 0.35 };
            var list = new List<Primitive>();
            var cy = -h / 2 + fh / 2;
            if (o.Str("frame", "circle") == "rect") list.Add(Prim.Rect(-w / 2, -h / 2, w, fh, style) with { Tag = AnnTags.Main });
            else { var r = Math.Min(w, fh) / 2; list.Add(Prim.Circle(0, cy, r, style) with { Tag = AnnTags.Main }); }
            var letter = LetterOf(o, ctx.Doc);
            var ly = h / 2 - band / 2;
            var ts = Math.Min(size, band * 0.75);
            if (!o.Flag("scaleBar", false))
            {
                if (letter.Length > 0) list.Add(Ann.Text(new Vec2(0, ly), letter, ts, "letter", bold: true));
                return list;
            }
            // Letter on the left, then the scale bar: a bar of the drawn length with end and middle ticks and its real length.
            var x0 = -w / 2;
            if (letter.Length > 0)
            {
                list.Add(Ann.Text(new Vec2(x0, ly), letter, ts, "letter", TextAnchor.Start, bold: true));
                x0 += Ann.TextExtent(letter, ts).X + ts;
            }
            var scale = Math.Max(0.1, o.Num("scale", 2));
            var real = Math.Max(0.01, o.Num("barLength", 5));
            var label0 = Ann.Fmt(real, 3) + " mm";
            var avail = w / 2 - x0 - Ann.TextExtent(label0, ts * 0.8).X - 1.5;
            if (real * scale > avail) real = NiceBelow(Math.Max(0.01, avail) / scale);
            var len = real * scale;
            if (len <= 0.2) { return list; }
            var label = Ann.Fmt(real, 3) + " mm";
            var tick = Math.Min(band * 0.25, 1.2);
            var bar = Style.ThinLine with { StrokeWidth = 0.35 };
            list.Add(Prim.Line(x0, ly, x0 + len, ly, bar) with { Tag = "scalebar" });
            foreach (var t in new[] { 0.0, 0.5, 1.0 })
                list.Add(Prim.Line(x0 + len * t, ly - tick, x0 + len * t, ly + (t == 0.5 ? 0 : tick), bar) with { Tag = "scalebar" });
            list.Add(Ann.Text(new Vec2(x0 + len + 1, ly), label, ts * 0.8, "scalebar", TextAnchor.Start));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Ann.Choice(o, "ann.detailview.frame", new I18nText("Frame", "Rahmen"), "frame", "circle", "frame", new (string, I18nText, string?)[]
            {
                ("circle", new I18nText("Circle", "Kreis"), IconIds.ShapeCircle),
                ("rect", new I18nText("Rectangle", "Rechteck"), IconIds.ShapeSquare),
            })) yield return a;
            yield return Ann.Toggle(o, "ann.detailview.scalebar", new I18nText("Graphic scale bar", "Grafischer Maßstab"), IconIds.Toggle, "scaleBar", false);
        }
    }
}
