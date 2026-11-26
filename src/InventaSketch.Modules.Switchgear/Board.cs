using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Reads a page (or selection) as an installation: rails with their devices in order, connections of wires and cables
    /// between pins, and the feed tree (which device feeds which circuit, room and device). Objects with the same
    /// designation belong together. Shared by the exports.
    /// </summary>
    internal sealed class Board
    {
        private readonly SketchDocument _doc;
        private readonly IReadOnlyList<SketchObject> _objects;
        private readonly Lang _lang;

        public Board(SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            _doc = doc; _objects = objects; _lang = lang;
        }

        public Lang Lang => _lang;
        public string L(string en, string de) => _lang == Lang.De ? de : en;

        public static DeviceType? Device(SketchObject o) => o.IsPointGeometry ? null : SwitchgearModule.TypeOf(o.Type) as DeviceType;

        /// <summary>A room device of this module or a canonical installation symbol of another module (<see cref="HouseDevice"/>).</summary>
        public static HouseDevice? House(SketchObject o)
            => o.IsPointGeometry ? null : SwitchgearModule.TypeOf(o.Type) is HouseType t ? HouseDevice.Of(t) : HouseDevice.Foreign(o.Type);

        public IEnumerable<(SketchObject Obj, DeviceType Type)> Devices()
        {
            foreach (var o in _objects)
                if (Device(o) is { } t) yield return (o, t);
        }

        public IEnumerable<(SketchObject Obj, HouseDevice Type)> HouseDevices()
        {
            foreach (var o in _objects)
                if (House(o) is { } t) yield return (o, t);
        }

        public IEnumerable<SketchObject> Rails() => _objects.Where(o => o.Type == Switchgear.Rails.RailId && !o.IsPointGeometry);
        public IEnumerable<SketchObject> Cables() => _objects.Where(o => o.Type == Lines.CableId && o.IsPointGeometry);
        public IEnumerable<SketchObject> Wires() => _objects.Where(o => o.Type == Lines.WireId && o.IsPointGeometry);

        // ------------------------------------------------------------------ rails

        /// <summary>A device on a rail: position in modules from the rail's left end and width in modules.</summary>
        internal sealed record Placed(SketchObject Obj, DeviceType Type, double Position, double Width, double U);

        /// <summary>A rail (null: devices on no rail) with its name and devices left to right.</summary>
        internal sealed record Row(SketchObject? Rail, string Name, IReadOnlyList<Placed> Devices);

        public IReadOnlyList<Row> Rows()
        {
            var rails = Rails().OrderBy(r => Math.Round(r.Transform.Y, 3)).ThenBy(r => r.Transform.X).ToList();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<Row>();
            var devices = Devices().Where(d => d.Type.ViewOf(d.Obj) == DeviceType.FrontView).ToList();
            for (var i = 0; i < rails.Count; i++)
            {
                var rail = rails[i];
                var pitch = RailType.Pitch(rail);
                var grid = RailType.Grid(rail);
                var start = grid.Count > 0 ? grid[0] : -rail.Size.W / 2;
                var on = new List<Placed>();
                foreach (var (o, t) in devices)
                {
                    if (taken.Contains(o.Id)) continue;
                    var (u, v) = rail.ToBox(new Vec2(o.Transform.X, o.Transform.Y));
                    if (Math.Abs(u) > 0.5 + 1e-6 || Math.Abs(v) * rail.Size.H > Math.Max(o.Size.H * 0.4, rail.Size.H / 2)) continue;
                    taken.Add(o.Id);
                    // Position on the rail's module grid (the grid is centred on the rail).
                    var left = u * rail.Size.W - o.Size.W / 2 - start;
                    on.Add(new Placed(o, t, pitch > Geo.Eps ? Math.Round(left / pitch * 2, MidpointRounding.AwayFromZero) / 2 : 0,
                        pitch > Geo.Eps ? Math.Round(o.Size.W / pitch * 10) / 10 : t.Modules(o), u));
                }
                var name = rail.Str("ref", "").Trim();
                rows.Add(new Row(rail, name.Length > 0 ? name : L("Rail ", "Schiene ") + (i + 1).ToString(CultureInfo.InvariantCulture), on.OrderBy(p => p.U).ToList()));
            }
            var loose = devices.Where(d => !taken.Contains(d.Obj.Id)).OrderBy(d => Math.Round(d.Obj.Transform.Y, 3)).ThenBy(d => d.Obj.Transform.X)
                .Select(d => new Placed(d.Obj, d.Type, 0, Math.Round(d.Type.Modules(d.Obj) * 10) / 10, 0)).ToList();
            if (loose.Count > 0) rows.Add(new Row(null, L("(not on a rail)", "(auf keiner Schiene)"), loose));
            return rows;
        }

        // ------------------------------------------------------------------ names

        public string TypeName(SketchObject o) => SwitchgearModule.TypeOf(o.Type)?.Name.Get(_lang) ?? o.Type;

        /// <summary>Name of a device as placed: for room devices its kind ("Double socket outlet"), else the type name.</summary>
        public string DeviceName(SketchObject o) => House(o) is { } h ? h.Describe(o, _lang) : TypeName(o);

        public static string Rating(SketchObject o) => Device(o)?.Spec.Rating(o) ?? "";

        /// <summary>Designation of an element for a connection list: terminal "-X1:3", device "-F3", else its name.</summary>
        public string Designation(SketchObject o)
        {
            var rf = o.Str("ref", "").Trim();
            if (Terminals.IsTerminal(o.Type))
            {
                var number = o.Str("number", "").Trim();
                if (rf.Length > 0 || number.Length > 0) return rf + (number.Length > 0 ? ":" + number : "");
            }
            if (rf.Length > 0) return rf;
            if (House(o) is { } h)
            {
                var room = o.Str("room", "").Trim();
                return h.Describe(o, _lang) + (room.Length > 0 ? " (" + room + ")" : "");
            }
            return TypeName(o);
        }

        /// <summary>The far end of a line docked at <paramref name="objectId"/>: "-F3:2", a room device, or the line's own fallback text.</summary>
        public string EndText(SketchObject line, int index, string fallbackKey)
        {
            var a = line.Attach is not null && index >= 0 && index < line.Attach.Count ? line.Attach[index] : null;
            if (a is not null && _doc.Get(a.ObjectId) is { } target)
            {
                var d = Designation(target);
                // Pins of terminals are named by the terminal itself ("-X1:3"); other pins add their id ("-F3:2").
                if (a.AnchorId is { } pin && !Terminals.IsTerminal(target.Type) && House(target) is null) d += ":" + pin;
                else if (a.AnchorId is { } tpin && Terminals.IsTerminal(target.Type) && Terminals.LevelCount(target) > 1) d += "." + tpin;
                return d;
            }
            return line.Str(fallbackKey, "").Trim();
        }

        /// <summary>Lines (wires and cables) docked at pin <paramref name="pin"/> of <paramref name="o"/>, with the index of the docked end.</summary>
        public IEnumerable<(SketchObject Line, int End)> LinesAt(SketchObject o, string pin, bool installationCables = false)
        {
            foreach (var x in _doc.Objects.Values)
            {
                if (!x.IsPointGeometry || !(Lines.IsLine(x.Type) || installationCables && x.Type == HouseDevice.InstallationCableId) || x.Attach is null) continue;
                for (var i = 0; i < x.Attach.Count; i++)
                    if (x.Attach[i] is { } a && a.ObjectId == o.Id && a.AnchorId == pin && (i == 0 || i == x.Attach.Count - 1)) yield return (x, i);
            }
        }

        /// <summary>Index of the other end of a line.</summary>
        public static int Other(SketchObject line, int end) => end == 0 ? (line.Points?.Count ?? 2) - 1 : 0;

        // ------------------------------------------------------------------ feed tree

        /// <summary>A node of the feed tree: all objects of one designation.</summary>
        internal sealed class Node
        {
            public Node(string key, string designation) { Key = key; Designation = designation; }
            public string Key { get; }
            public string Designation { get; }
            public List<SketchObject> Objects { get; } = new();
            public string? Upstream { get; set; }
            public string Source { get; set; } = "";
            public List<Node> Children { get; } = new();
            /// <summary>Loads fed by this node: room devices and drives (frequency inverters).</summary>
            public List<SketchObject> Loads { get; } = new();
            public bool Drawn => Objects.Count > 0;
        }

        private static bool InTree(DeviceRole r) => r is DeviceRole.Protection or DeviceRole.Rcd or DeviceRole.Rcbo or DeviceRole.MainSwitch or DeviceRole.Meter or DeviceRole.Spd;

        /// <summary>
        /// The feed tree: protective devices, RCDs, main switches, meters and surge protection by designation; each one's
        /// upstream device from its "fed from" parameter, else from the wiring (a wire or cable from a bottom pin of the
        /// upstream device to a top pin), else, for circuit breakers, the nearest RCD to the left on the same rail (assumed,
        /// as with a comb busbar). Room devices and drives hang below the designation they name (or are wired to).
        /// </summary>
        public IReadOnlyList<Node> FeedTree()
        {
            var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
            var order = new List<string>();
            string KeyOf(SketchObject o) { var r = o.Str("ref", "").Trim(); return r.Length > 0 ? r : "#" + o.Id; }
            Node Get(string key, string designation)
            {
                if (!nodes.TryGetValue(key, out var n)) { nodes[key] = n = new Node(key, designation); order.Add(key); }
                return n;
            }
            var devices = Devices().Where(d => InTree(d.Type.Spec.Role)).OrderBy(d => Math.Round(d.Obj.Transform.Y, 3)).ThenBy(d => d.Obj.Transform.X).ToList();
            foreach (var (o, _) in devices) Get(KeyOf(o), o.Str("ref", "").Trim()).Objects.Add(o);

            var rows = Rows();
            foreach (var n in nodes.Values.ToList())
            {
                var first = n.Objects[0];
                var t = Device(first)!;
                var feed = n.Objects.Select(o => o.Str("feed", "").Trim()).FirstOrDefault(f => f.Length > 0);
                if (feed is not null) { n.Upstream = feed; n.Source = L("parameter", "Parameter"); continue; }
                var wired = n.Objects.Select(WiredUpstream).FirstOrDefault(x => x is not null);
                if (wired is not null) { n.Upstream = KeyOf(wired); n.Source = L("wiring", "Verdrahtung"); continue; }
                if (t.Spec.Role == DeviceRole.Protection)
                {
                    var row = rows.FirstOrDefault(r => r.Rail is not null && r.Devices.Any(p => p.Obj.Id == first.Id));
                    if (row is not null)
                    {
                        var index = row.Devices.ToList().FindIndex(p => p.Obj.Id == first.Id);
                        var rcd = row.Devices.Take(index).Reverse().FirstOrDefault(p => p.Type.Spec.Role == DeviceRole.Rcd);
                        if (rcd is not null) { n.Upstream = KeyOf(rcd.Obj); n.Source = L("rail order (assumed)", "Schienenreihenfolge (angenommen)"); }
                    }
                }
            }
            // Room devices (also the installation symbols of other modules, HouseDevice) and drives (loads): their circuit, else the
            // protective device a cable or wire from them is docked on.
            foreach (var o in HouseDevices().Select(h => h.Obj).Concat(Devices().Where(d => d.Type.Spec.Role == DeviceRole.Drive).Select(d => d.Obj)))
            {
                var feed = o.Str("feed", "").Trim();
                if (feed.Length == 0 && WiredUpstream(o) is { } up) feed = KeyOf(up);
                if (feed.Length == 0) continue;
                (nodes.TryGetValue(feed, out var n) ? n : Get(feed, feed)).Loads.Add(o);
            }
            // Upstream designations that are not drawn become nodes of their own.
            foreach (var n in nodes.Values.ToList())
                if (n.Upstream is { } u && !nodes.ContainsKey(u)) Get(u, u);
            var roots = new List<Node>();
            foreach (var key in order)
            {
                var n = nodes[key];
                if (n.Upstream is { } u && nodes.TryGetValue(u, out var parent) && parent != n && !IsBelow(parent, n, nodes)) parent.Children.Add(n);
                else roots.Add(n);
            }
            return roots;
        }

        /// <summary>True if <paramref name="candidate"/> lies below <paramref name="n"/> already (a cycle would form).</summary>
        private static bool IsBelow(Node candidate, Node n, Dictionary<string, Node> nodes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var cur = candidate;
            while (cur.Upstream is { } u && nodes.TryGetValue(u, out var p) && seen.Add(cur.Key))
            {
                if (p == n) return true;
                cur = p;
            }
            return false;
        }

        /// <summary>
        /// The device feeding <paramref name="o"/> through a wire or cable: the line docked at a top pin of a rail device
        /// (any pin of a room device) whose other end is docked at a bottom pin of a feed-tree device. Installation symbols of
        /// other modules also count an installation cable of the electrical module (<see cref="HouseDevice.InstallationCableId"/>).
        /// </summary>
        private SketchObject? WiredUpstream(SketchObject o)
        {
            var dt = Device(o);
            var house = dt is null ? House(o) : null;
            IEnumerable<string> pins = dt is not null ? dt.Pins(o).Where(p => p.V < 0).Select(p => p.Id) : house?.Pins ?? new[] { "c" };
            var cables = house is { IsForeign: true };
            foreach (var pin in pins)
                foreach (var (line, end) in LinesAt(o, pin, cables))
                {
                    var other = line.Attach![Other(line, end)];
                    if (other is null || _doc.Get(other.ObjectId) is not { } up || up.Id == o.Id) continue;
                    if (Device(up) is not { } ut || !InTree(ut.Spec.Role)) continue;
                    if (ut.Pins(up).Any(p => p.Id == other.AnchorId && p.V > 0)) return up;
                }
            return null;
        }
    }

    /// <summary>
    /// A room device of the reports and of the circuit values: a room device of this module (<see cref="HouseType"/>) or one
    /// of the canonical installation symbols the room devices give way to in galleries and search (socket outlet, light
    /// switch and luminaire of the electrical module, appliance outlet of the building services). Modules do not reference
    /// each other: those symbols are known by their type ids and read through their parameters, which carry the same names
    /// and meaning as on the room devices here (<c>room</c>, <c>feed</c>, <c>phase</c>, <c>power</c>, <c>current</c>; the
    /// kind parameter names the device in the reports).
    /// </summary>
    internal sealed class HouseDevice
    {
        public const string InstallationCableId = "elec.house.cable";
        public const string ElecSocketId = "elec.house.socket";
        public const string ElecSwitchId = "elec.house.switch";
        public const string ElecLightId = "elec.house.light";
        public const string BsApplianceId = "bs.elec.appliance";

        private readonly HouseType? _own;
        private readonly Func<SketchObject, Lang, string> _describe;

        private HouseDevice(string typeId, HouseType? own, bool load, bool threePhase, IReadOnlyList<string> pins, Func<SketchObject, Lang, string> describe)
        {
            TypeId = typeId; _own = own; IsLoad = load; CanBeThreePhase = threePhase; Pins = pins; _describe = describe;
        }

        public string TypeId { get; }
        /// <summary>True for an installation symbol of another module.</summary>
        public bool IsForeign => _own is null;
        /// <summary>The device takes power: it counts in the circuit values when a power or current is entered on it.</summary>
        public bool IsLoad { get; }
        /// <summary>
        /// The device may be a three-phase load. Symbols of other modules compute their current themselves, single-phase unless
        /// their own phase says three-phase, so for them "from the circuit" never means three-phase (<see cref="SwValues.EffectivePhase"/>).
        /// </summary>
        public bool CanBeThreePhase { get; }
        /// <summary>The connection points a feeding cable or wire docks on.</summary>
        public IReadOnlyList<string> Pins { get; }

        /// <summary>Display name of the device as placed ("Double socket outlet", "Two-way switch").</summary>
        public string Describe(SketchObject o, Lang lang) => _describe(o, lang);

        public static HouseDevice Of(HouseType t) => new(t.Id, t, true, true, OwnPins, t.Describe);

        /// <summary>The installation symbol of another module with this type id, or null.</summary>
        public static HouseDevice? Foreign(string typeId) => ForeignTypes.TryGetValue(typeId, out var d) ? d : null;

        /// <summary>True if <paramref name="typeId"/> is an installation symbol of another module read as a room device.</summary>
        public static bool IsForeignType(string typeId) => ForeignTypes.ContainsKey(typeId);

        private static readonly string[] OwnPins = { "c" };

        private static I18nText T(string en, string de) => new(en, de);

        private static Func<SketchObject, Lang, string> ByKind(string key, string def, I18nText fallback, params (string Value, string En, string De)[] kinds)
            => (o, lang) =>
            {
                var v = o.Str(key, def);
                foreach (var k in kinds) if (k.Value == v) return lang == Lang.De ? k.De : k.En;
                return fallback.Get(lang);
            };

        private static readonly I18nText[] Sockets =
        {
            T("Single socket outlet", "Einfachsteckdose"), T("Double socket outlet", "Zweifachsteckdose"),
            T("Triple socket outlet", "Dreifachsteckdose"), T("Quadruple socket outlet", "Vierfachsteckdose"),
        };

        private static string Socket(SketchObject o, Lang lang)
        {
            var n = Math.Max(1, Math.Min(4, o.Int("gangs", 1)));
            var name = Sockets[n - 1].Get(lang);
            return o.Flag("protected", true) ? name : name + (lang == Lang.De ? " ohne Schutzkontakt" : " (no earth)");
        }

        private static readonly Dictionary<string, HouseDevice> ForeignTypes = new HouseDevice[]
        {
            new(ElecSocketId, null, true, false, new[] { "feed" }, Socket),
            new(ElecSwitchId, null, false, false, new[] { "feed" }, ByKind("kind", "oneway", T("Light switch", "Lichtschalter"),
                ("oneway", "One-way switch", "Ausschalter"), ("twoway", "Two-way switch", "Wechselschalter"),
                ("intermediate", "Intermediate switch", "Kreuzschalter"), ("push", "Push button", "Taster"), ("dimmer", "Dimmer", "Dimmer"))),
            new(ElecLightId, null, true, false, new[] { "n", "e", "s", "w" }, (_, lang) => T("Luminaire", "Leuchte").Get(lang)),
            new(BsApplianceId, null, true, true, new[] { "cable" }, ByKind("kind", "cooker", T("Appliance outlet", "Geräteanschlussdose"),
                ("cooker", "Cooker connection", "Herdanschluss"), ("washer", "Washing machine", "Waschmaschine"), ("dryer", "Tumble dryer", "Wäschetrockner"),
                ("dishwasher", "Dishwasher", "Geschirrspüler"), ("generic", "Appliance", "Gerät"))),
        }.ToDictionary(d => d.TypeId, StringComparer.Ordinal);
    }
}
