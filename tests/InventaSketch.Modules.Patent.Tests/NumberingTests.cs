using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;
using Xunit;

namespace InventaSketch.Modules.Patent.Tests
{
    /// <summary>A plain part (counts in the parts list) for numbering tests; parameter "kind" tells kinds apart.</summary>
    internal sealed class BlockType : ObjectType
    {
        public override string Id => "test.part.block";
        public override I18nText Name => new("Block", "Block");
        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H, Style.Visible) };
    }

    internal sealed class TestPartsModule : IModule
    {
        public string Id => "test";
        public I18nText Name => new("Test parts", "Testteile");
        public string Version => "1";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new BlockType() };
        public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("test.parts", new I18nText("Test", "Test"), Id, 1) };
        public IEnumerable<LibraryItem> Library => new[] { new LibraryItem("test.part.block", "test.part.block", new I18nText("Block", "Block"), "test.parts") };
    }

    internal static class Parts
    {
        public static Registry Reg() => Fx.Reg().Register(new TestPartsModule());

        public static SketchObject Block(Registry r, double x, double y, string kind, double w = 20, double h = 14)
            => (r.Create(r.Item("test.part.block")!, new Vec2(x, y)) with { Size = new Size2(w, h) }).WithParam("kind", kind);

        /// <summary>Five parts of different kinds in three rows (deliberately added out of reading order).</summary>
        public static SketchDocument Rows(Registry r, out string[] readingOrder)
        {
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            var topRight = Block(r, 150, 50, "a");
            var topLeft = Block(r, 50, 54, "b");           // slightly lower, still in the first row
            var middle = Block(r, 100, 120, "c");
            var bottomRight = Block(r, 160, 185, "d");
            var bottomLeft = Block(r, 60, 190, "e");
            doc = Edits.Add(doc, p, bottomRight, middle, topRight, bottomLeft, topLeft);
            readingOrder = new[] { topLeft.Id, topRight.Id, middle.Id, bottomLeft.Id, bottomRight.Id };
            return doc;
        }

        public static string? NumeralAt(SketchDocument doc, string partId)
            => doc.Objects.Values.Where(o => o.Type == PatentIds.Numeral && ReferenceNumerals.TargetOf(doc, o)?.Id == partId).Select(o => ReferenceNumerals.NumeralOf(o)).SingleOrDefault();
    }

    public class AutomaticNumberingTests
    {
        [Fact]
        public void NumbersPartsInReadingOrderWithLeaders()
        {
            var r = Parts.Reg();
            var doc = Parts.Rows(r, out var order);
            Assert.Equal(order, PartNumbering.PartsInReadingOrder(r, doc, doc.Pages[0]).Select(o => o.Id));

            var (numbered, list) = PartNumbering.NumberParts(r, doc, doc.Pages[0].Id);
            Assert.Equal(new[] { "10", "20", "30", "40", "50" }, order.Select(id => Parts.NumeralAt(numbered, id)));
            Assert.Equal(order, list.Select(x => x.PartId));
            // The part carries its numeral (parts list), the reference list names it, the lead line starts on the part.
            Assert.Equal(new[] { "10", "20", "30", "40", "50" }, order.Select(id => numbered.Get(id)!.Ref));
            Assert.Equal("Block", numbered.References["10"].Label);
            Assert.Contains(PartsList.Rows(r, numbered.Objects.Values, Lang.En), row => row.Numeral == "30");
            foreach (var x in list)
            {
                var n = numbered.Get(x.NumeralObjectId)!;
                Assert.True(numbered.Get(x.PartId)!.Bounds().Contains(numbered.ResolvePoints(n)[0]));
                Assert.False(numbered.Get(x.PartId)!.Bounds().Contains(numbered.ResolvePoints(n)[1]));
            }
            Assert.DoesNotContain(Fx.Auto(r, numbered), f => f.RuleId.StartsWith("PF-REF", StringComparison.Ordinal) || f.RuleId.StartsWith("PF-LEAD", StringComparison.Ordinal) || f.RuleId == "PF-PART-UNNUMBERED");

            var (ones, _) = PartNumbering.NumberParts(r, doc, doc.Pages[0].Id, new NumberingOptions { Scheme = NumeralScheme.Consecutive });
            Assert.Equal(new[] { "1", "2", "3", "4", "5" }, order.Select(id => Parts.NumeralAt(ones, id)));
        }

        [Fact]
        public void SkipsPartsThatAlreadyHaveANumeral()
        {
            var r = Parts.Reg();
            var doc = Parts.Rows(r, out var order);
            var p = doc.Pages[0].Id;
            var first = doc.Get(order[0])!;
            doc = ReferenceNumerals.Insert(r, doc, p, first.BoxPoint(0, 0), new Vec2(20, 30), "base plate", first.Id, "10").Doc;
            // A part that carries a numeral of its own keeps it and only gets the lead line.
            doc = Edits.Replace(doc, doc.Get(order[3])! with { Ref = "70" });
            var before = doc.Objects.Values.Count(o => o.Type == PatentIds.Numeral);

            var (numbered, list) = PartNumbering.NumberParts(r, doc, p);
            Assert.Equal(4, list.Count);
            Assert.DoesNotContain(list, x => x.PartId == order[0]);
            Assert.Equal(before + 4, numbered.Objects.Values.Count(o => o.Type == PatentIds.Numeral));
            Assert.Equal(new[] { "10", "20", "30", "70", "40" }, order.Select(id => Parts.NumeralAt(numbered, id)));
            Assert.Equal("base plate", numbered.References["10"].Label);

            // Nothing left to number: the same document comes back (no undo step).
            Assert.Same(numbered, PartNumbering.NumberParts(r, numbered, p).Doc);
            Assert.False(PartNumbering.HasUnnumbered(r, numbered, numbered.Pages[0]));
        }

        [Fact]
        public void RepeatedPartsShareSuffixOrSeparate()
        {
            var r = Parts.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            var screws = new[] { Parts.Block(r, 40, 60, "screw"), Parts.Block(r, 100, 60, "screw"), Parts.Block(r, 160, 60, "screw") };
            var plate = Parts.Block(r, 100, 150, "plate", 80, 30);
            doc = Edits.Add(doc, p, screws.Append(plate).ToArray());

            var shared = PartNumbering.NumberParts(r, doc, p).Doc;
            Assert.Equal(new[] { "10", "10", "10", "20" }, screws.Append(plate).Select(o => Parts.NumeralAt(shared, o.Id)));
            Assert.DoesNotContain(Fx.Auto(r, shared), f => f.RuleId == "PF-REF-DUPLICATE");

            var suffixed = PartNumbering.NumberParts(r, doc, p, new NumberingOptions { Repeated = RepeatedParts.Suffixed }).Doc;
            Assert.Equal(new[] { "10a", "10b", "10c", "20" }, screws.Append(plate).Select(o => Parts.NumeralAt(suffixed, o.Id)));
            Assert.Equal("10a, 10b, 10c Block\n20 Block 2\n", ReferenceNumerals.Export(suffixed));

            var separate = PartNumbering.NumberParts(r, doc, p, new NumberingOptions { Repeated = RepeatedParts.Separate }).Doc;
            Assert.Equal(new[] { "10", "20", "30", "40" }, screws.Append(plate).Select(o => Parts.NumeralAt(separate, o.Id)));
        }

        [Fact]
        public void NumberingIsOneUndoStep()
        {
            var r = Parts.Reg();
            var doc = Parts.Rows(r, out _);
            var p = doc.Pages[0].Id;
            doc = PatentExport.AddSheetFrame(r, doc, p, PatentProfiles.CommonSafe);
            var frame = doc.Objects.Values.Single(o => o.Type == PatentIds.Sheet);

            var actions = r.Actions(frame, new RenderContext(doc, Lang.De));
            var tens = actions.Single(a => a.Id == PartNumbering.ActionKey + "=tens");
            Assert.True(tens.IsEnabled);
            Assert.Equal("Alle Teile nummerieren", tens.Choice!.Label.De);
            Assert.True(tens.Choice.IsMenu);

            var history = new History(doc);
            history.Apply("Number all parts", d => ContextActions.Run(r, d, new[] { frame.Id }, PartNumbering.ActionKey + "=tens", null, Lang.De));
            Assert.Equal(5, history.Current.Objects.Values.Count(o => o.Type == PatentIds.Numeral));
            Assert.True(history.CanUndo);
            Assert.Same(doc, history.Undo());
            Assert.False(history.CanUndo);
            Assert.Equal(0, history.Current.Objects.Values.Count(o => o.Type == PatentIds.Numeral));

            // The same action is offered on a numeral; once every part is numbered it is greyed out.
            history.Redo();
            var numeral = history.Current.Objects.Values.First(o => o.Type == PatentIds.Numeral);
            Assert.False(r.Actions(numeral, new RenderContext(history.Current)).Single(a => a.Id == PartNumbering.ActionKey + "=ones").IsEnabled);
        }

        /// <summary>
        /// "Number all parts" without a selected element: page commands with the numeral's ids and menu, only on a page with
        /// parts, greyed out once every part has a numeral; running one numbers like the numeral's menu.
        /// </summary>
        [Fact]
        public void PageCommandsNumberWithoutASelection()
        {
            var r = Parts.Reg();
            var empty = SketchDocument.Create(PageSizes.A4Portrait);
            Assert.Empty(PartNumbering.PageCommands(r, empty, empty.Pages[0]));

            var doc = Parts.Rows(r, out _);
            var page = doc.Pages[0];
            var commands = PartNumbering.PageCommands(r, doc, page);
            Assert.Equal(new[] { PartNumbering.ActionKey + "=tens", PartNumbering.ActionKey + "=ones" }, commands.Select(c => c.Id));
            Assert.All(commands, c => Assert.True(c.IsEnabled));
            Assert.All(commands, c => Assert.Same(PartNumbering.Menu, c.Choice));
            Assert.Equal("1, 2, 3 …", commands[1].Label.De);

            var ones = commands[1].Run(r, doc, page.Id, Lang.De);
            Assert.Equal(new[] { "1", "2", "3", "4", "5" },
                ones.Objects.Values.Where(o => o.Type == PatentIds.Numeral).Select(o => ReferenceNumerals.NumeralOf(o)).OrderBy(n => int.Parse(n!)));

            var after = PartNumbering.PageCommands(r, ones, ones.Pages[0]);
            Assert.Equal(2, after.Count);
            Assert.All(after, c => Assert.False(c.IsEnabled));
        }
    }

    public class NumeralVariantTests
    {
        [Fact]
        public void SuffixesSplitAndCombine()
        {
            Assert.Equal(("10", "a"), ReferenceNumerals.SplitSuffix("10a"));
            Assert.Equal(("12", "''"), ReferenceNumerals.SplitSuffix("12″"));
            Assert.Equal(("12", "'"), ReferenceNumerals.SplitSuffix("(12')"));
            Assert.Equal(("A-3", ""), ReferenceNumerals.SplitSuffix("A-3"));
            Assert.Equal("10b", ReferenceNumerals.WithSuffix("10a", "b"));
            Assert.Equal("10", ReferenceNumerals.WithSuffix("10'", ""));
        }

        [Fact]
        public void VariantChoiceRenamesNumeralAndListGroupsVariants()
        {
            var r = Parts.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            var a = Parts.Block(r, 50, 80, "housing") with { Text = "housing" };
            var b = Parts.Block(r, 150, 80, "housing") with { Text = "housing" };
            var c = Parts.Block(r, 100, 160, "housing") with { Text = "housing" };
            doc = Edits.Add(doc, p, a, b, c);
            doc = PartNumbering.NumberParts(r, doc, p).Doc;
            var numerals = doc.Objects.Values.Where(o => o.Type == PatentIds.Numeral).OrderBy(o => o.Points![1].Y).ThenBy(o => o.Points![1].X).ToList();

            var options = r.Actions(numerals[1], new RenderContext(doc)).Where(x => x.Choice?.Key == "patent.numeral.suffix").ToList();
            Assert.Equal(ReferenceNumerals.Suffixes.Count, options.Count);
            Assert.True(options.Single(x => x.Id == "patent.numeral.suffix=none").IsActive);
            Assert.Equal("10a", options.Single(x => x.Id == "patent.numeral.suffix=a").Label.En);

            doc = ContextActions.Run(r, doc, new[] { numerals[1].Id }, "patent.numeral.suffix=a", null, Lang.En);
            doc = ContextActions.Run(r, doc, new[] { numerals[2].Id }, "patent.numeral.suffix='", null, Lang.En);
            Assert.Equal("10a", ReferenceNumerals.NumeralOf(doc.Get(numerals[1].Id)!));
            Assert.Equal("10a", doc.Get(b.Id)!.Ref); // the part follows its numeral
            Assert.Equal("10'", doc.Get(numerals[2].Id)!.Ref);
            Assert.Equal("housing", doc.References["10a"].Label);
            Assert.True(r.Actions(doc.Get(numerals[1].Id)!, new RenderContext(doc)).Single(x => x.Id == "patent.numeral.suffix=a").IsActive);

            // The list groups the variants of one number; variants are no "different numerals for one part".
            Assert.Equal(new[] { "10", "10'", "10a" }, ReferenceNumerals.Groups(doc).Single().Numerals);
            Assert.Equal("10, 10', 10a housing\n", ReferenceNumerals.Export(doc));
            Assert.DoesNotContain(ReferenceNumerals.Check(doc), i => i.Kind == ReferenceIssueKind.LabelWithDifferentNumerals);
            // A variant with its own name stays a separate line below its number.
            doc = Edits.SetReference(doc, "10b", "housing cover");
            Assert.Equal("10, 10', 10a housing\n10b housing cover\n", ReferenceNumerals.Export(doc));
            // Renumbering keeps the suffixes.
            var (renumbered, map) = ReferenceNumerals.Renumber(doc, new NumeralScheme(100, 100));
            Assert.Equal("100a", map["10a"]);
            Assert.Equal("100a", renumbered.Get(b.Id)!.Ref);
        }
    }

    public class NumeralPreflightTests
    {
        private static SketchDocument Numbered(Registry r, out SketchObject[] parts)
        {
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            parts = new[] { Parts.Block(r, 60, 80, "shaft"), Parts.Block(r, 150, 80, "gear"), Parts.Block(r, 100, 180, "bearing") };
            doc = Edits.Add(doc, p, parts);
            return PartNumbering.NumberParts(r, doc, p).Doc;
        }

        [Fact]
        public void ReportsNumeralPointingAtNothing()
        {
            var r = Parts.Reg();
            var doc = Numbered(r, out var parts);
            var p = doc.Pages[0].Id;
            Assert.DoesNotContain(Fx.Auto(r, doc), f => f.RuleId is "PF-LEAD-TARGET" or "PF-LEAD-FREE");

            // Loose numeral ending in empty space.
            var loose = r.Create(r.Item(PatentIds.Numeral)!, new Vec2(0, 0)) with { Points = new[] { new Vec2(60, 250), new Vec2(45, 240) }, Ref = "90" };
            var withLoose = Edits.Add(Edits.SetReference(doc, "90", "spring"), p, loose);
            var f1 = Fx.Auto(r, withLoose, PatentProfiles.Epo).Single(f => f.ObjectId == loose.Id && f.RuleId.StartsWith("PF-LEAD", StringComparison.Ordinal));
            Assert.Equal(("PF-LEAD-TARGET", Severity.Warning), (f1.RuleId, f1.Severity));
            Assert.Contains("points at nothing", f1.Message.En);
            Assert.Contains("https://", f1.Source);

            // Numeral attached to a part that was deleted (e.g. in the host, outside the engine).
            var numeral = doc.Objects.Values.First(o => o.Type == PatentIds.Numeral && ReferenceNumerals.TargetOf(doc, o)?.Id == parts[1].Id);
            var orphan = doc with { Objects = doc.Objects.Where(kv => kv.Key != parts[1].Id).ToDictionary(kv => kv.Key, kv => kv.Value), Pages = doc.Pages.Select(pg => pg with { Order = pg.Order.Where(id => id != parts[1].Id).ToList() }).ToList() };
            Assert.Contains(Fx.Auto(r, orphan, PatentProfiles.Uspto), f => f.RuleId == "PF-LEAD-TARGET" && f.ObjectId == numeral.Id && f.Message.En.Contains("no longer exists"));
            // Deleted in the engine: the lead line is detached where the part was – nothing is there any more.
            var removed = Edits.Remove(doc, new[] { parts[1].Id });
            Assert.Contains(Fx.Auto(r, removed), f => f.RuleId == "PF-LEAD-TARGET" && f.ObjectId == numeral.Id);
        }

        [Fact]
        public void ReportsDuplicateNumeralsForDifferentParts()
        {
            var r = Parts.Reg();
            var doc = Numbered(r, out var parts);
            var gearNumeral = doc.Objects.Values.First(o => o.Type == PatentIds.Numeral && ReferenceNumerals.TargetOf(doc, o)?.Id == parts[1].Id);
            // The gear's numeral is changed to the shaft's numeral: one sign, two different parts.
            doc = Edits.Replace(doc, ReferenceNumerals.SetNumeral(gearNumeral, "10"));
            var issue = ReferenceNumerals.Check(doc).Single(i => i.Kind == ReferenceIssueKind.SameNumeralDifferentParts);
            Assert.Equal("10", issue.Numeral);
            Assert.Contains(parts[0].Id, issue.ObjectIds);
            Assert.Contains(parts[1].Id, issue.ObjectIds);
            var f = Fx.Auto(r, doc).Single(x => x.RuleId == "PF-REF-DUPLICATE");
            Assert.Equal(Severity.Error, f.Severity);
            Assert.False(string.IsNullOrEmpty(f.Message.De));
        }

        [Fact]
        public void ReportsPartsWithoutNumeralsAsDraftingAid()
        {
            var r = Parts.Reg();
            var doc = Numbered(r, out _);
            var spring = Parts.Block(r, 100, 240, "spring");
            doc = Edits.Add(doc, doc.Pages[0].Id, spring);
            foreach (var profile in PatentProfiles.All)
            {
                var f = Fx.Auto(r, doc, profile).Single(x => x.RuleId == "PF-PART-UNNUMBERED");
                Assert.Equal((Severity.Info, spring.Id), (f.Severity, f.ObjectId));
                Assert.Equal(PreflightRule.DraftingAidSource, f.Source);
                Assert.Contains("no reference numeral", f.Message.En);
            }
            // A numeral with an arrow names the whole assembly it points at.
            var p = doc.Pages[0].Id;
            var a = Parts.Block(r, 40, 270, "pin") with { GroupId = "g1" };
            var b = Parts.Block(r, 70, 270, "nut") with { GroupId = "g1" };
            doc = Edits.Add(doc, p, a, b);
            doc = ReferenceNumerals.Insert(r, doc, p, a.BoxPoint(0, 0), new Vec2(30, 255), "pin assembly", a.Id, "60").Doc;
            var arrow = doc.Objects.Values.Single(o => o.Ref == "60" && o.Type == PatentIds.Numeral);
            Assert.Contains(Fx.Auto(r, doc), x => x.RuleId == "PF-PART-UNNUMBERED" && x.ObjectId == b.Id);
            doc = Edits.Replace(doc, arrow.WithParam("end", "arrow"));
            Assert.DoesNotContain(Fx.Auto(r, doc), x => x.RuleId == "PF-PART-UNNUMBERED" && x.ObjectId == b.Id);
        }
    }

    public class ExplodedViewAndShadingTests
    {
        [Fact]
        public void AssemblyAxisJoinsFacingEdgesAndFollowsParts()
        {
            var r = Parts.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            var shaft = Parts.Block(r, 50, 100, "shaft", 30, 10);
            var hub = Parts.Block(r, 120, 100, "hub", 20, 20);
            var far = Parts.Block(r, 180, 220, "cover");
            doc = Edits.Add(doc, p, shaft, hub, far);
            doc = PartNumbering.NumberParts(r, doc, p).Doc;
            var numeral = doc.Objects.Values.First(o => o.Type == PatentIds.Numeral && ReferenceNumerals.TargetOf(doc, o)?.Id == shaft.Id);

            var action = r.Actions(numeral, new RenderContext(doc)).Single(a => a.Id == ExplodedViews.AxisActionId);
            Assert.True(action.IsEnabled);
            doc = ContextActions.Run(r, doc, new[] { numeral.Id }, ExplodedViews.AxisActionId, null, Lang.En);
            var axis = doc.Objects.Values.Single(o => o.Type == PatentIds.AssemblyAxis);
            var pts = doc.ResolvePoints(axis);
            Assert.Equal(65, pts[0].X, 6);  // right edge of the shaft
            Assert.Equal(110, pts[1].X, 6); // left edge of the hub (nearest part, not the far cover)
            Assert.Equal(100, pts[0].Y, 6);
            Assert.True(ExplodedViews.Joined(doc, shaft.Id, hub.Id));

            var line = r.Render(axis, new RenderContext(doc)).OfType<LinePrim>().Single();
            Assert.Equal(DashStyle.Dash, line.Style.Dash);
            Assert.Equal(LineRole.Thin, line.Style.Role);
            Assert.Equal(LineRole.Center, r.Render(axis.WithParam("form", "chain"), new RenderContext(doc)).OfType<LinePrim>().Single().Style.Role);

            // Pulling the hub further out keeps the axis on it.
            doc = Edits.Move(doc, new[] { hub.Id }, new Vec2(15, 0));
            Assert.Equal(125, doc.ResolvePoints(doc.Get(axis.Id)!)[1].X, 6);
            // A second run joins the next nearest part.
            doc = ContextActions.Run(r, doc, new[] { numeral.Id }, ExplodedViews.AxisActionId, null, Lang.En);
            Assert.True(ExplodedViews.Joined(doc, shaft.Id, far.Id));
            Assert.DoesNotContain(Fx.Auto(r, doc), f => f.ObjectId is not null && doc.Get(f.ObjectId)?.Type == PatentIds.AssemblyAxis);
        }

        [Theory]
        [InlineData("cylinder")]
        [InlineData("cone")]
        [InlineData("sphere")]
        [InlineData("flat")]
        public void ShadingDrawsThinSpacedLines(string form)
        {
            var r = Parts.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var o = r.Create(r.Item(PatentIds.Shading + "." + form)!, new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var prims = r.Render(o, new RenderContext(doc));
            Assert.Equal(5, prims.Count);
            Assert.All(prims, p => Assert.Equal(LineRole.Thin, (p switch { LinePrim l => l.Style, PathPrim a => a.Style, _ => null })!.Role));
            Assert.Equal(8, r.Render(o.WithParam("lines", 8.0), new RenderContext(doc)).Count);
            // Lines stay inside the box and gather on the shaded (right) side for curved surfaces.
            var b = Prim.Bounds(prims)!.Value;
            Assert.True(o.Bounds().Inflate(0.01).Contains(b));
            if (form is "cylinder" or "cone") Assert.True(prims.OfType<LinePrim>().Count(l => Math.Max(l.A.X, l.B.X) > 100) >= 4);
            Assert.False(PartsList.IsPart(r, o));
            Assert.Contains(r.Actions(o, new RenderContext(doc)), a => a.Id == "patent.shading.form=sphere");
        }

        /// <summary>Gallery page: an exploded row of parts numbered automatically, joined by assembly axes, with shading.</summary>
        [Fact]
        public void WriteNumberingGallery()
        {
            var r = Parts.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            var row = new[] { Parts.Block(r, 45, 110, "shaft", 30, 10), Parts.Block(r, 90, 110, "hub", 16, 24), Parts.Block(r, 130, 110, "washer", 4, 20), Parts.Block(r, 165, 110, "nut", 12, 16) };
            var plate = Parts.Block(r, 105, 200, "plate", 90, 40);
            doc = Edits.Add(doc, p, row.Append(plate).ToArray());
            doc = Edits.Add(doc, p, r.Create(r.Item(PatentIds.Shading + ".cylinder")!, new Vec2(45, 110)) with { Size = new Size2(30, 10), Transform = new Transform(45, 110, 90) },
                r.Create(r.Item(PatentIds.Shading + ".sphere")!, new Vec2(105, 200)) with { Size = new Size2(40, 40) });
            doc = PartNumbering.NumberParts(r, doc, p, new NumberingOptions { Repeated = RepeatedParts.Suffixed }).Doc;
            for (var i = 0; i + 1 < row.Length; i++) doc = ExplodedViews.AddAxis(r, doc, p, row[i].Id, row[i + 1].Id).Doc;
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "gallery");
            System.IO.Directory.CreateDirectory(dir);
            var html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Numbering gallery</title><style>svg{border:1px solid #999;width:560px;height:792px}</style></head><body>"
                + string.Concat(doc.Pages.Select(pg => SvgRenderer.RenderPage(r, doc, pg))) + "</body></html>";
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "figures-numbering-gallery.html"), html);
            Assert.Equal(5, doc.Objects.Values.Count(o => o.Type == PatentIds.Numeral));
            Assert.Equal(3, doc.Objects.Values.Count(o => o.Type == PatentIds.AssemblyAxis));
        }

        [Fact]
        public void NewTypesRoundTripThroughJson()
        {
            var r = Parts.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var p = doc.Pages[0].Id;
            var a = Parts.Block(r, 50, 100, "a");
            var b = Parts.Block(r, 120, 100, "b");
            doc = Edits.Add(doc, p, a, b, r.Create(r.Item(PatentIds.Shading + ".cone")!, new Vec2(50, 200)).WithParam("lines", 7.0));
            doc = ExplodedViews.AddAxis(r, doc, p, a.Id, b.Id, "chain").Doc;
            doc = PartNumbering.NumberParts(r, doc, p, new NumberingOptions { Repeated = RepeatedParts.Suffixed }).Doc;
            doc = ReferenceNumerals.SetSuffix(doc, doc.Objects.Values.First(o => o.Type == PatentIds.Numeral).Id, "'");
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(ReferenceNumerals.Export(doc), ReferenceNumerals.Export(back));
            Assert.Equal(PatentExport.AllPagesSvg(r, doc), PatentExport.AllPagesSvg(r, back));
        }
    }
}
