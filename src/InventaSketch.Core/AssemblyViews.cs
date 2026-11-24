using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Switching a whole assembly (gear train, drive) between views while the mechanism stays the same. Views either show
    /// the axes in the drawing plane (stick, side, section: the axis is a line) or look along the axes (axial: the axis is
    /// a point). Within one kind every part keeps its place. Across kinds the layout is projected:
    /// <list type="bullet">
    /// <item>axes in the plane → along the axes: the coordinate along the axis is dropped, so parts on one shaft become
    /// concentric and meshing gears keep their centre distance;</item>
    /// <item>along the axes → axes in the plane: meshing gears lie in one plane (same position along the axis), parts on
    /// one axis are stacked along it, and the gear train is unfolded into the drawing plane with its centre distances.</item>
    /// </list>
    /// </summary>
    /// <summary>A domain's own rule for switching a whole installation's view (<see cref="ObjectType.AssemblyViewSwitch"/>).</summary>
    public interface IAssemblyViewSwitch
    {
        /// <summary>The objects <paramref name="ids"/> (all of this rule's types) switched to <paramref name="view"/>.</summary>
        SketchDocument Switch(Registry registry, SketchDocument doc, IReadOnlyList<string> ids, string view, Lang lang);
    }

    public static class AssemblyViews
    {
        /// <summary>Gap between parts stacked on one axis (mm) when the axes come into the drawing plane.</summary>
        public const double StackGap = 4;

        /// <summary>The objects <paramref name="ids"/> switched to <paramref name="view"/> with the layout kept (see the class).</summary>
        public static SketchDocument Switch(Registry registry, SketchDocument doc, IReadOnlyList<string> ids, string view, Lang lang = Lang.En)
        {
            // Objects whose types bring their own rule (building services: plan ↔ section ↔ schematic) go there, grouped.
            var own = ids.Select(id => (Id: id, Rule: doc.Get(id) is { } x ? registry.Type(x.Type)?.AssemblyViewSwitch : null)).ToList();
            foreach (var group in own.Where(x => x.Rule is not null).GroupBy(x => x.Rule!))
                doc = group.Key.Switch(registry, doc, group.Select(x => x.Id).ToList(), view, lang);
            ids = own.Where(x => x.Rule is null).Select(x => x.Id).ToList();
            if (ids.Count == 0) return doc;
            var ctx = new RenderContext(doc, lang);
            var objects = ids.Select(doc.Get).Where(o => o is not null && !o.IsPointGeometry).Select(o => o!).ToList();
            if (objects.Count == 0) return doc;
            var before = objects.ToDictionary(o => o.Id, o => (Centre: Centre(o), InPlane: InPlane(registry, o, ctx), Axis: AxisDir(registry, o, ctx)), StringComparer.Ordinal);
            var switched = Edits.Update(doc, objects.Select(o => o.Id).ToList(), o => registry.WithView(o, view, ctx));
            var ctx1 = new RenderContext(switched, lang);
            var after = objects.ToDictionary(o => o.Id, o => InPlane(registry, switched.Get(o.Id)!, ctx1), StringComparer.Ordinal);
            var toAxial = objects.Where(o => before[o.Id].InPlane && !after[o.Id]).Select(o => o.Id).ToList();
            var toPlane = objects.Where(o => !before[o.Id].InPlane && after[o.Id]).Select(o => o.Id).ToList();
            if (toAxial.Count == 0 && toPlane.Count == 0) return switched;
            var centres = objects.ToDictionary(o => o.Id, o => before[o.Id].Centre, StringComparer.Ordinal);
            var r = before[objects[0].Id].Centre;

            if (toAxial.Count > 0)
            {
                // Looking along the axes: drop the coordinate along the (common) axis.
                var axes = toAxial.Select(id => before[id].Axis).Where(a => a is not null).Select(a => a!.Value).ToList();
                var a = axes.Count > 0 ? axes[0] : new Vec2(1, 0);
                foreach (var id in toAxial)
                {
                    var c = centres[id];
                    centres[id] = c - a * (c - r).Dot(a);
                }
            }

            if (toPlane.Count > 0)
            {
                // Axes into the drawing plane (horizontal): meshing gears share their place along the axis, parts on one
                // axis are stacked along it, the train unfolds with its centre distances (upwards or downwards as before).
                var graph = Coupling.Graph(registry, doc, lang);
                var set = new HashSet<string>(toPlane, StringComparer.Ordinal);
                var mesh = graph.Where(g => g.Kind == LinkKind.Mesh && set.Contains(g.A) && set.Contains(g.B)).ToList();
                var s = new Dictionary<string, double>(StringComparer.Ordinal);
                var p = new Dictionary<string, double>(StringComparer.Ordinal);
                var order = toPlane.ToList();
                double Width(string id) => Math.Max(1, switched.Get(id)!.Size.W);
                // Gears meshing with each other (directly or through planets) lie in one plane: a sun and its ring gear are
                // concentric but not stacked.
                var plane = order.ToDictionary(id => id, id => id, StringComparer.Ordinal);
                string Find(string x) { while (plane[x] != x) x = plane[x] = plane[plane[x]]; return x; }
                foreach (var (_, a, b) in mesh) plane[Find(a)] = Find(b);
                // Extent already used along each axis (key: the first part placed on it).
                var axisEnd = new Dictionary<string, double>(StringComparer.Ordinal);
                string AxisKey(string id) => order.First(x => before[x].Centre.DistanceTo(before[id].Centre) <= 0.3 && s.ContainsKey(x));
                foreach (var root in order)
                {
                    if (s.ContainsKey(root)) continue;
                    s[root] = before[root].Centre.X - r.X;
                    p[root] = before[root].Centre.Y;
                    axisEnd[root] = s[root] + Width(root) / 2;
                    var queue = new Queue<string>();
                    queue.Enqueue(root);
                    while (queue.Count > 0)
                    {
                        var id = queue.Dequeue();
                        // Meshing partners: the same place along the axis, the centre distance across it.
                        foreach (var (_, a, b) in mesh.Where(m => m.A == id || m.B == id))
                        {
                            var other = a == id ? b : a;
                            if (s.ContainsKey(other)) continue;
                            var d = before[other].Centre - before[id].Centre;
                            var sign = Math.Abs(d.Y) > 1e-6 ? Math.Sign(d.Y) : (d.X >= 0 ? 1 : -1);
                            s[other] = s[id];
                            p[other] = p[id] + sign * d.Length;
                            var key = order.FirstOrDefault(x => x != other && before[x].Centre.DistanceTo(before[other].Centre) <= 0.3 && s.ContainsKey(x));
                            if (key is null) axisEnd[other] = s[other] + Width(other) / 2;
                            else axisEnd[AxisKey(key)] = Math.Max(axisEnd[AxisKey(key)], s[other] + Width(other) / 2);
                            queue.Enqueue(other);
                        }
                        // Parts on the same axis in another plane: stacked along the axis behind what is there already.
                        foreach (var other in order.Where(x => !s.ContainsKey(x) && before[x].Centre.DistanceTo(before[id].Centre) <= 0.3).ToList())
                        {
                            var key = AxisKey(id);
                            if (Find(other) == Find(id)) s[other] = s[id];
                            else s[other] = axisEnd[key] + StackGap + Width(other) / 2;
                            p[other] = p[id];
                            axisEnd[key] = Math.Max(axisEnd[key], s[other] + Width(other) / 2);
                            queue.Enqueue(other);
                        }
                    }
                }
                foreach (var id in toPlane) centres[id] = new Vec2(r.X + s[id], p[id]);
            }

            var moved = toAxial.Concat(toPlane).ToList();
            return Edits.Update(switched, moved, o => o with
            {
                Transform = o.Transform with
                {
                    X = Math.Round(centres[o.Id].X, 6),
                    Y = Math.Round(centres[o.Id].Y, 6),
                    Rotation = toPlane.Contains(o.Id) ? 0 : o.Transform.Rotation,
                },
            });
        }

        private static Vec2 Centre(SketchObject o) => new(o.Transform.X, o.Transform.Y);

        /// <summary>True if the object shows its axis in the drawing plane (an axis line or a side-view pitch circle).</summary>
        public static bool InPlane(Registry registry, SketchObject o, RenderContext ctx)
            => AxisDir(registry, o, ctx) is not null;

        private static Vec2? AxisDir(Registry registry, SketchObject o, RenderContext ctx)
        {
            foreach (var a in registry.PageAnchors(o, ctx))
            {
                if (a.Kind == AnchorKind.Circle && a.AxisOf() is { } ax) return ax;
                if (a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis && a.Q.DistanceTo(a.P) > 1e-9) return (a.Q - a.P).Normalized();
            }
            return null;
        }
    }
}
