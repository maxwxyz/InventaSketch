using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Calc 5 (linked charts): the speed, torque and power diagram of a gear train, one bar per shaft along the train
    /// [S16, S18]. The shafts are the groups of gears and shafts with the same solved speed in the gear's solver scope (the
    /// whole train joins it through the mesh and shaft links); they are ordered by falling speed (input of a reduction
    /// first), torque per shaft is the largest torque of its members, power P = 2π·n·T. Elements without a solved speed
    /// are left out.
    /// </summary>
    internal sealed partial class MechType : ICharacteristics
    {
        private static I18nText Tx(string en, string de) => new(en, de);

        public const string TrainSet = "train";

        private static readonly CharacteristicInfo TrainInfo = new(TrainSet, Tx("Gear train: speed and torque", "Getriebe: Drehzahl und Moment"));

        private bool HasCurves => (_id.StartsWith("mech.gear.", StringComparison.Ordinal) || _id == "mech.shaft")
            && ValueSpec.Variables.Any(v => v.Key == "n") && ValueSpec.Variables.Any(v => v.Key == "T");

        private static bool InTrain(string? type) => type is not null && (type.StartsWith("mech.gear.", StringComparison.Ordinal) || type == "mech.shaft");

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
            => HasCurves ? new[] { TrainInfo } : Array.Empty<CharacteristicInfo>();

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
            => HasCurves && key == TrainSet ? Train(c) : null;

        public override bool DependsOnNeighbours => HasCurves;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => HasCurves ? Characteristics.LinkedCharts(ctx.Doc, o.Id) : Array.Empty<string>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => HasCurves ? base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx)) : base.Actions(o, ctx);

        /// <summary>One shaft of the train: speed and torque (SI; torque NaN if unknown) and its members.</summary>
        internal sealed record TrainShaft(double N, double T, IReadOnlyList<string> Members);

        /// <summary>The shafts of the train in the solver scope of <paramref name="c"/>, by falling speed.</summary>
        internal static IReadOnlyList<TrainShaft> Shafts(CharacteristicContext c)
        {
            var groups = new List<(double N, List<string> Ids)>();
            foreach (var id in c.Table.ObjectIds)
            {
                if (!InTrain(c.Doc.Get(id)?.Type) || c.Value(id, "n") is not double n || n <= 0) continue;
                var g = groups.FindIndex(x => Math.Abs(x.N - n) <= 1e-6 * Math.Max(x.N, n));
                if (g < 0) groups.Add((n, new List<string> { id }));
                else groups[g].Ids.Add(id);
            }
            return groups.OrderByDescending(g => g.N)
                .Select(g => new TrainShaft(g.N, g.Ids.Select(id => c.Value(id, "T")).Where(t => t is not null).Select(t => t!.Value).DefaultIfEmpty(double.NaN).Max(), g.Ids))
                .ToList();
        }

        private static CharacteristicSet Train(CharacteristicContext c)
        {
            var rpm = Characteristics.UnitOf(Quantities.RotationalSpeed, "min⁻¹");
            var nm = Characteristics.UnitOf(Quantities.Torque, "N·m");
            var shafts = Shafts(c);
            var set = new CharacteristicSet(TrainSet, TrainInfo.Name,
                new CharacteristicAxis(Tx("Shaft (input → output)", "Welle (Antrieb → Abtrieb)"), "") { Min = 0.5, Max = Math.Max(1, shafts.Count) + 0.5 },
                new CharacteristicAxis(Tx("Speed n", "Drehzahl n"), rpm.Symbol) { Min = 0 },
                new CharacteristicAxis(Tx("Torque T", "Drehmoment M"), nm.Symbol) { Min = 0 },
                new CharacteristicAxis(Tx("Power P", "Leistung P"), "W") { Min = 0 });
            if (shafts.Count == 0)
            {
                set.Notes.Add(Tx("No speeds known: enter n on a gear or shaft", "Keine Drehzahlen bekannt: n an einem Zahnrad oder einer Welle eingeben"));
                return set;
            }
            // Bars: a horizontal segment per shaft, separated by gaps.
            List<Vec2> Bars(Func<TrainShaft, double> value)
            {
                var list = new List<Vec2>();
                for (var i = 0; i < shafts.Count; i++)
                {
                    var v = value(shafts[i]);
                    if (list.Count > 0) list.Add(new Vec2(double.NaN, double.NaN));
                    list.Add(new Vec2(i + 1 - 0.35, v));
                    list.Add(new Vec2(i + 1 + 0.35, v));
                }
                return list;
            }
            set.Curves.Add(new CharacteristicCurve("n", Tx("Speed n", "Drehzahl n"), Bars(s => rpm.FromSi(s.N))));
            if (shafts.Any(s => !double.IsNaN(s.T)))
            {
                set.Curves.Add(new CharacteristicCurve("T", Tx("Torque T", "Drehmoment M"), Bars(s => double.IsNaN(s.T) ? double.NaN : nm.FromSi(s.T))) { Panel = 1, Look = CurveLook.Second });
                set.Curves.Add(new CharacteristicCurve("P", Tx("Power P", "Leistung P"), Bars(s => double.IsNaN(s.T) ? double.NaN : s.N * s.T)) { Panel = 2, Look = CurveLook.Second });
            }
            else set.Notes.Add(Tx("No torques known: enter T on a gear or shaft", "Keine Momente bekannt: M an einem Zahnrad oder einer Welle eingeben"));
            return set;
        }
    }
}
