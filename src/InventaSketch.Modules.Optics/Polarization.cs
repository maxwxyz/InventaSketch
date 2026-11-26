using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>Shared drawing of the polarisation elements: a thin plate and its axis as a line across the element.</summary>
    internal static class PolarizationPlate
    {
        /// <summary>The axis line at <paramref name="deg"/> through the centre: half length within the box (at most 55 % of the half height).</summary>
        public static (Vec2 Dir, double Half) Axis(SketchObject o, double deg, double topReserve = 0)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var dir = Grips.LineDirection(deg);
            var half = Grips.HalfLength(dir, hw - 0.4, hh - 0.4 - topReserve);
            return (dir, Math.Min(half, 0.55 * hh));
        }

        /// <summary>
        /// The plate (thin rectangle over the height below <paramref name="topReserve"/>, which is left free for a text) and
        /// the double-arrow axis line.
        /// </summary>
        public static List<Primitive> Draw(SketchObject o, double deg, string fill, bool dashed, double topReserve = 0)
        {
            double w = o.Size.W, h = o.Size.H;
            var pw = Math.Min(w, Math.Max(1.0, 0.35 * w));
            var list = new List<Primitive> { Prim.Rect(-pw / 2, -h / 2 + topReserve, pw, h - topReserve, Ox.Filled(fill)) with { Tag = OpticsTags.Body } };
            var (dir, half) = Axis(o, deg, topReserve);
            if (half > 0.2)
            {
                var style = new Style { Role = LineRole.Thin, StrokeWidth = 0.3, Stroke = Ox.Black, ArrowStart = ArrowHead.Triangle, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
                if (dashed) style = style with { Dash = DashStyle.Dash };
                list.Add(new LinePrim(dir * -half, dir * half, style) { Tag = OpticsTags.Detail });
            }
            return list;
        }

        public static DragHandle Grip(SketchObject o, double deg, I18nText label, double topReserve = 0)
        {
            var (dir, half) = Axis(o, deg, topReserve);
            return Grips.Page(o, Grips.AxisAngle, dir * Math.Max(half, 1), DragConstraint.Free, label, OpticsTags.Detail);
        }

        public static SketchObject Move(SketchObject o, string key, Vec2 pagePos)
            => key == Grips.AxisAngle && Grips.LineDegrees(Grips.ToLocal(o, pagePos)) is double deg ? o.WithParam("angle", deg) : o;
    }

    /// <summary>
    /// Linear polariser: a thin plate with its transmission axis drawn as a double arrow across it (0° = along the element in
    /// the drawing plane, 90° = perpendicular to the drawing). Unpolarised light leaves with half its intensity, polarised
    /// light with the share cos² of the angle between its polarisation and the axis (Malus's law); crossed polarisers block.
    /// </summary>
    internal sealed class PolarizerType : OpticsType
    {
        public const string TypeId = "optics.polarizer";

        public PolarizerType() : base(TypeId, new I18nText("Linear polarizer", "Linearpolarisator"), OpticsCategories.Components,
            "polarizer", "polariser", "linear polarizer", "polarization", "analyzer", "polarisator", "polfilter", "polarisation", "analysator") { }

        public static readonly ParamDef AngleDef = Pm.Num("angle", "Transmission axis (0° = in the drawing plane)", "Durchlassrichtung (0° = in der Zeichenebene)", 0, -90, 90, "°", 1);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { AngleDef, Pm.Label };
        public override Size2 DefaultSize => new(8, 34);
        public override IReadOnlyList<string> Companions => new[] { WaveplateType.TypeId, TypeId, Sources.LaserId, Detectors.ScreenId, Detectors.PhotodiodeId };

        private static double Angle(SketchObject o) => Geo.Clamp(o.Num("angle", 0), -90, 90);

        private static I18nText AngleLabel(double deg) => Grips.Label("Transmission axis", "Durchlassrichtung", deg, "°");

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var list = PolarizationPlate.Draw(o, Angle(o), "#cfd8dc", dashed: false);
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hh = o.Size.H / 2;
            return new[] { new Surface(SurfaceKind.Polarizer, new Vec2(0, -hh), new Vec2(0, hh)) { Angle = Angle(o) } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        /// <summary>The transmission axis as a grip at the end of its line, dragged around the centre (whole degrees).</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            yield return PolarizationPlate.Grip(o, Angle(o), AngleLabel(Angle(o)));
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx) => PolarizationPlate.Move(o, key, pagePos);

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Plate", "Platte"), "shape");
            yield return new PartInfo(OpticsTags.Detail, new I18nText("Transmission axis", "Durchlassrichtung"), "line");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.NumberInput(o, "optics.polarizer.angle", new I18nText("Transmission axis", "Durchlassrichtung"), AngleDef, "polarizer");
        }
    }

    /// <summary>
    /// Wave plate (retarder), quarter-wave or half-wave: a glass plate with its fast axis drawn as a dashed double arrow and
    /// "λ/4" or "λ/2" written on it. It retards the component along the slow axis by π/2 or π (Jones calculus): a half-wave
    /// plate turns linear polarisation, a quarter-wave plate at 45° makes it circular. Unpolarised light passes unchanged.
    /// </summary>
    internal sealed class WaveplateType : OpticsType
    {
        public const string TypeId = "optics.waveplate";

        public WaveplateType() : base(TypeId, new I18nText("Wave plate", "Verzögerungsplatte"), OpticsCategories.Components,
            "wave plate", "waveplate", "retarder", "quarter-wave", "half-wave", "lambda/4", "verzögerungsplatte", "wellenplatte", "lambda-viertel", "lambda-halbe")
        { }

        public static readonly ParamDef Retardation = Pm.Choice("retardance", "Retardance", "Verzögerung", "quarter",
            ("quarter", "λ/4 (quarter-wave)", "λ/4 (Viertelwellenplatte)"), ("half", "λ/2 (half-wave)", "λ/2 (Halbwellenplatte)"));

        public static readonly ParamDef AngleDef = Pm.Num("angle", "Fast axis (0° = in the drawing plane)", "Schnelle Achse (0° = in der Zeichenebene)", 45, -90, 90, "°", 1);

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Retardation, AngleDef, Pm.Label };
        public override Size2 DefaultSize => new(8, 34);
        public override IReadOnlyList<string> Companions => new[] { PolarizerType.TypeId, Sources.LaserId, Detectors.ScreenId };

        private static double Angle(SketchObject o) => Geo.Clamp(o.Num("angle", 45), -90, 90);
        private static bool Half(SketchObject o) => o.Str("retardance", "quarter") == "half";

        /// <summary>Text size of "λ/4" (fits the box) and the room it takes at the top.</summary>
        private static (double Size, double Reserve) Text(SketchObject o)
        {
            var size = Math.Max(0.5, Math.Min(2.5, Math.Min(0.9 * o.Size.W / (3 * 0.55), 0.15 * o.Size.H)));
            return (size, size + 0.6);
        }

        private static I18nText AngleLabel(double deg) => Grips.Label("Fast axis", "Schnelle Achse", deg, "°");

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var (size, reserve) = Text(o);
            var list = PolarizationPlate.Draw(o, Angle(o), Ox.Glass, dashed: true, reserve);
            var y = -o.Size.H / 2 + 0.3 + size / 2;
            list.Add(new TextPrim(new Vec2(0, y), Half(o) ? "λ/2" : "λ/4", new TextStyle { Size = size, Anchor = TextAnchor.Middle, Baseline = TextBaseline.Middle }) { Tag = OpticsTags.Detail });
            list.AddRange(Ox.Label(o));
            return list;
        }

        internal override IReadOnlyList<Surface> Surfaces(SketchObject o)
        {
            var hh = o.Size.H / 2;
            return new[]
            {
                new Surface(SurfaceKind.Retarder, new Vec2(0, -hh + Text(o).Reserve), new Vec2(0, hh)) { Angle = Angle(o), Retardance = Half(o) ? Math.PI : Math.PI / 2 },
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "o");
        }

        /// <summary>The fast axis as a grip at the end of its line, dragged around the centre (whole degrees).</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            yield return PolarizationPlate.Grip(o, Angle(o), AngleLabel(Angle(o)), Text(o).Reserve);
        }

        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx) => PolarizationPlate.Move(o, key, pagePos);

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Body, new I18nText("Plate", "Platte"), "shape");
            yield return new PartInfo(OpticsTags.Detail, new I18nText("Fast axis and retardance", "Schnelle Achse und Verzögerung"), "line");
            if (!string.IsNullOrWhiteSpace(o.Str("label", ""))) yield return new PartInfo(OpticsTags.Label, new I18nText("Label", "Beschriftung"), "text");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            foreach (var opt in Ox.Choice(o, "optics.waveplate.retardance", new I18nText("Retardance", "Verzögerung"), Retardation, "waveplate")) yield return opt;
            yield return Ox.NumberInput(o, "optics.waveplate.angle", new I18nText("Fast axis", "Schnelle Achse"), AngleDef, "waveplate");
        }
    }
}
