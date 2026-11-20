# ADR-0003: Native shapes first, vector picture as documented fallback

- **Date:** 2026-11-20 · **Status:** accepted

## Context
Users must keep editing colours, line widths, fonts and positions with the PowerPoint/LibreOffice tools.

## Decision
The engine emits a host draw plan of primitives (rectangle, ellipse, line, freeform, text, group). Each host
adapter writes native shapes. Where a host lacks a capability (Office.js: freeform, arrowheads, pattern fill), the
adapter lowers the object (explicit geometry such as hatch lines) or inserts a vector picture for that object only,
and the [capability matrix](../en/hosts/capability-matrix.md) states it. Diagrams are never rasterised by default.

## Consequences
Per-host lowering rules and visual parity tests per host.
