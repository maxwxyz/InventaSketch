using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Vertical circulation and building elements in plan: lift (shaft with car and cross), ramp (outline, arrow up the slope
    /// and the slope in %), shaft (rectangle with diagonal cross), slab opening (dashed diagonals), roof plan (gable or hip
    /// roof with ridge, hips and fall arrows) and the railing (a point-geometry line with posts).
    /// </summary>
    internal static class Building
    {
        public static IEnumerable<ObjectType> Types()
        {
            var circ = ArchCategories.Stairs;
            yield return new BoxType("arch.lift", new I18nText("Lift (shaft with car)", "Aufzug (Schacht mit Kabine)"), circ, new Size2(20, 22), Lift,
                "lift", "elevator", "aufzug", "fahrstuhl", "lift shaft", "aufzugsschacht", "car", "kabine", "barrier-free", "barrierefrei")
            {
                Params = new[]
                {
                    Pm.Bool("counterweight", "Counterweight", "Gegengewicht", true),
                    Pm.Bool("door", "Landing door", "Schachttür", true),
                },
                Extra = o => new[]
                {
                    Arch.Toggle(o, "arch.lift.counterweight", new I18nText("Counterweight", "Gegengewicht"), IconIds.Toggle, "counterweight", true),
                    Arch.Toggle(o, "arch.lift.door", new I18nText("Landing door", "Schachttür"), IconIds.Toggle, "door", true),
                },
            };
            yield return new RampType("arch.ramp", new I18nText("Ramp", "Rampe"), circ, new Size2(60, 12), Ramp,
                "ramp", "rampe", "slope", "gefälle", "steigung", "incline", "barrier-free", "barrierefrei", "wheelchair", "rollstuhl", "rise", "höhenunterschied")
            {
                Params = new[]
                {
                    Pm.Num("slope", "Slope", "Steigung", 6, 0.5, 50, "%", 0.5),
                    Pm.Bool("down", "Arrow reversed", "Pfeil umgekehrt", false),
                    Pm.Bool("showSlope", "Show slope", "Steigung anzeigen", true),
                    Pm.Scale,
                    ValueLabels.ShowValues,
                },
                Extra = RampActions,
            };
            yield return new RailingType();

            var st = ArchCategories.Structure;
            yield return new BoxType("arch.shaft", new I18nText("Shaft (building)", "Schacht (Gebäude)"), st, new Size2(10, 10), Shaft,
                "shaft", "schacht", "installation shaft", "installationsschacht", "duct shaft", "riser", "steigschacht")
            {
                Params = new[] { Pm.Bool("cross", "Diagonal cross", "Diagonalkreuz", true) },
                Extra = o => new[] { Arch.Toggle(o, "arch.shaft.cross", new I18nText("Diagonal cross", "Diagonalkreuz"), IconIds.Toggle, "cross", true) },
            };
            yield return new BoxType("arch.void", new I18nText("Slab opening (void)", "Deckenöffnung (Luftraum)"), st, new Size2(30, 20), Void,
                "void", "opening", "slab opening", "deckenöffnung", "deckendurchbruch", "luftraum", "atrium", "galerie", "aussparung");
            yield return new BoxType("arch.roof", new I18nText("Roof plan", "Dachaufsicht"), st, new Size2(100, 70), Roof,
                "roof", "dach", "roof plan", "dachaufsicht", "dachdraufsicht", "gable", "satteldach", "hip", "walmdach", "ridge", "first", "grat", "eaves", "traufe")
            {
                Params = new[]
                {
                    Pm.Choice("kind", "Roof shape", "Dachform", "gable", RoofKinds.Select(k => (k.Value, k.Label.En, k.Label.De ?? k.Label.En)).ToArray()),
                    Pm.Choice("ridge", "Ridge direction (gable roof)", "Firstrichtung (Satteldach)", "long",
                        ("long", "Along the long side", "Parallel zur langen Seite"), ("short", "Along the short side", "Parallel zur kurzen Seite")),
                    Pm.Bool("arrows", "Fall arrows", "Gefällepfeile", true),
                    Pm.Bool("walls", "Outer walls below (dashed)", "Außenwände darunter (gestrichelt)", false),
                },
                Extra = o => Arch.Choice(o, "arch.roof.kind", new I18nText("Roof shape", "Dachform"), "kind", "gable", RoofKinds)
                    .Concat(new[]
                    {
                        Arch.Toggle(o, "arch.roof.arrows", new I18nText("Fall arrows", "Gefällepfeile"), IconIds.FlowArrow, "arrows", true),
                        Arch.Toggle(o, "arch.roof.walls", new I18nText("Outer walls below", "Außenwände darunter"), IconIds.Toggle, "walls", false),
                    }),
            };
        }

        internal static readonly (string Value, I18nText Label)[] RoofKinds =
        {
            ("gable", new I18nText("Gable roof", "Satteldach")), ("hip", new I18nText("Hip roof", "Walmdach")),
        };

        // ------------------------------------------------------------------ lift

        private static void Lift(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            var cw = o.Flag("counterweight", true);
            var door = o.Flag("door", true);
            double mx = 0.1 * c.W, top = cw ? 0.2 * c.H : 0.1 * c.H, bottom = door ? 0.16 * c.H : 0.1 * c.H;
            double x0 = -c.Hw + mx, x1 = c.Hw - mx, y0 = -c.Hh + top, y1 = c.Hh - bottom;
            c.Rect(x0, y0, x1, y1, St.Thin, "car");
            c.Line(new Vec2(x0, y0), new Vec2(x1, y1), St.Thin, "cross");
            c.Line(new Vec2(x1, y0), new Vec2(x0, y1), St.Thin, "cross");
            if (cw)
            {
                double gy0 = -c.Hh + 0.05 * c.H, gy1 = -c.Hh + 0.13 * c.H;
                c.Rect(-0.25 * c.W, gy0, 0.25 * c.W, gy1, St.Thin with { Fill = Fill.Solid("#bdbdbd") }, "counterweight");
            }
            if (door)
            {
                // Two-panel sliding landing door in front of the car, in the zone between car and shaft wall.
                var y = (y1 + c.Hh) / 2;
                var dw = (x1 - x0) * 0.8;
                var gap = Math.Min(0.15 * c.H, c.Hh - y1) * 0.25;
                c.Line(new Vec2(-dw / 2, y - gap), new Vec2(0, y - gap), St.Medium, "door");
                c.Line(new Vec2(0, y + gap), new Vec2(dw / 2, y + gap), St.Medium, "door");
            }
        }

        // ------------------------------------------------------------------ ramp

        private static IEnumerable<ContextAction> RampActions(SketchObject o)
        {
            yield return Arch.Toggle(o, "arch.ramp.reverse", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, "down", false, "direction");
            yield return Arch.Toggle(o, "arch.ramp.slope", new I18nText("Show slope", "Steigung anzeigen"), IconIds.Toggle, "showSlope", true);
        }

        /// <summary>The slope text ("6 %", "6,5 %" in German).</summary>
        internal static string SlopeText(double slope, Lang lang)
        {
            var decimals = Math.Abs(slope - Math.Round(slope)) < 1e-9 ? 0 : 1;
            return Arch.Format(slope, decimals, lang) + " %";
        }

        private static void Ramp(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var along = c.W >= c.H;
            var len = along ? c.W : c.H;
            var across = along ? c.H : c.W;
            // Local frame: u along the ramp, v across it.
            Vec2 P(double u, double v) => along ? new Vec2(u, v) : new Vec2(v, -u);
            var down = o.Flag("down", false);
            double u0 = -len * 0.42, u1 = len * 0.42;
            if (down) (u0, u1) = (u1, u0);
            var r = Math.Max(0.01, Math.Min(0.4, across * 0.08));
            c.Ellipse(P(u0, 0), r, r, St.Thin, "walk-start");
            var dir = Math.Sign(u1 - u0);
            c.Add(Prim.Line(P(u0 + dir * r, 0), P(u1, 0), St.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }), ArchTags.WalkLine);
            if (!o.Flag("showSlope", true)) return;
            var text = SlopeText(Math.Max(0, o.Num("slope", 6)), ctx.Lang);
            var size = Arch.FitText(text, 2.5, len * 0.5, across * 0.32);
            c.Add(Arch.Text(P(0, -across * 0.25), text, size, ArchTags.Label));
        }

        // ------------------------------------------------------------------ shaft, void

        private static void Shaft(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            if (!o.Flag("cross", true)) return;
            c.Line(new Vec2(-c.Hw, -c.Hh), new Vec2(c.Hw, c.Hh), St.Thin, "cross");
            c.Line(new Vec2(c.Hw, -c.Hh), new Vec2(-c.Hw, c.Hh), St.Thin, "cross");
        }

        private static void Void(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            c.Line(new Vec2(-c.Hw, -c.Hh), new Vec2(c.Hw, c.Hh), St.Hidden, "cross");
            c.Line(new Vec2(c.Hw, -c.Hh), new Vec2(-c.Hw, c.Hh), St.Hidden, "cross");
        }

        // ------------------------------------------------------------------ roof

        /// <summary>
        /// Roof plan: the eaves outline, the ridge and (hip roof) the hips at 45° in plan, i.e. equal pitches on all faces, so the
        /// ridge of a hip roof runs along the longer side (a square gives a pyramid roof). Fall arrows point down each face.
        /// </summary>
        private static void Roof(Canvas c, SketchObject o, RenderContext ctx)
        {
            var hip = o.Str("kind", "gable") == "hip";
            // Ridge axis: hip roofs always along the longer side; gable roofs as chosen.
            var alongX = c.W >= c.H;
            if (!hip && o.Str("ridge", "long") == "short") alongX = !alongX;
            double L = alongX ? c.W : c.H, B = alongX ? c.H : c.W;
            Vec2 P(double u, double v) => alongX ? new Vec2(u, v) : new Vec2(v, u);

            if (o.Flag("walls", false))
            {
                var inset = Math.Min(c.K * 0.06, 5);
                c.Rect(-c.Hw + inset, -c.Hh + inset, c.Hw - inset, c.Hh - inset, St.Hidden, "wall-below");
            }
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            if (hip)
            {
                var r = Math.Max(0, L / 2 - B / 2);
                c.Line(P(-r, 0), P(r, 0), St.Medium, "ridge");
                c.Line(P(-L / 2, -B / 2), P(-r, 0), St.Medium, "hip");
                c.Line(P(-L / 2, B / 2), P(-r, 0), St.Medium, "hip");
                c.Line(P(L / 2, -B / 2), P(r, 0), St.Medium, "hip");
                c.Line(P(L / 2, B / 2), P(r, 0), St.Medium, "hip");
            }
            else c.Line(P(-L / 2, 0), P(L / 2, 0), St.Medium, "ridge");

            if (!o.Flag("arrows", true)) return;
            var a = Math.Min(B * 0.22, c.K * 0.2);
            var arrow = St.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            void Fall(Vec2 from, Vec2 to) => c.Add(Prim.Line(from, to, arrow), "fall");
            Fall(P(0, -B * 0.25 + a / 2), P(0, -B * 0.25 - a / 2));
            Fall(P(0, B * 0.25 - a / 2), P(0, B * 0.25 + a / 2));
            if (hip && L / 2 - B / 6 > a)
            {
                var u = L / 2 - B / 6;
                var len = Math.Min(a, B / 6);
                Fall(P(-u + len / 2, 0), P(-u - len / 2, 0));
                Fall(P(u - len / 2, 0), P(u + len / 2, 0));
            }
        }
    }

    /// <summary>Railing (balustrade) in plan: a line along the points with posts at the vertices and at a set spacing.</summary>
    internal sealed class RailingType : PointType
    {
        public RailingType() : base("arch.railing", new I18nText("Railing", "Geländer"), ArchCategories.Stairs,
            "railing", "geländer", "balustrade", "brüstung", "handrail", "handlauf", "banister", "absturzsicherung") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        /// <summary>Corners and extending like walls and pipes.</summary>
        public override bool InsertablePoints => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Bool("posts", "Posts", "Pfosten", true),
            Pm.Num("spacing", "Post spacing", "Pfostenabstand", 1000, 200, 5000, "mm", 50),
            Pm.Scale,
        };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var list = new List<Primitive> { Prim.Polyline(p, St.Medium) with { Tag = "railing" } };
            if (!o.Flag("posts", true)) return list;
            var step = Math.Max(0.5, Math.Max(200, o.Num("spacing", 1000)) / Arch.ScaleOf(o, ctx.Doc));
            const double r = 0.35;
            var posts = new List<Vec2> { p[0] };
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var len = p[i].DistanceTo(p[i + 1]);
                if (len < Geo.Eps) continue;
                var n = Math.Max(1, (int)Math.Round(len / step));
                for (var k = 1; k <= n; k++) posts.Add(Vec2.Lerp(p[i], p[i + 1], (double)k / n));
            }
            foreach (var q in posts) list.Add(Prim.Circle(q.X, q.Y, r, St.Black) with { Tag = "post" });
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > 1e-6) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Arch.Toggle(o, "arch.railing.posts", new I18nText("Posts", "Pfosten"), IconIds.Toggle, "posts", true);
        }
    }
}
