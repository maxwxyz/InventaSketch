using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Shaft links of pumps and motors, read from the core coupling relations (<see cref="Coupling.Graph"/>, the same relations
    /// "select connected – moves along" follows):
    /// <list type="bullet">
    /// <item>A pump or motor whose hub sits on a shaft axis (a mechanical shaft, the shaft of an e-machine or motor, the output
    /// stub of a hydraulic motor) or that is concentric with another hub turns with it: both share the speed <c>n</c>.</item>
    /// <item>The torque <c>T</c> is passed on over a direct one-to-one coupling (neither element coupled to a third one with a
    /// torque), and from the mechanical shaft or coupling the machine sits on when the shaft line has at most two members that
    /// take or give torque (the rule of the mechanical module: with a branch no torque is passed).</item>
    /// </list>
    /// With P = 2π·n·T on each machine, the drive's power is the pump's drive power. Elements of other modules only need the
    /// value keys <c>n</c> and <c>T</c>; the mechanical shaft and coupling type ids (documented in mechanical-drives.md) are the
    /// only ids named here. The process module keeps the same rules for its rotating machines (modules share no code).
    /// </summary>
    internal static class ShaftLinks
    {
        private static readonly Relation SameSpeed = Relation.Equality("fluid.shaft.speed", Common.T("Same speed on one shaft", "Gleiche Drehzahl auf einer Welle"));
        private static readonly Relation SameTorque = Relation.Equality("fluid.shaft.torque", Common.T("Torque passed on by the shaft coupling", "Drehmoment über die Wellenkupplung weitergegeben"));

        private static readonly ConditionalWeakTable<SketchDocument, ConditionalWeakTable<Registry, IReadOnlyList<(LinkKind Kind, string A, string B)>>> Graphs = new();

        /// <summary>Depth of <see cref="GraphOf"/> builds on this thread (re-entrancy guard).</summary>
        [ThreadStatic] private static int _building;

        /// <summary>
        /// The core coupling relations of a document (cached per document and registry; documents are immutable). Reading the
        /// anchors of every object can resolve values again (a linked characteristic chart solves its source): such a nested
        /// request sees no shaft partners instead of recursing without end.
        /// </summary>
        private static IReadOnlyList<(LinkKind Kind, string A, string B)> GraphOf(Registry registry, SketchDocument doc)
        {
            var perDoc = Graphs.GetValue(doc, _ => new ConditionalWeakTable<Registry, IReadOnlyList<(LinkKind, string, string)>>());
            if (perDoc.TryGetValue(registry, out var cached)) return cached;
            if (_building > 0) return Array.Empty<(LinkKind, string, string)>();
            _building++;
            try { return perDoc.GetValue(registry, r => Coupling.Graph(r, doc, Lang.En)); }
            finally { _building--; }
        }

        /// <summary>Elements that turn with <paramref name="id"/>: carried by it, carrying it or concentric with it (a loose hub turns freely).</summary>
        internal static IReadOnlyList<string> Partners(Registry registry, SketchDocument doc, string id)
        {
            var list = new List<string>();
            foreach (var (kind, a, b) in GraphOf(registry, doc))
            {
                if (kind is not (LinkKind.Carried or LinkKind.Bonded) || a != id && b != id) continue;
                if (kind == LinkKind.Carried && doc.Get(b)?.Str("hub", "fixed") == "loose") continue;
                var other = a == id ? b : a;
                if (!list.Contains(other)) list.Add(other);
            }
            return list;
        }

        private static bool Has(Registry registry, SketchDocument doc, string id, string key)
            => doc.Get(id) is { } x && ValueModel.Variable(registry.Type(x.Type), key) is not null;

        private static BoundRelation Eq(Relation r, string a, string b, string key)
            => new(r, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(a, key), ["b"] = new VarRef(b, key) });

        /// <summary>The shaft relations <paramref name="o"/> takes part in (empty without a registry or partners).</summary>
        public static IEnumerable<BoundRelation> For(SketchObject o, RenderContext ctx, Registry? owner)
        {
            var registry = ctx.Registry ?? owner;
            if (registry is null) return Array.Empty<BoundRelation>();
            var doc = ctx.Doc;
            if (!Has(registry, doc, o.Id, "n")) return Array.Empty<BoundRelation>();
            var partners = Partners(registry, doc, o.Id);
            if (partners.Count == 0) return Array.Empty<BoundRelation>();
            var list = new List<BoundRelation>();
            foreach (var p in partners)
                if (Has(registry, doc, p, "n")) list.Add(Eq(SameSpeed, o.Id, p, "n"));
            if (Has(registry, doc, o.Id, "T"))
            {
                var torque = partners.Where(p => Has(registry, doc, p, "T")).ToList();
                if (torque.Count == 1 && !IsMechanical(doc, torque[0])
                    && Partners(registry, doc, torque[0]).Count(q => Has(registry, doc, q, "T")) == 1)
                    list.Add(Eq(SameTorque, o.Id, torque[0], "T"));
                // On a mechanical shaft line the shaft carries the line torque when at most two members take or give torque:
                // the machine takes it from the shaft or coupling it sits on.
                var pass = partners.Where(p => IsPassThrough(doc, p) && Has(registry, doc, p, "T")).ToList();
                if (pass.Count > 0 && TorqueMembers(registry, doc, o.Id) <= 2)
                    foreach (var p in pass) list.Add(Eq(SameTorque, o.Id, p, "T"));
            }
            return list;
        }

        /// <summary>Elements of the mechanical module (shafts, couplings, gears): their shaft lines pass the torque themselves.</summary>
        private static bool IsMechanical(SketchDocument doc, string id) => doc.Get(id)?.Type.StartsWith("mech.", StringComparison.Ordinal) == true;

        /// <summary>Shafts and couplings of the mechanical module: they pass the torque of a shaft line on unchanged.</summary>
        private static readonly HashSet<string> PassThrough = new(StringComparer.Ordinal)
        {
            "mech.shaft", "mech.spline", "mech.coupling.rigid", "mech.coupling.flexible", "mech.joint.universal",
        };

        private static bool IsPassThrough(SketchDocument doc, string id) => doc.Get(id) is { } x && PassThrough.Contains(x.Type);

        /// <summary>
        /// Members of a shaft line (elements with a speed joined rigidly; bearings, nuts, planet carriers and gear sets stay out,
        /// as in the mechanical module) that take or give torque, i.e. have a torque and are no shaft or coupling.
        /// </summary>
        private static int TorqueMembers(Registry registry, SketchDocument doc, string start)
        {
            bool Joins(string id)
            {
                var type = doc.Get(id)?.Type ?? "";
                return Has(registry, doc, id, "n") && !type.StartsWith("mech.bearing.", StringComparison.Ordinal) && type != "mech.screwdrive.nut"
                    && type != "mech.carrier" && type != "mech.diffcase" && !type.StartsWith("mech.gearset.", StringComparison.Ordinal);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal) { start };
            var queue = new Queue<string>(new[] { start });
            var count = 0;
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (Has(registry, doc, id, "T") && !IsPassThrough(doc, id)) count++;
                foreach (var p in Partners(registry, doc, id))
                    if (Joins(p) && seen.Add(p)) queue.Enqueue(p);
            }
            return count;
        }
    }
}
