using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>A light source with an optical power value (lamp, LED, laser): its docked ray bundles carry that power.</summary>
    internal interface ILightSource { }

    /// <summary>
    /// An element whose values are read from the traced light or from neighbouring elements (detectors, screens, lenses with
    /// a second lens, the object arrow): while its values label is shown, the other optics elements of its page report it as
    /// a dependent, so the label follows every change of the beam path.
    /// </summary>
    internal interface ITraceValues { }

    /// <summary>
    /// The light an element receives, collected from the traced rays of its page: per bundle source the share k of the
    /// emitted power (relative intensity at the element × power share of the ray ÷ rays of the bundle) and the
    /// intensity-weighted coherency matrix (A, B, C) of that light.
    /// </summary>
    internal sealed class Reception
    {
        public sealed class Part
        {
            /// <summary>The source element (lamp, LED, laser) or "ray:&lt;id&gt;" for a bundle without a power value.</summary>
            public string Key = "";
            /// <summary>The light source whose power <c>P</c> the share applies to; null for a bundle without one (weight 1).</summary>
            public string? SourceId;
            /// <summary>Share of the emitted power that arrives.</summary>
            public double K;
            /// <summary>Coherency matrix weighted with the share: Σ k·A, Σ k·B, Σ k·Re C, Σ k·Im C.</summary>
            public double A, B, Cr, Ci;
        }

        public List<Part> Parts { get; } = new();

        /// <summary>True if some of the light went through an assumed power division (schematic splitter, grating orders).</summary>
        public bool Estimate { get; set; }

        public Part For(string key, string? sourceId)
        {
            foreach (var p in Parts) if (p.Key == key) return p;
            var part = new Part { Key = key, SourceId = sourceId };
            Parts.Add(part);
            return part;
        }
    }

    /// <summary>
    /// The light arriving at the elements of a page: every traced ray object of the page is traced as it is drawn, and the
    /// paths that end on an element (detector, screen, stop, housing) are summed per element. Cached per document instance
    /// (documents are immutable), so the values of several detectors trace the page once.
    /// </summary>
    internal static class Light
    {
        internal sealed class PageLight
        {
            public Dictionary<string, Reception> ByElement { get; } = new(StringComparer.Ordinal);
            /// <summary>True if the page has at least one traced ray object.</summary>
            public bool AnyRays { get; set; }
            /// <summary>The traces of the page's ray objects (the light reaching an element that passes it, a grating).</summary>
            public List<TraceResult> Traces { get; } = new();
        }

        private static readonly ConditionalWeakTable<SketchDocument, Dictionary<string, PageLight>> Cache = new();

        /// <summary>The light on the page of <paramref name="objectId"/>.</summary>
        public static PageLight OfPage(SketchDocument doc, string objectId)
        {
            var page = doc.PageOf(objectId);
            var key = page?.Id ?? "";
            var perDoc = Cache.GetValue(doc, _ => new Dictionary<string, PageLight>(StringComparer.Ordinal));
            lock (perDoc)
            {
                if (perDoc.TryGetValue(key, out var found)) return found;
                var light = Compute(doc, page);
                perDoc[key] = light;
                return light;
            }
        }

        /// <summary>What <paramref name="elementId"/> receives, or null if no traced ray ends on it.</summary>
        public static Reception? Received(SketchDocument doc, string elementId)
            => OfPage(doc, elementId).ByElement.TryGetValue(elementId, out var r) ? r : null;

        private static PageLight Compute(SketchDocument doc, Page? page)
        {
            var result = new PageLight();
            var objects = page is null ? doc.Objects.Values : doc.ObjectsOn(page);
            var hidden = new HashSet<string>(doc.Layers.Where(l => !l.Visible).Select(l => l.Id), StringComparer.Ordinal);
            var bundles = new List<(string Key, string? Source, TraceResult Trace)>();
            var perKey = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var ray in objects)
            {
                if (ray.Type != RayType.TypeId || !ray.Flag("trace", true) || ray.Layer is not null && hidden.Contains(ray.Layer)) continue;
                TraceResult trace;
                try { trace = Beams.Trace(ray, doc); }
                catch (Exception) { continue; }
                result.AnyRays = true;
                result.Traces.Add(trace);
                var emitter = Beams.Emitter(ray, doc);
                var source = emitter is not null && OpticsModule.TypeOf(emitter.Type) is ILightSource ? emitter.Id : null;
                var key = source ?? "ray:" + ray.Id;
                bundles.Add((key, source, trace));
                perKey[key] = perKey.TryGetValue(key, out var c) ? c + 1 : 1;
            }
            foreach (var (key, source, trace) in bundles)
            {
                // Several bundles docked on one source share its power.
                var n = (double)Math.Max(1, trace.Starts) * perKey[key];
                foreach (var path in trace.Paths)
                {
                    if (path.Fan || !path.Absorbed || path.AbsorbedBy is not string at || path.Intensities.Count == 0) continue;
                    var k = path.Intensities[path.Intensities.Count - 1] * path.Weight / n;
                    if (k <= 0) continue;
                    if (!result.ByElement.TryGetValue(at, out var rec)) result.ByElement[at] = rec = new Reception();
                    var part = rec.For(key, source);
                    var pol = path.Polarizations[path.Polarizations.Count - 1] ?? Coherency.Unpolarized;
                    part.K += k;
                    part.A += k * pol.A;
                    part.B += k * pol.B;
                    part.Cr += k * pol.Cr;
                    part.Ci += k * pol.Ci;
                    rec.Estimate |= path.Estimate;
                }
            }
            return result;
        }

        /// <summary>
        /// The wavelengths (nm, 0 = white light) of the traced light that meets the segment from <paramref name="a"/> to
        /// <paramref name="b"/> (page coordinates) on the page of <paramref name="elementId"/>: one entry per ray segment that
        /// ends on it.
        /// </summary>
        public static List<double> ArrivingWavelengths(SketchDocument doc, string elementId, Vec2 a, Vec2 b)
        {
            var list = new List<double>();
            var ab = b - a;
            var l2 = ab.Dot(ab);
            if (l2 < 1e-12) return list;
            foreach (var trace in OfPage(doc, elementId).Traces)
                foreach (var path in trace.Paths)
                    for (var i = 1; i < path.Points.Count; i++)
                    {
                        var p = path.Points[i];
                        var t = (p - a).Dot(ab) / l2;
                        if (t < -1e-9 || t > 1 + 1e-9) continue;
                        if ((a + ab * t).DistanceTo(p) < 1e-6) list.Add(path.Lambdas[i - 1]);
                    }
            return list;
        }
    }

    /// <summary>
    /// Element values of the optics module (engineering-calculations conventions: SI internally – lengths in m, powers in W,
    /// angles in rad; focal lengths are in the drawing scale, so 40 mm on the page is f = 0.04 m and D = 25 dpt):
    /// light sources (power P, wavelength λ, photon energy), detectors and screens (received power, share of the emitted
    /// light, degree and angle of polarisation – results of the trace – and the irradiance as a sample estimate), thin lenses
    /// (f, refractive power D = 1/f, the thin lens equation 1/f = 1/g + 1/b with magnification β = −b/g, and with a second
    /// lens on the axis the system focal length 1/f = 1/f₁ + 1/f₂ − d/(f₁·f₂), d between the principal planes), the thick lens
    /// (f from the lensmaker's equation, principal planes, system focal length), curved mirrors (f = R/2, mirror equation),
    /// the prism (drawn apex angle, minimum deviation, dispersion), the diffraction grating (line density, period, grating
    /// equation for the orders drawn) and the object arrow (g, b, β of the drawn image).
    /// </summary>
    internal static class OpticsValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Planck constant and speed of light (exact SI values) [S1].</summary>
        private const double H = 6.62607015e-34, C0 = 299792458;

        // ------------------------------------------------------------------ parameters (values only)

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        public static readonly ParamDef PowerParam = Text("power", T("Optical power (empty = 1 mW)", "Optische Leistung (leer = 1 mW)"), "mW");
        public static readonly ParamDef LambdaParam = Text("lambda", T("Wavelength (empty = from the colour)", "Wellenlänge (leer = aus der Farbe)"), "nm");
        public static readonly ParamDef ObjectDistanceParam = Text("g", T("Object distance g (values)", "Gegenstandsweite g (Werte)"), "mm");
        public static readonly ParamDef ImageDistanceParam = Text("b", T("Image distance b (values)", "Bildweite b (Werte)"), "mm");

        // ------------------------------------------------------------------ text-valued kinds

        internal static readonly QuantityKind PolarisationKind = Quantities.Text("optics.polarisation", T("Polarisation", "Polarisation"), new[]
        {
            T("Unpolarised", "Unpolarisiert"), T("Linear", "Linear"), T("Circular", "Zirkular"), T("Elliptical", "Elliptisch"),
            T("Partially polarised", "Teilweise polarisiert"),
        });

        internal static readonly QuantityKind ImageKind = Quantities.Text("optics.image", T("Image", "Bild"), new[] { T("Real", "Reell"), T("Virtual", "Virtuell") });

        // ------------------------------------------------------------------ light sources

        public static readonly VarDef Power = new("P", T("Optical power", "Optische Leistung"), T("P", "P"), Quantities.Power)
        {
            Unit = "mW", Param = "power", Default = 1e-3, Min = 0, Max = 1e3,
        };

        public static readonly VarDef Lambda = new("lambda", T("Wavelength", "Wellenlänge"), T("λ", "λ"), Quantities.Length)
        {
            Unit = "nm", Param = "lambda", Min = 1e-8, Max = 1e-4,
        };

        public static readonly VarDef PhotonEnergy = new("Eph", T("Photon energy", "Photonenenergie"), T("E_ph", "E_ph"), Quantities.Energy)
        {
            Unit = "eV", Relevance = VarRelevance.Panel, Min = 0, Max = 1e-16,
        };

        /// <summary>Photon energy E = h·c/λ (h, c exact in the SI) [S1].</summary>
        public static readonly Relation PhotonRelation =
            new Relation("optics.source.photon", T("Photon energy", "Photonenenergie"), "{Eph} = h·c/{lambda}", "Eph", "lambda") { Source = "S1" }
                .Solve("Eph", "h·c/{lambda}", v => v("lambda") > 0 ? H * C0 / v("lambda") : double.NaN);

        public static readonly IReadOnlyList<VarDef> LampVariables = new[] { Power };
        public static readonly IReadOnlyList<VarDef> ColourSourceVariables = new[] { Power, Lambda, PhotonEnergy };
        public static readonly IReadOnlyList<Relation> ColourSourceRelations = new[] { PhotonRelation };

        public static readonly ValueCheck InvisibleCheck = new("optics.source.invisible", CheckSeverity.Hint,
            T("The wavelength lies outside the visible range (about 380 nm to 780 nm); the rays are drawn in the nearest colour.",
              "Die Wellenlänge liegt außerhalb des sichtbaren Bereichs (etwa 380 nm bis 780 nm); die Strahlen werden in der nächsten Farbe gezeichnet."),
            "λ < 380 nm or λ > 780 nm", c => c["lambda"] is double l && (l < 380e-9 || l > 780e-9));

        public static readonly IReadOnlyList<ValueCheck> SourceChecks = new[] { InvisibleCheck };

        /// <summary>The wavelength entered on a source (nm), or null when none (or an invalid one) is entered.</summary>
        public static double? EnteredWavelength(SketchObject o)
        {
            var (value, _, _, error) = ValueModel.ReadEntered(o, Lambda);
            if (error is not null || value is not double si) return null;
            var nm = si * 1e9;
            return nm >= 10 && nm <= 100000 ? nm : null;
        }

        /// <summary>
        /// The wavelength of a source as marking when none is entered: the one of its colour choice
        /// (<paramref name="colourNm"/>; 0 = white light, no wavelength).
        /// </summary>
        public static IEnumerable<BoundRelation> SourceLinks(SketchObject o, RenderContext ctx, double colourNm, bool hasLambda)
        {
            if (hasLambda && colourNm > 0 && EnteredWavelength(o) is null)
            {
                var rel = new Relation("optics.source.lambda", T("Wavelength of the colour", "Wellenlänge der Farbe"), "{lambda} = λ(colour)", "lambda")
                    { Validity = T("from the colour choice; enter a wavelength to set another one", "aus der Farbwahl; eine eingegebene Wellenlänge gilt stattdessen") }
                    .Solve("lambda", "λ(colour)", _ => colourNm * 1e-9);
                yield return BoundRelation.Local(rel, o.Id);
            }
            // The received power of every element its light reaches (the element reports the same relation).
            var page = Light.OfPage(ctx.Doc, o.Id);
            foreach (var kv in page.ByElement)
                if (kv.Value.Parts.Any(p => p.SourceId == o.Id && p.K > 0) && ctx.Doc.Get(kv.Key) is { } det
                    && OpticsModule.TypeOf(det.Type) is IValueType && ReceivedPower(det.Id, kv.Value) is { } b)
                    yield return b;
        }

        /// <summary>The colour choice of a source as options that also clear an entered wavelength (the colour then counts).</summary>
        public static IEnumerable<ContextAction> ColourChoice(SketchObject o, string id, I18nText label, ParamDef def, string group)
            => Ox.Choice(o, id, label, def, group).Select(a => a with { Apply = x => Without(a.Apply(x), LambdaParam.Key) });

        /// <summary>The object without parameter <paramref name="key"/>.</summary>
        public static SketchObject Without(SketchObject o, string key)
        {
            if (!o.Params.ContainsKey(key)) return o;
            var d = new Dictionary<string, ParamValue>(o.Params.Count);
            foreach (var kv in o.Params) if (kv.Key != key) d[kv.Key] = kv.Value;
            return o with { Params = d };
        }

        // ------------------------------------------------------------------ detectors and screens

        /// <summary>The receiving area of a detector or screen for the irradiance (entered in <c>area</c>, else a sample from the drawn height).</summary>
        public static readonly VarDef ReceivingArea = new("Ad", T("Receiving area (sample estimate)", "Empfangsfläche (Beispielschätzung)"), T("A", "A"), Quantities.Area)
        {
            Unit = "mm²", Param = "area", Min = 1e-12, Max = 1, Relevance = VarRelevance.Panel,
        };

        public static readonly IReadOnlyList<VarDef> DetectorVariables = new[]
        {
            new VarDef("Prx", T("Received power", "Empfangene Leistung"), T("P_e", "P_E"), Quantities.Power) { Unit = "mW", Min = 0, Max = 1e3 },
            new VarDef("eta", T("Share of the emitted light", "Anteil des ausgesandten Lichts"), T("η", "η"), Quantities.Ratio) { Unit = "%", Min = 0, Max = 1, ReadOnly = true },
            new VarDef("V", T("Degree of polarisation", "Polarisationsgrad"), T("V", "V"), Quantities.Ratio) { Unit = "%", Min = 0, Max = 1, ReadOnly = true },
            new VarDef("theta", T("Polarisation angle (0° = in the drawing plane)", "Polarisationswinkel (0° = in der Zeichenebene)"), T("θ", "θ"), Quantities.Angle)
                { Unit = "°", Min = -Math.PI / 2, Max = Math.PI / 2, ReadOnly = true },
            new VarDef("pol", T("Polarisation", "Polarisation"), T("Pol.", "Pol."), PolarisationKind) { Relevance = VarRelevance.Panel },
            ReceivingArea,
            new VarDef("Ee", T("Irradiance (sample estimate)", "Bestrahlungsstärke (Beispielschätzung)"), T("E_e", "E_e"), Quantities.Irradiance)
                { Unit = "W/m²", Min = 0, Max = 1e9, Relevance = VarRelevance.Panel, ReadOnly = true },
        };

        public static readonly ParamDef AreaParam = Text("area",
            T("Receiving area for the irradiance (empty = sample: a square as high as the drawn element)", "Empfangsfläche für die Bestrahlungsstärke (leer = Beispiel: ein Quadrat so hoch wie das gezeichnete Element)"), "mm²");

        private static readonly I18nText IrradianceValidity = T(
            "sample estimate: the received power spread evenly over the receiving area (no beam profile; the drawn rays are a sample of the bundle)",
            "Beispielschätzung: die empfangene Leistung gleichmäßig über die Empfangsfläche verteilt (kein Strahlprofil; die gezeichneten Strahlen sind eine Stichprobe des Bündels)");

        /// <summary>Irradiance E_e = P_e / A as a sample estimate (power spread evenly over the receiving area).</summary>
        public static readonly Relation IrradianceRelation =
            new Relation("optics.light.irradiance", T("Irradiance (sample estimate)", "Bestrahlungsstärke (Beispielschätzung)"), "{Ee} = {Prx}/{Ad}", "Ee", "Prx", "Ad")
                { Source = "S41", Validity = IrradianceValidity }
                .Solve("Ee", "{Prx}/{Ad}", v => v("Ad") > 0 ? v("Prx") / v("Ad") : double.NaN);

        public static readonly IReadOnlyList<Relation> DetectorRelations = new[] { IrradianceRelation };

        /// <summary>The receiving area when none is entered: a square face as high as the drawn element (h², a sample).</summary>
        private static IEnumerable<BoundRelation> AreaLinks(SketchObject o)
        {
            var (entered, _, _, error) = ValueModel.ReadEntered(o, ReceivingArea);
            if (entered is not null && error is null || o.Size.H <= 0) yield break;
            var h = o.Size.H / 1000;
            var rel = new Relation("optics.light.area", T("Sample receiving area", "Beispiel-Empfangsfläche"), "{Ad} = h²", "Ad")
                { Validity = T("sample estimate: a square face as high as the drawn element; enter the real area in its place", "Beispielschätzung: eine quadratische Fläche so hoch wie das gezeichnete Element; die tatsächliche Fläche lässt sich stattdessen eingeben") }
                .Solve("Ad", "h²", _ => h * h);
            yield return BoundRelation.Local(rel, o.Id);
        }

        private static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static readonly I18nText TraceValidity = T(
            "from the traced rays drawn on the page: relative intensity at the element × power share of the ray ÷ rays of the bundle",
            "aus den gezeichneten, verfolgten Strahlen der Seite: relative Intensität am Element × Leistungsanteil des Strahls ÷ Strahlen des Bündels");

        /// <summary>
        /// Received power P_e = Σ kᵢ·Pᵢ over the light sources whose bundles reach <paramref name="elementId"/> (solved for
        /// any source power too); 0 when nothing arrives; null when only bundles without a source power arrive.
        /// </summary>
        internal static BoundRelation? ReceivedPower(string elementId, Reception? rec)
        {
            var sources = rec?.Parts.Where(p => p.SourceId is not null && p.K > 0).ToList() ?? new List<Reception.Part>();
            if (sources.Count == 0 && rec is not null && rec.Parts.Any(p => p.K > 0)) return null;
            var vars = new[] { "Prx" }.Concat(sources.Select((_, i) => "P" + i.ToString(CultureInfo.InvariantCulture))).ToArray();
            var sum = sources.Count == 0 ? "0" : string.Join(" + ", sources.Select((p, i) => Num(p.K) + "·{P" + i.ToString(CultureInfo.InvariantCulture) + "}"));
            var rel = new Relation("optics.light.power", T("Received power", "Empfangene Leistung"), "{Prx} = " + sum, vars) { Validity = TraceValidity }
                .Solve("Prx", sum, v =>
                {
                    var s = 0.0;
                    for (var i = 0; i < sources.Count; i++) s += sources[i].K * v("P" + i.ToString(CultureInfo.InvariantCulture));
                    return s;
                });
            for (var i = 0; i < sources.Count; i++)
            {
                var j = i;
                var others = string.Join("", sources.Select((p, q) => q == j ? "" : " − " + Num(p.K) + "·{P" + q.ToString(CultureInfo.InvariantCulture) + "}"));
                rel.Solve("P" + j.ToString(CultureInfo.InvariantCulture), "({Prx}" + others + ")/" + Num(sources[j].K), v =>
                {
                    var s = v("Prx");
                    for (var q = 0; q < sources.Count; q++) if (q != j) s -= sources[q].K * v("P" + q.ToString(CultureInfo.InvariantCulture));
                    return s / sources[j].K;
                });
            }
            var map = new Dictionary<string, VarRef> { ["Prx"] = new VarRef(elementId, "Prx") };
            for (var i = 0; i < sources.Count; i++) map["P" + i.ToString(CultureInfo.InvariantCulture)] = new VarRef(sources[i].SourceId!, "P");
            return new BoundRelation(rel, map);
        }

        /// <summary>
        /// The relations of a detector or screen: received power, share of the emitted light (weighted by the source powers;
        /// bundles without a source count as power 1), degree of polarisation, polarisation angle and state of the light
        /// arriving (incoherent sum of the coherency matrices, weighted like the share); the sample receiving area unless one
        /// is entered (the irradiance relation is static).
        /// </summary>
        public static IEnumerable<BoundRelation> DetectorLinks(SketchObject o, RenderContext ctx)
        {
            foreach (var area in AreaLinks(o)) yield return area;
            var rec = Light.Received(ctx.Doc, o.Id);
            if (ReceivedPower(o.Id, rec) is { } power) yield return power;
            var parts = rec?.Parts.Where(p => p.K > 0).ToList() ?? new List<Reception.Part>();
            var sources = parts.Where(p => p.SourceId is not null).ToList();
            var map = new Dictionary<string, VarRef>();
            foreach (var key in new[] { "eta", "V", "theta", "pol" }) map[key] = new VarRef(o.Id, key);
            for (var i = 0; i < sources.Count; i++) map["P" + i.ToString(CultureInfo.InvariantCulture)] = new VarRef(sources[i].SourceId!, "P");
            var pv = sources.Select((_, i) => "P" + i.ToString(CultureInfo.InvariantCulture)).ToArray();

            // Weights: the source power for bundles of a light source, 1 for the others.
            double Weight(Reception.Part p, Func<string, double> v) => p.SourceId is null ? 1 : v("P" + sources.IndexOf(p).ToString(CultureInfo.InvariantCulture));
            (double A, double B, double Cr, double Ci, double K, double W) Sum(Func<string, double> v)
            {
                double a = 0, b = 0, cr = 0, ci = 0, k = 0, w = 0;
                foreach (var p in parts)
                {
                    var x = Weight(p, v);
                    a += x * p.A; b += x * p.B; cr += x * p.Cr; ci += x * p.Ci; k += x * p.K; w += x;
                }
                return (a, b, cr, ci, k, w);
            }
            BoundRelation Bind(string id, I18nText name, string target, string formula, Func<Func<string, double>, double> f)
            {
                var vars = new[] { target }.Concat(pv).ToArray();
                var rel = new Relation(id, name, "{" + target + "} = " + formula, vars) { Validity = TraceValidity }.Solve(target, formula, f);
                return new BoundRelation(rel, vars.ToDictionary(k => k, k => map[k]));
            }

            if (parts.Count == 0)
            {
                yield return Bind("optics.light.share", T("Share of the emitted light", "Anteil des ausgesandten Lichts"), "eta", "0", _ => 0);
                yield break;
            }
            yield return Bind("optics.light.share", T("Share of the emitted light", "Anteil des ausgesandten Lichts"), "eta", "Σ kᵢ·Pᵢ / Σ Pᵢ", v =>
            {
                var s = Sum(v);
                return s.W > 0 ? s.K / s.W : double.NaN;
            });
            // Normalised coherency matrix of all the light arriving.
            (double A, double B, double Cr, double Ci)? State(Func<string, double> v)
            {
                var s = Sum(v);
                return s.K > 1e-15 ? (s.A / s.K, s.B / s.K, s.Cr / s.K, s.Ci / s.K) : null;
            }
            double Degree((double A, double B, double Cr, double Ci) c) => Math.Min(1, Math.Sqrt((c.A - c.B) * (c.A - c.B) + 4 * (c.Cr * c.Cr + c.Ci * c.Ci)));
            yield return Bind("optics.light.degree", T("Degree of polarisation", "Polarisationsgrad"), "V", "√((A − B)² + 4·|C|²)",
                v => State(v) is { } c ? Degree(c) : double.NaN);
            yield return Bind("optics.light.angle", T("Polarisation angle", "Polarisationswinkel"), "theta", "½·atan2(2·Re C, A − B)", v =>
            {
                if (State(v) is not { } c) return double.NaN;
                var deg = Degree(c);
                // No direction for unpolarised or circular light.
                if (deg < 0.005 || Math.Abs(2 * c.Ci) > 0.995 * deg) return double.NaN;
                return 0.5 * Math.Atan2(2 * c.Cr, c.A - c.B);
            });
            yield return Bind("optics.light.state", T("Polarisation of the light", "Polarisation des Lichts"), "pol", "state(A, B, C)", v =>
            {
                if (State(v) is not { } c) return double.NaN;
                var deg = Degree(c);
                if (deg < 0.005) return 0;
                if (deg < 0.995) return 4;
                var circ = Math.Abs(2 * c.Ci);
                return circ > 0.995 ? 2 : circ > 0.02 ? 3 : 1;
            });
        }

        public static readonly IReadOnlyList<ValueCheck> DetectorChecks = new[]
        {
            new ValueCheck("optics.light.none", CheckSeverity.Info,
                T("No traced light ray of the page reaches this element.", "Kein verfolgter Lichtstrahl der Seite erreicht dieses Element."),
                "no ray of the page ends here", c => Light.OfPage(c.Context.Doc, c.Object.Id) is { AnyRays: true } p && !p.ByElement.ContainsKey(c.Object.Id)),
            new ValueCheck("optics.light.nosource", CheckSeverity.Info,
                T("Only rays without a light source arrive (free rays, a lens's or an object's own bundle): the received power needs a bundle docked on a lamp, LED or laser.",
                  "Es kommen nur Strahlen ohne Lichtquelle an (freie Strahlen, Bündel einer Linse oder eines Gegenstands): die empfangene Leistung braucht ein Bündel an einer Lampe, LED oder einem Laser."),
                "no bundle of a light source arrives", c => Light.Received(c.Context.Doc, c.Object.Id) is { } r && r.Parts.Count > 0 && r.Parts.All(p => p.SourceId is null)),
            new ValueCheck("optics.light.estimate", CheckSeverity.Hint,
                T("Part of the light passes a schematic beam splitter or a grating: the division of the power is assumed (equal shares), not computed.",
                  "Ein Teil des Lichts läuft über einen schematischen Strahlteiler oder ein Gitter: die Aufteilung der Leistung ist angenommen (gleiche Anteile), nicht berechnet."),
                "schematic splitter or grating in the path", c => Light.Received(c.Context.Doc, c.Object.Id) is { Estimate: true }),
        };

        // ------------------------------------------------------------------ thin lenses

        /// <summary>Variables of a converging (<paramref name="converging"/>) or diverging thin lens.</summary>
        public static IReadOnlyList<VarDef> LensVariables(bool converging) => new[]
        {
            new VarDef("f", converging ? T("Focal length", "Brennweite") : T("Focal length (amount; the lens diverges, f < 0)", "Brennweite (Betrag; die Linse zerstreut, f < 0)"),
                T("f", "f"), Quantities.Length) { Unit = "mm", StoredUnit = "mm", Param = "f", Default = 0.04, Min = 1e-3, Max = 5 },
            new VarDef("D", T("Refractive power", "Brechkraft"), T("D", "D"), Quantities.OpticalPower) { Unit = "dpt", Min = -1000, Max = 1000 },
            new VarDef("g", T("Object distance", "Gegenstandsweite"), T("g", "g"), Quantities.Length) { Unit = "mm", Param = "g", Min = -100, Max = 100 },
            new VarDef("b", T("Image distance", "Bildweite"), T("b", "b"), Quantities.Length) { Unit = "mm", Param = "b", Min = -100, Max = 100 },
            new VarDef("beta", T("Magnification", "Abbildungsmaßstab"), T("β", "β"), Quantities.Ratio) { Relevance = VarRelevance.Panel, Min = -1e4, Max = 1e4 },
        }.Concat(SystemVariables("d")).ToArray();

        /// <summary>
        /// The values with the next lens on the axis (thin or thick): system focal length, the drawn distance between the
        /// principal planes (key <paramref name="distanceKey"/>) and the back focal distance of the system.
        /// </summary>
        private static IEnumerable<VarDef> SystemVariables(string distanceKey) => new[]
        {
            new VarDef("fs", T("System focal length with the next lens", "Systembrennweite mit der nächsten Linse"), T("f_s", "f_s"), Quantities.Length)
                { Unit = "mm", Relevance = VarRelevance.Panel, Min = -100, Max = 100, ReadOnly = true },
            new VarDef(distanceKey, T("Distance to the next lens (principal planes H′ to H)", "Abstand zur nächsten Linse (Hauptebenen H′ bis H)"),
                T(distanceKey, distanceKey), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Panel, Min = -100, Max = 100, ReadOnly = true },
            new VarDef("sF", T("Back focal distance of the system (behind the next lens's H′)", "Bildseitige Schnittweite des Systems (hinter H′ der nächsten Linse)"),
                T("s′_F", "s′_F"), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Panel, Min = -100, Max = 100, ReadOnly = true },
        };

        private static double Div(double a, double b) => Math.Abs(b) < 1e-15 ? double.NaN : a / b;

        /// <summary>
        /// The imaging equation 1/f = 1/g + 1/b (signed f: <paramref name="s"/>·f) and the magnification β = −b/g with
        /// relation ids <paramref name="prefix"/>.imaging and .magnification.
        /// </summary>
        private static Relation[] ImagingRelations(string prefix, double s, I18nText name, I18nText validity)
        {
            var sf = s > 0 ? "{f}" : "(−{f})";
            double Positive(double v) => v > 0 ? v : double.NaN;
            return new[]
            {
                new Relation(prefix + ".imaging", name, "1/" + sf + " = 1/{g} + 1/{b}", "f", "g", "b") { Source = "S41", Validity = validity }
                    .Solve("b", "{g}·" + sf + "/({g} − " + sf + ")", v => Div(v("g") * s * v("f"), v("g") - s * v("f")))
                    .Solve("g", "{b}·" + sf + "/({b} − " + sf + ")", v => Div(v("b") * s * v("f"), v("b") - s * v("f")))
                    .Solve("f", (s > 0 ? "" : "−") + "{g}·{b}/({g} + {b})", v => Positive(s * Div(v("g") * v("b"), v("g") + v("b")))),
                new Relation(prefix + ".magnification", T("Magnification", "Abbildungsmaßstab"), "{beta} = −{b}/{g}", "beta", "b", "g") { Source = "S41" }
                    .Solve("beta", "−{b}/{g}", v => -Div(v("b"), v("g")))
                    .Solve("b", "−{beta}·{g}", v => -v("beta") * v("g"))
                    .Solve("g", "−{b}/{beta}", v => -Div(v("b"), v("beta"))),
            };
        }

        /// <summary>D = ±1/f, the thin lens equation and the magnification, with the sign of the lens type.</summary>
        public static IReadOnlyList<Relation> LensRelations(bool converging)
        {
            var s = converging ? 1.0 : -1.0;
            var sf = converging ? "{f}" : "(−{f})";
            return new[]
            {
                new Relation("optics.lens.power", T("Refractive power", "Brechkraft"), "{D} = 1/" + sf, "D", "f") { Source = "S41" }
                    .Solve("D", "1/" + sf, v => Div(1, s * v("f")))
                    .Solve("f", converging ? "1/{D}" : "−1/{D}", v => Div(s, v("D")) is var f && f > 0 ? f : double.NaN),
            }.Concat(ImagingRelations("optics.lens", s, T("Thin lens equation", "Linsengleichung"),
                T("ideal thin lens, paraxial; g > 0 in front of the lens, b > 0 behind it (real image)", "ideale dünne Linse, paraxial; g > 0 vor der Linse, b > 0 dahinter (reelles Bild)"))).ToArray();
        }

        /// <summary>
        /// The principal planes H and H′ of a lens on its local x axis (light along +x): the centre of a thin lens, H and H′ of
        /// a thick lens from its focal points; null for other elements and an afocal thick lens.
        /// </summary>
        internal static (double H, double H2)? PrincipalPlanes(SketchObject o)
        {
            switch (OpticsModule.TypeOf(o.Type))
            {
                case LensType:
                    return (0, 0);
                case ThickLensType:
                    var g = ThickLensType.Of(o);
                    if (g.Foci is not { } foci) return null;
                    return (foci.Front + g.F, foci.Back - g.F);
                default:
                    return null;
            }
        }

        /// <summary>Sign that makes the focal length variable <c>f</c> of a lens signed: ±1 for thin lenses (amount), 1 for the thick lens (signed).</summary>
        internal static double FocalSign(SketchObject o) => OpticsModule.TypeOf(o.Type) is LensType lt && !lt.Converging ? -1 : 1;

        /// <summary>
        /// The next lens (thin or thick) on the axis of <paramref name="o"/> (its centre ahead on the local +x axis of
        /// <paramref name="o"/>, within half its larger box side of the axis, the axes parallel), the sign of its focal length
        /// variable and the distance from the principal plane H′ of <paramref name="o"/> to the principal plane H of the next
        /// lens (mm; for two thin lenses the distance of the centres). A thick lens turned round meets the light with its H′.
        /// </summary>
        public static (SketchObject Lens, double Sign, double Distance)? NextLens(SketchObject o, SketchDocument doc)
        {
            if (o.IsPointGeometry || PrincipalPlanes(o) is not { } own) return null;
            var page = doc.PageOf(o.Id);
            if (page is null) return null;
            var m = o.Matrix();
            var c = m.Apply(Vec2.Zero);
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var exit = (m.Apply(new Vec2(own.H2, 0)) - c).Dot(axis);
            var hidden = new HashSet<string>(doc.Layers.Where(l => !l.Visible).Select(l => l.Id), StringComparer.Ordinal);
            (SketchObject, double, double, double)? best = null;
            foreach (var x in doc.ObjectsOn(page))
            {
                if (x.Id == o.Id || x.IsPointGeometry || x.Layer is not null && hidden.Contains(x.Layer)) continue;
                if (PrincipalPlanes(x) is not { } planes) continue;
                var mx = x.Matrix();
                var v = mx.Apply(Vec2.Zero) - c;
                var along = v.Dot(axis);
                if (along <= 1e-6 || Math.Abs(v.Cross(axis)) > 0.5 * Math.Max(x.Size.W, x.Size.H)) continue;
                var dir = mx.ApplyLinear(new Vec2(1, 0)).Normalized().Dot(axis);
                if (Math.Abs(dir) < 0.99) continue;
                var entry = (mx.Apply(new Vec2(dir > 0 ? planes.H : planes.H2, 0)) - c).Dot(axis);
                if (best is null || along < best.Value.Item4) best = (x, FocalSign(x), entry - exit, along);
            }
            return best is { } b ? (b.Item1, b.Item2, b.Item3) : null;
        }

        /// <summary>
        /// With a second lens (thin or thick) ahead on the axis: the drawn distance between the principal planes H′₁ and H₂
        /// (marking, key <paramref name="distanceKey"/>), the system focal length 1/f_s = 1/f₁ + 1/f₂ − d/(f₁·f₂) (Gullstrand)
        /// and the back focal distance s′_F = f₂·(f₁ − d)/(f₁ + f₂ − d) behind H′₂; f₁, f₂ signed (<paramref name="s1"/> for
        /// this lens's focal length variable).
        /// </summary>
        public static IEnumerable<BoundRelation> LensLinks(SketchObject o, RenderContext ctx, double s1, string distanceKey = "d")
        {
            if (NextLens(o, ctx.Doc) is not { } next) yield break;
            var dm = next.Distance / 1000;
            var distance = new Relation("optics.lens.distance", T("Drawn distance to the next lens", "Gezeichneter Abstand zur nächsten Linse"), "{d} = |H′₁H₂|", "d")
                { Validity = T("between the principal planes; for thin lenses between the centres", "zwischen den Hauptebenen; bei dünnen Linsen zwischen den Mitten") }
                .Solve("d", "|H′₁H₂|", _ => dm);
            yield return new BoundRelation(distance, new Dictionary<string, VarRef> { ["d"] = new(o.Id, distanceKey) });
            var s2 = next.Sign;
            string F(string key, double sign) => sign > 0 ? "{" + key + "}" : "(−{" + key + "})";
            string f1 = F("f1", s1), f2 = F("f2", s2);
            var map = new Dictionary<string, VarRef>
            {
                ["fs"] = new(o.Id, "fs"), ["sF"] = new(o.Id, "sF"), ["f1"] = new(o.Id, "f"), ["f2"] = new(next.Lens.Id, "f"), ["d"] = new(o.Id, distanceKey),
            };
            var validity = T("two lenses in air on one axis, d between the principal planes H′₁ and H₂ (Gullstrand)",
                "zwei Linsen in Luft auf einer Achse, d zwischen den Hauptebenen H′₁ und H₂ (Gullstrand)");
            double Den(Func<string, double> v) => s1 * v("f1") + s2 * v("f2") - v("d");
            var system = new Relation("optics.lens.system", T("System focal length", "Systembrennweite"), "{fs} = " + f1 + "·" + f2 + "/(" + f1 + " + " + f2 + " − {d})", "fs", "f1", "f2", "d")
                { Source = "S41", Validity = validity }
                .Solve("fs", f1 + "·" + f2 + "/(" + f1 + " + " + f2 + " − {d})", v => Math.Abs(Den(v)) < 1e-12 ? double.NaN : s1 * v("f1") * s2 * v("f2") / Den(v));
            var back = new Relation("optics.lens.backfocus", T("Back focal distance of the system", "Bildseitige Schnittweite des Systems"),
                    "{sF} = " + f2 + "·(" + f1 + " − {d})/(" + f1 + " + " + f2 + " − {d})", "sF", "f1", "f2", "d") { Source = "S41", Validity = validity }
                .Solve("sF", f2 + "·(" + f1 + " − {d})/(" + f1 + " + " + f2 + " − {d})", v => Math.Abs(Den(v)) < 1e-12 ? double.NaN : s2 * v("f2") * (s1 * v("f1") - v("d")) / Den(v));
            yield return new BoundRelation(system, map.Where(kv => system.Vars.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
            yield return new BoundRelation(back, map.Where(kv => back.Vars.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        /// <summary>Checks of a thin lens: virtual image, object in the focal plane, afocal pair (telescope).</summary>
        public static IReadOnlyList<ValueCheck> LensChecks(bool converging) => new[]
        {
            new ValueCheck("optics.lens.virtual", CheckSeverity.Info,
                T("Image distance b < 0: the image is virtual (on the object side of the lens); no light meets there, it cannot be caught on a screen.",
                  "Bildweite b < 0: das Bild ist virtuell (auf der Gegenstandsseite der Linse); dort trifft sich kein Licht, es lässt sich nicht auf einem Schirm auffangen."),
                "b < 0", c => c["b"] is double b && b < 0),
            new ValueCheck("optics.lens.focalplane", CheckSeverity.Info,
                T("The object lies in the focal plane (g = f): the light leaves parallel, the image lies at infinity.",
                  "Der Gegenstand liegt in der Brennebene (g = f): das Licht verlässt die Linse parallel, das Bild liegt im Unendlichen."),
                "g = f", c => converging && c["g"] is double g && c["f"] is double f && Math.Abs(g - f) <= 1e-6 * Math.Max(1e-6, f)),
            AfocalCheck(converging ? 1 : -1, "d"),
        };

        /// <summary>Afocal pair: f₁ + f₂ = d (signed focal lengths, d between the principal planes).</summary>
        private static ValueCheck AfocalCheck(double s1, string distanceKey) => new("optics.lens.afocal", CheckSeverity.Info,
            T("The two lenses form an afocal system (f₁ + f₂ = d, a telescope): parallel light leaves parallel, angular magnification −f₁/f₂.",
              "Die beiden Linsen bilden ein afokales System (f₁ + f₂ = d, ein Fernrohr): paralleles Licht tritt parallel aus, Winkelvergrößerung −f₁/f₂."),
            "f₁ + f₂ = d", c => Afocal(c, s1, distanceKey));

        private static bool Afocal(ValueCheckContext c, double s1, string distanceKey)
        {
            if (c[distanceKey] is not double d || c["f"] is not double f1) return false;
            if (NextLens(c.Object, c.Context.Doc) is not { } next || c.Table.Get(next.Lens.Id, "f")?.Value is not double f2) return false;
            var sum = s1 * f1 + next.Sign * f2;
            return Math.Abs(sum - d) <= 1e-6 * Math.Max(1e-6, Math.Abs(f1) + Math.Abs(f2));
        }

        // ------------------------------------------------------------------ thick lens

        public static readonly IReadOnlyList<VarDef> ThickLensVariables = new[]
        {
            new VarDef("f", T("Focal length (lensmaker's equation)", "Brennweite (Linsenschleiferformel)"), T("f", "f"), Quantities.Length) { Unit = "mm", Min = -100, Max = 100, ReadOnly = true },
            new VarDef("D", T("Refractive power", "Brechkraft"), T("D", "D"), Quantities.OpticalPower) { Unit = "dpt", Min = -1000, Max = 1000 },
            new VarDef("n", T("Refractive index", "Brechzahl"), T("n", "n"), Quantities.Ratio) { Param = "n", Default = 1.5, Min = 1.01, Max = 3, Relevance = VarRelevance.Panel },
            new VarDef("bfd", T("Back focal distance (rear vertex to F′)", "Bildseitige Schnittweite (hinterer Scheitel bis F′)"), T("s′_F", "s′_F"), Quantities.Length)
                { Unit = "mm", Min = -100, Max = 100, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("sH", T("Principal plane H (from the front vertex)", "Hauptebene H (ab dem vorderen Scheitel)"), T("s_H", "s_H"), Quantities.Length)
                { Unit = "mm", Min = -100, Max = 100, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("sH2", T("Principal plane H′ (from the rear vertex)", "Hauptebene H′ (ab dem hinteren Scheitel)"), T("s′_H", "s′_H"), Quantities.Length)
                { Unit = "mm", Min = -100, Max = 100, Relevance = VarRelevance.Panel, ReadOnly = true },
        }.Concat(SystemVariables("e")).ToArray();

        /// <summary>Check of the thick lens: afocal pair with the next lens.</summary>
        public static readonly IReadOnlyList<ValueCheck> ThickLensChecks = new[] { AfocalCheck(1, "e") };

        public static readonly IReadOnlyList<Relation> ThickLensRelations = new[]
        {
            new Relation("optics.thicklens.power", T("Refractive power", "Brechkraft"), "{D} = 1/{f}", "D", "f") { Source = "S41" }
                .Solve("D", "1/{f}", v => Math.Abs(v("f")) < 1e-15 ? double.NaN : 1 / v("f"))
                .Solve("f", "1/{D}", v => Math.Abs(v("D")) < 1e-15 ? double.NaN : 1 / v("D")),
        };

        /// <summary>
        /// The focal length and back focal distance of a thick lens from its index n and the radii and thickness of its drawn
        /// faces (lensmaker's equation; the radii follow from the box and the sag).
        /// </summary>
        public static IEnumerable<BoundRelation> ThickLensLinks(SketchObject o)
        {
            var g = ThickLensType.Of(o);
            double r1 = g.R1 / 1000, r2 = g.R2 / 1000, d = g.D / 1000;
            var validity = T("radii and centre thickness from the drawn lens (box and sag)", "Radien und Mittendicke aus der gezeichneten Linse (Box und Wölbungstiefe)");
            double Finite(double v) => double.IsInfinity(v) || Math.Abs(v) > 1e5 ? double.NaN : v;
            var focal = new Relation("optics.thicklens.focal", T("Lensmaker's equation", "Linsenschleiferformel"),
                    "1/{f} = ({n} − 1)·[1/R₁ − 1/R₂ + ({n} − 1)·d/({n}·R₁·R₂)]", "f", "n") { Source = "S41", Validity = validity }
                .Solve("f", "1/(({n} − 1)·[1/R₁ − 1/R₂ + ({n} − 1)·d/({n}·R₁·R₂)])", v => Finite(ThickLens.FocalLength(v("n"), r1, r2, d)));
            var back = new Relation("optics.thicklens.bfd", T("Back focal distance", "Bildseitige Schnittweite"), "{bfd} = {f}·(1 − ({n} − 1)·d/({n}·R₁))", "bfd", "f", "n")
                    { Source = "S41", Validity = validity }
                .Solve("bfd", "{f}·(1 − ({n} − 1)·d/({n}·R₁))", v => v("f") * (1 - (v("n") - 1) * d / (v("n") * r1)));
            // Principal planes (positive = behind the vertex, in the direction of the light): s_H = −f·(n − 1)·d/(n·R₂),
            // s′_H = −f·(n − 1)·d/(n·R₁); a flat face (R = ∞) puts the plane on the other face's vertex.
            var front = new Relation("optics.thicklens.hfront", T("Principal plane H", "Hauptebene H"), "{sH} = −{f}·({n} − 1)·d/({n}·R₂)", "sH", "f", "n")
                    { Source = "S41", Validity = validity }
                .Solve("sH", "−{f}·({n} − 1)·d/({n}·R₂)", v => -v("f") * (v("n") - 1) * d / (v("n") * r2));
            var rear = new Relation("optics.thicklens.hrear", T("Principal plane H′", "Hauptebene H′"), "{sH2} = −{f}·({n} − 1)·d/({n}·R₁)", "sH2", "f", "n")
                    { Source = "S41", Validity = validity }
                .Solve("sH2", "−{f}·({n} − 1)·d/({n}·R₁)", v => -v("f") * (v("n") - 1) * d / (v("n") * r1));
            yield return BoundRelation.Local(focal, o.Id);
            yield return BoundRelation.Local(back, o.Id);
            yield return BoundRelation.Local(front, o.Id);
            yield return BoundRelation.Local(rear, o.Id);
        }

        // ------------------------------------------------------------------ curved mirrors

        /// <summary>Variables of a concave (<paramref name="concave"/>) or convex mirror: f, R, g, b (values only) and β.</summary>
        public static IReadOnlyList<VarDef> MirrorVariables(bool concave) => new[]
        {
            new VarDef("f", concave ? T("Focal length", "Brennweite") : T("Focal length (amount; the mirror diverges, f < 0)", "Brennweite (Betrag; der Spiegel zerstreut, f < 0)"),
                T("f", "f"), Quantities.Length) { Unit = "mm", StoredUnit = "mm", Param = "f", Default = 0.04, Min = 1e-3, Max = 5 },
            new VarDef("R", T("Radius of curvature (amount, drawn: C at 2f)", "Krümmungsradius (Betrag, gezeichnet: M bei 2f)"), T("R", "R"), Quantities.Length)
                { Unit = "mm", Min = 2e-3, Max = 10, ReadOnly = true },
            new VarDef("g", T("Object distance", "Gegenstandsweite"), T("g", "g"), Quantities.Length) { Unit = "mm", Param = "g", Min = -100, Max = 100 },
            new VarDef("b", T("Image distance (in front of the mirror > 0)", "Bildweite (vor dem Spiegel > 0)"), T("b", "b"), Quantities.Length) { Unit = "mm", Param = "b", Min = -100, Max = 100 },
            new VarDef("beta", T("Magnification", "Abbildungsmaßstab"), T("β", "β"), Quantities.Ratio) { Relevance = VarRelevance.Panel, Min = -1e4, Max = 1e4 },
        };

        /// <summary>f = R/2, the mirror equation 1/f = 1/g + 1/b (concave f &gt; 0, convex f &lt; 0) and β = −b/g.</summary>
        public static IReadOnlyList<Relation> MirrorRelations(bool concave)
        {
            var paraxial = T("spherical mirror, paraxial (rays close to the axis)", "sphärischer Spiegel, paraxial (achsnahe Strahlen)");
            return new[]
            {
                new Relation("optics.mirror.radius", T("Focal length of a spherical mirror", "Brennweite eines Kugelspiegels"), "{f} = {R}/2", "f", "R")
                    { Source = "S41", Validity = paraxial }
                    .Solve("R", "2·{f}", v => 2 * v("f"))
                    .Solve("f", "{R}/2", v => v("R") / 2),
            }.Concat(ImagingRelations("optics.mirror", concave ? 1 : -1, T("Mirror equation", "Spiegelgleichung"),
                T("spherical mirror, paraxial; g > 0 in front of the mirror; b > 0 in front of it (real image), b < 0 behind it (virtual)",
                  "sphärischer Spiegel, paraxial; g > 0 vor dem Spiegel; b > 0 davor (reelles Bild), b < 0 dahinter (virtuell)"))).ToArray();
        }

        /// <summary>Checks of a curved mirror: virtual image, object in the focal plane (concave).</summary>
        public static IReadOnlyList<ValueCheck> MirrorChecks(bool concave) => new[]
        {
            new ValueCheck("optics.mirror.virtual", CheckSeverity.Info,
                T("Image distance b < 0: the image is virtual (behind the mirror); it cannot be caught on a screen.",
                  "Bildweite b < 0: das Bild ist virtuell (hinter dem Spiegel); es lässt sich nicht auf einem Schirm auffangen."),
                "b < 0", c => c["b"] is double b && b < 0),
            new ValueCheck("optics.mirror.focalplane", CheckSeverity.Info,
                T("The object lies in the focal plane (g = f): the light leaves parallel, the image lies at infinity.",
                  "Der Gegenstand liegt in der Brennebene (g = f): das Licht verlässt den Spiegel parallel, das Bild liegt im Unendlichen."),
                "g = f", c => concave && c["g"] is double g && c["f"] is double f && Math.Abs(g - f) <= 1e-6 * Math.Max(1e-6, f)),
        };

        // ------------------------------------------------------------------ prism

        /// <summary>Fraunhofer F (486.1 nm) and C (656.3 nm) lines; n(λ) of the tracer's two-term Cauchy fit (<see cref="Spectrum.Index"/>).</summary>
        private const double LineF = 486.1, LineC = 656.3;

        public static readonly IReadOnlyList<VarDef> PrismVariables = new[]
        {
            new VarDef("A", T("Apex angle (drawn, at the top corner)", "Brechender Winkel (gezeichnet, an der oberen Ecke)"), T("A", "γ"), Quantities.Angle)
                { Unit = "°", Min = 0, Max = Math.PI, ReadOnly = true },
            new VarDef("n", T("Refractive index (d line)", "Brechzahl (d-Linie)"), T("n", "n"), Quantities.Ratio) { Param = "n", Default = 1.5, Min = 1.0, Max = 3 },
            new VarDef("dmin", T("Minimum deviation", "Minimale Ablenkung"), T("δ_min", "δ_min"), Quantities.Angle) { Unit = "°", Min = 0, Max = Math.PI },
            new VarDef("dn", T("Dispersion n(F) − n(C)", "Dispersion n(F) − n(C)"), T("n_F − n_C", "n_F − n_C"), Quantities.Ratio)
                { Param = "dn", Default = 0.03, Min = 0, Max = 0.3, Relevance = VarRelevance.Panel },
            new VarDef("nF", T("Refractive index at the F line (486.1 nm)", "Brechzahl bei der F-Linie (486,1 nm)"), T("n_F", "n_F"), Quantities.Ratio)
                { Min = 1.0, Max = 3.5, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("nC", T("Refractive index at the C line (656.3 nm)", "Brechzahl bei der C-Linie (656,3 nm)"), T("n_C", "n_C"), Quantities.Ratio)
                { Min = 1.0, Max = 3.5, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("vd", T("Abbe number", "Abbe-Zahl"), T("ν_d", "ν_d"), Quantities.Ratio) { Min = 1, Max = 1e4, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("dFC", T("Angular spread F − C (incidence of minimum deviation)", "Winkelaufspreizung F − C (Einfall der minimalen Ablenkung)"), T("Δδ_FC", "Δδ_FC"), Quantities.Angle)
                { Unit = "°", Min = -Math.PI, Max = Math.PI, Relevance = VarRelevance.Panel, ReadOnly = true },
        };

        /// <summary>Minimum deviation δ_min = 2·asin(n·sin(A/2)) − A; NaN if no ray passes symmetrically (n·sin(A/2) ≥ 1).</summary>
        public static double MinimumDeviation(double n, double apex)
        {
            var s = n * Math.Sin(apex / 2);
            return apex <= 0 || s >= 1 ? double.NaN : 2 * Math.Asin(s) - apex;
        }

        /// <summary>
        /// Deviation of a ray entering a prism (apex angle <paramref name="apex"/>, index <paramref name="n"/>, in air) at the
        /// angle of incidence <paramref name="incidence"/>: δ = θ₁ + θ₄ − A; NaN on total internal reflection at the second face.
        /// </summary>
        public static double Deviation(double n, double apex, double incidence)
        {
            var inside = Math.Asin(Math.Sin(incidence) / n);
            var s4 = n * Math.Sin(apex - inside);
            return Math.Abs(s4) >= 1 ? double.NaN : incidence + Math.Asin(s4) - apex;
        }

        public static readonly IReadOnlyList<Relation> PrismRelations = new[]
        {
            new Relation("optics.prism.minimum", T("Minimum deviation", "Minimale Ablenkung"), "{n} = sin(({A} + {dmin})/2)/sin({A}/2)", "n", "A", "dmin")
                {
                    Source = "S41",
                    Validity = T("prism in air, symmetric passage (the ray inside parallel to the base), one wavelength",
                        "Prisma in Luft, symmetrischer Durchgang (der Strahl innen parallel zur Basis), eine Wellenlänge"),
                }
                .Solve("dmin", "2·asin({n}·sin({A}/2)) − {A}", v => MinimumDeviation(v("n"), v("A")))
                .Solve("n", "sin(({A} + {dmin})/2)/sin({A}/2)", v => v("A") > 0 ? Math.Sin((v("A") + v("dmin")) / 2) / Math.Sin(v("A") / 2) : double.NaN),
        };

        /// <summary>
        /// The drawn apex angle (marking) and, with dispersion on, n at the F and C lines (Cauchy fit of the tracer), the Abbe
        /// number ν_d = (n_d − 1)/(n_F − n_C) and the angular spread of the F and C lines at the incidence of minimum deviation.
        /// </summary>
        public static IEnumerable<BoundRelation> PrismLinks(SketchObject o)
        {
            var apex = PrismType.ApexAngle(o);
            if (apex > 0)
                yield return BoundRelation.Local(new Relation("optics.prism.apex", T("Drawn apex angle", "Gezeichneter brechender Winkel"), "{A} = drawn", "A")
                    .Solve("A", "drawn", _ => apex), o.Id);
            if (!o.Flag("dispersion", false)) yield break;
            var cauchy = T("two-term Cauchy fit through n_d and n_F − n_C (as traced)", "zweigliedrige Cauchy-Näherung durch n_d und n_F − n_C (wie verfolgt)");
            Relation Line(string key, string name, double line, I18nText title) =>
                new Relation("optics.prism." + key, title, "{" + key + "} = n({n}, {dn}, " + name + ")", key, "n", "dn") { Validity = cauchy }
                    .Solve(key, "n({n}, {dn}, " + name + ")", v => Spectrum.Index(v("n"), Math.Max(1e-12, v("dn")), line));
            yield return BoundRelation.Local(Line("nF", "λ_F", LineF, T("Refractive index at the F line", "Brechzahl bei der F-Linie")), o.Id);
            yield return BoundRelation.Local(Line("nC", "λ_C", LineC, T("Refractive index at the C line", "Brechzahl bei der C-Linie")), o.Id);
            yield return BoundRelation.Local(new Relation("optics.prism.abbe", T("Abbe number", "Abbe-Zahl"), "{vd} = ({n} − 1)/{dn}", "vd", "n", "dn") { Source = "S41" }
                .Solve("vd", "({n} − 1)/{dn}", v => v("dn") > 0 ? (v("n") - 1) / v("dn") : double.NaN), o.Id);
            yield return BoundRelation.Local(new Relation("optics.prism.spread", T("Angular spread of the F and C lines", "Winkelaufspreizung der F- und C-Linie"),
                    "{dFC} = δ(n_F) − δ(n_C) at θ₁ = asin({n}·sin({A}/2))", "dFC", "n", "dn", "A")
                {
                    Source = "S41",
                    Validity = T("prism in air; incidence of minimum deviation for the d line, n_F and n_C from the Cauchy fit",
                        "Prisma in Luft; Einfall der minimalen Ablenkung für die d-Linie, n_F und n_C aus der Cauchy-Näherung"),
                }
                .Solve("dFC", "δ(n_F) − δ(n_C)", v =>
                {
                    double n = v("n"), a = v("A"), dn = Math.Max(1e-12, v("dn"));
                    var s = n * Math.Sin(a / 2);
                    if (a <= 0 || s >= 1) return double.NaN;
                    var incidence = Math.Asin(s);
                    return Deviation(Spectrum.Index(n, dn, LineF), a, incidence) - Deviation(Spectrum.Index(n, dn, LineC), a, incidence);
                }), o.Id);
        }

        public static readonly IReadOnlyList<ValueCheck> PrismChecks = new[]
        {
            new ValueCheck("optics.prism.nominimum", CheckSeverity.Info,
                T("No ray passes the prism symmetrically (n·sin(A/2) ≥ 1): the light is totally reflected at the second face, there is no minimum deviation.",
                  "Kein Strahl durchläuft das Prisma symmetrisch (n·sin(γ/2) ≥ 1): das Licht wird an der zweiten Fläche totalreflektiert, es gibt keine minimale Ablenkung."),
                "n·sin(A/2) ≥ 1", c => c["n"] is double n && c["A"] is double a && n * Math.Sin(a / 2) >= 1),
        };

        // ------------------------------------------------------------------ diffraction grating

        /// <summary>The reference wavelength of the grating's angle parameter (first-order angle at 550 nm).</summary>
        public const double GratingReferenceNm = 550;

        public static readonly ParamDef LinesParam = new("lines", T("Lines per mm (empty = from the first-order angle)", "Striche pro mm (leer = aus dem Winkel 1. Ordnung)"), ParamKind.Text)
            { Advanced = true };

        public static readonly ParamDef GratingLambdaParam = Text("lambda", T("Wavelength for the values (empty = from the light reaching the grating)", "Wellenlänge für die Werte (leer = aus dem Licht am Gitter)"), "nm");

        /// <summary>Line density (kind <see cref="Quantities.LineDensity"/>, shown in lines/mm; entered in <c>lines</c>, it sets the drawn orders).</summary>
        public static readonly VarDef Lines = new("N", T("Line density", "Strichdichte"), T("N", "N"), Quantities.LineDensity)
        {
            Unit = "lines/mm", Param = "lines", Min = 1e3, Max = 1e7,
        };

        public static readonly VarDef GratingLambda = new("lambda", T("Wavelength", "Wellenlänge"), T("λ", "λ"), Quantities.Length)
        {
            Unit = "nm", Param = "lambda", Default = GratingReferenceNm * 1e-9, Min = 1e-8, Max = 1e-4,
        };

        public static readonly IReadOnlyList<VarDef> GratingVariables = new[]
        {
            Lines,
            new VarDef("d", T("Grating period", "Gitterkonstante"), T("d", "d"), Quantities.Length) { Unit = "µm", Min = 1e-8, Max = 1e-2, ReadOnly = true },
            GratingLambda,
            Order(1, VarRelevance.Card),
            Order(2, VarRelevance.Panel),
            Order(3, VarRelevance.Panel),
            new VarDef("s550", T("First-order angle at 550 nm (drawn)", "Winkel 1. Ordnung bei 550 nm (gezeichnet)"), T("θ₁(550 nm)", "θ₁(550 nm)"), Quantities.Angle)
                { Unit = "°", Min = 0, Max = Math.PI / 2, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("mmax", T("Highest order (normal incidence)", "Höchste Ordnung (senkrechter Einfall)"), T("m_max", "m_max"), Quantities.Count)
                { Min = 0, Max = 1e5, Relevance = VarRelevance.Panel, ReadOnly = true },
        };

        private static VarDef Order(int m, VarRelevance relevance)
        {
            var k = m.ToString(CultureInfo.InvariantCulture);
            return new VarDef("theta" + k, T("Angle of order " + k + " (normal incidence)", "Winkel der " + k + ". Ordnung (senkrechter Einfall)"), T("θ" + Sub(m), "θ" + Sub(m)), Quantities.Angle)
                { Unit = "°", Min = 0, Max = Math.PI / 2, Relevance = relevance, ReadOnly = true };
        }

        private static string Sub(int m) => m switch { 1 => "₁", 2 => "₂", _ => "₃" };

        /// <summary>The line density entered on a grating (lines per mm), or null when none (or an invalid one) is entered.</summary>
        public static double? EnteredLines(SketchObject o)
        {
            var (value, _, _, error) = ValueModel.ReadEntered(o, Lines);
            // SI value in lines per metre; the drawing works in lines per mm.
            return error is null && value is double n && n > 0 && n <= 1e8 ? n / 1e3 : null;
        }

        public static readonly IReadOnlyList<Relation> GratingRelations = new[]
        {
            new Relation("optics.grating.period", T("Grating period", "Gitterkonstante"), "{d} = 1/{N}", "d", "N") { Source = "S41" }
                .Solve("d", "1/{N}", v => v("N") > 0 ? 1 / v("N") : double.NaN)
                .Solve("N", "1/{d}", v => v("d") > 0 ? 1 / v("d") : double.NaN),
            new Relation("optics.grating.reference", T("First order at 550 nm", "Erste Ordnung bei 550 nm"), "{d}·sin {s550} = 550 nm", "d", "s550")
                { Source = "S41", Validity = T("normal incidence", "senkrechter Einfall") }
                .Solve("d", "550 nm/sin {s550}", v => Math.Sin(v("s550")) > 1e-9 ? GratingReferenceNm * 1e-9 / Math.Sin(v("s550")) : double.NaN)
                .Solve("s550", "asin(550 nm/{d})", v => v("d") > 0 && GratingReferenceNm * 1e-9 / v("d") < 1 ? Math.Asin(GratingReferenceNm * 1e-9 / v("d")) : double.NaN),
            new Relation("optics.grating.maxorder", T("Highest order", "Höchste Ordnung"), "{mmax} = ⌈{d}/{lambda}⌉ − 1", "mmax", "d", "lambda")
                { Source = "S41", Validity = T("normal incidence: |sin θ_m| = m·λ/d < 1", "senkrechter Einfall: |sin θ_m| = m·λ/d < 1") }
                .Solve("mmax", "⌈{d}/{lambda}⌉ − 1", v => v("lambda") > 0 && v("d") > 0 ? Math.Ceiling(v("d") / v("lambda") - 1e-9) - 1 : double.NaN),
        };

        /// <summary>
        /// The values of a grating from the drawing and the trace: the drawn first-order angle at 550 nm (marking, when no line
        /// density is entered), the wavelength of the traced light reaching the grating (marking, when it is one wavelength and
        /// none is entered) and the grating equation d·sin θ_m = m·λ for every order drawn (m = 1 … <c>orders</c>).
        /// </summary>
        public static IEnumerable<BoundRelation> GratingLinks(SketchObject o, RenderContext ctx)
        {
            if (EnteredLines(o) is null)
            {
                var s = Math.Asin(Math.Min(1, GratingType.Sine(o)));
                yield return BoundRelation.Local(new Relation("optics.grating.drawn", T("Drawn first-order angle at 550 nm", "Gezeichneter Winkel 1. Ordnung bei 550 nm"), "{s550} = angle", "s550")
                    .Solve("s550", "angle", _ => s), o.Id);
            }
            var (entered, _, _, error) = ValueModel.ReadEntered(o, GratingLambda);
            if ((entered is null || error is not null) && TracedWavelength(o, ctx.Doc) is double nm)
                yield return BoundRelation.Local(new Relation("optics.grating.light", T("Wavelength of the light at the grating", "Wellenlänge des Lichts am Gitter"), "{lambda} = λ(traced light)", "lambda")
                    { Validity = T("from the traced rays reaching the grating (one wavelength); enter a wavelength to use another one", "aus den verfolgten Strahlen am Gitter (eine Wellenlänge); eine eingegebene Wellenlänge gilt stattdessen") }
                    .Solve("lambda", "λ(traced light)", _ => nm * 1e-9), o.Id);
            var orders = Ox.ClampInt(o.Int("orders", 1), 1, 3);
            for (var m = 1; m <= orders; m++)
            {
                var k = m.ToString(CultureInfo.InvariantCulture);
                var mm = m;
                yield return BoundRelation.Local(new Relation("optics.grating.order" + k, T("Grating equation, order " + k, "Gittergleichung, " + k + ". Ordnung"),
                        "{d}·sin {theta" + k + "} = " + k + "·{lambda}", "theta" + k, "d", "lambda")
                    {
                        Source = "S41",
                        Validity = T("normal incidence (the tracer also follows oblique light: sin θ_m = sin θ_i + m·λ/d)", "senkrechter Einfall (die Strahlverfolgung folgt auch schrägem Licht: sin θ_m = sin θ_i + m·λ/d)"),
                    }
                    .Solve("theta" + k, "asin(" + k + "·{lambda}/{d})", v =>
                    {
                        var x = mm * v("lambda") / v("d");
                        return v("d") > 0 && x >= 0 && x < 1 ? Math.Asin(x) : double.NaN;
                    }), o.Id);
            }
        }

        /// <summary>The wavelength (nm) of the traced light reaching the grating when it is a single one (not white light), else null.</summary>
        internal static double? TracedWavelength(SketchObject o, SketchDocument doc)
        {
            if (o.IsPointGeometry) return null;
            var m = o.Matrix();
            var hh = o.Size.H / 2;
            var all = Light.ArrivingWavelengths(doc, o.Id, m.Apply(new Vec2(0, -hh)), m.Apply(new Vec2(0, hh)));
            if (all.Count == 0 || all[0] <= 0) return null;
            return all.All(l => Math.Abs(l - all[0]) < 1e-9) ? all[0] : null;
        }

        public static readonly IReadOnlyList<ValueCheck> GratingChecks = new[]
        {
            new ValueCheck("optics.grating.beyond", CheckSeverity.Info,
                T("An order drawn does not exist at this wavelength (m·λ ≥ d, beyond 90°): it is not drawn.",
                  "Eine gezeichnete Ordnung gibt es bei dieser Wellenlänge nicht (m·λ ≥ d, jenseits von 90°): sie wird nicht gezeichnet."),
                "m·λ/d ≥ 1 for an order m ≤ orders",
                c => c["lambda"] is double l && c["d"] is double d && d > 0 && Ox.ClampInt(c.Object.Int("orders", 1), 1, 3) * l / d >= RayTracer.MaxGratingSine),
        };

        // ------------------------------------------------------------------ object arrow

        public static readonly IReadOnlyList<VarDef> ArrowVariables = new[]
        {
            new VarDef("g", T("Object distance", "Gegenstandsweite"), T("g", "g"), Quantities.Length) { Unit = "mm", Min = 0, Max = 100, ReadOnly = true },
            new VarDef("b", T("Image distance", "Bildweite"), T("b", "b"), Quantities.Length) { Unit = "mm", Min = -100, Max = 100, ReadOnly = true },
            new VarDef("beta", T("Magnification", "Abbildungsmaßstab"), T("β", "β"), Quantities.Ratio) { Min = -1e4, Max = 1e4, ReadOnly = true },
            new VarDef("image", T("Image", "Bild"), T("Image", "Bild"), ImageKind),
            new VarDef("f", T("Focal length of the imaging element", "Brennweite des abbildenden Elements"), T("f", "f"), Quantities.Length)
                { Unit = "mm", Min = -100, Max = 100, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("y", T("Object size", "Gegenstandsgröße"), T("G", "G"), Quantities.Length) { Unit = "mm", Min = 0, Max = 100, Relevance = VarRelevance.Panel, ReadOnly = true },
            new VarDef("yi", T("Image size (negative = inverted)", "Bildgröße (negativ = umgekehrt)"), T("B", "B"), Quantities.Length)
                { Unit = "mm", Min = -1e4, Max = 1e4, Relevance = VarRelevance.Panel, ReadOnly = true },
        };

        public static readonly IReadOnlyList<Relation> ArrowRelations = new[]
        {
            new Relation("optics.image.distance", T("Thin lens equation", "Abbildungsgleichung"), "1/{f} = 1/{g} + 1/{b}", "b", "f", "g")
                { Source = "S41", Validity = T("ideal thin lens or curved mirror, paraxial; b > 0 real image", "ideale dünne Linse oder gewölbter Spiegel, paraxial; b > 0 reelles Bild") }
                .Solve("b", "{g}·{f}/({g} − {f})", v => Math.Abs(v("g") - v("f")) < 1e-12 ? double.NaN : v("g") * v("f") / (v("g") - v("f"))),
            new Relation("optics.image.magnification", T("Magnification", "Abbildungsmaßstab"), "{beta} = −{b}/{g}", "beta", "b", "g") { Source = "S41" }
                .Solve("beta", "−{b}/{g}", v => v("g") > 0 ? -v("b") / v("g") : double.NaN),
            new Relation("optics.image.size", T("Image size", "Bildgröße"), "{yi} = {beta}·{y}", "yi", "beta", "y") { Source = "S41" }
                .Solve("yi", "{beta}·{y}", v => v("beta") * v("y")),
            new Relation("optics.image.kind", T("Real or virtual image", "Reelles oder virtuelles Bild"), "{image} = real if {b} > 0, else virtual", "image", "b")
                .Solve("image", "real if {b} > 0", v => v("b") > 0 ? 0 : 1),
        };

        /// <summary>
        /// The drawn geometry of an object arrow as markings: object distance g to the lens or curved mirror it is imaged by,
        /// that element's focal length and the arrow size; nothing when no element images it.
        /// </summary>
        public static IEnumerable<BoundRelation> ArrowLinks(SketchObject o, RenderContext ctx)
        {
            if (Imaging.Find(o, ctx) is not { } frame) yield break;
            var foot = o.Matrix().Apply(new Vec2(0, o.Size.H / 2));
            var g = -frame.Frame(foot).X / 1000;
            if (g <= 0) yield break;
            var f = frame.F / 1000;
            var y = o.Size.H / 1000;
            Relation Marking(string id, I18nText name, string key, string what, double value)
                => new Relation(id, name, "{" + key + "} = " + what, key).Solve(key, what, _ => value);
            yield return BoundRelation.Local(Marking("optics.image.g", T("Drawn object distance", "Gezeichnete Gegenstandsweite"), "g", "drawn", g), o.Id);
            yield return BoundRelation.Local(Marking("optics.image.f", T("Focal length of the imaging element", "Brennweite des abbildenden Elements"), "f", "f(element)", f), o.Id);
            yield return BoundRelation.Local(Marking("optics.image.y", T("Drawn object size", "Gezeichnete Gegenstandsgröße"), "y", "drawn", y), o.Id);
        }

        public static readonly IReadOnlyList<ValueCheck> ArrowChecks = new[]
        {
            new ValueCheck("optics.image.virtual", CheckSeverity.Info,
                T("The image is virtual: it lies on the object side (behind a mirror) and cannot be caught on a screen.",
                  "Das Bild ist virtuell: es liegt auf der Gegenstandsseite (hinter einem Spiegel) und lässt sich nicht auf einem Schirm auffangen."),
                "b < 0", c => c["b"] is double b && b < 0),
            new ValueCheck("optics.image.infinity", CheckSeverity.Info,
                T("The object lies in the focal plane: the image lies at infinity.", "Der Gegenstand liegt in der Brennebene: das Bild liegt im Unendlichen."),
                "g = f", c => c["g"] is double g && c["f"] is double f && Math.Abs(g - f) <= 1e-6 * Math.Max(1e-6, Math.Abs(f))),
        };
    }
}
