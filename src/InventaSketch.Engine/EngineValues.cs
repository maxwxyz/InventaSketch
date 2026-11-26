using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Engine methods for element values, the unit converter and the calculator (Calc 0/1,
    /// docs/en/design/engineering-calculations.md section 1.8):
    /// <list type="bullet">
    /// <item><c>values</c> {id | ids, digits}: value table of the elements (all of the page without ids) with provenance,
    /// unit, formula, status, missing sets and conflicts.</item>
    /// <item><c>setValue</c> {id, key, text}: stores an entered value (text as typed for text parameters, else the number in
    /// the parameter's unit); one undo step; a wrong unit throws a bilingual message; empty text clears.</item>
    /// <item><c>clearValue</c> {id, key}: removes the entered value; one undo step.</item>
    /// <item><c>explain</c> {id, key, digits}: step-by-step formula lines ("R = U/I = 230 V/2.00 A = 115 Ω").</item>
    /// <item><c>dragValue</c> {id, key, value (SI) | text, preview}: preview=true returns the values as if entered, without
    /// changing the document; otherwise stores the value (formatted) as one undo step.</item>
    /// <item><c>checks</c> {ids}: violated plausibility checks (rules come with Calc 3/4).</item>
    /// <item><c>convertUnit</c> {value, fromUnit, unit} or {text, unit, quantityKind}: unit conversion with kind checks.</item>
    /// <item><c>calc</c> {text, digits}: the unit-aware calculator ("230 V * 16 A" → 3.68 kW).</item>
    /// <item><c>units</c> {quantityKind}: quantity kinds and their display units.</item>
    /// </list>
    /// </summary>
    public sealed partial class EngineApi
    {
        private void RegisterValueMethods()
        {
            _methods["values"] = ValuesCore;
            _methods["setValue"] = SetValueCore;
            _methods["clearValue"] = ClearValueCore;
            _methods["explain"] = ExplainCore;
            _methods["dragValue"] = DragValueCore;
            _methods["checks"] = ChecksCore;
            _methods["convertUnit"] = ConvertUnitCore;
            _methods["calc"] = CalcCore;
            _methods["units"] = UnitsCore;
            _methods["calcSheet"] = CalcSheetCore;
            _methods["muteCheck"] = MuteCheckCore;
            _methods["setUnitPreference"] = SetUnitPreferenceCore;
        }

        /// <summary>
        /// muteCheck: mutes (<c>text</c> "on", default) or unmutes ("off") the plausibility check <c>key</c> ("elec.r.power")
        /// for the drawing (Calc 6); one undo step; saved with the document. Returns the state and the muted ids.
        /// </summary>
        private object MuteCheckCore(EngineRequest r)
        {
            var s = S(r);
            var id = (r.Key ?? "").Trim();
            if (id.Length == 0) throw new ArgumentException("Missing 'key' (the check id).");
            var mute = !string.Equals((r.Text ?? "on").Trim(), "off", StringComparison.OrdinalIgnoreCase);
            var now = s.Doc.Settings.MutedChecks?.Contains(id) == true;
            if (now != mute) s.Apply(mute ? "Mute check" : "Unmute check", d => d with { Settings = d.Settings.WithMutedCheck(id, mute) });
            return new { state = StateOf(s, r), muted = s.Doc.Settings.MutedChecks ?? Array.Empty<string>() };
        }

        /// <summary>
        /// setUnitPreference: the preferred display unit <c>unit</c> for <c>quantityKind</c> ("pressure" → "MPa"; empty clears
        /// it) in this drawing (Calc 6); one undo step. Unknown kinds or units of another kind are refused.
        /// </summary>
        private object SetUnitPreferenceCore(EngineRequest r)
        {
            var s = S(r);
            var kind = Quantities.Get(r.QuantityKind) ?? throw new ArgumentException($"Unknown quantity kind '{r.QuantityKind}'.");
            var unit = string.IsNullOrWhiteSpace(r.Unit) ? null : (UnitCatalog.Find(r.Unit!, kind) ?? throw new ArgumentException($"'{r.Unit}' is no unit of {kind.Id}.")).Symbol;
            var current = s.Doc.Settings.UnitPreferences is { } p && p.TryGetValue(kind.Id, out var u) ? u : null;
            if (current != unit) s.Apply("Unit preference", d => d with { Settings = d.Settings.WithUnitPreference(kind.Id, unit) });
            return new { state = StateOf(s, r), unitPreferences = s.Doc.Settings.UnitPreferences ?? new Dictionary<string, string>() };
        }

        public string Values(string json) => Invoke("values", json);
        public string SetValue(string json) => Invoke("setValue", json);
        public string ClearValue(string json) => Invoke("clearValue", json);
        public string Explain(string json) => Invoke("explain", json);
        public string DragValue(string json) => Invoke("dragValue", json);
        public string Checks(string json) => Invoke("checks", json);
        public string ConvertUnit(string json) => Invoke("convertUnit", json);
        public string Calc(string json) => Invoke("calc", json);
        public string CalcSheet(string json) => Invoke("calcSheet", json);

        /// <summary>
        /// The calculation sheet of <c>ids</c> (default: the page) in <c>mode</c> "csv" (default), "markdown", "latex" or
        /// "table" (the cells of an annotation table): <c>{ text, rows, sources }</c> (Calc 6).
        /// </summary>
        private object CalcSheetCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var ids = ValueIds(r) ?? s.Page.Order.ToList();
            var table = SolveValues(s, r);
            var rows = InventaSketch.Core.CalcSheet.Rows(table, Registry, ids, lang, r.Digits ?? 3);
            var mode = (r.Mode ?? "csv").ToLowerInvariant();
            var text = mode switch
            {
                "csv" => InventaSketch.Core.CalcSheet.Csv(rows, lang),
                "markdown" or "md" => InventaSketch.Core.CalcSheet.Markdown(rows, lang),
                "latex" or "tex" => InventaSketch.Core.CalcSheet.Latex(rows, lang),
                "table" => InventaSketch.Core.CalcSheet.TableCells(rows, lang),
                _ => throw new ArgumentException($"Unknown mode '{r.Mode}' (csv, markdown, latex, table)."),
            };
            return new
            {
                text,
                rows = rows.Count,
                sources = InventaSketch.Core.CalcSheet.Cited(rows).Select(id => new { id, text = ValueSources.Get(id)!.Get(lang) }).ToList(),
            };
        }

        private Lang LangOf(EngineRequest r, EditorSession? s = null) => r.Lang is not null ? Language.Detect(r.Lang) : s?.Lang ?? Lang.En;

        private List<string>? ValueIds(EngineRequest r) => r.Id is not null ? new List<string> { r.Id } : r.Ids;

        private ValueTable SolveValues(EditorSession s, EngineRequest r, IReadOnlyDictionary<VarRef, double>? overrides = null)
        {
            var ids = ValueIds(r) ?? s.Page.Order.ToList();
            return ValueSolver.Solve(s.Doc, Registry, ids, new ValueSolveOptions { Overrides = overrides }, LangOf(r, s));
        }

        private (SketchObject O, ObjectType Type, VarDef Var) ValueTarget(EditorSession s, EngineRequest r)
        {
            var o = s.Doc.Get(r.Id ?? "") ?? throw new KeyNotFoundException($"Unknown object '{r.Id}'.");
            var type = Registry.Type(o.Type) ?? throw new KeyNotFoundException($"Unknown type '{o.Type}'.");
            var key = r.Key ?? throw new ArgumentException("Missing 'key'.");
            var v = ValueModel.Variable(type, key) ?? throw new KeyNotFoundException($"'{o.Type}' has no value '{key}'.");
            return (o, type, v);
        }

        private object ValuesCore(EngineRequest r)
        {
            var s = S(r);
            var table = SolveValues(s, r);
            return ValuesOf(table, LangOf(r, s), r.Digits ?? 3);
        }

        private static ValuesResult ValuesOf(ValueTable table, Lang lang, int digits)
        {
            var result = new ValuesResult { ElapsedMs = table.ElapsedMs, Truncated = table.Truncated };
            foreach (var id in table.ObjectIds)
            {
                var ov = new ObjectValues { Id = id, Status = Camel(table.Status(id).ToString()) };
                foreach (var v in table.Of(id))
                {
                    var unit = table.UnitOf(v);
                    ov.Vars.Add(new VarInfo
                    {
                        Key = v.Def.Key,
                        Name = v.Def.Name.Get(lang),
                        Symbol = v.Def.Symbol.Get(lang),
                        Kind = v.Def.Kind.Id,
                        Relevance = Camel(v.Def.Relevance.ToString()),
                        Unit = unit.Symbol,
                        UnitFactor = unit.Factor,
                        UnitOffset = unit.Offset,
                        Units = Units.DisplayUnits(v.Def.Kind).ToList(),
                        ReadOnly = v.Def.Kind.IsText,
                        Value = v.Value,
                        Number = v.Value is double d ? unit.FromSi(d) : null,
                        Display = v.Value is double x ? ValueFormat.Format(x, unit, lang, digits) : "",
                        Text = v.Text,
                        Provenance = Camel(v.Provenance.ToString()),
                        Formula = table.FormulaOf(v, lang),
                        Relation = v.Via?.Relation.Id,
                        Source = v.Via?.Relation.Source,
                        Tolerance = v.Tolerance,
                        Lo = v.Interval?.Lo,
                        Hi = v.Interval?.Hi,
                        Range = v.Interval is { } iv ? ValueFormat.Format(iv.Lo, unit, lang, digits) + " … " + ValueFormat.Format(iv.Hi, unit, lang, digits) : null,
                        Error = v.Error?.Get(lang),
                        Conflict = v.InConflict,
                        Param = v.Def.ParamKey,
                    });
                }
                ov.Missing = table.Missing(id).Select(m => m.ToList()).ToList();
                foreach (var c in table.ConflictsOf(id))
                {
                    var cv = table.Get(c.Var.ObjectId, c.Var.Key)!;
                    var formula = table.Formula(c.Relation, lang);
                    var vt = ValueFormat.Format(c.Value, table.UnitOf(cv), lang, digits);
                    var it = ValueFormat.Format(c.Implied, table.UnitOf(cv), lang, digits);
                    ov.Conflicts.Add(new ConflictInfo
                    {
                        Relation = c.Relation.Relation.Id, Formula = formula, ObjectId = c.Var.ObjectId, Key = c.Var.Key,
                        Value = c.Value, Implied = c.Implied, ValueText = vt, ImpliedText = it,
                        Involved = c.Involved.Select(x => x.ToString()).ToList(),
                        Message = formula + ": " + cv.Def.Symbol.Get(lang) + " = " + vt + " ≠ " + it,
                    });
                }
                ov.Hint = HintOf(table, id, ov, lang);
                result.Objects.Add(ov);
            }
            return result;
        }

        private static string HintOf(ValueTable table, string id, ObjectValues ov, Lang lang)
        {
            var de = lang == Lang.De;
            string Sym(string key) => ov.Vars.FirstOrDefault(v => v.Key == key)?.Symbol ?? key;
            switch (table.Status(id))
            {
                case ValueStatus.Checked: return "✓";
                case ValueStatus.Conflict: return (de ? "Widerspruch: " : "Conflict: ") + ov.Conflicts.First().Message;
                case ValueStatus.Determined: return "";
            }
            if (ov.Missing.Count == 0) return de ? "Nicht bestimmbar" : "Not determined";
            var n = ov.Missing[0].Count;
            var or = de ? " oder " : " or ";
            var options = ov.Missing.Select(m => string.Join(de ? " und " : " and ", m.Select(Sym))).ToList();
            var list = options.Count == 1 ? options[0] : string.Join(", ", options.Take(options.Count - 1)) + or + options.Last();
            return n == 1
                ? (de ? "Noch 1 Wert: " : "1 more value: ") + list
                : (de ? $"Noch {n} Werte, z. B. " : $"{n} more values, e.g. ") + options[0];
        }

        private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        private object SetValueCore(EngineRequest r)
        {
            var s = S(r);
            var (o, type, v) = ValueTarget(s, r);
            var text = (r.Text ?? (r.Value is { ValueKind: JsonValueKind.String } j ? j.GetString() : null) ?? "").Trim();
            if (text.Length == 0) return ClearValueCore(r);
            var parsed = ValueParser.Parse(text, v.Kind, LangOf(r, s), v.Unit);
            var value = ValueModel.ToParam(type, o, v, parsed);
            return Edit(new EngineRequest { Session = r.Session, Ids = new List<string> { o.Id }, IncludeSvg = r.IncludeSvg }, "Value",
                (st, ids) => Edits.Update(st.Doc, ids, x => Registry.SetParam(x, v.ParamKey, value)));
        }

        private object ClearValueCore(EngineRequest r)
        {
            var s = S(r);
            var (o, _, v) = ValueTarget(s, r);
            if (!o.Params.ContainsKey(v.ParamKey)) return StateOf(s, r);
            return Edit(new EngineRequest { Session = r.Session, Ids = new List<string> { o.Id }, IncludeSvg = r.IncludeSvg }, "Clear value",
                (st, ids) => Edits.Update(st.Doc, ids, x => Registry.SetParam(x, v.ParamKey, null)));
        }

        private object ExplainCore(EngineRequest r)
        {
            var s = S(r);
            var (o, _, v) = ValueTarget(s, r);
            var lang = LangOf(r, s);
            var table = ValueSolver.Solve(s.Doc, Registry, new[] { o.Id }, null, lang);
            var steps = table.Explain(o.Id, v.Key, lang, r.Digits ?? 3);
            return new ExplainResult
            {
                Id = o.Id, Key = v.Key,
                Steps = steps.Select(x => new ExplainStepInfo
                {
                    ObjectId = x.Var.ObjectId, Key = x.Var.Key, Provenance = Camel(x.Provenance.ToString()), Symbol = x.Symbol, Symbolic = x.Symbolic,
                    Substituted = x.Substituted, Result = x.Result, Text = x.Text, Relation = x.RelationId, Source = x.Source,
                }).ToList(),
                Text = string.Join("\n", steps.Select(x => x.Text)),
            };
        }

        private object DragValueCore(EngineRequest r)
        {
            var s = S(r);
            var (o, type, v) = ValueTarget(s, r);
            var lang = LangOf(r, s);
            double si;
            if (r.Value is { ValueKind: JsonValueKind.Number } n) si = n.GetDouble();
            else if (!string.IsNullOrWhiteSpace(r.Text)) si = ValueParser.Parse(r.Text, v.Kind, lang, v.Unit).Value;
            else throw new ArgumentException("Missing 'value' (SI number) or 'text'.");
            if (double.IsNaN(si) || double.IsInfinity(si)) throw new ArgumentException("The value must be a finite number.");
            if (r.Preview == true)
                return ValuesOf(SolveValues(s, r, new Dictionary<VarRef, double> { [new VarRef(o.Id, v.Key)] = si }), lang, r.Digits ?? 3);
            var value = ValueModel.ToParam(type, o, v, si, lang, r.Digits ?? 3);
            return Edit(new EngineRequest { Session = r.Session, Ids = new List<string> { o.Id }, IncludeSvg = r.IncludeSvg }, "Value",
                (st, ids) => Edits.Update(st.Doc, ids, x => Registry.SetParam(x, v.ParamKey, value)));
        }

        private object ChecksCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var table = SolveValues(s, r);
            return ValueSolver.Checks(table, Registry, new RenderContext(s.Doc, lang)).Select(f => new CheckInfo
            {
                Id = f.CheckId, ObjectId = f.ObjectId, Severity = Camel(f.Severity.ToString()), Message = f.Message.Get(lang), Rule = f.Rule, Source = f.Source,
            }).ToList();
        }

        private object ConvertUnitCore(EngineRequest r)
        {
            var lang = LangOf(r);
            var digits = r.Digits ?? 4;
            var kind = Quantities.Get(r.QuantityKind);
            if (r.QuantityKind is not null && kind is null) throw new ArgumentException($"Unknown quantity kind '{r.QuantityKind}'.");
            double si;
            Unit? from = null;
            if (!string.IsNullOrWhiteSpace(r.Text))
            {
                if (kind is not null)
                {
                    var p = ValueParser.Parse(r.Text, kind, lang);
                    si = p.Value; from = p.Unit;
                }
                else
                {
                    var c = UnitCalculator.Evaluate(r.Text, lang, digits);
                    si = c.Value;
                    from = c.Kind is null ? null : UnitCatalog.Find(c.Unit, c.Kind);
                    kind = c.Kind;
                }
            }
            else
            {
                if (r.Value is not { ValueKind: JsonValueKind.Number } n) throw new ArgumentException("Missing 'value' or 'text'.");
                var fromText = r.FromUnit ?? throw new ArgumentException("Missing 'fromUnit'.");
                from = (kind is null ? UnitCatalog.Find(fromText) : UnitCatalog.Find(fromText, kind)) ?? throw new FormatException(UnknownUnitText(fromText, lang));
                kind ??= from.Kind;
                si = from.ToSi(n.GetDouble());
            }
            if (string.IsNullOrWhiteSpace(r.Unit))
            {
                var shown = kind is null ? null : (from ?? kind.DefaultUnit);
                return new ConvertUnitResult
                {
                    Value = shown?.FromSi(si) ?? si, Unit = shown?.Symbol ?? "", Kind = kind?.Id ?? "",
                    Text = shown is null ? ValueFormat.Number(si, lang, digits) : ValueFormat.Format(si, shown, lang, digits),
                    Units = kind is null ? new List<string>() : Units.DisplayUnits(kind).ToList(),
                };
            }
            // Kind-strict: the target must be a unit of the same kind (torque never becomes energy).
            var targets = UnitCatalog.FindAll(r.Unit!);
            if (targets.Count == 0) throw new FormatException(UnknownUnitText(r.Unit!, lang));
            var to = kind is null ? targets.FirstOrDefault(t => from is null || t.Dimension == from.Dimension) : targets.FirstOrDefault(t => t.Kind == kind);
            if (to is null)
            {
                var fromName = kind?.Name.Get(lang) ?? "?";
                throw new FormatException(lang == Lang.De
                    ? $"{fromName} lässt sich nicht in {r.Unit} ({targets[0].Kind.Name.De}) umrechnen."
                    : $"Cannot convert a {fromName.ToLowerInvariant()} into {r.Unit} ({targets[0].Kind.Name.En.ToLowerInvariant()}).");
            }
            var number = to.FromSi(si);
            return new ConvertUnitResult
            {
                Value = number, Unit = to.Symbol, Kind = to.Kind.Id,
                Text = ValueFormat.Number(number, lang, digits) + (to.Symbol.Length == 0 ? "" : " " + to.Symbol),
                Units = Units.DisplayUnits(to.Kind).ToList(),
            };
        }

        private static string UnknownUnitText(string unit, Lang lang) => lang == Lang.De ? $"Unbekannte Einheit „{unit}“." : $"Unknown unit “{unit}”.";

        private object CalcCore(EngineRequest r)
        {
            var lang = LangOf(r);
            var c = UnitCalculator.Evaluate(r.Text, lang, r.Digits ?? 4);
            return new CalcResultInfo
            {
                Value = c.Value, Dimension = c.Dimension.ToString(), Kind = c.Kind?.Id, Unit = c.Unit, Number = c.Number, Text = c.Text, Steps = c.Steps.ToList(),
            };
        }

        private object UnitsCore(EngineRequest r)
        {
            var lang = LangOf(r);
            var kinds = r.QuantityKind is null ? Quantities.All : new[] { Quantities.Get(r.QuantityKind) ?? throw new ArgumentException($"Unknown quantity kind '{r.QuantityKind}'.") };
            return kinds.Select(k => new QuantityKindInfo { Id = k.Id, Name = k.Name.Get(lang), Dimension = k.Dimension.ToString(), Units = Units.DisplayUnits(k).ToList() }).ToList();
        }
    }
}
