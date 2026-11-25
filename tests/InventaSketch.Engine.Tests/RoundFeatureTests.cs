using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Features that are round by nature (rolling elements, bores, holes, pins, pivots, hub and shaft circles) stay round
    /// when a part is stretched non-uniformly; only the outline stretches (Quick Sketch: free scaling changes Size).
    /// </summary>
    public class RoundFeatureTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        /// <summary>Modules with mechanical parts: mechanical, kinematics, machines, robotics.</summary>
        private static readonly string[] Prefixes = { "mech.", "kin.", "machines.", "robot." };

        /// <summary>Primitive tags of features that are round by nature.</summary>
        private static readonly HashSet<string> RoundTags = new(StringComparer.Ordinal)
        {
            "rolling", "ball", "roller", "bore", "hole", "pin", "output-pin", "boss", "hub", "shaft", "bearing", "pivot",
            "actuator", "eccentric", "minor", "spider", "knob", "spring", "joint",
        };

        private static List<Primitive> Render(SketchObject o)
        {
            var empty = SketchDocument.Create();
            var doc = Edits.Add(empty, empty.Pages[0].Id, o);
            return Prim.Flatten(Reg.Render(o, new RenderContext(doc))).ToList();
        }

        /// <summary>
        /// Every library item of those modules, rendered at (2W, H) and (W, 2H): each circle tagged as a round feature at the
        /// default size is still a circle (Rx = Ry). (Box-stretched symbols size it from the smaller stretch, see the bearing
        /// test below; mechanisms laid out with one uniform model scale size it from that scale.)
        /// </summary>
        [Fact]
        public void RoundFeaturesStayRoundUnderNonUniformScaling()
        {
            var checkedCircles = 0;
            var bad = new List<string>();
            foreach (var item in Reg.Library.Where(i => Prefixes.Any(p => i.TypeId.StartsWith(p, StringComparison.Ordinal))))
            {
                var o = Reg.Create(item, new Vec2(100, 100));
                if (o.IsPointGeometry) continue;
                var at = Render(o);
                foreach (var size in new[] { new Size2(o.Size.W * 2, o.Size.H), new Size2(o.Size.W, o.Size.H * 2) })
                {
                    var st = Render(o with { Size = size });
                    // The same drawing at another size: primitives correspond by order (fluid views may add strokes).
                    if (st.Count != at.Count) continue;
                    for (var i = 0; i < at.Count; i++)
                    {
                        if (at[i] is not EllipsePrim e0 || Math.Abs(e0.Rx - e0.Ry) > 1e-6 || at[i].Tag is not { } tag || !RoundTags.Contains(tag)) continue;
                        checkedCircles++;
                        if (st[i] is not EllipsePrim e1 || Math.Abs(e1.Rx - e1.Ry) > 1e-6)
                            bad.Add(item.Id + " [" + (item.View ?? "") + "] " + tag + " at " + size.W + " x " + size.H);
                    }
                }
            }
            Assert.True(checkedCircles > 100, "the audit sees the round features (" + checkedCircles + ")");
            Assert.True(bad.Count == 0, "Round features drawn as ellipses:\n" + string.Join("\n", bad.Distinct()));
        }

        [Fact]
        public void BallsOfAStretchedBearingAreCirclesInsideTheRings()
        {
            var bearing = Reg.Create(Reg.Library.First(i => i.TypeId == "mech.bearing.ball" && i.View == "detailed"), new Vec2(50, 50));
            var r0 = Render(bearing).Where(p => p.Tag == "rolling").Cast<EllipsePrim>().First().Rx;
            foreach (var size in new[] { new Size2(bearing.Size.W * 3, bearing.Size.H), new Size2(bearing.Size.W, bearing.Size.H * 2) })
            {
                var prims = Render(bearing with { Size = size });
                var balls = prims.Where(p => p.Tag == "rolling").Cast<EllipsePrim>().ToList();
                Assert.Equal(2, balls.Count);
                foreach (var b in balls)
                {
                    Assert.Equal(b.Rx, b.Ry, 9);
                    // Sized from the smaller stretch (here 1): as at the default size.
                    Assert.Equal(r0, b.Rx, 9);
                    // The ball sits in the gap between the rings: the rings' bites follow the ball (round, same radius).
                    var rings = prims.Where(p => p.Tag is "outer-ring" or "inner-ring").OfType<PathPrim>().ToList();
                    Assert.Equal(4, rings.Count);
                    var bites = rings.Where(r => r.Segments.Count(s => s.Op != SegOp.Z && Math.Abs(s.P.DistanceTo(b.Center) - b.Rx) < 1e-6) > 10).ToList();
                    Assert.Equal(2, bites.Count);
                }
            }
        }
    }
}
