using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Vehicle subsystem overlays (axles and wheels, steering wheel, battery pack, e-drive unit, seat, door). The axle
    /// overlay is designed for the passenger-car footprint, so placed on a car of default size it lines up exactly.
    /// </summary>
    internal static class Subsystems
    {
        public const string Cat = AutoCategories.Subsystems;

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef AxleCount = Prm.Int("axles", "Number of axles", "Anzahl Achsen", 2, 1, 5);
        private static readonly ParamDef Steer = Prm.Num("steer", "Steering angle", "Lenkwinkel", 0, -40, 40, "°", 5);
        private static readonly ParamDef Centerline = Prm.Bool("centerline", "Vehicle centre line", "Fahrzeugmittellinie", true);
        private static readonly ParamDef Modules = Prm.Int("modules", "Modules per row", "Module je Reihe", 6, 1, 16);
        private static readonly ParamDef Inverter = Prm.Bool("inverter", "Show inverter", "Wechselrichter zeigen", true);
        private static readonly ParamDef Open = Prm.Num("open", "Opening angle", "Öffnungswinkel", 60, 0, 90, "°", 5);
        private static readonly ParamDef Headrest = Prm.Bool("headrest", "Headrest", "Kopfstütze", true);

        public static IEnumerable<AutoType> Types()
        {
            yield return Axles();
            yield return SteeringWheel();
            yield return Battery();
            yield return EDrive();
            yield return Seat();
            yield return Door();
        }

        // ------------------------------------------------------------------ axles and wheels

        private static double[] AxlePositions(SketchObject o, double front, double rear)
        {
            var n = Math.Max(1, Math.Min(5, o.Int("axles", 2)));
            if (n == 1) return new[] { 0.0 };
            var xs = new List<double> { front };
            for (var i = 0; i < n - 1; i++) xs.Add(rear + i * 7.0);
            return xs.ToArray();
        }

        private static AutoType Axles()
        {
            var b = Vehicles.Car;
            double hb = b.Wb / 2, tw = b.Wb * 0.13, r = b.WheelR;
            void Top(Pen p, SketchObject o)
            {
                var xs = AxlePositions(o, 13.5, -13.5);
                var steer = Geo.DegToRad(Geo.Clamp(o.Num("steer", 0), -40, 40));
                var front = xs.Max();
                if (o.Flag("centerline", true)) p.Line(-b.L / 2 + 1, 0, b.L / 2 - 1, 0, S.Ctr, "axis");
                var yIn = hb - 0.3 - tw;
                foreach (var x in xs)
                {
                    p.Line(x, -yIn, x, yIn, S.Vis, "axle");
                    foreach (var sgn in new[] { -1.0, 1.0 })
                    {
                        var c = new Vec2(x, sgn * (hb - 0.3 - tw / 2));
                        var a = x == front && xs.Length > 1 ? steer : 0;
                        var corners = new[] { new Vec2(-r, -tw / 2), new Vec2(r, -tw / 2), new Vec2(r, tw / 2), new Vec2(-r, tw / 2) }
                            .Select(v => c + v.Rotate(a)).ToList();
                        p.PolyD(corners, S.Opaque, true, AutoTags.Wheel);
                    }
                }
                if (xs.Length > 1)
                {
                    var rear = xs.Where(x => x < front).Max();
                    p.RBox(rear - 1.6, -1.8, rear + 1.6, 1.8, 0.4, S.OpaqueFine, "differential");
                }
            }
            void Side(Pen p, SketchObject o)
            {
                foreach (var x in AxlePositions(o, 13.5, -13.5))
                {
                    Body.Wheel(p, x, b.WheelY, r);
                    p.Line(x - r - 0.8, b.WheelY, x + r + 0.8, b.WheelY, S.Ctr, "axis");
                    p.Line(x, b.WheelY - r - 0.8, x, b.WheelY + r - 0.1, S.Ctr, "axis");
                }
            }
            IEnumerable<Anchor> Anchors(Pen p, SketchObject o, double y)
            {
                var xs = AxlePositions(o, 13.5, -13.5);
                yield return Anchor.Point(AnchorRole.Hub, p.V(xs.Max(), y), "axle.front");
                if (xs.Length > 1) yield return Anchor.Point(AnchorRole.Hub, p.V(xs.Min(), y), "axle.rear");
            }
            return new AutoType("auto.sub.axles", T("Axles and wheels", "Achsen und Räder"), Cat,
                new[] { "axle", "wheel", "chassis", "steering", "track", "wheelbase", "achse", "rad", "räder", "fahrwerk", "radstand", "spurweite" },
                Views.TopV(b.L, b.Wd, Top) with { Anchors = (p, o) => Anchors(p, o, 0) },
                Views.SideV(b.L, b.H, Side) with { Anchors = (p, o) => Anchors(p, o, b.WheelY) })
            {
                Params = new[] { AxleCount, Steer, Centerline },
                CenterHub = true,
                Extra = o => Prm.PlusMinus("steer", 0, -40, 40, 10, false, T("Steer right", "Nach rechts lenken"), T("Steer left", "Nach links lenken"), "param"),
            };
        }

        // ------------------------------------------------------------------ steering wheel

        private static AutoType SteeringWheel()
        {
            void Front(Pen p, SketchObject o)
            {
                p.Circle(0, 0, 5.8, S.Opaque, "rim");
                p.Circle(0, 0, 4.7, S.Fine, "rim");
                p.Poly(S.OpaqueFine, true, -5.0, -0.4, -1.6, -0.6, -1.6, 1.0, -5.0, 0.9);
                p.Poly(S.OpaqueFine, true, 5.0, -0.4, 1.6, -0.6, 1.6, 1.0, 5.0, 0.9);
                p.Poly(S.OpaqueFine, true, -0.7, 1.6, 0.7, 1.6, 0.8, 5.0, -0.8, 5.0);
                p.RBox(-2.1, -1.5, 2.1, 2.1, 0.9, S.OpaqueFine, "hub");
            }
            void Side(Pen p, SketchObject o)
            {
                var c = new Vec2(-1.5, -0.6);
                var a = Geo.DegToRad(20);
                p.PolyD(new[] { new Vec2(c.X + 0.5, c.Y), new Vec2(3.8, 5.6), new Vec2(2.6, 5.9), new Vec2(c.X - 0.5, c.Y + 0.6) }, S.OpaqueFine, true);
                var rim = Geo.ArcPoints(Vec2.Zero, 0.8, 5.4, 0, 2 * Math.PI, 40).Take(40).Select(v => c + v.Rotate(-a)).ToList();
                p.PolyD(rim, S.Opaque, true, "rim");
                p.Line(c + new Vec2(0, -1.6).Rotate(-a), c + new Vec2(0, 1.6).Rotate(-a), S.Fine, "hub");
            }
            void Top(Pen p, SketchObject o)
            {
                p.Box(-2.4, -0.7, 5.8, 0.7, S.OpaqueFine, "column");
                p.Ellipse(-3.6, 0, 1.6, 5.7, S.Opaque, "rim");
                p.Ellipse(-3.6, 0, 0.7, 4.6, S.Fine, "rim");
                p.RBox(-3.4, -1.8, -1.6, 1.8, 0.5, S.OpaqueFine, "hub");
            }
            return new AutoType("auto.sub.steering", T("Steering wheel", "Lenkrad"), Cat,
                new[] { "steering wheel", "steering", "hands on", "lenkrad", "lenkung", "hands-on" },
                Views.FrontV(12, 12, Front) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(0, 0), "hub") } },
                Views.SideV(9, 12, Side) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(-1.5, -0.6), "hub") } },
                Views.TopV(12, 12, Top));
        }

        // ------------------------------------------------------------------ battery pack

        private static AutoType Battery()
        {
            void Top(Pen p, SketchObject o)
            {
                var n = Math.Max(1, Math.Min(16, o.Int("modules", 6)));
                p.RBox(-15, -7, 15, 7, 1.0, S.Opaque, AutoTags.Body);
                double x0 = -13.6, x1 = 12.0, w = (x1 - x0) / n;
                for (var i = 0; i < n; i++)
                    for (var row = 0; row < 2; row++)
                        p.Box(x0 + i * w + 0.3, row == 0 ? -5.8 : 0.4, x0 + (i + 1) * w - 0.3, row == 0 ? -0.4 : 5.8, S.Fine, "module");
                p.Box(12.8, -3.6, 14.2, -1.4, S.Fine, "terminal");
                p.Box(12.8, 1.4, 14.2, 3.6, S.Fine, "terminal");
                p.Line(13.1, -2.5, 13.9, -2.5, S.Thin, "plus");
                p.Line(13.5, -2.9, 13.5, -2.1, S.Thin, "plus");
                p.Line(13.1, 2.5, 13.9, 2.5, S.Thin, "minus");
            }
            void Side(Pen p, SketchObject o)
            {
                var n = Math.Max(1, Math.Min(16, o.Int("modules", 6)));
                p.RBox(-15, -1.5, 15, 1.5, 0.5, S.Opaque, AutoTags.Body);
                double x0 = -13.6, x1 = 12.0, w = (x1 - x0) / n;
                for (var i = 1; i < n; i++) p.Line(x0 + i * w, -1.0, x0 + i * w, 1.0, S.Thin, "module");
            }
            return new AutoType("auto.sub.battery", T("Battery pack", "Batteriepack"), Cat,
                new[] { "battery", "traction battery", "hv battery", "cells", "ev", "batterie", "akku", "traktionsbatterie", "hochvoltspeicher" },
                Views.TopV(30, 14, Top),
                Views.SideV(30, 3, Side))
            {
                Params = new[] { Modules }.Concat(DynValues.RangeParams).ToList(),
                Uniform = false,
                CenterHub = true,
                // Range from the usable energy and the consumption per 100 km, checked against a planned trip (DynValues).
                ValueVars = DynValues.RangeVars,
                ValueRelations = DynValues.RangeRelations,
                Checks = DynValues.RangeChecks,
                Extra = o => Prm.PlusMinus("modules", 6, 1, 16, 1, false, T("More modules", "Mehr Module"), T("Fewer modules", "Weniger Module"), "param"),
            };
        }

        // ------------------------------------------------------------------ e-drive unit

        private static AutoType EDrive()
        {
            void Top(Pen p, SketchObject o)
            {
                p.Line(3.0, -9, 3.0, 9, S.Vis, "shaft");
                foreach (var sgn in new[] { -1.0, 1.0 }) p.Round(3.0, sgn * 7.6, 0.8, S.OpaqueFine, "joint");
                p.Box(-5.5, -4.0, 1.0, 4.0, S.Opaque, "motor");
                for (var i = 1; i <= 3; i++) p.Line(-5.5 + i * 1.6, -4.0, -5.5 + i * 1.6, 4.0, S.Thin, "fins");
                p.RBox(1.0, -3.0, 5.4, 5.6, 0.8, S.Opaque, "gearbox");
                if (o.Flag("inverter", true))
                {
                    p.Box(-5.5, -8.4, -0.5, -4.8, S.OpaqueFine, "inverter");
                    p.PolyD(Enumerable.Range(0, 17).Select(i => new Vec2(-4.6 + i * 3.2 / 16, -6.6 - 0.8 * Math.Sin(i * 2 * Math.PI / 16))), S.Thin, false, "inverter");
                }
            }
            void Side(Pen p, SketchObject o)
            {
                p.Rounded(new[] { new Vec2(0.4, -3.2), new Vec2(5.6, -1.6), new Vec2(5.6, 4.2), new Vec2(0.4, 4.6) }, 1.0, S.Opaque, "gearbox");
                p.Circle(-1.8, -0.2, 3.8, S.Opaque, "motor");
                p.Circle(-1.8, -0.2, 1.2, S.Fine, "motor");
                p.Circle(3.2, 1.6, 1.2, S.OpaqueFine, "output");
                p.Line(1.6, 1.6, 4.8, 1.6, S.Ctr, "axis");
                p.Line(3.2, 0.0, 3.2, 3.2, S.Ctr, "axis");
                if (o.Flag("inverter", true)) p.Box(-5.0, -4.9, 1.4, -4.0, S.OpaqueFine, "inverter");
            }
            return new AutoType("auto.sub.edrive", T("E-drive unit", "E-Antriebseinheit"), Cat,
                new[] { "e-drive", "electric motor", "edu", "e-axle", "powertrain", "elektromotor", "e-antrieb", "e-achse", "antrieb" },
                Views.TopV(12, 18, Top) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(3.0, 0), "output"), Anchor.Segment(AnchorRole.Axis, p.V(3.0, -9), p.V(3.0, 9)) } },
                Views.SideV(12, 10, Side) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(3.2, 1.6), "output") } })
            {
                Params = new[] { Inverter },
                Extra = o => new[] { Prm.Toggle(o, "inverter", true, T("Inverter", "Wechselrichter")) },
            };
        }

        // ------------------------------------------------------------------ seat

        private static AutoType Seat()
        {
            void Side(Pen p, SketchObject o)
            {
                p.Line(-2.4, 3.9, -3.0, 6.5, S.Fine, "base");
                p.Line(2.8, 3.6, 3.4, 6.5, S.Fine, "base");
                p.Line(-4.2, 6.5, 4.6, 6.5, S.Vis, "rail");
                p.Rounded(new[] { new Vec2(-4.4, 2.4), new Vec2(-3.6, -4.5), new Vec2(-1.6, -4.4), new Vec2(-2.0, 2.0) }, 0.9, S.Opaque, "backrest");
                p.Rounded(new[] { new Vec2(-4.0, 1.4), new Vec2(4.6, 0.8), new Vec2(4.9, 2.6), new Vec2(-3.8, 3.9) }, 0.8, S.Opaque, "cushion");
                if (o.Flag("headrest", true))
                {
                    p.Line(-2.8, -4.5, -3.0, -5.1, S.Fine, "headrest");
                    p.RBox(-4.1, -6.5, -1.9, -5.0, 0.6, S.OpaqueFine, "headrest");
                }
            }
            void Top(Pen p, SketchObject o)
            {
                p.RBox(-2.6, -3.8, 4.8, 3.8, 1.0, S.Opaque, "cushion");
                p.RBox(-4.8, -4.2, -2.0, 4.2, 0.9, S.Opaque, "backrest");
                if (o.Flag("headrest", true)) p.RBox(-4.6, -2.0, -3.2, 2.0, 0.5, S.OpaqueFine, "headrest");
            }
            void Front(Pen p, SketchObject o)
            {
                p.Line(-2.8, 4.2, -3.2, 6.5, S.Fine, "base");
                p.Line(2.8, 4.2, 3.2, 6.5, S.Fine, "base");
                if (o.Flag("headrest", true))
                {
                    p.Line(-1.0, -4.8, -1.0, -4.0, S.Fine, "headrest");
                    p.Line(1.0, -4.8, 1.0, -4.0, S.Fine, "headrest");
                    p.RBox(-2.2, -6.5, 2.2, -4.8, 0.7, S.OpaqueFine, "headrest");
                }
                p.RBox(-3.9, -4.2, 3.9, 3.0, 1.2, S.Opaque, "backrest");
                p.RBox(-4.4, 2.0, 4.4, 4.4, 0.8, S.Opaque, "cushion");
            }
            return new AutoType("auto.sub.seat", T("Seat", "Sitz"), Cat,
                new[] { "seat", "driver seat", "occupant", "sitz", "fahrersitz", "sitzplatz", "insasse" },
                Views.SideV(10, 13, Side) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(-2.0, 1.6), "hpoint") } },
                Views.TopV(10, 9, Top) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(-1.4, 0), "hpoint") } },
                Views.FrontV(9, 13, Front))
            {
                Params = new[] { Headrest },
                Extra = o => new[] { Prm.Toggle(o, "headrest", true, T("Headrest", "Kopfstütze")) },
            };
        }

        // ------------------------------------------------------------------ door

        private static AutoType Door()
        {
            void Side(Pen p, SketchObject o)
            {
                p.Rounded(new[] { new Vec2(-5.8, -0.3), new Vec2(5.0, -0.6), new Vec2(5.8, 4.6), new Vec2(-5.6, 4.8) }, 0.8, S.Opaque, AutoTags.Body);
                p.Rounded(new[] { new Vec2(-5.8, -0.3), new Vec2(-5.4, -4.8), new Vec2(1.6, -4.8), new Vec2(5.0, -0.6) }, 0.8, S.Opaque, "frame");
                p.Rounded(new[] { new Vec2(-5.0, -0.9), new Vec2(-4.7, -4.1), new Vec2(1.3, -4.1), new Vec2(3.8, -0.9) }, 0.5, S.Fine, AutoTags.Glass);
                p.Line(-4.6, 0.9, -2.8, 0.9, S.Fine, "handle");
            }
            void Top(Pen p, SketchObject o)
            {
                var a = Geo.DegToRad(Geo.Clamp(o.Num("open", 60), 0, 90));
                var hinge = new Vec2(5.0, 5.4);
                const double len = 10.5;
                p.Line(-6, 5.6, 6, 5.6, S.Thin, AutoTags.Body);
                if (a > 0.01)
                {
                    p.Line(hinge, hinge + new Vec2(-len, 0), S.Phantom, "closed");
                    p.PolyD(Geo.ArcPoints(hinge, len, len, Math.PI, a, 24), S.Thin, false, "swing");
                }
                var d = new Vec2(-Math.Cos(a), -Math.Sin(a));
                var n = d.Perp * 0.9;
                p.PolyD(new[] { hinge, hinge + d * len, hinge + d * len + n, hinge + n }, S.Opaque, true, "door");
                p.Round(hinge.X, hinge.Y, 0.35, S.Black, "hinge");
            }
            return new AutoType("auto.sub.door", T("Door", "Tür"), Cat,
                new[] { "door", "vehicle door", "opening", "tür", "fahrzeugtür", "türöffnung" },
                Views.SideV(12, 10, Side),
                Views.TopV(12, 12, Top) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(5.0, 5.4), "hinge") } })
            {
                Params = new[] { Open },
                Extra = o => Prm.PlusMinus("open", 60, 0, 90, 15, false, T("Open further", "Weiter öffnen"), T("Close further", "Weiter schließen"), "param"),
            };
        }
    }
}
