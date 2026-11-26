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
    }

    /// <summary>
    /// Element values of the optics module (engineering-calculations conventions: SI internally – lengths in m, powers in W,
    /// angles in rad; focal lengths are in the drawing scale, so 40 mm on the page is f = 0.04 m and D = 25 dpt):
    /// light sources (power P, wavelength λ, photon energy), detectors and screens (received power, share of the emitted
    /// light, degree and angle of polarisation – results of the trace), thin lenses (f, refractive power D = 1/f, the thin
    /// lens equation 1/f = 1/g + 1/b with magnification β = −b/g, and with a second lens on the axis the system focal length
    /// 1/f = 1/f₁ + 1/f₂ − d/(f₁·f₂)), the thick lens (f from the lensmaker's equation) and the object arrow (g, b, β of the
    /// drawn image).
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

        public static readonly IReadOnlyList<VarDef> DetectorVariables = new[]
        {
            new VarDef("Prx", T("Received power", "Empfangene Leistung"), T("P_e", "P_E"), Quantities.Power) { Unit = "mW", Min = 0, Max = 1e3 },
            new VarDef("eta", T("Share of the emitted light", "Anteil des ausgesandten Lichts"), T("η", "η"), Quantities.Ratio) { Unit = "%", Min = 0, Max = 1 },
            new VarDef("V", T("Degree of polarisation", "Polarisationsgrad"), T("V", "V"), Quantities.Ratio) { Unit = "%", Min = 0, Max = 1 },
            new VarDef("theta", T("Polarisation angle (0° = in the drawing plane)", "Polarisationswinkel (0° = in der Zeichenebene)"), T("θ", "θ"), Quantities.Angle)
                { Unit = "°", Min = -Math.PI / 2, Max = Math.PI / 2 },
            new VarDef("pol", T("Polarisation", "Polarisation"), T("Pol.", "Pol."), PolarisationKind) { Relevance = VarRelevance.Panel },
        };

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
        /// arriving (incoherent sum of the coherency matrices, weighted like the share).
        /// </summary>
        public static IEnumerable<BoundRelation> DetectorLinks(SketchObject o, RenderContext ctx)
        {
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
            new VarDef("D", T("Refractive power in dioptres (1/m)", "Brechkraft in Dioptrien (1/m)"), T("D", "D"), Quantities.Ratio) { Min = -1000, Max = 1000 },
            new VarDef("g", T("Object distance", "Gegenstandsweite"), T("g", "g"), Quantities.Length) { Unit = "mm", Param = "g", Min = -100, Max = 100 },
            new VarDef("b", T("Image distance", "Bildweite"), T("b", "b"), Quantities.Length) { Unit = "mm", Param = "b", Min = -100, Max = 100 },
            new VarDef("beta", T("Magnification", "Abbildungsmaßstab"), T("β", "β"), Quantities.Ratio) { Relevance = VarRelevance.Panel, Min = -1e4, Max = 1e4 },
            new VarDef("fs", T("System focal length with the next lens", "Systembrennweite mit der nächsten Linse"), T("f_s", "f_s"), Quantities.Length)
                { Unit = "mm", Relevance = VarRelevance.Panel, Min = -100, Max = 100 },
            new VarDef("d", T("Distance to the next lens", "Abstand zur nächsten Linse"), T("d", "d"), Quantities.Length)
                { Unit = "mm", Relevance = VarRelevance.Panel, Min = 0, Max = 100 },
            new VarDef("sF", T("Back focal distance of the system (behind the next lens)", "Bildseitige Schnittweite des Systems (hinter der nächsten Linse)"),
                T("s′_F", "s′_F"), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Panel, Min = -100, Max = 100 },
        };

        /// <summary>D = ±1/f, the thin lens equation and the magnification, with the sign of the lens type.</summary>
        public static IReadOnlyList<Relation> LensRelations(bool converging)
        {
            var s = converging ? 1.0 : -1.0;
            var sf = converging ? "{f}" : "(−{f})";
            double Div(double a, double b) => Math.Abs(b) < 1e-15 ? double.NaN : a / b;
            double Positive(double v) => v > 0 ? v : double.NaN;
            return new[]
            {
                new Relation("optics.lens.power", T("Refractive power", "Brechkraft"), "{D} = 1/" + sf, "D", "f")
                    .Solve("D", "1/" + sf, v => Div(1, s * v("f")))
                    .Solve("f", converging ? "1/{D}" : "−1/{D}", v => Positive(Div(s, v("D")))),
                new Relation("optics.lens.imaging", T("Thin lens equation", "Linsengleichung"), "1/" + sf + " = 1/{g} + 1/{b}", "f", "g", "b")
                    { Validity = T("ideal thin lens, paraxial; g > 0 in front of the lens, b > 0 behind it (real image)", "ideale dünne Linse, paraxial; g > 0 vor der Linse, b > 0 dahinter (reelles Bild)") }
                    .Solve("b", "{g}·" + sf + "/({g} − " + sf + ")", v => Div(v("g") * s * v("f"), v("g") - s * v("f")))
                    .Solve("g", "{b}·" + sf + "/({b} − " + sf + ")", v => Div(v("b") * s * v("f"), v("b") - s * v("f")))
                    .Solve("f", (converging ? "" : "−") + "{g}·{b}/({g} + {b})", v => Positive(s * Div(v("g") * v("b"), v("g") + v("b")))),
                new Relation("optics.lens.magnification", T("Magnification", "Abbildungsmaßstab"), "{beta} = −{b}/{g}", "beta", "b", "g")
                    .Solve("beta", "−{b}/{g}", v => -Div(v("b"), v("g")))
                    .Solve("b", "−{beta}·{g}", v => -v("beta") * v("g"))
                    .Solve("g", "−{b}/{beta}", v => -Div(v("b"), v("beta"))),
            };
        }

        /// <summary>
        /// The next thin lens on the axis of <paramref name="o"/> (its centre ahead of the lens on its local +x axis, within
        /// half its larger box side of the axis, the axes parallel) and the distance between the two centres (mm).
        /// </summary>
        public static (SketchObject Lens, LensType Type, double Distance)? NextLens(SketchObject o, SketchDocument doc)
        {
            if (o.IsPointGeometry) return null;
            var page = doc.PageOf(o.Id);
            if (page is null) return null;
            var m = o.Matrix();
            var c = m.Apply(Vec2.Zero);
            var axis = m.ApplyLinear(new Vec2(1, 0)).Normalized();
            var hidden = new HashSet<string>(doc.Layers.Where(l => !l.Visible).Select(l => l.Id), StringComparer.Ordinal);
            (SketchObject, LensType, double)? best = null;
            foreach (var x in doc.ObjectsOn(page))
            {
                if (x.Id == o.Id || x.IsPointGeometry || x.Layer is not null && hidden.Contains(x.Layer)) continue;
                if (OpticsModule.TypeOf(x.Type) is not LensType lt) continue;
                var mx = x.Matrix();
                var v = mx.Apply(Vec2.Zero) - c;
                var along = v.Dot(axis);
                if (along <= 1e-6 || Math.Abs(v.Cross(axis)) > 0.5 * Math.Max(x.Size.W, x.Size.H)) continue;
                if (Math.Abs(mx.ApplyLinear(new Vec2(1, 0)).Normalized().Dot(axis)) < 0.99) continue;
                if (best is null || along < best.Value.Item3) best = (x, lt, along);
            }
            return best;
        }

        /// <summary>
        /// With a second thin lens ahead on the axis: the drawn distance d (marking), the system focal length
        /// 1/f_s = 1/f₁ + 1/f₂ − d/(f₁·f₂) (Gullstrand) and the back focal distance s′_F = f₂·(f₁ − d)/(f₁ + f₂ − d) behind the
        /// second lens; f₁, f₂ signed by the lens types.
        /// </summary>
        public static IEnumerable<BoundRelation> LensLinks(SketchObject o, RenderContext ctx, bool converging)
        {
            if (NextLens(o, ctx.Doc) is not { } next) yield break;
            var dm = next.Distance / 1000;
            var distance = new Relation("optics.lens.distance", T("Drawn distance to the next lens", "Gezeichneter Abstand zur nächsten Linse"), "{d} = |O₁O₂|", "d")
                .Solve("d", "|O₁O₂|", _ => dm);
            yield return BoundRelation.Local(distance, o.Id);
            double s1 = converging ? 1 : -1, s2 = next.Type.Converging ? 1 : -1;
            string F(string key, double sign) => sign > 0 ? "{" + key + "}" : "(−{" + key + "})";
            string f1 = F("f1", s1), f2 = F("f2", s2);
            var map = new Dictionary<string, VarRef>
            {
                ["fs"] = new(o.Id, "fs"), ["sF"] = new(o.Id, "sF"), ["f1"] = new(o.Id, "f"), ["f2"] = new(next.Lens.Id, "f"), ["d"] = new(o.Id, "d"),
            };
            var validity = T("two thin lenses in air on one axis (Gullstrand)", "zwei dünne Linsen in Luft auf einer Achse (Gullstrand)");
            double Den(Func<string, double> v) => s1 * v("f1") + s2 * v("f2") - v("d");
            var system = new Relation("optics.lens.system", T("System focal length", "Systembrennweite"), "{fs} = " + f1 + "·" + f2 + "/(" + f1 + " + " + f2 + " − {d})", "fs", "f1", "f2", "d")
                { Validity = validity }
                .Solve("fs", f1 + "·" + f2 + "/(" + f1 + " + " + f2 + " − {d})", v => Math.Abs(Den(v)) < 1e-12 ? double.NaN : s1 * v("f1") * s2 * v("f2") / Den(v));
            var back = new Relation("optics.lens.backfocus", T("Back focal distance of the system", "Bildseitige Schnittweite des Systems"),
                    "{sF} = " + f2 + "·(" + f1 + " − {d})/(" + f1 + " + " + f2 + " − {d})", "sF", "f1", "f2", "d") { Validity = validity }
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
            new ValueCheck("optics.lens.afocal", CheckSeverity.Info,
                T("The two lenses form an afocal system (f₁ + f₂ = d, a telescope): parallel light leaves parallel, angular magnification −f₁/f₂.",
                  "Die beiden Linsen bilden ein afokales System (f₁ + f₂ = d, ein Fernrohr): paralleles Licht tritt parallel aus, Winkelvergrößerung −f₁/f₂."),
                "f₁ + f₂ = d", c => Afocal(c, converging)),
        };

        private static bool Afocal(ValueCheckContext c, bool converging)
        {
            if (c["d"] is not double d || c["f"] is not double f1) return false;
            if (NextLens(c.Object, c.Context.Doc) is not { } next || c.Table.Get(next.Lens.Id, "f")?.Value is not double f2) return false;
            var sum = (converging ? f1 : -f1) + (next.Type.Converging ? f2 : -f2);
            return Math.Abs(sum - d) <= 1e-6 * Math.Max(1e-6, Math.Abs(f1) + Math.Abs(f2));
        }

        // ------------------------------------------------------------------ thick lens

        public static readonly IReadOnlyList<VarDef> ThickLensVariables = new[]
        {
            new VarDef("f", T("Focal length (lensmaker's equation)", "Brennweite (Linsenschleiferformel)"), T("f", "f"), Quantities.Length) { Unit = "mm", Min = -100, Max = 100 },
            new VarDef("D", T("Refractive power in dioptres (1/m)", "Brechkraft in Dioptrien (1/m)"), T("D", "D"), Quantities.Ratio) { Min = -1000, Max = 1000 },
            new VarDef("n", T("Refractive index", "Brechzahl"), T("n", "n"), Quantities.Ratio) { Param = "n", Default = 1.5, Min = 1.01, Max = 3, Relevance = VarRelevance.Panel },
            new VarDef("bfd", T("Back focal distance (rear vertex to F′)", "Bildseitige Schnittweite (hinterer Scheitel bis F′)"), T("s′_F", "s′_F"), Quantities.Length)
                { Unit = "mm", Min = -100, Max = 100, Relevance = VarRelevance.Panel },
        };

        public static readonly IReadOnlyList<Relation> ThickLensRelations = new[]
        {
            new Relation("optics.thicklens.power", T("Refractive power", "Brechkraft"), "{D} = 1/{f}", "D", "f")
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
                    "1/{f} = ({n} − 1)·[1/R₁ − 1/R₂ + ({n} − 1)·d/({n}·R₁·R₂)]", "f", "n") { Validity = validity }
                .Solve("f", "1/(({n} − 1)·[1/R₁ − 1/R₂ + ({n} − 1)·d/({n}·R₁·R₂)])", v => Finite(ThickLens.FocalLength(v("n"), r1, r2, d)));
            var back = new Relation("optics.thicklens.bfd", T("Back focal distance", "Bildseitige Schnittweite"), "{bfd} = {f}·(1 − ({n} − 1)·d/({n}·R₁))", "bfd", "f", "n")
                    { Validity = validity }
                .Solve("bfd", "{f}·(1 − ({n} − 1)·d/({n}·R₁))", v => v("f") * (1 - (v("n") - 1) * d / (v("n") * r1)));
            yield return BoundRelation.Local(focal, o.Id);
            yield return BoundRelation.Local(back, o.Id);
        }

        // ------------------------------------------------------------------ object arrow

        public static readonly IReadOnlyList<VarDef> ArrowVariables = new[]
        {
            new VarDef("g", T("Object distance", "Gegenstandsweite"), T("g", "g"), Quantities.Length) { Unit = "mm", Min = 0, Max = 100 },
            new VarDef("b", T("Image distance", "Bildweite"), T("b", "b"), Quantities.Length) { Unit = "mm", Min = -100, Max = 100 },
            new VarDef("beta", T("Magnification", "Abbildungsmaßstab"), T("β", "β"), Quantities.Ratio) { Min = -1e4, Max = 1e4 },
            new VarDef("image", T("Image", "Bild"), T("Image", "Bild"), ImageKind),
            new VarDef("f", T("Focal length of the imaging element", "Brennweite des abbildenden Elements"), T("f", "f"), Quantities.Length)
                { Unit = "mm", Min = -100, Max = 100, Relevance = VarRelevance.Panel },
            new VarDef("y", T("Object size", "Gegenstandsgröße"), T("G", "G"), Quantities.Length) { Unit = "mm", Min = 0, Max = 100, Relevance = VarRelevance.Panel },
            new VarDef("yi", T("Image size (negative = inverted)", "Bildgröße (negativ = umgekehrt)"), T("B", "B"), Quantities.Length)
                { Unit = "mm", Min = -1e4, Max = 1e4, Relevance = VarRelevance.Panel },
        };

        public static readonly IReadOnlyList<Relation> ArrowRelations = new[]
        {
            new Relation("optics.image.distance", T("Thin lens equation", "Abbildungsgleichung"), "1/{f} = 1/{g} + 1/{b}", "b", "f", "g")
                { Validity = T("ideal thin lens or curved mirror, paraxial; b > 0 real image", "ideale dünne Linse oder gewölbter Spiegel, paraxial; b > 0 reelles Bild") }
                .Solve("b", "{g}·{f}/({g} − {f})", v => Math.Abs(v("g") - v("f")) < 1e-12 ? double.NaN : v("g") * v("f") / (v("g") - v("f"))),
            new Relation("optics.image.magnification", T("Magnification", "Abbildungsmaßstab"), "{beta} = −{b}/{g}", "beta", "b", "g")
                .Solve("beta", "−{b}/{g}", v => v("g") > 0 ? -v("b") / v("g") : double.NaN),
            new Relation("optics.image.size", T("Image size", "Bildgröße"), "{yi} = {beta}·{y}", "yi", "beta", "y")
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
