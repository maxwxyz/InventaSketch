using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Sequence diagram lifeline: head (box or actor figure) of fixed height with a dashed tail down to the bottom of the
    /// object box. Stretching the box lengthens the tail; the head keeps its height. The tail is a
    /// <see cref="AnchorRole.Rail"/>: message ends and activation bars dock anywhere on it, not only on a fixed raster.
    /// </summary>
    public sealed class LifelineType : DiagBoxType
    {
        /// <summary>Anchor id of the tail rail.</summary>
        public const string TailId = "tail";

        public LifelineType() : base("diag.seq.lifeline", new I18nText("Lifeline", "Lebenslinie"), DiagCategories.Sequence,
            "uml", "sequence", "sequenz", "sequenzdiagramm", "lifeline", "lebenslinie", "object", "objekt", "participant", "teilnehmer") { }

        public override Size2 DefaultSize => new(30, 80);
        protected override I18nText Placeholder => new(":Object", ":Objekt");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("head", "Head", "Kopf", "box", ("box", "Box (object)", "Kasten (Objekt)"), ("actor", "Actor figure", "Akteur-Figur")),
            P.Num("headHeight", "Head height", "Kopfhöhe", 9, 4, 60),
            P.Bool("destroyed", "Destroyed (X at the end)", "Zerstört (X am Ende)", false),
            P.Text("breaks", "Time gaps (% of the tail, e.g. 50; 80)", "Zeitlücken (% der Linie, z. B. 50; 80)", null),
            P.TextSize(),
        };

        /// <summary>Half length of the gap a time break leaves in the tail (mm).</summary>
        public const double BreakGap = 1.6;

        /// <summary>Time-gap positions as fractions of the tail (0..1, sorted), read from "50; 80" (percent).</summary>
        public static IReadOnlyList<double> Breaks(SketchObject o)
        {
            var list = new List<double>();
            foreach (var part in o.Str("breaks", "").Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(part.Trim().TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 && v < 100)
                    list.Add(v / 100);
            list.Sort();
            return list;
        }

        private static double HeadHeight(SketchObject o) => Math.Min(o.Num("headHeight", o.Str("head", "box") == "actor" ? 16 : 9), o.Size.H * 0.6);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var head = HeadHeight(o);
            var list = new List<Primitive>();
            var label = Label(o, ctx);
            var size = TextSizeOf(o);
            if (o.Str("head", "box") == "actor")
            {
                var lines = D.Lines(label);
                var ts = D.Fit(size, lines, w, head * 0.35, Math.Max(1, lines.Count));
                var labelH = lines.Count * ts * D.LineFactor;
                var figH = head - labelH;
                list.AddRange(D.StickFigure(new Vec2(0, -hh + figH / 2), Math.Min(w, figH * 0.6), figH, Style.Visible with { StrokeWidth = D.Stroke }));
                D.CenterText(list, label, ts, new Vec2(0, -hh + figH + labelH / 2), w, labelH);
            }
            else
            {
                list.Add(Prim.Rect(-hw, -hh, w, head, D.Outline) with { Tag = DiagTags.Outline });
                D.CenterText(list, label, size, new Vec2(0, -hh + head / 2), w - 2, head - 1);
            }
            var bottom = hh;
            var tailTop = -hh + head;
            var len = bottom - tailTop;
            var breaks = Breaks(o).Where(t => t * len > BreakGap + 0.5 && (1 - t) * len > BreakGap + 0.5).ToList();
            if (breaks.Count == 0)
                list.Add(new LinePrim(new Vec2(0, tailTop), new Vec2(0, bottom), D.Dashed(D.Thin)) { Tag = DiagTags.Main });
            else
            {
                // Time gaps: the tail is interrupted, two short parallel slashes ("//") mark each gap.
                var y = tailTop;
                var mark = Math.Min(2.2, hw);
                foreach (var t in breaks)
                {
                    var yc = tailTop + t * len;
                    if (yc - BreakGap > y) list.Add(new LinePrim(new Vec2(0, y), new Vec2(0, yc - BreakGap), D.Dashed(D.Thin)) { Tag = DiagTags.Main });
                    foreach (var dy in new[] { -BreakGap, BreakGap })
                        list.Add(new LinePrim(new Vec2(-mark, yc + dy + mark * 0.35), new Vec2(mark, yc + dy - mark * 0.35), D.Thin) { Tag = DiagTags.Marker });
                    y = yc + BreakGap;
                }
                if (bottom > y) list.Add(new LinePrim(new Vec2(0, y), new Vec2(0, bottom), D.Dashed(D.Thin)) { Tag = DiagTags.Main });
            }
            if (o.Flag("destroyed", false))
            {
                var x = Math.Min(2.5, hw);
                list.Add(new LinePrim(new Vec2(-x, bottom - 2 * x), new Vec2(x, bottom), D.OutlineOpen) { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(-x, bottom), new Vec2(x, bottom - 2 * x), D.OutlineOpen) { Tag = DiagTags.Marker });
            }
            return list;
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var head = HeadHeight(o);
            yield return ("top", new Vec2(0, -hh));
            yield return ("head.left", new Vec2(-hw, -hh + head / 2));
            yield return ("head.right", new Vec2(hw, -hh + head / 2));
            yield return ("bottom", new Vec2(0, hh));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var a in base.Anchors(o, ctx)) yield return a;
            var hh = o.Size.H / 2;
            var top = -hh + HeadHeight(o);
            if (hh - top <= Geo.Eps) yield break;
            yield return Anchor.Rail(new Vec2(0, top), new Vec2(0, hh), TailId);
            // The same line as an axis centres activation bars (their Hub) on the tail.
            yield return Anchor.Segment(AnchorRole.Axis, new Vec2(0, top), new Vec2(0, hh)) with { Id = TailId + ".axis" };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.seq.destroyed", new I18nText("Destroyed", "Zerstört"), "destroyed", false);
            yield return new ContextAction("diag.seq.timegap", new I18nText("Time gap", "Zeitlücke"), IconIds.Toggle,
                x => x.WithParam("breaks", Breaks(x).Count > 0 ? "" : "50"))
            { IsActive = Breaks(o).Count > 0 };
            yield return new ContextAction("diag.seq.actorHead", new I18nText("Actor figure", "Akteur-Figur"), IconIds.Toggle,
                x => x.WithParam("head", x.Str("head", "box") == "actor" ? "box" : "actor"))
            { IsActive = o.Str("head", "box") == "actor" };
        }
    }

    /// <summary>
    /// Activation (execution specification) bar on a lifeline. Ports along both sides every 2.5 mm; its centre is a
    /// <see cref="AnchorRole.Hub"/>, so it snaps centred onto a lifeline tail.
    /// </summary>
    public sealed class ActivationType : DiagBoxType
    {
        public ActivationType() : base("diag.seq.activation", new I18nText("Activation bar", "Aktivierungsbalken"), DiagCategories.Sequence,
            "uml", "sequence", "sequenz", "activation", "aktivierung", "execution", "ausführung", "focus of control", "balken") { }

        public override Size2 DefaultSize => new(3.5, 25);
        public override bool HasText => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H, D.Outline with { StrokeWidth = 0.3 }) with { Tag = DiagTags.Outline } };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("bottom", new Vec2(0, hh));
            var i = 0;
            for (var y = -hh; y <= hh + 1e-9; y += 2.5, i++)
            {
                yield return ("left." + D.Inv(i), new Vec2(-hw, y));
                yield return ("right." + D.Inv(i), new Vec2(hw, y));
            }
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            => base.Anchors(o, ctx).Append(Anchor.Point(AnchorRole.Hub, Vec2.Zero, "hub"));
    }

    /// <summary>Message kinds of sequence diagrams.</summary>
    public enum MessageKind { Sync, Async, Return, Create }

    /// <summary>
    /// Sequence message: synchronous (filled head), asynchronous (open head), reply (dashed, open head) or create
    /// (dashed, open head). Label text above the line. Two points on the same vertical draw a self-message loop.
    /// </summary>
    public sealed class MessageType : DiagConnectorType
    {
        public MessageType() : base("diag.seq.message", new I18nText("Message (sequence)", "Nachricht (Sequenz)"), DiagCategories.Sequence,
            "uml", "sequence", "sequenz", "message", "nachricht", "call", "aufruf", "sync", "async", "asynchron", "return", "antwort", "reply") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "sync", ("sync", "Synchronous", "Synchron"), ("async", "Asynchronous", "Asynchron"),
                ("return", "Reply", "Antwort"), ("create", "Create", "Erzeugen")),
            P.Text("step", "Step numeral (e.g. 302)", "Schrittnummer (z. B. 302)", null),
            P.TextSize(),
        };

        /// <summary>First numeral and increment of "Number messages" when the topmost message has no numeral yet.</summary>
        public const int NumberStart = 302, NumberIncrement = 2;

        /// <summary>
        /// Numbers the sequence messages on the page of <paramref name="o"/> top-down (by the height of their middle, then
        /// left to right): the topmost keeps its numeral if it is a number (else <see cref="NumberStart"/>), each next one
        /// gets <see cref="NumberIncrement"/> more. Idempotent, so running it for several selected messages gives one result.
        /// </summary>
        public static SketchDocument NumberMessages(SketchDocument doc, SketchObject o)
        {
            var page = doc.PageOf(o.Id);
            if (page is null) return doc;
            var msgs = doc.ObjectsOn(page).Where(x => x.Type == o.Type && x.IsPointGeometry)
                .Select(x => (Obj: x, Pts: doc.ResolvePoints(x)))
                .Where(x => x.Pts.Count >= 2)
                .OrderBy(x => Math.Round((x.Pts[0].Y + x.Pts[x.Pts.Count - 1].Y) / 2, 3)).ThenBy(x => Math.Min(x.Pts[0].X, x.Pts[x.Pts.Count - 1].X))
                .Select(x => x.Obj).ToList();
            if (msgs.Count == 0) return doc;
            var start = int.TryParse(msgs[0].Str("step", "").Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : NumberStart;
            var numbers = msgs.Select((m, i) => (m.Id, Value: D.Inv(start + i * NumberIncrement))).ToDictionary(x => x.Id, x => x.Value);
            return Edits.Update(doc, numbers.Keys.ToList(), x => x.Str("step", "") == numbers[x.Id] ? x : x.WithParam("step", numbers[x.Id]));
        }

        public static MessageKind KindOf(SketchObject o) => Enum.TryParse<MessageKind>(o.Str("kind", "sync"), true, out var k) ? k : MessageKind.Sync;

        protected override DiagEnd StartEnd(SketchObject o) => DiagEnd.None;
        protected override DiagEnd EndEnd(SketchObject o) => KindOf(o) == MessageKind.Sync ? DiagEnd.Filled : DiagEnd.Open;
        protected override bool IsDashed(SketchObject o) => KindOf(o) is MessageKind.Return or MessageKind.Create;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            // Self message: both ends on (nearly) the same vertical line -> loop to the right.
            if (p.Count == 2 && Math.Abs(p[0].X - p[1].X) < 0.5 && Math.Abs(p[0].Y - p[1].Y) > 0.5)
            {
                var dx = 8.0;
                var loop = new[] { p[0], p[0] + new Vec2(dx, 0), p[1] + new Vec2(dx, 0), p[1] };
                return base.RenderPoints(o, ctx, loop);
            }
            return base.RenderPoints(o, ctx, p);
        }

        protected override void Labels(List<Primitive> list, SketchObject o, RenderContext ctx, List<Vec2> path)
        {
            var text = o.Text;
            if (KindOf(o) == MessageKind.Create && string.IsNullOrEmpty(text)) text = "«create»";
            var step = o.Str("step", "").Trim();
            if (path.Count == 4 && Math.Abs(path[0].X - path[3].X) < 0.5)
            {
                // Self message: label to the right of the loop, the numeral before it.
                var mid = Vec2.Lerp(path[1], path[2], 0.5);
                list.AddRange(D.SideLabel(step.Length > 0 && !string.IsNullOrEmpty(text) ? step + " " + text : step.Length > 0 ? step : text, mid, new Vec2(1, 0), LabelSize(o),
                    step.Length > 0 && string.IsNullOrEmpty(text) ? DiagTags.Numeral : DiagTags.Label));
                return;
            }
            var (at, dir) = D.At(path, 0.5);
            list.AddRange(D.SideLabel(text, at, D.LabelSide(dir), LabelSize(o)));
            // Step numeral at the start of the arrow, above the line (as in method figures: "302").
            if (step.Length > 0) list.AddRange(EndLabel(step, path, true, 0, true, LabelSize(o)).Select(p => p with { Tag = DiagTags.Numeral }));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in D.Choice(o, "diag.msg.kind", new I18nText("Message", "Nachricht"), "kind", "sync", "kind", new (string, I18nText, string?)[]
            {
                ("sync", new I18nText("Synchronous", "Synchron"), IconIds.HeadTriangle),
                ("async", new I18nText("Asynchronous", "Asynchron"), IconIds.HeadOpen),
                ("return", new I18nText("Reply", "Antwort"), IconIds.HeadOpenDashed),
                ("create", new I18nText("Create", "Erzeugen"), IconIds.CreateObject),
            })) yield return a;
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
            yield return ContextAction.InDocument("diag.msg.number", new I18nText("Number messages (top-down, step 2)", "Nachrichten nummerieren (von oben, Schritt 2)"), IconIds.Renumber,
                (doc, x, _) => NumberMessages(doc, x)) with { Group = "numbers" };
        }
    }

    /// <summary>
    /// UML combined fragment: a frame with a pentagon tag in the top-left corner naming the operator (alt, opt, loop,
    /// par, break; the object text is appended, e.g. "loop 1..n") and one or more operands separated by dashed lines.
    /// Each operand carries an optional guard (one line of the <c>guards</c> parameter per operand). The frame is not
    /// filled, so lifelines and messages stay visible.
    /// </summary>
    public sealed class FragmentType : DiagBoxType
    {
        public FragmentType() : base("diag.seq.fragment", new I18nText("Combined fragment", "Kombiniertes Fragment"), DiagCategories.Sequence,
            "uml", "sequence", "sequenz", "fragment", "alt", "opt", "loop", "schleife", "par", "parallel", "break", "guard", "bedingung", "operand", "frame", "rahmen") { }

        public override Size2 DefaultSize => new(80, 40);

        public static readonly (string Value, string En, string De)[] Kinds =
        {
            ("alt", "alt (alternatives)", "alt (Alternativen)"), ("opt", "opt (option)", "opt (Option)"), ("loop", "loop (loop)", "loop (Schleife)"),
            ("par", "par (parallel)", "par (parallel)"), ("break", "break (break)", "break (Abbruch)"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Operator", "Operator", "alt", Kinds),
            P.Int("operands", "Operands", "Operanden", 2, 1, 8),
            P.Text("guards", "Guards (one per operand)", "Bedingungen (eine pro Operand)", null),
            P.TextSize(),
        };

        /// <summary>
        /// The guards per operand: the <c>guards</c> parameter, else localised placeholders ("[condition]" and, for alt,
        /// "[else]" on the further operands; none for par).
        /// </summary>
        public static IReadOnlyList<string> Guards(SketchObject o, Lang lang)
        {
            if (o.Params.TryGetValue("guards", out var v) && v.Text is not null) return D.Lines(v.Text);
            var kind = o.Str("kind", "alt");
            if (kind == "par") return Array.Empty<string>();
            var cond = lang == Lang.De ? "[Bedingung]" : "[condition]";
            return Enumerable.Range(0, OperandsOf(o)).Select(i => i == 0 ? cond : kind == "alt" ? "[else]" : "").ToList();
        }

        public static int OperandsOf(SketchObject o) => Math.Max(1, Math.Min(8, o.Int("operands", 2)));

        /// <summary>Top and bottom (local y) of each operand.</summary>
        public static IReadOnlyList<(double Top, double Bottom)> Operands(SketchObject o)
        {
            var n = OperandsOf(o);
            var hh = o.Size.H / 2;
            var oh = o.Size.H / n;
            return Enumerable.Range(0, n).Select(i => (-hh + i * oh, -hh + (i + 1) * oh)).ToList();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.OutlineOpen) with { Tag = DiagTags.Outline } };
            var tag = o.Str("kind", "alt") + (string.IsNullOrWhiteSpace(o.Text) ? "" : " " + o.Text!.Trim());
            var size = D.Fit(Math.Min(TextSizeOf(o), 3.2), new[] { tag }, w * 0.6 - 3, h * 0.3, 1);
            var th = size * D.LineFactor + 1.2;
            var tw = Math.Min(w * 0.6, D.Width(tag, size) + 3);
            var notch = Math.Min(th * 0.45, tw * 0.3);
            // Pentagon tag: top edge, right edge down, cut corner, bottom edge back to the frame.
            list.Add(Prim.Polyline(new[] { new Vec2(-hw + tw, -hh), new Vec2(-hw + tw, -hh + th - notch), new Vec2(-hw + tw - notch, -hh + th), new Vec2(-hw, -hh + th) }, D.OutlineOpen) with { Tag = DiagTags.Divider });
            list.Add(D.Text(new Vec2(-hw + 1.2, -hh + th / 2), tag, size, DiagTags.Name, TextAnchor.Start, bold: true));
            var guards = Guards(o, ctx.Lang);
            var ops = Operands(o);
            var gs = Math.Min(size, 3.0);
            for (var i = 0; i < ops.Count; i++)
            {
                var (top, bottom) = ops[i];
                if (i > 0) list.Add(new LinePrim(new Vec2(-hw, top), new Vec2(hw, top), D.Dashed(D.Thin)) { Tag = DiagTags.Divider });
                if (i >= guards.Count || guards[i].Trim().Length == 0) continue;
                var g = guards[i].Trim();
                // First operand: the guard right of the tag; further operands: just below the separator.
                var x = i == 0 ? -hw + tw + 1.5 : -hw + 1.5;
                var avail = hw - 1 - x;
                var s = D.Fit(gs, new[] { g }, avail, Math.Max(0, (i == 0 ? th : bottom - top) - 0.5), 1);
                var y = i == 0 ? -hh + th / 2 : top + s * D.LineFactor / 2 + 0.6;
                if (y + s / 2 > bottom) y = (top + bottom) / 2;
                list.Add(D.Text(new Vec2(x, y), g, s, DiagTags.Guard, TextAnchor.Start));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in D.Choice(o, "diag.fragment.kind", new I18nText("Operator", "Operator"), "kind", "alt", "kind",
                Kinds.Select(k => (k.Value, new I18nText(k.En, k.De), (string?)null)).ToList())) yield return a;
            yield return new ContextAction("diag.fragment.addOperand", new I18nText("Add operand", "Operand hinzufügen"), IconIds.RowAdd, x =>
            {
                var n = OperandsOf(x);
                if (n >= 8) return x;
                var grown = D.GrowDown(x.WithParam("operands", n + 1), x.Size.H / n);
                var g = Guards(x, lang).ToList();
                while (g.Count < n) g.Add("");
                g.Add("[else]");
                return grown.WithParam("guards", string.Join("\n", g));
            }) { Group = "rows", IsEnabled = OperandsOf(o) < 8 };
            yield return new ContextAction("diag.fragment.removeOperand", new I18nText("Remove last operand", "Letzten Operand entfernen"), IconIds.RowRemove, x =>
            {
                var n = OperandsOf(x);
                if (n <= 1) return x;
                var g = Guards(x, lang).Take(n - 1).ToList();
                return D.GrowDown(x.WithParam("operands", n - 1).WithParam("guards", string.Join("\n", g)), -x.Size.H / n);
            }) { Group = "rows", IsEnabled = OperandsOf(o) > 1 };
        }
    }}
