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

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>Library category ids of the building services module.</summary>
    public static class BsCategories
    {
        public const string Heating = "bs.heating";
        public const string Plumbing = "bs.plumbing";
        public const string Ventilation = "bs.ventilation";
        public const string Electrical = "bs.electrical";
    }

    /// <summary>View ids shared by every element of the module (plan, section/elevation, schematic/riser).</summary>
    public static class BsViews
    {
        public const string Plan = "plan";
        public const string Section = "section";
        public const string Schematic = "schematic";

        /// <summary>The three views in their canonical order.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Plan, Section, Schematic };

        public static readonly ViewDef PlanView = new(Plan, new I18nText("Plan", "Grundriss")) { Icon = IconIds.Plan };
        public static readonly ViewDef SectionView = new(Section, new I18nText("Section / elevation", "Schnitt / Ansicht")) { Icon = IconIds.Front };
        public static readonly ViewDef SchematicView = new(Schematic, new I18nText("Schematic (riser)", "Schema (Strang)")) { Icon = IconIds.Schematic };

        /// <summary>Plan and section/schematic share the x axis; section and schematic place elements by their mounting height.</summary>
        public static bool IsElevation(string view) => view == Section || view == Schematic;
    }

    /// <summary>Semantic primitive tags (tests, host naming).</summary>
    public static class BsTags
    {
        public const string Outline = "outline";
        public const string Pipe = "pipe";
        public const string Duct = "duct";
        public const string Label = "label";
        public const string Junction = "junction";
        public const string Stub = "stub";
    }

    /// <summary>Shared line styles (ISO 128 inspired roles).</summary>
    internal static class St
    {
        /// <summary>Visible edges of equipment in plan and section.</summary>
        public static readonly Style Thin = Style.ThinLine;
        /// <summary>Very fine lines (ribs, inner details).</summary>
        public static readonly Style Fine = Style.ThinLine with { StrokeWidth = 0.18 };
        /// <summary>Schematic symbol lines.</summary>
        public static readonly Style Medium = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Concealed parts (cabinets, frames behind a cover).</summary>
        public static readonly Style Hidden = Style.Hidden with { StrokeWidth = 0.18 };
        /// <summary>Axes.</summary>
        public static readonly Style Center = Style.CenterLine with { StrokeWidth = 0.18 };
        /// <summary>White-filled thin outline (parts lying on top of others).</summary>
        public static readonly Style ThinWhite = Style.ThinLine with { Fill = Fill.Solid("#ffffff") };
        /// <summary>White-filled medium outline (schematic parts lying on top of lines).</summary>
        public static readonly Style MediumWhite = Medium with { Fill = Fill.Solid("#ffffff") };
        /// <summary>Solid black (arrowheads, dots).</summary>
        public static readonly Style Black = Style.ThinLine with { StrokeWidth = 0.1, Fill = Fill.Solid("#000000") };
        /// <summary>Floor and ceiling lines in section views.</summary>
        public static readonly Style Floor = Style.Visible with { StrokeWidth = 0.35 };
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

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        /// <summary>
        /// Mounting height in mm: the bottom edge of the element above the finished floor (negative = in or below the floor).
        /// The section and schematic views place the element by it (<see cref="ServiceViews"/>).
        /// </summary>
        public static ParamDef Height(double def)
            => Num(HeightKey, "Mounting height (bottom edge above floor)", "Montagehöhe (Unterkante über OKFF)", def, -5000, 30000, "mm", 10);

        public const string HeightKey = "height";

        /// <summary>
        /// Plan scale denominator of one object (100 means 1:100), as in the architecture module. No default on purpose: unset,
        /// the document plan scale applies (<see cref="DocumentSettings.PlanScale"/>, else 1:100).
        /// </summary>
        public static readonly ParamDef Scale = new("scale", new I18nText("Plan scale 1: (this object)", "Planmaßstab 1: (dieses Objekt)"), ParamKind.Number)
        {
            Min = 1, Max = 10000, Step = 5, Advanced = true,
        };
    }

    /// <summary>Shared helpers of the building services module.</summary>
    public static class Bs
    {
        /// <summary>Default plan scale denominator (1:100). Library sizes are authored for this scale.</summary>
        public const double DefaultScale = 100;

        /// <summary>Approximate advance width per character relative to the em size.</summary>
        internal const double CharWidth = 0.55;

        /// <summary>
        /// Plan scale denominator of an object: its own "scale" parameter, else the document plan scale, else 1:100
        /// (the same resolution as in the architecture module).
        /// </summary>
        public static double ScaleOf(SketchObject o, SketchDocument? doc)
        {
            var own = o.Num("scale", 0);
            if (own > Geo.Eps) return own;
            return doc?.Settings.PlanScale is double s && s > Geo.Eps ? s : DefaultScale;
        }

        /// <summary>Mounting height in mm (bottom edge above the finished floor) of an element; the type default when unset.</summary>
        public static double HeightOf(SketchObject o, ObjectType? type)
        {
            var def = type?.Parameters.FirstOrDefault(p => p.Key == Pm.HeightKey)?.Default?.Number ?? 0;
            return o.Num(Pm.HeightKey, def);
        }

        /// <summary>Reads a length in millimetres typed by the user ("1010", "1010 mm", "1,01 m", "101 cm", "-50").</summary>
        internal static double ParseMm(string text, Lang lang)
            => ValueParser.ParseMillimetres(text, lang);

        internal static string Mm(double v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>Font size that fits <paramref name="text"/> into a width/height budget.</summary>
        internal static double FitText(string text, double preferred, double maxW, double maxH)
        {
            var len = Math.Max(1, (text ?? "").Length);
            return Math.Max(0.1, Math.Min(preferred, Math.Min(maxW / (len * CharWidth), maxH)));
        }

        internal static TextPrim Text(Vec2 at, string text, double size, string? tag = null, bool bold = false, TextAnchor anchor = TextAnchor.Middle)
            => new(at, text, new TextStyle { Size = size, Bold = bold, Anchor = anchor }) { Tag = tag };

        /// <summary>Style override: text gets the stroke colour, white fills keep their look.</summary>
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
            return Prim.Restyle(p, s => s with
            {
                Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                Opacity = over.Opacity ?? s.Opacity,
                StrokeWidth = over.StrokeWidth is double w && s.HasStroke && (s.Role ?? LineRole.Visible) is LineRole.Visible ? w : s.StrokeWidth,
                Fill = over.Fill is not null && s.Fill is not null && s.Fill.Kind != FillKind.None && s.Fill.Color != "#ffffff" ? over.Fill : s.Fill,
                Dash = over.Dash ?? s.Dash,
            });
        }

        internal static ContextAction Toggle(SketchObject o, string id, I18nText label, string icon, string key, bool fallback, string group = "param")
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group, ParamKeys = new[] { key } };

        /// <summary>Cycles a choice parameter through its options; the label names the next value.</summary>
        internal static ContextAction Cycle(SketchObject o, string id, ParamDef choice, string group = "param")
        {
            var values = choice.Options!;
            var fallback = choice.Default?.Text ?? values[0].Value;
            var cur = o.Str(choice.Key, fallback);
            var idx = Math.Max(0, values.ToList().FindIndex(v => v.Value == cur));
            var next = values[(idx + 1) % values.Count];
            return new ContextAction(id, next.Label, IconIds.Cycle, x => x.WithParam(choice.Key, next.Value)) { Group = group, ParamKeys = new[] { choice.Key } };
        }

        /// <summary>"Mounting height…" prompt (mm).</summary>
        internal static ContextAction HeightAction(SketchObject o, ObjectType type, Lang lang)
            => ContextAction.WithInput("bs.height.set", new I18nText("Mounting height…", "Montagehöhe…"), IconIds.QtyHeight,
                new I18nText("Mounting height, bottom edge above floor (mm)", "Montagehöhe, Unterkante über OKFF (mm)"), Mm(HeightOf(o, type)),
                (x, text) => x.WithParam(Pm.HeightKey, Math.Round(ParseMm(text, lang), 1)), "mm") with { Group = "height", ParamKeys = new[] { Pm.HeightKey } };
    }

    /// <summary>
    /// Local drawing surface of a box object: the box is centred at (0,0) with the actual size W x H, so every drawing stays
    /// inside its box under free (also non-uniform) scaling.
    /// </summary>
    internal sealed class Canvas
    {
        private readonly List<Primitive> _items = new();

        public Canvas(Size2 size, Lang lang = Lang.En)
        {
            W = Math.Max(size.W, 0.01);
            H = Math.Max(size.H, 0.01);
            Lang = lang;
        }

        public double W { get; }
        public double H { get; }
        public Lang Lang { get; }
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

        public void PolyN(Style s, bool closed, string? tag, params double[] uv)
        {
            var pts = new List<Vec2>();
            for (var i = 0; i + 1 < uv.Length; i += 2) pts.Add(N(uv[i], uv[i + 1]));
            Poly(pts, s, closed, tag);
        }

        public void Ellipse(Vec2 c, double rx, double ry, Style s, string? tag = null) => Add(Prim.Ellipse(c.X, c.Y, Math.Max(rx, 0.01), Math.Max(ry, 0.01), s), tag);

        public void Circle(Vec2 c, double r, Style s, string? tag = null) => Ellipse(c, r, r, s, tag);

        /// <summary>Elliptical arc; angles in degrees, positive sweep clockwise on screen.</summary>
        public void Arc(Vec2 c, double rx, double ry, double startDeg, double sweepDeg, Style s, string? tag = null)
            => Add(Prim.Arc(c.X, c.Y, Math.Max(rx, 0.01), Math.Max(ry, 0.01), Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg), s), tag);

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
            // Bold glyphs run wider than the average advance.
            Add(Bs.Text(at, text, Bs.FitText(text, preferred, bold ? maxW * 0.82 : maxW, maxH), tag ?? BsTags.Label, bold, anchor));
        }

        /// <summary>Filled arrowhead with its tip at <paramref name="tip"/>, pointing along <paramref name="dir"/>.</summary>
        public void Head(Vec2 tip, Vec2 dir, double len, string? tag = null)
        {
            var d = dir.Length > Geo.Eps ? dir * (1 / dir.Length) : new Vec2(1, 0);
            var b = tip - d * len;
            var n = d.Perp * (len * 0.4);
            Poly(new[] { tip, b + n, b - n }, St.Black, true, tag ?? "arrow");
        }

        /// <summary>Line with a filled arrowhead at <paramref name="b"/> (head drawn inside the segment).</summary>
        public void Arrow(Vec2 a, Vec2 b, Style s, double head, string? tag = null)
        {
            var d = b - a;
            var len = d.Length;
            if (len < Geo.Eps) return;
            head = Math.Min(head, len * 0.6);
            Line(a, b - d * (head * 0.8 / len), s, tag ?? "arrow");
            Head(b, d, head, tag);
        }

        /// <summary>Valve body: two triangles meeting at the centre (bow-tie), half length <paramref name="s"/>.</summary>
        public void BowTie(Vec2 c, double s, double hs, Style style, string? tag = null)
        {
            Poly(new[] { c, c + new Vec2(-s, -hs), c + new Vec2(-s, hs) }, style, true, tag ?? "valve");
            Poly(new[] { c, c + new Vec2(s, -hs), c + new Vec2(s, hs) }, style, true, tag ?? "valve");
        }

        /// <summary>Zigzag between <paramref name="a"/> and <paramref name="b"/> with <paramref name="n"/> teeth of amplitude <paramref name="amp"/>.</summary>
        public void Zigzag(Vec2 a, Vec2 b, int n, double amp, Style s, string? tag = null)
        {
            var d = b - a;
            var len = d.Length;
            if (len < Geo.Eps || n < 1) return;
            var nrm = d.Perp * (1 / len);
            var pts = new List<Vec2> { a };
            for (var i = 0; i < 2 * n; i++)
            {
                var t = (i + 0.5) / (2 * n);
                pts.Add(a + d * t + nrm * (i % 2 == 0 ? amp : -amp));
            }
            pts.Add(b);
            Poly(pts, s, false, tag);
        }
    }

    internal delegate void DrawFn(Canvas c, SketchObject o, RenderContext ctx);
}
