using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Calc 5 (linked charts): the centrifugal pump offers its pump curve H(V̇) = H₀·(1 − (V̇/V̇_max)²) with the system curve
    /// H_stat + k·V̇² and the operating point (<see cref="CharacteristicKit.PumpAndSystem"/>, [S28]). The curve data H₀, V̇_max
    /// and H_stat are optional text parameters of the pump; the chart is a dependent of the pump, so it follows every edit.
    /// </summary>
    internal sealed partial class ProcessType : ICharacteristics
    {
        private const string PumpId = "process.pump.centrifugal";

        private bool HasCurves => _id == PumpId;

        /// <summary>Curve parameters of the types with characteristic curves (empty for all others).</summary>
        private IReadOnlyList<ParamDef> CurveParams => HasCurves ? CharacteristicKit.PumpParams("m³/h") : Array.Empty<ParamDef>();

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
            => HasCurves ? new[] { CharacteristicKit.PumpInfo } : Array.Empty<CharacteristicInfo>();

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
            => HasCurves && key == CharacteristicKit.PumpSet ? CharacteristicKit.PumpAndSystem(c, "Vdot", "H", "m³/h", 100 / 3600.0, 40) : null;

        /// <summary>Linked charts render from the pump: they are its dependents.</summary>
        public override bool HasDependents => HasCurves;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => HasCurves ? Characteristics.LinkedCharts(ctx.Doc, o.Id) : Array.Empty<string>();

        /// <summary>The type's actions plus "Characteristic chart ▸" (insert a chart linked to the pump).</summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => HasCurves ? base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx)) : base.Actions(o, ctx);
    }
}
