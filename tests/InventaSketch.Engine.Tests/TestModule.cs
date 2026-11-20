using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Test-only module (the real modules may still be empty while they are developed in parallel).</summary>
    internal sealed class EngineTestModule : IModule
    {
        public string Id => "etest";
        public I18nText Name => new("Engine test", "Engine-Test");
        public string Version => "1.0";

        public IEnumerable<ObjectType> Types => new ObjectType[] { new Block(), new Wheel(), new Wire(), new Curve() };

        public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("etest", new I18nText("Test parts", "Testteile"), Id, 5) };

        public IEnumerable<LibraryItem> Library => new[]
        {
            new LibraryItem("etest.block", "etest.block", new I18nText("Block", "Block"), "etest") { Keywords = new[] { "box" } },
            new LibraryItem("etest.block.hatched", "etest.block", new I18nText("Hatched block", "Schraffierter Block"), "etest")
            {
                Style = new Style { Fill = Fill.Hatched(new HatchPattern(45, 2)) },
            },
            new LibraryItem("etest.wheel", "etest.wheel", new I18nText("Wheel", "Rad"), "etest") { Size = new Size2(20, 20) },
            new LibraryItem("etest.wire", "etest.wire", new I18nText("Wire", "Draht"), "etest"),
            new LibraryItem("etest.arrow", "etest.wire", new I18nText("Arrow", "Pfeil"), "etest") { Style = new Style { ArrowEnd = ArrowHead.Triangle } },
            new LibraryItem("etest.curve", "etest.curve", new I18nText("Curve", "Kurve"), "etest"),
        };

        internal sealed class Block : ObjectType
        {
            public override string Id => "etest.block";
            public override I18nText Name => new("Block", "Block");
            public override Size2 DefaultSize => new(20, 10);
            public override IReadOnlyList<ViewDef> Views => new[] { new ViewDef("front", new I18nText("Front", "Vorne")), new ViewDef("side", new I18nText("Side", "Seite")) };
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("teeth", new I18nText("Teeth", "Zähne"), ParamKind.Integer) { Default = 12, Min = 6, Max = 200 },
            };

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var style = Style.Visible.Merge(o.Style);
                return ViewOf(o) == "side"
                    ? new Primitive[] { Prim.Line(-o.Size.W / 2, 0, o.Size.W / 2, 0, style) }
                    : new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, style) };
            }

            public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            {
                var big = o.Num("teeth", 12) > 20;
                yield return new ContextAction("etest.more", new I18nText("More teeth", "Mehr Zähne"), "plus", x => x.WithParam("teeth", 40)) { IsActive = big };
            }
        }

        internal sealed class Wheel : ObjectType
        {
            public override string Id => "etest.wheel";
            public override I18nText Name => new("Wheel", "Rad");
            public override bool PreferUniformScale => true;

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.Ellipse(0, 0, o.Size.W / 2, o.Size.H / 2, Style.Visible.Merge(o.Style)) };

            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, o.Size.W / 2);
            }
        }

        internal sealed class Wire : ObjectType
        {
            public override string Id => "etest.wire";
            public override I18nText Name => new("Wire", "Draht");
            public override GeometryKind Geometry => GeometryKind.Points;

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var p = ctx.Doc.ResolvePoints(o);
                return new Primitive[] { Prim.Line(p[0], p[p.Count - 1], Style.Visible.Merge(o.Style)) };
            }
        }

        internal sealed class Curve : ObjectType
        {
            public override string Id => "etest.curve";
            public override I18nText Name => new("Curve", "Kurve");

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                double w = o.Size.W / 2, h = o.Size.H / 2;
                return new Primitive[]
                {
                    Prim.Path(new[] { PathSeg.Move(new Vec2(-w, h)), PathSeg.Cubic(new Vec2(-w, -h), new Vec2(w, -h), new Vec2(w, h)) }, Style.Visible),
                };
            }
        }
    }
}
