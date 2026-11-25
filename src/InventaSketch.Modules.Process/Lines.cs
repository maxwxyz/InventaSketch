using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>Routing of lines between their points: orthogonal (default) or free (straight).</summary>
    internal static class Routing
    {
        public static ParamDef Param() => Choice("route", "Routing", "Leitungsführung", "orthogonal",
            ("orthogonal", "Orthogonal", "Orthogonal"), ("free", "Free (straight)", "Frei (gerade)")).WithOptionIcons(IconIds.ForRoute);

        /// <summary>
        /// Exit direction (on the page) of an attached end: perpendicular to the box edge of the target the port sits on,
        /// honouring the target's rotation; a nozzle inside the box (on a dished head) leaves towards the nearest box edge.
        /// Null for free ends, line targets and the box centre.
        /// </summary>
        public static bool? ExitHorizontal(SketchDocument doc, SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a is null) return null;
            var target = doc.Get(a.ObjectId);
            if (target is null || target.IsPointGeometry) return null;
            Vec2 local;
            if (Math.Abs(Math.Abs(a.U) - 0.5) < 1e-6) local = new Vec2(Math.Sign(a.U), 0);
            else if (Math.Abs(Math.Abs(a.V) - 0.5) < 1e-6) local = new Vec2(0, Math.Sign(a.V));
            else
            {
                if (Math.Abs(a.U) < 1e-6 && Math.Abs(a.V) < 1e-6) return null;
                var toSide = (0.5 - Math.Abs(a.U)) * target.Size.W;
                var toTopBottom = (0.5 - Math.Abs(a.V)) * target.Size.H;
                local = toTopBottom <= toSide ? new Vec2(0, Math.Sign(a.V)) : new Vec2(Math.Sign(a.U), 0);
            }
            var dir = target.Matrix().ApplyLinear(local);
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
            return Clean(result);
        }

        /// <summary>Points without consecutive duplicates.</summary>
        public static List<Vec2> Clean(IReadOnlyList<Vec2> p)
        {
            var list = new List<Vec2>(p.Count);
            foreach (var q in p) if (list.Count == 0 || list[list.Count - 1].DistanceTo(q) > 1e-6) list.Add(q);
            return list;
        }

        /// <summary><see cref="AnchorRole.Rail"/> anchors along a polyline (ids rail0, rail1, …).</summary>
        public static IEnumerable<Anchor> Rails(IReadOnlyList<Vec2> p)
        {
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > Geo.Eps) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Polyline offset sideways by <paramref name="d"/> (mitred corners, left of the direction for d &gt; 0).</summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> p, double d)
        {
            var result = new List<Vec2>(p.Count);
            for (var i = 0; i < p.Count; i++)
            {
                var n0 = i > 0 ? (p[i] - p[i - 1]).Normalized().Perp : (p[1] - p[0]).Normalized().Perp;
                var n1 = i + 1 < p.Count ? (p[i + 1] - p[i]).Normalized().Perp : n0;
                var n = (n0 + n1).Normalized();
                var cos = n.Dot(n1);
                result.Add(p[i] + n * (d / Math.Max(0.3, cos)));
            }
            return result;
        }

        /// <summary>Points and unit directions every <paramref name="step"/> mm along a polyline, starting at <paramref name="first"/>.</summary>
        public static IEnumerable<(Vec2 At, Vec2 Dir)> Along(IReadOnlyList<Vec2> p, double first, double step, double endGap)
        {
            var total = 0.0;
            for (var i = 0; i + 1 < p.Count; i++) total += p[i].DistanceTo(p[i + 1]);
            var s = first;
            var walked = 0.0;
            for (var i = 0; i + 1 < p.Count && s <= total - endGap; i++)
            {
                var len = p[i].DistanceTo(p[i + 1]);
                if (len < Geo.Eps) continue;
                var dir = (p[i + 1] - p[i]) / len;
                while (s <= walked + len && s <= total - endGap)
                {
                    // No mark right on a corner (it would read as two marks).
                    var local = s - walked;
                    var nearCorner = (i > 0 && local < 1.2) || (i + 2 < p.Count && len - local < 1.2);
                    if (!nearCorner) yield return (p[i] + dir * local, dir);
                    s += step;
                }
                walked += len;
            }
        }

        /// <summary>Middle and unit direction of the longest segment (label position).</summary>
        public static (Vec2 Mid, Vec2 Dir) Longest(IReadOnlyList<Vec2> p)
        {
            var best = 0;
            var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var d = p[best + 1] - p[best];
            return (Vec2.Lerp(p[best], p[best + 1], 0.5), len > Geo.Eps ? d / len : new Vec2(1, 0));
        }
    }

    /// <summary>
    /// Base of the module's lines (point objects): orthogonal or free routing between their points, corners can be inserted,
    /// the routed path is a rail (branches and instrument connections dock anywhere on it) and attached ends leave a port
    /// perpendicular to the symbol edge.
    /// </summary>
    internal abstract class ProcessLineBase : ObjectType
    {
        private static readonly IReadOnlyList<ViewDef> ViewList = new[] { ProcessViews.SymbolView };

        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override IReadOnlyList<ViewDef> Views => ViewList;
        public override bool InsertablePoints => true;
        public override bool IsPart => false;

        /// <summary>Routed polyline of the object.</summary>
        public List<Vec2> Path(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
            => o.Str("route", "orthogonal") == "free"
                ? Routing.Clean(p)
                : Routing.Route(p, "orthogonal", Routing.ExitHorizontal(ctx.Doc, o, 0), Routing.ExitHorizontal(ctx.Doc, o, p.Count - 1));

        protected IReadOnlyList<Vec2> PathOf(SketchObject x, RenderContext ctx)
        {
            var q = ctx.Doc.ResolvePoints(x);
            return q.Count < 2 ? q : Path(x, q, ctx);
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            return p.Count < 2 ? Array.Empty<Anchor>() : Routing.Rails(Path(o, p, ctx));
        }

        /// <summary>Junction dots where an end is docked onto another line's rail (a tee, <see cref="Polylines.Branch"/>).</summary>
        protected static IEnumerable<Primitive> Junctions(SketchObject o, IReadOnlyList<Vec2> raw, double r)
        {
            if (o.Attach is null) yield break;
            foreach (var i in new[] { 0, raw.Count - 1 })
                if (i < o.Attach.Count && o.Attach[i]?.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal))
                    yield return Prim.Circle(raw[i].X, raw[i].Y, r, S.Black) with { Tag = ProcessTags.Junction };
        }

        protected ContextAction RouteAction(SketchObject o) => new("process.line.route", T("Orthogonal / straight", "Orthogonal / gerade"), IconIds.Toggle,
            x => x.WithParam("route", x.Str("route", "orthogonal") == "free" ? "orthogonal" : "free"))
        { IsActive = o.Str("route", "orthogonal") == "orthogonal", Group = "route", ParamKeys = new[] { "route" } };
    }

    /// <summary>
    /// Process line (pipe in a flow sheet): main (heavy) or secondary (thin) line, heat-traced (with a dashed tracer) or
    /// future (dashed); optional flow arrow and line number along the longest segment; hops where it crosses an earlier
    /// process line.
    /// </summary>
    internal sealed class ProcessLineType : ProcessLineBase
    {
        public const string TypeId = "process.line";

        public static readonly ParamDef Kind = Choice("kind", "Line type", "Leitungsart", "main",
            ("main", "Main process line", "Hauptfließlinie"), ("secondary", "Secondary / utility line", "Nebenfließlinie / Hilfsstoff"),
            ("traced", "Heat-traced line", "Beheizte Leitung (Begleitheizung)"), ("future", "Future / existing line", "Geplante / bestehende Leitung"))
            .WithOptionIcons(v => v switch
            {
                "main" => IconIds.SizeLarge,
                "secondary" => IconIds.SizeSmall,
                "traced" => IconIds.LineDashDot,
                "future" => IconIds.LineDash,
                _ => null,
            });
        private static readonly ParamDef Arrow = Flag("arrow", "Flow arrow", "Fließpfeil", false);
        private static readonly ParamDef LineNo = Text("lineNo", "Line number", "Leitungsnummer", "");
        private static readonly ParamDef Hops = Flag("hops", "Hops at crossings", "Brücken an Kreuzungen", true);

        public override string Id => TypeId;
        public override I18nText Name => T("Process line", "Prozessleitung (Fließlinie)");
        public override string Category => ProcessCategories.Lines;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "process line", "pipe", "pipeline", "piping", "flow line", "main line", "utility line", "heat traced", "p&id", "pfd",
            "prozessleitung", "rohrleitung", "fließlinie", "hauptfließlinie", "nebenfließlinie", "begleitheizung", "r&i", "fließschema",
        };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Kind, Arrow, LineNo, Routing.Param(), Hops };
        /// <summary>Process piping (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.Process;
        public override IReadOnlyList<string> Companions => new[]
        {
            "process.valve.manual", "process.valve.control", "process.valve.check", "process.pump.centrifugal", "process.instrument.bubble",
            "process.instrument.orifice", "process.fitting.reducer",
        };

        /// <summary>A line drawn over an earlier process line hops over it; hosts offer to connect there.</summary>
        public override bool HopsAtCrossings => true;
        public override bool DependsOnNeighbours => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) return Array.Empty<string>();
            return Crossings.Find(ctx.Doc, o, Path(o, p, ctx), x => PathOf(x, ctx), ctx.Registry ?? Owner).Select(c => c.Other).Distinct();
        }

        public static Style StyleOf(string kind) => kind switch
        {
            "secondary" => S.Thin with { StrokeWidth = 0.25 },
            "future" => S.Sym with { Dash = DashStyle.Dash },
            _ => S.Bold,
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var raw = ctx.Doc.ResolvePoints(o);
            if (raw.Count < 2) return Array.Empty<Primitive>();
            var route = Path(o, raw, ctx);
            if (route.Count < 2) return Array.Empty<Primitive>();
            var kind = o.Str("kind", "main");
            var style = StyleOf(kind);
            if (o.Flag("arrow", false)) style = style with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            var hops = o.Flag("hops", true) ? Crossings.Find(ctx.Doc, o, route, x => PathOf(x, ctx), ctx.Registry ?? Owner).Where(c => c.Above).ToList() : new List<Crossing>();
            Primitive main = hops.Count == 0 ? Prim.Polyline(route, style) : Prim.Path(Crossings.HopPath(route, hops), style);
            var list = new List<Primitive> { main with { Tag = ProcessTags.Line } };
            if (kind == "traced") list.Add(Prim.Polyline(Routing.Offset(route, 0.9), S.Dashed) with { Tag = "tracer" });
            list.AddRange(Junctions(o, raw, 0.7));
            var no = o.Str("lineNo", "").Trim();
            if (no.Length > 0)
            {
                var (mid, dir) = Routing.Longest(route);
                if (dir.X < -1e-9 || (Math.Abs(dir.X) < 1e-9 && dir.Y > 0)) dir = -dir;
                var up = new Vec2(dir.Y, -dir.X);
                const double size = 2.2;
                var rot = Geo.RadToDeg(Math.Atan2(dir.Y, dir.X));
                list.Add(new TextPrim(mid + up * (size * 0.9 + (kind == "traced" ? 0.9 : 0)), no, new TextStyle { Size = size }, Math.Abs(rot) < 1e-9 ? 0 : rot) { Tag = ProcessTags.Label });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Cycle("process.line.kind", T("Line type", "Leitungsart"), Kind),
            Toggle(o, "process.line.arrow", T("Flow arrow", "Fließpfeil"), "arrow", false, IconIds.FlowArrow),
            ContextAction.WithInput("process.line.number", T("Line number …", "Leitungsnummer …"), IconIds.Name,
                T("Line number (e.g. 100-P-001-DN50)", "Leitungsnummer (z. B. 100-P-001-DN50)"), o.Str("lineNo", ""),
                (x, t) => x.WithParam("lineNo", t.Trim())) with { Group = "label", ParamKeys = new[] { "lineNo" } },
            RouteAction(o),
            Toggle(o, "process.line.hops", T("Hops at crossings", "Brücken an Kreuzungen"), "hops", true, IconIds.Hop),
        };
    }

    /// <summary>
    /// Instrument signal line: process connection (thin), electric (dashed), pneumatic (double slashes), capillary (crosses)
    /// or data link (small circles), between instrument bubbles, control valves and process lines.
    /// </summary>
    internal sealed class SignalLineType : ProcessLineBase
    {
        public const string TypeId = "process.line.signal";

        public static readonly ParamDef Kind = Choice("kind", "Signal", "Signalart", "electric",
            ("connection", "Process connection / impulse line", "Prozessanschluss / Wirkdruckleitung"),
            ("electric", "Electric signal (dashed)", "Elektrisches Signal (gestrichelt)"),
            ("pneumatic", "Pneumatic signal (double slashes)", "Pneumatisches Signal (Doppelstriche)"),
            ("capillary", "Capillary tube (crosses)", "Kapillarleitung (Kreuze)"),
            ("data", "Data link / bus (circles)", "Datenverbindung / Bus (Kreise)"));

        public override string Id => TypeId;
        public override I18nText Name => T("Signal line", "Signalleitung (Wirkungslinie)");
        public override string Category => ProcessCategories.Lines;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "signal line", "instrument line", "electric signal", "pneumatic signal", "capillary", "data link", "impulse line", "control line",
            "signalleitung", "wirkungslinie", "wirklinie", "messleitung", "elektrisches signal", "pneumatisches signal", "kapillare", "datenleitung",
        };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Kind, Routing.Param() };
        /// <summary>Instrument signals (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.Instrument;
        public override IReadOnlyList<string> Companions => new[] { Instruments.BubbleId, "process.valve.control", "process.instrument.orifice" };

        public static Style StyleOf(string kind) => kind == "electric" ? S.Thin with { Dash = DashStyle.Dash } : S.Thin;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var raw = ctx.Doc.ResolvePoints(o);
            if (raw.Count < 2) return Array.Empty<Primitive>();
            var route = Path(o, raw, ctx);
            if (route.Count < 2) return Array.Empty<Primitive>();
            var kind = o.Str("kind", "electric");
            var list = new List<Primitive> { Prim.Polyline(route, StyleOf(kind)) with { Tag = ProcessTags.Line } };
            const double step = 8, first = 5, gap = 3;
            switch (kind)
            {
                case "pneumatic":
                    foreach (var (at, dir) in Routing.Along(route, first, step, gap))
                    {
                        var n = dir.Perp;
                        foreach (var off in new[] { -0.45, 0.45 })
                        {
                            var c = at + dir * off;
                            var slash = (n + dir * 0.6).Normalized() * 1.1;
                            list.Add(Prim.Line(c - slash, c + slash, S.Thin) with { Tag = "mark" });
                        }
                    }
                    break;
                case "capillary":
                    foreach (var (at, dir) in Routing.Along(route, first, step, gap))
                    {
                        var a = (dir + dir.Perp).Normalized() * 0.9;
                        var b = (dir - dir.Perp).Normalized() * 0.9;
                        list.Add(Prim.Line(at - a, at + a, S.Thin) with { Tag = "mark" });
                        list.Add(Prim.Line(at - b, at + b, S.Thin) with { Tag = "mark" });
                    }
                    break;
                case "data":
                    foreach (var (at, _) in Routing.Along(route, first, step, gap))
                        list.Add(Prim.Circle(at.X, at.Y, 0.6, S.White with { StrokeWidth = 0.25 }) with { Tag = "mark" });
                    break;
            }
            list.AddRange(Junctions(o, raw, 0.5));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Cycle("process.signal.kind", T("Signal type", "Signalart"), Kind),
            RouteAction(o),
        };
    }
}
