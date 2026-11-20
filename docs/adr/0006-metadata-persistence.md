# ADR-0006: Metadata in shape tags plus document-level mirror

- **Date:** 2026-11-20 · **Status:** accepted

## Decision
Every inserted object or assembly is a host group with id/type tags and a compressed model payload (`isk1:` =
GZip + Base64 of the canonical JSON). A document-level store (custom document properties, chunked) mirrors all
payloads so that copy/paste, undo and partial deletion can be reconciled. Tags travel with copy/paste; document
properties survive tag loss (for example LibreOffice dropping PowerPoint tags when importing a .pptx).

## Consequences
Reconciliation logic per host; detection of externally modified shapes ("detached" flag), never silent data loss.
Which Office.js store is reliable across platforms (tags vs. custom XML parts vs. custom properties) is verified in
the web add-in slice and recorded in the capability matrix.
