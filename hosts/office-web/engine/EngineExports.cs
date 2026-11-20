using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using InventaSketch.Engine;

namespace InventaSketch.Web.Engine
{
    /// <summary>
    /// JavaScript-visible wrapper around <see cref="EngineApi"/>. In TypeScript:
    /// <c>(await runtime.getAssemblyExports("InventaSketch.Web.Engine")).InventaSketch.Web.Engine.EngineExports</c>.
    /// All calls use JSON strings; <see cref="Call"/> never throws and returns <c>{"ok":…}</c>.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static partial class EngineExports
    {
        /// <summary>Dispatches any EngineApi method by name, e.g. Call("insert", "{\"session\":…}").</summary>
        [JSExport]
        public static string Call(string method, string requestJson) => EngineApi.Default.Call(method, requestJson);

        /// <summary>Engine version, modules and method list (JSON).</summary>
        [JSExport]
        public static string Version() => EngineApi.Default.Version();
    }

    internal static class Program
    {
        private static void Main()
        {
            // Nothing to start: the task pane calls the exports. Touch the API so registration errors surface early.
            _ = EngineApi.Default.Registry.Modules.Count;
        }
    }
}
