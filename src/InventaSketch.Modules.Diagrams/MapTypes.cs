using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Ishikawa (fishbone, cause-and-effect) diagram as one element: the effect in a head box on the right (object text), a
    /// spine with an arrow into the head and one diagonal bone per category, alternately above and below. Categories are the
    /// lines of <c>categories</c>; a line may list causes after a colon, separated by semicolons
    /// (<c>Machine: wear; vibration</c>), drawn as short ribs on the bone. While <c>categories</c> is not set, the six
    /// classic categories are shown in the user's language.
    /// </summary>
    public sealed class IshikawaType : DiagBoxType
    {
        public const int MaxCategories = 8;

        public IshikawaType() : base("diag.ishikawa", new I18nText("Ishikawa diagram (fishbone)", "Ishikawa-Diagramm (Fischgräte)"), DiagCategories.Maps,
            "ishikawa", "fishbone", "fischgräte", "fischgrätendiagramm", "cause and effect", "ursache-wirkung", "ursache wirkung", "6m", "root cause", "quality", "qualität") { }

        public override Size2 DefaultSize => new(140, 70);
        protected override I18nText Placeholder => new("Effect", "Wirkung");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("categories", "Categories (one per line, causes after \":\" separated by \";\")", "Kategorien (eine pro Zeile, Ursachen nach \":\" mit \";\" getrennt)", null),
            P.Bool("frames", "Category frames", "Kategorierahmen", true),
            P.TextSize(),
        };

        /// <summary>The six classic categories (6M) in the given language.</summary>
        public static string DefaultCategories(Lang lang) => lang == Lang.De
            ? "Mensch\nMaschine\nMethode\nMaterial\nMessung\nMitwelt"
            : "Man\nMachine\nMethod\nMaterial\nMeasurement\nEnvironment";

        private static string Raw(SketchObject o, Lang lang)
            => o.Params.TryGetValue("categories", out var v) && v.Text is not null ? v.Text : DefaultCategories(lang);

        /// <summary>Categories with their causes (at most <see cref="MaxCategories"/>).</summary>
        public static IReadOnlyList<(string Name, IReadOnlyList<string> Causes)> Categories(SketchObject o, Lang lang)
            => D.Lines(Raw(o, lang)).Take(MaxCategories).Select(l =>
            {
                var i = l.IndexOf(':');
                if (i < 0) return (l.Trim(), (IReadOnlyList<string>)Array.Empty<string>());
                var causes = l.Substring(i + 1).Split(';').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
                return (l.Substring(0, i).Trim(), (IReadOnlyList<string>)causes);
            }).ToList();

        private readonly struct FishLayout
        {
            public FishLayout(SketchObject o)
            {
                W = o.Size.W; H = o.Size.H;
                HeadW = Math.Max(Math.Min(W * 0.2, 40), Math.Min(10, W * 0.3));
                HeadH = Math.Min(H * 0.36, Math.Max(10, H * 0.25));
                SpineStart = -W / 2 + 0.5;
                SpineEnd = W / 2 - HeadW;
                LabelH = Math.Min(H * 0.22, o.Num("textSize", D.TextSize) * D.LineFactor + 2);
            }

            public double W { get; }
            public double H { get; }
            public double HeadW { get; }
            public double HeadH { get; }
            public double SpineStart { get; }
            public double SpineEnd { get; }
            public double LabelH { get; }
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            var g = new FishLayout(o);
            var hx = o.Size.W / 2 - g.HeadW / 2;
            yield return ("right", new Vec2(o.Size.W / 2, 0));
            yield return ("top", new Vec2(hx, -g.HeadH / 2));
            yield return ("bottom", new Vec2(hx, g.HeadH / 2));
            yield return ("left", new Vec2(g.SpineStart, 0));
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var g = new FishLayout(o);
            double hw = g.W / 2, hh = g.H / 2;
            var ts = TextSizeOf(o);
            var list = new List<Primitive>
            {
                Prim.Rect(hw - g.HeadW, -g.HeadH / 2, g.HeadW, g.HeadH, D.Outline) with { Tag = DiagTags.Outline },
                new LinePrim(new Vec2(g.SpineStart, 0), new Vec2(g.SpineEnd, 0), Style.Visible with { StrokeWidth = 0.6, ArrowEnd = ArrowHead.Triangle, ArrowSize = D.HeadSize }) { Tag = DiagTags.Main },
            };
            D.CenterText(list, Label(o, ctx), ts, new Vec2(hw - g.HeadW / 2, 0), g.HeadW - 2, g.HeadH - 1.5, bold: true);

            var cats = Categories(o, ctx.Lang);
            if (cats.Count == 0) return list;
            var nTop = (cats.Count + 1) / 2;
            var cw = (g.SpineEnd - g.SpineStart) / nTop;
            var boneH = Math.Max(1, hh - g.LabelH);
            var dx = Math.Min(cw * 0.45, boneH * 0.6);
            var frames = o.Flag("frames", true);
            var boneStyle = Style.Visible with { StrokeWidth = D.Stroke };
            var causeSize = Math.Min(ts * 0.8, 3.0);
            for (var i = 0; i < cats.Count; i++)
            {
                var top = i < nTop;
                var j = top ? i : i - nTop;
                var sy = top ? -1 : 1;
                var lx = g.SpineStart + (j + 0.5) * cw;
                var end = new Vec2(lx, sy * (hh - g.LabelH));
                var attach = new Vec2(lx + dx, 0);
                list.Add(new LinePrim(end, attach, boneStyle) { Tag = DiagTags.Outline });

                // Category label (in a frame) at the outer end of the bone.
                var (name, causes) = cats[i];
                var ly = sy * (hh - g.LabelH / 2);
                var size = D.Fit(ts, new[] { name }, cw - 3, g.LabelH - 1.2, 1);
                if (frames && name.Length > 0)
                {
                    var fw = Math.Min(cw - 0.6, D.Width(name, size) + 2.4);
                    var fh = Math.Max(0.5, g.LabelH - 0.6);
                    list.Add(Prim.Rect(lx - fw / 2, ly - fh / 2, fw, fh, D.Outline) with { Tag = DiagTags.Outline });
                }
                if (name.Length > 0) list.Add(D.Text(new Vec2(lx, ly), name, size, DiagTags.Category, bold: true));

                // Causes: short horizontal ribs leaving the bone to the left, the text beside the rib away from the bone.
                for (var m = 0; m < causes.Count; m++)
                {
                    var t = (m + 1.0) / (causes.Count + 1);
                    var p = Vec2.Lerp(end, attach, t);
                    var rl = cw * 0.5;
                    list.Add(new LinePrim(p, new Vec2(p.X - rl, p.Y), D.Thin) { Tag = DiagTags.Outline });
                    var spacing = boneH / (causes.Count + 1);
                    var cs = D.Fit(causeSize, new[] { causes[m] }, rl - 0.6, Math.Max(0.5, spacing / D.LineFactor), 1);
                    list.Add(D.Text(new Vec2(p.X - rl + 0.3, p.Y - sy * (cs * 0.75)), causes[m], cs, DiagTags.Cause, TextAnchor.Start));
                }
            }
            return list;
        }

        private static SketchObject SetCategories(SketchObject x, Lang lang, Func<List<string>, List<string>> edit)
            => x.WithParam("categories", string.Join("\n", edit(D.Lines(Raw(x, lang)).ToList())));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var cats = Categories(o, lang);
            var n = cats.Count;
            yield return new ContextAction("diag.ishikawa.bones.more", new I18nText("Add category", "Kategorie hinzufügen"), IconIds.CountMore,
                x => SetCategories(x, lang, l =>
                {
                    if (l.Count >= MaxCategories) return l;
                    l.Add((lang == Lang.De ? "Kategorie " : "Category ") + D.Inv(l.Count + 1));
                    return l;
                })) { Group = "categories", IsEnabled = n < MaxCategories };
            yield return new ContextAction("diag.ishikawa.bones.fewer", new I18nText("Remove category", "Kategorie entfernen"), IconIds.CountFewer,
                x => SetCategories(x, lang, l =>
                {
                    if (l.Count > 1) l.RemoveAt(l.Count - 1);
                    return l;
                })) { Group = "categories", IsEnabled = n > 1 };
            yield return ContextAction.WithInput("diag.ishikawa.cause", new I18nText("Add cause", "Ursache hinzufügen"), IconIds.Insert,
                new I18nText("Category number: cause (e.g. 2: wear)", "Kategorienummer: Ursache (z. B. 2: Verschleiß)"), "1: ", (x, s) => AddCause(x, lang, s)) with { Group = "categories" };
            for (var i = 0; i < n; i++)
            {
                var k = i;
                yield return ContextAction.WithInput("diag.ishikawa.rename." + D.Inv(k + 1),
                    new I18nText("Rename category " + D.Inv(k + 1), "Kategorie " + D.Inv(k + 1) + " umbenennen"), IconIds.Name,
                    new I18nText("Category name", "Name der Kategorie"), cats[k].Name, (x, s) => SetCategories(x, lang, l =>
                    {
                        if (k >= l.Count) return l;
                        var c = l[k].IndexOf(':');
                        l[k] = s.Trim().Replace(":", "") + (c >= 0 ? l[k].Substring(c) : "");
                        return l;
                    })) with { Group = "rename" };
            }
            yield return D.Toggle(o, "diag.ishikawa.frames", new I18nText("Category frames", "Kategorierahmen"), "frames", true);
        }

        /// <summary>Adds a cause to category n from input "n: cause" (a bare text goes to the first category).</summary>
        public static SketchObject AddCause(SketchObject x, Lang lang, string input)
        {
            var s = input.Trim();
            var index = 0;
            var colon = s.IndexOf(':');
            if (colon > 0 && int.TryParse(s.Substring(0, colon).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n))
            {
                index = n - 1;
                s = s.Substring(colon + 1).Trim();
            }
            s = s.Replace(";", ",");
            if (s.Length == 0) return x;
            return SetCategories(x, lang, l =>
            {
                if (index < 0 || index >= l.Count) throw new FormatException("No category " + (index + 1));
                l[index] = l[index].Contains(':') ? l[index].TrimEnd() + (l[index].TrimEnd().EndsWith(":", StringComparison.Ordinal) ? " " : "; ") + s : l[index].TrimEnd() + ": " + s;
                return l;
            });
        }
    }

    /// <summary>Mind map structure: links join nodes without a direction; a map is laid out from the node it is run on.</summary>
    public static class MindMap
    {
        public const string NodeType = "diag.mindmap.node";
        public const string LinkType = "diag.mindmap.link";

        /// <summary>Nodes joined to <paramref name="id"/> by a mind map link (either direction).</summary>
        public static IEnumerable<string> Neighbours(SketchDocument doc, string id)
        {
            foreach (var line in doc.Objects.Values)
            {
                if (line.Type != LinkType || line.Attach is null) continue;
                var ends = line.Attach.Where(a => a is not null && doc.Get(a.ObjectId) is { IsPointGeometry: false }).Select(a => a!.ObjectId).ToList();
                if (ends.Count < 2 || ends[0] == ends[ends.Count - 1]) continue;
                if (ends[0] == id) yield return ends[ends.Count - 1];
                else if (ends[ends.Count - 1] == id) yield return ends[0];
            }
        }

        /// <summary>New centres of the map around <paramref name="rootId"/>: "sides" (both sides), "right" or "radial".</summary>
        public static Dictionary<string, Vec2>? Layout(SketchDocument doc, string rootId, string mode)
        {
            var tree = TreeLayout.Build(doc, rootId, id => Neighbours(doc, id), mode == "radial" ? TreeLayout.Angle : d => d.Y);
            if (tree is null || tree.Children.Count == 0) return null;
            return mode switch
            {
                "radial" => TreeLayout.Radial(tree, 6),
                "right" => TreeLayout.Sideways(tree, 1, 12, 4),
                _ => TreeLayout.Sideways(tree, 2, 12, 4),
            };
        }
    }

    /// <summary>
    /// Mind map node: the idea is the object text; shapes rounded box (default), ellipse, box or a line under the text
    /// (classic branch style). "Arrange map" lays out every node reachable over mind map links around this one.
    /// </summary>
    public sealed class MindMapNodeType : DiagBoxType
    {
        public MindMapNodeType() : base(MindMap.NodeType, new I18nText("Mind map node", "Mindmap-Knoten"), DiagCategories.Maps,
            "mind map", "mindmap", "idea", "idee", "brainstorming", "topic", "thema", "branch", "zweig", "node", "knoten") { }

        public override Size2 DefaultSize => new(30, 12);
        protected override I18nText Placeholder => new("Idea", "Idee");

        public static readonly (string Value, string En, string De)[] Shapes =
        {
            ("rounded", "Rounded box", "Abgerundetes Rechteck"), ("ellipse", "Ellipse", "Ellipse"), ("box", "Box", "Rechteck"), ("line", "Underlined text", "Unterstrichener Text"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("shape", "Shape", "Form", "rounded", Shapes),
            P.Bool("bold", "Bold text", "Fette Schrift", false),
            P.TextSize(),
        };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            switch (o.Str("shape", "rounded"))
            {
                case "ellipse":
                    return D.EllipsePorts(o.Size);
                case "line":
                {
                    double hw = o.Size.W / 2, hh = o.Size.H / 2;
                    var y = hh - 0.4;
                    return new[] { ("left", new Vec2(-hw, y)), ("right", new Vec2(hw, y)), ("top", new Vec2(0, -hh)), ("bottom", new Vec2(0, y)) };
                }
                default:
                    return D.RectPorts(o.Size);
            }
        }

        private static IReadOnlyList<Primitive> ShapePreview(string shape) => shape switch
        {
            "ellipse" => new Primitive[] { Prim.Ellipse(0, 0, 3, 1.8, D.OutlineOpen) },
            "box" => new Primitive[] { Prim.Rect(-3, -1.8, 6, 3.6, D.OutlineOpen) },
            "line" => new Primitive[] { new LinePrim(new Vec2(-3, 1.5), new Vec2(3, 1.5), D.OutlineOpen) },
            _ => new Primitive[] { D.RoundedRect(-3, -1.8, 6, 3.6, 1.2, D.OutlineOpen) },
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var bold = o.Flag("bold", false);
            var outline = bold ? D.Outline with { StrokeWidth = 0.5 } : D.Outline;
            var list = new List<Primitive>();
            var text = Label(o, ctx);
            switch (o.Str("shape", "rounded"))
            {
                case "ellipse":
                    list.Add(Prim.Ellipse(0, 0, hw, hh, outline) with { Tag = DiagTags.Outline });
                    D.CenterText(list, text, TextSizeOf(o), Vec2.Zero, w * 0.72, h * 0.65, bold: bold);
                    break;
                case "box":
                    list.Add(Prim.Rect(-hw, -hh, w, h, outline) with { Tag = DiagTags.Outline });
                    D.CenterText(list, text, TextSizeOf(o), Vec2.Zero, w - 2, h - 1.5, bold: bold);
                    break;
                case "line":
                    list.Add(new LinePrim(new Vec2(-hw, hh - 0.4), new Vec2(hw, hh - 0.4), outline with { Fill = null }) { Tag = DiagTags.Outline });
                    D.CenterText(list, text, TextSizeOf(o), new Vec2(0, -0.4), w - 1, h - 1.6, bold: bold);
                    break;
                default:
                    list.Add(D.RoundedRect(-hw, -hh, w, h, Math.Min(3, Math.Min(w, h) * 0.3), outline) with { Tag = DiagTags.Outline });
                    D.CenterText(list, text, TextSizeOf(o), Vec2.Zero, w - 3, h - 1.5, bold: bold);
                    break;
            }
            return list;
        }

        /// <summary>"Arrange map" is offered greyed out while the node has no branch.</summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var linked = MindMap.Neighbours(ctx.Doc, o.Id).Any();
            return base.Actions(o, ctx).Select(a => a.Choice == ArrangeMenu ? a with { IsEnabled = linked } : a);
        }

        private static readonly ActionChoice ArrangeMenu = new("diag.mindmap.arrange", new I18nText("Arrange map", "Mindmap anordnen")) { IsMenu = true, BarIcon = IconIds.DistributeH };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in D.Choice(o, "diag.mindmap.shape", new I18nText("Shape", "Form"), "shape", "rounded", "shape",
                Shapes.Select(s => (s.Value, new I18nText(s.En, s.De), (string?)null)).ToList(), ShapePreview)) yield return a;
            yield return D.Toggle(o, "diag.mindmap.bold", new I18nText("Bold", "Fett"), "bold", false);
            foreach (var (mode, en, de) in new[] { ("sides", "Both sides", "Beidseitig"), ("right", "To the right", "Nach rechts"), ("radial", "Radial", "Radial") })
            {
                var m = mode;
                yield return ContextAction.InDocument(ArrangeMenu.Key + ActionChoices.Separator + m, new I18nText(en, de), IconIds.Option,
                    (doc, x, ctx) => MindMap.Layout(doc, x.Id, m) is { } centres
                        ? TreeLayout.Apply(doc, centres, ctx.Registry ?? Owner, MindMap.LinkType, m == "radial" ? TreeLayout.Facing : TreeLayout.Horizontal)
                        : doc) with { Choice = ArrangeMenu, Group = "choice:" + ArrangeMenu.Key };
            }
        }
    }

    /// <summary>
    /// Mind map branch: a smooth curve (cubic, leaving and entering along the main direction between its ends) or a
    /// straight line; no end notations.
    /// </summary>
    public sealed class MindMapLinkType : DiagConnectorType
    {
        public MindMapLinkType() : base(MindMap.LinkType, new I18nText("Mind map branch", "Mindmap-Zweig"), DiagCategories.Maps,
            "mind map", "mindmap", "branch", "zweig", "link", "verbindung", "curve", "kurve") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("curved", "Curved", "Geschwungen", true),
            P.Num("width", "Line width", "Linienbreite", 0.5, 0.1, 3, "mm", 0.05),
            P.TextSize(),
        };

        protected override DiagEnd StartEnd(SketchObject o) => DiagEnd.None;
        protected override DiagEnd EndEnd(SketchObject o) => DiagEnd.None;

        /// <summary>Control points of the branch curve from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static (Vec2 C1, Vec2 C2) Controls(Vec2 a, Vec2 b)
        {
            var d = b - a;
            return Math.Abs(d.X) >= Math.Abs(d.Y)
                ? (new Vec2(a.X + d.X / 2, a.Y), new Vec2(b.X - d.X / 2, b.Y))
                : (new Vec2(a.X, a.Y + d.Y / 2), new Vec2(b.X, b.Y - d.Y / 2));
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var style = Style.ThinLine with { StrokeWidth = o.Num("width", 0.5) };
            var list = new List<Primitive>();
            var path = Path(o, p);
            if (o.Flag("curved", true) && path.Count == 2)
            {
                var (c1, c2) = Controls(path[0], path[1]);
                list.Add(Prim.Path(new[] { PathSeg.Move(path[0]), PathSeg.Cubic(c1, c2, path[1]) }, style) with { Tag = DiagTags.Main });
            }
            else if (path.Count == 2) list.Add(new LinePrim(path[0], path[1], style) { Tag = DiagTags.Main });
            else list.Add(Prim.Polyline(path, style) with { Tag = DiagTags.Main });
            Labels(list, o, ctx, path);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.mindmap.curved", new I18nText("Curved", "Geschwungen"), "curved", true);
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
        }
    }
}
