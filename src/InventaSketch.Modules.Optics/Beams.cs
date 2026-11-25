using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>Ray bundles: docking on emitters, the "add ray bundle" action and the trace of a ray object.</summary>
    internal static class Beams
    {
        public const string AxisId = "optics.axis.line";
        /// <summary>Library item id of the light ray.</summary>
        public const string RayItem = RayType.TypeId;

        /// <summary>Ray objects whose start is docked on <paramref name="sourceId"/>.</summary>
        public static List<string> BundlesOf(SketchDocument doc, string sourceId)
            => doc.Objects.Values.Where(x => x.Type == RayType.TypeId && x.Attach is { Count: > 0 } a && a[0]?.ObjectId == sourceId).Select(x => x.Id).ToList();

        /// <summary>
        /// "Add ray bundle" on an emitter (a ray object docked on <paramref name="anchorId"/>), or "Remove ray bundle" when
        /// one is docked there already. The rays are traced live, so there is nothing to recompute later.
        /// </summary>
        public static ContextAction BundleAction(SketchObject o, SketchDocument? doc, string anchorId)
        {
            if (doc is not null && BundlesOf(doc, o.Id).Count > 0)
                return ContextAction.InDocument("optics.trace", new I18nText("Remove ray bundle", "Strahlenbündel entfernen"), IconIds.RayBundle,
                    (d, x, c) => Edits.Remove(d, BundlesOf(d, x.Id))) with { IsActive = true, Group = "rays" };
            return ContextAction.InDocument("optics.trace", new I18nText("Add ray bundle", "Strahlenbündel einfügen"), IconIds.RayBundle,
                (d, x, c) => AddBundle(d, x, anchorId)) with { Group = "rays" };
        }

        /// <summary>Adds a ray object whose start is docked on anchor <paramref name="anchorId"/> of <paramref name="source"/>.</summary>
        public static SketchDocument AddBundle(SketchDocument doc, SketchObject source, string anchorId)
        {
            var page = doc.PageOf(source.Id);
            if (page is null || source.IsPointGeometry || OpticsModule.TypeOf(source.Type) is not OpticsType type) return doc;
            var local = type.Anchors(source, new RenderContext(doc)).FirstOrDefault(a => a.Id == anchorId)?.P ?? Vec2.Zero;
            var m = source.Matrix();
            var p = m.Apply(local);
            var dir = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var (u, v) = source.ToBox(p);
            var ray = RayType.Instance.Create(p) with
            {
                Points = new[] { p, p + dir * 40 },
                Attach = new Attachment?[] { new Attachment(source.Id, u, v) { AnchorId = anchorId }, null },
            };
            return Edits.Add(doc, page.Id, ray);
        }

        /// <summary>
        /// Traces a ray object in its document: from the emitter its start is docked on (its rays and parameters), or as
        /// a single ray from its first point towards its second.
        /// </summary>
        public static TraceResult Trace(SketchObject ray, SketchDocument doc)
        {
            var pts = doc.ResolvePoints(ray);
            var scene = RayTracer.Scene(doc, ray);
            var tail = Geo.Clamp(ray.Num("tail", 0), 0, 5000);
            var virtualRays = ray.Flag("virtual", true);
            var source = ray.Attach is { Count: > 0 } att && att[0] is { } a ? doc.Get(a.ObjectId) : null;
            if (source is { IsPointGeometry: false } && OpticsModule.TypeOf(source.Type) is OpticsType st && st.Emit(source, scene) is { } em)
            {
                var sc = em.Exclude is null ? scene : scene.Where(e => e.Object.Id != em.Exclude).ToList();
                return RayTracer.Trace(sc, em.Rays, tail, 0, virtualRays);
            }
            if (pts.Count < 2) return new TraceResult();
            var d = pts[1] - pts[0];
            if (d.Length < 1e-9) d = new Vec2(1, 0);
            return RayTracer.Trace(scene, new[] { new StartRay(pts[0], d.Normalized()) }, tail, (pts[1] - pts[0]).Length, virtualRays);
        }
    }

    /// <summary>
    /// Light ray (point object in page coordinates). Docked with its start on a light source, lens, fibre end or object
    /// arrow it draws that element's bundle; otherwise it is a single ray from its first point towards its second. Either
    /// way it is traced live through the optical elements of the page (lenses, mirrors, prisms, splitters, stops, gratings,
    /// filters, detectors), so it follows when they move. With tracing switched off it is a freely drawn polyline.
    /// </summary>
    internal sealed class RayType : OpticsType
    {
        public const string TypeId = "optics.ray";

        public RayType() : base(TypeId, new I18nText("Light ray", "Lichtstrahl"), OpticsCategories.Beams,
            "ray", "light ray", "beam", "beam path", "ray trace", "strahl", "lichtstrahl", "strahlengang", "strahlenbündel") { }

        public static readonly ParamDef Arrows = Pm.Choice("arrows", "Arrowheads", "Pfeilspitzen", "middle",
            ("middle", "On every segment", "Auf jedem Abschnitt"), ("end", "At the end", "Am Ende"), ("none", "None", "Keine"));

        /// <summary>The shared instance (declared after <see cref="Arrows"/>, which its parameters use).</summary>
        public static readonly RayType Instance = new();

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[]
        {
            Pm.Bool("trace", "Trace through the elements", "Durch die Elemente verfolgen", true),
            Arrows,
            Pm.Bool("virtual", "Virtual rays (dashed extensions)", "Virtuelle Strahlen (gestrichelte Verlängerungen)", true),
            Pm.Num("tail", "Length after the last element (0 = automatic)", "Länge nach dem letzten Element (0 = automatisch)", 0, 0, 5000, "mm", 5),
        };

        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };
        public override bool IsPart => false;
        public override IReadOnlyList<string> Companions => new[] { Sources.LaserId, Sources.LampId, LensType.ConvergingId, MirrorType.PlaneId, Beams.AxisId };

        /// <summary>Rays are redrawn by the elements (their dependents), they have none of their own.</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Array.Empty<string>();

        private static Style RayStyle(string colour) => new() { Role = LineRole.Thin, StrokeWidth = 0.35, Stroke = colour };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < 2) return Array.Empty<Primitive>();
            var arrows = o.Str("arrows", "middle");
            if (!o.Flag("trace", true)) return Draw(new[] { (pts, (string?)null) }, arrows, OpticsTags.Ray + ".0", Ox.RayRed);
            TraceResult result;
            try { result = Beams.Trace(o, ctx.Doc); }
            catch (Exception)
            {
                // A drawing must never fail: fall back to the stored points.
                return Draw(new[] { (pts, (string?)null) }, arrows, OpticsTags.Ray + ".0", Ox.RayRed);
            }
            var baseColour = result.Dispersed ? Ox.RayWhite : Ox.RayRed;
            var list = new List<Primitive>();
            for (var i = 0; i < result.Paths.Count; i++)
            {
                var path = result.Paths[i];
                if (path.Points.Count < 2) continue;
                var tag = OpticsTags.Ray + "." + i.ToString(CultureInfo.InvariantCulture);
                // Runs of equal colour become one polyline each.
                var runs = new List<(IReadOnlyList<Vec2> Pts, string? Colour)>();
                var cur = new List<Vec2> { path.Points[0] };
                string? colour = Spectrum.Color(path.Lambdas[0]);
                for (var k = 0; k < path.Lambdas.Count; k++)
                {
                    var c = Spectrum.Color(path.Lambdas[k]);
                    if (c != colour)
                    {
                        runs.Add((cur, colour));
                        cur = new List<Vec2> { path.Points[k] };
                        colour = c;
                    }
                    cur.Add(path.Points[k + 1]);
                }
                runs.Add((cur, colour));
                list.AddRange(Draw(runs, arrows, tag, baseColour));
                foreach (var (a, b, lambda) in path.Virtual)
                    list.Add(new LinePrim(a, b, new Style { Role = LineRole.Hidden, StrokeWidth = 0.25, Stroke = Spectrum.Color(lambda) ?? baseColour }) { Tag = OpticsTags.Virtual + "." + i.ToString(CultureInfo.InvariantCulture) });
            }
            return list;
        }

        /// <summary>Polylines of one ray with its arrowheads (on every segment, at the end, or none).</summary>
        private static List<Primitive> Draw(IReadOnlyList<(IReadOnlyList<Vec2> Pts, string? Colour)> runs, string arrows, string tag, string baseColour)
        {
            var list = new List<Primitive>();
            for (var r = 0; r < runs.Count; r++)
            {
                var (pts, c) = runs[r];
                if (pts.Count < 2) continue;
                var style = RayStyle(c ?? baseColour);
                if (arrows == "end" && r == runs.Count - 1) style = style with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
                list.Add((pts.Count == 2 ? (Primitive)new LinePrim(pts[0], pts[1], style) : Prim.Polyline(pts, style)) with { Tag = tag });
                if (arrows != "middle") continue;
                var head = RayStyle(c ?? baseColour) with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
                for (var k = 0; k + 1 < pts.Count; k++)
                {
                    var seg = pts[k + 1] - pts[k];
                    if (seg.Length < 8) continue;
                    var d = seg.Normalized();
                    var mid = Vec2.Lerp(pts[k], pts[k + 1], 0.5);
                    list.Add(new LinePrim(mid - d * 2.0, mid + d * 0.9, head) { Tag = tag });
                }
            }
            return list;
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Ray, new I18nText("Rays", "Strahlen"), "line");
            if (o.Flag("trace", true) && o.Flag("virtual", true))
                yield return new PartInfo(OpticsTags.Virtual, new I18nText("Virtual rays", "Virtuelle Strahlen"), "line");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.Toggle(o, "optics.ray.trace", new I18nText("Trace through elements", "Durch Elemente verfolgen"), "trace", true, "ray");
            yield return Ox.Cycle(o, "optics.ray.arrows", new I18nText("Arrowheads", "Pfeilspitzen"), Arrows, "ray");
            yield return Ox.Toggle(o, "optics.ray.virtual", new I18nText("Virtual rays", "Virtuelle Strahlen"), "virtual", true, "ray");
            yield return Ox.NumberInput(o, "optics.ray.tail", new I18nText("Length after the last element", "Länge nach dem letzten Element"), Parameters[3], "ray");
        }
    }

    /// <summary>Optical axis: thin dash-dot centre line (point object). Its segment is a rail, so element ports dock on it.</summary>
    internal sealed class OpticalAxisType : OpticsType
    {
        public OpticalAxisType() : base(Beams.AxisId, new I18nText("Optical axis", "Optische Achse"), OpticsCategories.Beams,
            "optical axis", "axis", "centre line", "center line", "optische achse", "achse", "mittellinie", "symmetrieachse") { }

        public override IReadOnlyList<ParamDef> Parameters { get; } = new[] { Pm.Label };
        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-60, 0), new Vec2(60, 0) };
        public override bool IsPart => false;
        public override IReadOnlyList<string> Companions => new[] { LensType.ConvergingId, LensType.DivergingId, Sources.ObjectId, MirrorType.ConcaveId, Detectors.ScreenId };

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Array.Empty<string>();
        public override bool DependsOnNeighbours => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < 2) return Array.Empty<Primitive>();
            var style = Style.CenterLine with { StrokeWidth = 0.25 };
            var list = new List<Primitive> { (pts.Count == 2 ? (Primitive)new LinePrim(pts[0], pts[1], style) : Prim.Polyline(pts, style)) with { Tag = OpticsTags.Axis } };
            var label = o.Str("label", "");
            if (!string.IsNullOrWhiteSpace(label))
            {
                var end = pts[pts.Count - 1];
                var d = (end - pts[pts.Count - 2]).Normalized();
                list.Add(new TextPrim(end + d * 1.5, label, new TextStyle { Size = Ox.LabelSize, Anchor = d.X >= 0 ? TextAnchor.Start : TextAnchor.End }) { Tag = OpticsTags.Label });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                yield return Anchor.Rail(pts[i], pts[i + 1], "axis" + i.ToString(CultureInfo.InvariantCulture));
                yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(pts[i], pts[i + 1], 0.5));
            }
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Axis, new I18nText("Optical axis", "Optische Achse"), "line");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc) => Array.Empty<ContextAction>();
    }
}
