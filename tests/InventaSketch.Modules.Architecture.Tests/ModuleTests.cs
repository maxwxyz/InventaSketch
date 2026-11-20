using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new ArchitectureModule());

        public static RenderContext Ctx(Lang lang = Lang.En) => new(SketchDocument.Create(), lang);

        public static (Registry R, SketchDocument Doc) Doc(params SketchObject[] objs)
        {
            var r = Reg();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, objs);
            return (r, doc);
        }

        public static SketchObject Wall(Registry r, params Vec2[] pts)
            => r.Create(r.Item("arch.wall.exterior")!, Vec2.Zero) with { Points = pts };

        public static void AssertFinite(IEnumerable<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var r = Prim.Bounds(prims)!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static void AssertNearPoints(IReadOnlyList<Primitive> prims, IReadOnlyList<Vec2> pts, double margin, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var b = Prim.Bounds(prims)!.Value;
            var allowed = Rect.FromPoints(pts).Inflate(margin);
            Assert.True(allowed.Contains(b), $"{what}: bounds {b} not near points {allowed}");
            AssertFinite(prims, what);
        }
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] Expected =
        {
            "arch.wall", "arch.room", "arch.beam", "arch.column",
            "arch.door.single", "arch.door.double", "arch.door.sliding", "arch.door.opening",
            "arch.window.single", "arch.window.double", "arch.window.french",
            "arch.stair.straight", "arch.stair.l", "arch.stair.u", "arch.stair.spiral",
            "arch.furniture.bed", "arch.furniture.sofa", "arch.furniture.table", "arch.furniture.desk", "arch.furniture.wardrobe",
            "arch.furniture.counter", "arch.furniture.sink", "arch.furniture.stove", "arch.furniture.fridge",
            "arch.sanitary.wc", "arch.sanitary.washbasin", "arch.sanitary.shower", "arch.sanitary.bathtub",
            "arch.hvac.radiator", "arch.hvac.heatpump", "arch.hvac.boiler", "arch.hvac.pipe",
            "arch.site.north", "arch.site.level", "arch.site.section", "arch.site.boundary", "arch.site.parking", "arch.site.tree",
        };

        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var r = Fx.Reg();
            Assert.Equal("architecture", r.Modules.Single().Id);
            var ids = r.Types.Select(t => t.Id).ToList();
            foreach (var id in Expected) Assert.Contains(id, ids);
            Assert.All(ids, id => Assert.StartsWith("arch.", id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasIconViews()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.NotEmpty(t.Views);
                Assert.Equal("plan", t.Views[0].Id);
                foreach (var v in t.Views)
                {
                    Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                    Assert.Equal(v.Id == "plan" ? IconIds.Plan : IconIds.Front, v.Icon);
                }
                foreach (var p in t.Parameters) Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
            }
            foreach (var id in new[] { "arch.wall", "arch.door.single", "arch.window.double" })
                Assert.Contains(r.Type(id)!.Views, v => v.Id == "elevation");
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Theory]
        [InlineData("wand")]
        [InlineData("wall")]
        [InlineData("tür")]
        [InlineData("fenster")]
        [InlineData("treppe")]
        [InlineData("badewanne")]
        [InlineData("wärmepumpe")]
        [InlineData("nordpfeil")]
        [InlineData("küche")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                if (t.Geometry == GeometryKind.Points) o = o with { Points = t.DefaultPoints };
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
        }
    }

    public class RenderTests
    {
        private static IEnumerable<(LibraryItem Item, ObjectType Type, string View)> ItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2.5, 0.6)]
        [InlineData(0.4, 1.8)]
        [InlineData(0.3, 0.3)]
        public void EveryBoxItemRendersInsideItsBoxInEveryView(double sx, double sy)
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in ItemViews(r).Where(x => x.Type.Geometry == GeometryKind.Box))
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var o = r.Create(item, Vec2.Zero) with { View = view };
                    o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                    Fx.AssertInsideBox(type.Render(o, Fx.Ctx(lang)), o.Size, $"{item.Id}/{view} {sx}x{sy} {lang}");
                    count++;
                }
            Assert.True(count > 80, "expected many item/view combinations, got " + count);
        }

        [Fact]
        public void EveryPointItemRendersNearItsPoints()
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in ItemViews(r).Where(x => x.Type.Geometry == GeometryKind.Points))
            {
                var o = r.Create(item, new Vec2(100, 80)) with { View = view };
                Assert.NotNull(o.Points);
                // Elevations rise above the plan line by the wall height; everything else stays within a few millimetres.
                var margin = view == "elevation" ? 40 : 12;
                Fx.AssertNearPoints(type.Render(o, Fx.Ctx()), o.Points!, margin, $"{item.Id}/{view}");
            }
        }

        [Fact]
        public void WallRendersFacesHatchAndCaps()
        {
            var r = Fx.Reg();
            var wall = Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0));
            var prims = r.TypeOf(wall).Render(wall, Fx.Ctx());
            Assert.Equal(2, prims.Count(p => p.Tag == ArchTags.WallFace));
            Assert.Equal(2, prims.Count(p => p.Tag == ArchTags.WallCap));
            var fill = (PathPrim)prims.Single(p => p.Tag == ArchTags.WallFill);
            Assert.Equal(FillKind.Hatch, fill.Style.Fill!.Kind);
            // 365 mm exterior wall at 1:100 is 3.65 mm thick.
            var b = Prim.Bounds(new[] { fill })!.Value;
            Assert.Equal(3.65, b.H, 6);
            Assert.Equal(60, b.W, 6);

            var partition = r.Create(r.Item("arch.wall.partition")!, Vec2.Zero);
            var pf = (PathPrim)r.TypeOf(partition).Render(partition, Fx.Ctx()).Single(p => p.Tag == ArchTags.WallFill);
            Assert.Equal(FillKind.Solid, pf.Style.Fill!.Kind);
            Assert.Equal(1.0, Prim.Bounds(new[] { pf })!.Value.H, 6);
        }

        [Fact]
        public void PlanScaleChangesWallThickness()
        {
            var r = Fx.Reg();
            var wall = Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0)).WithParam("scale", 50.0).WithParam("thickness", 240.0);
            var fill = r.TypeOf(wall).Render(wall, Fx.Ctx()).Single(p => p.Tag == ArchTags.WallFill);
            Assert.Equal(4.8, Prim.Bounds(new[] { fill })!.Value.H, 6);
        }

        [Fact]
        public void ClosedWallHasMitredCornersAndNoCaps()
        {
            var r = Fx.Reg();
            var wall = r.Create(r.Item("arch.wall.rect")!, new Vec2(100, 100));
            var prims = r.TypeOf(wall).Render(wall, Fx.Ctx());
            Assert.DoesNotContain(prims, p => p.Tag == ArchTags.WallCap);
            Assert.Equal(2, prims.Count(p => p.Tag == ArchTags.WallFill));
            // Outer face corner of a 100 x 70 loop with 3.65 mm walls sits at half the thickness outside the centre line.
            var all = Prim.Bounds(prims)!.Value;
            Assert.Equal(100 + 3.65, all.W, 6);
            Assert.Equal(70 + 3.65, all.H, 6);
        }

        [Fact]
        public void TwoWallsJoinedAtAnEndAreMitred()
        {
            var r0 = Fx.Reg();
            var a = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0));
            var b = Fx.Wall(r0, new Vec2(60, 0), new Vec2(60, 40));
            var (r, doc) = Fx.Doc(a, b);
            var ctx = new RenderContext(doc);
            var pa = r.Render(a, ctx); var pb = r.Render(b, ctx);
            Assert.Single(pa, p => p.Tag == ArchTags.WallCap); // only the free end at (0,0)
            Assert.Single(pb, p => p.Tag == ArchTags.WallCap);
            // Both cores share the mitre corners (outer at (61.825, -1.825), inner at (58.175, 1.825)).
            var h = 3.65 / 2;
            foreach (var corner in new[] { new Vec2(60 + h, -h), new Vec2(60 - h, h) })
                foreach (var prims in new[] { pa, pb })
                {
                    var fill = (PathPrim)prims.Single(p => p.Tag == ArchTags.WallFill);
                    Assert.Contains(fill.Segments, s => s.Op != SegOp.Z && s.P.DistanceTo(corner) < 1e-6);
                }
        }

        [Fact]
        public void WallButtingAgainstAnotherIsTrimmedAndBreaksItsFace()
        {
            var r0 = Fx.Reg();
            var through = Fx.Wall(r0, new Vec2(0, 0), new Vec2(100, 0));
            var stub = r0.Create(r0.Item("arch.wall.interior")!, Vec2.Zero) with { Points = new[] { new Vec2(50, 0), new Vec2(50, 40) } };
            var (r, doc) = Fx.Doc(through, stub);
            var ctx = new RenderContext(doc);
            var pt = r.Render(through, ctx);
            // The lower face (y = +1.825) is broken where the 240 mm wall joins: three face runs instead of two.
            Assert.Equal(3, pt.Count(p => p.Tag == ArchTags.WallFace));
            var ps = r.Render(stub, ctx);
            Assert.Single(ps, p => p.Tag == ArchTags.WallCap);
            var fill = Prim.Bounds(ps.Where(p => p.Tag == ArchTags.WallFill))!.Value;
            Assert.Equal(3.65 / 2, fill.Y, 6); // trimmed to the face of the through wall
        }

        [Fact]
        public void DoorTakesTheThicknessOfItsWall()
        {
            var r0 = Fx.Reg();
            var wall = Fx.Wall(r0, new Vec2(0, 50), new Vec2(100, 50));
            var door = r0.Create(r0.Item("arch.door.single")!, new Vec2(40, 50));
            var (r, doc) = Fx.Doc(wall, door);
            var mask = (RectPrim)r.TypeOf(door).Render(door, new RenderContext(doc)).Single(p => p.Tag == ArchTags.Mask);
            Assert.Equal(3.65, mask.H, 6);
            Assert.Equal(door.Size.W, mask.W, 6);
            // Off the wall the optional parameter applies (240 mm at 1:100).
            var free = (RectPrim)r.TypeOf(door).Render(door, Fx.Ctx()).Single(p => p.Tag == ArchTags.Mask);
            Assert.Equal(2.4, free.H, 6);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void DoorLeafFollowsHingeAndSwing(bool hingeRight, bool swingOut)
        {
            var r = Fx.Reg();
            var door = r.Create(r.Item("arch.door.single")!, Vec2.Zero).WithParam("hingeRight", hingeRight).WithParam("swingOut", swingOut);
            var prims = r.TypeOf(door).Render(door, Fx.Ctx());
            var leaf = (LinePrim)prims.Single(p => p.Tag == ArchTags.Leaf);
            Assert.Equal(hingeRight ? door.Size.W / 2 : -door.Size.W / 2, leaf.A.X, 6);
            Assert.Equal(swingOut ? -1 : 1, Math.Sign(leaf.B.Y));
            var arc = (PathPrim)prims.Single(p => p.Tag == ArchTags.Swing);
            var end = arc.Segments[arc.Segments.Count - 1].P;
            Assert.Equal(hingeRight ? -door.Size.W / 2 : door.Size.W / 2, end.X, 6); // the swing closes at the other jamb
        }

        [Fact]
        public void RoomAreaOfARectangle()
        {
            var r = Fx.Reg();
            var pts = new[] { new Vec2(10, 10), new Vec2(50, 10), new Vec2(50, 40), new Vec2(10, 40) };
            Assert.Equal(12.0, Arch.RoomArea(pts, 100), 9); // 4 m x 3 m at 1:100
            Assert.Equal(3.0, Arch.RoomArea(pts, 50), 9);
            var room = r.Create(r.Item("arch.room.living")!, Vec2.Zero) with { Points = pts };
            var en = r.TypeOf(room).Render(room, Fx.Ctx(Lang.En));
            Assert.Equal("12.00 m²", ((TextPrim)en.Single(p => p.Tag == ArchTags.RoomArea)).Text);
            Assert.Equal("Living room", ((TextPrim)en.Single(p => p.Tag == ArchTags.RoomName)).Text);
            var de = r.TypeOf(room).Render(room, Fx.Ctx(Lang.De));
            Assert.Equal("12,00 m²", ((TextPrim)de.Single(p => p.Tag == ArchTags.RoomArea)).Text);
            Assert.Equal("Wohnen", ((TextPrim)de.Single(p => p.Tag == ArchTags.RoomName)).Text);
            var named = room with { Text = "Studio" };
            Assert.Equal("Studio", ((TextPrim)r.TypeOf(named).Render(named, Fx.Ctx()).Single(p => p.Tag == ArchTags.RoomName)).Text);
            var noArea = room.WithParam("showArea", false);
            Assert.DoesNotContain(r.TypeOf(noArea).Render(noArea, Fx.Ctx()), p => p.Tag == ArchTags.RoomArea);
        }

        [Theory]
        [InlineData(0.0, "±0.00", "±0,00")]
        [InlineData(2.75, "+2.75", "+2,75")]
        [InlineData(-0.15, "-0.15", "-0,15")]
        public void LevelMarkerText(double level, string en, string de)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("arch.site.level")!, Vec2.Zero).WithParam("level", level);
            Assert.Equal(en, ((TextPrim)r.TypeOf(o).Render(o, Fx.Ctx(Lang.En)).Single(p => p.Tag == ArchTags.Label)).Text);
            Assert.Equal(de, ((TextPrim)r.TypeOf(o).Render(o, Fx.Ctx(Lang.De)).Single(p => p.Tag == ArchTags.Label)).Text);
        }
    }

    public class SnappingTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void WallExposesEndsMountsAndAxes()
        {
            var r = Fx.Reg();
            var wall = Fx.Wall(r, new Vec2(0, 0), new Vec2(60, 0), new Vec2(60, 40));
            var anchors = r.PageAnchors(wall, Fx.Ctx()).ToList();
            Assert.Equal(3, anchors.Count(a => a.Role == AnchorRole.End));
            Assert.Equal(3, anchors.Count(a => a.Role == AnchorRole.Mount));
            var axes = anchors.Where(a => a.Role == AnchorRole.Axis && a.Kind == AnchorKind.Segment).ToList();
            Assert.Equal(2, axes.Count);
            Assert.Equal(new Vec2(60, 0), axes[0].Q);
        }

        [Fact]
        public void TwoWallsSnapTogetherAtAnEndpoint()
        {
            var r0 = Fx.Reg();
            var a = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0));
            var b = Fx.Wall(r0, new Vec2(61.5, 1.2), new Vec2(61.5, 40));
            var (r, doc) = Fx.Doc(a, b);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(b, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { b.Id }, ctx, Opt);
            Assert.Contains(targets, t => t.Role == AnchorRole.Mount && t.P == new Vec2(60, 0));
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("point", res.Kind);
            Assert.Equal(new Vec2(-1.5, -1.2).X, res.Delta.X, 6);
            Assert.Equal(-1.2, res.Delta.Y, 6);

            // After the move the walls form an L joint (mitred, no cap at the corner).
            doc = Edits.Move(doc, new[] { b.Id }, res.Delta);
            var moved = doc.Get(b.Id)!;
            Assert.Equal(new Vec2(60, 0), moved.Points![0]);
            Assert.Single(r.Render(moved, new RenderContext(doc)), p => p.Tag == ArchTags.WallCap);
        }

        [Fact]
        public void DraggedWallEndSnapsOntoAnotherWallVertex()
        {
            var r0 = Fx.Reg();
            var a = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0), new Vec2(60, 40));
            var (r, doc) = Fx.Doc(a);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], Array.Empty<string>(), new RenderContext(doc), Opt);
            var res = Snapper.SnapPoint(new Vec2(59, 1), targets, Opt);
            Assert.Equal(new Vec2(60, 0), new Vec2(59, 1) + res.Delta);
        }

        [Theory]
        [InlineData("arch.door.single")]
        [InlineData("arch.door.double")]
        [InlineData("arch.door.sliding")]
        [InlineData("arch.door.opening")]
        [InlineData("arch.window.single")]
        [InlineData("arch.window.french")]
        public void OpeningHubSnapsOntoWallAxis(string item)
        {
            var r0 = Fx.Reg();
            var wall = Fx.Wall(r0, new Vec2(0, 50), new Vec2(100, 50));
            var door = r0.Create(r0.Item(item)!, new Vec2(40, 52.2));
            var (r, doc) = Fx.Doc(wall, door);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(door, ctx).ToList();
            Assert.Contains(moving, a => a.Role == AnchorRole.Hub && a.P == new Vec2(40, 52.2));
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { door.Id }, ctx, Opt);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(0, res.Delta.X, 6);
            Assert.Equal(-2.2, res.Delta.Y, 6);
        }

        [Fact]
        public void OpeningSnapsOntoAVerticalWallSegment()
        {
            var r0 = Fx.Reg();
            var wall = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0), new Vec2(60, 40));
            var window = r0.Create(r0.Item("arch.window.single")!, new Vec2(61, 20)) with { Transform = new Transform(61, 20, 90) };
            var (r, doc) = Fx.Doc(wall, window);
            var ctx = new RenderContext(doc);
            var res = Snapper.SnapMove(r.PageAnchors(window, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { window.Id }, ctx, Opt), Opt);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-1, res.Delta.X, 6);
        }
    }

    public class ActionTests
    {
        private static SketchObject Act(ObjectType t, SketchObject o, string id) => t.Actions(o, Lang.En).Single(a => a.Id == id).Apply(o);

        [Fact]
        public void WallThicknessStepsAndKindCycle()
        {
            var r = Fx.Reg();
            var t = r.Type("arch.wall")!;
            var o = t.Create(Vec2.Zero);
            Assert.Equal(240, o.Num("thickness", 0));
            Assert.Equal(300, Act(t, o, "arch.thickness.inc").Num("thickness", 0));
            Assert.Equal(200, Act(t, o, "arch.thickness.dec").Num("thickness", 0));
            Assert.Contains("300 mm", t.Actions(o, Lang.De).Single(a => a.Id == "arch.thickness.inc").Label.De);
            var kinds = new List<string>();
            for (var i = 0; i < 3; i++) { o = Act(t, o, "arch.kind"); kinds.Add(o.Str("kind", "")); }
            Assert.Equal(new[] { "interior", "partition", "exterior" }, kinds);
            Assert.True(Act(t, o, "arch.closed").Flag("closed", false));
        }

        [Fact]
        public void DoorFlipsAndWidthSteps()
        {
            var r = Fx.Reg();
            var t = r.Type("arch.door.single")!;
            var o = r.Create(r.Item("arch.door.single")!, Vec2.Zero);
            Assert.True(Act(t, o, "arch.hinge").Flag("hingeRight", false));
            Assert.True(Act(t, o, "arch.swing").Flag("swingOut", false));
            var wider = Act(t, o, "arch.width.inc");
            Assert.Equal(o.Size.W + 1.25, wider.Size.W, 6);       // +125 mm at 1:100
            Assert.Equal(o.Size.H + 2.5, wider.Size.H, 6);        // the leaf grows with the width
            Assert.Equal(o.Size.W - 1.25, Act(t, o, "arch.width.dec").Size.W, 6);
            Assert.Equal(1025, Arch.OpeningWidth(wider), 6);

            var w = r.Type("arch.window.double")!;
            var win = r.Create(r.Item("arch.window.double")!, Vec2.Zero);
            Assert.True(Act(w, win, "arch.side").Flag("flip", false));
            Assert.Equal(win.Size.H, Act(w, win, "arch.width.inc").Size.H, 6);
            Assert.DoesNotContain(w.Actions(win, Lang.En), a => a.Id == "arch.swing");
        }

        [Fact]
        public void StairAndSiteActions()
        {
            var r = Fx.Reg();
            var s = r.Type("arch.stair.straight")!;
            var o = s.Create(Vec2.Zero);
            Assert.True(Act(s, o, "arch.stair.reverse").Flag("down", false));
            Assert.Equal(15, Act(s, o, "arch.steps.inc").Int("steps", 0));
            Assert.Equal(13, s.Render(o, Fx.Ctx()).Count(p => p.Tag == ArchTags.Tread));

            var tree = r.Type("arch.site.tree")!;
            Assert.Equal("conifer", Act(tree, tree.Create(Vec2.Zero), "arch.tree.kind").Str("kind", ""));
            var pipe = r.Type("arch.hvac.pipe")!;
            Assert.Equal("return", Act(pipe, pipe.Create(Vec2.Zero), "arch.pipe.medium").Str("medium", ""));
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void FloorPlanSurvivesJsonRoundTrip()
        {
            var (r, doc) = FloorPlan.Build();
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(doc.Objects.Count, back.Objects.Count);
            foreach (var o in doc.Objects.Values)
            {
                var b = back.Get(o.Id)!;
                Assert.Equal(o.Type, b.Type);
                Assert.Equal(o.View, b.View);
                Assert.Equal(o.Size, b.Size);
                Assert.Equal(o.Transform, b.Transform);
                Assert.Equal(o.Points?.ToList(), b.Points?.ToList());
                Assert.Equal(o.Params.OrderBy(kv => kv.Key), b.Params.OrderBy(kv => kv.Key));
            }
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            var compact = DocumentJson.FromCompact(DocumentJson.ToCompact(doc));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0], lang: Lang.De), SvgRenderer.RenderPage(r, compact, compact.Pages[0], lang: Lang.De));
        }
    }

    /// <summary>Sample house used by the round-trip test and the gallery.</summary>
    internal static class FloorPlan
    {
        public static (Registry R, SketchDocument Doc) Build()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Small house");
            SketchObject At(string item, double x, double y, Func<SketchObject, SketchObject>? f = null)
            {
                var o = r.Create(r.Item(item)!, new Vec2(x, y));
                return f is null ? o : f(o);
            }
            SketchObject Rot(SketchObject o, double deg) => o with { Transform = o.Transform with { Rotation = deg } };
            // 10 m x 7 m house at 1:100 with origin (60, 40).
            const double x0 = 60, y0 = 40, x1 = 160, y1 = 110;
            var objs = new List<SketchObject>
            {
                At("arch.room.living", 0, 0, o => o with { Points = new[] { new Vec2(x0, y0), new Vec2(115, y0), new Vec2(115, 80), new Vec2(x0, 80) } }),
                At("arch.room.kitchen", 0, 0, o => o with { Points = new[] { new Vec2(115, y0), new Vec2(x1, y0), new Vec2(x1, 75), new Vec2(115, 75) } }),
                At("arch.room.bath", 0, 0, o => o with { Points = new[] { new Vec2(130, 75), new Vec2(x1, 75), new Vec2(x1, y1), new Vec2(130, y1) } }),
                At("arch.room.bedroom", 0, 0, o => o with { Points = new[] { new Vec2(x0, 80), new Vec2(115, 80), new Vec2(115, y1), new Vec2(x0, y1) } }),
                At("arch.room.hall", 0, 0, o => o with { Points = new[] { new Vec2(115, 75), new Vec2(130, 75), new Vec2(130, y1), new Vec2(115, y1) } }),
                At("arch.wall.rect", 0, 0, o => o with { Points = new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) } }),
                At("arch.wall.interior", 0, 0, o => o with { Points = new[] { new Vec2(115, y0), new Vec2(115, y1) } }),
                At("arch.wall.partition", 0, 0, o => o with { Points = new[] { new Vec2(x0, 80), new Vec2(115, 80) } }),
                At("arch.wall.partition", 0, 0, o => o with { Points = new[] { new Vec2(115, 75), new Vec2(x1, 75) } }),
                At("arch.wall.partition", 0, 0, o => o with { Points = new[] { new Vec2(130, 75), new Vec2(130, y1) } }),
                At("arch.door.entrance", 122.5, y1, o => o.WithParam("swingOut", true).WithParam("hingeRight", true)),
                At("arch.door.single", 115, 95, o => Rot(o, 90).WithParam("swingOut", true)),
                At("arch.door.single", 145, 75, o => o.WithParam("hingeRight", true)),
                At("arch.door.single", 85, 80, o => o.WithParam("swingOut", true)),
                At("arch.window.double", 85, y0),
                At("arch.window.french", x0, 60, o => Rot(o, 90).WithParam("flip", true)),
                At("arch.window.single", 140, y0),
                At("arch.window.double", 85, y1, o => o.WithParam("flip", true)),
                At("arch.window.single", x1, 92, o => Rot(o, 90)),
                At("arch.furniture.sofa", 82, 50),
                At("arch.table.round", 103, 64),
                At("arch.counter.corner", 139, 45),
                At("arch.furniture.stove", 148, 45),
                At("arch.furniture.fridge", 154.5, 45),
                At("arch.furniture.bed", 103, 97),
                At("arch.furniture.wardrobe", 64.8, 95, o => Rot(o, 90)),
                At("arch.sanitary.bathtub", 148.5, 104),
                At("arch.sanitary.wc", 154.5, 82, o => Rot(o, 90)),
                At("arch.sanitary.washbasin", 136, 79),
                At("arch.hvac.radiator", 85, 43),
                At("arch.site.north", 185, 30),
                At("arch.site.level", 190, 60),
                At("arch.site.section", 0, 0, o => o with { Points = new[] { new Vec2(50, 90), new Vec2(170, 90) } }),
                At("arch.site.tree", 195, 95),
                At("arch.hvac.heatpump", 175, 70, o => Rot(o, 90)),
            };
            doc = Edits.Add(doc, doc.Pages[0].Id, objs.ToArray());
            return (r, doc);
        }
    }
}
