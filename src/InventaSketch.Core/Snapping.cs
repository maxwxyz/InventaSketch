using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace InventaSketch.Core
{
    public sealed record SnapOptions
    {
        /// <summary>Capture distance in mm (callers convert from screen pixels at the current zoom).</summary>
        public double Tolerance { get; init; } = 2.0;
        /// <summary>Grid spacing in mm; null disables grid snapping.</summary>
        public double? Grid { get; init; } = 5;
        public bool Objects { get; init; } = true;
        /// <summary>Semantic snapping: gear meshing, hubs on axes, concentric circles, ports, mounts, rails.</summary>
        public bool Semantic { get; init; } = true;
        /// <summary>Smart alignment guides (equal x/y with other objects and the page).</summary>
        public bool Alignment { get; init; } = true;
        public bool Page { get; init; } = true;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GuideKind { Vertical, Horizontal, Point, Circle, Segment }

    /// <summary>Visual feedback for the UI (page coordinates).</summary>
    public sealed record SnapGuide(GuideKind Kind, Vec2 A, Vec2 B, double Radius = 0);

    /// <summary>
    /// Kind of the winning snap: mesh, axis, concentric, port, mount, rail, point, align, grid or none.
    /// </summary>
    public sealed record SnapResult(Vec2 Delta, IReadOnlyList<SnapGuide> Guides, string Kind)
    {
        /// <summary>
        /// Target anchor of the winning object snap (semantic or point), carrying its <see cref="Anchor.Owner"/> and
        /// <see cref="Anchor.Id"/>; null for alignment, grid and no snap. See <see cref="Attachments.FromSnap"/>.
        /// </summary>
        public Anchor? Target { get; init; }

        public static SnapResult None(Vec2 delta) => new(delta, Array.Empty<SnapGuide>(), "none");
    }

    /// <summary>
    /// Snapping engine. Works on anchors in page coordinates, so it is identical for the sidebar preview,
    /// PowerPoint COM overlays and LibreOffice.
    /// <para>
    /// Ranking: every candidate within the tolerance belongs to a tier; a lower tier always wins, inside a tier the
    /// lower score (rule weight + distance / tolerance) wins. Tier 0 are the semantic rules (gear mesh, hub on axis,
    /// concentric, port, mount, rail), tier 1 the generic point snaps (centre, corners, edge midpoints, ends). So a pin
    /// of a 2 mm junction dot beats the dot's own corners even if a corner is closer. Without an object snap, alignment
    /// guides apply, then the grid.
    /// </para>
    /// <list type="table">
    /// <item><term>mesh (0)</term><description>Pitch circle touching a Pitch circle, or inside a PitchInner circle of clearly larger radius; never with a pulley or sprocket (<see cref="Anchor.NoMesh"/>).</description></item>
    /// <item><term>axis (0.1 / 0.2)</term><description>Hub point onto an Axis segment, Axis onto a Hub, collinear Axis segments (0.2).</description></item>
    /// <item><term>concentric (0.15)</term><description>Pitch/PitchInner/Hub centre onto a circle centre or Hub (also a rotor in a stator: see <see cref="InternalMeshMaxRatio"/>).</description></item>
    /// <item><term>port (0.15)</term><description>Port onto Port/End; End onto Port.</description></item>
    /// <item><term>mount (0.15 / 0.14)</term><description>Mount point onto Mount point; directed faces that fit each other (<see cref="Anchor.Accepts"/>) 0.14.</description></item>
    /// <item><term>rail (0.15 / 0.45)</term><description>End/Port onto a Rail segment: its end points like ports (0.15), anywhere in between (0.45).</description></item>
    /// <item><term>point (0.3 / 0.4)</term><description>Generic point onto point; centre onto centre is preferred (0.3).</description></item>
    /// </list>
    /// </summary>
    public static class Snapper
    {
        /// <summary>
        /// A Pitch circle inside a PitchInner circle meshes as an internal gear pair only if its radius is below this
        /// fraction of the ring radius. An internal gear pair needs a clear tooth-count difference between ring and
        /// pinion; nearly equal radii describe a fit (rotor in a stator bore, part in a bore) and snap concentric.
        /// The criterion is a ratio, so it does not depend on the zoom-dependent tolerance.
        /// </summary>
        public const double InternalMeshMaxRatio = 0.9;

        private const int SemanticTier = 0, PointTier = 1;
        private const double MeshWeight = 0, AxisWeight = 0.1, ConcentricWeight = 0.15, PortWeight = 0.15, MountWeight = 0.15,
            CollinearWeight = 0.2, RailWeight = 0.45, CenterWeight = 0.3, PointWeight = 0.4;

        /// <summary>Collects snap targets on a page, excluding the moving objects. Object anchors carry their <see cref="Anchor.Owner"/>.</summary>
        public static List<Anchor> Targets(Registry registry, SketchDocument doc, Page page, ICollection<string> exclude, RenderContext ctx, SnapOptions options)
        {
            var list = new List<Anchor>();
            if (options.Objects)
                foreach (var o in doc.ObjectsOn(page))
                {
                    if (exclude.Contains(o.Id) || registry.Type(o.Type) is null) continue;
                    foreach (var a in registry.PageAnchors(o, ctx)) list.Add(a with { Owner = o.Id });
                }
            if (options.Page)
            {
                double w = page.Size.W, h = page.Size.H;
                list.Add(Anchor.Point(AnchorRole.Center, new Vec2(w / 2, h / 2), "page"));
                list.Add(Anchor.Point(AnchorRole.EdgeMid, new Vec2(w / 2, 0), "page"));
                list.Add(Anchor.Point(AnchorRole.EdgeMid, new Vec2(w / 2, h), "page"));
                list.Add(Anchor.Point(AnchorRole.EdgeMid, new Vec2(0, h / 2), "page"));
                list.Add(Anchor.Point(AnchorRole.EdgeMid, new Vec2(w, h / 2), "page"));
            }
            return list;
        }

        private sealed record Candidate(Vec2 Shift, int Tier, double Score, string Kind, IReadOnlyList<SnapGuide> Guides, Anchor Target);

        private static bool IsDocking(Anchor m) => m.Kind == AnchorKind.Point && m.Role is AnchorRole.End or AnchorRole.Port;

        /// <summary>
        /// Two directed mounts with ids that dock on each other (<see cref="Anchor.DocksOn"/> either way) and face each other.
        /// Mounts without an id or a direction never count, so only directed connection faces get the preference.
        /// </summary>
        private static bool Fits(Anchor m, Anchor t)
            => m.Id is not null && t.Id is not null && m.Direction is { } dm && t.Direction is { } dt && dm.Dot(dt) < -0.5
               && (m.DocksOn(t.Id) || t.DocksOn(m.Id));

        /// <summary>
        /// Snaps a move. <paramref name="moving"/> are the anchors of the moving selection at the drag start;
        /// <paramref name="delta"/> is the raw pointer delta. Returns the corrected delta.
        /// </summary>
        public static SnapResult SnapMove(IReadOnlyList<Anchor> moving, Vec2 delta, IReadOnlyList<Anchor> targets, SnapOptions options)
        {
            var tol = options.Tolerance;
            var moved = moving.Select(a => a.Transform(Matrix2D.Translation(delta.X, delta.Y))).ToList();
            var best = (Candidate?)null;
            void Consider(Vec2 shift, int tier, double weight, string kind, Anchor target, params SnapGuide[] guides)
            {
                if (shift.Length > tol * 1.0001) return;
                var score = weight + shift.Length / tol;
                if (best is null || tier < best.Tier || (tier == best.Tier && score < best.Score))
                    best = new Candidate(shift, tier, score, kind, guides, target);
            }
            SnapGuide PointGuide(Vec2 p) => new(GuideKind.Point, p, p);

            if (options.Semantic)
            {
                foreach (var m in moved)
                {
                    foreach (var t in targets)
                    {
                        // Gear meshing: pitch circles touch (external) or a clearly smaller planet inside a ring (internal).
                        // Pulleys and sprockets do not mesh (Anchor.NoMesh): they snap concentric onto hubs instead.
                        if (m.Kind == AnchorKind.Circle && m.Role == AnchorRole.Pitch && t.Kind == AnchorKind.Circle && t.Role is AnchorRole.Pitch or AnchorRole.PitchInner
                            && !m.NoMesh && !t.NoMesh)
                        {
                            var external = t.Role == AnchorRole.Pitch;
                            // Nearly equal radii inside a ring are a fit (rotor in stator), handled by the concentric rule.
                            var fit = !external && m.Radius >= InternalMeshMaxRatio * t.Radius;
                            var required = external ? m.Radius + t.Radius : t.Radius - m.Radius;
                            // Side views (axis in the drawing plane) mesh only with side views on a parallel axis, across it.
                            var ma = m.AxisOf();
                            var ta = t.AxisOf();
                            var sameKind = ma is null && ta is null || ma is { } mx && ta is { } tx && Math.Abs(mx.Cross(tx)) <= 0.05;
                            if (!fit && required > Geo.Eps && sameKind)
                            {
                                var d = m.P - t.P;
                                var dist = d.Length;
                                var dir = dist < Geo.Eps ? new Vec2(1, 0) : d / dist;
                                if (ta is { } axis)
                                {
                                    var n = new Vec2(-axis.Y, axis.X);
                                    dir = d.Dot(n) >= 0 ? n : n * -1;
                                }
                                Consider(t.P + dir * required - m.P, SemanticTier, MeshWeight, "mesh", t, new SnapGuide(GuideKind.Circle, t.P, t.P + dir * required, required));
                            }
                        }
                        // Rack and pinion: a gear seen along its axis tangent to a rack's pitch line on the teeth side, or the
                        // rack's pitch line moved onto such a gear (the gear's centre within the rack's length).
                        if (m.Kind == AnchorKind.Circle && m.Role == AnchorRole.Pitch && !m.NoMesh && m.AxisOf() is null && m.Radius > Geo.Eps
                            && t.TeethSide() is { } tn)
                        {
                            var (foot, _) = Geo.ClosestOnSegment(m.P, t.P, t.Q);
                            var target = foot + tn * m.Radius;
                            Consider(target - m.P, SemanticTier, MeshWeight, "mesh", t,
                                new SnapGuide(GuideKind.Segment, t.P, t.Q), new SnapGuide(GuideKind.Circle, target, foot, m.Radius));
                        }
                        if (m.TeethSide() is { } mn && t.Kind == AnchorKind.Circle && t.Role == AnchorRole.Pitch && !t.NoMesh && t.AxisOf() is null && t.Radius > Geo.Eps)
                        {
                            // Shift along the normal so the line touches the circle, then along the line so the contact lies on it.
                            var s = (t.P - m.P).Dot(mn);
                            var shift = mn * (s - t.Radius);
                            var (foot, _) = Geo.ClosestOnSegment(t.P - mn * t.Radius, m.P + shift, m.Q + shift);
                            shift += t.P - mn * t.Radius - foot;
                            Consider(shift, SemanticTier, MeshWeight, "mesh", t,
                                new SnapGuide(GuideKind.Segment, m.P + shift, m.Q + shift), new SnapGuide(GuideKind.Circle, t.P, t.P - mn * t.Radius, t.Radius));
                        }
                        // Hub/centre onto a shaft axis.
                        if (m.Kind == AnchorKind.Point && m.Role is AnchorRole.Hub && t.Kind == AnchorKind.Segment && t.Role == AnchorRole.Axis)
                            Consider(Geo.ProjectOnLine(m.P, t.P, t.Q) - m.P, SemanticTier, AxisWeight, "axis", t, new SnapGuide(GuideKind.Segment, t.P, t.Q));
                        // Moving shaft axis onto a hub (dragging a shaft through gears).
                        if (m.Kind == AnchorKind.Segment && m.Role == AnchorRole.Axis && t.Kind == AnchorKind.Point && t.Role is AnchorRole.Hub)
                        {
                            var shift = t.P - Geo.ProjectOnLine(t.P, m.P, m.Q);
                            Consider(shift, SemanticTier, AxisWeight, "axis", t, new SnapGuide(GuideKind.Segment, m.P + shift, m.Q + shift));
                        }
                        // Collinear axes (shaft continues another shaft).
                        if (m.Kind == AnchorKind.Segment && m.Role == AnchorRole.Axis && t.Kind == AnchorKind.Segment && t.Role == AnchorRole.Axis)
                        {
                            var dm = (m.Q - m.P).Normalized(); var dt = (t.Q - t.P).Normalized();
                            if (Math.Abs(dm.Cross(dt)) <= 0.02)
                                Consider(Geo.ProjectOnLine(m.P, t.P, t.Q) - m.P, SemanticTier, CollinearWeight, "axis", t, new SnapGuide(GuideKind.Segment, t.P, t.Q));
                        }
                        // Concentric: circle centres / hubs / centres.
                        if (m.Role is AnchorRole.Pitch or AnchorRole.PitchInner or AnchorRole.Hub && (t.Kind == AnchorKind.Circle || t.Role is AnchorRole.Hub)
                            && m.Kind != AnchorKind.Segment && t.Kind != AnchorKind.Segment)
                            Consider(t.P - m.P, SemanticTier, ConcentricWeight, "concentric", t, PointGuide(t.P));
                        if (m.Kind != AnchorKind.Point) continue;
                        // Ports (electrical pins, diagram connection points): Port onto Port/End, End onto Port.
                        if (t.Kind == AnchorKind.Point && (m.Role == AnchorRole.Port && t.Role is AnchorRole.Port or AnchorRole.End || m.Role == AnchorRole.End && t.Role == AnchorRole.Port))
                            Consider(t.P - m.P, SemanticTier, PortWeight, "port", t, PointGuide(t.P));
                        // Mounts (sensor onto vehicle, wall end onto wall end). Directed faces that fit each other (same id or
                        // an accepted one, facing each other: a gripper's flange onto a tool changer's tool side) go first.
                        if (m.Role == AnchorRole.Mount && t.Kind == AnchorKind.Point && t.Role == AnchorRole.Mount)
                            Consider(t.P - m.P, SemanticTier, Fits(m, t) ? MountWeight - 0.01 : MountWeight, "mount", t, PointGuide(t.P));
                        // Rails: end points like ports, anywhere in between with a lower priority.
                        if (IsDocking(m) && t.Kind == AnchorKind.Segment && t.Role == AnchorRole.Rail)
                        {
                            var rail = new SnapGuide(GuideKind.Segment, t.P, t.Q);
                            // Rail ends rank just behind ports and mounts at the same place (a wall corner stays a corner).
                            Consider(t.P - m.P, SemanticTier, PortWeight + 0.01, "rail", t, rail, PointGuide(t.P));
                            Consider(t.Q - m.P, SemanticTier, PortWeight + 0.01, "rail", t, rail, PointGuide(t.Q));
                            var (on, _) = Geo.ClosestOnSegment(m.P, t.P, t.Q);
                            Consider(on - m.P, SemanticTier, RailWeight, "rail", t, rail, PointGuide(on));
                        }
                    }
                }
            }

            // Generic point snaps (centre, corners, edge midpoints, ends).
            foreach (var m in moved)
            {
                if (m.Kind != AnchorKind.Point) continue;
                foreach (var t in targets)
                {
                    if (t.Kind != AnchorKind.Point) continue;
                    var weight = m.Role == AnchorRole.Center && t.Role == AnchorRole.Center ? CenterWeight : PointWeight;
                    Consider(t.P - m.P, PointTier, weight, "point", t, PointGuide(t.P));
                }
            }

            if (best is not null)
                return new SnapResult(delta + best.Shift, best.Guides, best.Kind) { Target = best.Target };

            // Alignment (independent per axis), then grid.
            var guides = new List<SnapGuide>();
            double? dx = null, dy = null;
            if (options.Alignment)
            {
                double bestX = tol, bestY = tol;
                SnapGuide? gx = null, gy = null;
                foreach (var m in moved)
                {
                    if (m.Kind != AnchorKind.Point || m.Role is not (AnchorRole.Center or AnchorRole.Corner or AnchorRole.EdgeMid or AnchorRole.Hub or AnchorRole.End)) continue;
                    foreach (var t in targets)
                    {
                        if (t.Kind != AnchorKind.Point) continue;
                        var ex = t.P.X - m.P.X;
                        if (Math.Abs(ex) < bestX) { bestX = Math.Abs(ex); dx = ex; gx = new SnapGuide(GuideKind.Vertical, t.P, new Vec2(t.P.X, m.P.Y)); }
                        var ey = t.P.Y - m.P.Y;
                        if (Math.Abs(ey) < bestY) { bestY = Math.Abs(ey); dy = ey; gy = new SnapGuide(GuideKind.Horizontal, t.P, new Vec2(m.P.X, t.P.Y)); }
                    }
                }
                if (gx is not null) guides.Add(gx);
                if (gy is not null) guides.Add(gy);
            }
            var reference = moved.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Center) ?? moved.FirstOrDefault();
            if (options.Grid is double g && g > Geo.Eps && reference is not null)
            {
                dx ??= Math.Round(reference.P.X / g) * g - reference.P.X;
                dy ??= Math.Round(reference.P.Y / g) * g - reference.P.Y;
            }
            var kind = guides.Count > 0 ? "align" : (dx.HasValue || dy.HasValue) ? "grid" : "none";
            return new SnapResult(delta + new Vec2(dx ?? 0, dy ?? 0), guides, kind);
        }

        /// <summary>Snaps a single free point (endpoint/handle drag, insertion point). It docks like an <see cref="AnchorRole.End"/>.</summary>
        /// <returns>The correction to add to <paramref name="p"/>.</returns>
        public static SnapResult SnapPoint(Vec2 p, IReadOnlyList<Anchor> targets, SnapOptions options)
            => SnapMove(new[] { Anchor.Point(AnchorRole.End, p) }, Vec2.Zero, targets, options);
    }
}
