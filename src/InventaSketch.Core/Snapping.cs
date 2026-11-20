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
        /// <summary>Semantic snapping: gear meshing, hubs on axes, concentric circles, ports.</summary>
        public bool Semantic { get; init; } = true;
        /// <summary>Smart alignment guides (equal x/y with other objects and the page).</summary>
        public bool Alignment { get; init; } = true;
        public bool Page { get; init; } = true;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GuideKind { Vertical, Horizontal, Point, Circle, Segment }

    /// <summary>Visual feedback for the UI (page coordinates).</summary>
    public sealed record SnapGuide(GuideKind Kind, Vec2 A, Vec2 B, double Radius = 0);

    /// <summary>Kind of the winning snap: mesh, axis, concentric, port, point, align, grid or none.</summary>
    public sealed record SnapResult(Vec2 Delta, IReadOnlyList<SnapGuide> Guides, string Kind)
    {
        public static SnapResult None(Vec2 delta) => new(delta, Array.Empty<SnapGuide>(), "none");
    }

    /// <summary>
    /// Snapping engine. Works on anchors in page coordinates, so it is identical for the sidebar preview,
    /// PowerPoint COM overlays and LibreOffice. Semantic snaps (gear mesh, hub on axis) win over generic
    /// point snaps, which win over alignment guides, which win over the grid.
    /// </summary>
    public static class Snapper
    {
        /// <summary>Collects snap targets on a page, excluding the moving objects.</summary>
        public static List<Anchor> Targets(Registry registry, SketchDocument doc, Page page, ICollection<string> exclude, RenderContext ctx, SnapOptions options)
        {
            var list = new List<Anchor>();
            if (options.Objects)
                foreach (var o in doc.ObjectsOn(page))
                {
                    if (exclude.Contains(o.Id) || registry.Type(o.Type) is null) continue;
                    list.AddRange(registry.PageAnchors(o, ctx));
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

        private sealed record Candidate(Vec2 Shift, double Score, string Kind, SnapGuide Guide);

        /// <summary>
        /// Snaps a move. <paramref name="moving"/> are the anchors of the moving selection at the drag start;
        /// <paramref name="delta"/> is the raw pointer delta. Returns the corrected delta.
        /// </summary>
        public static SnapResult SnapMove(IReadOnlyList<Anchor> moving, Vec2 delta, IReadOnlyList<Anchor> targets, SnapOptions options)
        {
            var tol = options.Tolerance;
            var moved = moving.Select(a => a.Transform(Matrix2D.Translation(delta.X, delta.Y))).ToList();
            var best = (Candidate?)null;
            void Consider(Candidate c) { if (c.Shift.Length <= tol * 1.0001 && (best is null || c.Score < best.Score)) best = c; }

            if (options.Semantic)
            {
                foreach (var m in moved)
                {
                    foreach (var t in targets)
                    {
                        // Gear meshing: pitch circles touch (external) or planet inside ring (internal).
                        if (m.Kind == AnchorKind.Circle && m.Role == AnchorRole.Pitch && t.Kind == AnchorKind.Circle && t.Role is AnchorRole.Pitch or AnchorRole.PitchInner)
                        {
                            var required = t.Role == AnchorRole.Pitch ? m.Radius + t.Radius : t.Radius - m.Radius;
                            if (required <= Geo.Eps) continue;
                            var d = m.P - t.P;
                            var dist = d.Length;
                            var dir = dist < Geo.Eps ? new Vec2(1, 0) : d / dist;
                            var shift = dir * (required - dist);
                            Consider(new Candidate(shift, shift.Length / tol, "mesh", new SnapGuide(GuideKind.Circle, t.P, t.P + dir * required, required)));
                        }
                        // Hub/centre onto a shaft axis.
                        if (m.Kind == AnchorKind.Point && m.Role is AnchorRole.Hub && t.Kind == AnchorKind.Segment && t.Role == AnchorRole.Axis)
                        {
                            var proj = Geo.ProjectOnLine(m.P, t.P, t.Q);
                            var shift = proj - m.P;
                            Consider(new Candidate(shift, 0.1 + shift.Length / tol, "axis", new SnapGuide(GuideKind.Segment, t.P, t.Q)));
                        }
                        // Moving shaft axis onto a hub (dragging a shaft through gears).
                        if (m.Kind == AnchorKind.Segment && m.Role == AnchorRole.Axis && t.Kind == AnchorKind.Point && t.Role is AnchorRole.Hub)
                        {
                            var proj = Geo.ProjectOnLine(t.P, m.P, m.Q);
                            var shift = t.P - proj;
                            Consider(new Candidate(shift, 0.1 + shift.Length / tol, "axis", new SnapGuide(GuideKind.Segment, m.P + shift, m.Q + shift)));
                        }
                        // Collinear axes (shaft continues another shaft).
                        if (m.Kind == AnchorKind.Segment && m.Role == AnchorRole.Axis && t.Kind == AnchorKind.Segment && t.Role == AnchorRole.Axis)
                        {
                            var dm = (m.Q - m.P).Normalized(); var dt = (t.Q - t.P).Normalized();
                            if (Math.Abs(dm.Cross(dt)) > 0.02) continue;
                            var proj = Geo.ProjectOnLine(m.P, t.P, t.Q);
                            var shift = proj - m.P;
                            Consider(new Candidate(shift, 0.2 + shift.Length / tol, "axis", new SnapGuide(GuideKind.Segment, t.P, t.Q)));
                        }
                        // Concentric: circle centres / hubs / centres.
                        if (m.Role is AnchorRole.Pitch or AnchorRole.PitchInner or AnchorRole.Hub && (t.Kind == AnchorKind.Circle || t.Role is AnchorRole.Hub))
                        {
                            if (m.Kind == AnchorKind.Segment || t.Kind == AnchorKind.Segment) continue;
                            var shift = t.P - m.P;
                            Consider(new Candidate(shift, 0.15 + shift.Length / tol, "concentric", new SnapGuide(GuideKind.Point, t.P, t.P)));
                        }
                        // Ports (electrical pins, diagram connection points).
                        if (m.Kind == AnchorKind.Point && m.Role == AnchorRole.Port && t.Kind == AnchorKind.Point && t.Role is AnchorRole.Port or AnchorRole.End)
                        {
                            var shift = t.P - m.P;
                            Consider(new Candidate(shift, 0.15 + shift.Length / tol, "port", new SnapGuide(GuideKind.Point, t.P, t.P)));
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
                    var shift = t.P - m.P;
                    var weight = m.Role == AnchorRole.Center && t.Role == AnchorRole.Center ? 0.3 : 0.4;
                    Consider(new Candidate(shift, weight + shift.Length / tol, "point", new SnapGuide(GuideKind.Point, t.P, t.P)));
                }
            }

            if (best is not null)
                return new SnapResult(delta + best.Shift, new[] { best.Guide }, best.Kind);

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

        /// <summary>Snaps a single free point (endpoint/handle drag, insertion point).</summary>
        /// <returns>The correction to add to <paramref name="p"/>.</returns>
        public static SnapResult SnapPoint(Vec2 p, IReadOnlyList<Anchor> targets, SnapOptions options)
            => SnapMove(new[] { Anchor.Point(AnchorRole.End, p) }, Vec2.Zero, targets, options);
    }
}
