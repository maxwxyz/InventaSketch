using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Humans as uniform, stylised mannequin outlines (tapered capsules, white-filled so overlapping limbs read clearly):
    /// standing, walking, seated driver/passenger, child, hand with touching finger and a head with gaze direction.
    /// Scale 1:100 like the vehicles (an adult is 17.5 mm tall); the hand is a detail-scale symbol.
    /// </summary>
    internal static class Humans
    {
        public const string Cat = AutoCategories.Humans;

        private static I18nText T(string en, string de) => new(en, de);

        private static Vec2 V(double x, double y) => new(x, y);

        private static readonly ParamDef Role = Prm.Choice("role", "Role", "Rolle", "driver",
            ("driver", "Driver (hands on wheel)", "Fahrer (Hände am Lenkrad)"), ("passenger", "Passenger", "Beifahrer"));

        public static IEnumerable<ObjectType> Types()
        {
            yield return new AutoType("auto.human.standing", T("Person standing", "Person stehend"), Cat,
                new[] { "person", "human", "pedestrian", "standing", "vru", "mensch", "person", "fußgänger", "stehend" },
                Views.FrontV(6.4, 17.5, (p, o) => FrontFigure(p, 1, 1.15)),
                Views.SideV(5, 17.5, (p, o) => SideFigure(p, 1, 1.15)),
                Views.TopV(4, 5.2, (p, o) => TopStanding(p)));
            yield return new AutoType("auto.human.walking", T("Person walking", "Person gehend"), Cat,
                new[] { "person", "human", "pedestrian", "walking", "crossing", "vru", "mensch", "fußgänger", "gehend", "querend" },
                Views.SideV(9, 17.5, (p, o) => Walking(p)),
                Views.TopV(5.4, 5.6, (p, o) => TopWalking(p)));
            yield return new AutoType("auto.human.seated", T("Person seated", "Person sitzend"), Cat,
                new[] { "person", "driver", "passenger", "occupant", "seated", "fahrer", "beifahrer", "insasse", "sitzend" },
                Views.SideV(11.2, 14.6, Seated),
                Views.TopV(9.2, 5.6, SeatedTop))
            {
                Params = new[] { Role },
                Extra = o => new[] { Prm.Cycle(o, Role, T("Driver / passenger", "Fahrer / Beifahrer")) },
            };
            const double k = 0.63, head = 0.95;
            yield return new AutoType("auto.human.child", T("Child", "Kind"), Cat,
                new[] { "child", "kid", "pedestrian", "vru", "kind", "fußgänger" },
                Views.FrontV(6.4 * k, 17.5 * k, (p, o) => FrontFigure(p, k, head)),
                Views.SideV(5 * k, 17.5 * k, (p, o) => SideFigure(p, k, head)));
            yield return Hand();
            yield return new HeadType();
        }

        // ------------------------------------------------------------------ standing / walking

        private static void Cap(Pen p, double k, double x1, double y1, double x2, double y2, double r1, double r2, string tag = "limb")
            => p.Capsule(V(x1 * k, y1 * k), V(x2 * k, y2 * k), r1 * k, r2 * k, null, tag);

        /// <summary>Front view of a standing figure; <paramref name="k"/> scales the body, the head keeps its top at the top edge.</summary>
        private static void FrontFigure(Pen p, double k, double headR)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                Cap(p, k, s * 1.0, 4.6, s * 1.0, 8.0, 0.6, 0.4);
                Cap(p, k, s * 1.0, 8.25, s * 1.45, 8.35, 0.4, 0.38, "foot");
                Cap(p, k, s * 0.95, 0.6, s * 1.0, 4.6, 0.85, 0.62);
            }
            p.Rounded(new[] { V(-2.15 * k, -6.1 * k), V(2.15 * k, -6.1 * k), V(1.55 * k, -1.6 * k), V(1.85 * k, 1.0 * k), V(-1.85 * k, 1.0 * k), V(-1.55 * k, -1.6 * k) }, 0.8 * k, S.OpaqueFine, "torso");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                Cap(p, k, s * 2.0, -5.6, s * 2.45, -2.3, 0.55, 0.45);
                Cap(p, k, s * 2.45, -2.3, s * 2.6, 0.7, 0.45, 0.36);
                p.Circle(s * 2.65 * k, 1.35 * k, 0.5 * k, S.OpaqueFine, "hand");
            }
            Cap(p, k, 0, -6.9, 0, -6.0, 0.45, 0.45, "neck");
            p.Circle(0, -8.75 * k + headR, headR, S.OpaqueFine, "head");
        }

        private static void SideTorso(Pen p, double k) =>
            p.Rounded(new[] { V(-1.25 * k, -5.9 * k), V(0.9 * k, -6.0 * k), V(1.35 * k, -4.6 * k), V(1.0 * k, -1.8 * k), V(1.15 * k, 0.9 * k), V(-1.2 * k, 1.0 * k), V(-0.9 * k, -1.8 * k), V(-1.3 * k, -4.6 * k) }, 0.8 * k, S.OpaqueFine, "torso");

        private static void SideHead(Pen p, double cx, double cy, double r)
        {
            p.PolyD(new[] { V(cx + r * 0.95, cy - r * 0.25), V(cx + r * 1.25, cy + r * 0.15), V(cx + r * 0.9, cy + r * 0.3) }, S.OpaqueFine, true, "nose");
            p.Circle(cx, cy, r, S.OpaqueFine, "head");
        }

        private static void SideFigure(Pen p, double k, double headR)
        {
            Cap(p, k, 0.2, 4.6, 0.05, 8.0, 0.6, 0.4);
            Cap(p, k, 0.1, 8.3, 1.5, 8.35, 0.4, 0.36, "foot");
            Cap(p, k, 0.1, 0.6, 0.2, 4.6, 0.85, 0.62);
            SideTorso(p, k);
            Cap(p, k, 0, -5.4, -0.1, -2.3, 0.55, 0.45);
            Cap(p, k, -0.1, -2.3, 0.35, 0.7, 0.45, 0.36);
            p.Circle(0.45 * k, 1.35 * k, 0.5 * k, S.OpaqueFine, "hand");
            Cap(p, k, 0.15, -6.8, 0.05, -6.0, 0.45, 0.45, "neck");
            SideHead(p, 0.3 * k, -8.75 * k + headR, headR);
        }

        private static void Walking(Pen p)
        {
            // far side limbs first
            Cap(p, 1, -0.9, 4.5, -2.4, 7.6, 0.6, 0.4);
            Cap(p, 1, -2.9, 7.7, -1.8, 8.35, 0.4, 0.36, "foot");
            Cap(p, 1, 0, 0.6, -0.9, 4.5, 0.85, 0.62);
            Cap(p, 1, 0, -5.4, -0.9, -2.5, 0.55, 0.45);
            Cap(p, 1, -0.9, -2.5, -1.4, 0.3, 0.45, 0.36);
            p.Circle(-1.5, 0.9, 0.5, S.OpaqueFine, "hand");
            // near side
            Cap(p, 1, 1.4, 4.3, 1.9, 7.9, 0.6, 0.4);
            Cap(p, 1, 1.8, 8.35, 3.2, 7.9, 0.4, 0.36, "foot");
            Cap(p, 1, 0.1, 0.6, 1.4, 4.3, 0.85, 0.62);
            SideTorso(p, 1);
            Cap(p, 1, 0, -5.4, 0.9, -2.6, 0.55, 0.45);
            Cap(p, 1, 0.9, -2.6, 2.1, -0.9, 0.45, 0.36);
            p.Circle(2.5, -0.5, 0.5, S.OpaqueFine, "hand");
            Cap(p, 1, 0.25, -6.8, 0.1, -6.0, 0.45, 0.45, "neck");
            SideHead(p, 0.4, -7.6, 1.15);
        }

        private static void TopHead(Pen p, double cx, double r)
        {
            p.PolyD(new[] { V(cx + r * 0.8, -r * 0.25), V(cx + r * 1.3, 0), V(cx + r * 0.8, r * 0.25) }, S.OpaqueFine, true, "nose");
            p.Circle(cx, 0, r, S.OpaqueFine, "head");
        }

        private static void TopStanding(Pen p)
        {
            foreach (var s in new[] { -1.0, 1.0 }) p.Ellipse(1.2, s * 0.65, 0.7, 0.35, S.OpaqueFine, "foot");
            p.Ellipse(-0.1, 0, 1.15, 2.4, S.OpaqueFine, "shoulders");
            TopHead(p, 0.1, 1.0);
        }

        private static void TopWalking(Pen p)
        {
            p.Ellipse(2.0, 0.6, 0.7, 0.35, S.OpaqueFine, "foot");
            p.Ellipse(-1.6, -0.6, 0.7, 0.35, S.OpaqueFine, "foot");
            p.Capsule(V(0, -2.0), V(1.5, -2.3), 0.45, 0.38, null, "limb");
            p.Capsule(V(0, 2.0), V(-1.4, 2.3), 0.45, 0.38, null, "limb");
            p.Ellipse(-0.1, 0, 1.15, 2.4, S.OpaqueFine, "shoulders");
            TopHead(p, 0.1, 1.0);
        }

        // ------------------------------------------------------------------ seated

        /// <summary>Seated figure facing +x (design rectangle x -3.1..8.1, y -7.4..7.2, drawn via a re-centred pen).</summary>
        private static void Seated(Pen pen, SketchObject o)
        {
            var p = new Pen(new Size2(pen.W, pen.H), new Rect(-3.1, -7.4, 11.2, 14.6));
            var driver = o.Str("role", "driver") != "passenger";
            Cap(p, 1, 4.6, 1.8, 5.8, 6.2, 0.6, 0.4);
            Cap(p, 1, 5.7, 6.5, 7.4, 6.4, 0.42, 0.38, "foot");
            Cap(p, 1, 0, 2.3, 4.6, 1.8, 0.9, 0.65);
            Cap(p, 1, -0.2, 2.0, -1.6, -3.8, 1.45, 1.25, "torso");
            Cap(p, 1, -1.2, -3.6, 0.8, -1.0, 0.5, 0.42);
            if (driver)
            {
                Cap(p, 1, 0.8, -1.0, 3.4, -2.4, 0.42, 0.32);
                p.Circle(3.9, -2.6, 0.45, S.OpaqueFine, "hand");
            }
            else
            {
                Cap(p, 1, 0.8, -1.0, 2.9, 1.0, 0.42, 0.32);
                p.Circle(3.4, 1.2, 0.45, S.OpaqueFine, "hand");
            }
            Cap(p, 1, -1.5, -4.6, -1.5, -5.2, 0.45, 0.45, "neck");
            SideHead(p, -1.5, -6.0, 1.15);
            foreach (var item in p.Items) pen.Add(item);
        }

        private static void SeatedTop(Pen pen, SketchObject o)
        {
            var p = new Pen(new Size2(pen.W, pen.H), new Rect(-2.6, -2.8, 9.2, 5.6));
            var driver = o.Str("role", "driver") != "passenger";
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Ellipse(5.6, s * 1.0, 0.7, 0.4, S.OpaqueFine, "foot");
                p.Capsule(V(0, s * 0.95), V(4.4, s * 1.0), 0.9, 0.7, null, "limb");
            }
            p.Ellipse(-1.0, 0, 1.15, 2.4, S.OpaqueFine, "shoulders");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Capsule(V(-0.8, s * 2.0), V(1.2, s * 2.2), 0.5, 0.42, null, "limb");
                var hand = driver ? V(3.6, s * 1.5) : V(2.8, s * 1.2);
                p.Capsule(V(1.2, s * 2.2), driver ? V(3.2, s * 1.6) : V(2.4, s * 1.3), 0.42, 0.34, null, "limb");
                p.Circle(hand.X, hand.Y, 0.45, S.OpaqueFine, "hand");
            }
            TopHead(p, -0.9, 1.0);
            foreach (var item in p.Items) pen.Add(item);
        }

        // ------------------------------------------------------------------ hand

        private static readonly Vec2 Fingertip = new(5.85, 0.25);

        private static AutoType Hand()
        {
            void Side(Pen p, SketchObject o)
            {
                p.Capsule(V(-6.6, 1.4), V(-3.0, 0.8), 1.4, 1.2, null, "forearm");
                p.Capsule(V(0.9, 0.5), V(1.5, 2.0), 0.45, 0.4, null, "finger");
                p.Capsule(V(0.4, 1.0), V(0.9, 2.5), 0.45, 0.4, null, "finger");
                p.Capsule(V(-0.2, 1.4), V(0.2, 2.8), 0.42, 0.38, null, "finger");
                p.Rounded(new[] { V(-3.4, -1.0), V(0.6, -1.3), V(1.6, 0.0), V(1.0, 2.0), V(-3.2, 2.3) }, 0.8, S.OpaqueFine, "palm");
                p.Capsule(V(0.6, -0.6), V(5.4, 0.2), 0.55, 0.45, null, "finger");
                p.Capsule(V(-0.8, -1.0), V(1.6, -2.1), 0.5, 0.4, null, "thumb");
                if (o.Flag("ripple", true))
                    foreach (var r in new[] { 1.0, 1.8 })
                        p.Arc(Fingertip.X, Fingertip.Y, r, r, -60, 120, S.Thin, "ripple");
            }
            return new AutoType("auto.human.hand", T("Hand (touch)", "Hand (Berührung)"), Cat,
                new[] { "hand", "finger", "touch", "gesture", "press", "hand", "finger", "berührung", "tippen", "geste", "touch" },
                Views.SideV(16.4, 9, Side) with { Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(Fingertip), "fingertip") } })
            {
                Params = new[] { Prm.Bool("ripple", "Touch indication", "Berührungsandeutung", true) },
                Extra = o => new[] { Prm.Toggle(o, "ripple", true, T("Touch indication", "Berührungsandeutung")) },
            };
        }

        // ------------------------------------------------------------------ head with gaze direction

        /// <summary>
        /// Head with gaze direction (point object): point 0 is the head centre, point 1 the gaze target. Drawn in top or
        /// side view with a dashed gaze arrow and an optional gaze cone.
        /// </summary>
        internal sealed class HeadType : AutoPointType
        {
            private static readonly ParamDef Size = Prm.Num("size", "Head size", "Kopfgröße", 6, 1, 60, "mm", 1);
            private static readonly ParamDef Cone = Prm.Num("cone", "Gaze cone angle", "Blickkegel", 0, 0, 120, "°", 10);
            private static readonly ViewDef[] ViewDefs =
            {
                new("top", Automotive.Views.Top) { Icon = IconIds.Top },
                new("side", Automotive.Views.Side) { Icon = IconIds.Side },
            };

            public HeadType() : base("auto.human.head", T("Head with gaze direction", "Kopf mit Blickrichtung"), Cat,
                "head", "gaze", "eye", "driver monitoring", "attention", "kopf", "blick", "blickrichtung", "auge", "fahrerbeobachtung", "aufmerksamkeit") { }

            public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(30, 0) };
            public override IReadOnlyList<ViewDef> Views => ViewDefs;
            public override IReadOnlyList<ParamDef> Parameters => new[] { Size, Cone };

            protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            {
                var c = p[0];
                var target = p[p.Count - 1];
                var r = Geo.Clamp(o.Num("size", 6), 0.5, 60) / 2;
                var dir = (target - c).Normalized();
                if (dir.Length < 0.5) dir = new Vec2(1, 0);
                var n = dir.Perp;
                var fine = S.Fine with { StrokeWidth = Math.Min(0.35, Math.Max(0.18, r * 0.12)) };
                var opaque = fine with { Fill = Fill.Solid("#ffffff") };
                var list = new List<Primitive>();
                var side = ViewOf(o) == "side";
                Vec2 eye;
                if (side)
                {
                    list.Add(Prim.Polygon(new[] { c + dir * r * 0.95 - n * r * 0.25, c + dir * r * 1.25 + n * r * 0.15, c + dir * r * 0.9 + n * r * 0.3 }, opaque) with { Tag = "nose" });
                    list.Add(Prim.Circle(c.X, c.Y, r, opaque) with { Tag = "head" });
                    var ear = c - dir * r * 0.1 + n * r * 0.05;
                    list.Add(Prim.Circle(ear.X, ear.Y, r * 0.2, fine) with { Tag = "ear" });
                    eye = c + dir * r * 0.55 - n * r * 0.15;
                    list.Add(Prim.Circle(eye.X, eye.Y, r * 0.08, fine with { Fill = Fill.Solid("#000000") }) with { Tag = "eye" });
                }
                else
                {
                    foreach (var s in new[] { -1.0, 1.0 })
                        list.Add(Prim.Polygon(Shapes.Capsule(c + n * s * r * 0.85, c + n * s * r * 1.12, r * 0.18, r * 0.15), opaque) with { Tag = "ear" });
                    list.Add(Prim.Polygon(new[] { c + dir * r * 0.8 - n * r * 0.25, c + dir * r * 1.3, c + dir * r * 0.8 + n * r * 0.25 }, opaque) with { Tag = "nose" });
                    list.Add(Prim.Circle(c.X, c.Y, r, opaque) with { Tag = "head" });
                    eye = c + dir * r * 1.3;
                }
                var dist = (target - eye).Length;
                if (dist > r * 0.2 && (target - c).Length > r)
                {
                    list.Add(new LinePrim(eye, target, Style.ThinLine with { Dash = DashStyle.Dash, ArrowEnd = ArrowHead.Open }) { Tag = "gaze" });
                    var cone = Geo.DegToRad(Geo.Clamp(o.Num("cone", 0), 0, 120));
                    if (cone > 0.01)
                        foreach (var s in new[] { -1.0, 1.0 })
                            list.Add(new LinePrim(eye, eye + (target - eye).Rotate(s * cone / 2), Style.Helper) { Tag = "cone" });
                }
                return list;
            }

            public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            {
                var current = ViewOf(o);
                foreach (var v in ViewDefs)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, v.Icon!, x => x with { View = id }) { IsActive = current == id, Group = "view" };
                }
                foreach (var a in Prm.PlusMinus("cone", 0, 0, 120, 10, false, T("Wider gaze cone", "Blickkegel breiter"), T("Narrower gaze cone", "Blickkegel schmaler"), "param"))
                    yield return a;
            }
        }
    }
}
