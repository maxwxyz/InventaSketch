using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InventaSketch.Core;
using InventaSketch.Engine;

namespace InventaSketch.LibreOffice.Engine
{
    /// <summary>
    /// Engine process of the LibreOffice extension (ported from the Slidemax LibreOffice engine host).
    /// Protocol: one JSON object per line on stdin, <c>{"id": 1, "method": "insert", "params": {…}}</c>, where
    /// <c>params</c> is the <see cref="EngineRequest"/> of the <see cref="EngineApi"/> method; one JSON object per line on
    /// stdout, <c>{"id": 1, "ok": true, "result": …}</c> or <c>{"id": 1, "ok": false, "error": "…"}</c>.
    /// Extra methods: <c>exit</c> (ends the process after answering), <c>ping</c>, <c>methods</c>.
    /// <c>drawPlan</c> without <c>capabilities</c> uses <see cref="DrawCapabilities.LibreOffice"/>.
    /// The process ends at end of input. <c>--version</c> prints the version info and exits.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var api = EngineApi.Default;
            if (args.Length > 0 && args[0] == "--version")
            {
                Console.WriteLine(api.Version());
                return 0;
            }
            var utf8 = new UTF8Encoding(false);
            using var input = new StreamReader(Console.OpenStandardInput(), utf8);
            using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
            // Anything else written to Console.Out (diagnostics of a library) must not corrupt the protocol.
            Console.SetOut(Console.Error);
            string? line;
            while ((line = input.ReadLine()) != null)
            {
                if (line.Trim().Length == 0) continue;
                output.WriteLine(Handle(api, line, out var exit));
                if (exit) break;
            }
            return 0;
        }

        /// <summary>Handles one request line and returns the response line; never throws.</summary>
        internal static string Handle(EngineApi api, string line, out bool exit)
        {
            exit = false;
            JsonNode? id = null;
            try
            {
                var root = JsonNode.Parse(line) as JsonObject ?? throw new ArgumentException("Request must be a JSON object.");
                id = root["id"]?.DeepClone();
                var method = root["method"]?.GetValue<string>() ?? throw new ArgumentException("Missing 'method'.");
                var parameters = root["params"] as JsonObject ?? new JsonObject();
                switch (method)
                {
                    case "exit":
                        exit = true;
                        return Envelope(id, "{\"ok\":true,\"result\":null}");
                    case "ping":
                        return Envelope(id, "{\"ok\":true,\"result\":{\"pid\":" + Environment.ProcessId + "}}");
                    case "methods":
                        return Envelope(id, "{\"ok\":true,\"result\":" + JsonSerializer.Serialize(api.Methods.OrderBy(m => m, StringComparer.Ordinal)) + "}");
                    case "drawPlan" when parameters["capabilities"] is null:
                        parameters["capabilities"] = JsonSerializer.SerializeToNode(DrawCapabilities.LibreOffice, DocumentJson.Options);
                        break;
                }
                return Envelope(id, api.Call(method, parameters.ToJsonString()));
            }
            catch (Exception ex)
            {
                return Envelope(id, "{\"ok\":false,\"error\":" + JsonSerializer.Serialize(ex.Message) + "}");
            }
        }

        /// <summary>Prepends the request id to an <see cref="EngineApi.Call"/> envelope.</summary>
        private static string Envelope(JsonNode? id, string envelope)
            => "{\"id\":" + (id?.ToJsonString() ?? "null") + "," + envelope.Substring(1);
    }
}
