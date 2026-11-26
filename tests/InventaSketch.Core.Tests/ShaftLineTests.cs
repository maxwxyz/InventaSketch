using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Core shaft lines (<see cref="ShaftLines"/>) over the coupling graph, its re-entrancy guard and pitch circles that do not
    /// mesh (<see cref="IPitchMesh"/>), with small test types: a shaft (axis across its box), wheels and pulleys (hub at the
    /// centre, pitch circle radius 10), a bearing, a cage and a probe whose anchors ask for the graph (as a linked chart does).
    /// </summary>
    public class ShaftLineTests
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly Relation Speed = Relation.Equality("t.shaft.speed", T("Same speed", "Gleiche Drehzahl"));
        private static readonly Relation Torque = Relation.Equality("t.shaft.torque", T("Same torque", "Gleiches Moment"));

        private abstract class Part : ObjectType, IValueType, IValueLinks, IShaftPart
        {
            private readonly string _id;
            private readonly ShaftPart _part;
            private readonly bool _torque;
            protected Part(string id, ShaftPart part, bool torque) { _id = id; _part = part; _torque = torque; }
            public override string Id => _id;
            public override I18nText Name => new(_id);
            public override Size2 DefaultSize => new(20, 20);
            public IReadOnlyList<VarDef> Variables => _torque
                ? new[] { new VarDef("n", T("n", "n"), T("n", "n"), Quantities.RotationalSpeed) { Param = "speed" }, new VarDef("T", T("T", "T"), T("T", "T"), Quantities.Torque) { Param = "torque" } }
                : new[] { new VarDef("n", T("n", "n"), T("n", "n"), Quantities.RotationalSpeed) { Param = "speed" } };
            public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();
            public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
                => ctx.Registry is { } r ? ShaftLines.Of(r, ctx.Doc).Links(o.Id, Speed, Torque) : Array.Empty<BoundRelation>();
            ShaftPart IShaftPart.ShaftPart(SketchObject o) => _part;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
        }

        private sealed class ShaftType : Part
        {
            public ShaftType() : base("t.shaft", ShaftPart.PassThrough, true) { }
            public override Size2 DefaultSize => new(100, 6);
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Shaft(new Vec2(-o.Size.W / 2, 0), new Vec2(o.Size.W / 2, 0), o.Size.H / 2) };
        }

        private class WheelType : Part
        {
            public WheelType(string id = "t.wheel", ShaftPart part = ShaftPart.Member, bool torque = true) : base(id, part, torque) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
                => new[] { Anchor.Hub(Vec2.Zero, 0), Anchor.Circle(AnchorRole.Pitch, Vec2.Zero, 10) };
        }

        private sealed class PulleyType : WheelType, IPitchMesh
        {
            public PulleyType() : base("t.pulley") { }
            public bool MeshesAtPitch(SketchObject o) => false;
        }

        private sealed class HubType : Part
        {
            public HubType(string id, ShaftPart part, bool torque) : base(id, part, torque) { }
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => new[] { Anchor.Hub(Vec2.Zero, 0) };
        }

        /// <summary>Its anchors ask for the coupling graph and the shaft lines of the document (as a linked chart solving its pump).</summary>
        private sealed class ProbeType : ObjectType
        {
            public static readonly List<(bool Pending, int Links, int Lines)> Seen = new();
            public Registry? Reg;
            public override string Id => "t.probe";
            public override I18nText Name => new("Probe");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                if (Reg is { } r)
                    lock (Seen) Seen.Add((Coupling.GraphPending(ctx.Doc), Coupling.Graph(r, ctx.Doc).Count, ShaftLines.Of(r, ctx.Doc).Lines.Count));
                return Array.Empty<Anchor>();
            }
        }

        private sealed class TestModule : IModule
        {
            public readonly ProbeType Probe = new();
            public string Id => "ts";
            public I18nText Name => new("Shaft test");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[]
            {
                new ShaftType(), new WheelType(), new PulleyType(), new HubType("t.bearing", ShaftPart.Leaf, false),
                new HubType("t.cage", ShaftPart.Outside, true), new HubType("t.machine", ShaftPart.Member, true), Probe,
            };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        private static Registry Reg(out TestModule module)
        {
            module = new TestModule();
            var r = new Registry().Register(module);
            module.Probe.Reg = r;
            return r;
        }

        private static Registry Reg() => Reg(out _);

        private static SketchObject Make(Registry r, string type, string id, double x, double y) => r.Type(type)!.Create(new Vec2(x, y)) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Shaft lines");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static bool Binds(BoundRelation b, Relation r, string a, string c)
            => b.Relation == r && new[] { b.Map["a"].ObjectId, b.Map["b"].ObjectId }.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { a, c }.OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>A shaft from x = 0 to 100 at y = 50 with parts on its axis.</summary>
        private static SketchDocument OnShaft(Registry r, params (string Type, string Id, double X)[] parts)
            => Doc(new[] { Make(r, "t.shaft", "s", 50, 50) }.Concat(parts.Select(p => Make(r, p.Type, p.Id, p.X, 50))).ToArray());

        [Fact]
        public void PartsOnAShaftFormOneLineWithTheShaftAsRepresentative()
        {
            var r = Reg();
            var doc = OnShaft(r, ("t.wheel", "a", 20), ("t.machine", "b", 80), ("t.bearing", "brg", 50));
            var lines = ShaftLines.Of(r, doc);
            var line = Assert.Single(lines.Lines);
            Assert.Equal(new[] { "a", "b", "s" }, line.Members);
            Assert.Equal(new[] { "a", "b" }, line.Torque);
            Assert.Equal(new[] { "s" }, line.Pass);
            Assert.Equal("s", line.Rep);
            Assert.Equal("s", line.TorqueRep);
            Assert.True(line.CarriesTorque);
            // The bearing takes the shaft speed but joins nothing.
            Assert.Null(lines.LineOf("brg"));
            Assert.Equal("s", lines.LeafCarrier("brg"));
            Assert.Equal(new[] { "brg" }, lines.LeavesOf("s"));

            // Star: the shaft links every part, the others link the shaft.
            var fromShaft = lines.Links("s", Speed, Torque).ToList();
            Assert.Contains(fromShaft, b => Binds(b, Speed, "s", "a"));
            Assert.Contains(fromShaft, b => Binds(b, Speed, "s", "b"));
            Assert.Contains(fromShaft, b => Binds(b, Speed, "s", "brg"));
            Assert.Contains(fromShaft, b => Binds(b, Torque, "s", "a"));
            Assert.Contains(fromShaft, b => Binds(b, Torque, "s", "b"));
            var fromA = lines.Links("a", Speed, Torque).ToList();
            Assert.Equal(2, fromA.Count);
            Assert.Contains(fromA, b => Binds(b, Speed, "s", "a"));
            Assert.Contains(fromA, b => Binds(b, Torque, "s", "a"));
            Assert.DoesNotContain(lines.Links("a", Speed, null), b => b.Relation == Torque);
        }

        [Fact]
        public void ABranchPassesNoTorque()
        {
            var r = Reg();
            var lines = ShaftLines.Of(r, OnShaft(r, ("t.wheel", "a", 10), ("t.machine", "b", 50), ("t.machine", "c", 90)));
            var line = Assert.Single(lines.Lines);
            Assert.Equal(3, line.Torque.Count);
            Assert.False(line.CarriesTorque);
            Assert.DoesNotContain(lines.Links("b", Speed, Torque), b => b.Relation == Torque);
            Assert.Contains(lines.Links("b", Speed, Torque), b => Binds(b, Speed, "s", "b"));
        }

        [Fact]
        public void ALooseHubAndAnOutsidePartDoNotJoin()
        {
            var r = Reg();
            var loose = Make(r, "t.wheel", "loose", 30, 50).WithParam("hub", "loose");
            // A cage concentric with a machine: no line, no leaf.
            var doc = Doc(Make(r, "t.shaft", "s", 50, 50), loose, Make(r, "t.machine", "m", 70, 50),
                Make(r, "t.cage", "cage", 200, 50), Make(r, "t.machine", "m2", 200, 50));
            var lines = ShaftLines.Of(r, doc);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Bonded && new[] { l.A, l.B }.Contains("cage"));
            Assert.Null(lines.LineOf("loose"));
            Assert.Null(lines.LineOf("cage"));
            Assert.Null(lines.LineOf("m2"));
            Assert.Equal(new[] { "m", "s" }, lines.LineOf("m")!.Members);
        }

        [Fact]
        public void TwoMachinesOnOneHubShareSpeedAndTorque()
        {
            var r = Reg();
            var doc = Doc(Make(r, "t.machine", "drive", 40, 40).WithParam("speed", "1500 rpm").WithParam("torque", "40 N·m"),
                Make(r, "t.machine", "load", 40, 40));
            var line = Assert.Single(ShaftLines.Of(r, doc).Lines);
            Assert.Equal("drive", line.Rep);
            var t = ValueSolver.Solve(doc, r, new[] { "load" }, new ValueSolveOptions { BudgetMs = 10000 });
            Assert.Empty(t.Conflicts);
            Assert.Equal(1500 * 2 * Math.PI / 60, t.Get("load", "n")!.Value!.Value, 9);
            Assert.Equal(40, t.Get("load", "T")!.Value!.Value, 9);
        }

        [Fact]
        public void TheGraphIsReentrancySafeAndANestedRequestIsNotCached()
        {
            var r = Reg();
            ProbeType.Seen.Clear();
            var doc = OnShaft(r, ("t.wheel", "a", 20), ("t.machine", "b", 80));
            doc = Edits.Add(doc, doc.Pages[0].Id, Make(r, "t.probe", "probe", 300, 300));
            Assert.False(Coupling.GraphPending(doc));
            var graph = Coupling.Graph(r, doc);
            Assert.Equal(2, graph.Count(l => l.Kind == LinkKind.Carried));
            Assert.False(Coupling.GraphPending(doc));
            // The probe's anchors were read while the graph was built: the nested request saw an empty graph and no lines.
            Assert.Contains(ProbeType.Seen, s => s.Pending && s.Links == 0 && s.Lines == 0);
            // Neither the graph nor the shaft lines kept that empty answer.
            Assert.Same(graph, Coupling.Graph(r, doc));
            Assert.Single(ShaftLines.Of(r, doc).Lines);
            Assert.Same(ShaftLines.Of(r, doc), ShaftLines.Of(r, doc));
        }

        [Fact]
        public void PulleysDoNotMeshGearsDo()
        {
            var r = Reg();
            Assert.Contains(Coupling.Graph(r, Doc(Make(r, "t.wheel", "a", 50, 50), Make(r, "t.wheel", "b", 70, 50))), l => l.Kind == LinkKind.Mesh);
            Assert.DoesNotContain(Coupling.Graph(r, Doc(Make(r, "t.pulley", "a", 50, 50), Make(r, "t.pulley", "b", 70, 50))), l => l.Kind == LinkKind.Mesh);
            Assert.DoesNotContain(Coupling.Graph(r, Doc(Make(r, "t.wheel", "a", 50, 50), Make(r, "t.pulley", "b", 70, 50))), l => l.Kind == LinkKind.Mesh);
            // Moving a pulley away from a touching one leaves both sizes alone (no centre-distance scaling).
            var before = Doc(Make(r, "t.pulley", "a", 50, 50), Make(r, "t.pulley", "b", 70, 50));
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { "b" }, new Vec2(10, 0)));
            Assert.Equal(before.Get("a")!.Size, after.Get("a")!.Size);
            Assert.Equal(before.Get("b")!.Size, after.Get("b")!.Size);
        }
    }
}
