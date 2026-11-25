using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Review of every library element with "+" (build ports and quick add), scaled by 1.5 before: each fitting part is
    /// placed, takes over the scale and docks (axis ends meet, gears stay in mesh). Lists every element that fails.
    /// </summary>
    public class BuildAllElementsTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public BuildAllElementsTests(ITestOutputHelper output) => _out = output;

        private const double K = 1.5;

        private static (SketchDocument Doc, SketchObject Obj)? Scaled(LibraryItem item)
        {
            var o = Reg.Create(item, new Vec2(500, 400));
            if (o.IsPointGeometry) return null;
            o = o with { Size = new Size2(o.Size.W * K, o.Size.H * K) };
            var doc = SketchDocument.Create(new Size2(1000, 800));
            return (Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        private static double? Pitch(SketchDocument doc, SketchObject o)
            => Reg.PageAnchors(o, new RenderContext(doc)).FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch)?.Radius;

        [Fact]
        public void EveryBuildPortTakesAScaledFittingPart()
        {
            var failures = new List<string>();
            var checkedPlacements = 0;
            foreach (var item in Reg.Library)
            {
                if (Scaled(item) is not { } s) continue;
                var (doc, o) = s;
                IReadOnlyList<BuildPort> ports;
                try { ports = Building.Ports(Reg, doc, o.Id); }
                catch (Exception ex) { failures.Add($"{item.Id}: ports throw {ex.GetType().Name}: {ex.Message}"); continue; }
                foreach (var port in ports.Where(p => p.Kind is PortKind.Mesh or PortKind.InnerMesh or PortKind.AxisEnd or PortKind.Bevel))
                {
                    foreach (var fit in Building.Items(Reg, doc, o.Id, port, 3))
                    {
                        SketchObject? placed;
                        try { placed = Building.Place(Reg, doc, o.Id, port, fit); }
                        catch (Exception ex) { failures.Add($"{item.Id} {port.Id} + {fit.Id}: throws {ex.GetType().Name}: {ex.Message}"); continue; }
                        if (placed is null) { failures.Add($"{item.Id} {port.Id} + {fit.Id}: offered but not placed"); continue; }
                        checkedPlacements++;
                        var after = Edits.Add(doc, doc.Pages[0].Id, placed);
                        // Scale: the part is as much bigger than its library size as the element is.
                        if (Building.SourceScale(Reg, o) is { } so && Building.SourceScale(Reg, placed) is { } sp && port.Kind != PortKind.AxisEnd)
                        {
                            var want = Math.Sqrt(so.X * so.Y); var got = Math.Sqrt(sp.X * sp.Y);
                            if (Math.Abs(got / want - 1) > 0.02) failures.Add($"{item.Id} {port.Id} + {fit.Id}: scale {got:0.###}, element {want:0.###}");
                        }
                        switch (port.Kind)
                        {
                            case PortKind.Mesh:
                                if (Pitch(doc, o) is double r1 && Pitch(after, placed) is double r2)
                                {
                                    var d = new Vec2(o.Transform.X, o.Transform.Y).DistanceTo(new Vec2(placed.Transform.X, placed.Transform.Y));
                                    var centres = Reg.PageAnchors(placed, new RenderContext(after)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch).P
                                        .DistanceTo(Reg.PageAnchors(o, new RenderContext(doc)).First(a => a.Kind == AnchorKind.Circle && a.Role == AnchorRole.Pitch).P);
                                    if (Math.Abs(centres - (r1 + r2)) > 0.05) failures.Add($"{item.Id} {port.Id} + {fit.Id}: pitch circles {centres:0.##} apart, radii {r1:0.##}+{r2:0.##}");
                                }
                                break;
                            case PortKind.AxisEnd:
                                var axes = Reg.PageAnchors(placed, new RenderContext(after)).Where(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis).ToList();
                                var hubs = Reg.PageAnchors(placed, new RenderContext(after)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Hub).ToList();
                                var onAxis = axes.Any(a => a.P.DistanceTo(port.At) <= 0.5 || a.Q.DistanceTo(port.At) <= 0.5)
                                    || hubs.Any(h => Geo.ClosestOnSegment(h.P, port.AxisFrom, port.At).Point.DistanceTo(h.P) <= 0.5);
                                if (!onAxis) failures.Add($"{item.Id} {port.Id} + {fit.Id}: does not sit on the axis end");
                                break;
                        }
                    }
                }
            }
            foreach (var f in failures) _out.WriteLine(f);
            _out.WriteLine($"{checkedPlacements} placements checked, {failures.Count} failure(s)");
            Assert.True(checkedPlacements > 50, "too few placements: " + checkedPlacements);
            Assert.Empty(failures);
        }

        [Fact]
        public void EveryQuickAddKeepsTheScaleOfTheChain()
        {
            var failures = new List<string>();
            var n = 0;
            foreach (var item in Reg.Library)
            {
                if (Scaled(item) is not { } s || !Flow.CanChain(Reg, s.Obj)) continue;
                foreach (var next in Flow.NextItems(Reg, s.Obj, 3))
                {
                    var r = Flow.QuickAdd(Reg, s.Doc, s.Obj.Id, FlowSide.Right, itemId: next.Id);
                    var added = r.Doc.Get(r.ObjectId)!;
                    n++;
                    if (Building.SourceScale(Reg, s.Obj) is { } so && Building.SourceScale(Reg, added) is { } sa
                        && Math.Abs(Math.Sqrt(sa.X * sa.Y) / Math.Sqrt(so.X * so.Y) - 1) > 0.02)
                        failures.Add($"{item.Id} + {next.Id}: scale {Math.Sqrt(sa.X * sa.Y):0.###}");
                }
            }
            foreach (var f in failures) _out.WriteLine(f);
            _out.WriteLine($"{n} quick adds checked");
            Assert.True(n > 10, "too few quick adds: " + n);
            Assert.Empty(failures);
        }
    }
}
