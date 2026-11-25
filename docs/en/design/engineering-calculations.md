# Engineering values and calculations – concept and catalogue

Status: concept and plan, 2026-11-25 (maintainer request). Calc 0 and Calc 1 (quantities, units, parser, unit
converter and calculator, relation engine, engine API, Ohm's law on the resistor) are implemented; the design decisions
are in section 10. Calc 2 (the PowerPoint user interface: level, Values button, pie, value card, values label, drag,
conflict badge) is implemented in COM, see section 11. Calc 3–6 are open. German version:
[../../de/design/engineering-calculations.md](../../de/design/engineering-calculations.md).

**Why.** Two maintainer requests from 2026-11-25:

> "E/E, electrical, machine elements, machines, fluid power, solar, … everywhere values can be entered it should be
> possible to compute other parameters, dynamically and interactively, e.g. voltage, current, power, resistance,
> capacitance, inductance … speed, torque, power (centre distance / number of teeth), ratio, flow, heat quantity …"

> "One can sketch quickly, but also learn and understand interactively and playfully, and draw and compute concretely,
> physically correct and technically precise, and check things for plausibility. Likewise for charts. Also across
> modules where sensible, relevant and possible."

This document defines (1) the principles of a small calculation layer in Core and how it appears in the hosts, (2) a
catalogue of formula families per domain with sources, (3) per element family what exists today and what is needed,
and (4) a phased implementation plan with acceptance criteria. Sources are listed in section 9 and cited as [S*n*].
We implement physics and textbook relations only. We name standards so users know where binding rules are, but we do
not copy their texts, tables or limit values (CLAUDE.md, section 2).

---

## 1. Principles

### 1.1 Three levels of use

The same element serves three ways of working. The level is a document setting (ribbon: *Values: Off · Explore ·
Engineer*), and the user can change it at any time. Off is the default, so Quick Sketch stays first.

| Level | Who / what for | What the user sees | What the user does |
|---|---|---|---|
| **Quick sketch** (Off) | a principle figure in seconds | no values, no cards, no warnings; labels only if typed | draw and scale; entering values stays possible (pie *Values ▸*), nothing is required |
| **Explore** (learning) | understand how things depend on each other | a compact value card on hover; grips and sliders on values; formulas written out step by step; arrows showing which way dependents move | drag a value and watch everything follow (teeth → ratio → speed → torque); try "what if" without breaking anything (Esc reverts) |
| **Engineer** (precise) | concrete, physically correct values | units of choice, significant digits, tolerances, the source of each formula, plausibility checks with rule and source, a calculation sheet | enter datasheet values, check a design, export the calculation as a table |

Explore and Engineer use the same relations. They differ only in presentation: Explore shows rounded values (3
significant digits), motion and formulas, while Engineer shows units, precision, tolerances and sources.

### 1.2 Values, relations and provenance

- A **variable** (`VarDef`) is a named, typed quantity of an element. It has a key, a bilingual name, a symbol per
  language, a *quantity kind*, a display unit, a typical range and a relevance (card, panel or engineer only). Example:
  resistor `R` "Resistance"/"Widerstand", kind *electric resistance*, unit Ω, card.
- Symbols follow ISO/IEC 80000 and IEC 60027-1 [S2, S3]. Where the languages differ by convention, the symbol is
  bilingual: torque is *T* in English and *M* in German, speed of rotation is *n* in both.
- A **relation** is an equation between variables, for example *U = R·I* or *n₂ = n₁·z₁/z₂*. It has an id, a bilingual
  name, the solver for each variable it can be solved for (explicit inverse, or a bracketed numeric root search where
  no closed form exists), its validity conditions ("linear, unloaded divider", "ideal transformer") and a source id.
- Every value has a **provenance**:

| Provenance | Meaning | Look |
|---|---|---|
| *entered* | typed, dragged or read from a datasheet by the user | normal weight |
| *default* | the type's default (e.g. 430 Wp module), not entered | grey, "default" in the tooltip |
| *linked* | taken over from a connected element or a linked chart | chain pictogram |
| *derived* | computed by a relation | "=" in front, formula on hover |

- **Entered values are never overwritten.** Derived values are never stored as truth; the engine recomputes them on
  demand. Defaults give way when they contradict entered values, and the card says so ("default 430 Wp replaced by
  4 × 405 Wp from the string").
- **Solving in any direction.** The user enters whichever values they know, and the engine computes the rest when the
  system is determined. Enter *U* and *I* and you get *R* and *P*; enter *P* and *R* and you get *U* and *I*.
- **Under-determined:** the card shows how many values are missing and which ones would suffice: "One more value:
  *I*, *R* or *P*".
- **Over-determined:** if all values agree within the tolerance of the relation, the card shows a check mark ✓. If
  not, it shows a **conflict**. Both the entered value and the value the relation implies appear, along with the
  relation and the entries involved. The engine changes nothing. The user picks which entry to clear, or which to keep
  and recompute ("Keep *U* and *I*, derive *R*"), and that pick is one undo step.
- **Geometry stays independent.** Values never move or resize the drawing on their own (Quick Sketch first: scaling
  changes `Size`, never parameters). Where values imply a size, an explicit action applies it. Examples: "True size
  from module" (exists for gears), "Centre distance from values", "Duct width from flow".

### 1.3 Quantities, units, precision

- **Internal values are coherent SI** [S1]: m, kg, s, A, K, plus rad for plane angles. Each quantity kind has a
  dimension vector and a list of allowed display units. Some kinds share a dimension, such as torque and energy (N·m vs
  J) or frequency and speed of rotation (Hz vs 1/min). They are kept apart by the quantity kind, so a torque field
  offers N·m and kN·m but never J.
- **Temperature vs. temperature difference.** These are two kinds. *θ* accepts °C or K; *Δθ* accepts K only. In
  German, ΔT in "°C" is accepted on input and shown as K.
- **Parsing input** is one shared parser in Core. It replaces the three ad-hoc ones: Charts `Fmt.ParseInput`, Energy
  `Act.Number` and Building services `Bs.ParseMm`. It accepts:
  - a decimal comma or point
  - SI prefixes p n µ/u m k M G
  - the IEC 60062 letter code, with the letter as the decimal marker, as in `4k7`, `4R7` and `2n2` [S4]
  - units with or without a space (`10µF`, `1,5 kW`, `16 A`, `1500 rpm`, `1500/min`, `40 l/min`, `2.5 mm²`, `20 °C`)
  - fractions for ratios (`20/0,4`, `3:1`)

  A unit of the wrong kind is rejected with a message ("16 A is a current; this field expects a voltage"). A bare
  number takes the field's display unit.
- **Display.** Values are shown with significant digits: 3 in Explore, and 2–6 in Engineer as a document setting. SI
  prefixes are chosen automatically within the field's unit family (4.7 kΩ, 2.2 µF, 1.2 MW). The decimal separator
  follows the UI language. Entered values keep their text as typed, so "4k7" stays "4k7" on the label unless the user
  switches the label to normalised form.
- **No rounding drift.** Computation uses full double precision. Rounding happens only for display. Tolerance checks
  use the relation's tolerance: 1 × 10⁻⁶ relative for exact relations, and a declared tolerance for empirical ones,
  e.g. 2 % for a belt drive with slip.
- **Tolerances (Engineer).** An entered value may carry a tolerance (`4k7 ±5 %`, `100 µF −20/+80 %`). Derived values
  then show an interval computed by interval arithmetic [S37], for example *I* = 48.9 … 54.1 mA. This is worst-case,
  not statistics.
- **Standard series** (E12/E24 resistor values, preferred fuse ratings, module series) are useful for "nearest standard
  value" but are standard tables (IEC 60063, IEC 60898-1, ISO 54) [S5, S11, S16]. They are an open item: ship them only
  after a recorded decision (source and licence in `THIRD-PARTY-NOTICES.md`). Until then, free entry is the only path.

### 1.4 What is "relevant" per element

Every element declares at most **four card variables**, the ones a person would write next to the symbol (resistor:
*R*, *U*, *I*, *P*; gear: *z*, *m*, *n*, *T*). All other variables are in the Values panel, and engineering-only ones
(tolerance, temperature coefficient, efficiency split) are in the Engineer section. The order is fixed per type. The
card never grows beyond five lines; long lists live in the panel.

### 1.5 How it shows in the UI

| Place | Behaviour |
|---|---|
| **Floating bar** | One button *Values* (new pictogram `cmd-values`, "x=", with a short label) in group 1. It appears only for elements that have relations and only when the level is Explore or Engineer. Off keeps the bar unchanged. |
| **Pie menu** | *Edit ▸ Values ▸*: one entry per variable ("Voltage *U* … 230 V"), so values can be typed with `WithInput`. Derived ones show "= 460 W" and open the explanation. The fixed eight places stay unchanged (interaction-ux.md, section 6). |
| **Value card on hover** (Explore, Engineer) | A small card beside the element, 2–5 lines: entered values in normal weight, derived values with "=", a ✓ or conflict mark, "1 more value: …". Clicking a value edits it in place. In Explore, each value has a small slider or grip. It is overlay only and is never part of the slide. |
| **Label on the element** | "Show on drawing" per variable. The label is a real part of the element (part key `values`), so it is styled, exported and kept like any other text. For electrical parts, the existing `value` label shows the chosen variables ("4k7 / 0.25 W"). |
| **Grips on the drawing** (Explore) | The existing value grips (`DragHandles`) are extended to physical values: drag a gear's centre distance, a cylinder's stroke, a chart's operating point. Dependents update live. Release makes one undo step; Esc cancels. |
| **Step-by-step formula** (Explore) | Shown on a click on a derived value: "*P* = *U*·*I* = 230 V · 2.00 A = 460 W". For chains, one line per step ("*n*₂ = *n*₁·*z*₁/*z*₂ = 1500 min⁻¹ · 18/54 = 500 min⁻¹"), with arrows ↑↓ that show which way each dependent moves while dragging. |
| **Sidebar inspector** | A *Values* section listing every variable with provenance, unit picker, tolerance (Engineer), source of each relation (Engineer) and "Clear". |
| **Checks** | A small amber dot on the element (overlay only) and a *Checks* list (ribbon tools). See section 4. |

**Accessibility and language.** Every name, hint and message is bilingual (`I18nText`), with one-line hints in the
style of interaction-ux.md. Units and symbols are not translated (SI), except where the symbol convention differs
(*T*/*M*).

### 1.6 Propagation through connections

Values move between elements only through **explicit connections** that already exist in the model. Possible links
include meshes and couplings (`Links`, `Coupling`), shaft assemblies (`GearTrain`), wire nets (attachments, as used by
the netlist export), and pipe, duct and cable attachments with `PortMedia`. Each connection kind contributes **port
relations**:

| Connection | What is equal or conserved | Existing base |
|---|---|---|
| Gear mesh, belt, chain | speed ratio *n*₁/*n*₂ = *z*₂/*z*₁ (or *d*₂/*d*₁); torque *T*₂ = *T*₁·*i*·*η* | `Links.Ratio`, `GearTrain.Speeds` (relative speeds, today only for animation) |
| Shaft, coupling, gears fixed on a shaft | same *n*; torque passes through, minus branch torques | `GearTrain` (shafts, compound gears, carriers via Willis) |
| Planetary set | Willis equation *n*_s − *n*_c = −(*z*_r/*z*_s)(*n*_r − *n*_c); torque balance | `GearTrain` (carriers) |
| Electrical series (one branch) | same current *I*; voltages add; *R*, *L* add, 1/*C* adds | wire nets (attachments, junctions) |
| Electrical parallel (same two nets) | same voltage *U*; currents add; 1/*R* adds, *C* adds | wire nets |
| Node with all but one current known | Kirchhoff's current law: Σ*I* = 0 | wire nets |
| PV string / array | modules in series: *U* adds, *I* equal; strings in parallel: *I* adds | `energy.pv.string` has `modules` |
| Pipe or duct in series | same volume flow *V̇* (incompressible); pressure drops add | `PortMedia` `fluid`, `heating.*`, `water.*`, `air.*` |
| Pipe junction | Σ*V̇* = 0; for heat carriers also Σ(*V̇*·*θ*) mixing | junctions in Fluid/BS lines |
| Cable | same *I* at both ends; *U*_end = *U*_start − Δ*U* | `energy.line.cable` (kV, dc), house cable |

**Network reduction, not simulation.** Resistor, inductor and capacitor networks are reduced only by series/parallel
steps. A bridge or meshed network that cannot be reduced gets the message "not reducible here – use a circuit
simulator". Each port relation is local, so propagation is a chain of single-unknown steps (section 1.8).

### 1.7 Where it stops (out of scope)

- **No circuit simulation.** That means no nodal or mesh analysis of meshed networks, no non-linear device models, no
  transient simulation, and no AC load flow. Closed-form first-order responses (RC/RL step, RLC resonance) are in scope
  as formulas and linked charts.
- **No power-system studies:** short-circuit currents (IEC 60909), selectivity and protection coordination curves,
  earthing and touch voltage.
- **No standard tables:** ampacity tables and correction factors (IEC 60364-5-52 Annex B), design temperatures and
  U-values (EN 12831-1 national annexes), cylinder bore series (ISO 3320), E-series (until decided, section 1.3).
  Users enter *I*_z, *U*-values and design temperatures themselves.
- **No strength verification:** gear load capacity (ISO 6336), shaft fatigue (DIN 743), bolt calculation (VDI 2230),
  spring fatigue (EN 13906-1 beyond *F* = *c*·*s*). We show nominal stress at most, marked "nominal, no verification".
- **No CFD, pressure surge or two-phase flow; no refrigerant property data.** The p–h chart stays schematic.
- **No PV yield simulation.** Irradiation is an input number, for example from a tool such as PVGIS, and is not built
  in.
- **No certification claims.** No result is labelled "compliant with" a standard. Checks say "hint", give the rule,
  name the source, and leave the decision to the user.

### 1.8 Solver (Core, host-neutral)

1. Collect the variables of all elements in the scope: the selection's assembly, or the whole slide in a background
   pass. Collect their entered, default and linked values.
2. Collect the relations: local ones per element, port relations per connection, and assembly relations (absolute
   speeds = `GearTrain.Speeds` × one known speed).
3. **Local propagation** [S37]. Repeatedly take a relation with exactly one unknown and solve it. Use the explicit
   inverse, or a bracketed root search (Brent) within the variable's physical range, as for the Colebrook friction
   factor or a temperature from LMTD. Each solved variable records its trace: the relation and its inputs.
4. Relations with all variables known are checked against their tolerance. Any mismatch becomes a conflict carrying
   both traces.
5. Defaults are used only for variables still unknown after step 3, and the pass is then repeated. A default never
   creates a conflict; it gives way.
6. What remains unknown is reported per element with the smallest set of variables that would determine it.

The solver is deterministic, bounded (each relation is solved at most once per variable and per pass) and free of
side effects. Its result is a value table and a trace keyed by (object id, variable). The engine API gets these calls:
`values(objectId)`, `setValue(objectId, key, text)`, `clearValue`, `explain(objectId, key)`, `checks(scope)` and
`dragValue(objectId, key, value)` (live preview, no undo step until release). The budgets are:

- 1 000 elements and 5 000 relations in under 50 ms on .NET 10
- under 200 ms in WebAssembly
- a single element in under 1 ms, so dragging stays smooth

### 1.9 Plausibility checks

A check is a rule with the following fields:

- id, element types and severity (*info*, *hint* or *warning*; nothing blocks)
- a bilingual message that says what was found and what the user can do
- the rule as a formula
- a source

Checks run after solving and show as an amber dot on the element (overlay only) and in the *Checks* list. They can be
muted per element or per document, and the mute is stored. Checks never change values or geometry. The catalogue is in
section 4.

### 1.10 Charts linked to element values

A chart can be **linked** to one or more elements. The link is stored on the chart as a list of object ids plus a
binding of chart parameters to element variables. A linked chart:

- redraws when a linked value changes;
- shows the **operating point** (an intersection or a marked value) as a marker with a grip;
- **reads back**: dragging the marker writes the new value into the element as an entered value, one undo step for
  both. Examples: pump flow, motor operating torque;
- can be **unlinked**, which freezes its current values as the chart's own parameters.

The catalogue is in section 5.

### 1.11 Cross-module chains

Ports carry pairs of **power variables**: an *effort* and a *flow* whose product is power. This is the bond-graph view
of multi-domain systems [S38]. Each element along a chain has a transfer relation with an efficiency:

| Domain | Effort × flow | Power |
|---|---|---|
| Electrical DC | *U* × *I* | *P* = *U*·*I* |
| Electrical AC (1~/3~) | *U* × *I* with cos *φ*, *f* | *P* = *U*·*I*·cos *φ* (1~), √3·*U*·*I*·cos *φ* (3~) |
| Rotational mechanics | *T* × *ω* (*ω* = 2π*n*) | *P* = *T*·*ω* |
| Translational mechanics | *F* × *v* | *P* = *F*·*v* |
| Hydraulics / pneumatics | *p* × *V̇* | *P* = Δ*p*·*V̇* |
| Heat carrier (water, air) | *θ*_flow, *θ*_return × *V̇* (or *ṁ*) | *Φ* = *ṁ*·*c*·Δ*θ* |

The chains are in section 6.

---

## 2. Catalogue of formula families

Columns: relation(s), inputs ↔ outputs (any direction where the relation allows it), typical range, source. Symbols per
ISO/IEC 80000 [S2]. "↔" means the relation is solved for whichever variable is missing.

### 2.1 Electrical DC and AC

| Family | Relations | Variables (↔) | Typical range | Source |
|---|---|---|---|---|
| Ohm's law, power | *U* = *R*·*I*; *P* = *U*·*I* = *I*²*R* = *U*²/*R* | *U, I, R, P* (any two give the rest) | mV … kV; µA … kA; mΩ … MΩ | [S7] |
| Series / parallel | *R* = Σ*R*ᵢ; 1/*R* = Σ1/*R*ᵢ; *L* likewise; *C* inverse (1/*C* = Σ1/*C*ᵢ in series, *C* = Σ*C*ᵢ in parallel) | network → equivalent value | – | [S7] |
| Voltage divider (unloaded) | *U*₂ = *U*·*R*₂/(*R*₁+*R*₂) | *U, U*₂*, R*₁*, R*₂ | – | [S7] |
| Time constants | *τ* = *R*·*C*; *τ* = *L*/*R*; *u*_C(*t*) = *U*(1 − e^(−*t*/*τ*)); 63 % at *τ*, ≈ 99 % at 5*τ* | *R, C, L, τ, t* | µs … s | [S7] |
| Reactance | *X*_L = 2π*f*·*L*; *X*_C = 1/(2π*f*·*C*) | *f, L, C, X* | Hz … MHz | [S7] |
| Impedance (series RLC) | \|*Z*\| = √(*R*² + (*X*_L − *X*_C)²); *φ* = arctan((*X*_L − *X*_C)/*R*) | *R, X*_L*, X*_C*, Z, φ* | – | [S7] |
| Resonance, quality | *f*₀ = 1/(2π√(*L·C*)); *Q* = (1/*R*)√(*L*/*C*) (series); bandwidth *B* = *f*₀/*Q* | *L, C, R, f*₀*, Q, B* | – | [S7] |
| RC/RL filter | *f*_c = 1/(2π*R·C*), *f*_c = *R*/(2π*L*) | *R, C, L, f*_c | – | [S7] |
| AC power, power factor | *S* = *U*·*I*; *P* = *S*·cos *φ*; *Q* = *S*·sin *φ*; *S*² = *P*² + *Q*² | *U, I, S, P, Q, φ, λ* | cos *φ* 0.7 … 1 | [S6, S7] |
| Three-phase (balanced) | *S* = √3·*U*_LL·*I*; *P* = √3·*U*_LL·*I*·cos *φ*; *U*_LN = *U*_LL/√3 | *U, I, P, S, cos φ* | 230/400 V [S13] | [S7] |
| Ideal transformer | *U*₁/*U*₂ = *N*₁/*N*₂ = *I*₂/*I*₁; *S* = √3·*U*·*I* (3~) | *U*₁*, U*₂*, N*₁*/N*₂*, I, S* | – | [S7, S15] |
| Energy | *E* = *P*·*t*; cost = *E* × price (price is user input) | *P, t, E* | Wh … GWh | [S1] |

### 2.2 Cables and protection

We implement the physics. The binding limits, ampacity tables and correction factors are in IEC 60364-5-52 (clause
525 voltage drop, Annex G informative; Annex B current-carrying capacities) and IEC 60364-4-43, and in their national
adoptions DIN VDE 0100-520 and -430 [S8, S9]. The user enters *I*_z and the permitted voltage drop.

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Conductor resistance | *R* = *ρ*·*l*/*A*; *ρ*(*θ*) = *ρ*₂₀(1 + *α*(*θ* − 20 °C)); *ρ*₂₀,Cu = 0.017241 Ω·mm²/m, *α*_Cu ≈ 0.00393 K⁻¹; Al per [S12] | *l, A, θ, R* | 0.5 … 300 mm² | [S12] |
| Voltage drop (single-phase, two conductors) | Δ*U* = 2·*l*·*I*·(*R*′ cos *φ* + *X*′ sin *φ*); with *X*′ ≈ 0 for small sections: Δ*U* = 2·*l*·*I*·*ρ*/*A* | *l, I, A, ΔU*, Δ*u* % | – | [S7, S8] |
| Voltage drop (three-phase, balanced) | Δ*U* = √3·*l*·*I*·(*R*′ cos *φ* + *X*′ sin *φ*) | as above | – | [S7, S8] |
| Section from permitted drop (idea) | *A* = 2·*l*·*I*·*ρ*·cos *φ*/Δ*U*_perm (1~), next larger section is user choice | *l, I, ΔU*_perm → *A*_min | – | [S7] |
| Cable losses | *P*_loss = *k*·*I*²·*R* (*k* = 2 for 1~, 3 for 3~) | *I, R, P*_loss | – | [S7] |
| Overload coordination (check only) | *I*_B ≤ *I*_n ≤ *I*_z; *I*₂ ≤ 1.45·*I*_z | *I*_B (load), *I*_n (device), *I*_z (user-entered) | – | [S9] |
| MCB, fuse, RCD (labels) | MCB type B/C/D + *I*_n (e.g. "B16"), fuse *I*_n + class ("gG 35 A"), RCD *I*_Δn ("30 mA") parsed into label variables; no tripping curves | *I*_n, characteristic, *I*_Δn | 6 … 125 A; 10/30/300 mA | [S10, S11] |

### 2.3 Electronics (simple only)

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| LED series resistor | *R* = (*U*_S − *n*·*U*_F)/*I*_F; *P*_R = *I*_F²·*R* | *U*_S*, n, U*_F*, I*_F*, R, P*_R | *U*_F 1.8 … 3.5 V; *I*_F 2 … 20 mA (datasheet) | [S7] |
| Transistor as a switch (BJT) | *I*_B ≥ *k*·*I*_C/*B* (overdrive *k* ≈ 2 … 5); *R*_B = (*U*_in − *U*_BE)/*I*_B | *U*_in*, U*_BE*, I*_C*, B, R*_B | *U*_BE ≈ 0.7 V | [S7] |
| Op-amp gain (ideal) | inverting *A* = −*R*_f/*R*_in; non-inverting *A* = 1 + *R*_f/*R*_g; dB = 20 lg\|*A*\| | *R*, *A* | – | [S7] |
| Capacitor energy, charge | *Q* = *C*·*U*; *E* = ½*C*·*U*² | *C, U, Q, E* | – | [S7] |
| Inductor energy | *E* = ½*L*·*I*² | *L, I, E* | – | [S7] |
| Battery (circuit symbol) | *U* = cells × cell voltage (cell voltage user input) | cells, *U* | – | [S35] |

### 2.4 Machine elements

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Spur gear geometry | *d* = *m*·*z*; *d*_a = *m*(*z* + 2); *d*_f = *m*(*z* − 2.5); *p* = π*m*; helical: *m*_t = *m*_n/cos *β* | *m, z, d, d*_a*, β* | *m* 0.5 … 25 mm; *z* ≥ 17 without undercut at *α* = 20° | [S16, S18] |
| Centre distance (zero shift) | *a* = *m*(*z*₁ + *z*₂)/2 external; *a* = *m*(*z*₂ − *z*₁)/2 internal | *m, z*₁*, z*₂*, a* | – | [S16] |
| Ratio, speed, torque | *i* = *n*₁/*n*₂ = *z*₂/*z*₁ (= *d*₂/*d*₁); *T*₂ = *T*₁·*i*·*η*; *P* = 2π·*n*·*T*; *P*[kW] = *T*[N·m]·*n*[min⁻¹]/9549 | *z, i, n, T, P, η* | *η* 0.97 … 0.99 per spur/helical stage; worm 0.4 … 0.9 | [S18] |
| Multi-stage train | *i*_total = Π*i*_k; *η*_total = Π*η*_k | stages | – | [S18] |
| Worm gear | *i* = *z*_wheel/*z*_worm (starts); efficiency from lead angle and friction (input *η* in first version) | *z*₁ (starts), *z*₂*, i, η* | – | [S18] |
| Planetary (Willis) | *n*_s − *n*_c = −(*z*_r/*z*_s)(*n*_r − *n*_c); assembly condition (*z*_s + *z*_r)/*N*_planets integer; *z*_p = (*z*_r − *z*_s)/2 (zero shift) | *z*_s*, z*_r*, z*_p*, n*_s*, n*_r*, n*_c | stationary ratio 3 … 10 | [S18, S39] |
| Rack | *v* = *ω*·*d*/2; *F* = 2*T*/*d* | *m, z, n, v, F* | – | [S18] |
| Belt drive | *i* = *d*₂/*d*₁ (slip 1 … 2 % as declared tolerance); *v* = π·*d*₁·*n*₁; *F*_t = *P*/*v* | *d*₁*, d*₂*, n, v, P, F*_t | *v* ≤ 30 m/s (V-belt, typical) | [S18, S20] |
| Chain drive | *i* = *z*₂/*z*₁; mean *v* = *z*·*p*·*n*; *F*_t = *P*/*v* | *z, p, n, v, P* | – | [S18, S20] |
| Screw drive | *v* = *P*_h·*n*; *F* = 2π·*T*·*η*/*P*_h; *T* = *F*·*P*_h/(2π*η*) | *P*_h (lead), *n, v, T, F, η* | ball screw *η* ≈ 0.9; lead screw 0.2 … 0.6 | [S18, S23] |
| Shaft (torsion, nominal) | *T* = *P*/(2π*n*); *τ*_t = 16*T*/(π*d*³) (solid); *φ* = *T*·*l*/(*G*·*I*_p), *I*_p = π*d*⁴/32 | *P, n, T, d, τ*_t*, φ, l, G* | *G*_steel ≈ 81 GPa | [S18] (strength: DIN 743 [S22], out of scope) |
| Bearing life | *L*₁₀ = (*C*/*P*)^*p* [10⁶ rev], *p* = 3 ball, 10/3 roller; *L*₁₀h = 10⁶·*L*₁₀/(60·*n*) | *C, P, n, L*₁₀*, L*₁₀h | 10 000 … 100 000 h (application) | [S19] |
| Springs | *F* = *c*·*s*; helical compression *c* = *G*·*d*⁴/(8·*D*³·*n*_f); torsion spring *M* = *c*_φ·*φ* | *F, s, c, d, D, n*_f*, G* | – | [S21, S18] |
| Couplings, clutches | *n* and *T* pass through; slip clutch *T* ≤ *T*_slip; freewheel passes one direction only | *T, n* | – | [S18] |
| Translational | *P* = *F*·*v*; *W* = *F*·*s* | *F, v, s, P, W* | – | [S1] |

### 2.5 Electric machines and drives

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Synchronous speed | *n*_s = 60·*f*/*p*_pairs (pole pairs = pole count / 2; the model stores the pole count) | *f*, poles, *n*_s | 50/60 Hz | [S14, S15] |
| Slip (induction) | *s* = (*n*_s − *n*)/*n*_s; generator for *s* < 0 | *n*_s*, n, s* | *s* 1 … 6 % at rating | [S15] |
| Power, torque, speed | *P*_mech = 2π·*n*·*T*; *η* = *P*_out/*P*_in; *P*_el = √3·*U*·*I*·cos *φ* (3~) | *P, T, n, η, U, I, cos φ* | *η* 0.8 … 0.97 (IE classes [S14]) | [S14, S15] |
| Torque–speed (ASM, Kloss) | *T*/*T*_k = 2/(*s*/*s*_k + *s*_k/*s*) | *T*_k (breakdown), *s*_k | – | [S15] |
| DC / PM machine | *U* = *R*·*I* + *k*·*ω*; *T* = *k*·*I* (SI: torque constant = voltage constant) | *U, I, R, k, ω, T* | – | [S15] |
| Electrical frequency (PMSM) | *f* = *n*·*p*_pairs/60 | *n*, poles, *f* | – | [S15] |
| Wheel / vehicle | *v* = *ω*·*r*_dyn; *F* = *T*/*r*_dyn; *P* = *F*·*v* | *r*, *n*, *T*, *v*, *F* | – | [S18] |
| Combustion engine (block) | *P* = 2π·*n*·*T* (no thermodynamics) | *n, T, P* | – | [S1] |

### 2.6 Kinematics

| Family | Relations | Variables | Source |
|---|---|---|---|
| Degree of freedom (exists) | *F* = 3(*n* − 1) − 2*j*₁ − *j*₂ (Grübler/Kutzbach) | links, joints | [S24] |
| Grashof condition (code exists, unused) | *s* + *l* ≤ *p* + *q* → one link rotates fully | link lengths | [S24] |
| Slider-crank | stroke = 2*r*; *v*_max ≈ *ω*·*r* (rod much longer than crank) | *r, l, ω* | [S24] |
| Quick return | time ratio = (180° + *α*)/(180° − *α*) from the extreme positions | lengths | [S24] |
| Transmission angle | *μ* from the position solution; usual design limit *μ* ≥ 40° is a hint only | lengths, input angle | [S24] |
| Velocities | numerical derivative of the position solution × input speed (no new physics) | input *ω* | [S24] |

### 2.7 Fluid power (ISO 1219 symbols)

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Flow and velocity | *V̇* = *A*·*v*; *A* = π*d*_i²/4 | *V̇, d*_i*, v* | see checks 4.x | [S27] |
| Cylinder | *A*₁ = π*D*²/4; *A*₂ = π(*D*² − *d*²)/4; *F* = *p*·*A*·*η*_hm (minus back pressure × other area); *v* = *V̇*/*A*; area ratio *φ* = *A*₁/*A*₂ | *D, d, p, F, V̇, v, η*_hm | 10 … 350 bar (hydraulic), 6 … 10 bar (pneumatic) | [S26] |
| Pump | *V̇* = *V*_g·*n*·*η*_v; *T* = *V*_g·Δ*p*/(2π·*η*_hm); *P*_in = Δ*p*·*V̇*/*η*_t | *V*_g*, n, V̇, Δp, T, P, η* | *V*_g 1 … 500 cm³ | [S26] |
| Motor | *n* = *V̇*·*η*_v/*V*_g; *T* = *V*_g·Δ*p*·*η*_hm/(2π) | as pump | – | [S26] |
| Hydraulic power | *P* = Δ*p*·*V̇*; *P*[kW] = *p*[bar]·*V̇*[l/min]/600 | *p, V̇, P* | – | [S26] |
| Pressure drop in a pipe | Δ*p* = *λ*·(*l*/*d*)·*ρ*·*v*²/2; *Re* = *v*·*d*/*ν*; laminar *λ* = 64/*Re* (*Re* < 2300); turbulent Colebrook–White, or Blasius for smooth pipes | *v, d, l, ν, ρ, Δp* | oil *ν* ≈ 10 … 100 mm²/s | [S27] |
| Orifice / valve | *V̇* = *α*·*A*·√(2Δ*p*/*ρ*); datasheet scaling *V̇* = *V̇*_nom·√(Δ*p*/Δ*p*_nom) | *A, Δp, V̇* | – | [S26] |
| Accumulator | *p*₀·*V*₀ = *p*₁·*V*₁ = *p*₂·*V*₂ (isothermal), *p*·*V*^*κ* const. (adiabatic, *κ* ≈ 1.4); Δ*V* = *V*₁ − *V*₂ | *p*₀*, p*₁*, p*₂*, V*₀*, ΔV* | – | [S26] |
| Pneumatic air consumption | *V̇*_n = *A*·*s*·*f*·(*p*_e + *p*_amb)/*p*_amb (per stroke and direction) | *A, s, f, p*_e | – | [S26] (flow ratings: ISO 6358 [S25]) |

### 2.8 Thermal and process (ISO 10628 symbols)

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Heat quantity | *Q* = *m*·*c*·Δ*θ*; water *c* ≈ 4.19 kJ/(kg·K) | *m, c, Δθ, Q* | – | [S29] |
| Heat flow (carrier) | *Φ* = *ṁ*·*c*·Δ*θ* = *ρ*·*V̇*·*c*·Δ*θ*; water: *Φ*[kW] ≈ 1.163·*V̇*[m³/h]·Δ*θ*[K] | *V̇, Δθ, Φ* | – | [S29, S30] |
| Conduction / transmission | *Φ* = *U*·*A*·Δ*θ*; *U* = 1/(1/*h*_i + Σ*d*/*λ* + 1/*h*_e) | *U, A, Δθ, Φ* | – | [S29] |
| Heat exchanger (LMTD) | *Φ* = *U*·*A*·Δ*θ*_lm; Δ*θ*_lm = (Δ*θ*_a − Δ*θ*_b)/ln(Δ*θ*_a/Δ*θ*_b); counter- and parallel-flow; other layouts via correction factor (input) | four temperatures, *U, A, Φ* | – | [S29] |
| Pump (centrifugal) | *P*_hyd = *ρ*·*g*·*V̇*·*H*; *P*_shaft = *P*_hyd/*η*; affinity *V̇* ∝ *n*, *H* ∝ *n*², *P* ∝ *n*³ | *V̇, H, n, P, η* | – | [S28] |
| System curve | *H* = *H*_stat + *k*·*V̇*² (*k* from pressure drops) | *H*_stat*, k, V̇* | – | [S28] (exists in `chart.pump`) |
| Fan | *P* = Δ*p*·*V̇*/*η*; affinity laws as for pumps | Δ*p, V̇, P, η* | – | [S28, S30] |
| Vessel / storage | *V* = π*D*²·*h*/4 (cylinder); stored heat *Q* = *ρ*·*V*·*c*·Δ*θ* | *V, D, h, Δθ, Q* | – | [S29] |
| Thermodynamic cycles (exist) | Otto / Diesel efficiency (`process.chart.pv`) | *ε*, *φ*, *κ* | – | existing code |

### 2.9 HVAC and building services

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Air heat flow | *Φ* = *ρ*·*c*_p·*V̇*·Δ*θ*; air ≈ 0.34 W·h/(m³·K): *Φ*[W] ≈ 0.34·*V̇*[m³/h]·Δ*θ*[K] | *V̇, Δθ, Φ* | – | [S30] |
| Air change | *V̇* = *n*·*V*_room (air change rate *n* in h⁻¹, user input; per-person rates from EN 16798-1 are not copied) | *n, V, V̇* | – | [S30, S31] |
| Heat recovery | *θ*_supply = *θ*_out + *η*_t(*θ*_extract − *θ*_out) | temperatures, *η*_t | – | [S30] |
| Heating load (simplified, learning aid) | *Φ*_HL = (Σ*U*ᵢ·*A*ᵢ + 0.34·*n*·*V*)·(*θ*_int − *θ*_e) | areas, *U*-values, *n*, *V*, temperatures (all user input) | – | idea of EN 12831-1 simplified methods [S31] |
| Radiator output | *Φ* = *Φ*_50·(Δ*θ*/50 K)^*n*, *n* ≈ 1.3 (datasheet); Δ*θ* as logarithmic or arithmetic mean over room | *Φ*_50*, n, θ*_flow*, θ*_return*, θ*_room | – | EN 442-2 characteristic [S31] |
| Underfloor loop | *Φ* = *q*·*A*; *V̇* = *Φ*/(*ρ*·*c*·Δ*θ*); loop length ≈ *A*/spacing | *A, q, spacing, Δθ* | – | [S30] |
| Heating curve | *θ*_flow(*θ*_e) from the radiator law between the design point and *θ*_e = *θ*_room (slope and parallel shift as parameters) | *θ*_flow,design*, θ*_e,design*, θ*_room*, n* | – | [S30] |
| Pipe / duct sizing | *v* = 4*V̇*/(π*d*_i²); round duct *d*; rectangular *A* = *a*·*b*, *d*_h = 2*ab*/(*a* + *b*) | *V̇, d*_i*, a, b, v* | see checks | [S27, S30] |
| DN vs. inner diameter | DN is a nominal size; the inner diameter depends on the pipe series. The user enters *d*_i, or DN is used as an approximation flagged "≈" | DN, *d*_i | – | [S30] |
| Hot water storage | *Q* = *V*·*ρ*·*c*·Δ*θ* (1000 l, 40 K → 46.5 kWh) | *V, Δθ, Q* | – | [S29] |

### 2.10 Solar and energy

| Family | Relations | Variables | Typical range | Source |
|---|---|---|---|---|
| Module at STC | *P*_mpp = *U*_mpp·*I*_mpp; STC = 1000 W/m², 25 °C cell, AM 1.5 | *P, U*_oc*, U*_mpp*, I*_sc*, I*_mpp | 300 … 700 Wp | [S33] (datasheet data: EN 50380) |
| Temperature correction | *U*_oc(*θ*) = *U*_oc,STC·(1 + *β*·(*θ* − 25 °C)); likewise *U*_mpp, *I*_sc with *α*, *P* with *γ* | *β* ≈ −0.25 … −0.35 %/K | – | [S33, S34] (procedures: IEC 60891) |
| Cell temperature | *θ*_cell = *θ*_amb + (NMOT − 20 °C)·*G*/800 W/m² | *θ*_amb*, G*, NMOT | NMOT 40 … 47 °C | [S33, S34] |
| Irradiance scaling | *I* ≈ *I*_STC·*G*/1000 W/m²; *U* changes logarithmically (ignored in the first version, stated) | *G, I* | – | [S34] |
| String / array | *U*_string = *N*_s·*U*_mod; *I*_array = *N*_p·*I*_string; *P* = *N*_s·*N*_p·*P*_mod | *N*_s (exists: `modules`)*, N*_p | – | [S34] |
| Max string voltage | *U*_oc,max = *N*_s·*U*_oc(*θ*_min); min MPP voltage *U*_mpp,min = *N*_s·*U*_mpp(*θ*_max) | *θ*_min (user, site), *θ*_max | – | [S33] (IEC 62548-1, operating temperature) |
| DC/AC ratio | *R* = *P*_DC,STC/*P*_AC,rated | – | typically 1.0 … 1.3 | [S34] |
| Yield estimate | *E* = *P*_STC·(*H*_POA/1 kW/m²)·PR; specific yield *E*/*P*_STC | *H*_POA (input), PR (input, typ. 0.75 … 0.9) | – | [S33] (PR: IEC 61724-1) |
| Inverter | *P*_AC = *η*·*P*_DC; *I*_AC = *P*_AC/(√3·*U*·cos *φ*) (3~) | *η, P, U, I* | *η* 0.95 … 0.98 | [S33] (EN 50530) |
| Battery | *E* = *U*_nom·*Q*_n; *E*_usable = *E*·DoD; C-rate = *I*/(*Q*_n/1 h); *t* = *E*_usable/*P*; round trip *η*_rt | *U, Q*_n [Ah]*, E* [kWh]*, DoD, I, P, t* | 0.2 … 2 C | [S35] (current notation: IEC 61434) |
| EV charging | *P* = √3·*U*·*I* (3~, e.g. 400 V · 16 A ≈ 11 kW); *t* ≈ Δ*E*/(*P*·*η*) (taper ignored, stated) | *P, U, I, E, t* | – | [S7] |
| Wind turbine | *P* = ½·*ρ*·*A*·*v*³·*c*_p; *c*_p ≤ 16/27 (Betz) | *ρ, D, v, c*_p | *c*_p 0.35 … 0.5 | [S36] |
| Heat pump | *Φ*_H = COP·*P*_el; COP ≤ COP_Carnot = *T*_H/(*T*_H − *T*_C) (kelvin) | COP, *P*_el*, Φ*_H*, θ*_source*, θ*_flow | COP 2.5 … 5 | [S29, S32] |
| CHP | *η*_total = (*P*_el + *Φ*_th)/*P*_fuel; power-to-heat ratio *σ* = *P*_el/*Φ*_th | *P*_el*, Φ*_th*, P*_fuel | – | [S29] |
| Electrolyser / fuel cell | *ṁ*_H₂ = *η*·*P*/LHV; *P* = *η*·*ṁ*·LHV (LHV of H₂ ≈ 33.3 kWh/kg) | *P, η, ṁ* | – | [S29] |
| Hydrogen tank | stored energy *E* = *m*·LHV | *m, E* | – | [S29] |
| Grid transformer | *I* = *S*/(√3·*U*) per side | *S, U*_HV*, U*_LV*, I* | – | [S7] |

---

## 3. Element families: today and needed

"Today" is taken from the code (2026-11-25). The new parameters are ordinary optional `ParamDef`s with a `Unit`. A
`VarDef` binds an existing parameter in its stored unit, so documents need no migration. For example, Energy `voltage`
is stored in kV and the variable reads it as kV.

### 3.1 Electrical (`InventaSketch.Modules.Electrical`)

| Family | Today | Relations | New parameters (proposal) |
|---|---|---|---|
| Resistor, potentiometer | `value` (free text, only drawn, never parsed), `ref`, `showLabel` | Ohm, power, series/parallel, divider (pot: wiper ratio) | parse `value` as *R* (`4k7`); `voltage`, `current`, `power`, `prated` (rating), `tolerance`; pot: `wiper` 0…1 |
| Capacitor (incl. polarized) | `value` | *τ* = *RC*, *X*_C, *Q* = *CU*, *E*, series/parallel | parse `value` as *C*; `voltage`, `urated` |
| Inductor | `value`, `core` | *τ* = *L/R*, *X*_L, *E*, resonance with C | parse `value` as *L*; `current` |
| Transformer | `core` | ideal ratio, *S* | `u1`, `u2` (or ratio text "230/12 V"), `power` |
| Source (dc/ac/current), battery | `kind`, `cells` | *U*, *I*, *P*; AC *f* | parse `value` as *U* (or *I* for current source); `frequency` for ac |
| Diode, LED | `value` | LED series resistor | `uf`, `if` (or parse "2.1 V 20 mA") |
| Transistor (BJT/MOSFET/IGBT) | polarity, channel, envelope | switch base resistor (BJT only) | `beta`, `ic` (engineer) |
| Fuse, breaker, RCD | `value` only | overload coordination check | parse `value` as *I*_n + characteristic ("B16", "gG 35 A"); RCD *I*_Δn ("30 mA") |
| Motor / generator (symbol) | `supply` | *P*, *U*, *I*, cos *φ* | `power`, `voltage`, `current`, `pf` (or link to a Machines element) |
| Switch, contactor | `contact`, `poles` | passes *I* (closed) or breaks the net (open; exists as `closed`) | `irated` (engineer) |
| Wire / net | `route`, `hops` | series current, node KCL, net voltage label | none (values live on parts and nets) |
| House installation (socket, light, board, cable) | heights, gangs, conductors | load per circuit, cable drop, coordination | socket/light `power`; cable `length`, `section`, `material`; circuit device *I*_n |
| Op-amp, logic, ECU block | gate kind, inputs, pins | op-amp gain (external resistors) only | none (gain is derived from the resistors around it) |

### 3.2 Mechanical (`InventaSketch.Modules.Mechanical`)

| Family | Today | Relations | New parameters (proposal) |
|---|---|---|---|
| Spur / helical / internal gear, bevel | `teeth`, `module` (advanced, no default), `helix`, `hub`; "True size from module" exists | geometry, centre distance, ratio, speed, torque, power, efficiency | `speed`, `torque`, `power`, `efficiency` (per mesh, engineer), `pressureAngle` (engineer, 20° default), `helixAngle` (if helix) |
| Worm / worm wheel | `starts`, `teeth`, `module` | worm ratio, efficiency | `efficiency` |
| Rack | `module` | *v*, *F* from pinion | `force`, `speed` (linear) |
| Planetary set | `planets`, `stepped`; teeth only in explode specs | Willis, assembly condition | `zs`, `zr` (*z*_p derived) |
| Carrier | `fixed`, `innerRatio` | Willis (via `GearTrain`) | none |
| Pulley + belt | `hub`; belt `ratio` (diameter ratio, drawing only) | belt drive | pulley `diameter`; belt `slip` (engineer) |
| Sprocket + chain | `teeth`; chain none | chain drive | chain `pitch` |
| CVT | `ratio` (drawing) | *i* | – (`ratio` becomes a variable) |
| Screw drive | `pitch` (lead, advanced), `kind` | screw relations | `efficiency`, `force`, `speed` |
| Shaft | `hollow` | torsion (nominal), shared *n* | `diameter`, `innerDiameter` (hollow), `length`, `material` (*G*) |
| Bearings | none | *L*₁₀ | `c` (dynamic load rating), `load` *P*, `kind` ball/roller (exponent) |
| Springs (compression, tension, torsion) | `coils` | *F* = *c*·*s*, *c* from geometry | `rate`, `force`, `travel`, `wire`, `meanDiameter`, `material` |
| Couplings, clutches, freewheel, flywheel | `reverse`, `lockup`, `hub` | pass-through; flywheel *E* = ½*J*ω² | flywheel `inertia`; slip clutch `tslip` |
| Torque / rotation arrows | `direction` | display a linked *T* or *n* | `value` (text, parsed) or link to the element they point at |

### 3.3 Machines (`InventaSketch.Modules.Machines`)

| Family | Today | Relations | New parameters (proposal) |
|---|---|---|---|
| Radial / axial machine (cross-sections) | `poles`, `slots`, `phases`, `mode`, `rotor`, `layout` | *n*_s, *f*, slip, slot/pole numbers (slots per pole and phase *q* = *Q*/(2*p*·*m*)) | `frequency`, `speed`, `power`, `torque`, `voltage`, `current`, `pf`, `efficiency` |
| Drivetrain e-machine, engine | `kind`, `layout`, `cylinders` | power/torque/speed | `power`, `torque`, `speed` |
| Drive blocks: motor, inverter, battery, DC link, gearbox, differential, wheel | `mode`, `phases`, `dclink`; gearbox has **no ratio** | chain battery → inverter → motor → gearbox → differential → wheel | battery `voltage`, `capacity`; inverter `power`, `efficiency`; gearbox `ratio`, `efficiency`; differential `ratio`; wheel `radius` |
| Annotations (flux, rotation, torque) | `direction` | display linked values | link |

### 3.4 Kinematics (`InventaSketch.Modules.Kinematics`)

| Family | Today | Relations | New parameters (proposal) |
|---|---|---|---|
| Four-bar, slider-crank, Scotch yoke, toggle, quick return, cam | lengths (mm), `angle`, `dof` badge (Grübler; `Linkages.Grubler`, `Linkage.Dof`) | Grashof class (`IsGrashof` exists, unused), stroke, time ratio, transmission angle, output velocity | `inputSpeed` (min⁻¹) |
| `kin.motion.dof` badge | `links`, `lower`, `higher`, `formula` | Grübler (exists, with formula) | none; it is the model for the step-by-step display |

### 3.5 Fluid power (`InventaSketch.Modules.Fluid`)

Today **no numeric parameters**, only choices (medium, variable displacement, rod, cushion, ways, centre, …).

| Family | Relations | New parameters (proposal) |
|---|---|---|
| Pump, motor | displacement, flow, torque, power | `displacement` (cm³), `speed`, `pressure`, `efficiencyV`, `efficiencyHm` (engineer) |
| Cylinder (single, double) | force, speed, area ratio | `bore`, `rod`, `stroke`, `pressure`, `force`, `flow` |
| Rotary actuator | *T* = *V*_g·Δ*p*/(2π) | `displacement` |
| Relief / reducing / sequence valve | set pressure (limits *p*) | `setPressure` |
| Throttle, flow valve, directional valve | orifice scaling | `qnom`, `dpnom` (engineer) |
| Accumulator | Boyle / adiabatic | `volume`, `p0`, `p1`, `p2` |
| Line (working, pilot, drain, hose) | *V̇* = *A*·*v*, pressure drop | `innerDiameter`, `length` |
| Gauge, flow meter | show the value at their port (linked) | none |

### 3.6 Process (`InventaSketch.Modules.Process`)

Today: choices only. The p–v chart already computes Otto/Diesel efficiency.

| Family | Relations | New parameters (proposal) |
|---|---|---|
| Centrifugal pump, displacement pump, compressor, fan | pump power, affinity, link to `chart.pump` | `flow`, `head` (or `dp`), `power`, `efficiency`, `speed` |
| Heat exchangers (shell & tube, plate, air cooler) | *Φ* = *U*·*A*·Δ*θ*_lm, energy balance on both sides | `duty`, `area`, `u`, four temperatures, flows |
| Vessels, columns | volume, stored heat | `volume` (or `diameter`, `height`) |
| Lines | flow, velocity | `dn`/`innerDiameter`, `flow` |
| Valves | orifice scaling (*K*_v value as user input) | `kv` (engineer) |

### 3.7 Building services (`InventaSketch.Modules.BuildingServices`)

| Family | Today | Relations | New parameters (proposal) |
|---|---|---|---|
| Pipe (`bs.pipe`) | `medium`, `dn` (mm, no default), `height` | *v* from *V̇* and *d*_i, heat flow, pressure drop | `innerDiameter` (else DN ≈), `flow`, `length` |
| Duct (`bs.duct`) | `kind`, `width` (drawing) | *v* from *V̇*, *d*_h | `shape` round/rect, `height2` (rect side), `flow` |
| Boiler, heat pump | heights only | power, COP, flow from Δ*θ* | `power`, `flowTemp`, `returnTemp`, `cop` (heat pump) |
| Radiator, TRV | heights | radiator output | `phi50`, `exponent`, `roomTemp` |
| Floor loop, manifold | `spacing`, `circuits` | loop length, flow | `area`, `heatFlux` |
| Pump, mixer, expansion vessel | heights | flow, head; mixing temperature; expansion volume (idea) | `flow`, `head` |
| Ventilation unit, terminal | `kind` | air flow, heat recovery | `flow`, `recovery` |
| Water heater (`bs.san.heater`) | `coil` | stored heat, heat-up time | `volume`, `power` |
| Electrical installation (`bs.elec.*`) | kind | as Electrical house | as 3.1 |

### 3.8 Energy (`InventaSketch.Modules.Energy`)

This module is the richest today. Its `Act.Number` input with clamping and its `Fmt.Si` output are the model for the
shared parser and formatter.

| Family | Today | Relations | New parameters (proposal) |
|---|---|---|---|
| PV module | `power` (Wp, 430) | STC, temperature correction | `voc`, `vmp`, `isc`, `imp`, `betaVoc`, `gammaP`, `alphaIsc`, `nmot` (engineer) |
| PV string | `power` (Wp), `modules` (2–24); shows "6 × 430 Wp" and the total | string voltage, max/min voltage | link to module values or own copies; `tmin`, `tmax` (site) |
| PV array | `power` (kWp), `rows`, `columns` (drawing) | array = strings in parallel | `strings` (default rows) |
| Wind turbine | `power` (MW), `rotor` | wind power, Betz | `diameter`, `windSpeed`, `cp` |
| Genset | `power` (kVA), `fuel` | *P* = *S*·cos *φ* | `pf` |
| Inverter | `power` (kW), `phases`, `kind` | DC/AC ratio, *η*, AC current, string window | `udcMax`, `mpptMin`, `mpptMax`, `idcMax`, `efficiency` |
| Converter | `power`, `kind` | *η* | `efficiency` |
| Battery storage | `capacity` (kWh), `soc` (%) | *E* = *U*·*Q*, C-rate, run time | `voltage`, `ah`, `dod`, `cmax`, `roundTrip` |
| Charger, EV | `power` (kW), `dc`; EV `battery` (kWh) | charging power, time | `current`, `voltage` (AC 3~ default 400 V) |
| Building load | `power` (kW peak) | load balance | `annualEnergy` (kWh/a) |
| Grid infeed, transformer, substation, busbar | `voltage` (kV), `hv`/`lv`, `power` (kVA/MVA) | currents per side, voltage matching | – (values exist) |
| Cable (`energy.line.cable`) | `voltage` (kV), `dc`, `conductors` | voltage drop, losses | `length`, `section`, `material`, `current` |
| Heat pump, CHP, heat storage | `power` (kW), `thermal`, `volume` (l) | COP, CHP efficiency, stored heat | `cop`, `fuelPower`, `deltaT` |
| Electrolyser, fuel cell, H₂ tank | `power`, `mass` (kg), `pressure` (bar) | H₂ mass flow, stored energy | `efficiency` |
| Flow band (`energy.line.flow`) | `weight` (graphic band width) | optional: width ∝ linked power (Sankey style) | `value` (linked power) |

### 3.9 Charts (`InventaSketch.Modules.Charts`) and Architecture

- `chart.torque` (`tmax`, `nbase`, `nmax`), `chart.efficiency` (`etamax`), `chart.pump` (`h0`, `qmax`, `hstat`, `qop`;
  unitless; operating point computed) and `chart.bode` (`fc`, `zeta`, `gain`) are the first link targets. Their
  parameters become variables with units, and the unitless pump chart gets units when linked.
- Architecture rooms have a polygon. Area and volume (with a room height) are derived values that feed the heating
  load and the air change (cross-module).
- Annotation dimensions measure geometry. A dimension between two gear axes shows the centre distance; in Explore,
  dragging it solves for the module or the teeth, as the user chooses (section 1.2: geometry only on request).

---

## 4. Plausibility checks (catalogue)

All checks are hints: overlay only, mutable, and they never change anything. "User limit" means a value the user sets
in the document's check settings, because the binding limit is in a standard or a datasheet that we do not copy.

| Id | Elements | Rule | Severity | Source |
|---|---|---|---|---|
| `mech.mesh.module` | meshing gears | equal (normal) module and pressure angle | warning | [S16] |
| `mech.mesh.distance` | gear pair with a dimension or entered *a* | *a* = *m*(*z*₁ ± *z*₂)/2 (no profile shift modelled) | hint | [S16] |
| `mech.gear.undercut` | gear | *z* < 2/sin²*α* (≈ 17 at 20°) without profile shift | info | [S18] |
| `mech.planetary.assembly` | planetary set | (*z*_s + *z*_r)/*N*_planets integer; *z*_r − *z*_s even | warning | [S18, S39] |
| `mech.power.balance` | any chain | *P*_out ≤ *P*_in (more out than in is impossible); *P*_out ≈ *P*_in·Π*η* | warning | [S1] |
| `mech.bearing.life` | bearing | *L*₁₀h below the user's required life | hint | [S19] |
| `mech.belt.speed` | belt | *v* above the user limit (typical 30 m/s for V-belts) | hint | [S18] |
| `mech.screw.selflock` | lead screw | back-driving possible when *η* > 0.5 (hint for vertical loads) | info | [S18] |
| `kin.grashof` | four-bar | no link can rotate fully, but a crank input is set | hint | [S24] |
| `kin.dof` | mechanism with a drive | *F* ≠ number of drives | hint | [S24] |
| `elec.coord.overload` | protective device + cable/circuit | *I*_B ≤ *I*_n ≤ *I*_z violated (*I*_z user-entered) | warning | [S9] |
| `elec.rcd.additional` | socket circuit | no RCD with *I*_Δn ≤ 30 mA found upstream of a socket circuit | hint (national rules differ) | [S10] |
| `elec.cable.drop` | cable | Δ*u* above the user limit | hint | [S8] |
| `elec.r.power` | resistor | *P* > *P*_rated | warning | [S7] |
| `elec.led.current` | LED | *I* > the user's or datasheet *I*_F,max | warning | [S7] |
| `elec.voltage.mismatch` | element ↔ cable/busbar | port voltage ≠ connected line voltage (Energy ports carry kV today) | warning | – (consistency) |
| `elec.kcl` | node | Σ*I* ≠ 0 with all currents entered | warning | [S7] |
| `mach.slip.sign` | induction machine in motor mode | *n* > *n*_s (that is generator operation) | info | [S15] |
| `mach.efficiency` | any converter | *η* > 1 or *P*_out > *P*_in | warning | [S1] |
| `fluid.cyl.force` | cylinder | required *F* > *p*_max·*A*·*η*_hm (*p*_max from relief valve or pump) | warning | [S26] |
| `fluid.velocity` | line | *v* outside the user range per line kind (defaults from textbooks: suction 0.5 … 1.5 m/s, pressure 3 … 6 m/s, return 2 … 4 m/s) | hint | [S26] |
| `fluid.pressure.relief` | pump without a relief valve in the circuit | no pressure limitation found | hint | [S25] (ISO 4413) |
| `heat.velocity` | heating / water pipe | *v* outside the user range (heating distribution ≈ 0.3 … 1 m/s, textbook) | hint | [S30] |
| `air.velocity` | duct | *v* outside the user range (residential ≈ 2 … 4 m/s, textbook) | hint | [S30] |
| `heat.cross` | heat exchanger | temperature cross (cold outlet above hot inlet; parallel flow: cold outlet above hot outlet) | warning (impossible) | [S29] |
| `heat.cop.carnot` | heat pump | COP > COP_Carnot | warning (impossible) | [S29] |
| `heat.balance` | heat source ↔ consumers | Σ*Φ*_consumers > *Φ*_source | hint | [S29] |
| `pv.voc.max` | string ↔ inverter | *N*_s·*U*_oc(*θ*_min) > *U*_DC,max | warning | [S33] (IEC 62548-1) |
| `pv.mpp.min` | string ↔ inverter | *N*_s·*U*_mpp(*θ*_max) < MPPT minimum | hint | [S33, S34] |
| `pv.isc.max` | array ↔ inverter input | *N*_p·*I*_sc > *I*_DC,max | warning | [S33] |
| `pv.dcac` | PV ↔ inverter | DC/AC ratio outside the user range (default 0.8 … 1.5) | info | [S34] |
| `bat.crate` | battery | C-rate above the user's or datasheet maximum | hint | [S35] |
| `chart.nopoint` | linked pump/system chart | curves do not intersect (no operating point) | hint | [S28] |
| `value.range` | any variable | value far outside the typical range of its variable (factor 1000), e.g. 2300 V on a socket | info ("typo?") | – |

---

## 5. Charts linked to element values

| Chart | Linked elements and variables | Operating point | Read back (drag) |
|---|---|---|---|
| Motor torque–speed (`chart.torque`) | machine: *T*_max, base speed, *n*_max, *P*; ASM: Kloss curve from *T*_k, *s*_k; load torque from the driven chain | intersection of motor and load torque | dragging the point sets the load torque or speed |
| Efficiency map (`chart.efficiency`) | machine *η*_max; operating point (*n*, *T*) | marker | – |
| Pump / fan vs. system (`chart.pump`) | pump: *H*₀, *V̇*_max (or datasheet points); system: *H*_stat, *k* from the connected pipes' pressure drops; speed via affinity laws | intersection (exists: `OperatingPoint`) | dragging the point sets the pump flow (exists as a grip on `qop`) |
| PV I–V and P–V (new preset) | module/string: *U*_oc, *I*_sc, *U*_mpp, *I*_mpp, *N*_s, *θ*, *G* (single-diode model fitted to the datasheet points [S34]) | MPP marker; inverter MPPT window as a band; *U*_DC,max as a limit line | dragging the temperature or irradiance moves the curve (Explore) |
| Bode plot (`chart.bode`) | RC: *f*_c = 1/(2π*RC*); RLC: *f*₀, *ζ* = (*R*/2)√(*C*/*L*) | −3 dB marker | dragging *f*_c changes *C* or *R* (the user's choice, the other stays) |
| Step response (function chart, PT1/PT2) | RC/RL: *τ*; RLC: *f*₀, *ζ* | 63 % at *τ* | – |
| Gear train speed/torque diagram (new preset) | gear train: per shaft *n*, *T*, *P* (bars or steps along the train) | – | – |
| Heating curve (new preset) | heat generator / heating circuit: design point, room temperature, exponent | current outdoor temperature (input) | dragging the design point changes the flow temperature |
| Load / generation balance (new preset) | energy elements: Σ*P* generation vs. Σ*P* load | – | – |

Linked charts use the charts v2 part model (`docs/en/design/charts-v2.md`). The operating point is a part with a
grip, and the link shows as a chain pictogram in the chart's bar with "Linked to *Pump P1* – unlink".

---

## 6. Cross-module chains

| Chain | What flows | Transfer relations | Modules |
|---|---|---|---|
| E-motor → coupling → gearbox → differential → wheel | *n*, *T* (→ *P*); at the wheel *v*, *F* | motor *P*_mech = *η*·*P*_el; coupling pass-through; gearbox *n*₂ = *n*₁/*i*, *T*₂ = *T*₁·*i*·*η*; wheel *v* = *ω*·*r*, *F* = *T*/*r* | Machines, Mechanical (`GearTrain`), Automotive |
| Battery → inverter → motor (drive blocks) | *U*, *I* (DC) → *U*, *I*, *f* (AC) → *n*, *T* | *P*_AC = *η*_inv·*P*_DC; *f* = *n*·*p*/60 | Machines, Energy |
| PV modules → string → inverter → distribution board → circuits | *U*, *I* (DC) → *P*, *U*, *I* (AC) | string sums; inverter *η*, DC/AC ratio; board: Σ*I* per phase; circuits: protection coordination and cable drop | Energy, Electrical (house), Building services |
| Grid → transformer → busbar → feeders → loads | *U*, *S*, *I* | ideal transformer; busbar Σ*I*; load *P*, cos *φ* | Energy, Electrical |
| Pump → pipe network → heat exchanger → radiator / floor loop | *V̇*, Δ*p* (→ *P*_hyd); *θ*_flow, *θ*_return (→ *Φ*) | pump curve vs. system curve; pipe Δ*p*; heat exchanger LMTD; radiator law; junctions mix | Process, Building services, Energy (heat pump), Charts |
| Heat pump → storage → heating circuit → rooms | *Φ*, *θ*, *V̇* | COP; stored *Q*; heating curve; room heat load from the architecture room area | Energy, Building services, Architecture |
| Hydraulic power unit → valves → cylinder / motor | *p*, *V̇* (→ *F*, *v* or *T*, *n*) | pump *V̇* = *V*_g·*n*; relief limits *p*; cylinder *F*, *v*; motor *T*, *n* | Fluid, Machines (drive motor) |
| Linkage driven by a motor | *n* → output velocity, stroke rate | kinematic position solution, derivative | Kinematics, Machines |
| Ventilation unit → ducts → terminals → rooms | *V̇*, *θ* | Σ*V̇* at branches; heat recovery; air heat flow | Building services, Architecture |

**Rules.** The chain is found through existing connections only. Values pass only between matching media
(`PortMedia.Fits`). A port without a matching variable stops the chain silently; it never guesses. The solver scope for
a chain is the connected component of the selection. The background pass covers the slide. Nothing propagates across
slides in the first versions.

---

## 7. Implementation plan

Every phase follows the definition of done in CLAUDE.md:

- model, rendering, persistence round trip and unit tests
- bilingual names
- EN/DE docs and host capability notes
- gallery cases for labels

Hosts come in this order: COM first, then web (WebAssembly engine, same API), then LibreOffice.

**Calc 0 – Quantities and units (Core).** Add `QuantityKind`, `Unit`, `UnitCatalog`, `ValueParser` (SI prefixes,
decimal comma, IEC 60062 letter code, units, fractions, tolerances) and `ValueFormat` (significant digits, automatic
prefix, language). The Charts, Energy and Building services parsers delegate to it, with behaviour preserved.

*Accept:*
- parse/format tests for `4k7`, `4R7`, `2n2`, `10 µF`, `10uF`, `1,5 kW`, `1500 rpm`, `1500/min`, `40 l/min`,
  `2,5 mm²`, `20 °C` (temperature) vs. `20 K` (difference), `230/12 V`, `4k7 ±5 %`
- a wrong-kind unit is rejected with a bilingual message
- format → parse round trip within the shown digits
- `netstandard2.0` and `net10.0` both pass
- the existing chart, energy and BS input tests pass unchanged

**Calc 1 – Relation engine (Core + Engine API).** Add `VarDef`, `Relation` (with source id and tolerance) and
`ObjectType.Variables/Relations` (default empty), plus port relations and the solver of section 1.8. Provenance,
conflicts and the explanation trace come with it. Engine calls: `values`, `setValue`, `clearValue`, `explain`,
`dragValue` and `checks`. Entered values are stored in parameters (existing keys with their units); derived values are
never stored.

*Accept:*
- any two of *U, I, R, P* determine the other two
- three consistent entries give ✓; inconsistent ones give a conflict that names both values and leaves all
  parameters unchanged
- defaults give way without a conflict
- "missing: one of {…}" is reported
- JSON round trip keeps entered values and their text
- solver budgets of section 1.8 are met in a benchmark test
- deterministic results (the same input gives the same trace)

**Calc 0/1 addition – unit converter and calculator** (maintainer request 2026-11-25). `Units.Convert(value, from, to)`
with kind checks and °C/°F/K offsets, the display units per kind, a safe unit-aware expression calculator
(`UnitCalculator`: "230 V * 16 A" → 3.68 kW, "12 mm + 0.5 in", "1500 rpm in rad/s", "4k7 || 10k", "2 kWh / 230 V" → Ah)
and the engine methods `convertUnit`, `calc` and `units`. The calculator popup (More menu, value card) comes with Calc 2.

*Status:* Calc 0 and Calc 1 including the converter and calculator are done (section 10).

**Calc 2 – COM user interface (Explore and Engineer).** This phase adds:
- the level setting in the ribbon
- the *Values* bar button
- pie *Edit ▸ Values ▸*
- the value card on hover with inline edit
- the inspector *Values* section
- labels on the element (part `values`)
- grips and sliders on values with live update
- the step-by-step formula view
- conflict marks
- one undo step per entry or drag; Esc cancels

*Accept (PowerPoint scenario `values-ohm`):*
- a resistor gets 230 V and 2 A; the card shows *R* = 115 Ω and *P* = 460 W with formulas
- dragging *R* in Explore updates *I* and *P* live
- release is one undo step, and Esc restores
- an exported slide contains only the chosen labels, no card or marks
- Off shows nothing new

*Status:* done in COM (section 11) except the inspector *Values* section, the card on hover and on-drawing value grips
for the resistor (the grip is in the card); web and LibreOffice follow.

**Calc 3 – Electrical and machine elements, first chains and checks.** This phase adds:
- the element relations of sections 2.1–2.6 and 3.1–3.4
- parsing of the electrical `value`
- network reduction (series/parallel)
- the voltage divider and LED resistor
- gear trains with absolute *n*, *T*, *P* along `GearTrain` (Willis included) and efficiencies
- belt, chain and screw drives, springs, bearing life
- motors (*n*_s, slip, *P*–*T*–*n*)
- the drive-block chain
- the check set for gears, power balance, overload, RCD and resistor power

*Accept:*
- a three-stage spur train with *z* and *n*_in, *T*_in shows *n*, *T* and *P* on every shaft, matching a hand
  calculation
- modules 2 and 2.5 in one mesh give the module warning
- a bridge network reports "not reducible"
- "4k7" on a resistor in a 5 V series circuit with an LED (2.1 V) gives *I* ≈ 0.62 mA
- a B16 MCB on a circuit with *I*_z = 13 A (user) gives the coordination warning
- docs for the Electrical, Mechanical and Machines modules list the new variables

*Status (machine elements):* the gear, drive, spring, bearing, motor and drive-block items and their checks are done,
see section 12.

**Calc 4 – Fluid, thermal/process, building services, energy/solar, cross-module chains.** This phase adds:
- the relations of sections 2.7–2.10 and 3.5–3.8
- port flows across `PortMedia`
- the remaining checks

*Accept:*
- a string of 10 modules with *U*_oc = 49.5 V, *β* = −0.27 %/K and *θ*_min = −10 °C shows *U*_oc,max ≈ 541.8 V and
  warns against an inverter *U*_DC,max of 500 V
- a pump with *V*_g = 10 cm³ at 1450 min⁻¹ feeding a 50/28 mm cylinder at 100 bar shows *F* and *v* both ways
- a radiator chain shows *Φ* from flow, temperatures and *Φ*₅₀/*n*
- a room's area and air change give the air flow and duct velocity
- the PV → inverter → board chain shows the AC current per phase

*Status:* all five accept items are done in the Fluid, Process, Building services and Energy modules, with unit tests
(section 13); the board is the Energy module's busbar or meter, not an Electrical distribution board.

**Calc 5 – Linked charts.** This phase adds:
- linking of section 5: torque–speed, pump/system, PV I–V/P–V, Bode RC/RLC, step response, gear train diagram,
  heating curve
- operating points with grips
- read-back and unlink

*Accept:*
- changing the pump's *H*₀ redraws the linked chart and moves the operating point
- dragging the point changes the pump flow (one undo step for chart and element)
- unlinking freezes the values
- the PV chart shows the MPP and the inverter window

**Calc 6 – Engineer extras.** This phase adds:
- tolerances with interval results
- the calculation sheet: a table on the slide (Annotation table), CSV, Markdown/LaTeX
- a sources pane: each relation with its [S*n*] entry, bilingual
- check settings (user limits, mute list)
- unit preferences (bar/MPa, min⁻¹/s⁻¹, kWh/MJ)
- the decision on standard series (E-series, preferred ratings) and their licence

*Accept:*
- a 4k7 ±5 % resistor shows *I* as an interval
- the calculation sheet of the gear example lists quantity, symbol, value, unit, formula and source
- muting a check survives a save and reload

**Web and LibreOffice.** These follow after each COM phase. The engine API is shared. Web: the card in the context bar
and the task pane, grips on the stage. LibreOffice: the Values panel in the sidebar first, on-canvas grips with the
planned overlay.

*Accept:* the same scenarios as COM give the same values (engine tests are host-free), and the host notes list what
is missing.

**Parallel work.** The coordinator owns Calc 0/1 (Core, Engine) and the hosts. Later, agents own one module each
(`src/InventaSketch.Modules.<X>/Values*.cs` plus its tests and docs) and add relations and checks without touching
Core.

---

## 8. Host behaviour (summary)

| Host | Explore / Engineer UI | Grips | Labels | Checks |
|---|---|---|---|---|
| PowerPoint COM (first) | value card as an owner-drawn popup next to the floating bar; inspector section; pie *Edit ▸ Values ▸* | existing overlay grips (`DragHandles`) extended | native text part `values` in the unit (formatting with PowerPoint tools is folded back like other parts) | amber dot in the overlay, *Checks* pane |
| Office web (WebAssembly) | card in the context bar layer (`contextbar.ts`), task pane section | stage grips | as SVG/Office shapes | as COM |
| LibreOffice | sidebar panel (UNO) first | with the planned on-canvas overlay | native text | sidebar list |

---

## 9. Sources

Standards are named by number and title; their texts and tables are not reproduced. Textbooks are named in their
current editions.

- **[S1]** BIPM: *The International System of Units (SI)*, 9th edition, 2019.
- **[S2]** ISO 80000 / IEC 80000 *Quantities and units* (parts 1 general, 3 space and time, 4 mechanics,
  5 thermodynamics, 6 electromagnetism).
- **[S3]** IEC 60027-1 *Letter symbols to be used in electrical technology – Part 1: General*.
- **[S4]** IEC 60062 *Marking codes for resistors and capacitors* (letter code with the letter as decimal marker).
- **[S5]** IEC 60063 *Preferred number series for resistors and capacitors* (E series; not copied, section 1.3).
- **[S6]** IEC 60050 *International Electrotechnical Vocabulary* (Electropedia), terms for power and power factor.
- **[S7]** Textbooks of electrical engineering and electronics: G. Hagmann, *Grundlagen der Elektrotechnik* (AULA);
  P. Horowitz, W. Hill, *The Art of Electronics*, 3rd ed. (Cambridge University Press, 2015); U. Tietze, Ch. Schenk,
  E. Gamm, *Halbleiter-Schaltungstechnik* (Springer Vieweg).
- **[S8]** IEC 60364-5-52 *Low-voltage electrical installations – Part 5-52: Selection and erection of electrical
  equipment – Wiring systems* (consolidated Ed. 3.1, 2024; clause 525 voltage drop, Annex G informative); DIN VDE
  0100-520.
- **[S9]** IEC 60364-4-43:2023 *Protection for safety – Protection against overcurrent* (coordination of conductors
  and protective devices); DIN VDE 0100-430.
- **[S10]** IEC 60364-4-41 *Protection for safety – Protection against electric shock* (additional protection by
  RCDs); DIN VDE 0100-410.
- **[S11]** IEC 60898-1 (circuit-breakers for household installations), IEC 60269-1 (low-voltage fuses), IEC 61008-1 /
  IEC 61009-1 (RCCBs / RCBOs).
- **[S12]** IEC 60228 *Conductors of insulated cables*; IEC 60028 *International standard of resistance for copper*;
  IEC 60889 (hard-drawn aluminium wire).
- **[S13]** IEC 60038 *IEC standard voltages*.
- **[S14]** IEC 60034-1 *Rotating electrical machines – Rating and performance*; IEC 60034-30-1 (efficiency classes).
- **[S15]** R. Fischer, *Elektrische Maschinen* (Hanser); A. Hughes, B. Drury, *Electric Motors and Drives*, 5th ed.
  (Newnes, 2019).
- **[S16]** ISO 21771 (cylindrical involute gears and gear pairs – concepts and geometry); ISO 54 (modules); ISO 53
  (basic rack); DIN 3960.
- **[S17]** ISO 6336 (load capacity of spur and helical gears) – named as out of scope.
- **[S18]** Roloff/Matek, *Maschinenelemente* (Springer Vieweg); Niemann, Winter, Höhn, *Maschinenelemente*
  (Springer); K.-H. Decker, *Maschinenelemente* (Hanser).
- **[S19]** ISO 281:2007 *Rolling bearings – Dynamic load ratings and rating life*.
- **[S20]** ISO 5292 (V-belts and V-ribbed belts – calculation of power ratings); ISO 606 (short-pitch transmission
  roller chains and sprockets); ISO 10823 (guidance on the selection of roller chain drives).
- **[S21]** EN 13906-1/-2/-3 *Cylindrical helical springs made from round wire and bar – Calculation and design*.
- **[S22]** DIN 743 *Calculation of load capacity of shafts and axles* – out of scope.
- **[S23]** ISO 3408 (ball screws).
- **[S24]** H. Kerle, B. Corves, M. Hüsing, *Getriebetechnik* (Springer Vieweg); J. J. Uicker, G. R. Pennock,
  J. E. Shigley, *Theory of Machines and Mechanisms* (Oxford University Press).
- **[S25]** ISO 1219-1 (graphical symbols); ISO 4413 (hydraulic fluid power – general rules and safety requirements);
  ISO 4414 (pneumatics); ISO 3320 (cylinder bores and rod diameters – named, not copied); ISO 6358 (pneumatic
  flow-rate characteristics).
- **[S26]** D. Will, N. Gebhardt, *Hydraulik – Grundlagen, Komponenten, Systeme* (Springer Vieweg); Bosch Rexroth,
  *The Hydraulic Trainer*, Vol. 1.
- **[S27]** F. M. White, *Fluid Mechanics* (McGraw-Hill); C. F. Colebrook (1939), J. Inst. Civil Eng. 11; L. F. Moody
  (1944), Trans. ASME 66.
- **[S28]** J. F. Gülich, *Centrifugal Pumps* (Springer); ISO 9906 (rotodynamic pumps – hydraulic performance
  acceptance tests).
- **[S29]** VDI e.V. (ed.), *VDI Heat Atlas* / *VDI-Wärmeatlas* (Springer); F. P. Incropera et al., *Fundamentals of
  Heat and Mass Transfer* (Wiley).
- **[S30]** Recknagel, Sprenger, Albers, *Taschenbuch für Heizung + Klimatechnik* (current edition) – handbook values
  for velocities and air heat flow.
- **[S31]** EN 12831-1:2017 (design heat load, space heating load; revision prEN 12831-1:2025 in progress); EN 442-2
  (radiators – test methods and rating); EN 16798-1 / EN 16798-3 (ventilation); DIN 1946-6 (residential ventilation);
  DIN 1988-300 / EN 806-3 (drinking water pipe sizing).
- **[S32]** EN 14511 and EN 14825 (heat pumps – rating, seasonal performance).
- **[S33]** IEC 60904-3 (reference spectral irradiance, STC); IEC 60891:2021 (temperature and irradiance corrections of
  I–V characteristics); IEC 61215-2 (NMOT); EN 50380 (datasheet information of PV modules); IEC 62548-1:2023 (PV arrays
  – design requirements, operating temperature); IEC 61724-1 (PV system performance, performance ratio); EN 50530
  (inverter efficiency).
- **[S34]** K. Mertens, *Photovoltaics: Fundamentals, Technology and Practice* (Wiley); H. Häberlin, *Photovoltaics:
  System Design and Practice* (Wiley, 2012); W. De Soto, S. A. Klein, W. A. Beckman (2006), "Improvement and
  validation of a model for photovoltaic array performance", Solar Energy 80, 78–88; M. G. Villalva, J. R. Gazoli,
  E. R. Filho (2009), "Comprehensive approach to modeling and simulation of photovoltaic arrays", IEEE Trans. Power
  Electronics 24(5).
- **[S35]** IEC 61434 (designation of current in secondary cell standards, *I*_t = *C*_n/1 h); *Linden's Handbook of
  Batteries* (McGraw-Hill).
- **[S36]** E. Hau, *Wind Turbines* (Springer); A. Betz (1920), maximum power coefficient 16/27.
- **[S37]** G. L. Steele, G. J. Sussman (1980), "Constraints – a language for expressing almost-hierarchical
  descriptions", Artificial Intelligence 14; H. Abelson, G. J. Sussman, *Structure and Interpretation of Computer
  Programs*, 2nd ed., section 3.3.5 "Propagation of constraints"; R. E. Moore, *Interval Analysis* (1966).
- **[S38]** D. C. Karnopp, D. L. Margolis, R. C. Rosenberg, *System Dynamics: Modeling, Simulation, and Control of
  Mechatronic Systems* (Wiley) – power variables (effort × flow) across domains.
- **[S39]** H. W. Müller, *Die Umlaufgetriebe* (Springer) – Willis equation and planetary assembly conditions.

The existing code relations reused here are: `Linkages.Grubler` and `Linkage.Dof` (Kinematics, Core),
`GearTrain.Speeds` and `Links.Ratio` (Core), `Cycles.OttoEfficiency`/`DieselEfficiency` (Process) and
`PumpChart.OperatingPoint` (Charts).

---

## 10. Implementation notes: Calc 0 and Calc 1 (2026-11-25)

This section records the choices made where the concept left them open. The code is the reference.

### 10.1 Files

| Area | Files |
|---|---|
| Quantities, units, lookup, conversion | `src/InventaSketch.Core/Quantities.cs` (`Dimension`, `QuantityKind`, `Unit`, `Quantities`), `Units.cs` (`UnitCatalog`, `Units`) |
| Parser and formatter | `src/InventaSketch.Core/ValueParser.cs` (`ValueParser`, `ParsedValue`, `Tolerance`, `ValueFormat`) |
| Calculator | `src/InventaSketch.Core/UnitCalculator.cs` |
| Variables, relations, hooks | `src/InventaSketch.Core/Values.cs` (`VarDef`, `Relation`, `BoundRelation`, `IValueType`, `IValueLinks`, `IValueChecks`, `ValueModel`, `RootFinder`) |
| Solver | `src/InventaSketch.Core/ValueSolver.cs` (`ValueSolver`, `ValueTable`, `ValueConflict`, `ExplainStep`) |
| Engine API | `src/InventaSketch.Engine/EngineValues.cs` (partial `EngineApi`), DTOs in `Contracts.cs` |
| Demonstrator | `src/InventaSketch.Modules.Electrical/ElecValues.cs` (resistor) |

### 10.2 Quantities and units

- **Dimension** has six exponents: length, mass, time, current, temperature and **plane angle**. SI treats the radian as
  1, but tracking it keeps a speed of rotation (min⁻¹, rad/s) apart from a frequency (Hz) and makes "1500 rpm in rad/s"
  convert with 2π. Internally a speed of rotation is in rad/s, so *P* = *T*·*n* holds in SI without a 2π factor. Where a
  product needs the SI view (N·m × rad/s = W), the angle exponent is dropped when the result is named; rpm ↔ Hz stays
  refused.
- **Kinds** (39): ratio, efficiency, count, voltage, current, power, apparent and reactive power, heat flow, resistance,
  capacitance, inductance, charge, resistivity, frequency, speed of rotation, angle, time, length, area, volume, mass,
  force, torque, energy, speed, acceleration, pressure, volume flow, mass flow, temperature, temperature difference,
  density, specific heat, heat transfer coefficient, irradiance, kinematic viscosity, moment of inertia, spring rate.
  Torque and energy, power and heat flow, temperature and temperature difference share a dimension and are separate
  kinds. The first unit of a kind is its default display unit (charge: Ah, speed of rotation: min⁻¹, temperature: °C).
- **Units** have a factor and an offset to SI and an explicit list of SI prefixes they accept and show (Ω takes µ m k M
  G, F takes p n µ m). Squared and compound units (mm², kN·m, l/min) are listed explicitly and take no prefixes.
- **Lookup order:** exact spelling, prefix + exact spelling, then case-insensitive spelling (so "10 KW" and "1010 MM"
  still work). Spellings are normalised: blanks and `^` removed, superscripts as digits (mm2 = mm^2 = mm²), `*`, `⋅`
  and `.` between letters as `·` (and also without it: Nm = N·m), µ for u in prefixes, Ω sign, `º` and `℃`.

### 10.3 Parser and formatter

- A **bare number** takes the field's display unit (`VarDef.Unit`). A **letter code** ("4k7", "2n2", "R47") or a bare
  prefix ("10k", "2.5k") multiplies the kind's coherent unit (Ω, F, V …), as the code means. The letter R as decimal
  marker is accepted for resistances only.
- **Fractions:** a `/` or `:` followed by a number separates values ("230/12 V", "20/0,4"); a `/` followed by a letter
  belongs to a unit ("1500/min", "40 l/min"). For dimensionless kinds the result is the quotient; otherwise `Parts` holds
  every value and `Value` the first. Parts without a unit take the unit typed last.
- **Tolerances:** "±5 %", "+/-5%", "−20/+80 %", "±0,1 V" (absolute), and a trailing "1%" after a value ("10k 1%") for
  kinds that have no % unit themselves (on an efficiency field "97 %" is the value).
- **Temperature difference** accepts K; "°C" only in German (section 1.3), shown as K.
- **Numbers:** decimal comma or point; with both, the later one is the decimal separator; repeated separators are
  thousands separators (the rules of the former chart parser, now `ValueParser.TryNumber`).
- **Display:** `ValueFormat.Format` rounds to significant digits (default 3), picks the prefix from the unit's list that
  keeps the number in [1, 1000) after rounding (999.7 W → "1 kW"), trims trailing zeros unless asked to keep them (the
  formula lines keep them: "2.00 A"), and uses the decimal comma in German. Format → parse round-trips within the shown
  digits (tested for EN and DE).

### 10.4 Unit converter and calculator

- `Units.Convert(value, from, to)` is **kind-strict**: m → kg and also N·m → J are refused with a bilingual message
  (`TryConvert`). `Units.DisplayUnits(kind)` lists a kind's units.
- `UnitCalculator` is a recursive-descent parser (no code execution; at most 500 characters and 64 nesting levels):
  numbers in both languages and letter codes, every catalogue unit and products/quotients of them ("kg·m/s²", "V/A",
  "m3/h"), `+ − · × * / ÷ ^ ² ³`, `||` or `∥` (parallel), parentheses, sqrt/√, abs, sin, cos, tan (radians or an angle),
  ln, lg/log, exp, π, and a trailing conversion "in/to/as/into/nach/->/→ unit" (the last one whose rest is a unit, so
  "12 mm + 0.5 in" keeps the inch). It is **dimension-based**: sums need equal dimensions, conversions the same dimension
  (the angle may be dropped, except rpm ↔ Hz).
- **Result unit:** a sum keeps the unit typed first ("12 mm + 0.5 in" → 24.7 mm); otherwise the first kind with the
  result's dimension names it (power before heat flow, energy before torque) in its default unit with the best prefix;
  without a kind the coherent SI unit is written out. Temperatures typed in °C/°F are absolute: their difference is a
  difference in K, a difference added keeps °C, two absolute temperatures cannot be added. Default precision is 4
  significant digits.
- The engine method is called `convertUnit`, because `convert` already converts elements between types.

### 10.5 Relation engine

- **Type contract.** Instead of new virtual members on `ObjectType` (`Modules.cs` is shared with parallel work), a type
  implements `IValueType { Variables; Relations }`; `ValueModel.Variables(type)` returns an empty list for every other
  type. That matches "default empty" in the plan. Port relations come from `IValueLinks.ValueLinks(o, ctx)` (the hook for
  Calc 3/4: objects referenced by a link join the solver scope, breadth first), checks from `IValueChecks.ValueChecks`.
- **`VarDef`:** key, bilingual name and symbol, kind, display unit, bound parameter (default: the key), stored unit of a
  number parameter (e.g. kV), relevance, default (SI) and typical range (SI; also the bracket for a numeric solution).
- **Storage decision (as in the plan):** entered values live in the **parameters**, no new model field, so old
  documents are unchanged. A text parameter keeps the text as typed ("4k7 ±5 %", "230 V"); a number parameter holds the
  number in its stored unit. New value parameters are optional text `ParamDef`s with a unit hint. Stored texts are read
  leniently (German rules); a text that is not a value of the kind (e.g. "BC547") counts as not entered and the error is
  reported. Derived values are never stored.
- **`Relation`:** id, bilingual name, formula with keys in braces ("{U} = {R}·{I}"), explicit solvers per variable with
  their expression ("{U}/{I}", used for the explanation), optional residual for a bracketed Brent search, relative
  tolerance (default 1e-6), source id, validity, and `IsLink` for pass-through port relations (`Relation.Equality`).
  Redundant forms are declared explicitly (*P* = *U*·*I* = *I*²·*R* = *U*²/*R*), so that any two values are enough for
  local propagation.
- **Solver** (`ValueSolver.Solve(doc, registry, ids, options)`): scope = given ids (default: the first page) plus
  linked objects; relations in scope order, local before links; a work list solves every relation with exactly one
  unknown (each variable once); each value records its relation, inputs and the set of entered/default values it rests
  on (its *roots*). A fully known relation is an **independent check** only if one of its variables carries information
  the others do not (its roots are not covered by theirs); a passed independent check gives the status *checked* (✓), a
  failed one a **conflict** with the variable's value, the implied value and the entered values involved. Defaults are
  tried one at a time after propagation and withdrawn when they lead to a conflict. `Missing(id)` lists the smallest sets
  (up to three) of further variables that would determine the element. The time budget (default 50 ms) stops the pass
  and sets `Truncated`. `Overrides` solve with a value as if entered, without touching the document (drag preview).
- **Status per element:** empty, under, determined, checked, conflict.
- **Explanation:** one line per derived value of the chain, inputs first: "R = U/I = 230 V/2.00 A = 115 Ω"; entered
  values as typed ("R = 4k7"); numeric solutions "x = 2 (solved numerically: y = x³ + x)".
- **Measured:** 1000 resistors with two entered values each (4000 relations, 2000 texts parsed) solve in about 26–35 ms
  on .NET 10 in Release on a busy development machine (test `ThousandResistorsSolveWithinTheBudget`: best of six runs,
  limit 50 ms in Release, 150 ms in Debug builds). The WebAssembly budget (200 ms) is not measured yet.

### 10.6 Engine API (additive)

| Method | Request | Result |
|---|---|---|
| `values` | `session`, `id` or `ids` (default: page), `lang`, `digits` | `objects[]`: `id`, `status`, `hint` ("1 more value: R, I or P", "✓", "Conflict: …"), `vars[]` (key, name, symbol, kind, relevance, unit + factor/offset, units, value SI, number, display, text, provenance, formula, relation, source, tolerance, error, conflict, param), `missing`, `conflicts[]` (relation, formula, key, value/implied with texts, involved, message); `elapsedMs`, `truncated` |
| `setValue` | `id`, `key`, `text` | state; one undo step; wrong kind/unknown unit → bilingual error; empty text clears |
| `clearValue` | `id`, `key` | state; one undo step |
| `explain` | `id`, `key`, `digits` | `steps[]` and `text` |
| `dragValue` | `id`, `key`, `value` (SI) or `text`, `preview` | preview: like `values`, document unchanged; release: stores the formatted value, one undo step |
| `checks` | `ids` (default: page) | violated checks (empty until Calc 3 adds rules) |
| `convertUnit` | `value` + `fromUnit` + `unit`, or `text` (+ `quantityKind`) + `unit` | `value`, `unit`, `text`, `kind`, `units` |
| `calc` | `text`, `lang`, `digits` (default 4) | `value` (SI), `dimension`, `kind`, `unit`, `number`, `text`, `steps` |
| `units` | `quantityKind` (optional) | kinds with name, dimension and display units |

### 10.7 Demonstrator: resistor

`elec.resistor` has *R* (from the existing `value` text, so "4k7" on the label is the resistance), *U*, *I* and *P* (new
optional text parameters `voltage`, `current`, `power`). Any two give the others; three that agree give ✓; three that
disagree give a conflict that names both values and changes nothing. A `value` text that is not a resistance (e.g. "1/4
W") is ignored for *R*.

### 10.8 Parsers that now delegate

| Former parser | Now | Behaviour |
|---|---|---|
| Charts `Fmt.ParseInput`, `ChartData.TryNumber` | `ValueParser.ParseNumber`, `ValueParser.TryNumber` | identical code moved to Core |
| Energy `Act.Number` | number via `ValueParser.TryNumber` (suffix and clamping unchanged) | additionally accepts thousands separators |
| Building services `Bs.ParseMm`, Architecture `Arch.ParseMm` | `ValueParser.ParseMillimetres` (Architecture still rejects ≤ 0) | additionally accepts any length unit (km, in, ft) and blanks as thousands separators |

Not switched (different purpose): Energy `Fmt.ParseRatio` (voltage lists in kV; `ValueParser` reads "20/0,4 kV" the same
way and can replace it when the transformer gets values), `ChartBreaks.ParseInput` (ranges), `FunctionExpression`
(chart formulas in x; the calculator is separate) and the import number test in `Imports.cs`.

### 10.9 Open for later phases

- Calc 2 (COM): done, see section 11.
- Calc 6: interval results from tolerances (tolerances are parsed and kept, not yet propagated), mute list, unit
  preferences, E-series decision.
- The `value.range` check ("typo?") can use `VarDef.Min/Max` when the check catalogue starts (Calc 3).

### 10.10 Calc 3: Electrical (2026-11-25)

**Status of the electrical accept items of Calc 3 (section 7):** done – element relations of sections 2.1–2.3 for the
elements the module has, parsing of the electrical `value`, network reduction (series/parallel), voltage divider and LED
series resistor ("4k7" in a 5 V circuit with a 2.1 V LED gives *I* = 0.617 mA), the bridge reports "not reducible", a
B16 MCB with *I*_z = 13 A gives the coordination warning, the overload/RCD/resistor-power checks, and the Electrical
module docs list the new variables. The variables and checks per element are listed in
[../modules/electrical.md](../modules/electrical.md) ("Element values").

| Area | Files |
|---|---|
| Variables, relations, markings, checks | `src/InventaSketch.Modules.Electrical/ElecValues.cs` |
| Circuit analysis (nets, series/parallel reduction, circuit relations) | `src/InventaSketch.Modules.Electrical/ElecNetwork.cs` |
| Hooks on the type (`IValueLinks`, `IValueChecks`, `Marking`, `Net`, `Checks`) | `src/InventaSketch.Modules.Electrical/ElecType.cs` |
| Tests | `tests/InventaSketch.Modules.Electrical.Tests/CircuitValueTests.cs` |

Decisions:

- **Ratings are parameters, not variables.** *P*_rated, *I*_F,max and *I*_z are plain optional text parameters read by the
  checks. As variables they would never be determined by a relation and would keep every element "under-determined".
- **Markings.** Where the value text is not the element's own quantity (sources, lamps, LEDs, protective devices,
  transformer), each quantity found in it becomes a one-variable relation of that element ("*I*_n = B16 = 16 A"),
  returned by `IValueLinks`. The text is never rewritten; an entered value that disagrees gives a conflict. Passive parts
  keep the direct binding of Calc 1 (*R*, *C*, *L* = `value`).
- **Nets** are built as in the netlist export (attachments with pin ids, branches docked on lines), plus junction dots,
  closed contacts (protective devices count as closed unless switched off explicitly), mated connectors, busbars,
  junction boxes, boards, meters, earth symbols of one kind and net labels of one name (a lone label leads elsewhere and
  counts as a terminal of unknown current).
- **Reduction** (section 1.6): dangling branches are left out; parallel branches between the same nets; series at a net
  with exactly two branches and nothing else; sources and passive parts are reduced separately, then one source group
  across one load group closes the circuit. Each composite contributes sum relations over element variables (series: the
  same current; parallel: the same voltage; closed circuit: both), and resistive or capacitive groups the equivalent
  resistance or capacitance (Σ*U* = Σ*I*·*R*_eq, solved explicitly for *U* and *I*, numerically by Brent for one *R*).
  Single-line loads on a net fed by one branch give Kirchhoff's current law. A loop left over without terminals of
  unknown current marks its elements for `elec.net.reducible`.
- **Scope.** The relations of a circuit are returned by one element (the first by id that has variables); every other
  element returns a membership relation without solvers ("same circuit"), which never solves or checks anything but pulls
  the owner into the solver scope. So the card and the values label of one element see the whole circuit. The analysis
  is cached per document instance.
- **Orientation.** Voltages and currents are magnitudes in consumer orientation, valid for series-parallel networks driven
  by one source; sources in series are taken as aiding.

Open (electrical): AC impedance, reactance, time constants and resonance (needs a frequency variable, Calc 5 charts);
equivalent inductance; potentiometer wiper ratio, BJT base resistor and op-amp gain; motor/generator values (Machines);
cable values and `elec.cable.drop`; `elec.kcl` (needs current directions); `elec.voltage.mismatch`. **Core change
proposed:** a value solved by a relation without inputs (a marking) should root in itself like an entered value, so two
markings that disagree (a 24 V lamp on a 12 V secondary) give a conflict; today such a pair is not checked. Also useful
in Core: symbols qualified by the element's designator in cross-element formulas ("*U*(R1) = …"), since every resistor's
voltage is called *U*.

---

## 11. Implementation notes: Calc 2 in PowerPoint (COM) (2026-11-25)

This section records the choices made where the plan left them open. The code is the reference.

### 11.1 Files

| Area | Files |
|---|---|
| Level setting | Core `Model.cs` (`ValueLevel`, `DocumentSettings.ValueLevel`); COM `OnSlide/ValueCardModel.cs` (`ValueLevelSetting`), `Connect.cs` (*More ▸ Values*) |
| Values label, entering, drag | Core `ValueLabels.cs` (`ValueLabels`, `ValueEdits`, `ValueDrag`), hooked into `Registry.RenderLocal` and `Registry.Parts` |
| Card, bar, pie, badge | COM `OnSlide/ValueCardModel.cs` (content, pure), `OnSlide/ValueCard.cs` (popup), `OnSlide/OnSlideController.Values.cs` |
| Pictogram | `IconIds.Values` = `cmd-values` ("x="), in COM `IconPainter.SharedPaths` and web `icons.ts` |
| Tests | Core `ValueLevelTests`, Electrical `ValueLabelTests`, AddIn `ValueCardTests`, PowerPoint scenario `tests/powerpoint-com/scenarios/values-ohm.ps1` |

### 11.2 Decisions

- **Level.** In PowerPoint the document is the presentation, so the level is a presentation tag (`ISK_VALUES` =
  `quick`, `explore` or `engineer`). Units that carry `DocumentSettings.ValueLevel` (from the web host) are the fallback
  when the tag is missing. Core stores Quick sketch as "not set", so old documents are unchanged and `valueLevel` is
  written only for Explore and Engineer. The switch is *More ▸ Values* with three toggle buttons, the current one pressed,
  like *Symbol size*.
- **Bar.** In Explore and Engineer an element whose type has values gets *Values* (pictogram `cmd-values`, with its label)
  as the main action, which is group 1 of section 1.5. The former main action keeps its pictogram place. Quick sketch
  leaves the bar unchanged.
- **Pie.** *Edit ▸ Values ▸* is there at every level, because entering values stays possible in Quick sketch. It has one
  entry per variable ("Voltage *U* … 230 V", "Power *P* = 460 W") and then *Show values on the drawing*. The eight places
  stay as they are.
- **Card.** A borderless popup right of the element. Esc, a click outside or another window closes it, as with the insert
  box. One row per variable:
  - marker: ✎ entered, = derived, ○ default, ⇄ linked, ⚠ in conflict
  - symbol (italic), name, value field (the shared parser: "4k7", "230 V", "1,5 kW", German and English), unit (what a
    bare number means), grip ↔ (Explore only), × (clears an entered value)
  - under a derived value, in Explore and Engineer, its formula line ("R = U/I = 230 V/2.00 A = 115 Ω"); Engineer adds the
    source ("[S7]") and a fourth significant digit; Quick sketch shows the values only.

  The status line says ✓, what is missing ("1 more value: R, I or P") or that entries conflict; each conflict line names
  both values and the entered values involved. A wrong entry (a current in a voltage field) shows its message under the
  row, never in a message box. Enter or leaving the field applies it (`setValue` semantics: one undo step, the element is
  redrawn); × clears (`clearValue`). Below: *Calculator …* opens the calculator with the chosen value, and *Show on
  drawing* switches the values label. The card uses one text size; secondary text is grey.
- **On demand, not on hover.** The card opens from the bar or the pie instead of on hover. This keeps the slide calm by
  default (interaction-ux.md), and a hover card would compete with PowerPoint's own handles.
- **Live drag.** No geometry of the resistor encodes *U*, *I* or *R*, so a grip on the drawing would mean nothing. The
  grip is in the card instead:
  - dragging ↔ left or right changes the value logarithmically (3 significant digits);
  - the other rows and formulas follow live (solver overrides, the document is unchanged);
  - releasing stores the value as one undo step, and Esc restores.

  Holding a third value over-determines an element that has two entered values. `ValueDrag` then releases the entered
  values after the dragged one in the type's order, last first. Example: with *U* and *I* entered, dragging *R* releases
  *I*, so *I* and *P* follow, and the release stores *R* and clears *I* in the same undo step. Grips on the drawing stay
  for geometric values (`DragHandles`: charts now; gears and cylinders in Calc 3/4).
- **Values label.** The label is the part `values`. `Registry.RenderLocal` draws it for every `IValueType` while the
  parameter `showValues` is on:
  - placed centred below everything the element draws (its own labels included), upright at any rotation or mirroring,
    2.5 mm text;
  - lists the known card values, but leaves out a value that the type already shows when entered (`VarDef.OnTypeLabel`:
    the resistor's *R* is its value text "4k7");
  - `valueLabelKeys` ("R,P") chooses the variables explicitly (model and tests; the card offers the switch for the whole
    element);
  - shows entered texts with a unit as typed.

  The label is content: it shows at every level, is printed and exported, and stays with the element when it moves. The
  resistor lists `showValues` among its parameters, so the inspector shows it too.
- **Checks.** While the entered values of the selected element conflict, a small amber ⚠ badge sits outside its top
  right corner (Explore and Engineer). The badge is overlay only: it is never exported and never modal. The card shows
  the conflict lines.

### 11.3 Host notes (COM)

- The card, the pie entries and the badge act on the selected element; other elements of the slide show no badge yet.
- The values label is native PowerPoint text inside the unit, like the other labels.
- Automation (UI tests): `SetValueLevel`, `ValueLevel`, `Values`, `SetValue`, `ClearValue`, `ShowValues`, `DragValue`,
  `ValueBadge`, `ValueCard`.

### 11.4 Open

- The inspector's *Values* section (sidebar), the *Checks* list and the badge on every element of the slide (with the
  check rules of Calc 3).
- Choosing single variables for the label in the card (the model supports it).
- The engine's `dragValue` preview does not release values yet; the web host should use `ValueDrag` the same way.
- Unit picker and the significant-digit setting (Engineer, Calc 6); direction arrows ↑↓ while dragging.

---

## 12. Implementation notes: Calc 3 machine elements (2026-11-25)

This section records the choices made for the machine part of Calc 3 (Mechanical and Machines modules). The code is the
reference; the module docs list every variable ([mechanical-drives.md](../modules/mechanical-drives.md#engineering-values-calc-3),
[machines.md](../modules/machines.md#engineering-values-calc-3)).

### 12.1 Files

| Area | Files |
|---|---|
| Mechanical variables, relations, value parameters, checks | `src/InventaSketch.Modules.Mechanical/MechValues.cs`; `MechType` implements `IValueType`, `IValueLinks`, `IValueChecks` |
| Mechanical topology and port relations | `src/InventaSketch.Modules.Mechanical/MechLinks.cs` (`MechTopology`, `MechLinks`) |
| Machines variables, relations, checks, drive-block links | `src/InventaSketch.Modules.Machines/MachValues.cs` (`MachValues`, `MachLinks`); `MachType` in `Kit.cs` |
| Tests | `tests/InventaSketch.Modules.Mechanical.Tests/ValueTests.cs`, `tests/InventaSketch.Modules.Machines.Tests/ValueTests.cs` |

### 12.2 Decisions

- **Magnitudes.** Speeds, torques and powers of gears, shafts, pulleys, sprockets, machines and drive blocks are
  magnitudes; the direction of rotation is no value. Signed speeds would turn every external mesh into a sign conflict
  when users enter positive speeds on both sides. Only the planetary set uses signed member speeds (same sense =
  positive), because Willis' equation needs them.
- **Topology from the core coupling relations** (`Coupling.Graph`, the relations behind "select connected – moves
  along"), cached per document and registry:
  - *Shaft lines*: shafts, couplings and the parts fixed on them (carried or concentric, hub not loose) share *n*.
    Bearings take the speed of their shaft but join nothing. Planet carriers, differential cages and gear sets stay out.
  - *Torque along a shaft line* passes unchanged when at most two members take or give torque (gears, pulleys,
    sprockets, screws, machines); with three or more the split is unknown, so no torque link is made.
  - *Meshes*: touching gears (spur, ring, bevel, worm and wheel; the worm's z is its number of starts) keep
    *z*_a·*n*_a = *z*_b·*n*_b. Planets on a carrier pin are left out, since Willis across separate parts needs signed
    speeds.
  - *Strands*: belts keep *v* = π·*d*·*n* and *T* = *F*·*d*/2 at both pulleys; chains keep *v* = *z*·*p*·*n* and
    *T* = *F*·*z*·*p*/(2π), plus *z*₁·*n*₁ = *z*₂·*n*₂ and the torque ratio when no pitch is entered. A nut on a screw
    takes the screw's *v* and *F*.
- **Stage efficiency.** A gear's η is the efficiency of the mesh in which it is the driven gear (default 1). Across a
  mesh *P*_b/η_b = *P*_a/η_a, so *T*_b = *T*_a·(*z*_b/*z*_a)·(η_b/η_a). With an input torque and an output torque
  entered, the default gives way and the solver derives η.
- **Drive-block chain.** A connector (any line) whose first docked end sits on a block with a mechanical output and whose
  last docked end sits on a block with a mechanical input passes *n* and *T*: motor/e-machine/engine → gearbox
  (*n*₁ = *i*·*n*₂, *T*₂ = *T*₁·*i*·η) → differential (the same) → wheel (*v* = ω·*r*, *F* = *T*/*r*). Drivetrain parts
  on a mechanical shaft share its speed. Both modules use the keys `n`, `T` and `P` for rotating elements, so the links
  meet without a code dependency between the modules.
- **Storage.** Entered values live in optional text parameters (listed in the engineering section of the inspector);
  existing number parameters are reused: `teeth` (*z*), `module` (*m*, mm), screw `pitch` (*P*_h, mm), `starts` (worm
  *z*), `poles`. Creating an element stores `teeth` and `poles` as before, so they count as entered values.
- **Bearing life** *L*₁₀ is a count in millions of revolutions (no revolution unit exists); *L*₁₀h is a time in hours.
- **Checks** run in `ValueSolver.Checks`: `mech.mesh.module` [S16], `mech.power.balance` [S1],
  `mech.planetary.assembly` [S39], `mech.bearing.life` [S19], `mech.screw.selflock` [S18], `mach.slip.sign` [S15],
  `mach.efficiency` [S1], and **`mach.overload`** [S14] (power or torque above the entered rating *P*_N or *T*_N). The
  last one is new: section 4 does not list it, but the Calc 3 check set names "overload".

### 12.3 Status of the Calc 3 machine items

- Done: gear trains with absolute *n*, *T*, *P* and efficiencies (accept: three-stage spur train matches the hand
  calculation; modules 2 and 2.5 give the module warning); planetary set with Willis; belt, chain and screw drives;
  springs (*F* = *c*·*s*, *c* from geometry); bearing life; motors (*n*_s, slip, *P*–*T*–*n*, η); the drive-block chain;
  the checks above; module docs list the variables.
- Open:
  - Willis across separate parts (sun, planets on a carrier, ring, carrier as separate elements): needs signed speeds;
    use the planetary set element for now.
  - Torque split on a shaft line with three or more torque members (branches).
  - Torsion spring (needs a torsional rate kind N·m/rad in Core), rack–pinion link (the rack has no pitch anchor),
    CVT, Ravigneaux, differential gear set, strain wave and cycloidal drives, flywheel energy, slip clutch.
  - Generator operation of the machines (the efficiency relation is for motor operation); battery, inverter and DC link
    (Calc 4 with Energy).
  - Core: `ValueSolver.Solve` creates its `RenderContext` without the registry, so `IValueLinks` fall back to
    `ObjectType.Owner` (shared by every registry the type is registered with). Passing `ctx.For(registry)` in the solver
    (and in callers of `ValueSolver.Checks`) would make this exact.

---

## 13. Implementation notes: Calc 4 fluid, process, building services, energy (2026-11-25)

This section records the choices made for Calc 4 in the Fluid, Process, Building services and Energy modules. The code
is the reference; the module docs list every variable, relation, link and check
([fluid.md](../modules/fluid.md#engineering-values-calc-4), [process.md](../modules/process.md#engineering-values-calc-4),
[building-services.md](../modules/building-services.md#engineering-values-calc-4),
[energy.md](../modules/energy.md#engineering-values-calc-4)).

### 13.1 Files

| Area | Files |
|---|---|
| Net topology and port relations (one copy per module) | `src/InventaSketch.Modules.{Fluid,Process,BuildingServices,Energy}/ValueNets.cs` (`NetSpec`, `NetRule`, `PortVar`, `ValueNets`) |
| Fluid variables, relations, port roles, checks | `src/InventaSketch.Modules.Fluid/FluidValues.cs`; `FluidType` and `FluidLineType` implement `IValueType`, `IValueLinks`, `IValueChecks` |
| Process | `src/InventaSketch.Modules.Process/ProcessValues.cs`; `ProcessType`, `ProcessLineType` |
| Building services | `src/InventaSketch.Modules.BuildingServices/BsValues.cs`; `BsBoxType`, `PipeType`, `DuctType` |
| Energy | `src/InventaSketch.Modules.Energy/EnergyValues.cs`; `EnergyType` |
| Tests | `tests/InventaSketch.Modules.{Fluid,Process,BuildingServices,Energy}.Tests/ValueTests.cs` |

### 13.2 Decisions

- **Nets from attachments.** A net is what the module's lines (fluid line, process line, pipe and duct, energy cable)
  join: line ends docked on ports (attachments with a port id) and on other lines (tees). An end docked on a rail or on
  a busbar without a port id counts as the element's port `*`. Elements pass value classes from port to port where they
  are transparent (valves, couplings, a busbar's taps). The topology is built once per document (documents are
  immutable) and cached. Every value class has a rule:
  - *Equal* (pressure, voltage): the whole net shares the value, branches included.
  - *Flow* (volume and mass flow, stream temperature in Process): shared when at most two elements carry the class in
    the net; lines take it only on an unbranched run. A tee to a gauge or a relief valve does not stop the elements,
    because these carry no flow; a split between two consumers stops it (the split is unknown).
  - *Sum* (parallel PV strings, feeders on a board, radiators on a heating branch): a port marked *total* is the sum of
    the *part* ports of its net; an unbranched run with two elements behaves as *Flow*.
  - *Spread* (building services stream temperature): the single supplying outlet of a split passes its temperature to
    every inlet; mixing at a merge is computed only in the mixing valve.

  Port roles follow the drawing conventions of each module (generator flow port and pump outlet are totals, radiators are
  parts; a meter's device side b is a total, its grid side a a part). Nothing propagates through a port without a role.
- **Choice-dependent relations** (through-rod cylinder, pull-type cylinder, rectangular duct, single- or three-phase
  inverter, DC busbar, DC charger) are bound per object in `IValueLinks.ValueLinks` as local relations of the element,
  because `IValueType.Relations` is per type.
- **Stored defaults count as entered.** Existing rating parameters (module Wp, inverter kW, busbar kV, duct width,
  floor loop spacing …) are stored when an element is inserted, so they are entered values: a string shows its STC power
  at once. To keep this from producing conflicts with real datasheets, *P*_mpp = *U*_mpp·*I*_mpp has a 2 % tolerance and
  *d*_i ≈ DN a 50 % tolerance (DN is a nominal size). The value parameters added by Calc 4 are optional texts without a
  default, so a new element carries no value of its own.
- **Defaults that give way** (labelled *default* on the card): water ρ = 1000 kg/m³ and *c* = 4.19 kJ/(kg·K), pump and
  motor efficiencies 100 %, radiator exponent *n* = 1.3 and room temperature 20 °C, room height 2.5 m, inverter η = 97 %
  (typical, within the 0.95–0.98 of section 2.10), AC system voltage 400 V, cos φ = 1, air density 1.225 kg/m³.
- **Radiator operating point.** With flow, flow temperature, room temperature and Φ₅₀/*n* known, the return temperature
  follows from ρ·*c*·*V̇*·(θ_F − θ_R) = Φ₅₀·(Δθ_ln/50 K)^*n* (two unknowns, one equation after eliminating Φ). The relation
  is a residual solved by Brent within the temperature range; the excess temperature is continued as 0 for θ_R ≤ θ_i so
  the bracket always has a sign change.
- **Heat exchanger signs.** Side 1 and side 2 instead of hot and cold: Φ > 0 means heat from side 1 to side 2; with side 1
  colder every difference changes sign and the counter-flow LMTD stays valid. `heat.cross` fires when the two end
  differences have different signs.
- **Temperature coefficient and air change.** Core has no kind for %/K or h⁻¹. β is a ratio in % read as "per kelvin"
  ("−0.27" or "−0.27 %"), the air change rate a plain number per hour ("1"). Typing "−0.27 %/K" or "1/h" is refused by
  the parser. See 13.4.
- **AC current.** *U*_AC is the line-to-line system voltage in both cases: three-phase *I* = *P*/(√3·*U*·cos φ),
  single-phase (phase to neutral) *I* = *P*/((*U*/√3)·cos φ). Boards (busbar, meter) assume a symmetrical three-phase
  load.
- **Checks** run in `ValueSolver.Checks`: `fluid.cyl.force`, `fluid.velocity`, `fluid.pressure.relief` [S25, S26];
  `heat.cross` [S29]; `heat.velocity`, `air.velocity` [S30], `heat.cop.carnot`, `heat.balance` [S29]; `pv.voc.max`,
  `pv.mpp.min`, `pv.isc.max` [S33], `pv.dcac` [S34], `bat.crate` [S35]. **`wind.betz`** [S36] (*c*_p above 16/27) is new:
  section 4 does not list it, but the Betz limit of section 2.10 makes higher values impossible. The textbook velocity
  ranges are the defaults of section 4 until user limits exist (Calc 6). `fluid.velocity` takes the line function from
  what the line is docked on (pump S: suction; tank, T/R or drain line: return; otherwise pressure).

### 13.3 Status of the Calc 4 items

- Done: the five accept items (unit tests with hand calculations):
  - 10 modules, *U*_oc = 49.5 V, β = −0.27 %/K, θ_min = −10 °C: *U*_oc,max = 541.78 V; `pv.voc.max` against
    *U*_DC,max = 500 V;
  - pump 10 cm³ at 1450 min⁻¹ (14.5 l/min) through a 4/3 valve to a 50/28 mm cylinder at 100 bar: *F*_ext = 19.63 kN,
    *F*_ret = 13.48 kN, *v*_ext = 0.123 m/s, *v*_ret = 0.179 m/s, with the pressure entered at the pump or the cylinder;
  - boiler (55 °C) → pipe (50 l/h) → radiator (Φ₅₀ = 1000 W, *n* = 1.3): θ_R ≈ 46.0 °C, Φ ≈ 521 W, back to the boiler;
  - 20 m² room, *n* = 1 h⁻¹: 50 m³/h at terminal, duct and unit, 2.76 m/s in an 80 mm duct;
  - 10 × 400 Wp → inverter (η = 96 %) → 0.4 kV busbar: 3.84 kW, 5.54 A per phase (16.6 A single-phase).
- Done: the relations of sections 2.7–2.10 and 3.5–3.8 listed in the module docs, port flows for every module, the
  catalogue checks of section 4 for these domains, and every library item still inserts without values and triggers no
  check (tested per module).
- Open:
  - Pressure drop in pipes (Darcy–Weisbach, Colebrook) and orifice/valve scaling (*K*_v, *Q*_nom); pneumatic air
    consumption; compressors and turbines (gas properties).
  - Heating curve, heating load (idea of EN 12831-1), underfloor manifold sums, hot water circulation.
  - PV irradiance and cell temperature (NMOT), yield estimate, CHP, electrolyser and fuel cell, hydrogen tank, cable
    voltage drop in the energy module, EV charging time.
  - Room values from an architecture room (area, height) instead of the air terminal: needs the Architecture module to
    expose its area as a variable with a shared key.
  - Directions: nets are undirected; mixing at a merge and the split of a flow without totals need flow directions
    (arrows or line direction), which the model does not use yet.

### 13.4 Core changes that would help (not made here)

- **Kinds:** a temperature coefficient (%/K, 1/K, mV/K) and an air change unit h⁻¹ (1/h) for `Frequency`; then β and
  *n* take their units on input and display.
- **A shared net helper:** `ValueNets.cs` is the same in four modules (modules never depend on each other). A Core
  helper (`PortNets` with the rules Equal/Flow/Sum/Spread and port roles) would replace the copies and could serve the
  Electrical module too.
- **Duplicate links:** the solver collects the port relations of every object in scope, so a link between two objects is
  added twice (once from each). It is harmless (the second copy is never independent), but deduplicating by reference
  in `ValueSolver.Solve` would halve the link relations.
