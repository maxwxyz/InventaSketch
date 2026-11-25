using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// A place on an element where a click inserts something exactly there (interaction concept section 9 item 5: the "+"
    /// dots between the rows and columns of a table). <see cref="Position"/> is in the type's local coordinates for box
    /// objects (centred at 0,0 within <c>Size</c>) and in page coordinates for point objects; <see cref="InsertHandles.Of"/>
    /// returns page positions. <see cref="Direction"/> points away from the element (where a host may push the dot when
    /// it would cover another handle).
    /// </summary>
    public sealed record InsertHandle(string Key, Vec2 Position, Vec2 Direction, I18nText Label, Func<SketchObject, SketchObject> Apply);

    /// <summary>
    /// A type with insert places (<see cref="InsertHandle"/>). Kept apart from the context actions: hosts show the places as
    /// small "+" dots on the drawing, never as bar or pie entries (a table with ten rows would otherwise list nine
    /// "insert row" commands).
    /// </summary>
    public interface IInsertHandles
    {
        IEnumerable<InsertHandle> InsertHandles(SketchObject o, RenderContext ctx);
    }

    /// <summary>Reading the insert places of an object.</summary>
    public static class InsertHandles
    {
        /// <summary>The insert places of <paramref name="o"/> in page mm (directions turned with the box); empty for other types.</summary>
        public static IReadOnlyList<InsertHandle> Of(Registry registry, SketchObject o, RenderContext ctx)
        {
            if (registry.Type(o.Type) is not IInsertHandles type) return Array.Empty<InsertHandle>();
            try
            {
                var list = type.InsertHandles(o, ctx).ToList();
                if (o.IsPointGeometry) return list;
                var m = o.Matrix();
                return list.Select(h => h with { Position = m.Apply(h.Position), Direction = m.ApplyLinear(h.Direction).Normalized() }).ToList();
            }
            catch (Exception)
            {
                // A faulty type must not break the selection.
                return Array.Empty<InsertHandle>();
            }
        }

        /// <summary>The object with the insert place <paramref name="key"/> applied; unchanged for unknown keys.</summary>
        public static SketchObject Apply(Registry registry, SketchObject o, string key, RenderContext ctx)
            => (registry.Type(o.Type) as IInsertHandles)?.InsertHandles(o, ctx).FirstOrDefault(h => h.Key == key) is { } h ? h.Apply(o) : o;
    }
}
