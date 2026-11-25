# ADR-0007: Module contract and versioning

- **Date:** 2026-11-20 · **Status:** accepted

## Decision
Modules implement `IModule` (id, name, version, object types, library categories and items). Object types derive
from `ObjectType` (render, anchors, views, optional parameters, context actions). Type ids are stable
(`<module>.<family>.<variant>`); renaming requires a document migration. Unknown types in a document are kept and
skipped during rendering, never deleted. Modules ship as first-party assemblies first; loading third-party modules
is a later decision (security and versioning).

## Consequences
New domains are added without touching Core. Cross-module reuse goes through public types only (for example
Figures uses Annotation's lead-line helpers).
