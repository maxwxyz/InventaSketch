using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace InventaSketch.Core
{
    /// <summary>
    /// Domain export: the semantic content of a selection (or page) in a common format, not as graphics. Every exporter
    /// states which semantics it preserves (<see cref="Preserves"/>). Modules contribute exporters through
    /// <see cref="IExportProvider"/>; core contributes the bill of materials and the connection graph.
    /// </summary>
    public interface IExporter
    {
        string Id { get; }
        I18nText Name { get; }
        /// <summary>File extension without dot ("csv", "mmd", "graphml", "txt").</summary>
        string Extension { get; }
        string MimeType { get; }
        /// <summary>What the export keeps (shown to the user).</summary>
        I18nText Preserves { get; }
        /// <summary>True if the export has something to say about these objects.</summary>
        bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects);
        string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang);
    }

    /// <summary>Optional module interface: modules that offer domain exports.</summary>
    public interface IExportProvider
    {
        IEnumerable<IExporter> Exporters { get; }
    }

    public static class Exports
    {
        /// <summary>Exporters of core and of all registered modules that apply to the objects.</summary>
        public static IReadOnlyList<IExporter> For(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
            => All(registry).Where(e => SafeApplies(e, registry, doc, objects)).ToList();

        public static IEnumerable<IExporter> All(Registry registry)
        {
            yield return new BillOfMaterials();
            yield return new ConnectionGraph(mermaid: true);
            yield return new ConnectionGraph(mermaid: false);
            yield return new TikzExporter();
            foreach (var m in registry.Modules.OfType<IExportProvider>())
                foreach (var e in m.Exporters) yield return e;
        }

        private static bool SafeApplies(IExporter e, Registry r, SketchDocument d, IReadOnlyList<SketchObject> o)
        {
            try { return e.Applies(r, d, o); }
            catch (Exception) { return false; }
        }

        /// <summary>CSV field (comma separated, quoted when needed).</summary>
        public static string Csv(string? s)
        {
            s ??= "";
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r', ';' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        /// <summary>The main human-readable name of an object: its reference numeral and text, else the type name.</summary>
        public static string Label(Registry registry, SketchObject o, Lang lang)
        {
            var text = !string.IsNullOrWhiteSpace(o.Text) ? o.Text!.Trim()
                : new[] { "name", "label", "title", "text", "ref" }.Select(k => o.Str(k, "")).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
            return text ?? registry.Type(o.Type)?.Name.Get(lang) ?? o.Type;
        }
    }

    /// <summary>
    /// Bill of materials (CSV): one row per kind of part — same type, view-independent parameters and reference numeral —
    /// with quantity. Annotation (lines, arrows, texts, dimensions) is left out. Preserves: part types, names, numerals,
    /// quantities and the optional parameters as "key=value" pairs.
    /// </summary>
    internal sealed class BillOfMaterials : IExporter
    {
        public string Id => "core.bom";
        public I18nText Name => new("Bill of materials (CSV)", "Stückliste (CSV)");
        public string Extension => "csv";
        public string MimeType => "text/csv";
        public I18nText Preserves => new("Part types, names, reference numerals, quantities, parameters.", "Teiletypen, Bezeichnungen, Bezugszeichen, Mengen, Parameter.");

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects) => objects.Any(o => PartsList.IsPart(registry, o));

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var sb = new StringBuilder();
            sb.Append(lang == Lang.De ? "Pos,Bezugszeichen,Bezeichnung,Typ,Menge,Parameter" : "Item,Numeral,Name,Type,Quantity,Parameters").Append("\r\n");
            foreach (var r in PartsList.Rows(registry, objects, lang))
            {
                sb.Append(r.Item.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Exports.Csv(r.Numeral)).Append(',').Append(Exports.Csv(r.Name)).Append(',')
                  .Append(Exports.Csv(r.TypeName)).Append(',').Append(r.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Exports.Csv(r.Parameters)).Append("\r\n");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Connection graph: boxes are nodes, point objects whose both ends are attached to boxes (connectors, relations,
    /// arrows, wires between components) are edges. Mermaid flowchart or GraphML. Preserves: node names and types, edge
    /// direction and edge type/label; not the drawing geometry.
    /// </summary>
    internal sealed class ConnectionGraph : IExporter
    {
        private readonly bool _mermaid;
        public ConnectionGraph(bool mermaid) { _mermaid = mermaid; }

        public string Id => _mermaid ? "core.graph.mermaid" : "core.graph.graphml";
        public I18nText Name => _mermaid ? new("Connection graph (Mermaid)", "Verbindungsgraph (Mermaid)") : new("Connection graph (GraphML)", "Verbindungsgraph (GraphML)");
        public string Extension => _mermaid ? "mmd" : "graphml";
        public string MimeType => _mermaid ? "text/plain" : "application/xml";
        public I18nText Preserves => new("Nodes (names, types), directed edges (type, label); no geometry.", "Knoten (Namen, Typen), gerichtete Kanten (Typ, Beschriftung); keine Geometrie.");

        private static IEnumerable<(SketchObject Edge, string From, string To)> Edges(SketchDocument doc, IReadOnlyList<SketchObject> objects)
        {
            var ids = new HashSet<string>(objects.Select(o => o.Id));
            foreach (var o in objects)
            {
                if (!o.IsPointGeometry || o.Attach is not { Count: >= 2 } a) continue;
                var from = a[0]?.ObjectId;
                var to = a[a.Count - 1]?.ObjectId;
                if (from is null || to is null || from == to || !ids.Contains(from) || !ids.Contains(to)) continue;
                yield return (o, from, to);
            }
        }

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects) => Edges(doc, objects).Any();

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var edges = Edges(doc, objects).ToList();
            var nodeIds = edges.SelectMany(e => new[] { e.From, e.To }).Distinct().ToList();
            var byId = objects.ToDictionary(o => o.Id);
            var key = nodeIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => "n" + (x.i + 1).ToString(CultureInfo.InvariantCulture));
            string EdgeLabel(SketchObject e)
            {
                var text = !string.IsNullOrWhiteSpace(e.Text) ? e.Text!.Trim() : e.Str("label", "").Trim();
                return text.Length > 0 ? text : e.Str("kind", "");
            }
            if (_mermaid)
            {
                var sb = new StringBuilder("flowchart LR\n");
                foreach (var id in nodeIds) sb.Append("  ").Append(key[id]).Append("[\"").Append(Escape(Exports.Label(registry, byId[id], lang))).Append("\"]\n");
                foreach (var (e, from, to) in edges)
                {
                    var label = EdgeLabel(e);
                    sb.Append("  ").Append(key[from]).Append(label.Length > 0 ? " -->|" + Escape(label) + "| " : " --> ").Append(key[to]).Append('\n');
                }
                return sb.ToString();
            }
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false, Encoding = new UTF8Encoding(false) };
            var builder = new StringBuilder();
            using (var w = XmlWriter.Create(new System.IO.StringWriter(builder, CultureInfo.InvariantCulture), settings))
            {
                const string ns = "http://graphml.graphdrawing.org/xmlns";
                w.WriteStartElement("graphml", ns);
                foreach (var (id, forWhat, name) in new[] { ("label", "node", "label"), ("type", "node", "type"), ("elabel", "edge", "label"), ("etype", "edge", "type") })
                {
                    w.WriteStartElement("key", ns);
                    w.WriteAttributeString("id", id); w.WriteAttributeString("for", forWhat); w.WriteAttributeString("attr.name", name); w.WriteAttributeString("attr.type", "string");
                    w.WriteEndElement();
                }
                w.WriteStartElement("graph", ns);
                w.WriteAttributeString("id", "G"); w.WriteAttributeString("edgedefault", "directed");
                foreach (var id in nodeIds)
                {
                    w.WriteStartElement("node", ns); w.WriteAttributeString("id", key[id]);
                    Data(w, ns, "label", Exports.Label(registry, byId[id], lang)); Data(w, ns, "type", byId[id].Type);
                    w.WriteEndElement();
                }
                var n = 1;
                foreach (var (e, from, to) in edges)
                {
                    w.WriteStartElement("edge", ns);
                    w.WriteAttributeString("id", "e" + (n++).ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("source", key[from]); w.WriteAttributeString("target", key[to]);
                    var label = EdgeLabel(e);
                    if (label.Length > 0) Data(w, ns, "elabel", label);
                    Data(w, ns, "etype", e.Type);
                    w.WriteEndElement();
                }
                w.WriteEndElement(); w.WriteEndElement();
            }
            return builder.ToString();
        }

        private static void Data(XmlWriter w, string ns, string key, string value)
        {
            w.WriteStartElement("data", ns); w.WriteAttributeString("key", key); w.WriteString(value); w.WriteEndElement();
        }

        private static string Escape(string s) => s.Replace("\"", "'").Replace("\n", " ").Replace("|", "/");
    }
}
