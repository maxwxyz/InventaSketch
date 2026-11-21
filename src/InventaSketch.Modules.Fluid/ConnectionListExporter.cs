using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Fluid connection list (CSV), the fluid counterpart of a netlist: line ends docked to component ports (attachments
    /// with a port id) join ports into connections; permanent passages (junction dot, crossing, coupled quick coupling)
    /// merge or keep apart connections as drawn. One row per connection with the line types used and its ports
    /// ("0P1.P; 1V1.P"). Preserves: which ports are joined by which kind of line, component designators and displayed port
    /// names; a branch line attached to another line (docked onto its rail) joins its connection. Not preserved: the line
    /// routing, the switching paths inside valves and line ends that only touch other lines without being attached.
    /// </summary>
    internal sealed class ConnectionListExporter : IExporter
    {
        public string Id => "fluid.connections";
        public I18nText Name => new("Fluid connection list (CSV)", "Fluid-Verbindungsliste (CSV)");
        public string Extension => "csv";
        public string MimeType => "text/csv";
        public I18nText Preserves => new(
            "Connections between ports with line types, designators and port names; not routing or valve switching paths.",
            "Verbindungen zwischen Anschlüssen mit Leitungsarten, Kennzeichen und Anschlussnamen; nicht Leitungsführung oder Schaltwege der Ventile.");

        private static bool IsLine(SketchObject o) => o.Type == FluidLineType.TypeId;

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
            => objects.Any(o => IsLine(o) && o.Attach is not null && o.Attach.Any(a => a?.AnchorId is not null));

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var byId = objects.ToDictionary(o => o.Id);
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            var kinds = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            string Find(string x)
            {
                if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; }
                if (p == x) return x;
                var root = Find(p);
                parent[x] = root;
                return root;
            }
            void Union(string a, string b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }
            static string Key(string objectId, string portId) => objectId + "\u0001" + portId;

            // Every line is a node: the ports it is docked to join it, and so does a line it branches off (attached to it).
            static string Node(string lineId) => "\u0002" + lineId;
            foreach (var line in objects.Where(IsLine))
            {
                var node = Node(line.Id);
                Find(node);
                kinds[node] = new SortedSet<string>(StringComparer.Ordinal) { FluidLineType.KindOf(line) };
                foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                {
                    if (a is null || !byId.TryGetValue(a.ObjectId, out var target)) continue;
                    if (IsLine(target)) Union(node, Node(target.Id));
                    else if (a.AnchorId is not null) Union(Key(a.ObjectId, a.AnchorId), node);
                }
            }
            // Permanent passages inside objects (junction dots join everything docked to them; crossings keep W-E and N-S apart).
            foreach (var o in objects)
            {
                if (registry.Type(o.Type) is not FluidType { Passages: { } passages }) continue;
                foreach (var group in passages(o))
                {
                    var keys = group.Select(g => Key(o.Id, g)).Where(parent.ContainsKey).ToList();
                    for (var i = 1; i < keys.Count; i++) Union(keys[0], keys[i]);
                }
            }

            // Junction dots and crossings are part of the lines, not components: they are not listed as ports.
            bool IsNode(string key) => key[0] == '\u0002';
            bool IsPassage(string key) => byId[key.Split('\u0001')[0]].Type is "fluid.junction" or "fluid.crossing";
            string PortName(string key)
            {
                var parts = key.Split('\u0001');
                var o = byId[parts[0]];
                var type = registry.Type(o.Type);
                var name = o.Str("ref", "").Trim();
                if (name.Length == 0) name = type?.Name.Get(lang) ?? o.Type;
                var port = type is FluidType ft ? ft.PortName(o, parts[1]) : parts[1];
                return name + "." + port;
            }
            string Kind(string kind) => lang == Lang.De
                ? kind switch { "pilot" => "Steuerleitung", "drain" => "Leckölleitung", "hose" => "Schlauch", _ => "Arbeitsleitung" }
                : kind switch { "pilot" => "pilot", "drain" => "drain", "hose" => "hose", _ => "working" };

            var groups = parent.Keys.ToList().GroupBy(Find).Select(g =>
            {
                var ports = g.Where(k => !IsNode(k) && !IsPassage(k)).Select(PortName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
                var lineKinds = g.SelectMany(k => kinds.TryGetValue(k, out var s) ? s : Enumerable.Empty<string>()).Distinct().OrderBy(k => k, StringComparer.Ordinal).Select(Kind).ToList();
                return (Ports: ports, Kinds: lineKinds);
            })
            .Where(x => x.Ports.Count > 1)
            .OrderBy(x => x.Ports[0], StringComparer.Ordinal)
            .ToList();

            var sb = new StringBuilder(lang == Lang.De ? "Verbindung,Leitungsart,Anschlüsse\r\n" : "Connection,Line type,Ports\r\n");
            for (var i = 0; i < groups.Count; i++)
                sb.Append('C').Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Exports.Csv(string.Join(" ", groups[i].Kinds))).Append(',')
                  .Append(Exports.Csv(string.Join("; ", groups[i].Ports))).Append("\r\n");
            return sb.ToString();
        }
    }
}
