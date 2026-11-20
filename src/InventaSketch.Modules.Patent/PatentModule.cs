using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;

namespace InventaSketch.Modules.Patent
{
    /// <summary>Type ids of the patent module.</summary>
    public static class PatentIds
    {
        public const string Numeral = "patent.numeral";
        public const string Sheet = "patent.sheet";
        public const string PageNumber = "patent.pagenumber";
        public const string Category = "patent.drawing";
    }

    /// <summary>
    /// Reference numeral with lead line. Point 0 = attachment on the part, point 1 = numeral position.
    /// The numeral is <see cref="SketchObject.Ref"/> (preferred), else <see cref="SketchObject.Text"/>, else param "numeral".
    /// Under profiles that forbid enclosed signs (EPO, PCT, USPTO, common-safe) brackets, circles and quotes are never drawn.
    /// </summary>
    public sealed class NumeralType : AnnPointType
    {
        public const double DefaultTextSize = 5.0;

        public NumeralType() : base(PatentIds.Numeral, new I18nText("Reference numeral", "Bezugszeichen"), PatentIds.Category,
            "reference", "numeral", "sign", "bezugszeichen", "patent", "lead line", "bezugslinie") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(14, -10) };
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            new ParamDef("numeral", new I18nText("Numeral", "Bezugszeichen"), ParamKind.Text) { Default = "10" },
            new ParamDef("label", new I18nText("Part name", "Teilebezeichnung"), ParamKind.Text),
            new ParamDef("underline", new I18nText("Underlined (surface, no lead line)", "Unterstrichen (Fläche, ohne Bezugslinie)"), ParamKind.Boolean) { Default = false },
        }.Concat(LeaderType.LeaderParams("curved")).ToList();

        /// <summary>Stored numeral of an object (no profile sanitising).</summary>
        public static string NumeralOf(SketchObject o)
            => !string.IsNullOrEmpty(o.Ref) ? o.Ref! : !string.IsNullOrEmpty(o.Text) ? o.Text! : o.Str("numeral", "10");

        private static readonly (string Open, string Close)[] Enclosures =
        {
            ("(", ")"), ("[", "]"), ("{", "}"), ("<", ">"), ("\"", "\""), ("'", "'"), ("“", "”"), ("„", "“"), ("‘", "’"), ("«", "»"), ("»", "«"),
        };

        /// <summary>
        /// Removes matching brackets/quotes around a numeral ("(10)" → "10"). Primes such as 12' are kept
        /// because they are not enclosures (EP GL A-IX 7.5).
        /// </summary>
        public static string StripEnclosure(string s)
        {
            var t = s.Trim();
            for (var changed = true; changed && t.Length >= 2;)
            {
                changed = false;
                foreach (var (open, close) in Enclosures)
                    if (t.Length >= 2 && t.StartsWith(open, StringComparison.Ordinal) && t.EndsWith(close, StringComparison.Ordinal))
                    {
                        t = t.Substring(open.Length, t.Length - open.Length - close.Length).Trim();
                        changed = true;
                    }
            }
            return t;
        }

        /// <summary>True if the text is a sign enclosed in brackets or quotes, e.g. "(10)", "[12]", "'14'".</summary>
        public static bool IsEnclosed(string s) => s.Trim().Length > 0 && StripEnclosure(s) != s.Trim() && Regex.IsMatch(StripEnclosure(s), @"^\d+[A-Za-z]?['’]*$");

        public static string DisplayNumeral(SketchObject o, PatentProfile profile)
        {
            var n = NumeralOf(o);
            return profile.ForbidEnclosedNumerals ? StripEnclosure(n) : n;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var profile = PatentProfiles.Get(ctx.Doc.Settings.Profile);
            return LeaderGeometry.Build(p[0], p[p.Count - 1], DisplayNumeral(o, profile), o.Num("textSize", DefaultTextSize),
                LeaderGeometry.ParseCurve(o.Str("curve", "curved")), LeaderGeometry.ParseEnd(o.Str("end", "none")), o.Flag("bend", false), o.Flag("underline", false));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in LeaderType.LeaderActions(o, "curved")) yield return a;
            yield return new ContextAction("patent.numeral.underline", new I18nText("Underlined surface numeral", "Unterstrichenes Flächen-Bezugszeichen"), "underline",
                x => x.WithParam("underline", !x.Flag("underline", false))) { IsActive = o.Flag("underline", false) };
        }
    }

    /// <summary>
    /// Drawing-sheet helper: sheet outline and usable area (margins) of a profile. Role Helper; never exported
    /// and ignored by preflight (offices forbid frames around the sight: EP A49 Art. 1(1), PCT R. 11.6(e), 37 CFR 1.84(g)).
    /// </summary>
    public sealed class SheetType : AnnBoxType
    {
        public const string HelperColour = "#3a7bd5";

        public SheetType() : base(PatentIds.Sheet, new I18nText("Drawing sheet frame (helper)", "Zeichnungsblatt-Rahmen (Hilfslinie)"), PatentIds.Category,
            "sheet", "margin", "rand", "blatt", "frame", "usable area", "nutzfläche", "a4", "letter") { }

        public override Size2 DefaultSize => PageSizes.A4Portrait;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            new ParamDef("profile", new I18nText("Profile", "Profil"), ParamKind.Choice)
            {
                Default = PatentProfiles.CommonSafe.Id,
                Options = PatentProfiles.All.Select(p => new ParamOption(p.Id, p.Name)).ToList(),
            },
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var profile = PatentProfiles.Get(o.Str("profile", ctx.Doc.Settings.Profile ?? PatentProfiles.CommonSafe.Id));
            double w = o.Size.W, h = o.Size.H, x = -w / 2, y = -h / 2;
            var m = profile.Margins;
            var helper = Style.Helper with { Stroke = HelperColour };
            var area = Style.Helper with { Stroke = HelperColour, Dash = DashStyle.Dash };
            var ts = new TextStyle { Size = 3, Color = HelperColour, Anchor = TextAnchor.Start };
            var uw = w - m.Left - m.Right; var uh = h - m.Top - m.Bottom;
            return new Primitive[]
            {
                Prim.Rect(x, y, w, h, helper),
                Prim.Rect(x + m.Left, y + m.Top, uw, uh, area),
                Prim.Text(x + m.Left, y + m.Top - 3, profile.Name.Get(ctx.Lang) + FormattableString.Invariant($" – {uw:0.#} × {uh:0.#} mm"), ts),
            };
        }
    }

    /// <summary>Sheet number "n/N" (PCT R. 11.7; 37 CFR 1.84(t)), computed from the page position unless "auto" is off.</summary>
    public sealed class PageNumberType : AnnBoxType
    {
        public const double DefaultTextSize = 6.0;

        public PageNumberType() : base(PatentIds.PageNumber, new I18nText("Sheet number", "Blattnummer"), PatentIds.Category,
            "page", "sheet", "number", "blatt", "seite", "nummer", "1/3") { }

        public override Size2 DefaultSize => new(16, 8);
        public override bool HasText => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            new ParamDef("auto", new I18nText("Automatic n/N", "Automatisch n/N"), ParamKind.Boolean) { Default = true },
            new ParamDef("textSize", new I18nText("Text height", "Schrifthöhe"), ParamKind.Number) { Default = DefaultTextSize, Min = 1, Max = 30, Unit = "mm" },
        };

        public static string TextOf(SketchObject o, SketchDocument doc)
        {
            if (!o.Flag("auto", true)) return o.Text ?? "1/1";
            var page = doc.PageOf(o.Id);
            if (page is null) return o.Text ?? "1/1";
            var idx = doc.Pages.ToList().FindIndex(p => p.Id == page.Id);
            return PatentProfiles.Get(doc.Settings.Profile).FormatSheetNumber(idx + 1, doc.Pages.Count);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.Text(0, 0, TextOf(o, ctx.Doc), new TextStyle { Size = o.Num("textSize", DefaultTextSize) }) with { Tag = "sheetnumber" } };
    }

    /// <summary>Patent drawing module: reference numerals, sheet helper, sheet numbers, profiles, preflight and export.</summary>
    public sealed class PatentModule : IModule, IExportProvider
    {
        private static readonly ObjectType[] AllTypes = { new NumeralType(), new SheetType(), new PageNumberType() };

        public string Id => "patent";
        public I18nText Name => new("Patent", "Patent");
        public string Version => "0.2.0";
        public IEnumerable<ObjectType> Types => AllTypes;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(PatentIds.Category, new I18nText("Patent drawing", "Patentzeichnung"), Id, 55),
        };

        /// <summary>Domain exports of this module (<see cref="IExportProvider"/>).</summary>
        public IEnumerable<IExporter> Exporters => new IExporter[] { new ReferenceListExporter() };

        public IEnumerable<LibraryItem> Library => new[]
        {
            new LibraryItem(PatentIds.Numeral, PatentIds.Numeral, new I18nText("Reference numeral", "Bezugszeichen"), PatentIds.Category),
            new LibraryItem(PatentIds.Numeral + ".straight", PatentIds.Numeral, new I18nText("Reference numeral (straight)", "Bezugszeichen (gerade)"), PatentIds.Category)
            { Params = new Dictionary<string, ParamValue> { ["curve"] = "straight" } },
            new LibraryItem(PatentIds.Numeral + ".wavy", PatentIds.Numeral, new I18nText("Reference numeral (wavy)", "Bezugszeichen (wellig)"), PatentIds.Category)
            { Params = new Dictionary<string, ParamValue> { ["curve"] = "wavy" } },
            new LibraryItem(PatentIds.Numeral + ".assembly", PatentIds.Numeral, new I18nText("Reference numeral with arrow (assembly)", "Bezugszeichen mit Pfeil (Baugruppe)"), PatentIds.Category)
            { Params = new Dictionary<string, ParamValue> { ["end"] = "arrow" } },
            new LibraryItem(PatentIds.Numeral + ".surface", PatentIds.Numeral, new I18nText("Underlined surface numeral", "Unterstrichenes Flächen-Bezugszeichen"), PatentIds.Category)
            { Params = new Dictionary<string, ParamValue> { ["underline"] = true } },
            new LibraryItem(PatentIds.PageNumber, PatentIds.PageNumber, new I18nText("Sheet number", "Blattnummer"), PatentIds.Category),
            new LibraryItem(PatentIds.Sheet, PatentIds.Sheet, new I18nText("Drawing sheet frame (helper)", "Zeichnungsblatt-Rahmen (Hilfslinie)"), PatentIds.Category),
        };
    }
}
