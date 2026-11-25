using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// A matplotlib hatch pattern read into line families and shape grids (matplotlib.hatch): '/' lines at 45°, '\\' at 135°,
    /// '|' vertical, '-' horizontal, '+' both of the last two, 'x' (or 'X') both diagonals, 'o' small circles, 'O' large
    /// circles, '.' small filled dots, '*' filled stars. A character repeated makes that part denser ("//" is twice as dense
    /// as "/"); characters combine ("/o", "|-", "x."). Drawn natively: line families as hatch lines (one of them as the hatch
    /// fill of the area's closed shape, the others as explicit lines), shapes as small closed shapes inside the area – every
    /// host keeps them editable lines and shapes (docs/en/modules/charts.md "Areas").
    /// </summary>
    internal sealed record AreaPattern(IReadOnlyList<(double Angle, int Count)> Lines, IReadOnlyList<(char Shape, int Count)> Shapes)
    {
        /// <summary>Characters a hatch pattern may contain.</summary>
        public const string Chars = "/\\|-+xXoO.*";

        /// <summary>Longest pattern kept.</summary>
        public const int MaxLength = 12;

        /// <summary>The pattern without unknown characters (at most <see cref="MaxLength"/>), or null when nothing is left.</summary>
        public static string? Clean(string? hatch)
        {
            if (string.IsNullOrEmpty(hatch)) return null;
            var s = new string(hatch!.Where(c => Chars.IndexOf(c) >= 0).Take(MaxLength).ToArray());
            return s.Length == 0 ? null : s;
        }

        public bool IsEmpty => Lines.Count == 0 && Shapes.Count == 0;

        public static AreaPattern Parse(string? hatch)
        {
            var lines = new Dictionary<double, int>();
            var shapes = new Dictionary<char, int>();
            void Line(double a) => lines[a] = lines.TryGetValue(a, out var n) ? n + 1 : 1;
            foreach (var c in Clean(hatch) ?? "")
            {
                switch (c)
                {
                    case '/': Line(45); break;
                    case '\\': Line(135); break;
                    case '|': Line(90); break;
                    case '-': Line(0); break;
                    case '+': Line(0); Line(90); break;
                    case 'x': case 'X': Line(45); Line(135); break;
                    case 'o': case 'O': case '.': case '*':
                        shapes[c] = shapes.TryGetValue(c, out var k) ? k + 1 : 1; break;
                }
            }
            return new AreaPattern(lines.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList(),
                shapes.Select(kv => (kv.Key, kv.Value)).ToList());
        }

        /// <summary>Spacing of the line family drawn <paramref name="count"/> times dense, from the spacing of one character.</summary>
        public static double LineSpacing(double unit, int count) => unit / Math.Max(1, count);

        /// <summary>Row spacing of a shape grid drawn <paramref name="count"/> times dense (shapes a little sparser than lines).</summary>
        public static double ShapeSpacing(double unit, int count) => 1.25 * unit / Math.Max(1, count);

        /// <summary>Diameter of a shape relative to its row spacing (matplotlib: o 0.2, O 0.35, . 0.1, * 1/3).</summary>
        public static double ShapeSize(char shape) => shape switch { 'o' => 0.2, 'O' => 0.35, '.' => 0.1, _ => 1.0 / 3 };

        /// <summary>Filled shapes ('.' and '*'); circles 'o' and 'O' are open.</summary>
        public static bool Filled(char shape) => shape is '.' or '*';

        /// <summary>Most shapes per area (denser patterns over big areas get sparser).</summary>
        public const int MaxShapes = 4000;

        /// <summary>
        /// Shapes of one kind inside the polygons (staggered rows, each shape fully inside): closed sub-paths of one path
        /// (circles as four Béziers, stars as ten-point polygons).
        /// </summary>
        public static List<PathSeg> ShapeSegments(IReadOnlyList<IReadOnlyList<Vec2>> polys, char shape, double spacing)
        {
            var segs = new List<PathSeg>();
            var all = polys.Where(p => p.Count >= 3).ToList();
            if (all.Count == 0 || !(spacing > 0)) return segs;
            var b = Rect.FromPoints(all.SelectMany(p => p));
            // Keep the count bounded: widen the spacing for very big areas.
            var cells = b.W * b.H / (spacing * spacing);
            if (cells > MaxShapes) spacing *= Math.Sqrt(cells / MaxShapes);
            var r = Math.Max(0.04, ShapeSize(shape) * spacing / 2);
            var row = 0;
            for (var y = b.Y + spacing / 2; y < b.Bottom; y += spacing, row++)
            {
                var x0 = b.X + spacing / 2 + (row % 2 == 1 ? spacing / 2 : 0);
                for (var x = x0; x < b.Right; x += spacing)
                {
                    var c = new Vec2(x, y);
                    if (!InsideAll(all, c, r)) continue;
                    if (shape == '*') Star(segs, c, r * 1.4);
                    else Circle(segs, c, r);
                }
            }
            return segs;
        }

        /// <summary>True if <paramref name="c"/> lies inside the polygons (even-odd) at least <paramref name="r"/> from every edge.</summary>
        private static bool InsideAll(IReadOnlyList<IReadOnlyList<Vec2>> polys, Vec2 c, double r)
        {
            var inside = false;
            foreach (var poly in polys)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    if ((poly[i].Y > c.Y) != (poly[j].Y > c.Y) && c.X < (poly[j].X - poly[i].X) * (c.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
                    if (Geo.ClosestOnSegment(c, poly[j], poly[i]).Point.DistanceTo(c) < r) return false;
                }
            }
            return inside;
        }

        public static void Circle(List<PathSeg> segs, Vec2 c, double r)
        {
            const double k = 0.5522847498;
            var d = r * k;
            segs.Add(PathSeg.Move(new Vec2(c.X + r, c.Y)));
            segs.Add(PathSeg.Cubic(new Vec2(c.X + r, c.Y + d), new Vec2(c.X + d, c.Y + r), new Vec2(c.X, c.Y + r)));
            segs.Add(PathSeg.Cubic(new Vec2(c.X - d, c.Y + r), new Vec2(c.X - r, c.Y + d), new Vec2(c.X - r, c.Y)));
            segs.Add(PathSeg.Cubic(new Vec2(c.X - r, c.Y - d), new Vec2(c.X - d, c.Y - r), new Vec2(c.X, c.Y - r)));
            segs.Add(PathSeg.Cubic(new Vec2(c.X + d, c.Y - r), new Vec2(c.X + r, c.Y - d), new Vec2(c.X + r, c.Y)));
            segs.Add(PathSeg.Close);
        }

        private static void Star(List<PathSeg> segs, Vec2 c, double r)
        {
            for (var i = 0; i < 10; i++)
            {
                var a = -Math.PI / 2 + i * Math.PI / 5;
                var rr = i % 2 == 0 ? r : r * 0.45;
                var p = new Vec2(c.X + rr * Math.Cos(a), c.Y + rr * Math.Sin(a));
                segs.Add(i == 0 ? PathSeg.Move(p) : PathSeg.Line(p));
            }
            segs.Add(PathSeg.Close);
        }

        /// <summary>The pattern's characters for one line family (export: the matplotlib character of an angle).</summary>
        public static char LineChar(double angle)
        {
            var a = ((angle % 180) + 180) % 180;
            return Math.Abs(a - 45) < 22.5 ? '/' : Math.Abs(a - 135) < 22.5 ? '\\' : Math.Abs(a - 90) < 22.5 ? '|' : '-';
        }

        /// <summary>
        /// The matplotlib hatch pattern closest to an earlier fill (hatch, cross or dots with angle and spacing): one, two or
        /// three characters for spacings above 1.8, 0.9 … 1.8 and below 0.9 mm; null for solid and none.
        /// </summary>
        public static string? FromLegacy(string fill, double? angle, double spacing)
        {
            var a = angle ?? 45;
            var times = spacing <= 0.9 ? 3 : spacing <= 1.8 ? 2 : 1;
            var aa = ((a % 180) + 180) % 180;
            char c;
            switch (fill)
            {
                case "dots": c = '.'; break;
                case "cross": c = Math.Abs(aa - 45) < 22.5 || Math.Abs(aa - 135) < 22.5 ? 'x' : '+'; break;
                case "hatch": c = LineChar(a); break;
                default: return null;
            }
            return new string(c, times);
        }
    }
}
