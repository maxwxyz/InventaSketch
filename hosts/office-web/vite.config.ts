import { existsSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { join, resolve } from "node:path";
import { defineConfig } from "vite";

// Office requires https for add-ins. office-addin-dev-certs (npm run certs) writes a trusted localhost certificate to
// ~/.office-addin-dev-certs. Without it the dev server falls back to http (fine for dev.html in a normal browser).
function devHttps(): { key: Buffer; cert: Buffer } | undefined {
  if (process.env.ISK_HTTP === "1") return undefined;
  const dir = join(homedir(), ".office-addin-dev-certs");
  const key = join(dir, "localhost.key");
  const cert = join(dir, "localhost.crt");
  return existsSync(key) && existsSync(cert) ? { key: readFileSync(key), cert: readFileSync(cert) } : undefined;
}

export default defineConfig({
  base: "./",
  server: { port: 3000, strictPort: true, https: devHttps() },
  preview: { port: 3000, strictPort: true, https: devHttps() },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    target: "es2022",
    rollupOptions: {
      input: {
        taskpane: resolve(__dirname, "taskpane.html"),
        dev: resolve(__dirname, "dev.html"),
        sidebar: resolve(__dirname, "sidebar.html"),
      },
    },
  },
  test: {
    environment: "node",
    include: ["test/**/*.test.ts"],
    // Tests that start the .NET engine take seconds on a cold CI runner; the 5 s default timed out there.
    testTimeout: 60000,
  },
} as Parameters<typeof defineConfig>[0]);
