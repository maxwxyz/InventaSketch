using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Element values of the criticality overlays (engineering calculations, section 1). The TTC label computes the time to
    /// collision <c>TTC = d / (v_ego − v_target)</c> from the gap and both speeds (constant speeds, following in the same
    /// lane); the safety distance computes the stopping distance <c>s = v·t_r + v²/(2·a)</c> (reaction distance plus braking
    /// distance at constant deceleration) and compares it with the gap, which comes from the drawing at the dimension's
    /// scale unless it is entered. Checks: <c>auto.ttc.low</c> (TTC below 2 s) and <c>auto.braking.gap</c> (stopping
    /// distance longer than the gap). Illustrative values for sketches, not a validated driving function.
    /// </summary>
    internal static class AutoValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static ParamDef Text(string key, I18nText label, string unit, bool advanced = false) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = advanced };

        /// <summary>TTC threshold of the warning (s): a common rule of thumb for critical following situations, not a standard value.</summary>
        public const double TtcWarning = 2.0;

        // ------------------------------------------------------------------ time to collision

        private static readonly I18nText EgoName = T("Speed of the ego vehicle", "Geschwindigkeit des Egofahrzeugs");
        private static readonly I18nText TargetName = T("Speed of the vehicle ahead (target)", "Geschwindigkeit des vorausfahrenden Fahrzeugs (Ziel)");
        private static readonly I18nText GapName = T("Gap (distance to the vehicle ahead)", "Abstand (zum vorausfahrenden Fahrzeug)");
        private static readonly I18nText TtcName = T("Time to collision", "Zeit bis zur Kollision");

        public static readonly IReadOnlyList<VarDef> TtcVars = new VarDef[]
        {
            new("vEgo", EgoName, T("v_ego", "v_ego"), Quantities.Speed) { Unit = "km/h", Param = "vEgo", Min = 0, Max = 200 },
            new("vTarget", TargetName, T("v_target", "v_Ziel"), Quantities.Speed) { Unit = "km/h", Param = "vTarget", Min = 0, Max = 200 },
            new("gap", GapName, T("d", "d"), Quantities.Length) { Unit = "m", Param = "gap", Min = 0, Max = 1e4 },
            // The label draws the TTC itself; the values label leaves an entered TTC out.
            new("ttc", TtcName, T("TTC", "TTC"), Quantities.Time_) { Unit = "s", Param = "ttc", Min = 0, Max = 1e4, OnTypeLabel = true },
        };

        public static readonly IReadOnlyList<ParamDef> TtcParams = new[]
        {
            Text("vEgo", EgoName, "km/h"), Text("vTarget", TargetName, "km/h"), Text("gap", GapName, "m"), ValueLabels.ShowValues,
        };

        /// <summary>Time to collision (s) for a gap (m) and two speeds (m/s); null when the ego vehicle is not closing in.</summary>
        public static double? Ttc(double gap, double vEgo, double vTarget)
        {
            var closing = vEgo - vTarget;
            return closing > 1e-9 && gap >= 0 ? gap / closing : null;
        }

        private static double Closing(Func<string, double> v) => v("vEgo") - v("vTarget");

        /// <summary>auto.ttc: TTC = d / (v_ego − v_target).</summary>
        public static readonly Relation TtcRelation =
            new Relation("auto.ttc", T("Time to collision at constant speeds", "Zeit bis zur Kollision bei konstanten Geschwindigkeiten"),
                "{ttc} = {gap}/({vEgo} − {vTarget})", "ttc", "gap", "vEgo", "vTarget")
            {
                Validity = T("Constant speeds, following in the same lane, the ego vehicle faster than the vehicle ahead",
                    "Konstante Geschwindigkeiten, Folgefahrt im selben Fahrstreifen, Egofahrzeug schneller als das vorausfahrende"),
            }
            .Solve("ttc", "{gap}/({vEgo} − {vTarget})", v => Closing(v) > 0 ? v("gap") / Closing(v) : double.NaN)
            .Solve("gap", "{ttc}·({vEgo} − {vTarget})", v => v("ttc") * Closing(v))
            .Solve("vEgo", "{vTarget} + {gap}/{ttc}", v => v("vTarget") + v("gap") / v("ttc"))
            .Solve("vTarget", "{vEgo} − {gap}/{ttc}", v => v("vEgo") - v("gap") / v("ttc"));

        public static readonly IReadOnlyList<Relation> TtcRelations = new[] { TtcRelation };

        /// <summary>auto.ttc.low: the time to collision is below 2 s.</summary>
        public static readonly ValueCheck TtcLow = new("auto.ttc.low", CheckSeverity.Warning,
            T("The time to collision TTC is below 2 s: a critical approach. Increase the gap or reduce the ego speed (2 s is a common rule of thumb; use the threshold of your own function where it differs).",
              "Die Zeit bis zur Kollision TTC liegt unter 2 s: eine kritische Annäherung. Abstand vergrößern oder die Egogeschwindigkeit verringern (2 s ist ein üblicher Richtwert; wo die eigene Funktion eine andere Schwelle hat, diese verwenden)."),
            "TTC ≥ 2 s", c => c["ttc"] is double t && t < TtcWarning - 1e-9);

        public static readonly IReadOnlyList<ValueCheck> TtcChecks = new[] { TtcLow };

        /// <summary>The TTC (s) from the entered gap and speeds of a TTC label; null if one is missing, +∞ when not closing in.</summary>
        public static double? EnteredTtc(SketchObject o)
        {
            var gap = ValueModel.ReadEntered(o, TtcVars[2]).Value;
            var ego = ValueModel.ReadEntered(o, TtcVars[0]).Value;
            var target = ValueModel.ReadEntered(o, TtcVars[1]).Value;
            if (gap is not double d || ego is not double ve || target is not double vt) return null;
            return Ttc(d, ve, vt) ?? double.PositiveInfinity;
        }

        // ------------------------------------------------------------------ stopping distance

        public const double DefaultReaction = 1.0;
        public const double DefaultDeceleration = 8.0;

        private static readonly I18nText SpeedName = T("Speed of the ego vehicle", "Geschwindigkeit des Egofahrzeugs");
        private static readonly I18nText ReactionName = T("Reaction time (default 1 s)", "Reaktionszeit (Vorgabe 1 s)");
        private static readonly I18nText DecelName = T("Braking deceleration (default 8 m/s², full braking on a dry road)", "Bremsverzögerung (Vorgabe 8 m/s², Vollbremsung auf trockener Fahrbahn)");
        private static readonly I18nText ReactionDistName = T("Reaction distance", "Reaktionsweg");
        private static readonly I18nText BrakingDistName = T("Braking distance", "Bremsweg");
        private static readonly I18nText StoppingName = T("Stopping distance (reaction and braking distance)", "Anhalteweg (Reaktions- und Bremsweg)");
        private static readonly I18nText DrawnGapName = T("Gap (drawn length at the dimension scale, or entered)", "Abstand (gezeichnete Länge im Maßstab der Bemaßung oder eingegeben)");

        public static readonly IReadOnlyList<VarDef> BrakingVars = new VarDef[]
        {
            new("v", SpeedName, T("v", "v"), Quantities.Speed) { Unit = "km/h", Param = "speed", Min = 0, Max = 200 },
            new("s", StoppingName, T("s", "s"), Quantities.Length) { Unit = "m", Param = "stopping", Min = 0, Max = 1e4 },
            new("gap", DrawnGapName, T("d", "d"), Quantities.Length) { Unit = "m", Param = "gap", Min = 0, Max = 1e4 },
            new("tr", ReactionName, T("t_r", "t_R"), Quantities.Time_) { Unit = "s", Param = "reaction", Relevance = VarRelevance.Panel, Default = DefaultReaction, Min = 0, Max = 10 },
            new("a", DecelName, T("a", "a"), Quantities.Acceleration) { Unit = "m/s²", Param = "decel", Relevance = VarRelevance.Panel, Default = DefaultDeceleration, Min = 0.1, Max = 20 },
            new("sR", ReactionDistName, T("s_r", "s_R"), Quantities.Length) { Unit = "m", Param = "reactionDistance", Relevance = VarRelevance.Panel, Min = 0, Max = 1e4 },
            new("sB", BrakingDistName, T("s_b", "s_B"), Quantities.Length) { Unit = "m", Param = "brakingDistance", Relevance = VarRelevance.Panel, Min = 0, Max = 1e4 },
        };

        public static readonly IReadOnlyList<ParamDef> BrakingParams = new[]
        {
            Text("speed", SpeedName, "km/h"), Text("reaction", ReactionName, "s"), Text("decel", DecelName, "m/s²"), Text("gap", DrawnGapName, "m"),
            Text("stopping", StoppingName, "m", true), Text("reactionDistance", ReactionDistName, "m", true), Text("brakingDistance", BrakingDistName, "m", true),
            ValueLabels.ShowValues,
        };

        /// <summary>Stopping distance (m) s = v·t_r + v²/(2a) for v in m/s, t_r in s and a in m/s².</summary>
        public static double StoppingDistance(double v, double reaction, double decel) => v * reaction + v * v / (2 * decel);

        /// <summary>auto.braking.reaction: s_r = v·t_r (distance covered during the reaction time).</summary>
        public static readonly Relation ReactionRelation =
            new Relation("auto.braking.reaction", T("Reaction distance", "Reaktionsweg"), "{sR} = {v}·{tr}", "sR", "v", "tr")
                .Solve("sR", "{v}·{tr}", v => v("v") * v("tr"))
                .Solve("v", "{sR}/{tr}", v => v("sR") / v("tr"))
                .Solve("tr", "{sR}/{v}", v => v("sR") / v("v"));

        /// <summary>auto.braking.distance: s_b = v²/(2a) (constant deceleration to standstill).</summary>
        public static readonly Relation BrakingRelation =
            new Relation("auto.braking.distance", T("Braking distance at constant deceleration", "Bremsweg bei konstanter Verzögerung"), "{sB} = {v}²/(2·{a})", "sB", "v", "a")
            {
                Validity = T("Constant deceleration from the start of braking to standstill", "Konstante Verzögerung vom Bremsbeginn bis zum Stillstand"),
            }
            .Solve("sB", "{v}²/(2·{a})", v => v("v") * v("v") / (2 * v("a")))
            .Solve("v", "√(2·{a}·{sB})", v => Math.Sqrt(2 * v("a") * v("sB")))
            .Solve("a", "{v}²/(2·{sB})", v => v("v") * v("v") / (2 * v("sB")));

        /// <summary>auto.braking.stopping: s = s_r + s_b.</summary>
        public static readonly Relation StoppingRelation =
            new Relation("auto.braking.stopping", T("Stopping distance", "Anhalteweg"), "{s} = {sR} + {sB}", "s", "sR", "sB")
                .Solve("s", "{sR} + {sB}", v => v("sR") + v("sB"))
                .Solve("sR", "{s} − {sB}", v => v("s") - v("sB"))
                .Solve("sB", "{s} − {sR}", v => v("s") - v("sR"));

        public static readonly IReadOnlyList<Relation> BrakingRelations = new[] { ReactionRelation, BrakingRelation, StoppingRelation };

        /// <summary>auto.braking.gap: the stopping distance is longer than the gap.</summary>
        public static readonly ValueCheck BrakingGap = new("auto.braking.gap", CheckSeverity.Warning,
            T("The stopping distance s (reaction and braking distance) is longer than the gap d: the vehicle cannot stop before reaching the object. Increase the gap or reduce the speed.",
              "Der Anhalteweg s (Reaktions- und Bremsweg) ist länger als der Abstand d: Das Fahrzeug kann nicht vor dem Objekt anhalten. Abstand vergrößern oder Geschwindigkeit verringern."),
            "s ≤ d", c => c["s"] is double s && c["gap"] is double d && s > d * (1 + 1e-9));

        public static readonly IReadOnlyList<ValueCheck> BrakingChecks = new[] { BrakingGap };

        /// <summary>The gap of a safety distance from the drawing (m): drawn length divided by the scale (mm per m).</summary>
        public static double DrawnGap(IReadOnlyList<Vec2> p, SketchObject o)
            => p.Count < 2 ? 0 : (p[p.Count - 1] - p[0]).Length / Math.Max(0.01, o.Num("scale", 10));

        /// <summary>The safety distance's gap from the drawing, unless the gap is entered.</summary>
        public static IEnumerable<BoundRelation> BrakingLinks(SketchObject o, RenderContext ctx)
        {
            if (ValueModel.ReadEntered(o, BrakingVars[2]).Value is not null) yield break;
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count < 2) yield break;
            var m = DrawnGap(p, o);
            if (!(m > 0) || double.IsInfinity(m)) yield break;
            var mm = (p[p.Count - 1] - p[0]).Length;
            var expr = mm.ToString("0.#", CultureInfo.InvariantCulture) + " mm / " + Math.Max(0.01, o.Num("scale", 10)).ToString("0.###", CultureInfo.InvariantCulture) + " mm/m";
            var rel = new Relation("auto.gap.drawn", T("Gap from the drawing at the dimension scale", "Abstand aus der Zeichnung im Maßstab der Bemaßung"), "{gap} = " + expr, "gap")
                .Solve("gap", expr, _ => m);
            yield return BoundRelation.Local(rel, o.Id);
        }
    }
}
