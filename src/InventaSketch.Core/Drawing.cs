using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Host-neutral drawing primitives. Object types render into these; the SVG renderer and the
    // host adapters (PowerPoint COM, Office.js, LibreOffice UNO) consume them. Every primitive kind
    // must be implementable by every host, natively or through a documented fallback.
    // =============================================================================================

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DashStyle { Solid, Dash, DashDot, DashDotDot, Dot, LongDash }

    /// <summary>
    /// Line end decorations. Members are persisted by name; new members are appended at the end.
    /// <see cref="TriangleOpen"/> and <see cref="DiamondOpen"/> are hollow (UML generalisation/realisation and
    /// aggregation). The <c>Crow*</c> members are crow's-foot (information engineering) cardinality ends for ER
    /// diagrams; the tip is the point where the line meets the entity.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ArrowHead
    {
        None, Triangle, Open, Stealth, Diamond, Oval, Tick, Half,
        /// <summary>Hollow triangle (UML generalisation, realisation).</summary>
        TriangleOpen,
        /// <summary>Hollow diamond (UML aggregation).</summary>
        DiamondOpen,
        /// <summary>Crow's foot: one (single bar).</summary>
        CrowOne,
        /// <summary>Crow's foot: many (foot).</summary>
        CrowMany,
        /// <summary>Crow's foot: zero or one (circle and bar).</summary>
        CrowZeroOne,
        /// <summary>Crow's foot: one and only one (double bar).</summary>
        CrowOneOnly,
        /// <summary>Crow's foot: zero or many (circle and foot).</summary>
        CrowZeroMany,
        /// <summary>Crow's foot: one or many (bar and foot).</summary>
        CrowOneMany,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ArrowSize { Small, Medium, Large }

    /// <summary>Semantic line role inspired by ISO 128 (used for monochrome/patent output and default weights).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LineRole { Visible, Thin, Hidden, Center, Phantom, Helper, Cut }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FillKind { None, Solid, Hatch, Dots }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TextAnchor { Start, Middle, End }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TextBaseline { Middle, Top, Bottom, Alphabetic }

    /// <summary>Hatch definition: line family at Angle (degrees, counter-clockwise on screen) with Spacing (mm).</summary>
    public sealed record HatchPattern(double Angle = 45, double Spacing = 2.0, bool Cross = false, string? Color = null, double? Width = null);

    public sealed record Fill
    {
        public FillKind Kind { get; init; }
        public string? Color { get; init; }
        public HatchPattern? Hatch { get; init; }
        /// <summary>Dot spacing in mm for <see cref="FillKind.Dots"/>.</summary>
        public double? DotSpacing { get; init; }

        public static readonly Fill None = new() { Kind = FillKind.None };
        public static Fill Solid(string color) => new() { Kind = FillKind.Solid, Color = color };
        public static Fill Hatched(HatchPattern pattern, string? background = null) => new() { Kind = FillKind.Hatch, Hatch = pattern, Color = background };
        public static Fill Dotted(double spacing = 1.5) => new() { Kind = FillKind.Dots, DotSpacing = spacing };
    }

    /// <summary>Stroke/fill style. Null members mean "inherit/default".</summary>
    public sealed record Style
    {
        /// <summary>Stroke colour (#rrggbb). "none" disables the stroke.</summary>
        public string? Stroke { get; init; }
        /// <summary>Stroke width in mm.</summary>
        public double? StrokeWidth { get; init; }
        public DashStyle? Dash { get; init; }
        public Fill? Fill { get; init; }
        public ArrowHead? ArrowStart { get; init; }
        public ArrowHead? ArrowEnd { get; init; }
        public ArrowSize? ArrowSize { get; init; }
        public double? Opacity { get; init; }
        public LineRole? Role { get; init; }

        public static readonly Style Default = new();

        /// <summary>Returns a style where non-null members of <paramref name="over"/> win.</summary>
        public Style Merge(Style? over) => over is null ? this : new Style
        {
            Stroke = over.Stroke ?? Stroke,
            StrokeWidth = over.StrokeWidth ?? StrokeWidth,
            Dash = over.Dash ?? Dash,
            Fill = over.Fill ?? Fill,
            ArrowStart = over.ArrowStart ?? ArrowStart,
            ArrowEnd = over.ArrowEnd ?? ArrowEnd,
            ArrowSize = over.ArrowSize ?? ArrowSize,
            Opacity = over.Opacity ?? Opacity,
            Role = over.Role ?? Role,
        };

        public static Style Of(LineRole role) => new() { Role = role };
        public static readonly Style Visible = Of(LineRole.Visible);
        public static readonly Style ThinLine = Of(LineRole.Thin);
        public static readonly Style Hidden = Of(LineRole.Hidden);
        public static readonly Style CenterLine = Of(LineRole.Center);
        public static readonly Style Helper = Of(LineRole.Helper);

        /// <summary>Default stroke width per role (mm), following ISO 128 line group 0.5.</summary>
        public static double RoleWidth(LineRole role) => role switch
        {
            LineRole.Visible => 0.5,
            LineRole.Cut => 0.7,
            LineRole.Hidden => 0.35,
            LineRole.Helper => 0.18,
            _ => 0.25,
        };

        public static DashStyle RoleDash(LineRole role) => role switch
        {
            LineRole.Hidden => DashStyle.Dash,
            LineRole.Center => DashStyle.DashDot,
            LineRole.Cut => DashStyle.DashDot,
            LineRole.Phantom => DashStyle.DashDotDot,
            LineRole.Helper => DashStyle.Dot,
            _ => DashStyle.Solid,
        };

        [JsonIgnore] public string EffectiveStroke => Stroke ?? "#000000";
        [JsonIgnore] public double EffectiveWidth => StrokeWidth ?? RoleWidth(Role ?? LineRole.Visible);
        [JsonIgnore] public DashStyle EffectiveDash => Dash ?? RoleDash(Role ?? LineRole.Visible);
        [JsonIgnore] public Fill EffectiveFill => Fill ?? Fill.None;
        [JsonIgnore] public bool HasStroke => !string.Equals(Stroke, "none", StringComparison.OrdinalIgnoreCase);

        /// <summary>Dash pattern as multiples of mm, scaled with the stroke width.</summary>
        public static double[]? DashArray(DashStyle dash, double width)
        {
            var w = Math.Max(width, 0.18);
            return dash switch
            {
                DashStyle.Dash => new[] { 6 * w, 3 * w },
                DashStyle.LongDash => new[] { 12 * w, 4 * w },
                DashStyle.Dot => new[] { w, 2.5 * w },
                DashStyle.DashDot => new[] { 16 * w, 3 * w, 2 * w, 3 * w },
                DashStyle.DashDotDot => new[] { 16 * w, 3 * w, 2 * w, 3 * w, 2 * w, 3 * w },
                _ => null,
            };
        }
    }

    public sealed record TextStyle
    {
        /// <summary>Font size in mm (em height). 3.5 mm ≈ 10 pt.</summary>
        public double Size { get; init; } = 3.5;
        public string? Font { get; init; }
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        /// <summary>Underlines the whole text (all lines); hosts use their native font underline.</summary>
        public bool Underline { get; init; }
        public string? Color { get; init; }
        public TextAnchor Anchor { get; init; } = TextAnchor.Middle;
        public TextBaseline Baseline { get; init; } = TextBaseline.Middle;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SegOp { M, L, C, Q, Z }

    /// <summary>Path segment. Unused points are default.</summary>
    public readonly record struct PathSeg(SegOp Op, Vec2 P, Vec2 C1 = default, Vec2 C2 = default)
    {
        public static PathSeg Move(Vec2 p) => new(SegOp.M, p);
        public static PathSeg Line(Vec2 p) => new(SegOp.L, p);
        public static PathSeg Cubic(Vec2 c1, Vec2 c2, Vec2 p) => new(SegOp.C, p, c1, c2);
        public static PathSeg Quad(Vec2 c, Vec2 p) => new(SegOp.Q, p, c);
        public static readonly PathSeg Close = new(SegOp.Z, default);
        public PathSeg Transform(Matrix2D m) => Op == SegOp.Z ? this : new PathSeg(Op, m.Apply(P), Op is SegOp.C or SegOp.Q ? m.Apply(C1) : default, Op == SegOp.C ? m.Apply(C2) : default);
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(LinePrim), "line")]
    [JsonDerivedType(typeof(PathPrim), "path")]
    [JsonDerivedType(typeof(EllipsePrim), "ellipse")]
    [JsonDerivedType(typeof(RectPrim), "rect")]
    [JsonDerivedType(typeof(TextPrim), "text")]
    [JsonDerivedType(typeof(GroupPrim), "group")]
    public abstract record Primitive
    {
        /// <summary>Optional semantic tag (e.g. "pitch", "tip", "hub") for tests and host naming.</summary>
        public string? Tag { get; init; }
    }

    public sealed record LinePrim(Vec2 A, Vec2 B, Style Style) : Primitive;
    public sealed record PathPrim(IReadOnlyList<PathSeg> Segments, Style Style) : Primitive
    {
        [JsonIgnore] public bool IsClosed => Segments.Count > 0 && Segments[Segments.Count - 1].Op == SegOp.Z;
    }
    public sealed record EllipsePrim(Vec2 Center, double Rx, double Ry, Style Style) : Primitive;
    public sealed record RectPrim(double X, double Y, double W, double H, Style Style, double Radius = 0) : Primitive;
    public sealed record TextPrim(Vec2 Position, string Text, TextStyle TextStyle, double Rotation = 0) : Primitive;
    public sealed record GroupPrim(IReadOnlyList<Primitive> Children) : Primitive;

    /// <summary>Builders and operations for primitives.</summary>
    public static class Prim
    {
        public static LinePrim Line(Vec2 a, Vec2 b, Style? style = null) => new(a, b, style ?? Style.Visible);
        public static LinePrim Line(double x1, double y1, double x2, double y2, Style? style = null) => Line(new Vec2(x1, y1), new Vec2(x2, y2), style);
        public static RectPrim Rect(double x, double y, double w, double h, Style? style = null, double radius = 0) => new(x, y, w, h, style ?? Style.Visible, radius);
        public static RectPrim CRect(double cx, double cy, double w, double h, Style? style = null) => Rect(cx - w / 2, cy - h / 2, w, h, style);
        public static EllipsePrim Ellipse(double cx, double cy, double rx, double ry, Style? style = null) => new(new Vec2(cx, cy), rx, ry, style ?? Style.Visible);
        public static EllipsePrim Circle(double cx, double cy, double r, Style? style = null) => Ellipse(cx, cy, r, r, style);
        public static TextPrim Text(double x, double y, string text, TextStyle? ts = null, double rotation = 0) => new(new Vec2(x, y), text, ts ?? new TextStyle(), rotation);
        public static GroupPrim Group(params Primitive[] children) => new(children);
        public static GroupPrim Group(IEnumerable<Primitive> children) => new(children.ToList());

        public static PathPrim Polyline(IEnumerable<Vec2> points, Style? style = null, bool closed = false)
        {
            var segs = new List<PathSeg>();
            foreach (var p in points) segs.Add(segs.Count == 0 ? PathSeg.Move(p) : PathSeg.Line(p));
            if (closed) segs.Add(PathSeg.Close);
            return new PathPrim(segs, style ?? Style.Visible);
        }

        public static PathPrim Polygon(IEnumerable<Vec2> points, Style? style = null) => Polyline(points, style, closed: true);

        public static PathPrim Path(IEnumerable<PathSeg> segments, Style? style = null) => new(segments.ToList(), style ?? Style.Visible);

        /// <summary>Elliptical arc as a polyline (robust across all hosts).</summary>
        public static PathPrim Arc(double cx, double cy, double rx, double ry, double startRad, double sweepRad, Style? style = null)
            => Polyline(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, startRad, sweepRad), style);

        private const double Kappa = 0.5522847498307936;

        public static List<PathSeg> EllipseSegments(Vec2 c, double rx, double ry)
        {
            double kx = rx * Kappa, ky = ry * Kappa, cx = c.X, cy = c.Y;
            return new List<PathSeg>
            {
                PathSeg.Move(new(cx + rx, cy)),
                PathSeg.Cubic(new(cx + rx, cy + ky), new(cx + kx, cy + ry), new(cx, cy + ry)),
                PathSeg.Cubic(new(cx - kx, cy + ry), new(cx - rx, cy + ky), new(cx - rx, cy)),
                PathSeg.Cubic(new(cx - rx, cy - ky), new(cx - kx, cy - ry), new(cx, cy - ry)),
                PathSeg.Cubic(new(cx + kx, cy - ry), new(cx + rx, cy - ky), new(cx + rx, cy)),
                PathSeg.Close,
            };
        }

        /// <summary>Applies an affine transform exactly (rotated rectangles/ellipses become paths).</summary>
        public static Primitive Transform(Primitive p, Matrix2D m)
        {
            switch (p)
            {
                case LinePrim l:
                    return l with { A = m.Apply(l.A), B = m.Apply(l.B) };
                case RectPrim r when m.IsAxisAligned && r.Radius <= 0:
                    {
                        var a = m.Apply(new Vec2(r.X, r.Y));
                        var b = m.Apply(new Vec2(r.X + r.W, r.Y + r.H));
                        return r with { X = Math.Min(a.X, b.X), Y = Math.Min(a.Y, b.Y), W = Math.Abs(b.X - a.X), H = Math.Abs(b.Y - a.Y) };
                    }
                case RectPrim r:
                    return new PathPrim(new[] { new Vec2(r.X, r.Y), new Vec2(r.X + r.W, r.Y), new Vec2(r.X + r.W, r.Y + r.H), new Vec2(r.X, r.Y + r.H) }
                        .Select((v, i) => i == 0 ? PathSeg.Move(m.Apply(v)) : PathSeg.Line(m.Apply(v))).Concat(new[] { PathSeg.Close }).ToList(), r.Style) { Tag = r.Tag };
                case EllipsePrim e when m.IsAxisAligned:
                    return e with { Center = m.Apply(e.Center), Rx = Math.Abs(e.Rx * m.A), Ry = Math.Abs(e.Ry * m.D) };
                case EllipsePrim e:
                    return new PathPrim(EllipseSegments(e.Center, e.Rx, e.Ry).Select(s => s.Transform(m)).ToList(), e.Style) { Tag = e.Tag };
                case TextPrim t:
                    {
                        var mirrored = m.Determinant < 0;
                        var rot = t.Rotation + (mirrored ? 0 : m.RotationDegrees);
                        return t with { Position = m.Apply(t.Position), Rotation = Math.Abs(rot) < 1e-9 ? 0 : rot };
                    }
                case PathPrim path:
                    return path with { Segments = path.Segments.Select(s => s.Transform(m)).ToList() };
                case GroupPrim g:
                    return g with { Children = g.Children.Select(c => Transform(c, m)).ToList() };
                default:
                    throw new NotSupportedException(p.GetType().Name);
            }
        }

        /// <summary>Points relevant for bounds (control points included, text approximated).</summary>
        public static IEnumerable<Vec2> Points(Primitive p)
        {
            switch (p)
            {
                case LinePrim l: yield return l.A; yield return l.B; break;
                case RectPrim r: yield return new Vec2(r.X, r.Y); yield return new Vec2(r.X + r.W, r.Y + r.H); break;
                case EllipsePrim e: yield return new Vec2(e.Center.X - e.Rx, e.Center.Y - e.Ry); yield return new Vec2(e.Center.X + e.Rx, e.Center.Y + e.Ry); break;
                case TextPrim t:
                    {
                        var w = Math.Max(1, t.Text.Length) * t.TextStyle.Size * 0.55;
                        var h = t.TextStyle.Size;
                        var x0 = t.TextStyle.Anchor switch { TextAnchor.Middle => t.Position.X - w / 2, TextAnchor.End => t.Position.X - w, _ => t.Position.X };
                        yield return new Vec2(x0, t.Position.Y - h / 2);
                        yield return new Vec2(x0 + w, t.Position.Y + h / 2);
                        break;
                    }
                case PathPrim path:
                    foreach (var s in path.Segments)
                    {
                        if (s.Op == SegOp.Z) continue;
                        yield return s.P;
                        if (s.Op is SegOp.C or SegOp.Q) yield return s.C1;
                        if (s.Op == SegOp.C) yield return s.C2;
                    }
                    break;
                case GroupPrim g:
                    foreach (var c in g.Children) foreach (var q in Points(c)) yield return q;
                    break;
            }
        }

        public static Rect? Bounds(IEnumerable<Primitive> list)
        {
            var pts = list.SelectMany(Points).ToList();
            return pts.Count == 0 ? null : Core.Rect.FromPoints(pts);
        }

        public static IEnumerable<Primitive> Flatten(IEnumerable<Primitive> list)
        {
            foreach (var p in list)
            {
                if (p is GroupPrim g) { foreach (var c in Flatten(g.Children)) yield return c; }
                else yield return p;
            }
        }

        /// <summary>Applies a style transformation to every primitive (e.g. user colour overrides).</summary>
        public static Primitive Restyle(Primitive p, Func<Style, Style> f) => p switch
        {
            LinePrim l => l with { Style = f(l.Style) },
            PathPrim pa => pa with { Style = f(pa.Style) },
            EllipsePrim e => e with { Style = f(e.Style) },
            RectPrim r => r with { Style = f(r.Style) },
            GroupPrim g => g with { Children = g.Children.Select(c => Restyle(c, f)).ToList() },
            _ => p,
        };
    }
}
