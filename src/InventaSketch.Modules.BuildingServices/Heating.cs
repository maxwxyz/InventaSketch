using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Heating: boiler, heat pump, radiator, underfloor heating loop, manifold, circulator pump, mixing valve, expansion vessel,
    /// thermostatic valve. Symbols are drawn independently following common drawing practice.
    /// </summary>
    internal static class Heating
    {
        private const string Cat = BsCategories.Heating;

        private static readonly ParamDef Circuits = Pm.Int("circuits", "Heating circuits", "Heizkreise", 4, 1, 12);
        private static readonly ParamDef Spacing = Pm.Num("spacing", "Pipe spacing", "Verlegeabstand", 150, 50, 500, "mm", 25);

        private static readonly string[] Pipe = { PipeType.TypeId };

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.heat.boiler", new I18nText("Boiler (wall-hung)", "Heizkessel / Therme"), Cat,
                new Size2(4.4, 3.5), new Size2(4.4, 7.0), new Size2(4.4, 4.4), BoilerPlan, BoilerSection, BoilerSchematic,
                "boiler", "gas boiler", "combi boiler", "furnace", "heating", "heizkessel", "therme", "gastherme", "brennwert", "heizung", "wärmeerzeuger", "tga")
            {
                Mount = Mounting.Wall, DefaultHeight = 1100,
                Ports = BsBoxType.Uv(new[] { (-0.25, -0.5, "flow"), (0.25, -0.5, "return") }, new[] { (-0.25, 0.5, "flow"), (0.25, 0.5, "return") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.pump", "bs.heat.expansion", "bs.heat.mixer" },
            };

            yield return new BsBoxType("bs.heat.heatpump", new I18nText("Heat pump (outdoor unit)", "Wärmepumpe (Außeneinheit)"), Cat,
                new Size2(10, 4), new Size2(10, 8), new Size2(10, 5), HeatPumpPlan, HeatPumpSection, HeatPumpSchematic,
                "heat pump", "air source", "outdoor unit", "monoblock", "heating", "wärmepumpe", "luftwärmepumpe", "außeneinheit", "heizung", "tga")
            {
                Mount = Mounting.Floor, DefaultHeight = 0,
                Ports = BsBoxType.Uv(new[] { (-0.5, -0.2, "flow"), (-0.5, 0.2, "return") }, new[] { (-0.5, 0.15, "flow"), (-0.5, 0.35, "return") },
                    new[] { (-0.5, -0.25, "flow"), (-0.5, 0.25, "return") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.heater", "bs.heat.pump" },
            };

            yield return new BsBoxType("bs.heat.radiator", new I18nText("Radiator", "Heizkörper"), Cat,
                new Size2(10, 1.2), new Size2(10, 6), new Size2(10, 4), RadiatorPlan, RadiatorSection, RadiatorSchematic,
                "radiator", "panel radiator", "heater", "heating", "heizkörper", "plattenheizkörper", "heizung", "tga")
            {
                Mount = Mounting.Wall, DefaultHeight = 150,
                Ports = BsBoxType.Uv(new[] { (-0.5, 0.0, "flow"), (0.5, 0.0, "return") }, new[] { (-0.45, 0.5, "flow"), (0.45, 0.5, "return") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.trv" },
            };

            yield return new BsBoxType("bs.heat.floorloop", new I18nText("Underfloor heating loop", "Fußbodenheizung (Heizkreis)"), Cat,
                new Size2(30, 30), new Size2(30, 0.6), new Size2(30, 5), FloorLoopPlan, FloorLoopSection, FloorLoopSchematic,
                "underfloor heating", "floor heating", "radiant floor", "heating loop", "fußbodenheizung", "flächenheizung", "heizkreis", "heizschlange", "fbh", "tga")
            {
                Mount = Mounting.InFloor, WallBacked = false, DefaultHeight = -70,
                Params = new[] { Spacing },
                Ports = FloorLoopPorts,
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.manifold" },
            };

            yield return new BsBoxType("bs.heat.manifold", new I18nText("Heating circuit manifold", "Heizkreisverteiler"), Cat,
                new Size2(6, 1.2), new Size2(6, 4), new Size2(6, 3), ManifoldPlan, ManifoldSection, ManifoldSchematic,
                "manifold", "heating manifold", "distributor", "underfloor heating", "heizkreisverteiler", "verteiler", "fbh-verteiler", "tga")
            {
                Mount = Mounting.Wall, DefaultHeight = 400,
                Params = new[] { Circuits },
                Ports = ManifoldPorts,
                Extra = o => new[]
                {
                    new ContextAction("bs.manifold.more", new I18nText("More circuits", "Mehr Heizkreise"), IconIds.CountMore,
                        x => x.WithParam("circuits", Math.Min(12, x.Int("circuits", 4) + 1))) { Group = "circuits", IsEnabled = o.Int("circuits", 4) < 12, ParamKeys = new[] { "circuits" } },
                    new ContextAction("bs.manifold.fewer", new I18nText("Fewer circuits", "Weniger Heizkreise"), IconIds.CountFewer,
                        x => x.WithParam("circuits", Math.Max(1, x.Int("circuits", 4) - 1))) { Group = "circuits", IsEnabled = o.Int("circuits", 4) > 1, ParamKeys = new[] { "circuits" } },
                },
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.floorloop", "bs.heat.pump" },
            };

            yield return new BsBoxType("bs.heat.pump", new I18nText("Circulator pump", "Umwälzpumpe"), Cat,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), PumpPlan, PumpSection, PumpSchematic,
                "pump", "circulator", "circulation pump", "heating pump", "pumpe", "umwälzpumpe", "heizungspumpe", "zirkulationspumpe", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = Pipe,
            };

            yield return new BsBoxType("bs.heat.mixer", new I18nText("Mixing valve (3-way)", "Mischventil (3-Wege)"), Cat,
                new Size2(4, 4), new Size2(4, 4), new Size2(4, 4), MixerPlan, MixerSection, MixerSchematic,
                "mixing valve", "three-way valve", "3-way", "mixer", "mischer", "mischventil", "dreiwegeventil", "3-wege", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Ports = BsBoxType.Same((-0.5, 0, "a"), (0.5, 0, "ab"), (0, 0.5, "b")),
                CompanionIds = Pipe,
            };

            yield return new BsBoxType("bs.heat.expansion", new I18nText("Expansion vessel", "Ausdehnungsgefäß"), Cat,
                new Size2(3.6, 3.6), new Size2(3.6, 5), new Size2(3.6, 4.4), ExpansionPlan, ExpansionSection, ExpansionSchematic,
                "expansion vessel", "expansion tank", "membrane", "ausdehnungsgefäß", "mag", "membranausdehnungsgefäß", "tga")
            {
                Mount = Mounting.Wall, DefaultHeight = 1000, Uniform = true,
                Ports = BsBoxType.Uv(new[] { (0.0, -0.5, "pipe") }, new[] { (0.0, 0.5, "pipe") }),
                CompanionIds = Pipe,
            };

            yield return new BsBoxType("bs.heat.trv", new I18nText("Thermostatic valve", "Thermostatventil"), Cat,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), TrvPlan, TrvSection, TrvSchematic,
                "thermostatic valve", "trv", "radiator valve", "thermostat", "thermostatventil", "heizkörperventil", "thermostatkopf", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 750,
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.radiator" },
            };
        }

        // ------------------------------------------------------------------ boiler

        private static void BoilerPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            // Casing top seen from above and the flue (concentric air/flue pipe) near the wall.
            c.Rect(-c.Hw + 0.06 * c.W, -c.Hh + 0.06 * c.H, c.Hw - 0.06 * c.W, c.Hh - 0.06 * c.H, St.Fine, "casing");
            var r = 0.16 * c.K;
            var fc = new Vec2(0, -c.Hh + 0.12 * c.H + r);
            c.Circle(fc, r, St.Thin, "flue");
            c.Circle(fc, r * 0.55, St.Fine, "flue");
        }

        private static void Flame(Canvas c, Vec2 centre, double w, double h, Style s)
        {
            Vec2 P(double u, double v) => centre + new Vec2(u * w, v * h);
            c.Add(new PathPrim(new[]
            {
                PathSeg.Move(P(0, 0.5)),
                PathSeg.Cubic(P(-0.5, 0.45), P(-0.45, 0), P(-0.1, -0.5)),
                PathSeg.Cubic(P(-0.1, -0.15), P(0.12, -0.12), P(0.1, -0.32)),
                PathSeg.Cubic(P(0.5, 0), P(0.5, 0.45), P(0, 0.5)),
                PathSeg.Close,
            }, s), "flame");
        }

        private static void BoilerSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double top = -0.42 * c.H, bot = 0.42 * c.H;
            // Flue on top, casing, control panel and connection stubs below.
            c.Rect(-0.12 * c.W, -c.Hh, 0.12 * c.W, top, St.Thin, "flue");
            c.RoundRect(-c.Hw, top, c.Hw, bot, 0.05 * c.K, St.Thin, BsTags.Outline);
            c.Rect(-0.3 * c.W, 0.2 * c.H, 0.3 * c.W, 0.32 * c.H, St.Fine, "panel");
            c.Circle(new Vec2(0.18 * c.W, 0.26 * c.H), 0.03 * c.K, St.Fine, "knob");
            Flame(c, new Vec2(0, -0.08 * c.H), 0.36 * c.W, 0.22 * c.H, St.Fine);
            foreach (var u in new[] { -0.25, 0.25 }) c.LineN(u, 0.42, u, 0.5, St.Thin, BsTags.Stub);
        }

        private static void BoilerSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, 0.4 * c.H, St.Medium, BsTags.Outline);
            Flame(c, new Vec2(0, -0.05 * c.H), 0.4 * c.W, 0.55 * c.H, St.Medium);
            foreach (var u in new[] { -0.25, 0.25 }) c.LineN(u, 0.4, u, 0.5, St.Medium, BsTags.Stub);
        }

        // ------------------------------------------------------------------ heat pump

        private static string HpLabel(Lang lang) => lang == Lang.De ? "WP" : "HP";

        private static void HeatPumpPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            // Air outlet grille on the front (room side, bottom edge).
            var y0 = c.Hh - 0.22 * c.H;
            c.Line(new Vec2(-c.Hw, y0), new Vec2(0.25 * c.W, y0), St.Fine, "grille");
            var n = Math.Max(3, (int)Math.Round(0.75 * c.W / 0.8));
            for (var i = 0; i <= n; i++)
            {
                var x = -c.Hw + 0.04 * c.W + i * (0.7 * c.W) / n;
                c.Line(new Vec2(x, y0), new Vec2(x, c.Hh), St.Fine, "grille");
            }
            c.Text(new Vec2(0.37 * c.W, 0), HpLabel(ctx.Lang), 2.5, 0.2 * c.W, 0.5 * c.H);
        }

        private static void HeatPumpSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bot = c.Hh - 0.06 * c.H;
            c.Rect(-c.Hw, -c.Hh, c.Hw, bot, St.Thin, BsTags.Outline);
            foreach (var u in new[] { -0.4, 0.4 }) c.RectN(u - 0.04, 0.44, u + 0.04, 0.5, St.Thin, "foot");
            var r = Math.Min(0.27 * c.W, 0.4 * c.H);
            var fc = new Vec2(-c.Hw + 0.06 * c.W + r, (-c.Hh + bot) / 2);
            c.Circle(fc, r, St.Thin, "fan");
            c.Circle(fc, r * 0.15, St.Fine, "hub");
            for (var i = 0; i < 4; i++)
            {
                var a = Geo.DegToRad(20 + i * 90);
                c.Poly(new[] { fc + Vec2.Polar(r * 0.15, a), fc + Vec2.Polar(r * 0.6, a + 0.55), fc + Vec2.Polar(r * 0.88, a) }, St.Fine, true, "blade");
            }
            var x0 = fc.X + r + 0.05 * c.W;
            c.Line(new Vec2(x0, -c.Hh), new Vec2(x0, bot), St.Fine, "panel");
            c.Text(new Vec2((x0 + c.Hw) / 2, -0.15 * c.H), HpLabel(ctx.Lang), 2.5, (c.Hw - x0) * 0.6, 0.18 * c.H);
        }

        private static void HeatPumpSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            double x0 = -c.Hw + 0.08 * c.W;
            c.Rect(x0, -c.Hh, c.Hw, c.Hh, St.Medium, BsTags.Outline);
            c.Line(new Vec2(x0, c.Hh), new Vec2(c.Hw, -c.Hh), St.Medium, "diagonal");
            c.Text(new Vec2(x0 + 0.25 * (c.Hw - x0), -0.18 * c.H), HpLabel(ctx.Lang), 3, 0.4 * (c.Hw - x0), 0.4 * c.H);
            // Ambient air symbol (arrow into the unit) in the lower right triangle.
            c.Arrow(new Vec2(c.Hw - 0.05 * c.W, 0.3 * c.H), new Vec2(c.Hw - 0.25 * c.W, 0.3 * c.H), St.Thin, 0.12 * c.K, "air");
            foreach (var v in new[] { -0.25, 0.25 }) c.LineN(-0.5, v, x0 / c.W, v, St.Medium, BsTags.Stub);
        }

        // ------------------------------------------------------------------ radiator

        private static void RadiatorPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var valve = Math.Min(0.1 * c.W, c.H * 0.9);
            var body = c.Hw - valve;
            c.Rect(-c.Hw, -c.Hh, body, c.Hh, St.Thin, BsTags.Outline);
            var n = Math.Max(3, (int)Math.Round((body + c.Hw) / 0.8));
            for (var i = 1; i < n; i++) { var x = -c.Hw + i * (body + c.Hw) / n; c.Line(new Vec2(x, -c.Hh), new Vec2(x, c.Hh), St.Fine, "rib"); }
            var r = Math.Min(valve / 2, c.Hh) * 0.8;
            c.Circle(new Vec2((body + c.Hw) / 2, 0), r, St.Thin, "valve");
        }

        private static void RadiatorSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double top = -0.36 * c.H, bot = 0.4 * c.H;
            c.Rect(-c.Hw, top, c.Hw, bot, St.Thin, BsTags.Outline);
            c.Line(new Vec2(-c.Hw, top + 0.08 * c.H), new Vec2(c.Hw, top + 0.08 * c.H), St.Fine, "grille");
            var n = Math.Max(4, (int)Math.Round(c.W / 0.6));
            for (var i = 1; i < n; i++)
            {
                var x = -c.Hw + i * c.W / n;
                c.Line(new Vec2(x, top + 0.08 * c.H), new Vec2(x, bot), St.Fine, "rib");
            }
            // Thermostatic head on the valve at the top right, connection stubs below.
            var hx = c.Hw - 0.05 * c.W;
            var hw = Math.Min(0.03 * c.W, 0.08 * c.H);
            c.Line(new Vec2(hx, top), new Vec2(hx, top - 0.04 * c.H), St.Thin, "valve");
            c.RoundRect(hx - hw, -c.Hh, hx + hw, top - 0.04 * c.H, hw * 0.5, St.ThinWhite, "head");
            foreach (var u in new[] { -0.45, 0.45 }) c.LineN(u, 0.4, u, 0.5, St.Thin, BsTags.Stub);
        }

        private static void RadiatorSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            double top = -c.Hh, bot = 0.3 * c.H;
            c.Rect(-c.Hw, top, c.Hw, bot, St.Medium, BsTags.Outline);
            var teeth = Math.Max(3, (int)Math.Round(c.W / 1.6));
            c.Zigzag(new Vec2(-c.Hw + 0.05 * c.W, (top + bot) / 2), new Vec2(c.Hw - 0.05 * c.W, (top + bot) / 2), teeth, 0.28 * (bot - top), St.Thin, "heat");
            foreach (var u in new[] { -0.45, 0.45 }) c.LineN(u, 0.3, u, 0.5, St.Medium, BsTags.Stub);
        }

        // ------------------------------------------------------------------ underfloor heating loop

        /// <summary>Loop spacing on the page (mm), clamped so the drawing stays readable.</summary>
        private static double LoopStep(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = Math.Max(0.05, o.Num("spacing", 150) / Bs.ScaleOf(o, ctx.Doc));
            return Math.Max(s, Math.Max(c.W, c.H) / 60);
        }

        /// <summary>x of the feed (flow) and return columns at the left edge of the loop field.</summary>
        private static (double Xf, double Xr) FeedColumns(Canvas c)
        {
            var a = Math.Min(0.06 * c.W, 2.0);
            return (-c.Hw + 0.5 * a, -c.Hw + 1.5 * a);
        }

        private static IEnumerable<PortAt> FloorLoopPorts(Canvas c, SketchObject o, string view)
        {
            if (view == BsViews.Plan)
            {
                var (xf, xr) = FeedColumns(c);
                return new[] { new PortAt(new Vec2(xf, c.Hh), "flow"), new PortAt(new Vec2(xr, c.Hh), "return") };
            }
            return new[] { new PortAt(c.N(-0.5, -0.25), "flow"), new PortAt(c.N(-0.5, 0.25), "return") };
        }

        private static void FloorLoopPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Meander: the feed runs up the left side, the loop snakes down in horizontal lanes and returns next to the feed.
            var step = LoopStep(c, o, ctx);
            var (xf, xr) = FeedColumns(c);
            double xl = xr, xe = c.Hw - 0.5 * step, yTop = -c.Hh + 0.5 * step, yBot = c.Hh - step;
            var lanes = Math.Max(2, (int)Math.Floor((yBot - yTop) / step) + 1);
            if (lanes % 2 == 1) lanes--;
            var dy = (yBot - yTop) / (lanes - 1);
            var pts = new List<Vec2> { new(xf, c.Hh), new(xf, yTop), new(xe, yTop) };
            for (var k = 1; k < lanes; k++)
            {
                var y = yTop + k * dy;
                var fromRight = k % 2 == 1;
                pts.Add(new Vec2(fromRight ? xe : xl, y));
                pts.Add(new Vec2(fromRight ? xl : xe, y));
            }
            pts.Add(new Vec2(xl, c.Hh));
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Hidden, "field");
            c.Poly(pts, Media.PipeStyle(Media.Pipe("flow"), 0.25), false, BsTags.Pipe);
        }

        private static void FloorLoopSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Screed layer seen in section with the pipe cross-sections in it.
            c.Line(new Vec2(-c.Hw, -c.Hh), new Vec2(c.Hw, -c.Hh), St.Thin, BsTags.Outline);
            c.Line(new Vec2(-c.Hw, c.Hh), new Vec2(c.Hw, c.Hh), St.Fine, BsTags.Outline);
            var step = LoopStep(c, o, ctx);
            var r = Math.Min(0.3 * c.H, 0.3 * step);
            var n = Math.Max(1, (int)Math.Floor((c.W - step) / step) + 1);
            var x0 = -(n - 1) * step / 2;
            for (var i = 0; i < n; i++)
                c.Circle(new Vec2(x0 + i * step, 0), r, i % 2 == 0 ? Media.PipeStyle(Media.Pipe("flow"), 0.13) : Media.PipeStyle(Media.Pipe("return"), 0.13) with { Dash = DashStyle.Solid }, BsTags.Pipe);
        }

        private static void FloorLoopSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            double q = 0.25 * c.H, x0 = -c.Hw, x1 = c.Hw - 0.04 * c.W;
            c.Rect(-c.Hw + 0.02 * c.W, -c.Hh, c.Hw, c.Hh, St.Hidden, "field");
            var teeth = Math.Max(3, (int)Math.Round((x1 - x0) / 2.0));
            c.Zigzag(new Vec2(x0, -q), new Vec2(x1, -q), teeth, 0.18 * c.H, St.Medium, BsTags.Pipe);
            c.Poly(new[] { new Vec2(x1, -q), new Vec2(x1, q), new Vec2(x0, q) }, St.Medium, false, BsTags.Pipe);
        }

        // ------------------------------------------------------------------ manifold

        private static int CircuitCount(SketchObject o) => Math.Max(1, Math.Min(12, o.Int("circuits", 4)));

        /// <summary>Outlet positions (x of flow and return outlet) of every circuit.</summary>
        private static IEnumerable<(double F, double R)> Outlets(Canvas c, SketchObject o)
        {
            var n = CircuitCount(o);
            double x0 = -0.38 * c.W, span = 0.84 * c.W, pitch = span / n, d = 0.2 * pitch;
            for (var i = 0; i < n; i++)
            {
                var x = x0 + (i + 0.5) * pitch;
                yield return (x - d, x + d);
            }
        }

        private static string N(int i) => i.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static IEnumerable<PortAt> ManifoldPorts(Canvas c, SketchObject o, string view)
        {
            double vf = view == BsViews.Plan ? -0.15 : -0.2, vr = view == BsViews.Plan ? 0.15 : 0.05;
            if (view == BsViews.Schematic) { vf = -0.3; vr = 0.0; }
            yield return new PortAt(c.N(-0.5, vf), "flow");
            yield return new PortAt(c.N(-0.5, vr), "return");
            var i = 1;
            foreach (var (f, r) in Outlets(c, o))
            {
                yield return new PortAt(new Vec2(f, c.Hh), "f" + N(i));
                yield return new PortAt(new Vec2(r, c.Hh), "r" + N(i));
                i++;
            }
        }

        private static void ManifoldPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            double yf = -0.15 * c.H, yr = 0.15 * c.H, x1 = 0.46 * c.W;
            c.Line(new Vec2(-c.Hw, yf), new Vec2(x1, yf), Media.PipeStyle(Media.Pipe("flow"), 0.3), "bar");
            c.Line(new Vec2(-c.Hw, yr), new Vec2(x1, yr), Media.PipeStyle(Media.Pipe("return"), 0.3) with { Dash = DashStyle.Solid }, "bar");
            var r = Math.Min(0.08 * c.H, 0.25);
            foreach (var (f, rr) in Outlets(c, o))
            {
                c.Circle(new Vec2(f, yf), r, St.ThinWhite, "outlet");
                c.Circle(new Vec2(rr, yr), r, St.ThinWhite, "outlet");
            }
        }

        private static void ManifoldSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw + 0.04 * c.W, -c.Hh, c.Hw, c.Hh, St.Hidden, "cabinet");
            double yf = -0.2 * c.H, yr = 0.05 * c.H, bh = Math.Min(0.07 * c.H, 0.35), x1 = 0.46 * c.W;
            var flow = Media.PipeStyle(Media.Pipe("flow"), 0.25);
            var ret = Media.PipeStyle(Media.Pipe("return"), 0.25) with { Dash = DashStyle.Solid };
            foreach (var (f, r) in Outlets(c, o))
            {
                c.Line(new Vec2(r, yr), new Vec2(r, c.Hh), ret, BsTags.Stub);
                c.Line(new Vec2(f, yf), new Vec2(f, c.Hh), flow, BsTags.Stub);
                c.Circle(new Vec2(f, yf - bh * 1.6), bh * 0.7, St.Fine, "valve");
            }
            c.Line(new Vec2(-c.Hw, yf), new Vec2(-0.42 * c.W, yf), flow, BsTags.Stub);
            c.Line(new Vec2(-c.Hw, yr), new Vec2(-0.42 * c.W, yr), ret, BsTags.Stub);
            c.Rect(-0.42 * c.W, yf - bh / 2, x1, yf + bh / 2, St.ThinWhite, "bar");
            c.Rect(-0.42 * c.W, yr - bh / 2, x1, yr + bh / 2, St.ThinWhite, "bar");
        }

        private static void ManifoldSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            double yf = -0.3 * c.H, yr = 0.0, x1 = 0.46 * c.W;
            c.Line(new Vec2(-c.Hw, yf), new Vec2(x1, yf), St.Medium, "bar");
            c.Line(new Vec2(-c.Hw, yr), new Vec2(x1, yr), St.Medium, "bar");
            foreach (var (f, r) in Outlets(c, o))
            {
                c.Line(new Vec2(f, yf), new Vec2(f, c.Hh), St.Thin, BsTags.Stub);
                c.Line(new Vec2(r, yr), new Vec2(r, c.Hh), St.Thin, BsTags.Stub);
                c.Circle(new Vec2(f, yf), 0.05 * c.H, St.Black, "tee");
                c.Circle(new Vec2(r, yr), 0.05 * c.H, St.Black, "tee");
            }
            c.Text(new Vec2(-0.47 * c.W, yf - 0.1 * c.H), ctx.Lang == Lang.De ? "VL" : "F", 1.8, 0.12 * c.W, 0.14 * c.H, anchor: TextAnchor.Start);
            c.Text(new Vec2(-0.47 * c.W, yr - 0.1 * c.H), ctx.Lang == Lang.De ? "RL" : "R", 1.8, 0.12 * c.W, 0.14 * c.H, anchor: TextAnchor.Start);
        }

        // ------------------------------------------------------------------ circulator pump

        private static void PumpPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.3 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.Circle(Vec2.Zero, r, St.ThinWhite, BsTags.Outline);
            c.Circle(Vec2.Zero, r * 0.55, St.Fine, "motor");
            c.Head(new Vec2(c.Hw - 0.02 * c.W, 0), new Vec2(1, 0), 0.14 * c.K, "flow");
        }

        private static void PumpSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.22 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            // Motor block on top of the pump housing, with cooling ribs.
            double mw = 0.2 * c.K, top = -0.48 * c.K;
            c.Rect(-mw, top, mw, -r * 0.6, St.ThinWhite, "motor");
            for (var i = 1; i <= 3; i++) { var y = top + i * (-r * 0.6 - top) / 4; c.Line(new Vec2(-mw, y), new Vec2(mw, y), St.Fine, "rib"); }
            c.Circle(Vec2.Zero, r, St.ThinWhite, BsTags.Outline);
        }

        private static void PumpSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.38 * c.K;
            c.LineN(-0.5, 0, -r / c.W, 0, St.Medium, BsTags.Stub);
            c.LineN(r / c.W, 0, 0.5, 0, St.Medium, BsTags.Stub);
            c.Circle(Vec2.Zero, r, St.Medium, BsTags.Outline);
            // Triangle pointing in the flow direction, its tip on the circle.
            c.Poly(new[] { new Vec2(r, 0), new Vec2(-r * 0.5, -r * 0.866), new Vec2(-r * 0.5, r * 0.866) }, St.Medium, true, "triangle");
        }

        // ------------------------------------------------------------------ mixing valve

        private static void MixerBody(Canvas c, double s, Style st)
        {
            var hs = 0.6 * s;
            c.BowTie(Vec2.Zero, s, hs, st);
            c.Poly(new[] { Vec2.Zero, new Vec2(-hs, s), new Vec2(hs, s) }, st, true, "valve");
        }

        private static void MixerPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            c.LineN(-0.5, 0, -s / c.W, 0, St.Thin, BsTags.Stub);
            c.LineN(s / c.W, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.LineN(0, s / c.H, 0, 0.5, St.Thin, BsTags.Stub);
            MixerBody(c, s, St.Thin);
            // Actuator seen from above.
            c.Circle(Vec2.Zero, 0.16 * c.K, St.ThinWhite, "actuator");
        }

        private static void MixerSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bw = 0.2 * c.K, bh = 0.16 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.LineN(0, 0, 0, 0.5, St.Thin, BsTags.Stub);
            c.Rect(-bw, -bh, bw, bh, St.ThinWhite, BsTags.Outline);
            c.Line(new Vec2(0, -bh), new Vec2(0, -0.26 * c.K), St.Thin, "stem");
            c.RoundRect(-0.2 * c.K, -0.48 * c.K, 0.2 * c.K, -0.26 * c.K, 0.04 * c.K, St.ThinWhite, "actuator");
        }

        private static void MixerSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            c.LineN(-0.5, 0, -s / c.W, 0, St.Medium, BsTags.Stub);
            c.LineN(s / c.W, 0, 0.5, 0, St.Medium, BsTags.Stub);
            c.LineN(0, s / c.H, 0, 0.5, St.Medium, BsTags.Stub);
            MixerBody(c, s, St.Medium);
            // Motor actuator: stem and circle with M.
            var r = 0.12 * c.K;
            var mc = new Vec2(0, -0.36 * c.K);
            c.Line(Vec2.Zero, new Vec2(0, mc.Y + r), St.Thin, "stem");
            c.Circle(mc, r, St.MediumWhite, "actuator");
            c.Text(mc, "M", r * 1.3, r * 1.6, r * 1.4);
        }

        // ------------------------------------------------------------------ expansion vessel

        private static void ExpansionPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.42 * c.K;
            var centre = new Vec2(0, c.Hh - r - 0.04 * c.H);
            c.Line(new Vec2(0, -c.Hh), new Vec2(0, centre.Y - r), St.Thin, "bracket");
            c.Circle(centre, r, St.Thin, BsTags.Outline);
            c.Circle(centre, r * 0.2, St.Fine, "connection");
        }

        private static void ExpansionSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double top = -c.Hh, bot = 0.38 * c.H, w = 0.46 * c.W;
            c.RoundRect(-w, top, w, bot, w * 0.9, St.Thin, BsTags.Outline);
            var ym = (top + bot) / 2;
            c.Line(new Vec2(-w, ym), new Vec2(w, ym), St.Fine with { Dash = DashStyle.Dash }, "membrane");
            c.LineN(0, bot / c.H, 0, 0.5, St.Thin, BsTags.Stub);
        }

        private static void ExpansionSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = Math.Min(0.46 * c.W, 0.38 * c.H);
            var centre = new Vec2(0, -c.Hh + r + 0.02 * c.H);
            c.Circle(centre, r, St.Medium, BsTags.Outline);
            c.Line(new Vec2(-r, centre.Y), new Vec2(r, centre.Y), St.Medium, "membrane");
            c.Line(new Vec2(0, centre.Y + r), new Vec2(0, c.Hh), St.Medium, BsTags.Stub);
        }

        // ------------------------------------------------------------------ thermostatic valve

        private static void TrvPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            c.LineN(-0.5, 0, -s / c.W, 0, St.Thin, BsTags.Stub);
            c.LineN(s / c.W, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.BowTie(Vec2.Zero, s, 0.6 * s, St.Thin);
            c.Circle(Vec2.Zero, 0.14 * c.K, St.ThinWhite, "head");
        }

        private static void TrvSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bw = 0.18 * c.K, bh = 0.12 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.Rect(-bw, -bh, bw, bh, St.ThinWhite, BsTags.Outline);
            c.RoundRect(-0.14 * c.K, -0.48 * c.K, 0.14 * c.K, -bh, 0.06 * c.K, St.ThinWhite, "head");
            for (var i = 1; i <= 3; i++) { var x = -0.14 * c.K + i * 0.07 * c.K; c.Line(new Vec2(x, -0.44 * c.K), new Vec2(x, -bh - 0.04 * c.K), St.Fine, "grip"); }
        }

        private static void TrvSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            c.LineN(-0.5, 0, -s / c.W, 0, St.Medium, BsTags.Stub);
            c.LineN(s / c.W, 0, 0.5, 0, St.Medium, BsTags.Stub);
            c.BowTie(Vec2.Zero, s, 0.6 * s, St.Medium);
            // Thermostatic actuator: stem and sensor bulb.
            c.Line(Vec2.Zero, new Vec2(0, -0.3 * c.K), St.Thin, "stem");
            c.RoundRect(-0.16 * c.K, -0.48 * c.K, 0.16 * c.K, -0.3 * c.K, 0.09 * c.K, St.MediumWhite, "head");
        }
    }
}
