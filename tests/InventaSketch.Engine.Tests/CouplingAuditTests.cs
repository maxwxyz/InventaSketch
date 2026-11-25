using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Coupling review of every library element (maintainer request 2026-11-25): a line docked on each port of an element
    /// follows when the element is moved, rotated or scaled; a moved element snaps its port onto a free line end.
    /// Elements in the same domain as a pair that looks alike are compared for the overlap review.
    /// </summary>
    public class CouplingAuditTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public CouplingAuditTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public void LinesDockedOnEveryPortFollowTheElement()
        {
            var line = Reg.Item("ann.line")!;
            var failures = new List<string>();
            int ports = 0, elements = 0;
            foreach (var item in Reg.Library)
            {
                var o = Reg.Create(item, new Vec2(500, 400));
                if (o.IsPointGeometry) continue;
                var doc = SketchDocument.Create(new Size2(1000, 800));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                var anchors = Reg.PageAnchors(o, new RenderContext(doc)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port).ToList();
                if (anchors.Count == 0) continue;
                elements++;
                foreach (var port in anchors)
                {
                    ports++;
                    var far = port.P + new Vec2(37, -23);
                    var l = Reg.Create(line, far) with { Points = new[] { far, port.P } };
                    l = l with { Attach = new[] { null, Attachments.For(o, port.P, port) } };
                    if (l.Attach![1] is null) { failures.Add($"{item.Id} {port.Id}: port cannot be attached to"); continue; }
                    var d = Edits.Add(doc, doc.Pages[0].Id, l);
                    // Move, rotate and scale the element; the docked end must stay on the same port.
                    foreach (var (what, change) in new (string, Func<SketchObject, SketchObject>)[]
                    {
                        ("move", x => x with { Transform = x.Transform with { X = x.Transform.X + 12.5, Y = x.Transform.Y - 7 } }),
                        ("rotate", x => x with { Transform = x.Transform with { Rotation = x.Transform.Rotation + 90 } }),
                        ("scale", x => x with { Size = new Size2(x.Size.W * 1.4, x.Size.H * 0.8) }),
                    })
                    {
                        try
                        {
                            var moved = Edits.Update(d, new[] { o.Id }, change);
                            moved = Attachments.Refresh(Reg, moved, new RenderContext(moved));
                            var mo = moved.Get(o.Id)!;
                            var want = Reg.PageAnchors(mo, new RenderContext(moved)).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id == port.Id);
                            var end = moved.ResolvePoints(moved.Get(l.Id)!)[1];
                            if (want is null)
                            {
                                // A port that depends on the size (ports along an activation bar) may go away: the end then
                                // keeps its relative place on the element (and docks again when the port returns).
                                var a = moved.Get(l.Id)!.Attach![1]!;
                                if (end.DistanceTo(mo.BoxPoint(a.U, a.V)) > 0.05) failures.Add($"{item.Id} {port.Id} {what}: port gone and the end does not follow the element");
                            }
                            else if (end.DistanceTo(want.P) > 0.05) failures.Add($"{item.Id} {port.Id} {what}: line end {end.DistanceTo(want.P):0.##} mm off the port");
                        }
                        catch (Exception ex) { failures.Add($"{item.Id} {port.Id} {what}: {ex.GetType().Name}: {ex.Message}"); }
                    }
                }
            }
            foreach (var f in failures) _out.WriteLine("FAIL " + f);
            _out.WriteLine($"{elements} elements with ports, {ports} ports, {failures.Count} failure(s)");
            Assert.True(ports > 100, "too few ports: " + ports);
            Assert.Empty(failures);
        }

        [Fact]
        public void PairsWithTheSameNameCompared()
        {
            // Not an assertion: what each of two same-named elements offers (views, parameters, ports, anchors, actions),
            // for deciding which one stays in the library.
            string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var moduleOf = Reg.Categories.ToDictionary(c => c.Id, c => c.ModuleId);
            foreach (var g in Reg.Library.GroupBy(i => Norm(i.Name.En)).Where(g => g.Select(i => moduleOf[i.Category]).Distinct().Count() > 1))
                foreach (var item in g)
                {
                    var o = Reg.Create(item, new Vec2(100, 100));
                    var doc = SketchDocument.Create(new Size2(400, 300));
                    doc = Edits.Add(doc, doc.Pages[0].Id, o);
                    var t = Reg.Type(o.Type)!;
                    var a = Reg.PageAnchors(o, new RenderContext(doc)).ToList();
                    _out.WriteLine($"PAIR {item.Name.En} | {item.Id} ({moduleOf[item.Category]}, {o.Type}): views {t.Views.Count}, params {t.Parameters.Count}, "
                        + $"ports {a.Count(x => x.Role == AnchorRole.Port)}, anchors {a.Count}, actions {t.Actions(o, new RenderContext(doc)).Count()}, "
                        + $"point geometry {o.IsPointGeometry}, size {o.Size.W:0.#}x{o.Size.H:0.#}, DE \"{item.Name.De}\"");
                }
        }
    }
}
