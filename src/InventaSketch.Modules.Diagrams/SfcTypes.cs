using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>Shared keywords of the sequential function chart elements.</summary>
    internal static class Sfc
    {
        public static readonly string[] Keywords = { "sfc", "grafcet", "ablaufsprache", "ablaufsteuerung", "sequential function chart", "schrittkette", "plc", "sps" };

        public static string[] With(params string[] more) => Keywords.Concat(more).ToArray();
    }

    /// <summary>
    /// Step of a sequential function chart (drawn independently in the style of GRAFCET, IEC 60848): a square with the
    /// step number (object text); the initial step has a double frame. <c>active</c> marks the step with a token dot.
    /// Links dock at the top and bottom; actions at the right edge.
    /// </summary>
    public sealed class SfcStepType : DiagBoxType
    {
        private readonly bool _initial;

        public SfcStepType(bool initial) : base(initial ? "diag.sfc.step.initial" : "diag.sfc.step",
            initial ? new I18nText("Initial step (SFC)", "Anfangsschritt (SFC)") : new I18nText("Step (SFC)", "Schritt (SFC)"), DiagCategories.Sfc,
            Sfc.With(initial ? new[] { "initial step", "anfangsschritt", "initialschritt", "start", "double frame", "doppelrahmen" } : new[] { "step", "schritt", "square", "quadrat" }))
        {
            _initial = initial;
        }

        public override Size2 DefaultSize => new(10, 10);
        public override bool PreferUniformScale => true;
        protected override I18nText Placeholder => _initial ? new("0", "0") : new("1", "1");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("active", "Active (token)", "Aktiv (Marke)", false),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            var g = Math.Min(1.0, Math.Min(w, h) * 0.12);
            if (_initial) list.Add(Prim.Rect(-hw + g, -hh + g, Math.Max(0.1, w - 2 * g), Math.Max(0.1, h - 2 * g), D.OutlineOpen) with { Tag = DiagTags.Outline });
            var inner = _initial ? 2 * g : 0;
            var active = o.Flag("active", false);
            var dot = Math.Min(w, h) * 0.08;
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2(0, active ? -dot * 1.2 : 0), w - inner - 1.5, h - inner - 1.5 - (active ? dot * 2.4 : 0));
            if (active) list.Add(Prim.Circle(0, hh - inner / 2 - 0.6 - dot * 1.4, dot, D.Solid) with { Tag = DiagTags.Marker });
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.sfc.active", new I18nText("Active (token)", "Aktiv (Marke)"), "active", false);
        }
    }

    /// <summary>
    /// Transition: a short thick bar across the link line, which runs through the box from the top to the bottom port. The
    /// receptivity (condition, object text) is written to the right of the bar, outside the box.
    /// </summary>
    public sealed class SfcTransitionType : DiagBoxType
    {
        public SfcTransitionType() : base("diag.sfc.transition", new I18nText("Transition (SFC)", "Transition (SFC)"), DiagCategories.Sfc,
            Sfc.With("transition", "übergang", "receptivity", "übergangsbedingung", "condition", "bedingung", "weiterschaltbedingung")) { }

        public override Size2 DefaultSize => new(8, 6);

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("bottom", new Vec2(0, hh));
            yield return ("left", new Vec2(-hw, 0));
            yield return ("right", new Vec2(hw, 0));
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var list = new List<Primitive>
            {
                new LinePrim(new Vec2(0, -hh), new Vec2(0, hh), Style.ThinLine with { StrokeWidth = D.LineWidth }) { Tag = DiagTags.Outline },
                new LinePrim(new Vec2(-hw, 0), new Vec2(hw, 0), Style.Visible with { StrokeWidth = Math.Max(0.6, Math.Min(1.0, o.Size.H * 0.14)) }) { Tag = DiagTags.Marker },
            };
            var lines = D.Lines(o.Text);
            var s = TextSizeOf(o);
            var rowH = s * D.LineFactor;
            var y0 = -(lines.Count - 1) * rowH / 2;
            for (var i = 0; i < lines.Count; i++) list.Add(D.Text(new Vec2(hw + 1.2, y0 + i * rowH), lines[i], s, DiagTags.Label, TextAnchor.Start));
            return list;
        }
    }

    /// <summary>
    /// Action box attached to the right of a step: a rectangle with the action text; an optional <c>qualifier</c> (N, S, R,
    /// D, L, P … as in IEC 61131-3 SFC) gets its own cell on the left.
    /// </summary>
    public sealed class SfcActionType : DiagBoxType
    {
        public SfcActionType() : base("diag.sfc.action", new I18nText("Action (SFC)", "Aktion (SFC)"), DiagCategories.Sfc,
            Sfc.With("action", "aktion", "befehl", "command", "qualifier", "bestimmungszeichen", "output", "ausgang")) { }

        public override Size2 DefaultSize => new(30, 10);
        protected override I18nText Placeholder => new("Action", "Aktion");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("qualifier", "Qualifier (N, S, R, D, L, P …)", "Bestimmungszeichen (N, S, R, D, L, P …)", null),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            var q = o.Str("qualifier", "").Trim();
            var qw = q.Length == 0 ? 0 : Math.Min(Math.Max(6, q.Length * TextSizeOf(o) * D.CharWidth + 2), w * 0.35);
            if (qw > 0)
            {
                list.Add(new LinePrim(new Vec2(-hw + qw, -hh), new Vec2(-hw + qw, hh), D.OutlineOpen) { Tag = DiagTags.Divider });
                D.CenterText(list, q, TextSizeOf(o), new Vec2(-hw + qw / 2, 0), qw - 1, h - 1.5, DiagTags.Marker);
            }
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), new Vec2(qw / 2, 0), w - qw - 2, h - 1.5);
            return list;
        }
    }

    /// <summary>
    /// Branch bar of a sequential function chart: <c>parallel</c> (simultaneous sequences, double line) or
    /// <c>alternative</c> (selection of sequences, single line), with <c>branches</c> docking points on both sides
    /// (<c>b1.top</c>, <c>b1.bottom</c> …) plus <c>top</c> and <c>bottom</c> in the middle.
    /// </summary>
    public sealed class SfcBranchType : DiagBoxType
    {
        public SfcBranchType() : base("diag.sfc.branch", new I18nText("Branch bar (SFC)", "Verzweigungsbalken (SFC)"), DiagCategories.Sfc,
            Sfc.With("branch", "verzweigung", "parallel", "simultaneous", "gleichzeitig", "alternative", "selection", "auswahl", "double bar", "doppelstrich", "join", "zusammenführung")) { }

        public override Size2 DefaultSize => new(40, 3);
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Branch", "Verzweigung", "parallel", ("parallel", "Parallel (double line)", "Parallel (Doppellinie)"), ("alternative", "Alternative (single line)", "Alternativ (Einzellinie)")),
            P.Int("branches", "Branches", "Zweige", 2, 2, 8),
        };

        private static int Branches(SketchObject o) => Math.Max(2, Math.Min(8, o.Int("branches", 2)));

        /// <summary>X positions of the branches (local coordinates), inset from the bar ends.</summary>
        public static IReadOnlyList<double> BranchX(SketchObject o)
        {
            var n = Branches(o);
            var hw = o.Size.W / 2;
            var inset = Math.Min(3, o.Size.W * 0.08);
            return Enumerable.Range(0, n).Select(i => -hw + inset + i * (o.Size.W - 2 * inset) / (n - 1)).ToList();
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            var hh = o.Size.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("bottom", new Vec2(0, hh));
            var xs = BranchX(o);
            for (var i = 0; i < xs.Count; i++)
            {
                yield return ("b" + D.Inv(i + 1) + ".top", new Vec2(xs[i], -hh));
                yield return ("b" + D.Inv(i + 1) + ".bottom", new Vec2(xs[i], hh));
            }
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var list = new List<Primitive>();
            var style = Style.Visible with { StrokeWidth = D.Stroke };
            if (o.Str("kind", "parallel") == "alternative")
                list.Add(new LinePrim(new Vec2(-hw, 0), new Vec2(hw, 0), style) { Tag = DiagTags.Outline });
            else
            {
                var d = Math.Min(0.7, hh * 0.5);
                list.Add(new LinePrim(new Vec2(-hw, -d), new Vec2(hw, -d), style) { Tag = DiagTags.Outline });
                list.Add(new LinePrim(new Vec2(-hw, d), new Vec2(hw, d), style) { Tag = DiagTags.Outline });
            }
            return list;
        }

        private static IReadOnlyList<Primitive> KindPreview(string kind)
            => kind == "alternative"
                ? new Primitive[] { new LinePrim(new Vec2(-3, 0), new Vec2(3, 0), D.OutlineOpen) }
                : new Primitive[] { new LinePrim(new Vec2(-3, -0.6), new Vec2(3, -0.6), D.OutlineOpen), new LinePrim(new Vec2(-3, 0.6), new Vec2(3, 0.6), D.OutlineOpen) };

        /// <summary>One branch more or fewer; the bar grows or shrinks by one branch spacing about its centre.</summary>
        private static SketchObject Resize(SketchObject x, int delta)
        {
            var n = Branches(x);
            var m = Math.Max(2, Math.Min(8, n + delta));
            if (m == n) return x;
            var inset = Math.Min(3, x.Size.W * 0.08);
            var pitch = (x.Size.W - 2 * inset) / (n - 1);
            return x.WithParam("branches", m) with { Size = new Size2(Math.Max(6, x.Size.W + (m - n) * pitch), x.Size.H) };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in D.Choice(o, "diag.sfc.kind", new I18nText("Branch", "Verzweigung"), "kind", "parallel", "kind", new (string, I18nText, string?)[]
            {
                ("parallel", new I18nText("Parallel", "Parallel"), null),
                ("alternative", new I18nText("Alternative", "Alternativ"), null),
            }, KindPreview)) yield return a;
            var n = Branches(o);
            yield return new ContextAction("diag.sfc.branches.more", new I18nText("Add branch", "Zweig hinzufügen"), IconIds.CountMore, x => Resize(x, 1)) { Group = "branches", IsEnabled = n < 8 };
            yield return new ContextAction("diag.sfc.branches.fewer", new I18nText("Remove branch", "Zweig entfernen"), IconIds.CountFewer, x => Resize(x, -1)) { Group = "branches", IsEnabled = n > 2 };
        }
    }

    /// <summary>
    /// Link of a sequential function chart: a plain line, orthogonal by default, normally from top to bottom. A link whose
    /// end lies above its start (a loop back from the last transition to an earlier step) runs around on the left at
    /// <c>clearance</c> beyond its ends and carries a direction arrow in the middle of its rising run, as GRAFCET draws
    /// upward links.
    /// </summary>
    public sealed class SfcLinkType : PlainLinkType
    {
        public SfcLinkType() : base("diag.sfc.link", new I18nText("Link (SFC)", "Wirkverbindung (SFC)"), DiagCategories.Sfc,
            Sfc.With("link", "verbindung", "wirkverbindung", "line", "linie", "loop", "rücksprung")) { }

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("route", "Routing", "Führung", "orthogonal", ("straight", "Straight / waypoints", "Gerade / Stützpunkte"), ("orthogonal", "Orthogonal", "Rechtwinklig")),
            P.Num("clearance", "Loop clearance", "Abstand Rücksprung", 10, 2, 100),
            P.Bool("arrow", "Arrow on upward links", "Pfeil bei Aufwärtsverbindungen", true),
            P.TextSize(),
        };

        public override List<Vec2> Path(SketchObject o, IReadOnlyList<Vec2> p) => PathIn(o, p, null);

        /// <summary>
        /// The routed path; a loop back runs at <c>clearance</c> left of its ends and, with <paramref name="doc"/>, also left
        /// of every element it would otherwise cross (4 mm clear).
        /// </summary>
        public List<Vec2> PathIn(SketchObject o, IReadOnlyList<Vec2> p, SketchDocument? doc)
        {
            if (p.Count == 2 && o.Str("route", "orthogonal") == "orthogonal" && p[1].Y < p[0].Y - 1e-6
                && D.ExitHorizontal(o, 0) != true && D.ExitHorizontal(o, 1) != true)
            {
                // Loop back: down out of the start, left around, up, and into the end from above.
                Vec2 a = p[0], b = p[1];
                const double c = 3;
                var x = Math.Min(a.X, b.X) - o.Num("clearance", 10);
                if (doc?.PageOf(o.Id) is { } page)
                {
                    var right = Math.Max(a.X, b.X);
                    var boxes = doc.ObjectsOn(page).Where(e => !e.IsPointGeometry && e.Id != o.Id).Select(e => e.Bounds())
                        .Where(r => r.Bottom > b.Y - c && r.Y < a.Y + c && r.X < right).ToList();
                    for (var pass = 0; pass < 8; pass++)
                    {
                        var hit = boxes.Where(r => r.Right > x - 1e-6).ToList();
                        if (hit.Count == 0) break;
                        var nx = Math.Min(x, hit.Min(r => r.X) - 4);
                        if (Math.Abs(nx - x) < 1e-9) break;
                        x = nx;
                    }
                }
                return new List<Vec2> { a, new(a.X, a.Y + c), new(x, a.Y + c), new(x, b.Y - c), new(b.X, b.Y - c), b };
            }
            return base.Path(o, p);
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var path = PathIn(o, p, ctx.Doc);
            var list = new List<Primitive>();
            if (path[0].DistanceTo(path[path.Count - 1]) > 1e-9 || path.Count > 2) list.Add(MainLine(path));
            Labels(list, o, ctx, path);
            if (!o.Flag("arrow", true) || path[path.Count - 1].Y >= path[0].Y - 1e-6) return list;
            // The longest rising segment carries a filled arrowhead in its middle, pointing up the line.
            var best = -1;
            var bestLen = 0.0;
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var d = path[i + 1] - path[i];
                if (d.Y < -1e-6 && d.Length > bestLen) { best = i; bestLen = d.Length; }
            }
            if (best < 0 || bestLen < 3) return list;
            var dir = (path[best + 1] - path[best]).Normalized();
            var mid = Vec2.Lerp(path[best], path[best + 1], 0.5);
            var n = dir.Perp;
            const double len = 2.2, half = 0.9;
            var tip = mid + dir * (len / 2);
            var back = mid - dir * (len / 2);
            list.Add(Prim.Polygon(new[] { tip, back + n * half, back - n * half }, D.Solid) with { Tag = DiagTags.Marker });
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) yield break;
            yield return Anchor.Point(AnchorRole.Mid, D.At(PathIn(o, p, ctx.Doc), 0.5).P, "mid");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in base.Actions(o, lang)) yield return a;
            yield return D.Toggle(o, "diag.sfc.arrow", new I18nText("Upward arrow", "Aufwärtspfeil"), "arrow", true);
        }
    }
}
