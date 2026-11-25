using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>Light sources and the object arrow; helpers shared by the emitters.</summary>
    internal static class Sources
    {
        public const string LampId = "optics.source.lamp";
        public const string LedId = "optics.source.led";
        public const string LaserId = "optics.source.laser";
        public const string ObjectId = "optics.object.arrow";

        /// <summary>
        /// The nearest element ahead of <paramref name="origin"/> along <paramref name="axis"/> whose box the axis line
        /// crosses (centre within half its larger box side), with its distance along the axis; null if there is none.
        /// </summary>
        public static (SceneElement E, double Along)? Ahead(IReadOnlyList<SceneElement> scene, Vec2 origin, Vec2 axis, string? exclude)
        {
            (SceneElement, double)? best = null;
            foreach (var e in scene)
            {
                if (e.Object.Id == exclude) continue;
                var v = e.Centre - origin;
                var along = v.Dot(axis);
                if (along <= 1e-6) continue;
                var perp = Math.Abs(v.Cross(axis));
                if (perp > 0.5 * Math.Max(e.Object.Size.W, e.Object.Size.H)) continue;
                if (best is null || along < best.Value.Item2) best = (e, along);
            }
            return best;
        }

        /// <summary>
        /// The light entrance of an element: the centre and direction of its lens or curved mirror and 80 % of its half
        /// length (a camera's lens, the eye's pupil), else the box centre and 40 % of the box height.
        /// </summary>
        public static (Vec2 Centre, Vec2 Across, double Half) Aperture(SceneElement e)
        {
            if (e.Focusing is { } s)
            {
                var a = e.M.Apply(s.A);
                var b = e.M.Apply(s.B);
                return (Vec2.Lerp(a, b, 0.5), (b - a).Normalized(), 0.4 * a.DistanceTo(b));
            }
            return (e.Centre, e.Across, 0.4 * e.Object.Size.H);
        }

        /// <summary>
        /// Cone or parallel emission of a lamp or LED in local coordinates: from <paramref name="origin"/>, the drawn rays
        /// start at <paramref name="radius"/> (the bulb or dome).
        /// </summary>
        public static Emission Emit(SketchObject o, IReadOnlyList<SceneElement> scene, Vec2 origin, double radius, double lambda, bool radialAllowed)
        {
            var m = o.Matrix();
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var pageOrigin = m.Apply(origin);
            var n = Ox.ClampInt(o.Int("rays", 5), 1, 15);
            var mode = o.Str("emission", "cone");
            var rays = new List<StartRay>();
            if (mode == "parallel")
            {
                var half = ParallelHalf(o, scene, pageOrigin, axis);
                foreach (var y in n == 1 ? new[] { 0.0 } : Ox.Spaced(-half, half, n))
                    rays.Add(new StartRay(m.Apply(origin + new Vec2(radius, y)), axis, lambda));
            }
            else if (mode == "radial" && radialAllowed)
            {
                var count = Math.Max(3, n);
                for (var i = 0; i < count; i++)
                {
                    var t = 2 * Math.PI * i / count;
                    var dir = new Vec2(Math.Cos(t), Math.Sin(t));
                    rays.Add(new StartRay(m.Apply(origin + dir * radius), m.ApplyLinear(dir).Normalized(), lambda));
                }
            }
            else
            {
                var alpha = ConeAngle(o, scene, pageOrigin, axis);
                foreach (var t in Ox.Spaced(-alpha, alpha, n))
                {
                    var dir = new Vec2(Math.Cos(t), Math.Sin(t));
                    rays.Add(new StartRay(m.Apply(origin + dir * radius), m.ApplyLinear(dir).Normalized(), lambda));
                }
            }
            return new Emission(rays, o.Id);
        }

        /// <summary>
        /// Half cone angle (radians, 1° … 80°) of a lamp or LED: the parameter <c>spread</c>, or (0) just filling the lens,
        /// mirror or aperture of the next element on the axis, or 15° when there is none.
        /// </summary>
        public static double ConeAngle(SketchObject o, IReadOnlyList<SceneElement> scene, Vec2 pageOrigin, Vec2 axis)
        {
            var spread = o.Num("spread", 0);
            double alpha;
            if (spread > 0) alpha = Geo.DegToRad(Geo.Clamp(spread, 0.5, 80));
            else if (Ahead(scene, pageOrigin, axis, o.Id) is { } a)
            {
                var (c, _, half) = Aperture(a.E);
                alpha = Math.Atan2(half, Math.Max(1e-6, (c - pageOrigin).Dot(axis)));
            }
            else alpha = Geo.DegToRad(15);
            return Geo.Clamp(alpha, Geo.DegToRad(1), Geo.DegToRad(80));
        }

        /// <summary>
        /// Half height of a parallel bundle of a lamp or LED: half the parameter <c>beam</c>, or (0) 87.5 % of the aperture of
        /// the next element on the axis, or 60 % of the half box height when there is none.
        /// </summary>
        public static double ParallelHalf(SketchObject o, IReadOnlyList<SceneElement> scene, Vec2 pageOrigin, Vec2 axis)
        {
            var beam = o.Num("beam", 0);
            if (beam > 0) return beam / 2;
            return Ahead(scene, pageOrigin, axis, o.Id) is { } a ? 0.875 * Aperture(a.E).Half : 0.6 * o.Size.H / 2;
        }

        /// <summary>
        /// Grips of a lamp or LED with a docked ray bundle: the edge ray of the cone (half cone angle) or the edge of the
        /// parallel bundle (bundle height); none for emission in all directions or without docked rays.
        /// </summary>
        public static IEnumerable<DragHandle> EmitterGrips(SketchObject o, RenderContext ctx, Vec2 origin, double radius, bool radialAllowed)
        {
            if (Beams.BundlesOf(ctx.Doc, o.Id).Count == 0) yield break;
            var mode = o.Str("emission", "cone");
            if (mode == "radial" && radialAllowed) yield break;
            var scene = RayTracer.Scene(ctx.Doc, o);
            var m = o.Matrix();
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var pageOrigin = m.Apply(origin);
            if (mode == "parallel")
            {
                var half = ParallelHalf(o, scene, pageOrigin, axis);
                yield return Grips.Page(o, Grips.Beam, origin + new Vec2(radius, -half), DragConstraint.Vertical, Grips.BeamLabel(2 * half));
            }
            else yield return Grips.Cone(o, origin, ConeAngle(o, scene, pageOrigin, axis), radius + Math.Max(8, 2 * radius));
        }

        /// <summary>A lamp's or LED's grip dragged to <paramref name="pagePos"/>: the half cone angle or the bundle height.</summary>
        public static SketchObject MoveEmitterGrip(SketchObject o, string key, Vec2 pagePos, Vec2 origin) => key switch
        {
            Grips.Spread => o.WithParam("spread", Grips.ConeDegrees(Grips.ToLocal(o, pagePos), origin)),
            Grips.Beam => o.WithParam("beam", Grips.BundleHeight(Grips.ToLocal(o, pagePos).Y - origin.Y)),
            _ => o,
        };

        public static readonly ParamDef Emission = Pm.Choice("emission", "Emission", "Abstrahlung", "cone",
            ("cone", "Cone (point source)", "Kegel (Punktquelle)"), ("parallel", "Parallel (collimated)", "Parallel (kollimiert)"), ("radial", "All directions", "In alle Richtungen"));

        public static readonly ParamDef EmissionNoRadial = Pm.Choice("emission", "Emission", "Abstrahlung", "cone",
            ("cone", "Cone (point source)", "Kegel (Punktquelle)"), ("parallel", "Parallel (collimated)", "Parallel (kollimiert)"));
    }

    /// <summary>Incandescent lamp: bulb with filament and socket; emits a cone (filling the next element), parallel or all around.</summary>
    internal sealed class LampType : OpticsType
    {
        public LampType() : base(Sources.LampId, new I18nText("Lamp (light source)", "Lampe (Lichtquelle)"), OpticsCategories.Sources,
            "lamp", "bulb", "light source", "white light", "lampe", "glühlampe", "lichtquelle", "weißes licht") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Sources.Emission, Pm.Rays(5), Pm.Spread, Pm.Beam, Pm.Label };
        public override Size2 DefaultSize => new(18, 12);
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Beams.RayItem, Beams.AxisId, Detectors.ScreenId, PrismType.TypeId };

        private static (double R, double Cx) Bulb(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var r = Math.Max(0.5, Math.Min(hh * 0.95, o.Size.W * 0.33));
            return (r, hw - r);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var hw = o.Size.W / 2;
            var (r, cx) = Bulb(o);
            var sh = 0.45 * r;
            var join = cx - Math.Sqrt(r * r - sh * sh);
            var list = new List<Primitive>();
            if (join > -hw + 0.3)
            {
                list.Add(Prim.Rect(-hw, -sh, join + hw, 2 * sh, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body });
                var thread = Style.ThinLine with { StrokeWidth = 0.18 };
                var len = join + hw;
                for (var i = 1; i <= 3; i++)
                {
                    var x = -hw + len * i / 4;
                    list.Add(new LinePrim(new Vec2(x, -sh), new Vec2(x, sh), thread) { Tag = OpticsTags.Detail });
                }
            }
            list.Add(new EllipsePrim(new Vec2(cx, 0), r, r, Ox.Filled("#fffde7")) { Tag = OpticsTags.Body });
            var thin = Style.ThinLine with { StrokeWidth = 0.2 };
            var fil = new List<Vec2>();
            const int zig = 6;
            for (var i = 0; i <= zig; i++) fil.Add(new Vec2(cx + (i % 2 == 0 ? -0.12 : 0.12) * r, -0.3 * r + 0.6 * r * i / zig));
            list.Add(Prim.Polyline(fil, thin) with { Tag = OpticsTags.Detail });
            list.Add(new LinePrim(new Vec2(join, -0.2 * r), fil[0], thin) { Tag = OpticsTags.Detail });
            list.Add(new LinePrim(new Vec2(join, 0.2 * r), fil[fil.Count - 1], thin) { Tag = OpticsTags.Detail });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var (r, cx) = Bulb(o);
            var pts = Geo.ArcPoints(new Vec2(cx, 0), r, r, 0, 2 * Math.PI, 24);
            return Surface.Chain(new Surface(SurfaceKind.Absorb, default, default), pts).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var (r, cx) = Bulb(o);
            yield return Anchor.Point(AnchorRole.Port, new Vec2(cx + r, 0), "exit");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(cx, 0), "o");
        }

        internal override string? EmitAnchor => "exit";

        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var (r, cx) = Bulb(o);
            return Sources.Emit(o, scene, new Vec2(cx, 0), r, 0, radialAllowed: true);
        }

        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var (r, cx) = Bulb(o);
            return Sources.EmitterGrips(o, ctx, new Vec2(cx, 0), r, radialAllowed: true);
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
            => Sources.MoveEmitterGrip(o, key, pagePos, new Vec2(Bulb(o).Cx, 0));

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            foreach (var opt in Ox.Choice(o, "optics.emission", new I18nText("Emission", "Abstrahlung"), Sources.Emission, "rays")) yield return opt;
            foreach (var a in Ox.RayCount(o, Parameters[1])) yield return a;
        }
    }

    /// <summary>Light-emitting diode with dome, flange and leads; white or coloured light.</summary>
    internal sealed class LedType : OpticsType
    {
        public LedType() : base(Sources.LedId, new I18nText("LED light source", "LED-Lichtquelle"), OpticsCategories.Sources,
            "led", "light emitting diode", "light source", "diode", "leuchtdiode", "lichtquelle", "led-lichtquelle") { }

        public static readonly ParamDef Colour = Pm.Choice("color", "Light colour", "Lichtfarbe", "white",
            ("white", "White", "Weiß"), ("red", "Red", "Rot"), ("green", "Green", "Grün"), ("blue", "Blue", "Blau"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Sources.EmissionNoRadial, Pm.Rays(5), Pm.Spread, Pm.Beam, Colour, Pm.Label };
        public override Size2 DefaultSize => new(16, 10);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Beams.RayItem, Beams.AxisId, Detectors.PhotodiodeId };

        private static double Lambda(string c) => c switch { "red" => 630, "green" => 525, "blue" => 465, _ => 0 };

        private static string Tint(string c) => c switch { "red" => "#ffcdd2", "green" => "#c8e6c9", "blue" => "#bbdefb", _ => "#fffde7" };

        private static (double R, double Xd, double Xb) Dome(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var r = Math.Max(0.4, Math.Min(hh * 0.75, o.Size.W * 0.22));
            var xd = hw - r;
            var xb = Math.Max(-hw + 0.5, xd - 1.3 * r);
            return (r, xd, xb);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var (r, xd, xb) = Dome(o);
            var list = new List<Primitive>();
            var lead = Style.ThinLine with { StrokeWidth = 0.35 };
            var rim = Math.Min(0.8, Math.Max(0.2, xb + hw));
            foreach (var sy in new[] { -0.45, 0.45 })
                list.Add(new LinePrim(new Vec2(-hw, sy * r), new Vec2(xb - rim, sy * r), lead) { Tag = OpticsTags.Detail });
            list.Add(Prim.Rect(xb - rim, -Math.Min(hh, r + 0.6), rim, 2 * Math.Min(hh, r + 0.6), Ox.Filled(Tint(o.Str("color", "white")))) with { Tag = OpticsTags.Body });
            var outline = new List<Vec2> { new(xb, r), new(xb, -r) };
            outline.AddRange(Geo.ArcPoints(new Vec2(xd, 0), r, r, -Math.PI / 2, Math.PI, 16));
            list.Add(Prim.Polygon(outline, Ox.Filled(Tint(o.Str("color", "white")))) with { Tag = OpticsTags.Body });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var (r, xd, xb) = Dome(o);
            var pts = new List<Vec2> { new(xb, r), new(xb, -r) };
            pts.AddRange(Geo.ArcPoints(new Vec2(xd, 0), r, r, -Math.PI / 2, Math.PI, 12));
            pts.Add(new Vec2(xb, r));
            return Surface.Chain(new Surface(SurfaceKind.Absorb, default, default), pts).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var (r, xd, _) = Dome(o);
            yield return Anchor.Point(AnchorRole.Port, new Vec2(xd + r, 0), "exit");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(xd, 0), "o");
        }

        internal override string? EmitAnchor => "exit";

        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var (r, xd, _) = Dome(o);
            return Sources.Emit(o, scene, new Vec2(xd, 0), r, Lambda(o.Str("color", "white")), radialAllowed: false);
        }

        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var (r, xd, _) = Dome(o);
            return Sources.EmitterGrips(o, ctx, new Vec2(xd, 0), r, radialAllowed: false);
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
            => Sources.MoveEmitterGrip(o, key, pagePos, new Vec2(Dome(o).Xd, 0));

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            foreach (var opt in Ox.Choice(o, "optics.emission", new I18nText("Emission", "Abstrahlung"), Sources.EmissionNoRadial, "rays")) yield return opt;
            foreach (var opt in Ox.Choice(o, "optics.color", new I18nText("Light colour", "Lichtfarbe"), Colour, "rays")) yield return opt;
            foreach (var a in Ox.RayCount(o, Parameters[1])) yield return a;
        }
    }

    /// <summary>Laser: housing with output aperture; a parallel beam (one ray, or several across the beam height).</summary>
    internal sealed class LaserType : OpticsType
    {
        public LaserType() : base(Sources.LaserId, new I18nText("Laser", "Laser"), OpticsCategories.Sources,
            "laser", "beam", "coherent", "monochromatic", "laserstrahl", "kohärent", "monochromatisch", "strahlquelle") { }

        public static readonly ParamDef Colour = Pm.Choice("color", "Wavelength", "Wellenlänge", "red",
            ("red", "Red (650 nm)", "Rot (650 nm)"), ("green", "Green (532 nm)", "Grün (532 nm)"), ("blue", "Blue (445 nm)", "Blau (445 nm)"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Rays(1), Pm.Beam, Colour, Pm.Label };
        public override Size2 DefaultSize => new(28, 10);
        public override IReadOnlyList<string> Companions => new[] { MirrorType.PlaneId, SplitterType.CubeId, LensType.ConvergingId, Beams.RayItem, Detectors.ScreenId };

        internal static double Lambda(string c) => c switch { "green" => 532, "blue" => 445, _ => 650 };

        private static double Nozzle(SketchObject o) => Math.Min(4, o.Size.W * 0.15);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var nz = Nozzle(o);
            var bh = 0.85 * hh;
            var nh = Math.Max(0.4, 0.38 * hh);
            var list = new List<Primitive>
            {
                Prim.Rect(-hw, -bh, o.Size.W - nz, 2 * bh, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body },
                Prim.Rect(hw - nz, -nh, nz, 2 * nh, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body },
            };
            var cap = -hw + Math.Min(3, o.Size.W * 0.1);
            list.Add(new LinePrim(new Vec2(cap, -bh), new Vec2(cap, bh), Style.ThinLine) { Tag = OpticsTags.Detail });
            var dot = Math.Min(0.9, bh * 0.4);
            var colour = Spectrum.Color(Lambda(o.Str("color", "red")))!;
            list.Add(new EllipsePrim(new Vec2(hw - nz - dot - 1.2, 0), dot, dot, new Style { Role = LineRole.Thin, StrokeWidth = 0.15, Stroke = Ox.Black, Fill = Fill.Solid(colour) }) { Tag = OpticsTags.Detail });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var bh = 0.85 * hh;
            var pts = new List<Vec2> { new(-hw, -bh), new(hw, -bh), new(hw, bh), new(-hw, bh), new(-hw, -bh) };
            return Surface.Chain(new Surface(SurfaceKind.Absorb, default, default), pts).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, new Vec2(o.Size.W / 2, 0), "exit");
        }

        internal override string? EmitAnchor => "exit";

        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var m = o.Matrix();
            var n = Ox.ClampInt(o.Int("rays", 1), 1, 15);
            var beam = o.Num("beam", 0);
            var half = beam > 0 ? beam / 2 : n > 1 ? 0.3 * o.Size.H / 2 : 0;
            var lambda = Lambda(o.Str("color", "red"));
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var rays = (n == 1 ? new[] { 0.0 } : Ox.Spaced(-half, half, n))
                .Select(y => new StartRay(m.Apply(new Vec2(o.Size.W / 2, y)), axis, lambda)).ToList();
            return new Emission(rays, o.Id);
        }

        /// <summary>Half height of the beam of several rays: half the parameter <c>beam</c>, or 30 % of the half box height.</summary>
        private static double Half(SketchObject o) => o.Num("beam", 0) is var beam && beam > 0 ? beam / 2 : 0.3 * o.Size.H / 2;

        /// <summary>With several rays and a docked bundle: the edge of the beam (bundle height), at the output aperture.</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            if (Ox.ClampInt(o.Int("rays", 1), 1, 15) < 2 || Beams.BundlesOf(ctx.Doc, o.Id).Count == 0) yield break;
            var half = Half(o);
            yield return Grips.Page(o, Grips.Beam, new Vec2(o.Size.W / 2, -half), DragConstraint.Vertical, Grips.BeamLabel(2 * half));
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
            => key == Grips.Beam ? o.WithParam("beam", Grips.BundleHeight(Grips.ToLocal(o, pagePos).Y)) : o;

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            foreach (var opt in Ox.Choice(o, "optics.color", new I18nText("Wavelength", "Wellenlänge"), Colour, "rays")) yield return opt;
            foreach (var a in Ox.RayCount(o, Parameters[0])) yield return a;
        }
    }

    /// <summary>
    /// Object arrow for imaging figures: an upright arrow standing on the optical axis (foot) with its tip as a point
    /// source. A docked ray bundle draws the principal rays (parallel, central, focal ray) to the next lens or curved
    /// mirror on the axis, or a fan filling it.
    /// </summary>
    internal sealed class ObjectArrowType : OpticsType
    {
        public ObjectArrowType() : base(Sources.ObjectId, new I18nText("Object (arrow)", "Gegenstand (Pfeil)"), OpticsCategories.Beams,
            "object", "arrow", "imaging", "principal rays", "gegenstand", "pfeil", "abbildung", "hauptstrahlen", "konstruktionsstrahlen") { }

        public static readonly ParamDef Bundle = Pm.Choice("bundle", "Rays from the tip", "Strahlen von der Spitze", "principal",
            ("principal", "Principal rays", "Konstruktionsstrahlen"), ("fan", "Fan filling the lens", "Fächer über die Linse"));

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Bundle, Pm.Rays(5), Imaging.Mode, Imaging.NamesDef, Pm.Label };
        public override Size2 DefaultSize => new(4, 16);
        public override bool IsPart => false;
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Beams.AxisId, MirrorType.ConcaveId, Detectors.ScreenId };

        /// <summary>
        /// The arrow, its optional name and – with a lens or curved mirror in front of it on its axis – its image (real solid,
        /// virtual dashed) and optionally the construction rays (<see cref="Imaging"/>). The image lies outside the box, like the
        /// focal point marks of a lens.
        /// </summary>
        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var hh = o.Size.H / 2;
            var list = new List<Primitive>();
            list.AddRange(Imaging.Render(o, ctx));
            list.Add(new LinePrim(new Vec2(0, hh), new Vec2(0, -hh), Ox.Outline with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium }) { Tag = OpticsTags.Body });
            if (o.Flag("names", false)) list.Add(Imaging.Name(new Vec2(1.2, -hh), ctx.Lang == Lang.De ? "G" : "O"));
            list.AddRange(Ox.Label(o, new Vec2(0, -hh - 1.2 - Ox.LabelSize)));
            return list;
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Object arrow", "Gegenstandspfeil"), "line");
            if (Imaging.Shown(o) && Imaging.Find(o, ctx) is { } frame)
            {
                var hh = o.Size.H / 2;
                var m = o.Matrix();
                var tip = Imaging.Image(frame, m.Apply(new Vec2(0, -hh)));
                if (tip is not null && Imaging.Image(frame, m.Apply(new Vec2(0, hh))) is not null)
                    yield return new PartInfo(OpticsTags.Image, new I18nText("Image", "Bild"), "line");
                if (o.Str("image", "image") == "construction")
                    yield return new PartInfo(OpticsTags.Construction, new I18nText("Construction rays", "Konstruktionsstrahlen"), "line");
            }
            if (o.Flag("names", false)) yield return new PartInfo(OpticsTags.Names, new I18nText("Names", "Namen"), "text");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var hh = o.Size.H / 2;
            yield return Anchor.Point(AnchorRole.Port, new Vec2(0, hh), "foot");
            yield return Anchor.Point(AnchorRole.Port, new Vec2(0, -hh), "tip");
        }

        internal override string? EmitAnchor => "tip";

        internal override Emission? Emit(SketchObject o, IReadOnlyList<SceneElement> scene)
        {
            var m = o.Matrix();
            var hh = o.Size.H / 2;
            var tip = m.Apply(new Vec2(0, -hh));
            var foot = m.Apply(new Vec2(0, hh));
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var ahead = Sources.Ahead(scene, foot, axis, o.Id);
            var rays = new List<StartRay>();
            Vec2 Forward(Vec2 d) => d.Dot(axis) < 0 ? d * -1 : d;
            if (o.Str("bundle", "principal") == "fan")
            {
                var n = Ox.ClampInt(o.Int("rays", 5), 1, 15);
                if (ahead is { } a)
                {
                    var (c, across, half) = Sources.Aperture(a.E);
                    foreach (var y in Ox.Spaced(-half, half, n))
                    {
                        var target = c + across * y;
                        rays.Add(new StartRay(tip, Forward((target - tip).Normalized())));
                    }
                }
                else foreach (var t in Ox.Spaced(-Geo.DegToRad(10), Geo.DegToRad(10), n)) rays.Add(new StartRay(tip, axis.Rotate(t)));
                return new Emission(rays, o.Id);
            }
            rays.Add(new StartRay(tip, axis));
            if (ahead is { } e && e.E.Focusing is { } s)
            {
                var c = e.E.M.Apply(s.Centre);
                var ea = e.E.Axis;
                if (ea.Dot(axis) < 0) ea = ea * -1;
                // Central ray through the optical centre (vertex), focal ray through (or towards) the front focal point.
                rays.Add(new StartRay(tip, Forward((c - tip).Normalized())));
                var focal = c - ea * s.F;
                if (focal.DistanceTo(tip) > 1e-6) rays.Add(new StartRay(tip, Forward((focal - tip).Normalized())));
            }
            else
            {
                rays.Add(new StartRay(tip, axis.Rotate(Geo.DegToRad(8))));
                rays.Add(new StartRay(tip, axis.Rotate(Geo.DegToRad(16))));
            }
            return new Emission(rays, o.Id);
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var a in base.OwnActions(o, doc)) yield return a;
            foreach (var opt in Ox.Choice(o, "optics.bundle", new I18nText("Rays", "Strahlen"), Bundle, "rays")) yield return opt;
            foreach (var opt in Ox.Choice(o, "optics.image", new I18nText("Image", "Bild"), Imaging.Mode, "image")) yield return opt;
            yield return Ox.Toggle(o, "optics.names", new I18nText("Names of object and image", "Namen Gegenstand und Bild"), "names", false, "image");
        }
    }
}
