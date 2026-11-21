using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>Views shared by the point elements.</summary>
    internal static class PointViews
    {
        public static readonly IReadOnlyList<ViewDef> Two = new[]
        {
            new ViewDef("schematic", ViewSpec.SchematicLabel) { Icon = IconIds.Schematic },
            new ViewDef("detailed", ViewSpec.DetailedLabel) { Icon = IconIds.Detailed },
        };
    }

    /// <summary>
    /// Rigid binary link: bar between two joint centres (the two points), joint circles at the ends. The ends are
    /// <see cref="AnchorRole.Port"/> anchors, so links snap joint to joint.
    /// </summary>
    internal sealed class BinaryLinkType : KinPointType
    {
        public BinaryLinkType() : base("kin.link.binary", new I18nText("Rigid link (bar)", "Starres Glied (Stab)"), KinCategories.Links,
            "link", "bar", "rod", "rigid link", "binary link", "coupler", "glied", "stab", "koppel", "zweigelenkglied", "lenker")
        { }

        public override IReadOnlyList<ViewDef> Views => PointViews.Two;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override LinkageRole LinkageRole(SketchObject o) => InventaSketch.Core.LinkageRole.Link;
        protected override int Slots => 1;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Bool("startJoint", "Joint at start", "Gelenk am Anfang", true),
            KP.Bool("endJoint", "Joint at end", "Gelenk am Ende", true),
            JointParam,
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var r = JointRadius(o);
            var a = p[0]; var b = p[p.Count - 1];
            var list = new List<Primitive>();
            var detailed = ViewOf(o) == "detailed";
            if (detailed) list.Add(Frag.Capsule(a, b, 1.6 * r, KS.Body) with { Tag = KinTags.Link });
            else list.Add(Prim.Line(a, b, KS.Link) with { Tag = KinTags.Link });
            if (o.Flag("startJoint", true)) list.Add(Prim.Circle(a.X, a.Y, detailed ? 0.75 * r : r, KS.Joint) with { Tag = KinTags.Joint });
            if (o.Flag("endJoint", true)) list.Add(Prim.Circle(b.X, b.Y, detailed ? 0.75 * r : r, KS.Joint) with { Tag = KinTags.Joint });
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return KA.Toggle(o, "kin.joint.start", new I18nText("Joint at start", "Gelenk am Anfang"), IconIds.ArrowStart, "startJoint", true, "joints");
            yield return KA.Toggle(o, "kin.joint.end", new I18nText("Joint at end", "Gelenk am Ende"), IconIds.ArrowEnd, "endJoint", true, "joints");
        }
    }

    /// <summary>Ternary link: rigid plate with three joints (the three points).</summary>
    internal sealed class TernaryLinkType : KinPointType
    {
        public TernaryLinkType() : base("kin.link.ternary", new I18nText("Ternary link (plate)", "Dreigelenkglied (Platte)"), KinCategories.Links,
            "ternary link", "plate", "triangle link", "rigid body", "dreigelenkglied", "koppelplatte", "platte", "dreieck")
        { }

        public override IReadOnlyList<ViewDef> Views => PointViews.Two;
        protected override int MinPoints => 3;
        public override LinkageRole LinkageRole(SketchObject o) => InventaSketch.Core.LinkageRole.Link;
        protected override int Slots => 2;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-14, 8), new Vec2(14, 8), new Vec2(0, -12) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { JointParam };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var r = JointRadius(o);
            var tri = p.Take(3).ToList();
            var list = new List<Primitive>();
            if (ViewOf(o) == "detailed")
            {
                list.Add(Prim.Polygon(RoundedHull(tri, 1.6 * r), KS.Body) with { Tag = KinTags.Link });
                foreach (var q in tri) list.Add(Prim.Circle(q.X, q.Y, 0.75 * r, KS.Joint) with { Tag = KinTags.Joint });
                return list;
            }
            // Rigid plate: outline through the joints, light hatch marks the body as one rigid link (ISO 3952 style).
            var spacing = Geo.Clamp(Math.Sqrt(Math.Abs(Geo.PolygonArea(tri))) * 0.12, 0.8, 2.5);
            list.Add(Prim.Polygon(tri, KS.Link with { Fill = Fill.Hatched(new HatchPattern(45, spacing, Width: 0.18)) }) with { Tag = KinTags.Link });
            foreach (var q in tri) list.Add(Prim.Circle(q.X, q.Y, r, KS.Joint) with { Tag = KinTags.Joint });
            return list;
        }

        /// <summary>Outline of the union of circles (radius rr) around the vertices of a triangle (convex hull).</summary>
        private static List<Vec2> RoundedHull(IReadOnlyList<Vec2> tri, double rr)
        {
            var pts = tri.ToList();
            if (Geo.PolygonArea(pts) < 0) pts.Reverse(); // positive area = clockwise on screen (y down)
            var res = new List<Vec2>();
            for (var i = 0; i < pts.Count; i++)
            {
                var prev = pts[(i + pts.Count - 1) % pts.Count];
                var cur = pts[i];
                var next = pts[(i + 1) % pts.Count];
                // Outward normals of the edges prev->cur and cur->next (clockwise polygon on screen: outward = -Perp).
                var n0 = -(cur - prev).Normalized().Perp;
                var n1 = -(next - cur).Normalized().Perp;
                var a0 = Math.Atan2(n0.Y, n0.X);
                var a1 = Math.Atan2(n1.Y, n1.X);
                var sweep = a1 - a0;
                while (sweep < 0) sweep += 2 * Math.PI;
                while (sweep > 2 * Math.PI) sweep -= 2 * Math.PI;
                res.AddRange(Geo.ArcPoints(cur, rr, rr, a0, sweep, 10));
            }
            return res;
        }
    }

    /// <summary>Spring between two points: zigzag (schematic) or coil (detailed).</summary>
    internal sealed class SpringType : KinPointType
    {
        public SpringType() : base("kin.spring", new I18nText("Spring", "Feder"), KinCategories.Links,
            "spring", "compression spring", "tension spring", "elastic", "feder", "druckfeder", "zugfeder", "elastisch")
        { }

        public override IReadOnlyList<ViewDef> Views => PointViews.Two;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Int("coils", "Number of coils", "Windungszahl", 6, 2, 40),
            KP.Num("width", "Spring width", "Federbreite", 4, 0.5, 100, "mm", 0.5),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var n = Math.Max(2, Math.Min(40, o.Int("coils", 6)));
            var amp = Geo.Clamp(o.Num("width", 4), 0.5, 100) / 2;
            if (ViewOf(o) != "detailed")
                return new Primitive[] { Prim.Polyline(Frag.Zigzag(a, b, n, amp, 0.15), KS.Link) with { Tag = KinTags.Element } };
            // Coil: trochoid loops along the axis.
            var d = b - a; var len = d.Length;
            var u = len < Geo.Eps ? new Vec2(1, 0) : d / len;
            var v = u.Perp;
            var l0 = 0.15 * len; var body = len - 2 * l0;
            var pitch = body / n;
            var loop = Math.Min(amp * 0.5, pitch * 0.6);
            var pts = new List<Vec2> { a };
            var steps = n * 16;
            for (var i = 0; i <= steps; i++)
            {
                var t = (double)i / 16; // coils
                var x = l0 + body * t / n + loop * (1 - Math.Cos(2 * Math.PI * t));
                var y = -amp * Math.Sin(2 * Math.PI * t);
                pts.Add(a + u * x + v * y);
            }
            pts.Add(b);
            return new Primitive[] { Prim.Polyline(pts, KS.Link) with { Tag = KinTags.Element } };
        }
    }

    /// <summary>Viscous damper (dashpot) between two points.</summary>
    internal sealed class DamperType : KinPointType
    {
        public DamperType() : base("kin.damper", new I18nText("Damper", "Dämpfer"), KinCategories.Links,
            "damper", "dashpot", "shock absorber", "viscous", "dämpfer", "stoßdämpfer", "dämpfungsglied")
        { }

        public override IReadOnlyList<ViewDef> Views => PointViews.Two;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { KP.Num("width", "Damper width", "Dämpferbreite", 5, 0.5, 100, "mm", 0.5) };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var d = b - a; var len = d.Length;
            var u = len < Geo.Eps ? new Vec2(1, 0) : d / len;
            var v = u.Perp;
            var hw = Geo.Clamp(o.Num("width", 5), 0.5, 100) / 2;
            Vec2 P(double x, double y) => a + u * x + v * y;
            double c0 = 0.3 * len, c1 = 0.68 * len, pis = 0.55 * len;
            var list = new List<Primitive>();
            if (ViewOf(o) == "detailed")
            {
                list.Add(Prim.Line(a, P(c0, 0), KS.Link) with { Tag = KinTags.Element });
                list.Add(Prim.Polygon(new[] { P(c0, -hw), P(c1, -hw), P(c1, hw), P(c0, hw) }, KS.Link with { Fill = Fill.Dotted(Math.Max(0.6, hw * 0.35)) }) with { Tag = KinTags.Element });
                list.Add(Prim.Polygon(new[] { P(pis - 0.4, -hw * 0.85), P(pis + 0.4, -hw * 0.85), P(pis + 0.4, hw * 0.85), P(pis - 0.4, hw * 0.85) }, KS.Black) with { Tag = "piston" });
                list.Add(Prim.Line(P(pis, 0), b, KS.Link) with { Tag = KinTags.Element });
                return list;
            }
            // Cup open towards b, piston plate inside.
            list.Add(Prim.Line(a, P(c0, 0), KS.Link) with { Tag = KinTags.Element });
            list.Add(Prim.Polyline(new[] { P(c1, -hw), P(c0, -hw), P(c0, hw), P(c1, hw) }, KS.Link) with { Tag = KinTags.Element });
            list.Add(Prim.Line(P(pis, -hw * 0.7), P(pis, hw * 0.7), KS.Link) with { Tag = "piston" });
            list.Add(Prim.Line(P(pis, 0), b, KS.Link) with { Tag = KinTags.Element });
            return list;
        }
    }

    /// <summary>Motion path / trace of a point: dashed (smooth) polyline with an optional arrowhead.</summary>
    internal sealed class MotionPathType : KinPointType
    {
        public MotionPathType() : base("kin.motion.path", new I18nText("Motion path", "Bahnkurve"), KinCategories.Motion,
            "motion path", "trace", "trajectory", "locus", "path", "bahnkurve", "bahn", "trajektorie", "ortskurve", "bewegungsbahn")
        { }

        protected override bool PortsAtPoints => false;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-18, 6), new Vec2(-7, -5), new Vec2(7, -5), new Vec2(18, 6) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Bool("smooth", "Smooth curve", "Geglättet", true),
            KP.Bool("arrow", "Arrowhead (direction)", "Pfeilspitze (Richtung)", true),
            KP.Bool("closed", "Closed path", "Geschlossene Bahn", false),
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var closed = o.Flag("closed", false) && p.Count > 2;
            var style = KS.Trace with { StrokeWidth = 0.35, ArrowEnd = o.Flag("arrow", true) && !closed ? ArrowHead.Triangle : null, ArrowSize = ArrowSize.Medium };
            Primitive path = o.Flag("smooth", true) && p.Count > 2
                ? Prim.Path(Frag.Smooth(p, closed), style)
                : Prim.Polyline(p, style, closed);
            return new[] { path with { Tag = KinTags.Path } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return KA.Toggle(o, "kin.path.arrow", new I18nText("Arrowhead", "Pfeilspitze"), IconIds.ArrowEnd, "arrow", true, "path");
            yield return KA.Toggle(o, "kin.path.smooth", new I18nText("Smooth curve", "Geglättet"), IconIds.Toggle, "smooth", true, "path");
            yield return KA.Toggle(o, "kin.path.closed", new I18nText("Closed path", "Geschlossene Bahn"), IconIds.Cycle, "closed", false, "path");
        }
    }
}
