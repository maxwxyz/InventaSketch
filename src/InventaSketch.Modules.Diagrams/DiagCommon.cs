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

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>Library category ids of the diagrams module.</summary>
    public static class DiagCategories
    {
        public const string Structure = "diag.uml.structure";
        public const string UseCase = "diag.uml.usecase";
        public const string Activity = "diag.uml.activity";
        public const string State = "diag.uml.state";
        public const string Sequence = "diag.uml.sequence";
        public const string Relations = "diag.uml.relations";
        public const string Er = "diag.er";
        public const string System = "diag.system";
        public const string Bpmn = "diag.bpmn";
    }

    /// <summary>Semantic primitive tags used by the diagram types (tests, host naming, style overrides).</summary>
    public static class DiagTags
    {
        /// <summary>The outline of a box symbol; user fill/width/dash overrides apply to it.</summary>
        public const string Outline = "outline";
        /// <summary>
        /// The line of a connector; user width/dash overrides apply to it. Its end notations are Core arrowheads on its
        /// style (<see cref="Style.ArrowStart"/>/<see cref="Style.ArrowEnd"/>), so user arrowhead overrides apply too.
        /// </summary>
        public const string Main = "main";
        /// <summary>The name/title text of a symbol (one primitive per text line).</summary>
        public const string Name = "name";
        /// <summary>A stereotype line («interface»).</summary>
        public const string Stereotype = "stereotype";
        /// <summary>One line of a compartment (attribute, operation, literal, activity, table row).</summary>
        public const string Row = "row";
        /// <summary>A connector label (middle text, multiplicity or role).</summary>
        public const string Label = "label";
        /// <summary>Compartment or lane divider lines.</summary>
        public const string Divider = "divider";
        /// <summary>Port square of a block.</summary>
        public const string Port = "port";
        /// <summary>Inner marker of a symbol (gateway sign, component icon, decoration).</summary>
        public const string Marker = "marker";
    }

    /// <summary>
    /// Connector end notations: UML arrowheads and crow's-foot cardinalities. Each maps to a Core
    /// <see cref="ArrowHead"/> (<see cref="D.Head"/>); the names are the stored parameter values of the ER connector.
    /// </summary>
    public enum DiagEnd
    {
        None, Open, Filled, Hollow, FilledDiamond, HollowDiamond,
        /// <summary>Crow's foot: one (single bar).</summary>
        One,
        /// <summary>Crow's foot: one and only one (double bar).</summary>
        OnlyOne,
        /// <summary>Crow's foot: zero or one (circle and bar).</summary>
        ZeroOrOne,
        /// <summary>Crow's foot: many (foot).</summary>
        Many,
        /// <summary>Crow's foot: one or many (bar and foot).</summary>
        OneOrMany,
        /// <summary>Crow's foot: zero or many (circle and foot).</summary>
        ZeroOrMany,
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

        public static ParamDef TextSize() => Num("textSize", "Text height", "Schrifthöhe", D.TextSize, 1, 30, "mm", 0.5);

        public static ParamDef Stereotype(string? def = null) => Text("stereotype", "Stereotype", "Stereotyp", def);

        public static ParamDef Route() => Choice("route", "Routing", "Führung", "straight",
            ("straight", "Straight / waypoints", "Gerade / Stützpunkte"), ("orthogonal", "Orthogonal", "Rechtwinklig"));

        public static readonly (string, string, string)[] CrowOptions =
        {
            ("none", "None", "Keine"), ("one", "One", "Eins"), ("onlyOne", "One and only one", "Genau eins"),
            ("zeroOrOne", "Zero or one", "Null oder eins"), ("many", "Many", "Viele"),
            ("oneOrMany", "One or many", "Eins oder viele"), ("zeroOrMany", "Zero or many", "Null oder viele"),
        };
    }

    /// <summary>Shared diagram helpers: text layout, ports, styles, end notations, actions.</summary>
    public static class D
    {
        /// <summary>Default text height (em, mm). 3.5 mm ≈ 10 pt.</summary>
        public const double TextSize = 3.5;
        /// <summary>Conservative advance width per character relative to the em size (used to fit text into boxes).</summary>
        public const double CharWidth = 0.6;
        /// <summary>Row pitch relative to the text size.</summary>
        public const double LineFactor = 1.3;
        /// <summary>Text is never shrunk below this height when fitting it into a box.</summary>
        public const double MinTextSize = 1.8;
        public const string Paper = "#ffffff";
        public const string Ink = "#000000";
        /// <summary>Outline stroke width of symbols (mm).</summary>
        public const double Stroke = 0.35;
        /// <summary>Line width of connectors (mm).</summary>
        public const double LineWidth = 0.3;

        public static Style Outline => Style.Visible with { StrokeWidth = Stroke, Fill = Fill.Solid(Paper) };
        public static Style OutlineOpen => Style.Visible with { StrokeWidth = Stroke };
        public static Style Thin => Style.ThinLine with { StrokeWidth = 0.25 };
        public static Style Solid => Style.Visible with { StrokeWidth = Stroke, Fill = Fill.Solid(Ink) };
        public static Style Dashed(Style s) => s with { Dash = DashStyle.Dash };

        // ------------------------------------------------------------------ text

        /// <summary>Splits a multi-line text parameter into lines (trailing empty lines dropped).</summary>
        public static IReadOnlyList<string> Lines(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            var lines = text!.Replace("\r", "").Split('\n').ToList();
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        /// <summary>Approximate advance width of a text line.</summary>
        public static double Width(string text, double size) => Math.Max(1, text.Length) * size * CharWidth;

        /// <summary>
        /// Text height that fits <paramref name="rows"/> rows of the given lines into the available area: the preferred
        /// size, reduced only when the content would not fit (never below <see cref="MinTextSize"/>).
        /// </summary>
        public static double Fit(double size, IEnumerable<string> lines, double availW, double availH, double rows)
        {
            var s = size;
            var maxLen = lines.Select(l => l.Length).DefaultIfEmpty(0).Max();
            if (maxLen > 0) s = Math.Min(s, Math.Max(0, availW) / (maxLen * CharWidth));
            if (rows > 0) s = Math.Min(s, Math.Max(0, availH) / (rows * LineFactor));
            return Math.Max(MinTextSize, s);
        }

        /// <summary>Text primitive; <paramref name="underline"/> uses the host's native font underline (key attributes, static members).</summary>
        public static TextPrim Text(Vec2 at, string text, double size, string tag, TextAnchor anchor = TextAnchor.Middle, bool bold = false, bool italic = false, double rotation = 0, bool underline = false)
            => new(at, text, new TextStyle { Size = size, Anchor = anchor, Bold = bold, Italic = italic, Underline = underline }, rotation) { Tag = tag };

        /// <summary>Puts guillemets around a stereotype unless the user already typed them.</summary>
        public static string Guillemets(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return s;
            if (s.StartsWith("«", StringComparison.Ordinal) || s.StartsWith("<<", StringComparison.Ordinal)) return s;
            return "«" + s + "»";
        }

        /// <summary>
        /// Centred multi-line text inside an area (one <see cref="TextPrim"/> per line), fitted to the area.
        /// </summary>
        public static void CenterText(List<Primitive> list, string? text, double size, Vec2 center, double availW, double availH, string tag = DiagTags.Name, bool bold = false, bool italic = false, bool underline = false)
        {
            var lines = Lines(text);
            if (lines.Count == 0) return;
            var s = Fit(size, lines, availW, availH, lines.Count);
            var rowH = s * LineFactor;
            var y0 = center.Y - (lines.Count - 1) * rowH / 2;
            for (var i = 0; i < lines.Count; i++)
                list.Add(Text(new Vec2(center.X, y0 + i * rowH), lines[i], s, tag, TextAnchor.Middle, bold, italic, underline: underline && lines[i].Length > 0));
        }

        // ------------------------------------------------------------------ compartments

        /// <summary>
        /// Lays out stacked compartments (UML class, entity, state) top-down in a box of size w × h centred at the origin.
        /// The first section is the header (centred rows); further sections are left-aligned lists separated by divider
        /// lines; the last section takes the remaining height. Text is fitted so all rows stay inside the box.
        /// </summary>
        public static List<Primitive> Compartments(double w, double h, IReadOnlyList<IReadOnlyList<DiagRow>> sections, double textSize, double top = 0)
        {
            var list = new List<Primitive>();
            if (sections.Count == 0) return list;
            const double pad = 1.5;
            var vpad = Math.Min(1.0, h * 0.04);
            double Rows(IReadOnlyList<DiagRow> s) => s.Count == 0 ? 0.6 : s.Count;
            var totalRows = sections.Sum(Rows);
            var availH = h - top - sections.Count * 2 * vpad;
            var size = Fit(textSize, sections.SelectMany(s => s).Select(r => r.Text), w - 2 * pad, availH, totalRows);
            var rowH = size * LineFactor;
            double hw = w / 2, hh = h / 2;
            var y = -hh + top;
            for (var k = 0; k < sections.Count; k++)
            {
                var rows = sections[k];
                var last = k == sections.Count - 1;
                var secH = last ? hh - y : Rows(rows) * rowH + 2 * vpad;
                if (k > 0) list.Add(new LinePrim(new Vec2(-hw, y), new Vec2(hw, y), OutlineOpen) { Tag = DiagTags.Divider });
                var header = k == 0;
                // Header: rows centred vertically when it is the only section, otherwise at the top.
                var ry = header && sections.Count == 1 ? y + (secH - rows.Count * rowH) / 2 : y + vpad;
                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var at = new Vec2(header ? 0 : -hw + pad, ry + (i + 0.5) * rowH);
                    list.Add(Text(at, r.Text, size, r.Tag ?? (header ? DiagTags.Name : DiagTags.Row), header ? TextAnchor.Middle : TextAnchor.Start, r.Bold, r.Italic,
                        underline: r.Underline && r.Text.Length > 0));
                }
                y += secH;
            }
            return list;
        }

        /// <summary>Parses a class member line: a leading "_" marks a static member (underlined).</summary>
        public static DiagRow MemberRow(string line)
        {
            var t = line.TrimEnd();
            return t.StartsWith("_", StringComparison.Ordinal) ? new DiagRow(t.Substring(1)) { Underline = true } : new DiagRow(t);
        }

        /// <summary>Header rows: optional stereotype, then the name (bold, italic if abstract).</summary>
        public static List<DiagRow> Header(string? stereotype, string? name, bool italic = false)
        {
            var rows = new List<DiagRow>();
            if (!string.IsNullOrWhiteSpace(stereotype)) rows.Add(new DiagRow(Guillemets(stereotype!)) { Tag = DiagTags.Stereotype });
            foreach (var l in Lines(name)) rows.Add(new DiagRow(l) { Bold = true, Italic = italic });
            return rows;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>Rounded rectangle as a closed path with cubic corners (stays rounded under rotation in all hosts).</summary>
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

        public static PathPrim Diamond(double hw, double hh, Style style)
            => Prim.Polygon(new[] { new Vec2(0, -hh), new Vec2(hw, 0), new Vec2(0, hh), new Vec2(-hw, 0) }, style);

        /// <summary>UML stick figure fitted into the given area (head, body, arms, legs).</summary>
        public static IEnumerable<Primitive> StickFigure(Vec2 center, double w, double h, Style style)
        {
            var fh = Math.Min(h, w * 2.2);
            var top = center.Y - fh / 2;
            var r = fh * 0.13;
            var hw = Math.Min(w / 2, fh * 0.32);
            var neck = top + 2 * r;
            var hip = top + fh * 0.62;
            var arms = top + fh * 0.36;
            var cx = center.X;
            yield return new EllipsePrim(new Vec2(cx, top + r), r, r, style with { Fill = Fill.Solid(Paper) }) { Tag = DiagTags.Outline };
            yield return new LinePrim(new Vec2(cx, neck), new Vec2(cx, hip), style) { Tag = DiagTags.Outline };
            yield return new LinePrim(new Vec2(cx - hw, arms), new Vec2(cx + hw, arms), style) { Tag = DiagTags.Outline };
            yield return Prim.Polyline(new[] { new Vec2(cx - hw * 0.85, top + fh), new Vec2(cx, hip), new Vec2(cx + hw * 0.85, top + fh) }, style) with { Tag = DiagTags.Outline };
        }

        // ------------------------------------------------------------------ ports

        /// <summary>Ports at the edge midpoints and quarter points of a rectangular box (local coordinates).</summary>
        public static IEnumerable<(string Id, Vec2 P)> RectPorts(Size2 s)
        {
            double hw = s.W / 2, hh = s.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("top.q1", new Vec2(-hw / 2, -hh));
            yield return ("top.q3", new Vec2(hw / 2, -hh));
            yield return ("right", new Vec2(hw, 0));
            yield return ("right.q1", new Vec2(hw, -hh / 2));
            yield return ("right.q3", new Vec2(hw, hh / 2));
            yield return ("bottom", new Vec2(0, hh));
            yield return ("bottom.q1", new Vec2(-hw / 2, hh));
            yield return ("bottom.q3", new Vec2(hw / 2, hh));
            yield return ("left", new Vec2(-hw, 0));
            yield return ("left.q1", new Vec2(-hw, -hh / 2));
            yield return ("left.q3", new Vec2(-hw, hh / 2));
        }

        /// <summary>Eight ports on an ellipse filling the box (cardinal and diagonal points of the outline).</summary>
        public static IEnumerable<(string Id, Vec2 P)> EllipsePorts(Size2 s)
        {
            double hw = s.W / 2, hh = s.H / 2;
            string[] names = { "right", "bottom.right", "bottom", "bottom.left", "left", "top.left", "top", "top.right" };
            for (var i = 0; i < 8; i++)
            {
                var a = i * Math.PI / 4;
                yield return (names[i], new Vec2(Math.Abs(Math.Cos(a)) < 1e-12 ? 0 : hw * Math.Cos(a), Math.Abs(Math.Sin(a)) < 1e-12 ? 0 : hh * Math.Sin(a)));
            }
        }

        /// <summary>Diamond vertices plus edge midpoints.</summary>
        public static IEnumerable<(string Id, Vec2 P)> DiamondPorts(Size2 s)
        {
            double hw = s.W / 2, hh = s.H / 2;
            yield return ("top", new Vec2(0, -hh));
            yield return ("right", new Vec2(hw, 0));
            yield return ("bottom", new Vec2(0, hh));
            yield return ("left", new Vec2(-hw, 0));
            yield return ("top.right", new Vec2(hw / 2, -hh / 2));
            yield return ("bottom.right", new Vec2(hw / 2, hh / 2));
            yield return ("bottom.left", new Vec2(-hw / 2, hh / 2));
            yield return ("top.left", new Vec2(-hw / 2, -hh / 2));
        }

        // ------------------------------------------------------------------ connector geometry

        public static DiagEnd ParseEnd(string? s, DiagEnd fallback)
            => Enum.TryParse<DiagEnd>(s ?? "", true, out var e) ? e : fallback;

        public static string EndName(DiagEnd e)
        {
            var s = e.ToString();
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        public static bool IsCrowsFoot(DiagEnd e) => e >= DiagEnd.One;

        /// <summary>Order in which the crow's-foot cycle action steps through the notations.</summary>
        public static readonly DiagEnd[] CrowCycle = { DiagEnd.One, DiagEnd.OnlyOne, DiagEnd.ZeroOrOne, DiagEnd.Many, DiagEnd.OneOrMany, DiagEnd.ZeroOrMany };

        /// <summary>
        /// Arrowhead size of connector ends. Large keeps the hollow UML heads and crow's feet readable on the thin
        /// (<see cref="LineWidth"/>) connector lines.
        /// </summary>
        public const ArrowSize HeadSize = ArrowSize.Large;

        /// <summary>The Core arrowhead that draws an end notation.</summary>
        public static ArrowHead Head(DiagEnd e) => e switch
        {
            DiagEnd.Open => ArrowHead.Open,
            DiagEnd.Filled => ArrowHead.Triangle,
            DiagEnd.Hollow => ArrowHead.TriangleOpen,
            DiagEnd.FilledDiamond => ArrowHead.Diamond,
            DiagEnd.HollowDiamond => ArrowHead.DiamondOpen,
            DiagEnd.One => ArrowHead.CrowOne,
            DiagEnd.OnlyOne => ArrowHead.CrowOneOnly,
            DiagEnd.ZeroOrOne => ArrowHead.CrowZeroOne,
            DiagEnd.Many => ArrowHead.CrowMany,
            DiagEnd.OneOrMany => ArrowHead.CrowOneMany,
            DiagEnd.ZeroOrMany => ArrowHead.CrowZeroMany,
            _ => ArrowHead.None,
        };

        /// <summary>Length of the end notation along the line at the default connector width (used to keep labels clear of it).</summary>
        public static double EndLength(DiagEnd e) => ArrowHeads.Extent(Head(e), HeadSize, LineWidth);

        /// <summary>
        /// Orthogonal route between two points. Ends attached to a box edge leave/enter perpendicular to that edge.
        /// </summary>
        public static List<Vec2> Orthogonal(Vec2 a, Vec2 b, bool? startHorizontal, bool? endHorizontal)
        {
            var d = b - a;
            if (Math.Abs(d.X) < 1e-9 || Math.Abs(d.Y) < 1e-9) return new List<Vec2> { a, b };
            var sh = startHorizontal ?? Math.Abs(d.X) >= Math.Abs(d.Y);
            var eh = endHorizontal ?? sh;
            if (sh && eh) return new List<Vec2> { a, new((a.X + b.X) / 2, a.Y), new((a.X + b.X) / 2, b.Y), b };
            if (!sh && !eh) return new List<Vec2> { a, new(a.X, (a.Y + b.Y) / 2), new(b.X, (a.Y + b.Y) / 2), b };
            return sh ? new List<Vec2> { a, new(b.X, a.Y), b } : new List<Vec2> { a, new(a.X, b.Y), b };
        }

        /// <summary>Whether an attached point sits on a vertical box edge (true), a horizontal edge (false) or neither (null).</summary>
        public static bool? ExitHorizontal(SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a is null) return null;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6 && Math.Abs(a.V) < 0.5 - 1e-6) return true;
            if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6) return false;
            return null;
        }

        /// <summary>Point and unit direction at a fraction of the total polyline length.</summary>
        public static (Vec2 P, Vec2 Dir) At(IReadOnlyList<Vec2> path, double t)
        {
            var total = 0.0;
            for (var i = 0; i + 1 < path.Count; i++) total += path[i].DistanceTo(path[i + 1]);
            var target = total * t;
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var l = path[i].DistanceTo(path[i + 1]);
                if (target <= l + 1e-12 || i + 2 == path.Count)
                    return (l < Geo.Eps ? path[i] : Vec2.Lerp(path[i], path[i + 1], Geo.Clamp(target / l, 0, 1)), (path[i + 1] - path[i]).Normalized());
                target -= l;
            }
            return (path[0], new Vec2(1, 0));
        }

        /// <summary>Side normal for labels: points "up" for horizontal lines and "right" for vertical ones.</summary>
        public static Vec2 LabelSide(Vec2 dir)
        {
            var n = dir.Perp;
            if (n == Vec2.Zero) return new Vec2(0, -1);
            if (n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X < 0)) n = -n;
            return n;
        }

        /// <summary>Half extent of a single-line text box along a direction (support function of the box).</summary>
        public static double HalfExtentAlong(string text, double size, Vec2 dir)
        {
            var d = dir.Normalized();
            return Math.Abs(d.X) * Width(text, size) / 2 + Math.Abs(d.Y) * size / 2;
        }

        /// <summary>Label next to a connector point, offset to the given side so it does not touch the line.</summary>
        public static IEnumerable<Primitive> SideLabel(string? text, Vec2 at, Vec2 side, double size, string tag = DiagTags.Label)
        {
            var lines = Lines(text);
            if (lines.Count == 0) yield break;
            var rowH = size * LineFactor;
            var h = lines.Count * rowH;
            var wMax = lines.Max(l => Width(l, size));
            var off = 1.2 + Math.Abs(side.X) * wMax / 2 + Math.Abs(side.Y) * h / 2;
            var c = at + side * off;
            var y0 = c.Y - (lines.Count - 1) * rowH / 2;
            for (var i = 0; i < lines.Count; i++)
                yield return Text(new Vec2(c.X, y0 + i * rowH), lines[i], size, tag);
        }

        // ------------------------------------------------------------------ style overrides

        /// <summary>
        /// Applies a user style override: the stroke colour recolours lines, texts, arrowheads and solid (ink) fills;
        /// fill, width and dash overrides apply only to outlines and connector lines; arrowhead overrides
        /// (start, end, size) only to connector lines.
        /// </summary>
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
            var main = p.Tag is DiagTags.Outline or DiagTags.Main;
            var connector = p.Tag == DiagTags.Main;
            var color = over.Stroke is string c && !string.Equals(c, "none", StringComparison.OrdinalIgnoreCase) ? c : null;
            return Prim.Restyle(p, s =>
            {
                var fill = s.Fill;
                if (s.Fill is { Kind: FillKind.Solid } f)
                {
                    if (color is not null && string.Equals(f.Color, Ink, StringComparison.OrdinalIgnoreCase)) fill = Fill.Solid(color);
                    else if (over.Fill is not null && main) fill = over.Fill;
                }
                return s with
                {
                    Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                    Opacity = over.Opacity ?? s.Opacity,
                    StrokeWidth = over.StrokeWidth is double w && main ? w : s.StrokeWidth,
                    Fill = fill,
                    Dash = main ? over.Dash ?? s.Dash : s.Dash,
                    ArrowStart = connector ? over.ArrowStart ?? s.ArrowStart : s.ArrowStart,
                    ArrowEnd = connector ? over.ArrowEnd ?? s.ArrowEnd : s.ArrowEnd,
                    ArrowSize = connector ? over.ArrowSize ?? s.ArrowSize : s.ArrowSize,
                };
            });
        }

        // ------------------------------------------------------------------ actions

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string key, bool fallback, string? group = null)
            => new(id, label, IconIds.Toggle, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group };

        public static ContextAction SetChoice(SketchObject o, string id, I18nText label, string icon, string key, string value, string fallback, string group)
            => new(id, label, icon, x => x.WithParam(key, value)) { IsActive = string.Equals(o.Str(key, fallback), value, StringComparison.OrdinalIgnoreCase), Group = group };

        /// <summary>Appends a line to a multi-line text parameter and grows the box downwards by one row.</summary>
        public static ContextAction AddRow(string id, I18nText label, string key, string fallback, string line, bool grow = true)
            => new(id, label, IconIds.Text, x =>
            {
                var cur = Lines(x.Str(key, fallback));
                var next = x.WithParam(key, string.Join("\n", cur.Concat(new[] { line })));
                return grow ? GrowDown(next, x.Num("textSize", TextSize) * LineFactor) : next;
            }) { Group = "rows" };

        /// <summary>Removes the last line of a multi-line text parameter and shrinks the box by one row (not below 8 mm).</summary>
        public static ContextAction RemoveRow(string id, I18nText label, string key, string fallback, bool grow = true)
            => new(id, label, IconIds.Delete, x =>
            {
                var cur = Lines(x.Str(key, fallback)).ToList();
                if (cur.Count == 0) return x;
                cur.RemoveAt(cur.Count - 1);
                var next = x.WithParam(key, string.Join("\n", cur));
                if (!grow) return next;
                var dh = -Math.Min(x.Num("textSize", TextSize) * LineFactor, Math.Max(0, x.Size.H - 8));
                return GrowDown(next, dh);
            }) { Group = "rows" };

        /// <summary>Changes the box height keeping the top edge in place (honours rotation).</summary>
        public static SketchObject GrowDown(SketchObject o, double dh)
        {
            if (o.IsPointGeometry || Math.Abs(dh) < 1e-12) return o;
            var shift = o.Matrix().ApplyLinear(new Vec2(0, dh / 2));
            return o with
            {
                Size = new Size2(o.Size.W, Math.Max(0.5, o.Size.H + dh)),
                Transform = o.Transform with { X = o.Transform.X + shift.X, Y = o.Transform.Y + shift.Y },
            };
        }

        /// <summary>Returns a copy with points (and attachments) in reverse order.</summary>
        public static SketchObject Reverse(SketchObject o) => Edits.ReversePoints(o);

        /// <summary>Swaps the values of two parameters (missing values stay missing).</summary>
        public static SketchObject SwapParams(SketchObject o, string a, string b)
        {
            var d = new Dictionary<string, ParamValue>(o.Params.Count);
            foreach (var kv in o.Params) d[kv.Key] = kv.Value;
            var hasA = o.Params.TryGetValue(a, out var va);
            var hasB = o.Params.TryGetValue(b, out var vb);
            d.Remove(a); d.Remove(b);
            if (hasB) d[a] = vb;
            if (hasA) d[b] = va;
            return o with { Params = d };
        }

        /// <summary>
        /// Attaches point <paramref name="index"/> of a connector to a box object at a page position (e.g. a snapped
        /// port). The attachment is stored in normalised box coordinates so it follows the target.
        /// </summary>
        public static SketchDocument AttachPoint(SketchDocument doc, string objectId, int index, string targetId, Vec2 pagePoint)
            => Edits.AttachPoint(doc, objectId, index, targetId, pagePoint);

        internal static string Inv(int i) => i.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One rendered text row of a compartment.</summary>
    public sealed record DiagRow(string Text)
    {
        /// <summary>Underlines the whole row (native font underline).</summary>
        public bool Underline { get; init; }
        public bool Italic { get; init; }
        public bool Bold { get; init; }
        /// <summary>Tag override (defaults to name for header rows and row otherwise).</summary>
        public string? Tag { get; init; }
    }

    /// <summary>Base class of all diagram object types.</summary>
    public abstract class DiagType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected DiagType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;

        public override Primitive ApplyOverride(Primitive p, Style over) => D.Override(p, over);

        protected static double TextSizeOf(SketchObject o) => o.Num("textSize", D.TextSize);
    }

    /// <summary>
    /// Box diagram symbol: renders in local coordinates centred at (0,0) and exposes connector ports
    /// (by default at the edge midpoints and quarter points).
    /// </summary>
    public abstract class DiagBoxType : DiagType
    {
        protected DiagBoxType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        public override bool HasText => true;

        /// <summary>Text shown while the object has no own text (localised, never stored).</summary>
        protected virtual I18nText? Placeholder => null;

        /// <summary>Object text or the localised placeholder.</summary>
        protected string Label(SketchObject o, RenderContext ctx) => o.Text ?? Placeholder?.Get(ctx.Lang) ?? "";

        /// <summary>Connector ports in local coordinates.</summary>
        public virtual IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.RectPorts(o.Size);

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            => Ports(o).Select(p => Anchor.Point(AnchorRole.Port, p.P, p.Id));
    }

    /// <summary>Point diagram object: renders in page coordinates from <see cref="SketchDocument.ResolvePoints"/>.</summary>
    public abstract class DiagPointType : DiagType
    {
        protected DiagPointType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        public override GeometryKind Geometry => GeometryKind.Points;
        public override bool HasText => true;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < 2) return Array.Empty<Primitive>();
            return RenderPoints(o, ctx, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < p.Count; i++) yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(p[i], p[i + 1], 0.5), "m" + D.Inv(i));
        }
    }

    /// <summary>
    /// Connector with end notations, optional orthogonal routing and labels. Two points give a straight or orthogonally
    /// routed line; more points are user waypoints (polyline).
    /// </summary>
    public abstract class DiagConnectorType : DiagPointType
    {
        protected DiagConnectorType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        protected abstract DiagEnd StartEnd(SketchObject o);
        protected abstract DiagEnd EndEnd(SketchObject o);
        protected virtual bool IsDashed(SketchObject o) => false;
        protected virtual double LabelSize(SketchObject o) => Math.Min(TextSizeOf(o), 3.2);

        /// <summary>Routed path through the resolved points.</summary>
        public List<Vec2> Path(SketchObject o, IReadOnlyList<Vec2> p)
        {
            if (p.Count == 2 && o.Str("route", "straight") == "orthogonal")
                return D.Orthogonal(p[0], p[1], D.ExitHorizontal(o, 0), D.ExitHorizontal(o, 1));
            var list = new List<Vec2> { p[0] };
            for (var i = 1; i < p.Count; i++) if (p[i].DistanceTo(list[list.Count - 1]) > 1e-9) list.Add(p[i]);
            if (list.Count == 1) list.Add(p[p.Count - 1]);
            return list;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var path = Path(o, p);
            var list = new List<Primitive>();
            var n = path.Count;
            if (path[0].DistanceTo(path[n - 1]) > 1e-9 || n > 2)
            {
                // End notations are Core arrowheads on the line style: every host draws them (natively where possible),
                // and user arrowhead overrides replace them.
                var se = D.Head(StartEnd(o));
                var ee = D.Head(EndEnd(o));
                var style = Style.ThinLine with
                {
                    StrokeWidth = D.LineWidth,
                    Dash = IsDashed(o) ? DashStyle.Dash : DashStyle.Solid,
                    ArrowStart = se == ArrowHead.None ? null : se,
                    ArrowEnd = ee == ArrowHead.None ? null : ee,
                    ArrowSize = se == ArrowHead.None && ee == ArrowHead.None ? null : D.HeadSize,
                };
                list.Add(n == 2 ? new LinePrim(path[0], path[1], style) { Tag = DiagTags.Main } : Prim.Polyline(path, style) with { Tag = DiagTags.Main });
            }
            Labels(list, o, ctx, path);
            return list;
        }

        /// <summary>
        /// How far an end notation reaches along the line, honouring the user's arrowhead, size and width overrides
        /// (labels are placed behind it).
        /// </summary>
        protected double EndClearance(SketchObject o, bool atStart)
        {
            var head = (atStart ? o.Style?.ArrowStart : o.Style?.ArrowEnd) ?? D.Head(atStart ? StartEnd(o) : EndEnd(o));
            return ArrowHeads.Extent(head, o.Style?.ArrowSize ?? D.HeadSize, o.Style?.StrokeWidth ?? D.LineWidth);
        }

        /// <summary>Adds the middle label (object text). Subclasses add end labels.</summary>
        protected virtual void Labels(List<Primitive> list, SketchObject o, RenderContext ctx, List<Vec2> path)
        {
            var (mid, dir) = D.At(path, 0.5);
            list.AddRange(D.SideLabel(o.Text, mid, D.LabelSide(dir), LabelSize(o)));
        }

        /// <summary>
        /// Text near one end: <paramref name="above"/> on the label side, otherwise on the opposite side;
        /// <paramref name="clearance"/> is the length of the end notation along the line (<see cref="EndClearance"/>).
        /// </summary>
        protected static IEnumerable<Primitive> EndLabel(string? text, List<Vec2> path, bool atStart, double clearance, bool above, double size)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<Primitive>();
            var n = path.Count;
            var tip = atStart ? path[0] : path[n - 1];
            var inward = atStart ? (path[1] - path[0]).Normalized() : (path[n - 2] - path[n - 1]).Normalized();
            var side = D.LabelSide(inward);
            if (!above) side = -side;
            var along = clearance * 0.5 + 2.0 + D.HalfExtentAlong(text!, size, inward);
            return D.SideLabel(text, tip + inward * along, side, size);
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) yield break;
            var path = Path(o, p);
            yield return Anchor.Point(AnchorRole.Mid, D.At(path, 0.5).P, "mid");
        }

        protected static ContextAction RouteAction(SketchObject o)
            => new("diag.route.orthogonal", new I18nText("Orthogonal routing", "Rechtwinklig führen"), IconIds.Toggle,
                x => x.WithParam("route", x.Str("route", "straight") == "orthogonal" ? "straight" : "orthogonal"))
            { IsActive = o.Str("route", "straight") == "orthogonal", Group = "route" };
    }
}
