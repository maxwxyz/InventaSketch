using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Textbook cases of statically determinate beams (reactions, shear force, bending moment).</summary>
    public class BeamStaticsTests
    {
        private const int Digits = 9;

        [Fact]
        public void SimplySupportedCentreLoad()
        {
            // F = 10 kN at the middle of L = 4 m: A = B = F/2, M_max = F·L/4 = 10 kN·m at the middle.
            var s = BeamStatics.Solve(BeamModel.Simple(4, new[] { new BeamPointLoad(2, 10) }))!;
            Assert.Equal(5, s.Reactions[0].Force, Digits);
            Assert.Equal(5, s.Reactions[1].Force, Digits);
            Assert.Equal(10, s.MaxMoment.Value, Digits);
            Assert.Equal(2, s.MaxMoment.X, Digits);
            Assert.Equal(5, Math.Abs(s.MaxShear.Value), Digits);
            Assert.Equal(5, s.Shear(1), Digits);
            Assert.Equal(-5, s.Shear(3), Digits);
            Assert.Equal(0, s.Moment(0), Digits);
            Assert.Equal(0, s.Moment(4, left: true), Digits);
        }

        [Fact]
        public void SimplySupportedUniformLoad()
        {
            // q = 2 kN/m over L = 6 m: A = B = q·L/2 = 6 kN, M_max = q·L²/8 = 9 kN·m at the middle.
            var s = BeamStatics.Solve(BeamModel.Simple(6, lines: new[] { new BeamLineLoad(0, 6, 2) }))!;
            Assert.All(s.Reactions, r => Assert.Equal(6, r.Force, Digits));
            Assert.Equal(9, s.MaxMoment.Value, Digits);
            Assert.Equal(3, s.MaxMoment.X, Digits);
            Assert.Equal(6, s.Shear(0), Digits);
            Assert.Equal(-6, s.Shear(6, left: true), Digits);
            Assert.Equal(0, s.Shear(3), Digits);
            // The moment curve is sampled along the parabola and peaks at q·L²/8.
            var curve = s.MomentCurve();
            Assert.True(curve.Count > 10);
            Assert.Equal(9, curve.Max(p => p.Y), 6);
        }

        [Fact]
        public void SimplySupportedOffCentreLoad()
        {
            // M_max = F·a·b/L under the load; A = F·b/L, B = F·a/L.
            const double F = 12, a = 1, b = 3, L = a + b;
            var s = BeamStatics.Solve(BeamModel.Simple(L, new[] { new BeamPointLoad(a, F) }))!;
            Assert.Equal(F * b / L, s.Reactions[0].Force, Digits);
            Assert.Equal(F * a / L, s.Reactions[1].Force, Digits);
            Assert.Equal(F * a * b / L, s.MaxMoment.Value, Digits);
            Assert.Equal(a, s.MaxMoment.X, Digits);
            Assert.Equal(F * b / L, s.MaxShear.Value, Digits);
        }

        [Fact]
        public void CantileverEndLoad()
        {
            // Wall at the left, F at the free end: R = F, M = −F·L at the wall (hogging), zero at the free end.
            const double F = 3, L = 2;
            var s = BeamStatics.Solve(BeamModel.Cantilever(L, points: new[] { new BeamPointLoad(L, F) }))!;
            Assert.Equal(F, s.Reactions.Single().Force, Digits);
            Assert.Equal(-F * L, s.Reactions.Single().Moment, Digits);
            Assert.Equal(-F * L, s.Moment(0), Digits);
            Assert.Equal(-F * L, s.MaxMoment.Value, Digits);
            Assert.Equal(0, s.MaxMoment.X, Digits);
            Assert.Equal(0, s.Moment(L, left: true), Digits);
            Assert.Equal(F, s.Shear(1), Digits);
        }

        [Fact]
        public void CantileverWallRightAndUniformLoad()
        {
            // Wall at the right, F at the free left end: M = −F·L just left of the wall.
            var s = BeamStatics.Solve(BeamModel.Cantilever(5, fixedRight: true, points: new[] { new BeamPointLoad(0, 2) }))!;
            Assert.Equal(-10, s.Moment(5, left: true), Digits);
            Assert.Equal(-10, s.Reactions.Single().Moment, Digits);
            Assert.Equal(-10, s.MaxMoment.Value, Digits);
            Assert.Equal(-2, s.Shear(2), Digits);
            // Uniform load on a cantilever: M = −q·L²/2 at the wall.
            var u = BeamStatics.Solve(BeamModel.Cantilever(4, lines: new[] { new BeamLineLoad(0, 4, 1.5) }))!;
            Assert.Equal(-1.5 * 16 / 2, u.MaxMoment.Value, Digits);
            Assert.Equal(6, u.Reactions.Single().Force, Digits);
        }

        [Fact]
        public void BeamWithOverhang()
        {
            // Supports at 0 and 4, F at 6 (overhang): B = 6F/4, A = −F/2 (holding down), M = −2F over B.
            var m = new BeamModel(6, new[] { new BeamSupport(BeamSupportKind.Pin, 0), new BeamSupport(BeamSupportKind.Roller, 4) },
                new[] { new BeamPointLoad(6, 4) }, Array.Empty<BeamLineLoad>());
            var s = BeamStatics.Solve(m)!;
            Assert.Equal(-2, s.Reactions[0].Force, Digits);
            Assert.Equal(6, s.Reactions[1].Force, Digits);
            Assert.Equal(-8, s.MaxMoment.Value, Digits);
            Assert.Equal(4, s.MaxMoment.X, Digits);
        }

        [Fact]
        public void IndeterminateOrUnstableBeamsAreNotSolved()
        {
            var none = new BeamModel(4, Array.Empty<BeamSupport>(), new[] { new BeamPointLoad(2, 1) }, Array.Empty<BeamLineLoad>());
            var one = new BeamModel(4, new[] { new BeamSupport(BeamSupportKind.Pin, 0) }, Array.Empty<BeamPointLoad>(), Array.Empty<BeamLineLoad>());
            var propped = new BeamModel(4, new[] { new BeamSupport(BeamSupportKind.Fixed, 0), new BeamSupport(BeamSupportKind.Roller, 4) }, Array.Empty<BeamPointLoad>(), Array.Empty<BeamLineLoad>());
            var three = new BeamModel(4, new[] { new BeamSupport(BeamSupportKind.Pin, 0), new BeamSupport(BeamSupportKind.Roller, 2), new BeamSupport(BeamSupportKind.Roller, 4) }, Array.Empty<BeamPointLoad>(), Array.Empty<BeamLineLoad>());
            foreach (var m in new[] { none, one, propped, three }) Assert.Null(BeamStatics.Solve(m));
            Assert.False(BeamStatics.IsDeterminate(propped, out var reason));
            Assert.Equal("indeterminate", reason);
        }

        [Fact]
        public void EquilibriumHoldsForMixedLoads()
        {
            var m = BeamModel.Simple(8, new[] { new BeamPointLoad(1, 3), new BeamPointLoad(5, -2) }, new[] { new BeamLineLoad(2, 7, 1.25) });
            var s = BeamStatics.Solve(m)!;
            var loads = 3 - 2 + 1.25 * 5;
            Assert.Equal(loads, s.Reactions.Sum(r => r.Force), Digits);
            Assert.Equal(0, s.Shear(8), Digits);
            Assert.Equal(0, s.Moment(8, left: true), Digits);
        }

        [Theory]
        [InlineData(0.25, "F·L", "F·L/4")]
        [InlineData(0.5, "F", "F/2")]
        [InlineData(0.125, "q·L²", "q·L²/8")]
        [InlineData(-1, "F·L", "−F·L")]
        [InlineData(0.1875, "F·L", "3·F·L/16")]
        [InlineData(2, "F", "2·F")]
        [InlineData(0.1234, "F", "0.123·F")]
        public void SymbolicValues(double c, string factor, string expected) => Assert.Equal(expected, BeamStatics.Symbolic(c, factor));

        [Fact]
        public void LoadLabelsAreReadAsNumbersOrSymbols()
        {
            var a = LoadAmount.Read("10 kN", null, "F");
            Assert.False(a.IsSymbolic); Assert.Equal(10, a.Value); Assert.Equal("kN", a.Unit);
            var b = LoadAmount.Read("q = 2,5 kN/m", null, "q");
            Assert.Equal(2.5, b.Value); Assert.Equal("kN/m", b.Unit);
            var c = LoadAmount.Read("F₁", null, "F");
            Assert.True(c.IsSymbolic); Assert.Equal("F₁", c.Symbol); Assert.Equal(1, c.Value);
            var d = LoadAmount.Read("F", 7, "F");
            Assert.False(d.IsSymbolic); Assert.Equal(7, d.Value);
            Assert.Equal("F", LoadAmount.Read("", null, "F").Symbol);
        }

        [Fact]
        public void SharesSnapToSimpleFractions()
        {
            Assert.Equal(0.5, BeamCase.SnapShare(0.497));
            Assert.Equal(1.0 / 3, BeamCase.SnapShare(0.335), 12);
            Assert.Equal(0.29, BeamCase.SnapShare(0.29), 12);
        }
    }

    /// <summary>The beam diagram element: reading a drawn beam with supports and loads, labels and fallbacks.</summary>
    public class BeamDiagramTests
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

            /// <summary>Pin support whose pin touches the beam's lower side at x, roller likewise.</summary>
            public void Pin(double x) => Add("ann.support.pin", x, 42 + 7 - 1.2);
            public void Roller(double x) => Add("ann.support.roller", x, 42 + 8 - 1.2);

            public SketchObject Force(double x, string? label = null, bool up = false)
                => Add("ann.force", 0, 0, o =>
                {
                    o = o with { Points = up ? new[] { new Vec2(x, 62), new Vec2(x, 42) } : new[] { new Vec2(x, 18), new Vec2(x, 38) } };
                    return label is null ? o : o.WithParam("label", label);
                });

            public SketchObject Diagram(Func<SketchObject, SketchObject>? f = null)
                => Add("ann.beam.diagram", 60, 90, o => f is null ? o with { Size = new Size2(80, 44) } : f(o with { Size = new Size2(80, 44) }));

            public IReadOnlyList<Primitive> Render(SketchObject o, Lang lang = Lang.En) => R.Render(Doc.Get(o.Id)!, new RenderContext(Doc, lang));

            public string Text(SketchObject o, string tag, Lang lang = Lang.En) => T.All<TextPrim>(Render(o, lang)).Single(t => t.Tag == tag).Text;
        }

        [Fact]
        public void SimplySupportedBeamWithSymbolicCentreLoad()
        {
            var s = new Scene();
            var beam = s.Beam();
            s.Pin(20); s.Roller(100); s.Force(60);
            var d = s.Diagram();
            var c = BeamScene.Read(beam, s.Doc);
            Assert.Equal(2, c.Supports.Count);
            Assert.Equal(new[] { 0.0, 1.0 }, c.Supports.Select(x => x.X).OrderBy(x => x));
            Assert.Equal(0.5, c.PointLoads.Single().At);
            Assert.Equal("max V = F/2", s.Text(d, "vmax"));
            Assert.Equal("max M = F·L/4", s.Text(d, "mmax"));
            Assert.Equal("max Q = F/2", s.Text(d, "vmax", Lang.De));
            Assert.True(BeamDiagramType.CaseOf(s.Doc.Get(d.Id)!, s.Doc).FromBeam);
        }

        [Fact]
        public void NumericLoadAndLengthGiveValuesWithUnits()
        {
            var s = new Scene();
            s.Beam(length: 4);
            s.Pin(20); s.Roller(100); s.Force(60, "10 kN");
            var d = s.Diagram();
            Assert.Equal("max V = 5 kN", s.Text(d, "vmax"));
            Assert.Equal("max M = 10 kN·m", s.Text(d, "mmax"));
        }

        [Fact]
        public void DistributedLoadOnTheBeam()
        {
            var s = new Scene();
            s.Beam(length: 6);
            s.Pin(20); s.Roller(100);
            // The load's lower edge stands on the beam's upper side (y = 38).
            s.Add("ann.load.distributed", 60, 38 - 9, o => (o with { Size = new Size2(80, 18) }).WithParam("label", "2 kN/m"));
            var d = s.Diagram();
            Assert.Equal("max V = 6 kN", s.Text(d, "vmax"));
            Assert.Equal("max M = 9 kN·m", s.Text(d, "mmax"));
        }

        [Fact]
        public void CantileverFromAWallAtTheLeftEnd()
        {
            var s = new Scene();
            var beam = s.Beam();
            s.Add("ann.support.fixed", 17.5, 40); // wall line at x = 20
            s.Force(100);
            var d = s.Diagram();
            var c = BeamScene.Read(beam, s.Doc);
            Assert.Equal(BeamSupportKind.Fixed, c.Supports.Single().Kind);
            Assert.Equal(1.0, c.PointLoads.Single().At);
            Assert.Equal("max M = −F·L", s.Text(d, "mmax"));
            Assert.Equal("max V = F", s.Text(d, "vmax"));
        }

        [Fact]
        public void UpwardForceCountsNegative()
        {
            var s = new Scene();
            s.Beam(length: 2);
            s.Pin(20); s.Roller(100); s.Force(60, "4 kN", up: true);
            var d = s.Diagram();
            Assert.Equal("max M = −2 kN·m", s.Text(d, "mmax"));
        }

        [Fact]
        public void PositiveMomentIsDrawnBelowTheAxisByDefault()
        {
            var s = new Scene();
            s.Beam(); s.Pin(20); s.Roller(100); s.Force(60);
            var d = s.Diagram();
            var moment = T.All<PathPrim>(s.Render(d)).Single(p => p.Tag == "moment");
            var axisY = 90 + 44 / 4.0;
            Assert.True(moment.Segments.Max(g => g.P.Y) > axisY + 3, "sagging moment below the axis");
            var up = s.Diagram(o => o.WithParam("momentBelow", false));
            var moment2 = T.All<PathPrim>(s.Render(up)).Single(p => p.Tag == "moment");
            Assert.True(moment2.Segments.Min(g => g.P.Y) < axisY - 3);
        }

        [Fact]
        public void WithoutABeamTheOwnLoadCaseIsShown()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.beam.diagram", new Vec2(100, 100));
            var texts = T.All<TextPrim>(T.Render(r, doc, o.Id)).ToList();
            Assert.Equal("max V = F/2", texts.Single(t => t.Tag == "vmax").Text);
            Assert.Equal("max M = F·L/4", texts.Single(t => t.Tag == "mmax").Text);

            (doc, o) = T.Insert(r, "ann.beam.diagram.uniform", new Vec2(100, 100));
            texts = T.All<TextPrim>(T.Render(r, doc, o.Id)).ToList();
            Assert.Equal("max V = q·L/2", texts.Single(t => t.Tag == "vmax").Text);
            Assert.Equal("max M = q·L²/8", texts.Single(t => t.Tag == "mmax").Text);

            (doc, o) = T.Insert(r, "ann.beam.diagram.cantilever", new Vec2(100, 100));
            texts = T.All<TextPrim>(T.Render(r, doc, o.Id)).ToList();
            Assert.Equal("max M = −F·L", texts.Single(t => t.Tag == "mmax").Text);

            // Own numeric values: F = 8 kN at the middle of 3 m → M = 6 kN·m.
            var numeric = doc.Get(o.Id)!.WithParam("support", "simple").WithParam("at", 0.5).WithParam("force", 8.0).WithParam("length", 3.0);
            doc = Edits.Replace(doc, numeric);
            Assert.Equal("max M = 6 kN·m", T.All<TextPrim>(T.Render(r, doc, o.Id)).Single(t => t.Tag == "mmax").Text);
        }

        [Fact]
        public void MixedSymbolsAreLabelledWithoutAValue()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.beam.diagram", new Vec2(100, 100));
            doc = Edits.Replace(doc, doc.Get(o.Id)!.WithParam("load", "both"));
            var texts = T.All<TextPrim>(T.Render(r, doc, o.Id)).ToList();
            Assert.Equal("max M", texts.Single(t => t.Tag == "mmax").Text);
        }

        [Fact]
        public void IndeterminateBeamShowsANote()
        {
            var s = new Scene();
            s.Beam(); s.Pin(20); s.Roller(60); s.Roller(100); s.Force(40);
            var d = s.Diagram();
            Assert.Equal("not statically determinate", s.Text(d, "note"));
            Assert.Equal("nicht statisch bestimmt", s.Text(d, "note", Lang.De));
            Assert.DoesNotContain(T.All<PathPrim>(s.Render(d)), p => p.Tag == "moment");
        }

        [Fact]
        public void ABeamWithoutSupportsOrLoadsLeavesTheOwnCase()
        {
            var s = new Scene();
            s.Beam();
            var d = s.Diagram(o => o.WithParam("load", "uniform"));
            Assert.False(BeamDiagramType.CaseOf(s.Doc.Get(d.Id)!, s.Doc).FromBeam);
            Assert.Equal("max M = q·L²/8", s.Text(d, "mmax"));
        }

        [Fact]
        public void SceneElementsNameTheDiagramsAsDependents()
        {
            var s = new Scene();
            var beam = s.Beam(); s.Pin(20); s.Roller(100);
            var f = s.Force(60);
            var d = s.Diagram();
            var ctx = new RenderContext(s.Doc);
            foreach (var o in new[] { beam, f })
            {
                var type = s.R.Type(o.Type)!;
                Assert.True(type.HasDependents, o.Type);
                Assert.Contains(d.Id, type.Dependents(s.Doc.Get(o.Id)!, ctx));
            }
            Assert.True(s.R.Type("ann.beam.diagram")!.DependsOnNeighbours);
        }

        [Fact]
        public void OwnCaseChoicesHavePicturesAndDisappearUnderABeam()
        {
            var s = new Scene();
            var d = s.Diagram();
            var actions = s.R.Actions(s.Doc.Get(d.Id)!, new RenderContext(s.Doc)).ToList();
            var support = actions.Where(a => a.Choice?.Key == "ann.beamdiagram.support").ToList();
            Assert.Equal(3, support.Count);
            Assert.All(support, a => Assert.NotNull(a.Preview));
            Assert.Equal(3, actions.Count(a => a.Choice?.Key == "ann.beamdiagram.load"));

            s.Beam(); s.Pin(20); s.Roller(100); s.Force(60);
            actions = s.R.Actions(s.Doc.Get(d.Id)!, new RenderContext(s.Doc)).ToList();
            Assert.DoesNotContain(actions, a => a.Choice?.Key == "ann.beamdiagram.support");
        }
    }
}
