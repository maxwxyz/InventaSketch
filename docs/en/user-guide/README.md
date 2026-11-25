# User guide

> German: [../../de/user-guide/README.md](../../de/user-guide/README.md). This guide grows with every shipped feature
> and only describes what actually works.

## 1. What InventaSketch is for

Quick technical sketches inside your presentation: gearboxes and machine elements, mechanisms, technical figures with
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
7. Add lead lines and reference numerals; run the compatibility check for the chosen office; export black-and-white.

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
| `Ctrl+Space` | insert box: search and insert (fitting parts first) |
| `Tab` / `Shift+Tab` | next / previous part of the assembly (PowerPoint COM) |
| `Esc` | cancel / deselect |

## 5. Drawings for applications – important note

The preflight checks formal drawing rules of DPMA, EPO, PCT and USPTO with cited sources. It is an aid only and
does not replace review by an IP professional. In PowerPoint: InventaSketch ▸ Review ▸ "Compatibility"
checks every slide for the chosen office and lists errors, warnings, notes and points to check by hand with their
source; double-click a finding to go to its slide and element (engine `preflight`, `patentProfiles`). In the web
add-in the check button in the top bar opens the "Compatibility" panel, which does the same for the page; clicking a
finding selects its element. In LibreOffice Impress and Draw: InventaSketch ▸ "Compatibility (Drawing Rules)…" checks
every page; choosing a finding goes to its slide and unit.

Variants (before/after, prior art/invention): duplicate the slide and change the copy. InventaSketch ▸ Review ▸
"Compare with the previous slide" marks what was added (green), changed (orange) and removed (dashed grey) with a
legend; run it again to remove the marks (engine `diff`). In LibreOffice Impress and Draw: InventaSketch ▸ "Compare
with Previous Slide (Variants)".

## 6a. Importing drawings and data

InventaSketch ▸ Review ▸ "Import …" (web: the import button in the top bar; LibreOffice: InventaSketch ▸ "Import Drawing
(SVG, DXF)…") reads SVG and DXF drawings as editable shapes – lines, polylines, rectangles, ellipses and texts, grouped
as one element. A drawing with units keeps its real size when it fits on the slide; otherwise it is fitted. Arcs and
curves become polylines whose points can be moved. Colours, line types, fills, hatches, blocks and dimensions are not
imported. The same command reads CSV tables into charts and GeoJSON/GPX into map routes.

## 6b. Exporting for LaTeX and vector programs (2026-11-25)

"Export data" (COM: Review ▸ Export data; web: export in the top bar; LibreOffice: export command) offers
"LaTeX (TikZ)" for any selection or page – flowcharts, block diagrams, sketches, charts. The file is a standalone
LaTeX document with one `tikzpicture` (copy it into your document; it needs `\usepackage{tikz}` and the libraries
`patterns` and `patterns.meta`). Every text is a TikZ node: it is set in your document's font and stays editable and
formattable in LaTeX (special characters escaped, umlauts kept). Each element is a commented `scope`; colours are named
`\definecolor`s; hatches are TikZ patterns. Charts as pgfplots code are planned (charts v2).
The web add-in's SVG export writes texts as text and every element and part as a named group (Inkscape labels with the
element's name and the part key, e.g. `series.0`), so texts and styles can be edited in Inkscape or Illustrator.

## 6c. Building on with "+"

A single selected element shows a blue "+" at every free place where something can be added: a meshing gear on a gear,
the next shaft section or a part at a shaft end, a planet in a ring gear, the next link at a free joint, a tool on a robot
flange – and, for everything that is connected by lines, at every free **connection port**:

- **Which ports.** Electrical symbols, house installation, PCB pads, energy elements (PV modules, strings and arrays,
  inverters, batteries, wind turbines, transformers, switchgear, chargers, heat pumps, electrolysers …), building services
  (radiators, boilers, heat pumps, manifolds, pumps, valves, sanitary fixtures, risers, air terminals, ventilation units,
  outlets and detectors), fluid power, process plants (nozzles, valves, pumps, instruments) and the electrical, coolant and
  block-diagram terminals of electric machines. A port that already has a line or an element on it shows no "+".
- **What it offers.** Clicking the "+" opens the insert box with the connector of the port's medium first – a DC cable at
  the DC side of a PV module, inverter or battery, an AC cable at the grid side (with the voltage of the winding or the
  grid where the element states it), a heating flow pipe at a flow connection and a return pipe at a return, cold water,
  hot water, circulation and waste pipes at sanitary fixtures, a supply or extract duct at air terminals, an installation
  cable at sockets, switches and outlets, a wire at schematic symbols, a track at PCB pads, a working, pilot or drain line
  in fluid power, a process or signal line in a P&ID – then the elements that have a port of the same medium.
- **Connector.** Choosing the connector draws it straight out of the port, its start docked on the port, and selects it.
  Its free end shows a "+" again: choose the same line to continue it by one segment, or an element to put that element's
  fitting port on the line end (the line stays docked when the element moves). So you build a whole PV system, heating
  circuit or circuit by clicking "+" again and again.
- **Element.** Choosing an element directly places it one connector length away, with its fitting port facing back,
  joined to the port by the connector.
- **Media fit.** A general electrical wire fits DC and AC ports, a heating pump or valve fits flow and return pipes, a heat
  pump fits heat and cooling pipes. Elements of other modules are offered only when their port carries exactly that
  medium (an installation outlet of the building services after an installation cable, not a resistor after a PV string).
  A line drawn by hand from a connection port carries the port's medium, so its free end shows the "+" too.

Hosts: the PowerPoint web add-in shows these "+" for elements and for line ends (engine `buildPorts`, kinds `Connect`
and `LineEnd` with their `medium`; `buildItems`; `buildPlace` builds connector, element or longer line in one undo
step); LibreOffice lists the same ports in its sidebar and builds through the same engine calls. In the PowerPoint COM
add-in the "+" of elements offers connectors and elements, but an element chosen there is placed without its joining
connector, and line ends show no "+" yet; both follow with a host update.

## 6. Modules

Each module has its own page with its elements, views and parameters:
[mechanical drives](../modules/mechanical-drives.md), [kinematics](../modules/kinematics.md),
[electrical](../modules/electrical.md), [electric machines](../modules/machines.md),
[automotive](../modules/automotive.md), [architecture](../modules/architecture.md),
[building services](../modules/building-services.md), [fluid power](../modules/fluid.md),
[diagrams](../modules/diagrams.md), [maps and routes](../modules/maps.md), [charts](../modules/charts.md),
[process & thermal](../modules/process.md), [energy systems](../modules/energy.md), [robotics](../modules/robotics.md), [math & geometry](../modules/geometry.md).

Each element lives in one module (review 2026-11-25): installations (sanitary, heating, pipes) in building services,
rotation and torque arrows in mechanical, cams in kinematics, the north arrow in maps. Elements with the same name but a
different meaning carry telling names (miniature circuit breaker / power circuit breaker, process column / structural
column, spring symbol / spring between two joints). Which modules the ribbon and the library show: InventaSketch ▸
Modules.
How the add-ins work on the slide (context bar, insert box with Ctrl+Space, multi-selection, assemblies, motion mode) is
described per host: [PowerPoint COM](../hosts/powerpoint-com.md), [PowerPoint web](../hosts/office-web.md).
