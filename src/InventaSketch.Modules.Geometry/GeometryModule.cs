using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Geometry
{
    /// <summary>
    /// Math &amp; geometry module for teaching and explanation slides: coordinate systems (2D, number line, 3D, polar),
    /// function graphs, points and vectors (components, addition), angles with measured values, triangles with their
    /// classic constructions, circles (radius, tangent, Thales, unit circle), regular polygons and compass constructions.
    /// Every element is drawn with native primitives and insertable without parameters (see docs/en/modules/geometry.md).
    /// </summary>
    public sealed class GeometryModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() => new ObjectType[]
        {
            new CartesianAxesType(), new NumberLineType(), new SpatialAxesType(), new PolarGridType(),
            new FunctionGraphType(),
            new LabelledPointType(), new VectorType(), new VectorSumType(),
            new AngleMarkType(),
            new TriangleType(), new RegularPolygonType(), new CircleType(), new TangentType(), new ThalesType(), new UnitCircleType(),
            new PerpendicularBisectorType(), new AngleBisectorType(), new ParallelLinesType(),
        });

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "math";
        public I18nText Name => new("Math & geometry", "Mathematik & Geometrie");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(MathCategories.Axes, new I18nText("Coordinate systems", "Koordinatensysteme"), Id, 10),
            new LibraryCategory(MathCategories.Graphs, new I18nText("Function graphs", "Funktionsgraphen"), Id, 20),
            new LibraryCategory(MathCategories.Vectors, new I18nText("Points & vectors", "Punkte & Vektoren"), Id, 30),
            new LibraryCategory(MathCategories.Angles, new I18nText("Angles", "Winkel"), Id, 40),
            new LibraryCategory(MathCategories.Figures, new I18nText("Figures & circles", "Figuren & Kreise"), Id, 50),
            new LibraryCategory(MathCategories.Constructions, new I18nText("Constructions", "Konstruktionen"), Id, 60),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, Size2? size = null,
                IReadOnlyList<Vec2>? points = null, params string[] kw)
            {
                var t = types[type];
                list.Add(new LibraryItem(id, type, new I18nText(en, de), t.Category)
                {
                    Params = ps,
                    Size = t.Geometry == GeometryKind.Box ? size ?? t.DefaultSize : null,
                    Points = points,
                    Keywords = kw,
                });
            }
            void Default(string type) => Add(type, type, types[type].Name.En, types[type].Name.De!);

            // Coordinate systems.
            Default("math.axes.cartesian");
            Add("math.axes.cartesian.quadrant", "math.axes.cartesian", "First quadrant", "Erster Quadrant",
                P(("xMin", 0.0), ("xMax", 6.0), ("yMin", 0.0), ("yMax", 5.0)), new Size2(60, 55), kw: new[] { "quadrant", "positive", "erster quadrant" });
            Default("math.axes.number");
            Default("math.axes.spatial");
            Add("math.axes.spatial.isometric", "math.axes.spatial", "Coordinate system (3D, isometric)", "Koordinatensystem (3D, isometrisch)",
                P(("projection", "isometric")), kw: new[] { "isometric", "isometrisch" });
            Default("math.axes.polar");

            // Function graphs.
            Add("math.graph.function", "math.graph.function", "Parabola y = x²", "Parabel y = x²", kw: new[] { "quadratic", "quadratisch", "normalparabel" });
            Add("math.graph.function.linear", "math.graph.function", "Linear function", "Lineare Funktion",
                P(("function", "linear"), ("a", 0.5), ("d", 1.0), ("xMin", -5.0), ("xMax", 5.0), ("yMin", -4.0), ("yMax", 4.0)),
                kw: new[] { "line", "straight", "gerade", "steigung", "slope" });
            Add("math.graph.function.sine", "math.graph.function", "Sine curve", "Sinuskurve",
                P(("function", "sine"), ("xMin", -6.5), ("xMax", 6.5), ("yMin", -1.5), ("yMax", 1.5), ("yStep", 0.5), ("xPi", true)),
                new Size2(90, 45), kw: new[] { "sin", "trigonometric", "trigonometrisch", "periodic", "periodisch" });
            Add("math.graph.function.exponential", "math.graph.function", "Exponential function", "Exponentialfunktion",
                P(("function", "exponential"), ("xMin", -4.0), ("xMax", 3.0), ("yMin", -1.0), ("yMax", 8.0)),
                kw: new[] { "exp", "growth", "wachstum", "e-funktion" });

            // Points & vectors.
            Default("math.point.labelled");
            Add("math.vector.arrow", "math.vector.arrow", "Vector", "Vektor");
            Add("math.vector.arrow.position", "math.vector.arrow", "Position vector", "Ortsvektor",
                P(("label", "p"), ("tailLabel", "O"), ("tipLabel", "P"), ("points", true)),
                points: new[] { new Vec2(-15, 10), new Vec2(12, -10) }, kw: new[] { "position", "origin", "ortsvektor", "ursprung" });
            Add("math.vector.arrow.components", "math.vector.arrow", "Vector with components", "Vektor mit Komponenten",
                P(("components", true)), points: new[] { new Vec2(-15, 10), new Vec2(12, -10) },
                kw: new[] { "components", "decomposition", "komponenten", "zerlegung" });
            Add("math.vector.sum", "math.vector.sum", "Vector addition (parallelogram)", "Vektoraddition (Parallelogramm)");
            Add("math.vector.sum.tail", "math.vector.sum", "Vector addition (head to tail)", "Vektoraddition (aneinandergehängt)",
                P(("construction", "tail")), points: new[] { new Vec2(-18, 10), new Vec2(12, 10), new Vec2(22, -10) },
                kw: new[] { "head to tail", "aneinanderhängen", "polygon" });

            // Angles.
            Add("math.angle.mark", "math.angle.mark", "Angle", "Winkel");
            Add("math.angle.mark.right", "math.angle.mark", "Right angle", "Rechter Winkel", P(("label", "")),
                points: new[] { new Vec2(18, 10), new Vec2(-12, 10), new Vec2(-12, -20) }, kw: new[] { "90", "right", "rechter", "senkrecht", "perpendicular" });
            Add("math.angle.mark.value", "math.angle.mark", "Angle with value", "Winkel mit Gradzahl", P(("value", true)),
                kw: new[] { "value", "degrees", "wert", "gradzahl", "messen", "measure" });
            Add("math.angle.mark.arc", "math.angle.mark", "Angle mark (arc only)", "Winkelbogen (nur Markierung)", P(("rays", false)),
                kw: new[] { "arc", "mark", "bogen", "markierung" });

            // Figures & circles.
            Default("math.shape.triangle");
            Add("math.shape.triangle.right", "math.shape.triangle", "Right triangle", "Rechtwinkliges Dreieck", P(("kind", "right"), ("angleMarks", true)),
                kw: new[] { "right", "pythagoras", "rechtwinklig", "hypotenuse", "kathete" });
            Add("math.shape.triangle.incircle", "math.shape.triangle", "Triangle with incircle", "Dreieck mit Inkreis", P(("construction", "incircle")),
                new Size2(50, 42), kw: new[] { "incircle", "inkreis", "angle bisector", "winkelhalbierende" });
            Add("math.shape.triangle.circumcircle", "math.shape.triangle", "Triangle with circumcircle", "Dreieck mit Umkreis", P(("construction", "circumcircle")),
                new Size2(50, 50), kw: new[] { "circumcircle", "umkreis", "perpendicular bisector", "mittelsenkrechte" });
            Add("math.shape.polygon", "math.shape.polygon", "Regular hexagon", "Regelmäßiges Sechseck", kw: new[] { "hexagon", "sechseck" });
            Default("math.circle.basic");
            Default("math.circle.tangent");
            Default("math.circle.thales");
            Default("math.circle.unit");

            // Constructions.
            Default("math.construct.perpendicular");
            Default("math.construct.anglebisector");
            Default("math.lines.parallel");
            return list;
        }
    }
}
