using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    internal static class Detectors
    {
        public const string PhotodiodeId = "optics.detector.photodiode";
        public const string CameraId = "optics.detector.camera";
        public const string EyeId = "optics.detector.eye";
        public const string ScreenId = "optics.screen.plain";
        public const string SpectrometerId = "optics.detector.spectrometer";

        /// <summary>Closed rectangle outline as absorbing segments.</summary>
        public static IEnumerable<Surface> Box(double x0, double y0, double x1, double y1)
            => Surface.Chain(new Surface(SurfaceKind.Absorb, default, default), new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1), new Vec2(x0, y0) });
    }

    /// <summary>
    /// Base of the detectors and the screen: element values read from the traced light that ends on the element – received
    /// power, share of the emitted light, degree and angle of polarisation (<see cref="OpticsValues.DetectorLinks"/>).
    /// </summary>
    internal abstract class DetectorBase : OpticsType, IValueType, IValueLinks, IValueChecks, ITraceValues
    {
        protected DetectorBase(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        public IReadOnlyList<VarDef> Variables => OpticsValues.DetectorVariables;
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();
        public IEnumerable<ValueCheck> ValueChecks => OpticsValues.DetectorChecks;
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => OpticsValues.DetectorLinks(o, ctx);
    }

    /// <summary>Photodiode as an optical detector: housing with the dark active area on its front and two leads.</summary>
    internal sealed class PhotodiodeType : DetectorBase
    {
        public PhotodiodeType() : base(Detectors.PhotodiodeId, new I18nText("Photodiode (optical detector)", "Fotodiode (optischer Detektor)"), OpticsCategories.Detectors,
            "photodiode", "detector", "sensor", "receiver", "fotodiode", "photodiode", "detektor", "empfänger") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label, ValueLabels.ShowValues };
        public override Size2 DefaultSize => new(12, 10);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Sources.LaserId, SplitterType.CubeId };

        private static (double Front, double Back, double Bh) Housing(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var active = Math.Min(1.0, o.Size.W * 0.12);
            var leads = Math.Min(3, o.Size.W * 0.25);
            return (-hw + active, hw - leads, 0.9 * hh);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var (front, back, bh) = Housing(o);
            var list = new List<Primitive>
            {
                Prim.Rect(front, -bh, back - front, 2 * bh, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body },
                Prim.Rect(-hw, -0.6 * hh, front + hw, 1.2 * hh, Ox.ThinFilled(Ox.Dark)) with { Tag = OpticsTags.Detail },
            };
            var lead = Style.ThinLine with { StrokeWidth = 0.35 };
            foreach (var sy in new[] { -0.35, 0.35 })
                list.Add(new LinePrim(new Vec2(back, sy * hh), new Vec2(hw, sy * hh), lead) { Tag = OpticsTags.Detail });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hw = o.Size.W / 2;
            var (_, back, bh) = Housing(o);
            return Detectors.Box(-hw, -bh, back, bh).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-o.Size.W / 2, 0), "o");
        }
    }

    /// <summary>Camera with lens barrel and image sensor: the lens images onto the sensor (thin lens focused on it).</summary>
    internal sealed class CameraType : DetectorBase
    {
        public CameraType() : base(Detectors.CameraId, new I18nText("Camera (image sensor)", "Kamera (Bildsensor)"), OpticsCategories.Detectors,
            "camera", "image sensor", "imaging", "ccd", "cmos", "kamera", "bildsensor", "aufnahme") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label, ValueLabels.ShowValues };
        public override Size2 DefaultSize => new(26, 16);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Sources.ObjectId, Sources.LampId };

        private sealed record Geo2(double Hw, double Hh, double BarrelEnd, double Bh, double BodyH, double LensX, double LensH, double SensorX, double SensorH);

        private static Geo2 G(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var lb = 0.3 * o.Size.W;
            var lensX = -hw + 0.35 * lb;
            return new Geo2(hw, hh, -hw + lb, 0.6 * hh, 0.95 * hh, lensX, 0.55 * hh, hw - 0.2 * o.Size.W, 0.6 * hh);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var g = G(o);
            var list = new List<Primitive>
            {
                Prim.Rect(g.BarrelEnd, -g.BodyH, g.Hw - g.BarrelEnd, 2 * g.BodyH, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body },
                Prim.Rect(-g.Hw, -g.Bh, g.BarrelEnd + g.Hw, 2 * g.Bh, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body },
            };
            var lw = Math.Min(1.6, (g.BarrelEnd + g.Hw) * 0.3);
            var left = Ox.Surface(g.LensX - lw / 2, g.LensX - lw * 0.1, g.LensH, 10);
            var right = Ox.Surface(g.LensX + lw / 2, g.LensX + lw * 0.1, g.LensH, 10);
            right.Reverse();
            list.Add(Prim.Polygon(left.Concat(right), Ox.ThinFilled(Ox.Glass)) with { Tag = OpticsTags.Detail });
            var sw = Math.Min(0.8, o.Size.W * 0.04);
            list.Add(Prim.Rect(g.SensorX - sw / 2, -g.SensorH, sw, 2 * g.SensorH, Ox.ThinFilled(Ox.Dark)) with { Tag = OpticsTags.Detail });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var g = G(o);
            var list = new List<Surface>
            {
                new(SurfaceKind.ThinLens, new Vec2(g.LensX, -g.LensH), new Vec2(g.LensX, g.LensH)) { F = Math.Max(1, g.SensorX - g.LensX), Centre = new Vec2(g.LensX, 0) },
                new(SurfaceKind.Absorb, new Vec2(g.SensorX, -g.SensorH), new Vec2(g.SensorX, g.SensorH)),
                // Barrel walls and the body around the barrel opening.
                new(SurfaceKind.Absorb, new Vec2(-g.Hw, -g.Bh), new Vec2(g.BarrelEnd, -g.Bh)),
                new(SurfaceKind.Absorb, new Vec2(-g.Hw, g.Bh), new Vec2(g.BarrelEnd, g.Bh)),
                new(SurfaceKind.Absorb, new Vec2(g.BarrelEnd, -g.BodyH), new Vec2(g.BarrelEnd, -g.Bh)),
                new(SurfaceKind.Absorb, new Vec2(g.BarrelEnd, g.Bh), new Vec2(g.BarrelEnd, g.BodyH)),
                new(SurfaceKind.Absorb, new Vec2(g.BarrelEnd, -g.BodyH), new Vec2(g.Hw, -g.BodyH)),
                new(SurfaceKind.Absorb, new Vec2(g.Hw, -g.BodyH), new Vec2(g.Hw, g.BodyH)),
                new(SurfaceKind.Absorb, new Vec2(g.Hw, g.BodyH), new Vec2(g.BarrelEnd, g.BodyH)),
            };
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-o.Size.W / 2, 0), "o");
        }
    }

    /// <summary>
    /// Eye (section): eyeball, cornea, iris, crystalline lens, retina and optic nerve, looking towards −x. Light through the
    /// pupil is focused on the retina (thin lens), light outside it ends on the eyeball.
    /// </summary>
    internal sealed class EyeType : DetectorBase
    {
        public EyeType() : base(Detectors.EyeId, new I18nText("Eye", "Auge"), OpticsCategories.Detectors,
            "eye", "retina", "observer", "vision", "auge", "netzhaut", "betrachter", "sehen") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label, ValueLabels.ShowValues };
        public override Size2 DefaultSize => new(22, 20);
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, LensType.DivergingId, Sources.ObjectId };

        private static (double R, double Cx) Ball(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var r = Math.Max(0.5, Math.Min(0.95 * hh, o.Size.W / 2.4));
            return (r, -hw + 0.18 * r + r);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var hw = o.Size.W / 2;
            var (r, cx) = Ball(o);
            var c = new Vec2(cx, 0);
            var list = new List<Primitive>
            {
                new EllipsePrim(c, r, r, Ox.Filled("#ffffff")) { Tag = OpticsTags.Body },
            };
            // Cornea: arc of a smaller circle in front, between its two intersections with the eyeball.
            var cc = new Vec2(cx - 0.62 * r, 0);
            var rc = 0.55 * r;
            var d = 0.62 * r;
            var a = (r * r - rc * rc + d * d) / (2 * d);
            var hy = Math.Sqrt(Math.Max(0, r * r - a * a));
            var top = new Vec2(cx - a, -hy);
            var start = Math.Atan2(top.Y - cc.Y, top.X - cc.X);
            var sweep = -2 * (Math.PI + start);   // through the front (angle π) to the mirrored point below
            list.Add(Prim.Polyline(Geo.ArcPoints(cc, rc, rc, start, sweep, 16), Ox.Outline) with { Tag = OpticsTags.Body });
            // Iris and crystalline lens.
            var iris = Style.Visible with { StrokeWidth = 0.6 };
            var ix = cx - 0.72 * r;
            var wall = Math.Sqrt(Math.Max(0, r * r - 0.72 * 0.72 * r * r));
            list.Add(new LinePrim(new Vec2(ix, -0.3 * r), new Vec2(ix, -wall), iris) { Tag = OpticsTags.Detail });
            list.Add(new LinePrim(new Vec2(ix, 0.3 * r), new Vec2(ix, wall), iris) { Tag = OpticsTags.Detail });
            list.Add(new EllipsePrim(new Vec2(cx - 0.55 * r, 0), 0.12 * r, 0.38 * r, Ox.ThinFilled(Ox.Glass)) { Tag = OpticsTags.Detail });
            // Retina (thick arc at the back) and optic nerve.
            list.Add(Prim.Polyline(Geo.ArcPoints(c, r * 0.97, r * 0.97, -Math.PI / 3, 2 * Math.PI / 3, 16), Style.Visible with { StrokeWidth = 0.7, Stroke = "#8d6e63" }) with { Tag = OpticsTags.Detail });
            var nerve = Style.ThinLine with { StrokeWidth = 0.35 };
            foreach (var sy in new[] { -1.0, 1.0 })
            {
                var y0 = sy * 0.15 * r;
                list.Add(new LinePrim(new Vec2(cx + Math.Sqrt(r * r - y0 * y0), y0), new Vec2(hw, sy * 0.22 * r), nerve) { Tag = OpticsTags.Detail });
            }
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var (r, cx) = Ball(o);
            var lensX = cx - 0.55 * r;
            var list = new List<Surface>
            {
                new(SurfaceKind.ThinLens, new Vec2(lensX, -0.38 * r), new Vec2(lensX, 0.38 * r)) { F = cx + r - lensX, Centre = new Vec2(lensX, 0) },
            };
            // Eyeball as absorbing segments, leaving the pupil (front, |y| < 0.3 r) open.
            var pts = Geo.ArcPoints(new Vec2(cx, 0), r, r, 0, 2 * Math.PI, 36);
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var mid = Vec2.Lerp(pts[i], pts[i + 1], 0.5);
                if (mid.X < cx && Math.Abs(mid.Y) < 0.3 * r) continue;
                list.Add(new Surface(SurfaceKind.Absorb, pts[i], pts[i + 1]));
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var (r, cx) = Ball(o);
            yield return Anchor.Point(AnchorRole.Port, new Vec2(cx - 1.17 * r, 0), "o");
        }
    }

    /// <summary>
    /// Spectrometer: housing with the entry slit in its front wall (local −x side), and inside a reflection grating and a
    /// detector line with the dispersed light drawn between them (schematic, in spectral colours). For the ray tracer it is a
    /// detector: rays end on the housing.
    /// </summary>
    internal sealed class SpectrometerType : DetectorBase
    {
        public SpectrometerType() : base(Detectors.SpectrometerId, new I18nText("Spectrometer", "Spektrometer"), OpticsCategories.Detectors,
            "spectrometer", "spectrograph", "spectrum", "grating", "entrance slit", "spektrometer", "spektrograph", "spektrum", "eintrittsspalt")
        { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label, ValueLabels.ShowValues };
        public override Size2 DefaultSize => new(40, 26);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, FibreType.TypeId, Sources.LampId, GratingType.TypeId };

        private sealed record Inner(double Hw, double Hh, double Wall, double SlitHalf, Vec2 Grating, double GratingHalf, double GratingTilt, double DetY, double DetX0, double DetX1);

        private static Inner G(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var min = Math.Min(o.Size.W, o.Size.H);
            var wall = Geo.Clamp(0.05 * min, 0.3, 1.5);
            return new Inner(hw, hh, wall, Math.Max(0.15, 0.04 * o.Size.H), new Vec2(0.62 * hw, -0.3 * hh), 0.28 * hh, 20,
                0.62 * hh, -0.55 * hw, 0.35 * hw);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var g = G(o);
            var list = new List<Primitive>
            {
                Prim.Rect(-g.Hw, -g.Hh, 2 * g.Hw, 2 * g.Hh, Ox.Filled(Ox.Housing)) with { Tag = OpticsTags.Body },
            };
            // Entry slit: two dark jaws in the front wall, leaving a narrow gap on the axis.
            var jaw = Ox.ThinFilled(Ox.Dark);
            var jawH = 0.3 * g.Hh;
            list.Add(Prim.Rect(-g.Hw, -g.SlitHalf - jawH, g.Wall, jawH, jaw) with { Tag = OpticsTags.Detail });
            list.Add(Prim.Rect(-g.Hw, g.SlitHalf, g.Wall, jawH, jaw) with { Tag = OpticsTags.Detail });
            // Reflection grating: a tilted plate with grooves on its front.
            var along = RayTracer.Rotate(new Vec2(0, 1), g.GratingTilt);
            var front = RayTracer.Rotate(new Vec2(-1, 0), g.GratingTilt);
            var t = Math.Max(0.3, 0.12 * g.GratingHalf);
            var a = g.Grating - along * g.GratingHalf;
            var b = g.Grating + along * g.GratingHalf;
            list.Add(Prim.Polygon(new[] { a, b, b - front * t, a - front * t }, Ox.ThinFilled(Ox.Glass)) with { Tag = OpticsTags.Detail });
            var groove = Style.ThinLine with { StrokeWidth = 0.15 };
            for (var i = 1; i < 8; i++)
            {
                var p = Vec2.Lerp(a, b, i / 8.0);
                list.Add(new LinePrim(p, p + front * (0.6 * t), groove) { Tag = OpticsTags.Detail });
            }
            // Detector line (dark strip) near the bottom.
            var dh = Math.Max(0.3, 0.06 * g.Hh);
            list.Add(Prim.Rect(g.DetX0, g.DetY - dh / 2, g.DetX1 - g.DetX0, dh, Ox.ThinFilled(Ox.Dark)) with { Tag = OpticsTags.Detail });
            // Light inside: from the slit to the grating, and dispersed onto the detector line (red … violet).
            var inner = new Style { Role = LineRole.Thin, StrokeWidth = 0.2, Stroke = Ox.RayWhite };
            list.Add(new LinePrim(new Vec2(-g.Hw + g.Wall, 0), g.Grating, inner) { Tag = OpticsTags.Detail });
            var lines = Spectrum.Lines;
            for (var i = 0; i < lines.Length; i++)
            {
                var x = g.DetX0 + (g.DetX1 - g.DetX0) * (0.1 + 0.8 * i / (lines.Length - 1));
                list.Add(new LinePrim(g.Grating, new Vec2(x, g.DetY - dh / 2), inner with { Stroke = Spectrum.Color(lines[i]) }) { Tag = OpticsTags.Detail });
            }
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            return Detectors.Box(-hw, -hh, hw, hh).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-o.Size.W / 2, 0), "o");
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Housing", "Gehäuse"), "shape");
            yield return new PartInfo(OpticsTags.Detail, new I18nText("Slit, grating and detector line", "Spalt, Gitter und Detektorzeile"), "shape");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }
    }

    /// <summary>Screen (projection screen, observation plane): a plate that ends every ray reaching it.</summary>
    internal sealed class ScreenType : DetectorBase
    {
        public ScreenType() : base(Detectors.ScreenId, new I18nText("Screen", "Schirm"), OpticsCategories.Detectors,
            "screen", "projection", "image plane", "observation", "schirm", "projektionsschirm", "bildebene", "mattscheibe") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label, ValueLabels.ShowValues };
        public override Size2 DefaultSize => new(3, 40);
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, Sources.ObjectId, Sources.LampId };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, Ox.Filled("#eeeeee")) with { Tag = OpticsTags.Body } };
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            return Detectors.Box(-hw, -hh, hw, hh).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, new Vec2(-o.Size.W / 2, 0), "o");
        }
    }
}
