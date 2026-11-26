using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Fasteners and joints. Threads follow the ISO 6410 conventions (thick crest line, thin root line,
    /// thick thread limit, three-quarter thin root circle in end views); weld symbols are in the style of ISO 2553.
    /// </summary>
    internal static class Fasteners
    {
        public const string Cat = "mech.fasteners";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VSide = T("Side view", "Seitenansicht");
        private static readonly I18nText VTop = T("Top view", "Draufsicht");
        private static readonly I18nText VSection = T("Section", "Schnitt");

        public static IEnumerable<MechType> Types()
        {
            yield return new MechType("mech.screw", T("Hex bolt", "Sechskantschraube"), Cat,
                new[] { "screw", "bolt", "hex bolt", "thread", "schraube", "sechskantschraube", "gewinde", "verschraubung" },
                new ViewSpec("side", VSide, 42, 12, ScrewSide) { Anchors = ScrewAxis },
                new ViewSpec("top", VTop, 14, 14, ScrewTop))
            {
                Rotating = false,
            };
            yield return new MechType("mech.nut", T("Hex nut", "Sechskantmutter"), Cat,
                new[] { "nut", "hex nut", "mutter", "sechskantmutter", "gewinde" },
                // Bore: the nominal (major) thread radius 3.2 of the top view, so a nut fits a bolt (ShaftFit).
                new ViewSpec("side", VSide, 8, 14, NutSide) { Bore = (p, o) => 3.2 * p.Sy },
                new ViewSpec("top", VTop, 14, 14, NutTop) { Bore = (p, o) => 3.2 * p.K });
            yield return new MechType("mech.washer", T("Washer", "Unterlegscheibe"), Cat,
                new[] { "washer", "unterlegscheibe", "scheibe", "beilagscheibe" },
                new ViewSpec("side", VSide, 3, 16, (p, o) => WasherSide(p, false)) { Bore = (p, o) => 4.4 * p.Sy },
                new ViewSpec("section", VSection, 3, 16, (p, o) => WasherSide(p, true)) { Bore = (p, o) => 4.4 * p.Sy },
                new ViewSpec("top", VTop, 16, 16, WasherTop) { Bore = (p, o) => 4.4 * p.K });
            yield return new MechType("mech.rivet", T("Rivet", "Niet"), Cat,
                new[] { "rivet", "niet", "halbrundniet", "nietverbindung" },
                new ViewSpec("side", VSide, 16, 10, RivetSide) { Anchors = AxisAnchor },
                new ViewSpec("top", VTop, 10, 10, (p, o) => { p.Circle(0, 0, 4.6, S.Vis, "head"); p.CenterCross(0, 0, 5, 5); }))
            {
                Rotating = false,
            };
            yield return new MechType("mech.circlip", T("Retaining ring (circlip)", "Sicherungsring"), Cat,
                new[] { "circlip", "retaining ring", "snap ring", "shaft circlip", "sicherungsring", "seegerring", "wellensicherungsring", "sprengring" },
                // Bore: the inner radius of the ring, so it fits the shaft (groove) it is put on (ShaftFit).
                new ViewSpec("axial", T("Axial view", "Stirnansicht"), 22, 22, CirclipAxial) { Bore = (p, o) => ClipIn * p.K },
                new ViewSpec("side", VSide, 3, 22, CirclipSide) { Bore = (p, o) => ClipIn * p.Sy });
            yield return new MechType("mech.pin", T("Pin", "Stift"), Cat,
                new[] { "pin", "dowel pin", "cylindrical pin", "taper pin", "split pin", "cotter pin", "stift", "zylinderstift", "passstift", "kegelstift", "splint" },
                new ViewSpec("side", VSide, 30, 8, PinSide) { Anchors = AxisAnchor },
                new ViewSpec("top", T("End view", "Stirnansicht"), 8, 8, PinTop))
            {
                Params = new[] { PinKind },
                Rotating = false,
                Extra = o => ActionChoices.FromParam(o, "param.kind", PinKind, null, "param", PinKinds),
            };
            yield return new MechType("mech.join.weld", T("Weld", "Schweißnaht"), Cat,
                new[] { "weld", "fillet weld", "welding", "schweißnaht", "kehlnaht", "schweißen", "schweissnaht" },
                new ViewSpec("symbol", T("Symbol", "Symbol"), 16, 8, WeldSymbol),
                new ViewSpec("seam", T("Seam (section)", "Naht (Schnitt)"), 10, 10, (p, o) => p.Poly(S.Black, true, -5, 5, 5, 5, -5, -5)),
                new ViewSpec("bead", T("Bead (view)", "Raupe (Ansicht)"), 30, 6, WeldBead) { Fluid = true })
            {
                Rotating = false,
            };
            yield return new MechType("mech.join.adhesive", T("Adhesive joint", "Klebeverbindung"), Cat,
                new[] { "adhesive", "glue", "bonded joint", "klebeverbindung", "kleben", "klebstoff", "klebung" },
                new ViewSpec("layer", T("Layer", "Schicht"), 30, 3, (p, o) => p.Add(Prim.Rect(-p.W / 2, -p.H / 2, p.W, p.H, S.Dotted), "layer")) { Fluid = true },
                new ViewSpec("joint", T("Lap joint", "Überlappstoß"), 30, 12, (p, o) => LapJoint(p, false)))
            {
                Rotating = false,
            };
            yield return new MechType("mech.join.solder", T("Soldered joint", "Lötverbindung"), Cat,
                new[] { "solder", "brazing", "soldered joint", "lötverbindung", "löten", "lot", "hartlöten" },
                new ViewSpec("layer", T("Layer", "Schicht"), 30, 3, (p, o) => p.Add(Prim.Rect(-p.W / 2, -p.H / 2, p.W, p.H, S.Black), "layer")) { Fluid = true },
                new ViewSpec("joint", T("Lap joint", "Überlappstoß"), 30, 12, (p, o) => LapJoint(p, true)))
            {
                Rotating = false,
            };
            yield return new MechType("mech.join.press", T("Press fit", "Pressverband"), Cat,
                new[] { "press fit", "interference fit", "shrink fit", "pressverband", "presspassung", "querpressverband", "schrumpfverband" },
                new ViewSpec("section", VSection, 30, 20, PressFit) { Anchors = AxisAnchor });
        }

        private static IEnumerable<Anchor> AxisAnchor(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            yield return Anchor.Segment(AnchorRole.Axis, new Vec2(-hw, 0), new Vec2(hw, 0));
        }

        /// <summary>
        /// Bolt axis with its radii (<see cref="ShaftFit"/>): the head section (design x -21 to -14, radius 6) and the shank
        /// (radius 4, the nominal thread), so nuts and washers fit the thread.
        /// </summary>
        private static IEnumerable<Anchor> ScrewAxis(Pen p, SketchObject o)
        {
            var hw = p.W / 2;
            var head = 7.0 / 42;
            yield return Anchor.Shaft(new Vec2(-hw, 0), new Vec2(hw, 0), 4 * p.Sy, new[] { new AxisStep(0, head, 6 * p.Sy), new AxisStep(head, 1, 4 * p.Sy) });
        }

        /// <summary>Hex bolt: head on the left, shank with thread to the right (thin root lines, thick thread limit).</summary>
        private static void ScrewSide(Pen p, SketchObject o)
        {
            // head (hex across corners: three faces visible)
            p.Box(-20, -6, -14, 6, S.Vis, "head");
            p.Line(-20, -3, -14, -3, S.Vis, "head");
            p.Line(-20, 3, -14, 3, S.Vis, "head");
            // shank with chamfered end
            p.Poly(S.Vis, true, -14, -4, 19, -4, 20, -3, 20, 3, 19, 4, -14, 4);
            p.Line(2, -4, 2, 4, S.Vis, "thread-end");
            p.Line(2, -3.2, 19.8, -3.2, S.Thin, "thread-root");
            p.Line(2, 3.2, 19.8, 3.2, S.Thin, "thread-root");
            p.Line(-21, 0, 21, 0, S.Ctr, "axis");
        }

        private static void ScrewTop(Pen p, SketchObject o)
        {
            p.PolyD(Frag.Hexagon(0, 0, 6.6), S.Vis, true, "head");
            p.Circle(0, 0, 5.6, S.Thin, "washer-face");
            p.CenterCross(0, 0, 7, 7);
        }

        private static void NutSide(Pen p, SketchObject o)
        {
            p.Box(-4, -7, 4, 7, S.Vis, "body");
            p.Line(-4, -3.5, 4, -3.5, S.Vis, "face");
            p.Line(-4, 3.5, 4, 3.5, S.Vis, "face");
            p.Line(-4, -2.4, 4, -2.4, S.Hid, "thread");
            p.Line(-4, 2.4, 4, 2.4, S.Hid, "thread");
        }

        /// <summary>Nut end view: hexagon, thick minor-diameter circle, thin three-quarter major-diameter circle.</summary>
        private static void NutTop(Pen p, SketchObject o)
        {
            p.PolyD(Frag.Hexagon(0, 0, 7), S.Vis, true, "body");
            p.Hole(0, 0, 2.6, S.Vis, "minor");
            p.Arc(0, 0, 3.2, 3.2, -80, 270, S.Thin, "major");
            p.CenterCross(0, 0, 7, 6.2);
        }

        private static void WasherSide(Pen p, bool section)
        {
            if (section)
            {
                foreach (var s in new[] { -1.0, 1.0 }) p.Box(-1.5, s * 8, 1.5, s * 4.4, p.Hatch(), "washer");
                p.Line(-1.5, -4.4, -1.5, 4.4, S.Vis);
                p.Line(1.5, -4.4, 1.5, 4.4, S.Vis);
            }
            else
            {
                p.Box(-1.5, -8, 1.5, 8, S.Vis, "washer");
                p.Line(-1.5, -4.4, 1.5, -4.4, S.Hid, "hole");
                p.Line(-1.5, 4.4, 1.5, 4.4, S.Hid, "hole");
            }
            p.Line(-1.5, 0, 1.5, 0, S.Ctr, "axis");
        }

        private static void WasherTop(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 7.6, S.Vis, "outer");
            p.Hole(0, 0, 4.4, S.Vis, "inner");
            p.CenterCross(0, 0, 8, 8);
        }

        private static void RivetSide(Pen p, SketchObject o)
        {
            var pts = Pen.ArcD(-3.5, 0, 4.2, 4.8, 90, 180, 24);
            p.PolyD(pts, S.Vis, true, "head");
            p.Box(-3.5, -2.5, 7.5, 2.5, S.Vis, "shank");
            p.Line(-8, 0, 8, 0, S.Ctr, "axis");
        }

        // ------------------------------------------------------------------ retaining ring and pins

        /// <summary>Inner and outer radius of the retaining ring (design mm).</summary>
        private const double ClipIn = 6.6, ClipOut = 10;

        /// <summary>Open ring with the gap at the top and the two lugs with their holes for the circlip pliers.</summary>
        private static void CirclipAxial(Pen p, SketchObject o)
        {
            var ring = Pen.ArcD(0, 0, ClipOut, ClipOut, -70, 320);
            ring.AddRange(Pen.ArcD(0, 0, ClipIn, ClipIn, 250, -320));
            p.PolyD(ring, S.Vis, true, "ring");
            foreach (var a in new[] { -68.0, -112.0 })
            {
                var c = Frag.Polar(8.4, Geo.DegToRad(a));
                p.Hole(c.X, c.Y, 2.3, S.Vis, "lug");
                p.Hole(c.X, c.Y, 0.9, S.Vis, "hole");
            }
            p.CenterCross(0, 0, 11, 11);
        }

        /// <summary>Ring seen from the side, sitting in the shaft groove: the two rectangular sections blackened.</summary>
        private static void CirclipSide(Pen p, SketchObject o)
        {
            foreach (var s in new[] { -1.0, 1.0 }) p.Box(-1, s * ClipIn, 1, s * ClipOut, S.Black, "ring");
            p.Axis(1.5);
        }

        private static readonly string[] PinKinds = { "cylindrical", "taper", "split" };

        private static readonly ParamDef PinKind = new ParamDef("kind", T("Pin type", "Stiftart"), ParamKind.Choice)
        {
            Default = "cylindrical",
            Options = new[]
            {
                new ParamOption("cylindrical", T("Cylindrical pin", "Zylinderstift")),
                new ParamOption("taper", T("Taper pin", "Kegelstift")),
                new ParamOption("split", T("Split pin", "Splint")),
            },
        }.WithOptionIcons(v => v switch { "taper" => IconIds.ShapeTriangle, "split" => IconIds.Split, _ => IconIds.ShapeSquare });

        private static string PinKindOf(SketchObject o) => o.Str("kind", "cylindrical");

        /// <summary>
        /// Pin from the side: cylindrical pin with chamfered ends, taper pin (taper exaggerated so it shows), split pin with the
        /// eye on the left and two half-round legs of unequal length.
        /// </summary>
        private static void PinSide(Pen p, SketchObject o)
        {
            switch (PinKindOf(o))
            {
                case "taper":
                    p.Poly(S.Vis, true, -14.6, -3, 14.6, -2.3, 15, -1.9, 15, 1.9, 14.6, 2.3, -14.6, 3, -15, 2.6, -15, -2.6);
                    p.Line(-15, 0, 15, 0, S.Ctr, "axis");
                    break;
                case "split":
                    {
                        var eye = new List<Vec2> { new(-8, -2.6) };
                        eye.AddRange(Pen.ArcD(-11.4, 0, 3.6, 3.6, -90, -180));
                        eye.Add(new Vec2(-8, 2.6));
                        p.PolyD(eye, S.Vis, false, "eye");
                        p.Ellipse(-11.6, 0, 1.8, 1.2, S.Vis, "eye");
                        p.Poly(S.Vis, true, -8, -2.6, 12.2, -2.6, 13, -1.3, 12.2, 0, -8, 0);
                        p.Poly(S.Vis, true, -8, 0, 14.2, 0, 15, 1.3, 14.2, 2.6, -8, 2.6);
                        break;
                    }
                default:
                    p.Poly(S.Vis, true, -14.4, -3, 14.4, -3, 15, -2.4, 15, 2.4, 14.4, 3, -14.4, 3, -15, 2.4, -15, -2.4);
                    p.Line(-15, 0, 15, 0, S.Ctr, "axis");
                    break;
            }
        }

        private static void PinTop(Pen p, SketchObject o)
        {
            switch (PinKindOf(o))
            {
                case "taper":
                    p.Circle(0, 0, 3, S.Vis, "large-end");
                    p.Circle(0, 0, 2.3, S.Hid, "small-end");
                    break;
                case "split":
                    p.Circle(0, 0, 2.6, S.Vis, "legs");
                    p.Line(-2.6, 0, 2.6, 0, S.Vis, "split");
                    break;
                default:
                    p.Circle(0, 0, 3, S.Vis, "pin");
                    p.Circle(0, 0, 2.4, S.Thin, "chamfer");
                    break;
            }
            p.CenterCross(0, 0, 3.8, 3.8);
        }

        /// <summary>Fillet weld symbol (triangle) on the reference line.</summary>
        private static void WeldSymbol(Pen p, SketchObject o)
        {
            p.Line(-8, 3, 8, 3, S.Thin, "reference");
            p.Poly(S.Vis, true, -2.5, 3, 2.5, 3, -2.5, -2.5);
        }

        /// <summary>Weld bead in view: row of ripples (arcs) between two boundary lines.</summary>
        private static void WeldBead(Pen p, SketchObject o)
        {
            double hw = p.W / 2, hh = p.H / 2;
            p.LineP(new Vec2(-hw, -hh), new Vec2(hw, -hh), S.Vis, "edge");
            p.LineP(new Vec2(-hw, hh), new Vec2(hw, hh), S.Vis, "edge");
            var rx = Math.Min(hh * 0.6, 2);
            var step = rx * 1.1;
            for (var x = -hw; x + rx <= hw + 1e-9; x += step)
                p.PolyP(Geo.ArcPoints(new Vec2(x, 0), rx, hh, -Math.PI / 2, Math.PI, 12), S.Thin, false, "ripple");
        }

        /// <summary>Two hatched plates overlapping with the joining layer between them.</summary>
        private static void LapJoint(Pen p, bool solder)
        {
            p.Box(-15, -6, 6, -1, p.Hatch(45), "plate");
            p.Box(-6, 1, 15, 6, p.Hatch(135), "plate");
            p.Box(-6, -1, 6, 1, solder ? S.Black : S.Dotted, "layer");
            if (solder)
            {
                p.Poly(S.Black, true, 6, -1, 6, 1, 8, 1);
                p.Poly(S.Black, true, -6, 1, -6, -1, -8, -1);
            }
        }

        /// <summary>Interference fit in axial section: hub overlaps the (unsectioned) shaft by an exaggerated allowance.</summary>
        private static void PressFit(Pen p, SketchObject o)
        {
            p.Box(-15, -4.5, 15, 4.5, S.Vis, "shaft");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Box(-8, s * 10, 8, s * 4.0, p.Hatch(), "hub");
                p.Line(-8, s * 4.5, 8, s * 4.5, S.Heavy, "interface");
            }
            p.Line(-15, 0, 15, 0, S.Ctr, "axis");
        }
    }
}
