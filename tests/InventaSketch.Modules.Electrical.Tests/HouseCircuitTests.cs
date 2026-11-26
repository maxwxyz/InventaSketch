using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>
    /// The installation symbols are the canonical room devices: room, circuit ("fed from") and phase are optional
    /// parameters with the names of the switchgear room devices (whose reports and circuit values read them), the switch
    /// offers every kind of the switchgear light switch (intermediate and dimmer included) with drawn option pictograms.
    /// </summary>
    public class HouseCircuitTests
    {
        private static readonly Registry R = Fx.Reg();

        [Theory]
        [InlineData("elec.house.socket", true)]
        [InlineData("elec.house.switch", false)]
        [InlineData("elec.house.light", true)]
        public void RoomCircuitAndPhaseAreOptionalParameters(string typeId, bool load)
        {
            var t = R.Type(typeId)!;
            Assert.Equal(ParamKind.Text, t.Parameters.Single(p => p.Key == "room").Kind);
            Assert.Equal(ParamKind.Text, t.Parameters.Single(p => p.Key == "feed").Kind);
            var phase = t.Parameters.SingleOrDefault(p => p.Key == "phase");
            Assert.Equal(load, phase is not null);
            if (phase is not null) Assert.Equal(new[] { "circuit", "L1", "L2", "L3" }, phase.Options!.Select(o => o.Value));
            if (load)
            {
                Assert.Contains(t.Parameters, p => p.Key == "power");
                Assert.Contains(t.Parameters, p => p.Key == "current");
            }
            // Quick Sketch: every item inserts without them, and nothing extra is drawn.
            foreach (var item in R.Library.Where(i => i.TypeId == typeId))
            {
                var o = R.Create(item, Vec2.Zero);
                Assert.True(o.Str("room", "") == "" && o.Str("feed", "") == "" && o.Str("phase", "circuit") == "circuit", item.Id);
                Assert.DoesNotContain(Fx.Render(R, o), p => p.Tag == "circuit");
            }
        }

        [Fact]
        public void RoomAndCircuitAreTypedFromTheBarAndTheCircuitShowsOnThePlan()
        {
            var o = R.Create(R.Item("elec.house.socket.double")!, Vec2.Zero);
            var actions = R.Type("elec.house.socket")!.Actions(o, Lang.De).ToList();
            var room = actions.Single(a => a.Id == "elec.house.room");
            var feed = actions.Single(a => a.Id == "elec.house.feed");
            Assert.Equal("Raum …", room.Label.Get(Lang.De));
            var placed = feed.Input!.Apply(room.Input!.Apply(o, " Kitchen "), " -F3 ");
            Assert.Equal("Kitchen", placed.Str("room", ""));
            Assert.Equal("-F3", placed.Str("feed", ""));
            Assert.Contains(Fx.Render(R, placed), p => p is TextPrim { Text: "-F3" } && p.Tag == "circuit");
            // The phase is a choice of the loads (socket, luminaire), not of the switch.
            Assert.Contains(actions, a => a.Choice?.Key == "elec.house.phase");
            var sw = R.Create(R.Item("elec.house.switch")!, Vec2.Zero);
            Assert.DoesNotContain(R.Type("elec.house.switch")!.Actions(sw, Lang.En), a => a.Choice?.Key == "elec.house.phase");
        }

        [Fact]
        public void TheSwitchOffersIntermediateAndDimmerWithDrawnPictograms()
        {
            var t = R.Type("elec.house.switch")!;
            var o = R.Create(R.Item("elec.house.switch")!, Vec2.Zero);
            var options = t.Actions(o, Lang.De).Where(a => a.Choice?.Key == "elec.house.switch").ToList();
            Assert.Equal(new[] { "oneway", "twoway", "intermediate", "push", "dimmer" }, options.Select(a => ActionChoices.ValueOf(a.Id)));
            Assert.Equal("Kreuzschalter", options[2].Label.Get(Lang.De));
            Assert.All(options, a => Assert.NotEmpty(a.Preview!));
            // Each kind draws its own pictogram and its own plan symbol.
            Assert.Equal(options.Count, options.Select(a => string.Join(";", a.Preview!.Select(p => p.ToString()))).Distinct().Count());
            Assert.Equal("dimmer", options[4].Apply(o).Str("kind", ""));
        }

        [Theory]
        [InlineData("intermediate")]
        [InlineData("dimmer")]
        public void IntermediateAndDimmerRenderInEveryViewInsideTheBox(string kind)
        {
            var t = R.Type("elec.house.switch")!;
            var oneway = R.Create(R.Item("elec.house.switch")!, Vec2.Zero);
            var o = oneway.WithParam("kind", kind);
            foreach (var view in t.Views)
            {
                var v = R.WithView(o, view.Id, Fx.Ctx());
                var prims = Fx.Render(R, v);
                Fx.AssertInsideBox(prims, v.Size, kind + "/" + view.Id);
                var plain = Fx.Render(R, R.WithView(oneway, view.Id, Fx.Ctx()));
                Assert.NotEqual(plain.Count, prims.Count);
            }
            Assert.Contains(Fx.Render(R, o), p => p.Tag == (kind == "dimmer" ? "dimmer" : "lever"));
        }

        [Fact]
        public void TheNewParametersRoundTripThroughTheDocument()
        {
            var o = R.Create(R.Item("elec.house.light")!, Vec2.Zero) with { Id = "L1" };
            o = o.WithParam("room", "Hall").WithParam("feed", "-F5").WithParam("phase", "L3");
            var (doc, _) = Fx.Doc(o);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc)).Get("L1")!;
            Assert.Equal("Hall", back.Str("room", ""));
            Assert.Equal("-F5", back.Str("feed", ""));
            Assert.Equal("L3", back.Str("phase", ""));
        }
    }
}
