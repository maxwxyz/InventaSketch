using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Inline air duct parts: damper, fire damper and silencer. They sit in a duct run (ports <c>inlet</c>/<c>outlet</c> on
    /// the duct axis, fitting every air kind) and take the colour of their optional air kind in plan and section.
    /// </summary>
    internal static class DuctFittings
    {
        private const string Cat = BsCategories.Ventilation;

        private static readonly ParamDef AirKind = Media.AirParam("supply");

        private static readonly string[] Duct = { DuctType.TypeId, "bs.vent.terminal", "bs.vent.unit" };

        private static IEnumerable<ContextAction> KindChoice(SketchObject o) => Bs.Choice(o, "bs.ductpart.kind", AirKind);

        private static Func<Canvas, SketchObject, string, IEnumerable<PortAt>> Axis => BsBoxType.Same((-0.5, 0, "inlet"), (0.5, 0, "outlet"));

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.vent.damper", new I18nText("Duct damper", "Lüftungsklappe"), Cat,
                new Size2(4, 2.4), new Size2(4, 2.4), new Size2(4, 3), DamperPlan, DamperSection, DamperSchematic,
                "damper", "volume control damper", "shut-off damper", "air damper", "klappe", "luftklappe", "drosselklappe",
                "absperrklappe", "lüftung", "kwl", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, DefaultHeight = 2400,
                Params = new[] { AirKind }, Extra = KindChoice, Ports = Axis, CompanionIds = Duct,
            };

            yield return new BsBoxType("bs.vent.firedamper", new I18nText("Fire damper", "Brandschutzklappe"), Cat,
                new Size2(4, 2.4), new Size2(4, 2.4), new Size2(4, 4), FirePlan, FireSection, FireSchematic,
                "fire damper", "fire protection damper", "smoke damper", "brandschutzklappe", "bsk", "brandschutz", "lüftung", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, DefaultHeight = 2400,
                Params = new[] { AirKind }, Extra = KindChoice, Ports = Axis, CompanionIds = Duct,
            };

            yield return new BsBoxType("bs.vent.silencer", new I18nText("Duct silencer (sound attenuator)", "Schalldämpfer (Lüftung)"), Cat,
                new Size2(6, 2.4), new Size2(6, 2.4), new Size2(6, 3), SilencerPlan, SilencerSection, SilencerSchematic,
                "silencer", "sound attenuator", "attenuator", "duct silencer", "schalldämpfer", "rohrschalldämpfer",
                "kulissenschalldämpfer", "lüftung", "kwl", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, DefaultHeight = 2400,
                Params = new[] { AirKind }, Extra = KindChoice, Ports = Axis, CompanionIds = Duct,
            };
        }

        /// <summary>Duct part edges in the colour of the air kind (plan and section).</summary>
        private static Style AirLine(SketchObject o) => new() { Stroke = Media.AirOf(o.Str("kind", "supply")).Color, StrokeWidth = 0.25, Role = LineRole.Visible };

        private static string FireLabel(Lang lang) => lang == Lang.De ? "BSK" : "FD";

        /// <summary>Duct piece between the end stubs: box from -<paramref name="u"/> to <paramref name="u"/> (normalised), half height <paramref name="v"/>.</summary>
        private static void Piece(Canvas c, double u, double v, Style edge, Style stub)
        {
            c.RectN(-u, -v, u, v, edge, BsTags.Outline);
            c.LineN(-0.5, 0, -u, 0, stub, BsTags.Stub);
            c.LineN(u, 0, 0.5, 0, stub, BsTags.Stub);
        }

        private static void Blade(Canvas c, double u, double v, Style s)
        {
            c.LineN(-u, v, u, -v, s, "blade");
            c.Circle(Vec2.Zero, 0.05 * c.K, St.Black, "pivot");
        }

        // ------------------------------------------------------------------ damper

        private static void DamperPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var air = AirLine(o);
            Piece(c, 0.4, 0.3, air, air);
            Blade(c, 0.22, 0.26, St.Thin);
        }

        private static void DamperSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var air = AirLine(o);
            Piece(c, 0.4, 0.3, air, air);
            Blade(c, 0.22, 0.26, St.Thin);
            // Hand quadrant on top of the duct.
            c.RectN(-0.08, -0.48, 0.08, -0.3, St.ThinWhite, "handle");
        }

        private static void DamperSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.5, 0, -0.35, 0, St.Medium, BsTags.Stub);
            c.LineN(0.35, 0, 0.5, 0, St.Medium, BsTags.Stub);
            c.RectN(-0.35, -0.3, 0.35, 0.3, St.MediumWhite, BsTags.Outline);
            Blade(c, 0.22, 0.26, St.Medium);
        }

        // ------------------------------------------------------------------ fire damper

        private static void FirePlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var air = AirLine(o);
            Piece(c, 0.4, 0.3, air, air);
            Blade(c, 0.22, 0.26, St.Thin);
            // Thermal release (fusible link) marker.
            c.RectN(-0.36, -0.27, -0.24, -0.1, St.Black, "release");
        }

        private static void FireSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var air = AirLine(o);
            Piece(c, 0.4, 0.3, air, air);
            Blade(c, 0.22, 0.26, St.Thin);
            c.RectN(-0.22, -0.5, 0.22, -0.3, St.ThinWhite, "actuator");
            c.Text(c.N(0, -0.4), FireLabel(ctx.Lang), 0.8, 0.38 * c.W, 0.16 * c.H);
        }

        private static void FireSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.5, 0, -0.35, 0, St.Medium, BsTags.Stub);
            c.LineN(0.35, 0, 0.5, 0, St.Medium, BsTags.Stub);
            c.RectN(-0.35, -0.2, 0.35, 0.2, St.MediumWhite, BsTags.Outline);
            Blade(c, 0.2, 0.17, St.Medium);
            c.Text(c.N(0, -0.36), FireLabel(ctx.Lang), 1.2, 0.7 * c.W, 0.2 * c.H);
        }

        // ------------------------------------------------------------------ silencer

        private static void SilencerPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var air = AirLine(o);
            Piece(c, 0.42, 0.38, air, air);
            // Absorber lining along both walls, hatched.
            double x0 = -0.42 * c.W, x1 = 0.42 * c.W, yo = 0.38 * c.H, yi = 0.18 * c.H;
            c.Line(new Vec2(x0, -yi), new Vec2(x1, -yi), St.Fine, "lining");
            c.Line(new Vec2(x0, yi), new Vec2(x1, yi), St.Fine, "lining");
            var n = Math.Max(3, (int)Math.Round((x1 - x0) / 0.8));
            var p = (x1 - x0) / n;
            var d = 0.3 * p;
            for (var i = 0; i < n; i++)
            {
                var x = x0 + (i + 0.5) * p;
                c.Line(new Vec2(x - d, -yo), new Vec2(x + d, -yi), St.Fine, "absorber");
                c.Line(new Vec2(x - d, yi), new Vec2(x + d, yo), St.Fine, "absorber");
            }
        }

        private static void SilencerSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var air = AirLine(o);
            Piece(c, 0.42, 0.38, air, air);
            // Splitters inside (concealed) and the connection flanges.
            foreach (var v in new[] { -0.12, 0.12 }) c.LineN(-0.42, v, 0.42, v, St.Hidden, "splitter");
            foreach (var u in new[] { -0.42, 0.42 }) c.LineN(u, -0.46, u, 0.46, St.Thin, "flange");
        }

        private static void SilencerSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.5, 0, -0.38, 0, St.Medium, BsTags.Stub);
            c.LineN(0.38, 0, 0.5, 0, St.Medium, BsTags.Stub);
            c.RectN(-0.38, -0.3, 0.38, 0.3, St.MediumWhite, BsTags.Outline);
            c.Zigzag(c.N(-0.3, 0), c.N(0.3, 0), 3, 0.15 * c.H, St.Thin, "absorber");
        }
    }
}
