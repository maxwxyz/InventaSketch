using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Planar serial arm with two or three revolute joints (2R / 3R), the textbook robot of kinematics lessons: joint 1 on a
    /// short pedestal, links of lengths L1, L2 (L3) in the vertical x–z plane (side view), all joint axes along y, angles
    /// clockwise on screen from the x axis (project convention; an upward link has a negative angle). The type draws like
    /// the other robots (bodies or kinematic schematic, ghosts, grips, envelope, tools on the flange) and has element
    /// values for forward and inverse kinematics:
    /// <list type="bullet">
    /// <item>forward: x = Σ Lᵢ·cos Θᵢ, z = −Σ Lᵢ·sin Θᵢ, φ = Θₙ with Θᵢ = θ₁ + … + θᵢ (position of the flange relative to
    /// joint 1, z up; φ the direction of the last link);</item>
    /// <item>inverse: a target (x_T, z_T, for 3R also φ_T) gives the joint angles in closed form, elbow up (θ₂ ≥ 0) or down:
    /// cos θ₂ = (x_W² + y_W² − L1² − L2²)/(2·L1·L2), θ₁ = atan2(y_W, x_W) − atan2(L2·sin θ₂, L1 + L2·cos θ₂) in screen
    /// coordinates (y_W = −z_W), θ₃ = φ_T − θ₁ − θ₂, with the wrist W = T − L3·(cos φ_T, sin φ_T) for 3R. The action
    /// "Move to target" sets the joints; check <c>robot.ik.reach</c> warns when the target is out of reach.</item>
    /// </list>
    /// </summary>
    internal sealed class PlanarArmType : RobotArmType
    {
        public const string Id2 = "robot.arm.planar2r", Id3 = "robot.arm.planar3r";

        private readonly int _n;

        public PlanarArmType(int links) : base(links == 2 ? Id2 : Id3,
            links == 2 ? new I18nText("Planar arm (2 joints, 2R)", "Planarer Arm (2 Gelenke, 2R)") : new I18nText("Planar arm (3 joints, 3R)", "Planarer Arm (3 Gelenke, 3R)"),
            38, false,
            "planar arm", "planar robot", "2r", "3r", "two-link", "three-link", "serial arm", "forward kinematics", "inverse kinematics",
            "planarer arm", "ebener roboter", "zweigelenkarm", "dreigelenkarm", "vorwärtskinematik", "rückwärtskinematik", "inverse kinematik")
        {
            _n = links == 2 ? 2 : 3;
        }

        public int Links => _n;

        private static readonly IReadOnlyList<JointDef> Joints2 = new[]
        {
            JL.R(1, "shoulder", "Schulter", -40, -180, 180),
            JL.R(2, "elbow", "Ellbogen", 70, -170, 170),
        };

        private static readonly IReadOnlyList<JointDef> Joints3 = Joints2.Concat(new[] { JL.R(3, "wrist", "Hand", 40, -170, 170) }).ToList();

        public override IReadOnlyList<JointDef> Joints => _n == 2 ? Joints2 : Joints3;
        protected override string DefaultMotion => "j2";

        private static readonly double[] DefaultLengths = { 400, 300, 120 };

        protected override IReadOnlyList<ParamDef> GeometryParams => new[]
        {
            Pm.Num("base", "Height of joint 1", "Höhe Gelenk 1", 150, 10, 5000, "mm", 10),
            Pm.Num("l1", "Link 1 length", "Länge Glied 1", DefaultLengths[0], 10, 5000, "mm", 10),
            Pm.Num("l2", "Link 2 length", "Länge Glied 2", DefaultLengths[1], 10, 5000, "mm", 10),
        }.Concat(_n == 3 ? new[] { Pm.Num("l3", "Link 3 length (to the flange)", "Länge Glied 3 (bis Flansch)", DefaultLengths[2], 10, 5000, "mm", 5) } : Array.Empty<ParamDef>()).ToList();

        public static readonly ParamDef ElbowParam = Pm.Choice("elbow", "Inverse kinematics: elbow", "Rückwärtskinematik: Ellbogen", "up",
            ("up", "Elbow up (θ₂ ≥ 0)", "Ellbogen oben (θ₂ ≥ 0)"), ("down", "Elbow down (θ₂ < 0)", "Ellbogen unten (θ₂ < 0)"));

        protected override IReadOnlyList<ParamDef> ExtraParams => new[] { ElbowParam }.Concat(PlanarValues.TargetParams(_n)).ToList();

        // ------------------------------------------------------------------ geometry

        internal double[] Lengths(SketchObject o) => Enumerable.Range(0, _n).Select(i => Len(o, "l" + (i + 1).ToString(CultureInfo.InvariantCulture), DefaultLengths[i], 1)).ToArray();

        private static double Height(SketchObject o) => Len(o, "base", 150, 1);

        public override ArmPose Pose(SketchObject o, double[] q)
        {
            var l = Lengths(o);
            var h = Height(o);
            var u = l.Sum() / 820;
            var p = new ArmPose();
            var s = new V3(0, 0, h);
            p.Bodies.Add(Body3.Cylinder(V3.Zero, new V3(0, 0, Math.Min(30 * u, 0.3 * h)), 110 * u, 110 * u, BodyKind.Dark, RoboTags.Base));
            p.Bodies.Add(Body3.Cylinder(new V3(0, 0, Math.Min(30 * u, 0.3 * h)), s, 62 * u, 62 * u, BodyKind.Link, RoboTags.Base));
            var f = new Frame3(M3.Identity, s);
            var pts = new List<V3> { V3.Zero, s };
            var at = s;
            for (var i = 0; i < _n; i++)
            {
                p.Bodies.Add(Body3.Housing(at, V3.UnitY, (52 - 8 * i) * u, (130 - 20 * i) * u));
                p.Joints.Add(new SymJoint(at, V3.UnitY, JointKind.Revolute, "j" + (i + 1).ToString(CultureInfo.InvariantCulture)));
                f = new Frame3(f.R, at).Turn(V3.UnitY, q[i]);
                var next = f.At(l[i], 0, 0);
                var last = i == _n - 1;
                var tool = f.Dir(V3.UnitX);
                var plate = Math.Min(16 * u, 0.2 * l[i]);
                p.Bodies.Add(Body3.Capsule(at, last ? next - tool * plate : next, (36 - 6 * i) * u));
                if (last)
                {
                    p.Bodies.Add(Body3.Cylinder(next - tool * plate, next, 34 * u, 34 * u, BodyKind.White, RoboTags.Flange));
                    p.Flange = next;
                    p.ToolDir = tool;
                    p.FlangeRadius = 34 * u;
                }
                pts.Add(next);
                at = next;
            }
            p.Sticks.Add(pts.ToArray());
            p.Base = V3.Zero;
            return p;
        }

        /// <summary>Joint 1 in the side view (projected model coordinates, page y down).</summary>
        private static Vec2 Shoulder(SketchObject o) => new(0, -Height(o));

        /// <summary>
        /// Working range in the arm plane: the two-link area of links 1 and 2 over the joint ranges (2R: of the flange; 3R: of
        /// the wrist, joint 3, the flange is at most L3 further).
        /// </summary>
        private Area2 SideRegion(SketchObject o)
        {
            var l = Lengths(o);
            var (a, b) = Limit(o, Joints[0]);
            var (c, d) = Limit(o, Joints[1]);
            return Reach.TwoLink(Shoulder(o), l[0], l[1], a, b, c, d);
        }

        protected override double EnvelopeToFlange(SketchObject o) => _n == 3 ? Lengths(o)[2] : 0;

        /// <summary>
        /// Side view: the working range. Top view: the arm moves in the plane of the side view, so the range seen from above
        /// is the strip of its horizontal extent, as wide as the housing of joint 1.
        /// </summary>
        public override Area2? Envelope(SketchObject o, string view, double[] q)
        {
            var side = SideRegion(o);
            if (view != RoboViews.Top) return side;
            if (side.Fill.Count == 0) return null;
            double x0 = side.Fill.Min(p => p.X), x1 = side.Fill.Max(p => p.X);
            var w = 65 * Lengths(o).Sum() / 820;
            return Area2.Polygon(new[] { new Vec2(x0, -w), new Vec2(x1, -w), new Vec2(x1, w), new Vec2(x0, w) });
        }

        /// <summary>Reach of the flange from joint 1: L1 + L2 (+ L3).</summary>
        public override double? ReachOf(SketchObject o) => Lengths(o).Sum();

        // ------------------------------------------------------------------ target marker and framing

        /// <summary>The target entered for the inverse kinematics, relative to joint 1 (model mm, z up), or null.</summary>
        internal Vec2? Target(SketchObject o)
            => PlanarValues.Entered(o, "tx") is double x && PlanarValues.Entered(o, "tz") is double z ? new Vec2(x * 1000, z * 1000) : null;

        /// <summary>The target in projected side-view model coordinates (page y down).</summary>
        private Vec2? TargetPoint(SketchObject o) => Target(o) is { } t ? Shoulder(o) + new Vec2(t.X, -t.Y) : null;

        /// <summary>The frame also holds the target marker (side view), so "Move to target" stays inside the box.</summary>
        public override Rect ModelRect(SketchObject o, string view, ArmPose pose, double[] q)
        {
            var r = base.ModelRect(o, view, pose, q);
            if (view != RoboViews.Top && TargetPoint(o) is { } t)
            {
                var m = 0.04 * Math.Max(r.W, r.H);
                r = r.Union(new Rect(t.X - m, t.Y - m, 2 * m, 2 * m));
            }
            return r;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var prims = base.Render(o, ctx);
            if (ViewOf(o) == RoboViews.Top || TargetPoint(o) is not { } t) return prims;
            var s = SceneFor(o, out _, out _);
            var c = s.L(t);
            var r = 1.4 * s.R;
            var st = RS.Thin with { Stroke = RoboColors.Red, StrokeWidth = 0.25 };
            var list = prims.ToList();
            list.Add(Prim.Circle(c.X, c.Y, r, st) with { Tag = PlanarValues.TargetTag });
            list.Add(Prim.Line(c + new Vec2(-1.6 * r, 0), c + new Vec2(1.6 * r, 0), st) with { Tag = PlanarValues.TargetTag });
            list.Add(Prim.Line(c + new Vec2(0, -1.6 * r), c + new Vec2(0, 1.6 * r), st) with { Tag = PlanarValues.TargetTag });
            return list;
        }

        // ------------------------------------------------------------------ values

        public override bool FlangeValues => false;
        public override IReadOnlyList<VarDef> Variables => PlanarValues.Variables(_n);
        public override IReadOnlyList<Relation> Relations => PlanarValues.Relations(_n);
        public override IEnumerable<ValueCheck> ValueChecks => RoboValues.ArmChecks.Concat(new[] { PlanarValues.Unreachable });

        public override IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
            => base.ValueLinks(o, ctx).Concat(PlanarValues.IkRelations(_n, o.Str("elbow", "up") != "down").Select(r => BoundRelation.Local(r, o.Id)));

        /// <summary>The joint angles (degrees) that put the flange on the entered target, or null (no target, out of reach).</summary>
        internal double[]? TargetJoints(SketchObject o)
        {
            if (Target(o) is not { } t) return null;
            double? phi = _n == 3 ? PlanarValues.Entered(o, "tphi") is double a ? a / PlanarValues.Deg : null : null;
            return PlanarValues.Ik(Lengths(o), t.X, -t.Y, phi, o.Str("elbow", "up") != "down");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in base.Actions(o, lang)) yield return a;
            var ik = TargetJoints(o);
            yield return new ContextAction("robot.ik.apply", new I18nText("Move to target", "Zum Zielpunkt fahren"), IconIds.Values, x =>
            {
                var q = TargetJoints(x);
                if (q is null) return x;
                var r = x;
                for (var i = 0; i < q.Length; i++) r = r.WithParam("j" + (i + 1).ToString(CultureInfo.InvariantCulture), Math.Round(q[i], 3));
                return KeepScale(x, r);
            }) { Group = "joint", IsEnabled = ik is not null, ParamKeys = Joints.Select(j => j.Key).ToArray() };
            foreach (var opt in Rb.Choice(o, "robot.ik.elbow", ElbowParam, "param")) yield return opt;
        }
    }

    /// <summary>Values of the planar arm: forward and inverse kinematics (see <see cref="PlanarArmType"/>).</summary>
    internal static class PlanarValues
    {
        public const double Deg = Math.PI / 180;
        public const string TargetTag = "target";

        private static I18nText T(string en, string de) => new(en, de);

        private static string N(int i) => i.ToString(CultureInfo.InvariantCulture);

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static readonly I18nText TxName = T("Target x (from joint 1, forward)", "Ziel x (ab Gelenk 1, nach vorn)");
        private static readonly I18nText TzName = T("Target z (from joint 1, up)", "Ziel z (ab Gelenk 1, nach oben)");
        private static readonly I18nText TphiName = T("Target direction of the last link (clockwise from x)", "Zielrichtung des letzten Glieds (im Uhrzeigersinn ab x)");

        public static IEnumerable<ParamDef> TargetParams(int n)
        {
            yield return Text("targetX", TxName, "mm");
            yield return Text("targetZ", TzName, "mm");
            if (n == 3) yield return Text("targetPhi", TphiName, "°");
        }

        private static VarDef Len(int i, double def) =>
            new("L" + N(i), T("Link " + N(i) + " length", "Länge Glied " + N(i)), T("L" + N(i), "L" + N(i)), Quantities.Length)
            {
                Unit = "mm", StoredUnit = "mm", Param = "l" + N(i), Relevance = VarRelevance.Panel, Default = def / 1000, Min = 1e-5, Max = 100,
            };

        private static VarDef Theta(int i, double def) =>
            new("th" + N(i), T("Joint angle " + N(i), "Gelenkwinkel " + N(i)), T("θ" + N(i), "θ" + N(i)), Quantities.Angle)
            {
                Unit = "°", StoredUnit = "°", Param = "j" + N(i), Relevance = VarRelevance.Panel, Default = def * Deg, Min = -2 * Math.PI, Max = 2 * Math.PI,
            };

        private static VarDef Pos(string key, I18nText name, string symbol, VarRelevance rel = VarRelevance.Card) =>
            new(key, name, T(symbol, symbol), Quantities.Length) { Unit = "mm", Relevance = rel, ReadOnly = true, Min = -100, Max = 100 };

        private static VarDef Qt(int i) =>
            new("q" + N(i), T("Joint angle " + N(i) + " for the target (inverse kinematics)", "Gelenkwinkel " + N(i) + " für das Ziel (Rückwärtskinematik)"), T("θ" + N(i) + ",T", "θ" + N(i) + ",T"), Quantities.Angle)
            {
                Unit = "°", Relevance = VarRelevance.Panel, ReadOnly = true, Min = -2 * Math.PI, Max = 2 * Math.PI,
            };

        private static readonly double[] DefaultAngles = { -40, 70, 40 };
        private static readonly double[] DefaultLengths = { 400, 300, 120 };

        private static readonly VarDef Tx = new("tx", TxName, T("x_T", "x_T"), Quantities.Length) { Unit = "mm", Param = "targetX", Relevance = VarRelevance.Panel, Min = -100, Max = 100 };
        private static readonly VarDef Tz = new("tz", TzName, T("z_T", "z_T"), Quantities.Length) { Unit = "mm", Param = "targetZ", Relevance = VarRelevance.Panel, Min = -100, Max = 100 };
        private static readonly VarDef Tphi = new("tphi", TphiName, T("φ_T", "φ_T"), Quantities.Angle) { Unit = "°", Param = "targetPhi", Relevance = VarRelevance.Panel, Min = -2 * Math.PI, Max = 2 * Math.PI };

        private static readonly IReadOnlyList<VarDef>[] VarsByN = { Build(2), Build(3) };

        private static IReadOnlyList<VarDef> Build(int n)
        {
            var list = new List<VarDef>();
            for (var i = 1; i <= n; i++) list.Add(Len(i, DefaultLengths[i - 1]));
            for (var i = 1; i <= n; i++) list.Add(Theta(i, DefaultAngles[i - 1]));
            list.Add(Pos("x", T("Flange x (from joint 1, forward kinematics)", "Flansch x (ab Gelenk 1, Vorwärtskinematik)"), "x"));
            list.Add(Pos("z", T("Flange z (from joint 1, up)", "Flansch z (ab Gelenk 1, nach oben)"), "z"));
            list.Add(new VarDef("phi", T("Direction of the last link (clockwise from x)", "Richtung des letzten Glieds (im Uhrzeigersinn ab x)"), T("φ", "φ"), Quantities.Angle)
                { Unit = "°", Relevance = VarRelevance.Card, ReadOnly = true, Min = -4 * Math.PI, Max = 4 * Math.PI });
            list.Add(Tx);
            list.Add(Tz);
            if (n == 3) list.Add(Tphi);
            for (var i = 1; i <= n; i++) list.Add(Qt(i));
            // The robot values; only the reach stays on the card besides x, z and φ.
            list.AddRange(RoboValues.ArmVarsWithCard("reach").Where(v => v.Key is not ("fx" or "fy" or "fz")));
            return list;
        }

        public static IReadOnlyList<VarDef> Variables(int n) => VarsByN[n == 2 ? 0 : 1];

        // ------------------------------------------------------------------ forward kinematics

        private static string Sum(int n, Func<int, string> term) => string.Join(" + ", Enumerable.Range(1, n).Select(term));

        private static string ThetaSum(int i) => i == 1 ? "{th1}" : "(" + Sum(i, k => "{th" + N(k) + "}") + ")";

        private static double Cum(Func<string, double> v, int i) => Enumerable.Range(1, i).Sum(k => v("th" + N(k)));

        private static readonly IReadOnlyList<Relation>[] RelByN = { Fk(2), Fk(3) };

        private static IReadOnlyList<Relation> Fk(int n)
        {
            var keys = Enumerable.Range(1, n).SelectMany(i => new[] { "L" + N(i), "th" + N(i) }).ToArray();
            var ex = Sum(n, i => "{L" + N(i) + "}·cos " + ThetaSum(i));
            var ez = "−(" + Sum(n, i => "{L" + N(i) + "}·sin " + ThetaSum(i)) + ")";
            var ephi = Sum(n, i => "{th" + N(i) + "}");
            var validity = T("angles clockwise on screen from the x axis; position relative to joint 1, z up", "Winkel im Uhrzeigersinn ab der x-Achse; Lage bezogen auf Gelenk 1, z nach oben");
            return new[]
            {
                new Relation("robot.planar.fk.x", T("Forward kinematics: flange x", "Vorwärtskinematik: Flansch x"), "{x} = " + ex, new[] { "x" }.Concat(keys).ToArray()) { Validity = validity }
                    .Solve("x", ex, v => Enumerable.Range(1, n).Sum(i => v("L" + N(i)) * Math.Cos(Cum(v, i)))),
                new Relation("robot.planar.fk.z", T("Forward kinematics: flange z", "Vorwärtskinematik: Flansch z"), "{z} = " + ez, new[] { "z" }.Concat(keys).ToArray()) { Validity = validity }
                    .Solve("z", ez, v => -Enumerable.Range(1, n).Sum(i => v("L" + N(i)) * Math.Sin(Cum(v, i)))),
                new Relation("robot.planar.fk.phi", T("Forward kinematics: direction of the last link", "Vorwärtskinematik: Richtung des letzten Glieds"), "{phi} = " + ephi,
                    new[] { "phi" }.Concat(Enumerable.Range(1, n).Select(i => "th" + N(i))).ToArray()) { Validity = validity }
                    .Solve("phi", ephi, v => Cum(v, n)),
            }.Concat(RoboValues.ArmRelations).ToList();
        }

        public static IReadOnlyList<Relation> Relations(int n) => RelByN[n == 2 ? 0 : 1];

        // ------------------------------------------------------------------ inverse kinematics

        /// <summary>
        /// Joint angles (degrees, clockwise on screen) that put the flange of a planar chain with link lengths
        /// <paramref name="l"/> on the target (<paramref name="x"/>, <paramref name="y"/>) in screen coordinates relative to
        /// joint 1 (y down; = −z); for three links the last link points along <paramref name="phiDeg"/>. Elbow up: θ₂ ≥ 0.
        /// Null if the target is out of reach (or φ is missing for three links).
        /// </summary>
        public static double[]? Ik(IReadOnlyList<double> l, double x, double y, double? phiDeg, bool elbowUp)
        {
            if (l.Count < 2) return null;
            double wx = x, wy = y;
            if (l.Count >= 3)
            {
                if (phiDeg is not double phi) return null;
                wx -= l[2] * Math.Cos(phi * Deg);
                wy -= l[2] * Math.Sin(phi * Deg);
            }
            double l1 = l[0], l2 = l[1];
            var c2 = (wx * wx + wy * wy - l1 * l1 - l2 * l2) / (2 * l1 * l2);
            if (c2 < -1 - 1e-9 || c2 > 1 + 1e-9) return null;
            var q2 = Math.Acos(Math.Max(-1, Math.Min(1, c2))) * (elbowUp ? 1 : -1);
            var q1 = Math.Atan2(wy, wx) - Math.Atan2(l2 * Math.Sin(q2), l1 + l2 * Math.Cos(q2));
            var q = new List<double> { Rb.NormAngle(q1 / Deg), q2 / Deg };
            if (l.Count >= 3) q.Add(Rb.NormAngle(phiDeg!.Value - q[0] - q[1]));
            return q.ToArray();
        }

        private static readonly Dictionary<(int, bool), IReadOnlyList<Relation>> IkCache = new();

        /// <summary>θᵢ,T from the target, one relation per joint, for the elbow configuration.</summary>
        public static IReadOnlyList<Relation> IkRelations(int n, bool elbowUp)
        {
            lock (IkCache)
            {
                if (IkCache.TryGetValue((n, elbowUp), out var cached)) return cached;
                var inputs = new List<string> { "tx", "tz" };
                if (n == 3) inputs.Add("tphi");
                inputs.AddRange(Enumerable.Range(1, n).Select(i => "L" + N(i)));
                var name = elbowUp ? T("Inverse kinematics (elbow up)", "Rückwärtskinematik (Ellbogen oben)") : T("Inverse kinematics (elbow down)", "Rückwärtskinematik (Ellbogen unten)");
                var validity = T("closed form for a planar chain; cos θ₂ = (x_W² + z_W² − L1² − L2²)/(2·L1·L2)", "geschlossene Lösung für eine ebene Kette; cos θ₂ = (x_W² + z_W² − L1² − L2²)/(2·L1·L2)");
                var list = new List<Relation>();
                for (var i = 1; i <= n; i++)
                {
                    var k = i - 1;
                    var key = "q" + N(i);
                    var formula = i switch
                    {
                        1 => "atan2(−{tz}_W, {tx}_W) − atan2(L2·sin θ₂, L1 + L2·cos θ₂)",
                        2 => (elbowUp ? "+" : "−") + "arccos((x_W² + z_W² − {L1}² − {L2}²)/(2·{L1}·{L2}))",
                        _ => "{tphi} − θ₁ − θ₂",
                    };
                    list.Add(new Relation("robot.planar.ik." + key + (elbowUp ? ".up" : ".down"), name, "{" + key + "} = " + formula, new[] { key }.Concat(inputs).ToArray()) { Validity = validity }
                        .Solve(key, formula, v =>
                        {
                            var l = Enumerable.Range(1, n).Select(j => v("L" + N(j))).ToArray();
                            double? phi = n == 3 ? v("tphi") / Deg : null;
                            var q = Ik(l, v("tx"), -v("tz"), phi, elbowUp);
                            return q is null ? double.NaN : q[k] * Deg;
                        }));
                }
                IkCache[(n, elbowUp)] = list;
                return list;
            }
        }

        /// <summary>robot.ik.reach: the target entered for the inverse kinematics lies outside the arm's reach.</summary>
        public static readonly ValueCheck Unreachable = new("robot.ik.reach", CheckSeverity.Warning,
            T("The target entered for the inverse kinematics cannot be reached with these link lengths (too far from or too close to joint 1, or – with three links – not in this direction). Move the target or change the link lengths.",
              "Das für die Rückwärtskinematik eingegebene Ziel ist mit diesen Gliedlängen nicht erreichbar (zu weit von Gelenk 1 entfernt oder zu nah daran oder – bei drei Gliedern – nicht in dieser Richtung). Ziel verschieben oder Gliedlängen ändern."),
            "|L1 − L2| ≤ |W| ≤ L1 + L2",
            c =>
            {
                if (c["tx"] is not double x || c["tz"] is not double z || c["L1"] is not double l1 || c["L2"] is not double l2) return false;
                var l = new List<double> { l1, l2 };
                double? phi = null;
                if (c["L3"] is double l3)
                {
                    l.Add(l3);
                    if (c["tphi"] is double p) phi = p / Deg;
                    else return Math.Sqrt(x * x + z * z) > l1 + l2 + l3 + 1e-9;
                }
                return Ik(l, x, -z, phi, true) is null;
            });

        /// <summary>The entered value (SI) of the variable <paramref name="key"/> of a planar arm, or null.</summary>
        public static double? Entered(SketchObject o, string key)
        {
            var v = key switch { "tx" => Tx, "tz" => Tz, "tphi" => Tphi, _ => null };
            return v is null ? null : ValueModel.ReadEntered(o, v).Value;
        }
    }
}
