using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Applied couples, triangular and trapezoidal loads, Gerber beams and the static classification (textbook cases).</summary>
    public class BeamLoadsAndHingesTests
    {
        private const int Digits = 9;
        private static readonly double Sqrt3 = Math.Sqrt(3);

        private static BeamModel Beam(double length, BeamSupport[] supports, BeamPointLoad[]? points = null, BeamLineLoad[]? lines = null,
            BeamCouple[]? couples = null, double[]? hinges = null)
            => new(length, supports, points ?? Array.Empty<BeamPointLoad>(), lines ?? Array.Empty<BeamLineLoad>())
            {
                Couples = couples ?? Array.Empty<BeamCouple>(),
                Hinges = hinges ?? Array.Empty<double>(),
            };

        private static BeamSupport Pin(double x) => new(BeamSupportKind.Pin, x);
        private static BeamSupport Roller(double x) => new(BeamSupportKind.Roller, x);
        private static BeamSupport Wall(double x) => new(BeamSupportKind.Fixed, x);

        [Fact]
        public void CantileverWithEndMoment()
        {
            // Wall at the left, clockwise couple M₀ at the free end: no shear force, constant hogging moment −M₀, no wall force.
            const double M0 = 6, L = 3;
            var s = BeamStatics.Solve(Beam(L, new[] { Wall(0) }, couples: new[] { new BeamCouple(L, M0) }))!;
            Assert.Equal(0, s.Reactions.Single().Force, Digits);
            Assert.Equal(-M0, s.Reactions.Single().Moment, Digits);
            foreach (var x in new[] { 0.0, 1, 2.5 }) Assert.Equal(-M0, s.Moment(x), Digits);
            Assert.Equal(0, s.Moment(L), Digits); // the couple closes the diagram at the end
            Assert.Equal(0, s.MaxShear.Value, Digits);
            Assert.Equal(-M0, s.MaxMoment.Value, Digits);
            // Counter-clockwise: sagging +M₀.
            var ccw = BeamStatics.Solve(Beam(L, new[] { Wall(0) }, couples: new[] { new BeamCouple(L, -M0) }))!;
            Assert.Equal(M0, ccw.MaxMoment.Value, Digits);
        }

        [Fact]
        public void CoupleOnASimplySupportedBeamMakesTheMomentJump()
        {
            // Clockwise C = 8 at a = 1 on L = 4: A = −C/L, B = C/L, V = −C/L throughout; M jumps by C at a:
            // M(a−) = −C·a/L = −2, M(a+) = C·(L − a)/L = 6.
            var s = BeamStatics.Solve(Beam(4, new[] { Pin(0), Roller(4) }, couples: new[] { new BeamCouple(1, 8) }))!;
            Assert.Equal(-2, s.Reactions[0].Force, Digits);
            Assert.Equal(2, s.Reactions[1].Force, Digits);
            Assert.Equal(-2, s.Shear(2), Digits);
            Assert.Equal(-2, s.Moment(1, left: true), Digits);
            Assert.Equal(6, s.Moment(1), Digits);
            Assert.Equal(6, s.MaxMoment.Value, Digits);
            Assert.Equal(1, s.MaxMoment.X, Digits);
            // The curve shows the jump as two points at the same x.
            var jump = s.MomentCurve().Where(p => Math.Abs(p.X - 1) < 1e-12).Select(p => p.Y).ToList();
            Assert.Contains(jump, y => Math.Abs(y + 2) < 1e-9);
            Assert.Contains(jump, y => Math.Abs(y - 6) < 1e-9);
        }

        [Fact]
        public void SimplySupportedBeamWithTriangularLoad()
        {
            // q rising from 0 at A to q₀ at B: A = q₀·L/6, B = q₀·L/3, M_max = q₀·L²/(9√3) at x = L/√3.
            const double q0 = 3, L = 6;
            var s = BeamStatics.Solve(Beam(L, new[] { Pin(0), Roller(L) }, lines: new[] { new BeamLineLoad(0, L, 0, q0) }))!;
            Assert.Equal(q0 * L / 6, s.Reactions[0].Force, Digits);
            Assert.Equal(q0 * L / 3, s.Reactions[1].Force, Digits);
            Assert.Equal(q0 * L * L / (9 * Sqrt3), s.MaxMoment.Value, Digits);
            Assert.Equal(L / Sqrt3, s.MaxMoment.X, Digits);
            Assert.Equal(-q0 * L / 3, s.MaxShear.Value, Digits);
            Assert.Equal(L, s.MaxShear.X, Digits);
            Assert.Equal(0, s.Shear(L / Sqrt3), Digits);
            // Shear force sampled along the parabola, moment along the cubic.
            Assert.True(s.ShearCurve().Count > 10);
            Assert.Equal(q0 * L * L / (9 * Sqrt3), s.MomentCurve().Max(p => p.Y), Digits);

            // Falling the other way (given from right to left): mirrored values.
            var f = BeamStatics.Solve(Beam(L, new[] { Pin(0), Roller(L) }, lines: new[] { new BeamLineLoad(L, 0, 0, q0) }))!;
            Assert.Equal(q0 * L / 3, f.Reactions[0].Force, Digits);
            Assert.Equal(L - L / Sqrt3, f.MaxMoment.X, Digits);
        }

        [Fact]
        public void TrapezoidalLoadIsUniformPlusTriangle()
        {
            // q from qa = 1 to qb = 4 over L = 6: superposition of a uniform qa and a rising triangle qb − qa.
            const double qa = 1, qb = 4, L = 6;
            var s = BeamStatics.Solve(Beam(L, new[] { Pin(0), Roller(L) }, lines: new[] { new BeamLineLoad(0, L, qa, qb) }))!;
            Assert.Equal(qa * L / 2 + (qb - qa) * L / 6, s.Reactions[0].Force, Digits);
            Assert.Equal(qa * L / 2 + (qb - qa) * L / 3, s.Reactions[1].Force, Digits);
            Assert.Equal(qa * L * L / 8 + (qb - qa) * L * L / 16, s.Moment(L / 2), Digits);
            Assert.Equal(0, s.Shear(s.MaxMoment.X), 6);
            Assert.Equal(0, s.Moment(L, left: true), Digits);
        }

        [Fact]
        public void CantileverWithTriangularLoad()
        {
            // Wall at the left, q₀ at the wall falling to 0 at the free end: R = q₀·L/2, M = −q₀·L²/6 at the wall.
            const double q0 = 2, L = 3;
            var s = BeamStatics.Solve(Beam(L, new[] { Wall(0) }, lines: new[] { new BeamLineLoad(0, L, q0, 0) }))!;
            Assert.Equal(q0 * L / 2, s.Reactions.Single().Force, Digits);
            Assert.Equal(-q0 * L * L / 6, s.Reactions.Single().Moment, Digits);
            Assert.Equal(-q0 * L * L / 6, s.MaxMoment.Value, Digits);
            Assert.Equal(0, s.MaxMoment.X, Digits);
        }

        [Fact]
        public void GerberBeamWithOneHinge()
        {
            // Wall at 0, hinge at 2, roller at 6, uniform q = 1: the suspended part 2…6 is simply supported (B = q·4/2 = 2,
            // hinge force 2); the cantilever 0…2 carries its own load plus the hinge force: A = 4, M_A = −(q·2·1 + 2·2) = −6.
            var s = BeamStatics.Solve(Beam(6, new[] { Wall(0), Roller(6) }, lines: new[] { new BeamLineLoad(0, 6, 1) }, hinges: new[] { 2.0 }))!;
            Assert.Equal(4, s.Reactions[0].Force, Digits);
            Assert.Equal(-6, s.Reactions[0].Moment, Digits);
            Assert.Equal(2, s.Reactions[1].Force, Digits);
            Assert.Equal(0, s.Moment(2), Digits);
            Assert.Equal(2, s.Moment(4), Digits); // q·4²/8 at the middle of the suspended part
            Assert.Equal(-6, s.MaxMoment.Value, Digits);
            Assert.Equal(0, s.MaxMoment.X, Digits);
            Assert.Equal(new[] { 2.0 }, s.Hinges);
            Assert.Contains(2.0, s.Breaks);
        }

        [Fact]
        public void TwoSpanGerberBeamOnThreeSupports()
        {
            // Pin 0, roller 4, hinge 5, roller 8, F = 6 at 6.5: suspended part 5…8 gives C = 3 and a hinge force of 3 on the
            // overhanging beam 0…5: B = 3·5/4 = 3.75, A = −0.75; M_B = −3·1 = −3, under the load M = 3·1.5 = 4.5.
            var m = Beam(8, new[] { Pin(0), Roller(4), Roller(8) }, points: new[] { new BeamPointLoad(6.5, 6) }, hinges: new[] { 5.0 });
            Assert.Equal(BeamStatus.Determinate, BeamStatics.Check(m).Status);
            var s = BeamStatics.Solve(m)!;
            Assert.Equal(-0.75, s.Reactions[0].Force, Digits);
            Assert.Equal(3.75, s.Reactions[1].Force, Digits);
            Assert.Equal(3, s.Reactions[2].Force, Digits);
            Assert.Equal(-3, s.Moment(4), Digits);
            Assert.Equal(0, s.Moment(5), Digits);
            Assert.Equal(4.5, s.MaxMoment.Value, Digits);
            Assert.Equal(6.5, s.MaxMoment.X, Digits);
        }

        [Fact]
        public void StatusOfIndeterminateAndUnstableBeams()
        {
            BeamCheck C(BeamModel m) => BeamStatics.Check(m);
            // Fixed + roller (propped cantilever): degree 1; three supports: degree 1; clamped at both ends: degree 2.
            Assert.Equal(new BeamCheck(BeamStatus.Indeterminate, 1, 3, 2), C(Beam(4, new[] { Wall(0), Roller(4) })));
            Assert.Equal(BeamStatus.Indeterminate, C(Beam(4, new[] { Pin(0), Roller(2), Roller(4) })).Status);
            Assert.Equal(new BeamCheck(BeamStatus.Indeterminate, 2, 4, 2), C(Beam(4, new[] { Wall(0), Wall(4) })));
            // A hinge in a simply supported beam makes a mechanism; so do no supports, one hinged support or two at one place.
            Assert.Equal(new BeamCheck(BeamStatus.Unstable, 1, 2, 3), C(Beam(4, new[] { Pin(0), Roller(4) }, hinges: new[] { 2.0 })));
            Assert.Equal(BeamStatus.Unstable, C(Beam(4, Array.Empty<BeamSupport>())).Status);
            Assert.Equal(BeamStatus.Unstable, C(Beam(4, new[] { Pin(1) })).Status);
            Assert.Equal(BeamStatus.Unstable, C(Beam(4, new[] { Pin(2), Roller(2) })).Status);
            // Two hinges in the suspended part of a Gerber beam: unstable; a hinge on a propped cantilever: determinate.
            Assert.Equal(BeamStatus.Unstable, C(Beam(6, new[] { Wall(0), Roller(6) }, hinges: new[] { 2.0, 4.0 })).Status);
            Assert.Equal(BeamStatus.Determinate, C(Beam(6, new[] { Wall(0), Roller(6) }, hinges: new[] { 2.0 })).Status);
            // Hinges at the ends of the beam do not count.
            Assert.Equal(BeamStatus.Determinate, C(Beam(4, new[] { Pin(0), Roller(4) }, hinges: new[] { 0.0, 4.0 })).Status);
            // Not solved, with a reason.
            Assert.Null(BeamStatics.Solve(Beam(4, new[] { Pin(0), Roller(4) }, hinges: new[] { 2.0 })));
            Assert.False(BeamStatics.IsDeterminate(Beam(4, Array.Empty<BeamSupport>()), out var reason));
            Assert.Equal("no support", reason);
        }

        [Fact]
        public void EquilibriumHoldsForAllLoadKinds()
        {
            var m = Beam(10, new[] { Wall(0), Roller(7) }, points: new[] { new BeamPointLoad(9, 2) },
                lines: new[] { new BeamLineLoad(1, 6, 0.5, 2.5), new BeamLineLoad(6, 10, 1) }, couples: new[] { new BeamCouple(3, -4) }, hinges: new[] { 4.0 });
            var s = BeamStatics.Solve(m)!;
            var loads = 2 + (0.5 + 2.5) / 2 * 5 + 4;
            Assert.Equal(loads, s.Reactions.Sum(r => r.Force), Digits);
            Assert.Equal(0, s.Shear(10), Digits);
            Assert.Equal(0, s.Moment(10, left: true), Digits);
            Assert.Equal(0, s.Moment(4), Digits);
        }

        [Theory]
        [InlineData(0.0641500299099584, "q·L²", "q·L²/(9√3)")] // 1/(9√3)
        [InlineData(0.5773502691896258, "L", "L/√3")]
        [InlineData(1.1547005383792515, "F", "2·F/√3")]
        public void SymbolicValuesOverRootThree(double c, string factor, string expected) => Assert.Equal(expected, BeamStatics.Symbolic(c, factor));
    }

    /// <summary>The beam diagram reading couples, triangular loads and hinges from a drawing, and its status notes.</summary>
    public class BeamDiagramLoadsTests
    {
        private sealed class Scene
        {
            public Registry R = T.Reg();
            public SketchDocument Doc = SketchDocument.Create(new Size2(200, 160));

            public SketchObject Add(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = R.Create(R.Item(item)!, new Vec2(x, y));
                if (f is not null) o = f(o);
                Doc = Edits.Add(Doc, Doc.Pages[0].Id, o);
                return o;
            }

            /// <summary>Beam from x = 20 to 100 at y = 40 (4 mm high).</summary>
            public SketchObject Beam(double? length = null)
                => Add("ann.beam", 60, 40, o =>
                {
                    o = o with { Size = new Size2(80, 4) };
                    return length is double l ? o.WithParam("length", l) : o;
                });

            public void Pin(double x) => Add("ann.support.pin", x, 42 + 7 - 1.2);
            public void Roller(double x) => Add("ann.support.roller", x, 42 + 8 - 1.2);
            public void Wall() => Add("ann.support.fixed", 17.5, 40);

            /// <summary>Distributed load over the whole beam, standing on its upper side.</summary>
            public SketchObject Load(string? shape = null, string label = "q", bool mirrored = false)
                => Add(shape is null ? "ann.load.distributed" : "ann.load.distributed." + shape, 60, 38 - 9, o =>
                {
                    o = (o with { Size = new Size2(80, 18) }).WithParam("label", label);
                    return mirrored ? o with { Transform = o.Transform with { FlipX = true } } : o;
                });

            public SketchObject Diagram(Func<SketchObject, SketchObject>? f = null)
                => Add("ann.beam.diagram", 60, 90, o => f is null ? o with { Size = new Size2(80, 44) } : f(o with { Size = new Size2(80, 44) }));

            public IReadOnlyList<Primitive> Render(SketchObject o, Lang lang = Lang.En) => R.Render(Doc.Get(o.Id)!, new RenderContext(Doc, lang));

            public string Text(SketchObject o, string tag, Lang lang = Lang.En) => T.All<TextPrim>(Render(o, lang)).Single(t => t.Tag == tag).Text;
        }

        [Fact]
        public void CantileverWithADrawnEndMoment()
        {
            var s = new Scene();
            var beam = s.Beam();
            s.Wall();
            // Default moment symbol: counter-clockwise; at the free end of a left-clamped cantilever it sags the beam.
            s.Add("ann.moment", 100, 40, o => o.WithParam("label", "M₀"));
            var d = s.Diagram();
            var c = BeamScene.Read(beam, s.Doc);
            Assert.Equal(1.0, c.Couples.Single().At);
            Assert.Equal(-1, c.Couples.Single().Amount.Value);
            Assert.Equal("max M = M₀", s.Text(d, "mmax"));
            Assert.Equal("max V = 0", s.Text(d, "vmax"));
        }

        [Fact]
        public void ClockwiseNumericMomentGivesUnits()
        {
            var s = new Scene();
            s.Beam(length: 3);
            s.Wall();
            s.Add("ann.moment", 100, 40, o => o.WithParam("label", "6 kN·m").WithParam("ccw", false));
            var d = s.Diagram();
            Assert.Equal("max M = −6 kN·m", s.Text(d, "mmax"));
            Assert.Equal("max V = 0 kN", s.Text(d, "vmax"));
            // A mirrored symbol turns the other way.
            var s2 = new Scene();
            s2.Beam(length: 3);
            s2.Wall();
            s2.Add("ann.moment", 100, 40, o => o.WithParam("label", "6 kN·m").WithParam("ccw", false) with { Transform = o.Transform with { FlipX = true } });
            Assert.Equal("max M = 6 kN·m", s2.Text(s2.Diagram(), "mmax"));
        }

        [Fact]
        public void TriangularLoadOnASimplySupportedBeam()
        {
            var s = new Scene();
            var beam = s.Beam();
            s.Pin(20); s.Roller(100);
            s.Load("triangle");
            var d = s.Diagram();
            var line = BeamScene.Read(beam, s.Doc).LineLoads.Single();
            Assert.Equal((0.0, 1.0, 0.0, 1.0), (line.From, line.To, line.StartShare, line.EndShare));
            Assert.Equal("max V = −q·L/3", s.Text(d, "vmax"));
            Assert.Equal("max M = q·L²/(9√3)", s.Text(d, "mmax"));
            Assert.Equal("x = L/√3", s.Text(d, "mpos"));
        }

        [Fact]
        public void MirroredTriangleFallsToTheRight()
        {
            var s = new Scene();
            s.Beam(length: 6);
            s.Pin(20); s.Roller(100);
            s.Load("triangle", "3 kN/m", mirrored: true);
            var d = s.Diagram();
            // A = q₀·L/3 = 6 kN, M_max = q₀·L²/(9√3) = 6.928 kN·m at L − L/√3 = 2.536 m.
            Assert.Equal("max V = 6 kN", s.Text(d, "vmax"));
            Assert.Equal("max M = 6.928 kN·m", s.Text(d, "mmax"));
            Assert.Equal("x = 2.536 m", s.Text(d, "mpos"));
        }

        [Fact]
        public void TrapezoidalLoadIsRead()
        {
            var s = new Scene();
            var beam = s.Beam();
            s.Pin(20); s.Roller(100);
            s.Load("trapezoid");
            var line = BeamScene.Read(beam, s.Doc).LineLoads.Single();
            Assert.Equal(0.4, line.StartShare, 9);
            Assert.Equal(1.0, line.EndShare, 9);
        }

        [Fact]
        public void GerberBeamWithADrawnHinge()
        {
            var s = new Scene();
            var beam = s.Beam();
            s.Wall(); s.Roller(100);
            var h = s.Add("ann.beam.hinge", 60, 40);
            s.Load();
            var d = s.Diagram();
            Assert.Equal(new[] { 0.5 }, BeamScene.Read(beam, s.Doc).Hinges);
            // Suspended half: B = q·L/4; cantilever: M_A = −q·L²/8 − q·L/4·L/2 = −q·L²/4.
            Assert.Equal("max M = −q·L²/4", s.Text(d, "mmax"));
            Assert.Equal("x = 0", s.Text(d, "mpos"));
            var hinge = T.All<EllipsePrim>(s.Render(d)).Single(e => e.Tag == "hinge");
            Assert.Equal(60, hinge.Center.X, 6);
            Assert.Equal(90 + 44 / 4.0, hinge.Center.Y, 6);
            var hingeType = s.R.Type(BeamHingeType.TypeId)!;
            Assert.True(hingeType.HasDependents);
            Assert.Contains(d.Id, hingeType.Dependents(s.Doc.Get(h.Id)!, new RenderContext(s.Doc)));
        }

        [Fact]
        public void HingeInASimplySupportedBeamIsReportedUnstable()
        {
            var s = new Scene();
            s.Beam(); s.Pin(20); s.Roller(100);
            s.Add("ann.beam.hinge", 60, 40);
            s.Load();
            var d = s.Diagram();
            Assert.Equal("unstable (mechanism)", s.Text(d, "note"));
            Assert.Equal("kinematisch verschieblich", s.Text(d, "note", Lang.De));
            Assert.DoesNotContain(T.All<PathPrim>(s.Render(d)), p => p.Tag == "moment");
        }

        [Fact]
        public void LoadsWithoutSupportsAreReported()
        {
            var s = new Scene();
            s.Beam(); s.Load();
            var d = s.Diagram();
            Assert.Equal("no supports (unstable)", s.Text(d, "note"));
            Assert.Equal("keine Lager (verschieblich)", s.Text(d, "note", Lang.De));
        }

        [Fact]
        public void OwnCasePresetsWithoutABeam()
        {
            var r = T.Reg();
            string Label(string item, string tag)
            {
                var (doc, o) = T.Insert(r, item, new Vec2(100, 100));
                return T.All<TextPrim>(T.Render(r, doc, o.Id)).Single(t => t.Tag == tag).Text;
            }
            Assert.Equal("max M = q·L²/(9√3)", Label("ann.beam.diagram.triangle", "mmax"));
            Assert.Equal("x = L/√3", Label("ann.beam.diagram.triangle", "mpos"));
            Assert.Equal("max M = −M₀", Label("ann.beam.diagram.moment", "mmax"));
            Assert.Equal("max V = 0", Label("ann.beam.diagram.moment", "vmax"));
            Assert.Equal("max M = −q·L²/4", Label("ann.beam.diagram.gerber", "mmax"));
            Assert.Equal("x = L/2", Label("ann.beam.diagram", "mpos"));
        }

        [Fact]
        public void ShapedLoadsRenderArrowsFollowingTheIntensity()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.load.distributed.triangle", new Vec2(100, 100));
            var arrows = T.All<LinePrim>(T.Render(r, doc, o.Id)).Where(l => l.Tag == AnnTags.Main).OrderBy(l => l.A.X).ToList();
            Assert.Equal(6, arrows.Count); // the zero end has no arrow
            var lengths = arrows.Select(l => l.A.DistanceTo(l.B)).ToList();
            for (var i = 1; i < lengths.Count; i++) Assert.True(lengths[i] > lengths[i - 1]);
            var actions = r.Actions(doc.Get(o.Id)!, new RenderContext(doc)).Where(a => a.Choice?.Key == "ann.load.shape").ToList();
            Assert.Equal(4, actions.Count);
            Assert.All(actions, a => Assert.NotNull(a.Preview));
        }

        [Fact]
        public void ShapeSurvivesTheJsonRoundTrip()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.load.distributed.trapezoid", new Vec2(100, 100));
            doc = Edits.Replace(doc, doc.Get(o.Id)!.WithParam("low", 0.25));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc)).Get(o.Id)!;
            Assert.Equal((0.25, 1.0), DistributedLoadType.Shares(back));
        }
    }
}
