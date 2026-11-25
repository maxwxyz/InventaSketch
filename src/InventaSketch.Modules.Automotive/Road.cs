using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Road environment in top view: straight segment, curved segment, intersection, roundabout, crosswalk and lane
    /// markings. Lane centrelines are exposed as <see cref="AnchorRole.Axis"/> segments so vehicles (Hub anchors)
    /// snap onto lanes, and as <see cref="AnchorRole.Rail"/> segments so trajectory and line points (End anchors)
    /// dock anywhere on a lane centre. Scale 1:100 like the vehicles (a lane is 35 mm wide by default).
    /// </summary>
    internal static class Road
    {
        public const string Cat = AutoCategories.Road;
        public const double LaneWidth = 35;

        /// <summary>A lane centreline: the Axis segment (vehicle hubs) plus the same segment as a Rail (points dock on it).</summary>
        private static IEnumerable<Anchor> LaneCentre(Anchor axis)
        {
            yield return axis;
            yield return axis with { Role = AnchorRole.Rail, Id = axis.Id is null ? null : axis.Id + ".rail" };
        }

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly ParamDef Lanes = Prm.Int("lanes", "Number of lanes", "Anzahl Fahrstreifen", 2, 1, 8);

        public static ParamDef Markings(string def) => Prm.Choice("markings", "Lane markings", "Fahrstreifenmarkierung", def,
            ("dashed", "Dashed", "Gestrichelt"), ("solid", "Solid", "Durchgezogen"), ("center-solid", "Solid centre line", "Durchgezogene Mittellinie"), ("none", "None", "Keine"));

        private static readonly ParamDef Centerlines = Prm.Bool("centerlines", "Show lane centre lines", "Fahrstreifenmitten zeigen", false);
        private static readonly ParamDef Dash = Prm.Num("dash", "Dash length", "Strichlänge", 10, 1, 100, "mm", 1, advanced: true);
        private static readonly ParamDef Gap = Prm.Num("gap", "Gap length", "Lückenlänge", 8, 1, 100, "mm", 1, advanced: true);

        public static IEnumerable<ObjectType> Types()
        {
            yield return Straight();
            yield return Curve();
            yield return Intersection();
            yield return Roundabout();
            yield return Crosswalk();
            yield return new MarkingType();
        }

        private static int LaneCount(SketchObject o, int max = 8) => Math.Max(1, Math.Min(max, o.Int("lanes", 2)));

        /// <summary>Draws one lane separator (actual coordinates) according to the marking pattern.</summary>
        public static void Separator(Pen p, IReadOnlyList<Vec2> actual, bool solid, double dash, double gap)
        {
            if (solid) { p.Add(Prim.Polyline(actual, S.Fine), AutoTags.Marking); return; }
            foreach (var d in Shapes.Dashes(actual, dash, gap, -gap / 2)) p.Add(Prim.Polyline(d, S.Fine), AutoTags.Marking);
        }

        /// <summary>Whether separator <paramref name="k"/> (1..n-1) is drawn, and whether it is solid.</summary>
        public static (bool Draw, bool Solid) Pattern(SketchObject o, int k, int n, string def)
        {
            var m = o.Str("markings", def);
            return m switch
            {
                "none" => (false, false),
                "solid" => (true, true),
                "center-solid" => (true, n % 2 == 0 && k == n / 2),
                _ => (true, false),
            };
        }

        private static ContextAction LanesAction(int delta, bool resize) => new(
            delta > 0 ? "param.lanes.inc" : "param.lanes.dec",
            delta > 0 ? T("Add lane", "Fahrstreifen hinzufügen") : T("Remove lane", "Fahrstreifen entfernen"),
            delta > 0 ? AutoIcons.Increase : AutoIcons.Decrease,
            x =>
            {
                var n = LaneCount(x);
                var m = Math.Max(1, Math.Min(8, n + delta));
                if (m == n) return x;
                var y = x.WithParam("lanes", m);
                return resize ? y with { Size = new Size2(x.Size.W, x.Size.H * m / n) } : y;
            }) { Group = "lanes" };

        private static IEnumerable<ContextAction> RoadActions(SketchObject o, bool resize, ParamDef markings)
        {
            yield return LanesAction(+1, resize);
            yield return LanesAction(-1, resize);
            yield return Prm.Cycle(o, markings, T("Next marking pattern", "Nächstes Markierungsmuster"));
            yield return Prm.Toggle(o, "centerlines", false, T("Lane centre lines", "Fahrstreifenmitten"));
        }

        private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ straight segment

        private static AutoType Straight()
        {
            var markings = Markings("dashed");
            void Draw(Pen p, SketchObject o)
            {
                double hw = p.W / 2, hh = p.H / 2;
                var n = LaneCount(o);
                var lw = p.H / n;
                p.Line(-hw, -hh, hw, -hh, S.Vis, AutoTags.Edge);
                p.Line(-hw, hh, hw, hh, S.Vis, AutoTags.Edge);
                for (var k = 1; k < n; k++)
                {
                    var (draw, solid) = Pattern(o, k, n, "dashed");
                    if (draw) Separator(p, new[] { new Vec2(-hw, -hh + k * lw), new Vec2(hw, -hh + k * lw) }, solid, o.Num("dash", 10), o.Num("gap", 8));
                }
                if (o.Flag("centerlines", false))
                    for (var i = 0; i < n; i++) p.Line(-hw, -hh + (i + 0.5) * lw, hw, -hh + (i + 0.5) * lw, S.Ctr, AutoTags.Lane);
            }
            IEnumerable<Anchor> Anchors(Pen p, SketchObject o)
            {
                double hw = p.W / 2, hh = p.H / 2;
                var n = LaneCount(o);
                var lw = p.H / n;
                for (var i = 0; i < n; i++)
                {
                    var y = -hh + (i + 0.5) * lw;
                    foreach (var a in LaneCentre(Anchor.Segment(AnchorRole.Axis, new Vec2(-hw, y), new Vec2(hw, y)) with { Id = "lane" + I(i) })) yield return a;
                    yield return Anchor.Point(AnchorRole.End, new Vec2(-hw, y), "lane" + I(i) + ".start");
                    yield return Anchor.Point(AnchorRole.End, new Vec2(hw, y), "lane" + I(i) + ".end");
                }
            }
            return new AutoType("auto.road.straight", T("Road segment (straight)", "Straßenabschnitt (gerade)"), Cat,
                new[] { "road", "lane", "street", "highway", "motorway", "straight", "straße", "fahrbahn", "fahrstreifen", "autobahn", "gerade" },
                Views.TopV(160, 2 * LaneWidth, Draw) with { Fluid = true, Anchors = Anchors })
            {
                Params = new[] { Lanes, markings, Centerlines, Dash, Gap },
                Uniform = false,
                Extra = o => RoadActions(o, true, markings),
            };
        }

        // ------------------------------------------------------------------ curved segment

        private static AutoType Curve()
        {
            var markings = Markings("dashed");
            const double R = 120;
            var c = new Vec2(-60, 60);
            (int N, double Lw, double Ri) Geometry(SketchObject o)
            {
                var n = LaneCount(o, 6);
                var lw = Math.Min(LaneWidth, 110.0 / n);
                return (n, lw, R - n * lw);
            }
            List<Vec2> ArcAt(Pen p, double r) => Pen.ArcD(c.X, c.Y, r, r, -90, 90, 48).Select(p.V).ToList();
            void Draw(Pen p, SketchObject o)
            {
                var (n, lw, ri) = Geometry(o);
                p.Add(Prim.Polyline(ArcAt(p, R), S.Vis), AutoTags.Edge);
                p.Add(Prim.Polyline(ArcAt(p, ri), S.Vis), AutoTags.Edge);
                for (var k = 1; k < n; k++)
                {
                    var (draw, solid) = Pattern(o, k, n, "dashed");
                    if (draw) Separator(p, ArcAt(p, ri + k * lw), solid, o.Num("dash", 10), o.Num("gap", 8));
                }
                if (o.Flag("centerlines", false))
                    for (var i = 0; i < n; i++) p.Add(Prim.Polyline(ArcAt(p, ri + (i + 0.5) * lw), S.Ctr), AutoTags.Lane);
            }
            IEnumerable<Anchor> Anchors(Pen p, SketchObject o)
            {
                var (n, lw, ri) = Geometry(o);
                for (var i = 0; i < n; i++)
                {
                    var pts = ArcAt(p, ri + (i + 0.5) * lw);
                    foreach (var c in Shapes.AxisChords(pts, 8)) foreach (var a in LaneCentre(c with { Id = "lane" + I(i) })) yield return a;
                    yield return Anchor.Point(AnchorRole.End, pts[0], "lane" + I(i) + ".start");
                    yield return Anchor.Point(AnchorRole.End, pts[pts.Count - 1], "lane" + I(i) + ".end");
                }
            }
            return new AutoType("auto.road.curve", T("Road segment (curve)", "Straßenabschnitt (Kurve)"), Cat,
                new[] { "road", "curve", "bend", "lane", "street", "straße", "kurve", "biegung", "fahrbahn" },
                Views.TopV(R, R, Draw) with { Anchors = Anchors })
            {
                Params = new[] { Lanes, markings, Centerlines, Dash, Gap },
                Uniform = false,
                Extra = o => RoadActions(o, false, markings),
            };
        }

        // ------------------------------------------------------------------ intersection

        private static List<Vec2> Fillet(Vec2 center, Vec2 from, Vec2 to, int n = 12)
        {
            var a0 = Math.Atan2(from.Y - center.Y, from.X - center.X);
            var a1 = Math.Atan2(to.Y - center.Y, to.X - center.X);
            var sweep = a1 - a0;
            while (sweep > Math.PI) sweep -= 2 * Math.PI;
            while (sweep <= -Math.PI) sweep += 2 * Math.PI;
            var r = (from - center).Length;
            return Geo.ArcPoints(center, r, r, a0, sweep, n);
        }

        private static readonly Vec2[] ArmDirs = { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };

        private static AutoType Intersection()
        {
            var markings = Markings("center-solid");
            var arms = Prm.Choice("arms", "Arms", "Arme", "4", ("4", "Four-way (cross)", "Kreuzung (4 Arme)"), ("3", "T-junction", "Einmündung (3 Arme)"));
            var stop = Prm.Bool("stopLines", "Stop lines", "Haltlinien", true);
            var zebra = Prm.Bool("crosswalks", "Crosswalks", "Fußgängerüberwege", false);
            const double E = 80, rf = 10;
            (int N, double Lw, double Hw) Geometry(SketchObject o)
            {
                var n = LaneCount(o, 6);
                var lw = Math.Min(LaneWidth, 120.0 / n);
                return (n, lw, n * lw / 2);
            }
            IEnumerable<Vec2> Dirs(SketchObject o) => o.Str("arms", "4") == "3" ? ArmDirs.Take(3) : ArmDirs;
            void Draw(Pen p, SketchObject o)
            {
                var (n, lw, hw) = Geometry(o);
                var tee = o.Str("arms", "4") == "3";
                foreach (var sx in new[] { -1.0, 1.0 })
                    foreach (var sy in new[] { -1.0, 1.0 })
                    {
                        if (tee && sy < 0) continue;
                        var curb = new List<Vec2> { new(sx * E, sy * hw), new(sx * (hw + rf), sy * hw) };
                        curb.AddRange(Fillet(new Vec2(sx * (hw + rf), sy * (hw + rf)), new Vec2(sx * (hw + rf), sy * hw), new Vec2(sx * hw, sy * (hw + rf))).Skip(1));
                        curb.Add(new Vec2(sx * hw, sy * E));
                        p.PolyD(curb, S.Vis, false, AutoTags.Edge);
                    }
                if (tee) p.Line(-E, -hw, E, -hw, S.Vis, AutoTags.Edge);
                var stopLine = hw + 3;
                foreach (var d in Dirs(o))
                {
                    var nrm = d.Perp;
                    for (var k = 1; k < n; k++)
                    {
                        var (draw, solid) = Pattern(o, k, n, "center-solid");
                        var off = -hw + k * lw;
                        if (draw) Separator(p, new[] { p.V(d * (stopLine + (o.Flag("crosswalks", false) ? 12 : 0)) + nrm * off), p.V(d * E + nrm * off) }, solid, o.Num("dash", 10), o.Num("gap", 8));
                    }
                    if (o.Flag("stopLines", true))
                        p.Line(d * stopLine - nrm * hw, d * stopLine, S.Vis, "stopline");
                    if (o.Flag("crosswalks", false))
                    {
                        var m = Math.Max(2, 2 * n + 1);
                        var bw = 2 * hw / (2 * m - 1);
                        for (var i = 0; i < m; i++)
                        {
                            var o0 = -hw + 2 * i * bw;
                            p.PolyD(new[] { d * (stopLine + 2) + nrm * o0, d * (stopLine + 10) + nrm * o0, d * (stopLine + 10) + nrm * (o0 + bw), d * (stopLine + 2) + nrm * (o0 + bw) }, S.Fine, true, "crosswalk");
                        }
                    }
                    if (o.Flag("centerlines", false))
                        for (var i = 0; i < n; i++)
                        {
                            var off = -hw + (i + 0.5) * lw;
                            p.Line(d * hw + nrm * off, d * E + nrm * off, S.Ctr, AutoTags.Lane);
                        }
                }
            }
            IEnumerable<Anchor> Anchors(Pen p, SketchObject o)
            {
                var (n, lw, hw) = Geometry(o);
                var j = 0;
                foreach (var d in Dirs(o))
                {
                    var nrm = d.Perp;
                    for (var i = 0; i < n; i++, j++)
                    {
                        var off = -hw + (i + 0.5) * lw;
                        foreach (var a in LaneCentre(Anchor.Segment(AnchorRole.Axis, p.V(d * hw + nrm * off), p.V(d * E + nrm * off)) with { Id = "lane" + I(j) })) yield return a;
                        yield return Anchor.Point(AnchorRole.End, p.V(d * E + nrm * off), "lane" + I(j) + ".end");
                    }
                }
            }
            return new AutoType("auto.road.intersection", T("Intersection", "Kreuzung"), Cat,
                new[] { "intersection", "junction", "crossroads", "t-junction", "kreuzung", "einmündung", "knotenpunkt", "straße" },
                Views.TopV(2 * E, 2 * E, Draw) with { Anchors = Anchors })
            {
                Params = new[] { Lanes, markings, arms, stop, zebra, Centerlines, Dash, Gap },
                Uniform = false,
                Extra = o => RoadActions(o, false, markings).Append(Prm.Cycle(o, arms, T("Cross / T-junction", "Kreuzung / Einmündung")))
                    .Append(Prm.Toggle(o, "crosswalks", false, T("Crosswalks", "Fußgängerüberwege"))),
            };
        }

        // ------------------------------------------------------------------ roundabout

        private static AutoType Roundabout()
        {
            // Arms one lane each way at the road lane width (a 2-lane road docks on an arm; before, arm lanes were 20 wide
            // against 35 of the roads); ring outer radius, island and arm length follow from it.
            const double ha = LaneWidth, Ro = 70, Ri = 36, E = 100;
            var ringLanes = Prm.Int("ringLanes", "Lanes in the ring", "Fahrstreifen im Kreis", 1, 1, 2);
            var xint = Math.Sqrt(Ro * Ro - ha * ha);
            var beta = Math.Asin(ha / Ro);
            void Draw(Pen p, SketchObject o)
            {
                for (var k = 0; k < 4; k++)
                {
                    var a0 = k * Math.PI / 2 + beta;
                    p.Add(Prim.Polyline(Geo.ArcPoints(Vec2.Zero, Ro, Ro, a0, Math.PI / 2 - 2 * beta, 24).Select(p.V).ToList(), S.Vis), AutoTags.Edge);
                }
                p.Circle(0, 0, Ri, S.Vis, "island");
                p.Circle(0, 0, Ri + 4, S.Thin with { Dash = DashStyle.Dash }, "apron");
                if (o.Int("ringLanes", 1) >= 2)
                    Separator(p, Geo.ArcPoints(Vec2.Zero, (Ro + Ri + 4) / 2, (Ro + Ri + 4) / 2, 0, 2 * Math.PI, 96).Select(p.V).ToList(), false, o.Num("dash", 10) * 0.6, o.Num("gap", 8) * 0.6);
                foreach (var d in ArmDirs)
                {
                    var nrm = d.Perp;
                    foreach (var s in new[] { -1.0, 1.0 }) p.Line(d * xint + nrm * s * ha, d * E + nrm * s * ha, S.Vis, AutoTags.Edge);
                    p.Rounded(new[] { d * (Ro + 1.5) - nrm * 7, d * (Ro + 18), d * (Ro + 1.5) + nrm * 7 }, 1.5, S.Fine, "splitter");
                    Separator(p, new[] { p.V(d * (Ro + 20)), p.V(d * E) }, false, o.Num("dash", 10), o.Num("gap", 8));
                    if (o.Flag("centerlines", false))
                        foreach (var s in new[] { -1.0, 1.0 }) p.Line(d * xint + nrm * s * ha / 2, d * E + nrm * s * ha / 2, S.Ctr, AutoTags.Lane);
                }
                if (o.Flag("centerlines", false)) p.Circle(0, 0, (Ro + Ri + 4) / 2, S.Ctr, AutoTags.Lane);
            }
            IEnumerable<Anchor> Anchors(Pen p, SketchObject o)
            {
                var rc = (Ro + Ri + 4) / 2;
                var ring = Geo.ArcPoints(Vec2.Zero, rc, rc, 0, 2 * Math.PI, 64).Select(p.V).ToList();
                foreach (var c in Shapes.AxisChords(ring, 16)) foreach (var a in LaneCentre(c with { Id = "ring" })) yield return a;
                var j = 0;
                foreach (var d in ArmDirs)
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        var off = d.Perp * s * ha / 2;
                        foreach (var a in LaneCentre(Anchor.Segment(AnchorRole.Axis, p.V(d * xint + off), p.V(d * E + off)) with { Id = "lane" + I(j) })) yield return a;
                        yield return Anchor.Point(AnchorRole.End, p.V(d * E + off), "lane" + I(j++) + ".end");
                    }
            }
            return new AutoType("auto.road.roundabout", T("Roundabout", "Kreisverkehr"), Cat,
                new[] { "roundabout", "traffic circle", "rotary", "kreisverkehr", "kreisel", "straße" },
                Views.TopV(2 * E, 2 * E, Draw) with { Anchors = Anchors })
            {
                Params = new[] { ringLanes, Centerlines, Dash, Gap },
                Extra = o => new[] { Prm.Toggle(o, "centerlines", false, T("Lane centre lines", "Fahrstreifenmitten")) },
            };
        }

        // ------------------------------------------------------------------ crosswalk

        private static AutoType Crosswalk()
        {
            var stripes = Prm.Int("stripes", "Number of stripes", "Anzahl Streifen", 7, 2, 30);
            void Draw(Pen p, SketchObject o)
            {
                var n = Math.Max(2, Math.Min(30, o.Int("stripes", 7)));
                double hw = p.W / 2, hh = p.H / 2, h = p.H / (2 * n - 1);
                for (var i = 0; i < n; i++)
                    p.Add(Prim.Rect(-hw, -hh + 2 * i * h, p.W, h, S.Fine), "stripe");
            }
            return new AutoType("auto.road.crosswalk", T("Crosswalk", "Fußgängerüberweg"), Cat,
                new[] { "crosswalk", "zebra crossing", "pedestrian crossing", "zebrastreifen", "fußgängerüberweg", "überweg" },
                Views.TopV(30, 2 * LaneWidth, Draw) with { Fluid = true })
            {
                Params = new[] { stripes },
                Uniform = false,
                Extra = o => Prm.PlusMinus("stripes", 7, 2, 30, 1, false, T("More stripes", "Mehr Streifen"), T("Fewer stripes", "Weniger Streifen"), "param"),
            };
        }

        // ------------------------------------------------------------------ lane marking (point object)

        /// <summary>Lane marking line through the points: solid, dashed or double, optionally smoothed.</summary>
        internal sealed class MarkingType : AutoPointType
        {
            private static readonly ParamDef PatternDef = Prm.Choice("pattern", "Pattern", "Muster", "dashed",
                ("dashed", "Dashed", "Gestrichelt"), ("solid", "Solid", "Durchgezogen"), ("double", "Double solid", "Doppelt durchgezogen"));
            private static readonly ParamDef Smooth = Prm.Bool("smooth", "Smooth curve", "Glatte Kurve", false);

            public MarkingType() : base("auto.marking", T("Lane marking", "Fahrbahnmarkierung"), Cat,
                "lane marking", "road marking", "dashed line", "solid line", "markierung", "fahrbahnmarkierung", "leitlinie", "fahrstreifenbegrenzung") { }

            public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-40, 0), new Vec2(40, 0) };
            public override bool InsertablePoints => true;
            public override IReadOnlyList<ParamDef> Parameters => new[] { PatternDef, Smooth, Dash, Gap };

            protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            {
                var line = o.Flag("smooth", false) && p.Count > 2 ? Shapes.Sample(Shapes.Spline(p)) : p.ToList();
                var list = new List<Primitive>();
                switch (o.Str("pattern", "dashed"))
                {
                    case "solid":
                        list.Add(Prim.Polyline(line, S.Vis) with { Tag = AutoTags.Marking });
                        break;
                    case "double":
                        list.Add(Prim.Polyline(Shapes.Offset(line, -0.6), S.Fine) with { Tag = AutoTags.Marking });
                        list.Add(Prim.Polyline(Shapes.Offset(line, 0.6), S.Fine) with { Tag = AutoTags.Marking });
                        break;
                    default:
                        foreach (var d in Shapes.Dashes(line, Math.Max(0.5, o.Num("dash", 10)), Math.Max(0.5, o.Num("gap", 8))))
                            list.Add(Prim.Polyline(d, S.Vis) with { Tag = AutoTags.Marking });
                        break;
                }
                return list;
            }

            public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            {
                yield return Prm.Cycle(o, PatternDef, T("Solid / dashed / double", "Durchgezogen / gestrichelt / doppelt"));
                yield return Prm.Toggle(o, "smooth", false, T("Smooth curve", "Glatte Kurve"));
            }
        }
    }
}
