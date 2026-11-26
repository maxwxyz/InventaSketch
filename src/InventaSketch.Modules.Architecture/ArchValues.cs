using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Stair with values: storey height H, number of risers n (the drawn steps), riser height h = H/n, tread (going) g from
    /// the drawing at the plan scale, step length 2h + g and pitch α = arctan(h/g). The hint <c>arch.stair.steprule</c> reports
    /// a step length outside 59 … 65 cm (step rule 2h + g ≈ 63 cm) once a storey or riser height is given; the action
    /// "Steps for the step rule" picks the number of steps that comes closest to 63 cm.
    /// </summary>
    internal sealed class StairType : BoxType, IValueType, IValueLinks, IValueChecks
    {
        public const string StoreyParam = "storeyHeight";
        public const string RiserParam = "riser";
        /// <summary>Default storey height in metres (the default wall height, 2750 mm).</summary>
        public const double DefaultStorey = 2.75;
        /// <summary>Target and usual range of the step length 2h + g in metres.</summary>
        public const double StepTarget = 0.63, StepMin = 0.59, StepMax = 0.65;

        internal static readonly string[] ValueKeywords = { "riser", "tread", "going", "step rule", "steigung", "auftritt", "schrittmaß", "schrittmaßregel", "geschosshöhe" };

        private readonly int _steps;
        private readonly VarDef[] _vars;

        public StairType(string id, I18nText name, Size2 size, int steps, DrawFn draw, string[] keywords)
            : base(id, name, ArchCategories.Stairs, size, draw, keywords)
        {
            _steps = steps;
            _vars = new[]
            {
                new VarDef("H", T("Storey height", "Geschosshöhe"), T("H", "H"), Quantities.Length) { Unit = "m", Param = StoreyParam, Default = DefaultStorey, Min = 0.2, Max = 20 },
                new VarDef("h", T("Riser height", "Steigungshöhe"), T("h", "s"), Quantities.Length) { Unit = "cm", Param = RiserParam, Min = 0.01, Max = 1 },
                new VarDef("g", T("Tread (going) from the drawing", "Auftritt aus der Zeichnung"), T("g", "a"), Quantities.Length)
                    { Unit = "cm", Param = "tread", Min = 0.01, Max = 5, ReadOnly = true },
                new VarDef("step", T("Step length (step rule)", "Schrittmaß (Schrittmaßregel)"), T("2h + g", "2s + a"), Quantities.Length)
                    { Unit = "cm", Param = "stepLength", Min = 0.01, Max = 10, ReadOnly = true },
                new VarDef("n", T("Number of risers", "Anzahl Steigungen"), T("n", "n"), Quantities.Count) { Param = "steps", Default = steps, Min = 2, Max = 60, Relevance = VarRelevance.Panel },
                new VarDef("alpha", T("Pitch", "Steigungswinkel"), T("α", "α"), Quantities.Angle) { Unit = "°", Param = "pitch", Min = 0, Max = Math.PI / 2, ReadOnly = true, Relevance = VarRelevance.Panel },
            };
        }

        private static I18nText T(string en, string de) => new(en, de);

        public IReadOnlyList<VarDef> Variables => _vars;
        public IReadOnlyList<Relation> Relations => Rels;

        /// <summary>Riser height h = H/n.</summary>
        private static readonly Relation RiserRelation =
            new Relation("arch.stair.riser", T("Riser height from the storey height", "Steigungshöhe aus der Geschosshöhe"), "{h} = {H}/{n}", "h", "H", "n")
                .Solve("h", "{H}/{n}", v => v("H") / v("n"))
                .Solve("H", "{h}·{n}", v => v("h") * v("n"))
                .Solve("n", "{H}/{h}", v => v("H") / v("h"));

        /// <summary>Step length 2h + g (step rule).</summary>
        private static readonly Relation StepRelation =
            new Relation("arch.stair.steplength", T("Step length", "Schrittmaß"), "{step} = 2·{h} + {g}", "step", "h", "g") { Source = "S42" }
                .Solve("step", "2·{h} + {g}", v => 2 * v("h") + v("g"))
                .Solve("h", "({step} − {g})/2", v => (v("step") - v("g")) / 2)
                .Solve("g", "{step} − 2·{h}", v => v("step") - 2 * v("h"));

        /// <summary>Pitch tan α = h/g.</summary>
        private static readonly Relation PitchRelation =
            new Relation("arch.stair.pitch", T("Pitch of the stair", "Steigungswinkel der Treppe"), "{alpha} = arctan({h}/{g})", "alpha", "h", "g")
                .Solve("alpha", "arctan({h}/{g})", v => Math.Atan2(v("h"), v("g")))
                .Solve("h", "{g}·tan {alpha}", v => v("g") * Math.Tan(v("alpha")));

        private static readonly Relation[] Rels = { RiserRelation, StepRelation, PitchRelation };

        /// <summary>Tread depth (m) of the drawing: drawn tread times the plan scale.</summary>
        internal double TreadMetres(SketchObject o, SketchDocument? doc)
            => Stairs.TreadOnPage(Id, o.Size, Stairs.Steps(o, _steps)) * Arch.ScaleOf(o, doc) / 1000;

        /// <summary>The tread from the drawing: a relation of the stair alone (like the room's area from its outline).</summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            var g = TreadMetres(o, ctx.Doc);
            if (!(g > 0) || double.IsInfinity(g)) yield break;
            var expr = "g_drawn·" + Arch.ScaleOf(o, ctx.Doc).ToString("0.###", CultureInfo.InvariantCulture);
            var rel = new Relation("arch.stair.tread", T("Tread from the drawing at the plan scale", "Auftritt aus der Zeichnung im Planmaßstab"), "{g} = " + expr, "g")
                .Solve("g", expr, _ => g);
            yield return BoundRelation.Local(rel, o.Id);
        }

        public IEnumerable<ValueCheck> ValueChecks => Checks;

        private static readonly ValueCheck[] Checks =
        {
            new ValueCheck("arch.stair.steprule", CheckSeverity.Hint,
                T("The step length 2h + g lies outside 59 … 65 cm (step rule 2h + g ≈ 63 cm): use “Steps for the step rule” or change the run of the stair.",
                  "Das Schrittmaß 2s + a liegt außerhalb von 59 … 65 cm (Schrittmaßregel 2s + a ≈ 63 cm): „Stufen nach Schrittmaßregel“ verwenden oder die Lauflänge ändern."),
                "2h + g ∉ [59 cm; 65 cm]",
                // Only once the user gave a height (entered storey height or riser): the default storey height is a guess.
                c => c["step"] is double s && c.Table.Get(c.Object.Id, "H") is { } h && h.Provenance != Provenance.Default
                     && (s < StepMin - 5e-4 || s > StepMax + 5e-4)) { Source = "S42" },
        };

        /// <summary>The storey height entered for the stair (m), else the default 2.75 m.</summary>
        internal static double StoreyOf(SketchObject o)
        {
            var h = o.Num(StoreyParam, 0);
            return h > 0.1 ? h : DefaultStorey;
        }

        /// <summary>Number of steps (2 … 60) whose step length 2·H/n + g(n) comes closest to 63 cm for the stair as drawn.</summary>
        internal static int BestSteps(string id, SketchObject o, SketchDocument? doc)
        {
            var storey = StoreyOf(o);
            var scale = Arch.ScaleOf(o, doc);
            int best = Stairs.Steps(o, 2);
            var bestErr = double.MaxValue;
            for (var n = 2; n <= 60; n++)
            {
                var g = Stairs.TreadOnPage(id, o.Size, n) * scale / 1000;
                var err = Math.Abs(2 * storey / n + g - StepTarget);
                if (err < bestErr - 1e-12) { bestErr = err; best = n; }
            }
            return best;
        }

        /// <summary>"Steps for the step rule (n)": sets the number of steps that comes closest to 2h + g = 63 cm.</summary>
        internal static ContextAction FitAction(SketchObject o, RenderContext ctx, string id, int def)
        {
            var n = BestSteps(id, o, ctx.Doc);
            var label = new I18nText("Steps for the step rule (" + n + ")", "Stufen nach Schrittmaßregel (" + n + ")");
            return new ContextAction("arch.stair.fit", label, IconIds.Formula, x => x.WithParam("steps", n))
                { Group = "steps", IsEnabled = n != o.Int("steps", def) };
        }
    }

    /// <summary>
    /// Ramp with values: length l from the drawing (longer box side at the plan scale), slope s (the drawn "6 %"), rise
    /// Δh = s·l and angle α = arctan s. The hint <c>arch.ramp.slope</c> reports a slope above 6 %, a common limit for ramps
    /// used by wheelchairs (national rules differ).
    /// </summary>
    internal sealed class RampType : BoxType, IValueType, IValueLinks, IValueChecks
    {
        public RampType(string id, I18nText name, string category, Size2 size, DrawFn draw, params string[] keywords)
            : base(id, name, category, size, draw, keywords) { }

        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Slope above which the hint fires (6 %).</summary>
        public const double SlopeLimit = 0.06;

        private static readonly VarDef[] Vars =
        {
            new("l", T("Ramp length from the drawing", "Rampenlänge aus der Zeichnung"), T("l", "l"), Quantities.Length) { Unit = "m", Param = "length", Min = 0.01, Max = 1000, ReadOnly = true },
            new("s", T("Slope", "Steigung"), T("s", "s"), Quantities.Ratio) { Unit = "%", Param = "slope", StoredUnit = "%", Min = 0, Max = 1 },
            new("dh", T("Rise", "Höhenunterschied"), T("Δh", "Δh"), Quantities.Length) { Unit = "m", Param = "rise", Min = 0, Max = 100, ReadOnly = true },
            new("alpha", T("Angle of the slope", "Neigungswinkel"), T("α", "α"), Quantities.Angle) { Unit = "°", Param = "angle", Min = 0, Max = Math.PI / 2, ReadOnly = true, Relevance = VarRelevance.Panel },
        };

        private static readonly Relation RiseRelation =
            new Relation("arch.ramp.rise", T("Rise of the ramp", "Höhenunterschied der Rampe"), "{dh} = {s}·{l}", "dh", "s", "l")
                .Solve("dh", "{s}·{l}", v => v("s") * v("l"))
                .Solve("s", "{dh}/{l}", v => v("dh") / v("l"));

        private static readonly Relation AngleRelation =
            new Relation("arch.ramp.angle", T("Angle of the slope", "Neigungswinkel"), "{alpha} = arctan {s}", "alpha", "s")
                .Solve("alpha", "arctan {s}", v => Math.Atan(v("s")))
                .Solve("s", "tan {alpha}", v => Math.Tan(v("alpha")));

        private static readonly Relation[] Rels = { RiseRelation, AngleRelation };

        public IReadOnlyList<VarDef> Variables => Vars;
        public IReadOnlyList<Relation> Relations => Rels;

        /// <summary>Real length (m) of the ramp: the longer box side at the plan scale.</summary>
        internal static double LengthMetres(SketchObject o, SketchDocument? doc) => Math.Max(o.Size.W, o.Size.H) * Arch.ScaleOf(o, doc) / 1000;

        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            var l = LengthMetres(o, ctx.Doc);
            if (!(l > 0) || double.IsInfinity(l)) yield break;
            var expr = "l_drawn·" + Arch.ScaleOf(o, ctx.Doc).ToString("0.###", CultureInfo.InvariantCulture);
            var rel = new Relation("arch.ramp.length", T("Ramp length from the drawing at the plan scale", "Rampenlänge aus der Zeichnung im Planmaßstab"), "{l} = " + expr, "l")
                .Solve("l", expr, _ => l);
            yield return BoundRelation.Local(rel, o.Id);
        }

        public IEnumerable<ValueCheck> ValueChecks => Checks;

        private static readonly ValueCheck[] Checks =
        {
            new ValueCheck("arch.ramp.slope", CheckSeverity.Hint,
                T("The slope is above 6 %, a common limit for ramps used by wheelchairs (check the national rules; longer ramps need landings).",
                  "Die Steigung liegt über 6 %, einer üblichen Grenze für Rampen, die mit dem Rollstuhl befahren werden (nationale Regeln prüfen; lange Rampen brauchen Zwischenpodeste)."),
                "s > 6 %", c => c["s"] is double s && s > SlopeLimit + 1e-9),
        };
    }

    /// <summary>
    /// Wall values: length l from the centre line at the plan scale, height h (the elevation height parameter), gross area
    /// A = l·h, the area of the doors and windows sitting in the wall A_open (width at the plan scale times the height of
    /// their elevation), net area A_net = A − A_open, U-value, temperature difference Δθ and the transmission heat loss
    /// Φ_T = U·A_net·Δθ (steady state, no thermal bridges; not a heat load calculation to EN 12831).
    /// </summary>
    internal static class WallValues
    {
        public const string UParam = "uValue", DeltaParam = "deltaT", LossParam = "heatLoss";

        private static I18nText T(string en, string de) => new(en, de);

        internal static readonly VarDef[] Vars =
        {
            new("l", T("Wall length (centre line)", "Wandlänge (Achse)"), T("l", "l"), Quantities.Length) { Unit = "m", Param = "length", Min = 1e-3, Max = 1e5, ReadOnly = true, Relevance = VarRelevance.Panel },
            new("h", T("Wall height", "Wandhöhe"), T("h", "h"), Quantities.Length) { Unit = "m", Param = "height", StoredUnit = "mm", Default = 2.75, Min = 0.1, Max = 100, Relevance = VarRelevance.Panel },
            new("A", T("Wall area (gross)", "Wandfläche (brutto)"), T("A", "A"), Quantities.Area) { Unit = "m²", Param = "area", Min = 0, Max = 1e7, ReadOnly = true, Relevance = VarRelevance.Panel },
            new("Aopen", T("Area of doors and windows", "Fläche der Türen und Fenster"), T("A_open", "A_Öff"), Quantities.Area) { Unit = "m²", Param = "openingArea", Min = 0, Max = 1e7, ReadOnly = true, Relevance = VarRelevance.Panel },
            new("Anet", T("Wall area (net)", "Wandfläche (netto)"), T("A_net", "A_netto"), Quantities.Area) { Unit = "m²", Param = "netArea", Min = 0, Max = 1e7, ReadOnly = true },
            new("U", T("Thermal transmittance (U-value)", "Wärmedurchgangskoeffizient (U-Wert)"), T("U", "U"), Quantities.HeatTransfer) { Unit = "W/(m²·K)", Param = UParam, Min = 0.01, Max = 10 },
            new("dT", T("Temperature difference inside − outside", "Temperaturdifferenz innen − außen"), T("Δθ", "Δθ"), Quantities.TemperatureDifference) { Unit = "K", Param = DeltaParam, Min = 0, Max = 200 },
            new("PhiT", T("Transmission heat loss", "Transmissionswärmeverlust"), T("Φ_T", "Φ_T"), Quantities.HeatFlow) { Unit = "W", Param = LossParam, Min = 0, Max = 1e9 },
        };

        private static readonly Relation AreaRelation =
            new Relation("arch.wall.area", T("Wall area", "Wandfläche"), "{A} = {l}·{h}", "A", "l", "h") { Source = "S1" }
                .Solve("A", "{l}·{h}", v => v("l") * v("h"))
                .Solve("h", "{A}/{l}", v => v("A") / v("l"));

        private static readonly Relation NetRelation =
            new Relation("arch.wall.net", T("Net wall area", "Wandfläche netto"), "{Anet} = {A} − {Aopen}", "Anet", "A", "Aopen") { Source = "S1" }
                .Solve("Anet", "{A} − {Aopen}", v => v("A") - v("Aopen"));

        /// <summary>Φ_T = U·A·Δθ (steady one-dimensional heat transfer through a wall [S29]).</summary>
        private static readonly Relation LossRelation =
            new Relation("arch.wall.transmission", T("Transmission heat loss", "Transmissionswärmeverlust"), "{PhiT} = {U}·{Anet}·{dT}", "PhiT", "U", "Anet", "dT")
            {
                Source = "S29",
                Validity = T("steady state, no thermal bridges, net area without doors and windows; not a heat load calculation to EN 12831",
                             "stationär, ohne Wärmebrücken, Nettofläche ohne Türen und Fenster; keine Heizlastberechnung nach EN 12831"),
            }
                .Solve("PhiT", "{U}·{Anet}·{dT}", v => v("U") * v("Anet") * v("dT"))
                .Solve("U", "{PhiT}/({Anet}·{dT})", v => v("PhiT") / (v("Anet") * v("dT")))
                .Solve("dT", "{PhiT}/({U}·{Anet})", v => v("PhiT") / (v("U") * v("Anet")));

        internal static readonly Relation[] Rels = { AreaRelation, NetRelation, LossRelation };

        internal static IReadOnlyList<ParamDef> Params => new[]
        {
            new ParamDef(UParam, T("U-value", "U-Wert"), ParamKind.Text) { Unit = "W/(m²·K)", Advanced = true },
            new ParamDef(DeltaParam, T("Temperature difference inside − outside", "Temperaturdifferenz innen − außen"), ParamKind.Text) { Unit = "K", Advanced = true },
            new ParamDef(LossParam, T("Transmission heat loss", "Transmissionswärmeverlust"), ParamKind.Text) { Unit = "W", Advanced = true },
            ValueLabels.ShowValues,
        };

        /// <summary>Length (m) of the centre line (closed walls: the whole loop) at the plan scale.</summary>
        internal static double LengthMetres(WallLine w, double scale)
        {
            double len = 0;
            for (var k = 0; k < w.SegCount; k++) len += w.A(k).DistanceTo(w.B(k));
            return len * scale / 1000;
        }

        /// <summary>Height (m) of a door or window in the wall: the elevation box height when shown in elevation, else the preferred elevation height.</summary>
        internal static double OpeningHeightMetres(SketchObject o, ObjectType? type, SketchDocument? doc)
        {
            if (o.View == BoxType.ElevationView.Id) return o.Size.H * Arch.ScaleOf(o, doc) / 1000;
            var elev = (type as BoxType)?.ElevationSize;
            return elev is null ? 0 : elev.H * Arch.DefaultScale / 1000;
        }

        /// <summary>The doors and windows whose centre sits on the wall (as the wall's dependents and the band thickness use).</summary>
        internal static IEnumerable<SketchObject> OpeningsOn(WallLine w, SketchObject wall, SketchDocument doc)
        {
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(wall.Id));
            if (page is null) yield break;
            foreach (var other in doc.ObjectsOn(page))
            {
                if (other.IsPointGeometry || !Openings.IsOpening(other.Type)) continue;
                var c = new Vec2(other.Transform.X, other.Transform.Y);
                for (var k = 0; k < w.SegCount; k++)
                    if (Arch.DistanceToSegment(c, w.A(k), w.B(k)) <= Math.Max(w.Half, WallBuilder.JoinTolerance)) { yield return other; break; }
            }
        }

        /// <summary>Area (m²) of the doors and windows in the wall: real width times height.</summary>
        internal static double OpeningArea(WallLine w, SketchObject wall, RenderContext ctx, Func<string, ObjectType?> typeOf)
            => OpeningsOn(w, wall, ctx.Doc).Sum(op => Openings.RealWidth(op, ctx.Doc) / 1000 * OpeningHeightMetres(op, typeOf(op.Type), ctx.Doc));

        internal static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var w = WallLine.Of(o, ctx.Doc);
            if (w is null) yield break;
            var scale = Arch.ScaleOf(o, ctx.Doc);
            var l = LengthMetres(w, scale);
            if (!(l > 0) || double.IsInfinity(l)) yield break;
            var expr = "l_axis·" + scale.ToString("0.###", CultureInfo.InvariantCulture);
            yield return BoundRelation.Local(new Relation("arch.wall.length", T("Wall length from the centre line at the plan scale", "Wandlänge aus der Achse im Planmaßstab"), "{l} = " + expr, "l")
                .Solve("l", expr, _ => l), o.Id);
            var open = OpeningArea(w, o, ctx, id => Types.TryGetValue(id, out var t) ? t : null);
            yield return BoundRelation.Local(new Relation("arch.wall.openings", T("Doors and windows in the wall (width × height)", "Türen und Fenster in der Wand (Breite × Höhe)"), "{Aopen} = Σ b·h", "Aopen")
                .Solve("Aopen", "Σ b·h", _ => open), o.Id);
        }

        /// <summary>The module's door and window types by id (their elevation heights).</summary>
        private static readonly Dictionary<string, ObjectType> Types = Openings.Types().ToDictionary(t => t.Id);
    }
}
