using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new RoboticsModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        public static SketchObject Item(Registry r, string id, Vec2 at = default) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), at);

        public static T Type<T>(Registry r, string id) where T : ObjectType => (T)r.Type(id)!;

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var r = Prim.Bounds(prims)!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static void AssertFinite(IReadOnlyList<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        public static (SketchDocument Doc, string PageId) Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return (Edits.Add(doc, doc.Pages[0].Id, objects), doc.Pages[0].Id);
        }

        public static IReadOnlyList<Anchor> Anchors(Registry r, SketchObject o, AnchorRole role)
            => r.TypeOf(o).Anchors(o, Ctx()).Where(a => a.Role == role).ToList();

        public static IEnumerable<RobotArmType> Arms(Registry r) => r.Types.OfType<RobotArmType>();
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] AllTypeIds =
        {
            "robot.arm.articulated", "robot.arm.cobot", "robot.arm.scara", "robot.arm.delta", "robot.arm.gantry", "robot.mobile.agv",
            "robot.gripper.parallel", "robot.gripper.vacuum", "robot.gripper.magnetic", "robot.gripper.threefinger", "robot.tool.torch", "robot.tool.spindle",
            "robot.frame.coordinate", "robot.frame.tcp", "robot.reach.envelope",
            "robot.cell.workpiece", "robot.cell.conveyor", "robot.cell.pallet", "robot.cell.pedestal", "robot.safety.fence", "robot.safety.door",
            "robot.safety.curtain", "robot.safety.scanner", "robot.safety.zone", "robot.safety.estop",
        };

        [Fact]
        public void RegistersAllTypesAndCategories()
        {
            var r = Fx.Reg();
            Assert.Equal(AllTypeIds.OrderBy(x => x), r.Types.Select(t => t.Id).OrderBy(x => x));
            Assert.Equal(5, r.Categories.Count);
            Assert.All(r.Types, t => Assert.Contains(r.Categories, c => c.Id == t.Category));
            Assert.All(r.Library, i => Assert.Contains(r.Categories, c => c.Id == i.Category));
            Assert.Equal("robotics", r.Modules.Single().Id);
        }

        [Fact]
        public void TypeIdsFollowTheModuleConvention()
        {
            var r = Fx.Reg();
            Assert.All(r.Types, t => Assert.Matches(@"^robot\.[a-z]+\.[a-z0-9]+$", t.Id));
            Assert.All(r.Library, i => Assert.StartsWith("robot.", i.Id));
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void EverythingIsBilingual()
        {
            var r = Fx.Reg();
            Assert.False(string.IsNullOrWhiteSpace(new RoboticsModule().Name.De));
            foreach (var c in r.Categories) Assert.False(string.IsNullOrWhiteSpace(c.Name.De), c.Id);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Name.De), t.Id);
                Assert.NotEqual(t.Name.En, t.Name.De);
                Assert.NotEmpty(t.Keywords);
                foreach (var v in t.Views) Assert.False(string.IsNullOrWhiteSpace(v.Label.De), t.Id + " view " + v.Id);
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrWhiteSpace(p.Label.De), t.Id + "." + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrWhiteSpace(opt.Label.De), t.Id + "." + p.Key + "=" + opt.Value);
                }
                var o = t.Create(Vec2.Zero);
                foreach (var a in t.Actions(o, Lang.En)) Assert.False(string.IsNullOrWhiteSpace(a.Label.De), t.Id + " action " + a.Id);
            }
            foreach (var i in r.Library) Assert.False(string.IsNullOrWhiteSpace(i.Name.De), i.Id);
        }

        [Fact]
        public void ViewsUseTheSharedPictograms()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var v in t.Views)
                {
                    Assert.Contains(IconIds.ForView(v), new[] { IconIds.Side, IconIds.Top, IconIds.Axial });
                    Assert.NotNull(v.Size);
                }
            foreach (var arm in Fx.Arms(r)) Assert.Equal(new[] { "side", "top" }, arm.Views.Select(v => v.Id).OrderBy(x => x));
        }

        [Fact]
        public void EveryParameterIsOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Create(Vec2.Zero);
                Assert.Equal(t.Parameters.Count(p => p.Default.HasValue), o.Params.Count);
                foreach (var p in t.Parameters.Where(p => p.HasOptionIcons)) Assert.All(p.Options!, opt => Assert.Contains(opt.Icon!, IconIds.All));
            }
        }

        [Fact]
        public void LibraryItemsResolveAndCreate()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.Id == t.Id && i.TypeId == t.Id);
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId);
                Assert.NotNull(t);
                var o = r.Create(item, new Vec2(10, 10));
                Assert.Equal(item.TypeId, o.Type);
                if (item.View is not null) Assert.Contains(t!.Views, v => v.Id == item.View);
                foreach (var kv in item.Params ?? new Dictionary<string, ParamValue>()) Assert.Contains(t!.Parameters, p => p.Key == kv.Key);
            }
            Assert.True(r.Library.Count >= 45, "library has " + r.Library.Count);
        }

        [Theory]
        [InlineData("knickarm", "robot.arm.articulated")]
        [InlineData("roboter", "robot.arm.articulated")]
        [InlineData("industrial robot", "robot.arm.articulated")]
        [InlineData("greifer", "robot.gripper.parallel")]
        [InlineData("sauggreifer", "robot.gripper.vacuum")]
        [InlineData("schutzzaun", "robot.safety.fence")]
        [InlineData("lichtvorhang", "robot.safety.curtain")]
        [InlineData("arbeitsraum", "robot.reach.envelope")]
        [InlineData("reichweite", "robot.reach.envelope")]
        [InlineData("schweißbrenner", "robot.tool.torch")]
        [InlineData("ftf", "robot.mobile.agv")]
        [InlineData("not-halt", "robot.safety.estop")]
        [InlineData("scara", "robot.arm.scara")]
        [InlineData("delta", "robot.arm.delta")]
        [InlineData("portalroboter", "robot.arm.gantry")]
        [InlineData("cobot", "robot.arm.cobot")]
        [InlineData("laserscanner", "robot.safety.scanner")]
        [InlineData("koordinatensystem", "robot.frame.coordinate")]
        [InlineData("tcp", "robot.frame.tcp")]
        [InlineData("förderband", "robot.cell.conveyor")]
        public void SearchFindsElementsInBothLanguages(string query, string itemId)
            => Assert.Contains(Fx.Reg().Search(query), i => i.Id == itemId);
    }
}
