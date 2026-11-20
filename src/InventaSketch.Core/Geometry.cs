using System;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}

namespace InventaSketch.Core
{
    /// <summary>2D vector/point. All document geometry is in millimetres, y grows downwards.</summary>
    public readonly record struct Vec2(double X, double Y)
    {
        public static readonly Vec2 Zero = new(0, 0);
        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
        [System.Text.Json.Serialization.JsonIgnore] public double Length => Math.Sqrt(X * X + Y * Y);
        public double Dot(Vec2 b) => X * b.X + Y * b.Y;
        public double Cross(Vec2 b) => X * b.Y - Y * b.X;
        [System.Text.Json.Serialization.JsonIgnore] public Vec2 Perp => new(-Y, X);
        public Vec2 Normalized() { var l = Length; return l < Geo.Eps ? Zero : new Vec2(X / l, Y / l); }
        public double DistanceTo(Vec2 b) => (this - b).Length;
        public Vec2 Rotate(double rad) { var c = Math.Cos(rad); var s = Math.Sin(rad); return new(X * c - Y * s, X * s + Y * c); }
        public Vec2 RotateAround(Vec2 origin, double rad) => origin + (this - origin).Rotate(rad);
        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        public static Vec2 Polar(double radius, double rad) => new(radius * Math.Cos(rad), radius * Math.Sin(rad));
        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");
    }

    /// <summary>Axis-aligned rectangle (x, y = top-left).</summary>
    public readonly record struct Rect(double X, double Y, double W, double H)
    {
        [System.Text.Json.Serialization.JsonIgnore] public double Right => X + W;
        [System.Text.Json.Serialization.JsonIgnore] public double Bottom => Y + H;
        [System.Text.Json.Serialization.JsonIgnore] public Vec2 Center => new(X + W / 2, Y + H / 2);
        public bool Contains(Vec2 p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
        public bool Contains(Rect r) => r.X >= X && r.Y >= Y && r.Right <= Right && r.Bottom <= Bottom;
        public bool Intersects(Rect r) => X <= r.Right && r.X <= Right && Y <= r.Bottom && r.Y <= Bottom;
        public Rect Union(Rect r)
        {
            var x = Math.Min(X, r.X); var y = Math.Min(Y, r.Y);
            return new Rect(x, y, Math.Max(Right, r.Right) - x, Math.Max(Bottom, r.Bottom) - y);
        }
        public Rect Inflate(double d) => new(X - d, Y - d, W + 2 * d, H + 2 * d);
        public Vec2[] Corners() => new[] { new Vec2(X, Y), new Vec2(Right, Y), new Vec2(Right, Bottom), new Vec2(X, Bottom) };
        public static Rect FromPoints(IEnumerable<Vec2> points)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            var any = false;
            foreach (var p in points)
            {
                any = true;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return any ? new Rect(minX, minY, maxX - minX, maxY - minY) : default;
        }
    }

    /// <summary>Affine matrix [A C E; B D F; 0 0 1] in SVG/canvas convention.</summary>
    public readonly record struct Matrix2D(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Matrix2D Identity = new(1, 0, 0, 1, 0, 0);
        public static Matrix2D Translation(double x, double y) => new(1, 0, 0, 1, x, y);
        public static Matrix2D Scaling(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
        public static Matrix2D Rotation(double rad) { var c = Math.Cos(rad); var s = Math.Sin(rad); return new(c, s, -s, c, 0, 0); }

        /// <summary>this * n (n is applied first).</summary>
        public Matrix2D Multiply(Matrix2D n) => new(
            A * n.A + C * n.B, B * n.A + D * n.B,
            A * n.C + C * n.D, B * n.C + D * n.D,
            A * n.E + C * n.F + E, B * n.E + D * n.F + F);

        public static Matrix2D operator *(Matrix2D m, Matrix2D n) => m.Multiply(n);
        public Vec2 Apply(Vec2 p) => new(A * p.X + C * p.Y + E, B * p.X + D * p.Y + F);
        public Vec2 ApplyLinear(Vec2 p) => new(A * p.X + C * p.Y, B * p.X + D * p.Y);
        [System.Text.Json.Serialization.JsonIgnore] public double Determinant => A * D - B * C;
        [System.Text.Json.Serialization.JsonIgnore] public bool IsAxisAligned => Math.Abs(B) < 1e-9 && Math.Abs(C) < 1e-9;
        /// <summary>Rotation angle of the x axis in degrees.</summary>
        [System.Text.Json.Serialization.JsonIgnore] public double RotationDegrees => Math.Atan2(B, A) * 180 / Math.PI;

        public Matrix2D Invert()
        {
            var det = Determinant;
            if (Math.Abs(det) < Geo.Eps) throw new InvalidOperationException("Matrix is not invertible.");
            return new(D / det, -B / det, -C / det, A / det, (C * F - D * E) / det, (B * E - A * F) / det);
        }
    }

    /// <summary>Geometry helpers.</summary>
    public static class Geo
    {
        public const double Eps = 1e-9;
        public const double MmPerInch = 25.4;
        public const double PtPerMm = 72.0 / 25.4;

        public static double DegToRad(double d) => d * Math.PI / 180;
        public static double RadToDeg(double r) => r * 180 / Math.PI;
        public static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
        public static double NormDeg(double d) { var r = d % 360; return r < 0 ? r + 360 : r; }

        public static (Vec2 Point, double T) ClosestOnSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            var l2 = ab.Dot(ab);
            if (l2 < Eps) return (a, 0);
            var t = Clamp((p - a).Dot(ab) / l2, 0, 1);
            return (Vec2.Lerp(a, b, t), t);
        }

        public static Vec2 ProjectOnLine(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            var l2 = ab.Dot(ab);
            return l2 < Eps ? a : a + ab * ((p - a).Dot(ab) / l2);
        }

        public static Vec2? SegmentIntersection(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2)
        {
            var r = a2 - a1; var s = b2 - b1;
            var denom = r.Cross(s);
            if (Math.Abs(denom) < Eps) return null;
            var t = (b1 - a1).Cross(s) / denom;
            var u = (b1 - a1).Cross(r) / denom;
            if (t < -Eps || t > 1 + Eps || u < -Eps || u > 1 + Eps) return null;
            return a1 + r * t;
        }

        public static double PolygonArea(IReadOnlyList<Vec2> poly)
        {
            double s = 0;
            for (var i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                s += p.X * q.Y - q.X * p.Y;
            }
            return s / 2;
        }

        public static bool PointInPolygon(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        /// <summary>Samples an elliptical arc (angles in radians; positive sweep = clockwise on screen).</summary>
        public static List<Vec2> ArcPoints(Vec2 c, double rx, double ry, double start, double sweep, int segments = 0)
        {
            var n = segments > 0 ? segments : Math.Max(8, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 24)));
            var pts = new List<Vec2>(n + 1);
            for (var i = 0; i <= n; i++)
            {
                var a = start + sweep * i / n;
                pts.Add(new Vec2(c.X + rx * Math.Cos(a), c.Y + ry * Math.Sin(a)));
            }
            return pts;
        }

        public static Vec2 CubicAt(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, double t)
        {
            var u = 1 - t;
            return p0 * (u * u * u) + p1 * (3 * u * u * t) + p2 * (3 * u * t * t) + p3 * (t * t * t);
        }

        public static Vec2 QuadAt(Vec2 p0, Vec2 p1, Vec2 p2, double t)
        {
            var u = 1 - t;
            return p0 * (u * u) + p1 * (2 * u * t) + p2 * (t * t);
        }
    }
}
