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

        /// <summary>Cycles a choice parameter through its options.</summary>
        public static ContextAction Cycle(SketchObject o, string id, I18nText label, ParamDef def, string? group = null)
        {
            var values = def.Options!.Select(x => x.Value).ToList();
            var fallback = def.Default?.Text ?? values[0];
            return new ContextAction(id, label, IconIds.Cycle, x =>
            {
                var i = values.IndexOf(x.Str(def.Key, fallback));
                return x.WithParam(def.Key, values[(i + 1) % values.Count]);
            }) { Group = group, ParamKeys = new[] { def.Key } };
        }

        /// <summary>Number input clamped to the parameter's limits.</summary>
        public static ContextAction NumberInput(SketchObject o, string id, I18nText label, ParamDef def, string? group = null)
            => ContextAction.WithInput(id, label, IconIds.ForQuantity(def.Unit, def.Key), label, N(o.Num(def.Key, def.Default?.Number ?? 0)),
                (x, t) => x.WithParam(def.Key, Geo.Clamp(ParseNumber(t), def.Min ?? double.MinValue, def.Max ?? double.MaxValue)), def.Unit)
                with { Group = group, ParamKeys = new[] { def.Key } };

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

        /// <summary>Rays read the elements of their page; elements name the rays as their dependents.</summary>
        public override bool DependsOnNeighbours => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var page = ctx.Doc.PageOf(o.Id);
            if (page is null) return Array.Empty<string>();
            return ctx.Doc.ObjectsOn(page).Where(x => x.Id != o.Id && x.Type == RayType.TypeId).Select(x => x.Id).ToList();
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
