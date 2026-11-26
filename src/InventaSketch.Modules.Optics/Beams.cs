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
        /// <summary>
        /// The emitter whose bundle <paramref name="ray"/> draws (the element its start is docked on, if that element emits),
        /// or null for a free ray.
        /// </summary>
        public static SketchObject? Emitter(SketchObject ray, SketchDocument doc)
        {
            var source = ray.Attach is { Count: > 0 } att && att[0] is { } a ? doc.Get(a.ObjectId) : null;
            return source is { IsPointGeometry: false } && OpticsModule.TypeOf(source.Type) is OpticsType st
                && st.Emit(source, Array.Empty<SceneElement>()) is not null ? source : null;
        }

        public static TraceResult Trace(SketchObject ray, SketchDocument doc)
        {
            var pts = doc.ResolvePoints(ray);
            var scene = RayTracer.Scene(doc, ray);
            var tail = Geo.Clamp(ray.Num("tail", 0), 0, 5000);
            var virtualRays = ray.Flag("virtual", true);
            var ghosts = ray.Flag(RayType.GhostsKey, false);
            var source = ray.Attach is { Count: > 0 } att && att[0] is { } a ? doc.Get(a.ObjectId) : null;
            if (source is { IsPointGeometry: false } && OpticsModule.TypeOf(source.Type) is OpticsType st && st.Emit(source, scene) is { } em)
            {
                var sc = em.Exclude is null ? scene : scene.Where(e => e.Object.Id != em.Exclude).ToList();
                return RayTracer.Trace(sc, em.Rays, tail, 0, virtualRays, ghosts);
            }
            if (pts.Count < 2) return new TraceResult();
            var d = pts[1] - pts[0];
            if (d.Length < 1e-9) d = new Vec2(1, 0);
            return RayTracer.Trace(scene, new[] { new StartRay(pts[0], d.Normalized()) }, tail, (pts[1] - pts[0]).Length, virtualRays, ghosts);
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
            ValueLabels.ShowValues,
            Pm.Bool(GhostsKey, "Ghost reflections at glass faces", "Geisterreflexe an Glasflächen", false, advanced: true),
        };

        /// <summary>
        /// Boolean parameter: trace the light that uncoated or coated glass faces reflect partially as rays of their own
        /// (ghosts). Faces with ideal glass reflect nothing, thin lenses are not included.
        /// </summary>
        public const string GhostsKey = "ghosts";

        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };
        public override bool IsPart => false;
        public override IReadOnlyList<string> Companions => new[] { Sources.LaserId, Sources.LampId, LensType.ConvergingId, MirrorType.PlaneId, Beams.AxisId };

        /// <summary>Rays are redrawn by the elements (their dependents), they have none of their own.</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Array.Empty<string>();

        /// <summary>A ray is traced through the elements of its page.</summary>
        public override bool DependsOnNeighbours => true;
        public override bool HasDependents => false;

        private static Style RayStyle(string colour) => new() { Role = LineRole.Thin, StrokeWidth = 0.35, Stroke = colour };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < 2) return Array.Empty<Primitive>();
            var arrows = o.Str("arrows", "middle");
            if (!o.Flag("trace", true)) return Draw(new[] { (pts, (string?)null, 1.0) }, arrows, OpticsTags.Ray + ".0", Ox.RayRed, false);
            TraceResult result;
            try { result = Beams.Trace(o, ctx.Doc); }
            catch (Exception)
            {
                // A drawing must never fail: fall back to the stored points.
                return Draw(new[] { (pts, (string?)null, 1.0) }, arrows, OpticsTags.Ray + ".0", Ox.RayRed, false);
            }
            var baseColour = result.Dispersed ? Ox.RayWhite : Ox.RayRed;
            var list = new List<Primitive>();
            for (var i = 0; i < result.Paths.Count; i++)
            {
                var path = result.Paths[i];
                if (path.Points.Count < 2) continue;
                var tag = OpticsTags.Ray + "." + i.ToString(CultureInfo.InvariantCulture);
                // Runs of equal colour and intensity become one polyline each.
                var runs = new List<(IReadOnlyList<Vec2> Pts, string? Colour, double Intensity)>();
                var cur = new List<Vec2> { path.Points[0] };
                string? colour = Spectrum.Color(path.Lambdas[0]);
                var intensity = Level(path.Intensities[0]);
                for (var k = 0; k < path.Lambdas.Count; k++)
                {
                    var c = Spectrum.Color(path.Lambdas[k]);
                    var level = Level(path.Intensities[k]);
                    if (c != colour || level != intensity)
                    {
                        runs.Add((cur, colour, intensity));
                        cur = new List<Vec2> { path.Points[k] };
                        colour = c;
                        intensity = level;
                    }
                    cur.Add(path.Points[k + 1]);
                }
                runs.Add((cur, colour, intensity));
                list.AddRange(Draw(runs, arrows, tag, baseColour, path.Fan));
                foreach (var (a, b, lambda) in path.Virtual)
                    list.Add(new LinePrim(a, b, new Style { Role = LineRole.Hidden, StrokeWidth = 0.25, Stroke = Spectrum.Color(lambda) ?? baseColour }) { Tag = OpticsTags.Virtual + "." + i.ToString(CultureInfo.InvariantCulture) });
                if (ValueLabels.Shows(o) && !path.Fan) list.Add(EndLabel(path, ctx.Lang));
            }
            return list;
        }

        /// <summary>
        /// The values label at the end of a traced ray: its relative intensity and polarisation there ("46 % (linear 0°)"),
        /// above the last point (right of it for a ray running up or down).
        /// </summary>
        private static TextPrim EndLabel(TracedPath path, Lang lang)
        {
            var k = path.Intensities.Count - 1;
            var end = path.Last;
            var d = (end - path.Points[path.Points.Count - 2]).Normalized();
            var text = Readout(path.Intensities[k], path.Polarizations[k], lang);
            TextStyle style;
            Vec2 at;
            if (Math.Abs(d.X) >= 0.3)
            {
                // A ray running sideways: the text above its end, ending (or starting) there.
                at = end + new Vec2(0, -0.8);
                style = new TextStyle { Size = ValueLabels.TextSize, Anchor = d.X > 0 ? TextAnchor.End : TextAnchor.Start, Baseline = TextBaseline.Bottom };
            }
            else
            {
                // A ray running up or down: the text to the right of its end.
                at = end + new Vec2(0.8, 0);
                style = new TextStyle { Size = ValueLabels.TextSize, Anchor = TextAnchor.Start, Baseline = TextBaseline.Middle };
            }
            return new TextPrim(at, text, style) { Tag = ValueLabels.Part };
        }

        /// <summary>"46 %", "50 % (linear 0°)", "25 % (circular)", "86 % (16 % polarised)" in the language of the page.</summary>
        internal static string Readout(double intensity, Coherency? pol, Lang lang)
        {
            var de = lang == Lang.De;
            var pct = 100 * Geo.Clamp(intensity, 0, 1);
            var text = Percent(pct, de);
            if (pol is not Coherency c || c.Degree < 0.005) return text;
            if (c.Degree < 0.995) return text + (de ? " (" + Percent(100 * c.Degree, de) + " polarisiert)" : " (" + Percent(100 * c.Degree, de) + " polarised)");
            var circ = Math.Abs(c.Circularity);
            if (circ > 0.995) return text + (de ? " (zirkular)" : " (circular)");
            if (circ > 0.02) return text + (de ? " (elliptisch)" : " (elliptical)");
            var angle = Math.Round(c.Orientation, MidpointRounding.AwayFromZero);
            if (angle <= -90) angle += 180;
            if (angle == 0) angle = 0;   // no "-0"
            return text + " (linear " + angle.ToString("0", CultureInfo.InvariantCulture) + "°)";
        }

        private static string Percent(double pct, bool de)
        {
            var s = Math.Round(pct, pct < 9.95 ? 1 : 0, MidpointRounding.AwayFromZero).ToString(pct < 9.95 ? "0.0" : "0", CultureInfo.InvariantCulture);
            return (de ? s.Replace('.', ',') : s) + " %";
        }

        /// <summary>Intensity rounded to a drawing level (steps of 0.01), so tiny numerical differences do not split a ray.</summary>
        private static double Level(double intensity) => Math.Round(Geo.Clamp(intensity, 0, 1), 2);

        /// <summary>
        /// The ray style of one run: weaker light (behind a polariser) is drawn more transparent (opacity = relative intensity,
        /// at least 0.2); scan fan rays of a scanner mirror are thin and dashed.
        /// </summary>
        private static Style RunStyle(string colour, double intensity, bool fan)
        {
            var style = RayStyle(colour);
            if (intensity < 0.995) style = style with { Opacity = Math.Max(0.2, intensity) };
            if (fan) style = style with { Role = LineRole.Hidden, StrokeWidth = 0.25 };
            return style;
        }

        /// <summary>Polylines of one ray with its arrowheads (on every segment, at the end, or none; fan rays get none in the middle).</summary>
        private static List<Primitive> Draw(IReadOnlyList<(IReadOnlyList<Vec2> Pts, string? Colour, double Intensity)> runs, string arrows, string tag, string baseColour, bool fan)
        {
            var list = new List<Primitive>();
            for (var r = 0; r < runs.Count; r++)
            {
                var (pts, c, intensity) = runs[r];
                if (pts.Count < 2) continue;
                var style = RunStyle(c ?? baseColour, intensity, fan);
                if (arrows == "end" && r == runs.Count - 1) style = style with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
                list.Add((pts.Count == 2 ? (Primitive)new LinePrim(pts[0], pts[1], style) : Prim.Polyline(pts, style)) with { Tag = tag });
                if (arrows != "middle" || fan) continue;
                var head = RunStyle(c ?? baseColour, intensity, false) with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
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

        /// <summary>
        /// A free traced ray: its start direction as a grip on the drawn ray (halfway to the second point or to the first
        /// element it meets), dragged around the start (whole degrees, the length kept). A ray docked on an emitter takes its
        /// direction from the emitter (turn the emitter, or drag the emitter's grips) and has no grip of its own.
        /// </summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            if (!o.Flag("trace", true) || o.Points is not { Count: >= 2 } || Beams.Emitter(o, ctx.Doc) is not null) yield break;
            var pts = ctx.Doc.ResolvePoints(o);
            var d = pts[1] - pts[0];
            var len = d.Length;
            if (len < 1e-6) yield break;
            var reach = len;
            try
            {
                if (Beams.Trace(o, ctx.Doc).Paths.FirstOrDefault() is { Points: { Count: >= 2 } } path)
                    reach = Math.Min(len, path.Points[1].DistanceTo(pts[0]));
            }
            catch (Exception)
            {
                // A grip must never fail: stay on the stored segment.
            }
            var deg = Geo.RadToDeg(Math.Atan2(d.Y, d.X));
            yield return new DragHandle(Grips.Direction, pts[0] + d * (0.5 * reach / len), DragConstraint.Free,
                Grips.Label("Start angle", "Startwinkel", Dragging.Snap(deg, 0.1), "°")) { Part = OpticsTags.Ray };
        }

        /// <summary>The direction grip turns the second point around the first (whole degrees, clockwise on screen), keeping the length.</summary>
        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            if (key != Grips.Direction || o.Points is not { Count: >= 2 } stored) return o;
            var pts = ctx.Doc.ResolvePoints(o);
            var len = pts[1].DistanceTo(pts[0]);
            var v = pagePos - pts[0];
            if (len < 1e-6 || v.Length < 1e-6) return o;
            var rad = Geo.DegToRad(Dragging.Snap(Geo.RadToDeg(Math.Atan2(v.Y, v.X)), 1));
            var points = stored.ToArray();
            points[0] = pts[0];
            points[1] = pts[0] + new Vec2(Math.Cos(rad), Math.Sin(rad)) * len;
            // A second point docked somewhere is released: it now lies where the grip put it.
            var attach = o.Attach is { Count: > 1 } a && a[1] is not null ? a.Select((x, i) => i == 1 ? null : x).ToArray() : o.Attach;
            return o with { Points = points, Attach = attach };
        }

        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            yield return new PartInfo(OpticsTags.Ray, new I18nText("Rays", "Strahlen"), "line");
            if (o.Flag("trace", true) && o.Flag("virtual", true))
                yield return new PartInfo(OpticsTags.Virtual, new I18nText("Virtual rays", "Virtuelle Strahlen"), "line");
            if (o.Flag("trace", true) && ValueLabels.Shows(o))
                yield return new PartInfo(ValueLabels.Part, new I18nText("Intensity and polarisation", "Intensität und Polarisation"), "label");
        }

        protected override IEnumerable<ContextAction> OwnActions(SketchObject o, SketchDocument? doc)
        {
            yield return Ox.Toggle(o, "optics.ray.trace", new I18nText("Trace through elements", "Durch Elemente verfolgen"), "trace", true, "ray");
            foreach (var opt in Ox.Choice(o, "optics.ray.arrows", new I18nText("Arrowheads", "Pfeilspitzen"), Arrows, "ray")) yield return opt;
            yield return Ox.Toggle(o, "optics.ray.virtual", new I18nText("Virtual rays", "Virtuelle Strahlen"), "virtual", true, "ray");
            yield return Ox.NumberInput(o, "optics.ray.tail", new I18nText("Length after the last element", "Länge nach dem letzten Element"), Parameters[3], "ray");
            if (o.Flag("trace", true))
            {
                yield return Ox.Toggle(o, "optics.ray.values", new I18nText("Values at ray ends", "Werte an Strahlenden"), ValueLabels.ShowParam, false, "ray");
                yield return Ox.Toggle(o, "optics.ray.ghosts", new I18nText("Ghost reflections", "Geisterreflexe"), GhostsKey, false, "ray");
            }
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
        public override bool HasDependents => false;

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
