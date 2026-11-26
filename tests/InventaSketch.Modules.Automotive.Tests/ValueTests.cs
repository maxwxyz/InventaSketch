using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Automotive.Tests
{
    /// <summary>Time to collision and stopping distance as element values, with the checks auto.ttc.low and auto.braking.gap.</summary>
    public class CriticalityValueTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        private static SketchObject Item(Registry r, string id, Vec2 at) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        /// <summary>A safety distance dimension 50 mm long (5 m at the default 10 mm per m).</summary>
        private static SketchObject Dimension(Registry r) => Item(r, "auto.safetydistance", Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(50, 0) } };

        private static string LabelText(Registry r, SketchObject o, SketchDocument doc)
            => string.Join(" | ", r.Render(o, new RenderContext(doc)).OfType<TextPrim>().Select(t => t.Text));

        [Fact]
        public void TypesDeclareTheirValuesAndChecks()
        {
            var r = Fx.Reg();
            var ttc = r.Type("auto.motion.ttc")!;
            Assert.Equal(new[] { "vEgo", "vTarget", "gap", "ttc" }, ValueModel.Variables(ttc).Select(v => v.Key));
            Assert.Equal(new[] { "auto.ttc.low" }, ((IValueChecks)ttc).ValueChecks.Select(c => c.Id));
            var sd = r.Type("auto.motion.safetydistance")!;
            Assert.Equal(new[] { "v", "s", "gap", "tr", "a", "sR", "sB" }, ValueModel.Variables(sd).Select(v => v.Key));
            Assert.Equal(new[] { "auto.braking.reaction", "auto.braking.distance", "auto.braking.stopping" }, ValueModel.Relations(sd).Select(x => x.Id));
            Assert.Equal(new[] { "auto.braking.gap" }, ((IValueChecks)sd).ValueChecks.Select(c => c.Id));
            foreach (var t in new[] { ttc, sd })
            {
                Assert.Contains(t.Parameters, p => p.Key == ValueLabels.ShowParam);
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, t.Id);
                foreach (var v in ValueModel.Variables(t)) Assert.False(string.IsNullOrEmpty(v.Name.De), t.Id + "/" + v.Key);
                foreach (var c in ((IValueChecks)t).ValueChecks)
                {
                    Assert.StartsWith("auto.", c.Id);
                    Assert.False(string.IsNullOrEmpty(c.Message.De));
                    Assert.Null(c.Source); // no catalogued source fits a rule of thumb or the textbook kinematics
                }
            }
            // Other symbols have no values.
            Assert.False(ValueModel.HasValues(r.Type("auto.vehicle.car")));
        }

        [Fact]
        public void TextbookStoppingDistance()
        {
            // 100 km/h, 1 s reaction time, 8 m/s²: 27.8 m + 48.2 m = 76.0 m.
            var r = Fx.Reg();
            var o = Dimension(r).WithParam("speed", "100 km/h");
            var (doc, _) = Fx.Doc(o);
            var t = Solve(r, doc);
            Assert.Equal(27.78, Val(t, o, "sR")!.Value, 2);
            Assert.Equal(48.23, Val(t, o, "sB")!.Value, 2);
            Assert.Equal(76.0, Val(t, o, "s")!.Value, 1);
            Assert.Equal(1.0, Val(t, o, "tr")!.Value, 9);
            Assert.Equal(8.0, Val(t, o, "a")!.Value, 9);
            Assert.Equal(Provenance.Default, t.Get(o.Id, "tr")!.Provenance);
            Assert.Equal(Provenance.Derived, t.Get(o.Id, "s")!.Provenance);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void EnteredReactionTimeAndDecelerationReplaceTheDefaults()
        {
            var r = Fx.Reg();
            var o = Dimension(r).WithParam("speed", "100 km/h").WithParam("reaction", "1,5 s").WithParam("decel", "6 m/s²");
            var t = Solve(r, Fx.Doc(o).Doc);
            var v = 100 / 3.6;
            Assert.Equal(v * 1.5 + v * v / 12, Val(t, o, "s")!.Value, 6);
        }

        [Fact]
        public void SpeedFollowsFromAnEnteredBrakingDistance()
        {
            var r = Fx.Reg();
            var o = Dimension(r).WithParam("brakingDistance", "48.225 m");
            var t = Solve(r, Fx.Doc(o).Doc);
            Assert.Equal(100 / 3.6, Val(t, o, "v")!.Value, 3);
        }

        [Fact]
        public void GapComesFromTheDrawingUnlessEntered()
        {
            var r = Fx.Reg();
            var o = Dimension(r);
            var (doc, _) = Fx.Doc(o);
            Assert.Equal(5.0, Val(Solve(r, doc), o, "gap")!.Value, 9);
            var scaled = o.WithParam("scale", 2.0);
            Assert.Equal(25.0, Val(Solve(r, Fx.Doc(scaled).Doc), scaled, "gap")!.Value, 9);
            var entered = o.WithParam("gap", "80 m");
            var te = Solve(r, Fx.Doc(entered).Doc);
            Assert.Equal(80.0, Val(te, entered, "gap")!.Value, 9);
            Assert.Equal(Provenance.Entered, te.Get(entered.Id, "gap")!.Provenance);
            Assert.Empty(te.Conflicts);
            // The dimension's own value text shows the entered gap.
            var shown = entered.WithParam("showValue", true);
            Assert.Contains("d_safe = 80.0 m", LabelText(r, shown, Fx.Doc(shown).Doc));
        }

        [Fact]
        public void StoppingDistanceLongerThanTheGapWarns()
        {
            var r = Fx.Reg();
            var o = Dimension(r).WithParam("speed", "100 km/h");
            var f = Checks(r, Fx.Doc(o).Doc).Single(x => x.CheckId == "auto.braking.gap");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal(o.Id, f.ObjectId);
            Assert.DoesNotContain(Checks(r, Fx.Doc(o.WithParam("gap", "80 m")).Doc), x => x.CheckId == "auto.braking.gap");
            // Without a speed nothing is known and nothing is checked.
            Assert.Empty(Checks(r, Fx.Doc(Dimension(r)).Doc));
            // The library preset (50 km/h, 30 m) fits.
            var preset = Item(r, "auto.safetydistance.braking", Vec2.Zero);
            Assert.Empty(Checks(r, Fx.Doc(preset).Doc));
        }

        [Fact]
        public void TimeToCollisionFromGapAndSpeeds()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.ttc", Vec2.Zero).WithParam("vEgo", "100 km/h").WithParam("vTarget", "60 km/h").WithParam("gap", "20 m");
            var (doc, _) = Fx.Doc(o);
            var t = Solve(r, doc);
            Assert.Equal(20 / (40 / 3.6), Val(t, o, "ttc")!.Value, 9);
            Assert.Equal(Provenance.Derived, t.Get(o.Id, "ttc")!.Provenance);
            // The label draws the computed value.
            Assert.Contains("TTC = 1.8 s", LabelText(r, o, doc));
            var f = Checks(r, doc).Single(x => x.CheckId == "auto.ttc.low");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            // 30 m: 2.7 s, no warning.
            var far = o.WithParam("gap", "30 m");
            Assert.DoesNotContain(Checks(r, Fx.Doc(far).Doc), x => x.CheckId == "auto.ttc.low");
            Assert.Contains("TTC = 2.7 s", LabelText(r, far, Fx.Doc(far).Doc));
        }

        [Fact]
        public void NoCollisionWhenTheEgoVehicleIsNotFaster()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.ttc", Vec2.Zero).WithParam("vEgo", "50 km/h").WithParam("vTarget", "60 km/h").WithParam("gap", "20 m");
            var (doc, _) = Fx.Doc(o);
            Assert.Null(Val(Solve(r, doc), o, "ttc"));
            Assert.Contains("TTC = ∞", LabelText(r, o, doc));
            Assert.DoesNotContain(Checks(r, doc), x => x.CheckId == "auto.ttc.low");
        }

        [Fact]
        public void TtcSolvesBackwardsForTheEgoSpeed()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.ttc", Vec2.Zero).WithParam("ttc", 3.0).WithParam("vTarget", "50 km/h").WithParam("gap", "30 m");
            var t = Solve(r, Fx.Doc(o).Doc);
            Assert.Equal(50 / 3.6 + 10, Val(t, o, "vEgo")!.Value, 9);
            // Without entered speeds the label keeps showing the TTC parameter (quick sketch).
            var plain = Item(r, "auto.ttc", Vec2.Zero);
            Assert.Contains("TTC = 2.4 s", LabelText(r, plain, Fx.Doc(plain).Doc));
        }

        [Fact]
        public void ValuesLabelOfTheSafetyDistance()
        {
            var r = Fx.Reg();
            var o = Dimension(r).WithParam("speed", "100 km/h").WithParam(ValueLabels.ShowParam, true);
            var (doc, _) = Fx.Doc(o);
            var label = r.Render(o, new RenderContext(doc)).OfType<TextPrim>().Single(p => p.Tag == ValueLabels.Part);
            Assert.Contains("v = 100 km/h", label.Text);
            Assert.Contains("s = 76 m", label.Text);
            Assert.Contains("d = 5 m", label.Text);
        }

        [Fact]
        public void ValuesSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var ttc = Item(r, "auto.ttc.computed", new Vec2(50, 50));
            var sd = Item(r, "auto.safetydistance.braking", Vec2.Zero) with { Points = new[] { new Vec2(0, 80), new Vec2(60, 80) } };
            var (doc, _) = Fx.Doc(ttc, sd);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var t = Solve(r, back);
            Assert.Equal(25 / (40 / 3.6), Val(t, ttc, "ttc")!.Value, 9);
            Assert.Equal(30.0, Val(t, sd, "gap")!.Value, 9);
        }
    }

    /// <summary>V2X radio link, wheelchair user and tram.</summary>
    public class NewElementTests
    {
        private static SketchObject Item(Registry r, string id, Vec2 at) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        private static IReadOnlyList<Primitive> RenderLine(Registry r, SketchObject o) => r.Render(o, new RenderContext(Fx.Doc(o).Doc));

        [Fact]
        public void V2xLinkDrawsLineWavesArrowsAndLabel()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.v2x", Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(60, 0) } };
            var prims = RenderLine(r, o);
            var line = (LinePrim)prims.Single(p => p.Tag == AutoTags.Path);
            Assert.Equal(DashStyle.Dash, line.Style.Dash);
            Assert.Equal(ArrowHead.Triangle, line.Style.ArrowStart);
            Assert.Equal(ArrowHead.Triangle, line.Style.ArrowEnd);
            Assert.Equal(6, prims.Count(p => p.Tag == "wave"));
            Assert.Equal("V2V", prims.OfType<TextPrim>().Single().Text);

            var oneWay = o.WithParam("arrows", "end").WithParam("kind", "v2i");
            var p2 = RenderLine(r, oneWay);
            var l2 = (LinePrim)p2.Single(p => p.Tag == AutoTags.Path);
            Assert.Null(l2.Style.ArrowStart);
            Assert.Equal(3, p2.Count(p => p.Tag == "wave"));
            Assert.Equal("V2I", p2.OfType<TextPrim>().Single().Text);

            var zig = Item(r, "auto.v2x.v2p", Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(60, 0) } };
            var path = (PathPrim)RenderLine(r, zig).Single(p => p.Tag == AutoTags.Path);
            Assert.True(path.Segments.Count > 10, "zigzag");
            Assert.Contains(RenderLine(r, zig.WithParam("label", "PC5")), p => p is TextPrim t && t.Text == "PC5");
            Assert.DoesNotContain(RenderLine(r, o.WithParam("waves", false)), p => p.Tag == "wave");
        }

        [Fact]
        public void V2xEndsDockOntoVehicleMountsAndReverse()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.v2x")!;
            Assert.Equal(GeometryKind.Points, t.Geometry);
            var o = Item(r, "auto.v2x", Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(60, 0) } };
            var anchors = r.PageAnchors(o, new RenderContext(Fx.Doc(o).Doc)).ToList();
            Assert.Equal(new[] { "p0", "p1" }, anchors.Where(a => a.Role == AnchorRole.End).Select(a => a.Id));
            var rev = t.Actions(o, Lang.En).Single(a => a.Id == "auto.v2x.reverse").Apply(o);
            Assert.Equal(new Vec2(60, 0), rev.Points![0]);
            // The line choice has pictograms (dashed / zigzag) and so sits in the floating bar.
            Assert.True(t.Parameters.Single(p => p.Key == "course").HasOptionIcons);
        }

        [Theory]
        [InlineData("rollstuhl")]
        [InlineData("wheelchair")]
        [InlineData("straßenbahn")]
        [InlineData("tram")]
        [InlineData("v2x")]
        [InlineData("funkverbindung")]
        [InlineData("anhalteweg")]
        public void SearchFindsTheNewElements(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void WheelchairUserHasThreeViewsInThePeopleCategory()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.human.wheelchair")!;
            Assert.Equal(AutoCategories.Humans, t.Category);
            Assert.Equal(new[] { "side", "front", "top" }, t.Views.Select(v => v.Id));
            var side = Item(r, "auto.wheelchair.side", Vec2.Zero);
            var prims = t.Render(side, Fx.Ctx());
            Assert.Contains(prims, p => p.Tag == AutoTags.Wheel);
            Assert.Contains(prims, p => p.Tag == "head");
            // Same scale as a standing adult: 13.5 of 17.5 mm (seated in the chair) at the insert size.
            Assert.Equal(13.5, side.Size.H, 6);
        }

        [Fact]
        public void TramIsALongArticulatedVehicleThatSnapsOntoLanes()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.vehicle.tram")!;
            Assert.Equal(AutoCategories.Vehicles, t.Category);
            Assert.Equal(new[] { "top", "side", "front" }, t.Views.Select(v => v.Id));
            var top = Item(r, "auto.tram.top", new Vec2(100, 100));
            Assert.Equal(200 * AutomotiveModule.TopScale, top.Size.W, 6);
            var anchors = r.PageAnchors(top, new RenderContext(Fx.Doc(top).Doc)).ToList();
            Assert.Contains(anchors, a => a.Role == AnchorRole.Hub && a.Id == "center");
            Assert.Contains(anchors, a => a.Role == AnchorRole.Mount && a.Id == "mount.roof");
            var side = t.Render(Item(r, "auto.tram.side", Vec2.Zero), Fx.Ctx());
            Assert.Contains(side, p => p.Tag == "pantograph");
            Assert.Equal(4, side.Count(p => p.Tag == AutoTags.Wheel));
        }

        [Fact]
        public void WriteNewElementsScene()
        {
            var r = Fx.Reg();
            SketchObject At(string id, double x, double y) => Item(r, id, new Vec2(x, y));
            var road = At("auto.road.2", 169, 60) with { Size = new Size2(330, 50) };
            var tram = At("auto.tram.top", 120, 72.5);
            var car = At("auto.car.top", 230, 72.5);
            var lead = At("auto.car.top", 290, 47.5);
            var wheel = At("auto.wheelchair.top", 40, 30);
            var v2v = Item(r, "auto.v2x", Vec2.Zero) with { Points = new[] { new Vec2(230, 72.5), new Vec2(290, 47.5) } };
            var v2p = Item(r, "auto.v2x.v2p", Vec2.Zero) with { Points = new[] { new Vec2(120, 72.5), new Vec2(40, 30) } };
            var sd = Item(r, "auto.safetydistance.braking", Vec2.Zero) with { Points = new[] { new Vec2(245, 90), new Vec2(305, 90) } };
            var ttc = At("auto.ttc.computed", 260, 110);
            var sideTram = At("auto.tram.side", 120, 150);
            var frontTram = At("auto.tram.front", 220, 150);
            var ws = At("auto.wheelchair.side", 260, 160) with { Size = new Size2(23.2, 27) };
            var wf = At("auto.wheelchair.front", 290, 160) with { Size = new Size2(14, 27) };
            var doc = SketchDocument.Create(title: "New automotive elements");
            doc = Edits.Add(doc, doc.Pages[0].Id, road, tram, car, lead, wheel, v2v, v2p, sd, ttc, sideTram, frontTram, ws, wf);
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery-automotive");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "new-elements.svg"), SvgRenderer.RenderPage(r, doc, doc.Pages[0]), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "new-elements.svg")));
        }
    }
}
