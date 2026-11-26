using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Linear axis ("7th axis"): a rail with a carriage at "pos" (0 = start, 1 = end of the travel). A robot base docks on the
    /// carriage (mount "carriage") and moves with it: the carriage also carries a directed mount without an id, and a
    /// floor-standing robot's base a matching one, so <see cref="Coupling"/> moves the robot when the carriage moves.
    /// </summary>
    internal static class Track
    {
        public const string TrackId = "robot.track.linear";

        /// <summary>The carriage centre travels between design x −30 and 30.</summary>
        private const double Half = 30;

        private static double Pos(SketchObject o) => Geo.Clamp(o.Num("pos", 0.25), 0, 1);
        private static double Cx(SketchObject o) => -Half + 2 * Half * Pos(o);

        private static readonly I18nText GripLabel = new("Carriage position", "Schlittenposition");

        public static ObjectType Linear() => new RoboBoxType(TrackId, new I18nText("Linear axis (7th axis)", "Linearachse (7. Achse)"), RoboCategories.Robots,
            new[] { "linear axis", "linear track", "7th axis", "seventh axis", "robot track", "track motion", "rail", "linearachse", "7. achse", "siebte achse", "verfahrachse", "fahrachse", "lineareinheit", "schiene" },
            new SymView(RoboViews.SideView, new Size2(80, 8), (p, o) =>
            {
                var cx = Cx(o);
                if (o.Flag("ends", false))
                    foreach (var e in new[] { -Half, Half }) p.Box(e - 7, -4, e + 7, 0, RS.Ghost, RoboTags.Ghost);
                p.Box(-40, 0, 40, 2.6, RS.Body, RoboTags.Base);
                p.Line(-39, 0.9, 39, 0.9, RS.Fine);
                foreach (var x in new[] { -37.0, 0.0, 37.0 }) p.Box(x - 2, 2.6, x + 2, 4, RS.Dark, RoboTags.Base);
                p.Box(-40, -1.2, -39, 0, RS.Dark, RoboTags.Base);
                p.Box(39, -1.2, 40, 0, RS.Dark, RoboTags.Base);
                p.Box(cx - 7, -3, cx + 7, 0, RS.Body, RoboTags.Link);
                p.Box(cx - 6, -4, cx + 6, -3, RS.Dark, RoboTags.Link);
                p.Box(cx + 7, -2.6, cx + 9.5, -0.4, RS.Dark, RoboTags.Link);
            }, (p, o) =>
            {
                var cx = Cx(o);
                return new[]
                {
                    RoboBoxType.Mount(p, cx, -4, "carriage"), RoboBoxType.Carrier(p, cx, -4), RoboBoxType.Port(p, cx - 7, -1.5, "chain"),
                    RoboBoxType.Rail(p, -39, 0, 39, 0, "rail"), RoboBoxType.Mount(p, 0, 4, "floor"),
                };
            }) { Open = new OpenGrip(new Vec2(-Half, -1.5), new Vec2(2 * Half, 0)) { Key = "pos", Default = 0.25, Label = GripLabel } },
            new SymView(RoboViews.TopView, new Size2(80, 14), (p, o) =>
            {
                var cx = Cx(o);
                if (o.Flag("ends", false))
                    foreach (var e in new[] { -Half, Half }) p.Box(e - 7, -6.5, e + 7, 6.5, RS.Ghost, RoboTags.Ghost);
                p.Box(-40, -5, 40, -3, RS.Body, RoboTags.Base);
                p.Box(-40, 3, 40, 5, RS.Body, RoboTags.Base);
                p.Line(-39, 0, 39, 0, RS.Center, RoboTags.Axis);
                p.Box(-40, -6, -39, 6, RS.Dark, RoboTags.Base);
                p.Box(39, -6, 40, 6, RS.Dark, RoboTags.Base);
                p.Box(cx - 7, -6.5, cx + 7, 6.5, RS.Body, RoboTags.Link);
                foreach (var x in new[] { -4.6, 4.6 }) foreach (var y in new[] { -4.6, 4.6 }) p.Round(cx + x, y, 0.5, RS.White, RoboTags.Link);
                p.Line(cx - 1.2, 0, cx + 1.2, 0, RS.Center); p.Line(cx, -1.2, cx, 1.2, RS.Center);
                p.Box(cx + 7, -2, cx + 9.5, 2, RS.Dark, RoboTags.Link);
            }, (p, o) =>
            {
                var cx = Cx(o);
                return new[] { RoboBoxType.Mount(p, cx, 0, "carriage"), RoboBoxType.Carrier(p, cx, 0), RoboBoxType.Port(p, cx - 7, 0, "chain") };
            }) { Open = new OpenGrip(new Vec2(-Half, 3.2), new Vec2(2 * Half, 0)) { Key = "pos", Default = 0.25, Label = GripLabel } })
        {
            Params = new[]
            {
                Pm.Num("pos", "Carriage position (0 = start, 1 = end)", "Schlittenposition (0 = Anfang, 1 = Ende)", 0.25, 0, 1, null, 0.05),
                Pm.Bool("ends", "End positions (phantom lines)", "Endlagen (Strich-Zweipunkt-Linien)", false),
            },
            CompanionIds = new[] { "robot.arm.articulated", "robot.cable.dragchain", "robot.reach.envelope" },
            Extra = o => new[]
            {
                Rb.Toggle(o, "robot.track.ends", new I18nText("End positions", "Endlagen"), IconIds.Ghosts, "ends", false, "motion"),
            },
        };
    }

    /// <summary>
    /// Energy chain (drag chain) along a path between two ends: a band of links whose corners are rounded with the bend
    /// radius, with an end bracket at both ends. The ends dock like line ends (onto the carriage port "chain" of a linear
    /// axis, a rail or any port) and follow what they are docked on.
    /// </summary>
    internal sealed class DragChainType : RoboPointType
    {
        public override string Id => "robot.cable.dragchain";
        public override I18nText Name => new("Energy chain (drag chain)", "Energiekette (Schleppkette)");
        public override string Category => RoboCategories.Cell;
        public override IReadOnlyList<string> Keywords => new[] { "energy chain", "drag chain", "cable carrier", "cable chain", "e-chain", "energiekette", "schleppkette", "kabelschlepp", "energieführungskette", "leitungsführung" };
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-24, 4), new Vec2(12, 4), new Vec2(12, -4), new Vec2(-8, -4) };
        public override IReadOnlyList<string> Companions => new[] { Track.TrackId, "robot.arm.articulated" };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Num("radius", "Bend radius on the page", "Biegeradius auf dem Blatt", 4, 0.5, 100, "mm", 0.5),
            Pm.Num("width", "Chain height on the page", "Kettenhöhe auf dem Blatt", 2.4, 0.5, 20, "mm", 0.1),
            Pm.Num("pitch", "Link pitch on the page", "Gliederteilung auf dem Blatt", 2.4, 0.5, 50, "mm", 0.1),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var list = new List<Primitive>();
            if (p.Count < 2) return list;
            var w = Geo.Clamp(o.Num("width", 2.4), 0.5, 20);
            var r = Math.Max(Geo.Clamp(o.Num("radius", 4), 0.5, 100), 0.6 * w);
            var pitch = Geo.Clamp(o.Num("pitch", 2.4), 0.5, 50);
            var path = Fillet(p, r);
            if (path.Count < 2) return list;
            var left = Offset(path, w / 2);
            var right = Offset(path, -w / 2);
            var band = left.Concat(Enumerable.Reverse(right)).ToList();
            list.Add(Prim.Polygon(band, RS.Body) with { Tag = RoboTags.Chain });
            // Link joints every pitch along the path.
            var total = 0.0;
            for (var i = 1; i < path.Count; i++) total += path[i].DistanceTo(path[i - 1]);
            var count = (int)Math.Floor(total / pitch - 1e-9);
            if (count > 400) { count = 400; pitch = total / 401; }
            var seg = 1; var acc = 0.0;
            for (var k = 1; k <= count; k++)
            {
                var s = k * pitch;
                while (seg < path.Count - 1 && acc + path[seg].DistanceTo(path[seg - 1]) < s) { acc += path[seg].DistanceTo(path[seg - 1]); seg++; }
                var a = path[seg - 1]; var b = path[seg];
                var len = a.DistanceTo(b);
                if (len < 1e-9) continue;
                var q = Vec2.Lerp(a, b, Geo.Clamp((s - acc) / len, 0, 1));
                var n = ((b - a) / len).Perp * (w / 2);
                list.Add(Prim.Line(q + n, q - n, RS.Thin) with { Tag = RoboTags.Chain });
            }
            list.Add(Bracket(path[0], path[1] - path[0], w));
            list.Add(Bracket(path[path.Count - 1], path[path.Count - 1] - path[path.Count - 2], w));
            return list;
        }

        /// <summary>End bracket across the chain at <paramref name="at"/> (along the end tangent <paramref name="dir"/>).</summary>
        private static Primitive Bracket(Vec2 at, Vec2 dir, double w)
        {
            var u = dir.Length < 1e-9 ? new Vec2(1, 0) : dir.Normalized();
            var v = u.Perp * (w / 2 + 0.6);
            var h = u * 0.8;
            return Prim.Polygon(new[] { at - h - v, at + h - v, at + h + v, at - h + v }, RS.Dark) with { Tag = RoboTags.Base };
        }

        /// <summary>
        /// The polyline with its inner corners rounded by arcs of radius <paramref name="r"/> (a corner takes at most half of
        /// a shared segment, the whole first or last segment).
        /// </summary>
        internal static List<Vec2> Fillet(IReadOnlyList<Vec2> p, double r)
        {
            var res = new List<Vec2> { p[0] };
            for (var i = 1; i < p.Count - 1; i++)
            {
                Vec2 a = p[i - 1], b = p[i], c = p[i + 1];
                double la = a.DistanceTo(b), lc = c.DistanceTo(b);
                if (la < 1e-9 || lc < 1e-9) { res.Add(b); continue; }
                var u = (a - b) / la; var v = (c - b) / lc;
                var theta = Math.Acos(Geo.Clamp(u.Dot(v), -1, 1));
                if (theta > Math.PI - 1e-6 || theta < 1e-6) { res.Add(b); continue; }
                var t = r / Math.Tan(theta / 2);
                t = Math.Min(t, Math.Min(la * (i - 1 == 0 ? 1 : 0.5), lc * (i + 1 == p.Count - 1 ? 1 : 0.5)));
                var rr = t * Math.Tan(theta / 2);
                var s = b + u * t; var e = b + v * t;
                var bis = (u + v).Normalized();
                var centre = b + bis * (rr / Math.Sin(theta / 2));
                var a0 = Math.Atan2(s.Y - centre.Y, s.X - centre.X);
                var a1 = Math.Atan2(e.Y - centre.Y, e.X - centre.X);
                var sweep = a1 - a0;
                while (sweep > Math.PI) sweep -= 2 * Math.PI;
                while (sweep < -Math.PI) sweep += 2 * Math.PI;
                var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / Geo.DegToRad(10)));
                for (var k = 0; k <= steps; k++)
                {
                    var ang = a0 + sweep * k / steps;
                    res.Add(centre + new Vec2(rr * Math.Cos(ang), rr * Math.Sin(ang)));
                }
            }
            res.Add(p[p.Count - 1]);
            // Drop repeated points (a corner that used up a whole segment).
            var clean = new List<Vec2> { res[0] };
            foreach (var q in res.Skip(1)) if (q.DistanceTo(clean[clean.Count - 1]) > 1e-9) clean.Add(q);
            return clean;
        }

        /// <summary>The polyline offset sideways by <paramref name="d"/> (normals from the neighbouring points).</summary>
        private static List<Vec2> Offset(IReadOnlyList<Vec2> p, double d)
        {
            var res = new List<Vec2>(p.Count);
            for (var i = 0; i < p.Count; i++)
            {
                var t = p[Math.Min(p.Count - 1, i + 1)] - p[Math.Max(0, i - 1)];
                var n = t.Length < 1e-12 ? new Vec2(0, 1) : t.Normalized().Perp;
                res.Add(p[i] + n * d);
            }
            return res;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();
    }
}
