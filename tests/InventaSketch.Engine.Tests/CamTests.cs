using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Cam discs as parts and their followers (<see cref="Cams"/>), against the predefined cam mechanism.</summary>
    public class CamTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static (SketchDocument Doc, string Cam, string Slider) Exploded(string follower, double angle)
        {
            var item = Reg.Library.First(i => i.TypeId == "kin.cam.follower");
            var mech = Reg.Create(item, new Vec2(100, 100)).WithParam("follower", follower).WithParam("angle", angle);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, mech);
            Assert.True(Assemblies.CanExplode(Reg, mech));
            var parts = Assemblies.Explode(Reg, doc, mech.Id);
            var ex = SketchDocument.Create();
            ex = Edits.Add(ex, ex.Pages[0].Id, parts.ToArray());
            return (ex, parts.Single(p => p.Type == "kin.cam.disc").Id, parts.Single(p => p.Type == "kin.joint.prismatic").Id);
        }

        private static Vec2 Joint(SketchDocument doc, string slider)
            => Reg.PageAnchors(doc.Get(slider)!, new RenderContext(doc)).First(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port).P;

        [Theory]
        [InlineData("roller")]
        [InlineData("flat")]
        [InlineData("knife")]
        public void TurningTheCamDiscMovesTheFollowerLikeTheMechanism(string follower)
        {
            var (doc, cam, slider) = Exploded(follower, 0);
            Assert.True(Cams.IsCam(Reg, doc.Get(cam)!));
            Assert.Single(Cams.Followers(Reg, doc, cam));
            foreach (var deg in new[] { 40.0, 110.0, 200.0 })
            {
                var turned = Cams.Turn(Reg, doc, cam, deg);
                var (want, _, wantSlider) = Exploded(follower, deg);
                Assert.Equal(Joint(want, wantSlider).Y, Joint(turned, slider).Y, 1);
                Assert.Equal(Joint(doc, slider).X, Joint(turned, slider).X, 6); // stays on its guide
            }
        }

        [Fact]
        public void MotionModeTurnsACamDiscWithItsFollower()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var item = api.Registry.Library.First(i => i.TypeId == "kin.cam.follower").Id;
            var mech = EngineApiTests.Insert(api, session, item, 100, 80);
            EngineApiTests.Call(api, "explode", new { session, id = mech, ids = new[] { mech } });
            var doc = api.Sessions.Get(session).Doc;
            var cam = doc.Objects.Values.Single(o => o.Type == "kin.cam.disc").Id;
            var slider = doc.Objects.Values.Single(o => o.Type == "kin.joint.prismatic").Id;
            var info = EngineApiTests.Call(api, "motionInfo", new { session, id = cam });
            Assert.Equal("gear", info.GetProperty("kind").GetString());
            Assert.True(info.GetProperty("coupled").GetBoolean());
            var before = Joint(api.Sessions.Get(session).Doc, slider);
            EngineApiTests.Call(api, "motionTurn", new { session, id = cam, degrees = 45 });
            var after = api.Sessions.Get(session).Doc;
            Assert.Equal(45, after.Get(cam)!.Transform.Rotation, 6);
            // Nose straight up: the follower rises by the full lift (7 at the mechanism's scale).
            Assert.True(before.Y - Joint(after, slider).Y > 2, "the follower rises");
        }
    }
}
