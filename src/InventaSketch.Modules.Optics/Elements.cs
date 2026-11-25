using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>
    /// Converging or diverging lens: drawn as a section (biconvex, plano-convex, meniscus / biconcave, plano-concave,
    /// meniscus) or as the schematic double-arrow symbol. Traced as an ideal thin lens in its centre plane with the focal
    /// length <c>f</c> (drawing scale); the box height is its aperture. The optical axis is the local x axis.
    /// </summary>
    internal sealed class LensType : OpticsType
    {
        public const string ConvergingId = "optics.lens.converging";
        public const string DivergingId = "optics.lens.diverging";

        public LensType(bool converging) : base(converging ? ConvergingId : DivergingId,
            converging ? new I18nText("Converging lens", "Sammellinse") : new I18nText("Diverging lens", "Zerstreuungslinse"),
            OpticsCategories.Lenses,
            converging
                ? new[] { "lens", "converging", "convex", "positive", "focus", "linse", "sammellinse", "konvex", "brennpunkt" }
                : new[] { "lens", "diverging", "concave", "negative", "linse", "zerstreuungslinse", "konkav", "streulinse" })
        {
            Converging = converging;
            Shape = converging
                ? Pm.Choice("shape", "Lens shape", "Linsenform", "biconvex",
                    ("biconvex", "Biconvex", "Bikonvex"), ("planoconvex", "Plano-convex", "Plankonvex"), ("meniscus", "Meniscus (positive)", "Meniskus (positiv)"))
                : Pm.Choice("shape", "Lens shape", "Linsenform", "biconcave",
                    ("biconcave", "Biconcave", "Bikonkav"), ("planoconcave", "Plano-concave", "Plankonkav"), ("meniscus", "Meniscus (negative)", "Meniskus (negativ)"));
            Parameters = new[]
            {
                Shape, Pm.FocalLength(40), Pm.Focal,
                Pm.Rays(5), Bundle, Pm.Beam, Pm.Label,
            };
        }

        public bool Converging { get; }
        public ParamDef Shape { get; }

        public static readonly ParamDef Bundle = Pm.Choice("bundle", "Incoming bundle", "Einfallendes Bündel", "parallel",
            ("parallel", "Parallel to the axis", "Parallel zur Achse"), ("point", "From a point at 2f", "Von einem Punkt bei 2f"));

        public override IReadOnlyList<ParamDef> Parameters { get; }
        public override Size2 DefaultSize => new(8, 34);
        public override IReadOnlyList<ViewDef> Views { get; } = new[]
        {
            new ViewDef("shape", new I18nText("Lens section", "Linsenschnitt")) { Icon = IconIds.Detailed },
            new ViewDef("symbol", new I18nText("Symbol (double arrow)", "Symbol (Doppelpfeil)")) { Icon = IconIds.Symbol },
        };
        public override IReadOnlyList<string> Companions => new[] { ConvergingId, DivergingId, Beams.AxisId, Sources.LampId, Detectors.ScreenId, Sources.ObjectId };

        /// <summary>Signed focal length (positive converging).</summary>
        public double F(SketchObject o) => (Converging ? 1 : -1) * Math.Max(1, Math.Abs(o.Num("f", 40)));

        /// <summary>Surface x positions (centre, edge) of the left and right surfaces as fractions of the width.</summary>
        private (double Lc, double Le, double Rc, double Re) Profile(string shape) => (Converging, shape) switch
        {
            (true, "planoconvex") => (-0.5, -0.5, 0.5, -0.2),
            (true, "meniscus") => (-0.5, 0.05, 0.05, 0.5),
            (true, _) => (-0.5, -0.1, 0.5, 0.1),
            (false, "planoconcave") => (-0.5, -0.5, -0.3, 0.5),
            (false, "meniscus") => (-0.5, -0.25, -0.3, 0.5),
            (false, _) => (-0.1, -0.5, 0.1, 0.5),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, hh = o.Size.H / 2;
            var list = new List<Primitive>();
            if (ViewOf(o) == "symbol")
            {
                list.Add(new LinePrim(new Vec2(0, -hh), new Vec2(0, hh), Ox.Outline) { Tag = OpticsTags.Body });
                var a = Math.Min(w * 0.45, Math.Max(1.0, o.Size.H * 0.05));
                var b = Math.Min(2.4 * a, hh * 0.5);
                var head = new Style { Role = LineRole.Thin, StrokeWidth = 0.25, Fill = Fill.Solid(Ox.Black) };
                foreach (var sy in new[] { -1.0, 1.0 })
                {
                    var end = new Vec2(0, sy * hh);
                    var inner = new Vec2(0, sy * (hh - b));
                    var tri = Converging
                        ? new[] { end, new Vec2(-a, inner.Y), new Vec2(a, inner.Y) }
                        : new[] { inner, new Vec2(-a, end.Y), new Vec2(a, end.Y) };
                    list.Add(Prim.Polygon(tri, head) with { Tag = OpticsTags.Body });
                }
            }
            else
            {
                var (lc, le, rc, re) = Profile(o.Str("shape", Shape.Default!.Value.Text!));
                var left = Ox.Surface(lc * w, le * w, hh);
                var right = Ox.Surface(rc * w, re * w, hh);
                right.Reverse();
                list.Add(Prim.Polygon(left.Concat(right), Ox.GlassBody) with { Tag = OpticsTags.Body });
            }
            if (o.Flag("focal", false))
            {
                var f = Math.Abs(F(o));
                // F lies on the object side of a converging lens; F′ of a diverging lens is virtual, on the object side too.
                list.AddRange(Ox.FocalMark(new Vec2(-f, 0), Converging ? "F" : "F′"));
                list.AddRange(Ox.FocalMark(new Vec2(f, 0), Converging ? "F′" : "F"));
            }
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hh = o.Size.H / 2;
            return new[] { new Surface(SurfaceKind.ThinLens, new Vec2(0, -hh), new Vec2(0, hh)) { F = F(o), Centre = Vec2.Zero } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var f = Math.Abs(F(o));
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-f, 0), "f1");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(f, 0), "f2");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-2 * f, 0), "2f1");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(2 * f, 0), "2f2");
        }

        internal override string? EmitAnchor => "o";

        /// <summary>
        /// The lens's own demonstration bundle: parallel rays from the left (or rays from a point on the axis at 2f), across
        /// 70 % of the aperture unless a bundle height is set. They start in front of the lens and pass through it.
        /// </summary>
        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var m = o.Matrix();
            var f = Math.Abs(F(o));
            var hh = o.Size.H / 2;
            var beam = o.Num("beam", 0);
            var half = beam > 0 ? beam / 2 : 0.7 * hh;
            var n = Ox.ClampInt(o.Int("rays", 5), 1, 15);
            var heights = n == 1 ? new[] { -half } : Ox.Spaced(-half, half, n);
            var rays = new List<StartRay>();
            if (o.Str("bundle", "parallel") == "point")
            {
                var s = new Vec2(-2 * f, 0);
                foreach (var y in heights) rays.Add(new StartRay(m.Apply(s), m.ApplyLinear(new Vec2(0, y) - s).Normalized()));
            }
            else
            {
                var x = -Math.Max(f, 15);
                foreach (var y in heights) rays.Add(new StartRay(m.Apply(new Vec2(x, y)), m.ApplyLinear(new Vec2(1, 0)).Normalized()));
            }
            return new Emission(rays, null);
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Lens body", "Linsenkörper"), "shape");
            if (o.Flag("focal", false)) yield return new PartInfo(OpticsTags.Focal, new I18nText("Focal points", "Brennpunkte"), "mark");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            yield return Ox.Toggle(o, "optics.focal", new I18nText("Focal points F, F′", "Brennpunkte F, F′"), "focal", false, "lens");
            yield return Ox.NumberInput(o, "optics.f", new I18nText("Focal length", "Brennweite"), Parameters[1], "lens");
            if (ViewOf(o) != "symbol") yield return Ox.Cycle(o, "optics.shape", new I18nText("Lens shape", "Linsenform"), Shape, "lens");
            yield return Ox.Cycle(o, "optics.bundle", new I18nText("Parallel / point bundle", "Paralleles / punktförmiges Bündel"), Bundle, "rays");
            foreach (var a in Ox.RayCount(o, Parameters[3])) yield return a;
        }
    }

    /// <summary>
    /// Plane, concave or convex mirror: the reflecting surface (front, local −x side) with short hatching on the back.
    /// Curved mirrors reflect paraxially with focal length f (centre of curvature at 2f).
    /// </summary>
    internal sealed class MirrorType : OpticsType
    {
        public const string PlaneId = "optics.mirror.plane";
        public const string ConcaveId = "optics.mirror.concave";
        public const string ConvexId = "optics.mirror.convex";

        private readonly string _kind;

        public MirrorType(string kind) : base("optics.mirror." + kind, kind switch
            {
                "concave" => new I18nText("Concave mirror", "Hohlspiegel"),
                "convex" => new I18nText("Convex mirror", "Wölbspiegel"),
                _ => new I18nText("Plane mirror", "Planspiegel"),
            }, OpticsCategories.Mirrors,
            kind switch
            {
                "concave" => new[] { "mirror", "concave", "focusing", "reflector", "spiegel", "hohlspiegel", "konkav", "sammelspiegel" },
                "convex" => new[] { "mirror", "convex", "diverging", "spiegel", "wölbspiegel", "konvex", "zerstreuungsspiegel" },
                _ => new[] { "mirror", "plane", "flat", "reflection", "spiegel", "planspiegel", "eben", "reflexion" },
            })
        {
            _kind = kind;
            Parameters = kind == "plane"
                ? new[] { Pm.Label }
                : new[] { Pm.FocalLength(40), Pm.Focal, Pm.Label };
        }

        public override IReadOnlyList<ParamDef> Parameters { get; }
        public override Size2 DefaultSize => new(5, 34);
        public override IReadOnlyList<string> Companions => new[] { PlaneId, ConcaveId, Beams.AxisId, Sources.LaserId, Sources.LampId, Detectors.ScreenId };

        private bool Curved => _kind != "plane";

        /// <summary>Signed focal length: concave positive, convex negative; 0 for plane mirrors.</summary>
        public double F(SketchObject o) => _kind switch
        {
            "concave" => Math.Max(1, Math.Abs(o.Num("f", 40))),
            "convex" => -Math.Max(1, Math.Abs(o.Num("f", 40))),
            _ => 0,
        };

        /// <summary>The surface (from −h to +h), its vertex x and the hatch depth.</summary>
        private (List<Vec2> Pts, double Vertex, double Depth) Layout(SketchObject o)
        {
            double w = o.Size.W, hh = o.Size.H / 2;
            var x0 = -w / 2 + 0.25;
            var sag = Curved ? Math.Min(w * 0.3, o.Size.H * 0.08) : 0;
            var (xc, xe) = _kind switch
            {
                "concave" => (x0 + sag, x0),
                "convex" => (x0, x0 + sag),
                _ => (x0, x0),
            };
            var depth = Math.Max(0.3, Math.Min(2.2, w - 0.5 - sag));
            return (Ox.Surface(xc, xe, hh, 20), xc, depth);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var hh = o.Size.H / 2;
            var (pts, vertex, depth) = Layout(o);
            var list = new List<Primitive>();
            // Hatching on the back, 45° strokes starting on the surface.
            var hatch = Style.ThinLine with { StrokeWidth = 0.25 };
            var step = Math.Max(1.0, Math.Min(1.8, o.Size.H / 12));
            for (var y = -hh + depth; y <= hh + 1e-6; y += step)
            {
                var x = SurfaceX(pts, y);
                list.Add(new LinePrim(new Vec2(x, y), new Vec2(x + depth, y - depth), hatch) { Tag = OpticsTags.Hatch });
            }
            list.Add((pts.Count == 2 ? (Primitive)new LinePrim(pts[0], pts[1], Ox.Outline) : Prim.Polyline(pts, Ox.Outline)) with { Tag = OpticsTags.Body });
            if (Curved && o.Flag("focal", false))
            {
                var f = F(o);
                list.AddRange(Ox.FocalMark(new Vec2(vertex - f, 0), "F"));
                list.AddRange(Ox.FocalMark(new Vec2(vertex - 2 * f, 0), ctx.Lang == Lang.De ? "M" : "C"));
            }
            list.AddRange(Ox.Label(o));
            return list;
        }

        private static double SurfaceX(IReadOnlyList<Vec2> pts, double y)
        {
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                if (y >= Math.Min(a.Y, b.Y) - 1e-9 && y <= Math.Max(a.Y, b.Y) + 1e-9)
                    return Math.Abs(b.Y - a.Y) < 1e-12 ? a.X : a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
            }
            return pts[pts.Count - 1].X;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var (pts, vertex, _) = Layout(o);
            if (!Curved) return new[] { new Surface(SurfaceKind.Mirror, pts[0], pts[pts.Count - 1]) };
            return Surface.Chain(new Surface(SurfaceKind.CurvedMirror, default, default) { F = F(o), Centre = new Vec2(vertex, 0) }, pts).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var (_, vertex, _) = Layout(o);
            yield return Anchor.Point(AnchorRole.Port, new Vec2(vertex, 0), "o");
            if (!Curved) yield break;
            var f = F(o);
            yield return Anchor.Point(AnchorRole.Port, new Vec2(vertex - f, 0), "f");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(vertex - 2 * f, 0), "cc");
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Mirror surface", "Spiegelfläche"), "line");
            yield return new PartInfo(OpticsTags.Hatch, new I18nText("Hatching", "Schraffur"), "line");
            if (Curved && o.Flag("focal", false)) yield return new PartInfo(OpticsTags.Focal, new I18nText("Focal point and centre", "Brennpunkt und Mittelpunkt"), "mark");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            if (!Curved) yield break;
            yield return Ox.Toggle(o, "optics.focal", new I18nText("Focal point F and centre", "Brennpunkt F und Mittelpunkt"), "focal", false, "mirror");
            yield return Ox.NumberInput(o, "optics.f", new I18nText("Focal length", "Brennweite"), Parameters[0], "mirror");
        }
    }

    /// <summary>
    /// Glass prism (equilateral deviating prism or right-angle reflecting prism) traced with Snell's law at every face and
    /// total internal reflection. With dispersion, white light is split into five colours (n from a two-term Cauchy fit
    /// through the d-line index and n(F) − n(C)).
    /// </summary>
    internal sealed class PrismType : OpticsType
    {
        public const string TypeId = "optics.prism.triangle";

        public PrismType() : base(TypeId, new I18nText("Prism", "Prisma"), OpticsCategories.Mirrors,
            "prism", "dispersion", "refraction", "spectrum", "glass", "prisma", "dispersion", "brechung", "spektrum", "glas") { }

        public static readonly ParamDef ShapeDef = Pm.Choice("shape", "Prism shape", "Prismenform", "equilateral",
            ("equilateral", "Equilateral (deviating)", "Gleichseitig (ablenkend)"), ("rightangle", "Right-angle (reflecting)", "Rechtwinklig (umlenkend)"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            ShapeDef,
            Pm.Num("n", "Refractive index n (d line)", "Brechzahl n (d-Linie)", 1.5, 1.0, 3.0, null, 0.01),
            Pm.Bool("dispersion", "Dispersion (split white light)", "Dispersion (weißes Licht zerlegen)", false),
            Pm.Num("dn", "Dispersion n(F) − n(C)", "Dispersion n(F) − n(C)", 0.03, 0, 0.3, null, 0.005, advanced: true),
            Pm.Label,
        };

        public override Size2 DefaultSize => new(30, 26);
        public override IReadOnlyList<string> Companions => new[] { Sources.LampId, Sources.LaserId, Detectors.ScreenId, Beams.AxisId };

        private static List<Vec2> Corners(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            return o.Str("shape", "equilateral") == "rightangle"
                ? new List<Vec2> { new(-hw, -hh), new(hw, hh), new(-hw, hh) }
                : new List<Vec2> { new(0, -hh), new(hw, hh), new(-hw, hh) };
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var list = new List<Primitive> { Prim.Polygon(Corners(o), Ox.GlassBody) with { Tag = OpticsTags.Body } };
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var c = Corners(o);
            var centroid = new Vec2(c.Average(p => p.X), c.Average(p => p.Y));
            var n = Geo.Clamp(o.Num("n", 1.5), 1.0, 3.0);
            var dn = o.Flag("dispersion", false) ? Geo.Clamp(o.Num("dn", 0.03), 0, 0.3) : 0;
            var list = new List<Surface>();
            for (var i = 0; i < c.Count; i++)
            {
                var a = c[i]; var b = c[(i + 1) % c.Count];
                var normal = (b - a).Perp.Normalized();
                if (normal.Dot(Vec2.Lerp(a, b, 0.5) - centroid) < 0) normal = normal * -1;
                list.Add(new Surface(SurfaceKind.Refract, a, b) { Normal = normal, Index = n, Dispersion = dn });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Prism body", "Prismenkörper"), "shape");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.Toggle(o, "optics.dispersion", new I18nText("Dispersion", "Dispersion"), "dispersion", false, "prism");
            yield return Ox.NumberInput(o, "optics.n", new I18nText("Refractive index", "Brechzahl"), Parameters[1], "prism");
            yield return Ox.Cycle(o, "optics.prism.shape", new I18nText("Prism shape", "Prismenform"), ShapeDef, "prism");
        }
    }

    /// <summary>Beam splitter cube or plate: the diagonal surface (bottom left to top right) transmits and reflects.</summary>
    internal sealed class SplitterType : OpticsType
    {
        public const string CubeId = "optics.splitter.cube";
        public const string PlateId = "optics.splitter.plate";
        private readonly bool _cube;

        public SplitterType(bool cube) : base(cube ? CubeId : PlateId,
            cube ? new I18nText("Beam splitter cube", "Strahlteilerwürfel") : new I18nText("Beam splitter plate", "Strahlteilerplatte"),
            OpticsCategories.Mirrors,
            cube
                ? new[] { "beam splitter", "cube", "splitter", "interferometer", "strahlteiler", "würfel", "teilerwürfel", "interferometer" }
                : new[] { "beam splitter", "plate", "half mirror", "semi-transparent", "strahlteiler", "platte", "halbdurchlässig", "teilerspiegel" })
        {
            _cube = cube;
        }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label };
        public override Size2 DefaultSize => new(16, 16);
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<string> Companions => new[] { MirrorType.PlaneId, Sources.LaserId, Detectors.ScreenId, Detectors.PhotodiodeId };

        private (Vec2 A, Vec2 B) Diagonal(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            Vec2 a = new(-hw, hh), b = new(hw, -hh);
            if (_cube) return (a, b);
            var u = (b - a).Normalized();
            var t = Thickness(o);
            return (a + u * t, b - u * t);
        }

        private static double Thickness(SketchObject o) => Math.Max(0.6, 0.09 * Math.Min(o.Size.W, o.Size.H));

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive>();
            var (a, b) = Diagonal(o);
            if (_cube)
            {
                list.Add(Prim.Rect(-w / 2, -h / 2, w, h, Ox.GlassBody) with { Tag = OpticsTags.Body });
                list.Add(new LinePrim(a, b, Style.ThinLine with { StrokeWidth = 0.35 }) { Tag = OpticsTags.Detail });
            }
            else
            {
                var n = (b - a).Perp.Normalized() * (Thickness(o) / 2);
                list.Add(Prim.Polygon(new[] { a + n, b + n, b - n, a - n }, Ox.GlassBody) with { Tag = OpticsTags.Body });
            }
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var (a, b) = Diagonal(o);
            return new[] { new Surface(SurfaceKind.Split, a, b) };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Body", "Körper"), "shape");
            if (_cube) yield return new PartInfo(OpticsTags.Detail, new I18nText("Splitting surface", "Teilerfläche"), "line");
        }
    }

    /// <summary>Aperture stop or slit: two blades leaving an opening (fraction of the height) on the axis.</summary>
    internal sealed class StopType : OpticsType
    {
        public const string TypeId = "optics.stop.aperture";

        public StopType() : base(TypeId, new I18nText("Aperture stop", "Blende"), OpticsCategories.Components,
            "aperture", "stop", "diaphragm", "slit", "pinhole", "blende", "aperturblende", "spalt", "lochblende") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("opening", "Opening (fraction of the height)", "Öffnung (Anteil der Höhe)", 0.35, 0.02, 0.95, null, 0.05),
            Pm.Label,
        };

        public override Size2 DefaultSize => new(3, 34);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Detectors.ScreenId, Sources.LampId };

        private static double Open(SketchObject o) => Geo.Clamp(o.Num("opening", 0.35), 0.02, 0.95) * o.Size.H / 2;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hh = o.Size.H / 2, open = Open(o);
            var w = Math.Min(o.Size.W, 1.4);
            var blade = Ox.Filled(Ox.Black);
            var list = new List<Primitive>
            {
                Prim.Rect(-w / 2, -hh, w, hh - open, blade) with { Tag = OpticsTags.Body },
                Prim.Rect(-w / 2, open, w, hh - open, blade) with { Tag = OpticsTags.Body },
            };
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hh = o.Size.H / 2;
            return new[] { new Surface(SurfaceKind.Stop, new Vec2(0, -hh), new Vec2(0, hh)) { Open = Open(o), Centre = Vec2.Zero } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.NumberInput(o, "optics.opening", new I18nText("Opening", "Öffnung"), Parameters[0], "stop");
        }
    }

    /// <summary>Transmission grating: a plate with grooves; orders ±1 … ±3 leave at sin θm = sin θ + m·sin θ1·λ/550 nm.</summary>
    internal sealed class GratingType : OpticsType
    {
        public const string TypeId = "optics.grating.transmission";

        public GratingType() : base(TypeId, new I18nText("Diffraction grating", "Beugungsgitter"), OpticsCategories.Components,
            "grating", "diffraction", "orders", "spectrum", "gitter", "beugung", "beugungsgitter", "ordnung", "spektrum") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("angle", "First-order angle at 550 nm", "Winkel 1. Ordnung bei 550 nm", 20, 1, 70, "°", 1),
            Pm.Int("orders", "Orders on each side", "Ordnungen je Seite", 1, 1, 3),
            Pm.Label,
        };

        public override Size2 DefaultSize => new(4, 34);
        public override IReadOnlyList<string> Companions => new[] { Sources.LaserId, Sources.LampId, Detectors.ScreenId, LensType.ConvergingId };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hh = o.Size.H / 2, hw = o.Size.W / 2;
            var w = Math.Min(o.Size.W * 0.4, 1.2);
            var list = new List<Primitive> { Prim.Rect(-w / 2, -hh, w, 2 * hh, Ox.GlassBody) with { Tag = OpticsTags.Body } };
            var tick = Math.Max(0.2, Math.Min(hw - w / 2, 1.6));
            var groove = Style.ThinLine with { StrokeWidth = 0.18 };
            var step = Math.Max(0.8, Math.Min(1.4, o.Size.H / 24));
            for (var y = -hh + step / 2; y < hh; y += step)
                list.Add(new LinePrim(new Vec2(w / 2, y), new Vec2(w / 2 + tick, y), groove) { Tag = OpticsTags.Detail });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hh = o.Size.H / 2;
            return new[]
            {
                new Surface(SurfaceKind.Grating, new Vec2(0, -hh), new Vec2(0, hh))
                {
                    Sine = Math.Sin(Geo.DegToRad(Geo.Clamp(o.Num("angle", 20), 1, 70))),
                    Orders = Ox.ClampInt(o.Int("orders", 1), 1, 3),
                },
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.NumberInput(o, "optics.grating.angle", new I18nText("First-order angle", "Winkel 1. Ordnung"), Parameters[0], "grating");
            yield return new ContextAction("optics.grating.orders", new I18nText("More orders", "Mehr Ordnungen"), IconIds.CountMore,
                x => x.WithParam("orders", x.Int("orders", 1) % 3 + 1)) { Group = "grating", ParamKeys = new[] { "orders" } };
        }
    }

    /// <summary>Filter plate: neutral (passes everything) or a colour filter that passes its band and colours white light.</summary>
    internal sealed class FilterType : OpticsType
    {
        public const string TypeId = "optics.filter.plate";

        public FilterType() : base(TypeId, new I18nText("Optical filter", "Optischer Filter"), OpticsCategories.Components,
            "filter", "colour filter", "color filter", "neutral density", "optical filter", "farbfilter", "graufilter", "optischer filter") { }

        public static readonly ParamDef Tint = Pm.Choice("tint", "Filter colour", "Filterfarbe", "neutral",
            ("neutral", "Neutral (grey)", "Neutral (grau)"), ("red", "Red", "Rot"), ("green", "Green", "Grün"), ("blue", "Blue", "Blau"), ("yellow", "Yellow", "Gelb"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Tint, Pm.Label };
        public override Size2 DefaultSize => new(3, 30);
        public override IReadOnlyList<string> Companions => new[] { Sources.LampId, Detectors.ScreenId, LensType.ConvergingId };

        private static string Colour(string tint) => tint switch
        {
            "red" => "#ef9a9a",
            "green" => "#a5d6a7",
            "blue" => "#90caf9",
            "yellow" => "#fff59d",
            _ => "#cfd8dc",
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, Ox.Filled(Colour(o.Str("tint", "neutral")))) with { Tag = OpticsTags.Body } };
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hh = o.Size.H / 2;
            return new[] { new Surface(SurfaceKind.Filter, new Vec2(0, -hh), new Vec2(0, hh)) { Tint = o.Str("tint", "neutral") } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.Cycle(o, "optics.tint", new I18nText("Filter colour", "Filterfarbe"), Tint, "filter");
        }
    }

    /// <summary>
    /// Optical fibre with connectors at both ends. Light reaching the input face ends there (it is not traced through the
    /// fibre); a ray bundle docked on the output end leaves it as a cone.
    /// </summary>
    internal sealed class FibreType : OpticsType
    {
        public const string TypeId = "optics.fibre.cable";

        public FibreType() : base(TypeId, new I18nText("Optical fibre", "Lichtwellenleiter"), OpticsCategories.Components,
            "fibre", "fiber", "optical fibre", "light guide", "glasfaser", "lichtwellenleiter", "lwl", "faser") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Num("spread", "Half cone angle at the output", "Halber Kegelwinkel am Ausgang", 12, 1, 80, "°", 1),
            Pm.Rays(3),
            Pm.Label,
        };

        public override Size2 DefaultSize => new(50, 8);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Sources.LaserId, Detectors.PhotodiodeId };

        private static double Ferrule(SketchObject o) => Math.Min(6, o.Size.W * 0.15);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var c = Ferrule(o);
            var fh = 0.6 * hh;
            var conn = Ox.Filled(Ox.Housing);
            var list = new List<Primitive>
            {
                Prim.Rect(-hw, -fh, c, 2 * fh, conn) with { Tag = OpticsTags.Body },
                Prim.Rect(hw - c, -fh, c, 2 * fh, conn) with { Tag = OpticsTags.Body },
            };
            var a = new Vec2(-hw + c, 0);
            var b = new Vec2(hw - c, 0);
            var len = b.X - a.X;
            var amp = 0.75 * hh;
            var cable = Style.Visible with { StrokeWidth = 0.7 };
            list.Add(Prim.Path(new[] { PathSeg.Move(a), PathSeg.Cubic(new Vec2(a.X + len / 3, -amp), new Vec2(b.X - len / 3, amp), b) }, cable) with { Tag = OpticsTags.Body });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            double hw = o.Size.W / 2, fh = 0.6 * o.Size.H / 2;
            return new[]
            {
                new Surface(SurfaceKind.Absorb, new Vec2(-hw, -fh), new Vec2(-hw, fh)),
                new Surface(SurfaceKind.Absorb, new Vec2(hw, -fh), new Vec2(hw, fh)),
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var hw = o.Size.W / 2;
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-hw, 0), "in");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(hw, 0), "exit");
        }

        internal override string? EmitAnchor => "exit";

        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var m = o.Matrix();
            var origin = new Vec2(o.Size.W / 2, 0);
            var alpha = Geo.DegToRad(Geo.Clamp(o.Num("spread", 12), 1, 80));
            var n = Ox.ClampInt(o.Int("rays", 3), 1, 15);
            var rays = Ox.Spaced(-alpha, alpha, n).Select(a => new StartRay(m.Apply(origin), m.ApplyLinear(new Vec2(Math.Cos(a), Math.Sin(a))).Normalized())).ToList();
            return new Emission(rays, o.Id);
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            foreach (var a in Ox.RayCount(o, Parameters[1])) yield return a;
        }
    }
}
