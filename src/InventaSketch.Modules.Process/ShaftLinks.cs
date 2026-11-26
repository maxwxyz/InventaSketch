using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Shaft links of the rotating process machines (pumps, compressors, fans, turbines, motors), read from the core shaft
    /// lines (<see cref="ShaftLines"/>, built on the coupling relations that "select connected – moves along" follows):
    /// <list type="bullet">
    /// <item>A machine whose hub sits on a shaft axis (a mechanical shaft, the shaft stub of a motor or turbine, the shaft of
    /// an e-machine) or that is concentric with another hub turns with it: all parts of the shaft line share the speed
    /// <c>n</c>.</item>
    /// <item>The torque <c>T</c> passes along the line (a motor on the pump's hub, or both on one mechanical shaft or
    /// coupling) when at most two of its members take or give torque: with a branch no torque is passed.</item>
    /// </list>
    /// With the shaft power <c>P</c> = 2π·<c>n</c>·<c>T</c> on each machine, the drive's power becomes the pump's shaft power.
    /// </summary>
    internal static class ShaftLinks
    {
        private static readonly Relation SameSpeed = Relation.Equality("process.shaft.speed", P.T("Same speed on one shaft", "Gleiche Drehzahl auf einer Welle"));
        private static readonly Relation SameTorque = Relation.Equality("process.shaft.torque", P.T("Torque passed on by the shaft coupling", "Drehmoment über die Wellenkupplung weitergegeben"));

        /// <summary>The shaft relations <paramref name="o"/> takes part in (empty without a registry or shaft line).</summary>
        public static IEnumerable<BoundRelation> For(SketchObject o, RenderContext ctx, Registry? owner)
        {
            var registry = ctx.Registry ?? owner;
            return registry is null ? Array.Empty<BoundRelation>() : ShaftLines.Of(registry, ctx.Doc).Links(o.Id, SameSpeed, SameTorque);
        }
    }
}
