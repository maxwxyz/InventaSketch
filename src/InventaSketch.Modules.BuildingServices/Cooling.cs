using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Cooling: split air conditioner as one element with the wall-mounted indoor unit and the outdoor unit joined by the
    /// refrigerant lines. External connections: the condensate drain of the indoor unit (<c>waste</c>) and the power supply
    /// of the outdoor unit (<c>cable</c>). Refrigerant lines stay inside the element (there is no refrigerant pipe medium).
    /// </summary>
    internal static class Cooling
    {
        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.cool.split", new I18nText("Split air conditioner", "Split-Klimagerät"), BsCategories.Cooling,
                new Size2(18, 8), new Size2(18, 22), new Size2(18, 8), SplitPlan, SplitSection, SplitSchematic,
                "split air conditioner", "air conditioning", "ac", "split unit", "indoor unit", "outdoor unit", "cooling",
                "klimagerät", "split-klimagerät", "klimaanlage", "klima", "inneneinheit", "außeneinheit", "kühlung", "tga")
            {
                Mount = Mounting.Floor, WallBacked = false, DefaultHeight = 0,
                Ports = BsBoxType.Uv(
                    new[] { (-0.4, 0.5, "waste"), (0.5, -0.3, "cable") },
                    new[] { (-0.4, -0.38, "waste"), (0.5, 0.33, "cable") },
                    new[] { (-0.29, 0.5, "waste"), (0.5, 0.0, "cable") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.drain" },
            };
        }

        private static Style Refrigerant => St.Thin with { Dash = DashStyle.Dash };

        /// <summary>Fan of the outdoor unit: circle with two blades.</summary>
        private static void Fan(Canvas c, Vec2 centre, double r, Style s)
        {
            c.Circle(centre, r, s, "fan");
            c.Line(centre + new Vec2(-r * 0.7, -r * 0.7), centre + new Vec2(r * 0.7, r * 0.7), St.Fine, "fan");
            c.Line(centre + new Vec2(-r * 0.7, r * 0.7), centre + new Vec2(r * 0.7, -r * 0.7), St.Fine, "fan");
        }

        /// <summary>Snowflake (three crossing strokes) as the cooling mark of the indoor unit.</summary>
        private static void Snowflake(Canvas c, Vec2 centre, double r, Style s)
        {
            for (var i = 0; i < 3; i++)
            {
                var a = Geo.DegToRad(90 + i * 60);
                c.Line(centre - Vec2.Polar(r, a), centre + Vec2.Polar(r, a), s, "snowflake");
            }
        }

        private static void SplitPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Indoor unit on the room side (bottom), outdoor unit outside (top right); the wall runs between them.
            c.RectN(-0.48, 0.2, -0.02, 0.48, St.Thin, BsTags.Outline);
            c.LineN(-0.45, 0.42, -0.05, 0.42, St.Fine, "louvre");
            c.RectN(0.1, -0.48, 0.48, -0.05, St.Thin, "outdoor");
            var r = 0.8 * Math.Min(0.19 * c.W, 0.215 * c.H);
            Fan(c, c.N(0.29, -0.265), r, St.Fine);
            c.PolyN(Refrigerant, false, "refrigerant", -0.02, 0.28, 0.2, 0.28, 0.2, -0.05);
            c.PolyN(Refrigerant, false, "refrigerant", -0.02, 0.36, 0.26, 0.36, 0.26, -0.05);
            c.LineN(-0.4, 0.48, -0.4, 0.5, St.Thin, BsTags.Stub);
            c.LineN(0.48, -0.3, 0.5, -0.3, St.Thin, BsTags.Stub);
        }

        private static void SplitSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Indoor unit high on the wall, outdoor unit standing on the ground, refrigerant lines between.
            c.RoundRect(-0.48 * c.W, -c.Hh, -0.02 * c.W, -0.38 * c.H, 0.02 * c.K, St.Thin, BsTags.Outline);
            c.LineN(-0.45, -0.41, -0.05, -0.41, St.Fine, "louvre");
            c.RectN(0.1, 0.2, 0.48, 0.46, St.Thin, "outdoor");
            foreach (var u in new[] { 0.14, 0.44 }) c.LineN(u, 0.46, u, 0.5, St.Thin, "foot");
            var r = 0.8 * Math.Min(0.19 * c.W, 0.13 * c.H);
            Fan(c, c.N(0.29, 0.33), r, St.Fine);
            c.PolyN(Refrigerant, false, "refrigerant", -0.02, -0.46, 0.04, -0.46, 0.04, 0.27, 0.1, 0.27);
            c.PolyN(Refrigerant, false, "refrigerant", -0.02, -0.42, 0.07, -0.42, 0.07, 0.36, 0.1, 0.36);
            c.LineN(0.48, 0.33, 0.5, 0.33, St.Thin, BsTags.Stub);
        }

        private static void SplitSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RectN(-0.48, -0.3, -0.1, 0.3, St.Medium, BsTags.Outline);
            Snowflake(c, c.N(-0.29, 0), 0.8 * Math.Min(0.19 * c.W, 0.3 * c.H), St.Thin);
            c.RectN(0.1, -0.3, 0.48, 0.3, St.Medium, "outdoor");
            Fan(c, c.N(0.29, 0), 0.8 * Math.Min(0.19 * c.W, 0.3 * c.H), St.Thin);
            c.LineN(-0.1, -0.12, 0.1, -0.12, St.Thin, "refrigerant");
            c.LineN(-0.1, 0.12, 0.1, 0.12, St.Thin, "refrigerant");
            c.LineN(-0.29, 0.3, -0.29, 0.5, St.Medium, BsTags.Stub);
            c.LineN(0.48, 0, 0.5, 0, St.Medium, BsTags.Stub);
        }
    }
}
