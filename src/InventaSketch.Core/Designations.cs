using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>How a host names the pages a continuation marker points to ("slide 4", "slides 2, 4"; default "page").</summary>
    public sealed record PageNames(I18nText One, I18nText Many)
    {
        public static readonly PageNames Pages = new(new I18nText("page", "Seite"), new I18nText("pages", "Seiten"));
        public static readonly PageNames Slides = new(new I18nText("slide", "Folie"), new I18nText("slides", "Folien"));
    }

    /// <summary>
    /// Names that belong together (ROADMAP plan review 2026-11-25 night, item 12), module-agnostic:
    /// <list type="bullet">
    /// <item>every connection line (wire, cable, pipe, duct, bus …: a line with a medium, <see cref="Building.LineMedium"/>)
    /// carries a designation and an optional value label (cross-section, type, rating, DN, flow). The designation is the
    /// type's own parameter when it has one (<c>ref</c> of cabinet wires and cables, <c>label</c> of energy cables, <c>lineNo</c>
    /// of process lines – those types draw it themselves), else the generic <see cref="Key"/>; the generic label is placed
    /// below the longest segment and follows every edit (it is rendered, not stored);</item>
    /// <item>end markers: the designation at both ends of the line (<see cref="EndsKey"/>);</item>
    /// <item>equal designations mean the same connection (<see cref="ConnectKind.SameName"/>); across pages the host passes
    /// where else a designation occurs (<see cref="RenderContext.Continuations"/>) and the line shows a continuation marker
    /// "→ W3, slide 4" at its free end nearest the page edge; a free end at the page edge without a known continuation says
    /// "→ W3";</item>
    /// <item>elements with connection ports that have no designation of their own (building-services devices …) get the same
    /// generic designation, drawn above them; elements with their own designator label (electrical ref/value, fluid ref,
    /// process tag) keep it, and values stay with <see cref="ValueLabels"/>.</item>
    /// </list>
    /// </summary>
    public static class Designations
    {
        /// <summary>Generic designation parameter of connection lines and elements ("W3", "-W1", "K1").</summary>
        public const string Key = "ref";
        /// <summary>Generic value label of a connection line ("3×1.5 mm²", "DN 50", "16 A").</summary>
        public const string ValueKey = "value";
        /// <summary>Boolean parameter: the designation also at both ends of the line (end markers).</summary>
        public const string EndsKey = "refEnds";
        /// <summary>Tags of the rendered labels.</summary>
        public const string LabelTag = "designation";
        public const string EndTag = "designation-end";
        public const string ContinuationTag = "continuation";
        /// <summary>Text size of the labels (mm), as the line labels of the modules.</summary>
        public const double TextSize = 2.2;
        /// <summary>A free end this close to the page edge (mm) leaves the page.</summary>
        public const double EdgeTolerance = 3.0;

        private static readonly string[] LineKeys = { "ref", "label", "lineNo" };
        private static readonly string[] ElementKeys = { "ref", "tag", "name" };

        /// <summary>The designation parameter the type declares itself (it then draws the designation), or null.</summary>
        public static string? OwnKey(ObjectType type, bool line)
        {
            var keys = line ? LineKeys : ElementKeys;
            foreach (var p in type.Parameters) if (Array.IndexOf(keys, p.Key) >= 0) return p.Key;
            return null;
        }

        /// <summary>The parameter holding the designation of objects of <paramref name="type"/>.</summary>
        public static string KeyOf(ObjectType type, bool line) => OwnKey(type, line) ?? Key;

        /// <summary>True if <paramref name="o"/> is a connection line (it carries a medium).</summary>
        public static bool IsConnectionLine(Registry registry, SketchDocument doc, SketchObject o, Lang lang = Lang.En)
            => o.IsPointGeometry && Building.LineMedium(registry, doc, o, lang) is not null;

        /// <summary>The designation of <paramref name="o"/> as typed (trimmed), or null.</summary>
        public static string? Of(Registry registry, SketchObject o)
        {
            if (registry.Type(o.Type) is not { } type) return null;
            var text = o.Str(KeyOf(type, o.IsPointGeometry), "").Trim();
            return text.Length > 0 ? text : null;
        }

        /// <summary>The generic value label of a line (types without a designation of their own), or null.</summary>
        public static string? ValueOf(ObjectType type, SketchObject o)
        {
            if (OwnKey(type, true) is not null || type.Parameters.Any(p => p.Key == ValueKey)) return null;
            var text = o.Str(ValueKey, "").Trim();
            return text.Length > 0 ? text : null;
        }

        /// <summary>
        /// The comparison form of a designation: trimmed, upper case, without a leading IEC 81346 prefix sign ("-W1" = "W1").
        /// </summary>
        public static string Normalize(string designation)
        {
            var t = designation.Trim();
            while (t.Length > 1 && (t[0] == '-' || t[0] == '=' || t[0] == '+')) t = t.Substring(1);
            return t.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// The objects of <paramref name="doc"/> with a designation, by its comparison form: connection lines and elements
        /// (annotation lines with a label of their own are not designations).
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Index(Registry registry, SketchDocument doc, Lang lang = Lang.En)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var page in doc.Pages)
                foreach (var o in doc.ObjectsOn(page))
                {
                    if (Of(registry, o) is not { } d) continue;
                    if (o.IsPointGeometry && !IsConnectionLine(registry, doc, o, lang)) continue;
                    var k = Normalize(d);
                    if (!map.TryGetValue(k, out var list)) map[k] = list = new List<string>();
                    list.Add(o.Id);
                }
            return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
        }

        /// <summary>The objects with the same designation as <paramref name="id"/> (it first); just it when it has none.</summary>
        public static IReadOnlyList<string> SameName(Registry registry, SketchDocument doc, string id, Lang lang = Lang.En)
        {
            var o = doc.Get(id);
            if (o is null) return Array.Empty<string>();
            if (Of(registry, o) is not { } d || (o.IsPointGeometry && !IsConnectionLine(registry, doc, o, lang))) return new[] { id };
            var key = Normalize(d);
            var result = new List<string> { id };
            foreach (var page in doc.Pages)
                foreach (var x in doc.ObjectsOn(page))
                {
                    if (x.Id == id || Of(registry, x) is not { } dx || Normalize(dx) != key) continue;
                    if (x.IsPointGeometry && !IsConnectionLine(registry, doc, x, lang)) continue;
                    result.Add(x.Id);
                }
            return result;
        }

        /// <summary>The text of a continuation marker: "→ W3, slide 4", "→ W3, slides 2, 4", or "→ W3" without known pages.</summary>
        public static string ContinuationText(string designation, IReadOnlyList<string>? pages, PageNames? names, Lang lang)
        {
            if (pages is null || pages.Count == 0) return "→ " + designation;
            var n = names ?? PageNames.Pages;
            return "→ " + designation + ", " + (pages.Count == 1 ? n.One : n.Many).Get(lang) + " " + string.Join(", ", pages);
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>
        /// <paramref name="local"/> with the generic labels of <paramref name="o"/>: the designation and value label of a line
        /// without labels of its own (below its longest segment), end markers, continuation markers, and the designation of an
        /// element without a designator of its own (above it). Never throws; objects without a designation are returned as they are.
        /// </summary>
        public static IReadOnlyList<Primitive> Append(Registry registry, SketchObject o, RenderContext ctx, IReadOnlyList<Primitive> local)
        {
            if (local.Count == 0 || registry.Type(o.Type) is not { } type) return local;
            try
            {
                return o.IsPointGeometry ? AppendLine(registry, type, o, ctx, local) : AppendElement(type, o, local);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or FormatException)
            {
                return local;
            }
        }

        private static IReadOnlyList<Primitive> AppendElement(ObjectType type, SketchObject o, IReadOnlyList<Primitive> local)
        {
            if (!o.Params.ContainsKey(Key) || OwnKey(type, false) is not null) return local;
            var text = o.Str(Key, "").Trim();
            if (text.Length == 0) return local;
            var m = o.Matrix();
            if (Prim.Bounds(local.Select(p => Prim.Transform(p, m))) is not Rect b) return local;
            const double gap = 1.0;
            var at = new Vec2(b.X + b.W / 2, b.Y - gap - ValueLabels.TextSize / 2);
            var rotation = m.Determinant < 0 ? 0 : -m.RotationDegrees;
            if (Math.Abs(rotation) < 1e-9) rotation = 0;
            var label = new TextPrim(m.Invert().Apply(at), text, new TextStyle { Size = ValueLabels.TextSize, Anchor = TextAnchor.Middle }, rotation) { Tag = LabelTag };
            return local.Concat(new[] { label }).ToList();
        }

        private static IReadOnlyList<Primitive> AppendLine(Registry registry, ObjectType type, SketchObject o, RenderContext ctx, IReadOnlyList<Primitive> local)
        {
            var designation = Of(registry, o);
            var value = ValueOf(type, o);
            if (designation is null && value is null) return local;
            if (!IsConnectionLine(registry, ctx.Doc, o, ctx.Lang)) return local;
            var path = MainPath(local);
            if (path.Count < 2) return local;
            var list = new List<Primitive>(local);
            // Generic middle label: types without a designation of their own.
            if (OwnKey(type, true) is null)
            {
                var text = designation is null ? value! : value is null ? designation : designation + " · " + value;
                list.Add(AlongLongest(path, text, below: true) with { Tag = LabelTag });
            }
            if (designation is null) return list;
            // Continuation marker: where the line leaves the page or continues on another page.
            var pts = ctx.Doc.ResolvePoints(o);
            var size = (ctx.Doc.PageOf(o.Id) ?? (ctx.Doc.Pages.Count > 0 ? ctx.Doc.Pages[0] : null))?.Size;
            IReadOnlyList<string>? pages = null;
            if (ctx.Continuations is { } map && map.TryGetValue(Normalize(designation), out var p) && p.Count > 0) pages = p;
            var marked = new HashSet<int>();
            bool Free(int i) => o.Attach is null || i >= o.Attach.Count || o.Attach[i] is null;
            double EdgeDistance(Vec2 q) => size is { } s ? Math.Min(Math.Min(q.X, s.W - q.X), Math.Min(q.Y, s.H - q.Y)) : double.MaxValue;
            var ends = new[] { 0, pts.Count - 1 }.Distinct().ToList();
            if (pages is not null)
            {
                var end = ends.OrderBy(i => Free(i) ? 0 : 1).ThenBy(i => EdgeDistance(pts[i])).ThenByDescending(i => i).First();
                marked.Add(end);
            }
            else
                foreach (var i in ends)
                    if (Free(i) && EdgeDistance(pts[i]) <= EdgeTolerance) marked.Add(i);
            foreach (var i in marked)
                list.Add(AtEnd(path, i == 0, ContinuationText(designation, pages, ctx.PageNames, ctx.Lang), TextSize) with { Tag = ContinuationTag });
            // End markers: the designation at both ends (not where a continuation marker already names it).
            if (o.Flag(EndsKey, false))
                foreach (var i in ends)
                    if (!marked.Contains(i)) list.Add(AtEnd(path, i == 0, designation, TextSize * 0.9) with { Tag = EndTag });
            return list;
        }

        /// <summary>The vertices of the longest stroked path or line of the drawing (the line itself, routed and with hops).</summary>
        private static List<Vec2> MainPath(IEnumerable<Primitive> local)
        {
            List<Vec2> best = new();
            var bestLen = -1.0;
            foreach (var p in Prim.Flatten(local))
            {
                List<Vec2>? v = p switch
                {
                    LinePrim l when l.Style.HasStroke => new List<Vec2> { l.A, l.B },
                    PathPrim pa when pa.Style.HasStroke && !pa.IsClosed => pa.Segments.Where(s => s.Op != SegOp.Z).Select(s => s.P).ToList(),
                    _ => null,
                };
                if (v is null || v.Count < 2) continue;
                var len = 0.0;
                for (var i = 0; i + 1 < v.Count; i++) len += v[i].DistanceTo(v[i + 1]);
                if (len > bestLen + 1e-9) { bestLen = len; best = v; }
            }
            return best;
        }

        /// <summary>Reading direction of a segment direction: left to right, or bottom to top for vertical runs.</summary>
        private static Vec2 Reading(Vec2 dir) => dir.X < -1e-9 || (Math.Abs(dir.X) <= 1e-9 && dir.Y > 0) ? -dir : dir;

        private static double Angle(Vec2 r)
        {
            var a = Geo.RadToDeg(Math.Atan2(r.Y, r.X));
            return Math.Abs(a) < 1e-9 ? 0 : a;
        }

        /// <summary>A text centred on the longest segment, above or below it, upright.</summary>
        private static TextPrim AlongLongest(IReadOnlyList<Vec2> p, string text, bool below)
        {
            var best = 0;
            var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var d = len > Geo.Eps ? (p[best + 1] - p[best]) / len : new Vec2(1, 0);
            var r = Reading(d);
            var up = new Vec2(r.Y, -r.X);
            var at = Vec2.Lerp(p[best], p[best + 1], 0.5) + up * ((below ? -1 : 1) * TextSize * 0.9);
            return new TextPrim(at, text, new TextStyle { Size = TextSize, Anchor = TextAnchor.Middle }, Angle(r));
        }

        /// <summary>A text just inside one end of the path, above the last segment, upright, ending (or starting) at the end.</summary>
        private static TextPrim AtEnd(IReadOnlyList<Vec2> p, bool start, string text, double size)
        {
            var e = start ? p[0] : p[p.Count - 1];
            var k = start ? 1 : p.Count - 2;
            while (k > 0 && k < p.Count - 1 && p[k].DistanceTo(e) < Geo.Eps) k += start ? 1 : -1;
            var prev = p[k];
            var len = prev.DistanceTo(e);
            var outward = len > Geo.Eps ? (e - prev) / len : new Vec2(1, 0);
            var r = Reading(outward);
            var up = new Vec2(r.Y, -r.X);
            var forward = r.X * outward.X + r.Y * outward.Y > 0;
            var at = e - outward * 1.0 + up * (size * 0.8);
            return new TextPrim(at, text, new TextStyle { Size = size, Anchor = forward ? TextAnchor.End : TextAnchor.Start }, Angle(r));
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// Generic designation actions (pie Edit/bar, group "label"): for a connection line without a designation of its own
        /// "Designation …" and "Value label …"; for every designated line "End markers"; for an element with connection ports
        /// and no designator of its own "Designation …". <paramref name="own"/> are the type's actions (nothing is offered twice).
        /// </summary>
        public static IEnumerable<ContextAction> Actions(Registry registry, SketchObject o, RenderContext ctx, IReadOnlyList<ContextAction> own)
        {
            if (registry.Type(o.Type) is not { } type) yield break;
            if (own.Any(a => a.Id is "line.ref" or "elem.ref")) yield break;
            if (o.IsPointGeometry)
            {
                if (!IsConnectionLine(registry, ctx.Doc, o, ctx.Lang)) yield break;
                if (OwnKey(type, true) is null)
                {
                    yield return ContextAction.WithInput("line.ref", new I18nText("Designation …", "Bezeichnung …"), IconIds.Name,
                        new I18nText("Designation (e.g. W3, -W1)", "Bezeichnung (z. B. W3, -W1)"), o.Str(Key, ""),
                        (x, t) => x.WithParam(Key, (t ?? "").Trim())) with { Group = "label", ParamKeys = new[] { Key } };
                    if (type.Parameters.All(p => p.Key != ValueKey))
                        yield return ContextAction.WithInput("line.value", new I18nText("Value label …", "Wertangabe …"), IconIds.Value,
                            new I18nText("Value label (cross-section, type, rating, DN, flow)", "Wertangabe (Querschnitt, Typ, Bemessung, DN, Durchfluss)"), o.Str(ValueKey, ""),
                            (x, t) => x.WithParam(ValueKey, (t ?? "").Trim())) with { Group = "label", ParamKeys = new[] { ValueKey } };
                }
                if (Of(registry, o) is not null)
                {
                    var on = o.Flag(EndsKey, false);
                    yield return new ContextAction("line.ref.ends", new I18nText("End markers", "Endmarkierungen"), IconIds.Toggle,
                        x => x.WithParam(EndsKey, !x.Flag(EndsKey, false))) { IsActive = on, Group = "label", ParamKeys = new[] { EndsKey } };
                }
                yield break;
            }
            if (OwnKey(type, false) is not null || !HasConnectionPorts(registry, type, o, ctx)) yield break;
            yield return ContextAction.WithInput("elem.ref", new I18nText("Designation …", "Bezeichnung …"), IconIds.Name,
                new I18nText("Designation (e.g. P1, -QM1)", "Bezeichnung (z. B. P1, -QM1)"), o.Str(Key, ""),
                (x, t) => x.WithParam(Key, (t ?? "").Trim())) with { Group = "label", ParamKeys = new[] { Key } };
        }

        private static bool HasConnectionPorts(Registry registry, ObjectType type, SketchObject o, RenderContext ctx)
        {
            foreach (var a in registry.PageAnchors(o, ctx))
                if (a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && type.Connection(o, a) is not null) return true;
            return false;
        }

        /// <summary>Page labels as hosts pass them for <see cref="RenderContext.Continuations"/> ("2", "4").</summary>
        public static IReadOnlyList<string> PageLabels(IEnumerable<int> numbers) => numbers.Distinct().OrderBy(n => n).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();
    }
}
