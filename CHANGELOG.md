# Changelog

All notable changes are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Engine core (C#, netstandard2.0 + net10.0): document model, geometry, drawing primitives with line roles,
  arrowheads and hatching, module registry, semantic snapping (gear mesh, hub on axis, alignment, grid), pure edits
  with undo/redo, versioned JSON persistence with compact payload, SVG renderer with monochrome mode.
- Consolidated product specification, architecture overview, ADRs 0001–0008, research notes on the drawing rules
  of the offices (DPMA, EPO, PCT, USPTO) and on tools/host APIs.
- Repository governance: README (EN/DE), Apache-2.0 licence, contribution, security and conduct policies, roadmap.

### Changed
- The drawing-rule check is called "Compatibility" / "Kompatibilität" in every host; user-facing names, help texts,
  search keywords and documentation use neutral wording ("Figures" module, "Numerals & sheets", "Figure marks",
  office profiles "EPO (Europe)" and "USPTO (utility)"). Persisted type ids and engine commands are unchanged.
