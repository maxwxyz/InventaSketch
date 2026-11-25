using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Patent
{
    /// <summary>A preflight rule: id, research reference (P-number), title and the official sources per office.</summary>
    public sealed record PreflightRule(string Id, string Research, I18nText Title, IReadOnlyDictionary<Office, IReadOnlyList<Citation>> Sources)
    {
        /// <summary>Sources for a profile's office; the common profile cites all offices.</summary>
        public IReadOnlyList<Citation> For(Office office)
        {
            if (office == Office.Common) return Sources.Values.SelectMany(c => c).ToList();
            return Sources.TryGetValue(office, out var list) ? list : Array.Empty<Citation>();
        }

        public string SourceText(Office office) => string.Join("; ", For(office).Select(c => c.ToString()));
    }

    /// <summary>
    /// Rule catalogue. Sources are the rows of docs/en/research/drawing-rules.md (research status 2026-11-20);
    /// the "P" numbers refer to section 5 of that document.
    /// </summary>
    public static class PreflightRules
    {
        // ---- Source URLs (primary sources, see research doc) ----
        private const string PatV6 = "https://www.gesetze-im-internet.de/patv/__6.html";
        private const string PatVA2 = "https://www.gesetze-im-internet.de/patv/anlage_2.html";
        private const string DpmaFormal = "https://dpma.de/service/elektronische_anmeldung/dpmadirekt/voraussetzungen_anforderungen/formal/index.html";
        private const string DpmaP2791 = "https://www.dpma.de/fileadmin/user_upload/Dokumente/Formulare/Deutsch/patent/p2791.pdf";
        private const string EpA49 = "https://www.epo.org/en/legal/official-journal/2025/07/a49";
        private const string EpA57 = "https://www.epo.org/en/legal/official-journal/2025/09/a57";
        private const string EpGl = "https://www.epo.org/en/legal/guidelines-epc/2026/a_ix.html";
        private const string PctR11 = "https://www.wipo.int/pct/en/texts/rules/r11.html";
        private const string PctAi = "https://www.wipo.int/documents/d/pct-system/docs-en-texts-ai.pdf";
        private const string PctGuide = "https://www.wipo.int/documents/d/pct-system/docs-en-guide-gdvol1.pdf";
        private const string Cfr184 = "https://www.ecfr.gov/current/title-37/section-1.84";
        private const string Cfr183 = "https://www.ecfr.gov/current/title-37/section-1.83";
        private const string Mpep = "https://www.uspto.gov/web/offices/pac/mpep/s608.html";

        private static Citation De(string row, string prov, string url = PatVA2) => new(row, prov, url);
        private static Citation Ep(string row, string prov, string url = EpA49) => new(row, prov, url);
        private static Citation Pc(string row, string prov, string url = PctR11) => new(row, prov, url);
        private static Citation Us(string row, string prov, string url = Cfr184) => new(row, prov, url);

        private static PreflightRule R(string id, string research, string en, string de, Citation[]? dpma = null, Citation[]? epo = null, Citation[]? pct = null, Citation[]? us = null)
        {
            var d = new Dictionary<Office, IReadOnlyList<Citation>>();
            if (dpma is not null) d[Office.Dpma] = dpma;
            if (epo is not null) d[Office.Epo] = epo;
            if (pct is not null) d[Office.Pct] = pct;
            if (us is not null) d[Office.Uspto] = us;
            return new PreflightRule(id, research, new I18nText(en, de), d);
        }

        public static readonly PreflightRule PageSize = R("PF-PAGE-SIZE", "P1", "Sheet size", "Blattgröße",
            new[] { De("DE-01", "PatV § 6(2)", PatV6) }, new[] { Ep("EP-02", "OJ EPO 2025, A49 Art. 2(1)") }, new[] { Pc("PCT-01", "Rule 11.5 PCT") }, new[] { Us("US-06", "37 CFR 1.84(f)") });

        public static readonly PreflightRule PageOrientation = R("PF-PAGE-ORIENT", "P2", "Sheet orientation", "Blattausrichtung",
            new[] { De("DE-01", "PatV § 6(2)", PatV6) }, new[] { Ep("EP-02", "OJ EPO 2025, A49 Art. 2(2)") }, new[] { Pc("PCT-01", "Rule 11.2 PCT") }, new[] { Us("US-12", "37 CFR 1.84(i)") });

        public static readonly PreflightRule Margins = R("PF-MARGIN", "P3", "Object outside the usable area", "Objekt außerhalb der Nutzfläche",
            new[] { De("DE-02", "PatV Anlage 2 Nr. 1") }, new[] { Ep("EP-03", "OJ EPO 2025, A49 Art. 1(1)"), Ep("EP-03", "Guidelines A-IX 4.1", EpGl) },
            new[] { Pc("PCT-02", "Rule 11.6(c),(e) PCT") }, new[] { Us("US-07", "37 CFR 1.84(g)") });

        public static readonly PreflightRule Frame = R("PF-FRAME", "P4", "Frame around the usable area", "Rahmen um die Nutzfläche",
            null, new[] { Ep("EP-03", "OJ EPO 2025, A49 Art. 1(1)") }, new[] { Pc("PCT-02", "Rule 11.6(e) PCT") }, new[] { Us("US-07", "37 CFR 1.84(g)") });

        public static readonly PreflightRule Colour = R("PF-COLOUR", "P5", "Colour", "Farbe",
            new[] { De("DE-05", "PatV Anlage 2 (file formats); DPMA formal requirements", DpmaFormal) },
            new[] { Ep("EP-04", "OJ EPO 2025, A49 Art. 1(2)(a)"), Ep("EP-05", "OJ EPO 2025, A57", EpA57) },
            new[] { Pc("PCT-05", "Rule 11.13(a) PCT") }, new[] { Us("US-02", "37 CFR 1.84(a)(2)") });

        public static readonly PreflightRule Greyscale = R("PF-GREY", "P5", "Grey tones or transparency", "Grautöne oder Transparenz",
            new[] { De("DE-04", "PatV Anlage 2 Nr. 2") }, new[] { Ep("EP-04", "OJ EPO 2025, A49 Art. 1(2)(a)"), Ep("EP-05", "OJ EPO 2025, A57", EpA57) },
            new[] { Pc("PCT-05", "Rule 11.13(a) PCT") }, new[] { Us("US-02", "37 CFR 1.84(a)(2)"), Us("US-14", "37 CFR 1.84(l)") });

        public static readonly PreflightRule SolidBlack = R("PF-SOLID-BLACK", "P6", "Solid black area", "Schwarze Vollfläche",
            new[] { De("DE-04", "PatV Anlage 2 Nr. 2") }, new[] { Ep("EP-20", "Guidelines A-IX 7.2", EpGl) }, new[] { Pc("PCT-05", "Rule 11.13(a) PCT") }, new[] { Us("US-15", "37 CFR 1.84(m)") });

        public static readonly PreflightRule TextHeight = R("PF-TEXT-HEIGHT", "P7", "Character height below minimum", "Zeichenhöhe unter Minimum",
            new[] { De("DE-09", "PatV Anlage 2 Nr. 5") }, new[] { Ep("EP-11", "OJ EPO 2025, A49 Art. 1(2)(g)") }, new[] { Pc("PCT-10", "Rule 11.13(h) PCT") }, new[] { Us("US-18", "37 CFR 1.84(p)(3)") });

        public static readonly PreflightRule LineWidth = R("PF-LINE-WIDTH", "P8", "Thin line (reproduction at 2/3)", "Dünne Linie (Verkleinerung auf 2/3)",
            new[] { De("DE-07", "PatV Anlage 2 Nr. 4") }, new[] { Ep("EP-07", "OJ EPO 2025, A49 Art. 1(2)(c)") }, new[] { Pc("PCT-07", "Rule 11.13(c) PCT") }, new[] { Us("US-13", "37 CFR 1.84(k)") });

        public static readonly PreflightRule EnclosedNumeral = R("PF-NUMERAL-ENCLOSED", "P9", "Reference sign in brackets, circle or quotes", "Bezugszeichen in Klammern, Kreis oder Anführungszeichen",
            new[] { De("DE-12", "PatV Anlage 2 Nr. 7 (not regulated)") }, new[] { Ep("EP-08", "OJ EPO 2025, A49 Art. 1(2)(d)"), Ep("EP-08", "Guidelines A-IX 7.5", EpGl) },
            new[] { Pc("PCT-08", "Rule 11.13(e) PCT") }, new[] { Us("US-17", "37 CFR 1.84(p)(1)") });

        public static readonly PreflightRule LeadCrossing = R("PF-LEAD-CROSS", "P10", "Lead lines cross", "Bezugslinien kreuzen sich",
            null, new[] { Ep("EP-16", "Guidelines A-IX 7.5.1", EpGl) }, null, new[] { Us("US-20", "37 CFR 1.84(q)") });

        public static readonly PreflightRule LeadDangling = R("PF-LEAD-FREE", "P11", "Lead line not attached to a part", "Bezugslinie nicht mit Teil verbunden",
            null, new[] { Ep("EP-16", "Guidelines A-IX 7.5.1", EpGl) }, null, new[] { Us("US-20", "37 CFR 1.84(q)") });

        public static readonly PreflightRule FigMissing = R("PF-FIG-MISSING", "P13", "Figure label missing", "Figurenbezeichnung fehlt",
            new[] { De("DE-10", "PatV Anlage 2 Nr. 6") }, new[] { Ep("EP-13", "Guidelines A-IX 5.2", EpGl) }, new[] { Pc("PCT-12", "Rule 11.13(k) PCT") }, new[] { Us("US-24", "37 CFR 1.84(u)") });

        public static readonly PreflightRule FigDuplicate = R("PF-FIG-DUP", "P13", "Duplicate figure label", "Doppelte Figurenbezeichnung",
            new[] { De("DE-10", "PatV Anlage 2 Nr. 6") }, new[] { Ep("EP-12", "OJ EPO 2025, A49 Art. 1(2)(h)") }, new[] { Pc("PCT-12", "Rule 11.13(k) PCT") }, new[] { Us("US-24", "37 CFR 1.84(u)(1)") });

        public static readonly PreflightRule FigSequence = R("PF-FIG-SEQ", "P13", "Figures not numbered consecutively", "Figuren nicht fortlaufend nummeriert",
            new[] { De("DE-10", "PatV Anlage 2 Nr. 6") }, new[] { Ep("EP-12", "OJ EPO 2025, A49 Art. 1(2)(h)") }, new[] { Pc("PCT-12", "Rule 11.13(k) PCT") }, new[] { Us("US-24", "37 CFR 1.84(u)(1)") });

        public static readonly PreflightRule FigSingle = R("PF-FIG-SINGLE", "P13", "Single figure must not be labelled", "Einzelne Figur ohne Bezeichnung",
            null, new[] { Ep("EP-13", "Guidelines A-IX 5.2", EpGl) }, new[] { Pc("PCT-12", "Rule 11.13(k) PCT"), Pc("PCT-12", "PCT Applicant's Guide 5.141", PctGuide) }, new[] { Us("US-24", "37 CFR 1.84(u)(1)") });

        public static readonly PreflightRule FigSize = R("PF-FIG-SIZE", "P14", "Figure label not larger than reference signs", "Figurenbezeichnung nicht größer als Bezugszeichen",
            null, new[] { Ep("EP-13", "Guidelines A-IX 5.2", EpGl) }, null, new[] { Us("US-24", "37 CFR 1.84(u)(2)") });

        public static readonly PreflightRule SheetNumber = R("PF-SHEET-NUMBER", "P15", "Sheet number missing or malformed", "Blattnummer fehlt oder ist falsch",
            null, new[] { Ep("EP-14", "OJ EPO 2025, A49 Art. 2(5)"), Ep("EP-14", "Guidelines A-IX 4.2", EpGl) },
            new[] { Pc("PCT-03", "Rule 11.7 PCT"), Pc("PCT-03", "Administrative Instructions Section 207(b)(iii)", PctAi) }, new[] { Us("US-23", "37 CFR 1.84(t)") });

        public static readonly PreflightRule TextWords = R("PF-TEXT-WORDS", "P16", "Too much text in the drawing", "Zu viel Text in der Zeichnung",
            new[] { De("DE-13", "PatV Anlage 2 Nr. 8") }, new[] { Ep("EP-18", "OJ EPO 2025, A49 Art. 1(2)(j)"), Ep("EP-18", "Guidelines A-IX 8", EpGl) },
            new[] { Pc("PCT-04", "Rule 11.11 PCT") }, new[] { Us("US-16", "37 CFR 1.84(o)") });

        public static readonly PreflightRule ScaleText = R("PF-SCALE-TEXT", "P17", "Scale indication in the drawing", "Maßstabsangabe in der Zeichnung",
            new[] { De("DE-07", "PatV Anlage 2 Nr. 4") }, new[] { Ep("EP-07", "OJ EPO 2025, A49 Art. 1(2)(c)"), Ep("EP-07", "Guidelines A-IX 7.4", EpGl) },
            new[] { Pc("PCT-07", "Rule 11.13(d) PCT") }, new[] { Us("US-13", "37 CFR 1.84(k)") });

        public static readonly PreflightRule CentreLines = R("PF-CENTRELINE", "P18", "Centre lines not permitted", "Mittellinien nicht zulässig",
            null, null, new[] { Pc("PCT-14", "PCT Applicant's Guide 5.144", PctGuide) }, new[] { Us("US-08", "37 CFR 1.84(h)") });

        public static readonly PreflightRule RefConflict = R("PF-REF-CONFLICT", "P19", "Same numeral for different parts", "Gleiches Bezugszeichen für verschiedene Teile",
            new[] { De("DE-12", "PatV Anlage 2 Nr. 7") }, new[] { Ep("EP-15", "OJ EPO 2025, A49 Art. 1(2)(i)"), Ep("EP-15", "Guidelines A-IX 7.5.4", EpGl) },
            new[] { Pc("PCT-13", "Rule 11.13(m) PCT") }, new[] { Us("US-19", "37 CFR 1.84(p)(4)") });

        public static readonly PreflightRule RefLabel = R("PF-REF-LABEL", "P19", "Same part with different numerals", "Gleiches Teil mit verschiedenen Bezugszeichen",
            new[] { De("DE-12", "PatV Anlage 2 Nr. 7") }, new[] { Ep("EP-15", "Guidelines A-IX 7.5.4–7.5.5", EpGl) },
            new[] { Pc("PCT-13", "Rule 11.13(m) PCT") }, new[] { Us("US-19", "37 CFR 1.84(p)(4)") });

        public static readonly PreflightRule RefMissing = R("PF-REF-MISSING", "P19", "Numeral not in the reference list", "Bezugszeichen nicht in der Liste",
            new[] { De("DE-12", "PatV Anlage 2 Nr. 7") }, new[] { Ep("EP-15", "OJ EPO 2025, A49 Art. 1(2)(i)") },
            new[] { Pc("PCT-13", "Rule 11.13(l) PCT") }, new[] { Us("US-19", "37 CFR 1.84(p)(5)") });

        public static readonly PreflightRule RefUnused = R("PF-REF-UNUSED", "P19", "Reference list entry not drawn", "Listeneintrag nicht gezeichnet",
            new[] { De("DE-12", "PatV Anlage 2 Nr. 7") }, new[] { Ep("EP-15", "OJ EPO 2025, A49 Art. 1(2)(i)") },
            new[] { Pc("PCT-13", "Rule 11.13(l) PCT") }, new[] { Us("US-19", "37 CFR 1.84(p)(5)") });

        public static readonly PreflightRule ManualLines = R("PF-MANUAL-LINES", "P28", "Line quality", "Linienqualität",
            new[] { De("DE-04", "PatV Anlage 2 Nr. 2") }, new[] { Ep("EP-04", "OJ EPO 2025, A49 Art. 1(2)(a)") }, new[] { Pc("PCT-05", "Rule 11.13(a) PCT") }, new[] { Us("US-14", "37 CFR 1.84(l)") });

        public static readonly PreflightRule ManualContent = R("PF-MANUAL-CONTENT", "P28", "Proportions and completeness", "Proportionen und Vollständigkeit",
            new[] { De("DE-08", "PatV Anlage 2 Nr. 5"), De("DE-10", "PatV Anlage 2 Nr. 6") }, new[] { Ep("EP-10", "OJ EPO 2025, A49 Art. 1(2)(f)") }, new[] { Pc("PCT-09", "Rule 11.13(g) PCT") }, new[] { Us("US-01", "37 CFR 1.83(a)", Cfr183) });

        public static readonly PreflightRule ManualText = R("PF-MANUAL-TEXT", "P16", "Only indispensable words", "Nur unerlässliche Wörter",
            new[] { De("DE-13", "PatV Anlage 2 Nr. 8") }, new[] { Ep("EP-18", "Guidelines A-IX 8", EpGl) }, new[] { Pc("PCT-04", "Rule 11.11 PCT") }, new[] { Us("US-16", "37 CFR 1.84(o)") });

        public static readonly PreflightRule ManualPriorArt = R("PF-MANUAL-PRIOR-ART", "P27", "Prior-art figures labelled", "Stand-der-Technik-Figuren gekennzeichnet",
            new[] { De("DE-11", "PatV Anlage 2 Nr. 6") }, null, null, new[] { Us("US-26", "MPEP 608.02(g)", Mpep) });

        public static readonly PreflightRule ManualPhotos = R("PF-MANUAL-PHOTOS", "P22", "No photographs as drawings", "Keine Fotos als Zeichnungen",
            new[] { De("DE-15", "DPMA Merkblatt P 2791 section 2.4", DpmaP2791) }, new[] { Ep("EP-21", "Guidelines A-IX 1.2", EpGl) },
            new[] { Pc("PCT-15", "PCT Applicant's Guide 5.159", PctGuide) }, new[] { Us("US-04", "37 CFR 1.84(b)") });

        public static IReadOnlyList<PreflightRule> All { get; } = new[]
        {
            PageSize, PageOrientation, Margins, Frame, Colour, Greyscale, SolidBlack, TextHeight, LineWidth, EnclosedNumeral, LeadCrossing, LeadDangling,
            FigMissing, FigDuplicate, FigSequence, FigSingle, FigSize, SheetNumber, TextWords, ScaleText, CentreLines,
            RefConflict, RefLabel, RefMissing, RefUnused, ManualLines, ManualContent, ManualText, ManualPriorArt, ManualPhotos,
        };
    }
}
