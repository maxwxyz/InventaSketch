using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Calc 2 – element values in the hosts (docs/en/design/engineering-calculations.md, sections 1.5 and 7):
    // the values label on the drawing (part "values"), entering and clearing values on an object, and the live drag of a
    // value. Host-neutral, so the COM, web and LibreOffice hosts behave the same.
    // =============================================================================================

    /// <summary>
    /// The values label of an element ("R = 4.7 kΩ, P = 0.25 W"): a real part of the element (part key <see cref="Part"/>),
    /// drawn below the element by <see cref="Registry.RenderLocal"/> for every type with values (<see cref="IValueType"/>)
    /// while the object's parameter <see cref="ShowParam"/> is true. It shows the known card values, or the variables
    /// chosen in <see cref="KeysParam"/> (comma-separated keys). The label is content: it shows at every value level.
    /// </summary>
    public static class ValueLabels
    {
        /// <summary>Part key of the values label.</summary>
        public const string Part = "values";
        /// <summary>Boolean parameter: draw the values label.</summary>
        public const string ShowParam = "showValues";
        /// <summary>Text parameter: the variables shown, comma-separated keys ("R,P"); missing = the known card values.</summary>
        public const string KeysParam = "valueLabelKeys";
        /// <summary>Text size of the label in mm (as the designator/value labels of symbols).</summary>
        public const double TextSize = 2.5;

        /// <summary>
        /// Labels up to this width (mm) stay on one line even below a narrower box: a small symbol keeps the usual one-line
        /// label ("R = 115 Ω, U = 230 V, I = 2 A, P = 460 W"); only longer labels wrap to the box width or this width.
        /// </summary>
        public const double MinWrapWidth = 60;

        /// <summary>The parameter definition of the switch, for types that list it in their inspector.</summary>
        public static readonly ParamDef ShowValues = new(ShowParam, new I18nText("Show values", "Werte zeigen"), ParamKind.Boolean) { Default = false };

        public static readonly I18nText PartLabel = new("Values", "Werte");

        /// <summary>True if the object asks for the values label.</summary>
        public static bool Shows(SketchObject o) => o.Flag(ShowParam, false);

        /// <summary>The explicitly chosen keys, or null (the known card values).</summary>
        public static IReadOnlyList<string>? ChosenKeys(SketchObject o)
        {
            var text = o.Str(KeysParam, "");
            if (text.Trim().Length == 0) return null;
            return text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
        }

        /// <summary>The object with the label switched on or off (the chosen keys stay).</summary>
        public static SketchObject WithShown(SketchObject o, bool on)
            => on ? o.WithParam(ShowParam, true) : Without(o, ShowParam);

        /// <summary>
        /// The object with variable <paramref name="key"/> added to or removed from the label. The first choice turns the
        /// default list (<paramref name="shownNow"/>) into an explicit one; the label is switched on when a key is added.
        /// </summary>
        public static SketchObject WithKey(SketchObject o, IReadOnlyList<VarDef> vars, IEnumerable<string> shownNow, string key, bool on)
        {
            var set = new HashSet<string>(ChosenKeys(o) ?? shownNow, StringComparer.Ordinal);
            if (on) set.Add(key); else set.Remove(key);
            var ordered = vars.Select(v => v.Key).Where(set.Contains).ToList();
            var next = o.WithParam(KeysParam, string.Join(",", ordered));
            return on ? next.WithParam(ShowParam, true) : next;
        }

        private static SketchObject Without(SketchObject o, string key)
            => o.Params.ContainsKey(key) ? o with { Params = o.Params.Where(kv => kv.Key != key).ToDictionary(kv => kv.Key, kv => kv.Value) } : o;

        /// <summary>
        /// The values the label shows, in variable order: the chosen keys that are known, else the known card values without
        /// the ones the type draws itself when entered (<see cref="VarDef.OnTypeLabel"/>).
        /// </summary>
        public static IReadOnlyList<VarValue> Shown(SketchObject o, ValueTable table)
        {
            var vars = table.Of(o.Id);
            var chosen = ChosenKeys(o);
            if (chosen is not null) return vars.Where(v => v.Value is not null && chosen.Contains(v.Def.Key)).ToList();
            return vars.Where(v => v.Value is not null && v.Def.Relevance == VarRelevance.Card && !(v.Def.OnTypeLabel && v.Provenance == Provenance.Entered)).ToList();
        }

        /// <summary>
        /// How one value appears on the label: an entered text with a unit or letter code as typed ("4k7", "230 V"), else
        /// formatted with significant digits and the best SI prefix ("115 Ω").
        /// </summary>
        public static string Display(VarValue v, Lang lang, int digits = 3, ValueTable? table = null)
        {
            if (v.Value is not double d) return "?";
            if (v.Provenance == Provenance.Entered && v.Text is { } t && t.Trim().Any(char.IsLetter) && v.Tolerance is null) return t.Trim();
            return ValueFormat.Format(d, table?.UnitOf(v) ?? v.Def.DisplayUnit, lang, digits);
        }

        /// <summary>The label text: "R = 4.7 kΩ, P = 0.25 W"; empty when nothing is known.</summary>
        public static string Text(SketchObject o, ValueTable table, Lang lang, int digits = 3)
            => string.Join(", ", Entries(o, table, lang, digits));

        /// <summary>The entries of the label, one per value: "R = 4.7 kΩ".</summary>
        public static IReadOnlyList<string> Entries(SketchObject o, ValueTable table, Lang lang, int digits = 3)
            => Shown(o, table).Select(v => v.Def.Symbol.Get(lang) + " = " + Display(v, lang, digits, table)).ToList();

        /// <summary>Line pitch of a wrapped label as a multiple of the text size.</summary>
        public const double LineSpacing = 1.2;

        /// <summary>Estimated width of a label line in mm (the estimate of <see cref="Prim.Bounds"/> for text).</summary>
        public static double Width(string line) => Math.Max(1, line.Length) * TextSize * 0.55;

        /// <summary>
        /// The label lines for an available width (mm): one line "R = 4.7 kΩ, P = 0.25 W" while it fits, else the entries
        /// packed greedily into lines no wider than <paramref name="maxWidth"/> (an entry wider than that gets a line of its
        /// own; entries are never split). A non-positive or infinite width keeps one line.
        /// </summary>
        public static IReadOnlyList<string> Wrap(IReadOnlyList<string> entries, double maxWidth)
        {
            if (entries.Count == 0) return Array.Empty<string>();
            var one = string.Join(", ", entries);
            if (!(maxWidth > 0) || double.IsInfinity(maxWidth) || Width(one) <= maxWidth + 1e-9) return new[] { one };
            var lines = new List<string>();
            var current = "";
            foreach (var e in entries)
            {
                if (current.Length == 0) { current = e; continue; }
                var next = current + ", " + e;
                if (Width(next) <= maxWidth + 1e-9) current = next;
                else { lines.Add(current); current = e; }
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

        /// <summary>Solves the values of <paramref name="o"/> as it is now (also when the document holds an older copy of it).</summary>
        public static ValueTable SolveOne(Registry registry, SketchObject o, SketchDocument doc, Lang lang, ValueSolveOptions? options = null)
        {
            if (!ReferenceEquals(doc.Get(o.Id), o))
            {
                var objects = doc.Objects.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                objects[o.Id] = o;
                doc = doc with { Objects = objects };
            }
            return ValueSolver.Solve(doc, registry, new[] { o.Id }, options ?? new ValueSolveOptions { BudgetMs = 20 }, lang);
        }

        /// <summary>
        /// <paramref name="local"/> with the values label appended when the object asks for it: centred below what the
        /// element draws (its own labels included), upright on the page whatever the element's rotation or mirroring.
        /// Never throws; without values nothing is added.
        /// </summary>
        public static IReadOnlyList<Primitive> Append(Registry registry, SketchObject o, RenderContext ctx, IReadOnlyList<Primitive> local)
        {
            if (!Shows(o) || registry.Type(o.Type) is not IValueType || local.Count == 0) return local;
            try
            {
                var table = SolveOne(registry, o, ctx.Doc, ctx.Lang);
                var entries = Entries(o, table, ctx.Lang);
                if (entries.Count == 0) return local;
                var label = Place(o, local, entries);
                if (label.Count == 0) return local;
                var list = new List<Primitive>(local.Count + label.Count);
                list.AddRange(local);
                list.AddRange(label);
                return list;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or ArithmeticException or NotSupportedException)
            {
                return local;
            }
        }

        /// <summary>
        /// The label primitives (tag <see cref="Part"/>) in the object's local coordinates, below the drawn bounds on the page:
        /// one line, or for a box element whose label is wider than its box, its drawing on the page and <see cref="MinWrapWidth"/>, the entries
        /// stacked in lines no wider than that (<see cref="Wrap"/>), each centred. Point elements (lines, routes) keep one line.
        /// </summary>
        internal static IReadOnlyList<TextPrim> Place(SketchObject o, IReadOnlyList<Primitive> local, IReadOnlyList<string> entries)
        {
            var m = o.IsPointGeometry ? Matrix2D.Identity : o.Matrix();
            var page = Prim.Bounds(o.IsPointGeometry ? local : local.Select(p => Prim.Transform(p, m)));
            if (page is not Rect b || entries.Count == 0) return Array.Empty<TextPrim>();
            var lines = Wrap(entries, o.IsPointGeometry ? double.PositiveInfinity : Math.Max(MinWrapWidth, Math.Max(b.W, BoxWidthOnPage(o, m))));
            const double gap = 1.0;
            var mirrored = m.Determinant < 0;
            var rotation = o.IsPointGeometry || mirrored ? 0 : -m.RotationDegrees;
            if (Math.Abs(rotation) < 1e-9) rotation = 0;
            var inverse = o.IsPointGeometry ? Matrix2D.Identity : m.Invert();
            var result = new List<TextPrim>(lines.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                var at = new Vec2(b.X + b.W / 2, b.Bottom + gap + TextSize / 2 + i * TextSize * LineSpacing);
                var pos = o.IsPointGeometry ? at : inverse.Apply(at);
                result.Add(new TextPrim(pos, lines[i], new TextStyle { Size = TextSize, Anchor = TextAnchor.Middle }, rotation) { Tag = Part });
            }
            return result;
        }

        /// <summary>Width on the page of the object's box (its rotated outline's horizontal extent).</summary>
        private static double BoxWidthOnPage(SketchObject o, Matrix2D m)
        {
            double w = o.Size.W / 2, h = o.Size.H / 2;
            var xs = new[] { m.Apply(new Vec2(-w, -h)).X, m.Apply(new Vec2(w, -h)).X, m.Apply(new Vec2(w, h)).X, m.Apply(new Vec2(-w, h)).X };
            return xs.Max() - xs.Min();
        }

        /// <summary>The part entry of the label while it is shown (selectable, styled and hidden like any other part).</summary>
        public static PartInfo? PartOf(Registry registry, SketchObject o)
            => Shows(o) && registry.Type(o.Type) is IValueType ? new PartInfo(Part, PartLabel, "label") : null;
    }

    /// <summary>
    /// Entering and clearing values on an object (the engine's <c>setValue</c>/<c>clearValue</c> semantics for hosts that
    /// edit in-process): the text is parsed for the variable's quantity (wrong kinds and unknown units throw a bilingual
    /// <see cref="FormatException"/>), stored as typed in text parameters or as a number in the stored unit; an empty
    /// text clears. The caller makes one undo step of the result.
    /// </summary>
    public static class ValueEdits
    {
        /// <summary>The object with <paramref name="text"/> entered for variable <paramref name="key"/>; throws for unknown keys and invalid text.</summary>
        public static SketchObject Set(Registry registry, SketchObject o, string key, string? text, Lang lang)
        {
            var type = registry.Type(o.Type);
            var v = ValueModel.Variable(type, key) ?? throw new ArgumentException($"'{o.Type}' has no value '{key}'.");
            var t = (text ?? "").Trim();
            if (t.Length == 0) return Clear(registry, o, key);
            var parsed = ValueParser.Parse(t, v.Kind, lang, v.Unit);
            return registry.SetParam(o, v.ParamKey, ValueModel.ToParam(type, o, v, parsed));
        }

        /// <summary>The object without the entered value of <paramref name="key"/> (unchanged if none is entered).</summary>
        public static SketchObject Clear(Registry registry, SketchObject o, string key)
        {
            var v = ValueModel.Variable(registry.Type(o.Type), key) ?? throw new ArgumentException($"'{o.Type}' has no value '{key}'.");
            return o.Params.ContainsKey(v.ParamKey) ? registry.SetParam(o, v.ParamKey, null) : o;
        }

        /// <summary>The object with an SI value stored for <paramref name="key"/> (a drag or slider: formatted text, or the number in the stored unit).</summary>
        public static SketchObject SetSi(Registry registry, SketchObject o, string key, double si, Lang lang, int digits = 3)
        {
            var type = registry.Type(o.Type);
            var v = ValueModel.Variable(type, key) ?? throw new ArgumentException($"'{o.Type}' has no value '{key}'.");
            return registry.SetParam(o, v.ParamKey, ValueModel.ToParam(type, o, v, si, lang, digits));
        }
    }

    /// <summary>A drag preview: the solved values while the dragged one is held, and the entered values that give way to it.</summary>
    public sealed record ValueDragPlan(ValueTable Table, IReadOnlyList<string> Released);

    /// <summary>
    /// Live drag of a value (Explore, section 1.5): the dragged variable is held at the new value and everything that
    /// depends on it follows. Entered values never change silently, but holding a third value over-determines an element
    /// whose two values are entered (U and I entered, R dragged): the entered values after it in the type's order give
    /// way, last first, until the element agrees again (dragging R with U and I entered releases I, so I and P follow).
    /// The released values are cleared together with storing the dragged one when the drag is released (one undo step).
    /// </summary>
    public static class ValueDrag
    {
        public static ValueDragPlan Plan(SketchDocument doc, Registry registry, string objectId, string key, double si, Lang lang)
        {
            var o = doc.Get(objectId) ?? throw new ArgumentException($"Unknown object '{objectId}'.");
            var vars = ValueModel.Variables(registry.Type(o.Type));
            if (vars.All(v => v.Key != key)) throw new ArgumentException($"'{o.Type}' has no value '{key}'.");
            var overrides = new Dictionary<VarRef, double> { [new VarRef(objectId, key)] = si };
            var released = new List<string>();
            var current = o;
            var table = ValueLabels.SolveOne(registry, current, doc, lang, new ValueSolveOptions { Overrides = overrides });
            for (var i = vars.Count - 1; i >= 0 && table.ConflictsOf(objectId).Count > 0; i--)
            {
                var v = vars[i];
                if (v.Key == key || table.Get(objectId, v.Key)?.Provenance != Provenance.Entered) continue;
                current = ValueEdits.Clear(registry, current, v.Key);
                released.Add(v.Key);
                table = ValueLabels.SolveOne(registry, current, doc, lang, new ValueSolveOptions { Overrides = overrides });
            }
            return new ValueDragPlan(table, released);
        }

        /// <summary>The object as stored when the drag is released: the dragged value entered, the released ones cleared.</summary>
        public static SketchObject Apply(Registry registry, SketchObject o, string key, double si, IEnumerable<string> released, Lang lang, int digits = 3)
        {
            var next = ValueEdits.SetSi(registry, o, key, si, lang, digits);
            foreach (var k in released) next = ValueEdits.Clear(registry, next, k);
            return next;
        }
    }
}
