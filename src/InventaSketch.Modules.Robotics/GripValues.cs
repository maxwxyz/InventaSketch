using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>Further values of a robotics box symbol (besides its mass): variables, relations, parameters, checks, links.</summary>
    internal sealed record RoboValueSpec(IReadOnlyList<VarDef> Vars, IReadOnlyList<Relation> Relations, IReadOnlyList<ParamDef> Params, IReadOnlyList<ValueCheck> Checks)
    {
        /// <summary>Relations to other elements (the workpiece held at the tool centre point).</summary>
        public Func<SketchObject, RenderContext, IEnumerable<BoundRelation>>? Links { get; init; }
    }

    /// <summary>
    /// Holding forces of grippers (engineering calculations, robotics): a friction grip (parallel and three-finger gripper)
    /// holds the workpiece of mass m_W when the friction of the jaws carries its weight and inertia with a safety factor,
    /// μ·F_G ≥ m_W·(g + a)·S, so the required gripping force is F_req = m_W·(g + a)·S/μ (F_G the sum of the jaw forces, as
    /// datasheets give it; the load hangs between the jaws, acceleration a along the weight). A vacuum gripper holds by the
    /// pressure difference over its cups, F_th = Δp·n·π·d²/4, and needs F_req = m_W·(g + a)·S for a load lifted
    /// perpendicular to the suction faces. m_W is linked to the mass of the workpiece whose port "grip" sits on the tool's
    /// TCP. Checks <c>robot.grip.force</c> and <c>robot.vacuum.force</c>. Defaults: S = 2, a = 0 (at rest); μ, F_G, d and
    /// Δp come from the user (no default).
    /// </summary>
    internal static class GripValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, string symbol, QuantityKind kind, string unit, double max, VarRelevance rel = VarRelevance.Panel)
            => new(key, name, T(symbol, symbol), kind) { Unit = unit, Param = key, Relevance = rel, Min = 0, Max = max };

        private static readonly I18nText ForceName = T("Gripping force (datasheet, sum of the jaw forces)", "Greifkraft (Datenblatt, Summe der Backenkräfte)");
        private static readonly I18nText RequiredName = T("Required holding force", "Erforderliche Haltekraft");
        private static readonly I18nText MuName = T("Friction coefficient jaw – workpiece", "Reibungszahl Backe – Werkstück");
        private static readonly I18nText SafetyName = T("Safety factor", "Sicherheitsfaktor");
        private static readonly I18nText AccName = T("Acceleration of the motion (along the weight)", "Beschleunigung der Bewegung (in Richtung der Gewichtskraft)");
        private static readonly I18nText HeldName = T("Mass of the held workpiece", "Masse des gehaltenen Werkstücks");
        private static readonly I18nText HoldingName = T("Holding force of the cups (Δp·n·π·d²/4)", "Haltekraft der Saugnäpfe (Δp·n·π·d²/4)");
        private static readonly I18nText CupName = T("Suction cup diameter", "Saugnapfdurchmesser");
        private static readonly I18nText VacuumName = T("Vacuum (pressure difference to ambient)", "Vakuum (Druckdifferenz zur Umgebung)");
        private static readonly I18nText CupsName = T("Number of suction cups", "Anzahl Saugnäpfe");

        private static VarDef Safety => V("safety", SafetyName, "S", Quantities.Ratio, "", 100) with { Default = 2 };
        private static VarDef Acc => V("acc", AccName, "a", Quantities.Acceleration, "m/s²", 1e4) with { Default = 0 };
        private static VarDef Held => V("held", HeldName, "m_W", Quantities.Mass_, "kg", 1e5);
        private static VarDef Required => V("required", RequiredName, "F_req", Quantities.Force, "N", 1e8, VarRelevance.Card);

        private static readonly Relation Friction =
            new Relation("robot.grip.friction", T("Required gripping force of a friction grip", "Erforderliche Greifkraft beim Reibschluss"),
                "{required} = {held}·(g + {acc})·{safety}/{mu}", "required", "held", "acc", "safety", "mu")
            {
                Validity = T("force-closed grip by friction, workpiece hanging between the jaws, acceleration along the weight", "kraftschlüssiges Greifen durch Reibung, Werkstück hängt zwischen den Backen, Beschleunigung in Richtung der Gewichtskraft"),
            }
                .Solve("required", "{held}·(g + {acc})·{safety}/{mu}", v => v("held") * (RoboValues.G + v("acc")) * v("safety") / v("mu"))
                .Solve("held", "{required}·{mu}/((g + {acc})·{safety})", v => v("required") * v("mu") / ((RoboValues.G + v("acc")) * v("safety")));

        private static readonly Relation VacuumHolding =
            new Relation("robot.vacuum.holding", T("Holding force of the suction cups", "Haltekraft der Saugnäpfe"),
                "{holding} = {vacuum}·{cups}·π·{cupDia}²/4", "holding", "vacuum", "cups", "cupDia")
            {
                Validity = T("theoretical force from the pressure difference over the cup area (no seal losses)", "theoretische Kraft aus der Druckdifferenz über der Saugfläche (ohne Dichtverluste)"),
            }
                .Solve("holding", "{vacuum}·{cups}·π·{cupDia}²/4", v => v("vacuum") * v("cups") * Math.PI * v("cupDia") * v("cupDia") / 4)
                .Solve("vacuum", "4·{holding}/({cups}·π·{cupDia}²)", v => 4 * v("holding") / (v("cups") * Math.PI * v("cupDia") * v("cupDia")))
                .Solve("cupDia", "√(4·{holding}/({cups}·π·{vacuum}))", v => Math.Sqrt(4 * v("holding") / (v("cups") * Math.PI * v("vacuum"))));

        private static readonly Relation VacuumRequired =
            new Relation("robot.vacuum.required", T("Required holding force of a vacuum grip", "Erforderliche Haltekraft beim Vakuumgreifen"),
                "{required} = {held}·(g + {acc})·{safety}", "required", "held", "acc", "safety")
            {
                Validity = T("load lifted perpendicular to the suction faces, acceleration along the weight", "Last senkrecht zur Saugfläche angehoben, Beschleunigung in Richtung der Gewichtskraft"),
            }
                .Solve("required", "{held}·(g + {acc})·{safety}", v => v("held") * (RoboValues.G + v("acc")) * v("safety"))
                .Solve("held", "{required}/((g + {acc})·{safety})", v => v("required") / ((RoboValues.G + v("acc")) * v("safety")));

        private static bool Above(double? value, double? limit) => value is double v && limit is double l && v > l * (1 + 1e-9);

        /// <summary>robot.grip.force: the gripping force is below the force the friction grip needs.</summary>
        public static readonly ValueCheck GripForce = new("robot.grip.force", CheckSeverity.Warning,
            T("The gripping force F_G is below the force needed to hold the workpiece by friction, F_req = m_W·(g + a)·S/μ. Choose a stronger gripper, form-fitting jaws or a higher friction (jaw inserts), or move more gently.",
              "Die Greifkraft F_G liegt unter der Kraft, die zum Halten des Werkstücks durch Reibung nötig ist, F_req = m_W·(g + a)·S/μ. Einen stärkeren Greifer, formschlüssige Backen oder eine höhere Reibung (Backeneinsätze) wählen oder sanfter bewegen."),
            "F_G ≥ F_req", c => Above(c["required"], c["force"]));

        /// <summary>robot.vacuum.force: the suction cups hold less than the workpiece needs.</summary>
        public static readonly ValueCheck VacuumForce = new("robot.vacuum.force", CheckSeverity.Warning,
            T("The holding force of the suction cups F_th = Δp·n·π·d²/4 is below the force needed to lift the workpiece, F_req = m_W·(g + a)·S. Use larger or more cups, a deeper vacuum or move more gently.",
              "Die Haltekraft der Saugnäpfe F_th = Δp·n·π·d²/4 liegt unter der Kraft, die zum Anheben des Werkstücks nötig ist, F_req = m_W·(g + a)·S. Größere oder mehr Saugnäpfe, ein tieferes Vakuum oder eine sanftere Bewegung wählen."),
            "F_th ≥ F_req", c => Above(c["required"], c["holding"]));

        private static readonly Relation SameMass = Relation.Equality("robot.grip.held", T("The held workpiece's mass", "Masse des gehaltenen Werkstücks"));

        /// <summary>m_W = the mass of the workpiece whose port "grip" sits on the tool's TCP (when there is one).</summary>
        private static IEnumerable<BoundRelation> HeldLinks(SketchObject o, RenderContext ctx)
        {
            if (ctx.Registry is not { } reg) yield break;
            var tcp = reg.PageAnchors(o, ctx).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id == "tcp");
            if (tcp is null) yield break;
            var page = ctx.Doc.PageOf(o.Id);
            var objects = page is null ? ctx.Doc.Objects.Values : ctx.Doc.ObjectsOn(page);
            foreach (var w in objects.Where(x => x.Type == RoboValues.WorkpieceId))
                if (reg.PageAnchors(w, ctx).FirstOrDefault(a => a.Role == AnchorRole.Port && a.Id == "grip") is { } g && g.P.DistanceTo(tcp.P) <= RoboValues.DockTol)
                {
                    yield return new BoundRelation(SameMass, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(o.Id, "held"), ["b"] = new VarRef(w.Id, "mass") });
                    yield break;
                }
        }

        /// <summary>Parallel and three-finger gripper: friction grip.</summary>
        public static readonly RoboValueSpec FrictionGrip = new(
            new[]
            {
                V("force", ForceName, "F_G", Quantities.Force, "N", 1e8, VarRelevance.Card),
                Required,
                V("mu", MuName, "μ", Quantities.Ratio, "", 10),
                Safety, Acc, Held,
            },
            new[] { Friction },
            new[] { Text("force", ForceName, "N"), Text("required", RequiredName, "N"), Text("mu", MuName, ""), Text("safety", SafetyName, ""), Text("acc", AccName, "m/s²"), Text("held", HeldName, "kg") },
            new[] { GripForce }) { Links = HeldLinks };

        /// <summary>Vacuum gripper: holding force of the cups against the required force.</summary>
        public static readonly RoboValueSpec VacuumGrip = new(
            new[]
            {
                V("holding", HoldingName, "F_th", Quantities.Force, "N", 1e8, VarRelevance.Card),
                Required,
                V("cupDia", CupName, "d", Quantities.Length, "mm", 10),
                V("vacuum", VacuumName, "Δp", Quantities.Pressure, "bar", 1e6),
                new VarDef("cups", CupsName, T("n", "n"), Quantities.Count) { Param = "cups", Relevance = VarRelevance.Panel, Default = 1, Min = 1, Max = 100 },
                Safety, Acc, Held,
            },
            new[] { VacuumHolding, VacuumRequired },
            new[] { Text("holding", HoldingName, "N"), Text("required", RequiredName, "N"), Text("cupDia", CupName, "mm"), Text("vacuum", VacuumName, "bar"), Text("safety", SafetyName, ""), Text("acc", AccName, "m/s²"), Text("held", HeldName, "kg") },
            new[] { VacuumForce }) { Links = HeldLinks };

        /// <summary>Compressed-air supply of pneumatic grippers and tool changers (fluid power lines dock here).</summary>
        public static readonly PortConnection Air = PortConnection.Of(PortMedia.FluidPneumatic, "fluid.line");
    }
}
