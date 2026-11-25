<img src="assets/branding/logo-256.png" width="96" alt="InventaSketch logo" align="right" />

# InventaSketch

**Quick technical sketches, schematic machine elements and patent-style drawings — directly in PowerPoint and LibreOffice.**

[Deutsch](README.de.md) · [Specification](docs/en/product/specification.md) · [Architecture](docs/en/architecture/overview.md) · [Roadmap](ROADMAP.md) · [User guide](docs/en/user-guide/README.md)

> **Status: early development (v0.1).** The engine core, domain modules and the web add-in are being built.
> Nothing here is production-ready yet; see the [roadmap](ROADMAP.md) and the [host capability matrix](docs/en/hosts/capability-matrix.md)
> for what actually works today.

InventaSketch is a quick-and-dirty prototyping tool for engineers, inventors and patent professionals. Drag in a gear,
a shaft, a bearing, an arrow or a flowchart step, scale it freely, snap it to its neighbours, add lead lines and
reference numerals — no dimensions, no module, no tooth count required. When you need more, the same objects carry
optional engineering parameters and can be checked against patent-office drawing rules and exported in monochrome.

## Highlights (target scope)

- **Gearboxes in seconds:** spur, internal, bevel, worm, planetary and differential gears; shafts and bearings in
  stick-diagram style (VDI 2127 / ISO 3952 conventions), conventional views (ISO 2203 style) and sections — snapping
  meshes gears on their pitch circles and puts hubs on shaft axes.
- **Patent figures:** reference numerals with straight or curved lead lines, consistent numbering across figures,
  hatching, FIG labels, sheet numbers, office profiles (DPMA, EPO, PCT, USPTO) with a source-linked preflight and
  black-and-white export.
- **Annotation toolkit:** arrows with many heads, curved and elbow connectors, dimensions, brackets, section and
  break lines, text boxes, callouts, technical-mechanics symbols, flowchart shapes.
- **Native objects first:** inserted figures are native, editable PowerPoint/LibreOffice shapes wherever the host
  allows it; SVG/PDF/PNG export.
- **Modular domains (available):** mechanics and gearboxes (incl. screw drives, freewheels, torque converters, CVT,
  strain wave and cycloidal drives, housings), kinematics (joints, linkages, cams, motion), electrical
  (schematic symbols, wiring, house installation, power electronics, illustrative PCB), electric machines (radial and
  axial flux, components, drive blocks), automotive/ADAS/HMI (vehicles, people, roads, sensors, displays), architecture
  and floor plans (walls, openings, rooms, stairs, furniture), diagrams (UML, ER, sequence, activity, system, BPMN),
  maps and routes, fluid power, patent figures, building services (heating, plumbing, ventilation, electrical additions
  with plan, section and schematic views of the same installation), charts and characteristic curves (XY, bar, torque–speed, efficiency map,
  pump curve, Bode plot, tolerance bands), process & thermal (P&ID equipment, valves, instrumentation tags, HVAC
  items, T-s, p-v and p-h cycle diagrams), energy systems (PV, wind, storage, e-mobility, hydrogen, heat pumps, grid
  connection and single-line diagrams across voltage levels) and robotics (articulated, SCARA, delta, gantry and mobile
  robots posed by their joint angles, grippers and tools, reach envelopes, work cells and safety equipment).
- **Clean UI:** light/dark/auto theme, German/English (automatic), floating context bar, right-click pie menu,
  interactive sidebar editor where the host does not allow on-slide interaction.

## Hosts

| Host | Platforms | Status |
|---|---|---|
| PowerPoint COM add-in (takes precedence when installed) | Windows | planned |
| PowerPoint web add-in (Office.js, interactive sidebar editor) | Windows, macOS, web | in progress |
| LibreOffice extension (Impress, Draw, Calc) | Windows, macOS, Linux | planned |

## Build

Requirements: .NET 10 SDK (the build script installs it user-locally if missing); Node.js 20+ for the web add-in.

```powershell
.\scripts\build.ps1
```

```bash
./scripts/build.sh
```

## Repository layout

```text
src/InventaSketch.Core            engine core (model, geometry, snapping, rendering, persistence)
src/InventaSketch.Modules.*       domain modules (Annotation, Patent, Mechanical, …)
src/InventaSketch.Engine          JSON engine API shared by all hosts
hosts/office-web                  PowerPoint web add-in (WebAssembly engine + TypeScript UI)
hosts/powerpoint-com              PowerPoint COM add-in (.NET Framework 4.8)
hosts/libreoffice                 LibreOffice extension (planned)
tests                             xUnit tests; tests/powerpoint-com: PowerPoint UI scenarios
docs/en, docs/de                  documentation (English canonical, German translation)
docs/adr                          architecture decision records
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [CLAUDE.md](CLAUDE.md) (working rules for humans and AI agents).

## Licence

[Apache License 2.0](LICENSE). Patent-rule information is provided as an aid only and is not legal advice.
