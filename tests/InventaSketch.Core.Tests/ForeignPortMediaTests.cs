using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Lines of different media that meet only at a port of another module's element (engineering-calculations.md
    /// section 6.1): they are not joined into one net there, and only the leading medium joins that port's values.
    /// Module "a" (elements a.el with port "p", lines a.line with a medium) meets element "f" of module "b" at its port "q".
    /// </summary>
    public class ForeignPortMediaTests
    {
        private static NetSpec Spec(System.Func<string, bool>? leads = null) => new()
        {
            Prefix = "a",
            IsLine = o => o.Type == "a.line",
            PortVars = (o, port) => o.Type == "a.el" && port == "p" ? new[] { new PortVar("T", "t") } : System.Array.Empty<PortVar>(),
            Classes = new[] { "T" },
            Rule = _ => NetRule.Equal,
            Medium = o => o.Str("medium", "flow"),
            Leads = leads,
        };

        private static SketchObject El(string id, string type) => new() { Id = id, Type = type };

        private static SketchObject Line(string id, string medium, string from)
            => new SketchObject
            {
                Id = id, Type = "a.line",
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(from, 0, 0) { AnchorId = "p" }, new Attachment("f", 0, 0) { AnchorId = "q" } },
            }.WithParam("medium", medium);

        private static SketchDocument Doc(string medium1, string medium2)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, El("e1", "a.el"), El("e2", "a.el"), El("f", "b.el"), Line("l1", medium1, "e1"), Line("l2", medium2, "e2"));
        }

        private static bool Linked(ValueNets nets, string x, string y)
            => nets.For(x).Any(b => b.Map.Values.Any(v => v.ObjectId == y));

        [Fact]
        public void DifferentMediaAtOneForeignPortAreTwoNetsWithoutAJoin()
        {
            var nets = ValueNets.Of(Doc("flow", "return"), Spec());
            Assert.False(Linked(nets, "e1", "e2"));
            // No leading medium: neither net is joined with the foreign port.
            Assert.Empty(nets.Junctions);
        }

        [Fact]
        public void TheLeadingMediumAloneJoinsTheForeignPort()
        {
            var nets = ValueNets.Of(Doc("flow", "return"), Spec(m => m == "flow"));
            Assert.False(Linked(nets, "e1", "e2"));
            var j = Assert.Single(nets.Junctions);
            Assert.Equal(new[] { new VarRef("e1", "t") }, j.Elements);
            Assert.Equal(new[] { ("f", "q") }, j.ForeignPorts);
        }

        [Fact]
        public void TheSameMediumStillMeetsAtTheForeignPort()
        {
            var nets = ValueNets.Of(Doc("flow", "flow"), Spec());
            Assert.True(Linked(nets, "e1", "e2"));
            var j = Assert.Single(nets.Junctions);
            Assert.Equal(new[] { new VarRef("e1", "t"), new VarRef("e2", "t") }, j.Elements);
            Assert.Equal(new[] { ("f", "q") }, j.ForeignPorts);
        }

        [Fact]
        public void WithoutAMediumTheLineTypeDecides()
        {
            // Same line type and no medium function: one medium, as before.
            var spec = new NetSpec
            {
                Prefix = "a", IsLine = o => o.Type == "a.line", Classes = new[] { "T" }, Rule = _ => NetRule.Equal,
                PortVars = (o, port) => o.Type == "a.el" && port == "p" ? new[] { new PortVar("T", "t") } : System.Array.Empty<PortVar>(),
            };
            Assert.True(Linked(ValueNets.Of(Doc("flow", "return"), spec), "e1", "e2"));
        }

        [Fact]
        public void APortTheSpecDescribesIsAMemberAndJoinsAsBefore()
        {
            // The spec gives the foreign element a variable at the port: it is a member of one net, media do not matter.
            var spec = new NetSpec
            {
                Prefix = "a", IsLine = o => o.Type == "a.line", Classes = new[] { "T" }, Rule = _ => NetRule.Equal,
                Medium = o => o.Str("medium", "flow"),
                PortVars = (o, port) => (o.Type == "a.el" && port == "p") || (o.Type == "b.el" && port == "q") ? new[] { new PortVar("T", "t") } : System.Array.Empty<PortVar>(),
            };
            var nets = ValueNets.Of(Doc("flow", "return"), spec);
            Assert.True(Linked(nets, "e1", "f"));
            Assert.True(Linked(nets, "e2", "f") || Linked(nets, "e1", "e2"));
        }
    }
}
