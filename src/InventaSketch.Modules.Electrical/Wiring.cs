using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Base class of the electrical point-geometry types (wires, buses, cable routes, PCB tracks).</summary>
    internal abstract class ElecPointType : ObjectType
    {
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<ViewDef> _views;

        protected ElecPointType(string id, I18nText name, string category, ViewDef view, params string[] keywords)
        {
            Id = id; Name = name; Category = category; _keywords = keywords; _views = new[] { view };
        }

        public override string Id { get; }
        public override I18nText Name { get; }
        public override string Category { get; }
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _views;
        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            return pts.Count < 2 ? Array.Empty<Primitive>() : RenderPoints(o, pts, ctx);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx);
    }

    /// <summary>Routing of wires and tracks between their points: free (straight), orthogonal or 45 degrees.</summary>
    internal static class Routing
    {
        public static readonly string[] Modes = { "free", "orthogonal", "45" };

        /// <summary>
        /// Junction dots where an end of the line is docked onto another line (a T junction, <see cref="Polylines.Branch"/>):
        /// the connection is drawn with a dot, as is usual in circuit diagrams.
        /// </summary>
        public static IEnumerable<Primitive> BranchDots(SketchObject o, IReadOnlyList<Vec2> p)
        {
            if (o.Attach is null) yield break;
            foreach (var i in new[] { 0, p.Count - 1 })
                if (i < o.Attach.Count && o.Attach[i]?.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal))
                    yield return Prim.Circle(p[i].X, p[i].Y, 0.8, S.Black) with { Tag = "junction" };
        }

        public static ParamDef Param(string def) => Choice("route", T("Routing", "Leitungsführung"), def,
            ("free", "Free (straight)", "Frei (gerade)"), ("orthogonal", "Orthogonal", "Orthogonal"), ("45", "45°", "45°")).WithOptionIcons(IconIds.ForRoute);

        public static ContextAction CycleAction(SketchObject o, string def) => new("elec.route.next",
            T("Next routing (free / orthogonal / 45°)", "Nächste Führung (frei / orthogonal / 45°)"), IconIds.Cycle, x =>
            {
                var i = Array.IndexOf(Modes, x.Str("route", def));
                return x.WithParam("route", Modes[(i + 1 + Modes.Length) % Modes.Length]);
            }) { Group = "route", ParamKeys = new[] { "route" } };

        /// <summary>
        /// Exit direction (on the page) of an attached end: perpendicular to the box edge of the target the pin sits on,
        /// honouring the target's rotation. Null for free ends and pins inside the box.
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
                var a = pts[i]; var b = pts[i + 1];
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
                else if (mode == "45" && Math.Abs(d.X) > 1e-9 && Math.Abs(d.Y) > 1e-9 && Math.Abs(Math.Abs(d.X) - Math.Abs(d.Y)) > 1e-9)
                {
                    // Straight run along the dominant axis, then a 45 degree diagonal into the target.
                    var ax = Math.Abs(d.X); var ay = Math.Abs(d.Y);
                    result.Add(ax > ay ? new Vec2(a.X + Math.Sign(d.X) * (ax - ay), a.Y) : new Vec2(a.X, a.Y + Math.Sign(d.Y) * (ay - ax)));
                }
                result.Add(b);
            }
            return result;
        }

        /// <summary>
        /// <see cref="AnchorRole.Rail"/> anchors along a polyline (ids <c>rail0</c>, <c>rail1</c>, ...): other wire ends,
        /// junction dots and pins dock anywhere on it.
        /// </summary>
        public static IEnumerable<Anchor> Rails(IReadOnlyList<Vec2> p)
        {
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > Geo.Eps) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Midpoint and direction of the longest segment (label placement).</summary>
        public static (Vec2 Mid, Vec2 Dir) LongestSegment(IReadOnlyList<Vec2> p)
        {
            var best = 0; var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var dir = (p[best + 1] - p[best]).Normalized();
            if (dir.Length < Geo.Eps) dir = new Vec2(1, 0);
            return (Vec2.Lerp(p[best], p[best + 1], 0.5), dir);
        }

        /// <summary>Unit normal pointing up on the page (or left for vertical segments).</summary>
        public static Vec2 UpNormal(Vec2 dir)
        {
            var n = dir.Perp;
            return n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0) ? -n : n;
        }
    }

    /// <summary>Wire: polyline between pins with free, orthogonal or 45 degree routing. Ends snap to symbol pins.</summary>
    internal sealed class WireType : ElecPointType
    {
        public const string TypeId = "elec.wire";

        public WireType() : base(TypeId, T("Wire", "Leitung"), Connectors.Cat, new ViewDef("symbol", T("Wire", "Leitung")) { Icon = IconIds.Symbol },
            "wire", "connection", "conductor", "net", "leitung", "verbindung", "draht", "verdrahtung") { }

        private static readonly ParamDef Hops = Flag("hops", T("Hops at crossings", "Brücken an Kreuzungen"), true);

        public override IReadOnlyList<ParamDef> Parameters => new[] { Routing.Param("orthogonal"), Hops };
        public override bool InsertablePoints => true;
        /// <summary>A wire carries any electrical connection (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.Electric;

        /// <summary>Drawn path of a wire (routed).</summary>
        private static IReadOnlyList<Vec2> PathOf(SketchObject x, RenderContext ctx)
        {
            var q = ctx.Doc.ResolvePoints(x);
            return q.Count < 2 ? q : RouteOf(x, q, ctx);
        }

        /// <summary>A wire drawn over an earlier wire hops over it (unless switched off); hosts offer to connect there.</summary>
        public override bool HopsAtCrossings => true;
        public override bool DependsOnNeighbours => true;

        /// <summary>Wires crossing this one (their hops depend on where this one runs).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) return Array.Empty<string>();
            return Crossings.Find(ctx.Doc, o, RouteOf(o, p, ctx), x => PathOf(x, ctx), ctx.Registry ?? Owner).Select(c => c.Other).Distinct();
        }

        /// <summary>On a wire one adds components: offered by "Insert ▸".</summary>
        public override IReadOnlyList<string> Companions => new[] { "elec.resistor", "elec.capacitor", "elec.switch", "elec.diode", "elec.lamp", "elec.fuse", "elec.ground", "elec.junction" };

        private static List<Vec2> RouteOf(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
            => Routing.Route(p, o.Str("route", "orthogonal"), Routing.ExitHorizontal(ctx.Doc, o, 0), Routing.ExitHorizontal(ctx.Doc, o, p.Count - 1));

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
        {
            var route = RouteOf(o, p, ctx);
            var hops = o.Flag("hops", true) ? Crossings.Find(ctx.Doc, o, route, x => PathOf(x, ctx), ctx.Registry ?? Owner).Where(c => c.Above).ToList() : new List<Crossing>();
            Primitive main = hops.Count == 0 ? Prim.Polyline(route, S.Wire) : Prim.Path(Crossings.HopPath(route, hops), S.Wire);
            return new[] { main with { Tag = "wire" } }.Concat(Routing.BranchDots(o, p)).ToList();
        }

        /// <summary>The routed path is a rail: other wire ends and junction dots dock anywhere on it (T junctions).</summary>
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            return p.Count < 2 ? Array.Empty<Anchor>() : Routing.Rails(RouteOf(o, p, ctx));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Routing.CycleAction(o, "orthogonal"),
            Toggle(o, "elec.wire.hops", T("Hops at crossings on/off", "Brücken an Kreuzungen an/aus"), "hops", true, IconIds.Hop),
        };
    }

    /// <summary>
    /// Bus line (CAN, LIN, Ethernet, FlexRay or generic) with its protocol label; taps every 10 mm are ports and the line
    /// itself is a rail, so stubs dock on a tap or anywhere in between.
    /// </summary>
    internal sealed class BusType : ElecPointType
    {
        public const double TapSpacing = 10;

        private static readonly ParamDef Protocol = Choice("protocol", T("Protocol", "Protokoll"), "can",
            ("can", "CAN", "CAN"), ("lin", "LIN", "LIN"), ("ethernet", "Automotive Ethernet", "Automotive Ethernet"), ("flexray", "FlexRay", "FlexRay"), ("generic", "Generic bus", "Allgemeiner Bus"));

        public BusType() : base("elec.bus", T("Bus line", "Busleitung"), Connectors.Cat, new ViewDef("symbol", T("Bus line", "Busleitung")) { Icon = IconIds.Symbol },
            "bus", "can", "lin", "ethernet", "flexray", "network", "busleitung", "netzwerk", "bordnetz") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, 0), new Vec2(30, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Protocol };
        public override bool InsertablePoints => true;
        /// <summary>A bus carries data (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.Data;
        public override bool HasText => true;

        public static string Label(SketchObject o) => !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("protocol", "can") switch
        {
            "lin" => "LIN",
            "ethernet" => "Ethernet",
            "flexray" => "FlexRay",
            "generic" => "Bus",
            _ => "CAN",
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
        {
            var list = new List<Primitive> { Prim.Polyline(p, S.Bus) with { Tag = "bus" } };
            var (mid, dir) = Routing.LongestSegment(p);
            var pos = mid + Routing.UpNormal(dir) * 2.6;
            list.Add(Prim.Text(pos.X, pos.Y, Label(o), new TextStyle { Size = 2.8, Bold = true }) with { Tag = "protocol" });
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var k = 0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var len = p[i].DistanceTo(p[i + 1]);
                for (var s = 0.0; s <= len + 1e-9; s += TapSpacing)
                    yield return Anchor.Point(AnchorRole.Port, Vec2.Lerp(p[i], p[i + 1], len < Geo.Eps ? 0 : s / len), "tap" + (k++).ToString(CultureInfo.InvariantCulture));
            }
            foreach (var rail in Routing.Rails(p)) yield return rail;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => new[] { Cycle(o, "elec.bus.next", T("Next bus protocol", "Nächstes Busprotokoll"), Protocol) };
    }
}
