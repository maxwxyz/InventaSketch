using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Belt and chain drives: pulleys, sprockets, belts and chains.</summary>
    internal static class Drives
    {
        public const string Cat = "mech.drives";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSide = T("Side view", "Seitenansicht");
        private static readonly I18nText VSection = T("Section", "Schnitt");
        private static readonly I18nText VAxial = T("Axial view", "Stirnansicht");

        private const double Tip = 14, PitchR = 12.6;

        private static Anchor Pitch(Pen p, double r) => Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, r * p.K);

        /// <summary>
        /// Bore of pulleys and sprockets (design 3 of the rim radius 15): across the axis in the side, section and schematic
        /// views (the schematic has the same height), round in the axial views.
        /// </summary>
        private const double BoreR = 3;

        private static double SideBore(Pen p, SketchObject o) => BoreR * p.Sy;

        private static double AxialBore(Pen p, SketchObject o) => BoreR * p.K;

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType("mech.pulley", T("Belt pulley", "Riemenscheibe"), Cat,
                new[] { "pulley", "sheave", "v-belt pulley", "riemenscheibe", "keilriemenscheibe", "scheibe", "riementrieb" },
                new ViewSpec("stick", VStick, 8, 30, PulleyStick) { Bore = SideBore },
                new ViewSpec("axial", VAxial, 30, 30, PulleyAxial) { Anchors = (p, o) => new[] { Pitch(p, PitchR) }, Bore = AxialBore },
                new ViewSpec("side", VSide, 10, 30, (p, o) => PulleySide(p, false)) { Bore = SideBore },
                new ViewSpec("section", VSection, 10, 30, (p, o) => PulleySide(p, true)) { Bore = SideBore })
            {
                Params = new[] { Common.Hub },
                Extra = o => Common.HubChoice(o),
            };
            yield return new MechType("mech.belt", T("Belt", "Riemen"), Cat,
                new[] { "belt", "v-belt", "timing belt", "belt drive", "riemen", "keilriemen", "zahnriemen", "riementrieb" },
                new ViewSpec("open", T("Open belt", "Offener Riemen"), 60, 24, (p, o) => Belt(p, o, false)) { Fluid = true, Anchors = EndHubs },
                new ViewSpec("crossed", T("Crossed belt", "Gekreuzter Riemen"), 60, 24, (p, o) => Belt(p, o, true)) { Fluid = true, Anchors = EndHubs },
                new ViewSpec("loop", T("Closed loop", "Geschlossene Schleife"), 60, 24, BeltLoop) { Fluid = true, Anchors = LoopHubs },
                new ViewSpec(Serpentine, T("Over several pulleys", "Über mehrere Scheiben"), 80, 50, BeltSerpentine) { Fluid = true, Anchors = RouteHubs, Grips = RouteGrips })
            {
                Params = new[] { new ParamDef("ratio", T("Diameter ratio right/left", "Durchmesserverhältnis rechts/links"), ParamKind.Number) { Min = 0.1, Max = 10, Step = 0.1 }, Route },
                Rotating = false,
                Spans = true,
                Extra = RouteActions,
            };
            yield return new MechType("mech.sprocket", T("Chain sprocket", "Kettenrad"), Cat,
                new[] { "sprocket", "chain wheel", "kettenrad", "ritzel", "kettentrieb" },
                new ViewSpec("stick", VStick, 8, 30, SprocketStick) { Bore = SideBore },
                new ViewSpec("axial", VAxial, 30, 30, SprocketAxial) { Anchors = (p, o) => new[] { Pitch(p, PitchR) }, Bore = AxialBore },
                new ViewSpec("teeth", T("With teeth", "Mit Zähnen"), 30, 30, SprocketTeeth) { Anchors = (p, o) => new[] { Pitch(p, PitchR) }, Bore = AxialBore },
                new ViewSpec("side", VSide, 6, 30, SprocketSide) { Bore = SideBore })
            {
                Params = new[] { Common.Hub, Common.Teeth(18) },
                Extra = o => Common.HubChoice(o),
            };
            yield return new MechType("mech.chain", T("Roller chain", "Rollenkette"), Cat,
                new[] { "chain", "roller chain", "chain drive", "kette", "rollenkette", "kettentrieb" },
                new ViewSpec("links", T("Links", "Kettenglieder"), 60, 24, Chain) { Fluid = true, Anchors = EndHubs },
                new ViewSpec("line", T("Pitch line", "Teilungslinie"), 60, 24, ChainLine) { Fluid = true, Anchors = EndHubs },
                new ViewSpec(Serpentine, T("Over several sprockets", "Über mehrere Kettenräder"), 80, 50, ChainSerpentine) { Fluid = true, Anchors = RouteHubs, Grips = RouteGrips })
            {
                Params = new[] { Route },
                Rotating = false,
                Spans = true,
                Extra = RouteActions,
            };
            yield return new MechType(IdlerId, T("Idler pulley", "Umlenkrolle"), Cat,
                new[] { "idler", "idler pulley", "guide pulley", "back idler", "umlenkrolle", "spannrolle", "leitrolle", "riementrieb" },
                new ViewSpec("axial", VAxial, 30, 30, IdlerAxial) { Anchors = (p, o) => new[] { Pitch(p, IdlerPitch(o)) }, Bore = AxialBore },
                new ViewSpec("side", VSide, 10, 30, (p, o) => IdlerSide(p, o, false)) { Bore = SideBore },
                new ViewSpec("section", VSection, 10, 30, (p, o) => IdlerSide(p, o, true)) { Bore = SideBore },
                new ViewSpec("stick", VStick, 8, 30, IdlerStick) { Bore = SideBore })
            {
                Params = new[] { IdlerHub, BackSide },
                Extra = o => ActionChoices.FromParam(o, "param.hub", IdlerHub, null, "param", Common.HubKinds)
                    .Append(Common.Toggle(o, "back", true, T("Runs on belt back", "Läuft auf Riemenrücken"), IconIds.SideSwap)),
            };
            yield return new MechType(TensionerId, T("Belt tensioner", "Riemenspanner"), Cat,
                new[] { "tensioner", "belt tensioner", "automatic tensioner", "tensioner pulley", "riemenspanner", "spannrolle", "spannarm", "riementrieb" },
                new ViewSpec("axial", VAxial, 40, 28, TensionerAxial) { Anchors = TensionerAnchors },
                new ViewSpec("stick", VStick, 40, 28, TensionerStick) { Anchors = TensionerAnchors })
            {
                Rotating = false,
            };
        }

        // ------------------------------------------------------------------ serpentine route (belt and chain)

        public const string Serpentine = "serpentine";
        public const string IdlerId = "mech.pulley.idler";
        public const string TensionerId = "mech.tensioner";

        /// <summary>
        /// The serpentine route (<see cref="BeltRoute"/>). No stored default: empty means the default route, so belts and chains
        /// between two hubs carry no extra parameter.
        /// </summary>
        private static readonly ParamDef Route = new("route", T("Route over the hubs (u,v,k[,b]; …)", "Verlauf über die Naben (u,v,k[,b]; …)"), ParamKind.Text)
        {
            Advanced = true,
        };

        private static bool IsSerpentine(SketchObject o) => o.View == Serpentine;

        /// <summary>The route hubs placed in the actual box (chains keep room for their plates).</summary>
        private static List<BeltRoute.Placed> Placed(Pen p, SketchObject o, double margin = 0) => BeltRoute.Place(BeltRoute.Of(o), p.W, p.H, margin);

        /// <summary>Half the plate height of a chain drawn along a route (actual mm).</summary>
        private static double ChainHalf(Pen p) => Math.Min(1.6, Math.Max(0.5, Math.Min(p.W, p.H) * 0.03));

        private static List<BeltRoute.Placed> PlacedFor(Pen p, SketchObject o) => Placed(p, o, o.Type == "mech.chain" ? ChainHalf(p) : 0);

        /// <summary>One hub anchor per route hub, in route order: pulleys and sprockets snap onto them as onto the end hubs.</summary>
        private static IEnumerable<Anchor> RouteHubs(Pen p, SketchObject o)
        {
            var hubs = PlacedFor(p, o);
            for (var i = 0; i < hubs.Count; i++) yield return Anchor.Point(AnchorRole.Hub, hubs[i].C, "hub" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void BeltSerpentine(Pen p, SketchObject o) => p.PolyP(BeltRoute.Loop(Placed(p, o)), S.Vis, true, "belt");

        private static void ChainSerpentine(Pen p, SketchObject o)
        {
            var a = ChainHalf(p);
            var hubs = Placed(p, o, a);
            p.PolyP(BeltRoute.Loop(hubs, a), S.Vis, true, "plate");
            p.PolyP(BeltRoute.Loop(hubs, -a), S.Vis, true, "plate");
            foreach (var c in BeltRoute.Sample(BeltRoute.Loop(hubs), Math.Max(2.5, a * 3.2))) p.Add(Prim.Circle(c.X, c.Y, a * 0.6, S.Vis), "roller");
        }

        /// <summary>Grips of a route: every hub centre (free) and its running radius (horizontal, at the right of the circle).</summary>
        private static IEnumerable<MechGrip> RouteGrips(Pen p, SketchObject o)
        {
            var route = BeltRoute.Of(o);
            var hubs = PlacedFor(p, o);
            for (var i = 0; i < hubs.Count && i < route.Count; i++)
            {
                var k = i;
                var n = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                yield return new MechGrip(new DragHandle("hub." + k, hubs[i].C, DragConstraint.Free, T("Hub " + n, "Nabe " + n)), (x, pen, local) =>
                    WithHub(x, k, h => new BeltRoute.Hub(Dragging.Snap(local.X / pen.W, 0.005), Dragging.Snap(local.Y / pen.H, 0.005), h.K, h.Back)));
                yield return new MechGrip(new DragHandle("radius." + k, hubs[i].C + new Vec2(hubs[i].R, 0), DragConstraint.Horizontal, T("Radius " + n, "Radius " + n)), (x, pen, local) =>
                {
                    var c = BeltRoute.Place(BeltRoute.Of(x), pen.W, pen.H)[k].C;
                    return WithHub(x, k, h => new BeltRoute.Hub(h.U, h.V, Dragging.Snap(Math.Abs(local.X - c.X) / Math.Min(pen.W, pen.H), 0.005), h.Back));
                });
            }
        }

        private static SketchObject WithHub(SketchObject o, int index, Func<BeltRoute.Hub, BeltRoute.Hub> edit)
        {
            var route = BeltRoute.Of(o).ToList();
            if (index < 0 || index >= route.Count) return o;
            route[index] = BeltRoute.Clamp(edit(route[index]));
            return o.WithParam("route", BeltRoute.Format(route));
        }

        /// <summary>
        /// Serpentine view: add a back-side idler between the last and the first hub (it bends that strand inwards), remove the
        /// last hub (at least two stay).
        /// </summary>
        private static IEnumerable<ContextAction> RouteActions(SketchObject o)
        {
            if (!IsSerpentine(o)) yield break;
            var route = BeltRoute.Of(o);
            if (route.Count < 12)
                yield return new ContextAction("route.add", T("Add idler", "Umlenkrolle hinzufügen"), IconIds.AddPoint, x =>
                {
                    var r = BeltRoute.Of(x).ToList();
                    BeltRoute.Hub a = r[r.Count - 1], b = r[0];
                    var k = Math.Max(0.04, Math.Min(0.1, Math.Min(a.K, b.K) * 0.6));
                    r.Add(BeltRoute.Clamp(new BeltRoute.Hub((a.U + b.U) / 2, (a.V + b.V) / 2, k, true)));
                    return x.WithParam("route", BeltRoute.Format(r));
                }) { Group = "param" };
            if (route.Count > 2)
                yield return new ContextAction("route.remove", T("Remove last hub", "Letzte Nabe entfernen"), IconIds.RemovePoint, x =>
                {
                    var r = BeltRoute.Of(x).ToList();
                    return r.Count > 2 ? x.WithParam("route", BeltRoute.Format(r.Take(r.Count - 1))) : x;
                }) { Group = "param" };
        }

        // ------------------------------------------------------------------ idler pulley and tensioner

        private static readonly ParamDef IdlerHub = Common.Hub with { Default = "loose" };

        private static readonly ParamDef BackSide = new("back", T("Runs on the belt back (flat roller)", "Läuft auf dem Riemenrücken (glatte Rolle)"), ParamKind.Boolean) { Default = true };

        private static bool Back(SketchObject o) => o.Flag("back", true);

        /// <summary>Running radius: the flat rim for the belt back, the groove pitch line for the running face.</summary>
        private static double IdlerPitch(SketchObject o) => Back(o) ? Tip : PitchR;

        private static void IdlerAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, Tip, S.Vis, "rim");
            if (Back(o)) p.Circle(0, 0, 12.4, S.Thin, "flange");
            else
            {
                p.Circle(0, 0, PitchR, S.Ctr, "pitch");
                p.Circle(0, 0, 11, S.Thin, "groove");
            }
            p.Hole(0, 0, 6.5, S.Thin, "bearing");
            p.Hole(0, 0, 5, S.Vis, "hub");
            p.Hole(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        /// <summary>Flat roller (belt back) or grooved roller (running face) on a rolling bearing; the section shows the bearing simplified.</summary>
        private static void IdlerSide(Pen p, SketchObject o, bool section)
        {
            var back = Back(o);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (section)
                {
                    if (back) p.Poly(p.Hatch(), true, -4, s * 15, 4, s * 15, 4, s * 7, -4, s * 7);
                    else p.Poly(p.Hatch(), true, -4, s * 15, -1.6, s * 15, 0, s * 11.5, 1.6, s * 15, 4, s * 15, 4, s * 7, -4, s * 7);
                    // rolling bearing, simplified: rectangle with a diagonal cross
                    p.Box(-3, s * 7, 3, s * 3, S.Vis, "bearing");
                    p.Line(-3, s * 7, 3, s * 3, S.Thin, "bearing");
                    p.Line(-3, s * 3, 3, s * 7, S.Thin, "bearing");
                }
                if (!back) p.Line(-5, s * PitchR, 5, s * PitchR, S.Ctr, "pitch");
            }
            if (!section)
            {
                if (back)
                {
                    p.Box(-4, -15, 4, 15, S.Vis, "roller");
                    p.Line(-4, -13.5, 4, -13.5, S.Thin, "flange");
                    p.Line(-4, 13.5, 4, 13.5, S.Thin, "flange");
                }
                else p.Poly(S.Vis, true, -4, -15, -1.6, -15, 0, -11.5, 1.6, -15, 4, -15, 4, 15, 1.6, 15, 0, 11.5, -1.6, 15, -4, 15);
                p.Line(-4, -3, 4, -3, S.Hid, "bore");
                p.Line(-4, 3, 4, 3, S.Hid, "bore");
            }
            p.Axis(5);
        }

        private static void IdlerStick(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (Back(o)) p.Line(-4, s * 15, 4, s * 15, S.Stick, "rim");
                else p.Poly(S.Stick, false, -4, s * 15, -1.6, s * 15, 0, s * 13, 1.6, s * 15, 4, s * 15);
            }
            Frag.Hub(p, p.V(0, -13), p.V(0, 0), p.V(0, 13), o.Str("hub", "loose"), S.Stick);
        }

        /// <summary>Tensioner geometry (design mm): arm pivot and idler centre.</summary>
        private static readonly Vec2 Pivot = new(-12, 6), IdlerAt = new(9, -3);

        private const double IdlerR = 10;

        /// <summary>The idler hub (the belt runs over it like over a pulley) and the arm pivot (mount on the engine or frame).</summary>
        private static IEnumerable<Anchor> TensionerAnchors(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.Hub, p.V(IdlerAt), "idler");
            yield return Anchor.Point(AnchorRole.Mount, p.V(Pivot), "pivot");
        }

        /// <summary>
        /// Arm outline (design mm) from the pivot boss (radius <paramref name="boss"/>) to the idler rim: the pulley in front
        /// hides the rest of the arm.
        /// </summary>
        private static List<Vec2> Arm(double w0, double w1, double boss)
        {
            var u = (IdlerAt - Pivot).Normalized();
            var n = u.Perp;
            var a = Pivot + u * Math.Sqrt(Math.Max(0, boss * boss - w0 * w0));
            var b = IdlerAt - u * Math.Sqrt(Math.Max(0, IdlerR * IdlerR - w1 * w1));
            return new List<Vec2> { a + n * w0, b + n * w1, b - n * w1, a - n * w0 };
        }

        /// <summary>Tensioner seen along the axes: sprung arm with the spiral spring at the pivot, idler pulley on a bearing.</summary>
        private static void TensionerAxial(Pen p, SketchObject o)
        {
            var arm = Arm(3.2, 2.6, 3.8);
            p.Line(arm[0].X, arm[0].Y, arm[1].X, arm[1].Y, S.Vis, "arm");
            p.Line(arm[3].X, arm[3].Y, arm[2].X, arm[2].Y, S.Vis, "arm");
            // spiral (torsion) spring around the pivot, its outer leg against a stop on the base
            var spiral = new List<Vec2>();
            for (var i = 0; i <= 60; i++)
            {
                var t = i / 60.0;
                var a = Geo.DegToRad(90 + 450 * t);
                spiral.Add(Pivot + new Vec2(Math.Cos(a), Math.Sin(a)) * (4.6 + 2.6 * t));
            }
            p.PolyD(spiral, S.Vis, false, "spring");
            p.Hole(-18.4, 2.2, 1, S.Black, "stop");
            p.Hole(Pivot.X, Pivot.Y, 3.8, S.Vis, "boss");
            p.Hole(Pivot.X, Pivot.Y, 1.6, S.Vis, "pivot");
            p.Circle(IdlerAt.X, IdlerAt.Y, IdlerR, S.Vis, "rim");
            p.Circle(IdlerAt.X, IdlerAt.Y, 8.8, S.Thin, "flange");
            p.Hole(IdlerAt.X, IdlerAt.Y, 3.4, S.Vis, "hub");
            p.Hole(IdlerAt.X, IdlerAt.Y, 1.6, S.Vis, "bore");
            p.CenterCross(IdlerAt.X, IdlerAt.Y, 10.8, 10.8);
        }

        /// <summary>Schematic: pivoted lever (ground at the pivot), idler circle at its end, tension spring to ground.</summary>
        private static void TensionerStick(Pen p, SketchObject o)
        {
            p.Line(Pivot.X, Pivot.Y, IdlerAt.X, IdlerAt.Y, S.Stick, "arm");
            p.Circle(IdlerAt.X, IdlerAt.Y, IdlerR, S.Stick, "idler");
            p.Hole(IdlerAt.X, IdlerAt.Y, 1, S.Stick, "hub");
            p.Hole(Pivot.X, Pivot.Y, 1.6, S.Stick, "pivot");
            p.Line(Pivot.X - 5, Pivot.Y + 4, Pivot.X + 5, Pivot.Y + 4, S.Stick, "ground");
            Frag.GroundStrokes(p, p.V(Pivot.X - 5, Pivot.Y + 4), p.V(Pivot.X + 5, Pivot.Y + 4), new Vec2(0, 1), 2.5 * p.Sy, 2 * p.K);
            p.Line(Pivot.X - 1.4, Pivot.Y + 1.2, Pivot.X - 2.4, Pivot.Y + 4, S.Stick, "support");
            p.Line(Pivot.X + 1.4, Pivot.Y + 1.2, Pivot.X + 2.4, Pivot.Y + 4, S.Stick, "support");
            // tension spring from the middle of the arm down to ground
            var m = Vec2.Lerp(Pivot, IdlerAt, 0.45);
            p.PolyD(Frag.Zigzag(m.Y, 10, m.X, 1.6, 4).Select(q => new Vec2(q.Y, q.X)), S.Vis, false, "spring");
            p.Line(m.X - 3, 10, m.X + 3, 10, S.Stick, "ground");
            Frag.GroundStrokes(p, p.V(m.X - 3, 10), p.V(m.X + 3, 10), new Vec2(0, 1), 2.5 * p.Sy, 2 * p.K);
        }

        // ------------------------------------------------------------------ pulley

        private static void PulleyStick(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
                p.Poly(S.Stick, false, -4, s * 15, -1.6, s * 15, 0, s * 13, 1.6, s * 15, 4, s * 15);
            Frag.Hub(p, p.V(0, -13), p.V(0, 0), p.V(0, 13), o.Str("hub", "fixed"), S.Stick);
        }

        private static void PulleyAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, Tip, S.Vis, "rim");
            p.Circle(0, 0, PitchR, S.Ctr, "pitch");
            p.Circle(0, 0, 11, S.Thin, "groove");
            p.Hole(0, 0, 5.5, S.Vis, "hub");
            p.Hole(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        private static void PulleySide(Pen p, bool section)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                if (section)
                    p.Poly(p.Hatch(), true, -4, s * 15, -1.6, s * 15, 0, s * 11.5, 1.6, s * 15, 4, s * 15, 4, s * 3, -4, s * 3);
                p.Line(-5, s * PitchR, 5, s * PitchR, S.Ctr, "pitch");
            }
            if (!section)
            {
                p.Poly(S.Vis, true, -4, -15, -1.6, -15, 0, -11.5, 1.6, -15, 4, -15, 4, 15, 1.6, 15, 0, 11.5, -1.6, 15, -4, 15);
                p.Line(-4, -3, 4, -3, S.Hid, "bore");
                p.Line(-4, 3, 4, 3, S.Hid, "bore");
            }
            p.Axis(5);
        }

        // ------------------------------------------------------------------ belt

        private static (double L, double R) Radii(Pen p, SketchObject o)
        {
            var hh = p.H / 2;
            var ratio = o.Num("ratio", 1);
            if (ratio <= 0) ratio = 1;
            return ratio <= 1 ? (hh, hh * ratio) : (hh / ratio, hh);
        }

        /// <summary>Loop radii, reduced if the box is narrower than both pulleys.</summary>
        private static (double L, double R) LoopRadii(Pen p, SketchObject o)
        {
            var (rl, rr) = Radii(p, o);
            var k = Math.Min(1, p.W / (rl + rr));
            return (rl * k, rr * k);
        }

        private static IEnumerable<Anchor> EndHubs(Pen p, SketchObject o)
        {
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(-p.W / 2, 0), "left");
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(p.W / 2, 0), "right");
        }

        private static IEnumerable<Anchor> LoopHubs(Pen p, SketchObject o)
        {
            var (rl, rr) = LoopRadii(p, o);
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(-p.W / 2 + rl, 0), "left");
            yield return Anchor.Point(AnchorRole.Hub, new Vec2(p.W / 2 - rr, 0), "right");
        }

        /// <summary>Belt strands between two pulleys whose centres are at the left and right box edges.</summary>
        private static void Belt(Pen p, SketchObject o, bool crossed)
        {
            var hw = p.W / 2;
            var (rl, rr) = Radii(p, o);
            p.LineP(new Vec2(-hw, -rl), new Vec2(hw, crossed ? rr : -rr), S.Vis, "strand");
            p.LineP(new Vec2(-hw, rl), new Vec2(hw, crossed ? -rr : rr), S.Vis, "strand");
        }

        private static void BeltLoop(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            var (rl, rr) = LoopRadii(p, o);
            double cl = -hw + rl, cr = hw - rr;
            var pts = new List<Vec2> { new(cl, -rl) };
            pts.AddRange(Geo.ArcPoints(new Vec2(cr, 0), rr, rr, -Math.PI / 2, Math.PI, 24));
            pts.AddRange(Geo.ArcPoints(new Vec2(cl, 0), rl, rl, Math.PI / 2, Math.PI, 24));
            p.PolyP(pts, S.Vis, true, "belt");
        }

        // ------------------------------------------------------------------ sprocket and chain

        private static void SprocketStick(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Line(-4, s * 15, 4, s * 15, S.Stick, "tip");
                p.Round(p.V(0, s * 13.2), 1.0, S.Vis, "roller");
            }
            Frag.Hub(p, p.V(0, -15), p.V(0, 0), p.V(0, 15), o.Str("hub", "fixed"), S.Stick);
        }

        private static void SprocketAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, Tip, S.Vis, "tip");
            p.Circle(0, 0, PitchR, S.Ctr, "pitch");
            p.Circle(0, 0, 11.2, S.Thin, "root");
            p.Hole(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        private static void SprocketTeeth(Pen p, SketchObject o)
        {
            var z = GearMath.Teeth(o, 18);
            p.PolyD(GearMath.Sprocket(z, PitchR, Tip, -Math.PI / 2), S.Vis, true, "teeth");
            p.Circle(0, 0, PitchR, S.Ctr, "pitch");
            p.Hole(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        private static void SprocketSide(Pen p, SketchObject o)
        {
            p.Poly(S.Vis, true, -1.6, -15, 1.6, -15, 2.4, -13, 2.4, 13, 1.6, 15, -1.6, 15, -2.4, 13, -2.4, -13);
            p.Line(-3, -PitchR, 3, -PitchR, S.Ctr, "pitch");
            p.Line(-3, PitchR, 3, PitchR, S.Ctr, "pitch");
            p.Line(-2.4, -3, 2.4, -3, S.Hid, "bore");
            p.Line(-2.4, 3, 2.4, 3, S.Hid, "bore");
            p.Axis(3);
        }

        /// <summary>Chain strands with link plates and rollers (actual mm, pitch from strand height).</summary>
        private static void Chain(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            var a = Math.Min(1.6, p.H * 0.12);
            var pitch = Math.Max(2.5, a * 3.2);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var y = s * (hh - a);
                p.LineP(new Vec2(-hw, y - a), new Vec2(hw, y - a), S.Vis, "plate");
                p.LineP(new Vec2(-hw, y + a), new Vec2(hw, y + a), S.Vis, "plate");
                var n = Math.Max(1, (int)Math.Floor(p.W / pitch));
                var x0 = -(n - 1) * pitch / 2;
                for (var i = 0; i < n; i++) p.Add(Prim.Circle(x0 + i * pitch, y, a * 0.6, S.Vis), "roller");
            }
        }

        private static void ChainLine(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            p.LineP(new Vec2(-hw, -hh), new Vec2(hw, -hh), S.Ctr, "strand");
            p.LineP(new Vec2(-hw, hh), new Vec2(hw, hh), S.Ctr, "strand");
        }
    }
}
