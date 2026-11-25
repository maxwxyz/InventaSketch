using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Diagrams.Tests
{
    internal static class T
    {
        public static Registry Reg() => new Registry().Register(new DiagramsModule());

        public static (SketchDocument Doc, SketchObject Obj) Insert(Registry r, string itemId, Vec2 at, SketchDocument? doc = null)
        {
            doc ??= SketchDocument.Create(PageSizes.A4Landscape);
            var o = r.Create(r.Item(itemId) ?? throw new KeyNotFoundException(itemId), at);
            return (Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        public static IReadOnlyList<Primitive> Render(Registry r, SketchDocument doc, string id) => r.Render(doc.Get(id)!, new RenderContext(doc));

        public static IReadOnlyList<Primitive> Local(Registry r, SketchDocument doc, string id)
        {
            var o = doc.Get(id)!;
            return r.TypeOf(o).Render(o, new RenderContext(doc));
        }

        public static IEnumerable<T2> All<T2>(IEnumerable<Primitive> prims) where T2 : Primitive => Prim.Flatten(prims).OfType<T2>();

        /// <summary>Bounds like <see cref="Prim.Bounds"/> but honouring ±90° rotated text (vertical lane headers).</summary>
        public static Rect Bounds(IEnumerable<Primitive> prims)
        {
            var pts = new List<Vec2>();
            foreach (var p in Prim.Flatten(prims))
            {
                if (p is TextPrim t && Math.Abs(Math.Abs(t.Rotation) - 90) < 1e-6)
                {
                    var w = Math.Max(1, t.Text.Length) * t.TextStyle.Size * 0.55;
                    var h = t.TextStyle.Size;
                    pts.Add(new Vec2(t.Position.X - h / 2, t.Position.Y - w / 2));
                    pts.Add(new Vec2(t.Position.X + h / 2, t.Position.Y + w / 2));
                }
                else pts.AddRange(Prim.Points(p));
            }
            return Rect.FromPoints(pts);
        }

        public static string Sig(IEnumerable<Primitive> prims) => string.Join("|", Prim.Flatten(prims).Select(p => p.GetType().Name + ":" +
            string.Join(";", Prim.Points(p).Select(v => v.X.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.Y.ToString("0.00", CultureInfo.InvariantCulture)))
            + (p is EllipsePrim e ? "r" + e.Rx.ToString("0.00", CultureInfo.InvariantCulture) : "")));

        public static Fill? FillOf(Primitive p) => p switch { PathPrim pp => pp.Style.Fill, RectPrim rp => rp.Style.Fill, EllipsePrim ep => ep.Style.Fill, LinePrim lp => lp.Style.Fill, _ => null };
        public static Style StyleOf(Primitive p) => p switch { PathPrim pp => pp.Style, RectPrim rp => rp.Style, EllipsePrim ep => ep.Style, LinePrim lp => lp.Style, _ => Style.Default };

        public static Vec2 StartOf(Primitive p) => p is LinePrim l ? l.A : ((PathPrim)p).Segments[0].P;
        public static Vec2 EndOf(Primitive p) => p is LinePrim l ? l.B : ((PathPrim)p).Segments.Last(s => s.Op != SegOp.Z).P;
    }

    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => T.Reg();

        [Fact]
        public void AllTypesAreBilingualAndUseDiagPrefix()
        {
            var r = T.Reg();
            Assert.True(r.Types.Count() >= 40);
            foreach (var t in r.Types)
            {
                Assert.StartsWith("diag.", t.Id);
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.True(t.Keywords.Count >= 4, t.Id + " has too few keywords");
                foreach (var p in t.Parameters) Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "." + p.Key);
                foreach (var v in t.Views) Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
            }
            foreach (var i in r.Library) Assert.False(string.IsNullOrEmpty(i.Name.De), i.Id);
        }

        [Fact]
        public void EveryTypeHasALibraryItemInAKnownCategory()
        {
            var r = T.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            Assert.Equal(13, cats.Count);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            foreach (var i in r.Library) Assert.Contains(i.Category, cats);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Theory]
        [InlineData("klasse", "diag.uml.class")]
        [InlineData("krähenfuß", "diag.er.connector")]
        [InlineData("lebenslinie", "diag.seq.lifeline")]
        [InlineData("schwimmbahn", "diag.activity.swimlane")]
        [InlineData("datenbank", "diag.sys.database")]
        [InlineData("komposition", "diag.uml.relation")]
        [InlineData("zustand", "diag.state.state")]
        [InlineData("gateway parallel", "diag.bpmn.gateway")]
        [InlineData("primärschlüssel", "diag.er.entity")]
        public void SearchFindsKeywords(string query, string typeId) => Assert.Contains(T.Reg().Search(query), i => i.TypeId == typeId);

        [Fact]
        public void ViewsUseKnownIcons()
        {
            var known = typeof(IconIds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!).ToHashSet();
            var r = T.Reg();
            var views = r.Types.SelectMany(t => t.Views).ToList();
            Assert.True(views.Count >= 5);
            Assert.All(views, v => Assert.Contains(v.Icon!, known));
            Assert.Equal(new[] { "diagram", "table", "chen" }, r.Type("diag.er.entity")!.Views.Select(v => v.Id));
            Assert.Equal(new[] { "full", "compact" }, r.Type("diag.uml.class")!.Views.Select(v => v.Id));
            foreach (var t in r.Types)
                foreach (var a in t.Actions(t.Create(new Vec2(50, 50)), Lang.En))
                    Assert.Contains(a.Icon, known);
        }
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInsideItsBoxOrNearItsPoints()
        {
            var r = T.Reg();
            foreach (var item in r.Library)
            {
                var (doc, o) = T.Insert(r, item.Id, new Vec2(120, 100));
                var prims = T.Render(r, doc, o.Id);
                Assert.True(prims.Count > 0, item.Id + " renders nothing");
                var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0]);
                Assert.Contains("data-isk-type=\"" + item.TypeId + "\"", svg);
                Assert.DoesNotContain("NaN", svg);
                if (o.IsPointGeometry)
                {
                    var b = T.Bounds(prims);
                    var area = Rect.FromPoints(o.Points!).Inflate(16);
                    Assert.True(area.Contains(b), item.Id + " renders far from its points: " + b);
                    Assert.True(b.W + b.H > 2, item.Id + " is degenerate");
                }
                else
                {
                    var local = T.Bounds(T.Local(r, doc, o.Id));
                    var box = new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01);
                    Assert.True(box.Contains(local), item.Id + " renders outside its box: " + local + " vs " + box);
                }
            }
        }

        [Fact]
        public void BoxItemsStayInsideWhenStretched()
        {
            var r = T.Reg();
            foreach (var item in r.Library.Where(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Box))
                foreach (var (sx, sy) in new[] { (2.0, 1.5), (0.8, 1.0), (1.0, 2.5) })
                {
                    var (doc, o) = T.Insert(r, item.Id, new Vec2(120, 100));
                    doc = Edits.Scale(doc, new[] { o.Id }, new Vec2(120, 100), sx, sy);
                    var s = doc.Get(o.Id)!.Size;
                    var local = T.Bounds(T.Local(r, doc, o.Id));
                    Assert.True(new Rect(-s.W / 2, -s.H / 2, s.W, s.H).Inflate(0.01).Contains(local), item.Id + " @" + sx + "x" + sy + ": " + local);
                }
        }

        [Fact]
        public void ClassTextParameterLinesBecomeTextPrimitives()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.class", new Vec2(80, 60));
            doc = Edits.Replace(doc, o.WithParam("attributes", "+ a: int\n_count: int\n- b: String").WithParam("operations", "+ f(): void\n# g(x: int)") with { Text = "Order", Size = new Size2(50, 40) });
            var prims = T.Local(r, doc, o.Id);
            var rows = T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Row).Select(t => t.Text).ToList();
            Assert.Equal(new[] { "+ a: int", "count: int", "- b: String", "+ f(): void", "# g(x: int)" }, rows);
            Assert.Equal("count: int", Assert.Single(T.All<TextPrim>(prims), t => t.TextStyle.Underline).Text); // static member
            Assert.Equal(2, T.All<LinePrim>(prims).Count(l => l.Tag == DiagTags.Divider));
            var name = T.All<TextPrim>(prims).Single(t => t.Tag == DiagTags.Name);
            Assert.Equal("Order", name.Text);
            Assert.True(name.TextStyle.Bold);
            Assert.All(T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Row), t => Assert.Equal(TextAnchor.Start, t.TextStyle.Anchor));
            // Rows are stacked downwards.
            var ys = T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Row).Select(t => t.Position.Y).ToList();
            Assert.True(ys.Zip(ys.Skip(1), (a, b) => b > a).All(x => x));
        }

        [Fact]
        public void InterfaceAndEnumShowStereotypes()
        {
            var r = T.Reg();
            foreach (var (id, st) in new[] { ("diag.uml.interface", "«interface»"), ("diag.uml.enum", "«enumeration»") })
            {
                var (doc, o) = T.Insert(r, id, new Vec2(50, 50));
                Assert.Contains(T.All<TextPrim>(T.Local(r, doc, o.Id)), t => t.Tag == DiagTags.Stereotype && t.Text == st);
            }
            var (d2, e) = T.Insert(r, "diag.uml.enum", new Vec2(50, 50));
            Assert.Equal(new[] { "RED", "GREEN", "BLUE" }, T.All<TextPrim>(T.Local(r, d2, e.Id)).Where(t => t.Tag == DiagTags.Row).Select(t => t.Text));
        }

        [Fact]
        public void CompactViewAndTogglesHideCompartments()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.class", new Vec2(50, 50));
            int Dividers(SketchObject x) => T.All<LinePrim>(T.Local(r, Edits.Replace(doc, x), o.Id)).Count(l => l.Tag == DiagTags.Divider);
            int Rows(SketchObject x) => T.All<TextPrim>(T.Local(r, Edits.Replace(doc, x), o.Id)).Count(t => t.Tag == DiagTags.Row);
            Assert.Equal(2, Dividers(o));
            Assert.Equal(3, Rows(o));
            Assert.Equal(0, Dividers(o with { View = "compact" }));
            Assert.Equal(0, Rows(o with { View = "compact" }));
            var noOps = r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == "diag.class.operations").Apply(o);
            Assert.Equal(1, Dividers(noOps));
            Assert.Equal(2, Rows(noOps));
            Assert.False(noOps.Flag("showOperations", true));
        }

        [Fact]
        public void EntityMarksKeysInAllViews()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.er.entity", new Vec2(80, 60));
            var attrs = "*id: int\nname: varchar\n#customer_id: int\n*#region_id: int";
            doc = Edits.Replace(doc, o.WithParam("attributes", attrs) with { Size = new Size2(60, 40) });
            var e = doc.Get(o.Id)!;

            var diagram = T.Local(r, doc, o.Id);
            var rows = T.All<TextPrim>(diagram).Where(t => t.Tag == DiagTags.Row).Select(t => t.Text).ToList();
            Assert.Equal(new[] { "id: int", "name: varchar", "customer_id: int (FK)", "region_id: int (FK)" }, rows);
            // Key rows use the native font underline (the whole row; no estimated underline lines any more).
            Assert.Equal(new[] { "id: int", "region_id: int (FK)" }, T.All<TextPrim>(diagram).Where(t => t.TextStyle.Underline).Select(t => t.Text));
            Assert.DoesNotContain(T.All<LinePrim>(diagram), l => l.Tag != DiagTags.Divider);

            var table = T.Local(r, Edits.Replace(doc, e with { View = "table" }), o.Id);
            Assert.Equal(new[] { "PK", "FK", "PK, FK" }, T.All<TextPrim>(table).Where(t => t.Tag == DiagTags.Marker).Select(t => t.Text));
            Assert.Equal(new[] { "id", "int", "name", "varchar", "customer_id", "int", "region_id", "int" }, T.All<TextPrim>(table).Where(t => t.Tag == DiagTags.Row).Select(t => t.Text));
            Assert.Equal(new[] { "id", "region_id" }, T.All<TextPrim>(table).Where(t => t.TextStyle.Underline).Select(t => t.Text));

            var chen = T.Local(r, Edits.Replace(doc, e with { View = "chen", Size = new Size2(90, 50) }), o.Id);
            Assert.Equal(4, T.All<EllipsePrim>(chen).Count());
            Assert.Equal(new[] { "id", "region_id" }, T.All<TextPrim>(chen).Where(t => t.TextStyle.Underline).Select(t => t.Text));

            var (d2, key) = T.Insert(r, "diag.er.attribute", new Vec2(40, 40));
            Assert.DoesNotContain(T.All<TextPrim>(T.Local(r, d2, key.Id)), t => t.TextStyle.Underline);
            d2 = Edits.Replace(d2, key.WithParam("key", true));
            Assert.All(T.All<TextPrim>(T.Local(r, d2, key.Id)), t => Assert.True(t.TextStyle.Underline));
            Assert.Contains("text-decoration=\"underline\"", SvgRenderer.RenderPage(r, d2, d2.Pages[0]));

            var weak = T.Local(r, Edits.Replace(doc, e.WithParam("weak", true)), o.Id);
            Assert.Equal(2, weak.Count(p => p.Tag == DiagTags.Outline));
        }

        [Fact]
        public void LongContentIsFittedIntoTheBox()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.class", new Vec2(50, 50));
            var many = string.Join("\n", Enumerable.Range(1, 8).Select(i => "- attribute" + i + ": int"));
            doc = Edits.Replace(doc, o.WithParam("attributes", many));
            var prims = T.Local(r, doc, o.Id);
            Assert.Equal(8 + 1, T.All<TextPrim>(prims).Count(t => t.Tag == DiagTags.Row));
            Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)));
            Assert.True(T.All<TextPrim>(prims).First(t => t.Tag == DiagTags.Row).TextStyle.Size < D.TextSize);
        }

        [Fact]
        public void MonochromeSvgHasNoColours()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(100, 100)) with { Style = new Style { Stroke = "#cc2200" } };
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
            }
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.DoesNotContain("#cc2200", svg);
            var colours = Regex.Matches(svg, "#[0-9a-fA-F]{6}").Cast<Match>().Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();
            Assert.All(colours, c => Assert.True(c is "#000000" or "#ffffff", c));
        }

        [Fact]
        public void StyleOverrideRecoloursLinesHeadsAndText()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.relation.composition", new Vec2(80, 60));
            doc = Edits.Replace(doc, o with { Text = "owns", Style = new Style { Stroke = "#0000ff", StrokeWidth = 0.7 } });
            var prims = T.Render(r, doc, o.Id);
            var main = T.StyleOf(prims.Single(p => p.Tag == DiagTags.Main));
            Assert.Equal("#0000ff", main.Stroke);
            Assert.Equal(0.7, main.StrokeWidth);
            Assert.Equal(ArrowHead.Diamond, main.ArrowEnd);
            // The head is drawn from the line style: same colour, filled in the stroke colour.
            var head = Assert.Single(ArrowHeads.Explode(prims.Single(p => p.Tag == DiagTags.Main)).Heads);
            Assert.Equal("#0000ff", T.FillOf(head)!.Color);
            Assert.Equal("#0000ff", T.StyleOf(head).Stroke);
            Assert.All(T.All<TextPrim>(prims), t => Assert.Equal("#0000ff", t.TextStyle.Color));
        }

        [Fact]
        public void UserArrowheadOverridesApplyToConnectors()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.relation.generalization", new Vec2(80, 60));
            Style Main(SketchObject x) => T.StyleOf(T.Render(r, Edits.Replace(doc, x), o.Id).Single(p => p.Tag == DiagTags.Main));
            Assert.Equal(ArrowHead.TriangleOpen, Main(o).ArrowEnd);
            Assert.Equal(D.HeadSize, Main(o).ArrowSize);
            var over = Main(o with { Style = new Style { ArrowEnd = ArrowHead.Stealth, ArrowStart = ArrowHead.Oval, ArrowSize = ArrowSize.Small } });
            Assert.Equal(ArrowHead.Stealth, over.ArrowEnd);
            Assert.Equal(ArrowHead.Oval, over.ArrowStart);
            Assert.Equal(ArrowSize.Small, over.ArrowSize);
            Assert.Equal(ArrowHead.None, Main(o with { Style = new Style { ArrowEnd = ArrowHead.None } }).ArrowEnd);

            var (d2, er) = T.Insert(r, "diag.er.connector", new Vec2(80, 60));
            var erMain = T.StyleOf(T.Render(r, Edits.Replace(d2, er with { Style = new Style { ArrowStart = ArrowHead.CrowMany } }), er.Id).Single(p => p.Tag == DiagTags.Main));
            Assert.Equal(ArrowHead.CrowMany, erMain.ArrowStart);
            Assert.Equal(ArrowHead.CrowZeroMany, erMain.ArrowEnd);
        }
    }

    public class ConnectorTests
    {
        private static IReadOnlyList<Anchor> Ports(Registry r, SketchDocument doc, string id)
            => r.PageAnchors(doc.Get(id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).ToList();

        [Fact]
        public void BoxesExposePortsAtEdgeMidpointsAndQuarterPoints()
        {
            var r = T.Reg();
            var (doc, box) = T.Insert(r, "diag.uml.class", new Vec2(80, 40));
            var ports = Ports(r, doc, box.Id).Select(a => a.P).ToList();
            Assert.Equal(12, ports.Count);
            double hw = box.Size.W / 2, hh = box.Size.H / 2;
            foreach (var p in new[] { new Vec2(80 + hw, 40), new Vec2(80 - hw / 2, 40 - hh), new Vec2(80 + hw, 40 + hh / 2), new Vec2(80, 40 + hh) })
                Assert.Contains(ports, q => q.DistanceTo(p) < 1e-9);
            // Every box type exposes ports, and they lie on (or inside) the box.
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
            {
                var (d, o) = T.Insert(r, t.Id, new Vec2(100, 100));
                var list = Ports(r, d, o.Id);
                Assert.True(list.Count >= 2, t.Id + " has no ports");
                var bounds = doc.Objects.Count >= 0 ? d.Get(o.Id)!.Bounds().Inflate(1e-6) : default;
                Assert.All(list, a => Assert.True(bounds.Contains(a.P), t.Id + " port outside box"));
            }
        }

        [Fact]
        public void ConnectorEndsSnapToPortsAndFollowTheBoxes()
        {
            var r = T.Reg();
            var (doc, a) = T.Insert(r, "diag.uml.class", new Vec2(50, 50));
            (doc, var b) = T.Insert(r, "diag.uml.class", new Vec2(150, 90), doc);
            (doc, var rel) = T.Insert(r, "diag.uml.relation.generalization", new Vec2(100, 70), doc);
            var opts = new SnapOptions { Grid = null };
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { rel.Id }, new RenderContext(doc), opts);

            // Start: near the upper quarter port of the right edge of A.
            var portA = a.BoxPoint(0.5, -0.25);
            var snapA = Snapper.SnapPoint(portA + new Vec2(0.7, 0.6), targets, opts);
            var pa = portA + new Vec2(0.7, 0.6) + snapA.Delta;
            Assert.True(pa.DistanceTo(portA) < 1e-9, "start did not snap to the quarter port");
            // End: near the top-left quarter port of B.
            var portB = b.BoxPoint(-0.25, -0.5);
            var pb = portB + new Vec2(-0.5, -0.8) + Snapper.SnapPoint(portB + new Vec2(-0.5, -0.8), targets, opts).Delta;
            Assert.True(pb.DistanceTo(portB) < 1e-9, "end did not snap to the quarter port");

            doc = D.AttachPoint(doc, rel.Id, 0, a.Id, pa);
            doc = D.AttachPoint(doc, rel.Id, 1, b.Id, pb);
            var prims = T.Render(r, doc, rel.Id);
            var main = prims.Single(p => p.Tag == DiagTags.Main);
            Assert.True(T.StartOf(main).DistanceTo(portA) < 1e-9);
            Assert.True(T.EndOf(main).DistanceTo(portB) < 1e-9);
            Assert.Equal(ArrowHead.TriangleOpen, T.StyleOf(main).ArrowEnd);
            var (shaft, heads) = ArrowHeads.Explode(main);
            Assert.Contains(Prim.Points(Assert.Single(heads)), v => v.DistanceTo(portB) < 1e-9); // triangle tip on the port
            Assert.True(T.EndOf(shaft).DistanceTo(portB) > 2); // line stops at the hollow triangle's base

            doc = Edits.Move(doc, new[] { b.Id }, new Vec2(10, 20));
            var moved = doc.ResolvePoints(doc.Get(rel.Id)!)[1];
            Assert.True(moved.DistanceTo(portB + new Vec2(10, 20)) < 1e-9);
            Assert.True(T.EndOf(T.Render(r, doc, rel.Id).Single(p => p.Tag == DiagTags.Main)).DistanceTo(moved) < 1e-9);
        }

        [Fact]
        public void BlockPortsAreSnapTargets()
        {
            var r = T.Reg();
            var (doc, block) = T.Insert(r, "diag.sys.block", new Vec2(100, 80));
            doc = Edits.Replace(doc, block.WithParam("portsLeft", 3));
            var ports = Ports(r, doc, block.Id).Where(p => p.Id is "L1" or "L2" or "L3").ToList();
            Assert.Equal(3, ports.Count);
            Assert.Equal(new Vec2(100 - block.Size.W / 2, 80), ports.Single(p => p.Id == "L2").P);
            Assert.Equal(4, T.Local(r, doc, block.Id).Count(p => p.Tag == DiagTags.Port)); // 3 left + 1 right
            var targets = Snapper.Targets(r, doc, doc.Pages[0], Array.Empty<string>(), new RenderContext(doc), new SnapOptions { Grid = null });
            var l1 = ports.Single(p => p.Id == "L1").P;
            var snap = Snapper.SnapPoint(l1 + new Vec2(-1, 0.4), targets, new SnapOptions { Grid = null });
            Assert.True((l1 + new Vec2(-1, 0.4) + snap.Delta).DistanceTo(l1) < 1e-9);
        }

        [Fact]
        public void MessagesDockAnywhereOnLifelineTails()
        {
            var r = T.Reg();
            var (doc, life) = T.Insert(r, "diag.seq.lifeline", new Vec2(60, 80));
            // The tail is one rail (no port raster any more); head and bottom ports stay.
            Assert.DoesNotContain(Ports(r, doc, life.Id), p => p.Id!.StartsWith("tail.", StringComparison.Ordinal));
            var tail = Assert.Single(r.PageAnchors(doc.Get(life.Id)!, new RenderContext(doc)), a => a.Role == AnchorRole.Rail);
            Assert.Equal(LifelineType.TailId, tail.Id);
            Assert.Equal(60, tail.P.X, 9);
            Assert.Equal(60, tail.Q.X, 9);
            Assert.Equal(80 + life.Size.H / 2, Math.Max(tail.P.Y, tail.Q.Y), 9);

            var opts = new SnapOptions { Grid = null };
            var targets = Snapper.Targets(r, doc, doc.Pages[0], Array.Empty<string>(), new RenderContext(doc), opts);
            var raw = new Vec2(60.8, 93.3);
            var res = Snapper.SnapPoint(raw, targets, opts);
            Assert.Equal("rail", res.Kind);
            var snapped = raw + res.Delta;
            Assert.Equal(60, snapped.X, 9);
            Assert.Equal(93.3, snapped.Y, 9);

            // The docked message end is attached to the lifeline and follows it.
            (doc, var msg) = T.Insert(r, "diag.seq.message", new Vec2(100, 93.3), doc);
            var att = Attachments.FromSnap(doc, snapped, res)!;
            Assert.Equal(life.Id, att.ObjectId);
            doc = Edits.SetPoint(doc, msg.Id, 0, snapped, att);
            doc = Edits.Move(doc, new[] { life.Id }, new Vec2(15, 0));
            var end = doc.ResolvePoints(doc.Get(msg.Id)!)[0];
            Assert.Equal(75, end.X, 9);
            Assert.Equal(93.3, end.Y, 9);
        }

        [Fact]
        public void ActivationBarDocksOntoLifelineTail()
        {
            var r = T.Reg();
            var (doc, life) = T.Insert(r, "diag.seq.lifeline", new Vec2(60, 80));
            (doc, var bar) = T.Insert(r, "diag.seq.activation", new Vec2(61.1, 90.4), doc);
            var ctx = new RenderContext(doc);
            var opts = new SnapOptions { Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(bar, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { bar.Id }, ctx, opts), opts);
            // Centred by its hub on the tail axis (a side port docking on the rail would put an edge on the line).
            Assert.Equal("axis", res.Kind);
            Assert.Equal(60, 61.1 + res.Delta.X, 9);
            Assert.Equal(0, res.Delta.Y, 9);
        }

        [Fact]
        public void CrowsFootEndVariantsRenderDistinctGeometry()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.er.connector", new Vec2(100, 100));
            IReadOnlyList<Primitive> Heads(string v)
                => ArrowHeads.Explode(T.Render(r, Edits.Replace(doc, o.WithParam("endEnd", v).WithParam("startEnd", "none")), o.Id).Single(p => p.Tag == DiagTags.Main)).Heads;
            var sigs = new Dictionary<string, string>();
            foreach (var (value, _, _) in new[] { ("none", "", "") }.Concat(D.CrowCycle.Select(e => (D.EndName(e), "", ""))))
            {
                var heads = Heads(value);
                sigs[value] = T.Sig(heads);
                // The notation sits at the end point and never sticks out more than 7 mm along the line.
                var end = o.Points![1];
                Assert.All(heads.SelectMany(Prim.Points), p => Assert.True(p.DistanceTo(end) < 7.5, value));
                Assert.All(heads, h => Assert.NotEqual(FillKind.Solid, T.StyleOf(h).EffectiveFill.Kind)); // background independent
            }
            Assert.Equal(7, sigs.Values.Distinct().Count());
            Assert.Equal("", sigs["none"]);
            Assert.Equal(ArrowHead.CrowZeroMany, T.StyleOf(T.Render(r, doc, o.Id).Single(p => p.Tag == DiagTags.Main)).ArrowEnd);
            Assert.Equal(ArrowHead.CrowOneOnly, T.StyleOf(T.Render(r, doc, o.Id).Single(p => p.Tag == DiagTags.Main)).ArrowStart);

            int Count<TP>(string v) where TP : Primitive => T.All<TP>(Heads(v)).Count();
            Assert.Equal(1, Count<LinePrim>("one"));
            Assert.Equal(2, Count<LinePrim>("onlyOne"));
            Assert.Equal(1, Count<EllipsePrim>("zeroOrOne"));
            Assert.Equal(1, Count<PathPrim>("many"));
            Assert.Equal(1, Count<LinePrim>("oneOrMany"));
            Assert.Equal(1, Count<EllipsePrim>("zeroOrMany"));
        }

        [Fact]
        public void CycleActionStepsThroughCrowsFootNotations()
        {
            var r = T.Reg();
            var (_, o) = T.Insert(r, "diag.er.connector", new Vec2(100, 100));
            var type = r.TypeOf(o);
            var seen = new List<string>();
            var x = o;
            for (var i = 0; i < 6; i++)
            {
                x = type.Actions(x, Lang.En).Single(a => a.Id == "diag.crow.end").Apply(x);
                seen.Add(x.Str("endEnd", ""));
            }
            Assert.Equal(new[] { "one", "onlyOne", "zeroOrOne", "many", "oneOrMany", "zeroOrMany" }, seen);
            var swapped = type.Actions(o, Lang.En).Single(a => a.Id == "diag.swap").Apply(o);
            Assert.Equal("zeroOrMany", swapped.Str("startEnd", ""));
            Assert.Equal("onlyOne", swapped.Str("endEnd", ""));
        }

        [Fact]
        public void UmlRelationKindsUseTheirNotation()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.relation", new Vec2(100, 100));
            IReadOnlyList<Primitive> R(string kind) => T.Render(r, Edits.Replace(doc, o.WithParam("kind", kind)), o.Id);
            Primitive Main(string kind) => R(kind).Single(p => p.Tag == DiagTags.Main);
            ArrowHead? End(string kind) => T.StyleOf(Main(kind)).ArrowEnd;
            Primitive Head(string kind) => ArrowHeads.Explode(Main(kind)).Heads.Single();
            DashStyle Dash(string kind) => T.StyleOf(Main(kind)).EffectiveDash;

            Assert.Null(End("association"));
            Assert.Equal(ArrowHead.Open, End("directed"));
            Assert.Equal(ArrowHead.Open, End("dependency"));
            Assert.Equal(ArrowHead.TriangleOpen, End("generalization"));
            Assert.Equal(ArrowHead.TriangleOpen, End("realization"));
            Assert.Equal(ArrowHead.Diamond, End("composition"));
            Assert.Equal(ArrowHead.DiamondOpen, End("aggregation"));
            Assert.Null(End("link"));
            // Hollow heads are unfilled outlines; the line stops at their base (no paper fill needed).
            Assert.Equal(FillKind.None, T.StyleOf(Head("generalization")).EffectiveFill.Kind);
            Assert.Equal(4, ((PathPrim)Head("generalization")).Segments.Count); // triangle: M L L Z
            Assert.Equal(D.Ink, T.FillOf(Head("composition"))!.Color);
            Assert.Equal(FillKind.None, T.StyleOf(Head("aggregation")).EffectiveFill.Kind);
            Assert.Equal(5, ((PathPrim)Head("aggregation")).Segments.Count); // diamond: M L L L Z
            Assert.False(((PathPrim)Head("directed")).IsClosed);
            Assert.Equal(DashStyle.Solid, Dash("generalization"));
            Assert.Equal(DashStyle.Dash, Dash("realization"));
            Assert.Equal(DashStyle.Dash, Dash("dependency"));
            Assert.Equal(DashStyle.Dash, Dash("link"));
            Assert.Equal(DashStyle.Solid, T.StyleOf(Head("realization")).EffectiveDash);
        }

        [Fact]
        public void SwapKeepsMultiplicitiesAtTheirClassEnds()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.uml.relation.multiplicity", new Vec2(100, 100));
            doc = Edits.Replace(doc, o.WithParam("kind", "aggregation"));
            o = doc.Get(o.Id)!;
            Vec2 Pos(SketchDocument d, string text) => T.All<TextPrim>(T.Render(r, d, o.Id)).Single(t => t.Text == text).Position;
            var one = Pos(doc, "1");
            var many = Pos(doc, "0..*");
            Assert.True(one.X < many.X);
            var swapped = r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == "diag.swap").Apply(o);
            Assert.Equal(o.Points![0], swapped.Points![1]);
            var d2 = Edits.Replace(doc, swapped);
            Assert.True(Pos(d2, "1").DistanceTo(one) < 3);
            Assert.True(Pos(d2, "0..*").DistanceTo(many) < 3);
            // The diamond moved to the other end.
            var main = T.Render(r, d2, o.Id).Single(p => p.Tag == DiagTags.Main);
            Assert.Equal(ArrowHead.DiamondOpen, T.StyleOf(main).ArrowEnd);
            Assert.True(T.EndOf(main).DistanceTo(swapped.Points[1]) < 1e-9);
        }

        [Fact]
        public void OrthogonalRouteLeavesAttachedEdgesPerpendicular()
        {
            var r = T.Reg();
            var (doc, a) = T.Insert(r, "diag.er.entity", new Vec2(50, 50));
            (doc, var b) = T.Insert(r, "diag.er.entity", new Vec2(150, 110), doc);
            (doc, var c) = T.Insert(r, "diag.er.connector", new Vec2(100, 80), doc);
            doc = D.AttachPoint(doc, c.Id, 0, a.Id, a.BoxPoint(0.5, 0));
            doc = D.AttachPoint(doc, c.Id, 1, b.Id, b.BoxPoint(0, -0.5));
            doc = Edits.Replace(doc, doc.Get(c.Id)!.WithParam("route", "orthogonal"));
            var main = Assert.IsType<PathPrim>(T.Render(r, doc, c.Id).Single(p => p.Tag == DiagTags.Main));
            var pts = main.Segments.Select(s => s.P).ToList();
            Assert.Equal(3, pts.Count);
            Assert.Equal(pts[0].Y, pts[1].Y, 9);
            Assert.Equal(pts[1].X, pts[2].X, 9);
        }

        [Fact]
        public void SelfMessageDrawsALoop()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.seq.message.self", new Vec2(100, 100));
            var prims = T.Render(r, doc, o.Id);
            var main = Assert.IsType<PathPrim>(prims.Single(p => p.Tag == DiagTags.Main));
            Assert.Equal(4, main.Segments.Count);
            Assert.True(main.Segments[1].P.X > 105);
            Assert.Equal(ArrowHead.Triangle, main.Style.ArrowEnd); // synchronous: filled head
        }
    }

    public class ActionTests
    {
        [Fact]
        public void AllContextActionsApplyAndKeepObjectRenderable()
        {
            var r = T.Reg();
            foreach (var item in r.Library)
            {
                var (doc, o) = T.Insert(r, item.Id, new Vec2(100, 100));
                foreach (var a in r.TypeOf(o).Actions(o, Lang.De))
                {
                    Assert.False(string.IsNullOrEmpty(a.Label.De), a.Id);
                    var changed = a.Apply(o);
                    Assert.Equal(o.Id, changed.Id);
                    Assert.NotEmpty(T.Render(r, Edits.Replace(doc, changed), o.Id));
                }
            }
        }

        [Fact]
        public void AddAndRemoveRowEditTheTextParameterAndKeepTheTopEdge()
        {
            var r = T.Reg();
            var (_, o) = T.Insert(r, "diag.uml.class", new Vec2(100, 100));
            var type = r.TypeOf(o);
            var top = o.BoxPoint(0, -0.5);
            var added = type.Actions(o, Lang.En).Single(a => a.Id == "diag.class.addAttribute").Apply(o);
            Assert.Equal("- id: int\n- name: String\n- attribute: Type", added.Str("attributes", ""));
            Assert.Equal(o.Size.H + D.TextSize * D.LineFactor, added.Size.H, 9);
            Assert.True(added.BoxPoint(0, -0.5).DistanceTo(top) < 1e-9);
            var removed = type.Actions(added, Lang.En).Single(a => a.Id == "diag.class.removeAttribute").Apply(added);
            Assert.Equal("- id: int\n- name: String", removed.Str("attributes", ""));
            Assert.Equal(o.Size.H, removed.Size.H, 9);

            var (_, e) = T.Insert(r, "diag.er.entity", new Vec2(100, 100));
            var e2 = r.TypeOf(e).Actions(e, Lang.En).Single(a => a.Id == "diag.er.addAttribute").Apply(e);
            Assert.Equal(4, EntityType.AttributesOf(e2).Count);
        }

        [Fact]
        public void SwimlaneActionsChangeOrientationAndLanes()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.activity.swimlane", new Vec2(150, 100));
            var type = r.TypeOf(o);
            var three = type.Actions(o, Lang.En).Single(a => a.Id == "diag.lanes.add").Apply(o);
            Assert.Equal(new[] { "Lane 1", "Lane 2", "Lane 3" }, D.Lines(three.Str("lanes", "")));
            // The lanes keep their size: the container grows by one lane.
            Assert.Equal(new Size2(o.Size.W * 1.5, o.Size.H), three.Size);
            var horizontal = type.Actions(three, Lang.En).Single(a => a.Id == "diag.lanes.orientation").Apply(three);
            Assert.Equal("horizontal", horizontal.Str("orientation", ""));
            Assert.Equal(new Size2(three.Size.H, three.Size.W), horizontal.Size);
            var texts = T.All<TextPrim>(T.Local(r, Edits.Replace(doc, horizontal), o.Id)).ToList();
            Assert.Equal(3, texts.Count);
            Assert.All(texts, t => Assert.Equal(-90, t.Rotation));
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void JsonRoundTripPreservesAllItemsAndRendering()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var x = 10.0;
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(x % 280, 20 + 30 * Math.Floor(x / 280)));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                x += 40;
            }
            var entity = doc.Objects.Values.First(o => o.Type == "diag.er.entity");
            var cls = doc.Objects.Values.First(o => o.Type == "diag.uml.class");
            var crow = doc.Objects.Values.First(o => o.Type == "diag.er.connector");
            doc = D.AttachPoint(doc, crow.Id, 0, entity.Id, entity.BoxPoint(0.5, 0.25));
            doc = D.AttachPoint(doc, crow.Id, 1, cls.Id, cls.BoxPoint(-0.25, -0.5));
            doc = Edits.Replace(doc, doc.Get(cls.Id)!.WithParam("attributes", "+ a: int\n_b: long") with { Text = "Ünïcödé «x»" });
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
        }
    }

    /// <summary>Writes an HTML/SVG gallery of all library items plus example diagrams for visual inspection.</summary>
    public class GalleryTests
    {
        public static string GalleryDir => Path.Combine(AppContext.BaseDirectory, "gallery");

        private const double Scale = 4; // px per mm

        private static string Svg(IReadOnlyList<Primitive> prims, Rect b)
        {
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = b });
            return svg.Replace("<svg ", "<svg width=\"" + (b.W * Scale).ToString("0", CultureInfo.InvariantCulture) + "\" height=\"" + (b.H * Scale).ToString("0", CultureInfo.InvariantCulture) + "\" ");
        }

        [Fact]
        public void WriteGallery()
        {
            var r = T.Reg();
            var html = new StringBuilder();
            html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Diagrams gallery</title><style>")
                .Append("body{font-family:Arial,sans-serif;margin:16px;background:#fff}h2{margin:18px 0 6px;font-size:16px}")
                .Append(".grid{display:flex;flex-wrap:wrap;gap:10px}.cell{border:1px solid #ddd;padding:6px;width:220px;height:190px;display:flex;flex-direction:column;align-items:center;justify-content:space-between}")
                .Append(".cell svg{max-width:208px;max-height:150px}.cap{font-size:11px;color:#333;text-align:center}</style></head><body>");
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                html.Append("<h2>").Append(cat.Name.En).Append(" / ").Append(cat.Name.De).Append("</h2><div class=\"grid\">");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var (doc, o) = T.Insert(r, item.Id, new Vec2(100, 100));
                    if (o.IsPointGeometry && o.Text is null && item.TypeId != "diag.uml.relation") doc = Edits.Replace(doc, o with { Text = item.TypeId == "diag.seq.message" ? "call()" : "places" });
                    var prims = T.Render(r, doc, o.Id);
                    var b = (o.IsPointGeometry ? T.Bounds(prims) : o.Bounds()).Inflate(3);
                    html.Append("<div class=\"cell\">").Append(Svg(prims, b)).Append("<div class=\"cap\">").Append(item.Id).Append("<br>").Append(item.Name.En).Append("</div></div>");
                }
                html.Append("</div>");
            }
            var scenes = Scenes(r);
            html.Append("<h2>Scenes</h2>").Append(scenes).Append("</body></html>");
            Directory.CreateDirectory(GalleryDir);
            File.WriteAllText(Path.Combine(GalleryDir, "diagrams-scene.html"), "<!doctype html><html><body style=\"margin:0;background:#fff\">" + scenes + "</body></html>");
            var file = Path.Combine(GalleryDir, "diagrams-gallery.html");
            File.WriteAllText(file, html.ToString());
            Assert.True(new FileInfo(file).Length > 10000);
        }

        /// <summary>Example diagrams composed of several objects with attached connectors.</summary>
        private static string Scenes(Registry r)
        {
            var doc = SketchDocument.Create(new Size2(300, 300));
            var pid = doc.Pages[0].Id;
            SketchObject Add(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = r.Create(r.Item(item)!, new Vec2(x, y));
                if (f is not null) o = f(o);
                doc = Edits.Add(doc, pid, o);
                return o;
            }
            void Link(string item, SketchObject a, (double U, double V) ua, SketchObject b, (double U, double V) ub, Func<SketchObject, SketchObject>? f = null)
            {
                var c = Add(item, 0, 0, f);
                doc = D.AttachPoint(doc, c.Id, 0, a.Id, a.BoxPoint(ua.U, ua.V));
                doc = D.AttachPoint(doc, c.Id, 1, b.Id, b.BoxPoint(ub.U, ub.V));
            }
            // Class diagram
            var shape = Add("diag.uml.class.abstract", 45, 25);
            var circle = Add("diag.uml.class", 20, 80, o => o.WithParam("attributes", "- radius: double").WithParam("operations", "+ area(): double") with { Text = "Circle", Size = new Size2(36, 26) });
            var rect = Add("diag.uml.class", 70, 80, o => o.WithParam("attributes", "- w: double\n- h: double").WithParam("operations", "+ area(): double") with { Text = "Rect", Size = new Size2(36, 30) });
            var drawable = Add("diag.uml.interface", 120, 25, o => o with { Text = "Drawable" });
            Link("diag.uml.relation.generalization", circle, (0, -0.5), shape, (-0.25, 0.5));
            Link("diag.uml.relation.generalization", rect, (0, -0.5), shape, (0.25, 0.5));
            Link("diag.uml.relation.realization", shape, (0.5, 0), drawable, (-0.5, 0));
            var canvas = Add("diag.uml.class", 140, 85, o => o with { Text = "Canvas", View = "compact", Size = new Size2(30, 10) });
            Link("diag.uml.relation.multiplicity", rect, (0.5, 0), canvas, (-0.5, 0), o => o.WithParam("kind", "aggregation") with { Text = "draws" });
            Add("diag.uml.note", 120, 115, o => o with { Text = "Shapes are\nimmutable", Size = new Size2(30, 14) });

            // ER diagram
            var customer = Add("diag.er.entity", 200, 30, o => o.WithParam("attributes", "*id: int\nname: varchar\nemail: varchar") with { Text = "Customer" });
            var order = Add("diag.er.entity.table", 270, 30, o => o.WithParam("attributes", "*id: int\n#customer_id: int\ndate: date") with { Text = "Order", Size = new Size2(46, 26) });
            Link("diag.er.connector", customer, (0.5, 0), order, (-0.5, 0), o => o with { Text = "places" });
            var line = Add("diag.er.entity.weak", 270, 85, o => o with { Size = new Size2(46, 26) });
            Link("diag.er.connector", order, (0, 0.5), line, (0, -0.5), o => o.WithParam("startEnd", "onlyOne").WithParam("endEnd", "oneOrMany"));
            Add("diag.er.entity.chen", 205, 95, o => o.WithParam("attributes", "*id: int\nname\nemail\nphone") with { Text = "Customer", Size = new Size2(70, 44) });

            // Sequence diagram
            var l1 = Add("diag.seq.lifeline.actor", 25, 180, o => o with { Size = new Size2(26, 90) });
            var l2 = Add("diag.seq.lifeline", 85, 180, o => o with { Text = ":Server", Size = new Size2(30, 90) });
            var l3 = Add("diag.seq.lifeline", 145, 180, o => o with { Text = ":DB", Size = new Size2(26, 90), Params = new Dictionary<string, ParamValue>(o.Params) { ["destroyed"] = true } });
            var act = Add("diag.seq.activation", 85, 180, o => o with { Size = new Size2(3.5, 50) });
            void Msg(string item, double x1, double x2, double y, string text) { Add(item, 0, 0, o => o with { Points = new[] { new Vec2(x1, y), new Vec2(x2, y) }, Text = text }); }
            Msg("diag.seq.message", 25, 83.25, 160, "login()");
            Msg("diag.seq.message", 86.75, 145, 172, "query()");
            Msg("diag.seq.message.return", 145, 86.75, 184, "rows");
            Add("diag.seq.message.self", 0, 0, o => o with { Points = new[] { new Vec2(86.75, 190), new Vec2(86.75, 198) }, Text = "check()" });
            Msg("diag.seq.message.return", 83.25, 25, 203, "token");
            Msg("diag.seq.message.async", 86.75, 145, 215, "log()");

            // Activity with swimlanes
            Add("diag.activity.swimlane", 230, 190, o => o with { Size = new Size2(120, 100), Text = null });
            var start = Add("diag.activity.initial", 200, 155);
            var a1 = Add("diag.activity.action", 200, 175, o => o with { Text = "Receive order", Size = new Size2(40, 12) });
            var dec = Add("diag.activity.decision", 200, 197);
            var a2 = Add("diag.activity.action", 260, 197, o => o with { Text = "Ship", Size = new Size2(30, 12) });
            var end = Add("diag.activity.final", 260, 225);
            Link("diag.uml.relation.directed", start, (0, 0.5), a1, (0, -0.5));
            Link("diag.uml.relation.directed", a1, (0, 0.5), dec, (0, -0.5));
            Link("diag.uml.relation.directed", dec, (0.5, 0), a2, (-0.5, 0), o => o with { Text = "[paid]" });
            Link("diag.uml.relation.directed", a2, (0, 0.5), end, (0, -0.5));

            // Architecture
            var user = Add("diag.sys.user", 20, 265);
            var web = Add("diag.sys.client", 60, 265);
            var cloud = Add("diag.sys.cloud", 110, 265);
            var block = Add("diag.sys.block.sysml", 165, 265, o => o with { Text = "API" });
            var q = Add("diag.sys.queue", 225, 250);
            var db = Add("diag.sys.database", 225, 280, o => o with { Size = new Size2(20, 18) });
            Add("diag.sys.server", 280, 265);
            Add("diag.sys.container", 195, 265, o => o with { Text = "Backend", Size = new Size2(110, 50) });
            Link("diag.uml.relation.directed", user, (0.5, 0), web, (-0.5, 0));
            Link("diag.uml.relation.directed", web, (0.5, 0), cloud, (-0.43, 0));
            Link("diag.uml.relation.directed", cloud, (0.43, 0), block, (-0.5, -1.0 / 6));
            Link("diag.uml.relation.dependency", block, (0.5, -1.0 / 6), q, (-0.5, 0), o => o.WithParam("route", "orthogonal"));
            Link("diag.uml.relation.dependency", block, (0.5, 1.0 / 6), db, (-0.5, 0), o => o.WithParam("route", "orthogonal"));

            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ObjectIds = false });
            return svg.Replace("width=\"300mm\" height=\"300mm\"", "width=\"1200\" height=\"1200\"");
        }
    }
}
