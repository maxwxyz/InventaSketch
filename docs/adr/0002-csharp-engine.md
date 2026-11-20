# ADR-0002: One C# engine for all hosts

- **Date:** 2026-11-20 · **Status:** accepted

## Context
InventaSketch needs live interaction on the slide (PowerPoint COM: mouse hooks and a layered overlay window) and
on the canvas (LibreOffice: UNO mouse handlers), plus a sidebar editor in the Office web add-in. Snapping and
hit-testing must run synchronously while dragging. The research note
([tools-and-host-apis.md](../en/research/tools-and-host-apis.md), Part C.1) recommended a TypeScript core hosted in
WebView2 (COM) and in a browser with a local server (LibreOffice).

## Options
1. TypeScript core; COM via WebView2/ClearScript; LibreOffice via browser and local server.
2. C# core (netstandard2.0 + net10.0): in-process in COM (.NET Framework 4.8), WebAssembly in the task pane,
   self-contained .NET process for LibreOffice (JSON lines over stdio, driven by the Python-UNO extension).
3. Separate implementations per host.

## Decision
Option 2. It keeps a single source of domain logic, allows synchronous in-process interaction in COM, works with
the Python bundled with LibreOffice on all operating systems (there is no JavaScript runtime in LibreOffice) and is
proven in production by the maintainer's earlier proprietary add-in with the same three hosts. TypeScript is limited
to the web UI and Office.js translation; Python to UNO translation.

## Consequences
- One tested engine, parity across hosts, synchronous snapping in COM and LibreOffice.
- WebAssembly payload (about 3 MB compressed, about 1 s start) in the web add-in; per-OS engine builds for
  LibreOffice; JavaScript libraries (for example constraint solvers) must be ported to C# or bridged.

## Validation
Engine builds for netstandard2.0 and net10.0; WebAssembly publish of the engine (office-web); LibreOffice engine
process spike per OS (phase 3).
