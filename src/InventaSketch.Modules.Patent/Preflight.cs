using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;

namespace InventaSketch.Modules.Patent
{
    /// <summary>One preflight result. <see cref="PageId"/> is null for document-level findings.</summary>
    public sealed record Finding(string RuleId, Severity Severity, string? ObjectId, string? PageId, I18nText Message, I18nText? Suggestion, string Source);

    public sealed record PreflightOptions
    {
        /// <summary>Include "Manual" review reminders for requirements that cannot be checked automatically.</summary>
        public bool IncludeManual { get; init; } = true;
        /// <summary>Maximum words per free text (flowchart boxes use <see cref="MaxWordsFlowchart"/>).</summary>
        public int MaxWords { get; init; } = 4;
        public int MaxWordsFlowchart { get; init; } = 12;
    }

    /// <summary>
    /// Compatibility check (preflight) against the drawing rules of an office. Renders every page (helper layers, helper lines and the sheet frame excluded) and
    /// checks it against a <see cref="PatentProfile"/>. A clean result is a drafting aid, never a legal certification.
    /// </summary>
    public static class Preflight
    {
        public static readonly I18nText Disclaimer = new(
            "Preflight is a drafting aid based on the cited office rules (research status 2026-11-20). A result without findings does not certify that the drawings comply with the requirements of any office and is not legal advice. Check the current official rules or consult an IP attorney before filing.",
            "Der Preflight ist eine Zeichenhilfe auf Basis der zitierten Amtsvorschriften (Recherchestand 20.11.2026). Ein Ergebnis ohne Befunde bestätigt nicht, dass die Zeichnungen den Anforderungen eines Amts genügen, und ist keine Rechtsberatung. Prüfen Sie vor der Einreichung die aktuellen amtlichen Vorschriften oder fragen Sie eine Fachperson für gewerblichen Rechtsschutz.");

        private static readonly Regex ScalePattern = new(@"(?i)(\b1\s*:\s*\d+([.,]\d+)?\b|\b\d+\s*:\s*1\b|\bactual\s+size\b|\bnatural\s+size\b|\bscale\s*1\s*/\s*\d+|natürliche\s+größe|\bmaßstab\b|\bmassstab\b)", RegexOptions.Compiled);
        private static readonly Regex FigPattern = new(@"^\s*(FIG|Fig|Figure|Abb|Abbildung)\.?\s*(\d+)\s*([A-Za-z]?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private sealed class Ctx
        {
            public Ctx(Registry r, SketchDocument d, PatentProfile p, PreflightOptions o) { Reg = r; Doc = d; Profile = p; Options = o; }
            public Registry Reg { get; }
            public SketchDocument Doc { get; }
            public PatentProfile Profile { get; }
            public PreflightOptions Options { get; }
            public List<Finding> Findings { get; } = new();

            /// <summary>
            /// Adds a finding. Rules without a cited source for the profile's office do not apply to it; drafting aids
            /// (rules without any source) apply to every profile.
            /// </summary>
            public void Add(PreflightRule rule, Severity sev, string? objectId, string? pageId, I18nText message, I18nText? suggestion = null)
            {
                if (!rule.IsDraftingAid && rule.For(Profile.Office).Count == 0) return;
                Findings.Add(new Finding(rule.Id, sev, objectId, pageId, message, suggestion, rule.SourceText(Profile.Office)));
            }
        }

        private sealed record PageObject(SketchObject Obj, IReadOnlyList<Primitive> Prims, Rect? Bounds);

        /// <summary>Runs all checks. Findings are ordered by page, then by rule.</summary>
        public static IReadOnlyList<Finding> Run(Registry registry, SketchDocument doc, PatentProfile profile, PreflightOptions? options = null)
        {
            options ??= new PreflightOptions();
            // Render with the profile active, exactly as the export does.
            var d = doc with { Settings = doc.Settings with { Profile = profile.Id } };
            var c = new Ctx(registry, d, profile, options);
            var rctx = new RenderContext(d);
            var pages = new List<(Page Page, List<PageObject> Objects)>();
            foreach (var page in d.Pages)
            {
                var objs = registry.RenderPage(d, page, rctx, o => !PatentExport.IsExcluded(d, o))
                    .Select(r => { var prims = PatentExport.StripHelpers(r.Primitives); return new PageObject(r.Object, prims, Prim.Bounds(prims)); })
                    .Where(p => p.Prims.Count > 0)
                    .ToList();
                pages.Add((page, objs));
            }

            foreach (var (page, objs) in pages)
            {
                CheckPage(c, page);
                foreach (var po in objs) CheckObject(c, page, po);
                CheckLeadCrossings(c, page, objs);
                CheckNumeralTargets(c, page, objs);
                CheckSheetNumber(c, page, objs);
            }
            CheckFigures(c, pages);
            CheckReferences(c);
            CheckUnnumberedParts(c);
            if (options.IncludeManual) AddManual(c);
            return c.Findings;
        }

        // ------------------------------------------------------------------------------------------ page

        private static void CheckPage(Ctx c, Page page)
        {
            var s = page.Size;
            bool Match(Size2 a) => Math.Abs(a.W - s.W) < 1 && Math.Abs(a.H - s.H) < 1;
            bool MatchRot(Size2 a) => Math.Abs(a.W - s.H) < 1 && Math.Abs(a.H - s.W) < 1;
            var names = string.Join(", ", c.Profile.Sheets.Select(x => x.Name.En));
            if (!c.Profile.Sheets.Any(x => Match(x.Size) || MatchRot(x.Size)))
                c.Add(PreflightRules.PageSize, Severity.Error, null, page.Id,
                    new I18nText(FormattableString.Invariant($"Sheet size {s.W:0.#} × {s.H:0.#} mm is not accepted ({names})."),
                                 FormattableString.Invariant($"Blattgröße {s.W:0.#} × {s.H:0.#} mm ist nicht zulässig ({names}).")),
                    new I18nText("Use an accepted sheet size, e.g. A4 portrait (210 × 297 mm).", "Verwenden Sie eine zulässige Blattgröße, z. B. A4 hoch (210 × 297 mm)."));
            else if (s.W > s.H + 1)
                c.Add(PreflightRules.PageOrientation, c.Profile.Office is Office.Uspto or Office.Common ? Severity.Error : Severity.Warning, null, page.Id,
                    new I18nText("The sheet is landscape. Drawing sheets are upright; rotate the figure, not the sheet.", "Das Blatt ist quer. Zeichnungsblätter stehen hochkant; drehen Sie die Figur, nicht das Blatt."),
                    new I18nText("Switch the page to portrait and turn the figure sideways (top of the figure on the left).", "Seite auf Hochformat stellen und die Figur quer legen (Oberseite der Figur links)."));
        }

        // ------------------------------------------------------------------------------------------ objects

        private static void CheckObject(Ctx c, Page page, PageObject po)
        {
            var o = po.Obj;
            var usable = c.Profile.Usable(page.Size);
            var flat = Prim.Flatten(po.Prims).ToList();

            if (po.Bounds is Rect b && !usable.Inflate(0.05).Contains(b))
                c.Add(PreflightRules.Margins, Severity.Error, o.Id, page.Id,
                    new I18nText(FormattableString.Invariant($"Object lies outside the usable area (margins top {c.Profile.Margins.Top}, left {c.Profile.Margins.Left}, right {c.Profile.Margins.Right}, bottom {c.Profile.Margins.Bottom} mm)."),
                                 FormattableString.Invariant($"Objekt liegt außerhalb der Nutzfläche (Ränder oben {c.Profile.Margins.Top}, links {c.Profile.Margins.Left}, rechts {c.Profile.Margins.Right}, unten {c.Profile.Margins.Bottom} mm).")),
                    new I18nText("Move or scale the object into the usable area.", "Objekt in die Nutzfläche verschieben oder verkleinern."));

            // Frame around the sight.
            foreach (var p in flat)
            {
                if (p is TextPrim || p is LinePrim) continue;
                if (p is PathPrim path && !path.IsClosed) continue;
                var pb = Prim.Bounds(new[] { p });
                if (pb is Rect r && r.W >= usable.W * 0.9 && r.H >= usable.H * 0.9)
                {
                    c.Add(PreflightRules.Frame, Severity.Error, o.Id, page.Id,
                        new I18nText("A frame encloses the usable area. Frames around the sight are not permitted.", "Ein Rahmen umschließt die Nutzfläche. Rahmen um die Nutzfläche sind nicht zulässig."),
                        new I18nText("Remove the frame (use the sheet helper, which is not exported).", "Rahmen entfernen (die Blatt-Hilfslinie wird nicht exportiert)."));
                    break;
                }
            }

            // Colour, grey and transparency.
            var colourAllowed = c.Profile.Colour == ColourPolicy.AllowedWithConditions;
            string? colour = null, grey = null;
            foreach (var col in Colours(flat))
            {
                var k = Classify(col);
                if (k == 2) colour ??= col;
                else if (k == 1) grey ??= col;
            }
            if (colour is not null)
                c.Add(PreflightRules.Colour, colourAllowed ? Severity.Info : Severity.Error, o.Id, page.Id,
                    colourAllowed
                        ? new I18nText($"Colour {colour} is used. Accepted only for electronic filing (since 2025-10-01); avoid colour-dependent wording and expect B/W for PCT and paper.", $"Farbe {colour} wird verwendet. Nur bei elektronischer Einreichung zulässig (seit 01.10.2025); keine farbabhängigen Formulierungen, PCT und Papier bleiben schwarz-weiß.")
                        : new I18nText($"Colour {colour} is used. Drawings must be black and white.", $"Farbe {colour} wird verwendet. Zeichnungen müssen schwarz-weiß sein."),
                    new I18nText("Use black lines on white; the black-and-white export converts colours to black.", "Schwarze Linien auf Weiß verwenden; der Schwarzweiß-Export wandelt Farben in Schwarz um."));
            var transparent = flat.Any(p => StyleOf(p)?.Opacity is double op && op < 1);
            if (grey is not null || transparent)
                c.Add(PreflightRules.Greyscale, colourAllowed ? Severity.Info : Severity.Error, o.Id, page.Id,
                    new I18nText($"Grey tone {(grey ?? "")}{(transparent ? " / transparency" : "")} is used. Lines must be black, dense and dark.", $"Grauton {(grey ?? "")}{(transparent ? " / Transparenz" : "")} wird verwendet. Linien müssen schwarz, dicht und dunkel sein."),
                    new I18nText("Replace grey fills by hatching or stippling with thin black lines.", "Graue Flächen durch Schraffur oder Punktierung mit dünnen schwarzen Linien ersetzen."));

            // Solid black areas.
            foreach (var p in flat)
            {
                var st = StyleOf(p);
                if (st?.Fill is { Kind: FillKind.Solid, Color: string fc } && SvgRenderer.Luminance(fc) < 0.35 && Area(p) > c.Profile.MaxSolidBlackArea)
                {
                    c.Add(PreflightRules.SolidBlack, Severity.Warning, o.Id, page.Id,
                        new I18nText(FormattableString.Invariant($"Solid black area of {Area(p):0} mm². Solid black areas are not permitted (except bar graphs)."), FormattableString.Invariant($"Schwarze Vollfläche von {Area(p):0} mm². Schwarze Flächen sind nicht zulässig (außer Balkendiagramme).")),
                        new I18nText("Use hatching instead of a solid fill.", "Schraffur statt Vollfläche verwenden."));
                    break;
                }
            }

            // Text: height, enclosed numerals, scale text, word count.
            foreach (var t in flat.OfType<TextPrim>())
            {
                var cap = t.TextStyle.Size * c.Profile.CapHeightRatio;
                if (cap < c.Profile.MinCharHeight - 1e-6)
                {
                    var need = c.Profile.MinCharHeight / c.Profile.CapHeightRatio;
                    c.Add(PreflightRules.TextHeight, Severity.Error, o.Id, page.Id,
                        new I18nText(FormattableString.Invariant($"\"{t.Text}\": character height ≈{cap:0.0#} mm is below {c.Profile.MinCharHeight} mm."), FormattableString.Invariant($"\"{t.Text}\": Zeichenhöhe ≈{cap:0.0#} mm liegt unter {c.Profile.MinCharHeight} mm.")),
                        new I18nText(FormattableString.Invariant($"Set the text height to at least {need:0.0} mm (font size em)."), FormattableString.Invariant($"Schrifthöhe auf mindestens {need:0.0} mm (Schriftgrad) setzen.")));
                    break;
                }
            }
            if (o.Type == "ann.balloon")
                c.Add(PreflightRules.EnclosedNumeral, c.Profile.ForbidEnclosedNumerals ? Severity.Error : Severity.Info, o.Id, page.Id,
                    new I18nText("Reference sign in a circle (balloon).", "Bezugszeichen im Kreis (Positionsnummer)."),
                    new I18nText("Use a plain reference numeral with a lead line.", "Einfaches Bezugszeichen mit Bezugslinie verwenden."));
            foreach (var t in flat.OfType<TextPrim>())
                if (NumeralType.IsEnclosed(t.Text))
                {
                    c.Add(PreflightRules.EnclosedNumeral, c.Profile.ForbidEnclosedNumerals ? Severity.Error : Severity.Info, o.Id, page.Id,
                        new I18nText($"Reference sign \"{t.Text}\" is enclosed in brackets or quotes.", $"Bezugszeichen \"{t.Text}\" steht in Klammern oder Anführungszeichen."),
                        new I18nText($"Write {NumeralType.StripEnclosure(t.Text)} without enclosure.", $"{NumeralType.StripEnclosure(t.Text)} ohne Klammern schreiben."));
                    break;
                }
            foreach (var t in flat.OfType<TextPrim>())
                if (ScalePattern.IsMatch(t.Text))
                {
                    c.Add(PreflightRules.ScaleText, Severity.Error, o.Id, page.Id,
                        new I18nText($"Scale indication \"{t.Text}\". Scales are not permitted (only a graphical scale bar).", $"Maßstabsangabe \"{t.Text}\". Maßstabsangaben sind unzulässig (nur grafischer Maßstab)."),
                        new I18nText("Remove the scale text.", "Maßstabsangabe entfernen."));
                    break;
                }
            if (!IsNumberingText(o))
            {
                var limit = o.Type.StartsWith("ann.flow.", StringComparison.Ordinal) ? c.Options.MaxWordsFlowchart : c.Options.MaxWords;
                foreach (var t in flat.OfType<TextPrim>().Where(t => t.Tag is not (AnnTags.Numeral or AnnTags.DimText or AnnTags.FigLabel)))
                {
                    var words = t.Text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (words > limit)
                    {
                        c.Add(PreflightRules.TextWords, Severity.Warning, o.Id, page.Id,
                            new I18nText($"Text with {words} words. Drawings may only contain indispensable words.", $"Text mit {words} Wörtern. Zeichnungen dürfen nur unerlässliche Wörter enthalten."),
                            new I18nText("Move explanations to the description; keep short keywords only.", "Erläuterungen in die Beschreibung verschieben; nur kurze Stichworte belassen."));
                        break;
                    }
                }
            }

            // Line widths.
            foreach (var p in flat)
            {
                var st = StyleOf(p);
                if (st is null) continue;
                var w = st.HasStroke ? st.EffectiveWidth : double.MaxValue;
                if (st.Fill is { Kind: FillKind.Hatch, Hatch: HatchPattern h }) w = Math.Min(w, h.Width ?? Math.Max(0.18, st.EffectiveWidth * 0.5));
                if (w < c.Profile.MinLineWidth - 1e-9)
                {
                    c.Add(PreflightRules.LineWidth, Severity.Warning, o.Id, page.Id,
                        new I18nText(FormattableString.Invariant($"Line width {w:0.0##} mm is below {c.Profile.MinLineWidth} mm and may vanish when reduced to 2/3."), FormattableString.Invariant($"Linienbreite {w:0.0##} mm liegt unter {c.Profile.MinLineWidth} mm und kann bei Verkleinerung auf 2/3 verschwinden.")),
                        new I18nText("Use at least 0.25 mm for thin lines.", "Für dünne Linien mindestens 0,25 mm verwenden."));
                    break;
                }
            }

            // Centre lines.
            if (!c.Profile.CentreLinesAllowed && flat.Any(p => StyleOf(p)?.Role == LineRole.Center))
                c.Add(PreflightRules.CentreLines, Severity.Warning, o.Id, page.Id,
                    new I18nText("Centre line present. USPTO drawings must not contain centre lines.", "Mittellinie vorhanden. USPTO-Zeichnungen dürfen keine Mittellinien enthalten."),
                    new I18nText("Remove centre lines or use a profile that allows them (EPO, PCT).", "Mittellinien entfernen oder ein Profil verwenden, das sie erlaubt (EPA, PCT)."));
        }

        private static bool IsNumberingText(SketchObject o) => o.Type is PatentIds.PageNumber or "ann.figlabel" or "ann.flow.step";

        // ------------------------------------------------------------------------------------------ lead lines

        private static void CheckLeadCrossings(Ctx c, Page page, List<PageObject> objs)
        {
            var leads = objs.Select(po => (po.Obj, Lines: po.Prims.Where(p => p.Tag == AnnTags.Lead).SelectMany(p => Ann.Polylines(p)).ToList()))
                .Where(x => x.Lines.Count > 0).ToList();
            for (var i = 0; i < leads.Count; i++)
                for (var j = i + 1; j < leads.Count; j++)
                {
                    var x = Crossing(leads[i].Lines, leads[j].Lines);
                    if (x is null) continue;
                    var a = ReferenceNumerals.NumeralOf(leads[i].Obj) ?? leads[i].Obj.Id;
                    var b = ReferenceNumerals.NumeralOf(leads[j].Obj) ?? leads[j].Obj.Id;
                    c.Add(PreflightRules.LeadCrossing, c.Profile.LeadCrossingSeverity, leads[i].Obj.Id, page.Id,
                        new I18nText($"Lead lines of {a} and {b} cross each other.", $"Bezugslinien von {a} und {b} kreuzen sich."),
                        new I18nText("Move the numerals or bend one lead line so the lines do not cross.", "Bezugszeichen verschieben oder eine Bezugslinie biegen, damit sich die Linien nicht kreuzen."));
                }
        }

        private static Vec2? Crossing(List<List<Vec2>> a, List<List<Vec2>> b)
        {
            foreach (var pa in a)
                foreach (var pb in b)
                    for (var i = 0; i + 1 < pa.Count; i++)
                        for (var j = 0; j + 1 < pb.Count; j++)
                        {
                            var x = Geo.SegmentIntersection(pa[i], pa[i + 1], pb[j], pb[j + 1]);
                            if (x is null) continue;
                            // Touching at a shared end point (two leads on the same spot) is not a crossing.
                            var ends = new[] { pa[0], pa[pa.Count - 1], pb[0], pb[pb.Count - 1] };
                            if (ends.Any(e => e.DistanceTo(x.Value) < 0.05)) continue;
                            return x;
                        }
            return null;
        }

        // ------------------------------------------------------------------------------------------ sheet numbers

        private static void CheckSheetNumber(Ctx c, Page page, List<PageObject> objs)
        {
            if (c.Profile.SheetNumberSeverity is not Severity sev) return;
            var index = c.Doc.Pages.ToList().FindIndex(p => p.Id == page.Id);
            var expected = c.Profile.FormatSheetNumber(index + 1, c.Doc.Pages.Count);
            var numbers = objs.Where(po => po.Obj.Type == PatentIds.PageNumber).ToList();
            if (numbers.Count == 0)
            {
                c.Add(PreflightRules.SheetNumber, sev, null, page.Id,
                    new I18nText($"Sheet number \"{expected}\" is missing.", $"Blattnummer \"{expected}\" fehlt."),
                    new I18nText("Insert a sheet number at the top centre inside the usable area.", "Blattnummer oben mittig innerhalb der Nutzfläche einfügen."));
                return;
            }
            var pn = numbers[0];
            var text = PageNumberType.TextOf(pn.Obj, c.Doc);
            if (text != expected)
                c.Add(PreflightRules.SheetNumber, sev, pn.Obj.Id, page.Id,
                    new I18nText($"Sheet number \"{text}\" should be \"{expected}\".", $"Blattnummer \"{text}\" sollte \"{expected}\" lauten."),
                    new I18nText("Switch on automatic numbering.", "Automatische Nummerierung einschalten."));
            var usable = c.Profile.Usable(page.Size);
            if (pn.Bounds is Rect b && (Math.Abs(b.Center.X - usable.Center.X) > usable.W * 0.15 || b.Y > usable.Y + 25))
                c.Add(PreflightRules.SheetNumber, Severity.Warning, pn.Obj.Id, page.Id,
                    new I18nText("Sheet number is not at the top centre of the usable area.", "Blattnummer steht nicht oben mittig in der Nutzfläche."),
                    new I18nText("Place it centred directly below the top margin.", "Mittig direkt unter dem oberen Rand platzieren."));
        }

        // ------------------------------------------------------------------------------------------ figures

        private static void CheckFigures(Ctx c, List<(Page Page, List<PageObject> Objects)> pages)
        {
            var labels = new List<(PageObject Po, Page Page, string Text)>();
            var contentPages = new List<Page>();
            double numeralSize = 0;
            foreach (var (page, objs) in pages)
            {
                if (objs.Any(po => po.Obj.Type is not (PatentIds.PageNumber or "ann.figlabel"))) contentPages.Add(page);
                foreach (var po in objs.Where(po => po.Obj.Type == "ann.figlabel").OrderBy(po => po.Bounds?.Y ?? 0).ThenBy(po => po.Bounds?.X ?? 0))
                    labels.Add((po, page, FigLabelType.LabelOf(po.Obj)));
                foreach (var po in objs)
                    foreach (var t in Prim.Flatten(po.Prims).OfType<TextPrim>().Where(t => t.Tag == AnnTags.Numeral))
                        numeralSize = Math.Max(numeralSize, t.TextStyle.Size);
            }

            if (labels.Count == 1 && contentPages.Count <= 1)
            {
                if (c.Profile.SingleFigureUnlabelled && FigPattern.IsMatch(labels[0].Text))
                    c.Add(PreflightRules.FigSingle, Severity.Info, labels[0].Po.Obj.Id, labels[0].Page.Id,
                        new I18nText($"Only one figure: it should not be numbered or labelled (\"{labels[0].Text}\").", $"Nur eine Figur: Sie sollte weder nummeriert noch bezeichnet werden (\"{labels[0].Text}\")."),
                        new I18nText("Remove the figure label.", "Figurenbezeichnung entfernen."));
            }
            else
            {
                var labelled = new HashSet<string>(labels.Select(l => l.Page.Id));
                if (contentPages.Count > 1 || labels.Count > 0)
                    foreach (var p in contentPages.Where(p => !labelled.Contains(p.Id)))
                        c.Add(PreflightRules.FigMissing, Severity.Error, null, p.Id,
                            new I18nText("Sheet with drawing content but no figure label.", "Blatt mit Zeichnungsinhalt, aber ohne Figurenbezeichnung."),
                            new I18nText($"Add a figure label (\"{c.Profile.FigPrefix} n\").", $"Figurenbezeichnung einfügen (\"{c.Profile.FigPrefix} n\")."));
            }

            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var max = 0;
            foreach (var (po, page, text) in labels)
            {
                var m = FigPattern.Match(text);
                if (!m.Success)
                {
                    c.Add(PreflightRules.FigSequence, Severity.Warning, po.Obj.Id, page.Id,
                        new I18nText($"Figure label \"{text}\" does not follow the pattern \"{c.Profile.FigPrefix} 1\".", $"Figurenbezeichnung \"{text}\" folgt nicht dem Muster \"{c.Profile.FigPrefix} 1\"."),
                        new I18nText("Use the automatic figure numbering.", "Automatische Figurennummerierung verwenden."));
                    continue;
                }
                var num = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var key = num.ToString(CultureInfo.InvariantCulture) + m.Groups[3].Value.ToUpperInvariant();
                if (seen.ContainsKey(key))
                {
                    c.Add(PreflightRules.FigDuplicate, Severity.Error, po.Obj.Id, page.Id,
                        new I18nText($"Figure label \"{text}\" is used more than once.", $"Figurenbezeichnung \"{text}\" kommt mehrfach vor."),
                        new I18nText("Renumber the figures.", "Figuren neu nummerieren."));
                    continue;
                }
                seen[key] = po.Obj.Id;
                if (num != max && num != max + 1)
                    c.Add(PreflightRules.FigSequence, Severity.Error, po.Obj.Id, page.Id,
                        new I18nText($"Figure \"{text}\" breaks the consecutive numbering (expected {max + 1}).", $"Figur \"{text}\" unterbricht die fortlaufende Nummerierung (erwartet {max + 1})."),
                        new I18nText("Number figures consecutively in order of appearance.", "Figuren in der Reihenfolge ihres Erscheinens fortlaufend nummerieren."));
                max = Math.Max(max, num);
                var size = Prim.Flatten(po.Prims).OfType<TextPrim>().Select(t => t.TextStyle.Size).DefaultIfEmpty(0).Max();
                if (numeralSize > 0 && size <= numeralSize)
                    c.Add(PreflightRules.FigSize, Severity.Warning, po.Obj.Id, page.Id,
                        new I18nText("Figure label is not larger than the reference numerals.", "Figurenbezeichnung ist nicht größer als die Bezugszeichen."),
                        new I18nText("Increase the figure label text height.", "Schrifthöhe der Figurenbezeichnung erhöhen."));
            }
        }

        // ------------------------------------------------------------------------------------------ numeral targets

        /// <summary>
        /// Lead lines of numerals: attached to an element that no longer exists, or loose and ending on no drawn element
        /// ("points at nothing", warning); loose but ending on an element ("not attached", info).
        /// </summary>
        private static void CheckNumeralTargets(Ctx c, Page page, List<PageObject> objs)
        {
            foreach (var po in objs)
            {
                var o = po.Obj;
                if (!ReferenceNumerals.IsNumeralType(o.Type) || o.Flag("underline", false)) continue;
                var n = ReferenceNumerals.NumeralOf(o);
                var a = o.Attach is { Count: > 0 } att ? att[0] : null;
                if (a is not null && c.Doc.Get(a.ObjectId) is not null) continue;
                var pts = c.Doc.ResolvePoints(o);
                var onSomething = a is null && pts.Count > 0 && objs.Any(x => x.Obj.Id != o.Id && !ReferenceNumerals.IsNumeralType(x.Obj.Type) && x.Bounds is Rect b && b.Inflate(1).Contains(pts[0]));
                if (onSomething)
                    c.Add(PreflightRules.LeadDangling, Severity.Info, o.Id, page.Id,
                        new I18nText($"Lead line of {n} is not attached to a part.", $"Bezugslinie von {n} ist mit keinem Teil verbunden."),
                        new I18nText("Snap the line end onto the part so it follows when the part moves.", "Linienende auf das Teil einrasten, damit es beim Verschieben folgt."));
                else
                    c.Add(PreflightRules.LeadTarget, Severity.Warning, o.Id, page.Id,
                        a is null
                            ? new I18nText($"Numeral {n} points at nothing: its lead line ends on no element.", $"Bezugszeichen {n} zeigt ins Leere: seine Bezugslinie endet auf keinem Element.")
                            : new I18nText($"Numeral {n} points at an element that no longer exists.", $"Bezugszeichen {n} zeigt auf ein Element, das es nicht mehr gibt."),
                        new I18nText("Draw the lead line to the part it names, or delete the numeral.", "Bezugslinie bis zum bezeichneten Teil ziehen oder das Bezugszeichen löschen."));
            }
        }

        /// <summary>Parts (parts-list elements) that no numeral points at and that carry no numeral themselves (drafting aid).</summary>
        private static void CheckUnnumberedParts(Ctx c)
        {
            var numbered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, part, numeralId) in ReferenceNumerals.NumberedParts(c.Doc))
            {
                numbered.Add(part.Id);
                // A numeral with an arrow names the whole assembly (group) it points at.
                if (numeralId is not null && c.Doc.Get(numeralId) is { } nobj && nobj.Str("end", "none") == "arrow" && part.GroupId is not null)
                    foreach (var m in c.Doc.Objects.Values.Where(x => x.GroupId == part.GroupId)) numbered.Add(m.Id);
            }
            foreach (var page in c.Doc.Pages)
                foreach (var o in c.Doc.ObjectsOn(page))
                {
                    if (numbered.Contains(o.Id) || PatentExport.IsExcluded(c.Doc, o) || !PartsList.IsPart(c.Reg, o)) continue;
                    var name = Exports.Label(c.Reg, o, Lang.En);
                    var nameDe = Exports.Label(c.Reg, o, Lang.De);
                    c.Add(PreflightRules.PartUnnumbered, Severity.Info, o.Id, page.Id,
                        new I18nText($"Part \"{name}\" has no reference numeral.", $"Teil \"{nameDe}\" hat kein Bezugszeichen."),
                        new I18nText("Add a numeral, or number all parts of the page at once (numeral menu: Number all parts).", "Bezugszeichen ergänzen oder alle Teile der Seite auf einmal nummerieren (Menü des Bezugszeichens: Alle Teile nummerieren)."));
                }
        }

        // ------------------------------------------------------------------------------------------ references

        private static void CheckReferences(Ctx c)
        {
            foreach (var issue in ReferenceNumerals.Check(c.Doc))
            {
                var (rule, sev) = issue.Kind switch
                {
                    ReferenceIssueKind.SameNumeralDifferentLabels => (PreflightRules.RefConflict, Severity.Error),
                    ReferenceIssueKind.NumeralWithoutEntry => (PreflightRules.RefMissing, Severity.Error),
                    ReferenceIssueKind.LabelWithDifferentNumerals => (PreflightRules.RefLabel, Severity.Warning),
                    ReferenceIssueKind.SameNumeralDifferentParts => (PreflightRules.RefDuplicate, Severity.Error),
                    _ => (PreflightRules.RefUnused, Severity.Warning),
                };
                var obj = issue.ObjectIds.FirstOrDefault();
                c.Add(rule, sev, obj, obj is null ? null : c.Doc.PageOf(obj)?.Id, issue.Message,
                    new I18nText("Keep one numeral per part, consistent with the description (reference list).", "Ein Bezugszeichen je Teil, übereinstimmend mit der Beschreibung (Bezugszeichenliste)."));
            }
        }

        private static void AddManual(Ctx c)
        {
            void M(PreflightRule r, string en, string de) => c.Add(r, Severity.Manual, null, null, new I18nText(en, de));
            M(PreflightRules.ManualLines, "Check that all lines are durable, black, sufficiently dense and dark, uniformly thick and well-defined.",
                "Prüfen, dass alle Linien dauerhaft, schwarz, ausreichend dicht und dunkel, gleichmäßig dick und scharf begrenzt sind.");
            M(PreflightRules.ManualContent, "Check proportions, that every claimed feature is shown and that enough views are provided.",
                "Proportionen prüfen sowie, dass jedes beanspruchte Merkmal dargestellt ist und genügend Ansichten vorhanden sind.");
            M(PreflightRules.ManualText, "Check that the drawing contains only indispensable words (e.g. \"water\", \"open\", \"section A–A\").",
                "Prüfen, dass die Zeichnung nur unerlässliche Wörter enthält (z. B. \"Wasser\", \"offen\", \"Schnitt A–A\").");
            M(PreflightRules.ManualPriorArt, "Label prior-art figures (\"Prior Art\" / \"Stand der Technik\").",
                "Figuren zum Stand der Technik kennzeichnen (\"Stand der Technik\" / \"Prior Art\").");
            // Office-neutral: the DPMA does not accept photographs as drawings, EPO, PCT and USPTO only in exceptional cases.
            M(PreflightRules.ManualPhotos, "Do not use photographs instead of drawings (the offices accept them only in exceptional cases, if at all).",
                "Keine Fotos anstelle von Zeichnungen verwenden (die Ämter lassen sie höchstens in Ausnahmefällen zu).");
        }

        // ------------------------------------------------------------------------------------------ helpers

        private static Style? StyleOf(Primitive p) => p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, RectPrim r => r.Style, _ => null };

        private static IEnumerable<string> Colours(IEnumerable<Primitive> prims)
        {
            foreach (var p in prims)
            {
                if (p is TextPrim t) { yield return t.TextStyle.Color ?? "#000000"; continue; }
                var s = StyleOf(p);
                if (s is null) continue;
                if (s.HasStroke) yield return s.EffectiveStroke;
                if (s.Fill is { Kind: FillKind.Solid, Color: string fc }) yield return fc;
                if (s.Fill is { Kind: FillKind.Hatch } f)
                {
                    if (f.Color is not null) yield return f.Color;
                    if (f.Hatch?.Color is not null) yield return f.Hatch.Color;
                }
            }
        }

        /// <summary>0 = black/white, 1 = grey, 2 = colour.</summary>
        private static int Classify(string colour)
        {
            var c = colour.Trim().ToLowerInvariant();
            if (c is "none" or "black" or "white" or "transparent") return 0;
            var hex = c.TrimStart('#');
            if (hex.Length == 3) hex = string.Concat(hex.Select(ch => new string(ch, 2)));
            if (hex.Length < 6 || !int.TryParse(hex.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return 2;
            int r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255;
            if (r == g && g == b) return r is 0 or 255 ? 0 : 1;
            return 2;
        }

        private static double Area(Primitive p) => p switch
        {
            RectPrim r => r.W * r.H,
            EllipsePrim e => Math.PI * e.Rx * e.Ry,
            PathPrim pa when pa.IsClosed => Hatching.Contours(pa).Sum(c => Math.Abs(Geo.PolygonArea(c))),
            _ => 0,
        };
    }
}
