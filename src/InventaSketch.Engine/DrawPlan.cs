using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// What a host can draw natively. The draw plan lowers everything else: hatches into line segments,
    /// arrowheads into separate (rotated) shapes, and objects that still cannot be expressed into one SVG op.
    /// </summary>
    public sealed record DrawCapabilities
    {
        /// <summary>Free paths with cubic Béziers (COM BuildFreeform, UNO PolyPolygonBezier).</summary>
        public bool Freeform { get; init; }
        /// <summary>Arrowheads as a line attribute.</summary>
        public bool Arrowheads { get; init; }
        /// <summary>Scalable vector hatch fill (LibreOffice FillHatch). False lowers hatches into line segments.</summary>
        public bool PatternFill { get; init; }
        /// <summary>Shape rotation (Office.js PowerPointApi 1.10).</summary>
        public bool Rotation { get; init; } = true;
        /// <summary>Host can insert an SVG picture as fallback.</summary>
        public bool Svg { get; init; } = true;
        /// <summary>Host can create shapes at all (Office.js PowerPointApi 1.4). False turns every unit into an SVG op.</summary>
        public bool Shapes { get; init; } = true;

        /// <summary>PowerPoint COM: everything except scalable hatch fills.</summary>
        public static readonly DrawCapabilities Full = new() { Freeform = true, Arrowheads = true, PatternFill = false, Rotation = true };

        /// <summary>Office.js with PowerPointApi 1.10: rects, ellipses, lines, text, rotation, groups.</summary>
        public static readonly DrawCapabilities OfficeJs = new() { Freeform = false, Arrowheads = false, PatternFill = false, Rotation = true };

        /// <summary>
        /// LibreOffice (UNO drawing API): PolyPolygonBezier freeforms, line-end markers (LineStart/LineEnd), scalable
        /// FillHatch, rotation, groups and text. The LibreOffice engine process uses it for drawPlan by default.
        /// </summary>
        public static readonly DrawCapabilities LibreOffice = new() { Freeform = true, Arrowheads = true, PatternFill = true, Rotation = true };
    }

    public sealed class PlanStroke
    {
        public string Color { get; set; } = "#000000";
        /// <summary>Line weight in pt.</summary>
        public double Weight { get; set; }
        /// <summary>solid, dash, dashDot, dashDotDot, dot, longDash.</summary>
        public string Dash { get; set; } = "solid";
        public string? ArrowStart { get; set; }
        public string? ArrowEnd { get; set; }
        public string? ArrowSize { get; set; }
    }

    public sealed class PlanHatch
    {
        /// <summary>Degrees, counter-clockwise on screen.</summary>
        public double Angle { get; set; }
        /// <summary>Spacing in pt.</summary>
        public double Spacing { get; set; }
        public bool Cross { get; set; }
        public string Color { get; set; } = "#000000";
        public double Weight { get; set; }
    }

    public sealed class PlanFill
    {
        /// <summary>none, solid or hatch (hatch only when the host declared PatternFill).</summary>
        public string Kind { get; set; } = "none";
        public string? Color { get; set; }
        public PlanHatch? Hatch { get; set; }
    }

    public sealed class PlanSeg
    {
        /// <summary>M, L, C or Z (quadratic segments are converted to cubic).</summary>
        public string Op { get; set; } = "L";
        public double X { get; set; }
        public double Y { get; set; }
        public double? X1 { get; set; }
        public double? Y1 { get; set; }
        public double? X2 { get; set; }
        public double? Y2 { get; set; }
    }

    /// <summary>
    /// One host drawing operation in slide/page points (1 mm = 72/25.4 pt), y down.
    /// op: rect | ellipse | shape | line | polyline | freeform | text | group | svg.
    /// Boxes (x, y, w, h) are unrotated; rotation (degrees clockwise) turns them around their centre.
    /// </summary>
    public sealed class PlanOp
    {
        public string Op { get; set; } = "";
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? W { get; set; }
        public double? H { get; set; }
        public double? X1 { get; set; }
        public double? Y1 { get; set; }
        public double? X2 { get; set; }
        public double? Y2 { get; set; }
        public double? Rotation { get; set; }
        /// <summary>Corner radius in pt (rect).</summary>
        public double? Radius { get; set; }
        /// <summary>Geometric shape name for op "shape": triangle, diamond.</summary>
        public string? Shape { get; set; }
        public List<double[]>? Points { get; set; }
        public List<PlanSeg>? Segments { get; set; }
        public bool? Closed { get; set; }
        public PlanStroke? Stroke { get; set; }
        public PlanFill? Fill { get; set; }
        public double? Opacity { get; set; }
        public string? Text { get; set; }
        /// <summary>Font size in pt.</summary>
        public double? FontSize { get; set; }
        public string? Font { get; set; }
        public bool? Bold { get; set; }
        public bool? Italic { get; set; }
        /// <summary>Single underline of the whole text (native font attribute on every host).</summary>
        public bool? Underline { get; set; }
        public string? Color { get; set; }
        /// <summary>left, center, right.</summary>
        public string? Align { get; set; }
        public List<PlanOp>? Children { get; set; }
        public string? Svg { get; set; }
        /// <summary>Semantic tag of the source primitive (e.g. "hatch", "arrowEnd").</summary>
        public string? Tag { get; set; }
    }

    public sealed class PlanRect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
    }

    /// <summary>
    /// One host object: a single sketch object or an assembly (objects sharing a group id). The host draws the ops,
    /// groups them and stores the metadata (id, type, payload) on the group.
    /// </summary>
    public sealed class PlanUnit
    {
        /// <summary>Unit id: the object id, or the group id of an assembly.</summary>
        public string Id { get; set; } = "";
        public List<string> ObjectIds { get; set; } = new();
        /// <summary>Type of the first object (assemblies: the type of the first member).</summary>
        public string Type { get; set; } = "";
        /// <summary>Suggested host shape name.</summary>
        public string Name { get; set; } = "";
        /// <summary>native (all ops are host shapes) or svg (one svg op).</summary>
        public string Mode { get; set; } = "native";
        /// <summary>Bounds of all ops in pt.</summary>
        public PlanRect Bounds { get; set; } = new();
        /// <summary>Compact model of the unit (DocumentJson.ToCompact of a one-page document).</summary>
        public string Payload { get; set; } = "";
        public List<PlanOp> Ops { get; set; } = new();
        /// <summary>Why the unit fell back to SVG or what was approximated.</summary>
        public List<string> Notes { get; set; } = new();
    }

    public sealed class DrawPlan
    {
        public string Unit { get; set; } = "pt";
        public double PageWidth { get; set; }
        public double PageHeight { get; set; }
        public int Revision { get; set; }
        public List<PlanUnit> Units { get; set; } = new();
    }

    /// <summary>Builds host draw plans from rendered objects.</summary>
    public static class DrawPlanBuilder
    {
        public const double Pt = Geo.PtPerMm;

        private static double R(double v) => Math.Round(v, 3);

        public static DrawPlan Build(EditorSession session, IReadOnlyList<string> ids, DrawCapabilities caps)
        {
            var doc = session.Doc;
            var page = session.Page;
            var plan = new DrawPlan { PageWidth = R(page.Size.W * Pt), PageHeight = R(page.Size.H * Pt), Revision = session.Revision };
            var wanted = new HashSet<string>(ids);
            var done = new HashSet<string>();
            foreach (var o in doc.ObjectsOn(page))
            {
                if (!wanted.Contains(o.Id) || done.Contains(o.Id)) continue;
                var members = o.GroupId is null
                    ? new List<SketchObject> { o }
                    : doc.ObjectsOn(page).Where(x => x.GroupId == o.GroupId).ToList();
                foreach (var m in members) done.Add(m.Id);
                var unit = BuildUnit(session, members, caps);
                if (unit is not null) plan.Units.Add(unit);
            }
            return plan;
        }

        public static PlanUnit? BuildUnit(EditorSession session, IReadOnlyList<SketchObject> members, DrawCapabilities caps)
        {
            var registry = session.Registry;
            var ctx = session.Context();
            var first = members[0];
            var unit = new PlanUnit
            {
                Id = members.Count > 1 && first.GroupId is not null ? first.GroupId : first.Id,
                ObjectIds = members.Select(m => m.Id).ToList(),
                Type = first.Type,
                Payload = EngineApi.CompactOf(session.Doc, session.Page, members.Select(m => m.Id)),
            };
            unit.Name = "ISK:" + unit.Type + ":" + unit.Id;
            var needSvg = !caps.Shapes;
            if (needSvg) unit.Notes.Add("host has no shape API");
            var groups = new List<PlanOp>();
            var allPrims = new List<Primitive>();
            foreach (var o in members)
            {
                var type = registry.Type(o.Type);
                if (type is null) { unit.Notes.Add($"{o.Id}: unknown type '{o.Type}' skipped"); continue; }
                // Line group and the object's own style, as every renderer draws it.
                var local = registry.RenderLocal(o, ctx);
                var m = o.Matrix();
                allPrims.AddRange(local.Select(p => o.IsPointGeometry ? p : Prim.Transform(p, m)));
                if (!type.PreferNative) { needSvg = true; unit.Notes.Add($"{o.Type}: type prefers a picture"); }
                var lowering = new Lowering(caps, unit.Notes);
                foreach (var p in Prim.Flatten(local)) lowering.Add(p, m);
                if (lowering.NeedSvg) needSvg = true;
                groups.Add(new PlanOp { Op = "group", Tag = o.Id, Children = lowering.Ops });
            }
            if (allPrims.Count == 0) return null;
            var bounds = Prim.Bounds(allPrims) ?? default;
            var maxStroke = Prim.Flatten(allPrims).Select(StyleOf).Where(s => s is not null).Select(s => s!.EffectiveWidth).DefaultIfEmpty(0.5).Max();
            var hasArrows = Prim.Flatten(allPrims).Select(StyleOf).Any(s => s is not null && ((s.ArrowStart ?? ArrowHead.None) != ArrowHead.None || (s.ArrowEnd ?? ArrowHead.None) != ArrowHead.None));
            var pad = maxStroke / 2 + (hasArrows ? ArrowHeads.Length(ArrowSize.Large, maxStroke) : 0) + 0.5;
            var padded = bounds.Inflate(pad);

            var mono = session.Doc.Settings.Monochrome;
            if (needSvg && caps.Svg)
            {
                unit.Mode = "svg";
                var svg = SvgRenderer.RenderPrimitives(allPrims, new SvgOptions { Background = null, ViewBox = padded, Monochrome = mono });
                svg = svg.Replace("<svg xmlns=\"http://www.w3.org/2000/svg\" ",
                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" + F(padded.W) + "mm\" height=\"" + F(padded.H) + "mm\" ");
                unit.Ops.Add(new PlanOp { Op = "svg", X = R(padded.X * Pt), Y = R(padded.Y * Pt), W = R(padded.W * Pt), H = R(padded.H * Pt), Svg = svg });
                unit.Bounds = new PlanRect { X = R(padded.X * Pt), Y = R(padded.Y * Pt), W = R(padded.W * Pt), H = R(padded.H * Pt) };
            }
            else
            {
                unit.Ops.AddRange(groups.Count == 1 ? groups[0].Children! : groups);
                unit.Bounds = new PlanRect { X = R(bounds.X * Pt), Y = R(bounds.Y * Pt), W = R(bounds.W * Pt), H = R(bounds.H * Pt) };
                if (mono) Monochrome(unit.Ops);
            }
            return unit;
        }

        /// <summary>
        /// Black-and-white plan (<see cref="DocumentSettings.Monochrome"/>): lines, hatches and text become black (white
        /// stays white), opaque fills black or white by luminance, translucent fills (highlighters, tints) are dropped.
        /// </summary>
        public static void Monochrome(IEnumerable<PlanOp> ops)
        {
            foreach (var op in ops)
            {
                if (op.Stroke is { } s) s.Color = MonoInk(s.Color);
                if (op.Color is not null) op.Color = MonoInk(op.Color);
                if (op.Fill is { } f)
                {
                    if (f.Hatch is { } h)
                    {
                        h.Color = MonoInk(h.Color);
                        f.Color = f.Color is null || op.Opacity is < 1 ? null : MonoFill(f.Color);
                    }
                    else if (f.Kind == "solid" && f.Color is not null)
                    {
                        if (op.Opacity is < 1) op.Fill = new PlanFill();
                        else f.Color = MonoFill(f.Color);
                    }
                }
                if (op.Opacity is < 1) op.Opacity = null;
                if (op.Children is not null) Monochrome(op.Children);
            }
        }

        private static double Luminance(string color)
        {
            var hex = color.TrimStart('#');
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
            if (hex.Length < 6 || !int.TryParse(hex.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return 0;
            return 0.2126 * ((v >> 16) & 255) + 0.7152 * ((v >> 8) & 255) + 0.0722 * (v & 255);
        }

        private static string MonoInk(string color) => Luminance(color) > 245 ? "#FFFFFF" : "#000000";
        private static string MonoFill(string color) => Luminance(color) < 90 ? "#000000" : "#FFFFFF";

        private static string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// Heads a host with <see cref="DrawCapabilities.Arrowheads"/> draws as a line-end attribute (PowerPoint
        /// msoArrowhead*, OOXML a:headEnd/a:tailEnd): triangle, open, stealth, diamond and oval exist natively, tick and
        /// half are approximated by open and triangle. Hollow triangle/diamond and crow's-foot ends have no native line
        /// end and are always lowered to explicit shapes.
        /// </summary>
        public static bool HasNativeLineEnd(ArrowHead head) => head is ArrowHead.Triangle or ArrowHead.Open or ArrowHead.Stealth
            or ArrowHead.Diamond or ArrowHead.Oval or ArrowHead.Tick or ArrowHead.Half;

        /// <summary>
        /// Native line-end length per line weight as hosts map the plan sizes (COM: small → medium, medium and large →
        /// long). PowerPoint's line ends are about 3× (medium) and 5× (long) the line weight.
        /// </summary>
        public static double NativeHeadLength(ArrowSize size, double strokeWidth) => strokeWidth * (size == ArrowSize.Small ? 3 : 5);

        /// <summary>
        /// True if a native line end is at least 65 % of the engine's head length. Thin lines (where the engine keeps a
        /// minimum head size) and diamonds (1.5× longer) therefore get drawn heads.
        /// </summary>
        public static bool NativeHeadFits(ArrowHead head, ArrowSize size, double strokeWidth)
            => NativeHeadLength(size, strokeWidth) >= 0.65 * ArrowHeads.Extent(head, size, strokeWidth);

        internal static Style? StyleOf(Primitive p) => p switch
        {
            LinePrim l => l.Style,
            PathPrim pa => pa.Style,
            EllipsePrim e => e.Style,
            RectPrim r => r.Style,
            _ => null,
        };

        /// <summary>Lowers primitives (local coordinates + matrix) into plan ops for the given capabilities.</summary>
        private sealed class Lowering
        {
            private readonly DrawCapabilities _caps;
            private readonly List<string> _notes;

            public Lowering(DrawCapabilities caps, List<string> notes) { _caps = caps; _notes = notes; }

            public List<PlanOp> Ops { get; } = new();
            public bool NeedSvg { get; private set; }

            private void Fail(string why)
            {
                NeedSvg = true;
                if (!_notes.Contains(why)) _notes.Add(why);
            }

            private void Note(string what)
            {
                if (!_notes.Contains(what)) _notes.Add(what);
            }

            public void Add(Primitive local, Matrix2D m)
            {
                switch (local)
                {
                    case LinePrim l:
                        AddLine(m.Apply(l.A), m.Apply(l.B), l.Style, l.Tag);
                        break;
                    case RectPrim r:
                        AddBox("rect", local, new Vec2(r.X + r.W / 2, r.Y + r.H / 2), r.W, r.H, r.Style, m, r.Radius);
                        break;
                    case EllipsePrim e:
                        AddBox("ellipse", local, e.Center, 2 * e.Rx, 2 * e.Ry, e.Style, m, 0);
                        break;
                    case PathPrim p:
                        AddPath((PathPrim)Prim.Transform(p, m));
                        break;
                    case TextPrim t:
                        AddText((TextPrim)Prim.Transform(t, m));
                        break;
                }
            }

            private static PlanStroke? Stroke(Style s)
            {
                if (!s.HasStroke) return null;
                return new PlanStroke
                {
                    Color = s.EffectiveStroke,
                    Weight = R(s.EffectiveWidth * Pt),
                    Dash = Camel(s.EffectiveDash.ToString()),
                };
            }

            private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

            private PlanFill FillOf(Style s, Primitive pageShape)
            {
                var f = s.EffectiveFill;
                switch (f.Kind)
                {
                    case FillKind.Solid:
                        return new PlanFill { Kind = "solid", Color = f.Color };
                    case FillKind.Hatch when f.Hatch is not null:
                        {
                            var color = f.Hatch.Color ?? s.EffectiveStroke;
                            var weight = f.Hatch.Width ?? Math.Max(0.18, s.EffectiveWidth * 0.5);
                            if (_caps.PatternFill)
                                return new PlanFill
                                {
                                    Kind = "hatch", Color = f.Color,
                                    Hatch = new PlanHatch { Angle = f.Hatch.Angle, Spacing = R(f.Hatch.Spacing * Pt), Cross = f.Hatch.Cross, Color = color, Weight = R(weight * Pt) },
                                };
                            // Explicit hatch lines (exact ISO-like hatch on every host).
                            var segs = Hatching.Segments(Hatching.Contours(pageShape), f.Hatch);
                            var hatch = new PlanOp { Op = "group", Tag = "hatch", Children = new List<PlanOp>() };
                            var stroke = new PlanStroke { Color = color, Weight = R(weight * Pt), Dash = "solid" };
                            foreach (var (a, b) in segs) hatch.Children.Add(LineOp(a, b, stroke, null));
                            if (hatch.Children.Count > 0) Ops.Add(hatch);
                            return f.Color is not null ? new PlanFill { Kind = "solid", Color = f.Color } : new PlanFill();
                        }
                    case FillKind.Dots:
                        Fail("dot fill needs a picture");
                        return new PlanFill();
                    default:
                        return new PlanFill();
                }
            }

            private static PlanOp LineOp(Vec2 a, Vec2 b, PlanStroke? stroke, string? tag) => new()
            {
                Op = "line", X1 = R(a.X * Pt), Y1 = R(a.Y * Pt), X2 = R(b.X * Pt), Y2 = R(b.Y * Pt), Stroke = stroke, Tag = tag,
            };

            private void AddBox(string kind, Primitive local, Vec2 localCentre, double w, double h, Style s, Matrix2D m, double radius)
            {
                var c = m.Apply(localCentre);
                var rot = Geo.NormDeg(Math.Round(m.RotationDegrees, 6));
                if (rot >= 180) rot -= 180; // rectangles and ellipses are point-symmetric
                if (Math.Abs(rot - 180) < 1e-6 || rot < 1e-6) rot = 0;
                var pageShape = Prim.Transform(local, m);
                if (rot != 0 && !_caps.Rotation)
                {
                    if (_caps.Freeform && pageShape is PathPrim pp) { AddPath(pp); return; }
                    Fail("rotated shape needs PowerPointApi 1.10 (rotation)");
                    return;
                }
                // Fill first: hatch lines are inserted below the outline.
                var fill = FillOf(s, pageShape);
                var op = new PlanOp
                {
                    Op = kind,
                    X = R((c.X - w / 2) * Pt), Y = R((c.Y - h / 2) * Pt), W = R(w * Pt), H = R(h * Pt),
                    Rotation = rot == 0 ? null : R(rot),
                    Radius = radius > 0 ? R(radius * Pt) : null,
                    Stroke = Stroke(s), Fill = fill, Opacity = s.Opacity, Tag = local.Tag,
                };
                Ops.Add(op);
            }

            private void AddText(TextPrim t)
            {
                var ts = t.TextStyle;
                if (Math.Abs(t.Rotation) > 1e-6 && !_caps.Rotation) { Fail("rotated text needs PowerPointApi 1.10 (rotation)"); return; }
                var lines = t.Text.Replace("\r", "").Split('\n');
                var w = Math.Max(1, lines.Max(l => l.Length)) * ts.Size * 0.6 + ts.Size * 0.4;
                var h = lines.Length * ts.Size * 1.2;
                var x = ts.Anchor switch { TextAnchor.Middle => t.Position.X - w / 2, TextAnchor.End => t.Position.X - w, _ => t.Position.X };
                var y = ts.Baseline switch { TextBaseline.Top => t.Position.Y, TextBaseline.Bottom or TextBaseline.Alphabetic => t.Position.Y - h, _ => t.Position.Y - h / 2 };
                Ops.Add(new PlanOp
                {
                    Op = "text", X = R(x * Pt), Y = R(y * Pt), W = R(w * Pt), H = R(h * Pt),
                    Rotation = Math.Abs(t.Rotation) > 1e-6 ? R(Geo.NormDeg(t.Rotation)) : null,
                    Text = t.Text, FontSize = R(ts.Size * Pt), Font = ts.Font, Bold = ts.Bold ? true : null, Italic = ts.Italic ? true : null,
                    Underline = ts.Underline ? true : null,
                    Color = ts.Color ?? "#000000",
                    Align = ts.Anchor switch { TextAnchor.Start => "left", TextAnchor.End => "right", _ => "center" },
                    Tag = t.Tag,
                });
            }

            private void AddLine(Vec2 a, Vec2 b, Style s, string? tag)
            {
                var stroke = Stroke(s);
                if (stroke is null) return;
                var start = s.ArrowStart ?? ArrowHead.None;
                var end = s.ArrowEnd ?? ArrowHead.None;
                Vec2 a0 = a, b0 = b;
                if (end != ArrowHead.None)
                {
                    if (Native(end, s)) stroke.ArrowEnd = Camel(end.ToString());
                    else b = Head(end, b0, (b0 - a0).Normalized(), s, "arrowEnd");
                }
                if (start != ArrowHead.None)
                {
                    if (Native(start, s)) stroke.ArrowStart = Camel(start.ToString());
                    else a = Head(start, a0, (a0 - b0).Normalized(), s, "arrowStart");
                }
                if (stroke.ArrowStart is not null || stroke.ArrowEnd is not null) stroke.ArrowSize = Camel((s.ArrowSize ?? ArrowSize.Medium).ToString());
                Ops.Add(LineOp(a, b, stroke, tag));
            }

            /// <summary>
            /// True if the host draws this head as a line-end attribute. Native heads scale with the line weight only, so
            /// they are used only where they reach the engine's head size (see <see cref="NativeHeadFits"/>); otherwise
            /// the head is drawn as shapes and matches the other heads of the drawing.
            /// </summary>
            private bool Native(ArrowHead head, Style s) => _caps.Arrowheads && HasNativeLineEnd(head)
                && NativeHeadFits(head, s.ArrowSize ?? ArrowSize.Medium, s.EffectiveWidth);

            private static PlanFill SimpleFill(Style s)
                => s.EffectiveFill is { Kind: FillKind.Solid, Color: not null } f ? new PlanFill { Kind = "solid", Color = f.Color } : new PlanFill();

            /// <summary>
            /// Emits an arrowhead as separate shapes; returns the shortened line end. With freeform shapes the head is drawn
            /// with its exact geometry; otherwise triangles and diamonds become rotated geometric shapes and everything else
            /// (open heads, ticks, crow's feet) lines and ellipses.
            /// </summary>
            private Vec2 Head(ArrowHead head, Vec2 tip, Vec2 dir, Style s, string tag)
            {
                var size = s.ArrowSize ?? ArrowSize.Medium;
                var width = s.EffectiveWidth;
                var color = s.EffectiveStroke;
                var (prims, inset) = ArrowHeads.Build(head, tip, dir, size, color, width);
                var shaftEnd = tip - dir * inset;
                if (_caps.Arrowheads) Note(HasNativeLineEnd(head) ? $"arrowhead '{Camel(head.ToString())}' drawn as shapes: the native line end would be smaller at this line weight" : $"arrowhead '{Camel(head.ToString())}' has no native line end: drawn as shapes");
                if (!_caps.Freeform && head is ArrowHead.Triangle or ArrowHead.Stealth or ArrowHead.Half or ArrowHead.Diamond or ArrowHead.TriangleOpen or ArrowHead.DiamondOpen
                    && prims.Count == 1 && prims[0] is PathPrim poly)
                {
                    if (head is ArrowHead.Stealth or ArrowHead.Half) Note($"arrowhead '{Camel(head.ToString())}' approximated by a triangle");
                    // Office "triangle"/"diamond": apex at the top centre; rotate so the apex points along dir.
                    var deg = Geo.NormDeg(Geo.RadToDeg(Math.Atan2(dir.X, -dir.Y)));
                    if (Math.Abs(deg) > 1e-6 && !_caps.Rotation) { Fail("arrowheads need shape rotation or a picture"); return shaftEnd; }
                    // Box of the head: length along dir, half width across it (from the exact polygon).
                    var n = dir.Perp;
                    var pts = poly.Segments.Where(x => x.Op != SegOp.Z).Select(x => x.P).ToList();
                    var l = pts.Max(p => (tip - p).Dot(dir));
                    var hw = pts.Max(p => Math.Abs((p - tip).Dot(n)));
                    var c = tip - dir * (l / 2);
                    var diamond = head is ArrowHead.Diamond or ArrowHead.DiamondOpen;
                    Ops.Add(new PlanOp
                    {
                        Op = "shape", Shape = diamond ? "diamond" : "triangle",
                        X = R((c.X - hw) * Pt), Y = R((c.Y - l / 2) * Pt), W = R(2 * hw * Pt), H = R(l * Pt),
                        Rotation = Math.Abs(deg) > 1e-6 ? R(deg) : null,
                        Stroke = new PlanStroke { Color = color, Weight = R(width * Pt), Dash = "solid" }, Fill = SimpleFill(poly.Style), Tag = tag,
                    });
                    return diamond ? tip - dir * l : shaftEnd;
                }
                foreach (var p in prims)
                {
                    switch (p)
                    {
                        case LinePrim ln: Ops.Add(LineOp(ln.A, ln.B, Stroke(ln.Style), tag)); break;
                        case EllipsePrim e:
                            Ops.Add(new PlanOp
                            {
                                Op = "ellipse", X = R((e.Center.X - e.Rx) * Pt), Y = R((e.Center.Y - e.Ry) * Pt), W = R(2 * e.Rx * Pt), H = R(2 * e.Ry * Pt),
                                Stroke = Stroke(e.Style), Fill = SimpleFill(e.Style), Tag = tag,
                            });
                            break;
                        case PathPrim pa when _caps.Freeform:
                            Ops.Add(new PlanOp
                            {
                                Op = "polyline", Points = pa.Segments.Where(x => x.Op != SegOp.Z).Select(x => new[] { R(x.P.X * Pt), R(x.P.Y * Pt) }).ToList(),
                                Closed = pa.IsClosed, Stroke = Stroke(pa.Style), Fill = SimpleFill(pa.Style), Tag = tag,
                            });
                            break;
                        case PathPrim pa:
                            for (var i = 1; i < pa.Segments.Count; i++)
                            {
                                var seg = pa.Segments[i];
                                if (seg.Op == SegOp.L) Ops.Add(LineOp(pa.Segments[i - 1].P, seg.P, Stroke(pa.Style), tag));
                                else if (seg.Op == SegOp.Z) Ops.Add(LineOp(pa.Segments[i - 1].P, pa.Segments[0].P, Stroke(pa.Style), tag));
                            }
                            break;
                    }
                }
                return shaftEnd;
            }

            private void AddPath(PathPrim p)
            {
                var s = p.Style;
                var hasCurves = p.Segments.Any(x => x.Op is SegOp.C or SegOp.Q);
                var fill = FillOf(s, p);
                var start = s.ArrowStart ?? ArrowHead.None;
                var end = s.ArrowEnd ?? ArrowHead.None;
                var arrows = !p.IsClosed && (start != ArrowHead.None || end != ArrowHead.None);
                if (_caps.Freeform)
                {
                    var stroke = Stroke(s);
                    var segs = p.Segments.ToList();
                    if (arrows && stroke is not null) segs = LowerPathArrows(segs, s, stroke);
                    if (!hasCurves && segs.Count(x => x.Op == SegOp.M) == 1)
                        Ops.Add(new PlanOp { Op = "polyline", Points = segs.Where(x => x.Op != SegOp.Z).Select(x => new[] { R(x.P.X * Pt), R(x.P.Y * Pt) }).ToList(), Closed = p.IsClosed, Stroke = stroke, Fill = fill, Opacity = s.Opacity, Tag = p.Tag });
                    else
                        Ops.Add(new PlanOp { Op = "freeform", Segments = Segments(segs), Closed = p.IsClosed, Stroke = stroke, Fill = fill, Opacity = s.Opacity, Tag = p.Tag });
                    return;
                }
                if (hasCurves) { Fail("curves need freeform shapes or a picture"); return; }
                if (fill.Kind == "solid") { Fail("filled polygons need freeform shapes or a picture"); return; }
                var st = Stroke(s);
                if (st is null) return;
                var list = p.Segments.ToList();
                if (arrows) list = LowerPathArrows(list, s, null);
                // Open/closed polyline as straight line shapes (dash phase restarts per segment).
                Vec2? subStart = null, prev = null;
                foreach (var seg in list)
                {
                    switch (seg.Op)
                    {
                        case SegOp.M: subStart = prev = seg.P; break;
                        case SegOp.L:
                            if (prev is Vec2 a) Ops.Add(LineOp(a, seg.P, Clone(st), p.Tag));
                            prev = seg.P;
                            break;
                        case SegOp.Z:
                            if (prev is Vec2 z && subStart is Vec2 s0 && (z - s0).Length > Geo.Eps) Ops.Add(LineOp(z, s0, Clone(st), p.Tag));
                            prev = subStart;
                            break;
                    }
                }
                if (list.Count > 2) Note("polyline drawn as separate line shapes");
            }

            private static PlanStroke Clone(PlanStroke s) => new() { Color = s.Color, Weight = s.Weight, Dash = s.Dash };

            /// <summary>
            /// Lowers path arrowheads into shapes. Heads with a native line end are set on <paramref name="nativeStroke"/>
            /// instead (when given and the host supports arrowheads).
            /// </summary>
            private List<PathSeg> LowerPathArrows(List<PathSeg> segs, Style s, PlanStroke? nativeStroke)
            {
                if (segs.Count < 2) return segs;
                var end = s.ArrowEnd ?? ArrowHead.None;
                var start = s.ArrowStart ?? ArrowHead.None;
                if (nativeStroke is not null)
                {
                    if (end != ArrowHead.None && Native(end, s)) { nativeStroke.ArrowEnd = Camel(end.ToString()); end = ArrowHead.None; }
                    if (start != ArrowHead.None && Native(start, s)) { nativeStroke.ArrowStart = Camel(start.ToString()); start = ArrowHead.None; }
                    if (nativeStroke.ArrowStart is not null || nativeStroke.ArrowEnd is not null) nativeStroke.ArrowSize = Camel((s.ArrowSize ?? ArrowSize.Medium).ToString());
                }
                if (end != ArrowHead.None)
                {
                    var last = segs[segs.Count - 1];
                    var prev = segs[segs.Count - 2].P;
                    var from = last.Op switch { SegOp.C => last.C2, SegOp.Q => last.C1, _ => prev };
                    if ((last.P - from).Length < Geo.Eps) from = prev;
                    var tip = Head(end, last.P, (last.P - from).Normalized(), s, "arrowEnd");
                    segs[segs.Count - 1] = last with { P = tip };
                }
                if (start != ArrowHead.None)
                {
                    var first = segs[0];
                    var next = segs[1];
                    var to = next.Op is SegOp.C or SegOp.Q ? next.C1 : next.P;
                    if ((to - first.P).Length < Geo.Eps) to = next.P;
                    var tip = Head(start, first.P, (first.P - to).Normalized(), s, "arrowStart");
                    segs[0] = first with { P = tip };
                }
                return segs;
            }

            private static List<PlanSeg> Segments(IEnumerable<PathSeg> segs)
            {
                var list = new List<PlanSeg>();
                var last = Vec2.Zero;
                foreach (var s in segs)
                {
                    switch (s.Op)
                    {
                        case SegOp.M: list.Add(new PlanSeg { Op = "M", X = R(s.P.X * Pt), Y = R(s.P.Y * Pt) }); last = s.P; break;
                        case SegOp.L: list.Add(new PlanSeg { Op = "L", X = R(s.P.X * Pt), Y = R(s.P.Y * Pt) }); last = s.P; break;
                        case SegOp.C:
                            list.Add(new PlanSeg { Op = "C", X1 = R(s.C1.X * Pt), Y1 = R(s.C1.Y * Pt), X2 = R(s.C2.X * Pt), Y2 = R(s.C2.Y * Pt), X = R(s.P.X * Pt), Y = R(s.P.Y * Pt) });
                            last = s.P;
                            break;
                        case SegOp.Q:
                            {
                                // Exact quadratic-to-cubic elevation.
                                var c1 = last + (s.C1 - last) * (2.0 / 3);
                                var c2 = s.P + (s.C1 - s.P) * (2.0 / 3);
                                list.Add(new PlanSeg { Op = "C", X1 = R(c1.X * Pt), Y1 = R(c1.Y * Pt), X2 = R(c2.X * Pt), Y2 = R(c2.Y * Pt), X = R(s.P.X * Pt), Y = R(s.P.Y * Pt) });
                                last = s.P;
                                break;
                            }
                        case SegOp.Z: list.Add(new PlanSeg { Op = "Z" }); break;
                    }
                }
                return list;
            }
        }
    }
}
