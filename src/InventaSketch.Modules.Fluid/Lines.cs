using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Fluid.Common;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>Lines (point objects) and connection elements: junction dot, crossing, plug, quick coupling, enclosure.</summary>
    internal static class Lines
    {
        public const string Cat = "fluid.lines";

        private static readonly ParamDef Bridge = Flag("bridge", T("Bridge (hop) at the crossing", "Brücke (Bogen) an der Kreuzung"), false);
        private static readonly ParamDef Coupled = Flag("coupled", T("Coupled", "Gekuppelt"), true);
        private static readonly ParamDef CouplingCheck = Flag("check", T("With non-return valves", "Mit Rückschlagventilen"), false);

        public static IEnumerable<ObjectType> Types()
        {
            yield return new FluidLineType();

            yield return new FluidType("fluid.junction", T("Junction dot", "Verbindungspunkt"), Cat,
                new[] { "junction", "connection", "dot", "tee", "branch", "verbindungspunkt", "abzweig", "knoten", "leitungsverbindung" },
                Views.Symbol(2, 2, (p, o) => p.Dot(0, 0, 0.8, "junction")))
            {
                Labeled = false,
                Ports = Fixed(new Port("1", 0, 0)),
                Passages = _ => new[] { new[] { "1" } },
                CompanionIds = new[] { FluidLineType.TypeId, "fluid.gauge", "fluid.valve.relief" },
            };

            yield return new FluidType("fluid.crossing", T("Line crossing (not connected)", "Leitungskreuzung (nicht verbunden)"), Cat,
                new[] { "crossing", "line crossing", "not connected", "hop", "kreuzung", "leitungskreuzung", "nicht verbunden" },
                Views.Symbol(10, 10, Crossing))
            {
                Params = new[] { Bridge }, Labeled = false,
                Ports = Fixed(new Port("W", -5, 0), new Port("E", 5, 0), new Port("N", 0, -5), new Port("S", 0, 5)),
                Passages = _ => new[] { new[] { "W", "E" }, new[] { "N", "S" } },
                CompanionIds = new[] { FluidLineType.TypeId, "fluid.junction" },
                Extra = o => new[] { Toggle(o, "fluid.crossing.bridge", T("Bridge (hop)", "Brücke (Bogen)"), "bridge", false) },
            };

            yield return new FluidType("fluid.plug", T("Plugged port", "Verschlossener Anschluss"), Cat,
                new[] { "plug", "plugged port", "blanked", "closed port", "stopfen", "verschlossen", "verschlussschraube", "blindstopfen" },
                Views.Symbol(5, 5, (p, o) =>
                {
                    p.Line(0, -2.5, 0, 0.8, null, "line");
                    p.Line(-2, 0.8, 2, 0.8, S.Bold, "plug");
                }))
            {
                Labeled = false,
                Ports = Fixed(new Port("P", 0, -2.5)),
                CompanionIds = new[] { FluidLineType.TypeId },
            };

            yield return new FluidType("fluid.coupling", T("Quick coupling", "Schnellkupplung"), Cat,
                new[] { "quick coupling", "coupling", "quick connector", "quick disconnect", "schnellkupplung", "kupplung", "steckkupplung" },
                Views.Symbol(15, 10, Coupling))
            {
                Params = new[] { Coupled, CouplingCheck }, RefPrefix = "0X1",
                Ports = Fixed(new Port("1", -7.5, 0), new Port("2", 7.5, 0)),
                Passages = o => o.Flag("coupled", true) ? new[] { new[] { "1", "2" } } : Array.Empty<string[]>(),
                CompanionIds = new[] { FluidLineType.TypeId, "fluid.valve.check" },
                Extra = o => new[]
                {
                    Toggle(o, "fluid.coupling.coupled", T("Coupled / uncoupled", "Gekuppelt / entkuppelt"), "coupled", true),
                    Toggle(o, "fluid.check", T("With non-return valves", "Mit Rückschlagventilen"), "check", false),
                },
            };

            yield return new FluidType("fluid.enclosure", T("Enclosure (assembly boundary)", "Umrahmung (Baugruppe)"), Cat,
                new[] { "enclosure", "boundary", "assembly", "unit", "block", "frame", "umrahmung", "baugruppe", "geräteumrandung", "rahmen" },
                Views.Symbol(60, 40, (p, o) => p.Rect(-30, -20, 30, 20, S.Enclosure, "enclosure")))
            {
                RefPrefix = "1", Uniform = false,
                CompanionIds = new[] { "fluid.valve.directional", "fluid.valve.relief", FluidLineType.TypeId },
            };
        }

        private static void Crossing(Pen p, SketchObject o)
        {
            p.Line(-5, 0, 5, 0, null, "line");
            if (!o.Flag("bridge", false))
            {
                p.Line(0, -5, 0, 5, null, "line");
                return;
            }
            p.Line(0, -5, 0, -1.6, null, "line");
            p.Arc(0, 0, 1.6, 1.6, 270, 180, null, "bridge");
            p.Line(0, 1.6, 0, 5, null, "line");
        }

        private static void Coupling(Pen p, SketchObject o)
        {
            var coupled = o.Flag("coupled", true);
            var check = o.Flag("check", false);
            var plate = coupled ? 0.6 : 1.8;
            foreach (var dir in new[] { -1, 1 })
            {
                var start = dir * 7.5;
                if (check)
                {
                    // Check valve in each half, seat towards the coupling face (closed when uncoupled).
                    const double r = 1.0, vx = 2.6;
                    var edge = Valves.Seat(p, dir * vx, 0, dir, r, 2.0);
                    p.Line(start, 0, edge, 0, null, "line");
                    p.Line(dir * vx, 0, dir * plate, 0, null, "line");
                }
                else p.Line(start, 0, dir * plate, 0, null, "line");
                p.Line(dir * plate, -2.4, dir * plate, 2.4, S.Bold, "half");
            }
            if (coupled) p.Line(-plate, 0, plate, 0, null, "line");
        }
    }

    /// <summary>Routing of fluid lines between their points: orthogonal (default) or free (straight).</summary>
    internal static class Routing
    {
        public static readonly string[] Modes = { "orthogonal", "free" };

        public static ParamDef Param() => Choice("route", T("Routing", "Leitungsführung"), "orthogonal",
            ("orthogonal", "Orthogonal", "Orthogonal"), ("free", "Free (straight)", "Frei (gerade)")).WithOptionIcons(IconIds.ForRoute);

        /// <summary>
        /// Exit direction (on the page) of an attached end: perpendicular to the box edge of the target the port sits on,
        /// honouring the target's rotation. Null for free ends and ports inside the box.
        /// </summary>
        public static bool? ExitHorizontal(SketchDocument doc, SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a is null) return null;
            Vec2 local;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6) local = new Vec2(Math.Sign(a.U), 0);
            else if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6) local = new Vec2(0, Math.Sign(a.V));
            else return null;
            var target = doc.Get(a.ObjectId);
            var dir = target is null ? local : target.Matrix().ApplyLinear(local);
            return Math.Abs(dir.X) >= Math.Abs(dir.Y);
        }

        /// <summary>Routed polyline through all points.</summary>
        public static List<Vec2> Route(IReadOnlyList<Vec2> pts, string mode, bool? startHorizontal = null, bool? endHorizontal = null)
        {
            var result = new List<Vec2> { pts[0] };
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var sh = i == 0 ? startHorizontal : null;
                var eh = i + 2 == pts.Count ? endHorizontal : null;
                var d = b - a;
                if (mode == "orthogonal" && Math.Abs(d.X) > 1e-9 && Math.Abs(d.Y) > 1e-9)
                {
                    var s = sh ?? (eh ?? Math.Abs(d.X) >= Math.Abs(d.Y));
                    var e = eh ?? s;
                    if (s && e) { var mx = (a.X + b.X) / 2; result.Add(new Vec2(mx, a.Y)); result.Add(new Vec2(mx, b.Y)); }
                    else if (s) result.Add(new Vec2(b.X, a.Y));
                    else if (e) result.Add(new Vec2(a.X, b.Y));
                    else { var my = (a.Y + b.Y) / 2; result.Add(new Vec2(a.X, my)); result.Add(new Vec2(b.X, my)); }
                }
                result.Add(b);
            }
            return result;
        }

        /// <summary><see cref="AnchorRole.Rail"/> anchors along a polyline (ids rail0, rail1, …).</summary>
        public static IEnumerable<Anchor> Rails(IReadOnlyList<Vec2> p)
        {
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > Geo.Eps) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Fluid line: working line (solid), pilot line (long dashes), drain/leakage line (short dashes) or flexible hose (arc),
    /// as a point object between ports. Orthogonal routing leaves attached ports perpendicular to the symbol edge; the
    /// routed path is a rail, so other line ends and junction dots dock anywhere on it.
    /// </summary>
    internal sealed class FluidLineType : ObjectType
    {
        public const string TypeId = "fluid.line";

        public static readonly ParamDef Kind = Choice("kind", T("Line type", "Leitungsart"), "working",
            ("working", "Working line", "Arbeitsleitung"), ("pilot", "Pilot (control) line", "Steuerleitung"),
            ("drain", "Drain / leakage line", "Leckölleitung / Entlüftungsleitung"), ("hose", "Flexible hose", "Schlauchleitung")).WithOptionIcons(v => v switch
        {
            "working" => IconIds.LineSolid,
            "pilot" => IconIds.LineLongDash,
            "drain" => IconIds.LineDash,
            "hose" => IconIds.CourseWavy,
            _ => null,
        });

        private static readonly IReadOnlyList<ViewDef> ViewList = new[] { new ViewDef("symbol", T("Circuit symbol", "Schaltzeichen")) { Icon = IconIds.Symbol } };

        public override string Id => TypeId;
        public override I18nText Name => T("Fluid line", "Fluidleitung");
        public override string Category => Lines.Cat;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "line", "pipe", "working line", "pilot line", "control line", "drain line", "hose", "flexible", "tube",
            "leitung", "rohrleitung", "arbeitsleitung", "steuerleitung", "leckölleitung", "schlauch", "schlauchleitung",
        };
        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override IReadOnlyList<ViewDef> Views => ViewList;
        private static readonly ParamDef Hops = Flag("hops", T("Hops at crossings", "Brücken an Kreuzungen"), true);

        public override IReadOnlyList<ParamDef> Parameters => new[] { Kind, Routing.Param(), Hops };
        public override bool InsertablePoints => true;

        /// <summary>A line drawn over an earlier line hops over it (unless switched off); hosts offer to connect there.</summary>
        public override bool HopsAtCrossings => true;
        public override bool DependsOnNeighbours => true;

        private static IReadOnlyList<Vec2> PathOf(SketchObject x, RenderContext ctx)
        {
            var q = ctx.Doc.ResolvePoints(x);
            return q.Count < 2 ? q : Path(x, q, ctx);
        }

        /// <summary>Lines crossing this one (their hops depend on where this one runs).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) return Array.Empty<string>();
            return Crossings.Find(ctx.Doc, o, Path(o, p, ctx), x => PathOf(x, ctx), ctx.Registry ?? Owner).Select(c => c.Other).Distinct();
        }

        /// <summary>On a line one adds valves, cylinders and gauges ("Insert ▸").</summary>
        public override IReadOnlyList<string> Companions => new[]
        {
            DirectionalValves.TypeId, "fluid.cylinder.double", "fluid.cylinder.single", "fluid.gauge", "fluid.valve.check",
            "fluid.valve.throttle", "fluid.valve.relief", "fluid.junction",
        };

        public static string KindOf(SketchObject o) => o.Str("kind", "working");

        public static Style StyleOf(string kind) => kind switch
        {
            "pilot" => S.Pilot,
            "drain" => S.Drain,
            _ => S.Line,
        };

        /// <summary>Routed polyline (hoses: the straight chords between their points).</summary>
        public static List<Vec2> Path(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
            => KindOf(o) == "hose" || o.Str("route", "orthogonal") == "free"
                ? p.ToList()
                : Routing.Route(p, "orthogonal", Routing.ExitHorizontal(ctx.Doc, o, 0), Routing.ExitHorizontal(ctx.Doc, o, p.Count - 1));

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) return Array.Empty<Primitive>();
            var kind = KindOf(o);
            if (kind == "hose") return new Primitive[] { Prim.Path(Hose(p), S.Line) with { Tag = "hose" } };
            var route = Path(o, p, ctx);
            var hops = o.Flag("hops", true) ? Crossings.Find(ctx.Doc, o, route, x => PathOf(x, ctx), ctx.Registry ?? Owner).Where(c => c.Above).ToList() : new List<Crossing>();
            Primitive main = hops.Count == 0 ? Prim.Polyline(route, StyleOf(kind)) : Prim.Path(Crossings.HopPath(route, hops), StyleOf(kind));
            var list = new List<Primitive> { main with { Tag = "line" } };
            // Junction dot where an end is docked onto another line (a tee, Polylines.Branch).
            if (o.Attach is not null)
                foreach (var i in new[] { 0, p.Count - 1 })
                    if (i < o.Attach.Count && o.Attach[i]?.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal))
                        list.Add(Prim.Circle(p[i].X, p[i].Y, 0.8, S.Black) with { Tag = "junction" });
            return list;
        }

        /// <summary>Flexible hose: an arc-like curve bulging to one side between consecutive points.</summary>
        private static List<PathSeg> Hose(IReadOnlyList<Vec2> p)
        {
            var segs = new List<PathSeg> { PathSeg.Move(p[0]) };
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var a = p[i];
                var b = p[i + 1];
                var d = b - a;
                var bulge = d.Perp * -0.18;
                segs.Add(PathSeg.Cubic(a + d * 0.2 + bulge, a + d * 0.8 + bulge, b));
            }
            return segs;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            return p.Count < 2 ? Array.Empty<Anchor>() : Routing.Rails(Path(o, p, ctx));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Cycle("fluid.line.kind.next", T("Next line type (working / pilot / drain / hose)", "Nächste Leitungsart (Arbeit / Steuer / Leck / Schlauch)"), Kind),
            new ContextAction("fluid.line.route", T("Orthogonal / straight", "Orthogonal / gerade"), IconIds.Toggle,
                x => x.WithParam("route", x.Str("route", "orthogonal") == "free" ? "orthogonal" : "free"))
            { IsActive = o.Str("route", "orthogonal") == "orthogonal", Group = "route" },
            Toggle(o, "fluid.line.hops", T("Hops at crossings on/off", "Brücken an Kreuzungen an/aus"), "hops", true, IconIds.Hop),
        };
    }
}
