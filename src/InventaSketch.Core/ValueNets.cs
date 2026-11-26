using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
namespace InventaSketch.Core
{
    // =============================================================================================
    // Port flows across connections (Calc 4, engineering-calculations.md sections 1.6 and 13): one net builder shared by the
    // modules (Fluid, Process, Building services, Energy). Each module describes its elements and lines in a NetSpec; the
    // nets are built per document and spec and cached.
    // =============================================================================================

    /// <summary>How the values of one class meet in a connected net.</summary>
    public enum NetRule
    {
        /// <summary>Every member shares the value (pressure, voltage): the whole net, branches included.</summary>
        Equal,
        /// <summary>
        /// A through quantity (volume flow, temperature of a stream): shared when at most two elements carry it in the net;
        /// lines join only if the net does not branch.
        /// </summary>
        Flow,
        /// <summary>
        /// A total member is the sum of the part members (parallel strings, feeders on a board, the trunk of a branched pipe);
        /// an unbranched run with at most two elements, or a net without a total, behaves as <see cref="Flow"/>.
        /// </summary>
        Sum,
        /// <summary>
        /// The single total member passes its value to every part member (the supply temperature at a split); an unbranched run
        /// with at most two elements, or a net without exactly one total, behaves as <see cref="Flow"/>.
        /// </summary>
        Spread,
    }

    /// <summary>Role of a port variable in a <see cref="NetRule.Sum"/> class.</summary>
    public enum PortRole { Share, Part, Total }

    /// <summary>A variable an element exposes at one of its ports for a value class ("Q" → the pump's flow).</summary>
    public readonly record struct PortVar(string Class, string Key, PortRole Role = PortRole.Share);

    /// <summary>What a module tells the net builder about its elements and lines.</summary>
    public sealed class NetSpec
    {
        public string Prefix { get; init; } = "";
        /// <summary>Connection lines of the module (their ends dock on ports and on other lines).</summary>
        public Func<SketchObject, bool> IsLine { get; init; } = _ => false;
        /// <summary>Variables of an element at a port (port id "*" for an end docked without a port id, e.g. on a rail).</summary>
        public Func<SketchObject, string, IEnumerable<PortVar>> PortVars { get; init; } = (_, _) => Array.Empty<PortVar>();
        /// <summary>Variables of a line (one value along the whole line).</summary>
        public Func<SketchObject, IEnumerable<PortVar>> LineVars { get; init; } = _ => Array.Empty<PortVar>();
        /// <summary>Groups of ports an element passes a class through unchanged (valves, inline parts, couplings).</summary>
        public Func<SketchObject, string, IEnumerable<string[]>> Through { get; init; } = (_, _) => Array.Empty<string[]>();
        public Func<string, NetRule> Rule { get; init; } = _ => NetRule.Equal;
        /// <summary>Bilingual name of the link relation of a class ("Flow passed on").</summary>
        public Func<string, I18nText> Name { get; init; } = c => new I18nText(c);
        /// <summary>The classes in a fixed order (deterministic relation order).</summary>
        public IReadOnlyList<string> Classes { get; init; } = Array.Empty<string>();
        /// <summary>
        /// The physical key of a class for joins with other modules (<see cref="NetKeys"/>: "Uac" → "U"), null = not joined;
        /// unset = the shared vocabulary <see cref="NetKeys.ForClass"/>.
        /// </summary>
        public Func<string, string?>? Keys { get; init; }
        /// <summary>
        /// The medium of a line of this module ("heating.flow", "heating.return", "air.supply"). Lines of different media
        /// that meet only at a port of another module's element are not joined into one net there (a flow and a return pipe
        /// on the single heat port of an energy heat pump stay two nets). Unset = the line's type id.
        /// </summary>
        public Func<SketchObject, string>? Medium { get; init; }
        /// <summary>
        /// Where lines of several media of this module meet at one port of another module's element: the medium whose net
        /// takes that port's values in the join across modules (true for exactly one of them, e.g. the heating flow at an
        /// energy heat port, which gives the flow temperature). Unset, or not exactly one leading medium at the port: none
        /// of these nets is joined there.
        /// </summary>
        public Func<string, bool>? Leads { get; init; }

        /// <summary>The nets built for this spec, per document (documents are immutable).</summary>
        internal ConditionalWeakTable<SketchDocument, ValueNets> Cache { get; } = new();
    }

    /// <summary>
    /// The nets of a document for one module: line ends docked on ports (attachments with a port id) and on other lines
    /// join ports into nets; elements pass classes through (valves). For each class the members of a net are linked by its
    /// <see cref="NetRule"/>. Built once per document (documents are immutable) and cached.
    /// </summary>
    public sealed class ValueNets
    {
        private readonly Dictionary<string, List<BoundRelation>> _byObject = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> _component = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<int, List<(string Obj, string Port)>>> _ports = new(StringComparer.Ordinal);

        private static readonly Dictionary<string, Relation> Equalities = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Relation> Sums = new(StringComparer.Ordinal);

        private readonly List<NetJunction> _junctions = new();

        /// <summary>The spec the nets were built for.</summary>
        public NetSpec Spec { get; }

        /// <summary>
        /// Places where a net of this module meets an element or line of another module (type id prefix other than
        /// <see cref="NetSpec.Prefix"/>), per class, in class and net order. The nets' own relations ignore them; the solver
        /// joins them by physical key (<see cref="NetKeys"/>).
        /// </summary>
        public IReadOnlyList<NetJunction> Junctions => _junctions;

        /// <summary>The nets asked for during the running solver pass on this thread (null outside a pass).</summary>
        [ThreadStatic] private static List<ValueNets>? _recorder;

        /// <summary>Starts recording the nets asked for on this thread; returns the previous recorder (pass it to <see cref="EndRecording"/>).</summary>
        internal static List<ValueNets>? BeginRecording(List<ValueNets> into)
        {
            var previous = _recorder;
            _recorder = into;
            return previous;
        }

        internal static void EndRecording(List<ValueNets>? previous) => _recorder = previous;

        /// <summary>The nets of <paramref name="doc"/> (cached per document and spec).</summary>
        public static ValueNets Of(SketchDocument doc, NetSpec spec)
        {
            var nets = spec.Cache.GetValue(doc, d => new ValueNets(d, spec));
            if (_recorder is { } rec && !rec.Contains(nets)) rec.Add(nets);
            return nets;
        }

        /// <summary>The module of a type id: the part before the first dot ("energy.pv.string" → "energy").</summary>
        internal static string ModuleOf(string typeId)
        {
            var dot = typeId.IndexOf('.');
            return dot < 0 ? typeId : typeId.Substring(0, dot);
        }

        private static bool IsForeign(NetSpec spec, SketchObject o) => spec.Prefix.Length > 0 && ModuleOf(o.Type) != spec.Prefix;

        /// <summary>Port relations that involve <paramref name="objectId"/>.</summary>
        public IReadOnlyList<BoundRelation> For(string objectId)
            => _byObject.TryGetValue(objectId, out var l) ? l : (IReadOnlyList<BoundRelation>)Array.Empty<BoundRelation>();

        /// <summary>The element ports in the same net as (<paramref name="objectId"/>, <paramref name="port"/>) for a class (itself included).</summary>
        public IReadOnlyList<(string Obj, string Port)> PortsWith(string objectId, string port, string cls)
        {
            if (!_component.TryGetValue(cls, out var comp) || !comp.TryGetValue(PortNode(objectId, port), out var c)) return Array.Empty<(string, string)>();
            return _ports[cls].TryGetValue(c, out var l) ? l : (IReadOnlyList<(string, string)>)Array.Empty<(string, string)>();
        }

        private static string PortNode(string obj, string port) => "P\u0001" + obj + "\u0001" + port;
        private static string LineNode(string obj) => "L\u0001" + obj;
        /// <summary>A port of another module's element as reached by the lines of one medium (one node per medium).</summary>
        private static string ForeignPortNode(string obj, string port, string medium) => PortNode(obj, port) + "\u0001" + medium;

        /// <summary>True if the spec gives the element a variable or a passage at the port (a member then, not a junction).</summary>
        private static bool Describes(NetSpec spec, SketchObject o, string port)
            => spec.Classes.Any(c => spec.PortVars(o, port).Any(pv => pv.Class == c) || spec.Through(o, c).Any(g => g.Contains(port)));

        /// <summary>
        /// True if the net of a foreign port node takes part in the join across modules: always where lines of one medium of
        /// this module reach the port, else only the net of the single leading medium (<see cref="NetSpec.Leads"/>).
        /// </summary>
        private static bool JoinsAt(NetSpec spec, Dictionary<string, List<string>> foreignMedia, string[] parts)
        {
            if (parts.Length < 4 || !foreignMedia.TryGetValue(parts[1] + "\u0001" + parts[2], out var media) || media.Count < 2) return true;
            if (spec.Leads is not { } leads) return false;
            var leading = media.Where(leads).ToList();
            return leading.Count == 1 && leading[0] == parts[3];
        }

        private ValueNets(SketchDocument doc, NetSpec spec)
        {
            Spec = spec;
            var foreignLines = new List<(string LineNode, string Foreign)>();
            var objects = doc.Objects.Values.OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            var lines = objects.Where(o => o.IsPointGeometry && spec.IsLine(o)).ToList();
            var edges = new List<(string A, string B)>();
            var nodes = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            var degree = new Dictionary<string, int>(StringComparer.Ordinal);
            var tees = new HashSet<string>(StringComparer.Ordinal);
            // Media of this module's lines per port of a foreign element (element + "\u0001" + port), in docking order.
            var foreignMedia = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Node(string n) { if (known.Add(n)) nodes.Add(n); }

            foreach (var line in lines)
            {
                var ln = LineNode(line.Id);
                Node(ln);
                foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                {
                    if (a is null || a.ObjectId == line.Id || doc.Get(a.ObjectId) is not { } target) continue;
                    if (target.IsPointGeometry)
                    {
                        if (!spec.IsLine(target))
                        {
                            // A line of another module docked on: a junction, not part of the net.
                            if (IsForeign(spec, target)) foreignLines.Add((ln, target.Id));
                            continue;
                        }
                        Node(LineNode(target.Id));
                        edges.Add((ln, LineNode(target.Id)));
                        tees.Add(ln);
                        continue;
                    }
                    var port = a.AnchorId is { } id && !id.StartsWith("rail", StringComparison.Ordinal) && !id.StartsWith("bar", StringComparison.Ordinal) ? id : "*";
                    string pn;
                    if (IsForeign(spec, target) && !Describes(spec, target, port))
                    {
                        // A port of another module's element joins only lines of the same medium: a flow and a return pipe
                        // docked on one heat port are two nets, each with its own junction.
                        var medium = spec.Medium?.Invoke(line) ?? line.Type;
                        pn = ForeignPortNode(target.Id, port, medium);
                        var fk = target.Id + "\u0001" + port;
                        if (!foreignMedia.TryGetValue(fk, out var media)) foreignMedia[fk] = media = new List<string>();
                        if (!media.Contains(medium)) media.Add(medium);
                    }
                    else pn = PortNode(target.Id, port);
                    Node(pn);
                    edges.Add((ln, pn));
                    degree[pn] = degree.TryGetValue(pn, out var d) ? d + 1 : 1;
                }
            }

            // Ports of the same element that are docked (only those matter for passages).
            var docked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var n in nodes)
            {
                if (n[0] != 'P') continue;
                var parts = n.Split('\u0001');
                if (!docked.TryGetValue(parts[1], out var l)) docked[parts[1]] = l = new List<string>();
                l.Add(parts[2]);
            }

            foreach (var cls in spec.Classes)
            {
                var parent = new Dictionary<string, string>(StringComparer.Ordinal);
                string Find(string x)
                {
                    if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; }
                    if (p == x) return x;
                    var root = Find(p);
                    parent[x] = root;
                    return root;
                }
                void Union(string a, string b)
                {
                    var ra = Find(a);
                    var rb = Find(b);
                    if (ra != rb) parent[ra] = rb;
                }
                foreach (var n in nodes) Find(n);
                foreach (var (a, b) in edges) Union(a, b);
                foreach (var kv in docked)
                {
                    if (doc.Get(kv.Key) is not { } o) continue;
                    foreach (var group in spec.Through(o, cls))
                    {
                        var present = group.Where(g => kv.Value.Contains(g)).Select(g => PortNode(o.Id, g)).ToList();
                        for (var i = 1; i < present.Count; i++) Union(present[0], present[i]);
                    }
                }

                // Components in node order.
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                var comps = new List<List<string>>();
                foreach (var n in nodes)
                {
                    var r = Find(n);
                    if (!index.TryGetValue(r, out var c)) { index[r] = c = comps.Count; comps.Add(new List<string>()); }
                    comps[c].Add(n);
                }
                var compOf = new Dictionary<string, int>(StringComparer.Ordinal);
                var portsOf = new Dictionary<int, List<(string, string)>>();
                foreach (var n in nodes) compOf[n] = index[Find(n)];
                _component[cls] = compOf;
                _ports[cls] = portsOf;

                var rule = spec.Rule(cls);
                var foreignLinesOf = new Dictionary<int, List<string>>();
                foreach (var (lineNode, foreign) in foreignLines)
                {
                    var c = compOf[lineNode];
                    if (!foreignLinesOf.TryGetValue(c, out var fl)) foreignLinesOf[c] = fl = new List<string>();
                    if (!fl.Contains(foreign)) fl.Add(foreign);
                }
                for (var c = 0; c < comps.Count; c++)
                {
                    var elementMembers = new List<(string Obj, string Key, PortRole Role)>();
                    var lineMembers = new List<(string Obj, string Key)>();
                    var foreignPorts = new List<(string Obj, string Port)>();
                    var branched = false;
                    var portList = new List<(string, string)>();
                    foreach (var n in comps[c])
                    {
                        var parts = n.Split('\u0001');
                        if (parts[0] == "L")
                        {
                            if (tees.Contains(n)) branched = true;
                            if (doc.Get(parts[1]) is { } line)
                                foreach (var pv in spec.LineVars(line))
                                    if (pv.Class == cls && !lineMembers.Contains((line.Id, pv.Key))) lineMembers.Add((line.Id, pv.Key));
                            continue;
                        }
                        portList.Add((parts[1], parts[2]));
                        if (degree.TryGetValue(n, out var deg) && deg > 1) branched = true;
                        if (doc.Get(parts[1]) is not { } o) continue;
                        var member = false;
                        foreach (var pv in spec.PortVars(o, parts[2]))
                        {
                            if (pv.Class != cls) continue;
                            member = true;
                            if (!elementMembers.Any(m => m.Obj == o.Id && m.Key == pv.Key)) elementMembers.Add((o.Id, pv.Key, pv.Role));
                        }
                        if (!member && IsForeign(spec, o) && JoinsAt(spec, foreignMedia, parts) && !foreignPorts.Contains((o.Id, parts[2])))
                            foreignPorts.Add((o.Id, parts[2]));
                    }
                    portsOf[c] = portList;
                    elementMembers.Sort((x, y) => string.CompareOrdinal(x.Obj + "\u0001" + x.Key, y.Obj + "\u0001" + y.Key));
                    lineMembers.Sort((x, y) => string.CompareOrdinal(x.Obj + "\u0001" + x.Key, y.Obj + "\u0001" + y.Key));
                    foreignLinesOf.TryGetValue(c, out var foreignLinesHere);
                    if (foreignPorts.Count > 0 || foreignLinesHere is { Count: > 0 })
                        _junctions.Add(new NetJunction(spec, cls, rule, branched,
                            elementMembers.Select(m => new VarRef(m.Obj, m.Key)).ToList(), lineMembers.Select(m => new VarRef(m.Obj, m.Key)).ToList(),
                            foreignPorts, (IReadOnlyList<string>?)foreignLinesHere ?? Array.Empty<string>()));

                    var totals = elementMembers.Where(m => m.Role == PortRole.Total).ToList();
                    var simple = elementMembers.Count <= 2 && !branched;
                    if (rule == NetRule.Sum && totals.Count > 0 && !simple)
                    {
                        var parts = elementMembers.Where(m => m.Role != PortRole.Total).Select(m => new VarRef(m.Obj, m.Key)).ToList();
                        if (parts.Count == 0) continue;
                        foreach (var t in totals) AddSum(spec, cls, new VarRef(t.Obj, t.Key), parts);
                        continue;
                    }
                    if (rule == NetRule.Spread && totals.Count == 1 && !simple)
                    {
                        var total = new VarRef(totals[0].Obj, totals[0].Key);
                        foreach (var m in elementMembers.Where(m => m.Role != PortRole.Total)) AddEquality(spec, cls, total, new VarRef(m.Obj, m.Key));
                        continue;
                    }

                    var chain = new List<VarRef>();
                    if (rule == NetRule.Equal)
                    {
                        chain.AddRange(elementMembers.Select(m => new VarRef(m.Obj, m.Key)));
                        chain.AddRange(lineMembers.Select(m => new VarRef(m.Obj, m.Key)));
                    }
                    else if (elementMembers.Count <= 2)
                    {
                        chain.AddRange(elementMembers.Select(m => new VarRef(m.Obj, m.Key)));
                        if (!branched) chain.AddRange(lineMembers.Select(m => new VarRef(m.Obj, m.Key)));
                    }
                    for (var i = 1; i < chain.Count; i++) AddEquality(spec, cls, chain[0], chain[i]);
                }
            }
        }

        private void Register(BoundRelation b)
        {
            foreach (var id in b.Map.Values.Select(v => v.ObjectId).Distinct())
            {
                if (!_byObject.TryGetValue(id, out var l)) _byObject[id] = l = new List<BoundRelation>();
                l.Add(b);
            }
        }

        private void AddEquality(NetSpec spec, string cls, VarRef a, VarRef b)
        {
            if (a == b) return;
            Relation rel;
            var id = spec.Prefix + ".link." + cls;
            lock (Equalities)
                if (!Equalities.TryGetValue(id, out rel!)) Equalities[id] = rel = Relation.Equality(id, spec.Name(cls));
            Register(new BoundRelation(rel, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = a, ["b"] = b }));
        }

        private void AddSum(NetSpec spec, string cls, VarRef total, IReadOnlyList<VarRef> parts)
        {
            if (parts.Count == 1) { AddEquality(spec, cls, parts[0], total); return; }
            var id = spec.Prefix + ".sum." + cls;
            var key = id + "#" + parts.Count;
            Relation rel;
            lock (Sums)
                if (!Sums.TryGetValue(key, out rel!)) Sums[key] = rel = SumRelation(id, spec.Name(cls), parts.Count);
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["s"] = total };
            for (var i = 0; i < parts.Count; i++) map["a" + (i + 1)] = parts[i];
            Register(new BoundRelation(rel, map));
        }

        /// <summary>s = a1 + … + an, solved for every variable.</summary>
        private static Relation SumRelation(string id, I18nText name, int n)
        {
            var keys = Enumerable.Range(1, n).Select(i => "a" + i).ToArray();
            var sum = string.Join(" + ", keys.Select(k => "{" + k + "}"));
            var rel = new Relation(id, name, "{s} = " + sum, new[] { "s" }.Concat(keys).ToArray()) { Source = "S1" }
                .Solve("s", sum, v => keys.Sum(k => v(k)));
            foreach (var k in keys)
            {
                var others = keys.Where(x => x != k).ToArray();
                var kk = k;
                rel.Solve(kk, "{s} − " + string.Join(" − ", others.Select(x => "{" + x + "}")), v => v("s") - others.Sum(x => v(x)));
            }
            return rel;
        }
    }

    /// <summary>
    /// One net of a module (for one class) that reaches elements or lines of another module: its members as the module's net
    /// sees them and the foreign ports (element, port id) and foreign lines docked into it. Resolved by the solver
    /// (<see cref="ValueSolver"/>, cross-module join).
    /// </summary>
    public sealed class NetJunction
    {
        internal NetJunction(NetSpec spec, string cls, NetRule rule, bool branched, IReadOnlyList<VarRef> elements, IReadOnlyList<VarRef> lines,
            IReadOnlyList<(string Obj, string Port)> foreignPorts, IReadOnlyList<string> foreignLines)
        {
            Spec = spec; Class = cls; Rule = rule; Branched = branched; Elements = elements; Lines = lines; ForeignPorts = foreignPorts; ForeignLines = foreignLines;
        }

        public NetSpec Spec { get; }
        public string Class { get; }
        public NetRule Rule { get; }
        /// <summary>The net branches (a tee of lines or a port with more than one line).</summary>
        public bool Branched { get; }
        /// <summary>Element variables of the class in the net (sorted by object id and key).</summary>
        public IReadOnlyList<VarRef> Elements { get; }
        /// <summary>Line variables of the class in the net.</summary>
        public IReadOnlyList<VarRef> Lines { get; }
        /// <summary>Ports of elements of other modules in the net (that the spec gives no variable of the class there).</summary>
        public IReadOnlyList<(string Obj, string Port)> ForeignPorts { get; }
        /// <summary>Lines of other modules that a line of the net is docked on.</summary>
        public IReadOnlyList<string> ForeignLines { get; }
    }

    /// <summary>
    /// The shared vocabulary of physical keys that joins the values of different modules where their connections meet
    /// (engineering-calculations.md, section "Joins across modules"). A key names one quantity at a port: its quantity kind
    /// (both ends must have exactly this kind), the class names modules use for it (energy "Uac", "Ustr" → U; fluid power "Q"
    /// → V̇) and the variable names looked up on an element whose module describes no ports (an electrical lamp: U, I, P;
    /// for P an electrical input power "Pel" first, so a motor joins with its electrical side).
    /// </summary>
    public static class NetKeys
    {
        public const string Voltage = "U";
        public const string Current = "I";
        public const string Power = "P";
        public const string Frequency = "f";
        public const string Speed = "n";
        public const string Torque = "T";
        public const string VolumeFlow = "Vdot";
        public const string MassFlow = "m";
        public const string Pressure = "p";
        public const string Temperature = "theta";

        private sealed record Entry(string Key, QuantityKind Kind, I18nText Name, string[] Classes, string[] Variables);

        private static readonly Entry[] Table =
        {
            new(Voltage, Quantities.Voltage, new("Voltage passed on between modules", "Spannung modulübergreifend weitergegeben"), new[] { "U", "Uac", "Ustr" }, new[] { "U" }),
            new(Current, Quantities.Current, new("Current passed on between modules", "Strom modulübergreifend weitergegeben"), new[] { "I" }, new[] { "I" }),
            new(Power, Quantities.Power, new("Power passed on between modules", "Leistung modulübergreifend weitergegeben"), new[] { "P", "Pac", "Pdc" }, new[] { "Pel", "P" }),
            new(Frequency, Quantities.Frequency, new("Frequency passed on between modules", "Frequenz modulübergreifend weitergegeben"), new[] { "f" }, new[] { "f" }),
            new(Speed, Quantities.RotationalSpeed, new("Speed passed on between modules", "Drehzahl modulübergreifend weitergegeben"), new[] { "n" }, new[] { "n" }),
            new(Torque, Quantities.Torque, new("Torque passed on between modules", "Drehmoment modulübergreifend weitergegeben"), new[] { "T" }, new[] { "T" }),
            new(VolumeFlow, Quantities.VolumeFlow, new("Volume flow passed on between modules", "Volumenstrom modulübergreifend weitergegeben"), new[] { "Vdot", "Q" }, new[] { "Vdot", "Q" }),
            new(MassFlow, Quantities.MassFlow, new("Mass flow passed on between modules", "Massenstrom modulübergreifend weitergegeben"), new[] { "m" }, new[] { "m" }),
            new(Pressure, Quantities.Pressure, new("Pressure passed on between modules", "Druck modulübergreifend weitergegeben"), new[] { "p" }, new[] { "p" }),
            new(Temperature, Quantities.Temperature, new("Temperature passed on between modules", "Temperatur modulübergreifend weitergegeben"), new[] { "theta" }, new[] { "theta" }),
        };

        private static readonly Dictionary<string, Relation> Links = new(StringComparer.Ordinal);

        /// <summary>Every key, in vocabulary order.</summary>
        public static IReadOnlyList<string> All { get; } = Table.Select(e => e.Key).ToArray();

        private static Entry? Find(string? key) => key is null ? null : Table.FirstOrDefault(e => e.Key == key);

        /// <summary>The key of a net class by the shared vocabulary ("Uac" → "U", "Q" → "Vdot"), or null (not joined: limits as "UocMax").</summary>
        public static string? ForClass(string cls) => Table.FirstOrDefault(e => e.Classes.Contains(cls))?.Key;

        /// <summary>The key of <paramref name="cls"/> in <paramref name="spec"/> (its own <see cref="NetSpec.Keys"/>, else the vocabulary).</summary>
        public static string? Of(NetSpec spec, string cls)
        {
            var key = spec.Keys is { } k ? k(cls) : ForClass(cls);
            return Find(key)?.Key;
        }

        /// <summary>The quantity kind of a key, or null for an unknown key.</summary>
        public static QuantityKind? KindOf(string key) => Find(key)?.Kind;

        /// <summary>Variable names looked up, in order, on an element whose module describes no net ports.</summary>
        public static IReadOnlyList<string> VariableNames(string key) => Find(key)?.Variables ?? Array.Empty<string>();

        /// <summary>The link relation "value passed on between modules" of a key (shared instance).</summary>
        internal static Relation Link(string key)
        {
            lock (Links)
            {
                if (!Links.TryGetValue(key, out var rel))
                    Links[key] = rel = Relation.Equality("net.cross." + key, Find(key)?.Name ?? new I18nText(key + " passed on between modules", key + " modulübergreifend weitergegeben"));
                return rel;
            }
        }
    }
}
