using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>
    /// The lens or curved mirror an object arrow is imaged by, in a frame along the element's axis: x along the axis from the
    /// object towards the element (the element's centre or vertex at x = 0, the object at x = −g), y across it. Mirrors fold
    /// the light back: points after the reflection (<see cref="After"/>) are mirrored at the vertex plane.
    /// </summary>
    internal sealed class ImagingFrame
    {
        public ImagingFrame(SceneElement element, Vec2 centre, Vec2 axis, double f, bool mirror)
        {
            Element = element;
            Centre = centre;
            Axis = axis;
            Across = axis.Perp;
            F = f;
            Mirror = mirror;
        }

        public SceneElement Element { get; }
        /// <summary>Optical centre (lens) or vertex (mirror), page mm.</summary>
        public Vec2 Centre { get; }
        /// <summary>Unit vector along the axis, from the object towards the element.</summary>
        public Vec2 Axis { get; }
        public Vec2 Across { get; }
        /// <summary>Signed focal length: converging lens and concave mirror positive.</summary>
        public double F { get; }
        public bool Mirror { get; }

        /// <summary>Frame coordinates of a page point.</summary>
        public Vec2 Frame(Vec2 page) => new((page - Centre).Dot(Axis), (page - Centre).Dot(Across));

        /// <summary>A frame point on the incoming side (object space) on the page.</summary>
        public Vec2 Before(Vec2 p) => Centre + Axis * p.X + Across * p.Y;

        /// <summary>A frame point on the outgoing side (image space) on the page: behind a lens, folded back by a mirror.</summary>
        public Vec2 After(Vec2 p) => Centre + Axis * (Mirror ? -p.X : p.X) + Across * p.Y;
    }

    /// <summary>
    /// The paraxial image of one object point: object distance g, image distance b (thin lens equation 1/f = 1/g + 1/b,
    /// b positive behind a lens or in front of a mirror = real), magnification β = −b/g and the image point in frame
    /// coordinates.
    /// </summary>
    public readonly struct ImagePoint
    {
        public ImagePoint(double g, double b, double magnification, Vec2 point)
        {
            G = g; B = b; Magnification = magnification; Point = point;
        }

        public double G { get; }
        public double B { get; }
        public double Magnification { get; }
        /// <summary>Image point in frame coordinates (x = b, y = β·y of the object point).</summary>
        public Vec2 Point { get; }
        /// <summary>True for a real image (light really meets there), false for a virtual one.</summary>
        public bool Real => B > 0;
    }

    /// <summary>
    /// Image construction of the object arrow (<see cref="ObjectArrowType"/>): the image of the arrow through the next lens or
    /// curved mirror on its axis (real image solid, virtual image dashed) and optionally the three construction rays from its
    /// tip – the parallel ray (through F′ after the element), the central ray (through the centre or vertex, undeviated) and
    /// the focal ray (through F before the element, parallel after it). Pure paraxial geometry from the document: it follows
    /// whenever the object, the lens or the mirror moves.
    /// </summary>
    internal static class Imaging
    {
        public static readonly ParamDef Mode = Pm.Choice("image", "Image", "Bild", "image",
            ("off", "No image", "Kein Bild"), ("image", "Image", "Bild"), ("construction", "Image with construction rays", "Bild mit Konstruktionsstrahlen"));

        public static readonly ParamDef NamesDef = Pm.Bool("names", "Names of object and image", "Namen von Gegenstand und Bild", false);

        /// <summary>True if the arrow draws its image (and so depends on the elements in front of it).</summary>
        public static bool Shown(SketchObject arrow) => arrow.Str("image", "image") != "off";

        /// <summary>An image farther away than this (or magnified more) is treated as lying at infinity and not drawn.</summary>
        private const double MaxDistance = 5000;
        private const double MaxMagnification = 50;

        /// <summary>
        /// The nearest lens or curved mirror ahead of the arrow on its axis (its centre within half its larger box side from
        /// the axis line through the foot), as an imaging frame; null if there is none or a mirror turns its back to the object.
        /// </summary>
        public static ImagingFrame? Find(SketchObject arrow, IReadOnlyList<SceneElement> scene)
        {
            if (arrow.IsPointGeometry) return null;
            var m = arrow.Matrix();
            var foot = m.Apply(new Vec2(0, arrow.Size.H / 2));
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            if (axis.Length < 1e-9) return null;
            (SceneElement E, Surface S, Vec2 C, double Along)? best = null;
            foreach (var e in scene)
            {
                if (e.Object.Id == arrow.Id || e.Focusing is not { } s) continue;
                var c = e.M.Apply(s.Centre);
                var v = c - foot;
                var along = v.Dot(axis);
                if (along <= 1e-6) continue;
                if (Math.Abs(v.Cross(axis)) > 0.5 * Math.Max(e.Object.Size.W, e.Object.Size.H)) continue;
                if (best is null || along < best.Value.Along) best = (e, s, c, along);
            }
            if (best is not { } b) return null;
            var mirror = b.S.Kind == SurfaceKind.CurvedMirror;
            var ea = b.E.Axis;
            if (mirror)
            {
                // A mirror reflects on its local −x side: the object must lie there.
                if ((b.C - foot).Dot(ea) <= 1e-6) return null;
            }
            else if (ea.Dot(axis) < 0) ea = ea * -1;
            return new ImagingFrame(b.E, b.C, ea, b.S.F, mirror);
        }

        /// <summary>The image of page point <paramref name="page"/>; null if it lies at or behind the element or at infinity.</summary>
        public static ImagePoint? Image(ImagingFrame frame, Vec2 page) => Image(frame.F, frame.Frame(page));

        /// <summary>
        /// The image of the object point <paramref name="p"/> (frame coordinates, x = −g) by an ideal thin element of focal
        /// length <paramref name="f"/>; null if the point is not in front of the element or the image lies at infinity.
        /// </summary>
        public static ImagePoint? Image(double f, Vec2 p)
        {
            var g = -p.X;
            if (g <= 1e-6 || Math.Abs(f) < 1e-9) return null;
            var b = ThinLens.ImageDistance(f, g);
            if (double.IsInfinity(b) || double.IsNaN(b) || Math.Abs(b) > MaxDistance) return null;
            var beta = -b / g;
            if (Math.Abs(beta) > MaxMagnification) return null;
            return new ImagePoint(g, b, beta, new Vec2(b, beta * p.Y));
        }

        /// <summary>The imaging frame of <paramref name="arrow"/> in its document, or null (also when tracing fails).</summary>
        public static ImagingFrame? Find(SketchObject arrow, RenderContext ctx)
        {
            try { return Find(arrow, RayTracer.Scene(ctx.Doc, arrow)); }
            catch (Exception) { return null; }
        }

        private static Style Ray(string colour) => new() { Role = LineRole.Thin, StrokeWidth = 0.3, Stroke = colour };
        private static Style VirtualRay(string colour) => new() { Role = LineRole.Hidden, StrokeWidth = 0.25, Stroke = colour };

        /// <summary>Colours of the parallel, central and focal ray (one colour per ray, as in textbooks).</summary>
        private const string ParallelColour = "#d32f2f";
        private const string CentralColour = "#388e3c";
        private const string FocalColour = "#1976d2";

        /// <summary>
        /// The image (and optionally the construction rays and names) of <paramref name="arrow"/> in its local coordinates;
        /// empty when the arrow draws no image or there is no lens or curved mirror in front of it.
        /// </summary>
        public static List<Primitive> Render(SketchObject arrow, RenderContext ctx)
        {
            var list = new List<Primitive>();
            if (!Shown(arrow)) return list;
            var frame = Find(arrow, ctx);
            if (frame is null) return list;
            var m = arrow.Matrix();
            var inv = m.Invert();
            Vec2 Local(Vec2 page) => inv.Apply(page);
            var hh = arrow.Size.H / 2;
            var tipPage = m.Apply(new Vec2(0, -hh));
            var footPage = m.Apply(new Vec2(0, hh));
            var tip = frame.Frame(tipPage);
            var tipImage = Image(frame.F, tip);
            var footImage = Image(frame.F, frame.Frame(footPage));

            if (arrow.Str("image", "image") == "construction")
                list.AddRange(ConstructionRays(frame, tip, tipImage).Select(x => Prim.Transform(x.Prim, inv) with { Tag = OpticsTags.Construction }));

            if (tipImage is { } ti && footImage is { } fi)
            {
                var a = Local(frame.After(fi.Point));
                var b = Local(frame.After(ti.Point));
                if (a.DistanceTo(b) > 1e-6)
                {
                    var style = Ox.Outline with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
                    if (!ti.Real) style = style with { Role = LineRole.Hidden };
                    list.Add(new LinePrim(a, b, style) { Tag = OpticsTags.Image });
                }
                if (arrow.Flag("names", false))
                    list.Add(Name(b + new Vec2(1.2, 0), ctx.Lang == Lang.De ? "B" : "I"));
            }
            return list;
        }

        /// <summary>The name of the object or image next to its tip (local coordinates).</summary>
        public static Primitive Name(Vec2 at, string text)
            => new TextPrim(at, text, new TextStyle { Size = Ox.LabelSize, Italic = true, Anchor = TextAnchor.Start, Baseline = TextBaseline.Middle }) { Tag = OpticsTags.Names };

        /// <summary>
        /// Parallel, central and focal ray from the object tip <paramref name="t"/> (frame coordinates) in page coordinates:
        /// solid where light runs, dashed backward extensions to a virtual image and to a focal point the ray only aims at.
        /// Without a finite image the rays run on parallel or diverging and the focal ray is left out.
        /// </summary>
        internal static List<(Primitive Prim, int Ray)> ConstructionRays(ImagingFrame frame, Vec2 t, ImagePoint? image)
        {
            var list = new List<(Primitive, int)>();
            var f = frame.F;
            var g = -t.X;
            if (g <= 1e-6) return list;
            var real = image is { Real: true };
            // How far the rays run after the element: a little beyond a real image, else 1.5 f.
            var xEnd = image is { } im && im.Real ? im.B + Math.Max(10, 0.25 * im.B) : Math.Max(1.5 * Math.Abs(f), Math.Max(20, image is null ? g : 0));
            void In(Vec2 a, Vec2 b, string colour, int ray, bool dashed = false)
            {
                if (a.DistanceTo(b) > 1e-9) list.Add((new LinePrim(frame.Before(a), frame.Before(b), dashed ? VirtualRay(colour) : Ray(colour)), ray));
            }
            void Out(Vec2 a, Vec2 b, string colour, int ray, bool dashed = false)
            {
                if (a.DistanceTo(b) > 1e-9) list.Add((new LinePrim(frame.After(a), frame.After(b), dashed ? VirtualRay(colour) : Ray(colour)), ray));
            }
            Vec2 Run(Vec2 s, Vec2 d) => s + d * ((xEnd - s.X) / d.X);

            // 1. Parallel ray: parallel to the axis up to the element, then through (or away from) the focal point F′ = (f, 0).
            var p1 = new Vec2(0, t.Y);
            In(t, p1, ParallelColour, 0);
            var d1 = new Vec2(Math.Abs(f), -t.Y * Math.Sign(f));
            Out(p1, Run(p1, d1), ParallelColour, 0);
            // 2. Central ray: through the optical centre (vertex), undeviated (reflected symmetrically).
            var c = Vec2.Zero;
            In(t, c, CentralColour, 1);
            var d2 = new Vec2(g, -t.Y);
            Out(c, Run(c, d2), CentralColour, 1);
            if (image is { } ip)
            {
                // 3. Focal ray: through (or towards) the front focal point F = (−f, 0), parallel to the axis after the element.
                var p3 = new Vec2(0, ip.Point.Y);
                In(t, p3, FocalColour, 2);
                Out(p3, Run(p3, new Vec2(1, 0)), FocalColour, 2);
                var front = new Vec2(-f, 0);
                if (front.X > 1e-9) In(p3, front, FocalColour, 2, dashed: true);          // aims at F behind the element
                else if (front.X < t.X - 1e-9) In(front, t, FocalColour, 2, dashed: true); // comes from F in front of the object
                if (!real)
                {
                    // Virtual image: the rays seem to come from it (dashed backward extensions).
                    var back1 = f < 0 && f < ip.B ? new Vec2(f, 0) : ip.Point;
                    Out(p1, back1, ParallelColour, 0, dashed: true);
                    Out(c, ip.Point, CentralColour, 1, dashed: true);
                    Out(p3, ip.Point, FocalColour, 2, dashed: true);
                }
            }
            return list;
        }
    }
}
