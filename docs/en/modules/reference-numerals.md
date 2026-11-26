# Technical drawings: reference numerals module

Library: `src/InventaSketch.Modules.Patent` (module id `patent`) · Tests: `tests/InventaSketch.Modules.Patent.Tests` ·
German version: [../../de/modules/reference-numerals.md](../../de/modules/reference-numerals.md)

The module prepares technical drawings for filing with an office: **reference numerals** with lead lines, the
**reference list** (numeral and part name), **automatic numbering of all parts**, numeral **variants** (10a, 10b, 10′),
**assembly axes** for exploded views, **surface shading**, a drawing-sheet helper, sheet numbers, office profiles, a
**compatibility check (preflight)** and the black-and-white export. The office rules it checks are listed with their
sources in [../research/drawing-rules.md](../research/drawing-rules.md). A clean preflight is a drafting aid, never a
legal certification.

## Conventions

- **Quick sketch first:** every library item inserts without parameters. Scaling changes `Size` only.
- **Numerals** are point objects: point 0 is the end on the part (it docks on the part and follows it), the last point is
  the numeral. The numeral itself is `Ref` (preferred), else the text, else the parameter `numeral`. Under profiles that
  forbid enclosed signs (EPO, PCT, USPTO, common-safe) brackets and quotes are never drawn.
- **Parts** are the elements that count in the parts list (`PartsList.IsPart`): annotation, numerals, sheet marks, map
  symbols, diagram shapes and charts are no parts.
- **Line roles:** lead lines thin continuous; assembly axes thin dashed (or a dash-dot centre line); shading thin lines.
- All operations are pure document edits; each action returns one new document, so hosts record it as **one undo step**.

## Object types

| Type id | Element | Parameters (all optional) |
|---|---|---|
| `patent.numeral` | Reference numeral with lead line (straight, curved, wavy; arrow for a whole assembly; underlined surface numeral without lead line) | `numeral` (10), `label` (part name), `underline`, `curve`, `end`, `bend`, `textSize` |
| `patent.exploded.axis` | Assembly axis of an exploded view: thin line in the gap between two parts pulled apart; both ends dock on the parts | `form` (`dashed` default, `chain` = dash-dot centre line) |
| `patent.shading` | Surface shading laid over a part: thin spaced lines instead of grey tones, light from the upper left (lines gather on the lower right). Forms: cylinder (parallel lines closing up towards the edge), cone (lines fanning from the apex), sphere (concentric arcs), flat face (evenly spaced lines). Scale and rotate the box onto the surface; mirror it for light from the other side. Not a part. | `form`, `lines` (5, 2–20) |
| `patent.sheet` | Drawing-sheet frame and usable area of a profile (helper, never exported, ignored by preflight) | `profile` |
| `patent.pagenumber` | Sheet number "n/N", computed from the page position | `auto`, `textSize` |

Shading follows rows US-15 (37 CFR 1.84(m): thin spaced lines, light from the upper left at 45°) and EP-20 (Guidelines
A-IX 7.2: shading allowed where it helps understanding) of the research document; solid and grey fills remain preflight
findings.

## Actions

| Action | On | What it does |
|---|---|---|
| **Number all parts ▸ 10, 20, 30 … / 1, 2, 3 …** (`patent.numerals.auto=tens` / `=ones`) | numeral, sheet frame, sheet number | Numbers every part of the page that has no numeral yet, in reading order (rows top to bottom, left to right within a row; a part belongs to a row when its vertical centre lies within the height of the row's topmost part). Each part gets a numeral with a lead line from inside the part to beyond its outer upper corner; the numeral is written to the part (`Ref`, so the parts list shows it) and to the reference list with the part's name (made unique with a counter, "Block 2", when another number already uses the name). Parts that already have a numeral are skipped; a part that carries a numeral of its own keeps it and only gets the lead line. Identical parts (same type, text and parameters) share one numeral by default (`RepeatedParts.Shared`); the API also offers variants 10a, 10b (`Suffixed`) or one numeral each (`Separate`). Greyed out when every part is numbered. |
| **Variant ▸ 10 / 10a / 10b / 10c / 10d / 10′ / 10″** (`patent.numeral.suffix=…`) | numeral | Sets the variant suffix of the numeral (repeated or modified parts). The part it points at follows; a new variant gets the part name of its number in the reference list. Renumbering keeps suffixes (10a → 20a). |
| **Assembly axis to the nearest part** (`patent.numeral.axis`) | numeral attached to a part | Adds an assembly axis from the numbered part to the nearest part not yet joined to it, on the line through both centres, from edge to facing edge (centre to centre when the parts overlap). Run it again for the next part. |
| Surface ▸ cylinder / cone / sphere / flat face; More lines / Fewer lines | shading | Form and line count of the shading. |
| Line ▸ dashed / dash-dot | assembly axis | Line form of the axis. |

API (`ReferenceNumerals`, `PartNumbering`, `ExplodedViews`): `NextFree`, `Insert`, `Rename`, `Renumber`, `Apply`, `Sync`,
`List`, `Groups`, `Export`, `Check`, `SplitSuffix`, `WithSuffix`, `SetSuffix`, `NumberedParts`, `PartKind`,
`PartsInReadingOrder`, `NumberParts`, `AddAxis`, `NearestPart`.

## Reference list

`ReferenceNumerals.Export` writes the list as text, one line per part with its variants grouped ("10, 10a, 10′
housing"); a variant with its own name stays a separate line below its number. The CSV form keeps one row per numeral
(`numeral,label`), variants below their number. The domain export "Reference sign list (text)" uses the text form.

## Preflight findings on numerals

Besides the sheet, colour, text and figure checks (see the research document, section 5), the preflight reports:

| Rule | Severity | Finding | Source |
|---|---|---|---|
| `PF-LEAD-FREE` | info | Lead line not attached, but its end lies on an element | P11: EP-16, US-20 |
| `PF-LEAD-TARGET` | warning | **Numeral points at nothing**: the lead line ends on no element, or it is attached to an element that no longer exists | P11: EP-16, US-20 |
| `PF-REF-CONFLICT` | error | One numeral used for differently named parts | P19 |
| `PF-REF-DUPLICATE` | error | **One numeral on parts of different kinds** (different type, text or parameters; identical copies may share a numeral) | P19: DE-12, EP-15, PCT-13, US-19 |
| `PF-REF-LABEL` | warning | One part name with different numbers (variants of one number are fine) | P19 |
| `PF-REF-MISSING` / `PF-REF-UNUSED` | error / warning | Numeral not in the reference list / list entry never drawn | P19 |
| `PF-PART-UNNUMBERED` | info | **Part without numeral** (no numeral points at it and it carries none; a numeral with an arrow covers its whole group). Drafting aid for every profile: no office rule requires a numeral on every part, so the finding cites none. | drafting aid |

## Host notes

- Numerals, assembly axes and shading are plain lines, polylines and text, so every host draws them as native shapes.
- "Number all parts" and "Assembly axis to the nearest part" are document actions (`ContextAction.ApplyInDocument`) that
  add objects; hosts that run document actions apply them as one edit. They are offered on a numeral, the sheet frame and
  the sheet number; a page-level command (without a selected object) needs a host or engine entry point.

## Open items

- Page-level "Number all parts" without a selected numeral or sheet object (engine/host command).
- Numeral placement avoids the page edges only; it does not yet avoid other numerals or lead-line crossings
  (preflight reports crossings).
- Brackets embracing exploded parts for the US profile (research P20) are not drawn or checked yet.
