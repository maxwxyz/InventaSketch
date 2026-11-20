# Contributing to InventaSketch

Thank you for helping. Please read [CLAUDE.md](CLAUDE.md) — it contains the binding working rules for humans and AI
agents (architecture rules, language policy, git workflow, definition of done).

## Quick start

1. Install the .NET 10 SDK and Node.js 20+ (or run `scripts/build.ps1`, which installs .NET user-locally).
2. `dotnet test InventaSketch.sln`
3. Web add-in: see [docs/en/hosts/office-web.md](docs/en/hosts/office-web.md).

## Pull requests

- One topic per pull request, small and reviewable; English commit messages in imperative mood.
- Include tests (xUnit for engine code) and update English **and** German documentation.
- New object types: bilingual names, keywords, at least one library item, anchors for snapping, tests that every
  view renders inside its box, and an entry in the module documentation.
- Symbols must be drawn independently. Do not copy copyrighted symbol libraries, standard texts or images; record
  any third-party asset with its licence in `THIRD-PARTY-NOTICES.md`.
- No placeholder UI or unverified compliance claims.

## Reporting issues

Use GitHub issues with host, host version, operating system, steps to reproduce and the expected result.
Security issues: see [SECURITY.md](SECURITY.md).

By contributing you agree that your contribution is licensed under the Apache License 2.0.
