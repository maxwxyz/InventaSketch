# ADR-0008: Apache License 2.0

- **Date:** 2026-11-20 · **Status:** accepted

## Context
Public repository, engineering and patent context, extensible platform, possible commercial use and redistribution.

## Options
- MIT: simple, but no explicit patent grant.
- Apache-2.0: permissive, explicit patent licence and patent termination clause.
- MPL-2.0: file-level copyleft.
- GPL-3.0: strong copyleft; would complicate bundling with proprietary hosts and commercial distribution.

## Decision
Apache-2.0. The explicit patent grant matters for a tool used around inventions. Third-party content must be
compatible: no GPL code (for example libslvs/SolveSpace), no CC-BY-SA symbol libraries bundled, attribution in
`THIRD-PARTY-NOTICES.md`. A legal review is recommended before commercial distribution.
