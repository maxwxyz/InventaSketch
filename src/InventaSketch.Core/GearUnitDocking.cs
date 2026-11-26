using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// A member port of a gear unit (<see cref="IGearUnit"/>): the id of a <see cref="AnchorRole.Hub"/> point of the unit and
    /// the member a part docked there sits on. <see cref="Centre"/> marks the centre bore, which yields to a port at a shaft
    /// stub that the same part also touches. The id may also name a pitch circle of the unit (<see cref="AnchorRole.Pitch"/>,
    /// such as the outer teeth of a ring gear): a gear meshing there meshes with that member
    /// (<see cref="GearUnitDocking.MeshPort"/>).
    /// </summary>
    public sealed record GearUnitPort(string AnchorId, string Member, bool Centre = false);

    /// <summary>
    /// Optional for types whose members turn at different speeds (gear sets, a CVT): they join no shaft line as a whole but
    /// dock each member on its own shaft through a member port (<see cref="GearUnitDocking"/>). The core uses it for the
    /// moving gear train (<see cref="GearTrain"/>: the member speeds follow <see cref="SpeedRelations"/> instead of turning
    /// every docked part rigidly together) and for the torque rule of shaft lines (<see cref="ShaftLines"/>: a docked member
    /// takes or gives torque like a member of the line). Types implementing it for some objects only return no ports for the
    /// others.
    /// </summary>
    public interface IGearUnit
    {
        /// <summary>The member ports of <paramref name="o"/> in its current view (empty: not a gear unit).</summary>
        IReadOnlyList<GearUnitPort> MemberPorts(SketchObject o);

        /// <summary>
        /// Linear relations between the signed member speeds (about the unit's axis, same sense positive): each row is
        /// Σ k·ω_member = 0 with the member names of <see cref="MemberPorts"/> (members without a port, such as a ring gear,
        /// may appear). A held member is a row with itself alone. Fixed-ratio units give ω_out = f(ω_in); multi-DOF sets
        /// (planetary, differential) leave the docked members unresolved unless a member is held.
        /// </summary>
        IReadOnlyList<IReadOnlyDictionary<string, double>> SpeedRelations(SketchObject o);
    }

    /// <summary>
    /// Which member of a gear unit a part is docked on: a part whose shaft axis passes through a member port, or whose hub is
    /// concentric with it (within <see cref="Tolerance"/>, as the coupling), sits on that member. Ports at shaft stubs take
    /// precedence over the centre bore; a part touching two different members (a shaft through both stubs) would lock the
    /// unit and is docked on none. Shared by <see cref="GearTrain"/>, <see cref="ShaftLines"/> and the module topologies so
    /// they count docked members the same way.
    /// </summary>
    public static class GearUnitDocking
    {
        /// <summary>Docking tolerance of a port hub on a shaft axis or another hub (mm, as <see cref="Coupling"/>).</summary>
        public const double Tolerance = 0.3;

        /// <summary>The member ports of <paramref name="o"/> (empty when its type is no <see cref="IGearUnit"/>).</summary>
        public static IReadOnlyList<GearUnitPort> PortsOf(Registry registry, SketchObject? o)
            => o is not null && !o.IsPointGeometry && registry.Type(o.Type) is IGearUnit u ? u.MemberPorts(o) : Array.Empty<GearUnitPort>();

        /// <summary>True if <paramref name="o"/> is a gear unit with member ports.</summary>
        public static bool IsUnit(Registry registry, SketchObject? o) => PortsOf(registry, o).Count > 0;

        /// <summary>
        /// The port of <paramref name="unit"/> that <paramref name="part"/> is docked on, or null. <paramref name="anchorsOf"/>
        /// returns page anchors of an object (callers cache them).
        /// </summary>
        public static GearUnitPort? DockedPort(Registry registry, SketchObject unit, SketchObject part, Func<SketchObject, IReadOnlyList<Anchor>> anchorsOf)
        {
            var ports = PortsOf(registry, unit);
            if (ports.Count == 0) return null;
            var pa = anchorsOf(part);
            bool On(Vec2 h) => pa.Any(x => x.Kind == AnchorKind.Segment && x.Role == AnchorRole.Axis && Geo.ClosestOnSegment(h, x.P, x.Q).Point.DistanceTo(h) <= Tolerance)
                               || pa.Any(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Hub && x.P.DistanceTo(h) <= Tolerance);
            var hits = new List<GearUnitPort>();
            foreach (var a in anchorsOf(unit))
            {
                if (a.Kind != AnchorKind.Point || a.Role != AnchorRole.Hub || a.Id is not { } id || !On(a.P)) continue;
                var port = ports.FirstOrDefault(p => p.AnchorId == id);
                if (port is not null) hits.Add(port);
            }
            var chosen = hits.Any(h => !h.Centre) ? hits.Where(h => !h.Centre).ToList() : hits;
            return chosen.Count > 0 && chosen.Select(h => h.Member).Distinct(StringComparer.Ordinal).Count() == 1 ? chosen[0] : null;
        }

        /// <summary>
        /// The port whose teeth form the pitch circle the coupling meshes <paramref name="unit"/> with (its first
        /// <see cref="AnchorRole.Pitch"/> or <see cref="AnchorRole.PitchInner"/> circle, as <see cref="Coupling"/> reads it), when
        /// that circle's id names a port: a gear meshing there turns with that member (the outer teeth of a ring gear). Null
        /// for other units and for pitch circles that belong to no single member. <paramref name="anchors"/> are the unit's
        /// page anchors.
        /// </summary>
        public static GearUnitPort? MeshPort(Registry registry, SketchObject unit, IReadOnlyList<Anchor> anchors)
        {
            var ports = PortsOf(registry, unit);
            if (ports.Count == 0) return null;
            var pitch = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner);
            return pitch?.Id is { } id ? ports.FirstOrDefault(p => p.AnchorId == id) : null;
        }

        /// <summary><see cref="DockedPort(Registry, SketchObject, SketchObject, Func{SketchObject, IReadOnlyList{Anchor}})"/> with fresh page anchors.</summary>
        public static GearUnitPort? DockedPort(Registry registry, SketchDocument doc, SketchObject unit, SketchObject part, Lang lang = Lang.En)
        {
            var ctx = new RenderContext(doc, lang).For(registry);
            return DockedPort(registry, unit, part, o => registry.PageAnchors(o, ctx).ToList());
        }
    }
}
