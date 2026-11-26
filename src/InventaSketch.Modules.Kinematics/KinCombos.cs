using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Shaft links of the crank-driven mechanisms (four-bar, slider-crank, scotch yoke, six-bar), read from the core shaft
    /// lines (<see cref="ShaftLines"/>): a mechanism whose crank pivot hub ("hub") is concentric with a gear, pulley or
    /// motor hub, or sits on a shaft axis, turns with it, so the input speed <c>n</c> is shared across modules (a motor from
    /// the machines module drives the crank, a gear from the mechanical module sits on the crank shaft). Mechanisms have no
    /// torque variable, so no torque is passed.
    /// </summary>
    internal static class KinShaft
    {
        private static readonly Relation SameSpeed = Relation.Equality("kin.shaft.speed", new I18nText("Same speed on one shaft", "Gleiche Drehzahl auf einer Welle"));

        /// <summary>The shaft relations <paramref name="o"/> takes part in (empty without a registry or shaft line).</summary>
        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx, Registry? owner)
        {
            var registry = ctx.Registry ?? owner;
            return registry is null ? Array.Empty<BoundRelation>() : ShaftLines.Of(registry, ctx.Doc).Links(o.Id, SameSpeed, null);
        }
    }

    /// <summary>
    /// Instant centre (pole) of the coupler of a four-bar or of the connecting rod of a slider-crank relative to the frame,
    /// drawn at the current position (Kennedy's theorem [S24]): the coupler A–B turns for an instant about the intersection of
    /// the crank line O2–A and the rocker line O4–B (slider-crank: the crank line and the normal to the guide through B).
    /// Construction lines are thin dashed lines clipped to the box; the pole is marked only when it lies inside the box (it
    /// runs to infinity where the two lines become parallel).
    /// </summary>
    internal static class KinPole
    {
        public const string Tag = "pole";

        public static readonly ParamDef Param = KP.Bool("pole", "Show instant centre of the coupler", "Momentanpol der Koppel zeigen", false);

        public static bool Shown(SketchObject o) => o.Flag("pole", false);

        public static ContextAction Action(SketchObject o)
            => KA.Toggle(o, "kin.pole", new I18nText("Instant centre", "Momentanpol"), IconIds.Toggle, "pole", false, "annotation");

        /// <summary>
        /// Draws the pole <paramref name="pole"/> (model coordinates, null at infinity) with its construction lines along
        /// <paramref name="line1"/> and <paramref name="line2"/> (each from a fixed point through a moving joint).
        /// </summary>
        public static void Draw(Scene s, Vec2? pole, (Vec2 From, Vec2 Through) line1, (Vec2 From, Vec2 Through) line2, Lang lang)
        {
            if (pole is not Vec2 i) return;
            var box = new Rect(-s.W / 2, -s.H / 2, s.W, s.H);
            foreach (var (from, through) in new[] { line1, line2 })
            {
                var a = s.L(from); var b = s.L(through); var p = s.L(i);
                var d = b - a;
                if (d.Length < 1e-9) continue;
                // The segment spans the fixed point, the joint and the pole (in whatever order they lie on the line).
                double T(Vec2 q) => (q - a).Dot(d) / d.Dot(d);
                var ts = new[] { 0.0, 1.0, T(p) };
                var lo = Math.Min(ts[0], Math.Min(ts[1], ts[2]));
                var hi = Math.Max(ts[0], Math.Max(ts[1], ts[2]));
                if (Clip(a + d * lo, a + d * hi, box) is { } seg) s.Add(Prim.Line(seg.A, seg.B, KS.Trace), Tag);
            }
            var c = s.L(i);
            if (!box.Inflate(-0.6 * s.R).Contains(c)) return;
            s.Add(Prim.Circle(c.X, c.Y, 0.55 * s.R, KS.Joint with { StrokeWidth = 0.25 }), Tag);
            s.Add(Prim.Circle(c.X, c.Y, 0.2 * s.R, KS.Black), Tag);
            var text = lang == Lang.De ? "P₁₃" : "I₁₃";
            var size = s.TextSize * 0.85;
            // Up and to the right of the marker, or on the other side where the box edge is near (half width and height
            // estimated generously, so the label stays inside the box).
            double hw = 0.4 * size * text.Length + 0.1, hh = 0.75 * size;
            var x = c.X + 0.9 * s.R + hw;
            if (x + hw > s.W / 2) x = c.X - 0.9 * s.R - hw;
            var y = c.Y - 0.9 * s.R - hh;
            if (y - hh < -s.H / 2) y = c.Y + 0.9 * s.R + hh;
            var at = new Vec2(Geo.Clamp(x, -s.W / 2 + hw, s.W / 2 - hw), Geo.Clamp(y, -s.H / 2 + hh, s.H / 2 - hh));
            s.Add(Frag.FitText(at, text, size, s.W / 2, s.H / 2, Tag, italic: true));
        }

        /// <summary>The part of the segment a–b inside <paramref name="box"/> (Liang–Barsky), or null.</summary>
        internal static (Vec2 A, Vec2 B)? Clip(Vec2 a, Vec2 b, Rect box)
        {
            double t0 = 0, t1 = 1;
            var d = b - a;
            bool Edge(double p, double q)
            {
                if (Math.Abs(p) < 1e-15) return q >= 0;
                var r = q / p;
                if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
                return true;
            }
            if (!Edge(-d.X, a.X - box.X) || !Edge(d.X, box.X + box.W - a.X) || !Edge(-d.Y, a.Y - box.Y) || !Edge(d.Y, box.Y + box.H - a.Y)) return null;
            if (t1 - t0 < 1e-12) return null;
            return (a + d * t0, a + d * t1);
        }
    }
}
