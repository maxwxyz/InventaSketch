# ADR-0004: COM add-in takes precedence over the web add-in

- **Date:** 2026-11-20 · **Status:** accepted

## Context
On Windows both add-ins may be installed. Duplicate ribbons must be avoided, and the richer COM add-in (on-slide
interaction) should win.

## Decision
The web add-in's XML manifest contains `EquivalentAddins` with ProgId `InventaSketch.AddIn.Connect` and type `COM`.
Office on Windows then hides the web add-in while the COM add-in is installed. The unified JSON manifest equivalent
is `alternates.prefer.comAddin.progId`. The COM add-in registers per user (HKCU) without administrator rights.

## Consequences
While the COM add-in is installed, Office does not update the web add-in on that machine (documented in the user guide).

## Validation
Manual test on Windows with both add-ins installed (phase 2), recorded in the capability matrix.
