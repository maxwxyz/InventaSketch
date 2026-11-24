using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Switching a whole installation between plan, section and schematic while it stays the same installation
    /// (reference implementation of the mapping; meant to move into Core next to <see cref="AssemblyViews"/>).
    /// <list type="bullet">
    /// <item>The section looks at the plan from its lower page edge (towards −y), like the wall elevation of the architecture
    /// module. Plan and section/schematic share x: every element keeps its centre x and its box width.</item>
    /// <item>plan → section/schematic: the plan y is dropped (kept in <see cref="SketchObject.Meta"/>); the bottom edge goes to
    /// <c>floorY − height / scale</c> (<c>height</c> = mounting height in mm, <c>scale</c> = plan scale denominator). The floor
    /// line <c>floorY</c> is the lowest plan edge of the selection unless given.</item>
    /// <item>section ↔ schematic: x and the bottom edge stay; only the symbol (and its box height) changes.</item>
    /// <item>section/schematic → plan: the stored plan y and rotation come back; an element moved up or down in the elevation
    /// gets its new mounting height (<c>(floorY − bottom) × scale</c>).</item>
    /// <item>Lines: plan → elevation puts every point at <c>(x, floorY − height / scale)</c> and keeps the plan points; back in
    /// plan they are restored. Ends docked on ports follow their element (<see cref="Attachments.Refresh"/>).</item>
    /// </list>
    /// </summary>
    public static class InstallationViews
    {

        /// <summary>View ids of installation elements: plan, section (side/elevation at the mounting height), schematic.</summary>
        public const string Plan = "plan", Section = "section", Schematic = "schematic";

        /// <summary>The three views in their canonical order.</summary>
        public static readonly IReadOnlyList<string> AllViews = new[] { Plan, Section, Schematic };

        /// <summary>Parameter key of the mounting height in mm (bottom edge above the finished floor).</summary>
        public const string HeightKey = "height";

        /// <summary>Plan scale denominator when neither the object (param "scale") nor the document sets one.</summary>
        public const double DefaultScale = 100;

        /// <summary>Plan scale of one object: its own "scale" parameter, else the document plan scale, else 1:100.</summary>
        public static double ScaleOf(SketchObject o, SketchDocument? doc)
        {
            var own = o.Num("scale", 0);
            if (own > Geo.Eps) return own;
            return doc?.Settings.PlanScale is double s && s > Geo.Eps ? s : DefaultScale;
        }

        /// <summary>Mounting height in mm of an element; the type's default when unset.</summary>
        public static double HeightOf(SketchObject o, ObjectType? type)
        {
            var def = type?.Parameters.FirstOrDefault(p => p.Key == HeightKey)?.Default?.Number ?? 0;
            return o.Num(HeightKey, def);
        }
        /// <summary>The rule object for <see cref="ObjectType.AssemblyViewSwitch"/>.</summary>
        public static readonly IAssemblyViewSwitch Rule = new SwitchRule();

        private sealed class SwitchRule : IAssemblyViewSwitch
        {
            public SketchDocument Switch(Registry registry, SketchDocument doc, IReadOnlyList<string> ids, string view, Lang lang, double? groundY)
                => InstallationViews.Switch(registry, doc, ids, view, null, lang, groundY);
        }

        /// <summary>Meta key: plan y of the element centre (box) before it went to an elevation view.</summary>
        public const string MetaPlanY = "bs.planY";
        /// <summary>Meta key: plan rotation (degrees) of the element before it went to an elevation view.</summary>
        public const string MetaPlanRotation = "bs.planRotation";
        /// <summary>Meta key: plan points of a line ("x,y;x,y;…") before it went to an elevation view.</summary>
        public const string MetaPlanPoints = "bs.planPoints";
        /// <summary>Meta key: page y of the floor line used for the elevation views.</summary>
        public const string MetaFloorY = "bs.floorY";

        /// <summary>True if the type offers the three installation views (plan, section, schematic).</summary>
        public static bool TakesPart(ObjectType? type) => type is not null && AllViews.All(v => type.Views.Any(x => x.Id == v));

        /// <summary>Page y of the bottom edge of an element with mounting height <paramref name="heightMm"/> in an elevation view.</summary>
        public static double BottomY(double floorY, double heightMm, double scale) => floorY - heightMm / Math.Max(Geo.Eps, scale);

        /// <summary>Mounting height in mm of an element whose bottom edge sits at <paramref name="bottomY"/> in an elevation view.</summary>
        public static double HeightAt(double floorY, double bottomY, double scale) => (floorY - bottomY) * scale;

        /// <summary>
        /// The objects <paramref name="ids"/> switched to <paramref name="view"/> with the installation kept (see the class).
        /// The floor line is <paramref name="floorY"/>, else the one stored by an earlier switch, else the ground line of walls
        /// switched together with the installation (<paramref name="groundY"/>), else the lowest plan edge of the selection.
        /// </summary>
        public static SketchDocument Switch(Registry registry, SketchDocument doc, IReadOnlyList<string> ids, string view, double? floorY = null, Lang lang = Lang.En, double? groundY = null)
        {
            if (!AllViews.Contains(view)) return doc;
            var ctx = new RenderContext(doc, lang);
            var objects = ids.Select(doc.Get).Where(o => o is not null && TakesPart(registry.Type(o.Type))).Select(o => o!)
                .Where(o => registry.Type(o.Type)!.ViewOf(o) != view).ToList();
            if (objects.Count == 0) return doc;

            var floor = floorY ?? StoredFloor(objects) ?? groundY ?? PlanBottom(doc, objects.Where(o => registry.Type(o.Type)!.ViewOf(o) == Plan));
            var updated = new Dictionary<string, SketchObject>(StringComparer.Ordinal);
            foreach (var o in objects)
            {
                var type = registry.Type(o.Type)!;
                var from = type.ViewOf(o);
                var scale = ScaleOf(o, doc);
                var h = HeightOf(o, type);
                updated[o.Id] = o.IsPointGeometry
                    ? SwitchLine(o, from, view, floor, h, scale, ctx)
                    : SwitchBox(registry, o, from, view, floor, h, scale, ctx);
            }
            var next = Edits.Update(doc, updated.Keys.ToList(), o => updated[o.Id]);
            next = Attachments.Refresh(registry, next, new RenderContext(next, lang));
            return DockOnRails(registry, next, updated.Values.Where(o => o.IsPointGeometry).Select(o => o.Id).ToList(), lang);
        }

        /// <summary>
        /// Line ends docked on a port the target does not have in its new view (a pipe on the riser port of the plan) slide
        /// onto the target's rail at the line's own height (the riser seen in section). The port id is kept, so the end
        /// returns to the port when the plan comes back.
        /// </summary>
        private static SketchDocument DockOnRails(Registry registry, SketchDocument doc, IReadOnlyList<string> lineIds, Lang lang)
        {
            var ctx = new RenderContext(doc, lang);
            foreach (var id in lineIds)
            {
                var line = doc.Get(id);
                if (line?.Attach is null || line.Points is null) continue;
                var attach = line.Attach.ToList();
                var changed = false;
                for (var i = 0; i < attach.Count && i < line.Points.Count; i++)
                {
                    var a = attach[i];
                    if (a?.AnchorId is null || doc.Get(a.ObjectId) is not { IsPointGeometry: false } target) continue;
                    var anchors = registry.PageAnchors(target, ctx).ToList();
                    if (anchors.Any(x => x.Id == a.AnchorId && x.Kind == AnchorKind.Point)) continue;
                    var want = line.Points[i];
                    var rail = anchors.Where(x => x.Role == AnchorRole.Rail && x.Kind == AnchorKind.Segment)
                        .OrderBy(x => Geo.ClosestOnSegment(want, x.P, x.Q).Point.DistanceTo(want)).FirstOrDefault();
                    if (rail is null) continue;
                    var (u, v) = target.ToBox(Geo.ClosestOnSegment(want, rail.P, rail.Q).Point);
                    attach[i] = a with { U = u, V = v };
                    changed = true;
                }
                if (changed) doc = Edits.Replace(doc, line with { Attach = attach });
            }
            return doc;
        }

        private static SketchObject SwitchBox(Registry registry, SketchObject o, string from, string to, double floor, double h, double scale, RenderContext ctx)
        {
            var bottom = o.Transform.Y + o.Size.H / 2;
            if (from == Plan)
            {
                // Plan → elevation: keep x, drop y (stored), stand on the floor line at the mounting height.
                var meta = WithMeta(o.Meta, (MetaPlanY, F(o.Transform.Y)), (MetaPlanRotation, F(o.Transform.Rotation)), (MetaFloorY, F(floor)));
                var s = registry.WithView(o, to, ctx);
                var y = BottomY(floor, h, scale) - s.Size.H / 2;
                return s with { Meta = meta, Transform = o.Transform with { Y = R(y), Rotation = 0, FlipY = false } };
            }
            var fl = Num(o.Meta, MetaFloorY) ?? floor;
            if (to == Plan)
            {
                // Elevation → plan: the height follows where the element stands now; the plan place comes back.
                var height = Math.Round(HeightAt(fl, bottom, scale), 1);
                var s = registry.WithView(o, to, ctx).WithParam(HeightKey, height);
                var y = Num(o.Meta, MetaPlanY) ?? fl - s.Size.H / 2;
                var rot = Num(o.Meta, MetaPlanRotation) ?? 0;
                return s with { Transform = o.Transform with { Y = R(y), Rotation = rot } };
            }
            // Section ↔ schematic: x and the bottom edge stay.
            var t = registry.WithView(o, to, ctx);
            return t with { Transform = o.Transform with { Y = R(bottom - t.Size.H / 2) } };
        }

        private static SketchObject SwitchLine(SketchObject o, string from, string to, double floor, double h, double scale, RenderContext ctx)
        {
            var pts = o.Points ?? Array.Empty<Vec2>();
            if (from == Plan)
            {
                var meta = WithMeta(o.Meta, (MetaPlanPoints, string.Join(";", pts.Select(p => F(p.X) + "," + F(p.Y)))), (MetaFloorY, F(floor)));
                var y = R(BottomY(floor, h, scale));
                return o with { View = to, Meta = meta, Points = pts.Select(p => new Vec2(p.X, y)).ToList() };
            }
            if (to != Plan) return o with { View = to };
            var fl = Num(o.Meta, MetaFloorY) ?? floor;
            var resolved = ctx.Doc.ResolvePoints(o);
            var free = Enumerable.Range(0, resolved.Count).Where(i => o.Attach is null || i >= o.Attach.Count || o.Attach[i] is null).ToList();
            var avgY = (free.Count > 0 ? free.Select(i => resolved[i].Y) : resolved.Select(p => p.Y)).DefaultIfEmpty(BottomY(fl, h, scale)).Average();
            var line = o.WithParam(HeightKey, Math.Round(HeightAt(fl, avgY, scale), 1)) with { View = to };
            var stored = ParsePoints(o.Meta);
            return stored is not null && stored.Count == pts.Count ? line with { Points = stored } : line;
        }

        /// <summary>Floor line already used by selected objects in an elevation view.</summary>
        private static double? StoredFloor(IEnumerable<SketchObject> objects)
            => objects.Select(o => Num(o.Meta, MetaFloorY)).FirstOrDefault(v => v is not null);

        /// <summary>Lowest plan edge (largest y) of the given objects, 0 if none.</summary>
        private static double PlanBottom(SketchDocument doc, IEnumerable<SketchObject> objects)
        {
            var ys = objects.SelectMany(o => o.IsPointGeometry ? doc.ResolvePoints(o).Select(p => p.Y) : new[] { o.Bounds().Bottom }).ToList();
            return ys.Count == 0 ? 0 : ys.Max();
        }

        private static IReadOnlyDictionary<string, string> WithMeta(IReadOnlyDictionary<string, string>? meta, params (string Key, string Value)[] kv)
        {
            var d = meta is null ? new Dictionary<string, string>(StringComparer.Ordinal) : meta.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            foreach (var (k, v) in kv) d[k] = v;
            return d;
        }

        private static double? Num(IReadOnlyDictionary<string, string>? meta, string key)
            => meta is not null && meta.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

        private static List<Vec2>? ParsePoints(IReadOnlyDictionary<string, string>? meta)
        {
            if (meta is null || !meta.TryGetValue(MetaPlanPoints, out var s) || string.IsNullOrEmpty(s)) return null;
            var list = new List<Vec2>();
            foreach (var part in s.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length != 2 || !double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    || !double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return null;
                list.Add(new Vec2(x, y));
            }
            return list;
        }

        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static double R(double v) => Math.Round(v, 6);
    }
}
