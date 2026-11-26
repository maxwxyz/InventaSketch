using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Geneva drive (Maltese cross): a driver with crank pin and locking disc turns the slotted wheel by 360°/z per turn
    /// and locks it in between. Driver pivot O1 = (0,0), wheel pivot O2 = (C,0); crank radius a = C·sin(180°/z), slot mouth
    /// at R = C·cos(180°/z) (the pin enters and leaves the slots radially, without shock). Three links (frame, driver,
    /// wheel), two revolute pairs and one higher pair (pin in slot): F = 1.
    /// </summary>
    internal sealed class GenevaType : MechanismType
    {
        public GenevaType() : base("kin.mech.geneva", new I18nText("Geneva drive", "Malteserkreuzgetriebe"), KinCategories.Mechanisms, new Size2(64, 40),
            "geneva", "geneva drive", "maltese cross", "indexing", "intermittent", "malteserkreuz", "genfer trieb", "schrittgetriebe", "schaltgetriebe", "malteser")
        { }

        public override double DefaultAngle => -20;
        public override (int Links, int Lower, int Higher) Topology => (3, 2, 1);
        /// <summary>Ghosts of a wheel would cover the drawing: no ghost positions or motion path (animation frames stay).</summary>
        protected override bool HasMotionOptions => false;

        private static readonly ParamDef SlotsParam = KP.Choice("slots", "Number of slots", "Schlitzanzahl", "4", ("4", "4 slots", "4 Schlitze"), ("6", "6 slots", "6 Schlitze"));

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Angle(DefaultAngle),
            SlotsParam,
            KP.Num("centre", "Centre distance O1-O2", "Achsabstand O1-O2", 30, 0.1, 1e5),
            KP.Dof,
        };

        /// <summary>Proportions of the drive in model mm.</summary>
        internal readonly record struct Shape(int Z, double C, double A, double R, double Pin, double Slot, double Rw, double Lock);

        internal static Shape ShapeOf(SketchObject o)
        {
            var z = o.Str("slots", "4") == "6" ? 6 : 4;
            var c = Len(o, "centre", 30);
            var a = c * Math.Sin(Math.PI / z);
            var r = c * Math.Cos(Math.PI / z);
            var pin = 0.12 * a;
            var slot = 1.15 * pin;
            var rw = Math.Sqrt(r * r + slot * slot);
            return new Shape(z, c, a, r, pin, slot, rw, a - 1.8 * pin);
        }

        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O1", "A") ? (p["O1"], p["A"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            var sh = ShapeOf(o);
            var p = new Pose(true);
            p["O1"] = Vec2.Zero;
            p["O2"] = new Vec2(sh.C, 0);
            p["A"] = Linkages.Dir(angle) * sh.A;
            p.Trace = p["A"];
            return p;
        }

        protected override IEnumerable<Vec2> FixedPoints(SketchObject o)
        {
            var sh = ShapeOf(o);
            var rd = Math.Max(sh.A + sh.Pin, sh.Lock);
            yield return new Vec2(-rd, -rd);
            yield return new Vec2(rd, rd);
            yield return new Vec2(sh.C - sh.Rw, -sh.Rw);
            yield return new Vec2(sh.C + sh.Rw, sh.Rw);
        }

        /// <summary>
        /// Wheel outline in model mm at orientation <paramref name="wDeg"/>: z slots (parallel sides, round bottom where the
        /// pin is deepest), between them concave locking arcs (radius of the locking disc, centred where the driver is when
        /// the wheel is locked) and short horns on the outer circle.
        /// </summary>
        internal static List<Vec2> WheelOutline(Shape sh, double wDeg)
        {
            var pts = new List<Vec2>();
            var step = 2 * Math.PI / sh.Z;
            var eps = Math.Asin(Math.Min(1, sh.Slot / sh.Rw));
            var lockR = sh.Lock + 0.03 * sh.A;
            var cosD = (sh.Rw * sh.Rw + sh.C * sh.C - lockR * lockR) / (2 * sh.Rw * sh.C);
            var delta = Math.Acos(Math.Max(-1, Math.Min(1, cosD)));
            delta = Math.Min(delta, step / 2 - eps - 1e-3);
            void Outer(double from, double to)
            {
                if (to <= from) return;
                var n = Math.Max(1, (int)Math.Ceiling((to - from) / (Math.PI / 36)));
                for (var i = 0; i <= n; i++) pts.Add(Vec2.Polar(sh.Rw, from + (to - from) * i / n));
            }
            for (var k = 0; k < sh.Z; k++)
            {
                var sigma = Math.PI + k * step;
                var e = Vec2.Polar(1, sigma);
                var n = e.Perp; // = direction sigma + 90°
                var bottom = e * (sh.C - sh.A);
                // Slot: outer edge at sigma − eps, down, round bottom through −e, up to the edge at sigma + eps.
                pts.Add(e * sh.R - n * sh.Slot);
                for (var i = 0; i <= 8; i++)
                {
                    var a = sigma - Math.PI / 2 - Math.PI * i / 8;
                    pts.Add(bottom + Vec2.Polar(sh.Slot, a));
                }
                pts.Add(e * sh.R + n * sh.Slot);
                // Sector up to the next slot: horn, concave locking arc, horn.
                var mu = sigma + step / 2;
                Outer(sigma + eps, mu - delta);
                var q = Vec2.Polar(sh.C, mu);
                var x1 = Vec2.Polar(sh.Rw, mu - delta);
                var x2 = Vec2.Polar(sh.Rw, mu + delta);
                var b1 = Math.Atan2(x1.Y - q.Y, x1.X - q.X);
                var b2 = Math.Atan2(x2.Y - q.Y, x2.X - q.X);
                var sweep = Wrap(b2 - b1);
                if (Math.Abs(Wrap(b1 + sweep / 2 - (mu + Math.PI))) > Math.PI / 2) sweep -= Math.Sign(sweep) * 2 * Math.PI;
                for (var i = 1; i < 12; i++) pts.Add(q + Vec2.Polar(lockR, b1 + sweep * i / 12));
                Outer(mu + delta, sigma + step - eps);
            }
            var w = Geo.DegToRad(wDeg);
            var c = Math.Cos(w); var s = Math.Sin(w);
            return pts.Select(v => new Vec2(sh.C + v.X * c - v.Y * s, v.X * s + v.Y * c)).ToList();
        }

        /// <summary>Locking disc of the driver at angle φ: a circle with a relief (radius of the wheel) facing the pin.</summary>
        internal static List<Vec2> DiscOutline(Shape sh, double angleDeg)
        {
            var t = Geo.DegToRad(angleDeg);
            var relief = sh.Rw + 0.03 * sh.A;
            var cosD = (sh.Lock * sh.Lock + sh.C * sh.C - relief * relief) / (2 * sh.Lock * sh.C);
            if (cosD >= 1 || cosD <= -1) return Geo.ArcPoints(Vec2.Zero, sh.Lock, sh.Lock, 0, 2 * Math.PI, 72).Take(72).ToList();
            var dr = Math.Acos(cosD);
            var pts = Geo.ArcPoints(Vec2.Zero, sh.Lock, sh.Lock, t + dr, 2 * Math.PI - 2 * dr, 64);
            var v = Vec2.Polar(sh.C, t);
            var x1 = pts[pts.Count - 1];
            var x2 = pts[0];
            var b1 = Math.Atan2(x1.Y - v.Y, x1.X - v.X);
            var b2 = Math.Atan2(x2.Y - v.Y, x2.X - v.X);
            var sweep = Wrap(b2 - b1);
            if (Math.Abs(Wrap(b1 + sweep / 2 - (t + Math.PI))) > Math.PI / 2) sweep -= Math.Sign(sweep) * 2 * Math.PI;
            for (var i = 1; i < 16; i++) pts.Add(v + Vec2.Polar(relief, b1 + sweep * i / 16));
            return pts;
        }

        private static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            s.GroundPivot(new Vec2(ShapeOf(o).C, 0), new Vec2(0, 1));
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            var sh = ShapeOf(o);
            var angle = Geo.RadToDeg(Math.Atan2(p["A"].Y, p["A"].X));
            var w = Linkages.GenevaWheelAngle(sh.Z, sh.C, angle);
            s.Add(Prim.Polygon(WheelOutline(sh, w).Select(s.L), KS.Body), KinTags.Wheel);
            s.Add(Prim.Polygon(DiscOutline(sh, angle).Select(s.L), s.Detailed ? KS.Body : KS.Link), KinTags.Disc);
            s.Bar(p["O1"], p["A"]);
            var a = s.L(p["A"]);
            s.Add(Prim.Circle(a.X, a.Y, sh.Pin * s.S, KS.Joint), KinTags.Pin);
            if (s.Detailed)
            {
                foreach (var c in new[] { p["O1"], p["O2"] })
                {
                    var l = s.L(c);
                    var hub = 0.18 * sh.A * s.S;
                    s.Add(Prim.Circle(l.X, l.Y, hub, KS.Thin), "hub");
                    s.Add(Prim.Line(l - new Vec2(1.6 * hub, 0), l + new Vec2(1.6 * hub, 0), KS.Ctr), "center");
                    s.Add(Prim.Line(l - new Vec2(0, 1.6 * hub), l + new Vec2(0, 1.6 * hub), KS.Ctr), "center");
                }
            }
            s.Joint(p["O1"]);
            s.Joint(p["O2"]);
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O1"], "O1");
            yield return Mount(s, p["O2"], "O2");
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O1"]), "hub1");
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O2"]), "hub2");
            foreach (var k in new[] { "O1", "O2", "A" }) yield return Port(s, p[k], k);
        }

        protected override IEnumerable<ContextAction> ExtraActions(SketchObject o)
            => ActionChoices.FromParam(o, "kin.slots", SlotsParam, null, "param", null);
    }

    /// <summary>
    /// Ratchet and pawl (Klinkengesperre): a saw-tooth wheel on a fixed pivot and a pawl on a second fixed pivot, held on
    /// the wheel by a spring. The wheel turns clockwise freely (the pawl rides up the flanks and drops behind each tooth);
    /// turning back, the steep tooth face runs against the pawl tip and locks. "angle" is the wheel angle; the pawl rests
    /// at its first contact with the actual tooth outline. Three links, two revolute pairs, one higher pair (pawl tip on
    /// the teeth): F = 1.
    /// </summary>
    internal sealed class RatchetType : MechanismType
    {
        public RatchetType() : base("kin.ratchet", new I18nText("Ratchet and pawl", "Klinkengesperre"), KinCategories.Mechanisms, new Size2(36, 40),
            "ratchet", "pawl", "ratchet wheel", "click", "freewheel", "klinkengesperre", "sperrklinke", "klinke", "sperrrad", "gesperre", "klinkenrad")
        { }

        public override double DefaultAngle => 0;
        public override (int Links, int Lower, int Higher) Topology => (3, 2, 1);
        protected override bool HasMotionOptions => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            KP.Angle(DefaultAngle),
            KP.Int("teeth", "Number of teeth", "Zähnezahl", 12, 6, 36),
            KP.Bool("spring", "Show pawl spring", "Klinkenfeder zeigen", true),
            KP.Dof,
        };

        /// <summary>Tip radius of the wheel (model mm); the drawing is scaled into the box.</summary>
        internal const double Tip = 10;
        /// <summary>Fixed pivot of the pawl (model mm).</summary>
        internal static readonly Vec2 PawlPivot = new(-11.5, -13);
        /// <summary>Where the pawl tip would be without teeth (on the tip circle, upper left of the wheel).</summary>
        private static readonly Vec2 TipRest = Vec2.Polar(Tip, Geo.DegToRad(-105));
        internal static readonly double PawlLength = (TipRest - PawlPivot).Length;
        private static readonly Vec2 SpringAnchor = PawlPivot + (TipRest - PawlPivot) * 0.55 + new Vec2(0, -0.5 * Tip);

        private static int Teeth(SketchObject o) => Math.Max(6, Math.Min(36, o.Int("teeth", 12)));

        /// <summary>Root radius: the tooth depth shrinks with more teeth.</summary>
        internal static double Root(int z) => Tip * (1 - Geo.Clamp(2.4 / z, 0.08, 0.25));

        /// <summary>Tooth outline at wheel angle φ: per tooth the tip, the flank down to the root, then the steep radial face.</summary>
        internal static List<Vec2> Outline(int z, double angleDeg)
        {
            var pts = new List<Vec2>(2 * z);
            var p = 2 * Math.PI / z;
            var t = Geo.DegToRad(angleDeg);
            var rr = Root(z);
            for (var k = 0; k < z; k++)
            {
                pts.Add(Vec2.Polar(Tip, t + k * p));
                pts.Add(Vec2.Polar(rr, t + (k + 1) * p));
            }
            return pts;
        }

        /// <summary>The pawl tip at its first contact with the outline (swinging from the lifted side towards the wheel).</summary>
        internal static Vec2 PawlTip(IReadOnlyList<Vec2> outline)
        {
            var q = PawlPivot;
            var l = PawlLength;
            var rest = Math.Atan2(TipRest.Y - q.Y, TipRest.X - q.X);
            double best = double.MaxValue;
            Vec2? at = null;
            for (var i = 0; i < outline.Count; i++)
            {
                var p0 = outline[i]; var d = outline[(i + 1) % outline.Count] - p0;
                var f = p0 - q;
                double aa = d.Dot(d), bb = 2 * f.Dot(d), cc = f.Dot(f) - l * l;
                var disc = bb * bb - 4 * aa * cc;
                if (aa < 1e-12 || disc < 0) continue;
                var sq = Math.Sqrt(disc);
                foreach (var tt in new[] { (-bb - sq) / (2 * aa), (-bb + sq) / (2 * aa) })
                {
                    if (tt < -1e-9 || tt > 1 + 1e-9) continue;
                    var x = p0 + d * tt;
                    var delta = Math.Atan2(x.Y - q.Y, x.X - q.X) - rest;
                    while (delta > Math.PI) delta -= 2 * Math.PI;
                    while (delta <= -Math.PI) delta += 2 * Math.PI;
                    if (Math.Abs(delta) > Geo.DegToRad(50) || delta >= best) continue;
                    best = delta; at = x;
                }
            }
            return at ?? TipRest;
        }

        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O", "N") ? (p["O"], p["N"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            var outline = Outline(Teeth(o), angle);
            var tip = PawlTip(outline);
            var p = new Pose(true);
            p["O"] = Vec2.Zero;
            p["Q"] = PawlPivot;
            p["T"] = tip;
            p["N"] = Vec2.Polar(Tip, Geo.DegToRad(angle));
            p["M"] = PawlPivot + (tip - PawlPivot) * 0.55;
            p.Trace = tip;
            return p;
        }

        protected override IEnumerable<Vec2> FixedPoints(SketchObject o)
        {
            yield return new Vec2(-Tip, -Tip);
            yield return new Vec2(Tip, Tip);
            yield return PawlPivot;
            yield return SpringAnchor;
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            s.GroundPivot(PawlPivot, new Vec2(-0.7, -1));
            if (o.Flag("spring", true))
            {
                var a = s.L(SpringAnchor);
                var half = 2.2 * s.R;
                var target = s.Items;
                target.Add(Prim.Line(a - new Vec2(half, 0), a + new Vec2(half, 0), KS.Link) with { Tag = KinTags.Ground });
                Frag.GroundStrokes(target, a - new Vec2(half, 0), a + new Vec2(half, 0), new Vec2(0, -1), 1.2 * s.R, Frag.HatchSpacing(1.2 * s.R));
            }
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            var angle = AngleOf(o);
            s.Add(Prim.Polygon(Outline(Teeth(o), angle).Select(s.L), KS.Body), KinTags.Wheel);
            if (s.Detailed)
            {
                var c = s.L(p["O"]);
                s.Add(Prim.Circle(c.X, c.Y, 0.25 * Tip * s.S, KS.Thin), "hub");
            }
            // Pawl: a bar from its pivot with a wedge-shaped nose that sits on the teeth.
            var q = s.L(p["Q"]); var t = s.L(p["T"]);
            var u = (t - q).Normalized();
            var v = u.Perp;
            var nose = Math.Min(2.4 * s.R, 0.45 * (t - q).Length);
            var hw = s.Detailed ? 1.1 * s.R : 0.8 * s.R;
            if (s.Detailed)
                s.Add(Prim.Polygon(new[] { q - v * hw, t - u * nose - v * hw, t, t - u * nose + v * (0.3 * hw), q + v * hw }, KS.Body), KinTags.Pawl);
            else
            {
                s.Add(Prim.Line(q, t - u * nose, KS.Link), KinTags.Pawl);
                s.Add(Prim.Polygon(new[] { t - u * nose - v * hw, t, t - u * nose + v * hw }, KS.Body), KinTags.Pawl);
            }
            if (o.Flag("spring", true))
            {
                var a = s.L(SpringAnchor); var m = s.L(p["M"]);
                s.Add(Prim.Polyline(Frag.Zigzag(a, m, 4, 0.9 * s.R, 0.2), KS.Thin with { StrokeWidth = 0.3 }), KinTags.Element);
            }
            s.Joint(p["O"]);
            s.Joint(p["Q"]);
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O"], "O");
            yield return Mount(s, p["Q"], "Q");
            yield return Anchor.Point(AnchorRole.Hub, s.L(p["O"]), "hub");
            yield return Port(s, p["O"], "O");
            yield return Port(s, p["Q"], "Q");
        }
    }

    /// <summary>
    /// Six-bar linkage with one degree of freedom (6 links, 7 revolute pairs). Watt type: two four-bars in series, the
    /// rocker of the first is a ternary link (O4, B, C) that drives the second four-bar (O4, C, D, O6). Stephenson type: a
    /// dyad P–C–O6 hangs on the coupler point P of the four-bar (ternary coupler A, B, P). Loop closure by circle
    /// intersections; the input is the crank O2-A.
    /// </summary>
    internal sealed class SixBarType : MechanismType
    {
        public SixBarType() : base("kin.mech.sixbar", new I18nText("Six-bar linkage", "Sechsgliedriges Koppelgetriebe"), KinCategories.Mechanisms, new Size2(72, 46),
            "six-bar", "sixbar", "watt", "stephenson", "linkage", "sechsgliedrig", "sechsgelenk", "koppelgetriebe", "wattsche kette", "stephensonsche kette")
        { }

        public override double DefaultAngle => -60;
        public override (int Links, int Lower, int Higher) Topology => (6, 7, 0);

        private static readonly ParamDef KindParam = KP.Choice("kind", "Chain", "Kette", "watt", ("watt", "Watt", "Watt"), ("stephenson", "Stephenson", "Stephenson"));

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KindParam,
            KP.Num("ground", "Frame length O2-O4", "Gestelllänge O2-O4", 40, 0.1, 1e5),
            KP.Num("crank", "Crank length", "Kurbellänge", 12, 0.1, 1e5),
            KP.Num("coupler", "Coupler length", "Koppellänge", 36, 0.1, 1e5),
            KP.Num("rocker", "Rocker length", "Schwingenlänge", 30, 0.1, 1e5),
            KP.Num("arm", "Watt: second arm O4-C of the rocker", "Watt: zweiter Arm O4-C der Schwinge", 24, 0.1, 1e5, advanced: true),
            KP.Num("armAngle", "Watt: angle between the rocker arms", "Watt: Winkel zwischen den Schwingenarmen", 60, -360, 360, "°", 5, advanced: true),
            KP.Num("ground2", "Watt: frame length O4-O6", "Watt: Gestelllänge O4-O6", 36, 0.1, 1e5, advanced: true),
            KP.Num("coupler2", "Watt: second coupler C-D", "Watt: zweite Koppel C-D", 28, 0.1, 1e5, advanced: true),
            KP.Num("rocker2", "Watt: output rocker O6-D", "Watt: Abtriebsschwinge O6-D", 22, 0.1, 1e5, advanced: true),
            KP.Num("cpu", "Stephenson: coupler point along coupler", "Stephenson: Koppelpunkt längs Koppel", 0.5, -2, 3, null, 0.05, advanced: true),
            KP.Num("cpv", "Stephenson: coupler point offset (× coupler)", "Stephenson: Koppelpunkt-Versatz (× Koppel)", -0.3, -3, 3, null, 0.05, advanced: true),
            KP.Num("link5", "Stephenson: link P-C", "Stephenson: Glied P-C", 40, 0.1, 1e5, advanced: true),
            KP.Num("link6", "Stephenson: output rocker O6-C", "Stephenson: Abtriebsschwinge O6-C", 22, 0.1, 1e5, advanced: true),
            KP.Num("x6", "Stephenson: pivot O6, x", "Stephenson: Drehpunkt O6, x", 62, -1e5, 1e5, advanced: true),
            KP.Num("y6", "Stephenson: pivot O6, y", "Stephenson: Drehpunkt O6, y", -6, -1e5, 1e5, advanced: true),
        }).ToList();

        private static bool Watt(SketchObject o) => o.Str("kind", "watt") != "stephenson";

        private static Vec2 O6(SketchObject o)
            => Watt(o) ? new Vec2(Len(o, "ground", 40) + Len(o, "ground2", 36), 0) : new Vec2(o.Num("x6", 62), o.Num("y6", -6));

        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("O2", "A") ? (p["O2"], p["A"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            double d = Len(o, "ground", 40), a = Len(o, "crank", 12), b = Len(o, "coupler", 36), c = Len(o, "rocker", 30);
            var fb = Linkages.FourBar(d, a, b, c, angle, false);
            var o6 = O6(o);
            Vec2? last = null;
            Vec2 cPoint = default, pPoint = default;
            if (fb is { } f)
            {
                if (Watt(o))
                {
                    var t4 = Math.Atan2(f.B.Y - f.O4.Y, f.B.X - f.O4.X) + Geo.DegToRad(o.Num("armAngle", 60));
                    cPoint = f.O4 + Vec2.Polar(Len(o, "arm", 24), t4);
                    last = Linkages.CircleCircle(cPoint, Len(o, "coupler2", 28), o6, Len(o, "rocker2", 22), 1);
                }
                else
                {
                    var ab = f.B - f.A;
                    pPoint = f.A + ab * o.Num("cpu", 0.5) - ab.Perp * o.Num("cpv", -0.3);
                    last = Linkages.CircleCircle(pPoint, Len(o, "link5", 40), o6, Len(o, "link6", 22), -1);
                }
            }
            var p = new Pose(fb is not null && last is not null);
            p["O2"] = Vec2.Zero;
            p["O4"] = new Vec2(d, 0);
            p["O6"] = o6;
            p["A"] = Linkages.Dir(angle) * a;
            if (fb is null) return p;
            p["B"] = fb.Value.B;
            if (Watt(o))
            {
                p["C"] = cPoint;
                if (last is { } dd) { p["D"] = dd; p.Trace = dd; }
            }
            else
            {
                p["P"] = pPoint;
                p.Trace = pPoint;
                if (last is { } cc) p["C"] = cc;
            }
            return p;
        }

        protected override IEnumerable<Vec2> FixedPoints(SketchObject o)
        {
            yield return Vec2.Zero;
            yield return new Vec2(Len(o, "ground", 40), 0);
            yield return O6(o);
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            s.GroundPivot(new Vec2(Len(o, "ground", 40), 0), new Vec2(0, 1));
            s.GroundPivot(O6(o), new Vec2(0, 1));
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            s.Bar(p["O2"], p["A"]);
            if (p.Has("B"))
            {
                if (Watt(o))
                {
                    s.Bar(p["A"], p["B"]);
                    s.Plate(p["O4"], p["B"], p["C"]);
                    if (p.Has("D")) { s.Bar(p["C"], p["D"]); s.Bar(p["O6"], p["D"]); }
                }
                else
                {
                    s.Plate(p["A"], p["B"], p["P"]);
                    s.Bar(p["O4"], p["B"]);
                    if (p.Has("C")) { s.Bar(p["P"], p["C"]); s.Bar(p["O6"], p["C"]); }
                }
            }
            if (!s.Ghost) { s.Joint(p["O2"]); s.Joint(p["O4"]); s.Joint(p["O6"]); }
            foreach (var k in new[] { "A", "B", "C", "D", "P" })
                if (p.Has(k)) s.Joint(p[k]);
        }

        /// <summary>
        /// Pivots O2, O4, O6 and the links docked onto each other. Watt: crank, coupler, ternary rocker (O4, B, C), second
        /// coupler, output rocker. Stephenson: crank, ternary coupler (A, B, P), rocker, link P-C, output rocker.
        /// </summary>
        protected override IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s)
        {
            if (!p.Ok) return null;
            if (Watt(o))
                return new[]
                {
                    PivotPart(s, p["O2"]),                                                      // 0
                    PivotPart(s, p["O4"]),                                                      // 1
                    PivotPart(s, p["O6"]),                                                      // 2
                    LinkPart(s, p["O2"], p["A"], new PartDock(0, "c"), null),                  // 3 crank
                    LinkPart(s, p["A"], p["B"], new PartDock(3, "j1"), null),                  // 4 coupler
                    Ternary(s, p["O4"], p["B"], p["C"], new PartDock(1, "c"), new PartDock(4, "j1"), null), // 5 ternary rocker
                    LinkPart(s, p["C"], p["D"], new PartDock(5, "j2"), null),                  // 6 second coupler
                    LinkPart(s, p["O6"], p["D"], new PartDock(2, "c"), new PartDock(6, "j1")), // 7 output rocker
                };
            return new[]
            {
                PivotPart(s, p["O2"]),                                                          // 0
                PivotPart(s, p["O4"]),                                                          // 1
                PivotPart(s, p["O6"]),                                                          // 2
                LinkPart(s, p["O2"], p["A"], new PartDock(0, "c"), null),                      // 3 crank
                Ternary(s, p["A"], p["B"], p["P"], new PartDock(3, "j1"), null, null),          // 4 ternary coupler
                LinkPart(s, p["O4"], p["B"], new PartDock(1, "c"), new PartDock(4, "j1")),     // 5 rocker
                LinkPart(s, p["P"], p["C"], new PartDock(4, "j2"), null),                      // 6 link P-C
                LinkPart(s, p["O6"], p["C"], new PartDock(2, "c"), new PartDock(6, "j1")),     // 7 output rocker
            };
        }

        private static PartSpec Ternary(Scene s, Vec2 a, Vec2 b, Vec2 c, PartDock? atA, PartDock? atB, PartDock? atC)
            => new("kin.link.ternary", null, Vec2.Zero) { Points = new[] { s.L(a), s.L(b), s.L(c) }, Docks = new[] { atA, atB, atC } };

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O2"], "O2");
            yield return Mount(s, p["O4"], "O4");
            yield return Mount(s, p["O6"], "O6");
            foreach (var k in new[] { "O2", "A", "B", "O4", "C", "D", "P", "O6" })
                if (p.Has(k)) yield return Port(s, p[k], k);
        }

        protected override IEnumerable<ContextAction> ExtraActions(SketchObject o)
            => ActionChoices.FromParam(o, "kin.sixbar.kind", KindParam, null, "param", null);
    }

    /// <summary>
    /// Pantograph (Storchschnabel) for copying at scale k: fixed pivot O, long bars O–B and B–P (length l), short bars C–T
    /// and T–D (length l/k) forming the parallelogram C, B, D, T. O, the tracer T and the pen P stay on one line with
    /// OP = k·OT, so the pen draws the traced figure enlarged k times. Here the tracer runs round a circle (radius "trace",
    /// centre at distance "reach" from O); "angle" is its position on that circle. 5 links, 5 revolute pairs: F = 2 (the
    /// tracer moves freely in the plane).
    /// </summary>
    internal sealed class PantographType : MechanismType
    {
        public PantographType() : base("kin.mech.pantograph", new I18nText("Pantograph", "Storchschnabel (Pantograph)"), KinCategories.Mechanisms, new Size2(64, 44),
            "pantograph", "scaling linkage", "copying", "parallelogram", "storchschnabel", "pantograf", "pantograph", "vergrößerung", "parallelogrammgetriebe")
        { }

        public override double DefaultAngle => -90;
        public override (int Links, int Lower, int Higher) Topology => (5, 5, 0);

        public override IReadOnlyList<ParamDef> Parameters => MotionParams(DefaultAngle).Concat(new[]
        {
            KP.Num("length", "Long bar length", "Länge der langen Stäbe", 40, 0.1, 1e5),
            KP.Num("scale", "Scale factor k", "Maßstabsfaktor k", 2, 1.1, 10, null, 0.1),
            KP.Num("reach", "Distance O to the traced figure", "Abstand O zur Vorlage", 20, 0, 1e5),
            KP.Num("trace", "Radius of the traced circle", "Radius des abgefahrenen Kreises", 5, 0, 1e5),
        }).ToList();

        private static double Scale(SketchObject o) => Geo.Clamp(o.Num("scale", 2), 1.1, 10);

        private static Vec2 TraceCentre(SketchObject o) => new(Math.Max(0, o.Num("reach", 20)), 0);

        protected override (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => p.Has("T") ? (TraceCentre(o), p["T"]) : null;

        public override Pose Solve(SketchObject o, double angle)
        {
            var t = TraceCentre(o) + Linkages.Dir(angle) * Math.Max(0, o.Num("trace", 5));
            var k = Scale(o);
            var sol = Linkages.Pantograph(Len(o, "length", 40), k, t);
            var p = new Pose(sol is not null);
            p["O"] = Vec2.Zero;
            p["T"] = t;
            if (sol is not { } q) return p;
            p["C"] = q.C; p["B"] = q.B; p["D"] = q.D; p["P"] = q.P;
            p.Trace = q.P;
            return p;
        }

        protected override IEnumerable<Vec2> FixedPoints(SketchObject o)
        {
            yield return Vec2.Zero;
        }

        protected override void DrawFixed(Scene s, SketchObject o, Rect model)
        {
            s.GroundPivot(Vec2.Zero, new Vec2(0, 1));
            if (!o.Flag("path", false)) return;
            // The traced figure (the pen's enlarged copy is the motion path).
            var c = s.L(TraceCentre(o));
            var r = Math.Max(0, o.Num("trace", 5)) * s.S;
            if (r > 1e-6) s.Add(Prim.Circle(c.X, c.Y, r, KS.Trace), KinTags.Path);
        }

        protected override void DrawMoving(Scene s, SketchObject o, Pose p)
        {
            if (p.Has("B"))
            {
                s.Bar(p["O"], p["B"]);
                s.Bar(p["B"], p["P"]);
                s.Bar(p["C"], p["T"]);
                s.Bar(p["T"], p["D"]);
            }
            if (!s.Ghost) s.Joint(p["O"]);
            foreach (var k in new[] { "C", "B", "D", "T" })
                if (p.Has(k)) s.Joint(p[k]);
            if (p.Has("P") && !s.Ghost)
            {
                // Pen: a small filled point with a short holder line.
                var pen = s.L(p["P"]);
                s.Add(Prim.Circle(pen.X, pen.Y, 0.55 * s.R, KS.Black), KinTags.Pen);
            }
        }

        protected override IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model)
        {
            yield return Mount(s, p["O"], "O");
            foreach (var k in new[] { "O", "C", "B", "D", "T", "P" })
                if (p.Has(k)) yield return Port(s, p[k], k);
        }
    }
}
