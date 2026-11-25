using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Calc 1 – element values and relations (docs/en/design/engineering-calculations.md, sections 1.2, 1.8).
    // A type declares variables (VarDef) bound to its parameters and relations between them; the solver
    // (ValueSolver) computes derived values on demand. Entered values live in the parameters (existing keys, their
    // units); derived values are never stored.
    // =============================================================================================

    /// <summary>Where a value comes from.</summary>
    public enum Provenance
    {
        /// <summary>Not known (under-determined).</summary>
        Unknown,
        /// <summary>Typed, dragged or read from a datasheet by the user (stored in the parameter).</summary>
        Entered,
        /// <summary>The type's default; gives way to anything that contradicts it.</summary>
        Default,
        /// <summary>Taken over from a connected element (port relation).</summary>
        Linked,
        /// <summary>Computed by a relation.</summary>
        Derived,
    }

    /// <summary>Where a variable is shown (section 1.4): on the card (at most four), in the Values panel, or for engineers only.</summary>
    public enum VarRelevance { Card, Panel, Engineer }

    /// <summary>
    /// A named, typed quantity of an element: key, bilingual name and symbol (torque: T/M), quantity kind, display unit,
    /// typical range and relevance. It is bound to a parameter (<see cref="Param"/>, default the key): a text parameter
    /// keeps the text as typed ("4k7", "230 V"), a number parameter holds the value in <see cref="StoredUnit"/>.
    /// </summary>
    public sealed record VarDef(string Key, I18nText Name, I18nText Symbol, QuantityKind Kind)
    {
        /// <summary>Display unit spelling (bare numbers are read in it); null = the kind's default.</summary>
        public string? Unit { get; init; }
        /// <summary>Bound parameter key; null = <see cref="Key"/>.</summary>
        public string? Param { get; init; }
        /// <summary>Unit of a number parameter (e.g. "kV" for Energy voltages); null = <see cref="Unit"/>.</summary>
        public string? StoredUnit { get; init; }
        public VarRelevance Relevance { get; init; } = VarRelevance.Card;
        /// <summary>Default value (SI), used only when nothing determines the variable.</summary>
        public double? Default { get; init; }
        /// <summary>Typical range (SI), also the bracket of a numeric root search.</summary>
        public double? Min { get; init; }
        public double? Max { get; init; }
        /// <summary>
        /// The type already draws the entered value in a label of its own (the resistor's value text "4k7"): the values
        /// label (<see cref="ValueLabels"/>) leaves it out unless it is chosen explicitly.
        /// </summary>
        public bool OnTypeLabel { get; init; }

        public string ParamKey => Param ?? Key;

        /// <summary>The display unit (resolved).</summary>
        public Unit DisplayUnit => (Unit is null ? null : UnitCatalog.Find(Unit, Kind)) ?? Kind.DefaultUnit;
    }

    /// <summary>A variable of one element: (object id, variable key).</summary>
    public readonly record struct VarRef(string ObjectId, string Key)
    {
        public override string ToString() => ObjectId + "." + Key;
    }

    /// <summary>
    /// An equation between variables (by key), e.g. U = R·I. It carries the solver for each variable it can be solved for:
    /// an explicit inverse with its expression ("{U}/{I}", keys in braces, used for the step-by-step display), or a residual
    /// f = 0 solved by a bracketed root search (Brent) within the variable's range. Tolerance is relative (1e-6 for exact
    /// relations, a declared value for empirical ones).
    /// </summary>
    public sealed class Relation
    {
        private readonly Dictionary<string, (string Expression, Func<Func<string, double>, double> Compute)> _solvers = new(StringComparer.Ordinal);

        public Relation(string id, I18nText name, string formula, params string[] vars)
        {
            Id = id; Name = name; Formula = formula; Vars = vars;
        }

        /// <summary>Stable id, e.g. "elec.ohm".</summary>
        public string Id { get; }
        public I18nText Name { get; }
        /// <summary>The equation with keys in braces: "{U} = {R}·{I}".</summary>
        public string Formula { get; }
        public IReadOnlyList<string> Vars { get; }
        /// <summary>Source id of section 9 ("S7"), shown in Engineer.</summary>
        public string? Source { get; init; }
        public double Tolerance { get; init; } = 1e-6;
        /// <summary>Validity conditions ("ideal transformer").</summary>
        public I18nText? Validity { get; init; }
        /// <summary>Port relation that passes a value on unchanged: solved values count as <see cref="Provenance.Linked"/>.</summary>
        public bool IsLink { get; init; }
        /// <summary>Residual f(vars) = 0 for variables without an explicit solver.</summary>
        public Func<Func<string, double>, double>? Residual { get; private set; }

        /// <summary>Adds the explicit solver for <paramref name="key"/>: <paramref name="expression"/> with keys in braces.</summary>
        public Relation Solve(string key, string expression, Func<Func<string, double>, double> compute)
        {
            if (!Vars.Contains(key)) throw new ArgumentException($"'{key}' is not a variable of relation '{Id}'.");
            _solvers[key] = (expression, compute);
            return this;
        }

        /// <summary>Sets the residual used for a numeric root search.</summary>
        public Relation WithResidual(Func<Func<string, double>, double> residual) { Residual = residual; return this; }

        public bool CanSolve(string key) => _solvers.ContainsKey(key) || Residual is not null && Vars.Contains(key);
        public bool HasExplicit(string key) => _solvers.ContainsKey(key);

        /// <summary>The expression of the explicit solver ("{U}/{I}"); null for a numeric solution.</summary>
        public string? Expression(string key) => _solvers.TryGetValue(key, out var s) ? s.Expression : null;

        /// <summary>Solves for <paramref name="key"/> from the other values (NaN if impossible).</summary>
        public double Compute(string key, Func<string, double> get, double? lo = null, double? hi = null)
        {
            if (_solvers.TryGetValue(key, out var s))
            {
                try { return s.Compute(get); }
                catch (ArithmeticException) { return double.NaN; }
            }
            if (Residual is null) return double.NaN;
            double F(double x) => Residual(k => k == key ? x : get(k));
            return RootFinder.Solve(F, lo, hi);
        }

        /// <summary>A link: a = b, solved both ways (series current, shared speed …).</summary>
        public static Relation Equality(string id, I18nText name, string a = "a", string b = "b") =>
            new Relation(id, name, "{" + a + "} = {" + b + "}", a, b) { IsLink = true }
                .Solve(a, "{" + b + "}", v => v(b))
                .Solve(b, "{" + a + "}", v => v(a));

        public override string ToString() => Id;
    }

    /// <summary>A relation bound to concrete variables: relation key → (object id, variable key).</summary>
    public sealed class BoundRelation
    {
        private readonly string? _local;
        private IReadOnlyDictionary<string, VarRef>? _map;

        public BoundRelation(Relation relation, IReadOnlyDictionary<string, VarRef> map) { Relation = relation; _map = map; }

        private BoundRelation(Relation relation, string objectId) { Relation = relation; _local = objectId; }

        public Relation Relation { get; }

        /// <summary>Relation key → variable (built on first use for local bindings).</summary>
        public IReadOnlyDictionary<string, VarRef> Map => _map ??= Relation.Vars.ToDictionary(k => k, k => new VarRef(_local!, k));

        /// <summary>The single element of a local binding, else null.</summary>
        public string? LocalObject => _local;

        /// <summary>The relation bound to the variables of one element (keys unchanged).</summary>
        public static BoundRelation Local(Relation r, string objectId) => new(r, objectId);

        /// <summary>True if the relation involves a variable of <paramref name="objectId"/>.</summary>
        public bool Involves(string objectId) => _local is not null ? _local == objectId : Map.Values.Any(v => v.ObjectId == objectId);
    }

    /// <summary>A type with element values (Calc 1). Implemented by <see cref="ObjectType"/>s that have variables.</summary>
    public interface IValueType
    {
        /// <summary>Variables in display order (at most four with <see cref="VarRelevance.Card"/>).</summary>
        IReadOnlyList<VarDef> Variables { get; }
        /// <summary>Relations between the variables (keys of <see cref="Variables"/>).</summary>
        IReadOnlyList<Relation> Relations { get; }
    }

    /// <summary>
    /// Hook for propagation through connections (section 1.6, wired in Calc 3/4): port relations between this element's
    /// variables and those of connected elements (mesh ratio, series current …). Objects referenced by a link join the
    /// solver scope.
    /// </summary>
    public interface IValueLinks
    {
        IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx);
    }

    public enum CheckSeverity { Info, Hint, Warning }

    /// <summary>Plausibility rule (section 1.9): runs after solving, never changes values or geometry.</summary>
    public sealed record ValueCheck(string Id, CheckSeverity Severity, I18nText Message, string Rule, Func<ValueCheckContext, bool> Violated)
    {
        public string? Source { get; init; }
    }

    /// <summary>What a check sees: the element and its solved values.</summary>
    public sealed class ValueCheckContext
    {
        public ValueCheckContext(SketchObject obj, ValueTable table, RenderContext ctx) { Object = obj; Table = table; Context = ctx; }
        public SketchObject Object { get; }
        public ValueTable Table { get; }
        public RenderContext Context { get; }
        /// <summary>The solved value (SI) of a variable of this element, or null.</summary>
        public double? this[string key] => Table.Get(Object.Id, key)?.Value;
    }

    /// <summary>A type with plausibility checks (catalogue in section 4, rules come with Calc 3/4).</summary>
    public interface IValueChecks
    {
        IEnumerable<ValueCheck> ValueChecks { get; }
    }

    /// <summary>A violated check on one element.</summary>
    public sealed record CheckFinding(string CheckId, string ObjectId, CheckSeverity Severity, I18nText Message, string Rule, string? Source);

    /// <summary>Reading and writing entered values in parameters.</summary>
    public static class ValueModel
    {
        public static IReadOnlyList<VarDef> Variables(ObjectType? type) => (type as IValueType)?.Variables ?? Array.Empty<VarDef>();
        public static IReadOnlyList<Relation> Relations(ObjectType? type) => (type as IValueType)?.Relations ?? Array.Empty<Relation>();
        public static bool HasValues(ObjectType? type) => Variables(type).Count > 0;

        public static VarDef? Variable(ObjectType? type, string key) => Variables(type).FirstOrDefault(v => v.Key == key);

        /// <summary>
        /// The entered value of <paramref name="v"/> in <paramref name="o"/>: SI value, the text as typed (text parameters),
        /// tolerance, or a parse error (the value then counts as not entered). Texts are read leniently (German rules).
        /// </summary>
        public static (double? Value, string? Text, Tolerance? Tolerance, I18nText? Error) ReadEntered(SketchObject o, VarDef v)
        {
            if (!o.Params.TryGetValue(v.ParamKey, out var p)) return (null, null, null, null);
            if (p.Number is double n)
            {
                var unit = (v.StoredUnit ?? v.Unit) is string s ? UnitCatalog.Find(s, v.Kind) ?? v.Kind.DefaultUnit : v.Kind.DefaultUnit;
                return double.IsNaN(n) || double.IsInfinity(n) ? (null, null, null, null) : (unit.ToSi(n), null, null, null);
            }
            if (p.Text is string text)
            {
                if (text.Trim().Length == 0) return (null, null, null, null);
                return ValueParser.TryParse(text, v.Kind, out var parsed, out var error, Lang.De, v.Unit)
                    ? (parsed!.Value, text, parsed.Tolerance, null)
                    : (null, text, null, error);
            }
            return (null, null, null, null);
        }

        /// <summary>True if the variable is stored as text (text parameter, or no parameter definition and no number stored).</summary>
        public static bool StoresText(ObjectType? type, SketchObject o, VarDef v)
        {
            var def = type?.Parameters.FirstOrDefault(p => p.Key == v.ParamKey);
            if (def is not null) return def.Kind == ParamKind.Text;
            return !(o.Params.TryGetValue(v.ParamKey, out var cur) && cur.Number.HasValue);
        }

        /// <summary>
        /// The parameter value that stores <paramref name="parsed"/>: the text as typed for text parameters, else the number
        /// in the stored unit.
        /// </summary>
        public static ParamValue ToParam(ObjectType? type, SketchObject o, VarDef v, ParsedValue parsed)
        {
            if (StoresText(type, o, v)) return parsed.Text;
            var unit = (v.StoredUnit ?? v.Unit) is string s ? UnitCatalog.Find(s, v.Kind) ?? v.Kind.DefaultUnit : v.Kind.DefaultUnit;
            return unit.FromSi(parsed.Value);
        }

        /// <summary>The parameter value for an SI number (a drag or slider): formatted text for text parameters, else the number in the stored unit.</summary>
        public static ParamValue ToParam(ObjectType? type, SketchObject o, VarDef v, double si, Lang lang, int digits = 3)
        {
            if (StoresText(type, o, v)) return ValueFormat.Format(si, v.DisplayUnit, lang, digits);
            var unit = (v.StoredUnit ?? v.Unit) is string s ? UnitCatalog.Find(s, v.Kind) ?? v.Kind.DefaultUnit : v.Kind.DefaultUnit;
            return unit.FromSi(si);
        }
    }

    /// <summary>Bracketed root search (Brent) for relations without an explicit inverse.</summary>
    public static class RootFinder
    {
        /// <summary>A root of <paramref name="f"/> in [lo, hi]; without a range a sign change is searched on a logarithmic grid 1e-12 … 1e12 (positive values first). NaN if none.</summary>
        public static double Solve(Func<double, double> f, double? lo = null, double? hi = null)
        {
            if (lo is double a0 && hi is double b0) return Brent(f, a0, b0);
            var grid = new List<double>();
            for (var e = -12; e <= 12; e++) { grid.Add(Math.Pow(10, e)); grid.Add(3 * Math.Pow(10, e)); }
            foreach (var sign in new[] { 1.0, -1.0 })
            {
                var prev = sign * grid[0];
                var fp = f(prev);
                for (var i = 1; i < grid.Count; i++)
                {
                    var x = sign * grid[i];
                    var fx = f(x);
                    if (!double.IsNaN(fp) && !double.IsNaN(fx) && (fp == 0 || fp * fx < 0)) return Brent(f, prev, x);
                    prev = x; fp = fx;
                }
            }
            return double.NaN;
        }

        public static double Brent(Func<double, double> f, double a, double b, double tol = 1e-12, int maxIter = 200)
        {
            double fa = f(a), fb = f(b);
            if (double.IsNaN(fa) || double.IsNaN(fb) || fa * fb > 0) return double.NaN;
            if (fa == 0) return a;
            if (fb == 0) return b;
            if (Math.Abs(fa) < Math.Abs(fb)) { (a, b) = (b, a); (fa, fb) = (fb, fa); }
            double c = a, fc = fa, d = b - a;
            var mflag = true;
            for (var i = 0; i < maxIter; i++)
            {
                double s;
                if (fa != fc && fb != fc)
                    s = a * fb * fc / ((fa - fb) * (fa - fc)) + b * fa * fc / ((fb - fa) * (fb - fc)) + c * fa * fb / ((fc - fa) * (fc - fb));
                else s = b - fb * (b - a) / (fb - fa);
                var cond = (s - (3 * a + b) / 4) * (s - b) > 0
                    || mflag && Math.Abs(s - b) >= Math.Abs(b - c) / 2
                    || !mflag && Math.Abs(s - b) >= Math.Abs(c - d) / 2
                    || mflag && Math.Abs(b - c) < tol
                    || !mflag && Math.Abs(c - d) < tol;
                if (cond) { s = (a + b) / 2; mflag = true; } else mflag = false;
                var fs = f(s);
                d = c; c = b; fc = fb;
                if (fa * fs < 0) { b = s; fb = fs; } else { a = s; fa = fs; }
                if (Math.Abs(fa) < Math.Abs(fb)) { (a, b) = (b, a); (fa, fb) = (fb, fa); }
                if (fb == 0 || Math.Abs(b - a) <= tol * Math.Max(1, Math.Abs(b))) return b;
            }
            return b;
        }
    }
}
