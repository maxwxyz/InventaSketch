using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Belt and chain drives: pulleys, sprockets, belts and chains.</summary>
    internal static class Drives
    {
        public const string Cat = "mech.drives";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSide = T("Side view", "Seitenansicht");
        private static readonly I18nText VSection = T("Section", "Schnitt");
        private static readonly I18nText VAxial = T("Axial view", "Stirnansicht");

        private const double Tip = 14, PitchR = 12.6;

        private static Anchor Pitch(Pen p, double r) => Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, r * p.K);

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType("mech.pulley", T("Belt pulley", "Riemenscheibe"), Cat,
                new[] { "pulley", "sheave", "v-belt pulley", "riemenscheibe", "keilriemenscheibe", "scheibe", "riementrieb" },
                new ViewSpec("stick", VStick, 8, 30, PulleyStick),
                new ViewSpec("axial", VAxial, 30, 30, PulleyAxial) { Anchors = (p, o) => new[] { Pitch(p, PitchR) } },
                new ViewSpec("side", VSide, 10, 30, (p, o) => PulleySide(p, false)),
                new ViewSpec("section", VSection, 10, 30, (p, o) => PulleySide(p, true)))
            {
                Params = new[] { Common.Hub },
                Extra = o => new[] { Common.HubCycle(o) },
            };
            yield return new MechType("mech.belt", T("Belt", "Riemen"), Cat,
                new[] { "belt", "v-belt", "timing belt", "belt drive", "riemen", "keilriemen", "zahnriemen", "riementrieb" },
                new ViewSpec("open", T("Open belt", "Offener Riemen"), 60, 24, (p, o) => Belt(p, o, false)) { Fluid = true, Anchors = EndHubs },
                new ViewSpec("crossed", T("Crossed belt", "Gekreuzter Riemen"), 60, 24, (p, o) => Belt(p, o, true)) { Fluid = true, Anchors = EndHubs },
                new ViewSpec("loop", T("Closed loop", "Geschlossene Schleife"), 60, 24, BeltLoop) { Fluid = true, Anchors = LoopHubs })
            {
                Params = new[] { new ParamDef("ratio", T("Diameter ratio right/left", "Durchmesserverhältnis rechts/links"), ParamKind.Number) { Min = 0.1, Max = 10, Step = 0.1 } },
                Rotating = false,
            };
            yield return new MechType("mech.sprocket", T("Chain sprocket", "Kettenrad"), Cat,
                new[] { "sprocket", "chain wheel", "kettenrad", "ritzel", "kettentrieb" },
                new ViewSpec("stick", VStick, 8, 30, SprocketStick),
                new ViewSpec("axial", VAxial, 30, 30, SprocketAxial) { Anchors = (p, o) => new[] { Pitch(p, PitchR) } },
                new ViewSpec("teeth", T("With teeth", "Mit Zähnen"), 30, 30, SprocketTeeth) { Anchors = (p, o) => new[] { Pitch(p, PitchR) } },
                new ViewSpec("side", VSide, 6, 30, SprocketSide))
            {
                Params = new[] { Common.Hub, Common.Teeth(18) },
                Extra = o => new[] { Common.HubCycle(o) },
            };
            yield return new MechType("mech.chain", T("Roller chain", "Rollenkette"), Cat,
                new[] { "chain", "roller chain", "chain drive", "kette", "rollenkette", "kettentrieb" },
                new ViewSpec("links", T("Links", "Kettenglieder"), 60, 24, Chain) { Fluid = true, Anchors = EndHubs },
                new ViewSpec("line", T("Pitch line", "Teilungslinie"), 60, 24, ChainLine) { Fluid = true, Anchors = EndHubs })
            {
                Rotating = false,
            };
        }

        // ------------------------------------------------------------------ pulley

        private static void PulleyStick(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
                p.Poly(S.Stick, false, -4, s * 15, -1.6, s * 15, 0, s * 13, 1.6, s * 15, 4, s * 15);
            Frag.Hub(p, p.V(0, -13), p.V(0, 0), p.V(0, 13), o.Str("hub", "fixed"), S.Stick);
        }

        private static void PulleyAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, Tip, S.Vis, "rim");
            p.Circle(0, 0, PitchR, S.Ctr, "pitch");
            p.Circle(0, 0, 11, S.Thin, "groove");
            p.Circle(0, 0, 5.5, S.Vis, "hub");
            p.Circle(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        private static void PulleySide(Pen p, bool section)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (section)
                    p.Poly(p.Hatch(), true, -4, s * 15, -1.6, s * 15, 0, s * 11.5, 1.6, s * 15, 4, s * 15, 4, s * 3, -4, s * 3);
                p.Line(-5, s * PitchR, 5, s * PitchR, S.Ctr, "pitch");
            }
            if (!section)
            {
                p.Poly(S.Vis, true, -4, -15, -1.6, -15, 0, -11.5, 1.6, -15, 4, -15, 4, 15, 1.6, 15, 0, 11.5, -1.6, 15, -4, 15);
                p.Line(-4, -3, 4, -3, S.Hid, "bore");
                p.Line(-4, 3, 4, 3, S.Hid, "bore");
            }
            p.Axis(5);
        }

        // ------------------------------------------------------------------ belt

        private static (double L, double R) Radii(Pen p, SketchObject o)
        {
            var hh = p.H / 2;
            var ratio = o.Num("ratio", 1);
            if (ratio <= 0) ratio = 1;
            return ratio <= 1 ? (hh, hh * ratio) : (hh / ratio, hh);
        }

        /// <summary>Loop radii, reduced if the box is narrower than both pulleys.</summary>
        private static (double L, double R) LoopRadii(Pen p, SketchObject o)
        {
            var (rl, rr) = Radii(p, o);
            var k = Math.Min(1, p.W / (rl + rr));
            return (rl * k, rr * k);
        }

        private static IEnumerable<Anchor> EndHubs(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(-p.W / 2, 0), "left");
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(p.W / 2, 0), "right");
        }

        private static IEnumerable<Anchor> LoopHubs(Pen p, SketchObject o)
        {
            var (rl, rr) = LoopRadii(p, o);
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(-p.W / 2 + rl, 0), "left");
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(p.W / 2 - rr, 0), "right");
        }

        /// <summary>Belt strands between two pulleys whose centres are at the left and right box edges.</summary>
        private static void Belt(Pen p, SketchObject o, bool crossed)
        {
            var hw = p.W / 2;
            var (rl, rr) = Radii(p, o);
            p.LineP(new Vec2(-hw, -rl), new Vec2(hw, crossed ? rr : -rr), S.Vis, "strand");
            p.LineP(new Vec2(-hw, rl), new Vec2(hw, crossed ? -rr : rr), S.Vis, "strand");
        }

        private static void BeltLoop(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            var (rl, rr) = LoopRadii(p, o);
            double cl = -hw + rl, cr = hw - rr;
            var pts = new List<Vec2> { new(cl, -rl) };
            pts.AddRange(Geo.ArcPoints(new Vec2(cr, 0), rr, rr, -Math.PI / 2, Math.PI, 24));
            pts.AddRange(Geo.ArcPoints(new Vec2(cl, 0), rl, rl, Math.PI / 2, Math.PI, 24));
            p.PolyP(pts, S.Vis, true, "belt");
        }

        // ------------------------------------------------------------------ sprocket and chain

        private static void SprocketStick(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Line(-4, s * 15, 4, s * 15, S.Stick, "tip");
                p.Round(p.V(0, s * 13.2), 1.0, S.Vis, "roller");
            }
            Frag.Hub(p, p.V(0, -15), p.V(0, 0), p.V(0, 15), o.Str("hub", "fixed"), S.Stick);
        }

        private static void SprocketAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, Tip, S.Vis, "tip");
            p.Circle(0, 0, PitchR, S.Ctr, "pitch");
            p.Circle(0, 0, 11.2, S.Thin, "root");
            p.Circle(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        private static void SprocketTeeth(Pen p, SketchObject o)
        {
            var z = GearMath.Teeth(o, 18);
            p.PolyD(GearMath.Sprocket(z, PitchR, Tip, -Math.PI / 2), S.Vis, true, "teeth");
            p.Circle(0, 0, PitchR, S.Ctr, "pitch");
            p.Circle(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        private static void SprocketSide(Pen p, SketchObject o)
        {
            p.Poly(S.Vis, true, -1.6, -15, 1.6, -15, 2.4, -13, 2.4, 13, 1.6, 15, -1.6, 15, -2.4, 13, -2.4, -13);
            p.Line(-3, -PitchR, 3, -PitchR, S.Ctr, "pitch");
            p.Line(-3, PitchR, 3, PitchR, S.Ctr, "pitch");
            p.Line(-2.4, -3, 2.4, -3, S.Hid, "bore");
            p.Line(-2.4, 3, 2.4, 3, S.Hid, "bore");
            p.Axis(3);
        }

        /// <summary>Chain strands with link plates and rollers (actual mm, pitch from strand height).</summary>
        private static void Chain(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var a = Math.Min(1.6, p.H * 0.12);
            var pitch = Math.Max(2.5, a * 3.2);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var y = s * (hh - a);
                p.LineP(new Vec2(-hw, y - a), new Vec2(hw, y - a), S.Vis, "plate");
                p.LineP(new Vec2(-hw, y + a), new Vec2(hw, y + a), S.Vis, "plate");
                var n = Math.Max(1, (int)Math.Floor(p.W / pitch));
                var x0 = -(n - 1) * pitch / 2;
                for (var i = 0; i < n; i++) p.Add(Prim.Circle(x0 + i * pitch, y, a * 0.6, S.Vis), "roller");
            }
        }

        private static void ChainLine(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            p.LineP(new Vec2(-hw, -hh), new Vec2(hw, -hh), S.Ctr, "strand");
            p.LineP(new Vec2(-hw, hh), new Vec2(hw, hh), S.Ctr, "strand");
        }
    }
}
