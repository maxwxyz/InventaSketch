using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Library category ids of the annotation module.</summary>
    public static class AnnCategories
    {
        public const string Lines = "ann.lines";
        public const string Labels = "ann.labels";
        public const string Dimensions = "ann.dimensions";
        public const string Hatching = "ann.hatching";
        public const string PatentMarks = "ann.patent";
        public const string Mechanics = "ann.mechanics";
        public const string Flowchart = "ann.flow";
    }

    /// <summary>Semantic primitive tags used by the annotation types (tests, preflight, host naming).</summary>
    public static class AnnTags
    {
        /// <summary>The primary stroke of a point object; user arrow/dash/width overrides apply to it.</summary>
        public const string Main = "main";
        /// <summary>A reference-numeral lead line (preflight checks crossings).</summary>
        public const string Lead = "lead";
        /// <summary>A reference numeral text.</summary>
        public const string Numeral = "numeral";
        /// <summary>Measured dimension text.</summary>
        public const string DimText = "dimtext";
        /// <summary>Figure label text ("FIG. 1").</summary>
        public const string FigLabel = "figlabel";
        /// <summary>Explicit hatch strokes or dots.</summary>
        public const string Hatch = "hatch";
        /// <summary>Hatched/dotted region outline carrying a pattern fill.</summary>
        public const string Region = "region";
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class P
    {
        public static ParamDef Num(string key, string en, string de, double? def, double? min = null, double? max = null, string? unit = "mm", double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def.HasValue ? def.Value : (ParamValue?)null, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def };

        public static ParamDef Text(string key, string en, string de, string? def)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def is null ? null : (ParamValue?)def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        public static readonly (string, string, string)[] HeadOptions =
        {
            ("none", "None", "Keine"), ("triangle", "Triangle", "Dreieck"), ("open", "Open", "Offen"), ("stealth", "Stealth", "Pfeilspitze"),
            ("diamond", "Diamond", "Raute"), ("oval", "Dot", "Punkt"), ("tick", "Tick", "Schrägstrich"), ("half", "Half", "Halbpfeil"),
        };

        public static readonly (string, string, string)[] SizeOptions = { ("small", "Small", "Klein"), ("medium", "Medium", "Mittel"), ("large", "Large", "Groß") };

        public static IEnumerable<ParamDef> Heads(ArrowHead start, ArrowHead end, ArrowSize size = ArrowSize.Medium)
        {
            yield return Choice("headStart", "Start head", "Spitze Anfang", Ann.HeadName(start), HeadOptions);
            yield return Choice("headEnd", "End head", "Spitze Ende", Ann.HeadName(end), HeadOptions);
            yield return Choice("headSize", "Head size", "Spitzengröße", size.ToString().ToLowerInvariant(), SizeOptions);
        }

        public static ParamDef TextSize(double def) => Num("textSize", "Text height", "Schrifthöhe", def, 1, 50, "mm", 0.5);
    }

    /// <summary>Shared annotation helpers.</summary>
    public static class Ann
    {
        /// <summary>Default text height (em, mm). 5 mm em gives ≈3.6 mm cap height, above the 3.2 mm patent minimum.</summary>
        public const double TextSize = 5.0;
        /// <summary>Approximate advance width per character relative to the em size (Arial-like fonts).</summary>
        public const double CharWidth = 0.6;

        public static string HeadName(ArrowHead h) => h.ToString().ToLowerInvariant();

        public static ArrowHead Head(SketchObject o, string key, ArrowHead fallback)
            => Enum.TryParse<ArrowHead>(o.Str(key, ""), true, out var h) ? h : fallback;

        public static ArrowSize HeadSize(SketchObject o, ArrowSize fallback = ArrowSize.Medium)
            => Enum.TryParse<ArrowSize>(o.Str("headSize", ""), true, out var s) ? s : fallback;

        public static Style Thin => Style.ThinLine;
        public static Style Visible => Style.Visible;

        public static TextStyle Ts(double size, TextAnchor anchor = TextAnchor.Middle, bool bold = false, bool italic = false)
            => new() { Size = size, Anchor = anchor, Bold = bold, Italic = italic };

        public static TextPrim Text(Vec2 at, string text, double size, string? tag = null, TextAnchor anchor = TextAnchor.Middle, bool bold = false, bool italic = false, double rotation = 0)
            => new(at, text, Ts(size, anchor, bold, italic), rotation) { Tag = tag };

        /// <summary>Approximate text extent (width, cap-height box) for layout decisions.</summary>
        public static Vec2 TextExtent(string text, double size)
        {
            var lines = (text ?? "").Replace("\r", "").Split('\n');
            var w = Math.Max(1, lines.Max(l => l.Length)) * size * CharWidth;
            var h = size * (1.0 + 1.2 * (lines.Length - 1));
            return new Vec2(w, h);
        }

        /// <summary>Half extent of a text box along a direction (support function of the box).</summary>
        public static double HalfExtentAlong(string text, double size, Vec2 dir)
        {
            var e = TextExtent(text, size);
            var d = dir.Normalized();
            return Math.Abs(d.X) * e.X / 2 + Math.Abs(d.Y) * e.Y / 2;
        }

        public static string Fmt(double v, int decimals)
        {
            var fmt = decimals <= 0 ? "0" : "0." + new string('#', decimals);
            var s = v.ToString(fmt, CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        /// <summary>Screen angle (degrees) of a direction, normalised to (-90, 90] so text stays readable.</summary>
        public static double ReadableAngle(Vec2 d)
        {
            var a = Geo.RadToDeg(Math.Atan2(d.Y, d.X));
            while (a > 90 + 1e-9) a -= 180;
            while (a <= -90 + 1e-9) a += 180;
            return Math.Abs(a) < 1e-9 ? 0 : a;
        }

        /// <summary>
        /// Rounded rectangle as a closed path with cubic corners. (Core's <see cref="Prim.Transform"/> turns rounded
        /// <see cref="RectPrim"/>s into sharp polygons, so box types use this instead.)
        /// </summary>
        public static PathPrim RoundedRect(double x, double y, double w, double h, double r, Style style)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            const double k = 0.5522847498307936;
            double x1 = x + w, y1 = y + h, c = r * (1 - k);
            return new PathPrim(new[]
            {
                PathSeg.Move(new Vec2(x + r, y)), PathSeg.Line(new Vec2(x1 - r, y)),
                PathSeg.Cubic(new Vec2(x1 - c, y), new Vec2(x1, y + c), new Vec2(x1, y + r)), PathSeg.Line(new Vec2(x1, y1 - r)),
                PathSeg.Cubic(new Vec2(x1, y1 - c), new Vec2(x1 - c, y1), new Vec2(x1 - r, y1)), PathSeg.Line(new Vec2(x + r, y1)),
                PathSeg.Cubic(new Vec2(x + c, y1), new Vec2(x, y1 - c), new Vec2(x, y1 - r)), PathSeg.Line(new Vec2(x, y + r)),
                PathSeg.Cubic(new Vec2(x, y + c), new Vec2(x + c, y), new Vec2(x + r, y)), PathSeg.Close,
            }, style);
        }

        public static PathPrim Cubic(Vec2 a, Vec2 c1, Vec2 c2, Vec2 b, Style style, string? tag = null)
            => new PathPrim(new[] { PathSeg.Move(a), PathSeg.Cubic(c1, c2, b) }, style) { Tag = tag };

        /// <summary>Samples primitives (lines, paths incl. curves) into polylines in page coordinates.</summary>
        public static List<List<Vec2>> Polylines(Primitive p, int curveSteps = 16)
        {
            var result = new List<List<Vec2>>();
            switch (p)
            {
                case LinePrim l:
                    result.Add(new List<Vec2> { l.A, l.B });
                    break;
                case PathPrim path:
                    {
                        List<Vec2>? cur = null;
                        var last = Vec2.Zero;
                        var start = Vec2.Zero;
                        foreach (var s in path.Segments)
                        {
                            switch (s.Op)
                            {
                                case SegOp.M:
                                    if (cur is { Count: >= 2 }) result.Add(cur);
                                    cur = new List<Vec2> { s.P }; last = start = s.P; break;
                                case SegOp.L:
                                    (cur ??= new List<Vec2> { last }).Add(s.P); last = s.P; break;
                                case SegOp.C:
                                    cur ??= new List<Vec2> { last };
                                    for (var i = 1; i <= curveSteps; i++) cur.Add(Geo.CubicAt(last, s.C1, s.C2, s.P, (double)i / curveSteps));
                                    last = s.P; break;
                                case SegOp.Q:
                                    cur ??= new List<Vec2> { last };
                                    for (var i = 1; i <= curveSteps; i++) cur.Add(Geo.QuadAt(last, s.C1, s.P, (double)i / curveSteps));
                                    last = s.P; break;
                                case SegOp.Z:
                                    if (cur is not null) { cur.Add(start); result.Add(cur); }
                                    cur = null; last = start; break;
                            }
                        }
                        if (cur is { Count: >= 2 }) result.Add(cur);
                        break;
                    }
                case RectPrim r:
                    result.Add(new List<Vec2> { new(r.X, r.Y), new(r.X + r.W, r.Y), new(r.X + r.W, r.Y + r.H), new(r.X, r.Y + r.H), new(r.X, r.Y) });
                    break;
                case EllipsePrim e:
                    result.Add(Geo.ArcPoints(e.Center, e.Rx, e.Ry, 0, 2 * Math.PI, 48));
                    break;
                case GroupPrim g:
                    foreach (var c in g.Children) result.AddRange(Polylines(c, curveSteps));
                    break;
            }
            return result;
        }

        /// <summary>Inside test for several contours with the even-odd rule.</summary>
        public static bool Inside(Vec2 p, IReadOnlyList<IReadOnlyList<Vec2>> contours)
            => contours.Count(c => Geo.PointInPolygon(p, c)) % 2 == 1;

        /// <summary>Clips a segment to the inside of the contours (even-odd). Returns the inside pieces.</summary>
        public static List<(Vec2 A, Vec2 B)> ClipSegment(Vec2 a, Vec2 b, IReadOnlyList<IReadOnlyList<Vec2>> contours)
        {
            var ts = new List<double> { 0, 1 };
            var r = b - a;
            foreach (var c in contours)
                for (var i = 0; i < c.Count; i++)
                {
                    var p = c[i]; var q = c[(i + 1) % c.Count];
                    var s = q - p;
                    var den = r.Cross(s);
                    if (Math.Abs(den) < Geo.Eps) continue;
                    var t = (p - a).Cross(s) / den;
                    var u = (p - a).Cross(r) / den;
                    if (t > 0 && t < 1 && u >= 0 && u <= 1) ts.Add(t);
                }
            ts.Sort();
            var res = new List<(Vec2, Vec2)>();
            for (var i = 0; i + 1 < ts.Count; i++)
            {
                if (ts[i + 1] - ts[i] < 1e-9) continue;
                var m = Vec2.Lerp(a, b, (ts[i] + ts[i + 1]) / 2);
                if (Inside(m, contours)) res.Add((Vec2.Lerp(a, b, ts[i]), Vec2.Lerp(a, b, ts[i + 1])));
            }
            return res;
        }

        /// <summary>Applies a style override including text colour and tag-aware arrow/dash handling.</summary>
        public static Primitive Override(Primitive p, Style over)
        {
            switch (p)
            {
                case GroupPrim g:
                    return g with { Children = g.Children.Select(c => Override(c, over)).ToList() };
                case TextPrim t:
                    return over.Stroke is string col && !string.Equals(col, "none", StringComparison.OrdinalIgnoreCase)
                        ? t with { TextStyle = t.TextStyle with { Color = col } }
                        : t;
            }
            var main = p.Tag is AnnTags.Main or AnnTags.Lead;
            return Prim.Restyle(p, s =>
            {
                var role = s.Role ?? LineRole.Visible;
                return s with
                {
                    Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                    Opacity = over.Opacity ?? s.Opacity,
                    StrokeWidth = over.StrokeWidth is double w && (main || role is LineRole.Visible or LineRole.Cut) ? w : s.StrokeWidth,
                    Fill = over.Fill is not null && s.Fill is not null && s.Fill.Kind != FillKind.None && p.Tag != AnnTags.Hatch ? over.Fill : s.Fill,
                    ArrowStart = main ? over.ArrowStart ?? s.ArrowStart : s.ArrowStart,
                    ArrowEnd = main ? over.ArrowEnd ?? s.ArrowEnd : s.ArrowEnd,
                    ArrowSize = main ? over.ArrowSize ?? s.ArrowSize : s.ArrowSize,
                    Dash = main ? over.Dash ?? s.Dash : s.Dash,
                };
            });
        }

        /// <summary>Returns a copy with points (and attachments) in reverse order.</summary>
        public static SketchObject Reverse(SketchObject o) => Edits.ReversePoints(o);

        /// <summary>Choice action helper: sets <paramref name="key"/> to <paramref name="value"/>.</summary>
        public static ContextAction SetChoice(SketchObject o, string id, I18nText label, string icon, string key, string value, string current, string group)
            => new(id, label, icon, x => x.WithParam(key, value)) { IsActive = string.Equals(o.Str(key, current), value, StringComparison.OrdinalIgnoreCase), Group = group };

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string icon, string key, bool fallback, string? group = null)
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group };

        /// <summary>Arrowhead toggles, swap and head size for arrow-like point types.</summary>
        public static IEnumerable<ContextAction> ArrowActions(SketchObject o, ArrowHead defStart, ArrowHead defEnd)
        {
            var s = Head(o, "headStart", defStart);
            var e = Head(o, "headEnd", defEnd);
            yield return new ContextAction("ann.head.start", new I18nText("Arrowhead at start", "Pfeilspitze am Anfang"), "arrow-start",
                x => x.WithParam("headStart", Head(x, "headStart", defStart) == ArrowHead.None ? "triangle" : "none")) { IsActive = s != ArrowHead.None, Group = "heads" };
            yield return new ContextAction("ann.head.end", new I18nText("Arrowhead at end", "Pfeilspitze am Ende"), "arrow-end",
                x => x.WithParam("headEnd", Head(x, "headEnd", defEnd) == ArrowHead.None ? "triangle" : "none")) { IsActive = e != ArrowHead.None, Group = "heads" };
            yield return new ContextAction("ann.head.style", new I18nText("Next arrowhead style", "Nächste Pfeilspitzenform"), "arrow-style", x =>
            {
                var cur = Head(x, "headEnd", defEnd);
                var all = (ArrowHead[])Enum.GetValues(typeof(ArrowHead));
                var next = all[(Array.IndexOf(all, cur) + 1) % all.Length];
                if (next == ArrowHead.None) next = all[1];
                return x.WithParam("headEnd", HeadName(next));
            }) { Group = "heads" };
            yield return new ContextAction("ann.head.size", new I18nText("Next arrowhead size", "Nächste Pfeilspitzengröße"), "arrow-size",
                x => x.WithParam("headSize", ((ArrowSize)(((int)HeadSize(x) + 1) % 3)).ToString().ToLowerInvariant())) { Group = "heads" };
            yield return SwapAction();
        }

        public static ContextAction SwapAction() => new("ann.swap", new I18nText("Swap direction", "Richtung umkehren"), "swap", Reverse) { Group = "direction" };

        /// <summary>
        /// Attaches point <paramref name="index"/> of a point object to a box object at the given page position
        /// (snap result). The attachment is stored in normalised box coordinates so it follows the target.
        /// </summary>
        public static SketchDocument AttachPoint(SketchDocument doc, string objectId, int index, string targetId, Vec2 pagePoint)
            => Edits.AttachPoint(doc, objectId, index, targetId, pagePoint);
    }

    /// <summary>Base class of all annotation object types.</summary>
    public abstract class AnnType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected AnnType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;

        public override Primitive ApplyOverride(Primitive p, Style over) => Ann.Override(p, over);

        protected static double TextSizeOf(SketchObject o, double fallback = Ann.TextSize) => o.Num("textSize", fallback);
    }

    /// <summary>Box annotation: renders in local coordinates centred at (0,0) and exposes edge-midpoint ports.</summary>
    public abstract class AnnBoxType : AnnType
    {
        protected AnnBoxType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        /// <summary>Whether connector ports are exposed at the edge midpoints.</summary>
        protected virtual bool EdgePorts => true;

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            if (!EdgePorts) yield break;
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return Anchor.Point(AnchorRole.Port, new Vec2(0, -hh), "top");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(hw, 0), "right");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(0, hh), "bottom");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-hw, 0), "left");
        }
    }

    /// <summary>Point annotation: renders in page coordinates from <see cref="SketchDocument.ResolvePoints"/>.</summary>
    public abstract class AnnPointType : AnnType
    {
        protected AnnPointType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        public override GeometryKind Geometry => GeometryKind.Points;

        /// <summary>Minimum number of points needed to render.</summary>
        protected virtual int MinPoints => 2;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < MinPoints) return Array.Empty<Primitive>();
            return RenderPoints(o, ctx, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < p.Count; i++) yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(p[i], p[i + 1], 0.5), "m" + i.ToString(CultureInfo.InvariantCulture));
        }
    }
}
