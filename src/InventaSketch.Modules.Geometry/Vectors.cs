using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>Point with a label (A, B, P(2|3) …): a marker in the box centre and the label in a chosen direction.</summary>
    internal sealed class LabelledPointType : MathBoxType
    {
        public LabelledPointType() : base("math.point.labelled", new I18nText("Point with label", "Punkt mit Beschriftung"), MathCategories.Vectors,
            "point", "label", "dot", "punkt", "beschriftung", "markierung", "A") { }

        public override Size2 DefaultSize => new(14, 10);
        public override bool PreferUniformScale => true;

        public static readonly ParamDef Marker = Pm.Choice("marker", "Marker", "Markierung", "dot",
            ("dot", "Dot", "Punkt"), ("cross", "Cross", "Kreuz"), ("circle", "Circle", "Kreis"));
        public static readonly ParamDef Position = Pm.Choice("position", "Label position", "Lage der Beschriftung", "ne",
            ("ne", "Top right", "Rechts oben"), ("nw", "Top left", "Links oben"), ("se", "Bottom right", "Rechts unten"), ("sw", "Bottom left", "Links unten"),
            ("n", "Above", "Oben"), ("s", "Below", "Unten"), ("e", "Right", "Rechts"), ("w", "Left", "Links"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Text("label", "Label", "Beschriftung", "A"), Marker, Position,
        };

        internal static Vec2 PositionDir(string pos) => pos switch
        {
            "nw" => new Vec2(-1, -1), "se" => new Vec2(1, 1), "sw" => new Vec2(-1, 1),
            "n" => new Vec2(0, -1), "s" => new Vec2(0, 1), "e" => new Vec2(1, 0), "w" => new Vec2(-1, 0),
            _ => new Vec2(1, -1),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var k = K(o);
            var r = Math.Min(0.7 * Math.Min(k, 1.5), Math.Min(hw, hh) * 0.9);
            switch (o.Str("marker", "dot"))
            {
                case "cross":
                    f.Line(new Vec2(-r * 1.3, -r * 1.3), new Vec2(r * 1.3, r * 1.3), Mx.Mark, MathTags.Dot);
                    f.Line(new Vec2(-r * 1.3, r * 1.3), new Vec2(r * 1.3, -r * 1.3), Mx.Mark, MathTags.Dot);
                    break;
                case "circle":
                    f.Circle(Vec2.Zero, r, Mx.Mark);
                    break;
                default:
                    f.Dot(Vec2.Zero, r);
                    break;
            }
            f.Port("pt", Vec2.Zero);
            var text = o.Str("label", "A");
            if (text.Length == 0) return;
            var d = PositionDir(o.Str("position", "ne"));
            var gap = r * 1.3 + 0.4;
            var ts = Mx.LabelSize * k;
            var perChar = Math.Max(1, text.Length) * Mx.CharW;
            if (d.X != 0) ts = Math.Min(ts, (hw - gap) / perChar);
            else ts = Math.Min(ts, 2 * hw / perChar);
            if (d.Y != 0) ts = Math.Min(ts, d.X != 0 ? hh - gap * 0.6 : hh - gap);
            else ts = Math.Min(ts, 2 * hh);
            if (ts < 0.4) return;
            var w = Mx.TextW(text, ts);
            var at = d.X != 0 && d.Y != 0 ? new Vec2(d.X * (gap + w / 2), d.Y * (gap * 0.6 + ts / 2))
                : d.X != 0 ? new Vec2(d.X * (gap + w / 2), 0)
                : new Vec2(0, d.Y * (gap + ts / 2));
            f.Text(at, text, ts);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.TextInput(o, "math.point.label", new I18nText("Label…", "Beschriftung…"), "label", "A", "label");
            yield return Mx.Cycle(o, "math.point.position", new I18nText("Move label around", "Beschriftung versetzen"), Position, "label");
            yield return Mx.Cycle(o, "math.point.marker", new I18nText("Next marker", "Nächste Markierung"), Marker, "marker");
        }
    }

    /// <summary>Vector arrow between two points with its name, optional components (dashed) and point labels.</summary>
    internal sealed class VectorType : MathPointType
    {
        public VectorType() : base("math.vector.arrow", new I18nText("Vector", "Vektor"), MathCategories.Vectors,
            "vector", "arrow", "vektor", "pfeil", "ortsvektor", "position vector", "components", "komponenten", "zerlegung") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 8), new Vec2(15, -8) };
        public override IReadOnlyList<string> Companions => new[] { "math.vector.sum", "math.point.labelled", "math.axes.cartesian" };

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Text("label", "Name", "Name", "v"),
            Pm.Notation(),
            Pm.Bool("components", "Components (dashed)", "Komponenten (gestrichelt)", false),
            Pm.Text("tailLabel", "Start point label", "Beschriftung Anfangspunkt", ""),
            Pm.Text("tipLabel", "End point label", "Beschriftung Endpunkt", ""),
            Pm.Bool("points", "Mark the points", "Punkte markieren", false),
            Pm.Num("textSize", "Text height", "Schrifthöhe", Mx.LabelSize, 1, 20, "mm", 0.5),
        };

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            Vec2 a = p[0], b = p[p.Count - 1];
            var ts = o.Num("textSize", Mx.LabelSize);
            var notation = o.Str("notation", "arrow");
            var extra = Mx.NotationHeight(notation, ts);
            var d = b - a;
            var len = d.Length;
            if (len < 1e-6) { f.Dot(a); return; }
            var u = d / len;
            f.Line(a, b, Style.Visible with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium }, MathTags.Main);
            var name = o.Str("label", "v");
            if (name.Length > 0)
            {
                var n = Mx.UpperNormal(u);
                Mx.VectorLabel(f, Mx.Beside(Vec2.Lerp(a, b, 0.5), n, name, ts, 1.0, extra), name, ts, notation);
            }
            if (o.Flag("points", false)) { f.Dot(a); f.Dot(b); }
            var tail = o.Str("tailLabel", "");
            if (tail.Length > 0) f.Text(Mx.Beside(a, -u, tail, ts, 1.2), tail, ts);
            var tip = o.Str("tipLabel", "");
            if (tip.Length > 0) f.Text(Mx.Beside(b, u, tip, ts, 1.2), tip, ts);
            f.Pt(AnchorRole.Mid, Vec2.Lerp(a, b, 0.5), "mid");
            if (!o.Flag("components", false)) return;

            var corner = new Vec2(b.X, a.Y);
            var helper = Mx.Helper with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            var main = name.Length > 0 ? name : "v";
            var iw = Mx.IndexedWidth(main, "x", ts);
            if (Math.Abs(b.X - a.X) > 0.5)
            {
                f.Line(a, corner, helper, MathTags.Helper);
                // Below the horizontal component when the vector points up, else above it.
                var below = b.Y <= a.Y;
                var y = below ? a.Y + 1.0 + ts / 2 : a.Y - 1.0 - ts / 2 - extra;
                Mx.IndexedLabel(f, new Vec2((a.X + corner.X) / 2, y), main, "x", ts, notation);
            }
            if (Math.Abs(b.Y - a.Y) > 0.5)
            {
                f.Line(corner, b, helper, MathTags.Helper);
                var right = b.X >= a.X;
                var x = right ? corner.X + 1.2 + iw / 2 : corner.X - 1.2 - iw / 2;
                Mx.IndexedLabel(f, new Vec2(x, (corner.Y + b.Y) / 2), main, "y", ts, notation);
            }
            f.Pt(AnchorRole.Mid, corner, "corner");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Mx.TextInput(o, "math.vector.label", new I18nText("Name…", "Name…"), "label", "v", "label");
            yield return Mx.Cycle(o, "math.vector.notation", new I18nText("Next notation", "Nächste Schreibweise"), Pm.Notation(), "label");
            yield return Mx.Toggle(o, "math.vector.components", new I18nText("Components", "Komponenten"), "components", false, "vector");
            yield return Mx.Toggle(o, "math.vector.points", new I18nText("Mark the points", "Punkte markieren"), "points", false, "vector");
            yield return new ContextAction("math.vector.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Swap, Edits.ReversePoints) { Group = "vector" };
        }
    }

    /// <summary>
    /// Vector addition a + b as one element: parallelogram construction (points O, tip of a, tip of b) or head-to-tail
    /// construction (points O, tip of a, tip of b drawn from the tip of a), with the resultant.
    /// </summary>
    internal sealed class VectorSumType : MathPointType
    {
        public VectorSumType() : base("math.vector.sum", new I18nText("Vector addition", "Vektoraddition"), MathCategories.Vectors,
            "vector addition", "sum", "resultant", "parallelogram", "head to tail", "vektoraddition", "summe", "resultierende", "parallelogramm", "kräfteparallelogramm", "aneinanderhängen") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-18, 10), new Vec2(12, 10), new Vec2(-8, -10) };
        protected override int MinPoints => 3;

        public static readonly ParamDef Construction = Pm.Choice("construction", "Construction", "Konstruktion", "parallelogram",
            ("parallelogram", "Parallelogram", "Parallelogramm"), ("tail", "Head to tail", "Aneinanderhängen"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Construction,
            Pm.Text("labelA", "Name of the first vector", "Name des ersten Vektors", "a"),
            Pm.Text("labelB", "Name of the second vector", "Name des zweiten Vektors", "b"),
            Pm.Notation(),
            Pm.Bool("result", "Resultant", "Resultierende", true),
            Pm.Num("textSize", "Text height", "Schrifthöhe", Mx.LabelSize, 1, 20, "mm", 0.5),
        };

        /// <summary>O, the tip of a, the tip of the resultant S and the start of b (O or the tip of a) for the construction.</summary>
        internal static (Vec2 O, Vec2 A, Vec2 B, Vec2 S) Points(SketchObject o, IReadOnlyList<Vec2> p)
        {
            Vec2 O = p[0], A = p[1], third = p[2];
            var tail = o.Str("construction", "parallelogram") == "tail";
            var b = tail ? third - A : third - O;
            return (O, A, O + b, A + b);
        }

        internal override void Draw(Fig f, SketchObject o, Lang lang, IReadOnlyList<Vec2> p)
        {
            var (O, A, B, S) = Points(o, p);
            var tail = o.Str("construction", "parallelogram") == "tail";
            var ts = o.Num("textSize", Mx.LabelSize);
            var notation = o.Str("notation", "arrow");
            var extra = Mx.NotationHeight(notation, ts);
            string la = o.Str("labelA", "a"), lb = o.Str("labelB", "b");
            Style Arrow(string color, double w = 0.5) => Style.Visible with { Stroke = color, StrokeWidth = w, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
            var dashed = Mx.Helper with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            var centre = tail ? (O + A + S) / 3 : (O + S) / 2;

            void Label(Vec2 from, Vec2 to, string text, string color, Vec2 awayFrom)
            {
                if (text.Length == 0 || from.DistanceTo(to) < 1e-6) return;
                var mid = Vec2.Lerp(from, to, 0.5);
                var n = (to - from).Normalized().Perp;
                if (n.Dot(awayFrom - mid) > 0) n = -n;
                Mx.VectorLabel(f, Mx.Beside(mid, n, text, ts, 1.0, extra), text, ts, notation, color);
            }

            if (A.DistanceTo(O) > 1e-6) f.Line(O, A, Arrow(Mx.Blue), MathTags.Main);
            if (tail)
            {
                if (S.DistanceTo(A) > 1e-6) f.Line(A, S, Arrow(Mx.Green), MathTags.Main);
                Label(O, A, la, Mx.Blue, centre);
                Label(A, S, lb, Mx.Green, centre);
            }
            else
            {
                if (B.DistanceTo(O) > 1e-6) f.Line(O, B, Arrow(Mx.Green), MathTags.Main);
                if (S.DistanceTo(A) > 1e-6) f.Line(A, S, Mx.Colored(dashed, Mx.Green), MathTags.Helper);
                if (S.DistanceTo(B) > 1e-6) f.Line(B, S, Mx.Colored(dashed, Mx.Blue), MathTags.Helper);
                Label(O, A, la, Mx.Blue, centre);
                Label(O, B, lb, Mx.Green, centre);
            }
            if (o.Flag("result", true) && S.DistanceTo(O) > 1e-6)
            {
                f.Line(O, S, Arrow(Mx.Red, 0.6), MathTags.Resultant);
                if (la.Length > 0 && lb.Length > 0)
                {
                    var tokens = new List<(string, bool)> { (la, true), ("+", false), (lb, true) };
                    var mid = Vec2.Lerp(O, S, 0.5);
                    var n = (S - O).Normalized().Perp;
                    if (n.Dot(A - mid) > 0) n = -n;
                    var w = Mx.TokenRowWidth(tokens, ts);
                    var half = Math.Abs(n.X) * w / 2 + Math.Abs(n.Y) * (ts + extra) / 2;
                    var at = mid + n * (1.0 + half) + new Vec2(0, extra / 2);
                    Mx.TokenRow(f, at, tokens, ts, notation, Mx.Red);
                }
            }
            f.Port("sum", S);
            f.Pt(AnchorRole.Mid, Vec2.Lerp(O, S, 0.5), "mid");
        }

        /// <summary>The object switched to <paramref name="construction"/>, its third point moved so that a and b stay the same.</summary>
        internal static SketchObject Switch(SketchObject before, string construction)
        {
            var from = before.Str("construction", "parallelogram");
            var next = before.WithParam("construction", construction);
            if (from == construction || before.Points is not { Count: >= 3 } pts) return next;
            Vec2 O = pts[0], A = pts[1], third = pts[2];
            var moved = construction == "tail" ? A + (third - O) : O + (third - A);
            return WithPoint(next, 2, moved);
        }

        public override SketchObject OnParamChanged(SketchObject before, SketchObject after, string key)
        {
            if (key != "construction") return after;
            var target = after.Str("construction", "parallelogram");
            return Switch(after.WithParam("construction", before.Str("construction", "parallelogram")), target);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = o.Str("construction", "parallelogram");
            yield return new ContextAction("math.sum.parallelogram", new I18nText("Parallelogram", "Parallelogramm"), IconIds.Cycle, x => Switch(x, "parallelogram"))
                { IsActive = current == "parallelogram", Group = "construction", ParamKeys = new[] { "construction" } };
            yield return new ContextAction("math.sum.tail", new I18nText("Head to tail", "Aneinanderhängen"), IconIds.Cycle, x => Switch(x, "tail"))
                { IsActive = current == "tail", Group = "construction", ParamKeys = new[] { "construction" } };
            yield return Mx.Toggle(o, "math.sum.result", new I18nText("Resultant", "Resultierende"), "result", true, "construction");
            yield return Mx.Cycle(o, "math.sum.notation", new I18nText("Next notation", "Nächste Schreibweise"), Pm.Notation(), "label");
        }
    }
}
