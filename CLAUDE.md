# CLAUDE.md – working rules for AI agents and developers

This file is the binding operating guide for anyone (human or AI) working in this repository.
Product scope: [docs/en/product/specification.md](docs/en/product/specification.md). Architecture:
[docs/en/architecture/overview.md](docs/en/architecture/overview.md). Roadmap: [ROADMAP.md](ROADMAP.md).

## 1. Language

- **Talk to the maintainer in German** (plans, progress reports, questions, review and test summaries).
- **Repository content is English**: code, identifiers, comments, commit messages, README, docs, ADRs.
- German documentation is maintained in parallel under `docs/de/` and `README.de.md` and must be kept in sync
  with the English canonical version in the same change.
- User-facing strings are bilingual (`I18nText(En, De)`), never hard-coded in one language. Language detection:
  German if the host UI language starts with `de`, otherwise English.

## 2. Architecture rules (non-negotiable)

- **One engine, three hosts.** All domain logic lives in C# libraries under `src/` targeting
  `netstandard2.0;net10.0`. The same code runs in-process in the PowerPoint COM add-in (.NET Framework 4.8),
  as .NET WebAssembly in the Office web add-in and as a .NET child process for LibreOffice.
  Do not re-implement domain logic in TypeScript or Python; hosts only translate draw plans and UI events.
- `InventaSketch.Core` has no dependency on any host or module. Modules (`InventaSketch.Modules.*`) depend only on
  Core, never on each other's internals and never on host APIs.
- The canonical document model (`SketchDocument`) is the source of truth. SVG, PDF, PNG and native shapes are
  render targets. Never discard semantic data silently.
- **Quick Sketch first:** every object must be insertable without entering parameters; free uniform and
  non-uniform scaling changes `Size`, never parameters. Parameters are optional enrichment.
- **Native first:** prefer native host shapes (lines, rectangles, ellipses, freeforms, text, groups) that remain
  editable with the host's own formatting tools. Use a vector picture only where a host cannot express the geometry,
  and say so in the host capability matrix.
- No fake functionality: no placeholder buttons, no claims of standards compliance without a cited source.
- Clean room: the maintainer's proprietary product "Slidemax" is an architectural reference only. Do not copy its
  code into this public repository unless the maintainer explicitly approves a specific transfer in writing.
- Third-party symbols/assets only with a compatible licence recorded in `THIRD-PARTY-NOTICES.md`. ISO/IEC/DIN symbol
  shapes are drawn independently; never copy standard texts, tables or database content.

## 3. Build and test

Windows PowerShell (the .NET 10 SDK may be user-local in `%LOCALAPPDATA%\Microsoft\dotnet`):

```powershell
.\scripts\build.ps1            # restore, build all targets, run all tests
dotnet test InventaSketch.sln  # tests only
```

Linux/macOS: `./scripts/build.sh`. A change is only done when `build` and all tests pass locally.

## 4. Git workflow

- Short-lived feature branches (`feature/<topic>`), merged into `main` after build + tests pass, then pushed.
- Delete merged branches locally and remotely. Keep `main` always buildable.
- Commit messages in English, imperative mood. Never claim a push/merge succeeded without command output.
- Before a session or usage limit ends: save, build, test, commit; record open issues in `ROADMAP.md`
  ("Open items") so the next session can continue.

## 5. Parallel work (agents/subsessions)

- Use parallel agents for separable work (modules, host adapters, research, reviews, tests).
- Each agent gets explicit **file ownership** (a project folder). Agents never edit files they do not own,
  in particular `InventaSketch.sln`, `Directory.Build.props`, `CLAUDE.md` and other modules.
- Agents report changed files, assumptions, test results and open issues. The coordinating agent reviews before merging.

## 6. Definition of done (per feature)

Model + rendering + snapping anchors + inspector parameters (optional) + undo-safe edits + persistence round trip +
unit tests + bilingual names + English/German documentation + host capability notes. Visual output is checked
via the SVG gallery (`tools/InventaSketch.Gallery`) for representative cases.

## 7. Conventions

- Units: millimetres, y down, angles in degrees clockwise on screen (hatch angles: counter-clockwise, as drawn).
- Box objects render in local coordinates centred at (0,0) within `Size`; point objects render in page coordinates.
- Line roles follow ISO 128 ideas (`LineRole.Visible/Thin/Hidden/Center/Phantom/Helper/Cut`).
- Object type ids: `<module>.<family>.<variant>`, e.g. `mech.gear.spur`, `ann.arrow`, `patent.numeral`.
- Tests: xUnit, one test project per library, no host required.
