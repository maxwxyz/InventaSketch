using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Audit of anchors per element and view: which semantic contacts (hub, axis, pitch circle, ports, mounts, rails)
    /// every library item offers in every view of its type. Writes a report (isk-anchor-audit.txt in the temp folder) and
    /// checks the rules agreed for coupling and snapping.
    /// </summary>
    public class AnchorAuditTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static readonly AnchorRole[] Semantic =
        {
            AnchorRole.Hub, AnchorRole.Axis, AnchorRole.Pitch, AnchorRole.PitchInner, AnchorRole.Port, AnchorRole.Mount, AnchorRole.Rail, AnchorRole.End,
        };

        private static HashSet<AnchorRole> RolesOf(SketchObject o)
        {
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return new HashSet<AnchorRole>(Reg.PageAnchors(doc.Get(o.Id)!, new RenderContext(doc)).Select(a => a.Role).Where(Semantic.Contains));
        }

        /// <summary>Roles per view of every type (views from the type, each rendered from its first library item or the type default).</summary>
        private static Dictionary<string, Dictionary<string, HashSet<AnchorRole>>> Survey()
        {
            var result = new Dictionary<string, Dictionary<string, HashSet<AnchorRole>>>(StringComparer.Ordinal);
            foreach (var t in Reg.Types)
            {
                var item = Reg.Library.FirstOrDefault(i => i.TypeId == t.Id);
                if (item is null) continue;
                var views = t.Views.Count == 0 ? new string?[] { null } : t.Views.Select(v => (string?)v.Id).ToArray();
                var perView = new Dictionary<string, HashSet<AnchorRole>>(StringComparer.Ordinal);
                foreach (var v in views)
                {
                    var probe = Reg.Library.FirstOrDefault(i => i.TypeId == t.Id && i.View == v) ?? item;
                    var o = Reg.Create(probe, new Vec2(100, 100));
                    if (v is not null && !o.IsPointGeometry && o.View != v) o = Reg.WithView(o, v, new RenderContext(SketchDocument.Create()));
                    try { perView[v ?? "-"] = RolesOf(o); }
                    catch { perView[v ?? "-"] = new HashSet<AnchorRole>(); }
                }
                result[t.Id] = perView;
            }
            return result;
        }

        [Theory]
        [InlineData("machines.radial", "side", AnchorRole.Hub)]
        [InlineData("machines.radial", "schematic", AnchorRole.Hub)]
        [InlineData("machines.axial", "side", AnchorRole.Hub)]
        [InlineData("machines.drivetrain.emachine", "stick", AnchorRole.Axis)]
        [InlineData("arch.hvac.radiator", null, AnchorRole.Port)]
        [InlineData("arch.hvac.boiler", null, AnchorRole.Port)]
        [InlineData("arch.hvac.heatpump", null, AnchorRole.Port)]
        [InlineData("arch.sanitary.washbasin", null, AnchorRole.Port)]
        [InlineData("arch.hvac.pipe", null, AnchorRole.Rail)]
        [InlineData("arch.wall", null, AnchorRole.Rail)]
        [InlineData("mech.spur.stick", null, AnchorRole.Pitch)]
        public void ElementsOfferTheirContacts(string typeOrItem, string? view, AnchorRole role)
        {
            var item = Reg.Item(typeOrItem) ?? Reg.Library.FirstOrDefault(i => i.TypeId == typeOrItem && (view is null || i.View == view)) ?? Reg.Library.First(i => i.TypeId == typeOrItem);
            var o = Reg.Create(item, new Vec2(100, 100));
            if (view is not null && !o.IsPointGeometry && o.View != view) o = Reg.WithView(o, view, new RenderContext(SketchDocument.Create()));
            Assert.Contains(role, RolesOf(o));
        }

        [Theory]
        [InlineData("machines.radial")]
        [InlineData("machines.axial")]
        [InlineData("machines.drive.motor")]
        public void ElectricMachinesLeadTheirShaft(string typeId) => Assert.True(Reg.Type(typeId)!.DrivesShaft);

        [Fact]
        public void PipesInPlansTakeCornersAndBranches()
        {
            var pipe = Reg.Create(Reg.Library.First(i => i.TypeId == "arch.hvac.pipe"), Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(50, 0) } };
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, pipe);
            Assert.True(Polylines.CanExtend(Reg, pipe));
            Assert.True(Polylines.CanBranch(Reg, doc, pipe));
        }

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        [Fact]
        public void SideViewGearsMeshOnlyAcrossTheirAxisAndNeverWithAxialViews()
        {
            var a = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50));
            var r = Reg.PageAnchors(a, new RenderContext(Doc(a))).First(x => x.Role == AnchorRole.Pitch).Radius;
            // Above each other (parallel axes): mesh.
            var below = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50 + 2 * r));
            Assert.Contains(Links.For(Reg, Doc(a, below), new[] { a.Id }), l => l.Kind == LinkKind.Mesh);
            // Side by side along the axis: no mesh.
            var beside = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50 + 2 * r, 50));
            Assert.DoesNotContain(Links.For(Reg, Doc(a, beside), new[] { a.Id }), l => l.Kind == LinkKind.Mesh);
            // An axial-view gear touching the side view: no mesh.
            var axial = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(50, 0));
            var ra = Reg.PageAnchors(axial, new RenderContext(Doc(axial))).First(x => x.Role == AnchorRole.Pitch).Radius;
            axial = axial with { Transform = axial.Transform with { Y = 50 - r - ra } };
            Assert.DoesNotContain(Links.For(Reg, Doc(a, axial), new[] { a.Id }), l => l.Kind == LinkKind.Mesh);
        }

        [Fact]
        public void ASideViewGearSnapsStraightAcrossTheAxis()
        {
            var a = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50));
            var b = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(56, 82));
            var doc = Doc(a, b);
            var ctx = new RenderContext(doc);
            var r = Reg.PageAnchors(a, ctx).First(x => x.Role == AnchorRole.Pitch).Radius;
            var opts = new SnapOptions { Tolerance = 20 };
            var targets = Snapper.Targets(Reg, doc, doc.Pages[0], new[] { b.Id }, ctx, opts);
            var res = Snapper.SnapMove(Reg.PageAnchors(b, ctx).ToList(), Vec2.Zero, targets, opts);
            Assert.Equal("mesh", res.Kind);
            var moved = new Vec2(56, 82) + res.Delta;
            Assert.Equal(50, moved.X, 6);
            Assert.Equal(50 + 2 * r, moved.Y, 6);
        }

        [Fact]
        public void WritesTheAnchorReport()
        {
            var survey = Survey();
            var sb = new StringBuilder();
            sb.AppendLine("# Anchor audit: semantic anchor roles per type and view");
            foreach (var module in survey.Keys.GroupBy(k => k.Split('.')[0]).OrderBy(g => g.Key))
            {
                sb.AppendLine().AppendLine("## " + module.Key);
                foreach (var id in module.OrderBy(x => x))
                {
                    var views = survey[id];
                    var all = new HashSet<AnchorRole>(views.Values.SelectMany(v => v));
                    var line = id + ": " + string.Join(" | ", views.Select(kv => kv.Key + " [" + string.Join(",", kv.Value.OrderBy(r => r)) + "]"));
                    var gaps = views.Where(kv => all.Intersect(new[] { AnchorRole.Hub, AnchorRole.Axis, AnchorRole.Port }).Except(kv.Value).Any()).Select(kv => kv.Key).ToList();
                    if (all.Count == 0) line += "   <- no semantic anchors";
                    else if (gaps.Count > 0) line += "   <- views missing hub/axis/port of siblings: " + string.Join(",", gaps);
                    sb.AppendLine(line);
                }
            }
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "isk-anchor-audit.txt"), sb.ToString());
            Assert.NotEmpty(survey);
        }
    }
}
