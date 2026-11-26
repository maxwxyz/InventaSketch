using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// The value level of a presentation (engineering-calculations.md, section 1.1): Quick sketch (default, nothing new on
    /// the slide), Explore or Engineer. In PowerPoint the document is the presentation, so the level is kept in a
    /// presentation tag; documents that carry <see cref="DocumentSettings.ValueLevel"/> (from the web host) are the
    /// fallback when the tag is missing.
    /// </summary>
    internal static class ValueLevelSetting
    {
        public const string Tag = "ISK_VALUES";

        public static readonly ValueLevel[] Levels = { ValueLevel.QuickSketch, ValueLevel.Explore, ValueLevel.Engineer };

        /// <summary>The tag text of a level ("quick", "explore", "engineer").</summary>
        public static string TagOf(ValueLevel level) => level switch
        {
            ValueLevel.Explore => "explore",
            ValueLevel.Engineer => "engineer",
            _ => "quick",
        };

        /// <summary>The level of a tag text; null if empty or unknown.</summary>
        public static ValueLevel? Parse(string? tag) => (tag ?? "").Trim().ToLowerInvariant() switch
        {
            "explore" => ValueLevel.Explore,
            "engineer" => ValueLevel.Engineer,
            "quick" or "quicksketch" or "off" => ValueLevel.QuickSketch,
            _ => null,
        };

        /// <summary>Menu label of a level.</summary>
        public static string Label(ValueLevel level, Lang lang) => level switch
        {
            ValueLevel.Explore => lang == Lang.De ? "Erkunden" : "Explore",
            ValueLevel.Engineer => lang == Lang.De ? "Ingenieur" : "Engineer",
            _ => lang == Lang.De ? "Schnellskizze (Werte aus)" : "Quick sketch (values off)",
        };

        /// <summary>One line on what the level shows (menu supertip).</summary>
        public static string Hint(ValueLevel level, Lang lang) => level switch
        {
            ValueLevel.Explore => lang == Lang.De
                ? "Werte-Schaltfläche in der Leiste, Werte-Karte mit Formeln Schritt für Schritt, Werte ziehen und zusehen, wie alles folgt (3 Stellen)."
                : "Values button in the bar, value card with step-by-step formulas, drag a value and watch everything follow (3 digits).",
            ValueLevel.Engineer => lang == Lang.De
                ? "Wie Erkunden, mit 4 signifikanten Stellen und der Quelle jeder Formel; Widersprüche als Hinweis."
                : "Like Explore, with 4 significant digits and the source of each formula; conflicts shown as hints.",
            _ => lang == Lang.De
                ? "Nichts Neues auf der Folie: keine Werte-Schaltfläche, keine Hinweise. Werte bleiben über Bearbeiten ▸ Werte eingebbar."
                : "Nothing new on the slide: no Values button, no marks. Values can still be entered from Edit ▸ Values.",
        };

        /// <summary>Significant digits shown at a level.</summary>
        public static int Digits(ValueLevel level) => level == ValueLevel.Engineer ? 4 : 3;

        /// <summary>The level of the presentation: its tag, else the first unit document of the slide with a level, else Quick sketch.</summary>
        public static ValueLevel Read(PowerPoint.Presentation? presentation, PowerPoint.Slide? slide)
        {
            try
            {
                if (presentation is not null && Parse(presentation.Tags[Tag]) is { } level) return level;
            }
            catch
            {
                // No tag (or the presentation is closing).
            }
            if (slide is null) return ValueLevel.QuickSketch;
            try
            {
                return SketchUnits.All(slide).Select(u => u.Doc.Settings.ValueLevel).FirstOrDefault(l => l is not null) ?? ValueLevel.QuickSketch;
            }
            catch
            {
                return ValueLevel.QuickSketch;
            }
        }

        /// <summary>Stores the level of the presentation.</summary>
        public static void Write(PowerPoint.Presentation presentation, ValueLevel level)
        {
            try { presentation.Tags.Delete(Tag); } catch { /* not set */ }
            presentation.Tags.Add(Tag, TagOf(level));
        }
    }

    /// <summary>
    /// Calc 6 settings of the presentation as tags: muted plausibility checks (<c>ISK_MUTED</c>, ids separated by ";") and
    /// preferred display units (<c>ISK_UNITS</c>, "pressure=MPa;energy=MJ"). Applied to the document the values are solved in.
    /// </summary>
    internal static class ValueDocSettings
    {
        public const string MutedTag = "ISK_MUTED", UnitsTag = "ISK_UNITS";

        /// <summary>The preferred units offered in the ribbon (quantity kind id, unit symbols; the first is the default).</summary>
        public static readonly (string Kind, string[] Units)[] Offered =
        {
            ("pressure", new[] { "bar", "MPa", "kPa" }),
            ("rotationalSpeed", new[] { "min⁻¹", "s⁻¹", "rad/s" }),
            ("energy", new[] { "kWh", "MJ", "J" }),
            ("power", new[] { "W", "kW" }),
        };

        private static string Get(PowerPoint.Presentation? p, string tag)
        {
            try { return p?.Tags[tag] ?? ""; } catch { return ""; }
        }

        private static void Set(PowerPoint.Presentation p, string tag, string value)
        {
            try { p.Tags.Delete(tag); } catch { /* not set */ }
            if (value.Length > 0) p.Tags.Add(tag, value);
        }

        /// <summary><paramref name="s"/> with the presentation's muted checks and unit preferences.</summary>
        public static DocumentSettings Apply(PowerPoint.Presentation? p, DocumentSettings s)
        {
            var result = s;
            foreach (var id in Get(p, MutedTag).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) result = result.WithMutedCheck(id.Trim(), true);
            foreach (var pair in Get(p, UnitsTag).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split(new[] { '=' }, 2);
                if (kv.Length == 2) result = result.WithUnitPreference(kv[0].Trim(), kv[1].Trim());
            }
            return result;
        }

        /// <summary>Mutes or unmutes check <paramref name="id"/> in the presentation.</summary>
        public static void Mute(PowerPoint.Presentation p, string id, bool mute)
            => Set(p, MutedTag, string.Join(";", Apply(p, new DocumentSettings()).WithMutedCheck(id, mute).MutedChecks ?? Array.Empty<string>()));

        /// <summary>The preferred unit of <paramref name="kind"/> in the presentation, or null.</summary>
        public static string? UnitOf(PowerPoint.Presentation? p, string kind)
            => Apply(p, new DocumentSettings()).UnitPreferences is { } m && m.TryGetValue(kind, out var u) ? u : null;

        /// <summary>Sets (or with null clears) the preferred unit of <paramref name="kind"/>.</summary>
        public static void SetUnit(PowerPoint.Presentation p, string kind, string? unit)
        {
            var prefs = Apply(p, new DocumentSettings()).WithUnitPreference(kind, unit).UnitPreferences;
            Set(p, UnitsTag, prefs is null ? "" : string.Join(";", prefs.Select(kv => kv.Key + "=" + kv.Value)));
        }
    }

    /// <summary>One row of the value card: a variable with its value, provenance and explanation.</summary>
    internal sealed record ValueRow(string Key, string Symbol, string Name, Provenance Provenance)
    {
        /// <summary>Provenance marker: ✎ entered, = derived, ○ default, ⇄ linked, ⚠ in conflict, empty when unknown.</summary>
        public string Marker { get; init; } = "";
        /// <summary>The field text: the entered text as typed, else the formatted value; empty when unknown.</summary>
        public string Field { get; init; } = "";
        /// <summary>The variable's display unit (a bare number is read in it).</summary>
        public string Unit { get; init; } = "";
        /// <summary>The formula line of a derived value ("R = U/I = 230 V/2.00 A = 115 Ω"); null if not shown.</summary>
        public string? Formula { get; init; }
        /// <summary>A stored text that is not a value of the quantity, or a conflict of this variable.</summary>
        public string? Problem { get; init; }
        public bool Conflict { get; init; }
        /// <summary>An entered value can be cleared.</summary>
        public bool CanClear { get; init; }
        /// <summary>A known value can be dragged (Explore).</summary>
        public bool CanDrag { get; init; }
        /// <summary>A text-valued variable (a class such as the Grashof class): shown, never typed or dragged.</summary>
        public bool ReadOnly { get; init; }
        /// <summary>The value in SI, null when unknown.</summary>
        public double? Si { get; init; }
        /// <summary>The value formatted for the label, the pie and the calculator ("115 Ω"); empty when unknown.</summary>
        public string Display { get; init; } = "";
    }

    /// <summary>
    /// What the value card of an element shows (pure, unit tested): one row per variable, a status line ("✓", "1 more value:
    /// I, R or P", conflicts with both values) and whether the values label is on. Explore and Engineer add the formula
    /// line of derived values; Engineer adds the source and a fourth digit; Quick sketch (card opened from the pie) stays
    /// plain.
    /// </summary>
    /// <summary>A plausibility warning of the element in the card: the check id (for muting), its severity and the line shown.</summary>
    internal sealed record CardCheck(string Id, CheckSeverity Severity, string Text);

    internal sealed record ValueCardModel(string ObjectId, string Title, ValueLevel Level, IReadOnlyList<ValueRow> Rows, ValueStatus State, string Status, IReadOnlyList<string> Conflicts, bool ShowsLabel)
    {
        /// <summary>The element's plausibility warnings (Calc 3/4 checks, gentle, with rule and source; Calc 6 mute).</summary>
        public IReadOnlyList<CardCheck> Checks { get; init; } = Array.Empty<CardCheck>();

        /// <summary>The card lines of <paramref name="findings"/> for <paramref name="objectId"/>: "⚠ message (rule) [S9]".</summary>
        public static IReadOnlyList<CardCheck> CheckLines(IEnumerable<CheckFinding> findings, string objectId, Lang lang)
            => findings.Where(f => f.ObjectId == objectId)
                .Select(f => new CardCheck(f.CheckId, f.Severity,
                    (f.Severity == CheckSeverity.Warning ? "⚠ " : "ⓘ ") + f.Message.Get(lang)
                    + (string.IsNullOrWhiteSpace(f.Rule) ? "" : " (" + f.Rule + ")")
                    + (string.IsNullOrWhiteSpace(f.Source) ? "" : " [" + f.Source + "]")))
                .ToList();

        public static string Marker(Provenance p, bool conflict) => conflict ? "⚠" : p switch
        {
            Provenance.Entered => "✎",
            Provenance.Derived => "=",
            Provenance.Default => "○",
            Provenance.Linked => "⇄",
            _ => "",
        };

        /// <summary>Tooltip of a marker.</summary>
        public static string MarkerTip(Provenance p, bool conflict, Lang lang)
        {
            var de = lang == Lang.De;
            if (conflict) return de ? "Widerspruch: die eingegebenen Werte passen nicht zusammen" : "Conflict: the entered values do not agree";
            return p switch
            {
                Provenance.Entered => de ? "Eingegeben" : "Entered",
                Provenance.Derived => de ? "Berechnet" : "Derived",
                Provenance.Default => de ? "Vorgabewert, weicht jedem eingegebenen Wert" : "Default, gives way to any entered value",
                Provenance.Linked => de ? "Vom verbundenen Element übernommen" : "Taken over from the connected element",
                _ => de ? "Noch unbekannt" : "Not known yet",
            };
        }

        public static ValueCardModel Build(ValueTable table, string objectId, string title, ValueLevel level, Lang lang, bool showsLabel)
        {
            var de = lang == Lang.De;
            var digits = ValueLevelSetting.Digits(level);
            var explain = level != ValueLevel.QuickSketch;
            var conflicts = table.ConflictsOf(objectId);
            var rows = new List<ValueRow>();
            foreach (var v in table.Of(objectId))
            {
                var display = v.Value is double d ? ValueFormat.Format(d, table.UnitOf(v), lang, digits) : "";
                var field = v.Provenance == Provenance.Entered && v.Text is { } t ? t.Trim() : display;
                // A stored text that could not be read stays visible so it can be corrected.
                if (v.Value is null && v.Text is { } bad && bad.Trim().Length > 0) field = bad.Trim();
                string? formula = null;
                if (explain && v.Via is not null && v.Value is not null)
                {
                    var steps = table.Explain(objectId, v.Def.Key, lang, digits);
                    formula = steps.Count > 0 ? steps[steps.Count - 1].Text : table.FormulaOf(v, lang);
                    if (level == ValueLevel.Engineer && v.Via.Relation.Source is { } src) formula += "  [" + src + "]";
                }
                string? problem = v.Error?.Get(lang);
                var own = conflicts.FirstOrDefault(c => c.Var.ObjectId == objectId && c.Var.Key == v.Def.Key);
                if (own is not null)
                    problem = (de ? "Aus " : "From ") + table.Formula(own.Relation, lang) + ": " + ValueFormat.Format(own.Implied, table.UnitOf(v), lang, digits);
                rows.Add(new ValueRow(v.Def.Key, v.Def.Symbol.Get(lang), v.Def.Name.Get(lang), v.Provenance)
                {
                    Marker = Marker(v.Provenance, v.InConflict),
                    Field = field,
                    Unit = table.UnitOf(v).Symbol,
                    Formula = formula,
                    Problem = problem,
                    Conflict = v.InConflict,
                    CanClear = v.Provenance == Provenance.Entered || v.Error is not null,
                    CanDrag = level == ValueLevel.Explore && v.Value is double x && x > 0 && !v.Def.Kind.IsText,
                    ReadOnly = v.Def.Kind.IsText,
                    Si = v.Value,
                    Display = display,
                });
            }
            var conflictLines = conflicts.Select(c =>
            {
                var cv = table.Get(c.Var.ObjectId, c.Var.Key);
                var unit = cv is null ? null : table.UnitOf(cv);
                string F(double x) => unit is null ? ValueFormat.Number(x, lang, digits) : ValueFormat.Format(x, unit, lang, digits);
                var sym = cv?.Def.Symbol.Get(lang) ?? c.Var.Key;
                var involved = string.Join(", ", c.Involved.Where(r => r.ObjectId == objectId).Select(r => table.Get(r.ObjectId, r.Key)?.Def.Symbol.Get(lang) ?? r.Key));
                return "⚠ " + table.Formula(c.Relation, lang) + ": " + sym + " = " + F(c.Value) + " ≠ " + F(c.Implied)
                    + (involved.Length > 0 ? (de ? " · eingegeben: " : " · entered: ") + involved : "");
            }).ToList();
            var state = table.Status(objectId);
            return new ValueCardModel(objectId, title, level, rows, state, StatusLine(table, objectId, state, rows, lang), conflictLines, showsLabel);
        }

        /// <summary>The status line: ✓ consistent, conflict advice, what is missing, or how to start.</summary>
        internal static string StatusLine(ValueTable table, string objectId, ValueStatus state, IReadOnlyList<ValueRow> rows, Lang lang)
        {
            var de = lang == Lang.De;
            string Sym(string key) => rows.FirstOrDefault(r => r.Key == key)?.Symbol ?? key;
            switch (state)
            {
                case ValueStatus.Checked: return de ? "✓ Alle Werte passen zusammen" : "✓ All values agree";
                case ValueStatus.Conflict: return de ? "⚠ Widerspruch – einen der eingegebenen Werte löschen" : "⚠ Conflict – clear one of the entered values";
                case ValueStatus.Determined: return "";
                case ValueStatus.Empty: return de ? "Bekannte Werte eingeben – der Rest folgt" : "Enter what you know – the rest follows";
            }
            var missing = table.Missing(objectId);
            if (missing.Count == 0) return de ? "Nicht bestimmbar" : "Not determined";
            var options = missing.Select(m => string.Join(de ? " und " : " and ", m.Select(Sym))).ToList();
            var or = de ? " oder " : " or ";
            var list = options.Count == 1 ? options[0] : string.Join(", ", options.Take(options.Count - 1)) + or + options.Last();
            return missing[0].Count == 1
                ? (de ? "Noch 1 Wert: " : "1 more value: ") + list
                : (de ? $"Noch {missing[0].Count} Werte, z. B. " : $"{missing[0].Count} more values, e.g. ") + options[0];
        }

        /// <summary>The pie entry label of a row: "Voltage U … 230 V" (entered or unknown, typed) or "Power P = 460 W" (derived).</summary>
        public static string PieLabel(ValueRow r)
        {
            var head = r.Name + " " + r.Symbol;
            if (r.Si is null) return head + " …";
            return r.Provenance == Provenance.Entered ? head + " … " + r.Field : head + " = " + r.Display;
        }

        /// <summary>The card as text lines (UI tests through the automation object, logs): one line per row, then status and conflicts.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            foreach (var r in Rows)
            {
                var line = r.Key + "|" + r.Provenance.ToString().ToLowerInvariant() + "|" + r.Field + (r.Conflict ? "|conflict" : "");
                if (r.Formula is not null) line += "|" + r.Formula;
                lines.Add(line);
            }
            lines.Add("status|" + State.ToString().ToLowerInvariant() + "|" + Status);
            lines.AddRange(Conflicts.Select(c => "conflict|" + c));
            lines.Add("label|" + (ShowsLabel ? "on" : "off"));
            return string.Join("\n", lines);
        }
    }
}
