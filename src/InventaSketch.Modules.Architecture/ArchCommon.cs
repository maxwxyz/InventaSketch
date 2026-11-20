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

namespace InventaSketch.Modules.Architecture
{
    /// <summary>Library category ids of the architecture module.</summary>
    public static class ArchCategories
    {
        public const string Structure = "arch.structure";
        public const string Openings = "arch.openings";
        public const string Stairs = "arch.stairs";
        public const string Furniture = "arch.furniture";
        public const string Sanitary = "arch.sanitary";
        public const string Services = "arch.services";
        public const string Site = "arch.site";
    }

    /// <summary>Semantic primitive tags (tests, host naming).</summary>
    public static class ArchTags
    {
        /// <summary>Wall face line (cut edge of a wall).</summary>
        public const string WallFace = "wall-face";
        /// <summary>Hatched/filled wall core between the faces.</summary>
        public const string WallFill = "wall-fill";
        /// <summary>Wall end line at a free end.</summary>
        public const string WallCap = "wall-cap";
        /// <summary>White band that hides the wall inside an opening.</summary>
        public const string Mask = "mask";
        public const string Jamb = "jamb";
        public const string Leaf = "leaf";
        public const string Swing = "swing";
        public const string Glass = "glass";
        public const string Sill = "sill";
        public const string Tread = "tread";
        public const string WalkLine = "walk-line";
        public const string RoomName = "room-name";
        public const string RoomArea = "room-area";
        public const string Outline = "outline";
        public const string Label = "label";
    }

    /// <summary>Shared line styles (ISO 128 inspired roles; cut walls are drawn with the wide continuous line).</summary>
    internal static class St
    {
        /// <summary>Cut wall faces (wide).</summary>
        public static readonly Style Wall = Style.Visible;
        /// <summary>Cut faces of light partitions.</summary>
        public static readonly Style WallLight = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Visible edges below the cut plane (furniture, stairs, sanitary).</summary>
        public static readonly Style Thin = Style.ThinLine;
        /// <summary>Very fine lines (door swings, hatching details, glass).</summary>
        public static readonly Style Fine = Style.ThinLine with { StrokeWidth = 0.18 };
        /// <summary>Medium lines (door leaves, window frames).</summary>
        public static readonly Style Medium = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Edges above the cut plane / hidden (beams, lintels).</summary>
        public static readonly Style Hidden = Style.Hidden with { StrokeWidth = 0.25 };
        /// <summary>Axes (walking line centre, hanger rods).</summary>
        public static readonly Style Center = Style.CenterLine with { StrokeWidth = 0.18 };
        /// <summary>Opaque white band without outline (hides the wall inside an opening).</summary>
        public static readonly Style Mask = new() { Stroke = "none", Fill = Fill.Solid("#ffffff") };
        /// <summary>Solid black (reinforced concrete, filled arrowheads).</summary>
        public static readonly Style Black = Style.ThinLine with { Fill = Fill.Solid("#000000") };
        /// <summary>White-filled thin outline (objects lying on top of others).</summary>
        public static readonly Style ThinWhite = Style.ThinLine with { Fill = Fill.Solid("#ffffff") };
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class Pm
    {
        public static ParamDef Num(string key, string en, string de, double def, double? min = null, double? max = null, string? unit = "mm", double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def };

        public static ParamDef Text(string key, string en, string de, string def)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        /// <summary>Plan scale denominator (100 means 1:100). Converts real millimetres into drawing millimetres.</summary>
        public static readonly ParamDef Scale = Num("scale", "Plan scale 1:", "Planmaßstab 1:", Arch.DefaultScale, 1, 10000, null, 5, advanced: true);
    }

    /// <summary>Shared helpers of the architecture module.</summary>
    public static class Arch
    {
        /// <summary>Default plan scale denominator (1:100). Library sizes are authored for this scale.</summary>
        public const double DefaultScale = 100;

        /// <summary>Approximate advance width per character relative to the em size (matches <see cref="Prim.Points"/>).</summary>
        internal const double CharWidth = 0.55;

        /// <summary>Plan scale denominator of an object (falls back to 1:100).</summary>
        public static double ScaleOf(SketchObject o)
        {
            var s = o.Num("scale", DefaultScale);
            return s > Geo.Eps ? s : DefaultScale;
        }

        /// <summary>Area in square metres of a polygon given in page millimetres at plan scale 1:<paramref name="scale"/>.</summary>
        public static double RoomArea(IReadOnlyList<Vec2> pagePoints, double scale) => RoomType.AreaM2(pagePoints, scale);

        /// <summary>Real width in millimetres of a door or window (box width times plan scale).</summary>
        public static double OpeningWidth(SketchObject o) => Openings.RealWidth(o);

        /// <summary>Formats a number with fixed decimals, using a decimal comma in German.</summary>
        public static string Format(double v, int decimals, Lang lang)
        {
            var s = Math.Round(v, decimals).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            if (s.StartsWith("-", StringComparison.Ordinal) && s.Trim('-', '0', '.') == "") s = s.Substring(1);
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>Intersection of two infinite lines (point + direction); <paramref name="fallback"/> if parallel.</summary>
        internal static Vec2 Intersect(Vec2 a, Vec2 da, Vec2 b, Vec2 db, Vec2 fallback)
        {
            var den = da.Cross(db);
            if (Math.Abs(den) < 1e-9) return fallback;
            var t = (b - a).Cross(db) / den;
            return a + da * t;
        }

        internal static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b) => Geo.ClosestOnSegment(p, a, b).Point.DistanceTo(p);

        /// <summary>Font size that fits <paramref name="text"/> into a width/height budget.</summary>
        internal static double FitText(string text, double preferred, double maxW, double maxH)
        {
            var len = Math.Max(1, (text ?? "").Length);
            return Math.Max(0.1, Math.Min(preferred, Math.Min(maxW / (len * CharWidth), maxH)));
        }

        internal static TextPrim Text(Vec2 at, string text, double size, string? tag = null, bool bold = false, TextAnchor anchor = TextAnchor.Middle, bool italic = false)
            => new(at, text, new TextStyle { Size = size, Bold = bold, Italic = italic, Anchor = anchor }) { Tag = tag };

        /// <summary>Style override: text gets the stroke colour, masks and stroke-less fills keep their look.</summary>
        internal static Primitive Override(Primitive p, Style over)
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
            if (p.Tag == ArchTags.Mask) return p;
            return Prim.Restyle(p, s => s with
            {
                Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                Opacity = over.Opacity ?? s.Opacity,
                StrokeWidth = over.StrokeWidth is double w && s.HasStroke && (s.Role ?? LineRole.Visible) is LineRole.Visible ? w : s.StrokeWidth,
                Fill = over.Fill is not null && s.Fill is not null && s.Fill.Kind != FillKind.None ? over.Fill : s.Fill,
                Dash = over.Dash ?? s.Dash,
            });
        }

        internal static ContextAction Toggle(SketchObject o, string id, I18nText label, string icon, string key, bool fallback, string group = "param")
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group };

        /// <summary>Cycles a choice parameter through <paramref name="values"/>; the label names the next value.</summary>
        internal static ContextAction Cycle(SketchObject o, string id, string key, string fallback, IReadOnlyList<(string Value, I18nText Label)> values, string group = "param")
        {
            var cur = o.Str(key, fallback);
            var idx = Math.Max(0, values.ToList().FindIndex(v => v.Value == cur));
            var next = values[(idx + 1) % values.Count];
            return new ContextAction(id, next.Label, IconIds.Cycle, x => x.WithParam(key, next.Value)) { Group = group };
        }

        /// <summary>Clips a convex or concave polygon to the half-plane (p - origin)·normal &lt;= 0 (Sutherland-Hodgman).</summary>
        internal static List<Vec2> ClipHalfPlane(IReadOnlyList<Vec2> poly, Vec2 origin, Vec2 normal)
        {
            var res = new List<Vec2>();
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                var da = (a - origin).Dot(normal); var db = (b - origin).Dot(normal);
                if (da <= 0) res.Add(a);
                if ((da < 0 && db > 0) || (da > 0 && db < 0)) res.Add(Vec2.Lerp(a, b, da / (da - db)));
            }
            return res;
        }
    }

    /// <summary>
    /// Local drawing surface of a box object: the box is centred at (0,0) with the actual size W x H, so every
    /// drawing stays inside its box under free (also non-uniform) scaling.
    /// </summary>
    internal sealed class Canvas
    {
        private readonly List<Primitive> _items = new();

        public Canvas(Size2 size)
        {
            W = Math.Max(size.W, 0.01);
            H = Math.Max(size.H, 0.01);
        }

        public double W { get; }
        public double H { get; }
        public double Hw => W / 2;
        public double Hh => H / 2;
        /// <summary>Smaller box side (detail scale).</summary>
        public double K => Math.Min(W, H);
        public IReadOnlyList<Primitive> Items => _items;

        /// <summary>Normalised point (u, v in [-0.5, 0.5]) to local millimetres.</summary>
        public Vec2 N(double u, double v) => new(u * W, v * H);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(Vec2 a, Vec2 b, Style s, string? tag = null) => Add(Prim.Line(a, b, s), tag);

        public void LineN(double u0, double v0, double u1, double v1, Style s, string? tag = null) => Line(N(u0, v0), N(u1, v1), s, tag);

        /// <summary>Axis-aligned rectangle between two local corners.</summary>
        public void Rect(double x0, double y0, double x1, double y1, Style s, string? tag = null)
            => Add(Prim.Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), s), tag);

        public void RectN(double u0, double v0, double u1, double v1, Style s, string? tag = null) => Rect(u0 * W, v0 * H, u1 * W, v1 * H, s, tag);

        public void Poly(IEnumerable<Vec2> pts, Style s, bool closed, string? tag = null) => Add(Prim.Polyline(pts, s, closed), tag);

        public void Ellipse(Vec2 c, double rx, double ry, Style s, string? tag = null) => Add(Prim.Ellipse(c.X, c.Y, Math.Max(rx, 0.01), Math.Max(ry, 0.01), s), tag);

        /// <summary>Elliptical arc; angles in degrees, positive sweep clockwise on screen.</summary>
        public void Arc(Vec2 c, double rx, double ry, double startDeg, double sweepDeg, Style s, string? tag = null)
            => Add(Prim.Arc(c.X, c.Y, rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg), s), tag);

        /// <summary>Rounded rectangle as a closed path with cubic corners (stays rounded under host transforms).</summary>
        public void RoundRect(double x0, double y0, double x1, double y1, double r, Style s, string? tag = null)
        {
            double x = Math.Min(x0, x1), y = Math.Min(y0, y1), w = Math.Abs(x1 - x0), h = Math.Abs(y1 - y0);
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            const double k = 0.5522847498307936;
            double xr = x + w, yb = y + h, c = r * (1 - k);
            Add(new PathPrim(new[]
            {
                PathSeg.Move(new Vec2(x + r, y)), PathSeg.Line(new Vec2(xr - r, y)),
                PathSeg.Cubic(new Vec2(xr - c, y), new Vec2(xr, y + c), new Vec2(xr, y + r)), PathSeg.Line(new Vec2(xr, yb - r)),
                PathSeg.Cubic(new Vec2(xr, yb - c), new Vec2(xr - c, yb), new Vec2(xr - r, yb)), PathSeg.Line(new Vec2(x + r, yb)),
                PathSeg.Cubic(new Vec2(x + c, yb), new Vec2(x, yb - c), new Vec2(x, yb - r)), PathSeg.Line(new Vec2(x, y + r)),
                PathSeg.Cubic(new Vec2(x, y + c), new Vec2(x + c, y), new Vec2(x + r, y)), PathSeg.Close,
            }, s), tag);
        }

        /// <summary>Text fitted into a width/height budget.</summary>
        public void Text(Vec2 at, string text, double preferred, double maxW, double maxH, string? tag = null, bool bold = false, TextAnchor anchor = TextAnchor.Middle)
        {
            if (string.IsNullOrEmpty(text)) return;
            Add(Arch.Text(at, text, Arch.FitText(text, preferred, maxW, maxH), tag, bold, anchor));
        }
    }

    internal delegate void DrawFn(Canvas c, SketchObject o, RenderContext ctx);

    /// <summary>
    /// Data-driven box object type used for openings, stairs, furniture, sanitary, services and site symbols.
    /// The plan view is always present; an optional elevation view can be added.
    /// </summary>
    internal sealed class BoxType : ObjectType
    {
        public static readonly ViewDef PlanView = new("plan", new I18nText("Plan", "Grundriss")) { Icon = IconIds.Plan };
        public static readonly ViewDef ElevationView = new("elevation", new I18nText("Elevation", "Ansicht")) { Icon = IconIds.Front };

        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;
        private readonly Size2 _size;
        private readonly DrawFn _plan;

        public BoxType(string id, I18nText name, string category, Size2 size, DrawFn plan, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _size = size; _plan = plan; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override Size2 DefaultSize => _size;
        public override IReadOnlyList<ViewDef> Views => Elevation is null ? new[] { PlanView } : new[] { PlanView, ElevationView };
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;
        public override bool HasText => TextEditable;

        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        public DrawFn? Elevation { get; init; }
        /// <summary>Exposes a Hub anchor at the box centre (snaps onto wall and beam axes).</summary>
        public bool Hub { get; init; }
        public bool Uniform { get; init; }
        public bool TextEditable { get; init; }
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var c = new Canvas(o.Size);
            if (Elevation is not null && ViewOf(o) == ElevationView.Id) Elevation(c, o, ctx);
            else _plan(c, o, ctx);
            return c.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            if (Hub) yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero, "wall");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => Extra is null ? Array.Empty<ContextAction>() : Extra(o);

        public override Primitive ApplyOverride(Primitive p, Style over) => Arch.Override(p, over);
    }

    /// <summary>Base of point-geometry types (walls, rooms, beams, pipes, boundaries): renders in page coordinates.</summary>
    internal abstract class PointType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected PointType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<ViewDef> Views => new[] { BoxType.PlanView };

        protected virtual int MinPoints => 2;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            return pts.Count < MinPoints ? Array.Empty<Primitive>() : RenderPoints(o, ctx, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        public override Primitive ApplyOverride(Primitive p, Style over) => Arch.Override(p, over);
    }
}
