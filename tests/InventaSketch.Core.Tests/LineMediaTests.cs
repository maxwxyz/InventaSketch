using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Stub lines and elements for colour by medium and designations.</summary>
    internal sealed class MediaModule : IModule
    {
        public string Id => "media";
        public I18nText Name => new("Media test");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new PipeType(), new CoreType(), new PumpType(), new NoteType(), new LabelledLineType() };
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();

        internal abstract class LineBase : ObjectType
        {
            public override GeometryKind Geometry => GeometryKind.Points;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.Polyline(ctx.Doc.ResolvePoints(o), new Style { Stroke = "#333333", StrokeWidth = 0.5 }) with { Tag = "line" } };
        }

        /// <summary>A pipe whose medium is its parameter "m" (a <see cref="PortMedia"/> path).</summary>
        internal sealed class PipeType : LineBase
        {
            public override string Id => "media.pipe";
            public override I18nText Name => new("Pipe");
            public override string? LineMedium(SketchObject line) => line.Str("m", PortMedia.ColdWater);
        }

        /// <summary>A cabinet core drawn in its real colour.</summary>
        internal sealed class CoreType : LineBase
        {
            public override string Id => "media.core";
            public override I18nText Name => new("Core");
            public override string? LineMedium(SketchObject line) => PortMedia.Electric;
            public override string? MediumRole(SketchObject line) => LineMedia.OwnColour;
        }

        /// <summary>A line with its own designation parameter "lineNo" (it draws it itself).</summary>
        internal sealed class LabelledLineType : LineBase
        {
            public override string Id => "media.labelled";
            public override I18nText Name => new("Labelled line");
            public override string? LineMedium(SketchObject line) => PortMedia.Process;
            public override IReadOnlyList<ParamDef> Parameters => new[] { new ParamDef("lineNo", new I18nText("Line number"), ParamKind.Text) };
        }

        /// <summary>A pump with two heating ports and no designation of its own.</summary>
        internal sealed class PumpType : ObjectType
        {
            public override string Id => "media.pump";
            public override I18nText Name => new("Pump");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.Ellipse(0, 0, o.Size.W / 2, o.Size.H / 2) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Point(AnchorRole.Port, new Vec2(-o.Size.W / 2, 0), "in");
                yield return Anchor.Point(AnchorRole.Port, new Vec2(o.Size.W / 2, 0), "out");
            }
            public override PortConnection? Connection(SketchObject o, Anchor port) => PortConnection.Of(PortMedia.Heating);
        }

        /// <summary>An annotation line (no medium) with a "label".</summary>
        internal sealed class NoteType : LineBase
        {
            public override string Id => "media.note";
            public override I18nText Name => new("Note");
            public override IReadOnlyList<ParamDef> Parameters => new[] { new ParamDef("label", new I18nText("Label"), ParamKind.Text) };
        }
    }

    /// <summary>Colour by medium (<see cref="LineMedia"/>) and designations across pages (<see cref="Designations"/>).</summary>
    public class LineMediaTests
    {
        private static readonly Registry Reg = new Registry().Register(new ConnectModule()).Register(new MediaModule());

        private static SketchObject Line(string type, string id, Vec2 a, Vec2 b, params (string Key, ParamValue Value)[] ps)
        {
            var o = new SketchObject { Id = id, Type = type, Points = new[] { a, b }, Attach = new Attachment?[] { null, null } };
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            return o;
        }

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(new Size2(200, 100));
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static IEnumerable<string?> Strokes(IEnumerable<Primitive> prims)
            => Prim.Flatten(prims).OfType<PathPrim>().Select(p => p.Style.Stroke);

        [Fact]
        public void RolesComeFromTheParameterTheTypeAndTheMedium()
        {
            var doc = Doc(
                Line("conn.wire", "w", new(10, 10), new(50, 10)),
                Line("conn.wire", "l1", new(10, 20), new(50, 20), ("lineRole", "L1")),
                Line("media.pipe", "flow", new(10, 30), new(50, 30), ("m", PortMedia.HeatingFlow)),
                Line("media.pipe", "air", new(10, 40), new(50, 40), ("m", "air.extract")),
                Line("media.core", "core", new(10, 50), new(50, 50)),
                Line("media.note", "note", new(10, 60), new(50, 60)));
            Assert.Equal("power", LineMedia.Of(Reg, doc, doc.Get("w")!)!.Role);
            var l1 = LineMedia.Of(Reg, doc, doc.Get("l1")!)!;
            Assert.Equal(("electric", "L1", "#8d5524"), (l1.Medium, l1.Role, l1.Colour!.Color));
            Assert.Equal("IEC 60445", l1.Colour.Source.En);
            Assert.Equal("flow", LineMedia.Of(Reg, doc, doc.Get("flow")!)!.Colour!.Id);
            Assert.Equal("air.extract", LineMedia.Of(Reg, doc, doc.Get("air")!)!.Colour!.Id);
            Assert.Null(LineMedia.Of(Reg, doc, doc.Get("core")!)!.Colour);
            Assert.Null(LineMedia.Of(Reg, doc, doc.Get("note")!));
            // Every scheme entry names its source: a standard where one exists, else the convention.
            Assert.All(LineMedia.Scheme, c => Assert.Contains(c.Source.En, new[] { "IEC 60445", "DIN 2403", "Convention" }));
            Assert.Equal(LineMedia.Scheme.Count, LineMedia.Scheme.Select(c => c.Id).Distinct().Count());
            Assert.Equal("#2e9d3a", LineMedia.Entry("PE")!.Color);
            Assert.Equal("#f2d500", LineMedia.Entry("PE")!.Stripe);
        }

        [Fact]
        public void ColourModeRecoloursAtRenderTimeAndOffRestores()
        {
            var n = Line("conn.wire", "n", new(10, 10), new(50, 10), ("lineRole", "N"));
            var pe = Line("conn.wire", "pe", new(10, 20), new(50, 20), ("lineRole", "PE"));
            var doc = Doc(n, pe);
            var off = Strokes(Reg.Render(doc.Get("n")!, new RenderContext(doc))).ToList();
            Assert.DoesNotContain("#2f8fdd", off);
            var on = doc with { Settings = doc.Settings with { ColorBy = LineMedia.ByMedium } };
            Assert.Equal(new[] { "#2f8fdd" }, Strokes(Reg.Render(on.Get("n")!, new RenderContext(on))).Distinct());
            // Green-yellow: green line plus a dashed yellow copy.
            var stripes = Prim.Flatten(Reg.Render(on.Get("pe")!, new RenderContext(on))).OfType<PathPrim>().ToList();
            Assert.Contains(stripes, p => p.Style.Stroke == "#2e9d3a");
            Assert.Contains(stripes, p => p.Style.Stroke == "#f2d500" && p.Style.Dash == DashStyle.Dash);
            // The stored objects never change; an accent leaves media colours alone.
            Assert.Same(doc.Get("n"), on.Get("n"));
            var accent = on with { Settings = on.Settings with { Accent = "#c62828" } };
            Assert.Equal(new[] { "#2f8fdd" }, Strokes(Reg.Render(accent.Get("n")!, new RenderContext(accent))).Distinct());
            Assert.Equal(new[] { "N", "PE" }, LineMedia.Used(Reg, on).Select(c => c.Id));
        }

        [Fact]
        public void RoleChoiceIsADropdownForElectricLinesOnly()
        {
            var doc = Doc(Line("conn.wire", "w", new(10, 10), new(50, 10)), Line("media.pipe", "p", new(10, 30), new(50, 30)), Line("media.core", "c", new(10, 50), new(50, 50)));
            var wire = Reg.Actions(doc.Get("w")!, new RenderContext(doc, Lang.De)).Where(a => a.Choice?.Key == "line.role").ToList();
            Assert.Equal("Leiter", wire[0].Choice!.Label.De);
            Assert.Contains(wire, a => a.Id == "line.role=PE");
            Assert.True(wire.First(a => a.Id == "line.role=").IsActive);
            Assert.DoesNotContain(Reg.Actions(doc.Get("p")!, new RenderContext(doc)), a => a.Choice?.Key == "line.role");
            Assert.DoesNotContain(Reg.Actions(doc.Get("c")!, new RenderContext(doc)), a => a.Choice?.Key == "line.role");
            var next = ContextActions.Run(Reg, doc, new[] { "w" }, "line.role=L3", null, Lang.En);
            Assert.Equal("L3", next.Get("w")!.Str(LineMedia.RoleParam, ""));
        }

        [Fact]
        public void DesignationLabelEndMarkersAndContinuations()
        {
            // W3 runs to the right page edge (free end at x = 199.5).
            var w = Line("conn.wire", "w", new(20, 50), new(199.5, 50), ("ref", "W3"), ("value", "3×1.5 mm²"));
            var doc = Doc(w);
            var prims = Reg.Render(w, new RenderContext(doc));
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == Designations.LabelTag && t.Text == "W3 · 3×1.5 mm²");
            // At the page edge without a known continuation: "→ W3" at that end only.
            var cont = prims.OfType<TextPrim>().Single(t => t.Tag == Designations.ContinuationTag);
            Assert.Equal("→ W3", cont.Text);
            Assert.True(cont.Position.X > 150 && cont.Position.X < 200 && cont.TextStyle.Anchor == TextAnchor.End);
            // The host passes where else it occurs: "→ W3, slide 4" (German "Folie").
            var ctx = new RenderContext(doc, Lang.De) { Continuations = new Dictionary<string, IReadOnlyList<string>> { ["W3"] = new[] { "2", "4" } }, PageNames = PageNames.Slides };
            Assert.Contains(Reg.Render(w, ctx).OfType<TextPrim>(), t => t.Tag == Designations.ContinuationTag && t.Text == "→ W3, Folien 2, 4");
            // End markers at both ends, but not where the continuation marker names it already.
            var ends = Edits.Update(doc, new[] { "w" }, x => x.WithParam(Designations.EndsKey, true));
            var endMarks = Reg.Render(ends.Get("w")!, new RenderContext(ends)).OfType<TextPrim>().Where(t => t.Tag == Designations.EndTag).ToList();
            Assert.Single(endMarks);
            Assert.True(endMarks[0].Position.X < 40);
            // The label follows edits (it is rendered, not stored).
            var moved = Edits.Move(doc, new[] { "w" }, new Vec2(0, -30));
            Assert.Contains(Reg.Render(moved.Get("w")!, new RenderContext(moved)).OfType<TextPrim>(), t => t.Tag == Designations.LabelTag && t.Position.Y < 30);
        }

        [Fact]
        public void TypesWithTheirOwnDesignationKeepItAndActionsAreNotDoubled()
        {
            var own = Line("media.labelled", "p", new(20, 50), new(80, 50), ("lineNo", "100-P-001"));
            var doc = Doc(own, Line("conn.wire", "w", new(20, 70), new(80, 70)));
            Assert.Equal("100-P-001", Designations.Of(Reg, own));
            Assert.DoesNotContain(Reg.Render(own, new RenderContext(doc)).OfType<TextPrim>(), t => t.Tag == Designations.LabelTag);
            Assert.DoesNotContain(Reg.Actions(own, new RenderContext(doc)), a => a.Id == "line.ref");
            Assert.Contains(Reg.Actions(own, new RenderContext(doc)), a => a.Id == "line.ref.ends");
            var wire = Reg.Actions(doc.Get("w")!, new RenderContext(doc));
            Assert.Contains(wire, a => a.Id == "line.ref" && a.Input is not null);
            Assert.Contains(wire, a => a.Id == "line.value");
            Assert.DoesNotContain(wire, a => a.Id == "line.ref.ends");
            var named = ContextActions.Run(Reg, doc, new[] { "w" }, "line.ref", " -W7 ", Lang.En);
            Assert.Equal("-W7", Designations.Of(Reg, named.Get("w")!));
        }

        [Fact]
        public void ElementsWithPortsGetAGenericDesignationAbove()
        {
            var pump = new SketchObject { Id = "pump", Type = "media.pump", Transform = new Transform(100, 50), Size = new Size2(10, 10) };
            var doc = Doc(pump);
            var act = Reg.Actions(pump, new RenderContext(doc)).Single(a => a.Id == "elem.ref");
            var named = ContextActions.Run(Reg, doc, new[] { "pump" }, act.Id, "P1", Lang.En);
            var label = Reg.Render(named.Get("pump")!, new RenderContext(named)).OfType<TextPrim>().Single(t => t.Tag == Designations.LabelTag);
            Assert.Equal("P1", label.Text);
            Assert.True(label.Position.Y < 45);
            // No designation action for elements without connection ports.
            var part = new SketchObject { Id = "g", Type = "conn.gear", Transform = new Transform(50, 50), Size = new Size2(10, 10) };
            Assert.DoesNotContain(Reg.Actions(part, new RenderContext(Doc(part))), a => a.Id == "elem.ref");
        }

        [Fact]
        public void SameNameSelectsEqualDesignationsOnThePage()
        {
            var doc = Doc(
                Line("conn.wire", "a", new(10, 10), new(50, 10), ("ref", "W3")),
                Line("conn.wire", "b", new(10, 30), new(50, 30), ("ref", "-w3")),
                Line("conn.wire", "c", new(10, 50), new(50, 50), ("ref", "W4")),
                Line("media.note", "note", new(10, 70), new(50, 70), ("ref", "W3")));
            Assert.Equal(new[] { "a", "b" }, Connectivity.Connected(Reg, doc, "a", ConnectKind.SameName));
            Assert.Contains(Connectivity.Options(Reg, doc, "a"), o => o.Kind == ConnectKind.SameName);
            Assert.DoesNotContain(Connectivity.Options(Reg, doc, "c"), o => o.Kind == ConnectKind.SameName);
            Assert.Equal("Gleicher Name", Connectivity.Label(ConnectKind.SameName).De);
            var index = Designations.Index(Reg, doc);
            Assert.Equal(new[] { "a", "b" }, index["W3"]);
            Assert.False(index.ContainsKey("NOTE"));
            Assert.Equal("W3", Designations.Normalize(" -w3"));
        }
    }
}
