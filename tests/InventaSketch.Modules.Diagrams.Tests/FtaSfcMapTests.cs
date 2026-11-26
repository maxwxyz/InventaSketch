using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Diagrams.Tests
{
    public class FtaTests
    {
        /// <summary>Adds a fault tree link from the bottom of <paramref name="parent"/> to the top of <paramref name="child"/>.</summary>
        internal static SketchDocument Link(Registry r, SketchDocument doc, string type, SketchObject parent, SketchObject child, bool reversed = false)
        {
            var (d, l) = T.Insert(r, type, new Vec2(0, 0), doc);
            var (a, b) = reversed ? (child, parent) : (parent, child);
            d = D.AttachPoint(d, l.Id, 0, a.Id, reversed ? a.BoxPoint(0, -0.5) : a.BoxPoint(0, 0.5));
            return D.AttachPoint(d, l.Id, 1, b.Id, reversed ? b.BoxPoint(0, 0.5) : b.BoxPoint(0, -0.5));
        }

        private static (SketchDocument Doc, SketchObject Top, SketchObject Or, SketchObject And, SketchObject B1, SketchObject B2, SketchObject B3) Tree(Registry r)
        {
            var (doc, top) = T.Insert(r, "diag.fta.event.top", new Vec2(100, 20));
            (doc, var or) = T.Insert(r, "diag.fta.gate.or", new Vec2(100, 45), doc);
            (doc, var and) = T.Insert(r, "diag.fta.gate.and", new Vec2(70, 75), doc);
            (doc, var b1) = T.Insert(r, "diag.fta.event.basic", new Vec2(55, 105), doc);
            (doc, var b2) = T.Insert(r, "diag.fta.event.basic", new Vec2(85, 105), doc);
            (doc, var b3) = T.Insert(r, "diag.fta.event.undeveloped", new Vec2(130, 75), doc);
            doc = Edits.Replace(doc, b1.WithParam("p", "0.1"));
            doc = Edits.Replace(doc, b2.WithParam("p", "20 %"));
            doc = Edits.Replace(doc, b3.WithParam("p", "5e-2"));
            doc = Link(r, doc, Fta.LinkType, top, or);
            doc = Link(r, doc, Fta.LinkType, or, and);
            doc = Link(r, doc, Fta.LinkType, or, b3);
            doc = Link(r, doc, Fta.LinkType, and, b1);
            // Drawn upwards from the basic event: read the other way round.
            doc = Link(r, doc, Fta.LinkType, and, b2, reversed: true);
            return (doc, top, or, and, b1, b2, b3);
        }

        [Fact]
        public void EveryFaultTreeElementInsertsWithoutParameters()
        {
            var r = T.Reg();
            foreach (var id in new[] { "diag.fta.gate.and", "diag.fta.gate.or", "diag.fta.gate.vote", "diag.fta.gate.inhibit", "diag.fta.event.basic", "diag.fta.event.top",
                "diag.fta.event.intermediate", "diag.fta.event.undeveloped", "diag.fta.event.house", "diag.fta.event.conditioning", "diag.fta.transfer", "diag.fta.link" })
            {
                var (doc, o) = T.Insert(r, id, new Vec2(100, 100));
                Assert.NotEmpty(T.Render(r, doc, o.Id));
                Assert.Equal(DiagCategories.Fta, r.TypeOf(o).Category);
                if (!o.IsPointGeometry) Assert.Equal("diag.fta.link", r.TypeOf(o).ChainConnector(o));
            }
            // Gate shapes differ; the k-of-n gate writes k/n.
            var (d, vote) = T.Insert(r, "diag.fta.gate.vote", new Vec2(50, 50));
            Assert.Contains(T.All<TextPrim>(T.Local(r, d, vote.Id)), t => t.Text == "2/3");
            var sigs = new[] { "and", "or", "inhibit" }.Select(k => { var (dd, g) = T.Insert(r, "diag.fta.gate." + k, new Vec2(50, 50)); return T.Sig(T.Local(r, dd, g.Id)); }).ToList();
            Assert.Equal(3, sigs.Distinct().Count());
            // Event shapes: circle, rectangle, diamond.
            var (d1, basic) = T.Insert(r, "diag.fta.event.basic", new Vec2(50, 50));
            Assert.Single(T.Local(r, d1, basic.Id).OfType<EllipsePrim>());
            var (d2, top) = T.Insert(r, "diag.fta.event.top", new Vec2(50, 50));
            Assert.Single(T.Local(r, d2, top.Id).OfType<RectPrim>());
            Assert.Contains(T.All<TextPrim>(T.Local(r, d2, top.Id)), t => t.Text == "Top event" && t.TextStyle.Bold);
        }

        [Fact]
        public void ProbabilitiesParseAndFormat()
        {
            Assert.Equal(1e-4, Fta.ParseProbability("1e-4"));
            Assert.Equal(0.001, Fta.ParseProbability("0,001"));
            Assert.Equal(0.025, Fta.ParseProbability(" 2.5 % ")!.Value, 12);
            Assert.Null(Fta.ParseProbability("1.5"));
            Assert.Null(Fta.ParseProbability("Test 12"));
            Assert.Null(Fta.ParseProbability(""));
            Assert.Equal("p = 0.069", Fta.Format(0.069, Lang.En));
            Assert.Equal("p = 1.2e-5", Fta.Format(1.2e-5, Lang.En));
            Assert.Equal("p = 0,069", Fta.Format(0.069, Lang.De));
            // At least 2 of 3 independent events with p = 0.1: 3·0.01·0.9 + 0.001.
            Assert.Equal(0.028, Fta.AtLeast(2, new[] { 0.1, 0.1, 0.1 }), 12);
            Assert.Equal(0, Fta.AtLeast(3, new[] { 0.5, 0.5 }));
        }

        [Fact]
        public void GatesComputeTheTopEventProbability()
        {
            var r = T.Reg();
            var (doc, top, or, and, b1, b2, b3) = Tree(r);
            Assert.Equal(0.02, Fta.Probability(doc, and.Id)!.Value, 12);
            var expected = 1 - (1 - 0.02) * (1 - 0.05);
            Assert.Equal(expected, Fta.Probability(doc, or.Id)!.Value, 12);
            Assert.Equal(expected, Fta.Probability(doc, top.Id)!.Value, 12);
            Assert.Equal(new[] { b1.Id, b2.Id }, Fta.Children(doc, and.Id));

            // The top event shows the value inside; the gates under events do not repeat it; leaves show their p below.
            var label = Assert.Single(T.All<TextPrim>(T.Local(r, doc, top.Id)), t => t.Tag == DiagTags.Value);
            Assert.Equal(Fta.Format(expected, Lang.En), label.Text);
            Assert.DoesNotContain(T.All<TextPrim>(T.Local(r, doc, or.Id)), t => t.Tag == DiagTags.Value);
            Assert.Equal("p = 0.1", Assert.Single(T.All<TextPrim>(T.Local(r, doc, b1.Id)), t => t.Tag == DiagTags.Value).Text);
            var de = r.TypeOf(top).Render(doc.Get(top.Id)!, new RenderContext(doc, Lang.De));
            Assert.Contains(T.All<TextPrim>(de), t => t.Text == Fta.Format(expected, Lang.De));

            // A k-of-n gate in place of the AND gate (2 of 2 = AND); an unknown input leaves the value open.
            var vote = doc.Get(and.Id)! with { Type = "diag.fta.gate.vote" };
            Assert.Equal(0.02, Fta.Probability(Edits.Replace(doc, vote), and.Id)!.Value, 12);
            Assert.Equal(1 - 0.9 * 0.8, Fta.Probability(Edits.Replace(doc, vote.WithParam("k", 1)), and.Id)!.Value, 12);
            var open = Edits.Replace(doc, doc.Get(b1.Id)!.WithParam("p", ""));
            Assert.Null(Fta.Probability(open, top.Id));
            Assert.DoesNotContain(T.All<TextPrim>(T.Local(r, open, top.Id)), t => t.Tag == DiagTags.Value);

            // A gate at the top of a tree (no event above it) shows its value to the right.
            var (d2, g) = T.Insert(r, "diag.fta.gate.and", new Vec2(40, 40));
            (d2, var e1) = T.Insert(r, "diag.fta.event.basic", new Vec2(30, 70), d2);
            d2 = Edits.Replace(d2, e1.WithParam("p", "0.5"));
            d2 = Link(r, d2, Fta.LinkType, g, d2.Get(e1.Id)!);
            Assert.Contains(T.All<TextPrim>(T.Local(r, d2, g.Id)), t => t.Tag == DiagTags.Value && t.Text == "p = 0.5");
        }

        [Fact]
        public void ChangingAnInputRedrawsEverythingAboveIt()
        {
            var r = T.Reg();
            var (doc, top, or, and, b1, _, _) = Tree(r);
            var ctx = new RenderContext(doc);
            Assert.Equal(new[] { and.Id, or.Id, top.Id }, r.TypeOf(b1).Dependents(doc.Get(b1.Id)!, ctx));
            Assert.True(r.TypeOf(top).DependsOnNeighbours);
            Assert.False(r.TypeOf(b1).DependsOnNeighbours);
            var link = doc.Objects.Values.First(o => o.Type == Fta.LinkType && o.Attach![1]!.ObjectId == b1.Id);
            Assert.Equal(new[] { and.Id, or.Id, top.Id, b1.Id }.OrderBy(x => x), r.TypeOf(link).Dependents(link, ctx).OrderBy(x => x));

            // The probability action sets p; empty clears it; a bad value is refused.
            var b = doc.Get(b1.Id)!;
            var act = r.TypeOf(b).Actions(b, Lang.En).Single(a => a.Id == "diag.fta.p");
            Assert.Equal("1e-3", act.Input!.Apply(b, "1e-3").Str("p", ""));
            Assert.False(act.Input!.Apply(b, " ").Params.ContainsKey("p"));
            Assert.Throws<FormatException>(() => act.Input!.Apply(b, "often"));
        }

        [Fact]
        public void ArrangeTreeLaysTheFaultTreeOutTopDown()
        {
            var r = T.Reg();
            var (doc, top, or, and, b1, b2, b3) = Tree(r);
            // Scatter the elements, then arrange from a basic event: the whole tree is laid out from its top event.
            doc = Edits.Move(doc, new[] { and.Id }, new Vec2(40, 30));
            doc = Edits.Move(doc, new[] { b3.Id }, new Vec2(-70, 60));
            var topBefore = doc.Get(top.Id)!.Bounds();
            doc = ContextActions.Run(r, doc, new[] { b2.Id }, "diag.fta.arrange", null, Lang.En);
            var b = new Dictionary<string, Rect>();
            foreach (var id in new[] { top.Id, or.Id, and.Id, b1.Id, b2.Id, b3.Id }) b[id] = doc.Get(id)!.Bounds();
            Assert.Equal(topBefore.Y, b[top.Id].Y, 6);
            Assert.Equal(topBefore.X + topBefore.W / 2, b[top.Id].X + b[top.Id].W / 2, 6);
            // Levels go down; the parent is centred over its children; siblings do not overlap.
            Assert.True(b[or.Id].Y > b[top.Id].Bottom);
            Assert.True(b[and.Id].Y > b[or.Id].Bottom);
            Assert.True(b[b1.Id].Y > b[and.Id].Bottom);
            Assert.Equal((b[b1.Id].X + b[b1.Id].W / 2 + b[b2.Id].X + b[b2.Id].W / 2) / 2, b[and.Id].X + b[and.Id].W / 2, 6);
            Assert.True(b[b1.Id].Right < b[b2.Id].X);
            Assert.False(b[and.Id].Intersects(b[b3.Id]));
            // The links follow (docked ends).
            var link = doc.Objects.Values.First(o => o.Type == Fta.LinkType && o.Attach![1]!.ObjectId == b1.Id);
            var pts = doc.ResolvePoints(link);
            Assert.True(pts[1].DistanceTo(doc.Get(b1.Id)!.BoxPoint(0, -0.5)) < 1e-6);
        }
    }

    public class SfcTests
    {
        [Fact]
        public void StepsTransitionsActionsAndBranchesInsertAndRender()
        {
            var r = T.Reg();
            var (doc, step) = T.Insert(r, "diag.sfc.step", new Vec2(50, 50));
            Assert.Single(T.Local(r, doc, step.Id).OfType<RectPrim>());
            Assert.Equal("1", Assert.Single(T.All<TextPrim>(T.Local(r, doc, step.Id))).Text);
            (doc, var init) = T.Insert(r, "diag.sfc.step.initial", new Vec2(50, 80), doc);
            Assert.Equal(2, T.Local(r, doc, init.Id).OfType<RectPrim>().Count()); // double frame
            var active = r.TypeOf(step).Actions(step, Lang.En).Single(a => a.Id == "diag.sfc.active").Apply(step);
            Assert.Single(T.Local(r, Edits.Replace(doc, active), step.Id).OfType<EllipsePrim>(), e => e.Tag == DiagTags.Marker);

            (doc, var tr) = T.Insert(r, "diag.sfc.transition", new Vec2(50, 65), doc);
            doc = Edits.Replace(doc, doc.Get(tr.Id)! with { Text = "start AND ready" });
            var trp = T.Local(r, doc, tr.Id);
            var bar = trp.OfType<LinePrim>().Single(l => l.Tag == DiagTags.Marker);
            var cond = Assert.Single(T.All<TextPrim>(trp));
            Assert.Equal(TextAnchor.Start, cond.TextStyle.Anchor);
            Assert.True(cond.Position.X > bar.B.X);

            (doc, var act) = T.Insert(r, "diag.sfc.action.stored", new Vec2(90, 50), doc);
            var texts = T.All<TextPrim>(T.Local(r, doc, act.Id)).Select(t => t.Text).ToList();
            Assert.Equal(new[] { "S", "Action" }, texts);
            Assert.Single(T.Local(r, doc, act.Id).OfType<LinePrim>(), l => l.Tag == DiagTags.Divider);

            (doc, var br) = T.Insert(r, "diag.sfc.branch", new Vec2(50, 120), doc);
            Assert.Equal(2, T.Local(r, doc, br.Id).OfType<LinePrim>().Count()); // double line
            var ports = r.TypeOf(br).Anchors(doc.Get(br.Id)!, new RenderContext(doc)).Select(a => a.Id).ToList();
            Assert.Contains("b2.bottom", ports);
            var more = r.TypeOf(br).Actions(br, Lang.En).Single(a => a.Id == "diag.sfc.branches.more").Apply(br);
            Assert.Equal(3, more.Int("branches", 0));
            Assert.True(more.Size.W > br.Size.W);
            Assert.Contains("b3.top", r.TypeOf(br).Anchors(more, new RenderContext(doc)).Select(a => a.Id));
            (doc, var alt) = T.Insert(r, "diag.sfc.branch.alternative", new Vec2(50, 140), doc);
            Assert.Single(T.Local(r, doc, alt.Id).OfType<LinePrim>());
            Assert.Equal("diag.sfc.link", r.TypeOf(step).ChainConnector(step));
        }

        [Fact]
        public void UpwardLinksLoopAroundOnTheLeftWithAnArrow()
        {
            var r = T.Reg();
            var (doc, s1) = T.Insert(r, "diag.sfc.step.initial", new Vec2(50, 30));
            (doc, var t1) = T.Insert(r, "diag.sfc.transition", new Vec2(50, 50), doc);
            (doc, var down) = T.Insert(r, "diag.sfc.link", new Vec2(0, 0), doc);
            doc = D.AttachPoint(doc, down.Id, 0, s1.Id, s1.BoxPoint(0, 0.5));
            doc = D.AttachPoint(doc, down.Id, 1, t1.Id, t1.BoxPoint(0, -0.5));
            var downPrims = T.Render(r, doc, down.Id);
            Assert.DoesNotContain(downPrims, p => p.Tag == DiagTags.Marker);
            Assert.Null(T.StyleOf(downPrims.Single(p => p.Tag == DiagTags.Main)).ArrowEnd);

            (doc, var back) = T.Insert(r, "diag.sfc.link", new Vec2(0, 0), doc);
            doc = D.AttachPoint(doc, back.Id, 0, t1.Id, t1.BoxPoint(0, 0.5));
            doc = D.AttachPoint(doc, back.Id, 1, s1.Id, s1.BoxPoint(0, -0.5));
            var type = (SfcLinkType)r.TypeOf(back);
            var path = type.Path(doc.Get(back.Id)!, doc.ResolvePoints(doc.Get(back.Id)!));
            Assert.Equal(6, path.Count);
            Assert.True(path.Min(p => p.X) < Math.Min(s1.Bounds().X, t1.Bounds().X));
            var prims = T.Render(r, doc, back.Id);
            var arrow = Assert.Single(prims, p => p.Tag == DiagTags.Marker);
            // The arrow points up the rising run.
            var pts = Prim.Points(arrow).ToList();
            Assert.True(pts[0].Y < pts[1].Y && pts[0].Y < pts[2].Y);
            // An element beside the column: the loop runs around it as well.
            (doc, var side) = T.Insert(r, "diag.sfc.action", new Vec2(30, 40), doc);
            var around = type.PathIn(doc.Get(back.Id)!, doc.ResolvePoints(doc.Get(back.Id)!), doc);
            Assert.True(around.Min(p => p.X) <= side.Bounds().X - 4 + 1e-9);
            Assert.Equal(around.Min(p => p.X), T.Render(r, doc, back.Id).Where(p => p.Tag == DiagTags.Main).SelectMany(Prim.Points).Min(v => v.X), 6);
            var off = type.Actions(doc.Get(back.Id)!, Lang.En).Single(a => a.Id == "diag.sfc.arrow").Apply(doc.Get(back.Id)!);
            Assert.DoesNotContain(T.Render(r, Edits.Replace(doc, off), back.Id), p => p.Tag == DiagTags.Marker);
        }
    }

    public class MapTests
    {
        [Fact]
        public void IshikawaShowsTheSixClassicCategoriesInBothLanguages()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.ishikawa", new Vec2(150, 100));
            Assert.DoesNotContain(o.Params, p => p.Key == "categories");
            string[] Cats(Lang lang) => T.All<TextPrim>(r.TypeOf(o).Render(doc.Get(o.Id)!, new RenderContext(doc, lang))).Where(t => t.Tag == DiagTags.Category).Select(t => t.Text).ToArray();
            Assert.Equal(new[] { "Man", "Machine", "Method", "Material", "Measurement", "Environment" }, Cats(Lang.En));
            Assert.Equal(new[] { "Mensch", "Maschine", "Methode", "Material", "Messung", "Mitwelt" }, Cats(Lang.De));
            var prims = T.Local(r, doc, o.Id);
            Assert.Contains(T.All<TextPrim>(prims), t => t.Text == "Effect" && t.Tag == DiagTags.Name);
            // Spine with an arrow into the head, six bones, three above and three below the spine.
            var spine = prims.Single(p => p.Tag == DiagTags.Main);
            Assert.Equal(ArrowHead.Triangle, T.StyleOf(spine).ArrowEnd);
            var bones = prims.OfType<LinePrim>().Where(l => l.Tag == DiagTags.Outline && Math.Abs(l.B.Y) < 1e-9 && Math.Abs(l.A.Y) > 1).ToList();
            Assert.Equal(6, bones.Count);
            Assert.Equal(3, bones.Count(l => l.A.Y < 0));
            Assert.Equal(6, prims.OfType<RectPrim>().Count(rp => rp.Tag == DiagTags.Outline) - 1); // category frames + head
        }

        [Fact]
        public void IshikawaCategoriesAndCausesAreEditable()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.ishikawa", new Vec2(150, 100));
            var type = r.TypeOf(o);
            var fewer = type.Actions(o, Lang.En).Single(a => a.Id == "diag.ishikawa.bones.fewer").Apply(o);
            Assert.Equal(5, IshikawaType.Categories(fewer, Lang.En).Count);
            var more = type.Actions(o, Lang.De).Single(a => a.Id == "diag.ishikawa.bones.more").Apply(o);
            Assert.Equal("Kategorie 7", IshikawaType.Categories(more, Lang.De).Last().Name);
            var renamed = type.Actions(o, Lang.En).Single(a => a.Id == "diag.ishikawa.rename.2").Input!.Apply(o, "Equipment");
            Assert.Equal("Equipment", IshikawaType.Categories(renamed, Lang.En)[1].Name);
            var cause = type.Actions(o, Lang.En).Single(a => a.Id == "diag.ishikawa.cause").Input!;
            var withCauses = cause.Apply(cause.Apply(renamed, "2: wear"), "2: vibration");
            var cats = IshikawaType.Categories(withCauses, Lang.En);
            Assert.Equal(new[] { "wear", "vibration" }, cats[1].Causes);
            Assert.Equal("Equipment", cats[1].Name);
            Assert.Throws<FormatException>(() => cause.Apply(o, "9: nothing"));
            var prims = T.Local(r, Edits.Replace(doc, withCauses), o.Id);
            Assert.Equal(new[] { "wear", "vibration" }, T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Cause).Select(t => t.Text));
            var box = new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01);
            Assert.True(box.Contains(T.Bounds(prims)));
            Assert.Null(type.ChainConnector(o));
        }

        private static (SketchDocument Doc, SketchObject Root, List<SketchObject> Kids) Map(Registry r, int n)
        {
            var (doc, root) = T.Insert(r, "diag.mindmap.node.central", new Vec2(150, 100));
            var kids = new List<SketchObject>();
            for (var i = 0; i < n; i++)
            {
                (doc, var k) = T.Insert(r, "diag.mindmap.node", new Vec2(200 + 3 * i, 60 + 10 * i), doc);
                kids.Add(k);
                (doc, var l) = T.Insert(r, "diag.mindmap.link", new Vec2(0, 0), doc);
                doc = D.AttachPoint(doc, l.Id, 0, root.Id, root.BoxPoint(0.5, 0));
                doc = D.AttachPoint(doc, l.Id, 1, k.Id, k.BoxPoint(-0.5, 0));
            }
            return (doc, root, kids);
        }

        [Fact]
        public void MindMapArrangesBothSidesToTheRightAndRadially()
        {
            var r = T.Reg();
            var (doc, root, kids) = Map(r, 4);
            // A grandchild hangs below the first child.
            (doc, var g) = T.Insert(r, "diag.mindmap.node.line", new Vec2(260, 40), doc);
            (doc, var gl) = T.Insert(r, "diag.mindmap.link", new Vec2(0, 0), doc);
            doc = D.AttachPoint(doc, gl.Id, 0, g.Id, g.BoxPoint(-0.5, 0.45));
            doc = D.AttachPoint(doc, gl.Id, 1, kids[0].Id, kids[0].BoxPoint(0.5, 0)); // drawn towards the parent: undirected
            Vec2 C(SketchDocument d, SketchObject o) => new(d.Get(o.Id)!.Transform.X, d.Get(o.Id)!.Transform.Y);

            var sides = ContextActions.Run(r, doc, new[] { root.Id }, "diag.mindmap.arrange=sides", null, Lang.En);
            Assert.Equal(C(doc, root), C(sides, root));
            Assert.Equal(2, kids.Count(k => C(sides, k).X > 150));
            Assert.Equal(2, kids.Count(k => C(sides, k).X < 150));
            Assert.True(C(sides, g).X > C(sides, kids[0]).X);

            var right = ContextActions.Run(r, doc, new[] { root.Id }, "diag.mindmap.arrange=right", null, Lang.En);
            Assert.All(kids, k => Assert.True(C(right, k).X > 150));
            var ys = kids.Select(k => C(right, k).Y).OrderBy(y => y).ToList();
            Assert.Equal(100, (ys.First() + ys.Last()) / 2, 0); // children centred on the root, grandchild included
            Assert.All(ys.Zip(ys.Skip(1), (a, b) => b - a), dy => Assert.True(dy >= 12 - 1e-6));

            var radial = ContextActions.Run(r, doc, new[] { root.Id }, "diag.mindmap.arrange=radial", null, Lang.En);
            var radii = kids.Select(k => C(radial, k).DistanceTo(C(radial, root))).ToList();
            Assert.All(radii, x => Assert.Equal(radii[0], x, 6));
            Assert.True(C(radial, g).DistanceTo(C(radial, root)) > radii[0] + 1);
            var boxes = kids.Select(k => radial.Get(k.Id)!.Bounds()).Append(radial.Get(root.Id)!.Bounds()).ToList();
            for (var i = 0; i < boxes.Count; i++)
                for (var j = i + 1; j < boxes.Count; j++)
                    Assert.False(boxes[i].Intersects(boxes[j]), "overlap " + i + "/" + j);
        }

        [Fact]
        public void MindMapNodesAndBranchesRender()
        {
            var r = T.Reg();
            var (doc, root, kids) = Map(r, 1);
            var link = doc.Objects.Values.Single(o => o.Type == MindMap.LinkType);
            var main = (PathPrim)T.Render(r, doc, link.Id).Single(p => p.Tag == DiagTags.Main);
            Assert.Equal(SegOp.C, main.Segments[1].Op);
            var straight = r.TypeOf(link).Actions(link, Lang.En).Single(a => a.Id == "diag.mindmap.curved").Apply(link);
            Assert.IsType<LinePrim>(T.Render(r, Edits.Replace(doc, straight), link.Id).Single(p => p.Tag == DiagTags.Main));
            Assert.Single(T.Local(r, doc, root.Id).OfType<EllipsePrim>());
            Assert.Contains(T.All<TextPrim>(T.Local(r, doc, root.Id)), t => t.TextStyle.Bold);
            Assert.Contains(T.All<TextPrim>(T.Local(r, doc, kids[0].Id)), t => t.Text == "Idea");
            Assert.Equal("diag.mindmap.link", r.TypeOf(root).ChainConnector(root));
            foreach (var shape in MindMapNodeType.Shapes.Select(s => s.Value))
                Assert.NotEmpty(T.Local(r, Edits.Replace(doc, kids[0].WithParam("shape", shape)), kids[0].Id));
        }

        [Theory]
        [InlineData("fehlerbaum", "diag.fta.gate.and")]
        [InlineData("fault tree", "diag.fta.event.basic")]
        [InlineData("grafcet", "diag.sfc.step")]
        [InlineData("schrittkette", "diag.sfc.transition")]
        [InlineData("fischgräte", "diag.ishikawa")]
        [InlineData("mindmap", "diag.mindmap.node")]
        public void SearchFindsTheNewFamilies(string query, string typeId) => Assert.Contains(T.Reg().Search(query), i => i.TypeId == typeId);
    }
}
