using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Box symbols in the style of ISO 3952 kinematic diagrams (drawn independently): joints and supports, bodies.
    /// Every symbol is authored for a design box and stretched to the object size.
    /// </summary>
    internal static class Symbols
    {
        private static Vec2 Down => new(0, 1);

        public static IEnumerable<ObjectType> Types()
        {
            yield return Revolute();
            yield return GroundPivot();
            yield return Prismatic();
            yield return CamJoint();
            yield return Spherical();
            yield return GearContact();
            yield return Crank();
            yield return Rocker();
            yield return SliderBlock();
            yield return Piston();
            yield return RopePulley();
            yield return Gripper();
        }

        private static Anchor PortAt(Vec2 local, string id) => Anchor.Point(AnchorRole.Port, local, id);
        private static Anchor MountAt(Vec2 local, string id) => Anchor.Point(AnchorRole.Mount, local, id);

        // ------------------------------------------------------------------------------------------ joints & supports

        private static KinType Revolute() => new("kin.joint.revolute", new I18nText("Revolute joint", "Drehgelenk"), KinCategories.Joints, new Size2(8, 8),
            new[] { "revolute", "pin joint", "hinge", "pivot", "joint", "drehgelenk", "gelenk", "bolzen", "scharnier", "drehpaar" },
            ViewSpec.Schematic((p, o, c) => p.Joint(0, 0, 2.2)),
            ViewSpec.Detailed((p, o, c) =>
            {
                p.Round(0, 0, 3.4, KS.Body, "eye");
                p.Round(0, 0, 1.6, KS.Joint, KinTags.Joint);
                var cc = p.V(0, 0);
                var e = 3.9 * p.K;
                p.LineP(cc + new Vec2(-e, 0), cc + new Vec2(e, 0), KS.Ctr, "center");
                p.LineP(cc + new Vec2(0, -e), cc + new Vec2(0, e), KS.Ctr, "center");
            }))
        {
            Uniform = true,
            AnchorsFn = (p, o) => new[] { PortAt(p.V(0, 0), "c") },
            Role = LinkageRole.Joint,
        };

        private const double PivotY = -2.5, PivotR = 1.5;

        private static KinType GroundPivot() => new("kin.joint.ground", new I18nText("Fixed pivot (ground)", "Gestelldrehpunkt (Festlager)"), KinCategories.Joints, new Size2(12, 11),
            new[] { "ground", "fixed pivot", "frame", "support", "gestell", "festlager", "drehpunkt", "lagerbock", "fixpunkt", "stop", "anschlag", "limit" },
            ViewSpec.Schematic((p, o, c) => { Frag.GroundPivot(p.Items, p.V(0, PivotY), PivotR * p.K, Down, false); Stops(p, o); }),
            ViewSpec.Detailed((p, o, c) => { Frag.GroundPivot(p.Items, p.V(0, PivotY), PivotR * p.K, Down, true); Stops(p, o); }))
        {
            AnchorsFn = (p, o) => new[]
            {
                PortAt(p.V(0, PivotY), "c"),
                MountAt(p.V(0, PivotY) + new Vec2(0, 3 * PivotR * p.K), "base"),
            },
            Role = LinkageRole.FixedPivot,
            Params = new[]
            {
                KP.Bool("limited", "Stops (limited swing)", "Anschläge (begrenzter Schwenkbereich)", false),
                KP.Num("limitFrom", "Stop from (°, clockwise from right)", "Anschlag von (°, im Uhrzeigersinn von rechts)", -135, -360, 360, "°", 5),
                KP.Num("limitTo", "Stop to (°)", "Anschlag bis (°)", -45, -360, 360, "°", 5),
            },
            LimitsFn = PivotLimits,
        };

        /// <summary>
        /// Page angle range of a pivot with stops: the local range mirrored with the symbol (FlipX: a → 180° − a, FlipY:
        /// a → −a, the range reverses) and turned with it.
        /// </summary>
        internal static (double From, double To)? PivotLimits(SketchObject o)
        {
            if (!o.Flag("limited", false)) return null;
            double from = o.Num("limitFrom", -135), to = o.Num("limitTo", -45);
            if (o.Transform.FlipX) (from, to) = (180 - to, 180 - from);
            if (o.Transform.FlipY) (from, to) = (-to, -from);
            return (from + o.Transform.Rotation, to + o.Transform.Rotation);
        }

        /// <summary>Stops of a limited pivot: two thin stop lines at the range ends and a thin arc of the swing between them.</summary>
        private static void Stops(Pen p, SketchObject o)
        {
            if (!o.Flag("limited", false)) return;
            double from = o.Num("limitFrom", -135), to = o.Num("limitTo", -45);
            var c = p.V(0, PivotY);
            var r = 4 * PivotR * p.K;
            Vec2 Dir(double deg) => new(Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180));
            p.LineP(c + Dir(from) * (PivotR * p.K), c + Dir(from) * r, KS.Thin, "stop");
            p.LineP(c + Dir(to) * (PivotR * p.K), c + Dir(to) * r, KS.Thin, "stop");
            var sweep = ((to - from) % 360 + 360) % 360;
            var arc = new List<Vec2>();
            for (var i = 0; i <= 24; i++) arc.Add(c + Dir(from + sweep * i / 24) * (0.8 * r));
            p.Add(Prim.Polyline(arc, KS.Thin with { Dash = DashStyle.Dash }, false), "swing");
        }

        private static KinType Prismatic() => new("kin.joint.prismatic", new I18nText("Prismatic joint (slider on guide)", "Schubgelenk (Gleitstein auf Führung)"), KinCategories.Joints, new Size2(30, 10),
            new[] { "prismatic", "slider", "sliding pair", "guide", "linear", "schubgelenk", "gleitstein", "führung", "schubpaar", "schieber" },
            ViewSpec.Schematic((p, o, c) =>
            {
                p.Line(-15, 0, 15, 0, KS.Ctr, "axis");
                p.GroundLine(-15, 15, 2.6, 2.0);
                p.Box(-5.5, -2.6, 5.5, 2.6, KS.Body, KinTags.Slider);
                p.Joint(0, 0, 1.3);
            }),
            ViewSpec.Detailed((p, o, c) =>
            {
                p.Line(-15, 0, 15, 0, KS.Ctr, "axis");
                p.GroundLine(-15, 15, -2.9, 1.8, below: false);
                p.GroundLine(-15, 15, 2.9, 1.8);
                p.Box(-5.5, -2.6, 5.5, 2.6, KS.HatchWhite(1.2), KinTags.Slider);
                p.Joint(0, 0, 1.3);
            }))
        {
            AnchorsFn = (p, o) => new[]
            {
                PortAt(p.V(0, 0), "c"),
                Anchor.Segment(AnchorRole.Axis, p.V(-15, 0), p.V(15, 0)) with { Id = "axis" },
                MountAt(p.V(0, 2.6), "guide"),
            },
            Role = LinkageRole.Slider,
        };

        private static KinType CamJoint() => new("kin.joint.cam", new I18nText("Cam joint (higher pair)", "Kurvengelenk (höheres Paar)"), KinCategories.Joints, new Size2(22, 16),
            new[] { "cam joint", "cam pair", "higher pair", "contact", "kurvengelenk", "kurvenpaar", "höheres elementenpaar", "berührung" },
            ViewSpec.Schematic((p, o, c) => DrawCamJoint(p, false)),
            ViewSpec.Detailed((p, o, c) => DrawCamJoint(p, true)))
        {
            AnchorsFn = (p, o) => new[] { PortAt(p.V(0, 1.5), "contact") },
        };

        private static void DrawCamJoint(Pen p, bool detailed)
        {
            // Lower body: convex cam contour (circle arc, centre (0, 13.5), radius 12) touching the follower at (0, 1.5).
            var arc = new List<Vec2>();
            for (var i = 0; i <= 32; i++)
            {
                var x = -10.0 + 20.0 * i / 32;
                arc.Add(new Vec2(x, 13.5 - Math.Sqrt(144 - x * x)));
            }
            // Upper body: follower with a round tip (radius 3.5).
            var tip = new List<Vec2> { new(-3.5, -8) };
            tip.AddRange(Geo.ArcPoints(new Vec2(0, -2), 3.5, 3.5, Math.PI, -Math.PI, 16));
            tip.Add(new Vec2(3.5, -8));
            if (detailed)
            {
                var lower = arc.ToList(); lower.Add(new Vec2(10, 8)); lower.Add(new Vec2(-10, 8));
                p.PolyD(lower, KS.HatchWhite(1.3), true, "cam");
                p.PolyD(tip, KS.HatchWhite(1.3, 135), true, "follower");
            }
            else
            {
                p.PolyD(arc, KS.Link, false, "cam");
                // short strokes on the body side of the cam contour
                for (var i = 2; i <= 30; i += 4)
                {
                    var a = arc[i];
                    var n = (new Vec2(0, 13.5) - a).Normalized();
                    p.PolyD(new[] { a, a + n * 1.6 + new Vec2(-1.1, 0) }, KS.Thin, false, KinTags.Ground);
                }
                p.PolyD(tip, KS.Link, false, "follower");
            }
            p.Line(0, -7.5, 0, 7.5, KS.Ctr, "normal");
            p.Round(0, 1.5, 0.45, KS.Black, "contact");
        }

        private static KinType Spherical() => new("kin.joint.spherical", new I18nText("Spherical joint (ball joint)", "Kugelgelenk"), KinCategories.Joints, new Size2(18, 12),
            new[] { "spherical", "ball joint", "ball and socket", "kugelgelenk", "kugelpfanne", "gelenkkopf" },
            ViewSpec.Schematic((p, o, c) =>
            {
                p.Line(-9, 0, -4.6, 0, KS.Link, KinTags.Link);
                p.Line(2.4, 0, 9, 0, KS.Link, KinTags.Link);
                p.Arc(0, 0, 4.0, 4.0, 40, 280, KS.Link, "socket");
                p.Round(0, 0, 2.4, KS.Body, KinTags.Joint);
            }),
            ViewSpec.Detailed((p, o, c) =>
            {
                p.Line(-9, 0, -5.4, 0, KS.Link, KinTags.Link);
                var outer = Geo.ArcPoints(Vec2.Zero, 5.4, 5.4, Geo.DegToRad(40), Geo.DegToRad(280), 40);
                var inner = Geo.ArcPoints(Vec2.Zero, 3.9, 3.9, Geo.DegToRad(320), Geo.DegToRad(-280), 40);
                p.PolyD(outer.Concat(inner), KS.HatchWhite(1.1), true, "socket");
                p.Box(2.0, -0.9, 9, 0.9, KS.Body, KinTags.Link);
                p.Round(0, 0, 3.4, KS.Body, KinTags.Joint);
                p.Round(0, 0, 0.5, KS.Black);
            }))
        {
            AnchorsFn = (p, o) => new[] { PortAt(p.V(0, 0), "c") },
        };

        private static KinType GearContact() => new("kin.joint.gear", new I18nText("Gear contact (gear pair)", "Zahneingriff (Zahnradpaar)"), KinCategories.Joints, new Size2(36, 24),
            new[] { "gear pair", "gear contact", "mesh", "rolling contact", "zahneingriff", "zahnradpaar", "wälzpaar", "zahnrad" },
            ViewSpec.Schematic((p, o, c) => DrawGears(p, false)),
            ViewSpec.Detailed((p, o, c) => DrawGears(p, true)))
        {
            AnchorsFn = (p, o) => new[]
            {
                PortAt(p.V(-6.5, 0), "c1"), PortAt(p.V(10.5, 0), "c2"),
                Anchor.Point(AnchorRole.Hub, p.V(-6.5, 0), "hub1"), Anchor.Point(AnchorRole.Hub, p.V(10.5, 0), "hub2"),
                Anchor.Circle(AnchorRole.Pitch, p.V(-6.5, 0), 10.5 * Math.Sqrt(p.Sx * p.Sy)),
                Anchor.Circle(AnchorRole.Pitch, p.V(10.5, 0), 6.5 * Math.Sqrt(p.Sx * p.Sy)),
            },
        };

        private static void DrawGears(Pen p, bool detailed)
        {
            // Two pitch circles touching at x = 4 (radii 10.5 and 6.5).
            p.Ellipse(-6.5, 0, 10.5, 10.5, KS.Link, "gear");
            p.Ellipse(10.5, 0, 6.5, 6.5, KS.Link, "gear");
            if (detailed)
            {
                foreach (var (cx, r, from, to) in new[] { (-6.5, 10.5, -30.0, 30.0), (10.5, 6.5, 145.0, 215.0) })
                {
                    for (var a = from; a <= to + 1e-9; a += (to - from) / 6)
                    {
                        var d = Linkages.Dir(a);
                        p.PolyD(new[] { new Vec2(cx, 0) + d * (r - 1.2), new Vec2(cx, 0) + d * (r + 1.2) }, KS.Thin, false, "tooth");
                    }
                    p.Ellipse(cx, 0, r - 1.2, r - 1.2, KS.Thin, "root");
                }
                p.Line(-6.5, -11.5, -6.5, 11.5, KS.Ctr, "center");
                p.Line(10.5, -7.5, 10.5, 7.5, KS.Ctr, "center");
                p.Line(-17.5, 0, 17.5, 0, KS.Ctr, "center");
            }
            p.Joint(-6.5, 0, 1.3);
            p.Joint(10.5, 0, 1.3);
        }

        // ------------------------------------------------------------------------------------------ links & bodies

        private static KinType Crank() => new("kin.link.crank", new I18nText("Crank", "Kurbel"), KinCategories.Links, new Size2(32, 15),
            new[] { "crank", "driving crank", "input link", "kurbel", "antriebskurbel", "antrieb" },
            ViewSpec.Schematic((p, o, c) => DrawCrank(p, false)),
            ViewSpec.Detailed((p, o, c) => DrawCrank(p, true)))
        {
            AnchorsFn = (p, o) => new[]
            {
                PortAt(p.V(-10, -2), "pivot"), PortAt(p.V(11, -2), "pin"),
                MountAt(p.V(-10, -2) + new Vec2(0, 3 * PivotR * p.K), "base"),
            },
        };

        private static void DrawCrank(Pen p, bool detailed)
        {
            var c = p.V(-10, -2); var pin = p.V(11, -2);
            var r = PivotR * p.K;
            Frag.GroundPivot(p.Items, c, r, Down, detailed);
            if (detailed) p.Add(Frag.Capsule(c, pin, 1.6 * p.K, KS.Body), KinTags.Link);
            else p.LineP(c, pin, KS.Link, KinTags.Link);
            p.Add(Prim.Circle(c.X, c.Y, detailed ? 0.8 * r : r, KS.Joint), KinTags.Joint);
            p.Add(Prim.Circle(pin.X, pin.Y, detailed ? 0.8 * r : r, KS.Joint), KinTags.Joint);
            // Drive arrow around the pivot (clockwise on screen).
            var pts = Geo.ArcPoints(c, 4.6 * p.K, 4.6 * p.K, Geo.DegToRad(150), Geo.DegToRad(100), 16);
            p.Add(Prim.Polyline(pts, KS.Arrow), "drive");
        }

        private static KinType Rocker() => new("kin.link.rocker", new I18nText("Rocker", "Schwinge"), KinCategories.Links, new Size2(24, 30),
            new[] { "rocker", "lever", "oscillating link", "swing arm", "schwinge", "hebel", "schwinghebel" },
            ViewSpec.Schematic((p, o, c) => DrawRocker(p, false)),
            ViewSpec.Detailed((p, o, c) => DrawRocker(p, true)))
        {
            AnchorsFn = (p, o) => new[]
            {
                PortAt(p.V(0, 5.5), "pivot"), PortAt(p.V(0, -9.5), "pin"),
                MountAt(p.V(0, 5.5) + new Vec2(0, 3 * PivotR * p.K), "base"),
            },
        };

        private static void DrawRocker(Pen p, bool detailed)
        {
            var c = p.V(0, 5.5); var pin = p.V(0, -9.5);
            var r = PivotR * p.K;
            Frag.GroundPivot(p.Items, c, r, Down, detailed);
            if (detailed) p.Add(Frag.Capsule(c, pin, 1.6 * p.K, KS.Body), KinTags.Link);
            else p.LineP(c, pin, KS.Link, KinTags.Link);
            p.Add(Prim.Circle(c.X, c.Y, detailed ? 0.8 * r : r, KS.Joint), KinTags.Joint);
            p.Add(Prim.Circle(pin.X, pin.Y, detailed ? 0.8 * r : r, KS.Joint), KinTags.Joint);
            // Oscillation: double arrow arc around the pivot.
            var arc = Geo.ArcPoints(p.V(0, 5.5), 12 * p.Sx, 12 * p.Sy, Geo.DegToRad(-118), Geo.DegToRad(56), 16);
            p.Add(Prim.Polyline(arc, KS.DoubleArrow), "swing");
        }

        private static KinType SliderBlock() => new("kin.link.slider", new I18nText("Slider block", "Gleitstein"), KinCategories.Links, new Size2(14, 9),
            new[] { "slider", "slider block", "sliding block", "crosshead", "gleitstein", "kulissenstein", "schieber", "kreuzkopf" },
            ViewSpec.Schematic((p, o, c) => { p.Box(-6.5, -4, 6.5, 4, KS.Body, KinTags.Slider); p.Joint(0, 0, 1.4); }),
            ViewSpec.Detailed((p, o, c) => { p.Box(-6.5, -4, 6.5, 4, KS.HatchWhite(1.2), KinTags.Slider); p.Joint(0, 0, 1.6); p.Round(0, 0, 0.45, KS.Black); }))
        {
            AnchorsFn = (p, o) => new[] { PortAt(p.V(0, 0), "c"), Anchor.Point(AnchorRole.Hub, p.V(0, 0), "hub") },
        };

        private static double Stroke(SketchObject o) => Geo.Clamp(o.Num("stroke", 0.45), 0, 1);

        private static KinType Piston() => new("kin.link.piston", new I18nText("Piston and cylinder", "Kolben und Zylinder"), KinCategories.Links, new Size2(44, 14),
            new[] { "piston", "cylinder", "actuator", "hydraulic", "pneumatic", "kolben", "zylinder", "stellzylinder", "hydraulikzylinder", "pneumatikzylinder" },
            ViewSpec.Schematic((p, o, c) => DrawPiston(p, o, false)),
            ViewSpec.Detailed((p, o, c) => DrawPiston(p, o, true)))
        {
            Params = new[] { KP.Num("stroke", "Piston position (0 = retracted, 1 = extended)", "Kolbenstellung (0 = eingefahren, 1 = ausgefahren)", 0.45, 0, 1, null, 0.05) },
            AnchorsFn = (p, o) => new[]
            {
                PortAt(p.V(-20, 0), "base"), MountAt(p.V(-20, 0), "mount"),
                PortAt(p.V(PistonX(o) + 20, 0), "rod"),
                Anchor.Segment(AnchorRole.Axis, p.V(-22, 0), p.V(22, 0)) with { Id = "axis" },
            },
            Extra = o => new[]
            {
                new ContextAction("kin.stroke", new I18nText("Extend / retract", "Ausfahren / einfahren"), IconIds.Toggle,
                    x => x.WithParam("stroke", Stroke(x) > 0.5 ? 0.1 : 0.9)) { IsActive = Stroke(o) > 0.5, Group = "param" },
            },
        };

        private static double PistonX(SketchObject o) => -14 + 12 * Stroke(o);

        private static void DrawPiston(Pen p, SketchObject o, bool detailed)
        {
            var px = PistonX(o);
            var rodEnd = px + 20;
            p.Line(-18.5, 0, -17, 0, KS.Link);
            if (detailed)
            {
                // Walls as hatched sections (thickness 1), piston and rod as bodies.
                p.Box(-17, -5, 8, -4, KS.Hatch(0.9), "wall");
                p.Box(-17, 4, 8, 5, KS.Hatch(0.9), "wall");
                p.Box(-17, -4, -16, 4, KS.Hatch(0.9), "wall");
                p.Box(7, -4, 8, -1, KS.Hatch(0.9), "wall");
                p.Box(7, 1, 8, 4, KS.Hatch(0.9), "wall");
                p.Box(px + 1.2, -0.9, rodEnd - 1.4, 0.9, KS.Body, "rod");
                p.Box(px - 1.2, -4, px + 1.2, 4, KS.HatchWhite(0.9, 135), "piston");
            }
            else
            {
                p.Poly(KS.Link, false, "cylinder", 8, -1.2, 8, -5, -17, -5, -17, 5, 8, 5, 8, 1.2);
                p.Line(px, 0, rodEnd - 1.4, 0, KS.Link, "rod");
                p.Box(px - 1.0, -4.2, px + 1.0, 4.2, KS.Body, "piston");
            }
            p.Joint(-20, 0, 1.5);
            p.Joint(rodEnd, 0, 1.5);
        }

        private static KinType RopePulley() => new("kin.rope.pulley", new I18nText("Rope over pulley", "Seil über Rolle"), KinCategories.Links, new Size2(26, 40),
            new[] { "rope", "belt", "pulley", "sheave", "cable", "hoist", "seil", "riemen", "rolle", "seilrolle", "umlenkrolle", "flaschenzug" },
            ViewSpec.Schematic((p, o, c) => DrawPulley(p, o, false)),
            ViewSpec.Detailed((p, o, c) => DrawPulley(p, o, true)))
        {
            Params = new[] { KP.Bool("load", "Show load", "Last zeigen", true) },
            AnchorsFn = (p, o) => new[]
            {
                Anchor.Point(AnchorRole.Hub, p.V(0, -8), "hub"),
                PortAt(p.V(0, -8), "c"),
                PortAt(p.V(-7, 18), "rope1"),
                PortAt(p.V(7, o.Flag("load", true) ? 16 : 18), "rope2"),
                MountAt(p.V(0, -17), "ceiling"),
            },
            Extra = o => new[] { KA.Toggle(o, "kin.load", new I18nText("Load", "Last"), IconIds.Toggle, "load", true, "param") },
        };

        private static void DrawPulley(Pen p, SketchObject o, bool detailed)
        {
            // Ceiling and hanger.
            p.GroundLine(-7, 7, -17, 2.2, below: false);
            p.Line(0, -17, 0, -8, KS.Link, "hanger");
            // Rope: tangent strands on both sides, wrapped over the top half.
            var load = o.Flag("load", true);
            p.Arc(0, -8, 7, 7, 180, 180, KS.Link, "rope");
            p.Line(-7, -8, -7, 18, KS.Link, "rope");
            p.Line(7, -8, 7, load ? 9 : 18, KS.Link, "rope");
            p.Ellipse(0, -8, 7, 7, detailed ? KS.Body : KS.Link, "pulley");
            if (detailed)
            {
                p.Ellipse(0, -8, 5.6, 5.6, KS.Thin, "groove");
                p.Round(0, -8, 2.6, KS.Thin, "hub");
                p.Arc(0, -8, 7, 7, 180, 180, KS.Link, "rope");
            }
            p.Joint(0, -8, 1.3);
            if (load) p.Box(3, 9, 11, 16, detailed ? KS.HatchWhite(1.2) : KS.Body, "load");
        }

        private static double Opening(SketchObject o) => Geo.Clamp(o.Num("open", 0.6), 0, 1);

        private static KinType Gripper() => new("kin.gripper", new I18nText("Gripper jaws", "Greiferbacken"), KinCategories.Links, new Size2(30, 26),
            new[] { "gripper", "jaw", "end effector", "parallel gripper", "robot", "greifer", "greiferbacke", "backe", "parallelgreifer", "roboter" },
            ViewSpec.Schematic((p, o, c) => DrawGripper(p, o, false)),
            ViewSpec.Detailed((p, o, c) => DrawGripper(p, o, true)))
        {
            Params = new[] { KP.Num("open", "Opening (0 = closed, 1 = open)", "Öffnung (0 = geschlossen, 1 = offen)", 0.6, 0, 1, null, 0.1) },
            AnchorsFn = (p, o) => new[] { MountAt(p.V(0, -13), "flange"), PortAt(p.V(0, -13), "flange"), PortAt(p.V(0, 10), "grip") },
            Extra = o => new[]
            {
                new ContextAction("kin.open", new I18nText("Open / close jaws", "Backen öffnen / schließen"), IconIds.Toggle,
                    x => x.WithParam("open", Opening(x) > 0.3 ? 0.1 : 0.6)) { IsActive = Opening(o) > 0.3, Group = "param" },
            },
        };

        private static void DrawGripper(Pen p, SketchObject o, bool detailed)
        {
            var g = 3.5 + 7.5 * Opening(o);
            // Base body with the jaw guide (prismatic pairs).
            p.Box(-13, -13, 13, -6, detailed ? KS.HatchWhite(1.3) : KS.Body, "base");
            p.Line(-11.5, -8, 11.5, -8, KS.Thin, KinTags.Guide);
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                var x = sgn * g;
                if (detailed)
                {
                    p.Box(x - 1.2, -9.5, x + 1.2, 11, KS.Body, "jaw");
                    p.Box(x - sgn * 1.2, 7, x - sgn * 2.4, 11, KS.Hatch(0.7), "pad");
                }
                else
                {
                    p.Box(x - 1.6, -9.5, x + 1.6, -6.5, KS.Body, KinTags.Slider);
                    p.Poly(KS.Link, false, "jaw", x, -6.5, x, 11, x - sgn * 2.5, 11);
                }
            }
            if (g > 6) p.Line(-g + 3, 12.4, g - 3, 12.4, KS.DoubleArrow, "grip");
        }
    }
}
