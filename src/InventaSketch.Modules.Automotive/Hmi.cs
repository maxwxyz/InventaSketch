using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// HMI elements: instrument cluster, centre display, head-up display (optical path and virtual image), touch button,
    /// rotary knob, slider, warning icon placeholders and screen wireframe blocks. Generic, brand-neutral outlines.
    /// </summary>
    internal static class Hmi
    {
        public const string Cat = AutoCategories.Hmi;

        private static I18nText T(string en, string de) => new(en, de);

        public static IEnumerable<AutoType> Types()
        {
            yield return Cluster();
            yield return Display();
            yield return Hud();
            yield return Button();
            yield return Knob();
            yield return Slider();
            yield return Warning();
            yield return Wireframe();
        }

        private static Vec2 Pol(double cx, double cy, double r, double deg) => new(cx + r * Math.Cos(Geo.DegToRad(deg)), cy + r * Math.Sin(Geo.DegToRad(deg)));

        // ------------------------------------------------------------------ instrument cluster

        private static void Gauge(Pen p, double cx, double cy, double r, double value)
        {
            p.Circle(cx, cy, r, S.Fine, "gauge");
            for (var i = 0; i <= 10; i++)
            {
                var a = 135 + i * 27;
                p.Line(Pol(cx, cy, r * 0.82, a), Pol(cx, cy, r * 0.95, a), i % 5 == 0 ? S.Fine : S.Thin, "scale");
            }
            p.Line(new Vec2(cx, cy), Pol(cx, cy, r * 0.72, 135 + 270 * Geo.Clamp(value, 0, 1)), S.Vis, "needle");
            p.Round(cx, cy, 0.9, S.Black, "pivot");
        }

        private static AutoType Cluster()
        {
            var layout = Prm.Choice("layout", "Layout", "Gestaltung", "analog", ("analog", "Two dials", "Zwei Rundinstrumente"), ("digital", "Digital", "Digital"));
            var value = Prm.Num("value", "Needle position", "Zeigerstellung", 0.35, 0, 1, null, 0.05);
            var speed = Prm.Text("speed", "Speed text", "Geschwindigkeitstext", "50");
            void Draw(Pen p, SketchObject o)
            {
                p.Rounded(new[] { new Vec2(-45, -10), new Vec2(-30, -17), new Vec2(30, -17), new Vec2(45, -10), new Vec2(45, 17), new Vec2(-45, 17) }, 5, S.Vis, "frame");
                p.RBox(-42, -12, 42, 14.5, 3, S.Fine, "screen");
                if (o.Str("layout", "analog") == "digital")
                {
                    p.Text(0, -1, o.Str("speed", "50"), 12, 24, 13, bold: true);
                    p.Text(0, 8, "km/h", 3, 12, 4);
                    for (var i = 0; i < 8; i++) p.Box(-38 + i * 2.6, 9 - i * 1.4, -36.4 + i * 2.6, 11, S.Fine, "bar");
                    p.RBox(20, -8, 38, 10, 1.5, S.Thin, "panel");
                    return;
                }
                var v = Geo.Clamp(o.Num("value", 0.35), 0, 1);
                Gauge(p, -27, 1.5, 11, v);
                Gauge(p, 27, 1.5, 11, 1 - v);
                p.RBox(-12, -8.5, 12, 11, 1.5, S.Fine, "display");
                p.Text(0, -3, o.Str("speed", "50"), 6, 20, 7, bold: true);
                p.Line(-8, 4, 8, 4, S.Thin, "content");
                p.Line(-8, 7, 4, 7, S.Thin, "content");
            }
            return new AutoType("auto.hmi.cluster", T("Instrument cluster", "Kombiinstrument"), Cat,
                new[] { "instrument cluster", "dashboard", "speedometer", "gauges", "kombiinstrument", "tacho", "anzeige", "armaturen" },
                Views.FrontV(90, 34, Draw))
            {
                Params = new[] { layout, value, speed },
                Extra = o => new[] { Prm.Cycle(o, layout, T("Dials / digital", "Rundinstrumente / digital")) },
            };
        }

        // ------------------------------------------------------------------ centre display

        private static AutoType Display()
        {
            var content = Prm.Choice("content", "Content", "Inhalt", "tiles", ("tiles", "Tiles", "Kacheln"), ("map", "Map", "Karte"), ("none", "Empty", "Leer"));
            void Front(Pen p, SketchObject o)
            {
                p.RBox(-25, -16, 25, 16, 2.5, S.Vis, "bezel");
                p.Box(-22.5, -13.5, 22.5, 13.5, S.Fine, "screen");
                switch (o.Str("content", "tiles"))
                {
                    case "map":
                        p.Poly(S.Thin, false, -22.5, 6, -8, 2, 4, 5, 22.5, -4);
                        p.Poly(S.Thin, false, -6, -13.5, -4, 0, -8, 13.5);
                        p.Poly(S.Thin, false, 10, -13.5, 8, 3.5, 12, 13.5);
                        p.Poly(S.Fine, false, -4, 12, -5, 2.5, 2, -2, 6, -8);
                        p.Poly(S.Black, true, -4, 8.2, -2.2, 12.6, -4, 11.4, -5.8, 12.6);
                        break;
                    case "tiles":
                        p.Box(-21, -12, 21, -8, S.Thin, "statusbar");
                        foreach (var (x, y) in new[] { (-21.0, -6.5), (0.5, -6.5), (-21.0, 3.5), (0.5, 3.5) })
                            p.RBox(x, y, x + 20.5, y + 8.5, 1.2, S.Fine, "tile");
                        break;
                }
            }
            void Side(Pen p, SketchObject o)
            {
                p.Rounded(new[] { new Vec2(-1.2, -16), new Vec2(2.4, -15.4), new Vec2(1.2, 16), new Vec2(-2.4, 15.4) }, 0.8, S.Opaque, "body");
                p.Line(-0.8, -12.5, -1.9, 12.5, S.Thin, "screen");
            }
            return new AutoType("auto.hmi.display", T("Centre display", "Zentraldisplay"), Cat,
                new[] { "display", "centre display", "center display", "infotainment", "touchscreen", "screen", "zentraldisplay", "bildschirm", "infotainment" },
                Views.FrontV(50, 32, Front),
                Views.SideV(6, 32, Side))
            {
                Params = new[] { content },
                Extra = o => new[] { Prm.Cycle(o, content, T("Next content", "Nächster Inhalt")) },
            };
        }

        // ------------------------------------------------------------------ head-up display

        private static AutoType Hud()
        {
            var eye = new Vec2(-38, -8);
            var ws0 = new Vec2(22, 5); var ws1 = new Vec2(6, -20);
            void Side(Pen p, SketchObject o)
            {
                // windshield, dashboard, HUD unit
                p.Line(ws0, ws1, S.Vis, "windshield");
                p.Line(ws0 + new Vec2(0.9, 0.2), ws1 + new Vec2(0.9, 0.2), S.Thin, "windshield");
                p.Poly(S.Vis, false, -30, 6, 0, 6, 8, 4, 22, 5);
                p.RBox(4, 9, 20, 16, 1.0, S.Fine, "unit");
                p.Line(8, 13.5, 15, 11, S.Fine, "mirror");
                // optical path: unit -> windshield -> eye, virtual image beyond the windshield
                var w = new Vec2(16.24, -4);
                p.Line(new Vec2(11.5, 12.2), w, S.Thin with { ArrowEnd = ArrowHead.Open, ArrowSize = ArrowSize.Small }, "ray");
                p.Line(w, eye, S.Thin with { ArrowEnd = ArrowHead.Open, ArrowSize = ArrowSize.Small }, "ray");
                var img0 = new Vec2(38, -6.4); var img1 = new Vec2(38, 1.6);
                p.Line(eye, img0, Style.Helper, "virtual");
                p.Line(eye, img1, Style.Helper, "virtual");
                p.Line(img0, img1, S.Phantom with { StrokeWidth = 0.5 }, "image");
                // eye symbol
                p.PolyD(new[] { eye + new Vec2(-1.8, 0), eye + new Vec2(0, -1.0), eye + new Vec2(1.8, 0), eye + new Vec2(0, 1.0) }, S.OpaqueFine, true, "eye");
                p.Round(eye.X + 0.3, eye.Y, 0.45, S.Black, "pupil");
            }
            void Front(Pen p, SketchObject o)
            {
                p.Rounded(new[] { new Vec2(-40, 20), new Vec2(-31, -20), new Vec2(31, -20), new Vec2(40, 20) }, 3, S.Vis, "windshield");
                p.Poly(S.Thin, false, -30, 20, -2, -6);
                p.Poly(S.Thin, false, 30, 20, 2, -6);
                p.Poly(S.Thin with { Dash = DashStyle.Dash }, false, 0, 20, 0, -6);
                p.Line(-36, -6, 36, -6, S.Thin, "horizon");
                p.PolyD(new[] { new Vec2(-17, 13), new Vec2(-11, 1), new Vec2(11, 1), new Vec2(17, 13) }, S.Phantom with { StrokeWidth = 0.5 }, true, "image");
                p.Poly(S.Vis, false, -4, 10.5, 0, 6.5, 4, 10.5);
                p.Poly(S.Vis, false, -3, 7.5, 0, 4.0, 3, 7.5);
                p.Text(11.5, 9.5, o.Str("speed", "50"), 3, 6, 3.4, bold: true);
            }
            return new AutoType("auto.hmi.hud", T("Head-up display", "Head-up-Display"), Cat,
                new[] { "head-up display", "hud", "virtual image", "ar hud", "windshield", "head-up-display", "virtuelles bild", "windschutzscheibe", "projektion" },
                Views.SideV(80, 40, Side) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(eye), "eye"), Anchor.Point(AnchorRole.Port, p.V(38, -2.4), "image") } },
                Views.FrontV(80, 40, Front))
            {
                Params = new[] { Prm.Text("speed", "Speed text", "Geschwindigkeitstext", "50") },
                Uniform = false,
            };
        }

        // ------------------------------------------------------------------ touch button, knob, slider

        private static AutoType Button()
        {
            var pressed = Prm.Bool("pressed", "Pressed", "Gedrückt", false);
            void Draw(Pen p, SketchObject o)
            {
                p.RBox(-10, -5, 10, 5, 2.2, S.Vis, "button");
                if (o.Flag("pressed", false)) p.RBox(-9, -4, 9, 4, 1.6, S.Thin, "pressed");
                p.Text(0, 0, o.Text ?? "OK", 4, 16, 7);
            }
            return new AutoType("auto.hmi.button", T("Touch button", "Touch-Schaltfläche"), Cat,
                new[] { "button", "touch button", "soft key", "control", "schaltfläche", "taste", "softkey", "bedienelement" },
                Views.FrontV(20, 10, Draw))
            {
                Params = new[] { pressed },
                TextEditable = true,
                Uniform = false,
                Extra = o => new[] { Prm.Toggle(o, "pressed", false, T("Pressed", "Gedrückt")) },
            };
        }

        private static AutoType Knob()
        {
            var position = Prm.Num("position", "Pointer angle", "Zeigerwinkel", 30, -150, 150, "°", 15);
            void Front(Pen p, SketchObject o)
            {
                p.Circle(0, 0, 5.8, S.Vis, "knob");
                for (var i = 0; i < 24; i++)
                {
                    var a = i * 15.0;
                    p.Line(Pol(0, 0, 5.0, a), Pol(0, 0, 5.8, a), S.Thin, "grip");
                }
                p.Circle(0, 0, 4.2, S.Fine, "cap");
                var ang = -90 + Geo.Clamp(o.Num("position", 30), -150, 150);
                p.Line(Pol(0, 0, 1.4, ang), Pol(0, 0, 3.6, ang), S.Vis with { StrokeWidth = 0.7 }, "pointer");
            }
            void Side(Pen p, SketchObject o)
            {
                p.Box(-6, 3, 6, 4, S.Fine, "panel");
                p.RBox(-5, -4, 5, 3, 1.0, S.Opaque, "knob");
                for (var i = 1; i < 10; i++) p.Line(-5 + i, -2.6, -5 + i, 1.8, S.Thin, "grip");
            }
            return new AutoType("auto.hmi.knob", T("Rotary knob", "Drehsteller"), Cat,
                new[] { "rotary knob", "knob", "dial", "controller", "drehsteller", "drehknopf", "drehregler", "bedienelement" },
                Views.FrontV(12, 12, Front),
                Views.SideV(12, 8, Side))
            {
                Params = new[] { position },
                Extra = o => Prm.PlusMinus("position", 30, -150, 150, 30, false, T("Turn clockwise", "Im Uhrzeigersinn drehen"), T("Turn counter-clockwise", "Gegen den Uhrzeigersinn drehen"), "param"),
            };
        }

        private static AutoType Slider()
        {
            var value = Prm.Num("value", "Value", "Wert", 0.6, 0, 1, null, 0.05);
            var ticks = Prm.Bool("ticks", "Scale ticks", "Skalenstriche", false);
            void Draw(Pen p, SketchObject o)
            {
                var horizontal = p.W >= p.H;
                double len = horizontal ? p.W : p.H, thick = horizontal ? p.H : p.W;
                var r = thick * 0.35;
                var t = thick * 0.2;
                var v = Geo.Clamp(o.Num("value", 0.6), 0, 1);
                double a = -len / 2 + r, b = len / 2 - r;
                Vec2 M(double along, double across) => horizontal ? new Vec2(along, across) : new Vec2(across, -along);
                var track = new[] { M(a - t, -t / 2), M(b + t, -t / 2), M(b + t, t / 2), M(a - t, t / 2) };
                p.Add(Prim.Polygon(track, S.Fine), "track");
                var pos = a + (b - a) * v;
                p.Add(Prim.Polygon(new[] { M(a - t, -t / 2), M(pos, -t / 2), M(pos, t / 2), M(a - t, t / 2) }, S.Fine with { Fill = Fill.Hatched(new HatchPattern(45, Math.Max(0.4, t * 0.5))) }), "value");
                if (o.Flag("ticks", false))
                    for (var i = 0; i <= 10; i++)
                    {
                        var x = a + (b - a) * i / 10;
                        p.Add(Prim.Line(M(x, thick * 0.3), M(x, thick * 0.48), S.Thin), "tick");
                    }
                var c = M(pos, 0);
                p.Add(Prim.Circle(c.X, c.Y, r, S.OpaqueFine), "thumb");
            }
            return new AutoType("auto.hmi.slider", T("Slider", "Schieberegler"), Cat,
                new[] { "slider", "control", "volume", "temperature", "schieberegler", "regler", "bedienelement" },
                Views.FrontV(40, 8, Draw) with { Fluid = true })
            {
                Params = new[] { value, ticks },
                Uniform = false,
                Extra = o => Prm.PlusMinus("value", 0.6, 0, 1, 0.1, false, T("Increase value", "Wert erhöhen"), T("Decrease value", "Wert verringern"), "param"),
            };
        }

        // ------------------------------------------------------------------ warning icon and wireframe

        private static AutoType Warning()
        {
            var shape = Prm.Choice("shape", "Shape", "Form", "triangle", ("triangle", "Triangle", "Dreieck"), ("circle", "Circle", "Kreis"), ("square", "Square", "Quadrat"));
            void Draw(Pen p, SketchObject o)
            {
                double barTop = -2.6, barBottom = 1.6, dotY = 3.3;
                switch (o.Str("shape", "triangle"))
                {
                    case "circle":
                        p.Circle(0, 0, 5.5, S.Vis, "outline");
                        barTop = -3.2; barBottom = 1.0; dotY = 2.8;
                        break;
                    case "square":
                        p.RBox(-5.5, -5.5, 5.5, 5.5, 1.2, S.Vis, "outline");
                        barTop = -3.4; barBottom = 1.0; dotY = 2.8;
                        break;
                    default:
                        p.Rounded(new[] { new Vec2(0, -5.5), new Vec2(6, 5.0), new Vec2(-6, 5.0) }, 1.0, S.Vis, "outline");
                        p.Rounded(new[] { new Vec2(0, -3.6), new Vec2(4.4, 4.0), new Vec2(-4.4, 4.0) }, 0.6, S.Thin, "outline");
                        break;
                }
                p.Rounded(new[] { new Vec2(-0.6, barTop), new Vec2(0.6, barTop), new Vec2(0.35, barBottom), new Vec2(-0.35, barBottom) }, 0.2, S.Black, "mark");
                p.Round(0, dotY, 0.55, S.Black, "mark");
            }
            return new AutoType("auto.hmi.warning", T("Warning icon", "Warnsymbol"), Cat,
                new[] { "warning", "alert", "icon", "telltale", "caution", "warnung", "warnsymbol", "kontrollleuchte", "hinweis" },
                Views.SymbolV(12, 11, Draw))
            {
                Params = new[] { shape },
                Extra = o => new[] { Prm.Cycle(o, shape, T("Next icon shape", "Nächste Symbolform")) },
            };
        }

        private static AutoType Wireframe()
        {
            var kind = Prm.Choice("kind", "Block", "Block", "image",
                ("image", "Image placeholder", "Bildplatzhalter"), ("text", "Text block", "Textblock"), ("list", "List", "Liste"), ("map", "Map", "Karte"));
            void Draw(Pen p, SketchObject o)
            {
                double hw = p.W / 2, hh = p.H / 2;
                p.Add(Prim.Rect(-hw, -hh, p.W, p.H, S.Fine), "frame");
                var m = Math.Min(p.W, p.H) * 0.1;
                switch (o.Str("kind", "image"))
                {
                    case "text":
                        {
                            var lh = Math.Max(1.0, Math.Min(3.0, (p.H - 2 * m) / 6));
                            p.Add(Prim.Rect(-hw + m, -hh + m, p.W * 0.5, lh, S.Thin with { Fill = Fill.Hatched(new HatchPattern(45, 0.6)) }), "title");
                            for (var y = -hh + m + 2 * lh; y + 0.4 < hh - m; y += lh * 1.2)
                                p.Add(Prim.Line(new Vec2(-hw + m, y), new Vec2(hw - m - (y > hh - m - 2 * lh ? p.W * 0.3 : 0), y), S.Thin), "line");
                            break;
                        }
                    case "list":
                        {
                            const int rows = 4;
                            var rh = (p.H - 2 * m) / rows;
                            for (var i = 0; i < rows; i++)
                            {
                                var y = -hh + m + i * rh;
                                if (i > 0) p.Add(Prim.Line(new Vec2(-hw + m, y), new Vec2(hw - m, y), S.Thin), "separator");
                                var r = Math.Min(rh * 0.25, p.W * 0.05);
                                p.Add(Prim.Circle(-hw + m + r, y + rh / 2, r, S.Thin), "bullet");
                                p.Add(Prim.Line(new Vec2(-hw + m + 3 * r, y + rh / 2), new Vec2(hw - m - p.W * 0.15, y + rh / 2), S.Thin), "line");
                            }
                            break;
                        }
                    case "map":
                        for (var i = 1; i < 4; i++)
                        {
                            p.Add(Prim.Line(new Vec2(-hw + p.W * i / 4, -hh), new Vec2(-hw + p.W * i / 4, hh), Style.Helper), "grid");
                            p.Add(Prim.Line(new Vec2(-hw, -hh + p.H * i / 4), new Vec2(hw, -hh + p.H * i / 4), Style.Helper), "grid");
                        }
                        p.Add(Prim.Polyline(new[] { new Vec2(-hw + m, hh - m), new Vec2(-hw + p.W * 0.35, p.H * 0.05), new Vec2(p.W * 0.1, -p.H * 0.1), new Vec2(hw - m, -hh + m) }, S.Vis), "route");
                        p.Add(Prim.Circle(hw - m, -hh + m, Math.Min(p.W, p.H) * 0.04, S.Black), "destination");
                        break;
                    default:
                        p.Add(Prim.Line(new Vec2(-hw, -hh), new Vec2(hw, hh), S.Thin), "cross");
                        p.Add(Prim.Line(new Vec2(-hw, hh), new Vec2(hw, -hh), S.Thin), "cross");
                        break;
                }
            }
            return new AutoType("auto.hmi.wireframe", T("Screen wireframe block", "Bildschirm-Wireframe-Block"), Cat,
                new[] { "wireframe", "screen layout", "placeholder", "mockup", "ui", "wireframe", "platzhalter", "bildschirmaufbau", "layout" },
                Views.FrontV(40, 30, Draw) with { Fluid = true })
            {
                Params = new[] { kind },
                Uniform = false,
                Extra = o => new[] { Prm.Cycle(o, kind, T("Next block kind", "Nächste Blockart")) },
            };
        }
    }
}
