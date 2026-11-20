# ADR-0001: Canonical semantic document model

- **Date:** 2026-11-20 · **Status:** accepted

## Context
Sketches must survive editing in three hosts, exports to SVG/PDF/PNG and later re-editing with semantics
(gear meshing, lead-line attachments, reference numerals). Host shapes and SVG lose semantics.

## Options
1. SVG with embedded payload as source of truth (draw.io style).
2. Host shapes as truth, semantics reconstructed.
3. Own versioned JSON model; SVG and host shapes are render targets.

## Decision
Option 3: `SketchDocument` (`inventasketch-document`, version 1), mm units, immutable records, pure edits.

## Consequences
- Deterministic rendering, testable without hosts, lossless round trip.
- Hosts must store the payload (ADR-0006); externally edited shapes need reconciliation.

## Validation
`PersistenceTests.RoundTripPreservesEverything` (JSON and compact payload).
