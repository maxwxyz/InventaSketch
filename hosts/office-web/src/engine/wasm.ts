// Loads the .NET WebAssembly engine published to _framework (scripts/engine.mjs) and exposes EngineExports.Call.

import type { EngineBackend } from "./client";

interface DotnetRuntime {
  getAssemblyExports(assembly: string): Promise<{ InventaSketch: { Web: { Engine: { EngineExports: { Call(m: string, j: string): string; Version(): string } } } } }>;
}

interface DotnetModule {
  dotnet: { withApplicationArguments(...args: string[]): { create(): Promise<DotnetRuntime> }; create(): Promise<DotnetRuntime> };
}

export interface LoadInfo { backend: EngineBackend; millis: number }

/** Loads the runtime once per page. <frameworkUrl> points at the folder that contains dotnet.js. */
export async function loadWasmEngine(frameworkUrl = new URL("_framework/", document.baseURI).href): Promise<LoadInfo> {
  const started = performance.now();
  const url = new URL("dotnet.js", frameworkUrl).href;
  const mod = (await import(/* @vite-ignore */ url)) as DotnetModule;
  const runtime = await mod.dotnet.create();
  const exports = await runtime.getAssemblyExports("InventaSketch.Web.Engine");
  const api = exports.InventaSketch.Web.Engine.EngineExports;
  return { backend: { call: (m, j) => api.Call(m, j) }, millis: performance.now() - started };
}
