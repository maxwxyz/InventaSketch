using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Calc 5 – characteristic curves of elements for linked charts (docs/en/design/engineering-calculations.md,
    // sections 1.10, 5 and 14). A type that implements ICharacteristics computes named curve sets (pump and system curve,
    // torque–speed, PV I–V and P–V, Bode plot …) from its solved values. A chart stores a link "<element id>#<set key>" in
    // its parameter "link" and renders the set through the render context; the element lists the chart as a dependent, so
    // hosts redraw it with every change of the element. Operating points carry the values they write back, so dragging a
    // point changes the element (one document change: one undo step for chart and element).
    // Modules never depend on each other: the Charts module reads curve sets only through this contract.
    // =============================================================================================

    /// <summary>An axis of a curve set: label, display unit (the numbers of the curves are in this unit), scale and range.</summary>
    public sealed record CharacteristicAxis(I18nText Label, string Unit)
    {
        public bool Log { get; init; }
        public double? Min { get; init; }
        public double? Max { get; init; }
    }

    /// <summary>How a curve is drawn: the element's own curve, a further curve of it, a load or system curve (dashed), a helper (dotted).</summary>
    public enum CurveLook { Main, Second, Load, Helper }

    /// <summary>A named curve: points in the units of the set's axes, on panel <see cref="Panel"/> (0 = top).</summary>
    public sealed record CharacteristicCurve(string Key, I18nText Name, IReadOnlyList<Vec2> Points)
    {
        public int Panel { get; init; }
        public CurveLook Look { get; init; }
    }

    /// <summary>How an operating point can be dragged: not at all, along x (the y value follows the curve) or along y.</summary>
    public enum PointMotion { None, AlongX, AlongY }

    /// <summary>
    /// A value written back into an element when a point is dragged: a variable (<see cref="Key"/>, SI value, entered like a
    /// dragged value: entered values that would over-determine the element give way, see <see cref="ValueDrag"/>) or, with
    /// <see cref="Parameter"/>, a text parameter stored as "value unit" in <see cref="Unit"/> (curve data such as H₀).
    /// </summary>
    public sealed record ValueWrite(string ObjectId, string Key, double Value)
    {
        public bool Parameter { get; init; }
        /// <summary>Display unit of a parameter write (required with <see cref="Parameter"/>).</summary>
        public Unit? Unit { get; init; }
    }

    /// <summary>
    /// A marked point of a set (operating point, MPP, design point): position in axis units, panel, the text shown, and how
    /// it is dragged: <see cref="Write"/> receives the new coordinate along <see cref="Motion"/> (axis units) and returns the
    /// values to store.
    /// </summary>
    public sealed record CharacteristicPoint(string Key, I18nText Name, double X, double Y)
    {
        public int Panel { get; init; }
        /// <summary>Text next to the marker or in the legend ("Operating point 70 m³/h, 20.4 m"); null: <see cref="Name"/>.</summary>
        public I18nText? Label { get; init; }
        /// <summary>Open marker (a design point) instead of a filled one.</summary>
        public bool Open { get; init; }
        public PointMotion Motion { get; init; }
        public Func<double, IReadOnlyList<ValueWrite>>? Write { get; init; }

        /// <summary>True if the point has a grip (it can be dragged and writes values).</summary>
        public bool Draggable => Motion != PointMotion.None && Write is not null;
    }

    /// <summary>A band between two x values across the panels (the inverter's MPP window).</summary>
    public sealed record CharacteristicBand(string Key, I18nText Name, double From, double To)
    {
        /// <summary>Panel index, or -1 for all panels.</summary>
        public int Panel { get; init; } = -1;
    }

    /// <summary>A limit line: vertical at x = <see cref="Value"/> (default) or horizontal at y.</summary>
    public sealed record CharacteristicLine(string Key, I18nText Name, double Value)
    {
        public bool Vertical { get; init; } = true;
        /// <summary>Panel index, or -1 for all panels (vertical lines).</summary>
        public int Panel { get; init; } = -1;
    }

    /// <summary>
    /// A curve set computed from an element's values: x axis, one y axis per panel, curves, points, bands, limit lines and
    /// notes (assumed defaults, "curves do not intersect" …).
    /// </summary>
    public sealed class CharacteristicSet
    {
        public CharacteristicSet(string key, I18nText name, CharacteristicAxis x, params CharacteristicAxis[] panels)
        {
            if (panels.Length == 0) throw new ArgumentException("At least one panel is required.", nameof(panels));
            Key = key; Name = name; X = x; Panels = panels;
        }

        public string Key { get; }
        public I18nText Name { get; }
        public CharacteristicAxis X { get; }
        public IReadOnlyList<CharacteristicAxis> Panels { get; }
        public List<CharacteristicCurve> Curves { get; } = new();
        public List<CharacteristicPoint> Points { get; } = new();
        public List<CharacteristicBand> Bands { get; } = new();
        public List<CharacteristicLine> Lines { get; } = new();
        public List<I18nText> Notes { get; } = new();

        public CharacteristicCurve? Curve(string key) => Curves.FirstOrDefault(c => c.Key == key);
        public CharacteristicPoint? Point(string key) => Points.FirstOrDefault(p => p.Key == key);
    }

    /// <summary>A curve set an element offers (listed without computing it): key and bilingual name.</summary>
    public sealed record CharacteristicInfo(string Key, I18nText Name);

    /// <summary>
    /// What a curve set is computed from: the element, its document and registry, and the solved values of its solver scope
    /// (the element and everything linked to it).
    /// </summary>
    public sealed class CharacteristicContext
    {
        public CharacteristicContext(SketchObject obj, SketchDocument doc, Registry registry, ValueTable table)
        {
            Object = obj; Doc = doc; Registry = registry; Table = table;
        }

        public SketchObject Object { get; }
        public SketchDocument Doc { get; }
        public Registry Registry { get; }
        public ValueTable Table { get; }

        /// <summary>The solved value (SI) of a variable of the element, or null.</summary>
        public double? Value(string key) => Value(Object.Id, key);

        /// <summary>The solved value (SI) of a variable of any element in the scope, or null.</summary>
        public double? Value(string objectId, string key)
            => Table.Get(objectId, key)?.Value is double v && !double.IsNaN(v) && !double.IsInfinity(v) ? v : null;

        /// <summary>True if the element's value was entered (typed or dragged) rather than derived or defaulted.</summary>
        public bool Entered(string key) => Table.Get(Object.Id, key)?.Provenance == Provenance.Entered;

        /// <summary>
        /// A curve parameter of the element (text "40 m", "−12 °C", or a number in <paramref name="unit"/>) as an SI value of
        /// <paramref name="kind"/>; null when missing or not a value of the kind (read leniently, German rules).
        /// </summary>
        public double? Param(string key, QuantityKind kind, string? unit = null) => Characteristics.ReadParam(Object, key, kind, unit);

        /// <summary>The variable definition of the element's type, or null.</summary>
        public VarDef? Var(string key) => ValueModel.Variable(Registry.Type(Object.Type), key);
    }

    /// <summary>A type whose elements provide characteristic curves for linked charts (Calc 5).</summary>
    public interface ICharacteristics
    {
        /// <summary>The curve sets <paramref name="o"/> offers in its document (empty: none).</summary>
        IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx);

        /// <summary>The curve set <paramref name="key"/> computed from the solved values; null if the element does not offer it.</summary>
        CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c);
    }

    /// <summary>
    /// A type whose drag handles change the document beyond the object itself (an operating point of a linked chart writes
    /// a value into the linked element). Hosts call <see cref="DocumentHandles.Move"/> instead of
    /// <see cref="ObjectType.MoveHandle"/> and apply the result as one undo step.
    /// </summary>
    public interface IDocumentHandles
    {
        /// <summary>The document with handle <paramref name="key"/> of <paramref name="o"/> dropped at <paramref name="pagePos"/>; <paramref name="doc"/> if nothing changes.</summary>
        SketchDocument MoveHandleInDocument(SketchDocument doc, SketchObject o, string key, Vec2 pagePos, RenderContext ctx);
    }

    /// <summary>Document-level handle moves (<see cref="IDocumentHandles"/>), falling back to <see cref="ObjectType.MoveHandle"/>.</summary>
    public static class DocumentHandles
    {
        /// <summary>
        /// The document after dropping handle <paramref name="key"/> of object <paramref name="id"/> at <paramref name="pagePos"/>
        /// (page mm): a document-level move when the type offers one, otherwise the object's own move. Returns
        /// <paramref name="doc"/> unchanged when nothing changes (no undo step).
        /// </summary>
        public static SketchDocument Move(Registry registry, SketchDocument doc, string id, string key, Vec2 pagePos, Lang lang)
        {
            var o = doc.Get(id);
            var type = o is null ? null : registry.Type(o.Type);
            if (o is null || type is null || o.Locked) return doc;
            var ctx = new RenderContext(doc, lang).For(registry);
            if (type is IDocumentHandles dh)
            {
                var result = dh.MoveHandleInDocument(doc, o, key, pagePos, ctx);
                if (!ReferenceEquals(result, doc)) return result;
            }
            var moved = registry.MoveHandle(o, key, pagePos, ctx);
            return ReferenceEquals(moved, o) ? doc : Edits.Replace(doc, moved);
        }
    }

    /// <summary>Links between charts and elements, resolving curve sets and writing values back.</summary>
    public static class Characteristics
    {
        /// <summary>Type id of the linked characteristic chart (Charts module); "Insert characteristic chart" creates one.</summary>
        public const string ChartTypeId = "chart.linked";

        /// <summary>The chart parameter holding the link "&lt;element id&gt;#&lt;set key&gt;".</summary>
        public const string LinkParam = "link";

        /// <summary>Choice key of the element's "Characteristic chart ▸" menu.</summary>
        public const string InsertChoice = "values.chart";

        private const char Sep = '#';

        /// <summary>The link text of set <paramref name="setKey"/> of element <paramref name="objectId"/>.</summary>
        public static string Link(string objectId, string setKey) => objectId + Sep + setKey;

        /// <summary>Splits a link text into the element id and the set key.</summary>
        public static bool TryParseLink(string? link, out string objectId, out string setKey)
        {
            objectId = ""; setKey = "";
            if (string.IsNullOrWhiteSpace(link)) return false;
            var i = link!.LastIndexOf(Sep);
            if (i <= 0 || i >= link.Length - 1) return false;
            objectId = link.Substring(0, i).Trim();
            setKey = link.Substring(i + 1).Trim();
            return objectId.Length > 0 && setKey.Length > 0;
        }

        /// <summary>The link stored on a chart (null if none).</summary>
        public static string? LinkOf(SketchObject chart) => chart.Params.TryGetValue(LinkParam, out var v) && v.Text is { Length: > 0 } t ? t : null;

        /// <summary>The curve sets <paramref name="o"/> offers (empty for types without characteristics).</summary>
        public static IReadOnlyList<CharacteristicInfo> SetsOf(Registry? registry, SketchObject o, RenderContext ctx)
        {
            if ((registry?.Type(o.Type) ?? null) is not ICharacteristics c) return Array.Empty<CharacteristicInfo>();
            try { return c.CharacteristicSets(o, ctx); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or FormatException) { return Array.Empty<CharacteristicInfo>(); }
        }

        /// <summary>Ids of the charts linked to <paramref name="objectId"/> (its dependents: hosts redraw them when it changes).</summary>
        public static IReadOnlyList<string> LinkedCharts(SketchDocument doc, string objectId)
        {
            var prefix = objectId + Sep;
            List<string>? list = null;
            foreach (var o in doc.Objects.Values)
                if (o.Id != objectId && o.Params.TryGetValue(LinkParam, out var v) && v.Text is { } t && t.StartsWith(prefix, StringComparison.Ordinal))
                    (list ??= new List<string>()).Add(o.Id);
            return list is null ? Array.Empty<string>() : list;
        }

        // ------------------------------------------------------------------ resolving

        private static readonly ConditionalWeakTable<SketchDocument, Dictionary<string, CharacteristicSet?>> Cache = new();

        /// <summary>
        /// The curve set a link points to, computed from the solved values of the linked element in <paramref name="doc"/>
        /// (cached per document instance and registry); null if the element is missing or offers no such set.
        /// </summary>
        public static CharacteristicSet? Resolve(Registry registry, SketchDocument doc, string? link)
        {
            if (!TryParseLink(link, out var id, out var key)) return null;
            var o = doc.Get(id);
            if (o is null || registry.Type(o.Type) is not ICharacteristics provider) return null;
            var cacheKey = RuntimeHelpers.GetHashCode(registry).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + id + Sep + key;
            var map = Cache.GetValue(doc, _ => new Dictionary<string, CharacteristicSet?>(StringComparer.Ordinal));
            lock (map)
                if (map.TryGetValue(cacheKey, out var cached)) return cached;
            CharacteristicSet? set;
            try
            {
                var table = ValueSolver.Solve(doc, registry, new[] { id }, new ValueSolveOptions { BudgetMs = 200 });
                set = provider.Characteristic(o, key, new CharacteristicContext(o, doc, registry, table));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or FormatException or KeyNotFoundException)
            {
                set = null;
            }
            lock (map) map[cacheKey] = set;
            return set;
        }

        // ------------------------------------------------------------------ writing back

        /// <summary>
        /// The document with <paramref name="writes"/> applied in order: variables are entered like a dragged value (entered
        /// values that would over-determine the element give way, <see cref="ValueDrag"/>), parameters are stored as formatted
        /// text. Unknown objects and keys are skipped. One document change: the caller makes one undo step of it.
        /// </summary>
        public static SketchDocument Apply(Registry registry, SketchDocument doc, IEnumerable<ValueWrite> writes, Lang lang)
        {
            foreach (var w in writes)
            {
                if (double.IsNaN(w.Value) || double.IsInfinity(w.Value)) continue;
                var o = doc.Get(w.ObjectId);
                if (o is null || registry.Type(o.Type) is null) continue;
                try
                {
                    if (w.Parameter)
                    {
                        if (w.Unit is null) continue;
                        doc = Edits.Replace(doc, registry.SetParam(o, w.Key, ValueFormat.Format(w.Value, w.Unit, lang)));
                        continue;
                    }
                    var plan = ValueDrag.Plan(doc, registry, w.ObjectId, w.Key, w.Value, lang);
                    doc = Edits.Replace(doc, ValueDrag.Apply(registry, o, w.Key, w.Value, plan.Released, lang));
                }
                catch (ArgumentException)
                {
                    // A key the element does not have: nothing to write.
                }
            }
            return doc;
        }

        /// <summary>A text or number parameter read as an SI value of <paramref name="kind"/> (numbers in <paramref name="unit"/>); null if missing or invalid.</summary>
        public static double? ReadParam(SketchObject o, string key, QuantityKind kind, string? unit = null)
        {
            if (!o.Params.TryGetValue(key, out var p)) return null;
            if (p.Number is double n)
            {
                if (double.IsNaN(n) || double.IsInfinity(n)) return null;
                var u = unit is null ? kind.DefaultUnit : UnitCatalog.Find(unit, kind) ?? kind.DefaultUnit;
                return u.ToSi(n);
            }
            if (p.Text is not { } text || text.Trim().Length == 0) return null;
            return ValueParser.TryParse(text, kind, out var parsed, out _, Lang.De, unit) && parsed is not null
                && !double.IsNaN(parsed.Value) && !double.IsInfinity(parsed.Value) ? parsed.Value : null;
        }

        /// <summary>The unit <paramref name="spelling"/> of <paramref name="kind"/> (the kind's default unit if unknown).</summary>
        public static Unit UnitOf(QuantityKind kind, string? spelling) => spelling is null ? kind.DefaultUnit : UnitCatalog.Find(spelling, kind) ?? kind.DefaultUnit;

        // ------------------------------------------------------------------ actions

        private static readonly ActionChoice InsertMenu = new(InsertChoice, new I18nText("Characteristic chart", "Kennlinie")) { IsMenu = true, BarIcon = IconIds.Chart };

        /// <summary>
        /// "Characteristic chart ▸" of an element: one command per curve set it offers, each adding a chart linked to that set
        /// right of the element (one undo step). Empty when the element offers no set or no chart type is registered.
        /// </summary>
        public static IEnumerable<ContextAction> InsertActions(ObjectType type, SketchObject o, RenderContext ctx)
        {
            var registry = ctx.Registry ?? type.Owner;
            if (registry?.Type(ChartTypeId) is not { } chartType) return Array.Empty<ContextAction>();
            var sets = SetsOf(registry, o, ctx);
            if (sets.Count == 0) return Array.Empty<ContextAction>();
            var list = new List<ContextAction>();
            foreach (var s in sets)
            {
                var info = s;
                list.Add(ContextAction.InDocument(InsertChoice + ActionChoices.Separator + info.Key, info.Name, IconIds.Option,
                    (doc, x, c) => InsertChart(doc, x, info.Key, chartType)) with { Choice = InsertMenu, Group = "choice:" + InsertChoice });
            }
            return list;
        }

        /// <summary>The document with a new chart linked to set <paramref name="setKey"/> of <paramref name="element"/>, right of it on its page.</summary>
        public static SketchDocument InsertChart(SketchDocument doc, SketchObject element, string setKey, ObjectType chartType)
        {
            var page = doc.PageOf(element.Id) ?? doc.Pages.FirstOrDefault();
            if (page is null) return doc;
            var size = chartType.DefaultSize;
            var b = element.IsPointGeometry ? Rect.FromPoints(doc.ResolvePoints(element)) : element.Bounds();
            var at = new Vec2(b.Right + 10 + size.W / 2, b.Y + b.H / 2);
            var chart = chartType.Create(at).WithParam(LinkParam, Link(element.Id, setKey));
            return Edits.Add(doc, page.Id, chart);
        }
    }
}
