using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>Magnetic flux paths, rotation arrows around the shaft and torque arrows (box objects).</summary>
    internal static class Annotations
    {
        public const string Cat = "machines.annotations";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef Direction = Common.Choice("direction", T("Direction", "Richtung"), "ccw",
            new ParamOption("ccw", T("Counter-clockwise", "Gegen den Uhrzeigersinn")), new ParamOption("cw", T("Clockwise", "Im Uhrzeigersinn")));

        private static bool Cw(SketchObject o) => o.Str("direction", "ccw") == "cw";

        private static ContextAction Flip(SketchObject o) => new("param.direction", T("Reverse direction", "Richtung umkehren"), IconIds.Reverse,
            x => x.WithParam("direction", Cw(x) ? "ccw" : "cw")) { Group = "param" };

        public static IEnumerable<MachType> Types()
        {
            yield return new MachType("machines.ann.flux", T("Flux path", "Flusspfad"), Cat,
                new[] { "flux", "flux line", "magnetic flux", "flux path", "field line", "fluss", "magnetfluss", "feldlinie", "flusspfad", "magnetischer kreis" },
                new ViewSpec("arc", T("Arc between two poles", "Bogen zwischen zwei Polen"), IconIds.Axial, 40, 24, FluxArc),
                new ViewSpec("loop", T("Closed loop (magnetic circuit)", "Geschlossene Schleife (magnetischer Kreis)"), IconIds.Schematic, 40, 24, FluxLoop))
            {
                Params = new[] { Direction, Common.Flag("dashed", T("Dashed line", "Gestrichelt"), false) },
                Extra = o => new[] { Flip(o), Common.Toggle(o, "dashed", false, T("Dashed on/off", "Gestrichelt an/aus")) },
            };
            yield return new MachType("machines.ann.rotation", T("Rotation arrow", "Drehrichtungspfeil"), Cat,
                new[] { "rotation", "direction of rotation", "speed", "curved arrow", "drehrichtung", "drehpfeil", "drehsinn", "drehzahl" },
                new ViewSpec("axial", T("Around the centre (axial view)", "Um den Mittelpunkt (Stirnansicht)"), IconIds.Axial, 24, 24, RotationAxial) { Hub = true },
                new ViewSpec("side", T("Around the shaft (side view)", "Um die Welle (Seitenansicht)"), IconIds.Side, 12, 24, RotationSide) { Hub = true })
            {
                Params = new[] { Direction },
                Extra = o => new[] { Flip(o) },
            };
            yield return new MachType("machines.ann.torque", T("Torque arrow", "Drehmomentpfeil"), Cat,
                new[] { "torque", "moment", "torque arrow", "drehmoment", "momentenpfeil", "moment" },
                new ViewSpec("curved", T("Curved with label", "Gebogen mit Bezeichnung"), IconIds.Axial, 24, 24, TorqueCurved) { Hub = true },
                new ViewSpec("vector", T("Moment vector (double arrow)", "Momentenvektor (Doppelpfeil)"), IconIds.Side, 30, 8, TorqueVector) { Hub = true })
            {
                Params = new[] { Direction },
                Extra = o => new[] { Flip(o) },
            };
        }

        private static List<Vec2> Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, bool reverse)
        {
            var pts = Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg), 48);
            if (reverse) pts.Reverse();
            return pts;
        }

        private static Style FluxStyle(SketchObject o) => o.Flag("dashed", false) ? S.FluxArrow : S.Arrow with { StrokeWidth = 0.35 };

        /// <summary>U-shaped flux line from one pole through the yoke into the neighbouring pole.</summary>
        private static void FluxArc(Pen p, SketchObject o)
        {
            p.PolyD(Arc(0, 11, 18.5, 22, 180, 180, Cw(o)), FluxStyle(o), false, "flux");
        }

        /// <summary>Closed flux loop of a magnetic circuit with two arrowheads.</summary>
        private static void FluxLoop(Pen p, SketchObject o)
        {
            var cw = Cw(o);
            p.PolyD(Arc(0, 0, 19, 11, 200, 160, cw), FluxStyle(o), false, "flux");
            p.PolyD(Arc(0, 0, 19, 11, 20, 160, cw), FluxStyle(o), false, "flux");
        }

        private static void RotationAxial(Pen p, SketchObject o)
        {
            // counter-clockwise on screen = negative sweep (y down)
            p.PolyD(Arc(0, 0, 10, 10, 0, -270, Cw(o)), S.Arrow, false, "arrow");
            p.Round(Vec2.Zero, 0.8, S.Black, "axis");
        }

        private static void RotationSide(Pen p, SketchObject o)
        {
            p.PolyD(Arc(0, 0, 4.6, 10, 115, 290, Cw(o)), S.Arrow, false, "arrow");
        }

        private static void TorqueCurved(Pen p, SketchObject o)
        {
            p.PolyD(Arc(0, 0, 10, 10, 160, -220, Cw(o)), S.Arrow with { StrokeWidth = 0.5 }, false, "arrow");
            var label = string.IsNullOrWhiteSpace(o.Text) ? Common.L(p, "T", "M") : o.Text!;
            var size = Math.Min(6, 14.0 / Math.Max(1, label.Length) / 0.55);
            p.Text(0, 0, label, size, "label", bold: true);
        }

        /// <summary>Moment vector: double-headed arrow along the axis (right-hand rule).</summary>
        private static void TorqueVector(Pen p, SketchObject o)
        {
            var cw = Cw(o);
            double a = cw ? 15 : -15, b = -a;
            p.Line(a, 0, b, 0, S.Arrow with { StrokeWidth = 0.5 }, "arrow");
            // second head behind the tip (explicit polygon so it is identical in every host)
            var len = ArrowHeads.Length(ArrowSize.Medium, 0.5);
            var w = len * 0.38;
            var dir = new Vec2(Math.Sign(b - a), 0);
            var tip = p.V(b, 0) - dir * len;
            var back = tip - dir * len;
            p.Add(Prim.Polygon(new[] { tip, back + new Vec2(0, w), back - new Vec2(0, w) }, S.Thin with { Fill = Fill.Solid("#000000"), StrokeWidth = 0.5 }), "arrow");
        }
    }
}
