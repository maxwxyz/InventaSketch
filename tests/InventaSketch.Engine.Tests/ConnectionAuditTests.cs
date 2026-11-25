using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Connection review of every library element (maintainer report 2026-11-25: "with PV, building services etc. you still
    /// cannot connect further lines/cables with '+' and keep building"). For every port of every element: does it carry a
    /// medium, does its build "+" offer the connector of that medium and fitting elements, does the connector dock on the
    /// port and leave a free end with "+" that continues the line or takes the next element? Writes an inventory per module.
    /// </summary>
    public class ConnectionAuditTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public ConnectionAuditTests(ITestOutputHelper output) => _out = output;

        /// <summary>Modules whose ports are media connections: every port must carry a medium (except the listed mechanical ones).</summary>
        private static readonly HashSet<string> ConnectionModules = new(StringComparer.Ordinal)
        {
            "electrical", "energy", "building-services", "fluid", "process",
        };

        /// <summary>Ports in connection modules that are no media connections (mechanical couplings), by type and port id.</summary>
        private static readonly HashSet<string> Mechanical = new(StringComparer.Ordinal) { "process.motor:shaft" };

        private sealed class Row
        {
            public int Elements, Ports, Connection, ConnectorOffer, ElementOffer, Chain, Geometric, Failures;
        }

        private static SketchDocument Page() => SketchDocument.Create(new Size2(1000, 800));

        private static bool IsLine(LibraryItem i) => Reg.Type(i.TypeId)?.Geometry == GeometryKind.Points;

        [Fact]
        public void EveryConnectionPortOffersItsConnectorAndKeepsBuilding()
        {
            var moduleOf = Reg.Categories.ToDictionary(c => c.Id, c => c.ModuleId);
            var rows = new SortedDictionary<string, Row>(StringComparer.Ordinal);
            var failures = new List<string>();
            foreach (var item in Reg.Library)
            {
                var o = Reg.Create(item, new Vec2(500, 400));
                if (o.IsPointGeometry) continue;
                var doc = Doc(o);
                var anchors = Reg.PageAnchors(o, new RenderContext(doc)).Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id is not null).ToList();
                if (anchors.Count == 0) continue;
                var module = moduleOf.TryGetValue(item.Category, out var m) ? m : "?";
                if (!rows.TryGetValue(module, out var row)) rows[module] = row = new Row();
                row.Elements++;
                var type = Reg.Type(o.Type)!;
                var ports = Building.Ports(Reg, doc, o.Id);
                foreach (var a in anchors)
                {
                    row.Ports++;
                    var c = type.Connection(o, a);
                    if (c is null)
                    {
                        if (Flow.CanChain(Reg, o)) row.Chain++;
                        else row.Geometric++;
                        if (ConnectionModules.Contains(module) && !Mechanical.Contains(o.Type + ":" + a.Id))
                        {
                            row.Failures++;
                            failures.Add($"{module} {item.Id} {a.Id}: port without a medium");
                        }
                        continue;
                    }
                    row.Connection++;
                    var fail = CheckPort(doc, o, a, c, ports, row);
                    if (fail is not null) { row.Failures++; failures.Add($"{module} {item.Id} {a.Id} ({c.Medium}): {fail}"); }
                }
            }

            // Connection lines on their own: both free ends offer "+" (continue, elements).
            var lineRows = new SortedDictionary<string, (int Lines, int Ends, int Fails)>(StringComparer.Ordinal);
            foreach (var item in Reg.Library.Where(IsLine))
            {
                var line = Reg.Create(item, new Vec2(500, 400));
                var medium = Reg.Type(line.Type)!.LineMedium(line);
                if (medium is null) continue;
                var module = moduleOf.TryGetValue(item.Category, out var m) ? m : "?";
                var r = lineRows.TryGetValue(module, out var x) ? x : (Lines: 0, Ends: 0, Fails: 0);
                r.Lines++;
                var doc = Doc(line);
                var ends = Building.Ports(Reg, doc, line.Id).Where(p => p.Kind == PortKind.LineEnd).ToList();
                r.Ends += ends.Count;
                if (ends.Count != 2) { r.Fails++; failures.Add($"{module} line {item.Id}: {ends.Count} free ends with \"+\""); }
                foreach (var end in ends)
                {
                    var fail = CheckLineEnd(doc, line.Id, end);
                    if (fail is not null) { r.Fails++; failures.Add($"{module} line {item.Id} {end.Id}: {fail}"); }
                }
                lineRows[module] = r;
            }

            _out.WriteLine("CONNECTION INVENTORY (module: elements with ports, ports, connection ports, with connector offer, with element offer, quick-add ports, geometric ports, failures)");
            foreach (var (module, r) in rows)
                _out.WriteLine($"MODULE {module}: {r.Elements} elements, {r.Ports} ports, {r.Connection} connection ports, {r.ConnectorOffer} offer the connector, {r.ElementOffer} offer elements, {r.Chain} quick add, {r.Geometric} geometric/mechanical, {r.Failures} failure(s)");
            foreach (var (module, r) in lineRows)
                _out.WriteLine($"LINES {module}: {r.Lines} connection lines, {r.Ends} free ends with \"+\", {r.Fails} failure(s)");
            foreach (var f in failures) _out.WriteLine("FAIL " + f);
            Assert.True(rows.Values.Sum(r => r.Connection) > 500, "too few connection ports");
            Assert.Empty(failures);
        }

        private static SketchDocument Doc(SketchObject o)
        {
            var doc = Page();
            return Edits.Add(doc, doc.Pages[0].Id, o);
        }

        /// <summary>Checks one connection port of an element alone on a page; null if everything works.</summary>
        private static string? CheckPort(SketchDocument doc, SketchObject o, Anchor a, PortConnection c, IReadOnlyList<BuildPort> ports, Row row)
        {
            var port = ports.FirstOrDefault(p => p.Kind == PortKind.Connect && p.AnchorId == a.Id);
            if (port is null) return "no \"+\" at the port";
            if (port.At.DistanceTo(a.P) > 1e-6 || Math.Abs(port.Dir.Length - 1) > 1e-6) return "\"+\" off the port";
            var items = Building.Items(Reg, doc, o.Id, port, 12);
            var connectors = items.Where(IsLine).ToList();
            var elements = items.Where(i => !IsLine(i)).ToList();
            if (connectors.Count == 0) return "no connector offered";
            row.ConnectorOffer++;
            if (elements.Count == 0) return "no element offered";
            row.ElementOffer++;

            // The connector: start docked on the port, free end with "+".
            var built = Building.Apply(Reg, doc, o.Id, port, connectors[0]);
            if (built is null) return $"connector {connectors[0].Id} not placed";
            var line = built.Doc.Get(built.SelectId)!;
            if (!line.IsPointGeometry) return "connector is no line";
            var pts = built.Doc.ResolvePoints(line);
            if (line.Attach?[0] is not { } start || start.ObjectId != o.Id || start.AnchorId != a.Id) return "connector start not docked on the port";
            if (pts[0].DistanceTo(a.P) > 1e-6) return "connector start off the port";
            if (Building.LineMedium(Reg, built.Doc, line) is not { } lm || !PortMedia.Fits(lm, c.Medium)) return "connector carries no fitting medium";
            if (Building.Ports(Reg, built.Doc, o.Id).Any(p => p.AnchorId == a.Id)) return "port still offered after connecting";
            var end = Building.Ports(Reg, built.Doc, line.Id).SingleOrDefault(p => p.Kind == PortKind.LineEnd);
            if (end is null) return "connector end offers no \"+\"";
            var fail = CheckLineEnd(built.Doc, line.Id, end);
            if (fail is not null) return "connector end: " + fail;

            // The next element straight from the port: joined by the connector.
            var next = Building.Apply(Reg, doc, o.Id, port, elements[0]);
            if (next is null) return $"element {elements[0].Id} not placed";
            var joint = next.Doc.Objects.Values.FirstOrDefault(x => x.IsPointGeometry && x.Attach is { Count: >= 2 }
                && x.Attach[0]?.ObjectId == o.Id && x.Attach[x.Attach.Count - 1]?.ObjectId == next.SelectId);
            if (joint is null) return $"element {elements[0].Id} not joined to the port by a connector";
            var jp = next.Doc.ResolvePoints(joint);
            var el = next.Doc.Get(next.SelectId)!;
            var elPort = Reg.PageAnchors(el, new RenderContext(next.Doc)).FirstOrDefault(x => x.Role == AnchorRole.Port && x.Id == joint.Attach![joint.Attach.Count - 1]!.AnchorId);
            if (elPort is null || elPort.P.DistanceTo(jp[jp.Count - 1]) > 1e-6) return $"connector end off the port of {elements[0].Id}";
            if (el.Bounds().Intersects(o.Bounds())) return $"element {elements[0].Id} overlaps the source";
            return null;
        }

        /// <summary>Checks a free line end: "+" continues the line and docks an element; null if everything works.</summary>
        private static string? CheckLineEnd(SketchDocument doc, string lineId, BuildPort end)
        {
            var items = Building.Items(Reg, doc, lineId, end, 12);
            var line = doc.Get(lineId)!;
            var cont = items.FirstOrDefault(i => i.TypeId == line.Type);
            if (cont is null) return "does not continue the line";
            var el = items.FirstOrDefault(i => !IsLine(i));
            if (el is null) return "offers no element";
            var longer = Building.Apply(Reg, doc, lineId, end, cont);
            if (longer is null || longer.SelectId != lineId) return "continuing failed";
            var n0 = doc.ResolvePoints(line).Count;
            var after = longer.Doc.ResolvePoints(longer.Doc.Get(lineId)!);
            if (after.Count != n0 + 1) return "continuing added no point";
            if (!Building.Ports(Reg, longer.Doc, lineId).Any(p => p.Kind == PortKind.LineEnd && p.Id == end.Id)) return "continued end offers no \"+\"";
            var docked = Building.Apply(Reg, doc, lineId, end, el);
            if (docked is null) return $"element {el.Id} not placed at the end";
            var l2 = docked.Doc.Get(lineId)!;
            var index = Attachments.PointIndex(end.AnchorId) ?? -1;
            if (l2.Attach is null || index < 0 || l2.Attach[index]?.ObjectId != docked.SelectId) return $"line end not docked on {el.Id}";
            var anchor = Reg.PageAnchors(docked.Doc.Get(docked.SelectId)!, new RenderContext(docked.Doc)).FirstOrDefault(x => x.Role == AnchorRole.Port && x.Id == l2.Attach[index]!.AnchorId);
            if (anchor is null || anchor.P.DistanceTo(docked.Doc.ResolvePoints(l2)[index]) > 1e-6) return $"line end off the port of {el.Id}";
            return null;
        }
    }
}
