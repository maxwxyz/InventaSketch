using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Statically indeterminate beams, deflection lines and normal forces (textbook cases).</summary>
    public class BeamIndeterminateTests
    {
        private const int Digits = 9;

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
        public void ProppedCantileverWithUniformLoad()
        {
            // Wall at A, roller at B, q over L: B = 3·q·L/8, A = 5·q·L/8, M_A = −q·L²/8, field moment 9·q·L²/128 at x = 5·L/8.
            const double q = 2, L = 4;
            var s = BeamStatics.Solve(Beam(L, new[] { Wall(0), Roller(L) }, lines: new[] { new BeamLineLoad(0, L, q) }))!;
            Assert.Equal(new BeamCheck(BeamStatus.Indeterminate, 1, 3, 2), s.Check);
            Assert.Equal(5 * q * L / 8, s.Reactions[0].Force, Digits);
            Assert.Equal(-q * L * L / 8, s.Reactions[0].Moment, Digits);
            Assert.Equal(3 * q * L / 8, s.Reactions[1].Force, Digits);
            Assert.Equal(-q * L * L / 8, s.MaxMoment.Value, Digits);
            Assert.Equal(0, s.MaxMoment.X, Digits);
            Assert.Equal(9 * q * L * L / 128, s.Moment(5 * L / 8), Digits);
            Assert.Equal(0, s.Moment(L, left: true), Digits);
            // The deflection line has zero slope at the wall and zero deflection at both supports.
            Assert.Equal(0, s.Deflection(0), Digits);
            Assert.Equal(0, s.Slope(0), Digits);
            Assert.Equal(0, s.Deflection(L), Digits);
        }

        [Fact]
        public void ProppedCantileverWithCentreLoad()
        {
            // B = 5·F/16, M_A = −3·F·L/16, M under the load 5·F·L/32.
            const double F = 16, L = 2;
            var s = BeamStatics.Solve(Beam(L, new[] { Wall(0), Roller(L) }, points: new[] { new BeamPointLoad(L / 2, F) }))!;
            Assert.Equal(5 * F / 16, s.Reactions[1].Force, Digits);
            Assert.Equal(-3 * F * L / 16, s.Reactions[0].Moment, Digits);
            Assert.Equal(5 * F * L / 32, s.Moment(L / 2), Digits);
        }

        [Fact]
        public void TwoSpanContinuousBeamWithUniformLoad()
        {
            // Two equal spans l under q: middle support 5·q·l/4 (= 10·q·l/8), end supports 3·q·l/8, M_B = −q·l²/8.
            const double q = 3, l = 5;
            var s = BeamStatics.Solve(Beam(2 * l, new[] { Pin(0), Roller(l), Roller(2 * l) }, lines: new[] { new BeamLineLoad(0, 2 * l, q) }))!;
            Assert.Equal(5 * q * l / 4, s.Reactions[1].Force, Digits);
            Assert.Equal(3 * q * l / 8, s.Reactions[0].Force, Digits);
            Assert.Equal(3 * q * l / 8, s.Reactions[2].Force, Digits);
            Assert.Equal(-q * l * l / 8, s.Moment(l), Digits);
            Assert.Equal(-q * l * l / 8, s.MaxMoment.Value, Digits);
            Assert.Equal(l, s.MaxMoment.X, Digits);
            Assert.Equal(9 * q * l * l / 128, s.Moment(3 * l / 8), Digits);
        }

        [Fact]
        public void ThreeSpanContinuousBeamWithUniformLoad()
        {
            // Three equal spans l on four supports: inner supports 1.1·q·l, end supports 0.4·q·l, M = −0.1·q·l² over the inner supports.
            const double q = 1, l = 3;
            var s = BeamStatics.Solve(Beam(3 * l, new[] { Pin(0), Roller(l), Roller(2 * l), Roller(3 * l) }, lines: new[] { new BeamLineLoad(0, 3 * l, q) }))!;
            Assert.Equal(new BeamCheck(BeamStatus.Indeterminate, 2, 4, 2), s.Check);
            Assert.Equal(new[] { 0.4, 1.1, 1.1, 0.4 }.Select(c => c * q * l), s.Reactions.Select(r => r.Force), new Tol(1e-9));
            Assert.Equal(-0.1 * q * l * l, s.Moment(l), Digits);
            Assert.Equal(-0.1 * q * l * l, s.Moment(2 * l), Digits);
        }

        [Fact]
        public void FixedFixedBeamWithCentreLoad()
        {
            // Clamped at both ends, F at the middle: reactions F/2, M = −F·L/8 at the walls and +F·L/8 at the middle,
            // deflection F·L³/(192·EI) at the middle.
            const double F = 8, L = 3;
            var s = BeamStatics.Solve(Beam(L, new[] { Wall(0), Wall(L) }, points: new[] { new BeamPointLoad(L / 2, F) }))!;
            Assert.Equal(new BeamCheck(BeamStatus.Indeterminate, 2, 4, 2), s.Check);
            Assert.All(s.Reactions, r => Assert.Equal(F / 2, r.Force, Digits));
            Assert.All(s.Reactions, r => Assert.Equal(-F * L / 8, r.Moment, Digits));
            Assert.Equal(F * L / 8, s.Moment(L / 2), Digits);
            Assert.Equal(-F * L / 8, s.MaxMoment.Value, Digits);
            Assert.Equal(F * L * L * L / 192, s.MaxDeflection.Value, Digits);
            Assert.Equal(L / 2, s.MaxDeflection.X, Digits);
        }

        [Fact]
        public void FixedFixedBeamWithUniformLoad()
        {
            // M = −q·L²/12 at the walls, q·L²/24 at the middle, deflection q·L⁴/(384·EI).
            const double q = 6, L = 2;
            var s = BeamStatics.Solve(Beam(L, new[] { Wall(0), Wall(L) }, lines: new[] { new BeamLineLoad(0, L, q) }))!;
            Assert.Equal(-q * L * L / 12, s.Reactions[0].Moment, Digits);
            Assert.Equal(q * L * L / 24, s.Moment(L / 2), Digits);
            Assert.Equal(q * Math.Pow(L, 4) / 384, s.MaxDeflection.Value, Digits);
        }

        [Fact]
        public void CantileverDeflection()
        {
            // F at the free end: w = F·L³/(3·EI) at the end, slope F·L²/(2·EI); uniform q: w = q·L⁴/(8·EI).
            const double F = 5, L = 2;
            var s = BeamStatics.Solve(BeamModel.Cantilever(L, points: new[] { new BeamPointLoad(L, F) }))!;
            Assert.Equal(F * L * L * L / 3, s.MaxDeflection.Value, Digits);
            Assert.Equal(L, s.MaxDeflection.X, Digits);
            Assert.Equal(F * L * L * L / 3, s.Deflection(L), Digits);
            Assert.Equal(F * L * L / 2, s.Slope(L), Digits);
            // w(x) = F·x²·(3L − x)/(6·EI)
            Assert.Equal(F * 1 * (3 * L - 1) / 6, s.Deflection(1), Digits);
            var curve = s.DeflectionCurve();
            Assert.Equal(0, curve[0].Y, Digits);
            Assert.Equal(F * L * L * L / 3, curve[curve.Count - 1].Y, Digits);

            var u = BeamStatics.Solve(BeamModel.Cantilever(L, lines: new[] { new BeamLineLoad(0, L, 1) }))!;
            Assert.Equal(Math.Pow(L, 4) / 8, u.MaxDeflection.Value, Digits);
            // Wall at the right: the free end is at x = 0.
            var r = BeamStatics.Solve(BeamModel.Cantilever(L, fixedRight: true, points: new[] { new BeamPointLoad(0, F) }))!;
            Assert.Equal(F * L * L * L / 3, r.MaxDeflection.Value, Digits);
            Assert.Equal(0, r.MaxDeflection.X, Digits);
        }

        [Fact]
        public void SimplySupportedDeflections()
        {
            // Uniform: 5·q·L⁴/(384·EI) at the middle; centre load: F·L³/(48·EI).
            const double L = 4;
            var u = BeamStatics.Solve(BeamModel.Simple(L, lines: new[] { new BeamLineLoad(0, L, 1) }))!;
            Assert.Equal(5 * Math.Pow(L, 4) / 384, u.MaxDeflection.Value, Digits);
            Assert.Equal(L / 2, u.MaxDeflection.X, 6);
            var p = BeamStatics.Solve(BeamModel.Simple(L, new[] { new BeamPointLoad(L / 2, 1) }))!;
            Assert.Equal(L * L * L / 48, p.MaxDeflection.Value, Digits);
            Assert.True(p.DeflectionCurve().Count > 20);
        }

        [Fact]
        public void GerberBeamDeflectsWithAKinkAtTheHinge()
        {
            // Wall 0, hinge 2, roller 6, F = 4 at the hinge: the cantilever carries F alone (w = F·2³/3 at the hinge), the
            // suspended part rotates as a rigid body (no bending): w = w_h·(6 − x)/4.
            var s = BeamStatics.Solve(Beam(6, new[] { Wall(0), Roller(6) }, points: new[] { new BeamPointLoad(2, 4) }, hinges: new[] { 2.0 }))!;
            var wh = 4.0 * 8 / 3;
            Assert.Equal(wh, s.Deflection(2), Digits);
            Assert.Equal(wh / 2, s.Deflection(4), Digits);
            Assert.Equal(-wh / 4, s.Slope(3), Digits);
            Assert.Equal(wh, s.MaxDeflection.Value, Digits);
        }

        [Fact]
        public void EquilibriumHoldsForIndeterminateBeams()
        {
            var m = Beam(10, new[] { Wall(0), Roller(4), Roller(10) }, points: new[] { new BeamPointLoad(7, 3) },
                lines: new[] { new BeamLineLoad(1, 6, 0.5, 2.5) }, couples: new[] { new BeamCouple(8, -2) });
            var s = BeamStatics.Solve(m)!;
            Assert.Equal(BeamStatus.Indeterminate, s.Check.Status);
            Assert.Equal(3 + 1.5 * 5, s.Reactions.Sum(r => r.Force), Digits);
            Assert.Equal(0, s.Shear(10), Digits);
            Assert.Equal(0, s.Moment(10, left: true), Digits);
            foreach (var x in new[] { 0.0, 4, 10 }) Assert.Equal(0, s.Deflection(x), Digits);
            Assert.Equal(0, s.Slope(0), Digits);
        }

        [Fact]
        public void MechanismsStayUnstable()
        {
            // A hinge in a continuous beam between supports keeps it stable; two hinges in one span make a mechanism.
            Assert.NotNull(BeamStatics.Solve(Beam(8, new[] { Pin(0), Roller(4), Roller(8) }, lines: new[] { new BeamLineLoad(0, 8, 1) }, hinges: new[] { 5.0 })));
            var m = Beam(8, new[] { Pin(0), Roller(4), Roller(8) }, lines: new[] { new BeamLineLoad(0, 8, 1) }, hinges: new[] { 5.0, 6.0 });
            Assert.Equal(BeamStatus.Unstable, BeamStatics.Check(m).Status);
            Assert.Null(BeamStatics.Solve(m));
        }

        [Fact]
        public void InclinedLoadGivesANormalForce()
        {
            // Pin left, roller right, F at the middle at 45° (down and to the right): transverse F/√2, along the beam F/√2;
            // the pin takes H = −F/√2, the left half is in tension N = F/√2, the right half carries none.
            const double F = 10, L = 4;
            var load = BeamPointLoad.Inclined(L / 2, F, 45);
            var c = F / Math.Sqrt(2);
            Assert.Equal(c, load.F, Digits);
            Assert.Equal(c, load.Axial, Digits);
            var s = BeamStatics.Solve(BeamModel.Simple(L, new[] { load }))!;
            Assert.True(s.HasNormalForce);
            Assert.Equal(BeamStatus.Determinate, s.Check.Status);
            Assert.Equal(-c, s.Reactions[0].Horizontal, Digits);
            Assert.Equal(0, s.Reactions[1].Horizontal, Digits);
            Assert.Equal(c, s.Normal(1), Digits);
            Assert.Equal(0, s.Normal(3), Digits);
            Assert.Equal(c, s.MaxNormal.Value, Digits);
            Assert.Equal(c * L / 4, s.MaxMoment.Value, Digits);
            // Roller left, pin right: the left half is free, the right half in compression.
            var r = BeamStatics.Solve(new BeamModel(L, new[] { Roller(0), Pin(L) }, new[] { load }, Array.Empty<BeamLineLoad>()))!;
            Assert.Equal(0, r.Normal(1), Digits);
            Assert.Equal(-c, r.Normal(3), Digits);
            // Straight down: no normal force.
            Assert.Equal(0, BeamPointLoad.Inclined(1, F, 90).Axial);
            Assert.False(BeamStatics.Solve(BeamModel.Simple(L, new[] { BeamPointLoad.Inclined(1, F, 90) }))!.HasNormalForce);
        }

        [Fact]
        public void AxialLoadsNeedAHorizontalSupport()
        {
            var load = new BeamPointLoad(2, 0) { Axial = 6 };
            // Rollers only: the beam slides (unstable); two pins share the load (indeterminate, constant EA): +3 / −3.
            var rollers = new BeamModel(4, new[] { Roller(0), Roller(4) }, new[] { load }, Array.Empty<BeamLineLoad>());
            Assert.Equal(new BeamCheck(BeamStatus.Unstable, 1, 2, 3), BeamStatics.Check(rollers));
            Assert.Null(BeamStatics.Solve(rollers));
            var pins = new BeamModel(4, new[] { Pin(0), Pin(4) }, new[] { load }, Array.Empty<BeamLineLoad>());
            Assert.Equal(new BeamCheck(BeamStatus.Indeterminate, 1, 4, 3), BeamStatics.Check(pins));
            var s = BeamStatics.Solve(pins)!;
            Assert.Equal(3, s.Normal(1), Digits);
            Assert.Equal(-3, s.Normal(3), Digits);
            Assert.Equal(-3, s.Reactions[0].Horizontal, Digits);
            Assert.Equal(-3, s.Reactions[1].Horizontal, Digits);
            // Without loads along the beam two pins stay determinate.
            Assert.Equal(BeamStatus.Determinate, BeamStatics.Check(new BeamModel(4, new[] { Pin(0), Pin(4) }, new[] { new BeamPointLoad(2, 1) }, Array.Empty<BeamLineLoad>())).Status);
        }

        [Theory]
        [InlineData(1.0 / 3, "F·L³", "F·L³/(3·EI)")]
        [InlineData(5.0 / 384, "q·L⁴", "5·q·L⁴/(384·EI)")]
        [InlineData(-0.5, "M·L²", "−M·L²/(2·EI)")]
        [InlineData(1, "F·L³", "F·L³/EI")]
        [InlineData(0.005416, "q·L⁴", "0.00542·q·L⁴/EI")]
        public void DeflectionCoefficients(double c, string numerator, string expected) => Assert.Equal(expected, BeamStatics.OverStiffness(c, numerator));

        [Theory]
        [InlineData(0.7071067811865476, "F", "√2·F/2")]
        [InlineData(0.8660254037844386, "F", "√3·F/2")]
        public void SymbolicValuesWithRoots(double c, string factor, string expected) => Assert.Equal(expected, BeamStatics.Symbolic(c, factor));

        private sealed class Tol : IEqualityComparer<double>
        {
            private readonly double _eps;
            public Tol(double eps) => _eps = eps;
            public bool Equals(double a, double b) => Math.Abs(a - b) <= _eps;
            public int GetHashCode(double v) => 0;
        }
    }

    /// <summary>The beam diagram with indeterminate beams, the deflection row, the normal force row and the stretch to the beam.</summary>
    public class BeamDiagramRowsTests
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

            public SketchObject Load(string label = "q")
                => Add("ann.load.distributed", 60, 38 - 9, o => (o with { Size = new Size2(80, 18) }).WithParam("label", label));

            /// <summary>A force whose tip touches the beam's upper side at x, coming from <paramref name="from"/> (relative).</summary>
            public SketchObject Force(double x, Vec2 from, string? label = null)
                => Add("ann.force", 0, 0, o =>
                {
                    o = o with { Points = new[] { new Vec2(x, 38) + from, new Vec2(x, 38) } };
                    return label is null ? o : o.WithParam("label", label);
                });

            public SketchObject Diagram(double width = 80, Func<SketchObject, SketchObject>? f = null)
                => Add("ann.beam.diagram", 60, 90, o => f is null ? o with { Size = new Size2(width, 44) } : f(o with { Size = new Size2(width, 44) }));

            public IReadOnlyList<Primitive> Render(SketchObject o, Lang lang = Lang.En) => R.Render(Doc.Get(o.Id)!, new RenderContext(Doc, lang));

            public string Text(SketchObject o, string tag, Lang lang = Lang.En) => T.All<TextPrim>(Render(o, lang)).Single(t => t.Tag == tag).Text;
        }

        [Fact]
        public void DrawnTwoSpanBeamWithUniformLoad()
        {
            var s = new Scene();
            s.Beam(); s.Pin(20); s.Roller(60); s.Roller(100); s.Load();
            var d = s.Diagram();
            // Spans l = L/2: M_B = −q·l²/8 = −q·L²/32 over the middle support, shear 5·q·l/8 = 5·q·L/16 beside it.
            Assert.Equal("max M = −q·L²/32", s.Text(d, "mmax"));
            Assert.Equal("x = L/2", s.Text(d, "mpos"));
            Assert.Equal("max V = −5·q·L/16", s.Text(d, "vmax"));
            Assert.Equal("statically indeterminate (degree 1), EI constant", s.Text(d, "status"));
        }

        [Fact]
        public void DrawnProppedCantileverWithNumbers()
        {
            var s = new Scene();
            s.Beam(length: 4); s.Wall(); s.Roller(100); s.Load("2 kN/m");
            var d = s.Diagram();
            Assert.Equal("max M = −4 kN·m", s.Text(d, "mmax")); // −q·L²/8
            Assert.Equal("max V = 5 kN", s.Text(d, "vmax"));    // 5·q·L/8
            Assert.Equal("x = 0 m", s.Text(d, "mpos"));
        }

        [Fact]
        public void OwnIndeterminatePresets()
        {
            var r = T.Reg();
            string Label(string item, string tag)
            {
                var (doc, o) = T.Insert(r, item, new Vec2(100, 100));
                return T.All<TextPrim>(T.Render(r, doc, o.Id)).Single(t => t.Tag == tag).Text;
            }
            Assert.Equal("max M = −q·L²/8", Label("ann.beam.diagram.propped", "mmax"));
            Assert.Equal("max V = 5·q·L/8", Label("ann.beam.diagram.propped", "vmax"));
            Assert.Equal("max M = −q·L²/32", Label("ann.beam.diagram.continuous", "mmax"));
            Assert.Equal("max M = −F·L/8", Label("ann.beam.diagram.fixed", "mmax"));
            Assert.Equal("statically indeterminate (degree 2), EI constant", Label("ann.beam.diagram.fixed", "status"));
            Assert.Equal("max w = F·L³/(3·EI)", Label("ann.beam.diagram.deflection", "wmax"));
            Assert.Equal("x = L", Label("ann.beam.diagram.deflection", "wpos"));
        }

        [Fact]
        public void DeflectionRowWithRealStiffness()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.beam.diagram.deflection", new Vec2(100, 100));
            // F = 10 kN at the end of a 2 m cantilever, E = 210000 N/mm², I = 1000 cm⁴: w = F·L³/(3·E·I) = 12.698 mm.
            var x = doc.Get(o.Id)!.WithParam("force", 10.0).WithParam("length", 2.0).WithParam("modulus", 210000.0).WithParam("inertia", 1000.0);
            doc = Edits.Replace(doc, x);
            var texts = T.All<TextPrim>(T.Render(r, doc, o.Id)).ToList();
            Assert.Equal("max w = 12.698 mm", texts.Single(t => t.Tag == "wmax").Text);
            Assert.Equal("x = 2 m", texts.Single(t => t.Tag == "wpos").Text);
            // Without E and I the value is left out.
            doc = Edits.Replace(doc, x with { Params = x.Params.Where(kv => kv.Key != "modulus").ToDictionary(kv => kv.Key, kv => kv.Value) });
            Assert.Equal("max w", T.All<TextPrim>(T.Render(r, doc, o.Id)).Single(t => t.Tag == "wmax").Text);
            // Uniform load on a simply supported beam: 5·q·L⁴/(384·EI).
            (doc, o) = T.Insert(r, "ann.beam.diagram.uniform", new Vec2(100, 100));
            doc = Edits.Replace(doc, doc.Get(o.Id)!.WithParam("deflection", true));
            Assert.Equal("max w = 5·q·L⁴/(384·EI)", T.All<TextPrim>(T.Render(r, doc, o.Id)).Single(t => t.Tag == "wmax").Text);
            Assert.Contains(T.All<PathPrim>(T.Render(r, doc, o.Id)), p => p.Tag == "deflection");
            // The parameters survive the JSON round trip.
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc)).Get(o.Id)!;
            Assert.True(back.Flag("deflection", false));
        }

        [Fact]
        public void DeflectionToggleAddsARowAndGrowsTheBox()
        {
            var s = new Scene();
            var d = s.Diagram();
            Assert.Equal(new[] { "V", "M" }, BeamDiagramType.Rows(s.Doc.Get(d.Id)!, s.Doc));
            var action = s.R.Actions(s.Doc.Get(d.Id)!, new RenderContext(s.Doc)).Single(a => a.Id == "ann.beamdiagram.deflection");
            Assert.False(action.IsActive);
            s.Doc = action.Run(s.Doc, s.Doc.Get(d.Id)!, new RenderContext(s.Doc));
            var after = s.Doc.Get(d.Id)!;
            Assert.Equal(new[] { "V", "M", "w" }, BeamDiagramType.Rows(after, s.Doc));
            Assert.Equal(66, after.Size.H, 9);
            Assert.Equal(90 - 22, after.Bounds().Y, 9); // the top edge stays
            // Each row keeps its height: the moment axis lies 1.5 rows of 22 mm below the top.
            var axes = T.All<LinePrim>(s.Render(after)).Where(l => l.Tag == "axis").Select(l => l.A.Y).OrderBy(y => y).ToList();
            Assert.Equal(new[] { 68 + 11.0, 68 + 33.0, 68 + 55.0 }, axes);
            // And off again.
            action = s.R.Actions(after, new RenderContext(s.Doc)).Single(a => a.Id == "ann.beamdiagram.deflection");
            Assert.True(action.IsActive);
            s.Doc = action.Run(s.Doc, after, new RenderContext(s.Doc));
            Assert.Equal(44, s.Doc.Get(d.Id)!.Size.H, 9);
            Assert.Equal(68, s.Doc.Get(d.Id)!.Bounds().Y, 9);
        }

        [Fact]
        public void InclinedForceAddsANormalForceRow()
        {
            var s = new Scene();
            var beam = s.Beam(length: 4);
            s.Pin(20); s.Roller(100);
            s.Force(60, new Vec2(-20, -20), "10 kN"); // down and to the right at 45°
            var d = s.Diagram();
            var c = BeamScene.Read(beam, s.Doc);
            Assert.Equal(10 / Math.Sqrt(2), c.PointLoads.Single().Amount.Value, 9);
            Assert.Equal(10 / Math.Sqrt(2), c.AxialLoads.Single().Amount.Value, 9);
            Assert.Equal(new[] { "N", "V", "M" }, BeamDiagramType.Rows(s.Doc.Get(d.Id)!, s.Doc));
            Assert.Equal("max N = 7.071 kN", s.Text(d, "nmax"));
            Assert.Contains(T.All<PathPrim>(s.Render(d)), p => p.Tag == "normal");
            // Symbolic: √2·F/2 tension in the left half.
            var s2 = new Scene();
            s2.Beam(); s2.Pin(20); s2.Roller(100);
            s2.Force(60, new Vec2(-20, -20));
            Assert.Equal("max N = √2·F/2", s2.Text(s2.Diagram(), "nmax"));
        }

        [Fact]
        public void ForceAlongTheBeamOnRollersIsUnstable()
        {
            var s = new Scene();
            s.Beam(); s.Roller(20); s.Roller(100);
            s.Force(100, new Vec2(20, 0)); // pushing the right end to the left
            var d = s.Diagram();
            Assert.Equal("unstable (mechanism)", s.Text(d, "note"));
            // With a pin the left end holds it: compression over the whole beam.
            var s2 = new Scene();
            s2.Beam(); s2.Pin(20); s2.Roller(100);
            s2.Force(100, new Vec2(20, 0));
            Assert.Equal("max N = −F", s2.Text(s2.Diagram(), "nmax"));
        }

        [Fact]
        public void DiagramStretchesToTheBeamAndCanFitItsBox()
        {
            var s = new Scene();
            s.Beam(); s.Pin(20); s.Roller(100); s.Load();
            var d = s.Diagram(width: 50); // narrower than the beam (20 … 100), centred under it
            var moment = T.All<PathPrim>(s.Render(d)).Single(p => p.Tag == "moment");
            var xs = moment.Segments.Where(g => g.Op != SegOp.Z).Select(g => g.P.X).ToList();
            Assert.Equal(20, xs.Min(), 6);
            Assert.Equal(100, xs.Max(), 6);
            var axis = T.All<LinePrim>(s.Render(d)).First(l => l.Tag == "axis");
            Assert.Equal(20, Math.Min(axis.A.X, axis.B.X), 6);
            var fit = s.R.Actions(s.Doc.Get(d.Id)!, new RenderContext(s.Doc)).Single(a => a.Id == "ann.beamdiagram.fit");
            Assert.True(fit.IsEnabled);
            s.Doc = fit.Run(s.Doc, s.Doc.Get(d.Id)!, new RenderContext(s.Doc));
            var after = s.Doc.Get(d.Id)!;
            Assert.Equal(80, after.Size.W, 6);
            Assert.Equal(60, after.Transform.X, 6);
            Assert.False(s.R.Actions(after, new RenderContext(s.Doc)).Single(a => a.Id == "ann.beamdiagram.fit").IsEnabled);
            // Without a beam there is nothing to fit.
            var lone = new Scene();
            var own = lone.Diagram();
            Assert.DoesNotContain(lone.R.Actions(lone.Doc.Get(own.Id)!, new RenderContext(lone.Doc)), a => a.Id == "ann.beamdiagram.fit");
        }

        [Fact]
        public void MirroredBeamMirrorsTheDiagram()
        {
            var s = new Scene();
            s.Add("ann.beam", 60, 40, o => o with { Size = new Size2(80, 4), Transform = o.Transform with { FlipX = true } });
            s.Pin(20); s.Roller(100);
            s.Force(40, new Vec2(0, -20)); // at a quarter from the page's left end = three quarters along the mirrored beam
            var d = s.Diagram();
            var moment = T.All<PathPrim>(s.Render(d)).Single(p => p.Tag == "moment");
            var peak = moment.Segments.Where(g => g.Op != SegOp.Z).OrderByDescending(g => g.P.Y).First().P;
            Assert.Equal(40, peak.X, 6); // the peak stays under the load on the page
        }
    }
}
