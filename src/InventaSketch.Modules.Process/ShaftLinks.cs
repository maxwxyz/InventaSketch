using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Shaft links of the rotating process machines (pumps, compressors, fans, turbines, motors, generators), read from the
    /// core shaft lines (<see cref="ShaftLines"/>, built on the coupling relations that "select connected – moves along"
    /// follows):
    /// <list type="bullet">
    /// <item>A machine whose hub sits on a shaft axis (a mechanical shaft, the shaft stub of a motor or turbine, the shaft of
    /// an e-machine) or that is concentric with another hub turns with it: all parts of the shaft line share the speed
    /// <c>n</c>.</item>
    /// <item>The torque <c>T</c> passes along the line (a motor on the pump's hub, or both on one mechanical shaft or
    /// coupling) when at most two of its members take or give torque: with a branch no torque is passed.</item>
    /// <item>On such a line with exactly two torque members (the drive and the driven machine) the shaft power <c>P</c> is
    /// passed as well: a compressor's power demand reaches its motor, a turbine's output its generator, even when the speed
    /// is not known (with the speed known it follows from P = 2π·n·T anyway).</item>
    /// </list>
    /// With the shaft power <c>P</c> = 2π·<c>n</c>·<c>T</c> on each machine, the drive's power becomes the pump's shaft power.
    /// </summary>
    internal static class ShaftLinks
    {
        private static readonly Relation SameSpeed = Relation.Equality("process.shaft.speed", P.T("Same speed on one shaft", "Gleiche Drehzahl auf einer Welle"));
        private static readonly Relation SameTorque = Relation.Equality("process.shaft.torque", P.T("Torque passed on by the shaft coupling", "Drehmoment über die Wellenkupplung weitergegeben"));
        private static readonly Relation SamePower = Relation.Equality("process.shaft.power", P.T("Shaft power passed on by the shaft coupling", "Wellenleistung über die Wellenkupplung weitergegeben"));

        /// <summary>The shaft relations <paramref name="o"/> takes part in (empty without a registry or shaft line).</summary>
        public static IEnumerable<BoundRelation> For(SketchObject o, RenderContext ctx, Registry? owner)
        {
            var registry = ctx.Registry ?? owner;
            if (registry is null) return Array.Empty<BoundRelation>();
            var lines = ShaftLines.Of(registry, ctx.Doc);
            var links = lines.Links(o.Id, SameSpeed, SameTorque);
            return Power(lines, registry, ctx.Doc, o.Id) is { } p ? links.Concat(new[] { p }) : links;
        }

        /// <summary>
        /// P = P between <paramref name="id"/> and the other torque member of its shaft line, when the line carries torque and
        /// has exactly these two (a drive and a driven machine, shafts and couplings between them) and the other has a
        /// mechanical power <c>P</c>; null otherwise (a branch or a gear stage splits the power in an unknown way).
        /// </summary>
        internal static BoundRelation? Power(ShaftLines lines, Registry registry, SketchDocument doc, string id)
        {
            if (lines.LineOf(id) is not { } line || !line.CarriesTorque || line.Torque.Count != 2 || !line.Torque.Contains(id)) return null;
            var other = line.Torque[0] == id ? line.Torque[1] : line.Torque[0];
            if (doc.Get(other) is not { } oo || ValueModel.Variable(registry.Type(oo.Type), "P") is not { } pv || pv.Kind.Id != Quantities.Power.Id) return null;
            var (a, b) = string.CompareOrdinal(id, other) < 0 ? (id, other) : (other, id);
            return new BoundRelation(SamePower, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(a, "P"), ["b"] = new VarRef(b, "P") });
        }
    }
}
