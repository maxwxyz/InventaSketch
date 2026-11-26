using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>
    /// Paraxial data of a thick lens in air (lensmaker's equation with thickness): radii signed as usual in optics (positive
    /// when the centre of curvature lies behind the vertex, +∞ for a plane face), centre thickness d, refractive index n.
    /// </summary>
    public static class ThickLens
    {
        /// <summary>Effective focal length: 1/f = (n − 1)·[1/R1 − 1/R2 + (n − 1)·d / (n·R1·R2)]; infinite for an afocal lens.</summary>
        public static double FocalLength(double n, double r1, double r2, double d)
        {
            double c1 = 1 / r1, c2 = 1 / r2;
            var power = (n - 1) * (c1 - c2 + (n - 1) * d * c1 * c2 / n);
            return Math.Abs(power) < 1e-15 ? double.PositiveInfinity : 1 / power;
        }

        /// <summary>Back focal distance (rear vertex to F′): f·(1 − (n − 1)·d / (n·R1)).</summary>
        public static double BackFocalDistance(double n, double r1, double r2, double d)
            => FocalLength(n, r1, r2, d) * (1 - (n - 1) * d / (n * r1));

        /// <summary>Front focal distance (F to the front vertex): f·(1 + (n − 1)·d / (n·R2)).</summary>
        public static double FrontFocalDistance(double n, double r1, double r2, double d)
            => FocalLength(n, r1, r2, d) * (1 + (n - 1) * d / (n * r2));
    }

    /// <summary>
    /// Thick lens: two spherical faces drawn and traced exactly (Snell's law at both faces), glass of index <c>n</c>. The box
    /// is the lens: its width is the centre (convex) or edge (concave) thickness, its height the aperture; the parameter
    /// <c>sag</c> sets how deep the curved faces are, so the radii, and with them the focal length, follow from the box and
    /// scale with it. Focal points F, F′ and the principal planes H, H′ come from the lensmaker's equation.
    /// </summary>
    internal sealed class ThickLensType : OpticsType, IValueType, IValueLinks
    {
        public const string TypeId = "optics.lens.thick";

        public ThickLensType() : base(TypeId, new I18nText("Thick lens", "Dicke Linse"), OpticsCategories.Lenses,
            "thick lens", "lens", "spherical", "lensmaker", "principal planes", "dicke linse", "linse", "sphärisch", "hauptebenen", "linsenschleiferformel")
        { }

        public static readonly ParamDef ShapeDef = Pm.Choice("shape", "Lens shape", "Linsenform", "biconvex",
            ("biconvex", "Biconvex", "Bikonvex"), ("planoconvex", "Plano-convex", "Plankonvex"),
            ("biconcave", "Biconcave", "Bikonkav"), ("planoconcave", "Plano-concave", "Plankonkav"));

        public static readonly ParamDef IndexDef = Pm.Num("n", "Refractive index n", "Brechzahl n", 1.5, 1.01, 3.0, null, 0.01);
        public static readonly ParamDef SagDef = Pm.Num("sag", "Depth of the curved faces (share of the width)", "Wölbungstiefe (Anteil der Breite)", 0.35, 0.05, 0.45, null, 0.05);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            ShapeDef, IndexDef, SagDef,
            Pm.Bool("focal", "Focal points F, F′ and principal planes H, H′", "Brennpunkte F, F′ und Hauptebenen H, H′", false),
            Pm.Rays(5), Pm.Beam, CoatingDef, Pm.Label, ValueLabels.ShowValues,
        };

        public static readonly ParamDef CoatingDef = Pm.Coating("ideal");

        /// <summary>Focal length from the lensmaker's equation (index n and the drawn faces), refractive power, back focal distance.</summary>
        public IReadOnlyList<VarDef> Variables => OpticsValues.ThickLensVariables;
        public IReadOnlyList<Relation> Relations => OpticsValues.ThickLensRelations;
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => OpticsValues.ThickLensLinks(o);

        public override Size2 DefaultSize => new(10, 34);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Beams.AxisId, Sources.LaserId, Detectors.ScreenId };

        /// <summary>Vertex and edge x of both faces, half aperture, index and paraxial data of the lens in <paramref name="o"/>.</summary>
        internal sealed record Layout(double XL, double XLe, double XR, double XRe, double Hh, double N)
        {
            public double R1 => Radius(XL, XLe, Hh);
            public double R2 => Radius(XR, XRe, Hh);
            public double D => XR - XL;
            public double F => ThickLens.FocalLength(N, R1, R2, D);
            public double Bfd => ThickLens.BackFocalDistance(N, R1, R2, D);
            public double Ffd => ThickLens.FrontFocalDistance(N, R1, R2, D);
            /// <summary>Front and back focal points on the axis (local x), null if the lens is (nearly) afocal.</summary>
            public (double Front, double Back)? Foci => Math.Abs(F) < 1e5 ? (XL - Ffd, XR + Bfd) : null;
        }

        /// <summary>Signed radius of the face through (xc, 0) and (xe, ±h): positive when its centre lies at larger x; +∞ when flat.</summary>
        internal static double Radius(double xc, double xe, double h)
        {
            if (Math.Abs(xc - xe) < 1e-6 || h < 1e-6) return double.PositiveInfinity;
            var x0 = (xc * xc - xe * xe - h * h) / (2 * (xc - xe));
            return x0 - xc;
        }

        internal static Layout Of(SketchObject o)
        {
            var w = o.Size.W;
            var s = Geo.Clamp(o.Num("sag", 0.35), 0.05, 0.45);
            var n = Geo.Clamp(o.Num("n", 1.5), 1.01, 3.0);
            var hh = o.Size.H / 2;
            var (l, le, r, re) = o.Str("shape", "biconvex") switch
            {
                "planoconvex" => (-0.5, -0.5, 0.5, 0.5 - 2 * s),
                "biconcave" => (-0.5 + s, -0.5, 0.5 - s, 0.5),
                "planoconcave" => (-0.5, -0.5, 0.5 - 2 * s, 0.5),
                _ => (-0.5, -0.5 + s, 0.5, 0.5 - s),
            };
            return new Layout(l * w, le * w, r * w, re * w, hh, n);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var g = Of(o);
            var left = Ox.Surface(g.XL, g.XLe, g.Hh, 20);
            var right = Ox.Surface(g.XR, g.XRe, g.Hh, 20);
            right.Reverse();
            var list = new List<Primitive> { Prim.Polygon(left.Concat(right), Ox.GlassBody) with { Tag = OpticsTags.Body } };
            if (o.Flag("focal", false) && g.Foci is { } foci)
            {
                list.AddRange(Ox.FocalMark(new Vec2(foci.Front, 0), "F"));
                list.AddRange(Ox.FocalMark(new Vec2(foci.Back, 0), "F′"));
                // Principal planes: H at F + f, H′ at F′ − f (thin dashed lines over the aperture, named above).
                var plane = Style.ThinLine with { StrokeWidth = 0.2, Dash = DashStyle.Dash };
                var text = new TextStyle { Size = 2.5, Italic = true, Anchor = TextAnchor.Middle, Baseline = TextBaseline.Bottom };
                foreach (var (x, name) in new[] { (foci.Front + g.F, "H"), (foci.Back - g.F, "H′") })
                {
                    list.Add(new LinePrim(new Vec2(x, -g.Hh), new Vec2(x, g.Hh), plane) { Tag = OpticsTags.Focal });
                    list.Add(new TextPrim(new Vec2(x, -g.Hh - 0.6), name, text) { Tag = OpticsTags.Focal });
                }
            }
            list.AddRange(Ox.Label(o));
            return list;
        }

        /// <summary>Both faces (spherical or flat, refracting) and the edges of the lens (absorbing).</summary>
        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var g = Of(o);
            Surface Face(double xc, double xe, double r, Vec2 outward)
            {
                var proto = new Surface(SurfaceKind.Refract, new Vec2(xe, -g.Hh), new Vec2(xe, g.Hh)) { Normal = outward, Index = g.N, Centre = new Vec2(xc, 0), Coating = Fresnel.Of(o) };
                return double.IsInfinity(r) ? proto : proto with { Radius = Math.Abs(r), Sphere = new Vec2(xc + r, 0), Half = g.Hh };
            }
            return new[]
            {
                Face(g.XL, g.XLe, g.R1, new Vec2(-1, 0)),
                Face(g.XR, g.XRe, g.R2, new Vec2(1, 0)),
                new Surface(SurfaceKind.Absorb, new Vec2(g.XLe, -g.Hh), new Vec2(g.XRe, -g.Hh)),
                new Surface(SurfaceKind.Absorb, new Vec2(g.XLe, g.Hh), new Vec2(g.XRe, g.Hh)),
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
            if (Of(o).Foci is not { } foci) yield break;
            yield return Anchor.Point(AnchorRole.Port, new Vec2(foci.Front, 0), "f1");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(foci.Back, 0), "f2");
        }

        internal override string? EmitAnchor => "o";

        private static double BundleHalf(SketchObject o) => o.Num("beam", 0) is var beam && beam > 0 ? beam / 2 : 0.7 * o.Size.H / 2;

        /// <summary>Start x of the lens's own bundle: in front of the lens, at least 15 mm (or |f|) before it.</summary>
        private static double StartX(SketchObject o)
        {
            var g = Of(o);
            var f = Math.Abs(g.F);
            return g.XL - Math.Max(15, double.IsInfinity(f) ? 15 : Math.Min(f, 400));
        }

        /// <summary>The lens's own demonstration bundle: parallel to the axis from the left, across 70 % of the aperture (or <c>beam</c>).</summary>
        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var m = o.Matrix();
            var half = BundleHalf(o);
            var n = Ox.ClampInt(o.Int("rays", 5), 1, 15);
            var heights = n == 1 ? new[] { -half } : Ox.Spaced(-half, half, n);
            var x = StartX(o);
            var dir = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            return new Emission(heights.Select(y => new StartRay(m.Apply(new Vec2(x, y)), dir)).ToList(), null);
        }

        /// <summary>With its own bundle docked: the edge of the bundle (bundle height). The focal length follows from the shape, so it has no grip.</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            if (Beams.BundlesOf(ctx.Doc, o.Id).Count == 0) yield break;
            var half = BundleHalf(o);
            yield return Grips.Page(o, Grips.Beam, new Vec2(StartX(o), -half), DragConstraint.Vertical, Grips.BeamLabel(2 * half));
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
            => key == Grips.Beam ? o.WithParam("beam", Grips.BundleHeight(Grips.ToLocal(o, pagePos).Y)) : o;

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Lens body", "Linsenkörper"), "shape");
            if (o.Flag("focal", false)) yield return new PartInfo(OpticsTags.Focal, new I18nText("Focal points and principal planes", "Brennpunkte und Hauptebenen"), "mark");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            yield return Ox.Toggle(o, "optics.focal", new I18nText("Focal points and principal planes", "Brennpunkte und Hauptebenen"), "focal", false, "lens");
            yield return Ox.NumberInput(o, "optics.n", new I18nText("Refractive index", "Brechzahl"), IndexDef, "lens");
            // The depth of the curved faces (sag) is set in the inspector: one value pictogram per element means one value.
            foreach (var opt in Ox.Choice(o, "optics.shape", new I18nText("Lens shape", "Linsenform"), ShapeDef, "lens")) yield return opt;
            foreach (var opt in Ox.CoatingChoice(o, CoatingDef, "lens")) yield return opt;
            foreach (var a in Ox.RayCount(o, Parameters[4])) yield return a;
        }
    }

    /// <summary>
    /// Galvanometer scanner mirror: a plane mirror on a pivot (the galvo shaft) that turns by the parameter <c>angle</c>
    /// inside its box (0° = upright like a plane mirror, reflecting to the left; 45° turns a beam from the left upwards).
    /// Turning the mirror by an angle turns the reflected beam by twice that angle. Optionally the scan range ±<c>scan</c>
    /// is shown: the mirror's end positions dashed and the reflected rays at both ends as a dashed fan.
    /// </summary>
    internal sealed class ScannerMirrorType : OpticsType
    {
        public const string TypeId = "optics.mirror.scanner";

        public ScannerMirrorType() : base(TypeId, new I18nText("Scanner mirror (galvo)", "Scannerspiegel (Galvo)"), OpticsCategories.Mirrors,
            "scanner", "galvo", "galvanometer", "scan mirror", "beam steering", "scannerspiegel", "galvanometerspiegel", "ablenkspiegel", "strahlablenkung")
        { }

        public static readonly ParamDef AngleDef = Pm.Num("angle", "Mirror angle", "Spiegelwinkel", 45, -90, 90, "°", 1);
        public static readonly ParamDef ScanDef = Pm.Num("scan", "Mechanical scan range ±", "Mechanischer Scanbereich ±", 10, 1, 30, "°", 1);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            AngleDef, ScanDef,
            Pm.Bool("fan", "Scan range (end positions and ray fan)", "Scanbereich (Endlagen und Strahlenfächer)", false),
            Pm.Label,
        };

        public override Size2 DefaultSize => new(20, 20);
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<string> Companions => new[] { Sources.LaserId, LensType.ConvergingId, Detectors.ScreenId, MirrorType.PlaneId };

        private static double Angle(SketchObject o) => Geo.Clamp(o.Num("angle", 45), -90, 90);
        private static double Scan(SketchObject o) => Geo.Clamp(o.Num("scan", 10), 1, 30);
        private static bool ShowsFan(SketchObject o) => o.Flag("fan", false);

        /// <summary>Front normal (reflecting side) of the mirror at <paramref name="deg"/>: local −x turned by the angle.</summary>
        internal static Vec2 FrontNormal(double deg) => RayTracer.Rotate(new Vec2(-1, 0), deg);

        /// <summary>Half length of the mirror, the hatch depth and the pivot radius, so every drawn position stays in the box.</summary>
        private static (double Half, double Depth, double Pivot) Layout(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var min = Math.Min(o.Size.W, o.Size.H);
            var depth = Geo.Clamp(0.08 * min, 0.3, 1.6);
            var pivot = Geo.Clamp(0.09 * min, 0.3, 1.6);
            const double margin = 0.3;
            var angles = ShowsFan(o) ? new[] { Angle(o) - Scan(o), Angle(o), Angle(o) + Scan(o) } : new[] { Angle(o) };
            var half = double.PositiveInfinity;
            foreach (var a in angles)
            {
                var v = Grips.LineDirection(a);
                var back = FrontNormal(a) * -1;
                // The mirror ends ±half·v and the hatching behind them (up to depth along the back normal) stay inside.
                var lx = Math.Abs(v.X) > 1e-9 ? (hw - margin - Math.Abs(back.X) * depth) / Math.Abs(v.X) : double.PositiveInfinity;
                var ly = Math.Abs(v.Y) > 1e-9 ? (hh - margin - Math.Abs(back.Y) * depth) / Math.Abs(v.Y) : double.PositiveInfinity;
                half = Math.Min(half, Math.Min(lx, ly));
            }
            return (Math.Max(0.2, half), depth, pivot);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var (half, depth, pivot) = Layout(o);
            var a = Angle(o);
            var v = Grips.LineDirection(a);
            var back = FrontNormal(a) * -1;
            var list = new List<Primitive>();
            if (ShowsFan(o))
            {
                var ghost = Style.ThinLine with { StrokeWidth = 0.25, Dash = DashStyle.Dash };
                foreach (var e in new[] { a - Scan(o), a + Scan(o) })
                {
                    var ve = Grips.LineDirection(e);
                    list.Add(new LinePrim(ve * -half, ve * half, ghost) { Tag = OpticsTags.Detail });
                }
            }
            // Galvo shaft (pivot) behind the mirror.
            list.Add(new EllipsePrim(Vec2.Zero, pivot, pivot, Ox.ThinFilled(Ox.Housing)) { Tag = OpticsTags.Detail });
            // Hatching on the back, 45° strokes starting on the mirror.
            var hatch = Style.ThinLine with { StrokeWidth = 0.25 };
            var step = Math.Max(0.8, Math.Min(1.8, 2 * half / 10));
            for (var t = -half; t <= half - depth + 1e-6; t += step)
                list.Add(new LinePrim(v * t, v * t + (back + v) * depth, hatch) { Tag = OpticsTags.Hatch });
            list.Add(new LinePrim(v * -half, v * half, Ox.Outline) { Tag = OpticsTags.Body });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var (half, _, _) = Layout(o);
            var v = Grips.LineDirection(Angle(o));
            return new[] { new Surface(SurfaceKind.Reflect, v * -half, v * half) { Normal = FrontNormal(Angle(o)), Fan = ShowsFan(o) ? Scan(o) : 0 } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        /// <summary>The mirror angle as a grip on the upper end of the mirror, dragged around the pivot (whole degrees).</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var (half, _, _) = Layout(o);
            yield return Grips.Page(o, Grips.MirrorAngle, Grips.LineDirection(Angle(o)) * half, DragConstraint.Free,
                Grips.Label("Mirror angle", "Spiegelwinkel", Angle(o), "°"), OpticsTags.Body);
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
            => key == Grips.MirrorAngle && Grips.LineDegrees(Grips.ToLocal(o, pagePos)) is double deg ? o.WithParam("angle", deg) : o;

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Mirror surface", "Spiegelfläche"), "line");
            yield return new PartInfo(OpticsTags.Hatch, new I18nText("Hatching", "Schraffur"), "line");
            yield return new PartInfo(OpticsTags.Detail, new I18nText("Pivot and end positions", "Drehachse und Endlagen"), "shape");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.NumberInput(o, "optics.scanner.angle", new I18nText("Mirror angle", "Spiegelwinkel"), AngleDef, "scanner");
            yield return Ox.Toggle(o, "optics.scanner.fan", new I18nText("Scan range", "Scanbereich"), "fan", false, "scanner");
            // The scan range shows end positions of the mirror: the ghost-positions pictogram (the angle one is the mirror angle's).
            yield return Ox.NumberInput(o, "optics.scanner.scan", new I18nText("Scan range ±", "Scanbereich ±"), ScanDef, "scanner") with { Icon = IconIds.Ghosts };
        }
    }

    /// <summary>
    /// Light guide: a rectangular glass block (index <c>n</c>) whose four faces refract by Snell's law; light meeting a side
    /// face beyond the critical angle arcsin(1/n) is totally reflected, so it is guided to the far end.
    /// </summary>
    internal sealed class LightGuideType : OpticsType
    {
        public const string TypeId = "optics.lightguide";

        public LightGuideType() : base(TypeId, new I18nText("Light guide (rod)", "Lichtleitstab"), OpticsCategories.Components,
            "light guide", "light pipe", "total internal reflection", "waveguide", "rod", "lichtleiter", "lichtleitstab", "totalreflexion", "wellenleiter", "glasstab")
        { }

        public static readonly ParamDef IndexDef = Pm.Num("n", "Refractive index n", "Brechzahl n", 1.5, 1.01, 3.0, null, 0.01);

        public static readonly ParamDef CoatingDef = Pm.Coating("ideal");

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { IndexDef, CoatingDef, Pm.Label };
        public override Size2 DefaultSize => new(80, 8);
        public override IReadOnlyList<string> Companions => new[] { Sources.LaserId, LensType.ConvergingId, Detectors.PhotodiodeId, Detectors.ScreenId };

        /// <summary>Critical angle (degrees from the face normal) of glass with index <paramref name="n"/> against air.</summary>
        public static double CriticalAngle(double n) => Geo.RadToDeg(Math.Asin(1 / Math.Max(1.0001, n)));

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, Ox.GlassBody) with { Tag = OpticsTags.Body } };
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var n = Geo.Clamp(o.Num("n", 1.5), 1.01, 3.0);
            var coating = Fresnel.Of(o);
            Surface Face(Vec2 a, Vec2 b, Vec2 outward) => new(SurfaceKind.Refract, a, b) { Normal = outward, Index = n, Coating = coating };
            return new[]
            {
                Face(new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(0, -1)),
                Face(new Vec2(hw, -hh), new Vec2(hw, hh), new Vec2(1, 0)),
                Face(new Vec2(hw, hh), new Vec2(-hw, hh), new Vec2(0, 1)),
                Face(new Vec2(-hw, hh), new Vec2(-hw, -hh), new Vec2(-1, 0)),
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var hw = o.Size.W / 2;
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-hw, 0), "in");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(hw, 0), "exit");
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Glass block", "Glaskörper"), "shape");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.NumberInput(o, "optics.n", new I18nText("Refractive index", "Brechzahl"), IndexDef, "guide");
            foreach (var opt in Ox.CoatingChoice(o, CoatingDef, "guide")) yield return opt;
        }
    }
}
