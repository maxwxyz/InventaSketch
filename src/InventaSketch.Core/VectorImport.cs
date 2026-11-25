using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace InventaSketch.Core
{
    /// <summary>
    /// Importing vector drawings (SVG, ASCII DXF) as editable shapes. Parsing only: the result is a neutral list of
    /// lines, polylines, rectangles, ellipses and texts in millimetres (y down); arcs, Bézier curves, bulges and splines
    /// become polylines whose points stay editable. Colours, line types, fills, hatches, blocks/inserts and dimensions
    /// are not read. The engine turns the shapes into elements (<c>importVector</c>).
    /// </summary>
    public static class VectorImport
    {
        public enum ShapeKind { Line, Polyline, Rect, Ellipse, Text }

        /// <summary>
        /// One imported shape. Line/Polyline: <see cref="Points"/> (closed polylines set <see cref="Closed"/>); Rect and
        /// Ellipse: <see cref="Box"/> (axis-aligned; rotated ones arrive as polylines); Text: <see cref="Text"/> at the
        /// baseline start <see cref="Points"/>[0] with height <see cref="Height"/>; a rounded Rect has its corner <see cref="Radius"/>.
        /// </summary>
        public sealed record Shape(ShapeKind Kind, IReadOnlyList<Vec2> Points, Rect Box, bool Closed = false, string? Text = null, double Height = 0, double Radius = 0);

        /// <summary>The shapes of a drawing; <see cref="RealScale"/> when the file states its units (mm, cm, in …).</summary>
        public sealed record Drawing(IReadOnlyList<Shape> Shapes, bool RealScale)
        {
            public bool IsEmpty => Shapes.Count == 0;

            /// <summary>Union of the shapes' bounds (texts count with their height and an estimated width).</summary>
            public Rect Bounds()
            {
                var pts = new List<Vec2>();
                foreach (var s in Shapes)
                {
                    switch (s.Kind)
                    {
                        case ShapeKind.Rect:
                        case ShapeKind.Ellipse:
                            pts.Add(new Vec2(s.Box.X, s.Box.Y)); pts.Add(new Vec2(s.Box.X + s.Box.W, s.Box.Y + s.Box.H));
                            break;
                        case ShapeKind.Text:
                            var p = s.Points[0];
                            pts.Add(new Vec2(p.X, p.Y - s.Height)); pts.Add(new Vec2(p.X + 0.6 * s.Height * (s.Text?.Length ?? 1), p.Y));
                            break;
                        default:
                            pts.AddRange(s.Points);
                            break;
                    }
                }
                return pts.Count == 0 ? new Rect(0, 0, 0, 0) : Rect.FromPoints(pts);
            }
        }

        /// <summary>SVG or DXF by content; null if the text is neither.</summary>
        public static Drawing? Parse(string text)
        {
            var t = (text ?? "").TrimStart('﻿', ' ', '\t', '\r', '\n');
            if (t.StartsWith("<", StringComparison.Ordinal)) return t.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0 ? ParseSvg(t) : null;
            return LooksLikeDxf(t) ? ParseDxf(t) : null;
        }

        /// <summary>True if <paramref name="fileName"/> or the content looks like an SVG or DXF drawing (no parsing).</summary>
        public static bool IsVectorFile(string? fileName, string text)
        {
            var ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (ext is ".svg" or ".dxf") return true;
            if (ext is ".gpx" or ".geojson" or ".csv" or ".tsv") return false;
            var t = (text ?? "").TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return (t.StartsWith("<", StringComparison.Ordinal) && t.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0) || LooksLikeDxf(t);
        }

        private static bool LooksLikeDxf(string t)
        {
            var lines = t.Replace("\r\n", "\n").Split('\n');
            return lines.Length > 4 && lines[0].Trim() == "0" && lines[1].Trim() == "SECTION";
        }

        private static double D(string? s, double fallback = 0)
            => double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>Points of a circular arc from <paramref name="a0"/> to <paramref name="a1"/> (radians, counter-clockwise when a1 &gt; a0).</summary>
        internal static List<Vec2> Arc(Vec2 c, double rx, double ry, double a0, double a1, double rotation = 0)
        {
            var sweep = a1 - a0;
            var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 18)));   // at most 10° per segment
            var cos = Math.Cos(rotation); var sin = Math.Sin(rotation);
            var pts = new List<Vec2>(n + 1);
            for (var i = 0; i <= n; i++)
            {
                var a = a0 + sweep * i / n;
                double x = rx * Math.Cos(a), y = ry * Math.Sin(a);
                pts.Add(new Vec2(c.X + x * cos - y * sin, c.Y + x * sin + y * cos));
            }
            return pts;
        }

        private static void Bezier(List<Vec2> pts, Vec2 p0, Vec2 c1, Vec2 c2, Vec2 p3, int n = 12)
        {
            for (var i = 1; i <= n; i++)
            {
                var t = (double)i / n; var u = 1 - t;
                pts.Add(new Vec2(u * u * u * p0.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * p3.X,
                                 u * u * u * p0.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * p3.Y));
            }
        }

        // ------------------------------------------------------------------ SVG

        private static readonly Regex Number = new(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

        private static List<double> Numbers(string? s) => Number.Matches(s ?? "").Cast<Match>().Select(m => D(m.Value)).ToList();

        /// <summary>An SVG length in mm ("10mm", "2cm", "1in", "96px", "12pt"; plain numbers are px at 96 dpi); null if absent.</summary>
        private static double? LengthMm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s) || s!.Trim().EndsWith("%", StringComparison.Ordinal)) return null;
            var m = Number.Match(s);
            if (!m.Success) return null;
            var v = D(m.Value);
            var unit = s.Substring(m.Index + m.Length).Trim().ToLowerInvariant();
            return unit switch
            {
                "mm" => v, "cm" => v * 10, "in" => v * 25.4, "pt" => v * 25.4 / 72, "pc" => v * 25.4 / 6, _ => v * 25.4 / 96,
            };
        }

        private static bool HasUnit(string? s) => s is not null && Regex.IsMatch(s, @"(mm|cm|in|pt|pc)\s*$", RegexOptions.IgnoreCase);

        private static Matrix2D ParseTransform(string? s)
        {
            var m = Matrix2D.Identity;
            if (string.IsNullOrWhiteSpace(s)) return m;
            foreach (Match f in Regex.Matches(s, @"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)"))
            {
                var a = Numbers(f.Groups[2].Value);
                Matrix2D t = f.Groups[1].Value switch
                {
                    "matrix" when a.Count >= 6 => new Matrix2D(a[0], a[1], a[2], a[3], a[4], a[5]),
                    "translate" when a.Count >= 1 => Matrix2D.Translation(a[0], a.Count > 1 ? a[1] : 0),
                    "scale" when a.Count >= 1 => Matrix2D.Scaling(a[0], a.Count > 1 ? a[1] : a[0]),
                    "rotate" when a.Count >= 3 => Matrix2D.Translation(a[1], a[2]) * Matrix2D.Rotation(Geo.DegToRad(a[0])) * Matrix2D.Translation(-a[1], -a[2]),
                    "rotate" when a.Count >= 1 => Matrix2D.Rotation(Geo.DegToRad(a[0])),
                    "skewX" when a.Count >= 1 => new Matrix2D(1, 0, Math.Tan(Geo.DegToRad(a[0])), 1, 0, 0),
                    "skewY" when a.Count >= 1 => new Matrix2D(1, Math.Tan(Geo.DegToRad(a[0])), 0, 1, 0, 0),
                    _ => Matrix2D.Identity,
                };
                m = m * t;
            }
            return m;
        }

        /// <summary>SVG elements line, polyline, polygon, rect, circle, ellipse, path and text (with nested groups and transforms).</summary>
        public static Drawing ParseSvg(string text)
        {
            var doc = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using (var reader = XmlReader.Create(new System.IO.StringReader(text), settings)) doc.Load(reader);
            var root = doc.DocumentElement ?? throw new FormatException("Empty SVG.");
            // Units: width/height with a unit and a viewBox give the user unit in mm; otherwise 1 user unit = 1 px (96 dpi).
            var vb = Numbers(root.GetAttribute("viewBox"));
            var widthMm = LengthMm(root.GetAttribute("width"));
            var heightMm = LengthMm(root.GetAttribute("height"));
            var real = HasUnit(root.GetAttribute("width")) || HasUnit(root.GetAttribute("height"));
            var unit = Matrix2D.Scaling(25.4 / 96, 25.4 / 96);
            if (vb.Count == 4 && vb[2] > 0 && vb[3] > 0)
            {
                var sx = widthMm.HasValue ? widthMm.Value / vb[2] : heightMm.HasValue ? heightMm.Value / vb[3] : 25.4 / 96;
                var sy = heightMm.HasValue ? heightMm.Value / vb[3] : sx;
                unit = Matrix2D.Scaling(sx, sy) * Matrix2D.Translation(-vb[0], -vb[1]);
            }
            var shapes = new List<Shape>();
            Walk(root, unit, shapes);
            return new Drawing(shapes, real);
        }

        private static void Walk(XmlElement e, Matrix2D m, List<Shape> shapes)
        {
            foreach (XmlNode n in e.ChildNodes)
            {
                if (n is not XmlElement c) continue;
                var name = c.LocalName;
                if (name is "defs" or "clipPath" or "mask" or "symbol" or "marker" or "pattern" or "style" or "metadata" or "title" or "desc") continue;
                if (c.GetAttribute("display") == "none") continue;
                var cm = m * ParseTransform(c.GetAttribute("transform"));
                double A(string a) => D(c.GetAttribute(a));
                switch (name)
                {
                    case "g":
                    case "a":
                    case "svg":
                        Walk(c, cm, shapes);
                        break;
                    case "line":
                        shapes.Add(new Shape(ShapeKind.Line, new[] { cm.Apply(new Vec2(A("x1"), A("y1"))), cm.Apply(new Vec2(A("x2"), A("y2"))) }, default));
                        break;
                    case "polyline":
                    case "polygon":
                        var nums = Numbers(c.GetAttribute("points"));
                        var pts = new List<Vec2>();
                        for (var i = 0; i + 1 < nums.Count; i += 2) pts.Add(cm.Apply(new Vec2(nums[i], nums[i + 1])));
                        if (pts.Count >= 2) shapes.Add(new Shape(ShapeKind.Polyline, pts, default, name == "polygon" && pts.Count > 2));
                        break;
                    case "rect":
                        AddBox(shapes, cm, ShapeKind.Rect, A("x"), A("y"), A("width"), A("height"), c.HasAttribute("rx") ? A("rx") : A("ry"));
                        break;
                    case "circle":
                        AddBox(shapes, cm, ShapeKind.Ellipse, A("cx") - A("r"), A("cy") - A("r"), 2 * A("r"), 2 * A("r"));
                        break;
                    case "ellipse":
                        AddBox(shapes, cm, ShapeKind.Ellipse, A("cx") - A("rx"), A("cy") - A("ry"), 2 * A("rx"), 2 * A("ry"));
                        break;
                    case "path":
                        foreach (var (p, closed) in PathPoints(c.GetAttribute("d")))
                            if (p.Count >= 2) shapes.Add(new Shape(p.Count == 2 && !closed ? ShapeKind.Line : ShapeKind.Polyline, p.Select(cm.Apply).ToList(), default, closed && p.Count > 2));
                        break;
                    case "text":
                        var content = string.Join(" ", c.InnerText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
                        if (content.Length == 0) break;
                        var x = Numbers(c.GetAttribute("x")).FirstOrDefault(); var y = Numbers(c.GetAttribute("y")).FirstOrDefault();
                        if (c.SelectSingleNode("*[local-name()='tspan']") is XmlElement ts)
                        {
                            if (ts.HasAttribute("x")) x = Numbers(ts.GetAttribute("x")).FirstOrDefault();
                            if (ts.HasAttribute("y")) y = Numbers(ts.GetAttribute("y")).FirstOrDefault();
                        }
                        var size = FontSize(c) ?? 16;
                        var at = cm.Apply(new Vec2(x, y));
                        var h = (cm.Apply(new Vec2(x, y + size)) - at).Length;
                        shapes.Add(new Shape(ShapeKind.Text, new[] { at }, default, false, content, h));
                        break;
                }
            }
        }

        private static double? FontSize(XmlElement e)
        {
            for (XmlNode? n = e; n is XmlElement x; n = n.ParentNode)
            {
                var s = x.GetAttribute("font-size");
                if (string.IsNullOrEmpty(s))
                {
                    var m = Regex.Match(x.GetAttribute("style"), @"font-size\s*:\s*([^;]+)");
                    if (m.Success) s = m.Groups[1].Value;
                }
                if (!string.IsNullOrEmpty(s) && Number.Match(s) is { Success: true } v) return D(v.Value) * (s.Contains("pt") ? 96.0 / 72 : 1);
            }
            return null;
        }

        /// <summary>An axis-aligned rect/ellipse stays a box when the transform only scales and translates; else a polyline.</summary>
        private static void AddBox(List<Shape> shapes, Matrix2D m, ShapeKind kind, double x, double y, double w, double h, double radius = 0)
        {
            if (w <= 0 || h <= 0) return;
            if (Math.Abs(m.B) < 1e-9 && Math.Abs(m.C) < 1e-9)
            {
                var a = m.Apply(new Vec2(x, y)); var b = m.Apply(new Vec2(x + w, y + h));
                shapes.Add(new Shape(kind, Array.Empty<Vec2>(), new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)), Radius: Math.Max(0, radius * Math.Abs(m.A))));
                return;
            }
            var pts = kind == ShapeKind.Rect
                ? new List<Vec2> { new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h) }
                : Arc(new Vec2(x + w / 2, y + h / 2), w / 2, h / 2, 0, 2 * Math.PI).Take(36).ToList();
            shapes.Add(new Shape(ShapeKind.Polyline, pts.Select(m.Apply).ToList(), default, true));
        }

        /// <summary>Sub-paths of SVG path data as point lists (curves flattened), with their closed flag.</summary>
        public static List<(List<Vec2> Points, bool Closed)> PathPoints(string? d)
        {
            var result = new List<(List<Vec2>, bool)>();
            var tokens = Regex.Matches(d ?? "", @"[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?").Cast<Match>().Select(m => m.Value).ToList();
            var i = 0;
            var cur = new Vec2(0, 0); var start = cur; Vec2? lastCtrl = null;
            List<Vec2>? pts = null;
            char cmd = 'M';
            bool IsCmd(int k) => k < tokens.Count && char.IsLetter(tokens[k][0]) && tokens[k] != "e" && tokens[k] != "E";
            double Next() => i < tokens.Count ? D(tokens[i++]) : 0;
            void Flush(bool closed)
            {
                if (pts is { Count: >= 2 }) result.Add((pts, closed));
                pts = null;
            }
            while (i < tokens.Count)
            {
                if (IsCmd(i)) cmd = tokens[i++][0];
                else if (cmd is 'Z' or 'z') { i++; continue; }
                var rel = char.IsLower(cmd);
                Vec2 P(double x, double y) => rel ? new Vec2(cur.X + x, cur.Y + y) : new Vec2(x, y);
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M':
                        Flush(false);
                        cur = P(Next(), Next()); start = cur; pts = new List<Vec2> { cur };
                        cmd = rel ? 'l' : 'L';
                        lastCtrl = null;
                        break;
                    case 'L':
                        cur = P(Next(), Next()); (pts ??= new List<Vec2> { start }).Add(cur); lastCtrl = null;
                        break;
                    case 'H':
                        cur = new Vec2(rel ? cur.X + Next() : Next(), cur.Y); (pts ??= new List<Vec2> { start }).Add(cur); lastCtrl = null;
                        break;
                    case 'V':
                        cur = new Vec2(cur.X, rel ? cur.Y + Next() : Next()); (pts ??= new List<Vec2> { start }).Add(cur); lastCtrl = null;
                        break;
                    case 'C':
                    {
                        var c1 = P(Next(), Next()); var c2 = P(Next(), Next()); var e = P(Next(), Next());
                        Bezier(pts ??= new List<Vec2> { cur }, cur, c1, c2, e); cur = e; lastCtrl = c2;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = lastCtrl is { } lc ? new Vec2(2 * cur.X - lc.X, 2 * cur.Y - lc.Y) : cur;
                        var c2 = P(Next(), Next()); var e = P(Next(), Next());
                        Bezier(pts ??= new List<Vec2> { cur }, cur, c1, c2, e); cur = e; lastCtrl = c2;
                        break;
                    }
                    case 'Q':
                    case 'T':
                    {
                        var q = char.ToUpperInvariant(cmd) == 'Q' ? P(Next(), Next()) : lastCtrl is { } lc ? new Vec2(2 * cur.X - lc.X, 2 * cur.Y - lc.Y) : cur;
                        var e = P(Next(), Next());
                        Bezier(pts ??= new List<Vec2> { cur }, cur, cur + (q - cur) * (2.0 / 3), e + (q - e) * (2.0 / 3), e); cur = e; lastCtrl = q;
                        break;
                    }
                    case 'A':
                    {
                        double rx = Math.Abs(Next()), ry = Math.Abs(Next()), phi = Geo.DegToRad(Next());
                        bool large = Next() != 0, sweep = Next() != 0;
                        var e = P(Next(), Next());
                        var list = pts ??= new List<Vec2> { cur };
                        list.AddRange(SvgArc(cur, e, rx, ry, phi, large, sweep).Skip(1));
                        cur = e; lastCtrl = null;
                        break;
                    }
                    case 'Z':
                        cur = start; Flush(true); lastCtrl = null;
                        break;
                    default:
                        i++;
                        break;
                }
            }
            Flush(false);
            return result;
        }

        /// <summary>SVG elliptical arc (endpoint parameterisation, SVG 1.1 F.6.5) as points.</summary>
        private static List<Vec2> SvgArc(Vec2 p1, Vec2 p2, double rx, double ry, double phi, bool large, bool sweep)
        {
            if (rx < 1e-9 || ry < 1e-9 || (p1 - p2).Length < 1e-9) return new List<Vec2> { p1, p2 };
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (p1.X - p2.X) / 2, dy = (p1.Y - p2.Y) / 2;
            double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1) { rx *= Math.Sqrt(lambda); ry *= Math.Sqrt(lambda); }
            var num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var coef = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / den));
            double cx1 = coef * rx * y1 / ry, cy1 = -coef * ry * x1 / rx;
            var c = new Vec2(cos * cx1 - sin * cy1 + (p1.X + p2.X) / 2, sin * cx1 + cos * cy1 + (p1.Y + p2.Y) / 2);
            double Angle(double ux, double uy, double vx, double vy) => Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            var t1 = Angle(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry);
            var dt = Angle((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry);
            if (!sweep && dt > 0) dt -= 2 * Math.PI;
            else if (sweep && dt < 0) dt += 2 * Math.PI;
            return Arc(c, rx, ry, t1, t1 + dt, phi);
        }

        // ------------------------------------------------------------------ elements

        /// <summary>
        /// Editable elements for <paramref name="drawing"/>: lines (<c>ann.line</c>), polylines (<c>ann.polyline</c>, closed
        /// ones with "closed"), rectangles (<c>basic.rect</c>), ellipses (<c>basic.ellipse</c>) and texts (<c>ann.text</c>,
        /// with their height as "textSize"). A drawing with units that fits into the page keeps its real size and is
        /// centred; anything else is fitted into the page inset by a tenth. Types that are not installed are skipped.
        /// </summary>
        public static IReadOnlyList<SketchObject> Elements(Registry registry, Drawing drawing, Size2 page)
        {
            var created = new List<SketchObject>();
            var b = drawing.Bounds();
            if (drawing.IsEmpty) return created;
            var w = Math.Max(b.W, 1e-6); var h = Math.Max(b.H, 1e-6);
            var fits = drawing.RealScale && b.W <= page.W * 0.9 && b.H <= page.H * 0.9;
            var s = fits ? 1.0 : Math.Min(page.W * 0.8 / w, page.H * 0.8 / h);
            var offset = new Vec2(page.W / 2 - (b.X + b.W / 2) * s, page.H / 2 - (b.Y + b.H / 2) * s);
            Vec2 M(Vec2 p) => new(Math.Round(p.X * s + offset.X, 4), Math.Round(p.Y * s + offset.Y, 4));
            LibraryItem? Item(string type) => registry.Library.FirstOrDefault(i => i.TypeId == type && i.Params is not { Count: > 0 })
                ?? registry.Library.FirstOrDefault(i => i.TypeId == type);
            var line = Item("ann.line"); var poly = Item("ann.polyline"); var rect = Item("basic.rect");
            var ellipse = Item("basic.ellipse"); var text = Item("ann.text");
            foreach (var sh in drawing.Shapes)
            {
                switch (sh.Kind)
                {
                    case ShapeKind.Line when line is not null:
                        created.Add(registry.Create(line, M(sh.Points[0])) with { Points = sh.Points.Select(M).ToList(), Attach = null });
                        break;
                    case ShapeKind.Line:
                    case ShapeKind.Polyline:
                        if (poly is null) break;
                        var pts = sh.Points.Select(M).ToList();
                        // Drop points closer than 0.05 mm (flattened curves, duplicated vertices).
                        var thin = new List<Vec2> { pts[0] };
                        foreach (var p in pts.Skip(1)) if (p.DistanceTo(thin[thin.Count - 1]) >= 0.05) thin.Add(p);
                        if (thin.Count < 2) break;
                        var o = registry.Create(poly, thin[0]) with { Points = thin, Attach = null };
                        created.Add(sh.Closed && thin.Count > 2 ? o.WithParam("closed", true) : o);
                        break;
                    case ShapeKind.Rect when rect is not null:
                    case ShapeKind.Ellipse when ellipse is not null:
                        var item = sh.Kind == ShapeKind.Rect ? rect! : ellipse!;
                        var c = M(new Vec2(sh.Box.X + sh.Box.W / 2, sh.Box.Y + sh.Box.H / 2));
                        var box = registry.Create(item, c);
                        box = box with { Transform = box.Transform with { X = c.X, Y = c.Y }, Size = new Size2(Math.Max(0.1, sh.Box.W * s), Math.Max(0.1, sh.Box.H * s)) };
                        created.Add(sh.Kind == ShapeKind.Rect && sh.Radius > 0 ? box.WithParam("radius", Math.Round(sh.Radius * s, 3)) : box);
                        break;
                    case ShapeKind.Text when text is not null && !string.IsNullOrWhiteSpace(sh.Text):
                        var th = Math.Max(1, Math.Min(50, Math.Round(sh.Height * s, 2)));
                        var at = M(sh.Points[0]);
                        var tw = Math.Max(4, 0.62 * th * sh.Text!.Length + 2);
                        var t = registry.Create(text, at);
                        created.Add((t with { Transform = t.Transform with { X = at.X + tw / 2, Y = at.Y - th * 0.35 }, Size = new Size2(tw, th * 1.6), Text = sh.Text })
                            .WithParam("textSize", th));
                        break;
                }
            }
            return created;
        }

        // ------------------------------------------------------------------ DXF

        /// <summary>
        /// ASCII DXF entities LINE, LWPOLYLINE (with bulges), POLYLINE/VERTEX, CIRCLE, ARC, ELLIPSE, SPLINE (through its
        /// fit or control points), TEXT and MTEXT; units from $INSUNITS (unitless drawings count as mm). DXF y points up,
        /// the result y down.
        /// </summary>
        public static Drawing ParseDxf(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var pairs = new List<(int Code, string Value)>(lines.Length / 2);
            for (var i = 0; i + 1 < lines.Length; i += 2)
                if (int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)) pairs.Add((code, lines[i + 1].TrimEnd()));
            // Units.
            var units = 0;
            for (var i = 0; i + 1 < pairs.Count; i++)
                if (pairs[i].Code == 9 && pairs[i].Value.Trim() == "$INSUNITS" && pairs[i + 1].Code == 70) units = (int)D(pairs[i + 1].Value);
            var mm = units switch { 1 => 25.4, 2 => 304.8, 4 => 1, 5 => 10, 6 => 1000, 8 => 0.0254, 9 => 0.0254, 10 => 914.4, 14 => 100, _ => 1 };
            // Entities section, split into entities.
            var start = pairs.FindIndex(p => p.Code == 2 && p.Value.Trim() == "ENTITIES");
            var shapes = new List<Shape>();
            if (start < 0) return new Drawing(shapes, units != 0);
            var entities = new List<(string Type, List<(int Code, string Value)> Data)>();
            for (var i = start + 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code != 0) { if (entities.Count > 0) entities[entities.Count - 1].Data.Add(pairs[i]); continue; }
                var type = pairs[i].Value.Trim();
                if (type == "ENDSEC") break;
                entities.Add((type, new List<(int, string)>()));
            }
            Vec2 Map(double x, double y) => new(x * mm, -y * mm);
            List<Vec2>? polyline = null; var polyClosed = false;
            foreach (var (type, data) in entities)
            {
                double G(int code, double fallback = 0) { foreach (var (c, v) in data) if (c == code) return D(v, fallback); return fallback; }
                string? S(int code) { foreach (var (c, v) in data) if (c == code) return v; return null; }
                switch (type)
                {
                    case "LINE":
                        shapes.Add(new Shape(ShapeKind.Line, new[] { Map(G(10), G(20)), Map(G(11), G(21)) }, default));
                        break;
                    case "LWPOLYLINE":
                    {
                        var verts = new List<(double X, double Y, double Bulge)>();
                        foreach (var (c, v) in data)
                        {
                            if (c == 10) verts.Add((D(v), 0, 0));
                            else if (c == 20 && verts.Count > 0) verts[verts.Count - 1] = (verts[verts.Count - 1].X, D(v), verts[verts.Count - 1].Bulge);
                            else if (c == 42 && verts.Count > 0) verts[verts.Count - 1] = (verts[verts.Count - 1].X, verts[verts.Count - 1].Y, D(v));
                        }
                        var closed = ((int)G(70) & 1) != 0;
                        AddBulged(shapes, verts, closed, Map);
                        break;
                    }
                    case "POLYLINE":
                        polyline = new List<Vec2>(); polyClosed = ((int)G(70) & 1) != 0;
                        break;
                    case "VERTEX":
                        polyline?.Add(Map(G(10), G(20)));
                        break;
                    case "SEQEND":
                        if (polyline is { Count: >= 2 }) shapes.Add(new Shape(ShapeKind.Polyline, polyline, default, polyClosed && polyline.Count > 2));
                        polyline = null;
                        break;
                    case "CIRCLE":
                    {
                        var c = Map(G(10), G(20)); var r = G(40) * mm;
                        if (r > 0) shapes.Add(new Shape(ShapeKind.Ellipse, Array.Empty<Vec2>(), new Rect(c.X - r, c.Y - r, 2 * r, 2 * r)));
                        break;
                    }
                    case "ARC":
                    {
                        // Counter-clockwise in DXF (y up) = clockwise on screen: map each point.
                        var a0 = Geo.DegToRad(G(50)); var a1 = Geo.DegToRad(G(51));
                        if (a1 <= a0) a1 += 2 * Math.PI;
                        var pts = Arc(new Vec2(G(10), G(20)), G(40), G(40), a0, a1).Select(p => Map(p.X, p.Y)).ToList();
                        shapes.Add(new Shape(ShapeKind.Polyline, pts, default));
                        break;
                    }
                    case "ELLIPSE":
                    {
                        var cx = G(10); var cy = G(20); var mx = G(11); var my = G(21); var ratio = G(40, 1);
                        var t0 = G(41, 0); var t1 = G(42, 2 * Math.PI);
                        if (t1 <= t0) t1 += 2 * Math.PI;
                        var major = Math.Sqrt(mx * mx + my * my);
                        var rot = Math.Atan2(my, mx);
                        var full = Math.Abs(t1 - t0 - 2 * Math.PI) < 1e-6;
                        if (full && (Math.Abs(Math.Sin(rot)) < 1e-9 || Math.Abs(Math.Cos(rot)) < 1e-9))
                        {
                            var horizontal = Math.Abs(Math.Sin(rot)) < 1e-9;
                            double rx = (horizontal ? major : major * ratio) * mm, ry = (horizontal ? major * ratio : major) * mm;
                            var c = Map(cx, cy);
                            shapes.Add(new Shape(ShapeKind.Ellipse, Array.Empty<Vec2>(), new Rect(c.X - rx, c.Y - ry, 2 * rx, 2 * ry)));
                        }
                        else
                        {
                            var pts = Arc(new Vec2(cx, cy), major, major * ratio, t0, t1, rot).Select(p => Map(p.X, p.Y)).ToList();
                            if (full) pts.RemoveAt(pts.Count - 1);
                            shapes.Add(new Shape(ShapeKind.Polyline, pts, default, full));
                        }
                        break;
                    }
                    case "SPLINE":
                    {
                        // Fit points (11/21) if present, else the control points (10/20): an approximation, editable afterwards.
                        var fit = Coordinates(data, 11, 21); var ctrl = Coordinates(data, 10, 20);
                        var pts = (fit.Count >= 2 ? fit : ctrl).Select(p => Map(p.X, p.Y)).ToList();
                        if (pts.Count >= 2) shapes.Add(new Shape(ShapeKind.Polyline, pts, default, ((int)G(70) & 1) != 0 && pts.Count > 2));
                        break;
                    }
                    case "TEXT":
                    case "MTEXT":
                    {
                        var content = type == "MTEXT" ? MText(string.Concat(data.Where(p => p.Code == 3).Select(p => p.Value)) + (S(1) ?? "")) : S(1) ?? "";
                        if (content.Trim().Length == 0) break;
                        var h = G(40, 2.5) * mm;
                        // MTEXT anchors at its top left (attachment 1, the default): the baseline is one height lower.
                        var at = Map(G(10), G(20));
                        if (type == "MTEXT") at = new Vec2(at.X, at.Y + h);
                        shapes.Add(new Shape(ShapeKind.Text, new[] { at }, default, false, content.Trim(), h));
                        break;
                    }
                }
            }
            return new Drawing(shapes, units != 0);
        }

        private static List<Vec2> Coordinates(List<(int Code, string Value)> data, int xCode, int yCode)
        {
            var pts = new List<Vec2>();
            foreach (var (c, v) in data)
            {
                if (c == xCode) pts.Add(new Vec2(D(v), 0));
                else if (c == yCode && pts.Count > 0) pts[pts.Count - 1] = new Vec2(pts[pts.Count - 1].X, D(v));
            }
            return pts;
        }

        /// <summary>MTEXT without formatting codes (\P paragraphs become spaces, {\f…;…} keeps the text).</summary>
        private static string MText(string s)
        {
            s = Regex.Replace(s, @"\\[Pp]", " ");
            s = Regex.Replace(s, @"\\[A-Za-z][^;\\{}]*;", "");
            s = Regex.Replace(s, @"\\[LlOoKk]", "");
            return s.Replace("{", "").Replace("}", "").Replace("\\~", " ");
        }

        /// <summary>A LWPOLYLINE: straight segments, or arcs where a vertex has a bulge (tan of a quarter of the arc angle).</summary>
        private static void AddBulged(List<Shape> shapes, List<(double X, double Y, double Bulge)> verts, bool closed, Func<double, double, Vec2> map)
        {
            if (verts.Count < 2) return;
            var pts = new List<Vec2> { new(verts[0].X, verts[0].Y) };
            var n = closed ? verts.Count : verts.Count - 1;
            for (var i = 0; i < n; i++)
            {
                var a = verts[i]; var b = verts[(i + 1) % verts.Count];
                if (Math.Abs(a.Bulge) > 1e-9)
                {
                    var theta = 4 * Math.Atan(a.Bulge);
                    var chord = new Vec2(b.X - a.X, b.Y - a.Y); var len = chord.Length;
                    if (len > 1e-12)
                    {
                        var r = len / (2 * Math.Sin(Math.Abs(theta) / 2));
                        var mid = new Vec2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                        var h = r * Math.Cos(theta / 2);
                        // The centre lies to the left of the chord for a positive bulge (counter-clockwise arc).
                        var normal = new Vec2(-chord.Y / len, chord.X / len);
                        var c = mid + normal * (Math.Sign(a.Bulge) * h);
                        var a0 = Math.Atan2(a.Y - c.Y, a.X - c.X);
                        pts.AddRange(Arc(c, r, r, a0, a0 + theta).Skip(1));
                        continue;
                    }
                }
                pts.Add(new Vec2(b.X, b.Y));
            }
            if (closed && pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
            var mapped = pts.Select(p => map(p.X, p.Y)).ToList();
            shapes.Add(new Shape(mapped.Count == 2 && !closed ? ShapeKind.Line : ShapeKind.Polyline, mapped, default, closed && mapped.Count > 2));
        }
    }
}
