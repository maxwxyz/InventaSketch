using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using InventaSketch.Core;

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif

namespace InventaSketch.Modules.Patent
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Office { Dpma, Epo, Pct, Uspto, Common }

    /// <summary>Severity of a preflight finding. Manual = needs human review (not automatable).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Severity { Error, Warning, Info, Manual }

    /// <summary>Colour policy of an office for drawings.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColourPolicy
    {
        /// <summary>Black and white only (colour only by petition, if at all).</summary>
        BlackAndWhite,
        /// <summary>Colour/greyscale accepted under conditions (EPO electronic filing since 2025-10-01).</summary>
        AllowedWithConditions,
    }

    public sealed record SheetSize(string Id, I18nText Name, Size2 Size);

    /// <summary>Minimum blank margins in mm.</summary>
    public sealed record Margins(double Top, double Left, double Right, double Bottom);

    /// <summary>Official source of a rule (row id of docs/en/research/patent-drawing-rules.md, provision and URL).</summary>
    public sealed record Citation(string Row, string Provision, string Url)
    {
        public override string ToString() => Row + ": " + Provision + " <" + Url + ">";
    }

    /// <summary>
    /// Drawing-sheet profile of a patent office. Values follow docs/en/research/patent-drawing-rules.md
    /// (research status 2026-11-20). Profiles are a drafting aid, not legal advice.
    /// </summary>
    public sealed record PatentProfile
    {
        public string Id { get; init; } = "";
        public I18nText Name { get; init; } = new("");
        public Office Office { get; init; }
        /// <summary>Accepted sheet sizes (portrait). The first is the default.</summary>
        public IReadOnlyList<SheetSize> Sheets { get; init; } = new[] { PatentProfiles.A4 };
        /// <summary>Minimum margins of drawing sheets (identical for all offices: 25/25/15/10 mm).</summary>
        public Margins Margins { get; init; } = new(25, 25, 15, 10);
        /// <summary>Minimum height of digits and capital letters in mm (0.32 cm at all offices).</summary>
        public double MinCharHeight { get; init; } = 3.2;
        /// <summary>Cap height relative to the em size for Arial/Helvetica-like fonts (derived, ≈0.716).</summary>
        public double CapHeightRatio { get; init; } = 0.7;
        public ColourPolicy Colour { get; init; } = ColourPolicy.BlackAndWhite;
        /// <summary>Dot-dash centre lines allowed (USPTO: no, 37 CFR 1.84(h)).</summary>
        public bool CentreLinesAllowed { get; init; } = true;
        /// <summary>Brackets, circles and quotes around reference signs are forbidden (EPO, PCT, USPTO; DPMA: not regulated).</summary>
        public bool ForbidEnclosedNumerals { get; init; } = true;
        /// <summary>Severity of crossing lead lines (USPTO: must not cross, 37 CFR 1.84(q)).</summary>
        public Severity LeadCrossingSeverity { get; init; } = Severity.Warning;
        /// <summary>Severity of missing or malformed sheet numbers; null = sheet numbering not prescribed.</summary>
        public Severity? SheetNumberSeverity { get; init; } = Severity.Warning;
        /// <summary>Sheet number format; {n} = sheet, {N} = total.</summary>
        public string SheetNumberFormat { get; init; } = "{n}/{N}";
        /// <summary>Prefix of figure labels.</summary>
        public string FigPrefix { get; init; } = "FIG.";
        /// <summary>A single figure is neither numbered nor labelled "FIG." (EP GL A-IX 5.2, PCT R. 11.13(k), 37 CFR 1.84(u)).</summary>
        public bool SingleFigureUnlabelled { get; init; } = true;
        /// <summary>Line width below which a warning is raised (mm). Reproducibility heuristic, not a normative value.</summary>
        public double MinLineWidth { get; init; } = 0.18;
        /// <summary>Solid black area above which a warning is raised (mm²; 37 CFR 1.84(m) forbids solid black areas).</summary>
        public double MaxSolidBlackArea { get; init; } = 25;
        /// <summary>Font family for exported SVG (DPMA PDF accepts only Times, Helvetica, Courier, Symbol).</summary>
        public string FontFamily { get; init; } = "Arial, Helvetica, sans-serif";

        /// <summary>Usable area (sight) of a page of the given size.</summary>
        public Rect Usable(Size2 page) => new(Margins.Left, Margins.Top, page.W - Margins.Left - Margins.Right, page.H - Margins.Top - Margins.Bottom);

        public string FormatSheetNumber(int n, int total) => SheetNumberFormat.Replace("{n}", n.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{N}", total.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Built-in profiles: dpma, epo, pct, uspto and the strictest common denominator common-safe.</summary>
    public static class PatentProfiles
    {
        public static readonly SheetSize A4 = new("a4", new I18nText("A4 (210 × 297 mm)", "A4 (210 × 297 mm)"), PageSizes.A4Portrait);
        public static readonly SheetSize Letter = new("letter", new I18nText("US Letter (215.9 × 279.4 mm)", "US Letter (215,9 × 279,4 mm)"), PageSizes.UsLetterPortrait);

        public static readonly PatentProfile Dpma = new()
        {
            Id = "dpma", Name = new I18nText("DPMA (Germany)", "DPMA (Deutschland)"), Office = Office.Dpma,
            ForbidEnclosedNumerals = false, // not regulated by the DPMA (research DE-12)
            SheetNumberSeverity = null,     // not prescribed for drawing sheets (DE-14)
            SingleFigureUnlabelled = false, // no "Fig." label prescribed (DE-10)
            FigPrefix = "Fig.",
            FontFamily = "Helvetica, Arial, sans-serif", // DE-18: Times, Helvetica, Courier, Symbol only
        };

        public static readonly PatentProfile Epo = new()
        {
            Id = "epo", Name = new I18nText("EPO (European patent)", "EPA (Europäisches Patent)"), Office = Office.Epo,
            Colour = ColourPolicy.AllowedWithConditions,
        };

        public static readonly PatentProfile Pct = new()
        {
            Id = "pct", Name = new I18nText("PCT / WIPO", "PCT / WIPO"), Office = Office.Pct,
            SheetNumberSeverity = Severity.Error, FigPrefix = "Fig.",
        };

        public static readonly PatentProfile Uspto = new()
        {
            Id = "uspto", Name = new I18nText("USPTO (utility)", "USPTO (Utility Patent)"), Office = Office.Uspto,
            Sheets = new[] { A4, Letter }, CentreLinesAllowed = false, LeadCrossingSeverity = Severity.Error,
        };

        public static readonly PatentProfile CommonSafe = new()
        {
            Id = "common-safe", Name = new I18nText("Common safe profile (all offices)", "Gemeinsames sicheres Profil (alle Ämter)"), Office = Office.Common,
            CentreLinesAllowed = false, LeadCrossingSeverity = Severity.Error, SheetNumberSeverity = Severity.Error,
        };

        public static IReadOnlyList<PatentProfile> All { get; } = new[] { CommonSafe, Dpma, Epo, Pct, Uspto };

        public static PatentProfile? Find(string? id) => id is null ? null : All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        public static PatentProfile Get(string? id) => Find(id) ?? CommonSafe;
    }
}
