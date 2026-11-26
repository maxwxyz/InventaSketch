using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Generic body of a car-like vehicle (car, SUV, van, bus). All views are derived from a few key dimensions in
    /// design millimetres (scene scale 1:100, i.e. 10 mm per metre). Conventions: driving direction +x (front on the
    /// right), y down, the vehicle's left side is -y in the top view.
    /// </summary>
    internal sealed class CarBody
    {
        public double L { get; init; }
        /// <summary>Overall width including mirrors (top/front design height/width).</summary>
        public double Wd { get; init; }
        /// <summary>Side view height (ground at +H/2).</summary>
        public double H { get; init; }
        public double Clearance { get; init; }
        public double WheelR { get; init; }
        public double ArchR { get; init; }
        public double[] Axles { get; init; } = Array.Empty<double>();
        public double BumperY { get; init; }
        public double NoseY { get; init; }
        public double HoodX { get; init; }
        public double HoodY { get; init; }
        public double RoofFX { get; init; }
        public double RoofRX { get; init; }
        public double RoofY { get; init; }
        public double DeckX { get; init; }
        public double DeckY { get; init; }
        public double TailY { get; init; }
        public double BeltY { get; init; }
        public double CornerR { get; init; } = 1.5;
        public double TopRatio { get; init; } = 0.75;
        /// <summary>Front taper of the top-view outline (fraction of the body width).</summary>
        public double Taper { get; init; } = 0.1;
        public double[] Pillars { get; init; } = Array.Empty<double>();
        public double PillarW { get; init; } = 0.9;
        public double[] Seams { get; init; } = Array.Empty<double>();
        /// <summary>Glazed door panels (x0, x1), e.g. bus doors.</summary>
        public (double X0, double X1)[] DoorPanels { get; init; } = Array.Empty<(double, double)>();

        public double G => H / 2;
        public double Sill => G - Clearance;
        public double Wb => Wd * 0.88;
        public double WheelY => G - WheelR;

        public Size2 TopSize => new(L, Wd);
        public Size2 SideSize => new(L, H);
        public Size2 FrontSize => new(Wd, H);

        private static double XAt(Vec2 a, Vec2 b, double y)
        {
            if (Math.Abs(b.Y - a.Y) < 1e-9) return a.X;
            var t = Geo.Clamp((y - a.Y) / (b.Y - a.Y), 0, 1);
            return a.X + (b.X - a.X) * t;
        }

        private List<Vec2> Upper() => new()
        {
            new(L / 2 - 0.8, Sill), new(L / 2, BumperY), new(L / 2 - 0.4, NoseY), new(HoodX, HoodY),
            new(RoofFX, RoofY), new(RoofRX, RoofY), new(DeckX, DeckY), new(-L / 2 + 0.3, TailY),
            new(-L / 2, BumperY), new(-L / 2 + 0.8, Sill),
        };

        public void Side(Pen p, SketchObject o)
        {
            var outline = Upper();
            outline.AddRange(Body.ArchedBottom(-L / 2 + 0.8, L / 2 - 0.8, Sill, Axles, WheelY, ArchR));
            p.Rounded(outline, CornerR, S.Opaque, AutoTags.Body);

            // side glass between windshield line and rear line, above the belt line
            var ws0 = new Vec2(HoodX, HoodY); var ws1 = new Vec2(RoofFX, RoofY);
            var rw0 = new Vec2(RoofRX, RoofY); var rw1 = new Vec2(DeckX, DeckY);
            const double inset = 0.7;
            var top = RoofY + inset;
            var glass = new List<Vec2>
            {
                new(XAt(ws0, ws1, BeltY) - inset, BeltY), new(XAt(ws0, ws1, top) - inset, top),
                new(XAt(rw0, rw1, top) + inset, top), new(XAt(rw0, rw1, BeltY) + inset, BeltY),
            };
            p.Rounded(glass, 0.6, S.Fine, AutoTags.Glass);
            foreach (var x in Pillars) p.Box(x - PillarW / 2, top - 0.2, x + PillarW / 2, BeltY + 0.2, S.Opaque with { StrokeWidth = 0.35 }, "pillar");
            foreach (var (x0, x1) in DoorPanels)
            {
                p.Box(x0, top - 0.2, x1, Sill - 0.3, S.Opaque with { StrokeWidth = 0.35 }, "door");
                p.Line((x0 + x1) / 2, top - 0.2, (x0 + x1) / 2, Sill - 0.3, S.Fine, "door");
                p.Box(x0 + 0.6, top + 0.5, (x0 + x1) / 2 - 0.5, BeltY + 1.5, S.Fine, AutoTags.Glass);
                p.Box((x0 + x1) / 2 + 0.5, top + 0.5, x1 - 0.6, BeltY + 1.5, S.Fine, AutoTags.Glass);
            }
            var seams = Seams.OrderByDescending(x => x).ToList();
            for (var i = 0; i < seams.Count; i++)
            {
                p.Line(seams[i], BeltY + 0.2, seams[i], Sill - 0.4, S.Fine, "door");
                if (i > 0) p.Line(seams[i] + 0.7, BeltY + 1.3, seams[i] + 2.4, BeltY + 1.3, S.Fine, "handle");
            }
            // lights and mirror
            p.RBox(L / 2 - 3.0, NoseY + 0.5, L / 2 - 0.7, NoseY + 1.6, 0.4, S.Fine, "light");
            p.RBox(-L / 2 + 0.7, TailY + 0.5, -L / 2 + 2.3, TailY + 1.9, 0.3, S.Fine, "light");
            var mx = XAt(ws0, ws1, BeltY);
            p.Rounded(new[] { new Vec2(mx - 0.2, BeltY - 0.3), new Vec2(mx - 2.0, BeltY - 0.5), new Vec2(mx - 2.0, BeltY - 1.6), new Vec2(mx - 0.6, BeltY - 1.7) }, 0.4, S.OpaqueFine, "mirror");
            foreach (var x in Axles) Body.Wheel(p, x, WheelY, WheelR);
        }

        public IEnumerable<Anchor> SideAnchors(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(Axles.Max(), WheelY), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(Axles.Min(), WheelY), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2, (NoseY + BumperY) / 2), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2, (TailY + BumperY) / 2), "mount.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(RoofFX, RoofY), "mount.roof");
            yield return Anchor.Point(AnchorRole.Mount, p.V(XAt(new Vec2(HoodX, HoodY), new Vec2(RoofFX, RoofY), RoofY + 1.2) - 0.6, RoofY + 1.2), "mount.windshield");
            yield return Anchor.Point(AnchorRole.Mount, p.V(XAt(new Vec2(HoodX, HoodY), new Vec2(RoofFX, RoofY), BeltY) - 1.2, BeltY - 1.0), "mount.mirror");
        }

        public void Top(Pen p, SketchObject o)
        {
            double hb = Wb / 2, nose = L * 0.06, taper = Wb * Taper;
            var outline = new[]
            {
                new Vec2(-L / 2, -hb), new Vec2(L / 2 - nose, -hb), new Vec2(L / 2, -hb + taper),
                new Vec2(L / 2, hb - taper), new Vec2(L / 2 - nose, hb), new Vec2(-L / 2, hb),
            };
            // mirrors first (body covers their roots)
            var mx = HoodX - 0.4;
            foreach (var sgn in new[] { -1.0, 1.0 })
                p.Rounded(new[] { new Vec2(mx, sgn * (hb - 0.5)), new Vec2(mx - 2.4, sgn * (hb - 0.5)), new Vec2(mx - 1.8, sgn * Wd / 2), new Vec2(mx - 0.5, sgn * Wd / 2) }, 0.4, S.OpaqueFine, "mirror");
            p.Rounded(outline, Math.Min(2.6, Wb * 0.16), S.Opaque, AutoTags.Body);
            // greenhouse: windshield, roof, rear window and pillars
            double ga = hb - 1.1, wr = hb * TopRatio - 0.4;
            p.Rounded(new[] { new Vec2(HoodX, -ga), new Vec2(HoodX, ga), new Vec2(DeckX, ga), new Vec2(DeckX, -ga) }, 1.2, S.Fine, AutoTags.Glass);
            p.Box(RoofRX, -wr, RoofFX, wr, S.Fine, "roof");
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                p.Line(RoofFX, sgn * wr, HoodX, sgn * ga, S.Fine, "pillar");
                p.Line(RoofRX, sgn * wr, DeckX, sgn * ga, S.Fine, "pillar");
            }
            // glass indication on the windshield
            var span = HoodX - RoofFX;
            if (span > 3)
            {
                var wx = (HoodX + RoofFX) / 2;
                var k = span / 8;
                p.Line(wx + 0.8 * k, -ga * 0.5, wx - 0.4 * k, -ga * 0.15, S.Thin, AutoTags.Glass);
                p.Line(wx + 1.2 * k, -ga * 0.25, wx, ga * 0.1, S.Thin, AutoTags.Glass);
            }
            TopWheels(p, Axles, WheelR, hb, Wb * 0.13);
        }

        public static void TopWheels(Pen p, IEnumerable<double> axles, double r, double hb, double tw)
        {
            foreach (var x in axles)
                foreach (var sgn in new[] { -1.0, 1.0 })
                {
                    var y0 = sgn * (hb - 0.3); var y1 = sgn * (hb - 0.3 - tw);
                    p.Box(x - r, y0, x + r, y1, S.Hid, AutoTags.Wheel);
                }
        }

        public IEnumerable<Anchor> TopAnchors(Pen p, SketchObject o)
        {
            double hb = Wb / 2;
            yield return Anchor.Point(AnchorRole.Hub, p.V(Axles.Max(), 0), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(Axles.Min(), 0), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2, 0), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2, 0), "mount.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(RoofFX, 0), "mount.roof");
            yield return Anchor.Point(AnchorRole.Mount, p.V(HoodX - 1.4, -Wd / 2), "mount.mirror.left");
            yield return Anchor.Point(AnchorRole.Mount, p.V(HoodX - 1.4, Wd / 2), "mount.mirror.right");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2 - L * 0.03, -hb + Wb * Taper * 0.5), "mount.corner.fl");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2 - L * 0.03, hb - Wb * Taper * 0.5), "mount.corner.fr");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2 + 0.8, -hb + 0.8), "mount.corner.rl");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2 + 0.8, hb - 0.8), "mount.corner.rr");
        }

        public void Front(Pen p, SketchObject o, bool rear)
        {
            double hb = Wb / 2;
            var tw = Wb * 0.13;
            foreach (var sgn in new[] { -1.0, 1.0 })
                p.Box(sgn * (hb - 0.5), Sill - 1.5, sgn * (hb - 0.5 - tw), G, S.Vis, AutoTags.Wheel);
            if (!rear)
                foreach (var sgn in new[] { -1.0, 1.0 })
                    p.Rounded(new[] { new Vec2(sgn * (hb - 0.6), BeltY - 0.4), new Vec2(sgn * Wd / 2, BeltY - 0.7), new Vec2(sgn * Wd / 2, BeltY - 2.1), new Vec2(sgn * (hb - 0.6), BeltY - 2.2) }, 0.4, S.OpaqueFine, "mirror");
            double gb = hb - 0.6, gt = hb * TopRatio;
            p.Rounded(new[] { new Vec2(-gb, BeltY + 0.5), new Vec2(-gt, RoofY), new Vec2(gt, RoofY), new Vec2(gb, BeltY + 0.5) }, CornerR, S.Opaque, AutoTags.Body);
            var ins = 0.8;
            p.Rounded(new[] { new Vec2(-gb + ins + 0.2, BeltY - 0.3), new Vec2(-gt + ins, RoofY + ins), new Vec2(gt - ins, RoofY + ins), new Vec2(gb - ins - 0.2, BeltY - 0.3) }, 0.6, S.Fine, AutoTags.Glass);
            var lowTop = rear ? Math.Min(BeltY, DeckY) : Math.Min(BeltY, NoseY);
            p.Rounded(new[] { new Vec2(-hb + 0.5, Sill), new Vec2(-hb, BumperY), new Vec2(-hb + 0.2, lowTop), new Vec2(hb - 0.2, lowTop), new Vec2(hb, BumperY), new Vec2(hb - 0.5, Sill) }, CornerR, S.Opaque, AutoTags.Body);
            var ly = rear ? Math.Max(lowTop + 0.4, TailY + 0.4) : Math.Max(lowTop + 0.4, NoseY + 0.4);
            var lh = Math.Min(1.4, Math.Max(0.6, (BumperY - ly) * 0.5));
            foreach (var sgn in new[] { -1.0, 1.0 })
                p.RBox(sgn * (hb - 0.9), ly, sgn * (hb - 0.9 - Wb * 0.2), ly + lh, 0.4, S.Fine, "light");
            if (!rear) p.RBox(-Wb * 0.17, ly + 0.3, Wb * 0.17, Math.Max(ly + 0.8, BumperY - 0.3), 0.5, S.Fine, "grille");
            var py = Math.Min(BumperY + 0.4, Sill - 1.4);
            p.Box(-Wb * 0.13, py, Wb * 0.13, py + Math.Min(1.1, Sill - py - 0.2), S.Fine, "plate");
        }

        public IEnumerable<Anchor> FrontAnchors(Pen p, SketchObject o, bool rear)
        {
            yield return Anchor.Point(AnchorRole.Mount, p.V(0, rear ? (DeckY + BumperY) / 2 : (NoseY + BumperY) / 2), rear ? "mount.rear" : "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(0, RoofY), "mount.roof");
            if (!rear)
            {
                yield return Anchor.Point(AnchorRole.Mount, p.V(0, RoofY + 1.2), "mount.windshield");
                yield return Anchor.Point(AnchorRole.Mount, p.V(-Wd / 2, BeltY - 1.4), "mount.mirror.left");
                yield return Anchor.Point(AnchorRole.Mount, p.V(Wd / 2, BeltY - 1.4), "mount.mirror.right");
            }
        }
    }

    /// <summary>Shared body helpers (arched sill line, wheels).</summary>
    internal static class Body
    {
        /// <summary>Sill points from rear to front with wheel arches cut around the given axles.</summary>
        public static List<Vec2> ArchedBottom(double xRear, double xFront, double sill, IEnumerable<double> axles, double wheelY, double archR)
        {
            var pts = new List<Vec2>();
            var dy = sill - wheelY;
            foreach (var c in axles.OrderBy(x => x))
            {
                if (archR <= Math.Abs(dy)) continue;
                var dx = Math.Sqrt(archR * archR - dy * dy);
                if (c - dx <= xRear || c + dx >= xFront) continue;
                var a = Math.Asin(dy / archR);
                pts.Add(new Vec2(c - dx, sill));
                pts.AddRange(Geo.ArcPoints(new Vec2(c, wheelY), archR, archR, Math.PI - a, Math.PI + 2 * a, 16).Skip(1).Take(15));
                pts.Add(new Vec2(c + dx, sill));
            }
            pts.Reverse();
            return pts;
        }

        public static void Wheel(Pen p, double x, double y, double r)
        {
            p.Circle(x, y, r, S.Opaque, AutoTags.Wheel);
            p.Circle(x, y, r * 0.55, S.Fine, "rim");
            p.Circle(x, y, r * 0.15, S.Thin, "hub");
        }
    }

    /// <summary>Vehicle types: car, SUV, van, bus, tram, truck, tractor-trailer, motorcycle, bicycle, e-scooter, trailer, box vehicle.</summary>
    internal static class Vehicles
    {
        public const string Cat = AutoCategories.Vehicles;

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly CarBody Car = new()
        {
            L = 45, Wd = 20, H = 14.5, Clearance = 1.6, WheelR = 3.2, ArchR = 3.8, Axles = new[] { -13.5, 13.5 },
            BumperY = 3.0, NoseY = 0.4, HoodX = 12.5, HoodY = -1.0, RoofFX = 4.5, RoofRX = -7.5, RoofY = -7.0,
            DeckX = -16.5, DeckY = -1.6, TailY = -1.2, BeltY = -0.6, CornerR = 1.6, TopRatio = 0.72,
            Pillars = new[] { -1.5 }, Seams = new[] { 9.6, -1.5, -9.6 },
        };

        public static readonly CarBody Suv = new()
        {
            L = 47, Wd = 21, H = 17.5, Clearance = 2.4, WheelR = 3.7, ArchR = 4.3, Axles = new[] { -14.2, 14.2 },
            BumperY = 3.2, NoseY = -0.2, HoodX = 13.0, HoodY = -1.6, RoofFX = 6.0, RoofRX = -20.5, RoofY = -8.4,
            DeckX = -23.0, DeckY = -1.8, TailY = -1.0, BeltY = -1.2, CornerR = 1.6, TopRatio = 0.8, Taper = 0.08,
            Pillars = new[] { -1.0, -13.0 }, Seams = new[] { 9.7, -1.0, -9.7 },
        };

        public static readonly CarBody Van = new()
        {
            L = 52, Wd = 22, H = 21, Clearance = 2.0, WheelR = 3.4, ArchR = 4.0, Axles = new[] { -15.0, 16.5 },
            BumperY = 5.0, NoseY = 1.0, HoodX = 19.0, HoodY = -1.0, RoofFX = 13.0, RoofRX = -25.4, RoofY = -10.2,
            DeckX = -25.9, DeckY = -1.2, TailY = 0.0, BeltY = -0.6, CornerR = 2.0, TopRatio = 0.86, Taper = 0.06,
            Pillars = new[] { 5.5, -8.0, -18.0 }, PillarW = 1.0, Seams = new[] { 12.3, 5.5, -8.0 },
        };

        public static readonly CarBody Bus = new()
        {
            L = 120, Wd = 27, H = 32, Clearance = 3.0, WheelR = 5.0, ArchR = 5.8, Axles = new[] { -27.0, 40.0 },
            BumperY = 10.0, NoseY = -2.0, HoodX = 59.5, HoodY = -1.0, RoofFX = 58.5, RoofRX = -58.8, RoofY = -15.2,
            DeckX = -59.8, DeckY = -4.0, TailY = -3.0, BeltY = -2.5, CornerR = 1.4, TopRatio = 0.94, Taper = 0.03,
            Pillars = new[] { 44.0, 35.0, 26.0, 17.0, 8.0, -9.0, -18.0, -27.0, -36.0, -45.0, -54.0 }, PillarW = 1.2,
            DoorPanels = new[] { (46.5, 54.5), (-6.5, 1.5) },
        };

        private static AutoType CarLike(string id, I18nText name, CarBody b, string[] kw) =>
            new(id, name, Cat, kw,
                Views.TopV(b.L, b.Wd, b.Top) with { Anchors = b.TopAnchors },
                Views.SideV(b.L, b.H, b.Side) with { Anchors = b.SideAnchors },
                Views.FrontV(b.Wd, b.H, (p, o) => b.Front(p, o, false)) with { Anchors = (p, o) => b.FrontAnchors(p, o, false) },
                Views.RearV(b.Wd, b.H, (p, o) => b.Front(p, o, true)) with { Anchors = (p, o) => b.FrontAnchors(p, o, true) })
            {
                CenterHub = true,
            };

        public static IEnumerable<AutoType> Types()
        {
            yield return CarLike("auto.vehicle.car", T("Passenger car", "Pkw"), Car,
                new[] { "car", "passenger car", "sedan", "vehicle", "auto", "pkw", "fahrzeug", "limousine", "ego vehicle", "egofahrzeug" });
            yield return CarLike("auto.vehicle.suv", T("SUV", "SUV"), Suv,
                new[] { "suv", "crossover", "car", "vehicle", "geländewagen", "pkw", "fahrzeug" });
            yield return CarLike("auto.vehicle.van", T("Van", "Transporter"), Van,
                new[] { "van", "minibus", "delivery van", "vehicle", "transporter", "kleinbus", "lieferwagen", "fahrzeug" });
            yield return CarLike("auto.vehicle.bus", T("Bus", "Bus"), Bus,
                new[] { "bus", "coach", "city bus", "vehicle", "omnibus", "linienbus", "fahrzeug" });
            yield return Tram.Create();
            yield return Truck("auto.vehicle.truck", T("Truck", "Lkw"), TruckSpec.Rigid,
                new[] { "truck", "lorry", "box truck", "vehicle", "lkw", "lastwagen", "lastkraftwagen", "fahrzeug" });
            yield return Truck("auto.vehicle.semi", T("Tractor-trailer", "Sattelzug"), TruckSpec.Semi,
                new[] { "truck", "semi", "tractor-trailer", "articulated lorry", "lkw", "sattelzug", "sattelschlepper", "auflieger", "fahrzeug" });
            yield return Two.Motorcycle();
            yield return Two.Bicycle();
            yield return Two.Scooter();
            yield return Trailer();
            yield return BoxVehicle();
        }

        // ------------------------------------------------------------------ trucks

        internal sealed class TruckSpec
        {
            public double L, Wd, H, WheelR, CabX0, CabRoofY, BoxX0, BoxX1, BoxTop, BoxBottom, ChassisY0, ChassisY1;
            public double[] Axles = Array.Empty<double>();
            public bool Articulated;

            public static readonly TruckSpec Rigid = new()
            {
                L = 90, Wd = 27, H = 38, WheelR = 5.2, CabX0 = 23.5, CabRoofY = -12, BoxX0 = -45, BoxX1 = 21.5, BoxTop = -19, BoxBottom = 7.5,
                ChassisY0 = 7.5, ChassisY1 = 10.5, Axles = new[] { -25.0, 34.0 },
            };

            public static readonly TruckSpec Semi = new()
            {
                L = 165, Wd = 27, H = 40, WheelR = 5.2, CabX0 = 63.0, CabRoofY = -12, BoxX0 = -82.5, BoxX1 = 57.5, BoxTop = -20, BoxBottom = 6.5,
                ChassisY0 = 8.5, ChassisY1 = 11.0, Axles = new[] { -72.0, -61.0, -50.0, 46.0, 72.0 }, Articulated = true,
            };

            public double G => H / 2;
            public double WheelY => G - WheelR;
            public double Wb => Wd * 0.9;
            public double FrontAxle => Axles.Max();
        }

        private static AutoType Truck(string id, I18nText name, TruckSpec s, string[] kw) =>
            new(id, name, Cat, kw,
                Views.TopV(s.L, s.Wd, (p, o) => TruckTop(p, s)) with { Anchors = (p, o) => TruckTopAnchors(p, s) },
                Views.SideV(s.L, s.H, (p, o) => TruckSide(p, s)) with { Anchors = (p, o) => TruckSideAnchors(p, s) },
                Views.FrontV(s.Wd, s.H, (p, o) => TruckFront(p, s)) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(0, -2), "mount.front"), Anchor.Point(AnchorRole.Mount, p.V(0, s.CabRoofY), "mount.roof") } },
                Views.RearV(s.Wd, s.H, (p, o) => TruckRear(p, s)) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(0, s.BoxBottom + 2.5), "mount.rear") } })
            {
                CenterHub = true,
            };

        private static void TruckSide(Pen p, TruckSpec s)
        {
            double hl = s.L / 2, g = s.G, wy = s.WheelY;
            var cab0 = s.CabX0;
            // trailer / cargo box and chassis
            p.Box(s.BoxX0, s.BoxTop, s.BoxX1, s.BoxBottom, S.Opaque, AutoTags.Body);
            p.Line(s.BoxX0 + 1.2, s.BoxTop + 1.0, s.BoxX0 + 1.2, s.BoxBottom - 1.0, S.Fine, "door");
            p.Box(s.Articulated ? cab0 - 22 : s.BoxX0 + 1, s.ChassisY0, hl - 1.5, s.ChassisY1, S.Opaque with { StrokeWidth = 0.35 }, "chassis");
            if (s.Articulated)
            {
                // trailer running gear subframe above the trailer axles
                p.Box(s.Axles.Min() - s.WheelR - 1.5, s.BoxBottom, s.Axles.Where(a => a < 0).Max() + s.WheelR + 1.5, s.WheelY - s.WheelR - 0.6, S.Opaque with { StrokeWidth = 0.35 }, "subframe");
                // landing gear and fifth wheel
                p.Line(28, s.BoxBottom, 28, g - 1.2, S.Fine, "landing");
                p.Box(26.8, g - 1.2, 29.2, g - 0.6, S.Fine, "landing");
                p.Box(s.Axles.Where(a => a < s.FrontAxle).Max() - 3.5, s.ChassisY0 - 1.2, s.Axles.Where(a => a < s.FrontAxle).Max() + 3.5, s.ChassisY0, S.Fine, "fifthwheel");
                // roof fairing
                p.Rounded(new[] { new Vec2(cab0 + 0.5, s.CabRoofY), new Vec2(cab0 + 1.5, s.BoxTop + 0.6), new Vec2(cab0 + 8, s.BoxTop + 0.4), new Vec2(cab0 + 15, s.CabRoofY) }, 1.0, S.Opaque, "fairing");
            }
            // cab with front wheel arch
            var cab = new List<Vec2>
            {
                new(hl - 0.5, g - 6.5), new(hl, -2.0), new(hl - 0.6, s.CabRoofY + 1.2), new(hl - 2.0, s.CabRoofY), new(cab0, s.CabRoofY), new(cab0, g - 6.5),
            };
            cab.AddRange(Body.ArchedBottom(cab0, hl - 0.5, g - 6.5, new[] { s.FrontAxle }, wy, s.WheelR + 0.8));
            p.Rounded(cab, 1.2, S.Opaque, AutoTags.Body);
            p.Rounded(new[] { new Vec2(hl - 1.0, s.CabRoofY + 1.6), new Vec2(cab0 + 9, s.CabRoofY + 1.4), new Vec2(cab0 + 9, -3.0), new Vec2(hl - 0.7, -2.4) }, 0.6, S.Fine, AutoTags.Glass);
            p.Line(cab0 + 8, s.CabRoofY + 1.0, cab0 + 8, g - 6.8, S.Fine, "door");
            p.Line(cab0 + 6.6, -0.8, cab0 + 4.8, -0.8, S.Fine, "handle");
            p.Box(cab0 + 9, g - 6.0, cab0 + 12, g - 5.3, S.Fine, "step");
            p.Rounded(new[] { new Vec2(hl - 0.9, s.CabRoofY + 3.0), new Vec2(hl - 0.2, s.CabRoofY + 3.4), new Vec2(hl - 0.2, s.CabRoofY + 7.5), new Vec2(hl - 0.9, s.CabRoofY + 7.5) }, 0.3, S.OpaqueFine, "mirror");
            foreach (var x in s.Axles) Body.Wheel(p, x, wy, s.WheelR);
        }

        private static IEnumerable<Anchor> TruckSideAnchors(Pen p, TruckSpec s)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(s.FrontAxle, s.WheelY), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(s.Axles.Min(), s.WheelY), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(s.L / 2, -2), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(s.L / 2 - 2, s.CabRoofY), "mount.roof");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-s.L / 2, s.BoxBottom), "mount.rear");
        }

        private static void TruckTop(Pen p, TruckSpec s)
        {
            double hl = s.L / 2, hb = s.Wb / 2;
            foreach (var sgn in new[] { -1.0, 1.0 })
                p.Rounded(new[] { new Vec2(hl - 2.0, sgn * (hb - 0.5)), new Vec2(hl - 4.0, sgn * (hb - 0.5)), new Vec2(hl - 3.4, sgn * s.Wd / 2), new Vec2(hl - 2.4, sgn * s.Wd / 2) }, 0.3, S.OpaqueFine, "mirror");
            p.Box(s.BoxX0, -hb, s.BoxX1, hb, S.Opaque, AutoTags.Body);
            p.Rounded(new[] { new Vec2(s.CabX0, -hb), new Vec2(hl, -hb), new Vec2(hl, hb), new Vec2(s.CabX0, hb) }, 2.0, S.Opaque, AutoTags.Body);
            p.Line(hl - 2.2, -hb + 1.2, hl - 2.2, hb - 1.2, S.Fine, AutoTags.Glass);
            p.Box(s.CabX0 + 2, -hb + 2, hl - 4, hb - 2, S.Thin, "roof");
            CarBody.TopWheels(p, s.Axles, s.WheelR, hb, s.Wb * 0.12);
        }

        private static IEnumerable<Anchor> TruckTopAnchors(Pen p, TruckSpec s)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(s.FrontAxle, 0), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(s.Axles.Min(), 0), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(s.L / 2, 0), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-s.L / 2, 0), "mount.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(s.L / 2 - 2.2, 0), "mount.roof");
            yield return Anchor.Point(AnchorRole.Mount, p.V(s.L / 2 - 2.9, -s.Wd / 2), "mount.mirror.left");
            yield return Anchor.Point(AnchorRole.Mount, p.V(s.L / 2 - 2.9, s.Wd / 2), "mount.mirror.right");
        }

        private static void TruckFront(Pen p, TruckSpec s)
        {
            double hb = s.Wb / 2, g = s.G;
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                p.Box(sgn * (hb - 0.6), g - 9, sgn * (hb - 4.0), g, S.Vis, AutoTags.Wheel);
                p.Line(sgn * (hb - 0.3), s.CabRoofY + 3, sgn * s.Wd / 2, s.CabRoofY + 3, S.Fine, "mirror");
                p.Box(sgn * (s.Wd / 2 - 0.9), s.CabRoofY + 2, sgn * s.Wd / 2, s.CabRoofY + 7.5, S.OpaqueFine, "mirror");
            }
            p.Box(-hb, s.BoxTop, hb, s.CabRoofY + 1, S.Opaque, "box");
            p.Rounded(new[] { new Vec2(-hb, g - 6.5), new Vec2(-hb, s.CabRoofY), new Vec2(hb, s.CabRoofY), new Vec2(hb, g - 6.5) }, 1.5, S.Opaque, AutoTags.Body);
            p.Rounded(new[] { new Vec2(-hb + 1.2, -2.5), new Vec2(-hb + 1.6, s.CabRoofY + 1.6), new Vec2(hb - 1.6, s.CabRoofY + 1.6), new Vec2(hb - 1.2, -2.5) }, 0.8, S.Fine, AutoTags.Glass);
            p.RBox(-hb * 0.55, -0.5, hb * 0.55, 6.5, 0.6, S.Fine, "grille");
            for (var i = 1; i <= 3; i++) p.Line(-hb * 0.5, -0.5 + i * 1.75, hb * 0.5, -0.5 + i * 1.75, S.Thin, "grille");
            foreach (var sgn in new[] { -1.0, 1.0 }) p.RBox(sgn * (hb - 1.0), 7.0, sgn * (hb - 4.5), 8.6, 0.4, S.Fine, "light");
            p.Box(-hb, 9.2, hb, g - 6.5, S.Fine, "bumper");
        }

        private static void TruckRear(Pen p, TruckSpec s)
        {
            double hb = s.Wb / 2, g = s.G;
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                p.Box(sgn * (hb - 0.6), s.BoxBottom + 1, sgn * (hb - 3.4), g, S.Vis, AutoTags.Wheel);
                p.Box(sgn * (hb - 3.7), s.BoxBottom + 1, sgn * (hb - 6.5), g, S.Vis, AutoTags.Wheel);
            }
            p.Box(-hb, s.BoxTop, hb, s.BoxBottom, S.Opaque, AutoTags.Body);
            p.Line(0, s.BoxTop + 0.8, 0, s.BoxBottom - 0.8, S.Fine, "door");
            p.Box(-hb + 0.8, s.BoxTop + 0.8, hb - 0.8, s.BoxBottom - 0.8, S.Fine, "door");
            foreach (var x in new[] { -hb * 0.5, hb * 0.5 }) p.Line(x, s.BoxTop + 1.6, x, s.BoxBottom - 1.6, S.Thin, "lockbar");
            p.Box(-hb + 1.5, s.BoxBottom + 3.0, hb - 1.5, s.BoxBottom + 4.6, S.Opaque with { StrokeWidth = 0.35 }, "guard");
            foreach (var sgn in new[] { -1.0, 1.0 }) p.Box(sgn * (hb - 1.8), s.BoxBottom + 3.3, sgn * (hb - 4.2), s.BoxBottom + 4.3, S.Fine, "light");
        }

        // ------------------------------------------------------------------ trailer and generic box vehicle

        private static AutoType Trailer()
        {
            const double L = 40, Wd = 18, H = 14, g = 7, r = 2.9, ax = -4;
            const double bx0 = -20, bx1 = 12, top = -7, floor = 3.2, hb = 8.5;
            void Side(Pen p, SketchObject o)
            {
                p.Line(bx1 - 1, floor - 0.2, 19.2, 3.0, S.Vis, "drawbar");
                p.Rounded(new[] { new Vec2(18.4, 2.2), new Vec2(20, 2.4), new Vec2(20, 3.6), new Vec2(18.4, 3.8) }, 0.4, S.OpaqueFine, "coupling");
                p.Line(16.5, 3.0, 16.5, g - 1.2, S.Fine, "jockey");
                p.Round(16.5, g - 0.6, 0.6, S.Fine, "jockey");
                p.Box(bx0, top, bx1, floor, S.Opaque, AutoTags.Body);
                p.Line(bx0 + 1.2, top + 1, bx0 + 1.2, floor - 1, S.Fine, "door");
                p.Arc(ax, g - r, r + 0.8, r + 0.8, 180, 180, S.Fine, "fender");
                Body.Wheel(p, ax, g - r, r);
                p.RBox(bx0 + 0.3, floor - 1.8, bx0 + 1.0, floor - 0.6, 0.2, S.Fine, "light");
            }
            void Top(Pen p, SketchObject o)
            {
                p.Poly(S.Vis, false, bx1, -5.5, 19.2, 0, bx1, 5.5);
                p.Round(19.2, 0, 0.7, S.OpaqueFine, "coupling");
                p.Box(bx0, -hb, bx1, hb, S.Opaque, AutoTags.Body);
                p.Box(bx0 + 1, -hb + 1, bx1 - 1, hb - 1, S.Thin, "roof");
                CarBody.TopWheels(p, new[] { ax }, r, hb, 2.0);
            }
            void Rear(Pen p, SketchObject o)
            {
                foreach (var sgn in new[] { -1.0, 1.0 }) p.Box(sgn * (hb - 1.2), floor - 0.5, sgn * (hb - 3.4), g, S.Vis, AutoTags.Wheel);
                p.Box(-hb, top, hb, floor, S.Opaque, AutoTags.Body);
                p.Box(-hb + 0.8, top + 0.8, hb - 0.8, floor - 0.8, S.Fine, "door");
                foreach (var sgn in new[] { -1.0, 1.0 }) p.RBox(sgn * (hb - 0.9), floor - 2.4, sgn * (hb - 2.6), floor - 1.4, 0.2, S.Fine, "light");
            }
            return new AutoType("auto.vehicle.trailer", T("Trailer", "Anhänger"), Cat,
                new[] { "trailer", "box trailer", "car trailer", "anhänger", "kastenanhänger", "pkw-anhänger" },
                Views.TopV(L, Wd, Top) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(ax, 0), "axle.rear"), Anchor.Point(AnchorRole.Port, p.V(19.2, 0), "coupling"), Anchor.Point(AnchorRole.Mount, p.V(bx0, 0), "mount.rear") } },
                Views.SideV(L, H, Side) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(ax, g - r), "axle.rear"), Anchor.Point(AnchorRole.Port, p.V(19.4, 3.0), "coupling") } },
                Views.RearV(Wd, H, Rear))
            {
                CenterHub = true,
            };
        }

        private static AutoType BoxVehicle()
        {
            const double L = 40, Wd = 18, H = 14, g = 7, r = 2.8, hb = 8.2;
            double[] axles = { -12, 12 };
            void Top(Pen p, SketchObject o)
            {
                p.RBox(-L / 2, -hb, L / 2, hb, 1.5, S.Opaque, AutoTags.Body);
                p.Poly(S.Fine, true, 14, 0, 6, -4.5, 6, 4.5);
                p.Line(-14, 0, 6, 0, S.Ctr, "axis");
                CarBody.TopWheels(p, axles, r, hb, 2.2);
            }
            void Side(Pen p, SketchObject o)
            {
                p.RBox(-L / 2, -g, L / 2, g - 2.2, 1.5, S.Opaque, AutoTags.Body);
                foreach (var x in axles) Body.Wheel(p, x, g - r, r);
            }
            void Front(Pen p, SketchObject o)
            {
                foreach (var sgn in new[] { -1.0, 1.0 }) p.Box(sgn * (hb - 0.4), g - 4, sgn * (hb - 2.6), g, S.Vis, AutoTags.Wheel);
                p.RBox(-hb, -g, hb, g - 2.2, 1.5, S.Opaque, AutoTags.Body);
            }
            return new AutoType("auto.vehicle.box", T("Generic vehicle (box)", "Fahrzeug (generisch)"), Cat,
                new[] { "vehicle", "generic", "box", "ego", "object", "fahrzeug", "generisch", "quader", "egofahrzeug" },
                Views.TopV(L, Wd, Top) with { Anchors = (p, o) => BoxAnchors(p, axles, L, 0) },
                Views.SideV(L, H, Side) with { Anchors = (p, o) => BoxAnchors(p, axles, L, g - r) },
                Views.FrontV(Wd, H, Front))
            {
                CenterHub = true,
            };
        }

        private static IEnumerable<Anchor> BoxAnchors(Pen p, double[] axles, double L, double y)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(axles.Max(), y), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(axles.Min(), y), "axle.rear");
            yield return Anchor.Point(AnchorRole.Mount, p.V(L / 2, 0), "mount.front");
            yield return Anchor.Point(AnchorRole.Mount, p.V(-L / 2, 0), "mount.rear");
        }
    }

    /// <summary>Two-wheelers: motorcycle, bicycle and e-scooter.</summary>
    internal static class Two
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static IEnumerable<Anchor> Axles(Pen p, double rear, double front, double y)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(front, y), "axle.front");
            yield return Anchor.Point(AnchorRole.Hub, p.V(rear, y), "axle.rear");
        }

        public static AutoType Motorcycle()
        {
            const double r = 3.1, xr = -6.8, xf = 7.0, wy = 6 - r;
            void Side(Pen p, SketchObject o)
            {
                p.Line(-1.0, 3.7, -9.0, 1.6, S.Vis, "exhaust");
                Body.Wheel(p, xr, wy, r);
                Body.Wheel(p, xf, wy, r);
                p.Arc(xf, wy, r + 0.6, r + 0.6, 200, 110, S.Fine, "fender");
                p.Poly(S.Vis, false, -0.5, 2.6, xr, wy);
                p.Poly(S.OpaqueFine, true, xf + 0.3, wy, 5.3, -2.9, 5.9, -3.1, xf + 0.8, wy);
                p.RBox(-2.0, -0.8, 3.4, 3.2, 0.8, S.OpaqueFine, "engine");
                p.Rounded(new[] { new Vec2(5.2, -2.3), new Vec2(1.2, -3.5), new Vec2(-1.6, -2.5), new Vec2(-1.2, -0.6), new Vec2(4.7, -0.5) }, 0.8, S.Opaque with { StrokeWidth = 0.35 }, "tank");
                p.Rounded(new[] { new Vec2(-1.3, -2.6), new Vec2(-7.6, -2.3), new Vec2(-10.2, -3.0), new Vec2(-9.4, -1.5), new Vec2(-1.3, -1.2) }, 0.5, S.OpaqueFine, "seat");
                p.Line(5.6, -3.0, 4.2, -4.9, S.Vis, "handlebar");
                p.Round(6.4, -2.0, 0.9, S.OpaqueFine, "light");
            }
            void Top(Pen p, SketchObject o)
            {
                p.Box(xf - r, -0.8, xf + r, 0.8, S.Vis, AutoTags.Wheel);
                p.Box(xr - r, -0.9, xr + r, 0.9, S.Vis, AutoTags.Wheel);
                p.Rounded(new[] { new Vec2(6.2, -1.6), new Vec2(6.2, 1.6), new Vec2(-2.0, 2.4), new Vec2(-9.6, 1.4), new Vec2(-9.6, -1.4), new Vec2(-2.0, -2.4) }, 1.2, S.Opaque, AutoTags.Body);
                p.Ellipse(1.8, 0, 3.0, 1.7, S.Fine, "tank");
                p.Rounded(new[] { new Vec2(-1.4, -1.5), new Vec2(-7.6, -1.2), new Vec2(-7.6, 1.2), new Vec2(-1.4, 1.5) }, 0.6, S.Fine, "seat");
                p.Line(4.6, -3.6, 4.6, 3.6, S.Vis, "handlebar");
                foreach (var sgn in new[] { -1.0, 1.0 }) p.Round(4.9, sgn * 3.4, 0.5, S.OpaqueFine, "mirror");
            }
            void Front(Pen p, SketchObject o)
            {
                p.Box(-0.8, wy - 0.4, 0.8, 6, S.Vis, AutoTags.Wheel);
                p.Line(-1.0, wy, -0.8, -2.8, S.Fine, "fork");
                p.Line(1.0, wy, 0.8, -2.8, S.Fine, "fork");
                p.Rounded(new[] { new Vec2(-2.2, -0.6), new Vec2(-1.8, -3.6), new Vec2(1.8, -3.6), new Vec2(2.2, -0.6) }, 0.6, S.Opaque with { StrokeWidth = 0.35 }, "fairing");
                p.Round(0, -2.0, 1.0, S.OpaqueFine, "light");
                p.Line(-3.6, -4.3, 3.6, -4.3, S.Vis, "handlebar");
                foreach (var sgn in new[] { -1.0, 1.0 }) p.Round(sgn * 3.3, -5.3, 0.6, S.OpaqueFine, "mirror");
            }
            return new AutoType("auto.vehicle.motorcycle", T("Motorcycle", "Motorrad"), Vehicles.Cat,
                new[] { "motorcycle", "motorbike", "two-wheeler", "vru", "motorrad", "kraftrad", "zweirad" },
                Views.TopV(21, 8, Top) with { Anchors = (p, o) => Axles(p, xr, xf, 0) },
                Views.SideV(21, 12, Side) with { Anchors = (p, o) => Axles(p, xr, xf, wy).Append(Anchor.Point(AnchorRole.Mount, p.V(6.4, -2.0), "mount.front")) },
                Views.FrontV(8, 12, Front))
            {
                CenterHub = true,
            };
        }

        public static AutoType Bicycle()
        {
            const double r = 3.4, xr = -5.4, xf = 5.4, g = 5.25, wy = g - r;
            void Side(Pen p, SketchObject o)
            {
                foreach (var x in new[] { xr, xf })
                {
                    p.Circle(x, wy, r, S.Vis, AutoTags.Wheel);
                    p.Circle(x, wy, r - 0.5, S.Thin, "rim");
                    p.Round(x, wy, 0.25, S.Fine, "hub");
                }
                var bb = new Vec2(-0.4, 2.3); var st = new Vec2(-1.8, -2.5); var ht = new Vec2(3.6, -2.6); var hbm = new Vec2(3.9, -1.5);
                var rear = new Vec2(xr, wy); var front = new Vec2(xf, wy);
                p.Line(bb, st, S.Vis, "frame"); p.Line(st, ht, S.Vis, "frame"); p.Line(bb, hbm, S.Vis, "frame");
                p.Line(bb, rear, S.Vis, "frame"); p.Line(st, rear, S.Vis, "frame"); p.Line(ht, hbm, S.Vis, "frame");
                p.Line(hbm, front, S.Vis, "fork");
                p.Round(bb.X, bb.Y, 0.9, S.Fine, "chainring");
                p.Line(bb.X - 0.8, bb.Y - 1.2, bb.X + 0.8, bb.Y + 1.2, S.Fine, "crank");
                p.Line(st.X, st.Y, -2.0, -3.4, S.Vis, "seatpost");
                p.Rounded(new[] { new Vec2(-3.4, -3.8), new Vec2(-0.9, -3.7), new Vec2(-1.2, -3.2), new Vec2(-3.2, -3.3) }, 0.3, S.OpaqueFine, "saddle");
                p.Poly(S.Vis, false, ht.X, ht.Y, 3.4, -4.1, 2.4, -4.2);
            }
            void Top(Pen p, SketchObject o)
            {
                p.Box(xf - r, -0.4, xf + r, 0.4, S.Vis, AutoTags.Wheel);
                p.Box(xr - r, -0.4, xr + r, 0.4, S.Vis, AutoTags.Wheel);
                p.Line(-2.0, 0, 3.6, 0, S.Vis, "frame");
                p.Line(3.4, -2.6, 3.4, 2.6, S.Vis, "handlebar");
                p.Ellipse(-2.1, 0, 1.3, 0.6, S.OpaqueFine, "saddle");
                p.Line(-0.4, -1.6, -0.4, 1.6, S.Fine, "pedals");
            }
            void Front(Pen p, SketchObject o)
            {
                p.Box(-0.4, wy - r, 0.4, g, S.Vis, AutoTags.Wheel);
                p.Line(-0.6, wy, -0.5, -3.6, S.Fine, "fork");
                p.Line(0.6, wy, 0.5, -3.6, S.Fine, "fork");
                p.Line(-2.6, -4.2, 2.6, -4.2, S.Vis, "handlebar");
                p.Line(-1.7, 2.3, 1.7, 2.3, S.Fine, "pedals");
            }
            return new AutoType("auto.vehicle.bicycle", T("Bicycle", "Fahrrad"), Vehicles.Cat,
                new[] { "bicycle", "bike", "cyclist", "vru", "fahrrad", "rad", "radfahrer", "zweirad" },
                Views.TopV(18, 6, Top) with { Anchors = (p, o) => Axles(p, xr, xf, 0) },
                Views.SideV(18, 10.5, Side) with { Anchors = (p, o) => Axles(p, xr, xf, wy) },
                Views.FrontV(6, 10.5, Front))
            {
                CenterHub = true,
            };
        }

        public static AutoType Scooter()
        {
            const double r = 1.1, xr = -3.9, xf = 4.2, g = 5.75, wy = g - r;
            void Side(Pen p, SketchObject o)
            {
                p.RBox(-4.4, 3.4, 3.4, 4.1, 0.3, S.OpaqueFine, "deck");
                p.Line(xf, wy, 2.9, -5.0, S.Vis, "stem");
                p.Line(2.2, -5.2, 3.6, -5.2, S.Vis, "handlebar");
                p.Arc(xr, wy, r + 0.4, r + 0.4, 200, 110, S.Fine, "fender");
                foreach (var x in new[] { xr, xf }) Body.Wheel(p, x, wy, r);
            }
            void Top(Pen p, SketchObject o)
            {
                p.Box(xf - r, -0.35, xf + r, 0.35, S.Vis, AutoTags.Wheel);
                p.Box(xr - r, -0.35, xr + r, 0.35, S.Vis, AutoTags.Wheel);
                p.RBox(-4.4, -0.8, 3.4, 0.8, 0.4, S.Opaque, "deck");
                p.Line(2.9, -2.2, 2.9, 2.2, S.Vis, "handlebar");
                p.Line(2.9, -0.4, 4.2, 0, S.Fine, "stem");
            }
            void Front(Pen p, SketchObject o)
            {
                p.Box(-0.35, wy - r, 0.35, g, S.Vis, AutoTags.Wheel);
                p.Line(0, wy - r, 0, -5.0, S.Vis, "stem");
                p.Line(-2.2, -5.2, 2.2, -5.2, S.Vis, "handlebar");
            }
            return new AutoType("auto.vehicle.scooter", T("E-scooter", "E-Scooter"), Vehicles.Cat,
                new[] { "scooter", "e-scooter", "kick scooter", "micromobility", "vru", "roller", "tretroller", "elektroroller" },
                Views.TopV(11, 4.5, Top) with { Anchors = (p, o) => Axles(p, xr, xf, 0) },
                Views.SideV(11, 11.5, Side) with { Anchors = (p, o) => Axles(p, xr, xf, wy) },
                Views.FrontV(4.5, 11.5, Front))
            {
                CenterHub = true,
            };
        }
    }
}
