using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideTheBox()
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = Fx.InView(r, item, view);
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count > 70, "expected many item/view combinations, got " + count);
        }

        [Theory]
        [InlineData(2.5, 0.6)]
        [InlineData(0.4, 1.8)]
        [InlineData(3, 3)]
        [InlineData(0.3, 0.3)]
        public void FreeScalingKeepsPrimitivesInsideTheBox(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = Fx.InView(r, item, view);
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
            }
        }

        [Fact]
        public void AllParameterChoicesRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
            {
                var baseObj = t.Create(Vec2.Zero);
                foreach (var p in t.Parameters)
                {
                    IEnumerable<ParamValue> values = p.Kind switch
                    {
                        ParamKind.Choice => p.Options!.Select(o => (ParamValue)o.Value),
                        // Port names and designators are drawn outside the box by design (like ECAD labels).
                        ParamKind.Boolean when p.Key is "showPorts" or "showLabel" => Array.Empty<ParamValue>(),
                        ParamKind.Boolean => new ParamValue[] { true, false },
                        _ => Array.Empty<ParamValue>(),
                    };
                    foreach (var v in values)
                        foreach (var view in t.Views)
                        {
                            var o = r.WithView(baseObj, view.Id, Fx.Ctx()).WithParam(p.Key, v);
                            Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{t.Id}/{view.Id} {p.Key}={v}");
                        }
                }
            }
        }

        [Fact]
        public void EveryValveCombinationRendersInsideTheBox()
        {
            var r = Fx.Reg();
            var t = r.Type("fluid.valve.directional")!;
            var count = 0;
            foreach (var ways in DirectionalValves.WaysOptions)
                foreach (var left in DirectionalValves.Operators)
                    foreach (var right in new[] { "spring", "solenoid", "pilot", "none", "lever" })
                        foreach (var detent in new[] { false, true })
                        {
                            var o = t.Create(Vec2.Zero).WithParam("ways", ways).WithParam("actLeft", left).WithParam("actRight", right).WithParam("detent", detent);
                            var ft = (FluidType)t;
                            o = o with { Size = ft.Design(o) };
                            Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{ways} {left}/{right} detent={detent}");
                            count++;
                        }
            Assert.True(count > 500);
        }

        [Fact]
        public void PointTypesRenderAlongTheirPoints()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Points))
            {
                var o = r.Create(item, new Vec2(50, 50)) with { Points = new[] { new Vec2(20, 30), new Vec2(60, 45) } };
                var prims = Fx.Render(r, o);
                Assert.NotEmpty(prims);
                Fx.AssertFinite(prims, item.Id);
                var b = Prim.Bounds(prims)!.Value;
                Assert.True(b.Inflate(0.01).Contains(o.Points![0]) && b.Inflate(0.01).Contains(o.Points[o.Points.Count - 1]), item.Id);
            }
        }

        [Fact]
        public void OutputIsMonochromeSafe()
        {
            var r = Fx.Reg();
            static void Check(Primitive p, string what)
            {
                var style = p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, RectPrim re => re.Style, _ => null };
                if (p is TextPrim t) Assert.Null(t.TextStyle.Color);
                if (style is null) return;
                Assert.True(style.Stroke is null or "#000000", what + ": stroke " + style.Stroke);
                var fill = style.EffectiveFill;
                Assert.True(fill.Kind == FillKind.None || (fill.Kind == FillKind.Solid && fill.Color == "#000000"), what + ": fill " + fill.Kind + " " + fill.Color);
                Assert.True(style.Opacity is null, what + ": translucent");
            }
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(50, 50)).WithParam("ref", "1V1").WithParam("showPorts", true);
                foreach (var v in r.TypeOf(o).Views)
                    foreach (var p in Prim.Flatten(Fx.Render(r, r.WithView(o, v.Id, Fx.Ctx())))) Check(p, item.Id + "/" + v.Id);
            }
            // The monochrome SVG of a pneumatic and a hydraulic pump differs only where hollow and filled triangles differ.
            var doc = Fx.Doc(Fx.Item(r, "fluid.pump", new Vec2(40, 40)), Fx.Item(r, "fluid.compressor", new Vec2(80, 40))).Doc;
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.DoesNotContain("#ff", svg.Replace("#ffffff", ""), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HydraulicAndPneumaticDifferInTriangleFill()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "fluid.pump", "fluid.motor", "fluid.source", "fluid.valve.52.pilot" })
            {
                var o = Fx.Item(r, id);
                var toggle = Fx.Action(r, o, "fluid.medium");
                var other = toggle.Apply(o);
                string Medium(SketchObject x) => x.Str("medium", r.TypeOf(x).Parameters.Single(p => p.Key == "medium").Default!.Value.Text!);
                Assert.NotEqual(Medium(o), Medium(other));
                FillKind Fill(SketchObject x) => Fx.Render(r, x).OfType<PathPrim>().First(p => p.Tag is "triangle" or "pilot" && p.IsClosed).Style.EffectiveFill.Kind;
                var pneumatic = Medium(o) == "pneumatic" ? o : other;
                var hydraulic = Medium(o) == "pneumatic" ? other : o;
                Assert.Equal(FillKind.None, Fill(pneumatic));
                Assert.Equal(FillKind.Solid, Fill(hydraulic));
            }
        }

        [Fact]
        public void PortNamesAndDesignatorAreShownOnRequest()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "fluid.valve.43");
            Assert.DoesNotContain(Fx.Render(r, v), p => p is TextPrim);
            var named = Fx.Action(r, v, "fluid.ports").Apply(v);
            var texts = Fx.Render(r, named).OfType<TextPrim>().Where(t => t.Tag == "port").Select(t => t.Text).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "A", "B", "P", "T" }, texts);
            // Pneumatic: the return port is named R (exhaust), the id stays T.
            var pneu = Fx.Action(r, named, "fluid.medium").Apply(named);
            Assert.Contains(Fx.Render(r, pneu).OfType<TextPrim>(), t => t.Text == "R");
            Assert.Contains(Fx.Ports(r, pneu), a => a.Id == "T");

            var input = Fx.Action(r, v, "fluid.ref.set");
            Assert.NotNull(input.Input);
            Assert.Equal("Kennzeichen …", input.Label.Get(Lang.De));
            var labelled = input.Input!.Apply(v, " 1V2 ");
            Assert.Equal("1V2", labelled.Str("ref", ""));
            var label = Fx.Render(r, labelled).OfType<TextPrim>().Single(t => t.Tag == "label");
            Assert.Equal("1V2", label.Text);
            // Beside the valve (its ports are at the top and bottom), right-aligned to the left of the box.
            Assert.True(label.Position.X < -labelled.Size.W / 2);
            Assert.Equal(TextAnchor.End, label.TextStyle.Anchor);
            // A check valve has its ports left and right: the designator goes above.
            var check = Fx.Item(r, "fluid.check").WithParam("ref", "1V2");
            var cl = Fx.Render(r, check).OfType<TextPrim>().Single(t => t.Tag == "label");
            Assert.True(cl.Position.Y < -check.Size.H / 2);

            var toggle = Fx.Action(r, v, "fluid.label");
            Assert.Equal("1V1", toggle.Apply(v).Str("ref", ""));
            Assert.DoesNotContain(Fx.Render(r, Fx.Action(r, labelled, "fluid.label").Apply(labelled)), p => p.Tag == "label");

            // Rotated: the designator stays upright on the page.
            var rotated = labelled with { Transform = labelled.Transform with { Rotation = 90 } };
            Assert.All(r.Render(rotated, Fx.Ctx()).OfType<TextPrim>(), t => Assert.Equal(0, t.Rotation, 6));
        }

        [Fact]
        public void LineKindsUseDistinctStyles()
        {
            var r = Fx.Reg();
            DashStyle Dash(string id) => ((PathPrim)Fx.Render(r, Fx.Item(r, id)).Single()).Style.EffectiveDash;
            Assert.Equal(DashStyle.Solid, Dash("fluid.line"));
            Assert.Equal(DashStyle.LongDash, Dash("fluid.line.pilot"));
            Assert.Equal(DashStyle.Dash, Dash("fluid.line.drain"));
            var hose = (PathPrim)Fx.Render(r, Fx.Item(r, "fluid.line.hose")).Single();
            Assert.Contains(hose.Segments, s => s.Op == SegOp.C);
            Assert.Equal(DashStyle.DashDot, ((RectPrim)Fx.Render(r, Fx.Item(r, "fluid.enclosure")).Single()).Style.EffectiveDash);
        }

        [Fact]
        public void FrlViewSwitchKeepsProportions()
        {
            var r = Fx.Reg();
            var frl = Fx.Item(r, "fluid.frl");
            var simple = r.WithView(frl, "simplified", Fx.Ctx());
            Assert.Equal(1.5, simple.Size.W / simple.Size.H, 6);
            var action = Fx.Action(r, frl, "view.simplified");
            Assert.Equal(simple.Size, action.Apply(frl).Size);
            Assert.Contains(Fx.Render(r, frl), p => p.Tag == "enclosure");
            Assert.DoesNotContain(Fx.Render(r, simple), p => p.Tag == "enclosure");
        }
    }
}
