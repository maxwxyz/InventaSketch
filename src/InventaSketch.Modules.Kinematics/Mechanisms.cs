using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Four-bar linkage (crank O2-A, coupler A-B, rocker B-O4, frame O2-O4); analytic loop closure. Element values
    /// (<see cref="KinValues.FourBar"/>): Grashof margin, transmission angles, rocker swing, time ratio and the output speed
    /// at the current position; the Grashof class can be shown as a badge.
    /// </summary>
    internal sealed partial class FourBarType : MechanismType, IValueType, IValueLinks, IValueChecks
    {
        public FourBarType() : base("kin.mech.fourbar", new I18nText("Four-bar linkage", "Viergelenkgetriebe"), KinCategories.Mechanisms, new Size2(60, 44),
            "four-bar", "fourbar", "linkage", "crank-rocker", "coupler curve", "viergelenk", "koppelgetriebe", "kurbelschwinge", "koppelkurve", "gelenkviereck")
        { }

        public override double DefaultAngle => -60;
        public override (int Links, int Lower, int Higher) Topology => (4, 4, 0);

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KP.Num("ground", "Frame length O2-O4", "Gestelllänge O2-O4", 40, 0.1, 1e5),
            KP.Num("crank", "Crank length", "Kurbellänge", 12, 0.1, 1e5),
            KP.Num("coupler", "Coupler length", "Koppellänge", 36, 0.1, 1e5),
            KP.Num("rocker", "Rocker length", "Schwingenlänge", 30, 0.1, 1e5),
            KP.Choice("branch", "Assembly mode", "Montagelage", "open", ("open", "Open", "Offen"), ("crossed", "Crossed", "Gekreuzt")),
            KP.Num("cpu", "Coupler point along coupler", "Koppelpunkt längs Koppel", 0.5, -2, 3, null, 0.05, advanced: true),
            KP.Num("cpv", "Coupler point offset (× coupler)", "Koppelpunkt-Versatz (× Koppel)", 0, -3, 3, null, 0.05, advanced: true),
            GrashofParam,
            KinPole.Param,
        }).Concat(KinValues.FourBar.Params).ToList();

        private static readonly ParamDef GrashofParam = KP.Bool("grashof", "Show Grashof class", "Grashof-Klasse zeigen", false);

        public IReadOnlyList<VarDef> Variables => KinValues.FourBar.Variables;
        public IReadOnlyList<Relation> Relations => KinValues.FourBar.Relations;
        public IEnumerable<ValueCheck> ValueChecks => KinValues.FourBar.Checks;

        /// <summary>
        /// The velocity ratio, the coupler's angular velocity and the coupler point's speed and acceleration at the current
        /// position (for the assembly mode and coupler point of the element), and the shaft line on the crank pivot's hub.
        /// </summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            var crossed = o.Str("branch", "open") == "crossed";
            yield return BoundRelation.Local(crossed ? KinValues.RatioCrossed : KinValues.RatioOpen, o.Id);
            yield return BoundRelation.Local(crossed ? KinValues.CouplerSpeedCrossed : KinValues.CouplerSpeedOpen, o.Id);
            double u = o.Num("cpu", 0.5), v = o.Num("cpv", 0);
            yield return BoundRelation.Local(KinValues.CouplerPointSpeed(crossed, u, v), o.Id);
            yield return BoundRelation.Local(KinValues.CouplerPointAcc(crossed, u, v), o.Id);
            foreach (var l in KinShaft.Links(o, ctx, Owner)) yield return l;
        }

        /// <summary>Link lengths (frame, crank, coupler, rocker) of the element in model mm.</summary>
        internal static (double D, double A, double B, double C) Lengths(SketchObject o) => (Len(o, "ground", 40), Len(o, "crank", 12), Len(o, "coupler", 36), Len(o, "rocker", 30));

        /// <summary>The Grashof class of the element.</summary>
        public static GrashofClass ClassOf(SketchObject o)
        {
            var (d, a, b, c) = Lengths(o);
            return Linkages.Classify(d, a, b, c);
        }

        protected override bool ReservesTop(SketchObject o) => base.ReservesTop(o) || o.Flag("grashof", false);

        /// <summary>
        /// The instant centre of the coupler when "pole" is on, and the Grashof class badge ("Crank-rocker" /
        /// "Kurbelschwinge") at the top right when "grashof" is on.
        /// </summary>
        protected override void DrawAnnotations(Scene s, SketchObject o, Lang lang, double used)
        {
            if (KinPole.Shown(o))
            {
                var (d, a, b, c) = Lengths(o);
                if (Linkages.FourBar(d, a, b, c, AngleOf(o), o.Str("branch", "open") == "crossed") is { } p)
                    KinPole.Draw(s, Linkages.CouplerPole(p), (p.O2, p.A), (p.O4, p.B), lang);
            }
            if (o.Flag("grashof", false)) RightBadge(s, Linkages.ClassName(ClassOf(o)).Get(lang), KinTags.Grashof, used);
        }

        /// <summary>Input grip on the crank pin A, turning the crank about O2.</summary>
        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O2", "A") ? (p["O2"], p["A"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            var (d, a, b, c) = Lengths(o);
            var crossed = o.Str("branch", "open") == "crossed";
            var fb = Linkages.FourBar(d, a, b, c, angle, crossed);
            var p = new Pose(fb is not null);
            p["O2"] = Vec2.Zero;
            p["O4"] = new Vec2(d, 0);
            p["A"] = Linkages.Dir(angle) * a;
            if (fb is null) return p;
            p["B"] = fb.Value.B;
            var u = o.Num("cpu", 0.5); var v = o.Num("cpv", 0);
            var ab = fb.Value.B - fb.Value.A;
            var cp = fb.Value.A + ab * u - ab.Perp * v;
            if (Math.Abs(v) > 1e-9 || u < 0 || u > 1) p["P"] = cp;
            p.Trace = cp;
            return p;
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            s.GroundPivot(new Vec2(Len(o, "ground", 40), 0), new Vec2(0, 1));
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            s.Bar(p["O2"], p["A"]);
            if (p.Has("B"))
            {
                s.Bar(p["A"], p["B"]);
                s.Bar(p["B"], p["O4"]);
                if (p.Has("P")) { s.Bar(p["A"], p["P"]); s.Bar(p["B"], p["P"]); }
            }
            if (!s.Ghost) { s.Joint(p["O2"]); s.Joint(p["O4"]); }
            s.Joint(p["A"]);
            if (p.Has("B")) s.Joint(p["B"]);
        }

        /// <summary>Pivots O2 and O4, crank O2-A, coupler A-B, rocker O4-B.</summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s)
        {
            if (!p.Has("B")) return null;
            return new[]
            {
                PivotPart(s, p["O2"]),                                                     // 0
                PivotPart(s, p["O4"]),                                                     // 1
                LinkPart(s, p["O2"], p["A"], new PartDock(0, "c"), null),                 // 2 crank
                LinkPart(s, p["A"], p["B"], new PartDock(2, "j1"), null),                 // 3 coupler
                LinkPart(s, p["O4"], p["B"], new PartDock(1, "c"), new PartDock(3, "j1")), // 4 rocker
            };
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O2"], "O2");
            yield return Mount(s, p["O4"], "O4");
            // The crank pivot's hub: a gear, pulley or motor shaft docked there drives the crank (shaft line, speed n).
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O2"]), "hub");
            foreach (var k in new[] { "O2", "A", "B", "O4", "P" })
                if (p.Has(k)) yield return Port(s, p[k], k);
        }

        protected override IEnumerable<ContextAction> ExtraActions(SketchObject o)
        {
            var crossed = o.Str("branch", "open") == "crossed";
            yield return new ContextAction("kin.branch", new I18nText("Crossed assembly mode", "Gekreuzte Montagelage"), IconIds.Toggle,
                x => x.WithParam("branch", x.Str("branch", "open") == "crossed" ? "open" : "crossed")) { IsActive = crossed, Group = "param", ParamKeys = new[] { "branch" } };
            yield return KA.Toggle(o, "kin.grashof.badge", new I18nText("Show Grashof class", "Grashof-Klasse zeigen"), IconIds.Values, "grashof", false, "annotation");
            yield return KinPole.Action(o);
        }
    }

    /// <summary>Slider-crank (crank O-A, connecting rod A-B, slider B on a guide, optional offset).</summary>
    internal sealed class SliderCrankType : MechanismType, IValueType, IValueChecks, IValueLinks
    {
        public SliderCrankType() : base("kin.mech.slidercrank", new I18nText("Slider-crank", "Schubkurbel"), KinCategories.Mechanisms, new Size2(70, 34),
            "slider-crank", "slider crank", "crank", "piston", "connecting rod", "engine", "schubkurbel", "kurbeltrieb", "pleuel", "kolben", "schubkurbelgetriebe")
        { }

        public override double DefaultAngle => -60;
        public override (int Links, int Lower, int Higher) Topology => (4, 4, 0);

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KP.Num("crank", "Crank length", "Kurbellänge", 12, 0.1, 1e5),
            KP.Num("rod", "Connecting rod length", "Pleuellänge", 36, 0.1, 1e5),
            KP.Num("offset", "Offset (eccentricity)", "Versatz (Exzentrizität)", 0, -1e5, 1e5),
            KinPole.Param,
        }).Concat(KinValues.SliderCrank.Params).ToList();

        /// <summary>Stroke, time ratio, smallest transmission angle and slider speed (<see cref="KinValues.SliderCrank"/>).</summary>
        public IReadOnlyList<VarDef> Variables => KinValues.SliderCrank.Variables;
        public IReadOnlyList<Relation> Relations => KinValues.SliderCrank.Relations;
        public IEnumerable<ValueCheck> ValueChecks => KinValues.SliderCrank.Checks;

        /// <summary>The shaft line on the crank pivot's hub (a motor or gear docked there shares the speed n).</summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => KinShaft.Links(o, ctx, Owner);

        /// <summary>The instant centre of the connecting rod when "pole" is on.</summary>
        protected override void DrawAnnotations(Scene s, SketchObject o, Lang lang, double used)
        {
            if (!KinPole.Shown(o)) return;
            if (Linkages.SliderCrank(Len(o, "crank", 12), Len(o, "rod", 36), o.Num("offset", 0), AngleOf(o)) is { } p)
                KinPole.Draw(s, Linkages.RodPole(p), (p.O, p.A), (p.B, p.B + new Vec2(0, 1)), lang);
        }

        protected override IEnumerable<ContextAction> ExtraActions(SketchObject o)
        {
            yield return KinPole.Action(o);
        }

        /// <summary>Input grip on the crank pin A, turning the crank about O.</summary>
        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O", "A") ? (p["O"], p["A"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            double r = Len(o, "crank", 12), l = Len(o, "rod", 36), e = o.Num("offset", 0);
            var sc = Linkages.SliderCrank(r, l, e, angle);
            var p = new Pose(sc is not null);
            p["O"] = Vec2.Zero;
            p["A"] = Linkages.Dir(angle) * r;
            if (sc is null) return p;
            p["B"] = sc.Value.B;
            p.Trace = Vec2.Lerp(sc.Value.A, sc.Value.B, 0.5);
            return p;
        }

        private (double Min, double Max) Stroke(SketchObject o)
        {
            double min = double.MaxValue, max = double.MinValue;
            for (var a = 0; a < 360; a += 2)
            {
                var p = Solve(o, a);
                if (!p.Ok) continue;
                min = Math.Min(min, p["B"].X); max = Math.Max(max, p["B"].X);
            }
            if (min > max) { var l = Len(o, "rod", 36); min = max = l; }
            return (min, max);
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            var (min, max) = Stroke(o);
            var e = o.Num("offset", 0);
            s.Guide(new Vec2(min, e), new Vec2(max, e), new Vec2(0, 1));
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            s.Bar(p["O"], p["A"]);
            if (p.Has("B")) s.Bar(p["A"], p["B"]);
            if (p.Has("B")) s.Slider(p["B"], new Vec2(1, 0));
            if (!s.Ghost) s.Joint(p["O"]);
            s.Joint(p["A"]);
            if (p.Has("B")) s.Joint(p["B"]);
        }

        /// <summary>Pivot O, crank O-A, connecting rod A-B, slider on a horizontal guide at B.</summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s)
        {
            if (!p.Has("B")) return null;
            return new[]
            {
                PivotPart(s, p["O"]),                                                       // 0
                SliderPart(s, p["B"], new Vec2(1, 0), new Vec2(0, 1)),                      // 1 slider
                LinkPart(s, p["O"], p["A"], new PartDock(0, "c"), null),                    // 2 crank
                LinkPart(s, p["A"], p["B"], new PartDock(2, "j1"), new PartDock(1, "c")),   // 3 connecting rod
            };
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O"], "O");
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O"]), "hub");
            foreach (var k in new[] { "O", "A", "B" })
                if (p.Has(k)) yield return Port(s, p[k], k);
            var (min, max) = Stroke(o);
            var e = o.Num("offset", 0);
            var (a, b) = s.AxisSegment(new Vec2(min, e), new Vec2(max, e));
            yield return Anchor.Segment(AnchorRole.Axis, a, b) with { Id = "guide" };
        }
    }

    /// <summary>Scotch yoke: crank pin slides in a slotted yoke that translates on a guide (pure harmonic motion).</summary>
    internal sealed class ScotchYokeType : MechanismType, IValueType, IValueLinks
    {
        public ScotchYokeType() : base("kin.mech.scotchyoke", new I18nText("Scotch yoke", "Kreuzschubkurbel"), KinCategories.Mechanisms, new Size2(64, 34),
            "scotch yoke", "slotted link", "harmonic", "yoke", "kreuzschleife", "kreuzschubkurbel", "kurbelschleife", "schleife")
        { }

        public override double DefaultAngle => -60;
        public override (int Links, int Lower, int Higher) Topology => (4, 4, 0);

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KP.Num("crank", "Crank length", "Kurbellänge", 12, 0.1, 1e5),
        }).Concat(KinValues.ScotchYoke.Params).ToList();

        /// <summary>Stroke H = 2r and the yoke speed (<see cref="KinValues.ScotchYoke"/>).</summary>
        public IReadOnlyList<VarDef> Variables => KinValues.ScotchYoke.Variables;
        public IReadOnlyList<Relation> Relations => KinValues.ScotchYoke.Relations;

        /// <summary>The shaft line on the crank pivot's hub (a motor or gear docked there shares the speed n).</summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => KinShaft.Links(o, ctx, Owner);

        /// <summary>Input grip on the crank pin A, turning the crank about O.</summary>
        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O", "A") ? (p["O"], p["A"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            var r = Len(o, "crank", 12);
            var a = Linkages.Dir(angle) * r;
            var p = new Pose(true);
            p["O"] = Vec2.Zero;
            p["A"] = a;
            p["Y0"] = new Vec2(a.X, -1.25 * r);
            p["Y1"] = new Vec2(a.X, 1.25 * r);
            p["E"] = new Vec2(a.X + 3.4 * r, 0);
            p.Trace = a;
            return p;
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            var r = Len(o, "crank", 12);
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            // Prismatic pair yoke rod / frame: guide blocks above and below the rod.
            s.Guide(new Vec2(1.55 * r, 0), new Vec2(2.15 * r, 0), new Vec2(0, -1), 1.2, 0);
            s.Guide(new Vec2(1.55 * r, 0), new Vec2(2.15 * r, 0), new Vec2(0, 1), 1.2, 0);
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            var w = 1.9 * s.R; // half slot width (mm)
            var y0 = s.L(p["Y0"]); var y1 = s.L(p["Y1"]);
            var e = s.L(p["E"]);
            var style = s.Ghost ? KS.Ghost : KS.Link;
            // Yoke: closed slot frame plus rod.
            var frame = new[] { y0 + new Vec2(-w, 0), y0 + new Vec2(w, 0), y1 + new Vec2(w, 0), y1 + new Vec2(-w, 0) };
            s.Add(Prim.Polygon(frame, s.Detailed && !s.Ghost ? KS.Body : style), KinTags.Link);
            var mid = s.L(new Vec2(p["A"].X, 0));
            s.Add(Prim.Line(mid + new Vec2(w, 0), e, style), KinTags.Link);
            s.Bar(p["O"], p["A"]);
            s.Slider(p["A"], new Vec2(0, 1), 1.6, 1.5);
            if (!s.Ghost) s.Joint(p["O"]);
            s.Joint(p["A"]);
        }

        /// <summary>
        /// Pivot O, crank O-A whose pin slides in the slot Y0-Y1 of the yoke (a plate with the rod end E); sliders on
        /// horizontal guides at the slot end Y0 and at the rod end E keep the yoke upright, so it only translates.
        /// </summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s) => new[]
        {
            PivotPart(s, p["O"]),                                                         // 0
            SliderPart(s, p["Y0"], new Vec2(1, 0), new Vec2(0, -1)),                      // 1 upper guide
            SliderPart(s, p["E"], new Vec2(1, 0), new Vec2(0, 1)),                        // 2 rod guide
            new PartSpec("kin.link.ternary", null, Vec2.Zero)                             // 3 yoke
            {
                Points = new[] { s.L(p["Y0"]), s.L(p["Y1"]), s.L(p["E"]) },
                Docks = new PartDock?[] { new(1, "c"), null, new(2, "c") },
            },
            LinkPart(s, p["O"], p["A"], new PartDock(0, "c"), new PartDock(3, Linkage.SlotPrefix + "0")), // 4 crank, pin in the slot
        };

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O"], "O");
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O"]), "hub");
            yield return Port(s, p["O"], "O");
            yield return Port(s, p["A"], "A");
            yield return Port(s, p["E"], "E");
            var r = Len(o, "crank", 12);
            var (a, b) = s.AxisSegment(new Vec2(1.55 * r, 0), new Vec2(2.15 * r, 0), 0);
            yield return Anchor.Segment(AnchorRole.Axis, a, b) with { Id = "guide" };
        }
    }

    /// <summary>
    /// Toggle (knee) mechanism: link 1 from the fixed pivot A to the knee B, link 2 from B to the slider C on a guide
    /// through A. The input angle is the angle of link 1 from the guide axis; 0° is the stretched (toggle) position.
    /// </summary>
    internal sealed class ToggleType : MechanismType
    {
        public ToggleType() : base("kin.mech.toggle", new I18nText("Toggle mechanism", "Kniehebel"), KinCategories.Mechanisms, new Size2(40, 56),
            "toggle", "knee", "toggle press", "toggle clamp", "kniehebel", "kniehebelpresse", "spanner")
        { }

        public override double DefaultAngle => 30;
        public override (int Links, int Lower, int Higher) Topology => (4, 4, 0);

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KP.Num("link1", "Upper link length", "Länge oberer Hebel", 26, 0.1, 1e5),
            KP.Num("link2", "Lower link length", "Länge unterer Hebel", 26, 0.1, 1e5),
            KP.Bool("force", "Show input force", "Antriebskraft zeigen", true),
        }).ToList();

        protected override IEnumerable<double> Sweep(SketchObject o)
        {
            for (var a = -75; a <= 75; a += 5) yield return a;
        }

        /// <summary>Ghosts between the current angle and the stretched position.</summary>
        protected override IEnumerable<double> GhostAngles(SketchObject o, double angle, int n)
        {
            var a = KA.NormAngle(angle);
            if (Math.Abs(a) < 1) a = 45;
            for (var k = 1; k <= n; k++) yield return a * (1 - (double)k / (n + 1));
        }

        /// <summary>Input grip on the knee joint B, turning the upper link about A.</summary>
        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("A", "B") ? (p["A"], p["B"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            double l1 = Len(o, "link1", 26), l2 = Len(o, "link2", 26);
            var t = Geo.DegToRad(angle);
            var b = new Vec2(-l1 * Math.Sin(t), l1 * Math.Cos(t));
            var k2 = l2 * l2 - b.X * b.X;
            var p = new Pose(k2 >= -1e-9);
            p["A"] = Vec2.Zero;
            p["B"] = b;
            p.Trace = b;
            if (!p.Ok) return p;
            p["C"] = new Vec2(0, b.Y + Math.Sqrt(Math.Max(0, k2)));
            return p;
        }

        private (double Min, double Max) Stroke(SketchObject o)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var a in Sweep(o))
            {
                var p = Solve(o, a);
                if (!p.Ok) continue;
                min = Math.Min(min, p["C"].Y); max = Math.Max(max, p["C"].Y);
            }
            var cur = Solve(o, AngleOf(o));
            if (cur.Ok) { min = Math.Min(min, cur["C"].Y); max = Math.Max(max, cur["C"].Y); }
            if (min > max) min = max = Len(o, "link1", 26) + Len(o, "link2", 26);
            return (min, max);
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, -1));
            var (min, max) = Stroke(o);
            s.Guide(new Vec2(0, min), new Vec2(0, max), new Vec2(1, 0));
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            s.Bar(p["A"], p["B"]);
            if (p.Has("C"))
            {
                s.Bar(p["B"], p["C"]);
                s.Slider(p["C"], new Vec2(0, 1));
            }
            if (!s.Ghost && o.Flag("force", true) && Math.Abs(p["B"].X) > 1e-6)
                s.ForceArrow(p["B"], new Vec2(-Math.Sign(p["B"].X), 0));
            if (!s.Ghost) s.Joint(p["A"]);
            s.Joint(p["B"]);
            if (p.Has("C")) s.Joint(p["C"]);
        }

        /// <summary>Pivot A, link A-B, link B-C, slider at C on the guide through A.</summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s)
        {
            if (!p.Has("C")) return null;
            return new[]
            {
                PivotPart(s, p["A"]),                                                        // 0
                SliderPart(s, p["C"], new Vec2(0, 1), new Vec2(1, 0)),                       // 1 slider
                LinkPart(s, p["A"], p["B"], new PartDock(0, "c"), null),                     // 2 upper link
                LinkPart(s, p["B"], p["C"], new PartDock(2, "j1"), new PartDock(1, "c")),    // 3 lower link
            };
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["A"], "A");
            foreach (var k in new[] { "A", "B", "C" })
                if (p.Has(k)) yield return Port(s, p[k], k);
            var (min, max) = Stroke(o);
            var (a, b) = s.AxisSegment(new Vec2(0, min), new Vec2(0, max));
            yield return Anchor.Segment(AnchorRole.Axis, a, b) with { Id = "guide" };
        }
    }

    /// <summary>
    /// Crank and slotted lever quick-return mechanism (shaper drive): crank O2-A drives a block sliding on the lever
    /// O4-T; link T-B drives the ram slider B on a horizontal guide. Six links, seven lower pairs, F = 1.
    /// </summary>
    internal sealed class QuickReturnType : MechanismType, IValueType
    {
        public QuickReturnType() : base("kin.mech.quickreturn", new I18nText("Quick-return mechanism", "Schnellrücklaufgetriebe"), KinCategories.Mechanisms, new Size2(60, 64),
            "quick return", "quick-return", "shaper", "slotted lever", "crank and slotted lever", "schnellrücklauf", "kurbelschwinge", "shaping", "stoßmaschine", "kulisse")
        { }

        public override double DefaultAngle => -30;
        public override (int Links, int Lower, int Higher) Topology => (6, 7, 0);

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KP.Num("ground", "Pivot distance O2-O4", "Gestellabstand O2-O4", 20, 0.1, 1e5),
            KP.Num("crank", "Crank length", "Kurbellänge", 10, 0.1, 1e5),
            KP.Num("lever", "Lever length", "Schwingenlänge", 40, 0.1, 1e5),
            KP.Num("link", "Link length", "Koppellänge", 16, 0.1, 1e5),
            KP.Num("ram", "Ram guide height above O4", "Höhe Stößelführung über O4", 44, 0.1, 1e5),
        }).Concat(KinValues.QuickReturn.Params).ToList();

        /// <summary>Lever swing, time ratio and ram stroke (<see cref="KinValues.QuickReturn"/>).</summary>
        public IReadOnlyList<VarDef> Variables => KinValues.QuickReturn.Variables;
        public IReadOnlyList<Relation> Relations => KinValues.QuickReturn.Relations;

        /// <summary>Input grip on the crank pin A, turning the crank about O2.</summary>
        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O2", "A") ? (p["O2"], p["A"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            double d = Len(o, "ground", 20), r = Len(o, "crank", 10), lv = Len(o, "lever", 40), l = Len(o, "link", 16), h = Len(o, "ram", 44);
            var o4 = Vec2.Zero;
            var o2 = new Vec2(0, -d);
            var a = o2 + Linkages.Dir(angle) * r;
            var ok = (a - o4).Length > 1e-9;
            var p = new Pose(ok);
            p["O2"] = o2; p["O4"] = o4; p["A"] = a;
            p.Trace = a;
            if (!ok) return p;
            var u = (a - o4).Normalized();
            var t = o4 + u * lv;
            var dy = -h - t.Y;
            var k2 = l * l - dy * dy;
            if (k2 < -1e-9)
            {
                var q = new Pose(false);
                q["O2"] = o2; q["O4"] = o4; q["A"] = a; q["T"] = t;
                return q;
            }
            p["T"] = t;
            p["B"] = new Vec2(t.X + Math.Sqrt(Math.Max(0, k2)), -h);
            return p;
        }

        private (double Min, double Max) Stroke(SketchObject o)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var a in Sweep(o))
            {
                var p = Solve(o, a);
                if (!p.Ok) continue;
                min = Math.Min(min, p["B"].X); max = Math.Max(max, p["B"].X);
            }
            if (min > max) min = max = 0;
            return (min, max);
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            s.GroundPivot(new Vec2(0, -Len(o, "ground", 20)), new Vec2(-1, 0));
            var (min, max) = Stroke(o);
            var h = Len(o, "ram", 44);
            s.Guide(new Vec2(min, -h), new Vec2(max, -h), new Vec2(0, -1));
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            if (!p.Has("O2", "A")) return;
            if (p.Has("T")) s.Bar(p["O4"], p["T"]);
            s.Bar(p["O2"], p["A"]);
            if (p.Has("T")) s.Slider(p["A"], p["T"] - p["O4"], 2.2, 1.4);
            if (p.Has("T", "B"))
            {
                s.Bar(p["T"], p["B"]);
                s.Slider(p["B"], new Vec2(1, 0));
            }
            if (!s.Ghost) { s.Joint(p["O4"]); s.Joint(p["O2"]); }
            s.Joint(p["A"]);
            if (p.Has("T")) s.Joint(p["T"]);
            if (p.Has("B")) s.Joint(p["B"]);
        }

        /// <summary>Pivots O4 and O2, slotted lever O4-T, crank O2-A with its pin in the lever's slot, link T-B, ram slider at B.</summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s)
        {
            if (!p.Has("O2", "A") || !p.Has("T", "B")) return null;
            return new[]
            {
                PivotPart(s, p["O4"]),                                                       // 0
                PivotPart(s, p["O2"]),                                                       // 1
                SliderPart(s, p["B"], new Vec2(1, 0), new Vec2(0, -1)),                      // 2 ram
                LinkPart(s, p["O4"], p["T"], new PartDock(0, "c"), null),                    // 3 slotted lever
                LinkPart(s, p["O2"], p["A"], new PartDock(1, "c"), new PartDock(3, Linkage.SlotPrefix + "0")), // 4 crank, pin in the slot
                LinkPart(s, p["T"], p["B"], new PartDock(3, "j1"), new PartDock(2, "c")),    // 5 link
            };
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            if (p.Has("O4")) yield return Mount(s, p["O4"], "O4");
            if (p.Has("O2")) yield return Mount(s, p["O2"], "O2");
            foreach (var k in new[] { "O2", "A", "O4", "T", "B" })
                if (p.Has(k)) yield return Port(s, p[k], k);
            var (min, max) = Stroke(o);
            var h = Len(o, "ram", 44);
            var (a, b) = s.AxisSegment(new Vec2(min, -h), new Vec2(max, -h));
            yield return Anchor.Segment(AnchorRole.Axis, a, b) with { Id = "guide" };
        }
    }

    /// <summary>
    /// Cam disc as a part: the tangent cam of <see cref="CamFollowerType"/> about its centre (the pivot), turning with the
    /// object's rotation. Sliders whose guide runs through it and whose joint sits on its contour are its followers
    /// (<see cref="Cams"/>); "follower" says how they touch it (roller, flat face, knife edge).
    /// </summary>
    internal sealed class CamDiscType : ObjectType
    {
        public const string TypeId = "kin.cam.disc";
        public override string Id => TypeId;
        public override I18nText Name => new("Cam disc", "Kurvenscheibe");
        public override string Category => KinCategories.Links;
        public override IReadOnlyList<string> Keywords => new[] { "cam", "cam disc", "cam plate", "eccentric", "kurvenscheibe", "nocken", "nockenscheibe", "kurve" };
        public override Size2 DefaultSize => new(30, 30);
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<ViewDef> Views => new[]
        {
            new ViewDef("schematic", new I18nText("Schematic", "Schema")) { Icon = IconIds.Schematic },
            new ViewDef("detailed", new I18nText("Detailed", "Detailliert")) { Icon = IconIds.Detailed },
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Num("base", "Base circle radius", "Grundkreisradius", 8, 0.1, 1e5),
            KP.Num("lift", "Lift", "Hub", 7, 0, 1e5),
            KP.Choice("follower", "Follower", "Stößel", "roller", ("roller", "Roller", "Rolle"), ("flat", "Flat-faced", "Flachstößel"), ("knife", "Knife-edge", "Spitzenstößel")),
        };

        private static (double R0, double Lift) Shape(SketchObject o) => (Math.Max(0.01, o.Num("base", 8)), Math.Max(0, o.Num("lift", 7)));

        /// <summary>Local mm per model mm: the cam's reach (base + lift) fills half the box.</summary>
        private static double Scale(SketchObject o)
        {
            var (r0, h) = Shape(o);
            return Math.Min(o.Size.W, o.Size.H) / (2 * (r0 + h));
        }

        public override IReadOnlyList<Vec2>? CamContour(SketchObject o)
        {
            var (r0, h) = Shape(o);
            var k = Scale(o);
            return CamFollowerType.TangentProfile(r0, h, 0).Select(p => p * k).ToList();
        }

        public override (string Kind, double Radius) CamFollower(SketchObject o)
        {
            var kind = o.Str("follower", "roller");
            return (kind, kind == "roller" ? 0.3 * Shape(o).R0 * Scale(o) : 0);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var k = Scale(o);
            var r0 = Shape(o).R0;
            var list = new List<Primitive>
            {
                Prim.Polygon(CamContour(o)!, KS.Body) with { Tag = "cam" },
                Prim.Circle(0, 0, Math.Max(0.4, 0.12 * r0 * k), KS.Joint) with { Tag = KinTags.Joint },
            };
            if ((o.View ?? "schematic") == "detailed")
            {
                list.Add(Prim.Circle(0, 0, 0.25 * r0 * k, KS.Thin) with { Tag = "hub" });
                var e = 0.4 * r0 * k;
                list.Add(Prim.Line(new Vec2(-e, 0), new Vec2(e, 0), KS.Ctr) with { Tag = "center" });
                list.Add(Prim.Line(new Vec2(0, -e), new Vec2(0, e), KS.Ctr) with { Tag = "center" });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero, "hub");
            yield return Anchor.Point(AnchorRole.Port, Vec2.Zero, "c");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "kin.follower", Parameters.First(p => p.Key == "follower"), null, "param", null)) yield return a;
        }
    }

    /// <summary>
    /// Cam disc with follower (roller, flat-faced or knife-edge) on a vertical guide. The cam profile is a smooth
    /// tangent cam (base circle plus nose circle, lift = nose height); the follower position is computed from the actual contact at "angle".
    /// </summary>
    internal sealed partial class CamFollowerType : MechanismType
    {
        public CamFollowerType() : base("kin.cam.follower", new I18nText("Cam disc with follower", "Kurvenscheibe mit Stößel"), KinCategories.Links, new Size2(36, 50),
            "cam", "follower", "roller follower", "flat follower", "tappet", "kurvenscheibe", "nocken", "stößel", "rollenstößel", "flachstößel", "kurvengetriebe")
        { }

        public override double DefaultAngle => 0;
        /// <summary>Frame, cam, follower (+ roller): cam joint is a higher pair; the roller spin is a passive DOF.</summary>
        public override (int Links, int Lower, int Higher) Topology => (3, 2, 1);
        protected override bool HasMotionOptions => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Angle(DefaultAngle),
            KP.Choice("follower", "Follower", "Stößel", "roller", ("roller", "Roller", "Rolle"), ("flat", "Flat-faced", "Flachstößel"), ("knife", "Knife-edge", "Spitzenstößel")),
            KP.Num("base", "Base circle radius", "Grundkreisradius", 8, 0.1, 1e5),
            KP.Num("lift", "Lift", "Hub", 7, 0, 1e5),
            KP.Dof,
        }.Concat(CamValues.Params).ToList();

        private static string Follower(SketchObject o) => o.Str("follower", "roller");

        /// <summary>
        /// Tangent cam: convex hull of the base circle (radius r0 about the pivot) and a nose circle whose outermost point
        /// is at r0 + lift. At angle 0 the nose points up-left (-135°); the cam turns with "angle".
        /// </summary>
        private static List<Vec2> Profile(SketchObject o, double angle, int n = 72)
            => TangentProfile(Len(o, "base", 8), Math.Max(0, o.Num("lift", 7)), angle, n);

        /// <summary>The tangent cam contour about the pivot (base circle <paramref name="r0"/>, lift <paramref name="h"/>) at <paramref name="angle"/>.</summary>
        internal static List<Vec2> TangentProfile(double r0, double h, double angle, int n = 72)
        {
            var rn = Math.Min(r0, Math.Max(0.45 * r0, r0 - 0.5 * h));
            var dir = Geo.DegToRad(angle - 135);
            var nose = Vec2.Polar(r0 + h - rn, dir);
            var pts = new List<Vec2>(2 * n);
            for (var i = 0; i < n; i++)
            {
                var a = 2 * Math.PI * i / n;
                pts.Add(Vec2.Polar(r0, a));
                pts.Add(nose + Vec2.Polar(rn, a));
            }
            // Densify the straight flanks so contact searches see the whole contour.
            var hull = ConvexHull(pts);
            var step = 0.02 * r0;
            var dense = new List<Vec2>(hull.Count * 4);
            for (var i = 0; i < hull.Count; i++)
            {
                var a = hull[i]; var b = hull[(i + 1) % hull.Count];
                var k = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / step));
                for (var j = 0; j < k; j++) dense.Add(Vec2.Lerp(a, b, (double)j / k));
            }
            return dense;
        }

        /// <summary>Convex hull (monotone chain), clockwise on screen.</summary>
        private static List<Vec2> ConvexHull(List<Vec2> pts)
        {
            var p = pts.OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
            var hull = new List<Vec2>(p.Count + 1);
            for (var pass = 0; pass < 2; pass++)
            {
                var start = hull.Count;
                foreach (var v in p)
                {
                    while (hull.Count >= start + 2 && (hull[hull.Count - 1] - hull[hull.Count - 2]).Cross(v - hull[hull.Count - 2]) <= 1e-12)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(v);
                }
                hull.RemoveAt(hull.Count - 1);
                p.Reverse();
            }
            return hull;
        }

        private static double RollerRadius(SketchObject o) => 0.3 * Len(o, "base", 8);

        /// <summary>
        /// Cam disc (part), follower slider on its guide through the cam centre, fixed pivot under the cam: turning the cam
        /// disc in motion mode moves the follower (<see cref="Cams"/>).
        /// </summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s)
        {
            double r0 = Len(o, "base", 8), h = Math.Max(0, o.Num("lift", 7));
            var side = 2 * (r0 + h) * s.S;
            return new[]
            {
                PivotPart(s, p["O"]),
                new PartSpec(CamDiscType.TypeId, Detailed(o) ? "detailed" : "schematic", s.L(p["O"]))
                {
                    Size = new Size2(side, side),
                    Rotation = Math.Round(AngleOf(o), 6),
                    Params = new Dictionary<string, ParamValue> { ["base"] = r0, ["lift"] = h, ["follower"] = Follower(o) },
                },
                SliderPart(s, p["C"], new Vec2(0, 1), new Vec2(1, 0)),
            };
        }

        private static bool Detailed(SketchObject o) => (o.View ?? "schematic") == "detailed";

        /// <summary>Follower contact height (y of the roller centre, flat face or knife tip).</summary>
        private static double Contact(SketchObject o, IReadOnlyList<Vec2> profile)
        {
            switch (Follower(o))
            {
                case "flat":
                    return profile.Min(p => p.Y);
                case "knife":
                    {
                        var best = double.MaxValue;
                        for (var i = 0; i < profile.Count; i++)
                        {
                            var a = profile[i]; var b = profile[(i + 1) % profile.Count];
                            if ((a.X <= 0) == (b.X <= 0) || Math.Abs(b.X - a.X) < 1e-12) continue;
                            var y = a.Y + (0 - a.X) * (b.Y - a.Y) / (b.X - a.X);
                            best = Math.Min(best, y);
                        }
                        return best == double.MaxValue ? profile.Min(p => p.Y) : best;
                    }
                default:
                    {
                        var rr = RollerRadius(o);
                        var best = double.MaxValue;
                        foreach (var p in profile)
                        {
                            if (Math.Abs(p.X) >= rr) continue;
                            best = Math.Min(best, p.Y - Math.Sqrt(rr * rr - p.X * p.X));
                        }
                        return best;
                    }
            }
        }

        /// <summary>Input grip on the cam nose (its outermost point), turning the cam about its pivot O.</summary>
        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p)
            => p.Has("O", "N") ? (p["O"], p["N"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            double r0 = Len(o, "base", 8), h = Math.Max(0, o.Num("lift", 7));
            var prof = Profile(o, angle);
            var y = Contact(o, prof);
            var rod = 2.2 * r0;
            var rr = RollerRadius(o);
            var p = new Pose(true);
            p["O"] = Vec2.Zero;
            p["C"] = new Vec2(0, y);
            p["T"] = new Vec2(0, y - rod);
            // Cam nose: the outermost point of the contour (inside the framing square below), the input grip.
            p["N"] = Vec2.Polar(r0 + h, Geo.DegToRad(angle - 135));
            p.Extra.Add(new Vec2(-(r0 + h), -(r0 + h)));
            p.Extra.Add(new Vec2(r0 + h, r0 + h));
            p.Extra.Add(new Vec2(-0.7 * r0, y));
            p.Extra.Add(new Vec2(0.7 * r0, y));
            p.Extra.Add(new Vec2(0, y - rr));
            p.Trace = p["C"];
            return p;
        }

        private (double Lo, double Hi) GuideRange(SketchObject o)
        {
            double top = double.MinValue, bottom = double.MaxValue;
            foreach (var a in Sweep(o))
            {
                var p = Solve(o, a);
                top = Math.Max(top, p["T"].Y);       // lowest rod top
                bottom = Math.Min(bottom, p["C"].Y); // highest rod bottom
            }
            var len = bottom - top;
            return (top + 0.2 * len, bottom - 0.3 * len);
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            var (lo, hi) = GuideRange(o);
            if (hi > lo)
            {
                s.Guide(new Vec2(0, lo), new Vec2(0, hi), new Vec2(1, 0), 1.3, 0);
                s.Guide(new Vec2(0, lo), new Vec2(0, hi), new Vec2(-1, 0), 1.3, 0);
            }
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            var angle = AngleOf(o);
            var prof = Profile(o, angle).Select(s.L).ToList();
            var detailed = s.Detailed;
            s.Add(Prim.Polygon(prof, KS.Body), "cam");
            var c = s.L(p["C"]); var t = s.L(p["T"]);
            var f = Follower(o);
            var rr = RollerRadius(o) * s.S;
            var rodBottom = c;
            if (detailed) s.Add(Frag.Block((t + rodBottom) / 2, new Vec2(0, 1), (rodBottom - t).Length / 2, 0.9 * s.R, KS.Body), KinTags.Link);
            else s.Add(Prim.Line(t, rodBottom, KS.Link), KinTags.Link);
            switch (f)
            {
                case "flat":
                    {
                        var hw = 0.7 * Len(o, "base", 8) * s.S;
                        if (detailed) s.Add(Prim.Rect(c.X - hw, c.Y - 0.8 * s.R, 2 * hw, 0.8 * s.R, KS.Body), "follower");
                        else s.Add(Prim.Line(c + new Vec2(-hw, 0), c + new Vec2(hw, 0), KS.Link), "follower");
                        break;
                    }
                case "knife":
                    {
                        var k = 1.2 * s.R;
                        s.Add(Prim.Polygon(new[] { c, c + new Vec2(-k, -1.8 * k), c + new Vec2(k, -1.8 * k) }, KS.Body), "follower");
                        break;
                    }
                default:
                    s.Add(Prim.Circle(c.X, c.Y, rr, KS.Body), "follower");
                    s.Joint(p["C"]);
                    break;
            }
            // Cam pivot: ground symbol below the centre, bore/hub in the detailed view.
            s.GroundPivot(p["O"], new Vec2(0, 1));
            if (detailed)
            {
                var o0 = s.L(p["O"]);
                s.Add(Prim.Circle(o0.X, o0.Y, 0.25 * Len(o, "base", 8) * s.S, KS.Thin), "hub");
            }
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O"], "O");
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O"]), "hub");
            yield return Port(s, p["T"], "T");
            var (lo, hi) = GuideRange(o);
            yield return Anchor.Segment(AnchorRole.Axis, s.L(new Vec2(0, lo)), s.L(new Vec2(0, hi))) with { Id = "guide" };
        }

        protected override IEnumerable<ContextAction> ExtraActions(SketchObject o)
        {
            foreach (var a in ActionChoices.FromParam(o, "kin.follower", Parameters.First(p => p.Key == "follower"), null, "param", null)) yield return a;
        }
    }
}
