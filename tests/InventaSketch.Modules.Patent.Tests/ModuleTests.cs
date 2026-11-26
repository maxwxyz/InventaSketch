using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;
using Xunit;

namespace InventaSketch.Modules.Patent.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new AnnotationModule()).Register(new PatentModule());

        public static SketchObject Box(Registry r, string item, double x, double y, double w, double h, string? pattern = null)
        {
            var o = r.Create(r.Item(item)!, new Vec2(x, y)) with { Size = new Size2(w, h) };
            return pattern is null ? o : o.WithParam("pattern", pattern);
        }

        public static SketchObject Fig(Registry r, string label, double x, double y) => r.Create(r.Item("ann.figlabel")!, new Vec2(x, y)).WithParam("label", label);

        /// <summary>A two-sheet A4 document that passes every automatic check of every profile.</summary>
        public static SketchDocument Clean(Registry r, out string housingId)
        {
            var profile = PatentProfiles.CommonSafe;
            var doc = SketchDocument.Create(PageSizes.A4Portrait, "Clean");
            doc = doc with { Pages = new[] { doc.Pages[0], new Page { Id = "p2", Name = "Page 2", Size = PageSizes.A4Portrait } } };
            doc = PatentExport.ApplyProfile(doc, profile);
            var p1 = doc.Pages[0].Id; var p2 = doc.Pages[1].Id;

            var housing = Box(r, "ann.hatch.rect", 105, 140, 90, 50);
            var shaft = Box(r, "ann.hatch.ellipse", 105, 215, 40, 26, "cross");
            doc = Edits.Add(doc, p1, housing, shaft, Fig(r, "FIG. 1", 105, 268));
            housingId = housing.Id;
            doc = ReferenceNumerals.Insert(r, doc, p1, housing.BoxPoint(-0.3, -0.5), new Vec2(50, 95), "housing", housing.Id).Doc;
            doc = ReferenceNumerals.Insert(r, doc, p1, shaft.BoxPoint(0.45, 0), new Vec2(160, 240), "shaft", shaft.Id).Doc;

            var cover = Box(r, "ann.hatch.rect", 105, 150, 100, 60) with { Transform = new Transform(105, 150, 8) };
            var screw = Box(r, "ann.hatch.rect", 105, 220, 12, 30, "reverse");
            doc = Edits.Add(doc, p2, cover, screw, Fig(r, "FIG. 2", 105, 268));
            var (d3, n30) = ReferenceNumerals.Insert(r, doc, p2, new Vec2(100, 150), new Vec2(45, 80), "cover", cover.Id);
            doc = d3;
            var sub = ReferenceNumerals.NextFree(doc, null, n30.Ref);
            doc = ReferenceNumerals.Insert(r, doc, p2, screw.BoxPoint(0.5, 0.2), new Vec2(150, 245), "cover screw", screw.Id, sub).Doc;

            doc = PatentExport.ApplySheetNumbers(r, doc, profile);
            doc = PatentExport.AddSheetFrame(r, doc, p1, profile);
            doc = PatentExport.AddSheetFrame(r, doc, p2, profile);
            // A coloured note on the helper layer must be ignored by preflight and export.
            var note = r.Create(r.Item("ann.callout")!, new Vec2(10, 10)) with { Layer = PatentExport.HelperLayerId, Style = new Style { Stroke = "#ff0000" }, Text = "draft note with many words in it" };
            return Edits.Add(doc, p1, note);
        }

        public static IReadOnlyList<Finding> Auto(Registry r, SketchDocument doc, PatentProfile? profile = null)
            => Preflight.Run(r, doc, profile ?? PatentProfiles.CommonSafe).Where(f => f.Severity != Severity.Manual).ToList();

        public static string Dump(IEnumerable<Finding> f) => string.Join("\n", f.Select(x => $"{x.RuleId} {x.Severity} {x.Message.En}"));
    }

    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModulesRegisterAndItemsRender()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => i.TypeId.StartsWith("patent.", StringComparison.Ordinal)))
            {
                var doc = SketchDocument.Create(PageSizes.A4Portrait);
                var o = r.Create(item, new Vec2(100, 100));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                Assert.NotEmpty(r.Render(o, new RenderContext(doc)));
                Assert.False(string.IsNullOrEmpty(item.Name.De));
            }
        }

        [Fact]
        public void ProfilesFollowResearchValues()
        {
            Assert.Equal(new[] { "common-safe", "dpma", "epo", "pct", "uspto" }, PatentProfiles.All.Select(p => p.Id));
            foreach (var p in PatentProfiles.All)
            {
                Assert.Equal(new Margins(25, 25, 15, 10), p.Margins);
                Assert.Equal(3.2, p.MinCharHeight);
                var u = p.Usable(PageSizes.A4Portrait);
                Assert.Equal(170, u.W, 9);
                Assert.Equal(262, u.H, 9);
            }
            var letter = PatentProfiles.Uspto.Usable(PageSizes.UsLetterPortrait);
            Assert.InRange(letter.W, 175, 176.01); // sight ≤ 17.6 × 24.4 cm
            Assert.InRange(letter.H, 244, 244.5);
            Assert.Equal(ColourPolicy.AllowedWithConditions, PatentProfiles.Epo.Colour);
            Assert.Equal(ColourPolicy.BlackAndWhite, PatentProfiles.Pct.Colour);
            Assert.False(PatentProfiles.Uspto.CentreLinesAllowed);
            Assert.False(PatentProfiles.Dpma.ForbidEnclosedNumerals);
            Assert.Contains(PatentProfiles.Uspto.Sheets, s => s.Id == "letter");
            Assert.DoesNotContain(PatentProfiles.CommonSafe.Sheets, s => s.Id == "letter");
        }

        [Fact]
        public void EveryRuleCitesSourcesWithUrls()
        {
            foreach (var rule in PreflightRules.All)
            {
                Assert.Matches(@"^PF-[A-Z-]+$", rule.Id);
                Assert.False(string.IsNullOrEmpty(rule.Title.De));
                if (rule.IsDraftingAid)
                {
                    // Drafting aids claim no office rule and no research row.
                    Assert.Empty(rule.Research);
                    Assert.Equal(PreflightRule.DraftingAidSource, rule.SourceText(Office.Epo));
                    continue;
                }
                Assert.Matches(@"^P\d+$", rule.Research);
                Assert.NotEmpty(rule.Sources);
                Assert.All(rule.Sources.Values.SelectMany(c => c), c => Assert.StartsWith("https://", c.Url));
                Assert.False(string.IsNullOrEmpty(rule.Title.De));
            }
        }
    }

    public class PreflightTests
    {
        [Fact]
        public void CleanDocumentHasNoAutomaticFindingsInAnyProfile()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out _);
            foreach (var profile in PatentProfiles.All)
            {
                var all = Preflight.Run(r, doc, profile);
                var auto = all.Where(f => f.Severity != Severity.Manual).ToList();
                Assert.True(auto.Count == 0, profile.Id + ":\n" + Fx.Dump(auto));
                Assert.Contains(all, f => f.Severity == Severity.Manual && f.RuleId == "PF-MANUAL-LINES");
                Assert.All(all, f => Assert.Contains("https://", f.Source));
            }
            Assert.Empty(Preflight.Run(r, doc, PatentProfiles.Pct, new PreflightOptions { IncludeManual = false }));
            Assert.Contains("not legal advice", Preflight.Disclaimer.En);
            Assert.Contains("keine Rechtsberatung", Preflight.Disclaimer.De);
        }

        private static SketchDocument Mutate(Registry r, string kind, out string housing)
        {
            var doc = Fx.Clean(r, out housing);
            var h = housing;
            var p1 = doc.Pages[0].Id; var p2 = doc.Pages[1].Id;
            SketchObject Text(string t, double x = 105, double y = 60) => r.Create(r.Item("ann.text")!, new Vec2(x, y)) with { Text = t, Size = new Size2(60, 10) };
            SketchObject Find(Func<SketchObject, bool> f) => doc.Objects.Values.First(f);
            switch (kind)
            {
                case "letter": return doc with { Pages = doc.Pages.Select(p => p with { Size = PageSizes.UsLetterPortrait }).ToList() };
                case "landscape": return doc with { Pages = doc.Pages.Select(p => p with { Size = PageSizes.A4Landscape }).ToList() };
                case "margin": return Edits.Move(doc, new[] { h }, new Vec2(-55, 0));
                case "frame": return Edits.Add(doc, p1, Fx.Box(r, "ann.hatch.rect", 110, 156, 170, 262) with { Params = new Dictionary<string, ParamValue> { ["outline"] = true, ["pattern"] = "wide" } });
                case "colour": return Edits.Update(doc, new[] { h }, o => o with { Style = new Style { Stroke = "#cc0000" } });
                case "grey": return Edits.Update(doc, new[] { h }, o => o with { Style = new Style { Stroke = "#808080" } });
                case "solid": return Edits.Add(doc, p1, Fx.Box(r, "ann.label", 105, 60, 36, 12) with { Style = new Style { Fill = Fill.Solid("#000000") } });
                case "textheight": return Edits.Update(doc, new[] { Find(o => o.Type == PatentIds.Numeral).Id }, o => o.WithParam("textSize", 3.0));
                case "thinline": return Edits.Add(doc, p1, r.Create(r.Item("ann.line")!, new Vec2(105, 60)) with { Style = new Style { StrokeWidth = 0.1 } });
                case "balloon": return Edits.Add(doc, p1, r.Create(r.Item("ann.balloon")!, new Vec2(170, 60)));
                case "brackets": return Edits.Add(doc, p1, r.Create(r.Item("ann.leader")!, new Vec2(150, 60)) with { Text = "(10)" });
                case "bracketref": return Edits.Update(doc, new[] { Find(o => o.Type == PatentIds.Numeral).Id }, o => o with { Ref = "(10)" });
                case "crossing":
                    {
                        var a = r.Create(r.Item("patent.numeral.straight")!, new Vec2(0, 0)) with { Points = new[] { new Vec2(60, 60), new Vec2(120, 80) }, Ref = "10" };
                        var b = r.Create(r.Item("patent.numeral.straight")!, new Vec2(0, 0)) with { Points = new[] { new Vec2(60, 80), new Vec2(120, 60) }, Ref = "20" };
                        return Edits.Add(doc, p1, a, b);
                    }
                case "dangling": return Edits.Update(doc, new[] { Find(o => o.Type == PatentIds.Numeral).Id }, o => o with { Attach = null });
                case "figmissing": return Edits.Remove(doc, new[] { doc.ObjectsOn(doc.Pages[1]).First(o => o.Type == "ann.figlabel").Id });
                case "figdup": return Edits.Update(doc, new[] { doc.ObjectsOn(doc.Pages[1]).First(o => o.Type == "ann.figlabel").Id }, o => o.WithParam("label", "FIG. 1"));
                case "figseq": return Edits.Update(doc, new[] { doc.ObjectsOn(doc.Pages[1]).First(o => o.Type == "ann.figlabel").Id }, o => o.WithParam("label", "FIG. 3"));
                case "figsingle": return doc with { Pages = new[] { doc.Pages[0] } };
                case "figsize": return Edits.Update(doc, new[] { Find(o => o.Type == "ann.figlabel").Id }, o => o.WithParam("textSize", 4.6));
                case "nosheetnumber": return Edits.Remove(doc, new[] { doc.ObjectsOn(doc.Pages[1]).First(o => o.Type == PatentIds.PageNumber).Id });
                case "sheetnumberwrong": return Edits.Update(doc, new[] { doc.ObjectsOn(doc.Pages[1]).First(o => o.Type == PatentIds.PageNumber).Id }, o => o.WithParam("auto", false) with { Text = "Sheet 2" });
                case "words": return Edits.Add(doc, p1, Text("Housing made of die-cast aluminium alloy"));
                case "scale": return Edits.Add(doc, p1, Text("M 1:2"));
                case "centreline": return Edits.Add(doc, p1, r.Create(r.Item("ann.centerline")!, new Vec2(105, 140)));
                case "refconflict": return ReferenceNumerals.Insert(r, doc, p2, new Vec2(120, 200), new Vec2(170, 200), "lid", null, "10").Doc;
                case "reflabel": return ReferenceNumerals.Insert(r, doc, p2, new Vec2(120, 200), new Vec2(170, 200), "housing", null, "40").Doc;
                case "refmissing": return ReferenceNumerals.Insert(r, doc, p2, new Vec2(120, 200), new Vec2(170, 200), null, null, "50").Doc;
                case "refunused": return Edits.SetReference(doc, "99", "spring");
                default: throw new ArgumentException(kind);
            }
        }

        [Theory]
        [InlineData("letter", "PF-PAGE-SIZE", Severity.Error)]
        [InlineData("landscape", "PF-PAGE-ORIENT", Severity.Error)]
        [InlineData("margin", "PF-MARGIN", Severity.Error)]
        [InlineData("frame", "PF-FRAME", Severity.Error)]
        [InlineData("colour", "PF-COLOUR", Severity.Error)]
        [InlineData("grey", "PF-GREY", Severity.Error)]
        [InlineData("solid", "PF-SOLID-BLACK", Severity.Warning)]
        [InlineData("textheight", "PF-TEXT-HEIGHT", Severity.Error)]
        [InlineData("thinline", "PF-LINE-WIDTH", Severity.Warning)]
        [InlineData("balloon", "PF-NUMERAL-ENCLOSED", Severity.Error)]
        [InlineData("brackets", "PF-NUMERAL-ENCLOSED", Severity.Error)]
        [InlineData("crossing", "PF-LEAD-CROSS", Severity.Error)]
        [InlineData("dangling", "PF-LEAD-FREE", Severity.Info)]
        [InlineData("figmissing", "PF-FIG-MISSING", Severity.Error)]
        [InlineData("figdup", "PF-FIG-DUP", Severity.Error)]
        [InlineData("figseq", "PF-FIG-SEQ", Severity.Error)]
        [InlineData("figsingle", "PF-FIG-SINGLE", Severity.Info)]
        [InlineData("figsize", "PF-FIG-SIZE", Severity.Warning)]
        [InlineData("nosheetnumber", "PF-SHEET-NUMBER", Severity.Error)]
        [InlineData("sheetnumberwrong", "PF-SHEET-NUMBER", Severity.Error)]
        [InlineData("words", "PF-TEXT-WORDS", Severity.Warning)]
        [InlineData("scale", "PF-SCALE-TEXT", Severity.Error)]
        [InlineData("centreline", "PF-CENTRELINE", Severity.Warning)]
        [InlineData("refconflict", "PF-REF-CONFLICT", Severity.Error)]
        [InlineData("reflabel", "PF-REF-LABEL", Severity.Warning)]
        [InlineData("refmissing", "PF-REF-MISSING", Severity.Error)]
        [InlineData("refunused", "PF-REF-UNUSED", Severity.Warning)]
        public void PreflightFindsViolation(string kind, string ruleId, Severity severity)
        {
            var r = Fx.Reg();
            var doc = Mutate(r, kind, out _);
            var found = Fx.Auto(r, doc);
            Assert.True(found.Any(f => f.RuleId == ruleId && f.Severity == severity), kind + " expected " + ruleId + "/" + severity + ", got:\n" + Fx.Dump(found));
            var f = found.First(x => x.RuleId == ruleId);
            Assert.False(string.IsNullOrEmpty(f.Message.De));
            Assert.Contains("https://", f.Source);
        }

        [Fact]
        public void ProfileSpecificSeverities()
        {
            var r = Fx.Reg();
            Assert.DoesNotContain(Fx.Auto(r, Mutate(r, "letter", out _), PatentProfiles.Uspto), f => f.RuleId == "PF-PAGE-SIZE");
            Assert.Contains(Fx.Auto(r, Mutate(r, "colour", out _), PatentProfiles.Epo), f => f.RuleId == "PF-COLOUR" && f.Severity == Severity.Info);
            Assert.DoesNotContain(Fx.Auto(r, Mutate(r, "centreline", out _), PatentProfiles.Epo), f => f.RuleId == "PF-CENTRELINE");
            Assert.Contains(Fx.Auto(r, Mutate(r, "balloon", out _), PatentProfiles.Dpma), f => f.RuleId == "PF-NUMERAL-ENCLOSED" && f.Severity == Severity.Info);
            Assert.Contains(Fx.Auto(r, Mutate(r, "crossing", out _), PatentProfiles.Epo), f => f.RuleId == "PF-LEAD-CROSS" && f.Severity == Severity.Warning);
            Assert.DoesNotContain(Fx.Auto(r, Mutate(r, "nosheetnumber", out _), PatentProfiles.Dpma), f => f.RuleId == "PF-SHEET-NUMBER");
            Assert.Contains(Fx.Auto(r, Mutate(r, "nosheetnumber", out _), PatentProfiles.Uspto), f => f.RuleId == "PF-SHEET-NUMBER" && f.Severity == Severity.Warning);
            Assert.DoesNotContain(Fx.Auto(r, Mutate(r, "figsingle", out _), PatentProfiles.Dpma), f => f.RuleId == "PF-FIG-SINGLE");
            // "(10)" is drawn without brackets under forbidding profiles, so the drawing itself is clean there.
            var brackets = Mutate(r, "bracketref", out _);
            Assert.DoesNotContain(Fx.Auto(r, brackets, PatentProfiles.Pct), f => f.RuleId == "PF-NUMERAL-ENCLOSED");
            Assert.DoesNotContain(Fx.Auto(r, Mutate(r, "frame", out _), PatentProfiles.Dpma), f => f.RuleId == "PF-FRAME"); // no DPMA rule
            var numeral = brackets.Objects.Values.First(o => o.Ref == "(10)");
            var text = r.Render(numeral, new RenderContext(brackets with { Settings = brackets.Settings with { Profile = "pct" } })).OfType<TextPrim>().Single();
            Assert.Equal("10", text.Text);
            var dpmaText = r.Render(numeral, new RenderContext(brackets with { Settings = brackets.Settings with { Profile = "dpma" } })).OfType<TextPrim>().Single();
            Assert.Equal("(10)", dpmaText.Text);
        }

        [Fact]
        public void HelperObjectsAreExcluded()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out _);
            Assert.Contains(doc.Objects.Values, o => o.Type == PatentIds.Sheet);
            Assert.Contains(doc.Objects.Values, o => o.Layer == PatentExport.HelperLayerId && o.Type == "ann.callout");
            Assert.DoesNotContain(Fx.Auto(r, doc), f => f.ObjectId is not null && doc.Get(f.ObjectId)!.Layer == PatentExport.HelperLayerId);
        }
    }

    public class ReferenceNumeralTests
    {
        private static SketchDocument WithNumerals(Registry r, params (string Numeral, string? Label)[] items)
        {
            var doc = SketchDocument.Create(PageSizes.A4Portrait);
            var y = 50.0;
            foreach (var (n, l) in items) { doc = ReferenceNumerals.Insert(r, doc, doc.Pages[0].Id, new Vec2(100, y), new Vec2(60, y), l, null, n).Doc; y += 15; }
            return doc;
        }

        [Fact]
        public void NextFreeFollowsScheme()
        {
            var r = Fx.Reg();
            var empty = SketchDocument.Create();
            Assert.Equal("10", ReferenceNumerals.NextFree(empty));
            var doc = WithNumerals(r, ("10", "housing"), ("11", "lid"), ("30", "shaft"));
            Assert.Equal("20", ReferenceNumerals.NextFree(doc));
            Assert.Equal("12", ReferenceNumerals.NextFree(doc, null, "10"));
            Assert.Equal("31", ReferenceNumerals.NextFree(doc, null, "30"));
            Assert.Equal("100", ReferenceNumerals.NextFree(empty, new NumeralScheme(100, 100)));
            Assert.Equal("102", ReferenceNumerals.NextFree(WithNumerals(r, ("100", "a")), new NumeralScheme(100, 100, 2), "100"));
            Assert.Throws<InvalidOperationException>(() => ReferenceNumerals.NextFree(WithNumerals(r, ("10", "a"), ("11", "b")), new NumeralScheme(10, 2), "10"));
        }

        [Fact]
        public void RenameIsConsistentAcrossPages()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out _);
            var before = doc.Objects.Values.Count(o => o.Ref == "10");
            doc = ReferenceNumerals.Rename(doc, "10", "15");
            Assert.Equal(0, doc.Objects.Values.Count(o => o.Ref == "10"));
            Assert.Equal(before, doc.Objects.Values.Count(o => o.Ref == "15"));
            Assert.Equal("housing", doc.References["15"].Label);
            Assert.False(doc.References.ContainsKey("10"));
            Assert.Throws<InvalidOperationException>(() => ReferenceNumerals.Rename(doc, "15", "20"));
            Assert.Empty(Fx.Auto(r, doc));
        }

        [Fact]
        public void RenumberFollowsOrderOfAppearanceAndKeepsSubNumerals()
        {
            var r = Fx.Reg();
            var doc = WithNumerals(r, ("30", "shaft"), ("10", "housing"), ("11", "lid"), ("12'", "lid variant"));
            var (renumbered, map) = ReferenceNumerals.Renumber(doc);
            Assert.Equal("10", map["30"]);
            Assert.Equal("20", map["10"]);
            Assert.Equal("21", map["11"]);
            Assert.Equal("22'", map["12'"]);
            Assert.Equal("shaft", renumbered.References["10"].Label);
            Assert.Equal("lid variant", renumbered.References["22'"].Label);
            Assert.Equal(new[] { "10", "20", "21", "22'" }, ReferenceNumerals.Uses(renumbered).Select(u => u.Numeral));
        }

        [Fact]
        public void ExportListAsTextAndCsv()
        {
            var r = Fx.Reg();
            var doc = WithNumerals(r, ("20", "shaft, hollow"), ("10", "housing"), ("11", "lid"));
            Assert.Equal("10 housing\n11 lid\n20 shaft, hollow\n", ReferenceNumerals.Export(doc));
            Assert.Equal("numeral,label\r\n10,housing\r\n11,lid\r\n20,\"shaft, hollow\"\r\n", ReferenceNumerals.Export(doc, ReferenceListFormat.Csv));
        }

        [Fact]
        public void ConsistencyChecks()
        {
            var r = Fx.Reg();
            var doc = WithNumerals(r, ("10", "housing"), ("10", "lid"), ("20", "Housing"), ("30", null));
            doc = Edits.SetReference(doc, "90", "unused part");
            var issues = ReferenceNumerals.Check(doc);
            Assert.Contains(issues, i => i.Kind == ReferenceIssueKind.SameNumeralDifferentLabels && i.Numeral == "10");
            Assert.Contains(issues, i => i.Kind == ReferenceIssueKind.LabelWithDifferentNumerals && i.Numeral == "10, 20");
            Assert.Contains(issues, i => i.Kind == ReferenceIssueKind.NumeralWithoutEntry && i.Numeral == "30");
            Assert.Contains(issues, i => i.Kind == ReferenceIssueKind.EntryUnused && i.Numeral == "90");
            var synced = ReferenceNumerals.Sync(doc, removeUnused: true);
            Assert.True(synced.References.ContainsKey("30"));
            Assert.False(synced.References.ContainsKey("90"));
        }

        [Fact]
        public void NumeralFollowsPartAndNeverDrawsEnclosures()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out var housing);
            var numeral = doc.Objects.Values.First(o => o.Type == PatentIds.Numeral && o.Attach?[0]?.ObjectId == housing);
            var before = doc.ResolvePoints(numeral)[0];
            doc = Edits.Move(doc, new[] { housing }, new Vec2(4, 6));
            Assert.Equal(before + new Vec2(4, 6), doc.ResolvePoints(doc.Get(numeral.Id)!)[0]);
            var prims = r.Render(doc.Get(numeral.Id)!, new RenderContext(doc));
            Assert.DoesNotContain(prims, p => p is EllipsePrim || p is RectPrim);
            Assert.Equal("10", prims.OfType<TextPrim>().Single().Text);
            Assert.Equal("12'", NumeralType.StripEnclosure("12'"));
            Assert.Equal("12", NumeralType.StripEnclosure("“12”"));
            Assert.True(NumeralType.IsEnclosed("[14]"));
            Assert.False(NumeralType.IsEnclosed("14'"));
            Assert.False(NumeralType.IsEnclosed("(open)"));
        }
    }

    public class ExportTests
    {
        [Fact]
        public void PageSvgIsMonochromeWithoutHelpers()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out var housing);
            doc = Edits.Update(doc, new[] { housing }, o => o with { Style = new Style { Stroke = "#cc0000" } });
            var svgs = PatentExport.AllPagesSvg(r, doc);
            Assert.Equal(2, svgs.Count);
            foreach (var svg in svgs)
            {
                Assert.DoesNotContain(SheetType.HelperColour, svg);
                Assert.DoesNotContain("#cc0000", svg);
                Assert.DoesNotContain("#ff0000", svg);
                Assert.DoesNotContain("draft note", svg);
                Assert.DoesNotContain("data-isk-id", svg);
                Assert.Contains("viewBox=\"0 0 210 297\"", svg);
            }
            Assert.Contains(">1/2<", svgs[0]);
            Assert.Contains(">2/2<", svgs[1]);
            Assert.Contains(">FIG. 2<", svgs[1]);
        }

        [Fact]
        public void NumberFiguresInReadingOrder()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out _);
            var p1 = doc.Pages[0].Id;
            doc = Edits.Add(doc, p1, Fx.Fig(r, "x", 60, 120)); // above the existing FIG. 1 → becomes FIG. 1
            doc = PatentExport.NumberFigures(doc, PatentProfiles.Pct);
            var labels = doc.Pages.SelectMany(p => doc.ObjectsOn(p)).Where(o => o.Type == "ann.figlabel").OrderBy(o => doc.Pages.ToList().FindIndex(p => p.Order.Contains(o.Id))).ThenBy(o => o.Transform.Y)
                .Select(FigLabelType.LabelOf).ToList();
            Assert.Equal(new[] { "Fig. 1", "Fig. 2", "Fig. 3" }, labels);
            Assert.Equal("Fig. 3", doc.Pages[1].Figure);

            var single = Fx.Clean(r, out _) with { };
            single = single with { Pages = new[] { single.Pages[0] } };
            single = PatentExport.NumberFigures(single, PatentProfiles.Uspto, removeSingleLabel: true);
            Assert.DoesNotContain(single.ObjectsOn(single.Pages[0]), o => o.Type == "ann.figlabel");
        }

        [Fact]
        public void JsonRoundTripKeepsFigureData()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out _);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc, indented: true));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal("common-safe", back.Settings.Profile);
            Assert.Equal(ReferenceNumerals.Export(doc), ReferenceNumerals.Export(back));
            Assert.Equal(PatentExport.AllPagesSvg(r, doc), PatentExport.AllPagesSvg(r, back));
            Assert.Empty(Fx.Auto(r, back));
        }

        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var doc = Fx.Clean(r, out _);
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery");
            Directory.CreateDirectory(dir);
            var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>Figures gallery</title><style>body{margin:10px;font-family:Arial}svg{border:1px solid #999;margin:4px;width:560px;height:792px}</style></head><body>");
            html.Append("<div>Editor view (with sheet helper) and filing export (monochrome, helpers removed)</div>");
            foreach (var page in doc.Pages) html.Append(SvgRenderer.RenderPage(r, doc, page));
            foreach (var svg in PatentExport.AllPagesSvg(r, doc)) html.Append(svg);
            html.Append("</body></html>");
            File.WriteAllText(Path.Combine(dir, "figures-gallery.html"), html.ToString());
            var findings = Preflight.Run(r, doc, PatentProfiles.CommonSafe);
            File.WriteAllText(Path.Combine(dir, "figures-preflight.txt"), Fx.Dump(findings) + "\n\n" + ReferenceNumerals.Export(doc));
        }
    }
}
