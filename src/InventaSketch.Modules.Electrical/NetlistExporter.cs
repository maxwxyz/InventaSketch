using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Netlist / connection list (CSV): wire ends docked to component pins (attachments with a pin id) join pins into
    /// nets; junction dots and wires sharing a pin merge nets. One row per net with its pins "R1.1", "C1.2".
    /// Preserves: electrical connectivity between pins and component designators; not the wire routing.
    /// Wires that only touch other wires (no pin) are not connected (illustrative schematics have no ERC).
    /// </summary>
    internal sealed class NetlistExporter : IExporter
    {
        public string Id => "elec.netlist";
        public I18nText Name => new("Netlist / connection list (CSV)", "Netzliste / Verbindungsliste (CSV)");
        public string Extension => "csv";
        public string MimeType => "text/csv";
        public I18nText Preserves => new("Nets with the connected pins and designators; not the routing.", "Netze mit den verbundenen Pins und Kennzeichen; nicht die Leitungsführung.");

        private static bool IsWire(SketchObject o) => o.Type == WireType.TypeId || o.Type.StartsWith("elec.bus", StringComparison.Ordinal);

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
            => objects.Any(o => IsWire(o) && o.Attach is not null && o.Attach.Any(a => a?.AnchorId is not null));

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var byId = objects.ToDictionary(o => o.Id);
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            string Find(string x)
            {
                if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; }
                if (p == x) return x;
                var root = Find(p);
                parent[x] = root;
                return root;
            }
            void Union(string a, string b) => parent[Find(a)] = Find(b);

            foreach (var w in objects.Where(IsWire))
            {
                var pins = (w.Attach ?? Array.Empty<Attachment?>()).Where(a => a?.AnchorId is not null && byId.ContainsKey(a.ObjectId))
                    .Select(a => a!.ObjectId + "\u0001" + a.AnchorId).ToList();
                foreach (var p in pins) Find(p);
                for (var i = 1; i < pins.Count; i++) Union(pins[0], pins[i]);
            }
            // A junction dot connects all wires docked to any of its pins.
            foreach (var j in objects.Where(o => o.Type == "elec.junction"))
            {
                var pins = parent.Keys.Where(k => k.StartsWith(j.Id + "\u0001", StringComparison.Ordinal)).ToList();
                for (var i = 1; i < pins.Count; i++) Union(pins[0], pins[i]);
            }

            string PinName(string key)
            {
                var parts = key.Split('\u0001');
                var o = byId[parts[0]];
                var name = o.Str("ref", "").Trim();
                if (name.Length == 0) name = registry.Type(o.Type)?.Name.Get(lang) ?? o.Type;
                return name + "." + parts[1];
            }

            var nets = parent.Keys.GroupBy(Find)
                .Select(g => g.Where(k => byId[k.Split('\u0001')[0]].Type != "elec.junction").Select(PinName).OrderBy(n => n, StringComparer.Ordinal).ToList())
                .Where(n => n.Count > 1)
                .OrderBy(n => n[0], StringComparer.Ordinal)
                .ToList();
            var sb = new StringBuilder(lang == Lang.De ? "Netz,Pins\r\n" : "Net,Pins\r\n");
            for (var i = 0; i < nets.Count; i++)
                sb.Append("N").Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(',').Append(Exports.Csv(string.Join(" ", nets[i]))).Append("\r\n");
            return sb.ToString();
        }
    }
}
