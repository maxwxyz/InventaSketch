using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Low-floor tram (two articulated sections, 20 m × 2.65 m, bidirectional) in top, side and front view at the scene
    /// scale (10 mm per metre, like the other vehicles): rounded cab ends, window band, double doors, bellows at the joint,
    /// bogies under the skirts and a half pantograph on the roof. Driving direction +x; the centre is a hub that snaps
    /// onto lane centrelines like the other vehicles.
    /// </summary>
    internal static class Tram
    {
        public const string TypeId = "auto.vehicle.tram";

        private static I18nText T(string en, string de) => new(en, de);

        private const double L = 200, Wd = 26.5, H = 40;
        private const double G = H / 2;             // rail level (side and front view)
        private const double Roof = -12.0;          // roof line (3.2 m above the rails)
        private const double Skirt = 15.6;          // body bottom (low floor)
        private const double PantoTop = -19.5;      // raised pantograph collector
        private const double WheelR = 3.2;
        private const double Joint = 1.6;           // half length of the bellows
        private static readonly double[] Bogies = { -78, 78 };
        private static readonly double[] Doors = { -58, -24, 24, 58 };

        public static AutoType Create() =>
            new(TypeId, T("Tram", "Straßenbahn"), AutoCategories.Vehicles,
                new[] { "tram", "streetcar", "light rail", "trolley", "rail vehicle", "public transport", "vehicle",
                        "straßenbahn", "tram", "stadtbahn", "niederflurbahn", "schienenfahrzeug", "öpnv", "fahrzeug" },
                Views.TopV(L, Wd, (p, o) => Top(p)) with { Anchors = (p, o) => TopAnchors(p) },
                Views.SideV(L, H, (p, o) => Side(p)) with { Anchors = (p, o) => SideAnchors(p) },
                Views.FrontV(Wd, H, (p, o) => Front(p)) with { Anchors = (p, o) => FrontAnchors(p) })
            {
                CenterHub = true,
            };

        // ------------------------------------------------------------------ side view

        private static void Side(Pen p)
        {
            var hl = L / 2;
            // bogies first: the skirt hides the upper half of the wheels
            foreach (var x in Bogies)
            {
                foreach (var dx in new[] { -6.5, 6.5 }) Body.Wheel(p, x + dx, G - WheelR, WheelR);
                p.Box(x - 9.5, Skirt - 1.2, x + 9.5, Skirt + 1.0, S.Opaque with { StrokeWidth = 0.35 }, "bogie");
            }
            // two sections with cab ends on the outside
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                double inner = sgn * Joint, outer = sgn * hl;
                var outline = new List<Vec2>
                {
                    new(inner, Skirt), new(inner, Roof),
                    new(outer - sgn * 6.0, Roof), new(outer - sgn * 1.2, Roof + 4.0), new(outer, 2.0), new(outer - sgn * 0.4, Skirt),
                };
                p.Rounded(outline, 1.6, S.Opaque, AutoTags.Body);
                // cab windshield and destination display
                p.Rounded(new[] { new Vec2(outer - sgn * 6.2, Roof + 2.6), new Vec2(outer - sgn * 1.6, Roof + 4.6), new Vec2(outer - sgn * 0.6, 1.0), new Vec2(outer - sgn * 5.4, 1.0) }, 0.6, S.Fine, AutoTags.Glass);
                p.Box(outer - sgn * 10.0, Roof + 0.8, outer - sgn * 5.4, Roof + 2.2, S.Fine, "display");
                // passenger windows between the doors
                var edges = new List<double> { inner + sgn * 2.0 };
                foreach (var d in Doors.Where(x => Math.Sign(x) == Math.Sign(sgn)).OrderBy(x => sgn * x)) { edges.Add(d - sgn * 7.0); edges.Add(d + sgn * 7.0); }
                edges.Add(outer - sgn * 8.0);
                for (var i = 0; i + 1 < edges.Count; i += 2)
                {
                    double a = Math.Min(edges[i], edges[i + 1]), b = Math.Max(edges[i], edges[i + 1]);
                    var n = Math.Max(1, (int)Math.Round((b - a) / 9.0));
                    var w = (b - a) / n;
                    for (var k = 0; k < n; k++) p.RBox(a + k * w + 0.5, Roof + 2.6, a + (k + 1) * w - 0.5, 1.6, 0.6, S.Fine, AutoTags.Glass);
                }
            }
            // double doors with glazing
            foreach (var d in Doors)
            {
                p.Box(d - 6.0, Roof + 2.0, d + 6.0, Skirt - 0.3, S.Opaque with { StrokeWidth = 0.35 }, "door");
                p.Line(d, Roof + 2.0, d, Skirt - 0.3, S.Fine, "door");
                p.Box(d - 5.0, Roof + 3.0, d - 1.0, 7.0, S.Fine, AutoTags.Glass);
                p.Box(d + 1.0, Roof + 3.0, d + 5.0, 7.0, S.Fine, AutoTags.Glass);
            }
            // bellows at the joint
            p.Box(-Joint, Roof + 0.8, Joint, Skirt - 1.0, S.Opaque with { StrokeWidth = 0.3 }, "joint");
            foreach (var x in new[] { -0.8, 0.0, 0.8 }) p.Line(x, Roof + 1.4, x, Skirt - 1.6, S.Thin, "joint");
            // roof equipment and half pantograph
            p.Box(-40, Roof - 1.4, -22, Roof, S.Opaque with { StrokeWidth = 0.3 }, "roof");
            p.Box(28, Roof - 1.2, 44, Roof, S.Opaque with { StrokeWidth = 0.3 }, "roof");
            Pantograph(p, -31);
        }

        /// <summary>Half pantograph seen from the side: base, lower arm, upper arm and collector.</summary>
        private static void Pantograph(Pen p, double x)
        {
            var s = S.Fine with { StrokeWidth = 0.4 };
            p.PolyD(new[] { new Vec2(x - 5, Roof - 1.6), new Vec2(x + 3.5, Roof - 4.6), new Vec2(x - 1.0, PantoTop + 0.6) }, s, false, "pantograph");
            p.Line(x - 4.0, PantoTop + 0.6, x + 2.0, PantoTop + 0.6, s, "pantograph");
            p.Box(x - 3.2, PantoTop, x + 1.2, PantoTop + 0.6, S.Black, "pantograph");
        }

        private static IEnumerable<Anchor> SideAnchors(Pen p)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(Bogies.Max(), G - WheelR), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(Bogies.Min(), G - WheelR), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2, 4.0), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2, 4.0), "mount.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-31, PantoTop), "mount.roof");
        }

        // ------------------------------------------------------------------ top view

        private static void Top(Pen p)
        {
            double hl = L / 2, hb = Wd / 2 - 0.6;
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                double inner = sgn * Joint, outer = sgn * hl;
                var outline = new[]
                {
                    new Vec2(inner, -hb), new Vec2(outer - sgn * 5.0, -hb), new Vec2(outer, -hb + 3.2),
                    new Vec2(outer, hb - 3.2), new Vec2(outer - sgn * 5.0, hb), new Vec2(inner, hb),
                };
                p.Rounded(outline, 2.4, S.Opaque, AutoTags.Body);
                // windshield edge and roof outline
                p.Line(outer - sgn * 4.0, -hb + 2.2, outer - sgn * 4.0, hb - 2.2, S.Fine, AutoTags.Glass);
                p.Box(inner + sgn * 3.0, -hb + 2.0, outer - sgn * 7.0, hb - 2.0, S.Thin, "roof");
            }
            foreach (var sgn in new[] { -1.0, 1.0 }) p.Line(-Joint, sgn * (hb - 0.8), Joint, sgn * (hb - 0.8), S.Fine, "joint");
            foreach (var x in new[] { -0.8, 0.0, 0.8 }) p.Line(x, -hb + 0.8, x, hb - 0.8, S.Thin, "joint");
            // roof equipment and pantograph (rhomb seen from above)
            p.Box(-40, -6, -22, 6, S.Fine, "roof");
            p.Box(28, -5, 44, 5, S.Fine, "roof");
            p.PolyD(new[] { new Vec2(-36, 0), new Vec2(-31, -8), new Vec2(-26, 0), new Vec2(-31, 8) }, S.Fine, true, "pantograph");
            CarBody.TopWheels(p, Bogies.SelectMany(x => new[] { x - 6.5, x + 6.5 }), WheelR, hb, 2.4);
        }

        private static IEnumerable<Anchor> TopAnchors(Pen p)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(Bogies.Max(), 0), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(Bogies.Min(), 0), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2, 0), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2, 0), "mount.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-31, 0), "mount.roof");
        }

        // ------------------------------------------------------------------ front view

        private static void Front(Pen p)
        {
            double hb = Wd / 2 - 0.4, top = hb - 2.2;
            // pantograph above the roof
            var s = S.Fine with { StrokeWidth = 0.4 };
            p.PolyD(new[] { new Vec2(-3.0, Roof - 0.2), new Vec2(-6.5, Roof - 3.8), new Vec2(-3.0, PantoTop + 0.6) }, s, false, "pantograph");
            p.PolyD(new[] { new Vec2(3.0, Roof - 0.2), new Vec2(6.5, Roof - 3.8), new Vec2(3.0, PantoTop + 0.6) }, s, false, "pantograph");
            p.Box(-8.0, PantoTop, 8.0, PantoTop + 0.6, S.Black, "pantograph");
            p.Box(-6.0, Roof - 1.4, 6.0, Roof, S.Opaque with { StrokeWidth = 0.3 }, "roof");
            // wheels below the skirt
            foreach (var sgn in new[] { -1.0, 1.0 }) p.Box(sgn * 6.5, Skirt, sgn * 8.5, G, S.Vis, AutoTags.Wheel);
            // body: slightly narrower at the roof
            p.Rounded(new[] { new Vec2(-hb, Skirt), new Vec2(-hb, 2.0), new Vec2(-top, Roof), new Vec2(top, Roof), new Vec2(hb, 2.0), new Vec2(hb, Skirt) }, 2.0, S.Opaque, AutoTags.Body);
            p.Box(-7.5, Roof + 0.8, 7.5, Roof + 2.6, S.Fine, "display");
            p.Rounded(new[] { new Vec2(-hb + 1.4, 4.0), new Vec2(-top + 1.0, Roof + 3.4), new Vec2(top - 1.0, Roof + 3.4), new Vec2(hb - 1.4, 4.0) }, 1.0, S.Fine, AutoTags.Glass);
            foreach (var sgn in new[] { -1.0, 1.0 })
                p.RBox(sgn * (hb - 1.4), 8.0, sgn * (hb - 4.6), 9.6, 0.5, S.Fine, "light");
            p.RBox(-3.0, 10.4, 3.0, 13.4, 0.6, S.Fine, "coupler");
            p.Line(-hb + 0.4, Skirt - 1.2, hb - 0.4, Skirt - 1.2, S.Thin, "skirt");
        }

        private static IEnumerable<Anchor> FrontAnchors(Pen p)
        {
            yield return Anchor.Point(AnchorRole.Mount, p.V(0, 4.0), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(0, PantoTop), "mount.roof");
        }
    }
}
