using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// How a <see cref="DragHandle"/> may move, in the object's own (local) frame: freely, only horizontally (left/right) or
    /// only vertically (up/down). A rotated object turns the allowed direction with it (see <see cref="DragHandle.Direction"/>).
    /// </summary>
    public enum DragConstraint { Free, Horizontal, Vertical }

    /// <summary>
    /// An interactive handle of a box object that the user can drag to change a value directly on the drawing (a limit line or
    /// value line of a chart, a vertical marker, a mark on an axis …). Hosts draw the handles of the selection on their overlay
    /// and, on drop, call <see cref="ObjectType.MoveHandle"/> (engine: <c>moveHandle</c>) with the pointer position.
    /// </summary>
    /// <param name="Key">Stable id of the handle within its object ("limit.0", "ref.2", "xmark.1" …).</param>
    /// <param name="Position">Where the handle sits, in page mm.</param>
    /// <param name="Constraint">Allowed motion in the object's local frame.</param>
    /// <param name="Label">What the handle changes, with its current value ("Limit line: 5.5").</param>
    public sealed record DragHandle(string Key, Vec2 Position, DragConstraint Constraint, I18nText Label)
    {
        /// <summary>Part key of the element the handle belongs to (charts v2: "limit", "ref.0" …), or null.</summary>
        public string? Part { get; init; }

        /// <summary>
        /// Unit vector of the allowed motion in page coordinates (constrained handles; it follows the object's rotation and
        /// mirroring); <see cref="Vec2.Zero"/> for <see cref="DragConstraint.Free"/>.
        /// </summary>
        public Vec2 Direction { get; init; }

        /// <summary>
        /// Part key whose selection shows this handle too (progressive disclosure): the point handles of a curve
        /// ("point.0.3") appear when the curve ("series.0") or one of its points is selected; elsewhere only near the pointer.
        /// </summary>
        public string? ShowWith { get; init; }

        /// <summary>
        /// The handle has no grip of its own: hosts drag it when the pointer is pressed on its <see cref="Part"/> while that part
        /// is selected (a whole curve dragged by grabbing the curve). <see cref="Position"/> is a point of the part; the host
        /// moves it by the pointer's offset and calls <see cref="ObjectType.MoveHandle"/> with the moved position.
        /// </summary>
        public bool ByPart { get; init; }
    }

    /// <summary>Helpers shared by the types that offer drag handles and by the hosts that drag them.</summary>
    public static class Dragging
    {
        /// <summary>
        /// A handle computed in the object's local coordinates (box centred at 0,0) moved to page coordinates: the position is
        /// transformed by the object's matrix and the allowed direction follows rotation and mirroring.
        /// </summary>
        public static DragHandle ToPage(DragHandle local, SketchObject o)
        {
            var m = o.Matrix();
            var dir = local.Constraint switch
            {
                DragConstraint.Horizontal => m.ApplyLinear(new Vec2(1, 0)).Normalized(),
                DragConstraint.Vertical => m.ApplyLinear(new Vec2(0, 1)).Normalized(),
                _ => Vec2.Zero,
            };
            return local with { Position = m.Apply(local.Position), Direction = dir };
        }

        /// <summary>
        /// The pointer position <paramref name="pagePos"/> restricted to what the handle allows: projected onto the line
        /// through the handle along <see cref="DragHandle.Direction"/>; unchanged for free handles.
        /// </summary>
        public static Vec2 Constrain(DragHandle handle, Vec2 pagePos)
        {
            var d = handle.Direction;
            if (handle.Constraint == DragConstraint.Free || d.Length < Geo.Eps) return pagePos;
            var t = (pagePos - handle.Position).Dot(d);
            return handle.Position + d * t;
        }

        /// <summary>
        /// <paramref name="value"/> rounded to a multiple of <paramref name="resolution"/> (no rounding for a resolution that
        /// is not positive), cleaned of binary noise (0.30000000000000004 becomes 0.3).
        /// </summary>
        public static double Snap(double value, double resolution)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || !(resolution > 0) || double.IsInfinity(resolution)) return value;
            var v = Math.Round(value / resolution) * resolution;
            // Decimals of the resolution (plus one) remove the floating point residue of the multiplication.
            var decimals = (int)Math.Max(0, Math.Min(15, Math.Ceiling(-Math.Log10(resolution)) + 1));
            v = Math.Round(v, decimals);
            return v == 0 ? 0 : v; // no negative zero
        }

        /// <summary>
        /// <paramref name="value"/> rounded to <paramref name="digits"/> significant digits (log axes: 1.2·10³, 4.7·10⁻²).
        /// </summary>
        public static double SnapSignificant(double value, int digits = 2)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value)) return value;
            var e = Math.Floor(Math.Log10(Math.Abs(value)));
            var scale = Math.Pow(10, e - Math.Max(1, digits) + 1);
            var v = Math.Round(value / scale) * scale;
            var decimals = (int)Math.Max(0, Math.Min(15, -(e - Math.Max(1, digits) + 1)));
            return Math.Round(v, decimals);
        }

        /// <summary>The handle with <paramref name="key"/>, or null.</summary>
        public static DragHandle? Find(IEnumerable<DragHandle> handles, string key) => handles.FirstOrDefault(h => h.Key == key);
    }
}
