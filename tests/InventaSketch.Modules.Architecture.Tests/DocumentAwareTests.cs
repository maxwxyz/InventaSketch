using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    /// <summary>Align to wall, dependents for redraw, document plan scale, input actions and view sizes of openings.</summary>
    public class DocumentAwareTests
    {
        private static ContextAction Action(Registry r, SketchDocument doc, SketchObject o, string id)
            => r.TypeOf(o).Actions(o, new RenderContext(doc)).Single(a => a.Id == id);

        [Fact]
        public void DoorAlignsToTheWallSegmentItSitsOn()
        {
            var r0 = Fx.Reg();
            // Wall rising at 30 degrees (clockwise on screen); the door is placed roughly on it, unrotated.
            var dir = new Vec2(Math.Cos(Geo.DegToRad(30)), Math.Sin(Geo.DegToRad(30)));
            var wall = Fx.Wall(r0, new Vec2(0, 0), dir * 80);
            var door = r0.Create(r0.Item("arch.door.single")!, dir * 40 + dir.Perp * 1.0);
            var (r, doc) = Fx.Doc(wall, door);
            var align = Action(r, doc, door, "arch.align");
            Assert.True(align.IsEnabled);
            Assert.NotNull(align.ApplyInDocument);

            var after = ContextActions.Run(r, doc, new[] { door.Id }, "arch.align", null, Lang.En);
            var d = after.Get(door.Id)!;
            Assert.Equal(30, d.Transform.Rotation, 6);
            Assert.Equal(40, new Vec2(d.Transform.X, d.Transform.Y).DistanceTo(Vec2.Zero), 6);     // centred on the axis
            Assert.Equal(0, Arch.OpeningWidth(d) - Arch.OpeningWidth(door), 9);                    // size untouched
            Assert.Same(doc.Get(wall.Id), after.Get(wall.Id));

            // A door turned the other way round keeps its side: 210 degrees is closer to 180 than 30.
            var turned = door with { Transform = door.Transform with { Rotation = 180 } };
            var doc2 = Edits.Replace(doc, turned);
            var d2 = ContextActions.Run(r, doc2, new[] { door.Id }, "arch.align", null, Lang.En).Get(door.Id)!;
            Assert.Equal(-150, d2.Transform.Rotation, 6);
        }

        [Fact]
        public void AlignIsGreyedOutWithoutAWall()
        {
            var r0 = Fx.Reg();
            var window = r0.Create(r0.Item("arch.window.single")!, new Vec2(10, 10));
            var (r, doc) = Fx.Doc(window);
            Assert.False(Action(r, doc, window, "arch.align").IsEnabled);
            Assert.Same(doc, ContextActions.Run(r, doc, new[] { window.Id }, "arch.align", null, Lang.En));
            // Far away from a wall is the same as no wall.
            var wall = Fx.Wall(r0, new Vec2(0, 50), new Vec2(60, 50));
            var (r2, doc2) = Fx.Doc(window, wall);
            Assert.False(Action(r2, doc2, window, "arch.align").IsEnabled);
        }

        [Fact]
        public void WallDependentsAreJoinedWallsAndOpeningsOnIt()
        {
            var r0 = Fx.Reg();
            var main = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0));
            var corner = Fx.Wall(r0, new Vec2(60, 0), new Vec2(60, 40));          // L joint at the end
            var tee = Fx.Wall(r0, new Vec2(30, 0), new Vec2(30, 30));             // T joint against the face
            var loose = Fx.Wall(r0, new Vec2(0, 80), new Vec2(60, 80));
            var door = r0.Create(r0.Item("arch.door.single")!, new Vec2(15, 0));
            var lamp = r0.Create(r0.Item("arch.window.single")!, new Vec2(15, 40)); // not on the wall
            var (r, doc) = Fx.Doc(main, corner, tee, loose, door, lamp);
            var ctx = new RenderContext(doc);
            var deps = r.TypeOf(main).Dependents(main, ctx).OrderBy(x => x).ToList();
            Assert.Equal(new[] { corner.Id, tee.Id, door.Id }.OrderBy(x => x), deps);
            Assert.True(r.TypeOf(main).DependsOnNeighbours);
            // The butting wall depends on the wall it butts against and vice versa.
            Assert.Contains(main.Id, r.TypeOf(tee).Dependents(tee, ctx));
            // Openings read the wall but nothing depends on them.
            Assert.True(r.TypeOf(door).DependsOnNeighbours);
            Assert.Empty(r.TypeOf(door).Dependents(door, ctx));
            Assert.False(r.Type("arch.furniture.bed")!.DependsOnNeighbours);
        }

        [Fact]
        public void DocumentPlanScaleAppliesWhenTheObjectHasNoOwnScale()
        {
            var r0 = Fx.Reg();
            var wall = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0)).WithParam("thickness", 240.0);
            Assert.False(wall.Params.ContainsKey("scale"));                         // new objects carry no scale of their own
            var (r, doc) = Fx.Doc(wall);
            double Thick(SketchDocument d) => Prim.Bounds(r.Render(d.Get(wall.Id)!, new RenderContext(d)).Where(p => p.Tag == ArchTags.WallFill).ToList())!.Value.H;
            Assert.Equal(2.4, Thick(doc), 6);
            var at50 = doc with { Settings = doc.Settings with { PlanScale = 50 } };
            Assert.Equal(4.8, Thick(at50), 6);
            // An own scale wins over the document scale.
            var own = Edits.Replace(at50, wall.WithParam("scale", 100.0));
            Assert.Equal(2.4, Thick(own), 6);
            Assert.True(r.TypeOf(wall).UsesPlanScale);
            Assert.True(r.Type("arch.door.single")!.UsesPlanScale);
            Assert.False(r.Type("arch.furniture.bed")!.UsesPlanScale);

            // Opening widths and room areas follow as well.
            var door = r0.Create(r0.Item("arch.door.single")!, new Vec2(100, 100));
            Assert.Equal(450, Arch.OpeningWidth(door, at50), 6);
            Assert.Equal("450", r.TypeOf(door).Actions(door, new RenderContext(at50)).Single(a => a.Id == "arch.width.set").Input!.Value);
            Assert.Contains("575 mm", r.TypeOf(door).Actions(door, new RenderContext(at50)).Single(a => a.Id == "arch.width.inc").Label.En);
        }

        [Fact]
        public void InputActionsSetWidthThicknessAndRoomName()
        {
            var r0 = Fx.Reg();
            var door = r0.Create(r0.Item("arch.door.single")!, new Vec2(100, 100));
            var wall = Fx.Wall(r0, new Vec2(0, 0), new Vec2(60, 0));
            var room = r0.Create(r0.Item("arch.room.kitchen")!, new Vec2(50, 50));
            var (r, doc) = Fx.Doc(door, wall, room);

            var width = Action(r, doc, door, "arch.width.set");
            Assert.Equal("900", width.Input!.Value);
            Assert.Equal("mm", width.Input.Unit);
            Assert.Equal("Öffnungsbreite (mm)", width.Input.Label.De);
            var wider = ContextActions.Run(r, doc, new[] { door.Id }, "arch.width.set", "1,01 m", Lang.De).Get(door.Id)!;
            Assert.Equal(1010, Arch.OpeningWidth(wider), 6);
            Assert.Equal(door.Size.H + 2 * 1.1, wider.Size.H, 6);                // the leaf deepens with the width
            Assert.Throws<FormatException>(() => ContextActions.Run(r, doc, new[] { door.Id }, "arch.width.set", "wide", Lang.En));

            Assert.Equal("365", Action(r, doc, wall, "arch.thickness.set").Input!.Value);
            Assert.Equal(175, ContextActions.Run(r, doc, new[] { wall.Id }, "arch.thickness.set", "175 mm", Lang.En).Get(wall.Id)!.Num("thickness", 0));

            Assert.Equal("Kitchen", Action(r, doc, room, "arch.room.name").Input!.Value);
            Assert.Equal("Küche 1", ContextActions.Run(r, doc, new[] { room.Id }, "arch.room.name", " Küche 1 ", Lang.De).Get(room.Id)!.Text);
        }

        [Fact]
        public void OpeningsKeepTheirWidthWhenSwitchingToTheElevation()
        {
            var r = Fx.Reg();
            var t = r.Type("arch.window.single")!;
            var win = r.Create(r.Item("arch.window.single")!, new Vec2(50, 50));
            var ctx = new RenderContext(SketchDocument.Create());
            var elev = t.WithView(win, "elevation", ctx);
            Assert.Equal(new Size2(10, 12.5), elev.Size);                         // 1.00 m x 1.25 m at 1:100
            Assert.Equal(win.Size, t.WithView(elev, "plan", ctx).Size);

            var door = r.Type("arch.door.single")!;
            var d = r.Create(r.Item("arch.door.single")!, Vec2.Zero);
            var wide = d with { Size = new Size2(11, d.Size.H + 2 * 2) };          // widened by 2 mm with the leaf
            var de = door.WithView(wide, "elevation", ctx);
            Assert.Equal(new Size2(11, 21), de.Size);
            Assert.Equal(wide.Size.W, door.WithView(de, "plan", ctx).Size.W, 9);
            Assert.Equal(wide.Size.H, door.WithView(de, "plan", ctx).Size.H, 9);

            // At 1:50 the elevation height doubles, the width stays.
            var at50 = new RenderContext(SketchDocument.Create() with { Settings = new DocumentSettings { PlanScale = 50 } });
            Assert.Equal(new Size2(10, 25), t.WithView(win, "elevation", at50).Size);
        }
    }
}
