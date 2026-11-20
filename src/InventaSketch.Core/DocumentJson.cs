using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventaSketch.Core
{
    /// <summary>
    /// Versioned JSON persistence of the canonical document (".isk.json"), plus a compact
    /// GZip+Base64 form used for host metadata (shape tags, document properties).
    /// </summary>
    public static class DocumentJson
    {
        public static readonly JsonSerializerOptions Options = CreateOptions(indented: false);
        public static readonly JsonSerializerOptions Indented = CreateOptions(indented: true);

        public static JsonSerializerOptions CreateOptions(bool indented)
        {
            var o = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = null,
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            };
            o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return o;
        }

        public static string Serialize(SketchDocument doc, bool indented = false) => JsonSerializer.Serialize(doc, indented ? Indented : Options);

        /// <summary>Parses and migrates a document. Throws <see cref="FormatException"/> for foreign or newer files.</summary>
        public static SketchDocument Deserialize(string json)
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out var f) || f.GetString() != SketchDocument.FormatId)
                throw new FormatException("Not an InventaSketch document.");
            var version = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vi) ? vi : 0;
            if (version > SketchDocument.CurrentVersion)
                throw new FormatException($"Document version {version} is newer than supported version {SketchDocument.CurrentVersion}.");
            // Migrations from older versions go here (version 1 is the first public format).
            var doc = JsonSerializer.Deserialize<SketchDocument>(json, Options) ?? throw new FormatException("Empty document.");
            return doc with { Version = SketchDocument.CurrentVersion };
        }

        public static string ToCompact(SketchDocument doc)
        {
            var bytes = Encoding.UTF8.GetBytes(Serialize(doc));
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(bytes, 0, bytes.Length);
            return "isk1:" + Convert.ToBase64String(ms.ToArray());
        }

        public static SketchDocument FromCompact(string payload)
        {
            if (!payload.StartsWith("isk1:", StringComparison.Ordinal)) throw new FormatException("Unknown payload prefix.");
            var data = Convert.FromBase64String(payload.Substring(5));
            using var input = new MemoryStream(data);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, Encoding.UTF8);
            return Deserialize(reader.ReadToEnd());
        }
    }
}
