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

namespace InventaSketch.Modules.Optics
{
    /// <summary>Library category ids of the optics module.</summary>
    public static class OpticsCategories
    {
        public const string Lenses = "optics.lenses";
        public const string Mirrors = "optics.mirrors";
        public const string Components = "optics.components";
        public const string Sources = "optics.sources";
        public const string Detectors = "optics.detectors";
        public const string Beams = "optics.beams";
    }

    /// <summary>Primitive tags (= part keys, <see cref="PartLooks"/>) of the optics module.</summary>
    public static class OpticsTags
    {
        /// <summary>Body of an element: lens outline, prism, housing, mirror surface.</summary>
        public const string Body = "body";
        /// <summary>Hatching on the back of a mirror.</summary>
        public const string Hatch = "hatch";
        /// <summary>Focal point markers (F, F′, centre of curvature) on the optical axis.</summary>
        public const string Focal = "focal";
        /// <summary>Inner details (filament, sensor, diagonal of a beam splitter, grooves).</summary>
        public const string Detail = "detail";
        /// <summary>Optional text label of an element.</summary>
        public const string Label = "label";
        /// <summary>Light rays (all rays of a bundle, each one "ray.0", "ray.1" …).</summary>
        public const string Ray = "ray";
        /// <summary>Dashed backward extensions of rays leaving a diverging element (virtual rays).</summary>
        public const string Virtual = "virtual";
        /// <summary>The dash-dot optical axis.</summary>
        public const string Axis = "axis";
        /// <summary>Image arrow of an object arrow (real: solid, virtual: dashed).</summary>
        public const string Image = "image";
        /// <summary>Construction rays of an object arrow's image (parallel, central and focal ray).</summary>
        public const string Construction = "construction";
        /// <summary>Names of object and image ("G"/"B" in German, "O"/"I" in English).</summary>
        public const string Names = "names";
    }

    /// <summary>
    /// Keys, labels and snapping of the drag handles (grips) of the optics types (<see cref="ObjectType.DragHandles"/>): a value
    /// is dragged on the drawing (a focal point along the axis, the edge ray of a cone, the edge of a bundle) and written to its
    /// parameter; the box (<see cref="SketchObject.Size"/>) never changes.
    /// </summary>
    internal static class Grips
    {
        /// <summary>Focal point in front of a lens (local −f), shown with the focal points.</summary>
        public const string FocalFront = "f.front";
        /// <summary>Focal point behind a lens (local +f).</summary>
        public const string FocalBack = "f.back";
        /// <summary>Focal point of a curved mirror.</summary>
        public const string MirrorFocal = "f";
        /// <summary>Centre of curvature of a curved mirror (2f), shown with the focal point.</summary>
        public const string MirrorCentre = "cc";
        /// <summary>Edge ray of an emitted cone (half cone angle, parameter <c>spread</c>).</summary>
        public const string Spread = "spread";
        /// <summary>Edge of a parallel bundle (bundle height, parameter <c>beam</c>).</summary>
        public const string Beam = "beam";
        /// <summary>Inner edge of a stop's upper blade (parameter <c>opening</c>).</summary>
        public const string Opening = "opening";
        /// <summary>Start direction of a free ray (around its first point).</summary>
        public const string Direction = "dir";
        /// <summary>End of the axis line of a polariser or wave plate (parameter <c>angle</c>).</summary>
        public const string AxisAngle = "axis";
        /// <summary>Upper end of a scanner mirror (mirror angle, parameter <c>angle</c>).</summary>
        public const string MirrorAngle = "angle";
        /// <summary>Upper end of a glass plate (window tilt, parameter <c>tilt</c>).</summary>
        public const string Tilt = "tilt";

        /// <summary>
        /// Unit direction (local) of a line drawn at <paramref name="deg"/> degrees from the element's height direction:
        /// 0° points up (−y), positive angles turn clockwise on screen.
        /// </summary>
        public static Vec2 LineDirection(double deg)
        {
            var a = Geo.DegToRad(deg);
            return new Vec2(Math.Sin(a), -Math.Cos(a));
        }

        /// <summary>
        /// The angle (whole degrees, −90° … 90°) of a line through the element's centre and the local point
        /// <paramref name="local"/> (the inverse of <see cref="LineDirection"/>; a line has no direction, so it is folded into
        /// −90° … 90°); null if the point lies on the centre.
        /// </summary>
        public static double? LineDegrees(Vec2 local)
        {
            if (local.Length < 1e-9) return null;
            var deg = Geo.RadToDeg(Math.Atan2(local.X, -local.Y));
            if (deg > 90) deg -= 180;
            else if (deg < -90) deg += 180;
            return Geo.Clamp(Dragging.Snap(deg, 1), -90, 90);
        }

        /// <summary>Half length of a line through the centre in direction <paramref name="dir"/> that stays within ±hw × ±hh.</summary>
        public static double HalfLength(Vec2 dir, double hw, double hh)
        {
            var l = double.PositiveInfinity;
            if (Math.Abs(dir.X) > 1e-9) l = Math.Min(l, Math.Max(0, hw) / Math.Abs(dir.X));
            if (Math.Abs(dir.Y) > 1e-9) l = Math.Min(l, Math.Max(0, hh) / Math.Abs(dir.Y));
            return double.IsInfinity(l) ? 0 : l;
        }

        /// <summary>A number for a label: up to two decimals, decimal comma in German.</summary>
        public static string Num(double v, Lang lang)
        {
            var s = Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
            if (s == "-0") s = "0";
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>"Focal length f: 40 mm" / "Brennweite f: 40 mm".</summary>
        public static I18nText Label(string en, string de, double v, string unit)
        {
            var sep = unit == "°" ? "" : " ";
            return new I18nText(en + ": " + Num(v, Lang.En) + sep + unit, de + ": " + Num(v, Lang.De) + sep + unit);
        }

        public static I18nText FocalLabel(double f) => Label("Focal length f", "Brennweite f", f, "mm");
        public static I18nText SpreadLabel(double deg) => Label("Half cone angle", "Halber Kegelwinkel", deg, "°");
        public static I18nText BeamLabel(double h) => Label("Bundle height", "Bündelhöhe", h, "mm");

        /// <summary>A focal length dragged to <paramref name="distance"/> mm: whole millimetres, 1 … 5000 mm.</summary>
        public static double FocalLength(double distance) => Geo.Clamp(Dragging.Snap(Math.Abs(distance), 1), 1, 5000);

        /// <summary>A bundle height from the dragged half height: whole millimetres, 1 … 500 mm.</summary>
        public static double BundleHeight(double half) => Geo.Clamp(Dragging.Snap(2 * Math.Abs(half), 1), 1, 500);

        /// <summary>
        /// Half cone angle (whole degrees, 1° … 80°) of the point <paramref name="local"/> seen from the cone's origin
        /// <paramref name="origin"/> (local coordinates, axis = +x); either side of the axis counts.
        /// </summary>
        public static double ConeDegrees(Vec2 local, Vec2 origin)
        {
            var v = local - origin;
            var deg = v.X <= 1e-9 ? 80 : Geo.RadToDeg(Math.Atan2(Math.Abs(v.Y), v.X));
            return Geo.Clamp(Dragging.Snap(deg, 1), 1, 80);
        }

        /// <summary>A page point in the local coordinates of box object <paramref name="o"/>.</summary>
        public static Vec2 ToLocal(SketchObject o, Vec2 page) => o.Matrix().Invert().Apply(page);

        /// <summary>The handle in local coordinates moved to the page (position, allowed direction).</summary>
        public static DragHandle Page(SketchObject o, string key, Vec2 local, DragConstraint constraint, I18nText label, string? part = null)
            => Dragging.ToPage(new DragHandle(key, local, constraint, label) { Part = part }, o);

        /// <summary>
        /// The grip of the edge ray of a cone with half angle <paramref name="alpha"/> (radians) from <paramref name="origin"/>:
        /// on the upper edge ray, <paramref name="radius"/> from the origin (local coordinates).
        /// </summary>
        public static DragHandle Cone(SketchObject o, Vec2 origin, double alpha, double radius)
            => Page(o, Spread, origin + new Vec2(Math.Cos(alpha), -Math.Sin(alpha)) * radius, DragConstraint.Free, SpreadLabel(Geo.RadToDeg(alpha)));
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class Pm
    {
        public static ParamDef Num(string key, string en, string de, double def, double? min = null, double? max = null, string? unit = null, double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def, Advanced = advanced };

        public static ParamDef Text(string key, string en, string de, string def)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        public static readonly ParamDef Label = Text("label", "Label", "Beschriftung", "");
        public static ParamDef Rays(int def) => Int("rays", "Number of rays", "Anzahl Strahlen", def, 1, 15);
        public static readonly ParamDef Beam = Num("beam", "Aperture height of the bundle (0 = automatic)", "Bündelhöhe (0 = automatisch)", 0, 0, 500, "mm", 1);
        public static readonly ParamDef Spread = Num("spread", "Half cone angle (0 = fill the next element)", "Halber Kegelwinkel (0 = nächstes Element ausleuchten)", 0, 0, 80, "°", 1);
        public static readonly ParamDef Focal = Bool("focal", "Focal points F, F′", "Brennpunkte F, F′", false);
        public static ParamDef FocalLength(double def) => Num("f", "Focal length (drawing scale)", "Brennweite (Zeichnungsmaßstab)", def, 1, 5000, "mm", 1);

        /// <summary>
        /// How the glass faces treat the light: ideal (no losses, the default of the elements that existed before Fresnel
        /// losses, so their drawings stay unchanged), uncoated (Fresnel equations) or anti-reflection coated (λ/4 layer).
        /// </summary>
        public static ParamDef Coating(string def) => Choice("coating", "Glass surfaces", "Glasoberflächen", def,
            ("ideal", "Ideal (no reflection losses)", "Ideal (ohne Reflexionsverluste)"),
            ("none", "Uncoated (Fresnel reflection)", "Unvergütet (Fresnel-Reflexion)"),
            ("ar", "Anti-reflection coated (λ/4 layer)", "Entspiegelt (λ/4-Schicht)"));
    }

    /// <summary>Shared colours, styles and drawing helpers.</summary>
    internal static class Ox
    {
        public const string Black = "#000000";
        /// <summary>Light glass tint (lenses, prisms, splitters).</summary>
        public const string Glass = "#dcecf8";
        public const string Housing = "#eceff1";
        public const string Dark = "#37474f";
        /// <summary>Default colour of (white or monochromatic red) light rays.</summary>
        public const string RayRed = "#d32f2f";
        /// <summary>White light that is split into colours somewhere in the bundle is drawn neutral grey.</summary>
        public const string RayWhite = "#616161";

        public static readonly Style Outline = Style.Visible;
        public static readonly Style Thin = Style.ThinLine;
        public static Style Filled(string fill) => Style.Visible with { Fill = Fill.Solid(fill) };
        public static Style ThinFilled(string fill) => Style.ThinLine with { Fill = Fill.Solid(fill) };
        public static readonly Style GlassBody = Filled(Glass);

        public const double LabelSize = 3.5;

        /// <summary>Optional label below the box (outside it, like designators in other modules).</summary>
        public static IEnumerable<Primitive> Label(SketchObject o, Vec2? at = null)
        {
            var text = o.Str("label", "");
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var p = at ?? new Vec2(0, o.Size.H / 2 + 1.2);
            yield return new TextPrim(p, text, new TextStyle { Size = LabelSize, Anchor = TextAnchor.Middle, Baseline = TextBaseline.Top }) { Tag = OpticsTags.Label };
        }

        /// <summary>Focal point marker: short tick across the axis, a dot and its name below the axis.</summary>
        public static IEnumerable<Primitive> FocalMark(Vec2 p, string name)
        {
            var s = Style.ThinLine with { StrokeWidth = 0.25 };
            yield return new LinePrim(p + new Vec2(0, -1.4), p + new Vec2(0, 1.4), s) { Tag = OpticsTags.Focal };
            yield return new EllipsePrim(p, 0.55, 0.55, new Style { Role = LineRole.Thin, StrokeWidth = 0.1, Stroke = Black, Fill = Fill.Solid(Black) }) { Tag = OpticsTags.Focal };
            yield return new TextPrim(p + new Vec2(0, 2.0), name, new TextStyle { Size = 3.0, Italic = true, Anchor = TextAnchor.Middle, Baseline = TextBaseline.Top }) { Tag = OpticsTags.Focal };
        }

        /// <summary>Evenly spaced values from <paramref name="a"/> to <paramref name="b"/>; one value: the midpoint.</summary>
        public static IReadOnlyList<double> Spaced(double a, double b, int n)
        {
            if (n <= 1) return new[] { (a + b) / 2 };
            var list = new double[n];
            for (var i = 0; i < n; i++) list[i] = a + (b - a) * i / (n - 1);
            return list;
        }

        /// <summary>
        /// Points of a lens or mirror surface between y = −h and y = +h: the circular arc through (xEdge, −h), (xCentre, 0)
        /// and (xEdge, h), or a straight line when both x agree. Ordered from −h to +h.
        /// </summary>
        public static List<Vec2> Surface(double xCentre, double xEdge, double h, int n = 16)
        {
            var pts = new List<Vec2>(n + 1);
            if (Math.Abs(xCentre - xEdge) < 1e-6 || h < 1e-6)
            {
                pts.Add(new Vec2(xEdge, -h));
                pts.Add(new Vec2(xEdge, h));
                return pts;
            }
            // Centre of the circle lies on the axis: (xc − x0)² = (xe − x0)² + h².
            var x0 = (xCentre * xCentre - xEdge * xEdge - h * h) / (2 * (xCentre - xEdge));
            var r = Math.Abs(xCentre - x0);
            var side = Math.Sign(xCentre - x0);
            for (var i = 0; i <= n; i++)
            {
                var y = -h + 2 * h * i / n;
                pts.Add(new Vec2(x0 + side * Math.Sqrt(Math.Max(0, r * r - y * y)), y));
            }
            return pts;
        }

        public static int ClampInt(int v, int min, int max) => v < min ? min : v > max ? max : v;

        public static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Parses a typed number: "45", "45°", "−2,5", "40 mm". Throws <see cref="FormatException"/>.</summary>
        public static double ParseNumber(string? text)
        {
            var s = (text ?? "").Trim().Replace('−', '-').Replace("°", "").Replace("mm", "").Trim().Replace(',', '.');
            if (s.Length == 0 || !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
                throw new FormatException("Not a number: " + text);
            return v;
        }

        // ------------------------------------------------------------------ actions

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string key, bool fallback, string? group = null)
            => new(id, label, IconIds.Toggle, x => x.WithParam(key, !x.Flag(key, fallback))) { IsActive = o.Flag(key, fallback), Group = group, ParamKeys = new[] { key } };

        /// <summary>
        /// A choice parameter as a choice (one option per value, the current one marked; hosts show a dropdown) – never a
        /// cycle button (maintainer 2026-11-25).
        /// </summary>
        public static IEnumerable<ContextAction> Choice(SketchObject o, string id, I18nText label, ParamDef def, string? group = null)
            => ActionChoices.FromParam(o, id, def, label, group, null);

        /// <summary>Number input clamped to the parameter's limits.</summary>
        public static ContextAction NumberInput(SketchObject o, string id, I18nText label, ParamDef def, string? group = null)
            => ContextAction.WithInput(id, label, IconIds.ForQuantity(def.Unit, def.Key), label, N(o.Num(def.Key, def.Default?.Number ?? 0)),
                (x, t) => x.WithParam(def.Key, Geo.Clamp(ParseNumber(t), def.Min ?? double.MinValue, def.Max ?? double.MaxValue)), def.Unit)
                with { Group = group, ParamKeys = new[] { def.Key } };

        /// <summary>The glass surfaces (parameter <c>coating</c>) as a choice in the context bar.</summary>
        public static IEnumerable<ContextAction> CoatingChoice(SketchObject o, ParamDef def, string group)
            => Choice(o, "optics.coating", new I18nText("Glass surfaces", "Glasoberflächen"), def, group);

        /// <summary>One more / one less ray (integer parameter "rays").</summary>
        public static IEnumerable<ContextAction> RayCount(SketchObject o, ParamDef def)
        {
            var n = o.Int(def.Key, (int)(def.Default?.Number ?? 1));
            yield return new ContextAction("optics.rays.more", new I18nText("More rays", "Mehr Strahlen"), IconIds.CountMore,
                x => x.WithParam(def.Key, Math.Min((int)(def.Max ?? 15), x.Int(def.Key, n) + 1))) { Group = "rays", IsEnabled = n < (def.Max ?? 15) };
            yield return new ContextAction("optics.rays.less", new I18nText("Fewer rays", "Weniger Strahlen"), IconIds.CountFewer,
                x => x.WithParam(def.Key, Math.Max((int)(def.Min ?? 1), x.Int(def.Key, n) - 1))) { Group = "rays", IsEnabled = n > (def.Min ?? 1) };
        }
    }

    /// <summary>
    /// Base class of all optics types. Every type reports the light rays on its page as dependents: rays are traced live
    /// through the elements (<see cref="RayTracer"/>), so moving, turning or changing an element redraws them.
    /// </summary>
    internal abstract class OpticsType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected OpticsType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;

        /// <summary>
        /// Rays read the elements of their page and object arrows image through the next lens or curved mirror (only those
        /// two types render from their neighbours); elements name the rays (and, if they act on light, the object arrows
        /// that draw an image) as their dependents.
        /// </summary>
        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var page = ctx.Doc.PageOf(o.Id);
            if (page is null) return Array.Empty<string>();
            var optical = !o.IsPointGeometry && Surfaces(o).Count > 0;
            // Elements whose values label shows values of the traced light or of their neighbours follow too.
            return ctx.Doc.ObjectsOn(page)
                .Where(x => x.Id != o.Id && (x.Type == RayType.TypeId || optical && x.Type == Sources.ObjectId && Imaging.Shown(x)
                    || ValueLabels.Shows(x) && OpticsModule.TypeOf(x.Type) is ITraceValues))
                .Select(x => x.Id).ToList();
        }

        /// <summary>Optical surfaces in local coordinates (box types); empty for elements light passes unaffected.</summary>
        internal virtual IReadOnlyList<Surface> Surfaces(SketchObject o) => Array.Empty<Surface>();

        /// <summary>
        /// The rays an element sends out when a ray object is docked on it (light sources, fibre end, object arrow, and the
        /// incoming bundle of a lens); null for elements that do not emit.
        /// </summary>
        internal virtual Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene) => null;

        /// <summary>Anchor id where a ray bundle is docked ("Add ray bundle"); null = no emitter.</summary>
        internal virtual string? EmitAnchor => null;

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => OwnActions(o, null);

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx) => OwnActions(o, ctx.Doc);

        /// <summary>The type's actions; <paramref name="doc"/> is null when no document is known.</summary>
        protected virtual IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            if (EmitAnchor is not null) yield return Beams.BundleAction(o, doc, EmitAnchor);
        }
    }
}
