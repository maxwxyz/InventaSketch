using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Drive-specific arrows: rotation, torque and power flow (box objects).</summary>
    internal static class Arrows
    {
        public const string Cat = "mech.arrows";

        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Arc arrow style: thin-ish continuous line with a filled head at the end.</summary>
        private static readonly Style ArcArrow = Style.Visible with { StrokeWidth = 0.4, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };

        /// <summary>Power flow: thick coloured path with a triangle head (monochrome export turns it black).</summary>
        private static readonly Style Flow = Style.Visible with { StrokeWidth = 1.0, Stroke = "#C0392B", ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };

        private static readonly ParamDef Direction = new("direction", T("Direction", "Drehrichtung"), ParamKind.Choice)
        {
            Default = "ccw",
            Options = new[] { new ParamOption("ccw", T("Counter-clockwise", "Gegen den Uhrzeigersinn")), new ParamOption("cw", T("Clockwise", "Im Uhrzeigersinn")) },
        };

        private static bool Cw(SketchObject o) => o.Str("direction", "ccw") == "cw";

        private static ContextAction Flip(SketchObject o) => new("param.direction", T("Reverse direction", "Richtung umkehren"), "reverse",
            x => x.WithParam("direction", Cw(x) ? "ccw" : "cw")) { Group = "param" };

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType("mech.arrow.rotation", T("Rotation arrow", "Drehrichtungspfeil"), Cat,
                new[] { "rotation", "direction of rotation", "curved arrow", "drehrichtung", "drehpfeil", "rotationspfeil", "drehsinn" },
                new ViewSpec("side", T("Around shaft (side)", "Um die Welle (seitlich)"), 12, 24, RotationSide),
                new ViewSpec("axial", T("Axial view", "Stirnansicht"), 24, 24, RotationAxial),
                new ViewSpec("toward", T("Axis toward viewer (dot)", "Achse zum Betrachter (Punkt)"), 12, 12, (p, o) => AxisSymbol(p, false)),
                new ViewSpec("away", T("Axis away from viewer (cross)", "Achse vom Betrachter weg (Kreuz)"), 12, 12, (p, o) => AxisSymbol(p, true)))
            {
                Params = new[] { Direction },
                Extra = o => new[] { Flip(o) },
            };
            yield return new MechType("mech.arrow.torque", T("Torque arrow", "Drehmomentpfeil"), Cat,
                new[] { "torque", "moment", "drehmoment", "momentenpfeil", "moment" },
                new ViewSpec("curved", T("Curved", "Gebogen"), 24, 24, TorqueCurved),
                new ViewSpec("vector", T("Moment vector", "Momentenvektor"), 30, 8, TorqueVector))
            {
                Params = new[] { Direction },
                Extra = o => new[] { Flip(o) },
            };
            yield return new MechType("mech.arrow.powerflow", T("Power flow arrow", "Leistungsflusspfeil"), Cat,
                new[] { "power flow", "energy flow", "torque path", "leistungsfluss", "kraftfluss", "energiefluss", "momentenfluss" },
                new ViewSpec("straight", T("Straight", "Gerade"), 40, 8, (p, o) => p.Poly(Flow, false, -19, 0, 17.5, 0)),
                new ViewSpec("bent", T("Bent", "Abgewinkelt"), 40, 20, (p, o) => p.Poly(Flow, false, -19, -8, 4, -8, 4, 8, 17.5, 8)),
                new ViewSpec("ushape", T("U-turn (through gearbox)", "U-Form (durch das Getriebe)"), 40, 20, PowerU))
            {
                Rotating = false,
            };
        }

        private static List<Vec2> Arc(double rx, double ry, double startDeg, double sweepDeg, bool reverse)
        {
            var pts = Pen.ArcD(0, 0, rx, ry, startDeg, sweepDeg, 48);
            if (reverse) pts.Reverse();
            return pts;
        }

        /// <summary>Rotation arrow wrapped around a horizontal shaft (seen from the side): open ellipse with a head.</summary>
        private static void RotationSide(Pen p, SketchObject o)
        {
            p.PolyD(Arc(4.6, 10, 115, 290, Cw(o)), ArcArrow, false, "arrow");
        }

        private static void RotationAxial(Pen p, SketchObject o)
        {
            // Counter-clockwise on screen = negative sweep (y down).
            p.PolyD(Arc(10, 10, 0, -270, Cw(o)), ArcArrow, false, "arrow");
            p.Round(p.V(0, 0), 0.8, S.Black, "axis");
        }

        /// <summary>Axis direction symbols: dot = pointing toward the viewer, cross = away from the viewer.</summary>
        private static void AxisSymbol(Pen p, bool away)
        {
            p.Hole(0, 0, 5, S.Vis, "circle");
            var c = p.V(0, 0);
            if (away)
            {
                p.Seg(c, -3.5, -3.5, 3.5, 3.5, S.Vis);
                p.Seg(c, -3.5, 3.5, 3.5, -3.5, S.Vis);
            }
            else p.Round(c, 1.2, S.Black, "dot");
        }

        /// <summary>Filled triangular head with its tip at <paramref name="tip"/>, pointing along <paramref name="dir"/>.</summary>
        private static void Head(Pen p, Vec2 tip, Vec2 dir, double l)
        {
            var d = dir.Normalized();
            var n = d.Perp * (l * 0.38);
            var back = tip - d * l;
            p.PolyP(new[] { tip, back + n, back - n }, S.Black with { StrokeWidth = 0.2 }, true, "head");
        }

        /// <summary>Head length: standard size, reduced for very small boxes.</summary>
        private static double HeadLength(Pen p) => Math.Min(2.5, 0.12 * Math.Min(p.W, p.H));

        /// <summary>Curved torque arrow with a double head (two stacked heads).</summary>
        private static void TorqueCurved(Pen p, SketchObject o)
        {
            var pts = Arc(8.5, 8.5, 30, -240, Cw(o)).Select(p.V).ToList();
            var l = HeadLength(p);
            // cut the arc back so it ends inside the second head
            var tip = pts[pts.Count - 1];
            var dir = (tip - pts[pts.Count - 3]).Normalized();
            var path = pts.Where(q => q.DistanceTo(tip) > l * 1.6).ToList();
            p.PolyP(path, S.Vis with { StrokeWidth = 0.4 }, false, "arrow");
            Head(p, tip, dir, l);
            Head(p, tip - dir * (l * 0.9), dir, l);
            p.Round(p.V(0, 0), 0.8, S.Black, "axis");
        }

        /// <summary>Moment vector: straight line with a double arrowhead (right-hand rule).</summary>
        private static void TorqueVector(Pen p, SketchObject o)
        {
            var flip = Cw(o);
            var a = p.V(flip ? 14 : -14, 0);
            var b = p.V(flip ? -14 : 14, 0);
            var dir = (b - a).Normalized();
            var l = HeadLength(p);
            p.LineP(a, b - dir * (l * 1.5), S.Vis, "shaft");
            Head(p, b, dir, l);
            Head(p, b - dir * (l * 0.9), dir, l);
        }

        private static void PowerU(Pen p, SketchObject o)
        {
            var pts = new List<Vec2> { new(-19, -7) };
            pts.Add(new Vec2(8, -7));
            pts.AddRange(Pen.ArcD(8, 0, 7, 7, -90, 180, 24).Skip(1));
            pts.Add(new Vec2(-17.5, 7));
            p.PolyD(pts, Flow, false, "flow");
        }
    }
}
