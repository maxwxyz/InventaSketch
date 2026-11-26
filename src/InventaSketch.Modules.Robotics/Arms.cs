using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>Joint labels: J1 … in English, A1 … (Achse) in German.</summary>
    internal static class JL
    {
        public static JointDef R(int n, string en, string de, double def, double min, double max)
            => new("j" + n, new I18nText($"Joint {n} ({en})", $"Achse {n} ({de})"), def, min, max) { Short = new I18nText("J" + n, "A" + n) };
    }

    /// <summary>
    /// Six-axis articulated arm (industrial robot) with a spherical wrist. Zero pose: upper arm vertical, forearm horizontal
    /// pointing forward (+x). j1 turns about the vertical axis (clockwise in the top view), j2, j3 and j5 bend in the arm
    /// plane (clockwise in the side view at j1 = 0), j4 and j6 roll about the forearm and the tool axis (right-handed about the
    /// direction away from the robot, i.e. clockwise when looking from the robot towards the tool).
    /// </summary>
    internal sealed class ArticulatedArmType : RobotArmType
    {
        public const string TypeId = "robot.arm.articulated";

        public ArticulatedArmType() : base(TypeId, new I18nText("Articulated robot (6-axis)", "Knickarmroboter (6-achsig)"),
            45, false,
            "robot", "industrial robot", "articulated", "6-axis", "six axis", "arm", "roboter", "industrieroboter", "knickarm", "knickarmroboter", "6-achs", "sechsachs", "gelenkarm")
        { }

        private static readonly IReadOnlyList<JointDef> JointList = new[]
        {
            JL.R(1, "base", "Grundachse", 0, -180, 180),
            JL.R(2, "shoulder", "Schulter", 15, -180, 180),
            JL.R(3, "elbow", "Ellbogen", -25, -180, 180),
            JL.R(4, "forearm roll", "Unterarm drehen", 0, -180, 180),
            JL.R(5, "wrist bend", "Hand schwenken", 70, -125, 125),
            JL.R(6, "flange roll", "Flansch drehen", 0, -360, 360),
        };

        public override IReadOnlyList<JointDef> Joints => JointList;
        protected override string DefaultMotion => "j5";

        protected override IReadOnlyList<ParamDef> GeometryParams => GeometryDefs;

        internal static readonly IReadOnlyList<ParamDef> GeometryDefs = new[]
        {
            Pm.Num("base", "Shoulder height", "Schulterhöhe", 500, 50, 5000, "mm", 10),
            Pm.Num("offset", "Shoulder offset", "Schulterversatz", 150, 0, 2000, "mm", 10),
            Pm.Num("upper", "Upper arm length", "Oberarmlänge", 650, 50, 5000, "mm", 10),
            Pm.Num("fore", "Forearm length (elbow to wrist)", "Unterarmlänge (Ellbogen bis Hand)", 700, 50, 5000, "mm", 10),
            Pm.Num("wrist", "Wrist to flange", "Hand bis Flansch", 120, 10, 1000, "mm", 5),
        };

        internal static readonly IReadOnlyList<ParamDef> Limits = new[]
        {
            Pm.Num("j1lim", "Base rotation ± (J1 limit)", "Drehbereich ± (Grenze A1)", 170, 10, 180, "°", 5, advanced: true),
            Pm.Num("j2min", "Shoulder limit min (J2)", "Schulter min (A2)", -70, -180, 180, "°", 5, advanced: true),
            Pm.Num("j2max", "Shoulder limit max (J2)", "Schulter max (A2)", 110, -180, 180, "°", 5, advanced: true),
            Pm.Num("j3min", "Elbow limit min (J3)", "Ellbogen min (A3)", -80, -180, 180, "°", 5, advanced: true),
            Pm.Num("j3max", "Elbow limit max (J3)", "Ellbogen max (A3)", 70, -180, 180, "°", 5, advanced: true),
        };

        protected override IReadOnlyList<ParamDef> LimitParams => Limits;

        public override (double Min, double Max) Limit(SketchObject o, JointDef j) => j.Key switch
        {
            "j1" => (-Lim1(o), Lim1(o)),
            "j2" => Order(o.Num("j2min", -70), o.Num("j2max", 110)),
            "j3" => Order(o.Num("j3min", -80), o.Num("j3max", 70)),
            _ => (j.Min, j.Max),
        };

        private static double Lim1(SketchObject o) => Geo.Clamp(o.Num("j1lim", 170), 0, 180);
        internal static (double, double) Order(double a, double b) => a <= b ? (a, b) : (b, a);

        /// <summary>Geometry of one object (model mm).</summary>
        internal readonly struct Geo6
        {
            public Geo6(SketchObject o)
            {
                D1 = Len(o, "base", 500, 10); A1 = Geo.Clamp(o.Num("offset", 150), 0, 1e5); A2 = Len(o, "upper", 650, 10);
                D4 = Len(o, "fore", 700, 10); D6 = Len(o, "wrist", 120, 1);
                C3 = 0.15 * A2;
                U = A2 / 650;
            }
            public double D1 { get; }
            public double A1 { get; }
            public double A2 { get; }
            public double D4 { get; }
            public double D6 { get; }
            /// <summary>Elbow offset: the forearm axis lies this far above the elbow axis (fixed at 15 % of the upper arm).</summary>
            public double C3 { get; }
            /// <summary>Body size unit (bodies scale with the upper arm).</summary>
            public double U { get; }
            /// <summary>Effective forearm (elbow axis to wrist centre).</summary>
            public double L3 => Math.Sqrt(D4 * D4 + C3 * C3);
            /// <summary>Elbow angle φ (from stretched) at j3 = 0: 90° − atan2(c3, d4).</summary>
            public double Phi0 => 90 - Geo.RadToDeg(Math.Atan2(C3, D4));
            public double Foot => Math.Min(60 * U, 0.3 * D1);
        }

        public override ArmPose Pose(SketchObject o, double[] q)
        {
            var g = new Geo6(o);
            var u = g.U;
            var p = new ArmPose();
            var f0 = Frame3.Identity.Turn(new V3(0, 0, -1), q[0]);
            var s = f0.At(g.A1, 0, g.D1);
            p.Bodies.Add(Body3.Cylinder(V3.Zero, new V3(0, 0, g.Foot), 240 * u, 240 * u, BodyKind.Dark, RoboTags.Base));
            p.Bodies.Add(Body3.Cylinder(new V3(0, 0, g.Foot), new V3(0, 0, Math.Max(g.Foot + 1, g.D1 - 150 * u)), 195 * u, 170 * u, BodyKind.Link, RoboTags.Base));
            p.Bodies.Add(Body3.Box(f0, new V3(g.A1 * 0.6, 0, g.D1 - 120 * u), new V3(g.A1 * 0.6 + 120 * u, 140 * u, Math.Min(120 * u, g.D1 / 3)), BodyKind.Link, RoboTags.Base));
            p.Bodies.Add(Body3.Housing(s, f0.Dir(V3.UnitY), 125 * u, 300 * u));
            var f2 = new Frame3(f0.R, s).Turn(V3.UnitY, q[1]);
            var e = f2.At(0, 0, g.A2);
            p.Bodies.Add(Body3.Capsule(s, e, 95 * u));
            p.Bodies.Add(Body3.Housing(e, f2.Dir(V3.UnitY), 110 * u, 250 * u));
            var f3 = new Frame3(f2.R, e).Turn(V3.UnitY, q[2]);
            var w = f3.At(g.D4, 0, g.C3);
            p.Bodies.Add(Body3.Cylinder(f3.At(-170 * u, 0, g.C3), w, 92 * u, 68 * u, BodyKind.Link, RoboTags.Link));
            p.Bodies.Add(Body3.Cylinder(f3.At(-260 * u, 0, g.C3), f3.At(-150 * u, 0, g.C3), 82 * u, 82 * u, BodyKind.Dark, RoboTags.Link));
            var f4 = new Frame3(f3.R, w).Turn(V3.UnitX, q[3]);
            p.Bodies.Add(Body3.Housing(w, f4.Dir(V3.UnitY), 70 * u, 165 * u));
            var f5 = f4.Turn(V3.UnitY, q[4]);
            var tool = f5.Dir(V3.UnitX);
            var flange = f5.At(g.D6, 0, 0);
            var fr = 60 * u;
            var plate = Math.Min(20 * u, g.D6 * 0.3);
            p.Bodies.Add(Body3.Cylinder(w, flange - tool * plate, 52 * u, 46 * u, BodyKind.Link, RoboTags.Link));
            p.Bodies.Add(Body3.Cylinder(flange - tool * plate, flange, fr, fr, BodyKind.White, RoboTags.Flange));
            var f6 = f5.Turn(V3.UnitX, q[5]);
            p.FlangeMark = f6.At(g.D6, 0, fr * 0.7);
            p.FlangeRadius = fr;

            p.Joints.Add(new SymJoint(new V3(0, 0, (g.Foot + g.D1) * 0.45), new V3(0, 0, -1), JointKind.Revolute, "j1"));
            p.Joints.Add(new SymJoint(s, f0.Dir(V3.UnitY), JointKind.Revolute, "j2"));
            p.Joints.Add(new SymJoint(e, f2.Dir(V3.UnitY), JointKind.Revolute, "j3"));
            p.Joints.Add(new SymJoint(f3.At(g.D4 * 0.5, 0, g.C3), f3.Dir(V3.UnitX), JointKind.Revolute, "j4"));
            p.Joints.Add(new SymJoint(w, f4.Dir(V3.UnitY), JointKind.Revolute, "j5"));
            p.Joints.Add(new SymJoint(f5.At(g.D6 * 0.55, 0, 0), tool, JointKind.Revolute, "j6"));
            p.Sticks.Add(new[] { V3.Zero, new V3(0, 0, g.D1), s, e, f3.At(0, 0, g.C3), w, flange });
            p.Base = V3.Zero;
            p.Flange = flange;
            p.ToolDir = tool;
            return p;
        }

        /// <summary>Shoulder point in the side view (arm plane at j1 = 0).</summary>
        private static Vec2 Shoulder(Geo6 g) => new(g.A1, -g.D1);

        /// <summary>
        /// Working range of the wrist centre in the arm plane (side view, j1 = 0): two-link chain with L1 = upper arm,
        /// L2 = √(d4² + c3²), θ = −90° + j2 (screen angle of the upper arm), φ = j3 + 90° − atan2(c3, d4).
        /// </summary>
        internal static Area2 SideRegion(SketchObject o)
        {
            var g = new Geo6(o);
            var (j2a, j2b) = Order(o.Num("j2min", -70), o.Num("j2max", 110));
            var (j3a, j3b) = Order(o.Num("j3min", -80), o.Num("j3max", 70));
            return Reach.TwoLink(Shoulder(g), g.A2, g.L3, -90 + j2a, -90 + j2b, g.Phi0 + j3a, g.Phi0 + j3b);
        }

        /// <summary>Top view: annulus or sector R_in…R_out over the J1 range, from the horizontal extent of the side region.</summary>
        internal static Area2 TopRegion(SketchObject o)
        {
            var side = SideRegion(o);
            var (rin, rout) = HorizontalRange(side);
            var lim = Lim1(o);
            return Reach.Sector(Vec2.Zero, rin, rout, -lim, lim);
        }

        /// <summary>Minimum and maximum horizontal distance of an arm-plane region from the vertical axis (x = 0).</summary>
        internal static (double In, double Out) HorizontalRange(Area2 side)
        {
            var xs = side.Fill.Select(p => p.X).ToList();
            if (xs.Count == 0) return (0, 0);
            var rout = xs.Max(Math.Abs);
            var rin = xs.Min() <= 0 && xs.Max() >= 0 ? 0 : xs.Min(Math.Abs);
            return (rin, rout);
        }

        /// <summary>The envelope describes the wrist centre: the flange is d6 further.</summary>
        protected override double EnvelopeToFlange(SketchObject o) => new Geo6(o).D6;

        public override Area2? Envelope(SketchObject o, string view, double[] q)
        {
            if (view == RoboViews.Top) return TopRegion(o);
            var side = SideRegion(o);
            return Math.Cos(Geo.DegToRad(q[0])) < -1e-9 ? side.Map(p => new Vec2(-p.X, p.Y)) : side;
        }
    }

    /// <summary>
    /// Collaborative robot (cobot): six revolute joints with rounded joint housings and offset links. Zero pose: upper arm
    /// vertical, forearm horizontal forward, wrist link along the forearm. j1 about the vertical axis (clockwise in the top
    /// view), j2–j4 bend in the arm plane (clockwise in the side view at j1 = 0), j5 turns about the wrist's second axis,
    /// j6 rolls the flange.
    /// </summary>
    internal sealed class CobotType : RobotArmType
    {
        public CobotType() : base("robot.arm.cobot", new I18nText("Collaborative robot (cobot)", "Kollaborativer Roboter (Cobot)"),
            42, false,
            "cobot", "collaborative", "human robot collaboration", "hrc", "arm", "kollaborativ", "mensch-roboter-kollaboration", "mrk", "leichtbauroboter", "roboter")
        { }

        private static readonly IReadOnlyList<JointDef> JointList = new[]
        {
            JL.R(1, "base", "Basis", 0, -360, 360),
            JL.R(2, "shoulder", "Schulter", 20, -180, 180),
            JL.R(3, "elbow", "Ellbogen", -10, -180, 180),
            JL.R(4, "wrist 1", "Handgelenk 1", 80, -360, 360),
            JL.R(5, "wrist 2", "Handgelenk 2", 0, -360, 360),
            JL.R(6, "wrist 3 (flange)", "Handgelenk 3 (Flansch)", 0, -360, 360),
        };

        public override IReadOnlyList<JointDef> Joints => JointList;
        protected override string DefaultMotion => "j4";

        protected override IReadOnlyList<ParamDef> GeometryParams => new[]
        {
            Pm.Num("base", "Shoulder height", "Schulterhöhe", 180, 20, 3000, "mm", 10),
            Pm.Num("upper", "Upper arm length", "Oberarmlänge", 425, 50, 3000, "mm", 5),
            Pm.Num("fore", "Forearm length", "Unterarmlänge", 390, 50, 3000, "mm", 5),
            Pm.Num("wrist", "Wrist link (J4 to J5)", "Handglied (A4 bis A5)", 110, 10, 1000, "mm", 5),
            Pm.Num("flange", "J5 to flange", "A5 bis Flansch", 100, 10, 1000, "mm", 5),
        };

        protected override IReadOnlyList<ParamDef> LimitParams => new[]
        {
            Pm.Num("j1lim", "Base rotation ± (J1 limit)", "Drehbereich ± (Grenze A1)", 360, 10, 360, "°", 5, advanced: true),
            Pm.Num("j2min", "Shoulder limit min (J2)", "Schulter min (A2)", -110, -180, 180, "°", 5, advanced: true),
            Pm.Num("j2max", "Shoulder limit max (J2)", "Schulter max (A2)", 110, -180, 180, "°", 5, advanced: true),
            Pm.Num("j3min", "Elbow limit min (J3)", "Ellbogen min (A3)", -160, -180, 180, "°", 5, advanced: true),
            Pm.Num("j3max", "Elbow limit max (J3)", "Ellbogen max (A3)", 160, -180, 180, "°", 5, advanced: true),
        };

        public override (double Min, double Max) Limit(SketchObject o, JointDef j) => j.Key switch
        {
            "j1" => (-Geo.Clamp(o.Num("j1lim", 360), 0, 360), Geo.Clamp(o.Num("j1lim", 360), 0, 360)),
            "j2" => ArticulatedArmType.Order(o.Num("j2min", -110), o.Num("j2max", 110)),
            "j3" => ArticulatedArmType.Order(o.Num("j3min", -160), o.Num("j3max", 160)),
            _ => (j.Min, j.Max),
        };

        private readonly struct G
        {
            public G(SketchObject o)
            {
                D1 = Len(o, "base", 180, 10); A2 = Len(o, "upper", 425, 10); A3 = Len(o, "fore", 390, 10);
                D5 = Len(o, "wrist", 110, 1); D6 = Len(o, "flange", 100, 1); U = A2 / 425; Off = 0.32 * A2;
            }
            public double D1 { get; }
            public double A2 { get; }
            public double A3 { get; }
            public double D5 { get; }
            public double D6 { get; }
            public double U { get; }
            /// <summary>Lateral offset of the upper arm (housings side by side).</summary>
            public double Off { get; }
        }

        public override ArmPose Pose(SketchObject o, double[] q)
        {
            var g = new G(o);
            var u = g.U;
            var p = new ArmPose();
            var f0 = Frame3.Identity.Turn(new V3(0, 0, -1), q[0]);
            var s = new V3(0, 0, g.D1);
            var y0 = f0.Dir(V3.UnitY);
            p.Bodies.Add(Body3.Cylinder(V3.Zero, new V3(0, 0, Math.Min(40 * u, g.D1 * 0.3)), 82 * u, 82 * u, BodyKind.Dark, RoboTags.Base));
            p.Bodies.Add(Body3.Cylinder(new V3(0, 0, Math.Min(40 * u, g.D1 * 0.3)), s, 62 * u, 62 * u, BodyKind.Link, RoboTags.Base));
            p.Bodies.Add(Body3.Housing(s + y0 * (g.Off * 0.5), y0, 64 * u, g.Off + 128 * u));
            var f2 = new Frame3(f0.R, s).Turn(V3.UnitY, q[1]);
            var e = f2.At(0, 0, g.A2);
            p.Bodies.Add(Body3.Capsule(f2.At(0, g.Off, 0), f2.At(0, g.Off, g.A2), 50 * u));
            p.Bodies.Add(Body3.Housing(f2.At(0, g.Off * 0.5, g.A2), f2.Dir(V3.UnitY), 56 * u, g.Off + 112 * u));
            var f3 = new Frame3(f2.R, e).Turn(V3.UnitY, q[2]);
            var w1 = f3.At(g.A3, 0, 0);
            p.Bodies.Add(Body3.Capsule(e, w1, 42 * u));
            var f4 = new Frame3(f3.R, w1).Turn(V3.UnitY, q[3]);
            p.Bodies.Add(Body3.Housing(w1, f4.Dir(V3.UnitY), 46 * u, 110 * u));
            var w2 = f4.At(g.D5, 0, 0);
            p.Bodies.Add(Body3.Capsule(w1, w2, 40 * u));
            p.Bodies.Add(Body3.Housing(w2, f4.Dir(V3.UnitZ), 46 * u, 110 * u));
            var f5 = new Frame3(f4.R, w2).Turn(V3.UnitZ, q[4]);
            var tool = f5.Dir(V3.UnitX);
            var flange = f5.At(g.D6, 0, 0);
            var plate = Math.Min(16 * u, g.D6 * 0.3);
            p.Bodies.Add(Body3.Cylinder(w2, flange - tool * plate, 42 * u, 42 * u, BodyKind.Link, RoboTags.Joint, marker: true));
            p.Bodies.Add(Body3.Cylinder(flange - tool * plate, flange, 38 * u, 38 * u, BodyKind.White, RoboTags.Flange));
            p.FlangeMark = f5.Turn(V3.UnitX, q[5]).At(g.D6, 0, 27 * u);
            p.FlangeRadius = 38 * u;

            p.Joints.Add(new SymJoint(new V3(0, 0, g.D1 * 0.55), new V3(0, 0, -1), JointKind.Revolute, "j1"));
            p.Joints.Add(new SymJoint(s, y0, JointKind.Revolute, "j2"));
            p.Joints.Add(new SymJoint(e, f2.Dir(V3.UnitY), JointKind.Revolute, "j3"));
            p.Joints.Add(new SymJoint(w1, f4.Dir(V3.UnitY), JointKind.Revolute, "j4"));
            p.Joints.Add(new SymJoint(w2, f4.Dir(V3.UnitZ), JointKind.Revolute, "j5"));
            p.Joints.Add(new SymJoint(f5.At(g.D6 * 0.5, 0, 0), tool, JointKind.Revolute, "j6"));
            p.Sticks.Add(new[] { V3.Zero, s, e, w1, w2, flange });
            p.Base = V3.Zero;
            p.Flange = flange;
            p.ToolDir = tool;
            return p;
        }

        /// <summary>Working range of the wrist (J4 axis) in the arm plane: L1 = upper arm, L2 = forearm, θ = −90° + j2, φ = 90° + j3.</summary>
        private static Area2 SideRegion(SketchObject o)
        {
            var g = new G(o);
            var (a, b) = ArticulatedArmType.Order(o.Num("j2min", -110), o.Num("j2max", 110));
            var (c, d) = ArticulatedArmType.Order(o.Num("j3min", -160), o.Num("j3max", 160));
            return Reach.TwoLink(new Vec2(0, -g.D1), g.A2, g.A3, -90 + a, -90 + b, 90 + c, 90 + d);
        }

        /// <summary>The envelope describes the J4 axis: the flange is at most d5 + d6 further.</summary>
        protected override double EnvelopeToFlange(SketchObject o)
        {
            var g = new G(o);
            return g.D5 + g.D6;
        }

        public override Area2? Envelope(SketchObject o, string view, double[] q)
        {
            var side = SideRegion(o);
            if (view == RoboViews.Top)
            {
                var (rin, rout) = ArticulatedArmType.HorizontalRange(side);
                var lim = Geo.Clamp(o.Num("j1lim", 360), 0, 360);
                return Reach.Sector(Vec2.Zero, rin, rout, -lim, lim);
            }
            return Math.Cos(Geo.DegToRad(q[0])) < -1e-9 ? side.Map(p => new Vec2(-p.X, p.Y)) : side;
        }
    }

    /// <summary>
    /// SCARA robot: two horizontal arms turning about vertical axes (j1, j2, clockwise in the top view), a vertical quill
    /// stroke j3 (mm, positive downwards) and the tool rotation j4. Zero pose: both arms stretched along +x.
    /// </summary>
    internal sealed class ScaraType : RobotArmType
    {
        public ScaraType() : base("robot.arm.scara", new I18nText("SCARA robot", "SCARA-Roboter"),
            44, true,
            "scara", "horizontal articulated", "assembly robot", "pick and place", "roboter", "horizontal-knickarm", "montageroboter", "schwenkarm")
        { }

        private static readonly IReadOnlyList<JointDef> JointList = new[]
        {
            JL.R(1, "inner arm", "Innenarm", -30, -180, 180),
            JL.R(2, "outer arm", "Außenarm", 60, -180, 180),
            new JointDef("j3", new I18nText("Joint 3 (quill stroke, down)", "Achse 3 (Pinolenhub, nach unten)"), 80, 0, 2000, Prismatic: true) { Short = new I18nText("J3", "A3") },
            JL.R(4, "tool rotation", "Werkzeugdrehung", 0, -360, 360),
        };

        public override IReadOnlyList<JointDef> Joints => JointList;
        protected override string DefaultMotion => "j2";

        protected override IReadOnlyList<ParamDef> GeometryParams => new[]
        {
            Pm.Num("height", "Arm height", "Armhöhe", 400, 50, 3000, "mm", 10),
            Pm.Num("inner", "Inner arm length", "Innenarmlänge", 325, 20, 3000, "mm", 5),
            Pm.Num("outer", "Outer arm length", "Außenarmlänge", 275, 20, 3000, "mm", 5),
            Pm.Num("stroke", "Quill stroke", "Pinolenhub", 200, 10, 2000, "mm", 10),
        };

        protected override IReadOnlyList<ParamDef> LimitParams => new[]
        {
            Pm.Num("j1lim", "Inner arm ± (J1 limit)", "Innenarm ± (Grenze A1)", 132, 10, 180, "°", 1, advanced: true),
            Pm.Num("j2lim", "Outer arm ± (J2 limit)", "Außenarm ± (Grenze A2)", 145, 10, 180, "°", 1, advanced: true),
        };

        public override (double Min, double Max) Limit(SketchObject o, JointDef j) => j.Key switch
        {
            "j1" => (-Geo.Clamp(o.Num("j1lim", 132), 0, 180), Geo.Clamp(o.Num("j1lim", 132), 0, 180)),
            "j2" => (-Geo.Clamp(o.Num("j2lim", 145), 0, 180), Geo.Clamp(o.Num("j2lim", 145), 0, 180)),
            "j3" => (0, Len(o, "stroke", 200)),
            _ => (j.Min, j.Max),
        };

        private readonly struct G
        {
            public G(SketchObject o)
            {
                H = Len(o, "height", 400, 10); A1 = Len(o, "inner", 325, 5); A2 = Len(o, "outer", 275, 5); Stroke = Len(o, "stroke", 200); U = (A1 + A2) / 600;
            }
            public double H { get; }
            public double A1 { get; }
            public double A2 { get; }
            public double Stroke { get; }
            public double U { get; }
            /// <summary>Flange height at j3 = 0.</summary>
            public double Z0 => H - 130 * U;
        }

        public override ArmPose Pose(SketchObject o, double[] q)
        {
            var g = new G(o);
            var u = g.U;
            var p = new ArmPose();
            var down = new V3(0, 0, -1);
            var f0 = new Frame3(M3.Identity, new V3(0, 0, g.H)).Turn(down, q[0]);
            var p2 = f0.At(g.A1, 0, 0);
            var f1 = new Frame3(f0.R, p2).Turn(down, q[1]);
            var p3 = f1.At(g.A2, 0, 0);
            var zf = g.Z0 - q[2];
            p.Bodies.Add(Body3.Box(Frame3.Identity, new V3(-70 * u, 0, (g.H - 50 * u) / 2), new V3(150 * u, 120 * u, (g.H - 50 * u) / 2), BodyKind.Dark, RoboTags.Base));
            p.Bodies.Add(Body3.Cylinder(new V3(0, 0, g.H - 60 * u), new V3(0, 0, g.H), 105 * u, 105 * u, BodyKind.Link, RoboTags.Base));
            p.Bodies.Add(Body3.Slab(f0.P, p2, V3.UnitZ, 95 * u, 90 * u, BodyKind.Link, RoboTags.Link));
            p.Bodies.Add(Body3.Cylinder(p2 + new V3(0, 0, 40 * u), p2 + new V3(0, 0, 60 * u), 70 * u, 70 * u, BodyKind.Dark, RoboTags.Joint));
            p.Bodies.Add(Body3.Slab(p2 + new V3(0, 0, 100 * u), p3 + new V3(0, 0, 100 * u), V3.UnitZ, 80 * u, 80 * u, BodyKind.Link, RoboTags.Link));
            p.Bodies.Add(Body3.Cylinder(p3 + new V3(0, 0, 140 * u), p3 + new V3(0, 0, 250 * u), 52 * u, 52 * u, BodyKind.Link, RoboTags.Joint));
            var top = zf + 20 * u + g.Stroke + 380 * u;
            p.Bodies.Add(Body3.Cylinder(new V3(p3.X, p3.Y, zf + 20 * u), new V3(p3.X, p3.Y, top), 20 * u, 20 * u, BodyKind.White, RoboTags.Link));
            p.Bodies.Add(Body3.Cylinder(new V3(p3.X, p3.Y, zf + 20 * u), new V3(p3.X, p3.Y, zf), 40 * u, 40 * u, BodyKind.White, RoboTags.Flange));
            var f4 = new Frame3(f1.R, new V3(p3.X, p3.Y, zf)).Turn(down, q[3]);
            p.FlangeMark = f4.At(28 * u, 0, 0);
            p.FlangeRadius = 40 * u;
            var flange = new V3(p3.X, p3.Y, zf);

            p.Joints.Add(new SymJoint(new V3(0, 0, g.H), V3.UnitZ, JointKind.Revolute, "j1"));
            p.Joints.Add(new SymJoint(p2 + new V3(0, 0, 50 * u), V3.UnitZ, JointKind.Revolute, "j2"));
            p.Joints.Add(new SymJoint(p3 + new V3(0, 0, 100 * u), V3.UnitZ, JointKind.Prismatic, "j3"));
            p.Joints.Add(new SymJoint(flange + new V3(0, 0, 60 * u), V3.UnitZ, JointKind.Revolute, "j4"));
            p.Sticks.Add(new[] { V3.Zero, new V3(0, 0, g.H), p2 + new V3(0, 0, 50 * u), p3 + new V3(0, 0, 100 * u), flange });
            p.Base = V3.Zero;
            p.Flange = flange;
            p.ToolDir = down;
            return p;
        }

        /// <summary>Top view working range of the quill axis: two-link chain L1 = inner, L2 = outer arm, θ = j1, φ = j2.</summary>
        private static Area2 TopRegion(SketchObject o)
        {
            var g = new G(o);
            var l1 = Geo.Clamp(o.Num("j1lim", 132), 0, 180);
            var l2 = Geo.Clamp(o.Num("j2lim", 145), 0, 180);
            return Reach.TwoLink(Vec2.Zero, g.A1, g.A2, -l1, l1, -l2, l2);
        }

        public override Area2? Envelope(SketchObject o, string view, double[] q)
        {
            if (view == RoboViews.Top) return TopRegion(o);
            var g = new G(o);
            var l2 = Geo.Clamp(o.Num("j2lim", 145), 0, 180);
            var (rin, rout) = Reach.RhoRange(g.A1, g.A2, -l2, l2);
            var flangeX = Pose(o, q).Flange.X;
            var sign = flangeX < -1e-9 ? -1 : 1;
            var pts = new[] { new Vec2(sign * rin, -g.Z0), new Vec2(sign * rout, -g.Z0), new Vec2(sign * rout, -(g.Z0 - g.Stroke)), new Vec2(sign * rin, -(g.Z0 - g.Stroke)) };
            return Area2.Polygon(pts);
        }
    }

    /// <summary>
    /// Delta (parallel) robot: three upper arms driven at 120° about the base (j1–j3, positive swings the arm down),
    /// parallelogram forearms to the moving platform. The platform position is found by intersecting three spheres
    /// (forward kinematics of the delta robot); poses that cannot be assembled draw a note.
    /// </summary>
    internal sealed class DeltaType : RobotArmType
    {
        public DeltaType() : base("robot.arm.delta", new I18nText("Delta robot (parallel)", "Delta-Roboter (Parallelkinematik)"),
            40, false,
            "delta", "parallel", "parallel kinematics", "picker", "pick and place", "parallelkinematik", "deltaroboter", "picker", "roboter")
        { }

        private static readonly IReadOnlyList<JointDef> JointList = new[]
        {
            JL.R(1, "arm at 0°", "Arm bei 0°", 30, -40, 90),
            JL.R(2, "arm at 120°", "Arm bei 120°", 30, -40, 90),
            JL.R(3, "arm at 240°", "Arm bei 240°", 30, -40, 90),
        };

        public override IReadOnlyList<JointDef> Joints => JointList;
        protected override string DefaultMotion => "j1";

        protected override IReadOnlyList<ParamDef> GeometryParams => new[]
        {
            Pm.Num("height", "Base height", "Höhe der Grundplatte", 1000, 100, 5000, "mm", 10),
            Pm.Num("baseRadius", "Base radius (drive axes)", "Radius der Antriebe", 200, 20, 2000, "mm", 5),
            Pm.Num("upper", "Upper arm length", "Oberarmlänge", 300, 20, 3000, "mm", 5),
            Pm.Num("fore", "Forearm length", "Unterarmlänge", 750, 20, 5000, "mm", 5),
            Pm.Num("platform", "Platform radius", "Plattformradius", 60, 5, 1000, "mm", 5),
        };

        private readonly struct G
        {
            public G(SketchObject o)
            {
                H = Len(o, "height", 1000, 10); Rb = Len(o, "baseRadius", 200, 1); L = Len(o, "upper", 300, 5); Lf = Len(o, "fore", 750, 5);
                Rp = Len(o, "platform", 60, 1); U = L / 300;
            }
            public double H { get; }
            public double Rb { get; }
            public double L { get; }
            public double Lf { get; }
            public double Rp { get; }
            public double U { get; }
        }

        private static V3 Ui(int i) => new(Math.Cos(Geo.DegToRad(120 * i)), Math.Sin(Geo.DegToRad(120 * i)), 0);

        /// <summary>Platform centre for the arm angles (null if the forearms cannot be closed).</summary>
        internal static V3? Platform(double h, double rb, double l, double lf, double rp, double[] q, out V3[] elbows)
        {
            elbows = new V3[3];
            var c = new V3[3];
            for (var i = 0; i < 3; i++)
            {
                var ui = Ui(i);
                var t = Geo.DegToRad(q[i]);
                var b = ui * rb + new V3(0, 0, h);
                elbows[i] = b + ui * (l * Math.Cos(t)) + new V3(0, 0, -l * Math.Sin(t));
                c[i] = elbows[i] - ui * rp;
            }
            // Three spheres of radius lf around c0, c1, c2 (trilateration), lower solution.
            var d = c[1].DistanceTo(c[0]);
            if (d < 1e-9) return null;
            var ex = (c[1] - c[0]) * (1 / d);
            var iv = ex.Dot(c[2] - c[0]);
            var eyRaw = c[2] - c[0] - ex * iv;
            var jv = eyRaw.Length;
            if (jv < 1e-9) return null;
            var ey = eyRaw * (1 / jv);
            var ez = ex.Cross(ey);
            var x = d / 2;
            var y = (iv * iv + jv * jv) / (2 * jv) - iv / jv * x;
            var z2 = lf * lf - x * x - y * y;
            if (z2 < 0) return null;
            var z = Math.Sqrt(z2);
            var p1 = c[0] + ex * x + ey * y + ez * z;
            var p2 = c[0] + ex * x + ey * y - ez * z;
            return p1.Z < p2.Z ? p1 : p2;
        }

        private const double ToolLen = 60;

        public override ArmPose Pose(SketchObject o, double[] q)
        {
            var g = new G(o);
            var u = g.U;
            var p = new ArmPose { BaseUp = true };
            var plat = Platform(g.H, g.Rb, g.L, g.Lf, g.Rp, q, out var el);
            var plate = new List<V3>();
            for (var i = 0; i < 3; i++)
                foreach (var da in new[] { -40.0, 40.0 })
                {
                    var a = Geo.DegToRad(120 * i + da);
                    var rr = g.Rb + 90 * u;
                    plate.Add(new V3(rr * Math.Cos(a), rr * Math.Sin(a), g.H + 10 * u));
                    plate.Add(new V3(rr * Math.Cos(a), rr * Math.Sin(a), g.H + 60 * u));
                }
            p.Bodies.Add(Body3.Hull(plate, BodyKind.Dark, RoboTags.Base));
            var baseTri = new List<V3>();
            for (var i = 0; i < 3; i++)
            {
                var ui = Ui(i);
                var ti = new V3(0, 0, 1).Cross(ui);
                var b = ui * g.Rb + new V3(0, 0, g.H);
                baseTri.Add(b);
                p.Bodies.Add(Body3.Housing(b, ti, 50 * u, 130 * u, BodyKind.Dark));
                p.Bodies.Add(Body3.Cylinder(b + ti * (65 * u), b + ti * (170 * u), 42 * u, 42 * u, BodyKind.Dark, RoboTags.Base));
                p.Bodies.Add(Body3.Capsule(b, el[i], 26 * u));
                p.Joints.Add(new SymJoint(b, ti, JointKind.Revolute, "j" + (i + 1)));
                if (plat is V3 pc)
                {
                    var w = 42 * u;
                    var end = pc + ui * g.Rp;
                    p.Bodies.Add(Body3.Capsule(el[i] + ti * w, end + ti * w, 9 * u));
                    p.Bodies.Add(Body3.Capsule(el[i] - ti * w, end - ti * w, 9 * u));
                    p.Bodies.Add(Body3.Capsule(el[i] - ti * w, el[i] + ti * w, 14 * u, BodyKind.White, RoboTags.Joint));
                    p.Joints.Add(new SymJoint(el[i], ti, JointKind.Spherical, ""));
                    p.Sticks.Add(new[] { b, el[i], end });
                }
                else p.Sticks.Add(new[] { b, el[i] });
            }
            p.Sticks.Add(new[] { baseTri[0], baseTri[1], baseTri[2], baseTri[0] });
            p.Base = new V3(0, 0, g.H + 60 * u);
            if (plat is V3 c0)
            {
                p.Bodies.Add(Body3.Cylinder(c0 + new V3(0, 0, 15 * u), c0 - new V3(0, 0, 15 * u), g.Rp + 25 * u, g.Rp + 25 * u, BodyKind.Link, RoboTags.Flange));
                var flange = c0 - new V3(0, 0, ToolLen * u);
                p.Bodies.Add(Body3.Cylinder(c0 - new V3(0, 0, 15 * u), flange, 24 * u, 20 * u, BodyKind.White, RoboTags.Flange));
                p.Sticks.Add(new[] { c0 + Ui(0) * g.Rp, c0 + Ui(1) * g.Rp, c0 + Ui(2) * g.Rp, c0 + Ui(0) * g.Rp });
                p.Sticks.Add(new[] { c0, flange });
                p.Flange = flange;
                p.FlangeRadius = 20 * u;
            }
            else
            {
                p.Ok = false;
                p.Flange = new V3(0, 0, g.H - g.L);
            }
            p.ToolDir = new V3(0, 0, -1);
            return p;
        }

        /// <summary>Reachable flange positions sampled over the arm limits (n steps per arm).</summary>
        private static List<V3> Samples(SketchObject o, int n, out G g)
        {
            g = new G(o);
            var pts = new List<V3>();
            var lo = -40.0; var hi = 90.0;
            for (var a = 0; a < n; a++)
                for (var b = 0; b < n; b++)
                    for (var c = 0; c < n; c++)
                    {
                        var q = new[] { lo + (hi - lo) * a / (n - 1), lo + (hi - lo) * b / (n - 1), lo + (hi - lo) * c / (n - 1) };
                        if (Platform(g.H, g.Rb, g.L, g.Lf, g.Rp, q, out _) is V3 pc) pts.Add(pc - new V3(0, 0, ToolLen * g.U));
                    }
            return pts;
        }

        /// <summary>Envelope: convex hull of reachable flange positions sampled over the arm limits (an approximation).</summary>
        public override Area2? Envelope(SketchObject o, string view, double[] q)
        {
            var pts = Samples(o, 11, out _);
            if (pts.Count < 3) return null;
            return Area2.Polygon(Rb.Hull(pts.Select(p => Proj.To2(p, view))));
        }
    }

    /// <summary>
    /// Cartesian (gantry) robot: a bridge travelling along two y rails, a carriage travelling along the bridge (x) and a
    /// vertical ram (z, positive downwards). Joint values are the axis positions in mm from the start of each stroke.
    /// </summary>
    internal sealed class GantryType : RobotArmType
    {
        public GantryType() : base("robot.arm.gantry", new I18nText("Cartesian / gantry robot", "Linear- / Portalroboter"),
            50, false,
            "cartesian", "gantry", "linear robot", "xyz", "portal", "linearroboter", "portalroboter", "kartesisch", "flächenportal", "roboter")
        { }

        private static readonly IReadOnlyList<JointDef> JointList = new[]
        {
            new JointDef("x", new I18nText("X axis (carriage)", "X-Achse (Wagen)"), 600, 0, 20000, Prismatic: true) { Short = new I18nText("X") },
            new JointDef("y", new I18nText("Y axis (bridge)", "Y-Achse (Brücke)"), 400, 0, 20000, Prismatic: true) { Short = new I18nText("Y") },
            new JointDef("z", new I18nText("Z axis (ram, down)", "Z-Achse (Hub, nach unten)"), 200, 0, 20000, Prismatic: true) { Short = new I18nText("Z") },
        };

        public override IReadOnlyList<JointDef> Joints => JointList;
        protected override string DefaultMotion => "x";

        protected override IReadOnlyList<ParamDef> GeometryParams => new[]
        {
            Pm.Num("strokeX", "Stroke X", "Hub X", 1200, 50, 20000, "mm", 50),
            Pm.Num("strokeY", "Stroke Y", "Hub Y", 800, 50, 20000, "mm", 50),
            Pm.Num("strokeZ", "Stroke Z", "Hub Z", 500, 50, 10000, "mm", 50),
            Pm.Num("height", "Rail height", "Schienenhöhe", 1500, 300, 10000, "mm", 50),
        };

        public override (double Min, double Max) Limit(SketchObject o, JointDef j) => j.Key switch
        {
            "x" => (0, Len(o, "strokeX", 1200)),
            "y" => (0, Len(o, "strokeY", 800)),
            _ => (0, Len(o, "strokeZ", 500)),
        };

        private readonly struct G
        {
            public G(SketchObject o)
            {
                Sx = Len(o, "strokeX", 1200); Sy = Len(o, "strokeY", 800); Sz = Len(o, "strokeZ", 500);
                Hg = Math.Max(Len(o, "height", 1500), Sz + 400);
                U = Math.Max(0.4, Math.Min(Sx, Sy) / 800);
            }
            public double Sx { get; }
            public double Sy { get; }
            public double Sz { get; }
            public double Hg { get; }
            public double U { get; }
            public double RailX => Sx / 2 + 150 * U;
            public double RailY => Sy / 2 + 150 * U;
            /// <summary>Flange height at z = 0.</summary>
            public double Z0 => Hg - 260 * U;
            /// <summary>Ram offset in front of the bridge (−y).</summary>
            public double Front => 115 * U;
        }

        public override ArmPose Pose(SketchObject o, double[] q)
        {
            var g = new G(o);
            var u = g.U;
            var p = new ArmPose { Ground = false };
            var xw = -g.Sx / 2 + q[0];
            var yw = -g.Sy / 2 + q[1];
            var zf = g.Z0 - q[2];
            var id = Frame3.Identity;
            foreach (var sx in new[] { -1, 1 })
            {
                foreach (var sy in new[] { -1, 1 })
                {
                    p.Bodies.Add(Body3.Box(id, new V3(sx * g.RailX, sy * g.RailY, (g.Hg - 100 * u) / 2), new V3(40 * u, 40 * u, (g.Hg - 100 * u) / 2), BodyKind.Dark, RoboTags.Base));
                    p.Sticks.Add(new[] { new V3(sx * g.RailX, sy * g.RailY, 0), new V3(sx * g.RailX, sy * g.RailY, g.Hg - 60 * u) });
                }
                p.Bodies.Add(Body3.Box(id, new V3(sx * g.RailX, 0, g.Hg - 60 * u), new V3(45 * u, g.RailY + 45 * u, 40 * u), BodyKind.Link, RoboTags.Base));
                p.Guides.Add((new V3(sx * g.RailX, -g.RailY, g.Hg - 60 * u), new V3(sx * g.RailX, g.RailY, g.Hg - 60 * u)));
            }
            p.Bodies.Add(Body3.Box(id, new V3(0, yw, g.Hg + 40 * u), new V3(g.RailX + 50 * u, 60 * u, 60 * u), BodyKind.Link, RoboTags.Link));
            p.Guides.Add((new V3(-g.Sx / 2, yw - g.Front, g.Hg + 40 * u), new V3(g.Sx / 2, yw - g.Front, g.Hg + 40 * u)));
            p.Bodies.Add(Body3.Box(id, new V3(xw, yw - g.Front + 5 * u, g.Hg + 40 * u), new V3(105 * u, 55 * u, 95 * u), BodyKind.White, RoboTags.Joint));
            var ramTop = zf + g.Sz + 330 * u;
            p.Bodies.Add(Body3.Box(id, new V3(xw, yw - g.Front - 70 * u, (zf + 20 * u + ramTop) / 2), new V3(35 * u, 35 * u, (ramTop - zf - 20 * u) / 2), BodyKind.Link, RoboTags.Link));
            var flange = new V3(xw, yw - g.Front - 70 * u, zf);
            p.Bodies.Add(Body3.Cylinder(flange + new V3(0, 0, 20 * u), flange, 50 * u, 50 * u, BodyKind.White, RoboTags.Flange));
            p.Guides.Add((new V3(xw, yw - g.Front - 70 * u, ramTop), flange));
            p.Joints.Add(new SymJoint(new V3(xw, yw - g.Front, g.Hg + 40 * u), V3.UnitX, JointKind.Prismatic, "x"));
            p.Joints.Add(new SymJoint(new V3(g.RailX, yw, g.Hg - 60 * u), V3.UnitY, JointKind.Prismatic, "y"));
            p.Joints.Add(new SymJoint(new V3(xw, yw - g.Front - 70 * u, g.Hg + 40 * u), V3.UnitZ, JointKind.Prismatic, "z"));
            p.Sticks.Add(new[] { new V3(xw, yw - g.Front - 70 * u, g.Hg + 40 * u), flange });
            p.Base = new V3(-g.RailX, -g.RailY, 0);
            p.Flange = flange;
            p.ToolDir = new V3(0, 0, -1);
            p.FlangeRadius = 50 * u;
            return p;
        }

        /// <summary>A gantry has no radial reach (its working range is the stroke box).</summary>
        public override double? ReachOf(SketchObject o) => null;

        /// <summary>Working range of the flange: the stroke box (exact).</summary>
        public override Area2? Envelope(SketchObject o, string view, double[] q)
        {
            var g = new G(o);
            var u = g.U;
            var yOff = -g.Front - 70 * u;
            var corners = new List<V3>();
            foreach (var x in new[] { -g.Sx / 2, g.Sx / 2 })
                foreach (var y in new[] { -g.Sy / 2 + yOff, g.Sy / 2 + yOff })
                    foreach (var z in new[] { g.Z0, g.Z0 - g.Sz })
                        corners.Add(new V3(x, y, z));
            return Area2.Polygon(Rb.Hull(corners.Select(c => Proj.To2(c, view))));
        }
    }
}
