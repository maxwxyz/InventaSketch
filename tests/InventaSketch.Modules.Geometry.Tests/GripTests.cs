using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    /// <summary>
    /// Grips of geometry figures (<see cref="ObjectType.DragHandles"/>): angles that are parameters (radius direction, point
    /// of contact, C on the Thales circle, unit circle angle, bisected angle, transversal) and the arc radius of angle marks.
    /// </summary>
    public class GripTests
    {
        private static SketchObject Item(Registry r, string typeId, Vec2 at = default)
            => r.Create(r.Library.First(i => i.TypeId == typeId && i.Params is null), at);

        private static void Near(double expected, double actual, double tol = 1e-9, string what = "")
            => Assert.True(Math.Abs(expected - actual) <= tol, $"{what}: expected {expected}, got {actual}");

        private static void Near(Vec2 expected, Vec2 actual, double tol = 1e-9)
            => Assert.True(expected.DistanceTo(actual) <= tol, $"expected {expected}, got {actual}");

        private static Vec2 AnchorAt(Registry r, SketchObject o, string id)
            => o.Matrix().Apply(r.Type(o.Type)!.Anchors(o, Fx.Ctx()).First(a => a.Id == id).P);

        /// <summary><paramref name="p"/> turned about <paramref name="c"/> by <paramref name="deg"/> mathematical degrees (counter-clockwise on screen).</summary>
        private static Vec2 TurnCcw(Vec2 p, Vec2 c, double deg)
        {
            var a = -Geo.DegToRad(deg);
            var d = p - c;
            return c + new Vec2(d.X * Math.Cos(a) - d.Y * Math.Sin(a), d.X * Math.Sin(a) + d.Y * Math.Cos(a));
        }

        private static Vec2 MathDir(double deg) => new(Math.Cos(Geo.DegToRad(deg)), -Math.Sin(Geo.DegToRad(deg)));

        /// <summary>The grip <paramref name="key"/> sits at anchor <paramref name="at"/>; turning it about <paramref name="about"/> by <paramref name="deg"/> gives <paramref name="expected"/>.</summary>
        private static void AssertAngleGrip(Registry r, SketchObject o, string at, string about, double deg, double expected)
        {
            var h = r.DragHandles(o, Fx.Ctx()).Single(x => x.Key == "angle");
            Assert.Equal(DragConstraint.Free, h.Constraint);
            Near(AnchorAt(r, o, at), h.Position, 1e-6);
            var moved = r.MoveHandle(o, "angle", TurnCcw(h.Position, AnchorAt(r, o, about), deg), Fx.Ctx());
            Near(expected, moved.Num("angle", double.NaN), 1e-9, o.Type);
            Near(AnchorAt(r, moved, at), r.DragHandles(moved, Fx.Ctx()).Single(x => x.Key == "angle").Position, 1e-6);
            Assert.Same(o, r.MoveHandle(o, "radius", h.Position, Fx.Ctx()));
        }

        [Fact]
        public void CircleRadiusTurnsAboutTheCentre()
        {
            var r = Fx.Reg();
            var o = Item(r, "math.circle.basic", new Vec2(100, 100));
            var h = r.DragHandles(o, Fx.Ctx()).Single();
            // Box 40 × 40: radius 20 − 0.4 mm, direction 45°.
            Near(o.Matrix().Apply(MathDir(45) * 19.6), h.Position);
            Assert.Equal("Direction of the radius: 45°", h.Label.En);
            Assert.Equal("Richtung des Radius: 45°", h.Label.De);
            Near(120, r.MoveHandle(o, "angle", o.Matrix().Apply(MathDir(120) * 5), Fx.Ctx()).Num("angle", 0));
            // Snapped to whole degrees.
            Near(-30, r.MoveHandle(o, "angle", o.Matrix().Apply(MathDir(-30.4) * 5), Fx.Ctx()).Num("angle", 0));
            Assert.StartsWith("Direction of the diameter", r.DragHandles(o.WithParam("show", "diameter"), Fx.Ctx()).Single().Label.En);
            Assert.Empty(r.DragHandles(o.WithParam("show", "none"), Fx.Ctx()));
        }

        [Fact]
        public void PointsOnCirclesAreTurnedByTheirGrips()
        {
            var r = Fx.Reg();
            AssertAngleGrip(r, Item(r, "math.circle.tangent", new Vec2(100, 100)), "contact", "centre", 30, 90);
            AssertAngleGrip(r, Item(r, "math.circle.thales", new Vec2(100, 100)), "C", "centre", 30, 145);
            AssertAngleGrip(r, Item(r, "math.circle.unit", new Vec2(100, 100)), "P", "origin", 20, 60);
            AssertAngleGrip(r, Item(r, "math.construct.anglebisector", new Vec2(100, 100)), "ray2", "vertex", 10, 80);
            // C stays on the upper half of the Thales circle (1° … 179°).
            var thales = Item(r, "math.circle.thales", new Vec2(100, 100));
            var c = AnchorAt(r, thales, "centre");
            Near(179, r.MoveHandle(thales, "angle", c + MathDir(-90) * 5, Fx.Ctx()).Num("angle", 0));
            Near(1, r.MoveHandle(thales, "angle", c + MathDir(-10) * 5, Fx.Ctx()).Num("angle", 0));
        }

        [Fact]
        public void TransversalTurnsAboutTheCentre()
        {
            var r = Fx.Reg();
            var o = Item(r, "math.lines.parallel", new Vec2(100, 100));
            var h = r.DragHandles(o, Fx.Ctx()).Single();
            var c = o.Matrix().Apply(Vec2.Zero);
            var d = h.Position - c;
            Near(60, Geo.RadToDeg(Math.Atan2(-d.Y, d.X)), 1e-6, "on the transversal");
            Assert.Equal("Angle of the transversal: 60°", h.Label.En);
            Near(80, r.MoveHandle(o, "angle", TurnCcw(h.Position, c, 20), Fx.Ctx()).Num("angle", 0));
            Near(160, r.MoveHandle(o, "angle", TurnCcw(h.Position, c, 150), Fx.Ctx()).Num("angle", 0)); // clamped
        }

        [Fact]
        public void GripsFollowAShrunkenFigure()
        {
            // A tiny box shrinks the figure (Fig.Fit): grips and their edits use the same shrink.
            var r = Fx.Reg();
            var o = Item(r, "math.circle.tangent", new Vec2(100, 100)) with { Size = new Size2(40, 2) };
            Assert.NotEqual(Matrix2D.Identity, ((MathBoxType)r.Type(o.Type)!).Build(o, Lang.En).FitMatrix);
            AssertAngleGrip(r, o, "contact", "centre", 30, 90);
        }

        [Fact]
        public void AngleMarkRadiusIsDraggedFromTheVertex()
        {
            var r = Fx.Reg();
            // 45°: the arc sweeps −45° on screen from the first ray, the grip sits at 0.3 of it (−13.5°).
            var o = Item(r, "math.angle.mark") with { Points = new[] { new Vec2(20, 0), Vec2.Zero, new Vec2(20, -20) } };
            var h = r.DragHandles(o, Fx.Ctx()).Single();
            Assert.Equal("radius", h.Key);
            var dir = new Vec2(Math.Cos(Geo.DegToRad(-13.5)), Math.Sin(Geo.DegToRad(-13.5)));
            Near(dir * 7, h.Position);
            Near(dir, h.Direction);
            Assert.Equal("Arc radius: 7 mm", h.Label.En);
            Assert.Equal("Bogenradius: 7 mm", h.Label.De);
            Near(12.5, r.MoveHandle(o, "radius", dir * 12.3 + dir.Perp * 2, Fx.Ctx()).Num("radius", 0));
            Near(1, r.MoveHandle(o, "radius", -dir, Fx.Ctx()).Num("radius", 0));
            // Right angle: the grip is the outer corner of the square (side 0.6 · radius), on the diagonal.
            var right = o with { Points = new[] { new Vec2(20, 0), Vec2.Zero, new Vec2(0, -20) } };
            var diag = new Vec2(1, -1).Normalized();
            var g = r.DragHandles(right, Fx.Ctx()).Single();
            Near(diag * (7 * 0.6 * Math.Sqrt(2)), g.Position);
            Near(10, r.MoveHandle(right, "radius", diag * (10 * 0.6 * Math.Sqrt(2)), Fx.Ctx()).Num("radius", 0));
            Assert.Same(o, r.MoveHandle(o, "angle", h.Position, Fx.Ctx()));
        }

        [Fact]
        public void DraggedValuesSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var unit = Item(r, "math.circle.unit", new Vec2(100, 100));
            var h = r.DragHandles(unit, Fx.Ctx()).Single();
            unit = r.MoveHandle(unit, "angle", TurnCcw(h.Position, AnchorAt(r, unit, "origin"), 20), Fx.Ctx());
            var mark = Item(r, "math.angle.mark") with { Points = new[] { new Vec2(20, 0), Vec2.Zero, new Vec2(20, -20) } };
            mark = r.MoveHandle(mark, "radius", new Vec2(12.2, -2.9), Fx.Ctx());
            var doc = SketchDocument.Create();
            doc = Edits.Add(Edits.Add(doc, doc.Pages[0].Id, unit), doc.Pages[0].Id, mark);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Near(60, back.Get(unit.Id)!.Num("angle", 0));
            Near(mark.Num("radius", 0), back.Get(mark.Id)!.Num("radius", -1));
        }
    }
}
