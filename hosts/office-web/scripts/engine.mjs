// Publishes the .NET WebAssembly engine (engine/InventaSketch.Web.Engine.csproj) and copies its _framework folder to
// public/_framework, where Vite serves it in dev and copies it into dist on build.
// dotnet is looked up in PATH, %LOCALAPPDATA%\Microsoft\dotnet and ~/.dotnet.
// Flags: --if-missing (skip when public/_framework/dotnet.js exists).

import { spawnSync } from "node:child_process";
import { cpSync, existsSync, rmSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const target = join(root, "public", "_framework");
if (process.argv.includes("--if-missing") && existsSync(join(target, "dotnet.js"))) {
  console.log("engine: public/_framework exists, skipping publish (run `npm run engine` to rebuild)");
  process.exit(0);
}

const exe = process.platform === "win32" ? "dotnet.exe" : "dotnet";
const candidates = [
  process.env.DOTNET_ROOT && join(process.env.DOTNET_ROOT, exe),
  process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, "Microsoft", "dotnet", exe),
  join(homedir(), ".dotnet", exe),
].filter(Boolean);

function hasNet10(cmd) {
  const r = spawnSync(cmd, ["--list-sdks"], { encoding: "utf8" });
  return r.status === 0 && /^10\./m.test(r.stdout);
}

const dotnet = candidates.find((c) => existsSync(c) && hasNet10(c)) ?? (hasNet10(exe) ? exe : undefined);
if (!dotnet) {
  console.error("engine: no .NET 10 SDK found (https://dot.net). The task pane cannot run without the engine.");
  process.exit(1);
}

const project = join(root, "engine", "InventaSketch.Web.Engine.csproj");
const out = join(root, "engine", "bin", "publish");
console.log(`engine: ${dotnet} publish ${project}`);
const started = Date.now();
const result = spawnSync(dotnet, ["publish", project, "-c", "Release", "-o", out, "--nologo", "-v", "minimal"], {
  stdio: "inherit",
  env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: "1", DOTNET_NOLOGO: "1" },
});
if (result.status !== 0) process.exit(result.status ?? 1);

rmSync(target, { recursive: true, force: true });
cpSync(join(out, "wwwroot", "_framework"), target, { recursive: true });
console.log(`engine: copied to ${target} in ${((Date.now() - started) / 1000).toFixed(1)} s`);
