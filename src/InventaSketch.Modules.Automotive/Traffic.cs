using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>Traffic infrastructure and scenery: traffic light, generic traffic sign, barrier, parking slot, building, obstacle.</summary>
    internal static class Traffic
    {
        public const string Cat = AutoCategories.Traffic;

        private static I18nText T(string en, string de) => new(en, de);

        public static IEnumerable<AutoType> Types()
        {
            yield return TrafficLight();
            yield return Sign();
            yield return Barrier();
            yield return Parking();
            yield return Building();
            yield return Obstacle();
        }

        // ------------------------------------------------------------------ traffic light

        private static AutoType TrafficLight()
        {
            var state = Prm.Choice("state", "Signal", "Signal", "red",
                ("red", "Red", "Rot"), ("amber", "Amber", "Gelb"), ("green", "Green", "Grün"), ("off", "Off", "Aus"));
            var pole = Prm.Bool("pole", "Pole", "Mast", true);
            Size2 Design(SketchObject o) => o.Flag("pole", true) ? new Size2(8, 24) : new Size2(8, 13);
            void Front(Pen p, SketchObject o)
            {
                var d = Design(o);
                double y0 = -d.H / 2, y1 = y0 + 13;
                if (o.Flag("pole", true))
                {
                    p.Box(-0.5, y1, 0.5, d.H / 2 - 0.6, S.Fine, "pole");
                    p.Box(-2, d.H / 2 - 0.6, 2, d.H / 2, S.Fine, "base");
                }
                p.RBox(-3, y0, 3, y1, 1.0, S.Opaque, "housing");
                var lit = o.Str("state", "red");
                var names = new[] { "red", "amber", "green" };
                for (var i = 0; i < 3; i++)
                {
                    var cy = y0 + 2.6 + i * 3.9;
                    var on = names[i] == lit;
                    p.Arc(0, cy, 1.9, 1.9, 200, 140, S.Thin, "visor");
                    p.Circle(0, cy, 1.5, on ? S.Fine with { Fill = Fill.Dotted(0.55) } : S.Fine, on ? "lamp.on" : "lamp");
                    if (on)
                        foreach (var a in new[] { -20.0, 20.0, 160.0, 200.0 })
                        {
                            var u = new Vec2(Math.Cos(Geo.DegToRad(a)), Math.Sin(Geo.DegToRad(a)));
                            p.Line(u.X * 2.3, cy + u.Y * 2.3, u.X * 3.7, cy + u.Y * 3.7, S.Thin, "ray");
                        }
                }
            }
            void Top(Pen p, SketchObject o)
            {
                if (o.Flag("pole", true)) p.Round(-1.8, 0, 0.8, S.Fine, "pole");
                p.RBox(-0.8, -1.8, 1.4, 1.8, 0.4, S.Opaque, "housing");
                p.Poly(S.Fine, false, 1.4, -1.2, 2.6, -1.6);
                p.Poly(S.Fine, false, 1.4, 1.2, 2.6, 1.6);
            }
            return new AutoType("auto.traffic.light", T("Traffic light", "Ampel"), Cat,
                new[] { "traffic light", "signal", "traffic signal", "ampel", "lichtsignalanlage", "lichtzeichen" },
                Views.FrontV(8, 24, Front) with { DesignOf = Design },
                Views.TopV(6, 4, Top))
            {
                Params = new[] { state, pole },
                Extra = o => new[] { Prm.Cycle(o, state, T("Next signal state", "Nächster Signalzustand")), Prm.Toggle(o, "pole", true, T("Pole", "Mast")) },
            };
        }

        // ------------------------------------------------------------------ traffic sign

        private static string SignText(SketchObject o) => o.Text ?? (o.Str("shape", "circle") switch
        {
            "triangle" => "!",
            "rectangle" => "P",
            "octagon" => "STOP",
            _ => "50",
        });

        private static List<Vec2> Ngon(double cx, double cy, double r, int n, double startDeg)
            => Enumerable.Range(0, n).Select(i => new Vec2(cx + r * Math.Cos(Geo.DegToRad(startDeg + i * 360.0 / n)), cy + r * Math.Sin(Geo.DegToRad(startDeg + i * 360.0 / n)))).ToList();

        private static AutoType Sign()
        {
            var shape = Prm.Choice("shape", "Shape", "Form", "circle",
                ("circle", "Circle", "Kreis"), ("triangle", "Triangle", "Dreieck"), ("rectangle", "Rectangle", "Rechteck"), ("octagon", "Octagon", "Achteck"));
            var pole = Prm.Bool("pole", "Pole", "Pfosten", true);
            Size2 Design(SketchObject o) => o.Flag("pole", true) ? new Size2(14, 26) : new Size2(14, 14);
            void Front(Pen p, SketchObject o)
            {
                var d = Design(o);
                var cy = -d.H / 2 + 7;
                if (o.Flag("pole", true)) p.Box(-0.45, cy + 5, 0.45, d.H / 2, S.Fine, "pole");
                var text = SignText(o);
                switch (o.Str("shape", "circle"))
                {
                    case "triangle":
                        {
                            var outer = new List<Vec2> { new(0, cy - 6.6), new(6.9, cy + 5.4), new(-6.9, cy + 5.4) };
                            var inner = new List<Vec2> { new(0, cy - 3.9), new(4.6, cy + 4.1), new(-4.6, cy + 4.1) };
                            p.Rounded(outer, 1.0, S.Opaque, "plate");
                            p.Rounded(inner, 0.6, S.Fine, "border");
                            p.Text(0, cy + 1.6, text, 4.0, 4.4, 3.6, bold: true);
                            break;
                        }
                    case "rectangle":
                        p.RBox(-6, cy - 6, 6, cy + 6, 1.2, S.Opaque, "plate");
                        p.RBox(-5, cy - 5, 5, cy + 5, 0.8, S.Fine, "border");
                        p.Text(0, cy, text, 7.0, 9.0, 8.5, bold: true);
                        break;
                    case "octagon":
                        p.PolyD(Ngon(0, cy, 6.6, 8, 22.5), S.Opaque, true, "plate");
                        p.PolyD(Ngon(0, cy, 5.7, 8, 22.5), S.Fine, true, "border");
                        p.Text(0, cy, text, 3.6, 9.0, 5, bold: true);
                        break;
                    default:
                        p.Circle(0, cy, 6.5, S.Opaque, "plate");
                        p.Circle(0, cy, 5.0, S.Fine, "border");
                        p.Text(0, cy, text, 5.0, 8.0, 7, bold: true);
                        break;
                }
            }
            void Top(Pen p, SketchObject o)
            {
                if (o.Flag("pole", true)) p.Round(-0.8, 0, 0.6, S.Fine, "pole");
                p.Box(-0.2, -6.5, 0.6, 6.5, S.Opaque, "plate");
            }
            return new AutoType("auto.traffic.sign", T("Traffic sign", "Verkehrszeichen"), Cat,
                new[] { "traffic sign", "road sign", "speed limit", "stop sign", "warning sign", "verkehrszeichen", "schild", "tempolimit", "warnschild" },
                Views.FrontV(14, 26, Front) with { DesignOf = Design },
                Views.TopV(3, 14, Top))
            {
                Params = new[] { shape, pole },
                TextEditable = true,
                Extra = o => new[] { Prm.Cycle(o, shape, T("Next sign shape", "Nächste Schildform")), Prm.Toggle(o, "pole", true, T("Pole", "Pfosten")) },
            };
        }

        // ------------------------------------------------------------------ barrier

        private static AutoType Barrier()
        {
            var kind = Prm.Choice("kind", "Kind", "Art", "guardrail",
                ("guardrail", "Guardrail", "Schutzplanke"), ("wall", "Concrete wall", "Betonschutzwand"), ("boom", "Boom gate", "Schranke"));
            var posts = Prm.Int("posts", "Posts", "Pfosten", 6, 2, 30);
            int Posts(SketchObject o) => Math.Max(2, Math.Min(30, o.Int("posts", 6)));
            void Top(Pen p, SketchObject o)
            {
                switch (o.Str("kind", "guardrail"))
                {
                    case "wall":
                        p.Box(-30, -1.5, 30, 1.5, S.Hatch(1.0, 45, S.Vis), "wall");
                        break;
                    case "boom":
                        p.Box(-30, -1.5, -27, 1.5, S.Opaque, "post");
                        p.Box(-27, -0.5, 30, 0.5, S.Fine, "arm");
                        break;
                    default:
                        {
                            var n = Posts(o);
                            for (var i = 0; i < n; i++)
                            {
                                var x = -29.4 + i * 58.8 / (n - 1);
                                p.Box(x - 0.5, 0.2, x + 0.5, 1.4, S.Fine, "post");
                            }
                            p.Box(-30, -1.5, 30, -0.1, S.Opaque, "rail");
                            break;
                        }
                }
            }
            void Side(Pen p, SketchObject o)
            {
                switch (o.Str("kind", "guardrail"))
                {
                    case "wall":
                        p.Box(-30, -3, 30, 4, S.Opaque, "wall");
                        for (var i = 1; i < 6; i++) p.Line(-30 + i * 10, -3, -30 + i * 10, 4, S.Thin, "joint");
                        break;
                    case "boom":
                        p.Box(-30, -2.5, -26.5, 4, S.Opaque, "post");
                        p.Box(-26.5, -2.4, 30, -0.8, S.Opaque with { StrokeWidth = 0.35 }, "arm");
                        for (var i = 0; i < 7; i++)
                            p.Box(-24 + i * 8, -2.4, -20 + i * 8, -0.8, S.Fine with { Fill = Fill.Hatched(new HatchPattern(45, 0.6)) }, "stripe");
                        break;
                    default:
                        {
                            var n = Posts(o);
                            for (var i = 0; i < n; i++)
                            {
                                var x = -29.4 + i * 58.8 / (n - 1);
                                p.Box(x - 0.5, -3, x + 0.5, 4, S.Fine, "post");
                            }
                            p.Box(-30, -3.6, 30, -0.6, S.Opaque, "rail");
                            p.Line(-30, -2.1, 30, -2.1, S.Thin, "rail");
                            break;
                        }
                }
            }
            return new AutoType("auto.traffic.barrier", T("Barrier", "Absperrung"), Cat,
                new[] { "barrier", "guardrail", "crash barrier", "wall", "boom gate", "absperrung", "leitplanke", "schutzplanke", "schranke", "betonwand" },
                Views.TopV(60, 3, Top),
                Views.SideV(60, 8, Side))
            {
                Params = new[] { kind, posts },
                Uniform = false,
                Extra = o => new[] { Prm.Cycle(o, kind, T("Next barrier kind", "Nächste Absperrungsart")) },
            };
        }

        // ------------------------------------------------------------------ parking slot

        private static AutoType Parking()
        {
            var charging = Prm.Bool("charging", "Charging station", "Ladestation", false);
            void Top(Pen p, SketchObject o)
            {
                p.Line(-25, -12.5, 25, -12.5, S.Vis, AutoTags.Marking);
                p.Line(-25, 12.5, 25, 12.5, S.Vis, AutoTags.Marking);
                p.Line(25, -12.5, 25, 12.5, S.Vis, AutoTags.Marking);
                if (o.Flag("charging", false))
                {
                    p.RBox(20.5, -3.5, 24, 3.5, 0.5, S.Opaque with { StrokeWidth = 0.35 }, "charger");
                    p.Poly(S.Fine with { Fill = Fill.Solid("#000000") }, true, 22.6, -2.6, 21.3, 0.3, 22.3, 0.3, 21.8, 2.6, 23.2, -0.6, 22.2, -0.6);
                }
                if (!string.IsNullOrEmpty(o.Text)) p.Text(-19, 0, o.Text!, 6, 10, 10, bold: true);
            }
            return new AutoType("auto.traffic.parking", T("Parking slot", "Parkstand"), Cat,
                new[] { "parking", "parking slot", "parking space", "charging", "ev charging", "parkplatz", "stellplatz", "parkbucht", "ladestation", "laden" },
                Views.TopV(50, 25, Top) with { Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-25, 0), p.V(25, 0)) with { Id = "slot" } } })
            {
                Params = new[] { charging },
                TextEditable = true,
                Uniform = false,
                Extra = o => new[] { Prm.Toggle(o, "charging", false, T("Charging station", "Ladestation")) },
            };
        }

        // ------------------------------------------------------------------ building

        private static AutoType Building()
        {
            var floors = Prm.Int("floors", "Floors", "Geschosse", 3, 1, 12);
            void Top(Pen p, SketchObject o)
            {
                p.Box(-30, -20, 30, 20, S.Hatch(3.0, 45, S.Vis), AutoTags.Body);
                p.Box(-27, -17, 27, 17, S.Thin with { Fill = Fill.Solid("#ffffff") }, "roof");
            }
            void Side(Pen p, SketchObject o)
            {
                var n = Math.Max(1, Math.Min(12, o.Int("floors", 3)));
                p.Box(-30, -15, 30, 15, S.Opaque, AutoTags.Body);
                p.Line(-30, -13.5, 30, -13.5, S.Thin, "roof");
                var fh = 28.5 / n;
                for (var f = 0; f < n; f++)
                {
                    var y0 = -13.5 + f * fh;
                    for (var i = 0; i < 6; i++)
                    {
                        var x = -26 + i * 9.2;
                        if (f == n - 1 && i == 3) { p.Box(x, y0 + fh * 0.25, x + 4.5, 15, S.Fine, "door"); continue; }
                        p.Box(x, y0 + fh * 0.25, x + 4.5, y0 + fh * 0.75, S.Fine, "window");
                    }
                }
            }
            return new AutoType("auto.traffic.building", T("Building", "Gebäude"), Cat,
                new[] { "building", "house", "block", "occlusion", "gebäude", "haus", "häuserblock", "verdeckung", "bebauung" },
                Views.TopV(60, 40, Top),
                Views.SideV(60, 30, Side))
            {
                Params = new[] { floors },
                Uniform = false,
            };
        }

        // ------------------------------------------------------------------ obstacle

        private static AutoType Obstacle()
        {
            var kind = Prm.Choice("kind", "Kind", "Art", "box", ("box", "Box / object", "Kiste / Objekt"), ("cone", "Traffic cone", "Leitkegel"), ("debris", "Debris", "Trümmer"));
            void Top(Pen p, SketchObject o)
            {
                switch (o.Str("kind", "box"))
                {
                    case "cone":
                        p.Box(-4.5, -4.5, 4.5, 4.5, S.Fine, "base");
                        p.Circle(0, 0, 3.4, S.Fine, "cone");
                        p.Circle(0, 0, 2.2, S.Thin, "stripe");
                        p.Circle(0, 0, 0.8, S.Opaque with { StrokeWidth = 0.35 }, "tip");
                        break;
                    case "debris":
                        p.Rounded(new[] { new Vec2(-4.6, -1.0), new Vec2(-1.5, -4.4), new Vec2(3.2, -3.0), new Vec2(4.6, 1.4), new Vec2(0.8, 4.4), new Vec2(-3.6, 3.0) }, 0.6, S.Hatch(1.0, 45, S.Vis), AutoTags.Body);
                        break;
                    default:
                        p.Box(-4.5, -4.5, 4.5, 4.5, S.Opaque, AutoTags.Body);
                        p.Line(-4.5, -4.5, 4.5, 4.5, S.Thin, "cross");
                        p.Line(-4.5, 4.5, 4.5, -4.5, S.Thin, "cross");
                        break;
                }
            }
            void Side(Pen p, SketchObject o)
            {
                switch (o.Str("kind", "box"))
                {
                    case "cone":
                        p.Box(-4.5, 4.0, 4.5, 5, S.Fine, "base");
                        p.Rounded(new[] { new Vec2(-3.4, 4.0), new Vec2(-0.6, -5), new Vec2(0.6, -5), new Vec2(3.4, 4.0) }, 0.4, S.Opaque, "cone");
                        p.Poly(S.Fine with { Fill = Fill.Hatched(new HatchPattern(45, 0.6)) }, true, -1.9, -1, 1.9, -1, 2.4, 0.8, -2.4, 0.8);
                        break;
                    case "debris":
                        p.Rounded(new[] { new Vec2(-4.8, 5), new Vec2(-3.4, 1.6), new Vec2(-0.6, 0.2), new Vec2(2.4, 1.0), new Vec2(4.8, 5) }, 0.6, S.Hatch(1.0, 45, S.Vis), AutoTags.Body);
                        break;
                    default:
                        p.Box(-4.5, -4.0, 4.5, 5, S.Opaque, AutoTags.Body);
                        p.Line(-4.5, -4.0, 4.5, 5, S.Thin, "cross");
                        p.Line(-4.5, 5, 4.5, -4.0, S.Thin, "cross");
                        break;
                }
            }
            return new AutoType("auto.traffic.obstacle", T("Obstacle", "Hindernis"), Cat,
                new[] { "obstacle", "object", "cone", "debris", "lost cargo", "hindernis", "objekt", "leitkegel", "pylone", "trümmer", "ladung" },
                Views.TopV(10, 10, Top),
                Views.SideV(10, 10, Side))
            {
                Params = new[] { kind },
                Extra = o => new[] { Prm.Cycle(o, kind, T("Next obstacle kind", "Nächste Hindernisart")) },
            };
        }
    }
}
