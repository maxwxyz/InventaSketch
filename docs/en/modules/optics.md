# Optics module (`optics`)

Library: `src/InventaSketch.Modules.Optics` · Tests: `tests/InventaSketch.Modules.Optics.Tests` ·
German version: [../../de/modules/optics.md](../../de/modules/optics.md)

The module provides optical elements for technical figures and teaching slides — lenses (thin and thick), mirrors
(also a galvo scanner mirror), prisms, beam splitters (schematic, non-polarising and polarising), stops, gratings,
filters, polarisers, wave plates, a glass plate (also as a Brewster window), a light guide, an optical fibre, light
sources, detectors (also a spectrometer), a screen, an object arrow and the optical axis — and **light rays that are
traced live through the elements**, carrying their intensity and polarisation. All symbols are drawn independently; no
standard symbol artwork or tables are copied and no compliance with a standard is claimed.

It is a **sketching** module: the ray tracer is two-dimensional and uses ideal (paraxial) thin lenses and curved mirrors;
only the thick lens, prisms, the glass plate and the light guide are traced face by face with Snell's law. Fresnel
reflection losses and the polarisation they cause are optional per element (section "Fresnel losses and coatings"). It
does not design or optimise optical systems (no aberration analysis or radiometry in absolute units).

## Conventions

- **Quick sketch first:** every library item inserts without parameters. Free (uniform or non-uniform) scaling changes
  `Size` only. The box height of a lens, mirror, stop, grating or filter is its aperture.
- **Optical axis = local x axis** of every element (light from the left travels towards +x). Rotate or mirror the element
  to point it elsewhere; mirrors reflect on their local −x side (hatched back on +x).
- **Focal lengths are in the drawing scale** (mm on the page, parameter `f`, magnitude; the sign comes from the type:
  converging/concave positive, diverging/convex negative). Scaling an element does not change `f`.
- **Line roles (ISO 128 ideas):** element outlines visible lines (0.5 mm), details thin (0.18–0.35 mm), the optical axis a
  thin dash-dot centre line, rays thin continuous lines (0.35 mm, red by default), virtual rays and the scan fan rays of a
  scanner mirror dashed (hidden line role).
  Glass is filled with a light tint; monochrome output turns everything black and white.
- **Labels:** optional `label` parameter, drawn below the element (outside the box, like designators elsewhere).

## Object types

| Type id | Element | Parameters (all optional) |
|---|---|---|
| `optics.lens.converging` | Converging lens; views `shape` (section) and `symbol` (double arrow, heads outward) | `shape` biconvex/planoconvex/meniscus, `f` (40), `focal` (F, F′ markers), `rays`, `bundle`, `beam`, `coating` (ideal, inspector only), `label`, values `g`, `b` (inspector), `showValues` |
| `optics.lens.diverging` | Diverging lens; views `shape` and `symbol` (heads inward) | `shape` biconcave/planoconcave/meniscus, as above |
| `optics.mirror.plane` | Plane mirror with hatched back | `label` |
| `optics.mirror.concave` / `optics.mirror.convex` | Curved mirrors | `f` (40), `focal` (F and centre of curvature C/M), `label`, values `g`, `b` (inspector), `showValues` |
| `optics.prism.triangle` | Prism, equilateral (deviating) or right-angle (reflecting) | `shape`, `n` (1.5, d line), `dispersion`, `dn` = n(F) − n(C) (0.03), `coating` (ideal), `label`, `showValues` |
| `optics.splitter.cube` / `optics.splitter.plate` | Beam splitter: the diagonal (bottom left to top right) transmits and reflects; the polarising one draws its splitting surface double (cube) or a centre line (plate) | `split` schematic/nonpolarizing/polarizing (schematic), `reflectance` (reflected share when non-polarising, 0.5), `label` |
| `optics.stop.aperture` | Aperture stop / slit (two blades) | `opening` (fraction of the height, 0.35), `label` |
| `optics.grating.transmission` | Transmission grating (plate with grooves) | `angle` (first-order angle at 550 nm, 20°), `orders` (1–3), `label`, values `lines` (lines per mm; when entered it sets the drawn orders instead of `angle`), `lambda` (wavelength for the values), `showValues` |
| `optics.filter.plate` | Filter plate, neutral or coloured | `tint` neutral/red/green/blue/yellow, `label` |
| `optics.fibre.cable` | Optical fibre with connectors at both ends | `spread` (output half cone, 12°), `rays`, `label` |
| `optics.source.lamp` | Lamp (bulb, filament, socket) | `emission` cone/parallel/all directions, `rays`, `spread` (0 = fill the next element), `beam`, `label`, value `power`, `showValues` |
| `optics.source.led` | LED (dome, flange, leads) | `emission`, `rays`, `spread`, `beam`, `color` white/red/green/blue, `label`, values `power`, `lambda`, `showValues` |
| `optics.source.laser` | Laser (housing, output aperture) | `rays` (1), `beam`, `color` 650/532/445 nm, `label`, values `power`, `lambda`, `showValues` |
| `optics.detector.photodiode` | Photodiode as optical detector | `label`, value `area` (receiving area), `showValues` |
| `optics.detector.camera` | Camera: lens barrel focused on the image sensor | `label`, `area`, `showValues` |
| `optics.detector.eye` | Eye (section): cornea, iris, lens, retina, optic nerve | `label`, `area`, `showValues` |
| `optics.screen.plain` | Screen | `label`, `area`, `showValues` |
| `optics.object.arrow` | Object arrow standing on the axis (imaging figures) | `bundle` principal rays/fan, `rays`, `image` off/image/construction (image), `names` (false), `label`, `showValues` |
| `optics.ray` | Light ray (point object, page coordinates) | `trace` (true), `arrows` middle/end/none, `virtual` (true), `tail` (0 = automatic), `showValues` (intensity and polarisation at the ray ends, false), `ghosts` (ghost reflections, false) |
| `optics.axis.line` | Optical axis (dash-dot centre line, point object) | `label` |
| `optics.lens.thick` | Thick lens: two spherical faces, traced at both (box width = thickness, box height = aperture) | `shape` biconvex/planoconvex/biconcave/planoconcave, `n` (1.5), `sag` (depth of the curved faces as a share of the width, 0.35), `focal` (F, F′, principal planes H, H′), `rays`, `beam`, `coating` (ideal), `label`, `showValues` |
| `optics.mirror.scanner` | Galvo scanner mirror: plane mirror on a pivot, turned inside its box | `angle` (mirror angle, 45°; 0° = upright like a plane mirror), `scan` (mechanical scan range ±, 10°), `fan` (end positions and ray fan, false), `label` |
| `optics.polarizer` | Linear polariser: plate with its transmission axis as a double arrow | `angle` (transmission axis, 0°), `label` |
| `optics.waveplate` | Wave plate: plate with its fast axis (dashed double arrow) and "λ/4"/"λ/2" | `retardance` quarter/half (quarter), `angle` (fast axis, 45°), `label` |
| `optics.lightguide` | Light guide (rectangular glass rod, total internal reflection) | `n` (1.5), `coating` (ideal), `label` |
| `optics.window.plate` | Glass plate (window): plane-parallel plate tilted in its box; the edges stop light | `n` (1.5), `tilt` (angle of incidence along the axis, 0°, −80° … 80°), `brewster` (tilt = arctan n, false), `coating` (**uncoated** by default), `label` |
| `optics.detector.spectrometer` | Spectrometer: housing with entry slit, reflection grating, detector line and the dispersed light drawn inside | `label`, `area`, `showValues` |

Library items (48) include the lens shapes and symbols, thick biconvex and plano-convex lenses, three mirrors, the
scanner mirror with and without its scan range, deviating/dispersing/right-angle prisms, both splitters (schematic), the
50:50 cube (non-polarising) and the polarising cube (PBS), aperture and slit, grating, neutral and red filters, light
guide, linear polariser, quarter- and half-wave plate, glass plate and Brewster window, fibre, lamp (cone
and parallel), LED, red and green laser, photodiode, camera, eye, screen, spectrometer, traced and freely drawn light
ray, optical axis and object arrow.

## Light rays and ray tracing

A ray object (`optics.ray`) works in one of three ways:

1. **Docked on an emitter** (its first point attached to the emitter's anchor): it draws that element's bundle.
   - Lamp/LED: a cone from the bulb or dome (`spread` 0 = just fill the lens, mirror or aperture of the next element on
     the axis), parallel light (`beam` height, 0 = 70 % of the next element's aperture) or, for the lamp, all directions.
   - Laser: one ray or `rays` across `beam`, coloured by wavelength.
   - Fibre: a cone from the output end.
   - Lens (`o`): its own demonstration bundle, parallel to the axis from the left or from a point on the axis at 2f.
     The thick lens has the same parallel bundle (`rays`, `beam`).
   - Object arrow (`tip`): the **principal rays** to the next lens or curved mirror on the axis — parallel ray, central
     ray (through the optical centre or vertex) and focal ray (through or towards the front focal point) — or a fan
     across its aperture.
2. **Free ray** (not docked): a single ray from its first point towards its second point.
3. **Drawn freely** (`trace` = false): a polyline through its points, no tracing.

The context action **Add ray bundle** (`optics.trace`) on lamps, LEDs, lasers, fibres, lenses and object arrows inserts a
ray object docked on the element (anchor `exit`, `o` or `tip`); when one is docked already the action reads
**Remove ray bundle** and removes it.

**Live tracing.** Rays are traced every time they are drawn, from the current document: every optics element on the
page (hidden layers excluded) takes part. Every optics element reports the rays of its page as dependents
(`ObjectType.Dependents`, `HasDependents`; only rays and object arrows set `DependsOnNeighbours`); elements that act on light (they have an optical surface) also report
the object arrows of the page that draw an image. So moving, rotating, scaling or changing an element, or dragging one of
its grips, redraws the rays and images in the same undo step; the docked start point follows its emitter through the
ordinary attachment. Nothing has to be re-traced by hand.

**Physics used:**

- Thin lenses: ideal, in the centre plane of the lens, slope change −y/f (paraxial). Parallel rays meet exactly in F′;
  the thin-lens equation 1/f = 1/s + 1/s′ holds (`ThinLens.ImageDistance`). Diverging lenses draw the backward
  extensions to the virtual focal point as dashed **virtual rays**.
- Curved mirrors: ideal, as a thin mirror in the vertex plane with focal length f (C at 2f); the ray turns on the drawn
  surface and is aimed so that parallel rays meet exactly in F. Convex mirrors draw virtual rays.
- Plane mirrors, beam splitters: law of reflection, exact. The back of a mirror stops light. Mirrors are ideal (all light
  reflected, polarisation kept). Beam splitters (`split`): **schematic** (default, as before: transmitted and reflected
  ray both keep the full intensity), **non-polarising** (the share `reflectance` reflected, the rest transmitted, both with
  the incoming polarisation) or **polarising** (ideal PBS: the p part transmitted, polarised p, the s part reflected,
  polarised s; unpolarised light is halved). A part below 0.5 % is not drawn.
- Prisms: Snell's law at every face, total internal reflection when it applies. With dispersion, white light entering
  the prism splits into five colours (656, 589, 546, 486, 436 nm) with n(λ) from a two-term Cauchy fit through `n`
  and `dn`. The dispersing prism item uses `dn` = 0.05 (strong flint-like dispersion, chosen for visibility).
- Gratings: sin θm = sin θ + m · sin θ1 · λ / 550 nm for m = ±1 … ±`orders`; white light gives a spectrum per order.
- Stops pass rays within the opening and stop the others; filters pass their colour band (white light takes the filter
  colour); screens, detectors, the camera body, the eye ball, source housings and the fibre ends stop rays. The eye and
  camera focus parallel light on the retina / sensor.
- Thick lens: Snell's law at both spherical faces, intersected exactly with the circles (no facets); the edges stop light.
  The radii follow from the box and `sag` (a face of depth s over the half aperture h has R = (s² + h²) / 2s), so scaling
  the lens changes its focal length. F, F′, H and H′ come from the lensmaker's equation for a thick lens,
  1/f = (n − 1)·[1/R1 − 1/R2 + (n − 1)·d / (n·R1·R2)], back focal distance f·(1 − (n − 1)·d / (n·R1))
  (`ThickLens.FocalLength`, `BackFocalDistance`, `FrontFocalDistance`). Paraxial rays meet at F′ as the equation says;
  marginal rays cross the axis earlier (spherical aberration is visible, not analysed).
- Scanner mirror: law of reflection at the turned mirror, so turning it by Δ turns the reflected beam by 2Δ. With `fan`
  the mirror's end positions (±`scan`) are drawn dashed, and every ray reflected by it also sends two dashed **fan rays**
  (the reflections at both ends of the range, ±2·`scan` optical), which are traced on through the following elements
  (e.g. a scan lens). Fan rays do not branch again at a second scanner.
- Light guide: Snell's law at its four faces; light meeting a side face beyond the critical angle arcsin(1/n) (41.8° for
  n = 1.5) is totally reflected and guided to the far end, below it leaves through the side.
- Polarisation (Jones calculus): a ray carries its relative intensity (1 at the source) and its polarisation, unpolarised
  until it meets a polariser. Angles are measured across the ray from the drawing plane (0°) towards the perpendicular to
  the drawing (90°). A polariser passes half of unpolarised light and the share cos²(θ − axis) of polarised light (Malus's
  law) and polarises along its axis; below 0.5 % left (crossed polarisers) the ray ends there. A wave plate retards the
  component along its slow axis by π/2 (λ/4) or π (λ/2): a half-wave plate turns linear polarisation (θ → 2·axis − θ), a
  quarter-wave plate at 45° makes it circular (any analyser then passes half). The state is a coherency matrix, so light
  can also be **partially polarised** (behind a Brewster window). Mirrors and gratings keep the polarisation and the
  intensity; beam splitters and glass faces change them as described above and below. Weaker light is drawn more
  transparent (opacity = relative intensity, at least 0.2); light at full intensity is drawn exactly as before.
- The spectrometer is a detector for the tracer: rays end on its housing (at the slit wall); the grating and the spectrum
  inside are drawn schematically, not traced.
- After the last element a ray runs on for `tail` mm; automatic (0): as long again as the way so far (30–400 mm), and the
  open ends of a bundle are lined up across it. A free ray that meets nothing ends at its second point.
- **Values at the ray ends** (`showValues`, toggle **Values at ray ends**, off by default): every
  traced ray (not the scan fan rays) writes its relative intensity and polarisation at its end (part `values`, text
  2.5 mm, above the end; right of it for rays running up or down): "50 % (linear 0°)", "25 % (circular)",
  "86 % (16 % polarised)" (degree of polarisation), "100 %" for unpolarised light; German "polarisiert", "zirkular",
  "elliptisch", decimal comma. A ray ending on a detector thus shows what the detector receives.

### Fresnel losses and coatings

Glass faces (prism, thick lens, light guide, glass plate) and thin lenses have a parameter `coating` (**Glass
surfaces**, a choice in the context bar except on thin lenses, where it is set in the inspector):

| Value | Meaning |
|---|---|
| `ideal` | Ideal faces: no reflection losses and no change of the polarisation, also not on total internal reflection. **Default of every element that existed before**, so existing drawings render unchanged. |
| `none` | Uncoated glass: the Fresnel equations for s and p at every face (default of the glass plate). |
| `ar` | Anti-reflection coated: one quarter-wave layer of index √n designed for 550 nm, traced as a thin film (no reflection at 550 nm and normal incidence, a little at other wavelengths and angles); total internal reflection as uncoated. |

The physics (`Fresnel`, all angles from the face normal, loss-free media n1 → n2):

- Reflectances R_s = r_s², R_p = r_p² with r_s = (n1·cos θi − n2·cos θt) / (n1·cos θi + n2·cos θt) and
  r_p = (n1·cos θt − n2·cos θi) / (n1·cos θt + n2·cos θi); transmittances T = 1 − R. Uncoated glass n = 1.5 reflects 4 %
  per face at normal incidence (a thin lens therefore passes 0.96² = 92.2 % on the axis), 9.2 % (s) and 0.85 % (p) at 45°.
- Brewster angle tan θB = n2 / n1 (56.31° for n = 1.5): p is not reflected at all, s about 14.8 % per face. A plate
  tilted to Brewster's angle (`brewster`, library item **Brewster window**) passes p-polarised light without loss; inside
  it the light meets the second face at the internal Brewster angle 90° − θB, so that face reflects no p either.
  Unpolarised light leaves partially p-polarised: intensity (1 + T_s²)/2 = 86 % with degree of polarisation
  (1 − T_s²)/(1 + T_s²) = 16 %; a polariser behind it passes 50 % along p and 36.3 % along s.
- Total internal reflection beyond the critical angle sin θc = n2 / n1 (41.8° for n = 1.5): all light is reflected, with a
  phase difference between p and s (`Fresnel.ReflectionPhase`). Angles are measured in each ray's own frame, in which an
  ideal mirror changes nothing; in that frame a total reflection at 45° in n = 1.5 shifts p against s by 180° − 36.87°
  (textbook: tan(Δ/2) = cos θ·√(sin²θ − (n2/n1)²) / sin²θ, plus the 180° of the frame), and two total reflections at 54.6°
  in n = 1.51 add up to a quarter wave (the Fresnel rhomb). A right-angle prism with uncoated faces therefore turns linear
  45° light elliptical.
- A thin lens is taken as two plane faces of glass n = 1.5 across its axis, met at the ray's angle to the axis.
- Partial reflections at glass faces lower the transmitted ray. Light that is (practically) gone, below 0.5 %, ends the
  ray.
- **Ghost reflections** (`ghosts` on the ray, toggle **Ghost reflections**, off by default): the light a non-ideal glass
  face reflects partially is traced as a ray of its own, with the reflection factors r_p, r_s of the face (uncoated) or
  the amplitudes √R of the anti-reflection layer, polarisation included. A plate of n = 1.5 at normal incidence sends
  4 % straight back from its front face and 0.96 · 0.04 · 0.96 = 3.7 % from its back face; the transmitted 92.16 % are
  unchanged. A ghost of a ghost (0.16 %) is below 0.5 % and not drawn, so ghosts are first order in practice. Thin
  lenses (taken as two plane faces) send no ghosts, and total internal reflection is not a ghost. Ghosts are drawn like
  other weak light (opacity = intensity, at least 0.2).

## Element values (value card)

Optics elements are value types (`IValueType`, `IValueLinks`, `IValueChecks`; engineering-calculations conventions: SI
internally, display units as listed). Every value is optional; the drawing never needs one. `showValues` (**Show
values**) draws the known card values below the element. Values that are results of the trace or of the drawing are
read-only (`VarDef.ReadOnly`): every host shows them without an input field or drag handle. The received power stays
enterable, so a wanted detector power gives the source power.

| Element | Card values | Panel values | Relations |
|---|---|---|---|
| Lamp | optical power P (mW; empty = 1 mW, the default) | – | – |
| LED, laser | P; wavelength λ (nm; entered in `lambda`, else from the colour, none for white light) | photon energy E_ph (eV) | E_ph = h·c/λ (h, c exact SI values [S1]) |
| Photodiode, camera, eye, screen, spectrometer | received power P_e (mW), share of the emitted light η (%), degree of polarisation V (%), polarisation angle θ (°, 0° = in the drawing plane) | polarisation (unpolarised, linear, circular, elliptical, partially polarised); receiving area A (mm², entered in `area`, else a sample) and irradiance E_e (W/m², a sample estimate) | see below; E_e = P_e / A |
| Converging / diverging lens | focal length f (the parameter `f`, mm; the diverging lens shows its amount), refractive power D in dioptres (1/m), object distance g, image distance b (values only, parameters `g`, `b`) | magnification β, with a second lens (thin or thick) ahead on the axis: system focal length f_s, distance d between the principal planes, back focal distance s′_F | D = ±1/f; 1/f = 1/g + 1/b (signed f); β = −b/g; Gullstrand |
| Thick lens | f (lensmaker's equation from `n` and the drawn faces), D | n, back focal distance s′_F, principal planes s_H (from the front vertex) and s′_H (from the rear vertex), with a next lens on the axis f_s, e, s′_F of the system | 1/f = (n − 1)·[1/R1 − 1/R2 + (n − 1)·d / (n·R1·R2)]; s_H = −f·(n − 1)·d/(n·R2), s′_H = −f·(n − 1)·d/(n·R1); Gullstrand |
| Concave / convex mirror | f (the parameter `f`; the convex mirror shows its amount), radius of curvature R (drawn: C at 2f), g, b (values only, `g`, `b`; b > 0 in front of the mirror) | β | f = R/2; 1/f = 1/g + 1/b (concave f > 0, convex f < 0); β = −b/g |
| Prism | apex angle A (drawn, at the top corner), n (the parameter `n`, d line), minimum deviation δ_min | n_F − n_C (`dn`); with `dispersion` on: n_F, n_C (Cauchy fit of the tracer), Abbe number ν_d, angular spread Δδ_FC | n = sin((A + δ_min)/2)/sin(A/2); ν_d = (n_d − 1)/(n_F − n_C) |
| Diffraction grating | line density N (lines per mm, entered in `lines`), period d (µm), wavelength λ (entered in `lambda`, else of the traced light reaching the grating when it is one wavelength, else 550 nm), angle θ₁ of the first order | θ₂, θ₃ (when drawn), drawn first-order angle at 550 nm, highest order m_max | d = 1 mm / N; d·sin θ₁(550 nm) = 550 nm; d·sin θ_m = m·λ (normal incidence) |
| Object arrow | g, b, β, image real/virtual (of the drawn image) | focal length of the imaging element, object size G, image size B = β·G | 1/f = 1/g + 1/b, β = −b/g |

**Received light.** Every traced ray object of the page is traced as drawn; the paths that end on an element are summed.
A bundle docked on a lamp, LED or laser carries that source's power P, divided equally among its start rays (several
bundles on one source share it). A path contributes k = I · w / N: its relative intensity I at the element (polarisers,
splitters, Fresnel losses), its power share w (1, divided among the colours of dispersed white light and among the drawn
grating orders, and halved at a schematic splitter, whose rays are drawn at full intensity) and the number N of rays of
the bundle. Then:

- P_e = Σ kᵢ·Pᵢ over the sources; solved for a source power too (enter the received power → the source power follows,
  its 1 mW default gives way). P_e = 0 when no traced ray reaches the element; unknown when only rays without a source
  arrive (a free ray, the bundle of a lens or an object arrow).
- η = Σ kᵢ·Pᵢ / Σ Pᵢ over the bundles that arrive (bundles without a source count as power 1).
- The polarisation is the incoherent sum of the coherency matrices of all arriving light, weighted like η: V =
  √((A − B)² + 4|C|²), θ = ½·atan2(2·Re C, A − B) (no angle for unpolarised or circular light).

Examples: a 5 mW laser through a polariser at 30° onto a photodiode: P_e = 2.5 mW, η = 50 %, V = 100 %, θ = 30°, linear;
polariser 0° and analyser 60°: η = ½·cos² 60° = 12.5 %; a 50:50 non-polarising cube with `reflectance` 0.3 sends 7 and 3
mW of 10 mW to its two detectors; an uncoated plate at normal incidence passes 92.16 %.

**Lenses.** f = 40 mm gives D = 25 dpt (the diverging lens −25 dpt). With g = 60 mm the converging lens gives b = 120 mm
and β = −2, the diverging lens b = −24 mm and β = 0.4. Each of f, g, b follows from the other two (clear `f` on the card
to let it follow from g and b; the drawing then uses its default 40 mm). Two thin lenses on one axis (the second one's
centre within half its larger box side of the first one's axis, axes parallel; the nearest ahead counts) give
f_s = f₁·f₂ / (f₁ + f₂ − d) and s′_F = f₂·(f₁ − d) / (f₁ + f₂ − d), measured from the second lens (f₁ = 40, f₂ = 20,
d = 50 mm: f_s = 80 mm, s′_F = −20 mm, which the traced rays confirm). The partner may be a thick lens, and a thick lens
has the same values with the lens behind it: d (on the thick lens: e) is then measured between the principal planes H′
of the first and H of the second lens (generalised Gullstrand formula), and s′_F behind H′ of the second lens. A thick
lens turned round meets the light with its H′. Example: a thin lens f = 40 mm and the default thick lens (n = 1.5, 10 × 34
mm, f ≈ 44.8 mm) 60 mm behind it; the traced rays confirm f_s within 0.2 % (the thick lens is traced exactly, not
paraxially).

**Thick lens principal planes.** s_H = −f·(n − 1)·d/(n·R₂) from the front vertex and s′_H = −f·(n − 1)·d/(n·R₁) from the
rear vertex (positive in the direction of the light), the same planes H, H′ the focal-point marks draw.

**Curved mirrors.** f = 40 mm gives R = 80 mm; with g = 60 mm the concave mirror gives b = 120 mm (a real image in front
of it) and β = −2; a convex mirror of f = 30 mm with g = 60 mm gives b = −20 mm (virtual, behind it) and β = 1/3. As with
lenses, each of f, g, b follows from the other two (clear `f` first). R is read-only: it is what the drawing shows (the
centre of curvature C/M at 2f).

**Prism.** The apex angle A is read from the drawing: the angle at the top corner between the two faces meeting there
(60° for an equilateral prism whose height is √3/2 of its width, 45° for a square right-angle prism; it follows the box
when the prism is scaled). δ_min = 2·asin(n·sin(A/2)) − A: n = 1.5, A = 60° gives 37.18°, and a ray entering at
θ₁ = asin(n·sin(A/2)) = 48.59° is traced parallel to the base and leaves deviated by exactly that. A measured δ_min gives
n (clear `n` first: the spectrometer method). With `dispersion` on, n_F and n_C come from the tracer's two-term Cauchy
fit through n_d and `dn` (n_F − n_C = `dn` exactly), ν_d = (n_d − 1)/(n_F − n_C) (0.5/0.03 = 16.7 by default), and Δδ_FC
is the angle between the traced F and C lines when the light enters at the incidence of minimum deviation for the d line
(2.62° for the defaults).

**Diffraction grating.** The period follows from the drawn first-order angle at 550 nm (`angle`): 20° gives d = 550 nm /
sin 20° = 1.608 µm, N = 622 lines/mm. An entered line density (`lines`, e.g. "600") sets the period instead, and the
tracer draws the orders with it (sin θ_m = sin θ_i + m·λ/d); the angle input is then not offered. The wavelength is the
one of the traced light reaching the grating when that is a single wavelength (a laser or an LED of one colour), else
550 nm (white light, no light), unless one is entered in `lambda`. θ_m = asin(m·λ/d) for the orders drawn (normal
incidence; `orders` 1–3): 600 lines/mm with a red laser (650 nm) gives θ₁ = asin 0.39 = 22.95°, θ₂ = asin 0.78 =
51.26°, and no third order (m·λ/d = 1.17), which the tracer does not draw either; m_max = ⌈d/λ⌉ − 1 = 2.

**Irradiance (sample estimate).** E_e = P_e / A, the received power spread evenly over the receiving area. The area is
entered in `area` (value A); without one it is a sample: a square face as high as the drawn element (a 10 mm photodiode:
100 mm², so 1 mW gives 10 W/m²). Both are marked as estimates: the drawn rays are a sample of the bundle, there is no beam
profile, and the drawing is schematic.

**Checks** (notes on the card, they never change values): virtual image (b < 0) on lenses, curved mirrors and the object
arrow; object in the focal plane (g = f, image at infinity) on converging lenses and concave mirrors; afocal pair
(f₁ + f₂ = d, a telescope with angular magnification −f₁/f₂, also with a thick lens); no traced ray of the page reaches a
detector (only when the page has rays); only rays without a source arrive; the power went through a schematic splitter
or a grating (assumed equal shares, a hint); a wavelength outside about 380 to 780 nm (drawn in the nearest colour); no
minimum deviation (n·sin(A/2) ≥ 1: total internal reflection at the second face); a grating order drawn that does not
exist at the wavelength (m·λ ≥ d, beyond 90°; it is not drawn). Check ids: `optics.mirror.virtual`,
`optics.mirror.focalplane`, `optics.prism.nominimum`, `optics.grating.beyond` (besides the earlier `optics.lens.*`,
`optics.image.*`, `optics.light.*`, `optics.source.invisible`).

The wavelength entered on a laser or LED is also what the tracer uses (405 nm is drawn violet, and a dispersing prism
refracts it with n(405 nm)); choosing a colour in the context bar clears it. D uses the quantity kind "refractive power"
(`opticalPower`, unit dpt = 1/m). The lens, mirror, prism, grating and irradiance relations cite [S41] (optics
textbooks). The line density is a plain number of lines per mm (the quantity catalogue has no kind for it).

Read-only (results of the trace or of the drawing): η, V, θ, the polarisation, E_e; the arrow's image values; the thick
lens's f, s′_F, s_H, s′_H; the system values f_s, d/e, s′_F; the mirror's R; the prism's A, n_F, n_C, ν_d, Δδ_FC; the
grating's d, θ_m, the drawn angle at 550 nm and m_max.

While its values label is shown, a detector, a lens (thin or thick), a grating or an object arrow is reported as a
dependent of every optics element of its page, so the label follows any change of the beam path.

## Image construction

An object arrow (`optics.object.arrow`) with a converging or diverging lens or a curved mirror in front of it on its axis
(the nearest such element whose centre lies within half its larger box side of the arrow's axis line; a mirror must turn
its reflecting side to the arrow) draws its **image** – parameter `image`, a choice in the context bar:

- `off`: no image.
- `image` (default): the image arrow only. A **real** image (light really meets there) is drawn solid, a **virtual** one
  dashed (hidden line role).
- `construction`: the image with the three **construction rays** from the arrow tip, one colour each: the parallel ray
  (red: parallel to the axis up to the element, then through F′ or away from the virtual F′), the central ray (green:
  through the optical centre or vertex, undeviated or reflected symmetrically) and the focal ray (blue: through or towards
  F, parallel to the axis after the element). Backward extensions to a virtual image and the part of a focal ray that only
  aims at F are dashed. As in textbook figures, the rays turn on the element's principal plane, which is extended beyond the
  drawn aperture where needed.

Each point of the arrow (foot and tip) is imaged with the thin lens equation **1/f = 1/g + 1/b** and the magnification
**β = −b/g** (g object distance, b image distance; b > 0 behind a lens or in front of a mirror = real, b < 0 = virtual;
`ThinLens.ImageDistance`/`Magnification`). Examples (f = 40 mm): g = 2f gives b = 2f, β = −1; g = 1.5f gives b = 3f,
β = −2; g = f/2 gives b = −f, β = 2 (virtual, upright, magnified). A mirror folds the image space back onto its front;
a convex mirror's image lies behind it. At g = f (image at infinity), for images farther than 5 m or magnified more than
50 times, no image is drawn (the construction rays still are, without the focal ray).

`names` (toggle **Names of object and image**) writes the names at the tips: German "G" and "B", English "O" and "I".
The image and its rays are pure paraxial geometry from the document, drawn with the arrow (outside its box, like the focal
point marks of a lens), and follow live when the arrow, the lens or the mirror moves or changes. A ray bundle docked on the
arrow (principal rays, traced) still works independently: the traced rays meet in the drawn image.

## Grips (drag handles)

Values are dragged directly on the drawing (`ObjectType.DragHandles` / `MoveHandle`, page mm; the hosts show and drive
them generically). A drag only writes the parameter; the box (`Size`) and the position never change.

| Element | Grip (key) | Where | Moves | Writes |
|---|---|---|---|---|
| Lenses | `f.back`, and `f.front` when the focal points are drawn | F′ at +f (F at −f) on the axis | along the axis | `f` = distance from the lens, whole mm, 1–5000 (the sign stays with the type) |
| Lenses with their own bundle docked | `beam` | edge of the bundle (start of the parallel rays, or on the lens plane for rays from 2f) | across the axis | `beam` = twice the distance from the axis, whole mm |
| Concave/convex mirror | `f`, and `cc` when F and C are drawn | F (in front of a concave mirror, behind a convex one), C at 2f | along the axis | `f` = distance from the vertex (C: half of it), whole mm |
| Aperture stop / slit | `opening` | inner edge of the upper blade | across the axis | `opening` = share of the half height, steps of 0.01, 0.02–0.95 |
| Lamp, LED (cone) with docked rays | `spread` | on the upper edge ray of the cone, beyond the bulb | freely; the angle seen from the bulb centre counts | `spread` = half cone angle, whole degrees, 1°–80° |
| Lamp, LED (parallel) with docked rays | `beam` | edge of the bundle at the bulb/dome | across the axis | `beam`, whole mm |
| Laser with several rays and docked rays | `beam` | edge of the beam at the output | across the axis | `beam`, whole mm |
| Fibre with docked rays | `spread` | on the upper edge ray of the output cone | freely, angle from the output end | `spread`, whole degrees |
| Thick lens with its own bundle docked | `beam` | edge of the bundle at its start | across the axis | `beam`, whole mm (the focal length follows from shape, box and `n`, so it has no grip) |
| Scanner mirror | `angle` | upper end of the mirror | freely, around the pivot | `angle` = direction of the mirror line, whole degrees, folded into −90° … 90° |
| Polariser, wave plate | `axis` | end of the axis double arrow | freely, around the centre | `angle` = direction of the axis line, whole degrees, −90° … 90° |
| Glass plate (window) | `tilt` | upper end of the plate | freely, around the centre | `tilt` = direction of the plate, whole degrees, −80° … 80°; releases `brewster` |
| Free traced ray | `dir` | on the drawn ray, halfway to its second point or to the first element it meets | freely, around the start (arc) | the second point turned around the first, whole degrees (clockwise on screen), length kept |

Every grip carries a bilingual label with its current value ("Focal length f: 40 mm" / "Brennweite f: 40 mm", "Half cone
angle: 12°", "Start angle: 30°"). The grips of lenses and mirrors belong to the part `focal` when the marks are drawn
(selecting the focal points shows them). Emitter grips only appear while a ray bundle is docked (otherwise there is no cone
to see); a ray docked on an emitter takes its direction from the emitter (turn the emitter) and has no grip of its own;
an untraced ray is edited by its points. Prisms and gratings have no grips (no parameter maps onto a point of the drawing
naturally); the angle of a grating's first order is set in its number field.

## Snapping anchors

- Every element exposes its optical centre as a `Port` anchor (`o`; mirrors: vertex; detectors and screen: front face;
  sources: `exit`, lamp/LED also `o`; fibre: `in`, `exit`; object arrow: `foot`, `tip`).
- Lenses: `f1`/`f2` at ±f and `2f1`/`2f2` at ±2f on the axis; thick lens: `f1`/`f2` at F and F′ (lensmaker's equation);
  curved mirrors: `f` and `cc` (centre of curvature); scanner mirror: `o` at the pivot; light guide: `in`, `exit`, `o`.
  Port-to-port snapping lines up focal points (telescopes: F′ of the objective on F of the eyepiece).
- The optical axis (`optics.axis.line`) exposes its segment as a `Rail` (`axis0` …): element ports dock anywhere on it,
  so elements line up on one axis. No `Hub`/`Axis` roles are used, so no mechanical coupling or "+" build ports appear.

## Parts and cleaning

Part keys (`PartLooks`): `body`, `focal`, `hatch`, `detail`, `label` on elements; `image`, `construction` and `names`
on the object arrow (listed while drawn); `ray` (all rays, each `ray.<i>`), `virtual` and `values` (the readout at the
ends, while shown) on rays; `axis` on the optical axis. They can be restyled or hidden individually.

Clean slides (`Sanitize`): optics elements carry no values worth hiding; the generic pass replaces free-text labels
(designators such as `L1` stay). Focal lengths, indices and positions are kept.

## Persistence

All state is in the document model: parameters, `Size`, `Transform`, the ray's points and the attachment of its first
point (`AnchorId` = `exit`/`o`/`tip`). The traced paths, images, construction rays and grips are not stored; they are
recomputed from the document, so JSON round trips render identically. Entered values are parameters too (text as typed:
`power` "5 mW", `lambda` "532 nm", `g`, `b`, `area` "5 mm²", `lines` "600"; the focal length in `f`, the index in `n`,
the dispersion in `dn`); derived values are never stored.

## Host notes

- Everything is drawn with native primitives (lines, polylines, polygons, rectangles, ellipses, text, line-end
  arrowheads); no vector pictures are needed.
- Rays depend on neighbours: hosts that keep objects in separate units (PowerPoint COM) must render rays with the other
  units of the slide as context and redraw the dependents reported after an edit, as they already do for walls and lines.
- **Add ray bundle** inserts a new object (`ContextAction.ApplyInDocument`). The engine (Office web, LibreOffice) applies
  that, and the PowerPoint COM add-in inserts the new objects of an action result as new units. The library item
  "Light ray (traced)" docked by dragging its start onto a source works in every host.
- The stored points of a docked ray are not updated by the trace; hosts that show point handles show the docked start
  and the (unused) second point.
- **Object arrows with an image depend on neighbours** like rays: render them with the slide's other units as context and
  redraw them when a lens or mirror they image through is edited (they are reported as its dependents). The image lies
  outside the arrow's box, so the arrow's native group grows with it (like a lens with its focal point marks).
- **Grips:** the COM add-in shows and drags the grips of any type generically (preview of the dragged element, one undo
  step on release, dependents redrawn then). Rays and images in *other* units follow on release, not during the drag: a
  live follow while dragging a grip or moving a source, lens or mirror needs the host to re-render the reported
  dependents with the moved element in its drag preview (open item). The engine's
  `moveHandle` (web, LibreOffice) recognises a change by parameters and size only, so the free ray's direction grip, which
  changes the ray's points, needs `moveHandle` to compare the points too.

## Limits and next steps

- Two-dimensional, sequential; thin lenses and curved mirrors are paraxial ideals. No interference. Fresnel losses only
  where `coating` is not ideal; partial reflections at glass faces are drawn as rays only with `ghosts` (first order in
  practice, not at thin lenses). Mirrors are ideal (no metal reflectance or phase), splitters ideal (no extinction ratio,
  no absorption). The anti-reflection coating is an idealised single layer (index √n).
- Element values: the received power counts only the drawn rays (a sample of the bundle, not radiometry: the irradiance
  is a sample estimate over an assumed area, no étendue, no beam profile); results of the trace and of the drawing are
  read-only (the received power stays enterable). The system focal length covers two lenses (thin or thick), not chains
  of three or more. Grating values assume normal incidence (the tracer handles oblique light) and take the wavelength of
  the light only when one wavelength reaches the grating; the prism's spread covers the F and C lines at the d-line
  incidence of minimum deviation; mirrors have no values for oblique (off-axis) use.
- The object arrow's image construction uses thin lenses and curved mirrors only (not the thick lens; its traced rays do
  pass it). The thick lens has no focal-length grip.
- The spectrometer does not trace the light inside; its spectrum is a schematic drawing.
- The fibre does not guide light from its input to its output; a bundle on the output end starts a new cone.
- Light reaching the inside of an element from an unusual side (e.g. through the side of a lens) is treated by the
  element's single optical surface.
- The image construction uses the first lens or curved mirror on the arrow's axis only (no image of an image through a
  second element); the traced ray bundle does pass every element.
- Possible next steps: images through several elements, thick-lens imaging for the object arrow, system values for
  chains of lenses and mirrors, ghosts of thin lenses, Michelson/Mach-Zehnder templates.
