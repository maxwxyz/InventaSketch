using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Vehicle dynamics figures with element values (<see cref="DynValues"/>): the driving resistance diagram (a car on the
    /// road with air drag, rolling, gradient and tractive force arrows; the road tilts with an entered gradient) and the
    /// turning circle (single-track sketch with the turning centre, the radius of the front axle centre and the track of the
    /// outer front wheel; the steering angle follows an entered value). Both are insertable without parameters.
    /// </summary>
    internal static class Dynamics
    {
        public const string ResistanceId = "auto.motion.resistance";
        public const string TurningId = "auto.motion.turning";

        private static I18nText T(string en, string de) => new(en, de);

        public static IEnumerable<AutoType> Types()
        {
            yield return Resistance();
            yield return Turning();
        }

        private static string Num(double v, Lang lang, string format)
        {
            var s = v.ToString(format, CultureInfo.InvariantCulture);
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        private static readonly Style ForceArrow = S.Vis with { StrokeWidth = 0.5, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };

        // ------------------------------------------------------------------ driving resistance

        /// <summary>Largest drawn road inclination (the value may be steeper; the drawing stays inside its box).</summary>
        public const double MaxDrawnSlopeDeg = 12;

        /// <summary>The drawn road inclination (radians, uphill positive): arctan of the entered gradient, limited to ±12°.</summary>
        public static double DrawnSlope(SketchObject o)
        {
            var q = ValueModel.ReadEntered(o, DynValues.ResistanceVars[7]).Value;
            if (q is not double g || double.IsNaN(g)) return 0;
            var lim = Geo.DegToRad(MaxDrawnSlopeDeg);
            return Geo.Clamp(Math.Atan(g), -lim, lim);
        }

        private static AutoType Resistance()
        {
            void Draw(Pen p, SketchObject o)
            {
                var a = DrawnSlope(o);
                double ca = Math.Cos(a), sa = Math.Sin(a);
                // Road frame: u along the road (driving direction to the right), w up from the road; rotated about (0, 5.5).
                Vec2 Pt(double u, double w) => new(u * ca - (w - 5.5) * sa, -u * sa - (w - 5.5) * ca);
                List<Vec2> Pts(params double[] uw)
                {
                    var l = new List<Vec2>();
                    for (var i = 0; i + 1 < uw.Length; i += 2) l.Add(Pt(uw[i], uw[i + 1]));
                    return l;
                }
                void Label(double u, double w, string text, double size = 2.6) { var q = Pt(u, w); p.Text(q.X, q.Y, text, size, 12, size * 1.4); }

                // road and ground hatch
                p.Line(Pt(-30, 0), Pt(30, 0), S.Vis, AutoTags.Edge);
                for (var u = -28.0; u <= 28.0; u += 4) p.Line(Pt(u, 0), Pt(u - 1.6, -1.6), S.Thin, "ground");
                if (Math.Abs(a) > 1e-6)
                {
                    // horizontal reference and slope angle at the lower end of the road
                    var low = a > 0 ? Pt(-30, 0) : Pt(30, 0);
                    var dir = a > 0 ? 1.0 : -1.0;
                    p.Line(low, low + new Vec2(dir * 20, 0), S.Thin with { Dash = DashStyle.Dash }, "reference");
                    var start = a > 0 ? 0.0 : 180.0;
                    p.PolyD(Pen.ArcD(low.X, low.Y, 14, 14, start, -Geo.RadToDeg(a)), S.Thin, false, "angle");
                    // The gradient above the road surface at its lower end.
                    var q = ValueModel.ReadEntered(o, DynValues.ResistanceVars[7]).Value ?? 0;
                    var mid = Pt(a > 0 ? -25 : 25, 2.2);
                    p.Text(mid.X, mid.Y, Num(q * 100, p.Lang, "0.#") + " %", 2.4, 12, 3.4, tag: "gradient");
                }

                // car: body, windows, wheels, centre of gravity
                p.Rounded(Pts(-15, 3.2, -15, 7.6, -10, 9.2, -5.6, 13.6, 5.8, 13.6, 10.4, 9.4, 15, 8.4, 15, 3.2), 1.4, S.Opaque, AutoTags.Body);
                p.PolyD(Pts(-8.4, 9.6, -4.8, 12.6, 5.2, 12.6, 8.6, 9.6), S.Fine, true, AutoTags.Glass);
                foreach (var u in new[] { -9.5, 9.5 })
                {
                    var c = Pt(u, 3);
                    Body.Wheel(p, c.X, c.Y, 3);
                }
                var cg = Pt(0, 6.6);
                p.Circle(cg.X, cg.Y, 1.1, S.Fine, "cog");
                p.Line(Pt(-1.1, 6.6), Pt(1.1, 6.6), S.Fine, "cog");
                p.Line(Pt(0, 5.5), Pt(0, 7.7), S.Fine, "cog");

                // forces
                p.Line(Pt(24, 9.5), Pt(16, 9.5), ForceArrow, "force.air");
                Label(21.5, 12.3, p.L(T("F_air", "F_L")));
                p.Line(Pt(-7, -3), Pt(-15.5, -3), ForceArrow, "force.roll");
                Label(-11.2, -5.5, p.L(T("F_roll", "F_R")));
                p.Line(Pt(7, -3), Pt(16.5, -3), ForceArrow, "force.traction");
                Label(11.8, -5.5, p.L(T("F_t", "F_Z")));
                if (Math.Abs(a) > 1e-6)
                {
                    if (a > 0) p.Line(Pt(-17, 6.6), Pt(-26, 6.6), ForceArrow, "force.grade");
                    else p.Line(Pt(-26, 6.6), Pt(-17, 6.6), ForceArrow, "force.grade");
                    Label(-21.5, 9.3, p.L(T("F_grade", "F_St")));
                }
                p.Line(Pt(-5, 17), Pt(8, 17), S.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }, "speed");
                Label(-7.5, 17, "v");
            }
            return new AutoType(ResistanceId, T("Driving resistance", "Fahrwiderstand"), AutoCategories.Motion,
                new[] { "driving resistance", "road load", "air drag", "drag coefficient", "cw", "rolling resistance", "gradient", "slope", "power demand", "traction",
                        "fahrwiderstand", "luftwiderstand", "rollwiderstand", "steigung", "steigungswiderstand", "leistungsbedarf", "zugkraft" },
                Views.SideV(68, 40, Draw) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(DrawnPoint(o, 0, 6.6)), "cog") } })
            {
                Params = DynValues.ResistanceParams,
                ValueVars = DynValues.ResistanceVars,
                ValueRelations = DynValues.ResistanceRelations,
                Checks = DynValues.ResistanceChecks,
            };
        }

        /// <summary>A point of the resistance figure in design coordinates (road frame u, w; the frame tilts with the gradient).</summary>
        private static Vec2 DrawnPoint(SketchObject o, double u, double w)
        {
            var a = DrawnSlope(o);
            return new Vec2(u * Math.Cos(a) - (w - 5.5) * Math.Sin(a), -u * Math.Sin(a) - (w - 5.5) * Math.Cos(a));
        }

        // ------------------------------------------------------------------ turning circle

        /// <summary>The drawn steering angle (radians): an entered steering angle limited to 20 … 45°, else 30°.</summary>
        public static double DrawnSteer(SketchObject o)
        {
            var d = ValueModel.ReadEntered(o, DynValues.TurningVars[1]).Value;
            return Geo.Clamp(d is double v && !double.IsNaN(v) ? v : Geo.DegToRad(30), Geo.DegToRad(20), Geo.DegToRad(45));
        }

        /// <summary>One drawn element of the turning figure in world metres (fitted into the box afterwards).</summary>
        private sealed record Item(IReadOnlyList<Vec2> Points, Style Style, string Tag, bool Closed = false, string? Text = null, double Size = 0);

        private static AutoType Turning()
        {
            const double L = 2.7, B = 1.6, BodyW = 1.8, Overhang = 0.9, TyreR = 0.34, TyreW = 0.22;

            List<Item> Figure(SketchObject o, Lang lang)
            {
                var delta = DrawnSteer(o);
                var rh = L / Math.Tan(delta);
                var r = L / Math.Sin(delta);
                var m = new Vec2(-rh, 0);
                var front = new Vec2(0, -L);
                var items = new List<Item>();
                Item Seg(Vec2 a, Vec2 b, Style s, string tag) => new(new[] { a, b }, s, tag);
                List<Vec2> Arc(Vec2 c, double radius, double from, double sweep)
                {
                    var pts = new List<Vec2>();
                    for (var i = 0; i <= 24; i++) pts.Add(c + Vec2.Polar(radius, from + sweep * i / 24));
                    return pts;
                }
                List<Vec2> Tyre(Vec2 c, double angle)
                {
                    // heading up (−y), rotated by angle (positive = to the left, i.e. counter-clockwise on screen)
                    var d = new Vec2(-Math.Sin(angle), -Math.Cos(angle));
                    var n = new Vec2(d.Y * -1, d.X);
                    return new List<Vec2> { c + d * TyreR + n * (TyreW / 2), c + d * TyreR - n * (TyreW / 2), c - d * TyreR - n * (TyreW / 2), c - d * TyreR + n * (TyreW / 2) };
                }

                // turning paths: front axle centre (R) and outer front wheel (D/2)
                var aV = Math.Atan2(front.Y - m.Y, front.X - m.X);
                items.Add(new Item(Arc(m, r, aV, -Geo.DegToRad(50)), S.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }, AutoTags.Path));
                var outer = new Vec2(B / 2, -L);
                var ro = (outer - m).Length;
                var aO = Math.Atan2(outer.Y - m.Y, outer.X - m.X);
                items.Add(new Item(Arc(m, ro, aO, -Geo.DegToRad(40)), S.Phantom, "track"));

                // body outline, axles, centre line
                items.Add(new Item(new[] { new Vec2(-BodyW / 2, Overhang), new Vec2(BodyW / 2, Overhang), new Vec2(BodyW / 2, -L - Overhang), new Vec2(-BodyW / 2, -L - Overhang) },
                    S.Fine, AutoTags.Body, true));
                items.Add(Seg(new Vec2(0, Overhang), new Vec2(0, -L - Overhang), S.Ctr, "axis"));
                items.Add(Seg(new Vec2(-B / 2, 0), new Vec2(B / 2, 0), S.Vis, "axle"));
                items.Add(Seg(new Vec2(-B / 2, -L), new Vec2(B / 2, -L), S.Vis, "axle"));
                // rear axle line to the turning centre, wheel axes of the front wheels to the turning centre (Ackermann)
                items.Add(Seg(new Vec2(-B / 2, 0), m, S.Ctr, "axis"));
                foreach (var sgn in new[] { -1.0, 1.0 })
                {
                    var c = new Vec2(sgn * B / 2, -L);
                    var ang = Math.Atan2(L, rh + sgn * B / 2);
                    items.Add(Seg(c, m, Style.Helper, "steer"));
                    items.Add(new Item(Tyre(c, ang), S.Opaque, AutoTags.Wheel, true));
                    items.Add(new Item(Tyre(new Vec2(sgn * B / 2, 0), 0), S.Opaque, AutoTags.Wheel, true));
                }
                // radius of the front axle centre and the turning centre
                items.Add(Seg(m, front, S.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }, "radius"));
                const double cr = 0.18;
                items.Add(Seg(m + new Vec2(-cr * 1.6, 0), m + new Vec2(cr * 1.6, 0), S.Fine, "center"));
                items.Add(Seg(m + new Vec2(0, -cr * 1.6), m + new Vec2(0, cr * 1.6), S.Fine, "center"));

                // labels (text height in world metres, fitted with the figure)
                var th = 0.42;
                items.Add(new Item(new[] { m + new Vec2(0, 0.55) }, S.Thin, AutoTags.Label, Text: "M", Size: th));
                var mid = (m + front) * 0.5;
                var nrm = (front - m).Normalized();
                items.Add(new Item(new[] { mid + new Vec2(nrm.Y, -nrm.X) * 0.4 }, S.Thin, AutoTags.Label, Text: "R", Size: th));
                items.Add(new Item(new[] { new Vec2(-B / 2 - 0.55, -L - 0.55) }, S.Thin, AutoTags.Label, Text: "δ", Size: th));
                items.Add(new Item(new[] { new Vec2(0.38, -L / 2) }, S.Thin, AutoTags.Label, Text: "L", Size: th));
                return items;
            }

            (double K, Vec2 C) Fit(IReadOnlyList<Item> items, double dw, double dh)
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                foreach (var it in items)
                    foreach (var q in it.Points)
                    {
                        var hw = it.Text is null ? 0 : it.Text.Length * it.Size * Shapes.CharWidth / 2 + 0.05;
                        var hh = it.Text is null ? 0 : it.Size * 0.7;
                        x0 = Math.Min(x0, q.X - hw); x1 = Math.Max(x1, q.X + hw);
                        y0 = Math.Min(y0, q.Y - hh); y1 = Math.Max(y1, q.Y + hh);
                    }
                var k = Math.Min(dw * 0.94 / Math.Max(1e-6, x1 - x0), dh * 0.94 / Math.Max(1e-6, y1 - y0));
                return (k, new Vec2((x0 + x1) / 2, (y0 + y1) / 2));
            }

            void Draw(Pen p, SketchObject o)
            {
                var items = Figure(o, p.Lang);
                var (k, c) = Fit(items, p.DW, p.DH);
                Vec2 D(Vec2 w) => (w - c) * k;
                foreach (var it in items)
                {
                    if (it.Text is not null)
                    {
                        var at = D(it.Points[0]);
                        p.Text(at.X, at.Y, it.Text, it.Size * k, it.Text.Length * it.Size * k, it.Size * k * 1.3, tag: it.Tag);
                    }
                    else p.PolyD(it.Points.Select(D), it.Style, it.Closed, it.Tag);
                }
            }

            IEnumerable<Anchor> Anchors(Pen p, SketchObject o)
            {
                var items = Figure(o, Lang.En);
                var (k, c) = Fit(items, p.DW, p.DH);
                var delta = DrawnSteer(o);
                var m = new Vec2(-L / Math.Tan(delta), 0);
                yield return Anchor.Point(AnchorRole.Port, p.V((m - c) * k), "center");
                yield return Anchor.Point(AnchorRole.Hub, p.V((new Vec2(0, -L / 2) - c) * k), "vehicle");
            }

            return new AutoType(TurningId, T("Turning circle", "Wendekreis"), AutoCategories.Motion,
                new[] { "turning circle", "turning radius", "steering angle", "wheelbase", "ackermann", "single-track model", "lateral acceleration", "curve",
                        "wendekreis", "wendekreisradius", "lenkwinkel", "radstand", "einspurmodell", "querbeschleunigung", "kurve" },
                Views.TopV(56, 48, Draw) with { Anchors = Anchors })
            {
                Params = DynValues.TurningParams,
                ValueVars = DynValues.TurningVars,
                ValueRelations = DynValues.TurningRelations,
                Checks = DynValues.TurningChecks,
            };
        }
    }
}
