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
            P.TextSize(),
        };

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
            list.Add(new LinePrim(new Vec2(0, -hh + head), new Vec2(0, bottom), D.Dashed(D.Thin)) { Tag = DiagTags.Main });
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
            P.TextSize(),
        };

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
            if (path.Count == 4 && Math.Abs(path[0].X - path[3].X) < 0.5)
            {
                // Self message: label to the right of the loop.
                var mid = Vec2.Lerp(path[1], path[2], 0.5);
                list.AddRange(D.SideLabel(text, mid, new Vec2(1, 0), LabelSize(o)));
                return;
            }
            var (at, dir) = D.At(path, 0.5);
            list.AddRange(D.SideLabel(text, at, D.LabelSide(dir), LabelSize(o)));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.msg.sync", new I18nText("Synchronous", "Synchron"), IconIds.ArrowEnd, "kind", "sync", "sync", "kind");
            yield return D.SetChoice(o, "diag.msg.async", new I18nText("Asynchronous", "Asynchron"), IconIds.ArrowEnd, "kind", "async", "sync", "kind");
            yield return D.SetChoice(o, "diag.msg.return", new I18nText("Reply", "Antwort"), IconIds.ArrowEnd, "kind", "return", "sync", "kind");
            yield return D.SetChoice(o, "diag.msg.create", new I18nText("Create", "Erzeugen"), IconIds.ArrowEnd, "kind", "create", "sync", "kind");
            yield return new ContextAction("diag.swap", new I18nText("Swap direction", "Richtung umkehren"), IconIds.Swap, D.Reverse) { Group = "direction" };
        }
    }
}
