using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Options of a solver pass.</summary>
    public sealed class ValueSolveOptions
    {
        /// <summary>Time budget in milliseconds; the pass stops (and reports <see cref="ValueTable.Truncated"/>) when exceeded.</summary>
        public double BudgetMs { get; init; } = 50;
        /// <summary>Use type defaults for variables still unknown after propagation.</summary>
        public bool UseDefaults { get; init; } = true;
        /// <summary>Values (SI) treated as entered instead of the stored ones: a live drag preview (no document change).</summary>
        public IReadOnlyDictionary<VarRef, double>? Overrides { get; init; }
        /// <summary>Upper bound for objects pulled into the scope through links.</summary>
        public int MaxObjects { get; init; } = 20000;
    }

    /// <summary>State of an element's values.</summary>
    public enum ValueStatus
    {
        /// <summary>No variable known.</summary>
        Empty,
        /// <summary>Some known, some missing (see <see cref="ValueTable.Missing"/>).</summary>
        Under,
        /// <summary>All known, no independent check.</summary>
        Determined,
        /// <summary>Over-determined and consistent (✓).</summary>
        Checked,
        /// <summary>At least one relation disagrees.</summary>
        Conflict,
    }

    /// <summary>A solved variable.</summary>
    public sealed class VarValue
    {
        public VarRef Ref { get; internal set; }
        public VarDef Def { get; internal set; } = null!;
        /// <summary>SI value, null when unknown.</summary>
        public double? Value { get; internal set; }
        public Provenance Provenance { get; internal set; }
        /// <summary>The stored text as typed (entered text parameters).</summary>
        public string? Text { get; internal set; }
        public Tolerance? Tolerance { get; internal set; }
        /// <summary>Parse error of a stored text (the value then counts as not entered).</summary>
        public I18nText? Error { get; internal set; }
        /// <summary>The relation that derived or linked the value.</summary>
        public BoundRelation? Via { get; internal set; }
        /// <summary>The variables the value was computed from.</summary>
        public IReadOnlyList<VarRef> Inputs { get; internal set; } = Array.Empty<VarRef>();
        public bool InConflict { get; internal set; }
        /// <summary>
        /// Range of the value from the tolerances it rests on (Calc 6): an entered "4k7 ±5 %" gives 4.465 … 4.935 kΩ, a value
        /// derived from it the range its relation gives at the corners of its inputs' ranges. Null without a tolerance.
        /// </summary>
        public (double Lo, double Hi)? Interval { get; internal set; }
    }

    /// <summary>
    /// A relation that disagrees: the value of <see cref="Var"/> and the value the relation implies from the others,
    /// with the entered values involved. The engine changes nothing; the user decides.
    /// </summary>
    public sealed record ValueConflict(BoundRelation Relation, VarRef Var, double Value, double Implied, IReadOnlyList<VarRef> Involved);

    /// <summary>One line of a step-by-step explanation: "R = U/I = 230 V/2.00 A = 115 Ω".</summary>
    public sealed record ExplainStep(VarRef Var, Provenance Provenance, string Symbol, string Symbolic, string Substituted, string Result, string Text, string? RelationId, string? Source);

    /// <summary>
    /// Result of a solver pass: value and trace per (object id, variable), conflicts, status. Free of side effects.
    /// </summary>
    public sealed class ValueTable
    {
        /// <summary>Settings of the solved document (unit preferences and muted checks, Calc 6).</summary>
        public DocumentSettings Settings { get; internal set; } = new();

        /// <summary>
        /// The unit <paramref name="v"/> is shown in: the drawing's preferred unit for its quantity kind (bar/MPa, min⁻¹/s⁻¹,
        /// kWh/MJ) if it has one, else the value's own display unit.
        /// </summary>
        public Unit UnitOf(VarValue v)
            => Settings.UnitPreferences is { } prefs && prefs.TryGetValue(v.Def.Kind.Id, out var symbol) && UnitCatalog.Find(symbol, v.Def.Kind) is { } u
                ? u : v.Def.DisplayUnit;

        internal readonly List<VarValue> Vars = new();
        internal readonly Dictionary<VarRef, int> Index = new();
        internal readonly List<BoundRelation> Relations = new();
        internal readonly HashSet<int> CheckedRelations = new();
        internal readonly Dictionary<string, List<int>> ByObject = new(StringComparer.Ordinal);
        internal readonly List<ValueConflict> ConflictList = new();
        internal readonly Dictionary<string, SketchObject> Objects = new(StringComparer.Ordinal);
        /// <summary>Objects involved in a passed independent check, and conflicts per involved object.</summary>
        internal readonly HashSet<string> CheckedObjects = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, List<ValueConflict>> ConflictsByObject = new(StringComparer.Ordinal);
        /// <summary>Relations per object (local ones and links that involve it).</summary>
        internal readonly Dictionary<string, List<int>> RelationsByObject = new(StringComparer.Ordinal);

        /// <summary>Objects of the scope with variables, in solver order.</summary>
        public IReadOnlyList<string> ObjectIds { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<ValueConflict> Conflicts => ConflictList;
        public double ElapsedMs { get; internal set; }
        /// <summary>The time budget ran out; values may be incomplete.</summary>
        public bool Truncated { get; internal set; }

        public VarValue? Get(string objectId, string key) => Index.TryGetValue(new VarRef(objectId, key), out var i) ? Vars[i] : null;

        public IReadOnlyList<VarValue> Of(string objectId) => ByObject.TryGetValue(objectId, out var l) ? l.Select(i => Vars[i]).ToList() : (IReadOnlyList<VarValue>)Array.Empty<VarValue>();

        public IReadOnlyList<ValueConflict> ConflictsOf(string objectId)
            => ConflictsByObject.TryGetValue(objectId, out var l) ? l : (IReadOnlyList<ValueConflict>)Array.Empty<ValueConflict>();

        public ValueStatus Status(string objectId)
        {
            var vars = Of(objectId);
            if (vars.Count == 0 || vars.All(v => v.Value is null)) return ValueStatus.Empty;
            if (ConflictsOf(objectId).Count > 0) return ValueStatus.Conflict;
            if (vars.Any(v => v.Value is null)) return ValueStatus.Under;
            return CheckedObjects.Contains(objectId) ? ValueStatus.Checked : ValueStatus.Determined;
        }

        /// <summary>
        /// The smallest sets of further variables of the element that would determine all of its variables (local
        /// relations only): "one more value: I, R or P" gives three sets of one. Empty when determined or nothing helps.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> Missing(string objectId)
        {
            var vars = Of(objectId);
            var unknown = vars.Where(v => v.Value is null).Select(v => v.Def.Key).ToList();
            if (unknown.Count == 0) return Array.Empty<IReadOnlyList<string>>();
            var local = (RelationsByObject.TryGetValue(objectId, out var rl) ? rl : new List<int>())
                .Select(i => Relations[i]).Where(r => r.LocalObject == objectId || r.Map.Values.All(v => v.ObjectId == objectId)).Select(r => r.Relation).ToList();
            var known = new HashSet<string>(vars.Where(v => v.Value is not null).Select(v => v.Def.Key));
            var text = new HashSet<string>(vars.Where(v => v.Def.Kind.IsText).Select(v => v.Def.Key));
            bool Closes(IEnumerable<string> extra)
            {
                var k = new HashSet<string>(known);
                foreach (var e in extra) k.Add(e);
                var progress = true;
                while (progress)
                {
                    progress = false;
                    foreach (var r in local)
                    {
                        var u = r.Vars.Where(x => !k.Contains(x)).ToList();
                        // As in the solver: a relation with a text-valued variable is solved only for that variable.
                        if (u.Count == 1 && r.CanSolve(u[0]) && (text.Contains(u[0]) || !r.Vars.Any(text.Contains))) { k.Add(u[0]); progress = true; }
                    }
                }
                return unknown.All(k.Contains);
            }
            var result = new List<IReadOnlyList<string>>();
            // Text-valued variables are never entered: they are not offered, only required.
            var candidates = vars.Where(v => v.Value is null && !v.Def.Kind.IsText).Select(v => v.Def.Key).ToList();
            for (var size = 1; size <= Math.Min(3, candidates.Count) && result.Count == 0; size++)
                foreach (var set in Combinations(candidates, size))
                    if (Closes(set)) result.Add(set);
            return result;
        }

        private static IEnumerable<IReadOnlyList<string>> Combinations(IReadOnlyList<string> items, int size, int start = 0)
        {
            if (size == 0) { yield return Array.Empty<string>(); yield break; }
            for (var i = start; i <= items.Count - size; i++)
                foreach (var rest in Combinations(items, size - 1, i + 1))
                {
                    var l = new List<string> { items[i] };
                    l.AddRange(rest);
                    yield return l;
                }
        }

        /// <summary>
        /// Step-by-step explanation of a value: one line per derived value of its chain, inputs first
        /// ("n₂ = n₁·z₁/z₂ = 1500 min⁻¹ · 18/54 = 500 min⁻¹"). Entered and default values give one line "R = 4k7".
        /// </summary>
        public IReadOnlyList<ExplainStep> Explain(string objectId, string key, Lang lang, int digits = 3)
        {
            var steps = new List<ExplainStep>();
            var seen = new HashSet<VarRef>();
            void Visit(VarRef r, int depth)
            {
                if (depth > 64 || !seen.Add(r) || !Index.TryGetValue(r, out var i)) return;
                var v = Vars[i];
                if (v.Value is null) return;
                foreach (var input in v.Inputs) if (Vars[Index[input]].Via is not null) Visit(input, depth + 1);
                var sym = v.Def.Symbol.Get(lang);
                var result = Show(v, lang, digits, false);
                if (v.Via is null)
                {
                    var shown = v.Provenance == Provenance.Entered && v.Text is not null ? v.Text.Trim() : result;
                    steps.Add(new ExplainStep(r, v.Provenance, sym, sym, shown, result, sym + " = " + shown, null, null));
                    return;
                }
                var rel = v.Via.Relation;
                var relKey = v.Via.Map.First(kv => kv.Value == r).Key;
                var expr = rel.Expression(relKey);
                string symbolic, substituted;
                if (expr is not null)
                {
                    symbolic = Substitute(expr, v.Via, x => Get(x)?.Def.Symbol.Get(lang) ?? x.Key);
                    substituted = Substitute(expr, v.Via, x => Get(x) is { Value: not null } w ? Show(w, lang, digits, true) : "?");
                }
                else
                {
                    symbolic = Substitute(rel.Formula, v.Via, x => Get(x)?.Def.Symbol.Get(lang) ?? x.Key);
                    substituted = lang == Lang.De ? "numerisch gelöst" : "solved numerically";
                }
                var text = expr is null
                    ? sym + " = " + result + " (" + substituted + ": " + symbolic + ")"
                    : symbolic == substituted ? sym + " = " + symbolic + " = " + result
                    : sym + " = " + symbolic + " = " + substituted + " = " + result;
                steps.Add(new ExplainStep(r, v.Provenance, sym, symbolic, substituted, result, text, rel.Id, rel.Source));
            }
            Visit(new VarRef(objectId, key), 0);
            return steps;
        }

        /// <summary>The relation's equation with the variables' symbols ("U = R·I").</summary>
        public string Formula(BoundRelation b, Lang lang) => Substitute(b.Relation.Formula, b, x => SymbolIn(b, x, lang));

        /// <summary>How a derived or linked value is computed ("R = U/I"); null for entered, default and unknown values.</summary>
        public string? FormulaOf(VarValue v, Lang lang)
        {
            if (v.Via is null) return null;
            var key = v.Via.Map.First(kv => kv.Value == v.Ref).Key;
            var expr = v.Via.Relation.Expression(key);
            return SymbolIn(v.Via, v.Ref, lang) + " = " + (expr is null ? "f(" + Formula(v.Via, lang) + ")" : Substitute(expr, v.Via, x => SymbolIn(v.Via, x, lang)));
        }

        /// <summary>
        /// The symbol of <paramref name="r"/> in relation <paramref name="b"/>: plain within one element ("U = R·I"); in a
        /// relation over several elements qualified, so "U = I·(R + R)" reads "U = I·(R1 + R2)": the designator when it
        /// starts with the symbol ("R1" for R), else symbol and designator ("U_R1"), and without a designator an index in
        /// the order the elements appear (R₁, R₂).
        /// </summary>
        private string SymbolIn(BoundRelation b, VarRef r, Lang lang)
        {
            var symbol = Get(r)?.Def.Symbol.Get(lang) ?? r.Key;
            var objects = b.Map.Values.Select(v => v.ObjectId).Distinct().ToList();
            if (objects.Count < 2) return symbol;
            var designator = Objects.TryGetValue(r.ObjectId, out var o) ? o.Str("ref", "").Trim() : "";
            if (designator.Length > 0)
                return designator.StartsWith(symbol, StringComparison.Ordinal) && designator.Length > symbol.Length && !char.IsLetter(designator[symbol.Length])
                    ? designator : symbol + "_" + designator;
            var index = objects.IndexOf(r.ObjectId) + 1;
            return symbol + Subscript(index);
        }

        private static string Subscript(int n)
            => new string(n.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(c => (char)('₀' + (c - '0'))).ToArray());

        private VarValue? Get(VarRef r) => Index.TryGetValue(r, out var i) ? Vars[i] : null;

        private string Show(VarValue v, Lang lang, int digits, bool keepZeros)
            => v.Value is double d ? ValueFormat.Format(d, UnitOf(v), lang, digits, keepZeros) : "?";

        private static string Substitute(string template, BoundRelation b, Func<VarRef, string> text)
        {
            var s = template;
            foreach (var kv in b.Map.OrderByDescending(k => k.Key.Length)) s = s.Replace("{" + kv.Key + "}", text(kv.Value));
            return s;
        }
    }

    /// <summary>
    /// The solver of section 1.8: collects variables and relations of the scope (elements plus objects reached through
    /// <see cref="IValueLinks"/>), propagates locally (a relation with exactly one unknown is solved, explicit inverse or
    /// Brent), checks fully known relations, then fills still-unknown variables with defaults that give way. Deterministic
    /// (relation order = scope order, local before links), bounded (each variable is solved once), side-effect free and
    /// time-boxed. Entered values are never overwritten.
    /// </summary>
    public static class ValueSolver
    {
        private sealed class State
        {
            public double[] Value = Array.Empty<double>();
            public bool[] Known = Array.Empty<bool>();
            public Provenance[] Prov = Array.Empty<Provenance>();
            public int[] Via = Array.Empty<int>();
            public int[][] Inputs = Array.Empty<int[]>();
            public int[][] Roots = Array.Empty<int[]>();
            /// <summary>Variables in the order they were derived (for the tolerance ranges).</summary>
            public List<int> Order = new();

            public State Clone() => new()
            {
                Value = (double[])Value.Clone(), Known = (bool[])Known.Clone(), Prov = (Provenance[])Prov.Clone(),
                Via = (int[])Via.Clone(), Inputs = (int[][])Inputs.Clone(), Roots = (int[][])Roots.Clone(), Order = new List<int>(Order),
            };
        }

        /// <summary>Solves the values of <paramref name="ids"/> (null = every object of the first page) in <paramref name="doc"/>.</summary>
        public static ValueTable Solve(SketchDocument doc, Registry registry, IEnumerable<string>? ids, ValueSolveOptions? options = null, Lang lang = Lang.En)
        {
            var opt = options ?? new ValueSolveOptions();
            var sw = Stopwatch.StartNew();
            var table = new ValueTable { Settings = doc.Settings };
            // The context names the registry: links and checks of the types read their neighbours through it.
            var ctx = new RenderContext(doc, lang).For(registry);

            // 1. Scope: the given objects, then objects reached through links (breadth first, in discovery order).
            var start = ids?.ToList() ?? (doc.Pages.Count > 0 ? doc.Pages[0].Order.ToList() : doc.Objects.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
            var scope = new List<SketchObject>();
            var inScope = new HashSet<string>(StringComparer.Ordinal);
            var links = new List<BoundRelation>();
            var seenLinks = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(start);
            void Drain()
            {
                while (queue.Count > 0 && scope.Count < opt.MaxObjects)
                {
                    var id = queue.Dequeue();
                    if (!inScope.Add(id) || doc.Get(id) is not { } o) continue;
                    var type = registry.Type(o.Type);
                    if (type is null) continue;
                    scope.Add(o);
                    if (type is IValueLinks l)
                        foreach (var b in l.ValueLinks(o, ctx))
                        {
                            // Both ends of a link usually report it: keep one (a conflict is then reported once).
                            if (!seenLinks.Add(LinkKey(b))) continue;
                            links.Add(b);
                            foreach (var r in b.Map.Values) if (!inScope.Contains(r.ObjectId)) queue.Enqueue(r.ObjectId);
                        }
                }
            }
            // The module nets asked for while the scope is built (deterministic: they follow from the scope's types).
            var nets = new List<ValueNets>();
            var previousRecorder = ValueNets.BeginRecording(nets);
            try
            {
                Drain();
                // 1b. Joins across modules: lines of one module docked on elements of another pull their own module's
                // elements in, then the nets' junctions are linked by physical key (NetKeys). Repeated while that adds objects.
                var join = new CrossModuleJoin(doc, registry, scope, inScope, queue, links, seenLinks, nets);
                for (var round = 0; round < 32; round++)
                {
                    join.Hop();
                    join.Link();
                    if (queue.Count == 0 || scope.Count >= opt.MaxObjects) break;
                    Drain();
                }
            }
            finally
            {
                ValueNets.EndRecording(previousRecorder);
            }

            // 2. Variables with their entered values; local relations.
            var objectIds = new List<string>();
            var relVars = new List<int[]>();
            foreach (var o in scope)
            {
                var type = registry.Type(o.Type);
                var vars = ValueModel.Variables(type);
                if (vars.Count == 0) continue;
                objectIds.Add(o.Id);
                table.Objects[o.Id] = o;
                var list = new List<int>();
                foreach (var v in vars)
                {
                    var r = new VarRef(o.Id, v.Key);
                    if (table.Index.ContainsKey(r)) continue;
                    var (value, text, tol, error) = ValueModel.ReadEntered(o, v);
                    var vv = new VarValue { Ref = r, Def = v, Text = text, Tolerance = tol, Error = error };
                    if (opt.Overrides is not null && !v.Kind.IsText && opt.Overrides.TryGetValue(r, out var ov)) { value = ov; vv.Text = null; vv.Error = null; }
                    if (value is double d) { vv.Value = d; vv.Provenance = Provenance.Entered; }
                    table.Index[r] = table.Vars.Count;
                    list.Add(table.Vars.Count);
                    table.Vars.Add(vv);
                }
                table.ByObject[o.Id] = list;
                foreach (var rel in ValueModel.Relations(type))
                {
                    // Relation → variable indices, by position in the relation's variable list.
                    var idx = new int[rel.Vars.Count];
                    var ok = true;
                    for (var k = 0; k < idx.Length && ok; k++)
                    {
                        idx[k] = -1;
                        for (var q = 0; q < vars.Count; q++) if (vars[q].Key == rel.Vars[k]) { idx[k] = list[q]; break; }
                        ok = idx[k] >= 0;
                    }
                    if (!ok) continue;
                    AddRelation(table, BoundRelation.Local(rel, o.Id), o.Id);
                    relVars.Add(idx);
                }
            }
            foreach (var b in links)
                if (b.Relation.Vars.All(k => b.Map.TryGetValue(k, out var vr) && table.Index.ContainsKey(vr)))
                {
                    var at = table.Relations.Count;
                    table.Relations.Add(b);
                    foreach (var oid in b.Map.Values.Select(v => v.ObjectId).Distinct()) Track(table, oid, at);
                    relVars.Add(b.Relation.Vars.Select(k => table.Index[b.Map[k]]).ToArray());
                }
            table.ObjectIds = objectIds;

            // Variable → relations.
            var varRels = new List<int>[table.Vars.Count];
            for (var i = 0; i < varRels.Length; i++) varRels[i] = new List<int>();
            for (var r = 0; r < relVars.Count; r++) foreach (var v in relVars[r]) varRels[v].Add(r);

            var st = new State
            {
                Value = table.Vars.Select(v => v.Value ?? double.NaN).ToArray(),
                Known = table.Vars.Select(v => v.Value is not null).ToArray(),
                Prov = table.Vars.Select(v => v.Provenance).ToArray(),
                Via = Enumerable.Repeat(-1, table.Vars.Count).ToArray(),
                Inputs = new int[table.Vars.Count][],
                Roots = new int[table.Vars.Count][],
            };
            for (var i = 0; i < table.Vars.Count; i++)
            {
                st.Inputs[i] = Array.Empty<int>();
                st.Roots[i] = st.Known[i] ? new[] { i } : Array.Empty<int>();
            }

            // 3. Local propagation over all relations.
            var truncated = false;
            Propagate(table, relVars, varRels, st, Enumerable.Range(0, relVars.Count), sw, opt.BudgetMs, ref truncated);

            // 5. Defaults for what is still unknown; a default that leads to a disagreement is withdrawn.
            if (opt.UseDefaults && !truncated)
                for (var i = 0; i < table.Vars.Count; i++)
                {
                    if (st.Known[i] || table.Vars[i].Def.Kind.IsText || table.Vars[i].Def.Default is not double def) continue;
                    var before = st.Clone();
                    st.Value[i] = def; st.Known[i] = true; st.Prov[i] = Provenance.Default; st.Roots[i] = new[] { i };
                    Propagate(table, relVars, varRels, st, varRels[i], sw, opt.BudgetMs, ref truncated);
                    if (Check(table, relVars, st, null).Any(c => c.Roots.Contains(i))) st = before;
                }

            // 4. Checks of fully known relations.
            var conflicts = Check(table, relVars, st, table.CheckedRelations);

            // Results.
            for (var i = 0; i < table.Vars.Count; i++)
            {
                var v = table.Vars[i];
                if (!st.Known[i]) { v.Value = null; v.Provenance = Provenance.Unknown; continue; }
                v.Value = st.Value[i];
                v.Provenance = st.Prov[i];
                if (st.Via[i] >= 0)
                {
                    v.Via = table.Relations[st.Via[i]];
                    v.Inputs = st.Inputs[i].Select(k => table.Vars[k].Ref).ToList();
                }
            }
            foreach (var c in conflicts)
            {
                table.Vars[c.Var].InConflict = true;
                var involved = c.Roots.Where(k => st.Prov[k] == Provenance.Entered || IsMarking(st, k)).Select(k => table.Vars[k].Ref).ToList();
                // A marking that disagrees (a relation of one variable) is itself one of the values in conflict.
                if (relVars[c.Relation].Length == 1 && !involved.Contains(table.Vars[c.Var].Ref)) involved.Add(table.Vars[c.Var].Ref);
                var vc = new ValueConflict(table.Relations[c.Relation], table.Vars[c.Var].Ref, st.Value[c.Var], c.Implied, involved);
                table.ConflictList.Add(vc);
                foreach (var oid in relVars[c.Relation].Select(k => table.Vars[k].Ref.ObjectId).Distinct())
                {
                    if (!table.ConflictsByObject.TryGetValue(oid, out var l)) table.ConflictsByObject[oid] = l = new List<ValueConflict>();
                    l.Add(vc);
                }
            }
            foreach (var r in table.CheckedRelations)
                foreach (var k in relVars[r]) table.CheckedObjects.Add(table.Vars[k].Ref.ObjectId);
            Intervals(table, relVars, st);
            table.Truncated = truncated;
            table.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            return table;
        }

        /// <summary>Lines docked on each object (target id → line ids in id order), per document (documents are immutable).</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SketchDocument, Dictionary<string, List<string>>> Docked = new();

        private static IReadOnlyList<string> DockedOn(SketchDocument doc, string id)
        {
            var map = Docked.GetValue(doc, d =>
            {
                var m = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var o in d.Objects.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
                {
                    if (!o.IsPointGeometry || o.Attach is null) continue;
                    foreach (var a in o.Attach)
                    {
                        if (a is null || a.ObjectId == o.Id) continue;
                        if (!m.TryGetValue(a.ObjectId, out var l)) m[a.ObjectId] = l = new List<string>();
                        if (!l.Contains(o.Id)) l.Add(o.Id);
                    }
                }
                return m;
            });
            return map.TryGetValue(id, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>
        /// Joins across modules (engineering-calculations.md, "Joins across modules"). Each module builds its nets alone
        /// (<see cref="ValueNets"/>); where a net reaches an element or line of another module (<see cref="NetJunction"/>) the
        /// variables of the same physical key (<see cref="NetKeys"/>) and quantity kind are linked with an equality: the
        /// foreign element's own module nets name its variable at that port, else its variable of the key's name. Rules of the
        /// class are respected conservatively: a shared value (<see cref="NetRule.Equal"/>) joins the whole net; flows, sums and
        /// spreads only an unbranched run with at most two elements in all. Nothing is linked when the kinds differ or the
        /// foreign variable is ambiguous; nothing changes when no net crosses a module border.
        /// </summary>
        private sealed class CrossModuleJoin
        {
            private readonly SketchDocument _doc;
            private readonly Registry _registry;
            private readonly List<SketchObject> _scope;
            private readonly HashSet<string> _inScope;
            private readonly Queue<string> _queue;
            private readonly List<BoundRelation> _links;
            private readonly HashSet<string> _seenLinks;
            private readonly List<ValueNets> _nets;
            private readonly HashSet<NetJunction> _done = new();
            private readonly HashSet<string> _followed = new(StringComparer.Ordinal);
            private int _hopped;

            public CrossModuleJoin(SketchDocument doc, Registry registry, List<SketchObject> scope, HashSet<string> inScope, Queue<string> queue,
                List<BoundRelation> links, HashSet<string> seenLinks, List<ValueNets> nets)
            {
                _doc = doc; _registry = registry; _scope = scope; _inScope = inScope; _queue = queue; _links = links; _seenLinks = seenLinks; _nets = nets;
            }

            private void Enqueue(string id) { if (!_inScope.Contains(id)) _queue.Enqueue(id); }

            private IReadOnlyList<VarDef> VariablesOf(SketchObject o) => ValueModel.Variables(_registry.Type(o.Type));

            /// <summary>
            /// For every new object of the scope: lines of another module docked on it bring that module's elements with values
            /// (and the lines themselves when they have values) into the scope, following lines of that module docked on each
            /// other, so that the module's nets are built and its junctions seen (a lamp alone finds the PV string feeding it).
            /// </summary>
            public void Hop()
            {
                for (; _hopped < _scope.Count; _hopped++)
                {
                    var e = _scope[_hopped];
                    var module = ValueNets.ModuleOf(e.Type);
                    foreach (var lineId in DockedOn(_doc, e.Id))
                        if (_doc.Get(lineId) is { } line && ValueNets.ModuleOf(line.Type) != module) Follow(line);
                }
            }

            private void Follow(SketchObject first)
            {
                var module = ValueNets.ModuleOf(first.Type);
                var stack = new Stack<SketchObject>();
                stack.Push(first);
                while (stack.Count > 0)
                {
                    var line = stack.Pop();
                    if (!_followed.Add(line.Id)) continue;
                    if (VariablesOf(line).Count > 0) Enqueue(line.Id);
                    foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                    {
                        if (a is null || a.ObjectId == line.Id || _doc.Get(a.ObjectId) is not { } target || ValueNets.ModuleOf(target.Type) != module) continue;
                        if (target.IsPointGeometry) stack.Push(target);
                        else if (VariablesOf(target).Count > 0) Enqueue(target.Id);
                    }
                    foreach (var id in DockedOn(_doc, line.Id))
                        if (_doc.Get(id) is { IsPointGeometry: true } other && ValueNets.ModuleOf(other.Type) == module) stack.Push(other);
                }
            }

            /// <summary>Links the junctions of the recorded nets whose objects are all in the scope (the others wait for the next round).</summary>
            public void Link()
            {
                foreach (var nets in _nets.ToList())
                    foreach (var j in nets.Junctions)
                        if (!_done.Contains(j) && TryLink(j)) _done.Add(j);
            }

            /// <summary>False while a foreign object is not in the scope yet (it is queued); true when the junction is settled.</summary>
            private bool TryLink(NetJunction j)
            {
                var key = NetKeys.Of(j.Spec, j.Class);
                if (key is null || NetKeys.KindOf(key) is not { } kind) return true;
                var members = new List<VarRef>(j.Elements.Count + j.Lines.Count);
                members.AddRange(j.Elements);
                members.AddRange(j.Lines);
                foreach (var m in members)
                    if (_doc.Get(m.ObjectId) is not { } mo || VariablesOf(mo).FirstOrDefault(v => v.Key == m.Key) is not { } def || def.Kind.Id != kind.Id) return true;

                var pending = false;
                var foreign = new List<(VarRef Ref, bool Line)>();
                foreach (var (obj, port) in j.ForeignPorts)
                {
                    if (!_inScope.Contains(obj)) { Enqueue(obj); pending = true; continue; }
                    if (Resolve(obj, port, key, kind) is { } r && !foreign.Any(f => f.Ref == r)) foreign.Add((r, false));
                }
                foreach (var lineId in j.ForeignLines)
                {
                    if (!_inScope.Contains(lineId)) { Enqueue(lineId); pending = true; continue; }
                    if (Resolve(lineId, null, key, kind) is { } r && !foreign.Any(f => f.Ref == r)) foreign.Add((r, true));
                }
                if (pending) return false;
                if (foreign.Count == 0) return true;
                if (j.Rule != NetRule.Equal && (j.Branched || j.Elements.Count + foreign.Count(f => !f.Line) > 2)) return true;

                var anchor = members.Count > 0 ? members[0] : foreign[0].Ref;
                var rel = NetKeys.Link(key);
                foreach (var (r, _) in foreign)
                {
                    if (r == anchor) continue;
                    var b = new BoundRelation(rel, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = anchor, ["b"] = r });
                    if (!_seenLinks.Add(LinkKey(b))) continue;
                    _links.Add(b);
                    foreach (var v in b.Map.Values) Enqueue(v.ObjectId);
                }
                return true;
            }

            /// <summary>
            /// The variable of <paramref name="key"/> of a foreign element at <paramref name="port"/> (null port: a foreign line):
            /// named by the element's own module nets when they were built in this pass (exactly one), else the first of the key's
            /// variable names the element has; null when none, ambiguous or of another kind.
            /// </summary>
            private VarRef? Resolve(string objectId, string? port, string key, QuantityKind kind)
            {
                if (_doc.Get(objectId) is not { } o) return null;
                var vars = VariablesOf(o);
                if (vars.Count == 0) return null;
                var module = ValueNets.ModuleOf(o.Type);
                var own = _nets.FirstOrDefault(n => n.Spec.Prefix == module);
                string? name;
                if (own is not null)
                {
                    var spec = own.Spec;
                    var pvs = port is null ? (spec.IsLine(o) ? spec.LineVars(o) : Array.Empty<PortVar>()) : spec.PortVars(o, port);
                    var names = pvs.Where(pv => NetKeys.Of(spec, pv.Class) == key).Select(pv => pv.Key).Distinct(StringComparer.Ordinal).ToList();
                    if (names.Count != 1) return null;
                    name = names[0];
                }
                else name = NetKeys.VariableNames(key).FirstOrDefault(n => vars.Any(v => v.Key == n && v.Kind.Id == kind.Id));
                var def = name is null ? null : vars.FirstOrDefault(v => v.Key == name);
                return def is not null && def.Kind.Id == kind.Id ? new VarRef(o.Id, def.Key) : null;
            }
        }

        /// <summary>A value from a relation without inputs (a marking such as "24 V"): named among the values a conflict involves.</summary>
        private static bool IsMarking(State st, int k) => st.Via[k] >= 0 && st.Inputs[k] is { Length: 0 };

        private static void Propagate(ValueTable table, List<int[]> relVars, List<int>[] varRels, State st, IEnumerable<int> seed, Stopwatch sw, double budget, ref bool truncated)
        {
            var pending = new SortedSet<int>(seed);
            var n = 0;
            while (pending.Count > 0)
            {
                if ((++n & 63) == 0 && sw.Elapsed.TotalMilliseconds > budget) { truncated = true; return; }
                var r = pending.Min;
                pending.Remove(r);
                var vars = relVars[r];
                var unknown = -1;
                var count = 0;
                foreach (var v in vars) if (!st.Known[v]) { unknown = v; if (++count > 1) break; }
                if (count != 1) continue;
                // A text-valued variable never enters arithmetic: a relation with one is solved only for it.
                if (!table.Vars[unknown].Def.Kind.IsText && HasText(table, vars)) continue;
                var b = table.Relations[r];
                var key = KeyOf(b, vars, unknown);
                if (!b.Relation.CanSolve(key)) continue;
                var def = table.Vars[unknown].Def;
                var value = b.Relation.Compute(key, k => st.Value[vars[Pos(b.Relation, k)]], def.Min, def.Max);
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                st.Value[unknown] = value;
                st.Known[unknown] = true;
                st.Prov[unknown] = b.Relation.IsLink ? Provenance.Linked : Provenance.Derived;
                st.Via[unknown] = r;
                var inputs = new int[vars.Length - 1];
                var w = 0;
                foreach (var v in vars) if (v != unknown) inputs[w++] = v;
                st.Inputs[unknown] = inputs;
                st.Order.Add(unknown);
                // A value from a relation without inputs (a marking such as "24 V" on a lamp) is its own origin, like an entered
                // value: two markings that disagree are then checked against each other.
                st.Roots[unknown] = inputs.Length == 0 ? new[] { unknown } : Union(inputs.Select(v => st.Roots[v]));
                foreach (var next in varRels[unknown]) if (next != r) pending.Add(next);
            }
        }

        /// <summary>
        /// Tolerance ranges (Calc 6): entered values with a tolerance get its bounds; derived values, in the order they were
        /// derived, the range of their relation over the corners of their inputs' ranges (exact for relations monotonic in
        /// each input, as Ohm's law, power, ratios and sums are; at most 2⁶ corners).
        /// </summary>
        private static void Intervals(ValueTable table, List<int[]> relVars, State st)
        {
            var n = table.Vars.Count;
            var lo = new double[n];
            var hi = new double[n];
            var any = false;
            for (var i = 0; i < n; i++)
            {
                lo[i] = hi[i] = st.Value[i];
                if (st.Known[i] && st.Prov[i] == Provenance.Entered && table.Vars[i].Tolerance is { } tol)
                {
                    var (a, c) = tol.Bounds(st.Value[i]);
                    lo[i] = Math.Min(a, c); hi[i] = Math.Max(a, c);
                    any |= hi[i] > lo[i];
                }
            }
            if (!any) return;
            foreach (var k in st.Order)
            {
                var inputs = st.Inputs[k];
                if (inputs is null || inputs.Length == 0 || st.Via[k] < 0 || table.Vars[k].Def.Kind.IsText) continue;
                var wide = inputs.Where(v => hi[v] > lo[v]).ToList();
                if (wide.Count == 0 || wide.Count > 6) continue;
                var b = table.Relations[st.Via[k]];
                var vars = relVars[st.Via[k]];
                var key = KeyOf(b, vars, k);
                var value = new double[n];
                Array.Copy(st.Value, value, n);
                double min = double.PositiveInfinity, max = double.NegativeInfinity;
                for (var mask = 0; mask < 1 << wide.Count; mask++)
                {
                    for (var j = 0; j < wide.Count; j++) value[wide[j]] = (mask >> j & 1) == 0 ? lo[wide[j]] : hi[wide[j]];
                    var def = table.Vars[k].Def;
                    var x = b.Relation.Compute(key, name => value[vars[Pos(b.Relation, name)]], def.Min, def.Max);
                    if (double.IsNaN(x) || double.IsInfinity(x)) continue;
                    min = Math.Min(min, x); max = Math.Max(max, x);
                }
                if (min > max) continue;
                lo[k] = Math.Min(min, st.Value[k]); hi[k] = Math.Max(max, st.Value[k]);
            }
            for (var i = 0; i < n; i++)
                if (st.Known[i] && hi[i] > lo[i]) table.Vars[i].Interval = (lo[i], hi[i]);
        }

        /// <summary>
        /// Identity of a link: its relation and the variables it binds; a pass-through equality is the same whichever end
        /// reports it (a = b and b = a).
        /// </summary>
        private static string LinkKey(BoundRelation b)
        {
            var pairs = b.Map.Select(kv => kv.Key + "=" + kv.Value.ObjectId + "/" + kv.Value.Key).ToList();
            if (b.Relation.IsLink && b.Map.Count == 2)
                return b.Relation.Id + "|" + string.Join("|", b.Map.Values.Select(v => v.ObjectId + "/" + v.Key).OrderBy(s => s, StringComparer.Ordinal));
            pairs.Sort(StringComparer.Ordinal);
            return b.Relation.Id + "|" + string.Join("|", pairs);
        }

        private static void AddRelation(ValueTable table, BoundRelation b, string objectId)
        {
            Track(table, objectId, table.Relations.Count);
            table.Relations.Add(b);
        }

        private static void Track(ValueTable table, string objectId, int relation)
        {
            if (!table.RelationsByObject.TryGetValue(objectId, out var l)) table.RelationsByObject[objectId] = l = new List<int>();
            l.Add(relation);
        }

        private static int Pos(Relation r, string key)
        {
            var vars = r.Vars;
            for (var i = 0; i < vars.Count; i++) if (ReferenceEquals(vars[i], key) || vars[i] == key) return i;
            throw new KeyNotFoundException($"'{key}' is not a variable of relation '{r.Id}'.");
        }

        private static string KeyOf(BoundRelation b, int[] vars, int index)
        {
            for (var i = 0; i < vars.Length; i++) if (vars[i] == index) return b.Relation.Vars[i];
            throw new InvalidOperationException();
        }

        private readonly record struct Found(int Relation, int Var, double Implied, int[] Roots);

        /// <summary>
        /// Checks every fully known relation that is an independent check (some variable carries information the others do
        /// not: its roots are not covered by theirs). Mismatches beyond the relation's tolerance are returned; passing
        /// independent checks are recorded in <paramref name="passed"/>.
        /// </summary>
        private static List<Found> Check(ValueTable table, List<int[]> relVars, State st, HashSet<int>? passed)
        {
            var found = new List<Found>();
            for (var r = 0; r < relVars.Count; r++)
            {
                var vars = relVars[r];
                var complete = true;
                foreach (var v in vars) if (!st.Known[v]) { complete = false; break; }
                // Text-valued results (a class) are never checked: they carry no number to compare.
                if (!complete || HasText(table, vars)) continue;
                var b = table.Relations[r];
                int chosen = -1;
                for (var i = 0; i < vars.Length && chosen < 0; i++)
                    if (Informative(st, vars, i) && b.Relation.HasExplicit(b.Relation.Vars[i])) chosen = i;
                if (chosen < 0)
                {
                    // Independent but no explicit inverse for the informative variable: any explicit one will do.
                    var independent = false;
                    for (var i = 0; i < vars.Length && !independent; i++) independent = Informative(st, vars, i);
                    if (!independent) continue;
                    for (var i = 0; i < vars.Length && chosen < 0; i++) if (b.Relation.HasExplicit(b.Relation.Vars[i])) chosen = i;
                    if (chosen < 0) continue;
                }
                var v0 = vars[chosen];
                var implied = b.Relation.Compute(b.Relation.Vars[chosen], k => st.Value[vars[Pos(b.Relation, k)]]);
                var actual = st.Value[v0];
                var scale = Math.Max(Math.Abs(implied), Math.Abs(actual));
                var ok = !double.IsNaN(implied) && Math.Abs(implied - actual) <= b.Relation.Tolerance * scale + 1e-300;
                if (ok) { passed?.Add(r); continue; }
                found.Add(new Found(r, v0, implied, Union(vars.Select(v => st.Roots[v]))));
            }
            return found;
        }

        /// <summary>True if one of the variables is text-valued (<see cref="QuantityKind.IsText"/>).</summary>
        private static bool HasText(ValueTable table, int[] vars)
        {
            foreach (var v in vars) if (table.Vars[v].Def.Kind.IsText) return true;
            return false;
        }

        /// <summary>True if variable <paramref name="i"/> of a relation rests on a value none of the other variables rests on.</summary>
        private static bool Informative(State st, int[] vars, int i)
        {
            foreach (var x in st.Roots[vars[i]])
            {
                var covered = false;
                for (var j = 0; j < vars.Length && !covered; j++)
                    if (j != i && Array.BinarySearch(st.Roots[vars[j]], x) >= 0) covered = true;
                if (!covered) return true;
            }
            return false;
        }

        /// <summary>Sorted union of sorted sets.</summary>
        private static int[] Union(IEnumerable<int[]> sets)
        {
            var all = new List<int>();
            foreach (var s in sets) all.AddRange(s);
            if (all.Count == 0) return Array.Empty<int>();
            all.Sort();
            var w = 1;
            for (var k = 1; k < all.Count; k++) if (all[k] != all[w - 1]) all[w++] = all[k];
            all.RemoveRange(w, all.Count - w);
            return all.ToArray();
        }

        /// <summary>Runs the plausibility checks of the scope's types (<see cref="IValueChecks"/>) on a solved table.</summary>
        public static IReadOnlyList<CheckFinding> Checks(ValueTable table, Registry registry, RenderContext ctx)
        {
            var list = new List<CheckFinding>();
            foreach (var id in table.ObjectIds)
            {
                var o = table.Objects[id];
                if (registry.Type(o.Type) is not IValueChecks checks) continue;
                var c = new ValueCheckContext(o, table, ctx.Registry is null ? ctx.For(registry) : ctx);
                var muted = table.Settings.MutedChecks;
                foreach (var rule in checks.ValueChecks)
                {
                    // Checks the user muted for this drawing are not reported (Calc 6).
                    if (muted is not null && muted.Contains(rule.Id)) continue;
                    bool violated;
                    try { violated = rule.Violated(c); }
                    catch (ArithmeticException) { violated = false; }
                    if (violated) list.Add(new CheckFinding(rule.Id, id, rule.Severity, rule.Message, rule.Rule, rule.Source));
                }
            }
            return list;
        }
    }
}
