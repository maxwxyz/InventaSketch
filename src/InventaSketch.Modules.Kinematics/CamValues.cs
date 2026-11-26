using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Element values of the cam with follower (engineering-calculations.md section 2.6): base circle r₀ and lift h are the
    /// element's own parameters, the cam angle φ its input angle; the follower displacement s, the stroke H, the follower
    /// speed v at the current position (with the input speed n) and the pressure angle α (at the position and its largest
    /// value over a turn) follow from the exact contact of the tangent cam (base circle plus nose circle joined by tangent
    /// flanks) with an in-line translating follower [S24]. Check <c>kin.cam.pressure</c>: α_max above 30°.
    /// <para>
    /// Contact: the roller centre lies on the convex hull of the base and nose circles enlarged by the roller radius
    /// (0.3·r₀), the knife edge on the hull itself, both on the follower axis through the cam centre; the flat face touches
    /// the hull's highest point. s = (distance of the contact from the cam centre) − R_p with the prime circle R_p = r₀ + r_R
    /// (roller), r₀ (knife edge, flat face). tan α = (ds/dφ)/(R_p + s) for roller and knife edge; the flat face is always
    /// pushed along its axis (α = 0).
    /// </para>
    /// </summary>
    internal static class CamValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        private const double Deg = Math.PI / 180;

        /// <summary>Usual upper limit of the pressure angle of a translating follower (degrees) [S24].</summary>
        public const double PressureLimit = 30;

        private static readonly ParamDef PFollowerSpeed = KinValues.Text("followerSpeed", T("Follower speed", "Stößelgeschwindigkeit"), "m/s");

        /// <summary>Optional value parameters (input speed, follower speed) and the values-label switch.</summary>
        public static readonly IReadOnlyList<ParamDef> Params = new[] { KinValues.PInputSpeed, PFollowerSpeed, ValueLabels.ShowValues };

        public static readonly IReadOnlyList<VarDef> Variables = new[]
        {
            KinValues.Length("r0", "base", T("Base circle radius", "Grundkreisradius"), "r₀", 8),
            KinValues.Length("h", "lift", T("Lift", "Hub"), "h", 7),
            KinValues.Phi(0) with { Name = T("Cam angle", "Nockenwinkel") },
            KinValues.InputSpeed(),
            new VarDef("H", T("Stroke of the follower", "Hub des Stößels"), T("H", "H"), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Card, ReadOnly = true, Min = 0, Max = 100 },
            new VarDef("s", T("Follower displacement at this position", "Stößelweg in dieser Stellung"), T("s", "s"), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Card, ReadOnly = true, Min = 0, Max = 100 },
            new VarDef("v", T("Follower speed at this position", "Stößelgeschwindigkeit in dieser Stellung"), T("v", "v"), Quantities.Speed)
                { Unit = "m/s", Param = "followerSpeed", Relevance = VarRelevance.Card, Min = -1e4, Max = 1e4 },
            KinValues.Angle("alphamax", T("Largest pressure angle over a turn", "Größter Druckwinkel über eine Umdrehung"), T("α_max", "α_max"), VarRelevance.Card, Math.PI / 2),
            KinValues.Angle("alpha", T("Pressure angle at this position", "Druckwinkel in dieser Stellung"), T("α", "α"), VarRelevance.Panel, Math.PI / 2),
        };

        // ------------------------------------------------------------------ exact contact of the tangent cam

        /// <summary>Nose circle radius of the tangent cam (as drawn): min(r₀, max(0.45·r₀, r₀ − h/2)).</summary>
        public static double NoseRadius(double r0, double h) => Math.Min(r0, Math.Max(0.45 * r0, r0 - 0.5 * h));

        /// <summary>Roller radius of the roller follower (0.3·r₀), 0 for the other followers.</summary>
        public static double RollerRadius(string follower, double r0) => follower == "roller" ? 0.3 * r0 : 0;

        /// <summary>Prime circle radius R_p: r₀ + r_R for the roller, r₀ for knife edge and flat face.</summary>
        public static double PrimeRadius(string follower, double r0) => r0 + RollerRadius(follower, r0);

        /// <summary>
        /// The follower's contact height on its axis x = 0 above the cam centre at cam angle <paramref name="angleDeg"/> (screen
        /// y, negative = up): roller centre, knife tip or flat face, from the exact hull of base and nose circle.
        /// </summary>
        public static double Contact(string follower, double r0, double h, double angleDeg)
        {
            var rn = NoseRadius(r0, h);
            var n = Vec2.Polar(r0 + h - rn, Geo.DegToRad(angleDeg - 135));
            if (follower == "flat") return -Math.Max(r0, -n.Y + rn);
            var rr = RollerRadius(follower, r0);
            double r1 = r0 + rr, r2 = rn + rr;
            var top = -r1;
            if (Math.Abs(n.X) <= r2) top = Math.Min(top, n.Y - Math.Sqrt(r2 * r2 - n.X * n.X));
            // External tangent segments of the two circles (the cam flanks, offset by the roller).
            var d = n.Length;
            if (d > Math.Abs(r1 - r2) + 1e-12)
            {
                var u = n * (1 / d);
                var c = (r1 - r2) / d;
                var sn = Math.Sqrt(Math.Max(0, 1 - c * c));
                foreach (var sign in new[] { -1.0, 1.0 })
                {
                    var nn = u * c + u.Perp * (sign * sn);
                    var t1 = nn * r1; var t2 = n + nn * r2;
                    if ((t1.X <= 0) == (t2.X <= 0) || Math.Abs(t2.X - t1.X) < 1e-15) continue;
                    var y = t1.Y + (0 - t1.X) * (t2.Y - t1.Y) / (t2.X - t1.X);
                    top = Math.Min(top, y);
                }
            }
            return top;
        }

        /// <summary>Follower displacement s from its lowest position (same unit as r₀, h).</summary>
        public static double Displacement(string follower, double r0, double h, double angleDeg) => -Contact(follower, r0, h, angleDeg) - PrimeRadius(follower, r0);

        /// <summary>ds/dφ (per radian) at <paramref name="angleDeg"/>.</summary>
        public static double Rate(string follower, double r0, double h, double angleDeg)
            => Linkages.Derivatives(a => Displacement(follower, r0, h, a), angleDeg)?.D1 ?? double.NaN;

        /// <summary>Pressure angle (radians, ≥ 0) at <paramref name="angleDeg"/>: atan(|ds/dφ|/(R_p + s)); 0 for the flat face.</summary>
        public static double PressureAngle(string follower, double r0, double h, double angleDeg)
        {
            if (follower == "flat") return 0;
            var rate = Rate(follower, r0, h, angleDeg);
            var s = Displacement(follower, r0, h, angleDeg);
            return Math.Atan(Math.Abs(rate) / (PrimeRadius(follower, r0) + s));
        }

        /// <summary>Largest value of <paramref name="f"/> over a turn (sampled and refined, <see cref="Linkages.Extremes"/>).</summary>
        private static double Max(Func<double, double> f) => Linkages.Extremes(a => f(a) is var v && !double.IsNaN(v) ? v : null)?.Max ?? double.NaN;

        // ------------------------------------------------------------------ relations (one set per follower kind)

        private static readonly I18nText Validity = T("tangent cam (base circle and nose circle), in-line translating follower; derivatives at constant cam speed",
            "Tangentennocken (Grundkreis und Nockenkreis), mittiger Schubstößel; Ableitungen bei konstanter Nockendrehzahl");

        private static IReadOnlyList<Relation> Build(string follower)
        {
            var id = "kin.cam." + follower + ".";
            return new[]
            {
                new Relation(id + "stroke", T("Stroke of the follower", "Hub des Stößels"), "{H} = max s(φ)", "H", "r0", "h") { Source = "S24", Validity = Validity }
                    .Solve("H", "max s(φ)", v => Max(a => Displacement(follower, v("r0"), v("h"), a))),
                new Relation(id + "s", T("Follower displacement", "Stößelweg"), "{s} = s({phi})", "s", "r0", "h", "phi") { Source = "S24", Validity = Validity }
                    .Solve("s", "s({phi})", v => Displacement(follower, v("r0"), v("h"), v("phi") / Deg)),
                new Relation(id + "speed", T("Follower speed", "Stößelgeschwindigkeit"), "{v} = {n}·ds/dφ", "v", "n", "r0", "h", "phi") { Source = "S24", Validity = Validity }
                    .Solve("v", "{n}·ds/dφ", v => v("n") * Rate(follower, v("r0"), v("h"), v("phi") / Deg))
                    .Solve("n", "{v}/(ds/dφ)", v => v("v") / Rate(follower, v("r0"), v("h"), v("phi") / Deg)),
                new Relation(id + "pressure", T("Pressure angle", "Druckwinkel"), "{alpha} = arctan(|ds/dφ|/(R_p + s))", "alpha", "r0", "h", "phi") { Source = "S24", Validity = Validity }
                    .Solve("alpha", "arctan(|ds/dφ|/(R_p + s))", v => PressureAngle(follower, v("r0"), v("h"), v("phi") / Deg)),
                new Relation(id + "pressuremax", T("Largest pressure angle", "Größter Druckwinkel"), "{alphamax} = max arctan(|ds/dφ|/(R_p + s))", "alphamax", "r0", "h") { Source = "S24", Validity = Validity }
                    .Solve("alphamax", "max arctan(|ds/dφ|/(R_p + s))", v => follower == "flat" ? 0 : Max(a => PressureAngle(follower, v("r0"), v("h"), a))),
            };
        }

        private static readonly Dictionary<string, IReadOnlyList<Relation>> ByFollower = new[] { "roller", "flat", "knife" }.ToDictionary(f => f, Build);

        /// <summary>The relations for the follower kind of <paramref name="o"/>.</summary>
        public static IReadOnlyList<Relation> For(string follower) => ByFollower.TryGetValue(follower, out var r) ? r : ByFollower["roller"];

        /// <summary>kin.cam.pressure: the largest pressure angle exceeds 30° [S24].</summary>
        public static readonly ValueCheck Pressure = new(
            "kin.cam.pressure", CheckSeverity.Hint,
            T("The pressure angle between cam and follower exceeds 30° during the turn: the follower is pushed sideways in its guide and may jam. A larger base circle, a smaller lift or a flat-faced follower helps.",
              "Der Druckwinkel zwischen Kurvenscheibe und Stößel überschreitet während der Umdrehung 30°: der Stößel wird seitlich in seine Führung gedrückt und kann klemmen. Ein größerer Grundkreis, ein kleinerer Hub oder ein Flachstößel hilft."),
            "α_max ≤ 30°",
            c => c["alphamax"] is double a && a > PressureLimit * Deg + 1e-9)
        { Source = "S24" };
    }

    /// <summary>
    /// Values and the linked displacement diagram of the cam with follower: s(φ) over a turn of the cam with the pressure
    /// angle in a second panel and its usual limit 30° [S24]. The current position is a point on the curve; dragging it
    /// along the cam angle enters a new angle.
    /// </summary>
    internal sealed partial class CamFollowerType : IValueType, IValueLinks, IValueChecks, ICharacteristics
    {
        public const string DisplacementSet = "displacement";

        private static readonly CharacteristicInfo DisplacementInfo =
            new(DisplacementSet, new I18nText("Displacement diagram", "Hubdiagramm"));

        public IReadOnlyList<VarDef> Variables => CamValues.Variables;

        /// <summary>No shared relations: they depend on the follower kind and come per element (<see cref="ValueLinks"/>).</summary>
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();

        public IEnumerable<ValueCheck> ValueChecks => new[] { CamValues.Pressure };

        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => CamValues.For(Follower(o)).Select(r => BoundRelation.Local(r, o.Id));

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx) => new[] { DisplacementInfo };

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c) => key == DisplacementSet ? Diagram(o) : null;

        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Characteristics.LinkedCharts(ctx.Doc, o.Id);

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx));

        /// <summary>The displacement diagram of <paramref name="o"/> (s in mm, angles in degrees, cam angle 0° … 360°).</summary>
        internal CharacteristicSet Diagram(SketchObject o)
        {
            var f = Follower(o);
            double r0 = Len(o, "base", 8), h = Math.Max(0, o.Num("lift", 7));
            var set = new CharacteristicSet(DisplacementSet, DisplacementInfo.Name,
                new CharacteristicAxis(new I18nText("Cam angle φ", "Nockenwinkel φ"), "°") { Min = 0, Max = 360 },
                new CharacteristicAxis(new I18nText("Displacement s", "Stößelweg s"), "mm"),
                new CharacteristicAxis(new I18nText("Pressure angle α", "Druckwinkel α"), "°") { Min = 0, Max = 90 });
            var s = new List<Vec2>();
            var alpha = new List<Vec2>();
            for (var deg = 0; deg <= 360; deg++)
            {
                s.Add(new Vec2(deg, CamValues.Displacement(f, r0, h, deg)));
                alpha.Add(new Vec2(deg, Geo.RadToDeg(CamValues.PressureAngle(f, r0, h, deg))));
            }
            set.Curves.Add(new CharacteristicCurve("s", new I18nText("Displacement s", "Stößelweg s"), s));
            set.Curves.Add(new CharacteristicCurve("alpha", new I18nText("Pressure angle α", "Druckwinkel α"), alpha) { Panel = 1, Look = CurveLook.Second });
            set.Lines.Add(new CharacteristicLine("alpha.max", new I18nText("α = 30°", "α = 30°"), CamValues.PressureLimit) { Vertical = false, Panel = 1 });

            var angle = KA.NormAngle(AngleOf(o));
            if (angle < 0) angle += 360;
            var at = CamValues.Displacement(f, r0, h, angle);
            var id = o.Id;
            var text = "φ = " + Frag.Deg(angle) + ", s = " + at.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " mm";
            set.Points.Add(new CharacteristicPoint("position", new I18nText("Current position", "Aktuelle Stellung"), angle, at)
            {
                Label = new I18nText(text, text.Replace('.', ',')),
                Motion = PointMotion.AlongX,
                Write = x => new[] { new ValueWrite(id, "phi", Geo.DegToRad(Math.Round(Math.Max(0, Math.Min(360, x))))) },
            });
            set.Points.Add(new CharacteristicPoint("position.alpha", new I18nText("Pressure angle here", "Druckwinkel hier"), angle, Geo.RadToDeg(CamValues.PressureAngle(f, r0, h, angle))) { Panel = 1 });
            if (f == "flat") set.Notes.Add(new I18nText("Flat-faced follower: the pressure angle is 0°", "Flachstößel: der Druckwinkel ist 0°"));
            return set;
        }
    }
}
