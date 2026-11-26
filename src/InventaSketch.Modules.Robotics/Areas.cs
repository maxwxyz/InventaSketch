using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>Base of the module's point types (page coordinates).</summary>
    internal abstract class RoboPointType : ObjectType
    {
        public override GeometryKind Geometry => GeometryKind.Points;
        public override bool InsertablePoints => true;
        public override Primitive ApplyOverride(Primitive p, Style over) => Rb.Override(p, over);

        /// <summary>Segments of the polyline (closing segment included for closed outlines).</summary>
        protected static List<(Vec2 A, Vec2 B)> Segments(IReadOnlyList<Vec2> p, bool closed)
        {
            var list = new List<(Vec2, Vec2)>();
            for (var i = 0; i + 1 < p.Count; i++) list.Add((p[i], p[i + 1]));
            if (closed && p.Count > 2) list.Add((p[p.Count - 1], p[0]));
            return list;
        }
    }

    /// <summary>Safety fence as a polyline in plan: a continuous line with posts at the corners and at most every "panel" mm.</summary>
    internal sealed class FenceType : RoboPointType
    {
        public override string Id => "robot.safety.fence";
        public override I18nText Name => new("Safety fence", "Schutzzaun");
        public override string Category => RoboCategories.Safety;
        public override IReadOnlyList<string> Keywords => new[] { "fence", "safety fence", "guard", "guarding", "cell", "schutzzaun", "zaun", "schutzgitter", "trennende schutzeinrichtung", "roboterzelle" };
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, 18), new Vec2(-30, -18), new Vec2(30, -18), new Vec2(30, 18) };
        public override IReadOnlyList<string> Companions => new[] { "robot.safety.door", "robot.safety.curtain", "robot.safety.estop" };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Bool("closed", "Closed outline", "Geschlossen", false),
            Pm.Num("panel", "Panel width on the page", "Feldbreite auf dem Blatt", 20, 3, 500, "mm", 1),
            Pm.Bool("mesh", "Mesh band", "Gitterband", true),
        };

        public override bool IsClosed(SketchObject o) => o.Flag("closed", false);

        private static readonly Style FenceLine = Style.Visible with { StrokeWidth = 0.35, Stroke = "#3c4450" };
        private static readonly Style MeshLine = Style.ThinLine with { StrokeWidth = 0.13, Stroke = "#3c4450" };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var list = new List<Primitive>();
            if (p.Count < 2) return list;
            var closed = IsClosed(o) && p.Count > 2;
            var panel = Geo.Clamp(o.Num("panel", 20), 3, 500);
            const double h = 0.55;
            if (o.Flag("mesh", true))
                foreach (var (a, b) in Segments(p, closed))
                {
                    var d = b - a;
                    if (d.Length < 1e-9) continue;
                    var n = d.Normalized().Perp * h;
                    list.Add(Prim.Line(a + n, b + n, MeshLine) with { Tag = RoboTags.Link });
                    list.Add(Prim.Line(a - n, b - n, MeshLine) with { Tag = RoboTags.Link });
                }
            list.Add(Prim.Polyline(p, FenceLine, closed) with { Tag = RoboTags.Link });
            var segs = Segments(p, closed);
            for (var i = 0; i < segs.Count; i++)
            {
                var (a, b) = segs[i];
                var d = b - a;
                var len = d.Length;
                var u = len < 1e-9 ? new Vec2(1, 0) : d / len;
                var count = Math.Max(1, (int)Math.Ceiling(len / panel - 1e-9));
                // Every segment draws its start corner and intermediate posts; the end corner belongs to the next segment
                // (the last segment of an open fence draws its end post as well).
                var last = !closed && i == segs.Count - 1;
                for (var k = 0; k <= (last ? count : count - 1); k++) list.Add(Post(a + d * ((double)k / count), u));
            }
            return list;
        }

        private static Primitive Post(Vec2 c, Vec2 u)
        {
            var v = u.Perp;
            const double s = 0.6;
            return Prim.Polygon(new[] { c - u * s - v * s, c + u * s - v * s, c + u * s + v * s, c - u * s + v * s }, RS.Black with { Fill = Fill.Solid("#3c4450"), Stroke = "#3c4450" }) with { Tag = RoboTags.Post };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var segs = Segments(p, IsClosed(o));
            for (var i = 0; i < segs.Count; i++)
            {
                yield return Anchor.Rail(segs[i].A, segs[i].B, "rail" + i.ToString(CultureInfo.InvariantCulture));
                yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(segs[i].A, segs[i].B, 0.5), "m" + i.ToString(CultureInfo.InvariantCulture));
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Rb.Toggle(o, "robot.fence.closed", new I18nText("Closed fence", "Zaun schließen"), IconIds.Toggle, "closed", false),
            Rb.Toggle(o, "robot.fence.mesh", new I18nText("Mesh band", "Gitterband"), IconIds.FillHatch, "mesh", true),
        };
    }

    /// <summary>Safety zone: a closed, tinted or hatched area with an optional name (danger zone, fields, spaces).</summary>
    internal sealed class ZoneType : RoboPointType
    {
        public override string Id => "robot.safety.zone";
        public override I18nText Name => new("Safety zone", "Sicherheitsbereich");
        public override string Category => RoboCategories.Safety;
        public override IReadOnlyList<string> Keywords => new[] { "zone", "safety zone", "danger zone", "hazard", "restricted space", "collaborative workspace", "schutzbereich", "gefahrenbereich", "sicherheitsbereich", "kollaborationsraum", "schutzfeld", "warnfeld", "sperrbereich" };
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, -12), new Vec2(20, -12), new Vec2(20, 12), new Vec2(-20, 12) };
        public override bool IsClosed(SketchObject o) => true;
        public override bool IsPart => false;
        public override IReadOnlyList<string> Companions => new[] { "robot.safety.scanner", "robot.safety.fence", "robot.safety.curtain" };

        internal static readonly ParamDef KindParam = Pm.Choice("kind", "Zone", "Bereich", "hazard",
            ("hazard", "Danger zone", "Gefahrenbereich"),
            ("protective", "Protective field", "Schutzfeld"),
            ("warning", "Warning field", "Warnfeld"),
            ("restricted", "Restricted space", "Eingeschränkter Raum"),
            ("collaborative", "Collaborative workspace", "Kollaborationsraum"));

        public override IReadOnlyList<ParamDef> Parameters => new[] { KindParam, Pm.FillChoice("hatch"), Pm.Bool("label", "Name", "Bezeichnung", true) };

        /// <summary>Outline colour and tint of a zone kind (common practice, not a cited standard).</summary>
        internal static (string Line, string Tint, bool Dashed) Colors(string kind) => kind switch
        {
            "warning" => ("#b88600", RoboColors.WarningTint, true),
            "restricted" => (RoboColors.Blue, RoboColors.RestrictedTint, true),
            "collaborative" => (RoboColors.Green, RoboColors.CollaborativeTint, false),
            _ => (RoboColors.Red, RoboColors.ProtectiveTint, false),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var list = new List<Primitive>();
            if (p.Count < 2) return list;
            var kind = o.Str("kind", "hazard");
            var (line, tint, dashed) = Colors(kind);
            var tag = kind == "warning" ? RoboTags.Warning : RoboTags.Protective;
            switch (o.Str("fill", "hatch"))
            {
                case "tint": list.Add(Prim.Polygon(p, RS.Area(Fill.Solid(tint))) with { Tag = tag }); break;
                case "hatch": list.Add(Prim.Polygon(p, RS.Area(Fill.Hatched(new HatchPattern(45, 2.0, Color: line, Width: 0.18)))) with { Tag = tag }); break;
            }
            var st = Style.Visible with { StrokeWidth = 0.35, Stroke = line, Dash = dashed ? DashStyle.Dash : DashStyle.Solid };
            list.Add(Prim.Polyline(p, st, p.Count > 2) with { Tag = tag });
            if (o.Flag("label", true) && p.Count > 2)
            {
                var label = (KindParam.Options!.FirstOrDefault(x => x.Value == kind) ?? KindParam.Options![0]).Label.Get(ctx.Lang);
                var b = Rect.FromPoints(p);
                var size = Geo.Clamp(Math.Min(b.W / (label.Length * Rb.CharWidth + 1), b.H / 3), 1.0, 3.5);
                list.Add(new TextPrim(Centroid(p), label, new TextStyle { Size = size, Bold = true, Color = line }) { Tag = RoboTags.Label });
            }
            return list;
        }

        internal static Vec2 Centroid(IReadOnlyList<Vec2> p)
        {
            double a = 0, cx = 0, cy = 0;
            for (var i = 0; i < p.Count; i++)
            {
                var q = p[i]; var r = p[(i + 1) % p.Count];
                var c = q.X * r.Y - r.X * q.Y;
                a += c; cx += (q.X + r.X) * c; cy += (q.Y + r.Y) * c;
            }
            if (Math.Abs(a) < 1e-9) return new Vec2(p.Average(v => v.X), p.Average(v => v.Y));
            return new Vec2(cx / (3 * a), cy / (3 * a));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            var segs = Segments(p, true);
            for (var i = 0; i < segs.Count; i++)
                yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(segs[i].A, segs[i].B, 0.5), "m" + i.ToString(CultureInfo.InvariantCulture));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Rb.Toggle(o, "robot.zone.label", new I18nText("Show name", "Bezeichnung zeigen"), IconIds.LabelShow, "label", true, "annotation"),
        }.Concat(Rb.Choice(o, "robot.zone.kind", KindParam));
    }

    /// <summary>
    /// Reach envelope of an articulated arm as a stand-alone element: the working range of the wrist centre in the arm
    /// plane (side view) and the annulus or sector it sweeps about the first axis (top view), computed from the same
    /// parameters as <see cref="ArticulatedArmType"/>, with radius labels.
    /// </summary>
    internal sealed class ReachEnvelopeType : ObjectType
    {
        public override string Id => "robot.reach.envelope";
        public override I18nText Name => new("Reach envelope (working range)", "Arbeitsraum (Reichweite)");
        public override string Category => RoboCategories.Frames;
        public override IReadOnlyList<string> Keywords => new[] { "reach", "envelope", "working range", "workspace", "work envelope", "arbeitsraum", "reichweite", "arbeitsbereich", "hüllkurve" };
        public override IReadOnlyList<ViewDef> Views => new[] { RoboViews.SideView with { Size = new Size2(50, 34) }, RoboViews.TopView with { Size = new Size2(40, 40) } };
        public override Size2 DefaultSize => new(50, 34);
        public override bool PreferUniformScale => true;
        public override bool IsPart => false;
        public override IReadOnlyList<string> Companions => new[] { ArticulatedArmType.TypeId, "robot.safety.zone", "robot.safety.fence" };

        public override IReadOnlyList<ParamDef> Parameters =>
            ArticulatedArmType.GeometryDefs.Concat(new[]
            {
                Pm.FillChoice("tint"), Pm.Bool("labels", "Radius labels", "Radienangaben", true),
                Pm.Num("travel", "Travel of a linear axis (range swept along x)", "Verfahrweg einer Linearachse (Bereich entlang x überstrichen)", 0, 0, 50000, "mm", 100),
            }).Concat(ArticulatedArmType.Limits).ToList();

        private static double Travel(SketchObject o) => Geo.Clamp(o.Num("travel", 0), 0, 50000);

        /// <summary>
        /// The area swept when the base moves by 0 … <paramref name="dx"/> along x (a robot on a linear axis), drawn by its
        /// outer limit: in the top view the convex hull of the area at both ends of the travel (exact for the convex sector of
        /// the default limits), in the side view row by row from the leftmost to the rightmost reachable point (inner
        /// unreachable pockets are filled).
        /// </summary>
        internal static Area2 Sweep(Area2 area, double dx, bool convex)
        {
            if (dx <= 1e-9 || area.Fill.Count < 3) return area;
            if (convex) return Area2.Polygon(Rb.Hull(area.Fill.Concat(area.Fill.Select(p => new Vec2(p.X + dx, p.Y)))));
            var b = Rect.FromPoints(area.Fill);
            const int rows = 160;
            var ys = Enumerable.Range(0, rows + 1).Select(k => b.Y + b.H * Geo.Clamp(k / (double)rows, 1e-6, 1 - 1e-6)).ToList();
            // Rows right beside the leftmost and rightmost points, so the swept width is exact.
            foreach (var p in new[] { area.Fill.OrderBy(q => q.X).First(), area.Fill.OrderBy(q => q.X).Last() })
                foreach (var e in new[] { -1e-7, 1e-7 }) ys.Add(Geo.Clamp(p.Y + e * Math.Max(1, b.H), b.Y + 1e-6 * b.H, b.Bottom - 1e-6 * b.H));
            ys.Sort();
            var left = new List<Vec2>(); var right = new List<Vec2>();
            foreach (var y in ys)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                var poly = area.Fill;
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    Vec2 a = poly[i], c = poly[j];
                    if ((a.Y > y) == (c.Y > y)) continue;
                    var x = a.X + (c.X - a.X) * (y - a.Y) / (c.Y - a.Y);
                    lo = Math.Min(lo, x); hi = Math.Max(hi, x);
                }
                if (lo > hi) continue;
                left.Add(new Vec2(lo, y)); right.Add(new Vec2(hi + dx, y));
            }
            if (left.Count < 2) return area;
            right.Reverse();
            return Area2.Polygon(left.Concat(right).ToList());
        }

        private static Rect Model(SketchObject o, string view, Area2 area)
        {
            var r = Rect.FromPoints(area.Points);
            if (view == RoboViews.Top) return r.Inflate(Math.Max(r.W, r.H) * 0.04);
            r = r.Union(new Rect(0, 0, 0, 0));
            return r.Inflate(Math.Max(r.W, r.H) * 0.04);
        }

        private static Area2 AreaOf(SketchObject o, string view) => view == RoboViews.Top
            ? Sweep(ArticulatedArmType.TopRegion(o), Travel(o), true)
            : Sweep(ArticulatedArmType.SideRegion(o), Travel(o), false);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var view = ViewOf(o);
            var area = AreaOf(o, view);
            var s = new ArmScene(o.Size, Model(o, view, area));
            var fill = o.Str("fill", "tint");
            if (area.Fill.Count >= 3 && fill != "none")
            {
                var f = fill == "hatch" ? Fill.Hatched(new HatchPattern(45, Math.Max(0.8, s.R * 1.6), Color: RoboColors.Envelope, Width: 0.15)) : Fill.Solid(RoboColors.EnvelopeTint);
                s.Add(Prim.Polygon(area.Fill.Select(s.L).ToList(), RS.Area(f)), RoboTags.Envelope);
            }
            var line = Style.Visible with { StrokeWidth = 0.35, Stroke = RoboColors.Envelope };
            foreach (var (pts, closed) in area.Strokes) if (pts.Count >= 2) s.Add(Prim.Polyline(pts.Select(s.L).ToList(), line, closed), RoboTags.Envelope);
            var labels = o.Flag("labels", true);
            if (view == RoboViews.Top)
            {
                var c = s.L(Vec2.Zero);
                var k = 1.6 * s.R;
                s.Add(Prim.Line(c - new Vec2(k, 0), c + new Vec2(k, 0), RS.Center), RoboTags.Axis);
                s.Add(Prim.Line(c - new Vec2(0, k), c + new Vec2(0, k), RS.Center), RoboTags.Axis);
                if (Travel(o) > 1e-9)
                {
                    // The base's travel along the linear axis, with the axis cross at its end.
                    var e = s.L(new Vec2(Travel(o), 0));
                    s.Add(Prim.Line(c, e, RS.Center), RoboTags.Axis);
                    s.Add(Prim.Line(e - new Vec2(0, k), e + new Vec2(0, k), RS.Center), RoboTags.Axis);
                }
                var (rin, rout) = ArticulatedArmType.HorizontalRange(ArticulatedArmType.SideRegion(o));
                if (labels)
                {
                    RadiusLabel(s, area, Vec2.Zero, rout, 25, "R " + Rb.Fmt(rout, "0"));
                    if (rin > rout * 0.08) RadiusLabel(s, area, Vec2.Zero, rin, -25, "R " + Rb.Fmt(rin, "0"));
                }
            }
            else
            {
                var b = Rect.FromPoints(area.Points);
                var top = s.L(new Vec2(0, Math.Min(b.Y, 0)));
                var floor = s.L(Vec2.Zero);
                s.Add(Prim.Line(floor, top, RS.Center), RoboTags.Axis);
                s.Add(Prim.Line(new Vec2(-s.W / 2 + 0.2, floor.Y), new Vec2(s.W / 2 - 0.2, floor.Y), RS.Thin), RoboTags.Base);
                var g = new ArticulatedArmType.Geo6(o);
                var sh = s.L(new Vec2(g.A1, -g.D1));
                s.Add(Prim.Circle(sh.X, sh.Y, s.R * 0.8, RS.White), RoboTags.Joint);
                s.Add(Prim.Circle(sh.X, sh.Y, s.R * 0.25, RS.Black), RoboTags.Joint);
                if (labels)
                {
                    var (j3a, j3b) = ArticulatedArmType.Order(o.Num("j3min", -80), o.Num("j3max", 70));
                    var (_, rmax) = Reach.RhoRange(g.A2, g.L3, g.Phi0 + j3a, g.Phi0 + j3b);
                    RadiusLabel(s, area, new Vec2(g.A1, -g.D1), rmax, -30, "R " + Rb.Fmt(rmax, "0"));
                }
            }
            return s.Items;
        }

        /// <summary>
        /// Radius arrow from the centre to the area point at distance <paramref name="r"/> closest to the preferred direction
        /// (so the arrow ends on the area also when the shoulder or J1 range leaves gaps).
        /// </summary>
        private static void RadiusLabel(ArmScene s, Area2 area, Vec2 centre, double r, double angleDeg, string text)
        {
            var pref = Geo.DegToRad(angleDeg);
            var candidates = area.Fill.Where(q => Math.Abs(q.DistanceTo(centre) - r) <= Math.Max(1e-6, r * 2e-3)).ToList();
            if (candidates.Count == 0) return;
            double Diff(Vec2 q)
            {
                var d = Math.Atan2(q.Y - centre.Y, q.X - centre.X) - pref;
                return Math.Abs(Math.Atan2(Math.Sin(d), Math.Cos(d)));
            }
            var target = candidates.OrderBy(Diff).First();
            var u = (target - centre).Normalized();
            var c = s.L(centre);
            var e = s.L(centre + u * r);
            if (c.DistanceTo(e) < 1e-6) return;
            s.Add(Prim.Line(c, e, RS.Arrow with { Stroke = RoboColors.Envelope }), RoboTags.Label);
            var mid = Vec2.Lerp(c, e, 0.55) + u.Perp * (-0.8 * s.TextSize);
            s.Add(Rb.FitText(mid, text, s.TextSize, s.W / 2, s.H / 2, RoboTags.Label, color: RoboColors.Envelope));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var view = ViewOf(o);
            var area = AreaOf(o, view);
            var s = new ArmScene(o.Size, Model(o, view, area));
            yield return Anchor.Point(AnchorRole.Mount, s.L(Vec2.Zero), "base");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Rb.Toggle(o, "robot.reach.labels", new I18nText("Radius labels", "Radienangaben"), IconIds.LabelShow, "labels", true, "annotation"),
        };

        public override Primitive ApplyOverride(Primitive p, Style over) => Rb.Override(p, over);
    }
}
