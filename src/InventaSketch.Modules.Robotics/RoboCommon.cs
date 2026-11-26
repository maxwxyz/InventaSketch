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

namespace InventaSketch.Modules.Robotics
{
    /// <summary>Library category ids of the robotics module.</summary>
    public static class RoboCategories
    {
        public const string Robots = "robot.robots";
        public const string Tools = "robot.tools";
        public const string Frames = "robot.frames";
        public const string Cell = "robot.cell";
        public const string Safety = "robot.safety";
    }

    /// <summary>View ids of the robotics module.</summary>
    public static class RoboViews
    {
        /// <summary>Side view: looking along +y (world x to the right, z up).</summary>
        public const string Side = "side";
        /// <summary>Top view: looking down along −z (world x to the right, y up on the page).</summary>
        public const string Top = "top";
        /// <summary>Tools: looking along the tool axis from the flange.</summary>
        public const string Axial = "axial";

        public static readonly ViewDef SideView = new(Side, new I18nText("Side view", "Seitenansicht")) { Icon = IconIds.Side };
        public static readonly ViewDef TopView = new(Top, new I18nText("Top view", "Draufsicht")) { Icon = IconIds.Top };
        public static readonly ViewDef AxialView = new(Axial, new I18nText("Along the tool axis", "In Werkzeugachse")) { Icon = IconIds.Axial };
    }

    /// <summary>Semantic primitive tags of the robotics module (tests, host naming).</summary>
    public static class RoboTags
    {
        /// <summary>Base, column, pedestal or chassis.</summary>
        public const string Base = "base";
        /// <summary>Link body or schematic link line.</summary>
        public const string Link = "link";
        /// <summary>Joint housing or joint symbol.</summary>
        public const string Joint = "joint";
        /// <summary>Flange (tool mounting plate).</summary>
        public const string Flange = "flange";
        /// <summary>Ghost (alternative) position, a group of phantom lines.</summary>
        public const string Ghost = "ghost";
        /// <summary>Reach envelope area or outline.</summary>
        public const string Envelope = "envelope";
        /// <summary>Joint labels, field names, values.</summary>
        public const string Label = "label";
        /// <summary>Gripper jaws, fingers, cups.</summary>
        public const string Jaw = "jaw";
        /// <summary>Safety field (protective field).</summary>
        public const string Protective = "protective";
        /// <summary>Safety field (warning field).</summary>
        public const string Warning = "warning";
        /// <summary>Light beams of a light curtain.</summary>
        public const string Beam = "beam";
        /// <summary>Fence posts.</summary>
        public const string Post = "post";
        /// <summary>Coordinate axes and arrows.</summary>
        public const string Axis = "axis";
        /// <summary>Note shown when a pose cannot be reached.</summary>
        public const string NoReach = "noreach";
        /// <summary>Field of view of a camera.</summary>
        public const string Fov = "fov";
        /// <summary>Links of an energy chain.</summary>
        public const string Chain = "chain";
    }

    /// <summary>Shared colours (common practice, not a cited standard).</summary>
    internal static class RoboColors
    {
        public const string Body = "#eef1f5";
        public const string Dark = "#c5ccd6";
        public const string Red = "#d32f2f";
        public const string Yellow = "#f2b705";
        public const string Envelope = "#2f6db0";
        public const string EnvelopeTint = "#dfeaf7";
        public const string ProtectiveTint = "#fbd3d3";
        public const string WarningTint = "#fff1bf";
        public const string RestrictedTint = "#dbe7f5";
        public const string CollaborativeTint = "#d9f0d9";
        public const string Green = "#2e7d32";
        public const string Blue = "#1f5fa8";
    }

    /// <summary>Shared line styles (ISO 128 inspired roles).</summary>
    internal static class RS
    {
        /// <summary>Visible outline of bodies (medium width) with the light body fill.</summary>
        public static readonly Style Body = Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid(RoboColors.Body) };
        /// <summary>Darker body parts (bases, motors, feet).</summary>
        public static readonly Style Dark = Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid(RoboColors.Dark) };
        /// <summary>White-filled outline (caps, parts lying on top of others).</summary>
        public static readonly Style White = Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") };
        /// <summary>White-filled thin outline.</summary>
        public static readonly Style ThinWhite = Style.ThinLine with { Fill = Fill.Solid("#ffffff") };
        /// <summary>Continuous wide line (schematic links).</summary>
        public static readonly Style Wide = Style.Visible;
        public static readonly Style Medium = Style.Visible with { StrokeWidth = 0.35 };
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style Fine = Style.ThinLine with { StrokeWidth = 0.18 };
        public static readonly Style Center = Style.CenterLine with { StrokeWidth = 0.18 };
        public static readonly Style Hidden = Style.Hidden with { StrokeWidth = 0.25 };
        /// <summary>Alternative positions of movable parts (ISO 128 phantom line).</summary>
        public static readonly Style Ghost = Style.Of(LineRole.Phantom) with { StrokeWidth = 0.25 };
        public static readonly Style Black = Style.Visible with { StrokeWidth = 0.1, Fill = Fill.Solid("#000000") };
        public static readonly Style Arrow = Style.ThinLine with { StrokeWidth = 0.3, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
        public static Style Hatch(double spacing, double angle = 45) => Style.ThinLine with { Fill = Fill.Hatched(new HatchPattern(angle, spacing)) };
        public static Style HatchWhite(double spacing, double angle = 45) => Style.ThinLine with { Fill = Fill.Hatched(new HatchPattern(angle, spacing), "#ffffff") };
        /// <summary>Area fill without outline (outline drawn separately).</summary>
        public static Style Area(Fill fill) => new() { Stroke = "none", Fill = fill, Role = LineRole.Thin };
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class Pm
    {
        public static ParamDef Num(string key, string en, string de, double def, double? min = null, double? max = null, string? unit = "mm", double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1, Advanced = advanced };

        public static ParamDef Bool(string key, string en, string de, bool def, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def, Advanced = advanced };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        /// <summary>Area fill choice with the shared fill pictograms.</summary>
        public static ParamDef FillChoice(string def) =>
            Choice("fill", "Fill", "Füllung", def, ("tint", "Tint", "Getönt"), ("hatch", "Hatched", "Schraffiert"), ("none", "Outline only", "Nur Kontur"))
                .WithOptionIcons(IconIds.ForFill);
    }

    /// <summary>Shared helpers.</summary>
    internal static class Rb
    {
        /// <summary>Approximate advance width per character relative to the em size.</summary>
        public const double CharWidth = 0.55;

        public static string Fmt(double v, string format = "0.#") => v.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>Formats an angle in degrees ("−30°").</summary>
        public static string Deg(double v)
        {
            var s = Fmt(Math.Round(v, 1));
            return (s == "-0" ? "0" : s).Replace("-", "−") + "°";
        }

        /// <summary>Text that fits into the box (±hw, ±hh): the size shrinks and the position is clamped.</summary>
        public static TextPrim FitText(Vec2 at, string text, double size, double hw, double hh, string? tag = RoboTags.Label, bool bold = false, bool italic = false, string? color = null)
        {
            var n = Math.Max(1, text.Length);
            size = Math.Max(0.3, Math.Min(size, Math.Min(2 * hw / (n * CharWidth * (bold ? 1.1 : 1) + 0.01), 2 * hh)));
            var w = n * size * CharWidth;
            var x = Geo.Clamp(at.X, -hw + w / 2, hw - w / 2);
            var y = Geo.Clamp(at.Y, -hh + size / 2, hh - size / 2);
            if (w > 2 * hw) x = 0;
            return new TextPrim(new Vec2(x, y), text, new TextStyle { Size = size, Bold = bold, Italic = italic, Color = color }) { Tag = tag };
        }

        /// <summary>Style override: text takes the stroke colour, white fills keep their look, tinted areas take the fill.</summary>
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
            return Prim.Restyle(p, s => s with
            {
                Stroke = s.HasStroke ? over.Stroke ?? s.Stroke : s.Stroke,
                Opacity = over.Opacity ?? s.Opacity,
                StrokeWidth = over.StrokeWidth is double w && s.HasStroke && (s.Role ?? LineRole.Visible) is LineRole.Visible ? w : s.StrokeWidth,
                Fill = over.Fill is not null && s.Fill is not null && s.Fill.Kind == FillKind.Solid && s.Fill.Color is not "#ffffff" and not "#000000" ? over.Fill : s.Fill,
                Dash = over.Dash ?? s.Dash,
            });
        }

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string icon, string key, bool fallback, string group = "param")
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group, ParamKeys = new[] { key } };

        /// <summary>
        /// A choice parameter as a choice (one option per value, the current one marked; hosts show a dropdown) – never a
        /// cycle button (maintainer 2026-11-25). The label defaults to the parameter's.
        /// </summary>
        public static IEnumerable<ContextAction> Choice(SketchObject o, string id, ParamDef choice, string group = "param", I18nText? label = null)
            => ActionChoices.FromParam(o, id, choice, label, group, null);

        /// <summary>Normalises an angle to (−180, 180].</summary>
        public static double NormAngle(double a)
        {
            var r = Geo.NormDeg(a);
            return r > 180 + 1e-9 ? r - 360 : r;
        }

        /// <summary>Filled arrowhead polygon with its tip at <paramref name="tip"/> pointing along <paramref name="dir"/>.</summary>
        public static PathPrim Head(Vec2 tip, Vec2 dir, double len, Style? style = null)
        {
            var d = dir.Normalized();
            if (d.Length < Geo.Eps) d = new Vec2(1, 0);
            var b = tip - d * len;
            var n = d.Perp * (len * 0.38);
            return Prim.Polygon(new[] { tip, b + n, b - n }, style ?? RS.Black);
        }

        /// <summary>Convex hull (counter-clockwise in a y-up sense, i.e. clockwise on screen) of 2D points (monotone chain).</summary>
        public static List<Vec2> Hull(IEnumerable<Vec2> points)
        {
            var p = points.Distinct().OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
            if (p.Count < 3) return p;
            var h = new Vec2[2 * p.Count];
            var k = 0;
            for (var i = 0; i < p.Count; i++)
            {
                while (k >= 2 && (h[k - 1] - h[k - 2]).Cross(p[i] - h[k - 2]) <= 1e-12) k--;
                h[k++] = p[i];
            }
            for (int i = p.Count - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && (h[k - 1] - h[k - 2]).Cross(p[i] - h[k - 2]) <= 1e-12) k--;
                h[k++] = p[i];
            }
            return h.Take(k - 1).ToList();
        }

        /// <summary>Capsule (stadium) outline around the segment a..b with radius r.</summary>
        public static List<Vec2> Capsule(Vec2 a, Vec2 b, double r, int arc = 10)
        {
            var d = b - a;
            var ang = d.Length < 1e-9 ? 0 : Math.Atan2(d.Y, d.X);
            var pts = new List<Vec2>();
            pts.AddRange(Geo.ArcPoints(b, r, r, ang - Math.PI / 2, Math.PI, arc));
            pts.AddRange(Geo.ArcPoints(a, r, r, ang + Math.PI / 2, Math.PI, arc));
            return pts;
        }
    }

    /// <summary>
    /// Drawing helper for symbols authored in design millimetres for a reference box centred at (0,0) and stretched to
    /// the actual object size (free non-uniform scaling stretches the symbol). Round details use the uniform detail
    /// scale <see cref="K"/> so they stay round.
    /// </summary>
    internal sealed class Pad
    {
        public Pad(Size2 actual, Size2 design, Lang lang)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            Sx = W / Math.Max(design.W, 1e-6);
            Sy = H / Math.Max(design.H, 1e-6);
            K = Math.Min(Sx, Sy);
            Design = design;
            Lang = lang;
        }

        public double W { get; }
        public double H { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }
        public Size2 Design { get; }
        public Lang Lang { get; }
        public bool De => Lang == Lang.De;
        public List<Primitive> Items { get; } = new();

        /// <summary>Design to local millimetres.</summary>
        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => new(p.X * Sx, p.Y * Sy);

        public void Add(Primitive p, string? tag = null) => Items.Add(tag is null ? p : p with { Tag = tag });
        public void Line(double x1, double y1, double x2, double y2, Style s, string? tag = null) => Add(Prim.Line(V(x1, y1), V(x2, y2), s), tag);
        public void Poly(Style s, bool closed, string? tag, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed), tag);
        }
        public void PolyD(IEnumerable<Vec2> design, Style s, bool closed, string? tag = null) => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);

        public void Box(double x0, double y0, double x1, double y1, Style s, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s), tag);
        }

        /// <summary>Rounded box (corner radius in detail scale).</summary>
        public void RoundBox(double x0, double y0, double x1, double y1, double r, Style s, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            double x = a.X, y = a.Y, w = b.X - a.X, h = b.Y - a.Y;
            r = Math.Max(0, Math.Min(r * K, Math.Min(w, h) / 2));
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

        /// <summary>Stretched ellipse in design coordinates.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style s, string? tag = null)
            => Add(Prim.Ellipse(cx * Sx, cy * Sy, Math.Max(rx * Sx, 0.01), Math.Max(ry * Sy, 0.01), s), tag);

        /// <summary>True circle (detail scale) around a design centre.</summary>
        public void Round(double cx, double cy, double r, Style s, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, Math.Max(r * K, 0.01), s), tag);
        }

        /// <summary>Arc in design coordinates (stretched), angles in degrees clockwise on screen.</summary>
        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style s, string? tag = null)
            => PolyD(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg)), s, false, tag);

        /// <summary>Arrow (line with a filled head) between design points; the head size is in detail scale.</summary>
        public void Arrow(double x0, double y0, double x1, double y1, double head, Style? s = null, string? tag = null)
        {
            var a = V(x0, y0); var b = V(x1, y1);
            var d = b - a;
            var len = d.Length;
            if (len < Geo.Eps) return;
            var hl = Math.Min(head * K, len * 0.6);
            Add(Prim.Line(a, b - d * (hl * 0.8 / len), s ?? RS.Thin), tag ?? RoboTags.Axis);
            Add(Rb.Head(b, d, hl, (s ?? RS.Thin) is { Stroke: string col } && col != "none" ? RS.Black with { Fill = Fill.Solid(col), Stroke = col } : RS.Black), tag ?? RoboTags.Axis);
        }

        /// <summary>Text fitted into the box (design position, size in detail scale).</summary>
        public void Text(double x, double y, string text, double size, string? tag = RoboTags.Label, bool bold = false, bool italic = false, string? color = null)
            => Add(Rb.FitText(V(x, y), text, size * K, W / 2, H / 2, tag, bold, italic, color));
    }
}
