using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Box with one named pin "a" on the right edge, or on the left edge when <c>reverse</c> is set (polarity flip).</summary>
    internal sealed class PinModule : IModule
    {
        public string Id => "pin";
        public I18nText Name => new("Pins", "Pins");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => new ObjectType[] { new PinBox(), new TestModule.LineType() };
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();

        internal sealed class PinBox : ObjectType
        {
            public override string Id => "pin.box";
            public override I18nText Name => new("Pin box", "Pin-Kasten");
            public override Size2 DefaultSize => new(20, 10);
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H) };
            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                var x = o.Size.W / 2 * (o.Flag("reverse", false) ? -1 : 1);
                yield return Anchor.Point(AnchorRole.Port, new Vec2(x, 0), "a");
                if (!o.Flag("noB", false)) yield return Anchor.Point(AnchorRole.Port, new Vec2(0, o.Size.H / 2), "b");
            }
        }
    }

    public class SnappingRuleTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 2, Grid = null, Alignment = false, Page = false };

        private static void Near(Vec2 expected, Vec2 actual) => Assert.True(expected.DistanceTo(actual) < 1e-9, $"expected {expected}, got {actual}");

        private static Anchor P(AnchorRole role, double x, double y, string? id = null) => Anchor.Point(role, new Vec2(x, y), id);

        /// <summary>Generic anchors of a box centred at c (centre, corners, edge midpoints) plus extra anchors.</summary>
        private static List<Anchor> BoxAnchors(Vec2 c, double w, double h, params Anchor[] extra)
        {
            var list = new List<Anchor> { Anchor.Point(AnchorRole.Center, c) };
            foreach (var (u, v) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }) list.Add(Anchor.Point(AnchorRole.Corner, c + new Vec2(u * w / 2, v * h / 2)));
            foreach (var (u, v) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) }) list.Add(Anchor.Point(AnchorRole.EdgeMid, c + new Vec2(u * w / 2, v * h / 2)));
            list.AddRange(extra);
            return list;
        }

        // ------------------------------------------------------------------ rails

        [Fact]
        public void EndDocksAnywhereOnARail()
        {
            var rail = Anchor.Rail(new Vec2(0, 0), new Vec2(40, 0), "rail0");
            var r = Snapper.SnapPoint(new Vec2(17.3, 1.1), new[] { rail }, Opt);
            Assert.Equal("rail", r.Kind);
            Near(new Vec2(17.3, 0), new Vec2(17.3, 1.1) + r.Delta);
            Assert.Same(rail, r.Target);
            Assert.Contains(r.Guides, g => g.Kind == GuideKind.Segment);
            Assert.Contains(r.Guides, g => g.Kind == GuideKind.Point && g.A.DistanceTo(new Vec2(17.3, 0)) < 1e-9);
        }

        [Fact]
        public void PortDocksOnARailAndRailEndPointsActLikePorts()
        {
            var rail = Anchor.Rail(new Vec2(0, 0), new Vec2(0, 50));
            var moving = BoxAnchors(new Vec2(1.2, 20), 4, 4, P(AnchorRole.Port, 1.2, 20, "1"));
            var r = Snapper.SnapMove(moving, Vec2.Zero, new[] { rail }, Opt);
            Assert.Equal("rail", r.Kind);
            Near(new Vec2(-1.2, 0), r.Delta);

            // Close to the end of the rail the end point wins over the interior projection.
            var e = Snapper.SnapPoint(new Vec2(0.3, 0.6), new[] { rail }, Opt);
            Assert.Equal("rail", e.Kind);
            Near(Vec2.Zero, new Vec2(0.3, 0.6) + e.Delta);
        }

        [Fact]
        public void DiscretePortBeatsRailInterior()
        {
            var targets = new[] { Anchor.Rail(new Vec2(0, 0), new Vec2(40, 0)), P(AnchorRole.Port, 21, 0.5, "pin") };
            var r = Snapper.SnapPoint(new Vec2(21, -0.5), targets, Opt); // pin 1 mm away, rail 0.5 mm away
            Assert.Equal("port", r.Kind);
            Near(new Vec2(21, 0.5), new Vec2(21, -0.5) + r.Delta);
        }

        [Fact]
        public void RailsAndAxesKeepTheirOwnClients()
        {
            // A hub ignores rails; an end ignores shaft axes (Axis semantics unchanged).
            var hub = Snapper.SnapMove(new[] { P(AnchorRole.Hub, 10, 1) }, Vec2.Zero, new[] { Anchor.Rail(new Vec2(0, 0), new Vec2(40, 0)) }, Opt);
            Assert.Equal("none", hub.Kind);
            var end = Snapper.SnapPoint(new Vec2(10, 1), new[] { Anchor.Segment(AnchorRole.Axis, new Vec2(0, 0), new Vec2(40, 0)) }, Opt);
            Assert.Equal("none", end.Kind);
            var axis = Snapper.SnapMove(new[] { P(AnchorRole.Hub, 10, 1) }, Vec2.Zero, new[] { Anchor.Segment(AnchorRole.Axis, new Vec2(0, 0), new Vec2(40, 0)) }, Opt);
            Assert.Equal("axis", axis.Kind);
            Near(new Vec2(0, -1), axis.Delta);
        }

        // ------------------------------------------------------------------ gears and fits

        [Theory]
        [InlineData(0.8, -0.5)]
        [InlineData(-1.0, 0.6)]
        public void NearlyEqualCirclesSnapConcentricInsteadOfMeshing(double dx, double dy)
        {
            // Rotor (r 23) inside a stator bore (r 24): the internal-mesh offset would be 1 mm, but this is a fit.
            var moving = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(dx, dy), 23), P(AnchorRole.Hub, dx, dy) };
            var targets = new[] { Anchor.Circle(AnchorRole.PitchInner, Vec2.Zero, 24), P(AnchorRole.Hub, 0, 0) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("concentric", r.Kind);
            Assert.Equal(-dx, r.Delta.X, 9);
            Assert.Equal(-dy, r.Delta.Y, 9);
        }

        [Fact]
        public void ClearlySmallerPlanetStillMeshesInsideRing()
        {
            // Ratio 0.875 (< InternalMeshMaxRatio): a real internal gear pair, also with circle and hub anchors.
            var moving = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(0, -4.6), 35), P(AnchorRole.Hub, 0, -4.6) };
            var targets = new[] { Anchor.Circle(AnchorRole.PitchInner, Vec2.Zero, 40), P(AnchorRole.Hub, 0, 0) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt with { Tolerance = 5 });
            Assert.Equal("mesh", r.Kind);
            Assert.Equal(-5, (new Vec2(0, -4.6) + r.Delta).Y, 9);
            Assert.True(35 / 40.0 < Snapper.InternalMeshMaxRatio);
        }

        [Fact]
        public void ExternalMeshIsUnaffectedByTheFitRule()
        {
            var moving = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(0, 0), 20) };
            var targets = new[] { Anchor.Circle(AnchorRole.Pitch, new Vec2(40.8, 0), 21) };
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("mesh", r.Kind);
            Assert.Equal(-0.2, r.Delta.X, 9);
        }

        // ------------------------------------------------------------------ priorities

        [Fact]
        public void PinOfATinyObjectBeatsItsCloserCorner()
        {
            // 2 x 2 mm junction dot with its pin in the centre; a wire end lies 0.3 mm from a corner but 1.8 mm from the pin.
            var dot = BoxAnchors(Vec2.Zero, 2, 2, P(AnchorRole.Port, 0, 0, "1"));
            var wireEnd = P(AnchorRole.End, 1.3, 1.1);
            var r = Snapper.SnapMove(dot, Vec2.Zero, new[] { wireEnd }, Opt);
            Assert.Equal("port", r.Kind);
            Near(new Vec2(1.3, 1.1), r.Delta);

            // A free end dragged near the dot docks on its pin, not on the closer corner.
            var targets = BoxAnchors(new Vec2(50, 50), 2, 2, P(AnchorRole.Port, 50, 50, "1"));
            var p = new Vec2(51.2, 51.1);
            var s = Snapper.SnapPoint(p, targets, Opt);
            Assert.Equal("port", s.Kind);
            Near(new Vec2(50, 50), p + s.Delta);
        }

        [Fact]
        public void GenericPointSnapsStillWorkWithoutSemanticCandidates()
        {
            var moving = BoxAnchors(new Vec2(10, 10), 10, 10);
            var targets = BoxAnchors(new Vec2(20.6, 10.4), 10, 10);
            var r = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("point", r.Kind);
            Near(new Vec2(0.6, 0.4), r.Delta);
        }

        [Fact]
        public void MountSnapsOntoMountLikeAPort()
        {
            // The sensor's corner is closer to a vehicle corner, but the mount wins.
            var sensor = BoxAnchors(new Vec2(4, 0), 8, 6, P(AnchorRole.Mount, 0, 0, "mount"));
            var vehicle = BoxAnchors(new Vec2(-22.2, 0.9), 44, 18, P(AnchorRole.Mount, -1.2, 1.4, "mount.front"));
            var r = Snapper.SnapMove(sensor, Vec2.Zero, vehicle, Opt);
            Assert.Equal("mount", r.Kind);
            Near(new Vec2(-1.2, 1.4), r.Delta);
            Assert.Equal("mount.front", r.Target!.Id);

            // Mounts and ports do not mix.
            var cross = Snapper.SnapMove(new[] { P(AnchorRole.Mount, 0, 0) }, Vec2.Zero, new[] { P(AnchorRole.Port, 1, 0) }, Opt);
            Assert.Equal("point", cross.Kind);
        }

        [Fact]
        public void PortsStillSnapPortToPortAndToEnds()
        {
            var r = Snapper.SnapMove(new[] { P(AnchorRole.Port, 0, 0) }, Vec2.Zero, new[] { P(AnchorRole.End, 1, 1), P(AnchorRole.Corner, 0.2, 0) }, Opt);
            Assert.Equal("port", r.Kind);
            Near(new Vec2(1, 1), r.Delta);
        }

        // ------------------------------------------------------------------ targets and attachments

        private static (Registry R, SketchDocument Doc, SketchObject Box, SketchObject Line) PinDoc()
        {
            var r = new Registry().Register(new PinModule());
            var doc = SketchDocument.Create();
            var box = r.Type("pin.box")!.Create(new Vec2(50, 50));
            var line = r.Type("test.line")!.Create(new Vec2(100, 80));
            doc = Edits.Add(doc, doc.Pages[0].Id, box, line);
            return (r, doc, box, line);
        }

        [Fact]
        public void TargetsCarryOwnerAndSnapBuildsAnAttachmentWithThePinId()
        {
            var (r, doc, box, line) = PinDoc();
            var ctx = new RenderContext(doc);
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { line.Id }, ctx, Opt);
            Assert.All(targets, t => Assert.Equal(box.Id, t.Owner));
            var p = new Vec2(60.7, 50.6);
            var res = Snapper.SnapPoint(p, targets, Opt);
            Assert.Equal("port", res.Kind);
            var q = p + res.Delta;
            Near(new Vec2(60, 50), q);
            var att = Attachments.FromSnap(doc, q, res)!;
            Assert.Equal(box.Id, att.ObjectId);
            Assert.Equal("a", att.AnchorId);
            Assert.Equal(0.5, att.U, 9);

            // Grid snaps and page anchors give no attachment.
            Assert.Null(Attachments.FromSnap(doc, q, SnapResult.None(Vec2.Zero)));
        }

        [Fact]
        public void RefreshMovesPinAttachmentsAfterTheTargetChanged()
        {
            var (r, doc, box, line) = PinDoc();
            doc = Edits.AttachPoint(doc, line.Id, 1, box.Id, new Vec2(60, 50), "a");
            doc = Edits.AttachPoint(doc, line.Id, 0, box.Id, new Vec2(40, 45)); // plain corner attachment, no id
            Assert.Same(doc, Attachments.Refresh(r, doc));

            // Polarity flip: pin "a" moves to the left edge; the bound end follows, the plain attachment does not move.
            var flipped = Edits.Update(doc, new[] { box.Id }, o => o.WithParam("reverse", true));
            Near(new Vec2(60, 50), flipped.ResolvePoints(flipped.Get(line.Id)!)[1]);
            var refreshed = Attachments.Refresh(r, flipped, new RenderContext(flipped));
            var l = refreshed.Get(line.Id)!;
            Near(new Vec2(40, 50), refreshed.ResolvePoints(l)[1]);
            Near(new Vec2(40, 50), l.Points![1]);
            Assert.Equal(-0.5, l.Attach![1]!.U, 9);
            Assert.Equal("a", l.Attach[1]!.AnchorId);
            Near(new Vec2(40, 45), refreshed.ResolvePoints(l)[0]);
            Assert.Same(refreshed, Attachments.Refresh(r, refreshed));

            // Size change: the pin stays on the edge midpoint (it did before as well, but via its id now).
            var wider = Attachments.Refresh(r, Edits.Update(refreshed, new[] { box.Id }, o => o with { Size = new Size2(30, 10) }));
            Near(new Vec2(35, 50), wider.ResolvePoints(wider.Get(line.Id)!)[1]);
        }

        [Fact]
        public void RefreshKeepsAttachmentsWhosePinDisappeared()
        {
            var (r, doc, box, line) = PinDoc();
            doc = Edits.AttachPoint(doc, line.Id, 0, box.Id, new Vec2(50, 55), "b");
            var without = Edits.Update(doc, new[] { box.Id }, o => o.WithParam("noB", true).WithParam("reverse", true));
            var refreshed = Attachments.Refresh(r, without);
            Assert.Same(without, refreshed);
            var att = refreshed.Get(line.Id)!.Attach![0]!;
            Assert.Equal("b", att.AnchorId);
            Near(new Vec2(50, 55), refreshed.ResolvePoints(refreshed.Get(line.Id)!)[0]);
        }

        [Fact]
        public void RailRoleRoundTripsAsText()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(AnchorRole.Rail);
            Assert.Equal("\"Rail\"", json);
        }
    }
}
