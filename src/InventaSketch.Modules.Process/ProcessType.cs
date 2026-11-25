using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Connection point (nozzle) in design coordinates of the symbol. The id is the nozzle name (N1, N2 …, suction/discharge,
    /// tube/shell side) and stays stable across parameter changes and views, so line attachments survive them.
    /// </summary>
    internal readonly record struct Port(string Id, double X, double Y);

    /// <summary>
    /// Data-driven box type used for the equipment, fittings and instruments of the module: a P&amp;ID symbol drawn in design
    /// millimetres and stretched to the object size, optionally a block view for block flow diagrams, nozzles as
    /// <see cref="AnchorRole.Port"/> anchors (the same ids in every view), optional parameters and an equipment tag
    /// (e.g. "P-101") written outside the box, upright on the page.
    /// </summary>
    internal sealed class ProcessType : ObjectType
    {
        public const string TagKey = "tag";
        public const string ShowTagKey = "showTag";

        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly Size2 _design;
        private readonly Action<Pen, SketchObject> _draw;
        private IReadOnlyList<ViewDef>? _views;

        public ProcessType(string id, I18nText name, string category, double w, double h, Action<Pen, SketchObject> draw, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords; _design = new Size2(w, h); _draw = draw;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _views ??= Block
            ? new[] { ProcessViews.SymbolView, ProcessViews.BlockView }
            : new[] { ProcessViews.SymbolView };
        public override Size2 DefaultSize => Design(Normalize(new SketchObject { Type = _id }));
        public override bool PreferUniformScale => Uniform;
        public override IReadOnlyList<string> Companions => CompanionIds;

        /// <summary>Inspector edits keep the drawing scale like the type's own actions (<see cref="Adapt"/>).</summary>
        public override SketchObject OnParamChanged(SketchObject before, SketchObject after, string key) => Adapt(before, after);

        /// <summary>Type-specific parameters (tag parameters are added automatically when <see cref="TagPrefix"/> is set).</summary>
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Design box that depends on the parameters (supports, actuators); null = the constructor's design box.</summary>
        public Func<SketchObject, Size2>? DesignFor { get; init; }
        /// <summary>Nozzles in design coordinates (expects a normalised object).</summary>
        public Func<SketchObject, IReadOnlyList<Port>> Ports { get; init; } = _ => Array.Empty<Port>();
        /// <summary>Offers the block view of block flow diagrams (equipment, not inline fittings).</summary>
        public bool Block { get; init; }
        /// <summary>Equipment tag suggested by the "show tag" action (e.g. "P-101"); null = no external tag.</summary>
        public string? TagPrefix { get; init; }
        public bool Uniform { get; init; } = true;
        public bool Part { get; init; } = true;
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }
        public IReadOnlyList<string> CompanionIds { get; init; } = Array.Empty<string>();

        public override bool IsPart => Part;

        private static readonly ParamDef TagParam = P.Text(TagKey, "Tag (equipment number)", "Kennzeichen (Anlagenkennzeichen)", "");
        private static readonly ParamDef ShowTagParam = P.Flag(ShowTagKey, "Show tag", "Kennzeichen anzeigen", true);

        public override IReadOnlyList<ParamDef> Parameters => TagPrefix is null ? Params : Params.Concat(new[] { TagParam, ShowTagParam }).ToList();

        /// <summary>The object with every missing parameter set to its default, so drawings and ports read one value.</summary>
        public SketchObject Normalize(SketchObject o)
        {
            Dictionary<string, ParamValue>? d = null;
            foreach (var p in Parameters)
            {
                if (!p.Default.HasValue || o.Params.ContainsKey(p.Key)) continue;
                if (d is null)
                {
                    d = new Dictionary<string, ParamValue>();
                    foreach (var kv in o.Params) d[kv.Key] = kv.Value;
                }
                d[p.Key] = p.Default.Value;
            }
            return d is null ? o : o with { Params = d };
        }

        /// <summary>Design box of the object (may depend on parameters).</summary>
        public Size2 Design(SketchObject o) => DesignFor?.Invoke(Normalize(o)) ?? _design;

        /// <summary>Rotation that keeps text upright on the page after the object's transform.</summary>
        public static double UprightRotation(SketchObject o)
        {
            var t = o.Transform;
            return t.FlipX ^ t.FlipY ? 0 : -t.Rotation;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var n = Normalize(o);
            var design = Design(n);
            if (ViewOf(o) == ProcessViews.Block)
            {
                var block = new Pen(o.Size, o.Size, UprightRotation(o), ctx.Lang);
                DrawBlock(block, n, ctx.Lang);
                return block.Items;
            }
            var pen = new Pen(o.Size, design, UprightRotation(o), ctx.Lang);
            _draw(pen, n);
            if (TagPrefix is not null) AddTag(pen, n);
            return pen.Items;
        }

        /// <summary>Tag text when shown, else "".</summary>
        public static string TagOf(SketchObject o) => o.Flag(ShowTagKey, true) ? o.Str(TagKey, "").Trim() : "";

        /// <summary>
        /// Block of a block flow diagram: a rectangle filling the box with the tag (bold) and the equipment name, wrapped
        /// and fitted into the box.
        /// </summary>
        private void DrawBlock(Pen p, SketchObject o, Lang lang)
        {
            double hw = p.W / 2, hh = p.H / 2;
            p.Add(Prim.Rect(-hw, -hh, p.W, p.H, S.Sym, Math.Min(1.0, Math.Min(p.W, p.H) / 6)), ProcessTags.Body);
            var lines = new List<(string Text, bool Bold)>();
            var tag = TagPrefix is null ? "" : TagOf(o);
            if (tag.Length > 0) lines.Add((tag, true));
            foreach (var l in Wrap(_name.Get(lang), p.W > p.H * 1.6 ? 2 : 3)) lines.Add((l, false));
            var longest = lines.Max(l => l.Text.Length * (l.Bold ? 0.62 : 0.56));
            var size = Math.Min(2.5, Math.Min(p.W * 0.86 / Math.Max(0.5, longest), p.H * 0.8 / (lines.Count * 1.3)));
            size = Math.Max(0.1, size);
            var y0 = -(lines.Count - 1) * size * 1.3 / 2;
            for (var i = 0; i < lines.Count; i++)
            {
                var ts = new TextStyle { Size = size, Bold = lines[i].Bold, Anchor = TextAnchor.Middle, Baseline = TextBaseline.Middle };
                p.Add(Prim.Text(0, y0 + i * size * 1.3, lines[i].Text, ts, p.TextRotation), lines[i].Bold ? ProcessTags.Tag : ProcessTags.Label);
            }
        }

        /// <summary>Splits a name into at most <paramref name="maxLines"/> lines of similar length (word boundaries).</summary>
        internal static List<string> Wrap(string text, int maxLines)
        {
            var words = new List<string>();
            foreach (var w in (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // A lone separator ("/", "&", "-") stays with the word before it.
                if (words.Count > 0 && w.Length == 1 && !char.IsLetterOrDigit(w[0])) words[words.Count - 1] += " " + w;
                else words.Add(w);
            }
            var result = new List<string>();
            if (words.Count == 0) return result;
            var lines = Math.Min(maxLines, words.Count);
            var target = (double)text!.Length / lines;
            var cur = "";
            foreach (var w in words)
            {
                var next = cur.Length == 0 ? w : cur + " " + w;
                if (cur.Length > 0 && next.Length > target * 1.15 && result.Count < lines - 1)
                {
                    result.Add(cur);
                    cur = w;
                }
                else cur = next;
            }
            if (cur.Length > 0) result.Add(cur);
            return result;
        }

        /// <summary>
        /// Equipment tag outside the box, upright on the page: on the page side (below, right, above, left) with the fewest
        /// nozzles; beside a nozzle in the middle of that side the text moves off the connecting line.
        /// </summary>
        private void AddTag(Pen pen, SketchObject o)
        {
            var text = TagOf(o);
            if (text.Length == 0) return;
            const double size = 2.5, gap = 1.0;
            var lin = o.Matrix();
            var inv = lin.Invert();
            var origin = inv.Apply(Vec2.Zero);
            double hw = pen.W / 2, hh = pen.H / 2;
            var ports = LocalPorts(o).Select(p => p.P).ToList();
            // A nozzle belongs to the box side it is nearest to (nozzles on dished heads lie inside the box).
            Vec2 SideOf(Vec2 p) => hw - Math.Abs(p.X) < hh - Math.Abs(p.Y) ? new Vec2(Math.Sign(p.X), 0) : new Vec2(0, p.Y < 0 ? -1 : 1);
            bool OnSide(Vec2 p, Vec2 d) => SideOf(p) == d;
            Vec2 side = new(0, 1);
            var best = int.MaxValue;
            foreach (var pageDir in new[] { new Vec2(0, 1), new Vec2(1, 0), new Vec2(0, -1), new Vec2(-1, 0) })
            {
                var l = inv.Apply(pageDir) - origin;
                var d = Math.Abs(l.X) > Math.Abs(l.Y) ? new Vec2(Math.Sign(l.X), 0) : new Vec2(0, Math.Sign(l.Y));
                var count = ports.Count(p => OnSide(p, d));
                if (count < best) { best = count; side = d; }
            }
            var middleBlocked = ports.Any(p => OnSide(p, side) && (side.X != 0 ? Math.Abs(p.Y) < hh * 0.3 : Math.Abs(p.X) < hw * 0.3));
            var pd = lin.ApplyLinear(side);
            var horizontalOut = Math.Abs(pd.X) > Math.Abs(pd.Y) + 1e-6;
            var anchor = horizontalOut ? (pd.X > 0 ? TextAnchor.Start : TextAnchor.End) : TextAnchor.Middle;
            var ext = Math.Abs(side.X) * hw + Math.Abs(side.Y) * hh;
            var at = side * (ext + gap + (anchor == TextAnchor.Middle ? size / 2 : 0));
            if (middleBlocked)
            {
                // Shift along the side, away from the line leaving the middle nozzle.
                var along = new Vec2(-side.Y, side.X);
                var pa = lin.ApplyLinear(along);
                if (pa.X < -1e-6 || (Math.Abs(pa.X) < 1e-6 && pa.Y < 0)) along = -along;
                if (anchor == TextAnchor.Middle)
                {
                    at += along * gap;
                    anchor = lin.ApplyLinear(along).X >= 0 ? TextAnchor.Start : TextAnchor.End;
                }
                else at += along * (size * 0.9);
            }
            pen.Add(Prim.Text(at.X, at.Y, text, new TextStyle { Size = size, Anchor = anchor, Baseline = TextBaseline.Middle }, pen.TextRotation), ProcessTags.Tag);
        }

        /// <summary>
        /// Nozzles in local coordinates of the object, with the same ids in every view. The block view has no inner nozzles:
        /// a nozzle inside the symbol box (on a dished head, say) moves to the nearest edge of the block.
        /// </summary>
        public IEnumerable<(string Id, Vec2 P)> LocalPorts(SketchObject o)
        {
            var n = Normalize(o);
            var d = Design(n);
            double sx = o.Size.W / d.W, sy = o.Size.H / d.H;
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var block = ViewOf(o) == ProcessViews.Block;
            foreach (var p in Ports(n))
            {
                var q = new Vec2(p.X * sx, p.Y * sy);
                if (block && Math.Abs(Math.Abs(q.X) - hw) > 1e-6 && Math.Abs(Math.Abs(q.Y) - hh) > 1e-6)
                    q = hw - Math.Abs(q.X) < hh - Math.Abs(q.Y) ? new Vec2(Math.Sign(q.X) * hw, q.Y) : new Vec2(q.X, q.Y < 0 ? -hh : hh);
                yield return (p.Id, q);
            }
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var p in LocalPorts(o)) yield return Anchor.Point(AnchorRole.Port, p.P, p.Id);
        }

        /// <summary>
        /// Nozzles and pipe ports take a process line, instrument bubbles and actuator signal ports (S) a signal line (build
        /// "+", <see cref="Building"/>). The drive shaft of a motor is a mechanical coupling, no line.
        /// </summary>
        public override PortConnection? Connection(SketchObject o, Anchor port)
        {
            if (port.Id is null || port.Id == "shaft") return null;
            if (_id == Instruments.BubbleId || port.Id == "S" && (_id == "process.valve.control" || _id == "process.valve.threeway"))
                return PortConnection.Of(PortMedia.Instrument, "process.line.signal");
            return PortConnection.Of(PortMedia.Process, "process.line");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var n = Normalize(o);
            if (Extra is not null)
                foreach (var a in Extra(n))
                    yield return a.Input is null && a.ApplyInDocument is null ? a with { Apply = x => Adapt(x, a.Apply(Normalize(x))) } : a;
            if (TagPrefix is null) yield break;
            var prefix = TagPrefix;
            yield return new ContextAction("process.tag.show", P.T("Show tag", "Kennzeichen anzeigen"), IconIds.Text, x =>
            {
                if (TagOf(x).Length > 0) return x.WithParam(ShowTagKey, false);
                var y = x.WithParam(ShowTagKey, true);
                return y.Str(TagKey, "").Trim().Length == 0 ? y.WithParam(TagKey, prefix) : y;
            }) { IsActive = TagOf(n).Length > 0, Group = "label", ParamKeys = new[] { ShowTagKey } };
            yield return ContextAction.WithInput("process.tag.set", P.T("Tag …", "Kennzeichen …"), IconIds.Text,
                P.T("Tag (e.g. " + prefix + ")", "Kennzeichen (z. B. " + prefix + ")"), o.Str(TagKey, ""),
                (x, t) => x.WithParam(TagKey, t.Trim()).WithParam(ShowTagKey, true)) with { Group = "label", ParamKeys = new[] { TagKey } };
        }

        /// <summary>
        /// Keeps the drawing scale when an edit changes the design box (a support or an actuator added): the object size
        /// follows the design box, so symbols are never squeezed.
        /// </summary>
        public SketchObject Adapt(SketchObject before, SketchObject after)
        {
            var a = Design(before);
            var b = Design(after);
            if (Math.Abs(a.W - b.W) < 1e-9 && Math.Abs(a.H - b.H) < 1e-9) return after;
            return after with { Size = new Size2(b.W * before.Size.W / a.W, b.H * before.Size.H / a.H) };
        }
    }
}
