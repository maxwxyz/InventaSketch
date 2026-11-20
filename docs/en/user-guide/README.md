# User guide

> German: [../../de/user-guide/README.md](../../de/user-guide/README.md). This guide grows with every shipped feature
> and only describes what actually works.

## 1. What InventaSketch is for

Quick technical sketches inside your presentation: gearboxes and machine elements, mechanisms, patent figures with
reference numerals, functional diagrams, flowcharts. You drag elements in, scale them freely and let them snap
together. Numbers (module, tooth count, dimensions) are optional.

## 2. Installation

- **PowerPoint web add-in** (Windows, Mac, web): see [hosts/office-web.md](../hosts/office-web.md) for sideloading
  during development. Store distribution follows later.
- **PowerPoint COM add-in** (Windows) and **LibreOffice extension**: planned, see the [roadmap](../../../ROADMAP.md).

## 3. Basic workflow (target behaviour)

1. Open the InventaSketch pane, search the library (German or English terms work, e.g. "Zahnrad" or "gear").
2. Click an element: an outline follows the pointer and snaps. Click to place, or drag to size it. `Esc` cancels.
3. Move objects; they snap to centres, edges, shaft axes and — for gears in axial view — mesh on their pitch circles.
   Hold `Alt` to move freely.
4. Corner handles scale uniformly, side handles stretch in one direction. Parameters never change by scaling.
5. Right-click for the pie menu (views, flip, numeral, delete). The floating bar next to the selection fades when you
   move away.
6. Switch representations (stick diagram, side view, section, axial, teeth) without losing labels or links.
7. Add lead lines and reference numerals; run the patent preflight for the chosen office; export black-and-white.

## 4. Keyboard

| Key | Action |
|---|---|
| `Del` | delete |
| `Ctrl+Z` / `Ctrl+Y` | undo / redo |
| `Ctrl+D` | duplicate |
| `R` | rotate 90° |
| `X` / `Y` | mirror horizontally / vertically |
| Arrow keys | nudge (`Shift` = 10×) |
| `G` | group |
| `Shift+F10` | pie menu |
| `Esc` | cancel / deselect |

## 5. Patent drawings – important note

The preflight checks formal drawing rules of DPMA, EPO, PCT and USPTO with cited sources. It is an aid only and
does not replace review by a patent professional.
