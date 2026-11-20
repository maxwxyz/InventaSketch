# ADR-0005: LibreOffice via Python-UNO extension and .NET engine process

- **Date:** 2026-11-20 · **Status:** accepted (per-OS verification pending)

## Context
LibreOffice has no embedded web view in its UI; Java extensions need a JRE; the new .NET UNO bindings cannot be
used to write extensions yet; Python is bundled on Windows and macOS and available on Linux. A claimed 2026
"pywebview" blog post from The Document Foundation could not be verified.

## Decision
A Python-UNO `.oxt` provides menus, a sidebar panel, on-canvas handlers (mouse click and motion, key handler,
selection listener, context-menu interceptor) and writes native shapes (`PolyPolygonBezierShape`, `LineShape`,
`EllipseShape`, `TextShape`, native `FillHatch`, glue points). Domain logic runs in the self-contained .NET engine
process (JSON lines over stdin/stdout), packaged per runtime identifier, plus a portable variant that requires .NET 10.

## Consequences
Per-RID packages of about 20 MB; macOS packages must be built on macOS. All UNO work is marshalled to the main thread.

## Validation
Phase-3 spike on Windows, Linux and macOS; headless proof runs.
