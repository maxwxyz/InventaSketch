using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>What a handle on the slide does (one look per meaning, docs/en/design/overlay-ux.md).</summary>
    internal enum HandleKind
    {
        /// <summary>PowerPoint's own handle (resize, rotate, move): drawn by PowerPoint, an obstacle only.</summary>
        Native,
        /// <summary>Orange diamond on a point of a line: drag moves the point.</summary>
        Point,
        /// <summary>Quick-add triangle of a diagram element (fixed place outside the frame).</summary>
        Triangle,
        /// <summary>Filled blue "+": adds something at this place (build port, free line end, table side).</summary>
        Add,
        /// <summary>Lock badge where the element touches another one and follows it.</summary>
        Lock,
        /// <summary>Junction icon where a wire or pipe crosses another one.</summary>
        Junction,
        /// <summary>Small hollow dot in the middle of a segment: a new corner (click or drag).</summary>
        Corner,
        /// <summary>Stub icon beside a segment: drag a branch out of the line.</summary>
        Branch,
    }

    /// <summary>
    /// One handle to place (screen pixels): where it belongs (<see cref="Anchor"/>), the way it moves out of the way
    /// (<see cref="Dir"/>), its preferred distance from the anchor and its radius (logical pixels, scaled by the device
    /// scale). Fixed handles stay at their anchor and are obstacles for the others; optional ones may be hidden.
    /// </summary>
    internal sealed record HandleSpec(string Id, HandleKind Kind, PointF Anchor, PointF Dir, float Offset, float Radius, int Priority)
    {
        /// <summary>Stays at its anchor (PowerPoint's handles, point diamonds, quick-add triangles).</summary>
        public bool Fixed { get; init; }
        /// <summary>May be hidden when there is no free place (corner dots, branch handles).</summary>
        public bool Optional { get; init; }
        /// <summary>How far beyond <see cref="Offset"/> the handle may be pushed (logical pixels).</summary>
        public float MaxPush { get; init; } = 36;
        /// <summary>May turn all around its anchor (lock badges, branch handles); otherwise it only leans by up to 40°.</summary>
        public bool Orbit { get; init; }
        /// <summary>Moves only along its direction, both ways (corner dots slide along their segment).</summary>
        public bool Along { get; init; }
        /// <summary>The tooltip shown while the pointer is over the handle.</summary>
        public string Tip { get; init; } = "";
    }

    /// <summary>A placed handle: its position on screen and whether it is shown.</summary>
    internal sealed record PlacedHandle(HandleSpec Spec, PointF At, bool Visible)
    {
        public string Id => Spec.Id;
        public HandleKind Kind => Spec.Kind;
    }

    /// <summary>
    /// Collision-free layout of the handles around a selected element (maintainer verdict 2026-11-25: on a short pipe the
    /// rotate handle, a lock badge, two "+" and the point diamonds lay on top of each other). All positions are computed
    /// first, then placed in priority order: fixed handles (PowerPoint's own handles, point diamonds, triangles) stay where
    /// they are; every other handle takes the first free place on a search around its preferred place (further out along
    /// its direction, then leaning to the sides; orbiting handles also go round the anchor); a free place keeps a minimum
    /// gap of <see cref="Gap"/> logical pixels to every handle placed before. Optional handles without a free place are
    /// hidden, the others take the place with the most room. Pure function, no PowerPoint (unit tested).
    /// </summary>
    internal static class HandleLayout
    {
        /// <summary>Minimum free space between two handles (logical pixels).</summary>
        public const float Gap = 3;

        /// <summary>Elements smaller than this on screen (logical pixels) get no optional handles at all.</summary>
        public const float SmallElement = 24;

        /// <summary>Radius of PowerPoint's resize handles and of its rotate / move handles (logical pixels, with some slack).</summary>
        public const float NativeRadius = 5, NativeBigRadius = 11;

        /// <summary>Distance of PowerPoint's rotation handle above the frame and of the move handle below small frames.</summary>
        public const float RotateAbove = 35, MoveBelow = 40, ThinFrame = 30;

        private static readonly float[] LeanAngles = { 0, 20, -20, 40, -40 };
        private static readonly float[] OrbitAngles = { 0, 30, -30, 60, -60, 90, -90, 135, -135, 180 };

        /// <summary>Places the handles; the result keeps the order of <paramref name="specs"/>.</summary>
        public static List<PlacedHandle> Resolve(IReadOnlyList<HandleSpec> specs, float scale)
        {
            var placed = new PlacedHandle?[specs.Count];
            var taken = new List<(PointF At, float R)>();
            // Fixed handles first (in their order), then the others by priority (stable).
            var order = Enumerable.Range(0, specs.Count)
                .OrderBy(i => specs[i].Fixed ? 0 : 1)
                .ThenByDescending(i => specs[i].Fixed ? 0 : specs[i].Priority)
                .ThenBy(i => i)
                .ToList();
            foreach (var i in order)
            {
                var s = specs[i];
                if (s.Fixed)
                {
                    placed[i] = new PlacedHandle(s, s.Anchor, s.Kind != HandleKind.Native);
                    taken.Add((s.Anchor, s.Radius * scale));
                    continue;
                }
                var best = s.Anchor;
                var bestRoom = float.MinValue;
                var found = false;
                foreach (var c in Candidates(s, scale))
                {
                    var room = Room(c, s.Radius * scale, taken, scale);
                    if (room >= 0) { best = c; found = true; break; }
                    if (room > bestRoom) { bestRoom = room; best = c; }
                }
                var visible = found || !s.Optional;
                placed[i] = new PlacedHandle(s, best, visible);
                if (visible) taken.Add((best, s.Radius * scale));
            }
            return placed.Select(p => p!).ToList();
        }

        /// <summary>Free room around a candidate (negative: it overlaps a handle placed before, gap included).</summary>
        internal static float Room(PointF c, float radius, IEnumerable<(PointF At, float R)> taken, float scale)
        {
            var room = float.MaxValue;
            foreach (var (at, r) in taken)
            {
                var d = (float)Math.Sqrt((c.X - at.X) * (c.X - at.X) + (c.Y - at.Y) * (c.Y - at.Y));
                room = Math.Min(room, d - r - radius - Gap * scale);
            }
            return room;
        }

        /// <summary>Candidate places in search order: the preferred place, then further out, leaning or orbiting.</summary>
        internal static IEnumerable<PointF> Candidates(HandleSpec s, float scale)
        {
            var len = (float)Math.Sqrt(s.Dir.X * s.Dir.X + s.Dir.Y * s.Dir.Y);
            var dir = len < 1e-6f ? new PointF(0, -1) : new PointF(s.Dir.X / len, s.Dir.Y / len);
            var step = Math.Max(4f, s.Radius) * scale;
            var rings = s.MaxPush <= 0 ? 0 : (int)Math.Ceiling(s.MaxPush * scale / step);
            var angles = s.Orbit ? OrbitAngles : LeanAngles;
            var list = new List<(float Cost, PointF At)>();
            if (s.Along)
            {
                for (var ring = 0; ring <= rings; ring++)
                {
                    var dist = s.Offset * scale + ring * step;
                    list.Add((ring, new PointF(s.Anchor.X + dir.X * dist, s.Anchor.Y + dir.Y * dist)));
                    if (ring > 0) list.Add((ring + 0.1f, new PointF(s.Anchor.X - dir.X * dist, s.Anchor.Y - dir.Y * dist)));
                }
                return list.OrderBy(x => x.Cost).Select(x => x.At);
            }
            for (var ring = 0; ring <= rings; ring++)
            {
                var dist = s.Offset * scale + ring * step;
                for (var k = 0; k < angles.Length; k++)
                {
                    // Without any distance a turn changes nothing: the anchor itself once.
                    if (dist < 1e-3f && k > 0) break;
                    var a = angles[k] * Math.PI / 180;
                    float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                    var d = new PointF(dir.X * cos - dir.Y * sin, dir.X * sin + dir.Y * cos);
                    list.Add((ring + k * 0.55f, new PointF(s.Anchor.X + d.X * dist, s.Anchor.Y + d.Y * dist)));
                }
            }
            return list.OrderBy(x => x.Cost).Select(x => x.At);
        }

        /// <summary>
        /// PowerPoint's own handles around a selected unit's frame (screen pixels): the eight resize handles (edge handles
        /// of thin frames are hidden, as PowerPoint does), the rotation handle above the top centre and, for small frames,
        /// the move handle below the bottom centre. Our handles keep clear of them.
        /// </summary>
        public static IEnumerable<HandleSpec> NativeHandles(RectangleF frame, float scale)
        {
            float l = frame.Left, t = frame.Top, r = frame.Right, b = frame.Bottom, cx = (l + r) / 2, cy = (t + b) / 2;
            // As PowerPoint (and the resize take-over in OnSlideController.HandleAt): device pixels.
            var thinW = frame.Width < ThinFrame;
            var thinH = frame.Height < ThinFrame;
            var pts = new List<(string, PointF)> { ("tl", new PointF(l, t)), ("tr", new PointF(r, t)), ("br", new PointF(r, b)), ("bl", new PointF(l, b)) };
            if (!thinW) { pts.Add(("t", new PointF(cx, t))); pts.Add(("b", new PointF(cx, b))); }
            if (!thinH) { pts.Add(("l", new PointF(l, cy))); pts.Add(("r", new PointF(r, cy))); }
            foreach (var (id, p) in pts)
                yield return new HandleSpec("native:" + id, HandleKind.Native, p, default, 0, NativeRadius, 1000) { Fixed = true };
            yield return new HandleSpec("native:rotate", HandleKind.Native, new PointF(cx, t - RotateAbove * scale), default, 0, NativeBigRadius, 1000) { Fixed = true };
            if (thinW || thinH)
                yield return new HandleSpec("native:move", HandleKind.Native, new PointF(cx, b + MoveBelow * scale), default, 0, NativeBigRadius, 1000) { Fixed = true };
        }

        /// <summary>True if two visible placed handles overlap (gap not counted): the layout's promise, for tests and logs.</summary>
        public static bool AnyOverlap(IReadOnlyList<PlacedHandle> handles, float scale, bool includeNative = true)
        {
            var shown = handles.Where(h => h.Visible || (includeNative && h.Kind == HandleKind.Native)).ToList();
            for (var i = 0; i < shown.Count; i++)
                for (var j = i + 1; j < shown.Count; j++)
                {
                    if (shown[i].Spec.Fixed && shown[j].Spec.Fixed) continue;
                    var dx = shown[i].At.X - shown[j].At.X;
                    var dy = shown[i].At.Y - shown[j].At.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) < (shown[i].Spec.Radius + shown[j].Spec.Radius) * scale - 0.01) return true;
                }
            return false;
        }

        /// <summary>The visible handle under a screen point (hit radius at least 7 logical pixels), or null.</summary>
        public static PlacedHandle? HitTest(IReadOnlyList<PlacedHandle> handles, PointF p, float scale)
        {
            PlacedHandle? best = null;
            var bestD = double.MaxValue;
            foreach (var h in handles)
            {
                if (!h.Visible || h.Kind == HandleKind.Native) continue;
                var tol = Math.Max(7f, h.Spec.Radius * 1.3f) * scale;
                var dx = h.At.X - p.X;
                var dy = h.At.Y - p.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= tol && d < bestD) { best = h; bestD = d; }
            }
            return best;
        }

        /// <summary>"id@x,y" of the visible handles (automation and log), PowerPoint's handles left out.</summary>
        public static string Describe(IEnumerable<PlacedHandle> handles)
            => string.Join(";", handles.Where(h => h.Visible && h.Kind != HandleKind.Native)
                .Select(h => FormattableString.Invariant($"{h.Id}@{h.At.X:0},{h.At.Y:0},{h.Spec.Radius:0.#}")));
    }
}
