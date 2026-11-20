# Security policy

## Supported versions

InventaSketch is in early development; only the latest `main` is supported.

## Reporting a vulnerability

Please report vulnerabilities privately via GitHub's "Report a vulnerability" (Security tab → Advisories) instead of a
public issue. Include affected component (engine, web add-in, COM add-in, LibreOffice extension), version and steps to
reproduce. We aim to acknowledge reports within 7 days.

## Scope notes

- Imported SVG and documents are untrusted input; the engine must sanitise them and never execute embedded scripts.
- The web add-in loads only from its own origin; no remote code at runtime.
- The COM add-in and LibreOffice extension install per user without administrator rights and do not open network ports
  except an optional loopback-only development server.
